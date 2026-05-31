<?php
// ── Secure session cookie flags ───────────────────────────────────────────────
session_set_cookie_params([
    'lifetime' => 0,
    'path'     => '/',
    'secure'   => false,   // set to true once HTTPS is live
    'httponly' => true,
    'samesite' => 'Lax',
]);
session_start();

// ── Change this password ──────────────────────────────────────────────────────
define('ADMIN_PASSWORD', 'TesterEster!11');
// ─────────────────────────────────────────────────────────────────────────────

$error = '';

if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['password'])) {
    // CSRF check — token must match what was issued in the login form
    $token = $_POST['csrf_token'] ?? '';
    if (!hash_equals($_SESSION['csrf_token'] ?? '', $token)) {
        $error = 'Invalid request.';
    } elseif ($_POST['password'] === ADMIN_PASSWORD) {
        session_regenerate_id(true);   // prevent session fixation
        $_SESSION['admin'] = true;
        unset($_SESSION['csrf_token']);
    } else {
        sleep(1);   // slow down brute-force attempts
        $error = 'Wrong password.';
    }
}

// Generate a CSRF token for the login form if one doesn't exist yet
if (!($_SESSION['admin'] ?? false) && empty($_SESSION['csrf_token'])) {
    $_SESSION['csrf_token'] = bin2hex(random_bytes(16));
}

if (isset($_POST['logout'])) {
    session_destroy();
    header('Location: /admin.php');
    exit;
}

$submissions = [];
$file = __DIR__ . '/submissions.json';
if ($_SESSION['admin'] ?? false) {
    if (file_exists($file)) {
        $submissions = json_decode(file_get_contents($file), true) ?? [];
    }
}

$statusLabels = [
    'open'          => ['label' => 'Open',          'color' => '#9090a8'],
    'investigating' => ['label' => 'Investigating',  'color' => '#7ab3e8'],
    'fixed'         => ['label' => 'Fixed',          'color' => '#7ec88a'],
    'wontfix'       => ['label' => "Won't Fix",      'color' => '#c06060'],
    'planned'       => ['label' => 'Planned',        'color' => '#c9a84c'],
    'implemented'   => ['label' => 'Implemented',    'color' => '#7ec88a'],
    'rejected'      => ['label' => 'Rejected',       'color' => '#c06060'],
];

$bugStatuses        = ['open', 'investigating', 'fixed', 'wontfix'];
$suggestionStatuses = ['open', 'planned', 'implemented', 'rejected'];
?>
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>Fun Stuff UO — Admin</title>
<link href="https://fonts.googleapis.com/css2?family=Cinzel:wght@400;600;900&family=Inter:wght@400;500&display=swap" rel="stylesheet">
<style>
*, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
:root {
  --gold: #c9a84c; --gold-dark: #8a6e28;
  --bg: #080810; --bg-2: #0e0e1a; --surface: #12121e; --surface-2: #1a1a2e;
  --border: rgba(201,168,76,0.15); --border-2: rgba(201,168,76,0.3);
  --text: #e8e0d0; --text-muted: #9990a0; --text-dim: #6a6478;
}
body { font-family: 'Inter', sans-serif; background: var(--bg); color: var(--text); min-height: 100vh; padding: 2rem; }
h1 { font-family: 'Cinzel', serif; color: var(--gold); font-size: 1.4rem; margin-bottom: 2rem; letter-spacing: 0.08em; }

/* Login */
.login-wrap { max-width: 360px; margin: 10vh auto; }
.login-wrap h1 { text-align: center; margin-bottom: 2rem; }
.login-input { width: 100%; padding: 0.85rem 1rem; background: var(--surface-2); border: 0.5px solid var(--border-2); color: var(--text); font-size: 1rem; margin-bottom: 1rem; outline: none; }
.login-btn { width: 100%; padding: 0.85rem; background: var(--gold); color: var(--bg); font-family: 'Cinzel', serif; font-size: 0.8rem; letter-spacing: 0.12em; text-transform: uppercase; border: none; cursor: pointer; font-weight: 600; }
.login-btn:hover { background: #e8c97a; }
.login-error { color: #c06060; font-size: 0.85rem; margin-bottom: 1rem; }

/* Admin UI */
.topbar { display: flex; align-items: center; justify-content: space-between; margin-bottom: 2rem; padding-bottom: 1rem; border-bottom: 0.5px solid var(--border); }
.logout-btn { font-size: 0.72rem; letter-spacing: 0.1em; padding: 0.4rem 1rem; border: 1px solid var(--border-2); color: var(--text-dim); background: none; cursor: pointer; text-transform: uppercase; }
.logout-btn:hover { color: var(--gold); border-color: var(--gold-dark); }
.stats { display: flex; gap: 2rem; margin-bottom: 2rem; }
.stat { background: var(--surface); border: 0.5px solid var(--border); padding: 1rem 1.5rem; }
.stat-num { font-family: 'Cinzel', serif; font-size: 1.8rem; color: var(--gold); }
.stat-label { font-size: 0.65rem; letter-spacing: 0.12em; text-transform: uppercase; color: var(--text-dim); margin-top: 0.2rem; }
.filters { display: flex; gap: 0.75rem; margin-bottom: 1.5rem; }
.filter-btn { font-size: 0.65rem; letter-spacing: 0.1em; text-transform: uppercase; padding: 0.4rem 1rem; border: 0.5px solid var(--border); background: none; color: var(--text-dim); cursor: pointer; }
.filter-btn.active, .filter-btn:hover { border-color: var(--gold-dark); color: var(--gold); }
table { width: 100%; border-collapse: collapse; }
th { font-size: 0.62rem; letter-spacing: 0.14em; text-transform: uppercase; color: var(--text-dim); padding: 0.75rem 1rem; text-align: left; border-bottom: 0.5px solid var(--border); }
td { padding: 1rem; border-bottom: 0.5px solid rgba(201,168,76,0.08); vertical-align: top; }
tr:hover td { background: rgba(201,168,76,0.03); }
.type-badge { font-size: 0.55rem; letter-spacing: 0.1em; text-transform: uppercase; padding: 0.2rem 0.5rem; border: 0.5px solid; display: inline-block; }
.type-badge.bug { border-color: #8b1a1a; color: #c06060; }
.type-badge.suggestion { border-color: var(--gold-dark); color: var(--gold); }
.title-cell { font-family: 'Cinzel', serif; font-size: 0.82rem; margin-bottom: 0.3rem; }
.desc-cell { font-size: 0.78rem; color: var(--text-muted); line-height: 1.5; max-width: 420px; }
.meta-cell { font-size: 0.7rem; color: var(--text-dim); }
.status-select { background: var(--surface-2); border: 0.5px solid var(--border-2); color: var(--text); padding: 0.35rem 0.6rem; font-size: 0.72rem; cursor: pointer; width: 100%; }
.status-select:focus { outline: none; border-color: var(--gold-dark); }
.save-btn { margin-top: 0.4rem; width: 100%; padding: 0.35rem; background: none; border: 0.5px solid var(--border); color: var(--text-dim); font-size: 0.65rem; letter-spacing: 0.08em; text-transform: uppercase; cursor: pointer; }
.save-btn:hover { border-color: var(--gold-dark); color: var(--gold); }
.save-btn.saved { border-color: #3a7d44; color: #7ec88a; }
.empty { color: var(--text-dim); font-style: italic; padding: 2rem 0; }
</style>
</head>
<body>

<?php if (!($_SESSION['admin'] ?? false)): ?>
<div class="login-wrap">
  <h1>Admin Access</h1>
  <?php if ($error): ?><p class="login-error"><?= htmlspecialchars($error) ?></p><?php endif; ?>
  <form method="POST">
    <input type="hidden" name="csrf_token" value="<?= htmlspecialchars($_SESSION['csrf_token'] ?? '') ?>">
    <input class="login-input" type="password" name="password" placeholder="Password" autofocus>
    <button class="login-btn" type="submit">Enter</button>
  </form>
</div>

<?php else: ?>
<?php
$bugs    = array_filter($submissions, fn($s) => $s['type'] === 'bug');
$suggs   = array_filter($submissions, fn($s) => $s['type'] === 'suggestion');
$open    = array_filter($submissions, fn($s) => ($s['status'] ?? 'open') === 'open');
?>
<div class="topbar">
  <h1>Fun Stuff UO — Submissions</h1>
  <form method="POST"><button class="logout-btn" name="logout" value="1">Log out</button></form>
</div>

<div class="stats">
  <div class="stat"><div class="stat-num"><?= count($submissions) ?></div><div class="stat-label">Total</div></div>
  <div class="stat"><div class="stat-num"><?= count($bugs) ?></div><div class="stat-label">Bug Reports</div></div>
  <div class="stat"><div class="stat-num"><?= count($suggs) ?></div><div class="stat-label">Suggestions</div></div>
  <div class="stat"><div class="stat-num"><?= count($open) ?></div><div class="stat-label">Open</div></div>
</div>

<div class="filters">
  <button class="filter-btn active" onclick="filterTable('all', this)">All</button>
  <button class="filter-btn" onclick="filterTable('bug', this)">Bugs</button>
  <button class="filter-btn" onclick="filterTable('suggestion', this)">Suggestions</button>
  <button class="filter-btn" onclick="filterTable('open', this)">Open only</button>
</div>

<?php if (empty($submissions)): ?>
  <p class="empty">No submissions yet.</p>
<?php else: ?>
<table id="submissions-table">
  <thead>
    <tr>
      <th>Type</th>
      <th>Title &amp; Description</th>
      <th>From</th>
      <th>Date</th>
      <th style="width:140px">Status</th>
    </tr>
  </thead>
  <tbody>
  <?php foreach ($submissions as $s):
    $type    = $s['type'] ?? 'bug';
    $status  = $s['status'] ?? 'open';
    $options = $type === 'bug' ? $bugStatuses : $suggestionStatuses;
  ?>
  <tr data-type="<?= $type ?>" data-status="<?= $status ?>">
    <td><span class="type-badge <?= $type ?>"><?= $type === 'bug' ? '🐛 Bug' : '💡 Idea' ?></span></td>
    <td>
      <div class="title-cell"><?= htmlspecialchars($s['title']) ?></div>
      <div class="desc-cell"><?= nl2br(htmlspecialchars($s['desc'])) ?></div>
    </td>
    <td class="meta-cell">
      <?= $s['name'] ? htmlspecialchars($s['name']) : '<span style="color:var(--text-dim)">—</span>' ?><br>
      <?= $s['char'] ? htmlspecialchars($s['char']) : '' ?>
    </td>
    <td class="meta-cell"><?= htmlspecialchars($s['date']) ?></td>
    <td>
      <select class="status-select" data-id="<?= htmlspecialchars($s['id']) ?>">
        <?php foreach ($options as $opt): ?>
          <option value="<?= $opt ?>" <?= $status === $opt ? 'selected' : '' ?>>
            <?= $statusLabels[$opt]['label'] ?>
          </option>
        <?php endforeach; ?>
      </select>
      <button class="save-btn" onclick="saveStatus(this)">Save</button>
    </td>
  </tr>
  <?php endforeach; ?>
  </tbody>
</table>
<?php endif; ?>

<script>
function filterTable(filter, btn) {
  document.querySelectorAll('.filter-btn').forEach(b => b.classList.remove('active'));
  btn.classList.add('active');
  document.querySelectorAll('#submissions-table tbody tr').forEach(row => {
    if (filter === 'all') { row.style.display = ''; return; }
    if (filter === 'open') { row.style.display = row.dataset.status === 'open' ? '' : 'none'; return; }
    row.style.display = row.dataset.type === filter ? '' : 'none';
  });
}

async function saveStatus(btn) {
  const select = btn.previousElementSibling;
  const id     = select.dataset.id;
  const status = select.value;
  const data   = new FormData();
  data.append('id', id);
  data.append('status', status);
  const res = await fetch('/update-status.php', { method: 'POST', body: data });
  const json = await res.json();
  if (json.ok) {
    btn.textContent = 'Saved ✓';
    btn.classList.add('saved');
    btn.closest('tr').dataset.status = status;
    setTimeout(() => { btn.textContent = 'Save'; btn.classList.remove('saved'); }, 2000);
  } else {
    btn.textContent = 'Error';
    setTimeout(() => { btn.textContent = 'Save'; }, 2000);
  }
}
</script>
<?php endif; ?>
</body>
</html>

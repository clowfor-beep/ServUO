<?php
session_start();
header('Content-Type: application/json');

if (!isset($_SESSION['admin'])) {
    http_response_code(403);
    echo json_encode(['error' => 'Not authenticated']);
    exit;
}

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    http_response_code(405);
    echo json_encode(['error' => 'Method not allowed']);
    exit;
}

$id     = trim($_POST['id']     ?? '');
$status = trim($_POST['status'] ?? '');

$validStatuses = ['open', 'investigating', 'fixed', 'wontfix', 'planned', 'implemented', 'rejected'];
if (!$id || !in_array($status, $validStatuses)) {
    http_response_code(400);
    echo json_encode(['error' => 'Invalid input']);
    exit;
}

$file = __DIR__ . '/submissions.json';
if (!file_exists($file)) {
    http_response_code(404);
    echo json_encode(['error' => 'No submissions found']);
    exit;
}

// Atomic read-modify-write with exclusive lock.
$fp = fopen($file, 'c+');
if (!$fp) {
    http_response_code(500);
    echo json_encode(['error' => 'Storage unavailable']);
    exit;
}

flock($fp, LOCK_EX);

$submissions = json_decode(stream_get_contents($fp), true) ?? [];

$found = false;
foreach ($submissions as &$s) {
    if ($s['id'] === $id) {
        $s['status'] = $status;
        $found = true;
        break;
    }
}
unset($s);

if (!$found) {
    flock($fp, LOCK_UN);
    fclose($fp);
    http_response_code(404);
    echo json_encode(['error' => 'Submission not found']);
    exit;
}

ftruncate($fp, 0);
rewind($fp);
fwrite($fp, json_encode($submissions, JSON_PRETTY_PRINT));

flock($fp, LOCK_UN);
fclose($fp);

echo json_encode(['ok' => true]);

<?php
header('Content-Type: application/json');
header('Access-Control-Allow-Origin: *');

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    http_response_code(405);
    echo json_encode(['error' => 'Method not allowed']);
    exit;
}

$type  = trim($_POST['type']  ?? '');
$title = trim($_POST['title'] ?? '');
$desc  = trim($_POST['desc']  ?? '');
$name  = trim($_POST['name']  ?? '');
$char  = trim($_POST['char']  ?? '');

if (!in_array($type, ['bug', 'suggestion'])) {
    http_response_code(400);
    echo json_encode(['error' => 'Invalid type']);
    exit;
}
if (strlen($title) < 3 || strlen($title) > 120) {
    http_response_code(400);
    echo json_encode(['error' => 'Title must be 3-120 characters']);
    exit;
}
if (strlen($desc) < 10 || strlen($desc) > 2000) {
    http_response_code(400);
    echo json_encode(['error' => 'Description must be 10-2000 characters']);
    exit;
}

$entry = [
    'id'    => uniqid(),
    'type'  => $type,
    'title' => htmlspecialchars($title, ENT_QUOTES, 'UTF-8'),
    'desc'  => htmlspecialchars($desc,  ENT_QUOTES, 'UTF-8'),
    'name'  => htmlspecialchars($name,  ENT_QUOTES, 'UTF-8'),
    'char'  => htmlspecialchars($char,  ENT_QUOTES, 'UTF-8'),
    'date'  => date('Y-m-d'),
    'ts'    => time(),
];

$file = __DIR__ . '/submissions.json';

// Atomic read-modify-write with exclusive lock so concurrent submissions
// cannot interleave and corrupt the JSON file.
$fp = fopen($file, 'c+');
if (!$fp) {
    http_response_code(500);
    echo json_encode(['error' => 'Storage unavailable']);
    exit;
}

flock($fp, LOCK_EX);

$raw         = stream_get_contents($fp);
$submissions = json_decode($raw, true) ?? [];

array_unshift($submissions, $entry);
$submissions = array_slice($submissions, 0, 500);

ftruncate($fp, 0);
rewind($fp);
fwrite($fp, json_encode($submissions, JSON_PRETTY_PRINT));

flock($fp, LOCK_UN);
fclose($fp);

echo json_encode(['ok' => true]);

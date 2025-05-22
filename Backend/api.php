<?php
header('Content-Type: application/json; charset=UTF-8');

// Enable error logging
ini_set('log_errors', 1);
ini_set('error_log', __DIR__ . '/api_errors.log');

// Database configuration
$db_host = 'host';
$db_user = 'user';
$db_pass = 'pass';
$db_name = 'db';

// Create database connection
$conn = new mysqli($db_host, $db_user, $db_pass, $db_name);

// Check connection
if ($conn->connect_error) {
    error_log("Database connection failed: " . $conn->connect_error);
    http_response_code(500);
    echo json_encode(['error' => 'Database connection failed: ' . $conn->connect_error]);
    exit;
}

// Verify table existence
$result = $conn->query("SHOW TABLES LIKE 'bans'");
if ($result->num_rows === 0) {
    error_log("Table 'bans' does not exist in database '$db_name'");
    http_response_code(500);
    echo json_encode(['error' => 'Table bans does not exist']);
    exit;
}

// Verify API key
$headers = getallheaders();
$api_key = isset($headers['Authorization']) ? str_replace('Bearer ', '', $headers['Authorization']) : '';
if (empty($api_key)) {
    error_log("Missing API key in request");
    http_response_code(401);
    echo json_encode(['error' => 'Missing API key']);
    exit;
}

$stmt = $conn->prepare("SELECT api_key FROM api_keys WHERE api_key = ?");
if (!$stmt) {
    error_log("Prepare failed for API key check: " . $conn->error);
    http_response_code(500);
    echo json_encode(['error' => 'Database error during API key check: ' . $conn->error]);
    exit;
}
$stmt->bind_param("s", $api_key);
$stmt->execute();
$result = $stmt->get_result();
if ($result->num_rows === 0) {
    error_log("Invalid API key: $api_key");
    $stmt->close();
    http_response_code(401);
    echo json_encode(['error' => 'Invalid API key']);
    exit;
}
$stmt->close();

// Handle request method
$method = $_SERVER['REQUEST_METHOD'];

if ($method === 'POST') {
    // Read JSON input
    $input = file_get_contents('php://input');
    if (empty($input)) {
        error_log("No input data received for POST");
        http_response_code(400);
        echo json_encode(['error' => 'No input data']);
        exit;
    }

    $data = json_decode($input, true);
    if (json_last_error() !== JSON_ERROR_NONE) {
        error_log("JSON decode error: " . json_last_error_msg() . " Input: $input");
        http_response_code(400);
        echo json_encode(['error' => 'Invalid JSON: ' . json_last_error_msg()]);
        exit;
    }

    // Validate required fields
    $required_fields = ['PlayerId', 'PlayerName', 'Reason', 'Timestamp', 'Server'];
    foreach ($required_fields as $field) {
        if (!isset($data[$field]) || empty(trim($data[$field]))) {
            error_log("Missing or empty required field: $field");
            http_response_code(400);
            echo json_encode(['error' => "Missing or empty required field: $field"]);
            exit;
        }
    }

    $player_id = $data['PlayerId'];
    $player_name = $data['PlayerName'];
    $reason = $data['Reason'];
    $server = $data['Server'];
    $ip_address = $data['IpAddress'] ?? '';

    // Convert ISO 8601 timestamp to MySQL DATETIME format
    try {
        $timestamp = new DateTime($data['Timestamp']);
        $mysql_timestamp = $timestamp->format('Y-m-d H:i:s');
    } catch (Exception $e) {
        error_log("Invalid timestamp format: " . $data['Timestamp'] . ". Error: " . $e->getMessage());
        http_response_code(400);
        echo json_encode(['error' => 'Invalid timestamp format: ' . $data['Timestamp']]);
        exit;
    }

    // Handle duplicates with ON DUPLICATE KEY UPDATE
    $stmt = $conn->prepare("INSERT INTO bans (player_id, player_name, reason, timestamp, server, ip_address) VALUES (?, ?, ?, ?, ?, ?) ON DUPLICATE KEY UPDATE player_name = ?, reason = ?, timestamp = ?, server = ?, ip_address = ?");
    if (!$stmt) {
        error_log("Prepare failed for INSERT: " . $conn->error);
        http_response_code(500);
        echo json_encode(['error' => 'Database error during INSERT preparation: ' . $conn->error]);
        exit;
    }

    $stmt->bind_param("sssssssssss", $player_id, $player_name, $reason, $mysql_timestamp, $server, $ip_address, $player_name, $reason, $mysql_timestamp, $server, $ip_address);
    if (!$stmt->execute()) {
        error_log("INSERT failed: " . $stmt->error);
        http_response_code(500);
        echo json_encode(['error' => 'Failed to insert ban: ' . $stmt->error]);
        exit;
    }

    $stmt->close();
    error_log("Ban added successfully for player_id: $player_id");
    http_response_code(200);
    echo json_encode(['status' => 'Ban added successfully']);
} elseif ($method === 'GET') {
    $sql = "SELECT player_id, player_name, reason, timestamp, server, ip_address FROM bans ORDER BY timestamp DESC";
    error_log("Executing GET query: $sql");
    $result = $conn->query($sql);
    if ($result === false) {
        error_log("SELECT failed: " . $conn->error);
        http_response_code(500);
        echo json_encode(['error' => 'Database error during SELECT: ' . $conn->error]);
        exit;
    }

    $bans = [];
    $row_count = $result->num_rows;
    error_log("GET query returned $row_count rows");
    while ($row = $result->fetch_assoc()) {
        $bans[] = $row;
    }

    error_log("Returning bans: " . json_encode($bans));
    http_response_code(200);
    echo json_encode($bans);
} elseif ($method === 'DELETE') {
    // Handle unban request
    $player_id = isset($_GET['player_id']) ? $_GET['player_id'] : '';
    if (empty($player_id)) {
        error_log("Missing player_id for DELETE request");
        http_response_code(400);
        echo json_encode(['error' => 'Missing player_id']);
        exit;
    }

    $stmt = $conn->prepare("DELETE FROM bans WHERE player_id = ?");
    if (!$stmt) {
        error_log("Prepare failed for DELETE: " . $conn->error);
        http_response_code(500);
        echo json_encode(['error' => 'Database error during DELETE preparation: ' . $conn->error]);
        exit;
    }

    $stmt->bind_param("s", $player_id);
    if (!$stmt->execute()) {
        error_log("DELETE failed: " . $stmt->error);
        http_response_code(500);
        echo json_encode(['error' => 'Failed to delete ban: ' . $stmt->error]);
        exit;
    }

    $affected_rows = $stmt->affected_rows;
    $stmt->close();

    if ($affected_rows === 0) {
        error_log("No ban found for player_id: $player_id");
        http_response_code(404);
        echo json_encode(['error' => 'No ban found for player_id: ' . $player_id]);
        exit;
    }

    error_log("Ban deleted successfully for player_id: $player_id");
    http_response_code(200);
    echo json_encode(['status' => 'Ban deleted successfully']);
} elseif ($method === 'HEAD') {
    // Test endpoint for database connectivity
    $result = $conn->query("SELECT 1");
    if ($result) {
        error_log("HEAD request: Database connection OK");
        http_response_code(200);
    } else {
        error_log("HEAD request: Database connection failed: " . $conn->error);
        http_response_code(500);
    }
    exit;
} else {
    error_log("Unsupported method: $method");
    http_response_code(405);
    echo json_encode(['error' => 'Method not allowed']);
}

// Close connection
$conn->close();
?>
<?php
header('Content-Type: text/html; charset=UTF-8');

// Database configuration
$db_host = 'host';
$db_user = 'user';
$db_pass = 'pass';
$db_name = 'Db';

// Create database connection
$conn = new mysqli($db_host, $db_user, $db_pass, $db_name);

// Check connection
if ($conn->connect_error) {
    die("Connection failed: " . $conn->connect_error);
}

// Query to fetch all bans
$sql = "SELECT b.player_id, b.player_name, b.reason, b.timestamp, b.server, b.ip_address, a.group_name
        FROM bans b
        LEFT JOIN api_keys a ON b.api_key_id = a.id
        ORDER BY b.timestamp DESC";
$result = $conn->query($sql);

?>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Shared Ban List</title>
    <style>
        body {
            font-family: Arial, sans-serif;
            margin: 20px;
            background-color: #f4f4f4;
        }
        h1 {
            text-align: center;
            color: #333;
        }
        table {
            width: 100%;
            border-collapse: collapse;
            margin-top: 20px;
            background-color: #fff;
            box-shadow: 0 0 10px rgba(0,0,0,0.1);
        }
        th, td {
            padding: 12px;
            text-align: left;
            border: 1px solid #ddd;
        }
        th {
            background-color: #4CAF50;
            color: white;
        }
        tr:nth-child(even) {
            background-color: #f9f9f9;
        }
        tr:hover {
            background-color: #f1f1f1;
        }
        .error {
            color: red;
            text-align: center;
            margin-top: 20px;
        }
    </style>
</head>
<body>
    <h1>Shared Ban List</h1>
    <?php if ($result && $result->num_rows > 0): ?>
        <table>
            <tr>
                <th>Player ID</th>
                <th>Player Name</th>
                <th>Reason</th>
                <th>Timestamp</th>
                <th>Server</th>
                <th>Community/Clan</th>
                <!-- <th>IP Address</th> --> <!-- Commented out to hide IP addresses; uncomment to show -->
            </tr>
            <?php while ($row = $result->fetch_assoc()): ?>
                <tr>
                    <td><?php echo htmlspecialchars($row['player_id']); ?></td>
                    <td><?php echo htmlspecialchars($row['player_name']); ?></td>
                    <td><?php echo htmlspecialchars($row['reason']); ?></td>
                    <td><?php echo htmlspecialchars($row['timestamp']); ?></td>
                    <td><?php echo htmlspecialchars($row['server']); ?></td>
                    <!-- <td><?php echo htmlspecialchars($row['ip_address']); ?></td> --> <!-- Commented out -->
                    <td><?php echo htmlspecialchars($row['group_name']); ?></td>
                </tr>
            <?php endwhile; ?>
        </table>
    <?php else: ?>
        <p class="error">No bans found or database error.</p>
    <?php endif; ?>
</body>
</html>
<?php
// Close connection
$conn->close();
?>

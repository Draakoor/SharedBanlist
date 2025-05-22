using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Database.Models;

namespace SharedBanListPlugin
{
    public class SharedBanListPlugin : IPlugin
    {
        public string Name => "SharedBanListPlugin";
        public float Version => 2.0f;
        public string Author => "Draakoor";

        private readonly HttpClient _httpClient;
        private readonly string _apiEndpoint;
        private readonly string _apiKey;
        private readonly string _banMethod;
        private readonly bool _logNullClients;
        private readonly System.Timers.Timer _syncTimer;
        private IManager _manager;
        private readonly string _configPath = Path.Combine(Utilities.OperatingDirectory, "Configuration", "sharedbanlist.json");
        private readonly string _oldConfigPath = Path.Combine(Utilities.OperatingDirectory, "Plugins", "SharedBanList", "config.json");

        public class PluginConfig
        {
            public string ApiEndpoint { get; set; } = "https://hsngaming.de/api/api.php";
            public string ApiKey { get; set; } = "";
            public string BanMethod { get; set; } = "kick"; // Options: banclient, kick, ipban
            public bool LogNullClients { get; set; } = false; // Toggle logging of null clients
        }

        public SharedBanListPlugin()
        {
            _httpClient = new HttpClient();
            // Load or generate configuration
            PluginConfig config = LoadOrGenerateConfig();
            _apiEndpoint = config.ApiEndpoint;
            _apiKey = config.ApiKey;
            _banMethod = config.BanMethod;
            _logNullClients = config.LogNullClients;
            _syncTimer = new System.Timers.Timer(60000); // Sync every 60 seconds
            _syncTimer.Elapsed += async (s, e) => await SyncBansAsync();
            _syncTimer.AutoReset = true;
        }

        private PluginConfig LoadOrGenerateConfig()
        {
            try
            {
                // Check for old config path and warn if it exists
                if (File.Exists(_oldConfigPath))
                {
                    Console.WriteLine($"Warning: Old config file detected at {_oldConfigPath}. Please move it to {_configPath} and update to the new format.");
                }

                // If config exists, load it
                if (File.Exists(_configPath))
                {
                    string json = File.ReadAllText(_configPath);
                    var config = JsonSerializer.Deserialize<PluginConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (config == null)
                    {
                        Console.WriteLine($"Failed to deserialize config at {_configPath}: Result is null. Generating default config.");
                        return GenerateDefaultConfig();
                    }
                    return config;
                }

                // Config doesn't exist, generate it
                Console.WriteLine($"Config file not found at {_configPath}. Generating default config.");
                return GenerateDefaultConfig();
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Failed to parse config at {_configPath}: {ex.Message} at Line {ex.LineNumber}, Position {ex.BytePositionInLine}. Generating default config.");
                return GenerateDefaultConfig();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load config at {_configPath}: {ex.Message}. Generating default config.");
                return GenerateDefaultConfig();
            }
        }

        private PluginConfig GenerateDefaultConfig()
        {
            var defaultConfig = new PluginConfig();
            try
            {
                // Ensure Configuration directory exists
                string configDir = Path.GetDirectoryName(_configPath);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                    Console.WriteLine($"Created Configuration directory at {configDir}.");
                }

                // Write default config to file
                string json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
                Console.WriteLine($"Generated default config at {_configPath}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to generate config at {_configPath}: {ex.Message}. Using default values.");
            }
            return defaultConfig;
        }

        public Task OnLoadAsync(IManager manager)
        {
            _manager = manager;
            _syncTimer.Start();
            Console.WriteLine($"SharedBanListPlugin loaded. Ban method: {_banMethod}, LogNullClients: {_logNullClients}");
            return Task.CompletedTask;
        }

        public Task OnUnloadAsync()
        {
            _syncTimer.Stop();
            _httpClient.Dispose();
            Console.WriteLine("SharedBanListPlugin unloaded.");
            return Task.CompletedTask;
        }

        public Task OnEventAsync(GameEvent e, Server server)
        {
            if (e.Type == GameEvent.EventType.Ban)
            {
                return OnClientBannedAsync(e, server);
            }
            if (e.Type == GameEvent.EventType.Unban)
            {
                return OnClientUnbannedAsync(e, server);
            }
            if (e.Type == GameEvent.EventType.Connect)
            {
                return OnClientConnectAsync(e, server);
            }
            return Task.CompletedTask;
        }

        public Task OnTickAsync(Server server)
        {
            return Task.CompletedTask; // Not used in this plugin
        }

        private async Task OnClientBannedAsync(GameEvent e, Server server)
        {
            if (e.Target == null)
            {
                if (_logNullClients)
                    Console.WriteLine("OnClientBannedAsync: Target client is null. Skipping ban submission.");
                return;
            }

            if (IsBot(e.Target))
            {
                Console.WriteLine($"OnClientBannedAsync: Target {e.Target.Name ?? "Unknown"} is a bot. Skipping ban submission.");
                return;
            }

            var ban = new
            {
                PlayerId = e.Target.NetworkId.ToString(), // Convert to string for JSON
                PlayerName = e.Target.Name ?? "Unknown",
                Reason = e.Data?.ToString() ?? "No reason provided",
                Timestamp = DateTime.UtcNow.ToString("o"),
                Server = server.ToString(),
                IpAddress = e.Target.IPAddressString ?? "" // Handle null IP
            };

            var content = new StringContent(JsonSerializer.Serialize(ban), Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

            try
            {
                var response = await _httpClient.PostAsync(_apiEndpoint, content);
                response.EnsureSuccessStatusCode();
                Console.WriteLine($"Ban for {ban.PlayerName} sent to shared ban list.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send ban to shared ban list for {ban.PlayerName}: {ex.Message}");
            }
        }

        private async Task OnClientUnbannedAsync(GameEvent e, Server server)
        {
            if (e.Target == null)
            {
                if (_logNullClients)
                    Console.WriteLine("OnClientUnbannedAsync: Target client is null. Skipping unban submission.");
                return;
            }

            if (IsBot(e.Target))
            {
                Console.WriteLine($"OnClientUnbannedAsync: Target {e.Target.Name ?? "Unknown"} is a bot. Skipping unban submission.");
                return;
            }

            var playerId = e.Target.NetworkId.ToString();
            var playerName = e.Target.Name ?? "Unknown";

            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Delete, $"{_apiEndpoint}?player_id={Uri.EscapeDataString(playerId)}");
                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Failed to unban player {playerName} from shared ban list: Response status code {(int)response.StatusCode} ({response.ReasonPhrase}). Response: {responseBody}");
                    return;
                }

                Console.WriteLine($"Player {playerName} unbanned from shared ban list.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to unban player {playerName} from shared ban list: {ex.Message}");
            }
        }

        private async Task OnClientConnectAsync(GameEvent e, Server server)
        {
            try
            {
                if (e.Target == null)
                {
                    if (_logNullClients)
                        Console.WriteLine("OnClientConnectAsync: Target client is null. Skipping ban check.");
                    return;
                }

                if (IsBot(e.Target))
                {
                    Console.WriteLine($"OnClientConnectAsync: Client {e.Target.Name ?? "Unknown"} is a bot. Skipping ban check.");
                    return;
                }

                if (_banMethod == "ipban" && string.IsNullOrEmpty(e.Target.IPAddressString))
                {
                    Console.WriteLine($"OnClientConnectAsync: IP address unavailable for client {e.Target.Name ?? "Unknown"}. Skipping IP ban check.");
                    return;
                }

                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
                var response = await _httpClient.GetAsync(_apiEndpoint);
                response.EnsureSuccessStatusCode();

                var bansJson = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrEmpty(bansJson))
                {
                    Console.WriteLine("OnClientConnectAsync: Empty ban list received from API.");
                    return;
                }

                var bans = JsonSerializer.Deserialize<Ban[]>(bansJson);
                if (bans == null || bans.Length == 0)
                {
                    Console.WriteLine("OnClientConnectAsync: No bans deserialized from API response.");
                    return;
                }

                foreach (var ban in bans)
                {
                    if (ban == null || string.IsNullOrEmpty(ban.PlayerId))
                    {
                        Console.WriteLine("OnClientConnectAsync: Invalid ban entry (null or missing PlayerId).");
                        continue;
                    }

                    if (!long.TryParse(ban.PlayerId, out long playerId))
                    {
                        Console.WriteLine($"OnClientConnectAsync: Invalid PlayerId format: {ban.PlayerId}");
                        continue;
                    }

                    bool isBanned = e.Target.NetworkId == playerId;
                    if (_banMethod == "ipban" && !string.IsNullOrEmpty(ban.IpAddress) && !string.IsNullOrEmpty(e.Target.IPAddressString))
                    {
                        isBanned |= e.Target.IPAddressString == ban.IpAddress;
                    }

                    if (isBanned)
                    {
                        string command = _banMethod switch
                        {
                            "banclient" => $"banclient {ban.PlayerId} SharedBanList: {ban.Reason ?? "No reason provided"}",
                            "ipban" => ban.IpAddress != null ? $"ban {ban.IpAddress} SharedBanList: {ban.Reason ?? "No reason provided"}" : null,
                            _ => $"kick {ban.PlayerId} SharedBanList: {ban.Reason ?? "No reason provided"}" // Default to kick
                        };

                        if (command == null)
                        {
                            Console.WriteLine($"OnClientConnectAsync: Skipping ban for {ban.PlayerName ?? "Unknown"} (IP address missing for ipban).");
                            continue;
                        }

                        try
                        {
                            await Utilities.ExecuteCommandAsync(server, command);
                            Console.WriteLine($"Enforced ban/kick for {ban.PlayerName ?? "Unknown"} ({ban.PlayerId}) on {server.ToString()} using {_banMethod}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to execute {_banMethod} for {ban.PlayerName ?? "Unknown"}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to check bans on connect for client {e.Target?.Name ?? "Unknown"}: {ex.Message}");
            }
        }

        private async Task SyncBansAsync()
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
                var response = await _httpClient.GetAsync(_apiEndpoint);
                response.EnsureSuccessStatusCode();

                var bansJson = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrEmpty(bansJson))
                {
                    Console.WriteLine("SyncBansAsync: Empty ban list received from API.");
                    return;
                }

                var bans = JsonSerializer.Deserialize<Ban[]>(bansJson);
                if (bans == null || bans.Length == 0)
                {
                    Console.WriteLine("SyncBansAsync: No bans deserialized from API response.");
                    return;
                }

                foreach (var ban in bans)
                {
                    if (ban == null || string.IsNullOrEmpty(ban.PlayerId))
                    {
                        Console.WriteLine("SyncBansAsync: Invalid ban entry (null or missing PlayerId).");
                        continue;
                    }

                    if (!long.TryParse(ban.PlayerId, out long playerId))
                    {
                        Console.WriteLine($"SyncBansAsync: Invalid PlayerId format: {ban.PlayerId}");
                        continue;
                    }

                    foreach (var server in _manager.GetServers())
                    {
                        var client = server.GetClientsAsList().FirstOrDefault(c => c != null && !IsBot(c) && (c.NetworkId == playerId || (_banMethod == "ipban" && !string.IsNullOrEmpty(ban.IpAddress) && !string.IsNullOrEmpty(c.IPAddressString) && c.IPAddressString == ban.IpAddress)));
                        if (client == null)
                        {
                            string command = _banMethod switch
                            {
                                "banclient" => $"banclient {ban.PlayerId} SharedBanList: {ban.Reason ?? "No reason provided"}",
                                "ipban" => ban.IpAddress != null ? $"ban {ban.IpAddress} SharedBanList: {ban.Reason ?? "No reason provided"}" : null,
                                _ => $"kick {ban.PlayerId} SharedBanList: {ban.Reason ?? "No reason provided"}" // Default to kick
                            };

                            if (command == null)
                            {
                                Console.WriteLine($"SyncBansAsync: Skipping ban for {ban.PlayerName ?? "Unknown"} (IP address missing for ipban).");
                                continue;
                            }

                            try
                            {
                                await Utilities.ExecuteCommandAsync(server, command);
                                Console.WriteLine($"Applied shared ban/kick for {ban.PlayerName ?? "Unknown"} ({ban.PlayerId}) on {server.ToString()} using {_banMethod}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to execute {_banMethod} for {ban.PlayerName ?? "Unknown"}: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to sync bans: {ex.Message}");
            }
        }

        private bool IsBot(EFClient client)
        {
            if (client == null)
                return false;

            // Check if IsBot is available; otherwise, use heuristics
            if (client.GetType().GetProperty("IsBot") != null)
            {
                bool? isBot = client.GetType().GetProperty("IsBot")?.GetValue(client) as bool?;
                if (isBot == true)
                    return true;
            }

            // Heuristic checks for bots
            return client.NetworkId == 0 || string.IsNullOrEmpty(client.IPAddressString);
        }

        public class Ban
        {
            public string? PlayerId { get; set; }
            public string? PlayerName { get; set; }
            public string? Reason { get; set; }
            public string? Timestamp { get; set; }
            public string? Server { get; set; }
            public string? IpAddress { get; set; }
        }
    }
}
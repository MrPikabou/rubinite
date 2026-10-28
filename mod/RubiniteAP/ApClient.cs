using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RubiniteAP
{
    public sealed class NetworkItem
    {
        public long ItemId;
        public long LocationId;
        public int Player;
        public int Flags;
        public string Name;
    }

    public sealed class ConnectedInfo
    {
        public int Team;
        public int Slot;
        public string SeedName;
        public List<long> CheckedLocations = new List<long>();
        public JObject SlotData;
    }

    public sealed class ApClient : IDisposable
    {
        private const int ClientStatusPlaying = 20;
        private const int ClientStatusGoal = 30;

        private ClientWebSocket _socket;
        private CancellationTokenSource _cts;
        private Task _receiveTask;
        private Task _sendTask;
        private readonly ConcurrentQueue<string> _sendQueue = new ConcurrentQueue<string>();
        private readonly SemaphoreSlim _sendSignal = new SemaphoreSlim(0);
        private readonly ConcurrentQueue<Action> _actions = new ConcurrentQueue<Action>();

        private volatile bool _connected;
        private volatile bool _connecting;
        private bool _intentionalDisconnect;
        private JObject _roomVersion;
        private string _seedName;
        private string _host;
        private int _port;
        private string _slotName;
        private string _password;
        private string _uuid;
        private DateTime _nextReconnect = DateTime.MinValue;

        public bool IsConnected => _connected;
        public bool IsConnecting => _connecting;

        public event Action<ConnectedInfo> Connected;
        public event Action<int, List<NetworkItem>> ItemsReceived;
        public event Action<string> Log;
        public event Action<bool, string> ConnectionChanged;

        public void LogLine(string message)
        {
            // Network threads must never touch Unity state or the log list directly.
            Enqueue(() => Log?.Invoke(message));
        }

        public void Connect(string host, int port, string slotName, string password, string uuid)
        {
            if (_connected || _connecting)
                return;
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(slotName))
            {
                LogLine("Set a server host and slot name first.");
                return;
            }

            _host = host.Trim();
            _port = port > 0 ? port : 38281;
            _slotName = slotName.Trim();
            _password = password ?? "";
            _uuid = string.IsNullOrEmpty(uuid) ? Guid.NewGuid().ToString() : uuid;
            _intentionalDisconnect = false;
            _connecting = true;
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _receiveTask = Task.Run(async () =>
            {
                try
                {
                    var uri = BuildUri(_host, _port);
                    _socket = new ClientWebSocket();
                    _socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
                    LogLine("Connecting to " + uri + " ...");
                    await _socket.ConnectAsync(uri, token).ConfigureAwait(false);
                    _connected = true;
                    _connecting = false;
                    Enqueue(() => ConnectionChanged?.Invoke(true, "Connected"));
                    _sendTask = Task.Run(() => SendLoop(token));
                    await ReceiveLoop(token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (!_intentionalDisconnect)
                        LogLine("Connection failed: " + ex.Message);
                }
                finally
                {
                    _connecting = false;
                    var wasConnected = _connected;
                    _connected = false;
                    if (!_intentionalDisconnect)
                    {
                        Enqueue(() => ConnectionChanged?.Invoke(false, "Disconnected"));
                        if (wasConnected)
                            LogLine("Disconnected from Archipelago.");
                        _nextReconnect = DateTime.UtcNow.AddSeconds(5);
                    }
                    else
                    {
                        Enqueue(() => ConnectionChanged?.Invoke(false, "Disconnected"));
                    }
                }
            }, token);
        }

        public void Disconnect()
        {
            _intentionalDisconnect = true;
            _connected = false;
            _connecting = false;
            try { _cts?.Cancel(); } catch { }
            try { _socket?.Abort(); } catch { }
            try { _socket?.Dispose(); } catch { }
            _socket = null;
            Enqueue(() => ConnectionChanged?.Invoke(false, "Disconnected"));
        }

        public void Tick()
        {
            if (_intentionalDisconnect || _connected || _connecting)
                return;
            if (string.IsNullOrWhiteSpace(_slotName))
                return;
            if (DateTime.UtcNow < _nextReconnect)
                return;
            _nextReconnect = DateTime.UtcNow.AddSeconds(5);
            Connect(_host, _port, _slotName, _password, _uuid);
        }

        public void Pump()
        {
            while (_actions.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception ex) { LogLine("Client error: " + ex.Message); }
            }
        }

        private static Uri BuildUri(string host, int port)
        {
            if (host.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) ||
                host.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
                return new Uri(host);
            return new Uri("ws://" + host + ":" + port);
        }

        private void Enqueue(Action action)
        {
            _actions.Enqueue(action);
        }

        private void SendPacket(JObject packet)
        {
            var array = new JArray { packet };
            _sendQueue.Enqueue(array.ToString(Formatting.None));
            _sendSignal.Release();
        }

        private async Task SendLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && _socket != null && _socket.State == WebSocketState.Open)
                {
                    await _sendSignal.WaitAsync(token).ConfigureAwait(false);
                    while (_sendQueue.TryDequeue(out var message))
                    {
                        var bytes = Encoding.UTF8.GetBytes(message);
                        await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token)
                            .ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!_intentionalDisconnect)
                    LogLine("Send error: " + ex.Message);
            }
        }

        private async Task ReceiveLoop(CancellationToken token)
        {
            var buffer = new byte[64 * 1024];
            var builder = new StringBuilder();
            try
            {
                while (!token.IsCancellationRequested && _socket != null && _socket.State == WebSocketState.Open)
                {
                    var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                        if (result.EndOfMessage)
                        {
                            var text = builder.ToString();
                            builder.Clear();
                            HandleRaw(text);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException) { }
            catch (Exception ex)
            {
                if (!_intentionalDisconnect)
                    LogLine("Receive error: " + ex.Message);
            }
        }

        private void HandleRaw(string text)
        {
            try
            {
                var token = JToken.Parse(text);
                if (token is JArray array)
                {
                    foreach (var entry in array)
                        if (entry is JObject packet)
                            HandlePacket(packet);
                }
                else if (token is JObject packet)
                {
                    HandlePacket(packet);
                }
            }
            catch (Exception ex)
            {
                LogLine("Bad server message: " + ex.Message);
            }
        }

        private void HandlePacket(JObject packet)
        {
            var cmd = (string)packet["cmd"];
            if (string.IsNullOrEmpty(cmd))
                return;

            switch (cmd)
            {
                case "RoomInfo":
                    _roomVersion = packet["version"] as JObject;
                    if (packet["seed_name"] != null && packet["seed_name"].Type == JTokenType.String)
                        _seedName = (string)packet["seed_name"];
                    SendConnect();
                    break;

                case "Connected":
                    HandleConnected(packet);
                    break;

                case "ReceivedItems":
                    HandleReceivedItems(packet);
                    break;

                case "PrintJSON":
                    HandlePrintJson(packet);
                    break;

                case "ConnectionRefused":
                    HandleRefused(packet);
                    break;

                case "InvalidPacket":
                    Enqueue(() => LogLine("Server rejected a packet: " + packet["type"] + " " + packet["text"]));
                    break;

                case "Bounced":
                    // DeathLink and other bounce features are not used by this client.
                    break;
            }
        }

        private void SendConnect()
        {
            var version = _roomVersion ?? new JObject
            {
                ["major"] = 0,
                ["minor"] = 6,
                ["build"] = 7,
                ["class"] = "Version"
            };
            var packet = new JObject
            {
                ["cmd"] = "Connect",
                ["game"] = GameData.GameName,
                ["name"] = _slotName,
                ["uuid"] = _uuid,
                ["version"] = version,
                ["items_handling"] = 0b111,
                ["tags"] = new JArray("AP"),
                ["slot_data"] = true
            };
            packet["password"] = _password ?? "";
            SendPacket(packet);
        }

        private void HandleConnected(JObject packet)
        {
            var info = new ConnectedInfo
            {
                Team = (int?)packet["team"] ?? 0,
                Slot = (int?)packet["slot"] ?? 0,
                SeedName = packet["seed_name"] != null && packet["seed_name"].Type == JTokenType.String
                    ? (string)packet["seed_name"]
                    : (_seedName ?? ""),
                SlotData = packet["slot_data"] as JObject
            };
            if (packet["checked_locations"] is JArray checkedLocations)
                foreach (var id in checkedLocations)
                    info.CheckedLocations.Add((long)id);

            Enqueue(() =>
            {
                ConnectionChanged?.Invoke(true, "Connected to slot " + info.Slot);
                Connected?.Invoke(info);
            });
            SendStatus(ClientStatusPlaying);
        }

        private void HandleReceivedItems(JObject packet)
        {
            var index = (int?)packet["index"] ?? 0;
            var list = new List<NetworkItem>();
            if (packet["items"] is JArray items)
            {
                foreach (var entry in items)
                {
                    if (!(entry is JObject item)) continue;
                    list.Add(new NetworkItem
                    {
                        ItemId = (long?)item["item"] ?? 0,
                        LocationId = (long?)item["location"] ?? 0,
                        Player = (int?)item["player"] ?? 0,
                        Flags = (int?)item["flags"] ?? 0
                    });
                }
            }

            Enqueue(() => ItemsReceived?.Invoke(index, list));
        }

        private void HandlePrintJson(JObject packet)
        {
            var data = packet["data"];
            var text = JsonText(data);
            if (!string.IsNullOrEmpty(text))
                Enqueue(() => LogLine(text));
        }

        private static string JsonText(JToken token)
        {
            if (token == null)
                return null;
            if (token.Type == JTokenType.String)
                return (string)token;
            if (token is JObject obj)
                return (string)obj["text"];
            if (token is JArray array)
            {
                var builder = new StringBuilder();
                foreach (var part in array)
                    builder.Append(JsonText(part));
                return builder.ToString();
            }
            return null;
        }

        private void HandleRefused(JObject packet)
        {
            var errors = packet["errors"] is JArray arr ? arr.ToString(Formatting.None) : "unknown error";
            Enqueue(() =>
            {
                LogLine("Connection refused: " + errors);
                ConnectionChanged?.Invoke(false, "Refused: " + errors);
            });
            _intentionalDisconnect = true;
            try { _socket?.Abort(); } catch { }
        }

        public void SendStatus(int status)
        {
            SendPacket(new JObject { ["cmd"] = "StatusUpdate", ["status"] = status });
        }

        public void SendLocationChecks(IEnumerable<long> ids)
        {
            var array = new JArray();
            foreach (var id in ids)
                array.Add(id);
            if (array.Count == 0)
                return;
            SendPacket(new JObject { ["cmd"] = "LocationChecks", ["locations"] = array });
        }

        public void SendGoal()
        {
            SendStatus(ClientStatusGoal);
        }

        public void SendStatusUpdate()
        {
            if (_connected)
                SendStatus(ClientStatusPlaying);
        }

        public void SendSay(string message)
        {
            SendPacket(new JObject { ["cmd"] = "Say", ["text"] = message });
        }

        public void Dispose()
        {
            Disconnect();
            try { _receiveTask?.Wait(250); } catch { }
            try { _sendTask?.Wait(250); } catch { }
            _cts?.Dispose();
            _sendSignal.Dispose();
        }
    }
}

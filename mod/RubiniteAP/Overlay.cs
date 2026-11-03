using System;
using System.Linq;
using UnityEngine;

namespace RubiniteAP
{
    public sealed class Overlay
    {
        private const int WindowId = 0x5A17;
        private readonly GameBridge _bridge;
        private readonly ApClient _client;
        private readonly Settings _settings;
        private Rect _window = new Rect(24f, 24f, 500f, 620f);
        private bool _visible;
        private Vector2 _logScroll;

        private int _tab;
        private static readonly string[] TabNames = { "Settings", "Item Tracker" };
        private Vector2 _trackerScrollReceived;
        private Vector2 _trackerScrollSent;
        private bool _onlyOtherPlayers;
        private const int TrackerMaxItems = 100;

        public Overlay(GameBridge bridge, ApClient client, Settings settings)
        {
            _bridge = bridge;
            _client = client;
            _settings = settings;
        }

        public void Toggle()
        {
            _visible = !_visible;
        }

        public void Draw()
        {
            if (!_visible) return;
            _window = GUILayout.Window(WindowId, _window, DrawWindow, "Rubinite Archipelago");
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();

            _tab = GUILayout.Toolbar(_tab, TabNames);
            GUILayout.Space(4f);

            if (_tab == 0) DrawSettingsTab();
            else DrawTrackerTab();

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private void DrawSettingsTab()
        {
            GUILayout.Label("Server host");
            _settings.Server.Value = GUILayout.TextField(_settings.Server.Value ?? "localhost", GUILayout.Height(22f));
            GUILayout.Label("Port");
            var portText = GUILayout.TextField(_settings.Port.Value.ToString(), GUILayout.Height(22f));
            if (int.TryParse(portText, out var port)) _settings.Port.Value = port;
            GUILayout.Label("Slot name");
            _settings.Slot.Value = GUILayout.TextField(_settings.Slot.Value ?? "", GUILayout.Height(22f));
            GUILayout.Label("Password (optional)");
            _settings.Password.Value = GUILayout.PasswordField(_settings.Password.Value ?? "", '*', GUILayout.Height(22f));

            GUILayout.Space(4f);
            var newDeathLink = GUILayout.Toggle(
                _settings.DeathLinkEnabled.Value,
                "Enable DeathLink (only takes effect if seed also allows it)");
            if (newDeathLink != _settings.DeathLinkEnabled.Value)
            {
                _settings.DeathLinkEnabled.Value = newDeathLink;
                _client.SetDeathLinkEnabled(newDeathLink);
                SaveSettings();
            }

            GUILayout.BeginHorizontal();
            GUI.enabled = !_client.IsConnected;
            if (GUILayout.Button("Connect", GUILayout.Height(26f)))
            {
                SaveSettings();
                _client.SetDeathLinkEnabled(_settings.DeathLinkEnabled.Value);
                _client.Connect(_settings.Server.Value, _settings.Port.Value, _settings.Slot.Value,
                    _settings.Password.Value, _bridge.State.Uuid);
            }
            GUI.enabled = _client.IsConnected;
            if (GUILayout.Button("Disconnect", GUILayout.Height(26f)))
                _client.Disconnect();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUILayout.Label(_bridge.StatusMessage ?? "");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Sigils: " + CountOwned(GameData.Sigils));
            GUILayout.Label("Skills: " + CountPrefix("Skill "));
            GUILayout.Label("Talismans: " + CountPrefix("Talisman: "));
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUI.enabled = _client.IsConnected && !_bridge.ApRunActive;
            if (GUILayout.Button("Start Archipelago Run", GUILayout.Height(30f)))
            {
                SaveSettings();
                _bridge.StartRun();
            }
            GUI.enabled = true;
            if (_bridge.ApRunActive)
                GUILayout.Label("Run active. Return to title and reconnect to restart.");

            GUILayout.Space(6f);
            GUILayout.Label("Log");
            _logScroll = GUILayout.BeginScrollView(_logScroll, GUI.skin.box, GUILayout.Height(190f));
            var lines = _bridge.LogLines;
            var first = Math.Max(0, lines.Count - 40);
            for (var i = first; i < lines.Count; i++)
                GUILayout.Label(lines[i]);
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Status", GUILayout.Height(24f)))
                _client.SendStatusUpdate();
            if (GUILayout.Button("Close (F8)", GUILayout.Height(24f)))
                _visible = false;
            GUILayout.EndHorizontal();
        }

        private void DrawTrackerTab()
        {
            _onlyOtherPlayers = GUILayout.Toggle(
                _onlyOtherPlayers,
                "Only show items involving other players");

            var received = _bridge.GetReceivedHistory();
            var sent = _bridge.GetSentHistory().ToList();

            int receivedCount = _onlyOtherPlayers
                ? received.Count(r => r.SenderSlot != _bridge.MySlot)
                : received.Count;
            int sentCount = _onlyOtherPlayers
                ? sent.Count(s => s.RecipientSlot != _bridge.MySlot)
                : sent.Count;

            // --- Received ---
            GUILayout.Label($"Received ({receivedCount})");
            _trackerScrollReceived = GUILayout.BeginScrollView(
                _trackerScrollReceived, GUI.skin.box, GUILayout.Height(200f));

            int shown = 0;
            for (int i = received.Count - 1; i >= 0 && shown < TrackerMaxItems; i--)
            {
                var r = received[i];
                if (_onlyOtherPlayers && r.SenderSlot == _bridge.MySlot) continue;
                var sender = _bridge.GetPlayerName(r.SenderSlot);
                GUILayout.Label($"{r.ItemName}  ←  {sender}");
                shown++;
            }
            GUILayout.EndScrollView();

            // --- Sent ---
            GUILayout.Label($"Sent ({sentCount})");
            _trackerScrollSent = GUILayout.BeginScrollView(
                _trackerScrollSent, GUI.skin.box, GUILayout.Height(200f));

            shown = 0;
            for (int i = sent.Count - 1; i >= 0 && shown < TrackerMaxItems; i--)
            {
                var s = sent[i];
                if (_onlyOtherPlayers && s.RecipientSlot == _bridge.MySlot) continue;
                var recipient = _bridge.GetPlayerName(s.RecipientSlot);
                var loc = s.LocationDisplayName ?? "";
                GUILayout.Label($"{s.ItemName}  →  {recipient}   ({loc})");
                shown++;
            }
            GUILayout.EndScrollView();

            GUILayout.Space(2f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Close (F8)", GUILayout.Height(24f)))
                _visible = false;
            GUILayout.EndHorizontal();
        }

        private void SaveSettings()
        {
            try { RubiniteAPPlugin.Instance.Config.Save(); }
            catch (Exception ex) { _bridge.AddLog("Could not save settings: " + ex.Message); }
        }

        private int CountOwned(string[] names)
        {
            var count = 0;
            foreach (var name in names)
                if (_bridge.State.HasItem(name)) count++;
            return count;
        }

        private int CountPrefix(string prefix)
        {
            var count = 0;
            foreach (var name in _bridge.State.Items)
                if (name.StartsWith(prefix, StringComparison.Ordinal)) count++;
            return count;
        }
    }
}

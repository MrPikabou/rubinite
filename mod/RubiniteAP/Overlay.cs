using System;
using UnityEngine;

namespace RubiniteAP
{
    public sealed class Overlay
    {
        private const int WindowId = 0x5A17;
        private readonly GameBridge _bridge;
        private readonly ApClient _client;
        private readonly Settings _settings;
        private Rect _window = new Rect(24f, 24f, 470f, 540f);
        private bool _visible;
        private Vector2 _logScroll;

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
            if (!_visible)
                return;
            _window = GUILayout.Window(WindowId, _window, DrawWindow, "Rubinite Archipelago");
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();

            GUILayout.Label("Server host");
            _settings.Server.Value = GUILayout.TextField(_settings.Server.Value ?? "localhost", GUILayout.Height(22f));
            GUILayout.Label("Port");
            var portText = GUILayout.TextField(_settings.Port.Value.ToString(), GUILayout.Height(22f));
            if (int.TryParse(portText, out var port)) _settings.Port.Value = port;
            GUILayout.Label("Slot name");
            _settings.Slot.Value = GUILayout.TextField(_settings.Slot.Value ?? "", GUILayout.Height(22f));
            GUILayout.Label("Password (optional)");
            _settings.Password.Value = GUILayout.PasswordField(_settings.Password.Value ?? "", '*', GUILayout.Height(22f));

            GUILayout.BeginHorizontal();
            GUI.enabled = !_client.IsConnected;
            if (GUILayout.Button("Connect", GUILayout.Height(26f)))
            {
                SaveSettings();
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

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
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

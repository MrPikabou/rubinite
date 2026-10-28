using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace RubiniteAP
{
    [BepInPlugin(Guid, "Rubinite Archipelago", Version)]
    public sealed class RubiniteAPPlugin : BaseUnityPlugin
    {
        public const string Guid = "rubinite.archipelago";
        public const string Version = "0.1.0";

        public static RubiniteAPPlugin Instance { get; private set; }
        public static ManualLogSource Log => Instance != null ? Instance.Logger : null;

        public Settings Settings { get; private set; }
        public ApState State { get; private set; }
        public ApClient Client { get; private set; }
        public GameBridge Bridge { get; private set; }
        public Overlay Overlay { get; private set; }

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            useGUILayout = true;

            Settings = new Settings();
            Settings.Bind(Config);

            State = ApState.Load();
            State.EnsureUuid();

            Client = new ApClient();
            Bridge = new GameBridge(State, Client);
            Overlay = new Overlay(Bridge, Client, Settings);

            _harmony = new Harmony(Guid);
            try
            {
                _harmony.PatchAll(typeof(RubiniteAPPlugin).Assembly);
                Bridge.AddLog("Rubinite Archipelago " + Version + " loaded. F8 opens the overlay.");
            }
            catch (Exception ex)
            {
                Logger.LogError("Harmony patch failure: " + ex);
            }

            if (Settings.ShowOverlay.Value)
                Overlay.Toggle();

            if (Settings.AutoConnect.Value && !string.IsNullOrWhiteSpace(Settings.Slot.Value))
            {
                Client.Connect(Settings.Server.Value, Settings.Port.Value, Settings.Slot.Value,
                    Settings.Password.Value, State.Uuid);
            }
        }

        private void Update()
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.F8))
                    Overlay.Toggle();
            }
            catch
            {
                // The game may be running with an input backend that disables legacy Input.
            }

            Client.Tick();
            Client.Pump();
            Bridge.Tick();
        }

        private void OnGUI()
        {
            Overlay?.Draw();
        }

        private void OnDestroy()
        {
            try { Client?.Dispose(); } catch { }
            try { _harmony?.UnpatchSelf(); } catch { }
            Instance = null;
        }
    }
}

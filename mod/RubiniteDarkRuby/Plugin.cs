using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace RubiniteDarkRuby
{
    [BepInPlugin(Guid, "Rubinite Dark Ruby", Version)]
    public sealed class RubiniteDarkRubyPlugin : BaseUnityPlugin
    {
        public const string Guid = "rubinite.darkruby";
        public const string Version = "0.1.0";

        public static RubiniteDarkRubyPlugin Instance { get; private set; }
        public static ManualLogSource Log => Instance != null ? Instance.Logger : null;

        public Settings Settings { get; private set; }
        public DarkRubyState State { get; private set; }
        public DarkRubyModule Module { get; private set; }

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            useGUILayout = true;

            Settings = new Settings();
            Settings.Bind(Config);

            State = DarkRubyState.Load();

            Module = new DarkRubyModule(this);

            _harmony = new Harmony(Guid);
            try
            {
                _harmony.PatchAll(typeof(RubiniteDarkRubyPlugin).Assembly);
                Logger.LogInfo("Rubinite Dark Ruby " + Version + " loaded.");
                Logger.LogInfo("  EnableMod=" + Settings.EnableMod.Value
                             + " SpawnInCA=" + Settings.SpawnPortalInChallengeArea.Value
                             + " CustomSkin=" + Settings.UseCustomSkin.Value);
            }
            catch (Exception ex)
            {
                Logger.LogError("Harmony patch failure: " + ex);
            }
        }

        private void Update()
        {
            try { Module?.Tick(); }
            catch (Exception ex) { Logger.LogWarning("Module.Tick: " + ex.Message); }
        }

        private void OnDestroy()
        {
            try { _harmony?.UnpatchSelf(); } catch { }
            Instance = null;
        }
    }
}

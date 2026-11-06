using System;
using System.Reflection;
using BepInEx.Bootstrap;

namespace RubiniteDarkRuby
{
    /// <summary>
    /// Reflection-based bridge to the Archipelago mod. Detects whether AP is
    /// installed and whether a run is currently active. When both are true,
    /// the DR mod suppresses its standalone DR content (podium spawn, timer,
    /// win flow) and lets the AP mod handle everything. Skin, leaderboard
    /// block, and illusion block remain active in all modes.
    /// </summary>
    internal static class ApBridgeHook
    {
        private const string ApGuid = "rubinite.archipelago";

        private static bool _initAttempted;
        private static bool _apModPresent;
        private static object _gameBridgeInstance;
        private static PropertyInfo _apRunActiveProp;
        private static PropertyInfo _enableDarkRubyProp;

        public static bool ApModPresent
        {
            get { EnsureInit(); return _apModPresent; }
        }

        public static bool ApRunActive
        {
            get
            {
                EnsureInit();
                if (_gameBridgeInstance == null || _apRunActiveProp == null) return false;
                try { return (bool)_apRunActiveProp.GetValue(_gameBridgeInstance); }
                catch { return false; }
            }
        }

        public static bool EnableDarkRuby
        {
            get
            {
                EnsureInit();
                if (_gameBridgeInstance == null || _enableDarkRubyProp == null) return false;
                try { return (bool)_enableDarkRubyProp.GetValue(_gameBridgeInstance); }
                catch { return false; }
            }
        }

        /// <summary>
        /// True when the AP mod is loaded and a run is active. In this case the
        /// AP mod owns DR content for the current run, and the DR mod should only
        /// do visuals + blocks — no podium, no timer, no checks.
        /// </summary>
        public static bool ShouldSuppressStandalone =>
            ApModPresent && ApRunActive;

        private static void EnsureInit()
        {
            if (_initAttempted) return;

            if (!Chainloader.PluginInfos.TryGetValue(ApGuid, out var pluginInfo))
            {
                return;
            }

            _initAttempted = true;
            _apModPresent = true;

            try
            {
                var pluginInstance = pluginInfo.Instance;
                if (pluginInstance == null)
                {
                    RubiniteDarkRubyPlugin.Log?.LogWarning(
                        "AP mod detected but Instance is null");
                    return;
                }

                var pluginType = pluginInstance.GetType();
                var bridgeProp = pluginType.GetProperty("Bridge",
                    BindingFlags.Public | BindingFlags.Instance);
                if (bridgeProp == null)
                {
                    RubiniteDarkRubyPlugin.Log?.LogWarning(
                        "AP mod detected but Bridge property not found");
                    return;
                }

                _gameBridgeInstance = bridgeProp.GetValue(pluginInstance);
                if (_gameBridgeInstance == null)
                {
                    RubiniteDarkRubyPlugin.Log?.LogWarning(
                        "AP mod detected but Bridge instance is null");
                    return;
                }

                var bridgeType = _gameBridgeInstance.GetType();
                _apRunActiveProp = bridgeType.GetProperty("ApRunActive",
                    BindingFlags.Public | BindingFlags.Instance);
                _enableDarkRubyProp = bridgeType.GetProperty("EnableDarkRuby",
                    BindingFlags.Public | BindingFlags.Instance);

                RubiniteDarkRubyPlugin.Log?.LogInfo(
                    "AP mod detected — hook ready. "
                    + "ApRunActive=" + ApRunActive
                    + " EnableDarkRuby=" + EnableDarkRuby);
            }
            catch (Exception ex)
            {
                RubiniteDarkRubyPlugin.Log?.LogWarning(
                    "Failed to reflect into AP mod: " + ex.Message);
                _gameBridgeInstance = null;
            }
        }
    }
}

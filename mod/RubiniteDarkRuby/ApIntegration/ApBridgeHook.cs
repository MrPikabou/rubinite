using System;
using System.Reflection;
using BepInEx.Bootstrap;

namespace RubiniteDarkRuby
{
    /// <summary>
    /// Reflection-based bridge to the Archipelago mod. Detects whether AP is
    /// installed and whether a run is currently active. Handles both field and
    /// property members because the AP mod uses a mix of both.
    /// </summary>
    internal static class ApBridgeHook
    {
        private const string ApGuid = "rubinite.archipelago";

        private static bool _initAttempted;
        private static bool _apModPresent;
        private static object _gameBridgeInstance;
        private static MemberInfo _apRunActiveMember;
        private static MemberInfo _enableDarkRubyMember;
        private static MethodInfo _tryGetMappedLevelMethod;

        public static bool ApModPresent
        {
            get { EnsureInit(); return _apModPresent; }
        }

        public static bool ApRunActive
        {
            get { EnsureInit(); return ReadBool(_apRunActiveMember, _gameBridgeInstance); }
        }

        public static bool EnableDarkRuby
        {
            get { EnsureInit(); return ReadBool(_enableDarkRubyMember, _gameBridgeInstance); }
        }

        /// <summary>
        /// True when the AP mod is loaded and a run is active. In this case the
        /// AP mod owns DR content for the current run, and the DR mod should only
        /// do visuals + blocks — no podium, no timer, no checks.
        /// </summary>
        public static bool ShouldSuppressStandalone =>
            ApModPresent && ApRunActive;

        public static string GetMappedLevel(string portalLevel)
        {
            if (string.IsNullOrEmpty(portalLevel)) return null;
            EnsureInit();
            if (_gameBridgeInstance == null || _tryGetMappedLevelMethod == null) return null;

            try
            {
                var args = new object[] { portalLevel, null };
                if ((bool)_tryGetMappedLevelMethod.Invoke(_gameBridgeInstance, args))
                    return (string)args[1];
            }
            catch { }
            return null;
        }

        private static bool ReadBool(MemberInfo member, object instance)
        {
            if (member == null || instance == null) return false;
            try
            {
                if (member is PropertyInfo prop) return (bool)prop.GetValue(instance);
                if (member is FieldInfo field) return (bool)field.GetValue(instance);
            }
            catch { }
            return false;
        }

        private static MemberInfo FindBoolMember(Type type, string name)
        {
            return (MemberInfo)type.GetProperty(name,
                       BindingFlags.Public | BindingFlags.Instance)
                   ?? type.GetField(name,
                       BindingFlags.Public | BindingFlags.Instance);
        }

        private static void EnsureInit()
        {
            if (_initAttempted) return;

            if (!Chainloader.PluginInfos.TryGetValue(ApGuid, out var pluginInfo))
                return;

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
                _apRunActiveMember = FindBoolMember(bridgeType, "ApRunActive");
                _enableDarkRubyMember = FindBoolMember(bridgeType, "EnableDarkRuby");
                _tryGetMappedLevelMethod = bridgeType.GetMethod("TryGetMappedLevel",
                    BindingFlags.Public | BindingFlags.Instance);

                RubiniteDarkRubyPlugin.Log?.LogInfo(
                    "AP mod detected — hook ready. "
                    + "ApRunActive=" + ApRunActive
                    + " EnableDarkRuby=" + EnableDarkRuby
                    + " MappedLevelMethod=" + (_tryGetMappedLevelMethod != null)
                    + " ApRunActiveMember=" + _apRunActiveMember?.MemberType);
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

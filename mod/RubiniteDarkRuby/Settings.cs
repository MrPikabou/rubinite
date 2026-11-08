using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;

namespace RubiniteDarkRuby
{
    public sealed class Settings
    {
        public ConfigEntry<bool> EnableMod;
        public ConfigEntry<bool> SpawnPortalInChallengeArea;
        public ConfigEntry<string> PortalPositionPreset;
        public ConfigEntry<float> PortalOffsetX;
        public ConfigEntry<float> PortalOffsetY;
        public ConfigEntry<float> GradingTime;
        public ConfigEntry<string> GradingScale;
        public ConfigEntry<bool> UseCustomSkin;
        public ConfigEntry<bool> BlockVanillaLeaderboard;
        public ConfigEntry<bool> PauseTimerInCutscenes;
        public ConfigEntry<bool> LogSkinDiagnostics;

        public void Bind(ConfigFile config)
        {
            EnableMod = config.Bind("General", "EnableMod", true,
                "Master switch. When false, the mod does nothing.");

            SpawnPortalInChallengeArea = config.Bind("Portal", "SpawnInChallengeArea", true,
                "Spawn the Dark Ruby podium in the Challenge Area.");

            PortalPositionPreset = config.Bind("Portal", "PositionPreset", "ExtendedCorner",
                new ConfigDescription(
                    "Where to place the podium relative to the Beast podium. "
                    + "'ExtendedCorner' = far corner (default, room for a new floor later). "
                    + "'NearMaster' = closer to Roy's podium. "
                    + "'Custom' = use OffsetX/OffsetY below.",
                    new AcceptableValueList<string>("ExtendedCorner", "NearMaster", "Custom")));

            PortalOffsetX = config.Bind("Portal", "OffsetX", 10.75f,
                "X offset (only used when PositionPreset = 'Custom').");

            PortalOffsetY = config.Bind("Portal", "OffsetY", -25.45f,
                "Y offset (only used when PositionPreset = 'Custom').");

            GradingTime = config.Bind("Grading", "BaseTime", 10f,
                "Base time in seconds for S-rank. Must match the APworld value when running AP.");

            GradingScale = config.Bind("Grading", "Scale", "5,25,50",
                "Comma-separated thresholds added to BaseTime for S, A, B (C = anything over).");

            UseCustomSkin = config.Bind("Skin", "UseCustomSkin", true,
                "Master switch for the entire skin system. When false, no custom materials "
                + "are applied, no DR frame appears in the skin menu, and the skin-related "
                + "per-frame hooks are disabled. The boss fight, AP integration, and "
                + "leaderboard continue to work normally.");

            BlockVanillaLeaderboard = config.Bind("Leaderboard", "BlockVanillaLeaderboard", true,
                "Block the vanilla leaderboard for the Dark Ruby fight (no entry exists).");

            PauseTimerInCutscenes = config.Bind("Timer", "PauseInCutscenes", true,
                "Pause the fight timer during cutscenes.");

            LogSkinDiagnostics = config.Bind("Diagnostics", "LogSkinDiagnostics", false,
                "Log every material change on the player 'Girl' renderers. Very noisy — enable only for debugging.");
        }

        /// <summary>Resolves the effective X/Y offset for the current preset.</summary>
        public (float x, float y) ResolvePortalOffset()
        {
            switch (PortalPositionPreset.Value)
            {
                case "NearMaster":     return (11f, -17.33f);
                case "ExtendedCorner": return (10.25f, -25.45f);
                default:               return (PortalOffsetX.Value, PortalOffsetY.Value);
            }
        }

        public float[] ParseGradingScale()
        {
            var raw = GradingScale?.Value ?? "5,25,50";
            var parts = raw.Split(',');
            var result = new List<float>();
            foreach (var p in parts)
            {
                if (float.TryParse(p.Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var f))
                    result.Add(f);
            }
            return result.ToArray();
        }
    }
}

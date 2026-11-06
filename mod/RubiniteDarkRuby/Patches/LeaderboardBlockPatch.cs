using HarmonyLib;
using UnityEngine;

namespace RubiniteDarkRuby.Patches
{
    /// <summary>
    /// Blocks the vanilla leaderboard menu for Dark Ruby. There is no leaderboard
    /// entry for Dark Ruby (cut content), so opening the leaderboard softlocks the
    /// game.
    ///
    /// When the AP mod is also running and handling the same case, both patches
    /// fire but the block is idempotent — the second block is a no-op.
    /// </summary>
    [HarmonyPatch(typeof(LeaderboardMenu), "ShowLeaderboard")]
    internal static class LeaderboardBlockPatch
    {
        private static bool Prefix(ref LevelData levelData)
        {
            var settings = RubiniteDarkRubyPlugin.Instance?.Settings;
            if (settings?.EnableMod?.Value != true) return true;
            if (settings.BlockVanillaLeaderboard?.Value != true) return true;
            if (levelData == null) return true;

            if (levelData.DisplayName == "DarkRubyLevel")
            {
                RubiniteDarkRubyPlugin.Log?.LogInfo(
                    "Blocked vanilla leaderboard for Dark Ruby (no entry exists)");
                return false;
            }

            return true;
        }
    }
}

using HarmonyLib;
using UnityEngine;

//TODO DELETE
namespace RubiniteDarkRuby.Patches
{
    [HarmonyPatch(typeof(LevelManager), "SetUp")]
    internal static class LevelManagerDiagnosticPatch
    {
        private static void Postfix(LevelManager __instance)
        {
            if (__instance == null) return;
            var scene = __instance.gameObject.scene.name;
            bool deadActive = __instance.enableDead != null && __instance.enableDead.activeSelf;
            bool hurtActive = __instance.enableHurt != null && __instance.enableHurt.activeSelf;
            var mode = GamePlayCore.Instance?.challengeMode;
            int winCount = __instance.levelData?.winCount?.GetValue() ?? -1;

            RubiniteDarkRubyPlugin.Log?.LogInfo(
                "[LevelManager] scene=" + scene
                + " mode=" + mode
                + " enableDead=" + deadActive
                + " enableHurt=" + hurtActive
                + " levelData=" + (__instance.levelData?.DisplayName ?? "<null>")
                + " winCount=" + winCount);
        }
    }
}

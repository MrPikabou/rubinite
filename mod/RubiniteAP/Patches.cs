using System.Collections.Generic;
using HarmonyLib;

namespace RubiniteAP
{
    [HarmonyPatch(typeof(ChallengeLevelPoint), "Init")]
    internal static class ChallengeLevelPointInitPatch
    {
        private static void Postfix(ChallengeLevelPoint __instance)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge != null && bridge.ApRunActive)
                bridge.RefreshPortals();
        }
    }

    [HarmonyPatch(typeof(OnEnableLoadLevel), "OnEnable")]
    internal static class OnEnableLoadLevelPatch
    {
        private static bool Prefix(OnEnableLoadLevel __instance)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive)
                return true;
            var level = GameBridge.GetTargetLevel(__instance);
            if (bridge.ShouldBlockLoad(level))
                return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(ApplicationCore), "LoadLevel")]
    internal static class LoadLevelPatch
    {
        private static bool Prefix(string levelName)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive)
                return true;
            return !bridge.ShouldBlockLoad(levelName);
        }
    }

    [HarmonyPatch(typeof(GamePlayCore), "SetUp")]
    internal static class GamePlayCoreSetUpPatch
    {
        private static void Prefix()
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge != null && bridge.ApRunActive)
                bridge.PrepareSaveFlags();
        }
    }

    [HarmonyPatch(typeof(GamePlayCore), "Init")]
    internal static class GamePlayCoreInitPatch
    {
        private static void Postfix()
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge != null && bridge.ApRunActive)
                bridge.ApplyRuntime(true);
        }
    }

    [HarmonyPatch(typeof(LevelManager), "SetToMissionCompletedState")]
    internal static class LevelCompletedPatch
    {
        private static void Postfix(LevelManager __instance)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge != null && bridge.ApRunActive)
                bridge.HandleLevelCompleted(__instance);
        }
    }
}

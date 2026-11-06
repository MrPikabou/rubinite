using HarmonyLib;

namespace RubiniteDarkRuby.Patches
{
    [HarmonyPatch(typeof(MissionFailedMenu), "Enter")]
    internal static class MissionFailedMenuEnterPatch
    {
        private static void Postfix(MissionFailedMenu __instance)
        {
            if (ApBridgeHook.ShouldSuppressStandalone) return;
            
            var module = RubiniteDarkRubyPlugin.Instance?.Module;
            if (module == null) return;

            var lm = GamePlayCore.Instance?.levelManager;
            if (lm?.levelData?.DisplayName != "DarkRubyLevel") return;

            if (__instance.backToChallengeAreaButton != null)
                __instance.backToChallengeAreaButton.gameObject.SetActive(true);
            if (__instance.equipmentButton != null && !__instance.equipmentButton.gameObject.activeSelf)
                __instance.equipmentButton.gameObject.SetActive(true);

            RubiniteDarkRubyPlugin.Log?.LogInfo("Dark Ruby: MissionFailedMenu buttons activated");
            module.OnDarkRubyMissionFailed();
        }
    }
}

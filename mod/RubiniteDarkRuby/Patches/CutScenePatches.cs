using HarmonyLib;

namespace RubiniteDarkRuby.Patches
{
    [HarmonyPatch(typeof(CutSceneController), "StartCutScene")]
    internal static class CutSceneStartPatch
    {
        private static bool Prefix(CutSceneController __instance)
        {
            if (ApBridgeHook.ShouldSuppressStandalone) return true;

            var module = RubiniteDarkRubyPlugin.Instance?.Module;
            if (module == null) return true;

            var lm = GamePlayCore.Instance?.levelManager;
            if (lm?.levelData?.DisplayName != "DarkRubyLevel") return true;

            module.OnDarkRubyCutsceneStart(__instance?.name);

            bool bossDead = lm.boss != null && lm.boss.isDead;
            if (module.ShouldBlockDarkRubyCutscene(bossDead)) return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(GamePlayCore), "EnterCutSceneMode")]
    internal static class EnterCutSceneModePatch
    {
        private static void Postfix()
        {
            if (ApBridgeHook.ShouldSuppressStandalone) return;
            RubiniteDarkRubyPlugin.Instance?.Module?.OnDarkRubyEnterCutScene();
        }
    }

    [HarmonyPatch(typeof(GamePlayCore), "ExitCutSceneMode")]
    internal static class ExitCutSceneModePatch
    {
        private static void Postfix()
        {
            if (ApBridgeHook.ShouldSuppressStandalone) return;
            RubiniteDarkRubyPlugin.Instance?.Module?.OnDarkRubyExitCutScene();
        }
    }
}

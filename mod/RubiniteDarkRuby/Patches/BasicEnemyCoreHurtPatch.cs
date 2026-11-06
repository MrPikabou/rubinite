using HarmonyLib;

namespace RubiniteDarkRuby.Patches
{
    [HarmonyPatch(typeof(BasicEnemyCore), "Hurt")]
    internal static class BasicEnemyCoreHurtPatch
    {
        private static bool Prefix(BasicEnemyCore __instance, out bool __state)
        {
            __state = __instance.isDead;
            if (ApBridgeHook.ShouldSuppressStandalone) return true;
            return true;
        }

        private static void Postfix(BasicEnemyCore __instance, bool __state)
        {
            if (ApBridgeHook.ShouldSuppressStandalone) return;
            if (!(__instance is DarkRubyCore)) return;
            if (__state) return;
            if (!__instance.isDead) return;
            RubiniteDarkRubyPlugin.Instance?.Module?.OnDarkRubyBossDead();
        }
    }
}

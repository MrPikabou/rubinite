using HarmonyLib;

namespace RubiniteDarkRuby.Patches
{
    [HarmonyPatch(typeof(BasicEnemyCore), "Hurt")]
    internal static class BasicEnemyCoreHurtPatch
    {
        private static void Prefix(BasicEnemyCore __instance, out bool __state)
        {
            __state = __instance.isDead;
        }

        private static void Postfix(BasicEnemyCore __instance, bool __state)
        {
            if (!(__instance is DarkRubyCore)) return;
            if (__state) return;
            if (!__instance.isDead) return;
            RubiniteDarkRubyPlugin.Instance?.Module?.OnDarkRubyBossDead();
        }
    }
}

using HarmonyLib;
using PugTool.Unity.Singleton;
using UnityEngine;

//TODO DELETE
namespace RubiniteDarkRuby.Patches
{
    [HarmonyPatch(typeof(BasicEnemyCore), "Start")]
    internal static class BasicEnemyCoreStartDiagPatch
    {
        private static void Postfix(BasicEnemyCore __instance)
        {
            if (!(__instance is DarkRubyCore)) return;

            var core = GamePlayCore.Instance;
            var player = core?.player;
            var lm = core?.levelManager;
            var attr = __instance.enemyAttr;

            float[] hpBarMax = attr?.hpBarMaxValue;
            string hpBarMaxStr = hpBarMax == null ? "null"
                : "[" + string.Join(",", System.Array.ConvertAll(hpBarMax, x => x.ToString("F0"))) + "]";

            RubiniteDarkRubyPlugin.Log?.LogInfo(
                "[DRCore] " +
                "bossHP=" + __instance.HP + " " +
                "enemyAttr.maxHP=" + (attr?.maxHP ?? -1) + " " +
                "hpBarMaxValue=" + hpBarMaxStr + " " +
                "useMainBossUI=" + __instance.useMainBossUI + " " +
                "bossUI.active=" + (lm?.bossUI?.gameObject.activeSelf ?? false) + " " +
                "bossUI.alwaysHide=" + GetAlwaysHide(lm?.bossUI) + " " +
                "challengeMode=" + (core?.challengeMode) + " " +
                "player.skipDeath=" + (player?.skipDeath ?? false) + " " +
                "player.enableDead=" + (player?.enableDead ?? false) + " " +
                "player.HP=" + (player?.HP ?? -1) + "/" + (player?.Attr?.maxHP ?? -1) + " " +
                "player.hurtScale=" + (player?.Attr?.hurtScale ?? -1) + " " +
                "HPBottleCount=" + (player?.HPBottleCount ?? -1) + "/" + (core?.HPBottleCountMax?.GetValue() ?? -1));
        }

        private static bool GetAlwaysHide(BossUI ui)
        {
            if (ui == null) return false;
            var f = typeof(BossUI).GetField("alwaysHide",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return f != null && (bool)f.GetValue(ui);
        }
    }
}

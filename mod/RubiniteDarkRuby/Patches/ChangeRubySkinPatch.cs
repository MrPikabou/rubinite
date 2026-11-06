using HarmonyLib;
using UnityEngine;

namespace RubiniteDarkRuby.Patches
{
    [HarmonyPatch(typeof(ChangeRubySkin), "ChangeSkin")]
    internal static class ChangeRubySkinPatch
    {
        private static bool Prefix(ChangeRubySkin __instance)
        {
            if (!DarkRubySkin.IsActive) return true;
            if (__instance?.spriteRenderer == null) return true;

            if (__instance.spriteRenderer.gameObject.name != "Girl") return true;
            if (__instance.isPortrait) return true;

            var t = __instance.spriteRenderer.transform;
            while (t != null)
            {
                if (t.GetComponent<ChallengeLevelPoint>() != null) return true;
                t = t.parent;
            }

            if (!DarkRubySkin.ShouldApplyToRenderer(__instance.spriteRenderer)) return true;

            DarkRubySkin.ApplyToRenderer(__instance.spriteRenderer);
            return false;
        }
    }
}

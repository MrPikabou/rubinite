using HarmonyLib;
using UnityEngine;

namespace RubiniteDarkRuby.Patches
{
    /// <summary>
    /// Re-applies the DR skin whenever the game rebuilds the character skin
    /// (save load, vanilla color change, fade-in after death, etc.). Without
    /// this the DR material would get wiped by any internal ChangeSkin() call.
    /// </summary>
    [HarmonyPatch(typeof(ChangeRubySkin), "ChangeSkin")]
    internal static class ChangeRubySkinPatch
    {
        private static void Postfix(ChangeRubySkin __instance)
        {
            if (!DarkRubySkin.IsActive) return;
            if (__instance?.spriteRenderer == null) return;

            // Only the actual player character, not portraits or previews
            if (__instance.spriteRenderer.gameObject.name != "Girl") return;
            if (__instance.isPortrait) return;

            // Skip podium preview "Girl" sprites (children of ChallengeLevelPoint)
            var t = __instance.spriteRenderer.transform;
            while (t != null)
            {
                if (t.GetComponent<ChallengeLevelPoint>() != null) return;
                t = t.parent;
            }

            DarkRubySkin.ApplyToRenderer(__instance.spriteRenderer);
        }
    }
}

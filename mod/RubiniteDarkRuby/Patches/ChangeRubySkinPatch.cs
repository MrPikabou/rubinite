using HarmonyLib;
using UnityEngine;

namespace RubiniteDarkRuby.Patches
{
    [HarmonyPatch(typeof(ChangeRubySkin), "ChangeSkin")]
    internal static class ChangeRubySkinPatch
    {
        private static int _lastGirlFrame = -1;
        private static int _lastPortraitFrame = -1;

        private static bool Prefix(ChangeRubySkin __instance)
        {
            if (!DarkRubySkin.IsActive) return true;
            if (__instance == null) return true;

            if (!__instance.isPortrait && __instance.spriteRenderer != null)
            {
                if (__instance.spriteRenderer.gameObject.name != "Girl") return true;
                var t = __instance.spriteRenderer.transform;
                while (t != null)
                {
                    if (t.GetComponent<ChallengeLevelPoint>() != null) return true;
                    t = t.parent;
                }
                if (!DarkRubySkin.ShouldApplyToRenderer(__instance.spriteRenderer)) return true;

                if (_lastGirlFrame == Time.frameCount) return false;
                _lastGirlFrame = Time.frameCount;

                DarkRubySkin.ApplyToRenderer(__instance.spriteRenderer);
                return false;
            }

            return true;
        }

        private static void Postfix(ChangeRubySkin __instance)
        {
            if (!DarkRubySkin.IsActive) return;
            if (__instance == null) return;
            if (!__instance.isPortrait) return;
            if (DarkRubySkin.IsVanillaFrameFocused) return;

            var img = __instance.image;
            if (img == null) return;
            if (img.material != null && img.material.name.StartsWith(DarkRubySkin.MaterialName + "_Portrait")) return;

            if (_lastPortraitFrame == Time.frameCount) return;
            _lastPortraitFrame = Time.frameCount;
            DarkRubySkin.ApplyToPortraitImage(img);
        }
    }
}

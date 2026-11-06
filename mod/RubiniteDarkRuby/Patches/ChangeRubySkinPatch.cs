using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RubiniteDarkRuby.Patches
{
    /// <summary>
    /// When the DR skin is active and the game tries to rebuild the player's
    /// material (via ChangeSkin on wakeup/sleep, save load, palette change, etc.),
    /// we skip the original entirely and leave the DR material in place.
    /// This eliminates the one-frame "flash to vanilla" that Postfix produced.
    /// When DR skin is off, the original runs normally.
    /// </summary>
    [HarmonyPatch(typeof(ChangeRubySkin), "ChangeSkin")]
    internal static class ChangeRubySkinPatch
    {
        private static bool Prefix(ChangeRubySkin __instance)
        {
            if (!DarkRubySkin.IsActive) return true;
            if (__instance?.spriteRenderer == null) return true;

            if (SceneManager.GetActiveScene().name == "TitleMenu") return true;
            if (__instance.spriteRenderer.gameObject.scene.name == "TitleMenu") return true;

            if (__instance.spriteRenderer.gameObject.name != "Girl") return true;
            if (__instance.isPortrait) return true;

            var t = __instance.spriteRenderer.transform;
            while (t != null)
            {
                if (t.GetComponent<ChallengeLevelPoint>() != null) return true;
                t = t.parent;
            }

            DarkRubySkin.ApplyToRenderer(__instance.spriteRenderer);
            return false;
        }
    }
}

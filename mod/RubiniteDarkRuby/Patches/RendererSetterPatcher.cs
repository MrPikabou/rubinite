using HarmonyLib;
using UnityEngine;

namespace RubiniteDarkRuby.Patches
{
    [HarmonyPatch(typeof(Renderer), "set_material")]
    internal static class RendererSetMaterialPatch
    {
        private static void Postfix(Renderer __instance)
        {
            if (!DarkRubySkin.IsActive) return;
            if (!(__instance is SpriteRenderer sr)) return;
            if (sr.gameObject.name != "Girl") return;
            if (!DarkRubySkin.ShouldApplyToRenderer(sr)) return;
            if (sr.material == null) return;
            if (sr.material.name.StartsWith(DarkRubySkin.MaterialName)) return;
            DarkRubySkin.ApplyToRenderer(sr);
        }
    }

    [HarmonyPatch(typeof(Renderer), "set_sharedMaterial")]
    internal static class RendererSetSharedMaterialPatch
    {
        private static void Postfix(Renderer __instance)
        {
            if (!DarkRubySkin.IsActive) return;
            if (!(__instance is SpriteRenderer sr)) return;
            if (sr.gameObject.name != "Girl") return;
            if (!DarkRubySkin.ShouldApplyToRenderer(sr)) return;
            if (sr.material == null) return;
            if (sr.material.name.StartsWith(DarkRubySkin.MaterialName)) return;
            DarkRubySkin.ApplyToRenderer(sr);
        }
    }
}

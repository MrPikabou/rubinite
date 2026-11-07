using HarmonyLib;
using PugTool.Unity.Singleton;
using UnityEngine;

namespace RubiniteDarkRuby.Patches
{
    [HarmonyPatch(typeof(ItemDataOverview), "GetCurrentSkinMaterial")]
    internal static class ItemDataOverviewCharacterPatch
    {
        private static bool Prefix(ItemDataOverview __instance, ref Material __result)
        {
            if (!DarkRubySkin.IsSkinSystemEnabled) return true;
            var flag = __instance?.currentSkinIDFlag?.GetValue();
            var state = RubiniteDarkRubyPlugin.Instance?.State;

            if (RubiniteDarkRubyPlugin.Instance?.Settings?.LogSkinDiagnostics?.Value == true)
            {
                RubiniteDarkRubyPlugin.Log?.LogInfo(
                    "[SkinFlag] GetCurrentSkinMaterial: flag='" + (flag ?? "<null>")
                    + "' SkinSelected=" + (state?.SkinSelected ?? false)
                    + " LastVanilla='" + (state?.LastVanillaSkinId ?? "<null>") + "'");
            }

            if (flag != DarkRubySkinEntry.SkinId) return true;

            if (state?.SkinSelected != true)
            {
                DarkRubySkin.EnsureFlagConsistency();
                return true;
            }

            var mat = DarkRubySkin.GetOrCreateCharacterMaterial();
            if (mat == null) return true;
            __result = mat;
            return false;
        }
    }

    [HarmonyPatch(typeof(ItemDataOverview), "GetCurrentSkinMaterial_Portrait")]
    internal static class ItemDataOverviewPortraitPatch
    {
        private static bool Prefix(ItemDataOverview __instance, ref Material __result)
        {
            if (!DarkRubySkin.IsSkinSystemEnabled) return true;
            var flag = __instance?.currentSkinIDFlag?.GetValue();
            var state = RubiniteDarkRubyPlugin.Instance?.State;

            if (RubiniteDarkRubyPlugin.Instance?.Settings?.LogSkinDiagnostics?.Value == true)
            {
                RubiniteDarkRubyPlugin.Log?.LogInfo(
                    "[SkinFlag] GetCurrentSkinMaterial_Portrait: flag='" + (flag ?? "<null>")
                    + "' SkinSelected=" + (state?.SkinSelected ?? false)
                    + " LastVanilla='" + (state?.LastVanillaSkinId ?? "<null>") + "'");
            }

            if (flag != DarkRubySkinEntry.SkinId) return true;

            if (state?.SkinSelected != true)
            {
                DarkRubySkin.EnsureFlagConsistency();
                return true;
            }

            var mat = DarkRubySkin.BuildPortraitMaterial(__instance);
            if (mat == null) return true;
            __result = mat;
            return false;
        }
    }
}

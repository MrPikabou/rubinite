using HarmonyLib;
using UnityEngine;

namespace RubiniteDarkRuby.Patches
{
    [HarmonyPatch(typeof(ItemDataOverview), "GetCurrentSkinMaterial")]
    internal static class ItemDataOverviewCharacterPatch
    {
        private static bool Prefix(ItemDataOverview __instance, ref Material __result)
        {
            if (__instance?.currentSkinIDFlag?.GetValue() != DarkRubySkinEntry.SkinId)
                return true;
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
            if (__instance?.currentSkinIDFlag?.GetValue() != DarkRubySkinEntry.SkinId)
                return true;

            var mat = DarkRubySkin.BuildPortraitMaterial(__instance);
            if (mat == null)
            {
                var defaultItem = __instance.FindUniqueId(__instance.defaultSkinItem?.Id);
                __result = defaultItem?.skinMaterial_Portrait;
                return false;
            }

            __result = mat;
            return false;
        }
    }
}

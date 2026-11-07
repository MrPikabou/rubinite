using HarmonyLib;
using PugTool.Unity.Singleton;
using PugTool.Unity.StringList;
using UnityEngine;

namespace RubiniteDarkRuby.Patches
{
    [HarmonyPatch(typeof(ChangeSkinMenu), "Enter")]
    internal static class ChangeSkinMenuEnterPatch
    {
        private static void Postfix(ChangeSkinMenu __instance)
        {
            if (!DarkRubySkin.IsSkinSystemEnabled) return;
            var settings = RubiniteDarkRubyPlugin.Instance?.Settings;
            if (settings?.EnableMod?.Value != true) return;
            if (!DarkRubySkinEntry.ShouldShow) return;

            var injected = DarkRubySkinEntry.EnsureInjected(__instance);
            if (injected == null) return;

            var state = RubiniteDarkRubyPlugin.Instance?.State;
            if (state == null || !state.SkinSelected) return;

            DarkRubySkin.IsVanillaFrameFocused = false;

            var skinGroup = injected.transform.parent;
            if (skinGroup != null)
            {
                for (int i = 0; i < skinGroup.childCount; i++)
                {
                    var child = skinGroup.GetChild(i);
                    var disp = child.Find("displayWhenSelected")?.gameObject;
                    if (disp != null) disp.SetActive(child.gameObject == injected);
                }
            }

            if (__instance.selectedTitleText != null)
                __instance.selectedTitleText.text = "Shadow Pigments";

            SingletonScriptableObject<ItemDataOverview>.Instance
                .currentSkinIDFlag.SetValue("darkruby");

            DarkRubySkin.Apply();

            var skinFrame = injected.GetComponent<SkinFrame>();
            if (skinFrame?.selectable != null)
            {
                try { MenuCore.Instance.SetSelect(skinFrame.selectable); } catch { }
            }

            RubiniteDarkRubyPlugin.Log?.LogInfo(
                "DR skin menu entered with DR active — restored selection state");
            
            DarkRubySkin.InvalidateCrsCache();
        }
    }

    [HarmonyPatch(typeof(ChangeSkinMenu), "Exit")]
    internal static class ChangeSkinMenuExitPatch
    {
        private static void Postfix()
        {
            if (!DarkRubySkin.IsSkinSystemEnabled) return;
            DarkRubySkin.IsVanillaFrameFocused = false;

            var state = RubiniteDarkRubyPlugin.Instance?.State;
            var overview = SingletonScriptableObject<ItemDataOverview>.Instance;

            if (state?.SkinSelected == true)
            {
                if (overview?.currentSkinIDFlag != null)
                {
                    overview.currentSkinIDFlag.SetValue(DarkRubySkinEntry.SkinId);
                }
                DarkRubySkin.Apply();
            }

            DarkRubySkin.InvalidateCrsCache();
        }
    }

    [HarmonyPatch(typeof(SkinFrame), "OnSelect")]
    internal static class SkinFrameOnSelectPatch
    {
        private static bool Prefix(SkinFrame __instance, UnityEngine.EventSystems.BaseEventData eventData)
        {
            if (!DarkRubySkin.IsSkinSystemEnabled) return true;
            if (__instance == null) return true;

            bool isDR = __instance.gameObject.name == DarkRubySkinEntry.FrameName;

            DarkRubySkin.IsVanillaFrameFocused = !isDR;

            if (!isDR) return true;

            var itemOverview = SingletonScriptableObject<ItemDataOverview>.Instance;
            itemOverview?.currentSkinIDFlag?.SetValue(DarkRubySkinEntry.SkinId);

            DarkRubySkin.ApplyToPortraitPreview();

            if (__instance.displayWhenSelected != null)
                __instance.displayWhenSelected.SetActive(true);

            var menu = __instance.changeSkinMenu;
            if (menu?.selectedTitleText != null)
                menu.selectedTitleText.text = "Shadow Pigments";

            if (menu != null)
            {
                var skinGroup = menu.transform.Find("Skin_Group");
                if (skinGroup != null)
                {
                    for (int i = 0; i < skinGroup.childCount; i++)
                    {
                        var child = skinGroup.GetChild(i);
                        if (child.name == DarkRubySkinEntry.FrameName) continue;
                        var disp = child.Find("displayWhenSelected")?.gameObject;
                        if (disp != null) disp.SetActive(false);
                    }
                }
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(SkinFrame), "OnClick")]
    internal static class SkinFrameOnClickPatch
    {
        private static bool Prefix(SkinFrame __instance)
        {
            if (!DarkRubySkin.IsSkinSystemEnabled) return true;
            if (__instance == null) return true;

            var state = RubiniteDarkRubyPlugin.Instance?.State;
            var overview = SingletonScriptableObject<ItemDataOverview>.Instance;
            var currentFlag = overview?.currentSkinIDFlag?.GetValue();

            if (__instance.gameObject.name == DarkRubySkinEntry.FrameName)
            {
                if (state != null)
                {
                    state.SkinSelected = true;
                    state.Save();
                }
                if (overview?.currentSkinIDFlag != null)
                    overview.currentSkinIDFlag.SetValue(DarkRubySkinEntry.SkinId);

                DarkRubySkin.Apply();
                MenuCore.Instance?.ExitMenu();
                RubiniteDarkRubyPlugin.Log?.LogInfo("DR skin: committed via DR click");
                return false;
            }

            if (!string.IsNullOrEmpty(currentFlag) && currentFlag != DarkRubySkinEntry.SkinId)
            {
                if (state != null)
                {
                    state.LastVanillaSkinId = currentFlag;

                    if (GameSetting.Instance?.rubySkinID != null)
                        GameSetting.Instance.rubySkinID.SetValue(currentFlag, "RubiniteDarkRuby");

                    state.SkinSelected = false;
                    state.Save();
                    try { DataSaver.SaveFlag(DataSaver.path_saveFolder); } catch { }
                }
                DarkRubySkin.IsVanillaFrameFocused = false;
                DarkRubySkin.ClearPortraitTints();
                RubiniteDarkRubyPlugin.Log?.LogInfo(
                    "DR skin: committed vanilla '" + currentFlag + "'");
            }

            return true;
        }
    }
}

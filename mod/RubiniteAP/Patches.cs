using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Cinemachine;
using Game.Level;
using TMPro;
using PugTool.Unity.Singleton;
using PugTool.Unity.StringList;

namespace RubiniteAP
{
    [HarmonyPatch(typeof(DataSaver), "SaveFlag")]
    internal static class ApChallengeSaveProfilePatch
    {
        private static void Postfix()
        {
            RubiniteAPPlugin.Instance?.Bridge?.PersistChallengeSaveProfile();
        }
    }

    [HarmonyPatch(typeof(SkinFrame), "OnClick")]
    internal static class VanillaSkinPreferenceClickPatch
    {
        private static void Postfix()
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null) return;
            if (bridge.ApRunActive)
            {
                bridge.RecordApSkinPreference();
                return;
            }
            bridge.RecordVanillaSkinPreference();
        }
    }

    [HarmonyPatch(typeof(ChangeSkinMenu), "Exit")]
    internal static class VanillaSkinPreferenceMenuExitPatch
    {
        private static void Postfix()
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || bridge.ApRunActive) return;
            bridge.RecordVanillaSkinPreference();
            bridge.ApplyTitleScreenRedSkin();
        }
    }

    [HarmonyPatch(typeof(ChallengeLevelPoint), "Init")]
    internal static class ChallengeLevelPointInitPatch
    {
        private static bool _refreshQueued;

        private static void Postfix()
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive || _refreshQueued) return;
            _refreshQueued = true;
            RubiniteAPPlugin.Instance.StartCoroutine(RefreshNextFrame(bridge));
        }

        private static IEnumerator RefreshNextFrame(GameBridge bridge)
        {
            yield return null;
            _refreshQueued = false;
            bridge.RefreshPortals();
        }
    }

    [HarmonyPatch(typeof(OnEnableLoadLevel), "OnEnable")]
    internal static class OnEnableLoadLevelPatch
    {
        private static bool Prefix(OnEnableLoadLevel __instance)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive)
                return true;
            var level = GameBridge.GetTargetLevel(__instance);
            bridge.NotifyPodiumClick(level);
            if (bridge.ShouldBlockLoad(level))
            {
                bridge.ClearPendingPodiumClick();
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(ApplicationCore), "LoadLevel")]
    internal static class LoadLevelPatch
    {
        private static readonly HashSet<string> StoryScenes = new HashSet<string>
        {
            "MasterSceneCamp", "QueenSceneCamp", "BedRoom", "WhiteRoom",
            "BloodSea", "BadWichiwi", "FirstRunEnding_AllIsDream",
            "Prologue", "Intro", "NamelessChurch"
        };

        private static bool Prefix(ref string levelName)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive)
                return true;

            if (StoryScenes.Contains(levelName) || levelName.StartsWith("MasterSceneCamp"))
            {
                if (bridge.LastPortalLevel == "DarkRubyLevel" || bridge.LastPortalLevel == "DarkRuby")
                {
                    if (!bridge.DarkRubyCompleted)
                    {
                        LevelManager darkRubyMgr = null;
                        foreach (var mgr in Resources.FindObjectsOfTypeAll<LevelManager>())
                        {
                            if (mgr?.gameObject == null || !mgr.gameObject.scene.IsValid()) continue;
                            if (mgr.levelData?.DisplayName == "DarkRubyLevel") { darkRubyMgr = mgr; break; }
                        }

                        if (darkRubyMgr != null && bridge.IsPlayerAlive())
                        {
                            bridge.AddLog("Dark Ruby: opening ChallengeWinMenu instead of story scene");
                            bridge.HandleLevelCompleted(darkRubyMgr);
                            bridge.DarkRubyCompleted = true;

                            try
                            {
                                var winMenu = GamePlayCore.Instance.challengeWinMenu;
                                if (winMenu != null)
                                {
                                    bridge.ClearPendingPodiumClick();
                                    MenuCore.Instance.EnterMenu(winMenu);
                                    return false;
                                }
                                bridge.AddLog("WARNING: challengeWinMenu is null — falling back to CA");
                            }
                            catch (System.Exception ex) { bridge.AddLog("Failed to open win menu: " + ex.Message); }
                        }
                    }
                }

                bridge.ClearPendingPodiumClick();
                bridge.AddLog($"Intercepted story scene '{levelName}', returning to ChallengeArea.");
                levelName = "ChallengeArea";
            }

            if (bridge.ShouldBlockLoad(levelName))
                return false;

            levelName = bridge.RemapBossScene(levelName);

            if (levelName == "ChallengeArea")
            {
                // Reset dust values when returning to CA
                if (bridge._originalDustValues.Count > 0)
                {
                    var overview = SingletonScriptableObject<LevelDataOverview>.Instance;
                    if (overview != null)
                    {
                        foreach (var kv in bridge._originalDustValues)
                        {
                            var data = overview.FindUniqueId(kv.Key);
                            if (data != null)
                            {
                                data.dustFirstStage = kv.Value.first;
                                data.dustFinalStage = kv.Value.final;
                            }
                        }
                    }
                    bridge._originalDustValues.Clear();
                }

                bridge.FixReturnPortal();
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(MissionFailedMenu), "Enter")]
    internal static class MissionFailedMenuEnterPatch
    {
        private static void Postfix(MissionFailedMenu __instance)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive) return;

            var lm = GamePlayCore.Instance.levelManager;
            if (lm?.levelData?.DisplayName != "DarkRubyLevel") return;

            if (__instance.backToChallengeAreaButton != null)
                __instance.backToChallengeAreaButton.gameObject.SetActive(true);
            if (__instance.equipmentButton != null && !__instance.equipmentButton.gameObject.activeSelf)
                __instance.equipmentButton.gameObject.SetActive(true);

            bridge.AddLog("Dark Ruby: MissionFailedMenu buttons activated (timer resets on next attempt)");
        }
    }

    [HarmonyPatch(typeof(GamePlayCore), "SetUp")]
    internal static class GamePlayCoreSetUpPatch
    {
        private static void Prefix()
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge != null && bridge.ApRunActive)
                bridge.PrepareSaveFlags();
        }

        private static void Postfix()
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge != null && bridge.ApRunActive)
                bridge.OnGamePlaySetUpCompleted();
        }
    }

    [HarmonyPatch(typeof(GamePlayCore), "Init")]
    internal static class GamePlayCoreInitPatch
    {
        private static void Postfix()
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge != null && bridge.ApRunActive)
            {
                bridge.ApplyRuntime(true);
                RubiniteAPPlugin.Instance.StartCoroutine(ApplyApSkinAfterInitialization(bridge));
            }
        }

        private static IEnumerator ApplyApSkinAfterInitialization(GameBridge bridge)
        {
            yield return null;
            yield return new WaitForSeconds(0.1f);
            if (bridge != null && bridge.ApRunActive)
                bridge.ApplyConfiguredApSkin();
        }
    }

    [HarmonyPatch(typeof(LevelManager), "SetToMissionCompletedState")]
    internal static class LevelCompletedPatch
    {
        private static void Postfix(LevelManager __instance)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge != null && bridge.ApRunActive)
                bridge.HandleLevelCompleted(__instance);
        }
    }

    [HarmonyPatch(typeof(LevelManager), "SetUp")]
    internal static class LevelManagerSetUpPatch
    {
        private static void Prefix(LevelManager __instance)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive) return;
            if (__instance == null || __instance.gameObject.scene.name != "ChallengeArea") return;
            if (__instance.playerSpawnPoint == null) return;

            try { bridge.SpawnDarkRubyPodium(); }
            catch (System.Exception ex) { bridge.AddLog("SpawnDarkRubyPodium failed: " + ex.Message); }
            bridge.ApplyDarkRubyGradingToLevelData();
            bridge.SpawnPrefixSucceeded = false;

            var portalLevel = bridge.LastPortalLevel;
            if (string.IsNullOrEmpty(portalLevel)) return;

            foreach (var point in Resources.FindObjectsOfTypeAll<ChallengeLevelPoint>())
            {
                if (point?.gameObject == null || !point.gameObject.scene.IsValid()) continue;
                if (point.levelName?.displayName == portalLevel)
                {
                    var targetPos = point.transform.position;
                    targetPos.y -= 1.2f;
                    targetPos.z = __instance.playerSpawnPoint.position.z;
                    __instance.playerSpawnPoint.position = targetPos;
                    bridge.SpawnPrefixSucceeded = true;
                    bridge.AddLog("SpawnPrefix: moved playerSpawnPoint to '" + portalLevel + "'");
                    return;
                }
            }
            bridge.AddLog("SpawnPrefix: podium not found for '" + portalLevel + "'");
        }

        private static void Postfix(LevelManager __instance)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive) return;
            if (__instance == null) return;

            if (__instance.gameObject.scene.name == "DarkRubyLevel" && __instance.levelTimer != null)
            {
                bool wasActive = __instance.levelTimer.gameObject.activeSelf;
                if (!wasActive)
                {
                    __instance.levelTimer.gameObject.SetActive(true);
                    bridge.AddLog("Dark Ruby: activated levelTimer");
                }

                __instance.levelTimer.timer = 0f;
                __instance.levelTimer.SetStart();
                __instance.levelTimer.Pause();
                bridge.DarkRubyTimerPausedForCutscene = true;
                bridge.AddLog("Dark Ruby: timer started paused (waiting for intro cutscene)");
            }

            if (__instance.gameObject.scene.name == "ChallengeArea")
            {
                var portalLevel = bridge.LastPortalLevel;
                if (!string.IsNullOrEmpty(portalLevel))
                {
                    GameSetting.Instance.playerLastScene.SetValue(portalLevel, "RubiniteAP");
                    GameSetting.Instance.playerLastLeaveScene.SetValue(portalLevel, "RubiniteAP");
                    
                    RubiniteAPPlugin.Instance.StartCoroutine(bridge.TeleportPlayerToPodium(portalLevel));
                }
            }
        }
    }

    [HarmonyPatch(typeof(LeaderboardMenu), "ShowLeaderboard")]
    internal static class LeaderboardMenuShowLeaderboardPatch
    {
        private static bool Prefix(ref LevelData levelData)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive) return true;
            if (levelData == null) return true;

            if (MenuCore.Instance?.currentMenu is ChallengeWinMenu)
            {
                if (levelData.DisplayName == "DarkRubyLevel")
                {
                    bridge.AddLog("Blocked leaderboard for Dark Ruby (from Win Screen)");
                    return false;
                }
                return true;
            }

            string portalLevel = levelData.DisplayName;
            if (string.IsNullOrEmpty(portalLevel)) return true;

            if (bridge.TryGetMappedLevel(portalLevel, out var mappedLevel))
            {
                if (mappedLevel == "DarkRubyLevel")
                {
                    bridge.AddLog($"Blocked leaderboard for shuffled Dark Ruby (Portal: {portalLevel})");
                    return false;
                }

                if (mappedLevel != portalLevel)
                {
                    var overview = SingletonScriptableObject<LevelDataOverview>.Instance;
                    LevelData mappedData = overview?.FindUniqueId(mappedLevel);
                    
                    if (mappedData == null && overview != null)
                    {
                        foreach (var ld in overview.GetAllData())
                        {
                            if (ld != null && ld.DisplayName == mappedLevel) { mappedData = ld; break; }
                        }
                    }

                    if (mappedData != null)
                    {
                        bridge.AddLog($"Leaderboard remapped: '{portalLevel}' -> '{mappedLevel}'");
                        levelData = mappedData;
                    }
                }
            }
            else if (portalLevel == "DarkRubyLevel")
            {
                bridge.AddLog("Blocked leaderboard for Dark Ruby (no leaderboard exists)");
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(ChangeRubySkin), "ChangeSkin")]
    internal static class ChangeRubySkinPatch
    {
        private static void Prefix(ChangeRubySkin __instance)
        {
            RubiniteAPPlugin.Instance?.Bridge?.ForceTitleScreenRedBeforeSkinChange(__instance);
        }

        private static void Postfix(ChangeRubySkin __instance)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.IsHardcodedDarkRubyActive) return;
            if (__instance?.spriteRenderer == null) return;
            if (__instance.spriteRenderer.gameObject.name != "Girl") return;
            if (__instance.isPortrait) return;

            bool underPodium = false;
            var t = __instance.spriteRenderer.transform;
            while (t != null)
            {
                if (t.GetComponent<ChallengeLevelPoint>() != null) { underPodium = true; break; }
                t = t.parent;
            }
            if (underPodium) return;

            if (__instance.spriteRenderer.material != null
                && __instance.spriteRenderer.material.name.StartsWith("HardcodedDarkRubyMaterial"))
                return;

            try { bridge.ApplyHardcodedDarkRubySkinToRenderer(__instance.spriteRenderer); }
            catch (System.Exception ex) { bridge.AddLog("Dark Ruby re-skin failed: " + ex.Message); }
        }
    }

    [HarmonyPatch(typeof(CreateTalismanSelection), "Create")]
    internal static class CreateTalismanSelectionPatch
    {
        private static void Postfix(CreateTalismanSelection __instance)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive) return;

            if (__instance.gameObject.name.StartsWith("AP_ExtraSlot_"))
            {
                // Placeholder clones share their TalismanData asset with the template.
                return;
            }

            if (__instance.talismanData == null)
                return;

            var unlocked = bridge.IsShopTalismanUnlocked(__instance.talismanData.DisplayName);
            long locationId = bridge.GetShopLocationIdForDisplayName(__instance.talismanData.DisplayName);
            bool canPurchase = unlocked && locationId > 0 && !bridge.State.HasChecked(locationId);
            if (canPurchase)
                __instance.talismanData.storeState?.SetValue(AchievementState.ReadyToUnlock);
            ShopSlotVisibility.SetPurchaseAvailable(__instance, canPurchase);
            ShopSlotVisibility.SetVisible(__instance, unlocked);
        }
    }

    internal static class ShopSlotVisibility
    {
        public static void SetVisible(CreateTalismanSelection selection, bool visible)
        {
            if (selection == null) return;

            var grid = selection.GetComponentInParent<GridLayoutGroup>();
            var root = GetGridRoot(selection, grid != null ? grid.transform : null);
            root.gameObject.SetActive(visible);
        }

        public static void SetPurchaseAvailable(CreateTalismanSelection selection, bool available)
        {
            if (selection == null) return;
            foreach (var button in selection.GetComponentsInChildren<Button>(true))
                button.interactable = available;
        }

        public static Transform GetGridRoot(CreateTalismanSelection selection, Transform gridTransform)
        {
            var root = selection.transform;
            while (gridTransform != null && root.parent != null && root.parent != gridTransform)
                root = root.parent;
            return root;
        }
    }

    [HarmonyPatch(typeof(CreateTalismanSelection), "Setup")]
    internal static class CreateTalismanSetupPatch
    {
        private static void Prefix(CreateTalismanSelection __instance, out int __state)
        {
            __state = -1;
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive) return;
            var td = __instance.talismanData;
            if (td == null) return;

            if (!__instance.gameObject.name.StartsWith("AP_ExtraSlot_"))
            {
                long locationId = bridge.GetShopLocationIdForDisplayName(td.DisplayName);
                if (locationId > 0 && !bridge.State.HasChecked(locationId)
                    && bridge.IsShopTalismanUnlocked(td.DisplayName))
                    td.storeState?.SetValue(AchievementState.ReadyToUnlock);
            }

            int effective = CreateTalismanClickPricePatch.GetEffectivePrice(__instance, bridge);
            if (effective != td.costDust)
            {
                int original = td.costDust;
                if (CreateTalismanClickPricePatch.TrySetCostDust(td, effective))
                {
                    __state = original;
                }
            }
        }

        private static void Postfix(CreateTalismanSelection __instance, int __state)
        {
            if (__instance.gameObject.name.StartsWith("AP_ExtraSlot_"))
            {
                int index = int.Parse(__instance.gameObject.name.Replace("AP_ExtraSlot_", ""));
                var placeholderBridge = RubiniteAPPlugin.Instance?.Bridge;
                CreateItemMenuEnterPatch.ConfigurePlaceholder(__instance, index, placeholderBridge);
                if (__state >= 0 && __instance.talismanData != null)
                    CreateTalismanClickPricePatch.TrySetCostDust(__instance.talismanData, __state);
                return;
            }

            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive) return;

            var talismanData = __instance.talismanData;
            if (talismanData == null) return;
            string displayName = talismanData.DisplayName;

            long locationId = bridge.GetShopLocationIdForDisplayName(displayName);
            if (locationId > 0 && bridge.State.HasChecked(locationId))
            {
                if (__state >= 0)
                    CreateTalismanClickPricePatch.TrySetCostDust(talismanData, __state);
                ShopSlotVisibility.SetPurchaseAvailable(__instance, false);
                ShopSlotVisibility.SetVisible(__instance, false);
                return;
            }

            if (!bridge.IsShopTalismanUnlocked(displayName))
            {
                if (__state >= 0)
                    CreateTalismanClickPricePatch.TrySetCostDust(talismanData, __state);
                ShopSlotVisibility.SetPurchaseAvailable(__instance, false);
                ShopSlotVisibility.SetVisible(__instance, false);
                return;
            }
            if (locationId > 0)
                talismanData.storeState?.SetValue(AchievementState.ReadyToUnlock);
            ShopSlotVisibility.SetPurchaseAvailable(__instance, locationId > 0);
            ShopSlotVisibility.SetVisible(__instance, true);

            if (string.IsNullOrEmpty(displayName)
                || displayName.Contains("Exhibition")
                || displayName.Contains("TalismanData_")
                || displayName == "X19_Shield"
                || displayName == "無"
                || displayName.Contains("展場"))
            {
                UnityEngine.Object.Destroy(__instance.gameObject);
                return;
            }

            int effective = bridge.GetEffectiveShopPrice(__instance.talismanData);
            if (effective != __instance.talismanData.costDust)
            {
                foreach (var tmp in __instance.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
                {
                    if (tmp.gameObject.name == "Cost (TMP)")
                    {
                        tmp.text = effective.ToString();
                        break;
                    }
                }
            }

            if (bridge.TryGetScoutedShopItem(displayName, out var apItemName))
            {
                ReplaceShopDisplay(__instance, displayName, apItemName, bridge);
            }

            if (__state >= 0 && __instance.talismanData != null)
            {
                CreateTalismanClickPricePatch.TrySetCostDust(__instance.talismanData, __state);
            }
        }

        internal static void ReplaceShopDisplay(
            CreateTalismanSelection selection,
            string talismanDisplayName,
            string apItemName,
            GameBridge bridge)
        {
            if (selection == null || bridge == null) return;

            string displayName = apItemName;
            if (apItemName.StartsWith("Talisman: "))
            {
                displayName = apItemName.Substring("Talisman: ".Length);
            }
            else if (apItemName.StartsWith("Skill "))
            {
                int colon = apItemName.IndexOf(": ");
                if (colon > 0) displayName = apItemName.Substring(colon + 2);
            }

            foreach (var tmp in selection.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
            {
                if (tmp.gameObject.name == "Name_Text (TMP)")
                {
                    tmp.text = displayName;
                    break;
                }
            }

            foreach (var img in selection.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if (img.gameObject.name != "Icon") continue;
                bool isCrossGame = bridge.IsCrossGameShopItem(talismanDisplayName);
                var sprite = bridge.PickIconForItem(apItemName, isCrossGame);
                if (sprite != null) img.sprite = sprite;
                break;
            }
        }
    }

    [HarmonyPatch(typeof(CreateTalismanSelection), "SetInformation")]
    internal static class CreateTalismanSetInformationPatch
    {
        private static void Postfix(CreateTalismanSelection __instance)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive) return;

            var menu = MenuCore.Instance?.currentMenu as CreateItemMenu;
            if (menu == null) return;
            if (!CreateItemMenuEnterPatch.HasVisibleUnpurchasedSlots(menu, bridge))
            {
                ClearShopInformation(menu);
                return;
            }
            if (!__instance.gameObject.activeInHierarchy)
                return;

            string title;
            string description;
            Sprite icon;
            int notchCount;
            string apItemName = null;
            string displayName = null;

            var objectName = __instance.gameObject.name;
            if (objectName.StartsWith("AP_ExtraSlot_")
                && int.TryParse(objectName.Substring("AP_ExtraSlot_".Length), out var slotIndex))
            {
                if (bridge.TryGetScoutedShopItemForSlotIndex(slotIndex, out var slotTitle, out var slotItem))
                {
                    apItemName = slotItem;
                    displayName = slotTitle;
                    if (!bridge.TryGetShopDescription(slotTitle, slotItem,
                        out title, out description, out icon, out notchCount))
                        return;
                }
                else
                {
                    title = $"Shop Slot {slotIndex + 1}";
                    description = "A replacement Archipelago shop slot. Purchase it to check this Wichiwi Shop location.";
                    icon = bridge.PickIconForItem("Shop Expansion: Shop Slot");
                    notchCount = 0;
                }
            }
            else
            {
                displayName = __instance.talismanData?.DisplayName;
                if (string.IsNullOrEmpty(displayName)) return;

                if (displayName.Contains("Exhibition")
                    || displayName.Contains("TalismanData_")
                    || displayName == "X19_Shield"
                    || displayName == "無"
                    || displayName.Contains("展場"))
                    return;

                if (!bridge.TryGetScoutedShopItem(displayName, out apItemName)) return;
                if (!bridge.TryGetShopDescription(displayName, apItemName,
                    out title, out description, out icon, out notchCount)) return;
            }

            // Cross-game items already contain the importance in their description.
            // Local items (talismans/sigils/skills) need it appended here.
            string finalDesc = description ?? "";
            bool isCrossGame = !string.IsNullOrEmpty(displayName)
                            && bridge.IsCrossGameShopItem(displayName);
            if (!isCrossGame && !string.IsNullOrEmpty(apItemName) && !string.IsNullOrEmpty(displayName))
            {
                string importance = bridge.GetImportanceDisplay(apItemName, displayName);
                finalDesc = $"{finalDesc}\nImportance: {importance}";
            }

            SetRubyText(menu, "informationTitleText", title);
            SetRubyText(menu, "informationExplainText", finalDesc);

            if (icon != null)
            {
                menu.SetTalismanInformationIcon(icon);
                SetInformationIconVisible(menu, true);
            }

            // Notches: show real count for talismans, hide all for other items
            if (menu.explainNotches != null)
            {
                for (int i = 0; i < menu.explainNotches.Length; i++)
                {
                    if (menu.explainNotches[i] != null)
                        menu.explainNotches[i].gameObject.SetActive(i < notchCount);
                }
            }
        }

        internal static void ClearShopInformation(CreateItemMenu menu)
        {
            if (menu == null) return;

            SetRubyText(menu, "informationTitleText", "Shop is empty");
            SetRubyText(menu, "informationExplainText", "All currently unlocked shop slots have been purchased.");
            menu.SetTalismanInformationIcon((Sprite)null);
            SetInformationIconVisible(menu, false);

            if (menu.explainNotches == null) return;
            foreach (var notch in menu.explainNotches)
                if (notch != null)
                    notch.gameObject.SetActive(false);
        }

        private static void SetInformationIconVisible(CreateItemMenu menu, bool visible)
        {
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance;

            for (var type = menu.GetType(); type != null; type = type.BaseType)
            {
                foreach (var field in type.GetFields(flags))
                {
                    bool isIconField =
                        field.Name.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0
                        && (field.Name.IndexOf("information", StringComparison.OrdinalIgnoreCase) >= 0
                            || field.Name.IndexOf("talisman", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (!isIconField
                        || !typeof(UnityEngine.UI.Image).IsAssignableFrom(field.FieldType))
                        continue;

                    var image = field.GetValue(menu) as UnityEngine.UI.Image;
                    if (image == null)
                        continue;

                    image.enabled = visible;
                    image.gameObject.SetActive(visible);
                    return;
                }
            }

            foreach (var image in menu.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if (image == null || image.gameObject.name.IndexOf(
                    "icon", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                bool inInformationPanel = false;
                for (var parent = image.transform; parent != null && parent != menu.transform; parent = parent.parent)
                {
                    if (parent.name.IndexOf("information", StringComparison.OrdinalIgnoreCase) >= 0
                        || parent.name.IndexOf("talisman", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        inInformationPanel = true;
                        break;
                    }
                }

                if (!inInformationPanel)
                    continue;

                image.enabled = visible;
                image.gameObject.SetActive(visible);
                return;
            }
        }

        private static void SetRubyText(object menu, string fieldName, string value)
        {
            if (menu == null) return;
            var menuType = menu.GetType();
            var field = menuType.GetField(fieldName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (field == null) return;
            var target = field.GetValue(menu);
            if (target == null) return;

            var targetType = target.GetType();

            var uneditedField = targetType.GetField("uneditedText",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (uneditedField != null && uneditedField.FieldType == typeof(string))
            {
                uneditedField.SetValue(target, value);
            }
            else
            {
                var uneditedProp = targetType.GetProperty("uneditedText",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (uneditedProp != null && uneditedProp.CanWrite && uneditedProp.PropertyType == typeof(string))
                    uneditedProp.SetValue(target, value);
            }

            var setText = targetType.GetMethod("SetText",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                null, new[] { typeof(string) }, null);
            if (setText != null)
                setText.Invoke(target, new object[] { value });
        }
    }

    [HarmonyPatch(typeof(CreateItemMenu), "Process")]
    internal static class CreateItemMenuProcessPatch
    {
        private static bool Prefix(CreateItemMenu __instance)
        {
            if (SingletonPrefab<ApplicationCore>.Instance.controller.CollectionMenu())
            {
                MenuCore.Instance.collectMenu.OpenCollectMenu(0);
                return false; 
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(CreateTalismanSelection), "Click")]
    internal static class CreateTalismanClickPricePatch
    {
        private static bool Prefix(CreateTalismanSelection __instance)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive) return true;

            var objectName = __instance.gameObject.name;
            if (objectName.StartsWith("AP_ExtraSlot_")
                && int.TryParse(objectName.Substring("AP_ExtraSlot_".Length), out var slotIndex))
            {
                long locationId = bridge.GetShopLocationIdForIndex(slotIndex);
                if (locationId <= 0)
                {
                    bridge.AddLog($"Shop purchase blocked: no AP location is mapped to slot {slotIndex + 1}.");
                    return false;
                }

                int price = GetEffectivePrice(__instance, bridge);
                bridge.HandleShopPurchaseById(locationId, $"Slot {slotIndex + 1}", price);
                if (bridge.State.HasChecked(locationId))
                {
                    ShopSlotVisibility.SetVisible(__instance, false);
                    CreateItemMenuEnterPatch.ClearInformationIfNoVisibleSlots(
                        MenuCore.Instance?.currentMenu as CreateItemMenu);
                }
                return false;
            }

            if (__instance.talismanData == null) return true;

            string displayName = __instance.talismanData.DisplayName;
            long locId = bridge.GetShopLocationIdForDisplayName(displayName);
            if (locId <= 0) return true;

            int effectivePrice = GetEffectivePrice(__instance, bridge);
            bridge.HandleShopPurchaseById(locId, displayName, effectivePrice);
            if (bridge.State.HasChecked(locId))
            {
                ShopSlotVisibility.SetVisible(__instance, false);
                CreateItemMenuEnterPatch.ClearInformationIfNoVisibleSlots(
                    MenuCore.Instance?.currentMenu as CreateItemMenu);
            }
            return false;
        }

        internal static int GetEffectivePrice(CreateTalismanSelection selection, GameBridge bridge)
        {
            var objectName = selection.gameObject.name;
            if (objectName.StartsWith("AP_ExtraSlot_")
                && int.TryParse(objectName.Substring("AP_ExtraSlot_".Length), out var slotIndex))
                return bridge.GetEffectiveShopPriceForSlot(slotIndex);
            return bridge.GetEffectiveShopPrice(selection.talismanData);
        }

        internal static bool TrySetCostDust(TalismanData td, int value)
        {
            if (td == null) return false;
            try
            {
                var type = typeof(TalismanData);
                var field = type.GetField("costDust",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (field != null && field.FieldType == typeof(int))
                {
                    field.SetValue(td, value);
                    return true;
                }
                var prop = type.GetProperty("costDust",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (prop != null && prop.CanWrite && prop.PropertyType == typeof(int))
                {
                    prop.SetValue(td, value);
                    return true;
                }
            }
            catch (System.Exception ex)
            {
                RubiniteAPPlugin.Log?.LogWarning("Could not set talisman Blood Dust price: " + ex.Message);
                return false;
            }
            RubiniteAPPlugin.Log?.LogWarning("TalismanData has no writable costDust member.");
            return false;
        }
    }

    [HarmonyPatch(typeof(CreateTalismanSelection), "Create")]
    internal static class CreateTalismanCreatePricePatch
    {
        private static void Prefix(CreateTalismanSelection __instance, out int __state)
        {
            __state = -1;
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive) return;
            var td = __instance.talismanData;
            if (td == null) return;

            int effective = CreateTalismanClickPricePatch.GetEffectivePrice(__instance, bridge);
            if (effective == td.costDust) return;

            int original = td.costDust;
            if (!CreateTalismanClickPricePatch.TrySetCostDust(td, effective)) return;
            __state = original;
        }

        private static void Postfix(CreateTalismanSelection __instance, int __state)
        {
            if (__state < 0) return;
            if (__instance.talismanData != null)
                CreateTalismanClickPricePatch.TrySetCostDust(__instance.talismanData, __state);
        }
    }

    [HarmonyPatch(typeof(SkillTreeFrame), "Init")]
    internal static class SkillTreeInitDisplayPatch
    {
        private static void Postfix(SkillTreeFrame __instance)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            var data = __instance.skillTreeData;
            if (data == null) return;

            if (bridge == null || !bridge.ApRunActive)
            {
                ApplyVanillaIcon(__instance, bridge);
                return;
            }

            if (bridge.IsSkillPurchased(data.DisplayName)
                || !data.isKnown.GetValue()
                || !__instance.IsKnowable()
                || !bridge.TryGetScoutedSkillTreeItem(data.DisplayName, out var apItemName))
            {
                ApplyVanillaIcon(__instance, bridge);
                return;
            }

            ApplyItemIcon(__instance, bridge, apItemName);
        }

        internal static void ApplyItemIcon(SkillTreeFrame frame, GameBridge bridge, string apItemName)
        {
            if (frame == null || bridge == null || string.IsNullOrEmpty(apItemName))
                return;

            var apIcon = bridge.PickIconForItem(apItemName);
            if (apIcon == null) return;

            Vector3 scaled = apItemName.StartsWith("Skill ") ? Vector3.one : new Vector3(0.65f, 0.65f, 1f);
            if (frame.icon != null)
            {
                frame.icon.sprite = apIcon;
                frame.icon.rectTransform.localScale = scaled;
            }
            if (frame.fakeIcon != null)
            {
                frame.fakeIcon.sprite = apIcon;
                frame.fakeIcon.rectTransform.localScale = scaled;
            }
        }

        internal static void ApplyVanillaIcon(SkillTreeFrame frame, GameBridge bridge)
        {
            if (frame == null) return;

            var vanillaIcon = frame.skillData?.Icon;
            if (vanillaIcon == null && bridge != null && frame.skillTreeData != null)
            {
                var vanillaItemName = GameData.SkillItemName(frame.skillTreeData.DisplayName);
                vanillaIcon = bridge.PickIconForItem(vanillaItemName);
            }
            if (vanillaIcon == null) return;

            if (frame.icon != null)
            {
                frame.icon.sprite = vanillaIcon;
                frame.icon.rectTransform.localScale = Vector3.one;
            }
            if (frame.fakeIcon != null)
            {
                frame.fakeIcon.sprite = vanillaIcon;
                frame.fakeIcon.rectTransform.localScale = Vector3.one;
            }
        }

        internal static void RefreshScoutedIcon(GameBridge bridge, string treeDisplayId, string apItemName)
        {
            if (bridge == null || string.IsNullOrEmpty(treeDisplayId))
                return;

            foreach (var frame in Resources.FindObjectsOfTypeAll<SkillTreeFrame>())
            {
                if (frame == null || !frame.gameObject.scene.IsValid()
                    || !frame.gameObject.activeInHierarchy
                    || frame.skillTreeData?.DisplayName != treeDisplayId)
                    continue;

                if (bridge.IsSkillPurchased(treeDisplayId)
                    || !frame.skillTreeData.isKnown.GetValue()
                    || !frame.IsKnowable())
                    ApplyVanillaIcon(frame, bridge);
                else
                    ApplyItemIcon(frame, bridge, apItemName);
            }
        }
    }

    [HarmonyPatch(typeof(SkillTreeMenu), "SetSelectFrameDetailPaper")]
    internal static class SkillTreeMenuDetailPatch
    {
        private static void Postfix(SkillTreeMenu __instance, SkillTreeFrame skillTreeFrame)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive) return;
            bridge.RequestVisibleSkillTreeHints(GetVisibleSkillTreeIds());
            if (skillTreeFrame == null) return;

            var data = skillTreeFrame.skillTreeData;
            if (data == null) return;

            bool isUnknown = !data.isKnown.GetValue() || !skillTreeFrame.IsKnowable();
            if (isUnknown)
            {
                if (__instance.selectedIcon != null)
                    __instance.selectedIcon.rectTransform.localScale = Vector3.one;
                return;
            }

            if (bridge.IsSkillPurchased(data.DisplayName))
            {
                EnsureVanillaSkillIcon(__instance, skillTreeFrame);
                return;
            }

            bool hasScoutedItem = bridge.TryGetScoutedSkillTreeItem(data.DisplayName, out var apItemName);

            if (!hasScoutedItem)
            {
                if (__instance.selectedIcon != null) __instance.selectedIcon.rectTransform.localScale = Vector3.one;
                return;
            }

            string apTitle = apItemName;
            string apDesc = "An item from another world.";
            Sprite apIcon = bridge.PickIconForItem(apItemName);

            if (bridge.TryGetShopDescription(data.DisplayName, apItemName,
                out var t, out var d, out var i, out _))
            {
                apTitle = t;
                apDesc = d;
                if (i != null) apIcon = i;
            }

            __instance.selectedTitleText.uneditedText = apTitle;

            string importance = bridge.GetImportanceDisplay(apItemName, data.DisplayName);
            string itemDescription = bridge.IsCrossGameShopItem(data.DisplayName)
                ? "An item from another world."
                : apDesc ?? "";
            string combined = $"You will receive: <b>{apItemName}</b> [{importance}]\n{itemDescription}";

            __instance.selectedDescriptionText_NoVideo?.SetText(combined);
            __instance.selectedDescriptionText_HasVideo?.SetText(combined);

            if (__instance.descriptionSection_HasVideo != null)
                __instance.descriptionSection_HasVideo.SetActive(false);
            if (__instance.descriptionSection_NoVideo != null)
                __instance.descriptionSection_NoVideo.SetActive(true);
            try { __instance.videoPlayer?.Stop(); } catch { }

            if (apIcon != null && __instance.selectedIcon != null)
            {
                __instance.selectedIcon.sprite = apIcon;
                __instance.selectedIcon.rectTransform.localScale = new Vector3(0.8f, 0.8f, 1f);
            }
        }

        internal static void RefreshPurchasedItemDetails(SkillTreeFrame frame, GameBridge bridge)
        {
            var menu = MenuCore.Instance?.currentMenu as SkillTreeMenu;
            if (menu == null || frame?.skillTreeData == null
                || bridge == null
                || !bridge.IsSkillPurchased(frame.skillTreeData.DisplayName))
                return;

            menu.SetSelectFrameDetailPaper(frame);
        }

        private static void EnsureVanillaSkillIcon(SkillTreeMenu menu, SkillTreeFrame frame)
        {
            var vanillaIcon = frame.skillData?.Icon;
            if (vanillaIcon != null && menu.selectedIcon != null)
            {
                menu.selectedIcon.sprite = vanillaIcon;
                menu.selectedIcon.rectTransform.localScale = new Vector3(0.8f, 0.8f, 1f);
            }
        }

        private static IEnumerable<string> GetVisibleSkillTreeIds()
        {
            foreach (var frame in Resources.FindObjectsOfTypeAll<SkillTreeFrame>())
            {
                if (frame == null || !frame.gameObject.scene.IsValid() || !frame.gameObject.activeInHierarchy)
                    continue;

                var data = frame.skillTreeData;
                if (data != null && data.isKnown.GetValue() && frame.IsKnowable())
                    yield return data.DisplayName;
            }
        }

        internal static IEnumerable<string> GetVisibleSkillTreeIdsForHintRequest()
        {
            return GetVisibleSkillTreeIds();
        }
    }

    [HarmonyPatch(typeof(SkillTreeData), "get_cost")]
    internal static class SkillTreeEffectiveCostPatch
    {
        private static bool Prefix(SkillTreeData __instance, ref int __result)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive || __instance == null)
                return true;

            if (!bridge.TryGetEffectiveSkillPrice(__instance.DisplayName, out __result))
                return true;

            return false;
        }
    }

    [HarmonyPatch(typeof(SkillTreeFrame), "Init")]
    internal static class SkillTreeInitCostPatch
    {
        private static void Postfix(SkillTreeFrame __instance)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive) return;

            var data = __instance.skillTreeData;
            if (data == null) return;

            if (bridge.TryGetEffectiveSkillPrice(data.DisplayName, out var price))
            {
                __instance.costText?.SetText(price.ToString());
                __instance.costText2?.SetText(price.ToString());
            }

            // Hide the "purchasable" indicator for already-bought skills.
            if (bridge.IsSkillPurchased(data.DisplayName))
            {
                __instance.purchasableFrame?.SetActive(false);
            }
            else if (!GameData.IsFreeSkill(data.DisplayName)
                && data.isKnown.GetValue()
                && SkillTreeCheckUnlockablePatch.HasRequiredParentsPurchased(__instance, bridge))
            {
                __instance.purchasableFrame?.SetActive(true);
                __instance.costText?.gameObject.SetActive(true);
                __instance.costText2?.gameObject.SetActive(true);
            }
        }
    }

    [HarmonyPatch(typeof(SkillTreeFrame), "Unlock")]
    internal static class SkillTreeUnlockPatch
    {
        private static bool Prefix(SkillTreeFrame __instance)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive) return true;

            var data = __instance.skillTreeData;
            if (data == null) return true;
            var treeId = data.DisplayName;
            if (string.IsNullOrEmpty(treeId)) return true;
            if (!GameData.TryGetSkillTitle(treeId, out var title)) return true;

            if (GameData.IsFreeSkill(treeId))
                return true;

            if (bridge.IsSkillPurchased(treeId))
                return false;

            if (!SkillTreeCheckUnlockablePatch.HasRequiredParentsPurchased(__instance, bridge))
                return false;

            int price = data.cost;
            if (bridge.TryGetEffectiveSkillPrice(treeId, out var effective))
                price = effective;

            if (!SkillTreeCheckUnlockablePatch.CanAffordSkillTreePurchase(__instance, bridge))
            {
                __instance.purchasableFrame?.SetActive(false);
                return false;
            }

            if (!bridge.HandleSkillTreePurchase(treeId, title, price))
                return false;

            try
            {
                __instance.purchasableFrame?.SetActive(false);
                __instance.animatorController?.ChangeAnimation(
                    SkillTreeFrame.SkillTreeFrameAnimationType.LockToUnlock);
            }
            catch { }

            try { __instance.Init(); }
            catch { }
            SkillTreeInitDisplayPatch.ApplyVanillaIcon(__instance, bridge);
            SkillTreeMenuDetailPatch.RefreshPurchasedItemDetails(__instance, bridge);

            return false;
        }
    }

    [HarmonyPatch(typeof(SkillTreeFrame), "Update")]
    internal static class SkillTreeFrameUpdatePatch
    {
        private static void Prefix(SkillTreeFrame __instance)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive) return;
            var data = __instance.skillTreeData;
            if (data == null) return;

            bool received = bridge.IsSkillReceived(data.DisplayName);
            bool purchased = bridge.IsSkillPurchased(data.DisplayName);

            if (received && !purchased && !GameData.IsFreeSkill(data.DisplayName))
            {
                data.skillTreeDataDetail?.isUnlocked?.SetValue(false);
                data.secondRunSkillTreeDataDetail?.isUnlocked?.SetValue(false);
            }
        }

        private static void Postfix(SkillTreeFrame __instance)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            var data = __instance.skillTreeData;
            if (data == null) return;

            if (bridge == null || !bridge.ApRunActive)
            {
                var vanillaIcon = __instance.skillData?.Icon;
                if (vanillaIcon != null)
                {
                    if (__instance.icon != null && __instance.icon.sprite != vanillaIcon) { __instance.icon.sprite = vanillaIcon; __instance.icon.rectTransform.localScale = Vector3.one; }
                    if (__instance.fakeIcon != null && __instance.fakeIcon.sprite != vanillaIcon) { __instance.fakeIcon.sprite = vanillaIcon; __instance.fakeIcon.rectTransform.localScale = Vector3.one; }
                }
                return;
            }

            bool isPurchased = bridge.IsSkillPurchased(data.DisplayName);
            bool isReceived = bridge.IsSkillReceived(data.DisplayName);

            if (!isPurchased
                && data.isKnown.GetValue()
                && SkillTreeCheckUnlockablePatch.HasRequiredParentsPurchased(__instance, bridge)
                && bridge.TryGetScoutedSkillTreeItem(data.DisplayName, out var apItemName))
            {
                SkillTreeInitDisplayPatch.ApplyItemIcon(__instance, bridge, apItemName);
            }
            else
            {
                SkillTreeInitDisplayPatch.ApplyVanillaIcon(__instance, bridge);
            }

            if (GameData.IsFreeSkill(data.DisplayName))
                return;

            if (!isPurchased
                && data.isKnown.GetValue()
                && SkillTreeCheckUnlockablePatch.HasRequiredParentsPurchased(__instance, bridge))
            {
                __instance.costText?.gameObject.SetActive(true);
                __instance.costText2?.gameObject.SetActive(true);
            }

            var currentAnim = __instance.animatorController.currentAnimation;
            var targetAnim = currentAnim;

            if (isPurchased)
            {
                if (isReceived)
                {
                    if (currentAnim == SkillTreeFrame.SkillTreeFrameAnimationType.Disable ||
                        currentAnim == SkillTreeFrame.SkillTreeFrameAnimationType.Lock)
                    {
                        targetAnim = SkillTreeFrame.SkillTreeFrameAnimationType.Unlock;
                    }
                }
                else
                {
                    if (currentAnim != SkillTreeFrame.SkillTreeFrameAnimationType.Disable &&
                        currentAnim != SkillTreeFrame.SkillTreeFrameAnimationType.LockToUnlock)
                    {
                        targetAnim = SkillTreeFrame.SkillTreeFrameAnimationType.Disable;
                    }
                }
            }
            else
            {
                bool prerequisitesMet =
                    SkillTreeCheckUnlockablePatch.HasRequiredParentsPurchased(__instance, bridge);

                __instance.purchasableFrame?.SetActive(prerequisitesMet);

                if (prerequisitesMet)
                {
                    if (currentAnim == SkillTreeFrame.SkillTreeFrameAnimationType.Disable)
                        targetAnim = SkillTreeFrame.SkillTreeFrameAnimationType.Lock;
                }
                else
                {
                    if (currentAnim == SkillTreeFrame.SkillTreeFrameAnimationType.Lock)
                        targetAnim = SkillTreeFrame.SkillTreeFrameAnimationType.Disable;
                }
            }

            if (targetAnim != currentAnim)
            {
                __instance.animatorController.ChangeAnimation(targetAnim);
            }
        }
    }

    [HarmonyPatch(typeof(SkillTreeFrame), "CheckUnlockable")]
    internal static class SkillTreeCheckUnlockablePatch
    {
        private static bool Prefix(SkillTreeFrame __instance, ref bool __result)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive) return true;

            if (GameData.IsFreeSkill(__instance.skillTreeData?.DisplayName))
            {
                __result = true;
                return false;
            }

            if (!HasRequiredParentsPurchased(__instance, bridge))
            {
                __result = false;
                return false;
            }

            __result = CanAffordSkillTreePurchase(__instance, bridge);
            return false;
        }

        internal static bool HasRequiredParentsPurchased(SkillTreeFrame frame, GameBridge bridge)
        {
            if (frame == null || bridge == null)
                return false;

            var parents = frame.needUnlocked;
            if (parents == null)
                return true;

            for (int i = 0; i < parents.Length; i++)
            {
                var parent = parents[i];
                if (parent == null) continue;
                var parentData = parent.skillTreeData;
                if (parentData == null) continue;

                bool parentFree = GameData.IsFreeSkill(parentData.DisplayName);
                bool parentPurchased = bridge.IsSkillPurchased(parentData.DisplayName);

                if (!parentFree && !parentPurchased)
                    return false;
            }

            return true;
        }

        internal static bool CanAffordSkillTreePurchase(SkillTreeFrame frame, GameBridge bridge)
        {
            var data = frame?.skillTreeData;
            if (data == null || bridge == null)
                return false;

            int price = data.cost;
            if (bridge.TryGetEffectiveSkillPrice(data.DisplayName, out var effectivePrice))
                price = effectivePrice;

            var core = GameBridge.GetGamePlayCore();
            var settingsDust = GameSetting.Instance?.dustFlag;
            if (core?.dust == null && settingsDust == null)
                return false;

            int availableDust = core?.dust != null
                ? core.dust.GetValue()
                : settingsDust.GetValue();
            if (price < 0)
                return false;

            if (availableDust < price)
                return false;

            return true;
        }
    }

    [HarmonyPatch(typeof(SkillTreeFrame), "OnSelect")]
    internal static class SkillTreeFrameOnSelectPatch
    {
        private static void Postfix(SkillTreeFrame __instance)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive) return;
            var data = __instance.skillTreeData;
            if (data == null) return;

            // If the slot is already purchased, make sure the "hold to buy" ring 
            // stays hidden, even though the game's vanilla OnSelect might try to show it.
            if (bridge.IsSkillPurchased(data.DisplayName))
            {
                __instance.purchasableFrame?.SetActive(false);
                SkillTreeInitDisplayPatch.ApplyVanillaIcon(__instance, bridge);
                SkillTreeMenuDetailPatch.RefreshPurchasedItemDetails(__instance, bridge);
                return;
            }

            if (!GameData.IsFreeSkill(data.DisplayName))
            {
                bool prerequisitesMet =
                    SkillTreeCheckUnlockablePatch.HasRequiredParentsPurchased(__instance, bridge);
                __instance.purchasableFrame?.SetActive(prerequisitesMet);
            }
        }
    }

    [HarmonyPatch(typeof(ChallengeLevelMenu), "SetLevelInformation")]
    internal static class ChallengeLevelMenuSetLevelInformationPatch
    {
        private static void Postfix(ChallengeLevelMenu __instance)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive) return;
            if (__instance.currentLevelSelection == null
                || __instance.currentLevelSelection.levelData == null) return;

            var portalLevel = __instance.currentLevelSelection.levelData.DisplayName;
            if (string.IsNullOrEmpty(portalLevel)) return;

            if (bridge.TryGetMappedLevel(portalLevel, out var mappedLevel)
                && mappedLevel != portalLevel)
            {
                var mappedData = SingletonScriptableObject<LevelDataOverview>.Instance
                    .FindUniqueId(mappedLevel);
                if (mappedData != null)
                {
                    if (__instance.informationBossImage != null
                        && mappedData.MapSelect_ScenePic != null)
                        __instance.informationBossImage.sprite = mappedData.MapSelect_ScenePic;

                    if (__instance.informationLocalize != null
                        && mappedData.detailTerm != null)
                        __instance.informationLocalize.Term = mappedData.detailTerm.term;
                }
            }
        }
    }

    [HarmonyPatch(typeof(ChallengeLevelSelection), "Setup")]
    internal static class ChallengeLevelSelectionSetupPatch
    {
        private static void Postfix(ChallengeLevelSelection __instance, LevelData levelData)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive) return;

            var portalLevel = levelData?.DisplayName;
            if (string.IsNullOrEmpty(portalLevel)) return;

            if (bridge.TryGetMappedLevel(portalLevel, out var mappedLevel)
                && mappedLevel != portalLevel)
            {
                var mappedData = SingletonScriptableObject<LevelDataOverview>.Instance
                    .FindUniqueId(mappedLevel);
                if (mappedData != null)
                {
                    if (__instance.BossLevelIcon != null
                        && mappedData.bossIcon != null)
                        __instance.BossLevelIcon.sprite = mappedData.bossIcon;

                    if (__instance.levelNameLocalize != null
                        && mappedData.titleTerm != null)
                        __instance.levelNameLocalize.Term = mappedData.titleTerm.term;
                }
            }
        }
    }

    [HarmonyPatch(typeof(BloodDust), "SetBeGot")]
    internal static class BloodDustSetBeGotPatch
    {
        private static void Postfix(BloodDust __instance)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive) return;
            if (bridge.BossDustMultiplier <= 1) return;

            int baseAmount = (int)__instance.dustType;
            int extraAmount = baseAmount * (bridge.BossDustMultiplier - 1);

            if (extraAmount > 0)
            {
                var dust = GamePlayCore.Instance.dust;
                dust.SetValue(dust.GetValue() + extraAmount, "RubiniteAP");
                
                bridge.AddLog($"Multiplied dust pickup: +{baseAmount} -> +{baseAmount + extraAmount}");
            }
        }
    }

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

            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive) return;
            if (bridge.DarkRubyDeathHandled) return;

            var lm = GamePlayCore.Instance?.levelManager;

            if (lm?.levelTimer != null)
            {
                lm.levelTimer.Pause();
                bridge.DarkRubyTimerPausedForCutscene = true;
                bridge.AddLog("Dark Ruby: timer paused (boss died)");
            }

            bridge.DarkRubyDeathHandled = true;
            bridge.AddLog("Dark Ruby: death detected via Hurt() → scheduling Win Menu");
            RubiniteAPPlugin.Instance.StartCoroutine(OpenWinMenuAfterDeath(bridge));
        }

        private static System.Collections.IEnumerator OpenWinMenuAfterDeath(GameBridge bridge)
        {
            yield return new WaitForSeconds(3.0f);

            var lm = GamePlayCore.Instance.levelManager;
            if (lm == null) yield break;

            bridge.DarkRubyCompleted = true;
            bridge.HandleLevelCompleted(lm);
            bridge.ClearPendingPodiumClick();

            try
            {
                var winMenu = GamePlayCore.Instance.challengeWinMenu;
                if (winMenu != null)
                {
                    MenuCore.Instance.EnterMenu(winMenu);
                    bridge.AddLog("Dark Ruby: ChallengeWinMenu opened immediately after death");
                }
                else
                {
                    bridge.AddLog("WARNING: challengeWinMenu is null");
                }
            }
            catch (System.Exception ex)
            {
                bridge.AddLog("Failed to open Win Menu: " + ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(CutSceneController), "StartCutScene")]
    internal static class CutSceneControllerStartCutScenePatch
    {
        private static bool Prefix(CutSceneController __instance)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive) return true;

            var lm = GamePlayCore.Instance?.levelManager;
            if (lm?.levelData?.DisplayName != "DarkRubyLevel") return true;

            bridge.LastDarkRubyCutsceneName = __instance?.name;
            bridge.AddLog($"Dark Ruby: cutscene started '{__instance?.name}'");

            bool bossDead = lm.boss != null && lm.boss.isDead;
            bool winMenuOpen = MenuCore.Instance?.currentMenu == GamePlayCore.Instance?.challengeWinMenu;

            if ((bossDead && bridge.DarkRubyDeathHandled) || winMenuOpen)
            {
                bridge.AddLog($"Dark Ruby: blocked cutscene '{__instance?.name}'");
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(TalkingChooseMenu), "SetChoices")]
    internal static class TalkingChooseMenuSetChoicesPatch
    {
        private static void Postfix(TalkingChooseMenu __instance, string[] choicesText)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive) return;
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "ChallengeArea") return;
            if (__instance.choices == null || choicesText == null) return;

            var player = GamePlayCore.Instance?.player;
            if (player == null) return;

            string nearbyPortalLevel = null;
            foreach (var point in Resources.FindObjectsOfTypeAll<ChallengeLevelPoint>())
            {
                if (point?.gameObject == null || !point.gameObject.scene.IsValid()) continue;
                var dist = Vector2.Distance(player.transform.position, point.transform.position);
                if (dist < 3.5f)
                {
                    nearbyPortalLevel = point.levelName?.displayName;
                    break;
                }
            }

            if (string.IsNullOrEmpty(nearbyPortalLevel)) return;

            string targetBossLevel = nearbyPortalLevel;
            if (bridge.TryGetMappedLevel(nearbyPortalLevel, out var mapped))
                targetBossLevel = mapped;

            if (targetBossLevel == "DarkRubyLevel")
            {
                bridge.AddLog($"TalkingChooseMenu: near Podium leading to Dark Ruby (portal: {nearbyPortalLevel}), blocking Illusion");
                for (int i = 0; i < __instance.choices.Length; i++)
                {
                    var choice = __instance.choices[i];
                    if (choice == null) continue;
                    string text = choicesText[i] ?? "";
                    if (text.Contains("幻影") || text.Contains("幻"))
                    {
                        if (choice.button != null) choice.button.interactable = false;
                        choice.gameObject.SetActive(false);
                        bridge.AddLog($"TalkingChooseMenu: blocked Illusion [{i}] '{text}'");
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(GamePlayCore), "EnterCutSceneMode")]
    internal static class EnterCutSceneModeTimerPausePatch
    {
        private static void Postfix()
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive) return;

            var lm = GamePlayCore.Instance?.levelManager;
            if (lm?.levelData?.DisplayName != "DarkRubyLevel") return;
            if (lm.levelTimer == null) return;

            bridge.AddLog($"Dark Ruby: EnterCutSceneMode (current='{bridge.LastDarkRubyCutsceneName}')");

            if (lm.levelTimer.timer > 0f || bridge.DarkRubyTimerPausedForCutscene)
            {
                lm.levelTimer.Pause();
                bridge.DarkRubyTimerPausedForCutscene = true;
                bridge.AddLog("Dark Ruby: timer paused (cutscene entered)");
            }
        }
    }

    [HarmonyPatch(typeof(GamePlayCore), "ExitCutSceneMode")]
    internal static class ExitCutSceneModeTimerResumePatch
    {
        private static void Postfix()
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive) return;

            var lm = GamePlayCore.Instance?.levelManager;
            if (lm?.levelData?.DisplayName != "DarkRubyLevel") return;

            var introCutscenes = new[] { "EnterGameFromTitleCutscene", "EnterGameFromMasterLevelCutscene" };
            if (System.Array.IndexOf(introCutscenes, bridge.LastDarkRubyCutsceneName) >= 0)
            {
                var player = GamePlayCore.Instance?.player;
                if (player != null && lm.playerSpawnPoint != null)
                {
                    var pos = lm.playerSpawnPoint.position;
                    pos.z = player.transform.position.z;
                    player.transform.position = pos;
                    bridge.AddLog($"Dark Ruby: teleported to bench (playerSpawnPoint) at ({pos.x:F2}, {pos.y:F2})");
                }
            }

            if (!bridge.DarkRubyTimerPausedForCutscene) return;
            if (lm.levelTimer == null) return;

            bridge.AddLog($"Dark Ruby: ExitCutSceneMode (last='{bridge.LastDarkRubyCutsceneName}')");

            if (lm.boss != null && lm.boss.isDead)
            {
                bridge.AddLog("Dark Ruby: timer stays paused (boss dead)");
                return;
            }

            var introCutsceneNames = new HashSet<string>
            {
                "DarkRubyAppearCutscene",
                "DarkRubyFirstAppearCutscene",
            };

            if (!introCutsceneNames.Contains(bridge.LastDarkRubyCutsceneName))
            {
                bridge.AddLog($"Dark Ruby: timer stays paused (last='{bridge.LastDarkRubyCutsceneName}', not a boss-intro cutscene)");
                return;
            }

            lm.levelTimer.Resume();
            bridge.DarkRubyTimerPausedForCutscene = false;
            bridge.AddLog($"Dark Ruby: timer resumed after '{bridge.LastDarkRubyCutsceneName}'");
        }
    }

    [HarmonyPatch(typeof(StatusMenu), "Enter")]
    internal static class StatusMenuEnterPatch
    {
        private static void Postfix(StatusMenu __instance)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive) return;

            Transform currentMission = null;
            foreach (var t in __instance.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "CurrentMission")
                {
                    currentMission = t;
                    break;
                }
            }

            if (currentMission == null) return;

            Transform apTextTransform = currentMission.Find("AP_ObjectiveText");
            TextMeshProUGUI apText;

            if (apTextTransform == null)
            {
                var titleObj = currentMission.Find("TitleText (TMP)_CurrentMission");
                var titleTmp = titleObj?.GetComponent<TextMeshProUGUI>();
                var titleRt = titleObj?.GetComponent<RectTransform>();

                var go = new GameObject("AP_ObjectiveText");
                go.transform.SetParent(currentMission, false);
                
                var rt = go.AddComponent<RectTransform>();
                
                if (titleRt != null)
                {
                    rt.anchorMin = titleRt.anchorMin;
                    rt.anchorMax = titleRt.anchorMax;
                    rt.pivot = new Vector2(0f, 1f); 

                    float leftX = titleRt.anchoredPosition.x - (titleRt.sizeDelta.x * titleRt.pivot.x);
                    
                    rt.anchoredPosition = new Vector2(leftX + 10f, titleRt.anchoredPosition.y - 60f); 
                    rt.sizeDelta = new Vector2(400f, 300f); 
                }
                else
                {
                    rt.anchorMin = new Vector2(0, 1);
                    rt.anchorMax = new Vector2(0, 1);
                    rt.pivot = new Vector2(0f, 1f);
                    rt.anchoredPosition = new Vector2(50f, -100f);
                    rt.sizeDelta = new Vector2(400f, 300f);
                }

                apText = go.AddComponent<TextMeshProUGUI>();
                
                if (titleTmp != null)
                {
                    apText.font = titleTmp.font;
                    apText.color = new Color32(80, 50, 30, 255); 
                }
                else
                {
                    apText.color = new Color32(80, 50, 30, 255);
                }
                
                apText.fontSize = 24; 
                apText.alignment = TextAlignmentOptions.TopLeft;
                apText.enableWordWrapping = true;
                apText.richText = true;
                apText.lineSpacing = 10f; 
            }
            else
            {
                apText = apTextTransform.GetComponent<TextMeshProUGUI>();
            }

            string header = "<b><size=26>Your Current AP Tasks:</size></b>\n\n";
            apText.text = header + bridge.GetObjectiveText();
        }
    }

    [HarmonyPatch(typeof(CreateItemMenu), "Enter")]
    internal static class CreateItemMenuEnterPatch
    {
        private static void Postfix(CreateItemMenu __instance)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive) return;

            RubiniteAPPlugin.Instance.StartCoroutine(ConfigureShopSlotsNextFrame(__instance, bridge));
        }

        private static IEnumerator ConfigureShopSlotsNextFrame(CreateItemMenu menu, GameBridge bridge)
        {
            CreateTalismanSelection[] selections = null;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                yield return null;
                if (menu == null || bridge == null || !bridge.ApRunActive) yield break;
                selections = menu.GetComponentsInChildren<CreateTalismanSelection>(true);
                if (selections.Length > 0) break;
            }

            if (selections == null || selections.Length == 0)
            {
                bridge.AddLog("Shop UI filter found no CreateTalismanSelection entries under CreateItemMenu.");
                yield break;
            }

            int unlockedSlots = bridge.GetUnlockedShopSlotCount();
            int mappedEntries = 0;
            int visibleEntries = 0;
            int lockedEntries = 0;
            int checkedEntries = 0;
            var unmappedNames = new List<string>();

            GridLayoutGroup grid = null;
            int gridSelectionCount = -1;
            foreach (var candidate in menu.GetComponentsInChildren<GridLayoutGroup>(true))
            {
                int candidateCount = 0;
                foreach (var selection in selections)
                {
                    if (selection != null && selection.transform.IsChildOf(candidate.transform))
                        candidateCount++;
                }
                if (candidateCount > gridSelectionCount)
                {
                    grid = candidate;
                    gridSelectionCount = candidateCount;
                }
            }

            CreateTalismanSelection template = null;
            if (grid != null)
            {
                foreach (var selection in selections)
                {
                    if (selection != null && !selection.gameObject.name.StartsWith("AP_ExtraSlot_")
                        && selection.transform.IsChildOf(grid.transform))
                    {
                        template = selection;
                        break;
                    }
                }
            }

            if (grid != null && template != null)
            {
                var representedSlots = new HashSet<int>();
                foreach (var selection in selections)
                {
                    if (selection == null) continue;

                    var objectName = selection.gameObject.name;
                    if (objectName.StartsWith("AP_ExtraSlot_")
                        && int.TryParse(objectName.Substring("AP_ExtraSlot_".Length), out var placeholderIndex))
                    {
                        representedSlots.Add(placeholderIndex);
                    }
                    else if (selection.talismanData != null
                        && bridge.TryGetShopSlotIndex(selection.talismanData.DisplayName, out var slotIndex))
                    {
                        representedSlots.Add(slotIndex);
                    }
                }

                bool templateWasActive = template.gameObject.activeSelf;
                template.gameObject.SetActive(true);
                for (int index = 0; index < bridge.ShopSlotCount; index++)
                {
                    if (representedSlots.Contains(index))
                        continue;

                    long locationId = bridge.GetShopLocationIdForIndex(index);
                    if (locationId <= 0)
                    {
                        bridge.AddLog($"Could not create AP placeholder for shop slot {index + 1}: no location ID.");
                        continue;
                    }

                    int insertionIndex = grid.transform.childCount;
                    foreach (var existing in selections)
                    {
                        if (existing == null || existing.talismanData == null
                            || !bridge.TryGetShopSlotIndex(existing.talismanData.DisplayName, out var existingIndex)
                            || existingIndex <= index)
                            continue;

                        var existingRoot = ShopSlotVisibility.GetGridRoot(existing, grid.transform);
                        insertionIndex = existingRoot.GetSiblingIndex();
                        break;
                    }

                    var clone = UnityEngine.Object.Instantiate(template.gameObject, grid.transform);
                    clone.name = "AP_ExtraSlot_" + index;
                    clone.transform.SetSiblingIndex(insertionIndex);
                    var placeholder = clone.GetComponent<CreateTalismanSelection>();
                    if (placeholder == null)
                    {
                        UnityEngine.Object.Destroy(clone);
                        bridge.AddLog($"Could not create AP placeholder for shop slot {index + 1}: template has no selection component.");
                        continue;
                    }

                    clone.SetActive(true);
                    ConfigurePlaceholder(placeholder, index, bridge);
                    representedSlots.Add(index);
                }
                template.gameObject.SetActive(templateWasActive);
            }

            if (unlockedSlots > 30 && template != null)
            {
                bool templateWasActive = template.gameObject.activeSelf;
                template.gameObject.SetActive(true);
                for (int i = 30; i < unlockedSlots; i++)
                {
                    var slotName = "AP_ExtraSlot_" + i;
                    GameObject clone = null;
                    for (int childIndex = 0; childIndex < grid.transform.childCount; childIndex++)
                    {
                        var child = grid.transform.GetChild(childIndex);
                        if (child.name == slotName)
                        {
                            clone = child.gameObject;
                            break;
                        }
                    }

                    if (clone == null)
                    {
                        clone = UnityEngine.Object.Instantiate(template.gameObject, grid.transform);
                        clone.name = slotName;
                    }

                    clone.SetActive(true);
                    var placeholder = clone.GetComponent<CreateTalismanSelection>();
                    if (placeholder != null)
                        ConfigurePlaceholder(placeholder, i, bridge);
                }
                template.gameObject.SetActive(templateWasActive);
            }

            selections = menu.GetComponentsInChildren<CreateTalismanSelection>(true);
            foreach (var selection in selections)
            {
                if (selection == null) continue;

                if (selection.gameObject.name.StartsWith("AP_ExtraSlot_"))
                {
                    var indexText = selection.gameObject.name.Substring("AP_ExtraSlot_".Length);
                    if (int.TryParse(indexText, out var slotIndex))
                    {
                        ConfigurePlaceholder(selection, slotIndex, bridge);
                        var extraLocationId = bridge.GetShopLocationIdForIndex(slotIndex);
                        if (extraLocationId > 0) mappedEntries++;
                        if (extraLocationId > 0 && bridge.State.HasChecked(extraLocationId))
                        {
                            checkedEntries++;
                            ShopSlotVisibility.SetPurchaseAvailable(selection, false);
                            ShopSlotVisibility.SetVisible(selection, false);
                        }
                        else if (slotIndex < unlockedSlots)
                        {
                            visibleEntries++;
                            ShopSlotVisibility.SetPurchaseAvailable(selection, extraLocationId > 0);
                            ShopSlotVisibility.SetVisible(selection, true);
                        }
                        else
                        {
                            lockedEntries++;
                            ShopSlotVisibility.SetPurchaseAvailable(selection, false);
                            ShopSlotVisibility.SetVisible(selection, false);
                        }
                    }
                    continue;
                }

                if (selection.talismanData == null) continue;

                var displayName = selection.talismanData.DisplayName;
                if (bridge.TryGetScoutedShopItem(displayName, out var apItemName))
                    CreateTalismanSetupPatch.ReplaceShopDisplay(
                        selection, displayName, apItemName, bridge);

                if (bridge.TryGetShopSlotIndex(displayName, out _))
                    mappedEntries++;
                else if (unmappedNames.Count < 5)
                    unmappedNames.Add(displayName);

                var locationId = bridge.GetShopLocationIdForDisplayName(displayName);
                if (locationId > 0 && bridge.State.HasChecked(locationId))
                {
                    checkedEntries++;
                    ShopSlotVisibility.SetPurchaseAvailable(selection, false);
                    ShopSlotVisibility.SetVisible(selection, false);
                }
                else if (bridge.IsShopTalismanUnlocked(displayName))
                {
                    visibleEntries++;
                    ShopSlotVisibility.SetPurchaseAvailable(selection, locationId > 0);
                    ShopSlotVisibility.SetVisible(selection, true);
                }
                else
                {
                    lockedEntries++;
                    ShopSlotVisibility.SetPurchaseAvailable(selection, false);
                    ShopSlotVisibility.SetVisible(selection, false);
                }
            }

            bridge.RequestVisibleShopHints(unlockedSlots);
            if (visibleEntries == 0)
                CreateTalismanSetInformationPatch.ClearShopInformation(menu);

            var unmappedText = unmappedNames.Count == 0
                ? ""
                : $"; unmapped examples: {string.Join(", ", unmappedNames)}";
            bridge.AddLog($"Shop UI filter: {visibleEntries} visible, {lockedEntries} locked, " +
                $"{checkedEntries} checked hidden, {mappedEntries} mapped across " +
                $"{visibleEntries + lockedEntries + checkedEntries} entries " +
                $"(unlocked limit {unlockedSlots}){unmappedText}.");
        }

        internal static void ClearInformationIfNoVisibleSlots(CreateItemMenu menu)
        {
            if (menu == null) return;

            foreach (var selection in menu.GetComponentsInChildren<CreateTalismanSelection>(true))
                if (selection != null && selection.gameObject.activeInHierarchy)
                    return;

            CreateTalismanSetInformationPatch.ClearShopInformation(menu);
        }

        internal static bool HasVisibleUnpurchasedSlots(CreateItemMenu menu, GameBridge bridge)
        {
            if (menu == null || bridge == null || !bridge.ApRunActive)
                return false;

            var unlockedSlots = bridge.GetUnlockedShopSlotCount();
            foreach (var selection in menu.GetComponentsInChildren<CreateTalismanSelection>(true))
            {
                if (selection == null)
                    continue;

                var objectName = selection.gameObject.name;
                if (objectName.StartsWith("AP_ExtraSlot_")
                    && int.TryParse(objectName.Substring("AP_ExtraSlot_".Length), out var slotIndex))
                {
                    var locationId = bridge.GetShopLocationIdForIndex(slotIndex);
                    if (slotIndex < unlockedSlots
                        && locationId > 0
                        && !bridge.State.HasChecked(locationId))
                        return true;
                    continue;
                }

                var displayName = selection.talismanData?.DisplayName;
                if (string.IsNullOrEmpty(displayName)
                    || !bridge.IsShopTalismanUnlocked(displayName))
                    continue;

                var locationIdForTalisman = bridge.GetShopLocationIdForDisplayName(displayName);
                if (locationIdForTalisman > 0
                    && !bridge.State.HasChecked(locationIdForTalisman))
                    return true;
            }

            return false;
        }

        internal static void RefreshScoutedShopIcon(GameBridge bridge, long locationId)
        {
            if (bridge == null || locationId <= 0)
                return;

            foreach (var selection in Resources.FindObjectsOfTypeAll<CreateTalismanSelection>())
            {
                if (selection == null || !selection.gameObject.scene.IsValid())
                    continue;

                var objectName = selection.gameObject.name;
                if (objectName.StartsWith("AP_ExtraSlot_")
                    && int.TryParse(objectName.Substring("AP_ExtraSlot_".Length), out var slotIndex))
                {
                    if (bridge.GetShopLocationIdForIndex(slotIndex) == locationId)
                        ConfigurePlaceholder(selection, slotIndex, bridge);
                    continue;
                }

                var displayName = selection.talismanData?.DisplayName;
                if (string.IsNullOrEmpty(displayName)
                    || bridge.GetShopLocationIdForDisplayName(displayName) != locationId
                    || !bridge.TryGetScoutedShopItem(displayName, out var apItemName))
                    continue;

                CreateTalismanSetupPatch.ReplaceShopDisplay(
                    selection, displayName, apItemName, bridge);
            }
        }

        internal static void ConfigurePlaceholder(
            CreateTalismanSelection selection,
            int slotIndex,
            GameBridge bridge)
        {
            if (selection == null) return;

            var displayName = $"Slot {slotIndex + 1}";
            var icon = bridge?.FindSpriteByName("Pouch")
                ?? bridge?.FindSpriteByName("Bag")
                ?? bridge?.FindSpriteByName("ItemsIcon_21");
            if (bridge != null
                && bridge.TryGetScoutedShopItemForSlotIndex(slotIndex, out var slotTitle, out var slotItem))
            {
                if (!string.IsNullOrEmpty(slotItem))
                    displayName = GetShopDisplayName(slotItem);
                icon = bridge.PickIconForItem(
                    slotItem,
                    bridge.IsCrossGameShopItem(slotTitle)) ?? icon;
            }

            long locationId = bridge?.GetShopLocationIdForIndex(slotIndex) ?? 0;
            bool canPurchase = bridge != null
                && locationId > 0
                && slotIndex < bridge.GetUnlockedShopSlotCount()
                && !bridge.State.HasChecked(locationId);
            ShopSlotVisibility.SetPurchaseAvailable(selection, canPurchase);

            foreach (var tmp in selection.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
            {
                if (tmp.gameObject.name == "Name_Text (TMP)")
                    tmp.text = displayName;
                else if (tmp.gameObject.name == "Cost (TMP)")
                    tmp.text = (bridge?.GetEffectiveShopPriceForSlot(slotIndex) ?? 200).ToString();
            }

            if (icon != null)
            {
                foreach (var image in selection.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                {
                    if (image.gameObject.name != "Icon") continue;
                    image.sprite = icon;
                    break;
                }
            }
        }

        private static string GetShopDisplayName(string itemName)
        {
            if (itemName.StartsWith("Talisman: "))
                return itemName.Substring("Talisman: ".Length);

            if (itemName.StartsWith("Skill "))
            {
                var colonIndex = itemName.IndexOf(": ");
                if (colonIndex > 0)
                    return itemName.Substring(colonIndex + 2);
            }

            return itemName;
        }
    }
}

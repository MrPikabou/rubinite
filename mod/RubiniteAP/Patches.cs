using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using PugTool.Unity.Singleton;
using PugTool.Unity.StringList;

namespace RubiniteAP
{
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
        private static bool Prefix(ref string levelName)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive)
                return true;
            
            if (bridge.ShouldBlockLoad(levelName))
                return false;
            
            levelName = bridge.RemapBossScene(levelName);

            if (levelName == "ChallengeArea" && !string.IsNullOrEmpty(bridge.LastPortalLevel))
            {
                GameSetting.Instance.playerLastLeaveScene.SetValue(bridge.LastPortalLevel, "RubiniteAP");
                GameSetting.Instance.playerLastScene.SetValue(bridge.LastPortalLevel, "RubiniteAP");
                bridge.AddLog("Spawn override CA: forced playerLastLeaveScene to '" + bridge.LastPortalLevel + "'");
            }
        
            return true;
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
    }

    [HarmonyPatch(typeof(GamePlayCore), "Init")]
    internal static class GamePlayCoreInitPatch
    {
        private static void Postfix()
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge != null && bridge.ApRunActive)
                bridge.ApplyRuntime(true);
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

            var portalLevel = bridge.LastPortalLevel;
            if (string.IsNullOrEmpty(portalLevel)) return;

            // Find the correct podium and move the playerSpawnPoint BEFORE the game teleports the player.
            foreach (var point in Resources.FindObjectsOfTypeAll<ChallengeLevelPoint>())
            {
                if (point?.gameObject == null || !point.gameObject.scene.IsValid()) continue;
                if (point.levelName?.displayName == portalLevel)
                {
                    var targetPos = point.transform.position;
                    targetPos.y -= 1.2f;
                    // Keep the Z from the spawn point to not break 2D sorting
                    targetPos.z = __instance.playerSpawnPoint.position.z;
                    __instance.playerSpawnPoint.position = targetPos;
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

            var sceneName = __instance.gameObject.scene.name;
            
            if (sceneName == "ChallengeArea")
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

    [HarmonyPatch(typeof(ChangeRubySkin), "ChangeSkin")]
    internal static class ChangeRubySkinPatch
    {
        private static void Postfix()
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null) return;

            try
            {
                var itemOverview = SingletonScriptableObject<ItemDataOverview>.Instance;
                if (itemOverview != null && itemOverview.currentSkinIDFlag != null)
                {
                    string currentId = itemOverview.currentSkinIDFlag.GetValue();
                    //bridge.AddLog("DEBUG SKIN ID: '" + currentId + "'");
                }
            }
            catch (System.Exception ex)
            {
                bridge.AddLog("Skin log failed: " + ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(CreateTalismanSelection), "Create")]
    internal static class CreateTalismanSelectionPatch
    {
        private static void Postfix(CreateTalismanSelection __instance)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge != null && bridge.ApRunActive)
            {
                // Pass the display name to match with the APWorld titles
                bridge.HandleShopPurchase(__instance.talismanData.DisplayName);
                
                // Re-lock it immediately in the game data so it doesn't stay permanently unlocked
                __instance.talismanData.state.SetValue(AchievementState.Lock);
            }
        }
    }

    [HarmonyPatch(typeof(CreateTalismanSelection), "Setup")]
    internal static class CreateTalismanSetupPatch
    {
        private static void Postfix(CreateTalismanSelection __instance)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive) return;

            string displayName = __instance.talismanData.DisplayName;

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

            // Price display override
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
                ReplaceShopDisplay(__instance, displayName, apItemName);
            }
        }

        private static void ReplaceShopDisplay(CreateTalismanSelection selection, string talismanDisplayName, string apItemName)
        {
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
                var bridge = RubiniteAPPlugin.Instance?.Bridge;
                bool isCrossGame = bridge.IsCrossGameShopItem(talismanDisplayName);
                var sprite = bridge?.PickIconForItem(apItemName, isCrossGame);
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

            string displayName = __instance.talismanData?.DisplayName;
            if (string.IsNullOrEmpty(displayName)) return;

            if (displayName.Contains("Exhibition")
                || displayName.Contains("TalismanData_")
                || displayName == "X19_Shield"
                || displayName == "無"
                || displayName.Contains("展場"))
                return;

            if (!bridge.TryGetScoutedShopItem(displayName, out var apItemName)) return;
            if (!bridge.TryGetShopDescription(displayName, apItemName,
                out var title, out var description, out var icon, out var notchCount)) return;

            var menu = MenuCore.Instance.currentMenu as CreateItemMenu;
            if (menu == null) return;

            // Set RubyTextMeshProUGUI fields via reflection (see SetRubyText below)
            SetRubyText(menu, "informationTitleText", title);
            SetRubyText(menu, "informationExplainText", description ?? "");

            if (icon != null)
                menu.SetTalismanInformationIcon(icon);

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
                return;
            }

            var uneditedProp = targetType.GetProperty("uneditedText",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (uneditedProp != null && uneditedProp.CanWrite && uneditedProp.PropertyType == typeof(string))
            {
                uneditedProp.SetValue(target, value);
            }
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
        private static void Prefix(CreateTalismanSelection __instance, out int __state)
        {
            __state = -1;
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive) return;
            var td = __instance.talismanData;
            if (td == null) return;

            int effective = bridge.GetEffectiveShopPrice(td);
            if (effective == td.costDust) return;

            int original = td.costDust;
            if (!TrySetCostDust(td, effective)) return;
            __state = original;
        }

        private static void Postfix(CreateTalismanSelection __instance, int __state)
        {
            if (__state < 0) return;
            if (__instance.talismanData != null)
                TrySetCostDust(__instance.talismanData, __state);
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
            catch { }
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

            int effective = bridge.GetEffectiveShopPrice(td);
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
            if (bridge == null || !bridge.ApRunActive) return;

            var data = __instance.skillTreeData;
            if (data == null) return;

            // Only revert to vanilla if the CHECK is purchased.
            bool isPurchased = bridge.IsSkillPurchased(data.DisplayName);
            bool hasScoutedItem = bridge.TryGetScoutedSkillTreeItem(data.DisplayName, out var apItemName);

            if (isPurchased || !hasScoutedItem)
            {
                if (__instance.icon != null) __instance.icon.rectTransform.localScale = Vector3.one;
                if (__instance.fakeIcon != null) __instance.fakeIcon.rectTransform.localScale = Vector3.one;
                return;
            }

            var apIcon = bridge.PickIconForItem(apItemName);
            if (apIcon == null) return;

            Vector3 scaled = apItemName.StartsWith("Skill ") ? Vector3.one : new Vector3(0.65f, 0.65f, 1f);

            if (__instance.icon != null) 
            {
                __instance.icon.sprite = apIcon;
                __instance.icon.rectTransform.localScale = scaled;
            }
            if (__instance.fakeIcon != null) 
            {
                __instance.fakeIcon.sprite = apIcon;
                __instance.fakeIcon.rectTransform.localScale = scaled;
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
            if (skillTreeFrame == null) return;

            var data = skillTreeFrame.skillTreeData;
            if (data == null) return;

            // Only revert to vanilla description if the CHECK is purchased.
            bool isPurchased = bridge.IsSkillPurchased(data.DisplayName);
            bool isUnknown = !data.isKnown.GetValue() || !skillTreeFrame.IsKnowable();
            bool hasScoutedItem = bridge.TryGetScoutedSkillTreeItem(data.DisplayName, out var apItemName);

            if (isPurchased || isUnknown || !hasScoutedItem)
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

            var combined = "Buying this slot sends a location check to the multiworld.\n"
                        + "You will receive: " + apItemName + ".\n"
                        + (apDesc ?? "");
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

            // Cost display override
            int price = data.cost;
            if (bridge.TryGetEffectiveSkillPrice(data.DisplayName, out var p))
                price = p;

            if (price != data.cost)
            {
                __instance.costText?.SetText(price.ToString());
                __instance.costText2?.SetText(price.ToString());
            }

            // Hide the "purchasable" indicator for already-bought skills.
            if (bridge.IsSkillPurchased(data.DisplayName))
            {
                __instance.purchasableFrame?.SetActive(false);
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
            if (!GameData.SkillTitles.TryGetValue(treeId, out var title)) return true;

            if (bridge.IsSkillPurchased(treeId))
                return false;

            int price = data.cost;
            if (bridge.TryGetEffectiveSkillPrice(treeId, out var effective))
                price = effective;

            if (!bridge.HandleSkillTreePurchase(treeId, title, price))
                return true;

            // Play the unlock animation on the clicked frame as visual feedback.
            // We no longer call UpdateAllFrame() here to avoid the white flicker 
            // across all icons. The Update patch will handle children dynamically.
            try
            {
                __instance.purchasableFrame?.SetActive(false);
                __instance.animatorController?.ChangeAnimation(
                    SkillTreeFrame.SkillTreeFrameAnimationType.LockToUnlock);
            }
            catch { }

            // Immediately update the clicked frame's visuals so the icon
            // reverts from the AP item icon back to the vanilla skill icon.
            try { __instance.Init(); }
            catch { }

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

            if (received && !purchased)
            {
                data.skillTreeDataDetail?.isUnlocked?.SetValue(false);
                data.secondRunSkillTreeDataDetail?.isUnlocked?.SetValue(false);
            }
        }

        private static void Postfix(SkillTreeFrame __instance)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge == null || !bridge.ApRunActive) return;
            var data = __instance.skillTreeData;
            if (data == null) return;

            bool isPurchased = bridge.IsSkillPurchased(data.DisplayName);
            bool isReceived = bridge.IsSkillReceived(data.DisplayName);

            // 1. Icon: revert to vanilla ONLY if purchased.
            if (isPurchased)
            {
                var vanillaIcon = __instance.skillData?.Icon;
                if (vanillaIcon != null)
                {
                    if (__instance.icon != null && __instance.icon.sprite != vanillaIcon)
                    {
                        __instance.icon.sprite = vanillaIcon;
                        __instance.icon.rectTransform.localScale = Vector3.one;
                    }
                    if (__instance.fakeIcon != null && __instance.fakeIcon.sprite != vanillaIcon)
                    {
                        __instance.fakeIcon.sprite = vanillaIcon;
                        __instance.fakeIcon.rectTransform.localScale = Vector3.one;
                    }
                }
            }
            else if (bridge.TryGetScoutedSkillTreeItem(data.DisplayName, out var apItemName))
            {
                var apIcon = bridge.PickIconForItem(apItemName);
                if (apIcon != null)
                {
                    Vector3 scaled = apItemName.StartsWith("Skill ") ? Vector3.one : new Vector3(0.65f, 0.65f, 1f);
                    
                    if (__instance.icon != null && __instance.icon.sprite != apIcon)
                    {
                        __instance.icon.sprite = apIcon;
                        __instance.icon.rectTransform.localScale = scaled;
                    }
                    if (__instance.fakeIcon != null && __instance.fakeIcon.sprite != apIcon)
                    {
                        __instance.fakeIcon.sprite = apIcon;
                        __instance.fakeIcon.rectTransform.localScale = scaled;
                    }
                }
            }

            // 2. Animator state
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
                bool unlockable = true;
                for (int i = 0; i < __instance.needUnlocked.Length; i++)
                {
                    var parentData = __instance.needUnlocked[i].skillTreeData;
                    if (parentData == null) continue;

                    bool parentVanillaUnlocked = parentData.isUnlocked != null && parentData.isUnlocked.GetValue();
                    bool parentPurchased = bridge.IsSkillPurchased(parentData.DisplayName);
                    bool parentReceived = bridge.IsSkillReceived(parentData.DisplayName);

                    if (!parentVanillaUnlocked && !parentPurchased && !parentReceived)
                    {
                        unlockable = false;
                        break;
                    }
                }

                if (unlockable)
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

            for (int i = 0; i < __instance.needUnlocked.Length; i++)
            {
                var parent = __instance.needUnlocked[i];
                if (parent == null) continue;
                var parentData = parent.skillTreeData;
                if (parentData == null) continue;

                bool parentVanillaUnlocked = parentData.isUnlocked != null && parentData.isUnlocked.GetValue();
                bool parentPurchased = bridge.IsSkillPurchased(parentData.DisplayName);
                bool parentReceived = bridge.IsSkillReceived(parentData.DisplayName);

                if (!parentVanillaUnlocked && !parentPurchased && !parentReceived)
                {
                    __result = false;
                    return false;
                }
            }

            __result = true;
            return false;
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
}

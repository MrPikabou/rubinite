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
        private static void Postfix(ChallengeLevelPoint __instance)
        {
            var bridge = RubiniteAPPlugin.Instance?.Bridge;
            if (bridge != null && bridge.ApRunActive)
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
}

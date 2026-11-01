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
}

using HarmonyLib;
using UnityEngine;
using PugTool.Unity.Singleton;
using PugTool.Unity.StringList;

namespace RubiniteDarkRuby.Patches
{
    [HarmonyPatch(typeof(LevelManager), "SetUp")]
    internal static class LevelManagerSetUpPatch
    {
        private static void Prefix(LevelManager __instance)
        {
            if (ApBridgeHook.ShouldSuppressStandalone) return;

            var module = RubiniteDarkRubyPlugin.Instance?.Module;
            if (module == null || __instance == null) return;
            if (__instance.gameObject.scene.name != "ChallengeArea") return;
            if (__instance.playerSpawnPoint == null) return;

            var podium = module.EnsurePodiumSpawned();
            if (podium == null) return;

            var targetPos = podium.transform.position;
            targetPos.y -= 1.2f;
            targetPos.z = __instance.playerSpawnPoint.position.z;
            __instance.playerSpawnPoint.position = targetPos;
            RubiniteDarkRubyPlugin.Log?.LogInfo(
                "CA entry: moved playerSpawnPoint to DR podium (" + targetPos.x.ToString("F2")
                + "," + targetPos.y.ToString("F2") + ")");
            
            var overview = SingletonScriptableObject<ItemDataOverview>.Instance;
            if (overview == null) return;
            
            var flag = overview.currentSkinIDFlag.GetValue();
            if (!string.IsNullOrEmpty(flag) && flag != DarkRubySkinEntry.SkinId)
            {
                var item = overview.FindUniqueId(flag);
                if (item?.skinMaterial != null)
                {
                    var player = GamePlayCore.Instance?.player;
                    if (player != null)
                    {
                        foreach (var sr in player.GetComponentsInChildren<SpriteRenderer>(true))
                        {
                            if (sr.name != "Girl") continue;
                            if (sr.material != null && sr.material.name == item.skinMaterial.name) continue;
                            sr.material = item.skinMaterial;
                        }
                    }
                }
            }
        }

        private static void Postfix(LevelManager __instance)
        {
            if (ApBridgeHook.ShouldSuppressStandalone) return;
            
            var module = RubiniteDarkRubyPlugin.Instance?.Module;
            if (module == null || __instance == null) return;
            var scene = __instance.gameObject.scene.name;

            if (scene == "DarkRubyLevel")
            {
                module.OnDarkRubyLevelLoaded(__instance);
                return;
            }

            if (scene == "ChallengeArea")
            {
                module.OnChallengeAreaReturned();

                var portal = module.LastPortalLevel;
                if (!string.IsNullOrEmpty(portal))
                {
                    GameSetting.Instance.playerLastScene.SetValue(portal, "RubiniteDarkRuby");
                    GameSetting.Instance.playerLastLeaveScene.SetValue(portal, "RubiniteDarkRuby");
                    RubiniteDarkRubyPlugin.Instance.StartCoroutine(module.TeleportPlayerToDarkRubyPodium());
                    module.ClearLastPortalLevel();
                }
            }
        }
    }
}

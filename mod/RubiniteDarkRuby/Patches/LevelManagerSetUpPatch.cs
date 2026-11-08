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

    [HarmonyPatch(typeof(ApplicationCore), "LoadLevel")]
    internal static class LoadLevelIllusionPatch
    {
        private static void Prefix(ref string levelName, ref bool saveData)
        {
            var module = RubiniteDarkRubyPlugin.Instance?.Module;
            if (module == null || !module.IsIllusionRequested) return;

            if (levelName != "DarkRubyLevel")
            {
                module.IsIllusionRequested = false;
                RubiniteDarkRubyPlugin.Log?.LogInfo(
                    "DR: Illusion request cleared (non-DR level '" + levelName + "')");
                return;
            }

            saveData = false;
            module.IsIllusionRun = true;
            module.IsIllusionRequested = false;
            module.SnapshotWinCount();
            RubiniteDarkRubyPlugin.Log?.LogInfo(
                "DR: Loading DarkRubyLevel as Illusion (saveData=false)");
        }
    }

    [HarmonyPatch(typeof(BasicEnemyCore), "Start")]
    internal static class DarkRubyCoreIllusionPatch
    {
        private static void Prefix(BasicEnemyCore __instance)
        {
            if (!(__instance is DarkRubyCore)) return;
            var module = RubiniteDarkRubyPlugin.Instance?.Module;
            if (module == null || !module.IsIllusionRun) return;

            var attr = __instance.enemyAttr;
            if (attr == null) return;

            float playerMaxHP = GamePlayCore.Instance?.player?.Attr?.maxHP ?? 100f;
            float playerHit = GamePlayCore.Instance?.player?.Attr?.atk5_OfCriticalStrike ?? 40f;

            float bossHP = playerMaxHP;

            attr.maxHP = (int)bossHP;
            //attr.maxHP = 1; -> Vanilla HP
            attr.hpBarMaxValue = new[] { bossHP };
            RubiniteDarkRubyPlugin.Log?.LogInfo(
                "DR Illusion: boss maxHP=" + bossHP
                + " (playerMaxHP=" + playerMaxHP + ", playerHit=" + playerHit + ")");
        }

        private static void Postfix(BasicEnemyCore __instance)
        {
            if (!(__instance is DarkRubyCore)) return;
            var module = RubiniteDarkRubyPlugin.Instance?.Module;
            if (module == null || !module.IsIllusionRun) return;

            __instance.isSecondRun = false;

            var lm = GamePlayCore.Instance?.levelManager;
            if (lm?.bossUI?.textMesh != null)
            {
                lm.bossUI.textMesh.uneditedText = "Dark Ruby";
                RubiniteDarkRubyPlugin.Log?.LogInfo(
                    "DR Illusion: boss name re-asserted to 'Dark Ruby'");
            }
        }
    }

    [HarmonyPatch(typeof(LevelManager), "SetUp")]
    internal static class LevelManagerIllusionPatch
    {
        private static void Postfix(LevelManager __instance)
        {
            if (__instance == null) return;
            if (__instance.gameObject.scene.name != "DarkRubyLevel") return;
            var module = RubiniteDarkRubyPlugin.Instance?.Module;
            if (module == null || !module.IsIllusionRun) return;

            try
            {
                if (__instance.bossUI != null)
                {
                    __instance.bossUI.gameObject.SetActive(false);
                    RubiniteDarkRubyPlugin.Log?.LogInfo("DR Illusion: BossUI hidden until cutscene");
                }

                if (__instance.playerUI != null)
                {
                    __instance.playerUI.gameObject.SetActive(true);
                    __instance.playerUI.SetHide(true);
                    __instance.playerUI.SetAlpha(0f);
                    RubiniteDarkRubyPlugin.Log?.LogInfo("DR Illusion: PlayerUI hidden until cutscene");
                }

                var player = GamePlayCore.Instance?.player;
                if (player != null)
                {
                    if (GamePlayCore.Instance.HPBottleCountMax != null)
                        GamePlayCore.Instance.HPBottleCountMax.SetValue(3);

                    player.skipDeath = false;
                    player.HP = player.Attr != null ? player.Attr.maxHP : 100;
                    player.HPBottleCount = 3;
                    RubiniteDarkRubyPlugin.Log?.LogInfo(
                        "DR Illusion: player HP=" + player.HP
                        + " bottles=3 skipDeath=false");
                }
            }
            catch (System.Exception ex)
            {
                RubiniteDarkRubyPlugin.Log?.LogWarning(
                    "DR Illusion setup failed: " + ex.Message);
            }
        }
    }
}

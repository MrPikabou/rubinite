using HarmonyLib;

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

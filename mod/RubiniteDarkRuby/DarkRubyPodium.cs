using PugTool.Unity.Singleton;
using RubiniteDarkRuby.Resources;
using UnityEngine;

namespace RubiniteDarkRuby
{
    /// <summary>
    /// Spawns the Dark Ruby podium in the Challenge Area by duplicating the
    /// Beast podium as a template. Standalone-safe — no Archipelago references.
    /// </summary>
    internal static class DarkRubyPodium
    {
        private static Sprite _icon;
        private static Sprite _scenePic;

        public static GameObject TrySpawn(Settings settings)
        {
            if (!settings.EnableMod.Value) return null;
            if (!settings.SpawnPortalInChallengeArea.Value) return null;

            var overview = SingletonScriptableObject<LevelDataOverview>.Instance;
            if (overview == null) return null;

            var darkRubyData = FindLevelData(overview, "DarkRubyLevel");
            if (darkRubyData == null)
            {
                RubiniteDarkRubyPlugin.Log?.LogWarning(
                    "DarkRubyLevel LevelData not found — cannot spawn podium.");
                return null;
            }

            LoadSpritesOnce();
            ApplyIconAndScene(overview, darkRubyData);

            var darkRubyLevelName = overview.allSceneName?.DarkRuby;
            if (darkRubyLevelName == null)
            {
                RubiniteDarkRubyPlugin.Log?.LogWarning(
                    "allSceneName.DarkRuby missing — cannot spawn podium.");
                return null;
            }

            var template = FindPodiumByLevel("BeastLevel");
            if (template == null)
            {
                RubiniteDarkRubyPlugin.Log?.LogWarning(
                    "BeastLevel template podium not found — cannot spawn podium.");
                return null;
            }

            var newObj = Object.Instantiate(template.gameObject);
            newObj.name = "RubiniteDR_Podium";

            var (offX, offY) = settings.ResolvePortalOffset();
            newObj.transform.position = template.transform.position
                + new Vector3(offX, offY, 0f);

            var newPoint = newObj.GetComponent<ChallengeLevelPoint>();
            if (newPoint != null)
            {
                newPoint.levelName = darkRubyLevelName;
                if (newPoint.onEnableLoadLevel != null)
                    newPoint.onEnableLoadLevel.levelName = darkRubyLevelName;
                if (newPoint.lastSceneCondition != null)
                    newPoint.lastSceneCondition.levelName = darkRubyLevelName;

                newPoint.levelData = darkRubyData;

                if (newPoint.bossStageImage != null)
                {
                    if (darkRubyData.differentImageForChallengeArea
                        && darkRubyData.ScenePic_ForChallengeArea != null)
                        newPoint.bossStageImage.sprite = darkRubyData.ScenePic_ForChallengeArea;
                    else if (darkRubyData.MapSelect_ScenePic != null)
                        newPoint.bossStageImage.sprite = darkRubyData.MapSelect_ScenePic;
                }

                if (newPoint.bossIcon != null)
                {
                    var icon = darkRubyData.bossIcon_ForChallengeArea ?? darkRubyData.bossIcon;
                    if (icon != null) newPoint.bossIcon.sprite = icon;
                }

                if (newPoint.dustText != null)
                    newPoint.dustText.text =
                        (darkRubyData.dustFinalStage + darkRubyData.dustFirstStage).ToString();
            }

            RubiniteDarkRubyPlugin.Log?.LogInfo(
                "Podium spawned at " + newObj.transform.position
                + " (preset=" + settings.PortalPositionPreset.Value
                + ", offset=" + offX + "," + offY + ")");
            return newObj;
        }

        private static void LoadSpritesOnce()
        {
            if (_icon == null)
            {
                _icon = DarkRubyResources.LoadSprite("dark_ruby_icon.png", 0.7f);
                RubiniteDarkRubyPlugin.Log?.LogInfo(_icon != null
                    ? "Loaded dark_ruby_icon.png"
                    : "dark_ruby_icon.png missing — falling back to Beast icon");
            }
            if (_scenePic == null)
            {
                _scenePic = DarkRubyResources.LoadSprite("dark_ruby_scene.png");
                RubiniteDarkRubyPlugin.Log?.LogInfo(_scenePic != null
                    ? "Loaded dark_ruby_scene.png"
                    : "dark_ruby_scene.png missing — falling back to Beast scene pic");
            }
        }

        private static void ApplyIconAndScene(LevelDataOverview overview, LevelData darkRubyData)
        {
            var beastData = FindLevelData(overview, "BeastLevel");

            if (_icon != null)
            {
                darkRubyData.bossIcon = _icon;
                darkRubyData.bossIcon_ForChallengeArea = _icon;
            }
            else if (beastData?.bossIcon != null)
            {
                darkRubyData.bossIcon = beastData.bossIcon;
                darkRubyData.bossIcon_ForChallengeArea =
                    beastData.bossIcon_ForChallengeArea ?? beastData.bossIcon;
            }

            if (_scenePic != null)
            {
                darkRubyData.MapSelect_ScenePic = _scenePic;
                darkRubyData.differentImageForChallengeArea = true;
                darkRubyData.ScenePic_ForChallengeArea = _scenePic;
            }
            else if (beastData?.MapSelect_ScenePic != null)
            {
                darkRubyData.MapSelect_ScenePic = beastData.MapSelect_ScenePic;
            }
        }

        private static LevelData FindLevelData(LevelDataOverview overview, string id)
        {
            if (overview == null) return null;
            var data = overview.FindUniqueId(id);
            if (data != null) return data;
            foreach (var ld in overview.GetAllData())
                if (ld != null && ld.DisplayName == id) return ld;
            return null;
        }

        private static ChallengeLevelPoint FindPodiumByLevel(string levelName)
        {
            foreach (var p in UnityEngine.Resources.FindObjectsOfTypeAll<ChallengeLevelPoint>())
            {
                if (p?.gameObject == null) continue;
                if (!p.gameObject.scene.IsValid()) continue;
                if (p.levelName?.displayName == levelName) return p;
            }
            return null;
        }
    }
}

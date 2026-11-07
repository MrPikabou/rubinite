using Game.Level;
using HarmonyLib;
using PugTool.Unity.Singleton;
using UnityEngine;
using System.Collections.Generic;

namespace RubiniteDarkRuby.Patches
{
    internal static class LeaderboardMenuDRHelper
    {
        public static bool IsDarkRubyMode(LeaderboardMenu menu)
            => menu?.levelData?.DisplayName == "DarkRubyLevel";

        public static void PrepareForDarkRuby(LeaderboardMenu menu)
        {
            if (menu == null) return;
            if (menu.button_Friends != null) menu.button_Friends.gameObject.SetActive(false);
            if (menu.button_MyBest != null) menu.button_MyBest.gameObject.SetActive(false);
            if (menu.TagButtons != null)
                foreach (var b in menu.TagButtons)
                    if (b != null) b.gameObject.SetActive(false);

            if (menu.button_WorldRank != null)
            {
                menu.button_WorldRank.gameObject.SetActive(true);
                var label = menu.button_WorldRank.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (label != null) label.text = "Best";
                menu.button_WorldRank.onClick.RemoveAllListeners();
                menu.button_WorldRank.onClick.AddListener(() => RefreshFrames(menu));
            }

            if (menu.titlelocalize != null)
            {
                try { menu.titlelocalize.Term = "Dark Lake of Remembrance"; }
                catch (System.Exception ex)
                {
                    RubiniteDarkRubyPlugin.Log?.LogWarning(
                        "Could not set leaderboard title: " + ex.Message);
                }
            }

            RefreshFrames(menu);

            if (menu.button_WorldRank != null)
            {
                try { MenuCore.Instance.SetSelect(menu.button_WorldRank); } catch { }
            }
        }

        public static void RefreshFrames(LeaderboardMenu menu)
        {
            var state = RubiniteDarkRubyPlugin.Instance?.State;
            if (state == null || menu == null) return;

            float[] times = {
                state.BestTimeSeconds,
                state.SecondBestTimeSeconds,
                state.ThirdBestTimeSeconds,
            };
            var idsList = new List<int>[] {
                state.BestTalismanIds,
                state.SecondBestTalismanIds,
                state.ThirdBestTalismanIds,
            };

            for (int i = 0; i < menu.rankDataFrames.Count; i++)
            {
                var frame = menu.rankDataFrames[i];
                if (frame == null) continue;

                if (i < times.Length && times[i] >= 0f)
                {
                    var ids = idsList[i] ?? new List<int> { 0, 0, 0, 0, 0, 0 };
                    frame.SetFrame(new RankData(times[i], 0, i + 1, "You", ids), i == 0);
                }
                else
                {
                    frame.SetNoData();
                }
            }

            if (times[0] >= 0f)
            {
                if (menu.bestRecord_TimeText != null)
                    menu.bestRecord_TimeText.text = LevelTimer.GetTimeString(times[0]);
                if (menu.bestRecord_HitCountText != null)
                    menu.bestRecord_HitCountText.text = "0";
                try { menu.levelGrading?.SetGrading(times[0], menu.levelData); } catch { }

                if (menu.talismanIconDisplay != null)
                {
                    var bestIds = state.BestTalismanIds ?? new List<int> { 0, 0, 0, 0, 0, 0 };
                    var bestRankData = new RankData(times[0], 0, 1, "You", bestIds);
                    try { menu.talismanIconDisplay.UpdateDisplay(bestRankData); } catch { }
                }
            }
            else
            {
                try { menu.ClearBestRecord(); } catch { }
            }

            if (menu.scrollRect != null) menu.scrollRect.vertical = false;
            if (menu.scrollBarParentObject != null) menu.scrollBarParentObject.SetActive(false);
        }
    }

    [HarmonyPatch(typeof(LeaderboardMenu), "ShowLeaderboard")]
    internal static class LeaderboardMenuShowLeaderboardPatch
    {
        private static bool Prefix(LeaderboardMenu __instance, ref LevelData levelData)
        {
            var settings = RubiniteDarkRubyPlugin.Instance?.Settings;
            if (settings?.EnableMod?.Value != true) return true;
            if (levelData == null) return true;
            if (levelData.DisplayName != "DarkRubyLevel") return true;

            __instance.levelData = levelData;
            try { __instance.SetLevelTitleData(); } catch { }

            var mask = MenuCore.Instance?.transitionsMask;
            if (mask != null)
            {
                mask.SetTransition(() =>
                {
                    MenuCore.Instance.EnterMenu(__instance);
                    __instance.gameObject.SetActive(true);
                });
            }
            else
            {
                MenuCore.Instance?.EnterMenu(__instance);
                __instance.gameObject.SetActive(true);
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(LeaderboardMenu), "Enter")]
    internal static class LeaderboardMenuEnterPatch
    {
        private static bool Prefix(LeaderboardMenu __instance)
        {
            if (!LeaderboardMenuDRHelper.IsDarkRubyMode(__instance)) return true;
            __instance.gameObject.SetActive(true);
            LeaderboardMenuDRHelper.PrepareForDarkRuby(__instance);
            RubiniteDarkRubyPlugin.Log?.LogInfo("DR leaderboard opened (offline)");
            return false;
        }
    }

    [HarmonyPatch(typeof(LeaderboardMenu), "Process")]
    internal static class LeaderboardMenuProcessPatch
    {
        private static bool Prefix(LeaderboardMenu __instance)
        {
            if (!LeaderboardMenuDRHelper.IsDarkRubyMode(__instance)) return true;

            try
            {
                var app = SingletonPrefab<ApplicationCore>.Instance;
                if (app?.controller != null && app.controller.BackButtonDown())
                {
                    var mask = MenuCore.Instance?.transitionsMask;
                    if (mask != null)
                        mask.SetTransition(() => __instance.CloseMenu());
                    else
                        __instance.CloseMenu();
                }
            }
            catch { }
            return false;
        }
    }

    [HarmonyPatch(typeof(LeaderboardMenu), "Update")]
    internal static class LeaderboardMenuUpdatePatch
    {
        private static bool Prefix(LeaderboardMenu __instance)
        {
            if (!LeaderboardMenuDRHelper.IsDarkRubyMode(__instance)) return true;
            return false;
        }
    }
}

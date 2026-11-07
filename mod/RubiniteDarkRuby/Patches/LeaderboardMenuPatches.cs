using Game.Level;
using HarmonyLib;
using PugTool.Unity.Singleton;
using PugTool.Unity.StringList;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace RubiniteDarkRuby.Patches
{
    internal static class LeaderboardMenuDRHelper
    {
        private static UnityEngine.UI.Button _resetButton;
        private static TMPro.TextMeshProUGUI _resetLabel;
        private static TMPro.TextMeshProUGUI _statsLabel;
        private static float _resetClickedAt = -1f;

        private static string _originalWorldRankText;
        private static string _originalHeaderTimeText;
        private static bool _defaultsCaptured;
        private static LeaderboardMenu _attachedTo;

        public static bool IsDarkRubyMode(LeaderboardMenu menu)
            => menu?.levelData?.DisplayName == "DarkRubyLevel";

        // ---------- Default capture ----------

        private static void CaptureDefaults(LeaderboardMenu menu)
        {
            if (_defaultsCaptured || menu == null) return;
            try
            {
                var wrLabel = menu.button_WorldRank?.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (wrLabel != null) _originalWorldRankText = wrLabel.text;

                var header = FindHeaderTimeTmp(menu);
                if (header != null) _originalHeaderTimeText = header.text;

                _defaultsCaptured = true;
            }
            catch { }
        }

        private static TMPro.TMP_Text FindHeaderTimeTmp(LeaderboardMenu menu)
        {
            try
            {
                foreach (var tmp in menu.GetComponentsInChildren<TMPro.TMP_Text>(true))
                {
                    if (tmp == null) continue;
                    var t = tmp.text;
                    if (t == "Time" || t == "Time · Rank") return tmp;
                }
            }
            catch { }
            return null;
        }

        // ---------- Vanilla restoration ----------

        public static void RestoreVanillaDefaults(LeaderboardMenu menu)
        {
            if (menu == null) return;
            try
            {
                // Re-show tabs
                if (menu.button_Friends != null) menu.button_Friends.gameObject.SetActive(true);
                if (menu.button_MyBest != null) menu.button_MyBest.gameObject.SetActive(true);
                if (menu.button_WorldRank != null) menu.button_WorldRank.gameObject.SetActive(true);
                if (menu.TagButtons != null)
                    foreach (var b in menu.TagButtons)
                        if (b != null) b.gameObject.SetActive(true);

                // Restore tab label
                if (!string.IsNullOrEmpty(_originalWorldRankText))
                {
                    var wrLabel = menu.button_WorldRank?.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                    if (wrLabel != null) wrLabel.text = _originalWorldRankText;
                }

                // Restore "Time" header
                var header = FindHeaderTimeTmp(menu);
                if (header != null) header.text = "Time";

                // Hide custom elements
                if (_resetButton != null) _resetButton.gameObject.SetActive(false);
                if (_statsLabel != null) _statsLabel.gameObject.SetActive(false);

                // Restore scroll
                if (menu.scrollRect != null) menu.scrollRect.vertical = true;
                if (menu.scrollBarParentObject != null) menu.scrollBarParentObject.SetActive(true);
            }
            catch (System.Exception ex)
            {
                RubiniteDarkRubyPlugin.Log?.LogWarning("RestoreVanillaDefaults: " + ex.Message);
            }
        }

        // ---------- DR preparation ----------

        public static void PrepareForDarkRuby(LeaderboardMenu menu)
        {
            if (menu == null) return;
            CaptureDefaults(menu);

            // Hide Friends + MyBest tabs
            if (menu.button_Friends != null) menu.button_Friends.gameObject.SetActive(false);
            if (menu.button_MyBest != null) menu.button_MyBest.gameObject.SetActive(false);
            if (menu.TagButtons != null)
                foreach (var b in menu.TagButtons)
                    if (b != null) b.gameObject.SetActive(false);

            // Level title via TMP (I2Localization term approach fails)
            if (menu.titlelocalize != null)
            {
                try
                {
                    menu.titlelocalize.Term = "";
                    var tmp = menu.titlelocalize.GetComponent<TMPro.TMP_Text>()
                        ?? menu.titlelocalize.GetComponentInChildren<TMPro.TMP_Text>();
                    if (tmp != null) tmp.text = "Dark Lake of Remembrance";
                }
                catch (System.Exception ex)
                {
                    RubiniteDarkRubyPlugin.Log?.LogWarning(
                        "Could not set leaderboard title: " + ex.Message);
                }
            }

            // Rename WorldRank to "Best" — DON'T touch onClick (UpdateLeaderboard patch handles DR)
            if (menu.button_WorldRank != null)
            {
                menu.button_WorldRank.gameObject.SetActive(true);
                var label = menu.button_WorldRank.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (label != null) label.text = "Personal Bests";
            }

            EnsureResetButton(menu);
            RefreshFrames(menu);

            if (menu.button_WorldRank != null)
            {
                try { MenuCore.Instance.SetSelect(menu.button_WorldRank); } catch { }
            }
        }

        // ---------- Frame population ----------

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

            string[] labels = { "S", "A", "B", "C" };
            Color[] colors = {
                new Color(1.00f, 0.675f, 0.059f, 1f),
                new Color(0.75f, 0.90f, 1.00f, 1f),
                new Color(0.90f, 0.70f, 0.50f, 1f),
                new Color(0.80f, 0.80f, 0.80f, 1f),
                //new Color(0.820f, 0.192f, 0.153f, 1f),
            };

            for (int i = 0; i < menu.rankDataFrames.Count; i++)
            {
                var frame = menu.rankDataFrames[i];
                if (frame == null) continue;

                if (i < times.Length && times[i] >= 0f)
                {
                    var ids = idsList[i] ?? new List<int> { 0, 0, 0, 0, 0, 0 };
                    frame.SetFrame(new RankData(times[i], 0, i + 1, "You", ids), i == 0);

                    // Rank appended to NAME column, not TIME column → no stacking
                    int rank = CalculateRankIndex(times[i]);
                    if (rank >= 0 && rank < labels.Length && frame.playerNameText != null)
                    {
                        string hex = ColorUtility.ToHtmlStringRGB(colors[rank]);
                        frame.playerNameText.richText = true;
                        frame.playerNameText.text = "You  <color=#" + hex + "><b>["
                            + labels[rank] + "]</b></color>";
                    }
                    if (frame.timeText != null)
                        frame.timeText.text = LevelTimer.GetTimeString(times[i]);
                }
                else
                {
                    frame.SetNoData();
                }
            }

            // Personal Best panel
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
                    try { menu.talismanIconDisplay.UpdateDisplay(new RankData(times[0], 0, 1, "You", bestIds)); } catch { }
                }
            }
            else
            {
                try { menu.ClearBestRecord(); } catch { }
            }

            if (menu.scrollRect != null) menu.scrollRect.vertical = false;
            if (menu.scrollBarParentObject != null) menu.scrollBarParentObject.SetActive(false);
        }

        private static int CalculateRankIndex(float time)
        {
            var overview = SingletonScriptableObject<LevelDataOverview>.Instance;
            if (overview == null) return -1;

            LevelData data = overview.FindUniqueId("DarkRubyLevel");
            if (data == null)
                foreach (var ld in overview.GetAllData())
                    if (ld != null && ld.DisplayName == "DarkRubyLevel") { data = ld; break; }
            if (data?.gradingScale == null || data.gradingScale.Length == 0) return -1;

            float threshold = data.gradingTime;
            for (int i = 0; i < data.gradingScale.Length; i++)
            {
                threshold += data.gradingScale[i];
                if (time <= threshold) return i;
            }
            return data.gradingScale.Length;
        }

        // ---------- Reset button + stats (parented to menu root, visible) ----------

        public static void EnsureResetButton(LeaderboardMenu menu)
        {
            if (menu == null) return;

            if (_attachedTo != menu && (_resetButton != null || _statsLabel != null))
            {
                if (_resetButton != null && _resetButton.gameObject != null)
                    Object.Destroy(_resetButton.gameObject);
                if (_statsLabel != null && _statsLabel.gameObject != null)
                    Object.Destroy(_statsLabel.gameObject);
                _resetButton = null;
                _resetLabel = null;
                _statsLabel = null;
                _attachedTo = menu;
            }

            if (_resetButton == null)
            {
                try
                {
                    var font = menu.bestRecord_TimeText?.font;

                    // === Stats label ===
                    var statsGo = new GameObject("DR_StatsLabel");
                    statsGo.transform.SetParent(menu.transform, false);
                    var statsRt = statsGo.AddComponent<RectTransform>();
                    statsRt.anchorMin = new Vector2(0, 0);
                    statsRt.anchorMax = new Vector2(0, 0);
                    statsRt.pivot = new Vector2(0, 0);
                    statsRt.sizeDelta = new Vector2(400, 55);
                    statsRt.anchoredPosition = new Vector2(200, 120);

                    var statsTmp = statsGo.AddComponent<TMPro.TextMeshProUGUI>();
                    if (font != null) statsTmp.font = font;
                    statsTmp.fontSize = 26;
                    statsTmp.fontStyle = TMPro.FontStyles.Bold;
                    statsTmp.alignment = TMPro.TextAlignmentOptions.Left;
                    statsTmp.color = new Color(0.35f, 0.22f, 0.15f);
                    statsTmp.richText = true;
                    _statsLabel = statsTmp;

                    // === Reset button ===
                    var go = new GameObject("DR_ResetButton");
                    go.transform.SetParent(menu.transform, false);
                    var rt = go.AddComponent<RectTransform>();
                    rt.anchorMin = new Vector2(1, 0);
                    rt.anchorMax = new Vector2(1, 0);
                    rt.pivot = new Vector2(1, 0);
                    rt.sizeDelta = new Vector2(260, 55);
                    rt.anchoredPosition = new Vector2(-955, 115);

                    var bg = go.AddComponent<UnityEngine.UI.Image>();
                    Sprite tabSprite = null;
                    Image.Type tabType = Image.Type.Simple;
                    try
                    {
                        var tabImg = menu.button_WorldRank?.GetComponentInChildren<Image>();
                        if (tabImg != null && tabImg.sprite != null)
                        {
                            tabSprite = tabImg.sprite;
                            tabType = tabImg.type;
                        }
                    }
                    catch { }

                    if (tabSprite != null)
                    {
                        bg.sprite = tabSprite;
                        bg.type = tabType;
                    }

                    bg.color = new Color(0.9f, 0.18f, 0.16f, 1f);

                    var btn = go.AddComponent<UnityEngine.UI.Button>();
                    btn.targetGraphic = bg;

                    var nav = btn.navigation;
                    nav.mode = UnityEngine.UI.Navigation.Mode.None;
                    btn.navigation = nav;

                    var colors = btn.colors;
                    colors.normalColor      = Color.white;
                    colors.highlightedColor = new Color(2.00f, 2.00f, 2.00f, 1f);
                    colors.pressedColor     = new Color(0.8f, 0.8f, 0.8f, 1f);
                    colors.selectedColor    = Color.white;
                    colors.disabledColor    = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                    colors.colorMultiplier  = 1f;
                    colors.fadeDuration     = 0.08f;
                    btn.colors = colors;

                    var labelGo = new GameObject("Label");
                    labelGo.transform.SetParent(go.transform, false);
                    var labelRt = labelGo.AddComponent<RectTransform>();
                    labelRt.anchorMin = Vector2.zero;
                    labelRt.anchorMax = Vector2.one;
                    labelRt.offsetMin = new Vector2(10, 4);
                    labelRt.offsetMax = new Vector2(-10, -4);

                    var label = labelGo.AddComponent<TMPro.TextMeshProUGUI>();
                    if (font != null) label.font = font;
                    label.text = "Reset Times";
                    label.fontSize = 24;
                    label.fontStyle = TMPro.FontStyles.Bold;
                    label.alignment = TMPro.TextAlignmentOptions.Center;
                    label.color = Color.white;

                    _resetLabel = label;
                    _resetButton = btn;
                    btn.onClick.AddListener(() => OnResetClicked());
                    _attachedTo = menu;
                }
                catch (System.Exception ex)
                {
                    RubiniteDarkRubyPlugin.Log?.LogWarning(
                        "Could not create reset UI: " + ex.Message);
                    return;
                }
            }

            if (_resetButton != null) _resetButton.gameObject.SetActive(true);
            if (_statsLabel != null) _statsLabel.gameObject.SetActive(true);
            RefreshStatsText();
        }

        private static Sprite FindSpriteByName(string name)
        {
            try
            {
                foreach (var s in UnityEngine.Resources.FindObjectsOfTypeAll<Sprite>())
                    if (s != null && s.name == name) return s;
            }
            catch { }
            return null;
        }

        private static void RefreshStatsText()
        {
            if (_statsLabel == null) return;
            int clears = RubiniteDarkRubyPlugin.Instance?.State?.TotalClears ?? 0;
            _statsLabel.text = "Total Clears: <b>" + clears + "</b>";
        }

        private static void OnResetClicked()
        {
            float now = Time.unscaledTime;
            bool isConfirming = _resetClickedAt > 0 && (now - _resetClickedAt) < 3f;

            if (!isConfirming)
            {
                _resetClickedAt = now;
                if (_resetLabel != null) _resetLabel.text = "Confirm?";
                return;
            }

            var state = RubiniteDarkRubyPlugin.Instance?.State;
            if (state != null)
            {
                state.BestTimeSeconds = -1;
                state.SecondBestTimeSeconds = -1;
                state.ThirdBestTimeSeconds = -1;
                state.BestTalismanIds = new List<int> { 0, 0, 0, 0, 0, 0 };
                state.SecondBestTalismanIds = new List<int> { 0, 0, 0, 0, 0, 0 };
                state.ThirdBestTalismanIds = new List<int> { 0, 0, 0, 0, 0, 0 };
                //state.TotalClears = 0;
                state.Save();
            }

            _resetClickedAt = -1f;
            if (_resetLabel != null) _resetLabel.text = "Reset Times";

            RubiniteDarkRubyPlugin.Log?.LogInfo("DR leaderboard: times reset");

            var menu = MenuCore.Instance?.currentMenu as LeaderboardMenu;
            if (menu != null) { RefreshFrames(menu); RefreshStatsText(); }
        }

        public static void TickConfirmTimeout()
        {
            if (_resetClickedAt <= 0) return;
            if (Time.unscaledTime - _resetClickedAt > 3f)
            {
                _resetClickedAt = -1f;
                if (_resetLabel != null) _resetLabel.text = "Reset Times";
            }
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

            if (levelData.DisplayName != "DarkRubyLevel")
            {
                // Vanilla boss → full restoration, let vanilla run
                LeaderboardMenuDRHelper.RestoreVanillaDefaults(__instance);
                return true;
            }

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

    [HarmonyPatch(typeof(LeaderboardMenu), "UpdateLeaderboard")]
    internal static class LeaderboardMenuUpdateLeaderboardPatch
    {
        private static bool Prefix(LeaderboardMenu __instance)
        {
            // For DR: skip vanilla network download. Refresh is handled by PrepareForDarkRuby.
            if (!LeaderboardMenuDRHelper.IsDarkRubyMode(__instance)) return true;
            LeaderboardMenuDRHelper.RefreshFrames(__instance);
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
                LeaderboardMenuDRHelper.TickConfirmTimeout();

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

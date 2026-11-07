using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RubiniteDarkRuby.Patches
{
    /// <summary>
    /// Blocks the "Illusion" and "Leaderboard" options on the Dark Ruby podium dialogue
    /// menu. Both lead to cut content that softlocks when opened. Detection is by
    /// proximity to the nearest ChallengeLevelPoint - only the Dark Ruby podium is
    /// affected, all other podiums show their vanilla menu unchanged.
    ///
    /// When the AP mod is also running with boss shuffle, it handles the case
    /// where a shuffled portal (e.g. GuardianLevel -> DarkRubyLevel) leads to Dark Ruby.
    /// This patch only covers the actual Dark Ruby podium (levelName = DarkRubyLevel).
    /// Both are idempotent.
    /// </summary>
    [HarmonyPatch(typeof(TalkingChooseMenu), "SetChoices")]
    internal static class TalkingChooseMenuPatch
    {
        private static void Postfix(TalkingChooseMenu __instance, string[] choicesText)
        {
            var settings = RubiniteDarkRubyPlugin.Instance?.Settings;
            if (settings?.EnableMod?.Value != true) return;
            if (SceneManager.GetActiveScene().name != "ChallengeArea") return;
            if (__instance?.choices == null || choicesText == null) return;

            var player = GamePlayCore.Instance?.player;
            if (player == null) return;

            ChallengeLevelPoint nearest = null;
            float nearestDist = float.MaxValue;
            foreach (var point in UnityEngine.Resources.FindObjectsOfTypeAll<ChallengeLevelPoint>())
            {
                if (point?.gameObject == null || !point.gameObject.scene.IsValid()) continue;
                float d = Vector2.Distance(player.transform.position, point.transform.position);
                if (d < nearestDist) { nearestDist = d; nearest = point; }
            }

            if (nearest == null) return;
            if (nearestDist > 3.5f) return;

            // Resolve the actual boss level this podium leads to (handles shuffle for AP).
            var portalLevel = nearest.levelName?.displayName;
            if (string.IsNullOrEmpty(portalLevel)) return;

            string targetLevel = portalLevel;
            var mapped = ApBridgeHook.GetMappedLevel(portalLevel);
            if (!string.IsNullOrEmpty(mapped)) targetLevel = mapped;

            // Only block if this podium actually leads to Dark Ruby.
            if (targetLevel != "DarkRubyLevel") return;

            int blocked = 0;
            int count = Mathf.Min(__instance.choices.Length, choicesText.Length);
            for (int i = 0; i < count; i++)
            {
                var choice = __instance.choices[i];
                if (choice == null) continue;

                string text = choicesText[i] ?? "";
                if (text.Contains("Illusion")
                    || text.Contains("幻影")
                    || text.Contains("幻"))
                {
                    if (choice.button != null) choice.button.interactable = false;
                    choice.gameObject.SetActive(false);
                    blocked++;
                }
            }

            if (blocked > 0)
            {
                RubiniteDarkRubyPlugin.Log?.LogInfo(
                    "Blocked " + blocked + " cut-content option(s) at "
                    + portalLevel + " (-> " + targetLevel + ")");
            }
        }
    }
}

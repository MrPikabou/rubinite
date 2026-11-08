using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RubiniteDarkRuby.Patches
{
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
            }

            if (blocked > 0)
            {
                RubiniteDarkRubyPlugin.Log?.LogInfo(
                    "Blocked " + blocked + " cut-content option(s) at "
                    + portalLevel + " (-> " + targetLevel + ")");
            }
        }
    }

    [HarmonyPatch(typeof(TalkingChooseMenu), "Choose")]
    internal static class TalkingChooseMenuChoosePatch
    {
        private static void Prefix(TalkingChooseMenu __instance, int index)
        {
            if (__instance?.choices == null) return;
            if (index < 0 || index >= __instance.choices.Length) return;
            var choice = __instance.choices[index];
            if (choice == null) return;

            var text = choice.rubyTextMesh != null
                ? choice.rubyTextMesh.uneditedText
                : (choice.textMesh != null ? choice.textMesh.text : "");
            if (string.IsNullOrEmpty(text)) return;

            if (text.Contains("Illusion") || text.Contains("幻影") || text.Contains("幻"))
            {
                var player = GamePlayCore.Instance?.player;
                if (player == null) return;

                ChallengeLevelPoint nearest = null;
                float nearestDist = float.MaxValue;
                foreach (var p in UnityEngine.Resources.FindObjectsOfTypeAll<ChallengeLevelPoint>())
                {
                    if (p?.gameObject == null || !p.gameObject.scene.IsValid()) continue;
                    float d = Vector2.Distance(player.transform.position, p.transform.position);
                    if (d < nearestDist) { nearestDist = d; nearest = p; }
                }
                if (nearest == null || nearestDist > 3.5f) return;
                var portalLevel = nearest.levelName?.displayName;
                if (string.IsNullOrEmpty(portalLevel)) return;
                var mapped = ApBridgeHook.GetMappedLevel(portalLevel);
                var target = string.IsNullOrEmpty(mapped) ? portalLevel : mapped;
                if (target != "DarkRubyLevel") return;

                var module = RubiniteDarkRubyPlugin.Instance?.Module;
                if (module != null)
                {
                    module.IsIllusionRequested = true;
                    RubiniteDarkRubyPlugin.Log?.LogInfo("DR: Illusion button clicked");
                }
            }
        }
    }
}

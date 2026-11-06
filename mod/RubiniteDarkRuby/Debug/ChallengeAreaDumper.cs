using System.Collections.Generic;
using UnityEngine;

namespace RubiniteDarkRuby.Debug
{
    internal static class ChallengeAreaDumper
    {
        public static void DumpChallengeAreaSprites()
        {
            var log = RubiniteDarkRubyPlugin.Log;
            if (log == null) return;
            log.LogInfo("=== Challenge Area sprite dump ===");
            log.LogInfo("Scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

            var seen = new HashSet<string>();
            int total = 0;

            foreach (var sr in UnityEngine.Resources.FindObjectsOfTypeAll<SpriteRenderer>())
            {
                if (sr?.sprite == null) continue;
                if (!sr.gameObject.scene.IsValid()) continue;
                if (sr.gameObject.scene.name != "ChallengeArea") continue;

                total++;
                var key = sr.sprite.name;
                var pos = sr.transform.position;
                var logLine = "  [" + sr.gameObject.name + "] sprite='" + key
                    + "' pos=(" + pos.x.ToString("F2") + "," + pos.y.ToString("F2") + ")"
                    + " order=" + sr.sortingOrder
                    + " scale=(" + sr.transform.lossyScale.x.ToString("F2")
                    + "," + sr.transform.lossyScale.y.ToString("F2") + ")";

                // Log every instance of small floor-scale sprites near the podiums
                // (roughly x in [0..30], y in [-35..10]) so we can identify tiles.
                if (pos.x > -5f && pos.x < 40f && pos.y > -40f && pos.y < 20f)
                {
                    if (seen.Add(key + "@" + pos.x.ToString("F1") + "," + pos.y.ToString("F1")))
                        log.LogInfo(logLine);
                }
            }

            log.LogInfo("=== End sprite dump (" + total + " SpriteRenderers scanned) ===");
        }

        public static void DumpAllSpritesForObject(string objectName)
        {
            var log = RubiniteDarkRubyPlugin.Log;
            if (log == null) return;

            var go = GameObject.Find(objectName);
            if (go == null)
            {
                log.LogInfo("DumpAllSpritesForObject: '" + objectName + "' not found.");
                return;
            }

            log.LogInfo("=== Object dump: " + objectName + " ===");
            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
            {
                var s = sr.sprite != null ? sr.sprite.name : "(null)";
                log.LogInfo("  [" + sr.gameObject.name + "] sprite='" + s
                    + "' active=" + sr.gameObject.activeSelf);
            }
        }
    }
}

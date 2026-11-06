using System.Collections.Generic;
using HarmonyLib;
using UnityEngine.SceneManagement;

namespace RubiniteDarkRuby.Patches
{
    [HarmonyPatch(typeof(ApplicationCore), "LoadLevel")]
    internal static class LoadLevelPatch
    {
        private static readonly HashSet<string> StoryScenes = new HashSet<string>
        {
            "MasterSceneCamp", "QueenSceneCamp", "BedRoom", "WhiteRoom",
            "BloodSea", "BadWichiwi", "FirstRunEnding_AllIsDream",
            "Prologue", "Intro", "NamelessChurch"
        };

        private static bool Prefix(ref string levelName)
        {
            var module = RubiniteDarkRubyPlugin.Instance?.Module;
            if (module == null) return true;

            if (!StoryScenes.Contains(levelName) && !levelName.StartsWith("MasterSceneCamp"))
                return true;

            if (!module.ShouldInterceptStoryScene()) return true;
            if (!module.IsPlayerAlive()) return true;

            module.OnDarkRubyStorySceneIntercepted();
            return false;
        }
    }
}

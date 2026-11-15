using HarmonyLib;
using PugTool.Unity.Singleton;
using UnityEngine.SceneManagement;

//TODO DELETE
namespace RubiniteDarkRuby.Patches
{
    [HarmonyPatch(typeof(ApplicationCore), "LoadLevel")]
    internal static class LoadLevelDiagnosticPatch
    {
        private static void Prefix(string levelName, bool InitGamePlayCore, bool saveData)
        {
            var app = SingletonPrefab<ApplicationCore>.Instance;
            var mode = app?.GamePlayCore?.challengeMode;
            RubiniteDarkRubyPlugin.Log?.LogInfo(
                "[LoadLevel] level='" + levelName
                + "' initGPC=" + InitGamePlayCore
                + " saveData=" + saveData
                + " mode=" + mode
                + " from=" + SceneManager.GetActiveScene().name);
        }
    }
}

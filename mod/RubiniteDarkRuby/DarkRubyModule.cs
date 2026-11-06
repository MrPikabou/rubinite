using System;
using System.Collections;
using System.Reflection;
using PugTool.Unity.Singleton;
using PugTool.Unity.StringList;
using RubiniteDarkRuby.Debug;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RubiniteDarkRuby
{
    public sealed class DarkRubyModule
    {
        private readonly RubiniteDarkRubyPlugin _plugin;
        private GameObject _cachedPodium;
        private string _lastScene;

        private bool _darkRubyInProgress;
        private bool _darkRubyCompleted;
        private bool _darkRubyDeathHandled;
        private bool _darkRubyTimerPausedForCutscene;
        private string _lastDarkRubyCutsceneName;
        private string _lastPortalLevel;

        public bool DarkRubyCompleted => _darkRubyCompleted;
        public bool DarkRubyDeathHandled => _darkRubyDeathHandled;
        public bool DarkRubyTimerPausedForCutscene => _darkRubyTimerPausedForCutscene;
        public bool DarkRubyInProgress => _darkRubyInProgress;
        public string LastDarkRubyCutsceneName => _lastDarkRubyCutsceneName;
        public string LastPortalLevel => _lastPortalLevel;

        public DarkRubyModule(RubiniteDarkRubyPlugin plugin) { _plugin = plugin; }

        public void Tick()
        {
            if (!_plugin.Settings.EnableMod.Value) return;

            var scene = SceneManager.GetActiveScene().name;
            if (scene != _lastScene) { _lastScene = scene; _cachedPodium = null; }

            if (scene == "ChallengeArea")
            {
                if (Input.GetKeyDown(KeyCode.F10)) ChallengeAreaDumper.DumpChallengeAreaSprites();
                if (Input.GetKeyDown(KeyCode.F11)) RespawnPodium();
            }

            if (scene == "ChallengeArea" || scene == "DarkRubyLevel")
            {
                if (Input.GetKeyDown(KeyCode.F9)) DarkRubySkin.Toggle();

                if (DarkRubySkin.IsActive)
                {
                    var core = GamePlayCore.Instance;
                    if (core?.player != null) DarkRubySkin.Apply();
                }
            }

            if (scene != "ChallengeArea") return;
            if (!_plugin.Settings.SpawnPortalInChallengeArea.Value) return;
            EnsurePodiumSpawned();
        }

        /// <summary>
        /// Runs after all game Update() and animation work. Re-applies the Dark Ruby material
        /// whenever the game has rebuilt the player renderer via ChangeRubySkin.ChangeSkin().
        /// </summary>
        public void LateTick()
        {
            if (!_plugin.Settings.EnableMod.Value) return;
            if (!DarkRubySkin.IsActive) return;

            var scene = SceneManager.GetActiveScene().name;
            if (scene == "TitleMenu") return;
            if (scene != "ChallengeArea" && scene != "DarkRubyLevel") return;

            var player = GamePlayCore.Instance?.player;
            if (player == null) return;

            foreach (var sr in player.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.name != "Girl") continue;
                if (sr.material == null) continue;
                if (sr.material.name.StartsWith(DarkRubySkin.MaterialName)) continue;
                if (sr.gameObject.scene.name == "TitleMenu") continue;
                DarkRubySkin.ApplyToRenderer(sr);
            }
        }

        // ---- Podium ----

        public GameObject EnsurePodiumSpawned()
        {
            if (_cachedPodium != null) return _cachedPodium;
            var existing = GameObject.Find("RubiniteDR_Podium");
            if (existing != null) { _cachedPodium = existing; return existing; }
            try { var obj = DarkRubyPodium.TrySpawn(_plugin.Settings); if (obj != null) _cachedPodium = obj; }
            catch (Exception ex) { RubiniteDarkRubyPlugin.Log?.LogWarning("SpawnPodium failed: " + ex.Message); }
            return _cachedPodium;
        }

        private void RespawnPodium()
        {
            var old = GameObject.Find("RubiniteDR_Podium");
            if (old != null) { UnityEngine.Object.Destroy(old); RubiniteDarkRubyPlugin.Log?.LogInfo("Podium destroyed (F11)."); }
            _cachedPodium = null;
            EnsurePodiumSpawned();
        }

        // ---- CA entry ----

        public void OnChallengeAreaLoaded()
        {
            _cachedPodium = null;
            ApplyGradingToLevelData();
        }

        public void OnChallengeAreaReturned()
        {
            _darkRubyInProgress = false;
        }

        public void ClearLastPortalLevel() { _lastPortalLevel = null; }

        private void ApplyGradingToLevelData()
        {
            if (!_plugin.Settings.EnableMod.Value) return;
            var overview = SingletonScriptableObject<LevelDataOverview>.Instance;
            if (overview == null) return;

            LevelData data = overview.FindUniqueId("DarkRubyLevel");
            if (data == null)
                foreach (var ld in overview.GetAllData())
                    if (ld != null && ld.DisplayName == "DarkRubyLevel") { data = ld; break; }
            if (data == null) return;

            data.gradingTime = _plugin.Settings.GradingTime.Value;
            data.gradingScale = _plugin.Settings.ParseGradingScale();
            RubiniteDarkRubyPlugin.Log?.LogInfo(
                "Dark Ruby grading applied: time=" + data.gradingTime
                + ", scale=[" + string.Join(", ", data.gradingScale) + "]");
        }

        // ---- DR entry ----

        public void OnDarkRubyLevelLoaded(LevelManager lm)
        {
            if (!_plugin.Settings.EnableMod.Value) return;

            _darkRubyInProgress = true;
            _lastPortalLevel = "DarkRubyLevel";
            _darkRubyCompleted = false;
            _darkRubyDeathHandled = false;
            _darkRubyTimerPausedForCutscene = false;
            _lastDarkRubyCutsceneName = null;

            if (lm?.levelTimer == null) return;

            if (!lm.levelTimer.gameObject.activeSelf)
            {
                lm.levelTimer.gameObject.SetActive(true);
                RubiniteDarkRubyPlugin.Log?.LogInfo("Dark Ruby: activated levelTimer");
            }

            lm.levelTimer.timer = 0f;
            lm.levelTimer.SetStart();
            lm.levelTimer.Pause();
            _darkRubyTimerPausedForCutscene = true;
            RubiniteDarkRubyPlugin.Log?.LogInfo("Dark Ruby: timer started paused (waiting for intro cutscene)");
        }

        // ---- Cutscenes ----

        public void OnDarkRubyCutsceneStart(string cutsceneName)
        {
            _lastDarkRubyCutsceneName = cutsceneName;
            RubiniteDarkRubyPlugin.Log?.LogInfo("Dark Ruby: cutscene started '" + cutsceneName + "'");
        }

        public bool ShouldBlockDarkRubyCutscene(bool bossDead)
        {
            bool winMenuOpen = MenuCore.Instance?.currentMenu == GamePlayCore.Instance?.challengeWinMenu;
            if ((bossDead && _darkRubyDeathHandled) || winMenuOpen)
            {
                RubiniteDarkRubyPlugin.Log?.LogInfo("Dark Ruby: blocked cutscene '" + _lastDarkRubyCutsceneName + "'");
                return true;
            }
            return false;
        }

        public void OnDarkRubyEnterCutScene()
        {
            var lm = GamePlayCore.Instance?.levelManager;
            if (lm?.levelData?.DisplayName != "DarkRubyLevel" || lm.levelTimer == null) return;

            RubiniteDarkRubyPlugin.Log?.LogInfo(
                "Dark Ruby: EnterCutSceneMode (current='" + _lastDarkRubyCutsceneName + "')");

            if (lm.levelTimer.timer > 0f || _darkRubyTimerPausedForCutscene)
            {
                lm.levelTimer.Pause();
                _darkRubyTimerPausedForCutscene = true;
                RubiniteDarkRubyPlugin.Log?.LogInfo("Dark Ruby: timer paused (cutscene entered)");
            }
        }

        public void OnDarkRubyExitCutScene()
        {
            var lm = GamePlayCore.Instance?.levelManager;
            if (lm?.levelData?.DisplayName != "DarkRubyLevel") return;

            var introCutscenes = new[] { "EnterGameFromTitleCutscene", "EnterGameFromMasterLevelCutscene" };
            if (Array.IndexOf(introCutscenes, _lastDarkRubyCutsceneName) >= 0)
            {
                var player = GamePlayCore.Instance?.player;
                if (player != null && lm.playerSpawnPoint != null)
                {
                    var pos = lm.playerSpawnPoint.position;
                    pos.z = player.transform.position.z;
                    player.transform.position = pos;
                    RubiniteDarkRubyPlugin.Log?.LogInfo(
                        "Dark Ruby: teleported to bench at (" + pos.x.ToString("F2")
                        + "," + pos.y.ToString("F2") + ")");
                }
            }

            if (!_darkRubyTimerPausedForCutscene || lm.levelTimer == null) return;

            if (lm.boss != null && lm.boss.isDead)
            {
                RubiniteDarkRubyPlugin.Log?.LogInfo("Dark Ruby: timer stays paused (boss dead)");
                return;
            }

            var bossIntroCutscenes = new System.Collections.Generic.HashSet<string>
            { "DarkRubyAppearCutscene", "DarkRubyFirstAppearCutscene" };

            if (!bossIntroCutscenes.Contains(_lastDarkRubyCutsceneName))
            {
                RubiniteDarkRubyPlugin.Log?.LogInfo(
                    "Dark Ruby: timer stays paused (last='" + _lastDarkRubyCutsceneName
                    + "', not a boss-intro cutscene)");
                return;
            }

            lm.levelTimer.Resume();
            _darkRubyTimerPausedForCutscene = false;
            RubiniteDarkRubyPlugin.Log?.LogInfo("Dark Ruby: timer resumed after '" + _lastDarkRubyCutsceneName + "'");
        }

        // ---- Boss death ----

        public void OnDarkRubyBossDead()
        {
            if (_darkRubyDeathHandled) return;

            var lm = GamePlayCore.Instance?.levelManager;
            if (lm?.levelTimer != null)
            {
                lm.levelTimer.Pause();
                _darkRubyTimerPausedForCutscene = true;
                RubiniteDarkRubyPlugin.Log?.LogInfo("Dark Ruby: timer paused (boss died)");
            }

            _darkRubyDeathHandled = true;
            RubiniteDarkRubyPlugin.Log?.LogInfo("Dark Ruby: death detected → scheduling Win Menu");
            _plugin.StartCoroutine(OpenWinMenuAfterDeath());
        }

        private IEnumerator OpenWinMenuAfterDeath()
        {
            yield return new WaitForSeconds(3f);

            var lm = GamePlayCore.Instance?.levelManager;
            if (lm == null) yield break;

            _darkRubyCompleted = true;
            RecordFightResult(lm);
            _darkRubyInProgress = false;

            try
            {
                var winMenu = GamePlayCore.Instance.challengeWinMenu;
                if (winMenu != null)
                {
                    MenuCore.Instance.EnterMenu(winMenu);
                    RubiniteDarkRubyPlugin.Log?.LogInfo("Dark Ruby: ChallengeWinMenu opened");
                }
                else RubiniteDarkRubyPlugin.Log?.LogWarning("challengeWinMenu is null");
            }
            catch (Exception ex) { RubiniteDarkRubyPlugin.Log?.LogWarning("Failed to open Win Menu: " + ex.Message); }
        }

        // ---- Story-scene intercept fallback ----

        public bool ShouldInterceptStoryScene()
            => _darkRubyInProgress && !_darkRubyCompleted;

        public void OnDarkRubyStorySceneIntercepted()
        {
            var lm = GamePlayCore.Instance?.levelManager;
            if (lm == null) return;

            _darkRubyCompleted = true;
            RecordFightResult(lm);
            _darkRubyInProgress = false;

            RubiniteDarkRubyPlugin.Log?.LogInfo("Dark Ruby: story scene intercepted — opening win menu");
            try
            {
                var winMenu = GamePlayCore.Instance.challengeWinMenu;
                if (winMenu != null) MenuCore.Instance.EnterMenu(winMenu);
            }
            catch (Exception ex) { RubiniteDarkRubyPlugin.Log?.LogWarning("Failed to open win menu (intercept): " + ex.Message); }
        }

        // ---- Mission failed ----

        public void OnDarkRubyMissionFailed()
        {
            _darkRubyInProgress = false;
            RubiniteDarkRubyPlugin.Log?.LogInfo("Dark Ruby: MissionFailed — state cleared");
        }

        // ---- Helpers ----

        private void RecordFightResult(LevelManager lm)
        {
            if (lm?.levelTimer == null) return;
            float time = lm.levelTimer.timer;
            int rankIndex = CalculateRankIndex(lm.levelData, time);

            _plugin.State.LastFightTimeSeconds = time;
            _plugin.State.RecordClear(time, rankIndex);

            string rankStr = rankIndex switch
            {
                0 => "S", 1 => "A", 2 => "B", 3 => "C", _ => "?"
            };
            RubiniteDarkRubyPlugin.Log?.LogInfo(
                "Dark Ruby cleared in " + time.ToString("F2") + "s (Rank " + rankStr + "). Best: "
                + _plugin.State.BestTimeSeconds.ToString("F2") + "s");
        }

        private static int CalculateRankIndex(LevelData levelData, float time)
        {
            if (levelData?.gradingScale == null || levelData.gradingScale.Length == 0)
                return -1;

            float threshold = levelData.gradingTime;
            for (int i = 0; i < levelData.gradingScale.Length; i++)
            {
                threshold += levelData.gradingScale[i];
                if (time <= threshold) return i;
            }
            return levelData.gradingScale.Length;
        }

        public bool IsPlayerAlive()
        {
            var core = GamePlayCore.Instance;
            if (core?.player == null) return false;
            try
            {
                var sf = core.player.GetType().GetField("state",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (sf == null) return true;
                var sv = sf.GetValue(core.player);
                return sv?.ToString() != "Dead";
            }
            catch { return true; }
        }

        public IEnumerator TeleportPlayerToDarkRubyPodium()
        {
            yield return null;

            var core = GamePlayCore.Instance;
            if (core?.player == null) { RubiniteDarkRubyPlugin.Log?.LogInfo("Teleport: player not found"); yield break; }

            Vector3 targetPos = Vector3.zero;
            bool found = false;
            foreach (var point in UnityEngine.Resources.FindObjectsOfTypeAll<ChallengeLevelPoint>())
            {
                if (point?.gameObject == null || !point.gameObject.scene.IsValid()) continue;
                if (point.levelName?.displayName == "DarkRubyLevel")
                {
                    targetPos = point.transform.position; targetPos.y -= 1.2f;
                    targetPos.z = core.player.transform.position.z; found = true; break;
                }
            }
            if (!found) { RubiniteDarkRubyPlugin.Log?.LogInfo("Teleport: DR podium not found"); yield break; }

            RubiniteDarkRubyPlugin.Log?.LogInfo("Teleport: enforcing position at " + targetPos);
            float timer = 0f;
            while (timer < 1.5f)
            {
                var cc2 = GamePlayCore.Instance;
                if (cc2?.player == null) yield break;
                var cc = cc2.player.GetComponent<CharacterController>();
                var rb = cc2.player.GetComponent<Rigidbody2D>();
                var anim = cc2.player.GetComponent<Animator>();
                if (cc != null) cc.enabled = false;
                if (anim != null && timer < 0.2f) anim.enabled = false;
                if (rb != null) { rb.velocity = Vector2.zero; rb.position = targetPos; }
                cc2.player.transform.position = targetPos;
                if (cc != null) cc.enabled = true;
                if (anim != null && timer >= 0.2f) anim.enabled = true;
                timer += Time.deltaTime;
                yield return null;
            }
            RubiniteDarkRubyPlugin.Log?.LogInfo("Teleport: position lock released");
        }
    }
}

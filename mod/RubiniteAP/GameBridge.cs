using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using PugTool.Unity.Singleton;
using PugTool.Unity.StringList;
using UnityEngine;
using UnityEngine.SceneManagement;
using IOPath = System.IO.Path;

namespace RubiniteAP
{
    public sealed class GameBridge
    {
        private const string ChallengeAreaScene = "ChallengeArea";

        public readonly ApState State;
        public readonly ApClient Client;

        public bool ApRunActive;
        public string StatusMessage = "Not connected.";
        public readonly List<string> LogLines = new List<string>();

        private readonly Dictionary<long, string> _itemNames = new Dictionary<long, string>();
        private readonly Dictionary<string, string> _talismanItemByDataId = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _skillItemByTreeId = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _bossSceneMap = new Dictionary<string, string>();
        private readonly Dictionary<string, (string sigil, bool start)> _bossSlotInfo = new Dictionary<string, (string, bool)>();
        private bool _initialApplied;
        private bool _needsFullLock;
        private bool _startingRun;
        private int _messageCount;
        private string _goalBossKey = "Witch2";

        public event Action LogChanged;

        public GameBridge(ApState state, ApClient client)
        {
            State = state;
            Client = client;

            foreach (var kv in GameData.TalismanIds)
                _talismanItemByDataId[kv.Value] = "Talisman: " + kv.Key;
            foreach (var kv in GameData.SkillTitles)
            {
                var itemName = GameData.SkillItemName(kv.Key);
                if (!string.IsNullOrEmpty(itemName))
                    _skillItemByTreeId[kv.Key] = itemName;
            }

            Client.Connected += OnConnected;
            Client.ItemsReceived += OnItemsReceived;
            Client.ConnectionChanged += OnConnectionChanged;
            Client.Log += AddLog;
        }

        private void OnConnectionChanged(bool connected, string message)
        {
            StatusMessage = message;
            AddLog(message);
        }

        private void OnConnected(ConnectedInfo info)
        {
            var slot = RubiniteAPPlugin.Instance.Settings.Slot.Value.Trim();
            if (string.IsNullOrEmpty(slot))
                slot = info.SeedName;

            var seedChanged = !string.IsNullOrEmpty(info.SeedName) && State.Seed != info.SeedName;
            var slotChanged = !string.IsNullOrEmpty(State.Slot) && State.Slot != slot;
            if (string.IsNullOrEmpty(State.Slot) || State.Slot != slot || seedChanged || slotChanged)
            {
                State.ResetForSlot(slot, info.SeedName);
                _needsFullLock = true;
            }
            else
            {
                State.Slot = slot;
                State.Seed = info.SeedName;
                State.Save();
            }

            _itemNames.Clear();
            if (info.SlotData?["item_ids"] is JObject itemIds)
            {
                foreach (var property in itemIds.Properties())
                {
                    if (long.TryParse(property.Value.ToString(), out var id))
                        _itemNames[id] = property.Name;
                }
            }

            _bossSceneMap.Clear();
            if (info.SlotData?["boss_scene_map"] is JObject sceneMap)
            {
                foreach (var prop in sceneMap.Properties())
                {
                    var from = prop.Name;
                    var to = prop.Value?.ToString();
                    if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to))
                        _bossSceneMap[from] = to;
                }
                AddLog("Boss scene map loaded (" + _bossSceneMap.Count + " entries).");
            }

<<<<<<< HEAD
            _bossSlotInfo.Clear();
            if (info.SlotData?["bosses"] is JArray bossesArr)
            {       
                foreach (var bossToken in bossesArr)
                {
                    var key = bossToken["key"]?.ToString();
                    if (string.IsNullOrEmpty(key)) continue;
                    var sigil = bossToken["sigil"]?.ToString();   // null when starting
                    var start = bossToken["start"]?.Value<bool>() ?? false;
                    _bossSlotInfo[key] = (sigil, start);
                }
            AddLog("Boss slot info loaded (" + _bossSlotInfo.Count + " entries).");
            }

=======
>>>>>>> 88c858e9f1129d3bd215be8b689fc2ad43806a9b
            var goalFromSlot = info.SlotData?["goal_boss"]?.ToString();
            if (!string.IsNullOrEmpty(goalFromSlot))
            {
                _goalBossKey = goalFromSlot;
                AddLog("Goal boss: " + _goalBossKey);
            }

            _enabledChecks.Clear();
            if (info.SlotData?["enabled_checks"] is JArray enabledArray)
            {
                foreach (var item in enabledArray)
                {
                    var slug = item?.ToString();
                    if (!string.IsNullOrEmpty(slug))
                        _enabledChecks.Add(slug);
                }
            }
            else
            {
                // Fallback for older seeds (version 1): enable all optional checks.
                _enabledChecks.Add("first");
                _enabledChecks.Add("nohit");
                _enabledChecks.Add("notalisman");
                _enabledChecks.Add("flawless");
                _enabledChecks.Add("srank");
            }
            AddLog("Enabled checks: " + string.Join(", ", _enabledChecks));

            State.AddChecked(info.CheckedLocations);
            State.Save();

            StatusMessage = "Connected as " + slot + (string.IsNullOrEmpty(info.SeedName) ? "" : " (" + info.SeedName + ")");
            AddLog(StatusMessage);

            if (State.Checked.Count > 0)
                Client.SendLocationChecks(State.Checked);
            if (State.GoalSent)
                Client.SendGoal();

            if (ApRunActive)
                ApplyRuntime(_needsFullLock);
            _needsFullLock = false;
        }

        private void OnItemsReceived(int index, List<NetworkItem> items)
        {
            var fullInventory = index == 0;
            if (fullInventory)
            {
                State.Items.Clear();
                State.LastItemIndex = 0;
            }

            var added = new List<string>();
            var start = index;
            if (start < 0) start = 0;
            if (start > State.LastItemIndex + 64)
                AddLog("Item index gap (" + State.LastItemIndex + " -> " + start + "); accepting packet.");

            for (var i = 0; i < items.Count; i++)
            {
                var position = start + i;
                if (position < State.LastItemIndex)
                    continue;
                var item = items[i];
                string name;
                if (!_itemNames.TryGetValue(item.ItemId, out name) || string.IsNullOrEmpty(name))
                {
                    AddLog("Received item #" + item.ItemId + " from another world.");
                    continue;
                }
                State.AddItem(name);
                added.Add(name);
            }

            if (fullInventory)
                State.LastItemIndex = items.Count;
            else
                State.LastItemIndex = Math.Max(State.LastItemIndex, start + items.Count);
            State.Save();

            foreach (var name in added)
            {
                AddLog("Received: " + name);
                ShowItemToast(name);
            }

            if (added.Count > 0 && ApRunActive)
                ApplyRuntime(false);
        }

        public void AddLog(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;
            _messageCount++;
            LogLines.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + message);
            if (LogLines.Count > 200)
                LogLines.RemoveRange(0, LogLines.Count - 200);
            LogChanged?.Invoke();
        }

        private void ShowItemToast(string name)
        {
            // A lightweight in-game notice: the overlay keeps the full history.
            StatusMessage = "Received " + name;
        }

        public void Tick()
        {
            if (_startingRun)
                return;
            if (ApRunActive && DataSaver.currentSaveNumber != GameData.ChallengeSaveNumber)
                ApRunActive = false;
            if (ApRunActive && SceneManager.GetActiveScene().name == "TitleMenu")
            {
                ApRunActive = false;
                AddLog("Run ended (returned to the title screen).");
            }
        }

        public void StartRun()
        {
            if (!Client.IsConnected)
            {
                AddLog("Connect to Archipelago before starting a run.");
                return;
            }
            if (ApRunActive)
            {
                AddLog("An Archipelago run is already active.");
                return;
            }
            if (_startingRun)
            {
                AddLog("The run is already starting.");
                return;
            }
            if (SceneManager.GetActiveScene().name != "TitleMenu")
            {
                AddLog("Start the run from the title screen (return to title first).");
                return;
            }

            _startingRun = true;
            RubiniteAPPlugin.Instance.StartCoroutine(StartRunWhenTitleReady());
        }

        private IEnumerator StartRunWhenTitleReady()
        {
            // The title scene is loaded additively and the loader ignores new
            // requests while it is still fading/loading. Wait for it to finish.
            for (var i = 0; i < 100; i++)
            {
                if (SceneManager.GetActiveScene().name != "TitleMenu")
                {
                    _startingRun = false;
                    AddLog("The title screen changed before the run could start.");
                    yield break;
                }

                var loading = false;
                try
                {
                    var core = SingletonPrefab<ApplicationCore>.Instance;
                    loading = core != null && core.levelLoader != null && core.levelLoader.isLoading;
                }
                catch
                {
                    // ApplicationCore may not be ready yet; keep waiting.
                }
                if (!loading)
                    break;
                yield return new WaitForSeconds(0.1f);
            }

            yield return new WaitForSeconds(0.2f);
            if (SceneManager.GetActiveScene().name != "TitleMenu")
            {
                _startingRun = false;
                AddLog("The title screen changed before the run could start.");
                yield break;
            }
            DoStartRun();
        }

        private void DoStartRun()
        {
            try
            {
                BackupChallengeSave();
                ApRunActive = true;
                _initialApplied = false;
                _needsFullLock = true;

                // Build a challenge save, then rewrite its flags before the game
                // instantiates skills in GamePlayCore.SetUp().
                DataSaver.currentSaveNumber = GameData.ChallengeSaveNumber;
                ChallengeSave.RefreshFromAllNormalSaves();
                DataSaver.LoadFlag();
                ApplyToFlags(true);
                DataSaver.SaveFlag(DataSaver.path_saveFolder);

                ChallengeSave.EnterFromTitle();
                AddLog("Entering the Challenge Area...");
                RubiniteAPPlugin.Instance.StartCoroutine(WaitForChallengeArea());
            }
            catch (Exception ex)
            {
                ApRunActive = false;
                _startingRun = false;
                AddLog("Could not start the run: " + ex.Message);
            }
        }

        private IEnumerator WaitForChallengeArea()
        {
            for (var i = 0; i < 600; i++)
            {
                if (SceneManager.GetActiveScene().name == ChallengeAreaScene)
                {
                    _startingRun = false;
                    yield return new WaitForSeconds(0.4f);
                    ApplyRuntime(true);
                    RefreshPortals();
                    AddLog("Archipelago run ready. Pick an unlocked boss portal.");
                    yield break;
                }
                yield return new WaitForSeconds(0.1f);
            }
            _startingRun = false;
            AddLog("Timed out waiting for the Challenge Area.");
        }

        private static void BackupChallengeSave()
        {
            var versionFolder = DataSaver.path_versionFolder;
            var source = IOPath.Combine(versionFolder, DataSaver.CHALLENGE_SAVE_FOLDER);
            var backup = IOPath.Combine(versionFolder, "ChallengeSave_APBackup");
            if (Directory.Exists(source) && !Directory.Exists(backup))
            {
                CopyDirectory(source, backup);
            }
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, IOPath.Combine(destination, IOPath.GetFileName(file)), true);
            foreach (var directory in Directory.GetDirectories(source))
                CopyDirectory(directory, IOPath.Combine(destination, IOPath.GetFileName(directory)));
        }

        private static GamePlayCore GetGamePlayCore()
        {
            try { return GamePlayCore.Instance; }
            catch { return null; }
        }

        private bool IsReceivedTalisman(TalismanData talisman)
        {
            if (talisman == null || string.IsNullOrEmpty(talisman.DisplayName))
                return false;
            return _talismanItemByDataId.TryGetValue(talisman.DisplayName, out var itemName)
                   && State.HasItem(itemName);
        }

        private bool IsReceivedSkill(SkillTreeData tree)
        {
            if (tree == null || string.IsNullOrEmpty(tree.DisplayName))
                return false;
            return _skillItemByTreeId.TryGetValue(tree.DisplayName, out var itemName)
                   && State.HasItem(itemName);
        }

        /// <summary>Write item ownership into game flags, without touching instantiated skills.</summary>
        private void ApplyToFlags(bool lockUnreceived)
        {
            var talismanOverview = SingletonScriptableObject<TalismanDataOverview>.Instance;
            if (talismanOverview != null)
            {
                foreach (var talisman in talismanOverview.GetAllData())
                {
                    if (talisman == null || talisman.state == null)
                        continue;
                    if (IsReceivedTalisman(talisman))
                    {
                        talisman.state.SetValue(AchievementState.Unlock);
                        talisman.storeState?.SetValue(AchievementState.Unlock);
                    }
                    else if (lockUnreceived)
                    {
                        talisman.state.SetValue(AchievementState.Lock);
                        talisman.storeState?.SetValue(AchievementState.Lock);
                        talisman.isEquipped?.SetValue(0);
                    }
                }
            }

            var skillOverview = SingletonScriptableObject<SkillTreeDataOverview>.Instance;
            if (skillOverview != null)
            {
                foreach (var tree in skillOverview.GetAllData())
                {
                    if (tree == null || tree.skillTreeDataDetail == null)
                        continue;
                    if (tree.skillTreeDataDetail.isUnlocked != null
                        && tree.skillTreeDataDetail.isUnlocked.name == "GameFlagBool_True")
                        continue;
                    if (GameData.IsFreeSkill(tree.DisplayName))
                    {
                        SetSkillFlags(tree, true, false);
                        continue;
                    }
                    SetSkillFlags(tree, IsReceivedSkill(tree), lockUnreceived);
                }
            }
        }

        private static void SetSkillFlags(SkillTreeData tree, bool unlocked, bool applyLock)
        {
            var detail = tree.skillTreeDataDetail;
            if (detail?.isUnlocked != null)
                detail.isUnlocked.SetValue(unlocked);
            if (detail?.isKnown != null)
                detail.isKnown.SetValue(unlocked);

            var second = tree.secondRunSkillTreeDataDetail;
            if (second?.isUnlocked != null)
                second.isUnlocked.SetValue(unlocked);
            if (second?.isKnown != null)
                second.isKnown.SetValue(unlocked);
        }

        /// <summary>Apply ownership at runtime, instantiating newly received skills.</summary>
        public void ApplyRuntime(bool lockUnreceived)
        {
            // Record which received skills were already instantiated before the
            // flags are rewritten. GamePlayCore.SetUp() instantiates every skill
            // whose flag is true, so a skill that was already true is already live.
            var alreadyLive = new HashSet<string>();
            var skillOverview = SingletonScriptableObject<SkillTreeDataOverview>.Instance;
            if (skillOverview != null)
            {
                foreach (var tree in skillOverview.GetAllData())
                {
                    if (tree == null || tree.skillTreeDataDetail == null) continue;
                    if (GameData.IsFreeSkill(tree.DisplayName)) continue;
                    if (!IsReceivedSkill(tree)) continue;
                    var flag = tree.skillTreeDataDetail.isUnlocked;
                    if (flag != null && flag.GetValue())
                        alreadyLive.Add(tree.DisplayName);
                }
            }

            ApplyToFlags(lockUnreceived);

            var core = GetGamePlayCore();
            if (core != null && skillOverview != null)
            {
                foreach (var tree in skillOverview.GetAllData())
                {
                    if (tree == null || tree.skillTreeDataDetail == null)
                        continue;
                    if (tree.skillTreeDataDetail.isUnlocked != null
                        && tree.skillTreeDataDetail.isUnlocked.name == "GameFlagBool_True")
                        continue;
                    if (GameData.IsFreeSkill(tree.DisplayName))
                        continue;
                    if (!IsReceivedSkill(tree))
                        continue;
                    if (alreadyLive.Contains(tree.DisplayName))
                        continue;

                    try { core.SetSkill(tree); }
                    catch (Exception ex) { AddLog("Skill setup failed for " + tree.DisplayName + ": " + ex.Message); }
                }
            }

            if (ApRunActive && DataSaver.currentSaveNumber == GameData.ChallengeSaveNumber)
            {
                try { DataSaver.SaveFlag(DataSaver.path_saveFolder); }
                catch (Exception ex) { AddLog("Could not persist flags: " + ex.Message); }
            }

            RefreshPortals();
            _initialApplied = true;
        }

        private bool IsBossUnlocked(BossInfo boss)
        {
            if (boss == null)
                return true;
<<<<<<< HEAD

            // slot_data is the source of truth for version 2+ seeds.
            if (!string.IsNullOrEmpty(boss.Key) && _bossSlotInfo.TryGetValue(boss.Key, out var info))
            {
                if (info.start)
                    return true;
                return !string.IsNullOrEmpty(info.sigil) && State.HasItem(info.sigil);
            }

            // If slot_data is missing a boss entry, treat it as locked to avoid
            // accidentally unlocking progression. This should never happen with
            // version 2 seeds, but failing closed is safer than failing open.
            AddLog("WARNING: No slot_data entry for boss '" + boss.Key + "'; treating as locked.");
            return false;
=======
            if (boss.StartUnlocked)
                return true;
            return !string.IsNullOrEmpty(boss.Sigil) && State.HasItem(boss.Sigil);
>>>>>>> 88c858e9f1129d3bd215be8b689fc2ad43806a9b
        }

        /// <summary>Called from GamePlayCore.SetUp before flags are read into the save.</summary>
        public void PrepareSaveFlags()
        {
            if (!ApRunActive || DataSaver.currentSaveNumber != GameData.ChallengeSaveNumber)
                return;
            try
            {
                DataSaver.LoadFlag();
                ApplyToFlags(true);
                DataSaver.SaveFlag(DataSaver.path_saveFolder);
            }
            catch (Exception ex)
            {
                AddLog("Could not prepare save flags: " + ex.Message);
            }
        }

        public bool ShouldBlockLoad(string levelName)
        {
            if (!ApRunActive || DataSaver.currentSaveNumber != GameData.ChallengeSaveNumber)
                return false;
            var boss = GameData.FindByLevel(levelName);
            if (boss == null || IsBossUnlocked(boss))
                return false;
            StatusMessage = boss.Name + " is locked. Find " + boss.Sigil + ".";
            AddLog(StatusMessage);
            return true;
        }

        public void RefreshPortals()
        {
            if (!ApRunActive || DataSaver.currentSaveNumber != GameData.ChallengeSaveNumber)
                return;

            var points = Resources.FindObjectsOfTypeAll<ChallengeLevelPoint>();
            foreach (var point in points)
            {
                if (point == null || point.gameObject == null || !point.gameObject.scene.IsValid())
                    continue;
                var level = point.levelName != null ? point.levelName.displayName : null;
                var boss = GameData.FindByLevel(level);
                if (boss == null)
                    continue;
                var unlocked = IsBossUnlocked(boss);

                if (point.dialogueItem != null)
                    point.dialogueItem.gameObject.SetActive(unlocked);
                if (point.unlock_Part != null)
                    point.unlock_Part.SetActive(unlocked);
                if (point.lock_Part != null)
                    point.lock_Part.SetActive(!unlocked);
                if (!unlocked && point.onEnableLoadLevel != null
                    && point.onEnableLoadLevel.gameObject.activeSelf)
                {
                    point.onEnableLoadLevel.gameObject.SetActive(false);
                }
            }
        }

        public void HandleLevelCompleted(LevelManager manager)
        {
            if (!ApRunActive || manager == null || manager.levelData == null)
                return;
            if (DataSaver.currentSaveNumber != GameData.ChallengeSaveNumber)
                return;

            var boss = GameData.FindByLevel(manager.levelData.DisplayName);
            if (boss == null)
                return;

            var core = GetGamePlayCore();
            var hits = manager.Hits;
            var talismans = core != null && core.equippedTalismanList != null
                ? core.equippedTalismanList.Count
                : -1;

            var noHit = hits == 0;
            var noTalisman = talismans == 0;
            var flawless = noHit && noTalisman;
            var sRank = false;
            if (manager.levelTimer != null && manager.levelData.gradingScale != null
                && manager.levelData.gradingScale.Length > 0)
            {
                var threshold = manager.levelData.gradingTime + manager.levelData.gradingScale[0];
                sRank = manager.levelTimer.timer <= threshold;
            }

            var ids = new List<long> { boss.LocationBase };
            if (noHit && _enabledChecks.Contains("nohit"))
                ids.Add(boss.LocationBase + 1);
            if (noTalisman && _enabledChecks.Contains("notalisman"))
                ids.Add(boss.LocationBase + 2);
            if (flawless && _enabledChecks.Contains("flawless"))
                ids.Add(boss.LocationBase + 3);
            if (sRank && _enabledChecks.Contains("srank"))
                ids.Add(boss.LocationBase + 4);

            var newIds = new List<long>();
            foreach (var id in ids)
            {
                if (State.HasChecked(id)) continue;
                newIds.Add(id);
            }

            if (newIds.Count > 0)
            {
                State.AddChecked(newIds);
                State.Save();
                if (Client.IsConnected)
                    Client.SendLocationChecks(newIds);

                foreach (var id in newIds)
                {
                    var index = (int)(id - boss.LocationBase);
                    AddLog("Check: " + GameData.LocationName(boss, index));
                }
            }

            if (boss.Key == _goalBossKey && !State.GoalSent)
            {
                State.GoalSent = true;
                State.Save();
                if (Client.IsConnected)
                    Client.SendGoal();
                AddLog("Goal complete: " + boss.Name + " defeated!");
            }
        }

        public static string GetTargetLevel(OnEnableLoadLevel component)
        {
            if (component == null)
                return null;
            try
            {
                var method = typeof(OnEnableLoadLevel).GetMethod(
                    "GetLevelName", BindingFlags.Instance | BindingFlags.NonPublic);
                if (method != null)
                    return (string)method.Invoke(component, null);
            }
            catch
            {
                // fall through to the public field
            }
            return component.levelName != null ? component.levelName.displayName : null;
        }

        public string RemapBossScene(string levelName)
        {
            if (string.IsNullOrEmpty(levelName))
                return levelName;
            return _bossSceneMap.TryGetValue(levelName, out var mapped) && !string.IsNullOrEmpty(mapped)
                ? mapped
                : levelName;
        }
    }
}

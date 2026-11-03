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
using TMPro;

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
        private readonly Dictionary<int, string> _playerNames = new Dictionary<int, string>();

        private readonly Dictionary<string, string> _talismanItemByDataId = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _skillItemByTreeId = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _bossSceneMap = new Dictionary<string, string>();
        private readonly Dictionary<string, (string sigil, bool start)> _bossSlotInfoByLevel = new Dictionary<string, (string, bool)>();
        private readonly Dictionary<string, Sprite> _bossSteleByLevel = new Dictionary<string, Sprite>();

        private readonly Dictionary<string, long> _shopLocationIds = new Dictionary<string, long>();
        private readonly Dictionary<string, int> _shopGroupByTalisman = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _effectiveShopPrices = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _effectiveSkillPrices = new Dictionary<string, int>();
        private readonly Dictionary<string, string> _scoutedShopItems = new Dictionary<string, string>();
        private readonly Dictionary<string, int> _scoutedShopFlags = new Dictionary<string, int>();
        private readonly Dictionary<string, long> _skillTreeLocationIds = new Dictionary<string, long>();
        private readonly Dictionary<string, int> _fillerDustValues = new Dictionary<string, int>();
        private readonly Dictionary<string, string> _fillerIconNames = new Dictionary<string, string>();

        private readonly HashSet<string> _crossGameScoutedTitles = new HashSet<string>();
        private readonly HashSet<string> _enabledChecks = new HashSet<string>();
        
        private int _mySlot;
        private bool _needsFullLock;
        private bool _startingRun;
        private int _messageCount;
        private string _goalBossKey = "Witch2";

        private string _lastLoadedLevel = null;
        private string _lastPortalLevel = null;
        public string LastPortalLevel => _lastPortalLevel;
        private string _podiumClickPending = null;

        private int _requiredRankIndex = 0;

        private string _shopPriceMode = "vanilla";
        private bool _shopProgressionEnabled;
        private int _startingShopGroups;
        private string _skillPriceMode = "vanilla";

        private bool _flagsInitializedThisRun;
        private string _playerColorKey = "default";

        private bool _seedHasDeathLink;
        private int _deathLinkAmnesty;
        private int _deathsSinceLastSend;
        private int _lastKnownPlayerHp = -1;
        private bool _playerDeathFired;
        private System.Reflection.MethodInfo _playerKillMethod;
        
        private static System.Reflection.MethodInfo _getLevelNameMethod;
        private static bool _getLevelNameMethodSearched;
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _logQueue
            = new System.Collections.Concurrent.ConcurrentQueue<string>();

        private bool DeathLinkActive =>
            ApRunActive &&
            _seedHasDeathLink &&
            (RubiniteAPPlugin.Instance?.Settings?.DeathLinkEnabled?.Value ?? false);

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
            Client.LocationScouted += OnLocationScouted;
            Client.DataPackageReady += OnDataPackageReady;
            Client.Log += AddLog;
            Client.DeathLinkReceived += OnDeathLinkReceived;
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

            _shopLocationIds.Clear();
            if (info.SlotData?["shop_locations"] is JObject shopLocs)
            {
                foreach (var prop in shopLocs.Properties())
                {
                    if (long.TryParse(prop.Value.ToString(), out var id))
                        _shopLocationIds[prop.Name] = id;
                }
                AddLog("Shop locations loaded (" + _shopLocationIds.Count + " entries).");
            }

            if (_shopLocationIds.Count > 0)
            {
                Client.RequestLocationScoutsWhenReady(_shopLocationIds.Values);
                AddLog("Queued scouts for " + _shopLocationIds.Count + " shop slots (waiting for DataPackage).");
            }

            _skillTreeLocationIds.Clear();
            if (info.SlotData?["skill_tree_locations"] is JObject stLoc)
            {
                foreach (var prop in stLoc.Properties())
                {
                    if (long.TryParse(prop.Value.ToString(), out var id))
                        _skillTreeLocationIds[prop.Name] = id;
                }
                AddLog("Skill tree locations loaded (" + _skillTreeLocationIds.Count + " entries).");
            }

            if (_skillTreeLocationIds.Count > 0)
            {
                Client.RequestLocationScoutsWhenReady(_skillTreeLocationIds.Values);
                AddLog("Queued scouts for " + _skillTreeLocationIds.Count + " skill tree slots.");
            }

            _shopPriceMode = info.SlotData?["shop_price_mode"]?.ToString() ?? "vanilla";
            _effectiveShopPrices.Clear();
            if (info.SlotData?["shop_prices"] is JObject shopPrices)
            {
                foreach (var prop in shopPrices.Properties())
                {
                    if (int.TryParse(prop.Value.ToString(), out var price))
                        _effectiveShopPrices[prop.Name] = price;
                }
                if (_effectiveShopPrices.Count > 0)
                    AddLog("Shop prices loaded (" + _effectiveShopPrices.Count + " entries, mode=" + _shopPriceMode + ").");
            }

            _skillPriceMode = info.SlotData?["skill_price_mode"]?.ToString() ?? "vanilla";
            _effectiveSkillPrices.Clear();
            if (info.SlotData?["skill_prices"] is JObject skillPrices)
            {
                foreach (var prop in skillPrices.Properties())
                {
                    if (int.TryParse(prop.Value.ToString(), out var price))
                        _effectiveSkillPrices[prop.Name] = price;
                }
                if (_effectiveSkillPrices.Count > 0)
                {
                    AddLog("Skill prices loaded (" + _effectiveSkillPrices.Count + " entries, mode=" + _skillPriceMode + ").");
                    foreach (var kv in _effectiveSkillPrices)
                    {
                        AddLog($"  skill price: '{kv.Key}' = {kv.Value}");
                        break;
                    }
                }
            }

            _startingShopGroups = info.SlotData?["starting_shop_groups"]?.Value<int>() ?? 0;

            _fillerDustValues.Clear();
            _fillerIconNames.Clear();
            if (info.SlotData?["filler_items"] is JArray fillerArr)
            {
                foreach (var entry in fillerArr)
                {
                    var name = entry["name"]?.ToString();
                    var dust = entry["dust"]?.Value<int>() ?? 0;
                    var icon = entry["icon"]?.ToString();
                    if (!string.IsNullOrEmpty(name))
                    {
                        _fillerDustValues[name] = dust;
                        if (!string.IsNullOrEmpty(icon))
                            _fillerIconNames[name] = icon;
                    }
                }
                if (_fillerDustValues.Count > 0)
                    AddLog("Filler items loaded (" + _fillerDustValues.Count + " entries).");
            }

            _shopProgressionEnabled = info.SlotData?["progressive_shop"]?.Value<bool>() ?? false;
            _shopGroupByTalisman.Clear();
            if (_shopProgressionEnabled && info.SlotData?["shop_groups"] is JArray shopGroupsArr)
            {
                for (int i = 0; i < shopGroupsArr.Count; i++)
                {
                    if (shopGroupsArr[i] is JArray group)
                    {
                        foreach (var t in group)
                        {
                            var id = t?.ToString();
                            if (!string.IsNullOrEmpty(id))
                                _shopGroupByTalisman[id] = i;
                        }
                    }
                }
                AddLog("Shop progression enabled: " + shopGroupsArr.Count + " groups.");
                AddLog($"Progressive shop: {_shopProgressionEnabled}, starting groups: {_startingShopGroups}");
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

            _bossSlotInfoByLevel.Clear();
            if (info.SlotData?["bosses"] is JArray bossesArr)
            {
                foreach (var bossToken in bossesArr)
                {
                    var level = bossToken["level"]?.ToString();
                    if (string.IsNullOrEmpty(level)) continue;
                    var sigil = bossToken["sigil"]?.ToString();   // null when starting
                    var start = bossToken["start"]?.Value<bool>() ?? false;
                    _bossSlotInfoByLevel[level] = (sigil, start);
                }
                AddLog("Boss slot info loaded (" + _bossSlotInfoByLevel.Count + " entries).");
            }

            var rankFromSlot = info.SlotData?["required_rank"]?.Value<int>() ?? 0;
            _requiredRankIndex = rankFromSlot;
            AddLog("Required rank index: " + _requiredRankIndex);

            var colorFromSlot = info.SlotData?["player_color"]?.ToString();
            if (!string.IsNullOrEmpty(colorFromSlot))
            {
                _playerColorKey = colorFromSlot;
                AddLog("Player color: " + _playerColorKey);
            }

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

            _deathsSinceLastSend = 0;
            _lastKnownPlayerHp = -1;
            _playerDeathFired = false;
            _playerKillMethod = null;
            _seedHasDeathLink = info.SlotData?["death_link"]?.Value<bool>() ?? false;
            _deathLinkAmnesty = info.SlotData?["death_link_amnesty"]?.Value<int>() ?? 3;

            if (_seedHasDeathLink && RubiniteAPPlugin.Instance.Settings.DeathLinkEnabled.Value)
                AddLog("DeathLink enabled (amnesty=" + _deathLinkAmnesty + ").");
            else if (_seedHasDeathLink)
                AddLog("DeathLink: seed has it, but client setting is off.");

            StatusMessage = "Connected as " + slot + (string.IsNullOrEmpty(info.SeedName) ? "" : " (" + info.SeedName + ")");
            AddLog(StatusMessage);

            _mySlot = info.Slot;
            _playerNames.Clear();
            foreach (var kv in info.Players)
                _playerNames[kv.Key] = kv.Value;

            if (_playerNames.Count > 1)
                AddLog("Player names loaded (" + _playerNames.Count + " players).");

            if (State.Checked.Count > 0)
                Client.SendLocationChecks(State.Checked);
            if (State.GoalSent)
                Client.SendGoal();

            if (ApRunActive)
                ApplyRuntime(_needsFullLock);

            _needsFullLock = false;
        }

        private void OnLocationScouted(long locationId, long itemId, int playerId, string serverName, int flags)
        {
            // Already handled by the shop dictionary lookup, because
            // _scoutedShopItems[title] is populated for ANY title that matches a
            // location ID, and skill tree locations use the same title key format.
            // Just make sure the location ID loop covers skill tree IDs too:
            string title = null;
            foreach (var kv in _shopLocationIds)
                if (kv.Value == locationId) { title = kv.Key; break; }
            if (title == null)
                foreach (var kv in _skillTreeLocationIds)
                    if (kv.Value == locationId) { title = kv.Key; break; }
            if (title == null) return;

            string display;
            if (_itemNames.TryGetValue(itemId, out var local) && !string.IsNullOrEmpty(local))
                display = local;
            else if (!string.IsNullOrEmpty(serverName))
                display = serverName;
            else
            {
                var resolved = Client.LookupCrossGameItemName(playerId, itemId);
                display = !string.IsNullOrEmpty(resolved) ? resolved : "Item #" + itemId;
            }

            bool isCrossGame = playerId != _mySlot;
            if (isCrossGame && _playerNames.TryGetValue(playerId, out var otherName))
                display = otherName + "'s " + display;

            if (isCrossGame)
                _crossGameScoutedTitles.Add(title);
            else
                _crossGameScoutedTitles.Remove(title);

            _scoutedShopItems[title] = display;
            _scoutedShopFlags[title] = flags;
            // TODO AddLog("Scouted: '" + title + "' -> '" + display + "'");
        }

        public bool TryGetScoutedShopItem(string talismanTitle, out string itemName)
            => _scoutedShopItems.TryGetValue(talismanTitle, out itemName);
        
        public bool TryGetScoutedSkillTreeItem(string treeDisplayId, out string itemName)
        {
            if (!GameData.SkillTitles.TryGetValue(treeDisplayId, out var title))
            {
                itemName = null;
                return false;
            }
            var key = "Skill Tree: " + treeDisplayId.Replace('_', '-') + ": " + title;
            return _scoutedShopItems.TryGetValue(key, out itemName);
        }

        /// <summary>
        /// Returns the price the shop should charge for this slot.
        /// Randomized mode: pre-computed price from slot_data.
        /// Vanilla mode: the talisman's own costDust.
        /// </summary>
        public int GetEffectiveShopPrice(TalismanData talismanData)
        {
            if (talismanData == null) return 0;
            if (_shopPriceMode == "randomized"
                && !string.IsNullOrEmpty(talismanData.DisplayName)
                && _effectiveShopPrices.TryGetValue(talismanData.DisplayName, out var price))
            {
                return price;
            }
            return talismanData.costDust;
        }

        public bool TryGetEffectiveSkillPrice(string treeId, out int price)
        {
            price = 0;
            if (_skillPriceMode != "randomized" || string.IsNullOrEmpty(treeId))
                return false;

            // slot_data keys use hyphens ("2-1"); SkillTreeData.DisplayName uses
            // underscores ("2_1"). Normalize before lookup.
            var normalized = treeId.Replace('_', '-');
            return _effectiveSkillPrices.TryGetValue(normalized, out price);
        }

        public bool IsCrossGameShopItem(string talismanTitle)
            => _crossGameScoutedTitles.Contains(talismanTitle);

        /// <summary>
        /// Resolves title / description / icon for an AP item that sits in a shop slot.
        /// Talismans inherit the real TalismanData text; everything else gets a short
        /// generic Archipelago text appropriate for its kind.
        /// </summary>
        public bool TryGetShopDescription(string talismanTitle, string apItemName, out string title, out string description, out Sprite icon, out int notchCount)
        {
            title = apItemName;
            description = null;
            icon = null;
            notchCount = 0;

            if (string.IsNullOrEmpty(apItemName)) return false;

            // Cross-game item → generic text classified by AP item flags
            if (_crossGameScoutedTitles.Contains(talismanTitle))
            {
                int flags = _scoutedShopFlags.TryGetValue(talismanTitle, out var f) ? f : 0;
                string kind;
                if ((flags & 1) != 0)      kind = "a progression item that another player needs to finish their game";
                else if ((flags & 2) != 0) kind = "a useful item for another player";
                else if ((flags & 4) != 0) kind = "a trap that may hinder another player";
                else                       kind = "a filler item for another player";

                description = $"An item from another world — {kind}.";
                icon = PickIconForItem(apItemName, isCrossGame: true);
                return true;
            }

            if (apItemName.StartsWith("Talisman: "))
            {
                var overview = SingletonScriptableObject<TalismanDataOverview>.Instance;
                if (overview != null)
                {
                    foreach (var tal in overview.GetAllData())
                    {
                        if (tal == null || string.IsNullOrEmpty(tal.DisplayName)) continue;
                        if (_talismanItemByDataId.TryGetValue(tal.DisplayName, out var mapped)
                            && mapped == apItemName)
                        {
                            title = tal.titleTerm != null ? tal.titleTerm.GetSentence() : apItemName;
                            description = tal.detailTerm != null ? tal.detailTerm.GetSentence() : "";
                            icon = tal.Icon;
                            notchCount = tal.expendNotche;
                            return true;
                        }
                    }
                }
                description = "A talisman. Equip it to gain its effect.";
                icon = PickIconForItem(apItemName);
                return true;
            }

            if (apItemName.EndsWith(" Sigil"))
            {
                description = "A boss sigil from another world. "
                            + "It unlocks a boss portal in the Challenge Area.";
                icon = PickIconForItem(apItemName);
                return true;
            }

            if (_fillerDustValues.TryGetValue(apItemName, out int fillerDust))
            {
                description = $"A filler item from another world. "
                            + $"It grants {fillerDust} Blood Dust.";
                icon = PickIconForItem(apItemName);
                return true;
            }

            if (apItemName == "Shop Expansion")
            {
                description = "A shop expansion from another world. "
                            + "It unlocks a new group of Shop Slots at Wichiwi's Shop.";
                icon = PickIconForItem(apItemName);
                return true;
            }

            if (apItemName.StartsWith("Skill "))
            {
                int colonIdx = apItemName.IndexOf(": ");
                title = colonIdx > 0 ? apItemName.Substring(colonIdx + 2) : apItemName;

                var skillDesc = LookupSkillDescription(apItemName);
                description = !string.IsNullOrEmpty(skillDesc)
                    ? skillDesc
                    : "A skill-tree upgrade. Received via Archipelago.";

                icon = PickIconForItem(apItemName);
                return true;
            }

            description = "An item from another world.";
            icon = PickIconForItem(apItemName);
            return true;
        }

        private bool IsShopGroupUnlocked(string talismanDisplayName)
        {
            if (!_shopProgressionEnabled) return true;
            if (!_shopGroupByTalisman.TryGetValue(talismanDisplayName, out int groupIndex))
                return true;

            int expansions = 0;
            foreach (var item in State.Items)
                if (item == "Shop Expansion") expansions++;

            // Free groups = starting groups + received expansions
            return groupIndex < (_startingShopGroups + expansions);
        }

        private void OnItemsReceived(int index, List<NetworkItem> items)
        {
            var fullInventory = index == 0;
            // Remember how many items were already present before we reset.
            // On a full inventory replay (index=0), everything below this position
            // was already processed in a previous session and its dust is already
            // included in State.LastKnownDust.
            var oldLastIndex = State.LastItemIndex;

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

                if (name.StartsWith("Skill "))
                    AddLog($"Skill item received: '{name}' (HasItem={State.HasItem(name)})");

                // Only credit dust for items that are genuinely new since the last
                // session. Items below oldLastIndex were already counted when they
                // first arrived; re-adding them on full inventory replay would
                // double-count (e.g. +1500 for every Large Crystal the player has).
                bool isNewItem = !fullInventory || position >= oldLastIndex;

                if (isNewItem && _fillerDustValues.TryGetValue(name, out int dustValue))
                {
                    if (ApRunActive && DataSaver.currentSaveNumber == GameData.ChallengeSaveNumber
                        && GameSetting.Instance?.dustFlag != null)
                    {
                        int currentDust = GameSetting.Instance.dustFlag.GetValue();
                        GameSetting.Instance.dustFlag.SetValue(currentDust + dustValue, "RubiniteAP");
                        State.LastKnownDust = currentDust + dustValue;
                        AddLog($"Dust via item: +{dustValue} → {State.LastKnownDust} (in-run)");
                        try { DataSaver.SaveFlag(DataSaver.path_saveFolder); }
                        catch (Exception ex) { AddLog("Could not persist dust after item: " + ex.Message); }
                    }
                    else
                    {
                        // Not in an AP run — game's dust may belong to a different save.
                        // Accumulate in State only; DoStartRun applies it when the run starts.
                        State.LastKnownDust += dustValue;
                        AddLog($"Dust via item: +{dustValue} → {State.LastKnownDust} (queued, not in run)");
                    }
                    State.Save();
                }
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
            if (string.IsNullOrEmpty(message)) return;
            _messageCount++;
            var formatted = DateTime.Now.ToString("HH:mm:ss") + "  " + message;
            LogLines.Add(formatted);
            if (LogLines.Count > 200) LogLines.RemoveRange(0, LogLines.Count - 200);
            LogChanged?.Invoke();
            _logQueue.Enqueue(formatted);
        }

        private void FlushLogQueue()
        {
            if (_logQueue.IsEmpty) return;
            var sb = new System.Text.StringBuilder();
            while (_logQueue.TryDequeue(out var line))
                sb.AppendLine(line);
            try
            {
                var path = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "RubiniteAP_Log.txt");
                System.IO.File.AppendAllText(path, sb.ToString());
            }
            catch { }
        }

        private void ShowItemToast(string name)
        {
            // A lightweight in-game notice: the overlay keeps the full history.
            StatusMessage = "Received " + name;
        }

        public void Tick()
        {
            FlushLogQueue();

            if (_startingRun)
                return;

            var sceneName = SceneManager.GetActiveScene().name;
            
            if (ApRunActive && DeathLinkActive && sceneName != "TitleMenu")
                PollPlayerHpForDeathLink();

            bool wasActive = ApRunActive;

            if (ApRunActive && DataSaver.currentSaveNumber != GameData.ChallengeSaveNumber)
                ApRunActive = false;

            if (ApRunActive && sceneName == "TitleMenu")
            {
                ApRunActive = false;
                _lastLoadedLevel = null;
                _lastPortalLevel = null;
                _podiumClickPending = null;
                AddLog("Run ended (returned to the title screen).");
            }

            if (wasActive && !ApRunActive)
            {
                RestoreChallengeSave();
            }

            // --- AP Shop Terminal Interaktion & Text-Sichtbarkeit ---
            if (ApRunActive && sceneName == ChallengeAreaScene)
            {
                // --- FORCE CLEAR UNEARNED TALISMANS ---
                var core = GetGamePlayCore();
                if (core != null && core.equippedTalismanList != null && core.equippedTalismanList.Count > 0)
                {
                    for (int i = core.equippedTalismanList.Count - 1; i >= 0; i--)
                    {
                        var talisman = core.equippedTalismanList[i];
                        if (talisman != null && talisman.talismanData != null)
                        {
                            // If the player hasn't received this talisman via AP, remove it from the equipped list
                            if (!IsReceivedTalisman(talisman.talismanData))
                            {
                                talisman.talismanData.isEquipped?.SetValue(0);
                                core.equippedTalismanList.RemoveAt(i);
                            }
                        }
                    }
                }

                // Re-spawn the shop whenever the CA was reloaded (e.g. after a boss fight)
                if (GameObject.Find("AP_Shop_Terminal") == null)
                {
                    SpawnShopObject();
                    AddLog("Re-spawned Wichiwi AP Shop Terminal after scene reload.");
                }


                var shopObj = GameObject.Find("AP_Shop_Terminal");
                if (shopObj != null)
                {
                    var player = GetGamePlayCore()?.player;
                    if (player != null)
                    {
                        var uiGroup = shopObj.transform.Find("AP_Shop_UI")?.gameObject;
                        
                        // Use the interaction point instead of the shop origin
                        var interactPoint = shopObj.transform.Find("AP_Shop_InteractPoint");
                        Vector3 targetPos = interactPoint != null ? interactPoint.position : shopObj.transform.position;
                        float dist = Vector2.Distance(player.transform.position, targetPos);

                        bool isNear = dist < 1.75f;

                        if (uiGroup != null && uiGroup.activeSelf != isNear)
                        {
                            uiGroup.SetActive(isNear);
                        }

                        if (isNear && MenuCore.Instance.currentMenu == null)
                        {
                            if (Input.GetKeyDown(KeyCode.E))
                            {                                
                                var menus = Resources.FindObjectsOfTypeAll<CreateItemMenu>();
                                if (menus != null && menus.Length > 0)
                                {
                                    menus[0].OpenMenu();
                                    AddLog("Opened Wichiwi Shop via Terminal.");
                                }
                            }
                        }
                    }
                }
            }

            // --- F12 DEBUG: Scan all UI sprites in the scene ---
            // TODO Debugging, delete later
            if (Input.GetKeyDown(KeyCode.F12))
            {
                AddLog("=== SCANNING UI ASSETS ===");

                // (a) Normal Scan
                var sprites = Resources.FindObjectsOfTypeAll<SpriteRenderer>();
                foreach (var sr in sprites)
                {
                    if (sr == null || sr.sprite == null || !sr.gameObject.scene.IsValid()) continue;
                    if (!sr.gameObject.activeInHierarchy) continue;
                    AddLog($"SR: GameObject='{sr.gameObject.name}', Sprite='{sr.sprite.name}'");
                }

                var images = Resources.FindObjectsOfTypeAll<UnityEngine.UI.Image>();
                foreach (var img in images)
                {
                    if (img == null || img.sprite == null || !img.gameObject.scene.IsValid()) continue;
                    if (!img.gameObject.activeInHierarchy) continue;
                    AddLog($"UI.Image: GameObject='{img.gameObject.name}', Sprite='{img.sprite.name}'");
                }

                var tmps = Resources.FindObjectsOfTypeAll<TMPro.TextMeshProUGUI>();
                foreach (var tmp in tmps)
                {
                    if (tmp == null || !tmp.gameObject.scene.IsValid()) continue;
                    if (!tmp.gameObject.activeInHierarchy) continue;
                    AddLog($"TMP: GameObject='{tmp.gameObject.name}', Text='{tmp.text}'");
                }

                // (b) Searches for all BossStele_* sprites
                AddLog("--- BossStele sprites ---");
                var seenBoss = new HashSet<string>();
                foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
                {
                    if (s == null || string.IsNullOrEmpty(s.name)) continue;
                    if (s.name.StartsWith("BossStele_") && seenBoss.Add(s.name))
                        AddLog($"SPRITE: '{s.name}' ({s.rect.width}x{s.rect.height})");
                }

                // (c) UI_Crystal_* (Blood-Dust-Icons)
                AddLog("--- UI_Crystal sprites ---");
                var seenCrystal = new HashSet<string>();
                foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
                {
                    if (s == null || string.IsNullOrEmpty(s.name)) continue;
                    if (s.name.StartsWith("UI_Crystal_") && seenCrystal.Add(s.name))
                        AddLog($"SPRITE: '{s.name}'");
                }

                AddLog("--- SkillTreeData structure (full) ---");
                // (d)
                var skOver = SingletonScriptableObject<SkillTreeDataOverview>.Instance;
                if (skOver != null && skOver.GetAllData() != null)
                {
                    SkillTreeData sample = null;
                    foreach (var t in skOver.GetAllData()) { if (t != null) { sample = t; break; } }
                    if (sample != null)
                    {
                        var flags = System.Reflection.BindingFlags.Public
                                | System.Reflection.BindingFlags.NonPublic
                                | System.Reflection.BindingFlags.Instance
                                | System.Reflection.BindingFlags.FlattenHierarchy;

                        AddLog($"SKILL SAMPLE: DisplayName='{sample.DisplayName}'");
                        AddLog("--- TREE FIELDS ---");
                        foreach (var f in sample.GetType().GetFields(flags))
                        {
                            object val = null;
                            try { val = f.GetValue(sample); } catch { }
                            string typeName = f.FieldType.Name;
                            if (val == null) { AddLog($"  F.{f.Name} ({typeName}) = null"); continue; }
                            if (val is Sprite sp) AddLog($"  F.{f.Name} ({typeName}) = Sprite('{sp.name}')");
                            else if (val is UnityEngine.Object uo) AddLog($"  F.{f.Name} ({typeName}) = '{uo.name}'");
                            else AddLog($"  F.{f.Name} ({typeName}) = {val}");
                        }
                        AddLog("--- TREE PROPERTIES ---");
                        // NEW: recursive scan of skillName and secondRunSkillName
                        foreach (var fieldName in new[] { "skillName", "secondRunSkillName" })
                        {
                            var sn = GetFieldOrProperty(sample, fieldName);
                            if (sn == null)
                            {
                                AddLog($"--- {fieldName}: null ---");
                                continue;
                            }

                            AddLog($"--- {fieldName} ({sn.GetType().Name}) ---");
                            var snFlags = System.Reflection.BindingFlags.Public
                                    | System.Reflection.BindingFlags.NonPublic
                                    | System.Reflection.BindingFlags.Instance
                                    | System.Reflection.BindingFlags.FlattenHierarchy;

                            foreach (var f in sn.GetType().GetFields(snFlags))
                            {
                                object val = null;
                                try { val = f.GetValue(sn); } catch { }
                                if (val == null) { AddLog($"  {fieldName}.F.{f.Name} ({f.FieldType.Name}) = null"); continue; }
                                if (val is UnityEngine.Object uo) AddLog($"  {fieldName}.F.{f.Name} ({f.FieldType.Name}) = '{uo.name}'");
                                else AddLog($"  {fieldName}.F.{f.Name} ({f.FieldType.Name}) = {val}");
                            }
                            foreach (var p in sn.GetType().GetProperties(snFlags))
                            {
                                if (p.GetIndexParameters().Length > 0) continue;
                                object val = null;
                                try { val = p.GetValue(sn); } catch { }
                                if (val == null) continue;
                                if (val is UnityEngine.Object uo) AddLog($"  {fieldName}.P.{p.Name} ({p.PropertyType.Name}) = '{uo.name}'");
                                else AddLog($"  {fieldName}.P.{p.Name} ({p.PropertyType.Name}) = {val}");
                            }
                        }
                    }
                }

                // (e) Skill tree icons — finds every sprite whose name looks like a skill icon
                AddLog("--- SkillFrame → Icon mapping ---");
                var seenMap = new HashSet<string>();
                foreach (var img in Resources.FindObjectsOfTypeAll<UnityEngine.UI.Image>())
                {
                    if (img == null || !img.gameObject.scene.IsValid()) continue;
                    if (img.gameObject.name != "Icon") continue;
                    if (img.sprite == null) continue;
                    if (!img.sprite.name.StartsWith("SkillsTreeIcon_")) continue;
                    if (img.sprite.name.StartsWith("SkillsTreeIcon_2_")) continue;
                    if (img.sprite.name.StartsWith("SkillsTreeIcon_frame_")) continue;
                    if (img.sprite.name.StartsWith("SkillsTreeIcon_big_")) continue;

                    // Walk up the full chain, building "RootFrame > ... > IconMask > Icon"
                    var chain = new List<string>();
                    var t = img.transform;
                    while (t != null)
                    {
                        chain.Insert(0, t.name);
                        t = t.parent;
                    }
                    var path = string.Join(" > ", chain);
                    var key = img.sprite.name;
                    if (seenMap.Add(key + "|" + path))
                    {
                        AddLog($"SKILLMAP: '{path}' = '{img.sprite.name}'");
                    }
                }

                AddLog("--- Skill Flags Live State ---");
                var sko = SingletonScriptableObject<SkillTreeDataOverview>.Instance;
                if (sko != null)
                {
                    foreach (var t in sko.GetAllData())
                    {
                        if (t == null || t.skillTreeDataDetail == null) continue;
                        var u = t.skillTreeDataDetail.isUnlocked?.GetValue() ?? false;
                        var k = t.skillTreeDataDetail.isKnown?.GetValue() ?? false;
                        var u2 = t.secondRunSkillTreeDataDetail?.isUnlocked?.GetValue() ?? false;
                        var k2 = t.secondRunSkillTreeDataDetail?.isKnown?.GetValue() ?? false;
                        AddLog($"SKILLFLAG: {t.DisplayName} u1={u} k1={k} u2={u2} k2={k2}");
                    }
                }

                AddLog("=== SCAN COMPLETE ===");
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
                var isContinuation = State.ChallengeSaveInitialized;

                if (!isContinuation)
                {
                    // Fresh run: rebuild the challenge save from normal saves.
                    BackupChallengeSave();
                    DataSaver.currentSaveNumber = GameData.ChallengeSaveNumber;
                    ChallengeSave.RefreshFromAllNormalSaves();
                    State.ChallengeSaveInitialized = true;
                    State.Save();
                    _flagsInitializedThisRun = false;
                    AddLog("Starting a new AP run (challenge save rebuilt, dust reset).");
                }
                else
                {
                    // Continuation: preserve the challenge save from the previous session.
                    DataSaver.currentSaveNumber = GameData.ChallengeSaveNumber;
                    BackupChallengeSave();

                    _flagsInitializedThisRun = true;
                    AddLog("Continuing existing AP run (challenge save preserved, dust kept).");
                }

                ApRunActive = true;
                _needsFullLock = true;

                DataSaver.LoadFlag();

                if (GameSetting.Instance?.dustFlag != null)
                {
                    int target = State.LastKnownDust;
                    GameSetting.Instance.dustFlag.SetValue(target, "RubiniteAP");
                    AddLog($"Dust after DoStartRun LoadFlag: set to {target} from ApState");
                }

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
                    SpawnShopObject();
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

        private static void RestoreChallengeSave()
        {
            var versionFolder = DataSaver.path_versionFolder;
            var source = IOPath.Combine(versionFolder, "ChallengeSave_APBackup");
            var destination = IOPath.Combine(versionFolder, DataSaver.CHALLENGE_SAVE_FOLDER);
            
            if (Directory.Exists(source))
            {
                try
                {
                    if (Directory.Exists(destination))
                        Directory.Delete(destination, true);
                    CopyDirectory(source, destination);
                    Directory.Delete(source, true);
                    RubiniteAPPlugin.Instance?.Bridge?.AddLog("Vanilla Challenge Save restored.");
                }
                catch (Exception ex)
                {
                    RubiniteAPPlugin.Instance?.Bridge?.AddLog("Failed to restore Challenge Save: " + ex.Message);
                }
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

        public bool IsSkillReceived(string treeDisplayId)
        {
            if (string.IsNullOrEmpty(treeDisplayId)) return false;
            return _skillItemByTreeId.TryGetValue(treeDisplayId, out var itemName)
                && State.HasItem(itemName);
        }

        /// <summary>
        /// True if the AP location check for this skill has already been sent,
        /// meaning the player has already paid the dust cost at least once.
        /// Used to keep the skill frame visible ("known") after purchase even
        /// if the item hasn't arrived yet, and to prevent double purchases.
        /// </summary>
        public bool IsSkillPurchased(string treeDisplayId)
        {
            if (string.IsNullOrEmpty(treeDisplayId)) return false;
            if (!GameData.SkillTitles.TryGetValue(treeDisplayId, out var title)) return false;
            var locName = "Skill Tree: " + treeDisplayId.Replace('_', '-') + ": " + title;
            return _skillTreeLocationIds.TryGetValue(locName, out var locId) && State.HasChecked(locId);
        }

        /// <summary>Write item ownership into game flags, without touching instantiated skills.</summary>
        private void ApplyToFlags(bool lockUnreceived)
        {
            if (_startingRun) return;
            var talismanOverview = SingletonScriptableObject<TalismanDataOverview>.Instance;
            if (talismanOverview != null)
            {
                foreach (var talisman in talismanOverview.GetAllData())
                {
                    if (talisman == null || talisman.state == null)
                        continue;

                    // Filter out placeholder items so they never appear in the shop
                    string displayName = talisman.DisplayName;
                    if (string.IsNullOrEmpty(displayName)
                        || displayName.Contains("Exhibition")
                        || displayName.Contains("TalismanData_")
                        || displayName == "X19_Shield"
                        || displayName == "無")
                        continue;

                    bool received = IsReceivedTalisman(talisman);
                    bool isPurchased = _shopLocationIds.TryGetValue(displayName, out long locId)
                                    && State.HasChecked(locId);
                    bool groupUnlocked = IsShopGroupUnlocked(displayName);

                    // Ownership (state) — can the player equip this?
                    if (received)
                        talisman.state.SetValue(AchievementState.Unlock);
                    else if (lockUnreceived)
                        talisman.state.SetValue(AchievementState.Lock);

                    // Shop visibility (storeState)
                    // - Purchased → hide (already own it)
                    // - Group locked → hide (Shop Expansion not received yet)
                    // - Received → show as owned
                    // - Otherwise → show as buyable
                    if (isPurchased || !groupUnlocked)
                        talisman.storeState?.SetValue(AchievementState.Lock);
                    else if (received)
                        talisman.storeState?.SetValue(AchievementState.Unlock);
                    else
                        talisman.storeState?.SetValue(AchievementState.ReadyToUnlock);

                    // Only keep isEquipped=1 if the talisman is received AND actually
                    // lives in the runtime equipped list. Otherwise the UI will falsely
                    // show it as equipped.
                    bool inRuntime = false;
                    if (received)
                    {
                        var core = GetGamePlayCore();
                        if (core?.equippedTalismanList != null)
                        {
                            foreach (var t in core.equippedTalismanList)
                            {
                                if (t?.talismanData == talisman) { inRuntime = true; break; }
                            }
                        }
                    }
                    if (!received || !inRuntime)
                        talisman.isEquipped?.SetValue(0);
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

                    bool received = IsReceivedSkill(tree);

                    if (GameData.IsFreeSkill(tree.DisplayName))
                    {
                        SetSkillFlags(tree, true, true);
                        continue;
                    }

                    // Strictly vanilla: isUnlocked is only true if the player
                    // actually received the skill item from AP.
                    SetSkillFlags(tree, received, true);
                }
            }
        }

        /// <summary>
        /// Applies skill-tree visibility flags. isUnlocked governs whether the skill
        /// is actually usable; isKnown governs whether the slot is visible at all.
        /// Purchased-but-not-yet-received skills should be known=true, unlocked=false.
        /// </summary>
        private static void SetSkillFlags(SkillTreeData tree, bool unlocked, bool known)
        {
            var detail = tree.skillTreeDataDetail;
            if (detail?.isUnlocked != null)
                detail.isUnlocked.SetValue(unlocked);
            if (detail?.isKnown != null)
                detail.isKnown.SetValue(known);

            var second = tree.secondRunSkillTreeDataDetail;
            if (second?.isUnlocked != null)
                second.isUnlocked.SetValue(unlocked);
            if (second?.isKnown != null)
                second.isKnown.SetValue(known);
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

                    AddLog($"SetSkill: {tree.DisplayName} (received={IsReceivedSkill(tree)})");
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
        }

        private bool IsBossUnlocked(BossInfo boss, string levelName)
        {
            if (boss == null)
                return true;

            // slot_data is the source of truth for version 2+ seeds.
            // We look up by level scene name, which is stable even if boss keys change.
            if (!string.IsNullOrEmpty(levelName) && _bossSlotInfoByLevel.TryGetValue(levelName, out var info))
            {
                if (info.start)
                    return true;
                return !string.IsNullOrEmpty(info.sigil) && State.HasItem(info.sigil);
            }

            // If slot_data is missing a boss entry, treat it as locked to avoid
            // accidentally unlocking progression. This should never happen with
            // version 2 seeds, but failing closed is safer than failing open.
            AddLog("WARNING: No slot_data entry for level '" + levelName + "'; treating as locked.");
            return false;
        }

        /// <summary>Called from GamePlayCore.SetUp before flags are read into the save.</summary>
        public void PrepareSaveFlags()
        {
            if (!ApRunActive || DataSaver.currentSaveNumber != GameData.ChallengeSaveNumber)
                return;
            try
            {
                // Only reload flags from disk on the very first SetUp of a run.
                // Doing it on every level load wipes dust the game accumulates
                // during boss fights (dust is stored in GameSetting.dustFlag
                // in-memory and only persisted on our explicit SaveFlag calls).
                if (!_flagsInitializedThisRun)
                {
                    DataSaver.LoadFlag();

                    if (GameSetting.Instance?.dustFlag != null)
                    {
                        int target = State.LastKnownDust + State.PendingDust;
                        GameSetting.Instance.dustFlag.SetValue(target, "RubiniteAP");
                        AddLog($"Dust restored from ApState: {target} (last={State.LastKnownDust}, pending={State.PendingDust})");
                        State.LastKnownDust = target;
                        State.PendingDust = 0;
                        State.Save();
                    }

                    _flagsInitializedThisRun = true;
                }

                AddLog($"Dust in PrepareSaveFlags: current={GameSetting.Instance?.dustFlag?.GetValue()} pending={State.PendingDust} _flagsInitializedThisRun={_flagsInitializedThisRun}");

                if (GameSetting.Instance != null)
                {
                    // Apply the chosen AP Color
                    string skinId = GetSkinIdForColor(_playerColorKey);
                    if (GameSetting.Instance.rubySkinID != null)
                        GameSetting.Instance.rubySkinID.SetValue(skinId, "RubiniteAP");
                        
                    // This directly updates the player's visual appearance
                    if (SingletonScriptableObject<ItemDataOverview>.Instance != null)
                        SingletonScriptableObject<ItemDataOverview>.Instance.currentSkinIDFlag.SetValue(skinId, "RubiniteAP");
                }

                // Clear equipped talismans on start
                var core = GetGamePlayCore();
                if (core != null && core.equippedTalismanList != null)
                {
                    core.equippedTalismanList.Clear();
                }

                // Clear isEquipped flags on ALL talismans at run start, so stale flags
                // from previous runs don't leak through
                var talOverview = SingletonScriptableObject<TalismanDataOverview>.Instance;
                if (talOverview != null)
                {
                    foreach (var tal in talOverview.GetAllData())
                    {
                        if (tal == null) continue;
                        if (string.IsNullOrEmpty(tal.DisplayName)) continue;
                        if (tal.DisplayName.Contains("Exhibition")) continue;
                        if (tal.DisplayName.Contains("TalismanData_")) continue;
                        if (tal.DisplayName == "X19_Shield" || tal.DisplayName == "無") continue;
                        tal.isEquipped?.SetValue(0);
                    }
                }

                ApplyToFlags(true);
                DataSaver.SaveFlag(DataSaver.path_saveFolder);
            }
            catch (Exception ex)
            {
                AddLog("Could not prepare save flags: " + ex.Message);
            }
        }

        public void NotifyPodiumClick(string levelName)
        {
            if (string.IsNullOrEmpty(levelName)) return;
            AddLog($"Podium click: '{levelName}' (wasPending='{_podiumClickPending}', lastLoaded='{_lastLoadedLevel}')");
            _podiumClickPending = levelName;
        }

        public void ClearPendingPodiumClick()
        {
            if (!string.IsNullOrEmpty(_podiumClickPending))
            {
                AddLog("Podium click blocked: '" + _podiumClickPending + "'");
                _podiumClickPending = null;
            }
        }

        public bool ShouldBlockLoad(string levelName)
        {
            if (!ApRunActive || DataSaver.currentSaveNumber != GameData.ChallengeSaveNumber)
                return false;

            // Not a podium click, and loading the current scene → retry. Allow without re-check.
            if (string.IsNullOrEmpty(_podiumClickPending)
                && !string.IsNullOrEmpty(_lastLoadedLevel)
                && levelName == _lastLoadedLevel)
            {
                AddLog("Block: retry of '" + levelName + "' — allow");
                return false;
            }

            var boss = GameData.FindByLevel(levelName);
            if (boss == null || IsBossUnlocked(boss, levelName))
                return false;

            StatusMessage = boss.Name + " is locked. Find " + boss.Sigil + ".";
            AddLog("Block: '" + levelName + "' blocked (" + boss.Name + ")");
            return true;
        }

        public bool TryGetMappedLevel(string portalLevel, out string mappedLevel)
        {
            if (_bossSceneMap.TryGetValue(portalLevel, out mappedLevel))
                return !string.IsNullOrEmpty(mappedLevel);
            mappedLevel = portalLevel;
            return false;
        }

        public void RefreshPortals()
        {
            if (!ApRunActive || DataSaver.currentSaveNumber != GameData.ChallengeSaveNumber)
                return;

            if (_bossSceneMap.Count == 0)
            {
                AddLog("WARNING: RefreshPortals called with empty _bossSceneMap. "
                    + "Seed may be an old version without 'boss_scene_map'. No shuffle will be applied.");
            }

            bool loggedAnyMapping = false;
            var points = Resources.FindObjectsOfTypeAll<ChallengeLevelPoint>();
            foreach (var point in points)
            {
                if (point == null || point.gameObject == null || !point.gameObject.scene.IsValid())
                    continue;
                var portalLevel = point.levelName != null ? point.levelName.displayName : null;
                if (string.IsNullOrEmpty(portalLevel))
                    continue;

                string displayLevel = portalLevel;
                if (_bossSceneMap.TryGetValue(portalLevel, out var mapped) && !string.IsNullOrEmpty(mapped))
                    displayLevel = mapped;

                // Log once so we can verify in the log whether mapping fired at all.
                if (displayLevel != portalLevel && !loggedAnyMapping)
                {
                    //AddLog($"RefreshPortals: mapping '{portalLevel}' -> '{displayLevel}'");
                    loggedAnyMapping = true;
                }

                // 1. Visuals from the MAPPED level data.
                var overview = SingletonScriptableObject<LevelDataOverview>.Instance;

                LevelData mappedData = overview?.FindUniqueId(displayLevel);
                if (mappedData == null)
                {
                    // Fallback: match by DisplayName explicitly
                    foreach (var ld in overview.GetAllData())
                    {
                        if (ld != null && ld.DisplayName == displayLevel) { mappedData = ld; break; }
                    }
                }

                if (mappedData == null)
                {
                    AddLog($"RefreshPortals: FAIL - no LevelData for '{displayLevel}'");
                    continue;
                }

                if (point.bossIcon == null)
                {
                    AddLog($"RefreshPortals: FAIL - '{portalLevel}' has null bossIcon");
                    continue;
                }

                var iconSprite = mappedData.bossIcon_ForChallengeArea ?? mappedData.bossIcon;
                if (iconSprite == null)
                {
                    AddLog($"RefreshPortals: FAIL - '{displayLevel}' has no bossIcon_ForChallengeArea/bossIcon");
                    continue;
                }

                string prevName = point.bossIcon.sprite != null ? point.bossIcon.sprite.name : "null";
                point.bossIcon.sprite = iconSprite;
                //AddLog($"RefreshPortals: '{portalLevel}' icon: '{prevName}' -> '{iconSprite.name}'");

                if (point.bossStageImage != null)
                {
                    if (mappedData.differentImageForChallengeArea && mappedData.ScenePic_ForChallengeArea != null)
                        point.bossStageImage.sprite = mappedData.ScenePic_ForChallengeArea;
                    else if (mappedData.MapSelect_ScenePic != null)
                        point.bossStageImage.sprite = mappedData.MapSelect_ScenePic;
                }

                if (point.dustText != null)
                    point.dustText.text = (mappedData.dustFinalStage + mappedData.dustFirstStage).ToString();

                // 2. Lock state from the PORTAL'S own boss requirement.
                //    ShouldBlockLoad uses portalLevel, so this must match it.
                var portalBoss = GameData.FindByLevel(portalLevel);
                if (portalBoss != null)
                {
                    var unlocked = IsBossUnlocked(portalBoss, portalLevel);

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
        }

        private Sprite FindBossSteleSprite(string bossKey)
        {
            var level = LevelForBossKey(bossKey);
            if (string.IsNullOrEmpty(level)) return null;
            return _bossSteleByLevel.TryGetValue(level, out var sprite) ? sprite : null;
        }

        private static SpriteRenderer FindBossSteleRendererInPoint(ChallengeLevelPoint point)
        {
            if (point == null) return null;
            foreach (var sr in point.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr == null || sr.sprite == null) continue;
                if (!sr.sprite.name.StartsWith("BossStele_")) continue;
                if (sr.sprite.name == "BossStele_0") continue;
                return sr;
            }
            return null;
        }

        private static Sprite FindBossIconInPoint(ChallengeLevelPoint point)
            => FindBossSteleRendererInPoint(point)?.sprite;

        private static string SigilToBossKey(string sigilItemName)
        {
            switch (sigilItemName)
            {
                case "Beast Sigil":         return "Beast";
                case "Guardian Sigil":      return "Guardian";
                case "Cult Sigil":          return "Cult";
                case "Gravekeeper Sigil":   return "Gravekeeper";
                case "Plaguebearer Sigil":  return "PlagueRoot";
                case "Nestling Sigil":      return "Nestling";
                case "Faye II Sigil":       return "Guardian2";
                case "Ghost Sigil":         return "Ghost";
                case "Hunter Sigil":        return "Master";
                case "EyesTree Sigil":      return "EyesTree";
                case "Puppet Master Sigil": return "Queen";
                case "Witch Sigil":         return "Witch";
                case "Witch II Sigil":      return "Witch2";
                default:                    return null;
            }
        }

        private static string LevelForBossKey(string bossKey)
        {
            switch (bossKey)
            {
                case "Beast":       return "BeastLevel";
                case "Guardian":    return "GuardianLevel";
                case "Cult":        return "CultLevel";
                case "Gravekeeper": return "PlagueLevel";
                case "PlagueRoot":  return "PlagueRootLevel";
                case "Nestling":    return "NestlingLevel";
                case "Guardian2":   return "GuardianLevel2";
                case "Ghost":       return "GhostLevel";
                case "Master":      return "MasterLevel";
                case "EyesTree":    return "EyesTreeLevel";
                case "Queen":       return "QueenLevel";
                case "Witch":       return "Witch_FirstRunLevel";
                case "Witch2":      return "WitchLevel";
                default:            return null;
            }
        }

        private static Sprite GetSkillIcon(SkillTreeData tree)
        {
            if (tree == null || string.IsNullOrEmpty(tree.DisplayName)) return null;
            var iconName = SkillTreeIconName(tree.DisplayName);
            if (string.IsNullOrEmpty(iconName)) return null;
            return FindSpriteByName(iconName);
        }

        private static string SkillTreeIconName(string treeDisplayName)
        {
            // Mapping derived from F12 scan of the in-game skill tree (CollectMenu).
            // "_s" suffixed frames carried the actual icon; plain frames sometimes
            // held the generic placeholder (SkillsTreeIcon_5). The four Pureblood
            // Heart and three Enhanced Heal entries use that placeholder because the
            // repeated skills share an icon slot in the game UI - if a rerun with all
            // skills unlocked reveals real icons we can replace Icon_5 entries.
            switch (treeDisplayName)
            {
                case "1_1": return "SkillsTreeIcon_1";
                case "1_2": return "SkillsTreeIcon_23";
                case "1_3": return "SkillsTreeIcon_18";
                case "1_4": return "SkillsTreeIcon_8";

                case "2_1": return "SkillsTreeIcon_2";
                case "2_2": return "SkillsTreeIcon_17";
                case "2_3": return "SkillsTreeIcon_9";
                case "2_4": return "SkillsTreeIcon_5";
                case "2_5": return "SkillsTreeIcon_19";
                case "2_6": return "SkillsTreeIcon_16";
                case "2_7": return "SkillsTreeIcon_5";

                case "3_1": return "SkillsTreeIcon_3";
                case "3_2": return "SkillsTreeIcon_21";
                case "3_3": return "SkillsTreeIcon_5";
                case "3_4": return "SkillsTreeIcon_16";
                case "3_5": return "SkillsTreeIcon_5";
                case "3_6": return "SkillsTreeIcon_15";
                case "3_7": return "SkillsTreeIcon_8";

                case "4_1": return "SkillsTreeIcon_5";
                case "4_2": return "SkillsTreeIcon_22";
                case "4_3": return "SkillsTreeIcon_5";
                case "4_4": return "SkillsTreeIcon_20";
                case "4_5": return "SkillsTreeIcon_24";
                case "4_6": return "SkillsTreeIcon_5";

                default: return null;
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
            
            var rankAchieved = false;
            if (manager.levelTimer != null && manager.levelData.gradingScale != null && manager.levelData.gradingScale.Length > 0)
            {
                if (_requiredRankIndex >= manager.levelData.gradingScale.Length)
                {
                    // The player asked for a rank lower than the game's lowest tracked rank (e.g., C when only S, A, B exist).
                    // In this case, simply defeating the boss counts as achieving it.
                    rankAchieved = true;
                    AddLog($"Rank debug: req={_requiredRankIndex} (below game's lowest), auto-passing.");
                }
                else
                {
                    var threshold = manager.levelData.gradingTime + manager.levelData.gradingScale[_requiredRankIndex];
                    rankAchieved = manager.levelTimer.timer <= threshold;
                    AddLog($"Rank debug: req={_requiredRankIndex}, time={manager.levelTimer.timer:F1}, max={threshold:F1}");
                }
            }

            var ids = new List<long> { boss.LocationBase };
            if (noHit && _enabledChecks.Contains("nohit"))
                ids.Add(boss.LocationBase + 1);
            if (noTalisman && _enabledChecks.Contains("notalisman"))
                ids.Add(boss.LocationBase + 2);
            if (flawless && _enabledChecks.Contains("flawless"))
                ids.Add(boss.LocationBase + 3);
            if (rankAchieved && _enabledChecks.Contains("rank"))
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

            // After a win, the next CA visit is a fresh portal pick.
            // Clear retry state so the next LoadLevel gets remapped normally.
            if (!string.IsNullOrEmpty(_lastLoadedLevel))
            {
                AddLog("Boss defeated: cleared retry state for '" + _lastLoadedLevel + "'");
                _lastLoadedLevel = null;
                // _lastPortalLevel stays set: used to reposition the spawn point
                // when we return to the Challenge Area.
            }

            if (ApRunActive && DataSaver.currentSaveNumber == GameData.ChallengeSaveNumber)
            {
                if (GameSetting.Instance?.dustFlag != null)
                    State.LastKnownDust = GameSetting.Instance.dustFlag.GetValue();
                State.Save();

                try { DataSaver.SaveFlag(DataSaver.path_saveFolder); }
                catch (Exception ex) { AddLog("Could not persist dust after boss: " + ex.Message); }
            }
        }

        public static string GetTargetLevel(OnEnableLoadLevel component)
        {
            if (component == null) return null;

            if (!_getLevelNameMethodSearched)
            {
                _getLevelNameMethodSearched = true;
                try
                {
                    _getLevelNameMethod = typeof(OnEnableLoadLevel).GetMethod(
                        "GetLevelName", BindingFlags.Instance | BindingFlags.NonPublic);
                }
                catch { }
            }

            if (_getLevelNameMethod != null)
            {
                try { return (string)_getLevelNameMethod.Invoke(component, null); }
                catch { }
            }
            return component.levelName != null ? component.levelName.displayName : null;
        }

        public string RemapBossScene(string levelName)
        {
            if (string.IsNullOrEmpty(levelName))
                return levelName;

            // Podium click = fresh portal pick. Clear retry state and remap.
            if (!string.IsNullOrEmpty(_podiumClickPending))
            {
                AddLog("Remap (podium click): '" + levelName + "'");
                _podiumClickPending = null;
                _lastLoadedLevel = null;
                _lastPortalLevel = null;
            }
            else if (!string.IsNullOrEmpty(_lastLoadedLevel) && levelName == _lastLoadedLevel)
            {
                AddLog("Remap: retry of '" + levelName + "' — no remap");
                return levelName;
            }

            if (_bossSceneMap.TryGetValue(levelName, out var mapped) && !string.IsNullOrEmpty(mapped))
            {
                _lastPortalLevel = levelName;
                _lastLoadedLevel = mapped;
                AddLog("Remap: '" + levelName + "' -> '" + mapped + "'");
                return mapped;
            }

            AddLog("Remap: '" + levelName + "' has no mapping (pass through)");
            return levelName;
        }

        public IEnumerator TeleportPlayerToPodium(string portalLevel)
        {
            yield return null;

            var core = GetGamePlayCore();
            if (core == null || core.player == null)
            {
                AddLog("Teleport: player not found");
                yield break;
            }

            Vector3 targetPos = Vector3.zero;
            bool found = false;
            foreach (var point in Resources.FindObjectsOfTypeAll<ChallengeLevelPoint>())
            {   
                if (point?.gameObject == null || !point.gameObject.scene.IsValid()) continue;
                if (point.levelName?.displayName == portalLevel)
                {
                    targetPos = point.transform.position;
                    targetPos.y -= 1.2f; 
                    targetPos.z = core.player.transform.position.z; 
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                AddLog("Teleport: podium not found for '" + portalLevel + "'");
                yield break;
            }

            AddLog("Teleport: enforcing position at " + targetPos);

            float timer = 0f;
            while (timer < 1.5f)
            {
                GameSetting.Instance.playerLastScene.SetValue(portalLevel, "RubiniteAP");
                GameSetting.Instance.playerLastLeaveScene.SetValue(portalLevel, "RubiniteAP");

                core = GetGamePlayCore();
                if (core != null && core.player != null)
                {
                    var cc = core.player.GetComponent<CharacterController>();
                    var rb = core.player.GetComponent<Rigidbody2D>();
                    var anim = core.player.GetComponent<Animator>();

                    if (cc != null) cc.enabled = false;
                    
                    // Stop the wake-up animation from dragging the player back
                    if (anim != null && timer < 0.2f) anim.enabled = false; 
                    
                    if (rb != null)
                    {   
                        rb.velocity = Vector2.zero;
                        rb.position = targetPos;
                    }

                    core.player.transform.position = targetPos;

                    if (cc != null) cc.enabled = true;
                    
                    // Re-enable animation after the snap is done
                    if (anim != null && timer >= 0.2f) anim.enabled = true;
                }
                timer += Time.deltaTime;
                yield return null;
            }
            AddLog("Teleport: position lock released");
        }

        private string GetSkinIdForColor(string colorKey)
        {
            switch (colorKey)
            {
                case "red":       return "eb9dd477-a430-48f8-8a7d-f721ea740a08";
                case "blue":      return "9bd13712-7043-4590-bf83-a674fd1f64be";
                case "yellow":    return "4ff920c2-46b1-49ba-abe5-6d37f7670fe9";
                case "green":     return "224c421f-fb1d-4821-9df6-9312dc79b2b5";
                case "purple":    return "9d04bf67-b8e2-4a53-b9f8-5214fd8330e8";
                case "orange":    return "53a00205-a3e3-45bd-94bf-b965465c4116";
                case "black":     return "fdaf67be-43b6-4a87-9968-bffff54c650b";
                case "white":     return "1d096dbb-d1ab-418b-bbb2-f241cd71626b";
                case "pink":      return "5cceebc3-a992-4c26-bf24-484b1a0a86fa";
                case "turquoise": return "1ea4987a-70aa-47b3-b31b-c318079ae59d";
                default:          return "";
            }
        }

        public void HandleShopPurchase(string talismanTitle)
        {
            if (!ApRunActive) return;
            
            AddLog("Shop Purchase Attempt: '" + talismanTitle + "'");

            // Send the location check
            if (_shopLocationIds.TryGetValue(talismanTitle, out long locId))
            {
                var newIds = new List<long> { locId };
                if (!State.HasChecked(locId))
                {
                    State.AddChecked(newIds);
                    State.Save();
                    if (Client.IsConnected) Client.SendLocationChecks(newIds);
                    AddLog("Shop Check Sent: '" + talismanTitle + "' (ID: " + locId + ")");
                }
                else
                {
                    AddLog("Shop Check already checked: '" + talismanTitle + "'");
                }
            }
            else
            {
                AddLog("WARNING: No shop location found for '" + talismanTitle + "'");
            }

            // Re-lock the talisman so it disappears from the shop
            ApplyRuntime(true);

            if (GameSetting.Instance?.dustFlag != null)
                State.LastKnownDust = GameSetting.Instance.dustFlag.GetValue();
            State.Save();
            if (DataSaver.currentSaveNumber == GameData.ChallengeSaveNumber)
            {
                try { DataSaver.SaveFlag(DataSaver.path_saveFolder); }
                catch (Exception ex) { AddLog("Could not persist dust after shop purchase: " + ex.Message); }
            }
        }

        public bool HandleSkillTreePurchase(string treeId, string title, int price)
        {
            if (!ApRunActive) return false;

            var normalizedId = treeId.Replace('_', '-');
            var locName = "Skill Tree: " + normalizedId + ": " + title;

            if (!_skillTreeLocationIds.TryGetValue(locName, out var locId))
            {
                AddLog("WARNING: No skill tree location for '" + locName + "'");
                return false;
            }

            if (State.HasChecked(locId))
            {
                AddLog("Skill tree check already checked: '" + locName + "' (no dust deducted)");
                return true;
            }

            var core = GetGamePlayCore();
            int dustBefore = -1;
            if (core?.dust != null && price > 0)
            {
                dustBefore = core.dust.GetValue();
                core.dust.SetValue(dustBefore - price, "RubiniteAP SkillTreePurchase");
                AddLog($"Skill tree cost: -{price} ({dustBefore} -> {core.dust.GetValue()})");
            }

            if (core?.dust != null && GameSetting.Instance?.dustFlag != null
                && core.dust != GameSetting.Instance.dustFlag)
            {
                AddLog($"WARNING: core.dust != GameSetting.dustFlag (core={core.dust.GetValue()}, setting={GameSetting.Instance.dustFlag.GetValue()}). Syncing to core.");
                GameSetting.Instance.dustFlag.SetValue(core.dust.GetValue(), "RubiniteAP");
            }

            AddLog("Skill tree purchase: '" + locName + "'");

            var ids = new List<long> { locId };
            State.AddChecked(ids);
            State.Save();
            if (Client.IsConnected)
                Client.SendLocationChecks(ids);
            AddLog("Skill tree check sent: '" + locName + "' (ID: " + locId + ")");

            if (GameSetting.Instance?.dustFlag != null)
                State.LastKnownDust = GameSetting.Instance.dustFlag.GetValue();
            State.Save();

            if (DataSaver.currentSaveNumber == GameData.ChallengeSaveNumber)
            {
                try { DataSaver.SaveFlag(DataSaver.path_saveFolder); }
                catch (Exception ex) { AddLog("Could not persist skill flags: " + ex.Message); }
            }

            ApplyRuntime(true);

            return true;
        }

        public Sprite LoadCustomSprite(string fileName, float targetHeight = 0f, Texture2D shapeTex = null, float forcedPpu = 0f)
        {
            string path = System.IO.Path.Combine(BepInEx.Paths.PluginPath, fileName);
            if (System.IO.File.Exists(path))
            {
                try
                {
                    byte[] fileData = System.IO.File.ReadAllBytes(path);
                    Texture2D tex = new Texture2D(2, 2);
                    UnityEngine.ImageConversion.LoadImage(tex, fileData);

                    tex.filterMode = FilterMode.Point;
                    tex.wrapMode = TextureWrapMode.Clamp;

                    if (shapeTex != null)
                    {
                        shapeTex.filterMode = FilterMode.Point;
                        shapeTex.wrapMode = TextureWrapMode.Clamp;
                        tex = ApplyShapeToTexture(tex, shapeTex);
                    }
                    
                    float ppu = 100f;
                    if (forcedPpu > 0f)
                        ppu = forcedPpu;
                    else if (targetHeight > 0f)
                        ppu = tex.height / targetHeight; 

                    return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
                }
                catch (Exception ex)
                {
                    AddLog($"Failed to load {fileName}: {ex.Message}");
                }
            }
            return null;
        }

        private Sprite[] LoadAndSliceSpriteSheet(string fileName, int frameCount, float targetHeight)
        {
            string path = System.IO.Path.Combine(BepInEx.Paths.PluginPath, fileName);
            if (System.IO.File.Exists(path))
            {
                try
                {
                    byte[] fileData = System.IO.File.ReadAllBytes(path);
                    Texture2D tex = new Texture2D(2, 2);
                    UnityEngine.ImageConversion.LoadImage(tex, fileData);

                    tex.filterMode = FilterMode.Point;
                    tex.wrapMode = TextureWrapMode.Clamp;

                    int frameWidth = tex.width / frameCount;
                    int frameHeight = tex.height;

                    float ppu = 100f;
                    if (targetHeight > 0f) ppu = frameHeight / targetHeight;

                    Sprite[] sprites = new Sprite[frameCount];
                    for (int i = 0; i < frameCount; i++)
                    {
                        sprites[i] = Sprite.Create(tex, new Rect(i * frameWidth, 0, frameWidth, frameHeight), new Vector2(0.5f, 0.5f), ppu);
                    }
                    return sprites;
                }
                catch (Exception ex)
                {
                    AddLog($"Failed to load {fileName}: {ex.Message}");
                }
            }
            return null;
        }

        private Texture2D ApplyShapeToTexture(Texture2D baseTex, Texture2D shapeTex)
        {
            Color[] basePixels = baseTex.GetPixels();
            for (int y = 0; y < baseTex.height; y++)
            {
                for (int x = 0; x < baseTex.width; x++)
                {
                    float u = (float)x / baseTex.width;
                    float v = (float)y / baseTex.height;
                    
                    Color shapeColor = shapeTex.GetPixelBilinear(u, v);
                    
                    int idx = y * baseTex.width + x;
                    basePixels[idx].a = shapeColor.a; 
                }
            }
            baseTex.SetPixels(basePixels);
            baseTex.Apply();
            return baseTex;
        }

        public void SpawnShopObject()
        {
            if (GameObject.Find("AP_Shop_Terminal") != null) return;

            GameObject shopObj = new GameObject("AP_Shop_Terminal");
            shopObj.transform.position = new Vector3(3f, 1f, 0f); 
            shopObj.transform.localScale = new Vector3(1.5f, 1.5f, 1f);

             // Create an invisible interaction point at the center of the backpack
            GameObject interactPoint = new GameObject("AP_Shop_InteractPoint");
            interactPoint.transform.SetParent(shopObj.transform);
            // Adjust this Y value to move the interaction area up/down
            // Negative values move it down towards the bottom of the sprite
            interactPoint.transform.localPosition = new Vector3(0f, -0.1f, 0f);

            var sr = shopObj.AddComponent<SpriteRenderer>();
            
            Sprite shopSprite = LoadCustomSprite("wichiwi_equipment.png");
            if (shopSprite == null)
            {
                var talismanOverview = SingletonScriptableObject<TalismanDataOverview>.Instance;
                if (talismanOverview != null)
                {
                    var wichiwiTalisman = talismanOverview.FindUniqueId("15_WichiHat");
                    if (wichiwiTalisman != null) shopSprite = wichiwiTalisman.Icon;
                }
            }
            sr.sprite = shopSprite;
            sr.color = Color.white; 

            var col = shopObj.AddComponent<BoxCollider2D>();
            col.size = sr.sprite != null ? sr.sprite.bounds.size : new Vector2(1f, 1f);

            GameObject uiGroup = new GameObject("AP_Shop_UI");
            uiGroup.transform.SetParent(shopObj.transform);
            uiGroup.transform.localPosition = new Vector3(0f, 0.6f, -0.2f); 

            GameObject bgObj = new GameObject("AP_Shop_BG");
            var bgSr = bgObj.AddComponent<SpriteRenderer>();
            
            List<Sprite> bgSprites = new List<Sprite>();
            Sprite bgBase = LoadCustomSprite("ui_background_1.png", 0.55f);
            
            Texture2D shape2 = null, shape3 = null;
            string path2 = System.IO.Path.Combine(BepInEx.Paths.PluginPath, "ui_background_2.png");
            string path3 = System.IO.Path.Combine(BepInEx.Paths.PluginPath, "ui_background_3.png");
            if (System.IO.File.Exists(path2)) { shape2 = new Texture2D(2, 2); UnityEngine.ImageConversion.LoadImage(shape2, System.IO.File.ReadAllBytes(path2)); }
            if (System.IO.File.Exists(path3)) { shape3 = new Texture2D(2, 2); UnityEngine.ImageConversion.LoadImage(shape3, System.IO.File.ReadAllBytes(path3)); }

            if (shape2 != null) bgSprites.Add(LoadCustomSprite("ui_background_1.png", 0.55f, shape2));
            if (shape3 != null) bgSprites.Add(LoadCustomSprite("ui_background_1.png", 0.55f, shape3));
            if (bgBase != null) bgSprites.Add(bgBase);

            if (bgSprites.Count > 0)
            {
                bgSr.sprite = bgSprites[0];
                float targetWidth = 1.5f;
                float scaleX = targetWidth / bgSr.sprite.bounds.size.x; 
                bgObj.transform.localScale = new Vector3(scaleX, 1f, 1f);

                if (bgSprites.Count > 1)
                {
                    var animator = bgObj.AddComponent<UIBackgroundAnimator>();
                    animator.sprites = bgSprites.ToArray();
                    animator.frameRate = 0.15f;
                }
            }
            else
            {
                Texture2D bgTex = new Texture2D(1, 1);
                bgTex.SetPixel(0, 0, new Color(0.15f, 0.1f, 0.1f, 0.85f));
                bgTex.Apply();
                bgSr.sprite = Sprite.Create(bgTex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                bgObj.transform.localScale = new Vector3(2.0f, 0.6f, 1f);
            }
            bgSr.sortingOrder = 1000;
            bgSr.color = new Color(1f, 1f, 1f, 0.65f);
            bgObj.transform.SetParent(uiGroup.transform);
            bgObj.transform.localPosition = new Vector3(0f, 0f, 0.1f);

            // 3. E-Button (Load 4 individual frames with the exact same PPU to prevent jitter)
            GameObject btnObj = new GameObject("AP_Shop_Button");
            var btnSr = btnObj.AddComponent<SpriteRenderer>();
            
            List<Sprite> btnSpritesList = new List<Sprite>();
            
            // Load the first frame to establish the scale
            Sprite frame1 = LoadCustomSprite("E_Button_1.png", 0.3f);
            if (frame1 != null)
            {
                btnSpritesList.Add(frame1);
                
                // Load frames 2 to 4 using the EXACT same pixelsPerUnit as frame 1
                for (int i = 2; i <= 4; i++)
                {
                    Sprite frame = LoadCustomSprite($"E_Button_{i}.png", 0f, null, frame1.pixelsPerUnit);
                    if (frame != null) btnSpritesList.Add(frame);
                }
            }

            // Fallback to spritesheet if individual frames don't exist
            if (btnSpritesList.Count == 0)
            {
                Sprite[] sheetSprites = LoadAndSliceSpriteSheet("E_Button.png", 4, 0.4f);
                if (sheetSprites != null) btnSpritesList.AddRange(sheetSprites);
            }

            if (btnSpritesList.Count > 0)
            {
                btnSr.sprite = btnSpritesList[0];
                btnObj.transform.localScale = Vector3.one;
                
                if (btnSpritesList.Count > 1)
                {
                    var btnAnim = btnObj.AddComponent<UIBackgroundAnimator>();
                    btnAnim.sprites = btnSpritesList.ToArray();
                    btnAnim.frameRate = 0.12f;
                }
            }
            else
            {
                // Fallback if no images exist
                Texture2D redTex = new Texture2D(1, 1);
                redTex.SetPixel(0, 0, new Color(0.8f, 0.1f, 0.1f, 1f));
                redTex.Apply();
                btnSr.sprite = Sprite.Create(redTex, new Rect(0,0,1,1), new Vector2(0.5f, 0.5f), 1f);
                btnObj.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
            }
            btnSr.sortingOrder = 1001;
            btnObj.transform.SetParent(uiGroup.transform);
            btnObj.transform.localPosition = new Vector3(-0.4f, 0f, 0f); 

            TMPro.TMP_FontAsset gameFont = null;
            var tmps = Resources.FindObjectsOfTypeAll<TMPro.TextMeshProUGUI>();
            if (tmps != null && tmps.Length > 0) gameFont = tmps[0].font;

            GameObject textObj = new GameObject("AP_Shop_Text");
            var textMesh = textObj.AddComponent<TextMeshPro>();
            
            if (gameFont != null) textMesh.font = gameFont;
            
            textMesh.text = "Shop";
            textMesh.fontSize = 2.5f;
            textMesh.alignment = TextAlignmentOptions.Center;
            textMesh.color = Color.white;
            textMesh.enableWordWrapping = false;
            
            var textRenderer = textObj.GetComponent<MeshRenderer>();
            if (textRenderer != null) textRenderer.sortingOrder = 1001;
            
            textObj.transform.SetParent(uiGroup.transform);
            textObj.transform.localPosition = new Vector3(0.15f, 0f, 0f); 

            AddLog("Spawned Wichiwi AP Shop Terminal.");
        }

        public Sprite PickIconForItem(string itemName, bool isCrossGame = false)
        {
            if (string.IsNullOrEmpty(itemName)) return null;

            // Cross-game items have no local icon — explicit flag from the scout,
            // not a name heuristic. Vanilla names like "Mira's Shadow" would
            // otherwise be mis-detected.
            if (isCrossGame)
                return LoadCustomSprite("ap_icon_unknown.png", 0.5f);

            if (itemName.StartsWith("Talisman: "))
            {
                var overview = SingletonScriptableObject<TalismanDataOverview>.Instance;
                if (overview != null)
                {
                    foreach (var tal in overview.GetAllData())
                    {
                        if (tal?.Icon == null) continue;
                        if (!string.IsNullOrEmpty(tal.DisplayName)
                            && _talismanItemByDataId.TryGetValue(tal.DisplayName, out var mapped)
                            && mapped == itemName)
                        {
                            return tal.Icon;
                        }
                    }
                }
                return LoadCustomSprite("ap_icon_talisman.png", 0.5f);
            }

            if (itemName.StartsWith("Skill "))
            {
                var overview = SingletonScriptableObject<SkillTreeDataOverview>.Instance;
                if (overview != null)
                {
                    foreach (var tree in overview.GetAllData())
                    {
                        if (tree == null) continue;
                        var treeItemName = _skillItemByTreeId.TryGetValue(tree.DisplayName, out var n) ? n : null;
                        if (treeItemName == itemName)
                        {
                            var icon = GetSkillIcon(tree);
                            if (icon != null) return icon;
                        }
                    }
                }
                return LoadCustomSprite("ap_icon_skill.png", 0.5f);
            }

            if (_fillerDustValues.ContainsKey(itemName))
            {
                if (_fillerIconNames.TryGetValue(itemName, out var iconName))
                {
                    var sprite = FindSpriteByName(iconName);
                    if (sprite != null) return sprite;
                }
                return FindSpriteByName("UI_Crystal_0") ?? LoadCustomSprite("ap_icon_filler.png", 0.5f);
            }

            if (itemName == "Shop Expansion")
                return LoadCustomSprite("ap_icon_shop.png", 0.5f);

            if (itemName.EndsWith(" Sigil"))
            {
                var bossKey = SigilToBossKey(itemName);
                if (!string.IsNullOrEmpty(bossKey))
                {
                    var sprite = FindBossSteleSprite(bossKey);
                    if (sprite != null) return sprite;
                }
                return LoadCustomSprite("ap_icon_sigil.png", 0.5f);
            }

            return LoadCustomSprite("ap_icon_unknown.png", 0.5f);
        }

        private static Sprite FindSpriteByName(string name)
        {
            foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (s != null && s.name == name)
                    return s;
            }
            return null;
        }

        private static object GetFieldOrProperty(object obj, string name)
        {
            if (obj == null) return null;
            var type = obj.GetType();
            var flags = BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

            var field = type.GetField(name, flags);
            if (field != null)
            {
                try { return field.GetValue(obj); } catch { }
            }
            var prop = type.GetProperty(name, flags);
            if (prop != null && prop.GetIndexParameters().Length == 0)
            {
                try { return prop.GetValue(obj); } catch { }
            }
            return null;
        }

        private static string ExtractTermString(object term)
        {
            if (term == null) return null;

            var getSentence = term.GetType().GetMethod("GetSentence", Type.EmptyTypes);
            if (getSentence != null)
            {
                try
                {
                    var s = getSentence.Invoke(term, null) as string;
                    if (!string.IsNullOrEmpty(s)) return s;
                }
                catch { }
            }

            try
            {
                var s = term.ToString();
                if (!string.IsNullOrEmpty(s) && s != term.GetType().FullName)
                    return s;
            }
            catch { }

            return null;
        }

        private string LookupSkillDescription(string apItemName)
        {
            var overview = SingletonScriptableObject<SkillTreeDataOverview>.Instance;
            if (overview == null) return null;

            foreach (var tree in overview.GetAllData())
            {
                if (tree == null) continue;
                if (!_skillItemByTreeId.TryGetValue(tree.DisplayName, out var mapped) || mapped != apItemName)
                    continue;

                // Search candidates in order of likelihood. SkillName is where the
                // term fields most likely live - we didn't scan inside it yet, so
                // just try both direct tree fields and its nested SkillName objects.
                var candidates = new List<object>
                {
                    GetFieldOrProperty(tree, "skillName"),
                    GetFieldOrProperty(tree, "secondRunSkillName"),
                    tree.skillTreeDataDetail,
                    tree.secondRunSkillTreeDataDetail,
                    tree,
                };

                var fieldNames = new[]
                {
                    "detailTerm", "descriptionTerm", "explainTerm", "explanationTerm",
                    "textTerm", "bodyTerm",
                    "description", "detail", "text",
                    "detailText", "explainText", "descriptionText",
                };

                foreach (var candidate in candidates)
                {
                    if (candidate == null) continue;
                    foreach (var name in fieldNames)
                    {
                        var term = GetFieldOrProperty(candidate, name);
                        var text = ExtractTermString(term);
                        if (!string.IsNullOrEmpty(text)) return text;
                    }
                }
            }
            return null;
        }

        private void OnDataPackageReady()
        {
            if (!ApRunActive) return;

            // DataPackage just arrived. Re-scout shop slots so previously
            // unnamed cross-game items get their real names.
            if (_shopLocationIds.Count > 0)
            {
                Client.RequestLocationScouts(_shopLocationIds.Values);
                AddLog("Re-scouting " + _shopLocationIds.Count + " shop slots after DataPackage arrived.");
            }
        }

        private void OnDeathLinkReceived(string source, string cause)
        {
            if (!ApRunActive || !DeathLinkActive) return;
            var sceneName = SceneManager.GetActiveScene().name;
            if (sceneName == "TitleMenu" || sceneName == ChallengeAreaScene) return;

            AddLog("DeathLink from " + source + ": " + cause);

            var core = GetGamePlayCore();
            if (core?.player == null) return;

            if (TryKillLocalPlayer(core.player))
            {
                AddLog("Local player killed by DeathLink.");
                _lastKnownPlayerHp = -1;
                _playerDeathFired = false;
                _deathsSinceLastSend = 0;
            }
            else
            {
                AddLog("Could not kill player (no HP field or kill method found).");
            }
        }

        private void PollPlayerHpForDeathLink()
        {
            var core = GetGamePlayCore();
            if (core?.player == null) return;

            bool isDead;
            try
            {
                // PlayerCore.state ist ein öffentliches Feld vom Typ PlayerCore.State
                var stateField = core.player.GetType().GetField("state",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (stateField == null) return;
                var stateVal = stateField.GetValue(core.player);
                isDead = stateVal != null && stateVal.ToString() == "Dead";
            }
            catch { return; }

            if (!isDead && _lastKnownPlayerHp <= 0)
                _playerDeathFired = false;
            else if (isDead && _lastKnownPlayerHp > 0 && !_playerDeathFired)
            {
                OnLocalPlayerDied();
                _playerDeathFired = true;
            }

            _lastKnownPlayerHp = isDead ? 0 : 1;
        }

        private bool TryKillLocalPlayer(object player)
        {
            if (player == null) return false;
            var type = player.GetType();

            if (_playerKillMethod == null)
            {
                foreach (var name in new[] { "Dead", "DeadByPoison", "FakeDead" })
                {
                    var m = type.GetMethod(name,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                        null, Type.EmptyTypes, null);
                    if (m != null) { _playerKillMethod = m; break; }
                }
            }

            if (_playerKillMethod != null)
            {
                try { _playerKillMethod.Invoke(player, null); return true; }
                catch (Exception ex) { AddLog("Kill method failed: " + ex.Message); }
            }
            return false;
        }

        private void OnLocalPlayerDied()
        {
            _deathsSinceLastSend++;
            if (DeathLinkActive && _deathsSinceLastSend > _deathLinkAmnesty)
            {
                Client.SendDeathLink("died in " + SceneManager.GetActiveScene().name);
                AddLog("DeathLink sent (" + _deathsSinceLastSend + " deaths).");
                _deathsSinceLastSend = 0;
            }
            else
            {
                AddLog("Death forgiven (" + _deathsSinceLastSend + "/" + _deathLinkAmnesty + ").");
            }
        }
    }

    public class UIBackgroundAnimator : MonoBehaviour
    {
        public Sprite[] sprites;
        public float frameRate = 0.15f;
        private SpriteRenderer sr;
        private float timer;
        private int index;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
        }

        void Update()
        {
            if (sprites == null || sprites.Length <= 1 || sr == null) return;
            
            timer += Time.deltaTime;
            if (timer >= frameRate)
            {
                timer = 0;
                index = (index + 1) % sprites.Length;
                sr.sprite = sprites[index];
            }
        }
    }
}

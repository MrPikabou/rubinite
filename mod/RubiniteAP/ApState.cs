using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using BepInEx;
using Newtonsoft.Json;
using IOPath = System.IO.Path;

namespace RubiniteAP
{
    public sealed class ApState
    {
        public string Slot = "";
        public string Seed = "";
        public string Uuid = "";
        public int LastItemIndex;
        public int PendingDust = 0;
        public int LastKnownDust = 0;
        public List<string> Items = new List<string>();
        public List<long> Checked = new List<long>();
        public bool GoalSent;
        public bool ChallengeSaveInitialized;

        public float LastDarkRubyFightTime;
        public bool LastDarkRubyRankAchieved;

        public int DeathsSinceLastSend = 0;

        [JsonIgnore]
        public string FilePath;

        private static string StateDirectory => IOPath.Combine(Paths.ConfigPath, "RubiniteAP");
        private static string ProfilesDirectory => IOPath.Combine(StateDirectory, "Profiles");

        public string ProfileId => GetProfileId(Slot, Seed);
        public string ChallengeSavePath =>
            IOPath.Combine(StateDirectory, "ChallengeSaves", ProfileId);

        public static ApState Load()
        {
            var path = IOPath.Combine(StateDirectory, "state.json");
            try
            {
                if (File.Exists(path))
                {
                    var state = JsonConvert.DeserializeObject<ApState>(File.ReadAllText(path));
                    if (state != null)
                    {
                        state.FilePath = path;
                        state.Items ??= new List<string>();
                        state.Checked ??= new List<long>();
                        return state;
                    }
                }
            }
            catch (Exception ex)
            {
                RubiniteAPPlugin.Log.LogWarning("Could not load AP state: " + ex.Message);
            }

            return new ApState { FilePath = path };
        }

        public void Save()
        {
            try
            {
                SaveTo(FilePath);
            }
            catch (Exception ex)
            {
                RubiniteAPPlugin.Log.LogWarning("Could not save AP state: " + ex.Message);
            }
        }

        public bool LoadProfile(string slot, string seed)
        {
            var profilePath = ProfilePath(slot, seed);
            var clientUuid = Uuid;
            try
            {
                if (!string.IsNullOrEmpty(Slot) && !string.IsNullOrEmpty(Seed))
                    SaveTo(ProfilePath(Slot, Seed));

                if (File.Exists(profilePath))
                {
                    var profile = JsonConvert.DeserializeObject<ApState>(File.ReadAllText(profilePath));
                    if (profile == null)
                        throw new InvalidDataException("Profile file contains no state.");
                    CopyFrom(profile);
                }
                else
                {
                    FilePath = profilePath;
                    ResetForSlot(slot, seed);
                }

                Slot = slot;
                Seed = seed;
                Uuid = clientUuid;
                FilePath = profilePath;
                SaveTo(FilePath);
                return true;
            }
            catch (Exception ex)
            {
                RubiniteAPPlugin.Log.LogError(
                    "Could not load AP profile for slot '" + slot + "' and seed '" + seed + "': " + ex);
                return false;
            }
        }

        public static string GetProfileId(string slot, string seed)
        {
            var profileKey = (slot ?? "").Trim() + "\n" + (seed ?? "").Trim();
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(profileKey));
                var result = new StringBuilder(hash.Length * 2);
                foreach (var value in hash)
                    result.Append(value.ToString("x2"));
                return result.ToString();
            }
        }

        private static string ProfilePath(string slot, string seed)
        {
            return IOPath.Combine(ProfilesDirectory, GetProfileId(slot, seed) + ".json");
        }

        private void SaveTo(string path)
        {
            Directory.CreateDirectory(IOPath.GetDirectoryName(path));
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        private void CopyFrom(ApState profile)
        {
            Slot = profile.Slot ?? "";
            Seed = profile.Seed ?? "";
            Uuid = profile.Uuid ?? "";
            LastItemIndex = profile.LastItemIndex;
            PendingDust = profile.PendingDust;
            LastKnownDust = profile.LastKnownDust;
            Items = profile.Items ?? new List<string>();
            Checked = profile.Checked ?? new List<long>();
            GoalSent = profile.GoalSent;
            ChallengeSaveInitialized = profile.ChallengeSaveInitialized;
            LastDarkRubyFightTime = profile.LastDarkRubyFightTime;
            LastDarkRubyRankAchieved = profile.LastDarkRubyRankAchieved;
            DeathsSinceLastSend = profile.DeathsSinceLastSend;
        }

        public bool HasItem(string name)
        {
            return Items.Contains(name);
        }

        public int CountItems(string name)
        {
            var count = 0;
            foreach (var item in Items)
                if (item == name) count++;
            return count;
        }

        public void AddItem(string name)
        {
            Items.Add(name);
        }

        public bool HasChecked(long id)
        {
            return Checked.Contains(id);
        }

        public void AddChecked(IEnumerable<long> ids)
        {
            foreach (var id in ids)
                if (!Checked.Contains(id))
                    Checked.Add(id);
        }

        public void ResetForSlot(string slot, string seed)
        {
            Slot = slot;
            Seed = seed;
            LastItemIndex = 0;
            Items.Clear();
            Checked.Clear();
            GoalSent = false;
            PendingDust = 0;
            LastKnownDust = 0;
            ChallengeSaveInitialized = false;
            LastDarkRubyFightTime = 0;
            LastDarkRubyRankAchieved = false;
            DeathsSinceLastSend = 0;
            Save();
        }

        public void EnsureUuid()
        {
            if (string.IsNullOrEmpty(Uuid))
            {
                Uuid = Guid.NewGuid().ToString();
                Save();
            }
        }
    }
}

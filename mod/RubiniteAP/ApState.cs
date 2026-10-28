using System;
using System.Collections.Generic;
using System.IO;
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
        public List<string> Items = new List<string>();
        public List<long> Checked = new List<long>();
        public bool GoalSent;

        [JsonIgnore]
        public string FilePath;

        private static string StateDirectory => IOPath.Combine(Paths.ConfigPath, "RubiniteAP");

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
                Directory.CreateDirectory(StateDirectory);
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch (Exception ex)
            {
                RubiniteAPPlugin.Log.LogWarning("Could not save AP state: " + ex.Message);
            }
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

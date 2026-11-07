using System;
using System.IO;
using System.Collections.Generic;
using BepInEx;
using Newtonsoft.Json;
using IOPath = System.IO.Path;

namespace RubiniteDarkRuby
{
    public sealed class DarkRubyState
    {
        public bool CompletedOnce;
        public int TotalClears;

        // Offline best times (top 3). -1 = not set yet.
        public float BestTimeSeconds = -1f;
        public float SecondBestTimeSeconds = -1f;
        public float ThirdBestTimeSeconds = -1f;

        public List<int> BestTalismanIds = new List<int> { 0, 0, 0, 0, 0, 0 };
        public List<int> SecondBestTalismanIds = new List<int> { 0, 0, 0, 0, 0, 0 };
        public List<int> ThirdBestTalismanIds = new List<int> { 0, 0, 0, 0, 0, 0 };

        // Last fight result (for the win screen, regardless of rank)
        public float LastFightTimeSeconds = -1f;
        public bool LastFightRankAchieved;
        public int LastFightRankIndex = -1;   // 0=S, 1=A, 2=B, 3=C, -1=unknown

        public bool SkinSelected;
        public string LastVanillaSkinId;

        [JsonIgnore]
        public string FilePath;

        private static string StateDirectory => IOPath.Combine(Paths.ConfigPath, "RubiniteDarkRuby");

        public static DarkRubyState Load()
        {
            var path = IOPath.Combine(StateDirectory, "state.json");
            try
            {
                if (File.Exists(path))
                {
                    var state = JsonConvert.DeserializeObject<DarkRubyState>(File.ReadAllText(path));
                    if (state != null)
                    {
                        state.FilePath = path;
                        return state;
                    }
                }
            }
            catch (Exception ex)
            {
                RubiniteDarkRubyPlugin.Log?.LogWarning("Could not load DarkRuby state: " + ex.Message);
            }
            return new DarkRubyState { FilePath = path };
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(StateDirectory);
                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(FilePath, json);
                if (RubiniteDarkRubyPlugin.Instance?.Settings?.LogSkinDiagnostics?.Value == true)
                {
                    RubiniteDarkRubyPlugin.Log?.LogInfo(
                        "[StateSave] SkinSelected=" + SkinSelected);
                }
            }
            catch (Exception ex)
            {
                RubiniteDarkRubyPlugin.Log?.LogWarning("Could not save state: " + ex.Message);
            }
        }

        public void RecordClear(float timeSeconds, int rankIndex, List<int> talismanIds)
        {
            TotalClears++;
            CompletedOnce = true;
            LastFightTimeSeconds = timeSeconds;
            LastFightRankIndex = rankIndex;
            LastFightRankAchieved = rankIndex >= 0;

            bool isNewBest = BestTimeSeconds < 0 || timeSeconds < BestTimeSeconds;
            bool isNewSecond = !isNewBest
                && (SecondBestTimeSeconds < 0 || timeSeconds < SecondBestTimeSeconds);
            bool isNewThird = !isNewBest && !isNewSecond
                && (ThirdBestTimeSeconds < 0 || timeSeconds < ThirdBestTimeSeconds);

            var ids = talismanIds ?? new List<int> { 0, 0, 0, 0, 0, 0 };

            if (isNewBest)
            {
                ThirdBestTimeSeconds = SecondBestTimeSeconds;
                ThirdBestTalismanIds = SecondBestTalismanIds;
                SecondBestTimeSeconds = BestTimeSeconds;
                SecondBestTalismanIds = BestTalismanIds;
                BestTimeSeconds = timeSeconds;
                BestTalismanIds = ids;
            }
            else if (isNewSecond)
            {
                ThirdBestTimeSeconds = SecondBestTimeSeconds;
                ThirdBestTalismanIds = SecondBestTalismanIds;
                SecondBestTimeSeconds = timeSeconds;
                SecondBestTalismanIds = ids;
            }
            else if (isNewThird)
            {
                ThirdBestTimeSeconds = timeSeconds;
                ThirdBestTalismanIds = ids;
            }

            Save();
        }
    }
}
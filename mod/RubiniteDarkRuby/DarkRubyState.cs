using System;
using System.IO;
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
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch (Exception ex)
            {
                RubiniteDarkRubyPlugin.Log?.LogWarning("Could not save DarkRuby state: " + ex.Message);
            }
        }

        public void RecordClear(float timeSeconds, int rankIndex)
        {
            TotalClears++;
            CompletedOnce = true;
            LastFightTimeSeconds = timeSeconds;
            LastFightRankIndex = rankIndex;
            LastFightRankAchieved = rankIndex >= 0;
            Save();

            if (BestTimeSeconds < 0 || timeSeconds < BestTimeSeconds)
            {
                ThirdBestTimeSeconds = SecondBestTimeSeconds;
                SecondBestTimeSeconds = BestTimeSeconds;
                BestTimeSeconds = timeSeconds;
            }
            else if (SecondBestTimeSeconds < 0 || timeSeconds < SecondBestTimeSeconds)
            {
                ThirdBestTimeSeconds = SecondBestTimeSeconds;
                SecondBestTimeSeconds = timeSeconds;
            }
            else if (ThirdBestTimeSeconds < 0 || timeSeconds < ThirdBestTimeSeconds)
            {
                ThirdBestTimeSeconds = timeSeconds;
            }

            Save();
        }
    }
}
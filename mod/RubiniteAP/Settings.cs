using BepInEx.Configuration;

namespace RubiniteAP
{
    public sealed class Settings
    {
        public ConfigEntry<string> Server;
        public ConfigEntry<int> Port;
        public ConfigEntry<string> Slot;
        public ConfigEntry<string> Password;
        public ConfigEntry<bool> AutoConnect;
        public ConfigEntry<bool> ShowOverlay;
        public ConfigEntry<bool> DeathLinkEnabled;
        public ConfigEntry<string> HudDurationMode;
        public ConfigEntry<float>  HudHoldTime;

        public void Bind(ConfigFile config)
        {
            Server = config.Bind("Server", "Host", "localhost", "Archipelago server host (no ws:// prefix).");
            Port = config.Bind("Server", "Port", 38281, "Archipelago server port.");
            Slot = config.Bind("Server", "Slot", "", "Player/slot name in the Archipelago session.");
            Password = config.Bind("Server", "Password", "", "Room password, if any.");
            AutoConnect = config.Bind("Server", "AutoConnect", true, "Connect automatically on game launch when a slot name is set.");
            ShowOverlay = config.Bind("UI", "ShowOverlayOnLaunch", true, "Show the Archipelago overlay when the game starts.");
            DeathLinkEnabled = config.Bind("DeathLink", "Enabled", false,
                "Opt into DeathLink. Only takes effect if the seed also has death_link: true.");

            HudDurationMode = config.Bind(
                "UI",
                "HudDurationMode",
                "Timed",
                new ConfigDescription(
                    "HUD toast duration: 'Timed' (fades out after HoldTime) or 'Permanent' (remains until overridden).",
                    new AcceptableValueList<string>("Timed", "Permanent")));

            HudHoldTime = config.Bind(
                "UI",
                "HudHoldTime",
                5f,
                new ConfigDescription(
                    "HUD toast display duration in seconds (in Timed mode only).",
                    new AcceptableValueRange<float>(1f, 60f)));
        }
    }
}

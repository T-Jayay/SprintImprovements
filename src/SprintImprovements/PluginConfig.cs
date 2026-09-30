using BepInEx.Configuration;

namespace SprintImprovements
{
    /// <summary>What the sprint key does (the SprintMode setting).</summary>
    public enum SprintKeyMode
    {
        /// <summary>Press to start sprinting, press again to stop (the normal game).</summary>
        Toggle,
        /// <summary>Sprint while the key is held.</summary>
        Hold,
        /// <summary>Sprint whenever possible; hold the key to walk.</summary>
        Always
    }

    internal static class PluginConfig
    {
        private const string Section = "General";

        internal static ConfigEntry<bool> Enabled { get; private set; }

        internal static ConfigEntry<SprintKeyMode> SprintMode { get; private set; }

        internal static ConfigEntry<bool> BlockForcedSprint { get; private set; }

        internal static void Init(ConfigFile config)
        {
            Enabled = config.Bind(Section, "Enabled", true,
                "Off gives you the normal game's sprint.");
            SprintMode = config.Bind(Section, "SprintMode", SprintKeyMode.Toggle,
                "How the sprint key works. Toggle: press to sprint, press again to stop; stopping, strafing, skills "
                + "and stuns only pause sprint. Hold: sprint while you hold the key. Always: sprint whenever you can and "
                + "hold the key to walk.");
            BlockForcedSprint = config.Bind(Section, "BlockForcedSprint", true,
                "Skills no longer turn sprint on when you don't want to sprint (e.g. Mercenary's dashes, "
                + "Commando's Tactical Dive, Huntress's Blink, MUL-T's Transport Mode, Acrid's leaps). When you do "
                + "want to sprint, they work as in the normal game: in Toggle mode your toggle is on (also while "
                + "sprint is paused) or you turn it on during the skill; in Hold mode you hold the key; in Always "
                + "mode you don't. Off: they turn sprint on, and in Toggle mode that turns your toggle on, as in the "
                + "normal game.");

            // These change what the player wants from sprint: each player then starts again from what they see.
            Enabled.SettingChanged += (sender, args) => SprintGuard.OnModeChanged();
            SprintMode.SettingChanged += (sender, args) => SprintGuard.OnModeChanged();
        }
    }
}

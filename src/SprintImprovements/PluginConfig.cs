using System;
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

    /// <summary>
    /// The settings. Enabled and the others follow each other: turning Enabled on or off turns BlockForcedSprint and
    /// KeepSprintToggled on or off; turning either of those on, or choosing Hold or Always, turns Enabled on; and
    /// Toggle mode with both off turns it off. This works for changes from Risk of Options and from code or a config
    /// reload alike (BepInEx doesn't watch the file while the game runs).
    /// </summary>
    internal static class PluginConfig
    {
        private const string Section = "General";

        // Set while one linked setting is changing the others, so their change events don't link back.
        private static bool linking;

        internal static ConfigEntry<bool> Enabled { get; private set; }

        internal static ConfigEntry<bool> BlockForcedSprint { get; private set; }

        internal static ConfigEntry<bool> KeepSprintToggled { get; private set; }

        internal static ConfigEntry<SprintKeyMode> SprintMode { get; private set; }

        internal static void Init(ConfigFile config)
        {
            Enabled = config.Bind(Section, "Enabled", true,
                "Master switch. Off gives you the normal game's sprint (SprintMode is ignored while off). Turning "
                + "it on or off turns BlockForcedSprint and KeepSprintToggled on or off with it; turning either of "
                + "those on, or choosing Hold or Always, turns it back on.");
            BlockForcedSprint = config.Bind(Section, "BlockForcedSprint", true,
                "Skills no longer turn sprint on when you don't want to sprint (e.g. Mercenary's dashes, "
                + "Commando's Tactical Dive, Huntress's Blink, MUL-T's Transport Mode, Acrid's leaps). When you do "
                + "want to sprint, they work as in the normal game: in Toggle mode your toggle is on (also while "
                + "sprint is paused) or you turn it on during the skill; in Hold mode you hold the key; in Always "
                + "mode you don't.");
            KeepSprintToggled = config.Bind(Section, "KeepSprintToggled", true,
                "Toggle mode only: sprint stays toggled until you press sprint again, also on the next stage or "
                + "after a revive. Stopping, moving sideways or backwards, skills and stuns only pause it, and it "
                + "comes back by itself (shortly after a skill ends and you let go of its button); Hold and Always "
                + "always pause and resume sprint like this. Pressing sprint while it's paused and you move forward "
                + "sprints straight away, as in the normal game (this can cancel a skill), and keeps it on; standing "
                + "still or strafing, the press turns it off. Off: the normal game's toggle, which those things "
                + "switch off.");
            SprintMode = config.Bind(Section, "SprintMode", SprintKeyMode.Toggle,
                "How the sprint key works. Toggle: press to sprint, press again to stop (the normal game). Hold: "
                + "sprint while you hold the key; pressing it sprints straight away as in the normal game, even if "
                + "that cancels a skill. Always: sprint whenever you can and hold the key to walk; sprint never "
                + "cancels a skill in this mode. Choosing Hold or Always turns Enabled on.");

            Enabled.SettingChanged += OnEnabledChanged;
            BlockForcedSprint.SettingChanged += OnSubSettingChanged;
            KeepSprintToggled.SettingChanged += OnSubSettingChanged;
            SprintMode.SettingChanged += OnSubSettingChanged;

            // These change how the toggle works: each player then re-derives it from what they see, so sprint can't
            // get stuck on or off.
            Enabled.SettingChanged += OnToggleRulesChanged;
            KeepSprintToggled.SettingChanged += OnToggleRulesChanged;
            SprintMode.SettingChanged += OnToggleRulesChanged;
        }

        private static void OnEnabledChanged(object sender, EventArgs e)
        {
            Link(() =>
            {
                bool on = Enabled.Value;
                BlockForcedSprint.Value = on;
                KeepSprintToggled.Value = on;
            });
        }

        private static void OnSubSettingChanged(object sender, EventArgs e)
        {
            Link(() =>
            {
                bool any = BlockForcedSprint.Value || KeepSprintToggled.Value
                    || SprintMode.Value != SprintKeyMode.Toggle;
                if (Enabled.Value != any)
                {
                    Enabled.Value = any;
                }
            });
        }

        private static void OnToggleRulesChanged(object sender, EventArgs e)
        {
            SprintGuard.OnModeChanged();
        }

        private static void Link(Action apply)
        {
            if (linking)
            {
                return;
            }
            linking = true;
            try
            {
                apply();
            }
            finally
            {
                linking = false;
            }
        }
    }
}

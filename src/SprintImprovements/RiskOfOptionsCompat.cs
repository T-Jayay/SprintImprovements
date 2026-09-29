using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using RiskOfOptions;
using RiskOfOptions.Components.Options;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;
using UnityEngine;

namespace SprintImprovements
{
    /// <summary>
    /// In-game settings through Risk of Options, a soft dependency. Its types appear only inside the bodies of the
    /// non-inlined methods here, so they are only loaded when it is installed.
    /// </summary>
    internal static class RiskOfOptionsCompat
    {
        public const string Guid = "com.rune580.riskofoptions";

        // The store icon, embedded from thunderstore/SprintImprovements/icon.png by Directory.Build.targets.
        private const string IconResource = "SprintImprovements.icon.png";

        private static readonly Vector2 centerPivot = new Vector2(0.5f, 0.5f);

        // Identifiers of our checkboxes, to find their controls when redrawing.
        private static readonly HashSet<string> identifiers = new HashSet<string>();

        private static MethodInfo updateControls;
        private static bool updateControlsLookedUp;
        private static bool refreshFailureLogged;

        public static bool IsInstalled => Chainloader.PluginInfos.ContainsKey(Guid);

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Init()
        {
            ModSettingsManager.SetModDescription(
                "Sprint changes only when you want it to. Skills like Mercenary's dash no longer force it on; "
                + "stopping, strafing, skills and stuns only pause it; and you can pick toggle, hold or always-on "
                + "sprint. Only affects your own character.");
            SetModIcon();
            AddCheckBox(PluginConfig.Enabled, null);
            AddCheckBox(PluginConfig.BlockForcedSprint, null);
            // Only used in Toggle mode, and only while PollButtonInput is patched: greyed out otherwise (Risk of
            // Options re-checks this after every change made in its menu).
            AddCheckBox(PluginConfig.KeepSprintToggled,
                () => PluginConfig.SprintMode.Value != SprintKeyMode.Toggle || !SprintGuard.InputPatched);
            AddChoice(PluginConfig.SprintMode, () => !SprintGuard.InputPatched);
            // A mode change can turn Enabled on or off and grey KeepSprintToggled out or in: redraw the checkboxes.
            PluginConfig.SprintMode.SettingChanged += (sender, args) => RefreshCheckboxes();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AddCheckBox(ConfigEntry<bool> entry, Func<bool> isDisabled)
        {
            CheckBoxConfig config = new CheckBoxConfig();
            if (isDisabled != null)
            {
                config.checkIfDisabled = new BaseOptionConfig.IsDisabledDelegate(isDisabled);
            }
            CheckBoxOption option = new CheckBoxOption(entry, config);
            ModSettingsManager.AddOption(option);
            if (option.Identifier != null)
            {
                identifiers.Add(option.Identifier);
            }
            // Linked settings (see PluginConfig) change each other from code, and Risk of Options only redraws a
            // checkbox when it is clicked or its panel opens, so redraw ours whenever one of them changes.
            entry.SettingChanged += (sender, args) => RefreshCheckboxes();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AddChoice(ConfigEntryBase entry, Func<bool> isDisabled)
        {
            ChoiceConfig config = new ChoiceConfig();
            if (isDisabled != null)
            {
                config.checkIfDisabled = new BaseOptionConfig.IsDisabledDelegate(isDisabled);
            }
            ModSettingsManager.AddOption(new ChoiceOption(entry, config));
        }

        /// <summary>
        /// Shows the store icon in the mod list instead of a question mark. Fails soft with a warning.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SetModIcon()
        {
            try
            {
                Sprite icon = LoadIcon();
                if (icon)
                {
                    ModSettingsManager.SetModIcon(icon);
                }
            }
            catch (Exception e)
            {
                SprintImprovementsPlugin.Log.LogWarning(
                    $"Couldn't load the mod icon for Risk of Options: {e.GetType().Name}: {e.Message}");
            }
        }

        /// <summary>The embedded store icon, or null with a warning when it is missing or can't be decoded.</summary>
        private static Sprite LoadIcon()
        {
            byte[] png;
            using (Stream stream = typeof(RiskOfOptionsCompat).Assembly.GetManifestResourceStream(IconResource))
            {
                if (stream == null)
                {
                    SprintImprovementsPlugin.Log.LogWarning(
                        $"The DLL has no {IconResource} resource, so Risk of Options shows no icon for the mod.");
                    return null;
                }
                using (MemoryStream buffer = new MemoryStream())
                {
                    stream.CopyTo(buffer);
                    png = buffer.ToArray();
                }
            }
            // LoadImage replaces the placeholder size and format with the image's.
            Texture2D texture = new Texture2D(2, 2);
            if (!texture.LoadImage(png))
            {
                UnityEngine.Object.Destroy(texture);
                SprintImprovementsPlugin.Log.LogWarning(
                    $"Couldn't decode {IconResource}, so Risk of Options shows no icon for the mod.");
                return null;
            }
            // With the default Repeat, the edges of the scaled-down icon would blend with the opposite edges.
            texture.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), centerPivot);
        }

        /// <summary>
        /// Redraws our checkboxes in any open settings panel through the protected UpdateControls, which refreshes the
        /// tick, the greyed-out state and the "modified" marker. It is declared on
        /// ModSettingsControl&lt;TValue, TOptionConfig&gt;, the base class of ModSettingsBool.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RefreshCheckboxes()
        {
            if (!updateControlsLookedUp)
            {
                updateControlsLookedUp = true;
                updateControls = AccessTools.Method(typeof(ModSettingsBool), "UpdateControls", Type.EmptyTypes);
                if (updateControls == null)
                {
                    SprintImprovementsPlugin.Log.LogWarning("Risk of Options has no ModSettingsBool.UpdateControls, so "
                        + "checkboxes changed by a linked setting only redraw when clicked or reopened.");
                }
            }
            if (updateControls == null)
            {
                return;
            }
            try
            {
                foreach (ModSettingsBool checkbox in UnityEngine.Object.FindObjectsOfType<ModSettingsBool>())
                {
                    if (checkbox && checkbox.settingToken != null && identifiers.Contains(checkbox.settingToken))
                    {
                        updateControls.Invoke(checkbox, null);
                    }
                }
            }
            catch (Exception e)
            {
                if (!refreshFailureLogged)
                {
                    refreshFailureLogged = true;
                    SprintImprovementsPlugin.Log.LogWarning(
                        $"Couldn't redraw the Risk of Options checkboxes: {e.GetType().Name}: {e.Message}");
                }
            }
        }
    }
}

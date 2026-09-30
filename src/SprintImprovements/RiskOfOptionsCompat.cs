using System.IO;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;
using UnityEngine;

namespace SprintImprovements
{
    /// <summary>
    /// In-game settings through Risk of Options, a soft dependency. Its types appear only inside the body of the
    /// non-inlined <see cref="Init"/>, so they are only loaded when it is installed.
    /// </summary>
    internal static class RiskOfOptionsCompat
    {
        public const string Guid = "com.rune580.riskofoptions";

        // The store icon, embedded from thunderstore/SprintImprovements/icon.png by Directory.Build.targets.
        private const string IconResource = "SprintImprovements.icon.png";

        public static bool IsInstalled => Chainloader.PluginInfos.ContainsKey(Guid);

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        internal static void Init()
        {
            ModSettingsManager.SetModDescription(
                "Sprint changes only when you want it to. Skills like Mercenary's dash no longer force it on; "
                + "stopping, strafing, skills and stuns only pause it; and you can pick toggle, hold or always-on "
                + "sprint. Only affects your own character.");
            ModSettingsManager.SetModIcon(LoadIcon());
            // Risk of Options re-checks checkIfDisabled after every change made in its menu.
            ModSettingsManager.AddOption(new CheckBoxOption(PluginConfig.Enabled));
            ModSettingsManager.AddOption(new ChoiceOption(PluginConfig.SprintMode,
                new ChoiceConfig { checkIfDisabled = () => !PluginConfig.Enabled.Value }));
            ModSettingsManager.AddOption(new CheckBoxOption(PluginConfig.BlockForcedSprint,
                new CheckBoxConfig { checkIfDisabled = () => !PluginConfig.Enabled.Value }));
        }

        /// <summary>The store icon, shown in the mod list instead of a question mark.</summary>
        private static Sprite LoadIcon()
        {
            byte[] png;
            using (Stream stream = typeof(RiskOfOptionsCompat).Assembly.GetManifestResourceStream(IconResource))
            using (MemoryStream buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                png = buffer.ToArray();
            }
            // LoadImage replaces the placeholder size and format with the image's.
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(png);
            // With the default Repeat, the edges of the scaled-down icon would blend with the opposite edges.
            texture.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
    }
}

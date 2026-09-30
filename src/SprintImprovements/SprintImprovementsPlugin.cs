using BepInEx;
using BepInEx.Logging;

namespace SprintImprovements
{
    /// <summary>
    /// Sprint changes only when the player wants it to: skills no longer leave the player sprinting, the sprint toggle
    /// survives stopping, strafing, skills and stuns (they only pause it), and the sprint key can also be held to sprint
    /// or to walk (SprintMode). Client-side: only the players on this game instance are affected. See
    /// <see cref="SprintGuard"/>.
    /// </summary>
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(RiskOfOptionsCompat.Guid, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class SprintImprovementsPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "revor.SprintImprovements";
        public const string PluginName = "SprintImprovements";
        public const string PluginVersion = "1.1.0"; // tools/package.py checks this against the manifest

        internal static ManualLogSource Log { get; private set; }

        private void Awake()
        {
            Log = Logger;
            PluginConfig.Init(Config);
            SprintHooks.Init();
            if (RiskOfOptionsCompat.IsInstalled)
            {
                RiskOfOptionsCompat.Init();
            }
        }
    }
}

using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace SprintImprovements
{
    /// <summary>
    /// Sprint changes only when the player wants it to: skills no longer force it on, the sprint toggle survives
    /// stopping, strafing, skills and stuns (they only pause it), and the sprint key can also be held to sprint or to
    /// walk (SprintMode). Client-side: only the players on this game instance are affected. See
    /// <see cref="SprintGuard"/>.
    /// </summary>
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(RiskOfOptionsCompat.Guid, BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class SprintImprovementsPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "revor.SprintImprovements";
        public const string PluginName = "SprintImprovements";
        public const string PluginVersion = "1.0.0"; // tools/package.py checks this against the manifest

        internal static ManualLogSource Log { get; private set; }

        private void Awake()
        {
            Log = Logger;
            PluginConfig.Init(Config);
            SprintSensitiveStates.Init();
            if (!Patch())
            {
                return;
            }
            if (RiskOfOptionsCompat.IsInstalled)
            {
                try
                {
                    RiskOfOptionsCompat.Init();
                }
                catch (Exception e)
                {
                    Log.LogError($"Risk of Options settings failed to load; the config file still works. {e}");
                }
            }
        }

        /// <summary>Applies all patches or none, so the game never runs with only some of them.</summary>
        private static bool Patch()
        {
            Harmony harmony = new Harmony(PluginGUID);
            try
            {
                harmony.PatchAll(typeof(SprintImprovementsPlugin).Assembly);
            }
            catch (Exception e)
            {
                Log.LogError("Couldn't patch the game (a game update or another mod?), so the mod is disabled and "
                    + $"sprint works as in the normal game. {e}");
                try
                {
                    harmony.UnpatchSelf();
                }
                catch (Exception unpatchError)
                {
                    // The patches that stay do nothing: they all wait for SprintGuard.Patched.
                    Log.LogError($"Couldn't remove the patches that did apply; they stay inactive. {unpatchError}");
                }
                return false;
            }
            SprintGuard.Patched = true;
            return true;
        }
    }
}

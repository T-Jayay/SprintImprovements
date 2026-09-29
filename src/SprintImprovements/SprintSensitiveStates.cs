using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SprintImprovements
{
    /// <summary>
    /// Entity states (and their subclasses) during which a paused sprint doesn't resume, whichever of the body's state
    /// machines runs them, because sprinting would end or change them. <see cref="SprintPause"/> already keeps sprint
    /// off while a sprint-sensitive skill runs its activation state, so this list is mainly for the states that follow
    /// one (an active scope, a sidearm shot, Power Mode, the corrupted beam), plus the states RTAutoSprintEx never
    /// auto-sprints in.
    /// </summary>
    internal static class SprintSensitiveStates
    {
        private static readonly Dictionary<Type, bool> cache = new Dictionary<Type, bool>();

        private static Type[] types = Type.EmptyTypes;

        /// <summary>
        /// Builds the list. If a game update removed one of these states, the list stays empty (with a warning)
        /// instead of breaking the rest of the mod.
        /// </summary>
        internal static void Init()
        {
            try
            {
                types = Build();
            }
            catch (Exception e)
            {
                types = Type.EmptyTypes;
                SprintImprovementsPlugin.Log.LogWarning("Couldn't look up the sprint-sensitive skill states (a game "
                    + "update?), so a paused sprint may resume during some skills and end them. "
                    + $"{e.GetType().Name}: {e.Message}");
            }
        }

        /// <summary>Whether a state of this type keeps a paused sprint off.</summary>
        internal static bool Contains(Type stateType)
        {
            if (!cache.TryGetValue(stateType, out bool result))
            {
                for (int i = 0; i < types.Length && !result; i++)
                {
                    result = types[i].IsAssignableFrom(stateType);
                }
                cache[stateType] = result;
            }
            return result;
        }

        // Separate and never inlined, so that a missing type fails this call (caught in Init), not its caller.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Type[] Build()
        {
            return new[]
            {
                // End when the body sprints (they read isSprinting).
                typeof(EntityStates.Bandit2.Weapon.BaseSidearmState),         // Lights Out, Desperado
                typeof(EntityStates.Railgunner.Scope.BaseScopeState),         // the sniper scopes
                typeof(EntityStates.Toolbot.FireNailgun),                     // Auto-Nailgun
                typeof(EntityStates.Toolbot.ToolbotDualWieldBase),            // Power Mode
                typeof(EntityStates.VoidSurvivor.Weapon.FireCorruptHandBeam), // the corrupted beam
                typeof(EntityStates.DroneTech.Weapon.ShieldFormation),        // FIREWALL
                typeof(EntityStates.Huntress.HuntressWeapon.ChargeArrow),     // no vanilla skill uses it; for mods
                // Skills that sprinting cancels, and RTAutoSprintEx's no-auto-sprint states.
                typeof(EntityStates.Mage.Weapon.Flamethrower),
                typeof(EntityStates.Mage.Weapon.PrepWall),
                typeof(EntityStates.Engi.EngiMissilePainter.Paint),
                typeof(EntityStates.Captain.Weapon.SetupAirstrike),
                typeof(EntityStates.Captain.Weapon.SetupAirstrikeAlt),
                typeof(EntityStates.Captain.Weapon.SetupSupplyDrop),
                typeof(EntityStates.AimThrowableBase),                        // REX's mortars, MUL-T's stun drone, ...
                typeof(EntityStates.Drifter.AimRepossess)
            };
        }
    }
}

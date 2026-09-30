using System;
using System.Collections.Generic;
using RoR2;
using RoR2.Skills;

namespace SprintImprovements
{
    /// <summary>
    /// Entity states during which a paused sprint doesn't resume and a forced sprint isn't ended, whichever of the
    /// body's state machines runs them: the activation states of the skills that cancel sprint, that sprinting cancels
    /// or that force sprint (read from the skill catalog once it is ready, so modded skills count too), plus
    /// <see cref="extraStates"/> and their subclasses.
    /// </summary>
    internal static class SprintStates
    {
        // States that sprinting would end or change but that aren't a skill's activation state (an active scope, a
        // sidearm shot, Power Mode, the corrupted beam), and RTAutoSprintEx's no-auto-sprint states.
        private static readonly Type[] extraStates =
        {
            typeof(EntityStates.Bandit2.Weapon.BaseSidearmState),         // Lights Out, Desperado
            typeof(EntityStates.Railgunner.Scope.BaseScopeState),         // the sniper scopes
            typeof(EntityStates.Toolbot.FireNailgun),                     // Auto-Nailgun
            typeof(EntityStates.Toolbot.ToolbotDualWieldBase),            // Power Mode
            typeof(EntityStates.VoidSurvivor.Weapon.FireCorruptHandBeam), // the corrupted beam
            typeof(EntityStates.DroneTech.Weapon.ShieldFormation),        // FIREWALL
            typeof(EntityStates.Huntress.HuntressWeapon.ChargeArrow),     // no vanilla skill uses it; for mods
            typeof(EntityStates.Mage.Weapon.Flamethrower),
            typeof(EntityStates.Mage.Weapon.PrepWall),
            typeof(EntityStates.Engi.EngiMissilePainter.Paint),
            typeof(EntityStates.Captain.Weapon.SetupAirstrike),
            typeof(EntityStates.Captain.Weapon.SetupAirstrikeAlt),
            typeof(EntityStates.Captain.Weapon.SetupSupplyDrop),
            typeof(EntityStates.AimThrowableBase),                        // REX's mortars, MUL-T's stun drone, ...
            typeof(EntityStates.Drifter.AimRepossess)
        };

        // Whether each state type seen so far is sprint-sensitive; the skills' activation states are added at startup.
        private static readonly Dictionary<Type, bool> sensitive = new Dictionary<Type, bool>();

        [SystemInitializer(typeof(SkillCatalog))]
        private static void Init()
        {
            foreach (SkillDef skill in SkillCatalog.allSkillDefs)
            {
                Type state = skill.activationState.stateType;
                if (state != null
                    && (skill.cancelSprintingOnActivation || skill.canceledFromSprinting || skill.forceSprintDuringState))
                {
                    sensitive[state] = true;
                }
            }
        }

        internal static bool IsSprintSensitive(Type stateType)
        {
            if (!sensitive.TryGetValue(stateType, out bool result))
            {
                foreach (Type type in extraStates)
                {
                    result |= type.IsAssignableFrom(stateType);
                }
                sensitive[stateType] = result;
            }
            return result;
        }
    }
}

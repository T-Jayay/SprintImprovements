using System;
using HarmonyLib;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RoR2;

namespace SprintImprovements
{
    /// <summary>The mod's hooks: each hands over to <see cref="SprintGuard"/>.</summary>
    internal static class SprintHooks
    {
        // MMHOOK has no hooks for property accessors, so the setter is hooked directly, as the R2Wiki's On Hook page
        // shows for properties.
        private static Hook isSprintingSetterHook;

        internal static void Init()
        {
            IL.RoR2.PlayerCharacterMasterController.PollButtonInput += PlayerCharacterMasterController_PollButtonInput;
            isSprintingSetterHook = new Hook(
                AccessTools.PropertySetter(typeof(CharacterBody), nameof(CharacterBody.isSprinting)),
                CharacterBody_set_isSprinting);
        }

        /// <summary>
        /// BlockForcedSprint: orig isn't called for a forced sprint the player doesn't want, which also skips the hooks
        /// other mods added to this setter before this one.
        /// </summary>
        private static void CharacterBody_set_isSprinting(Action<CharacterBody, bool> orig, CharacterBody self,
            bool value)
        {
            if (SprintGuard.AllowSprintWrite(self, value))
            {
                orig(self, value);
            }
        }

        /// <summary>
        /// Calls <see cref="SprintGuard.OnPoll"/> right after PollButtonInput stores body.isSprinting, the value its
        /// sprint toggle starts from, and before it applies sprintInputPressReceived. If the game no longer reads
        /// isSprinting there, the method stays unchanged and the log says so; the mod then does nothing, since
        /// SprintGuard only acts for players it has seen in OnPoll.
        /// </summary>
        private static void PlayerCharacterMasterController_PollButtonInput(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);
            int sprintingLocal = -1;
            if (!cursor.TryGotoNext(MoveType.After,
                x => x.MatchCallOrCallvirt(
                    AccessTools.PropertyGetter(typeof(CharacterBody), nameof(CharacterBody.isSprinting))),
                x => x.MatchStloc(out sprintingLocal)))
            {
                SprintImprovementsPlugin.Log.LogError(il.Method.Name + " IL hook failed: the game changed. "
                    + "SprintImprovements does nothing until it is updated.");
                return;
            }
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldloc, sprintingLocal);
            cursor.EmitDelegate<Action<PlayerCharacterMasterController, bool>>(SprintGuard.OnPoll);
        }
    }
}

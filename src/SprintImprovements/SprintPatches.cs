using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using EntityStates;
using HarmonyLib;
using RoR2;
using UnityEngine;

namespace SprintImprovements
{
    /// <summary>
    /// The Harmony patches, applied all or nothing (see <see cref="SprintImprovementsPlugin"/>). Each hook only hands
    /// over to <see cref="SprintGuard"/> or <see cref="ServerSprintSync"/>. If that throws, the hook behaves as the
    /// normal game for that call and logs the first error, instead of breaking the game method every tick
    /// (PollButtonInput carries all of a player's input, HandleMovements every character's movement). The methods
    /// the hooks call inside their try are never inlined into them, so the JIT compiles that logic only when a hook
    /// calls it, inside the try: even a game member that a game update removed fails there.
    /// </summary>
    internal static class SprintPatches
    {
        /// <summary>Logs a hook's first unexpected error (it could otherwise repeat every tick).</summary>
        private static void ReportOnce(ref bool reported, string hook, Exception e)
        {
            if (reported)
            {
                return;
            }
            reported = true;
            SprintImprovementsPlugin.Log.LogError($"Unexpected error in the {hook} patch; that call behaved as in the "
                + $"normal game. Later errors there aren't logged. {e}");
        }

        /// <summary>
        /// The player's input poll: SprintGuard decides the sprint value it toggles; ServerSprintSync runs after it.
        /// </summary>
        [HarmonyPatch(typeof(PlayerCharacterMasterController), "PollButtonInput", new Type[0])]
        private static class PollButtonInputPatch
        {
            private const string Hook = "PlayerCharacterMasterController.PollButtonInput";

            // PCMC's pending sprint press: set by Update on a key press, cleared (and applied) by PollButtonInput.
            private const string PressFieldName = "sprintInputPressReceived";

            private static bool inputFailed;
            private static bool syncFailed;

            /// <summary>
            /// The signature of <see cref="SprintInputBase"/>, to get its MethodInfo without looking it up by name.
            /// </summary>
            private delegate bool SprintInputBaseCall(
                CharacterBody body, PlayerCharacterMasterController pcmc, ref bool pressReceived);

            /// <summary>
            /// Called by the patched PollButtonInput in place of body.isSprinting (see
            /// <see cref="SprintGuard.GetSprintInputBase"/>). On an unexpected error it returns the normal game's
            /// value and leaves the press for PCMC.
            /// </summary>
            internal static bool SprintInputBase(
                CharacterBody body, PlayerCharacterMasterController pcmc, ref bool pressReceived)
            {
                bool pressed = pressReceived;
                try
                {
                    return SprintGuard.GetSprintInputBase(body, pcmc, ref pressReceived);
                }
                catch (Exception e)
                {
                    pressReceived = pressed;
                    ReportOnce(ref inputFailed, Hook, e);
                    return body.isSprinting;
                }
            }

            /// <summary>
            /// Replaces PollButtonInput's only read of body.isSprinting with <see cref="SprintInputBase"/>. If the
            /// method doesn't look as expected, it is left alone (see <see cref="SprintGuard.InputPatched"/>).
            /// </summary>
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> code = new List<CodeInstruction>(instructions);
                string mismatch = ReplaceSprintRead(code);
                SprintGuard.InputPatched = mismatch == null;
                if (mismatch == null)
                {
                    SprintImprovementsPlugin.Log.LogInfo($"Patched {Hook} sprint toggle.");
                }
                else
                {
                    SprintImprovementsPlugin.Log.LogError($"Left {Hook} alone because {mismatch} (a game update or "
                        + "another mod?). SprintMode and KeepSprintToggled are unavailable and sprint uses the normal "
                        + "game's toggle; BlockForcedSprint still works.");
                }
                return code;
            }

            /// <summary>
            /// <c>[body] callvirt get_isSprinting</c> becomes
            /// <c>[body] ldarg.0 ldarg.0 ldflda sprintInputPressReceived call SprintInputBase</c>.
            /// </summary>
            /// <returns>Null when done; otherwise what didn't match, and the code is unchanged.</returns>
            private static string ReplaceSprintRead(List<CodeInstruction> code)
            {
                FieldInfo pressField = AccessTools.Field(typeof(PlayerCharacterMasterController), PressFieldName);
                if (pressField == null || pressField.FieldType != typeof(bool))
                {
                    return $"it has no bool field {PressFieldName}";
                }
                MethodInfo getter =
                    AccessTools.PropertyGetter(typeof(CharacterBody), nameof(CharacterBody.isSprinting));
                int index = -1;
                int reads = 0;
                for (int i = 0; i < code.Count; i++)
                {
                    if (code[i].Calls(getter))
                    {
                        index = i;
                        reads++;
                    }
                }
                if (reads != 1)
                {
                    return $"it reads CharacterBody.isSprinting {reads} times instead of once";
                }

                CodeInstruction read = code[index];
                CodeInstruction loadController = new CodeInstruction(OpCodes.Ldarg_0);
                // Whatever jumped to the read (or started a block there) must start at the first added instruction.
                read.MoveLabelsTo(loadController);
                read.MoveBlocksTo(loadController);
                read.opcode = OpCodes.Call;
                read.operand = new SprintInputBaseCall(SprintInputBase).Method;
                code.InsertRange(index, new[]
                {
                    loadController,
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldflda, pressField)
                });
                return null;
            }

            /// <summary>
            /// Runs every tick for each of this game instance's players, even while the UI has focus.
            /// </summary>
            private static void Postfix(CharacterBody ___body)
            {
                try
                {
                    ServerSprintSync.Update(___body);
                }
                catch (Exception e)
                {
                    ReportOnce(ref syncFailed, Hook, e);
                }
            }
        }

        /// <summary>Every write of isSprinting goes through SprintGuard, which can skip a forced sprint.</summary>
        [HarmonyPatch(typeof(CharacterBody), nameof(CharacterBody.isSprinting), MethodType.Setter)]
        private static class IsSprintingSetterPatch
        {
            private static bool failed;

            private static bool Prefix(CharacterBody __instance, bool value)
            {
                try
                {
                    return SprintGuard.AllowSprintWrite(__instance, value);
                }
                catch (Exception e)
                {
                    ReportOnce(ref failed, "CharacterBody.isSprinting setter", e);
                    return true;
                }
            }
        }

        /// <summary>After a skill activates: SprintGuard remembers it if it cancelled sprint.</summary>
        [HarmonyPatch(typeof(GenericSkill), "OnExecute", new Type[0])]
        private static class GenericSkillOnExecutePatch
        {
            private static bool failed;

            private static void Postfix(GenericSkill __instance)
            {
                try
                {
                    SprintGuard.OnSkillExecuted(__instance);
                }
                catch (Exception e)
                {
                    ReportOnce(ref failed, "GenericSkill.OnExecute", e);
                }
            }
        }

        /// <summary>HandleMovements writes sprint from the input: SprintGuard lets it through and tracks it.</summary>
        [HarmonyPatch(typeof(GenericCharacterMain), nameof(GenericCharacterMain.HandleMovements), new Type[0])]
        private static class HandleMovementsPatch
        {
            private const string Hook = "GenericCharacterMain.HandleMovements";

            private static bool failed;

            private static void Prefix(GenericCharacterMain __instance, out SprintGuard.MovementWrite __state)
            {
                __state = default;
                try
                {
                    __state = SprintGuard.BeginMovementWrite(__instance);
                }
                catch (Exception e)
                {
                    ReportOnce(ref failed, Hook, e);
                }
            }

            // ___moveVector: the move input HandleMovements just used (GatherInputs copies it from the input bank).
            private static void Postfix(SprintGuard.MovementWrite __state, Vector3 ___moveVector)
            {
                try
                {
                    SprintGuard.TrackMovementWrite(__state, ___moveVector);
                }
                catch (Exception e)
                {
                    ReportOnce(ref failed, Hook, e);
                }
            }

            // No try needed: EndMovementWrite only restores a field of this mod and touches no game member.
            private static void Finalizer(SprintGuard.MovementWrite __state)
            {
                SprintGuard.EndMovementWrite(__state);
            }
        }
    }
}

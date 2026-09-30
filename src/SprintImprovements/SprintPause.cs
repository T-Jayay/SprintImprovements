using EntityStates;
using RoR2;
using RoR2.Skills;
using UnityEngine;

namespace SprintImprovements
{
    /// <summary>What keeps a wanted sprint paused, and when the player's movement may start it again.</summary>
    internal static class SprintPause
    {
        // Resume a paused sprint only on a clear forward move, a little more than the normal game needs to keep
        // sprinting (a move input above 0.5, within 60 degrees of where you aim), so sprint doesn't flicker when the
        // stick or the aim sits right at the limit.
        private const float ResumeMinMove = 0.6f;
        private const float ResumeMaxAimAngle = 50f;

        private static readonly float resumeMinAimMoveDot = Mathf.Cos(ResumeMaxAimAngle * Mathf.Deg2Rad);

        /// <summary>
        /// True while something should keep sprint off, or keeps the body sprinting: the body is in a skill or stun
        /// state instead of normal movement, a state machine runs (or ran and hasn't finished) a sprint-sensitive state,
        /// or the player holds the button of a sprint-sensitive skill that is ready. Call it every tick: it remembers
        /// the state machines it saw in a sprint-sensitive state.
        /// </summary>
        internal static bool IsHeldOff(CharacterBody body, PlayerSprintState state)
        {
            // The body is in a skill, stun, shock, frozen, ... state instead of normal movement.
            bool held = IsBusy(state.bodyMachine);

            // A state machine running a sprint-sensitive state, and after it until the machine returns to its main
            // state (charge -> fire chains, a scope's follow-up states, ...).
            foreach (EntityStateMachine machine in state.machines)
            {
                EntityState current = machine ? machine.state : null;
                if (current != null && !machine.IsInMainState() && SprintStates.IsSprintSensitive(current.GetType())
                    && !state.heldMachines.Contains(machine))
                {
                    state.heldMachines.Add(machine);
                }
            }
            for (int i = state.heldMachines.Count - 1; i >= 0; i--)
            {
                if (IsBusy(state.heldMachines[i]))
                {
                    held = true;
                }
                else
                {
                    state.heldMachines.RemoveAt(i);
                }
            }

            // The button of a skill that cancels sprint, or that sprinting cancels, is held and can use the skill, so
            // holding fire doesn't flicker sprint between shots.
            SkillLocator skills = body.skillLocator;
            InputBankTest input = body.inputBank;
            return held || (skills && input && (IsSkillHeld(skills.primary, input.skill1)
                || IsSkillHeld(skills.secondary, input.skill2) || IsSkillHeld(skills.utility, input.skill3)
                || IsSkillHeld(skills.special, input.skill4)));
        }

        /// <summary>
        /// Whether the player's movement may start a paused sprint (see <see cref="ResumeMinMove"/>).
        /// </summary>
        internal static bool CanStartMoving(CharacterBody body)
        {
            InputBankTest input = body.inputBank;
            if (!input)
            {
                return false;
            }
            Vector3 move = input.moveVector;
            if (move.magnitude <= ResumeMinMove)
            {
                return false;
            }
            if ((body.bodyFlags & CharacterBody.BodyFlags.SprintAnyDirection) != 0)
            {
                return true;
            }
            Vector3 aim = input.aimDirection;
            aim.y = 0f;
            move.y = 0f;
            return Vector3.Dot(aim.normalized, move.normalized) >= resumeMinAimMoveDot;
        }

        /// <summary>Whether the machine is in (or about to enter) a state other than its main state.</summary>
        internal static bool IsBusy(EntityStateMachine machine)
        {
            return machine && (!machine.IsInMainState() || machine.HasPendingState());
        }

        /// <summary>
        /// Whether the button of this sprint-sensitive skill is held in a way that would use it, as
        /// GenericCharacterMain.HandleSkill decides: a press that a must-press skill already used, or a skill that
        /// isn't ready (no stock, cooldown), doesn't count.
        /// </summary>
        private static bool IsSkillHeld(GenericSkill slot, InputBankTest.ButtonState button)
        {
            SkillDef def = slot ? slot.skillDef : null;
            return def && (def.cancelSprintingOnActivation || def.canceledFromSprinting) && button.down
                && (!def.mustKeyPress || !button.hasPressBeenClaimed) && slot.IsReady();
        }
    }
}

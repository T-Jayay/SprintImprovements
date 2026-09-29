using System.Collections.Generic;
using EntityStates;
using RoR2;
using RoR2.Skills;
using UnityEngine;

namespace SprintImprovements
{
    /// <summary>What keeps a wanted sprint paused, and when it may start again.</summary>
    internal static class SprintPause
    {
        // Wait this long after the last thing holding sprint off before resuming, so sprint doesn't cut off the end of
        // attack animations (RTAutoSprintEx waits about as long).
        private const float ResumeDelay = 0.15f;

        // Resume a paused sprint only on a clear forward move, a little more than the normal game needs to keep
        // sprinting (a move input above 0.5, within 60 degrees of where you aim), so sprint doesn't flicker when the
        // stick or the aim sits right at the limit.
        private const float ResumeMinMove = 0.6f;
        private const float ResumeMaxAimAngle = 50f;

        private const string BodyMachineName = "Body";

        private static readonly float resumeMinAimMoveDot = Mathf.Cos(ResumeMaxAimAngle * Mathf.Deg2Rad);

        /// <summary>
        /// Whether a wanted sprint may come back now: nothing holds it off, and nothing has for
        /// <see cref="ResumeDelay"/>. Call it every tick, so the delay counts from the last tick something held
        /// sprint off.
        /// </summary>
        internal static bool CanResume(CharacterBody body, BodySprintState state)
        {
            float now = Time.fixedTime;
            if (IsHeldOff(body, state))
            {
                state.lastBlockedTime = now;
                return false;
            }
            return now - state.lastBlockedTime >= ResumeDelay;
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

        /// <summary>
        /// True while something should keep sprint off. Also drops finished skills from
        /// <see cref="BodySprintState.cancelSkills"/> and caches the body's state machines in the state.
        /// </summary>
        private static bool IsHeldOff(CharacterBody body, BodySprintState state)
        {
            // A skill that cancelled sprint: its state machine hasn't returned to its main state (charge -> fire
            // chains, flamethrowers, scopes, stances, ...), or its button is held and can use it again (so holding M1
            // doesn't flicker sprint between shots).
            bool held = false;
            List<GenericSkill> skills = state.cancelSkills;
            for (int i = skills.Count - 1; i >= 0; i--)
            {
                GenericSkill slot = skills[i];
                if (slot && (IsBusy(slot.stateMachine) || IsSkillHeld(body, slot)))
                {
                    held = true;
                }
                else
                {
                    skills.RemoveAt(i);
                }
            }
            if (held)
            {
                return true;
            }

            // The body is in a skill, stun, shock, frozen, ... state instead of normal movement.
            if (!state.bodyMachineSearched)
            {
                state.bodyMachine = EntityStateMachine.FindByCustomName(body.gameObject, BodyMachineName);
                state.bodyMachineSearched = true;
            }
            if (IsBusy(state.bodyMachine))
            {
                return true;
            }

            // Any state machine running a state that ends or changes when sprint turns on.
            if (state.machines == null)
            {
                state.machines = body.GetComponents<EntityStateMachine>();
            }
            foreach (EntityStateMachine machine in state.machines)
            {
                EntityState current = machine ? machine.state : null;
                if (current != null && SprintSensitiveStates.Contains(current.GetType()))
                {
                    return true;
                }
            }

            // Any sprint-sensitive skill (it cancels sprint when used, or sprinting cancels it) that is running its
            // activation state or whose button is held and can use it, even if it wasn't started through OnExecute.
            SkillLocator locator = body.skillLocator;
            if (!locator)
            {
                return false;
            }
            foreach (GenericSkill slot in locator.AllSkills)
            {
                SkillDef def = slot ? slot.skillDef : null;
                if (!def || !(def.cancelSprintingOnActivation || def.canceledFromSprinting))
                {
                    continue;
                }
                EntityStateMachine machine = slot.stateMachine;
                if (machine && machine.state != null && !machine.IsInMainState() && def.IsAlreadyInState(slot))
                {
                    return true;
                }
                if (IsSkillHeld(body, slot))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsBusy(EntityStateMachine machine)
        {
            return machine && (!machine.IsInMainState() || machine.HasPendingState());
        }

        /// <summary>
        /// Whether the skill's button is held in a way that would use the skill, as GenericCharacterMain.HandleSkill
        /// decides: a press that a must-press skill already used, or a skill that isn't ready (no stock, cooldown),
        /// doesn't count.
        /// </summary>
        private static bool IsSkillHeld(CharacterBody body, GenericSkill slot)
        {
            InputBankTest input = body.inputBank;
            SkillLocator locator = body.skillLocator;
            SkillDef def = slot.skillDef;
            if (!input || !locator || !def)
            {
                return false;
            }
            InputBankTest.ButtonState button;
            if (slot == locator.primary)
            {
                button = input.skill1;
            }
            else if (slot == locator.secondary)
            {
                button = input.skill2;
            }
            else if (slot == locator.utility)
            {
                button = input.skill3;
            }
            else if (slot == locator.special)
            {
                button = input.skill4;
            }
            else
            {
                return false;
            }
            return button.down && (!def.mustKeyPress || !button.hasPressBeenClaimed) && slot.IsReady();
        }
    }
}

using System.Runtime.CompilerServices;
using RoR2;
using UnityEngine;

namespace SprintImprovements
{
    /// <summary>
    /// Makes sprint change only when the player wants it to.
    ///
    /// The normal game's sprint is a toggle whose only memory is <see cref="CharacterBody.isSprinting"/>: every tick
    /// PlayerCharacterMasterController.PollButtonInput reads it, flips it if the sprint key was pressed
    /// (sprintInputPressReceived), turns it off unless the player moves within 60 degrees of where they aim, and passes
    /// it to the body's input; <see cref="EntityStates.GenericCharacterMain.HandleMovements"/> then sets the body's
    /// sprint from that input (off at a move input of 0.5 or less), which the game sends to the server. Skills and stuns
    /// set the body's sprint directly, so the toggle is lost: a skill that forces sprint on (Mercenary's dash) leaves
    /// the player sprinting, and one that cancels sprint (Commando's pistols) or a stun leaves them walking.
    ///
    /// This keeps what the player wants in <see cref="PlayerSprintState.wantsSprint"/>, and right after the poll reads
    /// isSprinting, presses the sprint key for the player when sprint should start or stop: sprint resumes once nothing
    /// holds it off (<see cref="SprintPause"/>). The game's own input path then changes sprint, networking included.
    /// The one exception (BlockForcedSprint): a skill's write of "sprinting" is skipped while the player doesn't want
    /// to sprint and the sprint input is up (<see cref="AllowSprintWrite"/>); see <see cref="ServerSprintSync"/> for
    /// what that means for the server.
    /// </summary>
    internal static class SprintGuard
    {
        // Wait this long after the last thing holding sprint off before resuming, so sprint doesn't cut off the end of
        // attack animations (RTAutoSprintEx waits about as long).
        private const float ResumeDelay = 0.15f;

        // Weak, so the entries of players who left can be freed. Split-screen players each have their own entry.
        private static readonly ConditionalWeakTable<PlayerCharacterMasterController, PlayerSprintState> players =
            new ConditionalWeakTable<PlayerCharacterMasterController, PlayerSprintState>();

        // Bumped when Enabled or SprintMode changes, so each player starts again from what they see.
        private static int modeVersion;

        internal static void OnModeChanged()
        {
            modeVersion++;
        }

        /// <summary>
        /// Whether a write of <paramref name="value"/> to the body's isSprinting may go through. Skills that force sprint
        /// on (Mercenary's dashes, forceSprintDuringState skills) are skipped while a player of this game instance
        /// doesn't want to sprint. The sprint input's own write (HandleMovements) goes through, because the input is
        /// down then; so does sprint input from another mod. Writes of false are never skipped.
        /// </summary>
        internal static bool AllowSprintWrite(CharacterBody body, bool value)
        {
            if (!value || body.isSprinting || !PluginConfig.Enabled.Value || !PluginConfig.BlockForcedSprint.Value
                || !body.hasEffectiveAuthority || body.isRemoteOp)
            {
                return true;
            }
            CharacterMaster master = body.master;
            PlayerCharacterMasterController pcmc = master ? master.playerCharacterMasterController : null;
            if (!pcmc || !players.TryGetValue(pcmc, out PlayerSprintState state) || state.wantsSprint)
            {
                return true;
            }
            InputBankTest input = body.inputBank;
            if (input && input.sprint.down)
            {
                return true;
            }
            ServerSprintSync.OnForcedSprintBlocked(state);
            return false;
        }

        /// <summary>
        /// Runs in PollButtonInput for each of this game instance's players, right after it reads body.isSprinting
        /// (<paramref name="sprinting"/>) and before it applies the sprint key's press.
        /// </summary>
        internal static void OnPoll(PlayerCharacterMasterController pcmc, bool sprinting)
        {
            CharacterBody body = pcmc.body;
            if (!PluginConfig.Enabled.Value || body.isRemoteOp)
            {
                // Remote-operated drones keep the normal game's sprint.
                return;
            }
            PlayerSprintState state = GetState(pcmc, body, sprinting);
            ServerSprintSync.Update(body, state);
            float now = Time.fixedTime;
            bool heldOff = SprintPause.IsHeldOff(body, state);
            if (heldOff)
            {
                state.lastHeldOffTime = now;
            }
            SprintKeyMode mode = PluginConfig.SprintMode.Value;
            bool sprintNow = false;
            switch (mode)
            {
                case SprintKeyMode.Hold:
                    // A fresh press sprints straight away, as in the normal game, even if that cancels a skill that
                    // sprinting cancels.
                    sprintNow = TakePress(pcmc);
                    state.wantsSprint = IsSprintKeyHeld(pcmc);
                    break;
                case SprintKeyMode.Always:
                    TakePress(pcmc);
                    state.wantsSprint = !IsSprintKeyHeld(pcmc);
                    break;
                case SprintKeyMode.Toggle:
                    if (pcmc.sprintInputPressReceived)
                    {
                        OnTogglePress(pcmc, body, state, sprinting, heldOff);
                        return;
                    }
                    break;
            }

            if (state.wantsSprint)
            {
                if (!sprinting && (sprintNow
                    || (!heldOff && now - state.lastHeldOffTime >= ResumeDelay && SprintPause.CanStartMoving(body))))
                {
                    Press(pcmc);
                }
            }
            else if (sprinting && !heldOff)
            {
                if (mode == SprintKeyMode.Toggle && !PluginConfig.BlockForcedSprint.Value)
                {
                    // A skill's forced sprint carries on and becomes the toggle, as in the normal game.
                    state.wantsSprint = true;
                }
                else
                {
                    // Sprinting without wanting to, and no skill keeps the body sprinting any more: the player stopped
                    // sprinting during a skill that kept the body sprinting, or let go of the key in Hold mode.
                    Press(pcmc);
                }
            }
        }

        /// <summary>
        /// The player pressed sprint in Toggle mode. PCMC flips the sprint value it read with the press, unless it is
        /// taken back here.
        /// </summary>
        private static void OnTogglePress(PlayerCharacterMasterController pcmc, CharacterBody body,
            PlayerSprintState state, bool sprinting, bool heldOff)
        {
            if (sprinting && (state.wantsSprint || !heldOff))
            {
                // The press stops the sprint the player sees (after the skill, if a skill keeps the body sprinting) and
                // turns the toggle off.
                state.wantsSprint = false;
            }
            else if (!state.wantsSprint)
            {
                // The toggle turns on. During a skill's forced sprint, the sprint just carries on afterwards.
                state.wantsSprint = true;
                if (sprinting)
                {
                    TakePress(pcmc);
                }
            }
            else if (!SprintPause.CanStartMoving(body) || SprintPause.IsBusy(state.bodyMachine))
            {
                // On but paused while standing still or strafing, or during a stun or a skill that takes over the
                // body's movement (the press couldn't start a sprint then): the press turns the toggle off.
                state.wantsSprint = false;
                TakePress(pcmc);
            }
            // Otherwise on but paused while moving forward: the press sprints straight away, as in the normal game (this
            // can cancel a skill that sprinting cancels, such as a scope), and the toggle stays on.
        }

        private static PlayerSprintState GetState(PlayerCharacterMasterController pcmc, CharacterBody body,
            bool sprinting)
        {
            PlayerSprintState state = players.GetOrCreateValue(pcmc);
            if (state.modeVersion != modeVersion)
            {
                // A new player, or the settings changed: start from what the player sees.
                state.modeVersion = modeVersion;
                state.wantsSprint = sprinting;
            }
            if (state.body != body)
            {
                state.SetBody(body);
            }
            return state;
        }

        /// <summary>Presses the sprint key for the player this tick: PCMC flips the sprint value it read.</summary>
        private static void Press(PlayerCharacterMasterController pcmc)
        {
            pcmc.sprintInputPressReceived = true;
        }

        /// <summary>Takes back the player's press this tick, so PCMC doesn't flip sprint; returns whether there was one.</summary>
        private static bool TakePress(PlayerCharacterMasterController pcmc)
        {
            bool pressed = pcmc.sprintInputPressReceived;
            pcmc.sprintInputPressReceived = false;
            return pressed;
        }

        private static bool IsSprintKeyHeld(PlayerCharacterMasterController pcmc)
        {
            NetworkUser user = pcmc.networkUser;
            Rewired.Player input = user ? user.inputPlayer : null;
            return input != null && input.GetButton(RewiredConsts.Action.Sprint);
        }
    }
}

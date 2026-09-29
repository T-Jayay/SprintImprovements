using System.Runtime.CompilerServices;
using EntityStates;
using RoR2;
using RoR2.Skills;
using UnityEngine;

namespace SprintImprovements
{
    /// <summary>
    /// Makes sprint change only when the player wants it to, in both directions.
    ///
    /// The normal game's sprint is a toggle whose only memory is <see cref="CharacterBody.isSprinting"/>: every tick
    /// PlayerCharacterMasterController.PollButtonInput takes body.isSprinting, flips it if the sprint key was pressed,
    /// turns it off unless the player moves within 60 degrees of where they aim, and pushes it into the input bank.
    /// <see cref="GenericCharacterMain.HandleMovements"/> writes that back to the body (off at a move input of 0.5 or
    /// less); that write is the only one that follows the player's input. Anything else that writes the body's sprint
    /// changes the toggle: skills that force sprint on (Mercenary's dash, forceSprintDuringState skills) leave the
    /// player sprinting, and skills that cancel sprint (cancelSprintingOnActivation) and stuns leave them walking.
    ///
    /// This keeps what the player wants separately (<see cref="PlayerSprintState.wantsSprint"/>, per player; with
    /// KeepSprintToggled it carries over to the player's next body):
    /// - Toggle mode with KeepSprintToggled: only the sprint key flips it (plus the movement code turning sprint on
    ///   from off, e.g. for an auto-sprint mod). Stopping, strafing, skills and stuns only pause sprint, and a press
    ///   while paused and moving forward sprints at once instead of flipping it.
    /// - Toggle mode without it (the normal game's toggle): it follows HandleMovements' writes, except that "not
    ///   sprinting" is ignored while something holds sprint off.
    /// - Hold and Always: whether the sprint key is held (Hold, where a fresh press also sprints at once) or not held
    ///   (Always), read every tick.
    /// Block (BlockForcedSprint): a write of true from anywhere but HandleMovements is skipped while the player
    /// doesn't want to sprint, unless the sprint input is down (e.g. held by an auto-sprint mod). Writes of false are
    /// never blocked.
    /// Resume: PollButtonInput's read of body.isSprinting is replaced by <see cref="GetSprintInputBase"/>, which also
    /// gives true while the player wants to sprint and nothing holds sprint off any more (<see cref="SprintPause"/>).
    /// PCMC's own aim/move check and the normal input -> HandleMovements path then turn sprint back on, networking
    /// included.
    ///
    /// Only bodies this game instance controls for a player are affected, not remote-operated drones. The hooks are in
    /// <see cref="SprintPatches"/>; <see cref="ServerSprintSync"/> keeps the server's copy of a client's body in step.
    /// </summary>
    internal static class SprintGuard
    {
        // HandleMovements turns sprint off at a move input of this or less.
        private const float VanillaSprintMinMove = 0.5f;

        // "This tick or the previous one": a time recorded then is at most one tick old, and the extra half tick is
        // margin for float rounding. PCMC runs before the state machines (script execution order -8 vs 0), so the
        // poll sees what skills did in the previous tick, and HandleMovements sees what the poll did in this one.
        private const float RecentTicks = 1.5f;

        // Weak tables, so the entries of destroyed masters and bodies can be freed. Split-screen players each have
        // their own master and body.
        private static readonly ConditionalWeakTable<CharacterMaster, PlayerSprintState> players =
            new ConditionalWeakTable<CharacterMaster, PlayerSprintState>();

        private static readonly ConditionalWeakTable<CharacterBody, BodySprintState> bodies =
            new ConditionalWeakTable<CharacterBody, BodySprintState>();

        // The body whose HandleMovements is running: its sprint write follows the player's input.
        private static CharacterBody inputWriteBody;

        // Bumped when a setting that changes how the toggle works changes, so each player re-derives it once.
        private static int modeVersion;

        /// <summary>Set once every patch is in (patching is all or nothing).</summary>
        internal static bool Patched { get; set; }

        /// <summary>
        /// Whether PollButtonInput calls <see cref="GetSprintInputBase"/>. Without it, SprintMode and KeepSprintToggled
        /// can't work and sprint uses the normal game's toggle; BlockForcedSprint still works.
        /// </summary>
        internal static bool InputPatched { get; set; }

        private static bool Active => Patched && PluginConfig.Enabled.Value;

        private static bool BlockActive => Active && PluginConfig.BlockForcedSprint.Value;

        /// <summary>The sprint mode in effect (Toggle while the mod is off or the input isn't patched).</summary>
        private static SprintKeyMode ActiveMode =>
            Active && InputPatched ? PluginConfig.SprintMode.Value : SprintKeyMode.Toggle;

        /// <summary>Toggle mode with KeepSprintToggled: only the sprint key changes the toggle.</summary>
        private static bool KeepsToggle => Active && InputPatched
            && PluginConfig.SprintMode.Value == SprintKeyMode.Toggle && PluginConfig.KeepSprintToggled.Value;

        /// <summary>What the HandleMovements patch carries from before the call to after it.</summary>
        internal struct MovementWrite
        {
            internal CharacterBody body;
            internal CharacterBody previousInputBody;
            internal bool wasSprinting;
        }

        /// <summary>
        /// A setting that changes how the toggle works changed: each player re-derives what they want once.
        /// </summary>
        internal static void OnModeChanged()
        {
            modeVersion++;
        }

        /// <summary>Whether <paramref name="time"/> is this fixed tick or the previous one.</summary>
        internal static bool IsRecent(float time)
        {
            return Time.fixedTime - time <= Time.fixedDeltaTime * RecentTicks;
        }

        internal static bool TryGetState(CharacterBody body, out BodySprintState state)
        {
            return bodies.TryGetValue(body, out state);
        }

        /// <summary>
        /// Replaces PollButtonInput's read of body.isSprinting, the value its sprint toggle starts from. PCMC then
        /// flips the result if <paramref name="pressReceived"/> is still set (and clears it), and turns it off unless
        /// the player moves within 60 degrees of where they aim.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool GetSprintInputBase(
            CharacterBody body, PlayerCharacterMasterController pcmc, ref bool pressReceived)
        {
            bool sprinting = body.isSprinting;
            if (!Active || !IsLocalPlayerBody(body))
            {
                return sprinting;
            }
            BodySprintState state = GetState(body);
            bool canResume = SprintPause.CanResume(body, state);
            PlayerSprintState player = state.player;
            if (player.modeVersion != modeVersion)
            {
                // How the toggle works changed: start from what the player sees, so sprint can't stick on or off.
                player.modeVersion = modeVersion;
                player.wantsSprint = sprinting;
            }

            switch (ActiveMode)
            {
                case SprintKeyMode.Hold:
                    return HoldInput(body, pcmc, player, sprinting, canResume, ref pressReceived);
                case SprintKeyMode.Always:
                    return AlwaysInput(body, pcmc, player, sprinting, canResume, ref pressReceived);
                case SprintKeyMode.Toggle:
                default:
                    return KeepsToggle
                        ? KeptToggleInput(body, player, sprinting, canResume, pressReceived)
                        : NormalToggleInput(state, sprinting, canResume, pressReceived);
            }
        }

        /// <summary>Whether a write of <paramref name="value"/> to the body's isSprinting may go through.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool AllowSprintWrite(CharacterBody body, bool value)
        {
            if (value == body.isSprinting || !Active || ReferenceEquals(body, inputWriteBody)
                || !IsLocalPlayerBody(body))
            {
                return true;
            }
            BodySprintState state = GetState(body);
            if (!value)
            {
                // Turning sprint off is never blocked. Something other than the sprint input did it (a skill, a stun,
                // a zipline, ...): wait a moment before resuming.
                state.lastBlockedTime = Time.fixedTime;
                return true;
            }
            if (!BlockActive || state.player.wantsSprint)
            {
                // BlockForcedSprint is off, or the player wants to sprint (was sprinting before the skill, or turned
                // sprint on during it).
                return true;
            }
            InputBankTest inputBank = body.inputBank;
            if (inputBank && inputBank.sprint.down)
            {
                // Sprint input from another source this tick (e.g. an auto-sprint mod).
                return true;
            }
            ServerSprintSync.OnForcedSprintBlocked(state);
            return false;
        }

        /// <summary>
        /// After a skill activated: remember skills that cancelled sprint, so it stays paused while they run.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void OnSkillExecuted(GenericSkill skill)
        {
            if (!Active)
            {
                return;
            }
            SkillDef def = skill.skillDef;
            if (!def || !def.cancelSprintingOnActivation)
            {
                return;
            }
            CharacterBody body = skill.characterBody;
            if (!body || !IsLocalPlayerBody(body))
            {
                return;
            }
            BodySprintState state = GetState(body);
            if (!state.cancelSkills.Contains(skill))
            {
                state.cancelSkills.Add(skill);
            }
            state.lastBlockedTime = Time.fixedTime;
        }

        /// <summary>HandleMovements is about to write the body's sprint from the player's input.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static MovementWrite BeginMovementWrite(GenericCharacterMain movementState)
        {
            EntityStateMachine outer = movementState.outer;
            CharacterBody body = outer ? outer.commonComponents.characterBody : null;
            MovementWrite write = new MovementWrite
            {
                body = body,
                previousInputBody = inputWriteBody,
                wasSprinting = body && body.isSprinting
            };
            inputWriteBody = body;
            return write;
        }

        /// <summary>
        /// HandleMovements wrote the body's sprint from the player's input: in Toggle mode, update what the player
        /// wants from it. <paramref name="moveVector"/> is the move input HandleMovements used.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void TrackMovementWrite(MovementWrite write, Vector3 moveVector)
        {
            CharacterBody body = write.body;
            if (!body || !Active || ActiveMode != SprintKeyMode.Toggle || !IsLocalPlayerBody(body))
            {
                // Hold and Always read the key every poll instead.
                return;
            }
            BodySprintState state = GetState(body);
            PlayerSprintState player = state.player;
            bool sprinting = body.isSprinting;
            if (KeepsToggle)
            {
                // Only the sprint key changes the toggle, plus sprint input that turns sprint on from elsewhere (e.g.
                // an auto-sprint mod). Only an off -> on change counts, so the tick after the player pressed sprint off
                // (body still sprinting) can't turn the toggle back on.
                if (sprinting && !write.wasSprinting)
                {
                    player.wantsSprint = true;
                }
            }
            else if (sprinting)
            {
                player.wantsSprint = true;
            }
            else if (moveVector.magnitude <= VanillaSprintMinMove)
            {
                // Standing still ends sprint, as in the normal game.
                player.wantsSprint = false;
            }
            else if (!IsRecent(state.suppressedTime))
            {
                // Sprint input off while nothing held it back: the player turned it off, or PCMC's aim/move check did
                // (strafing, as in the normal game). The poll sets suppressedTime earlier in this same tick.
                player.wantsSprint = false;
            }
        }

        /// <summary>HandleMovements is done (or threw): later sprint writes don't come from the input.</summary>
        internal static void EndMovementWrite(MovementWrite write)
        {
            inputWriteBody = write.previousInputBody;
        }

        /// <summary>Hold mode: sprint while the key is held.</summary>
        private static bool HoldInput(CharacterBody body, PlayerCharacterMasterController pcmc,
            PlayerSprintState player, bool sprinting, bool canResume, ref bool pressReceived)
        {
            // The key isn't a toggle here: take PCMC's pending press so it can't flip the value returned.
            bool pressed = pressReceived;
            pressReceived = false;
            bool held = IsSprintKeyHeld(pcmc);
            player.wantsSprint = held;
            // A fresh press sprints now, as in the normal game, even if that cancels a skill that sprinting cancels.
            return held && (sprinting || pressed || (canResume && SprintPause.CanStartMoving(body)));
        }

        /// <summary>
        /// Always mode: sprint whenever possible, and walk while the key is held. There is no "sprint now" input, so
        /// sprint never cancels a skill in this mode: it only resumes once nothing holds it off.
        /// </summary>
        private static bool AlwaysInput(CharacterBody body, PlayerCharacterMasterController pcmc,
            PlayerSprintState player, bool sprinting, bool canResume, ref bool pressReceived)
        {
            // The key isn't a toggle here: take PCMC's pending press so it can't flip the value returned.
            pressReceived = false;
            bool walking = IsSprintKeyHeld(pcmc);
            player.wantsSprint = !walking;
            return !walking && (sprinting || (canResume && SprintPause.CanStartMoving(body)));
        }

        /// <summary>
        /// Toggle mode with KeepSprintToggled: only the sprint key changes the toggle; stopping, strafing, skills and
        /// stuns only pause sprint.
        /// </summary>
        private static bool KeptToggleInput(
            CharacterBody body, PlayerSprintState player, bool sprinting, bool canResume, bool pressed)
        {
            if (pressed)
            {
                return OnTogglePress(body, player, sprinting);
            }
            if (sprinting && !player.wantsSprint && BlockActive)
            {
                // Turned off during a skill that keeps the body sprinting: HandleMovements doesn't run then, so the
                // one "off" tick from the press was lost. With BlockForcedSprint on, sprinting without wanting to
                // only happens this way, or while another mod holds the sprint input (its input then wins anyway).
                return false;
            }
            return sprinting || (player.wantsSprint && canResume && SprintPause.CanStartMoving(body));
        }

        /// <summary>
        /// A sprint press in Toggle mode with KeepSprintToggled. The press stays set: PCMC clears it and flips the
        /// value returned here, then applies its aim/move check. So returning false means "sprint now" and true
        /// means "stop".
        /// </summary>
        private static bool OnTogglePress(CharacterBody body, PlayerSprintState player, bool sprinting)
        {
            if (!sprinting && player.wantsSprint && SprintPause.CanStartMoving(body))
            {
                // On but paused (a skill, a stun, the resume delay) while moving forward: sprint now, as a press does
                // in the normal game, even if that ends a skill that sprinting ends. The toggle stays on.
                return false;
            }
            // Otherwise the press flips the toggle. Sprinting counts as on, so a press always stops a sprint the
            // player can see (e.g. one an auto-sprint mod started); paused while standing still or strafing counts as
            // on too, so that press turns it off.
            bool wasOn = player.wantsSprint || sprinting;
            player.wantsSprint = !wasOn;
            return wasOn;
        }

        /// <summary>
        /// Toggle mode without KeepSprintToggled: the normal game's toggle, plus the forced-sprint block.
        /// </summary>
        private static bool NormalToggleInput(BodySprintState state, bool sprinting, bool canResume, bool pressed)
        {
            PlayerSprintState player = state.player;
            if (pressed)
            {
                // PCMC flips the value returned: the key toggles what the player sees.
                player.wantsSprint = !sprinting;
            }
            else if (player.wantsSprint && !sprinting && !canResume)
            {
                // Something holds sprint off, so HandleMovements must not take the resulting "not sprinting" as the
                // player's choice. This keeps the toggle until the skill ends, so a skill that cancels sprint and then
                // forces it on again (Bandit's Smoke Bomb, Seeker's Sojourn) isn't blocked when you were sprinting.
                state.suppressedTime = Time.fixedTime;
            }
            return sprinting;
        }

        /// <summary>
        /// The state of one of this game instance's player bodies, created on first use (callers check
        /// <see cref="IsLocalPlayerBody"/> first, so the body has a master).
        /// </summary>
        private static BodySprintState GetState(CharacterBody body)
        {
            if (bodies.TryGetValue(body, out BodySprintState state))
            {
                return state;
            }
            CharacterMaster master = body.master;
            if (!players.TryGetValue(master, out PlayerSprintState player))
            {
                // The player's first body seen while the mod is on: start from what the player sees.
                player = new PlayerSprintState { wantsSprint = body.isSprinting, modeVersion = modeVersion };
                players.Add(master, player);
            }
            else if (!KeepsToggle)
            {
                // A new body. Only the kept toggle carries over: the normal game's toggle starts from the new body
                // (Hold and Always read the key every poll anyway).
                player.wantsSprint = body.isSprinting;
            }
            state = new BodySprintState(player);
            bodies.Add(body, state);
            return state;
        }

        /// <summary>
        /// A body this game instance controls for a player (host or client, any split-screen player).
        /// </summary>
        private static bool IsLocalPlayerBody(CharacterBody body)
        {
            if (!body.hasEffectiveAuthority || body.isRemoteOp)
            {
                return false;
            }
            CharacterMaster master = body.master;
            return master && master.playerCharacterMasterController;
        }

        private static bool IsSprintKeyHeld(PlayerCharacterMasterController pcmc)
        {
            NetworkUser user = pcmc.networkUser;
            Rewired.Player input = user ? user.inputPlayer : null;
            return input != null && input.GetButton(RewiredConsts.Action.Sprint);
        }
    }
}

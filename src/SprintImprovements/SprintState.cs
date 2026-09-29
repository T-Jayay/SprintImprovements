using System.Collections.Generic;
using RoR2;

namespace SprintImprovements
{
    /// <summary>
    /// What a player wants from sprint. Kept per <see cref="CharacterMaster"/>, so with KeepSprintToggled the toggle
    /// carries over to the player's next body (the next stage, a revive, turning into the Heretic).
    /// </summary>
    internal sealed class PlayerSprintState
    {
        /// <summary>
        /// Whether the player wants to sprint: the toggle in Toggle mode (it stays on while sprint is paused), the key
        /// held in Hold mode, the key not held in Always mode.
        /// </summary>
        internal bool wantsSprint;

        /// <summary>The <see cref="SprintGuard"/> mode version <see cref="wantsSprint"/> was derived for.</summary>
        internal int modeVersion;
    }

    /// <summary>Bookkeeping for one body of one of this game instance's players.</summary>
    internal sealed class BodySprintState
    {
        /// <summary>The player the body belongs to, shared with the player's other bodies.</summary>
        internal readonly PlayerSprintState player;

        /// <summary>Skills that cancelled sprint when they were activated and may still be running.</summary>
        internal readonly List<GenericSkill> cancelSkills = new List<GenericSkill>();

        /// <summary>The body's "Body" state machine (null if it has none).</summary>
        internal EntityStateMachine bodyMachine;

        /// <summary>
        /// Whether <see cref="bodyMachine"/> was looked up, so a body without one isn't searched every tick.
        /// </summary>
        internal bool bodyMachineSearched;

        /// <summary>All of the body's state machines, looked up on first use.</summary>
        internal EntityStateMachine[] machines;

        /// <summary>
        /// When something last held sprint off: a running skill, a stun, or a cancel that didn't come from the input.
        /// </summary>
        internal float lastBlockedTime = float.NegativeInfinity;

        /// <summary>Normal toggle only: when the poll last held back a wanted sprint that couldn't resume.</summary>
        internal float suppressedTime = float.NegativeInfinity;

        /// <summary>When a forced sprint was last blocked (a client then resyncs the server).</summary>
        internal float lastForceBlockTime = float.NegativeInfinity;

        /// <summary>Client only: when "not sprinting" was last sent to the server.</summary>
        internal float lastSyncTime = float.NegativeInfinity;

        internal BodySprintState(PlayerSprintState player)
        {
            this.player = player;
        }
    }
}

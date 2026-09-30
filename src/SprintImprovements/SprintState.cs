using System.Collections.Generic;
using RoR2;

namespace SprintImprovements
{
    /// <summary>
    /// What one of this game instance's players wants from sprint, and the state machines of their current body. Kept
    /// per player, so the sprint toggle carries over to the player's next body (the next stage, a revive, turning into
    /// the Heretic).
    /// </summary>
    internal sealed class PlayerSprintState
    {
        /// <summary>
        /// Whether the player wants to sprint: the toggle in Toggle mode (it stays on while sprint is paused), the key
        /// held in Hold mode, the key not held in Always mode.
        /// </summary>
        internal bool wantsSprint;

        /// <summary>
        /// The <see cref="SprintGuard"/> mode version <see cref="wantsSprint"/> was derived for (-1: not yet).
        /// </summary>
        internal int modeVersion = -1;

        /// <summary>The body the state machines below belong to.</summary>
        internal CharacterBody body;

        /// <summary>The body's "Body" state machine (null if it has none).</summary>
        internal EntityStateMachine bodyMachine;

        /// <summary>All of the body's state machines.</summary>
        internal EntityStateMachine[] machines;

        /// <summary>
        /// State machines that ran a sprint-sensitive state and haven't returned to their main state since.
        /// </summary>
        internal readonly List<EntityStateMachine> heldMachines = new List<EntityStateMachine>();

        /// <summary>When something last held sprint off.</summary>
        internal float lastHeldOffTime = float.NegativeInfinity;

        /// <summary>A forced sprint was blocked since the player's last input poll.</summary>
        internal bool blockedSinceLastPoll;

        /// <summary>
        /// A forced sprint was blocked and the server hasn't been told "not sprinting" since (see
        /// <see cref="ServerSprintSync"/>).
        /// </summary>
        internal bool serverMaySprint;

        /// <summary>The player has a new body: look up its state machines.</summary>
        internal void SetBody(CharacterBody newBody)
        {
            body = newBody;
            bodyMachine = EntityStateMachine.FindByCustomName(newBody.gameObject, "Body");
            machines = newBody.GetComponents<EntityStateMachine>();
            heldMachines.Clear();
        }
    }
}

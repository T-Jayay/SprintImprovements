using RoR2;
using UnityEngine.Networking;

namespace SprintImprovements
{
    /// <summary>
    /// Tells the server once that a client isn't sprinting after BlockForcedSprint skipped a forced sprint.
    ///
    /// The owning client decides its body's sprint: the isSprinting setter sends CmdUpdateSprint when the value changes,
    /// the server passes it on to the other players, and the owner ignores the server's value for its own body. But
    /// most skills that force sprint also do it on the server's copy of a client's body: SkillDef.OnFixedUpdate sets it
    /// for skills with forceSprintDuringState (Mercenary's dashes, Commando's Tactical Dive, Huntress's Blink, Acrid's
    /// leaps, MUL-T's Transport Mode, ...), and GenericSkill.FixedUpdate isn't limited to the owner; entity states such
    /// as Drifter's Tornado Slam and CHEF's Oil Spill do it outside their authority checks. Only a few (Loader's
    /// gauntlets, ...) write it on the owner alone. A client-side mod can't stop the server doing that during the skill, and since the client's own value doesn't change, it sends nothing afterwards:
    /// the server's copy would stay sprinting until the player next stops sprinting (Little Disciple fires, Rose
    /// Buckler's armor applies, the other players see a sprint).
    ///
    /// So once a whole tick has passed without a forced sprint being skipped, this sends "not sprinting" through the
    /// game's own command.
    /// The client has already sent the end of the skill (its state change) on the same reliable channel, whose messages
    /// arrive in the order they were sent (the game's own networking depends on that, e.g. an object's spawn message
    /// before the messages about it), so the server has left the skill by the time the command arrives.
    /// </summary>
    internal static class ServerSprintSync
    {
        /// <summary>A forced sprint was skipped: send once skipping stops.</summary>
        internal static void OnForcedSprintBlocked(PlayerSprintState state)
        {
            state.blockedSinceLastPoll = true;
        }

        /// <summary>
        /// Runs at each of the player's input polls, which come before the skills of the same tick
        /// (PlayerCharacterMasterController's script execution order is -8, the state machines' and skills' 0).
        /// </summary>
        internal static void Update(CharacterBody body, PlayerSprintState state)
        {
            if (state.blockedSinceLastPoll)
            {
                // Still skipping: the server's copy would only sprint again the next tick.
                state.blockedSinceLastPoll = false;
                state.serverMaySprint = true;
                return;
            }
            if (!state.serverMaySprint)
            {
                return;
            }
            state.serverMaySprint = false;
            // The host's own body needs no message: the host is the server.
            if (!NetworkServer.active && NetworkClient.active && !body.isSprinting)
            {
                body.CallCmdUpdateSprint(false);
            }
        }
    }
}

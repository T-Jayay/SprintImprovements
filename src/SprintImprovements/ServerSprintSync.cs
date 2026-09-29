using System.Runtime.CompilerServices;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace SprintImprovements
{
    /// <summary>
    /// Keeps the server's copy of a client's body in step with the forced-sprint block. A client moves its own body
    /// and tells the server when its sprint changes (the isSprinting setter sends CmdUpdateSprint), but the server also
    /// runs the skill code on its copy of the body (SkillDef.OnFixedUpdate isn't limited to the owner), so while a
    /// forced sprint is blocked here, the server's copy may be sprinting: other players see it, and server-side sprint
    /// effects apply. The client ignores the server's sprint value for its own body and sends nothing, because its own
    /// sprint didn't change. So once blocking stops, this sends "not sprinting", and again a little later in case the
    /// server was still in the skill when the first message arrived. Nothing is sent while blocking goes on: the
    /// server's copy would only turn sprint on again the next tick.
    /// </summary>
    internal static class ServerSprintSync
    {
        // Send "not sprinting" when blocking stops and once more SyncInterval later; stop SyncWindow after the last
        // block.
        private const float SyncInterval = 0.5f;
        private const float SyncWindow = 1f;

        /// <summary>
        /// A forced sprint was blocked: (re)start the window, so the first message goes out once blocking stops.
        /// </summary>
        internal static void OnForcedSprintBlocked(BodySprintState state)
        {
            state.lastForceBlockTime = Time.fixedTime;
            state.lastSyncTime = float.NegativeInfinity;
        }

        /// <summary>Runs every tick for each of this game instance's players, even while the UI has focus.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Update(CharacterBody body)
        {
            if (NetworkServer.active || !NetworkClient.active || !body || body.isSprinting
                || !SprintGuard.TryGetState(body, out BodySprintState state))
            {
                return;
            }
            float now = Time.fixedTime;
            if (SprintGuard.IsRecent(state.lastForceBlockTime) || now - state.lastForceBlockTime > SyncWindow
                || now - state.lastSyncTime < SyncInterval)
            {
                // Still blocking, long done, or sent a moment ago.
                return;
            }
            state.lastSyncTime = now;
            body.CallCmdUpdateSprint(false);
        }
    }
}

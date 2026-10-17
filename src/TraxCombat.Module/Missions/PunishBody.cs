using TaleWorlds.MountAndBlade;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 54 (the AI punishes a window - AI_NOTES "Step 54"): the engine side of the one read the punish needs - the man an AI soldier
    /// fights and how far he is. The offline smoke plays it.
    /// </summary>
    internal interface IPunishBody
    {
        /// <summary>His target - <c>Agent.GetTargetAgent()</c>, else <c>Agent.ImmediateEnemy</c>; an active enemy only - and the distance
        /// between them (metres); null with none.</summary>
        Agent? Target(TrackedAgent st, out float distance);
    }

    /// <summary>The game's side: native reads (the AI's own target, the positions), main thread - called from the fever's look only for an
    /// AI man on foot in contact while some window is open in the battle.</summary>
    internal sealed class GamePunishBody : IPunishBody
    {
        public Agent? Target(TrackedAgent st, out float distance)
        {
            distance = float.NaN;
            var a = st.Agent;
            var e = a.GetTargetAgent();
            if (!IsEnemy(a, e)) e = a.ImmediateEnemy;
            if (!IsEnemy(a, e)) return null;
            distance = (a.Position - e!.Position).Length;
            return e;
        }

        private static bool IsEnemy(Agent a, Agent? e) => e != null && e.IsActive() && a.IsEnemyOf(e);
    }

    /// <summary>The punish body every new <see cref="AthleticsLogic"/> starts with - the game's; the offline smoke swaps in its stand-in once,
    /// before any logic is built (a native read on its fake agents would crash the process).</summary>
    internal static class PunishBodyDefault
    {
        internal static IPunishBody Current = new GamePunishBody();
    }
}

using TaleWorlds.MountAndBlade;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 65 (the AI kicks and bashes - AI_NOTES "Step 65"): the engine side of the reads an AI kick needs, asked only AFTER the dice said
    /// yes at a man's look (a handful of men a second in a big battle). The offline smoke plays it.
    /// </summary>
    internal interface IAiKickBody
    {
        bool IsActive(TrackedAgent st);

        /// <summary>The AI drives him (not the player, not a man the player took over).</summary>
        bool IsAiControlled(TrackedAgent st);

        /// <summary>What he is and holds: on a horse, running away (a rout), a bow / crossbow / throwing weapon in his main hand, a shield in his
        /// off hand.</summary>
        void Hands(TrackedAgent st, out bool mounted, out bool routing, out bool ranged, out bool shield);

        /// <summary>The man he fights - <c>Agent.GetTargetAgent()</c>, else <c>Agent.ImmediateEnemy</c>; an active enemy on foot only - the
        /// distance (m) and how far off his facing he stands (degrees, 0-180); null with none.</summary>
        Agent? Target(TrackedAgent st, out float distance, out float offFacingDegrees);

        /// <summary>The angle (degrees) between <paramref name="st"/>'s facing and the line to <paramref name="other"/> (is his raised shield
        /// toward the kicker?).</summary>
        float FacingTo(TrackedAgent st, TrackedAgent other);
    }

    /// <summary>The game's side: native reads (the AI's own target, positions, the facing), managed equipment reads; main thread.</summary>
    internal sealed class GameAiKickBody : IAiKickBody
    {
        public bool IsActive(TrackedAgent st) => st.Agent.IsActive();

        public bool IsAiControlled(TrackedAgent st) => st.Agent.IsAIControlled && !st.Agent.IsMainAgent;

        public void Hands(TrackedAgent st, out bool mounted, out bool routing, out bool ranged, out bool shield)
        {
            var a = st.Agent;
            mounted = a.MountAgent != null;
            routing = a.IsRunningAway || a.IsRetreating();
            var main = a.WieldedWeapon.CurrentUsageItem;
            ranged = main != null && main.IsRangedWeapon;
            var off = a.WieldedOffhandWeapon.CurrentUsageItem;
            shield = off != null && off.IsShield;
        }

        public Agent? Target(TrackedAgent st, out float distance, out float offFacingDegrees)
        {
            distance = float.NaN;
            offFacingDegrees = float.NaN;
            var a = st.Agent;
            var e = a.GetTargetAgent();
            if (!IsEnemyOnFoot(a, e)) e = a.ImmediateEnemy;
            if (!IsEnemyOnFoot(a, e)) return null;
            distance = (a.Position - e!.Position).AsVec2.Length;
            offFacingDegrees = (float)GameImpactInputs.AngleFrom(a, e);
            return e;
        }

        public float FacingTo(TrackedAgent st, TrackedAgent other) => (float)GameImpactInputs.AngleFrom(st.Agent, other.Agent);

        private static bool IsEnemyOnFoot(Agent a, Agent? e) => e != null && e.IsActive() && e.IsHuman && e.MountAgent == null && a.IsEnemyOf(e);
    }

    /// <summary>The body every new <see cref="AthleticsLogic"/> starts with - the game's; the offline smoke swaps in its stand-in once, before
    /// any logic is built (a native read on its fake agents would crash the process).</summary>
    internal static class AiKickBodyDefault
    {
        internal static IAiKickBody Current = new GameAiKickBody();
    }
}

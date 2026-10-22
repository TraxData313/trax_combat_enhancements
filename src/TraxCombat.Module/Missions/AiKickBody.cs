using TaleWorlds.Library;
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

        /// <summary>Step 16's hook on him (the component + the engine's callback) - from the tick.</summary>
        AiInputHook.HookResult Hook(TrackedAgent st);

        /// <summary>His callback released if no wish is left (never for a man gone).</summary>
        void Release(TrackedAgent st);

        /// <summary>Step 80 (step 68's friend check, rebuilt): his STRIKE PATH - along his facing, <paramref name="reach"/> m long,
        /// <paramref name="halfWidth"/> m either side (<see cref="TraxCombat.Core.AiKickPathProbe"/>): is <paramref name="target"/> in it and the first
        /// body there, or does a man of his own side stand in it closer (within <paramref name="rearRank"/> m = a rear-rank man)?
        /// <paramref name="looked"/> = the friends the probe looked at.</summary>
        TraxCombat.Core.AiKickPath StrikePath(TrackedAgent st, Agent target, double reach, double halfWidth, double rearRank, out int looked);

        /// <summary>Step 68: is <paramref name="other"/> an enemy of <paramref name="st"/> (whom a landed kick hit)?</summary>
        bool IsEnemy(TrackedAgent st, TrackedAgent other);
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

        public AiInputHook.HookResult Hook(TrackedAgent st) => AiInputHook.Hook(st);

        public void Release(TrackedAgent st)
        {
            if (!st.Removed) AiInputHook.UnhookIfIdle(st);
        }

        private readonly MBList<Agent> _allies = new MBList<Agent>(); // reused - no allocation per read

        /// <summary>Step 80: his position and FACING (<c>GetMovementDirection</c> - the facing step 62's angle uses), the target's position, the
        /// game's proximity search for his side within the path's reach (<c>Mission.GetNearbyAllyAgents</c>, the list reused), each one fed to
        /// the Core probe - horses of his side too (a friend's horse in the way is a friend's body). Main thread; asked only in reach, in front
        /// and with the target in the path (a handful of reads a second). No mission / team = the path refused (no kick - vanilla's own way).</summary>
        public TraxCombat.Core.AiKickPath StrikePath(TrackedAgent st, Agent target, double reach, double halfWidth, double rearRank, out int looked)
        {
            looked = 0;
            var a = st.Agent;
            var m = Mission.Current;
            if (m == null || a.Team == null || target == null) return TraxCombat.Core.AiKickPath.TargetOffPath;
            var p = a.Position.AsVec2;
            var t = target.Position.AsVec2;
            var face = a.GetMovementDirection();
            var probe = TraxCombat.Core.AiKickPathProbe.Begin(p.x, p.y, face.x, face.y, t.x, t.y, reach, halfWidth, rearRank);
            if (probe.TargetIn && halfWidth > 0)
            {
                float radius = (float)(System.Math.Min(reach, probe.TargetAlong) + halfWidth);
                m.GetNearbyAllyAgents(p, radius, a.Team, _allies);
                for (int i = 0; i < _allies.Count; i++)
                {
                    var f = _allies[i];
                    if (f == null || f == a || f == a.MountAgent || !f.IsActive()) continue;
                    var q = f.Position.AsVec2;
                    probe.Add(q.x, q.y);
                }
                _allies.Clear();
            }
            looked = probe.Looked;
            return probe.Verdict;
        }

        public bool IsEnemy(TrackedAgent st, TrackedAgent other) => st.Agent.IsEnemyOf(other.Agent);

        private static bool IsEnemyOnFoot(Agent a, Agent? e) => e != null && e.IsActive() && e.IsHuman && e.MountAgent == null && a.IsEnemyOf(e);
    }

    /// <summary>The body every new <see cref="AthleticsLogic"/> starts with - the game's; the offline smoke swaps in its stand-in once, before
    /// any logic is built (a native read on its fake agents would crash the process).</summary>
    internal static class AiKickBodyDefault
    {
        internal static IAiKickBody Current = new GameAiKickBody();
    }
}

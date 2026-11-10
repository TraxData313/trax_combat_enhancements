using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TraxCombat.Missions
{
    /// <summary>Step 106b: one man's upper body as the engine shows it - the forced guard's read-back.</summary>
    internal struct OpeningPose
    {
        /// <summary>Channel 1's action name ("" none / unread).</summary>
        public string Action;

        /// <summary>Channel 1's action direction (the engine's <c>UsageDirection</c>: AttackUp 0 .. DefendRight 7; -1 none).</summary>
        public int Direction;

        /// <summary>Channel 1's action stage (the engine's <c>ActionStage</c>; -1 none).</summary>
        public int Stage;

        /// <summary>The engine's defend movement flag (<c>GetDefendMovementFlag</c>).</summary>
        public uint DefendFlag;

        /// <summary>Channel 1 in a defend stage (Defend / DefendParry).</summary>
        public bool Defending;

        public bool Read;
    }

    /// <summary>
    /// Step 106b (AI_NOTES "Step 106" §106b): every GAME call of the openings' forced guard and its test tool - who drives him, step 16's hook
    /// on him, his pose read back, the agent's <c>CanDefend</c> flag (the test tool's second lowering route). Main thread (the tick). The offline
    /// smoke plays a stand-in (a native call on a fake agent would crash).
    /// </summary>
    internal interface IOpeningBody
    {
        /// <summary>The AI drives him (not you, not a man you took over).</summary>
        bool AiDriven(TrackedAgent st);

        /// <summary>He is active and on foot.</summary>
        bool OnFoot(TrackedAgent st);

        /// <summary>The test tool: the nearest active ENEMY human on foot within <paramref name="metres"/> of you (null none).</summary>
        Agent? NearestFoe(Agent you, float metres);

        /// <summary>Step 16's hook on him (the component + the engine's callback).</summary>
        AiInputHook.HookResult Hook(TrackedAgent st);

        /// <summary>The callback off again when no wish is left (never for a removed man).</summary>
        void Release(TrackedAgent st);

        /// <summary>His upper body now.</summary>
        OpeningPose Pose(TrackedAgent st);

        /// <summary>Step 108: channel 1 shows <paramref name="guard"/> in a defend stage now - the cheap read (no action name, no allocation) the hold's
        /// recovery polls every tick.</summary>
        bool GuardShown(TrackedAgent st, int guard);

        /// <summary>The agent's <c>AgentFlag.CanDefend</c> is on.</summary>
        bool CanDefend(TrackedAgent st);

        /// <summary>Sets the agent's <c>AgentFlag.CanDefend</c> (vanilla's own climbers' route: SetAgentFlags without it).</summary>
        void SetCanDefend(TrackedAgent st, bool on);
    }

    internal sealed class GameOpeningBody : IOpeningBody
    {
        public bool AiDriven(TrackedAgent st)
        {
            var a = st.Agent;
            return a.IsAIControlled && !a.IsMainAgent && !a.IsPlayerControlled;
        }

        public bool OnFoot(TrackedAgent st)
        {
            var a = st.Agent;
            return a.IsActive() && a.MountAgent == null;
        }

        private readonly MBList<Agent> _near = new MBList<Agent>();

        public Agent? NearestFoe(Agent you, float metres)
        {
            var m = Mission.Current;
            if (m == null || you == null) return null;
            _near.Clear();
            m.GetNearbyAgents(you.Position.AsVec2, metres, _near);
            Agent? best = null;
            float bestD = metres * metres;
            foreach (var a in _near)
            {
                if (a == you || !a.IsActive() || !a.IsHuman || a.MountAgent != null || !a.IsEnemyOf(you)) continue;
                float d = a.Position.DistanceSquared(you.Position);
                if (d <= bestD)
                {
                    bestD = d;
                    best = a;
                }
            }
            _near.Clear();
            return best;
        }

        public AiInputHook.HookResult Hook(TrackedAgent st) => AiInputHook.Hook(st);

        public void Release(TrackedAgent st)
        {
            if (!st.Removed) AiInputHook.UnhookIfIdle(st);
        }

        public OpeningPose Pose(TrackedAgent st)
        {
            var a = st.Agent;
            var p = new OpeningPose { Action = string.Empty, Direction = -1, Stage = -1 };
            if (!a.IsActive()) return p;
            var act = a.GetCurrentAction(1);
            p.Action = act.Index >= 0 ? act.GetName() ?? string.Empty : string.Empty;
            p.Direction = (int)a.GetCurrentActionDirection(1);
            var stage = a.GetCurrentActionStage(1);
            p.Stage = (int)stage;
            p.Defending = stage == Agent.ActionStage.Defend || stage == Agent.ActionStage.DefendParry;
            p.DefendFlag = (uint)a.GetDefendMovementFlag();
            p.Read = true;
            return p;
        }

        public bool GuardShown(TrackedAgent st, int guard)
        {
            var a = st.Agent;
            if (!a.IsActive()) return false;
            var stage = a.GetCurrentActionStage(1);
            bool defending = stage == Agent.ActionStage.Defend || stage == Agent.ActionStage.DefendParry;
            return TraxCombat.Core.OpeningForceMath.PoseMatches(guard, false, (int)a.GetCurrentActionDirection(1), defending);
        }

        public bool CanDefend(TrackedAgent st) => (st.Agent.GetAgentFlags() & AgentFlag.CanDefend) != 0;

        public void SetCanDefend(TrackedAgent st, bool on)
        {
            var a = st.Agent;
            var f = a.GetAgentFlags();
            var want = on ? f | AgentFlag.CanDefend : f & ~AgentFlag.CanDefend;
            if (want != f) a.SetAgentFlags(want);
        }
    }

    /// <summary>The body every new logic starts with - the game's; the offline smoke swaps in its stand-in.</summary>
    internal static class OpeningBodyDefault
    {
        internal static IOpeningBody Current = new GameOpeningBody();
    }
}

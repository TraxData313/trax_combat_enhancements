using System.Collections.Generic;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 100b (AI_NOTES "Step 100" §1): the engine side of the timed block's GUARD CLOCK - the player's guard key every tick
    /// (<c>GetDefendMovementFlag</c> + <c>GetCurrentActionStage(1)</c>), the game's block-direction mode, and the channel-1 defend action read at
    /// contact (active / passive / a parry reaction - by its name, classified once per action index - and an active defend's age = its progress
    /// × its animation length). Main thread (the tick, the hit callback). The offline smoke plays a stand-in (a native read on a fake agent
    /// would crash).
    /// </summary>
    internal interface IGuardBody
    {
        /// <summary>His guard key now (Core <see cref="TimedBlockMath.GuardKey"/>: 0 = no guard).</summary>
        int GuardKey(Agent a);

        /// <summary>The game's block direction is AUTOMATIC (<c>BannerlordConfig.DefendDirectionControl</c> 2): only the press counts.</summary>
        bool AutomaticDirection { get; }

        /// <summary>Channel 1 at contact: what it shows and, for an ACTIVE defend, its age (s; NaN otherwise) and its animation's length /
        /// cyclic flag / whether it continues to another action (NaN / false when not read).</summary>
        GuardRead ReadContact(Agent victim, out double activeAge, out double length, out bool cyclic, out bool continues);

        /// <summary>Step 102: he is an AI man on the field (not the player's, not one the player took over) with an enemy within
        /// <paramref name="metres"/> - his guard clock should be fed (one native proximity count, no allocation).</summary>
        bool WatchWanted(TrackedAgent st, float metres);

        /// <summary>Step 102: his input callback on (step 16's hook - our component added once).</summary>
        AiInputHook.HookResult Hook(TrackedAgent st);

        /// <summary>Step 102: his callback off again if nothing else wants it (step 16's <see cref="AiInputHook.UnhookIfIdle"/>); never on a removed man.</summary>
        void Release(TrackedAgent st);
    }

    /// <summary>The game's reads: native flag / stage / progress reads, the action's name once per index (<c>ActionIndexCache.GetName</c>),
    /// <c>MBActionSet</c>'s animation length / flags / continue-to once per action set and action.</summary>
    internal sealed class GameGuardBody : IGuardBody
    {
        private readonly Dictionary<int, GuardRead> _kinds = new Dictionary<int, GuardRead>();
        private readonly Dictionary<long, (double Length, bool Cyclic, bool Continues)> _lengths = new Dictionary<long, (double, bool, bool)>();

        public int GuardKey(Agent a) => TimedBlockMath.GuardKey((int)a.GetDefendMovementFlag(), (int)a.GetCurrentActionStage(1));

        public bool WatchWanted(TrackedAgent st, float metres)
        {
            var a = st.Agent;
            if (!a.IsActive() || a.IsMainAgent || !a.IsAIControlled || a.Team == null) return false;
            var m = Mission.Current;
            return m != null && m.GetNearbyEnemyAgentCount(a.Team, a.Position.AsVec2, metres) > 0;
        }

        public AiInputHook.HookResult Hook(TrackedAgent st) => AiInputHook.Hook(st);

        public void Release(TrackedAgent st)
        {
            if (!st.Removed) AiInputHook.UnhookIfIdle(st);
        }

        public bool AutomaticDirection => BannerlordConfig.DefendDirectionControl == TimedBlockMath.DefendDirectionAutomatic;

        public GuardRead ReadContact(Agent victim, out double activeAge, out double length, out bool cyclic, out bool continues)
        {
            activeAge = double.NaN;
            length = double.NaN;
            cyclic = false;
            continues = false;
            var act = victim.GetCurrentAction(1);
            int index = act.Index;
            if (index < 0) return GuardRead.NotDefend;
            if (!_kinds.TryGetValue(index, out var kind))
            {
                kind = TimedBlockMath.ClassifyDefendName(act.GetName()); // once per action index (a string per index, never per read)
                _kinds[index] = kind;
            }
            if (kind != GuardRead.Active) return kind;
            var set = victim.ActionSet;
            long key = ((long)set.GetHashCode() << 32) | (uint)index;
            if (!_lengths.TryGetValue(key, out var l))
            {
                double seconds = MBActionSet.GetActionAnimationDuration(set, in act);
                var flags = MBActionSet.GetActionAnimationFlags(set, in act);
                var next = MBActionSet.GetActionAnimationContinueToAction(set, in act);
                l = (seconds, (flags & AnimFlags.anf_cyclic) != 0, next.Index >= 0);
                _lengths[key] = l;
            }
            length = l.Length;
            cyclic = l.Cyclic;
            continues = l.Continues;
            double progress = victim.GetCurrentActionProgress(1);
            if (!double.IsNaN(progress) && !double.IsNaN(l.Length) && l.Length > 0) activeAge = System.Math.Max(0, progress) * l.Length;
            return kind;
        }
    }

    /// <summary>Step 100b: the guard reads every new logic starts with (the smoke swaps in its stand-in once).</summary>
    internal static class GuardBodyDefault
    {
        internal static IGuardBody Current = new GameGuardBody();
    }
}

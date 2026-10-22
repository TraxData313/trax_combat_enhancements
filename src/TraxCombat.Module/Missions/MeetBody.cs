using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>Step 82: one man's guard as read at contact - what he held up, its direction, the raw reads (for the log), and the guarding item's
    /// weight and skill (the riposte's guard on a correct meet).</summary>
    internal struct MeetGuardRead
    {
        public MeetGuardKind Kind;
        public MeetDir Dir;
        public int ActionType, ActionDirection;
        public uint DefendFlag;
        public double GuardKg, GuardSkill;
    }

    /// <summary>
    /// Step 82 (AI_NOTES "Step 82"): the engine side of the meet rules - the defender's guard at contact, an AI man's block ability, the enemy a
    /// player's kick is aimed at. Main thread (the hit callback, the decorator's seams, the tick). The offline smoke plays it.
    /// </summary>
    internal interface IMeetBody
    {
        /// <summary>His guard now: channel 1's action type and its direction (<c>GetCurrentActionDirection(1)</c>), the engine's defend flag
        /// (<c>GetDefendMovementFlag</c>) - Core <see cref="ImpactMeetMath.GuardOf"/> - and the guarding item's weight / skill.</summary>
        MeetGuardRead Guard(Agent victim);

        /// <summary>His block ability: the game's <c>AIBlockOnDecideAbility</c> and <c>AiRandomizedDefendDirectionChance</c> (step 30's tired
        /// guess already written in by the stat decorator).</summary>
        void Ability(TrackedAgent st, out float blockOnDecide, out float randomizedDirection);

        /// <summary>The man a kick / bash of <paramref name="st"/> is aimed at (a player's: the nearest enemy on foot within
        /// <paramref name="reach"/> m and <paramref name="facing"/>° of his facing); null with none.</summary>
        Agent? Aim(TrackedAgent st, double reach, double facing);

        /// <summary>The AI drives him (not you, not a man you took over) and he stands, on foot.</summary>
        bool CanRead(TrackedAgent st);

        /// <summary>Step 16's hook on him (the component + the engine's callback) - from the tick; released when no wish is left.</summary>
        AiInputHook.HookResult Hook(TrackedAgent st);

        void Release(TrackedAgent st);
    }

    /// <summary>The game's reads (native action / flag reads, managed equipment and properties).</summary>
    internal sealed class GameMeetBody : IMeetBody
    {
        private readonly MBList<Agent> _enemies = new MBList<Agent>(); // reused - no allocation per read

        public MeetGuardRead Guard(Agent victim)
        {
            var g = new MeetGuardRead
            {
                ActionType = (int)victim.GetCurrentActionType(1),
                ActionDirection = (int)victim.GetCurrentActionDirection(1),
                DefendFlag = (uint)victim.GetDefendMovementFlag(),
            };
            g.Kind = ImpactMeetMath.GuardOf(g.ActionType, g.ActionDirection, g.DefendFlag, out g.Dir);
            if (g.Kind == MeetGuardKind.Shield)
            {
                var off = victim.WieldedOffhandWeapon;
                if (off.CurrentUsageItem != null && off.CurrentUsageItem.IsShield) g.GuardKg = GameImpactInputs.WeightOf(off);
                g.GuardSkill = GameImpactInputs.Skill(victim, DefaultSkills.OneHanded);
            }
            else if (g.Kind == MeetGuardKind.Weapon)
            {
                var main = victim.WieldedWeapon;
                var usage = main.CurrentUsageItem;
                if (usage != null)
                {
                    g.GuardKg = GameImpactInputs.WeightOf(main);
                    if (usage.RelevantSkill != null) g.GuardSkill = GameImpactInputs.Skill(victim, usage.RelevantSkill);
                }
            }
            return g;
        }

        public void Ability(TrackedAgent st, out float blockOnDecide, out float randomizedDirection)
        {
            var p = st.Agent.AgentDrivenProperties;
            blockOnDecide = p.AIBlockOnDecideAbility;
            randomizedDirection = p.AiRandomizedDefendDirectionChance;
        }

        public Agent? Aim(TrackedAgent st, double reach, double facing)
        {
            var a = st.Agent;
            var m = Mission.Current;
            if (m == null || a.Team == null) return null;
            m.GetNearbyEnemyAgents(a.Position.AsVec2, (float)reach + 0.5f, a.Team, _enemies);
            Agent? best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < _enemies.Count; i++)
            {
                var e = _enemies[i];
                if (e == null || !e.IsActive() || !e.IsHuman || e.MountAgent != null) continue;
                float d = (e.Position - a.Position).AsVec2.Length;
                if (d > reach || d >= bestD) continue;
                if (GameImpactInputs.AngleFrom(a, e) > facing) continue;
                best = e;
                bestD = d;
            }
            _enemies.Clear();
            return best;
        }

        public bool CanRead(TrackedAgent st)
        {
            var a = st.Agent;
            return a.IsActive() && a.IsAIControlled && !a.IsMainAgent && a.MountAgent == null;
        }

        public AiInputHook.HookResult Hook(TrackedAgent st) => AiInputHook.Hook(st);

        public void Release(TrackedAgent st)
        {
            if (!st.Removed) AiInputHook.UnhookIfIdle(st);
        }
    }

    /// <summary>The body every new <see cref="AthleticsLogic"/> starts with - the game's; the offline smoke swaps in its stand-in.</summary>
    internal static class MeetBodyDefault
    {
        internal static IMeetBody Current = new GameMeetBody();
    }
}

using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 67: the fear flock's GAME reads behind one seam (the step-49 pattern; the offline smoke plays a stand-in): his unit - its key in
    /// the table (team × formation), its order as the flock reads it, its centre of mass and facing - and his formation SLOT. Everything else
    /// (the scan, morale, his position, his frame, the hook) is courage's <see cref="ICourageBody"/>.
    /// </summary>
    internal interface IFearBody
    {
        /// <summary>His unit: the table's key (<see cref="FearMath.UnitKey"/>), the order (a wall arrangement = <see cref="FearOrder.Wall"/>),
        /// the centre of mass (<c>CachedAveragePosition</c>) and the facing (a unit vector). False = no unit to read.</summary>
        bool Unit(TrackedAgent st, out int key, out FearOrder order, out double cx, out double cy, out double fx, out double fy);

        /// <summary>His formation slot on the ground (vanilla's own point - <c>GetCurrentGlobalPositionOfUnit(agent, true)</c>). False = none
        /// (a charge: no slot).</summary>
        bool Slot(TrackedAgent st, out double x, out double y);

        /// <summary>Step 69: his unit's order as step 23 reads it (<c>GameBraceBody.KindOf</c>; no formation = <see cref="BraceOrderKind.None"/>).</summary>
        BraceOrderKind OrderKind(TrackedAgent st);

        /// <summary>Step 69: the nearest ACTIVE enemy man within <paramref name="radius"/> (m; NaN = none) - one proximity query (the threat line
        /// beyond courage's scan, the idle diagnostic's 25 m).</summary>
        double NearestEnemy(TrackedAgent st, float radius);

        /// <summary>Step 69: a ranged weapon in his hand (a bow, a crossbow, a throwing weapon), or his formation a ranged / horse-archer /
        /// skirmisher one (its class or its slot) - standing off is NORMAL for him.</summary>
        bool Ranged(TrackedAgent st);
    }

    /// <summary>The engine side (AI_NOTES "Step 67"): managed reads of his formation - <c>Team.TeamIndex</c>, <c>FormationIndex</c>, step 23's
    /// <see cref="GameBraceBody.KindOf"/>, <c>ArrangementOrder.OrderEnum</c>, <c>CachedAveragePosition</c>, <c>CurrentDirection</c> and the slot
    /// step 41 reads. Main thread (courage's look).</summary>
    internal sealed class GameFearBody : IFearBody
    {
        public bool Unit(TrackedAgent st, out int key, out FearOrder order, out double cx, out double cy, out double fx, out double fy)
        {
            key = -1;
            order = FearOrder.Hold;
            cx = cy = fx = fy = 0;
            var a = st.Agent;
            var f = a.Formation;
            if (f == null || a.Team == null || f.CountOfUnits <= 0) return false;
            key = FearMath.UnitKey(a.Team.TeamIndex, (int)f.FormationIndex);
            if (key < 0) return false;
            var arrangement = f.ArrangementOrder.OrderEnum;
            bool wall = arrangement == ArrangementOrder.ArrangementOrderEnum.ShieldWall || arrangement == ArrangementOrder.ArrangementOrderEnum.Square
                        || arrangement == ArrangementOrder.ArrangementOrderEnum.Circle;
            order = FearMath.OrderOf(GameBraceBody.KindOf(f), wall);
            var c = f.CachedAveragePosition;
            if (!c.IsValid) return false;
            cx = c.x;
            cy = c.y;
            var d = f.CurrentDirection;
            double len = System.Math.Sqrt(d.x * d.x + d.y * d.y);
            if (len > 1e-6)
            {
                fx = d.x / len;
                fy = d.y / len;
            }
            return true;
        }

        public bool Slot(TrackedAgent st, out double x, out double y)
        {
            x = y = double.NaN;
            var a = st.Agent;
            var f = a.Formation;
            if (f == null) return false;
            var p = f.GetCurrentGlobalPositionOfUnit(a, true);
            if (!p.IsValid) return false;
            x = p.x;
            y = p.y;
            return true;
        }

        private readonly MBList<Agent> _list = new MBList<Agent>(64);

        public BraceOrderKind OrderKind(TrackedAgent st) => GameBraceBody.KindOf(st.Agent.Formation);

        /// <summary><c>Mission.GetNearbyEnemyAgents</c> (it clears the list first) into one reused MBList - no allocation.</summary>
        public double NearestEnemy(TrackedAgent st, float radius)
        {
            var m = Mission.Current;
            var a = st.Agent;
            if (m == null || a.Team == null) return double.NaN;
            var p = a.Position.AsVec2;
            m.GetNearbyEnemyAgents(p, radius, a.Team, _list);
            double best = double.NaN;
            for (int i = 0; i < _list.Count; i++)
            {
                var o = _list[i];
                if (o == null || !o.IsHuman || !o.IsActive()) continue;
                var op = o.Position;
                double dx = op.x - p.x, dy = op.y - p.y;
                double d = System.Math.Sqrt(dx * dx + dy * dy);
                if (double.IsNaN(best) || d < best) best = d;
            }
            _list.Clear();
            return best;
        }

        /// <summary><c>WieldedWeapon.CurrentUsageItem.IsRangedWeapon</c> (a thrown weapon's usage too), the formation's <c>PhysicalClass</c>
        /// (QuerySystem.MainClass) and its <c>FormationIndex</c>.</summary>
        public bool Ranged(TrackedAgent st)
        {
            var a = st.Agent;
            var held = a.WieldedWeapon.CurrentUsageItem;
            if (held != null && held.IsRangedWeapon) return true;
            var f = a.Formation;
            if (f == null) return false;
            return RangedClass(f.FormationIndex) || RangedClass(f.PhysicalClass);
        }

        private static bool RangedClass(FormationClass c) => c == FormationClass.Ranged || c == FormationClass.HorseArcher || c == FormationClass.Skirmisher;
    }

    /// <summary>The fear body every new <see cref="AthleticsLogic"/> starts with - the game's; the offline smoke swaps in its stand-in once,
    /// before any logic is built (a native read on its fake agents would crash the process).</summary>
    internal static class FearBodyDefault
    {
        internal static IFearBody Current = new GameFearBody();
    }
}

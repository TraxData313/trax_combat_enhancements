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
    }

    /// <summary>The fear body every new <see cref="AthleticsLogic"/> starts with - the game's; the offline smoke swaps in its stand-in once,
    /// before any logic is built (a native read on its fake agents would crash the process).</summary>
    internal static class FearBodyDefault
    {
        internal static IFearBody Current = new GameFearBody();
    }
}

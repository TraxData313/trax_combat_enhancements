using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 73 - LINE DISCIPLINE BY TIER (Toni 2026-10-20; AI_NOTES "Step 73", DESIGN decision 67): the attach point of
    /// <see cref="LineDiscipline"/> - built at the mission's start, looked at in the logic's pre-tick (right after the game's agent and team
    /// tick, where the AI's behaviours set their widths; before the shield wall's writes, so they see the narrowed slots), the player's line
    /// orders through his OrderController.OnOrderIssued (subscribed once per controller), the [summary] "line discipline" lines.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private LineDiscipline? _lines;
        private GameLineWorld? _lineWorld;
        private OrderController? _lineController;

        /// <summary>This mission's line discipline (the offline smoke reads it), null before the start.</summary>
        internal LineDiscipline? Lines => _lines;

        private void StartLines()
        {
            try
            {
                _lineWorld = new GameLineWorld(Mission);
                _lines = new LineDiscipline(_lineWorld);
                _lines.Start();
            }
            catch (Exception e)
            {
                _lines = null;
                Failed("lines.start", e);
                TraxLog.Info("lines", "could not start the line discipline - the game's own widths this battle (see the [error] above)");
            }
        }

        private void TickLines(double now)
        {
            if (_lines == null) return;
            SubscribeLineOrders();
            _lines.Tick(now);
        }

        private void SubscribeLineOrders()
        {
            try
            {
                var c = Mission?.PlayerTeam?.PlayerOrderController;
                if (ReferenceEquals(c, _lineController)) return;
                if (_lineController != null) _lineController.OnOrderIssued -= OnLineOrderIssued;
                _lineController = c;
                if (c != null) c.OnOrderIssued += OnLineOrderIssued;
            }
            catch (Exception e)
            {
                Failed("lines.subscribe", e);
            }
        }

        /// <summary>The player's line order (the game's MoveToLineSegment - a click or a drag of the orders menu): each formation's share of
        /// the dragged line, the way the game splits it (side by side: by their minimum widths, 1.5 m between; one behind another: the whole).</summary>
        private void OnLineOrderIssued(OrderType orderType, MBReadOnlyList<Formation> appliedFormations, OrderController orderController, params object[] delegateParams)
        {
            try
            {
                if (_lines == null || _lineWorld == null) return;
                if (orderType != OrderType.MoveToLineSegment && orderType != OrderType.MoveToLineSegmentWithHorizontalLayout) return;
                if (appliedFormations == null || delegateParams == null || delegateParams.Length < 2) return;
                if (!(delegateParams[0] is WorldPosition p1) || !(delegateParams[1] is WorldPosition p2)) return;
                float length = (p2.AsVec2 - p1.AsVec2).Length;
                bool vertical = orderType == OrderType.MoveToLineSegment;
                float sumMin = 0;
                int count = 0;
                for (int i = 0; i < appliedFormations.Count; i++)
                {
                    var f = appliedFormations[i];
                    if (f == null || f.CountOfUnitsWithoutDetachedOnes <= 0) continue;
                    sumMin += f.MinimumWidth;
                    count++;
                }
                if (count == 0) return;
                float room = vertical ? Math.Max(0f, length - (count - 1) * 1.5f) : length;
                if (vertical ? room < sumMin : length < sumMin) return; // a click without a drag: the game keeps the widths
                double now = Mission.CurrentTime;
                for (int i = 0; i < appliedFormations.Count; i++)
                {
                    var f = appliedFormations[i];
                    if (f == null || f.CountOfUnitsWithoutDetachedOnes <= 0) continue;
                    double asked = vertical ? room * (sumMin > 0 ? f.MinimumWidth / sumMin : 1f / count) : room;
                    int slot = _lineWorld.SlotOf(f);
                    if (slot >= 0) _lines.NotePlayerLineOrder(slot, asked, now);
                }
            }
            catch (Exception e)
            {
                Failed("lines.order-event", e);
            }
        }

        private void WriteLineSummary()
        {
            try
            {
                if (_lineController != null) _lineController.OnOrderIssued -= OnLineOrderIssued;
            }
            catch (Exception e)
            {
                Failed("lines.unsubscribe", e);
            }
            _lineController = null;
            if (_lines == null)
            {
                TraxLog.Info("summary", "line discipline (step 73): not started this mission (see the [error] at its start)");
                return;
            }
            foreach (var line in _lines.SummaryLines()) TraxLog.Info("summary", line);
        }
    }
}

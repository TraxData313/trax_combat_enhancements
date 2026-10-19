using System;
using TaleWorlds.MountAndBlade.View.Screens;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 72 - NO ACCIDENTAL CHARGE ORDERS (Toni 2026-10-19; AI_NOTES "Step 72", DESIGN decision 66): the attach point of
    /// <see cref="OrderClickView"/> (the logic's first tick, beside the HUD views - after the game's own targeting handler in the behaviour
    /// list) and its [summary] "order clicks" line. The screen finalizes its views before the summary; the guard's numbers outlive it.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private OrderClickView? _orderClicks;

        /// <summary>This mission's order-click guard (the offline smoke reads it), null before the first tick / without a screen.</summary>
        internal OrderClickGuard? OrderClicks => _orderClicks?.Guard;

        private void AttachOrderClicks(MissionScreen screen)
        {
            try
            {
                var view = new OrderClickView();
                screen.AddMissionView(view);
                _orderClicks = view;
                TraxLog.Info("orders", "attached: the order-click guard (step 72) - " + view.Prepare());
            }
            catch (Exception e)
            {
                Failed("orders.attach", e);
                TraxLog.Info("orders", "could not attach the order-click guard - the game's own targeting this battle (see the [error] above)");
            }
        }

        private void WriteOrderClickSummary()
        {
            var guard = _orderClicks?.Guard;
            if (guard == null)
            {
                TraxLog.Info("summary", "order clicks (step 72): no guard this mission (no mission screen, or the attach failed)");
                return;
            }
            guard.Release();
            foreach (var line in guard.SummaryLines()) TraxLog.Info("summary", line);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 72 - what the guard reads from the game each frame (the view's engine reads; the offline smoke's stand-in). Cheap calls first:
    /// <see cref="Look"/> runs only while the button is down in the orders menu.
    /// </summary>
    internal interface IOrderClickWorld
    {
        /// <summary>The orders menu is open (or the deployment screen) with the mouse - no gamepad.</summary>
        bool OrderModeActive { get; }

        /// <summary>The left mouse button is down.</summary>
        bool ButtonDown { get; }

        /// <summary>The screen's height in pixels (the 1080p settings scale by it).</summary>
        double ScreenHeight { get; }

        /// <summary>
        /// The game's own pick (the unit a release now would charge), whether its banner is really on the screen (step 98) and the distances
        /// in screen pixels: the cursor → that unit's banner icon (NaN = no unit) and the cursor → the press point (<paramref name="press"/>:
        /// remember the point now, 0). Called every frame the orders menu is open (step 98 - the idle hold of an off-screen focus).
        /// </summary>
        void Look(bool press, out OrderClickLook look);

        /// <summary>The game's formation targeting off (true) or the game's own again (false).</summary>
        void SetTargetingDisabled(bool disabled);
    }

    /// <summary>
    /// Step 72 - NO ACCIDENTAL CHARGE ORDERS (Toni 2026-10-19; AI_NOTES "Step 72", DESIGN decision 66). Vanilla charges a unit on ANY release of
    /// the left button in the orders menu while a unit is FOCUSED - its banner point within 70 px of the screen centre, or the unit within 10 m
    /// of the camera - so a drag that sweeps across a banner, or a click near one, becomes a charge. The lever is the game's own public
    /// <c>MissionFormationTargetSelectionHandler.SetIsFormationTargetingDisabled</c>: while the button is held the guard keeps the targeting
    /// OFF unless the click is a deliberate one (pressed ON the unit's icon, still on it, never dragged) - the release then finds no unit and the
    /// game's own troop placer places its move (the dragged frontage kept). No order is issued or re-issued by us, so no charge ever starts (no
    /// sprint, no fever). Idle (no button) = the game's targeting untouched: the F-key charge at the unit you look at works as before. The
    /// master switch FIRST, read live. Engine-free (the smoke drives it).
    /// </summary>
    internal sealed class OrderClickGuard
    {
        private readonly IOrderClickWorld _world;
        private readonly OrderClickTracker _tracker = new OrderClickTracker();
        private readonly OrderClickStats _stats = new OrderClickStats();
        private bool _applied;
        private bool _firstHoldOff;
        private bool _firstKept;
        private bool _firstDrag;
        private bool _firstOffIcon;
        private bool _firstLeak;
        private bool _firstOffScreenIdle;
        private bool _firstOffScreenClick;
        private bool _firstOutsideCharge;
        private bool _wasIdleOffScreen;
        private bool _lastDown;
        private OrderClickLook _lastLook = new OrderClickLook { IconPixels = double.NaN };
        private string _seenRules = string.Empty;

        public OrderClickGuard(IOrderClickWorld world)
        {
            _world = world;
        }

        public OrderClickStats Stats => _stats;
        public OrderClickTracker Tracker => _tracker;

        /// <summary>The guard holds the game's targeting off right now.</summary>
        public bool Applied => _applied;

        public void Start()
        {
            _seenRules = OrderClickRules.From(TraxSettings.Shared).Describe();
            TraxLog.Info("orders", "mission start: " + _seenRules);
        }

        /// <summary>Not in this battle (naval, no targeting handler): the summary says why.</summary>
        public void StandDown(string why)
        {
            _stats.StandDown = why;
            TraxLog.Info("orders", "no accidental charge orders (step 72) NOT guarding this battle: " + why + " - the game's own targeting");
        }

        /// <summary>One frame (the view's OnPreDisplayMissionTick - before the game's targeting handler recomputes its focus).</summary>
        public void Tick(double now)
        {
            if (_stats.StandDown != null) return;
            try
            {
                var rules = OrderClickRules.From(TraxSettings.Shared);
                NoteRules(rules, now);
                bool active = rules.On && _world.OrderModeActive;
                bool down = active && _world.ButtonDown;
                bool press = down && !_lastDown;
                _lastDown = down;
                var look = new OrderClickLook { IconPixels = double.NaN };
                if (active)
                {
                    // step 98: every frame in the orders menu - an off-screen focus is held off with no button down too
                    _world.Look(press, out look);
                    if (_tracker.Phase != ClickPhase.Released) _lastLook = look; // the release frame keeps the hold's last look for the log
                }
                bool moveSeen = _tracker.MoveSeen;
                double maxDrag = _tracker.MaxDragPixels; // the click's, before a finishing frame resets it
                var outcome = _tracker.Frame(in rules, active, down, look.HasTarget, look.OnScreen, look.IconPixels, down ? look.DragPixels : 0,
                    _world.ScreenHeight);
                if (outcome != ClickOutcome.None) Count(outcome, moveSeen, maxDrag, now);
                NoteIdleOffScreen(now);
                Apply(_tracker.Disable, now);
            }
            catch (Exception e)
            {
                _stats.Errors++;
                AthleticsLogic.Failed("orders.tick", e);
                SafeRelease();
            }
        }

        /// <summary>An order the game issued for the player's formations (OrderController.OnOrderIssued).</summary>
        public void OnOrder(bool targetedCharge, bool move, double now)
        {
            try
            {
                if (targetedCharge) _stats.TargetedChargesSeen++;
                if (targetedCharge && _tracker.Phase == ClickPhase.Idle)
                {
                    // step 98: a targeted charge with no click in progress - an F key, or a release whose press the guard never saw
                    _stats.ChargesOutsideClick++;
                    if (!_firstOutsideCharge)
                    {
                        _firstOutsideCharge = true;
                        TraxLog.Info("orders", "first targeted charge with NO click in progress at " + Sec(now) + " s (step 98): an F key, or a release whose press the"
                            + " guard never saw - the targeting " + (_applied ? "held OFF (tell Claude)" : "the game's") + "; the game's focus at the last look: "
                            + _lastLook.Describe());
                    }
                }
                _tracker.NoteOrder(targetedCharge, move);
            }
            catch (Exception e)
            {
                _stats.Errors++;
                AthleticsLogic.Failed("orders.order", e);
            }
        }

        /// <summary>The mission ends / the view goes: the game's targeting back.</summary>
        public void Release() => SafeRelease();

        public IList<string> SummaryLines() => _stats.SummaryLines(OrderClickRules.From(TraxSettings.Shared));

        private void Apply(bool disable, double now)
        {
            if (disable)
            {
                _stats.DisabledFrames++;
                if (!_firstHoldOff && _tracker.Phase != ClickPhase.Idle)
                {
                    _firstHoldOff = true;
                    TraxLog.Info("orders", "first time the game's unit targeting was held OFF during a click at " + Sec(now) + " s (step 72): "
                        + (_tracker.Dragged ? "a drag (" + F0(_tracker.MaxDragPixels) + " px from the press)" : _tracker.PressOnIcon ? "the cursor left the banner icon" : "pressed off any banner icon")
                        + (_tracker.TargetSeen ? " - the game had a unit under the cursor to charge" : " - no unit under the cursor yet"));
                }
            }
            if (disable == _applied) return;
            _world.SetTargetingDisabled(disable);
            _applied = disable;
        }

        /// <summary>Step 98: the idle hold of an off-screen focus - counted per spell and per frame, its first time in full.</summary>
        private void NoteIdleOffScreen(double now)
        {
            bool on = _tracker.IdleOffScreen;
            if (on)
            {
                _stats.OffScreenHoldFrames++;
                if (!_wasIdleOffScreen) _stats.OffScreenHolds++;
                if (!_firstOffScreenIdle)
                {
                    _firstOffScreenIdle = true;
                    TraxLog.Info("orders", "first time an off-screen / behind-the-camera unit had the focus - targeting held off at " + Sec(now) + " s (step 98): "
                        + _lastLook.Describe() + " - a click now would have charged it; held off (no highlight, no charge) until the game focuses a unit on the screen");
                }
            }
            _wasIdleOffScreen = on;
        }

        private void SafeRelease()
        {
            try
            {
                if (_applied) _world.SetTargetingDisabled(false);
            }
            catch (Exception e)
            {
                _stats.Errors++;
                AthleticsLogic.Failed("orders.release", e);
            }
            _applied = false;
        }

        private void Count(ClickOutcome outcome, bool moveSeen, double maxDrag, double now)
        {
            _stats.Add(outcome, moveSeen);
            switch (outcome)
            {
                case ClickOutcome.ChargeKept:
                    if (_firstKept) return;
                    _firstKept = true;
                    TraxLog.Info("orders", "first deliberate charge KEPT at " + Sec(now) + " s (step 72): a click on the unit's banner icon - the game's own targeted charge; "
                        + _lastLook.Describe());
                    return;
                case ClickOutcome.ConvertedDrag:
                    if (_firstDrag) return;
                    _firstDrag = true;
                    TraxLog.Info("orders", "first accidental charge turned into a MOVE at " + Sec(now) + " s (step 72): a drag across a unit (the cursor "
                        + F0(maxDrag) + " px from the press) - the game's targeting held off, its troop placer's move "
                        + (moveSeen ? "went out" : "NOT seen (a move order was expected - tell Claude)"));
                    return;
                case ClickOutcome.ConvertedOffIcon:
                    if (_firstOffIcon) return;
                    _firstOffIcon = true;
                    TraxLog.Info("orders", "first accidental charge turned into a MOVE at " + Sec(now) + " s (step 72): a click near a unit but not on its banner icon"
                        + " - the game's targeting held off, its troop placer's move " + (moveSeen ? "went out" : "NOT seen (a move order was expected - tell Claude)"));
                    return;
                case ClickOutcome.ConvertedOffScreen:
                    if (_firstOffScreenClick) return;
                    _firstOffScreenClick = true;
                    TraxLog.Info("orders", "first click while an off-screen / behind-the-camera unit had the focus at " + Sec(now) + " s (step 98): " + _lastLook.Describe()
                        + " - the game's targeting held off, its troop placer's move " + (moveSeen ? "went out" : "NOT seen (a move order was expected - tell Claude)"));
                    return;
                case ClickOutcome.Leak:
                    if (_firstLeak) return;
                    _firstLeak = true;
                    TraxLog.Info("orders", "FAILURE at " + Sec(now) + " s (step 72): a targeted charge went out although the click was being turned into a move"
                        + " - the game's targeting was off; something else issued it (tell Claude); the game's focus at the last look: " + _lastLook.Describe());
                    return;
            }
        }

        private void NoteRules(in OrderClickRules rules, double now)
        {
            string text = rules.Describe();
            if (text == _seenRules) return;
            _seenRules = text;
            TraxLog.Info("orders", "settings changed mid-mission at " + Sec(now) + " s: " + text);
        }

        private static string Sec(double t) => double.IsNaN(t) ? "?" : t.ToString("0.0", CultureInfo.InvariantCulture);
        private static string F0(double v) => double.IsInfinity(v) ? "off-screen" : v.ToString("0", CultureInfo.InvariantCulture);
    }
}

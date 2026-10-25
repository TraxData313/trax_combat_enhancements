using System;
using System.Collections.Generic;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 72's settings, read live (AI_NOTES "Step 72", DESIGN decision 66): the master switch FIRST, OrderNoAccidentalCharge, the drag
    /// threshold and the icon radius - both in UI pixels of the 1080p layout, scaled by the screen's height like vanilla's own 70 px.
    /// </summary>
    public readonly struct OrderClickRules
    {
        public OrderClickRules(bool modEnabled, bool enabled, double dragPixels = 12, double iconPixels = 30)
        {
            ModEnabled = modEnabled;
            GuardEnabled = enabled;
            DragPixels = Clamp(dragPixels, OrderClickMath.MinPixels, OrderClickMath.MaxPixels);
            IconPixels = Clamp(iconPixels, OrderClickMath.MinPixels, OrderClickMath.MaxPixels);
        }

        public static OrderClickRules From(TraxSettings s) =>
            new OrderClickRules(s.ModEnabled, s.OrderNoAccidentalCharge, s.OrderChargeDragPixels, s.OrderChargeIconPixels);

        public bool ModEnabled { get; }

        /// <summary>OrderNoAccidentalCharge (the switch itself).</summary>
        public bool GuardEnabled { get; }

        /// <summary>The master switch FIRST, then OrderNoAccidentalCharge.</summary>
        public bool On => ModEnabled && GuardEnabled;

        public string? OffBecause => !ModEnabled ? "ModEnabled" : !GuardEnabled ? "OrderNoAccidentalCharge" : null;

        /// <summary>OrderChargeDragPixels: the cursor this far (1080p UI px) from where the press began = a DRAG.</summary>
        public double DragPixels { get; }

        /// <summary>OrderChargeIconPixels: the cursor within this (1080p UI px) of a unit's banner icon = ON the icon.</summary>
        public double IconPixels { get; }

        public string Describe()
        {
            string what = "no accidental charge orders (step 72): a unit is charged from the orders menu only by a CLICK with the cursor on its banner icon "
                          + "(within " + Px(IconPixels) + " px at the press AND at the release); a drag (the cursor " + Px(DragPixels)
                          + "+ px from where the press began) or a click off the icon places the move order instead; the F keys and the gamepad untouched";
            return On ? what + " - ON" : what + " - OFF (" + OffBecause + "): the game's own targeting (a release within "
                                         + Px(OrderClickMath.VanillaFocusPixels) + " px of a unit's banner charges it)";
        }

        private static string Px(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

        private static double Clamp(double v, double min, double max) => double.IsNaN(v) ? min : v < min ? min : v > max ? max : v;
    }

    /// <summary>Step 72: where an orders-menu click stands.</summary>
    public enum ClickPhase
    {
        /// <summary>No click: the game's own targeting (the F keys' targeted charge keeps working).</summary>
        Idle,

        /// <summary>The left button is down - a click (or a drag) in the making.</summary>
        Holding,

        /// <summary>The frame the button came up: the decision of the hold stands one more frame, so the game's release reads it.</summary>
        Released,
    }

    /// <summary>Step 72: why a click's charge was turned into the placer's move.</summary>
    public enum ClickReason
    {
        None,

        /// <summary>The cursor travelled OrderChargeDragPixels or more from where the press began.</summary>
        Drag,

        /// <summary>A click, but the cursor was not on the unit's banner icon at the press or at the release.</summary>
        OffIcon,
    }

    /// <summary>Step 72: how one orders-menu click ended.</summary>
    public enum ClickOutcome
    {
        /// <summary>No click ended this frame.</summary>
        None,

        /// <summary>A plain click ON a unit's banner icon: the game's targeted charge went out (vanilla).</summary>
        ChargeKept,

        /// <summary>The game would have charged a unit - a drag: its targeting was off, the placer's move stood.</summary>
        ConvertedDrag,

        /// <summary>The game would have charged a unit - a click off its icon: its targeting was off, the placer's move stood.</summary>
        ConvertedOffIcon,

        /// <summary>No unit under the cursor at all (the game would not have charged): a plain move, nothing to do.</summary>
        PlainMove,

        /// <summary>A targeted charge went out although the click was being converted - a FAILURE (the log says so).</summary>
        Leak,

        /// <summary>The click was cut short (the menu closed, the switch went off) - not counted.</summary>
        Aborted,

        /// <summary>Step 98: the game would have charged a unit whose banner was OFF the screen (vanilla's 10 m rule) - its targeting was off, a move.</summary>
        ConvertedOffScreen,
    }

    /// <summary>
    /// Step 98: one look at what the game focuses (the replica of its pick) - the unit a release now would charge, whether its banner point is
    /// really ON the screen (in front of the camera, inside the screen), the cursor → its icon and → the press point, and the numbers the log
    /// prints when a click or an idle frame goes wrong.
    /// </summary>
    public struct OrderClickLook
    {
        /// <summary>The game would charge a unit if the button came up now.</summary>
        public bool HasTarget;

        /// <summary>That unit's banner point is in front of the camera and inside the screen (false = off the screen / behind the camera).</summary>
        public bool OnScreen;

        /// <summary>That unit was picked by vanilla's "within 10 m of the camera" rule (focused wherever it is on the screen).</summary>
        public bool NearCamera;

        /// <summary>The cursor → that unit's banner icon (screen px; NaN = no unit).</summary>
        public double IconPixels;

        /// <summary>The cursor → the press point (screen px; 0 at the press, infinity = the press point behind the camera).</summary>
        public double DragPixels;

        /// <summary>That unit's median → the camera on the ground plane (m).</summary>
        public double CameraMetres;

        /// <summary>That unit's banner point on the screen (px; may lie outside it) and the projection's w (≤ 0 = behind the camera).</summary>
        public double ScreenX, ScreenY, W;

        /// <summary>The cursor (px) and the screen's size (px).</summary>
        public double CursorX, CursorY, ScreenWidth, ScreenHeight;

        /// <summary>The log's words for this focus.</summary>
        public string Describe()
        {
            if (!HasTarget) return "no unit focused";
            var c = CultureInfo.InvariantCulture;
            return "the unit " + CameraMetres.ToString("0.0", c) + " m from the camera"
                   + (NearCamera ? " (the game's 10 m rule focuses it wherever it is on the screen)" : "")
                   + ", its banner at (" + ScreenX.ToString("0", c) + ", " + ScreenY.ToString("0", c) + ") px, w " + W.ToString("0.###", c)
                   + (OnScreen ? " - on the screen" : W <= 0 ? " - BEHIND the camera" : " - OFF the screen")
                   + " (" + ScreenWidth.ToString("0", c) + " x " + ScreenHeight.ToString("0", c) + "), the cursor at ("
                   + CursorX.ToString("0", c) + ", " + CursorY.ToString("0", c) + "), "
                   + (double.IsNaN(IconPixels) || double.IsInfinity(IconPixels) ? "?" : IconPixels.ToString("0", c)) + " px from its icon";
        }
    }

    /// <summary>
    /// Step 72's pure geometry and the game's own focus rule, replicated (vanilla's MissionFormationTargetSelectionHandler, v1.4.8): the
    /// FOCUSED unit is every enemy formation within 10 m of the camera (its "distance to the centre" 0), then the one whose median point
    /// 3 m above the ground projects nearest the SCREEN CENTRE within 70 px × height / 1080; the click's charge goes to the first of them.
    /// </summary>
    public static class OrderClickMath
    {
        /// <summary>Vanilla's focus reach around the screen centre at 1080p (MaxDistanceToCenterForFocus).</summary>
        public const double VanillaFocusPixels = 70;

        /// <summary>Vanilla's layout height the pixel settings are given at.</summary>
        public const double ReferenceHeight = 1080;

        /// <summary>Vanilla: a formation this close to the camera (2D) is focused whatever the screen says.</summary>
        public const double NearCameraMetres = 10;

        /// <summary>Vanilla: a formation this far from the camera is never focused.</summary>
        public const double FarMetres = 1000;

        /// <summary>The banner icon (and vanilla's focus point) floats this high above the formation's median point.</summary>
        public const double MarkerHeightMetres = 3;

        public const double MinPixels = 1;
        public const double MaxPixels = 200;

        /// <summary>1080p UI pixels → this screen's pixels (vanilla scales its 70 px the same way); a bad height → unscaled.</summary>
        public static double Scaled(double pixels, double screenHeight) =>
            screenHeight > 0 && !double.IsNaN(screenHeight) && !double.IsInfinity(screenHeight) ? pixels * screenHeight / ReferenceHeight : pixels;

        public static double Distance(double x1, double y1, double x2, double y2)
        {
            double dx = x1 - x2, dy = y1 - y2;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>The cursor this far from the press point = a drag (NaN / infinity - the point lost behind the camera - counts as one).</summary>
        public static bool IsDrag(double pixelsFromPress, double thresholdPixels) =>
            double.IsNaN(pixelsFromPress) || pixelsFromPress >= thresholdPixels;

        /// <summary>The cursor this far from the focused unit's icon is ON it (NaN = no icon = not on it).</summary>
        public static bool OnIcon(double pixelsFromIcon, double radiusPixels) =>
            !double.IsNaN(pixelsFromIcon) && pixelsFromIcon <= radiusPixels;

        /// <summary>
        /// Step 98: a banner point really ON the screen - in front of the camera (w &gt; 0, not behind) and inside the screen's
        /// <paramref name="width"/> × <paramref name="height"/> (the usable area's pixels). NaN / infinity = not on it. Vanilla's 10 m rule focuses a
        /// unit wherever its point projects (only "behind the camera" is refused), so a click can charge a unit the player cannot see.
        /// </summary>
        public static bool OnScreen(double x, double y, bool behind, double width, double height)
        {
            if (behind) return false;
            if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y)) return false;
            if (!(width > 0) || !(height > 0)) return false;
            return x >= 0 && x <= width && y >= 0 && y <= height;
        }

        /// <summary>
        /// Vanilla's pick: the index (in iteration order) of the formation the click's charge would go to, or -1. <paramref name="cameraMetres"/>:
        /// each enemy formation's median → camera distance on the ground plane; <paramref name="behind"/>: its focus point behind the camera;
        /// <paramref name="screenX"/> / <paramref name="screenY"/>: its focus point on the screen (pixels).
        /// </summary>
        public static int VanillaPick(int count, double[] cameraMetres, bool[] behind, double[] screenX, double[] screenY,
            double centreX, double centreY, double screenHeight)
        {
            double best = Scaled(VanillaFocusPixels, screenHeight);
            int bestIndex = -1;
            for (int i = 0; i < count; i++)
            {
                double d = cameraMetres[i];
                if (double.IsNaN(d) || d >= FarMetres) continue;
                if (d <= NearCameraMetres)
                {
                    if (!behind[i]) return i; // vanilla's distance-0 entries come first in its list, in this order
                    continue;
                }
                if (behind[i]) continue;
                double c = Distance(screenX[i], screenY[i], centreX, centreY);
                if (c < best)
                {
                    best = c;
                    bestIndex = i;
                }
            }
            return bestIndex;
        }

        public static string OutcomeName(ClickOutcome o)
        {
            switch (o)
            {
                case ClickOutcome.ChargeKept: return "a deliberate charge kept (a click on the banner)";
                case ClickOutcome.ConvertedDrag: return "a charge turned into a move (a drag)";
                case ClickOutcome.ConvertedOffIcon: return "a charge turned into a move (a click off the banner icon)";
                case ClickOutcome.PlainMove: return "a plain move (no unit under the cursor)";
                case ClickOutcome.Leak: return "a charge that SLIPPED THROUGH a conversion";
                case ClickOutcome.Aborted: return "a click cut short";
                case ClickOutcome.ConvertedOffScreen: return "a charge turned into a move (a unit OFF the screen had the focus)";
                default: return "none";
            }
        }
    }

    /// <summary>
    /// Step 72's per-click state machine - pure, fed once a frame. While the left button is held in the orders menu it says whether the game's
    /// formation targeting must be OFF: a drag (sticky for the click), or the cursor not on the focused unit's icon at the press or now. The
    /// decision of the last held frame stands through the release frame (the game reads its focus on the release), then the click is
    /// counted and targeting is the game's again. The game's orders seen meanwhile (<see cref="NoteOrder"/>) say what really went out.
    /// </summary>
    public sealed class OrderClickTracker
    {
        public ClickPhase Phase { get; private set; }

        /// <summary>The game's formation targeting must be off now.</summary>
        public bool Disable { get; private set; }

        public bool PressOnIcon { get; private set; }
        public bool Dragged { get; private set; }

        /// <summary>The game had a unit to charge at the last held frame (the replica's pick).</summary>
        public bool TargetSeen { get; private set; }

        public bool ChargeSeen { get; private set; }
        public bool MoveSeen { get; private set; }

        /// <summary>The largest press → cursor distance of this click (screen px).</summary>
        public double MaxDragPixels { get; private set; }

        public ClickReason Reason => Dragged ? ClickReason.Drag : Disable ? ClickReason.OffIcon : ClickReason.None;

        private bool _prevDown;

        /// <summary>
        /// One frame. <paramref name="active"/>: the guard is on (master switch, the setting) AND the orders menu is open with the mouse;
        /// <paramref name="down"/>: the left button; <paramref name="hasTarget"/>: the game would charge a unit if released now;
        /// <paramref name="iconPixels"/>: the cursor → that unit's icon (screen px, NaN = none); <paramref name="dragPixels"/>: the cursor →
        /// the press point (screen px; 0 at the press). Returns how a click ended this frame (<see cref="ClickOutcome.None"/> mostly).
        /// </summary>
        public ClickOutcome Frame(in OrderClickRules rules, bool active, bool down, bool hasTarget, double iconPixels, double dragPixels, double screenHeight) =>
            Frame(in rules, active, down, hasTarget, hasTarget, iconPixels, dragPixels, screenHeight);

        /// <summary>
        /// Step 98: one frame with <paramref name="targetOnScreen"/> - the focused unit's banner really on the screen. A unit OFF the screen (or
        /// behind the camera) is never "under the cursor": a press can keep a charge only on an on-screen icon, and while no button is down the
        /// game's targeting is held off as long as its focus is off the screen (<see cref="IdleOffScreen"/>) - so even a release the guard never
        /// saw pressed cannot charge a unit the player cannot see. Visible units: step 72 exactly (the F keys and the highlight as vanilla).
        /// </summary>
        public ClickOutcome Frame(in OrderClickRules rules, bool active, bool down, bool hasTarget, bool targetOnScreen, double iconPixels, double dragPixels,
            double screenHeight)
        {
            var outcome = ClickOutcome.None;
            if (!rules.On || !active)
            {
                // step 98: the release frame's order (a charge, or the placer's move) usually closes the orders menu - the click is FINISHED,
                // not cut short (step 72 counted every such click "cut short", so its kept charges and its leaks never showed); a hold that
                // already saw the release's order (the screen read the release before this tick) is finished too
                if (Phase == ClickPhase.Released || (Phase == ClickPhase.Holding && (ChargeSeen || MoveSeen))) outcome = Finish();
                else if (Phase != ClickPhase.Idle) outcome = ClickOutcome.Aborted;
                Reset();
                _prevDown = down;
                return outcome;
            }
            double icon = OrderClickMath.Scaled(rules.IconPixels, screenHeight);
            double drag = OrderClickMath.Scaled(rules.DragPixels, screenHeight);
            if (Phase == ClickPhase.Released)
            {
                outcome = Finish();
                Reset();
            }
            if (Phase == ClickPhase.Idle)
            {
                if (down && !_prevDown)
                {
                    // the press
                    Phase = ClickPhase.Holding;
                    PressOnIcon = hasTarget && targetOnScreen && OrderClickMath.OnIcon(iconPixels, icon);
                    Dragged = false;
                    MaxDragPixels = 0;
                    ChargeSeen = false;
                    MoveSeen = false;
                    TargetOffScreen = false;
                    IdleOffScreen = false;
                    Hold(hasTarget, targetOnScreen, iconPixels, dragPixels, icon, drag);
                }
                else
                {
                    // step 98: no click - the game's targeting untouched unless its focus is a unit off the screen
                    IdleOffScreen = hasTarget && !targetOnScreen;
                    Disable = IdleOffScreen;
                }
            }
            else if (Phase == ClickPhase.Holding)
            {
                if (down) Hold(hasTarget, targetOnScreen, iconPixels, dragPixels, icon, drag);
                else Phase = ClickPhase.Released; // the decision of the hold stands this frame
            }
            _prevDown = down;
            return outcome;
        }

        /// <summary>Step 98: no button down, and the game's focus is a unit OFF the screen / behind the camera - its targeting held off.</summary>
        public bool IdleOffScreen { get; private set; }

        /// <summary>Step 98: during this click the game's focus was (at the last held frame) a unit off the screen.</summary>
        public bool TargetOffScreen { get; private set; }

        /// <summary>An order the game issued for the player's formations: counted against the click in progress (if any).</summary>
        public void NoteOrder(bool targetedCharge, bool move)
        {
            if (Phase == ClickPhase.Idle) return;
            if (targetedCharge) ChargeSeen = true;
            if (move) MoveSeen = true;
        }

        private void Hold(bool hasTarget, bool targetOnScreen, double iconPixels, double dragPixels, double icon, double drag)
        {
            TargetSeen = hasTarget;
            TargetOffScreen = hasTarget && !targetOnScreen;
            double d = double.IsNaN(dragPixels) ? double.PositiveInfinity : dragPixels;
            if (d > MaxDragPixels) MaxDragPixels = d;
            if (OrderClickMath.IsDrag(dragPixels, drag)) Dragged = true;
            bool onNow = hasTarget && targetOnScreen && OrderClickMath.OnIcon(iconPixels, icon);
            Disable = Dragged || !PressOnIcon || !onNow;
        }

        private ClickOutcome Finish()
        {
            if (ChargeSeen) return Disable ? ClickOutcome.Leak : ClickOutcome.ChargeKept;
            if (Disable && TargetSeen)
                return Dragged ? ClickOutcome.ConvertedDrag : TargetOffScreen ? ClickOutcome.ConvertedOffScreen : ClickOutcome.ConvertedOffIcon;
            return ClickOutcome.PlainMove;
        }

        private void Reset()
        {
            Phase = ClickPhase.Idle;
            Disable = false;
            PressOnIcon = false;
            Dragged = false;
            TargetSeen = false;
            TargetOffScreen = false;
            IdleOffScreen = false;
            ChargeSeen = false;
            MoveSeen = false;
            MaxDragPixels = 0;
        }
    }

    /// <summary>Step 72's numbers over one mission - the [summary] "order clicks" line.</summary>
    public sealed class OrderClickStats
    {
        public int Clicks;
        public int ChargesKept;
        public int ConvertedDrag;
        public int ConvertedOffIcon;
        public int MovesAfterConversion;
        public int PlainMoves;
        public int Leaks;
        public int Aborted;
        public int TargetedChargesSeen;
        public int Errors;

        /// <summary>Step 98: clicks whose charge would have gone to a unit OFF the screen - turned into moves.</summary>
        public int ConvertedOffScreen;

        /// <summary>Step 98: times (and frames) the targeting was held off with no button down because the game focused a unit off the screen.</summary>
        public int OffScreenHolds;
        public int OffScreenHoldFrames;

        /// <summary>Step 98: targeted charges the game issued with no click in progress (an F key, or a press the guard never saw).</summary>
        public int ChargesOutsideClick;

        /// <summary>Frames the guard held the game's targeting off.</summary>
        public int DisabledFrames;

        public string? StandDown;

        public void Add(ClickOutcome o, bool moveSeen)
        {
            switch (o)
            {
                case ClickOutcome.None: return;
                case ClickOutcome.Aborted: Aborted++; return;
                case ClickOutcome.ChargeKept: ChargesKept++; break;
                case ClickOutcome.ConvertedDrag: ConvertedDrag++; if (moveSeen) MovesAfterConversion++; break;
                case ClickOutcome.ConvertedOffIcon: ConvertedOffIcon++; if (moveSeen) MovesAfterConversion++; break;
                case ClickOutcome.ConvertedOffScreen: ConvertedOffScreen++; if (moveSeen) MovesAfterConversion++; break;
                case ClickOutcome.PlainMove: PlainMoves++; break;
                case ClickOutcome.Leak: Leaks++; break;
            }
            Clicks++;
        }

        public int Converted => ConvertedDrag + ConvertedOffIcon + ConvertedOffScreen;

        public IList<string> SummaryLines(in OrderClickRules rules)
        {
            var lines = new List<string>();
            string state = rules.On ? "ON" : "OFF (" + rules.OffBecause + ")";
            if (StandDown != null)
            {
                lines.Add("order clicks (step 72): not guarded this battle - " + StandDown + "; switch " + state);
                return lines;
            }
            lines.Add("order clicks (step 72): " + Clicks + " clicks in the orders menu - " + ChargesKept + " deliberate charges kept (a click on the banner), "
                      + Converted + " charges turned into moves (" + ConvertedDrag + " drags, " + ConvertedOffIcon + " clicks off the icon; the placer's move seen after "
                      + MovesAfterConversion + " of them), " + PlainMoves + " plain moves (no unit under the cursor), " + Leaks + " charges SLIPPED THROUGH, "
                      + Aborted + " cut short, " + Errors + " errors; targeted charges the game issued " + TargetedChargesSeen + "; targeting held off "
                      + DisabledFrames + " frames; step 98 - a unit OFF the screen had the focus: " + ConvertedOffScreen + " clicks on it turned into moves, "
                      + OffScreenHolds + " times held off with no button down (" + OffScreenHoldFrames + " frames); targeted charges with no click in progress (an F key, "
                      + "or a press never seen) " + ChargesOutsideClick + "; switch " + state);
            return lines;
        }
    }
}

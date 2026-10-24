using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 72 - NO ACCIDENTAL CHARGE ORDERS (Toni 2026-10-19: placing a unit with a click or a drag, the cursor crossing another unit's floating
/// banner turned the move into a charge at it - "only ... when my cursor is directly over their icon and when I click it, not click and drag";
/// AI_NOTES "Step 72", DESIGN decision 66): the settings, the game's own focus rule replicated, the per-click decision (press / hold / drag /
/// on the icon → the game's targeting held off or not), how a click is counted, the summary line.
/// </summary>
public class Step72Tests
{
    private const double H = 1080; // a 1080p screen: the settings' pixels as they are

    private static OrderClickRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return OrderClickRules.From(s);
    }

    private static readonly ParamDef[] New =
    {
        SettingsSchema.OrderNoAccidentalCharge, SettingsSchema.OrderChargeDragPixels, SettingsSchema.OrderChargeIconPixels,
    };

    [Fact]
    public void The_three_settings_have_their_own_Advanced_group_before_Advanced_live_and_default_on_12_30()
    {
        var all = SettingsSchema.All.ToList();
        int last = all.IndexOf(SettingsSchema.MoraleLabelMax);
        for (int i = 0; i < New.Length; i++)
        {
            Assert.Equal(last + 1 + i, all.IndexOf(New[i]));
            Assert.Same(SettingsSchema.OrderClickGroup, New[i].Group);
            Assert.Equal(SettingsPage.Advanced, New[i].Group.Page);
            Assert.Equal(ApplyTiming.Live, New[i].Timing);
            Assert.DoesNotContain(New[i], SettingsSchema.PlayerPage);
        }
        var groups = SettingsSchema.Groups;
        Assert.Equal(groups.Count - 4, groups.ToList().IndexOf(SettingsSchema.OrderClickGroup)); // right before step 73's "Line discipline by tier", step 84's "Unit changes", then "Advanced"
        var r = Rules();
        Assert.True(r.On);
        Assert.Equal(12, r.DragPixels);
        Assert.Equal(30, r.IconPixels);
    }

    [Fact]
    public void The_master_switch_comes_first_and_the_sentence_says_step_72()
    {
        var off = Rules(s => s.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm));
        Assert.False(off.On);
        Assert.Equal("ModEnabled", off.OffBecause);
        Assert.Equal("OrderNoAccidentalCharge", Rules(s => s.Set(SettingsSchema.OrderNoAccidentalCharge, false, SettingSources.Mcm)).OffBecause);
        Assert.Contains("(step 72)", Rules().Describe());
        Assert.Contains("within 30 px at the press AND at the release", Rules().Describe());
        Assert.EndsWith("- ON", Rules().Describe());
        Assert.Contains("OFF (ModEnabled): the game's own targeting (a release within 70 px", off.Describe());

        // off: the tracker never holds the targeting off, whatever the click does
        var t = new OrderClickTracker();
        t.Frame(in off, true, true, true, 100, 0, H);
        Assert.False(t.Disable);
        Assert.Equal(ClickPhase.Idle, t.Phase);
    }

    [Fact]
    public void Pixels_scale_with_the_screen_height_like_vanillas_70()
    {
        Assert.Equal(30, OrderClickMath.Scaled(30, 1080), 6);
        Assert.Equal(60, OrderClickMath.Scaled(30, 2160), 6);
        Assert.Equal(20, OrderClickMath.Scaled(30, 720), 6);
        Assert.Equal(30, OrderClickMath.Scaled(30, 0), 6); // a bad height: unscaled
        Assert.True(OrderClickMath.IsDrag(12, 12));
        Assert.False(OrderClickMath.IsDrag(11.9, 12));
        Assert.True(OrderClickMath.IsDrag(double.NaN, 12)); // the press point lost: a drag
        Assert.True(OrderClickMath.OnIcon(30, 30));
        Assert.False(OrderClickMath.OnIcon(30.1, 30));
        Assert.False(OrderClickMath.OnIcon(double.NaN, 30));
    }

    [Fact]
    public void Vanillas_pick_is_a_unit_within_10_m_first_else_the_banner_nearest_the_centre_within_70_px()
    {
        double cx = 960, cy = 540;
        // A: 50 px off the centre, B: 20 px off - B is the pick
        Assert.Equal(1, OrderClickMath.VanillaPick(2, new[] { 50.0, 80 }, new[] { false, false }, new[] { 1010.0, 980 }, new[] { 540.0, 540 }, cx, cy, H));
        // nobody within 70 px: none
        Assert.Equal(-1, OrderClickMath.VanillaPick(1, new[] { 50.0 }, new[] { false }, new[] { 1100.0 }, new[] { 540.0 }, cx, cy, H));
        // 69 px at 1080p - in; at 2160p the reach is 140 px
        Assert.Equal(0, OrderClickMath.VanillaPick(1, new[] { 50.0 }, new[] { false }, new[] { 1029.0 }, new[] { 540.0 }, cx, cy, H));
        Assert.Equal(0, OrderClickMath.VanillaPick(1, new[] { 50.0 }, new[] { false }, new[] { 2059.0 }, new[] { 1080.0 }, 1920, 1080, 2160)); // 139 px
        // a unit within 10 m of the camera wins whatever the screen says (vanilla's distance 0 - "over its men"), unless behind the camera
        Assert.Equal(1, OrderClickMath.VanillaPick(2, new[] { 50.0, 8 }, new[] { false, false }, new[] { 961.0, 1700 }, new[] { 540.0, 900 }, cx, cy, H));
        Assert.Equal(0, OrderClickMath.VanillaPick(2, new[] { 50.0, 8 }, new[] { false, true }, new[] { 961.0, 1700 }, new[] { 540.0, 900 }, cx, cy, H));
        // behind the camera / 1000 m and more: never
        Assert.Equal(-1, OrderClickMath.VanillaPick(2, new[] { 50.0, 1000 }, new[] { true, false }, new[] { 960.0, 960 }, new[] { 540.0, 540 }, cx, cy, H));
    }

    /// <summary>One click: frames of (down, hasTarget, icon px, drag px); returns the tracker and every frame's Disable and outcome.</summary>
    private static (OrderClickTracker t, bool[] disable, ClickOutcome[] outcome) Run(OrderClickRules r, params (bool down, bool target, double icon, double drag)[] frames)
    {
        var t = new OrderClickTracker();
        var d = new bool[frames.Length];
        var o = new ClickOutcome[frames.Length];
        for (int i = 0; i < frames.Length; i++)
        {
            var f = frames[i];
            o[i] = t.Frame(in r, true, f.down, f.target, f.icon, f.drag, H);
            d[i] = t.Disable;
        }
        return (t, d, o);
    }

    [Fact]
    public void A_plain_click_on_the_banner_keeps_the_games_targeted_charge()
    {
        var r = Rules();
        var t = new OrderClickTracker();
        t.Frame(in r, true, true, true, 10, 0, H);   // the press on the icon
        Assert.False(t.Disable);
        Assert.True(t.PressOnIcon);
        t.Frame(in r, true, true, true, 12, 3, H);   // held, a little jitter
        Assert.False(t.Disable);
        t.Frame(in r, true, false, true, 12, 3, H);  // the release frame: the game reads its focus - still on
        Assert.False(t.Disable);
        Assert.Equal(ClickPhase.Released, t.Phase);
        t.NoteOrder(targetedCharge: true, move: false); // the game's charge at the unit
        var outcome = t.Frame(in r, true, false, false, double.NaN, 0, H);
        Assert.Equal(ClickOutcome.ChargeKept, outcome);
        Assert.Equal(ClickPhase.Idle, t.Phase);
        Assert.False(t.Disable);
    }

    [Fact]
    public void A_drag_across_a_banner_holds_the_targeting_off_through_the_release_and_counts_as_a_converted_drag()
    {
        var r = Rules();
        // pressed on open ground (no unit), dragged across a banner (on the icon at the end), released there
        var (t, disable, outcome) = Run(r,
            (true, false, double.NaN, 0),
            (true, true, 40, 6),
            (true, true, 5, 25),     // 25 px from the press: a drag
            (true, true, 0, 40),
            (false, true, 0, 40));   // the release frame
        Assert.True(disable[0]);     // pressed off any icon: off from the start
        Assert.True(disable[2]);
        Assert.True(disable[3]);     // even with the cursor right on the icon
        Assert.True(disable[4]);     // the release frame keeps it off
        Assert.True(t.Dragged);
        t.NoteOrder(targetedCharge: false, move: true); // the placer's move
        Assert.Equal(ClickOutcome.ConvertedDrag, t.Frame(in r, true, false, false, double.NaN, 0, H));
        Assert.False(t.Disable);     // idle again: the game's targeting back (the F keys work as vanilla)
        Assert.All(outcome, o => Assert.Equal(ClickOutcome.None, o));
    }

    [Fact]
    public void A_drag_that_starts_on_the_icon_is_still_a_drag()
    {
        var r = Rules();
        var (t, disable, _) = Run(r,
            (true, true, 5, 0),      // pressed ON the icon
            (true, true, 8, 11),     // 11 px - not yet
            (true, true, 9, 13),     // 13 px - a drag from here on (sticky)
            (true, true, 2, 2));     // back over the press point: still a drag
        Assert.False(disable[0]);
        Assert.False(disable[1]);
        Assert.True(disable[2]);
        Assert.True(disable[3]);
        Assert.Equal(ClickReason.Drag, t.Reason);
        Assert.Equal(13, t.MaxDragPixels, 6);
    }

    [Fact]
    public void A_click_near_the_banner_or_over_the_men_is_a_move()
    {
        var r = Rules();
        // vanilla focuses it (50 px from the centre - within its 70), but the cursor is 50 px from the icon - not on it
        var (t, disable, _) = Run(r, (true, true, 50, 0), (true, true, 50, 1), (false, true, 50, 1));
        Assert.All(disable, d => Assert.True(d));
        Assert.Equal(ClickOutcome.ConvertedOffIcon, t.Frame(in r, true, false, false, double.NaN, 0, H));

        // pressed on the icon, the cursor slid off before the release (no drag: the camera / the unit moved)
        var (t2, d2, _) = Run(r, (true, true, 10, 0), (true, true, 45, 5));
        Assert.False(d2[0]);
        Assert.True(d2[1]);
        Assert.Equal(ClickReason.OffIcon, t2.Reason);

        // pressed off the icon, the unit walked under the cursor: the press counts - no charge
        var (t3, d3, _) = Run(r, (true, true, 60, 0), (true, true, 4, 2));
        Assert.True(d3[1]);
        Assert.False(t3.PressOnIcon);
    }

    [Fact]
    public void A_click_with_no_unit_under_the_cursor_is_a_plain_move_and_a_charge_that_slips_through_is_a_failure()
    {
        var r = Rules();
        var (t, _, _) = Run(r, (true, false, double.NaN, 0), (false, false, double.NaN, 0));
        Assert.Equal(ClickOutcome.PlainMove, t.Frame(in r, true, false, false, double.NaN, 0, H));

        var (t2, _, _) = Run(r, (true, true, 80, 0), (false, true, 80, 0));
        t2.NoteOrder(targetedCharge: true, move: false); // the game charged although its targeting was off
        Assert.Equal(ClickOutcome.Leak, t2.Frame(in r, true, false, false, double.NaN, 0, H));

        // an order outside a click is not the click's
        var idle = new OrderClickTracker();
        idle.NoteOrder(true, true);
        Assert.False(idle.ChargeSeen);
    }

    [Fact]
    public void The_menu_closing_or_the_switch_going_off_mid_click_lets_go_at_once()
    {
        var r = Rules();
        var t = new OrderClickTracker();
        t.Frame(in r, true, true, true, 80, 0, H);
        Assert.True(t.Disable);
        Assert.Equal(ClickOutcome.Aborted, t.Frame(in r, false, true, true, 80, 0, H)); // the orders menu closed
        Assert.False(t.Disable);
        // still held when the menu opens again: not a press - nothing until the button comes up and goes down again
        t.Frame(in r, true, true, true, 80, 0, H);
        Assert.Equal(ClickPhase.Idle, t.Phase);
        t.Frame(in r, true, false, false, double.NaN, 0, H);
        t.Frame(in r, true, true, true, 80, 0, H);
        Assert.Equal(ClickPhase.Holding, t.Phase);
        var off = Rules(s => s.Set(SettingsSchema.OrderNoAccidentalCharge, false, SettingSources.Mcm));
        Assert.Equal(ClickOutcome.Aborted, t.Frame(in off, true, true, true, 80, 0, H));
        Assert.False(t.Disable);
    }

    [Fact]
    public void A_quick_second_click_right_after_a_release_is_a_new_press()
    {
        var r = Rules();
        var t = new OrderClickTracker();
        t.Frame(in r, true, true, true, 80, 0, H);
        t.Frame(in r, true, false, true, 80, 0, H);              // released
        var o = t.Frame(in r, true, true, true, 5, 0, H);         // the next frame: counted, and a new press ON the icon
        Assert.Equal(ClickOutcome.ConvertedOffIcon, o);
        Assert.Equal(ClickPhase.Holding, t.Phase);
        Assert.False(t.Disable);
    }

    [Fact]
    public void The_summary_line_counts_every_outcome()
    {
        var s = new OrderClickStats();
        s.Add(ClickOutcome.ChargeKept, false);
        s.Add(ClickOutcome.ConvertedDrag, true);
        s.Add(ClickOutcome.ConvertedDrag, false);
        s.Add(ClickOutcome.ConvertedOffIcon, true);
        s.Add(ClickOutcome.PlainMove, true);
        s.Add(ClickOutcome.Leak, false);
        s.Add(ClickOutcome.Aborted, false);
        s.Add(ClickOutcome.None, false);
        Assert.Equal(6, s.Clicks);
        Assert.Equal(3, s.Converted);
        Assert.Equal(2, s.MovesAfterConversion);
        string line = s.SummaryLines(Rules())[0];
        Assert.StartsWith("order clicks (step 72): 6 clicks in the orders menu - 1 deliberate charges kept", line);
        Assert.Contains("3 charges turned into moves (2 drags, 1 clicks off the icon; the placer's move seen after 2 of them)", line);
        Assert.Contains("1 charges SLIPPED THROUGH, 1 cut short", line);
        Assert.EndsWith("switch ON", line);
        s.StandDown = "a naval battle";
        Assert.Equal("order clicks (step 72): not guarded this battle - a naval battle; switch ON", s.SummaryLines(Rules()).Single());
    }
}

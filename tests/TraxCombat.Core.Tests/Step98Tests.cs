using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 98 - AN OFF-SCREEN UNIT NEVER TAKES THE CHARGE (Toni 2026-10-25: "the bandits far on the left outside the screen are highlighted and I
/// click on them and I'm ordering a charge"; AI_NOTES "Step 98"): vanilla's 10 m rule focuses a unit wherever its banner projects, so the
/// guard keeps a charge only for a press ON an on-screen icon, holds the targeting off with no button down while the focus is off the screen,
/// and counts a click whose release closed the orders menu.
/// </summary>
public class Step98Tests
{
    private const double H = 1080;

    private static OrderClickRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return OrderClickRules.From(s);
    }

    [Fact]
    public void On_the_screen_means_in_front_of_the_camera_and_inside_the_screen()
    {
        Assert.True(OrderClickMath.OnScreen(960, 540, false, 1920, 1080));
        Assert.True(OrderClickMath.OnScreen(0, 1080, false, 1920, 1080));
        Assert.False(OrderClickMath.OnScreen(-420, 560, false, 1920, 1080));   // the bandits off the left edge
        Assert.False(OrderClickMath.OnScreen(2400, 560, false, 1920, 1080));
        Assert.False(OrderClickMath.OnScreen(960, -5, false, 1920, 1080));
        Assert.False(OrderClickMath.OnScreen(960, 540, true, 1920, 1080));    // behind the camera, even if it flips into the middle
        Assert.False(OrderClickMath.OnScreen(double.NaN, 540, false, 1920, 1080));
        Assert.False(OrderClickMath.OnScreen(double.PositiveInfinity, 540, false, 1920, 1080));
        Assert.False(OrderClickMath.OnScreen(960, 540, false, 0, 1080));
    }

    [Fact]
    public void Vanillas_10_m_rule_picks_a_unit_whatever_its_screen_point()
    {
        // a unit 7.5 m from the camera, its banner far off the left edge, and a far unit right at the centre: vanilla's charge goes to the near one
        int pick = OrderClickMath.VanillaPick(2, new[] { 7.5, 60.0 }, new[] { false, false }, new[] { -420.0, 960.0 }, new[] { 560.0, 540.0 }, 960, 540, H);
        Assert.Equal(0, pick);
        Assert.False(OrderClickMath.OnScreen(-420, 560, false, 1920, 1080));
    }

    [Fact]
    public void An_off_screen_focus_never_keeps_a_charge_even_with_its_projection_under_the_cursor()
    {
        var r = Rules();
        var t = new OrderClickTracker();
        // the press with the off-screen unit's projected point 5 px from the cursor (a degenerate projection): still held off
        t.Frame(in r, true, true, true, false, 5, 0, H);
        Assert.True(t.Disable);
        Assert.False(t.PressOnIcon);
        t.Frame(in r, true, true, true, false, 5, 1, H);
        t.Frame(in r, true, false, true, false, 5, 1, H);      // the release frame: still held off
        Assert.True(t.Disable);
        Assert.Equal(ClickOutcome.ConvertedOffScreen, t.Frame(in r, true, false, true, false, 1380, 0, H));

        // pressed on an on-screen icon, then the focus jumps to an off-screen unit: held off from that frame
        var t2 = new OrderClickTracker();
        t2.Frame(in r, true, true, true, true, 8, 0, H);
        Assert.False(t2.Disable);
        t2.Frame(in r, true, true, true, false, 8, 0, H);
        Assert.True(t2.Disable);
    }

    [Fact]
    public void An_on_screen_icon_click_still_charges_and_the_old_overload_is_step_72()
    {
        var r = Rules();
        var t = new OrderClickTracker();
        t.Frame(in r, true, true, true, true, 8, 0, H);
        Assert.False(t.Disable);
        t.Frame(in r, true, true, true, true, 9, 2, H);
        t.Frame(in r, true, false, true, true, 9, 2, H);
        Assert.False(t.Disable);
        t.NoteOrder(targetedCharge: true, move: false);
        Assert.Equal(ClickOutcome.ChargeKept, t.Frame(in r, true, false, true, true, 9, 0, H));

        var old = new OrderClickTracker();
        old.Frame(in r, true, true, true, 8, 0, H);
        Assert.False(old.Disable);
    }

    [Fact]
    public void With_no_button_an_off_screen_focus_is_held_off_and_a_visible_one_is_vanillas()
    {
        var r = Rules();
        var t = new OrderClickTracker();
        t.Frame(in r, true, false, true, false, 1380, 0, H);
        Assert.True(t.IdleOffScreen);
        Assert.True(t.Disable);
        t.Frame(in r, true, false, true, true, 50, 0, H);       // the unit came into view - the F keys and the highlight as vanilla
        Assert.False(t.IdleOffScreen);
        Assert.False(t.Disable);
        t.Frame(in r, true, false, false, true, double.NaN, 0, H);
        Assert.False(t.Disable);

        // the master switch first, then the setting: vanilla
        var off = Rules(s => s.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm));
        var t3 = new OrderClickTracker();
        t3.Frame(in off, true, false, true, false, 1380, 0, H);
        Assert.False(t3.Disable);
        var guardOff = Rules(s => s.Set(SettingsSchema.OrderNoAccidentalCharge, false, SettingSources.Mcm));
        t3.Frame(in guardOff, true, false, true, false, 1380, 0, H);
        Assert.False(t3.Disable);
        // the orders menu closed: nothing held
        var t4 = new OrderClickTracker();
        t4.Frame(in r, false, false, true, false, 1380, 0, H);
        Assert.False(t4.Disable);
    }

    [Fact]
    public void A_release_that_closes_the_orders_menu_is_counted_not_cut_short()
    {
        var r = Rules();
        // the guard's tick first: released, then the menu closed by the release's order
        var t = new OrderClickTracker();
        t.Frame(in r, true, true, true, true, 8, 0, H);
        t.Frame(in r, true, false, true, true, 8, 0, H);
        t.NoteOrder(true, false);
        Assert.Equal(ClickOutcome.ChargeKept, t.Frame(in r, false, false, false, false, double.NaN, 0, H));

        // the screen read the release first: the order seen during the hold, the menu already closed at the tick
        var t2 = new OrderClickTracker();
        t2.Frame(in r, true, true, true, true, 80, 0, H);
        t2.NoteOrder(false, true);
        Assert.Equal(ClickOutcome.ConvertedOffIcon, t2.Frame(in r, false, false, false, false, double.NaN, 0, H));

        // a leak shows now
        var t3 = new OrderClickTracker();
        t3.Frame(in r, true, true, true, true, 80, 0, H);
        t3.Frame(in r, true, false, true, true, 80, 0, H);
        t3.NoteOrder(true, false);
        Assert.Equal(ClickOutcome.Leak, t3.Frame(in r, false, false, false, false, double.NaN, 0, H));

        // a menu closing mid-hold with no order: still cut short
        var t4 = new OrderClickTracker();
        t4.Frame(in r, true, true, true, true, 80, 0, H);
        Assert.Equal(ClickOutcome.Aborted, t4.Frame(in r, false, true, true, true, 80, 0, H));
    }

    [Fact]
    public void The_summary_line_counts_the_off_screen_cases()
    {
        var s = new OrderClickStats();
        s.Add(ClickOutcome.ConvertedOffScreen, moveSeen: true);
        s.Add(ClickOutcome.ChargeKept, moveSeen: false);
        s.OffScreenHolds = 3;
        s.OffScreenHoldFrames = 240;
        s.ChargesOutsideClick = 1;
        string line = s.SummaryLines(Rules()).Single();
        Assert.StartsWith("order clicks (step 72): 2 clicks in the orders menu - 1 deliberate charges kept (a click on the banner), 1 charges turned into moves (0 drags, 0 clicks off the icon; the placer's move seen after 1 of them)", line);
        Assert.Contains("step 98 - a unit OFF the screen had the focus: 1 clicks on it turned into moves, 3 times held off with no button down (240 frames); "
                        + "targeted charges with no click in progress (an F key, or a press never seen) 1; switch ON", line);
        Assert.Equal("a charge turned into a move (a unit OFF the screen had the focus)", OrderClickMath.OutcomeName(ClickOutcome.ConvertedOffScreen));
    }

    [Fact]
    public void The_look_describes_where_the_focus_is()
    {
        var look = new OrderClickLook
        {
            HasTarget = true, OnScreen = false, NearCamera = true, CameraMetres = 7.5, ScreenX = -420, ScreenY = 560, W = 1, IconPixels = 1380,
            CursorX = 960, CursorY = 540, ScreenWidth = 1920, ScreenHeight = 1080,
        };
        string d = look.Describe();
        Assert.Contains("7.5 m from the camera (the game's 10 m rule focuses it wherever it is on the screen)", d);
        Assert.Contains("(-420, 560) px, w 1 - OFF the screen (1920 x 1080), the cursor at (960, 540), 1380 px from its icon", d);
        look.W = -0.5;
        Assert.Contains("BEHIND the camera", look.Describe());
        Assert.Equal("no unit focused", new OrderClickLook().Describe());
    }
}

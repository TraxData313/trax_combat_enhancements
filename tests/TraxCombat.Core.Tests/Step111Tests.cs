using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 111 (Toni's training field 2026-11-10, the veteran trainer with a shield; AI_NOTES "Step 111", DESIGN decision 100): the hold gate (no hold
/// while he reels / is not recovered / you reel), the arrow's latch through your swing, the perfect strike's flight, the stats' line and the settings.
/// </summary>
public class Step111Tests
{
    [Fact]
    public void The_settings_sit_after_the_openings_log_lines_with_their_defaults_and_are_live()
    {
        var all = SettingsSchema.All.ToList();
        int d = all.IndexOf(SettingsSchema.OpeningLogInfoLines);
        Assert.Equal(d + 1, all.IndexOf(SettingsSchema.OpeningHoldEndsWhenYouReel));
        Assert.Equal(d + 2, all.IndexOf(SettingsSchema.OpeningArrowSwingHoldMaxSeconds));
        Assert.Equal(d + 3, all.IndexOf(SettingsSchema.PerfectStrikeFlightSeconds));
        foreach (var p in new[] { SettingsSchema.OpeningHoldEndsWhenYouReel, SettingsSchema.OpeningArrowSwingHoldMaxSeconds, SettingsSchema.PerfectStrikeFlightSeconds })
        {
            Assert.DoesNotContain(p, SettingsSchema.PlayerPage);
            Assert.Same(SettingsSchema.OpeningLogInfoLines.Group, p.Group);
            Assert.Equal(ApplyTiming.Live, p.Timing);
        }
        var s = new TraxSettings();
        var hr = OpeningHoldRules.From(s);
        Assert.True(hr.EndsWhenYouReel);
        Assert.Equal(1.5, hr.ArrowSwingMax, 6);
        Assert.Equal(0.8, PerfectStrikeRules.From(s).FlightSeconds, 6);
        s.Set(SettingsSchema.PerfectStrikeFlightSeconds, 99.0, SettingSources.File); // clamped by the schema's range
        Assert.True(PerfectStrikeRules.From(s).FlightSeconds <= PerfectStrikeMath.MaxFlightSeconds);
    }

    [Fact]
    public void One_gate_refuses_a_hold_while_you_reel_he_reels_or_he_is_not_recovered()
    {
        Assert.Equal(HoldRefusal.None, OpeningHoldMath.Refuse(false, false, false, true));
        Assert.Equal(HoldRefusal.HeReels, OpeningHoldMath.Refuse(true, true, false, true)); // the perfect-strike window - never a hold
        Assert.Equal(HoldRefusal.HeImmune, OpeningHoldMath.Refuse(false, true, false, true)); // pic 4: his shield block right as the reel ended
        Assert.Equal(HoldRefusal.YouReel, OpeningHoldMath.Refuse(false, false, true, true)); // pic 9: "X ripostes - you reel"
        Assert.Equal(HoldRefusal.YouReel, OpeningHoldMath.Refuse(true, true, true, true)); // you first
        Assert.Equal(HoldRefusal.None, OpeningHoldMath.Refuse(false, false, true, false)); // the switch off = step 108
        Assert.Equal("you reeled", OpeningHoldMath.EndName(HoldEnd.YouReel));
        Assert.NotEqual("none", OpeningHoldMath.RefusalName(HoldRefusal.HeImmune));
    }

    private static ArrowReading R(ArrowCue cue, int attack) => new ArrowReading { Cue = cue, Mask = OpeningMath.Bit(attack), Alpha = 1f };

    [Fact]
    public void The_arrow_never_turns_while_your_swing_is_in_the_air()
    {
        var top = R(ArrowCue.Finish, OpeningMath.AttackUp);
        var left = R(ArrowCue.Hold, OpeningMath.AttackLeft);
        var latch = new ArrowLatch();
        latch.Begin(10, in top); // pic 3: the overhead begun on the perfect-strike arrow
        var shown = latch.Filter(10.3, 1.5, in left, out bool held); // pic 4: a hold's left mid-swing
        Assert.True(held);
        Assert.Equal(top.Mask, shown.Mask);
        Assert.Equal(ArrowCue.Finish, shown.Cue);
        Assert.True(latch.Held);
        shown = latch.Filter(10.4, 1.5, default, out held); // the window closed: the arrow may go
        Assert.False(held);
        Assert.False(shown.Shows);
        latch.End(); // the swing hit / missed: the arrow follows again
        shown = latch.Filter(10.5, 1.5, in left, out held);
        Assert.False(held);
        Assert.Equal(left.Mask, shown.Mask);

        // none shown at the wind-up: the arrow may APPEAR, then it stays
        latch.Begin(20, default);
        shown = latch.Filter(20.2, 1.5, in left, out held);
        Assert.False(held);
        Assert.Equal(left.Mask, shown.Mask);
        shown = latch.Filter(20.3, 1.5, in top, out held);
        Assert.True(held);
        Assert.Equal(left.Mask, shown.Mask);
        // the cap: a feint never resolves - after it the arrow follows the game
        shown = latch.Filter(21.6, 1.5, in top, out held);
        Assert.False(held);
        Assert.Equal(top.Mask, shown.Mask);
        Assert.False(latch.Active);
        // 0 = no latch (step 108)
        latch.Begin(30, in top);
        shown = latch.Filter(30.1, 0, in left, out held);
        Assert.False(held);
        Assert.Equal(left.Mask, shown.Mask);
        // the 106 flash is never held
        latch.Begin(40, in top);
        var flash = new ArrowReading { Cue = ArrowCue.Flash, Mask = OpeningMath.Bit(OpeningMath.AttackDown), Alpha = 0.5f };
        shown = latch.Filter(40.1, 1.5, in flash, out held);
        Assert.False(held);
        Assert.Equal(ArrowCue.Flash, shown.Cue);
    }

    [Fact]
    public void A_perfect_strike_swing_begun_in_the_window_keeps_it_open_until_its_flight_ends()
    {
        // pic 3-5: the reel to 57.85 s, the grace 0.15 s; the overhead began at 57.3 s
        double reel = 57.85, grace = 0.15, flight = 0.8;
        double until = PerfectStrikeMath.FlightEnd(57.3, reel, grace, flight);
        Assert.Equal(reel + grace + flight, until, 6);
        Assert.True(PerfectStrikeMath.OpenFor(58.2, reel, grace, until)); // the reel over - the flight keeps it
        Assert.False(PerfectStrikeMath.Open(58.2, reel, grace));
        Assert.True(PerfectStrikeMath.Outlasted(58.2, reel, grace));
        Assert.False(PerfectStrikeMath.OpenFor(until + 0.01, reel, grace, until));
        Assert.False(PerfectStrikeMath.OpenFor(58.2, reel, grace, 0)); // no flight = step 100g
        Assert.Equal(0, PerfectStrikeMath.FlightEnd(58.2, reel, grace, flight)); // begun after the window: nothing
        Assert.Equal(0, PerfectStrikeMath.FlightEnd(57.3, reel, grace, 0)); // the setting 0: nothing
    }

    [Fact]
    public void The_summary_lines_carry_the_step_111_clauses_and_the_new_end()
    {
        var st = new OpeningHoldStats();
        st.NoteRefused(HoldRefusal.HeImmune, true);
        st.NoteRefused(HoldRefusal.YouReel, true);
        st.NoteRefused(HoldRefusal.HeReels, false);
        st.NoteRefused(HoldRefusal.None, false);
        st.NoteEnd(HoldEnd.YouReel);
        st.ArrowSwingsHeld = 2;
        st.KickRollsRefused = 3;
        st.KicksCut = 1;
        string line = st.SummaryLine(OpeningHoldRules.From(new TraxSettings()));
        Assert.Contains("STEP 111 - no hold (your hit / his block): he reels 1 / 0, not recovered (stagger-immune) 0 / 1, you reel 0 / 1 (on)", line);
        Assert.Contains("your swings whose arrow change was held until they resolved 2", line);
        Assert.Contains("kick / bash rolls refused for a held man 3, his running kick / bash cut by the forced guard 1", line);
        Assert.Contains("you reeled 1", line);
        var ps = new PerfectStrikeStats { Flights = 3, FlightsOutlasted = 1, FlightsLanded = 2, FlightsBlocked = 1, FlightStrikesAfterReel = 1 };
        string pl = ps.SummaryLine(PerfectStrikeRules.From(new TraxSettings()));
        Assert.Contains("STEP 111 - your swings begun in a window 3 (+0.8 s at most past it; outlasted his reel - his guard kept down by the flight 1): landed 2, blocked / not on his body 1", pl);
        Assert.Contains("perfect strikes landed after his reel thanks to the flight 1", pl);
    }

    [Fact]
    public void The_latch_and_the_gate_allocate_nothing()
    {
        var top = R(ArrowCue.Finish, OpeningMath.AttackUp);
        var left = R(ArrowCue.Hold, OpeningMath.AttackLeft);
        var latch = new ArrowLatch();
        long bytes = NoAllocation.Bytes(() =>
        {
            for (int i = 0; i < 1000; i++)
            {
                latch.Begin(i, in top);
                latch.Filter(i + 0.1, 1.5, in left, out _);
                latch.End();
                OpeningHoldMath.Refuse(i % 2 == 0, i % 3 == 0, i % 5 == 0, true);
                PerfectStrikeMath.OpenFor(i, i - 0.5, 0.15, i + 0.2);
            }
        });
        Assert.Equal(0, bytes);
    }
}

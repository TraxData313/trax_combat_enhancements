using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>Step 46 (Anton, 2026-10-17: "how tired they are in a bar above that fever bar ... a green bar that gets to red"): every man's own
/// Athletics bar under ALT, drawn by step 44's view above his fever bar - the fill and the colour as the strip / the player bar read them, the
/// stack (a fixed height while the fever bar's place is kept), the switches by side with the master switch first, the nearest on the screen
/// projecting only the cap's worth, the summary.</summary>
public class AthleticsMarkTests
{
    private static readonly BarRules Design = new(75, 50, 25);

    private static void Near(double expected, double actual, double eps = 1e-5) =>
        Assert.True(Math.Abs(expected - actual) <= eps, "expected " + expected + ", got " + actual);

    private static FeverMarkRules Fever(bool mod = true, bool athletics = true, bool fever = true, bool show = true, bool enemy = true, int max = 200) =>
        new(mod, athletics, fever, show, enemy, max, 22, 5, 2.5);

    private static AthleticsMarkRules Bars(bool mod = true, bool athletics = true, bool show = true, bool enemy = true, int width = 22, int height = 5, int gap = 2) =>
        new(mod, athletics, show, enemy, width, height, gap);

    // ------------------------------------------------------------------ the bar's reading: the fill, the wound cap, the colour

    [Theory]
    [InlineData(1.00, BarBand.Green)]   // full
    [InlineData(0.75, BarBand.Green)]   // at the peak line (AthleticsPeakPercent 75): f = 1
    [InlineData(0.60, BarBand.Blue)]    // f 0.8 - just below the line
    [InlineData(0.50, BarBand.Yellow)]  // f 0.67
    [InlineData(0.30, BarBand.Orange)]  // f 0.4
    [InlineData(0.15, BarBand.Red)]     // f 0.2
    [InlineData(0.00, BarBand.Red)]     // empty
    public void The_fill_is_his_share_of_his_full_pool_and_the_colour_the_strips_band(double fraction, BarBand band)
    {
        var m = new FeverMark();
        double f = fraction >= 0.75 ? 1.0 : fraction / 0.75;
        AthleticsMarkMath.Read(ref m, in Design, fraction, 1.0, f);
        Near(fraction, m.BarFill);
        Assert.Equal(1f, m.BarUsable);
        Assert.Equal(band, m.Band);
        Assert.Equal(BarMath.Band(in Design, f), m.Band);                                  // the very rule of the strip and the player bar
    }

    [Fact]
    public void A_wounded_man_shows_his_cap_and_never_a_fill_past_it()
    {
        var m = new FeverMark { HasBar = true };
        AthleticsMarkMath.Read(ref m, in Design, 0.6, 0.6, 0.8);                            // at his cap: 60% of the bar, blue
        Near(0.6, m.BarFill);
        Near(0.6, m.BarUsable);
        Assert.Equal(BarBand.Blue, m.Band);
        Assert.True(AthleticsMarkMath.Wounded(in m));
        Assert.Equal("Athletics 60% blue, wounded to 60%", AthleticsMarkMath.DescribeBar(in m));
        AthleticsMarkMath.Read(ref m, in Design, 0.9, 0.5, 1.0);                            // never past the cap (a stale read)
        Near(0.5, m.BarFill);
        AthleticsMarkMath.Read(ref m, in Design, double.NaN, double.NaN, double.NaN);       // fail safe: nothing drawn, no cap, no alarm
        Assert.Equal(0f, m.BarFill);
        Assert.Equal(1f, m.BarUsable);
        Assert.Equal(BarBand.Green, m.Band);
        Assert.False(AthleticsMarkMath.Wounded(in m));
        Assert.Equal("no Athletics bar", AthleticsMarkMath.DescribeBar(new FeverMark()));
    }

    [Fact]
    public void The_band_colours_are_the_bars_own_hex_constants()
    {
        for (int i = 0; i < BarMath.BandCount; i++)
        {
            Assert.True(AthleticsMarkMath.ParseHex(BarMath.ColorHex((BarBand)i), out float r, out float g, out float b, out float a));
            AthleticsMarkMath.BandColor((BarBand)i, out float br, out float bg, out float bb);
            Assert.Equal(r, br);
            Assert.Equal(g, bg);
            Assert.Equal(b, bb);
            Assert.Equal(1f, a);
        }
        AthleticsMarkMath.BandColor(BarBand.Green, out float gr, out float gg, out _);
        Near(0x5C / 255.0, gr);
        Near(0xB8 / 255.0, gg);
        Assert.True(AthleticsMarkMath.ParseHex(AthleticsMarkMath.WoundHex, out float wr, out _, out _, out _));
        Near(0x4A / 255.0, wr);
        Assert.False(AthleticsMarkMath.ParseHex("red", out _, out _, out _, out _));
    }

    // ------------------------------------------------------------------ the stack

    [Fact]
    public void The_athletics_bar_sits_on_the_fever_bar_with_the_gap_between()
    {
        var f = Fever();
        var a = Bars();
        AthleticsMarkMath.Layout(in f, in a, 1f, 10f, true, true, true, 960f, 400f, out var box);
        Assert.Equal(22f, box.Width);
        Assert.Equal(5f + 2f + 5f, box.Height);                                              // bar + gap + fever bar
        Assert.Equal(949f, box.Left);
        Assert.Equal(388f, box.Top);
        Assert.Equal(22f, box.BarWidth);
        Assert.Equal(5f, box.BarHeight);
        Assert.Equal(22f, box.FeverWidth);
        Assert.Equal(5f, box.FeverHeight);
    }

    [Fact]
    public void Without_a_fever_the_bar_keeps_its_height_while_the_fever_bars_place_is_kept()
    {
        var f = Fever();
        var a = Bars();
        AthleticsMarkMath.Layout(in f, in a, 1f, 10f, true, true, true, 960f, 400f, out var inFever);
        AthleticsMarkMath.Layout(in f, in a, 1f, 10f, true, false, true, 960f, 400f, out var calm);
        Assert.Equal(inFever.Top, calm.Top);                                                 // no jump when his fever ends
        Assert.Equal(inFever.Height, calm.Height);
        // the fever bars cannot show for his side (switched off, or the fever off): the bar drops onto the point
        AthleticsMarkMath.Layout(in f, in a, 1f, 10f, true, false, false, 960f, 400f, out var noSlot);
        Assert.Equal(5f, noSlot.Height);
        Assert.Equal(395f, noSlot.Top);
        Assert.Equal(0f, noSlot.FeverHeight);
        // only the fever bar (the Athletics bars off): exactly step 44's mark
        AthleticsMarkMath.Layout(in f, in a, 1f, 10f, false, true, true, 960f, 400f, out var feverOnly);
        FeverMarkMath.Size(in f, 1f, 10f, out float w, out float h);
        FeverMarkMath.Place(960f, 400f, w, h, out float left, out float top);
        Assert.Equal(left, feverOnly.Left);
        Assert.Equal(top, feverOnly.Top);
        Assert.Equal(w, feverOnly.Width);
        Assert.Equal(h, feverOnly.Height);
        Assert.Equal(0f, feverOnly.BarHeight);
    }

    [Fact]
    public void The_stack_scales_with_the_ui_and_shrinks_far_away_gap_included()
    {
        var f = Fever();
        var a = Bars(width: 30, height: 4, gap: 3);
        AthleticsMarkMath.Layout(in f, in a, 4 / 3f, 10f, true, true, true, 1280f, 720f, out var big);
        Near(30 * 4 / 3.0, big.Width, 1e-4);                                                // the wider bar sets the box
        Near((4 + 3 + 5) * 4 / 3.0, big.Height, 1e-4);
        Near(1280 - 20, big.Left, 1e-4);
        AthleticsMarkMath.Layout(in f, in a, 1f, 60f, true, true, true, 960f, 400f, out var far);
        Near(18, far.Width, 1e-4);                                                           // 60% at 60 m
        Near((4 + 3 + 5) * 0.6, far.Height, 1e-4);
        AthleticsMarkMath.Layout(in f, Bars(width: 1, height: 1, gap: 0), 0.5f, 100f, true, false, false, 0f, 0f, out var tiny);
        Assert.Equal(1f, tiny.BarWidth);                                                     // never below a pixel
        Assert.Equal(1f, tiny.Height);
        AthleticsMarkMath.Layout(in f, in a, float.NaN, 10f, true, false, false, 0f, 0f, out var nan);
        Assert.Equal(30f, nan.Width);                                                        // a nonsense scale = 1
    }

    [Fact]
    public void A_riders_bars_go_a_metre_higher()
    {
        Assert.Equal(2.5f, AthleticsMarkMath.MarkHeight(2.5f, false));
        Assert.Equal(3.5f, AthleticsMarkMath.MarkHeight(2.5f, true));
        Assert.Equal(1f, AthleticsMarkMath.RiderLiftMetres);
    }

    // ------------------------------------------------------------------ who gets which bar: the switches by side, the master switch first

    private static FeverMark Man(int side, bool inFever = false, float distance = 20f, bool front = true) =>
        new() { Side = side, InFever = inFever, Distance = distance, InFront = front };

    [Fact]
    public void Every_man_gets_his_bar_and_a_man_in_a_fever_both()
    {
        var f = Fever();
        var a = Bars();
        var calm = Man((int)MarkerTeam.Yours);
        Assert.Equal(FeverMarkSkip.None, AthleticsMarkMath.Decide(ref calm, in f, in a));
        Assert.True(calm.HasBar && !calm.HasFever && calm.FeverSlot);                        // the fever bar's place kept
        var hot = Man((int)MarkerTeam.Ally, inFever: true);
        Assert.Equal(FeverMarkSkip.None, AthleticsMarkMath.Decide(ref hot, in f, in a));
        Assert.True(hot.HasBar && hot.HasFever);
        var foe = Man((int)MarkerTeam.Enemy, inFever: true);
        Assert.Equal(FeverMarkSkip.None, AthleticsMarkMath.Decide(ref foe, in f, in a));
        Assert.True(foe.HasBar && foe.HasFever);
    }

    [Fact]
    public void The_enemy_switches_work_apart_and_together()
    {
        var foe = Man((int)MarkerTeam.Enemy, inFever: true);
        Assert.Equal(FeverMarkSkip.None, AthleticsMarkMath.Decide(ref foe, Fever(), Bars(enemy: false)));
        Assert.True(!foe.HasBar && foe.HasFever);                                             // his fever bar alone
        Assert.Equal(FeverMarkSkip.None, AthleticsMarkMath.Decide(ref foe, Fever(enemy: false), Bars()));
        Assert.True(foe.HasBar && !foe.HasFever && !foe.FeverSlot);                           // his Athletics bar alone, no empty place under it
        Assert.Equal(FeverMarkSkip.EnemyOff, AthleticsMarkMath.Decide(ref foe, Fever(enemy: false), Bars(enemy: false)));
        var calmFoe = Man((int)MarkerTeam.Enemy);
        Assert.Equal(FeverMarkSkip.EnemyOff, AthleticsMarkMath.Decide(ref calmFoe, Fever(), Bars(enemy: false)));
        var mine = Man((int)MarkerTeam.Yours);
        Assert.Equal(FeverMarkSkip.None, AthleticsMarkMath.Decide(ref mine, Fever(enemy: false), Bars(enemy: false))); // yours: never the enemy switches
    }

    [Fact]
    public void The_fever_bar_needs_the_fever_marks_on_and_the_bars_need_their_switch()
    {
        var hot = Man((int)MarkerTeam.Yours, inFever: true);
        Assert.Equal(FeverMarkSkip.None, AthleticsMarkMath.Decide(ref hot, Fever(show: false), Bars()));
        Assert.True(hot.HasBar && !hot.HasFever && !hot.FeverSlot);
        Assert.Equal(FeverMarkSkip.None, AthleticsMarkMath.Decide(ref hot, Fever(fever: false), Bars()));
        Assert.False(hot.HasFever);
        Assert.Equal(FeverMarkSkip.None, AthleticsMarkMath.Decide(ref hot, Fever(), Bars(show: false)));
        Assert.True(!hot.HasBar && hot.HasFever);                                             // step 44 exactly
        Assert.Equal(FeverMarkSkip.EnemyOff, AthleticsMarkMath.Decide(ref hot, Fever(mod: false), Bars(mod: false)));
    }

    [Fact]
    public void Then_behind_the_camera_and_too_close_in_order()
    {
        var behind = Man((int)MarkerTeam.Yours, distance: 1f, front: false);
        Assert.Equal(FeverMarkSkip.Behind, AthleticsMarkMath.Decide(ref behind, Fever(), Bars()));
        var close = Man((int)MarkerTeam.Yours, distance: 1.4f);
        Assert.Equal(FeverMarkSkip.TooClose, AthleticsMarkMath.Decide(ref close, Fever(), Bars()));
        var nan = Man((int)MarkerTeam.Yours, distance: float.NaN);
        Assert.Equal(FeverMarkSkip.TooClose, AthleticsMarkMath.Decide(ref nan, Fever(), Bars()));
        var foeBehind = Man((int)MarkerTeam.Enemy, front: false);
        Assert.Equal(FeverMarkSkip.EnemyOff, AthleticsMarkMath.Decide(ref foeBehind, Fever(enemy: false), Bars(enemy: false))); // the switch first
    }

    [Fact]
    public void Hidden_reasons_come_in_order_with_the_master_switch_first()
    {
        Assert.Equal(AthleticsMarksHidden.ModOff, Bars(mod: false, athletics: false, show: false).Hidden);
        Assert.Equal(AthleticsMarksHidden.AthleticsOff, Bars(athletics: false, show: false).Hidden);
        Assert.Equal(AthleticsMarksHidden.SwitchedOff, Bars(show: false).Hidden);
        Assert.Equal(AthleticsMarksHidden.None, Bars().Hidden);
        Assert.False(Bars(mod: false).On);
        Assert.Equal("ModEnabled off (the master switch)", AthleticsMarkMath.HiddenName(AthleticsMarksHidden.ModOff));
        Assert.Equal("ShowAthleticsMarks off", AthleticsMarkMath.HiddenName(AthleticsMarksHidden.SwitchedOff));
        var man = Man((int)MarkerTeam.Yours, inFever: true);
        Assert.Equal(FeverMarkSkip.None, AthleticsMarkMath.Decide(ref man, Fever(), Bars(athletics: false)));
        Assert.True(!man.HasBar && man.HasFever);                                             // Athletics off = no Athletics bar
        var calm = Man((int)MarkerTeam.Yours);
        Assert.NotEqual(FeverMarkSkip.None, AthleticsMarkMath.Decide(ref calm, Fever(), Bars(mod: false)));   // nothing to draw for him: skipped
    }

    [Fact]
    public void The_settings_are_live_grouped_and_in_order()
    {
        var t = new TraxSettings();
        var r = AthleticsMarkRules.From(t);
        Assert.True(r.On);
        Assert.True(r.ShowEnemy);
        Assert.Equal(22, r.Width);
        Assert.Equal(5, r.Height);
        Assert.Equal(2, r.Gap);
        t.Set(SettingsSchema.AthleticsMarksShowEnemy, false, SettingSources.Mcm);
        t.Set(SettingsSchema.AthleticsMarkGap, 6, SettingSources.Mcm);
        r = AthleticsMarkRules.From(t);
        Assert.False(r.ShowEnemy);
        Assert.Equal(6, r.Gap);
        t.Set(SettingsSchema.ShowAthleticsMarks, false, SettingSources.Mcm);
        Assert.Equal(AthleticsMarksHidden.SwitchedOff, AthleticsMarkRules.From(t).Hidden);
        t.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
        Assert.Equal(AthleticsMarksHidden.ModOff, AthleticsMarkRules.From(t).Hidden);
        Assert.Same(SettingsSchema.AltMarkersGroup, SettingsSchema.ShowAthleticsMarks.Group);
        Assert.Same(SettingsSchema.AltMarkersGroup, SettingsSchema.AthleticsMarksShowEnemy.Group);
        Assert.Same(SettingsSchema.AdvancedGroup, SettingsSchema.AthleticsMarkGap.Group);
        Assert.Equal(SettingsSchema.FeverMarksShowEnemy.Index + 1, SettingsSchema.ShowAthleticsMarks.Index);
        Assert.Equal(SettingsSchema.ShowAthleticsMarks.Index + 2, SettingsSchema.FeverMarkMax.Index);      // the one cap last
        Assert.Equal(SettingsSchema.FeverMarkHeightMetres.Index + 1, SettingsSchema.AthleticsMarkWidth.Index);
        Assert.Equal(ApplyTiming.Live, SettingsSchema.ShowAthleticsMarks.Timing);
        Assert.Equal(ApplyTiming.Live, SettingsSchema.AthleticsMarkGap.Timing);
        Assert.Equal("Athletics bars 22 x 5 UI px, 2 px above the fever bar (its place kept while he has none), the enemy's too", Bars().Describe());
        Assert.EndsWith("yours and your allies' only (AthleticsMarksShowEnemy off)", Bars(enemy: false).Describe());
    }

    // ------------------------------------------------------------------ the pick: the nearest on the screen, projecting only what it needs

    private struct Projector : IMarkProjector
    {
        public int Calls;
        public float OffScreenBeyondX;

        public bool ProjectOnScreen(ref FeverMark m)
        {
            Calls++;
            return m.WorldX < OffScreenBeyondX;
        }
    }

    [Fact]
    public void The_cap_takes_the_nearest_on_the_screen_and_projects_only_what_it_needs()
    {
        var f = new FeverMarkFrame();
        float[] distances = { 50, 10, 30, 5, 40, 20, 60, 70 };
        float[] xs = { 0, 0, 0, 99, 0, 0, 0, 0 };                                            // the nearest (5 m) is off the screen's side
        for (int i = 0; i < distances.Length; i++)
        {
            ref var m = ref f.Add();
            m = new FeverMark { Key = i, Distance = distances[i], WorldX = xs[i] };
            f.Elect(i);
        }
        var p = new Projector { OffScreenBeyondX = 50 };
        Assert.Equal(3, FeverMarkMath.PickOnScreen(f, 3, ref p));
        Assert.Equal(new[] { 1, 5, 2 }, new[] { f.Order[0], f.Order[1], f.Order[2] });       // 10, 20, 30 m - the 5 m man took no slot
        Assert.Equal(4, p.Calls);                                                            // 5, 10, 20, 30 m projected - never the 4 farther
        Assert.Equal(1, f.Skipped[(int)FeverMarkSkip.OffScreen]);
        Assert.Equal(4, f.Skipped[(int)FeverMarkSkip.Capped]);
        Assert.Equal(FeverMarkSkip.OffScreen, f.Marks[3].Skip);
        Assert.Equal(3, f.Picked);

        // under the cap: all projected, in their own order, nothing sorted
        f.Clear();
        for (int i = 0; i < 3; i++)
        {
            ref var m = ref f.Add();
            m = new FeverMark { Key = i, Distance = 30 - i, WorldX = i == 1 ? 99 : 0 };
            f.Elect(i);
        }
        p = new Projector { OffScreenBeyondX = 50 };
        Assert.Equal(2, FeverMarkMath.PickOnScreen(f, 200, ref p));
        Assert.Equal(new[] { 0, 2 }, new[] { f.Order[0], f.Order[1] });
        Assert.Equal(3, p.Calls);
        Assert.Equal(0, f.Skipped[(int)FeverMarkSkip.Capped]);
        f.Clear();
        f.Add();
        f.Elect(0);
        Assert.Equal(0, FeverMarkMath.PickOnScreen(f, -5, ref p));                           // a nonsense cap = none
        f.DropLast();
        Assert.Equal(0, f.Count);
    }

    // ------------------------------------------------------------------ the summary

    [Fact]
    public void The_summary_counts_the_athletics_bars_by_colour_and_the_fever_bars()
    {
        var s = new FeverMarkStats();
        s.NoteShown();
        var skipped = new int[(int)FeverMarkSkip.Count];
        s.NoteBars(150, new[] { 60, 40, 30, 15, 5 }, 12, 20);
        s.NoteFrame(0.5, 150, 900, 20, 500, 400, false, 200, skipped, 0.05);
        skipped[(int)FeverMarkSkip.Capped] = 700;
        s.NoteBars(200, new[] { 50, 50, 50, 30, 20 }, 8, 60);
        s.NoteFrame(0.5, 200, 1000, 60, 520, 480, true, 200, skipped, 0.07);
        Assert.Equal(175.0, s.MeanBars);
        Assert.Equal(40.0, s.MeanFeverBars);
        Assert.Equal(110, s.BarsIn(BarBand.Green));
        Assert.Equal(25, s.BarsIn(BarBand.Red));
        Assert.Equal("hud: ALT bars - shown 1x, on screen 1.0 s over 2 frames; men marked a frame: mean 175.0, max 200; Athletics bars a frame: mean 175.0, max 200 "
                     + "(summed over frames: green 110, blue 90, yellow 80, orange 45, red 25; wounded 20); fever bars a frame: mean 40.0, max 60; most men looked at in a frame 1000 "
                     + "(yours and allies 520, enemy 480), most in a fever 60; frames at the cap 1 (FeverMarkMax 200 at the first - the nearest drawn); men skipped, summed over frames: "
                     + "beyond the cap 700; cost 60 µs a frame (most 70 µs); errors 0",
            s.SummaryLine(0));
    }
}

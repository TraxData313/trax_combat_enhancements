using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>Step 44 (Anton, 2026-10-17): the FEVER MARK under ALT - our own small bar above each man in a fever while the game shows its
/// markers: bright orange armed and at the burn's start, through yellow as the store empties, vanilla's ALT-icon distance fade, smaller far
/// away, the nearest FeverMarkMax picked, the skips in order, the master switch first, the summary.</summary>
public class FeverMarkTests
{
    private static void Near(double expected, double actual, double eps = 1e-5) =>
        Assert.True(Math.Abs(expected - actual) <= eps, "expected " + expected + ", got " + actual);

    private static FeverMarkRules Rules(bool mod = true, bool athletics = true, bool fever = true, bool show = true, bool enemy = true, int max = 200,
        int width = 22, int height = 5, double metres = 2.5) => new(mod, athletics, fever, show, enemy, max, width, height, metres);

    // ------------------------------------------------------------------ the colour and alpha by the fever

    [Fact]
    public void Armed_and_the_burns_start_are_bright_orange_at_full_alpha()
    {
        FeverMarkMath.Tint(true, 0.3f, out float r, out float g, out float b, out float a); // armed: the share does not matter
        Near(1, r);
        Near(0x8C / 255.0, g);
        Near(0x1A / 255.0, b);
        Near(1, a);
        FeverMarkMath.Tint(false, 1f, out r, out g, out b, out a);                      // burning, just begun
        Near(0x8C / 255.0, g);
        Near(1, a);
    }

    [Fact]
    public void Burning_fades_through_yellow_and_is_gone_when_empty()
    {
        FeverMarkMath.Tint(false, 0.5f, out float r, out float g, out float b, out float a);
        Near(1, r);
        Near((0x8C + 0xE0) / 2.0 / 255.0, g);
        Near((0x1A + 0x40) / 2.0 / 255.0, b);
        Near(0.45 + 0.55 * 0.5, a);
        FeverMarkMath.Tint(false, 0.01f, out _, out g, out _, out a);
        Assert.True(g > 0.86f && a < 0.46f && a > 0.44f, "nearly empty: yellow, faint - g " + g + ", a " + a);
        FeverMarkMath.Tint(false, 0f, out _, out _, out _, out a);
        Assert.Equal(0f, a);                                                              // empty: no mark
        FeverMarkMath.Tint(false, float.NaN, out _, out _, out _, out a);
        Assert.Equal(0f, a);
        FeverMarkMath.Tint(false, 1.7f, out _, out g, out _, out a);                     // clamped
        Near(0x8C / 255.0, g);
        Near(1, a);
    }

    [Fact]
    public void The_colour_constants_match_the_maths()
    {
        Assert.Equal("#FF8C1AFF", FeverMarkMath.OrangeHex);
        Assert.Equal("#FFE040FF", FeverMarkMath.YellowHex);
        FeverMarkMath.Tint(false, 0f, out float r, out float g, out float b, out _);
        Near(0xE0 / 255.0, g);                                                            // at share 0 the colour is the yellow (alpha 0)
        Near(0x40 / 255.0, b);
        Near(1, r);
    }

    // ------------------------------------------------------------------ distance: vanilla's fade, the smaller size

    [Fact]
    public void The_fade_is_vanillas_alt_icon_fade()
    {
        Assert.Equal(0f, FeverMarkMath.Fade(0f));
        Assert.Equal(0f, FeverMarkMath.Fade(1.49f));
        Near(0, FeverMarkMath.Fade(1.5f));
        Near(0.5, FeverMarkMath.Fade(4.75f));
        Near(1, FeverMarkMath.Fade(8f));
        Near(1, FeverMarkMath.Fade(300f));
        Assert.Equal(0f, FeverMarkMath.Fade(float.NaN));
    }

    [Fact]
    public void Far_marks_are_smaller_down_to_60_percent()
    {
        Near(1, FeverMarkMath.SizeFactor(5));
        Near(1, FeverMarkMath.SizeFactor(20));
        Near(0.8, FeverMarkMath.SizeFactor(40));
        Near(0.6, FeverMarkMath.SizeFactor(60));
        Near(0.6, FeverMarkMath.SizeFactor(500));
        var r = Rules();
        FeverMarkMath.Size(in r, 1f, 10, out float w, out float h);
        Near(22, w);
        Near(5, h);
        FeverMarkMath.Size(in r, 4 / 3f, 10, out w, out h);                              // the UI scale applies
        Near(22 * 4 / 3.0, w, 1e-4);
        FeverMarkMath.Size(in r, 1f, 60, out w, out h);
        Near(13.2, w, 1e-4);
        Near(3, h, 1e-4);
        var tiny = Rules(width: 1, height: 1);
        FeverMarkMath.Size(in tiny, 0.5f, 100, out w, out h);
        Assert.Equal(1f, w);                                                              // never below a pixel
        Assert.Equal(1f, h);
    }

    [Fact]
    public void The_mark_sits_centred_with_its_bottom_on_the_point()
    {
        FeverMarkMath.Place(960, 400, 22, 5, out float left, out float top);
        Assert.Equal(949f, left);
        Assert.Equal(395f, top);
        Assert.True(FeverMarkMath.OnScreen(left, top, 22, 5, 1920, 1080));
        Assert.True(FeverMarkMath.OnScreen(-10, 0, 22, 5, 1920, 1080));                  // partly on screen counts
        Assert.False(FeverMarkMath.OnScreen(-30, 400, 22, 5, 1920, 1080));
        Assert.False(FeverMarkMath.OnScreen(1920, 400, 22, 5, 1920, 1080));
        Assert.False(FeverMarkMath.OnScreen(500, 1080, 22, 5, 1920, 1080));
        Assert.False(FeverMarkMath.OnScreen(float.NaN, 400, 22, 5, 1920, 1080));
    }

    // ------------------------------------------------------------------ the skips in order, the pick

    private static FeverMark Man(int key, int side, float distance, bool front = true, bool armed = true, float share = 1f) =>
        new() { Key = key, Side = side, Distance = distance, InFront = front, Armed = armed, Share = share };

    [Fact]
    public void The_skips_come_in_order_enemy_switch_behind_too_close()
    {
        var enemyBehindClose = Man(1, (int)MarkerTeam.Enemy, 1f, front: false);
        Assert.Equal(FeverMarkSkip.EnemyOff, FeverMarkMath.PreSkip(in enemyBehindClose, false));
        Assert.Equal(FeverMarkSkip.Behind, FeverMarkMath.PreSkip(in enemyBehindClose, true));
        var close = Man(2, (int)MarkerTeam.Yours, 1f);
        Assert.Equal(FeverMarkSkip.TooClose, FeverMarkMath.PreSkip(in close, false));
        var nan = Man(3, (int)MarkerTeam.Ally, float.NaN);
        Assert.Equal(FeverMarkSkip.TooClose, FeverMarkMath.PreSkip(in nan, false));
        var fine = Man(4, (int)MarkerTeam.Ally, 1.5f);
        Assert.Equal(FeverMarkSkip.None, FeverMarkMath.PreSkip(in fine, false));          // allies count with yours: never the enemy switch
        var enemy = Man(5, (int)MarkerTeam.Enemy, 30f);
        Assert.Equal(FeverMarkSkip.None, FeverMarkMath.PreSkip(in enemy, true));
    }

    [Fact]
    public void The_nearest_are_picked_when_the_cap_bites()
    {
        var f = new FeverMarkFrame();
        float[] distances = { 50, 10, 30, 5, 40, 20 };
        for (int i = 0; i < distances.Length; i++)
        {
            ref var m = ref f.Add();
            m = Man(i, 0, distances[i]);
            f.Elect(i);
        }
        Assert.Equal(3, FeverMarkMath.Pick(f, 3));
        var picked = new[] { f.Order[0], f.Order[1], f.Order[2] };
        Assert.Equal(new[] { 3, 1, 5 }, picked);                                          // 5 m, 10 m, 20 m
        Assert.Equal(3, f.Skipped[(int)FeverMarkSkip.Capped]);
        Assert.Equal(FeverMarkSkip.Capped, f.Marks[0].Skip);                              // 50 m waits
        Assert.Equal(FeverMarkSkip.None, f.Marks[3].Skip);

        // under the cap: all, in their own order, nothing sorted or skipped
        f.Clear();
        for (int i = 0; i < 3; i++)
        {
            ref var m = ref f.Add();
            m = Man(i, 0, 30 - i);
            f.Elect(i);
        }
        Assert.Equal(3, FeverMarkMath.Pick(f, 200));
        Assert.Equal(new[] { 0, 1, 2 }, new[] { f.Order[0], f.Order[1], f.Order[2] });
        Assert.Equal(0, f.Skipped[(int)FeverMarkSkip.Capped]);
        Assert.Equal(0, FeverMarkMath.Pick(f, -5));                                       // a nonsense cap = none
    }

    [Fact]
    public void The_frame_grows_past_its_first_size_and_is_reused()
    {
        var f = new FeverMarkFrame();
        for (int i = 0; i < 600; i++)
        {
            ref var m = ref f.Add();
            m = Man(i, 0, 1000 - i);
            f.Elect(i);
        }
        Assert.Equal(600, f.Count);
        Assert.Equal(2, f.Grown);                                                         // 256 → 512 → 1024
        Assert.Equal(200, FeverMarkMath.Pick(f, 200));
        Assert.Equal(599, f.Order[0]);                                                    // the nearest (401 m)
        var marks = f.Marks;
        f.Clear();
        Assert.Equal(0, f.Count);
        Assert.Equal(0, f.Skipped[(int)FeverMarkSkip.Capped]);
        ref var again = ref f.Add();
        Assert.Same(marks, f.Marks);                                                       // reused, not reallocated
        Assert.Equal(0, again.Key);
    }

    // ------------------------------------------------------------------ the rules: master switch first, live settings

    [Fact]
    public void Hidden_reasons_come_in_order_with_the_master_switch_first()
    {
        Assert.Equal(FeverMarksHidden.ModOff, Rules(mod: false, athletics: false, fever: false, show: false).Hidden);
        Assert.Equal(FeverMarksHidden.AthleticsOff, Rules(athletics: false, fever: false, show: false).Hidden);
        Assert.Equal(FeverMarksHidden.FeverOff, Rules(fever: false, show: false).Hidden);
        Assert.Equal(FeverMarksHidden.SwitchedOff, Rules(show: false).Hidden);
        Assert.Equal(FeverMarksHidden.None, Rules().Hidden);
        Assert.Equal("ModEnabled off (the master switch)", FeverMarkMath.HiddenName(FeverMarksHidden.ModOff));
        Assert.Equal("FeverEnabled off (no fevers to mark)", FeverMarkMath.HiddenName(FeverMarksHidden.FeverOff));
    }

    [Fact]
    public void The_settings_are_live_and_in_the_alt_group()
    {
        var t = new TraxSettings();
        var r = FeverMarkRules.From(t);
        Assert.Equal(FeverMarksHidden.None, r.Hidden);
        Assert.True(r.ShowEnemy);
        Assert.Equal(200, r.Max);
        Assert.Equal(22, r.Width);
        Assert.Equal(5, r.Height);
        Near(2.5, r.HeightMetres);
        t.Set(SettingsSchema.FeverMarkMax, 50, SettingSources.Mcm);
        t.Set(SettingsSchema.FeverMarksShowEnemy, false, SettingSources.Mcm);
        t.Set(SettingsSchema.FeverEnabled, false, SettingSources.Mcm);
        r = FeverMarkRules.From(t);
        Assert.Equal(50, r.Max);
        Assert.False(r.ShowEnemy);
        Assert.Equal(FeverMarksHidden.FeverOff, r.Hidden);
        t.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
        Assert.Equal(FeverMarksHidden.ModOff, FeverMarkRules.From(t).Hidden);
        Assert.Same(SettingsSchema.AltMarkersGroup, SettingsSchema.ShowFeverMarks.Group);
        Assert.Same(SettingsSchema.AltMarkersGroup, SettingsSchema.FeverMarkMax.Group);
        Assert.Same(SettingsSchema.AdvancedGroup, SettingsSchema.FeverMarkWidth.Group);
        Assert.Equal(SettingsSchema.AltMarkersShowEnemy.Index + 1, SettingsSchema.ShowFeverMarks.Index);
        Assert.Equal(ApplyTiming.Live, SettingsSchema.ShowFeverMarks.Timing);
        Assert.Equal(ApplyTiming.Live, SettingsSchema.FeverMarkHeightMetres.Timing);
    }

    [Fact]
    public void The_sentence_names_the_layout()
    {
        Assert.Equal("marks 22 x 5 UI px (smaller far away; the game's UI scale applies), 2.5 m above his feet, at most 200 a frame (FeverMarkMax - the nearest to the camera), the enemy's too",
            Rules().Describe());
        Assert.EndsWith("yours and your allies' only (FeverMarksShowEnemy off)", Rules(enemy: false).Describe());
        Assert.Equal("behind the camera 3, beyond the cap 12", FeverMarkMath.DescribeSkips(new[] { 0, 0, 0, 3, 0, 0, 12 }));
        Assert.Equal("none", FeverMarkMath.DescribeSkips(new int[7]));
        var m = Man(17, (int)MarkerTeam.Yours, 12.34f, armed: false, share: 0.4f);
        m.ScreenX = 950;
        m.ScreenY = 420;
        Assert.Equal("yours man 17 at (950, 420), 12.3 m, burning 40% left", FeverMarkMath.DescribeMark(in m));
    }

    // ------------------------------------------------------------------ the summary

    [Fact]
    public void The_summary_line_counts_frames_marks_the_cap_and_the_skips()
    {
        var s = new FeverMarkStats();
        Assert.Equal("hud: ALT bars - shown 0x (the game's markers never came up with the bars on - ALT never held or the orders menu never opened in a fight, or the Athletics and fever bars off); errors 0",
            s.SummaryLine(0));
        s.NoteShown();
        var skipped = new int[(int)FeverMarkSkip.Count];
        var none = new int[BarMath.BandCount];
        skipped[(int)FeverMarkSkip.Behind] = 10;
        s.NoteBars(0, none, 0, 100);                                                       // step 44's fever bars alone (the Athletics bars off)
        s.NoteFrame(0.5, 100, 150, 150, 120, 30, false, 200, skipped, 0.02);
        skipped[(int)FeverMarkSkip.Capped] = 5;
        s.NoteBars(0, none, 0, 200);
        s.NoteFrame(0.5, 200, 400, 400, 300, 100, true, 200, skipped, 0.04);
        Assert.Equal(2, s.Frames);
        Assert.Equal(150.0, s.MeanMarks);
        Assert.Equal(150.0, s.MeanFeverBars);
        Assert.Equal(20, s.Skipped(FeverMarkSkip.Behind));
        Assert.Equal(5, s.Skipped(FeverMarkSkip.Capped));
        Assert.Equal("hud: ALT bars - shown 1x, on screen 1.0 s over 2 frames; men marked a frame: mean 150.0, max 200; Athletics bars a frame: mean 0.0, max 0; "
                     + "fever bars a frame: mean 150.0, max 200; most men looked at in a frame 400 (yours and allies 300, enemy 100), most in a fever 400; "
                     + "frames at the cap 1 (FeverMarkMax 200 at the first - the nearest drawn); men skipped, summed over frames: behind the camera 20, beyond the cap 5; cost 30 µs a frame (most 40 µs); errors 2",
            s.SummaryLine(2));
        s.Grown = 1;
        Assert.Contains(", the frame's arrays grew 1x; errors 0", s.SummaryLine(0));
    }
}

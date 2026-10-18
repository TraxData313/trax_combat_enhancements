using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>Step 58 (Anton, 2026-10-18: "this ALT view is too cluttered. Maybe add those lines as some averages on the group, and only show them on
/// up to 5 soldiers that I'm directly looking at and are really close, making it work in tournaments too"): the NEAR view - the men nearest the
/// screen's centre by the angle from the camera's forward, within the range, the cap with FeverMarkMax the ceiling, the mode switch, no fade -; the
/// formation label's fever share ("fever 12/40") with its hide rules (the master switch first); the summaries.</summary>
public class AltNearMarksTests
{
    private static void Near(double expected, double actual, double eps = 1e-5) =>
        Assert.True(Math.Abs(expected - actual) <= eps, "expected " + expected + ", got " + actual);

    // ------------------------------------------------------------------ the angle from the screen's centre

    [Fact]
    public void Facing_is_the_cosine_of_the_angle_from_the_cameras_forward()
    {
        Near(1, AltNearMath.Facing(0, 10, 0, 0, 1, 0));                                     // dead ahead
        Near(1, AltNearMath.Facing(0, 10, 0, 0, 7, 0));                                     // the forward's length does not matter
        Near(0, AltNearMath.Facing(10, 0, 0, 0, 1, 0));                                     // square to the side
        Near(-1, AltNearMath.Facing(0, -3, 0, 0, 1, 0));                                    // behind
        Near(Math.Cos(Math.PI / 4), AltNearMath.Facing(5, 5, 0, 0, 1, 0));                  // 45°
        Near(Math.Cos(Math.PI / 4), AltNearMath.Facing(50, 50, 0, 0, 1, 0));                // ... at any distance - the angle, not the metres
        Near(Math.Cos(Math.PI / 4), AltNearMath.Facing(0, 3, 3, 0, 1, 0));                  // up counts like sideways (the screen's centre)
        Assert.Equal(-1f, AltNearMath.Facing(0, 0, 0, 0, 1, 0));                            // no line - never first
        Assert.Equal(-1f, AltNearMath.Facing(1, 1, 0, 0, 0, 0));                            // no forward
        Assert.Equal(-1f, AltNearMath.Facing(float.NaN, 1, 0, 0, 1, 0));
        Near(0, AltNearMath.AngleDegrees(1f), 1e-6);
        Near(90, AltNearMath.AngleDegrees(0f), 1e-6);
        Near(45, AltNearMath.AngleDegrees(AltNearMath.Facing(5, 5, 0, 0, 1, 0)), 1e-3);
    }

    [Fact]
    public void The_key_grows_with_the_angle_so_the_centre_sorts_first()
    {
        float centre = AltNearMath.CentreKey(1f), ten = AltNearMath.CentreKey((float)Math.Cos(10 * Math.PI / 180)),
              thirty = AltNearMath.CentreKey((float)Math.Cos(30 * Math.PI / 180)), side = AltNearMath.CentreKey(0f);
        Assert.Equal(0f, centre);
        Assert.True(centre < ten && ten < thirty && thirty < side);
        Assert.Equal(2f, AltNearMath.CentreKey(-1f));
        Assert.Equal(2f, AltNearMath.CentreKey(float.NaN));
        Assert.Equal(0f, AltNearMath.CentreKey(1.5f));                                       // clamped
    }

    [Theory]
    [InlineData(14.9f, 15.0, true)]
    [InlineData(15.0f, 15.0, true)]                                                          // at the range: in
    [InlineData(15.1f, 15.0, false)]
    [InlineData(40f, 50.0, true)]
    [InlineData(float.NaN, 15.0, false)]
    public void The_range_is_the_camera_to_the_bars_point(float distance, double metres, bool inRange) =>
        Assert.Equal(inRange, AltNearMath.InRange(distance, metres));

    [Fact]
    public void The_near_view_does_not_fade_the_close_man_the_every_man_view_keeps_vanillas_fade()
    {
        Assert.Equal(1f, AltNearMath.Fade(true, 3f));                                       // a close opponent: bright (vanilla's fade would be 0.23)
        Assert.Equal(1f, AltNearMath.Fade(true, 14f));
        Assert.Equal(0f, AltNearMath.Fade(true, 1f));                                       // in the camera: never drawn anyway (too close)
        Near(FeverMarkMath.Fade(3f), AltNearMath.Fade(false, 3f));
        Near((3f - 1.5f) / 6.5f, AltNearMath.Fade(false, 3f));
    }

    // ------------------------------------------------------------------ the near pick through the frame

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

    /// <summary>A man at camera distance <paramref name="distance"/>, <paramref name="degrees"/> off the screen's centre (a world x for the projector).</summary>
    private static void Man(FeverMarkFrame f, float distance, double degrees, float worldX = 0f)
    {
        ref var m = ref f.Add();
        m.Key = f.Count - 1;
        m.Distance = distance;
        m.InFront = true;
        m.Facing = (float)Math.Cos(degrees * Math.PI / 180);
        m.WorldX = worldX;
    }

    /// <summary>The view's election in the near view: out of range = TooFar, else ranked by the angle.</summary>
    private static void ElectNear(FeverMarkFrame f, in AltNearRules near)
    {
        for (int i = 0; i < f.Count; i++)
        {
            ref var m = ref f.Marks[i];
            if (!AltNearMath.InRange(m.Distance, near.NearMetres)) f.SkipAt(i, FeverMarkSkip.TooFar);
            else f.Elect(i, AltNearMath.CentreKey(m.Facing));
        }
    }

    [Fact]
    public void The_near_view_picks_the_five_nearest_the_screens_centre_within_the_range()
    {
        var near = new AltNearRules(false, 5, 15, 200);
        var f = new FeverMarkFrame();
        Man(f, 4f, 40);      // 0: close, far off the centre
        Man(f, 12f, 2);      // 1: nearly dead centre
        Man(f, 6f, 25);      // 2
        Man(f, 30f, 0);      // 3: dead centre but beyond the range - never a candidate
        Man(f, 9f, 8);       // 4
        Man(f, 14f, 15);     // 5
        Man(f, 3f, 60);      // 6: the nearest man of all, at the screen's edge
        Man(f, 10f, 5);      // 7
        Man(f, 7f, 1, worldX: 99f); // 8: the most central, but OFF the screen (the projector says so) - takes no slot
        ElectNear(f, in near);
        Assert.Equal(1, f.Skipped[(int)FeverMarkSkip.TooFar]);
        Assert.Equal(FeverMarkSkip.TooFar, f.Marks[3].Skip);
        Assert.Equal(8, f.Eligible);
        var p = new Projector { OffScreenBeyondX = 50f };
        Assert.Equal(5, FeverMarkMath.PickOnScreen(f, near.Cap, ref p));
        Assert.Equal(new[] { 1, 7, 4, 5, 2 }, new[] { f.Order[0], f.Order[1], f.Order[2], f.Order[3], f.Order[4] }); // 2°, 5°, 8°, 15°, 25°
        Assert.Equal(6, p.Calls);                                                            // the off-screen 1° man + the five - never the 40° / 60° men
        Assert.Equal(1, f.Skipped[(int)FeverMarkSkip.OffScreen]);
        Assert.Equal(2, f.Skipped[(int)FeverMarkSkip.Capped]);
        Assert.Equal(FeverMarkSkip.Capped, f.Marks[6].Skip);                                 // the nearest man, but at the edge: waits
    }

    [Fact]
    public void Under_the_cap_every_candidate_in_range_is_drawn_and_the_every_man_view_ranks_by_distance()
    {
        var near = new AltNearRules(false, 5, 15, 200);
        var f = new FeverMarkFrame();
        Man(f, 4f, 40);
        Man(f, 12f, 2);
        Man(f, 20f, 3);
        ElectNear(f, in near);
        var p = new Projector { OffScreenBeyondX = 50f };
        Assert.Equal(2, FeverMarkMath.PickOnScreen(f, near.Cap, ref p));                    // a duel: the one or two men there, wherever on the screen
        Assert.Equal(0, f.Skipped[(int)FeverMarkSkip.Capped]);

        // the every-man view (step 46): no range, ranked by the camera distance, the cap FeverMarkMax
        var everyone = new AltNearRules(true, 5, 15, 2);
        f.Clear();
        Man(f, 4f, 40);
        Man(f, 12f, 2);
        Man(f, 20f, 3);
        for (int i = 0; i < f.Count; i++) f.Elect(i, f.Marks[i].Distance);
        Assert.Equal(2, FeverMarkMath.PickOnScreen(f, everyone.Cap, ref p));
        Assert.Equal(new[] { 0, 1 }, new[] { f.Order[0], f.Order[1] });                      // the nearest two, the central far one waits
    }

    // ------------------------------------------------------------------ the cap and the mode switch

    [Theory]
    [InlineData(false, 5, 200, 5)]
    [InlineData(false, 5, 3, 3)]       // FeverMarkMax stays the ceiling
    [InlineData(false, 50, 200, 50)]
    [InlineData(true, 5, 200, 200)]    // the every-man view: FeverMarkMax
    [InlineData(false, -2, 200, 0)]    // nonsense = none
    public void The_cap_is_the_near_max_under_the_hard_ceiling(bool everyone, int nearMax, int hardMax, int cap)
    {
        var r = new AltNearRules(everyone, nearMax, 15, hardMax);
        Assert.Equal(cap, r.Cap);
        Assert.Equal(!everyone, r.Near);
        Assert.Equal(everyone ? "everyone" : "near", r.ModeName);
    }

    [Fact]
    public void The_settings_are_live_grouped_in_order_and_default_to_the_near_view()
    {
        var t = new TraxSettings();
        var r = AltNearRules.From(t);
        Assert.True(r.Near);                                                                 // the near view by default (decision 50)
        Assert.Equal(5, r.NearMax);
        Near(15, r.NearMetres);
        Assert.Equal(200, r.HardMax);
        Assert.Equal(5, r.Cap);
        Assert.True(t.ShowFeverOnLabels);
        t.Set(SettingsSchema.AltNearMarksMax, 8, SettingSources.Mcm);
        t.Set(SettingsSchema.AltNearMarksMetres, 25.0, SettingSources.Mcm);
        r = AltNearRules.From(t);
        Assert.Equal(8, r.Cap);
        Near(25, r.NearMetres);
        t.Set(SettingsSchema.AltMarksEveryone, true, SettingSources.Mcm);                   // the old view back, live
        r = AltNearRules.From(t);
        Assert.True(r.Everyone);
        Assert.Equal(200, r.Cap);
        foreach (var p in new[] { SettingsSchema.ShowFeverOnLabels, SettingsSchema.AltNearMarksMax, SettingsSchema.AltNearMarksMetres })
        {
            Assert.Same(SettingsSchema.AltMarkersGroup, p.Group);
            Assert.Equal(ApplyTiming.Live, p.Timing);
        }
        Assert.Same(SettingsSchema.PlayerScreenGroup, SettingsSchema.AltMarksEveryone.Group);                        // the player page since step 59
        Assert.Equal(ApplyTiming.Live, SettingsSchema.AltMarksEveryone.Timing);
        Assert.Equal(SettingsSchema.AltMarkersShowEnemy.Index + 1, SettingsSchema.ShowFeverOnLabels.Index);        // with the labels
        Assert.Equal(SettingsSchema.AthleticsMarksShowEnemy.Index + 1, SettingsSchema.AltNearMarksMax.Index);      // after the bars' switches
        Assert.Equal(SettingsSchema.AltNearMarksMax.Index + 1, SettingsSchema.AltNearMarksMetres.Index);
        Assert.Equal(SettingsSchema.AltNearMarksMetres.Index + 1, SettingsSchema.FeverMarkMax.Index);              // the ceiling last
    }

    [Fact]
    public void The_sentence_names_the_mode()
    {
        Assert.Equal("mode near: up to 5 men nearest the screen's centre (AltNearMarksMax 5, FeverMarkMax 200 the ceiling) within 15.0 m of the camera (AltNearMarksMetres), no distance fade",
            new AltNearRules(false, 5, 15, 200).Describe());
        Assert.Equal("mode everyone (AltMarksEveryone on): every man on the screen, the nearest 200 to the camera (FeverMarkMax)",
            new AltNearRules(true, 5, 15, 200).Describe());
        Assert.Equal("beyond the near range", FeverMarkMath.SkipName(FeverMarkSkip.TooFar));
        var skipped = new int[(int)FeverMarkSkip.Count];
        skipped[(int)FeverMarkSkip.TooClose] = 1;
        skipped[(int)FeverMarkSkip.TooFar] = 7;
        skipped[(int)FeverMarkSkip.Capped] = 2;
        Assert.Equal("closer than 1.5 m 1, beyond the near range 7, beyond the cap 2", FeverMarkMath.DescribeSkips(skipped)); // in the order asked
    }

    // ------------------------------------------------------------------ the formation label's fever share

    private static FormationAthleticsStats Stats(int men, int inFever) =>
        new(men, 72, 8, 0.72, 0.08, 0, 0.9, 0, 0.81, 0, inFever);

    [Fact]
    public void The_label_reads_fever_in_the_ready_counts_style()
    {
        Assert.Equal("fever 12/40", FeverLabelMath.Text(12, 40));
        Assert.Equal("fever 0/9", FeverLabelMath.Text(0, 9));
        Assert.Equal(FeverMarkMath.OrangeHex, FeverLabelMath.ColorHex);                     // the fever bar's own orange
        Assert.Equal(12, Stats(40, 12).InFever);
        Assert.Equal(40, Stats(40, 99).InFever);                                             // never more than the men
        Assert.Equal(0, Stats(40, -3).InFever);
        var a = new MeanStd();
        a.Add(0.5);
        var st = FormationAthleticsStats.From(in a, in a, in a, in a, 0, 0, 0, 1);
        Assert.Equal(1, st.InFever);
        Assert.Equal(0, FormationAthleticsStats.From(in a, in a, in a, in a, 0, 0).InFever); // the older callers: none
    }

    [Fact]
    public void The_fever_line_hides_in_order_the_master_switch_first()
    {
        var on = new FeverLabelRules(true, true, true, true);
        Assert.Equal(FeverLabelHidden.None, FeverLabelMath.HiddenFor(in on, Stats(40, 12)));
        Assert.Equal(FeverLabelHidden.NoneInFever, FeverLabelMath.HiddenFor(in on, Stats(40, 0)));     // nobody surging: no line (less noise)
        Assert.Equal(FeverLabelHidden.NoMen, FeverLabelMath.HiddenFor(in on, Stats(0, 0)));
        Assert.Equal(FeverLabelHidden.SwitchedOff, FeverLabelMath.HiddenFor(new FeverLabelRules(false, true, true, true), Stats(40, 12)));
        Assert.Equal(FeverLabelHidden.FeverOff, FeverLabelMath.HiddenFor(new FeverLabelRules(false, true, true, false), Stats(40, 12)));
        Assert.Equal(FeverLabelHidden.AthleticsOff, FeverLabelMath.HiddenFor(new FeverLabelRules(false, true, false, false), Stats(40, 12)));
        Assert.Equal(FeverLabelHidden.ModOff, FeverLabelMath.HiddenFor(new FeverLabelRules(false, false, false, false), Stats(40, 12)));
        var t = new TraxSettings();
        Assert.Equal(FeverLabelHidden.None, FeverLabelRules.From(t).Hidden);
        t.Set(SettingsSchema.ShowFeverOnLabels, false, SettingSources.Mcm);
        Assert.Equal(FeverLabelHidden.SwitchedOff, FeverLabelRules.From(t).Hidden);
        t.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
        Assert.Equal(FeverLabelHidden.ModOff, FeverLabelRules.From(t).Hidden);
        Assert.Equal("fever share on (\"fever 12/40\", shown while a man of the formation is in a fever)", on.Describe());
        Assert.Equal("fever share hidden (FeverEnabled off)", new FeverLabelRules(true, true, true, false).Describe());
    }

    // ------------------------------------------------------------------ the summaries

    [Fact]
    public void The_label_summary_names_the_most_feverish_formation()
    {
        var s = new FeverLabelStats();
        Assert.Equal("hud: ALT markers - fever share (step 58): never shown (no values pushed)", s.SummaryLine("ALT markers"));
        s.NoteShown(3, 40, 0, (int)MarkerTeam.Yours);
        s.NoteShown(30, 40, 0, (int)MarkerTeam.Enemy);
        s.NoteShown(5, 20, 1, (int)MarkerTeam.Ally);
        s.NoteHidden(FeverLabelHidden.NoneInFever);
        s.NoteHidden(FeverLabelHidden.NoneInFever);
        s.NoteHidden(FeverLabelHidden.FeverOff);
        Assert.Equal(3, s.Shown);
        Assert.Equal("hud: ALT markers - fever share (step 58): shown 3x, the most in a fever 30/40 (75%, enemy 1 Infantry); hidden 3x (FeverEnabled off 1, nobody in a fever 2)",
            s.SummaryLine("ALT markers"));
    }

    [Fact]
    public void The_bars_summary_says_the_mode_and_the_near_marks_a_frame()
    {
        var s = new FeverMarkStats();
        s.NoteShown();
        var skipped = new int[(int)FeverMarkSkip.Count];
        skipped[(int)FeverMarkSkip.TooFar] = 40;
        s.NoteMode(true, 5, 5, 15);
        s.NoteFrame(0.02, 5, 50, 0, 20, 30, true, 5, skipped, 0.01);
        s.NoteMode(true, 3, 7, 30);                                                          // a later cap / range: the first is named
        s.NoteFrame(0.02, 3, 50, 0, 20, 30, false, 5, skipped, 0.01);
        s.NoteMode(false, 100, 200, 15);
        s.NoteFrame(0.02, 100, 50, 0, 20, 30, false, 200, skipped, 0.01);
        Assert.Equal(2, s.NearFrames);
        Assert.Equal(1, s.EveryoneFrames);
        Near(4, s.MeanNear);
        Assert.Equal(5, s.MostNear);
        string line = s.SummaryLine(0);
        Assert.Contains("beyond the near range 120", line);                                  // 40 a frame, summed over three
        Assert.Contains("; mode (step 58): near 2 frames (men marked a frame: mean 4.0, max 5 - up to 5 nearest the screen's centre within 15.0 m of the camera at the first), everyone 1 frames; errors 0", line);
    }
}

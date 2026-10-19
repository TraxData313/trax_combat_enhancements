using System;
using System.Collections.Generic;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 70 - THE FLOCK IS A WIDE BAND + NO LONE RUNNER BEHIND THE LINE (Toni's 30 v 50 looters 2026-10-19 15:59: the frightened looters stood
/// as a column ~15 men deep facing his line - the log's depth ÷ width in contact 1.44, 3.5 m deep, 2.6 m wide, "stepped in behind a comrade
/// 1394 looks", the green pull 0.61; his sketch: "a wide, shallow mass along the whole line, 4-6 men deep"; and a few looters ran round BEHIND
/// his line alone; AI_NOTES "Step 70", DESIGN decisions 63 / 64): the band's geometry, the depth cap, a 40-man looter unit's ranks / width with
/// the band against step 67's point shelter, the lone runner's rules and its three fixes.
/// </summary>
public class Step70Tests
{
    private static FearRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return FearRules.From(s);
    }

    private static LoneRunnerRules Lone(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return LoneRunnerRules.From(s);
    }

    private static void Near(double expected, double actual, double tol = 1e-3) => Assert.True(Math.Abs(expected - actual) <= tol, "expected " + expected + ", got " + actual);

    private static readonly ParamDef[] New =
    {
        SettingsSchema.FearBandEnabled, SettingsSchema.FearBandRanksMin, SettingsSchema.FearBandRanksMax, SettingsSchema.FearBandFileMetres,
        SettingsSchema.FearBandRankMetres, SettingsSchema.FearBandMaxWidthMetres, SettingsSchema.FearBandGapMetres, SettingsSchema.FearStrikeReachMetres,
        SettingsSchema.FearStrikeLeashMetres, SettingsSchema.LoneRunnerEnabled, SettingsSchema.LoneRunnerAheadMetres,
    };

    [Fact]
    public void The_eleven_settings_follow_the_mop_up_on_the_Advanced_page_live()
    {
        var all = SettingsSchema.All.ToList();
        int last = all.IndexOf(SettingsSchema.FearMopUpWinPercent);
        for (int i = 0; i < New.Length; i++)
        {
            Assert.Equal(last + 1 + i, all.IndexOf(New[i]));
            Assert.Same(SettingsSchema.CourageGroup, New[i].Group);
            Assert.Equal(SettingsPage.Advanced, New[i].Group.Page);
            Assert.Equal(ApplyTiming.Live, New[i].Timing);
            Assert.DoesNotContain(New[i], SettingsSchema.PlayerPage);
        }
        var r = Rules();
        Assert.True(r.BandEnabled);
        Assert.Equal(4, r.BandRanksMin);
        Assert.Equal(6, r.BandRanksMax);
        Near(1.0, r.BandFileMetres);
        Near(1.0, r.BandRankMetres);
        Near(40, r.BandMaxWidthMetres);
        Near(1.5, r.BandGapMetres);
        Near(2.5, r.StrikeReachMetres);
        Near(2.0, r.StrikeLeashMetres);
        var l = Lone();
        Assert.True(l.Enabled);
        Near(4, l.AheadMetres);
        var s = new TraxSettings();
        s.Set(SettingsSchema.FearBandRanksMax, 8, SettingSources.Mcm);
        s.Set(SettingsSchema.LoneRunnerAheadMetres, 6.0, SettingSources.Mcm);
        Assert.Equal(8, FearRules.From(s).BandRanksMax); // live
        Near(6, LoneRunnerRules.From(s).AheadMetres);
    }

    [Fact]
    public void The_master_switch_comes_first_and_the_sentences_say_step_70()
    {
        var off = Lone(s => s.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm));
        Assert.False(off.Enabled);
        Assert.Equal("ModEnabled", off.OffBecause);
        Assert.Equal("LoneRunnerEnabled", Lone(s => s.Set(SettingsSchema.LoneRunnerEnabled, false, SettingSources.Mcm)).OffBecause);
        Assert.False(LoneRunnerMath.ForcesWalk(in off, lone: true, winning: false));
        Assert.True(LoneRunnerMath.FeverPresses(in off, feverPresses: true, alone: true)); // off: the fever presses as in step 49b
        Assert.False(LoneRunnerMath.SprintEnds(in off, alone: true, pastEnemy: 5));
        Assert.False(Rules(s => s.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm)).Enabled);
        string on = Rules().Describe();
        Assert.Contains("; a WIDE BAND (step 70) along the enemy's front, not a point", on);
        Assert.Contains("4 ranks deep at a small pull to 6 at the full pull", on);
        Assert.Contains("; a front man with an enemy within 2.5 m is let go to strike", on);
        Assert.Contains("the point shelter of step 67 (FearBandEnabled off)", Rules(s => s.Set(SettingsSchema.FearBandEnabled, false, SettingSources.Mcm)).Describe());
        Assert.Contains("a LONE RUNNER (step 70) = an AI man on foot more than 4 m ahead of his own unit's front", Lone().Describe());
        Assert.Contains("unless he chases a beaten enemy", Lone().Describe());
        Assert.Contains("measured only (LoneRunnerEnabled off)", Lone(s => s.Set(SettingsSchema.LoneRunnerEnabled, false, SettingSources.Mcm)).Describe());
    }

    // ------------------------------------------------------------------ the band's geometry

    [Fact]
    public void The_band_is_four_ranks_at_a_small_pull_six_at_the_full_and_as_wide_as_the_front_it_faces()
    {
        var r = Rules();
        Assert.Equal(4, FearBandMath.Ranks(in r, 0));
        Assert.Equal(5, FearBandMath.Ranks(in r, 0.5));
        Assert.Equal(5, FearBandMath.Ranks(in r, 0.61)); // Toni's green looters' pull
        Assert.Equal(6, FearBandMath.Ranks(in r, 1));
        // the width: the enemy's frontage, or his men in ranks - whichever is wider - capped
        Near(20, FearBandMath.Width(in r, 40, 5, 20));      // 40 looters in 5 ranks = 8 m < his 20 m line
        Near(10, FearBandMath.Width(in r, 50, 5, double.NaN)); // no enemy formation known: the men in ranks
        Near(16, FearBandMath.Width(in r, 80, 5, 12));      // more men than the front: wider
        Near(40, FearBandMath.Width(in r, 400, 4, 30));     // capped
        Near(1, FearBandMath.Width(in r, 1, 6, double.NaN)); // at least one file
        // along the enemy's front: inside it when narrower, covering it when wider
        Near(0, FearBandMath.LateralCentre(0, 0, 20, 8));
        Near(6, FearBandMath.LateralCentre(9, 0, 20, 8));   // slid in so the band lies along his line
        Near(5, FearBandMath.LateralCentre(9, 0, 20, 30));  // wider than his line: it still covers it (−10..20 over his −10..10)
        Near(9, FearBandMath.LateralCentre(9, double.NaN, double.NaN, 8));
        Assert.Equal(20, FearBandMath.Files(20, 1));
        Assert.Equal(FearBandMath.MaxFiles, FearBandMath.Files(150, 1));
    }

    [Fact]
    public void The_front_stands_off_the_enemy_pushed_back_by_the_pull_and_creeps_back_at_the_drift_cap()
    {
        var r = Rules();
        Near(10 - 1.5 - 4 * 0.61, FearBandMath.FrontWanted(in r, 10, 0, 0.61));
        Near(3 - 4, FearBandMath.FrontWanted(in r, double.NaN, 3, 1)); // no enemy known: the men's own front
        Near(8, FearBandMath.FrontStep(5, 8, 0.05));   // forward: free
        Near(4.95, FearBandMath.FrontStep(5, 2, 0.05)); // back: at most the cap
        Near(2, FearBandMath.FrontStep(double.NaN, 2, 0.05));
        // a hold's cap: 6 m a minute x 25% = 0.0125 m in 0.5 s; a charge 0.05 m
        Near(0.0125, FearMath.DriftCap(in r, FearOrder.Hold, 0.5));
        Near(0.05, FearMath.DriftCap(in r, FearOrder.Charge, 0.5));
    }

    [Fact]
    public void A_full_file_sends_the_men_behind_its_ranks_out_toward_the_ends()
    {
        Assert.False(FearBandMath.Slides(5, 5, 5)); // a full file - its last allowed rank stays
        Assert.False(FearBandMath.Slides(6, 5, 5));
        Assert.True(FearBandMath.Slides(6, 5, 6));  // the sixth man of a 5-rank band slides
        Assert.False(FearBandMath.Slides(4, 5, 9)); // a file with room: nobody slides
        var counts = new int[] { 5, 5, 9, 5, 2, 0 };
        Assert.Equal(4, FearBandMath.FreeFile(counts, 0, 6, 2, 5));   // the nearest with room
        counts = new int[] { 2, 5, 9, 5, 2, 9 };
        Assert.Equal(0, FearBandMath.FreeFile(counts, 0, 6, 2, 5));   // a tie (0 and 4 both 2 away): toward the end farther from the middle
        Assert.Equal(-1, FearBandMath.FreeFile(new[] { 5, 9, 5 }, 0, 3, 1, 5)); // no room anywhere
        Near(4, FearBandMath.RankOf(10, 7, 1));
        Near(1, FearBandMath.RankOf(10, 11, 1));
    }

    // ------------------------------------------------------------------ the unit table's band

    private const int Key = 1 * FearMath.Formations + 1;

    private sealed class Man
    {
        public double X, Y, SlotX, SlotY;
        public readonly FearMind Mind = new FearMind();
    }

    /// <summary>Toni's looters: 40 men in a column 3 wide and ~14 deep (1 m apart), its front at y = 0, facing north; his line of 20 m at y = 10.</summary>
    private static List<Man> Column()
    {
        var men = new List<Man>();
        for (int i = 0; i < 40; i++)
        {
            double x = (i % 3) - 1, y = -(i / 3);
            men.Add(new Man { X = x, Y = y, SlotX = x, SlotY = y });
        }
        return men;
    }

    /// <summary>One period of the unit table: every man noted (in contact - the enemy he sees at his line's centre), his line read, published.</summary>
    private static void Period(FearUnits units, in FearRules r, List<Man> men, double t, double pull, FearOrder order, FearStats? stats = null)
    {
        for (int i = 0; i < men.Count; i++)
        {
            var m = men[i];
            units.Note(Key, 0.6, pull, 30, 0.5, true, 0, 10, 0, -6.5, 0, 1, m.X, m.Y, order, double.NaN, i,
                m.Mind.HasSlide ? m.Mind.SlideX : double.NaN, m.Mind.HasSlide ? m.Mind.SlideY : double.NaN); // a slid man counts in his file (the module does the same)
        }
        units.NoteEnemyFront(Key, t, 0, 10.5, 0, -1, 20, 1); // his line faces south, 20 m wide, 1 m deep
        units.Publish(t, in r, stats, true);
    }

    /// <summary>The men walk toward their places at 1.5 m/s for <paramref name="seconds"/> - the band (slot or own position) or step 67's point.</summary>
    private static void Walk(FearUnits units, in FearRules r, List<Man> men, double pull, FearOrder order, double seconds, ref double t, FearStats? stats = null)
    {
        for (double s = 0; s < seconds; s += 0.5)
        {
            t += 0.5;
            Period(units, in r, men, t, pull, order, stats);
            for (int i = 0; i < men.Count; i++)
            {
                var m = men[i];
                double tx, ty;
                bool charge = order == FearOrder.Charge;
                double bx = charge ? m.X : m.SlotX, by = charge ? m.Y : m.SlotY;
                if (r.BandEnabled)
                {
                    var kind = units.BandPlace(in r, Key, t, bx, by, m.X, m.Y, m.Mind, out double qx, out double qy, out double lateral, out _, i);
                    Assert.NotEqual(FearBandPlaceKind.NoBand, kind);
                    if (charge)
                    {
                        tx = qx;
                        ty = qy;
                    }
                    else
                    {
                        FearMath.Place(in r, bx, by, qx, qy, pull, out tx, out ty);
                        if (kind == FearBandPlaceKind.Slid) tx = -lateral; // the band faces north: its lateral axis is −x; a slid man goes the whole way along the front
                    }
                }
                else FearMath.Place(in r, bx, by, units.ShelterX[Key], units.ShelterY[Key], pull, out tx, out ty);
                double dx = tx - m.X, dy = ty - m.Y, d = Math.Sqrt(dx * dx + dy * dy), step = 0.75;
                if (d <= step)
                {
                    m.X = tx;
                    m.Y = ty;
                }
                else
                {
                    m.X += dx / d * step;
                    m.Y += dy / d * step;
                }
            }
        }
    }

    /// <summary>The shape as the summary measures it: men per occupied 1 m file across the front (x), the deepest file, the width.</summary>
    private static (double perFile, int deepest, double width) Shape(List<Man> men)
    {
        double min = men.Min(m => m.X), max = men.Max(m => m.X);
        int files = Math.Max(1, (int)Math.Ceiling(max - min + 1e-9));
        var c = new int[files];
        foreach (var m in men) c[Math.Min(files - 1, (int)Math.Floor(m.X - min))]++;
        int occupied = c.Count(n => n > 0);
        return (men.Count / (double)occupied, c.Max(), occupied);
    }

    [Fact]
    public void The_band_faces_his_line_its_front_short_of_it_its_width_his_frontage()
    {
        var r = Rules();
        var units = new FearUnits();
        var men = Column();
        Period(units, in r, men, 1000, 0.61, FearOrder.Charge);
        Assert.True(units.BandLive(Key, 1000));
        Near(0, units.BandUX[Key]);
        Near(1, units.BandUY[Key]); // toward his line (north), along its front
        Assert.Equal(5, units.BandRanks[Key]);
        Near(5, units.BandDepth[Key]);
        Near(20, units.BandWidth[Key]); // his 20 m line, wider than 40 men in 5 ranks
        Near(0, units.BandCentre[Key]);
        Assert.Equal(20, units.BandFiles[Key]);
        Near(10 - 1.5 - 4 * 0.61, units.BandFront[Key], 0.01); // forward is free: the front at once where it wants to be
        Assert.True(units.OwnFront[Key] > -1 && units.OwnFront[Key] < 1.5, "the column's own front (its mean + √3 σ): " + units.OwnFront[Key]);
        Near(10, units.EnemyFrontAlong[Key]); // the enemy his men see
        // the files: 3 files of 13-14 men
        Assert.Equal(13, units.FileCount[Key * FearBandMath.MaxFiles + 10]); // the middle file (x 0)
        Assert.Equal(14, units.FileCount[Key * FearBandMath.MaxFiles + 11]); // x −1 (the lateral axis is −x)
        Assert.Equal(13, units.FileCount[Key * FearBandMath.MaxFiles + 9]);
        Assert.True(units.Fronts(Key, 1000, 0, 5, out double ahead, out double past));
        Assert.True(ahead > 3.5 && past < -4, ahead + " / " + past);
        Assert.True(units.PastEnemyFront(Key, 1000, 0, 13) > 2);
    }

    [Fact]
    public void A_man_keeps_his_place_along_the_front_and_is_compressed_only_in_depth()
    {
        var r = Rules();
        var units = new FearUnits();
        var men = Column();
        Period(units, in r, men, 1000, 0.61, FearOrder.Hold);
        double front = units.BandFront[Key], rear = front - units.BandDepth[Key];
        // the front man of the middle file (rank 1): his x kept, his depth up into the band
        var kind = units.BandPlace(in r, Key, 1000, 0, 0, 0, 0, men[1].Mind, out double tx, out double ty, out _, out bool full, 1);
        Assert.Equal(FearBandPlaceKind.Compressed, kind);
        Near(0, tx);
        Assert.True(ty >= rear - 1e-6 && ty <= front + 1e-6, "his depth is in the band: " + ty);
        Assert.True(full, "his file holds 14 men - more than the 5 ranks");
        // a man far out on the flank of the band: his lateral clamped to the band's edge, nothing else
        var lone = new FearMind();
        kind = units.BandPlace(in r, Key, 1000, -30, 3, -30, 3, lone, out tx, out ty, out _, out _);
        Near(-9.5, tx); // the band's last file
        Near(3, ty);
        // the 9th man of the middle file (rank 9 > 5): out of the full file toward the ends - a file with room
        kind = units.BandPlace(in r, Key, 1000, 0, -8, 0, -8, men[25].Mind, out tx, out ty, out _, out _, 25);
        Assert.Equal(FearBandPlaceKind.Slid, kind);
        Assert.True(Math.Abs(tx) >= 1.5, "he slid out of the column's three files: x " + tx);
        Assert.True(men[25].Mind.HasSlide);
        // and he keeps that file for the spell
        kind = units.BandPlace(in r, Key, 1000, 0, -8, tx, ty, men[25].Mind, out double tx2, out _, out _, out _, 25);
        Assert.Equal(FearBandPlaceKind.Slid, kind);
        Near(tx, tx2);
        men[25].Mind.End(FearEnd.Calm);
        Assert.False(men[25].Mind.HasSlide);
    }

    [Fact]
    public void THE_40_MAN_LOOTER_UNIT_a_wide_shallow_band_against_step_67s_deep_point()
    {
        // Toni's battle in miniature: 40 green looters (pull 0.61 - the log's green mean) in a column 3 wide and 14 deep facing his 20 m line
        var stats = new FearStats();
        // step 67: THE POINT - every place slides toward the shelter (the unit's centre, creeping back), along the front as much as in depth
        var point = Rules(s => s.Set(SettingsSchema.FearBandEnabled, false, SettingSources.Mcm));
        var units = new FearUnits();
        var men = Column();
        double t = 1000;
        Walk(units, in point, men, 0.61, FearOrder.Hold, 30, ref t);
        var (pointFile, pointDeepest, pointWidth) = Shape(men);
        // step 70: THE BAND - the same men, the same pull
        var band = Rules();
        units = new FearUnits();
        men = Column();
        t = 1000;
        Walk(units, in band, men, 0.61, FearOrder.Hold, 30, ref t, stats);
        var (bandFile, bandDeepest, bandWidth) = Shape(men);
        Assert.True(pointDeepest >= 13 && pointWidth <= 2, "step 67's point: " + pointDeepest + " deep, " + pointWidth + " m wide (" + pointFile + " a file)");
        Assert.True(bandDeepest <= 6, "the band: its deepest file " + bandDeepest + " men (" + bandFile + " a file)");
        Assert.True(bandWidth >= 8, "the band: " + bandWidth + " m wide");
        Assert.True(bandFile <= 5.5, "the band: " + bandFile + " men a file");
        // the summary measured it (ON: the band's files over the cap fall away)
        Assert.True(stats.Band.Samples(true) > 0);
        Assert.True(stats.Band.MeanRanks(true) < 9, "men per file over the run (from 13.3 at the start): " + stats.Band.MeanRanks(true));
        // a CHARGE's men (no slot - their own places) end the same way
        units = new FearUnits();
        men = Column();
        t = 1000;
        Walk(units, in band, men, 0.61, FearOrder.Charge, 30, ref t);
        var (chargeFile, chargeDeepest, chargeWidth) = Shape(men);
        Assert.True(chargeDeepest <= 6 && chargeWidth >= 8, "the charging band: " + chargeDeepest + " deep, " + chargeWidth + " m wide (" + chargeFile + " a file)");
    }

    [Fact]
    public void The_band_line_reports_ranks_width_and_the_files_over_the_cap()
    {
        var b = new FearBandStats();
        b.AddShape(true, 4, 5, 10, 0, 10);
        b.AddShape(true, 6, 8, 6, 2, 6);
        b.AddShape(false, 13, 14, 3, 3, 3);
        b.AddKind(FearBandPlaceKind.Slid);
        b.StepInsRefused = 7;
        b.StrikeLooks = 3;
        Near(5, b.MeanRanks(true));
        Near(8, b.MeanWidth(true));
        Near(2 / 16.0, b.OverCapShare(true));
        Near(6.25, b.P90Ranks(true));
        var lines = new List<string>();
        b.AddSummaryLines(lines, Rules());
        Assert.Single(lines);
        Assert.StartsWith("fear flock band (step 70 - a wide band along the enemy's front, 4-6 ranks)", lines[0]);
        Assert.Contains("ON 5.0 men per file (p90 6.25), the deepest file 6.5, width 8.0 m", lines[0]);
        Assert.Contains("| OFF 13.0 men per file", lines[0]);
        Assert.Contains("slid out of a full file 1", lines[0]);
        Assert.Contains("refused (his file full - he slides instead) 7", lines[0]);
        Assert.Contains("front men let go to STRIKE 3 looks", lines[0]);
    }

    // ------------------------------------------------------------------ the lone runner

    [Fact]
    public void A_lone_runner_is_alone_with_an_enemy_near_and_out_beyond_his_line()
    {
        var r = Lone();
        Assert.True(LoneRunnerMath.Alone(1, 3));
        Assert.False(LoneRunnerMath.Alone(2, 3));          // two mates: not alone
        Assert.False(LoneRunnerMath.Alone(0, double.NaN));  // nobody near at all: not "alone among enemies"
        Assert.False(LoneRunnerMath.Alone(0, 9));          // the enemy beyond 8 m
        Assert.True(LoneRunnerMath.IsLone(in r, true, 5, double.NaN));   // 5 m ahead of his unit's front
        Assert.False(LoneRunnerMath.IsLone(in r, true, 3, -2));          // 3 m ahead, short of the enemy's front
        Assert.True(LoneRunnerMath.IsLone(in r, true, 1, 2));            // past the enemy's front by 2 m
        Assert.False(LoneRunnerMath.IsLone(in r, false, 20, 10));        // mates near: a charge's mass, never a lone runner
        Assert.True(LoneRunnerMath.Winning(2.0, 200, 0.8));   // chasing the beaten (his side 2 : 1, the enemy near him broken): left alone
        Assert.False(LoneRunnerMath.Winning(1.9, 200, 1));
        Assert.False(LoneRunnerMath.Winning(4, 200, 0.2));    // the odds alone (one legionary at the edge of the radius weighs little) - not beaten
        Assert.False(LoneRunnerMath.Winning(4, 200, double.NaN));
        Assert.False(LoneRunnerMath.Winning(double.NaN, 200, 1));
    }

    [Fact]
    public void The_cause_is_the_first_that_holds()
    {
        Assert.Equal(LoneCause.SprintIntoContact, LoneRunnerMath.Cause(true, true, true, true, true));
        Assert.Equal(LoneCause.PunishStepIn, LoneRunnerMath.Cause(false, true, true, true, true));
        Assert.Equal(LoneCause.CourageSurge, LoneRunnerMath.Cause(false, false, true, true, true));
        Assert.Equal(LoneCause.FeverPress, LoneRunnerMath.Cause(false, false, false, true, true));
        Assert.Equal(LoneCause.ChasingTarget, LoneRunnerMath.Cause(false, false, false, false, true));
        Assert.Equal(LoneCause.Game, LoneRunnerMath.Cause(false, false, false, false, false));
    }

    [Fact]
    public void The_three_fixes_fever_alone_the_sprint_and_the_walk_back()
    {
        var r = Lone();
        // FIX 1: the fever presses against bad odds - not for a man alone
        Assert.True(LoneRunnerMath.FeverPresses(in r, true, alone: false));
        Assert.False(LoneRunnerMath.FeverPresses(in r, true, alone: true));
        Assert.False(LoneRunnerMath.FeverPresses(in r, false, alone: false));
        // FIX 2: a sprint into contact ends alone or past the enemy's front
        Assert.True(LoneRunnerMath.SprintEnds(in r, alone: true, pastEnemy: double.NaN));
        Assert.True(LoneRunnerMath.SprintEnds(in r, alone: false, pastEnemy: 1.5));
        Assert.False(LoneRunnerMath.SprintEnds(in r, alone: false, pastEnemy: 0.5)); // at the enemy's front: the charge's punch goes in
        // FIX 3: a lone runner walks back - unless his side wins clearly around him
        Assert.True(LoneRunnerMath.ForcesWalk(in r, lone: true, winning: false));
        Assert.False(LoneRunnerMath.ForcesWalk(in r, lone: true, winning: true));
        Assert.False(LoneRunnerMath.ForcesWalk(in r, lone: false, winning: false));
    }

    [Fact]
    public void Courage_walks_a_lone_runner_back_whatever_the_odds()
    {
        var cr = CourageRules.From(new TraxSettings());
        // a brave man (odds 3 : 1 against the required 1) 2 m from his formation's centre with 1 mate: courage alone leaves him
        var calm = new CourageMind();
        Assert.Equal(CourageStep.None, calm.Look(in cr, 10, 3, 1, 2, 1));
        var lone = new CourageMind();
        Assert.Equal(CourageStep.TimidMoving, lone.Look(in cr, 10, 3, 1, 2, 1, lone: true));
        Assert.Equal(CouragePhase.Moving, lone.Phase);
        // among his mates again (4+): the walk ends - he holds; still lone at the next look = he walks again; never brave while lone
        Assert.Equal(CourageStep.StopMoving, lone.Look(in cr, 11, 3, 1, 8, 4, lone: true));
        Assert.Equal(CourageStep.StartMoving, lone.Look(in cr, 14, 3, 1, 8, 1, lone: true));
        Assert.Equal(CourageStep.StopMoving, lone.Look(in cr, 15, 3, 1, 3, 5));
        Assert.Equal(CourageStep.Brave, lone.Look(in cr, 18, 3, 1, 3, 5)); // not lone any more: brave again at the odds
    }

    [Fact]
    public void The_lone_runner_line_counts_by_cause_and_by_target()
    {
        var s = new LoneRunnerStats();
        var a = new LoneMind();
        var b = new LoneMind();
        s.Add(a, true, LoneCause.SprintIntoContact, 1.5, LoneTarget.None);
        s.Add(a, true, LoneCause.ChasingTarget, 0.5, LoneTarget.Player);
        s.Add(b, false, LoneCause.FeverPress, 2, LoneTarget.Hero);
        s.Add(b, false, LoneCause.FeverPress, 0.5, LoneTarget.Hero);
        s.WalksForced = 2;
        s.SprintsEnded = 1;
        Near(2, s.Seconds(true));
        Near(2.5, s.Seconds(false));
        Assert.Equal(1, s.Men(true));
        Assert.Equal(1, s.MenOf(LoneCause.FeverPress));
        Near(2.5, s.TargetSeconds(LoneTarget.Hero));
        Assert.Equal(1, s.TargetMen(LoneTarget.Player));
        var lines = new List<string>();
        s.AddSummaryLines(lines, Lone());
        Assert.Single(lines);
        Assert.StartsWith("lone runners (step 70 - the fixes ON; more than 4 m ahead of his unit's front or 1 m past the enemy's front", lines[0]);
        Assert.Contains("fixes ON 2.0 man-s (1 men), OFF 2.5 man-s (1 men)", lines[0]);
        Assert.Contains("the sprint into contact (step 64) 1.5 man-s (1 men)", lines[0]);
        Assert.Contains("the fever press (step 49b) 2.5 man-s (1 men)", lines[0]);
        Assert.Contains("you (the player) 0.5 man-s (1 men), a hero 2.5 man-s (1 men)", lines[0]);
        Assert.Contains("walks back forced 2", lines[0]);
        Assert.Contains("sprints into contact ended 1", lines[0]);
    }

    [Fact]
    public void A_sprint_into_contact_ending_for_a_lone_runner_has_a_name()
    {
        Assert.Equal("alone or past the enemy's front (step 70)", SprintMath.EndName(SprintEnd.LoneRunner));
        Assert.True((int)SprintEnd.LoneRunner > (int)SprintEnd.Stopped); // appended - old indexes kept
    }
}

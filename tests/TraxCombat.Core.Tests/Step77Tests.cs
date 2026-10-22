using System;
using System.Collections.Generic;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 77 (Toni's battles 2026-10-22 10:2x-10:48: 108 T1 recruits against his 59 T5 shield wall - their rear ranks walked round his wall's ends and
/// behind it, "dancing", then through the middle; AI_NOTES "Step 77", DESIGN decision 71) - PRESS, DON'T PASS: the settings (one on the player page,
/// fifteen Advanced), the master switch first, the line test, the FRONT SEGMENT (its front, ends, gaps, its rear face), the side-of-line verdicts and
/// the input clamp, the gaps, the wrap rule, the walk round an end, the table's enemy line / face / width, the fear band clamped to the line (its surplus
/// deeper), the block hold's range at the enemy's ends, the summary lines.
/// </summary>
public class Step77Tests
{
    private static readonly ParamDef[] Advanced =
    {
        SettingsSchema.PressToleranceMetres, SettingsSchema.PressWatchMetres, SettingsSchema.PressGapMetres, SettingsSchema.PressManWidthMetres,
        SettingsSchema.PressFrontDepthMetres, SettingsSchema.PressWrapMarginMetres, SettingsSchema.PressWrapMates, SettingsSchema.PressWrapMatesMetres,
        SettingsSchema.PressRoundEndMetres, SettingsSchema.PressWinPercent, SettingsSchema.PressLineMinMen, SettingsSchema.PressLineInSlotPercent,
        SettingsSchema.PressBandMarginMetres, SettingsSchema.PressBlockEndMetres, SettingsSchema.PressEndSlackMetres,
    };

    private static PressRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return PressRules.From(s);
    }

    /// <summary>Toni's wall: 29 files x 2 ranks, 0.8 m apart across (x), the front rank at y = 0, the rear at y = −1, facing +y (the recruits come
    /// from the north); <paramref name="fallen"/> files emptied (both men) from file <paramref name="at"/>.</summary>
    private static List<(double X, double Y)> Wall(int files = 29, int fallen = 0, int at = 14, int ranks = 2)
    {
        var men = new List<(double, double)>();
        for (int f = 0; f < files; f++)
        {
            if (f >= at && f < at + fallen) continue;
            for (int k = 0; k < ranks; k++) men.Add((-11.2 + f * 0.8, -k * 1.0));
        }
        return men;
    }

    /// <summary>The front of one face from men (facing +y): lateral = −x (e = (−uy, ux) = (−1, 0)), along = y.</summary>
    private static (double front, double lo, double hi, int gaps, double[] glo, double[] ghi, int frontMen) Face(List<(double X, double Y)> men, in PressRules r, bool rear = false)
    {
        int n = men.Count;
        var lat = new double[n];
        var alg = new double[n];
        for (int i = 0; i < n; i++)
        {
            lat[i] = -men[i].X;
            alg[i] = rear ? -men[i].Y : men[i].Y;
        }
        Array.Sort(lat, alg);
        var bins = new double[512];
        var glo = new double[PressMath.MaxGaps];
        var ghi = new double[PressMath.MaxGaps];
        int g = PressMath.FrontOf(lat, alg, n, r.ManWidthMetres, r.FrontDepthMetres, r.GapMetres, bins, glo, ghi, 0, out double front, out double lo, out double hi, out int fm, out _);
        return (front, lo, hi, g, glo, ghi, fm);
    }

    [Fact]
    public void The_switch_is_on_the_player_page_after_the_spears_and_the_numbers_on_Advanced_after_the_lone_runner()
    {
        var all = SettingsSchema.All.ToList();
        var sw = SettingsSchema.PressDontPass;
        Assert.Equal(all.IndexOf(SettingsSchema.SpearPreferenceEnabled) + 1, all.IndexOf(sw));
        Assert.Contains(sw, SettingsSchema.PlayerPage);
        Assert.Equal(SettingsPage.Player, sw.Group.Page);
        Assert.Equal(1, sw.Default, 9);
        Assert.DoesNotMatch(@"[Ss]tep \d", sw.Description);
        int first = all.IndexOf(SettingsSchema.LoneRunnerAheadMetres) + 1;
        for (int i = 0; i < Advanced.Length; i++)
        {
            Assert.Equal(first + i, all.IndexOf(Advanced[i]));
            Assert.Same(SettingsSchema.CourageGroup, Advanced[i].Group);
            Assert.Equal(ApplyTiming.Live, Advanced[i].Timing);
        }
        var r = Rules();
        Assert.True(r.On);
        Assert.Equal(0.5, r.ToleranceMetres, 6);
        Assert.Equal(4.0, r.WatchMetres, 6);
        Assert.Equal(2.0, r.GapMetres, 6);
        Assert.Equal(1.2, r.ManWidthMetres, 6);
        Assert.Equal(1.5, r.FrontDepthMetres, 6);
        Assert.Equal(6.0, r.WrapMarginMetres, 6);
        Assert.Equal(5, r.WrapMates);
        Assert.Equal(4.0, r.WrapMatesMetres, 6);
        Assert.Equal(2.0, r.RoundEndMetres, 6);
        Assert.Equal(50, r.WinPercent, 6);
        Assert.Equal(8, r.LineMinMen);
        Assert.Equal(60, r.LineInSlotPercent, 6);
        Assert.Equal(1.0, r.BandMarginMetres, 6);
        Assert.Equal(0.0, r.BlockEndMetres, 6);
        Assert.Equal(1.0, r.EndSlackMetres, 6);
    }

    [Fact]
    public void The_master_switch_first_then_the_switch_live()
    {
        Assert.Equal("ModEnabled", Rules(s => { s.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm); s.Set(SettingsSchema.PressDontPass, false, SettingSources.Mcm); }).OffBecause);
        var off = Rules(s => s.Set(SettingsSchema.PressDontPass, false, SettingSources.Mcm));
        Assert.False(off.On);
        Assert.Equal("PressDontPass", off.OffBecause);
        Assert.EndsWith("OFF (PressDontPass): measured only", off.Describe());
        Assert.EndsWith(" - ON", Rules().Describe());
        Assert.Contains("never walks past its front", Rules().Describe());
    }

    [Fact]
    public void The_line_test_walls_blocks_slots_rings_and_never_a_charge_a_retreat_or_a_handful()
    {
        var r = Rules();
        Assert.Equal(PressLineKind.Wall, PressMath.LineKind(in r, 59, 40, 59, 30, FearOrder.Wall, false, false));
        Assert.Equal(PressLineKind.Block, PressMath.LineKind(in r, 108, 0, 0, 0, FearOrder.Charge, true, false));
        Assert.Equal(PressLineKind.Slots, PressMath.LineKind(in r, 40, 0, 40, 30, FearOrder.Hold, false, false));      // 75% in their slots
        Assert.Equal(PressLineKind.Slots, PressMath.LineKind(in r, 40, 0, 40, 24, FearOrder.Advance, false, false));   // 60% exactly
        Assert.Equal(PressLineKind.None, PressMath.LineKind(in r, 40, 0, 40, 23, FearOrder.Hold, false, false));       // 57%: a crowd
        Assert.Equal(PressLineKind.None, PressMath.LineKind(in r, 40, 0, 40, 40, FearOrder.Charge, false, false));     // a charge's men have no slots
        Assert.Equal(PressLineKind.None, PressMath.LineKind(in r, 40, 0, 10, 10, FearOrder.Hold, false, false));       // a quarter read
        Assert.Equal(PressLineKind.None, PressMath.LineKind(in r, 59, 59, 59, 59, FearOrder.Retreat, false, false));
        Assert.Equal(PressLineKind.None, PressMath.LineKind(in r, 7, 7, 7, 7, FearOrder.Wall, false, false));          // a handful
        Assert.Equal(PressLineKind.Ring, PressMath.LineKind(in r, 40, 40, 40, 40, FearOrder.Wall, false, true));
    }

    [Fact]
    public void The_front_segment_of_Tonis_wall_its_ends_and_no_gap()
    {
        var r = Rules();
        var f = Face(Wall(), in r);
        Assert.Equal(0, f.front, 6);                          // the front rank
        Assert.Equal(58, f.frontMen);                         // the rear rank (1 m back) holds it too - within 1.5 m
        Assert.Equal(-11.2 - 0.6, f.lo, 6);                   // lateral = −x: the high-x end is the low lateral
        Assert.Equal(11.2 + 0.6, f.hi, 6);
        Assert.Equal(23.6, f.hi - f.lo, 6);                   // ~23 m - Toni's wall
        Assert.Equal(0, f.gaps);
        var rear = Face(Wall(), in r, rear: true);
        Assert.Equal(1.0, rear.front, 6);                     // the rear face: the rear rank at y = −1 (outward −y)
        Assert.Equal(0, rear.gaps);
        // a man who stepped 2 m out of the front never moves the front (the median of the files)
        var w = Wall();
        w.Add((0.0, 2.0));
        Assert.Equal(0, Face(w, in r).front, 6);
    }

    [Fact]
    public void A_real_gap_where_front_men_fell_and_none_where_a_rear_man_stepped_up()
    {
        var r = Rules();
        // four files gone (both ranks): 4 x 0.8 = 3.2 m of empty front → a gap between the neighbours' covers: 4.0 − 1.2 = 2.8 m
        var f = Face(Wall(fallen: 4, at: 14), in r);
        Assert.Equal(1, f.gaps);
        double width = f.ghi[0] - f.glo[0];
        Assert.Equal(4 * 0.8 + 0.8 - 1.2, width, 6);
        Assert.True(PressMath.InGap(-0.2 + -(14 * 0.8 - 11.2), f.glo, f.ghi, 0, 1)); // the middle of the fallen files
        Assert.False(PressMath.InGap(-(-11.2), f.glo, f.ghi, 0, 1));
        // two files gone: 2.4 − 1.2 = 1.2 m uncovered - no gap (men press on through no hole)
        Assert.Equal(0, Face(Wall(fallen: 2, at: 14), in r).gaps);
        // the FRONT man of three files fell, the rear men stood: they hold the front (1 m behind it) - no gap
        var w = Wall();
        w.RemoveAll(m => m.Y == 0 && m.X > -0.1 && m.X < 1.7);
        Assert.Equal(0, Face(w, in r).gaps);
        // a looser line (2.6 m apart): 1.4 m uncovered - no gap; 3.4 m apart: 2.2 m - a gap between every pair
        var loose = Enumerable.Range(0, 10).Select(i => (i * 2.6, 0.0)).ToList();
        Assert.Equal(0, Face(loose, in r).gaps);
        var looser = Enumerable.Range(0, 10).Select(i => (i * 3.4, 0.0)).ToList();
        Assert.Equal(PressMath.MaxGaps, Face(looser, in r).gaps);
    }

    [Fact]
    public void On_his_side_he_is_clamped_past_the_front_pushed_out_behind_the_line_walked_round_the_nearest_end()
    {
        var r = Rules();
        double lo = -11.8, hi = 11.8, depth = 1.0;
        // in front within the watch: clamped; farther: his own AI
        var o = PressMath.Decide(in r, 0.3, 0, lo, hi, depth, false, false, 0, false, false, false, true);
        Assert.Equal(PressAction.Clamp, o.Action);
        Assert.True(o.ClampFront);
        Assert.Equal(PressAction.Far, PressMath.Decide(in r, 6, 0, lo, hi, depth, false, false, 0, false, false, false, true).Action);
        // within the tolerance past it: still clamped (a jostle at the front)
        Assert.Equal(PressAction.Clamp, PressMath.Decide(in r, -0.4, 0, lo, hi, depth, false, false, 0, false, false, false, true).Action);
        // in the line's body: pushed straight back out to 0.3 m in front, his lateral kept
        o = PressMath.Decide(in r, -1.2, 3, lo, hi, depth, false, false, 0, false, false, false, true);
        Assert.Equal(PressAction.Push, o.Action);
        Assert.Equal(PressMath.PushToMetres, o.ToD, 6);
        Assert.Equal(3, o.ToL, 6);
        // beside its end, past the face, not wrapping: pushed forward too (no going round)
        Assert.Equal(PressAction.Push, PressMath.Decide(in r, -1.5, 14, lo, hi, depth, false, false, 9, false, false, false, true).Action);
        // behind the whole line: walked round the NEAREST end - sideways behind it first, then forward
        o = PressMath.Decide(in r, -4, 8, lo, hi, depth, false, false, 0, false, false, false, true);
        Assert.Equal(PressAction.Return, o.Action);
        Assert.Equal(hi + r.RoundEndMetres + 0.5, o.ToL, 6);
        Assert.Equal(-4, o.ToD, 6);
        o = PressMath.Decide(in r, -2.1, -8, lo, hi, depth, false, false, 0, false, false, false, true);
        Assert.Equal(PressAction.Return, o.Action);
        Assert.Equal(lo - r.RoundEndMetres - 0.5, o.ToL, 6);
        Assert.Equal(-2.1, o.ToD, 6);                                     // kept behind the rear (at least 2 m) while going sideways
        PressMath.ReturnPlace(in r, -1.5, -8, lo, hi, depth, out double behindD, out _);
        Assert.Equal(-(depth + PressMath.BehindExtraMetres), behindD, 6);
        o = PressMath.Decide(in r, -4, hi + 2.5, lo, hi, depth, false, false, 0, false, false, false, true);
        Assert.Equal(PressAction.Return, o.Action);
        Assert.Equal(PressMath.ReturnFrontMetres, o.ToD, 6);              // beyond the end: forward to his side
        // a broken line, a winning side: left alone
        Assert.Equal(PressAction.Broken, PressMath.Decide(in r, -4, 8, lo, hi, depth, false, false, 0, true, false, false, true).Action);
        Assert.Equal(PressAction.Winning, PressMath.Decide(in r, 0.2, 0, lo, hi, depth, false, false, 0, false, true, false, true).Action);
    }

    [Fact]
    public void Through_only_at_a_gap_round_only_with_a_wider_mass()
    {
        var r = Rules();
        double lo = -11.8, hi = 11.8, depth = 1.0;
        Assert.Equal(PressAction.Through, PressMath.Decide(in r, 0.2, 1, lo, hi, depth, true, false, 0, true, false, false, true).Action);
        Assert.Equal(PressAction.Through, PressMath.Decide(in r, -3, 1, lo, hi, depth, true, false, 0, true, false, false, true).Action);
        // Toni's recruits: 26.6 m against 23.6 m - not wider by 6 m: never round
        Assert.False(PressMath.UnitWraps(in r, 26.6, 23.6));
        Assert.True(PressMath.UnitWraps(in r, 60, 23.6));
        // a wider unit: a man beside the end with five mates goes round; a lone man or a group of four does not
        Assert.Equal(PressAction.Wrap, PressMath.Decide(in r, -2, 14, lo, hi, depth, false, true, 5, false, false, false, true).Action);
        Assert.Equal(PressAction.Wrap, PressMath.Decide(in r, 0.5, 14, lo, hi, depth, false, true, 7, false, false, false, true).Action);
        Assert.Equal(PressAction.Push, PressMath.Decide(in r, -2, 14, lo, hi, depth, false, true, 4, false, false, false, true).Action);
        Assert.Equal(PressAction.Push, PressMath.Decide(in r, -2, 14, lo, hi, depth, false, true, 0, false, false, false, true).Action);
        // the wrapping mass's man in FRONT of the line's middle still presses (round, not through)
        Assert.Equal(PressAction.Clamp, PressMath.Decide(in r, 0.3, 0, lo, hi, depth, false, true, 9, false, false, false, true).Action);
        // a wrapped man behind the line is left there (he came round legitimately)
        Assert.Equal(PressAction.Wrap, PressMath.Decide(in r, -5, 0, lo, hi, depth, false, true, 9, false, false, false, true).Action);
        Assert.True(PressMath.ManWraps(in r, true, 5));
        Assert.False(PressMath.ManWraps(in r, false, 30));
    }

    [Fact]
    public void A_rear_rank_man_never_drifts_out_past_the_lines_end()
    {
        var r = Rules();
        double lo = -11.8, hi = 11.8;
        var o = PressMath.Decide(in r, 3, 13.5, lo, hi, 1, false, false, 3, false, false, true, true);
        Assert.Equal(PressAction.Clamp, o.Action);
        Assert.Equal(1, o.Side);
        Assert.Equal(-1, PressMath.Decide(in r, 3, -13.5, lo, hi, 1, false, false, 3, false, false, true, true).Side);
        Assert.Equal(0, PressMath.Decide(in r, 3, 12.5, lo, hi, 1, false, false, 3, false, false, true, true).Side);   // within the slack
        Assert.Equal(0, PressMath.Decide(in r, 3, 13.5, lo, hi, 1, false, false, 3, false, false, false, true).Side);  // a front-rank man
        Assert.Equal(0, PressMath.Decide(in r, 3, 13.5, lo, hi, 1, false, false, 3, false, false, true, false).Side);  // his unit not in contact
        // a rear-rank man of a unit far from the line, beyond the watch: still clamped sideways while his unit is in contact
        o = PressMath.Decide(in r, 6, 14, lo, hi, 1, false, false, 3, false, false, true, true);
        Assert.Equal(PressAction.Clamp, o.Action);
        Assert.False(o.ClampFront);
    }

    [Fact]
    public void The_input_clamp_takes_out_only_the_part_toward_the_line()
    {
        float vx = 0.6f, vy = 0.8f;
        Assert.True(PressMath.ClampVector(ref vx, ref vy, 0, 1)); // the line is ahead (+y): only the forward part goes
        Assert.Equal(0.6f, vx, 4);
        Assert.Equal(0f, vy, 4);
        vx = 0.3f;
        vy = -0.9f;
        Assert.False(PressMath.ClampVector(ref vx, ref vy, 0, 1)); // backing away: untouched
        Assert.Equal(-0.9f, vy, 4);
        vx = 1;
        vy = 0;
        Assert.False(PressMath.ClampVector(ref vx, ref vy, 0, 1)); // along the front: untouched
        // a ring: pushed out radially, clamped within the watch
        var r = Rules();
        Assert.Equal(PressAction.Push, PressMath.DecideRing(in r, -1, false).Action);
        Assert.Equal(PressAction.Clamp, PressMath.DecideRing(in r, 0.5, false).Action);
        Assert.Equal(PressAction.Far, PressMath.DecideRing(in r, 9, false).Action);
    }

    [Fact]
    public void The_table_publishes_the_wall_as_a_line_and_the_recruits_attack_its_front_not_wider_by_the_margin()
    {
        var r = Rules();
        var t = new PressLines();
        int wallKey = FearMath.UnitKey(0, 2), recruits = FearMath.UnitKey(1, 1);
        var held = new bool[FearMath.UnitCount];
        var ring = new bool[FearMath.UnitCount];
        double now = 100;
        t.Due(now);
        for (int round = 0; round < 3; round++)
        {
            now += 0.5;
            foreach (var m in Wall()) t.Note(wallKey, m.X, m.Y, 0, 1, FearOrder.Wall, 0.2, held: true); // facing north
            // 108 recruits: 18 files x 6 ranks, 1.5 m apart across (26.6 m incl. a man), their front 1 m north of the wall
            for (int f = 0; f < 18; f++)
                for (int k = 0; k < 6; k++)
                {
                    t.Note(recruits, -12.75 + f * 1.5, 1 + k * 1.2, 0, -1, FearOrder.Charge, double.NaN, held: false);
                    t.Vote(recruits, wallKey);
                }
            Assert.True(t.Due(now));
            t.Publish(now, in r, held, ring);
        }
        Assert.True(t.IsLine(wallKey, now));
        Assert.Equal(PressLineKind.Wall, t.Kind[wallKey]);
        Assert.False(t.IsLine(recruits, now)); // a charge with no block: a crowd, not a line
        Assert.Equal(wallKey, t.LineOf(recruits, now));
        Assert.Equal(0, t.Face[recruits]);
        Assert.Equal(1.0, t.Depth[wallKey], 6);
        Assert.InRange(t.UnitWidth[recruits], 25.0, 28.0);
        Assert.False(t.Wraps[recruits]);
        Assert.Equal(1.0, t.UnitFront[recruits], 6);           // the recruits' front rank 1 m in front of the wall's front
        t.Frame(wallKey, 0, 3, 0.4, out double d, out double l);
        Assert.Equal(0.4, d, 6);
        Assert.Equal(-3, l, 6);
        t.World(wallKey, 0, d, l, out double x, out double y);
        Assert.Equal(3, x, 6);
        Assert.Equal(0.4, y, 6);
        t.Inward(wallKey, 0, out double nx, out double ny);
        Assert.Equal(0, nx, 6);
        Assert.Equal(-1, ny, 6);
        t.Frame(wallKey, 1, 0, -3, out d, out _);
        Assert.Equal(2, d, 6);                                 // 2 m behind the rear rank = 2 m in front of the rear face
        // a unit 60 m wide on the same wall: wider by 6 m+ - it wraps
        int wide = FearMath.UnitKey(1, 3);
        now += 0.5;
        foreach (var m in Wall()) t.Note(wallKey, m.X, m.Y, 0, 1, FearOrder.Wall, 0.2, held: true);
        for (int f = 0; f < 40; f++)
            for (int k = 0; k < 3; k++)
            {
                t.Note(wide, -29.25 + f * 1.5, 1 + k * 1.2, 0, -1, FearOrder.Charge, double.NaN, held: false);
                t.Vote(wide, wallKey);
            }
        t.Publish(now, in r, held, ring);
        Assert.True(t.Wraps[wide]);
        Assert.True(t.WrapUnits >= 1);
        // a unit behind the wall attacks its REAR face
        int behind = FearMath.UnitKey(1, 4);
        now += 0.5;
        foreach (var m in Wall()) t.Note(wallKey, m.X, m.Y, 0, 1, FearOrder.Wall, 0.2, held: true);
        for (int i = 0; i < 20; i++)
        {
            t.Note(behind, -5 + i * 0.5, -4, 0, 1, FearOrder.Charge, double.NaN, held: false);
            t.Vote(behind, wallKey);
        }
        t.Publish(now, in r, held, ring);
        Assert.Equal(1, t.Face[behind]);
    }

    [Fact]
    public void The_fear_band_is_never_wider_than_the_line_it_faces_its_surplus_deeper()
    {
        var fr = FearRules.From(new TraxSettings());
        double Publish(double margin, out int ranks, out FearUnits units)
        {
            units = new FearUnits { PressBandMarginMetres = margin };
            const int key = 1 * FearMath.Formations + 1;
            double t = 50;
            units.Due(t);
            for (int p = 0; p < 2; p++)
            {
                t += 0.5;
                for (int i = 0; i < 108; i++)
                    units.Note(key, 0.2, 0.0, 30, 0.5, true, 0, 10, 0, -4, 0, 1, (i % 18) * 1.5 - 12.75, -(i / 18) * 1.2, FearOrder.Charge, double.NaN, i);
                units.NoteEnemyFront(key, t, 0, 10.5, 0, -1, 23, 1); // Toni's wall faces south, 23 m wide
                units.Publish(t, in fr, null, true);
            }
            ranks = units.BandRanks[key];
            return units.BandWidth[key];
        }
        double off = Publish(double.NaN, out int offRanks, out var u0);
        double on = Publish(1.0, out int onRanks, out var u1);
        Assert.Equal(27.0, off, 6);                      // 108 men in 4 ranks of 1 m files - wider than the 23 m line (Toni's p90 27.5 m)
        Assert.Equal(4, offRanks);
        Assert.Equal(25.0, on, 6);                       // the line + 1 m a side
        Assert.Equal(5, onRanks);                        // ceil(108 / 25) - beyond the band's own 4: the surplus deeper
        Assert.True(u1.PressBandClamps > 0 && u1.PressBandExtraRanks > 0);
        Assert.Equal(0, u0.PressBandClamps);
        var r = Rules();
        Assert.Equal(25, PressMath.BandWidth(in r, 27, 23), 6);
        Assert.Equal(20, PressMath.BandWidth(in r, 20, 23), 6);
        Assert.Equal(27, PressMath.BandWidth(in r, 27, double.NaN), 6);
        Assert.Equal(5, PressMath.BandRanks(108, 25, 4));
        Assert.Equal(6, PressMath.BandRanks(10, 25, 6));
    }

    [Fact]
    public void The_block_holds_sideways_range_stays_within_the_enemy_lines_ends()
    {
        var r = Rules();
        // Toni's recruits: the cap 26.6 m ± the slack 2 = ±15.3 m; his wall's ends at ±11.8 m (centred) → cut to ±11.8
        Assert.True(PressMath.BlockRange(in r, 15.3, -11.8, 11.8, out double lo, out double hi));
        Assert.Equal(-11.8, lo, 6);
        Assert.Equal(11.8, hi, 6);
        // the wall off to one side: only the overlap; never narrower than 2 m
        Assert.True(PressMath.BlockRange(in r, 15.3, 5, 30, out lo, out hi));
        Assert.Equal(5, lo, 6);
        Assert.Equal(15.3, hi, 6);
        Assert.True(PressMath.BlockRange(in r, 15.3, 40, 60, out lo, out hi));
        Assert.Equal(2, hi - lo, 6);
        Assert.Equal(15.3, hi, 6);
        // a wider line: nothing cut
        Assert.False(PressMath.BlockRange(in r, 15.3, -30, 30, out lo, out hi));
        Assert.Equal(-15.3, lo, 6);
        // the range rule = step 74 for a symmetric range; a man at 13 m pulled to 10.8 inside the cut range
        var br = BlockHoldRules.From(new TraxSettings());
        var a = BlockHoldMath.Decide(in br, 1, 13.3, 7.2, 14, 0, double.NaN, false, out double tl1, out _, out _);
        var b = BlockHoldMath.DecideRange(in br, 1, -15.3, 15.3, 7.2, 14, 0, double.NaN, false, out double tl2, out _, out _);
        Assert.Equal(a, b);
        Assert.Equal(tl1, tl2, 9);
        var c = BlockHoldMath.DecideRange(in br, 1, -11.8, 11.8, 7.2, 13, 0, double.NaN, false, out double tl3, out _, out _);
        Assert.Equal(BlockPull.Lateral, c);
        Assert.Equal(10.8, tl3, 6);
        Assert.Equal(BlockPull.None, BlockHoldMath.DecideRange(in br, 1, -11.8, 11.8, 7.2, 11, 0, double.NaN, false, out _, out _, out _));
    }

    [Fact]
    public void The_cause_order_and_the_summary_lines()
    {
        Assert.Equal(PressCause.Courage, PressMath.CauseOf(true, true, true, true));
        Assert.Equal(PressCause.Fear, PressMath.CauseOf(false, true, true, true));
        Assert.Equal(PressCause.Block, PressMath.CauseOf(false, false, true, true));
        Assert.Equal(PressCause.ChargeTarget, PressMath.CauseOf(false, false, false, true));
        Assert.Equal(PressCause.Game, PressMath.CauseOf(false, false, false, false));
        var s = new PressStats();
        s.Crossings[0, 0, (int)PressCause.ChargeTarget, 0] = 12;
        s.Crossings[0, 1, (int)PressCause.Game, 0] = 3;
        s.Crossings[1, 1, (int)PressCause.Game, 1] = 2;
        s.AllowedBy[1, 0] = 2;
        s.Actions[1, (int)PressAction.Clamp] = 40;
        s.NearLooks[0] = 100;
        s.PastLooks[0] = 25;
        s.ClampFrames = 900;
        var lines = new List<string>();
        s.AddSummaryLines(lines, Rules(), new PressLines());
        Assert.Equal(2, lines.Count);
        Assert.StartsWith("press, don't pass (step 77): ON at the end - lines published 0", lines[0]);
        Assert.Contains("pressing (clamped) 40", lines[0]);
        Assert.Contains("CLAMPS applied 900 input frames", lines[0]);
        Assert.Contains("OFF 15 - round an end 12 (charge target 12), through the middle 3 (the game 3)", lines[1]);
        Assert.Contains("ON 2 - round an end 0, through the middle 2 (the game 2); allowed: a gap 2", lines[1]);
        Assert.Contains("OFF 25.0% (25 of 100)", lines[1]);
    }
}

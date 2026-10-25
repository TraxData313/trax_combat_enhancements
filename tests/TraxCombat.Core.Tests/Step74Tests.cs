using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 74 (Toni's battle 2026-10-20 20:30; AI_NOTES "Step 74", DESIGN decision 68) - part 1: THE MAP ONE RANK DEEPER (T0 7, T1 6, T2 5, T3 4, T4 3,
/// T5 2, T6 1 - T6 stays) in defaults.json, config format 16 moving only the untouched old values; the worked examples on the shipped map.
/// </summary>
public class Step74MapTests
{
    private static readonly double[] OldMap = { 6, 5, 4, 3, 2, 1, 1 };
    private static readonly double[] NewMap = { 7, 6, 5, 4, 3, 2, 1 };

    private static readonly ParamDef[] Keys =
    {
        SettingsSchema.LineDisciplineRanksT0, SettingsSchema.LineDisciplineRanksT1, SettingsSchema.LineDisciplineRanksT2, SettingsSchema.LineDisciplineRanksT3,
        SettingsSchema.LineDisciplineRanksT4, SettingsSchema.LineDisciplineRanksT5, SettingsSchema.LineDisciplineRanksT6,
    };

    /// <summary>defaults.json's step-74 map (the tests run on DESIGN's initial step-73 map).</summary>
    private static double Shipped(ParamDef p)
    {
        int i = Array.IndexOf(Keys, p);
        return i >= 0 ? NewMap[i] : p.Default;
    }

    internal static LineDisciplineRules ShippedRules() =>
        new LineDisciplineRules(true, true, true, NewMap[0], NewMap[1], NewMap[2], NewMap[3], NewMap[4], NewMap[5], NewMap[6]);

    [Fact]
    public void The_shipped_map_is_one_rank_deeper_below_T6()
    {
        Assert.Equal(24, ConfigFile.FormatVersion);
        var r = ShippedRules();
        Assert.Equal(NewMap, Enumerable.Range(0, 7).Select(t => r.RanksOf(t)));
        Assert.Equal("T0 7.0 / T1 6.0 / T2 5.0 / T3 4.0 / T4 3.0 / T5 2.0 / T6 1.0", r.MapText());
        Assert.Contains("120 recruits at 6 make a line at most 20 men wide.", SettingsSchema.LineDisciplineRanksT1.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Tonis_examples_on_the_new_map()
    {
        var r = ShippedRules();
        // 120 T1 recruits: D 6 → at most 20 files, 6 full ranks
        double d = LineDisciplineMath.MinDepth(120 * r.RanksOf(1), 120);
        Assert.Equal(6, d, 9);
        Assert.Equal(20, LineDisciplineMath.CapFiles(120, d));
        Assert.Equal("20 files x 6 ranks, 6.0 deep", LineDisciplineMath.ShapeText(120, 20));
        // 180 T1 (Toni's 20:30 battle): D 6 → 30 files (was 36 at D 5)
        Assert.Equal(30, LineDisciplineMath.CapFiles(180, 6));
        // 60 T5 veterans: D 2 → 30 files (step 73's map: never narrowed)
        double v = LineDisciplineMath.MinDepth(60 * r.RanksOf(5), 60);
        Assert.Equal(2, v, 9);
        Assert.Equal(30, LineDisciplineMath.CapFiles(60, v));
        // 60 T6 elites: D 1 → 60 files - never narrowed
        Assert.Equal(60, LineDisciplineMath.CapFiles(60, LineDisciplineMath.MinDepth(60 * r.RanksOf(6), 60)));
        // the mixed unit 70 T1 + 30 T3 + 20 T5 → D (420 + 120 + 40) / 120 = 4.83 → 25 files, 4 full ranks + 20
        double m = LineDisciplineMath.MinDepth(70 * r.RanksOf(1) + 30 * r.RanksOf(3) + 20 * r.RanksOf(5), 120);
        Assert.Equal(4.8333, m, 3);
        Assert.Equal(25, LineDisciplineMath.CapFiles(120, m));
        Assert.Equal(20, LineDisciplineMath.LastRankMen(120, 25));
        // a hero still counts as T6 (1 rank)
        Assert.Equal(1, r.RanksOf(LineDisciplineMath.TierOf(2, true, in r)));
    }

    [Fact]
    public void Format_16_moves_the_untouched_map_once_and_keeps_a_tuned_value()
    {
        var read = ConfigFile.Read("{ \"ConfigVersion\": 15, \"LineDisciplineRanksT0\": 6.0, \"LineDisciplineRanksT1\": 5.0, \"LineDisciplineRanksT2\": 4.0, "
                                   + "\"LineDisciplineRanksT3\": 3.0, \"LineDisciplineRanksT4\": 2.0, \"LineDisciplineRanksT5\": 1.0, \"LineDisciplineRanksT6\": 1.0 }");
        var notes = ConfigFile.Migrate(read, Shipped);
        Assert.Equal(6, notes.Count);
        Assert.Equal("LineDisciplineRanksT1: 5.0 → 6.0 (format 15 → 24, the old default; step 74, Toni: the tier map one rank deeper (T6 stays 1) - a recruit holds 6 ranks (120 recruits: at most 20 files))", notes[1]);
        for (int t = 0; t < 7; t++) Assert.Equal(NewMap[t], read.Values[Keys[t].Key], 9);
        Assert.Equal(6, ConfigFile.Migrate(read, Shipped).Count); // idempotent (the notes list is the read's own)

        // his own values stay - and are said once; T6 never moves
        var mine = ConfigFile.Read("{ \"ConfigVersion\": 15, \"LineDisciplineRanksT1\": 4.0, \"LineDisciplineRanksT5\": 1.0, \"LineDisciplineRanksT6\": 2.0 }");
        var kept = ConfigFile.Migrate(mine, Shipped);
        Assert.Equal(2, kept.Count);
        Assert.StartsWith("LineDisciplineRanksT1: 4.0 kept - your own value (format 15 → 24; the new default 6.0 is not pushed over it; step 74", kept[0], StringComparison.Ordinal);
        Assert.StartsWith("LineDisciplineRanksT5: 1.0 → 2.0 (format 15 → 24, the old default; step 74", kept[1], StringComparison.Ordinal);
        Assert.Equal(4, mine.Values["LineDisciplineRanksT1"], 9);
        Assert.Equal(2, mine.Values["LineDisciplineRanksT6"], 9);

        // a format-16 file: nothing moves; DESIGN's initial map (the tests' defaults): nothing moves either
        var now = ConfigFile.Read("{ \"ConfigVersion\": 16, \"LineDisciplineRanksT1\": 5.0 }");
        Assert.Empty(ConfigFile.Migrate(now, Shipped));
        var design = ConfigFile.Read("{ \"ConfigVersion\": 15, \"LineDisciplineRanksT1\": 5.0 }");
        Assert.Empty(ConfigFile.Migrate(design, p => p.Default));
        Assert.Equal(OldMap[1], SettingsSchema.LineDisciplineRanksT1.Default, 9); // DESIGN's initial value stays step 73's
    }
}

/// <summary>
/// Step 74 - part 2: THE BLOCK HOLD (a formation of low line discipline keeps its block in a charge and the last metres of an advance) and THE
/// BLOCK DIAGNOSTIC: the settings, the switches (the master switch first, step 73's, the hold's own), the strength by D, the rule (across, ahead,
/// behind, the striker, the hysteresis), the unit table's frame (the median centre line a trail cannot drag, the percentiles, the speed, the
/// first contact), the diagnostic's bins and the summary lines.
/// </summary>
public class Step74BlockHoldTests
{
    private static readonly ParamDef[] New =
    {
        SettingsSchema.LineBlockHoldEnabled, SettingsSchema.LineBlockSlackMetres, SettingsSchema.LineBlockFullAtDepth, SettingsSchema.LineBlockLooseMetres,
        SettingsSchema.LineBlockRankMetres, SettingsSchema.LineBlockAheadMetres, SettingsSchema.LineBlockStrikeReachMetres, SettingsSchema.LineBlockStrikeLeashMetres,
        SettingsSchema.LineBlockNearMetres, SettingsSchema.LineBlockLeadMetres, SettingsSchema.LineBlockMopUpWinPercent,
    };

    private static BlockHoldRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return BlockHoldRules.From(s);
    }

    [Fact]
    public void The_eleven_settings_sit_after_the_line_discipline_ones_on_the_Advanced_page_all_live()
    {
        var all = SettingsSchema.All.ToList();
        int first = all.IndexOf(SettingsSchema.LineMinWidthToDepthRatio) + 1; // step 83's ratio sits between
        for (int i = 0; i < New.Length; i++)
        {
            Assert.Equal(first + i, all.IndexOf(New[i]));
            Assert.Same(SettingsSchema.LineDisciplineGroup, New[i].Group);
            Assert.Equal(SettingsPage.Advanced, New[i].Group.Page);
            Assert.Equal(ApplyTiming.Live, New[i].Timing);
            Assert.DoesNotContain(New[i], SettingsSchema.PlayerPage);
        }
        var r = Rules();
        Assert.True(r.On);
        Assert.Equal(2, r.SlackMetres, 9);
        Assert.Equal(3, r.FullAtDepth, 9);
        Assert.Equal(10, r.LooseMetres, 9);
        Assert.Equal(1.2, r.RankMetres, 5);
        Assert.Equal(3, r.AheadMetres, 9);
        Assert.Equal(2.5, r.StrikeReachMetres, 9);
        Assert.Equal(2, r.StrikeLeashMetres, 9);
        Assert.Equal(30, r.NearMetres, 9);
        Assert.Equal(1.5, r.LeadMetres, 9);
        Assert.Equal(50, r.MopUpWinPercent, 9);
        var s = new TraxSettings();
        s.Set(SettingsSchema.LineBlockSlackMetres, 5.0, "test");
        Assert.Equal(5, BlockHoldRules.From(s).SlackMetres, 9); // live: the next rules read it
    }

    [Fact]
    public void The_master_switch_first_then_step_73s_then_its_own()
    {
        var off = Rules(s => s.Set(SettingsSchema.ModEnabled, false, "test"));
        Assert.False(off.On);
        Assert.Equal("ModEnabled", off.OffBecause);
        var lines = Rules(s => s.Set(SettingsSchema.LineDisciplineEnabled, false, "test"));
        Assert.False(lines.On);
        Assert.Equal("LineDisciplineEnabled", lines.OffBecause);
        var own = Rules(s => s.Set(SettingsSchema.LineBlockHoldEnabled, false, "test"));
        Assert.False(own.On);
        Assert.Equal("LineBlockHoldEnabled", own.OffBecause);
        var mine = Rules(s => s.Set(SettingsSchema.LineDisciplineForPlayer, false, "test"));
        Assert.True(mine.On);
        Assert.False(mine.PlayerOn);
        Assert.Contains("- OFF (LineBlockHoldEnabled)", own.Describe(), StringComparison.Ordinal);
        Assert.EndsWith("- ON", Rules().Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_hold_scales_with_D_elites_free()
    {
        var r = Rules();
        Assert.Equal(0, BlockHoldMath.Strength(in r, 1.0), 9);
        Assert.Equal(0, BlockHoldMath.Strength(in r, double.NaN), 9);
        Assert.Equal(0.5, BlockHoldMath.Strength(in r, 2.0), 9); // veterans on the new map
        Assert.Equal(1, BlockHoldMath.Strength(in r, 3.0), 9);
        Assert.Equal(1, BlockHoldMath.Strength(in r, 6.0), 9); // recruits
        Assert.Equal(27 + 2, BlockHoldMath.Allowance(in r, 27, 1), 9);
        Assert.Equal(27 + 2 + 5, BlockHoldMath.Allowance(in r, 27, 0.5), 9);
        Assert.Equal(27 + 2 + 10, BlockHoldMath.Allowance(in r, 27, 0), 9);
        Assert.Equal(6, BlockHoldMath.Ranks(180, 30)); // 180 men in 30 files = 6 ranks x 1.2 m
        Assert.Equal(7.2, BlockHoldMath.Depth(in r, 180, 30), 5);
        Assert.Equal(6, BlockHoldMath.Ranks(176, 30)); // a partly filled last rank counts
    }

    [Fact]
    public void Tonis_recruits_the_flank_runner_the_one_ahead_the_straggler_and_the_fighters()
    {
        var r = Rules();
        double half = 22, depth = 7.2; // 180 T1 at D 6: 30 files, ~44 m at 1.5 m a file
        Assert.Equal(BlockPull.None, BlockHoldMath.Decide(in r, 1, half, depth, 20, 0, double.NaN, false, out _, out _, out bool st0));
        Assert.False(st0);
        // round the right flank, 30 m out: pulled back to 23 m (the allowance 24 less the 1 m inside)
        Assert.Equal(BlockPull.Lateral, BlockHoldMath.Decide(in r, 1, half, depth, 30, 2, double.NaN, false, out double l1, out double a1, out _));
        Assert.Equal(23, l1, 9);
        Assert.Equal(2, a1, 9);
        Assert.Equal(BlockPull.Lateral, BlockHoldMath.Decide(in r, 1, half, depth, -40, 0, double.NaN, false, out double l2, out _, out _));
        Assert.Equal(-23, l2, 9);
        // 10 m ahead of the median (the front 3.6 + 3): held back to 5.6
        Assert.Equal(BlockPull.Ahead, BlockHoldMath.Decide(in r, 1, half, depth, 0, 10, double.NaN, false, out _, out double a3, out _));
        Assert.Equal(5.6, a3, 9);
        // ... but with an enemy at 2 m he fights
        Assert.Equal(BlockPull.None, BlockHoldMath.Decide(in r, 1, half, depth, 0, 8, 2.0, false, out _, out _, out bool st4));
        Assert.True(st4);
        // a fighter on the flank just past the allowance (24 + 1.5) fights; 6 m past it (round the flank) is walked back
        Assert.Equal(BlockPull.None, BlockHoldMath.Decide(in r, 1, half, depth, 25.5, 0, 2.0, false, out _, out _, out bool st5));
        Assert.True(st5);
        Assert.Equal(BlockPull.Lateral, BlockHoldMath.Decide(in r, 1, half, depth, 30, 0, 2.0, false, out _, out _, out bool st6));
        Assert.False(st6);
        // a straggler 12 m behind the median (the rear 3.6 + 2): pulled forward to -4.6
        Assert.Equal(BlockPull.Behind, BlockHoldMath.Decide(in r, 1, half, depth, 0, -12, double.NaN, false, out _, out double a7, out _));
        Assert.Equal(-4.6, a7, 9);
        // the hysteresis: a man being pulled is let go only 1 m inside the allowance
        Assert.Equal(BlockPull.Lateral, BlockHoldMath.Decide(in r, 1, half, depth, 23.5, 0, double.NaN, true, out _, out _, out _));
        Assert.Equal(BlockPull.None, BlockHoldMath.Decide(in r, 1, half, depth, 22.9, 0, double.NaN, true, out _, out _, out _));
        Assert.Equal(BlockPull.None, BlockHoldMath.Decide(in r, 1, half, depth, 23.5, 0, double.NaN, false, out _, out _, out _));
        // a looser block (veterans, s 0.5) lets the same man stand
        Assert.Equal(BlockPull.None, BlockHoldMath.Decide(in r, 0.5, half, depth, 27, 0, double.NaN, false, out _, out _, out _));
        var never = Rules(s => s.Set(SettingsSchema.LineBlockStrikeReachMetres, 0.0, "test"));
        Assert.Equal(BlockPull.Ahead, BlockHoldMath.Decide(in never, 1, half, depth, 0, 8, 1.0, false, out _, out _, out _));
        // the lead: only while the block moves forward
        Assert.Equal(1.5, BlockHoldMath.Lead(in r, 3), 9);
        Assert.Equal(0, BlockHoldMath.Lead(in r, 0.1), 9);
    }

    private static void Man(BlockUnits t, int key, double x, double y, double slot = double.NaN, double enemy = double.NaN) => t.Note(key, x, y, 0, 1, slot, enemy);

    [Fact]
    public void The_unit_table_a_median_centre_line_a_trail_cannot_drag_the_spread_the_speed_the_first_contact()
    {
        var r = Rules();
        var t = new BlockUnits();
        int key = FearMath.UnitKey(1, 0);
        var pub = new int[FearMath.UnitCount];
        Assert.False(t.Due(0)); // arms the clock
        // 150 men in a 30 x 5 block (x -21.75..21.75) and a trail of 30 to the east (x 30..59)
        for (int f = 0; f < 30; f++)
            for (int k = 0; k < 5; k++) Man(t, key, -21.75 + f * 1.5, -k * 1.2, slot: 0.5);
        for (int i = 0; i < 30; i++) Man(t, key, 30 + i, -2, slot: 10);
        t.NoteCap(key, 0.5, true, 6, 30, 44, 180, false, true);
        Assert.True(t.Due(0.5));
        Assert.Equal(1, t.Publish(0.5, in r, null, pub));
        Assert.Equal(key, pub[0]);
        Assert.True(t.Live(key, 0.5));
        Assert.Equal(180, t.Men[key]);
        Assert.Equal(1, t.UY[key], 9); // the facing (no band): north
        // the lateral = position . e, e = (-uy, ux) = (-1, 0): the block alone would centre at 0; the trail shifts the median by under 5 m (the mean: 6.5 m)
        Assert.InRange(t.CentreLat[key], -5, 0);
        Assert.InRange(t.LatMax[key] - t.LatMin[key], 80, 82);
        Assert.Equal(30 / 180.0, t.OutOfSlot[key], 9);
        Assert.False(t.InContact[key]);
        Assert.True(double.IsNaN(t.FirstContact[key]));
        t.Frame(key, 0.5, 59, -2, out double lat, out _);
        Assert.True(Math.Abs(lat) > 50); // the trail's last man far beyond any allowance
        // the next period: the block and its trail walked 2 m north (4 m/s), one man in contact
        for (int f = 0; f < 30; f++)
            for (int k = 0; k < 5; k++) Man(t, key, -21.75 + f * 1.5, 2 - k * 1.2, enemy: f == 15 && k == 0 ? 1.5 : double.NaN);
        for (int i = 0; i < 30; i++) Man(t, key, 30 + i, 0);
        Assert.Equal(1, t.Publish(1.0, in r, null, pub));
        Assert.Equal(4, t.Speed[key], 1);
        Assert.True(t.InContact[key]);
        Assert.Equal(1.0, t.FirstContact[key], 9);
        Assert.True(t.Near[key]);
        // a place moves with the block (extrapolated by its speed)
        t.World(key, 1.0, 0, 0, out double x0, out double y0);
        t.World(key, 1.5, 0, 0, out double x1, out double y1);
        Assert.Equal(2, y1 - y0, 6);
        Assert.Equal(0, x1 - x0, 6);
        // too few men: no frame
        var small = new BlockUnits();
        small.Due(0);
        for (int i = 0; i < 4; i++) Man(small, key, i, 0);
        small.Publish(0.5, in r, null, pub);
        Assert.False(small.Live(key, 0.5));
    }

    [Fact]
    public void Quantiles_of_sorted_values()
    {
        var v = new double[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        Assert.Equal(5, BlockHoldMath.Quantile(v, 11, 0.5), 9);
        Assert.Equal(1, BlockHoldMath.Quantile(v, 11, 0.1), 9);
        Assert.Equal(9, BlockHoldMath.Quantile(v, 11, 0.9), 9);
        Assert.Equal(0.5, BlockHoldMath.Quantile(new double[] { 0, 1 }, 2, 0.5), 9);
        Assert.True(double.IsNaN(BlockHoldMath.Quantile(v, 0, 0.5)));
    }

    [Fact]
    public void The_diagnostic_bins_by_the_time_from_first_contact_and_says_the_order_and_behaviour()
    {
        Assert.Equal(0, BlockShapeStats.BinOf(-30));
        Assert.Equal(3, BlockShapeStats.BinOf(-1));
        Assert.Equal(4, BlockShapeStats.BinOf(0));
        Assert.Equal(8, BlockShapeStats.BinOf(100));
        Assert.Equal("-5..0 s", BlockShapeStats.BinName(3));
        Assert.Equal("0..+5 s", BlockShapeStats.BinName(4));
        Assert.Equal("after +60 s", BlockShapeStats.BinName(8));
        var r = Rules();
        var t = new BlockUnits();
        var shape = new BlockShapeStats();
        int key = FearMath.UnitKey(1, 0);
        var pub = new int[FearMath.UnitCount];
        t.Due(0);
        double now = 0;
        for (int step = 0; step < 40; step++)
        {
            now += 0.5;
            bool charge = now > 9.5;
            double wide = charge ? 2.4 : 1.5; // the charge spreads them
            for (int f = 0; f < 30; f++)
                for (int k = 0; k < 6; k++) Man(t, key, (f - 15) * wide, now - k * 1.2, slot: charge ? 5 : 0.5, enemy: now >= 12 && f == 15 && k == 0 ? 1 : double.NaN);
            t.NoteCap(key, now, true, 6, 30, 44, 180, false, true);
            t.NoteFormation(key, now, charge ? "Charge" : "Move", charge ? "BehaviorTacticalCharge" : "BehaviorAdvance", "Line", 44);
            t.Publish(now, in r, null, pub);
            var u = shape.For(key, "the enemy's Infantry", false, true)!;
            shape.Add(u, t, key, now);
        }
        var lines = new System.Collections.Generic.List<string>();
        shape.AddSummaryLines(lines);
        Assert.Equal(2, lines.Count);
        Assert.StartsWith("block shape (step 74 diagnostic", lines[0], StringComparison.Ordinal);
        Assert.Contains("block shape, the enemy's Infantry (the other side, AI): first contact at 12.0 s", lines[1], StringComparison.Ordinal);
        Assert.Contains("-5..0 s: Move / BehaviorAdvance / Line 60%, Charge / BehaviorTacticalCharge / Line 40%", lines[1], StringComparison.Ordinal);
        Assert.Contains("0..+5 s: Charge / BehaviorTacticalCharge / Line 100%; width 55.7 m", lines[1], StringComparison.Ordinal);
        Assert.Contains("the game's 44.0 m vs the cap 44.0 m, out of slots 100%", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void The_summary_lines_both_sides_ON_and_OFF_before_and_after_contact()
    {
        var stats = new BlockHoldStats();
        var them = stats.Side(false);
        for (int i = 0; i < 90; i++) them.AddOffset(true, false, i % 2 == 0 ? 10 : -10, 22, 24);
        for (int i = 0; i < 10; i++) them.AddOffset(true, false, 30, 22, 24);
        them.Pulls[(int)BlockPull.Lateral] = 12;
        them.PulledMen = 9;
        them.PulledSeconds = 20.5;
        them.Ends[(int)BlockEnd.BackInside] = 11;
        them.LookKinds[(int)BlockLookKind.Inside] = 88;
        Assert.Equal(12, them.MeanOffset(true, false), 9);
        Assert.Equal(0.1, them.BeyondShare(true, false), 9);
        var lines = new System.Collections.Generic.List<string>();
        stats.AddSummaryLines(lines, Rules());
        Assert.Equal(3, lines.Count);
        Assert.StartsWith("block hold (step 74): ON at the end", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("block hold (step 74), your side", lines[1], StringComparison.Ordinal);
        Assert.Contains("ON before contact mean 12.0 m of 22.0 m, p90 0.50 x the half-width, the farthest 30.0 m, beyond the allowance 10.0% (100 looks)", lines[2], StringComparison.Ordinal);
        Assert.Contains("PULLED IN 12 times by 9 men (round the flank 12, ahead of the front 0, behind the rear 0), 20.5 man-s; ends: back inside 11;", lines[2], StringComparison.Ordinal);
        Assert.Contains("OFF before - (no looks)", lines[2], StringComparison.Ordinal);
    }
}

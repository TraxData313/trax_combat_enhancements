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
        Assert.Equal(16, ConfigFile.FormatVersion);
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
        Assert.Equal("LineDisciplineRanksT1: 5.0 → 6.0 (format 15 → 16, the old default; step 74, Toni: the tier map one rank deeper (T6 stays 1) - a recruit holds 6 ranks (120 recruits: at most 20 files))", notes[1]);
        for (int t = 0; t < 7; t++) Assert.Equal(NewMap[t], read.Values[Keys[t].Key], 9);
        Assert.Equal(6, ConfigFile.Migrate(read, Shipped).Count); // idempotent (the notes list is the read's own)

        // his own values stay - and are said once; T6 never moves
        var mine = ConfigFile.Read("{ \"ConfigVersion\": 15, \"LineDisciplineRanksT1\": 4.0, \"LineDisciplineRanksT5\": 1.0, \"LineDisciplineRanksT6\": 2.0 }");
        var kept = ConfigFile.Migrate(mine, Shipped);
        Assert.Equal(2, kept.Count);
        Assert.StartsWith("LineDisciplineRanksT1: 4.0 kept - your own value (format 15 → 16; the new default 6.0 is not pushed over it; step 74", kept[0], StringComparison.Ordinal);
        Assert.StartsWith("LineDisciplineRanksT5: 1.0 → 2.0 (format 15 → 16, the old default; step 74", kept[1], StringComparison.Ordinal);
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

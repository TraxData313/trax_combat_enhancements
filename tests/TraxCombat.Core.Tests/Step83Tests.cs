using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 83 - SMALL UNITS KEEP A WIDE LINE (Toni 2026-10-23: 20 T1 recruits at the minimum depth 6 stood 3-4 files wide - "a column"; AI_NOTES
/// "Step 83", DESIGN decision 79): the depth held = max(1, min(the tier's D, floor(√(men ÷ ratio)))), ratio = LineMinWidthToDepthRatio (2; 0 =
/// off). Toni's table on the shipped map, the ratio 0, a unit shrinking from 100 to 20, and the shape at least 2 : 1 for every count 1-200 at
/// every tier - files and ranks as the arrangement makes them, the partly filled last rank included.
/// </summary>
public class Step83Tests
{
    private static readonly double[] Shipped = { 7, 6, 5, 4, 3, 2, 1 };

    private static LineDisciplineRules ShippedRules(double ratio = 2) =>
        new LineDisciplineRules(true, true, true, Shipped[0], Shipped[1], Shipped[2], Shipped[3], Shipped[4], Shipped[5], Shipped[6], widthRatio: ratio);

    /// <summary>The held depth, the cap's files and the ranks they make for <paramref name="men"/> men of one tier.</summary>
    private static (double Depth, int Files, int Ranks) Shape(in LineDisciplineRules r, int tier, int men)
    {
        double d = LineDisciplineMath.HeldDepth(LineDisciplineMath.MinDepth(r.RanksOf(tier) * men, men), men, r.WidthRatio);
        int files = LineDisciplineMath.CapFiles(men, d);
        return (d, files, LineDisciplineMath.Ranks(men, files));
    }

    [Fact]
    public void The_ratio_sits_after_the_line_discipline_ones_on_the_Advanced_page_live_0_to_5_and_2_by_default()
    {
        var all = SettingsSchema.All.ToList();
        var p = SettingsSchema.LineMinWidthToDepthRatio;
        Assert.Equal(all.IndexOf(SettingsSchema.LineDisciplinePollSeconds) + 1, all.IndexOf(p));
        Assert.Same(SettingsSchema.LineDisciplineGroup, p.Group);
        Assert.Equal(SettingsPage.Advanced, p.Group.Page);
        Assert.DoesNotContain(p, SettingsSchema.PlayerPage);
        Assert.Equal(ApplyTiming.Live, p.Timing);
        Assert.Equal(0, p.Min, 9);
        Assert.Equal(5, p.Max, 9);
        Assert.Equal(2, p.Default, 9);

        var s = new TraxSettings();
        Assert.Equal(2, LineDisciplineRules.From(s).WidthRatio, 9);
        s.Set(p, 3.5, SettingSources.Mcm);
        Assert.Equal(3.5, LineDisciplineRules.From(s).WidthRatio, 6);
        Assert.Equal(0, new LineDisciplineRules(true, true, true, widthRatio: -1).WidthRatio, 9);
        Assert.Equal(5, new LineDisciplineRules(true, true, true, widthRatio: 9).WidthRatio, 9);
        Assert.Equal(0, new LineDisciplineRules(true, true, true, widthRatio: double.NaN).WidthRatio, 9);
        Assert.Equal(0, new LineDisciplineRules(true, true, true).WidthRatio, 9); // the old constructor: step 73 as before
        Assert.False(ShippedRules(2).Same(ShippedRules(3)));                     // a live change is seen
        Assert.Contains("a small unit's line at least 2.0 times as wide as deep", ShippedRules().Describe());
        Assert.Contains("no small-unit width cap (step 83: LineMinWidthToDepthRatio 0)", ShippedRules(0).Describe());
    }

    [Theory]
    [InlineData(20, 1, 3, 7, 3)]   // Toni's recruits: 3 ranks of 7 (the last 6)
    [InlineData(50, 1, 5, 10, 5)]  // 5 ranks of 10
    [InlineData(100, 1, 6, 17, 6)] // the tier's own 6
    [InlineData(8, 0, 2, 4, 2)]    // 8 looters: 2 ranks of 4
    public void Tonis_table_on_the_shipped_map(int men, int tier, double depth, int files, int ranks)
    {
        var r = ShippedRules();
        var shape = Shape(in r, tier, men);
        Assert.Equal(depth, shape.Depth, 9);
        Assert.Equal(files, shape.Files);
        Assert.Equal(ranks, shape.Ranks);
        Assert.True(shape.Files >= 2 * shape.Ranks, men + " men: " + shape.Files + " x " + shape.Ranks);
    }

    [Fact]
    public void The_ratio_0_is_step_73_the_tiers_depth_untouched()
    {
        var off = ShippedRules(0);
        var shape = Shape(in off, 1, 20);
        Assert.Equal(6, shape.Depth, 9);
        Assert.Equal(4, shape.Files); // ceil(20 / 6) = 4 = the game's √20: the column Toni saw
        Assert.Equal(5, shape.Ranks);
        for (int men = 1; men <= 200; men++)
        for (int t = 0; t <= LineDisciplineMath.MaxTier; t++)
            Assert.Equal(off.RanksOf(t), LineDisciplineMath.HeldDepth(off.RanksOf(t), men, 0), 9);
        Assert.Equal(3.83, LineDisciplineMath.HeldDepth(3.83, 120, 0), 9);
        Assert.Equal(1, LineDisciplineMath.HeldDepth(double.NaN, 20, 2), 9); // never below one rank
        Assert.Equal(6, LineDisciplineMath.HeldDepth(6, 0, 2), 9);           // nobody: nothing to cap
    }

    [Fact]
    public void A_unit_shrinking_from_100_to_20_follows_the_cap_down_and_stays_wide()
    {
        var r = ShippedRules();
        for (int men = 100; men >= 20; men--)
        {
            var shape = Shape(in r, 1, men);
            Assert.True(shape.Files >= 2 * shape.Ranks, men + " men: " + shape.Files + " x " + shape.Ranks);
            Assert.True(shape.Depth <= 6 + 1e-9);
        }
        Assert.Equal(17, Shape(in r, 1, 100).Files);
        Assert.Equal(10, Shape(in r, 1, 50).Files);
        Assert.Equal(7, Shape(in r, 1, 20).Files);
    }

    [Fact]
    public void Every_count_1_to_200_at_every_tier_is_at_least_twice_as_wide_as_deep_the_remainder_too()
    {
        var r = ShippedRules();
        for (int t = 0; t <= LineDisciplineMath.MaxTier; t++)
        for (int men = 1; men <= 200; men++)
        {
            var shape = Shape(in r, t, men);
            Assert.True(shape.Files >= 2 * shape.Ranks || shape.Ranks == 1, "T" + t + " " + men + " men: " + shape.Files + " files x " + shape.Ranks + " ranks");
            Assert.True(shape.Depth <= r.RanksOf(t) + 1e-9, "T" + t + " " + men + " men: deeper than the tier");
            Assert.True(shape.Files * shape.Ranks >= men && (shape.Ranks - 1) * shape.Files < men, "T" + t + " " + men + " men: the ranks do not hold them");
        }
        // any ratio and a mixed unit's fractional depth too
        foreach (double ratio in new[] { 0.5, 1, 1.5, 2.5, 3, 5 })
        for (double d = 1; d <= 12; d += 0.25)
        for (int men = 1; men <= 200; men++)
        {
            double held = LineDisciplineMath.HeldDepth(d, men, ratio);
            int files = LineDisciplineMath.CapFiles(men, held);
            int ranks = LineDisciplineMath.Ranks(men, files);
            Assert.True(files >= ratio * ranks || ranks == 1, "ratio " + ratio + ", D " + d + ", " + men + " men: " + files + " x " + ranks);
        }
    }
}

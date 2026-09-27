using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

public class SpreadStatsTests
{
    [Fact]
    public void Mean_and_population_std_of_a_textbook_set()
    {
        var s = new MeanStd();
        foreach (var x in new double[] { 2, 4, 4, 4, 5, 5, 7, 9 }) s.Add(x);
        Assert.Equal(8, s.Count);
        Assert.Equal(5.0, s.Mean, 12);
        Assert.Equal(4.0, s.Variance, 12);
        Assert.Equal(2.0, s.StdDev, 12);
    }

    [Fact]
    public void Empty_is_NaN_and_one_value_has_no_spread()
    {
        var s = new MeanStd();
        Assert.Equal(0, s.Count);
        Assert.True(double.IsNaN(s.Mean));
        Assert.True(double.IsNaN(s.StdDev));
        s.Add(0.7);
        Assert.Equal(0.7, s.Mean, 12);
        Assert.Equal(0.0, s.StdDev);
        s.Clear();
        Assert.Equal(0, s.Count);
        Assert.True(double.IsNaN(s.Mean));
    }

    [Fact]
    public void Stable_far_from_zero()
    {
        var s = new MeanStd();
        foreach (var x in new double[] { 2, 4, 4, 4, 5, 5, 7, 9 }) s.Add(1e9 + x);
        Assert.Equal(1e9 + 5, s.Mean, 6);
        Assert.Equal(2.0, s.StdDev, 6);
    }

    [Fact]
    public void Matches_the_naive_two_pass_formula_on_random_athletics_values()
    {
        var rng = new Random(7);
        var values = Enumerable.Range(0, 1000).Select(_ => rng.NextDouble() * 100).ToArray();
        var s = new MeanStd();
        foreach (var v in values) s.Add(v);
        double mean = values.Average();
        double std = Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Length);
        Assert.Equal(mean, s.Mean, 9);
        Assert.Equal(std, s.StdDev, 9);
    }

    [Fact]
    public void Formation_stats_band_is_clamped_and_described_in_points()
    {
        var points = new MeanStd();
        var fractions = new MeanStd();
        foreach (var p in new double[] { 64, 72, 80 })
        {
            points.Add(p);
            fractions.Add(p / 100);
        }
        var f = FormationAthleticsStats.From(points, fractions, exhausted: 0);
        Assert.Equal(3, f.Count);
        Assert.Equal(72.0, f.MeanPoints, 9);
        Assert.Equal(Math.Sqrt(128.0 / 3), f.StdPoints, 9);
        Assert.Equal(0.72, f.MeanFraction, 9);
        Assert.Equal("72 ± 7 (3 men)", f.Describe());
        Assert.Equal(0.72 - 2 * f.StdFraction, f.LowFraction(2), 9);
        Assert.Equal(0.0, f.LowFraction(20));
        Assert.Equal(1.0, f.HighFraction(20));

        var one = FormationAthleticsStats.From(Single(0), Single(0), exhausted: 1);
        Assert.Equal("0 ± 0 (1 man, 1 exhausted)", one.Describe());
        Assert.Equal("empty", default(FormationAthleticsStats).Describe());
    }

    private static MeanStd Single(double v)
    {
        var s = new MeanStd();
        s.Add(v);
        return s;
    }

    [Fact]
    public void Interval_stats_count_mean_median_and_leave_out_long_pauses()
    {
        var s = new IntervalStats();
        Assert.Equal("no samples", s.Describe());
        foreach (var v in new[] { 1.025, 1.275, 1.325, 1.325, 5.05 }) s.Add(v);
        s.Add(45);   // a pause, not a rhythm
        s.Add(-1);   // nonsense, ignored
        Assert.Equal(5, s.Count);
        Assert.Equal(1, s.OverCap);
        Assert.Equal(2.0, s.Mean, 9);
        Assert.Equal(1.325, s.Median, 9);   // bin centre of [1.30, 1.35)
        Assert.Equal(1.025, s.Min, 9);
        Assert.Equal(5.05, s.Max, 9);
        Assert.Equal("median 1.33 s, avg 2.00 s (n 5)", s.Describe());
        s.Clear();
        Assert.Equal(0, s.Count);
        Assert.Equal(0, s.OverCap);
    }

    [Fact]
    public void The_speed_verdict_says_plainly_whether_exhausted_attacks_were_slower()
    {
        var fresh = Fill(1.2, 20);
        Assert.Contains("not enough samples", SpeedVerdict.Describe(fresh, Fill(6.0, 2), 20));

        string slower = SpeedVerdict.Describe(fresh, Fill(6.0, 10), 20);
        Assert.Contains("(asked x5.00) - exhausted attacks ARE slower", slower);
        Assert.Contains("→ x4.92", slower); // bin centres: 6.025 / 1.225

        string clamped = SpeedVerdict.Describe(fresh, Fill(1.3, 10), 20);
        Assert.Contains("exhausted attacks are NOT clearly slower", clamped);
        Assert.Contains("RESEARCH UNVERIFIED #1", clamped);

        Assert.Contains("no verdict", SpeedVerdict.Describe(fresh, Fill(1.3, 10), 80));
    }

    private static IntervalStats Fill(double centre, int n)
    {
        var s = new IntervalStats();
        for (int i = 0; i < n; i++) s.Add(centre + 0.01);
        return s;
    }

    // ------------------------------------------------------------------ step 5c: the checks binned by f

    [Fact]
    public void Formation_stats_carry_the_mens_average_f()
    {
        var shares = new MeanStd();
        shares.Add(1.0);
        shares.Add(0.5);
        var f = FormationAthleticsStats.From(Single(60), Single(0.6), shares, exhausted: 0, inPeak: 1);
        Assert.Equal(0.75, f.MeanPeakShare, 9);
        Assert.Equal(1, f.InPeak);
        Assert.True(double.IsNaN(FormationAthleticsStats.From(Single(60), Single(0.6), 0).MeanPeakShare));
    }

    [Fact]
    public void Binned_intervals_compare_each_f_bin_with_the_peak_and_judge_the_tiredest()
    {
        var b = new BinnedIntervals();
        Assert.StartsWith("peak (f 1) no samples - not enough samples", b.Describe());
        for (int i = 0; i < 20; i++) b.Add(0, 1.21, 1f);
        for (int i = 0; i < 10; i++) b.Add(1, 1.41, 0.85f);
        for (int i = 0; i < 10; i++) b.Add(2, 2.41, 0.5f);
        Assert.EndsWith(" - tired attacks ARE slower (f below 0.5 vs the peak)", b.Describe()); // nobody empty yet: the next tiredest bin
        for (int i = 0; i < 10; i++) b.Add(3, 6.01, 0.2f);
        b.Add(3, 45, 0.2f);                                  // a pause: left out, asked not counted
        b.Mixed = 4;
        string line = b.Describe();
        Assert.StartsWith("peak (f 1) median ", line);
        Assert.Contains(" s (n 20) | f 0.5-1 median ", line);
        Assert.Contains(" x1.16 (asked x1.18, n 10) | f below 0.5 median ", line);   // 1.425 / 1.225 (bin centres)
        Assert.Contains(" x1.98 (asked x2.00, n 10) | empty (f 0) median ", line);
        Assert.Contains(" x4.92 (asked x5.00, n 10)", line);
        Assert.EndsWith(" - tired attacks ARE slower (empty (f 0) vs the peak)", line);
        Assert.Equal(0.2, b.AskedMean(3), 6);
        Assert.Equal(50, b.Count);

        var clamped = new BinnedIntervals();
        for (int i = 0; i < 20; i++) clamped.Add(0, 1.21, 1f);
        for (int i = 0; i < 10; i++) clamped.Add(2, 1.31, 0.4f);
        Assert.EndsWith("tired attacks are NOT clearly slower (f below 0.5 vs the peak): the engine may clamp the multiplier (RESEARCH UNVERIFIED #1) - tell Claude", clamped.Describe());

        var mild = new BinnedIntervals();
        for (int i = 0; i < 20; i++) mild.Add(0, 1.21, 1f);
        for (int i = 0; i < 10; i++) mild.Add(3, 1.31, 0.8f);
        Assert.EndsWith("the setting asks for less than x1.5 (empty (f 0) vs the peak), no verdict", mild.Describe());
    }

    [Fact]
    public void Run_speed_check_says_whether_the_engines_top_speed_follows_the_curve()
    {
        var c = new RunSpeedCheck();
        Assert.Equal("no samples", c.Describe(curveApplies: true));
        for (int i = 0; i < 30; i++) c.Add(0, 1.0, 1.0, i < 27 ? 0.3 : 0.98);
        for (int i = 0; i < 30; i++) c.Add(1, 0.79, 0.8, 0.5);
        for (int i = 0; i < 30; i++) c.Add(3, 0.31, 0.3, 0.29);
        Assert.Equal(0.98, c.ActualMax(0), 9);
        Assert.Equal(0.35, c.ActualP90(0), 9);              // 27 of 30 in [0.30, 0.35): 90% moved at or below x0.35
        string line = c.Describe(curveApplies: true);
        Assert.StartsWith("peak (f 1) engine top x1.00 asked x1.00, moving p90 x0.35 max x0.98 (n 30) | f 0.5-1 engine top x0.79 asked x0.80", line);
        Assert.EndsWith(" - the engine's top speed follows the curve", line);

        var stuck = new RunSpeedCheck();
        for (int i = 0; i < 30; i++) stuck.Add(3, 1.0, 0.3, 0.9);
        Assert.Contains("the engine's top speed does NOT follow the asked curve (empty (f 0): x1.00 vs asked x0.30)", stuck.Describe(true));

        var few = new RunSpeedCheck();
        for (int i = 0; i < 5; i++) few.Add(3, 0.3, 0.3, 0.3);
        Assert.EndsWith("not enough tired samples to judge (need 20 in a bin below the peak)", few.Describe(true));

        var horses = new RunSpeedCheck();
        for (int i = 0; i < 25; i++) horses.Add(3, 1.0, 1.0, 0.8);
        Assert.EndsWith(" - unaffected, as asked", horses.Describe(curveApplies: false));
        var slowed = new RunSpeedCheck();
        for (int i = 0; i < 25; i++) slowed.Add(3, 0.6, 1.0, 0.5);
        Assert.Contains("slowed although nothing was asked", slowed.Describe(curveApplies: false));
    }
}

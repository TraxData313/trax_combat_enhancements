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
}

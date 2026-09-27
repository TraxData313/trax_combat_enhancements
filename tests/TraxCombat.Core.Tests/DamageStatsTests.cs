using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

public class DamageStatsTests
{
    private static RollOutcome Roll(float before, float factor, double position)
        => new(before, DamageRoll.Apply(before, factor), factor, position);

    [Fact]
    public void Empty_mission_says_so()
    {
        var lines = new DamageStats().SummaryLines();
        Assert.Equal("damage rolls: none this mission", lines[0]);
        Assert.Contains("damage not rolled: 0 hits", lines);
        Assert.Contains("damage roll errors: none", lines);
        Assert.DoesNotContain(lines, l => l.StartsWith("damage rolls ran"));
    }

    [Fact]
    public void Counts_per_category_min_avg_max_and_damage_before_after()
    {
        var s = new DamageStats();
        s.AddRoll(DamageCategory.Melee, Roll(40f, 0.5f, 0.0));   // 40 → 20
        s.AddRoll(DamageCategory.Melee, Roll(40f, 1.5f, 0.99));  // 40 → 60
        s.AddRoll(DamageCategory.Ranged, Roll(30f, 1.0f, 0.5));  // 30 → 30
        s.AddRoll(DamageCategory.Mount, Roll(10f, 1.2f, 0.7));   // 10 → 12
        s.AddRoll(DamageCategory.Shield, Roll(5f, 0.8f, 0.3));   // 5 → 4

        Assert.Equal(5, s.Rolls);
        Assert.Equal(2, s.RollsIn(DamageCategory.Melee));
        s.Factors(out float min, out double mean, out float max);
        Assert.Equal(0.5f, min);
        Assert.Equal(1.5f, max);
        Assert.Equal((0.5 + 1.5 + 1.0 + 1.2 + 0.8) / 5, mean, 5);
        s.Damage(out long before, out long after);
        Assert.Equal(125, before);
        Assert.Equal(126, after);

        var lines = s.SummaryLines();
        Assert.Equal("damage rolls: 5 hits (melee 2, ranged 1, mounts 1, shields 1); factor min 0.50 / avg 1.000 / max 1.50; damage 125 → 126 (+0.8%)", lines[0]);
        Assert.Equal("damage by kind: melee 2 x0.50..1.50 avg 1.000 (80 → 80) | ranged 1 x1.00..1.00 avg 1.000 (30 → 30) | mounts 1 x1.20..1.20 avg 1.200 (10 → 12) | shields 1 x0.80..0.80 avg 0.800 (5 → 4)", lines[1]);
        Assert.StartsWith("damage dice, 10 equal slices from the lowest to the highest possible roll (even = fair): 1 0 0 1 0 1 0 1 0 1", lines[2]);
        Assert.Contains("damage rolls ran on the main thread: all 5", lines);
    }

    [Fact]
    public void Skips_are_counted_by_reason_in_plain_words()
    {
        var s = new DamageStats();
        for (int i = 0; i < 3; i++) s.AddSkip(DamageSkipReason.ShieldToggleOff);
        s.AddSkip(DamageSkipReason.FallDamage);
        s.AddSkip(DamageSkipReason.RangedToggleOff);
        s.AddSkip(DamageSkipReason.None); // not a skip - ignored
        Assert.Equal(5, s.SkipsTotal);
        Assert.Equal(3, s.Skips(DamageSkipReason.ShieldToggleOff));
        Assert.Contains("damage not rolled: 5 hits - fall damage 1, shield blocks (DamageRandomOnShields off) 3, ranged (DamageRandomRanged off) 1", s.SummaryLines());
    }

    [Fact]
    public void Every_skip_reason_has_a_plain_name()
    {
        foreach (DamageSkipReason r in Enum.GetValues(typeof(DamageSkipReason)))
        {
            if (r == DamageSkipReason.None) continue;
            Assert.NotEqual(r.ToString(), DamageStats.SkipName(r));
        }
    }

    [Fact]
    public void First_error_per_site_is_reported_once_the_rest_counted()
    {
        var s = new DamageStats();
        Assert.True(s.AddError("damage.roll"));
        Assert.False(s.AddError("damage.roll"));
        Assert.False(s.AddError("damage.roll"));
        Assert.True(s.AddError("damage.log"));
        Assert.Equal(4, s.Errors);
        Assert.Contains(s.SummaryLines(), l => l.StartsWith("damage roll errors: 4 (damage.roll 3, damage.log 1)"));
        s.Reset(); // next mission: the first error is reported again
        Assert.True(s.AddError("damage.roll"));
    }

    [Fact]
    public void Off_main_thread_rolls_are_called_out()
    {
        var s = new DamageStats();
        s.AddRoll(DamageCategory.Melee, Roll(10f, 1f, 0.5), offMainThread: true);
        s.AddRoll(DamageCategory.Melee, Roll(10f, 1f, 0.5));
        Assert.Equal(1, s.OffMainThread);
        Assert.Contains(s.SummaryLines(), l => l.StartsWith("damage rolls OFF the main thread: 1 of 2"));
    }

    [Fact]
    public void Reset_clears_everything()
    {
        var s = new DamageStats();
        s.AddRoll(DamageCategory.Ranged, Roll(10f, 1.1f, 0.6), true);
        s.AddSkip(DamageSkipReason.FallDamage);
        s.AddError("damage.roll");
        s.Reset();
        Assert.Equal(0, s.Rolls);
        Assert.Equal(0, s.SkipsTotal);
        Assert.Equal(0, s.Errors);
        Assert.Equal(0, s.OffMainThread);
        Assert.All(s.Histogram(), b => Assert.Equal(0, b));
        s.Factors(out float min, out double mean, out _);
        Assert.True(float.IsNaN(min) && double.IsNaN(mean));
    }

    [Fact]
    public void Histogram_edges_land_in_the_first_and_last_slice()
    {
        var s = new DamageStats();
        s.AddRoll(DamageCategory.Melee, Roll(10f, 0.5f, 0.0));
        s.AddRoll(DamageCategory.Melee, Roll(10f, 1.5f, 0.9999999));
        s.AddRoll(DamageCategory.Melee, Roll(10f, 1.5f, 1.0));  // defensive: clamped into the last slice
        var h = s.Histogram();
        Assert.Equal(1, h[0]);
        Assert.Equal(2, h[DamageStats.Bins - 1]);
    }

    [Fact]
    public void Thread_safe_under_parallel_hits()
    {
        var s = new DamageStats();
        var rng = ThreadSafeRandom.Shared;
        Parallel.For(0, 8, _ =>
        {
            for (int i = 0; i < 10_000; i++)
            {
                var r = DamageRoll.Roll(20f, 50, rng);
                s.AddRoll((DamageCategory)(i % 4), r);
                s.AddSkip(DamageSkipReason.FallDamage);
            }
        });
        Assert.Equal(80_000, s.Rolls);
        Assert.Equal(80_000, s.SkipsTotal);
        Assert.Equal(80_000, s.Histogram().Sum());
        s.Factors(out float min, out double mean, out float max);
        Assert.InRange(min, 0.5f, 0.51f);
        Assert.InRange(max, 1.49f, 1.5f);
        Assert.InRange(mean, 0.99, 1.01);
    }
}

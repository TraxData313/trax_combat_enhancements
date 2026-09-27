using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

public class DamageRollTests
{
    /// <summary>Hands out the given draws in order, then repeats the last.</summary>
    private sealed class FixedRandom : IRandomSource
    {
        private readonly double[] _values;
        private int _next;

        public FixedRandom(params double[] values) => _values = values;

        public double NextDouble() => _values[Math.Min(_next++, _values.Length - 1)];
    }

    private static readonly DamageRules Defaults = DamageRules.From(new TraxSettings());

    private static HitFacts Melee() => new(victimIsAgent: true, victimMissing: false, fall: false, shieldBlocked: false, missile: false, victimIsMount: false, horseCharge: false);
    private static HitFacts Arrow() => new(true, false, false, false, missile: true, false, false);
    private static HitFacts OnHorse(bool missile = false) => new(true, false, false, false, missile, victimIsMount: true, false);
    private static HitFacts Shield(bool missile = false) => new(true, false, false, shieldBlocked: true, missile, false, false);
    private static HitFacts Charge() => new(true, false, false, false, false, false, horseCharge: true);
    private static HitFacts Fall() => new(true, false, fall: true, false, false, false, false);
    private static HitFacts Door() => new(victimIsAgent: false, victimMissing: true, false, false, false, false, false);

    private static DamageRules Rules(bool enabled = true, int percent = 50, bool melee = true, bool ranged = true, bool mounts = true, bool shields = false)
        => new(enabled, percent, melee, ranged, mounts, shields);

    // ------------------------------------------------------------------ the factor and the rounding rule

    [Theory]
    [InlineData(0.0, 0.5, 0.5)]    // lowest draw → 1 - p
    [InlineData(0.5, 0.5, 1.0)]    // middle → 1
    [InlineData(0.25, 0.5, 0.75)]
    [InlineData(0.999999, 0.5, 1.5)] // highest draw → just under 1 + p
    [InlineData(0.3, 0.0, 1.0)]    // no spread
    [InlineData(0.0, 1.0, 0.0)]    // p = 100 %: can reach 0 (then the floor of 1 applies)
    public void Factor_maps_a_uniform_draw_onto_one_minus_p_to_one_plus_p(double u, double p, double expected)
    {
        Assert.Equal(expected, DamageRoll.Factor(u, p), 4);
    }

    [Fact]
    public void Design_example_a_50_damage_hit_lands_for_25_to_75()
    {
        Assert.Equal(25f, DamageRoll.Roll(50f, 50, new FixedRandom(0.0)).After, 3);
        Assert.Equal(50f, DamageRoll.Roll(50f, 50, new FixedRandom(0.5)).After, 3);
        Assert.Equal(75f, DamageRoll.Roll(50f, 50, new FixedRandom(0.9999999)).After, 3);
    }

    [Fact]
    public void A_zero_hit_stays_zero_and_a_positive_hit_never_lands_below_one()
    {
        Assert.Equal(0f, DamageRoll.Apply(0f, 1.4f));
        Assert.Equal(1f, DamageRoll.Apply(1f, 0.5f));      // 0.5 would round to 0 → floor 1
        Assert.Equal(1f, DamageRoll.Apply(2f, 0.01f));
        Assert.Equal(1f, DamageRoll.Apply(0.6f, 0.5f));    // the game shows 0.6 as 1 → still at least 1
        Assert.Equal(0.4f, DamageRoll.Apply(0.4f, 1.5f));  // the game shows 0.4 as 0 → left exactly as it was
        Assert.Equal(-3f, DamageRoll.Apply(-3f, 1.5f));    // never makes a negative worse
        Assert.Equal(30f, DamageRoll.Apply(20f, 1.5f), 3);
    }

    [Fact]
    public void Game_rounding_is_bankers_like_TaleWorlds_MathF_Round()
    {
        Assert.Equal(0, DamageRoll.GameRound(0.5f));
        Assert.Equal(1, DamageRoll.GameRound(0.51f));
        Assert.Equal(2, DamageRoll.GameRound(1.5f));
        Assert.Equal(2, DamageRoll.GameRound(2.5f));
        Assert.Equal(41, DamageRoll.GameRound(40.6f));
    }

    [Fact]
    public void A_broken_random_source_cannot_push_the_factor_out_of_range()
    {
        Assert.Equal(0.5f, DamageRoll.Roll(10f, 50, new FixedRandom(double.NaN)).Factor, 4);
        Assert.Equal(0.5f, DamageRoll.Roll(10f, 50, new FixedRandom(-3)).Factor, 4);
        Assert.InRange(DamageRoll.Roll(10f, 50, new FixedRandom(7)).Factor, 0.5f, 1.5f);
    }

    [Theory]
    [InlineData(-5, 0.0)]
    [InlineData(0, 0.0)]
    [InlineData(50, 0.5)]
    [InlineData(100, 1.0)]
    [InlineData(250, 1.0)]
    public void Spread_is_percent_over_100_clamped(int percent, double expected)
    {
        Assert.Equal(expected, DamageRoll.Spread(percent), 9);
    }

    [Fact]
    public void Outcome_reports_the_numbers_the_player_sees()
    {
        var r = DamageRoll.Roll(33.6f, 50, new FixedRandom(0.75)); // factor 1.25 → 42.0
        Assert.Equal(34, r.BeforeRounded);
        Assert.Equal(42, r.AfterRounded);
        Assert.Equal(1.25f, r.Factor, 4);
        Assert.Equal(0.75, r.Position, 9);
    }

    // ------------------------------------------------------------------ distribution (the dice are fair)

    [Fact]
    public void Distribution_bounds_are_never_exceeded_and_the_mean_is_one()
    {
        var rng = new SeededRandom(20260927);
        const int n = 200_000;
        double sum = 0, sumDamage = 0;
        float min = float.MaxValue, max = float.MinValue;
        var bins = new int[10];
        for (int i = 0; i < n; i++)
        {
            var r = DamageRoll.Roll(50f, 50, rng);
            Assert.InRange(r.Factor, 0.5f, 1.5f);
            Assert.InRange(r.After, 25f, 75f);
            sum += r.Factor;
            sumDamage += r.After;
            if (r.Factor < min) min = r.Factor;
            if (r.Factor > max) max = r.Factor;
            bins[Math.Min(9, (int)(r.Position * 10))]++;
        }
        Assert.InRange(sum / n, 0.997, 1.003);
        Assert.InRange(sumDamage / n, 49.85, 50.15);
        Assert.True(min < 0.501f && max > 1.499f, "the whole range is reached: " + min + ".." + max);
        foreach (int b in bins) Assert.InRange((double)b, n / 10 * 0.95, n / 10 * 1.05); // flat within 5 %
    }

    [Theory]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(75)]
    [InlineData(100)]
    public void Every_spread_stays_in_its_own_bounds(int percent)
    {
        var rng = new SeededRandom(percent);
        double p = percent / 100.0;
        for (int i = 0; i < 20_000; i++)
        {
            var r = DamageRoll.Roll(80f, percent, rng);
            Assert.InRange(r.Factor, (float)(1 - p) - 1e-5f, (float)(1 + p) + 1e-5f);
            Assert.True(r.After >= 1f, "a positive hit fell below 1");
        }
    }

    [Fact]
    public void Thread_safe_random_stays_in_range_from_many_threads()
    {
        var bad = 0;
        Parallel.For(0, 8, _ =>
        {
            for (int i = 0; i < 50_000; i++)
            {
                double u = ThreadSafeRandom.Shared.NextDouble();
                if (!(u >= 0 && u < 1)) Interlocked.Increment(ref bad);
            }
        });
        Assert.Equal(0, bad);
        // and it is not stuck (a System.Random shared across threads can collapse to 0)
        var seen = new HashSet<double>();
        for (int i = 0; i < 100; i++) seen.Add(ThreadSafeRandom.Shared.NextDouble());
        Assert.True(seen.Count > 90);
    }

    [Fact]
    public void Seeded_random_repeats()
    {
        var a = new SeededRandom(7);
        var b = new SeededRandom(7);
        for (int i = 0; i < 10; i++) Assert.Equal(a.NextDouble(), b.NextDouble());
    }

    // ------------------------------------------------------------------ the skip rules

    [Fact]
    public void Defaults_roll_melee_ranged_mounts_and_charges_but_not_shields()
    {
        Assert.Equal(DamageSkipReason.None, DamageRoll.Decide(Melee(), 30, Defaults));
        Assert.Equal(DamageSkipReason.None, DamageRoll.Decide(Arrow(), 30, Defaults));
        Assert.Equal(DamageSkipReason.None, DamageRoll.Decide(OnHorse(), 30, Defaults));
        Assert.Equal(DamageSkipReason.None, DamageRoll.Decide(OnHorse(missile: true), 30, Defaults));
        Assert.Equal(DamageSkipReason.None, DamageRoll.Decide(Charge(), 30, Defaults));
        Assert.Equal(DamageSkipReason.ShieldToggleOff, DamageRoll.Decide(Shield(), 30, Defaults));
        Assert.Equal(DamageSkipReason.ShieldToggleOff, DamageRoll.Decide(Shield(missile: true), 30, Defaults));
    }

    [Fact]
    public void Never_rolled_fall_objects_missing_victim_and_zero()
    {
        var all = Rules(shields: true);
        Assert.Equal(DamageSkipReason.FallDamage, DamageRoll.Decide(Fall(), 30, all));
        Assert.Equal(DamageSkipReason.NotAnAgent, DamageRoll.Decide(Door(), 30, all));
        Assert.Equal(DamageSkipReason.NoVictim, DamageRoll.Decide(new HitFacts(true, true, false, false, false, false, false), 30, all));
        Assert.Equal(DamageSkipReason.ZeroDamage, DamageRoll.Decide(Melee(), 0f, all));
        Assert.Equal(DamageSkipReason.ZeroDamage, DamageRoll.Decide(Melee(), 0.5f, all)); // the game shows it as 0
        Assert.Equal(DamageSkipReason.None, DamageRoll.Decide(Melee(), 0.51f, all));
    }

    [Fact]
    public void Switches_each_skip_their_own_kind()
    {
        Assert.Equal(DamageSkipReason.SwitchedOff, DamageRoll.Decide(Melee(), 30, Rules(enabled: false)));
        Assert.Equal(DamageSkipReason.SpreadZero, DamageRoll.Decide(Melee(), 30, Rules(percent: 0)));

        var noMelee = Rules(melee: false);
        Assert.Equal(DamageSkipReason.MeleeToggleOff, DamageRoll.Decide(Melee(), 30, noMelee));
        Assert.Equal(DamageSkipReason.MeleeToggleOff, DamageRoll.Decide(Charge(), 30, noMelee)); // a charge bump is melee
        Assert.Equal(DamageSkipReason.None, DamageRoll.Decide(Arrow(), 30, noMelee));

        var noRanged = Rules(ranged: false);
        Assert.Equal(DamageSkipReason.RangedToggleOff, DamageRoll.Decide(Arrow(), 30, noRanged));
        Assert.Equal(DamageSkipReason.None, DamageRoll.Decide(Melee(), 30, noRanged));

        var noMounts = Rules(mounts: false);
        Assert.Equal(DamageSkipReason.MountToggleOff, DamageRoll.Decide(OnHorse(), 30, noMounts));
        Assert.Equal(DamageSkipReason.MountToggleOff, DamageRoll.Decide(OnHorse(missile: true), 30, noMounts));
        Assert.Equal(DamageSkipReason.None, DamageRoll.Decide(Melee(), 30, noMounts));

        var shields = Rules(shields: true);
        Assert.Equal(DamageSkipReason.None, DamageRoll.Decide(Shield(), 30, shields));
    }

    [Fact]
    public void Target_and_attack_toggles_combine()
    {
        // An arrow into a horse needs both "ranged" and "on mounts"; a sword on a shield needs "melee" and "shields".
        Assert.Equal(DamageSkipReason.RangedToggleOff, DamageRoll.Decide(OnHorse(missile: true), 30, Rules(ranged: false)));
        Assert.Equal(DamageSkipReason.MeleeToggleOff, DamageRoll.Decide(Shield(), 30, Rules(melee: false, shields: true)));
        Assert.Equal(DamageSkipReason.RangedToggleOff, DamageRoll.Decide(Shield(missile: true), 30, Rules(ranged: false, shields: true)));
    }

    [Fact]
    public void Structural_rules_win_over_switches()
    {
        Assert.Equal(DamageSkipReason.FallDamage, DamageRoll.Decide(Fall(), 30, Rules(enabled: false)));
        Assert.Equal(DamageSkipReason.NotAnAgent, DamageRoll.Decide(Door(), 30, Rules(percent: 0)));
    }

    [Fact]
    public void Category_is_shield_then_mount_then_ranged_then_melee()
    {
        Assert.Equal(DamageCategory.Melee, Melee().Category);
        Assert.Equal(DamageCategory.Melee, Charge().Category);
        Assert.Equal(DamageCategory.Ranged, Arrow().Category);
        Assert.Equal(DamageCategory.Mount, OnHorse(missile: true).Category);
        Assert.Equal(DamageCategory.Shield, Shield(missile: true).Category);
    }

    [Fact]
    public void Rules_are_read_live_from_the_settings()
    {
        var s = new TraxSettings();
        Assert.Equal(DamageSkipReason.None, DamageRoll.Decide(Arrow(), 30, DamageRules.From(s)));
        s.Set(SettingsSchema.DamageRandomRanged, false, SettingSources.Mcm);          // mid-battle MCM change
        Assert.Equal(DamageSkipReason.RangedToggleOff, DamageRoll.Decide(Arrow(), 30, DamageRules.From(s)));
        s.Set(SettingsSchema.DamageRandomPercent, 20, SettingSources.Mcm);
        var rules = DamageRules.From(s);
        Assert.Equal(20, rules.Percent);
        Assert.InRange(DamageRoll.Roll(100f, rules.Percent, new SeededRandom(1)).After, 80f, 120f);
    }

    // ------------------------------------------------------------------ the upside follows the attacker's Athletics (step 5c)

    [Theory]
    [InlineData(0.0, 1.0, 0.5)]        // lowest draw: 1 - p, whatever the attacker's f
    [InlineData(0.0, 0.0, 0.5)]
    [InlineData(0.999999, 1.0, 1.5)]   // fresh: up to 1 + p
    [InlineData(0.999999, 0.5, 1.25)]  // halfway down: up to 1 + p/2 ("+25%")
    [InlineData(0.999999, 0.0, 1.0)]   // empty: never above normal
    [InlineData(0.5, 0.0, 0.75)]       // empty: uniform over [0.5, 1)
    [InlineData(0.5, 2.0, 1.0)]        // a share above 1 counts as 1
    [InlineData(0.999999, -1.0, 1.0)]  // below 0 counts as 0
    public void The_upside_shrinks_with_the_attackers_f_and_the_downside_never_changes(double u, double upside, double expected)
    {
        Assert.Equal(expected, DamageRoll.Factor(u, 0.5, upside), 4);
    }

    [Fact]
    public void Upside_is_the_attackers_f_only_while_the_setting_is_on_and_he_has_a_pool()
    {
        var on = new DamageRules(true, 50, true, true, true, false, modEnabled: true, upsideFollowsAthletics: true);
        var off = new DamageRules(true, 50, true, true, true, false, modEnabled: true, upsideFollowsAthletics: false);
        Assert.Equal(0.4, DamageRoll.Upside(on, 0.4), 9);
        Assert.Equal(1.0, DamageRoll.Upside(on, 1.0));
        Assert.Equal(0.0, DamageRoll.Upside(on, 0.0));
        Assert.Equal(1.0, DamageRoll.Upside(on, double.NaN));   // no tracked attacker: the full upside
        Assert.Equal(1.0, DamageRoll.Upside(off, 0.0));         // setting off: the full upside
        Assert.True(DamageRules.From(new TraxSettings()).UpsideFollowsAthletics); // DESIGN: on
    }

    [Fact]
    public void A_roll_reports_its_ceiling_and_never_goes_above_it()
    {
        var rng = new SeededRandom(11);
        foreach (double upside in new[] { 1.0, 0.75, 0.5, 0.25, 0.0 })
        {
            float max = 0, min = float.MaxValue;
            for (int i = 0; i < 20_000; i++)
            {
                var r = DamageRoll.Roll(100f, 50, rng, upside);
                Assert.Equal(upside, r.Upside, 9);
                Assert.Equal(1 + 0.5 * upside, r.Ceiling, 9);
                Assert.True(r.Factor <= r.Ceiling + 1e-6, "factor " + r.Factor + " above " + r.Ceiling);
                if (r.Factor > max) max = r.Factor;
                if (r.Factor < min) min = r.Factor;
            }
            Assert.InRange(min, 0.5f, 0.51f);                          // the downside never changes
            Assert.InRange(max, (float)(1 + 0.5 * upside) - 0.01f, (float)(1 + 0.5 * upside));
        }
        Assert.True(double.IsNaN(new RollOutcome(10f, 10f, 1f, 0.5).Ceiling)); // p unknown
    }
}

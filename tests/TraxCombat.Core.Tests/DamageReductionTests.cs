using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>Step 25 - the overall damage reduction (DESIGN §1 "Overall damage reduction",
/// interpretation 27): × (1 − r/100) AFTER the roll, on people, horses and shields, whatever the
/// roll's switches; never falls or objects; 0 stays 0, a positive hit never below 1; the master
/// switch alone turns it off.</summary>
public class DamageReductionTests
{
    private static HitFacts Melee() => new(victimIsAgent: true, victimMissing: false, fall: false, shieldBlocked: false, missile: false, victimIsMount: false, horseCharge: false);
    private static HitFacts Arrow() => new(true, false, false, false, missile: true, false, false);
    private static HitFacts OnHorse() => new(true, false, false, false, false, victimIsMount: true, false);
    private static HitFacts Shield() => new(true, false, false, shieldBlocked: true, false, false, false);
    private static HitFacts Charge() => new(true, false, false, false, false, false, horseCharge: true);
    private static HitFacts Fall() => new(true, false, fall: true, false, false, false, false);
    private static HitFacts Door() => new(victimIsAgent: false, victimMissing: true, false, false, false, false, false);
    private static HitFacts NoVictim() => new(true, victimMissing: true, false, false, false, false, false);

    private static DamageRules Rules(int reduction = 25, bool modEnabled = true, bool rollEnabled = true, int spread = 50,
        bool melee = true, bool ranged = true, bool mounts = true, bool shields = false)
        => new(rollEnabled, spread, melee, ranged, mounts, shields, modEnabled, upsideFollowsAthletics: false, reductionPercent: reduction);

    private sealed class FixedRandom : IRandomSource
    {
        private readonly double _u;
        public FixedRandom(double u) => _u = u;
        public double NextDouble() => _u;
    }

    // ------------------------------------------------------------------ the math

    [Fact]
    public void Anton_example_100_lands_75_at_25()
    {
        Assert.Equal(75f, DamageReduction.Apply(100f, 25), 4);
        Assert.Equal(0.75, DamageReduction.Multiplier(25), 6);
        Assert.Equal("an average 100 hit lands 75", DamageReduction.Example(25));
    }

    [Theory]
    [InlineData(0, 100f)]
    [InlineData(10, 90f)]
    [InlineData(50, 50f)]
    [InlineData(90, 10f)]
    [InlineData(95, 10f)]    // past the top: clamped to 90
    [InlineData(-5, 100f)]   // below 0: nothing
    public void The_percent_is_clamped_to_0_to_90(int percent, float expected)
    {
        Assert.Equal(expected, DamageReduction.Apply(100f, percent), 3);
    }

    [Fact]
    public void Zero_is_the_games_damage_exactly()
    {
        foreach (float d in new[] { 0.3f, 1f, 1.4f, 37.77f, 500f })
            Assert.Equal(d, DamageReduction.Apply(d, 0));
    }

    [Fact]
    public void A_positive_hit_never_lands_below_1_and_0_stays_0()
    {
        Assert.Equal(1f, DamageReduction.Apply(1f, 25));       // 0.75 would round to 1 anyway - kept at least 1
        Assert.Equal(1f, DamageReduction.Apply(1f, 90));       // 0.1 would show as 0
        Assert.Equal(1f, DamageReduction.Apply(2f, 90));       // 0.2
        Assert.Equal(0.4f, DamageReduction.Apply(0.4f, 50));   // shows as 0: untouched
        Assert.Equal(0f, DamageReduction.Apply(0f, 25));
        Assert.Equal(0.5f, DamageReduction.Apply(0.5f, 25));   // banker's rounding: 0.5 shows as 0
        for (int p = 0; p <= DamageReduction.MaxPercent; p++)
            for (float d = 0.6f; d < 30f; d += 0.37f)
                Assert.True(DamageRoll.GameRound(DamageReduction.Apply(d, p)) >= 1, d + " at " + p);
    }

    [Theory]
    [InlineData(10f, 25, 8)]   // 7.5 → banker's 8
    [InlineData(14f, 25, 10)]  // 10.5 → banker's 10
    [InlineData(33f, 25, 25)]  // 24.75 → 25
    [InlineData(3f, 50, 2)]    // 1.5 → 2
    public void The_game_rounds_the_reduced_value(float damage, int percent, int shown)
    {
        Assert.Equal(shown, DamageRoll.GameRound(DamageReduction.Apply(damage, percent)));
    }

    [Fact]
    public void After_the_roll_the_average_is_exactly_three_quarters()
    {
        // A 100 hit rolls 50..150 (±50), then the reduction: 37.5..112.5, 75 in the middle.
        var low = DamageRoll.Roll(100f, 50, new FixedRandom(0.0));
        var mid = DamageRoll.Roll(100f, 50, new FixedRandom(0.5));
        Assert.Equal(37.5f, DamageReduction.Reduce(100f, low.After, 25).After, 3);
        var r = DamageReduction.Reduce(100f, mid.After, 25);
        Assert.Equal(75f, r.After, 3);
        Assert.Equal(100, r.GameRounded);
        Assert.Equal(100, r.BeforeRounded);
        Assert.Equal(75, r.AfterRounded);
        Assert.Equal(25, r.Percent);

        var rng = new SeededRandom(7);
        double sum = 0;
        const int n = 100_000;
        for (int i = 0; i < n; i++)
            sum += DamageReduction.Apply(DamageRoll.Roll(100f, 50, rng).After, 25);
        Assert.InRange(sum / n, 74.5, 75.5);
    }

    [Fact]
    public void Reduce_clamps_the_percent_it_reports()
    {
        Assert.Equal(90, DamageReduction.Reduce(100f, 100f, 200).Percent);
        Assert.Equal(10f, DamageReduction.Reduce(100f, 100f, 200).After, 3);
    }

    // ------------------------------------------------------------------ which hits

    [Fact]
    public void Every_kind_of_landed_hit_is_reduced_shields_too()
    {
        var rules = Rules();
        foreach (var hit in new[] { Melee(), Arrow(), OnHorse(), Shield(), Charge() })
            Assert.Equal(ReductionSkipReason.None, DamageReduction.Decide(in hit, 50f, in rules));
    }

    [Fact]
    public void Falls_objects_no_victim_and_0_hits_are_never_reduced()
    {
        var rules = Rules();
        var fall = Fall();
        var door = Door();
        var none = NoVictim();
        var melee = Melee();
        Assert.Equal(ReductionSkipReason.FallDamage, DamageReduction.Decide(in fall, 50f, in rules));
        Assert.Equal(ReductionSkipReason.NotAnAgent, DamageReduction.Decide(in door, 50f, in rules));
        Assert.Equal(ReductionSkipReason.NoVictim, DamageReduction.Decide(in none, 50f, in rules));
        Assert.Equal(ReductionSkipReason.ZeroDamage, DamageReduction.Decide(in melee, 0.4f, in rules));
        Assert.Equal(ReductionSkipReason.ZeroDamage, DamageReduction.Decide(in melee, 0f, in rules));
    }

    [Fact]
    public void The_setting_at_0_and_the_master_switch_leave_it_alone_master_switch_counted_last()
    {
        var melee = Melee();
        var zero = Rules(reduction: 0);
        var off = Rules(modEnabled: false);
        var offAndZero = Rules(reduction: 0, modEnabled: false);
        Assert.Equal(ReductionSkipReason.PercentZero, DamageReduction.Decide(in melee, 50f, in zero));
        Assert.Equal(ReductionSkipReason.ModOff, DamageReduction.Decide(in melee, 50f, in off));
        // an OFF battle counts exactly what an ON one would reduce
        Assert.Equal(ReductionSkipReason.PercentZero, DamageReduction.Decide(in melee, 50f, in offAndZero));
        var fall = Fall();
        Assert.Equal(ReductionSkipReason.FallDamage, DamageReduction.Decide(in fall, 50f, in off));
    }

    [Fact]
    public void The_roll_off_or_its_toggles_off_still_reduce()
    {
        var melee = Melee();
        var arrow = Arrow();
        var horse = OnHorse();
        var shield = Shield();
        foreach (var rules in new[]
                 {
                     Rules(rollEnabled: false), Rules(spread: 0), Rules(melee: false, ranged: false, mounts: false, shields: false),
                 })
        {
            Assert.NotEqual(DamageSkipReason.None, DamageRoll.Decide(in melee, 50f, in rules));
            Assert.Equal(ReductionSkipReason.None, DamageReduction.Decide(in melee, 50f, in rules));
            Assert.Equal(ReductionSkipReason.None, DamageReduction.Decide(in arrow, 50f, in rules));
            Assert.Equal(ReductionSkipReason.None, DamageReduction.Decide(in horse, 50f, in rules));
            Assert.Equal(ReductionSkipReason.None, DamageReduction.Decide(in shield, 50f, in rules));
        }
    }

    [Fact]
    public void The_rules_read_the_live_setting()
    {
        var s = new TraxSettings();
        Assert.Equal(25, DamageRules.From(s).ReductionPercent); // DESIGN's initial value
        s.Set(SettingsSchema.DamageReductionPercent, 40, SettingSources.Mcm);
        Assert.Equal(40, DamageRules.From(s).ReductionPercent);
        s.Set(SettingsSchema.DamageReductionPercent, 0, SettingSources.Mcm);
        Assert.Equal(0, DamageRules.From(s).ReductionPercent);
        Assert.Equal(DamageReduction.MaxPercent, (int)SettingsSchema.DamageReductionPercent.Max);
        Assert.Same(SettingsSchema.PlayerDamageGroup, SettingsSchema.DamageReductionPercent.Group); // the player page since step 59
    }

    // ------------------------------------------------------------------ the stats and the summary line

    [Fact]
    public void The_summary_line_proves_the_reduction()
    {
        var st = new DamageStats();
        Assert.Contains("damage reduction (DamageReductionPercent): no hit reduced this mission; not reduced: 0 hits", st.SummaryLines());

        for (int i = 0; i < 3; i++) st.AddReduction(DamageCategory.Melee, DamageReduction.Reduce(100f, 100f, 25));
        st.AddReduction(DamageCategory.Ranged, DamageReduction.Reduce(40f, 60f, 25));
        st.AddReductionSkip(ReductionSkipReason.FallDamage);
        st.AddReductionSkip(ReductionSkipReason.NotAnAgent);
        st.AddReductionSkip(ReductionSkipReason.NotAnAgent);
        st.AddReductionSkip(ReductionSkipReason.None); // ignored

        Assert.Equal(4, st.Reduced);
        Assert.Equal(3, st.ReducedIn(DamageCategory.Melee));
        Assert.Equal(2, st.ReductionSkips(ReductionSkipReason.NotAnAgent));
        st.ReductionDamage(out long game, out long before, out long landed);
        Assert.Equal((340L, 360L, 270L), (game, before, landed));
        Assert.Contains("damage reduction (DamageReductionPercent 25%, after the roll): 4 hits (melee 3, ranged 1, mounts 0, shields 0); "
            + "damage the game's 340 → after the roll 360 → landed 270 (-25.0% from the roll's, -20.6% from the game's), avg 67.5 per hit "
            + "(the game's 85.0); not reduced: objects (doors, siege engines, ships) 2, fall damage 1", st.SummaryLines());

        st.AddReduction(DamageCategory.Shield, DamageReduction.Reduce(100f, 100f, 40));
        Assert.Contains(st.SummaryLines(), l => l.StartsWith("damage reduction (DamageReductionPercent 25-40% (changed mid-battle), after the roll): 5 hits", StringComparison.Ordinal));

        st.Reset();
        Assert.Equal(0, st.Reduced);
        Assert.Equal(0, st.ReductionSkips(ReductionSkipReason.NotAnAgent));
    }

    [Fact]
    public void Every_skip_reason_has_plain_words()
    {
        foreach (ReductionSkipReason r in Enum.GetValues(typeof(ReductionSkipReason)))
        {
            if (r == ReductionSkipReason.None) continue;
            Assert.NotEqual(r.ToString(), DamageStats.ReductionSkipName(r));
        }
        Assert.Equal("mod OFF (ModEnabled)", DamageStats.ReductionSkipName(ReductionSkipReason.ModOff));
        Assert.Equal("reduction 0 (DamageReductionPercent)", DamageStats.ReductionSkipName(ReductionSkipReason.PercentZero));
    }
}

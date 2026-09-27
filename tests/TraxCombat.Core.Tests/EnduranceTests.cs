using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

public class EnduranceTests
{
    /// <summary>DESIGN's defaults, as a player without a config file has them.</summary>
    private static EnduranceRules Defaults(bool enabled = true, int max = 100, float cost = 10, bool costOnMiss = true,
        float hero = 0.75f, float leader = 0.75f, int speed = 20, int recover = 0, float delayBlows = 2, float blowTime = 1.5f,
        float standing = 60, float moving = 120, float threshold = 0.5f)
        => new(enabled, max, cost, costOnMiss, hero, leader, speed, recover, delayBlows, blowTime, standing, moving, threshold);

    private static int BlowsToEmpty(Fighter f, EnduranceRules r)
    {
        for (int i = 1; i <= 1000; i++)
        {
            var o = EnduranceMath.Charge(f, r, i);
            if (o.EnteredExhaustion) return i;
        }
        return -1;
    }

    [Fact]
    public void Rules_read_the_live_settings()
    {
        var s = new TraxSettings();
        var r = EnduranceRules.From(s);
        Assert.True(r.Enabled);
        Assert.Equal(100, r.MaxEndurance);
        Assert.Equal(10f, r.CostPerBlow);
        Assert.Equal(20, r.ExhaustedAttackSpeedPercent);
        Assert.Equal(3.0, r.RegenDelaySeconds, 6);

        s.Set(SettingsSchema.CostPerBlow, 20, SettingSources.Mcm);
        s.Set(SettingsSchema.EnduranceEnabled, false, SettingSources.Mcm);
        var after = EnduranceRules.From(s);
        Assert.Equal(20f, after.CostPerBlow);
        Assert.False(after.Enabled);
    }

    [Fact]
    public void Costs_are_10_for_a_soldier_7_5_for_a_hero_5_6_for_a_party_leader()
    {
        var r = Defaults();
        Assert.Equal(10.0, EnduranceMath.BlowCostPoints(r, new Fighter()), 6);
        Assert.Equal(7.5, EnduranceMath.BlowCostPoints(r, new Fighter { IsHero = true }), 6);
        Assert.Equal(5.625, EnduranceMath.BlowCostPoints(r, new Fighter { IsHero = true, IsLeader = true }), 6);
        // a leader flag alone (custom battle general who is not a hero - defensive) still stacks one discount
        Assert.Equal(7.5, EnduranceMath.BlowCostPoints(r, new Fighter { IsLeader = true }), 6);
    }

    [Fact]
    public void A_soldier_lasts_10_blows_a_hero_14_a_party_leader_18()
    {
        var r = Defaults();
        Assert.Equal(10, BlowsToEmpty(new Fighter(), r));
        Assert.Equal(14, BlowsToEmpty(new Fighter { IsHero = true }, r));        // 100 / 7.5 = 13.3
        Assert.Equal(18, BlowsToEmpty(new Fighter { IsHero = true, IsLeader = true }, r)); // 100 / 5.625 = 17.8 ("~18 blows")
    }

    [Fact]
    public void Ten_blows_of_ten_empty_the_pool_exactly_and_the_ninth_leaves_ten()
    {
        var r = Defaults();
        var f = new Fighter();
        for (int i = 1; i <= 9; i++) EnduranceMath.Charge(f, r, i);
        Assert.Equal(10.0, EnduranceMath.Points(r, f), 6);
        Assert.False(f.Exhausted);
        var o = EnduranceMath.Charge(f, r, 10);
        Assert.True(o.Charged);
        Assert.True(o.EnteredExhaustion);
        Assert.Equal(10.0, o.Before, 6);
        Assert.Equal(0.0, o.After);
        Assert.Equal(0.0, f.Fraction);
        Assert.True(f.Exhausted);
        Assert.Equal(10.0, f.ExhaustedSince);
        Assert.Equal(1, f.ExhaustionsEntered);
        Assert.Equal(10, f.Blows);
        Assert.Equal(0.0, f.LowestFraction);
    }

    [Fact]
    public void Swinging_on_empty_stays_empty_and_exhausted_without_a_new_entry()
    {
        var r = Defaults(cost: 100);
        var f = new Fighter();
        Assert.True(EnduranceMath.Charge(f, r, 1).EnteredExhaustion);
        var again = EnduranceMath.Charge(f, r, 2);
        Assert.False(again.EnteredExhaustion);
        Assert.Equal(0.0, again.After);
        Assert.True(f.Exhausted);
        Assert.Equal(1, f.ExhaustionsEntered);
        Assert.Equal(2.0, f.LastBlowTime);
    }

    [Fact]
    public void The_outcome_reports_cost_and_multiplier_for_the_log()
    {
        var r = Defaults();
        var o = EnduranceMath.Charge(new Fighter { IsHero = true, IsLeader = true }, r, 5);
        Assert.Equal(5.625, o.Cost, 6);
        Assert.Equal(0.5625, o.Multiplier, 6);
        Assert.Equal(100.0, o.Pool);
        Assert.Equal(100.0, o.Before, 6);
        Assert.Equal(94.375, o.After, 6);
    }

    [Fact]
    public void Changing_the_pool_mid_battle_keeps_each_fighters_share()
    {
        var f = new Fighter();
        EnduranceMath.Charge(f, Defaults(max: 100), 1);                 // 90 of 100 = 90%
        Assert.Equal(90.0, EnduranceMath.Points(Defaults(max: 100), f), 6);
        var bigger = Defaults(max: 200);
        Assert.Equal(180.0, EnduranceMath.Points(bigger, f), 6);         // still 90%
        var o = EnduranceMath.Charge(f, bigger, 2);                      // 10 of 200 = 5%
        Assert.Equal(180.0, o.Before, 6);
        Assert.Equal(170.0, o.After, 6);
        Assert.Equal(0.85, f.Fraction, 9);
    }

    [Fact]
    public void Nothing_regenerates_during_the_delay_and_only_the_part_after_it_counts()
    {
        var r = Defaults(); // delay 3 s, full in 60 s standing
        var f = new Fighter();
        EnduranceMath.Charge(f, r, 10.0);                                // 90%
        var during = EnduranceMath.Regen(f, r, 12.9, 0.1, 0f, 5f);
        Assert.Equal(0.0, during.Gained);
        Assert.Equal(0.9, f.Fraction, 9);
        // the step (12.9, 13.5] straddles the end of the delay at 13.0: only 0.5 s refills
        var straddle = EnduranceMath.Regen(f, r, 13.5, 0.6, 0f, 5f);
        Assert.Equal(0.5, straddle.Seconds, 9);
        Assert.Equal(0.5 / 60, straddle.Gained, 9);
        Assert.True(f.Regenerating);
    }

    [Fact]
    public void Empty_to_full_takes_60_s_standing_and_120_s_moving()
    {
        var r = Defaults(cost: 100);
        var standing = new Fighter();
        EnduranceMath.Charge(standing, r, 0);
        var moving = new Fighter();
        EnduranceMath.Charge(moving, r, 0);

        double t = 3.0; // regen starts here
        RegenOutcome last = default;
        int steps = 0;
        while (standing.Fraction < 1 && steps < 10000)
        {
            t += 0.1;
            last = EnduranceMath.Regen(standing, r, t, 0.1, 0.2f, 5f);
            steps++;
        }
        Assert.True(last.ReachedFull);
        Assert.Equal(60.0, last.EpisodeStandingSeconds, 3);
        Assert.Equal(0.0, last.EpisodeMovingSeconds, 9);
        Assert.Equal(0.0, last.EpisodeStartFraction, 9);
        Assert.Equal(63.0, t, 3);

        t = 3.0;
        steps = 0;
        while (moving.Fraction < 1 && steps < 10000)
        {
            t += 0.1;
            last = EnduranceMath.Regen(moving, r, t, 0.1, 3.0f, 5f);
            steps++;
        }
        Assert.True(last.ReachedFull);
        Assert.Equal(120.0, last.EpisodeMovingSeconds, 3);
        Assert.Equal(123.0, t, 3);
    }

    [Fact]
    public void Moving_means_strictly_above_the_threshold_and_the_horse_speed_decides_for_riders()
    {
        var r = Defaults(threshold: 0.5f);
        var f = new Fighter();
        Assert.False(EnduranceMath.IsMoving(r, 0.5f));
        Assert.True(EnduranceMath.IsMoving(r, 0.51f));
        Assert.Equal(1.0 / 60, EnduranceMath.RegenFractionPerSecond(r, f, 0.5f, 5f), 9);
        Assert.Equal(1.0 / 120, EnduranceMath.RegenFractionPerSecond(r, f, 8f, 12f), 9);
        Assert.True(EnduranceMath.IsMoving(Defaults(threshold: 0f), 0.01f));
    }

    [Fact]
    public void A_new_blow_ends_the_refill_run_and_restarts_the_delay()
    {
        var r = Defaults();
        var f = new Fighter();
        EnduranceMath.Charge(f, r, 0);
        EnduranceMath.Regen(f, r, 4.0, 1.0, 0f, 5f); // 1 s of refill
        Assert.True(f.Regenerating);
        double before = f.Fraction;
        EnduranceMath.Charge(f, r, 4.0);
        Assert.False(f.Regenerating);
        Assert.Equal(before - 0.1, f.Fraction, 9);
        Assert.Equal(0.0, EnduranceMath.Regen(f, r, 6.9, 2.9, 0f, 5f).Gained); // still inside the new 3 s
    }

    [Fact]
    public void Speed_returns_the_moment_endurance_is_above_zero_by_default()
    {
        var r = Defaults(cost: 100);
        var f = new Fighter();
        EnduranceMath.Charge(f, r, 10);
        Assert.True(f.Exhausted);
        Assert.False(EnduranceMath.Regen(f, r, 13.0, 3.0, 0f, 5f).Recovered); // delay just ended, nothing gained
        var o = EnduranceMath.Regen(f, r, 13.1, 0.1, 0f, 5f);
        Assert.True(o.Recovered);
        Assert.Equal(3.1, o.ExhaustedSeconds, 6);
        Assert.False(f.Exhausted);
    }

    [Fact]
    public void With_a_recover_threshold_speed_returns_only_above_it_and_a_lowered_threshold_applies_at_once()
    {
        var r = Defaults(cost: 100, recover: 10);
        var f = new Fighter();
        EnduranceMath.Charge(f, r, 0);
        double t = 3.0;
        while (f.Fraction < 0.09)
        {
            t += 0.5;
            Assert.False(EnduranceMath.Regen(f, r, t, 0.5, 0f, 5f).Recovered);
        }
        Assert.True(f.Exhausted);                           // ~9.2%: not yet
        Assert.True(EnduranceMath.Regen(f, r, t + 1.0, 1.0, 0f, 5f).Recovered); // 1 s more: ~10.8% - above 10%
        EnduranceMath.Charge(f, Defaults(cost: 100), t + 1.0);                 // empty again
        Assert.True(f.Exhausted);
        t += 1.0;
        while (f.Fraction < 0.07)
        {
            t += 0.5;
            Assert.False(EnduranceMath.Regen(f, r, t, 0.5, 0f, 5f).Recovered);
        }
        // the player lowers the threshold to 5% mid-battle; he is also swinging, so nothing refills
        f.LastBlowTime = t;
        var lowered = Defaults(cost: 100, recover: 5);
        var o = EnduranceMath.Regen(f, lowered, t + 0.1, 0.1, 0f, 5f);
        Assert.Equal(0.0, o.Gained);
        Assert.True(o.Recovered);
    }

    [Fact]
    public void The_speed_multiplier_is_a_cliff_at_zero_read_live()
    {
        var r = Defaults(cost: 100);
        var f = new Fighter();
        Assert.Equal(1f, EnduranceMath.AttackSpeedMultiplier(r, f));
        EnduranceMath.Charge(f, r, 0);
        Assert.Equal(0.2f, EnduranceMath.AttackSpeedMultiplier(r, f), 5);
        Assert.Equal(0.5f, EnduranceMath.AttackSpeedMultiplier(Defaults(speed: 50), f), 5);
        Assert.Equal(1f, EnduranceMath.AttackSpeedMultiplier(Defaults(enabled: false), f));
        Assert.True(EnduranceMath.IsExhausted(r, f));
        Assert.False(EnduranceMath.IsExhausted(Defaults(enabled: false), f));
    }

    [Fact]
    public void A_speed_update_is_needed_for_a_real_step_or_any_move_to_or_from_one()
    {
        Assert.True(EnduranceMath.SpeedUpdateNeeded(1f, 0.2f));
        Assert.True(EnduranceMath.SpeedUpdateNeeded(0.2f, 1f));
        Assert.True(EnduranceMath.SpeedUpdateNeeded(0.2f, 0.5f));
        Assert.False(EnduranceMath.SpeedUpdateNeeded(0.2f, 0.2f));
        Assert.False(EnduranceMath.SpeedUpdateNeeded(0.50f, 0.51f));
        Assert.True(EnduranceMath.SpeedUpdateNeeded(0.995f, 1f)); // back to exactly full speed
    }

    [Fact]
    public void Switched_off_nothing_is_charged_or_refilled_and_everyone_reads_full()
    {
        var off = Defaults(enabled: false);
        var f = new Fighter();
        var o = EnduranceMath.Charge(f, off, 1);
        Assert.False(o.Charged);
        Assert.Equal(1.0, f.Fraction);
        Assert.Equal(0, f.Blows);

        EnduranceMath.Charge(f, Defaults(cost: 100), 2); // exhausted while on
        var read = EnduranceMath.Read(off, f);
        Assert.False(read.Enabled);
        Assert.Equal(100.0, read.Points);
        Assert.Equal(1.0, read.Fraction);
        Assert.False(read.Exhausted);
        Assert.Equal(0.0, EnduranceMath.Regen(f, off, 100, 10, 0f, 5f).Gained);

        f.ResetFull(); // what the module does when EnduranceEnabled goes off
        Assert.Equal(1.0, f.Fraction);
        Assert.False(f.Exhausted);
        Assert.False(f.Regenerating);
    }

    [Fact]
    public void Free_blows_still_restart_the_delay()
    {
        var r = Defaults(cost: 0);
        var f = new Fighter();
        var o = EnduranceMath.Charge(f, r, 7);
        Assert.True(o.Charged);
        Assert.Equal(0.0, o.Cost);
        Assert.Equal(1.0, f.Fraction);
        Assert.Equal(7.0, f.LastBlowTime);
        Assert.False(f.Exhausted);
    }

    [Fact]
    public void No_delay_refills_at_once_and_the_read_reports_points()
    {
        var r = Defaults(delayBlows: 0);
        var f = new Fighter { IsHero = true };
        EnduranceMath.Charge(f, r, 5);
        var o = EnduranceMath.Regen(f, r, 5.5, 0.5, 0f, 5f);
        Assert.Equal(0.5, o.Seconds, 9);
        var read = EnduranceMath.Read(r, f);
        Assert.True(read.Enabled);
        Assert.True(read.IsHero);
        Assert.Equal(100.0, read.Pool);
        Assert.Equal(92.5 + 100 * 0.5 / 60, read.Points, 6);
    }
}

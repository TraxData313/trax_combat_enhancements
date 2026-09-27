using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>DESIGN §2 (Athletics v2, step 5c) - one test group per rule: the pool from the
/// Athletics skill, costs in points, f and the peak zone, the health cap, the three curves, the
/// damage upside, regen by effort, and the bookkeeping (fractions kept on a pool change).</summary>
public class AthleticsTests
{
    /// <summary>DESIGN's initial values, as a player without a config file has them.</summary>
    private static AthleticsRules Defaults(bool enabled = true, int floor = 50, float perSkill = 1.0f, int peak = 75, bool healthCaps = true,
        float cost = 10, bool costOnMiss = true, float hero = 0.75f, float leader = 0.75f, int speed = 20, float run = 0.7f, float mount = 1.0f,
        bool damageFollows = true, float delayBlows = 2, float blowTime = 1.5f, float standing = 60, float atFullRun = 0.5f, float walk = 0.4f,
        bool modEnabled = true)
        => new(enabled, floor, perSkill, peak, healthCaps, cost, costOnMiss, hero, leader, speed, run, mount, damageFollows,
            delayBlows, blowTime, standing, atFullRun, walk, modEnabled);

    private static Fighter Troop(int skill, bool hero = false, bool leader = false) => new() { AthleticsSkill = skill, IsHero = hero, IsLeader = leader };

    /// <summary>Swings a fresh fighter until empty: (blows struck at full strength, blows to empty).</summary>
    private static (int full, int empty) Swing(Fighter f, AthleticsRules r)
    {
        int full = 0;
        for (int i = 1; i <= 1000; i++)
        {
            var o = AthleticsMath.Charge(f, r, i);
            if (o.PeakShareBefore >= 1.0) full++;
            if (o.EnteredExhaustion) return (full, i);
        }
        return (full, -1);
    }

    // ------------------------------------------------------------------ settings

    [Fact]
    public void Rules_read_the_live_settings()
    {
        var s = new TraxSettings();
        var r = AthleticsRules.From(s);
        Assert.True(r.Enabled);
        Assert.Equal(50, r.PoolFloor);
        Assert.Equal(1.0f, r.PoolPerSkill);
        Assert.Equal(75, r.PeakPercent);
        Assert.Equal(0.75, r.PeakFraction, 9);
        Assert.True(r.HealthCaps);
        Assert.Equal(10f, r.CostPerBlow);
        Assert.Equal(20, r.ExhaustedAttackSpeedPercent);
        Assert.Equal(0.7f, r.MinMoveSpeedMultiplier, 5);
        Assert.Equal(1.0f, r.MountMinSpeedMultiplier, 5);
        Assert.False(r.MountsSlow);
        Assert.True(r.DamageBonusFollows);
        Assert.Equal(3.0, r.RegenDelaySeconds, 6);
        Assert.Equal(0.5f, r.RegenMultiplierAtFullRun, 5);
        Assert.Equal(0.4f, r.WalkEffortFraction, 5);

        s.Set(SettingsSchema.AthleticsPoolFloor, 80, SettingSources.Mcm);
        s.Set(SettingsSchema.MountMinSpeedMultiplier, 0.6, SettingSources.Mcm);
        s.Set(SettingsSchema.AthleticsEnabled, false, SettingSources.Mcm);
        var after = AthleticsRules.From(s);
        Assert.Equal(80, after.PoolFloor);
        Assert.False(after.Enabled);
        Assert.False(after.MountsSlow); // off: nothing slows
    }

    // ------------------------------------------------------------------ the pool

    [Theory]
    [InlineData(20, 50)]   // imperial recruit: the floor
    [InlineData(40, 50)]   // tier 2: the floor
    [InlineData(50, 50)]
    [InlineData(60, 60)]   // elite cataphract
    [InlineData(130, 130)] // legionary
    [InlineData(170, 170)] // Fian champion
    [InlineData(300, 300)] // a 300-skill hero
    public void The_pool_is_the_Athletics_skill_with_a_floor_of_50(int skill, double pool)
    {
        Assert.Equal(pool, AthleticsMath.PoolPoints(Defaults(), Troop(skill)), 9);
        Assert.Equal(skill < 50, AthleticsMath.IsAtFloor(Defaults(), skill));
    }

    [Fact]
    public void Per_skill_scales_the_pool_and_it_never_drops_below_one_point()
    {
        Assert.Equal(195.0, AthleticsMath.PoolPoints(Defaults(perSkill: 1.5f), Troop(130)), 6);
        Assert.Equal(60.0, AthleticsMath.PoolPoints(Defaults(perSkill: 1.5f), Troop(40)), 6);  // 1.5 × 40 = 60, above the floor
        Assert.Equal(50.0, AthleticsMath.PoolPoints(Defaults(perSkill: 1.5f), Troop(20)), 6);  // 1.5 × 20 = 30: the floor
        Assert.Equal(20.0, AthleticsMath.PoolPoints(Defaults(floor: 0), Troop(20)), 6);
        Assert.Equal(1.0, AthleticsMath.PoolPoints(Defaults(floor: 0), Troop(0)), 6);        // never 0
        Assert.Equal(50.0, AthleticsMath.PoolPoints(Defaults(), new Fighter()), 6);          // skill unknown: the floor
    }

    // ------------------------------------------------------------------ cost in points

    [Fact]
    public void A_blow_costs_10_points_7_5_for_a_hero_5_6_for_a_party_leader_whatever_the_pool()
    {
        var r = Defaults();
        foreach (int skill in new[] { 20, 130, 300 })
        {
            Assert.Equal(10.0, AthleticsMath.BlowCostPoints(r, Troop(skill)), 6);
            Assert.Equal(7.5, AthleticsMath.BlowCostPoints(r, Troop(skill, hero: true)), 6);
            Assert.Equal(5.625, AthleticsMath.BlowCostPoints(r, Troop(skill, hero: true, leader: true)), 6);
        }
        // a leader flag alone (a custom battle general who is not a hero - defensive) still stacks one discount
        Assert.Equal(7.5, AthleticsMath.BlowCostPoints(r, Troop(20, leader: true)), 6);
    }

    [Fact]
    public void Points_land_against_each_fighters_own_pool()
    {
        var r = Defaults();
        var recruit = Troop(20);      // pool 50
        var legionary = Troop(130);   // pool 130
        var a = AthleticsMath.Charge(recruit, r, 1);
        var b = AthleticsMath.Charge(legionary, r, 1);
        Assert.Equal(50.0, a.Pool, 9);
        Assert.Equal(40.0, a.After, 9);
        Assert.Equal(0.8, recruit.Fraction, 9);
        Assert.Equal(130.0, b.Pool, 9);
        Assert.Equal(120.0, b.After, 9);
        Assert.Equal(120.0 / 130, legionary.Fraction, 9);
    }

    [Theory]
    [InlineData(20, false, false, 2, 5)]    // recruit (floor 50)
    [InlineData(130, false, false, 4, 13)]  // legionary
    [InlineData(170, false, false, 5, 17)]  // Fian champion
    [InlineData(300, true, true, 14, 54)]   // a 300-skill party leader (5.625 a blow: 53.3 blows' worth)
    [InlineData(300, true, false, 11, 40)]  // a 300-skill companion (7.5)
    public void DESIGN_blows_at_full_strength_and_to_empty(int skill, bool hero, bool leader, int atFull, int toEmpty)
    {
        var (full, empty) = Swing(Troop(skill, hero, leader), Defaults());
        Assert.Equal(atFull, full);
        Assert.Equal(toEmpty, empty);
    }

    [Fact]
    public void Five_blows_of_ten_empty_a_recruit_exactly()
    {
        var r = Defaults();
        var f = Troop(20);
        for (int i = 1; i <= 4; i++) AthleticsMath.Charge(f, r, i);
        Assert.Equal(10.0, AthleticsMath.Points(r, f), 6);
        Assert.False(f.Exhausted);
        var o = AthleticsMath.Charge(f, r, 5);
        Assert.True(o.EnteredExhaustion);
        Assert.Equal(10.0, o.Before, 6);
        Assert.Equal(0.0, o.After);
        Assert.Equal(0.0, f.Fraction);
        Assert.True(f.Exhausted);
        Assert.Equal(5.0, f.ExhaustedSince);
        Assert.Equal(1, f.ExhaustionsEntered);
        Assert.Equal(0.0, f.LowestFraction);
    }

    [Fact]
    public void Swinging_on_empty_stays_empty_without_a_new_entry()
    {
        var r = Defaults(cost: 100);
        var f = Troop(20);
        Assert.True(AthleticsMath.Charge(f, r, 1).EnteredExhaustion);
        var again = AthleticsMath.Charge(f, r, 2);
        Assert.False(again.EnteredExhaustion);
        Assert.Equal(0.0, again.After);
        Assert.Equal(1, f.ExhaustionsEntered);
        Assert.Equal(2.0, f.LastBlowTime);
    }

    [Fact]
    public void The_outcome_reports_cost_multiplier_pool_and_f_for_the_log()
    {
        var o = AthleticsMath.Charge(Troop(100, hero: true, leader: true), Defaults(), 5);
        Assert.Equal(5.625, o.Cost, 6);
        Assert.Equal(0.5625, o.Multiplier, 6);
        Assert.Equal(100.0, o.Pool);
        Assert.Equal(100.0, o.Before, 6);
        Assert.Equal(94.375, o.After, 6);
        Assert.Equal(1.0, o.PeakShareBefore);
        Assert.Equal(1.0, o.PeakShareAfter);
        Assert.False(o.LeftPeak);
    }

    // ------------------------------------------------------------------ f and the peak zone

    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(0.75, 1.0)]     // on the line: full strength
    [InlineData(0.6, 0.8)]
    [InlineData(0.375, 0.5)]
    [InlineData(0.075, 0.1)]
    [InlineData(0.0, 0.0)]
    public void F_is_the_share_of_the_peak_line_left(double fraction, double f)
    {
        var r = Defaults();
        Assert.Equal(f, AthleticsMath.PeakShareOf(r, fraction), 9);
    }

    [Fact]
    public void F_is_measured_on_each_fighters_own_pool_and_is_1_when_off()
    {
        var r = Defaults();
        var legionary = Troop(130);
        AthleticsMath.Charge(legionary, r, 1);
        AthleticsMath.Charge(legionary, r, 2);
        AthleticsMath.Charge(legionary, r, 3);
        var fourth = AthleticsMath.Charge(legionary, r, 4);  // 100 → 90: crosses the 97.5 line
        Assert.True(fourth.LeftPeak);
        Assert.Equal(90.0 / 97.5, AthleticsMath.PeakShare(r, legionary), 9);
        Assert.Equal(1.0, AthleticsMath.PeakShare(Defaults(enabled: false), legionary));
        Assert.Equal(1.0, AthleticsMath.PeakShare(Defaults(modEnabled: false), legionary));
        Assert.Equal(90.0 / 130, AthleticsMath.PeakShare(Defaults(peak: 100), legionary), 9); // peak 100 = the whole bar
    }

    [Theory]
    [InlineData(1.0, 0)]
    [InlineData(0.9999, 1)]
    [InlineData(0.5, 1)]
    [InlineData(0.4999, 2)]
    [InlineData(0.0001, 2)]
    [InlineData(0.0, 3)]
    public void The_summary_bins_f(double f, int bin) => Assert.Equal(bin, AthleticsMath.PeakBin(f));

    // ------------------------------------------------------------------ the curves

    [Fact]
    public void Attack_speed_is_S_plus_1_minus_S_times_f()
    {
        var r = Defaults();
        var f = Troop(100);
        Assert.Equal(1f, AthleticsMath.AttackSpeedMultiplier(r, f));          // full: exactly 1
        AthleticsMath.Charge(f, Defaults(cost: 62.5f), 1);                     // 37.5 of 100 → f 0.5
        Assert.Equal(0.6f, AthleticsMath.AttackSpeedMultiplier(r, f), 5);      // 0.2 + 0.8 × 0.5
        AthleticsMath.Charge(f, Defaults(cost: 100), 2);                       // empty
        Assert.Equal(0.2f, AthleticsMath.AttackSpeedMultiplier(r, f));         // exactly S
        Assert.Equal(0.5f, AthleticsMath.AttackSpeedMultiplier(Defaults(speed: 50), f), 5);
        Assert.Equal(1f, AthleticsMath.AttackSpeedMultiplier(Defaults(enabled: false), f));
        Assert.True(AthleticsMath.IsExhausted(r, f));
        Assert.False(AthleticsMath.IsExhausted(Defaults(enabled: false), f));
    }

    [Fact]
    public void Run_speed_is_M_plus_1_minus_M_times_f_and_horses_keep_theirs_by_default()
    {
        var r = Defaults();
        var f = Troop(100);
        Assert.Equal(1f, AthleticsMath.RunSpeedMultiplier(r, f));
        AthleticsMath.Charge(f, Defaults(cost: 62.5f), 1);                     // f 0.5
        Assert.Equal(0.85f, AthleticsMath.RunSpeedMultiplier(r, f), 5);        // 0.7 + 0.3 × 0.5
        Assert.Equal(1f, AthleticsMath.MountSpeedMultiplier(r, f));            // MountMinSpeedMultiplier 1: never
        Assert.Equal(0.75f, AthleticsMath.MountSpeedMultiplier(Defaults(mount: 0.5f), f), 5); // 0.5 + 0.5 × 0.5
        AthleticsMath.Charge(f, Defaults(cost: 100), 2);                       // empty
        Assert.Equal(0.7f, AthleticsMath.RunSpeedMultiplier(r, f), 5);
        Assert.Equal(0.5f, AthleticsMath.MountSpeedMultiplier(Defaults(mount: 0.5f), f), 5);
        Assert.Equal(1f, AthleticsMath.RunSpeedMultiplier(Defaults(modEnabled: false), f));
        Assert.Equal(1f, AthleticsMath.MountSpeedMultiplier(Defaults(mount: 0.5f, enabled: false), f));
    }

    [Fact]
    public void The_damage_upside_follows_f_unless_switched_off()
    {
        var f = Troop(100);
        AthleticsMath.Charge(f, Defaults(cost: 62.5f), 1); // f 0.5
        Assert.Equal(0.5, AthleticsMath.DamageUpside(Defaults(), f), 9);
        Assert.Equal(1.0, AthleticsMath.DamageUpside(Defaults(damageFollows: false), f));
        Assert.Equal(1.0, AthleticsMath.DamageUpside(Defaults(enabled: false), f)); // off: f reads 1
    }

    [Fact]
    public void A_speed_update_is_needed_for_a_5_percent_step_or_any_move_onto_an_end_point()
    {
        const float floor = 0.2f;
        Assert.True(AthleticsMath.SpeedUpdateNeeded(1f, 0.2f, floor));
        Assert.True(AthleticsMath.SpeedUpdateNeeded(0.2f, 1f, floor));
        Assert.True(AthleticsMath.SpeedUpdateNeeded(0.5f, 0.56f, floor));
        Assert.False(AthleticsMath.SpeedUpdateNeeded(0.5f, 0.54f, floor));   // below the step: waits
        Assert.False(AthleticsMath.SpeedUpdateNeeded(0.2f, 0.2f, floor));
        Assert.True(AthleticsMath.SpeedUpdateNeeded(0.98f, 1f, floor));      // back to exactly full speed
        Assert.True(AthleticsMath.SpeedUpdateNeeded(0.22f, 0.2f, floor));    // down to exactly the floor
        Assert.True(AthleticsMath.SpeedUpdateNeeded(0.2f, 0.21f, floor));    // leaving the floor
        Assert.False(AthleticsMath.SpeedUpdateNeeded(0.21f, 0.22f, floor));
        Assert.Equal(0.05f, AthleticsMath.SpeedUpdateStep);
    }

    // ------------------------------------------------------------------ the health cap

    [Fact]
    public void A_wound_cuts_athletics_to_the_health_left_at_once()
    {
        var r = Defaults();
        var f = Troop(100);
        AthleticsMath.Charge(f, r, 1);                                  // 90
        double cut = AthleticsMath.ApplyHealth(f, r, 0.75);             // 75% health
        Assert.Equal(0.15, cut, 9);
        Assert.Equal(0.75, f.Fraction, 9);
        Assert.Equal(75.0, AthleticsMath.UsablePoints(r, f), 9);
        Assert.Equal(0.75, f.LowestFraction, 9);
        Assert.Equal(0.0, AthleticsMath.ApplyHealth(f, r, 0.8));        // healthier: nothing cut
        Assert.Equal(0.75, f.Fraction, 9);
    }

    [Fact]
    public void Below_full_means_below_the_top_it_can_refill_to()
    {
        // Step 12: outside a battle the player bar stays up while this is true.
        var r = Defaults();
        var f = Troop(100);
        Assert.False(AthleticsMath.Read(r, f).BelowFull);                  // fresh
        AthleticsMath.Charge(f, r, 1);
        Assert.True(AthleticsMath.Read(r, f).BelowFull);                   // one blow: refilling
        AthleticsMath.Regen(f, r, 100, 100, 0f, 5f);                        // a long rest: the refill stops at the top
        Assert.False(AthleticsMath.Read(r, f).BelowFull);
        AthleticsMath.ApplyHealth(f, r, 0.5);                               // a wound caps the bar at half...
        Assert.False(AthleticsMath.Read(r, f).BelowFull);                  // ...and half is as full as it gets
        AthleticsMath.Charge(f, r, 101);
        Assert.True(AthleticsMath.Read(r, f).BelowFull);
        Assert.False(AthleticsMath.Read(Defaults(enabled: false), f).BelowFull); // Athletics off: everyone reads full
    }

    [Fact]
    public void The_peak_line_stays_on_the_full_pool_so_a_badly_wounded_fighter_never_gets_full_strength()
    {
        var r = Defaults();
        var f = Troop(100);
        AthleticsMath.ApplyHealth(f, r, 0.5);
        Assert.Equal(0.5, f.Fraction, 9);
        Assert.Equal(2.0 / 3, AthleticsMath.PeakShare(r, f), 9);        // "at 50% health, f is at most 0.67"
        Assert.True(AthleticsMath.AttackSpeedMultiplier(r, f) < 1f);
        var read = AthleticsMath.Read(r, f);
        Assert.Equal(100.0, read.Pool);
        Assert.Equal(50.0, read.UsablePool, 9);
        Assert.Equal(0.5, read.UsableFraction, 9);
        Assert.Equal(0.75, read.PeakFraction, 9);
        Assert.False(read.InPeakZone);
    }

    [Fact]
    public void Regen_never_fills_above_the_cap()
    {
        var r = Defaults(delayBlows: 0);
        var f = Troop(100);
        AthleticsMath.Charge(f, Defaults(cost: 100), 0);                // empty
        AthleticsMath.ApplyHealth(f, r, 0.6);
        RegenOutcome last = default;
        double t = 0;
        for (int i = 0; i < 1000 && !last.ReachedTop; i++)
        {
            t += 0.1;
            last = AthleticsMath.Regen(f, r, t, 0.1, 0f, 5f);
        }
        Assert.True(last.ReachedTop);
        Assert.Equal(0.6, last.Top, 9);
        Assert.Equal(0.6, f.Fraction, 9);
        Assert.Equal(36.0, t, 1);                                       // 0.6 of the pool at 1/60 per second
        Assert.Equal(0.0, AthleticsMath.Regen(f, r, t + 5, 5, 0f, 5f).Gained);
        AthleticsMath.ApplyHealth(f, r, 0.9);                           // healed: the cap rises, regen climbs again
        Assert.True(AthleticsMath.Regen(f, r, t + 6, 1, 0f, 5f).Gained > 0);
    }

    [Fact]
    public void The_cap_is_off_when_its_switch_or_athletics_is_off()
    {
        var f = Troop(100);
        Assert.Equal(0.0, AthleticsMath.ApplyHealth(f, Defaults(healthCaps: false), 0.3));
        Assert.Equal(1.0, f.Fraction);
        Assert.Equal(0.3, f.Health, 9);                                 // the health is still recorded
        Assert.Equal(1.0, AthleticsMath.UsableFraction(Defaults(healthCaps: false), f));
        Assert.Equal(0.0, AthleticsMath.ApplyHealth(f, Defaults(enabled: false), 0.3));
        Assert.Equal(1.0, f.Fraction);
        Assert.True(AthleticsMath.ApplyHealth(f, Defaults(), 0.3) > 0); // back on: it applies at the next check
        Assert.Equal(0.3, f.Fraction, 9);
    }

    // ------------------------------------------------------------------ regen by effort

    [Theory]
    [InlineData(0.0, 1.0)]
    [InlineData(0.4, 1.0)]    // a walk: full rate
    [InlineData(0.7, 0.75)]   // halfway between a walk and a full run
    [InlineData(1.0, 0.5)]    // a full run
    [InlineData(1.3, 0.5)]    // faster than the "top" (downhill, a charge): no lower than the full-run rate
    public void Regen_rate_is_full_at_a_walk_then_falls_in_a_straight_line_to_the_full_run_rate(double effort, double multiplier)
    {
        Assert.Equal(multiplier, AthleticsMath.RegenRateMultiplier(Defaults(), effort), 6); // the settings are floats
    }

    [Fact]
    public void Regen_effort_edge_settings()
    {
        Assert.Equal(1.0, AthleticsMath.RegenRateMultiplier(Defaults(walk: 1.0f), 1.0));     // any pace counts as walking
        Assert.Equal(0.0, AthleticsMath.RegenRateMultiplier(Defaults(atFullRun: 0f), 1.0));  // no refill at a full run
        Assert.Equal(1.0, AthleticsMath.RegenRateMultiplier(Defaults(atFullRun: 1f), 1.0));
        var f = Troop(100);
        Assert.Equal(1.0 / 60, AthleticsMath.RegenFractionPerSecond(Defaults(), f, 0f, 5f), 9);
        Assert.Equal(1.0 / 60, AthleticsMath.RegenFractionPerSecond(Defaults(), f, 3f, 0f), 9); // top unknown: full rate
        Assert.Equal(0.5 / 60, AthleticsMath.RegenFractionPerSecond(Defaults(), f, 4.5f, 4.5f), 9); // a rider's horse at its top
        Assert.Equal(0.5, AthleticsMath.Effort(2f, 4f), 9);
        Assert.Equal(0.0, AthleticsMath.Effort(2f, 0f));
        Assert.True(AthleticsMath.IsWalking(Defaults(), 0.4));
        Assert.False(AthleticsMath.IsWalking(Defaults(), 0.41));
    }

    [Fact]
    public void Empty_to_full_takes_60_s_standing_or_walking_and_120_s_at_a_full_run()
    {
        var r = Defaults(cost: 100);
        foreach (var (speed, expected, walking) in new[] { (0f, 60.0, true), (1.8f, 60.0, true), (4.5f, 120.0, false) })
        {
            var f = Troop(100);
            AthleticsMath.Charge(f, r, 0);
            double t = 3.0; // the delay ends here
            RegenOutcome last = default;
            while (f.Fraction < 1 && t < 1000)
            {
                t += 0.1;
                last = AthleticsMath.Regen(f, r, t, 0.1, speed, 4.5f);
            }
            Assert.True(last.ReachedTop);
            Assert.Equal(1.0, last.Top);
            Assert.Equal(expected, last.EpisodeSeconds, 3);
            Assert.Equal(walking ? expected : 0.0, last.EpisodeWalkSeconds, 3);
            Assert.Equal(60.0, last.EpisodeRateSeconds, 3);             // Σ rate × s = one full refill at rest
            Assert.Equal(0.0, last.EpisodeStartFraction, 9);
            Assert.Equal(3.0 + expected, t, 3);
            Assert.Equal(walking, last.Walking);
        }
    }

    [Fact]
    public void Nothing_regenerates_during_the_delay_and_only_the_part_after_it_counts()
    {
        var r = Defaults(); // delay 3 s, full in 60 s at rest
        var f = Troop(100);
        AthleticsMath.Charge(f, r, 10.0);                                // 90%
        var during = AthleticsMath.Regen(f, r, 12.9, 0.1, 0f, 5f);
        Assert.Equal(0.0, during.Gained);
        Assert.Equal(0.9, f.Fraction, 9);
        // the step (12.9, 13.5] straddles the end of the delay at 13.0: only 0.5 s refills
        var straddle = AthleticsMath.Regen(f, r, 13.5, 0.6, 0f, 5f);
        Assert.Equal(0.5, straddle.Seconds, 9);
        Assert.Equal(0.5 / 60, straddle.Gained, 9);
        Assert.True(f.Regenerating);
    }

    [Fact]
    public void A_new_blow_ends_the_refill_run_and_restarts_the_delay()
    {
        var r = Defaults();
        var f = Troop(100);
        AthleticsMath.Charge(f, r, 0);
        AthleticsMath.Regen(f, r, 4.0, 1.0, 0f, 5f); // 1 s of refill
        Assert.True(f.Regenerating);
        double before = f.Fraction;
        AthleticsMath.Charge(f, r, 4.0);
        Assert.False(f.Regenerating);
        Assert.Equal(before - 0.1, f.Fraction, 9);
        Assert.Equal(0.0, AthleticsMath.Regen(f, r, 6.9, 2.9, 0f, 5f).Gained); // still inside the new 3 s
    }

    [Fact]
    public void Leaving_zero_is_the_first_refill_above_it_and_the_curves_climb_with_the_bar()
    {
        var r = Defaults(cost: 100);
        var f = Troop(100);
        AthleticsMath.Charge(f, r, 10);
        Assert.True(f.Exhausted);
        Assert.False(AthleticsMath.Regen(f, r, 13.0, 3.0, 0f, 5f).Recovered); // delay just ended, nothing gained
        var o = AthleticsMath.Regen(f, r, 13.1, 0.1, 0f, 5f);
        Assert.True(o.Recovered);
        Assert.Equal(3.1, o.ExhaustedSeconds, 6);
        Assert.False(f.Exhausted);
        float attack = AthleticsMath.AttackSpeedMultiplier(r, f);
        Assert.InRange(attack, 0.2f, 0.21f);                                  // just above the floor, not a cliff
    }

    [Fact]
    public void Refilling_back_across_the_line_is_reported_once()
    {
        var r = Defaults(delayBlows: 0);
        var f = Troop(100);
        AthleticsMath.Charge(f, Defaults(cost: 26), 0);                   // 74: just below the line
        var o = AthleticsMath.Regen(f, r, 1.0, 1.0, 0f, 5f);              // +1.67
        Assert.True(o.EnteredPeak);
        Assert.False(AthleticsMath.Regen(f, r, 2.0, 1.0, 0f, 5f).EnteredPeak);
    }

    // ------------------------------------------------------------------ bookkeeping

    [Fact]
    public void Changing_the_pool_settings_mid_battle_keeps_each_fighters_share()
    {
        var recruit = Troop(20);                                          // pool 50
        AthleticsMath.Charge(recruit, Defaults(), 1);                     // 40 of 50 = 80%
        Assert.Equal(40.0, AthleticsMath.Points(Defaults(), recruit), 6);
        var bigger = Defaults(floor: 100);
        Assert.Equal(80.0, AthleticsMath.Points(bigger, recruit), 6);     // still 80%
        var o = AthleticsMath.Charge(recruit, bigger, 2);                 // 10 of 100 = 10%
        Assert.Equal(80.0, o.Before, 6);
        Assert.Equal(70.0, o.After, 6);
        Assert.Equal(0.7, recruit.Fraction, 9);

        var legionary = Troop(130);
        AthleticsMath.Charge(legionary, Defaults(), 1);                   // 120 of 130
        Assert.Equal(240.0, AthleticsMath.Points(Defaults(perSkill: 2f), legionary), 6);
    }

    [Fact]
    public void Switched_off_nothing_is_charged_or_refilled_and_everyone_reads_full()
    {
        var off = Defaults(enabled: false);
        var f = Troop(130);
        var o = AthleticsMath.Charge(f, off, 1);
        Assert.False(o.Charged);
        Assert.Equal(1.0, f.Fraction);
        Assert.Equal(0, f.Blows);

        AthleticsMath.Charge(f, Defaults(cost: 200), 2); // exhausted while on
        var read = AthleticsMath.Read(off, f);
        Assert.False(read.Enabled);
        Assert.Equal(130.0, read.Points);
        Assert.Equal(1.0, read.Fraction);
        Assert.Equal(1.0, read.PeakShare);
        Assert.False(read.Exhausted);
        Assert.Equal(0.0, AthleticsMath.Regen(f, off, 100, 10, 0f, 5f).Gained);

        f.ResetFull(); // what the module does when Athletics goes off
        Assert.Equal(1.0, f.Fraction);
        Assert.False(f.Exhausted);
        Assert.False(f.Regenerating);
    }

    [Fact]
    public void Free_blows_still_restart_the_delay()
    {
        var f = Troop(100);
        var o = AthleticsMath.Charge(f, Defaults(cost: 0), 7);
        Assert.True(o.Charged);
        Assert.Equal(0.0, o.Cost);
        Assert.Equal(1.0, f.Fraction);
        Assert.Equal(7.0, f.LastBlowTime);
        Assert.False(f.Exhausted);
    }

    [Fact]
    public void The_read_reports_points_pool_f_and_the_multipliers_applied()
    {
        var r = Defaults(delayBlows: 0);
        var f = Troop(180, hero: true);
        f.SpeedMultiplier = 0.9f;
        f.RunSpeedMultiplier = 0.8f;
        AthleticsMath.Charge(f, r, 5);                                    // 180 → 172.5
        AthleticsMath.Regen(f, r, 5.5, 0.5, 0f, 5f);                      // + 180 × 0.5 / 60 = 1.5
        var read = AthleticsMath.Read(r, f);
        Assert.True(read.Enabled);
        Assert.True(read.IsHero);
        Assert.Equal(180, read.AthleticsSkill);
        Assert.Equal(180.0, read.Pool);
        Assert.Equal(180.0, read.UsablePool);
        Assert.Equal(174.0, read.Points, 6);
        Assert.Equal(174.0 / 180, read.Fraction, 9);
        Assert.Equal(1.0, read.PeakShare);                                // 174 ≥ 135
        Assert.True(read.InPeakZone);
        Assert.Equal(0.9f, read.SpeedMultiplier);
        Assert.Equal(0.8f, read.RunSpeedMultiplier);
        Assert.Equal(1f, read.MountSpeedMultiplier);
    }
}

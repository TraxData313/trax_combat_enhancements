using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>Step 29 (Anton, 2026-10-12, after a tournament 1v1 where both men sat at empty for good): a skilful defender
/// recovers while he stays on the defence - ONLY a wrong-side shield block restarts the refill delay
/// (OnlyWrongSideBlocksStopRefill, default on); a right-side block and a weapon parry still cost, but the delay clock and
/// a running refill are left alone. Off = step 22 (every paid block restarts it).</summary>
public class BlockRefillTests
{
    /// <summary>The shipped refill: flat (RegenRateNearFullPercent 100), empty to full in 60 s at a walk, the delay 2 x 1.5 = 3 s.</summary>
    private static AthleticsRules Rules(bool onlyWrong = true, bool mod = true, bool enabled = true, int nearFull = 100)
        => new(enabled, 50, 1.0f, 75, true, 10, true, 0.75f, 0.75f, 3, 20, 0.3f, 1.0f, true, 2, 1.5f, 60, 0.5f, 0.4f, nearFull, mod, 1, 5, 2, onlyWrong);

    private static Fighter Man(int skill = 50, bool hero = false, bool leader = false) => new() { AthleticsSkill = skill, IsHero = hero, IsLeader = leader };

    // ------------------------------------------------------------------ the rule

    [Fact]
    public void On_only_a_wrong_side_block_restarts_the_delay_off_every_kind_does()
    {
        var on = Rules();
        Assert.False(AthleticsMath.BlockRestartsRefill(on, BlockKind.ShieldRightSide));
        Assert.False(AthleticsMath.BlockRestartsRefill(on, BlockKind.WeaponParry));
        Assert.True(AthleticsMath.BlockRestartsRefill(on, BlockKind.ShieldWrongSide));
        Assert.False(AthleticsMath.BlockRestartsRefill(on, BlockKind.None));

        var off = Rules(onlyWrong: false);
        Assert.True(AthleticsMath.BlockRestartsRefill(off, BlockKind.ShieldRightSide));
        Assert.True(AthleticsMath.BlockRestartsRefill(off, BlockKind.WeaponParry));
        Assert.True(AthleticsMath.BlockRestartsRefill(off, BlockKind.ShieldWrongSide));
        Assert.False(AthleticsMath.BlockRestartsRefill(off, BlockKind.None));
    }

    [Fact]
    public void A_clean_block_or_parry_costs_but_leaves_the_delay_clock_and_a_running_refill_alone()
    {
        var r = Rules();
        var f = Man(100); // pool 100
        AthleticsMath.Charge(f, r, 0);                       // a blow at 0: 100 → 90, the delay runs to 3 s
        AthleticsMath.Regen(f, r, 6, 6, 0f, 5f);             // 3 s refilled: + 5
        Assert.True(f.Regenerating);
        Assert.Equal(0.95, f.Fraction, 9);

        foreach (var kind in new[] { BlockKind.ShieldRightSide, BlockKind.WeaponParry })
        {
            var o = AthleticsMath.ChargeBlock(f, r, kind, 6);
            Assert.True(o.Charged);
            Assert.False(o.RestartedDelay);
            Assert.Equal(0, f.LastBlowTime);                 // the blow's clock, untouched
            Assert.True(f.Regenerating);                     // the refill run goes on
        }
        Assert.Equal(0.92, f.Fraction, 9);                   // the costs were paid: 1 + 2
        AthleticsMath.Regen(f, r, 7.5, 1.5, 0f, 5f);         // and the very next step refills: + 2.5
        Assert.Equal(0.945, f.Fraction, 9);

        var w = AthleticsMath.ChargeBlock(f, r, BlockKind.ShieldWrongSide, 7.5);
        Assert.True(w.RestartedDelay);
        Assert.Equal(7.5, f.LastBlowTime);
        Assert.False(f.Regenerating);
        Assert.Equal(0.895, f.Fraction, 9);
        AthleticsMath.Regen(f, r, 10.5, 3, 0f, 5f);          // inside the new 3 s delay: nothing
        Assert.Equal(0.895, f.Fraction, 9);
        AthleticsMath.Regen(f, r, 12, 1.5, 0f, 5f);          // past it: refilling again
        Assert.Equal(0.92, f.Fraction, 9);

        // blows, kicks and bashes restart it whatever the switch
        Assert.True(AthleticsMath.Charge(f, r, 12).RestartedDelay);
        Assert.True(AthleticsMath.ChargeKickOrBash(f, r, 13).RestartedDelay);
        Assert.Equal(13, f.LastBlowTime);
    }

    [Fact]
    public void A_clean_block_inside_a_running_delay_does_not_lengthen_it()
    {
        var r = Rules();
        var f = Man(100);
        AthleticsMath.Charge(f, r, 0);                       // the delay ends at 3 s
        AthleticsMath.ChargeBlock(f, r, BlockKind.WeaponParry, 2);
        AthleticsMath.Regen(f, r, 4, 4, 0f, 5f);             // 1 s past 3 s: + 100/60
        Assert.Equal(0.88 + 1.0 / 60, f.Fraction, 9);
    }

    [Fact]
    public void Off_is_step_22_every_paid_block_restarts_the_delay()
    {
        var r = Rules(onlyWrong: false);
        foreach (var kind in new[] { BlockKind.ShieldRightSide, BlockKind.WeaponParry, BlockKind.ShieldWrongSide })
        {
            var f = Man(100);
            AthleticsMath.Charge(f, r, 0);
            AthleticsMath.Regen(f, r, 6, 6, 0f, 5f);
            var o = AthleticsMath.ChargeBlock(f, r, kind, 6);
            Assert.True(o.RestartedDelay);
            Assert.Equal(6, f.LastBlowTime);
            Assert.False(f.Regenerating);
            double after = f.Fraction;
            AthleticsMath.Regen(f, r, 9, 3, 0f, 5f);
            Assert.Equal(after, f.Fraction, 9);              // held at the delay
        }
    }

    [Fact]
    public void The_switch_is_read_live_and_sits_after_the_block_costs()
    {
        var all = SettingsSchema.All.ToList();
        Assert.Same(SettingsSchema.OnlyWrongSideBlocksStopRefill, all[all.IndexOf(SettingsSchema.CostPerWeaponParry) + 1]);
        Assert.Equal(SettingsSchema.AthleticsGroup, SettingsSchema.OnlyWrongSideBlocksStopRefill.Group);

        var s = new TraxSettings(); // DESIGN's initial value: on
        Assert.True(s.OnlyWrongSideBlocksStopRefill);
        var f = Man(100);
        AthleticsMath.ChargeBlock(f, AthleticsRules.From(s), BlockKind.WeaponParry, 5);
        Assert.True(double.IsNegativeInfinity(f.LastBlowTime));
        s.Set(SettingsSchema.OnlyWrongSideBlocksStopRefill, false, SettingSources.Mcm);
        AthleticsMath.ChargeBlock(f, AthleticsRules.From(s), BlockKind.WeaponParry, 7);
        Assert.Equal(7, f.LastBlowTime);                     // the next block uses the new value
        s.Set(SettingsSchema.OnlyWrongSideBlocksStopRefill, true, SettingSources.Mcm);
        AthleticsMath.ChargeBlock(f, AthleticsRules.From(s), BlockKind.ShieldRightSide, 9);
        Assert.Equal(7, f.LastBlowTime);
    }

    [Fact]
    public void The_master_switch_and_AthleticsEnabled_off_charge_nothing_and_touch_no_clock()
    {
        foreach (var r in new[] { Rules(mod: false), Rules(enabled: false) })
        {
            var f = Man(100);
            foreach (var kind in new[] { BlockKind.ShieldRightSide, BlockKind.WeaponParry, BlockKind.ShieldWrongSide })
            {
                var o = AthleticsMath.ChargeBlock(f, r, kind, 4);
                Assert.False(o.Charged);
                Assert.False(o.RestartedDelay);
            }
            Assert.Equal(1.0, f.Fraction);
            Assert.True(double.IsNegativeInfinity(f.LastBlowTime));
        }
    }

    // ------------------------------------------------------------------ the deadlock Anton saw, and the rates

    /// <summary>Steps <paramref name="f"/> from <paramref name="from"/> to <paramref name="to"/> s at a stand (0.05 s regen
    /// ticks, as the mission's), with a block of <paramref name="kind"/> every 1.5 s; returns the fill just before each block.</summary>
    private static List<double> Defend(Fighter f, in AthleticsRules r, BlockKind kind, double from, double to)
    {
        var before = new List<double>();
        const double dt = 0.05;
        double nextBlock = from + 1.5;
        for (double now = from + dt; now <= to + 1e-9; now += dt)
        {
            AthleticsMath.Regen(f, r, now, dt, 0f, 5f);
            if (now >= nextBlock - 1e-9)
            {
                before.Add(f.Fraction);
                AthleticsMath.ChargeBlock(f, r, kind, now);
                nextBlock += 1.5;
            }
        }
        return before;
    }

    /// <summary>The tournament: a hero (x0.75) with Athletics skill 100 (pool 100) empties himself at 0 and then only
    /// parries, one blow every 1.5 s. The shipped flat refill gives 100 / 60 x 1.5 = 2.5 points per 1.5 s; a parry costs
    /// 2 x 0.75 = 1.5 - so he nets +1.0 point every 1.5 s, +40 a minute, and climbs out. Off (step 22): every parry
    /// restarts the 3 s delay, so he stays at 0 for good - Anton's deadlock.</summary>
    [Fact]
    public void A_hero_parrying_every_1_5_s_from_empty_climbs_2_5_in_1_5_out_per_parry_while_step_22_kept_him_at_0()
    {
        var r = Rules();
        var hero = Man(100, hero: true);
        for (int i = 0; i < 14; i++) AthleticsMath.Charge(hero, r, 0); // 14 x 7.5 → 0 at t 0
        Assert.True(hero.Exhausted);
        var marks = Defend(hero, r, BlockKind.WeaponParry, 0, 60);
        // the delay ends at 3 s; from the 3rd parry on (4.5 s) each cycle nets exactly +1.0 of 100
        for (int i = 3; i < marks.Count; i++) Assert.Equal(0.01, marks[i] - marks[i - 1], 6);
        Assert.False(hero.Exhausted);
        Assert.InRange(hero.Fraction, 0.35, 0.40);   // ~38 points after a minute of parrying

        var stuck = Man(100, hero: true);
        var off = Rules(onlyWrong: false);
        for (int i = 0; i < 14; i++) AthleticsMath.Charge(stuck, off, 0);
        Defend(stuck, off, BlockKind.WeaponParry, 0, 60);
        Assert.Equal(0, stuck.Fraction);
        Assert.True(stuck.Exhausted);
    }

    /// <summary>The rates decide, by design: a soldier at the pool floor (50) refills 50 / 60 x 1.5 = 1.25 points per 1.5 s.
    /// Right-side shield blocks (1) still net +0.25 per block; parries (2) net −0.75 - he stays at the bottom. A pool above
    /// 80 (a soldier), 60 (a hero) or any pool for a hero who leads his party (x0.56: 1.125 a parry) climbs while parrying.</summary>
    [Fact]
    public void A_floor_soldier_climbs_behind_his_shield_but_not_by_parrying_every_1_5_s()
    {
        var r = Rules();
        var shield = Man(50);
        for (int i = 0; i < 5; i++) AthleticsMath.Charge(shield, r, 0);
        var marks = Defend(shield, r, BlockKind.ShieldRightSide, 0, 60);
        for (int i = 3; i < marks.Count; i++) Assert.Equal(0.25 / 50, marks[i] - marks[i - 1], 6);

        var parry = Man(50);
        for (int i = 0; i < 5; i++) AthleticsMath.Charge(parry, r, 0);
        Defend(parry, r, BlockKind.WeaponParry, 0, 60);
        Assert.InRange(parry.Fraction, 0, 1.25 / 50 + 1e-9); // never more than one cycle's refill

        var leader = Man(50, hero: true, leader: true);
        for (int i = 0; i < 9; i++) AthleticsMath.Charge(leader, r, 0);
        Assert.Equal(0, leader.Fraction);
        marks = Defend(leader, r, BlockKind.WeaponParry, 0, 60);
        for (int i = 3; i < marks.Count; i++) Assert.Equal((1.25 - 1.125) / 50, marks[i] - marks[i - 1], 6);
    }

    [Fact]
    public void A_wrong_side_block_every_1_5_s_still_holds_him_at_the_delay()
    {
        var r = Rules();
        var f = Man(100, hero: true);
        for (int i = 0; i < 14; i++) AthleticsMath.Charge(f, r, 0);
        Defend(f, r, BlockKind.ShieldWrongSide, 0, 60);
        Assert.Equal(0, f.Fraction);
    }

    // ------------------------------------------------------------------ the words

    [Fact]
    public void The_settings_sentence_says_which_blocks_stop_the_refill()
    {
        Assert.Contains("; only a WRONG-side shield block restarts the refill delay - a right-side block or a parry costs but the refill keeps running (OnlyWrongSideBlocksStopRefill)",
            AthleticsStats.DescribeRules(Rules()));
        Assert.Contains("; every paid block restarts the refill delay (OnlyWrongSideBlocksStopRefill off)", AthleticsStats.DescribeRules(Rules(onlyWrong: false)));
    }

    [Fact]
    public void The_summary_counts_blocks_that_restarted_the_delay_by_kind()
    {
        var s = new AthleticsStats();
        s.AddBlockCharge(BlockKind.ShieldRightSide, false, false, false, 1, restartedDelay: false);
        s.AddBlockCharge(BlockKind.ShieldRightSide, false, false, false, 1, restartedDelay: true);
        s.AddBlockCharge(BlockKind.WeaponParry, false, false, false, 2, restartedDelay: false);
        s.AddBlockCharge(BlockKind.ShieldWrongSide, false, false, false, 5, restartedDelay: true);
        Assert.Equal(2, s.BlocksRestartedDelay);
        Assert.Equal(1, s.BlocksRestarted(BlockKind.ShieldRightSide));
        Assert.EndsWith(" | the refill delay restarted by 2 (right side 1, WRONG side 1, parries 0), the refill kept running under 2 (right side 1, WRONG side 0, parries 1) - OnlyWrongSideBlocksStopRefill (step 29)",
            s.BlocksLine());
    }
}

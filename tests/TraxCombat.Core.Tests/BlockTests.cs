using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>Step 22 (Anton, 2026-09-29, for slower battles): defending costs the DEFENDER - a shield block on the
/// right side 1, on the wrong side 5, a weapon parry 2 - × the blow's hero / party-leader multipliers, once per
/// blocked blow (<see cref="BlockTracker"/>); the refill a straight line again (RegenRateNearFullPercent 100); the
/// run floor 0.3; config format 5 moves the old defaults once.</summary>
public class BlockTests
{
    private static AthleticsRules Rules(float right = 1, float wrong = 5, float parry = 2, bool enabled = true, bool mod = true, int nearFull = 50)
        => new(enabled, 50, 1.0f, 75, true, 10, true, 0.75f, 0.75f, 3, 20, 0.7f, 1.0f, true, 2, 1.5f, 60, 0.5f, 0.4f, nearFull, mod, right, wrong, parry);

    private static Fighter Troop(int skill = 50, bool hero = false, bool leader = false) => new() { AthleticsSkill = skill, IsHero = hero, IsLeader = leader };

    // ------------------------------------------------------------------ the cost

    [Fact]
    public void Each_kind_costs_its_setting_times_the_hero_and_leader_multipliers_whatever_the_pool()
    {
        var r = Rules();
        foreach (int skill in new[] { 20, 130, 300 })
        {
            Assert.Equal(1.0, AthleticsMath.BlockCostPoints(r, Troop(skill), BlockKind.ShieldRightSide), 9);
            Assert.Equal(5.0, AthleticsMath.BlockCostPoints(r, Troop(skill), BlockKind.ShieldWrongSide), 9);
            Assert.Equal(2.0, AthleticsMath.BlockCostPoints(r, Troop(skill), BlockKind.WeaponParry), 9);
            Assert.Equal(0.75, AthleticsMath.BlockCostPoints(r, Troop(skill, hero: true), BlockKind.ShieldRightSide), 9);
            Assert.Equal(3.75, AthleticsMath.BlockCostPoints(r, Troop(skill, hero: true), BlockKind.ShieldWrongSide), 9);
            Assert.Equal(1.5, AthleticsMath.BlockCostPoints(r, Troop(skill, hero: true), BlockKind.WeaponParry), 9);
            Assert.Equal(0.5625, AthleticsMath.BlockCostPoints(r, Troop(skill, hero: true, leader: true), BlockKind.ShieldRightSide), 9);
            Assert.Equal(2.8125, AthleticsMath.BlockCostPoints(r, Troop(skill, hero: true, leader: true), BlockKind.ShieldWrongSide), 9);
            Assert.Equal(1.125, AthleticsMath.BlockCostPoints(r, Troop(skill, hero: true, leader: true), BlockKind.WeaponParry), 9);
        }
        Assert.Equal(0.0, AthleticsMath.BlockCostPoints(r, Troop(), BlockKind.None), 9);
        Assert.Equal(10.0, AthleticsMath.BlowCostPoints(r, Troop()), 9);        // the blow's own price is untouched
        Assert.Equal(3.0, AthleticsMath.KickOrBashCostPoints(r, Troop()), 9);   // and the kick's
    }

    [Fact]
    public void The_three_settings_sit_after_the_kick_cost_and_are_read_live()
    {
        var all = SettingsSchema.All.ToList();
        int kick = all.IndexOf(SettingsSchema.CostPerKickOrBash);
        Assert.Same(SettingsSchema.CostPerShieldBlock, all[kick + 1]);
        Assert.Same(SettingsSchema.CostPerWrongSideShieldBlock, all[kick + 2]);
        Assert.Same(SettingsSchema.CostPerWeaponParry, all[kick + 3]);
        foreach (var p in new[] { SettingsSchema.CostPerShieldBlock, SettingsSchema.CostPerWrongSideShieldBlock, SettingsSchema.CostPerWeaponParry })
        {
            Assert.Equal(SettingsSchema.AthleticsGroup, p.Group);
            Assert.Equal(0, p.Min);
            Assert.Equal(20, p.Max);
        }

        var s = new TraxSettings(); // DESIGN's initial values: 1 / 5 / 2
        var r = AthleticsRules.From(s);
        Assert.Equal(1f, r.CostPerShieldBlock);
        Assert.Equal(5f, r.CostPerWrongSideShieldBlock);
        Assert.Equal(2f, r.CostPerWeaponParry);
        s.Set(SettingsSchema.CostPerWrongSideShieldBlock, 8, SettingSources.Mcm);
        s.Set(SettingsSchema.CostPerWeaponParry, 25, SettingSources.Mcm); // above the range: clamped to 20
        r = AthleticsRules.From(s);
        Assert.Equal(8f, r.CostPerWrongSideShieldBlock);
        Assert.Equal(20f, r.CostPerWeaponParry);
        Assert.Equal(8.0, AthleticsMath.BlockCostPoints(r, Troop(), BlockKind.ShieldWrongSide), 9);
    }

    [Fact]
    public void A_block_drains_like_a_kick_restarts_the_refill_delay_and_is_never_a_blow()
    {
        var r = Rules();
        var f = Troop(); // the floor: 50
        var o = AthleticsMath.ChargeBlock(f, r, BlockKind.ShieldRightSide, 10);
        Assert.True(o.Charged);
        Assert.Equal(1.0, o.Cost, 9);
        Assert.Equal(50, o.Before, 9);
        Assert.Equal(49, o.After, 9);
        o = AthleticsMath.ChargeBlock(f, r, BlockKind.ShieldWrongSide, 11);
        Assert.Equal(44, o.After, 9);
        o = AthleticsMath.ChargeBlock(f, r, BlockKind.WeaponParry, 12);
        Assert.Equal(42, o.After, 9);
        Assert.Equal(42.0 / 50, f.Fraction, 9);
        Assert.Equal(3, f.BlocksPaid);
        Assert.Equal(0, f.Blows);
        Assert.Equal(0, f.KicksAndBashes);
        Assert.Equal(12, f.LastBlowTime); // effort: the refill delay restarts (step 18's rule for a kick)
        Assert.False(f.Regenerating);

        // wrong-side blocks alone take a fresh recruit below his peak line (37.5) on the 3rd and empty him on the 10th
        var g = Troop();
        for (int i = 1; i <= 10; i++)
        {
            o = AthleticsMath.ChargeBlock(g, r, BlockKind.ShieldWrongSide, i);
            if (i == 3) Assert.True(o.LeftPeak);
        }
        Assert.Equal(0, g.Fraction);
        Assert.True(g.Exhausted);
        Assert.True(o.EnteredExhaustion);
        o = AthleticsMath.ChargeBlock(g, r, BlockKind.ShieldWrongSide, 11); // never below 0
        Assert.Equal(0, o.After, 9);
        Assert.False(o.EnteredExhaustion);
    }

    [Fact]
    public void Free_at_cost_0_and_nothing_while_Athletics_or_the_mod_is_off()
    {
        foreach (var (r, kind) in new[]
                 {
                     (Rules(right: 0), BlockKind.ShieldRightSide), (Rules(wrong: 0), BlockKind.ShieldWrongSide), (Rules(parry: 0), BlockKind.WeaponParry),
                     (Rules(enabled: false), BlockKind.ShieldWrongSide), (Rules(mod: false), BlockKind.ShieldWrongSide), (Rules(), BlockKind.None),
                 })
        {
            var f = Troop();
            var o = AthleticsMath.ChargeBlock(f, r, kind, 5);
            Assert.False(o.Charged);
            Assert.Equal(1.0, f.Fraction);
            Assert.Equal(0, f.BlocksPaid);
            Assert.True(double.IsNegativeInfinity(f.LastBlowTime)); // not even the refill delay
        }
        // one kind free leaves the others priced
        Assert.True(AthleticsMath.ChargeBlock(Troop(), Rules(right: 0), BlockKind.WeaponParry, 5).Charged);
    }

    // ------------------------------------------------------------------ what is a block

    [Fact]
    public void Only_a_melee_blow_stopped_by_a_guard_is_a_block()
    {
        const int strike = 1, world = 2;
        Assert.Equal(BlockKind.ShieldRightSide, BlockMath.KindOf(false, false, false, false, true, true, BlockMath.ResultBlocked));
        Assert.Equal(BlockKind.ShieldWrongSide, BlockMath.KindOf(false, false, false, false, true, false, BlockMath.ResultBlocked));
        Assert.Equal(BlockKind.ShieldRightSide, BlockMath.KindOf(false, false, false, false, true, true, strike)); // the shield flag decides
        Assert.Equal(BlockKind.WeaponParry, BlockMath.KindOf(false, false, false, false, false, false, BlockMath.ResultParried));
        Assert.Equal(BlockKind.WeaponParry, BlockMath.KindOf(false, false, false, false, false, false, BlockMath.ResultBlocked));
        Assert.Equal(BlockKind.WeaponParry, BlockMath.KindOf(false, false, false, false, false, false, BlockMath.ResultChamberBlocked));
        Assert.Equal(BlockKind.None, BlockMath.KindOf(false, false, false, false, false, false, strike));            // landed
        Assert.Equal(BlockKind.None, BlockMath.KindOf(false, false, false, false, false, false, world));             // the wall
        Assert.Equal(BlockKind.None, BlockMath.KindOf(true, false, false, false, true, true, BlockMath.ResultBlocked));   // a missile: free
        Assert.Equal(BlockKind.None, BlockMath.KindOf(false, true, false, false, true, true, BlockMath.ResultBlocked));   // a kick / bash: not a blow
        Assert.Equal(BlockKind.None, BlockMath.KindOf(false, false, true, false, false, false, BlockMath.ResultBlocked)); // a horse charge
        Assert.Equal(BlockKind.None, BlockMath.KindOf(false, false, false, true, false, false, strike));             // the shield on his back
        Assert.Equal("shield block (WRONG side)", BlockMath.Name(BlockKind.ShieldWrongSide));
        Assert.Equal("CostPerWeaponParry", BlockMath.SettingKey(BlockKind.WeaponParry));
    }

    [Fact]
    public void One_charge_per_blocked_blow()
    {
        var t = new BlockTracker();
        Assert.True(t.IsNew(7, 3, 10.0));       // attacker 7's swing 3
        Assert.False(t.IsNew(7, 3, 10.05));     // the same swing touching the guard again
        Assert.False(t.IsNew(7, 3, 10.9));
        Assert.True(t.IsNew(7, 4, 11.0));       // his next swing, even chained at once
        Assert.True(t.IsNew(8, 4, 11.0));       // another man's blow at the same moment
        Assert.True(t.IsNew(7, 4, 11.1));       // back to 7: his swing 4 again - the LAST blow was 8's (only one remembered)
        Assert.False(t.IsNew(7, 4, 11.2));
        Assert.True(t.IsNew(7, 4, 12.3));       // 1 s later: a new blow whatever the counter says

        var c = new BlockTracker();             // no swing number (a couched lance, an untracked attacker): the time rule alone
        Assert.True(c.IsNew(9, 0, 5.0));
        Assert.False(c.IsNew(9, 0, 5.5));
        Assert.True(c.IsNew(9, 0, 6.0));
        Assert.True(c.IsNew(9, 0, 1.0));        // time went backwards (a new mission's clock): new
        c.Reset();
        Assert.True(c.IsNew(9, 0, 1.1));
    }

    // ------------------------------------------------------------------ the refill, straight again

    [Fact]
    public void At_100_the_refill_is_exactly_linear_empty_to_full_in_60_s()
    {
        var r = Rules(nearFull: 100);
        Assert.Equal(1.0, r.RegenNearFullShare);
        Assert.Equal(1.0 / 60, AthleticsMath.RegenRateAtEmpty(r), 12);
        foreach (double x in new[] { 0, 0.25, 0.5, 0.75, 1 }) Assert.Equal(1.0, AthleticsMath.RegenCurve(r, x), 12);
        foreach (double t in new[] { 1, 7.5, 15, 30, 45, 60 })
        {
            Assert.Equal(t / 60, AthleticsMath.RefillFrom(r, 0, t, 1.0), 12);
            Assert.Equal(t / 120, AthleticsMath.RefillFrom(r, 0, t, 0.5), 12);   // a full run: half the rate
        }
        Assert.Equal(60, AthleticsMath.RefillSeconds(r, 0, 1, 1.0), 9);
        Assert.Equal(30, AthleticsMath.RefillSeconds(r, 0, 0.5, 1.0), 9);
        Assert.Equal(45, AthleticsMath.RefillSeconds(r, 0, 0.75, 1.0), 9);
        Assert.Equal(15, AthleticsMath.RefillSeconds(r, 0.75, 1, 1.0), 9);        // the last quarter takes as long as any other

        // stepped through Regen at a walk: the same share every second, from empty to full
        var f = Troop();
        AthleticsMath.ChargeBlock(f, Rules(wrong: 20, nearFull: 100), BlockKind.ShieldWrongSide, 0);
        AthleticsMath.ChargeBlock(f, Rules(wrong: 20, nearFull: 100), BlockKind.ShieldWrongSide, 0);
        AthleticsMath.ChargeBlock(f, Rules(wrong: 20, nearFull: 100), BlockKind.ShieldWrongSide, 0); // 50 → 0
        Assert.Equal(0, f.Fraction);
        double now = r.RegenDelaySeconds;          // the delay is over
        double prev = f.Fraction;
        for (int i = 0; i < 59; i++)
        {
            now += 1;
            AthleticsMath.Regen(f, r, now, 1.0, 0f, 5f);
            Assert.Equal(1.0 / 60, f.Fraction - prev, 9);
            prev = f.Fraction;
        }
        now += 1;
        AthleticsMath.Regen(f, r, now, 1.0, 0f, 5f);
        Assert.Equal(1.0, f.Fraction, 9);
    }

    // ------------------------------------------------------------------ the migration (format 5)

    /// <summary>defaults.json's shipped values after step 22 - the tests run on DESIGN's initial ones (0.7, 50, 100),
    /// so they are handed in.</summary>
    private static double Shipped22(ParamDef p) =>
        p == SettingsSchema.MinMoveSpeedMultiplier ? 0.3 : p == SettingsSchema.RegenRateNearFullPercent ? 100 : p == SettingsSchema.AttackAnimationMinPercent ? 85 : p.Default;

    [Fact]
    public void A_format_4_file_holding_the_old_refill_curve_and_run_floor_gets_the_new_ones_once()
    {
        Assert.Equal(5, ConfigFile.FormatVersion);
        // Anton's config.json of 2026-09-29 (read only): format 4, the run floor 0.6, the curve 50, the animation 85
        string old = "{ \"ConfigVersion\": 4, \"MinMoveSpeedMultiplier\": 0.6, \"RegenRateNearFullPercent\": 50, \"AttackAnimationMinPercent\": 85, "
                     + "\"MountMinSpeedMultiplier\": 0.6, \"CostPerKickOrBash\": 3.0, \"DamageRandomPercent\": 40 }";
        var read = ConfigFile.Read(old);
        var notes = ConfigFile.Migrate(read, Shipped22);
        Assert.Equal(2, notes.Count);
        Assert.Contains("RegenRateNearFullPercent: 50 → 100 (format 4 → 5, the old default; step 22, Anton for slower battles: the refill is a straight line again - empty to full in the refill time at a walk, no faster when low)", notes);
        Assert.Contains("MinMoveSpeedMultiplier: 0.6 → 0.3 (format 4 → 5, the old default; step 22, Anton for slower battles: an empty man runs at 30% of his pace again)", notes);
        Assert.Equal(0.3, read.Values["MinMoveSpeedMultiplier"], 9);
        Assert.Equal(100, read.Values["RegenRateNearFullPercent"]);
        Assert.Equal(85, read.Values["AttackAnimationMinPercent"]);      // format 4 is past step 20b
        Assert.Equal(0.6, read.Values["MountMinSpeedMultiplier"], 9);    // another key holding 0.6: never touched
        Assert.Equal(40, read.Values["DamageRandomPercent"]);
        Assert.False(read.Values.ContainsKey("CostPerShieldBlock"));     // a new key: missing → its default (ConfigStore)
        Assert.Contains(SettingsSchema.CostPerShieldBlock, read.Missing);
        Assert.Equal(2, ConfigFile.Migrate(read, Shipped22).Count);       // idempotent: nothing new the second time

        // his own choices (not the old defaults) stay - each key on its own
        var own = ConfigFile.Read("{ \"ConfigVersion\": 4, \"MinMoveSpeedMultiplier\": 0.5, \"RegenRateNearFullPercent\": 70 }");
        Assert.Empty(ConfigFile.Migrate(own, Shipped22));
        Assert.Equal(0.5, own.Values["MinMoveSpeedMultiplier"], 9);
        Assert.Equal(70, own.Values["RegenRateNearFullPercent"]);
        var half = ConfigFile.Read("{ \"ConfigVersion\": 4, \"MinMoveSpeedMultiplier\": 0.45, \"RegenRateNearFullPercent\": 50 }");
        Assert.Single(ConfigFile.Migrate(half, Shipped22));
        Assert.Equal(0.45, half.Values["MinMoveSpeedMultiplier"], 9);
        Assert.Equal(100, half.Values["RegenRateNearFullPercent"]);

        // a format-5 file is never migrated (his 0.6 / 50 there are his own)
        var now = ConfigFile.Read("{ \"ConfigVersion\": 5, \"MinMoveSpeedMultiplier\": 0.6, \"RegenRateNearFullPercent\": 50 }");
        Assert.Empty(ConfigFile.Migrate(now, Shipped22));
        Assert.Equal(0.6, now.Values["MinMoveSpeedMultiplier"], 9);

        // a format-3 file: its curve 50 moves too (the key came with format 3); its 0.7 goes straight to 0.3 (step 20b's
        // rule, the default of today); a 0.6 there was never a default - his own
        var three = ConfigFile.Read("{ \"ConfigVersion\": 3, \"MinMoveSpeedMultiplier\": 0.7, \"RegenRateNearFullPercent\": 50 }");
        Assert.Equal(2, ConfigFile.Migrate(three, Shipped22).Count);
        Assert.Equal(0.3, three.Values["MinMoveSpeedMultiplier"], 9);
        Assert.Equal(100, three.Values["RegenRateNearFullPercent"]);
        var threeOwn = ConfigFile.Read("{ \"ConfigVersion\": 3, \"MinMoveSpeedMultiplier\": 0.6 }");
        Assert.Empty(ConfigFile.Migrate(threeOwn, Shipped22));
        Assert.Equal(0.6, threeOwn.Values["MinMoveSpeedMultiplier"], 9);

        // a format-2 file: its 0.3 IS today's default - nothing to push; a curve there (the key did not exist yet) is his own
        var two = ConfigFile.Read("{ \"ConfigVersion\": 2, \"MinMoveSpeedMultiplier\": 0.3, \"RegenRateNearFullPercent\": 50 }");
        var twoNotes = ConfigFile.Migrate(two, Shipped22);
        Assert.DoesNotContain(twoNotes, n => n.StartsWith("MinMoveSpeedMultiplier", StringComparison.Ordinal));
        Assert.DoesNotContain(twoNotes, n => n.StartsWith("RegenRateNearFullPercent", StringComparison.Ordinal));
        Assert.Equal(0.3, two.Values["MinMoveSpeedMultiplier"], 9);
        Assert.Equal(50, two.Values["RegenRateNearFullPercent"]);

        // on DESIGN's initial curve (50) there is nothing to push for it - the default is where it was
        var design = ConfigFile.Read("{ \"ConfigVersion\": 4, \"RegenRateNearFullPercent\": 50 }");
        Assert.Empty(ConfigFile.Migrate(design));
    }

    // ------------------------------------------------------------------ the summary words

    [Fact]
    public void The_settings_sentence_names_the_block_costs()
    {
        string text = AthleticsStats.DescribeRules(Rules());
        Assert.Contains("; a blocked blow costs the defender: shield right side 1.00 / wrong side 5.00 / weapon parry 2.00 points (a hero x0.75, a party leader x0.56)", text);
        Assert.Contains("the same rate all the way (RegenRateNearFullPercent 100)", AthleticsStats.DescribeRules(Rules(nearFull: 100)));
    }
}

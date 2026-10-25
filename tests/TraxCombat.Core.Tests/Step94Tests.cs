using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 94 - A BIGGER BASE POOL, CHEAPER SWINGS (Toni 2026-10-25: "no action at all for T1 troops in the early stages ... swings a few times
/// then waits and waits" - the refill takes 60 s for any pool, so a 50-point bar fitted 1-2 swings per rest; AI_NOTES "Step 94", DESIGN decision
/// 86): the pool = max(AthleticsPoolBase + AthleticsPoolPerSkill × skill, AthleticsPoolFloor, his tier floor) - shipped 100 + 0.5 × skill for
/// everyone, heroes and you too (the tier floors still never for heroes); CostPerBlow 10 → 7, CostPerKickOrBash 8 → 6, the block costs
/// unchanged; config format 23. The tests run on DESIGN's initial values, so the shipped ones are set here.
/// </summary>
public class Step94Tests
{
    /// <summary>defaults.json's step-94 values (DESIGN's initial: the base 100, 1.0 a skill point, a swing 10, a kick 3, the blocks 1 / 5 / 2).</summary>
    private static double Shipped(ParamDef p) =>
        p == SettingsSchema.AthleticsPoolBase ? 100 : p == SettingsSchema.AthleticsPoolPerSkill ? 0.5
        : p == SettingsSchema.CostPerBlow ? 7.0 : p == SettingsSchema.CostPerKickOrBash ? 6.0
        : p == SettingsSchema.CostPerShieldBlock ? 3.0 : p == SettingsSchema.CostPerWrongSideShieldBlock ? 6.0
        : p == SettingsSchema.CostPerWeaponParry ? 3.0 : p.Default;

    private static readonly ParamDef[] ShippedKeys =
    {
        SettingsSchema.AthleticsPoolBase, SettingsSchema.AthleticsPoolPerSkill, SettingsSchema.CostPerBlow, SettingsSchema.CostPerKickOrBash,
        SettingsSchema.CostPerShieldBlock, SettingsSchema.CostPerWrongSideShieldBlock, SettingsSchema.CostPerWeaponParry,
    };

    private static TraxSettings ShippedSettings()
    {
        var s = new TraxSettings();
        foreach (var p in ShippedKeys) s.Set(p, Shipped(p), SettingSources.File);
        return s;
    }

    private static AthleticsRules ShippedRules() => AthleticsRules.From(ShippedSettings());

    private static Fighter Man(int skill, int tier = -1, bool hero = false, bool leader = false) =>
        new() { AthleticsSkill = skill, Tier = tier, IsHero = hero, IsLeader = leader };

    [Fact]
    public void The_base_sits_beside_the_per_skill_multiplier_on_the_Advanced_page_ships_100_and_is_live()
    {
        var all = SettingsSchema.All.ToList();
        var p = SettingsSchema.AthleticsPoolBase;
        Assert.Equal("AthleticsPoolBase", p.Key);
        Assert.Equal(all.IndexOf(SettingsSchema.AthleticsPoolPerSkill) - 1, all.IndexOf(p));
        Assert.Equal(all.IndexOf(SettingsSchema.AthleticsFloorT6) + 1, all.IndexOf(p));
        Assert.Same(SettingsSchema.AthleticsGroup, p.Group);
        Assert.Equal(SettingsPage.Advanced, p.Group.Page);
        Assert.Equal(ApplyTiming.Live, p.Timing);
        Assert.Equal(ParamType.Int, p.Type);
        Assert.Equal(0, p.Min, 9);
        Assert.Equal(1000, p.Max, 9);
        Assert.Equal(100, p.Default, 9); // DESIGN's initial value is the shipped one
        Assert.Contains("Heroes and you too", p.Description, StringComparison.Ordinal);

        string defaults = DefaultsFile.ReadEmbeddedText();
        foreach (var line in new[] { "\"AthleticsPoolBase\": 100,", "\"AthleticsPoolPerSkill\": 0.5,", "\"CostPerBlow\": 7.0,", "\"CostPerKickOrBash\": 6.0,",
                     "\"CostPerShieldBlock\": 3.0,", "\"CostPerWrongSideShieldBlock\": 6.0,", "\"CostPerWeaponParry\": 3.0," })
            Assert.Contains(line, defaults, StringComparison.Ordinal);
        Assert.Equal(26, ConfigFile.FormatVersion);

        var s = ShippedSettings();
        Assert.Equal(110.0, AthleticsMath.PoolPoints(AthleticsRules.From(s), Man(20)), 9);
        s.Set(SettingsSchema.AthleticsPoolBase, 200, SettingSources.Mcm);
        Assert.Equal(210.0, AthleticsMath.PoolPoints(AthleticsRules.From(s), Man(20)), 9); // live: read at use
    }

    [Theory]
    [InlineData(0, 100.0)]
    [InlineData(20, 110.0)]   // a recruit
    [InlineData(60, 130.0)]   // a Banner Knight's skill, as a hero / without a tier
    [InlineData(100, 150.0)]
    [InlineData(140, 170.0)]  // a Druzhinnik Champion
    [InlineData(233, 216.5)]  // a big-skill hero
    public void The_pool_is_100_plus_half_the_skill(int skill, double pool)
    {
        var r = ShippedRules();
        Assert.Equal(pool, AthleticsMath.PoolPoints(in r, Man(skill)), 9);
        Assert.Equal(pool, AthleticsMath.PoolPointsForSkill(in r, skill), 9);
        Assert.Equal(pool, AthleticsMath.PoolPoints(in r, Man(skill, hero: true)), 9);   // heroes too
        Assert.False(AthleticsMath.IsAtFloor(in r, skill));                              // 100 is above the 50 floor for everyone
    }

    [Fact]
    public void The_tier_floors_still_lift_troops_above_the_new_formula_never_a_hero()
    {
        var r = ShippedRules();
        Assert.Equal(150.0, AthleticsMath.PoolPoints(in r, Man(60, 6)), 9);   // the Banner Knight: 130 → T6's 150
        Assert.True(AthleticsMath.IsLiftedByTier(in r, 60, 6));
        Assert.Equal(110.0, AthleticsMath.PoolPoints(in r, Man(20, 1)), 9);   // a recruit: 110 > T1's 50
        Assert.Equal(120.0, AthleticsMath.PoolPoints(in r, Man(30, 5)), 9);   // 115 → T5's 120
        Assert.Equal(170.0, AthleticsMath.PoolPoints(in r, Man(140, 6)), 9);  // the champion's own 170 > 150
        Assert.Equal(130.0, AthleticsMath.PoolPoints(in r, Man(60, 6, hero: true)), 9); // a hero: the formula, never his tier
        Assert.Equal(216.5, AthleticsMath.PoolPoints(in r, Man(233, 6, hero: true)), 9);
    }

    [Fact]
    public void Base_0_is_the_old_formula_and_the_master_switch_comes_first()
    {
        var s = ShippedSettings();
        s.Set(SettingsSchema.AthleticsPoolBase, 0, SettingSources.Mcm);
        s.Set(SettingsSchema.AthleticsPoolPerSkill, 1.0, SettingSources.Mcm);
        var r = AthleticsRules.From(s);
        foreach (int skill in new[] { 0, 20, 49, 50, 60, 130, 233 })
            Assert.Equal(Math.Max(50.0, skill), AthleticsMath.PoolPointsForSkill(in r, skill), 9);
        Assert.Equal(150.0, AthleticsMath.PoolPoints(in r, Man(60, 6)), 9); // step 93 exactly
        Assert.True(AthleticsMath.IsAtFloor(in r, 20));
        Assert.Equal("the Athletics skill x1.00, at least 50", AthleticsMath.FormulaText(in r)); // the old words, unchanged
        Assert.Equal("100 + the Athletics skill x0.50, at least 50", AthleticsMath.FormulaText(ShippedRules()));
        // the constructor's default is no base (the older tests)
        var plain = new AthleticsRules(true, 50, 1f, 75, true, 10, true, 0.75f, 0.75f, 3, 20, 0.7f, 1f, true, 2, 1.5f, 60, 0.5f, 0.4f, 100);
        Assert.Equal(0, plain.PoolBase);
        Assert.Equal(60.0, AthleticsMath.PoolPointsForSkill(in plain, 60), 9);

        var off = ShippedSettings();
        off.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
        var ro = AthleticsRules.From(off);
        Assert.Equal(100, ro.PoolBase);
        Assert.Equal(0, ro.EffectiveBase);                                   // the master switch: no base
        Assert.Equal(50.0, AthleticsMath.PoolPointsForSkill(in ro, 20), 9);
    }

    [Fact]
    public void A_live_change_of_the_base_re_pools_him_and_he_keeps_his_share()
    {
        var s = ShippedSettings();
        var f = Man(20);
        AthleticsMath.Charge(f, AthleticsRules.From(s), 0);
        double share = f.Fraction;
        Assert.Equal(1 - 7.0 / 110, share, 9);
        s.Set(SettingsSchema.AthleticsPoolBase, 0, SettingSources.Mcm);
        Assert.Equal(share, f.Fraction, 9);
        Assert.Equal(share * 50, AthleticsMath.Points(AthleticsRules.From(s), f), 9);
    }

    [Fact]
    public void Swings_cost_7_kicks_bashes_and_pushes_6_blocks_unchanged_heroes_and_leaders_keep_their_multipliers()
    {
        var r = ShippedRules();
        var man = Man(20);
        var hero = Man(20, hero: true);
        var leader = Man(20, hero: true, leader: true);
        Assert.Equal(7.0, AthleticsMath.BlowCostPoints(in r, man), 9);
        Assert.Equal(5.25, AthleticsMath.BlowCostPoints(in r, hero), 9);
        Assert.Equal(3.9375, AthleticsMath.BlowCostPoints(in r, leader), 9);
        Assert.Equal(6.0, AthleticsMath.KickOrBashCostPoints(in r, man), 9);
        Assert.Equal(4.5, AthleticsMath.KickOrBashCostPoints(in r, hero), 9);
        Assert.Equal(3.375, AthleticsMath.KickOrBashCostPoints(in r, leader), 9);
        Assert.Equal(3.0, AthleticsMath.BlockCostPoints(in r, man, BlockKind.ShieldRightSide), 9);
        Assert.Equal(6.0, AthleticsMath.BlockCostPoints(in r, man, BlockKind.ShieldWrongSide), 9);
        Assert.Equal(3.0, AthleticsMath.BlockCostPoints(in r, man, BlockKind.WeaponParry), 9);
        // a recruit's 110 empties on the 16th swing (15 whole ones fit), a legionary's 165 on the 24th, a 233 hero's 216.5 at 5.25 on the 42nd
        Assert.Equal(16, (int)Math.Ceiling(110 / 7.0));
        Assert.Equal(24, (int)Math.Ceiling(165 / 7.0));
        Assert.Equal(42, (int)Math.Ceiling(216.5 / 5.25));
        Assert.Contains("At 7 a recruit (a 110-point bar) is empty after 16 blows, a legionary (165) after 24.", SettingsSchema.CostPerBlow.Description, StringComparison.Ordinal);
        Assert.Contains("6 costs a hero 4.5 and a party leader 3.4", SettingsSchema.CostPerKickOrBash.Description, StringComparison.Ordinal);
        Assert.Contains("100 points plus half his Athletics skill", SettingsSchema.AthleticsEnabled.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void The_summary_says_the_new_formula()
    {
        var r = ShippedRules();
        Assert.StartsWith("ON - pool = 100 + the Athletics skill x0.50, at least 50; full strength at 75% of the pool and above; cost per blow 7.0 / hero 5.3 / party leader 3.9 points; a kick or shield bash 6.00 / hero 4.50 / party leader 3.38 points",
            AthleticsStats.DescribeRules(in r), StringComparison.Ordinal);
        var stats = new AthleticsStats();
        foreach (int skill in new[] { 20, 20, 100, 140 }) stats.AddFighter(skill, skillKnown: true);
        stats.AddFighter(60, true, 6);
        stats.PlayerSeen = true;
        stats.PlayerSkill = 233;
        var line = stats.SummaryLines(in r, c => "a" + c, new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, FormationAthleticsStats>>())
            .Single(l => l.StartsWith("Athletics pools", StringComparison.Ordinal));
        Assert.StartsWith("Athletics pools (100 + the Athletics skill x0.50, at least 50; settings at the end): 5 fighters - min 110 / avg 138.0 / max 170; 0 at the floor;", line, StringComparison.Ordinal);
        Assert.Contains("you 217 (skill 233)", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_23_moves_the_untouched_values_once_and_keeps_a_tuned_one()
    {
        var read = ConfigFile.Read("{ \"ConfigVersion\": 22, \"AthleticsPoolPerSkill\": 1.0, \"CostPerBlow\": 10.0, \"CostPerKickOrBash\": 8.0, \"CostPerShieldBlock\": 3.0 }");
        var notes = ConfigFile.Migrate(read, Shipped);
        Assert.Equal(3, notes.Count);
        Assert.Equal("AthleticsPoolPerSkill: 1.0 → 0.5 (format 22 → 26, the old default; step 94, Toni: a bigger base pool, cheaper swings - the bar is 100 + half the Athletics skill - each skill point adds 0.5 to the base)", notes[0]);
        Assert.StartsWith("CostPerBlow: 10.0 → 7.0 (format 22 → 26, the old default; step 94", notes[1], StringComparison.Ordinal);
        Assert.StartsWith("CostPerKickOrBash: 8.0 → 6.0 (format 22 → 26, the old default; step 94", notes[2], StringComparison.Ordinal);
        Assert.Equal(0.5, read.Values["AthleticsPoolPerSkill"], 9);
        Assert.Equal(7, read.Values["CostPerBlow"], 9);
        Assert.Equal(6, read.Values["CostPerKickOrBash"], 9);
        Assert.Equal(3, read.Values["CostPerShieldBlock"], 9);              // the blocks untouched
        Assert.False(read.Values.ContainsKey("AthleticsPoolBase"));         // a new key: the file gets the default like any missing key
        Assert.Equal(3, ConfigFile.Migrate(read, Shipped).Count);            // idempotent (the notes list is the read's own)

        // his own values stay - and are said once each
        var mine = ConfigFile.Read("{ \"ConfigVersion\": 22, \"AthleticsPoolPerSkill\": 0.8, \"CostPerBlow\": 12.0, \"CostPerKickOrBash\": 5.0 }");
        var kept = ConfigFile.Migrate(mine, Shipped);
        Assert.Equal(3, kept.Count);
        Assert.StartsWith("AthleticsPoolPerSkill: 0.8 kept - your own value (format 22 → 26; the new default 0.5 is not pushed over it; step 94", kept[0], StringComparison.Ordinal);
        Assert.StartsWith("CostPerBlow: 12.0 kept - your own value (format 22 → 26; the new default 7.0", kept[1], StringComparison.Ordinal);
        Assert.StartsWith("CostPerKickOrBash: 5.0 kept - your own value (format 22 → 26; the new default 6.0", kept[2], StringComparison.Ordinal);
        Assert.Equal(12, mine.Values["CostPerBlow"], 9);

        // a format-23 file is never touched; a file without the keys gets nothing to say
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 23, \"AthleticsPoolPerSkill\": 1.0, \"CostPerBlow\": 10.0 }"), Shipped));
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 22 }"), Shipped));
        // on DESIGN's initial values (the older tests) nothing moves: today's default is the old one
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 22, \"AthleticsPoolPerSkill\": 1.0, \"CostPerBlow\": 10.0 }"), p => p.Default));
    }

    [Fact]
    public void Older_files_chain_to_todays_values_in_one_note_each()
    {
        // format 15-18: CostPerBlow 10 (step 71's) → 7 by step 94; the kick's 6 (step 71's) is today's default already - nothing to move
        var f18 = ConfigFile.Read("{ \"ConfigVersion\": 18, \"CostPerBlow\": 10.0, \"CostPerKickOrBash\": 6.0 }");
        var n18 = ConfigFile.Migrate(f18, Shipped);
        Assert.Single(n18, n => n.StartsWith("CostPerBlow: 10.0 → 7.0 (format 18 → 26, the old default; step 94", StringComparison.Ordinal));
        Assert.DoesNotContain(n18, n => n.StartsWith("CostPerKickOrBash", StringComparison.Ordinal));
        Assert.Equal(7, f18.Values["CostPerBlow"], 9);
        Assert.Equal(6, f18.Values["CostPerKickOrBash"], 9);
        // format 14: CostPerBlow 8 → straight to 7 through step 71's block (its wording), never touched again by step 94
        var f14 = ConfigFile.Read("{ \"ConfigVersion\": 14, \"CostPerBlow\": 8.0, \"CostPerKickOrBash\": 5.0 }");
        var n14 = ConfigFile.Migrate(f14, Shipped);
        Assert.Single(n14, n => n.StartsWith("CostPerBlow: 8.0 → 7.0 (format 14 → 26, the old default; step 71", StringComparison.Ordinal));
        Assert.Single(n14, n => n.StartsWith("CostPerKickOrBash: 5.0 → 6.0 (format 14 → 26, the old default; step 71", StringComparison.Ordinal));
        Assert.Equal(7, f14.Values["CostPerBlow"], 9);
        Assert.Equal(6, f14.Values["CostPerKickOrBash"], 9);
        // a format-5 file's per-skill 1.0 moves too (the key since step 5c)
        var f5 = ConfigFile.Read("{ \"ConfigVersion\": 5, \"AthleticsPoolPerSkill\": 1.0 }");
        Assert.Single(ConfigFile.Migrate(f5, Shipped), n => n.StartsWith("AthleticsPoolPerSkill: 1.0 → 0.5 (format 5 → 26", StringComparison.Ordinal));
    }
}

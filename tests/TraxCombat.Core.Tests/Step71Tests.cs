using System;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 71 - MEN TIRE FASTER STILL (Toni 2026-10-19: "bump up how fast they tire, back to 10 for the swings and suggest for the rest
/// tiring moves, they need to tire faster still"; AI_NOTES "Step 71", DESIGN decision 65): CostPerBlow 8 → 10, CostPerKickOrBash 5 → 6,
/// CostPerWrongSideShieldBlock 5 → 6, CostPerShieldBlock 2 → 3, CostPerWeaponParry 2 → 3 in defaults.json; the hero / party-leader multipliers
/// kept; the refill unchanged (FullRegenSecondsStanding 60); config format 15.
/// </summary>
public class Step71Tests
{
    /// <summary>defaults.json's step-71 costs (the tests run on DESIGN's initial 10 / 3 / 1 / 5 / 2).</summary>
    private static double Shipped(ParamDef p) =>
        p == SettingsSchema.CostPerBlow ? 10.0 : p == SettingsSchema.CostPerKickOrBash ? 6.0
        : p == SettingsSchema.CostPerShieldBlock ? 3.0 : p == SettingsSchema.CostPerWrongSideShieldBlock ? 6.0
        : p == SettingsSchema.CostPerWeaponParry ? 3.0 : p.Default;

    private static AthleticsRules ShippedRules()
    {
        var s = new TraxSettings();
        foreach (var p in new[] { SettingsSchema.CostPerBlow, SettingsSchema.CostPerKickOrBash, SettingsSchema.CostPerShieldBlock,
                     SettingsSchema.CostPerWrongSideShieldBlock, SettingsSchema.CostPerWeaponParry })
            s.Set(p, Shipped(p), "test");
        return AthleticsRules.From(s);
    }

    [Fact]
    public void The_new_costs_tire_everyone_more_heroes_and_leaders_keep_their_multipliers()
    {
        var r = ShippedRules();
        var man = new Fighter();
        var hero = new Fighter { IsHero = true };
        var leader = new Fighter { IsHero = true, IsLeader = true };
        Assert.Equal(10.0, AthleticsMath.BlowCostPoints(in r, man), 6);
        Assert.Equal(7.5, AthleticsMath.BlowCostPoints(in r, hero), 6);
        Assert.Equal(5.625, AthleticsMath.BlowCostPoints(in r, leader), 6);
        Assert.Equal(6.0, AthleticsMath.KickOrBashCostPoints(in r, man), 6);
        Assert.Equal(4.5, AthleticsMath.KickOrBashCostPoints(in r, hero), 6);
        Assert.Equal(3.375, AthleticsMath.KickOrBashCostPoints(in r, leader), 6);
        Assert.Equal(3.0, AthleticsMath.BlockCostPoints(in r, man, BlockKind.ShieldRightSide), 6);
        Assert.Equal(2.25, AthleticsMath.BlockCostPoints(in r, hero, BlockKind.ShieldRightSide), 6);
        Assert.Equal(1.6875, AthleticsMath.BlockCostPoints(in r, leader, BlockKind.ShieldRightSide), 6);
        Assert.Equal(6.0, AthleticsMath.BlockCostPoints(in r, man, BlockKind.ShieldWrongSide), 6);
        Assert.Equal(3.375, AthleticsMath.BlockCostPoints(in r, leader, BlockKind.ShieldWrongSide), 6);
        Assert.Equal(3.0, AthleticsMath.BlockCostPoints(in r, man, BlockKind.WeaponParry), 6);
        Assert.Equal(2.25, AthleticsMath.BlockCostPoints(in r, hero, BlockKind.WeaponParry), 6);
        // the player page's hint: a recruit's 50-point bar empty after 5 blows (was 7 at 8), a legionary's 130 after 13 (was 17)
        Assert.Equal(5, (int)Math.Ceiling(50 / 10.0));
        Assert.Equal(13, (int)Math.Ceiling(130 / 10.0));
        Assert.Contains("At 10 a recruit (a 50-point bar) is empty after 5 blows, a legionary (130) after 13.", SettingsSchema.CostPerBlow.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_15_moves_the_untouched_values_once_and_keeps_a_tuned_one()
    {
        Assert.Equal(20, ConfigFile.FormatVersion);
        var read = ConfigFile.Read("{ \"ConfigVersion\": 14, \"CostPerBlow\": 8.0, \"CostPerWrongSideShieldBlock\": 5.0, \"CostPerShieldBlock\": 2.0, "
                                   + "\"CostPerKickOrBash\": 5.0, \"CostPerWeaponParry\": 2.0, \"FullRegenSecondsStanding\": 60.0 }");
        var notes = ConfigFile.Migrate(read, Shipped);
        Assert.Equal(5, notes.Count);
        Assert.Equal("CostPerBlow: 8.0 → 10.0 (format 14 → 20, the old default; step 71, Toni: the men tire faster still - a swing or shot costs 10)", notes[0]);
        Assert.StartsWith("CostPerWrongSideShieldBlock: 5.0 → 6.0 (format 14 → 20, the old default; step 71", notes[1], StringComparison.Ordinal);
        Assert.StartsWith("CostPerShieldBlock: 2.0 → 3.0 (format 14 → 20, the old default; step 71", notes[2], StringComparison.Ordinal);
        Assert.StartsWith("CostPerKickOrBash: 5.0 → 6.0 (format 14 → 20, the old default; step 71", notes[3], StringComparison.Ordinal);
        Assert.StartsWith("CostPerWeaponParry: 2.0 → 3.0 (format 14 → 20, the old default; step 71", notes[4], StringComparison.Ordinal);
        Assert.Equal(10, read.Values["CostPerBlow"], 9);
        Assert.Equal(6, read.Values["CostPerWrongSideShieldBlock"], 9);
        Assert.Equal(3, read.Values["CostPerShieldBlock"], 9);
        Assert.Equal(6, read.Values["CostPerKickOrBash"], 9);
        Assert.Equal(3, read.Values["CostPerWeaponParry"], 9);
        Assert.Equal(60, read.Values["FullRegenSecondsStanding"], 9); // the refill is not touched
        Assert.Equal(5, ConfigFile.Migrate(read, Shipped).Count); // idempotent (the notes list is the read's own)

        // his own values stay - and are said once
        var mine = ConfigFile.Read("{ \"ConfigVersion\": 14, \"CostPerBlow\": 9.0, \"CostPerWeaponParry\": 0.0, \"CostPerKickOrBash\": 4.0 }");
        var kept = ConfigFile.Migrate(mine, Shipped);
        Assert.Equal(3, kept.Count);
        Assert.StartsWith("CostPerBlow: 9.0 kept - your own value (format 14 → 20; the new default 10.0 is not pushed over it; step 71", kept[0], StringComparison.Ordinal);
        Assert.StartsWith("CostPerKickOrBash: 4.0 kept - your own value (format 14 → 20; the new default 6.0", kept[1], StringComparison.Ordinal);
        Assert.Equal(9, mine.Values["CostPerBlow"], 9);
        Assert.Equal(0, mine.Values["CostPerWeaponParry"], 9);
        Assert.Equal(4, mine.Values["CostPerKickOrBash"], 9);

        // already the new values, a file without the keys, a format-15 file: nothing to say
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 14, \"CostPerBlow\": 10.0, \"CostPerKickOrBash\": 6.0, \"CostPerWeaponParry\": 3.0 }"), Shipped));
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 14, \"DamageRandomPercent\": 40 }"), Shipped));
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 15, \"CostPerBlow\": 8.0, \"CostPerWeaponParry\": 2.0 }"), Shipped));
    }

    [Fact]
    public void Older_files_reach_todays_values_in_one_note_each()
    {
        // format 13: step 68 moves the 7 / 2 / 1 straight to today's values (one note each); step 71 the kick's 5 and the parry's 2
        var thirteen = ConfigFile.Read("{ \"ConfigVersion\": 13, \"CostPerBlow\": 7.0, \"CostPerWrongSideShieldBlock\": 2.0, \"CostPerShieldBlock\": 1.0, "
                                       + "\"CostPerKickOrBash\": 5.0, \"CostPerWeaponParry\": 2.0, \"FullRegenSecondsStanding\": 60.0 }");
        var notes = ConfigFile.Migrate(thirteen, Shipped);
        Assert.Equal(5, notes.Count);
        Assert.Single(notes, n => n.StartsWith("CostPerBlow: 7.0 → 10.0 (format 13 → 20, the old default; step 68", StringComparison.Ordinal));
        Assert.Single(notes, n => n.StartsWith("CostPerKickOrBash: 5.0 → 6.0 (format 13 → 20, the old default; step 71", StringComparison.Ordinal));
        Assert.Equal(10, thirteen.Values["CostPerBlow"], 9);
        Assert.Equal(6, thirteen.Values["CostPerWrongSideShieldBlock"], 9);
        Assert.Equal(3, thirteen.Values["CostPerShieldBlock"], 9);
        Assert.Equal(6, thirteen.Values["CostPerKickOrBash"], 9);
        Assert.Equal(3, thirteen.Values["CostPerWeaponParry"], 9);
        Assert.Equal(60, thirteen.Values["FullRegenSecondsStanding"], 9); // the refill is not touched
        // format 12: the kick's 3 goes through step 66 straight to 6 - one note, never a second "kept"
        var twelve = ConfigFile.Read("{ \"ConfigVersion\": 12, \"CostPerKickOrBash\": 3.0 }");
        var kick = ConfigFile.Migrate(twelve, Shipped);
        Assert.Single(kick);
        Assert.StartsWith("CostPerKickOrBash: 3.0 → 6.0 (format 12 → 20, the old default; step 66", kick[0], StringComparison.Ordinal);
        // format 13 with a hand-set 8 a swing (his own then): said once by step 68, not again by step 71
        var own = ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 13, \"CostPerBlow\": 8.0 }"), Shipped);
        Assert.Single(own);
        Assert.StartsWith("CostPerBlow: 8.0 kept - your own value (format 13 → 20; the new default 10.0", own[0], StringComparison.Ordinal);
        // a default that never moved says nothing about a hand-set value (step 71's MoveOrKeepIfMoved)
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 14, \"CostPerWeaponParry\": 4.0 }"), p => p.Default));
    }
}

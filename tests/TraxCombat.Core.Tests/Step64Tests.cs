using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 64 - THE WALL'S BLOCKS COUNT (Anton's looter battles 2026-10-19; AI_NOTES "Step 64", DESIGN decision 55): ~85% of an AI
/// shieldman's blocks read WRONG side even fresh (the native AI guesses its guard direction most of the time; a wall's poses are fixed),
/// so the correct-block rules never fired. A SHIELD block whose attacker stands within <c>ShieldFrontArcDegrees</c> of the blocker's facing
/// counts as a right-side block; outside it, the engine's verdict; parries keep the engine's direction.
/// </summary>
public class ShieldFrontArcTests
{
    private static ShieldArcRules Rules(bool mod = true, bool on = true, double degrees = 60) => new ShieldArcRules(mod, on, degrees);

    [Fact]
    public void The_two_settings_sit_on_the_Advanced_page_after_the_refill_rule_on_and_60_degrees_live()
    {
        var all = SettingsSchema.All.ToList();
        int refill = all.IndexOf(SettingsSchema.OnlyWrongSideBlocksStopRefill);
        Assert.Equal(refill + 1, all.IndexOf(SettingsSchema.ShieldFrontArcIsCorrect));
        Assert.Equal(refill + 2, all.IndexOf(SettingsSchema.ShieldFrontArcDegrees));
        foreach (var p in new[] { SettingsSchema.ShieldFrontArcIsCorrect, SettingsSchema.ShieldFrontArcDegrees })
        {
            Assert.Equal(SettingsPage.Advanced, p.Group.Page);
            Assert.Same(SettingsSchema.OnlyWrongSideBlocksStopRefill.Group, p.Group);
            Assert.Equal(ApplyTiming.Live, p.Timing);
        }
        var s = new TraxSettings();
        Assert.True(s.ShieldFrontArcIsCorrect);
        Assert.Equal(60, s.ShieldFrontArcDegrees);
        var r = ShieldArcRules.From(s);
        Assert.True(r.Enabled);
        Assert.Equal(60, r.Degrees);
        s.Set(SettingsSchema.ShieldFrontArcDegrees, 30, SettingSources.Mcm);
        Assert.Equal(30, ShieldArcRules.From(s).Degrees); // live
    }

    [Fact]
    public void A_wrong_side_shield_block_from_the_front_counts_right_outside_the_arc_the_game_decides()
    {
        var r = Rules();
        Assert.Equal(BlockKind.ShieldRightSide, ShieldArcMath.Resolve(in r, BlockKind.ShieldWrongSide, 0));
        Assert.Equal(BlockKind.ShieldRightSide, ShieldArcMath.Resolve(in r, BlockKind.ShieldWrongSide, 35));
        Assert.Equal(BlockKind.ShieldRightSide, ShieldArcMath.Resolve(in r, BlockKind.ShieldWrongSide, 60));
        Assert.Equal(BlockKind.ShieldWrongSide, ShieldArcMath.Resolve(in r, BlockKind.ShieldWrongSide, 61));
        Assert.Equal(BlockKind.ShieldWrongSide, ShieldArcMath.Resolve(in r, BlockKind.ShieldWrongSide, 170));
        Assert.Equal(BlockKind.ShieldWrongSide, ShieldArcMath.Resolve(in r, BlockKind.ShieldWrongSide, double.NaN)); // not read = the game's
        // the game's right side stays right from anywhere; parries and landed blows are never touched
        Assert.Equal(BlockKind.ShieldRightSide, ShieldArcMath.Resolve(in r, BlockKind.ShieldRightSide, 170));
        Assert.Equal(BlockKind.WeaponParry, ShieldArcMath.Resolve(in r, BlockKind.WeaponParry, 0));
        Assert.Equal(BlockKind.None, ShieldArcMath.Resolve(in r, BlockKind.None, 0));
        Assert.True(ShieldArcMath.NeedsAngle(in r, BlockKind.ShieldWrongSide));
        Assert.False(ShieldArcMath.NeedsAngle(in r, BlockKind.ShieldRightSide)); // no read when nothing can change
        Assert.False(ShieldArcMath.NeedsAngle(in r, BlockKind.WeaponParry));
    }

    [Fact]
    public void The_master_switch_first_then_the_switch_then_the_angle_0_and_180()
    {
        var off = Rules(mod: false);
        Assert.False(off.Enabled);
        Assert.Equal("ModEnabled", off.OffBecause);
        Assert.Equal(BlockKind.ShieldWrongSide, ShieldArcMath.Resolve(in off, BlockKind.ShieldWrongSide, 0));
        Assert.False(ShieldArcMath.NeedsAngle(in off, BlockKind.ShieldWrongSide));
        var sw = Rules(on: false);
        Assert.Equal("ShieldFrontArcIsCorrect", sw.OffBecause);
        Assert.Equal(BlockKind.ShieldWrongSide, ShieldArcMath.Resolve(in sw, BlockKind.ShieldWrongSide, 0));
        Assert.Equal("ModEnabled", Rules(mod: false, on: false).OffBecause);
        var dead = Rules(degrees: 0);
        Assert.Equal(BlockKind.ShieldRightSide, ShieldArcMath.Resolve(in dead, BlockKind.ShieldWrongSide, 0));
        Assert.Equal(BlockKind.ShieldWrongSide, ShieldArcMath.Resolve(in dead, BlockKind.ShieldWrongSide, 1));
        var any = Rules(degrees: 180);
        Assert.Equal(BlockKind.ShieldRightSide, ShieldArcMath.Resolve(in any, BlockKind.ShieldWrongSide, 180));
        Assert.Equal(180, Rules(degrees: 400).Degrees);
        Assert.Equal(0, Rules(degrees: -5).Degrees);
        Assert.Contains("within 60° of the blocker's facing counts as a RIGHT-side block", Rules().Describe());
        Assert.Contains("off (ShieldFrontArcIsCorrect)", sw.Describe());
    }

    [Fact]
    public void The_summary_counts_the_games_verdict_against_ours_by_who_and_the_angle()
    {
        var r = Rules();
        var stats = new ShieldArcStats();
        Assert.Contains("no melee shield block this battle", stats.SummaryLine(in r));
        // the AI: 2 right by the game, 6 wrong - 4 from the front (turned), 1 from the side, 1 never looked at (an untracked attacker)
        stats.Note(false, BlockKind.ShieldRightSide, BlockKind.ShieldRightSide, double.NaN);
        stats.Note(false, BlockKind.ShieldRightSide, BlockKind.ShieldRightSide, double.NaN);
        foreach (double a in new[] { 5.0, 20, 40, 59 }) stats.Note(false, BlockKind.ShieldWrongSide, ShieldArcMath.Resolve(in r, BlockKind.ShieldWrongSide, a), a);
        stats.Note(false, BlockKind.ShieldWrongSide, ShieldArcMath.Resolve(in r, BlockKind.ShieldWrongSide, 100), 100);
        stats.Note(false, BlockKind.ShieldWrongSide, BlockKind.ShieldWrongSide, double.NaN);
        // you: one wrong from the front
        stats.Note(true, BlockKind.ShieldWrongSide, BlockKind.ShieldRightSide, 10);
        stats.Note(false, BlockKind.WeaponParry, BlockKind.WeaponParry, double.NaN); // a parry is no shield block
        Assert.Equal(2, stats.EngineRight(false));
        Assert.Equal(6, stats.EngineWrong(false));
        Assert.Equal(6, stats.OursRight(false));
        Assert.Equal(2, stats.OursWrong(false));
        Assert.Equal(4, stats.Turned(false));
        Assert.Equal(1, stats.Turned(true));
        Assert.Equal(1, stats.NotLooked);
        string line = stats.SummaryLine(in r);
        Assert.Contains("shield front arc (step 64, on, ±60°):", line);
        Assert.Contains("AI men's shield blocks 8: the game right 2 (25%) / WRONG 6 → ours right 6 (75%) / WRONG 2 - 4 wrong-side blocks from the front counted right", line);
        Assert.Contains("YOURS 1: the game right 0 (0%) / WRONG 1 → ours right 1 (100%) / WRONG 0", line);
        Assert.Contains("0-15° 2 (2), 15-30° 1 (1), 30-45° 1 (1), 45-60° 1 (1), 90-120° 1", line);
        Assert.Contains("not looked at 1", line);
        Assert.Equal(0, ShieldArcMath.Band(0));
        Assert.Equal(3, ShieldArcMath.Band(59.9));
        Assert.Equal(4, ShieldArcMath.Band(60));
        Assert.Equal(6, ShieldArcMath.Band(180));
        Assert.Equal("120-180°", ShieldArcMath.BandName(6));
    }

    [Fact]
    public void A_block_counted_right_by_the_arc_gets_the_correct_block_rules_cost_refill_and_the_riposte()
    {
        // the cost and the refill: a right-side block costs CostPerShieldBlock and lets the refill run (step 29)
        var s = new TraxSettings();
        var ar = AthleticsRules.From(s);
        Assert.Equal(SettingsSchema.CostPerShieldBlock.Key, BlockMath.SettingKey(ShieldArcMath.Resolve(Rules(), BlockKind.ShieldWrongSide, 20)));
        Assert.False(AthleticsMath.BlockRestartsRefill(in ar, BlockKind.ShieldRightSide));
        Assert.True(AthleticsMath.BlockRestartsRefill(in ar, BlockKind.ShieldWrongSide));
        // the impact model: the same blocked blow as a right-side block can riposte, as a wrong-side one never
        Assert.True(ImpactMath.EventOf(false, false, false, BlockKind.ShieldRightSide, out var right));
        Assert.Equal(ImpactEvent.RightBlock, right);
        Assert.True(ImpactMath.CanRiposte(right));
        Assert.True(ImpactMath.EventOf(false, false, false, BlockKind.ShieldWrongSide, out var wrong));
        Assert.False(ImpactMath.CanRiposte(wrong));
    }
}

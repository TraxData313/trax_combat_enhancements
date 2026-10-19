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

/// <summary>
/// Step 64 - THE AI CHARGE HITS AT SPEED (AI_NOTES "Step 64", DESIGN decision 56): Anton's looters spent their small store early and stopped
/// 3 m short ("charge sprints 153 men avg 1.39 s; ends: store empty 100, an enemy within 3 m 65"). A charge order's and a fever's sprint run
/// INTO contact (ended by his first attack or by standing still); a small charge store waits for an enemy within 10 m; the fever sprint
/// starts at 4-8 m (config format 12).
/// </summary>
public class ChargeIntoContactTests
{
    private static SprintRules Sprint(bool into = true, double waitBelow = 3, double waitMetres = 10, bool ai = true, bool mod = true) =>
        new(mod, true, true, ai, 20, 20, 2, 1.1, 30, 7, 0.5, 0.5, 0, 50, 80, 2, 1, false, into, waitBelow, waitMetres);

    [Fact]
    public void The_three_settings_follow_the_rout_switch_on_the_Advanced_page_on_3_s_and_10_m_live()
    {
        var all = SettingsSchema.All.ToList();
        int rout = all.IndexOf(SettingsSchema.SprintOnRout);
        Assert.Equal(rout + 1, all.IndexOf(SettingsSchema.SprintChargeIntoContact));
        Assert.Equal(rout + 2, all.IndexOf(SettingsSchema.SprintChargeWaitBelowSeconds));
        Assert.Equal(rout + 3, all.IndexOf(SettingsSchema.SprintChargeWaitMetres));
        foreach (var p in new[] { SettingsSchema.SprintChargeIntoContact, SettingsSchema.SprintChargeWaitBelowSeconds, SettingsSchema.SprintChargeWaitMetres })
        {
            Assert.Equal(SettingsPage.Advanced, p.Group.Page);
            Assert.Same(SettingsSchema.SprintOnRout.Group, p.Group);
            Assert.Equal(ApplyTiming.Live, p.Timing);
        }
        var s = new TraxSettings();
        var r = SprintRules.From(s);
        Assert.True(r.ChargeIntoContact);
        Assert.Equal(3, r.ChargeWaitBelowSeconds, 6);
        Assert.Equal(10, r.ChargeWaitMetres, 6);
        s.Set(SettingsSchema.SprintChargeIntoContact, false, SettingSources.Mcm);
        Assert.False(SprintRules.From(s).ChargeIntoContact);
        Assert.Contains("a charge's (and a fever's) sprint runs INTO contact", r.Describe("LeftControl"));
        Assert.Contains("a charge sprint shorter than 3.0 s waits for an enemy within 10 m", r.ChargeText());
        Assert.Contains("ends at an enemy within 3 m (SprintChargeIntoContact off)", Sprint(into: false).ChargeText());
        Assert.Contains("whatever its store (SprintChargeWaitBelowSeconds 0)", Sprint(waitBelow: 0).ChargeText());
    }

    [Fact]
    public void A_charge_or_a_fever_sprint_runs_into_contact_an_advance_or_a_move_never()
    {
        var s = Sprint();
        Assert.True(SprintMath.IntoContact(in s, SprintOrder.Charge, fever: false));
        Assert.False(SprintMath.IntoContact(in s, SprintOrder.Advance, fever: false));
        Assert.False(SprintMath.IntoContact(in s, SprintOrder.Move, fever: false));
        Assert.True(SprintMath.IntoContact(in s, SprintOrder.Advance, fever: true)); // a feverish man's sprint at a near enemy
        var off = Sprint(into: false);
        Assert.False(SprintMath.IntoContact(in off, SprintOrder.Charge, fever: false));
        Assert.False(SprintMath.IntoContact(in off, SprintOrder.Charge, fever: true));
        var sp = new SprintStore();
        Assert.Equal(SprintStart.Started, sp.TryStartAi(in s, 0, 2, SprintOrder.Charge, intoContact: true));
        Assert.True(sp.IntoContact);
        sp.Stop();
        Assert.Equal(SprintStart.Started, sp.TryStartAi(in s, 0, 2, SprintOrder.Advance));
        Assert.False(sp.IntoContact);
        sp.Stop();
        sp.FillUp();
        Assert.Equal(SprintStart.Started, sp.TryStartRout(new SprintRules(true, true, true, true, 20, 20, 2, 1.1, 30, 7, 0.5, 0.5, 0, 50, 80, 2, 1, true, true), 0, 2));
        Assert.False(sp.IntoContact); // a rout never
    }

    [Fact]
    public void Into_contact_it_ends_only_when_he_stands_still_half_a_second_after_he_began_to_move()
    {
        var s = Sprint();
        var sp = new SprintStore();
        sp.TryStartAi(in s, 0, 2, SprintOrder.Charge, intoContact: true);
        // slow to start after the order: never ended before he moved
        for (int i = 0; i < 10; i++) Assert.False(sp.Advance(0.1, moving: false, out _));
        Assert.True(sp.Advance(0.1, moving: true, out _) == false);
        Assert.False(sp.Advance(0.1, moving: false, out _));
        Assert.False(sp.Advance(0.1, moving: false, out _));
        Assert.False(sp.Advance(0.1, moving: true, out _)); // moving again: the still clock back to 0
        for (int i = 0; i < 4; i++) Assert.False(sp.Advance(0.1, moving: false, out _));
        Assert.True(sp.Advance(0.1, moving: false, out var end));
        Assert.Equal(SprintEnd.Stopped, end);
        Assert.Equal(0.5, SprintMath.StoppedEndSeconds, 9);
        // an advance's sprint (not into contact) never ends by standing still
        var adv = new SprintStore();
        adv.TryStartAi(in s, 0, 4, SprintOrder.Advance);
        Assert.False(adv.Advance(0.1, moving: true, out _));
        for (int i = 0; i < 20; i++) Assert.False(adv.Advance(0.1, moving: false, out _));
        Assert.Equal("his first attack (into contact)", SprintMath.EndName(SprintEnd.Attacked));
        Assert.Equal("he stopped moving (into contact)", SprintMath.EndName(SprintEnd.Stopped));
    }

    [Fact]
    public void A_small_charge_store_waits_for_the_enemy_a_big_one_sprints_at_the_order()
    {
        var s = Sprint();
        Assert.True(SprintMath.ChargeWaits(in s, SprintOrder.Charge, 2.0));    // a looter's 2 s floor
        Assert.True(SprintMath.ChargeWaits(in s, SprintOrder.Charge, 2.99));
        Assert.False(SprintMath.ChargeWaits(in s, SprintOrder.Charge, 3.0));
        Assert.False(SprintMath.ChargeWaits(in s, SprintOrder.Charge, 6.5));   // a fresh legionary's 130 points / 20
        Assert.False(SprintMath.ChargeWaits(in s, SprintOrder.Charge, 0));     // nothing to sprint: nothing to wait for
        Assert.False(SprintMath.ChargeWaits(in s, SprintOrder.Advance, 2.0));  // an advance never waits
        var never = Sprint(waitBelow: 0);
        Assert.False(SprintMath.ChargeWaits(in never, SprintOrder.Charge, 2.0));
        Assert.Equal(1, Sprint(waitMetres: 0).ChargeWaitMetres, 9);            // at least a metre
    }

    [Fact]
    public void A_format_11_file_at_the_old_fever_sprint_distances_gets_4_and_8_once_tuned_kept()
    {
        static double Shipped(ParamDef p) => p == SettingsSchema.FeverSprintMinMetres ? 4.0 : p == SettingsSchema.FeverSprintMaxMetres ? 8.0 : p.Default;
        Assert.Equal(12, ConfigFile.FormatVersion);
        var read = ConfigFile.Read("{ \"ConfigVersion\": 11, \"FeverSprintMinMetres\": 3.0, \"FeverSprintMaxMetres\": 12.0 }");
        var notes = ConfigFile.Migrate(read, Shipped);
        Assert.Equal(2, notes.Count);
        Assert.StartsWith("FeverSprintMinMetres: 3.0 → 4.0 (format 11 → 12, the old default; step 64, Anton's looter battles", notes[0], StringComparison.Ordinal);
        Assert.StartsWith("FeverSprintMaxMetres: 12.0 → 8.0 (format 11 → 12, the old default; step 64", notes[1], StringComparison.Ordinal);
        Assert.Equal(4, read.Values["FeverSprintMinMetres"], 9);
        Assert.Equal(8, read.Values["FeverSprintMaxMetres"], 9);
        var mine = ConfigFile.Read("{ \"ConfigVersion\": 11, \"FeverSprintMaxMetres\": 15.0 }");
        var kept = ConfigFile.Migrate(mine, Shipped);
        Assert.Single(kept);
        Assert.StartsWith("FeverSprintMaxMetres: 15.0 kept - your own value (format 11 → 12; the new default 8.0", kept[0], StringComparison.Ordinal);
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 12, \"FeverSprintMaxMetres\": 12.0 }"), Shipped));
    }

    [Fact]
    public void The_summary_counts_sprints_into_contact_their_ends_and_the_waiting_charges()
    {
        var stats = new SprintStats();
        stats.IntoContactStarted = 5;
        stats.AddEnd(false, SprintOrder.Charge, 2, 1.5, SprintEnd.Attacked);
        stats.AddEnd(false, SprintOrder.Charge, 2, 1.2, SprintEnd.Attacked);
        stats.AddEnd(false, SprintOrder.Charge, 2, 0.8, SprintEnd.Stopped);
        stats.ChargeWaited = 4;
        stats.ChargeWaitStarted = 3;
        stats.ChargeWaitSeconds = 6;
        stats.ChargeWaitDropped = 1;
        var lines = new System.Collections.Generic.List<string>();
        stats.AddSummaryLines(lines, Sprint(), "LeftControl");
        string line = lines.Last();
        Assert.StartsWith("sprint into contact (step 64): a charge's (and a fever's) sprint runs INTO contact", line);
        Assert.Contains("sprints run into contact 5 (ended by his first attack 2, he stopped 1, an enemy within 3.0 m 0 - the advance / move ones)", line);
        Assert.Contains("charge sprints that WAITED for the enemy 4: started within 10.0 m 3 (avg 2.00 s each), never (a new order, mounted, gone, switched off) 1", line);
        Assert.Contains("ends: ", lines[2]);
        Assert.Contains("his first attack (into contact) 2", lines[2]);
    }
}

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
    public void A_tired_AI_mans_guessed_share_of_front_blocks_keeps_the_games_verdict_so_a_worn_out_wall_still_breaks()
    {
        var r = Rules();
        // w 0 (fresh, you, the setting 0): every front block right, whatever the roll
        Assert.Equal(BlockKind.ShieldRightSide, ShieldArcMath.Resolve(in r, BlockKind.ShieldWrongSide, 20, 0, 0.0, out bool k0));
        Assert.False(k0);
        // w 1 (an empty man at TiredBlockRandomPercent 100): the game's verdict, as before step 64
        Assert.Equal(BlockKind.ShieldWrongSide, ShieldArcMath.Resolve(in r, BlockKind.ShieldWrongSide, 20, 1, 0.999, out bool k1));
        Assert.True(k1);
        // w 0.5: a roll below keeps it wrong, at or above turns it right; outside the arc the roll never matters
        Assert.Equal(BlockKind.ShieldWrongSide, ShieldArcMath.Resolve(in r, BlockKind.ShieldWrongSide, 20, 0.5, 0.49, out _));
        Assert.Equal(BlockKind.ShieldRightSide, ShieldArcMath.Resolve(in r, BlockKind.ShieldWrongSide, 20, 0.5, 0.5, out _));
        Assert.Equal(BlockKind.ShieldWrongSide, ShieldArcMath.Resolve(in r, BlockKind.ShieldWrongSide, 100, 0, 1, out bool k2));
        Assert.False(k2); // not "kept by tiredness" - simply outside
        // the roll: the same blow always the same number, spread evenly over many blows (half below 0.5)
        Assert.Equal(ShieldArcMath.TiredRoll(7, 9, 3), ShieldArcMath.TiredRoll(7, 9, 3));
        int below = 0;
        for (int swing = 1; swing <= 4000; swing++)
        {
            double u = ShieldArcMath.TiredRoll(1000 + swing % 37, 2000 + swing % 11, swing);
            Assert.InRange(u, 0, 0.9999999);
            if (u < 0.5) below++;
        }
        Assert.InRange(below, 1850, 2150);
        var stats = new ShieldArcStats();
        stats.Note(false, BlockKind.ShieldWrongSide, BlockKind.ShieldWrongSide, 20, keptByTiredness: true);
        Assert.Equal(1, stats.KeptByTiredness);
        Assert.Contains("from the front but kept WRONG by the blocker's tiredness (step 30's guessed share of a tired man's guard) 1", stats.SummaryLine(in r));
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
        Assert.Equal(23, ConfigFile.FormatVersion);
        var read = ConfigFile.Read("{ \"ConfigVersion\": 11, \"FeverSprintMinMetres\": 3.0, \"FeverSprintMaxMetres\": 12.0 }");
        var notes = ConfigFile.Migrate(read, Shipped);
        Assert.Equal(2, notes.Count);
        Assert.StartsWith("FeverSprintMinMetres: 3.0 → 4.0 (format 11 → 23, the old default; step 64, Anton's looter battles", notes[0], StringComparison.Ordinal);
        Assert.StartsWith("FeverSprintMaxMetres: 12.0 → 8.0 (format 11 → 23, the old default; step 64", notes[1], StringComparison.Ordinal);
        Assert.Equal(4, read.Values["FeverSprintMinMetres"], 9);
        Assert.Equal(8, read.Values["FeverSprintMaxMetres"], 9);
        var mine = ConfigFile.Read("{ \"ConfigVersion\": 11, \"FeverSprintMaxMetres\": 15.0 }");
        var kept = ConfigFile.Migrate(mine, Shipped);
        Assert.Single(kept);
        Assert.StartsWith("FeverSprintMaxMetres: 15.0 kept - your own value (format 11 → 23; the new default 8.0", kept[0], StringComparison.Ordinal);
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

/// <summary>
/// Step 64 - THE CHARGE CARRIES TO THE BLOW and THE WORKED EXAMPLES (AI_NOTES "Step 64", MECHANICS §11c "The wall's blocks count"): the carry
/// 1.5 → 3 s (config format 12), an attack begun in the charge stays one to its blow; a RUNNING blow already counts proportionally. Anton's
/// looters into a fresh legionary shield wall, before and after: the troops of ChargeExamplesTests (a looter: pool 50, a 1.5 kg club at 85,
/// skill 30, toughness 1.35; a legionary: pool 130, skill 130, a 4 kg shield, toughness 5.97) on the shipped values.
/// </summary>
public class Step64ExamplesTests
{
    private sealed class Never : IRandomSource
    {
        public double NextDouble() => 0.999;
    }

    private readonly record struct Troop(double Pool, double Skill, double Kg, double Speed, ImpactDamageType Type, double Armour, int Level)
    {
        public double Toughness => CourageMath.BaseToughness(200, 100, Armour, Level);
    }

    private static readonly Troop Legionary = new(130, 130, 1.3, 92, ImpactDamageType.Cut, 110, 26);
    private static readonly Troop Looter = new(50, 30, 1.5, 85, ImpactDamageType.Blunt, 8, 5);
    private static readonly Troop Even = new(100, 100, 1.2, 92, ImpactDamageType.Cut, 50, 15);

    /// <summary>defaults.json's block model (step 56) and step 63's direction; step 64 adds nothing to the model's numbers.</summary>
    private static ImpactRules Shipped()
    {
        var s = new TraxSettings();
        s.Set(SettingsSchema.RightBlockStaggerMaxPercent, 30, "test");
        s.Set(SettingsSchema.RightBlockKnockdownMaxPercent, 10, "test");
        s.Set(SettingsSchema.ParryStaggerMaxPercent, 30, "test");
        s.Set(SettingsSchema.ParryKnockdownMaxPercent, 10, "test");
        s.Set(SettingsSchema.RiposteStaggerMaxPercent, 70, "test");
        s.Set(SettingsSchema.BlockStunTiredExtraPercent, 0, "test");
        s.Set(SettingsSchema.ImpactRearPercent, 100, "test");
        s.Set(SettingsSchema.ImpactFlankPercent, 100, "test");
        s.Set(SettingsSchema.ImpactChargeCarrySeconds, 3.0, "test");
        return ImpactRules.From(s);
    }

    private static ImpactDecision Decide(ImpactEvent e, Troop a, double aBar, Troop d, double dBar, bool charging = false, double speed = 1.0)
    {
        double fA = Math.Min(1, aBar / 0.75);
        var f = new ImpactFacts
        {
            Event = e,
            AttackerPoints = a.Pool * aBar,
            AttackerF = fA,
            AttackerAnimation = 0.85 + 0.15 * fA,
            AttackerSkill = a.Skill,
            DefenderPoints = d.Pool * dBar,
            DefenderBar = dBar,
            DefenderF = Math.Min(1, dBar / 0.75),
            DefenderSkill = d.Skill,
            WeaponWeight = a.Kg,
            SwingSpeed = a.Speed,
            DamageType = a.Type,
            GuardWeight = ImpactMath.IsBlock(e) ? 4.0 : 0,
            DefenderHasShield = ImpactMath.IsBlock(e),
            DefenderMaxHealth = 100,
            AttackerSpeed = charging ? 3.0 : speed,
            AttackerCharging = charging,
            AttackerToughness = a.Toughness,
            DefenderToughness = d.Toughness,
        };
        var r = Shipped();
        var ds = default(ImpactState);
        var at = default(ImpactState);
        return ImpactMath.Decide(in r, in f, ref ds, ref at, 100, new Never());
    }

    /// <summary>The attacker reels or is thrown back (the thrown back replaces the reel).</summary>
    private static double Bounced(in ImpactDecision d) => d.ThrownBack.Chance + (1 - d.ThrownBack.Chance) * d.Riposte.Chance;

    private static void Near(double expected, double actual, double tol, string what) =>
        Assert.True(Math.Abs(expected - actual) <= tol, what + ": " + (100 * actual).ToString("0.0") + "% vs " + (100 * expected).ToString("0.0") + "%");

    [Fact]
    public void The_carry_is_3_s_by_defaults_json_and_a_format_11_file_at_1_5_gets_it_once()
    {
        static double Shipped64(ParamDef p) => p == SettingsSchema.ImpactChargeCarrySeconds ? 3.0 : p.Default;
        var read = ConfigFile.Read("{ \"ConfigVersion\": 11, \"ImpactChargeCarrySeconds\": 1.5 }");
        var notes = ConfigFile.Migrate(read, Shipped64);
        Assert.Single(notes);
        Assert.StartsWith("ImpactChargeCarrySeconds: 1.5 → 3.0 (format 11 → 23, the old default; step 64, Anton's looter battles: the charge carries 3 s", notes[0], StringComparison.Ordinal);
        var mine = ConfigFile.Read("{ \"ConfigVersion\": 11, \"ImpactChargeCarrySeconds\": 2.0 }");
        Assert.StartsWith("ImpactChargeCarrySeconds: 2.0 kept - your own value", ConfigFile.Migrate(mine, Shipped64).Single(), StringComparison.Ordinal);
        Assert.Equal(3.0, Shipped().ChargeCarrySeconds, 9);
        Assert.Contains("An attack he BEGAN while sprinting or within this time stays a charge until it ends", SettingsSchema.ImpactChargeCarrySeconds.Description);
    }

    [Fact]
    public void The_charges_line_counts_the_blows_an_attack_begun_in_the_charge_carried()
    {
        var stats = new ImpactChargeStats();
        var charge = Decide(ImpactEvent.RightBlock, Looter, 1.0, Legionary, 1.0, charging: true);
        stats.NoteCharge(in charge, false, true);
        stats.NoteCharge(in charge, false, false);
        stats.NoteCharge(in charge, true, false);
        Assert.Equal(1, stats.CarriedByAttack);
        string line = stats.SummaryLines().Single(l => l.StartsWith("impact charges", StringComparison.Ordinal));
        Assert.Contains("3 of 3 decided events (sprinting at the blow 1, carried after the sprint 2) - of the carried, by an attack begun in the charge (step 64) 1", line);
    }

    [Fact]
    public void A_running_blow_counts_proportionally_in_the_momentum_and_the_reflected_charge_a_walking_one_never_throws()
    {
        var walk = Decide(ImpactEvent.RightBlock, Looter, 1.0, Legionary, 1.0, speed: 1.0);
        var run = Decide(ImpactEvent.RightBlock, Looter, 1.0, Legionary, 1.0, speed: 4.5);
        var charge = Decide(ImpactEvent.RightBlock, Looter, 1.0, Legionary, 1.0, charging: true);
        Assert.Equal(0, walk.ChargeShare, 9);
        Near(0.5, run.ChargeShare, 1e-9, "a 4.5 m/s blow's share (2 → 7 m/s)");
        Assert.Equal(1, charge.ChargeShare, 9);
        Assert.False(walk.ThrownBack.InRow);
        Assert.True(run.ThrownBack.Chance > 0.05 && run.ThrownBack.Chance < charge.ThrownBack.Chance,
            "running " + run.ThrownBack.Chance + " vs charging " + charge.ThrownBack.Chance);
        Assert.True(run.RiposteMomentum > walk.RiposteMomentum && run.RiposteMomentum < charge.RiposteMomentum);
        Assert.True(Bounced(in walk) < Bounced(in run) && Bounced(in run) < Bounced(in charge));
    }

    /// <summary>
    /// 50 looters charging a FRESH 20-man legionary wall (Anton's second battle, 12:47): at the peak the wall's shield blocks read right 7.3% /
    /// wrong 39.0% of the blows that reached a shieldman (the rest landed), 14 of 160 decided blows were charges. A wrong-side block never
    /// ripostes; a right-side one does. With the front arc (the looters come from the front) the 46.3% are right-side blocks; with the
    /// sprint into contact and the 3 s carry each looter's first blow is a charge.
    /// </summary>
    [Fact]
    public void Looters_into_a_fresh_legionary_wall_bounce_off_the_shields_now_before_and_after()
    {
        var charge = Decide(ImpactEvent.RightBlock, Looter, 1.0, Legionary, 1.0, charging: true);
        var walk = Decide(ImpactEvent.RightBlock, Looter, 1.0, Legionary, 1.0);
        var wrongCharge = Decide(ImpactEvent.WrongBlock, Looter, 1.0, Legionary, 1.0, charging: true);
        // the per-block numbers (step 62's, unchanged by step 64)
        Near(0.33, charge.ThrownBack.Chance, 0.02, "a charging looter into a right-side block - thrown back");
        Near(0.72, Bounced(in charge), 0.02, "a charging looter into a right-side block - reeled or thrown");
        Near(0.34, Bounced(in walk), 0.02, "a walking looter into a right-side block - reeled");
        Assert.False(walk.ThrownBack.InRow);
        Assert.False(wrongCharge.Riposte.InRow || wrongCharge.ThrownBack.InRow); // a WRONG-side block: the attacker never bounces
        Assert.True(wrongCharge.Knockdown.Chance + wrongCharge.Stagger.Chance < 0.01); // ... and the fresh legionary never moves either

        // BEFORE (the game's verdict; 14 of 160 blows charged): of the blows into the wall's shieldmen
        const double right0 = 0.073, wrong0 = 0.390, charged0 = 14.0 / 160;
        double before = right0 * (charged0 * Bounced(in charge) + (1 - charged0) * Bounced(in walk));
        Near(0.027, before, 0.005, "before - looter blows into the wall that bounce");
        // AFTER: the front arc counts the wall's blocks from the front right (the 46.3%)
        double right1 = right0 + wrong0;
        double firstBlow = right1 * Bounced(in charge);
        double laterBlow = right1 * Bounced(in walk);
        Near(0.33, firstBlow, 0.02, "after - a looter's FIRST (charging) blow into the wall bounces");
        Near(0.15, right1 * charge.ThrownBack.Chance, 0.015, "after - a looter's first blow THROWS HIM BACK");
        Near(0.16, laterBlow, 0.02, "after - a later (walking) blow bounces");
        Assert.True(firstBlow > 10 * before);

        // unchanged: two fresh even men (a walking blow into a right block: riposte ~1 %, never thrown; charging: thrown ~10 %)
        var evenWalk = Decide(ImpactEvent.RightBlock, Even, 1.0, Even, 1.0);
        var evenCharge = Decide(ImpactEvent.RightBlock, Even, 1.0, Even, 1.0, charging: true);
        Assert.False(evenWalk.ThrownBack.InRow);
        Assert.True(evenWalk.Riposte.Chance < 0.03, "even, walking - reel " + evenWalk.Riposte.Chance);
        Near(0.10, evenCharge.ThrownBack.Chance, 0.04, "even, charging - thrown back");
        Assert.Equal(0, evenCharge.Stagger.Chance + evenCharge.Knockdown.Chance, 3);
    }
}

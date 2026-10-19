using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 62 - MORE MEN FALL, AND THE CHARGE MATTERS (Anton 2026-10-19; AI_NOTES "Step 62", DESIGN decision 52): the lost falls (the shrug-off
/// yields to a decided fall, the backup blow, a killing blow never decided) and the falls' summary.
/// </summary>
public class ChargeTests
{
    private sealed class Dice : IRandomSource
    {
        public double NextDouble() => 0.999;
    }

    private static ImpactRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        // step 63's direction pressure off: these are step 62's rules (the multipliers x2 / x1.4 carried direction then)
        s.Set(SettingsSchema.ImpactRearPressure, 0, "test");
        s.Set(SettingsSchema.ImpactFlankPressure, 0, "test");
        tune?.Invoke(s);
        return ImpactRules.From(s);
    }

    /// <summary>Courage's toughness of the worked examples' troops (step 49): a looter 1.35, an imperial legionary 5.97.</summary>
    private static readonly double Looter = CourageMath.BaseToughness(200, 100, CourageMath.ExampleLooterArmour, CourageMath.ExampleLooterLevel);

    private static readonly double Legionary = CourageMath.BaseToughness(200, 100, CourageMath.ExampleLegionaryArmour, CourageMath.ExampleLegionaryLevel);

    /// <summary>One event between two men of pool 100 and skill 100 (a 1.2 kg sword at 92, a 4 kg shield / a 1.2 kg parrying sword), the
    /// peak line at 75 %, the attacker's tired swing (0.85 + 0.15 f) - with step 62's inputs.</summary>
    private static ImpactFacts Facts(ImpactEvent e, double bar, double attackerBar = 1.0, double tA = 0, double tD = 0, double speed = 0,
        bool ranged = false, double angle = 0, bool charging = false)
    {
        double fA = Math.Min(1, attackerBar / 0.75);
        return new ImpactFacts
        {
            Event = e,
            AttackerPoints = 100 * attackerBar,
            AttackerF = fA,
            AttackerAnimation = 0.85 + 0.15 * fA,
            AttackerSkill = 100,
            DefenderPoints = 100 * bar,
            DefenderBar = bar,
            DefenderF = Math.Min(1, bar / 0.75),
            DefenderSkill = 100,
            WeaponWeight = 1.2,
            SwingSpeed = 92,
            GuardWeight = e == ImpactEvent.Parry ? 1.2 : ImpactMath.IsBlock(e) ? 4.0 : 0,
            DefenderHasShield = e != ImpactEvent.Parry,
            DefenderMaxHealth = 100,
            AttackerSpeed = speed,
            AttackerToughness = tA,
            DefenderToughness = tD,
            DefenderRangedInHand = ranged,
            AttackAngleDegrees = angle,
        };
    }

    private static ImpactDecision Decide(in ImpactFacts f, ImpactRules? rules = null)
    {
        var r = rules ?? Rules();
        var d = default(ImpactState);
        var a = default(ImpactState);
        return ImpactMath.Decide(in r, in f, ref d, ref a, 100, new Dice());
    }

    // ------------------------------------------------------------------ the charge, the reflected charge

    private sealed class ScriptDice : IRandomSource
    {
        private readonly double[] _v;
        private int _i;

        public ScriptDice(params double[] v) => _v = v;

        public double NextDouble() => _i < _v.Length ? _v[_i++] : 0.999;
    }

    [Fact]
    public void A_charge_counts_at_least_the_sprint_reference_whatever_speed_was_measured_never_a_rider()
    {
        var r = Rules();
        Assert.Equal(1.5, r.ChargeCarrySeconds, 6);
        var f = Facts(ImpactEvent.CleanHit, 0.5, speed: 2.5);
        Assert.Equal(2.5, ImpactMath.EffectiveSpeed(in r, in f), 9);
        f.AttackerCharging = true;
        Assert.Equal(7.0, ImpactMath.EffectiveSpeed(in r, in f), 6);
        f.AttackerSpeed = 9;
        Assert.Equal(9.0, ImpactMath.EffectiveSpeed(in r, in f), 9);
        f.AttackerMounted = true;
        Assert.Equal(9.0, ImpactMath.EffectiveSpeed(in r, in f), 9);
        var g = Facts(ImpactEvent.CleanHit, 0.5, speed: 1.0);
        g.AttackerCharging = true;
        var d = Decide(in g);
        Assert.True(d.Charging);
        Assert.Equal(1.6, d.Momentum, 6);
        Assert.Equal(1.0, d.ChargeShare, 6);
        Assert.Equal(1.0, d.MeasuredSpeed, 9);
        Assert.Contains("a CHARGE, measured 1.0 m/s", ImpactMath.Describe(in d, in g, "A", "D", "LIVE"));
    }

    [Fact]
    public void A_charge_into_a_right_side_shield_block_can_be_thrown_back_a_walking_man_or_a_parry_never()
    {
        // a tired man (bar 30%) charging into a fresh shieldman's right-side block
        var walk = Facts(ImpactEvent.RightBlock, 1.0, attackerBar: 0.3, speed: 1.0);
        var charge = walk;
        charge.AttackerCharging = true;
        var dw = Decide(in walk);
        var dc = Decide(in charge);
        Assert.False(dw.ThrownBack.InRow);                    // a walking attacker only reels
        Assert.True(dc.ThrownBack.InRow && dc.ThrownBack.Chance > 0.3, "thrown back " + dc.ThrownBack.Chance);
        Assert.True(dc.XRiposte > dw.XRiposte, "his charge not turned against him: " + dc.XRiposte + " vs " + dw.XRiposte);
        Assert.Equal(2.0, dc.RiposteMomentum, 6);
        var parry = Facts(ImpactEvent.Parry, 1.0, attackerBar: 0.3, speed: 1.0);
        parry.AttackerCharging = true;
        Assert.False(Decide(in parry).ThrownBack.InRow);        // a sword parry never throws a charger
        Assert.False(Decide(in charge, Rules(s => s.Set(SettingsSchema.RiposteKnockdownMaxPercent, 0, SettingSources.Mcm))).ThrownBack.InRow);
        // it happens: he is DOWN (his record), the reel replaced, his get-up by his own tiredness
        var r = Rules();
        var dState = new ImpactState();
        var aState = new ImpactState();
        var d = ImpactMath.Decide(in r, in charge, ref dState, ref aState, 100, new ScriptDice(0.0, 0.0, 0.5));
        Assert.True(d.ThrownBack.Happens);
        Assert.False(d.Riposte.Happens);
        Assert.Equal(ImpactRefusal.Replaced, d.Riposte.Refusal);
        Assert.True(aState.Down(100.5) && !double.IsNaN(d.ThrownBackGetUpFactor) && d.AnyOutcome);
        Assert.Contains("THROWN BACK (x_r ", ImpactMath.Describe(in d, in charge, "A", "D", "LIVE"));
        // ... and a man still down / just up is not thrown again
        var again = ImpactMath.Decide(in r, in charge, ref dState, ref aState, 101, new ScriptDice(0.0, 0.0, 0.5));
        Assert.Equal(ImpactRefusal.Down, again.ThrownBack.Refusal);
    }

    [Fact]
    public void The_riposte_reel_grows_with_the_comparison_half_for_a_fresh_even_man()
    {
        var r = Rules(s => s.Set(SettingsSchema.ImpactRandomPercent, 0, SettingSources.Mcm));
        // two fresh even men: x_r about 0 (the 4 kg shield a little more) → about half the most
        var even = Facts(ImpactEvent.RightBlock, 1.0, attackerBar: 1.0);
        var dState = new ImpactState();
        var aState = new ImpactState();
        var d = ImpactMath.Decide(in r, in even, ref dState, ref aState, 100, new ScriptDice(0.0, 0.5));
        Assert.True(d.Riposte.Happens && d.XRiposte < 0.1, "x_r " + d.XRiposte);
        Assert.Equal(r.RiposteStunMax * (0.5 + 0.5 * d.XRiposte), d.RiposteStunSeconds, 6);
        // a tired, outmatched man (x_r above 1): all of it
        var tired = Facts(ImpactEvent.RightBlock, 1.0, attackerBar: 0.05, tA: Looter, tD: Legionary);
        dState = new ImpactState();
        aState = new ImpactState();
        var t = ImpactMath.Decide(in r, in tired, ref dState, ref aState, 100, new ScriptDice(0.0, 0.5));
        Assert.True(t.Riposte.Happens);
        Assert.Equal(Math.Min(r.StunCap, r.RiposteStunMax), t.RiposteStunSeconds, 3);
    }

    [Fact]
    public void The_charges_line_compares_charging_blows_with_the_rest()
    {
        var stats = new ImpactChargeStats();
        var charge = Facts(ImpactEvent.RightBlock, 1.0, attackerBar: 0.3, speed: 1.0);
        charge.AttackerCharging = true;
        var r = Rules();
        var dState = new ImpactState();
        var aState = new ImpactState();
        stats.NoteCharge(ImpactMath.Decide(in r, in charge, ref dState, ref aState, 100, new ScriptDice(0.0, 0.0, 0.5)), true);
        stats.NoteCharge(Decide(Facts(ImpactEvent.RightBlock, 1.0, attackerBar: 0.3, speed: 1.0)), false);
        var rear = Facts(ImpactEvent.CleanHit, 0.3, angle: 170);
        rear.AttackerCharging = true;
        stats.NoteCharge(Decide(in rear), false);
        Assert.Equal(3, stats.Decided);
        Assert.Equal(2, stats.Charging);
        Assert.Equal(1, stats.ChargingNow);
        Assert.Equal(1, stats.Carried);
        Assert.Equal(1, stats.ChargeThrown[0]);
        Assert.Equal(1, stats.ChargeBlows[5]);
        string line = stats.SummaryLines()[2];
        Assert.Contains("impact charges (step 62", line);
        Assert.Contains("2 of 3 decided events (sprinting at the blow 1, carried after the sprint 1)", line);
        Assert.Contains("into a right-side shield block 1: the attacker reeled 0%, THROWN BACK 100% (not charging 1: reeled ", line);
        Assert.Contains("landed from BEHIND 1: the defender staggered ", line);
    }

    // ------------------------------------------------------------------ the cap

    [Fact]
    public void The_multipliers_together_never_pass_the_cap_either_way()
    {
        var r = Rules();
        Assert.Equal(8.0, r.MultiplierCap, 6);
        Assert.Equal(8.0, ImpactMath.CapMultiplier(in r, 12), 9);
        Assert.Equal(0.125, ImpactMath.CapMultiplier(in r, 0.05), 9);
        Assert.Equal(3.3, ImpactMath.CapMultiplier(in r, 3.3), 9);
        Assert.Equal(1.0, ImpactMath.CapMultiplier(in r, double.NaN), 9);
        // a sprinting man of nine times the toughness into an archer's back: x1.6 × 3 × 1.3 × 2 = 12.5 → held at 8
        var f = Facts(ImpactEvent.CleanHit, 0.5, tA: 9, tD: 1, ranged: true, angle: 170);
        f.AttackerCharging = true;
        var d = Decide(in f);
        Assert.True(d.Capped);
        Assert.Equal(8.0, d.Extra * d.Momentum, 6);
        var open = Decide(in f, Rules(s => s.Set(SettingsSchema.ImpactMultiplierCapPercent, 2000, SettingSources.Mcm)));
        Assert.False(open.Capped);
        Assert.Equal(Math.Log(12.48 / 8, 2), open.Log2R - d.Log2R, 2);
        Assert.Contains("CAPPED at x8.00 in all", ImpactMath.Describe(in d, in f, "A", "D", "LIVE"));
    }

    [Fact]
    public void A_fresh_tough_shieldman_facing_a_charge_and_blocking_right_stays_near_unshakeable()
    {
        // a fresh legionary SPRINTING into a fresh legionary's right-side block: the blocker barely moves; the charger risks his own skin
        var f = Facts(ImpactEvent.RightBlock, 1.0, attackerBar: 1.0, tA: Legionary, tD: Legionary);
        f.AttackerCharging = true;
        var d = Decide(in f);
        Assert.Equal(0, d.Knockdown.Chance);
        Assert.True(d.Stagger.Chance < 0.02, "blocker staggered " + d.Stagger.Chance);
        Assert.True(d.ThrownBack.Chance > 0.05 && d.ThrownBack.Chance < 0.2, "thrown back " + d.ThrownBack.Chance);
        // ... and a looter sprinting into him: nothing at all on the blocker
        var g = Facts(ImpactEvent.RightBlock, 1.0, attackerBar: 1.0, tA: Looter, tD: Legionary);
        g.AttackerCharging = true;
        var e = Decide(in g);
        Assert.Equal(0, e.Knockdown.Chance);
        Assert.Equal(0, e.Stagger.Chance);
    }

    // ------------------------------------------------------------------ the uncapped critical

    [Fact]
    public void A_charging_melee_blow_rolls_the_full_upside_however_tired_never_a_missile_or_with_the_switch_off()
    {
        var s = new TraxSettings();
        Assert.True(s.SprintFullCritical);
        Assert.Same(SettingsSchema.DamageGroup, SettingsSchema.SprintFullCritical.Group);
        Assert.Equal(SettingsSchema.DamageRandomOnShields.Index + 1, SettingsSchema.SprintFullCritical.Index);
        var rules = DamageRules.From(s);
        Assert.True(rules.UpsideFollowsAthletics);
        Assert.Equal(0.2, DamageRoll.Upside(in rules, 0.2), 9);                              // a tired man's upside shrinks
        Assert.Equal(1.0, DamageRoll.ChargeUpside(in rules, 0.2, charging: true, missile: false), 9); // ... not when he charges
        Assert.Equal(0.2, DamageRoll.ChargeUpside(in rules, 0.2, charging: true, missile: true), 9);
        Assert.Equal(0.2, DamageRoll.ChargeUpside(in rules, 0.2, charging: false, missile: false), 9);
        s.Set(SettingsSchema.SprintFullCritical, false, SettingSources.Mcm);
        var off = DamageRules.From(s);
        Assert.Equal(0.2, DamageRoll.ChargeUpside(in off, 0.2, charging: true, missile: false), 9);
        // the roll itself: an empty man's charge can reach 1.5 (±50 %), his ordinary blow never above 1.0
        Assert.Equal(1.5f, DamageRoll.Factor(0.9999999, 0.5, DamageRoll.ChargeUpside(in rules, 0.0, true, false)), 4);
        Assert.Equal(1.0f, DamageRoll.Factor(0.9999999, 0.5, DamageRoll.Upside(in rules, 0.0)), 4);
    }

    [Fact]
    public void The_damage_critical_line_compares_the_charging_melee_rolls_with_the_rest()
    {
        var stats = new DamageStats();
        var charge = DamageRoll.Roll(40f, 50, new ScriptDice(0.99), 1.0);
        var other = DamageRoll.Roll(40f, 50, new ScriptDice(0.5), 0.3);
        stats.AddRoll(DamageCategory.Melee, in charge);
        stats.AddMeleeCharge(in charge, true, 0.3);
        stats.AddRoll(DamageCategory.Melee, in other);
        stats.AddMeleeCharge(in other, false, 0.3);
        Assert.Equal(1, stats.ChargeRolls(out double mean, out int lifted));
        Assert.Equal(1, lifted);
        Assert.True(mean > 1.45, "mean " + mean);
        Assert.Contains(stats.SummaryLines(), l => l.StartsWith("damage critical (step 62") && l.Contains("1 charging melee hits, avg x1.490") && l.Contains("would have allowed 30%")
                                                    && l.Contains("every other melee hit 1, avg x"));
    }

    // ------------------------------------------------------------------ direction

    [Fact]
    public void A_blow_from_behind_counts_twice_from_the_side_x1_4_a_block_never()
    {
        var r = Rules();
        Assert.Equal(0, ImpactMath.DirectionOf(in r, 0));
        Assert.Equal(0, ImpactMath.DirectionOf(in r, 59.9));
        Assert.Equal(1, ImpactMath.DirectionOf(in r, 60));
        Assert.Equal(1, ImpactMath.DirectionOf(in r, 120));
        Assert.Equal(2, ImpactMath.DirectionOf(in r, 120.1));
        Assert.Equal(2, ImpactMath.DirectionOf(in r, 180));
        Assert.Equal(0, ImpactMath.DirectionOf(in r, double.NaN));
        Assert.Equal(2.0, ImpactMath.DirectionFactor(in r, ImpactEvent.CleanHit, 170), 6);
        Assert.Equal(1.4, ImpactMath.DirectionFactor(in r, ImpactEvent.Kick, 90), 6);
        Assert.Equal(1.0, ImpactMath.DirectionFactor(in r, ImpactEvent.CleanHit, 20), 6);
        Assert.Equal(1.0, ImpactMath.DirectionFactor(in r, ImpactEvent.WrongBlock, 170), 6);
        var front = Decide(Facts(ImpactEvent.CleanHit, 0.5));
        var rear = Decide(Facts(ImpactEvent.CleanHit, 0.5, angle: 160));
        Assert.Equal(1.0, rear.Log2R - front.Log2R, 3);
        Assert.Equal(2, rear.Direction);
        Assert.Contains("from BEHIND (160°, x2.00)", ImpactMath.Describe(in rear, Facts(ImpactEvent.CleanHit, 0.5, angle: 160), "A", "B", "LIVE"));
        // live, and the side never past the rear's angle
        var tuned = Rules(s =>
        {
            s.Set(SettingsSchema.ImpactRearPercent, 300, SettingSources.Mcm);
            s.Set(SettingsSchema.ImpactFlankDegrees, 150, SettingSources.Mcm);
        });
        Assert.Equal(3.0, ImpactMath.DirectionFactor(in tuned, ImpactEvent.CleanHit, 170), 6);
        Assert.Equal(120, tuned.FlankDegrees);
        Assert.Equal(1.0, ImpactMath.DirectionFactor(Rules(s => s.Set(SettingsSchema.ImpactRearPercent, 100, SettingSources.Mcm)), ImpactEvent.CleanHit, 170), 6);
    }

    [Fact]
    public void The_direction_and_toughness_lines_count_landed_blows_on_men_on_foot()
    {
        var stats = new ImpactChargeStats();
        var r = Rules();
        for (int i = 0; i < 4; i++) stats.NoteDecision(Decide(Facts(ImpactEvent.CleanHit, 0.1, angle: 170, tA: Legionary, tD: Looter)));
        stats.NoteDecision(Decide(Facts(ImpactEvent.CleanHit, 0.9)));
        stats.NoteDecision(Decide(Facts(ImpactEvent.RightBlock, 0.9, angle: 170))); // a block: not counted
        Assert.Equal(new[] { 1, 0, 4 }, stats.DirBlows);
        Assert.Equal(4, stats.ToughBlows[3]);
        Assert.Equal(1, stats.ToughUnknown);
        var lines = stats.SummaryLines();
        Assert.Contains("impact direction (step 62", lines[0]);
        Assert.Contains("| rear 4 (x2.00: down ", lines[0]);
        Assert.Contains("much tougher 3x+ 4 (x2.10: down ", lines[1]);
        Assert.Contains("not counted (toughness off / unknown) 1", lines[1]);
    }

    // ------------------------------------------------------------------ toughness, a ranged man in melee

    [Fact]
    public void Toughness_counts_strongly_not_absolutely_a_legionary_on_a_looter_x2_and_the_looter_on_him_half()
    {
        Assert.Equal(1.35, Looter, 2);
        Assert.Equal(5.97, Legionary, 2);
        var r = Rules();
        Assert.Equal(0.5, r.ToughnessWeight, 6);
        Assert.Equal(2.10, ImpactMath.ToughnessFactor(in r, Legionary, Looter), 2);
        Assert.Equal(1 / 2.10, ImpactMath.ToughnessFactor(in r, Looter, Legionary), 2);
        Assert.Equal(1.0, ImpactMath.ToughnessFactor(in r, Legionary, Legionary), 9);
        Assert.Equal(1.0, ImpactMath.ToughnessFactor(in r, 0, Looter), 9);                 // unknown = even
        Assert.Equal(1.0, ImpactMath.ToughnessFactor(Rules(s => s.Set(SettingsSchema.ImpactToughnessWeight, 0.0, SettingSources.Mcm)), Legionary, Looter), 9);
        // in R: one doubling more (+0.25 x at the shipped mismatch weight) - the same blow, with and without
        var even = Decide(Facts(ImpactEvent.CleanHit, 0.5));
        var tough = Decide(Facts(ImpactEvent.CleanHit, 0.5, tA: Legionary, tD: Looter));
        Assert.Equal(Math.Log(2.10, 2), tough.Log2R - even.Log2R, 2);
        Assert.Equal(2.10, tough.ToughnessFactor, 2);
        Assert.Equal(Legionary / Looter, tough.ToughnessRatio, 6);
        Assert.True(double.IsNaN(even.ToughnessRatio));
        Assert.True(tough.X > even.X + 0.2, "x " + tough.X + " vs " + even.X);
    }

    [Fact]
    public void Two_fresh_men_of_the_same_toughness_still_never_stagger_each_other()
    {
        var d = Decide(Facts(ImpactEvent.CleanHit, 1.0, tA: Legionary, tD: Legionary));
        Assert.Equal(0, d.Knockdown.Chance);
        Assert.True(d.Stagger.Chance < 0.005, "stagger " + d.Stagger.Chance);
        var p = Decide(Facts(ImpactEvent.CleanHit, 1.0, tA: Looter, tD: Looter));
        Assert.Equal(0, p.Knockdown.Chance);
    }

    [Fact]
    public void The_riposte_reads_toughness_turned_round_a_tough_blocker_reels_a_weak_attacker_more()
    {
        var weakIntoTough = Decide(Facts(ImpactEvent.RightBlock, 1.0, attackerBar: 0.5, tA: Looter, tD: Legionary));
        var even = Decide(Facts(ImpactEvent.RightBlock, 1.0, attackerBar: 0.5));
        Assert.True(weakIntoTough.XRiposte > even.XRiposte + 0.2, "x_r " + weakIntoTough.XRiposte + " vs " + even.XRiposte);
        Assert.True(weakIntoTough.X <= even.X, "the tough blocker was pressed harder");
    }

    [Fact]
    public void A_man_with_a_bow_in_his_hand_takes_a_landed_blow_harder_never_a_block()
    {
        var r = Rules();
        Assert.Equal(1.3, r.RangedInHand, 6);
        Assert.Equal(1.3, ImpactMath.RangedFactor(in r, ImpactEvent.CleanHit, true), 6);
        Assert.Equal(1.3, ImpactMath.RangedFactor(in r, ImpactEvent.Kick, true), 6);
        Assert.Equal(1.0, ImpactMath.RangedFactor(in r, ImpactEvent.Parry, true), 6);
        Assert.Equal(1.0, ImpactMath.RangedFactor(in r, ImpactEvent.CleanHit, false), 6);
        Assert.Equal(1.0, ImpactMath.RangedFactor(Rules(s => s.Set(SettingsSchema.ImpactRangedInHandPercent, 100, SettingSources.Mcm)), ImpactEvent.CleanHit, true), 6);
        var archer = Decide(Facts(ImpactEvent.CleanHit, 0.5, ranged: true));
        var plain = Decide(Facts(ImpactEvent.CleanHit, 0.5));
        Assert.Equal(Math.Log(1.3, 2), archer.Log2R - plain.Log2R, 3);
    }

    // ------------------------------------------------------------------ the lost falls

    [Fact]
    public void The_lost_falls_settings_sit_after_the_punish_ones_live_on_by_default_with_a_half_second_backup()
    {
        var keys = SettingsSchema.InGroup(SettingsSchema.ImpactGroup).Select(p => p.Key).ToList();
        int i = keys.IndexOf("ImpactPunishRangeMetres");
        Assert.Equal(new[] { "ImpactShrugOffYieldsToImpact", "ImpactFallBackupSeconds" }, keys.Skip(i + 1).Take(2));
        var r = Rules();
        Assert.True(r.ShrugOffYields);
        Assert.Equal(0.5, r.FallBackupSeconds, 6);
        Assert.Equal(SettingsPage.Advanced, SettingsSchema.ImpactFallBackupSeconds.Group.Page);
        Assert.False(Rules(s => s.Set(SettingsSchema.ImpactShrugOffYieldsToImpact, false, SettingSources.Mcm)).ShrugOffYields);
        Assert.Equal(0, Rules(s => s.Set(SettingsSchema.ImpactFallBackupSeconds, 0.0, SettingSources.Mcm)).FallBackupSeconds);
        Assert.Equal(ImpactFallMath.MaxBackupSeconds, SettingsSchema.ImpactFallBackupSeconds.Max);
    }

    [Fact]
    public void The_backup_is_due_once_its_wait_is_over_and_never_after_a_fall_showed_or_a_backup_went()
    {
        Assert.False(ImpactFallMath.BackupDue(100.4, 100, 0.5, appeared: false, sent: false));
        Assert.True(ImpactFallMath.BackupDue(100.5, 100, 0.5, appeared: false, sent: false));
        Assert.True(ImpactFallMath.BackupDue(101.2, 100, 0.5, appeared: false, sent: false));
        Assert.False(ImpactFallMath.BackupDue(101.2, 100, 0.5, appeared: true, sent: false));  // the game's fall showed
        Assert.False(ImpactFallMath.BackupDue(101.2, 100, 0.5, appeared: false, sent: true));  // once
        Assert.False(ImpactFallMath.BackupDue(101.2, 100, 0, appeared: false, sent: false));   // 0 = no backup
        Assert.False(ImpactFallMath.BackupDue(101.2, double.NaN, 0.5, appeared: false, sent: false));
    }

    [Fact]
    public void A_blow_that_leaves_him_under_one_hit_point_kills_and_is_never_decided()
    {
        Assert.True(ImpactFallMath.Fatal(10, 10));
        Assert.True(ImpactFallMath.Fatal(10, 9.5));
        Assert.False(ImpactFallMath.Fatal(10, 9));
        Assert.False(ImpactFallMath.Fatal(100, 0));
        Assert.False(ImpactFallMath.Fatal(double.NaN, 5));
        var stats = new ImpactStats();
        stats.Skip(ImpactSkip.Fatal, ImpactEvent.CleanHit);
        stats.EverOn = true;
        var d = new ImpactDecision { Event = ImpactEvent.CleanHit, Bar = 0.5, Knockdown = ImpactRoll.None, ShieldDrop = ImpactRoll.None, Stagger = ImpactRoll.None, Riposte = ImpactRoll.None };
        stats.Note(in d, false, true, 0.001, 0);
        Assert.Contains(stats.SummaryLines(Rules()), l => l.Contains("the blow killed him (step 62) 1"));
    }

    [Fact]
    public void The_falls_line_counts_decided_played_by_the_game_by_the_backup_not_seen_and_the_shrug_offs_refused()
    {
        var f = new ImpactFallStats();
        Assert.Contains("no knockdown decided on a landed blow this mission", f.SummaryLine(true, 0.5, true));
        for (int i = 0; i < 5; i++) f.NoteDecided(ImpactEvent.CleanHit, 12);
        f.NoteDecided(ImpactEvent.CleanHit, 0);
        f.NoteDecided(ImpactEvent.Kick, 2);
        f.NotePlayed(ImpactEvent.CleanHit, false, 0.02);
        f.NotePlayed(ImpactEvent.CleanHit, false, 0.04);
        f.NotePlayed(ImpactEvent.CleanHit, true, 0.55);
        f.NotePlayed(ImpactEvent.Kick, false, 0.0);
        f.NoteNotSeen(ImpactEvent.CleanHit);
        f.NoteLeftField(ImpactEvent.CleanHit);
        f.BackupsSent = 2;
        f.ShrugRefusedForFall = 3;
        f.ShrugRefusedForStagger = 4;
        f.Fatal = 9;
        string line = f.SummaryLine(true, 0.5, true);
        Assert.Contains("decided 7 (kicks / bashes 1, clean hits 6) → played by the game 3 (0.02 s after the blow), by our backup blow 1 (0.55 s after the blow), STILL NOT SEEN 1, the man left the field / died first 1", line);
        Assert.Contains("clean hits 2 / 1 / 1", line);
        Assert.Contains("on a 0-damage blow (the engine plays no reaction) 1", line);
        Assert.Contains("backups (ImpactFallBackupSeconds 0.5 s) sent 2", line);
        Assert.Contains("the game's shrug-off REFUSED for a decided fall 3 / stagger 4", line);
        Assert.Contains("(ImpactShrugOffYieldsToImpact on;", line);
        Assert.Contains("landed blows not decided because they KILLED 9", line);
        Assert.Contains("ImpactFallBackupSeconds 0 - off", f.SummaryLine(true, 0, false));
        Assert.Contains("ImpactShrugOffYieldsToImpact OFF", f.SummaryLine(true, 0, false));
    }
}

/// <summary>
/// Step 62's WORKED EXAMPLES (docs/MECHANICS.md §11 "The charge", AI_NOTES "Step 62") on the SHIPPED values - DESIGN's initial values with
/// defaults.json's step-56 block model (right block / parry 30 / 10, the riposte 70, the stun's lengthening 0). The troops (the game's own
/// spnpccharacters.xml / bandits.xml): an IMPERIAL LEGIONARY - Athletics 130 (pool 130), one-handed 130, a 1.3 kg sword at 92, a 4 kg shield,
/// armour 110, level 26 (toughness 5.97); a LOOTER - Athletics 30 (pool 50, the floor), a 1.5 kg club at 85 (blunt), skill 30, armour 8, level 5
/// (1.35); a PEASANT ARCHER - Athletics 40 (pool 50), a bow in his hand, armour 15, level 11 (1.78); YOU - a pool of 120, skill 150, a 1.4 kg
/// sword at 95, armour 80, level 20 (4.33). Every swing at the game's tired animation (0.85 + 0.15 f).
/// </summary>
public class ChargeExamplesTests
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
    private static readonly Troop Archer = new(50, 40, 1.3, 92, ImpactDamageType.Cut, 15, 11);
    private static readonly Troop You = new(120, 150, 1.4, 95, ImpactDamageType.Cut, 80, 20);

    private static ImpactRules Shipped()
    {
        var s = new TraxSettings();
        s.Set(SettingsSchema.RightBlockStaggerMaxPercent, 30, "test");
        s.Set(SettingsSchema.RightBlockKnockdownMaxPercent, 10, "test");
        s.Set(SettingsSchema.ParryStaggerMaxPercent, 30, "test");
        s.Set(SettingsSchema.ParryKnockdownMaxPercent, 10, "test");
        s.Set(SettingsSchema.RiposteStaggerMaxPercent, 70, "test");
        s.Set(SettingsSchema.BlockStunTiredExtraPercent, 0, "test");
        // step 62 as shipped: direction by the multipliers (x2 / x1.4), no pressure - step 63's own examples are Step63ExamplesTests
        s.Set(SettingsSchema.ImpactRearPercent, 200, "test");
        s.Set(SettingsSchema.ImpactFlankPercent, 140, "test");
        s.Set(SettingsSchema.ImpactRearPressure, 0, "test");
        s.Set(SettingsSchema.ImpactFlankPressure, 0, "test");
        return ImpactRules.From(s);
    }

    private static ImpactFacts Blow(ImpactEvent e, Troop a, double aBar, Troop d, double dBar, bool charging = false, double angle = 0, bool bow = false, bool you = false)
    {
        double fA = Math.Min(1, aBar / 0.75);
        return new ImpactFacts
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
            AttackerSpeed = charging ? 3.0 : 1.0, // he slowed to swing - the charge carries it
            AttackerCharging = charging,
            AttackAngleDegrees = angle,
            DefenderRangedInHand = bow,
            AttackerToughness = a.Toughness,
            DefenderToughness = d.Toughness,
            AttackerIsPlayer = you,
        };
    }

    private static ImpactDecision Decide(in ImpactFacts f)
    {
        var r = Shipped();
        var d = default(ImpactState);
        var a = default(ImpactState);
        return ImpactMath.Decide(in r, in f, ref d, ref a, 100, new Never());
    }

    private static void Near(double expected, double actual, double tol, string what) =>
        Assert.True(Math.Abs(expected - actual) <= tol, what + ": " + (100 * actual).ToString("0.0") + "% vs " + (100 * expected).ToString("0") + "%");

    [Fact]
    public void A_legionary_charging_into_a_peasant_archers_back_throws_him_down()
    {
        Assert.Equal(1.78, Archer.Toughness, 2);
        // standing, from the front: the fresh archer staggered often, rarely down
        var front = Decide(Blow(ImpactEvent.CleanHit, Legionary, 1.0, Archer, 1.0, bow: true));
        Near(0.01, front.Knockdown.Chance, 0.015, "standing, front, a fresh archer - down");
        Near(0.29, front.Stagger.Chance, 0.03, "standing, front, a fresh archer - staggered");
        // the charge into his BACK: a fresh archer down 41 %, a tired one (40 %) at the clean hit's ceiling, 50 %
        var fresh = Decide(Blow(ImpactEvent.CleanHit, Legionary, 1.0, Archer, 1.0, charging: true, angle: 160, bow: true));
        Near(0.41, fresh.Knockdown.Chance, 0.02, "charging into a fresh archer's back - down");
        Assert.False(fresh.Capped); // x7.6 in all - under the cap of 8
        var tired = Decide(Blow(ImpactEvent.CleanHit, Legionary, 1.0, Archer, 0.4, charging: true, angle: 160, bow: true));
        Near(0.50, tired.Knockdown.Chance, 0.001, "charging into a tired (40 %) archer's back - down");
        // standing behind him: 23 % fresh
        Near(0.23, Decide(Blow(ImpactEvent.CleanHit, Legionary, 1.0, Archer, 1.0, angle: 160, bow: true)).Knockdown.Chance, 0.02, "standing, from behind, fresh - down");
    }

    [Fact]
    public void Looters_charging_a_fresh_legionary_shield_wall_bounce_many_reel_a_fair_share_thrown_back()
    {
        var charge = Decide(Blow(ImpactEvent.RightBlock, Looter, 1.0, Legionary, 1.0, charging: true));
        Near(0.33, charge.ThrownBack.Chance, 0.02, "a fresh looter's charge - thrown back");
        Near(0.58, charge.Riposte.Chance, 0.02, "a fresh looter's charge - the reel's chance (when not thrown)");
        Assert.Equal(0, charge.Knockdown.Chance);
        Assert.Equal(0, charge.Stagger.Chance);                 // the legionary does not move
        var tired = Decide(Blow(ImpactEvent.RightBlock, Looter, 0.6, Legionary, 1.0, charging: true));
        Near(0.40, tired.ThrownBack.Chance, 0.005, "a looter at 60 % charging - thrown back (the ceiling)");
        var walk = Decide(Blow(ImpactEvent.RightBlock, Looter, 1.0, Legionary, 1.0));
        Assert.False(walk.ThrownBack.InRow);                     // walking: never thrown
        Near(0.34, walk.Riposte.Chance, 0.02, "a fresh looter walking in - reels");
        // twenty looters into twenty fresh legionaries, each blocked right (seeded): many reel, a fair share on the ground
        var r = Shipped();
        var dice = new SeededRandom(62);
        int thrown = 0, reeled = 0;
        for (int i = 0; i < 20; i++)
        {
            var f = Blow(ImpactEvent.RightBlock, Looter, 1.0, Legionary, 1.0, charging: true);
            var ds = default(ImpactState);
            var a = default(ImpactState);
            var d = ImpactMath.Decide(in r, in f, ref ds, ref a, 100, dice);
            if (d.ThrownBack.Happens) thrown++;
            else if (d.Riposte.Happens) reeled++;
        }
        Assert.InRange(thrown, 3, 12);
        Assert.InRange(thrown + reeled, 8, 20);
    }

    [Fact]
    public void You_charging_into_a_fresh_high_tier_shieldman_blocking_right_can_be_thrown_back()
    {
        Assert.Equal(4.33, You.Toughness, 2);
        var fresh = Decide(Blow(ImpactEvent.RightBlock, You, 1.0, Legionary, 1.0, charging: true, you: true));
        Near(0.12, fresh.ThrownBack.Chance, 0.02, "you fresh, charging - thrown back");
        var half = Decide(Blow(ImpactEvent.RightBlock, You, 0.5, Legionary, 1.0, charging: true, you: true));
        Near(0.37, half.ThrownBack.Chance, 0.02, "you at half a bar, charging - thrown back");
        var walk = Decide(Blow(ImpactEvent.RightBlock, You, 1.0, Legionary, 1.0, you: true));
        Assert.False(walk.ThrownBack.InRow);
        Assert.True(walk.Riposte.Chance < 0.03, "you walking in fresh - reel " + walk.Riposte.Chance);
        // "Against you" 50 % halves it
        var s = new TraxSettings();
        s.Set(SettingsSchema.RiposteStaggerMaxPercent, 70, "test");
        s.Set(SettingsSchema.ImpactOnPlayerPercent, 50, "test");
        var r = ImpactRules.From(s);
        var f = Blow(ImpactEvent.RightBlock, You, 0.5, Legionary, 1.0, charging: true, you: true);
        var ds = default(ImpactState);
        var a = default(ImpactState);
        Near(half.ThrownBack.Chance / 2, ImpactMath.Decide(in r, in f, ref ds, ref a, 100, new Never()).ThrownBack.Chance, 0.005, "against you 50 %");
    }

    [Fact]
    public void Two_fresh_even_men_and_a_walking_man_into_an_even_right_block_stay_at_nothing()
    {
        var hit = Decide(Blow(ImpactEvent.CleanHit, Legionary, 1.0, Legionary, 1.0));
        Assert.Equal(0, hit.Knockdown.Chance);
        Assert.Equal(0, hit.Stagger.Chance);
        var walk = Decide(Blow(ImpactEvent.RightBlock, Legionary, 1.0, Legionary, 1.0));
        Assert.False(walk.ThrownBack.InRow);
        Assert.True(walk.Riposte.Chance < 0.02 && walk.Stagger.Chance == 0 && walk.Knockdown.Chance == 0, "walking into an even block: reel " + walk.Riposte.Chance);
        // the same man SPRINTING into the even block risks being thrown back (10 %) - the blocker still never moves
        var sprint = Decide(Blow(ImpactEvent.RightBlock, Legionary, 1.0, Legionary, 1.0, charging: true));
        Near(0.10, sprint.ThrownBack.Chance, 0.02, "an even man charging into a right block - thrown back");
        Assert.Equal(0, sprint.Stagger.Chance);
    }
}

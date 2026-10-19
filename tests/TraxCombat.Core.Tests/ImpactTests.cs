using System;
using System.Collections.Generic;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 48 (IMPACT I2, the model - docs/IMPACT.md §3-§5): the settings (placed, live, the master switch first, shadow, the families),
/// the S-curve, the even band, the skill / weight / impact / guard numbers, EVERY worked example of IMPACT §4 (A-G, computed by the step-45
/// script with the same formulas: a sword 1.2 kg at speed 92, the king's sword at 95, a 3 kg two-handed axe at 80, a 0.6 kg stick at 95, a
/// 1.8 kg mace at 85; the doc rounds to whole percents and two decimals), the lengths of §3.7 and their randomness bounds, the anti-abuse
/// state (immunity, halving, the reeling bonus), the player's share, riders, the shield, the damage term, and the summary.
/// </summary>
public class ImpactTests
{
    // ------------------------------------------------------------------ helpers

    /// <summary>Dice that answer the given numbers in order, then <see cref="Rest"/> (0.999 = nothing happens).</summary>
    private sealed class Dice : IRandomSource
    {
        private readonly Queue<double> _q;
        public double Rest = 0.999;
        public int Thrown;

        public Dice(params double[] values) => _q = new Queue<double>(values);

        public double NextDouble()
        {
            Thrown++;
            return _q.Count > 0 ? _q.Dequeue() : Rest;
        }
    }

    private readonly record struct Man(double Pool, double Bar, double Skill)
    {
        public double Points => Pool * Bar;
        public double F => Math.Min(1, Bar / 0.75); // the peak line at 75 % (AthleticsPeakPercent)
    }

    private readonly record struct Weapon(double Kg, double Speed, ImpactDamageType Type);

    private static readonly Weapon Sword = new(1.2, 92, ImpactDamageType.Cut);
    private static readonly Weapon KingSword = new(1.2, 95, ImpactDamageType.Cut);
    private static readonly Weapon Axe2H = new(3.0, 80, ImpactDamageType.Cut);
    private static readonly Weapon Stick = new(0.6, 95, ImpactDamageType.Blunt);
    private static readonly Weapon Mace = new(1.8, 85, ImpactDamageType.Blunt);

    /// <summary>Step 56: IMPACT §4's worked examples are the STEP-48 model - a correct block counted the blocker's tiredness in full and the
    /// riposte was × his f - so these tests pin <c>CorrectBlockTiredWeightPercent</c> and <c>RiposteBlockerTiredPercent</c> at 100 (step
    /// 48 exactly); step 56's model has its own tests (RightBlocksTests).</summary>
    private static ImpactRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        s.Set(SettingsSchema.CorrectBlockTiredWeightPercent, 100, "test");
        s.Set(SettingsSchema.RiposteBlockerTiredPercent, 100, "test");
        tune?.Invoke(s);
        return ImpactRules.From(s);
    }

    private static ImpactFacts Facts(ImpactEvent e, Man a, Man d, Weapon? w = null, double guardKg = 0, double animation = 1, bool shield = true)
    {
        var wp = w ?? Sword;
        return new ImpactFacts
        {
            Event = e,
            AttackerPoints = a.Points,
            AttackerF = a.F,
            AttackerAnimation = animation,
            AttackerSkill = a.Skill,
            DefenderPoints = d.Points,
            DefenderBar = d.Bar,
            DefenderF = d.F,
            DefenderSkill = d.Skill,
            WeaponWeight = e == ImpactEvent.Kick ? 0 : wp.Kg,
            SwingSpeed = wp.Speed,
            DamageType = wp.Type,
            GuardWeight = guardKg,
            DefenderHasShield = shield,
            DefenderMaxHealth = 100,
        };
    }

    private static ImpactDecision Decide(in ImpactFacts f, ImpactRules? rules = null, double now = 100, IRandomSource? dice = null)
    {
        var r = rules ?? Rules();
        var d = default(ImpactState);
        var a = default(ImpactState);
        return ImpactMath.Decide(in r, in f, ref d, ref a, now, dice ?? new Dice());
    }

    /// <summary>The doc's whole-percent figure. The step-45 script printed one decimal and the doc rounded that again (10.47 → "10.5" →
    /// 11), so a figure may sit up to 0.55 points away.</summary>
    private static void Pct(double docPercent, double chance, string what) =>
        Assert.True(Math.Abs(100 * chance - docPercent) <= 0.55, what + ": " + (100 * chance).ToString("0.00") + "% vs the doc's " + docPercent + "%");

    private static void X(double doc, double x, string what) =>
        Assert.True(Math.Abs(x - doc) <= 0.0051, what + ": x " + x.ToString("0.000") + " vs the doc's " + doc);

    private static void Near(double expected, double actual, double tol = 1e-6) =>
        Assert.True(Math.Abs(expected - actual) <= tol, actual + " vs " + expected);

    // ------------------------------------------------------------------ the settings

    [Fact]
    public void Every_IMPACT_section_5_setting_is_in_its_own_group_live_after_the_fever()
    {
        var keys = new[]
        {
            "ImpactEnabled", "ImpactKicksEnabled", "ImpactBlocksEnabled", "ImpactHitsEnabled", "ImpactRiposteEnabled", "ImpactShadowMode",
            "KickShieldDropMaxPercent", "KickKnockdownMaxPercent", "RightBlockStaggerMaxPercent", "RightBlockShieldDropMaxPercent", "RightBlockKnockdownMaxPercent",
            "ParryStaggerMaxPercent", "ParryKnockdownMaxPercent", "WrongBlockStaggerMaxPercent", "WrongBlockShieldDropMaxPercent", "WrongBlockKnockdownMaxPercent",
            "HitStaggerMaxPercent", "HitKnockdownMaxPercent", "RiposteStaggerMaxPercent",
            "CorrectBlockTiredWeightPercent", "RiposteBlockerTiredPercent", // step 56
            "StaggerBaseSeconds", "StaggerTiredExtraPercent", "StaggerMaxSeconds",
            "KickReelTiredExtraPercent", "GetUpMaxSlowdown", "BlockStunTiredExtraPercent", "RiposteStunMaxSeconds", "ImpactRandomPercent", "ImpactStunCapSeconds",
            "ImpactStaggerImmunitySeconds", "ImpactKnockdownImmunitySeconds", "ImpactDiminishWindowSeconds", "ImpactDiminishFactor", "ShieldPickupLockSeconds",
            "ImpactOnPlayerPercent", "ImpactRiderDismountPercent", "ImpactTiredNeverShrugsOff",
            "ImpactMomentumEnabled", "ImpactMomentumPercent", "ImpactMomentumWalkMetresPerSecond", "ImpactMomentumSprintMetresPerSecond", // step 52
            "ImpactMomentumRiderPercent", "ImpactMomentumRiderWalkMetresPerSecond", "ImpactMomentumRiderGallopMetresPerSecond", // step 53
            "ImpactShowMessages", "ImpactMessagesIncludeYourMen", "ImpactMessageMinSeconds", // step 53
            "ImpactPunishEnabled", "ImpactPunishAggressionPercent", "ImpactPunishGraceSeconds", "ImpactPunishRangeMetres", // step 54
            "ImpactShrugOffYieldsToImpact", "ImpactFallBackupSeconds", // step 62: the lost falls
            "ImpactToughnessWeight", "ImpactRangedInHandPercent", // step 62: toughness, a ranged man in melee
            "ImpactRearPercent", "ImpactFlankPercent", "ImpactRearDegrees", "ImpactFlankDegrees", // step 62: direction
            "ImpactChargeCarrySeconds", "RiposteKnockdownMaxPercent", "RiposteKnockdownFrom", "RiposteMomentumPercent", // step 62: the charge
            "ImpactMismatchWeight", "ImpactEvenBandDoublings",
            "ImpactBodyExponent", "ImpactBodyFloorPoints", "ImpactStaggeredBonus", "ImpactDamageWeight", "ImpactRefWeightKg", "ImpactWeightExponent",
            "ImpactRefSwingSpeed", "ImpactSpeedExponent", "ImpactBluntFactor", "ImpactCutFactor", "ImpactPierceFactor", "ImpactKickFactor", "ImpactBashFactor",
            "ImpactSkillMinFactor", "ImpactSkillMaxFactor", "ImpactGuardShieldRight", "ImpactGuardParry", "ImpactGuardShieldWrong", "ImpactGuardNone",
            "ImpactStaggerFrom", "ImpactShieldDropFrom", "ImpactKnockdownFrom",
        };
        Assert.Equal(88, keys.Length); // step 48's 60 + step 52's momentum 4 + step 53's rider momentum 3 and on-screen lines 3 + step 54's punish 4 + step 56's 2 + step 62's
        // Step 59: three of them on the player page - the switch and "against you" ("Staggers and knockdowns"), the on-screen lines
        // ("On screen"); the other 73 stay in this group in order.
        var player = new[] { "ImpactEnabled", "ImpactOnPlayerPercent", "ImpactShowMessages" };
        var inGroup = SettingsSchema.InGroup(SettingsSchema.ImpactGroup).Select(p => p.Key).ToList();
        Assert.Equal(keys.Where(k => !player.Contains(k)), inGroup);
        Assert.All(player, k => { SettingsSchema.TryGet(k, out var p); Assert.Equal(SettingsPage.Player, p.Group.Page); });
        foreach (var k in keys)
        {
            Assert.True(SettingsSchema.TryGet(k, out var p), k);
            Assert.Equal(ApplyTiming.Live, p.Timing);
        }
        Assert.Equal(SettingsSchema.FeverGroup.Order + 3, SettingsSchema.ImpactGroup.Order); // step 49: courage between them; step 55: cavalry flocking after it
        Assert.Equal(SettingsSchema.ImpactGroup.Order + 1, SettingsSchema.RefillGroup.Order);
        Assert.Same(SettingsSchema.AdvancedGroup, SettingsSchema.Groups[SettingsSchema.Groups.Count - 1]);
    }

    [Fact]
    public void Defaults_are_IMPACT_section_5s_with_the_shadow_switch_off_and_the_damage_term_at_0()
    {
        var s = new TraxSettings();
        Assert.True(s.ImpactEnabled);
        Assert.False(s.ImpactShadowMode); // Claude's call 2026-10-17 night (DESIGN decision 40): Anton sees it live in the morning
        Assert.Equal(0f, s.ImpactDamageWeight); // IMPACT §8 q1 - waits for Anton's answer
        var r = Rules();
        Assert.Equal(0.9, r.KickShieldDrop, 6);
        Assert.Equal(0.8, r.KickKnockdown, 6);
        Assert.Equal(0.8, r.RightStagger, 6);
        Assert.Equal(0.0, r.RightShieldDrop, 6);
        Assert.Equal(0.4, r.RightKnockdown, 6);
        Assert.Equal(0.9, r.WrongStagger, 6);
        Assert.Equal(0.5, r.WrongShieldDrop, 6);
        Assert.Equal(0.5, r.HitKnockdown, 6);
        Assert.Equal(0.5, r.RiposteMax, 6);
        Assert.Equal(0.25, r.MismatchWeight, 6);
        Assert.Equal(0.5, r.EvenBand, 6);
        Assert.Equal(0.75, r.StaggeredBonus, 6);
        Assert.Equal(10, r.BodyFloor, 6);
    }

    [Fact]
    public void The_master_switch_decides_first_then_Athletics_then_ImpactEnabled_then_the_family()
    {
        Assert.True(Rules().Enabled);
        Assert.Null(Rules().OffBecause);
        var off = Rules(s => s.Set(SettingsSchema.ModEnabled, false, "test"));
        Assert.False(off.Enabled);
        Assert.Equal("ModEnabled", off.OffBecause);
        Assert.Equal("ModEnabled", Rules(s =>
        {
            s.Set(SettingsSchema.ModEnabled, false, "test");
            s.Set(SettingsSchema.ImpactEnabled, false, "test");
            s.Set(SettingsSchema.AthleticsEnabled, false, "test");
        }).OffBecause);
        Assert.Equal("AthleticsEnabled", Rules(s => s.Set(SettingsSchema.AthleticsEnabled, false, "test")).OffBecause);
        Assert.Equal("ImpactEnabled", Rules(s => s.Set(SettingsSchema.ImpactEnabled, false, "test")).OffBecause);
        Assert.False(off.Decides(ImpactFamily.Kicks));
        var noKicks = Rules(s => s.Set(SettingsSchema.ImpactKicksEnabled, false, "test"));
        Assert.False(noKicks.Decides(ImpactFamily.Kicks));
        Assert.True(noKicks.Decides(ImpactFamily.Blocks));
        Assert.False(Rules(s => s.Set(SettingsSchema.ImpactBlocksEnabled, false, "test")).Decides(ImpactFamily.Blocks));
        Assert.False(Rules(s => s.Set(SettingsSchema.ImpactHitsEnabled, false, "test")).Decides(ImpactFamily.Hits));
        Assert.False(Rules(s => s.Set(SettingsSchema.ImpactRiposteEnabled, false, "test")).Decides(ImpactFamily.Riposte));
    }

    [Fact]
    public void Step_52_every_family_applies_live_and_the_spike_forces_shadow()
    {
        Assert.Equal(ImpactFamily.Kicks | ImpactFamily.Blocks | ImpactFamily.Riposte | ImpactFamily.Hits, ImpactMath.EffectsBuilt);
        var live = Rules();
        foreach (var f in new[] { ImpactFamily.Kicks, ImpactFamily.Blocks, ImpactFamily.Riposte, ImpactFamily.Hits })
        {
            Assert.True(live.Applies(f));
            Assert.True(live.KeepsState(f));
        }
        Assert.False(Rules(s => s.Set(SettingsSchema.ImpactHitsEnabled, false, "test")).Applies(ImpactFamily.Hits)); // step 52: its own switch
        Assert.False(live.ShadowNow);
        Assert.Equal("LIVE - kicks / bashes, blocks, clean hits, the riposte applied", live.ModeText());
        var shadow = Rules(s => s.Set(SettingsSchema.ImpactShadowMode, true, "test"));
        Assert.True(shadow.ShadowNow);
        Assert.StartsWith("SHADOW (ImpactShadowMode)", shadow.ModeText());
        var spike = Rules(s => s.Set(SettingsSchema.ImpactSpike, true, "test"));
        Assert.True(spike.ShadowNow);
        Assert.StartsWith("SHADOW (the spike is on", spike.ModeText());
        Assert.StartsWith("OFF (ModEnabled)", Rules(s => s.Set(SettingsSchema.ModEnabled, false, "test")).ModeText());
    }

    [Fact]
    public void The_settings_are_read_live_a_ceiling_change_moves_the_next_decision()
    {
        var s = new TraxSettings();
        var f = Facts(ImpactEvent.Kick, new Man(100, 1, 100), new Man(100, 0, 100));
        Pct(80, Decide(in f, ImpactRules.From(s)).Knockdown.Chance, "the kick's knockdown on an empty even man");
        s.Set(SettingsSchema.KickKnockdownMaxPercent, 60, SettingSources.Mcm);
        Pct(60, Decide(in f, ImpactRules.From(s)).Knockdown.Chance, "after the ceiling moved to 60");
        s.Set(SettingsSchema.ImpactEvenBandDoublings, 0, SettingSources.Mcm);
        Assert.True(Decide(in f, ImpactRules.From(s)).X > 1.0); // the kick's 1.2 now counts beyond the band
    }

    [Fact]
    public void The_sentence_carries_the_anchors_in_words()
    {
        string on = Rules().Describe();
        foreach (var part in new[] { "a kick on an empty even man: shield 90%, down 80%", "a block on the right side: stagger 80% / down 40%", "a parry: stagger 80% / down 40%",
                     "the wrong side: stagger 90% / shield 50% / down 40%", "a clean hit: stagger 80% / down 50%", "the riposte 50%", "two even fresh men: 0% always",
                     "0.60 s fresh → 1.50 s empty", "no new stagger 2.0 s after one", "no new knockdown 5.0 s after getting up", "each outcome x0.50 for 8.0 s", "against you 100%" })
            Assert.Contains(part, on);
        Assert.StartsWith("OFF (ImpactEnabled) - the game's own reactions", Rules(s => s.Set(SettingsSchema.ImpactEnabled, false, "test")).Describe());
    }

    // ------------------------------------------------------------------ the curve, the band, the numbers

    [Fact]
    public void The_S_curve_is_smooth_from_0_to_1_and_flat_outside()
    {
        Assert.Equal(0, ImpactMath.S(-0.5));
        Assert.Equal(0, ImpactMath.S(0));
        Assert.Equal(0.5, ImpactMath.S(0.5), 12);
        Assert.Equal(1, ImpactMath.S(1));
        Assert.Equal(1, ImpactMath.S(3));
        Assert.Equal(0, ImpactMath.S(double.NaN));
        Near(0.104, ImpactMath.S(0.2), 1e-9); // 3(0.04) − 2(0.008)
        double prev = 0;
        for (double t = 0.01; t <= 1; t += 0.01)
        {
            double v = ImpactMath.S(t);
            Assert.True(v >= prev);
            prev = v;
        }
        // from 0.35: nothing up to 0.35, half way at 0.675, the ceiling at 1
        Assert.Equal(0, ImpactMath.Curve(0.35, 0.35));
        Near(0.5, ImpactMath.Curve(0.675, 0.35), 1e-9);
        Assert.Equal(1, ImpactMath.Curve(1, 0.35));
    }

    [Fact]
    public void The_even_band_counts_nothing_inside_and_only_the_part_beyond()
    {
        var r = Rules();
        Assert.Equal(0, ImpactMath.Band(in r, 0));
        Assert.Equal(0, ImpactMath.Band(in r, 0.5));
        Assert.Equal(0, ImpactMath.Band(in r, -0.49));
        Near(1.5, ImpactMath.Band(in r, 2));
        Near(-1.5, ImpactMath.Band(in r, -2));
        // R = 4 (two doublings) adds 0.375 (IMPACT §3.2)
        Near(0.375, r.MismatchWeight * ImpactMath.Band(in r, Math.Log(4, 2)), 1e-9);
    }

    [Fact]
    public void Skill_weight_impact_and_guard_numbers_are_IMPACT_section_3_3s()
    {
        var r = Rules();
        Near(0.7833, ImpactMath.SkillFactor(in r, 20), 1e-4);   // a recruit
        Near(1.0833, ImpactMath.SkillFactor(in r, 200), 1e-4);  // a veteran
        Near(1.25, ImpactMath.SkillFactor(in r, 300), 1e-6);    // a 300 lord
        Near(1.25, ImpactMath.SkillFactor(in r, 500), 1e-6);    // capped
        Near(0.75, ImpactMath.SkillFactor(in r, -5), 1e-6);
        Near(0.87, ImpactMath.SwingImpact(in r, 0.6, 95, 1, ImpactDamageType.Blunt), 0.005); // a stick
        Near(0.91, ImpactMath.SwingImpact(in r, 1.2, 92, 1, ImpactDamageType.Cut), 0.005);   // a sword
        Near(1.34, ImpactMath.SwingImpact(in r, 1.8, 85, 1, ImpactDamageType.Blunt), 0.006); // a mace
        Near(1.26, ImpactMath.SwingImpact(in r, 3.0, 80, 1, ImpactDamageType.Cut), 0.005);   // a two-handed axe
        Near(0.8 * 0.914, ImpactMath.SwingImpact(in r, 1.2, 92, 1, ImpactDamageType.Pierce), 0.002);
        // a tired man's swing carries less: 85 % animation at empty
        Near(0.85 * 0.914, ImpactMath.SwingImpact(in r, 1.2, 92, 0.85, ImpactDamageType.Cut), 0.002);
        Near(1.63, ImpactMath.Guard(in r, ImpactEvent.RightBlock, 4.0), 0.005);
        Near(0.4 * 1.633, ImpactMath.Guard(in r, ImpactEvent.WrongBlock, 4.0), 0.002);
        Near(0.8 * Math.Sqrt(0.8), ImpactMath.Guard(in r, ImpactEvent.Parry, 1.2), 1e-6);
        Assert.Equal(1.0, ImpactMath.Guard(in r, ImpactEvent.CleanHit, 9));
        Assert.Equal(1.0, ImpactMath.WeightFactor(in r, 0)); // unknown = the reference
        var bash = Facts(ImpactEvent.Bash, new Man(100, 1, 100), new Man(100, 1, 100), new Weapon(6.0, 0, ImpactDamageType.Blunt));
        Near(2.0, ImpactMath.Impact(in r, in bash), 1e-9); // a 6 kg shield's bash
        Near(1.2, ImpactMath.Impact(in r, Facts(ImpactEvent.Kick, new Man(100, 1, 100), new Man(100, 1, 100))), 1e-6);
    }

    [Fact]
    public void Events_come_from_the_collision_kick_bash_blocks_and_landed_hits()
    {
        Assert.True(ImpactMath.EventOf(true, false, true, BlockKind.None, out var e) && e == ImpactEvent.Kick);
        Assert.True(ImpactMath.EventOf(true, true, true, BlockKind.None, out e) && e == ImpactEvent.Bash);
        Assert.False(ImpactMath.EventOf(true, false, false, BlockKind.None, out _)); // a kick into the shield
        Assert.True(ImpactMath.EventOf(false, true, false, BlockKind.ShieldRightSide, out e) && e == ImpactEvent.RightBlock);
        Assert.True(ImpactMath.EventOf(false, true, false, BlockKind.ShieldWrongSide, out e) && e == ImpactEvent.WrongBlock);
        Assert.True(ImpactMath.EventOf(false, true, false, BlockKind.WeaponParry, out e) && e == ImpactEvent.Parry);
        Assert.True(ImpactMath.EventOf(false, true, true, BlockKind.None, out e) && e == ImpactEvent.CleanHit);
        Assert.False(ImpactMath.EventOf(false, true, false, BlockKind.None, out _)); // a blow into nothing
        Assert.Equal(ImpactFamily.Kicks, ImpactMath.Family(ImpactEvent.Bash));
        Assert.Equal(ImpactFamily.Blocks, ImpactMath.Family(ImpactEvent.WrongBlock));
        Assert.Equal(ImpactFamily.Hits, ImpactMath.Family(ImpactEvent.CleanHit));
        Assert.Equal(0, ImpactMath.BarBand(1));
        Assert.Equal(0, ImpactMath.BarBand(0.75));
        Assert.Equal(1, ImpactMath.BarBand(0.6));
        Assert.Equal(2, ImpactMath.BarBand(0.25));
        Assert.Equal(3, ImpactMath.BarBand(0.1));
    }

    // ------------------------------------------------------------------ IMPACT §4 - the worked examples

    /// <summary>§4 A: two even men (pool 100, skill 100, a 1.2 kg sword, a 4 kg shield); the attacker fresh, the defender at the bar shown.
    /// (The 75 % row's wrong side reads 0.6 % - the doc printed it as 0 before step 48 corrected it to 1.)</summary>
    [Theory]
    //          bar   T     kick sh/down  right st/down  parry st/down  wrong st/sh/down  hit st/down
    [InlineData(1.00, 0.00, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)]
    [InlineData(0.75, 0.00, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0)]
    [InlineData(0.50, 0.33, 14, 1, 21, 0, 31, 1, 39, 12, 3, 21, 0)]
    [InlineData(0.25, 0.67, 82, 70, 59, 19, 77, 36, 88, 48, 38, 68, 35)]
    [InlineData(0.10, 0.87, 90, 80, 79, 39, 80, 40, 90, 50, 40, 80, 50)]
    [InlineData(0.00, 1.00, 90, 80, 80, 40, 80, 40, 90, 50, 40, 80, 50)]
    public void A_two_even_men(double bar, double t, double kSh, double kDn, double rSt, double rDn, double pSt, double pDn, double wSt, double wSh, double wDn, double hSt, double hDn)
    {
        var a = new Man(100, 1, 100);
        var d = new Man(100, bar, 100);
        var kick = Decide(Facts(ImpactEvent.Kick, a, d));
        Assert.True(Math.Abs(kick.T - t) < 0.005, "T " + kick.T);
        Pct(kSh, kick.ShieldDrop.Chance, "kick shield");
        Pct(kDn, kick.Knockdown.Chance, "kick down");
        Assert.False(kick.Stagger.InRow); // the kick's reel IS its stagger - never rolled
        var right = Decide(Facts(ImpactEvent.RightBlock, a, d, Sword, 4.0));
        Pct(rSt, right.Stagger.Chance, "right stagger");
        Pct(rDn, right.Knockdown.Chance, "right down");
        Assert.False(right.ShieldDrop.InRow); // RightBlockShieldDropMaxPercent 0
        var parry = Decide(Facts(ImpactEvent.Parry, a, d, Sword, 1.2));
        Pct(pSt, parry.Stagger.Chance, "parry stagger");
        Pct(pDn, parry.Knockdown.Chance, "parry down");
        var wrong = Decide(Facts(ImpactEvent.WrongBlock, a, d, Sword, 4.0));
        Pct(wSt, wrong.Stagger.Chance, "wrong stagger");
        Pct(wSh, wrong.ShieldDrop.Chance, "wrong shield");
        Pct(wDn, wrong.Knockdown.Chance, "wrong down");
        var hit = Decide(Facts(ImpactEvent.CleanHit, a, d));
        Pct(hSt, hit.Stagger.Chance, "hit stagger");
        Pct(hDn, hit.Knockdown.Chance, "hit down");
    }

    [Fact]
    public void A_two_EMPTY_even_men_every_kick_90_80_and_two_even_fresh_men_always_0()
    {
        var empty = new Man(100, 0, 100);
        var kick = Decide(Facts(ImpactEvent.Kick, empty, empty));
        Pct(90, kick.ShieldDrop.Chance, "shield");
        Pct(80, kick.Knockdown.Chance, "down");
        X(1.00, kick.X, "two empty men");
        var fresh = new Man(100, 1, 100);
        foreach (var e in new[] { ImpactEvent.Kick, ImpactEvent.RightBlock, ImpactEvent.Parry, ImpactEvent.CleanHit })
        {
            var d = Decide(Facts(e, fresh, fresh, Sword, e == ImpactEvent.Parry ? 1.2 : 4.0));
            Assert.Equal(0, d.Knockdown.Chance);
            Assert.Equal(0, d.ShieldDrop.Chance);
            Assert.Equal(0, d.Stagger.Chance);
            Assert.True(d.X <= 0.0001, e + ": x " + d.X);
        }
    }

    [Fact]
    public void B_the_200_at_100_king_against_the_50_at_20_man_both_ways()
    {
        var king = new Man(200, 1.0, 250);
        var man = new Man(50, 0.2, 60);
        var kick = Decide(Facts(ImpactEvent.Kick, king, man), dice: new Dice(0.999, 0.999, 0.5)); // no knockdown, no drop, the reel's roll at its centre
        X(1.33, kick.X, "the king kicks the man");
        Pct(90, kick.ShieldDrop.Chance, "shield");
        Pct(80, kick.Knockdown.Chance, "down");
        Near(2.1, kick.ReelFactor, 0.01); // "(reels ×2.1)"
        var right = Decide(Facts(ImpactEvent.RightBlock, king, man, KingSword, 3.5));
        X(1.09, right.X, "the king swings, the man blocks on the right side");
        Pct(40, right.Knockdown.Chance, "down");
        Pct(80, right.Stagger.Chance, "stagger");
        var wrong = Decide(Facts(ImpactEvent.WrongBlock, king, man, KingSword, 3.5));
        X(1.42, wrong.X, "... on the WRONG side");
        Pct(50, wrong.ShieldDrop.Chance, "shield");
        Pct(40, wrong.Knockdown.Chance, "down");
        Pct(90, wrong.Stagger.Chance, "stagger");
        var back = Decide(Facts(ImpactEvent.Kick, man, king));
        X(-0.46, back.X, "the man kicks the fresh king");
        Assert.Equal(0, back.ShieldDrop.Chance);
        Assert.Equal(0, back.Knockdown.Chance);
        Near(1.0, back.ReelFactor); // a vanilla knock-back only
        // the man's tired swing (step 20b's animation share 0.85 + 0.15 f) on the fresh king's shield
        var swing = Decide(Facts(ImpactEvent.RightBlock, man, king, new Weapon(1.2, 90, ImpactDamageType.Cut), 3.5, animation: 0.85 + 0.15 * man.F));
        X(-0.76, swing.X, "the man swings, the fresh king blocks");
        Assert.Equal(0, swing.Stagger.Chance);
        Assert.Equal(0, swing.Knockdown.Chance);
        var k30 = Decide(Facts(ImpactEvent.Kick, man, king with { Bar = 0.3 }));
        X(0.35, k30.X, "the man kicks the king at 30%");
        Pct(9, k30.ShieldDrop.Chance, "shield");
        Pct(0, k30.Knockdown.Chance, "down"); // x 0.353 sits a hair above the knockdown's 0.35 start: 0.007 %
        var k0 = Decide(Facts(ImpactEvent.Kick, man, king with { Bar = 0 }));
        X(1.00, k0.X, "the man kicks the king at 0% - an empty king is as weak as anyone");
        Pct(90, k0.ShieldDrop.Chance, "shield");
        Pct(80, k0.Knockdown.Chance, "down");
    }

    [Fact]
    public void C_the_peasants_stick_against_the_veteran_berserkers_two_handed_axe()
    {
        var berserker = new Man(180, 1.0, 200);
        var peasant = new Man(50, 1.0, 20);
        var parry = Decide(Facts(ImpactEvent.Parry, berserker, peasant, Axe2H, 0.6));
        X(0.55, parry.X, "the axe on a FRESH peasant's parry");
        Pct(46, parry.Stagger.Chance, "stagger");
        Pct(9, parry.Knockdown.Chance, "down");
        var tired = Decide(Facts(ImpactEvent.Parry, berserker, peasant with { Bar = 0.5 }, Axe2H, 0.6));
        X(1.01, tired.X, "the axe on the peasant's parry at 50%");
        Pct(80, tired.Stagger.Chance, "stagger");
        Pct(40, tired.Knockdown.Chance, "down");
        var clean = Decide(Facts(ImpactEvent.CleanHit, berserker, peasant, Axe2H));
        X(0.31, clean.X, "the axe lands clean on the fresh peasant");
        Pct(18, clean.Stagger.Chance, "stagger");
        Assert.Equal(0, clean.Knockdown.Chance);
        var stick = Decide(Facts(ImpactEvent.RightBlock, peasant, berserker, Stick, 4.0));
        X(-0.45, stick.X, "the peasant's stick on the fresh berserker's shield");
        Assert.Equal(0, stick.Stagger.Chance);
        Assert.Equal(0, stick.Knockdown.Chance);
    }

    [Fact]
    public void D_the_player_100_against_a_tournament_king_220()
    {
        var you = new Man(100, 1.0, 100);
        var king = new Man(220, 1.0, 250);
        var fresh = Decide(Facts(ImpactEvent.Kick, you, king));
        X(-0.04, fresh.X, "you kick the FRESH king");
        Assert.Equal(0, fresh.ShieldDrop.Chance);
        Assert.Equal(0, fresh.Knockdown.Chance);
        var k60 = Decide(Facts(ImpactEvent.Kick, you, king with { Bar = 0.6 }));
        X(0.20, k60.X, "at 60%");
        Assert.True(k60.ShieldDrop.Chance < 0.0001 && k60.Knockdown.Chance == 0);
        var k30 = Decide(Facts(ImpactEvent.Kick, you, king with { Bar = 0.3 }));
        X(0.60, k30.X, "at 30%");
        Pct(45, k30.ShieldDrop.Chance, "shield");
        Pct(26, k30.Knockdown.Chance, "down");
        var k10 = Decide(Facts(ImpactEvent.Kick, you, king with { Bar = 0.1 }));
        X(0.99, k10.X, "at 10% - tire him first, then break him");
        Pct(90, k10.ShieldDrop.Chance, "shield");
        Pct(80, k10.Knockdown.Chance, "down");
        // the king kicks YOU - your share (ImpactOnPlayerPercent 100) leaves the chances as they are
        var onYou = Facts(ImpactEvent.Kick, king, you);
        onYou.DefenderIsPlayer = true;
        var kf = Decide(in onYou);
        X(0.17, kf.X, "the king kicks you, fresh");
        Assert.Equal(0, kf.ShieldDrop.Chance);
        var at40 = Facts(ImpactEvent.Kick, king, you with { Bar = 0.4 });
        at40.DefenderIsPlayer = true;
        var k40 = Decide(in at40);
        X(0.80, k40.X, "the king kicks you at 40%");
        Pct(76, k40.ShieldDrop.Chance, "shield");
        Pct(62, k40.Knockdown.Chance, "down");
        var blk = Facts(ImpactEvent.RightBlock, king, you with { Bar = 0.4 }, Sword, 4.0);
        blk.DefenderIsPlayer = true;
        var b40 = Decide(in blk);
        X(0.53, b40.X, "the king swings, you block on the right side at 40%");
        Pct(7, b40.Knockdown.Chance, "down");
        Pct(43, b40.Stagger.Chance, "stagger");
    }

    [Theory]
    //          kg   speed  blunt  wrong@50 st/sh/down   →  @35 st/sh/down    parry@50 st/down → @35 st/down
    [InlineData(0.6, 95, true, 36, 11, 2, 71, 34, 22, 29, 1, 59, 19)]
    [InlineData(1.2, 92, false, 39, 12, 3, 73, 36, 24, 31, 1, 61, 21)]
    [InlineData(1.8, 85, true, 58, 24, 13, 85, 46, 35, 47, 10, 74, 33)]
    [InlineData(3.0, 80, false, 54, 22, 11, 83, 44, 34, 44, 8, 72, 31)]
    public void E_the_weapon_counts(double kg, double speed, bool blunt, double w50St, double w50Sh, double w50Dn, double w35St, double w35Sh, double w35Dn,
        double p50St, double p50Dn, double p35St, double p35Dn)
    {
        var w = new Weapon(kg, speed, blunt ? ImpactDamageType.Blunt : ImpactDamageType.Cut);
        var a = new Man(100, 1, 100);
        foreach (var (bar, st, sh, dn, pst, pdn) in new[] { (0.5, w50St, w50Sh, w50Dn, p50St, p50Dn), (0.35, w35St, w35Sh, w35Dn, p35St, p35Dn) })
        {
            var d = new Man(100, bar, 100);
            var wrong = Decide(Facts(ImpactEvent.WrongBlock, a, d, w, 4.0));
            Pct(st, wrong.Stagger.Chance, kg + " kg wrong-side stagger at " + bar);
            Pct(sh, wrong.ShieldDrop.Chance, kg + " kg wrong-side shield at " + bar);
            Pct(dn, wrong.Knockdown.Chance, kg + " kg wrong-side down at " + bar);
            var parry = Decide(Facts(ImpactEvent.Parry, a, d, w, 1.2));
            Pct(pst, parry.Stagger.Chance, kg + " kg parry stagger at " + bar);
            Pct(pdn, parry.Knockdown.Chance, kg + " kg parry down at " + bar);
        }
    }

    [Fact]
    public void F_a_man_already_reeling_from_an_impact_stagger()
    {
        var a = new Man(100, 1, 100);
        foreach (var (bar, kickDown, kickShield, swordDown, maceDown) in new[] { (1.0, 54.0, 69.0, 34.0, 34.0), (0.5, 80.0, 90.0, 50.0, 50.0) })
        {
            var d = new Man(100, bar, 100);
            ImpactDecision Reeling(in ImpactFacts f)
            {
                var r = Rules();
                var st = new ImpactState();
                st.Reel(200, 2); // reeling until 200 s
                var at = new ImpactState();
                return ImpactMath.Decide(in r, in f, ref st, ref at, 100, new Dice());
            }
            var kick = Reeling(Facts(ImpactEvent.Kick, a, d));
            Assert.True(kick.Reeling);
            Pct(kickDown, kick.Knockdown.Chance, "a kick on a reeling man at " + bar);
            Pct(kickShield, kick.ShieldDrop.Chance, "his shield at " + bar);
            var sword = Reeling(Facts(ImpactEvent.CleanHit, a, d));
            Pct(swordDown, sword.Knockdown.Chance, "a sword blow at " + bar);
            Assert.Equal(ImpactRefusal.StaggerImmune, sword.Stagger.Refusal); // immune while reeling
            var mace = Reeling(Facts(ImpactEvent.CleanHit, a, d, Mace));
            Pct(maceDown, mace.Knockdown.Chance, "a mace blow at " + bar);
        }
    }

    [Fact]
    public void G_the_riposte_50_20_0_45()
    {
        // computed with the attacker's swing at full animation speed (as the doc's script did)
        var vet = new Man(200, 1.0, 200);
        var rec = new Man(50, 0.4, 40);
        Pct(50, Decide(Facts(ImpactEvent.RightBlock, rec, vet, Sword, 4.0)).Riposte.Chance, "a veteran blocking a recruit at 40%");
        Pct(20, Decide(Facts(ImpactEvent.RightBlock, rec with { Bar = 1.0 }, vet, Sword, 4.0)).Riposte.Chance, "the same recruit fresh");
        var even = new Man(100, 1.0, 100);
        Assert.Equal(0, Decide(Facts(ImpactEvent.Parry, even, even, Sword, 1.2)).Riposte.Chance);
        var g = Decide(Facts(ImpactEvent.Parry, even with { Bar = 0.2 }, even, Sword, 1.2));
        Pct(45, g.Riposte.Chance, "a fresh man parrying an equal at 20%");
        // only on a right-side block or a parry; a tired defender ripostes less (× his f); its own switch
        Assert.False(Decide(Facts(ImpactEvent.WrongBlock, rec, vet, Sword, 4.0)).Riposte.InRow);
        Assert.False(Decide(Facts(ImpactEvent.CleanHit, rec, vet)).Riposte.InRow);
        Pct(50 * (0.5 / 0.75), Decide(Facts(ImpactEvent.RightBlock, rec, vet with { Bar = 0.5 }, Sword, 4.0)).Riposte.Chance, "the veteran at 50% (f 0.67)");
        Assert.False(Decide(Facts(ImpactEvent.RightBlock, rec, vet, Sword, 4.0), Rules(s => s.Set(SettingsSchema.ImpactRiposteEnabled, false, "test"))).Riposte.InRow);
        // it happens: the attacker recoils RiposteStunMaxSeconds × (0.5 + 0.5 × max(T_A, x_r) clamped to 1) - step 62: longer with the
        // comparison (step 48: × (0.5 + 0.5 T_A)) - and is reeling from then on
        var r = Rules();
        var dState = new ImpactState();
        var aState = new ImpactState();
        var f = Facts(ImpactEvent.RightBlock, rec, vet, Sword, 4.0);
        var dec = ImpactMath.Decide(in r, in f, ref dState, ref aState, 100, new Dice(0.0, 0.5, 0.5)); // the riposte's die (his own outcomes have no chance - not rolled), the stun's, the recoil's
        Assert.True(dec.Riposte.Happens);
        double ta = 1 - 0.4 / 0.75;
        Assert.True(dec.XRiposte > 1, "x_r " + dec.XRiposte); // the recruit at 40% into the veteran's shield: outmatched
        Near(1.0 * (0.5 + 0.5 * 1.0), dec.RiposteStunSeconds, 0.2 * ta + 1e-6);
        Assert.True(aState.Reeling(100.1) && aState.StaggerImmune(100 + dec.RiposteStunSeconds + 1.9));
    }

    // ------------------------------------------------------------------ the lengths (§3.7)

    [Theory]
    [InlineData(0.00, 0.60, 0.60, 0.60, 1.00, 1.00, 1.00)]
    [InlineData(0.25, 0.82, 0.76, 0.89, 1.38, 1.25, 1.25)]
    [InlineData(0.50, 1.05, 0.89, 1.21, 1.75, 1.50, 1.50)]
    [InlineData(0.75, 1.27, 0.99, 1.56, 2.12, 1.75, 1.75)]
    [InlineData(1.00, 1.50, 1.05, 1.95, 2.50, 2.00, 2.00)]
    public void Lengths_grow_and_get_less_predictable_with_tiredness(double t, double stagger, double lo, double hi, double reel, double getUp, double stun)
    {
        var r = Rules();
        Near(stagger, r.StaggerBase * ImpactMath.Lengthen(r.StaggerExtra, t, r.Random, 0.5), 0.006);
        Near(lo, r.StaggerBase * ImpactMath.Lengthen(r.StaggerExtra, t, r.Random, 0.0), 0.006);
        Near(hi, r.StaggerBase * ImpactMath.Lengthen(r.StaggerExtra, t, r.Random, 1.0), 0.006);
        Near(reel, ImpactMath.Lengthen(r.ReelExtra, t, r.Random, 0.5), 0.006);
        Near(getUp, ImpactMath.Lengthen(r.GetUpMax - 1, t, r.Random, 0.5), 0.006);
        Near(stun, ImpactMath.Lengthen(r.StunExtra, t, r.Random, 0.5), 0.006);
    }

    [Fact]
    public void The_randomness_stays_in_its_bounds_and_the_caps_hold()
    {
        var r = Rules();
        var dice = new SeededRandom(48);
        var empty = new Man(100, 0, 100);
        var fresh = new Man(100, 1, 100);
        double minSt = double.MaxValue, maxSt = 0, minUp = double.MaxValue, maxUp = 0;
        for (int i = 0; i < 4000; i++)
        {
            var dState = new ImpactState();
            var aState = new ImpactState();
            var f = Facts(ImpactEvent.WrongBlock, fresh, empty, Sword, 4.0);
            var d = ImpactMath.Decide(in r, in f, ref dState, ref aState, 100, dice);
            if (d.Stagger.Happens)
            {
                minSt = Math.Min(minSt, d.StaggerSeconds);
                maxSt = Math.Max(maxSt, d.StaggerSeconds);
            }
            if (d.Knockdown.Happens)
            {
                minUp = Math.Min(minUp, d.GetUpFactor);
                maxUp = Math.Max(maxUp, d.GetUpFactor);
            }
            Assert.Equal(2.0, d.StunFactor, 9); // step 51: no die - x (1 + T) exactly (the decorator's worker-thread seam applies this number)
        }
        Assert.True(minSt >= 1.05 - 1e-9 && maxSt <= 1.95 + 1e-9 && maxSt - minSt > 0.6, "stagger " + minSt + " - " + maxSt); // 1.5 s ± 30 %
        Assert.True(minUp >= 1.4 - 1e-9 && maxUp <= 2.0 + 1e-9, "get-up " + minUp + " - " + maxUp); // 2 × [0.7, 1.3], never above GetUpMaxSlowdown
        // a fresh man: no randomness at all
        var calm = Decide(Facts(ImpactEvent.WrongBlock, fresh, fresh, Sword, 4.0), r, dice: dice);
        Assert.Equal(1.0, calm.StunFactor);
        // the stagger's cap
        var capped = Rules(s => s.Set(SettingsSchema.StaggerMaxSeconds, 1.0, "test"));
        var dd = Decide(Facts(ImpactEvent.WrongBlock, fresh, empty, Sword, 4.0), capped, dice: new Dice(0.999, 0.999, 0.0, 0.999, 0.999));
        Assert.True(dd.Stagger.Happens);
        Assert.Equal(1.0, dd.StaggerSeconds, 6);
    }

    // ------------------------------------------------------------------ the anti-abuse state (§3.9)

    [Fact]
    public void A_stagger_gives_immunity_for_2_s_after_it_ends_and_halves_the_next_chance_for_8_s()
    {
        var r = Rules();
        var a = new Man(100, 1, 100);
        var d = new Man(100, 0.1, 100);
        var dState = new ImpactState();
        var aState = new ImpactState();
        var f = Facts(ImpactEvent.RightBlock, a, d, Sword, 4.0);
        // the first block: down 0.999 (no), stagger 0.0 (yes), stun …, stagger length at the roll's centre
        var first = ImpactMath.Decide(in r, in f, ref dState, ref aState, 100, new Dice(0.999, 0.0, 0.5, 0.5));
        Assert.True(first.Stagger.Happens);
        double end = 100 + first.StaggerSeconds;
        Assert.True(dState.Reeling(100.5) && dState.StaggerImmune(end + 1.99) && !dState.StaggerImmune(end + 2.01));
        // inside: immune to a new stagger, and the reeling bonus raises his knockdown chance (halved by the one recent outcome)
        var inside = ImpactMath.Decide(in r, in f, ref dState, ref aState, 100.5, new Dice());
        Assert.Equal(ImpactRefusal.StaggerImmune, inside.Stagger.Refusal);
        Assert.True(inside.Reeling);
        Assert.Equal(1, inside.Knockdown.Halvings);
        Near(inside.Knockdown.Raw * 0.5, inside.Knockdown.Chance, 1e-12);
        // after the immunity: staggerable again, still halved (within 8 s of the outcome)
        var after = ImpactMath.Decide(in r, in f, ref dState, ref aState, end + 2.5, new Dice());
        Assert.Equal(ImpactRefusal.None, after.Stagger.Refusal);
        Assert.False(after.Reeling);
        Assert.Equal(1, after.Stagger.Halvings);
        Near(after.Stagger.Raw * 0.5, after.Stagger.Chance, 1e-12);
        // 8 s on: the full chance again
        var later = ImpactMath.Decide(in r, in f, ref dState, ref aState, 108.01, new Dice());
        Assert.Equal(0, later.Stagger.Halvings);
        Near(later.Stagger.Raw, later.Stagger.Chance, 1e-12);
    }

    [Fact]
    public void A_knockdown_keeps_him_down_then_immune_for_5_s_after_getting_up_and_replaces_the_stagger()
    {
        var r = Rules();
        var a = new Man(100, 1, 100);
        var d = new Man(100, 0, 100);
        var dState = new ImpactState();
        var aState = new ImpactState();
        var f = Facts(ImpactEvent.WrongBlock, a, d, Sword, 4.0);
        var first = ImpactMath.Decide(in r, in f, ref dState, ref aState, 100, new Dice(0.0, 0.0, 0.5, 0.5)); // down yes, shield yes
        Assert.True(first.Knockdown.Happens && first.ShieldDrop.Happens);
        Assert.Equal(ImpactRefusal.Replaced, first.Stagger.Refusal); // the knockdown replaces the stagger
        Assert.True(double.IsNaN(first.StaggerSeconds));
        Near(2.0, first.GetUpFactor, 1e-9); // an empty man at the roll's centre: x2
        double up = 100 + ImpactMath.VanillaFallRiseSeconds * first.GetUpFactor;
        // on the ground: nothing more
        var onGround = ImpactMath.Decide(in r, in f, ref dState, ref aState, 101, new Dice());
        Assert.Equal(ImpactRefusal.Down, onGround.Knockdown.Refusal);
        Assert.Equal(ImpactRefusal.Down, onGround.ShieldDrop.Refusal);
        Assert.Equal(ImpactRefusal.Down, onGround.Stagger.Refusal);
        // up again: immune to a knockdown for 5 s, not to a stagger; two outcomes = two halvings
        var justUp = ImpactMath.Decide(in r, in f, ref dState, ref aState, up + 4.9, new Dice());
        Assert.Equal(ImpactRefusal.KnockdownImmune, justUp.Knockdown.Refusal);
        Assert.Equal(ImpactRefusal.None, justUp.Stagger.Refusal);
        Assert.Equal(2, justUp.Stagger.Halvings);
        Near(justUp.Stagger.Raw * 0.25, justUp.Stagger.Chance, 1e-12);
        var later = ImpactMath.Decide(in r, in f, ref dState, ref aState, up + 5.1, new Dice());
        Assert.Equal(ImpactRefusal.None, later.Knockdown.Refusal);
        // steps 49-51 set the measured moment he is up
        dState.GotUp(200, r.KnockdownImmunity);
        Assert.True(!dState.Down(200) && dState.KnockdownImmune(204.9) && !dState.KnockdownImmune(205.1));
    }

    [Fact]
    public void A_kick_on_a_tired_man_lengthens_his_reel_which_counts_as_reeling_a_fresh_mans_does_not()
    {
        var r = Rules();
        var a = new Man(100, 1, 100);
        var tired = new ImpactState();
        var aState = new ImpactState();
        var kick = ImpactMath.Decide(in r, Facts(ImpactEvent.Kick, a, new Man(100, 0.4, 100)), ref tired, ref aState, 100, new Dice());
        Assert.True(kick.ReelFactor > 1);
        Assert.True(tired.Reeling(100.1)); // the next blow on him gets the bonus - a kick-chain on a TIRED man
        var fresh = new ImpactState();
        var k2 = ImpactMath.Decide(in r, Facts(ImpactEvent.Kick, a, a), ref fresh, ref aState, 100, new Dice());
        Assert.Equal(1.0, k2.ReelFactor);
        Assert.False(fresh.Reeling(100.1)); // a fresh man's vanilla knock-back is no impact stagger - no chain on the fresh
    }

    [Fact]
    public void The_limits_follow_their_settings_window_0_and_factor_1_switch_the_halving_off()
    {
        var a = new Man(100, 1, 100);
        var f = Facts(ImpactEvent.RightBlock, a, new Man(100, 0.1, 100), Sword, 4.0);
        foreach (var tune in new Action<TraxSettings>[]
                 {
                     s => s.Set(SettingsSchema.ImpactDiminishWindowSeconds, 0, "test"),
                     s => s.Set(SettingsSchema.ImpactDiminishFactor, 1, "test"),
                 })
        {
            var r = Rules(tune);
            var st = new ImpactState();
            var at = new ImpactState();
            ImpactMath.Decide(in r, in f, ref st, ref at, 100, new Dice(0.999, 0.0)); // a stagger
            var next = ImpactMath.Decide(in r, in f, ref st, ref at, 104, new Dice());
            Near(next.Stagger.Raw, next.Stagger.Chance, 1e-12);
        }
        // remembered outcomes are capped (six), the oldest forgotten
        var many = new ImpactState();
        for (int i = 0; i < 9; i++) many.NoteOutcome(100 + 0.1 * i);
        Assert.Equal(ImpactMath.MaxRemembered, many.Recent(101, 8));
        Assert.Equal(0, many.Recent(120, 8));
    }

    // ------------------------------------------------------------------ who

    [Fact]
    public void Against_the_player_every_chance_is_ImpactOnPlayerPercent_of_the_AIs()
    {
        var f = Facts(ImpactEvent.Kick, new Man(100, 1, 100), new Man(100, 0, 100));
        f.DefenderIsPlayer = true;
        Pct(80, Decide(in f).Knockdown.Chance, "100 %");
        Pct(40, Decide(in f, Rules(s => s.Set(SettingsSchema.ImpactOnPlayerPercent, 50, "test"))).Knockdown.Chance, "50 %");
        var none = Decide(in f, Rules(s => s.Set(SettingsSchema.ImpactOnPlayerPercent, 0, "test")));
        Assert.Equal(0, none.Knockdown.Chance);
        Assert.Equal(0, none.ShieldDrop.Chance);
        Assert.False(none.Knockdown.Rolled);
        // the player as the ATTACKER is not protected by it
        var mine = Facts(ImpactEvent.Kick, new Man(100, 1, 100), new Man(100, 0, 100));
        mine.AttackerIsPlayer = true;
        Pct(80, Decide(in mine, Rules(s => s.Set(SettingsSchema.ImpactOnPlayerPercent, 0, "test"))).Knockdown.Chance, "your kick");
    }

    [Fact]
    public void A_rider_is_never_staggered_and_a_knockdown_knocks_him_off_at_half_the_chance()
    {
        var f = Facts(ImpactEvent.WrongBlock, new Man(100, 1, 100), new Man(100, 0, 100), Sword, 4.0);
        f.DefenderMounted = true;
        var d = Decide(in f, dice: new Dice(0.0));
        Pct(20, d.Knockdown.Chance, "knocked off (50 % of 40 %)");
        Assert.True(d.Knockdown.Happens && d.Dismount);
        Assert.Equal(ImpactRefusal.Replaced, d.Stagger.Refusal);
        var d2 = Decide(in f, dice: new Dice(0.999));
        Assert.False(d2.Dismount);
        Assert.Equal(ImpactRefusal.Rider, d2.Stagger.Refusal);
    }

    [Fact]
    public void A_shield_drop_needs_a_shield_in_his_hand()
    {
        var f = Facts(ImpactEvent.Kick, new Man(100, 1, 100), new Man(100, 0, 100), shield: false);
        var d = Decide(in f, dice: new Dice(0.999, 0.0));
        Assert.Equal(ImpactRefusal.NoShield, d.ShieldDrop.Refusal);
        Assert.False(d.ShieldDrop.Happens);
    }

    [Fact]
    public void The_damage_term_is_0_by_default_and_adds_its_share_of_full_health_when_set()
    {
        var f = Facts(ImpactEvent.CleanHit, new Man(100, 1, 100), new Man(100, 0.5, 100));
        f.Damage = 30;
        f.DefenderMaxHealth = 100;
        var off = Decide(in f);
        Assert.Equal(0, off.DamagePart);
        var on = Decide(in f, Rules(s => s.Set(SettingsSchema.ImpactDamageWeight, 1.0, "test")));
        Near(0.3, on.DamagePart, 1e-6);
        Near(off.X + 0.3, on.X, 1e-6);
        // never on a block (the shield took it)
        var blk = Facts(ImpactEvent.RightBlock, new Man(100, 1, 100), new Man(100, 0.5, 100), Sword, 4.0);
        blk.Damage = 30;
        Assert.Equal(0, Decide(in blk, Rules(s => s.Set(SettingsSchema.ImpactDamageWeight, 1.0, "test"))).DamagePart);
    }

    [Fact]
    public void The_dice_decide_knockdown_then_shield_then_stagger_one_die_each()
    {
        var f = Facts(ImpactEvent.WrongBlock, new Man(100, 1, 100), new Man(100, 0.25, 100), Sword, 4.0);
        var dice = new Dice(0.39, 0.47, 0.87);
        var d = Decide(in f, dice: dice);
        Assert.Equal(0.39, d.Knockdown.Die);
        Assert.False(d.Knockdown.Happens); // 0.39 > 0.380
        Assert.Equal(0.47, d.ShieldDrop.Die);
        Assert.True(d.ShieldDrop.Happens); // 0.47 < 0.484
        Assert.Equal(0.87, d.Stagger.Die);
        Assert.True(d.Stagger.Happens); // 0.87 < 0.881
        var none = Decide(in f, dice: new Dice(0.999, 0.999, 0.999));
        Assert.False(none.AnyOutcome);
    }

    // ------------------------------------------------------------------ the summary and the line

    [Fact]
    public void The_summary_reads_by_event_and_bar_band_with_would_in_step_48()
    {
        var stats = new ImpactStats();
        var r = Rules();
        var a = new Man(100, 1, 100);
        var dice = new Dice(0.0, 0.0);
        var st = new ImpactState();
        var at = new ImpactState();
        var kick = ImpactMath.Decide(in r, Facts(ImpactEvent.Kick, a, new Man(100, 0.1, 100)), ref st, ref at, 100, dice);
        stats.Note(in kick, shadow: false, applied: false, 0.002, 0);
        var st2 = new ImpactState();
        var block = ImpactMath.Decide(in r, Facts(ImpactEvent.RightBlock, a, new Man(100, 0.6, 100), Sword, 4.0), ref st2, ref at, 100, new Dice());
        stats.Note(in block, shadow: false, applied: false, 0.001, 0);
        var again = ImpactMath.Decide(in r, Facts(ImpactEvent.Kick, a, new Man(100, 0.1, 100)), ref st, ref at, 101, new Dice());
        stats.Note(in again, shadow: false, applied: false, 0.001, 0);
        stats.Skip(ImpactSkip.FamilyOff, ImpactEvent.CleanHit);
        stats.EverOn = true;
        var lines = stats.SummaryLines(in r);
        Assert.StartsWith("impact (step 48 - the model; LIVE, but nothing applied this mission (the built families: kicks / bashes, blocks, clean hits, the riposte) - decided and logged only): 3 decisions - kicks 2, shield bashes 0, right-side blocks 1", lines[0]);
        Assert.Contains("its family switched off 1", lines[0]);
        var kicks = lines.Single(l => l.StartsWith("impact kicks by the kicked man's bar 100-75% / 75-50% / 50-25% / 25-0%: 2 = 0 / 0 / 0 / 2"));
        Assert.Contains("would knock down 0 / 0 / 0 / 1 = 1", kicks);
        Assert.Contains("would drop a shield 0 / 0 / 0 / 1 = 1", kicks);
        Assert.Single(lines, l => l.StartsWith("impact right-side blocks by the blocker's bar"));
        Assert.Contains(lines, l => l.StartsWith("impact riposte (a right-side block or parry staggers the ATTACKER"));
        var limits = lines.Single(l => l.StartsWith("impact limits: refused - on the ground 2"));
        Assert.Contains("stagger immunity", limits);
        Assert.Contains(lines, l => l.StartsWith("impact lengths asked: stagger none, a kicked man's reel"));
        Assert.Contains(lines, l => l.StartsWith("impact cost: 3 decisions, 1.3 µs a decision (max 2.0 µs)"));
        // step 50: an APPLIED kick reads "did" on the kicks' line, the blocks' line still "would"
        var live = new ImpactStats();
        live.Note(in kick, shadow: false, applied: true, 0, 0);
        live.Note(in block, shadow: false, applied: false, 0, 0);
        var liveLines = live.SummaryLines(in r);
        Assert.StartsWith("impact (step 48 - the model; LIVE - kicks / bashes, blocks, clean hits, the riposte applied (1 decisions)", liveLines[0]);
        Assert.Contains("did knock down 0 / 0 / 0 / 1 = 1", liveLines.Single(l => l.StartsWith("impact kicks by")));
        Assert.Contains("would stagger", liveLines.Single(l => l.StartsWith("impact right-side blocks by")));
        var shadow = new ImpactStats();
        shadow.Note(in kick, shadow: true, applied: false, 0, 0);
        Assert.StartsWith("impact (step 48 - the model; SHADOW - decided and logged, nothing applied)", shadow.SummaryLines(in r)[0]);
        var never = new ImpactStats();
        never.Skip(ImpactSkip.Off, ImpactEvent.Kick);
        var off = Rules(s => s.Set(SettingsSchema.ImpactEnabled, false, "test"));
        Assert.Equal("impact (step 48 - the model): OFF all mission (ImpactEnabled) - nothing decided (1 melee events while off)", never.SummaryLines(in off).Single());
    }

    [Fact]
    public void One_decision_in_full_reads_inputs_x_chance_roll_outcome()
    {
        var king = new Man(200, 1.0, 250);
        var man = new Man(50, 0.2, 60);
        var f = Facts(ImpactEvent.Kick, king, man);
        var d = Decide(in f, dice: new Dice(0.41, 0.95, 0.5));
        string s = ImpactMath.Describe(in d, in f, "Rhagaea", "Aldric", "live, decided and logged only");
        Assert.StartsWith("Rhagaea's kick on Aldric (live, decided and logged only): Aldric bar 20% (T 0.73); points 200 vs 10", s);
        foreach (var part in new[] { "impact 1.20 (a kick) vs guard 1.00 (no guard)", "skill 250 vs 60", "→ x 1.33:", "knocked down 80% → rolled 0.41 YES", "shield dropped 90% → rolled 0.95 no" })
            Assert.Contains(part, s);
        var blk = Facts(ImpactEvent.RightBlock, man, king, Sword, 3.5);
        var bd = Decide(in blk);
        string b = ImpactMath.Describe(in bd, in blk, "Aldric", "Rhagaea", "shadow");
        Assert.StartsWith("Rhagaea's right-side block of Aldric's blow (shadow)", b);
        Assert.Contains("(shield, right side, 3.5 kg)", b);
        Assert.Contains("riposte (x_r", b);
        Assert.Contains("block stun x1.00", b);
    }
}

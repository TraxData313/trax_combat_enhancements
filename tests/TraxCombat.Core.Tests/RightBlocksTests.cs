using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 56 - RIGHT BLOCKS FAVOUR THE DEFENDER (Anton's tournament 2026-10-18: "make sure right blocks are a possible penalty for the
/// ATTACKER - now I felt them as a penalty for the defender"; AI_NOTES "Step 56", IMPACT §3.5 / §3.6, DESIGN decision 48). A CORRECT block
/// (right-side shield, parry) counts the blocker's tiredness only <c>CorrectBlockTiredWeightPercent</c> (25) of itself, its ceilings are 30 /
/// 10 (defaults.json), and the riposte - the attacker reels - is × (1 − <c>RiposteBlockerTiredPercent</c> (0) × T_D) up to 70 %; a WRONG-side
/// block keeps 90 / 50 / 40 and T in full. The tests run on defaults.json's step-56 values (<see cref="Shipped56"/> - DESIGN's Default column
/// keeps the initial 80 / 40 / 50 / 100), every man pool 100 / skill 100, a 1.2 kg sword at 92 swung at the game's tired animation share
/// (0.85 + 0.15 f), a 4 kg shield, a 1.2 kg parrying sword.
/// </summary>
public class RightBlocksTests
{
    private sealed class Dice : IRandomSource
    {
        public double NextDouble() => 0.999;
    }

    /// <summary>defaults.json's step-56 values over DESIGN's initial ones.</summary>
    private static void Shipped56(TraxSettings s)
    {
        s.Set(SettingsSchema.RightBlockStaggerMaxPercent, 30, "test");
        s.Set(SettingsSchema.RightBlockKnockdownMaxPercent, 10, "test");
        s.Set(SettingsSchema.ParryStaggerMaxPercent, 30, "test");
        s.Set(SettingsSchema.ParryKnockdownMaxPercent, 10, "test");
        s.Set(SettingsSchema.RiposteStaggerMaxPercent, 70, "test");
        s.Set(SettingsSchema.BlockStunTiredExtraPercent, 0, "test");
    }

    private static ImpactRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        Shipped56(s);
        tune?.Invoke(s);
        return ImpactRules.From(s);
    }

    private static ImpactFacts Block(ImpactEvent e, double defenderBar, double attackerBar, double attackerPool = 100, double defenderPool = 100,
        double attackerSkill = 100, double defenderSkill = 100, double kg = 1.2, double speed = 92, ImpactDamageType type = ImpactDamageType.Cut,
        double guardKg = double.NaN)
    {
        double fA = Math.Min(1, attackerBar / 0.75);
        return new ImpactFacts
        {
            Event = e,
            AttackerPoints = attackerPool * attackerBar,
            AttackerF = fA,
            AttackerAnimation = 0.85 + 0.15 * fA,
            AttackerSkill = attackerSkill,
            DefenderPoints = defenderPool * defenderBar,
            DefenderBar = defenderBar,
            DefenderF = Math.Min(1, defenderBar / 0.75),
            DefenderSkill = defenderSkill,
            WeaponWeight = kg,
            SwingSpeed = speed,
            DamageType = type,
            GuardWeight = !double.IsNaN(guardKg) ? guardKg : e == ImpactEvent.Parry ? 1.2 : 4.0,
            DefenderHasShield = e != ImpactEvent.Parry,
            DefenderMaxHealth = 100,
        };
    }

    private static ImpactDecision Decide(in ImpactFacts f, ImpactRules? rules = null)
    {
        var r = rules ?? Rules();
        var d = default(ImpactState);
        var a = default(ImpactState);
        return ImpactMath.Decide(in r, in f, ref d, ref a, 100, new Dice());
    }

    private static string P(double c) => (100 * c).ToString("0.0") + "%";

    // ------------------------------------------------------------------ the settings

    [Fact]
    public void The_two_new_settings_sit_after_the_riposte_ceiling_live_and_read_by_the_rules()
    {
        var keys = SettingsSchema.InGroup(SettingsSchema.ImpactGroup).Select(p => p.Key).ToList();
        int i = keys.IndexOf("RiposteStaggerMaxPercent");
        Assert.Equal("CorrectBlockTiredWeightPercent", keys[i + 1]);
        Assert.Equal("RiposteBlockerTiredPercent", keys[i + 2]);
        Assert.Equal(ApplyTiming.Live, SettingsSchema.CorrectBlockTiredWeightPercent.Timing);
        Assert.Equal(ApplyTiming.Live, SettingsSchema.RiposteBlockerTiredPercent.Timing);
        var s = new TraxSettings();
        Assert.Equal(25, s.CorrectBlockTiredWeightPercent);
        Assert.Equal(0, s.RiposteBlockerTiredPercent);
        var r = ImpactRules.From(s);
        Assert.Equal(0.25, r.CorrectBlockTiredWeight, 9);
        Assert.Equal(0.0, r.RiposteBlockerTired, 9);
        Assert.Contains("a correct block counts 25% of the blocker's tiredness", r.Describe());
        Assert.True(ImpactMath.IsCorrectBlock(ImpactEvent.RightBlock) && ImpactMath.IsCorrectBlock(ImpactEvent.Parry));
        Assert.False(ImpactMath.IsCorrectBlock(ImpactEvent.WrongBlock) || ImpactMath.IsCorrectBlock(ImpactEvent.Kick) || ImpactMath.IsCorrectBlock(ImpactEvent.CleanHit));
    }

    // ------------------------------------------------------------------ Anton's case: two empty even men

    [Fact]
    public void Two_EMPTY_even_men_a_correct_block_reels_the_ATTACKER_a_wrong_one_breaks_the_blocker()
    {
        var right = Decide(Block(ImpactEvent.RightBlock, 0, 0));
        var parry = Decide(Block(ImpactEvent.Parry, 0, 0));
        var wrong = Decide(Block(ImpactEvent.WrongBlock, 0, 0));
        // the blocker: his tiredness counts a quarter, the bodies are even - x 0.25 at most, ≤ ~5 % staggered, never down
        foreach (var d in new[] { right, parry })
        {
            Assert.Equal(1.0, d.T, 9);
            Assert.Equal(0.25, d.TiredWeight, 9);
            Assert.True(d.X <= 0.2501, d.Event + ": x " + d.X);
            Assert.True(d.Stagger.Chance <= 0.05, d.Event + " stagger " + P(d.Stagger.Chance));
            Assert.Equal(0, d.Knockdown.Chance);
            // the attacker: x_r = his T 1 → the riposte at its 70 % ceiling, the blocker's own T no excuse
            Assert.Equal(1.0, d.RiposteBlockerFactor, 9);
            Assert.True(d.Riposte.Chance >= 0.69, d.Event + " riposte " + P(d.Riposte.Chance));
            Assert.Equal(1.0, d.StunFactor, 9); // BlockStunTiredExtraPercent 0: the game's stun
        }
        Assert.True(right.Stagger.Chance < 0.02, "a 4 kg shield is a real guard: " + P(right.Stagger.Chance));
        // the WRONG side: T in full, the ceilings 90 / 50 / 40 - his own risk, no riposte
        Assert.Equal(1.0, wrong.TiredWeight, 9);
        Assert.Equal(1.0, wrong.X, 9);
        Assert.Equal(0.9, wrong.Stagger.Chance, 6);
        Assert.Equal(0.5, wrong.ShieldDrop.Chance, 6);
        Assert.Equal(0.4, wrong.Knockdown.Chance, 6);
        Assert.False(wrong.Riposte.InRow);
    }

    [Fact]
    public void Two_FRESH_even_men_nothing_either_way()
    {
        foreach (var e in new[] { ImpactEvent.RightBlock, ImpactEvent.Parry })
        {
            var d = Decide(Block(e, 1, 1));
            Assert.Equal(0, d.Stagger.Chance);
            Assert.Equal(0, d.Knockdown.Chance);
            Assert.True(d.Riposte.Chance <= 0.015, e + " riposte " + P(d.Riposte.Chance)); // T_A 0, the 4 kg shield just past the band: ~1 %
        }
    }

    [Theory]
    //          the attacker's bar → the riposte off a FRESH guard (right-side block) at least / at most
    [InlineData(1.00, 0.00, 0.015)]
    [InlineData(0.50, 0.38, 0.44)]   // T_A 0.33 + his smaller body: ~41 %
    [InlineData(0.25, 0.69, 0.70)]   // T_A 0.67 + the body: at the ceiling
    [InlineData(0.00, 0.69, 0.70)]
    public void A_tired_attacker_flailing_into_a_good_guard_opens_himself_up(double attackerBar, double min, double max)
    {
        var d = Decide(Block(ImpactEvent.RightBlock, 1, attackerBar));
        Assert.InRange(d.Riposte.Chance, min, max);
        Assert.Equal(0, d.Stagger.Chance); // the fresh blocker: nothing
    }

    [Fact]
    public void Two_EQUALLY_tired_men_the_attacker_pays_more_the_more_tired_both_are()
    {
        double last = -1;
        foreach (var bar in new[] { 0.5, 0.25, 0.0 })
        {
            var right = Decide(Block(ImpactEvent.RightBlock, bar, bar));
            var parry = Decide(Block(ImpactEvent.Parry, bar, bar));
            Assert.True(right.Riposte.Chance > last, "riposte at " + bar);
            last = right.Riposte.Chance;
            Assert.True(right.Riposte.Chance > 10 * Math.Max(0.01, right.Stagger.Chance), "the attacker's risk dominates at " + bar);
            Assert.True(parry.Stagger.Chance <= 0.05 && parry.Knockdown.Chance == 0, "parry at " + bar);
        }
    }

    [Fact]
    public void A_fresh_attacker_into_an_EXHAUSTED_correct_guard_hardly_breaks_it_into_a_wrong_one_does()
    {
        var right = Decide(Block(ImpactEvent.RightBlock, 0, 1));
        var parry = Decide(Block(ImpactEvent.Parry, 0, 1));
        var wrong = Decide(Block(ImpactEvent.WrongBlock, 0, 1));
        Assert.True(right.Stagger.Chance <= 0.10 && right.Knockdown.Chance <= 0.01, "right " + P(right.Stagger.Chance) + " / " + P(right.Knockdown.Chance));
        Assert.True(parry.Stagger.Chance <= 0.25 && parry.Knockdown.Chance <= 0.05, "parry " + P(parry.Stagger.Chance) + " / " + P(parry.Knockdown.Chance));
        Assert.Equal(0, right.Riposte.Chance); // a fresh attacker never reels
        Assert.True(wrong.Stagger.Chance >= 0.89 && wrong.Knockdown.Chance >= 0.39);
    }

    // ------------------------------------------------------------------ IMPACT §4 C's spirit: the stick against the axe

    [Fact]
    public void The_peasants_stick_against_the_berserkers_two_handed_axe_still_staggers()
    {
        // berserker pool 180, skill 200, a 3 kg two-handed axe at 80; peasant pool 50, skill 20, a 0.6 kg stick
        ImpactFacts Axe(double peasantBar) => Block(ImpactEvent.Parry, peasantBar, 1, attackerPool: 180, defenderPool: 50, attackerSkill: 200, defenderSkill: 20,
            kg: 3.0, speed: 80, guardKg: 0.6);
        var fresh = Decide(Axe(1));
        var half = Decide(Axe(0.5));
        var empty = Decide(Axe(0));
        Assert.InRange(fresh.Stagger.Chance, 0.15, 0.20);   // 17 % (step 48: 46 %)
        Assert.InRange(fresh.Knockdown.Chance, 0.01, 0.04); // 2 %
        Assert.InRange(half.Stagger.Chance, 0.24, 0.28);    // 26 %
        Assert.InRange(half.Knockdown.Chance, 0.05, 0.09);  // 7 %
        Assert.True(empty.Stagger.Chance >= 0.29 && empty.Knockdown.Chance >= 0.09); // the ceilings 30 / 10
        Assert.True(fresh.MismatchPart > 0.5, "the mismatch carries it: " + fresh.MismatchPart);
        // the stick on the fresh berserker's shield: nothing on him, the berserker's guard turns the stick (the peasant reels ~30 %)
        var stick = Decide(Block(ImpactEvent.RightBlock, 1, 1, attackerPool: 50, defenderPool: 180, attackerSkill: 20, defenderSkill: 200,
            kg: 0.6, speed: 95, type: ImpactDamageType.Blunt));
        Assert.Equal(0, stick.Stagger.Chance);
        Assert.InRange(stick.Riposte.Chance, 0.25, 0.35);
    }

    // ------------------------------------------------------------------ the levers: 100 / 100 = step 48 exactly

    [Fact]
    public void Weight_100_and_blocker_share_100_give_step_48s_model_back()
    {
        var r = Rules(s =>
        {
            s.Set(SettingsSchema.CorrectBlockTiredWeightPercent, 100, "test");
            s.Set(SettingsSchema.RiposteBlockerTiredPercent, 100, "test");
        });
        foreach (var (dBar, aBar) in new[] { (0.0, 0.0), (0.25, 1.0), (0.5, 0.25), (1.0, 0.4) })
        {
            var f = Block(ImpactEvent.RightBlock, dBar, aBar);
            var d = Decide(in f, r);
            Assert.Equal(1.0, d.TiredWeight, 9);
            Assert.Equal(d.T + d.MismatchPart, d.X, 9);
            double fD = Math.Min(1, dBar / 0.75);
            Assert.Equal(fD, d.RiposteBlockerFactor, 9);
            Assert.Equal(0.7 * ImpactMath.Curve(d.XRiposte, 0) * fD, d.Riposte.Chance, 9);
        }
        // and a weight of 0: the mismatch alone - two empty even men at x 0
        var none = Decide(Block(ImpactEvent.Parry, 0, 0), Rules(s => s.Set(SettingsSchema.CorrectBlockTiredWeightPercent, 0, "test")));
        Assert.Equal(0, none.X, 9);
        Assert.Equal(0, none.Stagger.Chance);
    }

    [Fact]
    public void The_decision_line_says_how_much_of_the_blockers_T_counted()
    {
        var f = Block(ImpactEvent.RightBlock, 0, 0);
        var d = Decide(in f);
        string line = ImpactMath.Describe(in d, in f, "A", "D", "live");
        Assert.Contains("(T 1.00, counted x0.25 on a correct block)", line);
        var w = Block(ImpactEvent.WrongBlock, 0, 0);
        var dw = Decide(in w);
        Assert.DoesNotContain("counted", ImpactMath.Describe(in dw, in w, "A", "D", "live"));
    }

    // ------------------------------------------------------------------ the migration (format 10)

    /// <summary>defaults.json's step-56 values - the tests run on DESIGN's initial 80 / 40 / 50 / 100, where nothing moves.</summary>
    private static double Shipped(ParamDef p) =>
        p == SettingsSchema.RightBlockStaggerMaxPercent || p == SettingsSchema.ParryStaggerMaxPercent ? 30
        : p == SettingsSchema.RightBlockKnockdownMaxPercent || p == SettingsSchema.ParryKnockdownMaxPercent ? 10
        : p == SettingsSchema.RiposteStaggerMaxPercent ? 70
        : p == SettingsSchema.BlockStunTiredExtraPercent ? 0
        : p == SettingsSchema.CostPerBlow ? 7 : p == SettingsSchema.CostPerWrongSideShieldBlock ? 2
        : p.Default;

    [Fact]
    public void A_format_9_file_at_the_old_block_defaults_gets_the_new_ones_once_tuned_values_kept_and_said()
    {
        Assert.Equal(24, ConfigFile.FormatVersion);
        var read = ConfigFile.Read("{ \"ConfigVersion\": 9, \"RightBlockStaggerMaxPercent\": 80, \"RightBlockKnockdownMaxPercent\": 40, \"ParryStaggerMaxPercent\": 80,"
                                   + " \"ParryKnockdownMaxPercent\": 40, \"RiposteStaggerMaxPercent\": 50, \"BlockStunTiredExtraPercent\": 100, \"WrongBlockStaggerMaxPercent\": 90 }");
        var notes = ConfigFile.Migrate(read, Shipped);
        Assert.Equal(6, notes.Count);
        Assert.Contains(notes, n => n.StartsWith("RightBlockStaggerMaxPercent: 80 → 30 (format 9 → 24, the old default; step 56, Anton's tournament", StringComparison.Ordinal));
        Assert.Contains(notes, n => n.StartsWith("RiposteStaggerMaxPercent: 50 → 70 (format 9 → 24, the old default; step 56", StringComparison.Ordinal));
        Assert.Contains(notes, n => n.StartsWith("BlockStunTiredExtraPercent: 100 → 0 (format 9 → 24, the old default; step 56: the impact spike's lever 2 FAILED", StringComparison.Ordinal));
        Assert.Equal(30, read.Values["RightBlockStaggerMaxPercent"], 9);
        Assert.Equal(10, read.Values["RightBlockKnockdownMaxPercent"], 9);
        Assert.Equal(30, read.Values["ParryStaggerMaxPercent"], 9);
        Assert.Equal(10, read.Values["ParryKnockdownMaxPercent"], 9);
        Assert.Equal(70, read.Values["RiposteStaggerMaxPercent"], 9);
        Assert.Equal(0, read.Values["BlockStunTiredExtraPercent"], 9);
        Assert.Equal(90, read.Values["WrongBlockStaggerMaxPercent"], 9); // the wrong side unchanged
        Assert.Equal(6, ConfigFile.Migrate(read, Shipped).Count);        // idempotent (the notes list is the read's own)
        // his own values stay - and are said once each
        var mine = ConfigFile.Read("{ \"ConfigVersion\": 9, \"RightBlockStaggerMaxPercent\": 60, \"RiposteStaggerMaxPercent\": 30, \"ParryKnockdownMaxPercent\": 10 }");
        var kept = ConfigFile.Migrate(mine, Shipped);
        Assert.Equal(2, kept.Count); // 10 is the new default already: nothing to say
        Assert.StartsWith("RightBlockStaggerMaxPercent: 60 kept - your own value (format 9 → 24; the new default 30 is not pushed over it; step 56", kept[0], StringComparison.Ordinal);
        Assert.StartsWith("RiposteStaggerMaxPercent: 30 kept - your own value (format 9 → 24;", kept[1], StringComparison.Ordinal);
        Assert.Equal(60, mine.Values["RightBlockStaggerMaxPercent"], 9);
        // a format-10 file is never touched; an older file without the keys gets nothing to say
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 10, \"RightBlockStaggerMaxPercent\": 80 }"), Shipped));
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 9, \"CostPerBlow\": 7.0 }"), Shipped));
        // on DESIGN's initial values nothing moves (the default IS the old one)
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 9, \"RightBlockStaggerMaxPercent\": 80 }")));
    }
}

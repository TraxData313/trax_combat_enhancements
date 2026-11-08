using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 100b - TIMED BLOCKS (Toni 2026-11-08: "as I block just as the attack is landing to trigger the stagger up to 2 sec long in a nicely
/// timed block"; AI_NOTES "Step 100", DESIGN decision 91): the score from the guard's age, the guard clock, the shares, the riposte's chance
/// and length blended from step 56's - and, through <see cref="ImpactMath.Decide"/>, a score of 0 reproducing step 56 EXACTLY.
/// </summary>
public class TimedBlockTests
{
    private const double P = 0.15, W = 0.5;

    private sealed class Dice : IRandomSource
    {
        private readonly double _v;
        public Dice(double v = 0.999) => _v = v;
        public double NextDouble() => _v;
    }

    private static TraxSettings Settings(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        // step 56's shipped values (RightBlocksTests.Shipped56) and the timed defaults, pinned so the numbers below never drift with defaults.json
        s.Set(SettingsSchema.RightBlockStaggerMaxPercent, 30, "test");
        s.Set(SettingsSchema.RightBlockKnockdownMaxPercent, 10, "test");
        s.Set(SettingsSchema.ParryStaggerMaxPercent, 30, "test");
        s.Set(SettingsSchema.ParryKnockdownMaxPercent, 10, "test");
        s.Set(SettingsSchema.RiposteStaggerMaxPercent, 70, "test");
        s.Set(SettingsSchema.BlockStunTiredExtraPercent, 0, "test");
        s.Set(SettingsSchema.RiposteStunMaxSeconds, 1.0, "test");
        s.Set(SettingsSchema.ImpactStunCapSeconds, 2.0, "test");
        s.Set(SettingsSchema.ImpactOnPlayerPercent, 100, "test");
        s.Set(SettingsSchema.TimedBlocksEnabled, 1, "test");
        s.Set(SettingsSchema.TimedBlockPerfectSeconds, 0.15, "test");
        s.Set(SettingsSchema.TimedBlockWindowSeconds, 0.5, "test");
        s.Set(SettingsSchema.TimedBlockRiposteMaxPercent, 85, "test");
        s.Set(SettingsSchema.TimedBlockStaggerMaxSeconds, 2.0, "test");
        s.Set(SettingsSchema.TimedBlockAgainstYouMaxSeconds, 1.2, "test");
        s.Set(SettingsSchema.TimedBlockParryPercent, 100, "test");
        s.Set(SettingsSchema.TimedBlockAiPercent, 50, "test");
        tune?.Invoke(s);
        return s;
    }

    private static ImpactRules Rules(Action<TraxSettings>? tune = null) => ImpactRules.From(Settings(tune));

    private static ImpactFacts Block(ImpactEvent e, double defenderBar, double attackerBar, TimingSource src = TimingSource.None, double age = double.NaN,
        bool defenderIsPlayer = true, bool attackerIsPlayer = false)
    {
        double fA = Math.Min(1, attackerBar / 0.75);
        return new ImpactFacts
        {
            Event = e,
            AttackerPoints = 100 * attackerBar,
            AttackerF = fA,
            AttackerAnimation = 0.85 + 0.15 * fA,
            AttackerSkill = 100,
            DefenderPoints = 100 * defenderBar,
            DefenderBar = defenderBar,
            DefenderF = Math.Min(1, defenderBar / 0.75),
            DefenderSkill = 100,
            WeaponWeight = 1.2,
            SwingSpeed = 92,
            DamageType = ImpactDamageType.Cut,
            GuardWeight = e == ImpactEvent.Parry ? 1.2 : 4.0,
            DefenderHasShield = e != ImpactEvent.Parry,
            DefenderMaxHealth = 100,
            DefenderIsPlayer = defenderIsPlayer,
            AttackerIsPlayer = attackerIsPlayer,
            TimingSource = src,
            GuardAge = age,
        };
    }

    private static ImpactDecision Decide(in ImpactFacts f, in ImpactRules r, IRandomSource dice, ref ImpactState defender, ref ImpactState attacker, double now = 100)
        => ImpactMath.Decide(in r, in f, ref defender, ref attacker, now, dice);

    private static ImpactDecision Decide(in ImpactFacts f, in ImpactRules r, double die = 0.999)
    {
        var d = default(ImpactState);
        var a = default(ImpactState);
        return ImpactMath.Decide(in r, in f, ref d, ref a, 100, new Dice(die));
    }

    // ------------------------------------------------------------------ the score

    [Fact]
    public void The_score_is_1_up_to_the_perfect_0_from_the_window_and_falls_smoothly_between()
    {
        Assert.Equal(1, TimedBlockMath.Score(0, P, W));
        Assert.Equal(1, TimedBlockMath.Score(P, P, W));
        Assert.Equal(0, TimedBlockMath.Score(W, P, W));
        Assert.Equal(0, TimedBlockMath.Score(3, P, W));
        Assert.Equal(0.5, TimedBlockMath.Score((P + W) / 2, P, W), 5);
        double prev = 1;
        for (double age = 0; age <= 0.6; age += 0.005)
        {
            double s = TimedBlockMath.Score(age, P, W);
            Assert.InRange(s, 0, 1);
            Assert.True(s <= prev + 1e-12, "not monotone at " + age);
            prev = s;
        }
        // smooth at both ends: the slope is ~0 just inside the window
        Assert.True(1 - TimedBlockMath.Score(P + 0.001, P, W) < 1e-4);
        Assert.True(TimedBlockMath.Score(W - 0.001, P, W) < 1e-4);
    }

    [Fact]
    public void A_bad_age_scores_0_and_a_window_not_above_the_perfect_is_a_step()
    {
        Assert.Equal(0, TimedBlockMath.Score(double.NaN, P, W));
        Assert.Equal(0, TimedBlockMath.Score(-0.01, P, W));
        Assert.Equal(0, TimedBlockMath.Score(double.PositiveInfinity, P, W));
        Assert.Equal(1, TimedBlockMath.Score(0.2, 0.3, 0.3));
        Assert.Equal(0, TimedBlockMath.Score(0.31, 0.3, 0.3));
        Assert.Equal(0, TimedBlockMath.Score(0.31, 0.3, 0.1));
    }

    [Fact]
    public void The_bands_cut_where_the_summary_says()
    {
        Assert.Equal(0, TimedBlockMath.Band(0));
        Assert.Equal(0, TimedBlockMath.Band(double.NaN));
        Assert.Equal(1, TimedBlockMath.Band(0.1));
        Assert.Equal(2, TimedBlockMath.Band(0.25));
        Assert.Equal(3, TimedBlockMath.Band(0.5));
        Assert.Equal(4, TimedBlockMath.Band(0.8));
        Assert.Equal(4, TimedBlockMath.Band(1));
    }

    // ------------------------------------------------------------------ the guard clock

    [Fact]
    public void The_guard_key_reads_the_defend_flag_or_the_defend_stage()
    {
        Assert.Equal(0, TimedBlockMath.GuardKey(0, 0));
        Assert.Equal(0x800, TimedBlockMath.GuardKey(0x800, 0));
        Assert.Equal(0x1000, TimedBlockMath.GuardKey(0x1000 | 0x1, TimedBlockMath.StageDefend));
        Assert.Equal(GuardClock.UpNoDirection, TimedBlockMath.GuardKey(0, TimedBlockMath.StageDefend));
        Assert.Equal(GuardClock.UpNoDirection, TimedBlockMath.GuardKey(0, TimedBlockMath.StageDefendParry));
        Assert.Equal(0, TimedBlockMath.GuardKey(0, 2)); // an attack release - no guard
    }

    [Fact]
    public void The_clock_starts_at_the_press_holds_while_held_and_starts_again_after_a_release()
    {
        var c = default(GuardClock);
        Assert.True(double.IsNaN(c.Age(1)));
        c.Note(0, 1.0, false);
        c.Note(0x800, 2.0, false);
        c.Note(0x800, 2.5, false);
        Assert.Equal(0.7, c.Age(2.7), 5);
        c.Note(0, 3.0, false);
        Assert.True(double.IsNaN(c.Age(3.0)));
        c.Note(0x800, 3.1, false);
        Assert.Equal(0.05, c.Age(3.15), 5);
        Assert.Equal(2, c.Presses);
    }

    [Fact]
    public void A_direction_change_restarts_the_clock_in_a_manual_mode_never_in_the_automatic_one()
    {
        var manual = default(GuardClock);
        manual.Note(0x1000, 1.0, false);
        manual.Note(0x800, 1.6, false);
        Assert.Equal(0.1, manual.Age(1.7), 5);
        Assert.Equal(1, manual.Redirects);

        var auto = default(GuardClock);
        auto.Note(0x1000, 1.0, true);
        auto.Note(0x800, 1.6, true);
        Assert.Equal(0.7, auto.Age(1.7), 5);
        Assert.Equal(0, auto.Redirects);

        // a guard up with no direction named never counts as a switch either way
        var flicker = default(GuardClock);
        flicker.Note(0x1000, 1.0, false);
        flicker.Note(GuardClock.UpNoDirection, 1.2, false);
        flicker.Note(0x1000, 1.4, false);
        Assert.Equal(0.5, flicker.Age(1.5), 5);
    }

    [Fact]
    public void A_defend_action_is_classified_by_its_name()
    {
        Assert.Equal(GuardRead.Active, TimedBlockMath.ClassifyDefendName("act_defend_up_1h_active"));
        Assert.Equal(GuardRead.Active, TimedBlockMath.ClassifyDefendName("act_defend_shield_right_1h_active_down_left_stance"));
        Assert.Equal(GuardRead.Passive, TimedBlockMath.ClassifyDefendName("act_defend_forward_2h_passive"));
        Assert.Equal(GuardRead.ParryReaction, TimedBlockMath.ClassifyDefendName("act_defend_up_1h_parry_heavy"));
        Assert.Equal(GuardRead.ParryReaction, TimedBlockMath.ClassifyDefendName("act_defend_shield_up_spear_parry_light_up"));
        Assert.Equal(GuardRead.OtherDefend, TimedBlockMath.ClassifyDefendName("act_defend_forward_1h"));
        Assert.Equal(GuardRead.NotDefend, TimedBlockMath.ClassifyDefendName("act_stagger_backward"));
        Assert.Equal(GuardRead.NotDefend, TimedBlockMath.ClassifyDefendName(null));
    }

    // ------------------------------------------------------------------ the shares

    [Fact]
    public void The_score_counts_only_a_correct_block_with_a_source_and_the_shares_apply()
    {
        var r = TimedBlockRules.From(Settings());
        Assert.Equal(1, TimedBlockMath.ScoreOf(r, ImpactEvent.RightBlock, TimingSource.PlayerClock, 0.05, true));
        Assert.Equal(0.5, TimedBlockMath.ScoreOf(r, ImpactEvent.RightBlock, TimingSource.AiActive, 0.05, false), 5); // the AI's 50 %
        Assert.Equal(0, TimedBlockMath.ScoreOf(r, ImpactEvent.RightBlock, TimingSource.AiHeld, 0.05, false));
        Assert.Equal(0.5, TimedBlockMath.ScoreOf(r, ImpactEvent.Parry, TimingSource.Chamber, double.NaN, false), 5);
        Assert.Equal(1, TimedBlockMath.ScoreOf(r, ImpactEvent.Parry, TimingSource.Chamber, double.NaN, true));
        Assert.Equal(0, TimedBlockMath.ScoreOf(r, ImpactEvent.RightBlock, TimingSource.None, 0.05, true));
        Assert.Equal(0, TimedBlockMath.ScoreOf(r, ImpactEvent.WrongBlock, TimingSource.PlayerClock, 0.05, true));
        Assert.Equal(0, TimedBlockMath.ScoreOf(r, ImpactEvent.CleanHit, TimingSource.PlayerClock, 0.05, true));

        var half = TimedBlockRules.From(Settings(s => s.Set(SettingsSchema.TimedBlockParryPercent, 40, "test")));
        Assert.Equal(0.4, TimedBlockMath.ScoreOf(half, ImpactEvent.Parry, TimingSource.PlayerClock, 0.05, true), 5);
        Assert.Equal(1, TimedBlockMath.ScoreOf(half, ImpactEvent.RightBlock, TimingSource.PlayerClock, 0.05, true));

        var off = TimedBlockRules.From(Settings(s => s.Set(SettingsSchema.TimedBlocksEnabled, 0, "test")));
        Assert.Equal(0, TimedBlockMath.ScoreOf(off, ImpactEvent.RightBlock, TimingSource.PlayerClock, 0.05, true));
        var master = TimedBlockRules.From(Settings(s => s.Set(SettingsSchema.ModEnabled, 0, "test")));
        Assert.False(master.Enabled);
        Assert.Equal("ModEnabled", master.OffBecause);
    }

    // ------------------------------------------------------------------ the blends

    [Fact]
    public void The_chance_is_step_56s_at_0_the_max_at_1_and_never_below_step_56s()
    {
        foreach (double p56 in new[] { 0, 0.003, 0.2, 0.7, 0.9 })
        {
            Assert.Equal(p56, TimedBlockMath.Chance(p56, 0.85, 0));
            Assert.Equal(p56, TimedBlockMath.Chance(p56, 0.85, -1));
            Assert.Equal(Math.Max(p56, 0.85), TimedBlockMath.Chance(p56, 0.85, 1), 12);
            for (double s = 0; s <= 1; s += 0.1) Assert.True(TimedBlockMath.Chance(p56, 0.85, s) >= p56);
        }
        Assert.Equal(0.01 + 0.84 * 0.5, TimedBlockMath.Chance(0.01, 0.85, 0.5), 12);
    }

    [Fact]
    public void The_length_is_step_56s_at_0_the_max_at_1_capped_by_the_stun_cap_and_on_you()
    {
        Assert.Equal(0.6, TimedBlockMath.Length(0.6, 0, 2.0, 2.0, false, 1.2, out bool c0));
        Assert.False(c0);
        Assert.Equal(2.0, TimedBlockMath.Length(0.6, 1, 2.0, 2.0, false, 1.2, out _), 12);
        Assert.Equal(1.3, TimedBlockMath.Length(0.6, 0.5, 2.0, 2.0, false, 1.2, out _), 12);
        Assert.Equal(1.5, TimedBlockMath.Length(0.6, 1, 2.0, 1.5, false, 1.2, out _), 12); // the stun cap
        Assert.Equal(1.2, TimedBlockMath.Length(0.6, 1, 2.0, 2.0, true, 1.2, out bool cy), 12); // on you
        Assert.True(cy);
        Assert.Equal(1.4, TimedBlockMath.Length(1.4, 1, 2.0, 2.0, true, 1.2, out bool never), 12); // never shorter than step 56's
        Assert.False(never);
        Assert.True(double.IsNaN(TimedBlockMath.Length(double.NaN, 1, 2.0, 2.0, false, 1.2, out _)));
    }

    // ------------------------------------------------------------------ through ImpactMath.Decide

    /// <summary>THE equivalence (the brief): a correct block whose score is 0 - no source, a held guard, a guard raised long ago, the AI's
    /// share at 0, the switch off - decides EXACTLY as step 56: the same chances, the same dice drawn, the same lengths, the same states.</summary>
    [Fact]
    public void A_score_of_0_is_step_56_exactly()
    {
        var bars = new[] { 1.0, 0.75, 0.5, 0.25, 0.05 };
        var events = new[] { ImpactEvent.RightBlock, ImpactEvent.Parry };
        var rOn = Rules();
        var rOff = Rules(s => s.Set(SettingsSchema.TimedBlocksEnabled, 0, "test"));
        var rAi0 = Rules(s => s.Set(SettingsSchema.TimedBlockAiPercent, 0, "test"));
        int compared = 0;
        foreach (var e in events)
        foreach (double db in bars)
        foreach (double ab in bars)
        foreach (bool attackerIsPlayer in new[] { false, true })
        {
            var baseline = Block(e, db, ab, attackerIsPlayer: attackerIsPlayer);
            var variants = new (ImpactFacts F, ImpactRules R)[]
            {
                (Block(e, db, ab, TimingSource.PlayerClock, 2.0, attackerIsPlayer: attackerIsPlayer), rOn), // raised long ago
                (Block(e, db, ab, TimingSource.PlayerClock, W, attackerIsPlayer: attackerIsPlayer), rOn), // at the window's edge
                (Block(e, db, ab, TimingSource.AiHeld, double.PositiveInfinity, false, attackerIsPlayer), rOn), // a held AI guard
                (Block(e, db, ab, TimingSource.AiActive, 0.0, false, attackerIsPlayer), rAi0), // the AI's share 0
                (Block(e, db, ab, TimingSource.PlayerClock, 0.0, attackerIsPlayer: attackerIsPlayer), rOff), // perfect, but the switch off
            };
            for (int seed = 1; seed <= 4; seed++)
            {
                var d0 = default(ImpactState);
                var a0 = default(ImpactState);
                a0.NoteOutcome(99.0); // a recent outcome: the halving must stay identical too
                var dice0 = new SeededRandom(seed);
                var reference = Decide(in baseline, in rOn, dice0, ref d0, ref a0);
                double nextReference = dice0.NextDouble();
                foreach (var (f, r) in variants)
                {
                    var d1 = default(ImpactState);
                    var a1 = default(ImpactState);
                    a1.NoteOutcome(99.0);
                    var dice1 = new SeededRandom(seed);
                    var got = Decide(in f, in r, dice1, ref d1, ref a1);
                    Assert.Equal(0, got.TimedScore);
                    Assert.Equal(reference.Riposte.Chance, got.Riposte.Chance);
                    Assert.Equal(reference.Riposte.Die, got.Riposte.Die);
                    Assert.Equal(reference.Riposte.Happens, got.Riposte.Happens);
                    Assert.Equal(reference.Riposte.Refusal, got.Riposte.Refusal);
                    Assert.Equal(reference.RiposteStunSeconds, got.RiposteStunSeconds);
                    Assert.Equal(reference.Stagger.Chance, got.Stagger.Chance);
                    Assert.Equal(reference.Knockdown.Chance, got.Knockdown.Chance);
                    Assert.Equal(reference.ThrownBack.Chance, got.ThrownBack.Chance);
                    Assert.Equal(nextReference, dice1.NextDouble()); // the same number of dice drawn
                    Assert.Equal(a0.ReelingUntil, a1.ReelingUntil);
                    Assert.Equal(a0.StaggerImmuneUntil, a1.StaggerImmuneUntil);
                    Assert.Equal(d0.ReelingUntil, d1.ReelingUntil);
                    Assert.False(got.TimedCappedForYou);
                    compared++;
                }
            }
        }
        Assert.Equal(2 * 5 * 5 * 2 * 4 * 5, compared);
    }

    [Fact]
    public void A_perfect_block_lifts_a_fresh_even_riposte_to_85_percent_and_2_seconds()
    {
        var r = Rules();
        var untimed = Decide(Block(ImpactEvent.RightBlock, 1, 1), in r, die: 0.5);
        Assert.False(untimed.Riposte.Happens); // step 56: a fresh attacker into a fresh guard almost never reels
        Assert.True(untimed.Riposte.Chance < 0.05);

        var perfect = Decide(Block(ImpactEvent.RightBlock, 1, 1, TimingSource.PlayerClock, 0.05), in r, die: 0.5);
        Assert.Equal(1, perfect.TimedScore);
        Assert.Equal(0.85, perfect.Riposte.Chance, 5);
        Assert.True(perfect.Riposte.Happens);
        Assert.Equal(2.0, perfect.RiposteStunSeconds, 5);
        Assert.Equal(untimed.Riposte.Chance, perfect.RiposteChance56, 12);
        Assert.InRange(perfect.RiposteStun56, 0.4, 1.0);

        // the parry the same (TimedBlockParryPercent 100)
        var parry = Decide(Block(ImpactEvent.Parry, 1, 1, TimingSource.PlayerClock, 0.1), in r, die: 0.5);
        Assert.Equal(0.85, parry.Riposte.Chance, 5);

        // halfway through the window: s 0.5
        var half = Decide(Block(ImpactEvent.RightBlock, 1, 1, TimingSource.PlayerClock, (P + W) / 2), in r, die: 0.5);
        Assert.Equal(0.5, half.TimedScore, 5);
        Assert.Equal(half.RiposteChance56 + (0.85 - half.RiposteChance56) * 0.5, half.Riposte.Chance, 5);
        Assert.Equal(half.RiposteStun56 + (2.0 - half.RiposteStun56) * 0.5, half.RiposteStunSeconds, 5);

        // the result object (the later parts' seam)
        var t = new TimedBlockResult(in perfect);
        Assert.True(t.Timed);
        Assert.Equal(4, t.Band);
        Assert.True(t.RiposteHappens);
        Assert.Equal(2.0, t.Seconds, 5);
    }

    [Fact]
    public void The_ai_times_at_half_strength_and_never_staggers_you_beyond_the_you_cap()
    {
        var r = Rules();
        var ai = Decide(Block(ImpactEvent.RightBlock, 1, 1, TimingSource.AiActive, 0.05, defenderIsPlayer: false, attackerIsPlayer: true), in r, die: 0.3);
        Assert.Equal(0.5, ai.TimedScore, 5);
        Assert.Equal(ai.RiposteChance56 + (0.85 - ai.RiposteChance56) * 0.5, ai.Riposte.Chance, 5);
        Assert.True(ai.Riposte.Happens);
        Assert.True(ai.RiposteStunSeconds <= 1.2 + 1e-9);
        Assert.True(ai.RiposteStunSeconds >= ai.RiposteStun56);

        // ImpactOnPlayerPercent still applies after the blend (on you)
        var gentle = Rules(s => s.Set(SettingsSchema.ImpactOnPlayerPercent, 50, "test"));
        var g = Decide(Block(ImpactEvent.RightBlock, 1, 1, TimingSource.AiActive, 0.05, defenderIsPlayer: false, attackerIsPlayer: true), in gentle, die: 0.3);
        Assert.Equal(ai.Riposte.Chance * 0.5, g.Riposte.Chance, 5);

        // a perfect AI share at 100 on you: held at 1.2 s
        var full = Rules(s => s.Set(SettingsSchema.TimedBlockAiPercent, 100, "test"));
        var f = Decide(Block(ImpactEvent.RightBlock, 1, 1, TimingSource.AiActive, 0.05, defenderIsPlayer: false, attackerIsPlayer: true), in full, die: 0.3);
        Assert.Equal(1.2, f.RiposteStunSeconds, 5);
        Assert.True(f.TimedCappedForYou);
    }

    [Fact]
    public void The_halving_and_the_immunity_still_apply_after_the_blend()
    {
        var r = Rules();
        var f = Block(ImpactEvent.RightBlock, 1, 1, TimingSource.PlayerClock, 0.05);
        var d = default(ImpactState);
        var a = default(ImpactState);
        a.NoteOutcome(99.5);
        var halved = Decide(in f, in r, new Dice(0.999), ref d, ref a);
        Assert.Equal(1, halved.Riposte.Halvings);
        Assert.Equal(0.85 * 0.5, halved.Riposte.Chance, 5);

        var d2 = default(ImpactState);
        var a2 = default(ImpactState);
        a2.Reel(99.0, 5); // immune until 104
        var immune = Decide(in f, in r, new Dice(0.0), ref d2, ref a2);
        Assert.Equal(ImpactRefusal.StaggerImmune, immune.Riposte.Refusal);
        Assert.False(immune.Riposte.Happens);
    }

    [Fact]
    public void A_wrong_side_block_or_a_rider_never_gets_a_timed_riposte()
    {
        var r = Rules();
        var wrong = Decide(Block(ImpactEvent.WrongBlock, 1, 1, TimingSource.PlayerClock, 0.05), in r, die: 0.0);
        Assert.Equal(0, wrong.TimedScore);
        Assert.False(wrong.Riposte.InRow);

        var f = Block(ImpactEvent.RightBlock, 1, 1, TimingSource.PlayerClock, 0.05);
        f.AttackerMounted = true;
        var rider = Decide(in f, in r, die: 0.0);
        Assert.Equal(1, rider.TimedScore); // scored - 100c's seam - but the riposte refused on a rider as before
        Assert.Equal(ImpactRefusal.Rider, rider.Riposte.Refusal);
        Assert.False(rider.Riposte.Happens);
        Assert.True(new TimedBlockResult(in rider).AttackerMounted);
    }

    [Fact]
    public void The_stats_count_by_band_and_write_three_summary_lines()
    {
        var r = Rules();
        var stats = new TimedBlockStats();
        var perfect = Decide(Block(ImpactEvent.RightBlock, 1, 1, TimingSource.PlayerClock, 0.05), in r, die: 0.5);
        stats.Note(new TimedBlockResult(in perfect), in perfect);
        var untimed = Decide(Block(ImpactEvent.Parry, 1, 1), in r, die: 0.5);
        stats.Note(new TimedBlockResult(in untimed), in untimed);
        stats.NoteVerdict(BlockVerdict.Parried, GuardRead.Active, 0.05, true);
        stats.NoteActiveLength(0.4, false, true);
        stats.NoteApplied(1);
        stats.NoteSeen(1);
        stats.NoteMeasured(1, 2.0, 1.95);
        Assert.Equal(1, stats.ByBand[0, 0, 4]);
        Assert.Equal(1, stats.ByBand[1, 0, 0]);
        Assert.Equal(1, stats.RiposteDecided[4]);
        Assert.Equal(0.05, stats.YourMedian(), 5);
        var lines = stats.SummaryLines(TimedBlockRules.From(Settings()));
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("timed blocks (step 100b; ON)", lines[0]);
        Assert.Contains("Parried + active 1 (yours: ≤0.15 s 1)", lines[1]);
        Assert.Contains("2.00→1.95 s", lines[2]);
    }

    [Fact]
    public void The_settings_sit_where_the_design_puts_them()
    {
        var all = SettingsSchema.All.ToList();
        Assert.Equal(all.IndexOf(SettingsSchema.ImpactMeetRulesEnabled) + 1, all.IndexOf(SettingsSchema.TimedBlocksEnabled));
        Assert.Contains(SettingsSchema.TimedBlocksEnabled, SettingsSchema.PlayerPage);
        Assert.DoesNotMatch(@"[Ss]tep \d", SettingsSchema.TimedBlocksEnabled.Description);
        var advanced = new[]
        {
            SettingsSchema.TimedBlockPerfectSeconds, SettingsSchema.TimedBlockWindowSeconds, SettingsSchema.TimedBlockRiposteMaxPercent,
            SettingsSchema.TimedBlockStaggerMaxSeconds, SettingsSchema.TimedBlockAgainstYouMaxSeconds, SettingsSchema.TimedBlockParryPercent,
            SettingsSchema.TimedBlockAiPercent,
        };
        int first = all.IndexOf(SettingsSchema.MeetAiReadMaxHoldSeconds) + 1;
        for (int i = 0; i < advanced.Length; i++)
        {
            Assert.Equal(first + i, all.IndexOf(advanced[i]));
            Assert.Equal(SettingsPage.Advanced, advanced[i].Group.Page);
        }
        Assert.Equal(1, SettingsSchema.TimedBlocksEnabled.Default, 5);
        Assert.Equal(0.15, SettingsSchema.TimedBlockPerfectSeconds.Default, 5);
        Assert.Equal(0.5, SettingsSchema.TimedBlockWindowSeconds.Default, 5);
        Assert.Equal(85, SettingsSchema.TimedBlockRiposteMaxPercent.Default, 5);
        Assert.Equal(2.0, SettingsSchema.TimedBlockStaggerMaxSeconds.Default, 5);
        Assert.Equal(1.2, SettingsSchema.TimedBlockAgainstYouMaxSeconds.Default, 5);
        Assert.Equal(100, SettingsSchema.TimedBlockParryPercent.Default, 5);
        Assert.Equal(50, SettingsSchema.TimedBlockAiPercent.Default, 5);
    }
}

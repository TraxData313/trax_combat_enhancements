using System;
using System.Globalization;
using System.Linq;
using TraxCombat.Core;
using Xunit.Abstractions;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 101 - timed-block tuning after Toni's first playtest (2026-11-08; AI_NOTES "Step 101", DESIGN decision 91 (l) / (m)):
///   - "make the perfect blocks stagger 100% of the time and bump up all probs by 15% in the table": a timed block's riposte chance + 15 points
///     after the blend, at most 100 % - Toni's table for fresh even men; a PERFECT block skips the attacker's recent-stagger halving and his
///     immunity after the last stagger (never while he still reels); score 0 still step 56 exactly;
///   - "giving more time overall for the more skillful": the perfect window by the blocker's weapon skill (0.10 / 0.15 / 0.20 / 0.25 s at
///     0 / 100 / 200 / 300), the no-timing point stretched with it;
///   - the slow motion's config format 28 (TimedBlockSlowMoSpeed 0.3 → 0.25, the whole-ramp key retired).
/// </summary>
public class Step101Tests
{
    private readonly ITestOutputHelper _out;

    public Step101Tests(ITestOutputHelper output) => _out = output;

    private sealed class Dice : IRandomSource
    {
        private readonly double _v;
        public Dice(double v = 0.999) => _v = v;
        public double NextDouble() => _v;
    }

    /// <summary>Step 56's shipped values and the timed defaults (TimedBlockTests'), with step 101's keys at their design values.</summary>
    private static TraxSettings Settings(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
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
        bool defenderIsPlayer = true, bool attackerIsPlayer = false, double skill = double.NaN)
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
            GuardSkill = double.IsNaN(skill) ? 0 : skill,
            HasGuardSkill = !double.IsNaN(skill),
        };
    }

    private static ImpactDecision Decide(in ImpactFacts f, in ImpactRules r, double die = 0.999)
    {
        var d = default(ImpactState);
        var a = default(ImpactState);
        return ImpactMath.Decide(in r, in f, ref d, ref a, 100, new Dice(die));
    }

    private static double ChanceAt(double age, double skill = double.NaN)
    {
        var f = double.IsNaN(age) ? Block(ImpactEvent.RightBlock, 1, 1, skill: skill) : Block(ImpactEvent.RightBlock, 1, 1, TimingSource.PlayerClock, age, skill: skill);
        return Decide(in f, Rules()).Riposte.Chance;
    }

    // ------------------------------------------------------------------ A. the riposte chance

    [Fact]
    public void Tonis_table_for_fresh_even_men()
    {
        var c = CultureInfo.InvariantCulture;
        double held = ChanceAt(double.NaN);
        Assert.InRange(held, 0.005, 0.02); // step 56: ~1 %
        var table = new (double Age, int Percent)[] { (0.15, 100), (0.20, 95), (0.25, 83), (0.30, 67), (0.35, 49), (0.40, 33), (0.45, 21) };
        foreach (var (age, percent) in table)
        {
            double got = ChanceAt(age);
            _out.WriteLine("guard " + age.ToString("0.00", c) + " s -> " + (100 * got).ToString("0.0", c) + " %");
            Assert.Equal(percent, (int)Math.Round(100 * got));
        }
        _out.WriteLine("held -> " + (100 * held).ToString("0.0", c) + " %");
        Assert.Equal(1.0, ChanceAt(0.05), 12); // a perfect block: 100 % exactly
        Assert.Equal(held, ChanceAt(0.5), 12); // the window's edge: step 56
    }

    [Fact]
    public void The_bonus_is_capped_never_below_the_blend_and_score_0_is_untouched()
    {
        Assert.Equal(0.01, TimedBlockMath.TimedChance(0.01, 0.85, 0, 0.15, 1), 12);
        Assert.Equal(0.01, TimedBlockMath.TimedChance(0.01, 0.85, -1, 0.15, 1), 12);
        Assert.Equal(1.0, TimedBlockMath.TimedChance(0.01, 0.85, 1, 0.15, 1), 12);
        Assert.Equal(0.9, TimedBlockMath.TimedChance(0.01, 0.85, 1, 0.15, 0.9), 12); // the cap
        Assert.Equal(0.85, TimedBlockMath.TimedChance(0.01, 0.85, 1, 0.15, 0.5), 12); // a cap below the blend never cuts it
        Assert.Equal(0.01 + 0.84 * 0.5 + 0.15, TimedBlockMath.TimedChance(0.01, 0.85, 0.5, 0.15, 1), 12);
        Assert.Equal(TimedBlockMath.Chance(0.3, 0.85, 0.4), TimedBlockMath.TimedChance(0.3, 0.85, 0.4, 0, 1), 12); // no bonus = step 100b
    }

    /// <summary>The brief: score 0 still decides EXACTLY as step 56 with step 101 on - the same chances, dice, lengths and states.</summary>
    [Fact]
    public void A_score_of_0_is_still_step_56_exactly_with_step_101_on()
    {
        var bars = new[] { 1.0, 0.5, 0.05 };
        var rOn = Rules();
        int compared = 0;
        foreach (var e in new[] { ImpactEvent.RightBlock, ImpactEvent.Parry })
        foreach (double db in bars)
        foreach (double ab in bars)
        foreach (bool recent in new[] { false, true })
        {
            var baseline = Block(e, db, ab);
            var variants = new[]
            {
                Block(e, db, ab, TimingSource.PlayerClock, 2.0),
                Block(e, db, ab, TimingSource.PlayerClock, 2.0, skill: 300), // a skilled man's held guard
                Block(e, db, ab, TimingSource.AiHeld, double.PositiveInfinity, false),
            };
            for (int seed = 1; seed <= 3; seed++)
            {
                var d0 = default(ImpactState);
                var a0 = default(ImpactState);
                if (recent) a0.NoteOutcome(99.0);
                var dice0 = new SeededRandom(seed);
                var reference = ImpactMath.Decide(in rOn, in baseline, ref d0, ref a0, 100, dice0);
                double next = dice0.NextDouble();
                foreach (var f in variants)
                {
                    var d1 = default(ImpactState);
                    var a1 = default(ImpactState);
                    if (recent) a1.NoteOutcome(99.0);
                    var dice1 = new SeededRandom(seed);
                    var got = ImpactMath.Decide(in rOn, in f, ref d1, ref a1, 100, dice1);
                    Assert.Equal(0, got.TimedScore);
                    Assert.Equal(reference.Riposte.Chance, got.Riposte.Chance);
                    Assert.Equal(reference.Riposte.Halvings, got.Riposte.Halvings);
                    Assert.Equal(reference.Riposte.Die, got.Riposte.Die);
                    Assert.Equal(reference.Riposte.Happens, got.Riposte.Happens);
                    Assert.Equal(reference.RiposteStunSeconds, got.RiposteStunSeconds);
                    Assert.Equal(next, dice1.NextDouble());
                    Assert.Equal(a0.ReelingUntil, a1.ReelingUntil);
                    Assert.Equal(a0.StaggerImmuneUntil, a1.StaggerImmuneUntil);
                    Assert.False(got.TimedSkippedHalving || got.TimedSkippedImmunity);
                    compared++;
                }
            }
        }
        Assert.Equal(2 * 3 * 3 * 2 * 3 * 3, compared);
    }

    [Fact]
    public void A_perfect_block_skips_his_recent_staggers_and_the_immunity_after_his_last_one()
    {
        var r = Rules();
        var f = Block(ImpactEvent.RightBlock, 1, 1, TimingSource.PlayerClock, 0.05);

        var d = default(ImpactState);
        var a = default(ImpactState);
        a.NoteOutcome(99.5);
        a.NoteOutcome(99.8);
        var got = ImpactMath.Decide(in r, in f, ref d, ref a, 100, new Dice(0.999));
        Assert.Equal(0, got.Riposte.Halvings);
        Assert.Equal(1.0, got.Riposte.Chance, 12);
        Assert.True(got.Riposte.Happens);
        Assert.True(got.TimedSkippedHalving);

        // his stagger ended at 99.5, immune to 101.5: a perfect block staggers him again
        var d2 = default(ImpactState);
        var a2 = default(ImpactState);
        a2.Reel(99.5, 2);
        var again = ImpactMath.Decide(in r, in f, ref d2, ref a2, 100, new Dice(0.999));
        Assert.Equal(ImpactRefusal.None, again.Riposte.Refusal);
        Assert.True(again.Riposte.Happens);
        Assert.True(again.TimedSkippedImmunity);

        // STILL REELING (until 100.5): refused - a new stagger on top of a running one would tangle the animation
        var d3 = default(ImpactState);
        var a3 = default(ImpactState);
        a3.Reel(100.5, 2);
        var reeling = ImpactMath.Decide(in r, in f, ref d3, ref a3, 100, new Dice(0.0));
        Assert.Equal(ImpactRefusal.StaggerImmune, reeling.Riposte.Refusal);
        Assert.False(reeling.Riposte.Happens);
        Assert.False(reeling.TimedSkippedImmunity);

        // a rider keeps the refused riposte (the mounted part dropped)
        var rider = f;
        rider.AttackerMounted = true;
        var rd = Decide(in rider, in r, 0.0);
        Assert.Equal(ImpactRefusal.Rider, rd.Riposte.Refusal);
        Assert.False(rd.Riposte.Happens);
    }

    [Fact]
    public void A_sharp_but_not_perfect_block_keeps_the_halving_and_the_immunity_and_the_switches_bring_them_back()
    {
        var r = Rules();
        var sharp = Block(ImpactEvent.RightBlock, 1, 1, TimingSource.PlayerClock, 0.2);
        var d = default(ImpactState);
        var a = default(ImpactState);
        a.NoteOutcome(99.5);
        var got = ImpactMath.Decide(in r, in sharp, ref d, ref a, 100, new Dice(0.999));
        Assert.Equal(1, got.Riposte.Halvings);
        Assert.False(got.TimedSkippedHalving);

        var keep = Rules(s =>
        {
            s.Set(SettingsSchema.TimedBlockPerfectSkipsHalving, 0, "test");
            s.Set(SettingsSchema.TimedBlockPerfectSkipsImmunity, 0, "test");
        });
        var perfect = Block(ImpactEvent.RightBlock, 1, 1, TimingSource.PlayerClock, 0.05);
        var d2 = default(ImpactState);
        var a2 = default(ImpactState);
        a2.NoteOutcome(99.5);
        var halved = ImpactMath.Decide(in keep, in perfect, ref d2, ref a2, 100, new Dice(0.999));
        Assert.Equal(0.5, halved.Riposte.Chance, 12);
        var d3 = default(ImpactState);
        var a3 = default(ImpactState);
        a3.Reel(99.5, 2);
        Assert.Equal(ImpactRefusal.StaggerImmune, ImpactMath.Decide(in keep, in perfect, ref d3, ref a3, 100, new Dice(0.0)).Riposte.Refusal);
    }

    [Fact]
    public void The_stats_count_perfect_blocks_and_their_skips()
    {
        var r = Rules();
        var stats = new TimedBlockStats();
        var f = Block(ImpactEvent.RightBlock, 1, 1, TimingSource.PlayerClock, 0.05);
        var d = default(ImpactState);
        var a = default(ImpactState);
        a.NoteOutcome(99.5);
        var got = ImpactMath.Decide(in r, in f, ref d, ref a, 100, new Dice(0.5));
        stats.Note(new TimedBlockResult(in got), in got);
        Assert.Equal(1, stats.PerfectRolls);
        Assert.Equal(1, stats.PerfectRipostes);
        Assert.Equal(1, stats.PerfectSkippedHalving);
        Assert.Contains("perfect blocks 1 rolled → 1 ripostes (the halving skipped 1, the immunity skipped 0)", stats.SummaryLines(TimedBlockRules.From(Settings()))[2]);
    }

    // ------------------------------------------------------------------ C. the window by skill

    [Fact]
    public void The_perfect_window_follows_the_weapon_skill_through_the_four_points_and_holds_beyond()
    {
        var t = TimedBlockRules.From(Settings());
        Assert.True(t.SkillScales);
        Assert.Equal(0.10, TimedBlockMath.PerfectFor(in t, 0), 9);
        Assert.Equal(0.10, TimedBlockMath.PerfectFor(in t, -20), 9);
        Assert.Equal(0.125, TimedBlockMath.PerfectFor(in t, 50), 9);
        Assert.Equal(0.15, TimedBlockMath.PerfectFor(in t, 100), 9);
        Assert.Equal(0.175, TimedBlockMath.PerfectFor(in t, 150), 9);
        Assert.Equal(0.20, TimedBlockMath.PerfectFor(in t, 200), 9);
        Assert.Equal(0.25, TimedBlockMath.PerfectFor(in t, 300), 9);
        Assert.Equal(0.25, TimedBlockMath.PerfectFor(in t, 330), 9); // clamped above 300
        Assert.Equal(0.15, TimedBlockMath.PerfectFor(in t, double.NaN), 6); // unknown = the flat setting
    }

    [Fact]
    public void The_no_timing_point_stretches_with_the_window()
    {
        var t = TimedBlockRules.From(Settings());
        void Check(double skill, double perfect, double window)
        {
            TimedBlockMath.WindowFor(in t, skill, out double p, out double w);
            Assert.Equal(perfect, p, 6);
            Assert.Equal(window, w, 3);
        }
        Check(0, 0.10, 0.333);
        Check(100, 0.15, 0.5);
        Check(200, 0.20, 0.667);
        Check(300, 0.25, 0.833);
        Check(400, 0.25, 0.833);
        // the switch off, or the skill unknown: the flat pair
        var flat = TimedBlockRules.From(Settings(s => s.Set(SettingsSchema.TimedBlockSkillScales, 0, "test")));
        TimedBlockMath.WindowFor(in flat, 300, out double fp, out double fw);
        Assert.Equal(0.15, fp, 6);
        Assert.Equal(0.5, fw, 6);
        TimedBlockMath.WindowFor(in t, double.NaN, out double np, out double nw);
        Assert.Equal(0.15, np, 6);
        Assert.Equal(0.5, nw, 6);
    }

    [Fact]
    public void A_skilled_blocker_gets_more_time_and_a_novice_less_through_the_decision()
    {
        // a 0.2 s guard: a 100-skill man is sharp (95 %), a 300-skill man is perfect (100 %), a 0-skill man is late
        Assert.Equal(0.95, Math.Round(ChanceAt(0.2, 100), 2), 9);
        Assert.Equal(1.0, ChanceAt(0.2, 300), 12);
        Assert.InRange(ChanceAt(0.2, 0), 0.66, 0.68); // his window 0.10 / 0.333: 0.2 s scores like 0.30 s at skill 100
        // a 0.4 s guard: nothing for a novice (his window ends at 0.333), something for a master
        Assert.Equal(ChanceAt(double.NaN), ChanceAt(0.4, 0), 12);
        Assert.True(ChanceAt(0.4, 300) > 0.7);
        // the decision records the window it used
        var d = Decide(Block(ImpactEvent.RightBlock, 1, 1, TimingSource.PlayerClock, 0.2, skill: 200), Rules());
        Assert.Equal(0.20, d.TimedPerfect, 6);
        Assert.Equal(0.667, d.TimedWindow, 3);
        Assert.Equal(1, d.TimedScore, 9);
        Assert.Equal(0.20, new TimedBlockResult(in d).Perfect, 6);
        Assert.Equal("Your riposte - X reels 2.00 s / 2.00 (guard 0.20 s / 0.20)",
            TimedBlockMath.YourBlockLine("X", ImpactEvent.RightBlock, true, 2.0, 2.0, TimingSource.PlayerClock, 0.2, d.TimedPerfect));
    }

    [Fact]
    public void Tonis_skill_table()
    {
        var c = CultureInfo.InvariantCulture;
        var t = TimedBlockRules.From(Settings());
        _out.WriteLine("skill | perfect | no-bonus | 0.1 s | 0.2 s | 0.3 s | 0.5 s");
        foreach (int skill in new[] { 50, 100, 150, 200, 250, 300 })
        {
            TimedBlockMath.WindowFor(in t, skill, out double p, out double w);
            string row = skill + " | " + p.ToString("0.000", c) + " | " + w.ToString("0.000", c);
            foreach (double age in new[] { 0.1, 0.2, 0.3, 0.5 }) row += " | " + (100 * ChanceAt(age, skill)).ToString("0", c) + "%";
            _out.WriteLine(row);
            Assert.Equal(1.0, ChanceAt(0.1, skill), 12); // 0.1 s is inside every window from 0
        }
    }

    // ------------------------------------------------------------------ the settings and config format 28

    private static double Shipped(ParamDef p) => p == SettingsSchema.TimedBlockSlowMoSpeed ? 0.25 : p.Default;

    [Fact]
    public void Format_28_moves_the_old_slow_motion_speed_keeps_a_tuned_one_and_drops_the_retired_key()
    {
        Assert.Equal(30, ConfigFile.FormatVersion);
        var old = ConfigFile.Read("{ \"ConfigVersion\": 27, \"TimedBlockSlowMoSpeed\": 0.3, \"TimedBlockSlowMoRealSeconds\": 0.5 }");
        var notes = ConfigFile.Migrate(old, Shipped);
        Assert.Equal(0.25, old.Values["TimedBlockSlowMoSpeed"], 6);
        Assert.Contains(notes, n => n.StartsWith("TimedBlockSlowMoSpeed: 0.3", StringComparison.Ordinal) && n.Contains("step 101"));
        Assert.Contains(notes, n => n.StartsWith("TimedBlockSlowMoRealSeconds 0.5 dropped", StringComparison.Ordinal));
        Assert.DoesNotContain(old.Unknown, u => u.Key == "TimedBlockSlowMoRealSeconds");

        var tuned = ConfigFile.Read("{ \"ConfigVersion\": 27, \"TimedBlockSlowMoSpeed\": 0.5 }");
        var kept = ConfigFile.Migrate(tuned, Shipped);
        Assert.Equal(0.5, tuned.Values["TimedBlockSlowMoSpeed"], 6);
        Assert.Contains(kept, n => n.StartsWith("TimedBlockSlowMoSpeed: 0.5 kept", StringComparison.Ordinal));

        var mine = ConfigFile.Read("{ \"ConfigVersion\": 28, \"TimedBlockSlowMoSpeed\": 0.3 }");
        ConfigFile.Migrate(mine, Shipped);
        Assert.Equal(0.3, mine.Values["TimedBlockSlowMoSpeed"], 6); // a format-28 file's 0.3 is his own
    }

    [Fact]
    public void The_shipped_defaults_are_tonis()
    {
        string text = DefaultsFile.ReadEmbeddedText();
        foreach (var kv in new[]
        {
            "\"TimedBlockRiposteBonusPoints\": 15,", "\"TimedBlockRiposteCapPercent\": 100,", "\"TimedBlockPerfectSkipsHalving\": true,",
            "\"TimedBlockPerfectSkipsImmunity\": true,", "\"TimedBlockSkillScales\": true,", "\"TimedBlockPerfectAtSkill0\": 0.1,",
            "\"TimedBlockPerfectAtSkill100\": 0.15,", "\"TimedBlockPerfectAtSkill200\": 0.2,", "\"TimedBlockPerfectAtSkill300\": 0.25,",
            "\"TimedBlockSlowMoSpeed\": 0.25,", "\"TimedBlockSlowMoHoldSeconds\": 1.5,", "\"TimedBlockSlowMoEaseOutSeconds\": 0.3,",
            "\"TimedBlockSlowMoCooldownSeconds\": 4.0,", "\"TimedBlockSlowMoMinScore\": 0.8,",
        })
            Assert.Contains(kv, text, StringComparison.Ordinal);
        Assert.DoesNotContain("TimedBlockSlowMoRealSeconds", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_new_settings_sit_on_the_advanced_impact_page_in_order()
    {
        var all = SettingsSchema.All.ToList();
        var keys = new[]
        {
            SettingsSchema.TimedBlockRiposteBonusPoints, SettingsSchema.TimedBlockRiposteCapPercent, SettingsSchema.TimedBlockPerfectSkipsHalving,
            SettingsSchema.TimedBlockPerfectSkipsImmunity, SettingsSchema.TimedBlockSkillScales, SettingsSchema.TimedBlockPerfectAtSkill0,
            SettingsSchema.TimedBlockPerfectAtSkill100, SettingsSchema.TimedBlockPerfectAtSkill200, SettingsSchema.TimedBlockPerfectAtSkill300,
            SettingsSchema.TimedBlockSlowMoMinScore,
        };
        int first = all.IndexOf(SettingsSchema.TimedBlockAiPercent) + 1;
        for (int i = 0; i < keys.Length; i++)
        {
            Assert.Equal(first + i, all.IndexOf(keys[i]));
            Assert.Equal(SettingsPage.Advanced, keys[i].Group.Page);
        }
        Assert.All(new[] { SettingsSchema.TimedBlockSlowMoSpeed, SettingsSchema.TimedBlockSlowMoHoldSeconds, SettingsSchema.TimedBlockSlowMoEaseOutSeconds,
            SettingsSchema.TimedBlockSlowMoCooldownSeconds }, p => Assert.StartsWith("Slow motion:", p.Label, StringComparison.Ordinal));
        Assert.Equal(0.2, SettingsSchema.TimedBlockSlowMoHoldSeconds.Min, 6);
        Assert.Equal(4, SettingsSchema.TimedBlockSlowMoHoldSeconds.Max, 6);
        Assert.Equal(1, SettingsSchema.TimedBlockSlowMoEaseOutSeconds.Max, 6);
        Assert.Equal(0.1, SettingsSchema.TimedBlockSlowMoSpeed.Min, 6);
    }
}

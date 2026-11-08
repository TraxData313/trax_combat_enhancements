using System;
using TraxCombat.Core;
using Xunit.Abstractions;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 102 - THE AI TIMES ITS BLOCKS (Toni 2026-11-08; AI_NOTES "Step 102", DESIGN decision 91 (n)): the AI guard clock - fed with the defend
/// bits of the AI's own input frames (read, never changed) - starts on a rise seen from the frame before; a direction change is not a new guard; a
/// flicker of the bits is one guard; a gap says nothing; at contact a known age scores at the AI's 50 % with the player's skill window.
/// </summary>
public class Step102Tests
{
    private readonly ITestOutputHelper _out;

    public Step102Tests(ITestOutputHelper output) => _out = output;

    private const double Frame = 1.0 / 120; // the callback's rate in Toni's logs (~110-130 a second a man)
    private const uint DefendLeft = 0x400, DefendRight = 0x800, DefendDown = 0x2000, AttackDown = 0x200, Forward = 0x1;

    /// <summary>Frames from <paramref name="t"/> to <paramref name="until"/> with <paramref name="flags"/>; returns the time after the last.</summary>
    private static double Run(ref AiGuardClock c, double t, double until, uint flags)
    {
        for (; t < until - 1e-9; t += Frame) c.Note(flags, t);
        return t;
    }

    [Fact]
    public void A_rise_seen_from_the_frame_before_starts_the_clock()
    {
        var c = default(AiGuardClock);
        double t = Run(ref c, 10, 11, Forward); // no guard for 1 s
        Assert.Equal(AiGuardNote.Rise, c.Note(DefendDown | Forward, t));
        Assert.Equal(AiClockRead.Known, c.Read(t + 0.05, out double age));
        Assert.Equal(0.05, age, 9);
        t = Run(ref c, t + Frame, t + 0.3, DefendDown);
        Assert.Equal(AiClockRead.Known, c.Read(t, out age));
        Assert.InRange(age, 0.29, 0.31);
    }

    [Fact]
    public void A_held_guard_ages_and_a_direction_change_is_not_a_new_guard()
    {
        var c = default(AiGuardClock);
        double t = Run(ref c, 0, 0.5, 0);
        t = Run(ref c, t, t + 1.0, DefendDown); // up for 1 s
        Assert.Equal(AiGuardNote.None, c.Note(DefendLeft, t)); // turned to the blow: the same guard (like the automatic block direction)
        Assert.Equal(AiClockRead.Known, c.Read(t + 0.01, out double age));
        Assert.True(age > 0.99, "a direction change restarted the AI's clock: " + age);
    }

    [Fact]
    public void A_flicker_of_the_bits_is_one_guard_a_real_drop_is_a_new_one()
    {
        var c = default(AiGuardClock);
        double t = Run(ref c, 0, 0.5, 0);
        double raised = t;
        t = Run(ref c, t, t + 0.6, DefendRight);
        t = Run(ref c, t, t + 0.05, AttackDown); // down 0.05 s (< DropSeconds 0.1) - e.g. our hold's frames
        Assert.Equal(AiGuardNote.Flicker, c.Note(DefendRight, t));
        Assert.Equal(AiClockRead.Known, c.Read(t, out double age));
        Assert.Equal(t - raised, age, 6);
        t = Run(ref c, t + Frame, t + 0.3, 0); // down 0.3 s
        Assert.Equal(AiGuardNote.Rise, c.Note(DefendRight, t));
        Assert.Equal(AiClockRead.Known, c.Read(t + 0.1, out age));
        Assert.Equal(0.1, age, 6);
    }

    [Fact]
    public void A_gap_says_nothing_about_when_the_guard_went_up()
    {
        var c = default(AiGuardClock);
        Assert.Equal(AiGuardNote.UnseenStart, c.Note(DefendDown, 5)); // the first frame ever, guard up
        Assert.Equal(AiClockRead.UnseenStart, c.Read(5.05, out double age));
        Assert.True(double.IsNaN(age));
        double t = Run(ref c, 5 + Frame, 6, 0);
        // the callback off for 1 s (not watched), back with his guard up: unknown again
        Assert.Equal(AiGuardNote.UnseenStart, c.Note(DefendDown, t + 1));
        Assert.Equal(AiClockRead.UnseenStart, c.Read(t + 1.02, out _));
        // the first frame after a gap with NO guard, and a rise within DropSeconds of it: still unknown (it may have been up in the gap)
        var d = default(AiGuardClock);
        d.Note(0, 20);
        Assert.Equal(AiGuardNote.Flicker, d.Note(DefendDown, 20 + 0.05));
        Assert.Equal(AiClockRead.UnseenStart, d.Read(20.1, out _));
        // ...and one after it: a real rise
        var e = default(AiGuardClock);
        Run(ref e, 30, 30.2, 0);
        Assert.Equal(AiGuardNote.Rise, e.Note(DefendDown, 30.2));
    }

    [Fact]
    public void At_contact_stale_no_guard_and_just_dropped()
    {
        var c = default(AiGuardClock);
        Assert.Equal(AiClockRead.NotWatched, c.Read(1, out _)); // never seen
        double t = Run(ref c, 0, 0.5, 0);
        Assert.Equal(AiClockRead.NoGuardInInput, c.Read(t, out _));
        double raised = t;
        t = Run(ref c, t, t + 0.2, DefendDown);
        c.Note(0, t); // the bits just went down (the engine's guard is still there)
        Assert.Equal(AiClockRead.Known, c.Read(t + 0.05, out double age));
        Assert.Equal(t + 0.05 - raised, age, 6);
        Assert.Equal(AiClockRead.NoGuardInInput, c.Read(t + 0.15, out _));
        Assert.Equal(AiClockRead.NotWatched, c.Read(t + 0.3, out _)); // no frame for 0.3 s: stale
    }

    private static ImpactRules Rules()
    {
        var s = new TraxSettings();
        s.Set(SettingsSchema.RiposteStaggerMaxPercent, 70, "test");
        s.Set(SettingsSchema.RiposteStunMaxSeconds, 1.0, "test");
        s.Set(SettingsSchema.ImpactStunCapSeconds, 2.0, "test");
        s.Set(SettingsSchema.ImpactOnPlayerPercent, 100, "test");
        s.Set(SettingsSchema.TimedBlocksEnabled, 1, "test");
        s.Set(SettingsSchema.TimedBlockAiPercent, 50, "test");
        s.Set(SettingsSchema.TimedBlockAgainstYouMaxSeconds, 1.2, "test");
        return ImpactRules.From(s);
    }

    [Fact]
    public void The_AI_clock_scores_at_the_AI_share_with_the_skill_window()
    {
        var r = Rules().Timed;
        Assert.Equal(0.5, TimedBlockMath.ScoreOf(r, ImpactEvent.RightBlock, TimingSource.AiClock, 0.05, false), 9);
        TimedBlockMath.WindowFor(r, 300, out double p, out double w); // skill 300: perfect 0.25 s
        Assert.Equal(0.5, TimedBlockMath.ScoreOf(r, ImpactEvent.Parry, TimingSource.AiClock, 0.24, false, p, w), 9);
        Assert.Equal(0, TimedBlockMath.ScoreOf(r, ImpactEvent.RightBlock, TimingSource.AiClock, 2.0, false));
        Assert.Equal("the AI's guard clock", TimedBlockMath.SourceName(TimingSource.AiClock));
    }

    [Fact]
    public void The_decision_takes_the_AI_clock_and_the_AI_riposte_on_you_stays_at_most_1_2_s()
    {
        var f = new ImpactFacts
        {
            Event = ImpactEvent.RightBlock, AttackerPoints = 100, AttackerF = 1, AttackerAnimation = 1, AttackerSkill = 100, DefenderPoints = 100, DefenderBar = 1,
            DefenderF = 1, DefenderSkill = 100, WeaponWeight = 1.2, SwingSpeed = 92, DamageType = ImpactDamageType.Cut, GuardWeight = 4.0, DefenderHasShield = true,
            DefenderMaxHealth = 100, DefenderIsPlayer = false, AttackerIsPlayer = true, TimingSource = TimingSource.AiClock, GuardAge = 0.0,
        };
        var rules = Rules();
        var d = default(ImpactState);
        var a = default(ImpactState);
        var dec = ImpactMath.Decide(in rules, in f, ref d, ref a, 100, new FixedDice(0.0));
        Assert.Equal(TimingSource.AiClock, dec.TimedSource);
        Assert.Equal(0.5, dec.TimedScore, 9);
        Assert.True(dec.Riposte.Happens);
        Assert.InRange(dec.RiposteStunSeconds, 0, 1.2 + 1e-9);
    }

    private sealed class FixedDice : IRandomSource
    {
        private readonly double _v;
        public FixedDice(double v) => _v = v;
        public double NextDouble() => _v;
    }

    [Fact]
    public void The_stats_count_the_source_and_the_line_reads()
    {
        var t = new TimedBlockStats();
        var s = new AiGuardClockStats();
        s.NoteContact(AiClockRead.Known, 0.12);
        s.NoteContact(AiClockRead.Known, 0.40);
        s.NoteContact(AiClockRead.UnseenStart, double.NaN);
        s.NoteContact(AiClockRead.NotWatched, double.NaN);
        s.NoteWatch(true);
        s.NoteWatch(true);
        s.NoteWatch(false);
        s.Frames = 1200;
        s.Notes[(int)AiGuardNote.Rise] = 7;
        Assert.Equal(0.26, s.Median(), 9);
        string line = s.SummaryLine(t, 5, 0.25);
        _out.WriteLine(line);
        Assert.StartsWith("timed blocks - the AI's guard clock (step 102;", line);
        Assert.Contains("AI correct blocks by timing 0 / 0-0.25 / 0.25-0.5 / 0.5-0.8 / 0.8-1: 0/0/0/0/0", line);
        Assert.Contains("a known guard age 2 of 4 (50%)", line);
        Assert.Contains("median AI guard age 260 ms", line);
        Assert.Contains("men watched at most 2 at once", line);
        Assert.Contains("guard rises 7", line);
        Assert.Equal(6, t.BySource.Length); // room for the new source
    }

    [Fact]
    public void Noting_and_reading_allocate_nothing()
    {
        var c = default(AiGuardClock);
        var stats = new AiGuardClockStats();
        long bytes = NoAllocation.Bytes(() =>
        {
            double t = 0;
            for (int i = 0; i < 100000; i++)
            {
                uint f = (i / 37) % 3 == 0 ? DefendDown : 0u;
                c.Note(f, t);
                c.Read(t, out _);
                stats.NoteContact(AiClockRead.Known, 0.2);
                t += Frame;
            }
        });
        Assert.Equal(0, bytes);
    }

    [Fact]
    public void The_cost_of_a_frame()
    {
        var c = default(AiGuardClock);
        const int n = 2_000_000;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        double t = 0;
        for (int i = 0; i < n; i++)
        {
            c.Note((i / 37) % 3 == 0 ? DefendDown : 0u, t);
            t += Frame;
        }
        sw.Stop();
        double ns = sw.Elapsed.TotalMilliseconds * 1e6 / n;
        _out.WriteLine("AiGuardClock.Note: " + ns.ToString("0.0") + " ns a frame (managed part only)");
        Assert.True(ns < 1000, "a frame costs " + ns + " ns");
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using TraxCombat.Core;
using Xunit.Abstractions;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 104b - THE REEL IN THE SADDLE, LIVE (DESIGN decision 93, AI_NOTES "104b - built"): the lock's frame (no attack, no guard, a ready
/// cancelled - never his steering), a reeling RIDER stays out of step 105's knockdown ("a rider") and gets no reeling bonus (his knock-off is
/// 104c's), the reel's cause, the switch ON with config format 29 moving 104a's OFF once, the on-screen words, the [summary] line (left over 0),
/// no allocation.
/// </summary>
public class Step104bTests
{
    private readonly ITestOutputHelper _out;

    public Step104bTests(ITestOutputHelper output) => _out = output;

    private sealed class Script : IRandomSource
    {
        private readonly Queue<double> _q;
        public int Calls;
        public Script(params double[] v) => _q = new Queue<double>(v);
        public double NextDouble()
        {
            Calls++;
            return _q.Count > 0 ? _q.Dequeue() : 0.5;
        }
    }

    private const uint MoveAndTurn = 0xF | 0x30; // Forward | Backward | StrafeRight | StrafeLeft | TurnRight | TurnLeft
    private const uint AttackLeft = 0x40, DefendRight = 0x1000;

    // ------------------------------------------------------------------ A. the lock's frame

    [Fact]
    public void The_lock_takes_out_attack_and_guard_and_never_the_steering()
    {
        uint f = AiInputMath.ReelLock(MoveAndTurn | AttackLeft | DefendRight, noGuard: true, inReady: false, out var edit);
        Assert.Equal(MoveAndTurn, f);
        Assert.Equal(InputEdit.AttackCleared | InputEdit.GuardCleared, edit);
        // TimedRiderNoGuard off: only the attack
        f = AiInputMath.ReelLock(MoveAndTurn | AttackLeft | DefendRight, noGuard: false, inReady: false, out edit);
        Assert.Equal(MoveAndTurn | DefendRight, f);
        Assert.Equal(InputEdit.AttackCleared, edit);
        // a quiet frame: untouched, no edit
        Assert.Equal(MoveAndTurn, AiInputMath.ReelLock(MoveAndTurn, true, false, out edit));
        Assert.Equal(InputEdit.None, edit);
        // every bit outside attack / defend survives (the event-free movement flags of a rider)
        for (int bit = 0; bit < 32; bit++)
        {
            uint b = 1u << bit;
            if ((b & (AiInputMath.AttackMask | AiInputMath.DefendMask)) != 0) continue;
            Assert.Equal(b, AiInputMath.ReelLock(b, true, false, out _));
        }
    }

    [Fact]
    public void In_a_ready_the_blow_is_cancelled_never_released()
    {
        uint f = AiInputMath.ReelLock(MoveAndTurn | AttackLeft, noGuard: true, inReady: true, out var edit);
        Assert.Equal(MoveAndTurn | AiInputMath.DefendDown, f); // the guard bit cancels the ready (step 16's rule) - the only guard ever written
        Assert.Equal(InputEdit.AttackCleared | InputEdit.ReadyCancelled, edit);
        Assert.Equal(0u, f & AiInputMath.AttackMask);
    }

    // ------------------------------------------------------------------ B. a reeling RIDER and step 105 / the reeling bonus

    private static ImpactFacts CleanHitOnARider(bool riderDefender) => new ImpactFacts
    {
        Event = ImpactEvent.CleanHit,
        AttackerPoints = 100, AttackerF = 1, AttackerAnimation = 1, AttackerSkill = 100,
        DefenderPoints = 100, DefenderBar = 1, DefenderF = 1, DefenderSkill = 100,
        WeaponWeight = 1.2, SwingSpeed = 92, DamageType = ImpactDamageType.Cut, GuardWeight = 1.2, DefenderMaxHealth = 100, Damage = 30,
        DefenderMounted = riderDefender,
    };

    [Fact]
    public void A_reeling_rider_is_refused_by_step_105_and_gets_no_reeling_bonus()
    {
        var ir = ImpactRules.From(new TraxSettings());
        Assert.True(ir.ReelingHit.On); // step 105 on (80 %)
        var reeling = default(ImpactState);
        reeling.Reel(101, 2, ReelCause.RiderReel);
        var calm = default(ImpactState);
        var a1 = default(ImpactState);
        var a2 = default(ImpactState);
        var f = CleanHitOnARider(true);
        var d1 = ImpactMath.Decide(in ir, in f, ref reeling, ref a1, 100, new Script());
        var d2 = ImpactMath.Decide(in ir, in f, ref calm, ref a2, 100, new Script());
        Assert.True(d1.ReelingHit.Asked);
        Assert.Equal(ReelingHitRefusal.Mounted, d1.ReelingHit.Refusal); // refused "a rider" - never the 80 % knockdown
        Assert.False(d1.ReelingHit.Happens);
        Assert.False(d1.Reeling); // no reeling bonus on a rider's pressure
        Assert.Equal(d2.X, d1.X, 12); // his knock-off decided exactly as if he did not reel
        Assert.Equal(d2.Knockdown.Chance, d1.Knockdown.Chance, 12);
        // a man on FOOT who reels still gets both (step 105 unchanged)
        var footReeling = default(ImpactState);
        footReeling.Reel(101, 2, ReelCause.TimedRiposte);
        var a3 = default(ImpactState);
        var foot = CleanHitOnARider(false);
        var d3 = ImpactMath.Decide(in ir, in foot, ref footReeling, ref a3, 100, new Script(0.1, 0.1, 0.1, 0.1));
        Assert.True(d3.Reeling);
        Assert.Equal(ReelingHitRefusal.None, d3.ReelingHit.Refusal);
    }

    [Fact]
    public void The_reels_cause_is_named_and_counted()
    {
        Assert.Equal(ReelingHitMath.CauseCount, Enum.GetValues(typeof(ReelCause)).Length);
        Assert.Equal("a reel in the saddle", ReelingHitMath.CauseName(ReelCause.RiderReel));
        var s = default(ImpactState);
        s.Reel(10, 2, ReelCause.RiderReel);
        Assert.True(s.Reeling(9.9) && s.StaggerImmune(11.9));
        Assert.Equal(ReelCause.RiderReel, s.ReelCause);
    }

    // ------------------------------------------------------------------ C. the switch ON, config format 29

    private static double Shipped(ParamDef p) => p == SettingsSchema.TimedBlocksOnHorseback ? 1 : p.Default;

    [Fact]
    public void The_switch_ships_on_and_format_29_moves_104as_off_once()
    {
        Assert.Equal(29, ConfigFile.FormatVersion);
        // shipped ON in defaults.json (the tests' own defaults are DESIGN's initial values - the switch's is off, as 104a shipped it)
        Assert.Contains("\"TimedBlocksOnHorseback\": true,", DefaultsFile.ReadEmbeddedText(), StringComparison.Ordinal);
        // Toni's file: written by step 104a (format 28) with the switch OFF he never touched → ON, said once
        var read = ConfigFile.Read("{ \"ConfigVersion\": 28, \"TimedBlocksOnHorseback\": false }");
        var notes = ConfigFile.Migrate(read, Shipped);
        Assert.Equal(1, read.Values["TimedBlocksOnHorseback"], 9);
        Assert.StartsWith("TimedBlocksOnHorseback: false → true (format 28 → 29, the old default; step 104b", Assert.Single(notes), StringComparison.Ordinal);
        // a format-29 file keeps whatever it holds (turned off by hand stays off)
        var mine = ConfigFile.Read("{ \"ConfigVersion\": 29, \"TimedBlocksOnHorseback\": false }");
        Assert.Empty(ConfigFile.Migrate(mine, Shipped));
        Assert.Equal(0, mine.Values["TimedBlocksOnHorseback"], 9);
        // an older file never had the key - nothing to move (it takes the default like any missing key)
        var old = ConfigFile.Read("{ \"ConfigVersion\": 27 }");
        Assert.DoesNotContain(ConfigFile.Migrate(old, Shipped), n => n.StartsWith("TimedBlocksOnHorseback", StringComparison.Ordinal));
        var rr = TimedRiderRules.From(On());
        Assert.True(rr.Live);
        Assert.False(TimedRiderRules.From(On(s => s.Set(SettingsSchema.ModEnabled, false, "test"))).Live); // the master switch first
        Assert.Equal("act_stagger_backward", rr.ReelAction); // the default reel (Toni's J-tool pick is a setting change)
        Assert.Equal(1.0, rr.AnimSpeed, 9);
        Assert.Equal(1, TimedRiderAnimMath.RiderChannel);
        Assert.Contains("animation test tool", SettingsSchema.TimedRiderReelAction.Description, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"[Ss]tep \d", SettingsSchema.TimedBlocksOnHorseback.Description);
        Assert.DoesNotContain("Not built", SettingsSchema.TimedBlocksOnHorseback.Description, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ D. the words, the summary line

    [Fact]
    public void Your_reel_line_on_screen()
    {
        Assert.Equal("Your block - Polmarc reels in the saddle 1.45 s / 1.50 (guard 0.20 s / 0.15)",
            TimedRiderLines.YourReel("Polmarc", ImpactEvent.RightBlock, 1.45, 1.5, TimingSource.PlayerClock, 0.20, 0.15));
        Assert.Equal("Your parry - Polmarc reels in the saddle 1.50 s / 1.50 (a chamber)",
            TimedRiderLines.YourReel("Polmarc", ImpactEvent.Parry, 1.5, 1.5, TimingSource.Chamber, 0, 0.15));
        Assert.Null(TimedRiderLines.YourReel("Polmarc", ImpactEvent.RightBlock, 1.0, 1.5, TimingSource.None, double.NaN, 0.15));
        // the shadow line says the reel is live
        var r = TimedRiderRules.From(On());
        var f = new TimedRiderFacts
        {
            Event = ImpactEvent.RightBlock, BlockerMounted = true, AttackerMounted = true, BlockerIsPlayer = true, Score = 1, RowInRow = true, RowChance = 1,
            RowRefusal = ImpactRefusal.Rider, Length56 = 0.6, StunCap = 2, HorseSpeed = 6.5, TiredMountMultiplier = 1,
        };
        var o = TimedRiderMath.Decide(in r, f, double.NaN, new Script(0.5, 0.9, 0.9));
        string live = TimedRiderMath.Describe(in o, "Rider", "you", true);
        _out.WriteLine(live);
        Assert.Contains("REELS (live) 1.50 s", live);
        Assert.Contains("the reel LIVE; the rear / knock-off shadow only (step 104c)", live);
        Assert.Contains("WOULD reel 1.50 s", TimedRiderMath.Describe(in o, "Rider", "you", false));
    }

    [Fact]
    public void The_summary_line_counts_every_release_and_left_over_reads_0()
    {
        var r = TimedRiderRules.From(On());
        var st = new TimedRiderReelStats();
        st.NoteDecided(1, true, false);
        st.NoteDecided(0.6, false, false);
        st.NoteDecided(0.3, false, true);
        st.Started = 2;
        st.LockAi = 1;
        st.LockYou = 1;
        st.AnimTaken = 2;
        st.HorsesChecked = 2;
        st.HorseCheckSum = 1.0;
        st.NoteSpeed(0, 6.5);
        st.NoteSpeed(1, 4.0);
        st.NoteSpeed(2, 3.5);
        st.NoteFrame(false, InputEdit.AttackCleared | InputEdit.GuardCleared);
        st.NoteFrame(true, InputEdit.AttackCleared);
        st.NoteEnded(RiderReelEnd.Ended, true, 1.5, 1.50);
        st.NoteEnded(RiderReelEnd.Dismounted, true, 1.0, 0.4);
        st.NoteEnded(RiderReelEnd.Removed, false, 0.8, double.NaN);
        st.LeftOver = 0;
        string line = st.SummaryLine(in r, true);
        _out.WriteLine(line);
        Assert.StartsWith("timed blocks on horseback (step 104b): the reel LIVE - reels decided 3 by timing band [none / <0.25 / <0.5 / <0.8 / 0.8+] [0 / 0 / 1 / 1 / 1]", line);
        Assert.Contains("(your blocks 1, on you 1, pool full 0); started 2, gone before the tick 1", line);
        Assert.Contains("in the saddle (whole reels) asked mean 1.50 s → measured mean 1.50 s (1)", line); // the dismounted one by its reason only
        Assert.Contains("the animation act_stagger_backward x1.00 on channel 1: taken 2", line);
        Assert.Contains("the AI 1 (frames 1 - attacks out 1, guards out 1", line);
        Assert.Contains("you 1 (presses swallowed - attacks 1, guards 0)", line);
        Assert.Contains("locks released: at its end 1, death / removal 1, dismounted 1, a switch off 0, the mission end 0, an error 0 - left over at the mission end 0", line);
        Assert.Contains("horses checked 2 (mean x0.50; his speed at the start 6.5 → 0.5 s 4.0 → the end 3.5 m/s)", line);
        Assert.Equal(TimedRiderReelStats.EndCount, Enum.GetValues(typeof(RiderReelEnd)).Length);
        Assert.Contains("OFF (TimedBlocksOnHorseback)", st.SummaryLine(TimedRiderRules.From(Off()), true));
        Assert.Contains("OFF (the riposte not live", st.SummaryLine(in r, false));
    }

    private static TraxSettings Off() => new TraxSettings(); // DESIGN's initial value: off

    /// <summary>The switch as shipped (defaults.json: on).</summary>
    private static TraxSettings On(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        s.Set(SettingsSchema.TimedBlocksOnHorseback, true, "test");
        tune?.Invoke(s);
        return s;
    }

    // ------------------------------------------------------------------ E. no allocation in the per-frame parts

    [Fact]
    public void The_lock_frame_and_the_counts_allocate_nothing()
    {
        var st = new TimedRiderReelStats();
        uint sink = 0;
        long bytes = NoAllocation.Bytes(() =>
        {
            for (int i = 0; i < 1000; i++)
            {
                sink ^= AiInputMath.ReelLock((uint)i * 0x41u, (i & 1) == 0, (i & 4) == 0, out var e);
                st.NoteFrame((i & 2) == 0, e);
                st.NoteSpeed(i % 3, i * 0.01);
            }
        });
        Assert.Equal(0, bytes);
        _out.WriteLine("sink " + sink);
    }
}

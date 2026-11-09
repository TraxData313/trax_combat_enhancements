using System;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 106b - OPENINGS, THE FORCED GUARD, the pure part (AI_NOTES "Step 106" §106b, DESIGN decision 95 (i)): the guard's movement bits, the
/// forced frame (attack bits out, the guard replaced, or every guard bit out when lowered - DefendBlock too, the move bits his), when a shield
/// gives way, the live block roll, the test tool's cycle and its read-back verdict, the settings (the tool off, K, 2 s), <c>OpeningRules.Runs</c>,
/// the [summary] line, no allocation.
/// </summary>
public class Step106bTests
{
    private const uint Forward = 0x1, TurnLeft = 0x20, AttackLeft = 0x40, AttackUp = 0x100, DefendLeft = 0x400, DefendRight = 0x800,
        DefendUp = 0x1000, DefendDown = 0x2000, DefendAuto = 0x4000, DefendBlock = 0x8000, Action = 0x10000;

    private static OpeningRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return OpeningRules.From(s);
    }

    [Fact]
    public void Each_guard_is_its_own_engine_bit_and_back()
    {
        Assert.Equal(DefendUp, OpeningForceMath.GuardFlag(OpeningMath.GuardUp));
        Assert.Equal(DefendDown, OpeningForceMath.GuardFlag(OpeningMath.GuardDown));
        Assert.Equal(DefendLeft, OpeningForceMath.GuardFlag(OpeningMath.GuardLeft));
        Assert.Equal(DefendRight, OpeningForceMath.GuardFlag(OpeningMath.GuardRight));
        Assert.Equal(0u, OpeningForceMath.GuardFlag(OpeningMath.Unknown));
        Assert.Equal(0u, OpeningForceMath.GuardFlag(OpeningMath.AttackUp));
        for (int g = OpeningMath.GuardUp; g <= OpeningMath.GuardRight; g++) Assert.Equal(g, OpeningForceMath.GuardOfFlag(OpeningForceMath.GuardFlag(g)));
        Assert.Equal(OpeningMath.Unknown, OpeningForceMath.GuardOfFlag(DefendAuto));
        Assert.Equal(OpeningMath.Unknown, OpeningForceMath.GuardOfFlag(DefendUp | DefendLeft));
        Assert.Equal(0xFC00u, OpeningForceMath.GuardBits);
    }

    [Fact]
    public void The_forced_frame_takes_his_attack_and_writes_the_balance_guard_his_moves_kept()
    {
        uint f = OpeningForceMath.Frame(Forward | TurnLeft | AttackLeft | DefendUp | Action, DefendRight, false, out var edit);
        Assert.Equal(Forward | TurnLeft | Action | DefendRight, f);
        Assert.Equal(InputEdit.AttackCleared | InputEdit.GuardRaised, edit);

        f = OpeningForceMath.Frame(Forward | DefendRight, DefendRight, false, out edit); // his own guard already there
        Assert.Equal(Forward | DefendRight, f);
        Assert.Equal(InputEdit.None, edit);

        f = OpeningForceMath.Frame(Forward, DefendDown, false, out edit); // no guard of his own: ours raised
        Assert.Equal(Forward | DefendDown, f);
        Assert.Equal(InputEdit.GuardRaised, edit);

        f = OpeningForceMath.Frame(DefendAuto | DefendBlock, DefendLeft, false, out _);
        Assert.Equal(DefendLeft, f);
    }

    [Fact]
    public void Lowered_means_every_guard_bit_out_DefendBlock_too()
    {
        uint f = OpeningForceMath.Frame(Forward | AttackUp | DefendDown | DefendBlock | DefendAuto, DefendRight, true, out var edit);
        Assert.Equal(Forward, f);
        Assert.Equal(InputEdit.AttackCleared | InputEdit.GuardCleared, edit);
        f = OpeningForceMath.Frame(Forward, 0u, true, out edit);
        Assert.Equal(Forward, f);
        Assert.Equal(InputEdit.None, edit);
    }

    [Fact]
    public void A_shield_gives_way_from_its_second_link_and_after_an_unguarded_hit()
    {
        var r = Rules();
        var dice = new SeededRandom(1);
        int sword = OpeningMath.WeaponMask(true, true);
        var first = OpeningMath.Plan(in r, 1, OpeningEntry.ShieldBlocked, 100, false, OpeningMath.GuardUp, OpeningMath.AttackUp, sword, dice);
        var second = OpeningMath.Plan(in r, 2, OpeningEntry.ShieldBlocked, 100, false, OpeningMath.GuardUp, OpeningMath.AttackUp, sword, dice);
        var parry = OpeningMath.Plan(in r, 1, OpeningEntry.Blocked, 100, false, OpeningMath.GuardUp, OpeningMath.AttackUp, sword, dice);
        var unguarded = OpeningMath.Plan(in r, 1, OpeningEntry.FrontUnguarded, 100, false, OpeningMath.Unknown, OpeningMath.AttackUp, sword, dice);
        Assert.False(OpeningForceMath.Lowers(true, in first));
        Assert.True(OpeningForceMath.Lowers(true, in second));
        Assert.False(OpeningForceMath.Lowers(false, in parry));
        Assert.True(OpeningForceMath.Lowers(true, in unguarded));
        Assert.False(OpeningForceMath.Lowers(false, in second));
    }

    [Fact]
    public void The_live_roll_is_through_at_or_above_his_block_chance()
    {
        var r = Rules();
        Assert.True(OpeningForceMath.Through(0.0, OpeningMath.BlockChance(in r, 1.0, 1, false))); // perfect: chance 0
        Assert.False(OpeningForceMath.Through(0.999, OpeningMath.BlockChance(in r, 0.0, 1, true))); // the shield holds: chance 1
        Assert.True(OpeningForceMath.Through(0.5, 0.5));
        Assert.False(OpeningForceMath.Through(0.49, 0.5));
        Assert.False(OpeningForceMath.Through(double.NaN, 0));
    }

    [Fact]
    public void The_test_tool_cycles_six_guards_both_ways()
    {
        Assert.Equal(OpeningTestMode.Low, OpeningForceMath.Next(-1, false));
        Assert.Equal(OpeningTestMode.LoweredByFlag, OpeningForceMath.Next(-1, true));
        Assert.Equal(OpeningTestMode.High, OpeningForceMath.Next((int)OpeningTestMode.Low, false));
        Assert.Equal(OpeningTestMode.Low, OpeningForceMath.Next((int)OpeningTestMode.LoweredByFlag, false));
        Assert.Equal(OpeningTestMode.LoweredByFlag, OpeningForceMath.Next((int)OpeningTestMode.Low, true));
        Assert.Equal(OpeningMath.GuardDown, OpeningForceMath.TestGuard(OpeningTestMode.Low));
        Assert.Equal(OpeningMath.GuardUp, OpeningForceMath.TestGuard(OpeningTestMode.High));
        Assert.Equal(OpeningMath.GuardLeft, OpeningForceMath.TestGuard(OpeningTestMode.Left));
        Assert.Equal(OpeningMath.GuardRight, OpeningForceMath.TestGuard(OpeningTestMode.Right));
        Assert.Equal(OpeningMath.Unknown, OpeningForceMath.TestGuard(OpeningTestMode.Lowered));
        Assert.True(OpeningForceMath.TestLowers(OpeningTestMode.Lowered) && OpeningForceMath.TestLowers(OpeningTestMode.LoweredByFlag));
        Assert.False(OpeningForceMath.TestLowers(OpeningTestMode.Right));
        for (int m = 0; m < (int)OpeningTestMode.Count; m++) Assert.NotEqual("?", OpeningForceMath.TestName((OpeningTestMode)m));
    }

    [Fact]
    public void The_read_back_verdict()
    {
        Assert.True(OpeningForceMath.PoseMatches(OpeningMath.GuardLeft, false, OpeningMath.GuardLeft, true));
        Assert.False(OpeningForceMath.PoseMatches(OpeningMath.GuardLeft, false, OpeningMath.GuardRight, true));
        Assert.False(OpeningForceMath.PoseMatches(OpeningMath.GuardLeft, false, OpeningMath.GuardLeft, false));
        Assert.True(OpeningForceMath.PoseMatches(OpeningMath.Unknown, true, -1, false));
        Assert.False(OpeningForceMath.PoseMatches(OpeningMath.Unknown, true, OpeningMath.GuardDown, true));
    }

    [Fact]
    public void The_settings_ship_the_tool_off_on_K_for_two_seconds_and_runs_follows_either_switch()
    {
        var s = new TraxSettings();
        Assert.False(s.OpeningGuardTest);
        Assert.Equal("K", s.OpeningGuardTestKey);
        Assert.Equal(2.0, s.OpeningGuardTestSeconds, 3);
        Assert.Equal(SettingsPage.Advanced, SettingsSchema.OpeningGuardTest.Group.Page);
        var r = Rules();
        Assert.True(r.Runs);
        r = Rules(t => t.Set(SettingsSchema.OpeningsShadowLog, false, SettingSources.File));
        Assert.False(r.Runs);
        r = Rules(t =>
        {
            t.Set(SettingsSchema.OpeningsShadowLog, false, SettingSources.File);
            t.Set(SettingsSchema.OpeningsEnabled, true, SettingSources.File);
        });
        Assert.True(r.Runs && r.Enabled);
        r = Rules(t =>
        {
            t.Set(SettingsSchema.OpeningsEnabled, true, SettingSources.File);
            t.Set(SettingsSchema.ModEnabled, false, SettingSources.File);
        });
        Assert.False(r.Runs || r.Enabled);
    }

    [Fact]
    public void The_summary_line_counts_frames_poses_and_lowerings()
    {
        var st = new OpeningForceStats();
        st.Balance = 3;
        st.Held = 2;
        st.ShieldLowerings = 1;
        st.Tests = 6;
        st.RollsThrough = 2;
        st.RollsBlocked = 1;
        st.BlockedAnyway = 1;
        st.NoteFrames(120, 10, 30, 5, 40);
        st.NotePose(true, true);
        st.NotePose(false, false);
        st.NoteEnd(OpeningForceEnd.Resolved);
        st.FlagsOff = st.FlagsBack = 1;
        string line = st.SummaryLine(Rules(), true);
        Assert.StartsWith("openings - the forced guard (step 106b; live OFF (OpeningsEnabled), the test tool ON): forced off balance 3, held after a won roll 2 (shield / guard LOWERED 1), test 6", line);
        Assert.Contains("frames written 120 (attack out 10, his guard replaced 30, taken out 5, lowered 40)", line);
        Assert.Contains("poses read back 2 (as wanted 1, changed 1)", line);
        Assert.Contains("BLOCKED ANYWAY 1", line);
        Assert.Contains("CanDefend off / back 1 / 1", line);
        Assert.Contains("the blow resolved 1", line);
    }

    [Fact]
    public void The_frame_and_the_stats_allocate_nothing()
    {
        var st = new OpeningForceStats();
        long bytes = NoAllocation.Bytes(() =>
        {
            uint acc = 0;
            for (int i = 0; i < 5000; i++)
            {
                acc ^= OpeningForceMath.Frame((uint)i * 0x45u, OpeningForceMath.GuardFlag(4 + i % 4), i % 3 == 0, out _);
                st.NoteFrames(1, 1, 0, 0, i % 2);
                st.NoteEnd((OpeningForceEnd)(i % (int)OpeningForceEnd.Count));
                st.NotePose(i % 2 == 0, i % 3 == 0);
            }
            _sink = acc; // step 106e: was GC.KeepAlive(acc) - it BOXED the uint (24 bytes every run, the test failed on its own allocation)
        });
        Assert.Equal(0, bytes);
    }

    private static uint _sink;
}

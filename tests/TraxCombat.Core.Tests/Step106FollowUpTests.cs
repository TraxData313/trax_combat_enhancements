using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 106 follow-up (Toni 2026-11-09, DESIGN 95 (n)): an off-balance man's attack lock is capped at 1 s a RUN - spamming swings into his guard
/// (each correct block re-arming the force) no longer freezes him; a new run only after 1 s free.
/// </summary>
public class Step106FollowUpTests
{
    [Fact]
    public void The_cap_is_one_second_a_constant()
    {
        Assert.Equal(1.0, OpeningAttackLock.MaxSeconds);
    }

    [Fact]
    public void A_single_off_balance_state_locks_at_most_one_second_not_the_chain_time()
    {
        var l = new OpeningAttackLock();
        Assert.True(l.Begin(7, 10.0, 2.0, out double until));
        Assert.Equal(11.0, until, 6);
        Assert.True(l.Active);

        var shorter = new OpeningAttackLock();
        Assert.True(shorter.Begin(7, 10.0, 0.5, out until)); // a chain time under the cap keeps its own length
        Assert.Equal(10.5, until, 6);
    }

    [Fact]
    public void Spamming_re_arms_do_not_extend_the_lock_he_is_free_after_one_second()
    {
        var l = new OpeningAttackLock();
        Assert.True(l.Begin(3, 0.0, 2.0, out double until));   // your swing blocked: off balance
        Assert.Equal(1.0, until, 6);
        l.End(0.4);                                            // your next wind-up let him go
        Assert.True(l.Begin(3, 0.6, 2.0, out until));          // blocked again 0.2 s later: the same run
        Assert.Equal(1.0, until, 6);                           // still ends at the run's first second
        l.End(1.0);                                            // the cap
        Assert.False(l.Begin(3, 1.3, 2.0, out _));             // you keep swinging: spent - no force, he may attack
        Assert.False(l.Begin(3, 1.9, 2.0, out _));
        Assert.True(l.Begin(3, 2.0, 2.0, out until));          // 1 s free since the last force: a new run
        Assert.Equal(3.0, until, 6);
    }

    [Fact]
    public void Another_man_starts_his_own_run()
    {
        var l = new OpeningAttackLock();
        Assert.True(l.Begin(3, 0.0, 2.0, out _));
        l.End(0.9);
        Assert.True(l.Begin(4, 1.0, 2.0, out double until));
        Assert.Equal(2.0, until, 6);
        Assert.Equal(4, l.Target);
    }

    [Fact]
    public void A_force_still_on_keeps_the_run()
    {
        var l = new OpeningAttackLock();
        Assert.True(l.Begin(3, 0.0, 2.0, out _));
        Assert.True(l.Begin(3, 0.95, 2.0, out double until)); // re-armed while on (no End between)
        Assert.Equal(1.0, until, 6);
        Assert.False(l.Begin(3, 1.0, 2.0, out _));             // at the cap: spent
        l.Reset();
        Assert.False(l.Has);
        Assert.True(l.Begin(3, 1.0, 2.0, out _));
    }

    [Fact]
    public void The_summary_counts_the_lock()
    {
        var st = new OpeningForceStats { LockCapped = 2, LockSpent = 3 };
        Assert.Contains("the attack lock (at most 1.0 s a run): cut short 2, spent - not forced 3", st.SummaryLine(OpeningRules.From(new TraxSettings()), false));
    }

    [Fact]
    public void The_lock_allocates_nothing()
    {
        var l = new OpeningAttackLock();
        long bytes = NoAllocation.Bytes(() =>
        {
            double t = 0;
            for (int i = 0; i < 5000; i++)
            {
                t += 0.3;
                if (l.Begin(i % 3, t, 2.0, out double u)) _sink += u;
                if (i % 2 == 0) l.End(t + 0.1);
            }
        });
        Assert.Equal(0, bytes);
    }

    private static double _sink;
}

using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 109 (Toni 2026-11-10: tired men walk up and stare from very close; AI_NOTES "Step 109", DESIGN decision 98) - KEEP YOUR DISTANCE: the settings
/// (the switch on the player page after the war cries, seven Advanced after the war cries' numbers), the master switch first, the quiet wait, who may
/// back off, how far (the weapon lengths, the cap, the fallback), the walk against the rest's limit behind his front, the stand, the enemy stepping in,
/// the verdict's order, the stats and the [summary] line, no allocation.
/// </summary>
public class Step109Tests
{
    private static readonly ParamDef[] Advanced =
    {
        SettingsSchema.KeepDistanceQuietMinSeconds, SettingsSchema.KeepDistanceQuietMaxSeconds, SettingsSchema.KeepDistanceWeaponLengths,
        SettingsSchema.KeepDistanceFallbackLengthMetres, SettingsSchema.KeepDistanceMaxBackMetres, SettingsSchema.KeepDistanceMaxHoldSeconds,
        SettingsSchema.KeepDistanceReachMetres,
    };

    private static KeepDistanceRules On() => new KeepDistanceRules(true, true, true);

    [Fact]
    public void The_switch_follows_the_war_cries_on_the_player_page_and_the_numbers_follow_theirs()
    {
        var all = SettingsSchema.All.ToList();
        Assert.Equal(all.IndexOf(SettingsSchema.WarCriesEnabled) + 1, all.IndexOf(SettingsSchema.KeepDistanceEnabled));
        Assert.Contains(SettingsSchema.KeepDistanceEnabled, SettingsSchema.PlayerPage);
        Assert.Equal(1, SettingsSchema.KeepDistanceEnabled.Default, 9);
        Assert.DoesNotMatch(@"[Ss]tep \d", SettingsSchema.KeepDistanceEnabled.Description);
        int first = all.IndexOf(SettingsSchema.WarCryBattlePerSecond) + 1;
        for (int i = 0; i < Advanced.Length; i++)
        {
            Assert.Equal(first + i, all.IndexOf(Advanced[i]));
            Assert.Same(SettingsSchema.CourageGroup, Advanced[i].Group);
            Assert.Equal(ApplyTiming.Live, Advanced[i].Timing);
        }
        var r = KeepDistanceRules.From(new TraxSettings());
        Assert.True(r.On);
        Assert.Equal(2.0, r.QuietMinSeconds, 9);
        Assert.Equal(3.0, r.QuietMaxSeconds, 9);
        Assert.Equal(2.0, r.WeaponLengths, 9);
        Assert.Equal(1.0, r.FallbackLengthMetres, 9);
        Assert.Equal(4.0, r.MaxBackMetres, 9);
        Assert.Equal(10.0, r.MaxHoldSeconds, 9);
        Assert.Equal(3.0, r.ReachMetres, 9);
        var s = new TraxSettings();
        s.Set(SettingsSchema.KeepDistanceMaxBackMetres, 2.5, SettingSources.Mcm);
        Assert.Equal(2.5, KeepDistanceRules.From(s).MaxBackMetres, 5); // live
    }

    [Fact]
    public void The_master_switch_comes_first_then_athletics_then_its_own()
    {
        Assert.False(new KeepDistanceRules(false, true, true).On);
        Assert.Equal("ModEnabled", new KeepDistanceRules(false, false, false).OffBecause);
        Assert.Equal("AthleticsEnabled", new KeepDistanceRules(true, false, true).OffBecause);
        Assert.Equal("KeepDistanceEnabled", new KeepDistanceRules(true, true, false).OffBecause);
        Assert.Null(On().OffBecause);
        Assert.Contains("OFF (KeepDistanceEnabled): measured only", new KeepDistanceRules(true, true, false).Describe());
        Assert.EndsWith("ON", On().Describe());
        // off: never starts, even when every other gate says yes
        Assert.False(KeepDistanceMath.Starts(new KeepDistanceRules(false, true, true), true, false, true, 1.0, true));
        Assert.Equal(KeepEnd.SwitchedOff, KeepDistanceMath.Verdict(new KeepDistanceRules(true, true, false), false, true, false, true, true, 1));
    }

    [Fact]
    public void Each_man_rolls_his_own_quiet_wait_between_two_and_three_seconds()
    {
        var r = On();
        Assert.Equal(2.0, KeepDistanceMath.QuietSeconds(in r, 0), 9);
        Assert.Equal(3.0, KeepDistanceMath.QuietSeconds(in r, 1), 9);
        Assert.Equal(2.5, KeepDistanceMath.QuietSeconds(in r, 0.5), 9);
        Assert.Equal(2.5, KeepDistanceMath.QuietSeconds(in r, double.NaN), 9);
        Assert.Equal(3.0, KeepDistanceMath.QuietSeconds(in r, 7), 9);
        var dice = new Random(109);
        double lo = 9, hi = 0;
        for (int i = 0; i < 1000; i++)
        {
            double q = KeepDistanceMath.QuietSeconds(in r, dice.NextDouble());
            lo = Math.Min(lo, q);
            hi = Math.Max(hi, q);
        }
        Assert.InRange(lo, 2.0, 2.05);
        Assert.InRange(hi, 2.95, 3.0);
        // a max below the min is the min (no inverted range)
        var flat = new KeepDistanceRules(true, true, true, quietMinSeconds: 3, quietMaxSeconds: 1);
        Assert.Equal(3.0, KeepDistanceMath.QuietSeconds(in flat, 0.9), 9);
    }

    [Fact]
    public void The_quiet_clock_runs_from_the_later_of_his_last_action_and_his_last_keep()
    {
        Assert.Equal(10, KeepDistanceMath.QuietStart(10, 4), 9);
        Assert.Equal(12, KeepDistanceMath.QuietStart(10, 12), 9);
        Assert.Equal(10, KeepDistanceMath.QuietStart(10, double.NaN), 9);
        Assert.True(double.IsNegativeInfinity(KeepDistanceMath.QuietStart(double.NaN, double.NegativeInfinity)));
        Assert.False(KeepDistanceMath.Quiet(12.4, 10, 2.5));
        Assert.True(KeepDistanceMath.Quiet(12.5, 10, 2.5));
        Assert.True(KeepDistanceMath.Quiet(5, double.NegativeInfinity, 2.5)); // no action ever
        Assert.False(KeepDistanceMath.Quiet(50, 10, double.NaN)); // not rolled yet
    }

    [Fact]
    public void He_backs_off_only_when_he_cannot_attack_is_free_to_and_his_enemy_is_in_reach()
    {
        var r = On();
        Assert.True(KeepDistanceMath.CannotAttack(true, false, false));
        Assert.True(KeepDistanceMath.CannotAttack(false, true, false));
        Assert.True(KeepDistanceMath.CannotAttack(false, false, true));
        Assert.False(KeepDistanceMath.CannotAttack(false, false, false));
        Assert.True(KeepDistanceMath.Starts(in r, true, false, true, 1.2, true));
        Assert.True(KeepDistanceMath.Starts(in r, true, false, true, 3.0, true)); // at the reach
        Assert.False(KeepDistanceMath.Starts(in r, true, false, true, 3.01, true)); // beyond it
        Assert.False(KeepDistanceMath.Starts(in r, true, false, true, double.NaN, true)); // no enemy
        Assert.False(KeepDistanceMath.Starts(in r, false, false, true, 1.2, true)); // a wall man, a rider, a routing man, a bowman
        Assert.False(KeepDistanceMath.Starts(in r, true, true, true, 1.2, true)); // resting - the rest has him
        Assert.False(KeepDistanceMath.Starts(in r, true, false, false, 1.2, true)); // he can attack
        Assert.False(KeepDistanceMath.Starts(in r, true, false, true, 1.2, false)); // not quiet long enough
        // OFF: still a candidate (the OFF looks measure), never a start
        var off = new KeepDistanceRules(true, true, false);
        Assert.True(KeepDistanceMath.Candidate(in off, true, false, true, 1.2));
        Assert.False(KeepDistanceMath.Starts(in off, true, false, true, 1.2, true));
    }

    [Fact]
    public void How_far_two_of_his_own_weapon_lengths_capped_a_fallback_when_unknown()
    {
        var r = On();
        Assert.Equal(1.9, KeepDistanceMath.BackMetres(in r, 0.95), 9);  // a sword
        Assert.Equal(3.6, KeepDistanceMath.BackMetres(in r, 1.8), 9);   // a short spear
        Assert.Equal(4.0, KeepDistanceMath.BackMetres(in r, 2.5), 9);   // a long spear - the cap
        Assert.Equal(2.0, KeepDistanceMath.BackMetres(in r, double.NaN), 9); // unknown - the fallback 1 m
        Assert.Equal(2.0, KeepDistanceMath.BackMetres(in r, 0.05), 9);  // no real weapon - the fallback
        Assert.True(KeepDistanceMath.BackMetres(in r, 2.0) > KeepDistanceMath.BackMetres(in r, 0.9)); // Toni: a spear keeps a bigger distance
        var none = new KeepDistanceRules(true, true, true, weaponLengths: 0);
        Assert.Equal(0, KeepDistanceMath.BackMetres(in none, 1.0), 9);
        var big = new KeepDistanceRules(true, true, true, maxBackMetres: 99);
        Assert.Equal(10, big.MaxBackMetres, 9); // the range clamps
    }

    [Fact]
    public void The_walk_reaches_his_goal_and_never_passes_the_rests_limit_behind_his_front()
    {
        // no front (a duel, a tournament): all the way to the goal
        Assert.Equal(1.9, KeepDistanceMath.WalkBack(1.2, 3.1, double.NaN, 4), 9);
        Assert.Equal(0, KeepDistanceMath.WalkBack(3.0, 3.1, double.NaN, 4), 9); // within the arrive margin: there
        Assert.Equal(0, KeepDistanceMath.WalkBack(3.5, 3.1, double.NaN, 4), 9); // past it
        // a line: 3 m behind his front, the rest's limit 4 → 1 m more at most
        Assert.Equal(1.0, KeepDistanceMath.WalkBack(1.2, 5.2, 3.0, 4), 9);
        Assert.Equal(0, KeepDistanceMath.WalkBack(1.2, 5.2, 3.95, 4), 9);
        Assert.Equal(0, KeepDistanceMath.WalkBack(1.2, 5.2, 6.0, 4), 9); // already past it (his line moved on)
        Assert.Equal(1.9, KeepDistanceMath.WalkBack(1.2, 3.1, -2.0, 4), 9); // he stands ahead of his front: the goal decides
        Assert.Equal(0, KeepDistanceMath.WalkBack(double.NaN, 3.1, double.NaN, 4), 9);
    }

    [Fact]
    public void Holding_he_stands_unless_the_enemy_walked_off_or_his_line_moved_on_and_a_step_in_is_seen()
    {
        Assert.True(KeepDistanceMath.Stands(3.0, 3.1, double.NaN, 4));
        Assert.True(KeepDistanceMath.Stands(1.0, 3.1, double.NaN, 4)); // the enemy came in - he STANDS and defends (never backs again at once)
        Assert.True(KeepDistanceMath.Stands(4.1, 3.1, double.NaN, 4));
        Assert.False(KeepDistanceMath.Stands(4.2, 3.1, double.NaN, 4)); // the enemy walked off - his own AI may close to the gap
        Assert.True(KeepDistanceMath.Stands(3.0, 3.1, 4.5, 4));
        Assert.False(KeepDistanceMath.Stands(3.0, 3.1, 4.6, 4)); // his line moved on - he keeps up with it
        Assert.False(KeepDistanceMath.Stands(double.NaN, 3.1, double.NaN, 4));
        Assert.True(KeepDistanceMath.SteppedIn(2.0, 3.0));
        Assert.False(KeepDistanceMath.SteppedIn(2.3, 3.0)); // a shuffle is not a step in
        Assert.False(KeepDistanceMath.SteppedIn(2.0, double.NaN));
    }

    [Fact]
    public void The_verdict_switch_then_rest_then_gates_then_action_then_target_then_ready_then_time()
    {
        var r = On();
        Assert.Equal(KeepEnd.Count, KeepDistanceMath.Verdict(in r, false, true, false, true, true, 5));
        Assert.Equal(KeepEnd.Rest, KeepDistanceMath.Verdict(in r, true, false, true, false, false, 99)); // the rest wins over everything but the switch
        Assert.Equal(KeepEnd.Excluded, KeepDistanceMath.Verdict(in r, false, false, true, false, false, 99));
        Assert.Equal(KeepEnd.Action, KeepDistanceMath.Verdict(in r, false, true, true, false, false, 99));
        Assert.Equal(KeepEnd.Target, KeepDistanceMath.Verdict(in r, false, true, false, false, false, 99));
        Assert.Equal(KeepEnd.Ready, KeepDistanceMath.Verdict(in r, false, true, false, true, false, 99));
        Assert.Equal(KeepEnd.Timeout, KeepDistanceMath.Verdict(in r, false, true, false, true, true, 10.01));
        Assert.Equal(KeepEnd.Count, KeepDistanceMath.Verdict(in r, false, true, false, true, true, 10.0));
        foreach (KeepEnd e in Enum.GetValues(typeof(KeepEnd)))
            if (e != KeepEnd.Count) Assert.False(string.IsNullOrEmpty(KeepDistanceMath.EndName(e)));
        Assert.Equal("rest took over", KeepDistanceMath.EndName(KeepEnd.Rest));
    }

    [Fact]
    public void A_mind_reset_restarts_his_quiet_clock_and_forgets_the_target()
    {
        var m = new KeepMind { Phase = KeepPhase.Holding, Target = new object(), QuietRoll = 2.4, Since = 3, BackSince = 4, Candidate = true };
        Assert.True(m.Keeping);
        m.Reset(20);
        Assert.False(m.Keeping);
        Assert.Null(m.Target);
        Assert.True(double.IsNaN(m.QuietRoll));
        Assert.Equal(20, m.QuietFrom, 9);
        Assert.False(m.Candidate);
    }

    [Fact]
    public void The_stats_and_the_summary_line_and_no_allocation()
    {
        var st = new KeepDistanceStats();
        for (int i = 0; i < 10; i++) st.AddEnd(i < 9 ? KeepEnd.Ready : KeepEnd.Action, 4 + i, 1.0 + 0.2 * i);
        Assert.Equal(9, st.Ends[(int)KeepEnd.Ready]);
        Assert.Equal(1, st.Ends[(int)KeepEnd.Action]);
        Assert.Equal(1.9, st.WalkSum / st.WalkN, 9);
        Assert.InRange(st.WalkQuantile(0.9), 2.6, 2.75); // the 9th of 10 is 2.6 m - its bin's top
        Assert.Equal(2.8, st.WalkQuantile(1.0), 9);
        Assert.Equal(13, st.HeldMax, 9);
        st.AddBack(1.9, false, again: false);
        st.AddBack(4.0, true, again: true);
        Assert.Equal(2, st.BacksOff);
        Assert.Equal(1, st.BacksOffSpear);
        Assert.Equal(1, st.ReBacks);
        st.AddWait(true, true, 4.0);
        st.AddWait(true, true, 5.0);
        st.AddWait(false, false, 1.1);
        st.AddWait(false, false, 30); // beyond the measure - not counted
        Assert.Equal(4.5, st.WaitMean(true, true), 9);
        Assert.Equal(1.1, st.WaitMean(false, false), 9);
        Assert.True(double.IsNaN(st.WaitMean(true, false)));
        string line = st.SummaryLine(On());
        Assert.StartsWith("keep distance (step 109) ON: backed off 2 (spear 1", line);
        Assert.Contains("ready 9,", line);
        Assert.Contains("action 1,", line);
        Assert.Contains("switch 0,", line);
        Assert.Contains("rest took over 0,", line);
        Assert.Contains("ON spear 4.50 m (2 looks)", line);
        Assert.Contains("OFF spear -, other 1.10 m (1 looks)", line);
        Assert.Contains("quiet spells broken by action 0", line);
        Assert.Contains("OFF (KeepDistanceEnabled)", st.SummaryLine(new KeepDistanceRules(true, true, false)));

        var r = On();
        double sink = 0;
        long bytes = NoAllocation.Bytes(() =>
        {
            for (int i = 0; i < 1000; i++)
            {
                double q = KeepDistanceMath.QuietSeconds(in r, i / 1000.0);
                bool go = KeepDistanceMath.Starts(in r, true, false, true, 1.5, KeepDistanceMath.Quiet(i, 0, q));
                double w = KeepDistanceMath.WalkBack(1.5, 1.5 + KeepDistanceMath.BackMetres(in r, 0.9), i % 5, 4);
                sink += q + w + (go ? 1 : 0) + (int)KeepDistanceMath.Verdict(in r, false, true, false, true, i % 2 == 0, i);
                st.AddEnd(KeepEnd.Ready, 1, w);
                st.AddWait(true, false, w);
            }
        });
        Assert.Equal(0, bytes);
        Assert.True(sink > 0);
    }
}

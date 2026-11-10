using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 110 (Toni's training field 2026-11-10; AI_NOTES "Step 110", DESIGN decision 99): (a) a TIRED man who could swing but stands quiet keeps his
/// distance too - the mark, the ready mark under the wound cap, the keep's own memory; (b) the training field's fresh start - the settings, the master
/// switch first, the health jump, the bout's start / end by the men hostile to you, the lines.
/// </summary>
public class Step110Tests
{
    private static KeepDistanceRules Keep(double below = 50, double recover = 15) =>
        new KeepDistanceRules(true, true, true, tiredBelowPercent: below, tiredRecoverPercent: recover);

    private static TrainingFieldRefillRules Train(bool on = true, double jump = 5) => new TrainingFieldRefillRules(true, true, on, jump);

    [Fact]
    public void The_settings_sit_after_their_neighbours_with_their_defaults_and_are_live()
    {
        var all = SettingsSchema.All.ToList();
        Assert.Equal(all.IndexOf(SettingsSchema.KeepDistanceReachMetres) + 1, all.IndexOf(SettingsSchema.KeepDistanceTiredBelowPercent));
        Assert.Equal(all.IndexOf(SettingsSchema.KeepDistanceTiredBelowPercent) + 1, all.IndexOf(SettingsSchema.KeepDistanceTiredRecoverPercent));
        Assert.Equal(all.IndexOf(SettingsSchema.CrouchRefillForAi) + 1, all.IndexOf(SettingsSchema.TrainingFieldRefill));
        Assert.Equal(all.IndexOf(SettingsSchema.TrainingFieldRefill) + 1, all.IndexOf(SettingsSchema.TrainingFieldRefillHealthJumpPercent));
        Assert.Same(SettingsSchema.CourageGroup, SettingsSchema.KeepDistanceTiredBelowPercent.Group);
        Assert.Same(SettingsSchema.RefillGroup, SettingsSchema.TrainingFieldRefill.Group);
        foreach (var p in new[] { SettingsSchema.KeepDistanceTiredBelowPercent, SettingsSchema.KeepDistanceTiredRecoverPercent, SettingsSchema.TrainingFieldRefill,
                     SettingsSchema.TrainingFieldRefillHealthJumpPercent })
        {
            Assert.DoesNotContain(p, SettingsSchema.PlayerPage);
            Assert.Equal(ApplyTiming.Live, p.Timing);
        }
        var s = new TraxSettings();
        var k = KeepDistanceRules.From(s);
        Assert.Equal(50, k.TiredBelowPercent, 9);
        Assert.Equal(15, k.TiredRecoverPercent, 9);
        var t = TrainingFieldRefillRules.From(s);
        Assert.True(t.On); // ON as Toni asked
        Assert.Equal(5, t.HealthJumpPercent, 9);
        s.Set(SettingsSchema.KeepDistanceTiredBelowPercent, 30, SettingSources.Mcm);
        Assert.Equal(30, KeepDistanceRules.From(s).TiredBelowPercent, 9); // live
    }

    [Fact]
    public void A_tired_man_waits_at_or_below_the_mark_and_a_tired_keep_lasts_to_the_ready_mark()
    {
        var r = Keep();
        Assert.True(KeepDistanceMath.TiredWaits(in r, 0.32, 1.0, keepingTired: false));  // Toni's rookie trainer at 32%
        Assert.True(KeepDistanceMath.TiredWaits(in r, 0.50, 1.0, keepingTired: false));  // at the mark
        Assert.False(KeepDistanceMath.TiredWaits(in r, 0.55, 1.0, keepingTired: false)); // above it: does not start
        Assert.True(KeepDistanceMath.TiredWaits(in r, 0.55, 1.0, keepingTired: true));   // a tired keep goes on to 65%
        Assert.False(KeepDistanceMath.TiredWaits(in r, 0.65, 1.0, keepingTired: true));  // ready
        Assert.Equal(0.65, KeepDistanceMath.TiredReadyAt(in r, 1.0), 9);
    }

    [Fact]
    public void The_wound_cap_lowers_the_ready_mark_and_a_man_at_his_cap_never_waits()
    {
        var r = Keep();
        Assert.Equal(0.38, KeepDistanceMath.TiredReadyAt(in r, 0.40), 9); // cap 40% → ready at 38%
        Assert.False(KeepDistanceMath.TiredWaits(in r, 0.39, 0.40, keepingTired: false)); // at his cap: nothing to gather
        Assert.True(KeepDistanceMath.TiredWaits(in r, 0.20, 0.40, keepingTired: false));
        Assert.False(KeepDistanceMath.TiredWaits(in r, 0.385, 0.40, keepingTired: true));
    }

    [Fact]
    public void The_mark_zero_is_step_109_exactly()
    {
        var r = Keep(below: 0);
        Assert.False(KeepDistanceMath.TiredWaits(in r, 0.0, 1.0, keepingTired: false));
        Assert.False(KeepDistanceMath.TiredWaits(in r, 0.1, 1.0, keepingTired: true));
        Assert.DoesNotContain("step 110", r.Describe());
        Assert.Contains("or is tired (his bar at or below 50%", Keep().Describe());
        Assert.False(KeepDistanceMath.TiredWaits(in r, double.NaN, 1.0, false));
    }

    [Fact]
    public void A_waiting_tired_man_is_a_candidate_and_his_verdict_is_ready_only_past_the_mark()
    {
        var r = Keep();
        bool waiting = KeepDistanceMath.CannotAttack(false, false, false) || KeepDistanceMath.TiredWaits(in r, 0.32, 1.0, false);
        Assert.True(KeepDistanceMath.Candidate(in r, eligible: true, resting: false, waiting, enemyDist: 1.2));
        Assert.Equal(KeepEnd.Count, KeepDistanceMath.Verdict(in r, false, true, false, true, KeepDistanceMath.TiredWaits(in r, 0.60, 1.0, true), 4));
        Assert.Equal(KeepEnd.Ready, KeepDistanceMath.Verdict(in r, false, true, false, true, KeepDistanceMath.TiredWaits(in r, 0.66, 1.0, true), 4));
        Assert.Equal(KeepEnd.Timeout, KeepDistanceMath.Verdict(in r, false, true, false, true, true, 10.5));
    }

    [Fact]
    public void The_keep_summary_line_counts_the_tired_keeps_and_the_candidate_looks()
    {
        var st = new KeepDistanceStats();
        st.TiredKeeps = 2;
        st.QuietDone = 3;
        st.CandidateLooks[3] = 40;
        st.CandidateLooks[0] = 5;
        string line = st.SummaryLine(Keep());
        Assert.StartsWith("keep distance (step 109) ON: backed off 0", line);
        Assert.Contains("step 110 - from tiredness alone (he could swing) 2 keeps, waits that ran out 3, candidate looks by why: pause 5, empty stack 0, brace 0, tired only 40", line);
    }

    [Fact]
    public void The_training_refill_gate_puts_the_master_switch_first()
    {
        Assert.Equal("ModEnabled", new TrainingFieldRefillRules(false, false, false).OffBecause);
        Assert.Equal("AthleticsEnabled", new TrainingFieldRefillRules(true, false, true).OffBecause);
        Assert.Equal("TrainingFieldRefill", new TrainingFieldRefillRules(true, true, false).OffBecause);
        Assert.Null(Train().OffBecause);
        Assert.Equal(1, new TrainingFieldRefillRules(true, true, true, 0).HealthJumpPercent, 9);
        Assert.True(TrainingFieldMath.IsController("TrainingFieldMissionController"));
        Assert.False(TrainingFieldMath.IsController("HideoutMissionController"));
        Assert.False(TrainingFieldMath.IsController(null));
    }

    [Fact]
    public void The_watch_sees_a_health_reset_and_a_bout_beginning_and_ending_but_never_on_its_first_reading()
    {
        var r = Train();
        var w = new TrainingFieldWatch();
        Assert.Equal(TrainingRefillWhy.None, w.Observe(0.6, 0, in r));       // the baseline
        Assert.Equal(TrainingRefillWhy.None, w.Observe(0.6, 0, in r));
        Assert.Equal(TrainingRefillWhy.BoutBegan, w.Observe(0.6, 1, in r));  // "Defeat the trainer!" - he turned hostile
        Assert.Equal(TrainingRefillWhy.HealthReset, w.Observe(1.0, 1, in r)); // the controller wrote your full health on
        Assert.Equal(0.6, w.HealthBefore, 9);
        Assert.Equal(TrainingRefillWhy.None, w.Observe(0.7, 1, in r));       // hit: a fall is no reset
        Assert.Equal(TrainingRefillWhy.None, w.Observe(0.72, 1, in r));      // 2% < 5%
        Assert.Equal(TrainingRefillWhy.BoutEnded, w.Observe(0.72, 0, in r)); // the trainers back on your team
        Assert.Equal(TrainingRefillWhy.HealthReset, w.Observe(1.0, 0, in r)); // the reset after the bout
        Assert.Equal(TrainingRefillWhy.None, w.Observe(double.NaN, -1, in r)); // no player this tick
        Assert.Equal(TrainingRefillWhy.None, w.Observe(1.0, 0, in r));
    }

    [Fact]
    public void A_loss_drops_your_health_then_the_reset_refills()
    {
        var r = Train();
        var w = new TrainingFieldWatch();
        w.Observe(0.3, 1, in r);
        Assert.Equal(TrainingRefillWhy.None, w.Observe(0.011, 1, in r));     // the loss: the game writes 1.1 health
        Assert.Equal(TrainingRefillWhy.BoutEnded, w.Observe(0.011, 0, in r));
        Assert.Equal(TrainingRefillWhy.HealthReset, w.Observe(1.0, 0, in r)); // 1.5 s later: full - and so the bar's cap
    }

    [Fact]
    public void The_lines_say_what_happened()
    {
        string on = TrainingFieldRefillStats.MomentLine(TrainingRefillWhy.HealthReset, 120.5, Train(), 0.54, 1.0, 3, 0.2, 1.0, 110, "Training Master 32.0% → 100.0%");
        Assert.Equal("training field (step 110) at 120.5 s: the game reset your health (54% → 100%) - a fresh start for 3 men: YOU 22.0 → 110.0 of 110.0 (swings and sprint full); Training Master 32.0% → 100.0%", on);
        string off = TrainingFieldRefillStats.MomentLine(TrainingRefillWhy.BoutEnded, 5, Train(on: false), double.NaN, double.NaN, 0, double.NaN, double.NaN, double.NaN, "");
        Assert.Equal("training field (step 110) at 5.0 s: the trainer's bout ended (nobody hostile to you any more) - nobody refilled (TrainingFieldRefill is off)", off);
        var st = new TrainingFieldRefillStats();
        st.AddMoment(TrainingRefillWhy.HealthReset);
        st.AddMoment(TrainingRefillWhy.BoutEnded);
        st.Refills = 2;
        st.MenRefilled = 6;
        st.AddYou(0.2);
        st.AddYou(0.4);
        Assert.Equal("training field fresh start (step 110) ON: moments - your health reset 1, bouts began 0, bouts ended 1; fresh starts 2 (men refilled 6, you 2), "
            + "with a switch off 0; your bar before them mean 30%, lowest 20%; released 0 (pauses, step backs, braces, sprints); errors 0", st.SummaryLine(Train()));
    }

    [Fact]
    public void The_watch_allocates_nothing()
    {
        var r = Train();
        var w = new TrainingFieldWatch();
        w.Observe(1, 0, in r);
        long before = System.GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) w.Observe(i % 2 == 0 ? 0.5 : 1.0, i % 3 == 0 ? 0 : 1, in r);
        Assert.Equal(0, System.GC.GetAllocatedBytesForCurrentThread() - before);
    }
}

using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>DESIGN §2's attack RATE (step 5e): the switches read live (the master switch first),
/// the AI's decision values at m, the pace hold's target, the fighting-rhythm cap, the verdict, and
/// the per-mission phase / cycle / hold / guard bookkeeping with its summary text.</summary>
public class AttackRateTests
{
    // ------------------------------------------------------------------ rules

    [Fact]
    public void Rules_read_the_live_switches_and_the_master_switch_turns_everything_off()
    {
        var s = new TraxSettings();
        var r = AttackRateRules.From(s);
        Assert.True(r.AiDecisionsOn);
        Assert.True(r.PaceOn);
        Assert.Null(r.PaceOffBecause);
        Assert.StartsWith("ON - animations x m always", r.Describe());
        Assert.Contains("AI decisions (AttackRateAiDecisions) on: the chance to attack, to riposte and to loose x m, the aim before a shot ÷ m", r.Describe());
        Assert.EndsWith("blocking and the recoil after a block: untouched", r.Describe());

        s.Set(SettingsSchema.AttackRatePaceHold, false, SettingSources.Mcm);
        r = AttackRateRules.From(s);
        Assert.False(r.PaceOn);
        Assert.True(r.AiDecisionsOn);
        Assert.Equal("AttackRatePaceHold", r.PaceOffBecause);
        Assert.Contains("pace hold (AttackRatePaceHold) off", r.Describe());

        s.Set(SettingsSchema.AttackRateAiDecisions, false, SettingSources.Mcm);
        Assert.False(AttackRateRules.From(s).AiDecisionsOn);

        s.Set(SettingsSchema.AttackRatePaceHold, true, SettingSources.Mcm);
        s.Set(SettingsSchema.AttackRateAiDecisions, true, SettingSources.Mcm);
        s.Set(SettingsSchema.AthleticsEnabled, false, SettingSources.Mcm);
        r = AttackRateRules.From(s);
        Assert.False(r.AiDecisionsOn);
        Assert.False(r.PaceOn);
        Assert.Equal("AthleticsEnabled", r.PaceOffBecause);

        s.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
        r = AttackRateRules.From(s);
        Assert.Equal("ModEnabled", r.PaceOffBecause); // the master switch is named first
        Assert.StartsWith("OFF - the whole mod is switched off (ModEnabled)", r.Describe());
    }

    // ------------------------------------------------------------------ the maths

    [Fact]
    public void AI_chances_follow_m_and_waits_follow_one_over_m()
    {
        Assert.Equal(0.072f, AttackRateMath.ScaleChance(0.144f, 0.5f), 6);   // the chance to attack, halved at m 0.5
        Assert.Equal(0.0288f, AttackRateMath.ScaleChance(0.144f, 0.2f), 6);  // no 0.05 floor: five times rarer at m 0.2
        Assert.Equal(1.0f, AttackRateMath.ScaleChance(1.0f, 1f), 6);
        Assert.Equal(1.5f, AttackRateMath.ScaleWait(0.75f, 0.5f), 6);         // the aim before a shot, doubled
        Assert.Equal(0f, AttackRateMath.ScaleWait(0f, 0.2f), 6);              // siege defenders' 0 stays 0
        Assert.Equal(0.01f, AttackRateMath.SafeM(0f), 6);                     // never divide by 0
        Assert.Equal(1f, AttackRateMath.SafeM(float.NaN), 6);
        Assert.Equal(1f, AttackRateMath.SafeM(3f), 6);
    }

    [Fact]
    public void The_target_is_the_fresh_cycle_over_m__one_attack_a_second_becomes_one_every_two()
    {
        Assert.Equal(2.0, AttackRateMath.TargetCycle(1.0, 0.5f), 9);   // Anton's example
        Assert.Equal(5.0, AttackRateMath.TargetCycle(1.0, 0.2f), 6);
        Assert.Equal(1.4, AttackRateMath.TargetCycle(1.4, 1f), 9);
    }

    [Fact]
    public void The_hold_ends_when_the_next_ready_can_start_and_still_meet_the_target()
    {
        // fresh cycle 1.2 s, m 0.5 → the next release no sooner than 2.4 s after this one (at 10.0 s);
        // his ready takes 0.3 s at m 1, so 0.6 s at m 0.5 → the ready may begin at 11.8 s
        double ready = AttackRateMath.ExpectedReady(0.3, 1f, 0.5f);
        Assert.Equal(0.6, ready, 6);
        Assert.Equal(11.8, AttackRateMath.HoldUntil(10.0, 1.2, 0.5f, ready), 6);
        Assert.True(AttackRateMath.HoldNeeded(11.8, 10.9));      // the swing ended at 10.9: hold 0.9 s
        Assert.False(AttackRateMath.HoldNeeded(11.8, 11.75));    // 0.05 s: not worth two flag writes
        Assert.Equal(0, AttackRateMath.ExpectedReady(-1, 1f, 0.5f), 9); // no ready seen yet
        Assert.Equal(0.4, AttackRateMath.ExpectedReady(0.8, 0.5f, 1f), 6); // a ready struck at m 0.5, now fresh again
    }

    [Fact]
    public void The_fresh_reference_is_his_own_else_the_missions_after_five_samples()
    {
        Assert.Equal(1.5, AttackRateMath.FreshReference(3.0, 2, 1.2, 40, out bool own), 9);
        Assert.True(own);
        Assert.Equal(1.2, AttackRateMath.FreshReference(0, 0, 1.2, 5, out own), 9);
        Assert.False(own);
        Assert.True(double.IsNaN(AttackRateMath.FreshReference(0, 0, 1.2, 4, out _)));
    }

    [Fact]
    public void Only_a_fighting_rhythm_counts__the_cap_grows_with_one_over_m()
    {
        Assert.True(AttackRateMath.Countable(AttackKind.Melee, 3.9, 1.0));
        Assert.False(AttackRateMath.Countable(AttackKind.Melee, 4.1, 1.0));
        Assert.True(AttackRateMath.Countable(AttackKind.Melee, 19.0, 0.2));   // 4 s ÷ 0.2
        Assert.True(AttackRateMath.Countable(AttackKind.Ranged, 11.0, 1.0));  // a crossbow reloads for seconds
        Assert.False(AttackRateMath.Countable(AttackKind.Ranged, 13.0, 1.0));
        Assert.False(AttackRateMath.Countable(AttackKind.Melee, -0.1, 1.0));
    }

    [Fact]
    public void The_verdict_is_on_target_within_15_percent()
    {
        Assert.Equal("on target", AttackRateMath.Verdict(1.0));
        Assert.Equal("on target", AttackRateMath.Verdict(0.86));
        Assert.Equal("on target", AttackRateMath.Verdict(1.14));
        Assert.Equal("too fast", AttackRateMath.Verdict(0.84));
        Assert.Equal("too slow", AttackRateMath.Verdict(1.16));
        Assert.Equal("no verdict", AttackRateMath.Verdict(double.NaN));
    }

    // ------------------------------------------------------------------ the stats

    /// <summary>A melee AI group whose peak cycles average 1.2 s; the tired bands at the given
    /// cycle ÷ the ideal target.</summary>
    private static AttackRateStats Melee(double halfBandFactor, double emptyFactor)
    {
        var s = new AttackRateStats();
        for (int i = 0; i < 10; i++)
        {
            s.AddCycle(AttackKind.Melee, false, 0, i % 2 == 0 ? 1.1 : 1.3, 1f);
            s.AddPhase(AttackKind.Melee, false, 0, AttackPhase.WindUp, 0.3, 1f);
            s.AddPhase(AttackKind.Melee, false, 0, AttackPhase.Release, 0.5, 1f);
            s.AddPhase(AttackKind.Melee, false, 0, AttackPhase.Pause, 0.4, 1f);
        }
        for (int i = 0; i < 6; i++)
        {
            s.AddCycle(AttackKind.Melee, false, 1, 1.2 / 0.8 * halfBandFactor, 0.8f);
            s.AddPhase(AttackKind.Melee, false, 1, AttackPhase.WindUp, 0.3 / 0.8, 0.8f);
            s.AddPhase(AttackKind.Melee, false, 1, AttackPhase.Release, 0.5 / 0.8, 0.8f);
        }
        for (int i = 0; i < 4; i++) s.AddCycle(AttackKind.Melee, false, 3, 1.2 / 0.2 * emptyFactor, 0.2f);
        return s;
    }

    [Fact]
    public void Bands_are_judged_against_the_peaks_cycle_times_their_average_one_over_m()
    {
        var s = Melee(1.0, 1.0);
        Assert.Equal(1.2, s.FreshCycle(AttackKind.Melee, false), 9);
        Assert.Equal(1.5, s.TargetCycle(AttackKind.Melee, false, 1), 6);
        Assert.Equal(1.0, s.Ratio(AttackKind.Melee, false, 1), 6);
        Assert.Equal(6.0, s.TargetCycle(AttackKind.Melee, false, 3), 5);
        Assert.True(double.IsNaN(s.Ratio(AttackKind.Melee, false, 2)));           // no cycles there
        Assert.True(double.IsNaN(s.FreshCycle(AttackKind.Melee, true)));          // nothing for the player

        var lines = s.SummaryLines(new AttackRateRules(true, true, true, true), switchedDuringBattle: false);
        Assert.Contains("attack rate, melee, AI, peak (f 1): wind-up 0.30 + held -, swing 0.50 (clean, hit nothing -), recoil after a block -, pause 0.40 | cycle 1.20 s (n 10), m 1.00 - the fresh reference", lines);
        Assert.Contains("attack rate, melee, AI, f 0.5-1: wind-up 0.38 (x1.25) + held -, swing 0.63 (x1.25) (clean, hit nothing -), recoil after a block -, pause - | cycle 1.50 s (n 6), m 0.80 → target 1.50 s: 100% - on target", lines);
        Assert.Contains(lines, l => l.StartsWith("attack rate, melee, AI, empty (f 0): wind-up -", StringComparison.Ordinal) && l.EndsWith("→ target 6.00 s: 100% - on target", StringComparison.Ordinal));
        Assert.Contains("attack rate, melee, AI - verdict: ON TARGET in 2 of 2 tired bands (fresh cycle 1.20 s; f 0.5-1 100% on target, empty (f 0) 100% on target)", lines);
        Assert.Contains("attack rate, melee, you: no attacks measured", lines);
        Assert.Contains("attack rate, ranged, AI: no attacks measured", lines);
    }

    [Fact]
    public void Too_fast_and_too_slow_are_said_plainly()
    {
        // the animations alone (the AI's pause unchanged): cycles only 70% of the target when empty
        var lines = Melee(1.0, 0.7).SummaryLines(new AttackRateRules(true, true, true, false), false);
        Assert.Contains(lines, l => l.StartsWith("attack rate, melee, AI, empty (f 0):", StringComparison.Ordinal) && l.EndsWith(": 70% - too fast", StringComparison.Ordinal));
        Assert.Contains("attack rate, melee, AI - verdict: OFF TARGET in 1 of 2 tired bands (fresh cycle 1.20 s; f 0.5-1 100% on target, empty (f 0) 70% too fast)", lines);

        var slow = Melee(1.3, 1.0).SummaryLines(new AttackRateRules(true, true, true, true), false);
        Assert.Contains(slow, l => l.StartsWith("attack rate, melee, AI, f 0.5-1:", StringComparison.Ordinal) && l.EndsWith(": 130% - too slow", StringComparison.Ordinal));
    }

    [Fact]
    public void A_group_needs_five_cycles_at_the_peak_and_a_band_three()
    {
        var s = new AttackRateStats();
        for (int i = 0; i < 4; i++) s.AddCycle(AttackKind.Ranged, false, 0, 3.0, 1f);
        for (int i = 0; i < 2; i++) s.AddCycle(AttackKind.Ranged, false, 2, 9.0, 0.35f);
        s.AddPhase(AttackKind.Ranged, false, 2, AttackPhase.Reload, 1.2, 0.35f);
        var lines = s.SummaryLines(new AttackRateRules(true, true, true, true), false);
        Assert.Contains("attack rate, ranged, AI, peak (f 1): draw - + aim -, loose -, reload -, pause - | cycle 3.00 s (n 4), m 1.00 - the fresh reference needs 5 cycles at the peak", lines);
        Assert.Contains(lines, l => l.StartsWith("attack rate, ranged, AI, f below 0.5: draw - + aim -, loose -, reload 1.20, pause -", StringComparison.Ordinal)
                                    && l.EndsWith(" - no fresh reference, no verdict", StringComparison.Ordinal));
        Assert.Contains("attack rate, ranged, AI - verdict: no fresh reference (4 cycles at the peak, need 5)", lines);

        s.AddCycle(AttackKind.Ranged, false, 0, 3.0, 1f);
        lines = s.SummaryLines(new AttackRateRules(true, true, true, true), false);
        Assert.Contains(lines, l => l.StartsWith("attack rate, ranged, AI, f below 0.5:", StringComparison.Ordinal)
                                    && l.EndsWith("→ target 8.57 s - too few cycles for a verdict (need 3)", StringComparison.Ordinal));
        Assert.Contains("attack rate, ranged, AI - verdict: fresh cycle 3.00 s, no tired band with 3 cycles yet", lines);
    }

    [Fact]
    public void Pauses_and_cycles_beyond_the_cap_are_left_out_and_counted()
    {
        var s = new AttackRateStats();
        Assert.False(s.AddCycle(AttackKind.Melee, true, 0, 9.0, 1f));        // walked to the next enemy
        Assert.True(s.AddCycle(AttackKind.Melee, true, 3, 9.0, 0.2f));        // the same 9 s is a rhythm at m 0.2
        s.AddPhase(AttackKind.Melee, true, 0, AttackPhase.Pause, 20.0, 1f);
        s.AddMixed(AttackKind.Melee, true);
        s.AddReadyCancelled(AttackKind.Ranged, false);
        s.AddChained(AttackKind.Melee, false);
        s.AddSteppedBack(AttackKind.Melee, false);
        Assert.Equal(2, s.LeftOut[AttackRateStats.Group(AttackKind.Melee, true)]);
        var lines = s.SummaryLines(new AttackRateRules(true, true, true, true), true);
        Assert.EndsWith(" - an attack-rate switch CHANGED during this battle: the rows below mix both settings", lines[0]);
        Assert.Contains("attack rate - left out: cycles whose two ends fell in different f bands (melee AI / you, ranged AI / you) 0 / 1, 0 / 0; longer than 4 s ÷ m melee or 12 s ÷ m ranged (a pause, not a fighting rhythm) 0 / 2, 0 / 0; readies that ended in no attack (cancelled, feints) 0 / 0, 1 / 0; chained (the next ready straight out of the last attack, pause 0) 1 / 0, 0 / 0; with a step back in them (its pause, not the attack rhythm) 1 / 0, 0 / 0", lines);
    }

    [Fact]
    public void Pace_holds_their_reasons_and_the_guard_by_f_are_summarised()
    {
        var s = new AttackRateStats();
        s.AddHoldStart(1, 0.8f);
        s.AddHoldStart(3, 0.2f);
        s.AddHoldEnd(PaceEnd.TimeUp, 0.5);
        s.AddHoldEnd(PaceEnd.SwingStarted, 1.5);
        s.AddRelease(PaceRelease.ClearedByUs);
        s.AddRelease(PaceRelease.Waiting);
        s.ClearedAfterWaiting = 1;
        s.AddNotHeld(PaceNotHeld.FullStrength);
        s.AddNotHeld(PaceNotHeld.NotNeeded);
        s.AddRefused(PaceRefusal.Busy);
        s.AddNextReadyAfterHold(0.2);
        s.AddNextReadyAfterHold(0.4);
        s.AddNextReadyAfterHold(30);   // not a rhythm: left out
        s.AddAiScaled();
        s.AddAiScaled();
        for (int i = 0; i < 10; i++) s.AddHitTaken(0, held: false, blocked: i < 4);
        for (int i = 0; i < 4; i++) s.AddHitTaken(3, held: true, blocked: i < 2);
        Assert.Equal(2, s.Holds);
        Assert.Equal(1.0, s.HoldMeanSeconds, 9);

        var lines = s.SummaryLines(new AttackRateRules(true, true, true, true), false);
        Assert.Contains("attack rate - AI decisions (AttackRateAiDecisions on at the end): scaled in 2 recomputes - the chance to attack and to riposte x m, to loose x m, the aim before a shot ÷ m; the AI rows' pause and aim show whether the native AI follows them", lines);
        Assert.Contains("attack rate - pace hold (AttackRatePaceHold on at the end; tired AI fighters on foot, after a melee swing): 2 holds, avg 1.00 s, max 1.50 s at avg m 0.50; by f: f 0.5-1 1, f below 0.5 0, empty (f 0) 1", lines);
        Assert.Contains(lines, l => l.StartsWith("attack rate - pace hold, not held: at full strength 1, not needed (his swing and next ready already fill the target) 1,", StringComparison.Ordinal)
                                    && l.Contains("| not started by the tick: busy with a game job (scripted, an object, a ladder, detached, mounted) 1,"));
        Assert.Contains(lines, l => l.StartsWith("attack rate - pace hold ends: time up 1, a swing started anyway 1 (must be about 0 - NoAttack holds swings),", StringComparison.Ordinal)
                                    && l.Contains("| NoAttack cleared by us 1, already cleared by the game 0, a game job on him at the end (left alone, cleared once free: 1) 1, still held at mission end 0")
                                    && l.EndsWith("| the next ready came avg 0.30 s after a hold ended (n 2) - near 0 = the hold set his rhythm", StringComparison.Ordinal));
        Assert.Contains("attack rate - guard by f (melee hits on fighters on foot that were blocked or parried; blocking is never slowed - tired men must not block less): peak (f 1) 40% (n 10) | f 0.5-1 n/a (n 0) | f below 0.5 n/a (n 0) | empty (f 0) 50% (n 4) | while held by the pace hold 50% (n 4)", lines);
    }

    [Fact]
    public void Off_by_the_master_switch_the_summary_says_so_first()
    {
        var lines = new AttackRateStats().SummaryLines(new AttackRateRules(false, true, true, true), false);
        Assert.Equal("attack rate settings at the end (DESIGN §2 - the whole cycle follows the attack speed m): OFF - the whole mod is switched off (ModEnabled): vanilla attack rate", lines[0]);
    }
}

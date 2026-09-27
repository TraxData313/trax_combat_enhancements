using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>DESIGN §2's attack RATE (step 5e; step 13 PAUSE ONLY): the switches read live (the master
/// switch first), the AI's decision values at m, the fighting-rhythm cap, the verdict, and the
/// per-mission phase / cycle / timer / hold / guard bookkeeping with its summary text.</summary>
public class AttackRateTests
{
    // ------------------------------------------------------------------ rules

    [Fact]
    public void Rules_read_the_live_switches_and_the_master_switch_turns_everything_off()
    {
        var s = new TraxSettings();
        var r = AttackRateRules.From(s);
        Assert.False(r.AiDecisionsOn);   // step 13: off by default - on top of the timer it double-counts
        Assert.True(r.PaceOn);
        Assert.True(r.PlayerTimerOn);
        Assert.Equal(100, r.AnimationMinPercent);
        Assert.Null(r.PaceOffBecause);
        Assert.Null(r.PlayerTimerOffBecause);
        Assert.StartsWith("ON - PAUSE ONLY: animations at full speed (AttackAnimationMinPercent 100); after each attack no new attack for D x (1/m - 1)", r.Describe());
        Assert.Contains("you (AttackRatePlayerTimer) on - your attack button does nothing until it ends, held it attacks the moment it ends", r.Describe());
        Assert.Contains("AI (AttackRatePaceHold) on - NoAttack, melee and ranged, on foot and mounted", r.Describe());
        Assert.Contains("AI decisions (AttackRateAiDecisions) off", r.Describe());
        Assert.EndsWith("never held: blocking, parrying, moving, weapon switches, kicks", r.Describe());

        s.Set(SettingsSchema.AttackRatePaceHold, false, SettingSources.Mcm);
        s.Set(SettingsSchema.AttackRateAiDecisions, true, SettingSources.Mcm);
        s.Set(SettingsSchema.AttackAnimationMinPercent, 60, SettingSources.Mcm);
        r = AttackRateRules.From(s);
        Assert.False(r.PaceOn);
        Assert.True(r.AiDecisionsOn);
        Assert.Equal("AttackRatePaceHold", r.PaceOffBecause);
        Assert.Contains("AI (AttackRatePaceHold) off", r.Describe());
        Assert.Contains("animations x max(m, 0.60) (AttackAnimationMinPercent 60 - a little slow-mo)", r.Describe());
        Assert.Contains("AI decisions (AttackRateAiDecisions) on: the chance to attack, to riposte and to loose x m, the aim before a shot ÷ m", r.Describe());

        s.Set(SettingsSchema.AttackRatePlayerTimer, false, SettingSources.Mcm);
        r = AttackRateRules.From(s);
        Assert.False(r.PlayerTimerOn);
        Assert.Equal("AttackRatePlayerTimer", r.PlayerTimerOffBecause);
        Assert.Contains("you (AttackRatePlayerTimer) off", r.Describe());

        s.Set(SettingsSchema.AttackRatePaceHold, true, SettingSources.Mcm);
        s.Set(SettingsSchema.AttackRatePlayerTimer, true, SettingSources.Mcm);
        s.Set(SettingsSchema.AthleticsEnabled, false, SettingSources.Mcm);
        r = AttackRateRules.From(s);
        Assert.False(r.AiDecisionsOn);
        Assert.False(r.PaceOn);
        Assert.False(r.PlayerTimerOn);
        Assert.Equal("AthleticsEnabled", r.PaceOffBecause);
        Assert.Equal("AthleticsEnabled", r.PlayerTimerOffBecause);

        s.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
        r = AttackRateRules.From(s);
        Assert.Equal("ModEnabled", r.PaceOffBecause); // the master switch is named first
        Assert.Equal("ModEnabled", r.PlayerTimerOffBecause);
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
            s.AddAnimation(AttackKind.Melee, false, 0, 1f);
        }
        for (int i = 0; i < 6; i++)
        {
            s.AddCycle(AttackKind.Melee, false, 1, 1.2 / 0.8 * halfBandFactor, 0.8f);
            s.AddPhase(AttackKind.Melee, false, 1, AttackPhase.WindUp, 0.3, 0.8f);  // step 13: full-speed animations
            s.AddPhase(AttackKind.Melee, false, 1, AttackPhase.Release, 0.5, 0.8f);
            s.AddAnimation(AttackKind.Melee, false, 1, 1f);
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
        Assert.Equal(1.0, s.AnimationMean(AttackKind.Melee, false, 1), 9);

        var lines = s.SummaryLines(new AttackRateRules(true, true, false, true), switchedDuringBattle: false);
        Assert.Contains("attack rate, melee, AI, peak (f 1): animations asked x1.00 - wind-up 0.30 + held -, swing 0.50 (clean, hit nothing -), recoil after a block -, pause 0.40 | cycle 1.20 s (n 10), m 1.00 - the fresh reference", lines);
        Assert.Contains("attack rate, melee, AI, f 0.5-1: animations asked x1.00 - wind-up 0.30 (x1.00) + held -, swing 0.50 (x1.00) (clean, hit nothing -), recoil after a block -, pause - | cycle 1.50 s (n 6), m 0.80 → target 1.50 s: 100% - on target", lines);
        Assert.Contains(lines, l => l.StartsWith("attack rate, melee, AI, empty (f 0): animations asked - - wind-up -", StringComparison.Ordinal) && l.EndsWith("→ target 6.00 s: 100% - on target", StringComparison.Ordinal));
        Assert.Contains("attack rate, melee, AI - verdict: ON TARGET in 2 of 2 tired bands (fresh cycle 1.20 s; f 0.5-1 100% on target, empty (f 0) 100% on target)", lines);
        Assert.Contains("attack rate, melee, you: no attacks measured", lines);
        Assert.Contains("attack rate, ranged, AI: no attacks measured", lines);
    }

    [Fact]
    public void Too_fast_and_too_slow_are_said_plainly()
    {
        var lines = Melee(1.0, 0.7).SummaryLines(new AttackRateRules(true, true, false, false), false);
        Assert.Contains(lines, l => l.StartsWith("attack rate, melee, AI, empty (f 0):", StringComparison.Ordinal) && l.EndsWith(": 70% - too fast", StringComparison.Ordinal));
        Assert.Contains("attack rate, melee, AI - verdict: OFF TARGET in 1 of 2 tired bands (fresh cycle 1.20 s; f 0.5-1 100% on target, empty (f 0) 70% too fast)", lines);

        var slow = Melee(1.3, 1.0).SummaryLines(new AttackRateRules(true, true, false, true), false);
        Assert.Contains(slow, l => l.StartsWith("attack rate, melee, AI, f 0.5-1:", StringComparison.Ordinal) && l.EndsWith(": 130% - too slow", StringComparison.Ordinal));
    }

    [Fact]
    public void A_group_needs_five_cycles_at_the_peak_and_a_band_three()
    {
        var s = new AttackRateStats();
        for (int i = 0; i < 4; i++) s.AddCycle(AttackKind.Ranged, false, 0, 3.0, 1f);
        for (int i = 0; i < 2; i++) s.AddCycle(AttackKind.Ranged, false, 2, 9.0, 0.35f);
        s.AddPhase(AttackKind.Ranged, false, 2, AttackPhase.Reload, 1.2, 0.35f);
        var lines = s.SummaryLines(new AttackRateRules(true, true, false, true), false);
        Assert.Contains("attack rate, ranged, AI, peak (f 1): animations asked - - draw - + aim -, loose -, reload -, pause - | cycle 3.00 s (n 4), m 1.00 - the fresh reference needs 5 cycles at the peak", lines);
        Assert.Contains(lines, l => l.StartsWith("attack rate, ranged, AI, f below 0.5: animations asked - - draw - + aim -, loose -, reload 1.20, pause -", StringComparison.Ordinal)
                                    && l.EndsWith(" - no fresh reference, no verdict", StringComparison.Ordinal));
        Assert.Contains("attack rate, ranged, AI - verdict: no fresh reference (4 cycles at the peak, need 5)", lines);

        s.AddCycle(AttackKind.Ranged, false, 0, 3.0, 1f);
        lines = s.SummaryLines(new AttackRateRules(true, true, false, true), false);
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
        var lines = s.SummaryLines(new AttackRateRules(true, true, false, true), true);
        Assert.EndsWith(" - an attack-rate switch CHANGED during this battle: the rows below mix both settings", lines[0]);
        Assert.Contains("attack rate - left out: cycles whose two ends fell in different f bands (melee AI / you, ranged AI / you) 0 / 1, 0 / 0; longer than 4 s ÷ m melee or 12 s ÷ m ranged (a pause, not a fighting rhythm) 0 / 2, 0 / 0; readies that ended in no attack (cancelled, feints) 0 / 0, 1 / 0; chained (the next ready straight out of the last attack, pause 0) 1 / 0, 0 / 0; with a step back in them (its pause, not the attack rhythm) 1 / 0, 0 / 0", lines);
    }

    [Fact]
    public void Each_timer_is_measured_against_the_gap_it_left__an_attack_inside_it_is_early()
    {
        var s = new AttackRateStats();
        // your melee in band f 0.5-1: D 0.8 s at m 0.5 → 0.8 s asked; the next attacks 0.82 and 1.0 s later
        s.AddTimer(AttackKind.Melee, true, 1, 0.8, 0.5f, 0.8);
        Assert.False(s.AddNextAttackAfterTimer(AttackKind.Melee, true, 1, 0.82, 0.8, 0.5f));
        s.AddTimer(AttackKind.Melee, true, 1, 0.8, 0.5f, 0.8);
        Assert.False(s.AddNextAttackAfterTimer(AttackKind.Melee, true, 1, 1.0, 0.8, 0.5f));
        // one inside its timer: early (must be 0 in game), still no crash on a lull beyond the cap
        s.AddTimer(AttackKind.Melee, true, 1, 0.8, 0.5f, 0.8);
        Assert.True(s.AddNextAttackAfterTimer(AttackKind.Melee, true, 1, 0.5, 0.8, 0.5f));
        Assert.False(s.AddNextAttackAfterTimer(AttackKind.Melee, true, 1, 30.0, 0.8, 0.5f));  // a lull: left out of the gap
        Assert.Equal(3, s.Timers(AttackKind.Melee, true, 1));
        Assert.Equal(0.8, s.TimerAskedMean(AttackKind.Melee, true, 1), 9);
        Assert.Equal(3, s.GapCount(AttackKind.Melee, true, 1));
        Assert.Equal(1, s.StartedEarly(AttackKind.Melee, true));
        Assert.Equal(0.05, AttackRateStats.EarlyToleranceSeconds, 9);   // a frame or two of slack

        s.SwallowedInAttack = 2;
        s.SwallowedInTimer = 5;
        s.Flashes = 4;
        s.PlayerEarlyHolds = 3;
        s.PlayerEarlyHoldsTooShort = 1;
        s.AddFiredHeld(0.02);
        s.AddFiredHeld(0.04);
        s.AddPlayerEnd(PlayerTimerEnd.SwitchedOff);
        s.PlayerHeldAtMissionEnd = 1;
        s.AddPlayerEnd(PlayerTimerEnd.MissionEnd);
        var lines = s.SummaryLines(new AttackRateRules(true, true, false, true), false);
        Assert.Contains("attack rate, melee, you, f 0.5-1 - timer: 3 (D avg 0.80 s at m 0.50 → asked avg 0.80 s = D x (1/m - 1)); measured: the next attack began avg 0.77 s after the attack's end (n 3), 0.07 s after the timer ended; started before the timer ended: 1 (must be 0)", lines);
        Assert.Contains("attack rate - your timer (AttackRatePlayerTimer on at the end): 3 timers (melee 3, ranged 0), avg asked 0.80 s, max 0.80 s; your presses swallowed: 2 during your own attack (no chained blow), 5 during the countdown - the recovery bar flashed 4x; the button held through the end 2x, your attack began avg 0.03 s after (n 2) - near 0 = hold-to-attack works; attacks that started while held anyway: 0 (must be 0 - the input gate missed them); holds begun at your swing's start 3 (ended with no countdown, below 0.1 s: 1); ended early: switched off 1, not you any more 0, mission end 1 (still running at the end, released: 1)", lines);
    }

    [Fact]
    public void AI_timers_their_reasons_and_the_guard_by_f_are_summarised()
    {
        var s = new AttackRateStats();
        s.AddHoldStart(1, 0.8f, AttackKind.Melee);
        s.AddHoldStart(3, 0.2f, AttackKind.Ranged, mounted: true);
        s.AddHoldEnd(PaceEnd.TimeUp, 0.5);
        s.AddHoldEnd(PaceEnd.AttackStarted, 1.5);
        s.AddRelease(PaceRelease.ClearedByUs);
        s.AddRelease(PaceRelease.Waiting);
        s.ClearedAfterWaiting = 1;
        s.AddNotHeld(PaceNotHeld.FullStrength);
        s.AddNotHeld(PaceNotHeld.NotNeeded);
        s.AddNotHeld(PaceNotHeld.NoDuration);
        s.AddRefused(PaceRefusal.Busy);
        s.AddNextReadyAfterHold(0.2);
        s.AddNextReadyAfterHold(0.4);
        s.AddNextReadyAfterHold(30);   // not a rhythm: left out
        s.AddAiScaled();
        s.AddAiScaled();
        for (int i = 0; i < 10; i++) s.AddHitTaken(0, held: false, blocked: i < 4);
        for (int i = 0; i < 4; i++) s.AddHitTaken(3, held: true, blocked: i < 2);
        Assert.Equal(2, s.Holds);
        Assert.Equal(1, s.HoldsOf(AttackKind.Ranged));
        Assert.Equal(1, s.HoldsMounted);
        Assert.Equal(1.0, s.HoldMeanSeconds, 9);

        var lines = s.SummaryLines(new AttackRateRules(true, true, false, true), false);
        Assert.Contains("attack rate - AI decisions (AttackRateAiDecisions off at the end): scaled in 2 recomputes - the chance to attack and to riposte x m, to loose x m, the aim before a shot ÷ m (off by default since step 13: on top of the timer it double-counts)", lines);
        Assert.Contains("attack rate - AI timer (AttackRatePaceHold on at the end; NoAttack after each attack of a tired AI fighter, melee and ranged, on foot and mounted): 2 holds (melee 1, ranged 1, mounted 1), avg 1.00 s, max 1.50 s at avg m 0.50; by f: f 0.5-1 1, f below 0.5 0, empty (f 0) 1", lines);
        Assert.Contains(lines, l => l.StartsWith("attack rate - AI timer, not held: at full strength 1, not needed (below 0.1 s) 1, the next attack already readied at the attack's end 0, the attack's length not measured 1,", StringComparison.Ordinal)
                                    && l.Contains("| not started by the tick: busy with a game job (scripted, an object, a ladder, detached) 1,"));
        Assert.Contains(lines, l => l.StartsWith("attack rate - AI timer ends: time up 1, an attack started anyway 1 (must be about 0 - NoAttack holds attacks),", StringComparison.Ordinal)
                                    && l.Contains("| NoAttack cleared by us 1, already cleared by the game 0, a game job on him at the end (left alone, cleared once free: 1, of them under a long scripted frame: 0) 1, still held at mission end 0")
                                    && l.EndsWith("| the next ready came avg 0.30 s after a hold ended (n 2) - the AI's own re-decision after NoAttack lifts", StringComparison.Ordinal));
        Assert.Contains("attack rate - guard by f (melee hits on fighters on foot that were blocked or parried; blocking is never slowed - tired men must not block less): peak (f 1) 40% (n 10) | f 0.5-1 n/a (n 0) | f below 0.5 n/a (n 0) | empty (f 0) 50% (n 4) | while held by the AI timer 50% (n 4)", lines);
    }

    [Fact]
    public void Off_by_the_master_switch_the_summary_says_so_first()
    {
        var lines = new AttackRateStats().SummaryLines(new AttackRateRules(false, true, true, true), false);
        Assert.Equal("attack rate settings at the end (DESIGN §2 - the attack RATE follows the attack speed m): OFF - the whole mod is switched off (ModEnabled): vanilla attack rate", lines[0]);
    }
}

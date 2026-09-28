using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>Step 13 - PAUSE ONLY (DESIGN §2): the no-attack timer's maths (D × (1/m − 1), the animation
/// floor, the countdown text, the flash), YOUR timer's state machine as the input gate drives it (the
/// hold from the release's start, the countdown, swallowed presses, hold-to-attack, releases) and what
/// the Attack recovery bar reads from it.</summary>
public class AttackTimerTests
{
    // ------------------------------------------------------------------ the maths

    [Fact]
    public void The_pause_is_D_times_one_over_m_minus_one__the_rate_is_exactly_x_m()
    {
        Assert.Equal(1.0, AttackTimerMath.Pause(1.0, 0.5f), 6);    // Anton's example: 1 s attacks at m 0.5 → one every 2 s
        Assert.Equal(4.0, AttackTimerMath.Pause(1.0, 0.2f), 5);    // empty (m 0.2): four attack lengths
        Assert.Equal(0.25, AttackTimerMath.Pause(1.0, 0.8f), 5);
        foreach (float m in new[] { 0.2f, 0.37f, 0.5f, 0.9f })
            Assert.Equal(0.84 / m, 0.84 + AttackTimerMath.Pause(0.84, m), 5);   // D + pause = D ÷ m
        Assert.Equal(0, AttackTimerMath.Pause(1.0, 1f), 9);        // full strength: no pause
        Assert.Equal(0, AttackTimerMath.Pause(0, 0.5f), 9);        // no duration: no pause
        Assert.Equal(0, AttackTimerMath.Pause(double.NaN, 0.5f), 9);
        Assert.Equal(99.0, AttackTimerMath.Pause(1.0, 0f), 4);     // m 0 is kept at 0.01 - never a division by 0
        Assert.True(AttackTimerMath.Worth(0.1));
        Assert.False(AttackTimerMath.Worth(0.09));                 // below 0.1 s: no timer
    }

    [Fact]
    public void The_animations_follow_a_straight_line_by_f_down_to_the_percent_at_empty()
    {
        // step 20b (Anton's 85): A + (1 − A) × f - full speed at the peak line, 85% empty, 92.5% halfway
        Assert.Equal(1f, AttackTimerMath.AnimationMultiplier(1.0, 85), 6);
        Assert.Equal(0.85f, AttackTimerMath.AnimationMultiplier(0.0, 85), 6);
        Assert.Equal(0.925f, AttackTimerMath.AnimationMultiplier(0.5, 85), 6);
        Assert.Equal(0.8875f, AttackTimerMath.AnimationMultiplier(0.25, 85), 6);
        // not max(m, 0.85): at f 0.5 (m 0.6) the old rule sat at the 0.85 floor already
        Assert.True(AttackTimerMath.AnimationMultiplier(0.9, 85) > AttackTimerMath.AnimationMultiplier(0.5, 85));
        // 100 = full speed always (step 13's PAUSE ONLY), at any f
        foreach (double f in new[] { 0.0, 0.3, 0.5, 0.99, 1.0 })
            Assert.Equal(1f, AttackTimerMath.AnimationMultiplier(f, 100), 6);
        // 0 = the line is f itself (the pure maths; the setting stops at 5, so a swing never freezes)
        foreach (double f in new[] { 0.1, 0.4, 0.75 })
            Assert.Equal((float)f, AttackTimerMath.AnimationMultiplier(f, 0), 6);
        Assert.Equal(5, SettingsSchema.AttackAnimationMinPercent.Min);
        Assert.Equal(0.05f, AttackTimerMath.AnimationMultiplier(0.0, (int)SettingsSchema.AttackAnimationMinPercent.Min), 6);
        // above the peak line, NaN, out-of-range percents: safe
        Assert.Equal(1f, AttackTimerMath.AnimationMultiplier(1.7, 50), 6);
        Assert.Equal(1f, AttackTimerMath.AnimationMultiplier(double.NaN, 50), 6);
        Assert.Equal(0.85f, AttackTimerMath.AnimationMultiplier(-0.2, 85), 6);
        Assert.Equal(1f, AttackTimerMath.AnimationMultiplier(0.0, 140), 6);
        // monotonic in f, never above 1
        float last = 0f;
        for (int i = 0; i <= 100; i++)
        {
            float a = AttackTimerMath.AnimationMultiplier(i / 100.0, 85);
            Assert.True(a >= last && a <= 1f);
            last = a;
        }
    }

    [Fact]
    public void The_decorator_reads_f_off_the_applied_attack_multiplier_so_the_line_moves_with_m()
    {
        const float s = 0.2f; // ExhaustedAttackSpeedPercent 20 (DESIGN)
        Assert.Equal(1.0, AttackTimerMath.PeakShareOfAttack(1f, s), 9);
        Assert.Equal(0.0, AttackTimerMath.PeakShareOfAttack(s, s), 6);
        Assert.Equal(0.5, AttackTimerMath.PeakShareOfAttack(0.6f, s), 6);
        foreach (double f in new[] { 0.0, 0.125, 0.5, 0.8125, 1.0 })
        {
            float m = AthleticsMath.Curve(s, f);   // the same curve the engine applies
            Assert.Equal(f, AttackTimerMath.PeakShareOfAttack(m, s), 5);
            Assert.Equal(AttackTimerMath.AnimationMultiplier(f, 85), AttackTimerMath.AnimationForAttack(m, s, 85), 5);
        }
        Assert.Equal(0.925f, AttackTimerMath.AnimationForAttack(0.6f, s, 85), 5);
        Assert.Equal(0.85f, AttackTimerMath.AnimationForAttack(0.2f, s, 85), 5);
        Assert.Equal(1f, AttackTimerMath.AnimationForAttack(0.2f, s, 100), 6);   // 100: never slowed
        // attacks never slowed (ExhaustedAttackSpeedPercent 100: m is always 1) → the animations stay at full speed too
        Assert.Equal(1f, AttackTimerMath.AnimationForAttack(1f, AthleticsRules.AttackSpeedFloorOf(100), 85), 6);
        Assert.Equal(1.0, AttackTimerMath.PeakShareOfAttack(0.5f, 1f), 9);
        Assert.Equal(0.0, AttackTimerMath.PeakShareOfAttack(0.1f, s), 9);          // below the floor (a stale m after a live change): clamped
        Assert.Equal(1f, AttackTimerMath.AnimationForAttack(float.NaN, s, 85), 6);
        Assert.Equal(0.05f, AthleticsRules.AttackSpeedFloorOf(5), 6);
        Assert.Equal(0.01f, AthleticsRules.AttackSpeedFloorOf(0), 6);
    }

    [Fact]
    public void D_is_taken_at_full_animation_speed_so_the_pause_in_seconds_does_not_grow_with_a_slower_swing()
    {
        // an empty man (m 0.2) whose 0.84 s attack played at x0.85 took 0.84 / 0.85 s on the clock
        double played = 0.84 / 0.85;
        double d = AttackTimerMath.AtFullSpeed(played, 0.85f);
        Assert.Equal(0.84, d, 6);
        Assert.Equal(AttackTimerMath.Pause(0.84, 0.2f), AttackTimerMath.Pause(d, 0.2f), 6);   // 3.36 s, as with full-speed animations
        Assert.Equal(3.36, AttackTimerMath.Pause(d, 0.2f), 5);
        Assert.Equal(played, AttackTimerMath.AtFullSpeed(played, 1f), 9);                     // full speed: the played seconds
        Assert.Equal(played, AttackTimerMath.AtFullSpeed(played, float.NaN), 9);
        Assert.Equal(played, AttackTimerMath.AtFullSpeed(played, 0f), 9);
    }

    [Fact]
    public void The_countdown_reads_tenths_rounded_up_and_never_zero_while_it_runs()
    {
        Assert.Equal("1.4 s", AttackTimerMath.CountdownText(1.32));
        Assert.Equal("1.3 s", AttackTimerMath.CountdownText(1.3));
        Assert.Equal("0.1 s", AttackTimerMath.CountdownText(0.01));
        Assert.Equal("4.0 s", AttackTimerMath.CountdownText(3.95));
        Assert.Equal(string.Empty, AttackTimerMath.CountdownText(0));
        Assert.Equal(string.Empty, AttackTimerMath.CountdownText(-1));
        Assert.Equal(0.7, AttackTimerMath.Remaining(10.0, 10.7), 9);
        Assert.Equal(0, AttackTimerMath.Remaining(11.0, 10.7), 9);
    }

    [Fact]
    public void The_flash_is_two_quick_pulses()
    {
        Assert.Equal(0.32, AttackTimerMath.FlashSeconds, 9);
        Assert.True(AttackTimerMath.FlashOn(0));
        Assert.True(AttackTimerMath.FlashOn(0.11));
        Assert.False(AttackTimerMath.FlashOn(0.13));   // the gap
        Assert.False(AttackTimerMath.FlashOn(0.19));
        Assert.True(AttackTimerMath.FlashOn(0.21));    // the second pulse
        Assert.True(AttackTimerMath.FlashOn(0.31));
        Assert.False(AttackTimerMath.FlashOn(0.33));   // over
        Assert.False(AttackTimerMath.FlashOn(-0.1));
    }

    // ------------------------------------------------------------------ your timer, as the gate drives it

    [Fact]
    public void A_hold_from_the_release_swallows_presses_then_the_countdown_runs_and_a_held_button_fires_at_its_end()
    {
        var t = new PlayerAttackTimer();
        Assert.False(t.Holding);

        // the release starts at 10.0 s; expected pause 0.4 s: the hold begins at once
        Assert.True(t.AttackStarted(10.0, AttackKind.Melee, 0.4));
        Assert.True(t.InAttack && t.Holding && !t.Running);
        var f = t.Frame(10.02, pressing: false);            // the baseline frame: nothing is a press
        Assert.False(f.Clear || f.Swallowed);
        f = t.Frame(10.2, pressing: true);                  // a click during his own swing: swallowed
        Assert.True(f.Clear && f.Swallowed && f.InAttack && f.FlashStarted);
        f = t.Frame(10.25, pressing: true);                 // still held: cleared, no new press
        Assert.True(f.Clear && !f.Swallowed);

        // the attack ends at 10.5 s: D 0.8 s at m 0.5 → the countdown of 0.8 s
        Assert.True(t.AttackEnded(10.5, AttackKind.Melee, 0.8, 0.5f, out bool early));
        Assert.False(early);
        Assert.True(t.Running && !t.InAttack);
        Assert.Equal(0.8, t.Pause, 6);
        Assert.Equal(11.3, t.TimerEnd, 6);
        Assert.Equal(0.3, t.Remaining(11.0), 6);
        f = t.Frame(11.0, pressing: true);                  // held through: cleared, not a new press
        Assert.True(f.Clear && !f.Swallowed);
        f = t.Frame(11.1, pressing: false);
        f = t.Frame(11.15, pressing: true);                 // a new press in the countdown: swallowed; the last flash is long over - a new one
        Assert.True(f.Swallowed && !f.InAttack && f.FlashStarted);
        Assert.Equal(2, t.SwallowedThisHold);

        f = t.Frame(11.3, pressing: true);                  // the end, button held: NOT cleared - the attack starts now
        Assert.True(f.Ended && f.HeldAtEnd && !f.Clear);
        Assert.False(t.Holding);
        Assert.Equal(11.3, t.EndedAt, 9);
        Assert.True(t.HeldAtEnd);
    }

    [Fact]
    public void A_flash_is_not_restarted_while_one_still_runs()
    {
        var t = new PlayerAttackTimer();
        t.AttackEnded(0, AttackKind.Melee, 1.0, 0.2f, out _);  // 4 s pause, no hold before: baseline on the first frame
        t.Frame(0.01, false);
        Assert.True(t.Frame(0.1, true).FlashStarted);
        t.Frame(0.15, false);
        var f = t.Frame(0.2, true);                             // hammering: swallowed, but the flash keeps its rhythm
        Assert.True(f.Swallowed && !f.FlashStarted);
        t.Frame(0.3, false);
        Assert.True(t.Frame(0.5, true).FlashStarted);           // the first flash is over: a new one
        Assert.True(t.FlashLit(0.55));
        Assert.False(t.FlashLit(0.64));
    }

    [Fact]
    public void A_button_already_held_when_the_hold_begins_is_hold_to_attack_not_a_press()
    {
        var t = new PlayerAttackTimer();
        Assert.True(t.AttackEnded(5.0, AttackKind.Ranged, 2.6, 0.5f, out _));  // a bow: draw + loose + reload
        var f = t.Frame(5.01, pressing: true);             // he keeps the button down for the next draw
        Assert.True(f.Clear && !f.Swallowed);
        Assert.Equal(0, t.SwallowedThisHold);
        f = t.Frame(7.6, pressing: true);
        Assert.True(f.Ended && f.HeldAtEnd && !f.Clear);   // the draw begins the moment it ends
    }

    [Fact]
    public void A_pause_below_the_minimum_starts_nothing_and_ends_an_early_hold()
    {
        var t = new PlayerAttackTimer();
        Assert.False(t.AttackStarted(1.0, AttackKind.Melee, 0.05));  // just below the peak line: no hold, chains allowed
        Assert.False(t.Holding);
        Assert.False(t.AttackEnded(1.5, AttackKind.Melee, 0.8, 0.95f, out bool early)); // 0.042 s: no timer
        Assert.False(early);

        Assert.True(t.AttackStarted(2.0, AttackKind.Melee, 0.12));   // expected 0.12 s…
        Assert.False(t.AttackEnded(2.5, AttackKind.Melee, 0.6, 0.9f, out early)); // …but it came out at 0.067 s
        Assert.True(early);
        Assert.False(t.Holding);
    }

    [Fact]
    public void Release_ends_any_hold_at_once()
    {
        var t = new PlayerAttackTimer();
        t.AttackEnded(0, AttackKind.Melee, 1.0, 0.5f, out _);
        Assert.True(t.Release());
        Assert.False(t.Holding || t.Running);
        Assert.False(t.Release());                          // nothing left
        Assert.False(t.Frame(0.5, true).Clear);             // the gate lets everything through now
    }

    // ------------------------------------------------------------------ the recovery bar's read

    [Fact]
    public void The_recovery_bar_is_empty_during_the_attack_fills_over_the_pause_and_is_full_otherwise()
    {
        var t = new PlayerAttackTimer();
        var full = AttackRecoveryReading.From(t, 0, timerOn: true);
        Assert.Equal(1f, full.Share, 6);
        Assert.False(full.Recovering);
        Assert.Equal(string.Empty, full.SecondsText);

        t.AttackStarted(1.0, AttackKind.Melee, 0.5);
        var swing = AttackRecoveryReading.From(t, 1.2, timerOn: true);
        Assert.Equal(0f, swing.Share, 6);                  // "empties when I attack"
        Assert.True(swing.Recovering && swing.InAttack);
        Assert.Equal(string.Empty, swing.SecondsText);

        t.AttackEnded(1.5, AttackKind.Melee, 0.8, 0.4f, out _);   // 1.2 s pause
        var early = AttackRecoveryReading.From(t, 1.5, timerOn: true);
        Assert.Equal(0f, early.Share, 6);
        Assert.Equal("1.2 s", early.SecondsText);          // "inside it add the secs delay added"
        var half = AttackRecoveryReading.From(t, 2.1, timerOn: true);
        Assert.Equal(0.5f, half.Share, 5);
        Assert.Equal("0.6 s", half.SecondsText);
        Assert.Equal(0.8, half.Duration, 9);
        Assert.Equal(0.4f, half.M, 6);

        t.Frame(2.7, false);                               // over
        var done = AttackRecoveryReading.From(t, 2.7, timerOn: true);
        Assert.Equal(1f, done.Share, 6);
        Assert.False(done.Recovering);

        var off = AttackRecoveryReading.Full(timerOn: false);
        Assert.Equal(1f, off.Share, 6);
        Assert.False(off.TimerOn);
        Assert.True(double.IsNaN(off.FlashStartedAt));
    }
}

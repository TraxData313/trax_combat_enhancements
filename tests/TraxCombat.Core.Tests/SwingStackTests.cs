using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>Step 32 (Anton, 2026-10-12 - "chain swings stack"): every fighter stores max(2, round(points / 20)) swings, an attack
/// spends one and waits only at 0, one comes back per the pause that attack would have left (at his CURRENT m) while he is not
/// attacking; kicks and bashes spend one too.</summary>
public class SwingStackTests
{
    private static SwingStackRules Stack(bool on = true, double per = 20, int min = 2, bool kicks = true, bool mod = true, bool athletics = true) =>
        new(mod, athletics, on, per, min, kicks);

    private static AthleticsRules Athletics() => AthleticsRules.From(OldPool.Settings()); // step 94: the base pinned 0

    // ------------------------------------------------------------------ the cap

    [Fact]
    public void Rounding_is_half_away_from_zero_never_bankers()
    {
        Assert.Equal(8, SwingStackMath.RoundAway(7.5));
        Assert.Equal(3, SwingStackMath.RoundAway(2.5));
        Assert.Equal(1, SwingStackMath.RoundAway(0.5));
        Assert.Equal(4, SwingStackMath.RoundAway(3.75));
        Assert.Equal(3, SwingStackMath.RoundAway(3.49));
    }

    [Fact]
    public void The_cap_is_points_over_20_rounded_never_below_2_even_at_empty()
    {
        Assert.Equal(8, SwingStackMath.Cap(150, 20, 2)); // pool 150 full: 7.5 → 8
        Assert.Equal(4, SwingStackMath.Cap(75, 20, 2));  // half of it: 3.75 → 4
        Assert.Equal(2, SwingStackMath.Cap(0, 20, 2));   // empty: the floor
        Assert.Equal(3, SwingStackMath.Cap(50, 20, 2));  // pool 50 full: 2.5 → 3
        Assert.Equal(2, SwingStackMath.Cap(10, 20, 2));  // 0.5 → 1 → the floor 2
        Assert.Equal(15, SwingStackMath.Cap(300, 20, 2));
        Assert.Equal(1, SwingStackMath.Cap(0, 20, 1));
        Assert.Equal(5, SwingStackMath.Cap(0, 20, 5));
        Assert.Equal(30, SwingStackMath.Cap(150, 5, 2));
        Assert.Equal(2, SwingStackMath.Cap(150, 100, 2)); // 1.5 → 2
    }

    [Fact]
    public void The_cap_follows_his_CURRENT_points()
    {
        var r = Athletics();
        var s = Stack();
        var f = new Fighter { AthleticsSkill = 150 };
        Assert.Equal(8, SwingStackMath.Cap(in s, in r, f));
        f.Fraction = 0.5;
        Assert.Equal(4, SwingStackMath.Cap(in s, in r, f));
        f.Fraction = 0;
        Assert.Equal(2, SwingStackMath.Cap(in s, in r, f));
        var low = new Fighter { AthleticsSkill = 10 }; // the pool floor 50
        Assert.Equal(3, SwingStackMath.Cap(in s, in r, low));
    }

    // ------------------------------------------------------------------ spending

    [Fact]
    public void Spawns_full_and_spends_one_per_attack_then_attacks_at_empty_lose_the_refill()
    {
        var st = new SwingStack();
        Assert.Equal(8, st.Count(8));
        for (int i = 7; i >= 0; i--)
        {
            Assert.Equal(SpendResult.FromStack, st.Spend(8, out _));
            Assert.Equal(i, st.Count(8));
        }
        st.Advance(0, 1.0, 8, attacking: false);
        st.Advance(0.5, 1.0, 8, attacking: false);
        Assert.Equal(0.5, st.Progress, 9);
        Assert.Equal(SpendResult.AtEmpty, st.Spend(8, out _));
        Assert.Equal(0, st.Count(8));
        Assert.Equal(0.0, st.Progress, 9); // an attack is never free
    }

    [Fact]
    public void A_points_drop_clamps_the_stored_count()
    {
        var st = new SwingStack();
        st.Advance(0, 1.0, 8, false);
        Assert.Equal(8, st.Stored);
        Assert.True(st.Clamp(4));
        Assert.Equal(4, st.Stored);
        Assert.False(st.Clamp(6)); // a higher cap never adds a swing - the refill does
        Assert.Equal(4, st.Count(6));
    }

    // ------------------------------------------------------------------ the refill

    [Fact]
    public void One_swing_comes_back_per_pause_at_the_current_m_while_not_attacking()
    {
        double d = 0.8;
        float m = 0.5f;
        double p = AttackTimerMath.Pause(d, m); // 0.8 s
        Assert.Equal(0.8, p, 9);
        var st = new SwingStack();
        st.Advance(0, p, 4, false);
        for (int i = 0; i < 4; i++) st.Spend(4, out _);
        Assert.Equal(0, st.Count(4));
        Assert.Equal(p, st.SecondsToNext(p, 4), 9);
        st.Advance(0.4, p, 4, false);
        Assert.Equal(0, st.Count(4));
        Assert.Equal(0.4, st.SecondsToNext(p, 4), 9);
        st.Advance(0.8, p, 4, false);
        Assert.Equal(1, st.Count(4));
        st.Advance(2.4, p, 4, false); // two more pauses
        Assert.Equal(3, st.Count(4));
        st.Advance(10, p, 4, false); // the cap
        Assert.Equal(4, st.Count(4));
        Assert.Equal(0.0, st.Progress, 9);

        // at a better m the same swing comes back sooner: m 0.8 → 0.2 s
        Assert.Equal(0.2, AttackTimerMath.Pause(d, 0.8f), 6);
    }

    [Fact]
    public void The_clock_stands_still_while_he_attacks()
    {
        var st = new SwingStack();
        st.Advance(0, 1.0, 2, false);
        st.Spend(2, out _);
        st.Spend(2, out _);
        st.Advance(5, 1.0, 2, attacking: true);
        Assert.Equal(0, st.Count(2));
        Assert.Equal(0.0, st.Progress, 9);
        st.Advance(5.5, 1.0, 2, attacking: false);
        Assert.Equal(0.5, st.Progress, 9);
        Assert.Equal(0.5, st.ProgressAt(5.5, 1.0, 2, false), 9);
        Assert.Equal(0.75, st.ProgressAt(5.75, 1.0, 2, false), 9); // the bar's projection between advances
        Assert.Equal(0.5, st.ProgressAt(5.75, 1.0, 2, true), 9);
    }

    [Fact]
    public void At_and_above_the_peak_line_the_stack_refills_at_once()
    {
        var st = new SwingStack();
        st.Advance(0, 0, 8, false);
        st.Spend(8, out _);
        st.Spend(8, out _);
        Assert.Equal(0.0, AttackTimerMath.Pause(0.8, 1f), 9);
        st.Advance(0.01, AttackTimerMath.Pause(0.8, 1f), 8, false);
        Assert.Equal(8, st.Count(8));
        Assert.Equal(1.0, st.ProgressAt(0.01, 0, 8, false), 9);
    }

    [Fact]
    public void The_AIs_own_gap_after_a_swing_from_the_stack_does_not_refill()
    {
        var st = new SwingStack();
        st.Advance(0, 1.0, 4, false);
        st.Spend(4, out _);
        st.StartGrace(1.0); // his gap: 1 s
        st.Advance(1.5, 1.0, 4, false);
        Assert.Equal(3, st.Count(4));
        Assert.Equal(0.5, st.Progress, 9); // only the half second after the gap
    }

    [Fact]
    public void Bursts_are_spends_in_a_row_with_no_swing_back_between()
    {
        var st = new SwingStack();
        st.Advance(0, 1.0, 8, false);
        st.Spend(8, out int r0);
        Assert.Equal(0, r0);
        st.Spend(8, out _);
        st.Spend(8, out _);
        Assert.Equal(3, st.Run);
        st.Advance(1.2, 1.0, 8, false); // one back
        st.Spend(8, out int closed);
        Assert.Equal(3, closed);
        Assert.Equal(1, st.Run);
        Assert.Equal(1, st.CloseRun());
    }

    [Fact]
    public void A_wait_at_0_marks_the_next_swing_after_waiting()
    {
        var st = new SwingStack();
        st.Advance(0, 1.0, 2, false);
        st.Spend(2, out _);
        st.Spend(2, out _);
        st.Advance(1.0, 1.0, 2, false);
        st.MarkWaited();
        Assert.Equal(SpendResult.AfterWaiting, st.Spend(2, out _));
    }

    [Fact]
    public void A_fresh_start_fills_the_stack()
    {
        var st = new SwingStack();
        st.Advance(0, 1.0, 4, false);
        for (int i = 0; i < 4; i++) st.Spend(4, out _);
        Assert.Equal(4, st.FillUp());
        Assert.Equal(4, st.Count(4));
        Assert.Equal(6, st.Count(6));
    }

    // ------------------------------------------------------------------ the rules

    [Fact]
    public void The_rules_are_read_live_and_the_master_switch_comes_first()
    {
        var s = new TraxSettings();
        var r = SwingStackRules.From(s);
        Assert.True(r.Enabled);
        Assert.True(r.KicksOn);
        Assert.Equal(20, r.PointsPerSwing);
        Assert.Equal(2, r.Minimum);
        s.Set(SettingsSchema.SwingStackPointsPerSwing, 10, SettingSources.Mcm);
        s.Set(SettingsSchema.SwingStackMinimum, 3, SettingSources.Mcm);
        r = SwingStackRules.From(s);
        Assert.Equal(10, r.PointsPerSwing);
        Assert.Equal(3, r.Minimum);
        s.Set(SettingsSchema.KicksUseSwings, 0, SettingSources.Mcm);
        Assert.False(SwingStackRules.From(s).KicksOn);
        Assert.True(SwingStackRules.From(s).Enabled);
        s.Set(SettingsSchema.KicksUseSwings, 1, SettingSources.Mcm);
        s.Set(SettingsSchema.SwingStackEnabled, 0, SettingSources.Mcm);
        r = SwingStackRules.From(s);
        Assert.False(r.Enabled);
        Assert.False(r.KicksOn);
        Assert.Equal("SwingStackEnabled", r.OffBecause);
        Assert.Contains("step 13's pause after EVERY attack", r.Describe());
        s.Set(SettingsSchema.SwingStackEnabled, 1, SettingSources.Mcm);
        s.Set(SettingsSchema.ModEnabled, 0, SettingSources.Mcm);
        r = SwingStackRules.From(s);
        Assert.False(r.Enabled);
        Assert.False(r.KicksOn);
        Assert.Equal("ModEnabled", r.OffBecause);
        Assert.Contains("no pause at all", r.Describe());
        Assert.False(Stack(athletics: false).Enabled);
    }

    [Fact]
    public void The_rules_clamp_odd_values()
    {
        Assert.Equal(SwingStackMath.MinPointsPerSwing, Stack(per: 0).PointsPerSwing);
        Assert.Equal(1, Stack(min: 0).Minimum);
        Assert.Equal(10, Stack(min: 50).Minimum);
        Assert.Contains("a full 150 pool holds 8", Stack().Describe());
    }

    // ------------------------------------------------------------------ your timer and the bar

    [Fact]
    public void Your_countdown_takes_the_stacks_wait_and_follows_it()
    {
        var t = new PlayerAttackTimer();
        Assert.True(t.AttackEnded(10, AttackKind.Melee, 0.8, 0.5f, 1.5, out _));
        Assert.Equal(11.5, t.TimerEnd, 9);
        Assert.Equal(1.5, t.Pause, 9);
        t.Retarget(11.0); // his m rose: sooner
        Assert.Equal(11.0, t.TimerEnd, 9);
        Assert.Equal(1.0, t.Pause, 9);
        var f = t.Frame(11.0, pressing: true);
        Assert.True(f.Ended);
        Assert.True(f.HeldAtEnd); // held, it attacks the moment the swing is back
        Assert.False(t.Holding);

        Assert.False(t.StartCountdown(20, 0.05)); // below the minimum: nothing
        Assert.True(t.StartCountdown(20, 0.6));
        Assert.True(t.Running);
        Assert.Equal(20.6, t.TimerEnd, 9);
        // NaN = step 13's own pause
        Assert.True(new PlayerAttackTimer().AttackEnded(0, AttackKind.Melee, 0.8, 0.5f, double.NaN, out _));
    }

    [Fact]
    public void The_bar_shows_the_count_and_the_refill_toward_the_next()
    {
        var full = AttackRecoveryReading.Full(true).WithStack(8, 8, 1);
        Assert.Equal(1f, full.Share);
        Assert.False(full.Recovering);
        Assert.Equal("8/8", full.CountText);
        var part = AttackRecoveryReading.Full(true).WithStack(3, 8, 0.25);
        Assert.Equal(0.25f, part.Share);
        Assert.True(part.Recovering);
        Assert.Equal("3/8", part.CountText);
        var over = AttackRecoveryReading.Full(true).WithStack(12, 8, 0);
        Assert.Equal(8, over.Stored);
        Assert.Equal(string.Empty, AttackRecoveryReading.Full(true).CountText);
        Assert.Equal("Swings", SwingStackMath.LabelWord);
    }

    // ------------------------------------------------------------------ the summary

    [Fact]
    public void The_summary_counts_attacks_bursts_waits_kicks_and_the_fill()
    {
        var stats = new SwingStackStats();
        stats.AddSpend(true, SpendResult.FromStack);
        stats.AddSpend(true, SpendResult.FromStack);
        stats.AddSpend(true, SpendResult.AfterWaiting);
        stats.AddSpend(false, SpendResult.AtEmpty);
        stats.AddRun(true, 3);
        stats.AddRun(true, 1); // not a burst
        stats.AddRun(false, 12);
        stats.AddWait(true, 0.8);
        stats.AddFill(0, 1.0, 1);
        stats.AddFill(3, 0.5, 2);
        stats.KicksRefusedYou = 2;
        var lines = stats.SummaryLines(Stack(), switched: false);
        Assert.Equal(5, lines.Count);
        Assert.Contains("you: attacks 3 (from the stack 2, after waiting at 0 1, at an empty stack 0)", lines[1]);
        Assert.Contains("bursts (2+ in a row, no swing back between) 1 by length 3: 1, longest 3", lines[1]);
        Assert.Contains("waits at 0 1 (avg 0.80 s, longest 0.80 s)", lines[1]);
        Assert.Contains("10+: 1", lines[2]);
        Assert.Contains("refused at 0 - yours 2", lines[3]);
        Assert.Contains("peak (f 1) 100%", lines[4]);
        Assert.Contains("empty (f 0) 50%", lines[4]);
        Assert.Equal(1, stats.Bursts(true));
    }
}

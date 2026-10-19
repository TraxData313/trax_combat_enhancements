using System;
using System.Collections.Generic;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 69 - WINNERS DON'T HANG BACK (Toni's battles 2026-10-19 15:5x / 16:0x: "some of my men are sitting there in distance while my soldiers
/// are finishing the enemy off"; the 100 v 100's green men pull 0.30, afraid 67%, 163 'by chance' sprints back, charge looks flocking 24269;
/// AI_NOTES "Step 69", DESIGN decision 62): fear ignored who is winning. Now the WIN (courage's local odds, the enemy near him shaken) calms it -
/// losing men exactly as before -, no pull without an enemy near, a charge's mop-up drops the loose-mass leash; the IDLE-MEN diagnostic.
/// </summary>
public class Step69Tests
{
    private static FearRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return FearRules.From(s);
    }

    private static void Near(double expected, double actual, double tol = 1e-3) => Assert.True(Math.Abs(expected - actual) <= tol, "expected " + expected + ", got " + actual);

    [Fact]
    public void The_six_settings_follow_the_charge_loose_mass_on_the_Advanced_page_live()
    {
        var all = SettingsSchema.All.ToList();
        var win = new[]
        {
            SettingsSchema.FearWinCalms, SettingsSchema.FearWinEvenOddsPercent, SettingsSchema.FearWinClearOddsPercent, SettingsSchema.FearWinShakenPercent,
            SettingsSchema.FearThreatMetres, SettingsSchema.FearMopUpWinPercent,
        };
        int last = all.IndexOf(SettingsSchema.FearChargeLooseMetres);
        for (int i = 0; i < win.Length; i++)
        {
            Assert.Equal(last + 1 + i, all.IndexOf(win[i]));
            Assert.Same(SettingsSchema.CourageGroup, win[i].Group);
            Assert.Equal(SettingsPage.Advanced, win[i].Group.Page);
            Assert.Equal(ApplyTiming.Live, win[i].Timing);
            Assert.DoesNotContain(win[i], SettingsSchema.PlayerPage);
        }
        var r = Rules();
        Assert.True(r.WinCalms);
        Assert.Equal(100, r.WinEvenOddsPercent);
        Assert.Equal(200, r.WinClearOddsPercent);
        Assert.Equal(50, r.WinShakenPercent);
        Near(12.0, r.ThreatMetres);
        Assert.Equal(50, r.MopUpWinPercent);
        var s = new TraxSettings();
        s.Set(SettingsSchema.FearWinClearOddsPercent, 300, SettingSources.Mcm);
        Assert.Equal(300, FearRules.From(s).WinClearOddsPercent); // live
    }

    [Fact]
    public void The_master_switch_comes_first_and_the_sentence_says_step_69()
    {
        var off = Rules(s => s.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm));
        Assert.False(off.Enabled);
        Assert.StartsWith("fear flock OFF (ModEnabled)", off.Describe());
        string on = Rules().Describe();
        Assert.Contains("; WINNING calms (step 69): his fear x (1 - win), win 0 at local odds 1.0 (losing below: unchanged) to 1 at 2.0", on);
        Assert.Contains("; no pull without an enemy within 12 m", on);
        Assert.Contains("; a charge's MOP-UP (its men's win 50%+) - no loose-mass leash", on);
        Assert.Contains("winning calms OFF (FearWinCalms)", Rules(s => s.Set(SettingsSchema.FearWinCalms, false, SettingSources.Mcm)).Describe());
        Assert.Contains("(FearThreatMetres 0)", Rules(s => s.Set(SettingsSchema.FearThreatMetres, 0.0, SettingSources.Mcm)).Describe());
    }

    [Fact]
    public void The_win_is_zero_when_losing_or_even_and_one_at_clear_odds()
    {
        var r = Rules();
        Near(0, FearMath.Win(in r, 0.5, 0));
        Near(0, FearMath.Win(in r, 0.99, 1));       // LOSING: even a broken enemy does not calm him - step 67 exactly
        Near(0, FearMath.Win(in r, 1.0, 0));        // even: unchanged
        Near(0.5, FearMath.Win(in r, 1.5, 0));
        Near(1, FearMath.Win(in r, 2.0, 0));
        Near(1, FearMath.Win(in r, 3.0, 0));
        Near(1, FearMath.Win(in r, double.PositiveInfinity, 0));
        Assert.True(double.IsNaN(FearMath.Win(in r, double.NaN, 0))); // no enemy in his radius: no picture of his own
        // the enemy near him shaken (at even odds or better): 1 − (1 − odds win)(1 − 50% × shaken)
        Near(0.5, FearMath.Win(in r, 1.0, 1));
        Near(0.25, FearMath.Win(in r, 1.0, 0.5));
        Near(1 - 0.5 * 0.75, FearMath.Win(in r, 1.5, 0.5));
        Near(1, FearMath.ShakenShare(in r, 15));    // the broken line (a routing man's spent morale)
        Near(0.5, FearMath.ShakenShare(in r, 37.5));
        Near(0, FearMath.ShakenShare(in r, 70));
        Near(0, FearMath.ShakenShare(in r, -1));
        // the switch off: no win anywhere
        var off = Rules(s => s.Set(SettingsSchema.FearWinCalms, false, SettingSources.Mcm));
        Near(0, FearMath.Win(in off, 3.0, 1));
        Near(0, FearMath.WinMixed(in off, 1, 1));
        Assert.False(FearMath.MopUp(in off, 1));
        // the lines are settings
        var wide = Rules(s => s.Set(SettingsSchema.FearWinClearOddsPercent, 300, SettingSources.Mcm));
        Near(0.5, FearMath.Win(in wide, 2.0, 0));
    }

    [Fact]
    public void The_unit_win_relaxes_a_winning_mass_together_and_a_losing_unit_gives_nothing()
    {
        var r = Rules();
        Near(0.5, FearMath.WinMixed(in r, 0, 1));          // he is at even odds, his unit wins: half of the unit's
        Near(1, FearMath.WinMixed(in r, 1, 0));            // he wins clearly in a losing unit: his own (the max)
        Near(0, FearMath.WinMixed(in r, 0, 0));            // a losing man in a losing unit: nothing
        Near(0.8, FearMath.WinMixed(in r, double.NaN, 0.8)); // no enemy in his radius: the unit's picture
        Near(0, FearMath.WinMixed(in r, double.NaN, double.NaN));
        Near(0.6, FearMath.WinMixed(in r, 0.6, double.NaN));
        Near(0.3444, FearMath.Calmed(0.68889, 0.5));
        Near(0.68889, FearMath.Calmed(0.68889, 0));
    }

    /// <summary>THE WORKED EXAMPLE (MECHANICS §11b2, AI_NOTES "Step 69"): a TIRED man (30% of his bar) at morale 50 - a green looter (level 5,
    /// discipline 0) and a veteran legionary (level 26, discipline 0.7) - at his side's local odds 0.5 / 1 / 1.5 / 2 / 3, his unit feeling and
    /// winning as he does, somebody behind him, no shaken enemy: fear / pull. Losing and even = step 67's numbers exactly.</summary>
    [Theory]
    //          level  odds   fear     pull
    [InlineData(5, 0.5, 0.6889, 0.8291)]
    [InlineData(5, 1.0, 0.6889, 0.8291)]
    [InlineData(5, 1.5, 0.3444, 0.2991)]
    [InlineData(5, 2.0, 0.0, 0.0)]
    [InlineData(5, 3.0, 0.0, 0.0)]
    [InlineData(26, 0.5, 0.2067, 0.0872)]
    [InlineData(26, 1.0, 0.2067, 0.0872)]
    [InlineData(26, 1.5, 0.1033, 0.0)]
    [InlineData(26, 2.0, 0.0, 0.0)]
    [InlineData(26, 3.0, 0.0, 0.0)]
    public void The_worked_example_tired_green_and_veteran_by_the_odds(int level, double odds, double fear, double pull)
    {
        var r = Rules();
        double own = FearMath.Own(in r, 50, 0.3, FearMath.Discipline(in r, level, false));
        double baseFear = FearMath.Mixed(in r, own, own);
        double win = FearMath.Win(in r, odds, 0);
        double f = FearMath.Calmed(baseFear, FearMath.WinMixed(in r, win, win));
        Near(fear, f);
        Near(pull, FearMath.Pull(in r, f));
        // the step-67 number (no win) is the losing / even one
        if (odds <= 1.0) Near(FearMath.Mixed(in r, own, own), f, 1e-9);
    }

    [Fact]
    public void Losing_men_keep_every_step_67_number()
    {
        var r = Rules();
        var off = Rules(s => s.Set(SettingsSchema.FearWinCalms, false, SettingSources.Mcm));
        foreach (double morale in new[] { 80.0, 50, 20 })
        foreach (double bar in new[] { 1.0, 0.5, 0 })
        foreach (int level in new[] { 5, 16, 26 })
        foreach (double odds in new[] { 0.1, 0.5, 0.99 })
        foreach (double shaken in new[] { 0.0, 1 })
        {
            double own = FearMath.Own(in r, morale, bar, FearMath.Discipline(in r, level, false));
            double win = FearMath.Win(in r, odds, shaken);
            Assert.Equal(FearMath.Mixed(in off, own, own), FearMath.Calmed(FearMath.Mixed(in r, own, own), FearMath.WinMixed(in r, win, 0)), 12);
        }
    }

    [Fact]
    public void No_enemy_near_no_pull()
    {
        var r = Rules();
        Assert.True(FearMath.Threatened(in r, 5));
        Assert.True(FearMath.Threatened(in r, 12));
        Assert.False(FearMath.Threatened(in r, 12.5));
        Assert.False(FearMath.Threatened(in r, double.NaN)); // nobody near
        var always = Rules(s => s.Set(SettingsSchema.FearThreatMetres, 0.0, SettingSources.Mcm));
        Assert.True(FearMath.Threatened(in always, double.NaN)); // 0 = step 67
    }

    [Fact]
    public void A_charge_mops_up_when_its_men_win()
    {
        var r = Rules();
        Assert.False(FearMath.MopUp(in r, double.NaN));
        Assert.False(FearMath.MopUp(in r, 0.49));
        Assert.True(FearMath.MopUp(in r, 0.5));
        Assert.True(FearMath.MopUp(in r, 1));
        Assert.False(FearMath.MopUp(Rules(s => s.Set(SettingsSchema.FearMopUpWinPercent, 0, SettingSources.Mcm)), 1)); // 0 = never
    }

    [Fact]
    public void The_new_ends_are_appended_and_named()
    {
        Assert.Equal((int)FearEnd.Error + 1, (int)FearEnd.Winning);
        Assert.Equal((int)FearEnd.Winning + 1, (int)FearEnd.NoThreat);
        Assert.Equal((int)FearEnd.NoThreat + 1, (int)FearEnd.Count);
        Assert.Equal("winning (step 69)", FearMath.EndName(FearEnd.Winning));
        Assert.Equal("no enemy near (step 69)", FearMath.EndName(FearEnd.NoThreat));
    }

    [Fact]
    public void The_unit_table_publishes_the_mean_win_of_its_men_in_contact_only()
    {
        var r = Rules();
        var u = new FearUnits();
        int key = FearMath.UnitKey(1, 2);
        Assert.False(u.Due(0)); // arms the clock
        u.Note(key, 0.6, 0.5, 40, 0.5, true, 0, 5, 0, 0, 0, 1, 0, 0, FearOrder.Charge, 1.0);
        u.Note(key, 0.6, 0.5, 40, 0.5, true, 0, 5, 0, 0, 0, 1, 1, 0, FearOrder.Charge, 0.5);
        u.Note(key, 0.6, 0.5, 40, 0.5, false, double.NaN, double.NaN, 0, 0, 0, 1, 2, 0, FearOrder.Charge); // no enemy in his radius: not counted
        Assert.True(u.Due(0.5));
        u.Publish(0.5, in r, null, true);
        Near(0.75, u.WinOf(key, 0.5));
        Near(0.6, u.FearOf(key, 0.5)); // the BASE fear (the unit's mix feeds on it)
        u.Note(key, 0.6, 0.5, 40, 0.5, false, double.NaN, double.NaN, 0, 0, 0, 1, 0, 0, FearOrder.Charge);
        u.Publish(1.0, in r, null, true);
        Assert.True(double.IsNaN(u.WinOf(key, 1.0))); // nobody in contact: not known
        Assert.True(double.IsNaN(u.WinOf(key, 5.0))); // stale
    }

    [Fact]
    public void The_idle_watch_counts_a_stand_past_five_seconds_once_whole_then_each_gap()
    {
        var m = new IdleMind();
        Assert.True(double.IsNaN(IdleMath.Gap(m, 10, 0, 0, out double v)) && double.IsNaN(v)); // the first look: no speed
        double gap = IdleMath.Gap(m, 10.5, 0.1, 0, out v);
        Near(0.5, gap);
        Near(0.2, v);
        Assert.True(IdleMath.Idle(true, v, 15));
        Assert.False(IdleMath.Idle(false, v, 15));    // a hold: not counted
        Assert.False(IdleMath.Idle(true, 1.0, 15));   // walking
        Assert.False(IdleMath.Idle(true, v, 2.5));    // fighting (an enemy within 3 m)
        Assert.False(IdleMath.Idle(true, v, 30));     // nothing within 25 m
        Assert.False(IdleMath.Idle(true, v, double.NaN));
        double counted = 0;
        double t = 10.5;
        counted += IdleMath.Advance(m, t, gap, true); // stands since 10.0
        for (int k = 0; k < 9; k++) counted += IdleMath.Advance(m, t += 0.5, 0.5, true); // 15.0: 5 s - not yet
        Near(0, counted);
        counted += IdleMath.Advance(m, t += 0.5, 0.5, true); // 15.5: past 5 s - the whole 5.5 s once
        Near(5.5, counted);
        counted += IdleMath.Advance(m, t += 0.5, 0.5, true);
        Near(6.0, counted);
        Assert.Equal(0, IdleMath.Advance(m, t += 0.5, 0.5, false)); // he moved: the stand ends
        Assert.True(double.IsNaN(m.Since) && !m.Counted);
        Assert.Equal(0, IdleMath.Advance(m, t += 0.5, double.NaN, true)); // no gap (not watched): starts again
        // a long gap is not a speed
        var g = new IdleMind();
        IdleMath.Gap(g, 0, 0, 0, out _);
        Assert.True(double.IsNaN(IdleMath.Gap(g, 3, 0, 0, out v)) && double.IsNaN(v));
    }

    [Fact]
    public void A_charge_or_an_advance_is_a_moving_order()
    {
        foreach (var k in new[] { BraceOrderKind.Charge, BraceOrderKind.ChargeToTarget, BraceOrderKind.AttackEntity, BraceOrderKind.AiCharge, BraceOrderKind.Advance, BraceOrderKind.AiAdvance })
            Assert.True(IdleMath.MovingOrder(k), k.ToString());
        foreach (var k in new[] { BraceOrderKind.Stop, BraceOrderKind.Move, BraceOrderKind.Retreat, BraceOrderKind.FallBack, BraceOrderKind.Follow, BraceOrderKind.NoOrder, BraceOrderKind.NoFormation })
            Assert.False(IdleMath.MovingOrder(k), k.ToString());
    }

    [Fact]
    public void The_holder_is_named_in_priority_and_counted_by_men()
    {
        Assert.Equal(IdleHolder.Ranged, IdleMath.Holder(true, true, true, true, true));
        Assert.Equal(IdleHolder.WallHold, IdleMath.Holder(false, true, true, true, true));
        Assert.Equal(IdleHolder.FearFlock, IdleMath.Holder(false, false, true, true, true));
        Assert.Equal(IdleHolder.CourageTimid, IdleMath.Holder(false, false, false, true, true));
        Assert.Equal(IdleHolder.Brace, IdleMath.Holder(false, false, false, false, true));
        Assert.Equal(IdleHolder.Game, IdleMath.Holder(false, false, false, false, false));
        var stats = new IdleStats();
        var a = new IdleMind();
        var b = new IdleMind();
        stats.Add(a, IdleHolder.FearFlock, 6, 12);
        stats.Add(a, IdleHolder.FearFlock, 0.5, 14);
        stats.Add(b, IdleHolder.FearFlock, 5.5, 10);
        stats.Add(b, IdleHolder.Ranged, 7, 18);
        stats.Add(b, IdleHolder.Game, 0, 18); // nothing to count
        Near(12, stats.SecondsOf(IdleHolder.FearFlock));
        Assert.Equal(2, stats.MenOf(IdleHolder.FearFlock));
        Assert.Equal(1, stats.MenOf(IdleHolder.Ranged));
        Assert.Equal(0, stats.MenOf(IdleHolder.Game));
        Near(12, stats.MeanEnemy(IdleHolder.FearFlock));
        Near(12, stats.OursSeconds);
        var lines = new List<string>();
        stats.AddSummaryLines(lines);
        Assert.Single(lines);
        Assert.StartsWith("idle men (step 69 - AI men on foot on a charge / advance, an enemy within 25 m and none within 3 m, standing (below 0.5 m/s) more than 5 s", lines[0]);
        Assert.Contains("OURS 12 man-s - the fear flock (step 67) 12 man-s (2 men, the enemy 12.0 m)", lines[0]);
        Assert.Contains("the game itself (none of ours) 0 man-s (0 men, the enemy - m)", lines[0]);
        Assert.Contains("a ranged weapon in hand / a ranged or skirmisher formation (NORMAL) 7 man-s (1 men, the enemy 18.0 m)", lines[0]);
    }

    [Fact]
    public void The_winning_line_files_the_looks_by_the_odds_and_shows_losing_unchanged()
    {
        var r = Rules();
        var st = new FearStats { MenLooked = 3 };
        st.AddWin(in r, 0.5, 0, 0.6, 0.6, true, true);
        st.AddWin(in r, 1.5, 0.5, 0.6, 0.3, true, true);
        st.AddWin(in r, 2.5, 1, 0.6, 0, true, false);
        st.AddWin(in r, double.NaN, 0.8, 0.5, 0.1, true, false);
        st.CalmedByWinLooks = 2;
        st.AddEnd(FearEnd.Winning);
        Near(0.6, st.WinFearBefore(0));
        Near(0.6, st.WinFearAfter(0)); // losing: unchanged
        Near(0, st.WinFearAfter(2));
        var lines = new List<string>();
        st.AddWinLines(lines, in r);
        Assert.Single(lines);
        Assert.StartsWith("fear flock and winning (step 69) by his side's local odds: losing (odds below 1.0) 1 looks: win 0.00, fear 0.60 → 0.60, afraid 100.0% → 100.0%", lines[0]);
        Assert.Contains("clear (2.0+) 1 looks: win 1.00, fear 0.60 → 0.00, afraid 100.0% → 0.0%", lines[0]);
        Assert.Contains("calmed by winning 2 looks (spells ended 1)", lines[0]);
        Assert.Contains("no enemy within 12 m 0 looks", lines[0]);
        var none = new List<string>();
        new FearStats().AddWinLines(none, in r);
        Assert.Empty(none); // nobody looked at: no line
    }
}

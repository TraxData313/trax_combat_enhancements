using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>Step 43 (Anton, 2026-10-17): AGGRESSION BY ATHLETICS (the AI's two attack-decision chances × a share sliding with his bar from
/// 60% full to 20% empty, in 0.05 steps), the FEVER (a new charge / advance order arms a store of current points ÷ 10 s at an ABSOLUTE 80%,
/// burning from the first contact, faster with a shield in hand, fading back to his tired share), the fever sprint (a roll every 1 s at
/// 50% × fever left), cheaper blows and wrong-side blocks (format 8).</summary>
public class FeverTests
{
    private static FeverRules Rules(int fresh = 60, int exhausted = 20, bool fever = true, double pps = 10, int peak = 80, double contact = 5,
        double shield = 0.7, bool sprint = true, double min = 3, double max = 12, double check = 1, int chance = 50, bool mod = true, bool athletics = true) =>
        new(mod, athletics, fever, fresh, exhausted, pps, peak, contact, shield, sprint, min, max, check, chance);

    private static void Near(double expected, double actual, double eps = 1e-6) =>
        Assert.True(Math.Abs(expected - actual) <= eps, "expected " + expected + ", got " + actual);

    // ------------------------------------------------------------------ aggression by the bar

    [Fact]
    public void The_share_slides_with_the_bar_from_60_to_20_percent()
    {
        var r = Rules();
        Near(0.6, FeverMath.TiredShare(in r, 1));
        Near(0.5, FeverMath.TiredShare(in r, 0.75));
        Near(0.4, FeverMath.TiredShare(in r, 0.5));
        Near(0.2, FeverMath.TiredShare(in r, 0));
        Near(0.2, FeverMath.TiredShare(in r, -1));          // clamped
        Near(0.6, FeverMath.TiredShare(in r, 1.5));
        // a wounded man capped at half health is at most at a half bar: no special case, the bar reads it
        Near(0.4, FeverMath.TiredShare(in r, 0.5));
        // 100 / 100 = the game's own at any bar
        Near(1, FeverMath.TiredShare(Rules(fresh: 100, exhausted: 100), 0.3));
        Assert.Equal(1f, FeverMath.DecisionFactor(Rules(fresh: 100, exhausted: 100), FeverPhase.None, 0, 0, 0.3, false));
    }

    [Fact]
    public void The_applied_values_in_005_steps_with_no_floor_of_ours()
    {
        var r = Rules();
        float fresh = FeverMath.DecisionFactor(in r, FeverPhase.None, 0, 0, 1, false);
        float half = FeverMath.DecisionFactor(in r, FeverPhase.None, 0, 0, 0.5, false);
        float empty = FeverMath.DecisionFactor(in r, FeverPhase.None, 0, 0, 0, false);
        Near(0.6, fresh);
        Near(0.4, half);
        Near(0.2, empty);
        Near(0.0864, FeverMath.ApplyChance(0.144f, fresh), 1e-6);  // a charge: 0.144 → fresh 0.086
        Near(0.0288, FeverMath.ApplyChance(0.144f, empty), 1e-6);  // empty 0.029
        Near(0.01, FeverMath.ApplyChance(0.05f, empty), 1e-6);     // a shield wall's 0.05 (the game's clamp) → 0.01: no floor
        Near(0.016, FeverMath.ApplyChance(0.08f, empty), 1e-6);    // the riposte
        Near(0.55, FeverMath.DecisionFactor(in r, FeverPhase.None, 0, 0, 0.9, false));   // 0.56 → 0.55
        Near(0.25, FeverMath.DecisionFactor(in r, FeverPhase.None, 0, 0, 0.1, false));   // 0.24 → 0.25
        Near(0.45, FeverMath.Quantize(0.474));
        Near(0.5, FeverMath.Quantize(0.476));
        Assert.Equal(0, FeverMath.Quantize(-0.1));
        Assert.Equal(1f, FeverMath.ApplyChance(0.9f, 2f));          // clamped to 1
        Assert.Equal(0f, FeverMath.ApplyChance(-0.2f, 0.5f));
        // over a full bar the share takes (0.6 - 0.2) / 0.05 = 8 steps: about one recompute per 12.5% of the bar
        int steps = 0;
        float last = FeverMath.DecisionFactor(in r, FeverPhase.None, 0, 0, 1, false);
        for (int i = 999; i >= 0; i--)
        {
            float now = FeverMath.DecisionFactor(in r, FeverPhase.None, 0, 0, i / 1000.0, false);
            if (now != last) steps++;
            last = now;
        }
        Assert.Equal(8, steps);
    }

    [Fact]
    public void The_player_and_the_switches_leave_the_games_values()
    {
        Assert.Equal(1f, FeverMath.DecisionFactor(Rules(), FeverPhase.Armed, 8, 8, 0.2, isPlayer: true));
        Assert.Equal(1f, FeverMath.DecisionFactor(Rules(mod: false), FeverPhase.Armed, 8, 8, 0.2, false));        // the master switch first
        Assert.Equal(1f, FeverMath.DecisionFactor(Rules(athletics: false), FeverPhase.None, 0, 0, 0.2, false));
        Assert.Equal(0.144f, FeverMath.ApplyChance(0.144f, 1f));
        Assert.Equal("ModEnabled", Rules(mod: false, athletics: false).OffBecause);
        Assert.Equal("ModEnabled", Rules(mod: false).AggressionOffBecause);
        Assert.Equal("AthleticsEnabled", Rules(athletics: false).AggressionOffBecause);
        Assert.Null(Rules().AggressionOffBecause);
        Assert.Equal("FeverEnabled", Rules(fever: false).OffBecause);
    }

    [Fact]
    public void The_share_combines_with_step_5e_multiplicatively()
    {
        // AttackRateAiDecisions on, a man at m 0.5 and a full bar: the game's 0.144 x 0.5 (5e) x 0.6 = 0.0432, either order
        float f = FeverMath.DecisionFactor(Rules(), FeverPhase.None, 0, 0, 1, false);
        float a = FeverMath.ApplyChance(AttackRateMath.ScaleChance(0.144f, 0.5f), f);
        float b = AttackRateMath.ScaleChance(FeverMath.ApplyChance(0.144f, f), 0.5f);
        Near(0.0432, a, 1e-6);
        Near(a, b, 1e-7);
    }

    // ------------------------------------------------------------------ the store, armed, burning

    [Fact]
    public void The_store_is_his_CURRENT_points_over_10_seconds()
    {
        Near(8, FeverMath.Store(80, 10));      // Anton's example: a 100-point man at 80 points
        Near(10, FeverMath.Store(100, 10));
        Near(4, FeverMath.Store(40, 10));      // tired: shorter
        Near(15, FeverMath.Store(150, 10));    // a veteran's big pool: longer
        Assert.Equal(0, FeverMath.Store(0, 10));
        var r = Rules();
        var f = new FeverStore();
        Assert.Equal(FeverArm.Armed, f.Arm(in r, SprintOrder.Charge, 80, 12, byPlayer: true));
        Assert.Equal(FeverPhase.Armed, f.Phase);
        Near(8, f.Full);
        Near(8, f.Left);
        Assert.True(f.ByPlayer);
        Assert.Equal(12, f.ArmedAt);
        Assert.Equal(FeverArm.NoStore, new FeverStore().Arm(in r, SprintOrder.Charge, 0, 0, false));
        Assert.Equal(FeverArm.NotAggressive, new FeverStore().Arm(in r, SprintOrder.Move, 80, 0, false));
        Assert.Equal(FeverArm.NotAggressive, new FeverStore().Arm(in r, SprintOrder.None, 80, 0, false));
    }

    [Fact]
    public void Armed_is_the_absolute_peak_and_does_not_burn_until_contact()
    {
        var r = Rules();
        var f = new FeverStore();
        f.Arm(in r, SprintOrder.Advance, 80, 0, false);
        Near(0.8, FeverMath.AggressionShare(in r, f.Phase, f.Left, f.Full, 0.8));       // max(0.52, 0.8)
        Near(0.8, FeverMath.AggressionShare(in r, f.Phase, f.Left, f.Full, 0.1));       // a tired man too: max(0.24, 0.8)
        Near(0.8, FeverMath.DecisionFactor(in r, f.Phase, f.Left, f.Full, 0.8, false));
        Near(0.1152, FeverMath.ApplyChance(0.144f, 0.8f), 1e-6);                        // a charge's fever peak 0.115
        Assert.False(f.Burn(in r, 30, shieldInHand: false));                             // 30 s marching: nothing spent
        Near(8, f.Left);
        Assert.True(f.StartBurn(31));
        Assert.Equal(FeverPhase.Burning, f.Phase);
        Assert.False(f.StartBurn(32));                                                    // once
        Assert.False(f.Burn(in r, 1, false));
        Near(7, f.Left);
        Assert.False(f.Burn(in r, 1, true));                                              // a shield in hand: 1 / 0.7 s a second
        Near(7 - 1 / 0.7, f.Left);
        // a peak at or below his tired share adds nothing
        var low = Rules(peak: 50);
        Near(0.6, FeverMath.AggressionShare(in low, FeverPhase.Armed, 8, 8, 1));
        Near(0.6, FeverMath.AggressionShare(in low, FeverPhase.Burning, 4, 8, 1));
        Near(0.5, FeverMath.AggressionShare(in low, FeverPhase.Armed, 8, 8, 0.25));     // max(0.3, 0.5)
    }

    [Fact]
    public void A_shield_taken_out_mid_fever_switches_the_burn_live()
    {
        var r = Rules();
        var f = new FeverStore();
        f.Arm(in r, SprintOrder.Charge, 100, 0, false);   // 10 s
        f.StartBurn(5);
        f.Burn(in r, 2, false);                           // two-handed axe: 2 s
        Assert.False(f.ShieldSwitched);
        f.Burn(in r, 2, true);                            // shield off the back: 2 x 1/0.7
        Assert.True(f.ShieldSwitched);
        Near(10 - 2 - 2 / 0.7, f.Left);
        Near(2, f.BurnPlainSeconds);
        Near(2, f.BurnShieldSeconds);
        // a whole fever with the shield up lasts 0.7 as long: 10 s of store in 7 s
        var g = new FeverStore();
        g.Arm(in r, SprintOrder.Charge, 100, 0, false);
        g.StartBurn(0);
        int ticks = 0;
        while (!g.Burn(in r, 0.25, true)) ticks++;
        Near(7, (ticks + 1) * 0.25, 0.25);
        Assert.Equal(0, g.Left);
    }

    [Fact]
    public void The_fade_runs_from_the_peak_to_his_CURRENT_tired_share()
    {
        var r = Rules();
        // a fresh man (bar 1, tired 0.6): 0.8 → 0.7 at half → 0.6 at empty
        Near(0.8, FeverMath.AggressionShare(in r, FeverPhase.Burning, 8, 8, 1));
        Near(0.7, FeverMath.AggressionShare(in r, FeverPhase.Burning, 4, 8, 1));
        Near(0.6, FeverMath.AggressionShare(in r, FeverPhase.Burning, 0, 8, 1));
        // tired keeps sinking as he fights: at a half bar (0.4) half the fever left = 0.6; at a quarter bar (0.3) = 0.55
        Near(0.6, FeverMath.AggressionShare(in r, FeverPhase.Burning, 4, 8, 0.5));
        Near(0.55, FeverMath.AggressionShare(in r, FeverPhase.Burning, 4, 8, 0.25));
        Near(0.3, FeverMath.AggressionShare(in r, FeverPhase.Burning, 0, 8, 0.25));      // never below tired
        Near(0.4, FeverMath.AggressionShare(Rules(fever: false), FeverPhase.Burning, 8, 8, 0.5)); // the fever off: tired only
        // a whole burn at 0.25 s looks for a fresh man: the arming 0.6 → 0.8, then 0.75, 0.7, 0.65, 0.6 = 5 recomputes
        var f = new FeverStore();
        f.Arm(in r, SprintOrder.Charge, 80, 0, false);
        var seen = new List<float> { FeverMath.DecisionFactor(in r, FeverPhase.None, 0, 0, 1, false) };
        void Look()
        {
            float now = FeverMath.DecisionFactor(in r, f.Phase, f.Left, f.Full, 1, false);
            if (now != seen[^1]) seen.Add(now);
        }
        Look();
        f.StartBurn(0);
        while (!f.Burn(in r, 0.25, false)) Look();
        f.Stop();
        Look();
        Assert.Equal(6, seen.Count);
        Near(0.6, seen[0]);
        Near(0.8, seen[1]);
        Near(0.6, seen[^1]);
        for (int i = 2; i < seen.Count; i++) Assert.True(seen[i] < seen[i - 1]);
    }

    [Fact]
    public void A_repeat_does_not_rearm_a_different_aggressive_order_takes_the_larger()
    {
        var r = Rules();
        var f = new FeverStore();
        f.Arm(in r, SprintOrder.Advance, 80, 0, false);
        f.StartBurn(1);
        f.Burn(in r, 5, false);                                                     // 3 s left
        Assert.Equal(FeverArm.Repeat, f.Arm(in r, SprintOrder.Advance, 70, 6, true)); // advance again: nothing
        Near(3, f.Left);
        Assert.Equal(FeverArm.Rearmed, f.Arm(in r, SprintOrder.Charge, 70, 6, true)); // the charge after it: a fresh 7 s > 3 s
        Near(7, f.Left);
        Near(7, f.Full);
        Assert.Equal(SprintOrder.Charge, f.Order);
        Assert.Equal(FeverPhase.Burning, f.Phase);                                  // the clock goes on
        Assert.True(f.ByPlayer);
        Assert.Equal(FeverArm.KeptLarger, f.Arm(in r, SprintOrder.Advance, 20, 7, false)); // a fresh 2 s < 7 s left: kept
        Near(7, f.Left);
        Assert.Equal(SprintOrder.Advance, f.Order);
    }

    [Fact]
    public void An_order_ends_it_and_the_master_switch_never_arms()
    {
        var r = Rules();
        var f = new FeverStore();
        f.Arm(in r, SprintOrder.Charge, 80, 0, false);
        f.Stop();
        Assert.False(f.Running);
        Near(0.4, FeverMath.DecisionFactor(in r, f.Phase, f.Left, f.Full, 0.5, false));
        Assert.Equal(FeverArm.Off, new FeverStore().Arm(Rules(mod: false), SprintOrder.Charge, 80, 0, false));
        Assert.Equal(FeverArm.Off, new FeverStore().Arm(Rules(fever: false), SprintOrder.Charge, 80, 0, false));
        Assert.Equal(FeverArm.Off, new FeverStore().Arm(Rules(athletics: false), SprintOrder.Charge, 80, 0, false));
        // the fever off: his tired share even for a stale armed store
        var g = new FeverStore();
        g.Arm(in r, SprintOrder.Charge, 80, 0, false);
        Near(0.6, FeverMath.AggressionShare(Rules(fever: false), g.Phase, g.Left, g.Full, 1));
        Assert.Equal(SprintOrder.Charge, FeverMath.Group(BraceOrderKind.AiCharge));
        Assert.Equal(SprintOrder.Advance, FeverMath.Group(BraceOrderKind.AiAdvance));
        Assert.Equal(SprintOrder.Charge, FeverMath.Group(BraceOrderKind.ChargeToTarget));
        Assert.Equal(SprintOrder.None, FeverMath.Group(BraceOrderKind.Move));
        Assert.Equal(SprintOrder.None, FeverMath.Group(BraceOrderKind.NoFormation));
    }

    // ------------------------------------------------------------------ the fever sprint

    [Fact]
    public void The_sprint_chance_fades_with_the_fever()
    {
        var r = Rules();
        Near(0.5, FeverMath.SprintChance(in r, FeverPhase.Armed, 8, 8));
        Near(0.5, FeverMath.SprintChance(in r, FeverPhase.Burning, 8, 8));
        Near(0.25, FeverMath.SprintChance(in r, FeverPhase.Burning, 4, 8));
        Near(0, FeverMath.SprintChance(in r, FeverPhase.Burning, 0, 8));
        Near(0, FeverMath.SprintChance(in r, FeverPhase.None, 8, 8));
        Near(0, FeverMath.SprintChance(Rules(sprint: false), FeverPhase.Armed, 8, 8));
        Near(0, FeverMath.SprintChance(Rules(chance: 0), FeverPhase.Armed, 8, 8));
        Assert.True(FeverMath.SprintRoll(0.25, 0.2));
        Assert.False(FeverMath.SprintRoll(0.25, 0.3));
        Assert.False(FeverMath.SprintRoll(0, 0));
        Assert.Equal(0, FeverMath.ShareBand(0.9));
        Assert.Equal(1, FeverMath.ShareBand(0.5));
        Assert.Equal(2, FeverMath.ShareBand(0.2));
    }

    [Fact]
    public void The_roll_comes_every_check_seconds_from_the_arming()
    {
        var r = Rules(check: 1);
        var f = new FeverStore();
        Assert.False(f.SprintDue(in r, 0));                 // no fever, no roll
        f.Arm(in r, SprintOrder.Charge, 80, 10, false);
        Assert.True(f.SprintDue(in r, 10));
        Assert.False(f.SprintDue(in r, 10.5));
        Assert.True(f.SprintDue(in r, 11));
        Assert.False(f.SprintDue(in r, 11.9));
        Assert.True(f.SprintDue(in r, 12.2));
        Assert.True(f.SprintDue(Rules(check: 0.25), 13.2));  // read live
        Assert.False(f.SprintDue(Rules(check: 0.25), 13.3));
        Assert.True(f.SprintDue(Rules(check: 0.25), 13.45));
    }

    [Fact]
    public void The_reserve_still_caps_what_a_fever_sprint_spends()
    {
        var s = new SprintRules(true, true, true, true, 20, 20, 2, 1.1, 30, 7, 0.5, 0.5, 0, 50, 80);
        // an advance keeps 50%: at a full 5 s store he sprints 2.5 s; at 40% nothing (reserve kept)
        var full = new SprintStore();
        Assert.Equal(SprintStart.Started, full.TryStartAi(in s, 0, 5, SprintOrder.Advance));
        Near(2.5, full.Length);
        var low = new SprintStore();
        low.Clamp(5);
        low.TryStartAi(in s, 0, 5, SprintOrder.Charge);    // a charge spends all of it
        low.Advance(3, true, out _);                         // 2 s left = 40%
        low.Stop();
        Assert.Equal(SprintStart.ReserveKept, low.TryStartAi(in s, 1, 5, SprintOrder.Advance));
        Near(0, SprintMath.AiAmount(in s, SprintOrder.Advance, 2, 5));
        Near(2, SprintMath.AiAmount(in s, SprintOrder.Charge, 2, 5));
    }

    // ------------------------------------------------------------------ settings, migration, summary

    [Fact]
    public void The_settings_are_live_and_grouped_after_the_sprint()
    {
        var t = new TraxSettings();
        var r = FeverRules.From(t);
        Assert.True(r.AggressionOn);
        Assert.True(r.Enabled);
        Assert.Equal(60, r.FreshPercent);
        Assert.Equal(20, r.ExhaustedPercent);
        Assert.Equal(80, r.PeakPercent);
        Near(10, r.PointsPerSecond);
        Near(0.7, r.ShieldDurationMultiplier, 1e-6);
        Assert.Equal(50, r.SprintChancePercent);
        t.Set(SettingsSchema.AiAggressionFreshPercent, 100, SettingSources.Mcm);
        t.Set(SettingsSchema.AiAggressionExhaustedPercent, 100, SettingSources.Mcm);
        r = FeverRules.From(t);
        Assert.Equal(1f, FeverMath.DecisionFactor(in r, FeverPhase.None, 0, 0, 0.1, false));
        t.Set(SettingsSchema.FeverAggressionPercent, 150, SettingSources.Mcm);
        r = FeverRules.From(t);
        Near(1.5, FeverMath.DecisionFactor(in r, FeverPhase.Armed, 1, 1, 0.1, false));
        Assert.Equal(SettingsSchema.SprintGroup.Order + 1, SettingsSchema.FeverGroup.Order);
        Assert.Same(SettingsSchema.AdvancedGroup, SettingsSchema.Groups[SettingsSchema.Groups.Count - 1]);
        Assert.Same(SettingsSchema.PlayerAiGroup, SettingsSchema.AiAggressionFreshPercent.Group); // the player page since step 59
        Assert.Same(SettingsSchema.PlayerAiGroup, SettingsSchema.FeverEnabled.Group);
        Assert.Same(SettingsSchema.FeverGroup, SettingsSchema.FeverSprintChancePercent.Group);
        Assert.Equal("FeverPointsPerSecond", SettingsSchema.InGroup(SettingsSchema.FeverGroup).First().Key);
    }

    [Fact]
    public void The_sentence_names_the_numbers()
    {
        string d = Rules().Describe();
        Assert.StartsWith("aggression: AI men on foot decide to attack at 60% of the game's rate at a full bar, sliding in a straight line to 20% at an empty one "
                          + "(AiAggressionFreshPercent / AiAggressionExhaustedPercent - the bar = points / his full pool) - attack chance on a charge 0.144 → fresh 0.086, "
                          + "half 0.058, empty 0.029, fever peak 0.115 (riposte the same shares)", d);
        Assert.Contains("CURRENT Athletics points / 10.0 s (FeverPointsPerSecond - 80 points = 8.0 s) at 80% of the game's rate (FeverAggressionPercent, never below his tired share)", d);
        Assert.Contains("within 5.0 m (FeverContactMetres), burns x1.43 faster with a shield in hand", d);
        Assert.Contains("fever sprint: every 1.0 s a feverish man with sprint above his order's reserve and an enemy 3.0-12.0 m away sprints with chance 50% x the fever left", d);
        Assert.Equal("aggression: off (AthleticsEnabled) - the game's own attack decisions; fever: off (AthleticsEnabled)", Rules(athletics: false).Describe());
    }

    /// <summary>Step 43's shipped defaults (defaults.json) - the tests run on DESIGN's initial 10 / 5, where there is nothing to push.</summary>
    private static double Shipped43(ParamDef p) =>
        p == SettingsSchema.CostPerBlow ? 3 : p == SettingsSchema.CostPerWrongSideShieldBlock ? 2 : p.Default;

    [Fact]
    public void A_format_7_file_at_the_old_blow_and_wrong_side_costs_gets_3_and_2_once_tuned_values_kept()
    {
        Assert.Equal(24, ConfigFile.FormatVersion);
        var read = ConfigFile.Read("{ \"ConfigVersion\": 7, \"CostPerBlow\": 10.0, \"CostPerWrongSideShieldBlock\": 5.0, \"CostPerShieldBlock\": 1.0, \"CostPerWeaponParry\": 2.0 }");
        var notes = ConfigFile.Migrate(read, Shipped43);
        Assert.Equal(2, notes.Count);
        Assert.Contains(notes, n => n.StartsWith("CostPerBlow: 10.0 → 3.0 (format 7 → 24, the old default; steps 43 / 46, Anton: a swing or shot costs less", StringComparison.Ordinal));
        Assert.Contains(notes, n => n.StartsWith("CostPerWrongSideShieldBlock: 5.0 → 2.0 (format 7 → 24, the old default; step 43", StringComparison.Ordinal));
        Assert.Equal(3, read.Values["CostPerBlow"], 9);
        Assert.Equal(2, read.Values["CostPerWrongSideShieldBlock"], 9);
        Assert.Equal(1, read.Values["CostPerShieldBlock"], 9);        // the others stay
        Assert.Equal(2, read.Values["CostPerWeaponParry"], 9);
        Assert.Equal(2, ConfigFile.Migrate(read, Shipped43).Count);   // idempotent (the notes list is the read's own)
        // his own values stay
        var mine = ConfigFile.Read("{ \"ConfigVersion\": 7, \"CostPerBlow\": 8.0, \"CostPerWrongSideShieldBlock\": 4.0 }");
        Assert.Empty(ConfigFile.Migrate(mine, Shipped43));
        Assert.Equal(8, mine.Values["CostPerBlow"], 9);
        Assert.Equal(4, mine.Values["CostPerWrongSideShieldBlock"], 9);
        // a format-8 file's 10 is his own now (step 46: kept, and said so)
        var eight = ConfigFile.Read("{ \"ConfigVersion\": 8, \"CostPerBlow\": 10.0, \"CostPerWrongSideShieldBlock\": 5.0 }");
        // (step 68's own notes apart: on this lambda's step-43 default 2 a format-8 file's 5 reads as his own - Step68Tests runs step 68's)
        var eightNotes = ConfigFile.Migrate(eight, Shipped43).Where(n => !n.Contains("step 68", StringComparison.Ordinal)).ToList();
        Assert.Single(eightNotes);
        Assert.StartsWith("CostPerBlow: 10.0 kept - your own value (format 8 → 24;", eightNotes[0], StringComparison.Ordinal);
        Assert.Equal(10, eight.Values["CostPerBlow"], 9);
        // an older file moves too (both defaults go back to steps 5c / 22)
        var five = ConfigFile.Read("{ \"ConfigVersion\": 5, \"CostPerBlow\": 10.0, \"CostPerWrongSideShieldBlock\": 5.0 }");
        Assert.Equal(2, ConfigFile.Migrate(five, Shipped43).Count);
        // on DESIGN's initial values nothing moves (the default IS the old one)
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 7, \"CostPerBlow\": 10.0 }")));
    }

    /// <summary>Step 46's shipped defaults (defaults.json, Anton's playtest): CostPerBlow 7 - the tests run on DESIGN's initial 10.</summary>
    private static double Shipped46(ParamDef p) =>
        p == SettingsSchema.CostPerBlow ? 7 : p == SettingsSchema.CostPerWrongSideShieldBlock ? 2 : p.Default;

    [Fact]
    public void Step46_a_format_8_file_at_step_43s_blow_cost_gets_7_once_and_older_files_end_at_7_too()
    {
        Assert.Equal(24, ConfigFile.FormatVersion);
        // a format-8 file still holding step 43's 3: → 7, once
        var eight = ConfigFile.Read("{ \"ConfigVersion\": 8, \"CostPerBlow\": 3.0, \"CostPerWrongSideShieldBlock\": 2.0 }");
        var notes = ConfigFile.Migrate(eight, Shipped46);
        Assert.Single(notes);
        Assert.StartsWith("CostPerBlow: 3.0 → 7.0 (format 8 → 24, the old default; step 46, Anton's playtest: a swing or shot costs 7 Athletics", notes[0], StringComparison.Ordinal);
        Assert.Equal(7, eight.Values["CostPerBlow"], 9);
        Assert.Equal(2, eight.Values["CostPerWrongSideShieldBlock"], 9);
        Assert.Single(ConfigFile.Migrate(eight, Shipped46));                                // idempotent (the notes list is the read's own)
        // a tuned format-8 value is kept - and logged
        var tuned = ConfigFile.Read("{ \"ConfigVersion\": 8, \"CostPerBlow\": 5.0 }");
        var kept = ConfigFile.Migrate(tuned, Shipped46);
        Assert.Equal(new[] { "CostPerBlow: 5.0 kept - your own value (format 8 → 24; the new default 7.0 is not pushed over it; step 46, Anton's playtest: a swing or shot costs 7 Athletics (step 43's 3 was too cheap))" }, kept);
        Assert.Equal(5, tuned.Values["CostPerBlow"], 9);
        // already 7, or no key: nothing to say
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 8, \"CostPerBlow\": 7.0 }"), Shipped46));
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 8, \"DamageRandomPercent\": 40 }"), Shipped46));
        // the chain: an older file's step 5c default 10 (moved by step 43) ends at 7 - never at 3
        foreach (int format in new[] { 5, 7 })
        {
            var old = ConfigFile.Read("{ \"ConfigVersion\": " + format + ", \"CostPerBlow\": 10.0, \"CostPerWrongSideShieldBlock\": 5.0 }");
            var chain = ConfigFile.Migrate(old, Shipped46);
            Assert.Equal(7, old.Values["CostPerBlow"], 9);
            Assert.Equal(2, old.Values["CostPerWrongSideShieldBlock"], 9);
            Assert.Contains(chain, n => n.StartsWith("CostPerBlow: 10.0 → 7.0 (format " + format + " → 24, the old default; steps 43 / 46", StringComparison.Ordinal));
            Assert.Equal(2, chain.Count);
        }
        // an older file's 3 was his own (3 was never the default before format 8): kept, no note
        var seven = ConfigFile.Read("{ \"ConfigVersion\": 7, \"CostPerBlow\": 3.0 }");
        Assert.Empty(ConfigFile.Migrate(seven, Shipped46));
        Assert.Equal(3, seven.Values["CostPerBlow"], 9);
        // on DESIGN's initial values (10) a format-8 file's 3 is his own: kept, said so
        Assert.Single(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 8, \"CostPerBlow\": 3.0 }")));
    }

    [Fact]
    public void The_summary_lines()
    {
        var r = Rules();
        var st = new FeverStats();
        st.AddArmed(SprintOrder.Charge, true);
        st.AddArmed(SprintOrder.Charge, false);
        st.AddArmed(SprintOrder.Advance, false);
        st.Rearmed = 1;
        st.Repeats = 2;
        st.NotArmedMounted = 3;
        st.AddBurnStart(4);
        st.AddBurnStart(10);
        st.AddEnd(FeverEnd.Faded, true, 5.6, 0, 8, false, 5);
        st.AddEnd(FeverEnd.Faded, true, 0, 8, 8, true, 5);
        st.AddEnd(FeverEnd.OrderEnded, false, 0, 0, 0, false, 2);
        st.DecisionRecomputes = 30;
        st.AiManSeconds = 600;
        st.SprintChecks = 9;
        st.AddSprint(0.9);
        st.AddSprint(0.5);
        st.AddRefusal(FeverSprintRefusal.MissedRoll);
        st.AddRefusal(FeverSprintRefusal.NoEnemyInRange);
        st.AddContactTime(FeverStats.Calm, 0, 60);
        st.AddContactTime(FeverStats.Calm, 3, 60);
        st.AddContactTime(FeverStats.InFever, 0, 60);
        st.AddContactTime(FeverStats.NoFormation, 1, 30);
        for (int i = 0; i < 6; i++) st.AddAttack(FeverStats.Calm, 0);
        for (int i = 0; i < 2; i++) st.AddAttack(FeverStats.Calm, 3);
        for (int i = 0; i < 9; i++) st.AddAttack(FeverStats.InFever, 0);
        st.AddAttack(FeverStats.NoFormation, 1);
        st.AddValues(0, 0.144f, 0.0864f, 0.08f, 0.048f);
        st.AddValues(FeverStats.BarBands, 0.144f, 0.1152f, 0.08f, 0.064f);
        Assert.Equal(1, FeverStats.BarBand(0.7));
        Assert.Equal(3, FeverStats.BarBand(0.25));
        var lines = new List<string>();
        st.AddSummaryLines(lines, in r);
        Assert.Equal(10, lines.Count);
        Assert.StartsWith("fever settings: aggression: AI men on foot", lines[0]);
        Assert.Equal("fever armed: 3 (charge 2 - by you 1 / by the AI general 1; advance 1 - by you 0 / by the AI general 1); a different aggressive order on a running fever: "
                     + "re-armed to a fresh one 1, kept (more left than a fresh one) 0; the same order again ignored 2; not armed: on a horse 3, no Athletics 0, player-controlled 0", lines[1]);
        Assert.Equal("fever contact: 2 of 3 fevers met an enemy within 5.0 m - armed → contact mean 7.0 s, max 10.0 s; never met one (ended still armed): 1 (a hold / move / retreat order 1)", lines[2]);
        Assert.Equal("fever burn: faded 2; burn length with a shield in hand (most of it) 1, mean 5.6 s from a mean store of 8.0 s (x0.70); without 1, mean 8.0 s from a mean store of 8.0 s (x1.00) "
                     + "(FeverShieldDurationMultiplier 0.70 - a shield man's fever lasts that share); a shield taken up or put away mid-burn 1; all ends: faded 2, a hold / move / retreat order 1", lines[3]);
        Assert.Equal("fever recomputes: 30 asked for the decision values (a 0.05 step of the share) over 10.0 AI man-minutes on foot = 3.00 a man-minute; per fever 4.0 (max 5)", lines[4]);
        Assert.Equal("fever sprints: started 2 (fever left 100-67%: 1, 67-33%: 1, 33-0%: 0) in 9 checks; refused: missed the roll 1, reserve kept 0, no store 0, sprinting already 0, "
                     + "in melee 0, no enemy in range 1, sprint off 0", lines[5]);
        Assert.Equal("fever proof: AI melee attacks per man-minute in contact (an enemy within 5.0 m, on foot), by bar 100-75 / 75-50 / 50-25 / 25-0 %: in a formation, no fever "
                     + "6.00 / - / - / 2.00 (8 attacks over 2.0 man-min); in a fever 9.00 / - / - / - (9 attacks over 1.0 man-min) - in a fever x2.25 the rate without", lines[6]);
        Assert.Equal("fever proof, no formation (tournaments, arenas, duels - no orders, no fever): by bar 100-75 / 75-50 / 50-25 / 25-0 % - / 2.00 / - / - (1 attacks over 0.5 man-min)", lines[7]);
        Assert.Equal("fever values written (AI on foot, the game's → ours): attack chance bar 100-75% 0.144 → 0.086 (x0.60, 1), bar 75-50% -, bar 50-25% -, bar 25-0% -, in a fever 0.144 → 0.115 (x0.80, 1); "
                     + "riposte chance bar 100-75% 0.080 → 0.048 (x0.60, 1), bar 75-50% -, bar 50-25% -, bar 25-0% -, in a fever 0.080 → 0.064 (x0.80, 1)", lines[8]);
        Assert.StartsWith("fever cost: ", lines[9]);
    }
}

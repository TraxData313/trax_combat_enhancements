using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>Step 43 (Anton, 2026-10-17): the CALM (the AI's two attack-decision chances ÷ AiCalmFactor), the FEVER (a new charge / advance
/// order arms a store of current points ÷ 10 s at x3 the calm values, burning from the first contact, faster with a shield in hand,
/// fading in 10 steps back to calm), the fever sprint (a roll every 1 s at 50% × fever left), cheaper blows (format 8).</summary>
public class FeverTests
{
    private static FeverRules Rules(double calm = 3, bool fever = true, double pps = 10, double peak = 3, double contact = 5, double shield = 0.7,
        bool sprint = true, double min = 3, double max = 12, double check = 1, int chance = 50, bool mod = true, bool athletics = true) =>
        new(mod, athletics, fever, calm, pps, peak, contact, shield, sprint, min, max, check, chance);

    private static void Near(double expected, double actual, double eps = 1e-6) =>
        Assert.True(Math.Abs(expected - actual) <= eps, "expected " + expected + ", got " + actual);

    // ------------------------------------------------------------------ calm

    [Fact]
    public void Calm_divides_the_two_chances_with_no_floor_of_ours()
    {
        var r = Rules();
        float f = FeverMath.DecisionFactor(in r, FeverPhase.None, 0, 0, isPlayer: false);
        Near(1.0 / 3, f);
        Near(0.048, FeverMath.ApplyChance(0.144f, f), 1e-6);        // a charge's attack chance
        Near(0.032, FeverMath.ApplyChance(0.096f, f), 1e-6);        // other orders
        Near(0.05 / 3, FeverMath.ApplyChance(0.05f, f), 1e-6);      // a shield wall's (the game's own 0.05 clamp) - below 0.05: no floor
        Near(0.08 / 3, FeverMath.ApplyChance(0.08f, f), 1e-6);      // the riposte
        Assert.Equal(1f, FeverMath.ApplyChance(0.9f, 3f));            // clamped to 1
        Assert.Equal(0f, FeverMath.ApplyChance(-0.2f, 0.5f));          // never below 0
    }

    [Fact]
    public void Calm_1_the_player_and_the_switches_leave_the_games_values()
    {
        Assert.Equal(1f, FeverMath.DecisionFactor(Rules(calm: 1, fever: false), FeverPhase.None, 0, 0, false));
        Assert.Equal(1f, FeverMath.DecisionFactor(Rules(), FeverPhase.Armed, 8, 8, isPlayer: true));
        Assert.Equal(1f, FeverMath.DecisionFactor(Rules(mod: false), FeverPhase.Armed, 8, 8, false));        // the master switch first
        Assert.Equal(1f, FeverMath.DecisionFactor(Rules(athletics: false), FeverPhase.None, 0, 0, false));
        Assert.Equal(0.144f, FeverMath.ApplyChance(0.144f, 1f));
        Assert.False(Rules(calm: 1).CalmOn);
        Assert.Equal("ModEnabled", Rules(mod: false, athletics: false).OffBecause);
        Assert.Equal("ModEnabled", Rules(mod: false).CalmOffBecause);
        Assert.Equal("AiCalmFactor 1", Rules(calm: 1).CalmOffBecause);
        Assert.Equal("FeverEnabled", Rules(fever: false).OffBecause);
    }

    [Fact]
    public void Calm_combines_with_step_5e_multiplicatively()
    {
        // AttackRateAiDecisions on, a man at m 0.5: the game's 0.144 x 0.5 (5e) / 3 (calm) = 0.024, either order
        float f = FeverMath.DecisionFactor(Rules(), FeverPhase.None, 0, 0, false);
        float a = FeverMath.ApplyChance(AttackRateMath.ScaleChance(0.144f, 0.5f), f);
        float b = AttackRateMath.ScaleChance(FeverMath.ApplyChance(0.144f, f), 0.5f);
        Near(0.024, a, 1e-6);
        Near(a, b, 1e-7);
        // at the fever's peak the calm is undone, 5e's m stays
        float peak = FeverMath.DecisionFactor(Rules(), FeverPhase.Armed, 8, 8, false);
        Near(0.072, FeverMath.ApplyChance(AttackRateMath.ScaleChance(0.144f, 0.5f), peak), 1e-6);
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
    public void Armed_is_the_peak_and_does_not_burn_until_contact()
    {
        var r = Rules();
        var f = new FeverStore();
        f.Arm(in r, SprintOrder.Advance, 80, 0, false);
        Near(3, FeverMath.Multiplier(in r, f.Phase, f.Left, f.Full));
        Near(1, FeverMath.DecisionFactor(in r, f.Phase, f.Left, f.Full, false));   // calm 3 x peak 3 = the game's own
        Assert.False(f.Burn(in r, 30, shieldInHand: false));                        // 30 s marching: nothing spent
        Near(8, f.Left);
        Assert.True(f.StartBurn(31));
        Assert.Equal(FeverPhase.Burning, f.Phase);
        Assert.False(f.StartBurn(32));                                               // once
        Assert.False(f.Burn(in r, 1, false));
        Near(7, f.Left);
        Assert.False(f.Burn(in r, 1, true));                                         // a shield in hand: 1 / 0.7 s a second
        Near(7 - 1 / 0.7, f.Left);
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
    public void The_fade_is_a_straight_line_in_ten_steps_ending_at_calm()
    {
        var r = Rules();
        Near(3, FeverMath.Multiplier(in r, FeverPhase.Burning, 8, 8));
        Near(3, FeverMath.Multiplier(in r, FeverPhase.Burning, 7.6, 8));        // 0.95 → the first tenth holds the peak
        Near(2.8, FeverMath.Multiplier(in r, FeverPhase.Burning, 6.8, 8));      // 0.85 → 0.9
        Near(2.0, FeverMath.Multiplier(in r, FeverPhase.Burning, 4, 8));        // half
        Near(2.0, FeverMath.Multiplier(in r, FeverPhase.Burning, 4, 8, exact: true));
        Near(1.2, FeverMath.Multiplier(in r, FeverPhase.Burning, 0.01, 8));     // the last step runs to empty
        Near(1.0, FeverMath.Multiplier(in r, FeverPhase.Burning, 0, 8));
        Near(1.0, FeverMath.Multiplier(in r, FeverPhase.None, 0, 8));
        // the applied factor: calm 1/3 → 1 at the peak, 2/3 at half
        Near(2.0 / 3, FeverMath.DecisionFactor(in r, FeverPhase.Burning, 4, 8, false));
        // a whole burn at 0.25 s looks: 10 distinct values below the peak's arming → 11 recomputes with the arming and the end
        var f = new FeverStore();
        f.Arm(in r, SprintOrder.Charge, 80, 0, false);
        var seen = new List<float> { FeverMath.DecisionFactor(in r, FeverPhase.None, 0, 0, false) };
        void Look()
        {
            float now = FeverMath.DecisionFactor(in r, f.Phase, f.Left, f.Full, false);
            if (now != seen[^1]) seen.Add(now);
        }
        Look();
        f.StartBurn(0);
        while (!f.Burn(in r, 0.25, false)) Look();
        f.Stop();
        Look();
        Assert.Equal(12, seen.Count);               // calm, peak, 9 steps down, calm = 11 changes
        Near(1.0 / 3, seen[^1]);
        Near(1, seen[1]);
        for (int i = 2; i < seen.Count - 1; i++) Assert.True(seen[i] < seen[i - 1]);
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
        Near(1.0 / 3, FeverMath.DecisionFactor(in r, f.Phase, f.Left, f.Full, false));
        Assert.Equal(FeverArm.Off, new FeverStore().Arm(Rules(mod: false), SprintOrder.Charge, 80, 0, false));
        Assert.Equal(FeverArm.Off, new FeverStore().Arm(Rules(fever: false), SprintOrder.Charge, 80, 0, false));
        Assert.Equal(FeverArm.Off, new FeverStore().Arm(Rules(athletics: false), SprintOrder.Charge, 80, 0, false));
        // the fever off: the multiplier is 1 even for a stale burning store
        var g = new FeverStore();
        g.Arm(in r, SprintOrder.Charge, 80, 0, false);
        Near(1, FeverMath.Multiplier(Rules(fever: false), g.Phase, g.Left, g.Full));
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
        Assert.True(r.CalmOn);
        Assert.True(r.Enabled);
        Near(3, r.CalmFactor);
        Near(10, r.PointsPerSecond);
        Near(0.7, r.ShieldDurationMultiplier, 1e-6);
        Assert.Equal(50, r.SprintChancePercent);
        t.Set(SettingsSchema.AiCalmFactor, 1.5, SettingSources.Mcm);
        t.Set(SettingsSchema.FeverAggressionMultiplier, 2, SettingSources.Mcm);
        r = FeverRules.From(t);
        Near(1.5, r.CalmFactor);
        Near(2 / 1.5, FeverMath.DecisionFactor(in r, FeverPhase.Armed, 1, 1, false), 1e-6);
        Assert.Equal(SettingsSchema.SprintGroup.Order + 1, SettingsSchema.FeverGroup.Order);
        Assert.Same(SettingsSchema.AdvancedGroup, SettingsSchema.Groups[SettingsSchema.Groups.Count - 1]);
        Assert.Same(SettingsSchema.FeverGroup, SettingsSchema.AiCalmFactor.Group);
        Assert.Same(SettingsSchema.FeverGroup, SettingsSchema.FeverSprintChancePercent.Group);
    }

    [Fact]
    public void The_sentence_names_the_numbers()
    {
        string d = Rules().Describe();
        Assert.StartsWith("calm: AI men on foot decide to attack 3.00x less often (AiCalmFactor - the game's attack and riposte chances ÷ 3.00, e.g. a charge's attack chance 0.144 → 0.048)", d);
        Assert.Contains("CURRENT Athletics points / 10.0 s (FeverPointsPerSecond - 80 points = 8.0 s) at x3.00 the calm values (FeverAggressionMultiplier: x1.00 the game's - attack chance 0.144 → 0.144)", d);
        Assert.Contains("within 5.0 m (FeverContactMetres), burns x1.43 faster with a shield in hand", d);
        Assert.Contains("fever sprint: every 1.0 s a feverish man with sprint above his order's reserve and an enemy 3.0-12.0 m away sprints with chance 50% x the fever left", d);
        Assert.Equal("calm: off (AiCalmFactor 1) - the game's own attack decisions; fever: off (FeverEnabled)", Rules(calm: 1, fever: false).Describe());
    }

    /// <summary>Step 43's shipped defaults (defaults.json) - the tests run on DESIGN's initial 10 / 5, where there is nothing to push.</summary>
    private static double Shipped43(ParamDef p) =>
        p == SettingsSchema.CostPerBlow ? 3 : p == SettingsSchema.CostPerWrongSideShieldBlock ? 2 : p.Default;

    [Fact]
    public void A_format_7_file_at_the_old_blow_and_wrong_side_costs_gets_3_and_2_once_tuned_values_kept()
    {
        Assert.Equal(8, ConfigFile.FormatVersion);
        var read = ConfigFile.Read("{ \"ConfigVersion\": 7, \"CostPerBlow\": 10.0, \"CostPerWrongSideShieldBlock\": 5.0, \"CostPerShieldBlock\": 1.0, \"CostPerWeaponParry\": 2.0 }");
        var notes = ConfigFile.Migrate(read, Shipped43);
        Assert.Equal(2, notes.Count);
        Assert.Contains(notes, n => n.StartsWith("CostPerBlow: 10.0 → 3.0 (format 7 → 8, the old default; step 43, Anton: a swing or shot costs 3 Athletics", StringComparison.Ordinal));
        Assert.Contains(notes, n => n.StartsWith("CostPerWrongSideShieldBlock: 5.0 → 2.0 (format 7 → 8, the old default; step 43", StringComparison.Ordinal));
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
        // a format-8 file's 10 is his own now
        var eight = ConfigFile.Read("{ \"ConfigVersion\": 8, \"CostPerBlow\": 10.0, \"CostPerWrongSideShieldBlock\": 5.0 }");
        Assert.Empty(ConfigFile.Migrate(eight, Shipped43));
        Assert.Equal(10, eight.Values["CostPerBlow"], 9);
        // an older file moves too (both defaults go back to steps 5c / 22)
        var five = ConfigFile.Read("{ \"ConfigVersion\": 5, \"CostPerBlow\": 10.0, \"CostPerWrongSideShieldBlock\": 5.0 }");
        Assert.Equal(2, ConfigFile.Migrate(five, Shipped43).Count);
        // on DESIGN's initial values nothing moves (the default IS the old one)
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 7, \"CostPerBlow\": 10.0 }")));
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
        st.AddEnd(FeverEnd.Faded, true, 5.6, 0, 8, false, 11);
        st.AddEnd(FeverEnd.Faded, true, 0, 8, 8, true, 11);
        st.AddEnd(FeverEnd.OrderEnded, false, 0, 0, 0, false, 2);
        st.SprintChecks = 9;
        st.AddSprint(0.9);
        st.AddSprint(0.5);
        st.AddRefusal(FeverSprintRefusal.MissedRoll);
        st.AddRefusal(FeverSprintRefusal.NoEnemyInRange);
        st.AddContactTime(false, 120);
        st.AddContactTime(true, 60);
        for (int i = 0; i < 6; i++) st.AddAttack(false);
        for (int i = 0; i < 9; i++) st.AddAttack(true);
        st.AddValues(FeverValueState.Calm, 0.144f, 0.048f, 0.08f, 0.0267f);
        st.AddValues(FeverValueState.Peak, 0.144f, 0.144f, 0.08f, 0.08f);
        var lines = new List<string>();
        st.AddSummaryLines(lines, in r);
        Assert.Equal(9, lines.Count);
        Assert.StartsWith("fever settings: calm: AI men on foot", lines[0]);
        Assert.Equal("fever armed: 3 (charge 2 - by you 1 / by the AI general 1; advance 1 - by you 0 / by the AI general 1); a different aggressive order on a running fever: "
                     + "re-armed to a fresh one 1, kept (more left than a fresh one) 0; the same order again ignored 2; not armed: on a horse 3, no Athletics 0, player-controlled 0", lines[1]);
        Assert.Equal("fever contact: 2 of 3 fevers met an enemy within 5.0 m - armed → contact mean 7.0 s, max 10.0 s; never met one (ended still armed): 1 (a hold / move / retreat order 1)", lines[2]);
        Assert.Equal("fever burn: faded 2; burn length with a shield in hand (most of it) 1, mean 5.6 s from a mean store of 8.0 s (x0.70); without 1, mean 8.0 s from a mean store of 8.0 s (x1.00) "
                     + "(FeverShieldDurationMultiplier 0.70 - a shield man's fever lasts that share); a shield taken up or put away mid-burn 1; all ends: faded 2, a hold / move / retreat order 1", lines[3]);
        Assert.Equal("fever fade: 8.0 decision recomputes per fever (max 11) - the arming, 9 steps down a full fade, the end = 11 for a fever that faded", lines[4]);
        Assert.Equal("fever sprints: started 2 (fever left 100-67%: 1, 67-33%: 1, 33-0%: 0) in 9 checks; refused: missed the roll 1, reserve kept 0, no store 0, sprinting already 0, "
                     + "in melee 0, no enemy in range 1, sprint off 0", lines[5]);
        Assert.Equal("fever proof: AI melee attacks per man-minute in contact (an enemy within 5.0 m, on foot): calm 3.00 (6 attacks over 2.0 man-min), feverish (armed or burning) 9.00 "
                     + "(9 attacks over 1.0 man-min) - feverish x3.00 the calm rate", lines[6]);
        Assert.Equal("fever values written (AI on foot): attack chance calm 0.144 → 0.048 (x0.33, 1 recomputes), peak 0.144 → 0.144 (x1.00, 1 recomputes), mid-fade -; "
                     + "riposte chance calm 0.080 → 0.027 (x0.33, 1 recomputes), peak 0.080 → 0.080 (x1.00, 1 recomputes), mid-fade -", lines[7]);
        Assert.StartsWith("fever cost: ", lines[8]);
    }
}

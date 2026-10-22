using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>Step 49 (Anton, 2026-10-17 night): COURAGE AND COHESION - toughness-weighted local odds against the odds a man needs (tiredness,
/// morale, shock, fever, hero), timid men walking back to the nearest bigger group of their side or holding among their mates, the
/// hysteresis, the walk's direction (round the enemy), the sprint back and the panic, and the worked examples of AI_NOTES "Step 49".</summary>
public class CourageTests
{
    private static CourageRules Rules(bool mod = true, bool on = true, bool athletics = true, double radius = 6, int baseOdds = 100, int armour = 200,
        int level = 100, int health = 50, int tired = 100, int morale = 100, int fever = 40, int hero = 75, int hysteresis = 25, double minTimid = 2,
        double regroup = 5, int mates = 4, bool sprint = true, double sprintMetres = 12, int sprintEnemies = 3, int keep = 50, bool weights = true,
        double reach = 2, double shockSeconds = 6, int shock = 100, int heavy = 150, double flock = 20, int flockMin = 6, int panic = 50, int panicKeep = 0,
        int surge = 5, double surgeSeconds = 4) =>
        new(mod, on, athletics, radius, baseOdds, armour, level, health, tired, morale, fever, hero, hysteresis, minTimid, regroup, mates, sprint, sprintMetres,
            sprintEnemies, keep, weights, reach, shockSeconds, shock, heavy, flock, flockMin, panic, panicKeep, surge, surgeSeconds);

    private static void Near(double expected, double actual, double eps = 1e-3) =>
        Assert.True(Math.Abs(expected - actual) <= eps, "expected " + expected + ", got " + actual);

    private static readonly CourageRules R = Rules();
    private static double Looter => CourageMath.BaseToughness(in R, CourageMath.ExampleLooterArmour, CourageMath.ExampleLooterLevel);
    private static double Legionary => CourageMath.BaseToughness(in R, CourageMath.ExampleLegionaryArmour, CourageMath.ExampleLegionaryLevel);

    // ------------------------------------------------------------------ toughness

    [Fact]
    public void A_looter_is_about_a_quarter_of_an_armoured_veteran()
    {
        Near(1.16 * (1 + 5 / 30.0), Looter);                     // 1.353
        Near(3.2 * (1 + 26 / 30.0), Legionary);                   // 5.973
        double ratio = Legionary / Looter;
        Assert.InRange(ratio, 4.0, 5.0);                          // Anton: "looters against all-metal men need 4-5:1"
        // levels above 60 count as 60; no armour, no level = 1
        Near(CourageMath.BaseToughness(in R, 0, 60), CourageMath.BaseToughness(in R, 0, 90));
        Near(1, CourageMath.BaseToughness(in R, 0, 0));
        Near(1, CourageMath.BaseToughness(Rules(armour: 0, level: 0), 110, 26)); // both weights 0 = everyone equal
    }

    [Fact]
    public void Wounds_take_off_the_health_weight_share()
    {
        Near(Legionary * 0.75, CourageMath.Toughness(in R, Legionary, 0.5));
        Near(Legionary * 0.5, CourageMath.Toughness(in R, Legionary, 0));
        Near(Legionary, CourageMath.Toughness(in R, Legionary, 1.5));        // clamped
        Near(Legionary, CourageMath.Toughness(Rules(health: 0), Legionary, 0.1));
    }

    [Fact]
    public void An_enemy_with_a_two_hander_weighs_half_again()
    {
        Near(Legionary * 1.5, CourageMath.EnemyToughness(in R, Legionary, true));
        Near(Legionary, CourageMath.EnemyToughness(in R, Legionary, false));
        Near(Legionary, CourageMath.EnemyToughness(Rules(heavy: 100), Legionary, true));
    }

    [Fact]
    public void Neighbours_count_in_full_to_half_the_radius_then_fade()
    {
        Near(1, CourageMath.Weight(0, 6));
        Near(1, CourageMath.Weight(3, 6));
        Near(0.5, CourageMath.Weight(4.5, 6));
        Near(0, CourageMath.Weight(6, 6));
        Near(0, CourageMath.Weight(7, 6));
    }

    [Fact]
    public void The_odds_are_his_side_over_the_enemy_and_no_enemy_is_nothing_to_fear()
    {
        Near(0.5, CourageMath.Odds(2, 4));
        Assert.True(double.IsPositiveInfinity(CourageMath.Odds(2, 0)));
        Assert.False(CourageMath.Timid(CourageMath.Odds(1, 0), 5));
    }

    // ------------------------------------------------------------------ the odds he needs

    [Fact]
    public void Required_odds_terms_multiply()
    {
        Near(1, CourageMath.Required(in R, 1, 50, 0, false, out var p));
        Near(1, p.Tired * p.Morale * p.Fever * p.Hero * p.Shock);
        Near(2, CourageMath.Required(in R, 0, 50, 0, false, out _));          // empty: twice
        Near(1.5, CourageMath.Required(in R, 0.5, 50, 0, false, out _));
        Near(1.5, CourageMath.Required(in R, 1, 25, 0, false, out _));        // morale 25: 1.5
        Near(2, CourageMath.Required(in R, 1, 0, 0, false, out _));
        Near(1, CourageMath.Required(in R, 1, 90, 0, false, out _));          // high morale adds nothing
        Near(1, CourageMath.Required(in R, 1, -1, 0, false, out _));          // no AI component
        Near(0.4, CourageMath.Required(in R, 1, 50, 1, false, out _));        // a full fever
        Near(0.7, CourageMath.Required(in R, 1, 50, 0.5, false, out _));
        Near(0.75, CourageMath.Required(in R, 1, 50, 0, true, out _));        // a hero
        Near(2, CourageMath.Required(in R, 1, 50, 0, false, 1, out _));      // full shock doubles
        Near(4.0 / 3, CourageMath.Required(in R, 1, 50, 0, false, CourageMath.ShockShare(in R, 1), out _));
        // all together: empty, morale 25, a half fever, a hero, full shock = 1 x 2 x 1.5 x 0.7 x 0.75 x 2
        Near(2 * 1.5 * 0.7 * 0.75 * 2, CourageMath.Required(in R, 0, 25, 0.5, true, 1, out _));
        // Athletics off: tiredness counts 0
        Near(1, CourageMath.Required(Rules(athletics: false), 0, 50, 0, false, out _));
    }

    [Fact]
    public void Shock_counts_landed_hits_in_its_window_up_to_three()
    {
        Near(0, CourageMath.ShockShare(in R, 0));
        Near(1 / 3.0, CourageMath.ShockShare(in R, 1));
        Near(1, CourageMath.ShockShare(in R, 3));
        Near(1, CourageMath.ShockShare(in R, 7));
        Near(0, CourageMath.ShockShare(Rules(shockSeconds: 0), 3));
        var h = new ShockTimes();
        h.Note(10);
        h.Note(12);
        Assert.Equal(2, h.Recent(13, 6));
        Assert.Equal(1, h.Recent(16.5, 6));
        Assert.Equal(0, h.Recent(18.1, 6));
        for (int i = 0; i < 6; i++) h.Note(20 + i * 0.1);
        Assert.Equal(4, h.Recent(21, 6));                          // the newest four kept
    }

    [Fact]
    public void Fever_share_is_full_while_armed_and_fades_while_burning()
    {
        Near(1, CourageMath.FeverShare(FeverPhase.Armed, 10, 10));
        Near(0.3, CourageMath.FeverShare(FeverPhase.Burning, 3, 10));
        Near(0, CourageMath.FeverShare(FeverPhase.None, 3, 10));
    }

    [Fact]
    public void Panic_is_shocked_and_below_half_the_required()
    {
        Assert.True(CourageMath.Panic(in R, 0.34, 0.6, 1.33));
        Assert.False(CourageMath.Panic(in R, 0, 0.1, 1.33));        // not shocked: timid, never panic
        Assert.False(CourageMath.Panic(in R, 0.34, 0.7, 1.33));
        Assert.False(CourageMath.Panic(Rules(panic: 0), 1, 0.01, 1));
    }

    // ------------------------------------------------------------------ the mind

    [Fact]
    public void Far_and_alone_he_walks_back_among_mates_he_holds()
    {
        var m = new CourageMind();
        Assert.Equal(CourageStep.TimidMoving, m.Look(in R, 0, 0.1, 1, 15, 0));
        Assert.Equal(CouragePhase.Moving, m.Phase);
        var m2 = new CourageMind();
        Assert.Equal(CourageStep.TimidHolding, m2.Look(in R, 0, 0.1, 1, 15, 4)); // 4 mates
        var m3 = new CourageMind();
        Assert.Equal(CourageStep.TimidHolding, m3.Look(in R, 0, 0.1, 1, 4, 0));  // within 5 m of the centre
        var m4 = new CourageMind();
        Assert.Equal(CourageStep.TimidHolding, m4.Look(Rules(mates: 0), 0, 0.1, 1, 15, 0)); // 0 = never walks
        // shocked: twice the mates, half the radius
        var m5 = new CourageMind();
        Assert.Equal(CourageStep.TimidMoving, m5.Look(in R, 0, 0.1, 1, 3, 5, shocked: true));
    }

    [Fact]
    public void The_walk_ends_before_courage_comes_back()
    {
        var m = new CourageMind();
        m.Look(in R, 0, 0.1, 1, 15, 0);
        // out of the enemy's reach (odds infinite) after 3 s - still walking: back with his group first
        Assert.Equal(CourageStep.None, m.Look(in R, 3, double.PositiveInfinity, 1, 10, 0));
        Assert.Equal(CouragePhase.Moving, m.Phase);
        Assert.Equal(CourageStep.StopMoving, m.Look(in R, 4, double.PositiveInfinity, 1, 4.9, 0));
        Assert.Equal(RegroupEnd.Arrived, m.LastRegroupEnd);
        Near(4, m.LastMovingSeconds);
        Assert.Equal(CourageStep.Brave, m.Look(in R, 4.5, double.PositiveInfinity, 1, 4.9, 0));
        // among mates
        var a = new CourageMind();
        a.Look(in R, 0, 0.1, 1, 15, 0);
        Assert.Equal(CourageStep.StopMoving, a.Look(in R, 1, 0.1, 1, 12, 4));
        Assert.Equal(RegroupEnd.AmongMates, a.LastRegroupEnd);
        // too long
        var b = new CourageMind();
        b.Look(in R, 0, 0.1, 1, 15, 0);
        Assert.Equal(CourageStep.StopMoving, b.Look(in R, CourageMath.MaxMovingSeconds, 0.1, 1, 15, 0));
        Assert.Equal(RegroupEnd.TookTooLong, b.LastRegroupEnd);
    }

    [Fact]
    public void Brave_again_needs_the_hysteresis_and_the_minimum_hold()
    {
        var m = new CourageMind();
        m.Look(in R, 0, 0.9, 1, 1, 6);                              // timid, holding
        Assert.Equal(CourageStep.None, m.Look(in R, 1, 2, 1, 1, 6));     // odds back but only 1 s
        Assert.Equal(CourageStep.None, m.Look(in R, 2.5, 1.2, 1, 1, 6)); // 1.2 < 1.25
        Assert.Equal(CourageStep.Brave, m.Look(in R, 3, 1.25, 1, 1, 6));
        Assert.False(m.Timid);
        // a flip within 2 s of the last = a wobble
        Assert.Equal(CourageStep.TimidHolding, m.Look(in R, 4, 0.5, 1, 1, 6));
        Assert.Equal(1, m.Wobbles);
    }

    [Fact]
    public void A_holding_man_left_alone_far_out_walks_again()
    {
        var m = new CourageMind();
        m.Look(in R, 0, 0.1, 1, 15, 5);                             // holding among 5
        Assert.Equal(CourageStep.None, m.Look(in R, 1, 0.1, 1, 6.5, 0)); // within 5 + 2 m: no wobble at the edge
        Assert.Equal(CourageStep.StartMoving, m.Look(in R, 2, 0.1, 1, 7.5, 0));
        m.End(3);
        Assert.False(m.Timid);
        Assert.Equal(RegroupEnd.Ended, m.LastRegroupEnd);
    }

    // ------------------------------------------------------------------ the walk

    [Fact]
    public void The_walk_heads_for_the_centre_leaning_away_from_the_enemy_and_goes_round_when_they_stand_between()
    {
        // centre south, enemy north: straight south
        Assert.True(CourageMath.Direction(0, -10, 0, -1, out double x, out double y));
        Near(0, x);
        Near(-1, y);
        // centre west, enemy north: west, leaning south
        CourageMath.Direction(-10, 0, 0, -1, out x, out y);
        Assert.True(x < -0.8 && y < -0.3);
        // a man who looped behind a wall: the centre lies beyond the enemy (north), away = south → he goes round (east or west), never through
        CourageMath.Direction(0, 10, 0, -1, out x, out y);
        Assert.True(Math.Abs(x) > 0.85, "not round the enemy: " + x + ", " + y);
        // nothing to go by
        Assert.False(CourageMath.Direction(0, 0, 0, 0, out _, out _));
    }

    [Fact]
    public void The_sprint_back_keeps_its_reserve_and_a_panic_keeps_almost_nothing()
    {
        Assert.True(CourageMath.SprintWanted(in R, 13, 0));
        Assert.True(CourageMath.SprintWanted(in R, 5, 3));
        Assert.False(CourageMath.SprintWanted(in R, 5, 2));
        Assert.True(CourageMath.SprintWanted(in R, 5, 0, panic: true));
        Assert.False(CourageMath.SprintWanted(Rules(sprint: false), 50, 9, true));
        Near(2.5, CourageMath.SprintAmount(in R, 5, 5));
        Near(5, CourageMath.SprintAmount(in R, 5, 5, panic: true));
        Near(0, CourageMath.SprintAmount(in R, 2.55, 5));          // 0.05 s above: below the AI's 0.2 s minimum
        Near(1, CourageMath.SprintAmount(Rules(panicKeep: 20), 2, 5, panic: true));
    }

    [Fact]
    public void The_timid_melee_curve_falls_below_go_to_position_just_past_his_reach()
    {
        CourageMath.TimidMelee(2, out float y1, out float x2, out float y2, out float x3, out float y3);
        double goAt2 = CourageMath.CurveAt(2, CourageMath.TimidGoToY1, CourageMath.TimidGoToX2, CourageMath.TimidGoToY2, CourageMath.TimidGoToX3, CourageMath.TimidGoToY3);
        Assert.True(CourageMath.CurveAt(1.5, y1, x2, y2, x3, y3) > goAt2);  // within reach: fights
        Assert.True(CourageMath.CurveAt(2, y1, x2, y2, x3, y3) > goAt2);
        double goAt3 = CourageMath.CurveAt(3, CourageMath.TimidGoToY1, CourageMath.TimidGoToX2, CourageMath.TimidGoToY2, CourageMath.TimidGoToX3, CourageMath.TimidGoToY3);
        Assert.True(CourageMath.CurveAt(3, y1, x2, y2, x3, y3) < goAt3);    // a metre beyond: keeps his place
        // step 49b: within his reach Melee is at FULL - never below vanilla's own 8 at 0 m
        for (double d = 0; d <= 2.0001; d += 0.25) Assert.True(CourageMath.CurveAt(d, y1, x2, y2, x3, y3) >= 8, "melee below vanilla's 8 at " + d + " m");
        // vanilla's DefaultMove melee still beats its GoToPos at 6 m - why men hunt: 8 → 5 at 7 m vs 3 → 5 at 7 m
        Assert.True(CourageMath.CurveAt(6, 8, 7, 5, 20, 0.01) > CourageMath.CurveAt(6, 3, 7, 5, 20, 6));
    }

    // ------------------------------------------------------------------ the worked examples (AI_NOTES "Step 49", HOW-IT-WORKS)

    private static double Odds(int allies, double allyT, int enemies, double enemyT) => CourageMath.Odds(allies * allyT, enemies * enemyT);

    [Fact]
    public void Example_40_looters_against_a_20_man_metal_wall_almost_nobody_flanks()
    {
        // the wall: 2 ranks of 10; a FRONT looter has 13 mates and 11 legionaries within reach; a FLANK looter at the wall's end 3 mates vs 6
        double front = Odds(14, Looter, 11, Legionary);
        double flank = Odds(4, Looter, 6, Legionary);
        Near(0.288, front, 0.005);
        Near(0.151, flank, 0.005);
        // step 49b: in the charge's fever, not hit, they PRESS - the first clash happens …
        Assert.True(CourageMath.FeverPresses(1, shocked: false));
        // … a looter HIT in the clash falls back even in his fever (0.29 < 0.4 x 1.33) …
        Assert.True(CourageMath.Timid(front, CourageMath.Required(in R, 1, 50, 1, false, CourageMath.ShockShare(in R, 1), out _)));
        // … and once the fever is out they line up timid opposite the wall, holding among their mates …
        Assert.True(CourageMath.Timid(front, 1));
        Assert.Equal(CourageStep.TimidHolding, new CourageMind().Look(in R, 0, front, 1, 3, 13)); // among 13 mates: holds
        // … and now and then one surges in: 5% a look x his bar - about one fresh man in 20 each half second
        Assert.True(CourageMath.SurgeEligible(in R, true, false, 13, 2));
        Near(0.05, CourageMath.SurgeChance(in R, 1));
        Assert.Equal(CourageStep.TimidMoving, new CourageMind().Look(in R, 0, flank, 1, 12, 3));  // 3 mates, 12 m from the mass: walks back
    }

    [Fact]
    public void Example_200_looters_wrap_the_flanks_as_a_mass()
    {
        double front = Odds(25, Looter, 11, Legionary);
        double flank = Odds(30, Looter, 6, Legionary);
        Near(0.515, front, 0.005);
        Near(1.133, flank, 0.005);
        Assert.True(CourageMath.Timid(front, 1));                  // the front holds once the fever is out…
        Assert.True(CourageMath.FeverPresses(0.5, false));         // …and presses while the charge's fever burns (step 49b: fever beats the odds)
        Assert.False(CourageMath.Timid(flank, 1));                 // the flanks wrap - as a crowd of 30, never one by one
        Assert.True(CourageMath.Timid(Odds(5, Looter, 6, Legionary), 1)); // five who split off alone: timid, back to the mass
    }

    [Fact]
    public void Example_equal_troops_need_about_two_to_one_to_envelop()
    {
        Assert.False(CourageMath.Timid(Odds(11, Legionary, 11, Legionary), 1)); // a line against a line at even odds: brave, they fight
        Assert.False(CourageMath.Timid(Odds(10, Legionary, 6, Legionary), 1));  // 40 vs 20: 10 men on each wall end vs 6 - they wrap
        Assert.True(CourageMath.Timid(Odds(5, Legionary, 6, Legionary), 1));    // 30 vs 20: 5 per end - they do not
    }

    [Fact]
    public void Example_fresh_shock_troops_with_great_axes_break_a_tired_hit_flank()
    {
        // a flank man of a tired line (bar 30%), two hits in the last 6 s, 3 mates within reach; 4 fresh two-hander men hit them
        double odds = CourageMath.Odds(4 * Legionary, 4 * CourageMath.EnemyToughness(in R, Legionary, true));
        Near(0.667, odds, 0.005);
        double req = CourageMath.Required(in R, 0.3, 50, 0, false, CourageMath.ShockShare(in R, 2), out _);
        Near(1.7 * (1 + 2 / 3.0), req, 0.005);                     // 2.83
        Assert.True(CourageMath.Panic(in R, CourageMath.ShockShare(in R, 2), odds, req)); // PANIC: sprints back keeping 0%
        Assert.Equal(CourageStep.TimidMoving, new CourageMind().Look(in R, 0, odds, req, 6, 3, shocked: true)); // 3 < 8 mates: back to the bigger group
        // a fresh, unhit man of the same line facing the same four: timid, but no panic - and with 4 mates he holds
        double fresh = CourageMath.Required(in R, 1, 50, 0, false, 0, out _);
        Assert.True(CourageMath.Timid(odds, fresh) && !CourageMath.Panic(in R, 0, odds, fresh));
        Assert.Equal(CourageStep.TimidHolding, new CourageMind().Look(in R, 0, odds, fresh, 6, 4));
    }

    // ------------------------------------------------------------------ step 49b: the fever press, the surge, the standoff

    [Fact]
    public void Fever_overrides_the_odds_but_not_shock()
    {
        Assert.True(CourageMath.FeverPresses(1, false));           // armed
        Assert.True(CourageMath.FeverPresses(0.2, false));         // burning
        Assert.False(CourageMath.FeverPresses(0, false));          // no fever
        Assert.False(CourageMath.FeverPresses(1, true));           // hit: the fever no longer carries him
    }

    [Fact]
    public void The_surge_roll_its_length_and_the_bar_share()
    {
        Assert.True(CourageMath.SurgeEligible(in R, true, false, 4, 7.9));
        Assert.False(CourageMath.SurgeEligible(in R, false, false, 4, 2));  // brave already
        Assert.False(CourageMath.SurgeEligible(in R, true, true, 4, 2));    // shocked
        Assert.False(CourageMath.SurgeEligible(in R, true, false, 3, 2));   // too few mates
        Assert.False(CourageMath.SurgeEligible(in R, true, false, 6, 8.5)); // no enemy within 8 m
        Assert.False(CourageMath.SurgeEligible(Rules(surge: 0), true, false, 6, 2));
        Near(0.05, CourageMath.SurgeChance(in R, 1));
        Near(0.025, CourageMath.SurgeChance(in R, 0.5));           // tired men surge less
        Near(0, CourageMath.SurgeChance(in R, 0));
        Near(0.05, CourageMath.SurgeChance(Rules(athletics: false), 0)); // Athletics off: no bar to scale by
        Assert.True(CourageMath.SurgeRoll(0.05, 0.049));
        Assert.False(CourageMath.SurgeRoll(0.05, 0.05));
        var m = new CourageMind();
        m.SurgeUntil = 10 + R.SurgeSeconds;
        Assert.True(m.Surging(13.9));
        Assert.False(m.Surging(14));
        Near(4, R.SurgeSeconds);
    }

    [Fact]
    public void The_standoff_is_timid_men_3_to_8_m_off_with_no_attack_in_10_s()
    {
        Assert.True(CourageMath.Standoff(true, 5, -1, 100));
        Assert.True(CourageMath.Standoff(true, 5, 90, 100));
        Assert.False(CourageMath.Standoff(true, 5, 91, 100));      // someone swung 9 s ago
        Assert.False(CourageMath.Standoff(true, 2, -1, 100));      // in contact - a fight
        Assert.False(CourageMath.Standoff(true, 9, -1, 100));
        Assert.False(CourageMath.Standoff(false, 5, -1, 100));
    }

    // ------------------------------------------------------------------ switches, settings, summary

    [Fact]
    public void The_master_switch_comes_first()
    {
        Assert.False(Rules(mod: false).Enabled);
        Assert.Equal("ModEnabled", Rules(mod: false, on: false).OffBecause);
        Assert.Equal("CourageEnabled", Rules(on: false).OffBecause);
        Assert.True(Rules(athletics: false).Enabled);              // works without Athletics
        Assert.StartsWith("courage OFF (ModEnabled)", Rules(mod: false).Describe());
    }

    [Fact]
    public void The_settings_sit_in_their_group_after_the_fever_and_are_live()
    {
        Assert.Equal(SettingsSchema.FeverGroup.Order + 1, SettingsSchema.CourageGroup.Order);
        Assert.Equal(SettingsSchema.CourageGroup.Order + 1, SettingsSchema.CavalryFlockGroup.Order); // step 55: cavalry flocking right after courage
        var keys = SettingsSchema.InGroup(SettingsSchema.CourageGroup).Select(p => p.Key).ToList();
        Assert.Equal(85, keys.Count);                                           // the 29th, CourageEnabled, on the player page (step 59); step 67: + 25 (the fear flock); step 69: + 6 (winning calms); step 70: + 11 (the band, the strike, the lone runner); step 77: + 15 (press, don't pass)
        Assert.Equal("CourageRadiusMetres", keys[0]);
        Assert.Same(SettingsSchema.PlayerAiGroup, SettingsSchema.CourageEnabled.Group);
        Assert.All(SettingsSchema.InGroup(SettingsSchema.CourageGroup), p => Assert.Equal(ApplyTiming.Live, p.Timing));
    }

    [Fact]
    public void The_sentence_names_the_examples()
    {
        string d = R.Describe();
        Assert.Contains("a looter (armour 8, level 5) 1.35, an imperial legionary (armour 110, level 26) 5.97 = 4.4 looters", d);
        Assert.Contains("PANIC", d);
        Assert.Contains("nearest bigger group", d);
    }

    [Fact]
    public void The_summary_lines()
    {
        var s = new CourageStats();
        var lines = new List<string>();
        s.AddSummaryLines(lines, in R);
        Assert.Equal(2, lines.Count);
        Assert.StartsWith("courage: no AI man", lines[1]);
        s.MenLooked = 10;
        s.AddLook(true, 30, true, true);
        s.AddLook(true, 30, false, true);
        s.AddLook(false, 60, true, true);
        for (int i = 0; i < 9; i++) s.AddDistance(true, 2.5);
        s.AddDistance(true, 30);
        s.AddDistance(false, 10);
        s.AddOdds(0.1, true, false);
        s.AddOdds(3, false, true);
        s.TimidStarts = 3;
        s.TimidByShock = 1;
        s.AddRegroupEnd(RegroupEnd.Arrived, 4);
        s.AddCompression(true, 10, 3);
        s.AddEnd(CourageEnd.Brave);
        lines.Clear();
        s.AddSummaryLines(lines, in R);
        Assert.Equal(11, lines.Count);
        Assert.Contains("ON 30.00 man-seconds per AI man-minute", lines[1]);
        Assert.Contains("OFF 60.00", lines[1]);
        Assert.Contains("ON mean 5.3 m, p90 3 m (10 looks); OFF mean 10.0 m, p90 11 m (1 looks)", lines[2]);
        Assert.Contains("caused by SHOCK (a landed hit within the shock window) 1, by the odds alone 2", lines[3]);
        Assert.Contains("arrived (within the regroup radius) 1", lines[4]);
        Assert.Contains("shocked 10.0 → 3.0 m (1 walks)", lines[5]);
        Assert.Contains("fever presses overriding timidity 0 looks", lines[6]);
        Assert.Contains("< 0.25: 100% timid of 1", lines[7]);
        Assert.Contains(">= 2: 0% timid of 1", lines[8]);
    }
}

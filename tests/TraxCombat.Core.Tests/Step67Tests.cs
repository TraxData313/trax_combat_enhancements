using System;
using System.Collections.Generic;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 67 - MORALE EYES + THE FEAR FLOCK (Anton 2026-10-19: "give me eyes on their morale too"; "as troops lose their morale and freshness,
/// they flock less to their formation position and more to the centre of their unit's mass, but pushed back away from the enemy mass ...
/// stepping behind their comrades"; AI_NOTES "Step 67", DESIGN decision 60): the settings, the fear (morale, tiredness, discipline, the thin
/// line, the unit's share), THE WORKED EXAMPLE (looters vs legionaries at fresh / half / spent × morale 80 / 50 / 20), the pull, his place,
/// the shelter and its drift cap, the cover / anchor / behind tests, the charge's loose mass, the sprint, the wall's break, the unit table,
/// the summary; the morale bar in the ALT stack and the near formations' morale line.
/// </summary>
public class Step67Tests
{
    private static FearRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return FearRules.From(s);
    }

    private static void Near(double expected, double actual, double tol = 1e-3) => Assert.True(Math.Abs(expected - actual) <= tol, "expected " + expected + ", got " + actual);

    private const int LooterLevel = 5, LegionaryLevel = 26;

    [Fact]
    public void The_fear_settings_follow_courage_and_the_morale_eyes_follow_the_ALT_cap_on_the_Advanced_page_live()
    {
        var all = SettingsSchema.All.ToList();
        var fear = new[]
        {
            SettingsSchema.FearFlockEnabled, SettingsSchema.FearMoraleCalm, SettingsSchema.FearMoraleBroken, SettingsSchema.FearFreshPercent,
            SettingsSchema.FearMoraleWeightPercent, SettingsSchema.FearTiredWeightPercent, SettingsSchema.DisciplineLevelLow, SettingsSchema.DisciplineLevelHigh,
            SettingsSchema.DisciplineMaxPercent, SettingsSchema.FearUnitSharePercent, SettingsSchema.FearThinLinePercent, SettingsSchema.FearStartPercent,
            SettingsSchema.FearFullPercent, SettingsSchema.FearShelterBackMetres, SettingsSchema.FearShelterMaxMetres, SettingsSchema.FearDriftMetresPerMinute,
            SettingsSchema.FearDriftHoldPercent, SettingsSchema.FearCoverWidthMetres, SettingsSchema.FearAnchorFreshPercent, SettingsSchema.FearAnchorSeconds,
            SettingsSchema.FearSprintMetres, SettingsSchema.FearSprintChancePercent, SettingsSchema.FearSprintKeepPercent, SettingsSchema.FearWallBreakMorale,
            SettingsSchema.FearChargeLooseMetres,
        };
        int last = all.IndexOf(SettingsSchema.CourageTimidMeleeMetres);
        for (int i = 0; i < fear.Length; i++)
        {
            Assert.Equal(last + 1 + i, all.IndexOf(fear[i]));
            Assert.Same(SettingsSchema.CourageGroup, fear[i].Group);
            Assert.Equal(SettingsPage.Advanced, fear[i].Group.Page);
            Assert.Equal(ApplyTiming.Live, fear[i].Timing);
        }
        var eyes = new[] { SettingsSchema.ShowMoraleMarks, SettingsSchema.ShowMoraleOnLabels, SettingsSchema.MoraleLabelMetres, SettingsSchema.MoraleLabelMax };
        int cap = all.IndexOf(SettingsSchema.FeverMarkMax);
        for (int i = 0; i < eyes.Length; i++)
        {
            Assert.Equal(cap + 1 + i, all.IndexOf(eyes[i]));
            Assert.Same(SettingsSchema.AltMarkersGroup, eyes[i].Group);
            Assert.Equal(SettingsPage.Advanced, eyes[i].Group.Page);
            Assert.Equal(ApplyTiming.Live, eyes[i].Timing);
        }
        Assert.DoesNotContain(SettingsSchema.FearFlockEnabled, SettingsSchema.PlayerPage); // the brief: new settings on the Advanced page
        var r = Rules();
        Assert.True(r.Enabled);
        Assert.Equal(60, r.MoraleCalm);
        Assert.Equal(15, r.MoraleBroken);
        Assert.Equal(75, r.FreshPercent);
        Assert.Equal(70, r.DisciplineMaxPercent);
        Assert.Equal(15, r.StartPercent);
        Assert.Equal(80, r.FullPercent);
        Near(4.0, r.ShelterBackMetres);
        Near(6.0, r.DriftMetresPerMinute);
        Assert.Equal(30, r.WallBreakMorale);
        var s = new TraxSettings();
        Assert.True(s.ShowMoraleMarks && s.ShowMoraleOnLabels && s.MoraleLabelMax == 4);
        s.Set(SettingsSchema.FearStartPercent, 30, SettingSources.Mcm);
        Assert.Equal(30, FearRules.From(s).StartPercent); // live
    }

    [Fact]
    public void The_master_switch_comes_first()
    {
        var off = Rules(s => s.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm));
        Assert.False(off.Enabled);
        Assert.Equal("ModEnabled", off.OffBecause);
        var flockOff = Rules(s => s.Set(SettingsSchema.FearFlockEnabled, false, SettingSources.Mcm));
        Assert.False(flockOff.Enabled);
        Assert.Equal("FearFlockEnabled", flockOff.OffBecause);
        Assert.StartsWith("fear flock OFF (ModEnabled)", off.Describe());
        Assert.False(FearMath.WallBreaks(in off, 5)); // off = walls hold (step 41's way)
        var eyes = new MoraleMarkRules(false, true, true);
        Assert.Equal(MoraleMarksHidden.ModOff, eyes.Hidden);
        Assert.Equal(MoraleMarksHidden.SwitchedOff, new MoraleMarkRules(true, false, true).Hidden);
        Assert.Equal(MoraleLabelHidden.ModOff, MoraleLabelMath.HiddenFor(new MoraleLabelRules(false, false, 60, 4), double.NaN, double.NaN, 99));
        Assert.True(Rules(s => s.Set(SettingsSchema.AthleticsEnabled, false, SettingSources.Mcm)).Enabled); // works without Athletics
    }

    [Fact]
    public void Fear_is_two_dangers_morale_and_tiredness()
    {
        var r = Rules();
        Near(0, FearMath.MoraleShare(in r, 80));
        Near(0, FearMath.MoraleShare(in r, 60));
        Near(10.0 / 45, FearMath.MoraleShare(in r, 50));
        Near(1, FearMath.MoraleShare(in r, 15));
        Near(1, FearMath.MoraleShare(in r, 0));
        Near(0, FearMath.MoraleShare(in r, -1));          // no morale (a tournament) = no fear from it
        Near(0, FearMath.TiredShare(in r, 1.0));
        Near(0, FearMath.TiredShare(in r, 0.75));
        Near(1.0 / 3, FearMath.TiredShare(in r, 0.5));
        Near(1, FearMath.TiredShare(in r, 0));
        Near(1 - (1 - 10.0 / 45) * (1 - 1.0 / 3), FearMath.Raw(in r, 50, 0.5)); // either frightens, both more
        var noAthletics = Rules(s => s.Set(SettingsSchema.AthleticsEnabled, false, SettingSources.Mcm));
        Near(0, FearMath.TiredShare(in noAthletics, 0));
        var halfTired = Rules(s => s.Set(SettingsSchema.FearTiredWeightPercent, 50, SettingSources.Mcm));
        Near(0.5, FearMath.Raw(in halfTired, 90, 0));
    }

    [Fact]
    public void Discipline_comes_from_the_troop_level()
    {
        var r = Rules();
        Near(0, FearMath.Discipline(in r, LooterLevel, false));
        Near(0, FearMath.Discipline(in r, 6, false));             // a recruit
        Near(0.35, FearMath.Discipline(in r, 16, false));          // a tier-3 man halfway
        Near(0.70, FearMath.Discipline(in r, LegionaryLevel, false));
        Near(0.70, FearMath.Discipline(in r, 40, false));
        Near(0.70, FearMath.Discipline(in r, 1, true));            // a hero
        Assert.Equal(0, FearMath.DisciplineBand(0));
        Assert.Equal(1, FearMath.DisciplineBand(0.35));
        Assert.Equal(2, FearMath.DisciplineBand(0.7));
    }

    /// <summary>THE WORKED EXAMPLE (MECHANICS §9b, AI_NOTES "Step 67"): his own fear and the pull for a looter (level 5, discipline 0) and an
    /// imperial legionary (level 26, discipline 0.7) at a fresh / half / spent bar × morale 80 / 50 / 20 - the unit's share equal (the unit
    /// feels as he does), nobody missing behind him.</summary>
    [Theory]
    //          level  bar   morale  fear     pull
    [InlineData(5, 1.0, 80, 0.0, 0.0)]
    [InlineData(5, 1.0, 50, 0.2222, 0.1111)]
    [InlineData(5, 1.0, 20, 0.8889, 1.0)]
    [InlineData(5, 0.5, 80, 0.3333, 0.2821)]
    [InlineData(5, 0.5, 50, 0.4815, 0.5100)]
    [InlineData(5, 0.5, 20, 0.9259, 1.0)]
    [InlineData(5, 0.0, 80, 1.0, 1.0)]
    [InlineData(5, 0.0, 50, 1.0, 1.0)]
    [InlineData(5, 0.0, 20, 1.0, 1.0)]
    [InlineData(26, 1.0, 80, 0.0, 0.0)]
    [InlineData(26, 1.0, 50, 0.0667, 0.0)]
    [InlineData(26, 1.0, 20, 0.2667, 0.1795)]
    [InlineData(26, 0.5, 80, 0.1000, 0.0)]
    [InlineData(26, 0.5, 50, 0.1444, 0.0)]
    [InlineData(26, 0.5, 20, 0.2778, 0.1966)]
    [InlineData(26, 0.0, 80, 0.3000, 0.2308)]
    [InlineData(26, 0.0, 50, 0.3000, 0.2308)]
    [InlineData(26, 0.0, 20, 0.3000, 0.2308)]
    public void The_worked_example_looters_and_legionaries(int level, double bar, double morale, double fear, double pull)
    {
        var r = Rules();
        double d = FearMath.Discipline(in r, level, false);
        double own = FearMath.Own(in r, morale, bar, d);
        double mixed = FearMath.Mixed(in r, own, own); // the whole unit alike
        Near(fear, mixed);
        Near(pull, FearMath.Pull(in r, mixed));
        Assert.Equal(pull > 0, FearMath.Afraid(in r, mixed, false));
    }

    [Fact]
    public void Green_troops_thicken_a_thin_line_veterans_hold_it()
    {
        var r = Rules();
        double looter = FearMath.Own(in r, 80, 1.0, 0, inContact: true, somebodyBehind: false);
        double legion = FearMath.Own(in r, 80, 1.0, 0.7, inContact: true, somebodyBehind: false);
        Near(0.25, looter);
        Near(0.1538, FearMath.Pull(in r, looter));                     // a fresh looter in one rank edges back to the mass
        Near(0.075, legion);
        Near(0, FearMath.Pull(in r, legion));                           // a fresh legionary keeps his slot
        Near(0, FearMath.Own(in r, 80, 1.0, 0, inContact: false, somebodyBehind: false)); // no enemy near = no thin-line fear
        Near(0, FearMath.Own(in r, 80, 1.0, 0, inContact: true, somebodyBehind: true));   // a second rank behind him
    }

    [Fact]
    public void The_unit_moves_together_and_the_pull_has_a_hysteresis()
    {
        var r = Rules();
        Near(0.5, FearMath.Mixed(in r, 0.2, 0.8));
        Near(0.2, FearMath.Mixed(in r, 0.2, double.NaN));               // the unit not known yet
        var alone = Rules(s => s.Set(SettingsSchema.FearUnitSharePercent, 0, SettingSources.Mcm));
        Near(0.2, FearMath.Mixed(in alone, 0.2, 0.8));
        Assert.False(FearMath.Afraid(in r, 0.15, false));
        Assert.True(FearMath.Afraid(in r, 0.16, false));
        Assert.True(FearMath.Afraid(in r, 0.11, true));                // a flocking man calms below 0.15 - 0.05
        Assert.False(FearMath.Afraid(in r, 0.09, true));
        Near(0, FearMath.Pull(in r, 0.15));
        Near(1, FearMath.Pull(in r, 0.8));
        Near(0.5, FearMath.Pull(in r, 0.475));
    }

    [Fact]
    public void His_place_blends_from_his_slot_to_the_shelter_never_too_far()
    {
        var r = Rules();
        FearMath.Place(in r, 0, 0, 0, -10, 0.5, out double tx, out double ty);
        Near(0, tx);
        Near(-5, ty);
        FearMath.Place(in r, 0, 0, 0, -30, 1, out tx, out ty);
        Near(-12, ty);                                                   // FearShelterMaxMetres
        FearMath.Place(in r, 3, 4, 0, -10, 0, out tx, out ty);
        Near(3, tx);
        Near(4, ty);                                                     // no pull = his slot exactly
        FearMath.ShelterWanted(in r, 10, 10, 0, -1, 0.5, out double sx, out double sy);
        Near(10, sx);
        Near(8, sy);                                                     // 4 m x the unit's pull 0.5 behind
        FearMath.ShelterWanted(in r, 10, 10, 0, 0, 1, out sx, out sy);
        Near(10, sy);                                                    // no enemy known = the centre
    }

    [Fact]
    public void The_shelter_drifts_back_slowly_by_order_and_forward_freely()
    {
        var r = Rules();
        Near(6.0 / 60 * 0.5 * 0.25, FearMath.DriftCap(in r, FearOrder.Hold, 0.5));
        Near(6.0 / 60 * 0.5 * 0.25, FearMath.DriftCap(in r, FearOrder.Wall, 0.5));
        Near(6.0 / 60 * 0.5, FearMath.DriftCap(in r, FearOrder.Charge, 0.5));
        Near(6.0 / 60 * 0.5, FearMath.DriftCap(in r, FearOrder.Advance, 0.5));
        double back = FearMath.DriftStep(0, 0, 0, -4, 0, -1, 0.05, out double nx, out double ny);
        Near(0.05, back);
        Near(-0.05, ny);                                                 // capped
        back = FearMath.DriftStep(0, 0, 3, 5, 0, -1, 0.05, out nx, out ny);
        Near(0, back);
        Near(3, nx);
        Near(5, ny);                                                     // forward and sideways free
        back = FearMath.DriftStep(0, 0, 2, -4, 0, -1, 0.05, out nx, out ny);
        Near(2, nx);
        Near(-0.05, ny);                                                 // sideways free, back capped
        // a minute under a hold: 1.5 m back at most; under a charge 6 m
        double y = 0;
        for (int i = 0; i < 120; i++)
        {
            FearMath.DriftStep(0, y, 0, y - 4, 0, -1, FearMath.DriftCap(in r, FearOrder.Hold, 0.5), out _, out double ny2);
            y = ny2;
        }
        Near(-1.5, y);
    }

    [Fact]
    public void Cover_behind_and_the_spot_behind_a_comrade()
    {
        var r = Rules();
        // he at (0, 0), the nearest enemy 5 m north (0, 5): u = (0, 1)
        FearMath.AlongAcross(0, 0, 0, 1, 0.3, 1.5, out double along, out double across);
        Near(1.5, along);
        Near(0.3, across);
        Assert.True(FearMath.Covers(in r, along, across, 5));          // a comrade 1.5 m in front, 0.3 m aside covers him
        Assert.False(FearMath.Covers(in r, 1.5, 1.2, 5));               // too far aside
        Assert.False(FearMath.Covers(in r, 0.1, 0, 5));                 // beside him, not in front
        Assert.False(FearMath.Covers(in r, 4.9, 0, 5));                 // at the enemy
        Assert.True(FearMath.Behind(-1.2, 0.4));
        Assert.False(FearMath.Behind(-3.5, 0));
        Assert.False(FearMath.Behind(-1.2, 2));
        FearMath.BehindSpot(2, 3, 0, 1, out double sx, out double sy);
        Near(2, sx);
        Near(2, sy);                                                     // 1 m behind the comrade, away from the enemy
    }

    [Fact]
    public void A_fresh_comrade_anchors_him_a_while()
    {
        var r = Rules();
        Assert.True(FearMath.Anchors(in r, 0.8));
        Assert.True(FearMath.Anchors(in r, 0.6));
        Assert.False(FearMath.Anchors(in r, 0.5));                      // the comrade tired: the pull comes back
        Assert.True(FearMath.AnchorRunning(in r, 100, 119.9));
        Assert.False(FearMath.AnchorRunning(in r, 100, 120.1));
        Assert.False(FearMath.AnchorRunning(in r, double.NaN, 100));
        var noAthletics = Rules(s => s.Set(SettingsSchema.AthleticsEnabled, false, SettingSources.Mcm));
        Assert.True(FearMath.Anchors(in noAthletics, 0));              // no Athletics = every comrade fresh
        var never = Rules(s => s.Set(SettingsSchema.FearAnchorSeconds, 0.0, SettingSources.Mcm));
        Assert.False(FearMath.Anchors(in never, 1));
    }

    [Fact]
    public void A_charge_is_a_loose_mass_tighter_with_fear()
    {
        var r = Rules();
        Near(12, FearMath.LooseRadius(in r, 0));
        Near(6, FearMath.LooseRadius(in r, 0.5));
        Near(FearMath.MinLooseMetres, FearMath.LooseRadius(in r, 1));
        Assert.Equal(FearOrder.Charge, FearMath.OrderOf(BraceOrderKind.Charge, false));
        Assert.Equal(FearOrder.Charge, FearMath.OrderOf(BraceOrderKind.AiCharge, true));
        Assert.Equal(FearOrder.Wall, FearMath.OrderOf(BraceOrderKind.Stop, true));
        Assert.Equal(FearOrder.Hold, FearMath.OrderOf(BraceOrderKind.Move, false));
        Assert.Equal(FearOrder.Advance, FearMath.OrderOf(BraceOrderKind.AiAdvance, false));
        Assert.Equal(FearOrder.Retreat, FearMath.OrderOf(BraceOrderKind.Retreat, true));
        Assert.Equal(0, FearMath.DriftGroup(FearOrder.Wall));
        Assert.Equal(2, FearMath.DriftGroup(FearOrder.Charge));
        Assert.Equal(-1, FearMath.DriftGroup(FearOrder.Retreat));
    }

    [Fact]
    public void The_sprint_back_and_the_walk()
    {
        var r = Rules();
        Assert.True(FearMath.SprintWanted(in r, 9, false));            // far
        Assert.False(FearMath.SprintWanted(in r, 6, false));
        Assert.True(FearMath.SprintWanted(in r, 2, true));             // alone with an enemy near
        Assert.False(FearMath.SprintWanted(in r, 1, true));            // already there
        Near(1, FearMath.Stick(3));
        Near(0.5, FearMath.Stick(1.25));
        Near(FearMath.MinStick, FearMath.Stick(0.1));
        Near(2.5, FearMath.SprintAmount(in r, 5, 5));                  // keeps 50%
        Assert.True(FearMath.NearSlot(0.5, 0.5, 0, 0));                 // a small pull: his own AI keeps him at his slot
        Assert.False(FearMath.NearSlot(0, 1.2, 0, 0));
        Near(0, FearMath.SprintAmount(in r, 2, 5));
    }

    [Fact]
    public void A_shield_wall_breaks_only_below_its_morale_line()
    {
        var r = Rules();
        Assert.True(FearMath.WallBreaks(in r, 25));
        Assert.False(FearMath.WallBreaks(in r, 30));
        Assert.False(FearMath.WallBreaks(in r, double.NaN));
        var never = Rules(s => s.Set(SettingsSchema.FearWallBreakMorale, 0, SettingSources.Mcm));
        Assert.False(FearMath.WallBreaks(in never, 0));
        Assert.Equal("the wall broke (its morale)", WallMath.Name(WallVerdict.Breaking));
        Assert.Equal(WallVerdict.FarBehind + 1, WallVerdict.Breaking);  // appended - the old verdicts keep their indexes
    }

    [Fact]
    public void The_unit_table_publishes_means_the_shelter_and_the_samples()
    {
        var r = Rules();
        var units = new FearUnits();
        var stats = new FearStats();
        int key = FearMath.UnitKey(1, 2);
        Assert.Equal(18, key);
        Assert.Equal(-1, FearMath.UnitKey(8, 0));
        Assert.False(units.Due(0));                                       // the first call arms the clock
        // 10 men in a line 9 m wide (facing north), enemies 4 m north of each
        for (int i = 0; i < 10; i++)
            units.Note(key, 0.6, 0.6, 40, 0.5, true, i - 4.5, 4, 0, 0, 0, 1, i - 4.5, 0, FearOrder.Hold);
        Assert.True(units.Due(0.5));
        units.Publish(0.5, in r, stats, true);
        Assert.True(units.Live(key, 0.6));
        Near(0.6, units.FearOf(key, 0.6));
        Near(40, units.MoraleOf(key, 0.6));
        Assert.True(units.HasEnemy[key]);
        Near(0, units.AwayX[key]);
        Near(-1, units.AwayY[key]);                                       // away from the enemy = south
        Near(0, units.ShelterY[key]);                                     // the first publish: at the centre (no jump)
        // the next round wants it 4 m x 0.6 behind the centre, which moved back 2.4 m: its step back capped under a hold
        for (int i = 0; i < 10; i++)
            units.Note(key, 0.6, 0.6, 40, 0.5, true, i - 4.5, 1.6, 0, -2.4, 0, 1, i - 4.5, -2.4, FearOrder.Hold);
        units.Publish(1.0, in r, stats, true);
        Near(-6.0 / 60 * 0.5 * 0.25, units.ShelterY[key]);
        Near(2.4 * 60 / 0.5, stats.DriftPerMinute(true, 0), 1e-6);        // the centre moved 2.4 m back in 0.5 s
        Assert.True(stats.DepthPerWidth(true) < 0.01);                    // a single rank: no depth
        Assert.True(stats.MeanSpread(true) > 2);
        Assert.False(units.Live(key, 3.5));                               // stale after 2 s without a look
        Assert.True(double.IsNaN(units.FearOf(key, 3.5)));
    }

    [Fact]
    public void The_summary_has_its_lines()
    {
        var stats = new FearStats();
        var lines = new List<string>();
        stats.AddSummaryLines(lines, Rules());
        Assert.Equal(2, lines.Count);
        Assert.StartsWith("fear flock settings: fear flock: fear = 1 - (1 - 100% x morale below 60 (full at 15))", lines[0]);
        Assert.Contains("a looter at morale 20 and half a bar 1.00, a legionary 0.20", lines[0]);
        stats.MenLooked = 10;
        stats.AddLook(true, true, true, false, false);
        stats.AddLook(false, true, false, true, false);
        stats.AddFear(0, 0.5, 0.54, true);
        stats.AddFear(0.7, 0.2, 0.08, true);
        stats.AddEnd(FearEnd.Calm);
        stats.AddFlockLook(FearOrder.Charge);
        lines.Clear();
        stats.AddSummaryLines(lines, Rules());
        Assert.Equal(7, lines.Count);
        Assert.Contains(lines, l => l.StartsWith("fear flock shape (every unit every 0.5 s): spread"));
        Assert.Contains(lines, l => l.StartsWith("fear flock drift - a unit's centre away from the enemy near it, metres a minute (+ = back): hold / wall ON - / OFF -"));
        Assert.Contains(lines, l => l.StartsWith("fear flock cover (in contact - an enemy within 8 m): a comrade between him and the nearest enemy ON 100.0% of 1 looks, OFF 0.0% of 1"));
        Assert.Contains(lines, l => l.StartsWith("fear flock by discipline: green (discipline < 0.25): 1 looks, fear 0.50, pull 0.54, afraid 100.0%"));
        Assert.Contains(lines, l => l.StartsWith("fear flock spells: 0 by 0 of 10 men looked; ends calm again 1; flocking looks by order charge 1"));
        Assert.Contains(lines, l => l.StartsWith("fear flock vs courage and walls: courage's walk back took the man 0 looks (it goes first)"));
    }

    // ------------------------------------------------------------------ the morale eyes

    [Fact]
    public void The_morale_bar_sits_at_the_bottom_of_the_stack()
    {
        var fever = new FeverMarkRules(true, true, true, true, true, 200, 22, 4, 2.5);
        var bars = new AthleticsMarkRules(true, true, true, true, 22, 5, 2);
        var morale = new MoraleMarkRules(true, true, true);
        var m = new FeverMark { Side = (int)MarkerTeam.Yours, InFront = true, Distance = 10, Morale = 64 };
        Assert.Equal(FeverMarkSkip.None, AthleticsMarkMath.Decide(ref m, in fever, in bars, in morale));
        Assert.True(m.HasBar && m.HasMorale && m.FeverSlot && !m.HasFever);
        AthleticsMarkMath.Layout(in fever, in bars, 1f, 10f, true, false, true, true, 500, 300, out var box);
        AthleticsMarkMath.Layout(in fever, in bars, 1f, 10f, true, false, true, 500, 300, out var old);
        Assert.Equal(22f, box.MoraleWidth);
        Assert.Equal(5f, box.MoraleHeight);
        Assert.Equal(5f + 2f, box.FeverLift);                               // the fever bar stands on the morale bar + the gap
        Assert.Equal(old.Height + 7f, box.Height);
        Assert.Equal(300f, box.Top + box.Height);                            // the bottom on his point
        Assert.Equal(0f, old.MoraleHeight);                                  // without it: step 46's stack exactly
        Assert.Equal(0f, old.FeverLift);
        // only a morale bar (the Athletics and fever bars off): he is still marked
        var noBars = new AthleticsMarkRules(true, true, false, false, 22, 5, 2);
        var noFever = new FeverMarkRules(true, true, false, false, true, 200, 22, 4, 2.5);
        var lone = new FeverMark { Side = (int)MarkerTeam.Enemy, InFront = true, Distance = 10, Morale = 30 };
        Assert.Equal(FeverMarkSkip.None, AthleticsMarkMath.Decide(ref lone, in noFever, in noBars, in morale));
        Assert.True(lone.HasMorale && !lone.HasBar);
        Assert.Equal(FeverMarkSkip.EnemyOff, AthleticsMarkMath.Decide(ref lone, in noFever, in noBars, new MoraleMarkRules(true, true, false)));
        Assert.Equal(FeverMarkSkip.None, AthleticsMarkMath.Decide(ref m, in fever, in bars));      // the old call: no morale place
        Assert.False(m.HasMorale);
        Assert.Equal(0.64f, MoraleMarkMath.Fill(64), 4);
        Assert.Equal(0f, MoraleMarkMath.Fill(-1));
        Assert.False(MoraleMarkMath.Known(-1));
        Assert.Equal("morale 64", MoraleMarkMath.Describe(64.2));
        Assert.True(AthleticsMarkMath.ParseHex(MoraleMarkMath.ColorHex, out _, out _, out float b, out _) && b > 0.9f); // blue
    }

    [Fact]
    public void The_near_formations_get_the_morale_line()
    {
        var r = new MoraleLabelRules(true, true, 60, 2);
        Assert.Equal(MoraleLabelHidden.None, MoraleLabelMath.HiddenFor(in r, 64, 30, 0));
        Assert.Equal(MoraleLabelHidden.None, MoraleLabelMath.HiddenFor(in r, 64, 30, 1));
        Assert.Equal(MoraleLabelHidden.Capped, MoraleLabelMath.HiddenFor(in r, 64, 30, 2));
        Assert.Equal(MoraleLabelHidden.TooFar, MoraleLabelMath.HiddenFor(in r, 64, 61, 0));
        Assert.Equal(MoraleLabelHidden.NoMorale, MoraleLabelMath.HiddenFor(in r, double.NaN, 30, 0));
        Assert.Equal(MoraleLabelHidden.SwitchedOff, MoraleLabelMath.HiddenFor(new MoraleLabelRules(true, false, 60, 2), 64, 30, 0));
        Assert.True(MoraleLabelMath.Eligible(in r, 64, 60));
        Assert.False(MoraleLabelMath.Eligible(in r, 64, 60.5));
        Assert.Equal("morale 64, fresh 41%", MoraleLabelMath.Text(64, 41));
        Assert.Equal(65, MoraleLabelMath.Round(64.5));
        var stats = new MoraleLabelStats();
        stats.NoteShown(64, 41, 0, (int)MarkerTeam.Yours);
        stats.NoteShown(22, 10, 2, (int)MarkerTeam.Enemy);
        stats.NoteHidden(MoraleLabelHidden.Capped);
        stats.NoteFrame(2);
        Assert.Equal("hud: ALT markers - morale line (step 67): pushed 2x, 2.0 formations a frame on the screen; the lowest morale 22 (enemy 3, fresh 10%); hidden beyond the nearest 1",
            stats.SummaryLine("ALT markers"));
        var points = new MeanStd();
        points.Add(50);
        var fr = new MeanStd();
        fr.Add(0.41);
        var mo = new MeanStd();
        mo.Add(60);
        mo.Add(68);
        var st = FormationAthleticsStats.From(in points, in fr, in fr, in fr, 0, 0, 0, 0, in mo);
        Near(64, st.MeanMorale);
        Assert.True(double.IsNaN(FormationAthleticsStats.From(in points, in fr, in fr, in fr, 0, 0).MeanMorale)); // the old call: unknown
    }

    [Fact]
    public void The_ALT_summary_counts_the_morale_bars()
    {
        var stats = new FeverMarkStats();
        stats.NoteShown();
        stats.NoteBars(5, new int[BarMath.BandCount], 0, 0, 5, 300);
        stats.NoteFrame(0.02, 5, 5, 0, 5, 0, false, 5, new int[(int)FeverMarkSkip.Count], 0.01);
        string line = stats.SummaryLine(0);
        Assert.Contains("; morale bars a frame (step 67): mean 5.0, max 5 (their morale 60.0 mean); errors 0", line);
        var none = new FeverMarkStats();
        none.NoteShown();
        none.NoteFrame(0.02, 1, 1, 0, 1, 0, false, 5, new int[(int)FeverMarkSkip.Count], 0.01);
        Assert.DoesNotContain("morale bars", none.SummaryLine(0));                // no morale bars = step 58's line exactly
    }
}

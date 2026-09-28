using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>Step 21 (Anton, 2026-09-28): battle pace - the attack's class from what the fighter holds, the pause after it
/// (the tired part + the class's share: foot melee stretches the expected cycle by 1 / (1 − %), archers wait extra seconds),
/// and the per-class summary lines Anton tunes from.</summary>
public class BattlePaceTests
{
    private static BattlePaceRules Rules(int shield = 30, int foot = 15, double gap = 1.0, double bow = 2.0, double crossbow = 2.5) =>
        new(shield, foot, gap, bow, crossbow);

    // ------------------------------------------------------------------ the class

    [Fact]
    public void The_class_comes_from_what_he_holds_and_whether_he_rides()
    {
        Assert.Equal(AttackClass.ShieldInfantry, BattlePaceMath.Classify(melee: true, mounted: false, shieldInOffHand: true, RangedWeaponKind.None));
        Assert.Equal(AttackClass.FootMelee, BattlePaceMath.Classify(true, false, false, RangedWeaponKind.None));
        Assert.Equal(AttackClass.Rider, BattlePaceMath.Classify(true, true, true, RangedWeaponKind.None));   // a shield on horseback is still a rider
        Assert.Equal(AttackClass.Rider, BattlePaceMath.Classify(true, true, false, RangedWeaponKind.None));
        Assert.Equal(AttackClass.Bow, BattlePaceMath.Classify(false, false, false, RangedWeaponKind.Bow));
        Assert.Equal(AttackClass.Bow, BattlePaceMath.Classify(false, true, false, RangedWeaponKind.Bow));     // horse archers too
        Assert.Equal(AttackClass.Crossbow, BattlePaceMath.Classify(false, true, false, RangedWeaponKind.Crossbow));
        Assert.Equal(AttackClass.OtherRanged, BattlePaceMath.Classify(false, false, false, RangedWeaponKind.Other));
        Assert.Equal(AttackClass.OtherRanged, BattlePaceMath.Classify(false, false, true, RangedWeaponKind.None));
    }

    [Fact]
    public void A_class_that_does_not_match_the_attack_that_ended_falls_back_to_the_plain_class_of_its_kind()
    {
        Assert.Equal(AttackClass.ShieldInfantry, BattlePaceMath.Consistent(AttackClass.ShieldInfantry, AttackKind.Melee, mounted: false));
        Assert.Equal(AttackClass.FootMelee, BattlePaceMath.Consistent(AttackClass.Bow, AttackKind.Melee, false));        // never a share it was not read for
        Assert.Equal(AttackClass.Rider, BattlePaceMath.Consistent(AttackClass.Crossbow, AttackKind.Melee, true));
        Assert.Equal(AttackClass.Rider, BattlePaceMath.Consistent(AttackClass.ShieldInfantry, AttackKind.Melee, true));
        Assert.Equal(AttackClass.OtherRanged, BattlePaceMath.Consistent(AttackClass.ShieldInfantry, AttackKind.Ranged, false));
        Assert.Equal(AttackClass.Crossbow, BattlePaceMath.Consistent(AttackClass.Crossbow, AttackKind.Ranged, true));
    }

    // ------------------------------------------------------------------ the pause

    [Fact]
    public void A_fresh_shield_man_waits_so_his_cycle_is_30_percent_longer()
    {
        // D 0.8 s, his own gap 1.0 s: the expected cycle 1.8 s ÷ 0.7 = 2.571 s → the share 0.771 s (the tired part 0)
        var p = BattlePaceMath.Plan(Rules(), AttackClass.ShieldInfantry, 0.8, 1f);
        Assert.Equal(0.0, p.Tired, 9);
        Assert.Equal(0.3 * 1.8 / 0.7, p.Extra, 9);
        Assert.Equal(p.Extra, p.Total, 9);
        Assert.Equal(30, p.Percent);
        Assert.True(p.HasExtra);
        Assert.Equal(1.8 / 0.7, BattlePaceMath.ModelCycle(0.8, p.Total, 1.0), 9);
        Assert.Equal(0.30, BattlePaceMath.FewerShare(1.8, BattlePaceMath.ModelCycle(0.8, p.Total, 1.0)), 9);
        // the brief's first idea (m × 0.7 on the attack alone) would have bought 0.34 s: 16% fewer swings, not 30
        double dOnly = AttackTimerMath.Pause(0.8, 0.7f);
        Assert.InRange(BattlePaceMath.FewerShare(1.8, 1.8 + dOnly), 0.15, 0.17);
    }

    [Fact]
    public void Tired_or_fresh_he_swings_the_percent_less_than_tiredness_alone_lets_him()
    {
        var r = Rules();
        foreach (var c in new[] { AttackClass.ShieldInfantry, AttackClass.FootMelee })
        {
            double q = r.SwingsLessPercent(c) / 100.0;
            foreach (float m in new[] { 1f, 0.9f, 0.73f, 0.5f, 0.41f, 0.2f })
            {
                var p = BattlePaceMath.Plan(r, c, 0.8, m);
                Assert.Equal(AttackTimerMath.Pause(0.8, m), p.Tired, 9);           // the tired part is step 13's, unchanged
                double at0 = BattlePaceMath.ModelCycle(0.8, p.Tired, r.AiGapSeconds);
                double with = BattlePaceMath.ModelCycle(0.8, p.Total, r.AiGapSeconds);
                Assert.Equal(at0 / (1 - q), with, 9);
                Assert.Equal(q, BattlePaceMath.FewerShare(at0, with), 9);
            }
        }
        // a tired shield man (m 0.5): (0.8 + 0.3 × 1.8) ÷ 0.7 = 1.914 s, of which 0.8 tired
        var t = BattlePaceMath.Plan(r, AttackClass.ShieldInfantry, 0.8, 0.5f);
        Assert.Equal(1.34 / 0.7, t.Total, 9);
        Assert.Equal(0.8, t.Tired, 9);
        // a fresh man without a shield (15%): 0.15 × 1.8 ÷ 0.85
        Assert.Equal(0.15 * 1.8 / 0.85, BattlePaceMath.Plan(r, AttackClass.FootMelee, 0.8, 1f).Total, 9);
    }

    [Fact]
    public void Archers_wait_extra_seconds_on_top_of_the_tired_pause()
    {
        var r = Rules();
        var fresh = BattlePaceMath.Plan(r, AttackClass.Bow, 2.5, 1f);
        Assert.Equal(0.0, fresh.Tired, 9);
        Assert.Equal(2.0, fresh.Extra, 9);
        Assert.Equal(0, fresh.Percent);
        var tired = BattlePaceMath.Plan(r, AttackClass.Bow, 2.5, 0.5f);
        Assert.Equal(2.5, tired.Tired, 9);
        Assert.Equal(4.5, tired.Total, 9);
        Assert.Equal(2.5, BattlePaceMath.Plan(r, AttackClass.Crossbow, 3.5, 1f).Total, 9);
        Assert.Equal(3.5 + 2.5, BattlePaceMath.Plan(r, AttackClass.Crossbow, 3.5, 0.5f).Total, 9);
    }

    [Fact]
    public void Riders_and_thrown_weapons_keep_the_tired_pause_alone()
    {
        var r = Rules(shield: 90, foot: 90, bow: 10, crossbow: 10);
        foreach (var c in new[] { AttackClass.Rider, AttackClass.OtherRanged })
        {
            Assert.Equal(0.0, BattlePaceMath.Plan(r, c, 0.8, 1f).Total, 9);
            Assert.Equal(0.8, BattlePaceMath.Plan(r, c, 0.8, 0.5f).Total, 9);
            Assert.False(BattlePaceMath.Plan(r, c, 0.8, 0.5f).HasExtra);
            Assert.Equal("no setting - tiredness only", r.SettingText(c));
        }
    }

    [Fact]
    public void Zero_turns_each_share_off()
    {
        var off = Rules(0, 0, 1.0, 0, 0);
        Assert.False(off.AnyOn);
        foreach (var c in new[] { AttackClass.ShieldInfantry, AttackClass.FootMelee, AttackClass.Bow, AttackClass.Crossbow })
        {
            Assert.Equal(0.0, BattlePaceMath.Plan(off, c, 0.8, 1f).Total, 9);
            Assert.Equal(0.8, BattlePaceMath.Plan(off, c, 0.8, 0.5f).Total, 9); // step 13's pause, to the bit
        }
        Assert.True(Rules(0, 0, 1.0, 0.5, 0).AnyOn);
    }

    [Fact]
    public void A_melee_share_needs_the_attack_length_and_the_numbers_are_kept_in_range()
    {
        var p = BattlePaceMath.Plan(Rules(), AttackClass.ShieldInfantry, double.NaN, 1f);
        Assert.Equal(0.0, p.Total, 9);
        var clamped = new BattlePaceRules(120, -5, double.NaN, -1, double.NaN);
        Assert.Equal(BattlePaceMath.MaxSwingsLessPercent, clamped.ShieldInfantryPercent);
        Assert.Equal(0, clamped.FootMeleePercent);
        Assert.Equal(0.0, clamped.AiGapSeconds, 9);
        Assert.Equal(0.0, clamped.BowSeconds, 9);
        Assert.Equal(0.0, clamped.CrossbowSeconds, 9);
        Assert.True(double.IsFinite(BattlePaceMath.Plan(clamped, AttackClass.ShieldInfantry, 0.8, 0.2f).Total)); // 90% never divides by 0
    }

    [Fact]
    public void The_rules_are_read_live_from_the_settings()
    {
        var s = new TraxSettings();
        var r = BattlePaceRules.From(s);
        Assert.Equal(30, r.ShieldInfantryPercent);
        Assert.Equal(15, r.FootMeleePercent);
        Assert.Equal(1.0, r.AiGapSeconds, 6);
        Assert.Equal(2.0, r.BowSeconds, 6);
        Assert.Equal(2.5, r.CrossbowSeconds, 6);
        s.Set(SettingsSchema.ShieldInfantrySwingsLessPercent, 95, SettingSources.Mcm); // above the range: clamped to 90
        s.Set(SettingsSchema.ExtraPauseAfterBowShotSeconds, 1.5, SettingSources.Mcm);
        var after = BattlePaceRules.From(s);
        Assert.Equal(90, after.ShieldInfantryPercent);
        Assert.Equal(1.5, after.BowSeconds, 6);
        Assert.False(after.SameAs(r));
        Assert.True(r.SameAs(Rules()));
        Assert.Same(SettingsSchema.BattlePaceGroup, SettingsSchema.ShieldInfantrySwingsLessPercent.Group);
        Assert.Same(SettingsSchema.AdvancedGroup, SettingsSchema.Groups[SettingsSchema.Groups.Count - 1]);
    }

    [Fact]
    public void The_settings_sentence_says_the_master_switch_first()
    {
        Assert.StartsWith("ON - AI shield infantry swing 30% less (ShieldInfantrySwingsLessPercent), other AI foot melee swing 15% less (FootMeleeSwingsLessPercent) - after each such swing a pause that stretches his expected cycle (his attack + his tired pause + his own gap AiMeleeGapSeconds 1.00 s) ÷ (1 - the share), at full strength or tired; AI bowmen wait 2.0 s more after each shot (ExtraPauseAfterBowShotSeconds), crossbowmen wait 2.5 s more after each shot (ExtraPauseAfterCrossbowShotSeconds), on foot and mounted",
            Rules().Describe(true));
        Assert.StartsWith("OFF (ModEnabled off - no AI pause at all) - ", Rules().Describe(false, "ModEnabled"));
        Assert.Contains("AI shield infantry unchanged (ShieldInfantrySwingsLessPercent 0 = off), other AI foot melee unchanged (FootMeleeSwingsLessPercent 0 = off)", Rules(0, 0, 1, 0, 0).Describe(true));
        Assert.Contains("AI bowmen unchanged (ExtraPauseAfterBowShotSeconds 0 = off), crossbowmen unchanged (ExtraPauseAfterCrossbowShotSeconds 0 = off)", Rules(0, 0, 1, 0, 0).Describe(true));
        Assert.Contains("riders' melee, thrown weapons, slings and you: tiredness only", Rules().Describe(true));
    }

    // ------------------------------------------------------------------ the summary

    [Fact]
    public void The_summary_gives_each_class_its_rate_a_minute_and_checks_the_model()
    {
        var s = new BattlePaceStats();
        s.AddAttack(AttackClass.ShieldInfantry, newMan: true);
        s.AddAttack(AttackClass.ShieldInfantry, false);
        s.AddAttack(AttackClass.ShieldInfantry, true);
        s.AddAttack(AttackClass.ShieldInfantry, false);
        s.AddPause(AttackClass.ShieldInfantry, 0, 0.77);
        s.AddPause(AttackClass.ShieldInfantry, 0, 0.77);
        s.AddPause(AttackClass.ShieldInfantry, 0.8, 1.2);
        s.AddNoPause(AttackClass.ShieldInfantry);
        for (int i = 0; i < 3; i++) s.AddCycle(AttackClass.ShieldInfantry, 2.6, 0.77, 1.8, 2.57);
        s.AddGapAfterPause(AttackClass.ShieldInfantry, 1.0);
        s.AddGapAfterPause(AttackClass.ShieldInfantry, 1.0);

        s.AddAttack(AttackClass.Bow, true);
        s.AddAttack(AttackClass.Bow, false);
        s.AddPause(AttackClass.Bow, 0, 2.0);
        s.AddPause(AttackClass.Bow, 1.0, 2.0);
        s.AddCycle(AttackClass.Bow, 6.5, 2.0, double.NaN, double.NaN);
        s.AddCycle(AttackClass.Bow, 6.5, 2.0, double.NaN, double.NaN);
        s.AddGapAfterPause(AttackClass.Bow, 1.1);

        s.AddAttack(AttackClass.Rider, true);
        s.AddPause(AttackClass.Rider, 0.5, 0);

        Assert.Equal(4, s.Attacks(AttackClass.ShieldInfantry));
        Assert.Equal(2, s.Men(AttackClass.ShieldInfantry));
        Assert.Equal(3, s.Pauses(AttackClass.ShieldInfantry));
        Assert.Equal(2, s.PausesShareOnly(AttackClass.ShieldInfantry));
        Assert.Equal(1, s.PausesTiredAndShare(AttackClass.ShieldInfantry));
        Assert.Equal(1, s.PausesTiredOnly(AttackClass.Rider));
        Assert.Equal(2.6 / 2.57, s.ModelRatio(AttackClass.ShieldInfantry), 9);
        Assert.True(double.IsNaN(s.ModelRatio(AttackClass.Bow)));

        var lines = s.SummaryLines(Rules(), paceOn: true, offBecause: null, changedDuringBattle: false);
        Assert.Equal(1 + (int)AttackClass.Count, lines.Count);
        Assert.StartsWith("battle pace (step 21) settings at the end: ON - AI shield infantry swing 30% less", lines[0]);
        Assert.Contains("battle pace - shield infantry (AI melee on foot, a shield in the other hand; ShieldInfantrySwingsLessPercent 30 - asks 30% fewer swings): 4 attacks by 2 men; pauses 3 - at full strength 2 (the share alone), tired 1 (tired + the share), tired only 0; tired part avg 0.27 s, the share avg 0.91 s; no pause 1 (full strength with no share, under 0.1 s, not measured, chained or refused); cycle 2.60 s (n 3) = 23.1 swings a minute per man while fighting; his own gap after a pause avg 1.00 s (n 2 - the model assumes 1.00 s, AiMeleeGapSeconds: shorter = part of the pause hid in his own idle time); the model (his attack + the pause + his own gap): with the setting at 0 1.80 s = 33.3 a minute, with it 2.57 s = 23.3 a minute (30% fewer) - measured ÷ it 101%: on target", lines);
        Assert.Contains("battle pace - other foot melee (AI melee on foot without a shield - two-handers, polearms, a one-hander alone; FootMeleeSwingsLessPercent 15 - asks 15% fewer swings): no attacks", lines);
        Assert.Contains("battle pace - riders (AI melee on horseback; no setting - tiredness only): 1 attacks by 1 man; pauses 1 (tired - no share); tired part avg 0.50 s; no pause 0 (full strength with no share, under 0.1 s, not measured, chained or refused); no cycle measured (a fighting rhythm needs two attacks in a row)", lines);
        Assert.Contains("battle pace - bowmen (AI bow shots, on foot and horse archers; ExtraPauseAfterBowShotSeconds 2.0 - sized for about 30% fewer shots): 2 shots by 1 man; pauses 2 - at full strength 1 (the share alone), tired 1 (tired + the share), tired only 0; tired part avg 0.50 s, the share avg 2.00 s; no pause 0 (full strength with no share, under 0.1 s, not measured, chained or refused); cycle 6.50 s (n 2) = 9.2 shots a minute per man while shooting; his own gap after a pause avg 1.10 s (n 1); the extra inside a cycle avg 2.00 s - if none of it hid in his own gap, without it the cycle would be 4.50 s = 13.3 a minute: at most 31% fewer shots (the same battle with the slider at 0 is the real check)", lines);
        Assert.Contains("battle pace - crossbowmen (AI crossbow shots, on foot and mounted; ExtraPauseAfterCrossbowShotSeconds 2.5 - sized for about 30% fewer shots): no attacks", lines);
        Assert.Contains("battle pace - thrown and slings (AI javelins, throwing axes and knives, stones, slings; no setting - tiredness only): no attacks", lines);
    }

    [Fact]
    public void At_zero_the_model_line_checks_its_own_gap_and_a_change_mid_battle_is_flagged()
    {
        var s = new BattlePaceStats();
        s.AddAttack(AttackClass.FootMelee, true);
        for (int i = 0; i < 4; i++) s.AddCycle(AttackClass.FootMelee, 1.8, 0, 1.8, 1.8);
        var lines = s.SummaryLines(Rules(foot: 0), paceOn: false, offBecause: "AttackRatePaceHold", changedDuringBattle: true);
        Assert.StartsWith("battle pace (step 21) settings at the end: OFF (AttackRatePaceHold off - no AI pause at all) - ", lines[0]);
        Assert.EndsWith(" - a battle-pace setting CHANGED during this battle: the lines below mix both", lines[0]);
        Assert.Contains(lines, l => l.StartsWith("battle pace - other foot melee (AI melee on foot without a shield - two-handers, polearms, a one-hander alone; FootMeleeSwingsLessPercent 0): 1 attacks by 1 man; pauses 0; no pause 0", StringComparison.Ordinal)
                                    && l.EndsWith("; cycle 1.80 s (n 4) = 33.3 swings a minute per man while fighting; the model (his attack + the pause + his own gap): with the setting at 0 1.80 s = 33.3 a minute - measured ÷ it 100%: on target (at 0 this checks the model's own gap)", StringComparison.Ordinal));
    }

    [Fact]
    public void Per_minute_and_fewer_share_handle_the_edges()
    {
        Assert.Equal(20.0, BattlePaceMath.PerMinute(3.0), 9);
        Assert.True(double.IsNaN(BattlePaceMath.PerMinute(0)));
        Assert.True(double.IsNaN(BattlePaceMath.FewerShare(double.NaN, 2)));
        Assert.True(double.IsNaN(BattlePaceMath.ModelCycle(double.NaN, 1, 1)));
        Assert.Equal("swings", BattlePaceMath.Unit(AttackClass.Rider));
        Assert.Equal("shots", BattlePaceMath.Unit(AttackClass.Crossbow));
        Assert.Equal("throws", BattlePaceMath.Unit(AttackClass.OtherRanged));
    }
}

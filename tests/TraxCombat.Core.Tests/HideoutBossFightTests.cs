using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>Step 19 (Anton, 2026-09-28): the hideout boss fight is a fresh start for the player's side -
/// <see cref="AthleticsMath.FreshStart"/> (to the top the wounds allow), the game's facts the module reads
/// (the objective id, the duel / battle text ids, the controllers), the gate, and the log / summary texts.</summary>
public class HideoutBossFightTests
{
    private static AthleticsRules Rules(bool enabled = true, bool mod = true, bool caps = true)
        => new(enabled, 50, 1.0f, 75, caps, 10, true, 0.75f, 0.75f, 3, 20, 0.7f, 1.0f, true, 2, 1.5f, 60, 0.5f, 0.4f, 50, mod);

    private static Fighter Tired(int skill, double fraction)
    {
        var f = new Fighter { AthleticsSkill = skill };
        var r = Rules();
        // drain him with blows to (about) the fraction asked - the real path, so Exhausted / LastBlowTime are set
        while (f.Fraction > fraction + 1e-9) AthleticsMath.Charge(f, r, 100);
        return f;
    }

    // ------------------------------------------------------------------ the refill

    [Fact]
    public void An_empty_fighter_refills_to_a_full_bar_and_starts_over()
    {
        var r = Rules();
        var f = Tired(120, 0);
        Assert.True(f.Exhausted);
        var o = AthleticsMath.FreshStart(f, r, health: 1.0);
        Assert.True(o.Refilled);
        Assert.Equal(1.0, f.Fraction, 9);
        Assert.False(f.Exhausted);
        Assert.False(f.Regenerating);
        Assert.True(double.IsNegativeInfinity(f.LastBlowTime)); // no regen delay left over
        Assert.Equal(1.0, AthleticsMath.PeakShare(r, f), 9);    // full strength: every curve back to 1
        Assert.Equal(1f, AthleticsMath.AttackSpeedMultiplier(r, f));
        Assert.Equal(1f, AthleticsMath.RunSpeedMultiplier(r, f));
        Assert.Equal(0.0, o.BeforePoints, 9);
        Assert.Equal(120.0, o.AfterPoints, 9);
        Assert.Equal(120.0, o.GainedPoints, 9);
        Assert.True(o.WasExhausted);
        Assert.False(o.Capped);
        Assert.False(o.AlreadyAtTop);
        Assert.Equal(0.0, o.PeakShareBefore, 9);
        Assert.Equal(1.0, o.PeakShareAfter, 9);
    }

    [Fact]
    public void Wounds_still_cap_it_and_a_badly_wounded_man_stays_below_full_strength()
    {
        var r = Rules();
        var f = Tired(100, 0.2);
        var o = AthleticsMath.FreshStart(f, r, health: 0.6);
        Assert.Equal(0.6, f.Fraction, 9);                         // the health left, not the whole bar
        Assert.Equal(0.6, f.Health, 9);                           // today's health is recorded first
        Assert.True(o.Capped);
        Assert.Equal(0.6 / 0.75, AthleticsMath.PeakShare(r, f), 9); // the peak line stays on the full pool
        Assert.Equal(40.0, o.GainedPoints, 9);

        var g = Tired(100, 0.2);
        AthleticsMath.FreshStart(g, Rules(caps: false), health: 0.3); // the cap switched off: a full bar
        Assert.Equal(1.0, g.Fraction, 9);
    }

    [Fact]
    public void A_man_already_at_his_top_is_counted_as_already_full()
    {
        var f = new Fighter { AthleticsSkill = 80 };
        var o = AthleticsMath.FreshStart(f, Rules(), health: 1.0);
        Assert.True(o.Refilled && o.AlreadyAtTop);
        Assert.Equal(0.0, o.GainedPoints, 9);
    }

    [Fact]
    public void Nothing_happens_while_Athletics_or_the_mod_is_off()
    {
        foreach (var r in new[] { Rules(enabled: false), Rules(mod: false) })
        {
            var f = Tired(100, 0.3);
            double before = f.Fraction;
            var o = AthleticsMath.FreshStart(f, r, 1.0);
            Assert.False(o.Refilled);
            Assert.Equal(before, f.Fraction, 9);
        }
    }

    // ------------------------------------------------------------------ the game's facts

    [Fact]
    public void The_boss_objective_is_known_by_its_id_and_the_kind_by_its_text_id()
    {
        Assert.True(HideoutBossFightMath.IsBossObjective("hideout_mission_defeat_hideout_boss_objective"));
        Assert.False(HideoutBossFightMath.IsBossObjective("hideout_mission_clear_the_main_camp_objective"));
        Assert.False(HideoutBossFightMath.IsBossObjective(null));

        Assert.Equal(BossFightKind.Duel, HideoutBossFightMath.KindOf("{=QEynMlwL}Win the Duel"));
        Assert.Equal(BossFightKind.Battle, HideoutBossFightMath.KindOf("{=0sPTRh6L}Win the Fight"));
        Assert.Equal(BossFightKind.Duel, HideoutBossFightMath.KindOf("{=QEynMlwL}Gewinne das Duell")); // the id decides, any language
        Assert.Equal(BossFightKind.Duel, HideoutBossFightMath.KindOf("Win the Duel"));                 // no id: the English fallback
        Assert.Equal(BossFightKind.Battle, HideoutBossFightMath.KindOf("Win the Fight"));
        Assert.Equal(BossFightKind.Unknown, HideoutBossFightMath.KindOf("{=xyz}Gagner"));
        Assert.Equal(BossFightKind.Unknown, HideoutBossFightMath.KindOf(null));
    }

    [Fact]
    public void Both_hideout_controllers_are_recognised_by_name()
    {
        Assert.True(HideoutBossFightMath.IsHideoutController("HideoutMissionController"));
        Assert.True(HideoutBossFightMath.IsHideoutController("HideoutAmbushMissionController"));
        Assert.False(HideoutBossFightMath.IsHideoutController("HideoutCinematicController"));
        Assert.False(HideoutBossFightMath.IsHideoutController(null));
    }

    [Fact]
    public void The_gate_asks_the_master_switch_first()
    {
        Assert.Null(HideoutBossFightMath.RefillOffBecause(true, true, true));
        Assert.Equal("ModEnabled", HideoutBossFightMath.RefillOffBecause(false, false, false));
        Assert.Equal("AthleticsEnabled", HideoutBossFightMath.RefillOffBecause(true, false, true));
        Assert.Equal("HideoutBossFightRefill", HideoutBossFightMath.RefillOffBecause(true, true, false));

        var s = new TraxSettings();
        Assert.True(s.HideoutBossFightRefill); // DESIGN's initial value: on
        Assert.Null(HideoutBossFightMath.RefillOffBecause(s));
        s.Set(SettingsSchema.HideoutBossFightRefill, false, SettingSources.Mcm);
        Assert.Equal("HideoutBossFightRefill", HideoutBossFightMath.RefillOffBecause(s)); // read live
    }

    // ------------------------------------------------------------------ the texts

    private static HideoutBossFightStats DuelWon()
    {
        var r = Rules();
        var h = new HideoutBossFightStats { Controller = "HideoutMissionController" };
        h.NoteIntro(312.4);
        Assert.True(h.BeginFight(340.1, BossFightKind.Duel));
        var you = Tired(120, 0.25);
        h.AddPlayerSide(true, you.Fraction, 120);
        var o = AthleticsMath.FreshStart(you, r, 1.0);
        h.AddRefill(in o, isYou: true);
        h.YourPauseReleased = true;
        h.AddBossSide(1.0, 1.0);
        for (int i = 0; i < 7; i++) h.AddAside();
        return h;
    }

    [Fact]
    public void The_fight_line_says_who_refilled_and_proves_the_boss_came_fresh()
    {
        var h = DuelWon();
        Assert.Equal("hideout boss fight (duel): refilled 1 of the player's side (you 30.0 → 120.0 of 120) at 340.1 s - in a duel only you: your men stand aside; "
                     + "1 on the player's side, 0 already full, 0 held below full by their wounds (to the health they have left), 0 were empty; +90.0 Athletics in all; "
                     + "released: your attack pause yes, AI pauses 0 (+0 queued), step backs 0 (+0 queued); the boss's side: 1 fighter, all fresh (at full - spawned for this fight); "
                     + "standing aside: 7 (not refilled)", h.FightLine());

        var b = new HideoutBossFightStats { Controller = "HideoutAmbushMissionController" };
        b.BeginFight(90, BossFightKind.Battle);
        var r = Rules();
        var man = Tired(100, 0);
        b.AddPlayerSide(false, man.Fraction, 100);
        var o = AthleticsMath.FreshStart(man, r, 0.5);
        b.AddRefill(in o, isYou: false);
        b.PausesReleased = 1;
        b.AddBossSide(1.0, 1.0);
        b.AddBossSide(0.62, 1.0); // not fresh: the line says so plainly
        Assert.StartsWith("hideout boss fight (battle): refilled 1 of the player's side (you: not on the field) at 90.0 s - you and your men still standing; ", b.FightLine());
        Assert.Contains("1 held below full by their wounds (to the health they have left), 1 were empty; +50.0 Athletics in all; released: your attack pause no, AI pauses 1 (+0 queued)", b.FightLine());
        Assert.EndsWith("the boss's side: 2 fighters, 1 at full, lowest 62% - NOT all fresh (tell Claude)", b.FightLine());
    }

    [Fact]
    public void Switched_off_or_failed_the_line_says_nobody_refilled()
    {
        var h = new HideoutBossFightStats();
        h.BeginFight(50, BossFightKind.Battle);
        h.AddPlayerSide(true, 0.5, 100);
        h.NotRefilled("HideoutBossFightRefill");
        Assert.StartsWith("hideout boss fight (battle) at 50.0 s: nobody refilled - HideoutBossFightRefill is off (the player's side: 1, you 50.0 of 100)", h.FightLine());

        var m = new HideoutBossFightStats();
        m.BeginFight(50, BossFightKind.Unknown);
        m.NotRefilled("ModEnabled");
        Assert.StartsWith("hideout boss fight (duel or battle) at 50.0 s: nobody refilled - the whole mod is off (ModEnabled)", m.FightLine());

        var e = new HideoutBossFightStats();
        e.BeginFight(50, BossFightKind.Duel);
        e.Failed("hideout.refill");
        Assert.Equal("hideout boss fight (duel) at 50.0 s: the refill FAILED (hideout.refill) - nothing refilled, the fight goes on as it was (tell Claude)", e.FightLine());
    }

    [Fact]
    public void The_fight_is_seen_once_per_mission()
    {
        var h = new HideoutBossFightStats();
        Assert.True(h.BeginFight(10, BossFightKind.Duel));
        Assert.False(h.BeginFight(20, BossFightKind.Battle));
        Assert.Equal(1, h.SeenAgain);
        Assert.Equal(BossFightKind.Duel, h.Kind);
        Assert.Equal(10, h.FightAt);
    }

    [Fact]
    public void The_summary_names_the_boss_phase_and_says_plainly_when_the_hook_never_fired()
    {
        Assert.Empty(new HideoutBossFightStats().SummaryLines()); // not a hideout: no line

        var none = new HideoutBossFightStats { Controller = "HideoutMissionController" };
        Assert.Equal(new[] { "hideout boss phase (HideoutMissionController): none this mission - the boss intro never played (the camp was not cleared: left, lost, or no boss phase)" },
            none.SummaryLines());

        var missed = new HideoutBossFightStats { Controller = "HideoutAmbushMissionController" };
        missed.NoteIntro(200);
        Assert.Equal(new[]
        {
            "hideout boss phase (HideoutAmbushMissionController): the boss intro played at 200.0 s, but the start of the boss fight was NEVER SEEN - "
            + "if you fought the boss (duel or battle), the refill hook never fired: tell Claude (if you left during the talk with the boss, nothing is wrong)",
        }, missed.SummaryLines());

        Assert.Equal(new[]
        {
            "hideout boss phase (HideoutMissionController): the boss intro at 312.4 s, the fight began at 340.1 s as a DUEL - a fresh start: 1 of the player's side refilled "
            + "(1 on it, 0 already full, 0 held by wounds; you 30.0 → 120.0 of 120); the boss's side: 1 fighter, all fresh (at full - spawned for this fight)",
        }, DuelWon().SummaryLines());

        var failed = new HideoutBossFightStats { Controller = "HideoutMissionController" };
        failed.NoteIntro(10);
        failed.BeginFight(30, BossFightKind.Battle);
        failed.Failed("hideout.refill");
        Assert.Contains("as a BATTLE (men to men) - the refill FAILED (hideout.refill): nothing refilled - tell Claude", failed.SummaryLines()[0]);
    }
}

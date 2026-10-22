using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 82 - MEETING A BASH OR A KICK THE RIGHT WAY (Toni 2026-10-22: "a valid way to transfer your body's kinetic energy ... if met properly
/// the initiator should be caught off balance"; AI_NOTES "Step 82", DESIGN decision 78): the settings, the guard read, the row selection and
/// its ceilings, the attacker-side pressure of a correct meet (the riposte's, the roles turned), the defender's rows (a bash the shield stopped,
/// a bash that landed), a kick met any other way as before, the master switch, the AI's read chance by bar, the reaction-time cut, the guard
/// frame, the summary lines.
/// </summary>
public class Step82Tests
{
    private sealed class Dice : IRandomSource
    {
        private readonly double[] _v;
        private int _i;
        public double Rest = 0.999;

        public Dice(params double[] values) => _v = values;

        public double NextDouble() => _i < _v.Length ? _v[_i++] : Rest;
    }

    private static ImpactRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return ImpactRules.From(s);
    }

    /// <summary>A kick / bash: the attacker (pool 100, skill 100, a 4 kg shield for a bash) at <paramref name="attackerBar"/>, the defender (pool
    /// 100, Athletics 100) at <paramref name="bar"/>, met as <paramref name="meet"/> (a correct meet's guard: a 4 kg shield / a 1.2 kg sword,
    /// skill <paramref name="guardSkill"/>).</summary>
    private static ImpactFacts Facts(ImpactEvent e, ImpactMeet meet, double bar = 1.0, double attackerBar = 1.0, bool blocked = false,
        double attackerSkill = 100, double guardSkill = 100, bool shield = true)
    {
        double fA = Math.Min(1, attackerBar / 0.75);
        return new ImpactFacts
        {
            Event = e,
            Meet = meet,
            AltBlocked = blocked,
            AttackerPoints = 100 * attackerBar,
            AttackerF = fA,
            AttackerAnimation = 0.85 + 0.15 * fA,
            AttackerSkill = attackerSkill,
            DefenderPoints = 100 * bar,
            DefenderBar = bar,
            DefenderF = Math.Min(1, bar / 0.75),
            DefenderSkill = 100,
            WeaponWeight = e == ImpactEvent.Bash ? 4.0 : 0,
            DamageType = ImpactDamageType.Blunt,
            GuardWeight = ImpactMeetMath.ByShield(meet) ? 4.0 : 1.2,
            MeetGuardSkill = guardSkill,
            DefenderHasShield = shield,
            DefenderMaxHealth = 100,
        };
    }

    private static ImpactDecision Decide(in ImpactFacts f, ImpactRules? rules = null, IRandomSource? dice = null)
    {
        var r = rules ?? Rules();
        var d = default(ImpactState);
        var a = default(ImpactState);
        return ImpactMath.Decide(in r, in f, ref d, ref a, 100, dice ?? new Dice());
    }

    private static void Pct(double percent, double chance, string what) =>
        Assert.True(Math.Abs(100 * chance - percent) <= 0.55, what + ": " + (100 * chance).ToString("0.00") + "% vs " + percent + "%");

    // ------------------------------------------------------------------ the settings

    [Fact]
    public void The_switch_is_on_the_player_page_the_ceilings_and_the_read_in_Advanced_Impact_live_and_shipped()
    {
        var all = SettingsSchema.All.ToList();
        Assert.Equal(all.IndexOf(SettingsSchema.ImpactOnPlayerPercent) + 1, all.IndexOf(SettingsSchema.ImpactMeetRulesEnabled));
        Assert.Equal(SettingsPage.Player, SettingsSchema.ImpactMeetRulesEnabled.Group.Page);
        var adv = new[]
        {
            SettingsSchema.MeetBashShieldRightStaggerPercent, SettingsSchema.MeetBashShieldRightFallPercent, SettingsSchema.MeetBashWeaponRightStaggerPercent,
            SettingsSchema.MeetBashWeaponRightFallPercent, SettingsSchema.MeetBashShieldWrongStaggerPercent, SettingsSchema.MeetBashShieldWrongFallPercent,
            SettingsSchema.MeetBashShieldWrongShieldDropPercent, SettingsSchema.MeetBashWeaponWrongStaggerPercent, SettingsSchema.MeetBashWeaponWrongFallPercent,
            SettingsSchema.MeetBashOpenStaggerPercent, SettingsSchema.MeetBashOpenFallPercent, SettingsSchema.MeetBashOpenShieldDropPercent,
            SettingsSchema.MeetKickShieldDownStaggerPercent, SettingsSchema.MeetKickShieldDownFallPercent, SettingsSchema.MeetKickWeaponDownStaggerPercent,
            SettingsSchema.MeetKickWeaponDownFallPercent, SettingsSchema.MeetAiReadScalePercent, SettingsSchema.MeetAiReadReactionSeconds, SettingsSchema.MeetAiReadMaxHoldSeconds,
        };
        int after = all.IndexOf(SettingsSchema.AiKickRearRankMetres);
        for (int i = 0; i < adv.Length; i++)
        {
            Assert.Equal(after + 1 + i, all.IndexOf(adv[i]));
            Assert.Same(SettingsSchema.ImpactGroup, adv[i].Group);
            Assert.Equal(ApplyTiming.Live, adv[i].Timing);
        }
        string text = DefaultsFile.ReadEmbeddedText();
        foreach (var line in new[]
                 {
                     "\"ImpactMeetRulesEnabled\": true,", "\"MeetBashShieldRightStaggerPercent\": 70,", "\"MeetBashShieldRightFallPercent\": 30,",
                     "\"MeetBashWeaponRightStaggerPercent\": 45,", "\"MeetBashWeaponRightFallPercent\": 15,", "\"MeetBashShieldWrongStaggerPercent\": 85,",
                     "\"MeetBashShieldWrongFallPercent\": 40,", "\"MeetBashShieldWrongShieldDropPercent\": 35,", "\"MeetBashWeaponWrongStaggerPercent\": 95,",
                     "\"MeetBashWeaponWrongFallPercent\": 50,", "\"MeetBashOpenStaggerPercent\": 98,", "\"MeetBashOpenFallPercent\": 60,", "\"MeetBashOpenShieldDropPercent\": 45,",
                     "\"MeetKickShieldDownStaggerPercent\": 60,", "\"MeetKickShieldDownFallPercent\": 25,", "\"MeetKickWeaponDownStaggerPercent\": 50,",
                     "\"MeetKickWeaponDownFallPercent\": 30,", "\"MeetAiReadScalePercent\": 100,", "\"MeetAiReadReactionSeconds\": 0.15,", "\"MeetAiReadMaxHoldSeconds\": 1.5,",
                 })
            Assert.Contains(line, text, StringComparison.Ordinal);
        var s = new TraxSettings();
        s.Set(SettingsSchema.MeetBashOpenStaggerPercent, 50, SettingSources.Mcm);
        Assert.Equal(0.5, ImpactMeetRules.From(s).BashOpenStagger, 9); // live
    }

    [Fact]
    public void The_master_switch_comes_first_then_the_rules_own_switch()
    {
        var on = ImpactMeetRules.From(new TraxSettings());
        Assert.True(on.Enabled);
        Assert.True(on.AiReads);
        var s = new TraxSettings();
        s.Set(SettingsSchema.ModEnabled, false, "t");
        var off = ImpactMeetRules.From(s);
        Assert.False(off.Enabled);
        Assert.False(off.AiReads);
        Assert.Equal("ModEnabled", off.OffBecause);
        var s2 = new TraxSettings();
        s2.Set(SettingsSchema.ImpactMeetRulesEnabled, false, "t");
        Assert.Equal("ImpactMeetRulesEnabled", ImpactMeetRules.From(s2).OffBecause);
        var s3 = new TraxSettings();
        s3.Set(SettingsSchema.MeetAiReadScalePercent, 0, "t");
        Assert.True(ImpactMeetRules.From(s3).Enabled);
        Assert.False(ImpactMeetRules.From(s3).AiReads);
        // off: a judged meet is ignored - step 65's decision exactly
        var r = Rules(t => t.Set(SettingsSchema.ImpactMeetRulesEnabled, false, "t"));
        var f = Facts(ImpactEvent.Bash, ImpactMeet.BashShieldRight);
        var d = Decide(in f, r);
        Assert.Equal(ImpactMeet.NotJudged, d.Meet);
        var plain = f;
        plain.Meet = ImpactMeet.NotJudged;
        var d0 = Decide(in plain, r);
        Assert.Equal(d0.X, d.X, 9);
        Assert.Equal(d0.Knockdown.Chance, d.Knockdown.Chance, 9);
        Assert.Contains("OFF (ImpactMeetRulesEnabled)", ImpactMeetRules.From(s2).Describe());
        Assert.Contains("ON - a bash met by a RIGHT guard", on.Describe());
    }

    // ------------------------------------------------------------------ the guard read and the row

    [Fact]
    public void The_guard_is_read_from_the_action_its_direction_and_the_defend_flag()
    {
        Assert.Equal(MeetGuardKind.Shield, ImpactMeetMath.GuardOf(2, 7, 0, out var d));
        Assert.Equal(MeetDir.Right, d);
        Assert.Equal(MeetGuardKind.Shield, ImpactMeetMath.GuardOf(2, -1, 0x2000, out d)); // the action's direction unknown → the defend flag
        Assert.Equal(MeetDir.Down, d);
        Assert.Equal(MeetGuardKind.Shield, ImpactMeetMath.GuardOf(2, -1, 0, out d));
        Assert.Equal(MeetDir.Unknown, d);
        Assert.Equal(MeetGuardKind.Weapon, ImpactMeetMath.GuardOf(7, -1, 0, out d)); // DefendForward1h = down
        Assert.Equal(MeetDir.Down, d);
        Assert.Equal(MeetGuardKind.Weapon, ImpactMeetMath.GuardOf(5, -1, 0, out d)); // DefendRight2h
        Assert.Equal(MeetDir.Right, d);
        Assert.Equal(MeetGuardKind.Weapon, ImpactMeetMath.GuardOf(14, -1, 0, out d)); // DefendLeftStaff
        Assert.Equal(MeetDir.Left, d);
        Assert.Equal(MeetGuardKind.Weapon, ImpactMeetMath.GuardOf(12, -1, 0, out d)); // DefendUpStaff
        Assert.Equal(MeetDir.Up, d);
        Assert.Equal(MeetGuardKind.Swinging, ImpactMeetMath.GuardOf(19, -1, 0, out _)); // ReadyMelee
        Assert.Equal(MeetGuardKind.Swinging, ImpactMeetMath.GuardOf(20, -1, 0, out _)); // ReleaseMelee
        Assert.Equal(MeetGuardKind.Swinging, ImpactMeetMath.GuardOf(31, -1, 0, out _)); // his own bash
        Assert.Equal(MeetGuardKind.None, ImpactMeetMath.GuardOf(35, -1, 0, out _)); // Idle
        Assert.Equal(MeetGuardKind.None, ImpactMeetMath.GuardOf(0, -1, 0, out _));
    }

    [Fact]
    public void The_row_is_selected_by_kind_direction_and_whether_the_shield_stopped_it()
    {
        Assert.Equal(ImpactMeet.BashShieldRight, ImpactMeetMath.Classify(true, true, MeetGuardKind.Shield, MeetDir.Right));
        Assert.Equal(ImpactMeet.BashShieldWrong, ImpactMeetMath.Classify(true, true, MeetGuardKind.Shield, MeetDir.Down));
        Assert.Equal(ImpactMeet.BashShieldWrong, ImpactMeetMath.Classify(true, true, MeetGuardKind.None, MeetDir.Unknown)); // stopped = a shield, whatever the read
        Assert.Equal(ImpactMeet.BashShieldRight, ImpactMeetMath.Classify(true, false, MeetGuardKind.Shield, MeetDir.Right));
        Assert.Equal(ImpactMeet.BashWeaponRight, ImpactMeetMath.Classify(true, false, MeetGuardKind.Weapon, MeetDir.Right));
        Assert.Equal(ImpactMeet.BashWeaponWrong, ImpactMeetMath.Classify(true, false, MeetGuardKind.Weapon, MeetDir.Up));
        Assert.Equal(ImpactMeet.BashWeaponWrong, ImpactMeetMath.Classify(true, false, MeetGuardKind.Weapon, MeetDir.Unknown));
        Assert.Equal(ImpactMeet.BashOpen, ImpactMeetMath.Classify(true, false, MeetGuardKind.None, MeetDir.Unknown));
        Assert.Equal(ImpactMeet.BashOpen, ImpactMeetMath.Classify(true, false, MeetGuardKind.Swinging, MeetDir.Unknown));
        Assert.Equal(ImpactMeet.KickShieldDown, ImpactMeetMath.Classify(false, false, MeetGuardKind.Shield, MeetDir.Down));
        Assert.Equal(ImpactMeet.KickWeaponDown, ImpactMeetMath.Classify(false, false, MeetGuardKind.Weapon, MeetDir.Down));
        Assert.Equal(ImpactMeet.KickOther, ImpactMeetMath.Classify(false, false, MeetGuardKind.Shield, MeetDir.Right));
        Assert.Equal(ImpactMeet.KickOther, ImpactMeetMath.Classify(false, false, MeetGuardKind.None, MeetDir.Unknown));
        // the ceilings - Toni's numbers (stagger / fall / drop)
        var m = ImpactMeetRules.From(new TraxSettings());
        void Row(ImpactMeet meet, double st, double fall, double drop, bool attacker)
        {
            ImpactMeetMath.Row(in m, meet, out double a, out double b, out double c);
            Assert.Equal(st, a, 9);
            Assert.Equal(fall, b, 9);
            Assert.Equal(drop, c, 9);
            Assert.Equal(attacker, ImpactMeetMath.AttackerAtRisk(meet));
            Assert.True(ImpactMeetMath.HasRow(meet));
        }
        Row(ImpactMeet.BashShieldRight, 0.70, 0.30, 0, true);
        Row(ImpactMeet.BashWeaponRight, 0.45, 0.15, 0, true);
        Row(ImpactMeet.BashShieldWrong, 0.85, 0.40, 0.35, false);
        Row(ImpactMeet.BashWeaponWrong, 0.95, 0.50, 0, false);
        Row(ImpactMeet.BashOpen, 0.98, 0.60, 0.45, false);
        Row(ImpactMeet.KickShieldDown, 0.60, 0.25, 0, true);
        Row(ImpactMeet.KickWeaponDown, 0.50, 0.30, 0, true);
        Assert.False(ImpactMeetMath.HasRow(ImpactMeet.KickOther));
        Assert.False(ImpactMeetMath.HasRow(ImpactMeet.NotJudged));
    }

    // ------------------------------------------------------------------ the correct meet: the attacker at risk

    [Fact]
    public void A_correct_meet_leaves_the_defender_untouched_and_puts_the_attacker_at_risk_by_his_own_tiredness()
    {
        // fresh even men: x_r 0 - nothing on either man
        var fresh = Decide(Facts(ImpactEvent.Bash, ImpactMeet.BashShieldRight, blocked: true));
        Assert.True(fresh.MeetAttacker);
        Assert.True(fresh.MeetBlocked);
        Assert.False(fresh.Knockdown.InRow || fresh.ShieldDrop.InRow || fresh.Stagger.InRow);
        Assert.Equal(0, fresh.Riposte.Chance, 9);
        Assert.Equal(0, fresh.ThrownBack.Chance, 9);
        // an EMPTY basher into a fresh guard: the ceilings - 30 % down, else 70 % staggered
        var empty = Decide(Facts(ImpactEvent.Bash, ImpactMeet.BashShieldRight, attackerBar: 0, blocked: true));
        Pct(30, empty.ThrownBack.Chance, "the empty basher falls");
        Pct(70, empty.Riposte.Chance, "the empty basher staggers");
        Assert.False(empty.Knockdown.InRow || empty.Stagger.InRow);
        // a half-tired basher in between, and the attacker's tiredness is what moves it (the defender exhausted changes ~nothing)
        var half = Decide(Facts(ImpactEvent.Bash, ImpactMeet.BashShieldRight, attackerBar: 0.375, blocked: true));
        Assert.True(half.Riposte.Chance > 0.2 && half.Riposte.Chance < empty.Riposte.Chance, "half " + half.Riposte.Chance);
        var tiredGuard = Decide(Facts(ImpactEvent.Bash, ImpactMeet.BashShieldRight, bar: 0.05, attackerBar: 0.375, blocked: true));
        Assert.True(tiredGuard.Riposte.Chance <= half.Riposte.Chance + 1e-9, "a tired guard is not a better guard");
        Assert.Equal(0.5, half.TAttacker, 9);
        // a landed correct meet (a weapon right): braced - the knock-back refused, the reaction cut, no defender roll
        var weapon = Decide(Facts(ImpactEvent.Bash, ImpactMeet.BashWeaponRight, attackerBar: 0));
        Assert.True(weapon.Braced);
        Assert.Equal(0.2, weapon.ReelFactor, 6); // KickBraceReelPercent 20
        Pct(15, weapon.ThrownBack.Chance, "the weapon-right basher falls");
        Pct(45, weapon.Riposte.Chance, "the weapon-right basher staggers");
        // kicks met DOWN: the kicker's own rows
        var kick = Decide(Facts(ImpactEvent.Kick, ImpactMeet.KickShieldDown, attackerBar: 0));
        Pct(25, kick.ThrownBack.Chance, "the kicker falls");
        Pct(60, kick.Riposte.Chance, "the kicker staggers");
        var kickW = Decide(Facts(ImpactEvent.Kick, ImpactMeet.KickWeaponDown, attackerBar: 0));
        Pct(30, kickW.ThrownBack.Chance, "the kicker falls (weapon down)");
        Pct(50, kickW.Riposte.Chance, "the kicker staggers (weapon down)");
    }

    [Fact]
    public void The_attacker_side_pressure_is_the_mismatch_turned_round_the_defenders_body_skill_and_guard()
    {
        var r = Rules();
        // a fresh weak basher (skill 20) into a fresh master's shield (skill 300): the mismatch alone throws him
        var f = Facts(ImpactEvent.Bash, ImpactMeet.BashShieldRight, blocked: true, attackerSkill: 20, guardSkill: 300);
        var d = Decide(in f, r);
        Assert.True(d.XRiposte > 0.04, "x_r " + d.XRiposte); // skill x1.25 vs x0.78 = x1.6: 0.18 doublings past the even band, x 0.25
        Assert.True(d.Riposte.Chance > 0, "a master punishes a recruit's bash");
        // the guard is the shield's (ImpactGuardShieldRight x its weight), the skill the guard's
        Assert.Equal(ImpactMeetMath.Guard(in r, ImpactMeet.BashShieldRight, 4.0), d.Guard, 9);
        Assert.Equal(ImpactMath.SkillFactor(in r, 300), d.SkillD, 9);
        // the roles turned: the same blow on a defender at risk keeps the kick / bash row's guard (none) and his Athletics skill
        var open = Decide(Facts(ImpactEvent.Bash, ImpactMeet.BashOpen, attackerSkill: 20, guardSkill: 300));
        Assert.Equal(r.GuardNone, open.Guard, 9);
        Assert.Equal(ImpactMath.SkillFactor(in r, 100), open.SkillD, 9);
        // a weapon guard is weaker than a shield (the parry's guard, a lighter item): a smaller x_r at the same tiredness
        var shield = Decide(Facts(ImpactEvent.Bash, ImpactMeet.BashShieldRight, attackerBar: 0.375, blocked: true));
        var sword = Decide(Facts(ImpactEvent.Bash, ImpactMeet.BashWeaponRight, attackerBar: 0.375));
        Assert.True(sword.XRiposte < shield.XRiposte, sword.XRiposte + " vs " + shield.XRiposte);
        // you as the basher: your share
        var you = Facts(ImpactEvent.Bash, ImpactMeet.BashShieldRight, attackerBar: 0, blocked: true);
        you.AttackerIsPlayer = true;
        var dy = Decide(in you, Rules(t => t.Set(SettingsSchema.ImpactOnPlayerPercent, 50, "t")));
        Pct(15, dy.ThrownBack.Chance, "you fall at half");
        // his recent outcomes halve the next
        var attacker = default(ImpactState);
        attacker.NoteOutcome(99);
        var defender = default(ImpactState);
        var ff = Facts(ImpactEvent.Bash, ImpactMeet.BashShieldRight, attackerBar: 0, blocked: true);
        var dd = ImpactMath.Decide(in r, in ff, ref defender, ref attacker, 100, new Dice());
        Pct(15, dd.ThrownBack.Chance, "halved for his last outcome");
    }

    // ------------------------------------------------------------------ the defender at risk

    [Fact]
    public void A_bash_the_shield_stopped_with_a_wrong_guard_rolls_the_blockers_rows_on_his_full_pressure()
    {
        var empty = Decide(Facts(ImpactEvent.Bash, ImpactMeet.BashShieldWrong, bar: 0, blocked: true));
        Assert.False(empty.MeetAttacker);
        Assert.True(empty.MeetBlocked);
        Pct(40, empty.Knockdown.Chance, "the empty blocker falls");
        Pct(35, empty.ShieldDrop.Chance, "the empty blocker drops it");
        Pct(85, empty.Stagger.Chance, "the empty blocker staggers");
        Assert.True(double.IsNaN(empty.ReelFactor)); // no knock-back plays on a stopped bash
        var fresh = Decide(Facts(ImpactEvent.Bash, ImpactMeet.BashShieldWrong, blocked: true));
        Assert.True(fresh.Stagger.Chance < 0.01, "two fresh even men: " + fresh.Stagger.Chance);
        Assert.Equal(0, fresh.Knockdown.Chance, 9);
        // a stagger gets its §3.7 length and his record reels
        var r = Rules();
        var dState = default(ImpactState);
        var aState = default(ImpactState);
        var f = Facts(ImpactEvent.Bash, ImpactMeet.BashShieldWrong, bar: 0, blocked: true);
        var d = ImpactMath.Decide(in r, in f, ref dState, ref aState, 100, new Dice(0.99, 0.99, 0.01, 0.5));
        Assert.True(d.Stagger.Happens);
        Assert.True(d.StaggerSeconds > 0);
        Assert.True(dState.Reeling(100.1));
    }

    [Fact]
    public void A_bash_that_landed_gets_through_at_the_rows_ceiling_times_the_brace_curve_and_held_means_no_fall()
    {
        var r = Rules();
        var fresh = Decide(Facts(ImpactEvent.Bash, ImpactMeet.BashOpen));
        double curve = KickBraceMath.FailChance(r.Brace, fresh.X);
        Assert.Equal(0.98 * curve, fresh.Stagger.Chance, 9);
        Assert.Equal(fresh.Stagger.Chance, fresh.BraceFail, 9);
        var weapon = Decide(Facts(ImpactEvent.Bash, ImpactMeet.BashWeaponWrong));
        Assert.Equal(0.95 * curve, weapon.Stagger.Chance, 9);
        // a spent man: the ceiling itself
        var spent = Decide(Facts(ImpactEvent.Bash, ImpactMeet.BashOpen, bar: 0.1));
        Assert.True(spent.BraceSpent);
        Assert.Equal(0.98, spent.Stagger.Chance, 9);
        // held (the die above): braced - no fall, no drop
        var held = Decide(Facts(ImpactEvent.Bash, ImpactMeet.BashOpen), dice: new Dice(0.999));
        Assert.True(held.Braced);
        Assert.Equal(ImpactRefusal.Braced, held.Knockdown.Refusal);
        Assert.Equal(ImpactRefusal.Braced, held.ShieldDrop.Refusal);
        // through on an empty man: the fall and the drop at their ceilings, no shield = no drop
        var through = Decide(Facts(ImpactEvent.Bash, ImpactMeet.BashOpen, bar: 0, shield: false), dice: new Dice(0.01, 0.99, 0.5));
        Assert.True(through.Stagger.Happens);
        Pct(60, through.Knockdown.Chance, "the open man falls");
        Assert.Equal(ImpactRefusal.NoShield, through.ShieldDrop.Refusal);
        Assert.True(through.ReelFactor > 1, "an empty man's full reel");
    }

    [Fact]
    public void A_kick_met_any_other_way_is_decided_as_before()
    {
        var f = Facts(ImpactEvent.Kick, ImpactMeet.KickOther, bar: 0.3);
        var d = Decide(in f);
        var plain = f;
        plain.Meet = ImpactMeet.NotJudged;
        var d0 = Decide(in plain);
        Assert.Equal(ImpactMeet.KickOther, d.Meet);
        Assert.False(d.MeetAttacker);
        Assert.Equal(d0.X, d.X, 9);
        Assert.Equal(d0.Knockdown.Chance, d.Knockdown.Chance, 9);
        Assert.Equal(d0.BraceFail, d.BraceFail, 9);
        Assert.Equal(Rules().KickKnockdown, d.Knockdown.Ceiling, 9);
    }

    // ------------------------------------------------------------------ the AI's read

    [Fact]
    public void The_read_chance_is_the_block_chance_falling_with_the_bar_and_scaled()
    {
        // a 150-skill man on realistic (the game: block decision 0.84, a random direction 0.89 of the time), step 30 at 100 %
        var tb = new TiredBlockRules(true, true, 100);
        double At(double f) => ImpactMeetMath.ReadChanceAt(1.0, 0.84f, 0.89f, TiredBlockMath.Weight(in tb, f, false));
        double fresh = At(1), half = At(0.5), empty = At(0);
        Assert.Equal(0.84 * (1 - 0.89 * 0.75), fresh, 5);
        Assert.Equal(0.84 * (1 - 0.945 * 0.75), half, 5);
        Assert.Equal(0.84 * 0.25, empty, 5); // a spent man guesses: one in four
        Assert.True(fresh > half && half > empty);
        // a lord (block 0.98, random 0.115) reads most of them; a looter (0.5, 1.0) one in eight
        Assert.True(ImpactMeetMath.ReadChance(1, 0.98f, 0.115f) > 0.88);
        Assert.Equal(0.125, ImpactMeetMath.ReadChance(1, 0.5f, 1f), 9);
        // the scale, clamped 0-1
        Assert.Equal(0, ImpactMeetMath.ReadChance(0, 0.98f, 0.1f), 9);
        Assert.Equal(1, ImpactMeetMath.ReadChance(2, 0.98f, 0f), 9);
        Assert.Equal(0, ImpactMeetMath.ReadChance(1, float.NaN, 0f), 9);
        Assert.Equal(0.5 * fresh, ImpactMeetMath.ReadChanceAt(0.5, 0.84f, 0.89f, 0f), 9);
    }

    [Fact]
    public void The_guard_goes_up_after_the_reaction_time_and_a_sooner_hit_is_too_late()
    {
        Assert.False(ImpactMeetMath.GuardUp(10.10, 10, 0.15));
        Assert.True(ImpactMeetMath.GuardUp(10.15, 10, 0.15));
        Assert.True(ImpactMeetMath.TooLate(10, 10.10, 0.15));
        Assert.False(ImpactMeetMath.TooLate(10, 10.15, 0.15));
        Assert.False(ImpactMeetMath.TooLate(10, 10.55, 0.15)); // an AI bash's 0.4 s guard + its swing: in time
        Assert.True(ImpactMeetMath.GuardUp(10, 10, 0));
        // the frame: his attack bits and his own guard out, the read guard in
        uint frame = ImpactMeetMath.GuardFrame(0x40 | 0x2000 | 0x1, ImpactMeetMath.GuardFlag(true));
        Assert.Equal(0x800u | 0x1u, frame);
        Assert.Equal(0x2000u, ImpactMeetMath.GuardFlag(false));
    }

    [Fact]
    public void The_summary_lines_name_each_meeting_the_outcomes_and_the_reads()
    {
        var st = new ImpactMeetStats();
        st.NoteDecision(ImpactMeet.BashShieldRight, true, false, false, false, false, true, false, true);
        st.NoteDecision(ImpactMeet.BashOpen, false, false, true, false, false, false, true, false);
        st.NoteGuard(MeetGuardKind.Shield, MeetDir.Unknown);
        st.NoteRead(0, 0.3, true);
        st.NoteReadHit(0, false, true);
        st.NoteRead(3, 0.2, false);
        st.NoteEngineSide(true, true, 2);
        var lines = st.SummaryLines(ImpactMeetRules.From(new TraxSettings()));
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("impact meets (step 82) - each kick / bash by how it was met (ON)", lines[0]);
        Assert.Contains("bash - shield, guard right 1 → the ATTACKER staggered 1 (100%)", lines[0]);
        Assert.Contains("(an AI read it: 1)", lines[0]);
        Assert.Contains("bash - no guard or mid-swing 1 → the defender staggered 1 (100%)", lines[0]);
        Assert.Contains("(you in 1)", lines[0]);
        Assert.Contains("a guard with NO direction read 1", lines[1]);
        Assert.Contains("the ENGINE called correct-side 1", lines[1]);
        Assert.Contains("100-75%: rolled 1 (chance avg 30%) → READ 1 (100%); of the reads hit: in time 1, TOO LATE 0, met correctly 1", lines[2]);
        Assert.Contains("25-0%: rolled 1 (chance avg 20%) → READ 0 (0%)", lines[2]);
        Assert.Equal(2, st.Total);
    }
}

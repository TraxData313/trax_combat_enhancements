using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 88 - A WEAPON PUSH FOR MEN WITHOUT A SHIELD + NEW SHARES (Toni 2026-10-24: "with my two handed axe guarding and pressing shield bash it
/// pushes with the axe"; "make them as the shield bash, but make shield up the correct block for them"; AI_NOTES "Step 88", DESIGN decision 81):
/// the push share placed / shipped / cut, the shieldman's 25 / 40 / 35 and the no-shield 35 / 20 / 45 on seeded rolls, a push played like a bash
/// (its guard, its path), config format 20, the push's correct meet = a guard UP, the push rows on the bash's ceilings, the summary text.
/// </summary>
public class Step88Tests
{
    private sealed class Seeded : IRandomSource
    {
        private readonly Random _r;

        public Seeded(int seed) => _r = new Random(seed);

        public double NextDouble() => _r.NextDouble();
    }

    private sealed class Dice : IRandomSource
    {
        private readonly double[] _v;
        private int _i;
        public double Rest = 0.999;

        public Dice(params double[] values) => _v = values;

        public double NextDouble() => _i < _v.Length ? _v[_i++] : Rest;
    }

    /// <summary>The shipped shares (defaults.json - the tests run on DESIGN's initial values, 20 / 35 for the shieldman).</summary>
    private static AiKickRules Shipped() => new AiKickRules(true, true, true, 1.6, 40, 2, true, 25, 40, 35, 0.6, 0.75, 1.2, 20);

    /// <summary>defaults.json's step-88 values for the migration's seam.</summary>
    private static double ShippedDefault(ParamDef p) =>
        p == SettingsSchema.AiShieldmanKickPercent ? 25 : p == SettingsSchema.AiShieldmanBashPercent ? 40 : p.Default;

    // ------------------------------------------------------------------ the shares

    [Fact]
    public void The_push_share_sits_after_the_no_shield_kick_on_the_Advanced_page_live_and_the_new_shares_ship()
    {
        var all = SettingsSchema.All.ToList();
        var p = SettingsSchema.AiNoShieldPushPercent;
        Assert.Equal(all.IndexOf(SettingsSchema.AiNoShieldKickPercent) + 1, all.IndexOf(p));
        Assert.Same(SettingsSchema.ImpactGroup, p.Group);
        Assert.Equal(SettingsPage.Advanced, p.Group.Page);
        Assert.Equal(ApplyTiming.Live, p.Timing);
        Assert.Equal(0, p.Min, 9);
        Assert.Equal(100, p.Max, 9);
        Assert.Equal(20, p.Default, 9); // DESIGN's initial value
        string text = DefaultsFile.ReadEmbeddedText();
        // step 95 moved the shipped shares on: 27 / 43 / 30 and 45 / 25 / 30 (Step95Tests)
        foreach (var line in new[] { "\"AiShieldmanKickPercent\": 27,", "\"AiShieldmanBashPercent\": 43,", "\"AiNoShieldKickPercent\": 45,", "\"AiNoShieldPushPercent\": 25," })
            Assert.Contains(line, text, StringComparison.Ordinal);
        var s = new TraxSettings();
        Assert.Equal(0.20, AiKickRules.From(s).NoShieldPush, 9);
        s.Set(p, 30, SettingSources.Mcm);
        Assert.Equal(0.30, AiKickRules.From(s).NoShieldPush, 9); // live
        var r = Shipped();
        Assert.Equal(0.25, r.ShieldKick, 9);
        Assert.Equal(0.40, r.ShieldBash, 9);
        Assert.Equal(0.35, r.SwingShare(true), 9);
        Assert.Equal(0.35, r.NoShieldKick, 9);
        Assert.Equal(0.20, r.NoShieldPush, 9);
        Assert.Equal(0.45, r.SwingShare(false), 9);
        Assert.Contains("a SHIELDMAN kick 25% / shield bash 40% / swing 35%, a man with NO shield kick 35% / weapon push 20% / swing 45%", r.Describe(), StringComparison.Ordinal);
        // only the push on = still enabled; every share 0 = off with all four named
        Assert.True(new AiKickRules(true, true, true, 1.6, 40, 2, true, 0, 0, 0, 0, 0, 0, 10).Enabled);
        Assert.Equal("AiShieldmanKickPercent, AiShieldmanBashPercent, AiNoShieldKickPercent and AiNoShieldPushPercent all 0",
            new AiKickRules(true, true, true, 1.6, 40, 2, true, 0, 0, 0).OffBecause);
        Assert.Equal("ModEnabled", new AiKickRules(false, true, true, 1.6, 40, 2, true, 25, 40, 35, 0, 0, 0, 20).OffBecause); // the master switch first
    }

    [Fact]
    public void A_no_shield_mans_kick_and_push_never_go_over_100_the_push_gives_way()
    {
        var over = new AiKickRules(true, true, true, 1.6, 40, 2, true, 25, 40, 70, 0, 0, 0, 50);
        Assert.True(over.NoShieldSharesCut);
        Assert.Equal(0.70, over.NoShieldKick, 9);
        Assert.Equal(0.30, over.NoShieldPush, 9);
        Assert.Equal(0, over.SwingShare(false), 9);
        Assert.Contains("kick 70% / weapon push 30% / swing 0% (kick + push set over 100% - the push cut to what is left)", over.Describe(), StringComparison.Ordinal);
        var exact = new AiKickRules(true, true, true, 1.6, 40, 2, true, 25, 40, 40, 0, 0, 0, 60);
        Assert.False(exact.NoShieldSharesCut);
        Assert.Equal(0.60, exact.NoShieldPush, 9);
        var wild = new AiKickRules(true, true, true, 1.6, 40, 2, true, 25, 40, 35, 0, 0, 0, -20);
        Assert.Equal(0, wild.NoShieldPush, 9);
        Assert.False(Shipped().NoShieldSharesCut);
    }

    [Fact]
    public void The_roll_splits_kick_then_bash_or_push_then_swing_and_seeded_rolls_come_out_near_25_40_35_and_35_20_45()
    {
        var r = Shipped();
        Assert.Equal(AiKickKind.Kick, AiKickMath.Roll(in r, true, 0.2499));
        Assert.Equal(AiKickKind.Bash, AiKickMath.Roll(in r, true, 0.25));
        Assert.Equal(AiKickKind.Bash, AiKickMath.Roll(in r, true, 0.6499));
        Assert.Equal(AiKickKind.None, AiKickMath.Roll(in r, true, 0.65));
        Assert.Equal(AiKickKind.Kick, AiKickMath.Roll(in r, false, 0.3499));
        Assert.Equal(AiKickKind.Push, AiKickMath.Roll(in r, false, 0.35));
        Assert.Equal(AiKickKind.Push, AiKickMath.Roll(in r, false, 0.5499));
        Assert.Equal(AiKickKind.None, AiKickMath.Roll(in r, false, 0.55));
        Assert.Equal(AiKickKind.None, AiKickMath.Roll(in r, false, double.NaN));
        var dice = new Seeded(88);
        const int n = 100000;
        int[] shield = new int[AiKickMath.KindCount], bare = new int[AiKickMath.KindCount];
        for (int i = 0; i < n; i++)
        {
            shield[(int)AiKickMath.Roll(in r, true, dice.NextDouble())]++;
            bare[(int)AiKickMath.Roll(in r, false, dice.NextDouble())]++;
        }
        Assert.InRange(shield[1] / (double)n, 0.245, 0.255);
        Assert.InRange(shield[2] / (double)n, 0.395, 0.405);
        Assert.InRange(shield[0] / (double)n, 0.345, 0.355);
        Assert.Equal(0, shield[3]); // a shieldman never pushes - he bashes
        Assert.InRange(bare[1] / (double)n, 0.345, 0.355);
        Assert.InRange(bare[3] / (double)n, 0.195, 0.205);
        Assert.InRange(bare[0] / (double)n, 0.445, 0.455);
        Assert.Equal(0, bare[2]); // no shield never bashes
        // push 0 = step 81 exactly: 35 / 65
        var old = new AiKickRules(true, true, true, 1.6, 40, 2, true, 25, 40, 35, 0, 0, 0, 0);
        var d2 = new Seeded(81);
        int pushes = 0, kicks = 0;
        for (int i = 0; i < n; i++)
        {
            var k = AiKickMath.Roll(in old, false, d2.NextDouble());
            if (k == AiKickKind.Push) pushes++;
            if (k == AiKickKind.Kick) kicks++;
        }
        Assert.Equal(0, pushes);
        Assert.InRange(kicks / (double)n, 0.345, 0.355);
    }

    [Fact]
    public void A_push_plays_as_a_bash_the_guard_up_first_then_the_kick_key_with_it_held_on_the_bashs_path()
    {
        Assert.True(AiKickMath.GuardUp(AiKickKind.Push));
        Assert.True(AiKickMath.GuardUp(AiKickKind.Bash));
        Assert.False(AiKickMath.GuardUp(AiKickKind.Kick));
        Assert.Equal(AiKickPhase.Guard, AiKickMath.FirstPhase(AiKickKind.Push));
        Assert.Equal(10 + AiKickMath.BashLeadSeconds, AiKickMath.PhaseEnd(AiKickPhase.Guard, 10, AiKickKind.Push), 9);
        uint ev = 0xFFFF;
        uint g = AiKickMath.Frame(AiKickPhase.Guard, AiKickKind.Push, AiInputMath.AttackMask, ref ev, out bool stop);
        Assert.Equal(AiKickMath.DefendUp, g);
        Assert.Equal(0u, ev);
        Assert.False(stop);
        uint p = AiKickMath.Frame(AiKickPhase.Press, AiKickKind.Push, 0, ref ev, out stop);
        Assert.Equal(AiKickMath.DefendUp, p); // the guard held through the press - the engine's context for a bash
        Assert.Equal(AiKickMath.KickEventBit, ev);
        uint k = AiKickMath.Frame(AiKickPhase.Press, AiKickKind.Kick, 0, ref ev, out stop);
        Assert.Equal(0u, k); // a kick's press has no guard
        var r = Shipped();
        Assert.Equal(r.BashPathHalfWidth, r.PathHalfWidth(AiKickKind.Push), 9);
        Assert.Equal(r.FriendClear, r.PathHalfWidth(AiKickKind.Kick), 9);
        // step 18's tracker sees a push as the engine's WeaponBash - a bash (charged once, CostPerKickOrBash)
        Assert.Equal(KickBashKind.Bash, AiKickMath.Shown(AiKickKind.Push));
        Assert.Equal(KickBashKind.Bash, KickBashTracker.KindOf(KickBashTracker.ActionWeaponBash));
        Assert.Equal("weapon push", AiKickMath.KindName(AiKickKind.Push));
        Assert.Equal("pushes", AiKickMath.PluralName(AiKickKind.Push));
    }

    // ------------------------------------------------------------------ the migration

    [Fact]
    public void Format_20_moves_the_shieldmans_old_shares_keeps_a_tuned_one_and_leaves_format_20_alone()
    {
        Assert.Equal(26, ConfigFile.FormatVersion);
        var read = ConfigFile.Read("{ \"ConfigVersion\": 19, \"AiShieldmanKickPercent\": 20, \"AiShieldmanBashPercent\": 35, \"AiNoShieldKickPercent\": 35 }");
        var notes = ConfigFile.Migrate(read, ShippedDefault);
        Assert.Equal(25, read.Values["AiShieldmanKickPercent"], 9);
        Assert.Equal(40, read.Values["AiShieldmanBashPercent"], 9);
        Assert.Equal(35, read.Values["AiNoShieldKickPercent"], 9);
        Assert.False(read.Values.ContainsKey("AiNoShieldPushPercent")); // a new key: the default fills it like any missing key
        Assert.Contains(notes, n => n.StartsWith("AiShieldmanKickPercent: 20 → 25 (format 19 → 26, the old default; step 88", StringComparison.Ordinal));
        Assert.Contains(notes, n => n.StartsWith("AiShieldmanBashPercent: 35 → 40 (format 19 → 26, the old default; step 88", StringComparison.Ordinal));
        var tuned = ConfigFile.Read("{ \"ConfigVersion\": 19, \"AiShieldmanKickPercent\": 10, \"AiShieldmanBashPercent\": 35 }");
        var tn = ConfigFile.Migrate(tuned, ShippedDefault);
        Assert.Equal(10, tuned.Values["AiShieldmanKickPercent"], 9);
        Assert.Equal(40, tuned.Values["AiShieldmanBashPercent"], 9);
        Assert.Contains(tn, n => n.StartsWith("AiShieldmanKickPercent: 10 kept - your own value (format 19 → 26; the new default 25", StringComparison.Ordinal));
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 20, \"AiShieldmanKickPercent\": 20, \"AiShieldmanBashPercent\": 35 }"), ShippedDefault));
        // a format-18 file has no shares yet (step 81's keys) - nothing to move here; on DESIGN's initial values the move is silent
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 19, \"AiShieldmanKickPercent\": 20, \"AiShieldmanBashPercent\": 35 }"), p => p.Default));
        // the rewritten file is format 21 (step 90) and carries the push share
        var s = new TraxSettings();
        string written = ConfigFile.Write(s.Snapshot());
        Assert.Contains("\"ConfigVersion\": 26", written, StringComparison.Ordinal);
        Assert.Contains("\"AiNoShieldPushPercent\": 20", written, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ the meet

    [Fact]
    public void A_pushs_correct_meet_is_a_guard_up_shield_or_weapon_and_every_other_guard_is_the_bashs_wrong_or_open_row()
    {
        Assert.Equal(ImpactMeet.PushShieldUp, ImpactMeetMath.Classify(true, false, MeetGuardKind.Shield, MeetDir.Up, push: true));
        Assert.Equal(ImpactMeet.PushWeaponUp, ImpactMeetMath.Classify(true, false, MeetGuardKind.Weapon, MeetDir.Up, push: true));
        Assert.Equal(ImpactMeet.PushShieldWrong, ImpactMeetMath.Classify(true, false, MeetGuardKind.Shield, MeetDir.Right, push: true)); // right is a BASH's meet
        Assert.Equal(ImpactMeet.PushShieldWrong, ImpactMeetMath.Classify(true, false, MeetGuardKind.Shield, MeetDir.Down, push: true));
        Assert.Equal(ImpactMeet.PushWeaponWrong, ImpactMeetMath.Classify(true, false, MeetGuardKind.Weapon, MeetDir.Right, push: true));
        Assert.Equal(ImpactMeet.PushWeaponWrong, ImpactMeetMath.Classify(true, false, MeetGuardKind.Weapon, MeetDir.Unknown, push: true));
        Assert.Equal(ImpactMeet.PushOpen, ImpactMeetMath.Classify(true, false, MeetGuardKind.None, MeetDir.Unknown, push: true));
        Assert.Equal(ImpactMeet.PushOpen, ImpactMeetMath.Classify(true, false, MeetGuardKind.Swinging, MeetDir.Unknown, push: true));
        // the engine stopped it with a raised shield: a shield meet whatever the read - up correct, else wrong
        Assert.Equal(ImpactMeet.PushShieldUp, ImpactMeetMath.Classify(true, true, MeetGuardKind.Unknown, MeetDir.Up, push: true));
        Assert.Equal(ImpactMeet.PushShieldWrong, ImpactMeetMath.Classify(true, true, MeetGuardKind.None, MeetDir.Unknown, push: true));
        // a shield bash is untouched: right correct, up wrong
        Assert.Equal(ImpactMeet.BashShieldRight, ImpactMeetMath.Classify(true, false, MeetGuardKind.Shield, MeetDir.Right, push: false));
        Assert.Equal(ImpactMeet.BashShieldWrong, ImpactMeetMath.Classify(true, false, MeetGuardKind.Shield, MeetDir.Up));
        Assert.Equal(ImpactMeet.KickShieldDown, ImpactMeetMath.Classify(false, false, MeetGuardKind.Shield, MeetDir.Down, push: true)); // push only with bash
        Assert.True(ImpactMeetMath.AttackerAtRisk(ImpactMeet.PushShieldUp));
        Assert.True(ImpactMeetMath.AttackerAtRisk(ImpactMeet.PushWeaponUp));
        Assert.False(ImpactMeetMath.AttackerAtRisk(ImpactMeet.PushShieldWrong));
        Assert.False(ImpactMeetMath.AttackerAtRisk(ImpactMeet.PushOpen));
        Assert.True(ImpactMeetMath.ByShield(ImpactMeet.PushShieldUp));
        Assert.True(ImpactMeetMath.ByShield(ImpactMeet.PushShieldWrong));
        Assert.False(ImpactMeetMath.ByShield(ImpactMeet.PushWeaponUp));
        Assert.True(ImpactMeetMath.IsPush(ImpactMeet.PushOpen));
        Assert.False(ImpactMeetMath.IsPush(ImpactMeet.BashOpen));
        // the AI's read raises the guard UP against a push (RIGHT against a bash, DOWN against a kick)
        Assert.Equal(ImpactMeetMath.FlagDefendUp, ImpactMeetMath.GuardFlag(true, true));
        Assert.Equal(ImpactMeetMath.FlagDefendRight, ImpactMeetMath.GuardFlag(true, false));
        Assert.Equal(ImpactMeetMath.FlagDefendDown, ImpactMeetMath.GuardFlag(false, true));
        Assert.Equal(14, ImpactMeetMath.MeetCount);
    }

    [Fact]
    public void The_push_rows_are_the_bash_ceilings_and_decide_like_the_bash_rows()
    {
        var m = ImpactMeetRules.From(new TraxSettings());
        (ImpactMeet push, ImpactMeet bash)[] pairs =
        {
            (ImpactMeet.PushShieldUp, ImpactMeet.BashShieldRight), (ImpactMeet.PushWeaponUp, ImpactMeet.BashWeaponRight),
            (ImpactMeet.PushShieldWrong, ImpactMeet.BashShieldWrong), (ImpactMeet.PushWeaponWrong, ImpactMeet.BashWeaponWrong), (ImpactMeet.PushOpen, ImpactMeet.BashOpen),
        };
        foreach (var (push, bash) in pairs)
        {
            ImpactMeetMath.Row(in m, push, out double ps, out double pf, out double pd);
            ImpactMeetMath.Row(in m, bash, out double bs, out double bf, out double bd);
            Assert.Equal(bs, ps, 9);
            Assert.Equal(bf, pf, 9);
            Assert.Equal(bd, pd, 9);
            Assert.True(ImpactMeetMath.HasRow(push));
            Assert.Equal(bash, ImpactMeetMath.CeilingRow(push));
        }
        // the numbers Toni named: up-shield the pusher 70 / 30, up-weapon 45 / 15, shield wrong 85 / 40 / 35, weapon wrong 95 / 50, open 98 / 60 / 45
        ImpactMeetMath.Row(in m, ImpactMeet.PushShieldUp, out double s1, out double f1, out _);
        Assert.Equal(0.70, s1, 9);
        Assert.Equal(0.30, f1, 9);
        ImpactMeetMath.Row(in m, ImpactMeet.PushWeaponUp, out double s2, out double f2, out _);
        Assert.Equal(0.45, s2, 9);
        Assert.Equal(0.15, f2, 9);
        ImpactMeetMath.Row(in m, ImpactMeet.PushShieldWrong, out double s3, out double f3, out double d3);
        Assert.Equal((0.85, 0.40, 0.35), (s3, f3, d3));
        ImpactMeetMath.Row(in m, ImpactMeet.PushWeaponWrong, out double s4, out double f4, out double d4);
        Assert.Equal((0.95, 0.50, 0.0), (s4, f4, d4));
        ImpactMeetMath.Row(in m, ImpactMeet.PushOpen, out double s5, out double f5, out double d5);
        Assert.Equal((0.98, 0.60, 0.45), (s5, f5, d5));

        // a decision: a spent pusher (an 8 kg two-handed axe) into a fresh shield held UP = the PUSHER at risk, exactly as a spent basher into a shield guarding right
        var r = ImpactRules.From(new TraxSettings());
        var pf2 = Facts(ImpactMeet.PushShieldUp, push: true);
        var bf2 = Facts(ImpactMeet.BashShieldRight, push: false);
        var dp = Decide(in r, in pf2);
        var db = Decide(in r, in bf2);
        Assert.True(dp.MeetAttacker);
        Assert.Equal(ImpactMeet.PushShieldUp, dp.Meet);
        Assert.Equal(db.Riposte.Chance, dp.Riposte.Chance, 9);
        Assert.Equal(db.ThrownBack.Chance, dp.ThrownBack.Chance, 9);
        Assert.True(dp.Riposte.Chance > 0.5); // a spent man pushing a fresh guard is thrown off balance
        Assert.False(dp.Knockdown.Happens || dp.Stagger.Happens || dp.ShieldDrop.Happens); // the defender takes nothing
        // the same push into a guard held RIGHT = the defender's row (the blocker at risk), like a bash into a wrong guard
        var wrong = Facts(ImpactMeet.PushShieldWrong, push: true, blocked: true, attackerBar: 1.0, bar: 0.1);
        var dw = Decide(in r, in wrong);
        Assert.False(dw.MeetAttacker);
        Assert.True(dw.MeetBlocked);
        Assert.True(dw.Stagger.Chance > 0.5);
        // the push's impact comes from the weapon's weight (the module fills WeaponWeight from the weapon): a heavier weapon presses harder
        var light = Facts(ImpactMeet.PushOpen, push: true, weapon: 1.0, bar: 0.5);
        var heavy = Facts(ImpactMeet.PushOpen, push: true, weapon: 8.0, bar: 0.5);
        Assert.True(ImpactMath.BlowImpact(in r, in heavy) > ImpactMath.BlowImpact(in r, in light));
        Assert.True(Decide(in r, in heavy).X > Decide(in r, in light).X);
    }

    private static ImpactFacts Facts(ImpactMeet meet, bool push, double bar = 1.0, double attackerBar = 0.0, bool blocked = false, double weapon = 8.0)
    {
        double fA = Math.Min(1, attackerBar / 0.75);
        return new ImpactFacts
        {
            Event = ImpactEvent.Bash,
            Push = push,
            Meet = meet,
            AltBlocked = blocked,
            AttackerPoints = 100 * attackerBar,
            AttackerF = fA,
            AttackerAnimation = 0.85 + 0.15 * fA,
            AttackerSkill = 100,
            DefenderPoints = 100 * bar,
            DefenderBar = bar,
            DefenderF = Math.Min(1, bar / 0.75),
            DefenderSkill = 100,
            WeaponWeight = weapon,
            DamageType = ImpactDamageType.Blunt,
            GuardWeight = ImpactMeetMath.ByShield(meet) ? 4.0 : 1.2,
            MeetGuardSkill = 100,
            DefenderHasShield = true,
            DefenderMaxHealth = 100,
        };
    }

    private static ImpactDecision Decide(in ImpactRules r, in ImpactFacts f)
    {
        var d = default(ImpactState);
        var a = default(ImpactState);
        return ImpactMath.Decide(in r, in f, ref d, ref a, 100, new Dice());
    }

    // ------------------------------------------------------------------ the summary

    [Fact]
    public void The_summary_counts_pushes_apart_in_the_shares_the_hits_the_path_and_the_meets()
    {
        var r = Shipped();
        var st = new AiKickStats();
        // 20 no-shield moves: 7 kicks, 4 pushes (1 refused by the path), 9 swings
        for (int i = 0; i < 7; i++) { st.NoteMove(1, AiKickKind.Kick); st.Asked(AiKickKind.Kick, 1); }
        for (int i = 0; i < 4; i++) st.NoteMove(1, AiKickKind.Push);
        for (int i = 0; i < 3; i++) st.Asked(AiKickKind.Push, 1);
        for (int i = 0; i < 9; i++) st.NoteMove(1, AiKickKind.None);
        st.Skips[(int)AiKickSkip.FriendInTheWay]++;
        st.NoteRefused(1, AiKickSkip.FriendInTheWay);
        st.NotePath(AiKickKind.Push, AiKickPath.FriendFirst);
        st.Played(AiKickKind.Push, 1, 0.5);
        st.Landed(AiKickKind.Push, 1, false);
        st.NoteOutcome(AiKickKind.Push, AiKickOutcome.EnemyTarget);
        st.NoteOutcome(AiKickKind.Push, AiKickOutcome.FriendBody);
        st.Blocks.NoteFriendHit(AiKickKind.Push, AiKickBlocker.None, 15);
        st.ContactManSeconds = 60;
        var lines = st.SummaryLines(in r).ToList();
        Assert.Equal(9, lines.Count); // step 95: + the step-in bash (the 9th); step 91: + the shield bash's reach (the 8th)
        Assert.Contains("ai kicks: asked 10 (kicks 7, bashes 0, pushes 3) → PLAYED 1", lines[1], StringComparison.Ordinal);
        Assert.Contains("no shield 20 moves - rolled kick 35.0% / push 20.0% / swing 45.0% (target 35% / 20% / 45%) → DONE kick 35.0% / push 15.0% / swing 50.0%; refused safety 0, stack empty 0, cooldown 0, path 1",
            lines[4], StringComparison.Ordinal);
        Assert.Contains("kicks 7, bashes 0 and pushes 3 asked = 10.00 a man-minute in contact", lines[4], StringComparison.Ordinal);
        Assert.Contains("| pushes: on an ENEMY 1 (his target 1, another 0, into his shield 0), on a FRIEND 1 (the man 1, his shield 0), a horse / untracked 0, MISSED 0 - friends 50.0% of the hits", lines[5], StringComparison.Ordinal);
        Assert.Contains("; pushes clear 0, the enemy off his strike path 0, a friend in front, beyond the rear-rank reach 1, a friend in front of him 0 - REFUSED 1", lines[5], StringComparison.Ordinal);
        Assert.Contains("LANDED ON A FRIEND (his place at the hit, the angle at the decision) - kicks 0; bashes 0; pushes 1:", lines[6], StringComparison.Ordinal);

        var meets = new ImpactMeetStats();
        meets.NoteDecision(ImpactMeet.PushShieldUp, true, false, false, false, false, true, false, true);
        meets.NoteDecision(ImpactMeet.PushOpen, false, false, true, false, false, false, true, false);
        var ml = meets.SummaryLines(ImpactMeetRules.From(new TraxSettings()));
        Assert.StartsWith("impact meets (step 82 / 88) - each kick / bash / push by how it was met (ON): bash - shield, guard right 0", ml[0], StringComparison.Ordinal);
        Assert.Contains(" | kick - any other guard 0 | PUSHES (step 88 - the bash's ceilings, a guard UP the correct meet): push - shield, guard up 1 → the ATTACKER staggered 1 (100%), down 0 (0%); the defender held 1 (100%) (an AI read it: 1)",
            ml[0], StringComparison.Ordinal);
        Assert.Contains(" | push - no guard or mid-swing 1 → the defender staggered 1 (100%), down 0 (0%), shield dropped 0 (0%), held 0 (0%) (you in 1)", ml[0], StringComparison.Ordinal);
        Assert.Contains("a weapon PUSH (step 88) as a bash, but met correctly by a guard UP", ImpactMeetRules.From(new TraxSettings()).Describe(), StringComparison.Ordinal);
    }
}

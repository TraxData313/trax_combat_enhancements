using System;
using System.Collections.Generic;
using System.Linq;
using TraxCombat.Core;
using Xunit.Abstractions;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 104a - TIMED BLOCKS ON HORSEBACK, THE LADDER AND THE SHADOW (Toni 2026-11-09: "can you add the reel vs mounted troops as well somehow";
/// AI_NOTES "Step 104" §2, DESIGN decision 93): the design table at skill 100 (the reel's chance and length, the rear, the knock-off by speed and
/// match), held = refused as today, the AI's half strength never rears / unhorses you, the 1.0 s cap on you, the refusals, the shadow decision's
/// dice, a real rider decision (still refused live), the settings, the [summary] line, the J tool's rider candidates.
/// </summary>
public class Step104aTests
{
    private readonly ITestOutputHelper _out;

    public Step104aTests(ITestOutputHelper output) => _out = output;

    private sealed class Script : IRandomSource
    {
        private readonly Queue<double> _q;
        public int Calls;
        public Script(params double[] v) => _q = new Queue<double>(v);
        public double NextDouble()
        {
            Calls++;
            return _q.Count > 0 ? _q.Dequeue() : 0.999;
        }
    }

    private static TraxSettings Settings(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return s;
    }

    private static TimedRiderRules Rules(Action<TraxSettings>? tune = null) => TimedRiderRules.From(Settings(tune));

    // skill 100: perfect 0.15 s, no timing after 0.5 s (TimedBlockWindowSeconds x perfect / TimedBlockPerfectSeconds)
    private static double ScoreAt(double age) => TimedBlockMath.Score(age, 0.15, 0.5);

    private static double Chance(double s) => TimedBlockMath.TimedChance(0.01, 0.85, s, 0.15, 1.0); // step 56's ~1 %, step 101's blend + 15, cap 100

    private static TimedRiderFacts Facts(double s, bool youBlock = true, bool youRide = false, bool blockerMounted = true, double chance = double.NaN,
        double speed = 6.5, double xr = 0, ImpactEvent e = ImpactEvent.RightBlock) => new TimedRiderFacts
    {
        Event = e,
        BlockerMounted = blockerMounted,
        AttackerMounted = true,
        BlockerIsPlayer = youBlock,
        AttackerIsPlayer = youRide,
        Score = s,
        RowInRow = true,
        RowChance = double.IsNaN(chance) ? Chance(s) : chance,
        RowRefusal = ImpactRefusal.Rider,
        Length56 = 0.6,
        StunCap = 2.0,
        XRiposte = xr,
        HorseSpeed = speed,
        TiredMountMultiplier = 1,
    };

    // ------------------------------------------------------------------ A. the design table (skill 100, fresh even men, a canter)

    [Theory]
    [InlineData(0.10, 1.00, 1.00, 1.50, 0.50)]
    [InlineData(0.20, 0.94, 0.95, 1.45, 0.47)]
    [InlineData(0.30, 0.61, 0.67, 1.15, 0.00)]
    [InlineData(0.40, 0.20, 0.33, 0.78, 0.00)]
    public void The_design_table_at_skill_100(double age, double s, double reel, double seconds, double rear)
    {
        var r = Rules();
        double score = ScoreAt(age);
        Assert.Equal(s, score, 2);
        Assert.Equal(reel, Chance(score), 2);
        Assert.Equal(seconds, TimedRiderMath.ReelLength(in r, 0.6, score, 2.0, false, out bool capped), 2);
        Assert.False(capped);
        Assert.Equal(rear, TimedRiderMath.RearChance(score, r.RearMinScore, r.RearShare), 2);
        // the knock-off only at a perfect block
        double band = TimedRiderMath.SpeedBand(6.5, r.RiderWalk, r.RiderGallop);
        double off = TimedRiderMath.DismountChance(in r, score, true, band, TimedRiderMath.MatchFactor(0));
        if (age <= 0.15) Assert.Equal(0.094, off, 3); // ~9 % at a canter (30 x 0.625 x 0.5)
        else Assert.Equal(0, off);
        _out.WriteLine("guard " + age + " s: s " + score.ToString("0.00") + ", reel " + (100 * Chance(score)).ToString("0") + "%, " + TimedRiderMath.ReelLength(in r, 0.6, score, 2.0, false, out _).ToString("0.00")
            + " s, rear " + (100 * TimedRiderMath.RearChance(score, r.RearMinScore, r.RearShare)).ToString("0") + "%, off " + (100 * off).ToString("0.0") + "%");
    }

    [Fact]
    public void A_galloping_weak_rider_is_knocked_off_30_percent_a_parry_half_standing_a_quarter()
    {
        var r = Rules();
        Assert.Equal(0.625, TimedRiderMath.SpeedBand(6.5, r.RiderWalk, r.RiderGallop), 9);
        Assert.Equal(1, TimedRiderMath.SpeedBand(12, r.RiderWalk, r.RiderGallop), 9);
        Assert.Equal(0.25, TimedRiderMath.SpeedBand(0, r.RiderWalk, r.RiderGallop), 9);
        Assert.Equal(0.5, TimedRiderMath.MatchFactor(0), 9);
        Assert.Equal(1, TimedRiderMath.MatchFactor(0.6), 9);
        Assert.Equal(0.30, TimedRiderMath.DismountChance(in r, 1, true, 1, TimedRiderMath.MatchFactor(1)), 9);
        Assert.Equal(0.15, TimedRiderMath.DismountChance(in r, 1, false, 1, 1), 9);
        Assert.Equal(0.30 * 0.25 * 0.5, TimedRiderMath.DismountChance(in r, 1, true, 0.25, 0.5), 9);
        Assert.Equal(0, TimedRiderMath.DismountChance(in r, 0.95, true, 1, 1)); // not perfect
    }

    [Fact]
    public void Held_is_refused_as_today_with_no_die()
    {
        var r = Rules();
        var dice = new Script();
        var o = TimedRiderMath.Decide(in r, Facts(0), double.NaN, dice);
        Assert.True(o.Asked);
        Assert.Equal(TimedRiderRefusal.NotTimed, o.Refusal);
        Assert.False(o.Reel);
        Assert.Equal(0, dice.Calls);
        Assert.Equal(0, ScoreAt(0.6));
    }

    [Fact]
    public void The_AI_at_half_strength_never_rears_or_unhorses_and_never_you_whatever_the_share()
    {
        var r = Rules();
        // an AI blocker: its timing x 0.5 - at most 0.5 - no rear (0.8 floor), no knock-off (perfect only)
        double sAi = 1.0 * TimedBlockRules.From(Settings()).AiShare;
        Assert.Equal(0.5, sAi, 9);
        var o = TimedRiderMath.Decide(in r, Facts(sAi, youBlock: false, youRide: false), double.NaN, new Script(0.0, 0.0, 0.0));
        Assert.True(o.Reel);
        Assert.False(o.Rear);
        Assert.False(o.Dismount);
        Assert.Equal(0, o.RearChance);
        Assert.Equal(0, o.DismountChance);
        // on YOU: even a perfect AI block (the share at 100) never rears your horse or unhorses you, and the reel stops at 1.0 s
        var you = TimedRiderMath.Decide(in r, Facts(1, youBlock: false, youRide: true), double.NaN, new Script(0.0, 0.0, 0.0));
        Assert.True(you.Reel);
        Assert.False(you.Rear);
        Assert.False(you.Dismount);
        Assert.True(double.IsNaN(you.RearChance) && double.IsNaN(you.DismountChance));
        Assert.Equal(1.0, you.ReelSeconds, 9);
        Assert.True(you.CappedForYou);
        Assert.True(double.IsNaN(you.StrikeWindow)); // the AI has no perfect strike
    }

    [Fact]
    public void Your_perfect_block_rolls_the_reel_then_the_knock_off_then_the_rear_one_die_each()
    {
        var r = Rules();
        // the reel taken (0.5 < 1), the knock-off missed (0.5 > 0.094), the rear taken (0.2 < 0.5)
        var dice = new Script(0.5, 0.5, 0.2);
        var o = TimedRiderMath.Decide(in r, Facts(1), 0.9, dice);
        Assert.Equal(3, dice.Calls);
        Assert.True(o.Reel && !o.Dismount && o.Rear);
        Assert.Equal(1.5, o.ReelSeconds, 9);
        Assert.Equal(0.9, o.AnimSeconds, 9); // the clip's own length at x1.0 (below the 1.0 s cap)
        Assert.Equal(0.6, o.LockSeconds, 9);
        Assert.Equal(0.5, o.HorseCheck, 9);
        Assert.Equal(1.5 + 0.15, o.StrikeWindow, 9); // + PerfectStrikeGraceSeconds
        Assert.True(o.SlowMo);
        // knocked off: the rear is not rolled
        var dice2 = new Script(0.5, 0.01);
        var o2 = TimedRiderMath.Decide(in r, Facts(1), double.NaN, dice2);
        Assert.True(o2.Dismount && !o2.Rear);
        Assert.Equal(2, dice2.Calls);
        Assert.Equal(1.0, o2.AnimSeconds, 9); // TimedRiderReelAnimMaxSeconds
        // the reel missed: nothing after it
        var dice3 = new Script(0.999);
        var o3 = TimedRiderMath.Decide(in r, Facts(0.2), double.NaN, dice3);
        Assert.False(o3.Reel);
        Assert.Equal(1, dice3.Calls);
        _out.WriteLine(TimedRiderMath.Describe(in o, "Rider", "you"));
        _out.WriteLine(TimedRiderMath.Describe(in o2, "Rider", "you"));
        _out.WriteLine(TimedRiderMath.Describe(in o3, "Rider", "you"));
    }

    [Fact]
    public void The_reel_rate_follows_the_chance_over_many_seeded_rolls()
    {
        var r = Rules();
        var dice = new SeededRandom(104);
        int reels = 0, n = 20000;
        double s = ScoreAt(0.30);
        for (int i = 0; i < n; i++)
            if (TimedRiderMath.Decide(in r, Facts(s), double.NaN, dice).Reel) reels++;
        Assert.InRange(reels / (double)n, Chance(s) - 0.015, Chance(s) + 0.015);
    }

    [Fact]
    public void The_monotone_ladder()
    {
        var r = Rules();
        double prevC = -1, prevL = -1, prevR = -1;
        for (double age = 0.6; age >= 0; age -= 0.01)
        {
            double s = ScoreAt(age);
            double c = Chance(s), l = TimedRiderMath.ReelLength(in r, 0.6, s, 2.0, false, out _), rr = TimedRiderMath.RearChance(s, r.RearMinScore, r.RearShare);
            Assert.True(c >= prevC - 1e-12 && l >= prevL - 1e-12 && rr >= prevR - 1e-12);
            prevC = c;
            prevL = l;
            prevR = rr;
        }
    }

    // ------------------------------------------------------------------ B. the refusals

    [Fact]
    public void The_refusals_in_order()
    {
        var r = Rules();
        TimedRiderRefusal Why(TimedRiderFacts f, TimedRiderRules? rules = null) => TimedRiderMath.Decide(rules ?? r, f, double.NaN, new Script()).Refusal;
        var f = Facts(1);
        f.AttackerPassive = true;
        Assert.Equal(TimedRiderRefusal.Lance, Why(f));
        Assert.Equal(TimedRiderRefusal.FromFootOff, Why(Facts(1, blockerMounted: false), Rules(s => s.Set(SettingsSchema.TimedRiderFromFoot, false, "test"))));
        Assert.Equal(TimedRiderRefusal.None, Why(Facts(1, blockerMounted: false)));
        Assert.Equal(TimedRiderRefusal.OnYouOff, Why(Facts(0.5, youBlock: false, youRide: true), Rules(s => s.Set(SettingsSchema.TimedRiderReelOnYou, false, "test"))));
        f = Facts(1);
        f.RowRefusal = ImpactRefusal.Down;
        Assert.Equal(TimedRiderRefusal.Down, Why(f));
        f.RowRefusal = ImpactRefusal.Replaced;
        Assert.Equal(TimedRiderRefusal.Replaced, Why(f));
        f = Facts(1);
        f.AttackerReeling = true;
        Assert.Equal(TimedRiderRefusal.Reeling, Why(f));
        f = Facts(0.9);
        f.AttackerImmune = true;
        Assert.Equal(TimedRiderRefusal.Immune, Why(f));
        f.Score = 1; // a perfect block skips his immunity (step 101)
        Assert.Equal(TimedRiderRefusal.None, Why(f));
        f = Facts(1);
        f.RowInRow = false;
        Assert.Equal(TimedRiderRefusal.NoRow, Why(f));
        Assert.Equal(TimedRiderRefusal.Off, Why(Facts(1), Rules(s => s.Set(SettingsSchema.ModEnabled, false, "test"))));
        Assert.Equal(TimedRiderRefusal.Off, Why(Facts(1), Rules(s => s.Set(SettingsSchema.TimedBlocksEnabled, false, "test"))));
        // the switch OFF (DESIGN's initial value - the tests' default; shipped ON since step 104b) does not stop the shadow; ON = live
        Assert.False(r.Switch);
        Assert.False(r.Live);
        Assert.True(r.ShadowOn);
        var on = Rules(s => s.Set(SettingsSchema.TimedBlocksOnHorseback, true, "test"));
        Assert.Equal(TimedRiderRefusal.None, Why(Facts(1), on));
        Assert.True(on.Switch && on.Live && on.ShadowOn);
    }

    [Fact]
    public void A_rider_blocking_a_man_on_foot_only_notes_104d_with_no_die()
    {
        var r = Rules();
        var f = Facts(1, blockerMounted: true);
        f.AttackerMounted = false;
        f.FootRiposteHappened = true;
        var dice = new Script();
        var o = TimedRiderMath.Decide(in r, f, double.NaN, dice);
        Assert.Equal(TimedRiderCombo.RiderVsFoot, o.Combo);
        Assert.True(o.PushOutMounted && o.StrikeFromHorseback && !o.Reel);
        Assert.Equal(0, dice.Calls);
        f.AttackerMounted = false;
        f.BlockerMounted = false;
        Assert.False(TimedRiderMath.Decide(in r, f, double.NaN, dice).Asked); // nobody rides: not the shadow's
    }

    [Fact]
    public void The_animation_part_and_the_horse_check()
    {
        var r = Rules();
        Assert.Equal(1.0, TimedRiderMath.AnimSeconds(in r, 1.5, double.NaN), 9);
        Assert.Equal(0.6, TimedRiderMath.AnimSeconds(in r, 0.6, double.NaN), 9);
        Assert.Equal(0.8, TimedRiderMath.AnimSeconds(in r, 1.5, 0.8), 9);
        var none = Rules(s => { Assert.True(TextValues.TryIndex("none", out int i)); s.Set(SettingsSchema.TimedRiderReelAction, i, "test"); });
        Assert.Equal(0, TimedRiderMath.AnimSeconds(in none, 1.5, 1.0));
        Assert.Equal(0.5, TimedRiderMath.HorseCheck(1, 0.5), 9);
        Assert.Equal(0.4, TimedRiderMath.HorseCheck(0.4, 0.5), 9);
        Assert.Equal(0.5, TimedRiderMath.HorseCheck(double.NaN, 0.5), 9);
    }

    // ------------------------------------------------------------------ C. a real decision: still refused live (s 0 = today exactly)

    [Fact]
    public void A_real_rider_decision_is_still_refused_live_and_the_shadow_reads_it()
    {
        var s = Settings(t => t.Set(SettingsSchema.ImpactOnPlayerPercent, 100, "test"));
        var ir = ImpactRules.From(s);
        var f = new ImpactFacts
        {
            Event = ImpactEvent.RightBlock,
            AttackerPoints = 100, AttackerF = 1, AttackerAnimation = 1, AttackerSkill = 100,
            DefenderPoints = 100, DefenderBar = 1, DefenderF = 1, DefenderSkill = 100,
            WeaponWeight = 1.2, SwingSpeed = 92, DamageType = ImpactDamageType.Cut, GuardWeight = 4, DefenderHasShield = true, DefenderMaxHealth = 100,
            DefenderIsPlayer = true, DefenderMounted = true, AttackerMounted = true, AttackerSpeed = 6.5,
            TimingSource = TimingSource.PlayerClock, GuardAge = 0.05, HasGuardSkill = true, GuardSkill = 100,
        };
        var vs = default(ImpactState);
        var ast = default(ImpactState);
        var d = ImpactMath.Decide(in ir, in f, ref vs, ref ast, 100, new Script());
        Assert.Equal(1, d.TimedScore, 9);
        Assert.True(d.Riposte.InRow);
        Assert.Equal(ImpactRefusal.Rider, d.Riposte.Refusal); // live: refused exactly as before step 104
        Assert.False(d.Riposte.Happens);
        Assert.False(ast.Reeling(100.1));
        Assert.Equal(1, d.Riposte.Chance, 9); // a perfect block's timed chance
        var tf = new TimedRiderFacts
        {
            Event = f.Event, BlockerMounted = true, AttackerMounted = true, BlockerIsPlayer = true, Score = d.TimedScore,
            RowInRow = true, RowChance = d.Riposte.Chance, RowRefusal = d.Riposte.Refusal, Length56 = ImpactMath.RiposteStun56(in ir, in d, 0.5),
            StunCap = ir.StunCap, XRiposte = d.XRiposte, HorseSpeed = f.AttackerSpeed, TiredMountMultiplier = 1,
        };
        var o = TimedRiderMath.Decide(Rules(), tf, double.NaN, new Script(0.5, 0.9, 0.9));
        Assert.True(o.Reel);
        Assert.Equal(1.5, o.ReelSeconds, 9);
        _out.WriteLine("L56 " + tf.Length56.ToString("0.00") + " s, x_r " + d.XRiposte.ToString("0.00") + ": " + TimedRiderMath.Describe(in o, "Rider", "you"));
    }

    [Fact]
    public void The_shared_riposte_length_is_step_56s()
    {
        var ir = ImpactRules.From(Settings());
        var d = new ImpactDecision { TAttacker = 0, XRiposte = 0 };
        Assert.Equal(Math.Min(ir.StunCap, ir.RiposteStunMax * 0.5), ImpactMath.RiposteStun56(in ir, in d, 0.5), 9);
        d.XRiposte = 1;
        Assert.Equal(Math.Min(ir.StunCap, ir.RiposteStunMax), ImpactMath.RiposteStun56(in ir, in d, 0.5), 9);
    }

    // ------------------------------------------------------------------ D. the settings, the summary, the J tool's candidates

    [Fact]
    public void The_settings_are_where_the_design_says_and_the_live_switch_starts_off_in_design()
    {
        var all = SettingsSchema.All.ToList();
        Assert.Equal(all.IndexOf(SettingsSchema.ReelingHitKnockdownEnabled) + 1, all.IndexOf(SettingsSchema.TimedBlocksOnHorseback));
        Assert.Contains(SettingsSchema.TimedBlocksOnHorseback, SettingsSchema.PlayerPage);
        Assert.DoesNotMatch(@"[Ss]tep \d", SettingsSchema.TimedBlocksOnHorseback.Description);
        Assert.Equal(0, SettingsSchema.TimedBlocksOnHorseback.Default, 5); // DESIGN's INITIAL value (104a: off) - shipped ON since 104b in defaults.json (Step104bTests)
        var adv = new[]
        {
            SettingsSchema.TimedRiderReelMaxSeconds, SettingsSchema.TimedRiderReelAgainstYouMaxSeconds, SettingsSchema.TimedRiderReelOnYou, SettingsSchema.TimedRiderReelAction,
            SettingsSchema.TimedRiderReelAnimSpeed, SettingsSchema.TimedRiderReelAnimMaxSeconds, SettingsSchema.TimedRiderNoGuard, SettingsSchema.TimedRiderHorseCheckPercent,
            SettingsSchema.TimedRiderFromFoot, SettingsSchema.TimedRiderRearMinScore, SettingsSchema.TimedRiderRearPercent, SettingsSchema.TimedRiderDismountPercent,
            SettingsSchema.TimedRiderDismountWeaponPercent, SettingsSchema.PerfectStrikeOnHorseback, SettingsSchema.PerfectStrikeHorsebackAnySide, SettingsSchema.TimedBlockPushOutMounted,
        };
        int at = all.IndexOf(SettingsSchema.ReelingHitKnockdownPercent);
        for (int i = 0; i < adv.Length; i++)
        {
            Assert.Equal(at + 1 + i, all.IndexOf(adv[i]));
            Assert.Same(SettingsSchema.ImpactGroup, adv[i].Group);
        }
        var r = Rules();
        Assert.Equal(1.5, r.ReelMax, 9);
        Assert.Equal(1.0, r.ReelYouMax, 9);
        Assert.Equal("act_stagger_backward", r.ReelAction);
        Assert.Equal(1.0, r.AnimSpeed, 9);
        Assert.Equal(1.0, r.AnimMax, 9);
        Assert.Equal(0.5, r.HorseCheck, 9);
        Assert.Equal(0.8, r.RearMinScore, 9);
        Assert.Equal(0.5, r.RearShare, 9);
        Assert.Equal(0.3, r.DismountShare, 9);
        Assert.Equal(0.5, r.DismountWeaponShare, 9);
        Assert.True(r.ReelOnYou && r.NoGuard && r.FromFoot && r.StrikeOnHorseback && r.StrikeAnySide && r.PushOutMounted);
        Assert.Contains("SHADOW only", r.Describe());
        Assert.Contains("the reel in the saddle LIVE", Rules(s => s.Set(SettingsSchema.TimedBlocksOnHorseback, true, "test")).Describe());
    }

    [Fact]
    public void The_summary_line()
    {
        var r = Rules(s => s.Set(SettingsSchema.TimedBlocksOnHorseback, false, "test")); // the shadow's own words with the switch off (as in 104a)
        var st = new TimedRiderStats();
        st.Note(TimedRiderMath.Decide(in r, Facts(1), double.NaN, new Script(0.5, 0.5, 0.2)), true, false);
        st.Note(TimedRiderMath.Decide(in r, Facts(0), double.NaN, new Script()), true, false);
        st.Note(TimedRiderMath.Decide(in r, Facts(0.5, youBlock: false, blockerMounted: false), double.NaN, new Script(0.1)), false, false);
        var foot = Facts(1);
        foot.AttackerMounted = false;
        foot.FootRiposteHappened = true;
        st.Note(TimedRiderMath.Decide(in r, foot, double.NaN, new Script()), true, false);
        string line = st.SummaryLine(in r);
        _out.WriteLine(line);
        Assert.StartsWith("timed blocks on horseback (shadow, step 104a): the switch OFF - NOTHING APPLIED; correct blocks of a rider's blow 3 (rider vs rider 2 / foot vs rider 1", line);
        Assert.Contains("WOULD reel 2 of 2 rolled", line);
        Assert.Contains("rears 1 of 1 sharp", line);
        Assert.Contains("untimed (today's refusal) 1", line);
        Assert.Contains("a rider blocking a man on foot 1 (timed 1", line);
    }

    [Fact]
    public void The_J_tools_rider_candidates_and_the_mounted_recoils()
    {
        var c = TimedRiderAnimMath.Candidates;
        Assert.Equal(16, c.Length);
        Assert.Equal(c.Length, c.Select(x => x.Label).Distinct().Count());
        Assert.Equal(RiderAnimKind.Configured, c[0].Kind);
        Assert.Equal(RiderAnimKind.Rear, c[c.Length - 1].Kind);
        Assert.Equal(8, c.Count(x => x.Kind == RiderAnimKind.Recoil));
        Assert.Contains(c, x => x.Kind == RiderAnimKind.Action && x.Action == "act_stagger_backward" && x.Speed == 1f);
        Assert.Contains(c, x => x.Kind == RiderAnimKind.Action && x.Action == "act_stagger_backward" && x.Speed == 0.5f);
        Assert.Contains(c, x => x.Kind == RiderAnimKind.Action && x.Action == "act_strike2_chest_front");
        Assert.Contains(c, x => x.Kind == RiderAnimKind.PushParryHeavy && x.Action == TimedPushOutMath.ParryHeavy);
        Assert.Contains(c, x => x.Kind == RiderAnimKind.PushAction && x.Action == "act_shield_bash");
        Assert.Equal(1, TimedRiderAnimMath.RiderChannel); // never channel 0 on a rider
        // the mounted recoils as item_usage_sets.xml wires them (v1.4.8)
        Assert.Equal(new[] { "act_blocked_overswing_horseback", "act_quick_blocked_overswing_horseback" },
            TimedRiderAnimMath.MountedRecoilNames(TimedRecoilMath.ClassOf("onehanded_shield_swing", true), TimedRecoilMath.Up, "onehanded_shield_swing"));
        Assert.Equal("act_blocked_slash_horseback_left", TimedRiderAnimMath.MountedRecoilNames(RecoilClass.OneHanded, TimedRecoilMath.Left, "onehanded_swing")[0]);
        Assert.Equal("act_blocked_thrust_1h_horseback", TimedRiderAnimMath.MountedRecoilNames(RecoilClass.OneHanded, TimedRecoilMath.Down, "onehanded_swing_thrust")[0]);
        Assert.Equal("act_blocked_slash_2h_horseback_right", TimedRiderAnimMath.MountedRecoilNames(RecoilClass.TwoHanded, TimedRecoilMath.Right, "twohanded_block_swing")[0]);
        Assert.Equal("act_blocked_slashleft_staff_horseback", TimedRiderAnimMath.MountedRecoilNames(TimedRecoilMath.ClassOf("twohanded_widegrip_axe", false), TimedRecoilMath.Left, "twohanded_widegrip_axe")[0]);
        Assert.Equal("act_blocked_overswing_spear_horseback", TimedRiderAnimMath.MountedRecoilNames(TimedRecoilMath.ClassOf("polearm_block_thrust", false), TimedRecoilMath.Up, "polearm_block_thrust")[0]);
        Assert.Equal("act_blocked_thrust_staff_horseback", TimedRiderAnimMath.MountedRecoilNames(TimedRecoilMath.ClassOf("polearm_block_swing_thrust", false), TimedRecoilMath.Down, "polearm_block_swing_thrust")[0]);
        Assert.Equal("act_blocked_thrust_1h_lance", TimedRiderAnimMath.MountedRecoilNames(TimedRecoilMath.ClassOf("polearm_block_shield_thrust", true), TimedRecoilMath.Down, "polearm_block_shield_thrust")[0]);
        Assert.Empty(TimedRiderAnimMath.MountedRecoilNames(TimedRecoilMath.ClassOf("polearm_couch", true), TimedRecoilMath.Down, "polearm_couch"));
        Assert.Empty(TimedRiderAnimMath.MountedRecoilNames(RecoilClass.Spear, TimedRecoilMath.Left, "polearm_block_thrust"));
    }
}

using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 65 - THE AI KICKS AND BASHES (Anton 2026-10-19: "make the AI do a few shield bashes and kicks now and then - the game has it but the NPCs
/// never use them"; AI_NOTES "Step 65", DESIGN decision 57): the settings, the master switch, the kind, the input frame of each phase, the summary.
/// Step 81 replaced the rate, its factors and the turtle memory with the shares (Step81Tests).
/// </summary>
public class AiKickTests
{
    private static AiKickRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return AiKickRules.From(s);
    }

    [Fact]
    public void The_settings_sit_on_the_Advanced_page_in_the_impact_group_after_the_cap_on_and_live()
    {
        var all = SettingsSchema.All.ToList();
        int cap = all.IndexOf(SettingsSchema.ImpactMultiplierCapPercent);
        var keys = new[]
        {
            SettingsSchema.AiKicksEnabled, SettingsSchema.AiShieldmanKickPercent, SettingsSchema.AiShieldmanBashPercent, SettingsSchema.AiNoShieldKickPercent,
            SettingsSchema.AiNoShieldPushPercent, SettingsSchema.AiKickReachMetres, SettingsSchema.AiBashReachMetres,
            SettingsSchema.AiBashStepIn, SettingsSchema.AiBashStepInToMetres, SettingsSchema.AiBashStepInMaxSeconds, SettingsSchema.AiKickFacingDegrees, SettingsSchema.AiKickCooldownSeconds,
            SettingsSchema.AiKickKicksARaisedShield, // step 91: AiBashReachMetres after the kick reach; step 95: the step-in's three after it
        };
        for (int i = 0; i < keys.Length; i++)
        {
            Assert.Equal(cap + 1 + i, all.IndexOf(keys[i]));
            Assert.Same(SettingsSchema.ImpactGroup, keys[i].Group);
            Assert.Equal(SettingsPage.Advanced, keys[i].Group.Page);
            Assert.Equal(ApplyTiming.Live, keys[i].Timing);
        }
        var s = new TraxSettings();
        Assert.True(s.AiKicksEnabled);
        Assert.True(AiKickRules.From(s).Enabled);
        s.Set(SettingsSchema.AiKickReachMetres, 2.0, SettingSources.Mcm);
        Assert.Equal(2.0, AiKickRules.From(s).Reach, 6); // live
    }

    [Fact]
    public void The_master_switch_comes_first_then_Athletics_then_the_switch_then_a_share()
    {
        Assert.Equal("ModEnabled", Rules(s => { s.Set(SettingsSchema.ModEnabled, false, "t"); s.Set(SettingsSchema.AiKicksEnabled, false, "t"); }).OffBecause);
        Assert.Equal("AthleticsEnabled", Rules(s => { s.Set(SettingsSchema.AthleticsEnabled, false, "t"); s.Set(SettingsSchema.AiKicksEnabled, false, "t"); }).OffBecause);
        Assert.Equal("AiKicksEnabled", Rules(s => s.Set(SettingsSchema.AiKicksEnabled, false, "t")).OffBecause);
        Assert.Null(Rules(s => s.Set(SettingsSchema.AiNoShieldKickPercent, 0, "t")).OffBecause); // the shieldmen still kick and bash
        Assert.Equal("AiShieldmanKickPercent, AiShieldmanBashPercent, AiNoShieldKickPercent and AiNoShieldPushPercent all 0",
            Rules(s => { s.Set(SettingsSchema.AiShieldmanKickPercent, 0, "t"); s.Set(SettingsSchema.AiShieldmanBashPercent, 0, "t"); s.Set(SettingsSchema.AiNoShieldKickPercent, 0, "t"); s.Set(SettingsSchema.AiNoShieldPushPercent, 0, "t"); }).OffBecause);
        Assert.Null(Rules().OffBecause);
        Assert.StartsWith("OFF (ModEnabled)", Rules(s => s.Set(SettingsSchema.ModEnabled, false, "t")).Describe(), StringComparison.Ordinal);
        Assert.Contains("the engine's kick key; a bash = the kick key with the guard up", Rules().Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_shield_bashes_unless_the_target_holds_his_shield_up_toward_him_no_shield_kicks()
    {
        var r = Rules();
        Assert.Equal(AiKickKind.Kick, AiKickMath.KindFor(in r, false, false));
        Assert.Equal(AiKickKind.Kick, AiKickMath.KindFor(in r, false, true));
        Assert.Equal(AiKickKind.Bash, AiKickMath.KindFor(in r, true, false));
        Assert.Equal(AiKickKind.Kick, AiKickMath.KindFor(in r, true, true));
        var always = Rules(s => s.Set(SettingsSchema.AiKickKicksARaisedShield, false, "t"));
        Assert.Equal(AiKickKind.Bash, AiKickMath.KindFor(in always, true, true));
    }

    [Fact]
    public void Each_phase_writes_the_engines_own_bits_both_kinds_hold_the_guard_first()
    {
        Assert.Equal(AiKickPhase.Guard, AiKickMath.FirstPhase(AiKickKind.Bash));
        Assert.Equal(AiKickPhase.Guard, AiKickMath.FirstPhase(AiKickKind.Kick)); // step 81: the guard cancels the wind-up the kick replaces
        Assert.Equal(AiKickPhase.Press, AiKickMath.Next(AiKickPhase.Guard));
        Assert.Equal(AiKickPhase.Commit, AiKickMath.Next(AiKickPhase.Press));
        Assert.Equal(AiKickPhase.None, AiKickMath.Next(AiKickPhase.Commit));
        Assert.Equal(10.4, AiKickMath.PhaseEnd(AiKickPhase.Guard, 10, AiKickKind.Bash), 9);
        Assert.Equal(10.15, AiKickMath.PhaseEnd(AiKickPhase.Guard, 10, AiKickKind.Kick), 9);
        Assert.Equal(10.15, AiKickMath.PhaseEnd(AiKickPhase.Press, 10, AiKickKind.Kick), 9);
        Assert.Equal(11.5, AiKickMath.PhaseEnd(AiKickPhase.Commit, 10, AiKickKind.Kick), 9);
        uint attackDown = 0x200, defendDown = 0x2000, forward = 0x1, jump = 0x8;
        // the lead: his own attack / guard out, the guard UP (a bash's context; a kick's cancel of the wind-up), every event out, moving on
        foreach (var kind in new[] { AiKickKind.Bash, AiKickKind.Kick })
        {
            uint ev0 = jump;
            uint f0 = AiKickMath.Frame(AiKickPhase.Guard, kind, attackDown | defendDown | forward, ref ev0, out bool stop0);
            Assert.Equal(AiKickMath.DefendUp | forward, f0);
            Assert.Equal(0u, ev0);
            Assert.False(stop0);
        }
        // the press: the kick key (+ the guard for a bash)
        uint ev = jump;
        uint f = AiKickMath.Frame(AiKickPhase.Press, AiKickKind.Bash, attackDown, ref ev, out bool stop);
        Assert.Equal(AiKickMath.DefendUp, f);
        Assert.Equal(AiKickMath.KickEventBit, ev);
        ev = 0;
        f = AiKickMath.Frame(AiKickPhase.Press, AiKickKind.Kick, defendDown | forward, ref ev, out stop);
        Assert.Equal(forward, f); // a kick drops the guard
        Assert.Equal(AiKickMath.KickEventBit, ev);
        // the commit: nothing of his own, standing still
        ev = jump;
        f = AiKickMath.Frame(AiKickPhase.Commit, AiKickKind.Kick, attackDown | forward, ref ev, out stop);
        Assert.Equal(forward, f);
        Assert.Equal(0u, ev);
        Assert.True(stop);
        // no attempt: untouched
        ev = jump;
        Assert.Equal(attackDown, AiKickMath.Frame(AiKickPhase.None, AiKickKind.None, attackDown, ref ev, out stop));
        Assert.Equal(jump, ev);
        Assert.Equal(AiInputMath.AttackMask, 0x3C0u);
    }

    [Fact]
    public void His_own_attack_reload_kick_or_weapon_change_keeps_him_busy()
    {
        foreach (int a in new[] { 15, 16, 17, 18, 19, 20, 28, 29, 30, 31, 33, 34 }) Assert.True(AiKickMath.IsBusyAction(a), a.ToString());
        foreach (int a in new[] { -1, 0, 2, 7, 21, 22, 23, 35, 36, 48 }) Assert.False(AiKickMath.IsBusyAction(a), a.ToString());
    }

    [Fact]
    public void The_summary_lines_count_asked_played_landed_by_kind_and_man_type()
    {
        var st = new AiKickStats();
        st.Asked(AiKickKind.Kick, AiKickStats.TypeOf(true));
        st.Asked(AiKickKind.Bash, AiKickStats.TypeOf(true));
        st.Asked(AiKickKind.Kick, AiKickStats.TypeOf(false));
        st.Played(AiKickKind.Kick, 0, 0.1);
        st.Played(AiKickKind.Bash, 0, 0.5);
        st.Landed(AiKickKind.Kick, 0, false);
        st.Landed(AiKickKind.Bash, 0, true);
        st.Ends[(int)AiKickEnd.Played] = 2;
        st.Ends[(int)AiKickEnd.NotPlayed] = 1;
        st.Skips[(int)AiKickSkip.OutOfReach] = 900;
        st.Skips[(int)AiKickSkip.NotFacing] = 40;
        st.Looks = 1000;
        st.ContactManSeconds = 120;
        st.PointsSpent = 9;
        var lines = st.SummaryLines(Rules()).ToList();
        Assert.Equal(9, lines.Count); // step 95: + the step-in bash (the 9th); step 91: + the shield bash's reach; step 80: + whom they hit and the strike path; step 81: the shares line in place of step 66's; step 87: + who blocks the path
        Assert.StartsWith("ai kicks (step 65 / 81): ON", lines[0], StringComparison.Ordinal);
        Assert.Contains("ai kicks: asked 3 (kicks 2, bashes 1, pushes 0) → PLAYED 2 (67%), seen 0.30 s after the ask → landed on the man 1, into a shield 1", lines[1], StringComparison.Ordinal);
        Assert.Contains("1.50 asked a man-minute in contact (2.0 man-minutes)", lines[1], StringComparison.Ordinal);
        Assert.Contains("Athletics spent on them 9", lines[1], StringComparison.Ordinal);
        Assert.Contains("shieldmen 2 → 2 → 1 / 1 | no shield 1 → 0 → 0 / 0", lines[2], StringComparison.Ordinal);
        Assert.Contains("played 2, NOT played (nothing showed within 1.5 s) 1", lines[3], StringComparison.Ordinal);
        Assert.Contains("(step 81 - 1000 AI melee wind-ups): not a move in reach - the enemy beyond kick reach 900, the enemy not in front 40", lines[3], StringComparison.Ordinal);
        Assert.StartsWith("ai kicks - shares of the melee moves in kick reach (step 81): ", lines[4], StringComparison.Ordinal);
    }
}

/// <summary>
/// Step 65 - THE KICKED MAN BRACES (Anton 2026-10-19: "make sure a looter kicking a heavy shielded troop doesn't do too much - maybe now and then
/// a lucky kick can stagger him for a fast bit"; DESIGN decision 58): the rules, the failure curve, the cut, the decision's brace.
/// </summary>
public class KickBraceTests
{
    private static ImpactRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return ImpactRules.From(s);
    }

    private sealed class Fixed : IRandomSource
    {
        private readonly double _u;
        public int Draws;

        public Fixed(double u) => _u = u;

        public double NextDouble()
        {
            Draws++;
            return _u;
        }
    }

    [Fact]
    public void The_seven_settings_follow_the_AI_kicks_on_the_Advanced_page_on_and_live()
    {
        var all = SettingsSchema.All.ToList();
        int last = all.IndexOf(SettingsSchema.AiKickKicksARaisedShield);
        var keys = new[]
        {
            SettingsSchema.KickBraceEnabled, SettingsSchema.KickBraceFromPressure, SettingsSchema.KickBraceToPressure, SettingsSchema.KickBraceMinBarPercent,
            SettingsSchema.KickBraceReelPercent, SettingsSchema.KickShortReelPercent, SettingsSchema.KickBraceRefusesKnockBack,
        };
        for (int i = 0; i < keys.Length; i++)
        {
            Assert.Equal(last + 1 + i, all.IndexOf(keys[i]));
            Assert.Equal(SettingsPage.Advanced, keys[i].Group.Page);
            Assert.Equal(ApplyTiming.Live, keys[i].Timing);
        }
        var b = KickBraceRules.From(new TraxSettings());
        Assert.True(b.On);
        Assert.Equal(-0.7, b.FromPressure, 6);
        Assert.Equal(1.0, b.ToPressure, 6);
        Assert.Equal(0.2, b.MinBar, 6);
        Assert.Equal(0.2, b.BraceReel, 6);
        Assert.Equal(0.4, b.ShortReel, 6);
        Assert.True(b.RefusesKnockBack);
        var off = KickBraceRules.From(Settings(s => s.Set(SettingsSchema.ModEnabled, false, "t")));
        Assert.False(off.On);
        Assert.Equal("ModEnabled", off.OffBecause); // the master switch first
        Assert.StartsWith("OFF (KickBraceEnabled)", KickBraceRules.From(Settings(s => s.Set(SettingsSchema.KickBraceEnabled, false, "t"))).Describe(), StringComparison.Ordinal);
    }

    private static TraxSettings Settings(Action<TraxSettings> tune)
    {
        var s = new TraxSettings();
        tune(s);
        return s;
    }

    [Fact]
    public void The_failure_is_an_S_curve_on_the_kicks_pressure_and_the_reel_cut_grows_with_tiredness()
    {
        var b = Rules().Brace;
        Assert.Equal(0, KickBraceMath.FailChance(in b, -0.8), 9);
        Assert.Equal(0, KickBraceMath.FailChance(in b, -0.7), 9);
        Assert.Equal(0.5, KickBraceMath.FailChance(in b, 0.15), 6); // the middle (the settings are floats)
        Assert.Equal(1, KickBraceMath.FailChance(in b, 1.0), 9);
        Assert.Equal(1, KickBraceMath.FailChance(in b, double.NaN), 9);
        Assert.True(KickBraceMath.Spent(in b, 0.19));
        Assert.False(KickBraceMath.Spent(in b, 0.2));
        Assert.Equal(0.4, KickBraceMath.ReelCut(in b, 0), 9);
        Assert.Equal(0.7, KickBraceMath.ReelCut(in b, 0.5), 9);
        Assert.Equal(1, KickBraceMath.ReelCut(in b, 1), 9);
        Assert.Equal(KickBraceOutcome.Braced, KickBraceMath.Classify(true, false, 0.2));
        Assert.Equal(KickBraceOutcome.Down, KickBraceMath.Classify(false, true, double.NaN));
        Assert.Equal(KickBraceOutcome.ShortReel, KickBraceMath.Classify(false, false, 0.4));
        Assert.Equal(KickBraceOutcome.FullReel, KickBraceMath.Classify(false, false, 1.0));
        Assert.Equal(0, KickBraceMath.TierBand(1.35));
        Assert.Equal(1, KickBraceMath.TierBand(3.0));
        Assert.Equal(2, KickBraceMath.TierBand(5.97));
        Assert.Equal(3, KickBraceMath.TierBand(10));
        Assert.Equal(8f, ImpactLiveMath.FastSpeedFor(0.1), 6);   // never faster than 8 x
        Assert.Equal(5f, ImpactLiveMath.FastSpeedFor(0.2), 4);
        Assert.Equal(1f, ImpactLiveMath.FastSpeedFor(1.0), 6);
        Assert.Equal(1f, ImpactLiveMath.SpeedFor(0.4), 6);       // step 50's slow path never speeds up
    }

    private static ImpactFacts Kick(double aPoints, double aSkill, double dPool, double dBar, double dSkill, double tA, double tD, bool shield = true)
    {
        double fD = Math.Min(1, dBar / 0.75);
        return new ImpactFacts
        {
            Event = ImpactEvent.Kick,
            AttackerPoints = aPoints,
            AttackerF = 1,
            AttackerAnimation = 1,
            AttackerSkill = aSkill,
            DefenderPoints = dPool * dBar,
            DefenderBar = dBar,
            DefenderF = fD,
            DefenderSkill = dSkill,
            DefenderHasShield = shield,
            DefenderMaxHealth = 100,
            AttackerToughness = tA,
            DefenderToughness = tD,
        };
    }

    [Fact]
    public void A_braced_man_is_never_knocked_down_or_disarmed_his_reaction_cut_and_no_reeling_state()
    {
        var r = Rules();
        // two even men, the defender at 30% (x ≈ 0.5: knockdown / drop rolls live) - the die 0.95: the brace holds (fail ~79%)
        var f = Kick(100, 100, 100, 0.3, 100, 3, 3);
        var ds = default(ImpactState);
        var at = default(ImpactState);
        var dice = new Fixed(0.95);
        var d = ImpactMath.Decide(in r, in f, ref ds, ref at, 100, dice);
        Assert.Equal(KickBraceOutcome.Braced, d.Brace);
        Assert.True(d.Braced);
        Assert.False(d.Knockdown.Happens);
        Assert.Equal(ImpactRefusal.Braced, d.Knockdown.Refusal);
        Assert.Equal(ImpactRefusal.Braced, d.ShieldDrop.Refusal);
        Assert.Equal(0.2, d.ReelFactor, 9);
        Assert.False(ds.Reeling(100.1)); // no impact stagger - no reeling bonus, no immunity
        Assert.Contains("BRACE (step 65): fails", ImpactMath.Describe(in d, in f, "A", "B", "LIVE"), StringComparison.Ordinal);
        // the same die low: the brace fails and step 50's rolls go on
        var d2 = ImpactMath.Decide(in r, in f, ref ds, ref at, 200, new Fixed(0.01));
        Assert.False(d2.Braced);
        Assert.True(d2.Knockdown.Happens);
        Assert.Equal(KickBraceOutcome.Down, d2.Brace);
    }

    [Fact]
    public void A_spent_man_gets_step_50_exactly_and_the_brace_off_draws_no_die()
    {
        var on = Rules();
        var off = Rules(s => s.Set(SettingsSchema.KickBraceEnabled, false, "t"));
        var f = Kick(100, 100, 100, 0.1, 100, 3, 3); // 10% of his bar: below the brace's 20%
        var s1 = default(ImpactState);
        var a1 = default(ImpactState);
        var s2 = default(ImpactState);
        var a2 = default(ImpactState);
        var dOn = ImpactMath.Decide(in on, in f, ref s1, ref a1, 100, new SeededRandom(7));
        var dOff = ImpactMath.Decide(in off, in f, ref s2, ref a2, 100, new SeededRandom(7));
        Assert.True(dOn.BraceSpent);
        Assert.Equal(dOff.Knockdown.Happens, dOn.Knockdown.Happens);
        Assert.Equal(dOff.ReelFactor, dOn.ReelFactor);
        Assert.Equal(dOff.Knockdown.Chance, dOn.Knockdown.Chance, 12);
        Assert.Equal(KickBraceOutcome.NotAsked, dOff.Brace);
        // a rider, a block: never asked
        var rider = f;
        rider.DefenderMounted = true;
        Assert.Equal(KickBraceOutcome.NotAsked, ImpactMath.Decide(in on, in rider, ref s1, ref a1, 300, new SeededRandom(7)).Brace);
    }

    [Fact]
    public void Against_you_the_failure_scales_with_ImpactOnPlayerPercent()
    {
        var r = Rules(s => s.Set(SettingsSchema.ImpactOnPlayerPercent, 0, "t"));
        var f = Kick(100, 100, 100, 1.0, 100, 3, 3);
        f.DefenderIsPlayer = true;
        var ds = default(ImpactState);
        var at = default(ImpactState);
        var d = ImpactMath.Decide(in r, in f, ref ds, ref at, 100, new Fixed(0.0));
        Assert.Equal(0, d.BraceFail, 9);
        Assert.True(d.Braced);
    }

    [Fact]
    public void The_summary_lines_by_tier_and_bar()
    {
        var st = new KickBraceStats();
        st.Note(KickBraceOutcome.Braced, 5.97, 1.0, 0.13, false, false, false, false);
        st.Note(KickBraceOutcome.ShortReel, 5.97, 0.9, 0.13, false, false, true, false);
        st.Note(KickBraceOutcome.Down, 1.35, 0.1, 1, true, true, false, true);
        st.KnockBackAsked = 3;
        st.KnockBackRefused = 1;
        var lines = st.SummaryLines(KickBraceRules.From(new TraxSettings())).ToList();
        Assert.Equal(5, lines.Count);
        Assert.Contains("3 landed kicks / bashes decided (1 bashes): braced 1 (33.3%), short reel 1 (33.3%), full reel 0 (0.0%), knocked down 1 (33.3%); on a SPENT man (below 20% of his bar - no brace) 1", lines[0], StringComparison.Ordinal);
        Assert.Contains("heavy (4-7) 2: braced 50% / short 50% / full 0% / down 0% (the model's mean failure chance 13%)", lines[1], StringComparison.Ordinal);
        Assert.Contains("light (toughness < 2) 1: braced 0% / short 0% / full 0% / down 100%", lines[1], StringComparison.Ordinal);
        Assert.Contains("100-75% 2: braced 50% / short 50%", lines[2], StringComparison.Ordinal);
        Assert.Contains("the knock-back seam asked 3, the knock-back REFUSED for a braced man 1", lines[3], StringComparison.Ordinal);
        Assert.Contains("kicked 1 (braced 0, short reel 1, full reel 0, down 0); your kicks / bashes on others 1 (braced 0, short reel 0, full reel 0, down 1)", lines[4], StringComparison.Ordinal);
    }
}

/// <summary>
/// Step 65 - THE WORKED EXAMPLE (MECHANICS §11c "The kicked man braces", DESIGN decision 58): the real model with the shipped kick values, 20 000
/// seeded kicks per row. Troops as step 64's example: a looter (pool 50, Athletics 30, toughness 1.35), a legionary (pool 130, Athletics 130,
/// toughness 5.97), even men (pool 100, Athletics 100, toughness 3.0).
/// </summary>
public class Step65ExamplesTests
{
    private readonly record struct Troop(double Pool, double Ath, double Armour, int Level)
    {
        public double Toughness => CourageMath.BaseToughness(200, 100, Armour, Level);
    }

    private static readonly Troop Legionary = new(130, 130, 110, 26);
    private static readonly Troop Looter = new(50, 30, 8, 5);
    private static readonly Troop Even = new(100, 100, 50, 15);

    private readonly record struct Row(double Fail, double Braced, double Short, double Full, double Down, double Drop, double ShortReel);

    private static Row Run(Troop a, double aBar, Troop d, double dBar, int n = 20000)
    {
        var r = ImpactRules.From(new TraxSettings());
        var dice = new SeededRandom(65);
        int br = 0, sh = 0, fu = 0, dn = 0, drop = 0;
        double fail = 0, reel = 0;
        for (int i = 0; i < n; i++)
        {
            var f = new ImpactFacts
            {
                Event = ImpactEvent.Kick,
                AttackerPoints = a.Pool * aBar,
                AttackerF = Math.Min(1, aBar / 0.75),
                AttackerAnimation = 1,
                AttackerSkill = a.Ath,
                DefenderPoints = d.Pool * dBar,
                DefenderBar = dBar,
                DefenderF = Math.Min(1, dBar / 0.75),
                DefenderSkill = d.Ath,
                DefenderHasShield = true,
                DefenderMaxHealth = 100,
                AttackerSpeed = 1.0,
                AttackerToughness = a.Toughness,
                DefenderToughness = d.Toughness,
            };
            var ds = default(ImpactState);
            var at = default(ImpactState);
            var dd = ImpactMath.Decide(in r, in f, ref ds, ref at, 100, dice);
            fail = dd.BraceFail;
            switch (dd.Brace)
            {
                case KickBraceOutcome.Braced: br++; break;
                case KickBraceOutcome.ShortReel: sh++; reel += dd.ReelFactor; break;
                case KickBraceOutcome.FullReel: fu++; break;
                case KickBraceOutcome.Down: dn++; break;
            }
            if (dd.ShieldDrop.Happens) drop++;
        }
        return new Row(fail, br / (double)n, sh / (double)n, fu / (double)n, dn / (double)n, drop / (double)n, sh > 0 ? reel / sh : double.NaN);
    }

    private static void Near(double expected, double actual, double tol, string what) =>
        Assert.True(Math.Abs(expected - actual) <= tol, what + ": " + (100 * actual).ToString("0.0") + "% vs " + (100 * expected).ToString("0.0") + "%");

    [Fact]
    public void A_looter_kicking_a_fresh_legionary_shieldman_a_short_reel_one_time_in_eight_never_a_fall_or_a_dropped_shield()
    {
        var row = Run(Looter, 1.0, Legionary, 1.0);
        Near(0.129, row.Fail, 0.005, "the model's failure");
        Assert.InRange(row.Short, 0.10, 0.15);          // Anton: "now and then a lucky kick ... for a fast bit"
        Assert.Equal(0, row.Down);
        Assert.Equal(0, row.Drop);
        Assert.Equal(0, row.Full);
        Assert.Equal(0.4, row.ShortReel, 6);            // 40% of the game's knock-back
        // a looter at 60% of his bar: fewer still; the legionary at half his bar: about half fail, still no fall
        var tired = Run(Looter, 0.6, Legionary, 1.0);
        Assert.InRange(tired.Short, 0.05, 0.10);
        var half = Run(Looter, 1.0, Legionary, 0.5);
        Assert.InRange(half.Short, 0.40, 0.55);
        Assert.Equal(0, half.Down);
    }

    [Fact]
    public void Two_fresh_even_men_brace_about_two_times_in_three()
    {
        var row = Run(Even, 1.0, Even, 1.0);
        Near(0.37, row.Fail, 0.01, "even fresh men's failure");
        Assert.InRange(row.Braced, 0.60, 0.66);
        Assert.Equal(0, row.Down);
        Assert.Equal(0.4, row.ShortReel, 6);
    }

    [Fact]
    public void A_fresh_kicker_on_an_empty_man_no_brace_step_50_in_full()
    {
        var row = Run(Legionary, 1.0, Even, 0.0);
        Assert.Equal(1, row.Fail);
        Assert.Equal(0, row.Braced);
        Assert.InRange(row.Down, 0.75, 0.85);             // step 50's 80% ceiling at x ≥ 1
        Assert.InRange(row.Drop, 0.85, 0.95);
        var looter = Run(Legionary, 1.0, Looter, 0.0);
        Assert.Equal(0, looter.Braced);
        Assert.InRange(looter.Down, 0.75, 0.85);
    }
}

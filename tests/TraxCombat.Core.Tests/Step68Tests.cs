using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 68 - THE WALL PUSHES AS ONE + KICKS THROUGH THE IMPACT MODEL + MEN TIRE MORE (Toni's 100 v 100 of 2026-10-19, battle_terrain_biome_048:
/// wall men in contact more than 1 m ahead of their slots 15.4% with the wall ordered forward - F1 then F4 = ADVANCE; 92 AI kicks / bashes "landed
/// on the man", 33 decided; "they need to hold the wall and push the whole wall, not to break up"; "tire the men more to slow down the combat again
/// a bit after the first impact"; AI_NOTES "Step 68", DESIGN decision 61): the five settings, the pace (group, gap, the capped point, why not),
/// the walk back after a kick, no kick through a friend and whom a kick landed on, every landed kick against the decisions, the new costs and
/// config format 14.
/// </summary>
public class Step68Tests
{
    /// <summary>defaults.json's step-68 costs (the tests run on DESIGN's initial 10 / 1 / 5).</summary>
    private static double Shipped(ParamDef p) =>
        p == SettingsSchema.CostPerBlow ? 8.0 : p == SettingsSchema.CostPerShieldBlock ? 2.0 : p == SettingsSchema.CostPerWrongSideShieldBlock ? 5.0
        : p == SettingsSchema.CostPerKickOrBash ? 5.0 : p.Default;

    private static WallPaceRules Pace(bool mod = true, bool hold = true, bool asOne = true, double step = 1.0, double near = 10.0) =>
        new WallPaceRules(mod, hold, asOne, step, near);

    /// <summary>A held man of a shield wall facing +y, standing on his slot (0, 0); the game's order slot <paramref name="lead"/> m ahead; the enemy's
    /// front <paramref name="gap"/> m away.</summary>
    private static WallFacts Man(BraceOrderKind order = BraceOrderKind.Advance, double lead = 6.0, double gap = 2.0, bool orderSlot = true) => new WallFacts
    {
        Ai = true,
        HasFormation = true,
        Arrangement = WallArrangement.ShieldWall,
        Order = order,
        HasPositions = true,
        AgentX = 0, AgentY = 0,
        SlotX = 0, SlotY = 0,
        DirX = 0, DirY = 1,
        CentreX = 0, CentreY = -1,
        AvgSpeed = 3.5,
        HasOrderSlot = orderSlot,
        OrderX = 0, OrderY = lead,
        EnemyGap = gap,
    };

    [Fact]
    public void The_five_settings_sit_on_the_Advanced_page_live_after_their_systems()
    {
        var all = SettingsSchema.All.ToList();
        int pull = all.IndexOf(SettingsSchema.ShieldWallPullBackSeconds);
        var wall = new[] { SettingsSchema.ShieldWallAdvanceAsOne, SettingsSchema.ShieldWallAdvanceStepMetres, SettingsSchema.ShieldWallAdvanceNearMetres, SettingsSchema.ShieldWallKickReturnMetres };
        for (int i = 0; i < wall.Length; i++)
        {
            Assert.Equal(pull + 1 + i, all.IndexOf(wall[i]));
            Assert.Same(SettingsSchema.BattlePaceGroup, wall[i].Group);
            Assert.Equal(SettingsPage.Advanced, wall[i].Group.Page);
            Assert.Equal(ApplyTiming.Live, wall[i].Timing);
        }
        Assert.Equal(all.IndexOf(SettingsSchema.KickBraceRefusesKnockBack) + 1, all.IndexOf(SettingsSchema.AiKickFriendClearMetres));
        Assert.Same(SettingsSchema.ImpactGroup, SettingsSchema.AiKickFriendClearMetres.Group);
        Assert.Equal(SettingsPage.Advanced, SettingsSchema.AiKickFriendClearMetres.Group.Page);
        Assert.Equal(ApplyTiming.Live, SettingsSchema.AiKickFriendClearMetres.Timing);
        var s = new TraxSettings(); // DESIGN's initial values = the shipped ones for the new keys
        Assert.True(s.ShieldWallAdvanceAsOne);
        Assert.Equal(1.0, s.ShieldWallAdvanceStepMetres, 6);
        Assert.Equal(10.0, s.ShieldWallAdvanceNearMetres, 6);
        Assert.Equal(0.5, s.ShieldWallKickReturnMetres, 6);
        Assert.Equal(0.5, s.AiKickFriendClearMetres, 6);
        Assert.Equal(0.3, SettingsSchema.ShieldWallAdvanceStepMetres.Min, 6);
        Assert.Equal(3.0, SettingsSchema.ShieldWallAdvanceStepMetres.Max, 6);
    }

    [Fact]
    public void The_held_orders_fall_into_standing_advance_and_moving_and_only_the_moving_ones_are_paced()
    {
        Assert.Equal(WallOrderGroup.Standing, WallPaceMath.GroupOf(BraceOrderKind.Stop));
        Assert.Equal(WallOrderGroup.Advance, WallPaceMath.GroupOf(BraceOrderKind.Advance));
        Assert.Equal(WallOrderGroup.Advance, WallPaceMath.GroupOf(BraceOrderKind.AiAdvance));
        foreach (var k in new[] { BraceOrderKind.Move, BraceOrderKind.FallBack, BraceOrderKind.Follow, BraceOrderKind.FollowEntity })
        {
            Assert.Equal(WallOrderGroup.Moving, WallPaceMath.GroupOf(k));
            Assert.True(WallPaceMath.Paces(k));
        }
        Assert.False(WallPaceMath.Paces(BraceOrderKind.Stop));
        Assert.True(WallPaceMath.Paces(BraceOrderKind.Advance));
        // the step-41 order gate still lets a charge go and holds an advance - the pace never meets a charge
        Assert.Equal(WallVerdict.ChargeOrder, WallMath.OrderVerdict(BraceOrderKind.Charge));
        Assert.Equal(WallVerdict.ChargeOrder, WallMath.OrderVerdict(BraceOrderKind.ChargeToTarget));
        Assert.Equal(WallVerdict.Hold, WallMath.OrderVerdict(BraceOrderKind.Advance));
    }

    [Fact]
    public void An_advancing_man_near_the_enemy_is_sent_one_step_toward_the_games_slot_never_further()
    {
        var r = Pace();
        Assert.True(WallPaceMath.Pace(in r, Man(lead: 6), out double x, out double y));
        Assert.Equal(0.0, x, 9);
        Assert.Equal(1.0, y, 9); // 1 m toward the game's order slot 6 m ahead, not 6
        // sideways too: the step is along the line to the order slot
        var f = Man();
        f.OrderX = 3;
        f.OrderY = 4;
        Assert.True(WallPaceMath.Pace(in r, f, out x, out y));
        Assert.Equal(0.6, x, 9);
        Assert.Equal(0.8, y, 9);
        // within the step: the game's own slot (nothing to cap)
        Assert.False(WallPaceMath.Pace(in r, Man(lead: 0.8), out _, out _));
        Assert.Equal(WallPaceSkip.WithinStep, WallPaceMath.Why(in r, Man(lead: 0.8)));
        // the step is a setting (live): 2 m
        Assert.True(WallPaceMath.Pace(Pace(step: 2), Man(lead: 6), out _, out y));
        Assert.Equal(2.0, y, 9);
        // far from the enemy (the game's march), no enemy known, a stop, no order slot
        Assert.False(WallPaceMath.Pace(in r, Man(gap: 25), out _, out _));
        Assert.Equal(WallPaceSkip.Far, WallPaceMath.Why(in r, Man(gap: 25)));
        Assert.Equal(WallPaceSkip.Far, WallPaceMath.Why(in r, Man(gap: double.NaN)));
        Assert.Equal(WallPaceSkip.Standing, WallPaceMath.Why(in r, Man(order: BraceOrderKind.Stop)));
        Assert.Equal(WallPaceSkip.NoOrderSlot, WallPaceMath.Why(in r, Man(orderSlot: false)));
        Assert.Equal(WallPaceSkip.None, WallPaceMath.Why(in r, Man()));
        // a fall back walks back one step at a time too
        var back = Man(order: BraceOrderKind.FallBack, lead: -7);
        Assert.True(WallPaceMath.Pace(in r, back, out _, out y));
        Assert.Equal(-1.0, y, 9);
    }

    [Fact]
    public void The_master_switch_comes_first_then_the_hold_then_the_pace_switch()
    {
        Assert.Equal("ModEnabled", Pace(mod: false, hold: false, asOne: false).OffBecause);
        Assert.Equal("ShieldWallHoldsSlots", Pace(hold: false).OffBecause);
        Assert.Equal("ShieldWallAdvanceAsOne", Pace(asOne: false).OffBecause);
        Assert.Null(Pace().OffBecause);
        foreach (var r in new[] { Pace(mod: false), Pace(hold: false), Pace(asOne: false) })
        {
            Assert.False(WallPaceMath.Pace(in r, Man(), out _, out _));
            Assert.Equal(WallPaceSkip.Off, WallPaceMath.Why(in r, Man()));
        }
        Assert.StartsWith("OFF (ModEnabled) - a held wall under an advance walks to the game's own slots", Pace(mod: false).Describe(), StringComparison.Ordinal);
        Assert.StartsWith("ON (ShieldWallAdvanceAsOne) - a held shield wall / square / circle under a MOVING order (advance - yours or the AI's -, move, fall back, follow) within 10.0 m of the enemy's front walks as ONE line: each man is sent at most 1.00 m ahead",
            Pace().Describe(), StringComparison.Ordinal);
        var s = new TraxSettings();
        s.Set(SettingsSchema.ShieldWallAdvanceStepMetres, 0.1, SettingSources.Mcm); // below the range: clamped
        Assert.True(WallPaceRules.From(s).StepMetres >= WallPaceMath.MinStepMetres - 1e-9);
        s.Set(SettingsSchema.ShieldWallAdvanceAsOne, false, SettingSources.Mcm);
        Assert.False(WallPaceRules.From(s).Enabled); // live
    }

    [Fact]
    public void The_front_gap_is_the_centres_distance_less_half_of_each_depth()
    {
        Assert.Equal(6.0, WallPaceMath.FrontGap(10, 4, 4), 9);
        Assert.Equal(0.0, WallPaceMath.FrontGap(3, 4, 4), 9); // overlapping fronts read 0
        Assert.True(double.IsNaN(WallPaceMath.FrontGap(double.NaN, 4, 4)));
        Assert.True(WallPaceMath.Engaged(0, 0));
        Assert.True(WallPaceMath.Engaged(10, 10));
        Assert.False(WallPaceMath.Engaged(10.5, 10));
        Assert.False(WallPaceMath.Engaged(double.NaN, 10));
    }

    [Fact]
    public void The_by_order_line_splits_the_in_contact_leak_and_the_pace_line_counts_the_writes()
    {
        var st = new WallPaceStats();
        for (int i = 0; i < 100; i++) st.AddSample(BraceOrderKind.Stop, i < 1 ? 1.5 : 0.1, true);     // 1% leak standing
        for (int i = 0; i < 100; i++) st.AddSample(BraceOrderKind.Advance, i < 15 ? 1.5 : 0.1, true);  // 15% advancing
        st.AddSample(BraceOrderKind.Move, 0.2, false);
        string line = st.ByOrderLine();
        Assert.StartsWith("wall slots ON by order (step 68 - the in-contact samples, an enemy within 5 m): standing (stop / hold) 100: more than 1 m ahead 1.0%", line, StringComparison.Ordinal);
        Assert.Contains("| ADVANCE (yours or the AI's) 100: more than 1 m ahead 15.0%, more than 2 m 0.0%, mean 0.31 m, the farthest 1.5 m", line, StringComparison.Ordinal);
        Assert.Contains("| moving (move / fall back / follow) - (1 samples, none in contact)", line, StringComparison.Ordinal);
        st.EverOn = true;
        st.Paced(WallOrderGroup.Advance, 6.0, 1.5);
        st.Paced(WallOrderGroup.Moving, 2.0, 3.5);
        st.MenPaced = 2;
        st.Skip(WallPaceSkip.Far);
        st.Restored = 1;
        string pace = st.PaceLine(Pace());
        Assert.Contains(" - 2 paced writes on 2 men (advance 1, move / fall back / follow 1); the game's order slot lay 4.00 m on average from his slot in the line (the farthest 6.0 m), the enemy's front 2.5 m away on average", pace, StringComparison.Ordinal);
        Assert.Contains("the enemy's front farther than 10.0 m (the game's march) 1", pace, StringComparison.Ordinal);
        Assert.Contains("frames given back to the game's 1; errors 0", pace, StringComparison.Ordinal);
        Assert.EndsWith("never on this battle", new WallPaceStats().PaceLine(Pace(asOne: false)), StringComparison.Ordinal);
    }

    [Fact]
    public void After_his_own_kick_a_held_man_walks_back_at_once_from_half_a_metre()
    {
        const double leash = 1.0, kickReturn = 0.5;
        var s = default(WallOutOfPlace);
        // 0.8 m out after his own kick: out of place (step 63's mark is 1.25 m), walked at once when he can move
        Assert.Equal(WallOutMove.Began, WallPullMath.Step(ref s, leash, 0.8, false, false, 0.5, 10.0, 0, kickReturn, true));
        Assert.True(s.AfterOwnKick);
        Assert.Equal(WallOutMove.None, WallPullMath.Step(ref s, leash, 0.8, false, false, 0.5, 10.1, 0.1, kickReturn, true)); // still in his kick
        Assert.Equal(WallOutMove.PullStarted, WallPullMath.Step(ref s, leash, 0.8, true, false, 0.5, 10.2, 0.1, kickReturn, false)); // no 0.5 s wait
        Assert.Equal(WallOutMove.None, WallPullMath.Step(ref s, leash, 0.4, true, false, 0.5, 10.3, 0.1, kickReturn, false));
        Assert.Equal(WallOutMove.Back, WallPullMath.Step(ref s, leash, 0.24, true, false, 0.5, 10.4, 0.1, kickReturn, false)); // back within 0.25
        // without a kick 0.8 m is nobody's business (step 42's leash allows 1 m)
        var t = default(WallOutOfPlace);
        Assert.Equal(WallOutMove.None, WallPullMath.Step(ref t, leash, 0.8, true, false, 0.5, 10.0, 0, kickReturn, false));
        // the setting 0: step 63 exactly (1.25 m, the wait) - with ShieldWallPullBackSeconds 0 too: never walked
        var u = default(WallOutOfPlace);
        Assert.Equal(WallOutMove.None, WallPullMath.Step(ref u, leash, 0.8, true, false, 0.5, 10.0, 0, 0, true));
        Assert.Equal(WallOutMove.Began, WallPullMath.Step(ref u, leash, 1.3, true, false, 0, 10.0, 0, 0, true));
        Assert.False(u.AfterOwnKick);
        Assert.Equal(WallOutMove.None, WallPullMath.Step(ref u, leash, 1.3, true, false, 0, 12.0, 0.1, 0, true));
        // the kick return works with step 63's pull at 0
        var w = default(WallOutOfPlace);
        WallPullMath.Step(ref w, leash, 0.9, true, false, 0, 10.0, 0, kickReturn, true);
        Assert.Equal(WallOutMove.PullStarted, WallPullMath.Step(ref w, leash, 0.9, true, false, 0, 10.1, 0.1, kickReturn, true));
        // the old overload is step 63's
        var v = default(WallOutOfPlace);
        Assert.Equal(WallOutMove.None, WallPullMath.Step(ref v, leash, 0.8, true, false, 0.5, 10.0, 0));
    }

    [Fact]
    public void The_pulled_back_line_counts_the_spells_after_a_kick_apart()
    {
        var p = new WallPullStats();
        p.Began(false, true);
        var s = new WallOutOfPlace { Out = true, Since = 10, AfterOwnKick = true, MaxOut = 0.9, Pulled = true };
        p.Ended(in s, WallOutEnd.Back, 11.2);
        p.Began(true);
        string line = p.SummaryLine(0.5, 1.0);
        Assert.Contains("2 spells out of place (after an impact 1, other 1)", line, StringComparison.Ordinal);
        Assert.EndsWith("; step 68 - after his OWN kick / bash or a punish press 1 (back in 1.20 s (n 1))", line, StringComparison.Ordinal);
    }

    [Fact]
    public void A_friend_on_the_line_to_the_enemy_holds_the_kick_a_man_beside_him_does_not()
    {
        // the kicker at (0, 0) facing his enemy 1.6 m ahead at (0, 1.6) - since step 80 the probe along his FACING
        static AiKickPath V(double fx, double fy, double clear)
        {
            var p = AiKickPathProbe.Begin(0, 0, 0, 1, 0, 1.6, 1.6, clear, 0);
            p.Add(fx, fy);
            return p.Verdict;
        }
        Assert.Equal(AiKickPath.FriendFirst, V(0.0, 0.8, 0.5));   // his front-rank mate between them
        Assert.Equal(AiKickPath.FriendFirst, V(0.4, 0.9, 0.5));   // a little to the side, still on the line
        Assert.Equal(AiKickPath.Clear, V(0.7, 0.9, 0.5));         // beyond the clearance
        Assert.Equal(AiKickPath.Clear, V(0.6, 0.0, 0.5));         // shoulder to shoulder beside him
        Assert.Equal(AiKickPath.Clear, V(0.0, -0.8, 0.5));        // behind him
        Assert.Equal(AiKickPath.Clear, V(0.0, 2.0, 0.5));         // beyond the enemy
        Assert.Equal(AiKickPath.Clear, V(0.0, 0.8, 0));           // the check off
        var s = new TraxSettings();
        s.Set(SettingsSchema.AiKickFriendClearMetres, 0.5, SettingSources.Mcm);
        var r = AiKickRules.From(s);
        Assert.Equal(0.5, r.FriendClear, 6);
        Assert.Contains("; only when the enemy is the FIRST body in his strike path along his facing (step 80: a kick's path 0.50 m", r.Describe(), StringComparison.Ordinal);
        s.Set(SettingsSchema.AiKickFriendClearMetres, 0.0, SettingSources.Mcm);
        s.Set(SettingsSchema.AiBashPathHalfWidthMetres, 0.0, SettingSources.Mcm);
        Assert.DoesNotContain("strike path", AiKickRules.From(s).Describe(), StringComparison.Ordinal);
        Assert.Equal(16, AiKickMath.SkipCount); // step 81 rebuilt the reasons around the decision point
        Assert.Equal("a friend in front, beyond the rear-rank reach (step 90)", AiKickMath.SkipName(AiKickSkip.FriendInTheWay));
        Assert.Equal(AiKickMath.SkipCount, Enum.GetValues(typeof(AiKickSkip)).Length);
    }

    [Fact]
    public void The_ai_kicks_line_says_whom_the_landed_ones_hit_and_the_friend_reads()
    {
        var st = new AiKickStats();
        st.Asked(AiKickKind.Bash, 0);
        st.Played(AiKickKind.Bash, 0, 0.4);
        st.Landed(AiKickKind.Bash, 0, false);
        st.NoteVictim(AiKickVictim.Friend);
        st.FriendChecksBlocked = 4;
        st.FriendChecksClear = 9;
        var s = new TraxSettings();
        s.Set(SettingsSchema.AiKickFriendClearMetres, 0.5, SettingSources.Mcm);
        var lines = st.SummaryLines(AiKickRules.From(s)).ToList();
        Assert.Contains("→ landed on the man 1 (step 68: on his target 0, on another enemy 0, on a FRIEND (the game cancels it - never decided) 1, on a horse / a man not tracked 0), into a shield 0", lines[1], StringComparison.Ordinal);
        Assert.Contains("; step 68 / 80 - a friend first in the strike path (a kick's 0.5 m either side): held 4, clear 9", lines[3], StringComparison.Ordinal);
        Assert.Equal(string.Empty, new AiKickStats().VictimsText());
    }

    [Fact]
    public void Every_landed_kick_is_decided_or_named_and_a_kick_into_a_raised_shield_is_counted_apart()
    {
        var st = new ImpactStats();
        // Toni's battle in miniature: 92 landed - 33 decided, the rest on friends; 39 into a shield
        for (int i = 0; i < 60; i++) st.NoteKickHit(false, true, false, false);
        for (int i = 0; i < 32; i++) st.NoteKickHit(true, true, false, false);
        for (int i = 0; i < 39; i++) st.NoteKickHit(true, false, true, false);
        st.NoteKickHit(false, false, false, false);
        st.Decided[(int)ImpactEvent.Kick, 0] = 21;
        st.Decided[(int)ImpactEvent.Bash, 1] = 12;
        for (int i = 0; i < 59; i++) st.Skip(ImpactSkip.NotEnemies, i < 39 ? ImpactEvent.Kick : ImpactEvent.Bash);
        st.Skip(ImpactSkip.NotEnemies, ImpactEvent.CleanHit); // a clean hit's skip is not a kick's
        string line = st.KickCoverageLine();
        Assert.StartsWith("impact kicks / bashes landed vs decided (step 68): landed on a man 92 (kicks 60, bashes 32; by you 0) → DECIDED 33 (kicks 21, bashes 12); not decided: on a FRIEND (the game cancels it, nothing plays - step 63) 59, on a horse / a man not tracked 0, it killed him 0",
            line, StringComparison.Ordinal);
        Assert.Contains("; unaccounted 0 (every landed kick / bash on an enemy decided)", line, StringComparison.Ordinal);
        Assert.Contains("| into a raised shield 39 (kicks 0, bashes 39) - not decided: the shield stopped it", line, StringComparison.Ordinal);
        Assert.EndsWith("other (the world, a miss) 1", line, StringComparison.Ordinal);
        Assert.Equal(60, st.Skipped[(int)ImpactSkip.NotEnemies]); // the header's count keeps every event
        st.NoteKickHit(false, true, false, true); // a gap - one landed kick nobody decided or named
        Assert.Contains("; unaccounted 1 (should be 0 - tell Claude)", st.KickCoverageLine(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_new_costs_tire_everyone_more_heroes_and_leaders_keep_their_multipliers()
    {
        var s = new TraxSettings();
        foreach (var p in new[] { SettingsSchema.CostPerBlow, SettingsSchema.CostPerShieldBlock, SettingsSchema.CostPerWrongSideShieldBlock, SettingsSchema.CostPerKickOrBash })
            s.Set(p, Shipped(p), "test");
        var r = AthleticsRules.From(s);
        var man = new Fighter();
        var hero = new Fighter { IsHero = true };
        var leader = new Fighter { IsHero = true, IsLeader = true };
        Assert.Equal(8.0, AthleticsMath.BlowCostPoints(in r, man), 6);
        Assert.Equal(6.0, AthleticsMath.BlowCostPoints(in r, hero), 6);
        Assert.Equal(4.5, AthleticsMath.BlowCostPoints(in r, leader), 6);
        Assert.Equal(5.0, AthleticsMath.KickOrBashCostPoints(in r, man), 6);             // the kick / bash stays 5
        Assert.Equal(2.0, AthleticsMath.BlockCostPoints(in r, man, BlockKind.ShieldRightSide), 6);
        Assert.Equal(1.5, AthleticsMath.BlockCostPoints(in r, hero, BlockKind.ShieldRightSide), 6);
        Assert.Equal(5.0, AthleticsMath.BlockCostPoints(in r, man, BlockKind.ShieldWrongSide), 6);
        Assert.Equal(2.8125, AthleticsMath.BlockCostPoints(in r, leader, BlockKind.ShieldWrongSide), 6);
        Assert.Equal(2.0, AthleticsMath.BlockCostPoints(in r, man, BlockKind.WeaponParry), 6); // the parry stays 2
        // a recruit's 50-point bar: empty after 7 blows (was 8 at 7), a legionary's 130 after 17 (was 19)
        Assert.Equal(7, (int)Math.Ceiling(50 / 8.0));
        Assert.Equal(17, (int)Math.Ceiling(130 / 8.0));
    }

    [Fact]
    public void Format_14_moves_the_old_costs_once_and_keeps_a_tuned_value()
    {
        Assert.Equal(24, ConfigFile.FormatVersion);
        var read = ConfigFile.Read("{ \"ConfigVersion\": 13, \"CostPerBlow\": 7.0, \"CostPerShieldBlock\": 1.0, \"CostPerWrongSideShieldBlock\": 2.0, \"CostPerWeaponParry\": 2.0, \"CostPerKickOrBash\": 5.0 }");
        var notes = ConfigFile.Migrate(read, Shipped);
        Assert.Equal(3, notes.Count);
        Assert.StartsWith("CostPerBlow: 7.0 → 8.0 (format 13 → 24, the old default; step 68, Toni: tire the men more to slow the combat again a bit after the first impact - a swing or shot costs 8)", notes[0], StringComparison.Ordinal);
        Assert.StartsWith("CostPerWrongSideShieldBlock: 2.0 → 5.0 (format 13 → 24, the old default; step 68", notes[1], StringComparison.Ordinal);
        Assert.StartsWith("CostPerShieldBlock: 1.0 → 2.0 (format 13 → 24, the old default; step 68", notes[2], StringComparison.Ordinal);
        Assert.Equal(8, read.Values["CostPerBlow"], 9);
        Assert.Equal(2, read.Values["CostPerShieldBlock"], 9);
        Assert.Equal(5, read.Values["CostPerWrongSideShieldBlock"], 9);
        Assert.Equal(2, read.Values["CostPerWeaponParry"], 9); // the parry stays
        Assert.Equal(5, read.Values["CostPerKickOrBash"], 9);  // the kick stays
        Assert.Equal(3, ConfigFile.Migrate(read, Shipped).Count); // idempotent (the notes list is the read's own)
        // his own values stay - and are said once
        var mine = ConfigFile.Read("{ \"ConfigVersion\": 13, \"CostPerBlow\": 6.0, \"CostPerShieldBlock\": 0.0, \"CostPerWrongSideShieldBlock\": 3.0 }");
        var kept = ConfigFile.Migrate(mine, Shipped);
        Assert.Equal(3, kept.Count);
        Assert.StartsWith("CostPerBlow: 6.0 kept - your own value (format 13 → 24; the new default 8.0 is not pushed over it; step 68", kept[0], StringComparison.Ordinal);
        Assert.Equal(6, mine.Values["CostPerBlow"], 9);
        Assert.Equal(0, mine.Values["CostPerShieldBlock"], 9);
        Assert.Equal(3, mine.Values["CostPerWrongSideShieldBlock"], 9);
        // already the new values, a file without the keys, a format-14 file: nothing to say
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 13, \"CostPerBlow\": 8.0, \"CostPerShieldBlock\": 2.0, \"CostPerWrongSideShieldBlock\": 5.0 }"), Shipped));
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 13, \"DamageRandomPercent\": 40 }"), Shipped));
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 14, \"CostPerBlow\": 7.0 }"), Shipped));
        // the chain: a format-7 file's step 5c 10 and step 22 5 end at today's 8 and 5 (never at 3 / 7 / 2)
        var seven = ConfigFile.Read("{ \"ConfigVersion\": 7, \"CostPerBlow\": 10.0, \"CostPerWrongSideShieldBlock\": 5.0, \"CostPerShieldBlock\": 1.0 }");
        ConfigFile.Migrate(seven, Shipped);
        Assert.Equal(8, seven.Values["CostPerBlow"], 9);
        Assert.Equal(5, seven.Values["CostPerWrongSideShieldBlock"], 9);
        Assert.Equal(2, seven.Values["CostPerShieldBlock"], 9);
        // a format-8 file's 2 (step 43's default) → 5; a format-8 file's 7 was his own (step 46's 7 came in format 9): kept, no second note
        var eight = ConfigFile.Read("{ \"ConfigVersion\": 8, \"CostPerBlow\": 7.0, \"CostPerWrongSideShieldBlock\": 2.0 }");
        var eightNotes = ConfigFile.Migrate(eight, Shipped);
        Assert.Equal(7, eight.Values["CostPerBlow"], 9);
        Assert.Equal(5, eight.Values["CostPerWrongSideShieldBlock"], 9);
        Assert.Single(eightNotes, n => n.StartsWith("CostPerBlow", StringComparison.Ordinal));
        // on DESIGN's initial values (10 / 1 / 5) nothing of step 68 moves a file holding them
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 13, \"CostPerBlow\": 10.0, \"CostPerShieldBlock\": 1.0, \"CostPerWrongSideShieldBlock\": 5.0 }")));
    }
}

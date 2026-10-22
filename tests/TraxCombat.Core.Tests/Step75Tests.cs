using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 75 (Toni 2026-10-22: "the men in the shield wall still step forward even when on a standing order, they take a few steps to strike at an
/// enemy, then fall back"; AI_NOTES "Step 75", DESIGN decision 69) - A STANDING WALL NEVER LEAVES ITS SLOT: the settings (one on the player page, five
/// Advanced), the switches in order (the master switch first), the hold-type orders, the "at its place" rule for a Move (latched, its ways out), the
/// leash 0 and the tight tolerance, the walk back from the slack (at once, never while kicking), the summary line.
/// </summary>
public class Step75Tests
{
    private static readonly ParamDef[] Advanced =
    {
        SettingsSchema.ShieldWallStandFirmSlackMetres, SettingsSchema.ShieldWallStandFirmArrivedMetres, SettingsSchema.ShieldWallStandFirmStillMetresPerSecond,
        SettingsSchema.ShieldWallStandFirmPinFrame, SettingsSchema.ShieldWallStandFirmTightTolerance,
    };

    private static StandFirmRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return StandFirmRules.From(s);
    }

    /// <summary>A held man of a shield wall facing +y; his formation at (0, <paramref name="fromPlace"/>) against its order position (0, 0), its men moving
    /// <paramref name="speed"/> m/s.</summary>
    private static WallFacts Man(BraceOrderKind order, double fromPlace = 0, double speed = 0, double orderX = 0) => new WallFacts
    {
        Ai = true,
        HasFormation = true,
        Arrangement = WallArrangement.ShieldWall,
        Order = order,
        HasPositions = true,
        DirX = 0, DirY = 1,
        VelX = 0, VelY = speed,
        AvgSpeed = 3.5,
        Deviation = 1.2,
        HasFormationOrder = order == BraceOrderKind.Move,
        FormationX = orderX, FormationY = fromPlace,
        FormationOrderX = orderX, FormationOrderY = 0,
    };

    [Fact]
    public void The_switch_is_on_the_player_page_after_the_step_out_and_the_five_numbers_on_Advanced_after_the_kick_return()
    {
        var all = SettingsSchema.All.ToList();
        var sw = SettingsSchema.ShieldWallStandFirm;
        Assert.Equal(all.IndexOf(SettingsSchema.ShieldWallStepOutMetres) + 1, all.IndexOf(sw));
        Assert.Contains(sw, SettingsSchema.PlayerPage);
        Assert.Equal(SettingsPage.Player, sw.Group.Page);
        Assert.Equal(ParamType.Bool, sw.Type);
        Assert.Equal(ApplyTiming.Live, sw.Timing);
        Assert.Equal(1, sw.Default, 9); // on by default
        Assert.DoesNotMatch(@"[Ss]tep \d", sw.Description); // a player's words - no step numbers
        int first = all.IndexOf(SettingsSchema.ShieldWallKickReturnMetres) + 1;
        for (int i = 0; i < Advanced.Length; i++)
        {
            Assert.Equal(first + i, all.IndexOf(Advanced[i]));
            Assert.Same(SettingsSchema.ShieldWallKickReturnMetres.Group, Advanced[i].Group);
            Assert.Equal(SettingsPage.Advanced, Advanced[i].Group.Page);
            Assert.Equal(ApplyTiming.Live, Advanced[i].Timing);
        }
        var r = Rules();
        Assert.True(r.Enabled);
        Assert.Equal(0.3, r.SlackMetres, 6);
        Assert.Equal(2.0, r.ArrivedMetres, 6);
        Assert.Equal(0.5, r.StillMetresPerSecond, 6);
        Assert.True(r.PinFrame);
        Assert.True(r.TightTolerance);
    }

    [Fact]
    public void The_master_switch_first_then_the_hold_then_the_switch_live()
    {
        var on = Rules();
        Assert.Equal(StandFirmVerdict.Stands, StandFirmMath.Decide(in on, true, StandFirmVerdict.Stands));
        var mod = Rules(s => { s.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm); s.Set(SettingsSchema.ShieldWallHoldsSlots, false, SettingSources.Mcm); });
        Assert.Equal(StandFirmVerdict.ModOff, StandFirmMath.Decide(in mod, true, StandFirmVerdict.Stands));
        Assert.Equal("ModEnabled", mod.OffBecause);
        var hold = Rules(s => s.Set(SettingsSchema.ShieldWallHoldsSlots, false, SettingSources.Mcm));
        Assert.Equal(StandFirmVerdict.HoldOff, StandFirmMath.Decide(in hold, true, StandFirmVerdict.Stands));
        var off = Rules(s => s.Set(SettingsSchema.ShieldWallStandFirm, false, SettingSources.Mcm));
        Assert.False(off.Enabled);
        Assert.Equal(StandFirmVerdict.SwitchedOff, StandFirmMath.Decide(in off, true, StandFirmVerdict.Stands));
        Assert.Equal("ShieldWallStandFirm", off.OffBecause);
        Assert.StartsWith("OFF (ShieldWallStandFirm)", off.Describe(), StringComparison.Ordinal);
        Assert.Equal(StandFirmVerdict.NotHeld, StandFirmMath.Decide(in on, false, StandFirmVerdict.Stands));
        Assert.Equal(StandFirmVerdict.NotArrived, StandFirmMath.Decide(in on, true, StandFirmVerdict.NotArrived));
        Assert.False(on.SameAs(in off));
        Assert.Contains("never leaves his slot", on.Describe(), StringComparison.Ordinal);
        Assert.Contains("walked straight back to within 0.15 m", on.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void Only_a_stop_or_a_plain_move_is_a_hold_order()
    {
        var r = Rules();
        foreach (BraceOrderKind k in Enum.GetValues(typeof(BraceOrderKind)))
        {
            if (k == BraceOrderKind.Count) continue;
            bool hold = k == BraceOrderKind.Stop || k == BraceOrderKind.Move;
            Assert.Equal(hold, StandFirmMath.IsHoldOrder(k));
            var latch = new StandFirmLatch { Arrived = true };
            var v = StandFirmMath.Conditions(in r, Man(k), ref latch);
            if (!hold)
            {
                Assert.Equal(StandFirmVerdict.NotAHoldOrder, v); // advance (yours / the AI's), charges, follow, fall back, retreat: never
                Assert.False(latch.Arrived);
            }
        }
        // a stop stands at once - wherever the formation is, moving or not
        var l = default(StandFirmLatch);
        Assert.Equal(StandFirmVerdict.Stands, StandFirmMath.Conditions(in r, Man(BraceOrderKind.Stop, fromPlace: 30, speed: 3), ref l));
    }

    [Fact]
    public void A_move_stands_once_at_its_place_and_still_then_latched_until_a_new_place_or_pushed_far()
    {
        var r = Rules();
        var l = default(StandFirmLatch);
        // walking in: 10 m off, then 1.5 m off but still moving 1.2 m/s
        Assert.Equal(StandFirmVerdict.NotArrived, StandFirmMath.Conditions(in r, Man(BraceOrderKind.Move, 10, 1.2), ref l));
        Assert.Equal(StandFirmVerdict.NotArrived, StandFirmMath.Conditions(in r, Man(BraceOrderKind.Move, 1.5, 1.2), ref l));
        Assert.False(l.Arrived);
        // there and stopped
        Assert.Equal(StandFirmVerdict.Stands, StandFirmMath.Conditions(in r, Man(BraceOrderKind.Move, 1.5, 0.3), ref l));
        Assert.True(l.Arrived);
        // latched: the fighting line shuffles (1.5 m/s) and is pushed back 3.5 m - still standing (within 2 x 2 m)
        Assert.Equal(StandFirmVerdict.Stands, StandFirmMath.Conditions(in r, Man(BraceOrderKind.Move, 3.5, 1.5), ref l));
        // pushed 4.5 m - let go (the game walks it home as a line)
        Assert.Equal(StandFirmVerdict.NotArrived, StandFirmMath.Conditions(in r, Man(BraceOrderKind.Move, 4.5, 0), ref l));
        Assert.False(l.Arrived);
        // back at its place: stands again
        Assert.Equal(StandFirmVerdict.Stands, StandFirmMath.Conditions(in r, Man(BraceOrderKind.Move, 0.5, 0), ref l));
        // a NEW move 10 m to the side: the latch goes at once (the formation is 10 m from the new place)
        Assert.Equal(StandFirmVerdict.NotArrived, StandFirmMath.Conditions(in r, new WallFacts
        {
            Order = BraceOrderKind.Move, HasFormationOrder = true, FormationX = 0, FormationY = 0.5, FormationOrderX = 10, FormationOrderY = 0,
        }, ref l));
        Assert.False(l.Arrived);
        // no order position known: never
        var f = Man(BraceOrderKind.Move);
        f.HasFormationOrder = false;
        Assert.Equal(StandFirmVerdict.NotArrived, StandFirmMath.Conditions(in r, f, ref l));
        // another order clears the latch
        StandFirmMath.Conditions(in r, Man(BraceOrderKind.Move), ref l);
        Assert.True(l.Arrived);
        StandFirmMath.Conditions(in r, Man(BraceOrderKind.Advance), ref l);
        Assert.False(l.Arrived);
        // the numbers live: 5 m counts as at its place at ArrivedMetres 6
        var wide = Rules(s => s.Set(SettingsSchema.ShieldWallStandFirmArrivedMetres, 6.0, SettingSources.Mcm));
        var l2 = default(StandFirmLatch);
        Assert.Equal(StandFirmVerdict.Stands, StandFirmMath.Conditions(in wide, Man(BraceOrderKind.Move, 5, 0.2), ref l2));
    }

    [Fact]
    public void The_leash_is_0_and_the_tolerance_tight_only_while_he_stands_firm()
    {
        Assert.Equal(0, StandFirmMath.Leash(true, 1.0), 9);
        Assert.Equal(1.0, StandFirmMath.Leash(false, 1.0), 9);
        var r = Rules();
        Assert.Equal(0.3, StandFirmMath.Tolerance(in r, true, 1.2), 6);
        Assert.Equal(0.2, StandFirmMath.Tolerance(in r, true, 0.2), 6);
        Assert.Equal(1.2, StandFirmMath.Tolerance(in r, false, 1.2), 6);
        var loose = Rules(s => s.Set(SettingsSchema.ShieldWallStandFirmTightTolerance, false, SettingSources.Mcm));
        Assert.Equal(1.2, StandFirmMath.Tolerance(in loose, true, 1.2), 6);
        // a stepping man's leash at 0 is reined at once (step 42's LeashOff)
        var leash = new WallLeash { Free = true };
        Assert.Equal(LeashMove.LeashOff, LeashMath.Step(ref leash, StandFirmMath.Leash(true, 1.0), 0.6, true, 10));
        Assert.False(leash.Free);
    }

    [Fact]
    public void Out_of_place_from_the_slack_whatever_moved_him_walked_back_at_once_never_while_kicking()
    {
        // 0.4 m ahead (inside step 63's 1.25 m and step 68's 0.5 m): out of place standing firm, nobody's business otherwise
        var s = default(WallOutOfPlace);
        Assert.Equal(WallOutMove.None, WallPullMath.Step(ref s, 0, 0.4, true, false, 0.5, 10, 0.1, 0.5, false, 0));
        Assert.Equal(WallOutMove.Began, WallPullMath.Step(ref s, 0, 0.4, true, false, 0, 10, 0.1, 0, false, 0.3));
        Assert.True(s.StandFirm && !s.OwnKick && !s.AfterOwnKick);
        // walked back at once (no ShieldWallPullBackSeconds wait - it is 0 here)
        Assert.Equal(WallOutMove.PullStarted, WallPullMath.Step(ref s, 0, 0.38, true, false, 0, 10.1, 0.1, 0, false, 0.3));
        // still 0.2 m out: not back yet (half the slack 0.15)
        Assert.Equal(WallOutMove.None, WallPullMath.Step(ref s, 0, 0.2, true, false, 0, 10.2, 0.1, 0, false, 0.3));
        Assert.Equal(WallOutMove.Back, WallPullMath.Step(ref s, 0, 0.14, true, false, 0, 10.3, 0.1, 0, false, 0.3));
        Assert.False(s.Out || s.Pulling);

        // his own kick carries him 0.6 m: out (the kick lever), never walked while it plays, at once after
        var k = default(WallOutOfPlace);
        Assert.Equal(WallOutMove.Began, WallPullMath.Step(ref k, 0, 0.6, false, false, 0.5, 20, 0.1, 0.5, true, 0.3));
        Assert.True(k.StandFirm && k.OwnKick);
        Assert.Equal(WallOutMove.None, WallPullMath.Step(ref k, 0, 0.6, false, false, 0.5, 20.1, 0.1, 0.5, true, 0.3));
        Assert.Equal(WallOutMove.PullStarted, WallPullMath.Step(ref k, 0, 0.6, true, false, 0.5, 20.2, 0.1, 0.5, true, 0.3));
        // knocked down mid-walk: the walk pauses
        Assert.Equal(WallOutMove.PullStopped, WallPullMath.Step(ref k, 0, 0.5, false, true, 0.5, 20.3, 0.1, 0.5, false, 0.3));

        // standing firm no more mid-spell (a new order): the old rules decide the way back (within step 63's 0.5 m)
        var m = default(WallOutOfPlace);
        WallPullMath.Step(ref m, 0, 0.45, true, false, 0, 30, 0.1, 0, false, 0.3);
        Assert.Equal(WallOutMove.Back, WallPullMath.Step(ref m, 0, 0.45, true, false, 0, 30.1, 0.1, 0, false, 0));

        // the 10-argument step (step 68) unchanged: 0.4 m with no kick = nothing
        var o = default(WallOutOfPlace);
        Assert.Equal(WallOutMove.None, WallPullMath.Step(ref o, 0, 0.4, true, false, 0.5, 10, 0.1, 0.5, false));
    }

    [Fact]
    public void A_running_sprint_ends_with_its_own_name()
    {
        Assert.Equal("his shield wall stands firm (step 75)", SprintMath.EndName(SprintEnd.StandFirm));
        Assert.True((int)SprintEnd.StandFirm < (int)SprintEnd.Count);
    }

    [Fact]
    public void The_summary_line_compares_ON_and_OFF_and_names_every_lever()
    {
        var r = Rules();
        var st = new StandFirmStats();
        Assert.EndsWith("no AI man stood in a held shield wall / square / circle on a stop or a move at its place this battle", st.SummaryLine(in r), StringComparison.Ordinal);
        st.EverOn = true;
        st.MenStood = 12;
        for (int i = 0; i < 100; i++) st.AddSample(true, i < 2 ? 0.7 : 0.1, contact: true);  // 2% beyond 0.5 m, none beyond 1 m
        for (int i = 0; i < 100; i++) st.AddSample(false, i < 20 ? 1.1 : 0.2, contact: true); // 20% beyond 1 m
        for (int i = 0; i < 40; i++) st.AddStrike(true, fromSlot: i < 38);
        for (int i = 0; i < 45; i++) st.AddStrike(false, fromSlot: i < 25);
        st.Refuse(StandFirmLever.StepOut);
        st.Refuse(StandFirmLever.Sprint);
        st.Began(afterImpact: true, ownKick: false);
        st.Began(afterImpact: false, ownKick: true);
        st.Back.Add(0.4);
        st.LetGo(WallVerdict.Breaking);
        st.LetGo(WallVerdict.FarBehind);
        st.LetGo(WallVerdict.ChargeOrder);
        st.AddLook(StandFirmVerdict.NotArrived);
        st.FramesPinned = 300;
        string line = st.SummaryLine(in r);
        Assert.StartsWith("wall stands firm (step 75 - ShieldWallStandFirm ON now, slack 0.30 m): held 50 man-s (in contact 50 man-s) by 12 men", line, StringComparison.Ordinal);
        Assert.Contains("IN CONTACT ahead of the slot - ON more than 0.5 m 2.0%, more than 1 m 0.0%, mean 0.11 m, the farthest 0.70 m (100 samples)", line, StringComparison.Ordinal);
        Assert.Contains("| OFF more than 0.5 m 20.0%, more than 1 m 20.0%, mean 0.38 m, the farthest 1.10 m (100 samples)", line, StringComparison.Ordinal);
        Assert.Contains("strikes - ON from the slot 38, out of it 2 (48.0 a man-minute in contact); OFF from the slot 25, out of it 20 (54.0 a man-minute in contact)", line, StringComparison.Ordinal);
        Assert.Contains("levers refused - the step out (leash) 1, a sprint 1, the advance pace 0, his own kick / bash or a punish press 1, pushed / the game's approach / a blow 1 (after an impact 1)", line, StringComparison.Ordinal);
        Assert.Contains("walked back 2 times, back in 0.40 s (n 1); the engine's walk target pinned to the slot 300 times", line, StringComparison.Ordinal);
        Assert.Contains("let go while standing firm: the wall broke (its morale) 1, far behind (rejoining) 1, other (a new order, a charge ...) 1", line, StringComparison.Ordinal);
        Assert.Contains("a move not at its place yet 1", line, StringComparison.Ordinal);
        var off = Rules(s => s.Set(SettingsSchema.ShieldWallStandFirm, false, SettingSources.Mcm));
        Assert.StartsWith("wall stands firm (step 75 - OFF (ShieldWallStandFirm) now, slack 0.30 m)", st.SummaryLine(in off), StringComparison.Ordinal);
    }
}

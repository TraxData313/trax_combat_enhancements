using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 99 (Toni 2026-11-06: "can you make it so that troops on the two sides of the shield wall are facing the flanks not forward?"; AI_NOTES
/// "Step 99", DESIGN decision 90) - THE ENDS OF A STANDING SHIELD WALL FACE THE FLANKS: the settings (one on the player page, three Advanced), which men
/// are ends (the outermost files of a rank, a short rank none), the width gate, the angles (the front corner 45, the rest 90), the world direction (the
/// game's TransformToParentUnitF), the enemy in front, the verdict in gate order, the angle stats and the summary line.
/// </summary>
public class Step99Tests
{
    private static readonly ParamDef[] Advanced =
    {
        SettingsSchema.ShieldWallFlankFiles, SettingsSchema.ShieldWallFlankFrontCornerDegrees, SettingsSchema.ShieldWallFlankReleaseMetres,
    };

    private static WallFlankRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return WallFlankRules.From(s);
    }

    /// <summary>A man whose slot is (0, 0), the wall facing +y.</summary>
    private static WallFacts Slot() => new WallFacts { HasPositions = true, SlotX = 0, SlotY = 0, DirX = 0, DirY = 1 };

    [Fact]
    public void The_switch_is_on_the_player_page_after_stand_firm_and_the_three_numbers_on_Advanced_after_the_stand_firm_ones()
    {
        var all = SettingsSchema.All.ToList();
        var sw = SettingsSchema.ShieldWallFlanksFaceOut;
        Assert.Equal(all.IndexOf(SettingsSchema.ShieldWallStandFirm) + 1, all.IndexOf(sw));
        Assert.Contains(sw, SettingsSchema.PlayerPage);
        Assert.Equal(ParamType.Bool, sw.Type);
        Assert.Equal(ApplyTiming.Live, sw.Timing);
        Assert.Equal(1, sw.Default, 9);
        Assert.DoesNotMatch(@"[Ss]tep \d", sw.Description);
        int first = all.IndexOf(SettingsSchema.ShieldWallStandFirmTightTolerance) + 1;
        for (int i = 0; i < Advanced.Length; i++)
        {
            Assert.Same(Advanced[i], all[first + i]);
            Assert.Equal(SettingsPage.Advanced, Advanced[i].Group.Page);
            Assert.Equal(ApplyTiming.Live, Advanced[i].Timing);
        }
        Assert.Equal(ParamType.Int, SettingsSchema.ShieldWallFlankFiles.Type);
        Assert.Equal(1, SettingsSchema.ShieldWallFlankFiles.Default, 9);
        Assert.Equal(1, SettingsSchema.ShieldWallFlankFiles.Min, 9);
        Assert.Equal(3, SettingsSchema.ShieldWallFlankFiles.Max, 9);
        Assert.Equal(45, SettingsSchema.ShieldWallFlankFrontCornerDegrees.Default, 9);
        Assert.Equal(90, SettingsSchema.ShieldWallFlankFrontCornerDegrees.Max, 9);
        Assert.Equal(4, SettingsSchema.ShieldWallFlankReleaseMetres.Default, 9);
        Assert.Equal(10, SettingsSchema.ShieldWallFlankReleaseMetres.Max, 9);
    }

    [Fact]
    public void The_switches_go_in_order_with_the_master_switch_first()
    {
        var on = Rules();
        Assert.True(on.Enabled);
        Assert.Null(on.OffBecause);
        Assert.Equal(1, on.Files);
        Assert.Equal(45, on.CornerDegrees, 9);
        Assert.Equal(4, on.ReleaseMetres, 9);
        Assert.StartsWith("ON (ShieldWallFlanksFaceOut)", on.Describe(), StringComparison.Ordinal);

        var off = Rules(s => s.Set(SettingsSchema.ShieldWallFlanksFaceOut, false, SettingSources.Mcm));
        Assert.Equal("ShieldWallFlanksFaceOut", off.OffBecause);
        var hold = Rules(s => { s.Set(SettingsSchema.ShieldWallFlanksFaceOut, false, SettingSources.Mcm); s.Set(SettingsSchema.ShieldWallHoldsSlots, false, SettingSources.Mcm); });
        Assert.Equal("ShieldWallHoldsSlots", hold.OffBecause);
        var mod = Rules(s => { s.Set(SettingsSchema.ShieldWallHoldsSlots, false, SettingSources.Mcm); s.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm); });
        Assert.Equal("ModEnabled", mod.OffBecause);
        Assert.StartsWith("OFF (ModEnabled)", mod.Describe(), StringComparison.Ordinal);

        Assert.Equal(WallFlankVerdict.ModOff, WallFlankMath.Decide(in mod, true, false, WallArrangement.ShieldWall, true, true, 20, WallFlankSide.Left, false));
        Assert.Equal(WallFlankVerdict.HoldOff, WallFlankMath.Decide(in hold, true, false, WallArrangement.ShieldWall, true, true, 20, WallFlankSide.Left, false));
        Assert.Equal(WallFlankVerdict.SwitchedOff, WallFlankMath.Decide(in off, true, false, WallArrangement.ShieldWall, true, true, 20, WallFlankSide.Left, false));
        Assert.False(off.SameAs(in on));
        Assert.True(Rules().SameAs(in on));
    }

    [Fact]
    public void The_verdict_needs_a_held_man_of_a_standing_shield_wall_wide_enough_at_an_end_with_no_enemy_in_front()
    {
        var r = Rules();
        WallFlankVerdict D(bool held = true, bool stepping = false, WallArrangement a = WallArrangement.ShieldWall, bool stands = true, bool placed = true,
            int files = 20, WallFlankSide side = WallFlankSide.Right, bool front = false) => WallFlankMath.Decide(in r, held, stepping, a, stands, placed, files, side, front);

        Assert.Equal(WallFlankVerdict.Turned, D());
        Assert.Equal(WallFlankVerdict.NotHeld, D(held: false));
        Assert.Equal(WallFlankVerdict.SteppingOut, D(stepping: true));
        Assert.Equal(WallFlankVerdict.NotShieldWall, D(a: WallArrangement.Square));
        Assert.Equal(WallFlankVerdict.NotShieldWall, D(a: WallArrangement.Circle));
        Assert.Equal(WallFlankVerdict.NotShieldWall, D(a: WallArrangement.Other)); // a plain line keeps its front (Toni: shield wall only)
        Assert.Equal(WallFlankVerdict.NotStanding, D(stands: false));               // advancing / moving / falling back: everyone forward
        Assert.Equal(WallFlankVerdict.Unplaced, D(placed: false));
        Assert.Equal(WallFlankVerdict.Narrow, D(files: 4));
        Assert.Equal(WallFlankVerdict.Turned, D(files: 5));                         // 2 x 1 + 3
        Assert.Equal(WallFlankVerdict.NotAnEnd, D(side: WallFlankSide.None));
        Assert.Equal(WallFlankVerdict.FrontEnemy, D(front: true));

        Assert.True(WallFlankMath.MayTurn(in r, true, false, WallArrangement.ShieldWall, true));
        Assert.False(WallFlankMath.MayTurn(in r, true, false, WallArrangement.ShieldWall, false));
        Assert.False(WallFlankMath.MayTurn(in r, false, false, WallArrangement.ShieldWall, true));
        Assert.False(WallFlankMath.MayTurn(in r, true, false, WallArrangement.Square, true));
    }

    [Fact]
    public void The_ends_are_the_outermost_files_of_a_rank_and_a_short_rank_has_none()
    {
        // one file a side: nobody to his left = the left end
        Assert.Equal(WallFlankSide.Left, WallFlankMath.Side(0, 1, 1));
        Assert.Equal(WallFlankSide.Right, WallFlankMath.Side(1, 0, 1));
        Assert.Equal(WallFlankSide.None, WallFlankMath.Side(1, 1, 1));
        Assert.Equal(WallFlankSide.None, WallFlankMath.Side(0, 0, 1));             // alone in his rank
        // two files: the second man from the end turns too
        Assert.Equal(WallFlankSide.Left, WallFlankMath.Side(1, 2, 2));
        Assert.Equal(WallFlankSide.None, WallFlankMath.Side(2, 2, 2));
        Assert.Equal(WallFlankSide.None, WallFlankMath.Side(1, 1, 2));             // a short rank of three
        Assert.Equal(WallFlankSide.Right, WallFlankMath.Side(3, 0, 3));
        Assert.True(WallFlankMath.WideEnough(9, 3) && !WallFlankMath.WideEnough(8, 3));
    }

    [Fact]
    public void The_front_corner_turns_45_and_the_ranks_behind_90_toward_their_own_side()
    {
        var r = Rules();
        Assert.Equal(45, WallFlankMath.Degrees(in r, 0), 9);
        Assert.Equal(90, WallFlankMath.Degrees(in r, 1), 9);
        Assert.Equal(90, WallFlankMath.Degrees(in r, 5), 9);

        // the wall faces +y: its right is +x (the game's Vec2.Side through TransformToParentUnitF), its left -x
        WallFlankMath.Outward(0, 1, WallFlankSide.Left, 90, out double lx, out double ly);
        Assert.Equal(-1, lx, 9);
        Assert.Equal(0, ly, 9);
        WallFlankMath.Outward(0, 1, WallFlankSide.Right, 90, out double rx, out double ry);
        Assert.Equal(1, rx, 9);
        Assert.Equal(0, ry, 9);
        WallFlankMath.Outward(0, 1, WallFlankSide.Left, 45, out double cx, out double cy);
        Assert.Equal(-Math.Sqrt(0.5), cx, 9);
        Assert.Equal(Math.Sqrt(0.5), cy, 9);
        Assert.Equal(45, WallFlankMath.AngleDegrees(cx, cy, 0, 1), 6);
        // a wall facing +x: its right is -y (the game's (y, -x)); not normalised in, normalised out
        WallFlankMath.Outward(2, 0, WallFlankSide.Right, 90, out double ex, out double ey);
        Assert.Equal(0, ex, 9);
        Assert.Equal(-1, ey, 9);
        // 0 degrees = forward; no side = the wall's direction
        WallFlankMath.Outward(0, 1, WallFlankSide.Left, 0, out double fx, out double fy);
        Assert.Equal(0, fx, 9);
        Assert.Equal(1, fy, 9);
        WallFlankMath.Outward(0, 1, WallFlankSide.None, 90, out double nx, out double ny);
        Assert.Equal(0, nx, 9);
        Assert.Equal(1, ny, 9);

        var r90 = Rules(s => s.Set(SettingsSchema.ShieldWallFlankFrontCornerDegrees, 90.0, SettingSources.Mcm));
        Assert.Equal(90, WallFlankMath.Degrees(in r90, 0), 9);
    }

    [Fact]
    public void An_enemy_close_in_front_releases_and_one_on_the_flank_does_not()
    {
        var f = Slot();
        Assert.True(WallFlankMath.EnemyInFront(in f, 0, 3, 4));       // straight ahead, 3 m
        Assert.True(WallFlankMath.EnemyInFront(in f, -1.5, 2, 4));    // ahead more than aside
        Assert.False(WallFlankMath.EnemyInFront(in f, 0, 5, 4));      // too far
        Assert.False(WallFlankMath.EnemyInFront(in f, -3, 1, 4));     // on his flank - he is facing it
        Assert.False(WallFlankMath.EnemyInFront(in f, 0, -2, 4));     // behind
        Assert.False(WallFlankMath.EnemyInFront(in f, 0, 1, 0));      // 0 = never
        Assert.False(WallFlankMath.EnemyInFront(in f, double.NaN, 1, 4));
    }

    [Fact]
    public void The_stats_say_whether_the_engine_turned_them()
    {
        var st = new WallFlankStats();
        var r = Rules();
        Assert.Contains("no end man of a standing shield wall was turned this battle", st.SummaryLine(in r));
        st.EverOn = true;
        st.Turn(true, false);
        st.Turn(true, true);
        st.Turn(false, false);
        st.AddAngle(false, 4, 88);
        st.AddAngle(false, 12, 80);
        st.AddAngle(false, 70, 20);
        st.AddAngle(true, 2, 44);
        st.Release(WallFlankVerdict.FrontEnemy);
        st.Release(WallFlankVerdict.NotStanding);
        st.TurnedSamples = 10;
        Assert.Equal(2, st.MenTurned);
        Assert.Equal(1, st.MenCorner);
        Assert.Equal(3, st.Turns);
        Assert.Equal(12.5, st.MedianOffTarget(false), 9);
        Assert.Equal(2.0 / 3, st.ShareWithin(false, 20), 9);
        Assert.Equal(2.5, st.MedianOffTarget(true), 9);
        Assert.True(double.IsNaN(new WallFlankStats().MedianOffTarget(true)));
        var line = st.SummaryLine(in r);
        Assert.StartsWith("wall flanks (step 99 - ShieldWallFlanksFaceOut ON now, 1 file(s) a side, the front corner 45 deg, released by an enemy within 4.0 m in front): end men turned 2 (front corners 1), turns 3, turned 5 man-s", line, StringComparison.Ordinal);
        Assert.Contains("flank men (90 deg) off the asked: median 13 deg", line);
        Assert.Contains("turned forward again: the wall moved 1, an enemy in front 1;", line);
    }
}

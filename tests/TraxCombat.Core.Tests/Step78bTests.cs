using System;
using System.Collections.Generic;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 78b (Toni's 225 v 229 battle 2026-10-22 16:10: the enemy's 69 m of recruits went round both ends of his ~31 m line; AI_NOTES "Step 78b", DESIGN
/// decision 73) - NO WRAPPING: the wrap's switch and the wing's bend, THE WING (the bent front, standing off the end, walked back, round the end to his
/// side, never a wrap with the switch off, a unit ordered there), THE GAP FROM THE MEN (bins, the plane - since step 78c only the natural-gap diagnostic
/// uses it), the press's summary line. Step 78c removed the lull (its clock, its lines, its grace, its format-17 move) and their tests.
/// </summary>
public class Step78bTests
{
    // ------------------------------------------------------------------ settings and the migration

    [Fact]
    public void The_wraps_switch_and_the_wings_bend_sit_after_the_press_and_are_live()
    {
        var all = SettingsSchema.All.ToList();
        int p = all.IndexOf(SettingsSchema.PressEndSlackMetres);
        Assert.Equal(p + 1, all.IndexOf(SettingsSchema.PressWrapAllowed));
        Assert.Equal(p + 2, all.IndexOf(SettingsSchema.PressWingBendDegrees));
        Assert.Same(SettingsSchema.CourageGroup, SettingsSchema.PressWrapAllowed.Group);
        Assert.DoesNotContain(SettingsSchema.PressWrapAllowed, SettingsSchema.PlayerPage);
        var s = new TraxSettings();
        var pr = PressRules.From(s);
        Assert.False(pr.WrapAllowed);
        Assert.Equal(30, pr.WingBendDegrees, 9);
        s.Set(SettingsSchema.PressWrapAllowed, true, SettingSources.Mcm);
        Assert.True(PressRules.From(s).WrapAllowed);
        Assert.Contains("NEVER round an end", pr.Describe());
    }

    // ------------------------------------------------------------------ the gap from the men

    [Fact]
    public void The_gap_is_measured_from_the_nearest_men_in_bins_along_the_contact()
    {
        // A at along ≤ 1 (front 1), B from along 0 (meshed by 1 m) over 10 m; two of A's men 3 m deeper into B in one bin
        var aAl = new List<double>();
        var aLa = new List<double>();
        var bAl = new List<double>();
        var bLa = new List<double>();
        for (int f = 0; f < 10; f++)
            for (int k = 0; k < 2; k++)
            {
                aAl.Add(1 - k);
                aLa.Add(f - 4.5);
                bAl.Add(0 + k);
                bLa.Add(f - 4.5);
            }
        aAl.Add(4);
        aLa.Add(-4.5);
        var maxA = new double[64];
        var minB = new double[64];
        var g = new double[64];
        var m = new double[64];
        var fa = new double[64];
        var fb = new double[64];
        int bins = RhythmMath.MeasureGap(aAl.ToArray(), aLa.ToArray(), aAl.Count, bAl.ToArray(), bLa.ToArray(), bAl.Count, maxA, minB, g, m, fa, fb,
            out double mean, out double q10, out double qh, out double q90, out double plane, out double frontA, out double frontB);
        Assert.Equal(6, bins);                       // 10 m in 2 m bins - the -4.5 / -3.5 men share one with the -5..-4 bin's edge
        Assert.Equal(-1, q90, 6);                    // most of the contact meshed by 1 m
        Assert.True(q10 < -1.5);                     // the bin with the deep men
        Assert.Equal(0.5, plane, 6);                 // the contact plane - the middle of the mesh
        Assert.Equal(1, frontA, 6);
        Assert.Equal(0, frontB, 6);
        Assert.True(mean < -1);
    }

    // ------------------------------------------------------------------ the wing

    private static PressRules Press(bool wrap = false, double bend = 30) =>
        new PressRules(true, true, endSlackMetres: 1, wrapAllowed: wrap, wingBendDegrees: bend);

    [Fact]
    public void Past_an_end_the_front_bends_back_and_a_wing_stands_off_the_end()
    {
        var r = Press();
        // a line from l −10 to +10, 1 m deep
        Assert.Equal(0, PressMath.WingPlane(in r, 0.8), 9);                          // within the slack: the straight front
        Assert.Equal(Math.Tan(Math.PI / 6) * 4, PressMath.WingPlane(in r, 5), 9);     // 5 m past: 2.31 m in front
        double fd = PressMath.FrontDistance(in r, 3, 15, -10, 10, out int side);
        Assert.Equal(1, side);
        Assert.Equal((3 - Math.Tan(Math.PI / 6) * 4) * Math.Cos(Math.PI / 6), fd, 9);
        Assert.Equal(0.4, PressMath.FrontDistance(in r, 0.4, 5, -10, 10, out side), 9);
        Assert.Equal(0, side);
        // in front of the bent front: a WING held there (clamped), sideways outward clamped too while his unit is in contact
        var o = PressMath.Decide(in r, 3, 15, -10, 10, 1, false, true, 20, false, false, false, true);
        Assert.Equal(PressAction.Wing, o.Action);
        Assert.True(o.ClampFront);
        Assert.Equal(1, o.Wing);
        Assert.Equal(1, o.Side);
        o = PressMath.Decide(in r, 3, -15, -10, 10, 1, false, true, 20, false, false, false, true);
        Assert.Equal(PressAction.Wing, o.Action);
        Assert.Equal(-1, o.Wing);
        Assert.Equal(-1, o.Side);
        // level with the line's front 5 m past its end - forward of the bent front: walked straight back to it (+0.3)
        o = PressMath.Decide(in r, 0.5, 15, -10, 10, 1, false, true, 20, false, false, false, true);
        Assert.Equal(PressAction.Push, o.Action);
        Assert.Equal(1, o.Wing);
        Assert.Equal(Math.Tan(Math.PI / 6) * 4 + PressMath.PushToMetres, o.ToD, 9);
        Assert.Equal(15, o.ToL, 9);
        // behind the line beside its end: round to his side - to the bent front there, not level with the flank
        o = PressMath.Decide(in r, -5, 15, -10, 10, 1, false, true, 20, false, false, false, true);
        Assert.Equal(PressAction.Return, o.Action);
        Assert.Equal(Math.Tan(Math.PI / 6) * 4 + PressMath.PushToMetres, o.ToD, 9);
        // behind the middle of the line: round the nearest end (step 77), then forward in front of the bent front there
        o = PressMath.Decide(in r, -5, 6, -10, 10, 1, false, false, 0, false, false, false, true);
        Assert.Equal(PressAction.Return, o.Action);
        Assert.Equal(13, o.ToL, 9);
        PressMath.ReturnPlace(in r, -5, 13.5, -10, 10, 1, out double toD, out _);
        Assert.Equal(Math.Max(PressMath.ReturnFrontMetres, Math.Tan(Math.PI / 6) * 2.5 + PressMath.PushToMetres), toD, 9);
        // within the ends nothing changes: step 77's clamp
        o = PressMath.Decide(in r, 0.3, 4, -10, 10, 1, false, true, 20, false, false, false, true);
        Assert.Equal(PressAction.Clamp, o.Action);
        Assert.Equal(0, o.Wing);
        // bend 0 = step 77's straight front (and no side clamp for a front-rank man)
        var flat = Press(bend: 0);
        o = PressMath.Decide(in flat, 0.5, 15, -10, 10, 1, false, true, 20, false, false, false, true);
        Assert.Equal(PressAction.Clamp, o.Action);
        Assert.Equal(0, o.Side);
    }

    [Fact]
    public void No_wrap_with_the_switch_off_a_unit_ordered_there_goes_there()
    {
        var off = Press();
        Assert.False(PressMath.ManWraps(in off, true, 30));
        // Toni's battle: the enemy's 69 m of recruits on his 31 m line - a wing man 19 m past an end, with his mass, past the front
        var o = PressMath.Decide(in off, -1, 34.5, -15.5, 15.5, 1, false, true, 30, false, false, false, true);
        Assert.NotEqual(PressAction.Wrap, o.Action);
        Assert.Equal(PressAction.Push, o.Action);
        Assert.Equal(Math.Tan(Math.PI / 6) * 18 + PressMath.PushToMetres, o.ToD, 9);   // 10.7 m in front: a refused flank, standing off
        o = PressMath.Decide(in off, -8, 34.5, -15.5, 15.5, 1, false, true, 30, false, false, false, true);
        Assert.Equal(PressAction.Return, o.Action);                                       // never left behind as a wrap
        // the switch on = step 77's wrap
        var on = Press(wrap: true);
        Assert.Equal(PressAction.Wrap, PressMath.Decide(in on, -1, 34.5, -15.5, 15.5, 1, false, true, 30, false, false, false, true).Action);
        // a unit ORDERED there (a move): free beside the line and behind it - the order's own place
        Assert.Equal(PressAction.Ordered, PressMath.Decide(in off, -1, 34.5, -15.5, 15.5, 1, false, true, 30, false, false, false, true, ordered: true).Action);
        Assert.Equal(PressAction.Ordered, PressMath.Decide(in off, -8, 3, -15.5, 15.5, 1, false, false, 0, false, false, false, true, ordered: true).Action);
        // ... but never through the line's body: in front of the line within its ends the clamp holds, in its body he is pushed out
        Assert.Equal(PressAction.Clamp, PressMath.Decide(in off, 0.3, 3, -15.5, 15.5, 1, false, false, 0, false, false, false, true, ordered: true).Action);
        Assert.Equal(PressAction.Push, PressMath.Decide(in off, -1, 3, -15.5, 15.5, 1, false, false, 0, false, false, false, true, ordered: true).Action);
    }

    // ------------------------------------------------------------------ the summary lines

    [Fact]
    public void The_summary_lines()
    {
        var p = new PressStats();
        p.Actions[1, (int)PressAction.Wing] = 120;
        p.WingMen = 30;
        p.WingPastMax = 18.5;
        p.WingPushedMen = 4;
        var pl = new List<string>();
        p.AddSummaryLines(pl, Press(), new PressLines());
        Assert.Equal(3, pl.Count);
        Assert.Contains("NO WRAPPING: the wrap off (PressWrapAllowed)", pl[2]);
        Assert.Contains("WINGS held at an end (looks) ON 120 | OFF 0 by 30 men (the farthest 18.5 m past an end), walked back to the bent front 4 men", pl[2]);
        Assert.Contains("round the end with his mass 0 (0 with the wrap off)", pl[2]);
    }
}

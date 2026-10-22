using System;
using System.Collections.Generic;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 78b (Toni's 225 v 229 battle 2026-10-22 16:10: "between the two fronts [a lull], never, not for a sec" - 2 lulls in 435 s, the rotation kept the
/// front men's bars up, the fronts meshed; the enemy's 69 m of recruits went round both ends of his ~31 m line; AI_NOTES "Step 78b", DESIGN decision
/// 73) - THE LULL THAT SHOWS AND NO WRAPPING: the 16 settings and the format-17 migration, THE BREATHER CLOCK (its roll by the pair's order, the whole
/// unit's bar - the rotation cannot hide tiredness -, the blows slowed, the spent trigger), THE GAP FROM THE MEN (bins, the plane, the lines from a
/// meshed fight, opened, stepped back while it does not hold), the charge order's grace, THE WING (the bent front, standing off the end, walked back,
/// round the end to his side, never a wrap with the switch off, a unit ordered there), the summary lines.
/// </summary>
public class Step78bTests
{
    private static readonly ParamDef[] Lull =
    {
        SettingsSchema.LullClockChargeMinSeconds, SettingsSchema.LullClockChargeMaxSeconds, SettingsSchema.LullClockAdvanceMinSeconds,
        SettingsSchema.LullClockAdvanceMaxSeconds, SettingsSchema.LullClockHoldMinSeconds, SettingsSchema.LullClockHoldMaxSeconds,
        SettingsSchema.LullClockChargePercent, SettingsSchema.LullClockAdvancePercent, SettingsSchema.LullClockHoldPercent,
        SettingsSchema.LullAttackRateFallenPercent, SettingsSchema.LullUnitDepthMetres, SettingsSchema.LullChargeGraceSeconds, SettingsSchema.LullGapMetres,
        SettingsSchema.LullMaxBackMetres,
    };

    private sealed class Fixed : IRandomSource
    {
        public double V;

        public double NextDouble() => V;
    }

    private static int A => FearMath.UnitKey(0, 1);
    private static int B => FearMath.UnitKey(1, 2);

    private static RhythmRules Rules(double minContact = 10, int rateFallen = 60, double grace = 3) =>
        new RhythmRules(true, true, true, true, lullMinContactSeconds: minContact, attackRateFallenPercent: rateFallen, chargeGraceSeconds: grace);

    // ------------------------------------------------------------------ settings and the migration

    [Fact]
    public void The_sixteen_settings_sit_after_their_systems_and_are_live()
    {
        var all = SettingsSchema.All.ToList();
        int l = all.IndexOf(SettingsSchema.LullRotates) + 1;
        for (int i = 0; i < Lull.Length; i++)
        {
            Assert.Equal(l + i, all.IndexOf(Lull[i]));
            Assert.Same(SettingsSchema.CourageGroup, Lull[i].Group);
            Assert.Equal(ApplyTiming.Live, Lull[i].Timing);
        }
        int p = all.IndexOf(SettingsSchema.PressEndSlackMetres);
        Assert.Equal(p + 1, all.IndexOf(SettingsSchema.PressWrapAllowed));
        Assert.Equal(p + 2, all.IndexOf(SettingsSchema.PressWingBendDegrees));
        Assert.Same(SettingsSchema.CourageGroup, SettingsSchema.PressWrapAllowed.Group);
        Assert.DoesNotContain(SettingsSchema.PressWrapAllowed, SettingsSchema.PlayerPage);
        // DESIGN's initial values (the tests' defaults)
        var s = new TraxSettings();
        var r = RhythmRules.From(s);
        Assert.Equal(30, r.ClockChargeMinSeconds, 9);
        Assert.Equal(60, r.ClockChargeMaxSeconds, 9);
        Assert.Equal(20, r.ClockHoldMinSeconds, 9);
        Assert.Equal(40, r.ClockHoldMaxSeconds, 9);
        Assert.Equal(55, r.ClockChargePercent);
        Assert.Equal(65, r.ClockHoldPercent);
        Assert.Equal(60, r.AttackRateFallenPercent);
        Assert.Equal(8, r.UnitDepthMetres, 9);
        Assert.Equal(3, r.ChargeGraceSeconds, 9);
        Assert.Equal(3.5, r.GapMetres, 9);
        Assert.Equal(8, r.MaxBackMetres, 9);
        var pr = PressRules.From(s);
        Assert.False(pr.WrapAllowed);
        Assert.Equal(30, pr.WingBendDegrees, 9);
        // live
        s.Set(SettingsSchema.LullGapMetres, 5.0, SettingSources.Mcm);
        s.Set(SettingsSchema.PressWrapAllowed, true, SettingSources.Mcm);
        Assert.Equal(5, RhythmRules.From(s).GapMetres, 9);
        Assert.True(PressRules.From(s).WrapAllowed);
        // a max below its min is the min
        Assert.Equal(40, new RhythmRules(true, true, true, true, clockChargeMinSeconds: 40, clockChargeMaxSeconds: 10).ClockChargeMaxSeconds, 9);
        Assert.Contains("THE BREATHER CLOCK", r.Describe());
        Assert.Contains("CLEAR GAP of 3.5 m", r.Describe());
        Assert.Contains("NEVER round an end", pr.Describe());
    }

    /// <summary>defaults.json's step-78b recover points (the tests run on DESIGN's initial 10 / 15 / 20).</summary>
    private static double Shipped(ParamDef p) =>
        p == SettingsSchema.LullRecoverChargePercent ? 20 : p == SettingsSchema.LullRecoverAdvancePercent ? 25 : p == SettingsSchema.LullRecoverHoldPercent ? 30 : p.Default;

    [Fact]
    public void Format_17_moves_the_untouched_recover_points_once_and_keeps_a_tuned_value()
    {
        Assert.Equal(17, ConfigFile.FormatVersion);
        var read = ConfigFile.Read("{ \"ConfigVersion\": 16, \"LullRecoverChargePercent\": 10, \"LullRecoverAdvancePercent\": 15, \"LullRecoverHoldPercent\": 20, \"PressWrapMarginMetres\": 6.0 }");
        var notes = ConfigFile.Migrate(read, Shipped);
        Assert.Equal(3, notes.Count);
        Assert.StartsWith("LullRecoverChargePercent: 10 → 20 (format 16 → 17, the old default; step 78b", notes[0], StringComparison.Ordinal);
        Assert.Equal(20, read.Values["LullRecoverChargePercent"], 9);
        Assert.Equal(25, read.Values["LullRecoverAdvancePercent"], 9);
        Assert.Equal(30, read.Values["LullRecoverHoldPercent"], 9);
        Assert.Equal(6, read.Values["PressWrapMarginMetres"], 9);   // the wrap's margin untouched - its own switch (a new key) is off
        var mine = ConfigFile.Read("{ \"ConfigVersion\": 16, \"LullRecoverChargePercent\": 12, \"LullRecoverHoldPercent\": 20 }");
        var kept = ConfigFile.Migrate(mine, Shipped);
        Assert.Equal(2, kept.Count);
        Assert.StartsWith("LullRecoverChargePercent: 12 kept - your own value (format 16 → 17; the new default 20", kept[0], StringComparison.Ordinal);
        Assert.Equal(12, mine.Values["LullRecoverChargePercent"], 9);
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 17, \"LullRecoverChargePercent\": 10 }"), Shipped));
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 16, \"LullRecoverChargePercent\": 10 }"), q => q.Default)); // DESIGN's initial: nothing moves
    }

    // ------------------------------------------------------------------ the clock

    [Fact]
    public void The_clock_rolls_by_the_more_aggressive_order_and_fires_on_worn_units_or_slowed_blows()
    {
        var r = Rules();
        Assert.Equal(BraceOrder.Charge, RhythmMath.PairGroup(BraceOrder.Hold, BraceOrder.Charge));
        Assert.Equal(BraceOrder.Advance, RhythmMath.PairGroup(BraceOrder.Advance, BraceOrder.Hold));
        Assert.Equal(BraceOrder.Hold, RhythmMath.PairGroup(BraceOrder.Hold, BraceOrder.Hold));
        Assert.Equal(30, RhythmMath.ClockSeconds(in r, BraceOrder.Charge, 0), 9);
        Assert.Equal(45, RhythmMath.ClockSeconds(in r, BraceOrder.Charge, 0.5), 9);
        Assert.Equal(60, RhythmMath.ClockSeconds(in r, BraceOrder.Charge, 1), 9);
        Assert.Equal(37.5, RhythmMath.ClockSeconds(in r, BraceOrder.Advance, 0.5), 9);
        Assert.Equal(30, RhythmMath.ClockSeconds(in r, BraceOrder.Hold, 0.5), 9);       // sooner under a hold
        // the clock: time AND (worn OR slowed)
        Assert.Equal(LullTrigger.Count, RhythmMath.Trigger(in r, 44, 45, 0.4, BraceOrder.Charge, 0.4, BraceOrder.Charge, false));
        Assert.Equal(LullTrigger.Clock, RhythmMath.Trigger(in r, 45, 45, 0.4, BraceOrder.Charge, 0.4, BraceOrder.Charge, false));
        Assert.Equal(LullTrigger.Count, RhythmMath.Trigger(in r, 50, 45, 0.6, BraceOrder.Charge, 0.4, BraceOrder.Charge, false)); // one unit fresh enough
        Assert.Equal(LullTrigger.Clock, RhythmMath.Trigger(in r, 50, 45, 0.6, BraceOrder.Charge, 0.7, BraceOrder.Charge, true));  // ... but the blows slowed
        Assert.Equal(LullTrigger.Clock, RhythmMath.Trigger(in r, 31, 30, 0.6, BraceOrder.Hold, 0.6, BraceOrder.Hold, false));     // a hold's 65%
        // the spent test (step 78's thresholds, now on the unit's bar) - at once after the least contact
        Assert.Equal(LullTrigger.Tired, RhythmMath.Trigger(in r, 10, 45, 0.25, BraceOrder.Charge, 0.25, BraceOrder.Charge, false));
        Assert.Equal(LullTrigger.Count, RhythmMath.Trigger(in r, 9, 45, 0.25, BraceOrder.Charge, 0.25, BraceOrder.Charge, false));
        // the blows slowed: 60% of the peak
        Assert.True(RhythmMath.RateFallen(in r, 6, 10));
        Assert.False(RhythmMath.RateFallen(in r, 6.5, 10));
        Assert.False(RhythmMath.RateFallen(in r, 0, 0));
        var bars = Rules(rateFallen: 0);
        Assert.False(RhythmMath.RateFallen(in bars, 0, 10));
    }

    /// <summary>Two lines facing each other along y (A south facing north, B north facing south), 10 files 1 m apart, 3 ranks; A's ranks' bars by rank.</summary>
    private static void Note(RhythmUnits t, double aFront, double bFront, double[] barA, double barB, BraceOrderKind ka, BraceOrderKind kb, double contactWithin = 2.5,
        Func<int, int, double>? bShift = null)
    {
        for (int k = 0; k < 3; k++)
            for (int f = 0; f < 10; f++)
            {
                double ay = aFront - k, by = bFront + k + (bShift?.Invoke(k, f) ?? 0);
                t.Note(A, f, ay, barA[k], k == 0 && by - ay <= contactWithin + 3, k == 0 ? B : -1, ka, false, false);
                t.Note(B, f, by, barB, k == 0, k == 0 ? A : -1, kb, false, false);
            }
    }

    private static double Run(RhythmUnits t, in RhythmRules r, double now, double seconds, double aFront, double bFront, double[] barA, double barB,
        BraceOrderKind ka = BraceOrderKind.Charge, BraceOrderKind kb = BraceOrderKind.Charge, int attacksA = 0, Func<int, int, double>? bShift = null)
    {
        for (double s = 0; s < seconds - 1e-9; s += 0.5)
        {
            now += 0.5;
            Note(t, aFront, bFront, barA, barB, ka, kb, bShift: bShift);
            for (int i = 0; i < attacksA; i++) t.NoteAttack(A);
            if (t.Due(now)) t.Publish(now, in r);
        }
        return now;
    }

    [Fact]
    public void The_whole_units_bar_starts_the_clocks_lull_though_the_rotated_front_is_fresh()
    {
        // A's front rank rotated fresh (80%), its ranks behind spent (30%): the front men's bar would never say spent - the unit's does
        var r = Rules();
        var t = new RhythmUnits { Dice = new Fixed { V = 0.5 } }; // a charge's clock 45 s
        var barA = new[] { 0.8, 0.3, 0.3 };
        double now = Run(t, in r, 100, 44, 0, 1, barA, 0.4);
        Assert.False(t.InLull[A]);
        Assert.Equal(45, t.ClockAt[A], 6);
        Assert.Equal((0.8 * 10 + 0.3 * 20) / 30, t.UnitBar[A], 6);   // all three ranks within 8 m of the front
        Assert.True(t.FrontBar[A] > RhythmMath.LullStart(in r, BraceOrder.Charge)); // step 78's front bar: never spent
        now = Run(t, in r, now, 2, 0, 1, barA, 0.4);
        Assert.True(t.InLull[A] && t.InLull[B]);
        Assert.Equal(LullTrigger.Clock, t.LullTriggerOf[A]);
        Assert.Equal(t.UnitBar[A], t.LullStartBar[A], 6);
        // a fresh unit (65%) never lulls on the clock alone
        var fresh = new RhythmUnits { Dice = new Fixed { V = 0.5 } };
        Run(fresh, in r, 100, 80, 0, 1, new[] { 0.65, 0.65, 0.65 }, 0.4);
        Assert.False(fresh.InLull[A]);
        Assert.True(fresh.ClockWaitLooks > 0);
        // a deep unit measured only near its front: LullUnitDepthMetres 1.5 = the first two ranks
        var shallow = new RhythmUnits { Dice = new Fixed { V = 0 } };
        var rs = new RhythmRules(true, true, true, true, unitDepthMetres: 1.5);
        Run(shallow, in rs, 100, 2, 0, 1, new[] { 0.8, 0.3, 0.9 }, 0.4);
        Assert.Equal(0.55, shallow.UnitBar[A], 6);
    }

    [Fact]
    public void The_blows_slowing_start_the_clocks_lull_and_the_clock_starts_again_after_it()
    {
        var r = Rules(minContact: 10);
        var t = new RhythmUnits { Dice = new Fixed { V = 0 } }; // a charge's clock 30 s
        var fresh = new[] { 0.7, 0.7, 0.7 };
        double now = Run(t, in r, 100, 20, 0, 1, fresh, 0.7, attacksA: 10);  // a brisk exchange
        double peak = t.RatePeak[A];
        Assert.True(peak > 10);
        now = Run(t, in r, now, 9, 0, 1, fresh, 0.7, attacksA: 10);
        Assert.False(t.InLull[A]);                                         // the clock not run yet (29 s)
        now = Run(t, in r, now, 12, 0, 1, fresh, 0.7, attacksA: 1);       // the blows die down
        Assert.True(t.InLull[A]);
        Assert.Equal(LullTrigger.Clock, t.LullTriggerOf[A]);
        Assert.True(t.LullStartRate[A] <= 0.6 * t.LullStartRatePeak[A] + 1e-9);
        // the lull ends (too long) - the contact clock restarts from the next contact, not from the old spell
        Run(t, in r, now, 31, 0, 1, fresh, 0.7);
        Assert.False(t.InLull[A]);
        Assert.Contains(LullEnd.Timeout, t.EndedWhy);
        Assert.True(double.IsNaN(t.ContactSince[A]) || t.ContactSince[A] > now);
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
        var r = Rules();
        RhythmMath.StartLines(in r, plane, frontA, frontB, out double la, out double lb);
        Assert.Equal(-1.25, la, 6);                  // the plane − 1.75 (below the front − the 2 m ease: −1)
        Assert.Equal(2.25, lb, 6);                   // the plane + 1.75 (beyond the front + 2: 2)
        Assert.Equal(3.5, lb - la, 6);
        Assert.False(RhythmMath.GapHolds(in r, 2.9));
        Assert.True(RhythmMath.GapHolds(in r, 3.0));
        // a fight that already has a gap still backs off the ease: fronts 0 and 3 → lines −2 and 5
        RhythmMath.StartLines(in r, 1.5, 0, 3, out la, out lb);
        Assert.Equal(-2, la, 6);
        Assert.Equal(5, lb, 6);
    }

    [Fact]
    public void A_meshed_lull_separates_about_its_middle_opens_and_steps_back_while_the_gap_does_not_hold()
    {
        var r = new RhythmRules(true, true, true, true, lullMinContactSeconds: 0, lullMaxSeconds: 120);
        var t = new RhythmUnits { Dice = new Fixed { V = 0.5 } };
        var spent = new[] { 0.2, 0.2, 0.2 };
        // A's front at y 1, B's at y 0: meshed by a metre (A's centroid 0, B's 1)
        double now = Run(t, in r, 100, 1, 1, 0, spent, 0.2);
        Assert.True(t.InLull[A]);
        Assert.Equal(LullTrigger.Tired, t.LullTriggerOf[A]);
        Assert.Equal(-1, t.LullStartGap[A], 6);
        Assert.Equal(-1.25, t.LullLine[A], 6);       // along +y
        Assert.Equal(-2.25, t.LullLine[B], 6);       // along −y: B's line at y 2.25
        Assert.Equal(2.25, t.LullEase[A], 6);        // A backs 2.25 m from its nearest men
        Assert.Equal(2.25, t.LullEase[B], 6);
        Assert.Equal(-t.LullUX[A], t.LullUX[B], 9);
        Assert.Equal(-t.LullUY[A], t.LullUY[B], 9);
        Assert.Equal(1.25, t.AheadOfLullLine(A, 0, 0), 6);
        // both sides walk back to their lines: the gap 3.5 m - it holds
        now = Run(t, in r, now, 1, -1.25, 2.25, spent, 0.2);
        Assert.True(t.InLull[A]);
        Assert.Equal(3.5, t.GapMean[A], 6);
        Assert.False(double.IsNaN(t.LullOpenedAfter[A]));
        Assert.Equal(A, t.Opened[0] == A || t.Opened[0] == B ? A : -1);
        Assert.Equal(-1.25, t.LullLine[A], 6);       // held: no step back
        // another pair: A obeys, B stays meshed (stuck) - after the settle A steps its line back 0.25 m a publish, B keeps its line
        var u = new RhythmUnits { Dice = new Fixed { V = 0.5 } };
        now = Run(u, in r, 200, 1, 1, 0, spent, 0.2);
        double lb0 = u.LullLine[B];
        now = Run(u, in r, now, 2.5, -1.25, 0, spent, 0.2);
        Assert.Equal(-1.25, u.LullLine[A], 6);       // within the settle
        now = Run(u, in r, now, 2, u.LullLine[A], 0, spent, 0.2);
        Assert.True(u.LullLine[A] < -1.25 - 0.4);
        Assert.Equal(lb0, u.LullLine[B], 9);         // B is not at its line - stepping it back would do nothing
        Assert.True(u.LineSteps >= 2);
        // the gap holds once A has backed far enough from the stuck B (3 m): no further step
        for (int i = 0; i < 20; i++) now = Run(u, in r, now, 0.5, u.LullLine[A], 0, spent, 0.2);
        Assert.Equal(-3, u.LullLine[A], 6);
        // the cap: B pressing after A (a metre off its men) - never more than LullMaxBackMetres beyond where the line began
        for (int i = 0; i < 40; i++) now = Run(u, in r, now, 0.5, u.LullLine[A], u.LullLine[A] + 1, spent, 0.2);
        Assert.True(u.InLull[A]);
        Assert.Equal(-1.25 - 8, u.LullLine[A], 6);
        Assert.True(u.GapLooks > 0);
    }

    [Fact]
    public void A_charge_order_in_the_lulls_first_seconds_waits_for_the_grace()
    {
        var r = Rules(minContact: 0);
        var spent = new[] { 0.2, 0.2, 0.2 };
        var t = new RhythmUnits { Dice = new Fixed { V = 0.5 } };
        double now = Run(t, in r, 100, 1, 0, 1, spent, 0.2, BraceOrderKind.Stop, BraceOrderKind.Stop);
        Assert.True(t.InLull[A]);
        now = Run(t, in r, now, 1, 0, 1, spent, 0.2, BraceOrderKind.Charge, BraceOrderKind.Stop);
        Assert.True(t.InLull[A]);
        Assert.Equal(1, t.ChargeDeferred);
        now = Run(t, in r, now, 3, 0, 1, spent, 0.2, BraceOrderKind.Charge, BraceOrderKind.Stop);
        Assert.False(t.InLull[A]);
        Assert.Equal(LullEnd.ChargeOrder, t.EndedWhy[t.EndedN - 1 >= 0 ? t.EndedN - 1 : 0]);
        // a deferred charge order taken back (a hold again) within the grace: the lull goes on
        var b = new RhythmUnits { Dice = new Fixed { V = 0.5 } };
        now = Run(b, in r, 300, 1, 0, 1, spent, 0.2, BraceOrderKind.Stop, BraceOrderKind.Stop);
        now = Run(b, in r, now, 0.5, 0, 1, spent, 0.2, BraceOrderKind.Charge, BraceOrderKind.Stop);
        now = Run(b, in r, now, 4, 0, 1, spent, 0.2, BraceOrderKind.Stop, BraceOrderKind.Stop);
        Assert.True(b.InLull[A]);
        // grace 0 = step 78's: at once
        var r0 = Rules(minContact: 0, grace: 0);
        var c = new RhythmUnits { Dice = new Fixed { V = 0.5 } };
        now = Run(c, in r0, 400, 1, 0, 1, spent, 0.2, BraceOrderKind.Stop, BraceOrderKind.Stop);
        Run(c, in r0, now, 0.5, 0, 1, spent, 0.2, BraceOrderKind.Charge, BraceOrderKind.Stop);
        Assert.False(c.InLull[A]);
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
        var s = new RhythmStats();
        var t = new RhythmUnits();
        s.Lulls = 3;
        s.AddLullStart(LullTrigger.Clock, 6, 12, 41);
        s.AddLullStart(LullTrigger.Clock, 5, 10, 47);
        s.AddLullStart(LullTrigger.Tired, double.NaN, 0, 12);
        s.AddLullEnd(LullEnd.Recovered, 15, 5.2, 4.5);
        s.AddLullEnd(LullEnd.ChargeOrder, 9, 2.0, double.NaN);
        t.GapLooks = 4;
        t.GapMeanSum = 14;
        t.GapQ10Sum = 10;
        t.GapQ90Sum = 18;
        t.LineSteps = 7;
        t.ChargeDeferred = 1;
        var lines = new List<string>();
        s.AddSummaryLines(lines, Rules(), t);
        Assert.Equal(3, lines.Count);
        Assert.Contains("THE LULL'S TRIGGER AND GAP: started by the breather clock 2, by both units spent 1", lines[2]);
        Assert.Contains("mean 3.5 m (p10 2.5, p90 4.5", lines[2]);
        Assert.Contains("after mean 4.5 s", lines[2]);
        Assert.Contains("never 1", lines[2]);
        Assert.Contains("stepped back while it did not hold 7 times", lines[2]);
        Assert.Contains("deferred 1", lines[2]);
        Assert.Contains("recovered (the unit's bar) 1", lines[1]);

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

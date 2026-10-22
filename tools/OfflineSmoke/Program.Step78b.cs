using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Step 78b (Toni's 225 v 229 battle 2026-10-22: 2 lulls in 435 s, "between the two fronts, never, not for a sec"; the enemy's 69 m of recruits went
    /// round both ends of his ~31 m line): THE LULL THAT SHOWS - a meshed six-minute grind through the real logic with courage's stand-in world and the
    /// REAL input filter gets its breathers from the clock, each opening a clear gap measured from the men; NO WRAPPING - a 69 m unit charging a 31 m
    /// held line under a stand-in of the game's charge that sends every man beyond the line's end round it: nobody goes round, the wings stand off the
    /// ends behind the bent front; a unit ordered there still goes; the cost at 1000 men. What it cannot check: how the engine walks the men (PLAYTEST
    /// D43b).
    /// </summary>
    internal static partial class Program
    {
        /// <summary>The shipped step-78b values (defaults.json).</summary>
        private static void Step78bDefaults()
        {
            S.Set(SettingsSchema.LullUnitDepthMetres, 8.0, SettingSources.File);
            S.Set(SettingsSchema.LullClockChargeMinSeconds, 30.0, SettingSources.File);
            S.Set(SettingsSchema.LullClockChargeMaxSeconds, 60.0, SettingSources.File);
            S.Set(SettingsSchema.LullClockAdvanceMinSeconds, 25.0, SettingSources.File);
            S.Set(SettingsSchema.LullClockAdvanceMaxSeconds, 50.0, SettingSources.File);
            S.Set(SettingsSchema.LullClockHoldMinSeconds, 20.0, SettingSources.File);
            S.Set(SettingsSchema.LullClockHoldMaxSeconds, 40.0, SettingSources.File);
            S.Set(SettingsSchema.LullClockChargePercent, 55, SettingSources.File);
            S.Set(SettingsSchema.LullClockAdvancePercent, 60, SettingSources.File);
            S.Set(SettingsSchema.LullClockHoldPercent, 65, SettingSources.File);
            S.Set(SettingsSchema.LullAttackRateFallenPercent, 60, SettingSources.File);
            S.Set(SettingsSchema.LullChargeGraceSeconds, 3.0, SettingSources.File);
            S.Set(SettingsSchema.LullGapMetres, 3.5, SettingSources.File);
            S.Set(SettingsSchema.LullMaxBackMetres, 8.0, SettingSources.File);
            S.Set(SettingsSchema.LullRecoverChargePercent, 20, SettingSources.File);
            S.Set(SettingsSchema.LullRecoverAdvancePercent, 25, SettingSources.File);
            S.Set(SettingsSchema.LullRecoverHoldPercent, 30, SettingSources.File);
            S.Set(SettingsSchema.PressWrapAllowed, false, SettingSources.File);
            S.Set(SettingsSchema.PressWingBendDegrees, 30.0, SettingSources.File);
        }

        private static AthleticsLogic NewStep78bLogic(FakeCourageBody body, FakeRhythmBody rhythm, int seed)
        {
            var logic = NewStep78Logic(body, rhythm, rotation: true, lull: true);
            Step78bDefaults();
            logic.RhythmUnits.Dice = new SeededRandom(seed);
            return logic;
        }

        /// <summary>The stand-in grind: the game walks each man at his nearest enemy until within 1.2 m (a charge); in contact (an enemy within 2.5 m) his
        /// bar drains <paramref name="drain"/> a second and he swings about every 2 s × his bar (the attack rate's notes), out of contact it refills (full in
        /// 60 s); every vector through the REAL input filter whenever a wish of ours is on him; 1.5 m/s.</summary>
        private static double RunGrind(AthleticsLogic logic, FakeCourageBody body, double t, double seconds, double drain, Random dice, Action<double>? each = null)
        {
            const double speed = 1.5, dt = 0.1;
            for (double s = 0; s < seconds - 1e-9; s += dt)
            {
                foreach (var m in body.Men)
                {
                    double best = double.MaxValue;
                    FakeCourageMan? near = null;
                    foreach (var e in body.Men)
                    {
                        if (e.Team == m.Team) continue;
                        double d = (e.X - m.X) * (e.X - m.X) + (e.Y - m.Y) * (e.Y - m.Y);
                        if (d < best)
                        {
                            best = d;
                            near = e;
                        }
                    }
                    float vx = 0, vy = 0;
                    double dist = Math.Sqrt(best);
                    if (near != null && dist > 1.2)
                    {
                        double dx = near.X - m.X, dy = near.Y - m.Y;
                        vx = (float)(dx / dist);
                        vy = (float)(dy / dist);
                    }
                    var input = m.St.Input;
                    if (input != null && input.Active)
                    {
                        uint flags = 0;
                        AiInputHook.Apply(m.St, input, false, ref flags, ref vx, ref vy);
                    }
                    m.X += vx * speed * dt;
                    m.Y += vy * speed * dt;
                    if (near != null && dist <= 2.5)
                    {
                        SetBar(m.St, Math.Max(0, m.St.Fraction - drain * dt));
                        if (dice.NextDouble() < dt * 0.5 * m.St.Fraction) logic.NoteRhythmAttack(m.St, false, t);
                    }
                    else SetBar(m.St, Math.Min(1.0, m.St.Fraction + dt / 60.0));
                }
                logic.TickCourage(t += dt);
                each?.Invoke(t);
            }
            return t;
        }

        /// <summary>The gap between the two units from the men themselves (the smoke's own measure): in 2 m bins across, the south unit's front-most and the
        /// north unit's rear-most; the median over the bins with both.</summary>
        private static double MenGap(List<FakeCourageMan> south, List<FakeCourageMan> north)
        {
            var gaps = new List<double>();
            for (double x = -20; x < 20; x += 2)
            {
                var s = south.Where(m => m.X >= x && m.X < x + 2).ToList();
                var n = north.Where(m => m.X >= x && m.X < x + 2).ToList();
                if (s.Count == 0 || n.Count == 0) continue;
                gaps.Add(n.Min(m => m.Y) - s.Max(m => m.Y));
            }
            if (gaps.Count == 0) return double.NaN;
            gaps.Sort();
            return gaps[gaps.Count / 2];
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void MeshedGrindGetsItsBreathers()
        {
            var keep = _logic;
            try
            {
                var body = new FakeCourageBody();
                var rhythm = new FakeRhythmBody();
                var logic = NewStep78bLogic(body, rhythm, 781);
                body.Orders[1] = body.Orders[2] = FearOrder.Charge;
                double t = 101000;
                // two 14 x 3 lines MESHED by a metre (Toni's first lull: "the gap between the fronts -20.7 m"): the south front at y 0, the north front at y −1,
                // its files half a metre over
                var a = new List<FakeCourageMan>();
                var b = new List<FakeCourageMan>();
                for (int k = 0; k < 3; k++)
                    for (int f = 0; f < 14; f++)
                    {
                        a.Add(body.ByIndex[FearMan(logic, body, 44000 + k * 14 + f, -6.5 + f, -k * 1.0, 0, 1, legionary: true, morale: 70, bar: 1.0).AgentIndex]);
                        b.Add(body.ByIndex[FearMan(logic, body, 44100 + k * 14 + f, -6.0 + f, -1.0 + k * 1.0, 1, 2, legionary: true, morale: 70, bar: 1.0).AgentIndex]);
                    }
                double startGap = MenGap(a, b);
                var dice = new Random(78);
                var u = logic.RhythmUnits;
                int ka = FearMath.UnitKey(0, 1);
                var perLull = new List<double>();
                var perLullSmoke = new List<double>();
                double lullMax = double.NaN, smokeMax = double.NaN;
                bool was = false;
                double lullSeconds = 0;
                t = RunGrind(logic, body, t, 360, 0.008, dice, now =>
                {
                    bool inLull = u.InLull[ka];
                    if (inLull)
                    {
                        lullSeconds += 0.1;
                        if (!double.IsNaN(u.GapMean[ka]) && (double.IsNaN(lullMax) || u.GapMean[ka] > lullMax)) lullMax = u.GapMean[ka];
                        double g = MenGap(a, b);
                        if (!double.IsNaN(g) && (double.IsNaN(smokeMax) || g > smokeMax)) smokeMax = g;
                    }
                    else if (was)
                    {
                        perLull.Add(lullMax);
                        perLullSmoke.Add(smokeMax);
                        lullMax = smokeMax = double.NaN;
                    }
                    was = inLull;
                });
                var rs = logic.RhythmStats;
                Console.WriteLine("        step 78b a MESHED six-minute grind (14 x 3 a side, meshed by 1 m - the gap from the men " + F2s(startGap) + " m): lulls "
                                  + rs.Lulls + " (the clock " + rs.LullsBy[(int)LullTrigger.Clock] + ", both spent " + rs.LullsBy[(int)LullTrigger.Tired] + "), "
                                  + F2s(lullSeconds) + " s in lulls, the gap at its widest in each (the logic's measure) " + string.Join(" / ", perLull.Select(F2s))
                                  + " m, (the smoke's own) " + string.Join(" / ", perLullSmoke.Select(F2s)) + " m; opened after mean "
                                  + F2s(rs.OpenedSecondsN > 0 ? rs.OpenedSecondsSum / rs.OpenedSecondsN : double.NaN) + " s, never " + rs.NeverOpened + "; ends "
                                  + string.Join(", ", Enumerable.Range(0, (int)LullEnd.Count).Where(i => rs.LullEnds[i] > 0).Select(i => RhythmMath.LullEndName((LullEnd)i) + " " + rs.LullEnds[i]))
                                  + "; lines stepped back " + u.LineSteps + ", men walked back " + rs.MenWalkedBack);
                Check(startGap < 0, "precondition: the grind is not meshed (" + startGap + ")");
                Check(rs.Lulls >= 2, "a six-minute grind got only " + rs.Lulls + " lulls (step 78b wants 2+)");
                Check(rs.LullsBy[(int)LullTrigger.Clock] >= 1, "no lull came from the breather clock");
                Check(perLull.Count(g => g >= 3.0) >= 2, "fewer than two lulls opened a 3 m gap (the logic's measure): " + string.Join(" / ", perLull.Select(F2s)));
                Check(perLullSmoke.Count(g => g >= 3.0) >= 2, "fewer than two lulls opened a 3 m gap (the smoke's own measure): " + string.Join(" / ", perLullSmoke.Select(F2s)));
                Check(rs.OpenedSecondsN >= 2, "the gap was not seen to hold (opened " + rs.OpenedSecondsN + ")");
                LogHas("[rhythm] first LULL at ");
                LogHas("the breather clock");
                LogHas("[rhythm] first lull GAP OPENED at ");
                logic.WriteRhythmSummary();
                LogHas("[summary] battle rhythm (step 78b) - THE LULL'S TRIGGER AND GAP: started by the breather clock");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        private const double WideLineHalf = 15.6; // the line's men at x = −15.2..15.2 (39 files at 0.8 m - 31 m), its front rank at y = 0 facing SOUTH

        /// <summary>The stand-in of the game's charge for the wide unit: each man at his nearest enemy; within 1.2 m he fights (stands); a man beyond the
        /// line's end heads ROUND it and in behind it (the AI's flanking the press must stop). Every vector through the REAL input filter. 4 m/s.</summary>
        private static double RunWide(AthleticsLogic logic, List<FakeCourageMan> attackers, List<FakeCourageMan> line, double t, double seconds, Action<double>? each = null)
        {
            const double speed = 4, dt = 0.1;
            for (double s = 0; s < seconds - 1e-9; s += dt)
            {
                foreach (var m in attackers)
                {
                    FakeCourageMan? near = null;
                    double best = double.MaxValue;
                    foreach (var e in line)
                    {
                        double dd = (e.X - m.X) * (e.X - m.X) + (e.Y - m.Y) * (e.Y - m.Y);
                        if (dd < best)
                        {
                            best = dd;
                            near = e;
                        }
                    }
                    if (near == null) continue;
                    double tx = near.X, ty = near.Y;
                    bool fights = Math.Sqrt(best) <= 1.2;
                    if (Math.Abs(m.X) > WideLineHalf + 0.6)
                    {
                        fights = false;
                        double side = m.X >= 0 ? 1 : -1;
                        if (m.Y < -0.5)
                        {
                            tx = side * (WideLineHalf + 2); // round the end: past its corner ...
                            ty = 2;
                        }
                        else
                        {
                            tx = side * (WideLineHalf - 4); // ... then in behind the line
                            ty = 3;
                        }
                    }
                    double dx = tx - m.X, dy = ty - m.Y, len = Math.Sqrt(dx * dx + dy * dy);
                    float vx = 0, vy = 0;
                    if (!fights && len > 1e-6)
                    {
                        vx = (float)(dx / len);
                        vy = (float)(dy / len);
                    }
                    var input = m.St.Input;
                    if (input != null && input.Active)
                    {
                        uint flags = 0;
                        AiInputHook.Apply(m.St, input, false, ref flags, ref vx, ref vy);
                    }
                    m.X += vx * speed * dt;
                    m.Y += vy * speed * dt;
                }
                logic.TickCourage(t += dt);
                each?.Invoke(t);
            }
            return t;
        }

        /// <summary>Toni's line: your formation 2, 39 files x 2 ranks 0.8 m apart (31 m), its front at y = 0 facing south, held by step 41; and the enemy's
        /// 229 recruits as 46 files x 5 ranks 1.5 m apart (69 m), formation 3, their front 6 m south, on a charge (or a move - <paramref name="kind"/>).</summary>
        private static (List<FakeCourageMan> line, List<FakeCourageMan> wide) WideOnNarrow(AthleticsLogic logic, FakeCourageBody body, int index, BraceOrderKind kind)
        {
            body.Orders[2] = FearOrder.Wall;
            body.Facings[2] = (0, -1);
            var line = new List<FakeCourageMan>();
            for (int f = 0; f < 39; f++)
                for (int k = 0; k < 2; k++)
                {
                    var st = FearMan(logic, body, index + f * 2 + k, -15.2 + f * 0.8, k * 1.0, 0, 2, legionary: true, morale: 70, bar: 1.0);
                    var m = body.ByIndex[st.AgentIndex];
                    m.Gate = CourageSkip.WallHeld;
                    line.Add(m);
                }
            body.EnemyFronts[2] = (0, -8.4, 0, 1, 69, 5.0);
            body.Orders[3] = kind == BraceOrderKind.Move ? FearOrder.Hold : FearOrder.Charge;
            body.Kinds[3] = kind;
            body.Facings[3] = (0, 1);
            var wide = new List<FakeCourageMan>();
            for (int f = 0; f < 46; f++)
                for (int k = 0; k < 5; k++)
                {
                    var st = FearMan(logic, body, index + 200 + f * 5 + k, -33.75 + f * 1.5, -6 - k * 1.2, 1, 3, legionary: false, morale: 70, bar: 1.0, slot: false);
                    wide.Add(body.ByIndex[st.AgentIndex]);
                }
            body.EnemyFronts[3] = (0, 0.5, 0, -1, 31, 1.0);
            return (line, wide);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WideUnitStandsOffTheEnds()
        {
            var keep = _logic;
            try
            {
                // ---- step 77's wrap (PressWrapAllowed on, no bend): the wings go round both ends - the A / B
                var body = new FakeCourageBody();
                var block = new FakeBlockBody();
                var logic = NewStep77Logic(body, block, press: true);
                Step78bDefaults();
                S.Set(SettingsSchema.PressWrapAllowed, true, SettingSources.File);
                S.Set(SettingsSchema.PressWingBendDegrees, 0.0, SettingSources.File);
                double t = 102000;
                var (line, wide) = WideOnNarrow(logic, body, 46000, BraceOrderKind.Charge);
                t = RunWide(logic, wide, line, t, 15);
                int roundOld = wide.Count(m => m.Y > 0.95);
                long wrapOld = logic.PressStats.Actions[1, (int)PressAction.Wrap];
                Check(logic.PressLines.Wraps[FearMath.UnitKey(1, 3)] && roundOld >= 10, "the A / B: with step 77's wrap the 69 m unit did not go round the 31 m line ("
                      + roundOld + " men past its front, wraps " + logic.PressLines.Wraps[FearMath.UnitKey(1, 3)] + ")");

                // ---- step 78b: no wrapping - the wings stand off the ends behind the bent front
                body = new FakeCourageBody();
                block = new FakeBlockBody();
                logic = NewStep77Logic(body, block, press: true);
                Step78bDefaults();
                t = 102500;
                (line, wide) = WideOnNarrow(logic, body, 47000, BraceOrderKind.Charge);
                var startX = wide.ToDictionary(m => m, m => m.X);
                int pastEver = 0;
                double worstAhead = double.NegativeInfinity;
                double tan = Math.Tan(30 * Math.PI / 180);
                t = RunWide(logic, wide, line, t, 20, _ =>
                {
                    pastEver = Math.Max(pastEver, wide.Count(m => m.Y > 0.95));
                    foreach (var m in wide)
                    {
                        double past = Math.Abs(m.X) - WideLineHalf;
                        if (past <= 1) continue;
                        double plane = tan * (past - 1);   // the bent front's d there (his side = south, d = −y)
                        double ahead = plane - (-m.Y);       // how far forward of the bent front
                        if (ahead > worstAhead) worstAhead = ahead;
                    }
                });
                var ps = logic.PressStats;
                // the wings: the men who started beyond the line's ends (the stand-in has no collisions - they gather at the end's corner; the game's crowd
                // spreads them along the bent front); where they stand now, how far forward of the bent front
                var wings = wide.Where(m => Math.Abs(startX[m]) > WideLineHalf + 1).ToList();
                var atEnds = wings.Where(m => Math.Abs(m.X) > WideLineHalf - 1 && Math.Abs(m.X) - WideLineHalf <= 8).ToList();
                double endAhead = double.NegativeInfinity;
                foreach (var m in wings)
                {
                    double past = Math.Abs(m.X) - WideLineHalf;
                    endAhead = Math.Max(endAhead, (past > 1 ? tan * (past - 1) : 0) + m.Y);
                }
                long roundOn = 0;
                for (int c = 0; c < (int)PressCause.Count; c++) roundOn += ps.Crossings[1, 0, c, 0] + ps.Crossings[1, 0, c, 1];
                Console.WriteLine("        step 78b a 69 m unit on a 31 m line: step 77's wrap " + roundOld + " men past its front (wrap looks " + wrapOld + "); NO WRAP - past the front "
                                  + "at worst " + pastEver + ", crossings round an end " + roundOn + ", the wrap looks " + ps.Actions[1, (int)PressAction.Wrap] + "; wing looks "
                                  + ps.Actions[1, (int)PressAction.Wing] + " by " + ps.WingMen + " men, walked back to the bent front " + ps.WingPushedMen + "; the worst a wing man stood forward "
                                  + "of the bent front " + F2s(worstAhead) + " m at any moment (a man running in between two looks - still short of the line's front), at the end "
                                  + F2s(endAhead) + " m; " + wings.Count + " wing men (beyond the ends at the start), " + atEnds.Count + " of them standing at an end now (within 8 m of it, never round it)");
                Check(pastEver == 0, "NO WRAP: " + pastEver + " men of the wide unit got past the line's front");
                Check(roundOn == 0, "NO WRAP: " + roundOn + " crossings round an end");
                Check(ps.Actions[1, (int)PressAction.Wrap] == 0, "NO WRAP: the old wrap still let men round (" + ps.Actions[1, (int)PressAction.Wrap] + " looks)");
                Check(ps.Actions[1, (int)PressAction.Wing] > 50 && ps.WingMen >= 10, "the wings were not held at the bent front: " + ps.Actions[1, (int)PressAction.Wing] + " looks, " + ps.WingMen + " men");
                Check(endAhead < 1.0, "a wing man stands " + F2s(endAhead) + " m forward of the bent front at the end (round the end)");
                Check(worstAhead < 4.5, "a wing man stood " + F2s(worstAhead) + " m forward of the bent front");
                Check(wings.Count >= 60 && atEnds.Count >= wings.Count * 9 / 10, "the wings did not stand at the ends: " + atEnds.Count + " of " + wings.Count);
                LogHas("[press] first WING man held at an end at ");
                logic.WritePressSummary();
                LogHas("[summary] press, don't pass (step 78b) - NO WRAPPING: the wrap off (PressWrapAllowed)");

                // ---- a unit ORDERED there (a move - the general's flanking order): its men go round - the order's own place
                body = new FakeCourageBody();
                block = new FakeBlockBody();
                logic = NewStep77Logic(body, block, press: true);
                Step78bDefaults();
                t = 103000;
                (line, wide) = WideOnNarrow(logic, body, 48000, BraceOrderKind.Move);
                t = RunWide(logic, wide, line, t, 15);
                int ordered = wide.Count(m => m.Y > 0.95 && Math.Abs(m.X) > WideLineHalf);
                Console.WriteLine("        step 78b the same unit ORDERED there (a move): " + ordered + " men round the ends (ordered looks " + logic.PressStats.Actions[1, (int)PressAction.Ordered]
                                  + "), the middle past the front " + wide.Count(m => m.Y > 0.95 && Math.Abs(m.X) <= WideLineHalf - 1));
                Check(ordered >= 10 && logic.PressStats.Actions[1, (int)PressAction.Ordered] > 0, "a unit ORDERED round the line was held back (" + ordered + ")");
                Check(wide.Count(m => m.Y > 0.95 && Math.Abs(m.X) <= WideLineHalf - 1) == 0, "an ordered unit walked through the line's middle");
                LogHas("[press] first man of a unit ORDERED beside / behind a line at ");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        /// <summary>The cost: 1000 men - two 500-man blocks in contact, both spent, a lull running (the gap measured from all 1000 men every 0.5 s, the lines,
        /// the walk back), every man looked at - small, no bytes.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Step78bCostStaysFlat()
        {
            var keep = _logic;
            try
            {
                var body = new FakeCourageBody();
                var rhythm = new FakeRhythmBody();
                var logic = NewStep78bLogic(body, rhythm, 7810);
                body.Orders[1] = body.Orders[2] = FearOrder.Charge;
                double t0 = 104000;
                for (int k = 0; k < 20; k++)
                    for (int f = 0; f < 25; f++)
                    {
                        FearMan(logic, body, 49000 + k * 25 + f, f - 12, -k * 1.0, 0, 1, legionary: true, morale: 70, bar: 0.2);
                        FearMan(logic, body, 50000 + k * 25 + f, f - 12, 1.0 + k * 1.0, 1, 2, legionary: true, morale: 70, bar: 0.2);
                    }
                for (int k = 0; k < 24; k++) logic.TickCourage(t0 += 0.5); // 12 s: both spent after 10 s of contact - the lull runs
                var u = logic.RhythmUnits;
                Check(u.InLull[FearMath.UnitKey(0, 1)], "cost: no lull between the spent blocks");
                const int ticks = 300;
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var rs = logic.RhythmStats;
                double c0 = rs.LookCostMs + rs.SteerCostMs + rs.PublishCostMs;
                long t0Ticks = rs.Ticks, gap0 = u.GapLooks;
                var sw = Stopwatch.StartNew();
                for (int k = 1; k <= ticks; k++) logic.TickCourage(t0 + k / 60.0);
                sw.Stop();
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double own = (rs.LookCostMs + rs.SteerCostMs + rs.PublishCostMs - c0) / Math.Max(1, rs.Ticks - t0Ticks);
                Console.WriteLine("        step 78b cost: 1000 men in a lull (" + logic.RhythmMenNow + " with a rhythm wish, the gap measured " + (u.GapLooks - gap0) + " times) - the courage tick "
                                  + (sw.Elapsed.TotalMilliseconds / ticks).ToString("0.000", CultureInfo.InvariantCulture) + " ms (the stand-in's scans are plain loops), the rhythm's own "
                                  + own.ToString("0.000", CultureInfo.InvariantCulture) + " ms a tick, " + allocated + " bytes over " + ticks + " ticks");
                Check(u.GapLooks - gap0 >= 5, "cost: the gap was not measured in the lull (" + (u.GapLooks - gap0) + ")");
                Check(own < 0.5, "cost: the rhythm's own " + own + " ms a tick (budget 0.5)");
                Check(allocated < 256 * 1024, "cost: step 78b allocates - " + allocated + " bytes over " + ticks + " ticks");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }
    }
}

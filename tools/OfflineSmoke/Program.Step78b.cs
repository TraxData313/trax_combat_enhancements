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
    /// Step 78b (Toni's 225 v 229 battle 2026-10-22: the enemy's 69 m of recruits went round both ends of his ~31 m line): NO WRAPPING - a 69 m unit
    /// charging a 31 m held line under a stand-in of the game's charge that sends every man beyond the line's end round it: nobody goes round, the wings
    /// stand off the ends behind the bent front; a unit ordered there still goes. Step 78c removed 78b's lull and its smoke; the stand-in grind and the
    /// smoke's own gap measure stay here for Program.Step78c.cs. What it cannot check: how the engine walks the men (PLAYTEST D43b).
    /// </summary>
    internal static partial class Program
    {
        /// <summary>The shipped step-78b press values (defaults.json): no wrapping, the wing's bend.</summary>
        private static void Step78bDefaults()
        {
            S.Set(SettingsSchema.PressWrapAllowed, false, SettingSources.File);
            S.Set(SettingsSchema.PressWingBendDegrees, 30.0, SettingSources.File);
        }

        /// <summary>The stand-in grind: the game walks each man at his nearest enemy until within 1.2 m (a charge - <paramref name="charging"/>, everyone by
        /// default); in contact (an enemy within 2.5 m) his bar drains <paramref name="drain"/> a second, out of contact it refills (full in 60 s); every vector
        /// through the REAL input filter whenever a wish of ours is on him (<paramref name="frame"/> sees each man's written vector); 1.5 m/s. Step 78c:
        /// <paramref name="lines"/> = a line's charge - team 0 pushes north, team 1 south, straight, and only at an enemy AHEAD of him (the game's slots and
        /// collisions keep a line a line; the plain stand-in has men chase single enemies through the other side's ranks).</summary>
        private static double RunGrind(AthleticsLogic logic, FakeCourageBody body, double t, double seconds, double drain, Random dice, Action<double>? each = null,
            Func<FakeCourageMan, bool>? charging = null, Action<FakeCourageMan, float, float>? frame = null, bool lines = false)
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
                    if (lines)
                    {
                        if (near != null && dist > 1.2 && (charging == null || charging(m)) && (near.Y - m.Y) * (m.Team == 0 ? 1 : -1) > 0) vy = m.Team == 0 ? 1f : -1f;
                    }
                    else if (near != null && dist > 1.2 && (charging == null || charging(m)))
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
                        frame?.Invoke(m, vx, vy);
                    }
                    m.X += vx * speed * dt;
                    m.Y += vy * speed * dt;
                    if (near != null && dist <= 2.5)
                    {
                        SetBar(m.St, Math.Max(0, m.St.Fraction - drain * dt));
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
    }
}

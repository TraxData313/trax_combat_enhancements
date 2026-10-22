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
    /// Step 78c (Toni 2026-10-22, after playing 78b: "dont script for all of them to walk away or to push into each other, only per men rule, if tired -
    /// step back and wait to recover"): EACH MAN RESTS ON HIS OWN through the real logic with courage's stand-in world and the REAL input filter - a meshed
    /// six-minute grind with many staggered individual rests and no mass walk-off; both sides tired = a gap by itself; one side tired = the fresh side
    /// presses; a shield wall rests in its slots; the input frame; the switches; the cost at 1000 men. What it cannot check: how the engine walks a man
    /// backwards and whether its charge honours the clamp (PLAYTEST D43c).
    /// </summary>
    internal static partial class Program
    {
        private static AthleticsLogic NewStep78cLogic(FakeCourageBody body, FakeRhythmBody rhythm, bool rotation, int seed)
        {
            var logic = NewStep78Logic(body, rhythm, rotation: rotation, rest: true);
            Step78bDefaults();
            logic.RestDice = new SeededRandom(seed);
            return logic;
        }

        /// <summary>How far a man stands behind his own front where he stands: the front-most man of his unit within 1.5 m to either side of him (the men
        /// in front of him - a line bends in a grind, so a unit-wide quantile would read a pushed-on wing as "his" front). The south unit (team 0) faces
        /// north, the north unit south.</summary>
        private static double BehindOwnFront(FakeCourageMan m, List<FakeCourageMan> unit)
        {
            double front = m.Team == 0 ? m.Y : -m.Y;
            foreach (var u in unit)
                if (Math.Abs(u.X - m.X) <= 1.5) front = Math.Max(front, m.Team == 0 ? u.Y : -u.Y);
            return front - (m.Team == 0 ? m.Y : -m.Y);
        }

        private static (double Dist, double Dx, double Dy) NearestEnemy(FakeCourageBody body, FakeCourageMan m)
        {
            double best = double.MaxValue, dx = 0, dy = 0;
            foreach (var e in body.Men)
            {
                if (e.Team == m.Team) continue;
                double d = (e.X - m.X) * (e.X - m.X) + (e.Y - m.Y) * (e.Y - m.Y);
                if (d >= best) continue;
                best = d;
                dx = e.X - m.X;
                dy = e.Y - m.Y;
            }
            double len = Math.Sqrt(best);
            return len > 1e-9 ? (len, dx / len, dy / len) : (double.NaN, 0, 0);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void MeshedGrindRestsManByMan()
        {
            var keep = _logic;
            try
            {
                var body = new FakeCourageBody();
                var rhythm = new FakeRhythmBody();
                var logic = NewStep78cLogic(body, rhythm, rotation: true, seed: 7803);
                body.Kinds[1] = body.Kinds[2] = BraceOrderKind.Advance; // both advancing - a NEW charge order comes at 180 s
                double t = 105000;
                var a = new List<FakeCourageMan>();
                var b = new List<FakeCourageMan>();
                for (int k = 0; k < 3; k++)
                    for (int f = 0; f < 14; f++)
                    {
                        a.Add(body.ByIndex[FearMan(logic, body, 52000 + k * 14 + f, -6.5 + f, -k * 1.0, 0, 1, legionary: true, morale: 70, bar: 1.0).AgentIndex]);
                        b.Add(body.ByIndex[FearMan(logic, body, 52100 + k * 14 + f, -6.0 + f, -1.0 + k * 1.0, 1, 2, legionary: true, morale: 70, bar: 1.0).AgentIndex]);
                    }
                double startGap = MenGap(a, b);
                var dice = new Random(783);
                var rs = logic.RestStats;
                var was = new Dictionary<FakeCourageMan, bool>();
                var starts = new List<double>();
                var rested = new HashSet<FakeCourageMan>();
                double worstBehind = 0, chargeAt = t + 180, restingAtOrder = -1, restingAfter = -1;
                long restLooks = 0, farLooks = 0, walkFar = 0;
                long towardEnemy = 0, forward = 0, running = 0, walkFrames = 0, endsBefore = 0;
                t = RunGrind(logic, body, t, 360, 0.008, dice, lines: true, each: now =>
                {
                    foreach (var m in body.Men)
                    {
                        bool r = m.St.Rhythm.Resting;
                        if (r && (!was.TryGetValue(m, out bool w) || !w))
                        {
                            starts.Add(now);
                            rested.Add(m);
                        }
                        was[m] = r;
                        if (r && !m.St.Rhythm.RestWall)
                        {
                            double behind = BehindOwnFront(m, m.Team == 0 ? a : b);
                            worstBehind = Math.Max(worstBehind, behind);
                            restLooks++;
                            if (behind > 5.0) farLooks++;
                            var inp = m.St.Input;
                            if (inp != null && inp.RestMove && (inp.RestX != 0 || inp.RestY != 0) && logic.RhythmUnits.BehindFront(m.St.Rhythm.Key, m.X, m.Y, now) > 4.0 + 0.1) walkFar++;
                        }
                    }
                    if (restingAtOrder < 0 && now >= chargeAt)
                    {
                        restingAtOrder = body.Men.Count(m => m.St.Rhythm.Resting);
                        endsBefore = rs.Ends.Sum();
                        body.Kinds[1] = body.Kinds[2] = BraceOrderKind.Charge; // the general's NEW charge order
                    }
                    else if (restingAfter < 0 && restingAtOrder >= 0 && now >= chargeAt + 1.0)
                    {
                        restingAfter = body.Men.Count(m => m.St.Rhythm.Resting);
                        endsBefore = rs.Ends.Sum() - endsBefore; // every rest that ended in that second (his own mark, contagion, a swap)
                    }
                }, frame: (m, vx, vy) =>
                {
                    var s = m.St.Input!;
                    if (!m.St.Rhythm.Resting || !s.RestMove || s.Backpedal || s.WallPull || s.Flock) return;
                    walkFrames++;
                    var (d, ux, uy) = NearestEnemy(body, m);
                    if (!double.IsNaN(d) && vx * ux + vy * uy > 1e-4) towardEnemy++; // the nearest enemy may stand beside him in a mesh - counted, not a failure
                    if ((m.Team == 0 ? vy : -vy) > 1e-4) forward++;                  // toward the enemy's side: never - he steps straight back
                    if (Math.Sqrt(vx * vx + vy * vy) > RestMath.WalkStick + 1e-4) running++;
                });
                // the most rests begun in any 2 s
                starts.Sort();
                int burst = 0;
                for (int i = 0, j = 0; i < starts.Count; i++)
                {
                    while (starts[i] - starts[j] > 2.0) j++;
                    burst = Math.Max(burst, i - j + 1);
                }
                Console.WriteLine("        step 78c a MESHED six-minute grind (14 x 3 a side, meshed " + F2s(startGap) + " m): rests " + rs.Started + " by " + rested.Count
                                  + " of 84 men, the most begun in any 2 s " + burst + "; lasted mean " + F2s(rs.SecondsN > 0 ? rs.SecondsSum / rs.SecondsN : double.NaN)
                                  + " s; back in by his own mark " + rs.Ends[(int)RestIn.OwnMark] + ", by contagion " + rs.Ends[(int)RestIn.Contagion] + " (rolls " + rs.ContagionRolls
                                  + "), rotated " + rs.Ends[(int)RestIn.Rotated] + "; stepped back mean " + F2s(rs.BackN > 0 ? rs.BackSum / rs.BackN : double.NaN) + " / p90 "
                                  + F2s(rs.BackQuantile(0.9)) + " / max " + F2s(rs.BackMax) + " m; the farthest a resting man stood behind his own front " + F2s(worstBehind)
                                  + " m (the logic's " + F2s(rs.BehindFrontMax) + "), resting looks more than 5 m behind it " + farLooks + " of " + restLooks + ", rest walks while 4 m+ behind it (the logic's front) " + walkFar + "; walk frames " + walkFrames + " - toward the enemy's side " + forward + ", closing on the nearest enemy NOW (a mesh: one beside or behind him) " + towardEnemy + ", running " + running
                                  + "; turned away " + rs.TurnedAway + " of " + rs.FacingSamples + "; resting at the new charge order " + restingAtOrder + ", a second later "
                                  + restingAfter + " (" + endsBefore + " ended by their own mark / contagion / a swap meanwhile)");
                Check(startGap < 0, "precondition: the grind is not meshed (" + startGap + ")");
                Check(rs.Started >= 40 && rested.Count >= 40, "too few rests in a six-minute grind: " + rs.Started + " by " + rested.Count + " men");
                Check(burst <= 20, "rests came in a wave: " + burst + " begun within 2 s (staggered rests wanted)");
                // no walk-off: the rest never WALKS a man more than 4 m behind his own front (nor 4 m from his start, below); a man can still find himself
                // farther behind when his own line pushes on or an enemy breaks into it (the stand-in has no collisions - men slip through each other) - rare
                Check(walkFar == 0, "a resting man was walked back while " + walkFar + " frames 4 m+ behind his front (no walk-off wanted)");
                Check(restLooks > 1000 && farLooks <= restLooks / 20 && worstBehind <= 8.0, "resting men stood more than 5 m behind their front in " + farLooks + " of " + restLooks
                      + " looks (5% allowed), the farthest " + F2s(worstBehind) + " m (8 allowed - never by the rest's own walk, checked above)");
                Check(rs.BackMax <= 4.0 + 0.5, "a man backed " + F2s(rs.BackMax) + " m (the limit 4 m)");
                Check(walkFrames > 100 && forward == 0 && running == 0, "the walk back: " + walkFrames + " frames, toward the enemy's side " + forward + ", running " + running);
                Check(rs.FacingSamples > 50 && rs.TurnedAway == 0, "facing: " + rs.TurnedAway + " turned away of " + rs.FacingSamples);
                Check(rs.Ends[(int)RestIn.OwnMark] >= 10 && rs.Ends[(int)RestIn.Contagion] >= 1, "back in: own mark " + rs.Ends[(int)RestIn.OwnMark] + ", contagion " + rs.Ends[(int)RestIn.Contagion]);
                Check(rs.Ends[(int)RestIn.SwitchedOff] + rs.Ends[(int)RestIn.Excluded] + rs.Ends[(int)RestIn.Error] == 0, "rests ended by something else: "
                      + string.Join("/", rs.Ends));
                Check(restingAtOrder > 0 && restingAfter >= restingAtOrder - endsBefore, "a new charge order dragged resting men in (" + restingAtOrder + " → " + restingAfter + ")");
                LogHas("[rhythm] first REST at ");
                LogHas("[rhythm] first resting man WALKED BACK at ");
                LogHas("[rhythm] first man BACK IN after a rest at ");
                LogHas("[rhythm] first man IN BY CONTAGION at ");
                logic.WriteRhythmSummary();
                LogHas("[summary] rest (step 78c) - EACH MAN ON HIS OWN ON: rests started ");
                LogHas("[summary] rest (step 78c) - THE NATURAL GAP between engaged fronts");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void BothSidesTiredAGapAppears()
        {
            var keep = _logic;
            try
            {
                // ---- both sides spent: a gap by itself, the diagnostic counts it for both sides
                var body = new FakeCourageBody();
                var rhythm = new FakeRhythmBody { PlayerTeam = 0 };
                var logic = NewStep78cLogic(body, rhythm, rotation: false, seed: 7804);
                body.Orders[1] = body.Orders[2] = FearOrder.Charge;
                double t = 106000;
                var a = RhythmLine(logic, body, 53000, 0, 1, 12, 3, 0, -1, 0.15, 0.15);
                var b = RhythmLine(logic, body, 53100, 1, 2, 12, 3, 1.0, 1, 0.15, 0.15);
                double startGap = MenGap(a, b);
                var dice = new Random(784);
                t = RunGrind(logic, body, t, 15, 0.008, dice, lines: true);
                double gap = MenGap(a, b);
                var rs = logic.RestStats;
                Console.WriteLine("        step 78c BOTH SIDES TIRED (15%): the gap " + F2s(startGap) + " → " + F2s(gap) + " m (the smoke's own), rests " + rs.Started
                                  + ", the diagnostic - your side " + rs.GapClear[1] + " of " + rs.GapLooks[1] + " looks with 2 m+, the other " + rs.GapClear[0] + " of " + rs.GapLooks[0]);
                Check(gap >= 2.5, "both sides tired: no gap appeared (" + F2s(gap) + " m)");
                Check(rs.GapClear[0] > 0 && rs.GapClear[1] > 0, "the diagnostic did not count the gap for both sides");
                Check(rs.BackMax <= 4.5, "both sides tired: a man backed " + F2s(rs.BackMax) + " m");

                // ---- one side spent: the fresh side follows and presses; the tired men back at most 4 m, then stand and defend
                body = new FakeCourageBody();
                rhythm = new FakeRhythmBody();
                logic = NewStep78cLogic(body, rhythm, rotation: false, seed: 7805);
                body.Orders[1] = body.Orders[2] = FearOrder.Charge;
                t = 106500;
                a = RhythmLine(logic, body, 53200, 0, 1, 12, 3, 0, -1, 0.15, 0.15);
                b = RhythmLine(logic, body, 53300, 1, 2, 12, 3, 1.0, 1, 1.0, 1.0);
                var aStart = a.ToDictionary(m => m, m => (m.X, m.Y));
                double bFront0 = b.Min(m => m.Y);
                t = RunGrind(logic, body, t, 20, 0.0, new Random(785), lines: true);
                rs = logic.RestStats;
                double farthest = a.Max(m => Math.Sqrt((m.X - aStart[m].X) * (m.X - aStart[m].X) + (m.Y - aStart[m].Y) * (m.Y - aStart[m].Y)));
                double bFront = b.Min(m => m.Y);
                Console.WriteLine("        step 78c ONE SIDE TIRED: the fresh front moved " + F2s(bFront0 - bFront) + " m forward (pressing), the tired men backed at most "
                                  + F2s(farthest) + " m from where they stood (the limit 4 m), still resting " + a.Count(m => m.St.Rhythm.Resting) + " of 36, the fresh men resting "
                                  + b.Count(m => m.St.Rhythm.Resting));
                Check(bFront0 - bFront >= 2.0, "one side tired: the fresh side did not follow (" + F2s(bFront0 - bFront) + " m)");
                Check(farthest <= 4.0 + 0.5, "one side tired: a tired man backed " + F2s(farthest) + " m");
                Check(b.All(m => !m.St.Rhythm.Resting), "one side tired: a fresh man rested");

                // ---- the input frame: the rest's walk beats the press's, the swap's, courage's and the block's; the backpedal, the wall's pull and the flock beat it
                var probe = a[0].St;
                var s = probe.Input!;
                bool keepRest = s.RestMove, keepHold = s.RestHoldAttacks, keepRhythm = s.Rhythm;
                s.Rhythm = true;
                s.RestMove = true;
                s.RestX = 0.1f;
                s.RestY = -0.5f;
                s.Press = s.PressMove = true;
                s.PressX = 1f;
                s.PressY = 0f;
                s.RhythmMove = true;
                s.RhythmX = 0f;
                s.RhythmY = 1f;
                s.Regroup = true;
                s.Block = true;
                uint fl = AiInputMath.AttackMask & 0x40;
                float vx = 0.5f, vy = 0.5f;
                s.RestHoldAttacks = true;
                AiInputHook.Apply(probe, s, false, ref fl, ref vx, ref vy);
                Check(Math.Abs(vx - 0.1f) < 1e-4 && Math.Abs(vy + 0.5f) < 1e-4, "the input frame: the rest's walk did not win over the press / swap / courage / block (" + vx + ", " + vy + ")");
                Check((fl & AiInputMath.AttackMask) == 0 && (fl & AiInputMath.DefendDown) != 0, "the input frame: a resting man's attack not held / no guard");
                s.Flock = true;
                s.FlockX = 1f;
                s.FlockY = 0f;
                fl = 0;
                vx = vy = 0.5f;
                AiInputHook.Apply(probe, s, false, ref fl, ref vx, ref vy);
                Check(!(Math.Abs(vx - 0.1f) < 1e-4 && Math.Abs(vy + 0.5f) < 1e-4), "the input frame: the rest's walk beat the fear flock");
                s.Flock = false;
                s.Backpedal = true;
                s.BackX = -1f;
                s.BackY = 0f;
                vx = vy = 0.5f;
                AiInputHook.Apply(probe, s, false, ref fl, ref vx, ref vy);
                Check(Math.Abs(vx + 1f) < 1e-4, "the input frame: the rest's walk beat a step back's backpedal");
                s.Backpedal = false;
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                fl = AiInputMath.AttackMask & 0x40;
                vx = vy = 0.5f;
                AiInputHook.Apply(probe, s, false, ref fl, ref vx, ref vy);
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                Check(Math.Abs(vx - 0.1f) > 1e-4 && (fl & AiInputMath.AttackMask) != 0, "the input frame: the rest acts with the master switch off");
                s.Press = s.PressMove = s.RhythmMove = s.Regroup = s.Block = false;
                s.RestMove = keepRest;
                s.RestHoldAttacks = keepHold;
                s.Rhythm = keepRhythm;

                // ---- the switch off: every rest ends at once, the wishes let go
                Check(a.Any(m => m.St.Rhythm.Resting), "precondition: nobody rests before the switch goes off");
                S.Set(SettingsSchema.RestEnabled, false, SettingSources.Mcm);
                logic.TickCourage(t += 0.1);
                Check(body.Men.All(m => !m.St.Rhythm.Resting && (m.St.Input == null || (!m.St.Input.RestMove && !m.St.Input.RestHoldAttacks))), "RestEnabled off: a rest still on");
                LogHas("[rhythm] the rest switched OFF at ");
                t = RunGrind(logic, body, t, 2, 0.0, new Random(786));
                Check(body.Men.All(m => !m.St.Rhythm.Resting), "RestEnabled off: a man started a rest");
                Check(logic.RestStats.WouldRestOff > 0, "RestEnabled off: the tired men at the fight not counted");
            }
            finally
            {
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ShieldWallRestsInItsSlots()
        {
            var keep = _logic;
            try
            {
                var body = new FakeCourageBody();
                var rhythm = new FakeRhythmBody();
                var logic = NewStep78cLogic(body, rhythm, rotation: true, seed: 7806);
                body.Orders[2] = FearOrder.Charge;
                double t = 107000;
                // a 12 x 2 held wall facing north, fresh at first; a fresh enemy 12 x 3 grinding it down
                var wall = RhythmLine(logic, body, 54000, 0, 1, 12, 2, 0, -1, 0.9, 0.9);
                foreach (var m in wall) m.Gate = CourageSkip.WallHeld;
                var foe = RhythmLine(logic, body, 54100, 1, 2, 12, 3, 1.0, 1, 1.0, 1.0);
                double worstOut = 0;
                long restingLooks = 0, walked = 0;
                var dice = new Random(787);
                t = RunGrind(logic, body, t, 150, 0.01, dice, lines: true, each: now =>
                {
                    foreach (var m in wall)
                    {
                        var rm = m.St.Rhythm;
                        if (!rm.Resting || rm.InSwap) continue;
                        restingLooks++;
                        double d = Math.Sqrt((m.X - m.SlotX) * (m.X - m.SlotX) + (m.Y - m.SlotY) * (m.Y - m.SlotY));
                        if (d > worstOut) worstOut = d;
                        if (m.St.Input != null && m.St.Input.RestMove) walked++;
                    }
                    foreach (var f in foe) SetBar(f.St, 1.0); // the enemy stays fresh
                }, charging: m => m.Team == 1);
                var rs = logic.RestStats;
                int outNow = wall.Count(m => m.St.Rhythm.Resting && !m.St.Rhythm.InSwap && Math.Sqrt((m.X - m.SlotX) * (m.X - m.SlotX) + (m.Y - m.SlotY) * (m.Y - m.SlotY)) > 0.5);
                var resting = wall.FirstOrDefault(m => m.St.Rhythm.Resting);
                bool leash0 = resting != null && (bool)typeof(AthleticsLogic).GetMethod("RestingInWall", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(null, new object[] { resting.St })!;
                Console.WriteLine("        step 78c A SHIELD WALL (12 x 2 held) ground down by a fresh enemy for 150 s: rests in the slots " + rs.StartedInWall + " (of " + rs.Started
                                  + "), resting looks " + restingLooks + "; MEN MORE THAN 0.5 m OUT OF THEIR SLOTS BECAUSE OF RESTING: " + outNow + " now, the farthest any resting wall man "
                                  + "stood from his slot " + F2s(worstOut) + " m (the logic's: " + rs.WallOut + " of " + rs.WallLooks + " looks), rest walks " + walked
                                  + "; swaps " + logic.RhythmStats.Started + "; the leash 0 for a resting wall man " + leash0);
                Check(rs.StartedInWall >= 10, "the wall: only " + rs.StartedInWall + " rests in the slots");
                Check(outNow == 0 && worstOut <= 0.5 && rs.WallOut == 0, "the wall: a resting man left his slot (" + F2s(worstOut) + " m, " + rs.WallOut + " looks)");
                Check(rs.WallLooks > 0, "the wall: the slots were never measured");
                Check(walked == 0, "the wall: a resting wall man was walked (" + walked + " frames)");
                Check(leash0, "the wall: a resting wall man's leash is not 0");
                LogHas("[rhythm] first REST IN A WALL SLOT at ");
                logic.WriteRhythmSummary();
                LogHas("WALL men resting in their slots: ");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Step78cCostStaysFlat()
        {
            var keep = _logic;
            try
            {
                var body = new FakeCourageBody();
                var rhythm = new FakeRhythmBody();
                var logic = NewStep78cLogic(body, rhythm, rotation: true, seed: 7807);
                body.Orders[1] = body.Orders[2] = FearOrder.Charge;
                double t0 = 108000;
                for (int k = 0; k < 20; k++)
                    for (int f = 0; f < 25; f++)
                    {
                        FearMan(logic, body, 55000 + k * 25 + f, f - 12, -k * 1.0, 0, 1, legionary: true, morale: 70, bar: 0.15);
                        FearMan(logic, body, 56000 + k * 25 + f, f - 12, 1.0 + k * 1.0, 1, 2, legionary: true, morale: 70, bar: 0.15);
                    }
                for (int k = 0; k < 3; k++) logic.TickCourage(t0 += 0.5); // the rests begun, the arrays allocated
                int resting = body.Men.Count(m => m.St.Rhythm.Resting);
                const int ticks = 300;
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var rs = logic.RhythmStats;
                var ss = logic.RestStats;
                double c0 = rs.LookCostMs + rs.SteerCostMs + rs.PublishCostMs;
                long t0Ticks = rs.Ticks;
                var sw = Stopwatch.StartNew();
                for (int k = 1; k <= ticks; k++) logic.TickCourage(t0 + k / 60.0);
                sw.Stop();
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double own = (rs.LookCostMs + rs.SteerCostMs + rs.PublishCostMs - c0) / Math.Max(1, rs.Ticks - t0Ticks);
                Console.WriteLine("        step 78c cost: 1000 men, " + resting + " resting (" + logic.RhythmMenNow + " with a rhythm wish) - the courage tick "
                                  + (sw.Elapsed.TotalMilliseconds / ticks).ToString("0.000", CultureInfo.InvariantCulture) + " ms (the stand-in's scans are plain loops), the rhythm's own "
                                  + "(the rest inside it) " + own.ToString("0.000", CultureInfo.InvariantCulture) + " ms a tick, " + allocated + " bytes over " + ticks + " ticks; the rest's looks "
                                  + ss.Looks + ", steers " + ss.Steers);
                Check(resting >= 100, "cost: the tired blocks did not rest (" + resting + ")");
                Check(own < 0.5, "cost: the rhythm's own " + own + " ms a tick (budget 0.5)");
                Check(allocated < 256 * 1024, "cost: step 78c allocates - " + allocated + " bytes over " + ticks + " ticks");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        /// <summary>The master-switch step's part: off = every rest let go at once; on = tired men rest again.</summary>
        private static void RestFollowsTheMasterSwitch()
        {
            var keep = _logic;
            try
            {
                var body = new FakeCourageBody();
                var rhythm = new FakeRhythmBody();
                var logic = NewStep78cLogic(body, rhythm, rotation: false, seed: 7808);
                double t = 108500;
                var a = RhythmLine(logic, body, 57000, 0, 1, 8, 2, 0, -1, 0.15, 0.15);
                RhythmLine(logic, body, 57100, 1, 2, 8, 2, 1.0, 1, 0.9, 0.9);
                for (int k = 0; k < 3; k++) logic.TickCourage(t += 0.5);
                Check(a.Any(m => m.St.Rhythm.Resting), "precondition: nobody rests (step 78c)");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                logic.TickCourage(t += 0.1);
                Check(a.All(m => !m.St.Rhythm.Resting && (m.St.Input == null || (!m.St.Input.RestMove && !m.St.Input.RestHoldAttacks))), "mod off: a rest still on (step 78c)");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                for (int k = 0; k < 3; k++) logic.TickCourage(t += 0.5);
                Check(a.Any(m => m.St.Rhythm.Resting), "mod back on: nobody rests again (step 78c)");
            }
            finally
            {
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }
    }
}

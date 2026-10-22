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
    /// Step 77 (Toni's battles 2026-10-22 10:2x-10:48: 108 T1 recruits against his 59 T5 shield wall - at contact the AI's tactical charge, the rear
    /// ranks walking round his wall's ends and behind it, then through the middle): PRESS, DON'T PASS through the real logic with courage's stand-in
    /// world - a 6-deep, 108-man block (D 6, 18 files, 26.6 m) CHARGING a 23 m two-deep shield wall, its men driven by a stand-in of the game's charge
    /// (each at his nearest enemy, the flank files round the wall's ends to its rear, half its rear ranks breaking off round the ends and in behind the
    /// wall - the "dance"), every man's input through the REAL filter (AiInputHook.Apply - the block's vector, the press's walk and clamp): OFF men
    /// get behind the wall, ON every man stays on his side; a real 3 m gap lets men through; a genuinely wider unit wraps as a body (a lone straggler
    /// does not); the input frame's priority; the cost for 1000 men. What it cannot check: how the engine walks the men (PLAYTEST D42).
    /// </summary>
    internal static partial class Program
    {
        private const double WallFrontY = 0, WallHalf = 11.6; // the wall's front rank at y = 0 facing SOUTH, its men at x = −11.2..11.2

        private static void Step77Defaults()
        {
            S.Set(SettingsSchema.PressDontPass, true, SettingSources.File);
            S.Set(SettingsSchema.PressToleranceMetres, 0.5, SettingSources.File);
            S.Set(SettingsSchema.PressWatchMetres, 4.0, SettingSources.File);
            S.Set(SettingsSchema.PressGapMetres, 2.0, SettingSources.File);
            S.Set(SettingsSchema.PressManWidthMetres, 1.2, SettingSources.File);
            S.Set(SettingsSchema.PressFrontDepthMetres, 1.5, SettingSources.File);
            S.Set(SettingsSchema.PressWrapMarginMetres, 6.0, SettingSources.File);
            S.Set(SettingsSchema.PressWrapMates, 5, SettingSources.File);
            S.Set(SettingsSchema.PressWrapMatesMetres, 4.0, SettingSources.File);
            S.Set(SettingsSchema.PressRoundEndMetres, 2.0, SettingSources.File);
            S.Set(SettingsSchema.PressWinPercent, 50.0, SettingSources.File);
            S.Set(SettingsSchema.PressLineMinMen, 8, SettingSources.File);
            S.Set(SettingsSchema.PressLineInSlotPercent, 60.0, SettingSources.File);
            S.Set(SettingsSchema.PressBandMarginMetres, 1.0, SettingSources.File);
            S.Set(SettingsSchema.PressBlockEndMetres, 0.0, SettingSources.File);
            S.Set(SettingsSchema.PressEndSlackMetres, 1.0, SettingSources.File);
            S.Set(SettingsSchema.PressWrapAllowed, true, SettingSources.File);   // step 78b: step 77's own wrap (78b's smoke turns it off)
            S.Set(SettingsSchema.PressWingBendDegrees, 0.0, SettingSources.File); // step 78b: step 77's straight front
        }

        private static AthleticsLogic NewStep77Logic(FakeCourageBody body, FakeBlockBody block, bool press)
        {
            AthleticsDefaults();
            SprintDefaults();
            FeverDefaults();
            CourageDefaults();
            FearDefaults();
            Step70Defaults();
            Step74Defaults();
            Step77Defaults(); // before the logic: its mission-start line says step 77
            S.Set(SettingsSchema.PressDontPass, press, SettingSources.File);
            S.Set(SettingsSchema.CourageEnabled, false, SettingSources.File); // the press and the block alone (courage measures)
            S.Set(SettingsSchema.FearFlockEnabled, false, SettingSources.File);
            var logic = NewCourageLogic(body);
            block.World = body;
            logic.BlockBody = block;
            return logic;
        }

        /// <summary>Toni's wall: formation 2 of your side, 29 files x 2 ranks 0.8 m apart, its front at y = 0 facing south, held by step 41 (each man in his
        /// slot); <paramref name="fallen"/> files from <paramref name="at"/> gone.</summary>
        private static List<FakeCourageMan> ToniWall(AthleticsLogic logic, FakeCourageBody body, int index, int fallen = 0, int at = 13)
        {
            body.Orders[2] = FearOrder.Wall;
            body.Facings[2] = (0, -1);
            var wall = new List<FakeCourageMan>();
            for (int f = 0; f < 29; f++)
            {
                if (f >= at && f < at + fallen) continue;
                for (int k = 0; k < 2; k++)
                {
                    var st = FearMan(logic, body, index + f * 2 + k, -11.2 + f * 0.8, WallFrontY + k * 1.0, 0, 2, legionary: true, morale: 70, bar: 1.0);
                    var m = body.ByIndex[st.AgentIndex];
                    m.Gate = CourageSkip.WallHeld;
                    wall.Add(m);
                }
            }
            body.EnemyFronts[2] = (0, -26, 0, 1, 26.6, 7.2);
            return wall;
        }

        /// <summary>The recruits: formation 1 of the other side, 108 men in 18 files x 6 ranks (1.5 m / 1.2 m), their front <paramref name="front"/> m south
        /// of the wall, on a CHARGE; step 73's cap D 6, 18 files, 26.6 m (the block hold holds them); the wall read as their enemy front.</summary>
        private static List<FakeCourageMan> Recruits108(AthleticsLogic logic, FakeCourageBody body, FakeBlockBody block, int index, double front = 14)
        {
            body.Orders[1] = FearOrder.Charge;
            body.Facings[1] = (0, 1);
            var men = new List<FakeCourageMan>();
            for (int f = 0; f < 18; f++)
                for (int k = 0; k < 6; k++)
                {
                    var st = FearMan(logic, body, index + f * 6 + k, -12.75 + f * 1.5, WallFrontY - front - k * 1.2, 1, 1, legionary: false, morale: 70, bar: 1.0, slot: false);
                    men.Add(body.ByIndex[st.AgentIndex]);
                }
            body.EnemyFronts[1] = (0, 0.5, 0, -1, 23.2, 1.6);
            block.Caps[1] = (6, 18, 26.6, 108);
            return men;
        }

        /// <summary>The stand-in of the GAME's charge (4 m/s): each man at his nearest enemy; within 1.2 m of one he fights (stands); a DANCER (and a man
        /// beside the wall's end) heads round the nearer end to its rear and in behind it; a man of <paramref name="through"/> heads through it at x = the
        /// gap's middle. Every man's vector goes through the REAL input filter (AiInputHook.Apply) whenever a wish of ours is on him - the stand-in frame is
        /// the world's. The wall stands. Returns the time at the end.</summary>
        private static double RunPress(AthleticsLogic logic, FakeCourageBody body, List<FakeCourageMan> attackers, List<FakeCourageMan> wall, double t, double seconds,
            HashSet<FakeCourageMan> dancers, HashSet<FakeCourageMan>? through = null, double gapX = 0, Action<double>? midway = null)
        {
            const double speed = 4, dt = 0.1;
            for (double s = 0; s < seconds - 1e-9; s += dt)
            {
                foreach (var m in attackers)
                {
                    FakeCourageMan? near = null;
                    double best = double.MaxValue;
                    foreach (var e in wall)
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
                    if (through != null && through.Contains(m))
                    {
                        tx = gapX; // through the gap to the wall's rear
                        ty = 5;
                        fights = false;
                    }
                    else if (dancers.Contains(m) || Math.Abs(m.X) > WallHalf + 0.6)
                    {
                        fights = false;
                        double side = m.X >= 0 ? 1 : -1;
                        if (m.Y < WallFrontY + 2.5 && Math.Abs(m.X) < WallHalf + 2.5)
                        {
                            tx = side * (WallHalf + 3); // round the end
                            ty = m.Y < WallFrontY - 3 ? m.Y + 2 : WallFrontY + 3;
                        }
                        else
                        {
                            tx = side * 4; // in behind the wall
                            ty = WallFrontY + 3;
                        }
                    }
                    double dx = tx - m.X, dy = ty - m.Y, len = Math.Sqrt(dx * dx + dy * dy);
                    float vx = 0, vy = 0; // fighting: the game stands him (our wishes may still walk him)
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
                midway?.Invoke(s);
            }
            return t;
        }

        /// <summary>Men past the wall's front (beyond the tolerance + one step) and men BEHIND it (past its rear rank by 1 m).</summary>
        private static (int past, int behind, double deepest) PastTheWall(List<FakeCourageMan> men, double minX = double.NegativeInfinity, double maxX = double.PositiveInfinity)
        {
            int past = 0, behind = 0;
            double deepest = 0;
            foreach (var m in men)
            {
                if (m.X < minX || m.X > maxX) continue;
                double by = m.Y - WallFrontY;
                if (by > deepest) deepest = by;
                if (by > 0.5 + 0.45) past++;
                if (by > 2.0) behind++;
            }
            return (past, behind, deepest);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PressThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                // ---- 1. OFF: the game's charge and the dance - men get behind the wall, round its ends
                var body = new FakeCourageBody();
                var block = new FakeBlockBody();
                var logic = NewStep77Logic(body, block, press: false);
                double t = 90000;
                var wall = ToniWall(logic, body, 30000);
                var recruits = Recruits108(logic, body, block, 30200);
                var dancers = new HashSet<FakeCourageMan>(recruits.Where((m, i) => (i % 6) >= 3 && (i / 6) % 2 == 0)); // half the rear ranks
                foreach (var m in dancers) body.Targets[m.St.AgentIndex] = (Math.Sign(m.X) * 4, WallFrontY + 1, LoneTarget.Other); // their targets: the wall's rear rank
                t = RunPress(logic, body, recruits, wall, t, 14, dancers);
                var off = PastTheWall(recruits);
                var offStats = logic.PressStats;
                long offCross = 0;
                for (int w = 0; w < 2; w++)
                    for (int c = 0; c < (int)PressCause.Count; c++)
                        offCross += offStats.Crossings[0, w, c, 0] + offStats.Crossings[0, w, c, 1];
                Console.WriteLine("        step 77 OFF (the game's charge + the dance): " + off.past + " recruits past the wall's front, " + off.behind + " behind it (the deepest "
                                  + F2s(off.deepest) + " m); crossings counted " + offCross + " (round an end " + Sum(offStats, 0, 0) + ", through the middle " + Sum(offStats, 0, 1) + ")");
                Check(off.behind >= 10 && offCross >= 10, "the stand-in charge put nobody behind the wall (the A / B): " + off.behind + " behind, " + offCross + " crossings");
                Check(logic.PressingNow == 0 && offStats.NearLooks[0] > 0, "OFF: the press steered, or measured nothing (" + logic.PressingNow + ")");
                Check(offStats.Crossings[0, 0, (int)PressCause.ChargeTarget, 0] + offStats.Crossings[0, 0, (int)PressCause.ChargeTarget, 1]
                      + offStats.Crossings[0, 0, (int)PressCause.Block, 0] + offStats.Crossings[0, 0, (int)PressCause.Block, 1] > 0,
                    "OFF: no crossing round an end was put down to the dancers' targets (or the block's walk)");
                Check(logic.PressLines.Kind[FearMath.UnitKey(0, 2)] == PressLineKind.Wall, "OFF: the wall was not published as a line held by step 41: " + logic.PressLines.Kind[FearMath.UnitKey(0, 2)]);
                LogHas("[press] first man CROSSED an enemy line's front with the press OFF at ");

                // ---- 2. ON: the same charge and dance - every man stays on his side of the wall, pressing its front
                body = new FakeCourageBody();
                block = new FakeBlockBody();
                logic = NewStep77Logic(body, block, press: true);
                LogHas("[press] mission start: press, don't pass (step 77): an AI man on foot fighting an enemy LINE");
                t = 91000;
                wall = ToniWall(logic, body, 31000);
                recruits = Recruits108(logic, body, block, 31200);
                dancers = new HashSet<FakeCourageMan>(recruits.Where((m, i) => (i % 6) >= 3 && (i / 6) % 2 == 0));
                foreach (var m in dancers) body.Targets[m.St.AgentIndex] = (Math.Sign(m.X) * 4, WallFrontY + 1, LoneTarget.Other);
                int worstPast = 0;
                double worstDeep = 0;
                t = RunPress(logic, body, recruits, wall, t, 14, dancers, midway: s =>
                {
                    var p = PastTheWall(recruits);
                    if (p.past > worstPast) worstPast = p.past;
                    if (p.deepest > worstDeep) worstDeep = p.deepest;
                });
                var on = PastTheWall(recruits);
                var st = logic.PressStats;
                long onCross = 0;
                for (int w = 0; w < 2; w++)
                    for (int c = 0; c < (int)PressCause.Count; c++)
                        onCross += st.Crossings[1, w, c, 0];
                int atFront = recruits.Count(m => m.Y > WallFrontY - 1.5);
                long bit = recruits.Sum(m => m.St.Input?.PressClampFrames ?? 0), sideBit = recruits.Sum(m => m.St.Input?.PressSideFrames ?? 0);
                Console.WriteLine("        step 77 ON (press, don't pass): " + on.past + " past the front at the end, " + on.behind + " behind it (at worst over the run " + worstPast
                                  + " past, the deepest " + F2s(worstDeep) + " m); " + atFront + " men pressing within 1.5 m of its front; crossings not allowed " + onCross
                                  + "; wishes " + st.Wishes + " by " + st.Men + " men, the clamp bit " + bit + " input frames (the front) + " + sideBit + " (a rear-rank man's side), pushed "
                                  + st.PushedMen + ", walked round " + st.ReturnedMen + "; the block's range cut " + st.BlockRangeLooks + " looks, the band clamped "
                                  + logic.FearUnits.PressBandClamps);
                Check(worstPast == 0 && on.behind == 0, "ON: men got past the wall's front: at worst " + worstPast + " past, the deepest " + F2s(worstDeep) + " m, " + on.behind + " behind it at the end");
                Check(onCross == 0, "ON: " + onCross + " men crossed the front without a gap or a wrap");
                Check(atFront >= 20, "ON: the recruits did not press the wall (" + atFront + " within 1.5 m of its front)");
                Check(st.Men > 0 && logic.PressingNow > 0, "ON: nobody pressed: " + st.Men + " men, " + logic.PressingNow + " now");
                Check(st.Actions[1, (int)PressAction.Clamp] > 0 && bit > 0, "ON: no clamp verdict, or the clamp never bit (" + bit + " frames)");
                Check(logic.PressLines.LineOf(FearMath.UnitKey(1, 1), t) == FearMath.UnitKey(0, 2), "ON: the recruits' enemy line is not the wall");
                Check(!logic.PressLines.Wraps[FearMath.UnitKey(1, 1)], "ON: the recruits (26.6 m on a 23 m wall) were let round as a wider mass");
                LogHas("[press] first enemy LINE at ");
                LogHas("[press] first man PRESSING an enemy line at ");
                // the live switch: off = every wish let go at once
                S.Set(SettingsSchema.PressDontPass, false, SettingSources.Mcm);
                logic.TickCourage(t += 0.1);
                Check(logic.PressingNow == 0 && recruits.All(m => m.St.Input == null || !m.St.Input.Press), "PressDontPass off live: men still pressed");
                LogHas("switched OFF mid-mission at");
                S.Set(SettingsSchema.PressDontPass, true, SettingSources.Mcm);
                logic.WriteCourageSummary();
                LogHas("[summary] press, don't pass (step 77): ON at the end - lines published ");
                LogHas("[summary] press, don't pass (step 77) - men who CROSSED an enemy line's front");

                // ---- 3. a man already BEHIND the wall is walked round its nearest end to his side
                body = new FakeCourageBody();
                block = new FakeBlockBody();
                logic = NewStep77Logic(body, block, press: true);
                t = 92000;
                wall = ToniWall(logic, body, 32000);
                recruits = Recruits108(logic, body, block, 32200, front: 1.2);
                var lost = recruits[recruits.Count - 1]; // the rightmost file's last man - put behind the wall's right half
                lost.X = 7;
                lost.Y = WallFrontY + 4;
                var all = new HashSet<FakeCourageMan>(new[] { lost });
                double startX = lost.X;
                t = RunPress(logic, body, recruits, wall, t, 12, new HashSet<FakeCourageMan>()); // he wants the wall's rear man (nearest) - the game's
                Console.WriteLine("        step 77 a man behind the wall: from (" + F2s(startX) + ", 4.00) to (" + F2s(lost.X) + ", " + F2s(lost.Y) + "); walked round "
                                  + logic.PressStats.ReturnedMen + " men, pushed " + logic.PressStats.PushedMen);
                Check(lost.Y < WallFrontY + 0.6 && Math.Abs(lost.X) > 8, "a man behind the wall was not walked round its end to his side: (" + F2s(lost.X) + ", " + F2s(lost.Y) + ")");
                Check(logic.PressStats.ReturnedMen >= 1, "the walk round the end was not counted");
                LogHas("[press] first man walked ROUND a line's end at ");
                _ = all;
            }
            finally
            {
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        private static long Sum(PressStats s, int on, int where)
        {
            long n = 0;
            for (int c = 0; c < (int)PressCause.Count; c++) n += s.Crossings[on, where, c, 0] + s.Crossings[on, where, c, 1];
            return n;
        }

        /// <summary>A REAL GAP (four files fallen - 2.8 m that no front man covers) lets the men facing it through, ON; a man behind the broken wall is left
        /// there. A GENUINELY WIDER unit (40 files, 60 m) wraps round the wall's ends as a body; a lone straggler of it does not.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PressGapsAndWraps()
        {
            var keep = _logic;
            try
            {
                // ---- the gap
                var body = new FakeCourageBody();
                var block = new FakeBlockBody();
                var logic = NewStep77Logic(body, block, press: true);
                double t = 93000;
                var wall = ToniWall(logic, body, 33000, fallen: 4, at: 13); // x = −0.8 .. 1.6 gone: front men at −1.6 and 2.4 → a gap (−1.0, 1.8) in x
                var recruits = Recruits108(logic, body, block, 33200, front: 6);
                double gapX = 0.4;
                var through = new HashSet<FakeCourageMan>(recruits.Where(m => Math.Abs(m.X - gapX) <= 1.0));
                t = RunPress(logic, body, recruits, wall, t, 10, new HashSet<FakeCourageMan>(), through, gapX);
                var inGap = PastTheWall(recruits, gapX - 1.5, gapX + 1.5);
                var elsewhere = PastTheWall(recruits.Where(m => !through.Contains(m)).ToList());
                var st = logic.PressStats;
                int key = FearMath.UnitKey(0, 2);
                int gaps = logic.PressLines.GapN[2 * key];
                double gw = gaps > 0 ? logic.PressLines.GapHi[2 * key * PressMath.MaxGaps] - logic.PressLines.GapLo[2 * key * PressMath.MaxGaps] : 0;
                Console.WriteLine("        step 77 a 3 m GAP (4 files fallen): the front's gaps " + gaps + " (" + F2s(gw) + " m); " + inGap.behind + " of " + through.Count
                                  + " men went through it (allowed crossings by the gap " + st.AllowedBy[1, 0] + "), " + elsewhere.past + " others past the front; through-a-gap looks "
                                  + st.Actions[1, (int)PressAction.Through]);
                Check(gaps == 1 && gw > 2.5 && gw < 3.1, "the fallen files did not make one ~2.8 m gap: " + gaps + " gaps, " + F2s(gw) + " m");
                Check(inGap.behind >= 3 && st.AllowedBy[1, 0] >= 3, "ON: the men facing the gap did not go through it: " + inGap.behind + " behind, " + st.AllowedBy[1, 0] + " allowed");
                Check(elsewhere.past == 0, "ON: men away from the gap got past the front: " + elsewhere.past);
                Check(st.ReturnedMen == 0, "ON: a man behind a BROKEN line was walked back (" + st.ReturnedMen + ")");
                LogHas("[press] first GAP in a line at ");

                // ---- the wrap: a 60 m unit (40 files x 3) on the 23 m wall - its flanks go round as a body
                body = new FakeCourageBody();
                block = new FakeBlockBody();
                logic = NewStep77Logic(body, block, press: true);
                t = 94000;
                wall = ToniWall(logic, body, 34000);
                body.Orders[3] = FearOrder.Charge;
                body.Facings[3] = (0, 1);
                var wide = new List<FakeCourageMan>();
                for (int f = 0; f < 40; f++)
                    for (int k = 0; k < 3; k++)
                    {
                        var s = FearMan(logic, body, 34200 + f * 3 + k, -29.25 + f * 1.5, WallFrontY - 6 - k * 1.2, 1, 3, legionary: false, morale: 70, bar: 1.0, slot: false);
                        wide.Add(body.ByIndex[s.AgentIndex]);
                    }
                var straggler = body.ByIndex[FearMan(logic, body, 34400, -50, WallFrontY - 6, 1, 3, legionary: false, morale: 70, bar: 1.0, slot: false).AgentIndex];
                wide.Add(straggler);
                body.EnemyFronts[3] = (0, 0.5, 0, -1, 23.2, 1.6);
                var noDance = new HashSet<FakeCourageMan>();
                t = RunPress(logic, body, wide, wall, t, 12, noDance);
                int unit = FearMath.UnitKey(1, 3);
                var flanks = wide.Where(m => m != straggler && m.Y > WallFrontY + 2.0 && Math.Abs(m.X) > WallHalf - 1).ToList();
                var middle = PastTheWall(wide.Where(m => m != straggler && Math.Abs(m.X) < WallHalf - 1).ToList());
                st = logic.PressStats;
                Console.WriteLine("        step 77 a WIDER unit (60 m on the 23 m wall): its width read " + F2s(logic.PressLines.UnitWidth[unit]) + " m, wraps "
                                  + logic.PressLines.Wraps[unit] + "; " + flanks.Count + " flank men round the ends behind the wall (wrap looks " + st.Actions[1, (int)PressAction.Wrap]
                                  + ", allowed crossings by the wrap " + st.AllowedBy[1, 0 + 1] + "), the middle " + middle.past + " past; the lone straggler at ("
                                  + F2s(straggler.X) + ", " + F2s(straggler.Y) + ")");
                Check(logic.PressLines.Wraps[unit] && logic.PressLines.WrapUnits >= 1, "the 60 m unit was not read as wider than the 23 m wall (or its wrap not kept): " + F2s(logic.PressLines.UnitWidth[unit]) + " m");
                Check(flanks.Count >= 10 && st.Actions[1, (int)PressAction.Wrap] > 0, "the wider unit's flanks did not go round as a body: " + flanks.Count);
                Check(middle.past == 0, "the wider unit's middle got through the wall: " + middle.past);
                Check(straggler.Y <= WallFrontY + 0.95, "the lone straggler went round the wall alone: (" + F2s(straggler.X) + ", " + F2s(straggler.Y) + ")");
                LogHas("[press] first man let ROUND a line's end with his mass at ");

                // ---- the input frame: the press's walk after the backpedal and the wall's pull, before the walk back / the flock / the block; the clamp LAST
                var s0 = new AiInputState { Press = true, PressMove = true, PressX = 1, PressY = 0, Block = true, BlockX = 0, BlockY = 1 };
                uint flags = 0;
                float vx = 0, vy = 1;
                Check(AiInputHook.ApplyPressMove(s0, ref flags, ref vx, ref vy) && vx == 1 && vy == 0, "the press's walk did not write its vector");
                s0.WallPull = true;
                S.Set(SettingsSchema.ShieldWallHoldsSlots, true, SettingSources.Mcm);
                Check(!AiInputHook.ApplyPressMove(s0, ref flags, ref vx, ref vy), "the press's walk beat the wall's pull");
                s0.WallPull = false;
                var s1 = new AiInputState { Press = true, PressFront = true, PressNX = 0, PressNY = 1 };
                vx = 0.6f;
                vy = 0.8f;
                Check(AiInputHook.ApplyPressClamp(s1, ref flags, ref vx, ref vy) && Math.Abs(vx - 0.6f) < 1e-4 && Math.Abs(vy) < 1e-4 && s1.PressClampFrames == 1,
                    "the clamp did not take out the part toward the line: (" + vx + ", " + vy + ")");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                vx = 0;
                vy = 1;
                Check(!AiInputHook.ApplyPressClamp(s1, ref flags, ref vx, ref vy) && vy == 1, "the clamp bit with the master switch off");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                S.Set(SettingsSchema.PressDontPass, false, SettingSources.Mcm);
                Check(!AiInputHook.ApplyPressClamp(s1, ref flags, ref vx, ref vy), "the clamp bit with PressDontPass off");
                S.Set(SettingsSchema.PressDontPass, true, SettingSources.Mcm);
            }
            finally
            {
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        /// <summary>The cost: 1000 men - 500 recruits (D 6) pressing a 500-man wall, every look's line, every pressing man steered - small, no bytes.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Step77CostStaysFlat()
        {
            var keep = _logic;
            try
            {
                var body = new FakeCourageBody();
                var block = new FakeBlockBody();
                var logic = NewStep77Logic(body, block, press: true);
                const int men = 1000;
                double t0 = 95000;
                body.Orders[1] = FearOrder.Charge;
                body.Facings[1] = (0, 1);
                body.Orders[2] = FearOrder.Wall;
                body.Facings[2] = (0, -1);
                for (int i = 0; i < 500; i++) FearMan(logic, body, 36000 + i, (i % 84) * 1.5 - 62, -0.8 - (i / 84) * 1.2, 1, 1, legionary: false, morale: 70, bar: 1.0, slot: false);
                for (int i = 0; i < men / 2; i++)
                {
                    var st = FearMan(logic, body, 37000 + i, (i % 125) * 0.8 - 50, (i / 125) * 1.0, 0, 2, legionary: true, morale: 70, bar: 1.0);
                    body.ByIndex[st.AgentIndex].Gate = CourageSkip.WallHeld;
                }
                body.EnemyFronts[1] = (0, 1.5, 0, -1, 100, 4);
                body.EnemyFronts[2] = (0, -4, 0, 1, 126, 7);
                block.Caps[1] = (6, 84, 124.55, 500);
                for (int k = 0; k < 14; k++) logic.TickCourage(t0 += 0.5); // every unit's arrays allocated, every first-time line written
                const int ticks = 300;
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var sw = Stopwatch.StartNew();
                for (int k = 1; k <= ticks; k++) logic.TickCourage(t0 + k / 60.0);
                sw.Stop();
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double msPerTick = sw.Elapsed.TotalMilliseconds / ticks;
                var ps = logic.PressStats;
                Console.WriteLine("        step 77 cost: " + men + " men (" + logic.PressingNow + " pressing now) - the courage + fear + block + press tick "
                                  + msPerTick.ToString("0.000", CultureInfo.InvariantCulture) + " ms (the stand-in's scans are plain loops over all 1000; the press's own "
                                  + ((ps.LookCostMs + ps.SteerCostMs + ps.PublishCostMs) / Math.Max(1, ps.Ticks)).ToString("0.000", CultureInfo.InvariantCulture) + " ms a tick), "
                                  + allocated + " bytes over " + ticks + " ticks");
                Check(logic.PressingNow > 100, "cost: the recruits are not pressing the wall (" + logic.PressingNow + ")");
                Check(msPerTick < 3.0, "cost: the courage + fear + block + press tick over " + men + " men took " + msPerTick + " ms (budget 3 ms with the stand-in's O(n) reads)");
                Check(allocated < 256 * 1024, "cost: step 77 allocates - " + allocated + " bytes over " + ticks + " ticks");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        /// <summary>The master-switch step's part: off = every pressing man let go at once; on = pressed again.</summary>
        private static void PressFollowsTheMasterSwitch()
        {
            var keep = _logic;
            try
            {
                var body = new FakeCourageBody();
                var block = new FakeBlockBody();
                var logic = NewStep77Logic(body, block, press: true);
                double t = 96000;
                var wall = ToniWall(logic, body, 38000);
                var recruits = Recruits108(logic, body, block, 38200, front: 0.8);
                for (int k = 0; k < 6; k++) logic.TickCourage(t += 0.5);
                Check(logic.PressingNow > 0, "precondition: nobody presses the wall (step 77)");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                logic.TickCourage(t += 0.1);
                Check(logic.PressingNow == 0 && recruits.All(m => m.St.Input == null || !m.St.Input.Press), "mod off: men still pressed (step 77)");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                for (int k = 0; k < 4; k++) logic.TickCourage(t += 0.5);
                Check(logic.PressingNow > 0, "mod back on: nobody presses again (step 77)");
                _ = wall;
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

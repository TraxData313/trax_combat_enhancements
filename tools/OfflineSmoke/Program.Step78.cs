using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Step 78 (Toni 2026-10-22, sketch 1-4: the grind, then a lull): THE BATTLE'S RHYTHM through the real logic with courage's stand-in world and the
    /// REAL input filter (AiInputHook.Apply) - two lines grinding: the tired front rank swaps with the fresh men right behind it, the fresh man beside before
    /// the tired one backs off (the line never opens); both fronts spent: each eases back to its lull line, a gap opens, and closes on recovery / a new charge
    /// order; a held wall man's slot follows the swap and the lull; the cost at 1000 men. What it cannot check: how the engine walks the men (PLAYTEST D43).
    /// </summary>
    internal static partial class Program
    {
        /// <summary>The rhythm's own game calls: the player's units by team, the slots of the stand-in's men swapped.</summary>
        private sealed class FakeRhythmBody : IRhythmBody
        {
            public FakeCourageBody? World;
            public int PlayerTeam = -1;
            public bool Slots = true;
            public int Swaps;

            public bool PlayerUnit(TrackedAgent st) => World != null && World.ByIndex.TryGetValue(st.AgentIndex, out var m) && m.Team == PlayerTeam;

            public bool CanSwap(TrackedAgent a, TrackedAgent b)
            {
                if (!Slots || World == null || !World.ByIndex.TryGetValue(a.AgentIndex, out var ma) || !World.ByIndex.TryGetValue(b.AgentIndex, out var mb)) return false;
                return ma.Team == mb.Team && ma.Formation == mb.Formation;
            }

            public bool SwapSlots(TrackedAgent a, TrackedAgent b)
            {
                if (!CanSwap(a, b)) return false;
                var ma = World!.ByIndex[a.AgentIndex];
                var mb = World.ByIndex[b.AgentIndex];
                (ma.SlotX, mb.SlotX) = (mb.SlotX, ma.SlotX);
                (ma.SlotY, mb.SlotY) = (mb.SlotY, ma.SlotY);
                Swaps++;
                return true;
            }
        }

        private static void UseFakeRhythmBody()
        {
            Check(RhythmBodyDefault.Current is GameRhythmBody, "the default rhythm body is not the game's: " + RhythmBodyDefault.Current.GetType().Name);
            RhythmBodyDefault.Current = new FakeRhythmBody();
            Check(new AthleticsLogic().RhythmBody == RhythmBodyDefault.Current, "a new logic does not start with the default rhythm body");
            // the engine's action codes behind the Core's busy rule
            Check(RhythmMath.ActReadyMelee == (int)Agent.ActionCodeType.ReadyMelee && RhythmMath.ActFall == (int)Agent.ActionCodeType.Fall
                  && RhythmMath.ActKick == (int)Agent.ActionCodeType.Kick && RhythmMath.ActWeaponBash == (int)Agent.ActionCodeType.WeaponBash
                  && RhythmMath.ActStrikeLight == (int)Agent.ActionCodeType.StrikeLight && RhythmMath.ActMountStrike == (int)Agent.ActionCodeType.MountStrike,
                "the engine's action codes moved (step 78's busy rule)");
            Check(RhythmMath.BusyAction((int)Agent.ActionCodeType.ReleaseMelee) && RhythmMath.BusyAction((int)Agent.ActionCodeType.StrikeKnockBack)
                  && !RhythmMath.BusyAction((int)Agent.ActionCodeType.Guard) && !RhythmMath.BusyAction((int)Agent.ActionCodeType.DefendShield), "the busy rule");
            // the swap lever is the game's own public call
            var m = typeof(Formation).GetMethod("SwitchUnitLocations", new[] { typeof(Agent), typeof(Agent) });
            Check(m != null && m.IsPublic, "Formation.SwitchUnitLocations(Agent, Agent) is not public any more (step 78's swap lever)");
        }

        private static void Step78Defaults(bool rotation = true, bool lull = true)
        {
            S.Set(SettingsSchema.RotationEnabled, rotation, SettingSources.File);
            S.Set(SettingsSchema.LullEnabled, lull, SettingSources.File);
            S.Set(SettingsSchema.LullForPlayerUnits, true, SettingSources.File);
            S.Set(SettingsSchema.RotationTiredPercent, 35, SettingSources.File);
            S.Set(SettingsSchema.RotationUseBraceFloor, true, SettingSources.File);
            S.Set(SettingsSchema.RotationFresherPercent, 25, SettingSources.File);
            S.Set(SettingsSchema.RotationBehindMetres, 1.5, SettingSources.File);
            S.Set(SettingsSchema.RotationFileMetres, 1.0, SettingSources.File);
            S.Set(SettingsSchema.RotationContactMetres, 2.5, SettingSources.File);
            S.Set(SettingsSchema.RotationBesideMetres, 0.7, SettingSources.File);
            S.Set(SettingsSchema.RotationApproachSeconds, 3.0, SettingSources.File);
            S.Set(SettingsSchema.RotationMaxSeconds, 6.0, SettingSources.File);
            S.Set(SettingsSchema.RotationCooldownSeconds, 12.0, SettingSources.File);
            S.Set(SettingsSchema.RotationRestSeconds, 6.0, SettingSources.File);
            S.Set(SettingsSchema.LullContactMen, 3, SettingSources.File);
            S.Set(SettingsSchema.LullMinContactSeconds, 10.0, SettingSources.File);
            S.Set(SettingsSchema.LullStartChargePercent, 30, SettingSources.File);
            S.Set(SettingsSchema.LullStartAdvancePercent, 40, SettingSources.File);
            S.Set(SettingsSchema.LullStartHoldPercent, 50, SettingSources.File);
            S.Set(SettingsSchema.LullRecoverChargePercent, 10, SettingSources.File);
            S.Set(SettingsSchema.LullRecoverAdvancePercent, 15, SettingSources.File);
            S.Set(SettingsSchema.LullRecoverHoldPercent, 20, SettingSources.File);
            S.Set(SettingsSchema.LullEaseMetres, 2.0, SettingSources.File);
            S.Set(SettingsSchema.LullFrontDepthMetres, 1.5, SettingSources.File);
            S.Set(SettingsSchema.LullMaxSeconds, 30.0, SettingSources.File);
            S.Set(SettingsSchema.LullCooldownSeconds, 20.0, SettingSources.File);
            S.Set(SettingsSchema.LullRotates, true, SettingSources.File);
            // step 78b: step 78's own lull - no clock, the front men's bar (the first two ranks), a charge order at once; the gap and the
            // back-step as shipped (Program.Step78b.cs runs the clock)
            S.Set(SettingsSchema.LullClockChargeMinSeconds, 600.0, SettingSources.File);
            S.Set(SettingsSchema.LullClockChargeMaxSeconds, 600.0, SettingSources.File);
            S.Set(SettingsSchema.LullClockAdvanceMinSeconds, 600.0, SettingSources.File);
            S.Set(SettingsSchema.LullClockAdvanceMaxSeconds, 600.0, SettingSources.File);
            S.Set(SettingsSchema.LullClockHoldMinSeconds, 600.0, SettingSources.File);
            S.Set(SettingsSchema.LullClockHoldMaxSeconds, 600.0, SettingSources.File);
            S.Set(SettingsSchema.LullClockChargePercent, 55, SettingSources.File);
            S.Set(SettingsSchema.LullClockAdvancePercent, 60, SettingSources.File);
            S.Set(SettingsSchema.LullClockHoldPercent, 65, SettingSources.File);
            S.Set(SettingsSchema.LullAttackRateFallenPercent, 0, SettingSources.File);
            S.Set(SettingsSchema.LullUnitDepthMetres, 1.5, SettingSources.File);
            S.Set(SettingsSchema.LullChargeGraceSeconds, 0.0, SettingSources.File);
            S.Set(SettingsSchema.LullGapMetres, 3.5, SettingSources.File);
            S.Set(SettingsSchema.LullMaxBackMetres, 8.0, SettingSources.File);
        }

        private static AthleticsLogic NewStep78Logic(FakeCourageBody body, FakeRhythmBody rhythm, bool rotation = true, bool lull = true)
        {
            AthleticsDefaults();
            SprintDefaults();
            FeverDefaults();
            CourageDefaults();
            FearDefaults();
            Step70Defaults();
            Step74Defaults();
            Step77Defaults();
            Step78Defaults(rotation, lull); // before the logic: its mission-start line says step 78
            S.Set(SettingsSchema.CourageEnabled, false, SettingSources.File); // the rhythm alone (courage measures)
            S.Set(SettingsSchema.FearFlockEnabled, false, SettingSources.File);
            S.Set(SettingsSchema.PressDontPass, false, SettingSources.File);
            S.Set(SettingsSchema.LineBlockHoldEnabled, false, SettingSources.File);
            var logic = NewCourageLogic(body);
            rhythm.World = body;
            logic.RhythmBody = rhythm;
            return logic;
        }

        /// <summary>A line of <paramref name="files"/> x <paramref name="ranks"/>, 1 m apart, its front at <paramref name="frontY"/>, its ranks toward
        /// <paramref name="back"/> (−1 = south); the front rank's bar <paramref name="frontBar"/>, the rest <paramref name="rearBar"/>.</summary>
        private static List<FakeCourageMan> RhythmLine(AthleticsLogic logic, FakeCourageBody body, int index, int team, int formation, int files, int ranks, double frontY, int back,
            double frontBar, double rearBar)
        {
            var men = new List<FakeCourageMan>();
            for (int k = 0; k < ranks; k++)
                for (int f = 0; f < files; f++)
                {
                    var st = FearMan(logic, body, index + k * files + f, -(files - 1) / 2.0 + f, frontY + back * k * 1.0, team, formation, legionary: true, morale: 70,
                        bar: k == 0 ? frontBar : rearBar);
                    men.Add(body.ByIndex[st.AgentIndex]);
                }
            return men;
        }

        /// <summary>The stand-in world for <paramref name="seconds"/>: the game holds a man still (a hold) or walks him at his nearest enemy until within
        /// 1.2 m (a charge - <paramref name="charging"/>); every man's vector goes through the REAL input filter whenever a wish of ours is on him; 1.5 m/s.
        /// <paramref name="refill"/> = bar per second for men without an enemy within 2.5 m.</summary>
        private static double RunRhythm(AthleticsLogic logic, FakeCourageBody body, double t, double seconds, Func<FakeCourageMan, bool> charging, double refill = 0,
            Action<double>? each = null)
        {
            const double speed = 1.5, dt = 0.1;
            for (double s = 0; s < seconds - 1e-9; s += dt)
            {
                foreach (var m in body.Men)
                {
                    float vx = 0, vy = 0;
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
                    if (near != null && charging(m) && Math.Sqrt(best) > 1.2)
                    {
                        double dx = near.X - m.X, dy = near.Y - m.Y, len = Math.Sqrt(dx * dx + dy * dy);
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
                    if (refill > 0 && (near == null || Math.Sqrt(best) > 2.5)) SetBar(m.St, Math.Min(1.0, m.St.Fraction + refill * dt));
                }
                logic.TickCourage(t += dt);
                each?.Invoke(t);
            }
            return t;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RotationInTheGrind()
        {
            var keep = _logic;
            try
            {
                // ---- OFF: the chances counted, nobody moves
                var body = new FakeCourageBody();
                var rhythm = new FakeRhythmBody();
                var logic = NewStep78Logic(body, rhythm, rotation: false, lull: false);
                double t = 97000;
                var a = RhythmLine(logic, body, 39000, 0, 1, 12, 3, 0, -1, 0.25, 0.85);
                var b = RhythmLine(logic, body, 39100, 1, 2, 12, 3, 1.0, 1, 0.9, 0.9);
                t = RunRhythm(logic, body, t, 4, _ => false);
                var rs = logic.RhythmStats;
                Check(rs.Chances[0] >= 10 && rs.Started == 0, "rotation OFF: chances " + rs.Chances[0] + ", swaps " + rs.Started + " (step 78)");
                Check(a.All(m => m.St.Input == null || !m.St.Input.Rhythm), "rotation OFF: a rhythm wish on a man");
                LogHas("first CHANCE to swap with the rotation OFF");
                double offFront = a.Where(m => m.Y > -0.5).Average(m => m.St.Fraction);

                // ---- ON: the tired front rank swaps with the fresh second rank; the line never opens
                body = new FakeCourageBody();
                rhythm = new FakeRhythmBody();
                logic = NewStep78Logic(body, rhythm, rotation: true, lull: false);
                t = 97100;
                a = RhythmLine(logic, body, 39200, 0, 1, 12, 3, 0, -1, 0.25, 0.85);
                b = RhythmLine(logic, body, 39300, 1, 2, 12, 3, 1.0, 1, 0.9, 0.9);
                var files = Enumerable.Range(0, 12).Select(f => -5.5 + f).ToArray();
                int opened = 0, minFront = int.MaxValue, frames = 0;
                t = RunRhythm(logic, body, t, 12, _ => false, each: _ =>
                {
                    frames++;
                    foreach (double fx in files)
                    {
                        int n = a.Count(m => Math.Abs(m.X - fx) <= 0.75 && m.Y >= -0.45);
                        if (n == 0) opened++;
                    }
                    minFront = Math.Min(minFront, a.Count(m => m.Y >= -0.45));
                });
                rs = logic.RhythmStats;
                double onFront = a.Where(m => m.Y >= -0.45).Average(m => m.St.Fraction);
                int frontTired = a.Count(m => m.Y >= -0.45 && m.St.Fraction < 0.5);
                Console.WriteLine("        step 78 rotation: swaps asked " + rs.Started + ", done " + rs.Ends[(int)RotationEnd.Done] + " (slots swapped " + rhythm.Swaps + "), the front rank's bar "
                                  + offFront.ToString("0.00", CultureInfo.InvariantCulture) + " (OFF) → " + onFront.ToString("0.00", CultureInfo.InvariantCulture) + " (ON), tired men still at the front "
                                  + frontTired + ", the fewest men at the front " + minFront + " of 12, file-frames with nobody at the front " + opened + " of " + frames * 12
                                  + ", the tired man held " + (rs.WaitN > 0 ? (rs.WaitSum / rs.WaitN).ToString("0.00", CultureInfo.InvariantCulture) : "-") + " s for the fresh one");
                Check(rs.Ends[(int)RotationEnd.Done] >= 10, "rotation ON: only " + rs.Ends[(int)RotationEnd.Done] + " swaps done of 12 files (step 78)");
                Check(rhythm.Swaps >= 10, "rotation ON: the slots were not swapped (" + rhythm.Swaps + ")");
                Check(onFront > 0.75 && frontTired <= 2, "rotation ON: the front is not fresh - " + onFront + ", tired " + frontTired);
                Check(opened == 0, "rotation ON: the line OPENED - " + opened + " file-frames with nobody at the front");
                Check(minFront >= 12, "rotation ON: the front rank fell to " + minFront + " men");
                Check(rs.HoldFrames > 0 || a.Any(m => m.St.Input != null && m.St.Input.RhythmHeldFrames >= 0), "the tired man's hold");
                Check(b.All(m => m.St.Input == null || !m.St.Input.Rhythm), "rotation: the fresh enemy line was touched");
                LogHas("first SWAP asked");
                LogHas("first fresh man BESIDE the tired one");
                LogHas("first SWAP DONE");
                // the tired men went back and REST: none walks forward past his new place
                var back = a.Where(m => m.St.Fraction < 0.5).ToList();
                Check(back.All(m => m.Y < -0.5), "rotation: a tired man still at the front after the swaps");
                // the input frame: the swap's walk after the press's walk, before courage's / the flock's / the block's; the hold guards
                var probe = a.First(m => m.Y < -0.5).St;
                var s = probe.Input!;
                bool was = s.Rhythm;
                s.Rhythm = true;
                s.RhythmMove = true;
                s.RhythmX = 0.3f;
                s.RhythmY = -0.9f;
                s.Flock = true;
                s.FlockX = 1f;
                s.FlockY = 0f;
                s.RhythmHoldAttacks = true;
                uint fl = AiInputMath.AttackMask & 0x40;
                float vx = 0.5f, vy = 0.5f;
                AiInputHook.Apply(probe, s, false, ref fl, ref vx, ref vy);
                Check(Math.Abs(vx - 0.3f) < 1e-4 && Math.Abs(vy + 0.9f) < 1e-4, "the input frame: the swap's walk did not win over the flock (" + vx + ", " + vy + ")");
                Check((fl & AiInputMath.AttackMask) == 0 && (fl & AiInputMath.DefendDown) != 0, "the input frame: the tired man's attack not held / no guard");
                s.Flock = false;
                s.RhythmHoldAttacks = false;
                s.RhythmMove = false;
                s.RhythmClamp = true;
                s.RhythmNX = 0;
                s.RhythmNY = 1;
                vx = 0.6f;
                vy = 0.8f;
                fl = 0;
                AiInputHook.Apply(probe, s, false, ref fl, ref vx, ref vy);
                Check(Math.Abs(vy) < 1e-4 && Math.Abs(vx - 0.6f) < 1e-4, "the input frame: the clamp did not take out the forward part");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                vx = 0.6f;
                vy = 0.8f;
                AiInputHook.Apply(probe, s, false, ref fl, ref vx, ref vy);
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                Check(Math.Abs(vy - 0.8f) < 1e-4, "the input frame: the clamp bites with the master switch off");
                s.RhythmClamp = false;
                s.Rhythm = was;

                // ---- a held wall man: his slot follows the swap (the wall hold's walk target), the lull's shift
                var wallBody = new FakeCourageBody();
                var wallRhythm = new FakeRhythmBody();
                var wl = NewStep78Logic(wallBody, wallRhythm, rotation: true, lull: true);
                t = 97500;
                var wa = RhythmLine(wl, wallBody, 39400, 0, 1, 6, 2, 0, -1, 0.25, 0.9);
                foreach (var m in wa) m.Gate = CourageSkip.WallHeld;
                var wb = RhythmLine(wl, wallBody, 39500, 1, 2, 6, 2, 1.0, 1, 0.9, 0.9);
                for (int k = 0; k < 4; k++) wl.TickCourage(t += 0.5);
                Check(wl.RhythmStats.Started >= 1, "a held wall man: no swap started (" + wl.RhythmStats.Started + ", refused " + string.Join("/", wl.RhythmStats.Refused) + ")");
                var tired = wa.First(m => m.St.Rhythm.InSwap && m.St.Rhythm.Tired);
                var fresh = (TrackedAgent)tired.St.Rhythm.Partner!;
                var f0 = new WallFacts { HasPositions = true, SlotX = 99, SlotY = 99 };
                Check(InvokeWallSlot(wl, fresh, ref f0, t) && Math.Abs(f0.SlotY - tired.Y) < 0.05 && Math.Abs(Math.Abs(f0.SlotX - tired.X) - 0.7) < 0.05,
                    "a held fresh man's slot is not BESIDE the tired one: (" + f0.SlotX + ", " + f0.SlotY + ")");
                var f1 = new WallFacts { HasPositions = true, SlotX = 99, SlotY = 99 };
                Check(InvokeWallSlot(wl, tired.St, ref f1, t) && Math.Abs(f1.SlotX - tired.X) < 0.05 && Math.Abs(f1.SlotY - tired.Y) < 0.05, "a held tired man's slot is not his place while he holds");
                _ = wb;
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        private static bool InvokeWallSlot(AthleticsLogic logic, TrackedAgent st, ref WallFacts f, double now)
        {
            var mi = typeof(AthleticsLogic).GetMethod("RhythmWallSlot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var args = new object[] { st, f, now };
            bool r = (bool)mi.Invoke(logic, args)!;
            f = (WallFacts)args[1];
            return r;
        }

        /// <summary>The gap between two lines' fronts (unit 1 faces north at y up to its front, unit 2 south).</summary>
        private static double Gap(List<FakeCourageMan> south, List<FakeCourageMan> north)
        {
            double sf = south.OrderByDescending(m => m.Y).Take(Math.Max(1, south.Count / 3)).Average(m => m.Y);
            double nf = north.OrderBy(m => m.Y).Take(Math.Max(1, north.Count / 3)).Average(m => m.Y);
            return nf - sf;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void LullBetweenSpentFronts()
        {
            var keep = _logic;
            try
            {
                // ---- OFF: both fronts spent - counted, nothing moves
                var body = new FakeCourageBody();
                var rhythm = new FakeRhythmBody();
                var logic = NewStep78Logic(body, rhythm, rotation: true, lull: false);
                body.Orders[1] = body.Orders[2] = FearOrder.Charge;
                double t = 98000;
                var a = RhythmLine(logic, body, 40000, 0, 1, 12, 3, 0, -1, 0.2, 0.22);
                var b = RhythmLine(logic, body, 40100, 1, 2, 12, 3, 1.0, 1, 0.2, 0.22);
                t = RunRhythm(logic, body, t, 14, _ => true);
                Check(logic.RhythmUnits.WouldStart >= 1 && logic.RhythmStats.Lulls == 0, "lull OFF: would start " + logic.RhythmUnits.WouldStart + ", lulls " + logic.RhythmStats.Lulls);
                double offGap = Gap(a, b);

                // ---- ON: a lull opens a gap, the men face each other behind their lines; recovery closes it
                body = new FakeCourageBody();
                rhythm = new FakeRhythmBody();
                logic = NewStep78Logic(body, rhythm, rotation: true, lull: true);
                body.Orders[1] = body.Orders[2] = FearOrder.Charge;
                t = 98100;
                a = RhythmLine(logic, body, 40200, 0, 1, 12, 3, 0, -1, 0.2, 0.22);
                b = RhythmLine(logic, body, 40300, 1, 2, 12, 3, 1.0, 1, 0.2, 0.22);
                double startGap = Gap(a, b);
                t = RunRhythm(logic, body, t, 9.5, _ => true);
                Check(logic.RhythmStats.Lulls == 0, "lull ON: a lull before the contact time (10 s)");
                double maxGap = 0, atStart = double.NaN, lullSince = double.NaN;
                t = RunRhythm(logic, body, t, 6, _ => true, each: now =>
                {
                    if (logic.RhythmStats.Lulls > 0 && double.IsNaN(lullSince))
                    {
                        lullSince = now;
                        atStart = Gap(a, b);
                    }
                    maxGap = Math.Max(maxGap, Gap(a, b));
                });
                var u = logic.RhythmUnits;
                Check(logic.RhythmStats.Lulls == 1, "lull ON: no lull after 15 s of two spent fronts (" + logic.RhythmStats.Lulls + ")");
                Check(maxGap >= startGap + 3.0, "lull ON: the gap opened only to " + maxGap.ToString("0.00", CultureInfo.InvariantCulture) + " m from " + startGap.ToString("0.00", CultureInfo.InvariantCulture));
                Check(maxGap <= startGap + 2 * 2.0 + 1.0, "lull ON: the fronts eased back too far - " + maxGap);
                Check(logic.RhythmStats.MenWalkedBack > 20, "lull ON: men walked back " + logic.RhythmStats.MenWalkedBack);
                LogHas("first LULL");
                LogHas("first man WALKED BACK in a lull");
                double lullGap = Gap(a, b);
                // recovery: the men refill out of contact - the lull ends, the stand-in charge closes the gap
                t = RunRhythm(logic, body, t, 12, _ => true, refill: 1.0 / 60);
                Check(logic.RhythmStats.LullEnds[(int)LullEnd.Recovered] == 1, "lull ON: not ended by recovery (" + string.Join("/", logic.RhythmStats.LullEnds) + ")");
                t = RunRhythm(logic, body, t, 4, _ => true);
                double closed = Gap(a, b);
                Console.WriteLine("        step 78 lull: the gap " + startGap.ToString("0.0", CultureInfo.InvariantCulture) + " m in contact (OFF after 14 s " + offGap.ToString("0.0", CultureInfo.InvariantCulture)
                                  + " m), in the lull up to " + maxGap.ToString("0.0", CultureInfo.InvariantCulture) + " m (" + lullGap.ToString("0.0", CultureInfo.InvariantCulture)
                                  + " m held), after the recovery " + closed.ToString("0.0", CultureInfo.InvariantCulture) + " m; men walked back " + logic.RhythmStats.MenWalkedBack
                                  + ", lull lasted " + (logic.RhythmStats.LullSecondsSum).ToString("0.0", CultureInfo.InvariantCulture) + " s");
                Check(closed <= startGap + 0.6, "lull ON: the gap did not close after the recovery (" + closed + ")");
                Check(a.Concat(b).All(m => m.St.Input == null || !m.St.Input.RhythmClamp), "lull ended: a clamp still on");
                LogHas("first lull ENDED");

                // ---- a NEW charge order ends a holding pair's lull at once; a rotation during the lull
                body = new FakeCourageBody();
                rhythm = new FakeRhythmBody();
                logic = NewStep78Logic(body, rhythm, rotation: true, lull: true);
                body.Orders[1] = body.Orders[2] = FearOrder.Hold;
                t = 98400;
                a = RhythmLine(logic, body, 40400, 0, 1, 12, 3, 0, -1, 0.3, 0.3);
                b = RhythmLine(logic, body, 40500, 1, 2, 12, 3, 1.0, 1, 0.3, 0.3);
                foreach (var m in a.Where(m => m.Y < -1.5)) SetBar(m.St, 0.95); // a fresh third rank of the south line
                t = RunRhythm(logic, body, t, 13, _ => false);
                Check(logic.RhythmStats.Lulls == 1, "a holding pair: no lull (hold's 50%) - " + logic.RhythmStats.Lulls);
                t = RunRhythm(logic, body, t, 6, _ => false);
                Check(logic.RhythmStats.InLull >= 1, "a lull: no swap during it (the fresh third rank) - " + logic.RhythmStats.InLull);
                LogHas("first swap DURING A LULL");
                Check(logic.RhythmStats.LullEnds[(int)LullEnd.Recovered] == 1, "a lull with a fresh rank: the swaps did not bring its front back (" + string.Join("/", logic.RhythmStats.LullEnds) + ")");
                body = new FakeCourageBody();
                rhythm = new FakeRhythmBody();
                logic = NewStep78Logic(body, rhythm, rotation: false, lull: true);
                body.Orders[1] = body.Orders[2] = FearOrder.Hold;
                t = 98550;
                a = RhythmLine(logic, body, 40800, 0, 1, 12, 3, 0, -1, 0.3, 0.3);
                b = RhythmLine(logic, body, 40900, 1, 2, 12, 3, 1.0, 1, 0.3, 0.3);
                t = RunRhythm(logic, body, t, 13, _ => false);
                Check(logic.RhythmStats.Lulls == 1, "a holding pair: no lull - " + logic.RhythmStats.Lulls);
                body.Kinds[1] = BraceOrderKind.Charge;
                t = RunRhythm(logic, body, t, 1.2, _ => false);
                Check(logic.RhythmStats.LullEnds[(int)LullEnd.ChargeOrder] == 1, "a new charge order did not end the lull (" + string.Join("/", logic.RhythmStats.LullEnds) + ")");

                // ---- your units: LullForPlayerUnits off = no lull for a pair with yours (counted)
                body = new FakeCourageBody();
                rhythm = new FakeRhythmBody { PlayerTeam = 0 };
                logic = NewStep78Logic(body, rhythm, rotation: false, lull: true);
                S.Set(SettingsSchema.LullForPlayerUnits, false, SettingSources.File);
                body.Orders[1] = body.Orders[2] = FearOrder.Hold;
                t = 98700;
                a = RhythmLine(logic, body, 40600, 0, 1, 12, 3, 0, -1, 0.3, 0.3);
                b = RhythmLine(logic, body, 40700, 1, 2, 12, 3, 1.0, 1, 0.3, 0.3);
                t = RunRhythm(logic, body, t, 12, _ => false);
                Check(logic.RhythmStats.Lulls == 0 && logic.RhythmUnits.RefusedPlayer >= 1, "your units off: lulls " + logic.RhythmStats.Lulls + ", refused " + logic.RhythmUnits.RefusedPlayer);
                logic.WriteRhythmSummary();
                LogHas("[summary] battle rhythm (step 78) - ROTATION");
                LogHas("[summary] battle rhythm (step 78) - LULLS");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Step78CostStaysFlat()
        {
            var keep = _logic;
            try
            {
                var body = new FakeCourageBody();
                var rhythm = new FakeRhythmBody();
                var logic = NewStep78Logic(body, rhythm, rotation: true, lull: true);
                double t0 = 99000;
                // 1000 men: two 500-man blocks (25 files x 20 ranks) in contact, the front ranks tired, a fresh rank behind
                for (int k = 0; k < 20; k++)
                    for (int f = 0; f < 25; f++)
                    {
                        FearMan(logic, body, 41000 + k * 25 + f, f - 12, -k * 1.0, 0, 1, legionary: true, morale: 70, bar: k % 2 == 0 ? 0.25 : 0.9);
                        FearMan(logic, body, 42000 + k * 25 + f, f - 12, 1.0 + k * 1.0, 1, 2, legionary: true, morale: 70, bar: k % 2 == 0 ? 0.25 : 0.9);
                    }
                for (int k = 0; k < 3; k++) logic.TickCourage(t0 += 0.5); // the units' arrays allocated, the swaps begun - the measure runs through them
                const int ticks = 300;
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var rs = logic.RhythmStats;
                double c0 = rs.LookCostMs + rs.SteerCostMs + rs.PublishCostMs;
                long t0Ticks = rs.Ticks;
                var sw = Stopwatch.StartNew();
                for (int k = 1; k <= ticks; k++) logic.TickCourage(t0 + k / 60.0);
                sw.Stop();
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double own = (rs.LookCostMs + rs.SteerCostMs + rs.PublishCostMs - c0) / Math.Max(1, rs.Ticks - t0Ticks);
                Console.WriteLine("        step 78 cost: 1000 men (" + logic.RhythmMenNow + " with a rhythm wish, swaps asked " + rs.Started + ") - the courage tick "
                                  + (sw.Elapsed.TotalMilliseconds / ticks).ToString("0.000", CultureInfo.InvariantCulture) + " ms (the stand-in's scans are plain loops), the rhythm's own "
                                  + own.ToString("0.000", CultureInfo.InvariantCulture) + " ms a tick, " + allocated + " bytes over " + ticks + " ticks");
                Check(rs.Started >= 40, "cost: the tired fronts did not swap (" + rs.Started + ")");
                Check(own < 0.5, "cost: the rhythm's own " + own + " ms a tick (budget 0.5)");
                Check(allocated < 256 * 1024, "cost: step 78 allocates - " + allocated + " bytes over " + ticks + " ticks");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        /// <summary>The master-switch step's part: off = every swap and wish let go at once; on = swapping again.</summary>
        private static void RhythmFollowsTheMasterSwitch()
        {
            var keep = _logic;
            try
            {
                var body = new FakeCourageBody();
                var rhythm = new FakeRhythmBody();
                var logic = NewStep78Logic(body, rhythm, rotation: true, lull: true);
                double t = 99500;
                var a = RhythmLine(logic, body, 43000, 0, 1, 8, 3, 0, -1, 0.25, 0.9);
                RhythmLine(logic, body, 43100, 1, 2, 8, 3, 1.0, 1, 0.9, 0.9);
                for (int k = 0; k < 3; k++) logic.TickCourage(t += 0.5);
                Check(logic.RhythmMenNow > 0, "precondition: nobody swaps (step 78)");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                logic.TickCourage(t += 0.1);
                Check(logic.RhythmMenNow == 0 && a.All(m => !m.St.Rhythm.InSwap && (m.St.Input == null || !m.St.Input.Rhythm)), "mod off: a swap / wish still on (step 78)");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                foreach (var m in a) m.St.Rhythm.NextRotation = double.NegativeInfinity;
                for (int k = 0; k < 3; k++) logic.TickCourage(t += 0.5);
                Check(logic.RhythmMenNow > 0, "mod back on: nobody swaps again (step 78)");
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

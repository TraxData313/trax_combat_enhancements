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
    /// Step 74 (Toni's battle 2026-10-20 20:30: 180 T1 recruits held 5 deep by step 73 spread into a wide loose crowd just before contact, a trail
    /// wrapping round his right): THE BLOCK HOLD through the real logic with courage's stand-in world - a 180-man D 6 block (the shipped map, 30
    /// files, 44 m) CHARGING a narrower line, its men driven by a stand-in of the game's charge (each man at the nearest enemy, the flank men round
    /// the enemy's ends to its rear, the right-hand files drifting outward along a "rock"): with the hold OFF the crowd spreads and trails round the
    /// flank; ON it stays within its width + the slack and reaches contact as fast; a D 1 elite unit untouched; the live switch; the input frame's
    /// priority; the diagnostic's lines; the cost for 1000 men. What it cannot check: how the engine walks the men (PLAYTEST D39).
    /// </summary>
    internal static partial class Program
    {
        /// <summary>The block body's stand-in: each formation's step-73 cap and its order / behaviour names (none = not counted).</summary>
        private sealed class FakeBlockBody : IBlockBody
        {
            public FakeCourageBody? World;
            public readonly Dictionary<int, (double D, int Files, double Width, int Men)> Caps = new Dictionary<int, (double, int, double, int)>();
            public string Order = "Charge", Behaviour = "BehaviorTacticalCharge", Arrangement = "Line";
            public int CapReads, FormationReads;

            public bool Cap(TrackedAgent st, LineDiscipline? lines, out double depth, out int capFiles, out double capWidth, out int men, out bool playerSide, out bool ai)
            {
                CapReads++;
                depth = capWidth = double.NaN;
                capFiles = men = 0;
                playerSide = false;
                ai = true;
                if (World == null || !World.ByIndex.TryGetValue(st.AgentIndex, out var m) || !Caps.TryGetValue(m.Formation, out var c)) return false;
                depth = c.D;
                capFiles = c.Files;
                capWidth = c.Width;
                men = c.Men;
                playerSide = m.Team == 0;
                return true;
            }

            public void Formation(TrackedAgent st, out string order, out string behaviour, out string arrangement, out double gameWidth)
            {
                FormationReads++;
                order = Order;
                behaviour = Behaviour;
                arrangement = Arrangement;
                gameWidth = 44;
            }

            public string Name(TrackedAgent st) => World != null && World.ByIndex.TryGetValue(st.AgentIndex, out var m) && m.Team == 0 ? "your Infantry" : "the enemy's Infantry";
        }

        private static void Step74Defaults()
        {
            S.Set(SettingsSchema.LineDisciplineEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.LineDisciplineForPlayer, true, SettingSources.File);
            S.Set(SettingsSchema.LineBlockHoldEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.LineBlockSlackMetres, 2.0, SettingSources.File);
            S.Set(SettingsSchema.LineBlockFullAtDepth, 3.0, SettingSources.File);
            S.Set(SettingsSchema.LineBlockLooseMetres, 10.0, SettingSources.File);
            S.Set(SettingsSchema.LineBlockRankMetres, 1.2, SettingSources.File);
            S.Set(SettingsSchema.LineBlockAheadMetres, 3.0, SettingSources.File);
            S.Set(SettingsSchema.LineBlockStrikeReachMetres, 2.5, SettingSources.File);
            S.Set(SettingsSchema.LineBlockStrikeLeashMetres, 2.0, SettingSources.File);
            S.Set(SettingsSchema.LineBlockNearMetres, 30.0, SettingSources.File);
            S.Set(SettingsSchema.LineBlockLeadMetres, 1.5, SettingSources.File);
            S.Set(SettingsSchema.LineBlockMopUpWinPercent, 50.0, SettingSources.File);
        }

        /// <summary>The block hold idle in every older step: the default block body counts no formation (the courage world has no line discipline).</summary>
        private static void UseFakeBlockBody()
        {
            Check(BlockBodyDefault.Current is GameBlockBody, "the default block body is not the game's: " + BlockBodyDefault.Current.GetType().Name);
            BlockBodyDefault.Current = new FakeBlockBody();
            Check(new AthleticsLogic().BlockBody == BlockBodyDefault.Current, "a new logic does not start with the default block body");
        }

        private static AthleticsLogic NewStep74Logic(FakeCourageBody body, FakeBlockBody block)
        {
            AthleticsDefaults();
            SprintDefaults();
            FeverDefaults();
            CourageDefaults();
            FearDefaults();
            Step70Defaults();
            Step74Defaults(); // before the logic: its mission-start lines say step 74
            S.Set(SettingsSchema.CourageEnabled, false, SettingSources.File); // the block alone (courage measures)
            S.Set(SettingsSchema.FearFlockEnabled, false, SettingSources.File);
            var logic = NewCourageLogic(body);
            block.World = body;
            logic.BlockBody = block;
            return logic;
        }

        /// <summary>Toni's charge in miniature: 180 recruits (formation 1, a charge) in 30 files x 6 ranks 1.5 m / 1.2 m apart, their front 40 m south
        /// of a line of 40 men 20 files x 2 (formation 2, 28.5 m wide) - its frontage read. The recruits' step-73 cap: D 6, 30 files, 44.05 m.</summary>
        private static (List<FakeCourageMan> block, List<FakeCourageMan> line) RecruitsChargeALine(AthleticsLogic logic, FakeCourageBody body, FakeBlockBody block, int index, double depth = 6)
        {
            body.Orders[1] = FearOrder.Charge;
            body.Orders[2] = FearOrder.Hold;
            var recruits = new List<FakeCourageMan>();
            var line = new List<FakeCourageMan>();
            for (int f = 0; f < 30; f++)
                for (int k = 0; k < 6; k++)
                {
                    var st = FearMan(logic, body, index + f * 6 + k, -21.75 + f * 1.5, -40 - k * 1.2, 1, 1, legionary: false, morale: 70, bar: 1.0, slot: false);
                    recruits.Add(body.ByIndex[st.AgentIndex]);
                }
            for (int f = 0; f < 20; f++)
                for (int k = 0; k < 2; k++)
                {
                    var st = FearMan(logic, body, index + 300 + f * 2 + k, -14.25 + f * 1.5, k * 1.2, 2, 2, legionary: true, morale: 70, bar: 1.0);
                    line.Add(body.ByIndex[st.AgentIndex]);
                }
            body.EnemyFronts[1] = (0, 0.6, 0, -1, 28.5, 2.4);
            body.EnemyFronts[2] = (0, -42, 0, 1, 44, 7);
            block.Caps[1] = (depth, depth <= 1.0001 ? 180 : 30, depth <= 1.0001 ? 268.75 : 44.05, 180);
            return (recruits, line);
        }

        /// <summary>The stand-in of the GAME's charge for the recruits (4 m/s): each at his nearest enemy; a man beyond the enemy's end (|x| past 15.25 m)
        /// round its end to its rear; the five right-hand files drift outward 2 m/s along "the rock" (Toni's trail); in contact (1.2 m) he stands. A man
        /// with the block wish walks its vector instead. (The trail: the right-hand files head for a point 36 m out round the right flank.) The line stands. Returns the first contact time (NaN = none) and the widest offset seen near contact.</summary>
        private static (double contact, double t) RunCharge(AthleticsLogic logic, FakeCourageBody body, List<FakeCourageMan> block, List<FakeCourageMan> line, double t, double seconds,
            Action<double>? midway = null)
        {
            const double speed = 4, dt = 0.1;
            double contact = double.NaN;
            var trail = new HashSet<FakeCourageMan>(block.Where(m => m.X > 14));
            for (double s = 0; s < seconds - 1e-9; s += dt)
            {
                foreach (var m in block)
                {
                    FakeCourageMan? near = null;
                    double best = double.MaxValue;
                    foreach (var e in line)
                    {
                        double d = (e.X - m.X) * (e.X - m.X) + (e.Y - m.Y) * (e.Y - m.Y);
                        if (d < best)
                        {
                            best = d;
                            near = e;
                        }
                    }
                    if (near == null) continue;
                    if (Math.Sqrt(best) <= 1.2)
                    {
                        if (double.IsNaN(contact)) contact = t;
                        continue; // fighting
                    }
                    var input = m.St.Input;
                    if (input != null && input.Block)
                    {
                        m.X += input.BlockX * speed * dt;
                        m.Y += input.BlockY * speed * dt;
                        continue;
                    }
                    double tx = near.X, ty = near.Y;
                    if (trail.Contains(m))
                    {
                        tx = 36; // the trail: along the rock, wide round the right flank to the enemy's rear
                        ty = 6;
                    }
                    else if (Math.Abs(m.X) > 15.25 && m.Y > -10)
                    {
                        tx = Math.Sign(m.X) * 20; // round the end, to the rear
                        ty = 5;
                    }
                    double dx = tx - m.X, dy = ty - m.Y, len = Math.Sqrt(dx * dx + dy * dy);
                    if (len > 1e-6)
                    {
                        m.X += dx / len * speed * dt;
                        m.Y += dy / len * speed * dt;
                    }
                }
                logic.TickCourage(t += dt);
                midway?.Invoke(s);
            }
            return (contact, t);
        }

        private static (double median, double widest, double width1090, int beyond) BlockShape(List<FakeCourageMan> block, double allowance)
        {
            var xs = block.Select(m => m.X).OrderBy(x => x).ToList();
            double med = xs[xs.Count / 2];
            double widest = xs.Max(x => Math.Abs(x - med));
            double w = xs[(int)(0.9 * (xs.Count - 1))] - xs[(int)(0.1 * (xs.Count - 1))];
            return (med, widest, w, xs.Count(x => Math.Abs(x - med) > allowance));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void BlockHoldThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                const double allowance = 22.0 + 2 + 2.5; // the half-width + the slack + one look's run outward (0.5 s at 4 m/s) + a hair - a man is caught at his next look
                // ---- 1. OFF: the game's charge dissolves the block - wide and a trail round the right flank
                var body = new FakeCourageBody();
                var block = new FakeBlockBody();
                var logic = NewStep74Logic(body, block);
                S.Set(SettingsSchema.LineBlockHoldEnabled, false, SettingSources.File);
                double t = 80000;
                var (recruits, line) = RecruitsChargeALine(logic, body, block, 19000);
                var off = RunCharge(logic, body, recruits, line, t, 16);
                var offShape = BlockShape(recruits, allowance);
                Console.WriteLine("        step 74 OFF (the game's charge): contact at +" + F2s(off.contact - t) + " s; the men " + F2s(offShape.width1090) + " m wide (10th-90th), the widest "
                                  + F2s(offShape.widest) + " m from the median, " + offShape.beyond + " men beyond " + F2s(allowance) + " m");
                Check(offShape.widest > 30 && offShape.beyond >= 10, "the stand-in charge did not dissolve the block (the A / B): widest " + F2s(offShape.widest) + " m, " + offShape.beyond + " beyond");
                Check(logic.BlockingNow == 0 && logic.BlockStats.Theirs.Looks[0, 0] + logic.BlockStats.Theirs.Looks[0, 1] > 0,
                    "OFF: the block hold steered, or measured nothing (" + logic.BlockingNow + ")");
                double offContact = off.contact - t;

                // ---- 2. ON: the same charge stays a block within its width and reaches contact as fast
                body = new FakeCourageBody();
                block = new FakeBlockBody();
                logic = NewStep74Logic(body, block);
                LogHas("[lines] mission start: the block hold (step 74): a formation of low line discipline");
                t = 81000;
                (recruits, line) = RecruitsChargeALine(logic, body, block, 19500);
                double worst = 0;
                int worstBeyond = 0;
                var on = RunCharge(logic, body, recruits, line, t, 16, s =>
                {
                    if (s < 3) return; // the first frames: the frame being learned
                    var sh = BlockShape(recruits, allowance);
                    if (sh.widest > worst) worst = sh.widest;
                    if (sh.beyond > worstBeyond) worstBeyond = sh.beyond;
                });
                var onShape = BlockShape(recruits, allowance);
                var them = logic.BlockStats.Theirs;
                long pulled = them.Pulls[(int)BlockPull.Lateral] + them.Pulls[(int)BlockPull.Ahead] + them.Pulls[(int)BlockPull.Behind];
                Console.WriteLine("        step 74 ON (the block hold): contact at +" + F2s(on.contact - t) + " s (OFF +" + F2s(offContact) + "); the men " + F2s(onShape.width1090)
                                  + " m wide (10th-90th), the widest " + F2s(onShape.widest) + " m (the worst over the run " + F2s(worst) + " m, " + worstBeyond + " men beyond " + F2s(allowance)
                                  + " m at worst); pulled " + pulled + " times by " + them.PulledMen + " men (round the flank " + them.Pulls[(int)BlockPull.Lateral] + "), striking looks "
                                  + them.LookKinds[(int)BlockLookKind.Striking]);
                Check(onShape.width1090 <= 44.05 + 0.5, "ON: the recruits are wider than their 44 m (10th-90th): " + F2s(onShape.width1090));
                Check(worst <= allowance + 0.5 && worstBeyond == 0, "ON: men trailed round the flank beyond the block: the widest " + F2s(worst) + " m, " + worstBeyond + " beyond");
                Check(!double.IsNaN(on.contact) && on.contact - t <= offContact + 1.0, "ON: the block stalled - contact at +" + F2s(on.contact - t) + " s vs OFF +" + F2s(offContact));
                Check(pulled > 0 && them.PulledMen > 0 && them.Pulls[(int)BlockPull.Lateral] > 0, "ON: nobody was walked back round the flank");
                Check(them.LookKinds[(int)BlockLookKind.Striking] > 0, "ON: no man in contact was let go to fight");
                Check(recruits.Count(m => m.Y > -3) >= 60, "ON: the block did not reach the line (" + recruits.Count(m => m.Y > -3) + " men within 3 m of it)");
                LogHas("[block] first look at the enemy's Infantry (the other side, AI) at ");
                LogHas("[block] FIRST CONTACT of the enemy's Infantry at ");
                LogHas("[block] first man walked back into his block at ");

                // ---- 3. the live switch: off = every pull let go at once; on again = held again
                S.Set(SettingsSchema.LineBlockHoldEnabled, false, SettingSources.Mcm);
                logic.TickCourage(on.t + 0.1);
                Check(logic.BlockingNow == 0 && recruits.All(m => m.St.Input == null || !m.St.Input.Block), "LineBlockHoldEnabled off live: men still walked back");
                S.Set(SettingsSchema.LineBlockHoldEnabled, true, SettingSources.Mcm);
                LogHas("switched OFF mid-mission at");

                // ---- 4. the summary
                logic.WriteCourageSummary();
                LogHas("[summary] block hold (step 74): ON at the end");
                LogHas("[summary] block hold (step 74), the other side - each man's lateral offset from his block's centre line vs its half-width");
                LogHas("[summary] block shape, the enemy's Infantry (the other side, AI): first contact at ");
                LogHas("Charge / BehaviorTacticalCharge / Line 100%");

                // ---- 5. an ELITE unit (D 1): its cap is its whole count - never touched, the same charge
                body = new FakeCourageBody();
                block = new FakeBlockBody();
                logic = NewStep74Logic(body, block);
                t = 82000;
                (recruits, line) = RecruitsChargeALine(logic, body, block, 19900, depth: 1.0);
                int most = 0;
                RunCharge(logic, body, recruits, line, t, 16, s => most = Math.Max(most, logic.BlockingNow));
                var elite = logic.BlockStats.Theirs;
                Console.WriteLine("        step 74 elites (D 1): looks as elites " + elite.LookKinds[(int)BlockLookKind.Elite] + ", walked back at most " + most + " at once");
                Check(most == 0 && elite.PulledMen == 0 && elite.LookKinds[(int)BlockLookKind.Elite] > 0, "a D 1 elite unit was held: " + most + " at once, " + elite.PulledMen + " men");

                // ---- 6. the input frame: the block's vector is the LAST wish - the fear flock and courage's walk back win
                var s0 = new AiInputState { Block = true, BlockX = 1, BlockY = 0 };
                uint flags = 0;
                float vx = 0, vy = 1;
                Check(AiInputHook.ApplyBlock(s0, ref flags, ref vx, ref vy) && vx == 1 && vy == 0, "the block's frame did not write its vector");
                s0.Flock = true;
                S.Set(SettingsSchema.FearFlockEnabled, true, SettingSources.Mcm);
                vx = 0;
                vy = 1;
                Check(!AiInputHook.ApplyBlock(s0, ref flags, ref vx, ref vy) && vx == 0 && vy == 1, "the block's vector beat the fear flock's");
                s0.Flock = false;
                s0.Regroup = true;
                S.Set(SettingsSchema.CourageEnabled, true, SettingSources.Mcm);
                Check(!AiInputHook.ApplyBlock(s0, ref flags, ref vx, ref vy), "the block's vector beat courage's walk back");
                s0.Regroup = false;
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                Check(!AiInputHook.ApplyBlock(s0, ref flags, ref vx, ref vy), "the block's vector with the master switch off");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
            }
            finally
            {
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        /// <summary>The cost: 1000 men - 500 recruits (D 6, 84 files) charging with 100 of them trailing far out on the flank, 500 legionaries - every look's
        /// block frame, every pulled man steered - small, no bytes.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Step74CostStaysFlat()
        {
            var keep = _logic;
            try
            {
                var body = new FakeCourageBody();
                var block = new FakeBlockBody();
                var logic = NewStep74Logic(body, block);
                const int men = 1000;
                double t0 = 83000;
                body.Orders[1] = FearOrder.Charge;
                for (int i = 0; i < 400; i++) FearMan(logic, body, 21000 + i, (i % 84) * 1.5 - 62, -10 - (i / 84) * 1.2, 1, 1, legionary: false, morale: 70, bar: 1.0, slot: false);
                for (int i = 0; i < 100; i++) FearMan(logic, body, 21400 + i, 70 + i * 0.5, -8 - (i % 3), 1, 1, legionary: false, morale: 70, bar: 1.0, slot: false);
                for (int i = 0; i < men / 2; i++) FearMan(logic, body, 22000 + i, (i % 100) * 1.5 - 75, (i / 100) * 1.2, 2, 2, legionary: true, morale: 70, bar: 1.0);
                body.EnemyFronts[1] = (0, 2.4, 0, -1, 150, 6);
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
                Console.WriteLine("        step 74 cost: " + men + " men (" + logic.BlockingNow + " walked back into their block now) - the courage + fear + block tick "
                                  + msPerTick.ToString("0.000", CultureInfo.InvariantCulture) + " ms (the stand-in's scans are plain loops over all 1000), " + allocated + " bytes over " + ticks + " ticks");
                Check(logic.BlockingNow > 0, "cost: nobody of the trail is walked back");
                Check(msPerTick < 3.0, "cost: the courage + fear + block tick over " + men + " men took " + msPerTick + " ms (budget 3 ms with the stand-in's O(n) reads)");
                Check(allocated < 256 * 1024, "cost: step 74 allocates - " + allocated + " bytes over " + ticks + " ticks");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        /// <summary>The master-switch step's part: off = every man walked back into his block let go at once; on = held again.</summary>
        private static void BlockFollowsTheMasterSwitch()
        {
            var keep = _logic;
            try
            {
                var body = new FakeCourageBody();
                var block = new FakeBlockBody();
                var logic = NewStep74Logic(body, block);
                double t = 84000;
                var (recruits, line) = RecruitsChargeALine(logic, body, block, 23000);
                foreach (var m in recruits.Where(m => m.X > 10)) m.X += 30; // a trail far round the flank
                for (int k = 0; k < 6; k++) logic.TickCourage(t += 0.5);
                Check(logic.BlockingNow > 0, "precondition: the trail is not walked back (step 74)");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                logic.TickCourage(t += 0.1);
                Check(logic.BlockingNow == 0 && recruits.All(m => m.St.Input == null || !m.St.Input.Block), "mod off: men still walked back into their block (step 74)");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                for (int k = 0; k < 3; k++) logic.TickCourage(t += 0.5);
                Check(logic.BlockingNow > 0, "mod back on: the trail not walked back again (step 74)");
                _ = line;
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

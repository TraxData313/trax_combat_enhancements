using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 42 - A SMALL STEP OUT OF THE WALL (AI_NOTES "Step 42"): the REAL <see cref="AthleticsLogic"/> with
    /// step 41's stand-in engine side, men who walk while they are stepping out (2 m/s) and walk back while held (1 m/s) - the
    /// leash biting at L within the 0.1 s look, the return, the next step, the enemy gone, the leash changed live (0, 2), the master
    /// switch, the summary - and the cost. What only the game can tell: that the native AI really closes on its enemy when freed and
    /// lands blows (PLAYTEST D12).
    /// </summary>
    internal static partial class Program
    {
        private const double LeashWalkOut = 2.0;  // m/s - a man stepping out at his enemy
        private const double LeashWalkBack = 1.0; // m/s - a held man walking back to his slot

        private static string F2s(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        /// <summary>A man of a SQUARE on its left side: slot (−5, 0), the centre (0, 0), the formation facing +y - his "out" is −x.</summary>
        private static WallFacts SquareSideMan(double outM)
        {
            var f = WallMan(arrangement: WallArrangement.Square);
            f.SlotX = -5; f.SlotY = 0;
            f.CentreX = 0; f.CentreY = 0;
            f.AgentX = -5 - outM; f.AgentY = 0;
            return f;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WallStepOutThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                WallDefaults();
                S.Set(SettingsSchema.ShieldWallStepOutMetres, 1.0, SettingSources.File);
                var body = new FakeWallBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.WallBody = body;
                var ws = logic.WallStats;
                var ls = ws.Leash;
                LogHas("a held man may step up to 1.0 m out of his slot at an enemy close in front of it (within 1.0 + 2.0 m of the slot), then is held again and walks back");
                double t = 15000;

                // the men: 900 a shield wall's front man, his enemy 2.5 m in front of his slot (out of his reach, inside L + 2 m);
                // 901 no enemy; 902 his enemy BEHIND his slot; 903 his enemy 5 m in front (beyond L + 2 m); 904 a square's side man,
                // his enemy 2.5 m out to the side
                var men = new Dictionary<int, TrackedAgent>();
                var outs = new Dictionary<int, double>();
                void Man(int i, double outM)
                {
                    men[i] = logic.Track(FakeAgent(i))!;
                    outs[i] = outM;
                    body.Facts[i] = i == 904 ? SquareSideMan(outM) : WallMan(ahead: outM);
                }
                Man(900, 0); body.Enemies[900] = (0, 2.5);
                Man(901, 0);
                Man(902, 0); body.Enemies[902] = (0, -2);
                Man(903, 0); body.Enemies[903] = (0, 5);
                Man(904, 0); body.Enemies[904] = (-7.5, 0);
                bool Free(int i) => men[i].Wall != null && men[i].Wall!.Leash.Free;

                // one frame: the logic's pre-tick, then the men move - out at their enemy while free, back while held
                double max900 = 0;
                void Frame()
                {
                    t += 1.0 / 60;
                    logic.TickWall(t);
                    foreach (int i in new[] { 900, 904 })
                    {
                        double o = outs[i];
                        if (Free(i)) o += LeashWalkOut / 60;
                        else if (o > 0) o = Math.Max(0, o - LeashWalkBack / 60);
                        outs[i] = o;
                        body.Facts[i] = i == 904 ? SquareSideMan(o) : WallMan(ahead: o);
                    }
                    if (outs[900] > max900) max900 = outs[900];
                }
                void Frames(double seconds)
                {
                    for (int k = (int)Math.Round(seconds * 60); k > 0; k--) Frame();
                }
                bool FramesUntil(Func<bool> done, double seconds)
                {
                    for (int k = (int)Math.Round(seconds * 60); k > 0; k--)
                    {
                        if (done()) return true;
                        Frame();
                    }
                    return done();
                }

                // ---- the first look: the two men with an enemy close in front step out at once - vanilla's value, nothing written
                logic.TickWall(t);
                Check(Free(900) && Free(904) && !Free(901) && !Free(902) && !Free(903), "the first look did not free exactly 900 and the square's side man 904");
                Check(body.Held(900) == 0 && body.Held(904) == 0 && body.Held(901) == 1 && body.Held(902) == 1 && body.Held(903) == 1 && logic.WalledNow == 5,
                    "the first look: the stepping men written, or the held men not");
                Check(ls.Steps == 2 && ls.MenStepped == 2 && ws.MenHeld == 5, "the first look's counts: steps " + ls.Steps + ", men " + ls.MenStepped);
                LogHas("[wall] first step out of the wall this mission: (agent 900) at 15000.0 s - his enemy 2.50 m out of his slot (close: within the leash 1.0 + 2.0 m of the slot), he stood 0.00 m out");

                // ---- 900 walks out at 2 m/s: no write at vanilla's update while he is out, reined within the 0.1 s look past L
                int held900 = body.Held(900);
                body.Stamps[900] = 0.5f; // vanilla's update on him while he is out
                Check(FramesUntil(() => !Free(900), 1.5), "900 was never reined");
                double over = max900 - 1.0;
                Console.WriteLine("        step 42: walking out at " + F2s(LeashWalkOut) + " m/s, reined at " + F2s(max900) + " m out of his slot (the leash 1.0 m, "
                                  + F2s(over) + " m past it - a " + F2s(LeashMath.LookSeconds) + " s look; a 0.25 s one would allow 0.5 m)");
                Check(max900 >= 1.0 && over <= LeashWalkOut * LeashMath.LookSeconds + LeashWalkOut / 60 + 1e-6, "the leash did not bite within one 0.1 s look of L: reined at " + max900);
                Check(body.Held(900) == held900 + 1 && ws.Writes(WallWriteKind.Leash) >= 1 && ws.Writes(WallWriteKind.AfterVanilla) == 0,
                    "reined: not one \"keep your slot\" write by the leash, or written after vanilla's update while he was out");
                Check(ls.Ends(LeashEnd.ReachedLeash) >= 1, "the step's end was not counted 'reached the leash'");
                LogHas("[wall] first step out ended: (agent ");
                LogHas(" - reached the leash: the farthest ");
                LogHas(" - held again (\"keep your slot\" written TRUE), he walks back");
                Check(men[904].Wall!.EverStepped && ls.Ends(LeashEnd.ReachedLeash) <= 2, "the square's side man did not step out toward his side");

                // ---- he walks back (1 m/s): no new step before he is back in his slot; back = may step again - the enemy still there
                int steps = ls.Steps;
                Check(FramesUntil(() => outs[900] <= 0.5, 1.5) && ls.Steps == steps && !Free(900), "a new step before he was back in his slot");
                Check(FramesUntil(() => Free(900), 1.5) && ls.BackInSlot >= 1 && ls.Steps > steps, "back in his slot, the enemy still close: no second step");

                // ---- the enemy backs off far while he is out: held within the 0.1 s look, "enemy gone"
                body.Enemies[900] = (0, 10);
                Frames(LeashMath.LookSeconds + 2.0 / 60);
                Check(!Free(900) && ls.Ends(LeashEnd.EnemyGone) == 1, "the enemy gone: he stayed out");
                body.Enemies[900] = (0, 2.5);
                Check(FramesUntil(() => Free(900), 3), "the enemy back in front: no new step");

                // ---- the others never stepped: no enemy, an enemy behind his slot, one beyond the leash + a strike
                Check(!men[901].Wall!.EverStepped && !men[902].Wall!.EverStepped && !men[903].Wall!.EverStepped, "a man without an enemy close in front stepped out");

                // ---- the leash to 0 live while he is out: held within the look, "leash set to 0"; then no enemy read at all
                S.Set(SettingsSchema.ShieldWallStepOutMetres, 0.0, SettingSources.Mcm);
                Frames(LeashMath.LookSeconds + 2.0 / 60);
                Check(!Free(900) && !Free(904) && ls.Ends(LeashEnd.LeashOff) >= 1, "the leash set to 0: a man stayed out");
                LogHas("[wall] ShieldWallStepOutMetres 1.0 → 0.0 m mid-mission at ");
                LogHas("no step out (ShieldWallStepOutMetres 0 - the hard hold of step 41) - a man stepping out now is looked at again within 0.10 s");
                int reads = body.EnemyReads, stepsAt0 = ls.Steps, released = body.ReleaseCalls;
                Frames(2);
                Check(body.EnemyReads == reads && ls.Steps == stepsAt0 && body.ReleaseCalls == released, "the leash at 0: an enemy read, a step or a release (step 41 exactly reads none)");

                // ---- 2 m live: he steps again, reined at 2 m
                S.Set(SettingsSchema.ShieldWallStepOutMetres, 2.0, SettingSources.Mcm);
                Check(FramesUntil(() => outs[900] <= 0.05, 2) && FramesUntil(() => Free(900), 1), "the leash at 2 m: no new step");
                max900 = 0;
                Check(FramesUntil(() => !Free(900), 2), "the leash at 2 m: never reined");
                Check(max900 >= 2.0 && max900 <= 2.0 + LeashWalkOut * LeashMath.LookSeconds + LeashWalkOut / 60 + 1e-6, "the leash at 2 m reined at " + max900);

                // ---- the master switch while a man is out: everyone given vanilla's value, the step "let go"
                Check(FramesUntil(() => Free(900), 3), "no step before the master switch check");
                int letGo = ls.Ends(LeashEnd.LetGo);
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                Frames(1.0 / 60);
                Check(logic.WalledNow == 0 && !Free(900) && ls.Ends(LeashEnd.LetGo) > letGo, "mod off: a man still out or held");
                Frames(0.5);
                Check(logic.WalledNow == 0 && !Free(900), "mod off: stepped or held again");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                Frames(0.5);
                Check(logic.WalledNow == 5, "mod on: not held again");

                // ---- the summary
                logic.WriteWallSummary();
                LogHas("[summary] wall step out (step 42): leash 2.0 m now - ");
                LogHas(" men (");
                LogHas(" a man a minute while held, out of their slots ");
                LogHas("; ended: reached the leash ");
                LogHas("; reined at the leash ");
                LogHas("; back in their slots after a step ");
                Check(ws.Errors == 0, "errors: " + ws.Errors);
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                AthleticsDefaults();
                WallDefaults();
            }
        }

        /// <summary>Step 42: with the leash at 0 a man with an enemy close in front gets EXACTLY step 41's writes - the same scripted
        /// 3 s (vanilla's update every 0.5 s, the staggered look) with and without the enemy, and not one enemy read.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WallLeashZeroIsStep41()
        {
            var keep = _logic;
            try
            {
                int Run(bool enemy, out int releases, out int reads)
                {
                    AthleticsDefaults();
                    WallDefaults(); // the leash 0
                    var body = new FakeWallBody();
                    var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                    logic.WallBody = body;
                    logic.Track(FakeAgent(950));
                    body.Facts[950] = WallMan(ahead: 0.2);
                    if (enemy) body.Enemies[950] = (0, 2.0);
                    double t = 16000;
                    for (int k = 0; k <= 180; k++)
                    {
                        if (k % 30 == 0) body.Stamps[950] = k;
                        logic.TickWall(t + k / 60.0);
                    }
                    releases = body.ReleaseCalls;
                    reads = body.EnemyReads;
                    Check(logic.WallStats.Leash.Steps == 0, "the leash at 0: a step out");
                    return body.HoldCalls;
                }
                int with = Run(true, out int relWith, out int readsWith);
                int without = Run(false, out int relWithout, out int readsWithout);
                Check(with == without && relWith == relWithout && readsWith == 0 && readsWithout == 0 && with > 0,
                    "the leash at 0: " + with + " writes with an enemy in front vs " + without + " without, " + readsWith + " enemy reads (must be equal, and 0 reads)");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
                WallDefaults();
            }
        }

        /// <summary>Step 42's cost: a thousand held men with the leash at 1 m, every fifth with an enemy close in front (200 stepping
        /// out, looked at every 0.1 s), vanilla's update every 0.5 s - the pre-tick stays small and allocates nothing.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WallLeashCostStaysFlat()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                WallDefaults();
                S.Set(SettingsSchema.ShieldWallStepOutMetres, 1.0, SettingSources.File);
                var body = new FakeWallBody { Record = false };
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.WallBody = body;
                const int men = 1000;
                double t0 = 17000;
                for (int i = 0; i < men; i++)
                {
                    logic.Track(FakeAgent(6000 + i));
                    body.Facts[6000 + i] = WallMan(ahead: 0.1);
                    body.Stamps[6000 + i] = 0f;
                    if (i % 5 == 0)
                    {
                        body.Enemies[6000 + i] = (0, 2.5);
                        body.Contact.Add(6000 + i);
                    }
                }
                logic.TickWall(t0);
                Check(logic.WalledNow == men && logic.WallStats.Leash.Steps == men / 5, "cost: not every man held, or not every fifth out: " + logic.WallStats.Leash.Steps);
                const int ticks = 600;
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var sw = new Stopwatch();
                int reads = body.EnemyReads;
                for (int k = 1; k <= ticks; k++)
                {
                    for (int i = k % 30; i < men; i += 30) body.Stamps[6000 + i] = k;
                    sw.Start();
                    logic.TickWall(t0 + k / 60.0);
                    sw.Stop();
                }
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double msPerTick = sw.Elapsed.TotalMilliseconds / ticks;
                int read = body.EnemyReads - reads;
                Console.WriteLine("        step 42 cost: " + men + " men held, 200 out on the leash - the pre-tick " + msPerTick.ToString("0.000", CultureInfo.InvariantCulture)
                                  + " ms a tick, " + allocated + " bytes allocated over " + ticks + " ticks, " + read + " enemy reads (" + (read / (double)men / 10).ToString("0.0", CultureInfo.InvariantCulture) + " a man a second)");
                Check(msPerTick < 0.5, "cost: the pre-tick with the leash took " + msPerTick + " ms (budget 0.5 ms)");
                Check(allocated < 256 * 1024, "cost: the leash allocates - " + allocated + " bytes over " + ticks + " ticks");
                Check(logic.WallStats.Leash.Steps == men / 5 && logic.WallStats.Leash.LeashSamples > 0, "cost: the men out did not stay out, or no leash sample");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
                WallDefaults();
            }
        }
    }
}

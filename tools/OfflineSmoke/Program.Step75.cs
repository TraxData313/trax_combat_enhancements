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
    /// Offline checks for step 75 - A STANDING SHIELD WALL NEVER LEAVES ITS SLOT (Toni 2026-10-22: "the men in the shield wall still step forward even
    /// when on a standing order, they take a few steps to strike at an enemy, then fall back"; AI_NOTES "Step 75"). The REAL logic with step 41's stand-in
    /// engine side and a stand-in world: ten men of a halted wall, an enemy 2.2 m in front of each (inside step 42's leash + reach), the men walking out
    /// at 2 m/s while the leash frees them, backing at 1 m/s while walked back, the game's own approach creeping them 0.3 m/s at the enemy while the
    /// engine's frame is not pinned (the Stop's "stand where you are"), a shove of 0.5 m on one man every 0.4 s, swings every 1.3 s. ON: within ~0.3 m,
    /// swinging from the slot; OFF (live): they step out as today; an ADVANCE: not applied; a MOVE only at its place. What only the game can tell: that the
    /// engine honours the pinned frame and the tight tolerance, and that men really swing from the slot (PLAYTEST D40).
    /// </summary>
    internal static partial class Program
    {
        private sealed class FirmWorld
        {
            public readonly AthleticsLogic Logic;
            public readonly FakeWallBody Body;
            public readonly List<TrackedAgent> Men = new List<TrackedAgent>();
            public readonly Dictionary<int, double> Out = new Dictionary<int, double>();
            public BraceOrderKind Order = BraceOrderKind.Stop;
            public double FormationFromPlace;
            public double FormationSpeed;
            public double T;
            private int _frame;

            // per-run measures
            public double SumOut;
            public long Samples, Beyond035, Beyond05;
            public double MaxOut;
            public int Steps;

            public FirmWorld(AthleticsLogic logic, FakeWallBody body, int first, int men, double t)
            {
                Logic = logic;
                Body = body;
                T = t;
                for (int i = 0; i < men; i++)
                {
                    int id = first + i;
                    Men.Add(logic.Track(FakeAgent(id))!);
                    Out[id] = 0;
                    body.Enemies[id] = (0, 2.2);
                    body.Contact.Add(id);
                    body.Stamps[id] = 0f;
                    body.Facts[id] = Facts(0);
                }
            }

            public WallFacts Facts(double ahead)
            {
                var f = WallMan(ahead: ahead, order: Order);
                if (Order == BraceOrderKind.Move)
                {
                    f.HasFormationOrder = true;
                    f.FormationX = 0;
                    f.FormationY = FormationFromPlace;
                    f.FormationOrderX = 0;
                    f.FormationOrderY = 0;
                }
                f.VelY = FormationSpeed;
                return f;
            }

            public void ResetMeasures()
            {
                SumOut = 0;
                Samples = Beyond035 = Beyond05 = 0;
                MaxOut = 0;
            }

            public void Run(double seconds, bool shove = true)
            {
                int frames = (int)Math.Round(seconds * 60);
                for (int k = 0; k < frames; k++)
                {
                    _frame++;
                    T += 1.0 / 60;
                    // vanilla's ~0.5 s update on each man, staggered
                    for (int i = 0; i < Men.Count; i++)
                        if ((_frame + i * 3) % 30 == 0) Body.Stamps[Men[i].AgentIndex] = _frame;
                    Logic.TickWall(T);
                    for (int i = 0; i < Men.Count; i++)
                    {
                        var st = Men[i];
                        int id = st.AgentIndex;
                        double o = Out[id];
                        var ws = st.Wall;
                        bool free = ws != null && ws.Leash.Free;
                        bool pulled = st.Input != null && st.Input.WallPull;
                        bool pinned = Body.Paced.ContainsKey(id);
                        if (free) o += 2.0 / 60;                            // the leash freed him: the native AI closes on its enemy
                        else if (pulled) o -= 1.0 / 60;                     // walked back (step 63's backpedal)
                        else if (!pinned && o < 0.6) o += 0.3 / 60;         // the game's own approach (a Stop's frame = where he stands)
                        else if (o > 0) o = Math.Max(0, o - 1.0 / 60);      // the flag / the pinned frame pulls him in
                        if (o < -0.1) o = -0.1;
                        if (shove && (_frame + i * 24) % 240 == 0) o += 0.5; // a shove, a blow, a lunge: each man every 4 s
                        if ((_frame + i * 7) % 78 == 0) Logic.NoteFirmAttack(st, false, T); // he swings (every 1.3 s) - from where he stands
                        Out[id] = o;
                        Body.Facts[id] = Facts(o);
                        double a = Math.Max(0, o);
                        SumOut += a;
                        Samples++;
                        if (a > 0.35) Beyond035++;
                        if (a > 0.5) Beyond05++;
                        if (a > MaxOut) MaxOut = a;
                    }
                }
            }

            public double Mean => Samples > 0 ? SumOut / Samples : 0;
            public double Share035 => Samples > 0 ? (double)Beyond035 / Samples : 0;
            public double Share05 => Samples > 0 ? (double)Beyond05 / Samples : 0;
            public bool AnyFree => Men.Any(m => m.Wall != null && m.Wall.Leash.Free);
        }

        private static void Step75On()
        {
            S.Set(SettingsSchema.ShieldWallHoldsSlots, true, SettingSources.File);
            S.Set(SettingsSchema.ShieldWallStepOutMetres, 1.0, SettingSources.File);
            S.Set(SettingsSchema.ShieldWallPullBackSeconds, 0.5, SettingSources.File);
            foreach (var p in new[]
            {
                SettingsSchema.ShieldWallAdvanceAsOne, SettingsSchema.ShieldWallKickReturnMetres, SettingsSchema.ShieldWallStandFirm, SettingsSchema.ShieldWallStandFirmSlackMetres,
                SettingsSchema.ShieldWallStandFirmArrivedMetres, SettingsSchema.ShieldWallStandFirmStillMetresPerSecond, SettingsSchema.ShieldWallStandFirmPinFrame,
                SettingsSchema.ShieldWallStandFirmTightTolerance,
            })
                S.Set(p, p.Default, SettingSources.File);
        }

        private static string P(double share) => (100 * share).ToString("0.0", CultureInfo.InvariantCulture) + "%";

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void StandingWallStandsFirm()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                WallDefaults();
                Step75On();
                Check(S.ShieldWallStandFirm && Math.Abs(S.ShieldWallStandFirmSlackMetres - 0.3) < 1e-6 && S.ShieldWallStandFirmPinFrame && S.ShieldWallStandFirmTightTolerance,
                    "stand firm is not on (slack 0.3 m, the frame pinned, the tolerance tight) by default");
                var body = new FakeWallBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.WallBody = body;
                var fs = logic.StandFirmStats;
                LogHas("[wall] stand firm (step 75): ON (ShieldWallStandFirm) - an AI man held in a shield wall, square or circle whose formation is on a STOP");
                var w = new FirmWorld(logic, body, 7500, 10, 17500);

                // ---- ON, a STOP: nobody steps out, the frame pinned to the slot, the tolerance 0.3, shoves walked back, swings from the slot
                w.Run(20);
                double onMean = w.Mean, on035 = w.Share035, on05 = w.Share05, onMax = w.MaxOut;
                int onFrom = fs.StrikesFromSlot(true), onOut = fs.StrikesOut(true);
                Console.WriteLine("        step 75 ON (a stop): mean ahead of the slot " + F2s(onMean) + " m, beyond 0.35 m " + P(on035) + ", beyond 0.5 m " + P(on05) + ", the farthest "
                                  + F2s(onMax) + " m; steps out " + logic.WallStats.Leash.Steps + " (refused " + fs.Refused(StandFirmLever.StepOut) + "); walked back " + fs.Spells
                                  + " times (back in " + F2s(fs.Back.Mean) + " s); strikes from the slot " + onFrom + ", out of it " + onOut);
                Check(logic.WallStats.Leash.Steps == 0 && !w.AnyFree, "ON: a man standing firm stepped out on the leash");
                Check(fs.Refused(StandFirmLever.StepOut) >= 10, "ON: the steps the leash would have given are not counted as refused: " + fs.Refused(StandFirmLever.StepOut));
                Check(fs.MenStood == 10 && w.Men.All(m => m.Wall!.Firm), "ON: not every man of the halted wall stands firm");
                Check(body.Paced.Count == 10 && body.Paced.Values.All(p => Math.Abs(p.X) < 1e-9 && Math.Abs(p.Y) < 1e-9) && fs.FramesPinned > 100,
                    "ON: the engine's walk target is not pinned to each man's slot");
                Check(body.Deviations.Values.All(d => Math.Abs(d - 0.3) < 1e-6) && fs.ToleranceWrites > 100, "ON: the tolerance handed to the engine is not the slack 0.3 m");
                Check(onMean <= 0.2 && on05 <= 0.12 && onMax <= 0.85, "ON: the wall's men do not keep within ~0.3 m of their slots: mean " + onMean + ", beyond 0.5 m " + on05 + ", max " + onMax);
                Check(fs.Spells >= 30 && fs.Back.Count >= 30 && fs.Back.Mean < 0.8 && fs.Refused(StandFirmLever.Pushed) == fs.Spells, "ON: the shoves were not walked straight back: " + fs.Spells + " / " + fs.Back.Count);
                Check(onFrom >= 100 && onOut <= onFrom / 10, "ON: the men do not swing from their slots: " + onFrom + " / " + onOut);
                LogHas("[wall] first man STANDING FIRM this mission (step 75): (agent 7500) at ");
                LogHas("(a stop stands at once); he stands 0.00 m out of his slot - no step out, no sprint, walked back beyond 0.30 m, the engine's walk target his slot, the tolerance 0.30 m (the formation's 1.00 m)");
                LogHas("[wall] first lever refused for a man standing firm (step 75) - the step out (leash): (agent 75");
                LogHas("[wall] first lever refused for a man standing firm (step 75) - pushed / the game's approach / a blow: (agent 75");
                LogHas("[wall] first man standing firm back in his slot (step 75): (agent 75");
                LogHas("[wall] first strike by a man standing firm (step 75): (agent 75");

                // ---- OFF live: the leash steps them out as today, the game's frame given back at once
                S.Set(SettingsSchema.ShieldWallStandFirm, false, SettingSources.Mcm);
                w.ResetMeasures();
                w.Run(20);
                int offSteps = logic.WallStats.Leash.Steps;
                Console.WriteLine("        step 75 OFF (live): mean ahead of the slot " + F2s(w.Mean) + " m, beyond 0.35 m " + P(w.Share035) + ", beyond 0.5 m " + P(w.Share05) + ", the farthest "
                                  + F2s(w.MaxOut) + " m; steps out " + offSteps + "; strikes from the slot " + fs.StrikesFromSlot(false) + ", out of it " + fs.StrikesOut(false));
                LogHas("[wall] stand firm (step 75) changed mid-mission at ");
                LogHas(" - the standing walls' men back to the leash and the game's frame within 0.25 s");
                Check(offSteps >= 9 && w.Mean > onMean + 0.2 && w.Share05 > on05 + 0.2, "OFF: the wall does not step out as today: steps " + offSteps + ", mean " + w.Mean + ", beyond 0.5 m " + w.Share05);
                Check(body.Paced.Count == 0 && w.Men.All(m => !m.Wall!.Firm), "OFF: the game's frame not given back / men still standing firm");
                Check(body.Deviations.Values.All(d => Math.Abs(d - 1.0) < 1e-6), "OFF: the formation's own tolerance not handed back");
                Check(fs.StrikesOut(false) > fs.StrikesOut(true), "OFF: no strikes out of the slot counted");

                // ---- ON again, then an ADVANCE: not applied (the leash steps them)
                S.Set(SettingsSchema.ShieldWallStandFirm, true, SettingSources.Mcm);
                w.Run(3);
                Check(w.Men.All(m => m.Wall!.Firm) && !w.AnyFree, "ON again: not standing firm within a look");
                w.Order = BraceOrderKind.Advance;
                int before = logic.WallStats.Leash.Steps;
                w.Run(5);
                Check(w.Men.All(m => !m.Wall!.Firm) && logic.WallStats.Leash.Steps > before && fs.Looks(StandFirmVerdict.NotAHoldOrder) > 0,
                    "an ADVANCE: stand firm applied (it must not be)");

                // ---- a MOVE: walking in = not applied; at its place and still = applied; a new place = not
                w.Order = BraceOrderKind.Move;
                w.FormationFromPlace = 8;
                w.FormationSpeed = 1.4;
                w.Run(2);
                Check(w.Men.All(m => !m.Wall!.Firm) && fs.Looks(StandFirmVerdict.NotArrived) > 0, "a MOVE 8 m from its place, walking: stand firm applied");
                w.FormationFromPlace = 1.2;
                w.FormationSpeed = 0.2;
                w.Run(2);
                Check(w.Men.All(m => m.Wall!.Firm), "a MOVE at its place and still: not standing firm");
                w.FormationSpeed = 1.6; // the fighting line shuffles: latched
                w.Run(1);
                Check(w.Men.All(m => m.Wall!.Firm), "a MOVE at its place, shuffling: the latch let go");
                w.FormationFromPlace = 5; // pushed far back: let go to the game's walk home
                w.Run(1);
                Check(w.Men.All(m => !m.Wall!.Firm), "a MOVE pushed 5 m from its place: still standing firm");
                w.FormationFromPlace = 0.3;
                w.FormationSpeed = 0;
                w.Run(1);

                // ---- a running sprint ends; his own kick carries him out and he is walked back after it
                S.Set(SettingsSchema.SprintEnabled, true, SettingSources.File);
                S.Set(SettingsSchema.SprintAiEnabled, true, SettingSources.File);
                var man = w.Men[0];
                var res = man.Sprint.TryStartAi(SprintRules.From(S), w.T, 5, SprintOrder.Move);
                Check(res == SprintStart.Started && man.Sprint.Active, "precondition: the sprint did not start: " + res);
                w.Run(0.6, shove: false);
                Check(!man.Sprint.Active && fs.Refused(StandFirmLever.Sprint) == 1, "a man standing firm kept his sprint");
                LogHas("[wall] first lever refused for a man standing firm (step 75) - a sprint: (agent 7500)");
                man.PrevAction = man.PrevLowerAction = 35;
                logic.ObserveLowerAction(man, KickBashTracker.ActionKick, w.T, Rules);
                w.Out[man.AgentIndex] = 0.6;
                w.Run(0.4, shove: false);
                Check(man.Wall!.Out.Out && man.Wall.Out.OwnKick && (man.Input == null || !man.Input.WallPull), "his kick: not out of place, or walked while the kick plays");
                logic.ObserveLowerAction(man, 35, w.T, Rules);
                w.Run(1.0, shove: false);
                Check(!man.Wall.Out.Out && fs.Refused(StandFirmLever.KickOrPunish) >= 1, "after his kick: not walked straight back");

                // ---- let go: a charge (other), far behind (rejoining), the master switch
                int otherBefore = fs.LetGoOther;
                w.Order = BraceOrderKind.Charge;
                w.Run(0.5, shove: false);
                Check(fs.LetGoOther >= otherBefore + 10 && w.Men.All(m => !m.Wall!.Held), "a CHARGE: the standing men not let go");
                w.Order = BraceOrderKind.Stop;
                w.Run(0.5, shove: false);
                var far = w.Men[1];
                var ff = w.Facts(0);
                ff.AgentY = -30;
                body.Facts[far.AgentIndex] = ff;
                logic.TickWall(w.T += 0.3);
                logic.TickWall(w.T += 0.3);
                Check(fs.LetGoFarBehind >= 1 && !far.Wall!.Held, "a man far behind his slot was not let go to rejoin");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                w.Run(0.5, shove: false);
                Check(w.Men.All(m => !m.Wall!.Firm && !m.Wall.Held) && body.Paced.Count == 0, "the master switch off: men still held / standing firm / frames pinned");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);

                logic.WriteWallSummary();
                LogHas("[summary] wall stands firm (step 75 - ShieldWallStandFirm ON now, slack 0.30 m): held ");
                LogHas(" | levers refused - the step out (leash) ");
                LogHas(", a sprint 1, ");
                LogHas(" | let go while standing firm: the wall broke (its morale) 0, far behind (rejoining) ");
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void StandFirmCostStaysFlat()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                WallDefaults();
                Step75On();
                var body = new FakeWallBody { Record = false };
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.WallBody = body;
                const int men = 1000;
                double t0 = 17900;
                for (int i = 0; i < men; i++)
                {
                    logic.Track(FakeAgent(9000 + i));
                    body.Facts[9000 + i] = WallMan(ahead: (i % 7) * 0.1);
                    body.Stamps[9000 + i] = 0f;
                    body.Enemies[9000 + i] = (0, 2.2);
                    if (i % 2 == 0) body.Contact.Add(9000 + i);
                }
                logic.TickWall(t0);
                const int ticks = 600;
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var sw = new Stopwatch();
                for (int k = 1; k <= ticks; k++)
                {
                    for (int i = k % 30; i < men; i += 30) body.Stamps[9000 + i] = k;
                    sw.Start();
                    logic.TickWall(t0 + k / 60.0);
                    sw.Stop();
                }
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double msPerTick = sw.Elapsed.TotalMilliseconds / ticks;
                Console.WriteLine("        step 75 cost: " + men + " men standing firm - the pre-tick " + msPerTick.ToString("0.000", CultureInfo.InvariantCulture) + " ms a tick, "
                                  + allocated + " bytes allocated over " + ticks + " ticks, " + logic.StandFirmStats.FramesPinned + " frames pinned, "
                                  + logic.StandFirmStats.Refused(StandFirmLever.StepOut) + " steps refused, " + logic.StandFirmStats.Spells + " walked back");
                Check(logic.StandFirmStats.MenStood == men, "cost: not every man stands firm: " + logic.StandFirmStats.MenStood);
                Check(msPerTick < 0.5, "cost: the pre-tick with stand firm over " + men + " men took " + msPerTick + " ms (budget 0.5 ms)");
                Check(allocated < 256 * 1024, "cost: stand firm allocates - " + allocated + " bytes over " + ticks + " ticks");
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

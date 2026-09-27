using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for the step back (step 5d) - the REAL <see cref="AthleticsLogic"/> bookkeeping
    /// (the roll at every swing's end by f, the queue, the cap, the timer read live, every release
    /// path, the switch-off, the logs and the summary) with a stand-in for the engine side
    /// (<see cref="IStepBackBody"/>): the fake agents have no native side, so the game's own checks,
    /// the scripted-movement call and its release are recorded instead of run. What it cannot check:
    /// that the engine moves the man back facing his enemy and the formation takes him again -
    /// PLAYTEST "Step back" and the [summary] step-back lines prove that in game.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>The engine side, played by the smoke: answers set per check, calls recorded.</summary>
        private sealed class FakeStepBody : IStepBackBody
        {
            public bool Allow = true;
            public StepBackRefusal NextRefusal = StepBackRefusal.None;
            public bool Take = true;
            public bool StillScriptedAfterRelease;
            public int MovementState = 1; // holding a line
            public readonly Dictionary<int, StepBackEnd> EndFor = new Dictionary<int, StepBackEnd>();
            public readonly List<int> Started = new List<int>();
            public readonly List<int> Released = new List<int>();
            public int Samples;

            public bool MissionAllows(Mission? mission) => Allow;

            public string MissionNote(Mission? mission) => "smoke mission";

            public StepBackRefusal Probe(TrackedAgent st, in StepBackRules r, ref StepBackPlan plan)
            {
                if (NextRefusal != StepBackRefusal.None)
                {
                    var why = NextRefusal;
                    NextRefusal = StepBackRefusal.None;
                    return why;
                }
                plan.From = new Vec3(10f, 10f, 0f);
                plan.Spot = new Vec3(10f, 10f - r.Distance, 0f); // the enemy stands to the north
                plan.Radians = StepBackMath.Radians(0, 1);
                plan.EnemyDistance = 1.6;
                plan.StartFacingCos = 0.95;
                plan.MovementOrder = 2;
                plan.Arrangement = 2;
                plan.MovementState = MovementState;
                plan.FlagsBefore = 0;
                return StepBackRefusal.None;
            }

            public bool Start(TrackedAgent st, ref StepBackPlan plan, in StepBackRules r)
            {
                if (!Take) return false;
                Started.Add(st.AgentIndex);
                plan.FlagsAfter = (int)(Agent.AIScriptedFrameFlags.GoToPosition | Agent.AIScriptedFrameFlags.DoNotRun
                                        | (r.HoldAttacks ? Agent.AIScriptedFrameFlags.NoAttack : 0));
                plan.OurFlags = plan.FlagsAfter & ~(int)Agent.AIScriptedFrameFlags.GoToPosition;
                return true;
            }

            public StepBackEnd Check(TrackedAgent st, in StepBackPlan plan) =>
                EndFor.TryGetValue(st.AgentIndex, out var why) ? why : StepBackEnd.None;

            public bool Sample(TrackedAgent st, in StepBackPlan plan, out StepBackSnapshot s)
            {
                Samples++;
                s = new StepBackSnapshot
                {
                    Valid = true, Position = new Vec3(10f, 9.2f, 0f), FacingCos = 0.9, SpeedAway = 0.8, EnemyDistance = 2.4,
                };
                return true;
            }

            public StepBackRelease Release(TrackedAgent st, in StepBackPlan plan)
            {
                Released.Add(st.AgentIndex);
                return new StepBackRelease
                {
                    Released = true, StillScripted = StillScriptedAfterRelease, FlagsAfter = 0,
                    End = new StepBackSnapshot { Valid = true, Position = new Vec3(10f, 8.8f, 0f), FacingCos = 0.97, SpeedAway = 0.2, EnemyDistance = 2.8 },
                };
            }
        }

        /// <summary>Dice that always show the same number.</summary>
        private sealed class FixedDice : IRandomSource
        {
            private readonly double _u;

            public FixedDice(double u) => _u = u;

            public double NextDouble() => _u;
        }

        private static Action<Agent, Agent?>? _setMount;

        private static void StepBackDefaults()
        {
            S.Set(SettingsSchema.StepBackEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.StepBackMaxChancePercent, 100, SettingSources.File);
            S.Set(SettingsSchema.StepBackDistance, 2.0, SettingSources.File);
            S.Set(SettingsSchema.StepBackSeconds, 1.5, SettingSources.File);
            S.Set(SettingsSchema.StepBackEnemyRange, 4.0, SettingSources.File);
            S.Set(SettingsSchema.StepBackHoldAttacks, true, SettingSources.File);
            S.Set(SettingsSchema.StepBackMaxAtOnce, 50, SettingSources.File);
        }

        /// <summary>A recruit (the floor pool, 50) swung empty: 5 swings.</summary>
        private static TrackedAgent EmptyRecruit(int index, ref double t)
        {
            var st = _logic!.Track(FakeAgent(index))!;
            for (int i = 0; i < 5; i++) Swing(st, ref t);
            return st;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void StepBackThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                StepBackDefaults();
                var logic = new AthleticsLogic();
                _logic = logic;
                SetStatic(typeof(AthleticsLogic), "_current", logic);
                typeof(AthleticsLogic).GetMethod("StartAthletics", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(logic, null);
                LogHas("[stepback] mission start: ON - after a melee swing an AI fighter on foot steps back with chance 100% x (1 - f) (0 at full strength), 2.0 m straight away from his enemy for up to 1.5 s, only with the enemy within 4.0 m, no swings while stepping back, at most 50 at once - read live; technique: a scripted step");
                var body = new FakeStepBody();
                logic.StepBackBody = body;
                logic.StepBackDice = new FixedDice(0.999); // only a chance of 1 (an empty bar) says yes
                var stats = logic.StepStats;
                double t = 1000;

                // --- the roll follows f: a recruit's 5 swings - f 1, 0.8, 0.53, 0.27, then empty
                var a = logic.Track(FakeAgent(100))!;
                Swing(a, ref t);
                Check(stats.Swings(0) == 1 && stats.Yes(0) == 0 && stats.MeanChance(0) == 0, "peak zone: the roll was not 0%");
                for (int i = 0; i < 3; i++) Swing(a, ref t);
                Check(stats.Swings(1) == 2 && stats.Swings(2) == 1 && stats.DiceYes == 0, "f 0.8 / 0.53 / 0.27 not binned or said yes to dice 0.999: bins "
                    + stats.Swings(1) + "/" + stats.Swings(2) + ", yes " + stats.DiceYes);
                Check(Math.Abs(stats.MeanChance(1) - (0.2 + (1 - 20 / 37.5)) / 2) < 1e-6 && Math.Abs(stats.MeanChance(2) - (1 - 10 / 37.5)) < 1e-6,
                    "the chance is not 100% x (1 - f): " + stats.MeanChance(1) + " / " + stats.MeanChance(2));
                Swing(a, ref t);
                Check(a.Exhausted && stats.Swings(3) == 1 && stats.Yes(3) == 1, "an empty swing did not roll a yes");
                Check(logic.SteppingNow == 0 && body.Started.Count == 0, "a step back started inside the swing (must wait for the tick)");

                // --- the tick starts it; a swing while stepping back is counted; the mid-step sample; time up
                logic.TickStepBacks(t);
                Check(logic.SteppingNow == 1 && body.Started.Count == 1 && stats.Started == 1 && stats.StartedHolding == 1, "the tick did not start the queued step back");
                LogHas("[stepback] first step back this mission: (agent 100) at ");
                LogHas("scripted flags none → GoToPosition|NoAttack|DoNotRun (GoToPosition set: the engine took it)");
                double started = t;
                Swing(a, ref t);
                Check(stats.SwingsWhileStepping == 1 && stats.NotRolled(StepBackNotRolled.AlreadyStepping) == 1, "a swing while stepping back was not counted / was rolled");
                logic.TickStepBacks(started + 0.8);
                Check(stats.MidSamples == 1 && logic.SteppingNow == 1, "no mid-step sample at half time");
                logic.TickStepBacks(started + 1.4);
                Check(logic.SteppingNow == 1, "released before StepBackSeconds");
                logic.TickStepBacks(started + 1.5);
                Check(logic.SteppingNow == 0 && body.Released.Count == 1 && stats.Ended(StepBackEnd.TimeUp) == 1 && stats.Releases == 1,
                    "not released when StepBackSeconds ran out");
                LogHas("[stepback] first step back ended (time up) after 1.5 s: now at (10.0, 8.8, 0.0), moved 1.20 m (0.80 m from the spot), facing his enemy (14° off, 2.8 m from him); mid-step facing his enemy (26° off), moving away (0.80 m/s away); hits taken 0 (blocked 0), swings 1; released: scripted movement off");

                // --- live time: a shorter StepBackSeconds applies to a running step back
                var b = EmptyRecruit(101, ref t);
                logic.TickStepBacks(t);
                Check(logic.SteppingNow == 1, "second recruit not started");
                S.Set(SettingsSchema.StepBackSeconds, 0.5, SettingSources.Mcm);
                logic.TickStepBacks(t + 0.6);
                Check(logic.SteppingNow == 0, "a shorter StepBackSeconds did not apply to a running step back");
                LogHas("[stepback] settings now: ON - after a melee swing an AI fighter on foot steps back with chance 100% x (1 - f) (0 at full strength), 2.0 m straight away from his enemy for up to 0.5 s");
                S.Set(SettingsSchema.StepBackSeconds, 1.5, SettingSources.Mcm);

                // --- never rolled: a rider; not a field battle
                _setMount ??= FieldSetter<Agent?>("_cachedMountAgent");
                var rider = logic.Track(FakeAgent(102))!;
                _setMount(rider.Agent, FakeAgent(900));
                for (int i = 0; i < 5; i++) Swing(rider, ref t);
                Check(stats.NotRolled(StepBackNotRolled.Mounted) == 5 && rider.StepBack == null, "a rider rolled for a step back");
                _setMount(rider.Agent, null);
                body.Allow = false;
                var arena = EmptyRecruit(103, ref t);
                Check(stats.NotRolled(StepBackNotRolled.MissionKind) == 5 && arena.StepBack == null, "a swing in a non-battle mission was rolled");
                body.Allow = true;

                // --- refused by the game's checks; the engine not taking it; the cap
                int rolls = stats.DiceYes;
                body.NextRefusal = StepBackRefusal.HoldingArrangement;
                EmptyRecruit(104, ref t);
                logic.TickStepBacks(t);
                Check(stats.Refused(StepBackRefusal.HoldingArrangement) == 1 && logic.SteppingNow == 0, "a shield-wall refusal started anyway");
                body.Take = false;
                EmptyRecruit(105, ref t);
                logic.TickStepBacks(t);
                Check(stats.Refused(StepBackRefusal.EngineIgnored) == 1 && logic.SteppingNow == 0, "a step back the engine did not take is still tracked");
                body.Take = true;
                S.Set(SettingsSchema.StepBackMaxAtOnce, 2, SettingSources.Mcm);
                var c1 = EmptyRecruit(106, ref t);
                var c2 = EmptyRecruit(107, ref t);
                var c3 = EmptyRecruit(108, ref t);
                var c4 = EmptyRecruit(109, ref t);
                logic.TickStepBacks(t);
                Check(logic.SteppingNow == 2 && stats.Refused(StepBackRefusal.AtOnceCap) == 2 && stats.PeakAtOnce == 2, "StepBackMaxAtOnce not held: "
                    + logic.SteppingNow + " stepping, cap refusals " + stats.Refused(StepBackRefusal.AtOnceCap));
                Check(stats.DiceYes - rolls == 6, "every empty swing should have been a yes: " + (stats.DiceYes - rolls));
                S.Set(SettingsSchema.StepBackMaxAtOnce, 50, SettingSources.Mcm);

                // --- cut short: order change (released), handed over (NOT released), left the field (no engine call)
                int releasedBefore = body.Released.Count;
                body.EndFor[106] = StepBackEnd.OrderChanged;
                body.EndFor[107] = StepBackEnd.HandedOver;
                logic.TickStepBacks(t + 0.1);
                Check(logic.SteppingNow == 0 && stats.Ended(StepBackEnd.OrderChanged) == 1 && stats.Ended(StepBackEnd.HandedOver) == 1, "order change / hand-over not ended");
                Check(body.Released.Count == releasedBefore + 1 && body.Released[body.Released.Count - 1] == 106, "released the handed-over man (the game's job) or not the ordered one");
                body.EndFor.Clear();
                for (int i = 0; i < 2; i++) Swing(c3, ref t); // c3 and c4 are empty: a new yes each
                Swing(c4, ref t);
                logic.TickStepBacks(t);
                Check(logic.SteppingNow == 2, "c3/c4 did not start");
                releasedBefore = body.Released.Count;
                typeof(AthleticsLogic).GetMethod("Untrack", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(logic, new object[] { c3.Agent });
                Check(logic.SteppingNow == 1 && stats.Ended(StepBackEnd.LeftField) == 1 && body.Released.Count == releasedBefore, "leaving the field did not end it, or called the engine on a removed man");

                // --- switched off mid-battle: everyone stepping back released at once; no new rolls; back on
                S.Set(SettingsSchema.StepBackEnabled, false, SettingSources.Mcm);
                logic.TickStepBacks(t + 0.2);
                Check(logic.SteppingNow == 0 && stats.Ended(StepBackEnd.SwitchedOff) == 1 && stats.SwitchedOffReleases == 1, "switching StepBackEnabled off did not release everyone");
                LogHas("[stepback] StepBackEnabled switched OFF mid-mission at ");
                LogHas(" s: 1 fighters stepping back released to their formations at once");
                int rollsOff = stats.Rolls;
                Swing(c4, ref t);
                Check(stats.Rolls == rollsOff, "a swing was rolled while the step back was off");
                S.Set(SettingsSchema.StepBackEnabled, true, SettingSources.Mcm);
                logic.TickStepBacks(t);
                LogHas("[stepback] StepBackEnabled switched ON mid-mission: tired fighters step back again from their next swing");
                Swing(c4, ref t);
                logic.TickStepBacks(t);
                Check(logic.SteppingNow == 1, "back on: no step back");

                // --- the summary: mission end releases the one still stepping back; the release check
                body.StillScriptedAfterRelease = false;
                logic.WriteAthleticsSummary();
                Check(logic.SteppingNow == 0 && stats.MidStepAtMissionEnd == 1 && stats.OverdueAtMissionEnd == 0 && stats.Ended(StepBackEnd.MissionEnd) == 1,
                    "mission end did not release the man still stepping back");
                LogHas("[summary] step back - technique: a scripted step - the engine's SetScriptedPositionAndDirection");
                LogHas("[summary] step back rolls after AI melee swings on foot, by f (the chance must be 0% at full strength and rise as f falls): peak (f 1) ");
                LogHas(" swings, chance avg 0%, dice yes 0 (0%) | f 0.5-1 ");
                LogHas("; not rolled: you 0, riders 5, not a field battle (tournament, arena, duel, naval, deployment, ending) 5");
                LogHas("[summary] step back starts: dice yes ");
                LogHas("(shield wall/square/circle 1, StepBackMaxAtOnce reached 2, engine did not take the scripted position 1)");
                LogHas("[summary] step back ends: 7 - completed (time up) 2, cut short 5 (left the field 1, mission end 1, switched off 1, formation order changed 1, handed over to the game (not disabled) 1)");
                LogHas("[summary] step back facing (THE risk: a turned back) - at the start: facing his enemy 7 (100%)");
                LogHas("[summary] step back guard: hits taken while stepping back 0");
                LogHas("swings started while stepping back 1 (0 expected: StepBackHoldAttacks is on)");
                LogHas("[summary] step back release check: 5 released through the engine - scripted movement still on right after 0 (must be 0)");
                LogHas("at mission end: 1 were mid-step (released then), overdue (past their time) 0 (must be 0), scripted movement still on after that release 0 (must be 0)");
                Check(logic.StepStats.SummaryLines(StepBackRules.From(S)).Count == 8, "the step-back summary is not 8 lines");
                Check(logic.RateStats.SteppedBack[0] > 0, "step 5e: no attack-rate cycle was left out for a step back in it (its pause is not the attack rhythm)");
                LogHas("; with a step back in them (its pause, not the attack rhythm) ");
                _ = b;
                _ = c1;
                _ = c2;
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                StepBackDefaults();
            }
        }

        /// <summary>Review 10a R5: a record whose agent left unseen - its index reused by a new agent, or
        /// the agent deleted without a removal - is forgotten like a man leaving the field: his step
        /// back and pace hold end, his slowed horse is queued for release, and nothing calls the engine
        /// on the old agent. Before, only the loop entry went and the tick kept working on him.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void StaleRecordsAreForgotten()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                StepBackDefaults();
                var logic = new AthleticsLogic();
                _logic = logic;
                SetStatic(typeof(AthleticsLogic), "_current", logic);
                typeof(AthleticsLogic).GetMethod("StartAthletics", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(logic, null);
                var body = new FakeStepBody();
                logic.StepBackBody = body;
                logic.StepBackDice = new FixedDice(0.999);
                var paceBody = new FakePaceBody();
                logic.PaceBody = paceBody;
                const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
                var paceHeld = (List<TrackedAgent>)typeof(AthleticsLogic).GetField("_paceHeld", Private)!.GetValue(logic)!;
                var horses = (List<Agent>)typeof(AthleticsLogic).GetField("_horsesToRelease", Private)!.GetValue(logic)!;
                double t = 3000;

                // an empty recruit stepping back, under a pace hold and on a slowed horse
                var old = EmptyRecruit(120, ref t);
                logic.TickStepBacks(t);
                Check(logic.SteppingNow == 1, "precondition: the recruit does not step back");
                old.Pace = new PaceState { Active = true, StartedAt = t, Until = t + 5 };
                paceHeld.Add(old);
                var horse = FakeAgent(700);
                old.SlowedMount = horse;
                typeof(AthleticsLogic).GetMethod("RegisterMount", Private)!.Invoke(logic, new object[] { horse, old });
                int stepReleases = body.Released.Count, paceReleases = paceBody.Released.Count;

                // his index comes back on a new agent before his removal was seen
                var fresh = logic.Track(FakeAgent(120))!;
                Check(!ReferenceEquals(fresh, old) && old.Removed && logic.SteppingNow == 0 && logic.StepStats.Ended(StepBackEnd.LeftField) == 1,
                    "the stale record still steps back");
                Check(!old.Pace.Active && paceHeld.Count == 0 && logic.RateStats.Ended(PaceEnd.LeftField) == 1, "the stale record is still held");
                Check(old.SlowedMount == null && horses.Count == 1 && ReferenceEquals(horses[0], horse), "the stale record's horse was not queued for release");
                Check(body.Released.Count == stepReleases && paceBody.Released.Count == paceReleases, "an engine call on the stale (gone) agent");

                // the deletion backstop: an agent deleted without a removal we saw is forgotten; again = nothing
                logic.OnAgentDeleted(fresh.Agent);
                Check(fresh.Removed, "OnAgentDeleted did not forget the agent");
                logic.OnAgentDeleted(fresh.Agent);
                logic.OnAgentDeleted(FakeAgent(121)); // never tracked
                Check(logic.StepStats.Ended(StepBackEnd.LeftField) == 1 && logic.Stats.Errors == 0, "the backstop did something twice or failed");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                StepBackDefaults();
            }
        }
    }
}

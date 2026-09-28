using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TraxCombat.Core;
using TraxCombat.Missions;
using TraxCombat.Models;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for the attack RATE (step 5e; step 13 PAUSE ONLY) - the REAL
    /// <see cref="AthleticsLogic"/>: every channel-1 action fed through its observer (phases, the
    /// attack's D, cycles by f, the targets and verdicts), the AI's no-attack timer (asked at the
    /// attack's end, started by the tick, lifted on every path) with a stand-in for the engine side
    /// (<see cref="IPaceBody"/>) - melee, ranged, riders; YOUR timer through the logic and the input
    /// gate's managed decision (the hold from the release, swallowed presses, the flash, hold-to-attack,
    /// a missed attack, the switches, the recovery bar's read); the gate's place in the behaviour list;
    /// the first-slowed log. The fake agents have no native side: what it cannot check is that the
    /// engine honours NoAttack and the cleared attack bits - PLAYTEST "Attack rate" and the [summary]
    /// attack-rate lines prove that in game.
    /// </summary>
    internal static partial class Program
    {
        private const int ActReadyMeleeCode = (int)Agent.ActionCodeType.ReadyMelee;
        private const int ActBlockedCode = (int)Agent.ActionCodeType.BlockedMelee;
        private const int ActReadyRangedCode = (int)Agent.ActionCodeType.ReadyRanged;
        private const int ActReleaseRangedCode = (int)Agent.ActionCodeType.ReleaseRanged;
        private const int ActReleaseThrowingCode = (int)Agent.ActionCodeType.ReleaseThrowing;
        private const int ActReloadCode = (int)Agent.ActionCodeType.Reload;

        /// <summary>The engine side of the AI timer, played by the smoke: answers set per check, calls recorded.</summary>
        private sealed class FakePaceBody : IPaceBody
        {
            public bool Take = true;
            public PaceRefusal? NextRefusal;
            public AiInputHook.HookResult Hook = AiInputHook.HookResult.FirstHookTurnedOn; // step 16: as the input body reports it
            public int WaitingReleases;
            public bool UnderAPlainFrame;
            public readonly List<int> Started = new List<int>();
            public readonly List<int> Released = new List<int>();
            public readonly Dictionary<int, PaceEnd> EndFor = new Dictionary<int, PaceEnd>();

            public bool Start(TrackedAgent st, PaceState ps, out PaceRefusal refusal)
            {
                refusal = PaceRefusal.EngineIgnored;
                if (NextRefusal != null)
                {
                    refusal = NextRefusal.Value;
                    NextRefusal = null;
                    return false;
                }
                if (!Take) return false;
                Started.Add(st.AgentIndex);
                ps.Hook = Hook;
                ps.FlagsBefore = 0;
                ps.FlagsAfter = (int)Agent.AIScriptedFrameFlags.NoAttack;
                return true;
            }

            public bool LastEvenUnderAFrame;

            public PaceRelease Release(TrackedAgent st, PaceState ps, bool evenUnderAFrame)
            {
                LastEvenUnderAFrame = evenUnderAFrame;
                if (WaitingReleases > 0 && !(evenUnderAFrame && UnderAPlainFrame))
                {
                    WaitingReleases--;
                    return PaceRelease.Waiting;
                }
                Released.Add(st.AgentIndex);
                ps.FlagsAfter = 0;
                return PaceRelease.ClearedByUs;
            }

            public bool MustEnd(TrackedAgent st, out PaceEnd why)
            {
                if (EndFor.TryGetValue(st.AgentIndex, out why))
                {
                    EndFor.Remove(st.AgentIndex);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// One melee attack as the engine plays it since step 13 - the animations at FULL speed: a ready of
        /// <paramref name="ready"/> (80% wind-up, then held - D takes the wind-up only), a swing of
        /// <paramref name="swing"/>, an optional block recoil, then the tick; the AI's own pause is
        /// <paramref name="pause"/> - but an AI timer keeps it from the next ready until the tick lifts it
        /// (the AI obeying NoAttack, readying at once). D = 0.8 × ready + swing (0.82 s by default).
        /// </summary>
        private static void Attack(AthleticsLogic logic, TrackedAgent st, ref double t, double ready = 0.4, double swing = 0.5, double pause = 0.4, bool blocked = false)
        {
            var r = Rules;
            logic.ObserveAction(st, ActReadyMeleeCode, t, in r);
            AthleticsLogic.ReadyFull(st, t + 0.8 * ready);
            t += ready;
            logic.ObserveAction(st, ActRelease, t, in r);
            st.SpeedDirty = false; // as if the tick loop had applied the new multiplier
            t += swing;
            if (blocked)
            {
                logic.ObserveAction(st, ActBlockedCode, t, in r);
                t += 0.3;
            }
            logic.ObserveAction(st, ActIdle, t, in r);
            logic.TickPace(t);                         // the tick right after the attack's end
            var ps = st.Pace;
            t = ps != null && ps.Active && ps.Until > t + pause ? ps.Until : t + pause;
            logic.TickPace(t);                         // the tick before his next ready: an expired timer ends
        }

        /// <summary>One shot: draw (full at <paramref name="draw"/> × 0.8), loose 0.13 s, the reload that
        /// follows (or none: a throw), then the tick. D = the draw's wind-up + the loose + the reload.</summary>
        private static void Shot(AthleticsLogic logic, TrackedAgent st, ref double t, double draw = 1.25, double reload = 1.2, bool thrown = false)
        {
            var r = Rules;
            logic.ObserveAction(st, ActReadyRangedCode, t, in r);
            AthleticsLogic.ReadyFull(st, t + 0.8 * draw);
            t += draw;
            logic.ObserveAction(st, thrown ? ActReleaseThrowingCode : ActReleaseRangedCode, t, in r);
            t += 0.13;
            if (reload > 0)
            {
                logic.ObserveAction(st, ActReloadCode, t, in r);
                t += reload;
            }
            logic.ObserveAction(st, ActIdle, t, in r);
            logic.TickPace(t);
        }

        private static AthleticsLogic NewRateLogic(FakePaceBody body)
        {
            var logic = new AthleticsLogic();
            _logic = logic;
            SetStatic(typeof(AthleticsLogic), "_current", logic);
            typeof(AthleticsLogic).GetMethod("StartAthletics", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(logic, null);
            logic.PaceBody = body;
            logic.StepBackDice = new FixedDice(0.999);
            logic.StepBackBody = new FakeStepBody { Allow = false }; // no step backs in these checks
            logic.StepBackInputBody = new FakeStepBody { Allow = false, Input = true };
            logic.PaceInputBody = new FakePaceBody { Take = false };   // step 16: the old technique here (AthleticsDefaults) - never used
            return logic;
        }

        /// <summary>A 300-skill fighter: 7 swings end at full strength, empty after 30.</summary>
        private static TrackedAgent Veteran(AthleticsLogic logic, int index)
        {
            var st = logic.Track(FakeAgent(index))!;
            st.AthleticsSkill = 300;
            return st;
        }

        /// <summary>A fighter tired by a wound: his bar capped at <paramref name="health"/>% (f = health ÷ 75).</summary>
        private static TrackedAgent Wounded(AthleticsLogic logic, int index, float health, double t)
        {
            var st = logic.Track(FakeAgent(index))!;
            SetHealth(st.Agent, health, 100f);
            logic.CheckHealth(st, Rules, t);
            st.SpeedDirty = false;
            return st;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AttackRateThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                S.Set(SettingsSchema.StepBackEnabled, false, SettingSources.File);
                var body = new FakePaceBody();
                var b = NewRateLogic(body);
                var sb = b.RateStats;
                LogHas("[rate] mission start: ON - PAUSE ONLY: animations at full speed (AttackAnimationMinPercent 100); after each attack no new attack for D x (1/m - 1) (D = its wind-up + release, ranged + its reload): you (AttackRatePlayerTimer) on");
                LogHas("AI (AttackRatePaceHold) on - NoAttack (AttackRatePaceByInput off: the engine's no-attack flag, step 13's technique), melee and ranged, on foot and mounted; AI decisions (AttackRateAiDecisions) off; never held: blocking, parrying, moving, weapon switches, kicks - read live");

                // ---- A: an AI melee fighter, full-speed animations (D 0.82 s): no timer at full strength,
                // none below 0.1 s, then D x (1/m - 1) after every attack, the gap exactly the timer
                var y = Veteran(b, 211);
                double t = 2000;
                for (int i = 0; i < 10; i++) Attack(b, y, ref t);
                Check(sb.NotHeld(PaceNotHeld.FullStrength) == 7 && sb.NotHeld(PaceNotHeld.NotNeeded) == 3 && sb.Holds == 0,
                    "A: full strength " + sb.NotHeld(PaceNotHeld.FullStrength) + " (7), below 0.1 s " + sb.NotHeld(PaceNotHeld.NotNeeded) + " (3), holds " + sb.Holds);
                Attack(b, y, ref t); // the 11th: m 0.876 → 0.82 x 0.142 = 0.117 s - the first timer
                Check(sb.Holds == 1 && body.Started.Count == 1 && body.Released.Count == 1 && sb.Ended(PaceEnd.TimeUp) == 1,
                    "A: the first worthwhile timer was not held and lifted: holds " + sb.Holds + ", started " + body.Started.Count + ", released " + body.Released.Count);
                LogHas("[rate] first AI timer this mission: (agent 211) at ");
                LogHas(" - his melee attack (D 0.82 s: wind-up + swing) ended at ");
                LogHas(" at attack speed x0.88 (f 0.84) → no new attack for 0.12 s = D x (1/m - 1), until ");
                LogHas("; scripted flags 0 → 2 (NoAttack set: the engine took it)");
                LogHas("[rate] first AI timer ended at ");
                LogHas(" - time up; scripted flags now 0; melee hits taken while held 0 (blocked 0); the gap to his next attack is in the summary (\"timer:\" rows)");
                for (int i = 0; i < 29; i++) Attack(b, y, ref t);
                Check(y.Exhausted && sb.Holds == 30 && sb.Released(PaceRelease.ClearedByUs) == 30, "A: holds " + sb.Holds + " (30), cleared " + sb.Released(PaceRelease.ClearedByUs));
                Check(Near(sb.TimerAskedMean(AttackKind.Melee, false, 3), 3.28) && Near(sb.GapMean(AttackKind.Melee, false, 3), 3.28) && sb.StartedEarly(AttackKind.Melee, false) == 0,
                    "A: empty timers asked " + sb.TimerAskedMean(AttackKind.Melee, false, 3) + " (3.28 = 0.82 x 4), gap " + sb.GapMean(AttackKind.Melee, false, 3) + ", early " + sb.StartedEarly(AttackKind.Melee, false));
                // full-speed animations: the empty swing is the fresh swing; the cycle = 0.9 + 3.28 (the AI's
                // own 0.4 s pause runs inside the timer) - the verdict against fresh ÷ m says so honestly
                Check(Near(sb.PhaseMean(AttackKind.Melee, false, 3, AttackPhase.Release), 0.5) && Near(sb.PhaseMean(AttackKind.Melee, false, 3, AttackPhase.WindUp), 0.32)
                      && Near(sb.CycleMean(AttackKind.Melee, false, 3), 4.18) && Near(sb.AnimationMean(AttackKind.Melee, false, 3), 1.0) && Near(sb.FreshCycle(AttackKind.Melee, false), 1.3),
                    "A: empty swing " + sb.PhaseMean(AttackKind.Melee, false, 3, AttackPhase.Release) + ", wind-up " + sb.PhaseMean(AttackKind.Melee, false, 3, AttackPhase.WindUp)
                    + ", cycle " + sb.CycleMean(AttackKind.Melee, false, 3) + " (4.18), animations x" + sb.AnimationMean(AttackKind.Melee, false, 3) + ", fresh " + sb.FreshCycle(AttackKind.Melee, false));
                var linesA = sb.SummaryLines(AttackRateRules.From(S), false);
                Check(linesA.Exists(l => l.StartsWith("attack rate, melee, AI, empty (f 0): animations asked x1.00 - wind-up 0.32 (x1.00) + held 0.08 (x1.00), swing 0.50 (x1.00)", StringComparison.Ordinal)
                                         && l.EndsWith("m 0.20 → target 6.50 s: 64% - too fast", StringComparison.Ordinal)),
                    "A: the empty band row: " + string.Join(" // ", linesA));
                Check(linesA.Exists(l => l.StartsWith("attack rate, melee, AI, empty (f 0) - timer: 1", StringComparison.Ordinal)
                                         && l.Contains("(D avg 0.82 s at m 0.20 → asked avg 3.28 s = D x (1/m - 1)); measured: the next attack began avg 3.28 s after the attack's end")
                                         && l.Contains(" 0.00 s after the timer ended; started before the timer ended: 0 (must be 0); the timer's floor D/m avg 4.10 s - the cycle vs it: 102% (n ")),
                    "A: the empty band's timer row: " + string.Join(" // ", linesA));

                // ---- B: ranged - the timer after the RELOAD, D = draw + loose + reload; a throw with none
                var x = Wounded(b, 230, 40f, t); // f 0.53, m 0.63 (no charge offline: the shot event needs a mission)
                Shot(b, x, ref t);
                Check(x.Pace != null && x.Pace.Active && x.Pace.Kind == AttackKind.Ranged && Near(x.Pace.Duration, 2.33) && Near(x.Pace.Pause, 2.33 * (1 / 0.62666667 - 1)),
                    "B: the archer's timer: " + (x.Pace == null ? "none" : x.Pace.Kind + ", D " + x.Pace.Duration + ", pause " + x.Pace.Pause));
                Check(Near(x.Pace!.Until - t, x.Pace.Pause), "B: the ranged timer does not run from the reload's end");
                t = x.Pace.Until;
                b.TickPace(t);
                Shot(b, x, ref t, draw: 0.9, reload: 0, thrown: true);
                Check(x.Pace.Active && Near(x.Pace.Duration, 0.72 + 0.13), "B: a throw's D (wind-up + release, no reload): " + x.Pace.Duration);
                Check(sb.HoldsOf(AttackKind.Ranged) == 2, "B: ranged holds " + sb.HoldsOf(AttackKind.Ranged));
                t = x.Pace.Until;
                b.TickPace(t);

                // ---- C: riders are held too (step 13 - no animation slow-down would leave them at the full rate)
                var rider = Wounded(b, 231, 40f, t);
                _setMount ??= FieldSetter<Agent?>("_cachedMountAgent");
                _setMount(rider.Agent, FakeAgent(901));
                Attack(b, rider, ref t, pause: 0);
                Check(sb.HoldsMounted == 1 && body.Started.Contains(231), "C: a rider was not held");
                _setMount(rider.Agent, null);

                // ---- D: every path of the AI timer
                var r = Rules;
                // chained: the next ready straight out of the swing - never held
                int chained = sb.NotHeld(PaceNotHeld.AlreadyReadied);
                b.ObserveAction(y, ActReadyMeleeCode, t, in r);
                AthleticsLogic.ReadyFull(y, t + 0.3);
                t += 0.4;
                b.ObserveAction(y, ActRelease, t, in r);
                t += 0.5;
                b.ObserveAction(y, ActReadyMeleeCode, t, in r); // chained
                b.TickPace(t);
                Check(sb.NotHeld(PaceNotHeld.AlreadyReadied) == chained + 1 && !y.Pace!.Active, "D: a chained blow was held");
                AthleticsLogic.ReadyFull(y, t + 0.3);
                t += 0.4;
                b.ObserveAction(y, ActRelease, t, in r);
                t += 0.5;
                b.ObserveAction(y, ActIdle, t, in r);

                // an attack slips through NoAttack: counted, the hold lifted by the next tick, the gap early
                b.TickPace(t);
                Check(y.Pace!.Active, "D: no hold after the swing");
                int earlyBefore = sb.StartedEarly(AttackKind.Melee, false);
                t += 0.2;
                b.ObserveAction(y, ActReadyMeleeCode, t, in r);
                b.TickPace(t);
                Check(!y.Pace.Active && sb.Ended(PaceEnd.AttackStarted) == 1 && sb.StartedEarly(AttackKind.Melee, false) == earlyBefore + 1,
                    "D: an attack while held did not end the hold / was not counted early");
                AthleticsLogic.ReadyFull(y, t + 0.3);
                t += 0.4;
                b.ObserveAction(y, ActRelease, t, in r);
                t += 0.5;
                b.ObserveAction(y, ActIdle, t, in r);

                // a game job on him when the hold ends: our NoAttack stays until he is free (never under the job)
                b.TickPace(t);
                Check(y.Pace.Active, "D: no hold (game job check)");
                body.WaitingReleases = 2;
                double until = y.Pace.Until;
                b.TickPace(until);
                Check(!y.Pace.Active && y.Pace.Waiting && sb.Released(PaceRelease.Waiting) == 1, "D: the hold did not wait for the game job");
                b.TickPace(until + 0.1);                     // not yet re-checked (every 0.25 s)
                b.TickPace(until + 0.3);                     // re-checked: still busy
                Check(y.Pace.Waiting, "D: stopped waiting while the job ran");
                b.TickPace(until + 0.6);                     // free: cleared
                Check(!y.Pace.Waiting && sb.ClearedAfterWaiting == 1 && !body.LastEvenUnderAFrame, "D: NoAttack not cleared once the game job ended");
                t = until + 0.6;

                // a long plain scripted frame (a job that may want him to fight): lifted after 3 s anyway
                Attack(b, y, ref t, pause: 0);
                AttackUntilHeld(b, y, ref t);
                body.WaitingReleases = 1000;
                body.UnderAPlainFrame = true;
                until = y.Pace.Until;
                b.TickPace(until);                           // a frame on him: wait
                for (double wait = 0.25; wait < 2.9; wait += 0.25) b.TickPace(until + wait);
                Check(y.Pace.Waiting && sb.ClearedUnderAFrame == 0, "D: lifted under a frame before 3 s");
                b.TickPace(until + 3.05);
                Check(!y.Pace.Waiting && sb.ClearedUnderAFrame == 1 && body.LastEvenUnderAFrame, "D: our NoAttack outlived a 3 s scripted frame");
                body.WaitingReleases = 0;
                body.UnderAPlainFrame = false;
                t = until + 3.05;

                // the player takes him: ended at once
                AttackUntilHeld(b, y, ref t);
                body.EndFor[211] = PaceEnd.PlayerControl;
                b.TickPace(t + 0.05);
                Check(!y.Pace.Active && sb.Ended(PaceEnd.PlayerControl) == 1, "D: the player taking him did not end the hold");
                t += 0.05;

                // refused by the engine side: the game already had NoAttack on him; the flag did not stick
                body.NextRefusal = PaceRefusal.AlreadyNoAttack;
                Attack(b, y, ref t);
                Check(sb.Refused(PaceRefusal.AlreadyNoAttack) == 1, "D: AlreadyNoAttack not counted");
                body.Take = false;
                Attack(b, y, ref t);
                Check(sb.Refused(PaceRefusal.EngineIgnored) == 1 && !y.Pace.Active, "D: a hold the engine did not take is tracked");
                body.Take = true;

                // left the field while held: no engine call
                var z = Wounded(b, 212, 30f, t);
                AttackUntilHeld(b, z, ref t);
                Check(z.Pace != null && z.Pace.Active, "D: z not held");
                int releases = body.Released.Count;
                typeof(AthleticsLogic).GetMethod("Untrack", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(b, new object[] { z.Agent });
                Check(!z.Pace!.Active && sb.Ended(PaceEnd.LeftField) == 1 && body.Released.Count == releases, "D: leaving the field did not end the hold or called the engine");

                // switched off mid-hold: every hold lifted at once, logged; back on
                AttackUntilHeld(b, y, ref t);
                Check(y.Pace.Active && b.HeldNow == 1, "D: y not held before the switch-off");
                S.Set(SettingsSchema.AttackRatePaceHold, false, SettingSources.Mcm);
                b.ApplySettingsChange(S);
                b.TickPace(t + 0.05);
                Check(b.HeldNow == 0 && sb.Ended(PaceEnd.SwitchedOff) == 1, "D: AttackRatePaceHold off did not lift every hold");
                LogHas("[rate] AttackRatePaceHold switched OFF mid-mission at ");
                LogHas(" s: 1 held fighters may attack again at once");
                int holds = sb.Holds;
                t += 0.05;
                Attack(b, y, ref t);
                Check(sb.Holds == holds, "D: held while switched off");
                S.Set(SettingsSchema.AttackRatePaceHold, true, SettingSources.Mcm);
                b.ApplySettingsChange(S);
                b.TickPace(t);
                LogHas("[rate] the AI timer is ON again at ");

                // the AI-decision switch and the animation floor: every tired fighter recomputed, logged
                y.SpeedDirty = false;
                S.Set(SettingsSchema.AttackRateAiDecisions, true, SettingSources.Mcm);
                b.ApplySettingsChange(S);
                Check(y.SpeedDirty, "D: AttackRateAiDecisions on did not ask a recompute of a tired fighter");
                LogHas("[rate] AttackRateAiDecisions switched ON mid-mission at ");
                LogHas(" tired fighters get their AI attack values scaled by their attack speed over the next ticks (at most 50 recomputes a tick)");
                S.Set(SettingsSchema.AttackRateAiDecisions, false, SettingSources.Mcm);
                b.ApplySettingsChange(S);
                y.SpeedDirty = false;
                S.Set(SettingsSchema.AttackAnimationMinPercent, 60, SettingSources.Mcm);
                b.ApplySettingsChange(S);
                Check(y.SpeedDirty, "D: AttackAnimationMinPercent did not ask a recompute of a tired fighter");
                LogHas("[rate] AttackAnimationMinPercent now 60 at ");
                LogHas(" tired fighters get their attack animations x (0.60 + 0.40 f): full speed at the peak line, x0.80 halfway, x0.60 empty over the next ticks (at most 50 recomputes a tick) - a slower swing on top of the timer; the pause in seconds unchanged");
                S.Set(SettingsSchema.AttackAnimationMinPercent, 100, SettingSources.Mcm);
                b.ApplySettingsChange(S);
                y.SpeedDirty = false;

                // review 10a R1: a tired swing whose step back is REFUSED (the cap, a shield wall, no enemy
                // near) is still held. Step 16: one whose step back STARTS is held too - the timer survives the step
                // back; this NoAttack hold waits behind the scripted walk and is set the tick it ends
                var stepBody = new FakeStepBody();
                b.StepBackBody = stepBody;
                S.Set(SettingsSchema.StepBackEnabled, true, SettingSources.File);
                Check(y.Exhausted, "R1: precondition - y is not empty (his swing would not ask for a step back)");
                int holdsR1 = sb.Holds;
                stepBody.NextRefusal = StepBackRefusal.AtOnceCap;
                SwingOnce(b, y, ref t);
                Check(y.StepBack != null && y.StepBack.Pending, "R1: the empty swing did not ask for a step back");
                b.TickStepBacks(t);                          // the tick: step backs first...
                b.TickPace(t);                               // ...then the holds
                Check(b.StepStats.Refused(StepBackRefusal.AtOnceCap) == 1 && b.SteppingNow == 0 && y.Pace.Active && sb.Holds == holdsR1 + 1,
                    "R1: a refused step back left the swing unheld - holds " + (sb.Holds - holdsR1) + ", held now " + y.Pace.Active);
                t = y.Pace.Until;
                b.TickPace(t);                               // time up
                SwingOnce(b, y, ref t);
                b.TickStepBacks(t);                          // this time it starts
                b.TickPace(t);
                Check(b.SteppingNow == 1 && !y.Pace.Active && y.Pace.Deferred && b.DeferredNow == 1 && sb.Holds == holdsR1 + 1 && b.HoldStats.Deferred == 1,
                    "R1 / step 16: the hold was not deferred behind the scripted step back (never dropped, never over its frame)");
                double untilR1 = y.Pace.Until;
                t += 1.6;
                b.TickStepBacks(t);                          // its time is up: released...
                b.TickPace(t);                               // ...and the deferred NoAttack set in the same tick
                Check(b.SteppingNow == 0 && y.Pace.Active && !y.Pace.Deferred && b.DeferredNow == 0 && sb.Holds == holdsR1 + 2
                      && b.HoldStats.DeferredStarted == 1 && Near(y.Pace.Until, untilR1),
                    "R1 / step 16: the deferred hold was not set when the step back ended (the timer must survive the step back)");
                t = y.Pace.Until;
                b.TickPace(t);                               // the rest of the pause served
                Check(!y.Pace.Active && b.HoldStats.HoldsOverlappingAStep == 1, "step 16: the deferred hold did not end / was not counted as overlapping");
                // a short pause fully covered by the step back: never set, counted
                var mild = Wounded(b, 213, 70f, t);          // f 0.93: a pause of a few tenths
                stepBody.NextRefusal = StepBackRefusal.None;
                S.Set(SettingsSchema.StepBackMaxChancePercent, 100, SettingSources.Mcm);
                b.StepBackDice = new FixedDice(0.0);         // every roll says yes
                SwingOnce(b, mild, ref t);
                b.TickStepBacks(t);
                b.TickPace(t);
                Check(b.SteppingNow == 1 && mild.Pace != null && mild.Pace.Deferred, "step 16: the mild man's hold was not deferred");
                t += 1.6;
                b.TickStepBacks(t);
                b.TickPace(t);
                Check(!mild.Pace!.Active && !mild.Pace.Deferred && b.HoldStats.DeferredCovered == 1 && sb.NotHeld(PaceNotHeld.CoveredByStepBack) == 1,
                    "step 16: a pause the step back covered was set anyway or not counted");
                b.StepBackDice = new FixedDice(0.999);
                S.Set(SettingsSchema.StepBackEnabled, false, SettingSources.File);
                b.StepBackBody = new FakeStepBody { Allow = false };

                // review 10a R2: a new hold while the last one still waits for a game job - listed once
                var heldList = (List<TrackedAgent>)typeof(AthleticsLogic).GetField("_paceHeld", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(b)!;
                AttackUntilHeld(b, y, ref t);
                body.WaitingReleases = 2;
                t = y.Pace.Until;
                b.TickPace(t);                               // a game job on him: waiting
                Check(y.Pace.Waiting && heldList.Count == 1, "R2: not waiting");
                SwingOnce(b, y, ref t);                      // (the stand-in lets him swing - in game the job cleared NoAttack)
                b.TickPace(t);                               // the waiting pass (still busy), then the new hold starts
                Check(y.Pace.Active && heldList.Count == 1, "R2: the new hold listed him twice: " + heldList.Count);
                int endsR2 = sb.Ended(PaceEnd.TimeUp);
                t = y.Pace.Until;
                b.TickPace(t);
                Check(!y.Pace.Active && heldList.Count == 0 && sb.Ended(PaceEnd.TimeUp) == endsR2 + 1, "R2: the hold ended twice or stayed listed: ends "
                    + (sb.Ended(PaceEnd.TimeUp) - endsR2) + ", listed " + heldList.Count);
                body.WaitingReleases = 0;

                // ---- E: YOUR timer through the real logic (the input gate's decision driven with made-up presses)
                t += 10;
                PlayerTimerThroughTheLogic(b, ref t);

                // mission end: a running AI hold and your running countdown are released before the summary
                // (the master switch in E refilled everyone: a freshly wounded AI fighter is the one held)
                var w = Wounded(b, 240, 20f, t);
                AttackUntilHeld(b, w, ref t);
                Check(b.HeldNow == 1, "end: w not held before the mission end");
                b.WriteAthleticsSummary();
                Check(b.HeldNow == 0 && sb.HeldAtMissionEnd == 1 && sb.Ended(PaceEnd.MissionEnd) == 1, "end: mission end did not lift the hold");
                Check(!b.PlayerTimer.Holding && sb.PlayerHeldAtMissionEnd == 1, "end: your countdown was not released at the mission end");
                LogHas(" - an attack-rate switch CHANGED during this battle: the rows below mix both settings");
                LogHas("[summary] attack rate, melee, AI, peak (f 1): animations asked x1.00 - wind-up 0.32 + held 0.08, swing 0.50 (clean, hit nothing 0.50), recoil after a block -, pause 0.40 | cycle 1.30 s (n 7), m 1.00 - the fresh reference");
                LogHas("[summary] attack rate, melee, AI - verdict: ");
                LogHas("[summary] attack rate, ranged, AI, f 0.5-1 - timer: 2 (D avg ");
                LogHas("[summary] attack rate, melee, you, f below 0.5 - timer: ");
                LogHas("[summary] attack rate, ranged, you, f 0.5-1 - timer: ");
                LogHas("[summary] attack rate - your timer (AttackRatePlayerTimer on at the end): ");
                LogHas("the button held through the end 1x, your attack began avg 0.01 s after (n 1) - near 0 = hold-to-attack works; attacks that started while held anyway: 1 (must be 0 - the input gate missed them)");
                LogHas("ended early: switched off 2, not you any more 1, mission end 1 (still running at the end, released: 1)");
                LogHas("[summary] attack rate - AI timer (AttackRatePaceHold on at the end; technique at the end: NoAttack (AttackRatePaceByInput off: the engine's no-attack flag, step 13's technique); after each attack of a tired AI fighter, melee and ranged, on foot and mounted - step 21: fresh ones too when their class has a battle-pace share): ");
                LogHas("[summary] attack rate - AI timer, not held: at full strength 7, not needed (below 0.1 s) 3, the next attack already readied at the attack's end 1,");
                LogHas("[summary] attack rate - AI timer ends: time up ");
                LogHas(", an attack started anyway 1 (must be about 0 - the hold stops attacks), switched off ");
                LogHas(", left the field 1, mission end 1, you took him 1, error 0");
                // (3 waited: the game job, the long frame and R2's - R2's was followed by a new hold, not cleared)
                LogHas("a game job on him at the end (left alone, cleared once free: 2, of them under a long scripted frame: 1) 3, still held at mission end 1");
                LogHas("[summary] attack rate - guard by f (melee hits on fighters on foot that were blocked or parried; blocking is never slowed - tired men must not block less): ");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
                StepBackDefaults();
            }
        }

        /// <summary>
        /// Step 20b: a slower swing never lengthens the pause. With AttackAnimationMinPercent 85 a wounded AI man
        /// (f 0.4, m 0.52) plays his wind-up and swing at x0.91 - longer on the clock by 1/0.91 - and his timer is
        /// still the FULL-SPEED D (0.82 s) x (1/m - 1): D is built from each phase's played seconds x the animation
        /// multiplier it played at. The gap it leaves = the timer; the log's first-timer line and the summary's timer
        /// row name the played attack; the mission-start sentence names the line.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SlowerSwingKeepsThePauseInSeconds()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                S.Set(SettingsSchema.StepBackEnabled, false, SettingSources.File);
                S.Set(SettingsSchema.AttackAnimationMinPercent, 85, SettingSources.File);
                var body = new FakePaceBody();
                var b = NewRateLogic(body);
                var sb = b.RateStats;
                LogHas("[rate] mission start: ON - PAUSE ONLY: animations x (0.85 + 0.15 f): full speed at the peak line, x0.93 halfway, x0.85 empty (AttackAnimationMinPercent 85); "
                       + "after each attack no new attack for D x (1/m - 1) (D = its wind-up + release, ranged + its reload, at full animation speed - the pause in seconds does not grow with the slower swing)");

                double t = 5000;
                var w = Wounded(b, 250, 30f, t);   // f 0.4 → m 0.52, animations x0.91
                float anim = AttackTimerMath.AnimationForAttack(w.SpeedMultiplier, Rules.AttackSpeedFloor, S.AttackAnimationMinPercent);
                Check(Near(w.SpeedMultiplier, 0.52f) && Near(anim, 0.91f), "20b: the wounded man's m " + w.SpeedMultiplier + " (0.52), animations x" + anim + " (0.91)");
                // the engine plays his 0.4 s ready and 0.5 s swing at x0.91: 1/0.91 longer on the clock
                Attack(b, w, ref t, ready: 0.4 / anim, swing: 0.5 / anim);
                var ps = w.Pace;
                Check(ps != null && sb.Holds == 1, "20b: no AI timer after the slower swing");
                float mEnd = ps!.Asked;
                double fullSpeedPause = AttackTimerMath.Pause(0.82, mEnd);   // what a full-speed attack at that m would leave
                Check(Near(ps.Duration, 0.82) && Near(ps.Played, 0.82 / anim) && Near(ps.Pause, fullSpeedPause),
                    "20b: D " + ps.Duration + " (0.82 at full speed), played " + ps.Played + " (" + (0.82 / anim) + "), pause " + ps.Pause + " (" + fullSpeedPause + ", as with full-speed animations)");
                Check(Near(sb.AnimationMean(AttackKind.Melee, false, 2), 0.91) && Near(sb.PhaseMean(AttackKind.Melee, false, 2, AttackPhase.Release), 0.5 / anim),
                    "20b: the band's animations x" + sb.AnimationMean(AttackKind.Melee, false, 2) + " (0.91), swing " + sb.PhaseMean(AttackKind.Melee, false, 2, AttackPhase.Release) + " (as played)");
                LogHas(" - his melee attack (D 0.82 s: wind-up + swing at full animation speed; played 0.90 s - the slower swing) ended at ");
                LogHas(" → no new attack for " + fullSpeedPause.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s = D x (1/m - 1), until ");
                var lines = sb.SummaryLines(AttackRateRules.From(S), false);
                Check(lines.Exists(l => l.StartsWith("attack rate, melee, AI, f below 0.5 - timer: 1 (D avg 0.82 s at full animation speed - played 0.90 s, the slower swing at m ", StringComparison.Ordinal)),
                    "20b: the timer row: " + string.Join(" // ", lines));
                Check(lines.Exists(l => l.StartsWith("attack rate, melee, AI, f below 0.5: animations asked x0.91 - wind-up 0.35", StringComparison.Ordinal)),
                    "20b: the band row: " + string.Join(" // ", lines));

                // back at 100: the next attack plays at full speed, D = played (step 13's behaviour exactly)
                S.Set(SettingsSchema.AttackAnimationMinPercent, 100, SettingSources.Mcm);
                b.ApplySettingsChange(S);
                Check(w.SpeedDirty, "20b: AttackAnimationMinPercent 100 did not ask a recompute of the tired man");
                w.SpeedDirty = false;
                Attack(b, w, ref t);
                // the first timer's gap was measured at this attack's start: exactly the full-speed pause
                Check(Near(sb.GapMean(AttackKind.Melee, false, 2), fullSpeedPause) && sb.GapCount(AttackKind.Melee, false, 2) == 1 && sb.StartedEarly(AttackKind.Melee, false) == 0,
                    "20b: the gap it left " + sb.GapMean(AttackKind.Melee, false, 2) + " (the timer " + fullSpeedPause + "), early " + sb.StartedEarly(AttackKind.Melee, false));
                Check(Near(w.Pace!.Duration, 0.82) && Near(w.Pace.Played, 0.82) && Near(w.Pace.Pause, AttackTimerMath.Pause(0.82, w.Pace.Asked)),
                    "20b: at 100 D " + w.Pace.Duration + ", played " + w.Pace.Played + ", pause " + w.Pace.Pause);
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
                StepBackDefaults();
            }
        }

        /// <summary>A swing (ready, release, idle) with no tick after it.</summary>
        private static void SwingOnce(AthleticsLogic logic, TrackedAgent st, ref double t)
        {
            var r = Rules;
            logic.ObserveAction(st, ActReadyMeleeCode, t, in r);
            AthleticsLogic.ReadyFull(st, t + 0.32);
            t += 0.4;
            logic.ObserveAction(st, ActRelease, t, in r);
            st.SpeedDirty = false;
            t += 0.5;
            logic.ObserveAction(st, ActIdle, t, in r);
        }

        /// <summary>A swing and the tick after it: a tired AI fighter is held from there.</summary>
        private static void AttackUntilHeld(AthleticsLogic logic, TrackedAgent st, ref double t)
        {
            SwingOnce(logic, st, ref t);
            logic.TickPace(t);
        }

        /// <summary>
        /// Step 13, YOUR timer (the fake agent stands in for Mission.MainAgent): the hold from the
        /// release's start, a press during the swing swallowed (the flash), the countdown D x (1/m - 1),
        /// a press in it swallowed, the button held through the end - the next ready at once
        /// (hold-to-attack), the recovery bar's read at each moment, an attack the gate missed, the
        /// switch and the master switch releasing it, a ranged countdown from the reload's end.
        /// </summary>
        private static void PlayerTimerThroughTheLogic(AthleticsLogic b, ref double t)
        {
            var sb = b.RateStats;
            var timer = b.PlayerTimer;
            var me = Wounded(b, 300, 40f, t);   // capped at 40% of his bar: f 0.53 before a blow
            me.AthleticsSkill = 300;            // a 300-point bar: a blow costs 1/30 of it
            b.SmokePlayer = me.Agent;
            var r = Rules;

            // a swing: at its release the charge takes f to 0.49 (m 0.59); the expected pause (wind-up 0.32 +
            // no rest known yet) x (1/m - 1) = 0.22 s ≥ 0.1 → the hold begins at the release's start
            b.ObserveAction(me, ActReadyMeleeCode, t, in r);
            AthleticsLogic.ReadyFull(me, t + 0.32);
            Check(!timer.Holding, "E: held during the ready (clearing the bits there would release the blow)");
            t += 0.4;
            double release = t;
            b.ObserveAction(me, ActRelease, t, in r);
            Check(timer.InAttack && sb.PlayerEarlyHolds == 1, "E: the hold did not begin at the release's start");
            Check(AthleticsLogic.TryGetPlayerRecovery(me.Agent, t, out var during) && during.Share == 0f && during.Recovering && during.SecondsText.Length == 0,
                "E: the recovery bar is not empty during the attack: " + during.Share);
            b.GateFrame(me, t + 0.01, false);                             // the baseline frame
            var f1 = b.GateFrame(me, t + 0.2, true);                      // a click during his own swing
            Check(f1.Clear && f1.Swallowed && f1.InAttack && f1.FlashStarted && sb.SwallowedInAttack == 1 && sb.Flashes == 1,
                "E: a press during the swing was not swallowed (no chained blow)");
            LogHas("[athletics] YOU: attack pressed at ");
            LogHas(" during your own attack (no chained blow below the peak line) - swallowed: the engine never saw it (no wind-up); the Attack recovery bar flashes; keep it held and the attack starts the moment the pause ends");
            b.GateFrame(me, t + 0.3, false);                              // let go
            t += 0.5;
            double end = t;
            b.ObserveAction(me, ActIdle, t, in r);                        // the attack ends: the countdown
            float m = AthleticsMath.AttackSpeedMultiplier(Rules, me);
            double pause = 0.82 * (1 / m - 1);
            Check(timer.Running && !timer.InAttack && Near(timer.Duration, 0.82) && Near(timer.Pause, pause) && Near(timer.TimerEnd, end + pause),
                "E: the countdown: D " + timer.Duration + " (0.82), pause " + timer.Pause + " (" + pause + ")");
            LogHas("[athletics] YOU: first attack pause this battle at ");
            LogHas(" - your melee attack (wind-up 0.32 + swing 0.50 = D 0.82 s) ended at attack speed x0.59 (f 0.49) → no new attack for 0.57 s = D x (1/m - 1), until ");
            LogHas("; the hold began at your release's start (expected 0.22 s); your attack button does nothing until then (held, it attacks the moment it ends)");
            Check(AthleticsLogic.TryGetPlayerRecovery(me.Agent, end + pause / 2, out var half) && Math.Abs(half.Share - 0.5f) < 0.01f && half.SecondsText == "0.3 s" && half.Recovering,
                "E: halfway the recovery bar reads " + half.Share + " \"" + half.SecondsText + "\"");

            var f2 = b.GateFrame(me, end + 0.1, true);                    // a press in the countdown
            Check(f2.Swallowed && f2.Clear && !f2.InAttack && f2.FlashStarted && sb.SwallowedInTimer == 1 && sb.Flashes == 2, "E: a press in the countdown was not swallowed");
            var f3 = b.GateFrame(me, end + 0.3, true);                    // held on: cleared, no new press
            Check(f3.Clear && !f3.Swallowed, "E: a held button counted as a new press");
            var fe = b.GateFrame(me, end + pause, true);                  // the end, still held
            Check(fe.Ended && fe.HeldAtEnd && !fe.Clear && !timer.Holding, "E: the held button was not let through at the end");
            LogHas("[athletics] YOU: first attack pause ended at ");
            LogHas(" - your attack button was held: the next attack starts now; presses swallowed during it: 2");
            Check(AthleticsLogic.TryGetPlayerRecovery(me.Agent, end + pause, out var full) && full.Share == 1f && !full.Recovering, "E: the recovery bar is not full after the pause");
            t = end + pause + 0.01;
            b.ObserveAction(me, ActReadyMeleeCode, t, in r);             // the engine starts the held attack at once
            Check(sb.PlayerFiredHeld == 1 && Near(sb.FiredAfterMean, 0.01) && sb.StartedEarly(AttackKind.Melee, true) == 0, "E: the held button's attack was not counted as fired");
            LogHas("[athletics] YOU: your held attack button started the next attack at ");
            LogHas(", 0.01 s after the pause ended (hold-to-attack: near 0 = it fires the moment the recovery bar is full)");
            Check(release < end && Near(end - release, 0.5), "E: the swing's time");

            // the next swing: the hold from its release (his rest now known: 0.5 s) - then an attack the gate MISSED
            AthleticsLogic.ReadyFull(me, t + 0.32);
            t += 0.4;
            b.ObserveAction(me, ActRelease, t, in r);
            Check(timer.InAttack && sb.PlayerEarlyHolds == 2, "E: the second hold did not begin at the release");
            t += 0.5;
            b.ObserveAction(me, ActIdle, t, in r);
            Check(timer.Running, "E: no second countdown");
            t += 0.1;
            b.ObserveAction(me, ActReadyMeleeCode, t, in r);             // a ready although the hold was on
            Check(sb.PlayerStartedAnyway == 1 && !timer.Holding, "E: an attack during the hold was not counted / the hold kept");
            LogHas("[athletics] YOU: an attack began at ");
            LogHas(" - the input gate did not stop it; released (tell Claude: the gate's order or the engine's input timing)");
            AthleticsLogic.ReadyFull(me, t + 0.32);
            t += 0.4;
            b.ObserveAction(me, ActRelease, t, in r);
            t += 0.5;
            b.ObserveAction(me, ActIdle, t, in r);

            // AttackRatePlayerTimer off mid-countdown: released at once, logged; off = no hold at all
            Check(timer.Running, "E: no countdown before the switch-off");
            S.Set(SettingsSchema.AttackRatePlayerTimer, false, SettingSources.Mcm);
            b.ApplySettingsChange(S);
            b.TickPlayerTimer(t + 0.05);
            Check(!timer.Holding && sb.PlayerEnded(PlayerTimerEnd.SwitchedOff) == 1, "E: AttackRatePlayerTimer off did not release your pause");
            LogHas("[rate] AttackRatePlayerTimer switched OFF mid-mission at ");
            LogHas("[athletics] YOU: your attack pause released at ");
            LogHas(" - switched off (ModEnabled, AthleticsEnabled or AttackRatePlayerTimer): attack at once");
            Check(AthleticsLogic.TryGetPlayerRecovery(me.Agent, t, out var offRead) && !offRead.TimerOn && offRead.Share == 1f, "E: the recovery read is not full and off");
            t += 0.1;
            b.ObserveAction(me, ActReadyMeleeCode, t, in r);
            AthleticsLogic.ReadyFull(me, t + 0.32);
            t += 0.4;
            b.ObserveAction(me, ActRelease, t, in r);
            Check(!timer.Holding, "E: held while AttackRatePlayerTimer is off");
            t += 0.5;
            b.ObserveAction(me, ActIdle, t, in r);
            Check(!timer.Holding, "E: a countdown while AttackRatePlayerTimer is off");
            S.Set(SettingsSchema.AttackRatePlayerTimer, true, SettingSources.Mcm);
            b.ApplySettingsChange(S);

            // the master switch mid-countdown: released at once (the AI's too - TickPace); back on = full
            t += 0.5;
            b.ObserveAction(me, ActReadyMeleeCode, t, in r);
            AthleticsLogic.ReadyFull(me, t + 0.32);
            t += 0.4;
            b.ObserveAction(me, ActRelease, t, in r);
            t += 0.5;
            b.ObserveAction(me, ActIdle, t, in r);
            Check(timer.Running, "E: no countdown before the master switch");
            S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
            b.ApplySettingsChange(S);
            b.TickPlayerTimer(t + 0.05);
            b.TickPace(t + 0.05);
            Check(!timer.Holding && sb.PlayerEnded(PlayerTimerEnd.SwitchedOff) == 2, "E: ModEnabled off did not release your pause");
            S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
            b.ApplySettingsChange(S);
            b.TickPace(t + 0.1);
            t += 0.2;

            // review R26 (step 17): manning a siege engine - RangedSiegeWeapon fires on its pilot's attack bits; that
            // click is the engine's shot, not your attack: never cleared, never a swallowed press, the pause runs on
            SetHealth(me.Agent, 40f, 100f);
            b.CheckHealth(me, Rules, t);
            me.SpeedDirty = false;
            b.ObserveAction(me, ActReadyMeleeCode, t, in r);
            AthleticsLogic.ReadyFull(me, t + 0.32);
            t += 0.4;
            b.ObserveAction(me, ActRelease, t, in r);
            t += 0.5;
            b.ObserveAction(me, ActIdle, t, in r);
            Check(timer.Running, "R26: no countdown before the siege engine");
            int swallowedR26 = sb.SwallowedInTimer + sb.SwallowedInAttack, flashesR26 = sb.Flashes;
            var fo = b.GateFrame(me, t + 0.05, true, usingObject: true);   // a click on the ballista
            Check(!fo.Clear && !fo.Swallowed && timer.Running && sb.SwallowedInTimer + sb.SwallowedInAttack == swallowedR26 && sb.Flashes == flashesR26,
                "R26: the gate held a siege engine's trigger (your attack bits on a machine you man)");
            var foEnd = b.GateFrame(me, timer.TimerEnd, true, usingObject: true);
            Check(foEnd.Ended && !foEnd.HeldAtEnd && !foEnd.Clear && !timer.Holding, "R26: your pause did not run out on time while you manned the engine");
            t = timer.EndedAt + 0.1;

            // review R25 (step 17): RTS Camera's free camera hands your hero to the AI (he stays Mission.MainAgent): a
            // running pause ends at once, none starts while the AI drives him, and his attacks are no "gate misses"
            b.ObserveAction(me, ActReadyMeleeCode, t, in r);
            AthleticsLogic.ReadyFull(me, t + 0.32);
            t += 0.4;
            b.ObserveAction(me, ActRelease, t, in r);
            t += 0.5;
            b.ObserveAction(me, ActIdle, t, in r);
            Check(timer.Running, "R25: no countdown before the AI took your hero");
            int notYouR25 = sb.PlayerEnded(PlayerTimerEnd.NotYou), anywayR25 = sb.PlayerStartedAnyway, earlyR25 = sb.StartedEarly(AttackKind.Melee, true);
            b.SmokePlayerAiControlled = true;
            b.TickPlayerTimer(t + 0.02);
            Check(!timer.Holding && sb.PlayerEnded(PlayerTimerEnd.NotYou) == notYouR25 + 1, "R25: your pause was not released when the AI took your hero");
            t += 0.05;
            for (int i = 0; i < 2; i++)                                       // the AI attacks with him, twice
            {
                b.ObserveAction(me, ActReadyMeleeCode, t, in r);
                AthleticsLogic.ReadyFull(me, t + 0.32);
                t += 0.4;
                b.ObserveAction(me, ActRelease, t, in r);
                Check(!timer.Holding, "R25: a hold began on an AI-driven hero");
                t += 0.5;
                b.ObserveAction(me, ActIdle, t, in r);
                Check(!timer.Holding, "R25: a countdown started on an AI-driven hero (the gate cannot hold the AI's input)");
                t += 0.1;
            }
            Check(sb.PlayerStartedAnyway == anywayR25 && sb.StartedEarly(AttackKind.Melee, true) == earlyR25 && sb.PlayerEnded(PlayerTimerEnd.NotYou) == notYouR25 + 1,
                "R25: the AI's attacks with your hero read as gate misses: started anyway " + (sb.PlayerStartedAnyway - anywayR25) + ", early " + (sb.StartedEarly(AttackKind.Melee, true) - earlyR25));
            b.SmokePlayerAiControlled = false;                                // back in your hands: held again
            b.ObserveAction(me, ActReadyMeleeCode, t, in r);
            AthleticsLogic.ReadyFull(me, t + 0.32);
            t += 0.4;
            b.ObserveAction(me, ActRelease, t, in r);
            Check(timer.InAttack, "R25: back in your hands, your attack was not held again");
            t += 0.5;
            b.ObserveAction(me, ActIdle, t, in r);
            Check(timer.Running, "R25: back in your hands, no countdown");
            b.GateFrame(me, timer.TimerEnd, false);                           // it runs out, the button up
            Check(!timer.Holding, "R25: the countdown did not end");
            t = timer.EndedAt + 0.1;
            me.ResetFull();                                                   // the checks below start from a full bar, as before R25 / R26

            // ranged: the hold from the loose, the countdown from the RELOAD's end (D = draw + loose + reload)
            SetHealth(me.Agent, 40f, 100f);
            b.CheckHealth(me, Rules, t);
            me.SpeedDirty = false;
            b.ObserveAction(me, ActReadyRangedCode, t, in r);
            AthleticsLogic.ReadyFull(me, t + 1.0);
            t += 1.25;
            b.ObserveAction(me, ActReleaseRangedCode, t, in r);
            Check(timer.InAttack, "E: no hold from the loose");
            t += 0.13;
            b.ObserveAction(me, ActReloadCode, t, in r);
            Check(timer.InAttack && !timer.Running, "E: the countdown started before the reload ended");
            t += 1.2;
            b.ObserveAction(me, ActIdle, t, in r);
            float mr = AthleticsMath.AttackSpeedMultiplier(Rules, me);
            Check(timer.Running && timer.Kind == AttackKind.Ranged && Near(timer.Duration, 2.33) && Near(timer.Pause, 2.33 * (1 / mr - 1)),
                "E: the ranged countdown: " + timer.Kind + ", D " + timer.Duration + " (2.33), pause " + timer.Pause);
            // (left running: the mission end releases it)
        }

        /// <summary>Step 13: the input gate goes FIRST in the behaviour list, so the reverse pre-tick loop runs it
        /// right after the player controller - the list manipulation on a stand-in Mission (lists only).</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void GateGoesFirstInTheBehaviourList()
        {
            var mission = (Mission)FormatterServices.GetUninitializedObject(typeof(Mission));
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(Mission).GetField("<MissionBehaviors>k__BackingField", Private)!.SetValue(mission, new List<MissionBehavior>());
            typeof(Mission).GetField("<MissionLogics>k__BackingField", Private)!.SetValue(mission, new List<MissionLogic>());
            typeof(Mission).GetField("_otherMissionBehaviors", Private)!.SetValue(mission, new List<MissionBehavior>());
            var list = mission.MissionBehaviors;
            var other = new AthleticsLogic();
            var controller = (MissionBehavior)FormatterServices.GetUninitializedObject(typeof(MissionMainAgentController));
            list.Add(other);
            list.Add(controller);
            var logic = new AthleticsLogic();
            mission.AddMissionBehavior(logic);                            // appended last - like SubModule does
            string text = PlayerAttackGate.Attach(mission, logic);
            var gate = logic.Gate;
            Check(gate != null && ReferenceEquals(list[0], gate) && list.Count == 4 && mission.MissionLogics.Contains(gate!),
                "the gate is not first in the list (or not a mission logic): " + string.Join(", ", list.ConvertAll(x => x.GetType().Name)));
            Check(list.IndexOf(controller) == 2 && list.IndexOf(gate!) < list.IndexOf(controller), "the gate would pre-tick before the controller");
            Check(text == "attached: your attack gate FIRST in the behaviour list (0 of 4; behaviours pre-tick from the end, so it runs right after MissionMainAgentController at 2) - while your attack pause holds it clears only the attack bits of your input; blocking, kicks, moving, weapon switches untouched",
                "the attach line: " + text);
            // the reverse pre-tick order: the gate comes after the controller
            var order = new List<MissionBehavior>();
            for (int i = list.Count - 1; i >= 0; i--) order.Add(list[i]);
            Check(order.IndexOf(gate!) > order.IndexOf(controller), "in the pre-tick loop the gate runs before the controller");
            gate!.OnPreMissionTick(0.016f);                               // no hold: returns at once, touches nothing
            Check(!logic.PlayerTimer.Holding && logic.Stats.Errors == 0, "the gate did something with no hold");
        }

        /// <summary>The first slowed fighter's before → after line, driven through the real decorator
        /// (in game ApplyFighterSpeed does it around UpdateAgentProperties) - step 13: the animations stay
        /// at full speed (AttackAnimationMinPercent 100), the AI decisions untouched (off).</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FirstSlowedIsLogged()
        {
            AthleticsDefaults();
            var st = _logic!.Track(FakeAgent(220))!;
            var p = st.Agent.AgentDrivenProperties;
            _statTop!.UpdateAgentStats(st.Agent, p);
            var before = SpeedPenalty.Snapshot.Take(st.Agent);
            var aiBefore = SpeedPenalty.AiSnapshot.Take(st.Agent);
            st.SpeedMultiplier = 0.5f;
            _statTop.UpdateAgentStats(st.Agent, p);
            typeof(AthleticsLogic).GetMethod("LogFirstSlowed", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_logic, new object[] { st, before, aiBefore });
            LogHas("[rate] first slowed fighter this mission: (agent 220) at ");
            LogHas("animations (AttackAnimationMinPercent 100 → x1.00): swing 1.050 → 1.050, thrust/draw 1.020 → 1.020, reload 0.950 → 0.950 (each x1.00 as asked - full speed, no slow-mo)");
            LogHas("AI decisions (AttackRateAiDecisions off): attack chance 0.144 → 0.144 (x1.00), riposte chance 0.080 → 0.080 (x1.00), shoot chance 0.650 → 0.650 (x1.00), aim before a shot 0.600 → 0.600 (x1.00) (unchanged, as asked)");
            LogHas("the timer (you: AttackRatePlayerTimer on, AI: AttackRatePaceHold on) comes after each attack");
            LogHas("untouched on purpose: handling (blocking), shield defend speed, the recoil after a block, AIHoldingReady");
            Check(Near(p.HandlingMultiplier, 1.1f) && Near(p.MaxSpeedMultiplier, 0.8f * 1f), "the handling (blocking) or the run speed was touched by the attack multiplier");
            st.SpeedMultiplier = 1f;
        }
    }
}

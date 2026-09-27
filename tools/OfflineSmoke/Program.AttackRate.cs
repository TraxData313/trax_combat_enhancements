using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;
using TraxCombat.Models;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for the attack RATE (step 5e) - the REAL <see cref="AthleticsLogic"/>: every
    /// channel-1 action fed through its observer (phases, cycles by f, the targets and verdicts), the
    /// pace hold's bookkeeping (asked at the swing's end, started by the tick, lifted on every path)
    /// with a stand-in for the engine side (<see cref="IPaceBody"/>), the AI-decision switch
    /// re-applied, the first-slowed log. The fake agents have no native side: what it cannot check is
    /// that the engine honours NoAttack and the AI values - PLAYTEST "Attack rate" and the [summary]
    /// attack-rate lines prove that in game.
    /// </summary>
    internal static partial class Program
    {
        private const int ActReadyMeleeCode = (int)Agent.ActionCodeType.ReadyMelee;
        private const int ActBlockedCode = (int)Agent.ActionCodeType.BlockedMelee;

        /// <summary>The engine side of the pace hold, played by the smoke: answers set per check, calls recorded.</summary>
        private sealed class FakePaceBody : IPaceBody
        {
            public bool Take = true;
            public PaceRefusal? NextRefusal;
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
        /// One melee attack as the engine would play it if it honours the animation multipliers: a
        /// ready of <paramref name="ready"/> ÷ m (80% wind-up, then held), a swing of
        /// <paramref name="swing"/> ÷ m (m = the multiplier in effect when the ready began), an optional
        /// block recoil, then the tick; the AI's natural pause is <paramref name="pause"/> (its OWN
        /// seconds, unscaled: the worst case) - but a pace hold keeps it from the next ready until the
        /// hold is lifted (the AI obeying NoAttack). Returns when the next ready may begin.
        /// </summary>
        private static void Attack(AthleticsLogic logic, TrackedAgent st, ref double t, double ready = 0.4, double swing = 0.5, double pause = 0.4, bool blocked = false)
        {
            var r = Rules;
            float m = st.SpeedMultiplier;
            logic.ObserveAction(st, ActReadyMeleeCode, t, in r);
            AthleticsLogic.ReadyFull(st, t + 0.8 * ready / m);
            t += ready / m;
            logic.ObserveAction(st, ActRelease, t, in r);
            st.SpeedDirty = false; // as if the tick loop had applied the new multiplier
            t += swing / m;
            if (blocked)
            {
                logic.ObserveAction(st, ActBlockedCode, t, in r);
                t += 0.3;
            }
            logic.ObserveAction(st, ActIdle, t, in r);
            logic.TickPace(t);                         // the tick right after the swing's end
            var ps = st.Pace;
            if (ps != null && ps.Active && ps.Until > t + pause)
            {
                t = ps.Until;
                logic.TickPace(t);                     // the hold is lifted: the AI readies at once
            }
            else
            {
                t += pause;
            }
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
            return logic;
        }

        /// <summary>A 300-skill fighter: 8 blows at full strength (7 fresh cycles), empty after 30.</summary>
        private static TrackedAgent Veteran(AthleticsLogic logic, int index)
        {
            var st = logic.Track(FakeAgent(index))!;
            st.AthleticsSkill = 300;
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
                var rr = AttackRateRules.From(S);

                // ---- A: the animations alone (pace hold off, the AI's pause unchanged) - too fast when tired
                S.Set(SettingsSchema.AttackRatePaceHold, false, SettingSources.File);
                var bodyA = new FakePaceBody();
                var a = NewRateLogic(bodyA);
                LogHas("[rate] mission start: ON - animations x m always (swing, thrust / bow draw / throw, reload); AI decisions (AttackRateAiDecisions) on: the chance to attack, to riposte and to loose x m, the aim before a shot ÷ m; pace hold (AttackRatePaceHold) off; blocking and the recoil after a block: untouched - read live");
                var x = Veteran(a, 200);
                double t = 2000;
                for (int i = 0; i < 40; i++) Attack(a, x, ref t);
                var sa = a.RateStats;
                Check(x.Exhausted && sa.CycleCount(AttackKind.Melee, false, 0) == 7 && sa.CycleCount(AttackKind.Melee, false, 3) >= 9,
                    "A: cycles at the peak " + sa.CycleCount(AttackKind.Melee, false, 0) + ", empty " + sa.CycleCount(AttackKind.Melee, false, 3));
                Check(Near(sa.FreshCycle(AttackKind.Melee, false), 1.3) && Near(sa.PhaseMean(AttackKind.Melee, false, 0, AttackPhase.WindUp), 0.32)
                      && Near(sa.PhaseMean(AttackKind.Melee, false, 0, AttackPhase.Held), 0.08) && Near(sa.PhaseMean(AttackKind.Melee, false, 3, AttackPhase.Release), 2.5)
                      && Near(sa.PhaseMean(AttackKind.Melee, false, 3, AttackPhase.Pause), 0.4),
                    "A: phases - fresh cycle " + sa.FreshCycle(AttackKind.Melee, false) + ", wind-up " + sa.PhaseMean(AttackKind.Melee, false, 0, AttackPhase.WindUp)
                    + ", held " + sa.PhaseMean(AttackKind.Melee, false, 0, AttackPhase.Held) + ", empty swing " + sa.PhaseMean(AttackKind.Melee, false, 3, AttackPhase.Release)
                    + ", empty pause " + sa.PhaseMean(AttackKind.Melee, false, 3, AttackPhase.Pause));
                // empty: 0.9 ÷ 0.2 + 0.4 = 4.9 s where 1.3 ÷ 0.2 = 6.5 s was asked → about 75% (the
                // first empty cycle carries the last swing struck at the previous m: a little shorter)
                double ratioA = sa.Ratio(AttackKind.Melee, false, 3);
                Check(ratioA > 0.70 && ratioA < 0.76, "A: empty ratio " + ratioA);
                var linesA = sa.SummaryLines(AttackRateRules.From(S), false);
                Check(linesA.Exists(l => l.StartsWith("attack rate, melee, AI, empty (f 0): wind-up 1.60 (x5.00) + held 0.40 (x5.00), swing 2.50 (x5.00) (clean, hit nothing 2.50 (x5.00)), recoil after a block -, pause 0.40 (x1.00) | cycle ", StringComparison.Ordinal)
                                         && l.Contains("m 0.20 → target 6.50 s: 7") && l.EndsWith("% - too fast", StringComparison.Ordinal)),
                    "A: the empty band does not read too fast: " + string.Join(" // ", linesA));
                Check(linesA.Exists(l => l.StartsWith("attack rate, melee, AI - verdict: OFF TARGET", StringComparison.Ordinal)), "A: the group verdict is not OFF TARGET");
                Check(sa.Holds == 0 && x.Pace == null, "A: a hold with AttackRatePaceHold off");

                // ---- B: the pace hold on - the AI obeying NoAttack meets the target
                S.Set(SettingsSchema.AttackRatePaceHold, true, SettingSources.File);
                var body = new FakePaceBody();
                var b = NewRateLogic(body);
                var sb = b.RateStats;

                // a man tired by his wounds before any fresh cycle was seen: no reference, not held
                var w = b.Track(FakeAgent(210))!;
                SetHealth(w.Agent, 40f, 100f);
                b.CheckHealth(w, Rules, t);
                w.SpeedDirty = false;
                Attack(b, w, ref t);
                Attack(b, w, ref t);
                Check(sb.NotHeld(PaceNotHeld.NoReference) == 2 && sb.Holds == 0, "B: a hold without a fresh reference: " + sb.NotHeld(PaceNotHeld.NoReference));

                var y = Veteran(b, 211);
                // 7 swings end at full strength (m 1: never held); the 8th is the last struck at f 1, but
                // its charge takes him below the line - its end is the first tired one
                for (int i = 0; i < 7; i++) Attack(b, y, ref t);
                Check(y.FreshCycleCount == 6 && Near(y.FreshCycleSum / y.FreshCycleCount, 1.3) && sb.NotHeld(PaceNotHeld.FullStrength) == 7 && sb.Holds == 0,
                    "B: his fresh cycles " + y.FreshCycleCount + ", full-strength swings not held " + sb.NotHeld(PaceNotHeld.FullStrength));
                Attack(b, y, ref t); // the first tired swing end: a hold (logged in full)
                Check(sb.Holds == 1 && body.Started.Count == 1 && body.Released.Count == 1 && sb.Ended(PaceEnd.TimeUp) == 1,
                    "B: the first tired swing was not held and lifted: holds " + sb.Holds + ", started " + body.Started.Count + ", released " + body.Released.Count);
                LogHas("[rate] first pace hold this mission: (agent 211) at ");
                LogHas("fresh cycle 1.30 s (his own, 7 samples) → target ");
                LogHas("; scripted flags 0 → 2 (NoAttack set: the engine took it)");
                LogHas("[rate] first pace hold ended at ");
                LogHas(" - time up; scripted flags now 0; his next ready's delay after the hold is in the summary (pace hold ends)");
                for (int i = 0; i < 40; i++) Attack(b, y, ref t);
                Check(y.Exhausted && Near(sb.Ratio(AttackKind.Melee, false, 3), 1.0) && Near(sb.Ratio(AttackKind.Melee, false, 1), 1.0),
                    "B: held, the cycles do not meet the target: empty " + sb.Ratio(AttackKind.Melee, false, 3) + ", 0.5-1 " + sb.Ratio(AttackKind.Melee, false, 1));
                var linesB = sb.SummaryLines(AttackRateRules.From(S), false);
                Check(linesB.Exists(l => l.StartsWith("attack rate, melee, AI - verdict: ON TARGET in ", StringComparison.Ordinal)), "B: not ON TARGET: " + string.Join(" // ", linesB));
                Check(linesB.Exists(l => l.StartsWith("attack rate, melee, AI, empty (f 0):", StringComparison.Ordinal) && l.EndsWith("→ target 6.50 s: 100% - on target", StringComparison.Ordinal)),
                    "B: the empty band is not 100% on target");
                // the empty pause IS the hold: 6.5 s − the 4.5 s his ready and swing take (the first one a
                // little longer - its swing still ran at the previous m)
                Check(Math.Abs(sb.PhaseMean(AttackKind.Melee, false, 3, AttackPhase.Pause) - (6.5 - 0.9 / 0.2)) < 0.05, "B: the empty pause is not the hold: " + sb.PhaseMean(AttackKind.Melee, false, 3, AttackPhase.Pause));
                Check(sb.Holds >= 30 && sb.Released(PaceRelease.ClearedByUs) == sb.Holds,
                    "B: holds " + sb.Holds + " vs cleared " + sb.Released(PaceRelease.ClearedByUs));

                // chained: the next ready straight out of the swing - never held
                var r = Rules;
                int chained = sb.NotHeld(PaceNotHeld.AlreadyReadied);
                b.ObserveAction(y, ActReadyMeleeCode, t, in r);
                t += 2;
                b.ObserveAction(y, ActRelease, t, in r);
                t += 2.5;
                b.ObserveAction(y, ActReadyMeleeCode, t, in r); // chained
                b.TickPace(t);
                Check(sb.NotHeld(PaceNotHeld.AlreadyReadied) == chained + 1 && y.Pace!.Active == false, "B: a chained blow was held");
                t += 2;
                b.ObserveAction(y, ActRelease, t, in r);
                t += 2.5;
                b.ObserveAction(y, ActIdle, t, in r);

                // a swing slips through NoAttack: counted, the hold lifted by the next tick
                b.TickPace(t);
                Check(y.Pace!.Active, "B: no hold after the swing");
                double heldAt = t;
                t += 0.2;
                b.ObserveAction(y, ActReadyMeleeCode, t, in r);
                t += 0.1;
                b.ObserveAction(y, ActRelease, t, in r);
                b.TickPace(t);
                Check(!y.Pace.Active && sb.Ended(PaceEnd.SwingStarted) == 1, "B: a swing while held did not end the hold");
                t += 2.5;
                b.ObserveAction(y, ActIdle, t, in r);
                _ = heldAt;

                // a game job on him when the hold ends: our NoAttack stays until he is free (never under the job)
                b.TickPace(t);
                Check(y.Pace.Active, "B: no hold (game job check)");
                body.WaitingReleases = 2;
                double until = y.Pace.Until;
                b.TickPace(until);
                Check(!y.Pace.Active && y.Pace.Waiting && sb.Released(PaceRelease.Waiting) == 1, "B: the hold did not wait for the game job");
                b.TickPace(until + 0.1);                     // not yet re-checked (every 0.25 s)
                b.TickPace(until + 0.3);                     // re-checked: still busy
                Check(y.Pace.Waiting, "B: stopped waiting while the job ran");
                b.TickPace(until + 0.6);                     // free: cleared
                Check(!y.Pace.Waiting && sb.ClearedAfterWaiting == 1 && !body.LastEvenUnderAFrame, "B: NoAttack not cleared once the game job ended");
                t = until + 0.6;

                // a long plain scripted frame (a job that may want him to fight): lifted after 3 s anyway
                Attack(b, y, ref t, pause: 0);
                b.ObserveAction(y, ActReadyMeleeCode, t, in r);
                t += 2;
                b.ObserveAction(y, ActRelease, t, in r);
                t += 2.5;
                b.ObserveAction(y, ActIdle, t, in r);
                b.TickPace(t);
                Check(y.Pace.Active, "B: no hold (long frame check)");
                body.WaitingReleases = 1000;
                body.UnderAPlainFrame = true;
                until = y.Pace.Until;
                b.TickPace(until);                           // a frame on him: wait
                for (double wait = 0.25; wait < 2.9; wait += 0.25) b.TickPace(until + wait);
                Check(y.Pace.Waiting && sb.ClearedUnderAFrame == 0, "B: lifted under a frame before 3 s");
                b.TickPace(until + 3.05);
                Check(!y.Pace.Waiting && sb.ClearedUnderAFrame == 1 && body.LastEvenUnderAFrame, "B: our NoAttack outlived a 3 s scripted frame");
                body.WaitingReleases = 0;
                body.UnderAPlainFrame = false;
                t = until + 3.05;

                // the player takes him / he mounts: ended at once
                Attack(b, y, ref t, pause: 0);                // (ends with the hold lifted by time)
                b.ObserveAction(y, ActReadyMeleeCode, t, in r);
                t += 2;
                b.ObserveAction(y, ActRelease, t, in r);
                t += 2.5;
                b.ObserveAction(y, ActIdle, t, in r);
                b.TickPace(t);
                body.EndFor[211] = PaceEnd.Mounted;
                b.TickPace(t + 0.05);
                Check(!y.Pace.Active && sb.Ended(PaceEnd.Mounted) == 1, "B: mounting did not end the hold");

                // refused by the engine side: the game already had NoAttack on him; the flag did not stick
                body.NextRefusal = PaceRefusal.AlreadyNoAttack;
                Attack(b, y, ref t);
                Check(sb.Refused(PaceRefusal.AlreadyNoAttack) == 1, "B: AlreadyNoAttack not counted");
                body.Take = false;
                Attack(b, y, ref t);
                Check(sb.Refused(PaceRefusal.EngineIgnored) == 1 && !y.Pace.Active, "B: a hold the engine did not take is tracked");
                body.Take = true;

                // a rider is never held
                _setMount ??= FieldSetter<Agent?>("_cachedMountAgent");
                _setMount(y.Agent, FakeAgent(901));
                Attack(b, y, ref t);
                Check(sb.NotHeld(PaceNotHeld.Rider) == 1, "B: a rider was considered for a hold");
                _setMount(y.Agent, null);

                // left the field while held: no engine call
                var z = Veteran(b, 212);
                for (int i = 0; i < 12; i++) Attack(b, z, ref t);
                b.ObserveAction(z, ActReadyMeleeCode, t, in r);
                t += 0.5;
                b.ObserveAction(z, ActRelease, t, in r);
                t += 0.6;
                b.ObserveAction(z, ActIdle, t, in r);
                b.TickPace(t);
                Check(z.Pace != null && z.Pace.Active, "B: z not held");
                int releases = body.Released.Count;
                typeof(AthleticsLogic).GetMethod("Untrack", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(b, new object[] { z.Agent });
                Check(!z.Pace!.Active && sb.Ended(PaceEnd.LeftField) == 1 && body.Released.Count == releases, "B: leaving the field did not end the hold or called the engine");

                // switched off mid-hold: every hold lifted at once, logged; back on
                b.ObserveAction(y, ActReadyMeleeCode, t, in r);
                t += 2;
                b.ObserveAction(y, ActRelease, t, in r);
                t += 2.5;
                b.ObserveAction(y, ActIdle, t, in r);
                b.TickPace(t);
                Check(y.Pace.Active && b.HeldNow == 1, "B: y not held before the switch-off");
                S.Set(SettingsSchema.AttackRatePaceHold, false, SettingSources.Mcm);
                b.ApplySettingsChange(S);
                b.TickPace(t + 0.05);
                Check(b.HeldNow == 0 && sb.Ended(PaceEnd.SwitchedOff) == 1, "B: AttackRatePaceHold off did not lift every hold");
                LogHas("[rate] AttackRatePaceHold switched OFF mid-mission at ");
                LogHas(" s: 1 held fighters may attack again at once");
                int holds = sb.Holds;
                Attack(b, y, ref t);
                Check(sb.Holds == holds, "B: held while switched off");
                S.Set(SettingsSchema.AttackRatePaceHold, true, SettingSources.Mcm);
                b.ApplySettingsChange(S);
                b.TickPace(t);
                LogHas("[rate] the pace hold is ON again at ");

                // the AI-decision switch: every tired fighter recomputed, logged
                y.SpeedDirty = false;
                S.Set(SettingsSchema.AttackRateAiDecisions, false, SettingSources.Mcm);
                b.ApplySettingsChange(S);
                Check(y.SpeedDirty, "B: AttackRateAiDecisions off did not ask a recompute of a tired fighter");
                LogHas("[rate] AttackRateAiDecisions switched OFF mid-mission at ");
                LogHas(" tired fighters get their AI attack values back over the next ticks (at most 50 recomputes a tick)");
                S.Set(SettingsSchema.AttackRateAiDecisions, true, SettingSources.Mcm);
                b.ApplySettingsChange(S);
                y.SpeedDirty = false;

                // review 10a R1: a tired swing whose step back is REFUSED (the cap, a shield wall, no enemy
                // near) is still held; one whose step back STARTS is not (the step back holds his attacks)
                var stepBody = new FakeStepBody();
                b.StepBackBody = stepBody;
                S.Set(SettingsSchema.StepBackEnabled, true, SettingSources.File);
                Check(y.Exhausted, "R1: precondition - y is not empty (his swing would not ask for a step back)");
                int holdsR1 = sb.Holds, steppingNotHeld = sb.NotHeld(PaceNotHeld.SteppingBack);
                stepBody.NextRefusal = StepBackRefusal.AtOnceCap;
                b.ObserveAction(y, ActReadyMeleeCode, t, in r);
                t += 2;
                b.ObserveAction(y, ActRelease, t, in r);
                t += 2.5;
                b.ObserveAction(y, ActIdle, t, in r);
                Check(y.StepBack != null && y.StepBack.Pending, "R1: the empty swing did not ask for a step back");
                b.TickStepBacks(t);                          // the tick: step backs first...
                b.TickPace(t);                               // ...then the holds
                Check(b.StepStats.Refused(StepBackRefusal.AtOnceCap) == 1 && b.SteppingNow == 0 && y.Pace.Active && sb.Holds == holdsR1 + 1,
                    "R1: a refused step back left the swing unheld - holds " + (sb.Holds - holdsR1) + ", held now " + y.Pace.Active);
                t = y.Pace.Until;
                b.TickPace(t);                               // time up
                b.ObserveAction(y, ActReadyMeleeCode, t, in r);
                t += 2;
                b.ObserveAction(y, ActRelease, t, in r);
                t += 2.5;
                b.ObserveAction(y, ActIdle, t, in r);
                b.TickStepBacks(t);                          // this time it starts
                b.TickPace(t);
                Check(b.SteppingNow == 1 && !y.Pace.Active && sb.Holds == holdsR1 + 1 && sb.NotHeld(PaceNotHeld.SteppingBack) == steppingNotHeld + 1,
                    "R1: a hold started on a man stepping back (or was not counted as stepping back)");
                t += 1.6;
                b.TickStepBacks(t);                          // its time is up: released
                Check(b.SteppingNow == 0, "R1: the step back did not end");
                S.Set(SettingsSchema.StepBackEnabled, false, SettingSources.File);
                b.StepBackBody = new FakeStepBody { Allow = false };

                // review 10a R2: a new hold while the last one still waits for a game job - listed once
                var heldList = (List<TrackedAgent>)typeof(AthleticsLogic).GetField("_paceHeld", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(b)!;
                b.ObserveAction(y, ActReadyMeleeCode, t, in r);
                t += 2;
                b.ObserveAction(y, ActRelease, t, in r);
                t += 2.5;
                b.ObserveAction(y, ActIdle, t, in r);
                b.TickPace(t);
                Check(y.Pace.Active, "R2: no hold");
                body.WaitingReleases = 2;
                t = y.Pace.Until;
                b.TickPace(t);                               // a game job on him: waiting
                Check(y.Pace.Waiting && heldList.Count == 1, "R2: not waiting");
                b.ObserveAction(y, ActReadyMeleeCode, t, in r); // (the stand-in lets him swing - in game the job cleared NoAttack)
                t += 2;
                b.ObserveAction(y, ActRelease, t, in r);
                t += 2.5;
                b.ObserveAction(y, ActIdle, t, in r);
                b.TickPace(t);                               // the waiting pass (still busy), then the new hold starts
                Check(y.Pace.Active && heldList.Count == 1, "R2: the new hold listed him twice: " + heldList.Count);
                int endsR2 = sb.Ended(PaceEnd.TimeUp);
                t = y.Pace.Until;
                b.TickPace(t);
                Check(!y.Pace.Active && heldList.Count == 0 && sb.Ended(PaceEnd.TimeUp) == endsR2 + 1, "R2: the hold ended twice or stayed listed: ends "
                    + (sb.Ended(PaceEnd.TimeUp) - endsR2) + ", listed " + heldList.Count);
                body.WaitingReleases = 0;

                // mission end: a running hold is lifted before the summary; the summary mentions the switch
                b.ObserveAction(y, ActReadyMeleeCode, t, in r);
                t += 2;
                b.ObserveAction(y, ActRelease, t, in r);
                t += 2.5;
                b.ObserveAction(y, ActIdle, t, in r);
                b.TickPace(t);
                Check(b.HeldNow == 1, "B: y not held before the mission end");
                b.WriteAthleticsSummary();
                Check(b.HeldNow == 0 && sb.HeldAtMissionEnd == 1 && sb.Ended(PaceEnd.MissionEnd) == 1, "B: mission end did not lift the hold");
                LogHas(" - an attack-rate switch CHANGED during this battle: the rows below mix both settings");
                LogHas("[summary] attack rate, melee, AI, peak (f 1): wind-up 0.32 + held 0.08, swing 0.50 (clean, hit nothing 0.50), recoil after a block -, pause 0.40 | cycle 1.30 s (n 14), m 1.00 - the fresh reference");
                LogHas("[summary] attack rate, melee, AI - verdict: ON TARGET in ");
                LogHas("[summary] attack rate, ranged, AI: no attacks measured");
                LogHas("[summary] attack rate - pace hold (AttackRatePaceHold on at the end; tired AI fighters on foot, after a melee swing): ");
                LogHas("[summary] attack rate - pace hold, not held: at full strength ");
                LogHas("[summary] attack rate - pace hold ends: time up ");
                LogHas(", a swing started anyway 1 (must be about 0 - NoAttack holds swings), switched off 1, left the field 1, mission end 1, you took him 0, mounted 1, error 0");
                // (3 waited: the game job, the long frame and R2's - R2's was followed by a new hold, not cleared)
                LogHas("a game job on him at the end (left alone, cleared once free: 2, of them under a long scripted frame: 1) 3, still held at mission end 1");
                LogHas("[summary] attack rate - guard by f (melee hits on fighters on foot that were blocked or parried; blocking is never slowed - tired men must not block less): ");
                _ = rr;
                _ = w;
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
                StepBackDefaults();
            }
        }

        /// <summary>The first slowed fighter's before → after line, driven through the real decorator
        /// (in game ApplyFighterSpeed does it around UpdateAgentProperties).</summary>
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
            LogHas("T1 animations: swing 1.050 → 0.525, thrust/draw 1.020 → 0.510, reload 0.950 → 0.475 (each x0.50 as asked)");
            LogHas("T2 AI decisions (AttackRateAiDecisions on): attack chance 0.144 → 0.072 (x0.50), riposte chance 0.080 → 0.040 (x0.50), shoot chance 0.650 → 0.325 (x0.50), aim before a shot 0.600 → 1.200 (x2.00) (chances x0.50, the aim ÷ 0.50 as asked)");
            LogHas("untouched on purpose: handling (blocking), shield defend speed, the recoil after a block, AIHoldingReady");
            Check(Near(p.HandlingMultiplier, 1.1f), "the handling (blocking) was touched");
            st.SpeedMultiplier = 1f;
        }
    }
}

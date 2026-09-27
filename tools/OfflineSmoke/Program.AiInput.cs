using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 16 - the AI holds through the AI's own input (AI_NOTES "Step 16"): the REAL
    /// <see cref="AthleticsLogic"/> with stand-ins for the engine sides of the input technique (hooking a man has
    /// native calls - the fake agents have none). What runs for real: the technique read at each start, the wishes the
    /// logic writes into each man's <see cref="AiInputState"/>, the filter the component runs on every frame
    /// (<see cref="AiInputHook.Apply"/> - the attack bits out, the guard raised, a ready cancelled, the backwards
    /// vector), the timer surviving a step back both ways round (the later end), the arrival and the edge, the master
    /// switch, leaving the field, the "never called" warning, the guard by state through OnMeleeHit, the logs and the
    /// summary. What only the game can tell: that the engine calls the hook and honours the edited input - PLAYTEST's
    /// A/B and the [summary] "AI holds" lines prove it.
    /// </summary>
    internal static partial class Program
    {
        private const uint MvForward = (uint)Agent.MovementControlFlag.Forward;
        private const uint MvAttackDown = (uint)Agent.MovementControlFlag.AttackDown;
        private const uint MvAttackLeft = (uint)Agent.MovementControlFlag.AttackLeft;
        private const uint MvDefendLeft = (uint)Agent.MovementControlFlag.DefendLeft;
        private const uint MvDefendDown = (uint)Agent.MovementControlFlag.DefendDown;

        /// <summary>One frame of the engine asking the hook - what <see cref="AiInputComponent.OnAIInputSet"/> does
        /// after its early-out and the not-AI check (the fake agents cannot answer IsAIControlled).</summary>
        private static uint InputFrame(TrackedAgent st, uint flags, ref float vx, ref float vy)
        {
            var s = st.Input!;
            if (!s.Active) return flags;
            s.Calls++;
            AiInputHook.Apply(st, s, S.AiHoldRaiseGuard, ref flags, ref vx, ref vy);
            return flags;
        }

        private static uint InputFrame(TrackedAgent st, uint flags)
        {
            float vx = 0f, vy = 1f;
            return InputFrame(st, flags, ref vx, ref vy);
        }

        /// <summary>A melee hit on <paramref name="victim"/> through the real OnMeleeHit (the attacker untracked).</summary>
        private static void MeleeHitOn(AthleticsLogic logic, TrackedAgent victim, bool blocked)
        {
            var cd = AttackCollisionData.GetAttackCollisionDataForDebugPurpose(
                blocked, false, false, true, false, false, false, false, false, false, false, false,
                blocked ? CombatCollisionResult.Blocked : CombatCollisionResult.StrikeAgent, 0, 0, 0, 0, default(BoneBodyPartType), 0, default(Agent.UsageDirection), 0,
                default(CombatHitResultFlags), 0.5f, 0.5f, 0f, 0f, 0f, 0f, 0f, 0f,
                Vec3.Zero, Vec3.Zero, Vec3.Zero, Vec3.Zero, Vec3.Zero, Vec3.Zero, Vec3.Zero);
            logic.OnMeleeHit(FakeAgent(990), victim.Agent, false, cd);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AiHoldsByInput()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                StepBackDefaults();
                S.Set(SettingsSchema.AttackRatePaceByInput, true, SettingSources.File);
                S.Set(SettingsSchema.StepBackBackpedal, true, SettingSources.File);
                S.Set(SettingsSchema.AiHoldRaiseGuard, true, SettingSources.File);

                // ---- the Core bits are the game's own; the component overrides the hook, vanilla's do not
                Check((uint)Agent.MovementControlFlag.AttackMask == AiInputMath.AttackMask && (uint)Agent.MovementControlFlag.DefendMask == AiInputMath.DefendMask
                      && (uint)Agent.MovementControlFlag.DefendDown == AiInputMath.DefendDown && (uint)Agent.MovementControlFlag.MoveMask == (AiInputMath.MoveBits | 0x30),
                    "Core's input bits are not the game's MovementControlFlag values");
                Check(AiInputHook.OverridesInputSet(typeof(AiInputComponent)) && !AiInputHook.OverridesInputSet(typeof(HumanAIComponent))
                      && !AiInputHook.OverridesInputSet(typeof(CommonAIComponent)), "the override check is wrong (ours must override OnAIInputSet, vanilla's components do not)");

                var logic = new AthleticsLogic();
                _logic = logic;
                SetStatic(typeof(AthleticsLogic), "_current", logic);
                typeof(AthleticsLogic).GetMethod("StartAthletics", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(logic, null);
                LogHas("AI (AttackRatePaceHold) on - by input (AttackRatePaceByInput on: only the attack bits taken out of his own input, his guard his own, raised when he wants to attack - AiHoldRaiseGuard on), melee and ranged, on foot and mounted");
                LogHas("at most 50 at once; a backpedal (StepBackBackpedal on: a backwards input, facing his enemy, until the distance is covered) - read live; technique: a backpedal - a backwards movement written into the AI's own input");
                var legacyPace = new FakePaceBody();
                var inputPace = new FakePaceBody();
                var legacyStep = new FakeStepBody();
                var inputStep = new FakeStepBody { Input = true };
                logic.PaceBody = legacyPace;
                logic.PaceInputBody = inputPace;
                logic.StepBackBody = legacyStep;
                logic.StepBackInputBody = inputStep;
                logic.StepBackDice = new FixedDice(0.999); // only an empty bar steps back
                var rate = logic.RateStats;
                var steps = logic.StepStats;
                var holds = logic.HoldStats;
                double t = 5000;

                // ---- A: a tired man's AI timer by input - hooked, only the attack bits out, the guard raised
                var y = Wounded(logic, 400, 40f, t);                  // f 0.53
                AttackUntilHeld(logic, y, ref t);
                Check(y.Pace != null && y.Pace.Active && y.Pace.ByInput && y.Input != null && y.Input.HoldAttacks && inputPace.Started.Contains(400)
                      && legacyPace.Started.Count == 0 && rate.HoldsByInput == 1 && rate.HoldsByFlag == 0,
                    "A: the timer did not start by input (or the NoAttack body was used)");
                LogHas("[rate] first AI timer this mission: (agent 400) at ");
                LogHas("; technique: BY INPUT - our component added, the engine's input callback turned on by us; only the attack bits are taken out of his own input while it runs, a guard raised when he wants to attack; scripted flags 0 (none of ours)");
                Check(InputFrame(y, MvForward) == MvForward, "A: a frame without an attack wish was changed");
                Check(InputFrame(y, MvForward | MvAttackDown) == (MvForward | MvDefendDown), "A: the attack wish was not replaced by a guard");
                Check(InputFrame(y, MvAttackLeft | MvDefendLeft) == MvDefendLeft, "A: his own guard was not kept as it was");
                S.Set(SettingsSchema.AiHoldRaiseGuard, false, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                LogHas("[rate] AiHoldRaiseGuard switched OFF mid-mission at ");
                Check(InputFrame(y, MvForward | MvAttackDown) == MvForward, "A: with AiHoldRaiseGuard off the attack must only be dropped");
                int prevAction = y.PrevAction;
                y.PrevAction = ActReadyMeleeCode;                   // in a ready: cancelled with a guard, never released
                Check(InputFrame(y, MvAttackDown) == MvDefendDown, "A: an attack wish in a ready was not cancelled (clearing the bits alone would release the blow)");
                y.PrevAction = prevAction;
                S.Set(SettingsSchema.AiHoldRaiseGuard, true, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                LogHas("[rate] AiHoldRaiseGuard switched ON mid-mission at ");
                var s = y.Input!;
                Check(s.Calls == 5 && s.AttackCleared == 4 && s.GuardRaised == 1 && s.OwnGuardKept == 1 && s.ReadyCancelled == 1, "A: the hook's counts: calls "
                    + s.Calls + ", cleared " + s.AttackCleared + ", raised " + s.GuardRaised + ", kept " + s.OwnGuardKept + ", cancelled " + s.ReadyCancelled);
                MeleeHitOn(logic, y, blocked: true);                // a hit while held, blocked
                MeleeHitOn(logic, y, blocked: false);
                Check(holds.Hits(0) == 2 && holds.Blocked(0) == 1 && y.Pace!.HitsTaken == 2, "A: the guard by state did not file the hits on a held man");
                t = y.Pace!.Until;
                logic.TickPace(t);
                Check(!y.Pace.Active && !s.HoldAttacks && !s.Active && inputPace.Released.Contains(400) && legacyPace.Released.Count == 0, "A: the timer by input did not end cleanly");
                Check(InputFrame(y, MvAttackDown) == MvAttackDown, "A: an idle man's input was still changed");
                LogHas("[rate] first AI timer ended at ");
                LogHas(" - time up; the input hook: the engine's first call: movement bits Forward → Forward, input vector (0.00, 1.00) → (0.00, 1.00); an attack wish while held: Forward|AttackDown → Forward|DefendDown (the attack taken out, a guard raised); calls so far 5; melee hits taken while held 2 (blocked 1)");

                // ---- B: the timer SURVIVES a step back - by input both start at once; the attacks stay held until the later end
                S.Set(SettingsSchema.StepBackDistance, 1.0, SettingSources.Mcm);   // the stand-in covers 0.3 m a steer: arrives on the 4th
                var e = EmptyRecruit(401, ref t);                    // his 5th swing: empty - a step back AND a timer asked
                Check(e.StepBack != null && e.StepBack.Pending && e.Pace != null && e.Pace.Pending, "B: the empty swing did not ask for both");
                logic.TickStepBacks(t);
                logic.TickPace(t);
                var es = e.Input!;
                Check(e.StepBack!.Active && e.StepBack.ByInput && es.Backpedal && es.StepHoldAttacks && e.Pace!.Active && e.Pace.ByInput && es.HoldAttacks && e.Pace.Overlapped
                      && inputStep.Started.Contains(401) && legacyStep.Started.Count == 0,
                    "B: the step back and the timer did not both start (the timer must survive the step back - R1's drop is gone)");
                Check(Near(es.BackX, 0.1) && Near(es.BackY, -0.99), "B: the first backwards vector was not written: (" + es.BackX + ", " + es.BackY + ")");
                LogHas("[stepback] first step back this mission: (agent 401) at ");
                LogHas("technique: a BACKPEDAL through his own input (our component added, the engine's input callback turned on by us; the line away from him (0.00, -1.00) in his own frame now (0.10, -0.99) - (0, -1) = straight back; his attacks held (only the attack bits out); scripted flags none (none of ours))");
                float vx = 0f, vy = 1f;
                Check(InputFrame(e, MvForward | MvAttackDown, ref vx, ref vy) == MvDefendDown && Near(vx, 0.1) && Near(vy, -0.99),
                    "B: while stepping back and held: the attack not out, his Forward bit not out, or the backwards vector not written");
                double started = t;
                for (int i = 1; i <= 2; i++) logic.TickStepBacks(started + 0.25 * i);
                Check(e.StepBack.Active && steps.Samples >= 1, "B: ended too early / never sampled");
                logic.TickStepBacks(started + 0.75);              // the 4th steer: 1.2 m of 1.0 → arrived
                Check(!e.StepBack.Active && steps.Ended(StepBackEnd.Arrived) == 1 && !es.Backpedal && !es.StepHoldAttacks && es.HoldAttacks && inputStep.Released.Contains(401),
                    "B: the backpedal did not arrive and stop (or it took the timer with it)");
                LogHas("[stepback] first step back ended (arrived (StepBackDistance covered)) after 0.8 s: ");
                LogHas("; the backpedal input stopped - nothing of ours left in the engine (scripted flags none); his own AI and formation take him back");
                LogHas("the engine's first call: movement bits Forward|AttackDown → DefendDown, input vector (0.00, 1.00) → (0.10, -0.99); an attack wish while stepping back: Forward|AttackDown → DefendDown (the attack taken out, a guard raised)");
                vx = 0f;
                vy = 1f;
                Check(InputFrame(e, MvAttackDown, ref vx, ref vy) == MvDefendDown && vx == 0f && vy == 1f, "B: after the step back the timer no longer held him - or the vector was still written");
                t = e.Pace!.Until;
                logic.TickPace(t);
                Check(!e.Pace.Active && !es.Active && holds.HoldsOverlappingAStep == 1, "B: the overlapping timer did not end / was not counted");
                S.Set(SettingsSchema.StepBackDistance, 2.0, SettingSources.Mcm);

                // ---- C: the step back outlasts the timer: still held until ITS end; the ground ends behind him
                var c = Wounded(logic, 402, 70f, t);                // f 0.93: after a swing a pause of a few tenths
                logic.StepBackDice = new FixedDice(0.0);            // this swing steps back
                SwingOnce(logic, c, ref t);
                logic.TickStepBacks(t);
                logic.TickPace(t);
                var cs = c.Input!;
                Check(c.StepBack!.Active && c.Pace != null && c.Pace.Active && cs.HoldAttacks && cs.Backpedal, "C: not both running");
                InputFrame(c, MvForward);                           // the engine called us (no "never called")
                logic.TickPace(c.Pace!.Until);                      // the timer is over...
                logic.TickStepBacks(c.Pace.Until);
                Check(!c.Pace.Active && !cs.HoldAttacks && c.StepBack.Active && cs.StepHoldAttacks, "C: the timer's end did not leave the step back holding");
                Check(InputFrame(c, MvAttackLeft) == MvDefendDown, "C: his attacks were free before the LATER end (the step back's)");
                inputStep.SteerEnd[402] = StepBackEnd.EdgeAhead;
                logic.TickStepBacks(c.Pace.Until + 0.1);
                Check(!c.StepBack.Active && steps.Ended(StepBackEnd.EdgeAhead) == 1 && !cs.Active, "C: the edge ahead did not stop the backpedal");
                Check(InputFrame(c, MvAttackLeft) == MvAttackLeft, "C: attacks still held after both ended");
                inputStep.SteerEnd.Clear();
                logic.StepBackDice = new FixedDice(0.999);
                t = c.Pace.Until + 0.2;

                // ---- D: the master switch releases a running backpedal and a running timer at once; back on = both again
                var d = EmptyRecruit(403, ref t);
                logic.TickStepBacks(t);
                logic.TickPace(t);
                Check(d.StepBack!.Active && d.Pace!.Active && d.Input!.Active, "D: precondition - not both running");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                logic.TickStepBacks(t + 0.05);
                logic.TickPace(t + 0.05);
                Check(!d.StepBack!.Active && !d.Pace!.Active && !d.Input!.Active && steps.Ended(StepBackEnd.SwitchedOff) >= 1 && rate.Ended(PaceEnd.SwitchedOff) >= 1,
                    "D: ModEnabled off did not release the backpedal and the timer at once");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                logic.TickStepBacks(t + 0.1);
                logic.TickPace(t + 0.1);
                t += 0.2;

                // ---- E: he leaves the field while both run - every wish off, no engine call on him
                var g = EmptyRecruit(404, ref t);
                logic.TickStepBacks(t);
                logic.TickPace(t);
                Check(g.StepBack!.Active && g.Pace!.Active, "E: precondition - not both running");
                int stepReleases = inputStep.Released.Count, paceReleases = inputPace.Released.Count;
                typeof(AthleticsLogic).GetMethod("Untrack", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(logic, new object[] { g.Agent });
                Check(!g.Input!.Active && inputStep.Released.Count == stepReleases && inputPace.Released.Count == paceReleases,
                    "E: leaving the field left a wish on, or called the engine on a removed man");

                // ---- E2 (review R27, step 17): the game takes a backpedalling man over (a ladder, an object) - ended "handed
                // over", and the input body's release still runs: a backpedal has nothing of the game's to cancel, and the
                // engine's input callback we turned on must go off again (before, it stayed on until his next hold ended)
                var ho = EmptyRecruit(408, ref t);
                logic.TickStepBacks(t);
                logic.TickPace(t);
                Check(ho.StepBack!.Active && ho.StepBack.ByInput && ho.Pace != null && ho.Pace.Active, "E2: precondition - not backpedalling and held");
                InputFrame(ho, MvForward);                          // the engine called us (no "never called")
                var hoIn = ho.Input!;
                int handedOver = steps.Ended(StepBackEnd.HandedOver);
                inputStep.EndFor[408] = StepBackEnd.HandedOver;
                logic.TickStepBacks(t + 0.1);
                Check(!ho.StepBack.Active && steps.Ended(StepBackEnd.HandedOver) == handedOver + 1 && inputStep.Released.Contains(408)
                      && !hoIn.Backpedal && hoIn.HoldAttacks,
                    "E2 / R27: a backpedal handed over to the game was not released through the input body (the callback would stay on) - or it took the timer with it");
                inputStep.EndFor.Clear();
                t = ho.Pace!.Until;
                logic.TickPace(t);
                Check(!ho.Pace.Active && !hoIn.Active, "E2: his timer did not end after the hand-over");

                // ---- F: a hold the engine never calls us during: counted and warned once
                var h = Wounded(logic, 405, 40f, t);
                AttackUntilHeld(logic, h, ref t);
                Check(h.Pace != null && h.Pace.Active, "F: precondition - not held");
                t = h.Pace!.Until;
                logic.TickPace(t);
                Check(holds.HoldsWithoutACall == 1, "F: a hold with no call from the engine was not counted: " + holds.HoldsWithoutACall);
                LogHas(" and the engine never called our input hook - the new way may not work in this game: switch \"Tired AI keep their guard up\" (AttackRatePaceByInput) off and tell Claude");

                // ---- G: the switch back to the old ways mid-battle: new holds by NoAttack, new steps scripted, logged
                S.Set(SettingsSchema.AttackRatePaceByInput, false, SettingSources.Mcm);
                S.Set(SettingsSchema.StepBackBackpedal, false, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                logic.TickStepBacks(t);
                LogHas("[rate] AttackRatePaceByInput switched OFF mid-mission at ");
                LogHas("[stepback] StepBackBackpedal switched OFF mid-mission at ");
                var k = EmptyRecruit(406, ref t);
                logic.TickStepBacks(t);
                logic.TickPace(t);
                Check(k.StepBack!.Active && !k.StepBack.ByInput && legacyStep.Started.Contains(406) && k.Pace != null && k.Pace.Deferred,
                    "G: after the switch the step back was not the scripted walk / the NoAttack hold not deferred behind it");
                t += 1.6;
                logic.TickStepBacks(t);
                logic.TickPace(t);
                Check(k.Pace!.Active && !k.Pace.ByInput && legacyPace.Started.Contains(406), "G: the deferred NoAttack hold was not set when the scripted walk ended");
                t = k.Pace.Until;
                logic.TickPace(t);

                // ---- the summary: the technique, the guard by state, the hook, the overlaps; the header
                logic.WriteAthleticsSummary();
                LogHas("[summary] AI holds - technique: the AI timer NoAttack (AttackRatePaceByInput off: the engine's no-attack flag, step 13's technique) - this battle ");
                LogHas(" holds by input, 1 by NoAttack (AttackRatePaceByInput SWITCHED mid-battle: the lines mix both)");
                LogHas("backpedals, 1 scripted walks (StepBackBackpedal SWITCHED mid-battle: the lines mix both)");
                LogHas("[summary] AI holds - GUARD (melee hits on AI fighters on foot that were blocked or parried; THE fix target: held and stepping back close to everyone else): held by the timer 50% (n 2)");
                LogHas("[summary] AI holds - the input hook (AgentComponent.OnAIInputSet): ");
                LogHas("the attack bits taken out in ");
                LogHas("holds / backpedals the engine never called us during 1 / 0");
                LogHas("[summary] AI holds - the timer survives a step back: holds that overlapped a step back ");
                LogHas("NoAttack holds deferred behind a scripted step back 1 (set when the step ended 1, covered by the step back 0)");
                LogHas("[summary] step back - technique: MIXED this battle (StepBackBackpedal switched): ");
                LogHas(", arrived (StepBackDistance covered) 1, cut short ");
                LogHas("the ground ends behind him (edge ahead) 1");
                LogHas("; backpedals ended by stopping the input ");
                Check(logic.HoldsHeader() == "AI holds: mixed (a switch changed mid-battle)", "the summary header's technique: " + logic.HoldsHeader());

                // ---- the component itself: it loads, is built on an agent, and an idle man's frame is untouched
                var comp = new AiInputComponent(y.Agent, y);
                var ef = Agent.EventControlFlag.Kick;
                var mf = Agent.MovementControlFlag.AttackDown;
                var v = new Vec2(0.5f, 0.5f);
                comp.OnAIInputSet(ref ef, ref mf, ref v);
                Check(mf == Agent.MovementControlFlag.AttackDown && ef == Agent.EventControlFlag.Kick && v.x == 0.5f && v.y == 0.5f,
                    "the component touched an idle man's input");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
                StepBackDefaults();
            }
        }
    }
}

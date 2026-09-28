using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.Missions.Objectives;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 19 - the hideout boss fight is a fresh start for the player's side. The REAL
    /// <see cref="AthleticsLogic"/> is handed the game's own <see cref="MissionObjective"/> type (a stand-in with
    /// the boss objective's id and the game's raw "{=QEynMlwL}Win the Duel" / "{=0sPTRh6L}Win the Fight" names),
    /// with the teams played by a stand-in (<see cref="AthleticsLogic.HideoutSideOf"/>) and the step back / AI
    /// pause bodies by the smoke's (Program.StepBack.cs, Program.AttackRate.cs). What it cannot check: that the
    /// game's MissionObjectiveLogic really shows the objective, the teams in a duel, the intro's CutScene mode -
    /// PLAYTEST "F5" and the [athletics] / [summary] lines prove those in game.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>The game's objective type with SandBox's DefeatHideoutBossObjective's id and names (that class is
        /// internal to SandBox - the logic reads only UniqueId and Name.Value).</summary>
        private sealed class SmokeObjective : MissionObjective
        {
            private readonly string _id;
            private readonly TextObject _name;

            public SmokeObjective(string id, string rawName)
                : base(null)
            {
                _id = id;
                _name = new TextObject(rawName);
            }

            public override string UniqueId => _id;

            public override TextObject Name => _name;

            public override TextObject Description => _name;
        }

        private static SmokeObjective BossObjective(bool duel) => new SmokeObjective(HideoutBossFightMath.BossObjectiveId,
            duel ? "{=QEynMlwL}Win the Duel" : "{=0sPTRh6L}Win the Fight");

        /// <summary>A new mission's boss phase (the logic keeps one per mission).</summary>
        private static void ResetHideout(string? controller = "HideoutMissionController")
        {
            var h = new HideoutBossFightStats { Controller = controller };
            typeof(AthleticsLogic).GetField("_hideout", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_logic, h);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void HideoutBossFightIsAFreshStart()
        {
            SetStatic(typeof(AthleticsLogic), "_current", _logic); // (the fail-safe step left no mission running)
            AthleticsDefaults();
            StepBackDefaults();
            var logic = _logic!;
            logic.ApplySettingsChange(S);
            typeof(AthleticsLogic).GetField("_stepClosed", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(logic, false);
            typeof(AthleticsLogic).GetField("_paceClosed", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(logic, false);
            var stepBody = new FakeStepBody();
            logic.StepBackBody = stepBody;
            logic.StepBackDice = new FixedDice(0.999); // only an empty bar steps back
            var paceBody = new FakePaceBody();
            logic.PaceBody = paceBody;
            ResetHideout();
            double t = 900;

            // --- the first fight: the player's side tired in every way that must end
            // a man stepping back (swung empty; his own pause waits behind the scripted walk - deferred)
            var stepper = EmptyRecruit(81, ref t);
            logic.TickStepBacks(t);
            Check(logic.SteppingNow == 1 && stepper.StepBack != null && stepper.StepBack.Active, "precondition: fighter 81 is not stepping back");
            // a wounded man held by his AI pause
            var held = logic.Track(FakeAgent(82))!;
            for (int i = 0; i < 3; i++) Swing(held, ref t);
            var hr = Rules;
            logic.ObserveAction(held, ActReady, t, in hr);
            t += 0.5 / held.SpeedMultiplier;
            logic.ObserveAction(held, ActRelease, t, in hr);
            held.SpeedDirty = false;
            t += 0.5 / held.SpeedMultiplier;
            logic.ObserveAction(held, ActIdle, t, in hr);
            logic.TickPace(t);
            Check(logic.HeldNow == 1 && paceBody.Started.Contains(82), "precondition: fighter 82 is not held by his pause");
            SetHealth(held.Agent, 60f, 100f);
            // a man whose last swing just ended: his step back and his pause are still QUEUED (no tick yet)
            var queued = EmptyRecruit(86, ref t);
            Check(queued.StepBack != null && queued.StepBack.Pending && queued.Pace != null && queued.Pace.Pending, "precondition: fighter 86 has nothing queued");
            // you, your countdown running
            var me = logic.Track(FakeAgent(80))!;
            logic.SmokePlayer = me.Agent;
            for (int i = 0; i < 3; i++) Swing(me, ref t);
            Check(logic.PlayerTimer.Running && Near(me.Fraction, 0.4), "precondition: your countdown is not running");
            // one man who will stand aside, and the boss's side - spawned fresh in the intro
            var aside = logic.Track(FakeAgent(83))!;
            for (int i = 0; i < 3; i++) Swing(aside, ref t);
            var boss = logic.Track(FakeAgent(84))!;
            var bossMan = logic.Track(FakeAgent(85))!;
            Check(boss.Fraction == 1 && bossMan.Fraction == 1, "precondition: the boss's side is not fresh at spawn");

            var sides = new Dictionary<int, HideoutSide>
            {
                [80] = HideoutSide.Player, [81] = HideoutSide.Player, [82] = HideoutSide.Player, [86] = HideoutSide.Player,
                [83] = HideoutSide.Aside, [84] = HideoutSide.Boss, [85] = HideoutSide.Boss,
            };
            logic.HideoutSideOf = st => sides.TryGetValue(st.AgentIndex, out var s) ? s : HideoutSide.Gone;

            // --- the intro, then another objective (nothing), then the BATTLE's objective
            logic.NoteBossIntro(t);
            LogHas("[athletics] hideout: the boss intro began at " + t.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s (the cutscene)");
            double intro = t;
            t += 20;
            logic.ObserveObjective(new SmokeObjective("hideout_mission_clear_the_main_camp_objective", "{=x}Clear the camp"), t);
            logic.ObserveObjective(null, t);
            Check(!logic.HideoutStats.FightSeen && Near(me.Fraction, 0.4), "another objective started the fresh start");
            int stepReleased = stepBody.Released.Count, paceReleased = paceBody.Released.Count, paceStarted = paceBody.Started.Count, stepStarted = stepBody.Started.Count;
            logic.ObserveObjective(BossObjective(duel: false), t);
            var h = logic.HideoutStats;
            Check(h.FightSeen && h.Kind == BossFightKind.Battle, "the battle's objective was not seen as the boss fight (battle)");
            // everyone of the player's side at the top his wounds allow, not exhausted, full speed asked
            Check(me.Fraction == 1 && stepper.Fraction == 1 && queued.Fraction == 1 && !stepper.Exhausted && !queued.Exhausted,
                "battle: the player's side was not refilled: you " + me.Fraction + ", 81 " + stepper.Fraction + ", 86 " + queued.Fraction);
            Check(Near(held.Fraction, 0.6) && Near(held.Health, 0.6), "battle: the wounded man was not refilled to his cap (60%): " + held.Fraction);
            Check(me.SpeedMultiplier == 1f && me.RunSpeedMultiplier == 1f && me.SpeedDirty && stepper.SpeedMultiplier == 1f
                  && Near(held.SpeedMultiplier, 0.2f + 0.8f * 0.8f) && Near(held.RunSpeedMultiplier, 0.7f + 0.3f * 0.8f),
                "battle: the speeds were not re-targeted to the new fill");
            Check(aside.Fraction < 1 && Near(aside.Fraction, 0.4), "battle: the man standing aside was refilled");
            // everything that ran on them ended - each through its own path
            Check(logic.SteppingNow == 0 && stepBody.Released.Count == stepReleased + 1 && logic.StepStats.Ended(StepBackEnd.FreshStart) == 1,
                "battle: the step back was not released (fresh start)");
            Check(logic.HeldNow == 0 && paceBody.Released.Count == paceReleased + 1 && logic.RateStats.Ended(PaceEnd.FreshStart) == 1,
                "battle: the AI pause was not lifted (fresh start)");
            Check(!logic.PlayerTimer.Holding && logic.RateStats.PlayerEnded(PlayerTimerEnd.FreshStart) == 1, "battle: your pause was not released");
            Check(!queued.StepBack!.Pending && !queued.Pace!.Pending && !stepper.Pace!.Deferred, "battle: a queued step back / pause survived the fresh start");
            logic.TickStepBacks(t);
            logic.TickPace(t);
            logic.TickPlayerTimer(t);
            Check(logic.SteppingNow == 0 && logic.HeldNow == 0 && stepBody.Started.Count == stepStarted && paceBody.Started.Count == paceStarted,
                "battle: the next tick still started a queued step back or pause");
            Check(h.PlayerSide == 4 && h.Refilled == 4 && h.AlreadyFull == 0 && h.Capped == 1 && h.WereEmpty == 2 && h.BossSide == 2 && h.BossSideFresh && h.Aside == 1
                  && h.StepBacksReleased == 1 && h.StepBacksDropped == 1 && h.PausesReleased == 1 && h.PausesDropped == 2 && h.YourPauseReleased,
                "battle: the counts are off");
            LogHas("[athletics] hideout boss fight (battle): refilled 4 of the player's side (you 20.0 → 50.0 of 50) at 9");
            LogHas(" s - you and your men still standing; 4 on the player's side, 0 already full, 1 held below full by their wounds (to the health they have left), 2 were empty; "
                   + "+150.0 Athletics in all; released: your attack pause yes, AI pauses 1 (+2 queued), step backs 1 (+1 queued); "
                   + "the boss's side: 2 fighters, all fresh (at full - spawned for this fight); standing aside: 1 (not refilled)");
            LogHas("[athletics] YOU: your attack pause released at ");
            LogHas(" - a fresh start (the hideout boss fight began): attack at once");

            // the objective seen again (vanilla never does): ignored - one fresh start per mission
            Swing(me, ref t);
            logic.ObserveObjective(BossObjective(duel: false), t);
            Check(Near(me.Fraction, 0.8) && h.SeenAgain == 1, "a second boss objective refilled again");
            LogHas("[athletics] hideout: the boss objective came back at ");

            // the summary names the phase and the refill; the fresh-start ends appear in their own lines
            logic.WriteHideoutSummary();
            LogHas("[summary] hideout boss phase (HideoutMissionController): the boss intro at " + intro.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                   + " s, the fight began at " + (intro + 20).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s as a BATTLE (men to men) - a fresh start: 4 of the player's side refilled "
                   + "(4 on it, 0 already full, 1 held by wounds; you 20.0 → 50.0 of 50); the boss's side: 2 fighters, all fresh (at full - spawned for this fight); "
                   + "the boss objective came back 1x (ignored - one fresh start per mission)");
            var stepLines = logic.StepStats.SummaryLines(StepBackRules.From(S));
            Check(stepLines.Exists(l => l.Contains("a fresh start (hideout boss fight) 1")), "the step-back summary does not name the fresh start");
            var rateLines = logic.RateStats.SummaryLines(AttackRateRules.From(S), false);
            Check(rateLines.Exists(l => l.StartsWith("attack rate - AI timer ends: ") && l.Contains(", error 0, a fresh start (hideout boss fight) 1 | lifted by us "))
                  && rateLines.Exists(l => l.StartsWith("attack rate - your timer ") && l.EndsWith(", a fresh start (hideout boss fight) 1")),
                "the attack-rate summary does not name the fresh starts");

            // --- the DUEL: your men stand aside (the game moves them to Team.Invalid) - only you refill
            ResetHideout("HideoutAmbushMissionController");
            for (int i = 0; i < 2; i++) Swing(me, ref t);
            for (int i = 0; i < 3; i++) Swing(stepper, ref t);
            double stepperBefore = stepper.Fraction;
            sides[81] = sides[82] = sides[86] = sides[85] = HideoutSide.Aside;
            logic.ObserveObjective(BossObjective(duel: true), t);
            h = logic.HideoutStats;
            Check(h.Kind == BossFightKind.Duel && me.Fraction == 1 && Near(stepper.Fraction, stepperBefore), "duel: not you alone refilled");
            LogHas("[athletics] hideout boss fight (duel): refilled 1 of the player's side (you 20.0 → 50.0 of 50) at ");
            LogHas(" s - in a duel only you: your men stand aside; 1 on the player's side, ");
            LogHas("the boss's side: 1 fighter, all fresh (at full - spawned for this fight); standing aside: 5 (not refilled)");
            logic.WriteHideoutSummary();
            LogHas("[summary] hideout boss phase (HideoutAmbushMissionController): (the intro was not seen - the refill does not need it) the fight began at ");
            LogHas(" as a DUEL - a fresh start: 1 of the player's side refilled");

            // --- switched off: the moment is logged, nobody refilled (live)
            ResetHideout();
            Swing(me, ref t);
            S.Set(SettingsSchema.HideoutBossFightRefill, false, SettingSources.Mcm);
            logic.ObserveObjective(BossObjective(duel: false), t);
            Check(Near(me.Fraction, 0.8) && logic.HideoutStats.OffBecause == "HideoutBossFightRefill", "HideoutBossFightRefill off: somebody was refilled");
            LogHas("[athletics] hideout boss fight (battle) at ");
            LogHas(": nobody refilled - HideoutBossFightRefill is off (the player's side: 1, you 40.0 of 50)");
            S.Set(SettingsSchema.HideoutBossFightRefill, true, SettingSources.Mcm);

            // --- fail safe: the side reads throw → ONE [error], nothing refilled, the summary says so
            ResetHideout();
            logic.HideoutSideOf = _ => throw new InvalidOperationException("smoke: the teams could not be read");
            int errorsBefore = Occurrences(LogText, "[error] hideout.refill: ");
            logic.ObserveObjective(BossObjective(duel: true), t);
            Check(Near(me.Fraction, 0.8) && logic.HideoutStats.FailedAt == "hideout.refill", "a failing side read still refilled");
            LogHas("[athletics] hideout boss fight (duel) at ");
            LogHas(": the refill FAILED (hideout.refill) - nothing refilled, the fight goes on as it was (tell Claude)");
            logic.WriteHideoutSummary();
            LogHas("as a DUEL - the refill FAILED (hideout.refill): nothing refilled - tell Claude");
            ResetHideout();
            logic.ObserveObjective(BossObjective(duel: true), t); // the same failure in the next hideout: counted, not logged again
            Check(Occurrences(LogText, "[error] hideout.refill: ") - errorsBefore == 1, "the refill's failure was not logged exactly once");

            // --- the intro played but the fight's start was never seen: the summary says so plainly
            ResetHideout("HideoutMissionController");
            logic.NoteBossIntro(t);
            logic.WriteHideoutSummary();
            LogHas("[summary] hideout boss phase (HideoutMissionController): the boss intro played at ");
            LogHas(" s, but the start of the boss fight was NEVER SEEN - if you fought the boss (duel or battle), the refill hook never fired: tell Claude");
            // not a hideout, no boss fight: no line at all
            ResetHideout(null);
            int summaries = Occurrences(LogText, "[summary] hideout boss phase");
            logic.WriteHideoutSummary();
            Check(Occurrences(LogText, "[summary] hideout boss phase") == summaries, "a mission that is no hideout wrote a hideout line");

            // leave things as the next steps expect them: no stand-ins, no mission running, the file's values back
            logic.HideoutSideOf = null;
            logic.SmokePlayer = null;
            ResetHideout(null);
            SetStatic(typeof(AthleticsLogic), "_current", null);
            ConfigStore.Reload("after the hideout smoke");
        }
    }
}

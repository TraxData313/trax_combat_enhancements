using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;
using TraxCombat.Models;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Step 43 (Anton, 2026-10-17): CALM AND FEVER through the real stat decorator and the real logic - the AI's two melee attack-decision
    /// chances ÷ AiCalmFactor, a new charge / advance order arming a fever of current points ÷ 10 s at x3 the calm values, the burn from the
    /// first contact (x1/0.7 with a shield in hand, read live), the fade in 10 steps back to calm, the fever sprint (AI_NOTES "Step 43").
    /// </summary>
    internal static partial class Program
    {
        /// <summary>The older steps run with the calm and the fever OFF (they count the game's AI values and the recomputes).</summary>
        private static void FeverOff()
        {
            S.Set(SettingsSchema.AiCalmFactor, 1.0, SettingSources.File);
            S.Set(SettingsSchema.FeverEnabled, false, SettingSources.File);
        }

        private static void FeverDefaults()
        {
            S.Set(SettingsSchema.AiCalmFactor, 3.0, SettingSources.File);
            S.Set(SettingsSchema.FeverEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.FeverPointsPerSecond, 10.0, SettingSources.File);
            S.Set(SettingsSchema.FeverAggressionMultiplier, 3.0, SettingSources.File);
            S.Set(SettingsSchema.FeverContactMetres, 5.0, SettingSources.File);
            S.Set(SettingsSchema.FeverShieldDurationMultiplier, 0.7, SettingSources.File);
            S.Set(SettingsSchema.FeverSprintToClose, true, SettingSources.File);
            S.Set(SettingsSchema.FeverSprintMinMetres, 3.0, SettingSources.File);
            S.Set(SettingsSchema.FeverSprintMaxMetres, 12.0, SettingSources.File);
            S.Set(SettingsSchema.FeverSprintCheckSeconds, 1.0, SettingSources.File);
            S.Set(SettingsSchema.FeverSprintChancePercent, 50, SettingSources.File);
        }

        private static AthleticsLogic NewFeverLogic(FakeSprintBody body, FakeWeaponFacts facts, double roll)
        {
            var logic = NewSprintLogic(body);
            logic.WeaponFacts = facts;
            logic.FeverDice = new FixedDice(roll);
            return logic;
        }

        /// <summary>Every 0.25 s from <paramref name="from"/> (exclusive) to <paramref name="to"/>: the fever's looks (each man once).</summary>
        private static double Looks(AthleticsLogic logic, double from, double to)
        {
            double t = from;
            while (t + 0.25 <= to + 1e-9)
            {
                t += 0.25;
                logic.TickFever(t);
            }
            return t;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FeverThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                SprintDefaults();
                FeverDefaults();
                S.Set(SettingsSchema.SprintAiEnabled, false, SettingSources.File); // part 1: the order poll serves the fever alone
                S.Set(SettingsSchema.SprintRefillSeconds, 120, SettingSources.File); // the store barely refills: the reserve check stays a reserve check
                var body = new FakeSprintBody();
                var facts = new FakeWeaponFacts();
                var logic = NewFeverLogic(body, facts, 0.1);
                LogHas("[fever] mission start: calm: AI men on foot decide to attack 3.00x less often (AiCalmFactor - the game's attack and riposte chances ÷ 3.00, e.g. a charge's attack chance 0.144 → 0.048)");
                var baseModel = new FreshStatsModel();
                var top = new TraxAgentStatModel(new AgentStatCalculateModel[] { baseModel });
                top.Initialize(baseModel);
                double t = 7000;

                // ---- CALM: an AI man in a formation on hold; you; a rider; a man in no formation (a tournament)
                var man = logic.Track(FakeAgent(80))!;
                man.AthleticsSkill = 100; // pool 100, full
                body.Slots[80] = 0;
                var me = logic.Track(FakeAgent(81))!;
                me.AthleticsSkill = 100;
                body.Slots[81] = 0;
                logic.SmokePlayer = me.Agent;
                var rider = logic.Track(FakeAgent(82))!;
                rider.AthleticsSkill = 100;
                body.Slots[82] = 0;
                body.Mounted.Add(82);
                Mount(rider, 182);
                var duelist = logic.Track(FakeAgent(83))!;
                duelist.AthleticsSkill = 100;
                body.Orders[0] = BraceOrderKind.Stop;
                logic.TickSprint(0.1f, t, Rules);                    // the first look at the orders only records
                t = Looks(logic, t, t + 0.25);
                Check(Near(man.DecisionFactor, 1f / 3f) && man.SpeedDirty, "calm: the AI man's factor is not 1/3 (or no recompute asked): " + man.DecisionFactor);
                Check(me.DecisionFactor == 1f, "calm: YOUR factor is not 1: " + me.DecisionFactor);
                Check(Near(duelist.DecisionFactor, 1f / 3f), "calm: a man in no formation (a tournament) is not calm: " + duelist.DecisionFactor);
                var p = man.Agent.AgentDrivenProperties;
                top.UpdateAgentStats(man.Agent, p);
                Check(Near(p.AIAttackOnDecideChance, 0.048f) && Near(p.AIAttackOnParryChance, 0.08f / 3f), "calm: the decorator did not write 0.144 → 0.048 and 0.080 → 0.027: "
                    + p.AIAttackOnDecideChance + " / " + p.AIAttackOnParryChance);
                Check(Near(p.AiShootFreq, 0.65f) && Near(p.AiWaitBeforeShootFactor, 0.6f) && Near(p.AIDecideOnAttackChance, 0.5f), "calm: a value that is not the two melee chances moved");
                top.UpdateAgentStats(man.Agent, p);
                Check(Near(p.AIAttackOnDecideChance, 0.048f), "calm: compounded over two recomputes: " + p.AIAttackOnDecideChance);
                LogHas("[fever] first decision values written this mission: (agent 80) (calm, x0.33) - attack chance 0.144 → 0.048, riposte chance 0.080 → 0.027");
                var pm = me.Agent.AgentDrivenProperties;
                top.UpdateAgentStats(me.Agent, pm);
                Check(Near(pm.AIAttackOnDecideChance, 0.144f), "calm: YOUR values were touched");
                rider.DecisionFactor = 1f / 3f;                      // even with a factor on his record, a rider keeps the game's values
                var pr = rider.Agent.AgentDrivenProperties;
                top.UpdateAgentStats(rider.Agent, pr);
                Check(Near(pr.AIAttackOnDecideChance, 0.144f), "calm: a RIDER's values were touched: " + pr.AIAttackOnDecideChance);

                // ---- a CHARGE (the AI sprint off: the poll runs for the fever alone): armed at the peak, nothing burns before contact
                body.Orders[0] = BraceOrderKind.Charge;
                t += 0.25;
                logic.TickSprint(0.1f, t, Rules);
                var fv = man.Fever;
                Check(fv.Phase == FeverPhase.Armed && Near(fv.Full, 10.0) && fv.Order == SprintOrder.Charge && !fv.ByPlayer, "charge: the man's fever not armed with 100 / 10 = 10 s: "
                    + fv.Phase + " " + fv.Full);
                Check(!me.Fever.Running && !rider.Fever.Running && !duelist.Fever.Running, "charge: you, the rider or the man in no formation got a fever");
                Check(logic.FeverStats.NotArmedPlayer == 1 && logic.FeverStats.NotArmedMounted == 1, "charge: the not-armed counts (you 1, rider 1) are off");
                Check(man.DecisionFactor == 1f && man.SpeedDirty, "armed: the factor is not the peak 3 / 3 = 1: " + man.DecisionFactor);
                top.UpdateAgentStats(man.Agent, p);
                Check(Near(p.AIAttackOnDecideChance, 0.144f) && Near(p.AIAttackOnParryChance, 0.08f), "armed: the peak is not the game's own values: " + p.AIAttackOnDecideChance);
                LogHas("[fever] first fever armed this battle: (agent 80) at ");
                LogHas(" s on a new charge order (the AI general's, Charge) - 100 Athletics points / 10.0 = 10.00 s of fever; his attack decisions x1.00 the game's (calm x0.33) until an enemy comes within 5.0 m, then the clock runs");
                t = Looks(logic, t, t + 5);                          // five seconds of walking at the enemy
                Check(fv.Phase == FeverPhase.Armed && Near(fv.Left, 10.0), "armed: the store burned before contact: " + fv.Left);

                // ---- CONTACT: the burn; 3 s without a shield, then the shield taken out: x1/0.7
                body.EnemyAt[80] = 2f;
                double contactAt = t + 0.25;
                t = Looks(logic, t, contactAt);
                Check(fv.Phase == FeverPhase.Burning && logic.FeverStats.BurnsStarted == 1, "contact: the burn did not start");
                LogHas("[fever] first fever burning this battle: (agent 80) met an enemy within 5.0 m at ");
                t = Looks(logic, t, t + 3);
                Check(Near(fv.Left, 7.0), "burning 3 s without a shield: not 7 s left: " + fv.Left);
                // mid-fade: share 0.7 → x(1 + 2 x 0.7) / 3 = 0.8 of the game's
                Check(Near(man.DecisionFactor, 0.8f), "mid-fade: the factor at 7 of 10 s is not 0.8: " + man.DecisionFactor);
                top.UpdateAgentStats(man.Agent, p);
                Check(Near(p.AIAttackOnDecideChance, 0.1152f), "mid-fade: the attack chance is not 0.144 x 0.8: " + p.AIAttackOnDecideChance);
                facts.Shield.Add(80);                                // the shield off his back
                double shieldFrom = t;
                int guard = 0;
                while (fv.Running && guard++ < 200) t = Looks(logic, t, t + 0.25);
                double shieldBurn = t - shieldFrom;
                Check(!fv.Running && Math.Abs(shieldBurn - 4.9) <= 0.3, "the shield's burn: 7 s of store should go in 7 x 0.7 = 4.9 s, took " + shieldBurn);
                Check(logic.FeverStats.Ended(FeverEnd.Faded) == 1 && Near(man.DecisionFactor, 1f / 3f), "faded: not counted, or the factor not back to calm: " + man.DecisionFactor);
                Check(fv.Recomputes == FeverMath.FadeSteps + 1, "the fade's recomputes per fever: " + fv.Recomputes + " (expected the arming + 9 steps + the end = 11)");
                top.UpdateAgentStats(man.Agent, p);
                Check(Near(p.AIAttackOnDecideChance, 0.048f), "empty: the attack chance is not calm again: " + p.AIAttackOnDecideChance);
                LogHas("[fever] first fever faded this battle: (agent 80) at ");
                LogHas(" s with a shield in hand, ");

                // ---- the proof's counts: attacks in contact by state
                logic.NoteFeverAttack(man, false);                   // calm, in contact
                Check(logic.FeverStats.Attacks(false) == 1, "a calm attack in contact was not counted");
                logic.NoteFeverAttack(man, true);                    // a rider's swing: never
                Check(logic.FeverStats.Attacks(false) == 1, "a mounted attack was counted");
                Check(logic.FeverStats.ContactSeconds(true) > 7 && logic.FeverStats.ContactSeconds(false) >= 0, "the feverish time in contact is not ~8 s: " + logic.FeverStats.ContactSeconds(true));

                // ---- the FEVER SPRINT: an advance on a second formation, the enemy 8 m away (armed, full chance 50%, the dice 0.1)
                var runner = logic.Track(FakeAgent(84))!;
                runner.AthleticsSkill = 100;                          // sprint store 100 / 20 = 5 s
                body.Slots[84] = 1;
                body.Orders[1] = BraceOrderKind.Stop;
                body.EnemyAt[84] = 8f;
                t += 0.25;
                logic.TickSprint(0.1f, t, Rules);                    // slot 1 first seen
                body.Orders[1] = BraceOrderKind.AiAdvance;
                t += 0.25;
                logic.TickSprint(0.1f, t, Rules);
                var rf = runner.Fever;
                Check(rf.Phase == FeverPhase.Armed && rf.Order == SprintOrder.Advance && !runner.Sprint.Active, "advance: the runner not armed (or sprinting with the AI sprint off)");
                int sprintOff = logic.FeverStats.Refused(FeverSprintRefusal.SprintOff);
                t = Looks(logic, t, t + 0.25);
                Check(logic.FeverStats.Refused(FeverSprintRefusal.SprintOff) == sprintOff + 1, "SprintAiEnabled off: the fever sprint was not refused 'sprint off'");
                S.Set(SettingsSchema.SprintAiEnabled, true, SettingSources.Mcm);
                t = Looks(logic, t, t + 1);
                Check(runner.Sprint.Active && Near(runner.Sprint.Length, 2.5) && logic.FeverStats.SprintsStarted == 1 && logic.FeverStats.SprintsInBand(0) == 1,
                    "the fever sprint did not start for the 2.5 s above the advance's 50% reserve (armed = the 100-67% band): " + runner.Sprint.Active + " " + runner.Sprint.Length);
                LogHas("[fever] first fever sprint this battle: (agent 84) at ");
                LogHas(" - an enemy 3.0-12.0 m away, fever 100% left → chance 50% (FeverSprintChancePercent 50 x the fever left); sprints 2.50 s - what he held above the advance reserve (50%)");
                t = Looks(logic, t, t + 1);
                Check(logic.FeverStats.Refused(FeverSprintRefusal.Sprinting) == 1, "a check while sprinting was not refused 'sprinting already'");
                // the sprint runs out (2.5 s of moving): the store is at the reserve now
                for (int k = 0; k < 40; k++) logic.TickSprint(0.1f, t + k * 0.1, Rules);
                t += 4;
                Check(!runner.Sprint.Active, "the fever sprint did not end");
                int checks = logic.FeverStats.SprintChecks;
                t = Looks(logic, t, t + 3);
                Check(logic.FeverStats.SprintChecks == checks + 3, "the roll cadence is not one a second: " + (logic.FeverStats.SprintChecks - checks) + " checks in 3 s");
                Check(logic.FeverStats.Refused(FeverSprintRefusal.ReserveKept) >= 1 && logic.FeverStats.SprintsStarted == 1, "at the reserve a second sprint started (the reserve must cap it)");
                // the enemy in melee / out of range, a missed roll - with something above the reserve again (the reserve to 0, live)
                S.Set(SettingsSchema.SprintAiAdvanceKeepPercent, 0, SettingSources.Mcm);
                body.EnemyAt[84] = 2.5f;                            // within 5 m: his fever starts burning too
                t = Looks(logic, t, t + 1);
                Check(logic.FeverStats.Refused(FeverSprintRefusal.InMelee) == 1 && rf.Phase == FeverPhase.Burning, "an enemy at 2.5 m was not 'in melee' (or no burn)");
                body.EnemyAt[84] = 20f;
                t = Looks(logic, t, t + 1);
                Check(logic.FeverStats.Refused(FeverSprintRefusal.NoEnemyInRange) == 1, "an enemy at 20 m was not 'no enemy in range'");
                body.EnemyAt[84] = 8f;
                logic.FeverDice = new FixedDice(0.9);
                t = Looks(logic, t, t + 1);
                Check(logic.FeverStats.Refused(FeverSprintRefusal.MissedRoll) == 1 && !runner.Sprint.Active, "a 0.9 roll against a fading chance started a sprint");
                S.Set(SettingsSchema.SprintAiAdvanceKeepPercent, 50, SettingSources.Mcm);

                // ---- re-arming: the charge after the advance (a fresh 10 s is larger than what is left - re-armed to the peak), then a hold ends it
                body.Orders[1] = BraceOrderKind.Charge;
                t += 0.25;
                logic.TickSprint(0.1f, t, Rules);
                Check(rf.Running && rf.Order == SprintOrder.Charge && logic.FeverStats.Rearmed == 1 && Near(rf.Left, 10.0) && rf.Phase == FeverPhase.Burning,
                    "charge after the advance: not re-armed to a fresh 10 s (still burning) with the charge's order: " + rf.Left);
                body.Orders[1] = BraceOrderKind.ChargeToTarget;
                t += 0.25;
                logic.TickSprint(0.1f, t, Rules);
                Check(logic.FeverStats.Repeats == 1, "a charge to a target after a charge was not a repeat");
                body.Orders[1] = BraceOrderKind.Stop;
                t += 0.25;
                logic.TickSprint(0.1f, t, Rules);
                Check(!rf.Running && logic.FeverStats.Ended(FeverEnd.OrderEnded) == 1, "a hold did not end the burning fever (the general called them back)");

                // ---- live settings: AiCalmFactor 1 = the game's values, back to 3; a burning fever ended by FeverEnabled off
                S.Set(SettingsSchema.AiCalmFactor, 1.0, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                LogHas("[fever] settings changed mid-mission at ");
                Check(man.DecisionFactor == 1f, "AiCalmFactor 1: the factor is not 1");
                top.UpdateAgentStats(man.Agent, p);
                Check(Near(p.AIAttackOnDecideChance, 0.144f), "AiCalmFactor 1: not the game's value");
                S.Set(SettingsSchema.AiCalmFactor, 3.0, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                Check(Near(man.DecisionFactor, 1f / 3f), "AiCalmFactor 3 again: not calm");
                body.Orders[0] = BraceOrderKind.Stop;
                t += 0.25;
                logic.TickSprint(0.1f, t, Rules);
                body.Orders[0] = BraceOrderKind.Advance;
                t += 0.25;
                logic.TickSprint(0.1f, t, Rules);
                t = Looks(logic, t, t + 1);
                Check(fv.Phase == FeverPhase.Burning, "a second fever (the enemy still at 2 m) is not burning");
                S.Set(SettingsSchema.FeverEnabled, false, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                logic.TickFever(t + 0.1);
                Check(!fv.Running && logic.FeversRunning == 0 && Near(man.DecisionFactor, 1f / 3f), "FeverEnabled off: the burning fever did not end at once (or not calm)");
                LogHas("[fever] FeverEnabled switched off at ");
                S.Set(SettingsSchema.FeverEnabled, true, SettingSources.Mcm);

                // ---- the summary
                logic.WriteFeverSummary();
                LogHas("[summary] fever settings: calm: AI men on foot decide to attack 3.00x less often");
                LogHas("[summary] fever armed: 3 (charge 1 - by you 0 / by the AI general 1; advance 2 - by you 0 / by the AI general 2)");
                LogHas("[summary] fever contact: 3 of 3 fevers met an enemy within 5.0 m");
                LogHas("[summary] fever burn: faded 1; burn length with a shield in hand (most of it) 1, mean ");
                LogHas("[summary] fever fade: ");
                LogHas("[summary] fever sprints: started 1 (fever left 100-67%: 1, 67-33%: 0, 33-0%: 0) in ");
                LogHas("[summary] fever proof: AI melee attacks per man-minute in contact (an enemy within 5.0 m, on foot): calm ");
                LogHas("[summary] fever values written (AI on foot): attack chance calm 0.144 → 0.048 (x0.33, ");
                logic.SmokePlayer = null;
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FeverCostStaysFlat()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                SprintDefaults();
                FeverDefaults();
                var body = new FakeSprintBody();
                var facts = new FakeWeaponFacts();
                var logic = NewFeverLogic(body, facts, 0.99);
                const int men = 1000;
                double t0 = 15000;
                var list = new List<TrackedAgent>(men);
                for (int i = 0; i < men; i++)
                {
                    var st = logic.Track(FakeAgent(4000 + i))!;
                    st.AthleticsSkill = 100;
                    body.Slots[4000 + i] = i % 8;
                    if (i % 2 == 0) body.EnemyAt[4000 + i] = 2f;       // half in contact (burning), half 8 m off (armed, rolling for a sprint)
                    else body.EnemyAt[4000 + i] = 8f;
                    if (i % 3 == 0) facts.Shield.Add(4000 + i);
                    list.Add(st);
                }
                for (int s = 0; s < 8; s++) body.Orders[s] = BraceOrderKind.Stop;
                logic.TickSprint(0.1f, t0, Rules);
                for (int s = 0; s < 8; s++) body.Orders[s] = BraceOrderKind.Charge;
                logic.TickSprint(0.1f, t0 + 0.25, Rules);
                Check(logic.FeversRunning == men, "cost: not every man armed: " + logic.FeversRunning);
                logic.TickFever(t0 + 0.25);
                const int ticks = 300;
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var sw = Stopwatch.StartNew();
                for (int k = 1; k <= ticks; k++) logic.TickFever(t0 + 0.25 + k / 60.0);  // five seconds at 60 fps
                sw.Stop();
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double msPerTick = sw.Elapsed.TotalMilliseconds / ticks;
                int dirty = 0;
                foreach (var st in list) if (st.SpeedDirty) dirty++;
                Console.WriteLine("        step 43 cost: " + men + " men in a fever (half burning) - the fever's tick " + msPerTick.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                                  + " ms a tick, " + allocated + " bytes allocated over " + ticks + " ticks; " + logic.FeverStats.SprintChecks + " sprint checks, " + dirty + " men waiting for a recompute");
                Check(msPerTick < 0.5, "cost: the fever's tick over " + men + " men took " + msPerTick + " ms (budget 0.5 ms)");
                Check(allocated < 256 * 1024, "cost: the fever's tick allocates - " + allocated + " bytes over " + ticks + " ticks");
                Check(logic.FeverStats.BurnsStarted == men / 2, "cost: not every man in contact burns: " + logic.FeverStats.BurnsStarted);
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }
    }
}

using System;
using System.Runtime.CompilerServices;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;
using TraxCombat.Models;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 64 - THE WALL'S BLOCKS COUNT, THE CHARGE HITS AT SPEED (AI_NOTES "Step 64"). Anton's looter battles of
    /// 2026-10-19: ~85% of an AI shieldman's blocks read WRONG side even fresh, nobody bounced off the wall. Through the real logic's pieces of
    /// OnMeleeHit (its tracked-attacker part makes native reads a stand-in agent cannot answer): the verdict, the impact model's event, the
    /// guard lines, the summary.
    /// </summary>
    internal static partial class Program
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ShieldFrontArc()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                Step63On();
                Step56BlockModel();
                S.Set(SettingsSchema.ShieldFrontArcIsCorrect, SettingsSchema.ShieldFrontArcIsCorrect.Default, SettingSources.File);
                S.Set(SettingsSchema.ShieldFrontArcDegrees, SettingsSchema.ShieldFrontArcDegrees.Default, SettingSources.File);
                Check(S.ShieldFrontArcIsCorrect && S.ShieldFrontArcDegrees == 60, "the front arc is not on at 60° by default: " + S.ShieldFrontArcIsCorrect + " / " + S.ShieldFrontArcDegrees);
                var inputs = new FakeImpactInputs();
                var body = new FakeImpactBody();
                var facts = new FakeWeaponFacts();
                var logic = NewPaceLogic(new FakePaceBody(), facts);
                logic.ImpactBody = body;
                logic.ImpactInputs = inputs;
                var dice = new ScriptDice();
                logic.ImpactDice = dice;
                double t = 64000;
                logic.TickImpact(t);
                LogHas("[guard] mission start: shield front arc: on - a shield block whose attacker stands within 60° of the blocker's facing counts as a RIGHT-side block");

                TrackedAgent Man(int index, double bar, int skill)
                {
                    var m = logic.Track(FakeAgent(index))!;
                    m.AthleticsSkill = skill;
                    SetBar(m, bar);
                    return m;
                }
                var legionary = Man(6400, 1.0, 130);
                inputs.Guards[6400] = (4.0, 130);
                inputs.Shields.Add(6400);
                facts.Shield.Add(6400);
                var looter = Man(6401, 1.0, 30);
                inputs.Weapons[6401] = (1.5, 85, ImpactDamageType.Blunt, 30);
                var arc = logic.ShieldArcStatsForSmoke;
                var guard = logic.TiredBlockStatsForSmoke;

                // ---- 1. the game says WRONG side, the looter 20° off the legionary's facing: a right-side block for every system of ours
                inputs.BlockAngles[6400] = 20;
                var wrong = WrongBlock();
                Check(logic.BlockKindOf(looter.Agent, legionary.Agent, in wrong) == BlockKind.ShieldRightSide, "a wrong-side block from 20° is not counted right");
                logic.ShieldArcHitTaken(looter.Agent, legionary.Agent, in wrong);
                Check(arc.EngineWrong(false) == 1 && arc.OursRight(false) == 1 && arc.Turned(false) == 1, "the arc's counts: engine wrong " + arc.EngineWrong(false) + ", ours right " + arc.OursRight(false));
                LogHas("[guard] first shield block from the front counted RIGHT this battle (step 64): (agent 6400)'s shield block of (agent 6401) at ");
                LogHas("- the attacker 20° off his facing (within ±60°): the game said WRONG side, the mod counts a right-side block");
                logic.ImpactModelHitTaken(looter.Agent, legionary.Agent, in wrong);
                Check(logic.LastImpact.Event == ImpactEvent.RightBlock, "the impact model did not decide a right-side block: " + logic.LastImpact.Event);
                logic.TiredGuardHit(looter.Agent, legionary.Agent, in wrong);
                Check(guard.WrongSide(0) == 0 && guard.Blows(0) == 1, "the guard lines counted the game's wrong side: wrong " + guard.WrongSide(0) + " of " + guard.Blows(0)); // bin 0 = the peak

                // ---- 2. a CHARGING looter into that block (his sprint ended 0.5 s ago): the reflected charge can throw him back
                looter.SprintEndedAt = t - 0.5;
                dice.Rest = 0.0; // every roll with a chance comes up (the fresh legionary's own rows are 0)
                logic.ImpactModelHitTaken(looter.Agent, legionary.Agent, in wrong);
                dice.Rest = 0.999;
                Check(logic.LastImpact.Event == ImpactEvent.RightBlock && logic.LastImpact.Charging && logic.LastImpact.ThrownBack.Happens,
                    "a charging looter into a front-arc block not thrown back: " + logic.LastImpact.Event + ", charging " + logic.LastImpact.Charging + ", thrown " + logic.LastImpact.ThrownBack.Chance);

                // ---- 2b. an EXHAUSTED legionary (step 30's w 1 - every guard a guess): his front block keeps the game's WRONG side (a tired wall
                // still breaks); at w 0.5 about half of many blows turn right, each blow's verdict the same for every system that asks
                var tired = Man(6403, 0.0, 130);
                inputs.Guards[6403] = (4.0, 130);
                inputs.Shields.Add(6403);
                inputs.BlockAngles[6403] = 10;
                tired.BlockRandomWeight = 1f;
                Check(logic.BlockKindOf(looter.Agent, tired.Agent, in wrong) == BlockKind.ShieldWrongSide, "an exhausted man's front block (w 1) was counted right");
                logic.ShieldArcHitTaken(looter.Agent, tired.Agent, in wrong);
                Check(arc.KeptByTiredness == 1, "the tired block was not counted as kept by tiredness: " + arc.KeptByTiredness);
                tired.BlockRandomWeight = 0.5f;
                int rightHalf = 0;
                for (int i = 0; i < 400; i++)
                {
                    looter.ReleaseSerial = 1000 + i;
                    var k1 = logic.BlockKindOf(looter.Agent, tired.Agent, in wrong);
                    var k2 = logic.BlockKindOf(looter.Agent, tired.Agent, in wrong);
                    Check(k1 == k2, "the same blow's verdict differed between two asks");
                    if (k1 == BlockKind.ShieldRightSide) rightHalf++;
                }
                Check(rightHalf > 160 && rightHalf < 240, "w 0.5: " + rightHalf + " of 400 front blocks right (about half expected)");

                // ---- 3. from 100° (the side): the game's WRONG side stands; the engine's right side stays right from behind
                var flank = Man(6402, 1.0, 130);
                inputs.Guards[6402] = (4.0, 130);
                inputs.Shields.Add(6402);
                inputs.BlockAngles[6402] = 100;
                Check(logic.BlockKindOf(looter.Agent, flank.Agent, in wrong) == BlockKind.ShieldWrongSide, "a wrong-side block from 100° counted right");
                logic.ShieldArcHitTaken(looter.Agent, flank.Agent, in wrong);
                logic.ImpactModelHitTaken(looter.Agent, flank.Agent, in wrong);
                Check(logic.LastImpact.Event == ImpactEvent.WrongBlock, "a side block was not decided wrong-side: " + logic.LastImpact.Event);
                int reads = inputs.AngleReads;
                var right = RightBlock();
                inputs.BlockAngles[6402] = 170;
                Check(logic.BlockKindOf(looter.Agent, flank.Agent, in right) == BlockKind.ShieldRightSide && inputs.AngleReads == reads, "the game's right side read the angle / turned wrong");
                logic.ShieldArcHitTaken(looter.Agent, flank.Agent, in right);
                var parry = Parried();
                Check(logic.BlockKindOf(looter.Agent, flank.Agent, in parry) == BlockKind.WeaponParry && inputs.AngleReads == reads, "a parry was looked at");

                // ---- 4. an untracked attacker (a horse, a stand-in): the game's verdict, no native read
                Check(logic.BlockKindOf(FakeAgent(6499), legionary.Agent, in wrong) == BlockKind.ShieldWrongSide && inputs.AngleReads == reads, "an untracked attacker was looked at");
                logic.ShieldArcHitTaken(FakeAgent(6499), legionary.Agent, in wrong);
                Check(arc.NotLooked == 1, "the untracked attacker's block not counted as not looked at: " + arc.NotLooked);

                // ---- 5. live: the arc to 10° (20° is outside), the switch off, the master switch off - the game's verdict at once
                S.Set(SettingsSchema.ShieldFrontArcDegrees, 10, SettingSources.Mcm);
                Check(logic.BlockKindOf(looter.Agent, legionary.Agent, in wrong) == BlockKind.ShieldWrongSide, "ShieldFrontArcDegrees 10 not live");
                S.Set(SettingsSchema.ShieldFrontArcDegrees, 60, SettingSources.Mcm);
                S.Set(SettingsSchema.ShieldFrontArcIsCorrect, false, SettingSources.Mcm);
                reads = inputs.AngleReads;
                Check(logic.BlockKindOf(looter.Agent, legionary.Agent, in wrong) == BlockKind.ShieldWrongSide && inputs.AngleReads == reads, "the switch off not live (or the angle still read)");
                S.Set(SettingsSchema.ShieldFrontArcIsCorrect, true, SettingSources.Mcm);
                Check(logic.BlockKindOf(looter.Agent, legionary.Agent, in wrong) == BlockKind.ShieldRightSide, "the switch back on not live");

                // ---- 6. the summary line: the game's verdict against ours
                logic.WriteShieldArcSummary();
                LogHas("shield front arc (step 64, on, ±60°): AI men's shield blocks 5: the game right 1 (20%) / WRONG 4 → ours right 2 (40%) / WRONG 3 - 1 wrong-side blocks from the front counted right");
                LogHas("0-15° 1, 15-30° 1 (1), 90-120° 1, not looked at 1");
                LogHas("from the front but kept WRONG by the blocker's tiredness (step 30's guessed share of a tired man's guard) 1");
                Console.WriteLine("        step 64 front arc: a wrong-side block from 20° → right (the impact model's RightBlock; a charging looter thrown back), from 100° the game's; " + arc.SummaryLine(ShieldArcRules.From(S)));
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                ImpactHooks.RestoreStunCap();
                ImpactHooks.KnockDown = null;
                ImpactHooks.BlockStun = null;
                ImpactHooks.PickupLock = null;
                ImpactHooks.ShrugOff = null;
                ImpactModelDefaults();
                AthleticsDefaults();
            }
        }

        /// <summary>
        /// THE AI CHARGE HITS AT SPEED (step 64) through the real logic with the stand-in sprint body: a legionary's big store sprints at the
        /// charge order and runs INTO contact (no end at 3 m), ended by his first attack (the carry's clock from then); a looter's small store
        /// WAITS until an enemy is within 10 m, then sprints into contact and ends when he stands still; a new order drops a wait; the switch off
        /// = step 62 (an end at 3 m); the wait at 0 = a looter sprints at the order; the first lines and the summary's line.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ChargeSprintIntoContact()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                SprintDefaults();
                S.Set(SettingsSchema.SprintMinSeconds, 2.0, SettingSources.File);
                S.Set(SettingsSchema.SprintRefillSeconds, 120, SettingSources.File);
                foreach (var p in new[] { SettingsSchema.SprintChargeIntoContact, SettingsSchema.SprintChargeWaitBelowSeconds, SettingsSchema.SprintChargeWaitMetres })
                    S.Set(p, p.Default, SettingSources.File);
                Check(S.SprintChargeIntoContact && Math.Abs(S.SprintChargeWaitBelowSeconds - 3) < 1e-6 && Math.Abs(S.SprintChargeWaitMetres - 10) < 1e-6,
                    "the step-64 sprint defaults are not on / 3 s / 10 m");
                var body = new FakeSprintBody();
                var logic = NewSprintLogic(body);
                var stats = logic.SprintStats;
                double t = 64500;
                void Tick(float dt = 0.1f)
                {
                    t += dt;
                    logic.TickSprint(dt, t, Rules);
                }
                void Order(int slot, BraceOrderKind kind)
                {
                    body.Orders[slot] = kind;
                    t += 0.3;
                    Tick();
                }
                TrackedAgent Man(int index, int skill, int slot)
                {
                    var m = logic.Track(FakeAgent(index))!;
                    m.AthleticsSkill = skill;
                    body.Slots[index] = slot;
                    body.Speeds[index] = 3.5f;
                    return m;
                }
                var legionary = Man(6410, 130, 5); // 130 points = 6.5 s
                var looter = Man(6411, 30, 5);     // the pool's floor 50 points = 2.5 s
                body.EnemyAt[6410] = 30;
                body.EnemyAt[6411] = 30;
                body.Orders[5] = BraceOrderKind.Stop;
                t += 0.3;
                Tick();

                // ---- 1. the charge order 30 m out: the legionary sprints at once INTO contact, the looter's 2.5 s WAITS
                Order(5, BraceOrderKind.Charge);
                Check(legionary.Sprint.Active && legionary.Sprint.IntoContact && legionary.Sprint.Order == SprintOrder.Charge,
                    "the legionary's charge sprint did not start into contact: active " + legionary.Sprint.Active + ", into contact " + legionary.Sprint.IntoContact);
                Check(!looter.Sprint.Active && looter.ChargeSprintWaiting && stats.ChargeWaited == 1, "the looter's small store did not wait: active " + looter.Sprint.Active);
                LogHas("[sprint] first charge sprint WAITING this battle (step 64): (agent 6411) at ");
                LogHas("his sprint 2.50 s is below 3.0 s (SprintChargeWaitBelowSeconds) and no enemy within 10.0 m");

                // ---- 2. the legionary reaches the enemy: 2 m - no end at 3 m; his first attack ends it, the carry's clock from then
                body.EnemyAt[6410] = 2;
                for (int i = 0; i < 5; i++) Tick();
                Check(legionary.Sprint.Active, "the charge sprint ended at an enemy within 3 m (it should run into contact)");
                CallPrivate(logic, "SprintAttackBegan", legionary, t);
                Check(!legionary.Sprint.Active && Math.Abs(legionary.SprintEndedAt - t) < 1e-9 && stats.Ends(false, SprintEnd.Attacked) == 1,
                    "his first attack did not end the sprint into contact (the carry's clock): ended at " + legionary.SprintEndedAt);
                LogHas("[sprint] first sprint INTO contact ended by an attack this battle (step 64): (agent 6410) at ");

                // ---- 3. the looter: an enemy at 9 m - he sprints now, into contact; he is stopped in the press - ended 0.5 s later
                body.EnemyAt[6411] = 9;
                Tick();
                Tick();
                Tick();
                Check(looter.Sprint.Active && looter.Sprint.IntoContact && !looter.ChargeSprintWaiting && stats.ChargeWaitStarted == 1,
                    "the waiting looter did not sprint at 9 m: active " + looter.Sprint.Active);
                LogHas("[sprint] first WAITING charge sprint started this battle (step 64): (agent 6411) at ");
                body.EnemyAt[6411] = 1.5f;
                Tick();
                Check(looter.Sprint.Active, "the looter's sprint ended at an enemy within 3 m");
                body.Speeds[6411] = 0.2f;
                for (int i = 0; i < 3; i++) Tick();
                Check(looter.Sprint.Active, "the looter's sprint ended before 0.5 s standing still");
                for (int i = 0; i < 4 && looter.Sprint.Active; i++) Tick();
                Check(!looter.Sprint.Active && stats.Ends(false, SprintEnd.Stopped) == 1, "standing still did not end the sprint into contact");
                body.Speeds[6411] = 3.5f;

                // ---- 4. a new order drops a wait
                var other = Man(6412, 30, 6);
                body.EnemyAt[6412] = 30;
                body.Orders[6] = BraceOrderKind.Stop;
                t += 0.3;
                Tick();
                Order(6, BraceOrderKind.Charge);
                Check(other.ChargeSprintWaiting, "precondition: the second looter did not wait");
                Order(6, BraceOrderKind.Stop);
                Check(!other.ChargeSprintWaiting && stats.ChargeWaitDropped == 1, "a new order did not drop the wait: dropped " + stats.ChargeWaitDropped);

                // ---- 5. the switch off (live): a charge sprint ends at 3 m again; the wait at 0: a small store sprints at the order
                S.Set(SettingsSchema.SprintChargeIntoContact, false, SettingSources.Mcm);
                S.Set(SettingsSchema.SprintChargeWaitBelowSeconds, 0.0, SettingSources.Mcm);
                for (int i = 0; i < 200; i++) Tick(); // the stores refill
                body.EnemyAt[6412] = 30;
                Order(6, BraceOrderKind.Charge);
                Check(other.Sprint.Active && !other.Sprint.IntoContact && !other.ChargeSprintWaiting, "the wait at 0 / the switch off: the looter did not sprint at the order, or into contact");
                body.EnemyAt[6412] = 2;
                for (int i = 0; i < 4 && other.Sprint.Active; i++) Tick();
                Check(!other.Sprint.Active && stats.Ends(false, SprintEnd.InMelee) >= 1, "the switch off: the sprint did not end at 3 m");

                var lines = new System.Collections.Generic.List<string>();
                stats.AddSummaryLines(lines, SprintRules.From(S), "LeftControl");
                string line = lines[lines.Count - 1];
                Check(line.Contains("sprints run into contact 2 (ended by his first attack 1, he stopped 1") && line.Contains("charge sprints that WAITED for the enemy 2: started within 10.0 m 1"),
                    "the summary's into-contact line: " + line);
                Console.WriteLine("        step 64 charge sprint: " + line);
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        /// <summary>
        /// THE CHARGE CARRIES TO THE BLOW (step 64) through the real logic and the impact model: the carry is 3 s by default; an attack BEGUN
        /// within it stays a charge until its blow is decided, however long after (a slow swing); that blow spends it; an attack begun after the
        /// carry is no charge; the damage decorator's read (TryGetCharging) agrees; the charges line counts the carried-by-an-attack blow.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ChargeCarriesToTheBlow()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                Step63On();
                Step56BlockModel();
                Check(Math.Abs(S.ImpactChargeCarrySeconds - 3.0) < 1e-6, "ImpactChargeCarrySeconds is not 3 by default: " + S.ImpactChargeCarrySeconds);
                var inputs = new FakeImpactInputs();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = new FakeImpactBody();
                logic.ImpactInputs = inputs;
                logic.ImpactDice = new ScriptDice();
                double t = 64800;
                logic.TickImpact(t);
                var legionary = logic.Track(FakeAgent(6420))!;
                legionary.AthleticsSkill = 130;
                inputs.Guards[6420] = (4.0, 130);
                inputs.Shields.Add(6420);
                var looter = logic.Track(FakeAgent(6421))!;
                looter.AthleticsSkill = 30;
                inputs.Weapons[6421] = (1.5, 85, ImpactDamageType.Blunt, 30);
                var block = RightBlock();

                // within the 3 s carry (2.5 s after the sprint): a charge
                looter.SprintEndedAt = t - 2.5;
                Check(AthleticsLogic.TryGetCharging(looter.Agent), "2.5 s after a sprint is no charge with the 3 s carry");
                // his attack begins now (inside the carry) - its blow comes 1.5 s later, 4 s after the sprint: still a charge, carried by the attack
                CallPrivate(logic, "MarkChargeAttack", looter, t);
                Check(looter.ChargeAttack && logic.ChargeAttacksMarked == 1, "an attack begun inside the carry was not marked a charge");
                logic.TickImpact(t += 1.5);
                Check(AthleticsLogic.TryGetCharging(looter.Agent), "the damage decorator's read lost the attack begun in the charge");
                logic.ImpactModelHitTaken(looter.Agent, legionary.Agent, in block);
                Check(logic.LastImpact.Charging && logic.LastImpact.ChargeShare > 0.999, "the slow swing's blow was not a charge: " + logic.LastImpact.Charging);
                Check(!looter.ChargeAttack && double.IsNaN(looter.SprintEndedAt), "the carried charge was not spent on its blow");
                double thrown = logic.LastImpact.ThrownBack.Chance;
                logic.TickImpact(t += 0.5);
                logic.ImpactModelHitTaken(looter.Agent, legionary.Agent, in block);
                Check(!logic.LastImpact.Charging, "a second blow after the spent charge still charged");

                // an attack begun AFTER the carry: no charge
                looter.SprintEndedAt = t - 3.2;
                CallPrivate(logic, "MarkChargeAttack", looter, t);
                Check(!looter.ChargeAttack && !AthleticsLogic.TryGetCharging(looter.Agent), "an attack begun 3.2 s after the sprint was marked a charge");
                logic.WriteImpactSummary();
                LogHas("of the carried, by an attack begun in the charge (step 64) 1");
                Console.WriteLine("        step 64 carry: an attack begun 2.5 s after the sprint struck at 4 s as a CHARGE into a fresh legionary's right block (thrown back "
                                  + (100 * thrown).ToString("0") + "%), then spent; an attack begun at 3.2 s: none");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                ImpactHooks.RestoreStunCap();
                ImpactHooks.KnockDown = null;
                ImpactHooks.BlockStun = null;
                ImpactHooks.PickupLock = null;
                ImpactHooks.ShrugOff = null;
                ImpactModelDefaults();
                AthleticsDefaults();
            }
        }

        /// <summary>The master-switch step's step-64 part: off = the game's block verdict at once (no angle read), on = the front arc again.</summary>
        private static void ShieldArcFollowsTheMasterSwitch()
        {
            var keep = _logic;
            try
            {
                S.Set(SettingsSchema.ShieldFrontArcIsCorrect, true, SettingSources.File);
                var inputs = new FakeImpactInputs();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactInputs = inputs;
                var a = logic.Track(FakeAgent(6490))!;
                var v = logic.Track(FakeAgent(6491))!;
                inputs.BlockAngles[6491] = 0;
                var wrong = WrongBlock();
                Check(logic.BlockKindOf(a.Agent, v.Agent, in wrong) == BlockKind.ShieldRightSide, "precondition: the front arc did not turn a block right (step 64)");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                int reads = inputs.AngleReads;
                Check(logic.BlockKindOf(a.Agent, v.Agent, in wrong) == BlockKind.ShieldWrongSide && inputs.AngleReads == reads, "mod off: the front arc still turned a block (step 64)");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                Check(logic.BlockKindOf(a.Agent, v.Agent, in wrong) == BlockKind.ShieldRightSide, "mod back on: the front arc did not come back (step 64)");
            }
            finally
            {
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                S.Set(SettingsSchema.ShieldFrontArcIsCorrect, false, SettingSources.File);
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
            }
        }
    }
}

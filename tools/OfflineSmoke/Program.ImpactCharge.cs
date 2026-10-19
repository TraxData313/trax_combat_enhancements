using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;
using TraxCombat.Models;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 62 - MORE MEN FALL, AND THE CHARGE MATTERS (AI_NOTES "Step 62", DESIGN decision 52): through the REAL shrug-off
    /// hook, the REAL damage decorator (its KnockDown seam, its damage roll) and the REAL logic with the stand-in body - THE LOST FALLS (a decided
    /// fall on a blow the game would shrug off refuses the shrug-off and plays, ONE decision per blow; a decided stagger the same; a fall the engine
    /// never plays backed up by our own blow after 0.5 s, once; refused in the water; no backup at 0; a killing blow never decided; the switch off =
    /// the game's shrug-off), TOUGHNESS (the kit read once a man), DIRECTION, a ranged man in melee, THE CHARGE (sprinting, the carry spent by the
    /// first blow, a rout never), THE REFLECTED CHARGE on YOU (thrown back by a shieldman - our blow on you, the red line, its Fall seen), THE
    /// UNCAPPED CRITICAL (an empty man's charge rolls up to x1.5), the summary lines, the master switch and the cost.
    /// </summary>
    internal static partial class Program
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ImpactLostFallsThroughTheLogic()
        {
            var keep = _logic;
            float thresholdKeep = ManagedParameters.GetParameter(ManagedParametersEnum.DamageInterruptAttackThresholdCut);
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                Step62On();
                ManagedParameters.SetParameter(ManagedParametersEnum.DamageInterruptAttackThresholdCut, 5f); // the game's shrug-off threshold
                var inputs = new FakeImpactInputs();
                var body = new FakeImpactBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = body;
                logic.ImpactInputs = inputs;
                var dice = new ScriptDice();
                logic.ImpactDice = dice;
                var model = SpikeDamageModel();
                var game = new CustomAgentApplyDamageModel();
                var falls = logic.ImpactFallStats;
                double t = 62000;
                logic.TickImpact(t);
                LogHas("step 62: a decided fall / stagger refuses the game's shrug-off on, a fall not seen in 0.5 s = our blow;");

                TrackedAgent Man(int index, double bar)
                {
                    var m = logic.Track(FakeAgent(index))!;
                    m.AthleticsSkill = 125; // pool 100
                    SetBar(m, bar);
                    return m;
                }
                var sword = Man(6200, 1.0);
                inputs.Weapons[6200] = (1.2, 92, ImpactDamageType.Cut, 125);
                var light = CleanHitDealing(3); // under the game's threshold 5: the game says "shrugged off"
                // the engine's order in Mission.CreateMeleeBlow: the shrug-off (the real hook), then the KnockDown seam (the real decorator) with the
                // flag as the shrug-off left it, then the same callback's OnMeleeHit
                (bool Kept, bool Down) Blow(TrackedAgent a, TrackedAgent v, in AttackCollisionData cd)
                {
                    var blow = new Blow(a.AgentIndex);
                    bool kept = ImpactHooks.ShrugOffKept(v.Agent, in cd, in blow, game);
                    if (kept) blow.BlowFlag |= BlowFlags.ShrugOff;
                    bool down = model.DecideAgentKnockedDownByBlow(a.Agent, v.Agent, in cd, null!, in blow);
                    logic.ImpactModelHitTaken(a.Agent, v.Agent, in cd);
                    return (kept, down);
                }
                void Tick(double seconds) => TickFor(logic, ref t, seconds);

                // ---- 1. a decided FALL on a blow the game would shrug off: decided at the shrug-off, the shrug-off REFUSED, the KnockDown seam
                // answers the same decision (one decision), the Fall plays
                var empty = Man(6201, 0.0);
                int decisions = logic.ImpactModelStats.Decisions;
                dice.Next(0.0, 0.5); // down, the get-up
                var r1 = Blow(sword, empty, in light);
                Check(!r1.Kept && r1.Down, "a decided fall on a shrugged-off blow: the shrug-off kept " + r1.Kept + ", KnockDown " + r1.Down);
                Check(logic.ImpactModelStats.Decisions == decisions + 1, "the blow decided " + (logic.ImpactModelStats.Decisions - decisions) + " times (one, at the shrug-off)");
                Check(falls.ShrugRefusedForFall == 1 && falls.TakenFromShrugSeam == 1 && falls.Decided[1] == 1, "the falls' counts: refused " + falls.ShrugRefusedForFall + ", taken " + falls.TakenFromShrugSeam + ", decided " + falls.Decided[1]);
                Check(logic.ImpactHitStats.KnockdownOnShrugOff == 0, "a knockdown still answered on a shrugged-off blow");
                t = PlayReaction(logic, body, empty, ImpactActions.Fall, t + 1.0 / 60, 1.5, index: 6201);
                Check(falls.PlayedByGame[1] == 1 && falls.PlayedByBackup[1] == 0 && body.OwnBlows.Count == 0, "the game's Fall not counted as played (or a backup sent): game " + falls.PlayedByGame[1]);

                // ---- 2. a decided fall the engine NEVER plays: our own blow from the tick after 0.5 s - once - and its Fall counted as the backup's
                var lost = Man(6202, 0.0);
                dice.Next(0.0, 0.5);
                Check(Blow(sword, lost, in light).Down, "the second fall not answered");
                Tick(0.45);
                Check(body.OwnBlows.Count == 0, "the backup came before its 0.5 s");
                Tick(0.1);
                Check(body.OwnBlows.Count == 1 && body.OwnBlows[0] == (6202, 6200, SpikeMath.OwnBlowDamage) && falls.BackupsSent == 1,
                    "no backup blow after 0.5 s: " + string.Join(", ", body.OwnBlows));
                LogHas("[impact] live: first BACKUP knockdown (step 62) at ");
                t = PlayReaction(logic, body, lost, ImpactActions.Fall, t, 1.5, index: 6202);
                Check(falls.PlayedByBackup[1] == 1 && body.OwnBlows.Count == 1, "the backup's Fall not counted (or a second backup): backup " + falls.PlayedByBackup[1]);

                // ---- 3. the backup refused before any engine call (in the water) - then not seen
                var wet = Man(6203, 0.0);
                body.Water.Add(6203);
                dice.Next(0.0, 0.5);
                Blow(sword, wet, in light);
                Tick(1.2);
                Check(falls.BackupsRefused == 1 && falls.NotSeen[1] == 1 && body.OwnBlows.Count == 1, "the water's backup: refused " + falls.BackupsRefused + ", not seen " + falls.NotSeen[1]);
                body.Water.Remove(6203);

                // ---- 4. a decided STAGGER on a shrugged-off blow: refused too (his flinch must play); nothing decided: the game's answer, then step 52's
                // tired rule (a fresh man keeps his shrug-off)
                var reel = Man(6204, 0.0);
                dice.Next(0.999, 0.0, 0.5); // no knockdown, the stagger, its length
                var r4 = Blow(sword, reel, in light);
                Check(!r4.Kept && !r4.Down && logic.LastImpact.Stagger.Happens && falls.ShrugRefusedForStagger == 1, "a decided stagger did not refuse the shrug-off");
                var fresh = Man(6205, 1.0);
                var r5 = Blow(sword, fresh, in light);
                Check(r5.Kept && !r5.Down && falls.ShrugDecidedNothing == 1, "a fresh man's shrug-off not kept / nothing not counted");

                // ---- 5. a blow that KILLS is never decided
                inputs.Fatal = true;
                decisions = logic.ImpactModelStats.Decisions;
                var dead = Man(6206, 0.0);
                dice.Next(0.0, 0.5);
                Check(!Blow(sword, dead, in light).Down && logic.ImpactModelStats.Decisions == decisions && falls.Fatal == 1, "a killing blow was decided");
                inputs.Fatal = false;

                // ---- 6. the switch off: the game's shrug-off wins (nothing decided at the shrug-off); no backup at 0
                S.Set(SettingsSchema.ImpactShrugOffYieldsToImpact, false, SettingSources.Mcm);
                decisions = logic.ImpactModelStats.Decisions;
                var blow6 = new Blow(sword.AgentIndex);
                ImpactHooks.ShrugOffKept(Man(6207, 0.4).Agent, in light, in blow6, game);
                Check(logic.ImpactModelStats.Decisions == decisions, "ImpactShrugOffYieldsToImpact off: the shrug-off decided the blow");
                S.Set(SettingsSchema.ImpactShrugOffYieldsToImpact, true, SettingSources.Mcm);
                S.Set(SettingsSchema.ImpactFallBackupSeconds, 0.0, SettingSources.Mcm);
                var nobackup = Man(6208, 0.0);
                dice.Next(0.0, 0.5);
                Blow(sword, nobackup, in light);
                Tick(1.2);
                Check(body.OwnBlows.Count == 1 && falls.NotSeen[1] == 2, "ImpactFallBackupSeconds 0: a backup was sent");
                S.Set(SettingsSchema.ImpactFallBackupSeconds, 0.5, SettingSources.Mcm);

                // ---- 7. a KICK's fall backed up too
                var kicker = Man(6209, 1.0);
                var kicked = Man(6210, 0.0);
                dice.Next(0.0, 0.999, 0.5); // down, no shield drop (no shield anyway), the get-up
                var kick = KickLands();
                kick.InflictedDamage = 2;
                Check(Blow(kicker, kicked, in kick).Down, "the kick's knockdown not answered");
                Tick(0.6);
                Check(body.OwnBlows.Exists(b => b.Victim == 6210) && falls.Decided[0] == 1, "the kick's fall not backed up");

                logic.WriteImpactSummary();
                LogHas("[summary] impact falls (step 62 - every knockdown decided on a landed blow and what became of it; the target: seen = decided): decided ");
                LogHas("→ played by the game 1 (");
                LogHas(", by our backup blow 1 (");
                LogHas("the game's shrug-off REFUSED for a decided fall ");
                LogHas("landed blows not decided because they KILLED 1");
                LogHas("the blow killed him (step 62) 1");
                Console.WriteLine("        step 62 falls: decided " + falls.Total(falls.Decided) + ", played by the game " + falls.Total(falls.PlayedByGame) + ", by the backup " + falls.Total(falls.PlayedByBackup)
                                  + ", not seen " + falls.Total(falls.NotSeen) + "; shrug-offs refused for a fall " + falls.ShrugRefusedForFall + " / a stagger " + falls.ShrugRefusedForStagger
                                  + "; backups sent " + falls.BackupsSent + ", refused " + falls.BackupsRefused);
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
                ManagedParameters.SetParameter(ManagedParametersEnum.DamageInterruptAttackThresholdCut, thresholdKeep);
                ImpactModelDefaults();
                AthleticsDefaults();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ImpactChargeThroughTheLogic()
        {
            var keep = _logic;
            float capKeep = StunCap;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                BlocksStunDefaults();
                Step56BlockModel();
                Step62On();
                var inputs = new FakeImpactInputs();
                var body = new FakeImpactBody();
                var sink = new FakeMessageSink();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = body;
                logic.ImpactInputs = inputs;
                logic.MessageSink = sink;
                var dice = new ScriptDice();
                logic.ImpactDice = dice;
                var model = SpikeDamageModel();
                var charge = logic.ImpactChargeStats;
                double t = 63000;
                logic.TickImpact(t);

                TrackedAgent Man(int index, double bar, double armour, int level)
                {
                    var m = logic.Track(FakeAgent(index))!;
                    m.AthleticsSkill = 125;
                    SetBar(m, bar);
                    inputs.Kits[index] = (armour, level);
                    return m;
                }
                ImpactDecision Strike(TrackedAgent a, TrackedAgent v)
                {
                    var cd = CleanHitDealing(20);
                    var blow = new Blow(a.AgentIndex);
                    model.DecideAgentKnockedDownByBlow(a.Agent, v.Agent, in cd, null!, in blow);
                    logic.ImpactModelHitTaken(a.Agent, v.Agent, in cd);
                    return logic.LastImpact;
                }
                void Tick(double seconds) => TickFor(logic, ref t, seconds);

                // ---- 1. TOUGHNESS: a legionary (armour 110, level 26) on a looter (8, 5) - x2.10, each kit read once
                var legionary = Man(6300, 1.0, 110, 26);
                inputs.Weapons[6300] = (1.3, 92, ImpactDamageType.Cut, 130);
                var looter = Man(6301, 1.0, 8, 5);
                int reads = inputs.KitReads;
                var d = Strike(legionary, looter);
                Check(Close(d.ToughnessFactor, 2.10, 0.01) && Close(d.ToughnessRatio, 5.97 / 1.35, 0.03), "toughness: x" + d.ToughnessFactor + ", ratio " + d.ToughnessRatio);
                d = Strike(legionary, looter);
                Check(inputs.KitReads == reads + 2, "the kit read more than once a man: " + (inputs.KitReads - reads));
                // health counts: the looter at half health is softer
                inputs.Health[6301] = 0.5;
                Check(Strike(legionary, looter).ToughnessFactor > d.ToughnessFactor, "a wounded looter not softer");
                inputs.Health.Remove(6301);

                // ---- 2. DIRECTION and an archer caught in melee
                var archer = Man(6302, 1.0, 15, 11);
                inputs.RangedInHand.Add(6302);
                inputs.Angles[6302] = 160;
                d = Strike(legionary, archer);
                Check(d.Direction == 2 && Close(d.DirectionFactor, 2.0, 1e-9) && Close(d.RangedFactor, 1.3, 1e-9), "behind / a bow: " + d.Direction + " x" + d.DirectionFactor + " x" + d.RangedFactor);
                inputs.Angles[6302] = 90;
                Check(Strike(legionary, archer).Direction == 1, "from the side not counted");
                Check(charge.DirBlows[2] >= 1 && charge.DirBlows[1] >= 1 && charge.RangedBlows >= 2, "the direction stats: " + string.Join(" / ", charge.DirBlows));

                // ---- 3. THE CHARGE: a sprint that ended 0.5 s ago carries into the FIRST blow only; a running sprint every blow; a rout never
                var charger = Man(6303, 1.0, 110, 26);
                inputs.Weapons[6303] = (1.3, 92, ImpactDamageType.Cut, 130);
                inputs.Speeds[6303] = 2.0; // he slowed to swing
                var target = Man(6304, 0.5, 15, 11);
                charger.SprintEndedAt = t - 0.5;
                d = Strike(charger, target);
                Check(d.Charging && Close(d.Momentum, 1.6, 1e-6) && Close(d.MeasuredSpeed, 2.0, 1e-9) && double.IsNaN(charger.SprintEndedAt), "the carried charge: charging " + d.Charging + ", x" + d.Momentum);
                Check(!Strike(charger, target).Charging, "the carry not spent by the first blow");
                charger.SprintEndedAt = t - 2.0;
                Check(!Strike(charger, target).Charging, "a sprint 2 s ago still charged (the carry is 1.5 s)");
                S.Set(SettingsSchema.SprintEnabled, true, SettingSources.File); // the older steps pin the sprint off
                S.Set(SettingsSchema.SprintAiEnabled, true, SettingSources.File);
                var rules = SprintRules.From(S);
                var started = charger.Sprint.TryStartAi(in rules, t, 5, SprintOrder.Charge);
                Check(started == SprintStart.Started && Strike(charger, target).Charging && Strike(charger, target).Charging, "a running sprint did not charge every blow (" + started + ")");
                charger.Sprint.Stop();
                charger.Sprint.FillUp();
                started = charger.Sprint.TryStartRout(in rules, t, 5);
                Check(started == SprintStart.Started && !Strike(charger, target).Charging, "a ROUT charged (" + started + ")");
                charger.Sprint.Stop();
                charger.SprintEndedAt = double.NaN;

                // ---- 4. THE REFLECTED CHARGE on YOU: you charge into a fresh legionary's right-side block - THROWN BACK (our blow on you, owned by
                // him, from the tick), the red line, its Fall watched; walking into it you never are
                var you = Man(6305, 0.5, 80, 20);
                inputs.Weapons[6305] = (1.4, 95, ImpactDamageType.Cut, 150);
                var wall = Man(6306, 1.0, 110, 26);
                inputs.Guards[6306] = (4.0, 130);
                inputs.Shields.Add(6306);
                logic.SmokePlayer = you.Agent;
                logic.TickImpact(t += 1.0 / 60);
                logic.ImpactModelHitTaken(you.Agent, wall.Agent, RightBlock()); // walking
                Check(!logic.LastImpact.ThrownBack.InRow, "a walking attacker could be thrown back");
                you.SprintEndedAt = t - 0.3;
                dice.Next(0.0, 0.5); // thrown back, his get-up
                logic.ImpactModelHitTaken(you.Agent, wall.Agent, RightBlock());
                d = logic.LastImpact;
                Check(d.Charging && d.ThrownBack.Happens && d.ThrownBack.Chance > 0.3 && d.Riposte.Refusal == ImpactRefusal.Replaced && you.Impact.Down(t + 0.5),
                    "you charging into a right-side block: thrown back " + d.ThrownBack.Chance + " (" + d.ThrownBack.Happens + ")");
                LogHas("[impact] YOU: you charged into (agent 6306)'s shield: THROWN BACK (");
                Tick(1.0 / 60);
                Check(body.OwnBlows.Exists(b => b.Victim == 6305 && b.Attacker == 6306) && charge.ThrownApplied == 1 && charge.ThrownYou == 1, "our blow on you not registered (owned by the shieldman)");
                LogHas("[impact] live: first CHARGE THROWN BACK (step 62) at ");
                Tick(0.05);
                Check(sink.Lines.Exists(l => l.Text.StartsWith("You are thrown back by ", StringComparison.Ordinal) && l.Text.EndsWith("'s shield", StringComparison.Ordinal) && l.Hex == ImpactMessageMath.ReceivedHex),
                    "the red line: " + string.Join(" | ", sink.Lines.Select(l => l.Text + " " + l.Hex)));
                t = PlayReaction(logic, body, you, ImpactActions.Fall, t, 1.5, index: 6305);
                Check(charge.ThrownSeen == 1, "your Fall not seen");
                // a PARRY never throws a charger back
                var fencer = Man(6307, 1.0, 110, 26);
                inputs.Guards[6307] = (1.2, 130);
                you.SprintEndedAt = t - 0.3;
                logic.TickImpact(t += 6.0); // your get-up immunity over
                logic.ImpactModelHitTaken(you.Agent, fencer.Agent, Parried());
                Check(!logic.LastImpact.ThrownBack.InRow, "a parry could throw a charger back");
                logic.SmokePlayer = null;

                // ---- 5. THE UNCAPPED CRITICAL through the real damage decorator: an EMPTY man's blow rolls up to x1.0 - his charge up to x1.5
                DamageDefaults();
                DamageRandomizer.Rng = ThreadSafeRandom.Shared;
                DamageRandomizer.OnMissionStart();
                var tired = Man(6308, 0.0, 50, 10);
                var plain = Enumerable.Range(0, 2000).Select(_ => Hit(attacker: tired.Agent)).ToList();
                tired.SprintEndedAt = t - 0.2;
                var charged = Enumerable.Range(0, 2000).Select(_ => Hit(attacker: tired.Agent)).ToList();
                Check(plain.Max() <= 50f && charged.Max() > 74f && charged.Min() < 26f, "the critical: plain max " + plain.Max() + ", charging " + charged.Min() + ".." + charged.Max());
                Check(DamageRandomizer.Stats.ChargeRolls(out double mean, out int lifted) == 2000 && lifted == 2000 && mean > 0.95 && plain.Average() < 38.5f,
                    "the charging rolls: mean x" + mean + " (the full 0.5-1.5), lifted " + lifted + "; plain mean " + plain.Average() + " (0.5-1.0 of 50)");
                S.Set(SettingsSchema.SprintFullCritical, false, SettingSources.Mcm);
                Check(Enumerable.Range(0, 2000).Select(_ => Hit(attacker: tired.Agent)).Max() <= 50f, "SprintFullCritical off: the charge still rolled the full upside");
                S.Set(SettingsSchema.SprintFullCritical, true, SettingSources.Mcm);
                DamageRandomizer.WriteSummary();
                LogHas("[summary] damage critical (step 62 - SprintFullCritical: a CHARGING attacker's melee blow rolls the full upside whatever his tiredness): 2000 charging melee hits, avg x");
                tired.SprintEndedAt = double.NaN;
                Console.WriteLine("        step 62: toughness legionary / looter x" + Strike(legionary, looter).ToughnessFactor.ToString("0.00", CultureInfo.InvariantCulture)
                                  + "; you charging into a fresh legionary's right block thrown back " + (100 * d.ThrownBack.Chance).ToString("0", CultureInfo.InvariantCulture)
                                  + "%; an empty man's charge rolls avg x" + mean.ToString("0.00", CultureInfo.InvariantCulture) + " (his plain blow avg x" + (plain.Average() / 50).ToString("0.00", CultureInfo.InvariantCulture) + ")");

                logic.WriteImpactSummary();
                LogHas("[summary] impact direction (step 62 - every decided landed blow on a man on foot by where it came from");
                LogHas("[summary] impact toughness (step 62 - the same blows by the attacker's toughness ÷ the defender's, courage's measure): ");
                LogHas("[summary] impact charges (step 62 - a CHARGE = the attacker sprinting, or his first blow within ImpactChargeCarrySeconds after a sprint): ");
                LogHas("; thrown back applied 1 → Fall seen 1, not seen 0, not applied 0; YOU thrown back 1");
                LogHas(", thrown back 1) → lines SHOWN ");
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
                ManagedParameters.SetParameter(ManagedParametersEnum.StunPeriodMax, capKeep);
                ImpactModelDefaults();
                AthleticsDefaults();
                DamageDefaults();
                DamageRandomizer.OnMissionStart();
            }
        }

        /// <summary>
        /// THE CHARGE ON A LINE (Anton's two pictures, 200 men each, seeded dice, through the real decorator and logic): a fresh LEGIONARY charging
        /// (the sprint's carry) into the BACKS of peasant archers (bow in hand) at a fresh / half bar vs walking into their front; LOOTERS charging
        /// a fresh legionary shield wall blocking right - thrown back / reeling - vs walking in.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ImpactChargeOnALine()
        {
            var keep = _logic;
            float capKeep = StunCap;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                BlocksStunDefaults();
                Step56BlockModel();
                Step62On();
                var inputs = new FakeImpactInputs();
                var body = new FakeImpactBody { Record = false };
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = body;
                logic.ImpactInputs = inputs;
                logic.ImpactDice = new SeededRandom(62);
                var model = SpikeDamageModel();
                double t = 64000;
                logic.TickImpact(t);
                int next = 64000;
                TrackedAgent Man(int skill, double bar, double armour, int level)
                {
                    var m = logic.Track(FakeAgent(next++))!;
                    m.AthleticsSkill = skill;
                    SetBar(m, bar);
                    inputs.Kits[m.AgentIndex] = (armour, level);
                    return m;
                }
                var legionary = Man(130, 1.0, 110, 26);
                inputs.Weapons[legionary.AgentIndex] = (1.3, 92, ImpactDamageType.Cut, 130);

                double chanceSum = 0;
                double IntoArchers(double bar, bool charging, double angle)
                {
                    int down = 0;
                    chanceSum = 0;
                    var cd = CleanHitDealing(20);
                    for (int i = 0; i < 200; i++)
                    {
                        var v = Man(40, bar, 15, 11);
                        inputs.RangedInHand.Add(v.AgentIndex);
                        inputs.Angles[v.AgentIndex] = angle;
                        if (charging) legionary.SprintEndedAt = t;
                        var blow = new Blow(legionary.AgentIndex);
                        if (model.DecideAgentKnockedDownByBlow(legionary.Agent, v.Agent, in cd, null!, in blow)) down++;
                        logic.ImpactModelHitTaken(legionary.Agent, v.Agent, in cd);
                        chanceSum += logic.LastImpact.Knockdown.Chance;
                        if ((i & 7) == 7) logic.TickImpact(t += 1.0 / 60);
                    }
                    return down / 200.0;
                }
                double walkFront = IntoArchers(1.0, false, 0);
                double chargeBackFresh = IntoArchers(1.0, true, 160), freshChance = chanceSum / 200;
                double chargeBackHalf = IntoArchers(0.5, true, 160), halfChance = chanceSum / 200;

                (double Thrown, double Reeled) LootersIntoTheWall(bool charging)
                {
                    int thrown = 0, reeled = 0;
                    for (int i = 0; i < 200; i++)
                    {
                        var a = Man(30, 1.0, 8, 5);
                        inputs.Weapons[a.AgentIndex] = (1.5, 85, ImpactDamageType.Blunt, 30);
                        var w = Man(130, 1.0, 110, 26);
                        inputs.Guards[w.AgentIndex] = (4.0, 130);
                        inputs.Shields.Add(w.AgentIndex);
                        if (charging) a.SprintEndedAt = t;
                        logic.ImpactModelHitTaken(a.Agent, w.Agent, RightBlock());
                        var d = logic.LastImpact;
                        if (d.ThrownBack.Happens) thrown++;
                        else if (d.Riposte.Happens) reeled++;
                        if ((i & 7) == 7) logic.TickImpact(t += 1.0 / 60);
                    }
                    return (thrown / 200.0, reeled / 200.0);
                }
                var walkIn = LootersIntoTheWall(false);
                var chargeIn = LootersIntoTheWall(true);
                string P(double v) => (100 * v).ToString("0", CultureInfo.InvariantCulture) + "%";
                Console.WriteLine("        step 62 THE CHARGE ON A LINE (200 men each, seeded): a legionary walking into fresh peasant archers' front - down " + P(walkFront)
                                  + "; charging into their BACKS - down " + P(chargeBackFresh) + " fresh (the model's chance " + P(freshChance) + "), " + P(chargeBackHalf) + " at half a bar ("
                                  + P(halfChance) + ") | looters into a fresh legionary shield wall blocking right: walking - thrown back "
                                  + P(walkIn.Thrown) + ", reel " + P(walkIn.Reeled) + "; CHARGING - thrown back " + P(chargeIn.Thrown) + ", reel " + P(chargeIn.Reeled));
                Check(walkFront < 0.05 && chargeBackFresh > 0.3 && chargeBackHalf > 0.4, "the charge into the archers' backs: front " + P(walkFront) + ", back " + P(chargeBackFresh) + " / " + P(chargeBackHalf));
                Check(walkIn.Thrown == 0 && chargeIn.Thrown > 0.2 && chargeIn.Thrown + chargeIn.Reeled > 0.6, "the looters' charge into the wall: walking thrown " + P(walkIn.Thrown) + ", charging thrown " + P(chargeIn.Thrown) + " reel " + P(chargeIn.Reeled));
                var stats = logic.ImpactChargeStats;
                Check(stats.ChargeBlows[0] == 200 && stats.ChargeBlows[5] == 400 && stats.OtherBlows[3] == 200 && stats.OtherBlows[0] == 200, "the charge stats by kind: " + string.Join(" / ", stats.ChargeBlows) + " | " + string.Join(" / ", stats.OtherBlows));
                logic.WriteImpactSummary();
                LogHas("| into a right-side shield block 200: the attacker reeled ");
                LogHas("| landed from BEHIND 400: the defender staggered ");
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
                ManagedParameters.SetParameter(ManagedParametersEnum.StunPeriodMax, capKeep);
                ImpactModelDefaults();
                AthleticsDefaults();
            }
        }

        /// <summary>The cost: a thousand men (kits, angles, bows, a third charging), twenty clean hits a tick through the real shrug-off hook +
        /// KnockDown seam + OnMeleeHit (the early decision, the backups) and twenty right-side blocks (the reflected charge) - small, no allocation.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ImpactChargeCostStaysFlat()
        {
            var keep = _logic;
            float thresholdKeep = ManagedParameters.GetParameter(ManagedParametersEnum.DamageInterruptAttackThresholdCut);
            float capKeep = StunCap;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                BlocksStunDefaults();
                Step56BlockModel();
                Step62On();
                ManagedParameters.SetParameter(ManagedParametersEnum.DamageInterruptAttackThresholdCut, 5f);
                var inputs = new FakeImpactInputs();
                var body = new FakeImpactBody { Record = false };
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = body;
                logic.ImpactInputs = inputs;
                logic.ImpactDice = new SeededRandom(620);
                var model = SpikeDamageModel();
                var game = new CustomAgentApplyDamageModel();
                const int men = 1000;
                var all = new TrackedAgent[men];
                for (int i = 0; i < men; i++)
                {
                    int index = 12000 + i;
                    var m = logic.Track(FakeAgent(index))!;
                    m.AthleticsSkill = 50 + (i % 200);
                    SetBar(m, (i % 101) / 100.0);
                    inputs.Weapons[index] = (0.6 + (i % 5) * 0.6, 80 + (i % 20), (ImpactDamageType)(i % 3), 20 + (i % 280));
                    inputs.Kits[index] = (i % 150, 1 + i % 40);
                    inputs.Angles[index] = (i * 37) % 180;
                    if (i % 7 == 0) inputs.RangedInHand.Add(index);
                    if (i % 2 == 0)
                    {
                        inputs.Guards[index] = (4.0, 100);
                        inputs.Shields.Add(index);
                    }
                    all[i] = m;
                }
                var hit = CleanHitDealing(3);
                var block = RightBlock();
                int k = 0;
                void Frame(int f)
                {
                    double t = 65000 + f / 60.0;
                    logic.TickImpact(t);
                    for (int e = 0; e < 20; e++, k++)
                    {
                        var a = all[(k * 7) % men];
                        var v = all[(k * 13 + 1) % men];
                        if (k % 3 == 0) a.SprintEndedAt = t;
                        var blow = new Blow(a.AgentIndex);
                        if (ImpactHooks.ShrugOffKept(v.Agent, in hit, in blow, game)) blow.BlowFlag |= BlowFlags.ShrugOff;
                        model.DecideAgentKnockedDownByBlow(a.Agent, v.Agent, in hit, null!, in blow);
                        logic.ImpactModelHitTaken(a.Agent, v.Agent, in hit);
                        var b = all[(k * 11 + 3) % men];
                        var w = all[(k * 17 + 2) % men];
                        if (k % 2 == 0) b.SprintEndedAt = t;
                        logic.ImpactModelHitTaken(b.Agent, w.Agent, in block);
                    }
                }
                for (int f = 0; f < 600; f++) Frame(f); // warm-up: every first line written
                const int ticks = 1200;
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var sw = Stopwatch.StartNew();
                for (int f = 600; f < 600 + ticks; f++) Frame(f);
                sw.Stop();
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double ms = sw.Elapsed.TotalMilliseconds / ticks;
                var falls = logic.ImpactFallStats;
                var charge = logic.ImpactChargeStats;
                Console.WriteLine("        step 62 cost: " + men + " men, 20 clean hits (the shrug-off hook, the seam, OnMeleeHit) + 20 right-side blocks a tick, a third / half charging - "
                                  + ms.ToString("0.000", CultureInfo.InvariantCulture) + " ms a tick, " + allocated + " bytes over " + ticks + " ticks; falls decided " + falls.Total(falls.Decided)
                                  + ", backups sent " + falls.BackupsSent + ", shrug-offs refused " + (falls.ShrugRefusedForFall + falls.ShrugRefusedForStagger) + "; charges " + charge.Charging
                                  + ", thrown back " + charge.ThrownApplied + ", capped " + charge.Capped);
                Check(ms < 0.5, "cost: 40 step-62 events a tick over " + men + " men took " + ms + " ms (budget 0.5 ms)");
                Check(allocated < 64 * 1024, "cost: step 62 allocates - " + allocated + " bytes over " + ticks + " ticks");
                Check(falls.Total(falls.Decided) > 100 && charge.Charging > 1000 && charge.ThrownApplied > 10, "cost: too few step-62 outcomes - falls " + falls.Total(falls.Decided)
                      + ", charges " + charge.Charging + ", thrown back " + charge.ThrownApplied);
                logic.WriteImpactSummary();
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
                ManagedParameters.SetParameter(ManagedParametersEnum.DamageInterruptAttackThresholdCut, thresholdKeep);
                ManagedParameters.SetParameter(ManagedParametersEnum.StunPeriodMax, capKeep);
                ImpactModelDefaults();
                AthleticsDefaults();
            }
        }

        /// <summary>The master-switch step's step-62 part: off = the shrug-off the game's (nothing decided there), no charge read by the damage
        /// roll, a pending fall let go with no backup; back on = live again.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ImpactChargeFollowsTheMasterSwitch()
        {
            var keep = _logic;
            float thresholdKeep = ManagedParameters.GetParameter(ManagedParametersEnum.DamageInterruptAttackThresholdCut);
            try
            {
                ImpactModelDefaults();
                Step62On();
                ManagedParameters.SetParameter(ManagedParametersEnum.DamageInterruptAttackThresholdCut, 5f);
                var body = new FakeImpactBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = body;
                logic.ImpactInputs = new FakeImpactInputs();
                var dice = new ScriptDice();
                logic.ImpactDice = dice;
                var model = SpikeDamageModel();
                var game = new CustomAgentApplyDamageModel();
                double t = 26000;
                var a = logic.Track(FakeAgent(2980))!;
                var v = logic.Track(FakeAgent(2981))!;
                SetBar(v, 0.0);
                logic.TickImpact(t);
                var cd = CleanHitDealing(3);
                var blow = new Blow(a.AgentIndex);
                dice.Next(0.0, 0.5);
                Check(!ImpactHooks.ShrugOffKept(v.Agent, in cd, in blow, game), "precondition: a decided fall did not refuse the shrug-off (step 62)");
                model.DecideAgentKnockedDownByBlow(a.Agent, v.Agent, in cd, null!, in blow);
                logic.ImpactModelHitTaken(a.Agent, v.Agent, in cd);
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                logic.TickImpact(t += 1.0 / 60);
                TickFor(logic, ref t, 1.0);
                Check(body.OwnBlows.Count == 0, "mod off: a backup blow was sent (step 62)");
                int decisions = logic.ImpactModelStats.Decisions;
                var w = logic.Track(FakeAgent(2982))!;
                SetBar(w, 0.0);
                Check(ImpactHooks.ShrugOffKept(w.Agent, in cd, in blow, game) && logic.ImpactModelStats.Decisions == decisions, "mod off: the shrug-off decided a blow (step 62)");
                a.SprintEndedAt = t;
                Check(!AthleticsLogic.TryGetCharging(a.Agent), "mod off: the damage roll would read a charge (step 62)");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                logic.TickImpact(t += 1.0 / 60);
                dice.Next(0.0, 0.5);
                var x = logic.Track(FakeAgent(2983))!;
                SetBar(x, 0.0);
                Check(!ImpactHooks.ShrugOffKept(x.Agent, in cd, in blow, game), "mod back on: a decided fall did not refuse the shrug-off (step 62)");
                logic.ImpactModelHitTaken(a.Agent, x.Agent, in cd);
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
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                ManagedParameters.SetParameter(ManagedParametersEnum.DamageInterruptAttackThresholdCut, thresholdKeep);
                ImpactModelDefaults();
            }
        }
    }
}

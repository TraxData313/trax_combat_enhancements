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
    /// Offline checks for step 53 - IMPACT I6, RIDERS + THE ON-SCREEN LINES (AI_NOTES "Step 53"): the REAL damage decorator's dismount seam
    /// (<c>DecideAgentDismountedByBlow</c>) and the REAL <see cref="AthleticsLogic"/> with the stand-in body - a rider knocked off by a clean hit
    /// / a kick (answered in the seam, followed until he is off: seen / not seen), his shield dropped from the tick, a BLOCKING rider knocked off
    /// by our own CanDismount blow from the tick (no horse by then = refused), never a forced stagger or a slowed reaction on a rider, a fresh
    /// rider untouched; a rider's MOMENTUM (his horse at a gallop x1.3, a couched lance x1.0) and its summary columns; the on-screen lines - to you,
    /// by you, merged within 1.5 s, your men's heroes behind the switch, never in shadow, the switch off; the fail safe (a throwing dismount blow
    /// disables its lever alone, a throwing decider = the game's answer); the master switch; the summary - then the CAVALRY CHARGE and the cost.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>A dismount decider that throws (the hook's fail-safe).</summary>
        private sealed class ThrowingDismount : IDismountDecider
        {
            public bool DismountOnBlow(Agent attacker, Agent victim, in AttackCollisionData collision, in Blow blow, bool gameSays) => throw new InvalidOperationException("smoke: the dismount decider failed");
        }

        private static void RidersCleanUp(AthleticsLogic? keep)
        {
            _logic = keep;
            SetStatic(typeof(AthleticsLogic), "_current", keep);
            ImpactHooks.RestoreStunCap();
            ImpactHooks.KnockDown = null;
            ImpactHooks.BlockStun = null;
            ImpactHooks.PickupLock = null;
            ImpactHooks.ShrugOff = null;
            ImpactHooks.Dismount = null;
            ImpactModelDefaults();
            S.Set(SettingsSchema.ImpactShowMessages, true, SettingSources.File);
            S.Set(SettingsSchema.ImpactMessagesIncludeYourMen, false, SettingSources.File);
            S.Set(SettingsSchema.ImpactMessageMinSeconds, 1.5, SettingSources.File);
            AthleticsDefaults();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ImpactRidersLiveThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                S.Set(SettingsSchema.AthleticsPoolPerSkill, 0.8, SettingSources.File);
                var inputs = new FakeImpactInputs();
                var body = new FakeImpactBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = body;
                logic.ImpactInputs = inputs;
                var sink = new FakeMessageSink();
                logic.MessageSink = sink;
                var dice = new ScriptDice();
                logic.ImpactDice = dice;
                var riders = logic.ImpactRiderStats;
                var hits = logic.ImpactHitStats;
                var model = SpikeDamageModel();
                double t = 36000;
                logic.TickImpact(t);
                Check(ReferenceEquals(ImpactHooks.Dismount, logic), "the logic is not the decorator's dismount decider");

                TrackedAgent Man(int index, double bar)
                {
                    var m = logic.Track(FakeAgent(index))!;
                    m.AthleticsSkill = 125; // pool 100
                    SetBar(m, bar);
                    return m;
                }
                TrackedAgent Rider(int index, double bar)
                {
                    var m = Man(index, bar);
                    inputs.Mounted.Add(index);
                    body.Mounted.Add(index);
                    return m;
                }
                // the engine's order for a man on a horse: CreateMeleeBlow asks the DISMOUNT question (never the KnockDown one), then OnMeleeHit
                bool Landed(TrackedAgent a, TrackedAgent v, AttackCollisionData? kind = null)
                {
                    var cd = kind ?? CleanHit();
                    var blow = new Blow(a.AgentIndex);
                    bool off = model.DecideAgentDismountedByBlow(a.Agent, v.Agent, in cd, null!, in blow);
                    logic.ImpactModelHitTaken(a.Agent, v.Agent, in cd);
                    return off;
                }
                void Tick() => logic.TickImpact(t += 1.0 / 60);

                var sword = Man(8000, 1.0);
                inputs.Weapons[8000] = (1.2, 92, ImpactDamageType.Cut, 125);

                // ---- 1. a clean hit on an EXHAUSTED rider: knocked off (50% = 50 x 50%), answered in the dismount seam, followed - seen off
                var r1 = Rider(8001, 0.0);
                int staggers = body.Staggers.Count, slows = body.Slows.Count;
                dice.Next(0.0, 0.5);
                Check(Landed(sword, r1), "an exhausted rider's dismount not answered in the decorator's seam");
                var d = logic.LastImpact;
                Check(d.Dismount && d.Knockdown.Happens && Math.Abs(d.Knockdown.Chance - 0.25) < 1e-9 && d.Stagger.Refusal == ImpactRefusal.Replaced,
                    "the rider's decision: dismount " + d.Dismount + ", chance " + d.Knockdown.Chance + " (50% x 50%)");
                Check(riders.Applied[(int)ImpactDismountWay.Seam] == 1 && logic.ImpactRiderChecks == 1 && riders.Decisions[2] == 1, "the seam's dismount not counted / followed");
                LogHas("[impact] live: first rider KNOCKED OFF his horse at ");
                LogHas(" - answered in the damage decorator's dismount seam (DecideAgentDismountedByBlow)");
                Tick();
                body.Mounted.Remove(8001); // the engine plays it: off his horse
                Tick();
                Check(riders.Seen[(int)ImpactDismountWay.Seam] == 1 && logic.ImpactRiderChecks == 0, "the dismount not seen");
                LogHas("[impact] live: first rider SEEN off his horse at ");
                Check(body.Staggers.Count == staggers && body.Slows.Count == slows && body.StaggerCallsOnRefused == 0, "an engine action on a rider (a stagger or a slowed reaction)");

                // ---- 2. a stagger on a rider: NEVER (a right-side block on an exhausted rider, the dice say yes to everything)
                var r2 = Rider(8002, 0.0);
                inputs.Shields.Add(8002);
                inputs.Guards[8002] = (4.0, 100);
                dice.Next(0.999); // no knockdown; the stagger refused before its die
                logic.ImpactModelHitTaken(sword.Agent, r2.Agent, RightBlock());
                d = logic.LastImpact;
                Check(!d.Stagger.Happens && d.Stagger.Refusal == ImpactRefusal.Rider && riders.StaggersRefused >= 1 && logic.ImpactBlockPendingCount == 0,
                    "a rider's block staggered (or queued a block action)");
                Tick();
                Check(body.Staggers.Count == staggers && body.StaggerCallsOnRefused == 0, "a forced stagger called on a rider");

                // ---- 3. a kick on a rider with a shield: the shield knocked from his hand from the tick (a horse does not hold it for him); no reel slowed
                var kicker = Man(8003, 1.0);
                var r3 = Rider(8004, 0.0);
                inputs.Shields.Add(8004);
                body.ShieldInHand[8004] = 3;
                dice.Next(0.999, 0.0, 0.5); // no knockdown, the shield, the reel's length
                Check(!Landed(kicker, r3, KickLands()), "a kick knocked the rider off when its dice said no");
                Check(logic.LastImpact.ShieldDrop.Happens && riders.ShieldQueued == 1, "the rider's shield drop not queued");
                int drops = body.Drops.Count;
                Tick();
                Check(body.Drops.Count == drops + 1 && body.Drops.Last().Man == 8004 && riders.ShieldDropped == 1, "the rider's shield not dropped from the tick");
                Check(!body.Slows.Any(s => s.Man == 8004), "a rider's reel slowed");

                // ---- 4. a BLOCKING rider knocked off: our own CanDismount blow from the tick, owned by the attacker, seen
                var r4 = Rider(8005, 0.0);
                inputs.Guards[8005] = (4.0, 100);
                dice.Next(0.0, 0.5); // knocked off (wrong side: 40% x 50% = 20%), the get-up roll
                logic.ImpactModelHitTaken(sword.Agent, r4.Agent, WrongBlock());
                Check(logic.LastImpact.Dismount && logic.ImpactRiderPendingCount == 1 && logic.ImpactBlockPendingCount == 0, "a blocking rider's dismount not queued for the tick");
                int dismounts = body.OwnDismounts.Count;
                Tick();
                Check(body.OwnDismounts.Count == dismounts + 1 && body.OwnDismounts.Last() == (8005, 8000, SpikeMath.OwnBlowDamage) && riders.Applied[(int)ImpactDismountWay.OwnBlow] == 1,
                    "our dismount blow not registered on the blocking rider (owned by the attacker)");
                LogHas("[impact] live: first BLOCKING rider knocked off his horse - our own blow (RegisterBlow, CanDismount, 1 damage) on ");
                Tick();
                Check(riders.Seen[(int)ImpactDismountWay.OwnBlow] == 1, "the blocking rider not seen off his horse");
                // ... one whose horse is gone by the tick: refused, no engine call
                var r5 = Rider(8006, 0.0);
                inputs.Guards[8006] = (4.0, 100);
                dice.Next(0.0, 0.5);
                logic.ImpactModelHitTaken(sword.Agent, r5.Agent, WrongBlock());
                body.Mounted.Remove(8006);
                dismounts = body.OwnDismounts.Count;
                Tick();
                Check(body.OwnDismounts.Count == dismounts && riders.DismountNotApplied[(int)ImpactNotApplied.NoHorse] == 1 && !r5.Impact.Down(t + 0.5),
                    "no horse by the tick: an engine call, or his record still down");

                // ---- 5. a dismount the engine does not play: still on the horse after 2 s = not seen, his record's ground time taken back
                var r6 = Rider(8007, 0.0);
                dice.Next(0.0, 0.5);
                Check(Landed(sword, r6), "the second dismount not answered");
                Check(r6.Impact.Down(t + 0.1), "precondition: the knocked-off rider is on the ground in his record");
                TickFor(logic, ref t, 2.2);
                Check(riders.NotSeen[(int)ImpactDismountWay.Seam] == 1 && !r6.Impact.Down(t) && !r6.Impact.KnockdownImmune(t), "a dismount never played: not counted, or his record still down / immune");
                LogHas("the engine did not play the dismount; tell Claude if the summary shows many (PLAYTEST D22)");
                body.Mounted.Remove(8007);

                // ---- 6. a FRESH rider: nothing (the game's answer); the KnockDown seam never answers a rider (the engine never asks it for him)
                var fresh = Rider(8008, 1.0);
                Check(!Landed(sword, fresh) && logic.LastImpact.Knockdown.Chance == 0, "a fresh rider knocked off");
                var r7 = Rider(8009, 0.0);
                dice.Next(0.0, 0.5);
                var hitBlow = new Blow(sword.AgentIndex);
                var cdHit = CleanHit();
                Check(!model.DecideAgentKnockedDownByBlow(sword.Agent, r7.Agent, in cdHit, null!, in hitBlow), "the KnockDown seam answered a rider");
                logic.ImpactModelHitTaken(sword.Agent, r7.Agent, in cdHit);
                Check(riders.DismountNotApplied[(int)ImpactNotApplied.OutsideSeam] == 1, "a rider's knockdown asked outside the dismount seam not counted");

                // ---- 7. a RIDER's MOMENTUM: his horse at a gallop x1.30, a couched lance x1.00 (the game's own big hit), by the horse's band
                var lancer = Man(8010, 1.0);
                inputs.Mounted.Add(8010);
                inputs.Weapons[8010] = (1.2, 92, ImpactDamageType.Cut, 125);
                inputs.Speeds[8010] = 10.0;
                var footman = Man(8011, 0.4);
                logic.ImpactModelHitTaken(lancer.Agent, footman.Agent, CleanHit());
                Check(Math.Abs(logic.LastImpact.Momentum - 1.3) < 1e-9, "a galloping rider's momentum: x" + logic.LastImpact.Momentum);
                inputs.Couched.Add(8010);
                logic.ImpactModelHitTaken(lancer.Agent, Man(8012, 0.4).Agent, CleanHit());
                Check(Math.Abs(logic.LastImpact.Momentum - 1.0) < 1e-9, "a couched lance got momentum: x" + logic.LastImpact.Momentum);
                inputs.Couched.Remove(8010);
                Check(hits.RiderBlowsBy[3] == 1 && hits.CouchedLances == 1, "the riders' momentum columns: galloping " + hits.RiderBlowsBy[3] + ", couched " + hits.CouchedLances);

                // ---- 8. the ON-SCREEN LINES: YOU knocked off (red), then - within 1.5 s - you knock a rider off and a man down: ONE merged line later
                TickFor(logic, ref t, 2.0); // nothing waits
                int lines = sink.Lines.Count;
                var you = Rider(8013, 0.0);
                logic.SmokePlayer = you.Agent;
                dice.Next(0.0, 0.5);
                Check(Landed(sword, you), "your dismount not answered");
                Tick();
                Check(sink.Lines.Count == lines + 1 && sink.Lines.Last() == ("You are knocked off your horse", "#D65252FF"), "your line: " + string.Join(" | ", sink.Lines.Skip(lines)));
                LogHas("[impact] on screen at ");
                LogHas("(#D65252FF): You are knocked off your horse");
                LogHas("[impact] YOU: (agent 8000)'s clean hit on you (riding) (your bar 0%, x ");
                body.Mounted.Remove(8013);
                inputs.Mounted.Remove(8013);
                inputs.Weapons[8013] = (1.2, 92, ImpactDamageType.Cut, 125);
                var victim1 = Rider(8014, 0.0);
                dice.Next(0.0, 0.5);
                Landed(you, victim1);
                var victim2 = Man(8015, 0.0);
                dice.Next(0.0, 0.5);
                var cd2 = CleanHit();
                var blow2 = new Blow(you.AgentIndex);
                Check(model.DecideAgentKnockedDownByBlow(you.Agent, victim2.Agent, in cd2, null!, in blow2), "your knockdown not answered");
                logic.ImpactModelHitTaken(you.Agent, victim2.Agent, in cd2);
                Tick();
                Check(sink.Lines.Count == lines + 1, "a second line within 1.5 s: " + string.Join(" | ", sink.Lines.Skip(lines)));
                TickFor(logic, ref t, 1.6);
                Check(sink.Lines.Count == lines + 2 && sink.Lines.Last() == ("You knock (agent 8014) off his horse; you knock (agent 8015) down", "#AE4AD9FF"),
                    "the merged line: " + string.Join(" | ", sink.Lines.Skip(lines)));
                Check(logic.ImpactMessages.Merged >= 1, "nothing merged");
                // your shield knocked away (a kick, from the tick) - to you, red (once you are up from the fall: a man on the ground loses no shield)
                inputs.Mounted.Remove(8014);
                TickFor(logic, ref t, 3.5);
                var kicker2 = Man(8016, 1.0);
                inputs.Shields.Add(8013);
                body.ShieldInHand[8013] = 2;
                dice.Next(0.0, 0.5); // the knockdown refused (immune: you were knocked off a moment ago - no die), the shield, the reel
                var kick = KickLands();
                var kb = new Blow(kicker2.AgentIndex);
                model.DecideAgentKnockedDownByBlow(kicker2.Agent, you.Agent, in kick, null!, in kb);
                logic.ImpactModelHitTaken(kicker2.Agent, you.Agent, in kick);
                TickFor(logic, ref t, 1.6);
                Check(sink.Lines.Last() == ("Your shield is knocked from your hand", "#D65252FF"), "your shield's line: " + sink.Lines.Last());
                logic.SmokePlayer = null;

                // ---- 9. your MEN (ImpactMessagesIncludeYourMen): your man knocks an enemy HERO off - a white line; a common enemy - none
                lines = sink.Lines.Count;
                var yours = Man(8017, 1.0);
                body.Yours.Add(8017);
                inputs.Weapons[8017] = (1.2, 92, ImpactDamageType.Cut, 125);
                var lord = Rider(8018, 0.0);
                lord.HeroName = "Derthert";
                dice.Next(0.0, 0.5);
                Landed(yours, lord);
                TickFor(logic, ref t, 1.6);
                Check(sink.Lines.Count == lines, "your men's line with the switch off");
                S.Set(SettingsSchema.ImpactMessagesIncludeYourMen, true, SettingSources.Mcm);
                var lord2 = Rider(8019, 0.0);
                lord2.HeroName = "Caladog";
                dice.Next(0.0, 0.5);
                Landed(yours, lord2);
                var common = Rider(8020, 0.0);
                dice.Next(0.0, 0.5);
                Landed(yours, common);
                TickFor(logic, ref t, 1.6);
                Check(sink.Lines.Count == lines + 1 && sink.Lines.Last() == ("(agent 8017) knocks Caladog off his horse", "#FFFFFFFF"), "your men's line: " + string.Join(" | ", sink.Lines.Skip(lines)));
                S.Set(SettingsSchema.ImpactMessagesIncludeYourMen, false, SettingSources.Mcm);

                // ---- 10. SHADOW: decided, nothing answered, no line; ImpactShowMessages off: no line, what waits thrown away
                logic.SmokePlayer = sword.Agent;
                S.Set(SettingsSchema.ImpactShadowMode, true, SettingSources.Mcm);
                Tick();
                lines = sink.Lines.Count;
                var shadowed = Rider(8021, 0.0);
                dice.Next(0.0, 0.5);
                Check(!Landed(sword, shadowed) && logic.LastImpact.Dismount, "shadow mode answered a dismount (or did not decide it)");
                TickFor(logic, ref t, 1.6);
                Check(sink.Lines.Count == lines, "a line in shadow mode");
                S.Set(SettingsSchema.ImpactShadowMode, false, SettingSources.Mcm);
                Tick();
                var quiet = Rider(8022, 0.0);
                dice.Next(0.0, 0.5);
                Check(Landed(sword, quiet), "the dismount not answered after shadow");
                S.Set(SettingsSchema.ImpactShowMessages, false, SettingSources.Mcm);
                TickFor(logic, ref t, 1.6);
                Check(sink.Lines.Count == lines && logic.ImpactMessages.Waiting == 0, "ImpactShowMessages off: a line shown or left waiting");
                S.Set(SettingsSchema.ImpactShowMessages, true, SettingSources.Mcm);
                logic.SmokePlayer = null;

                // ---- 11. the FAIL SAFE: our dismount blow throwing disables its lever alone (one [error]); then a landed blow's dismount is not answered
                body.ThrowOnDismount = true;
                var r8 = Rider(8023, 0.0);
                inputs.Guards[8023] = (4.0, 100);
                dice.Next(0.0, 0.5);
                logic.ImpactModelHitTaken(sword.Agent, r8.Agent, WrongBlock());
                Tick();
                body.ThrowOnDismount = false;
                Check(logic.ImpactLiveStats.Disabled(ImpactLever.Dismount) && !logic.ImpactLiveStats.Disabled(ImpactLever.OwnBlow) && !r8.Impact.Down(t + 0.5),
                    "a throwing dismount blow: the lever not off alone, or his record still down");
                LogHas("[impact] live lever a rider knocked off his horse disabled for this mission after an error at impact.live-dismount-blow");
                var r9 = Rider(8024, 0.0);
                dice.Next(0.0, 0.5);
                Check(!Landed(sword, r9) && riders.DismountNotApplied[(int)ImpactNotApplied.LeverOff] >= 1, "the dismount lever off: still answered");
                var plugged = ImpactHooks.Dismount;
                ImpactHooks.Dismount = new ThrowingDismount();
                var tb = new Blow(sword.AgentIndex);
                var tcd = CleanHit();
                Check(!ImpactHooks.DismountOnBlow(sword.Agent, r9.Agent, in tcd, in tb, false) && ImpactHooks.DismountOnBlow(sword.Agent, r9.Agent, in tcd, in tb, true),
                    "a throwing dismount decider was not the game's answer");
                ImpactHooks.Dismount = plugged;
                LogHas("smoke: the dismount decider failed");

                // ---- the summary
                logic.WriteImpactSummary();
                LogHas("[summary] impact riders (step 53 - a knockdown on a rider = KNOCKED OFF his horse at 50% of its chance, never a stagger; LIVE - kicks / bashes, blocks, clean hits, the riposte applied): live decisions on riders ");
                LogHas("→ APPLIED ");
                LogHas("[summary] impact riders' shields and limits: shields knocked from a rider's hand decided ");
                LogHas("[summary] impact messages (step 53 - on-screen lines, ImpactShowMessages on, only yours, at most one every 1.5 s): offered ");
                LogHas("|| RIDERS (step 53 - by the horse's speed into the blow;");
                LogHas("couched lances (no momentum - the game's own big hit) 1");
                Console.WriteLine("        step 53: dismounts decided " + riders.DismountsDecided + " → applied " + riders.Applied[0] + " (seam) + " + riders.Applied[1] + " (our blow) → seen "
                                  + riders.Seen[0] + " + " + riders.Seen[1] + ", not seen " + (riders.NotSeen[0] + riders.NotSeen[1]) + "; riders' shields dropped " + riders.ShieldDropped
                                  + "; staggers refused on riders " + riders.StaggersRefused + "; lines shown " + logic.ImpactMessages.Shown + ", parts merged " + logic.ImpactMessages.Merged);
            }
            finally
            {
                RidersCleanUp(keep);
            }
        }

        /// <summary>
        /// THE CAVALRY CHARGE (Claude's call for step 53, Anton's shock charge on horseback): lines of 200 men hit ONCE by a fresh rider's sword
        /// through the real decorator and logic with seeded dice - standing, at a canter, at a full gallop (x1.3), the gallop into an exhausted
        /// line and into a FRESH line; beside them step 52's sprinting man on foot (x1.6). A charge must throw a tired line down harder than a
        /// standing rider, never fell everyone, and leave a fresh line standing.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ImpactCavalryChargeThrowsTiredMenDown()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                S.Set(SettingsSchema.AthleticsPoolPerSkill, 0.8, SettingSources.File);
                var inputs = new FakeImpactInputs();
                var body = new FakeImpactBody { Record = false };
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = body;
                logic.ImpactInputs = inputs;
                logic.ImpactDice = new SeededRandom(53);
                var model = SpikeDamageModel();
                double t = 37000;
                logic.TickImpact(t);
                int next = 9000;
                TrackedAgent Man(double bar)
                {
                    var m = logic.Track(FakeAgent(next++))!;
                    m.AthleticsSkill = 125; // pool 100
                    SetBar(m, bar);
                    return m;
                }
                TrackedAgent Rider(double speed)
                {
                    var m = Man(1.0);
                    inputs.Mounted.Add(m.AgentIndex);
                    inputs.Weapons[m.AgentIndex] = (1.2, 92, ImpactDamageType.Cut, 125);
                    inputs.Speeds[m.AgentIndex] = speed;
                    return m;
                }
                var standing = Rider(0);
                var canter = Rider(6.5);
                var gallop = Rider(10);
                var sprinter = Man(1.0);
                inputs.Weapons[sprinter.AgentIndex] = (1.2, 92, ImpactDamageType.Cut, 125);
                inputs.Speeds[sprinter.AgentIndex] = 8.0;

                (double Down, double Any) Line(TrackedAgent attacker, double bar, int men = 200)
                {
                    int down = 0, any = 0;
                    var cd = CleanHit();
                    for (int i = 0; i < men; i++)
                    {
                        var v = Man(bar);
                        var blow = new Blow(attacker.AgentIndex);
                        if (model.DecideAgentKnockedDownByBlow(attacker.Agent, v.Agent, in cd, null!, in blow)) down++;
                        logic.ImpactModelHitTaken(attacker.Agent, v.Agent, in cd);
                        if (logic.LastImpact.AnyOutcome) any++;
                        if ((i & 7) == 7) logic.TickImpact(t += 1.0 / 60);
                    }
                    return ((double)down / men, (double)any / men);
                }
                var standHalf = Line(standing, 0.4);
                var canterHalf = Line(canter, 0.4);
                var gallopHalf = Line(gallop, 0.4);
                var gallopEmpty = Line(gallop, 0.05);
                var gallopFresh = Line(gallop, 1.0);
                var sprintHalf = Line(sprinter, 0.4);
                string P(double v) => (100 * v).ToString("0", CultureInfo.InvariantCulture) + "%";
                Console.WriteLine("        step 53 CAVALRY CHARGE (200 men each, a fresh rider's 1.2 kg sword, hit once): into a 40% line standing - down " + P(standHalf.Down) + ", any " + P(standHalf.Any)
                                  + "; at a canter (6.5 m/s, x1.15) - down " + P(canterHalf.Down) + ", any " + P(canterHalf.Any) + "; at a GALLOP (10 m/s, x1.30) - down " + P(gallopHalf.Down)
                                  + ", any " + P(gallopHalf.Any) + "; the gallop into an exhausted (5%) line - down " + P(gallopEmpty.Down) + ", any " + P(gallopEmpty.Any)
                                  + "; into a FRESH line - any " + P(gallopFresh.Any) + "; a man SPRINTING on foot (8 m/s, x1.60) into the 40% line - down " + P(sprintHalf.Down));
                Check(gallopHalf.Down > standHalf.Down && gallopHalf.Any > standHalf.Any, "the gallop no harder than a standing rider: " + P(gallopHalf.Down) + " vs " + P(standHalf.Down));
                Check(canterHalf.Any >= standHalf.Any && canterHalf.Any <= gallopHalf.Any + 0.05, "the canter not between: " + P(canterHalf.Any));
                Check(gallopEmpty.Down < 0.6, "a cavalry charge fells everyone: " + P(gallopEmpty.Down) + " of an exhausted line");
                Check(gallopFresh.Any == 0, "a cavalry charge broke a FRESH line: " + P(gallopFresh.Any));
                Check(gallopHalf.Down <= sprintHalf.Down + 0.05, "a galloping rider's sword above a sprinting man's (+30 vs +60%)");
                var hits = logic.ImpactHitStats;
                Check(hits.RiderBlowsBy[3] == 600 && hits.RiderBlowsBy[0] == 200 && hits.RiderBlowsBy[2] == 200, "the riders' columns: " + string.Join(" / ", hits.RiderBlowsBy));
                logic.WriteImpactSummary();
                LogHas("| galloping 600 (10.0 m/s, impact x1.30): an outcome ");
            }
            finally
            {
                RidersCleanUp(keep);
            }
        }

        /// <summary>The cost: a thousand men at every bar, half of them riders, twenty landed blows a tick on riders through the decorator's
        /// dismount seam + twenty blocks by riders (our dismount blows from the tick), the dismount checks - small and allocating nothing.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ImpactRidersCostStaysFlat()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                var inputs = new FakeImpactInputs();
                var body = new FakeImpactBody { Record = false, DismountTakesHimOff = false };
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = body;
                logic.ImpactInputs = inputs;
                logic.ImpactDice = new SeededRandom(530);
                var model = SpikeDamageModel();
                const int men = 1000;
                var all = new TrackedAgent[men];
                for (int i = 0; i < men; i++)
                {
                    var m = logic.Track(FakeAgent(13000 + i))!;
                    m.AthleticsSkill = 50 + (i % 200);
                    SetBar(m, (i % 101) / 100.0);
                    inputs.Weapons[13000 + i] = (0.6 + (i % 5) * 0.6, 80 + (i % 20), (ImpactDamageType)(i % 3), 20 + (i % 280));
                    inputs.Guards[13000 + i] = (2.0 + (i % 4), 50 + (i % 200));
                    if (i % 2 == 1)
                    {
                        inputs.Mounted.Add(13000 + i);
                        body.Mounted.Add(13000 + i);
                    }
                    if (i % 3 == 0) inputs.Speeds[13000 + i] = i % 12;
                    all[i] = m;
                    all[i] = m;
                }
                var hit = CleanHit();
                var wrong = WrongBlock();
                int k = 0;
                void Frame(int f)
                {
                    double t = 47000 + f / 60.0;
                    logic.TickImpact(t);
                    for (int e = 0; e < 20; e++, k++)
                    {
                        var a = all[(k * 7) % men];
                        var v = all[((k * 13) | 1) % men]; // a rider
                        var blow = new Blow(a.AgentIndex);
                        model.DecideAgentDismountedByBlow(a.Agent, v.Agent, in hit, null!, in blow);
                        logic.ImpactModelHitTaken(a.Agent, v.Agent, in hit);
                        var w = all[((k * 17) | 1) % men];
                        logic.ImpactModelHitTaken(a.Agent, w.Agent, in wrong);
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
                var riders = logic.ImpactRiderStats;
                Console.WriteLine("        step 53 cost: " + men + " men (half riders), 20 landed blows + 20 blocks on riders a tick - " + ms.ToString("0.000", CultureInfo.InvariantCulture) + " ms a tick, "
                                  + allocated + " bytes over " + ticks + " ticks; rider decisions " + riders.DecisionsTotal + ", dismounts applied " + riders.Applied[0] + " + " + riders.Applied[1]
                                  + ", not seen " + (riders.NotSeen[0] + riders.NotSeen[1]) + ", pool full " + riders.PoolFull);
                Check(ms < 0.5, "cost: 40 rider events a tick over " + men + " men took " + ms + " ms (budget 0.5 ms)");
                Check(allocated < 64 * 1024, "cost: the riders allocate - " + allocated + " bytes over " + ticks + " ticks");
                Check(riders.Applied[0] > 100 && riders.Applied[1] > 100, "cost: too few dismounts - " + riders.Applied[0] + " / " + riders.Applied[1]);
                logic.WriteImpactSummary();
            }
            finally
            {
                RidersCleanUp(keep);
            }
        }

        /// <summary>The master-switch step's riders part: off = the dismount seam the game's, nothing queued played, no line; back on = live.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ImpactRidersFollowTheMasterSwitch()
        {
            var keep = _logic;
            try
            {
                ImpactModelDefaults();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                var body = new FakeImpactBody();
                var inputs = new FakeImpactInputs();
                logic.ImpactBody = body;
                logic.ImpactInputs = inputs;
                var sink = new FakeMessageSink();
                logic.MessageSink = sink;
                var dice = new ScriptDice();
                logic.ImpactDice = dice;
                var model = SpikeDamageModel();
                double t = 25000;
                var a = logic.Track(FakeAgent(2980))!;
                logic.TickImpact(t);
                TrackedAgent Rider(int i)
                {
                    var r = logic.Track(FakeAgent(i))!;
                    SetBar(r, 0.0);
                    inputs.Mounted.Add(i);
                    body.Mounted.Add(i);
                    inputs.Guards[i] = (4.0, 100);
                    return r;
                }
                var cd = CleanHit();
                var blow = new Blow(a.AgentIndex);
                logic.SmokePlayer = a.Agent;
                // a blocking rider's dismount queued, then the switch goes: nothing played, no line, the seam the game's
                var blocker = Rider(2981);
                dice.Next(0.0, 0.5);
                logic.ImpactModelHitTaken(a.Agent, blocker.Agent, WrongBlock());
                Check(logic.ImpactRiderPendingCount == 1, "precondition: the blocking rider's dismount not queued (step 53)");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                logic.TickImpact(t += 0.02);
                Check(body.OwnDismounts.Count == 0 && logic.ImpactRiderPendingCount == 0, "mod off: a queued dismount blow played (step 53)");
                var r = Rider(2982);
                dice.Next(0.0, 0.5);
                Check(!model.DecideAgentDismountedByBlow(a.Agent, r.Agent, in cd, null!, in blow), "mod off: a rider knocked off (step 53)");
                logic.ImpactModelHitTaken(a.Agent, r.Agent, in cd);
                logic.TickImpact(t += 2.0);
                Check(sink.Lines.Count == 0, "mod off: an on-screen line (step 53)");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                logic.TickImpact(t += 0.02);
                var r2 = Rider(2983);
                dice.Next(0.0, 0.5);
                Check(model.DecideAgentDismountedByBlow(a.Agent, r2.Agent, in cd, null!, in blow), "mod back on: the rider's dismount not answered (step 53)");
                logic.ImpactModelHitTaken(a.Agent, r2.Agent, in cd);
                logic.TickImpact(t += 0.02);
                Check(sink.Lines.Count == 1 && sink.Lines[0].Text == "You knock (agent 2983) off his horse", "mod back on: your line missing (step 53): " + string.Join(" | ", sink.Lines));
                logic.SmokePlayer = null;
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
                ImpactHooks.Dismount = null;
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
            }
        }
    }
}

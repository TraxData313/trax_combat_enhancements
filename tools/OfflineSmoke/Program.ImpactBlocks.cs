using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;
using TraxCombat.Models;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 51 - IMPACT I4, BLOCKS AND THE RIPOSTE LIVE (AI_NOTES "Step 51"): the REAL damage decorator's stun seam from
    /// worker threads and the REAL <see cref="AthleticsLogic"/> with the stand-in body (<c>FakeImpactBody</c> - it plays what the engine would:
    /// the stagger we started at its index, a Fall and its _continue, a guard) - the stun by tiredness (the T snapshot, the game's cap raised and
    /// given back, the untouched stuns clamped), a right-side block's stagger FROM THE TICK at the speed for its length, measured, its end in the
    /// blocker's record, a block and a hit from inside it; a wrong-side block's shield dropped from the tick; a blocker knocked down by our own
    /// blow (its fall-and-rise slowed and measured, ONE shock for courage); the riposte on the attacker (you - the YOU line); you staggered on
    /// your block (the YOU line, the weapon's name); ImpactOnPlayerPercent 0; a rider (nothing, no engine call); a man down at the tick (no
    /// engine call); the engine refusing; shadow; blocks off / the riposte alone; the riposte off; the guard timed; failing levers disabled
    /// alone; the summary - and the cost.
    /// </summary>
    internal static partial class Program
    {
        private static void BlocksStunDefaults()
        {
            ManagedParameters.SetParameter(ManagedParametersEnum.StunPeriodMax, 0.8f);
            S.Set(SettingsSchema.ImpactStunCapSeconds, 2.0, SettingSources.File);
            S.Set(SettingsSchema.BlockStunTiredExtraPercent, 100, SettingSources.File);
            S.Set(SettingsSchema.ImpactOnPlayerPercent, 100, SettingSources.File);
            S.Set(SettingsSchema.ImpactBlocksEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.ImpactRiposteEnabled, true, SettingSources.File);
        }

        /// <summary>The stun the real decorator gives a blocked blow (the base model's, then ours), asked from a WORKER thread.</summary>
        private static (float Att, float Def) StunFromWorker(TraxDamageModel model, TrackedAgent a, TrackedAgent d, float att, float def)
        {
            float x = att, y = def;
            Task.Run(() => model.CalculateDefendedBlowStunMultipliers(a.Agent, d.Agent, CombatCollisionResult.Blocked, null!, null!, ref x, ref y)).Wait();
            return (x, y);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ImpactBlocksLiveThroughTheLogic()
        {
            var keep = _logic;
            float capKeep = StunCap;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                BlocksStunDefaults();
                S.Set(SettingsSchema.AthleticsPoolPerSkill, 0.8, SettingSources.File);
                var inputs = new FakeImpactInputs();
                var body = new FakeImpactBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = body;
                logic.ImpactInputs = inputs;
                var dice = new ScriptDice();
                logic.ImpactDice = dice;
                var bl = logic.ImpactBlockStats;
                var model = SpikeDamageModel();
                double t = 50000;
                logic.TickImpact(t);
                Check(StunCap == 2f && ImpactHooks.StunCapRaised && Near(ImpactHooks.VanillaStunCap, 0.8f), "blocks live: the game's stun cap was not raised 0.8 → 2 s: " + StunCap);
                LogHas("[impact] live: blocks live at 50000.0 s - the game's stun cap (StunPeriodMax) 0.80 → 2.00 s while they are (ImpactStunCapSeconds)");

                TrackedAgent Man(int index, int athletics, double bar)
                {
                    var m = logic.Track(FakeAgent(index))!;
                    m.AthleticsSkill = athletics;
                    SetBar(m, bar);
                    return m;
                }
                void Tick(double seconds = 1.0 / 60) => TickFor(logic, ref t, seconds);
                var attacker = Man(6000, 125, 1.0);
                inputs.Weapons[6000] = (1.2, 92, ImpactDamageType.Cut, 100);

                // ---- 1. THE STUN BY TIREDNESS - the real decorator from WORKER threads, T from the tick's snapshot
                var tired = Man(6001, 125, 0.0);
                var fresh = Man(6002, 125, 1.0);
                Check(Near(StunFromWorker(model, attacker, tired, 0.1f, 0.3f).Def, 0.3f), "the stun lengthened before the tick wrote the man's T");
                Tick();
                Check(Near(tired.ImpactTired, 1f) && Near(fresh.ImpactTired, 0f), "the T snapshot: " + tired.ImpactTired + " / " + fresh.ImpactTired);
                var s1 = StunFromWorker(model, attacker, tired, 0.1f, 0.3f);
                Check(Near(s1.Def, 0.6f) && Near(s1.Att, 0.1f), "an exhausted blocker's stun x2 (the attacker's recoil untouched): " + s1.Att + " / " + s1.Def);
                Check(Near(StunFromWorker(model, attacker, fresh, 0.1f, 0.3f).Def, 0.3f), "a fresh blocker's stun changed");
                Check(Near(StunFromWorker(model, attacker, tired, 0.1f, 1.2f).Def, 1.6f), "the game's 1.2 s (it would play 0.8) x2 is not 1.6 s");
                Check(Near(StunFromWorker(model, attacker, fresh, 0.1f, 1.2f).Def, 0.8f), "an untouched stun above the game's 0.8 was not clamped to it while our cap is raised");
                S.Set(SettingsSchema.ImpactStunCapSeconds, 1.0, SettingSources.Mcm);
                Tick();
                Check(StunCap == 1f && Near(StunFromWorker(model, attacker, tired, 0.1f, 0.9f).Def, 1.0f), "ImpactStunCapSeconds 1.0 live: the cap " + StunCap);
                S.Set(SettingsSchema.ImpactStunCapSeconds, 2.0, SettingSources.Mcm);
                S.Set(SettingsSchema.BlockStunTiredExtraPercent, 50, SettingSources.Mcm);
                Tick();
                Check(StunCap == 2f && Near(StunFromWorker(model, attacker, tired, 0.1f, 0.3f).Def, 0.45f), "BlockStunTiredExtraPercent 50 live: not x1.5");
                S.Set(SettingsSchema.BlockStunTiredExtraPercent, 100, SettingSources.Mcm);
                var you = Man(6003, 100, 0.0);
                logic.SmokePlayer = you.Agent;
                S.Set(SettingsSchema.ImpactOnPlayerPercent, 50, SettingSources.Mcm);
                Tick();
                Check(Near(you.ImpactTired, 0.5f) && Near(StunFromWorker(model, attacker, you, 0.1f, 0.3f).Def, 0.45f), "your stun x ImpactOnPlayerPercent 50: T " + you.ImpactTired);
                S.Set(SettingsSchema.ImpactOnPlayerPercent, 100, SettingSources.Mcm);
                logic.SmokePlayer = null;
                var mounted = Man(6004, 125, 0.0);
                body.Mounted.Add(6004);
                Tick();
                Check(Near(mounted.ImpactTired, 0f) && Near(StunFromWorker(model, attacker, mounted, 0.1f, 0.3f).Def, 0.3f), "a rider's stun lengthened (step 53's)");
                long before = bl.Stun.Calls;
                Parallel.For(0, 200, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
                {
                    float x = 0.1f, y = 0.3f;
                    model.CalculateDefendedBlowStunMultipliers(attacker.Agent, (i % 2 == 0 ? tired : fresh).Agent, CombatCollisionResult.Blocked, null!, null!, ref x, ref y);
                });
                Check(bl.Stun.Calls == before + 200 && bl.Stun.OffMainThread >= 9 && bl.Stun.Lengthened >= 100, "the seam's tally from worker threads: " + bl.Stun.Calls + " / off " + bl.Stun.OffMainThread); // Parallel.For runs some on this thread too

                // ---- 2. a RIGHT-SIDE block by an EXHAUSTED man: the stagger queued, made FROM THE TICK at 0.9 / 1.5 = 0.6, measured, his record
                var blocker = Man(6010, 125, 0.0);
                inputs.Guards[6010] = (4.0, 100);
                inputs.Shields.Add(6010);
                dice.Next(0.999, 0.0, 0.5); // no knockdown, staggered, its length at the roll's centre (x2.5 at T 1)
                logic.ImpactModelHitTaken(attacker.Agent, blocker.Agent, RightBlock());
                var d = logic.LastImpact;
                Check(d.Stagger.Happens && Close(d.StaggerSeconds, 1.5, 1e-5) && Close(d.StunFactor, 2.0, 1e-9), "the right-side block on the exhausted man: stagger " + d.Stagger.Happens + " " + d.StaggerSeconds + " s");
                Check(body.Staggers.Count == 0 && logic.ImpactBlockPendingCount == 1, "the stagger was started INSIDE the hit callback (or not queued)");
                Check(blocker.Impact.Reeling(t + 0.5) && blocker.Impact.Recent(t, 6) == 1, "the live stagger is not on his record (once)");
                LogHas("[impact] first right-side block decided at ");
                Tick();
                Check(body.Staggers.Count == 1 && body.Staggers[0] == (6010, false) && Near(body.StaggerSpeeds[0].Speed, 0.6f) && logic.ImpactBlockPendingCount == 0
                      && bl.AppliedOf(ImpactOutcome.Stagger) == 1 && bl.StaggerAcceptedWithPriority == 1,
                    "the stagger was not made from the tick at speed 0.6 with the engine's priority: " + body.Staggers.Count + (body.StaggerSpeeds.Count > 0 ? " at " + body.StaggerSpeeds[0].Speed : string.Empty));
                LogHas("[impact] live: first block stagger - (agent 6010) after his right-side block - act_stagger_backward from the tick at ");
                LogHas("with the engine's priority, speed 0.60 for 1.50 s asked (the animation's own length 0.90 s - a guess until one is measured)");
                // the engine plays it (its index 4242): a block and a hit from INSIDE it are counted, the block refused (he reels: immune)
                double from = t;
                body.Actions[6010] = Ch0(ImpactActions.StrikeLight, 4242);
                Tick(0.2);
                Check(bl.SeenOf(ImpactOutcome.Stagger) == 1, "the stagger was not seen");
                LogHas("[impact] live: first block stagger seen at ");
                var other = Man(6011, 125, 1.0);
                logic.ImpactModelHitTaken(other.Agent, blocker.Agent, RightBlock());
                Check(bl.BlocksFromInside == 1 && logic.LastImpact.Stagger.Refusal == ImpactRefusal.StaggerImmune, "a block from inside the stagger: " + bl.BlocksFromInside);
                logic.ImpactModelHitTaken(other.Agent, blocker.Agent, CleanHit());
                Check(bl.HitsFromInside == 1, "a hit from inside the stagger: " + bl.HitsFromInside);
                Tick(1.3);
                body.Actions.Remove(6010);
                Tick();
                double end = t;
                Check(bl.StaggerMeasured.Count == 1 && Within(bl.StaggerMeasured.Mean, 1.5, 0.05) && Within(logic.StaggerNaturalSeconds, 0.9, 0.03),
                    "the stagger's length: " + bl.StaggerMeasured.Mean + " s, the learned animation " + logic.StaggerNaturalSeconds + " s");
                Check(!blocker.Impact.Reeling(end) && blocker.Impact.StaggerImmune(end + 1.9) && !blocker.Impact.StaggerImmune(end + 2.1),
                    "the measured end not in his record (reeling " + blocker.Impact.ReelingUntil + ", immune " + blocker.Impact.StaggerImmuneUntil + ", end ~" + end + ")");
                LogHas("[impact] live: first block stagger measured at ");

                // ---- 3. a WRONG-SIDE block by an exhausted shielded man: the shield dropped FROM THE TICK + the stagger
                var turtle = Man(6020, 125, 0.0);
                inputs.Guards[6020] = (4.0, 100);
                inputs.Shields.Add(6020);
                body.ShieldInHand[6020] = 3;
                dice.Next(0.999, 0.0, 0.0, 0.5); // no knockdown, the shield, the stagger, its length
                logic.ImpactModelHitTaken(attacker.Agent, turtle.Agent, WrongBlock());
                Check(logic.LastImpact.ShieldDrop.Happens && logic.LastImpact.Stagger.Happens && !body.Drops.Contains((6020, 3)), "the wrong-side block: not decided, or the drop inside the callback");
                Tick();
                Check(body.Drops.Contains((6020, 3)) && body.Staggers.Exists(s => s.Man == 6020) && bl.AppliedOf(ImpactOutcome.ShieldDrop) == 1, "the wrong-side block's drop / stagger not made from the tick");
                LogHas("[impact] live: first shield drop after a WRONG-side block made from the tick at ");
                body.SlotEmptied.Add(6020);
                Tick(0.6);
                Check(bl.SeenOf(ImpactOutcome.ShieldDrop) == 1, "the wrong-side block's shield never left the hand in the count");

                // ---- 4. a BLOCKER KNOCKED DOWN: our own blow from the tick, his Fall slowed x2 and measured, the 5 s immunity, ONE shock
                var floored = Man(6030, 125, 0.0);
                inputs.Guards[6030] = (4.0, 100);
                dice.Next(0.0, 0.5); // knocked down (the stagger replaced), the get-up's roll at its centre (x2 at T 1)
                logic.ImpactModelHitTaken(attacker.Agent, floored.Agent, RightBlock());
                d = logic.LastImpact;
                Check(d.Knockdown.Happens && d.Stagger.Refusal == ImpactRefusal.Replaced && Close(d.GetUpFactor, 2.0, 1e-9) && body.OwnBlows.Count == 0, "the blocker's knockdown: not decided, or our blow inside the callback");
                Tick();
                Check(body.OwnBlows.Count == 1 && body.OwnBlows[0] == (6030, 6000, 1) && bl.OwnBlows == 1, "our own blow not made from the tick: " + body.OwnBlows.Count);
                LogHas("[impact] live: first BLOCKER knocked down - our own blow (RegisterBlow, KnockDown | KnockBack, 1 damage) on (agent 6030)");
                // courage: our blow's OnAgentHit is NOT a second shock (the knockdown is one already)
                var hit = typeof(AthleticsLogic).GetMethod("CourageHitTaken", BindingFlags.Instance | BindingFlags.NonPublic)!;
                ImpactHooks.BeginOwnBlow();
                try { hit.Invoke(logic, new object[] { floored.Agent, t }); }
                finally { ImpactHooks.EndOwnBlow(); }
                Check(floored.Hits.Recent(t, 6) == 0 && floored.Impact.Recent(t, 6) == 1, "our own blow counted as a second shock: hits " + floored.Hits.Recent(t, 6));
                hit.Invoke(logic, new object[] { floored.Agent, t });
                Check(floored.Hits.Recent(t, 6) == 1, "a real hit was not a shock");
                int slows = body.Slows.Count;
                double after = PlayReaction(logic, body, floored, ImpactActions.Fall, t + 1.0 / 60, 3.0, continueAt: 1.0, index: 1300);
                t = after;
                var fallSlows = body.Slows.Skip(slows).Where(x => x.Man == 6030).ToList();
                Check(fallSlows.Count == 2 && fallSlows.All(x => Near(x.Speed, 0.5f)) && bl.SeenOf(ImpactOutcome.Knockdown) == 1 && bl.BlockGetUpMeasured.Count == 1
                      && Within(bl.BlockGetUpMeasured.Mean, 3.0, 0.05), "the blocker's fall-and-rise not slowed x2 / measured: " + fallSlows.Count + ", " + bl.BlockGetUpMeasured.Mean);
                Check(floored.Impact.KnockdownImmune(after + 4.7) && !floored.Impact.KnockdownImmune(after + 5.2), "the blocker's get-up immunity not from the measured end");
                LogHas("[impact] live: first blocker's Fall seen at ");

                // ---- 5. the RIPOSTE: a fresh veteran blocks YOU (a recruit at 40 %) - your forced stagger from the tick, the YOU line
                var veteran = Man(6040, 250, 1.0);
                inputs.Guards[6040] = (4.0, 200);
                inputs.Shields.Add(6040);
                var recruit = Man(6041, 62, 0.4);
                inputs.Weapons[6041] = (1.2, 92, ImpactDamageType.Cut, 40);
                logic.SmokePlayer = recruit.Agent;
                dice.Next(0.0, 0.5); // the riposte (his own outcomes have no chance), its recoil's roll
                logic.ImpactModelHitTaken(recruit.Agent, veteran.Agent, RightBlock());
                d = logic.LastImpact;
                Check(d.Riposte.Happens && d.Riposte.Chance > 0.2 && d.AttackerIsPlayer && recruit.Impact.Reeling(t + 0.1) && veteran.Impact.Recent(t, 8) == 0,
                    "the riposte on you: " + d.Riposte.Chance + ", your record reeling " + recruit.Impact.Reeling(t + 0.1));
                LogHas("[impact] YOU: (agent 6040) blocked you (right-side block): you reel (riposte, ");
                Tick();
                Check(body.Staggers.Exists(s => s.Man == 6041) && bl.RiposteAppliedTotal == 1, "the riposte's stagger was not made on the ATTACKER from the tick");
                LogHas("[impact] live: first RIPOSTE - the attacker (agent 6041) staggered after (agent 6040)'s right-side block");
                t = PlayReaction(logic, body, recruit, ImpactActions.StrikeLight, t, d.RiposteStunSeconds, index: 4242);
                Check(bl.RiposteSeen == 1 && bl.RiposteMeasured.Count == 1, "the riposte's stagger not seen / measured");
                logic.SmokePlayer = null;

                // ---- 6. YOU staggered on your block (the weapon's name); ImpactOnPlayerPercent 0 = never
                var me = Man(6050, 125, 0.1);
                inputs.Guards[6050] = (4.0, 100);
                logic.SmokePlayer = me.Agent;
                dice.Next(0.999, 0.0, 0.5);
                model.DecideAgentKnockedDownByBlow(attacker.Agent, me.Agent, RightBlock(), null!, new Blow(attacker.AgentIndex)); // the engine asks; a block is not decided there
                logic.ImpactModelHitTaken(attacker.Agent, me.Agent, RightBlock());
                Check(logic.LastImpact.Stagger.Happens && logic.LastImpact.DefenderIsPlayer, "your block did not stagger you");
                LogHas("[impact] YOU: your right-side block of (agent 6000)'s test axe (your bar 10%, x ");
                LogHas("): you are staggered (");
                Tick();
                S.Set(SettingsSchema.ImpactOnPlayerPercent, 0, SettingSources.Mcm);
                var me2 = Man(6051, 125, 0.0);
                logic.SmokePlayer = me2.Agent;
                int pending = logic.ImpactBlockPendingCount;
                logic.ImpactModelHitTaken(attacker.Agent, me2.Agent, WrongBlock());
                Check(logic.LastImpact.Stagger.Chance == 0 && logic.LastImpact.Knockdown.Chance == 0 && logic.ImpactBlockPendingCount == pending, "ImpactOnPlayerPercent 0: your block still staggered you");
                S.Set(SettingsSchema.ImpactOnPlayerPercent, 100, SettingSources.Mcm);
                logic.SmokePlayer = null;

                // ---- 7. a RIDER blocks: decided, nothing applied, no engine call; a man DOWN at the tick: refused before any call; the engine refusing
                var rider = Man(6060, 125, 0.0);
                inputs.Mounted.Add(6060);
                body.Mounted.Add(6060);
                dice.Next(0.0, 0.5);
                logic.ImpactModelHitTaken(attacker.Agent, rider.Agent, WrongBlock());
                Check(logic.LastImpact.Dismount && logic.ImpactBlockPendingCount == 0 && rider.Impact.Recent(t, 8) == 0 && bl.NotApplied[(int)ImpactOutcome.Knockdown, (int)ImpactNotApplied.Rider] == 1,
                    "a rider's block applied something (step 53's)");
                var downed = Man(6070, 125, 0.0);
                dice.Next(0.999, 0.0, 0.5);
                logic.ImpactModelHitTaken(attacker.Agent, downed.Agent, RightBlock());
                body.Actions[6070] = Ch0(ImpactActions.Fall, 77); // he went down before the tick
                int calls = body.Staggers.Count;
                Tick();
                body.Actions.Remove(6070);
                Check(body.Staggers.Count == calls && body.StaggerCallsOnRefused == 0 && bl.NotApplied[(int)ImpactOutcome.Stagger, (int)ImpactNotApplied.CannotForce] == 1 && !downed.Impact.Reeling(t),
                    "a man down at the tick: an engine call, or his record still reels");
                var refused = Man(6080, 125, 0.0);
                dice.Next(0.999, 0.0, 0.5);
                logic.ImpactModelHitTaken(attacker.Agent, refused.Agent, RightBlock());
                body.AcceptStagger = false;
                calls = body.Staggers.Count;
                Tick();
                body.AcceptStagger = true;
                Check(body.Staggers.Count == calls + 2 && body.Staggers[calls] == (6080, false) && body.Staggers[calls + 1] == (6080, true)
                      && bl.NotApplied[(int)ImpactOutcome.Stagger, (int)ImpactNotApplied.EngineRefused] == 1 && !refused.Impact.Reeling(t),
                    "the engine refusing both ways: calls " + (body.Staggers.Count - calls) + ", his record " + refused.Impact.Reeling(t));

                // ---- 8. the GUARD timed: a half-tired blocker's stun x1.2 vs a fresh one's (the control)
                var guard = Man(6090, 125, 0.6);
                var guardFresh = Man(6091, 125, 1.0);
                foreach (var (g, seconds) in new[] { (guard, 0.36), (guardFresh, 0.3) })
                {
                    dice.Next(0.999, 0.999, 0.999);
                    body.Actions[g.AgentIndex] = Ch1(2, 60);
                    logic.ImpactModelHitTaken(attacker.Agent, g.Agent, RightBlock());
                    double gs = t;
                    Tick();
                    while (t - gs < seconds + 0.05)
                    {
                        if (t - gs >= seconds) body.Actions[g.AgentIndex] = Ch1(2, 99);
                        Tick();
                    }
                    body.Actions.Remove(g.AgentIndex);
                }
                Check(bl.GuardMeasured.Count == 1 && bl.GuardControl.Count == 1 && Within(bl.GuardAsked.Mean, 1.2, 0.01) && bl.GuardMeasured.Mean > bl.GuardControl.Mean,
                    "the guard timing: asked " + bl.GuardAsked.Mean + ", " + bl.GuardMeasured.Mean + " vs " + bl.GuardControl.Mean);

                // ---- 9. SHADOW: decided, nothing queued; the riposte OFF: none; blocks OFF + the riposte on: the riposte alone
                S.Set(SettingsSchema.ImpactShadowMode, true, SettingSources.Mcm);
                Tick();
                Check(StunCap == 0.8f && !ImpactHooks.StunCapRaised, "shadow: the game's stun cap not given back");
                var sh = Man(6100, 125, 0.0);
                dice.Next(0.999, 0.0, 0.5);
                logic.ImpactModelHitTaken(attacker.Agent, sh.Agent, RightBlock());
                Check(logic.LastImpact.Stagger.Happens && logic.ImpactBlockPendingCount == 0, "shadow mode queued a stagger");
                Check(Near(StunFromWorker(model, attacker, tired, 0.1f, 0.3f).Def, 0.3f), "shadow mode lengthened a stun");
                S.Set(SettingsSchema.ImpactShadowMode, false, SettingSources.Mcm);
                Tick(3);
                Check(StunCap == 2f, "back live: the cap not raised again");
                S.Set(SettingsSchema.ImpactRiposteEnabled, false, SettingSources.Mcm);
                var recruit2 = Man(6042, 62, 0.4);
                inputs.Weapons[6042] = (1.2, 92, ImpactDamageType.Cut, 40);
                logic.ImpactModelHitTaken(recruit2.Agent, veteran.Agent, RightBlock());
                Check(!logic.LastImpact.Riposte.InRow, "ImpactRiposteEnabled off: a riposte was asked");
                S.Set(SettingsSchema.ImpactRiposteEnabled, true, SettingSources.Mcm);
                S.Set(SettingsSchema.ImpactBlocksEnabled, false, SettingSources.Mcm);
                Tick();
                Check(StunCap == 0.8f && Near(StunFromWorker(model, attacker, tired, 0.1f, 0.3f).Def, 0.3f), "blocks off: the stun still lengthened / the cap kept");
                dice.Next(0.0, 0.5);
                logic.ImpactModelHitTaken(recruit2.Agent, veteran.Agent, RightBlock());
                d = logic.LastImpact;
                Check(d.Riposte.Happens && !d.Stagger.InRow && double.IsNaN(d.StunFactor) && logic.ImpactBlockPendingCount == 1, "blocks off, the riposte on: not the riposte alone");
                Tick();
                Check(body.Staggers.Exists(s => s.Man == 6042) && bl.RiposteAppliedTotal == 2, "the riposte alone was not made from the tick");
                S.Set(SettingsSchema.ImpactBlocksEnabled, true, SettingSources.Mcm);
                Tick(1);

                // ---- 10. a failing lever disables itself alone: the stagger, our blow, the stun seam (a worker thread's error)
                body.ThrowOnStagger = true;
                var f1 = Man(6110, 125, 0.0);
                dice.Next(0.999, 0.0, 0.5);
                logic.ImpactModelHitTaken(attacker.Agent, f1.Agent, RightBlock());
                Tick();
                body.ThrowOnStagger = false;
                var live = logic.ImpactLiveStats;
                Check(live.Disabled(ImpactLever.ForcedStagger) && !live.Disabled(ImpactLever.OwnBlow) && !f1.Impact.Reeling(t), "a throwing stagger did not disable its lever alone");
                LogHas("smoke: SetActionChannel failed");
                LogHas("[impact] live lever the forced stagger (blocks, the riposte) disabled for this mission after an error at impact.live-stagger");
                var f2 = Man(6111, 125, 0.0);
                dice.Next(0.999, 0.0, 0.5);
                logic.ImpactModelHitTaken(attacker.Agent, f2.Agent, RightBlock());
                Check(logic.ImpactBlockPendingCount == 0 && !f2.Impact.Reeling(t) && bl.NotApplied[(int)ImpactOutcome.Stagger, (int)ImpactNotApplied.LeverOff] >= 1, "a stagger queued after its lever went");
                var f3 = Man(6112, 125, 0.0);
                dice.Next(0.0, 0.5);
                logic.ImpactModelHitTaken(attacker.Agent, f3.Agent, RightBlock());
                Tick();
                Check(bl.OwnBlows == 2, "our blow stopped with the stagger's lever");
                body.ThrowOnOwnBlow = true;
                var f4 = Man(6113, 125, 0.0);
                dice.Next(0.0, 0.5);
                logic.ImpactModelHitTaken(attacker.Agent, f4.Agent, RightBlock());
                Tick();
                body.ThrowOnOwnBlow = false;
                Check(live.Disabled(ImpactLever.OwnBlow) && !f4.Impact.Down(t), "a throwing blow did not disable its lever (or his record lies down)");
                LogHas("[impact] live lever the blocker's knockdown (our own blow) disabled for this mission after an error at impact.live-own-blow");
                ImpactHooks.BlockStun = new ThrowingStun();
                var s2 = StunFromWorker(model, attacker, tired, 0.1f, 0.3f);
                ImpactHooks.BlockStun = logic;
                Check(Near(s2.Def, 0.3f), "a throwing stun seam's write survived");
                Tick();
                Check(live.Disabled(ImpactLever.BlockStun) && StunCap == 0.8f && Near(StunFromWorker(model, attacker, tired, 0.1f, 0.3f).Def, 0.3f),
                    "the stun seam's error did not disable it and give the cap back");
                LogHas("[impact] live lever the block stun by tiredness disabled for this mission after an error at impact.live-stun");

                // ---- the summary
                logic.WriteImpactSummary();
                LogHas("[summary] impact live right-side shield blocks by the blocker's bar 100-75% / 75-50% / 50-25% / 25-0% (step 51; LIVE - kicks / bashes, blocks, clean hits, the riposte applied");
                LogHas("[summary] impact live WRONG-side shield blocks by the blocker's bar ");
                LogHas("[summary] impact live parries by the blocker's bar ");
                LogHas("[summary] impact live block staggers: asked ");
                LogHas("BLOCKS MADE FROM INSIDE A STAGGER 1 (should be ~0 - the proof a stagger is a real window), hits taken inside one 1");
                LogHas("[summary] impact live blockers knocked down: our own blows registered 2 → Fall seen 1");
                LogHas("[summary] impact live riposte (a fresh blocker staggers the ATTACKER) by the attacker's bar ");
                LogHas("[summary] impact live block stun: the decorator's calls ");
                LogHas("the game's cap 0.80 → 2.00 s (raised ");
                LogHas("[summary] impact live block limits: stagger refused - reeling or within the immunity 1");
                LogHas("levers disabled the block stun by tiredness (at impact.live-stun), the forced stagger (blocks, the riposte) (at impact.live-stagger), the blocker's knockdown (our own blow) (at impact.live-own-blow)");
                Check(StunCap == 0.8f && !ImpactHooks.StunCapRaised, "the summary did not give the game's cap back");
                Console.WriteLine("        step 51: stagger asked " + bl.StaggerAsked.Mean.ToString("0.00", CultureInfo.InvariantCulture) + " s → measured "
                                  + bl.StaggerMeasured.Mean.ToString("0.00", CultureInfo.InvariantCulture) + " s at speed " + bl.StaggerSpeed.Mean.ToString("0.00", CultureInfo.InvariantCulture)
                                  + " (learned animation " + logic.StaggerNaturalSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " s); blocker's get-up "
                                  + bl.BlockGetUpMeasured.Mean.ToString("0.00", CultureInfo.InvariantCulture) + " s (asked x" + bl.BlockGetUpAsked.Mean.ToString("0.00", CultureInfo.InvariantCulture)
                                  + "); riposte " + bl.RiposteAsked.Mean.ToString("0.00", CultureInfo.InvariantCulture) + " → " + bl.RiposteMeasured.Mean.ToString("0.00", CultureInfo.InvariantCulture)
                                  + " s; the guard x" + bl.GuardAsked.Mean.ToString("0.00", CultureInfo.InvariantCulture) + " → " + bl.GuardMeasured.Mean.ToString("0.00", CultureInfo.InvariantCulture)
                                  + " s vs " + bl.GuardControl.Mean.ToString("0.00", CultureInfo.InvariantCulture) + " s; seam calls " + bl.Stun.Calls + " (off the main thread " + bl.Stun.OffMainThread + ")");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                ImpactHooks.RestoreStunCap();
                ImpactHooks.KnockDown = null;
                ImpactHooks.BlockStun = null;
                ImpactHooks.PickupLock = null;
                ManagedParameters.SetParameter(ManagedParametersEnum.StunPeriodMax, capKeep);
                BlocksStunDefaults();
                ManagedParameters.SetParameter(ManagedParametersEnum.StunPeriodMax, capKeep);
                ImpactModelDefaults();
                AthleticsDefaults();
            }
        }

        /// <summary>The cost: a thousand men at every bar, twenty LIVE blocks a tick of the three kinds (staggers, drops and our blows queued
        /// and made from the tick, the watch pool busy) + twenty stun-seam calls a tick and the T snapshot of every man - small, allocating
        /// nothing.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ImpactBlocksLiveCostStaysFlat()
        {
            var keep = _logic;
            float capKeep = StunCap;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                BlocksStunDefaults();
                var inputs = new FakeImpactInputs();
                var body = new FakeImpactBody { Record = false };
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = body;
                logic.ImpactInputs = inputs;
                logic.ImpactDice = new SeededRandom(51);
                var model = SpikeDamageModel();
                const int men = 1000;
                var all = new TrackedAgent[men];
                for (int i = 0; i < men; i++)
                {
                    var m = logic.Track(FakeAgent(11000 + i))!;
                    m.AthleticsSkill = 50 + (i % 200);
                    SetBar(m, (i % 101) / 100.0);
                    inputs.Weapons[11000 + i] = (0.6 + (i % 5) * 0.6, 80 + (i % 20), (ImpactDamageType)(i % 3), 20 + (i % 280));
                    inputs.Guards[11000 + i] = (1 + (i % 6), 20 + (i % 280));
                    if (i % 3 != 0)
                    {
                        inputs.Shields.Add(11000 + i);
                        body.ShieldInHand[11000 + i] = 3;
                    }
                    all[i] = m;
                }
                var kinds = new[] { RightBlock(), WrongBlock(), Parried() };
                int k = 0;
                void Frame(int f)
                {
                    double t = 60000 + f / 60.0;
                    logic.TickImpact(t);
                    for (int e = 0; e < 20; e++, k++)
                    {
                        var a = all[(k * 7) % men];
                        var v = all[(k * 13 + 1) % men];
                        float x = 0.1f, y = 0.3f;
                        model.CalculateDefendedBlowStunMultipliers(a.Agent, v.Agent, CombatCollisionResult.Blocked, null!, null!, ref x, ref y);
                        var cd = kinds[k % kinds.Length];
                        logic.ImpactModelHitTaken(a.Agent, v.Agent, in cd);
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
                var bl = logic.ImpactBlockStats;
                var live = logic.ImpactLiveStats;
                Console.WriteLine("        step 51 cost: " + men + " men, 20 live blocks + 20 stun-seam calls a tick - " + ms.ToString("0.000", CultureInfo.InvariantCulture) + " ms a tick, "
                                  + allocated + " bytes over " + ticks + " ticks; " + bl.LiveDecisions + " live blocks, staggered " + bl.AppliedOf(ImpactOutcome.Stagger) + ", shields dropped "
                                  + bl.AppliedOf(ImpactOutcome.ShieldDrop) + ", knocked down " + bl.OwnBlows + ", ripostes " + bl.RiposteAppliedTotal + ", stuns lengthened " + bl.Stun.Lengthened
                                  + ", deferred " + bl.Deferred + ", live tick " + (live.TickMs / Math.Max(1, live.Ticks)).ToString("0.000", CultureInfo.InvariantCulture) + " ms");
                Check(ms < 0.5, "cost: 20 live blocks a tick over " + men + " men took " + ms + " ms (budget 0.5 ms)");
                Check(allocated < 64 * 1024, "cost: the live blocks allocate - " + allocated + " bytes over " + ticks + " ticks");
                Check(bl.AppliedOf(ImpactOutcome.Stagger) > 100 && bl.AppliedOf(ImpactOutcome.ShieldDrop) > 20 && bl.OwnBlows > 20 && bl.Stun.Lengthened > 1000,
                    "cost: too few live outcomes - staggers " + bl.AppliedOf(ImpactOutcome.Stagger) + ", drops " + bl.AppliedOf(ImpactOutcome.ShieldDrop) + ", blows " + bl.OwnBlows);
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
                ManagedParameters.SetParameter(ManagedParametersEnum.StunPeriodMax, capKeep);
                ImpactModelDefaults();
                AthleticsDefaults();
            }
        }

        /// <summary>The master-switch step's part (step 51): a tired blocker's stun lengthened and a stagger queued; ModEnabled off = the
        /// game's stun at once (before the tick), at the tick the cap back and the queued stagger dropped unplayed; on = live again.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ImpactBlocksFollowTheMasterSwitch()
        {
            var keep = _logic;
            float capKeep = StunCap;
            try
            {
                ImpactModelDefaults();
                BlocksStunDefaults();
                var body = new FakeImpactBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = body;
                logic.ImpactInputs = new FakeImpactInputs();
                var dice = new ScriptDice();
                logic.ImpactDice = dice;
                var model = SpikeDamageModel();
                double t = 23000;
                var a = logic.Track(FakeAgent(2960))!;
                var v = logic.Track(FakeAgent(2961))!;
                SetBar(v, 0.0);
                logic.TickImpact(t);
                Check(StunCap == 2f && Near(StunFromWorker(model, a, v, 0.1f, 0.3f).Def, 0.6f), "precondition: a tired blocker's stun not lengthened (step 51)");
                dice.Next(0.999, 0.0, 0.5);
                logic.ImpactModelHitTaken(a.Agent, v.Agent, RightBlock());
                Check(logic.ImpactBlockPendingCount == 1, "precondition: no stagger queued (step 51)");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                Check(Near(StunFromWorker(model, a, v, 0.1f, 0.3f).Def, 0.3f), "mod off: the stun still lengthened before the tick (step 51)");
                logic.TickImpact(t + 0.02);
                Check(StunCap == 0.8f && logic.ImpactBlockPendingCount == 0 && body.Staggers.Count == 0, "mod off: the cap / the queued stagger not let go at once (step 51)");
                LogHas("[impact] blocks and the riposte no longer live at ");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                logic.TickImpact(t + 0.04);
                Check(StunCap == 2f && Near(StunFromWorker(model, a, v, 0.1f, 0.3f).Def, 0.6f), "mod back on: the blocks not live again (step 51)");
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
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                ManagedParameters.SetParameter(ManagedParametersEnum.StunPeriodMax, capKeep);
            }
        }
    }
}

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
    /// Offline checks for step 52 - IMPACT I5, CLEAN HITS LIVE + THE REELING BONUS + MOMENTUM (AI_NOTES "Step 52"): the REAL damage
    /// decorator's KnockDown seam, the REAL shrug-off hook and the REAL <see cref="AthleticsLogic"/> with the stand-in body (it plays the flinch
    /// / the fall the engine would) - an untouched flinch timed (the control, its length learned); an exhausted man's stagger = his own flinch
    /// slowed to 1.5 s and measured, the end in his record; the REELING BONUS live - a staggered man kicked down, a weapon blow on a staggered
    /// man knocking him down; tired men never shrug off (refused / kept by the threshold x (1 − T), a rider, a kick, the switches); a knockdown on
    /// a shrugged-off blow counted; YOU; a rider (nothing); shadow; a failing slow lever and a throwing shrug-off decider; the master switch; the
    /// summary - then Anton's SHOCK CHARGE (a fresh sprinting axe-man into a tired line vs a standing swordsman) and the cost.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>A shrug-off decider that throws (the hook's fail-safe).</summary>
        private sealed class ThrowingShrugOff : IShrugOffDecider
        {
            public bool ShrugOffKept(Agent victim, in AttackCollisionData collision, float gameThreshold) => throw new InvalidOperationException("smoke: the shrug-off decider failed");
        }

        private static AttackCollisionData CleanHitDealing(int damage)
        {
            var cd = CleanHit();
            cd.InflictedDamage = damage;
            return cd;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ImpactHitsLiveThroughTheLogic()
        {
            var keep = _logic;
            float thresholdKeep = ManagedParameters.GetParameter(ManagedParametersEnum.DamageInterruptAttackThresholdCut);
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                S.Set(SettingsSchema.AthleticsPoolPerSkill, 0.8, SettingSources.File);
                ManagedParameters.SetParameter(ManagedParametersEnum.DamageInterruptAttackThresholdCut, 5f); // the game's shrug-off threshold (managed_core_parameters.xml)
                var inputs = new FakeImpactInputs();
                var body = new FakeImpactBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = body;
                logic.ImpactInputs = inputs;
                var dice = new ScriptDice();
                logic.ImpactDice = dice;
                var hits = logic.ImpactHitStats;
                var live = logic.ImpactLiveStats;
                var model = SpikeDamageModel();
                var game = new CustomAgentApplyDamageModel();
                double t = 32000;
                logic.TickImpact(t);
                LogHas("LIVE - kicks / bashes, blocks, clean hits, the riposte applied; families:");
                LogHas("tired men always flinch on; momentum (the attacker's run into the blow) +60% impact at 7.0 m/s and faster, none at 2.0 m/s or slower; a rider's horse +30% at 10.0 m/s, none at 3.0 m/s (a couched lance never)");

                TrackedAgent Man(int index, double bar)
                {
                    var m = logic.Track(FakeAgent(index))!;
                    m.AthleticsSkill = 125; // pool 100
                    SetBar(m, bar);
                    return m;
                }
                bool Hit(TrackedAgent a, TrackedAgent v, bool shruggedOff = false)
                {
                    var cd = CleanHit();
                    var blow = new Blow(a.AgentIndex);
                    if (shruggedOff) blow.BlowFlag |= BlowFlags.ShrugOff;
                    bool down = model.DecideAgentKnockedDownByBlow(a.Agent, v.Agent, in cd, null!, in blow); // the engine's question, then
                    logic.ImpactModelHitTaken(a.Agent, v.Agent, in cd);                                       // its OnMeleeHit
                    return down;
                }
                bool Kick(TrackedAgent a, TrackedAgent v)
                {
                    var cd = KickLands();
                    var blow = new Blow(a.AgentIndex);
                    bool down = model.DecideAgentKnockedDownByBlow(a.Agent, v.Agent, in cd, null!, in blow);
                    logic.ImpactModelHitTaken(a.Agent, v.Agent, in cd);
                    return down;
                }
                var sword = Man(6000, 1.0);
                inputs.Weapons[6000] = (1.2, 92, ImpactDamageType.Cut, 125);

                // ---- 1. a CONTROL: a clean hit on a fresh even man - no outcome; his untouched flinch timed (0.45 s), its length learned
                var fresh = Man(6001, 0.9);
                Check(!Hit(sword, fresh) && !logic.LastImpact.AnyOutcome && logic.ImpactLiveWatches == 1, "a fresh even man's clean hit: an outcome or no control watch");
                int slows = body.Slows.Count;
                t = PlayReaction(logic, body, fresh, ImpactActions.StrikeLight, t + 1.0 / 60, 0.45, index: 1300);
                Check(body.Slows.Count == slows && hits.FlinchControl.Count == 1 && Math.Abs(hits.FlinchControl.Mean - 0.45) < 0.03 && Math.Abs(logic.FlinchNaturalSeconds - 0.45) < 0.03,
                    "the control flinch: slowed " + (body.Slows.Count - slows) + ", measured " + hits.FlinchControl.Mean + ", learned " + logic.FlinchNaturalSeconds);

                // ---- 2. a STAGGER on an EXHAUSTED man: his own flinch slowed to 1.5 s (x3.33 of 0.45 at speed 0.30), measured, the end his
                var tired = Man(6002, 0.0);
                dice.Next(0.999, 0.0, 0.5); // no knockdown, the stagger, its length at the centre (T 1: 0.6 x 2.5)
                Check(!Hit(sword, tired), "the exhausted man's stagger answered as a knockdown");
                var d = logic.LastImpact;
                Check(d.Stagger.Happens && Math.Abs(d.StaggerSeconds - 1.5) < 1e-5 && d.X >= 1.0, "the exhausted man's stagger: " + d.Stagger.Happens + ", " + d.StaggerSeconds + " s, x " + d.X);
                Check(tired.Impact.Reeling(t + 0.5), "the stagger not on his record (reeling)");
                double natural = logic.FlinchNaturalSeconds;
                slows = body.Slows.Count;
                double end = PlayReaction(logic, body, tired, ImpactActions.StrikeMedium, t + 1.0 / 60, 1.5, index: 1310);
                var mine = body.Slows.Skip(slows).Where(x => x.Man == 6002).ToList();
                Check(mine.Count == 1 && Math.Abs(mine[0].Speed - (float)(natural / 1.5)) < 1e-3, "the flinch not slowed to " + (natural / 1.5) + " the tick it showed: " + string.Join(", ", mine));
                Check(hits.FlinchMeasured.Count == 1 && Math.Abs(hits.FlinchMeasured.Mean - 1.5) < 0.04 && hits.Seen[1] == 1, "the stagger not measured: " + hits.FlinchMeasured.Mean);
                Check(!tired.Impact.Reeling(end) && tired.Impact.StaggerImmune(end + 1.8) && !tired.Impact.StaggerImmune(end + 2.2), "the measured end not in his record");
                LogHas("[impact] live: first clean hit that STAGGERS at ");
                LogHas(" s - (agent 6000)'s cut blow on (agent 6002) (his bar 0%, x ");
                LogHas("[impact] live: first clean-hit flinch seen at ");
                LogHas("[impact] live: first clean-hit flinch slowed at ");
                LogHas("[impact] live: first clean-hit stagger measured at ");
                t = end;

                // ---- 3. THE REELING BONUS live: a man at 70% staggered by a clean hit (x 0.07 - 1% - the dice say yes), then KICKED inside his
                // stagger: x + 0.75 → knocked down (80% x S x the halving); the same kick on a calm man at 70%: nothing
                var kicker = Man(6003, 1.0);
                var calm = Man(6004, 0.7);
                Kick(kicker, calm);
                Check(logic.LastImpact.Knockdown.Chance == 0 && !logic.LastImpact.Reeling, "a kick on a calm man at 70% could knock him down");
                var reel = Man(6005, 0.7);
                dice.Next(0.0, 0.5); // the stagger (no knockdown die - its chance is 0), its length
                Hit(sword, reel);
                Check(logic.LastImpact.Stagger.Happens && reel.Impact.Reeling(t + 0.1), "the 70% man was not staggered");
                dice.Next(0.0, 0.5); // the kick: down, the get-up roll
                bool kickedDown = Kick(kicker, reel);
                d = logic.LastImpact;
                Check(kickedDown && d.Reeling && Math.Abs(d.X - (1 - 0.7 / 0.75) - 0.75) < 0.01 && d.Knockdown.Halvings == 1 && d.Knockdown.Chance > 0.25,
                    "a STAGGERED man was not kicked down (x " + d.X + ", reeling " + d.Reeling + ", chance " + d.Knockdown.Chance + ")");
                // ... and a WEAPON blow on a staggered man can knock him down too
                var reel2 = Man(6006, 0.7);
                dice.Next(0.0, 0.5);
                Hit(sword, reel2);
                dice.Next(0.0, 0.5);
                var axe = Man(6007, 1.0);
                inputs.Weapons[6007] = (3.0, 80, ImpactDamageType.Cut, 125);
                inputs.TwoHanded.Add(6007);
                bool hitDown = Hit(axe, reel2);
                d = logic.LastImpact;
                Check(hitDown && d.Reeling && d.Knockdown.Happens && d.Stagger.Refusal == ImpactRefusal.Replaced && hits.KnockdownsByReeling[1] == 1,
                    "a weapon blow on a staggered man did not knock him down (x " + d.X + ", chance " + d.Knockdown.Chance + ")");
                LogHas("[impact] live: first clean hit that KNOCKS DOWN at ");
                LogHas(", REELING +0.75");
                // let the watches run out (no reactions played)
                TickFor(logic, ref t, 1.2);

                // ---- 4. TIRED MEN NEVER SHRUG OFF: the real hook asks the base model's threshold (5) - x (1 − T)
                var empty = Man(6008, 0.0);
                var half = Man(6009, 0.375); // f 0.5, T 0.5
                var cd3 = CleanHitDealing(3);
                var blow3 = new Blow(sword.AgentIndex);
                Check(!ImpactHooks.ShrugOffKept(empty.Agent, in cd3, in blow3, game), "an exhausted man shrugged off a 3-damage blow");
                Check(!ImpactHooks.ShrugOffKept(half.Agent, in cd3, in blow3, game), "a half-tired man shrugged off a 3-damage blow (threshold 2.5)");
                var cd2 = CleanHitDealing(2);
                Check(ImpactHooks.ShrugOffKept(half.Agent, in cd2, in blow3, game), "a half-tired man did not shrug off a 2-damage blow");
                Check(ImpactHooks.ShrugOffKept(fresh.Agent, in cd3, in blow3, game), "a fresh man's shrug-off was refused");
                var kickCd = KickLands();
                kickCd.InflictedDamage = 2;
                Check(ImpactHooks.ShrugOffKept(empty.Agent, in kickCd, in blow3, game), "a KICK's shrug-off was refused (clean hits only)");
                var riderMan = Man(6010, 0.0);
                body.Mounted.Add(6010);
                Check(ImpactHooks.ShrugOffKept(riderMan.Agent, in cd3, in blow3, game), "a rider's shrug-off was refused (step 53)");
                S.Set(SettingsSchema.ImpactTiredNeverShrugsOff, false, SettingSources.Mcm);
                Check(ImpactHooks.ShrugOffKept(empty.Agent, in cd3, in blow3, game), "ImpactTiredNeverShrugsOff off: still refused");
                S.Set(SettingsSchema.ImpactTiredNeverShrugsOff, true, SettingSources.Mcm);
                S.Set(SettingsSchema.ImpactHitsEnabled, false, SettingSources.Mcm);
                Check(ImpactHooks.ShrugOffKept(empty.Agent, in cd3, in blow3, game), "ImpactHitsEnabled off: still refused");
                S.Set(SettingsSchema.ImpactHitsEnabled, true, SettingSources.Mcm);
                Check(hits.ShrugAskedTotal == 3 && hits.ShrugRefusedTotal == 2, "the shrug-offs not counted: asked " + hits.ShrugAskedTotal + ", refused " + hits.ShrugRefusedTotal);
                LogHas("[impact] live: first shrug-off REFUSED at ");
                LogHas("the game's threshold 5.0 x (1 - T) = 0.0 - a tired man flinches (ImpactTiredNeverShrugsOff)");
                // a knockdown answered on a blow the game had shrugged off is counted (whether the engine plays it: PLAYTEST D21)
                var shrugged = Man(6011, 0.0);
                dice.Next(0.0, 0.5);
                Check(Hit(sword, shrugged, shruggedOff: true) && hits.KnockdownOnShrugOff == 1, "a knockdown on a shrugged-off blow not counted");

                // ---- 5. YOU knocked down by a clean hit (the YOU line); you hit a man down
                var you = Man(6012, 0.0);
                logic.SmokePlayer = you.Agent;
                dice.Next(0.0, 0.5);
                Check(Hit(sword, you), "your knockdown not answered");
                LogHas("[impact] YOU: (agent 6000)'s blow on you: you are knocked down (50%; your bar 0%, x ");
                inputs.Weapons[6012] = (1.2, 92, ImpactDamageType.Cut, 125);
                dice.Next(0.999, 0.0, 0.5);
                Hit(you, Man(6013, 0.0));
                LogHas("[impact] YOU: your blow on (agent 6013): he is staggered (80%; his bar 0%, x ");
                logic.SmokePlayer = null;

                // ---- 6. a RIDER: step 53 - the KnockDown seam never answers him (his is the dismount seam - Program.ImpactRiders.cs), no flinch watched
                var rider = Man(6014, 0.0);
                inputs.Mounted.Add(6014);
                int watches = logic.ImpactLiveWatches;
                dice.Next(0.0);
                Check(!Hit(sword, rider) && logic.ImpactLiveWatches == watches && hits.NotApplied[0, (int)ImpactNotApplied.Rider] == 0 && logic.ImpactRiderStats.Decisions[2] == 1,
                    "a clean hit on a rider: answered in the KnockDown seam, watched, or not the riders' (step 53)");

                // ---- 7. SHADOW: decided and logged, nothing answered or watched
                S.Set(SettingsSchema.ImpactShadowMode, true, SettingSources.Mcm);
                logic.TickImpact(t += 1.0 / 60);
                var shadowed = Man(6015, 0.0);
                watches = logic.ImpactLiveWatches;
                dice.Next(0.0, 0.5);
                Check(!Hit(sword, shadowed) && logic.LastImpact.Knockdown.Happens && logic.ImpactLiveWatches == watches && shadowed.Impact.Down(t + 0.5),
                    "shadow mode applied a clean hit (or did not keep the 'would' record)");
                Check(ImpactHooks.ShrugOffKept(empty.Agent, in cd3, in blow3, game), "shadow mode refused a shrug-off");
                S.Set(SettingsSchema.ImpactShadowMode, false, SettingSources.Mcm);
                logic.TickImpact(t += 1.0 / 60);

                // ---- 8. a failing lever: SetCurrentActionSpeed throws on a hit's flinch - that lever off alone, the knockdown still answered;
                // a throwing shrug-off decider = the game's answer, one [error]
                body.ThrowOnSlow = true;
                var slowFails = Man(6016, 0.0);
                dice.Next(0.999, 0.0, 0.5);
                Hit(sword, slowFails);
                t = PlayReaction(logic, body, slowFails, ImpactActions.StrikeMedium, t + 1.0 / 60, 0.45, index: 1400);
                body.ThrowOnSlow = false;
                Check(live.Disabled(ImpactLever.SlowReaction) && hits.FlinchControl.Count == 2, "a throwing slow on a hit's flinch: the lever not off / the flinch not measured untouched");
                LogHas("[impact] live lever the slowed reel / get-up disabled for this mission after an error at impact.live-slow");
                dice.Next(0.999, 0.0, 0.5);
                Hit(sword, Man(6017, 0.0));
                Check(hits.NotApplied[1, (int)ImpactNotApplied.LeverOff] == 1, "the slow lever off: a stagger still applied");
                dice.Next(0.0, 0.5);
                Check(Hit(sword, Man(6018, 0.0)), "the slow lever off: the knockdown no longer answered");
                var plugged = ImpactHooks.ShrugOff;
                ImpactHooks.ShrugOff = new ThrowingShrugOff();
                Check(ImpactHooks.ShrugOffKept(empty.Agent, in cd3, in blow3, game), "a throwing shrug-off decider was not the game's answer");
                ImpactHooks.ShrugOff = plugged;
                LogHas("smoke: the shrug-off decider failed");

                // ---- 9. the master switch: off = nothing answered, the shrug-off the game's, the hits' watches let go at once; on = live again
                dice.Next(0.0, 0.5);
                Hit(sword, Man(6019, 0.0)); // a get-up watch running
                Check(logic.ImpactLiveWatches >= 1, "no hit watch to let go");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                logic.TickImpact(t += 1.0 / 60);
                Check(logic.ImpactLiveWatches == 0, "mod off: the hits' watches not let go");
                LogHas("[impact] clean hits no longer live at ");
                dice.Next(0.0, 0.5);
                Check(!Hit(sword, Man(6020, 0.0)), "mod off: a clean hit knocked down");
                Check(ImpactHooks.ShrugOffKept(empty.Agent, in cd3, in blow3, game), "mod off: a shrug-off refused");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                logic.TickImpact(t += 1.0 / 60);
                dice.Next(0.0, 0.5);
                Check(Hit(sword, Man(6021, 0.0)), "mod back on: the clean hit's knockdown not answered");

                // ---- the summary
                logic.WriteImpactSummary();
                LogHas("[summary] impact momentum (step 52 - every decided kick, bash, block and clean hit by the attacker's speed into the blow");
                LogHas("[summary] impact live clean hits, cut, by the hit man's bar 100-75% / 75-50% / 50-25% / 25-0% (step 52; LIVE - kicks / bashes, blocks, clean hits, the riposte applied): ");
                LogHas("[summary] impact live clean hits, two-handed, by the hit man's bar ");
                LogHas("[summary] impact live clean-hit knockdowns: on men REELING from an impact stagger 1 of ");
                LogHas("knockdowns answered on a blow the game had shrugged off 1");
                LogHas("[summary] impact live clean-hit staggers (the engine's own flinch slowed): asked 1.50 s (n 1)");
                LogHas("[summary] impact live shrug-offs (ImpactTiredNeverShrugsOff - the game's threshold x f): the game let a tired man shrug a clean hit off 3 times");
                LogHas("[summary] impact live clean-hit limits: ");
                LogHas("[summary] impact clean hits by the hit man's bar ");
                Console.WriteLine("        step 52: the control flinch " + hits.FlinchControl.Mean.ToString("0.00", CultureInfo.InvariantCulture) + " s; the exhausted man's stagger asked 1.50 s → measured "
                                  + hits.FlinchMeasured.Mean.ToString("0.00", CultureInfo.InvariantCulture) + " s; shrug-offs refused " + hits.ShrugRefusedTotal + " of " + hits.ShrugAskedTotal
                                  + "; knockdowns on reeling men " + hits.KnockdownsByReeling[1] + " of " + hits.HitsByReeling[1]);
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
                S.Set(SettingsSchema.ImpactTiredNeverShrugsOff, true, SettingSources.File);
                S.Set(SettingsSchema.ImpactHitsEnabled, true, SettingSources.File);
                AthleticsDefaults();
            }
        }

        /// <summary>
        /// Anton's SHOCK CHARGE (2026-10-17: "a fresh shock unit attacking a tired line must be devastating"): lines of 200 men, each hit ONCE
        /// through the real decorator and logic with seeded dice - a STANDING fresh swordsman into a half-tired line (bar 40 %) vs a fresh
        /// SPRINTING (8 m/s) two-handed axe-man into the same line, the same axe standing, and the sprinting axe into an EXHAUSTED line; a
        /// sprinting kick vs a standing one; the momentum summary line by speed band - the proof that a charge throws tired men around.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ImpactShockChargeThrowsTiredMenAround()
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
                logic.ImpactDice = new SeededRandom(52);
                var model = SpikeDamageModel();
                double t = 34000;
                logic.TickImpact(t);
                int next = 7000;
                TrackedAgent Man(double bar)
                {
                    var m = logic.Track(FakeAgent(next++))!;
                    m.AthleticsSkill = 125; // pool 100
                    SetBar(m, bar);
                    return m;
                }
                var swordsman = Man(1.0);
                inputs.Weapons[swordsman.AgentIndex] = (1.2, 92, ImpactDamageType.Cut, 125);
                var axeStanding = Man(1.0);
                inputs.Weapons[axeStanding.AgentIndex] = (3.0, 80, ImpactDamageType.Cut, 125);
                inputs.TwoHanded.Add(axeStanding.AgentIndex);
                var axeCharging = Man(1.0);
                inputs.Weapons[axeCharging.AgentIndex] = (3.0, 80, ImpactDamageType.Cut, 125);
                inputs.TwoHanded.Add(axeCharging.AgentIndex);
                inputs.Speeds[axeCharging.AgentIndex] = 8.0; // a fresh man's sprint (step 33/35: x1.8-2 of a ~4.4 m/s run)
                var kickerStanding = Man(1.0);
                var kickerCharging = Man(1.0);
                inputs.Speeds[kickerCharging.AgentIndex] = 8.0;

                (double Down, double Any) Line(TrackedAgent attacker, double bar, bool kick, int men = 200)
                {
                    int down = 0, any = 0;
                    var cd = kick ? KickLands() : CleanHit();
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
                var swordHalf = Line(swordsman, 0.4, false);
                var axeHalf = Line(axeStanding, 0.4, false);
                var chargeHalf = Line(axeCharging, 0.4, false);
                var chargeEmpty = Line(axeCharging, 0.05, false);
                var kickHalf = Line(kickerStanding, 0.4, true);
                var kickChargeHalf = Line(kickerCharging, 0.4, true);
                string P(double v) => (100 * v).ToString("0", CultureInfo.InvariantCulture) + "%";
                Console.WriteLine("        step 52 SHOCK CHARGE (200 men each, hit once): a standing swordsman into a 40% line - down " + P(swordHalf.Down) + ", any outcome " + P(swordHalf.Any)
                                  + "; a standing two-handed axe - down " + P(axeHalf.Down) + ", any " + P(axeHalf.Any) + "; the axe SPRINTING (8 m/s) - down " + P(chargeHalf.Down) + ", any " + P(chargeHalf.Any)
                                  + "; the sprinting axe into an EXHAUSTED (5%) line - down " + P(chargeEmpty.Down) + ", any " + P(chargeEmpty.Any) + "; kicks into the 40% line standing - down "
                                  + P(kickHalf.Down) + ", sprinting - down " + P(kickChargeHalf.Down));
                Check(chargeHalf.Down >= 0.2 && chargeHalf.Down >= 3 * Math.Max(0.01, swordHalf.Down) && chargeHalf.Down > axeHalf.Down + 0.05,
                    "the charge does not throw a tired line around: sword " + P(swordHalf.Down) + ", axe standing " + P(axeHalf.Down) + ", axe sprinting " + P(chargeHalf.Down));
                Check(chargeHalf.Any > axeHalf.Any, "momentum did not raise the outcome rate");
                Check(chargeEmpty.Down >= 0.35, "the sprinting axe into an exhausted line: down only " + P(chargeEmpty.Down));
                Check(kickChargeHalf.Down > kickHalf.Down, "a sprinting kick no stronger than a standing one");
                var hits = logic.ImpactHitStats;
                Check(hits.Blows[3] == 600 && hits.Blows[0] == 600 && hits.Down[3] > 2 * hits.Down[0], "the momentum stats by speed band: " + string.Join(" / ", hits.Blows) + ", down " + string.Join(" / ", hits.Down));
                logic.WriteImpactSummary();
                LogHas("[summary] impact momentum (step 52 - every decided kick, bash, block and clean hit by the attacker's speed into the blow");
                LogHas("| sprinting 600 (8.0 m/s, impact x1.60): an outcome ");
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

        /// <summary>The cost: a thousand men at every bar, twenty LIVE clean hits a tick through the decorator's seam (half of the attackers
        /// running or sprinting) and twenty shrug-off questions through the real hook - small and allocating nothing.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ImpactHitsLiveCostStaysFlat()
        {
            var keep = _logic;
            float thresholdKeep = ManagedParameters.GetParameter(ManagedParametersEnum.DamageInterruptAttackThresholdCut);
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                ManagedParameters.SetParameter(ManagedParametersEnum.DamageInterruptAttackThresholdCut, 5f);
                var inputs = new FakeImpactInputs();
                var body = new FakeImpactBody { Record = false };
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = body;
                logic.ImpactInputs = inputs;
                logic.ImpactDice = new SeededRandom(520);
                var model = SpikeDamageModel();
                var game = new CustomAgentApplyDamageModel();
                const int men = 1000;
                var all = new TrackedAgent[men];
                for (int i = 0; i < men; i++)
                {
                    var m = logic.Track(FakeAgent(11000 + i))!;
                    m.AthleticsSkill = 50 + (i % 200);
                    SetBar(m, (i % 101) / 100.0);
                    inputs.Weapons[11000 + i] = (0.6 + (i % 5) * 0.6, 80 + (i % 20), (ImpactDamageType)(i % 3), 20 + (i % 280));
                    if (i % 4 == 0) inputs.TwoHanded.Add(11000 + i);
                    if (i % 2 == 0) inputs.Speeds[11000 + i] = (i % 10);
                    all[i] = m;
                }
                var hit = CleanHitDealing(3);
                int k = 0;
                void Frame(int f)
                {
                    double t = 45000 + f / 60.0;
                    logic.TickImpact(t);
                    for (int e = 0; e < 20; e++, k++)
                    {
                        var a = all[(k * 7) % men];
                        var v = all[(k * 13 + 1) % men];
                        var blow = new Blow(a.AgentIndex);
                        ImpactHooks.ShrugOffKept(v.Agent, in hit, in blow, game);
                        model.DecideAgentKnockedDownByBlow(a.Agent, v.Agent, in hit, null!, in blow);
                        logic.ImpactModelHitTaken(a.Agent, v.Agent, in hit);
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
                var hits = logic.ImpactHitStats;
                Console.WriteLine("        step 52 cost: " + men + " men, 20 live clean hits + 20 shrug-off questions a tick - " + ms.ToString("0.000", CultureInfo.InvariantCulture) + " ms a tick, "
                                  + allocated + " bytes over " + ticks + " ticks; live hits " + hits.LiveDecisions + ", knocked down " + hits.AppliedOf(0) + ", staggered " + hits.AppliedOf(1)
                                  + ", shrug-offs refused " + hits.ShrugRefusedTotal + " of " + hits.ShrugAskedTotal + ", pool full " + hits.WatchesFull);
                Check(ms < 0.5, "cost: 20 live clean hits a tick over " + men + " men took " + ms + " ms (budget 0.5 ms)");
                Check(allocated < 64 * 1024, "cost: the live clean hits allocate - " + allocated + " bytes over " + ticks + " ticks");
                Check(hits.AppliedOf(0) > 100 && hits.AppliedOf(1) > 100 && hits.ShrugRefusedTotal > 100, "cost: too few live outcomes - " + hits.AppliedOf(0) + " / " + hits.AppliedOf(1) + " / " + hits.ShrugRefusedTotal);
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
                ImpactModelDefaults();
                AthleticsDefaults();
            }
        }

        /// <summary>The master-switch step's clean-hit part: off = a clean hit's knockdown not answered and a tired man's shrug-off the game's;
        /// back on = live again.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ImpactHitsFollowTheMasterSwitch()
        {
            var keep = _logic;
            float thresholdKeep = ManagedParameters.GetParameter(ManagedParametersEnum.DamageInterruptAttackThresholdCut);
            try
            {
                ImpactModelDefaults();
                ManagedParameters.SetParameter(ManagedParametersEnum.DamageInterruptAttackThresholdCut, 5f);
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = new FakeImpactBody();
                logic.ImpactInputs = new FakeImpactInputs();
                var dice = new ScriptDice();
                logic.ImpactDice = dice;
                var model = SpikeDamageModel();
                var game = new CustomAgentApplyDamageModel();
                double t = 24000;
                var a = logic.Track(FakeAgent(2970))!;
                var v = logic.Track(FakeAgent(2971))!;
                SetBar(v, 0.0);
                logic.TickImpact(t);
                var cd = CleanHitDealing(3);
                var blow = new Blow(a.AgentIndex);
                Check(!ImpactHooks.ShrugOffKept(v.Agent, in cd, in blow, game), "precondition: an exhausted man's shrug-off not refused (step 52)");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                Check(ImpactHooks.ShrugOffKept(v.Agent, in cd, in blow, game), "mod off: a shrug-off refused (step 52)");
                dice.Next(0.0, 0.5);
                Check(!model.DecideAgentKnockedDownByBlow(a.Agent, v.Agent, in cd, null!, in blow), "mod off: a clean hit knocked down (step 52)");
                logic.ImpactModelHitTaken(a.Agent, v.Agent, in cd);
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                logic.TickImpact(t + 0.02);
                dice.Next(0.0, 0.5);
                var w = logic.Track(FakeAgent(2972))!;
                SetBar(w, 0.0);
                Check(model.DecideAgentKnockedDownByBlow(a.Agent, w.Agent, in cd, null!, in blow), "mod back on: the clean hit's knockdown not answered (step 52)");
                logic.ImpactModelHitTaken(a.Agent, w.Agent, in cd);
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
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                ManagedParameters.SetParameter(ManagedParametersEnum.DamageInterruptAttackThresholdCut, thresholdKeep);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;
using TraxCombat.Models;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 48 - IMPACT I2, THE MODEL (AI_NOTES "Step 48", docs/IMPACT.md §3-§6): the REAL damage decorator's KnockDown
    /// seam and the REAL <see cref="AthleticsLogic"/> decide stand-in events (a kick, a bash, a right-side block, a wrong-side block, a parry,
    /// a clean hit, a rider, you) with a stand-in for the engine's reads (<see cref="IImpactInputs"/>: the weapon, the guard, the skills, the
    /// mounts) and both men's REAL Athletics records - one decision per event (a landed blow decided in the decorator, taken by OnMeleeHit's
    /// part), the worked examples' numbers through the logic, nothing applied in step 48 (the KnockDown answer stays the game's), shadow vs
    /// live, the family switches, the spike forcing shadow, the master switch, a failing read, the lines and the summary - and the cost.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>The smoke's stand-in for the engine's reads: per attacker index what he strikes with, per defender index his guard, the
        /// shields in hand, the riders, the damage; a throw on demand.</summary>
        private sealed class FakeImpactInputs : IImpactInputs
        {
            public readonly Dictionary<int, (double Kg, double Speed, ImpactDamageType Type, double Skill)> Weapons = new Dictionary<int, (double, double, ImpactDamageType, double)>();
            public readonly Dictionary<int, (double Kg, double Skill)> Guards = new Dictionary<int, (double, double)>();
            public readonly HashSet<int> Shields = new HashSet<int>();
            public readonly HashSet<int> Mounted = new HashSet<int>();
            public readonly Dictionary<int, double> Speeds = new Dictionary<int, double>(); // step 52: the attacker's speed into the blow (m/s)
            public readonly HashSet<int> TwoHanded = new HashSet<int>(); // step 52: a two-handed grip
            public readonly HashSet<int> Couched = new HashSet<int>(); // step 53: a couched lance (a passive attack)
            public double Damage { get; set; }
            public bool Throw;
            public int Calls;

            public void Fill(Agent attacker, Agent victim, in AttackCollisionData cd, ref ImpactFacts f)
            {
                Calls++;
                if (Throw) throw new InvalidOperationException("smoke: the weapon read failed");
                f.AttackerMounted = Mounted.Contains(attacker.Index);
                f.DefenderMounted = Mounted.Contains(victim.Index);
                f.DefenderHasShield = Shields.Contains(victim.Index);
                f.Damage = Damage;
                f.DefenderMaxHealth = 100;
                f.AttackerSpeed = Speeds.TryGetValue(attacker.Index, out var v) ? v : 0; // step 53: a rider's = his horse's
                f.AttackerPassive = f.AttackerMounted && Couched.Contains(attacker.Index);
                f.TwoHanded = TwoHanded.Contains(attacker.Index);
                if (f.Event != ImpactEvent.Kick && Weapons.TryGetValue(attacker.Index, out var w))
                {
                    f.WeaponWeight = w.Kg;
                    f.SwingSpeed = w.Speed;
                    f.DamageType = w.Type;
                    f.AttackerSkill = w.Skill;
                }
                if (ImpactMath.IsBlock(f.Event) && Guards.TryGetValue(victim.Index, out var g))
                {
                    f.GuardWeight = g.Kg;
                    f.DefenderSkill = g.Skill;
                }
            }

            /// <summary>Step 51: the weapon's name for a YOU line ("a test axe" when the attacker has a weapon, else none).</summary>
            public string? BlowName(Agent attacker, in AttackCollisionData cd) => Weapons.ContainsKey(attacker.Index) ? "test axe" : null;
        }

        /// <summary>Dice that answer the given numbers in order, then <see cref="Rest"/>.</summary>
        private sealed class ScriptDice : IRandomSource
        {
            private readonly Queue<double> _q = new Queue<double>();
            public double Rest = 0.999;

            public void Next(params double[] values)
            {
                _q.Clear();
                foreach (var v in values) _q.Enqueue(v);
            }

            public double NextDouble() => _q.Count > 0 ? _q.Dequeue() : Rest;
        }

        /// <summary>Step 48: every logic built from here on reads the impact model's engine inputs through the stand-in (the game's reads the
        /// wielded indices through native pointers - a fake agent would crash the process).</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UseFakeImpactInputs()
        {
            Check(ImpactInputsDefault.Current is GameImpactInputs, "the default impact inputs are not the game's: " + ImpactInputsDefault.Current.GetType().Name);
            ImpactInputsDefault.Current = new FakeImpactInputs();
            Check(new AthleticsLogic().ImpactInputs == ImpactInputsDefault.Current, "a new logic does not start with the default impact inputs");
            Check(GameImpactInputs.TypeOf((int)DamageTypes.Cut) == ImpactDamageType.Cut && GameImpactInputs.TypeOf((int)DamageTypes.Pierce) == ImpactDamageType.Pierce
                  && GameImpactInputs.TypeOf((int)DamageTypes.Blunt) == ImpactDamageType.Blunt && GameImpactInputs.TypeOf((int)DamageTypes.Invalid) == ImpactDamageType.Cut,
                "the game's DamageTypes do not map onto the model's");
            Check((int)StrikeType.Thrust == 1 && (int)StrikeType.Swing == 0, "the game's StrikeType moved");
        }

        /// <summary>The model on, live (no shadow), the spike off.</summary>
        private static void ImpactModelDefaults()
        {
            S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.ImpactEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.ImpactShadowMode, false, SettingSources.File);
            S.Set(SettingsSchema.ImpactSpike, false, SettingSources.File);
            S.Set(SettingsSchema.VerboseLogging, false, SettingSources.File);
        }

        private static void SetBar(TrackedAgent st, double fraction) =>
            typeof(Fighter).GetProperty("Fraction")!.GetSetMethod(true)!.Invoke(st, new object[] { fraction });

        private static AttackCollisionData Collision(bool alternative, bool shieldBlock, bool correctSide, CombatCollisionResult result, int slot, int damageType = 0, int strike = 0) =>
            AttackCollisionData.GetAttackCollisionDataForDebugPurpose(shieldBlock, correctSide, alternative, true, false, false, false, false, false, false, false, false,
                result, slot, strike, damageType, 0, default(BoneBodyPartType), 0, default(Agent.UsageDirection), 0, default(CombatHitResultFlags), 0.5f, 0.5f, 0f, 0f, 0f, 0f, 0f, 0f,
                Vec3.Zero, Vec3.Zero, Vec3.Zero, Vec3.Zero, Vec3.Zero, Vec3.Zero, Vec3.Zero);

        private static AttackCollisionData KickLands() => Collision(true, false, false, CombatCollisionResult.StrikeAgent, -1, 2);
        private static AttackCollisionData BashLands() => Collision(true, false, false, CombatCollisionResult.StrikeAgent, 3, 2);
        private static AttackCollisionData RightBlock() => Collision(false, true, true, CombatCollisionResult.Blocked, 0);
        private static AttackCollisionData WrongBlock() => Collision(false, true, false, CombatCollisionResult.Blocked, 0);
        private static AttackCollisionData Parried() => Collision(false, false, false, CombatCollisionResult.Parried, 0);
        private static AttackCollisionData CleanHit() => Collision(false, false, false, CombatCollisionResult.StrikeAgent, 0);

        private static bool Close(double v, double target, double tol) => Math.Abs(v - target) <= tol;

        /// <summary>
        /// The model through the real decorator and logic: the start line; a landed kick decided in the decorator's KnockDown seam (the
        /// answer stays the game's - nothing applies in step 48 - though the dice knocked him down) and taken by OnMeleeHit's part (ONE
        /// decision); IMPACT §4 B and A's numbers through the real records; a bash; a rider; you at 0 %; the shadow state (on the ground,
        /// stagger immunity, the halving); shadow vs live; the family switch; the spike forcing shadow; a failing read; the lines; the summary.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ImpactModelThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                S.Set(SettingsSchema.AthleticsPoolPerSkill, 0.8, SettingSources.File); // so a 250-skill king has a 200 pool (IMPACT §4 B)
                S.Set(SettingsSchema.AttackAnimationMinPercent, 85, SettingSources.File); // step 20b's tired swing: 0.85 + 0.15 f (IMPACT §3.3)
                var inputs = new FakeImpactInputs();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = new FakeImpactBody();
                logic.ImpactInputs = inputs;
                var dice = new ScriptDice();
                logic.ImpactDice = dice;
                var stats = logic.ImpactModelStats;
                var model = SpikeDamageModel();
                LogHas("[impact] mission start: the model (step 48 - IMPACT I2, docs/IMPACT.md) LIVE - kicks / bashes, blocks, clean hits, the riposte applied; families: kicks / bashes on, blocks on, clean hits on, riposte on; anchors - a kick on an empty even man: shield 90%, down 80%");
                LogHas("two even fresh men: 0% always");
                double t = 19000;
                logic.TickImpact(t);
                Check(logic.ImpactModelOn, "the model is not on");

                TrackedAgent Man(int index, int athletics, double bar)
                {
                    var m = logic.Track(FakeAgent(index))!;
                    m.AthleticsSkill = athletics;
                    SetBar(m, bar);
                    return m;
                }
                // IMPACT §4 B: the 200-at-100% king (skill 250) against 50-at-20% men (skill 60), swords, 3.5 kg shields
                var king = Man(4800, 250, 1.0);
                inputs.Weapons[4800] = (1.2, 95, ImpactDamageType.Cut, 250);
                inputs.Guards[4800] = (3.5, 250);
                inputs.Shields.Add(4800);
                TrackedAgent Peasant(int i)
                {
                    var m = Man(i, 60, 0.2);
                    inputs.Weapons[i] = (1.2, 90, ImpactDamageType.Cut, 60);
                    inputs.Guards[i] = (3.5, 60);
                    inputs.Shields.Add(i);
                    return m;
                }

                // ---- a landed KICK: decided in the decorator's KnockDown seam, the dice say DOWN - since step 50 the answer is KnockDown
                var man1 = Peasant(4801);
                var kick = KickLands();
                var blow = new Blow(king.AgentIndex);
                dice.Next(0.0, 0.0, 0.5); // knocked down, shield dropped, the get-up roll at its centre
                bool down = model.DecideAgentKnockedDownByBlow(king.Agent, man1.Agent, in kick, null!, in blow);
                Check(down, "step 50: the kick's knockdown was not answered in the decorator's seam");
                Check(stats.Decisions == 1 && stats.FromDecorator == 1, "the decorator did not decide the kick: " + stats.Decisions + " / " + stats.FromDecorator);
                logic.ImpactModelHitTaken(king.Agent, man1.Agent, in kick);
                Check(stats.Decisions == 1, "OnMeleeHit's part decided the kick a second time: " + stats.Decisions);
                var d = logic.LastImpact;
                Check(d.Event == ImpactEvent.Kick && Close(d.X, 1.33, 0.006) && Close(d.Knockdown.Chance, 0.80, 0.001) && Close(d.ShieldDrop.Chance, 0.90, 0.001)
                      && d.Knockdown.Happens && d.ShieldDrop.Happens && Close(d.GetUpFactor, 1 + d.T, 0.001),
                    "the king's kick on the man (IMPACT §4 B: x 1.33, shield 90%, down 80%): x " + d.X + ", " + d.ShieldDrop.Chance + " / " + d.Knockdown.Chance + ", get-up x" + d.GetUpFactor);
                Check(stats.LiveDecisions == 1 && stats.ShadowDecisions == 0 && stats.Applied == 1, "a live kick (step 50 - applied): live " + stats.LiveDecisions + ", applied " + stats.Applied);
                LogHas("[impact] first kick decided at 19000.0 s - (agent 4800)'s kick on (agent 4801) (LIVE): (agent 4801) bar 20% (T 0.73); points 200 vs 10");
                LogHas("skill 250 vs 60 (x1.17 / x0.85) → R 7.37");
                LogHas("→ x 1.33: knocked down 80% → rolled 0.00 YES; shield dropped 90% → rolled 0.00 YES; gets up x1.73 slower");
                // on the ground (the state remembers what WOULD have happened): the next blow on him is refused
                logic.ImpactModelHitTaken(king.Agent, man1.Agent, RightBlock());
                d = logic.LastImpact;
                Check(d.Event == ImpactEvent.RightBlock && d.Knockdown.Refusal == ImpactRefusal.Down && d.Stagger.Refusal == ImpactRefusal.Down, "a man on the ground was not refused");

                // ---- right / wrong side blocks on fresh copies of the man (IMPACT §4 B: x 1.09 / 1.42)
                var man2 = Peasant(4802);
                dice.Next(0.999, 0.0, 0.5); // no knockdown, staggered, the stagger's roll at its centre (step 51: the stun has no die)
                logic.ImpactModelHitTaken(king.Agent, man2.Agent, RightBlock());
                d = logic.LastImpact;
                Check(d.Event == ImpactEvent.RightBlock && Close(d.X, 1.09, 0.006) && Close(d.Stagger.Chance, 0.80, 0.001) && Close(d.Knockdown.Chance, 0.40, 0.001)
                      && d.Stagger.Happens && Close(d.StaggerSeconds, 0.6 * (1 + 1.5 * d.T), 0.001) && Close(d.StunFactor, 1 + d.T, 0.001) && !d.ShieldDrop.InRow,
                    "the king's blow on the man's right-side block: x " + d.X + ", stagger " + d.Stagger.Chance + " (" + d.StaggerSeconds + " s), down " + d.Knockdown.Chance);
                // step 51: blocks are LIVE - the stagger is his (reeling, the immunity after it, one outcome on his record) and queued for the tick
                Check(man2.Impact.Reeling(t + 0.5) && man2.Impact.StaggerImmune(t + d.StaggerSeconds + 1.9) && man2.Impact.Recent(t, 8) == 1 && logic.ImpactBlockPendingCount == 1,
                    "a live block's stagger is not on the man's record / not queued for the tick");
                LogHas("[impact] first right-side block decided at 19000.0 s - (agent 4801)'s right-side block of (agent 4800)'s blow (LIVE)");
                LogHas("knocked down 10% (cut for 2 recent) - refused: on the ground; staggered 20% (cut for 2 recent) - refused: on the ground");
                LogHas("[impact] first time staggered happens, at 19000.0 s - (agent 4802)'s right-side block of (agent 4800)'s blow");
                // in SHADOW the record is kept ("would" alike): a fresh copy of the man - the same block staggers him, then inside his stagger:
                // immune to a new one, the reeling bonus, the halving
                S.Set(SettingsSchema.ImpactShadowMode, true, SettingSources.Mcm);
                man2 = Peasant(4804);
                dice.Next(0.999, 0.0, 0.5);
                logic.ImpactModelHitTaken(king.Agent, man2.Agent, RightBlock());
                d = logic.LastImpact;
                Check(d.Stagger.Happens && man2.Impact.Reeling(t + 0.5) && man2.Impact.StaggerImmune(t + d.StaggerSeconds + 1.9) && !man2.Impact.StaggerImmune(t + d.StaggerSeconds + 2.1),
                    "shadow: the stagger's reeling / immunity not kept on his record");
                logic.ImpactModelHitTaken(king.Agent, man2.Agent, WrongBlock());
                d = logic.LastImpact;
                Check(d.Event == ImpactEvent.WrongBlock && d.Stagger.Refusal == ImpactRefusal.StaggerImmune && d.Reeling && d.Knockdown.Halvings == 1
                      && Close(d.Knockdown.Chance, 0.20, 0.001) && Close(d.X, 1.42 + 0.75, 0.006),
                    "a blow on a reeling man: x " + d.X + ", stagger " + d.Stagger.Refusal + ", down " + d.Knockdown.Chance + " (halvings " + d.Knockdown.Halvings + ")");
                S.Set(SettingsSchema.ImpactShadowMode, false, SettingSources.Mcm);
                var man3 = Peasant(4803);
                logic.ImpactModelHitTaken(king.Agent, man3.Agent, WrongBlock());
                d = logic.LastImpact;
                Check(Close(d.X, 1.42, 0.006) && Close(d.ShieldDrop.Chance, 0.50, 0.001) && Close(d.Stagger.Chance, 0.90, 0.001) && Close(d.Knockdown.Chance, 0.40, 0.001),
                    "the king's blow on the man's WRONG-side block (x 1.42, 90 / 50 / 40): x " + d.X);
                // the man swings at the fresh king (his tired swing - the step 20b animation share through the real logic): nothing
                logic.ImpactModelHitTaken(man3.Agent, king.Agent, RightBlock());
                d = logic.LastImpact;
                Check(Close(d.X, -0.76, 0.006) && d.Stagger.Chance == 0 && d.Knockdown.Chance == 0, "the man's blow on the fresh king (x −0.76, 0 / 0): x " + d.X);

                // ---- IMPACT §4 A through the real records: even men (skill 100 - pool 80 at 0.8 a skill point: the RATIO of the points is
                // what counts), the defender at 25 % (parry 77 / 36, a clean hit 68 / 35 - in the decorator)
                var even1 = Man(4810, 100, 1.0);
                var even2 = Man(4811, 100, 0.25);
                var even3 = Man(4812, 100, 0.25);
                inputs.Weapons[4810] = (1.2, 92, ImpactDamageType.Cut, 100);
                inputs.Guards[4811] = (1.2, 100);
                logic.ImpactModelHitTaken(even1.Agent, even2.Agent, Parried());
                d = logic.LastImpact;
                Check(d.Event == ImpactEvent.Parry && Close(d.Stagger.Chance, 0.768, 0.006) && Close(d.Knockdown.Chance, 0.364, 0.006), "an even parry at 25%: " + d.Stagger.Chance + " / " + d.Knockdown.Chance);
                var hit = CleanHit();
                var b2 = new Blow(even1.AgentIndex);
                Check(!model.DecideAgentKnockedDownByBlow(even1.Agent, even3.Agent, in hit, null!, in b2), "a clean hit whose dice said no got KnockDown (step 52: clean hits live)");
                logic.ImpactModelHitTaken(even1.Agent, even3.Agent, in hit);
                d = logic.LastImpact;
                Check(d.Event == ImpactEvent.CleanHit && Close(d.Stagger.Chance, 0.683, 0.006) && Close(d.Knockdown.Chance, 0.345, 0.006) && stats.FromDecorator == 2,
                    "an even clean hit at 25% (68 / 35, decided in the decorator): " + d.Stagger.Chance + " / " + d.Knockdown.Chance + ", from the decorator " + stats.FromDecorator);
                // two even fresh men: 0 always
                var even4 = Man(4813, 100, 1.0);
                logic.ImpactModelHitTaken(even1.Agent, even4.Agent, KickLands());
                d = logic.LastImpact;
                Check(d.X <= 0.0001 && d.Knockdown.Chance == 0 && d.ShieldDrop.Chance == 0 && d.ReelFactor == 1, "two even fresh men: not 0 (x " + d.X + ")");

                // ---- a bash (an alternative attack with the shield: its weight); a rider (no stagger; a knockdown = knocked off at 50 %)
                inputs.Weapons[4814] = (6.0, 0, ImpactDamageType.Blunt, 100);
                var basher = Man(4814, 125, 1.0);
                logic.ImpactModelHitTaken(basher.Agent, Man(4815, 125, 0.5).Agent, BashLands());
                d = logic.LastImpact;
                Check(d.Event == ImpactEvent.Bash && Close(d.Impact, 2.0, 0.001), "a bash with a 6 kg shield: " + d.Event + ", impact " + d.Impact);
                var rider = Man(4816, 125, 0.0);
                inputs.Mounted.Add(4816);
                inputs.Guards[4816] = (4.0, 100);
                dice.Next(0.0);
                logic.ImpactModelHitTaken(even1.Agent, rider.Agent, WrongBlock());
                d = logic.LastImpact;
                Check(d.Dismount && d.Knockdown.Happens && Close(d.Knockdown.Chance, 0.20, 0.001) && d.Stagger.Refusal == ImpactRefusal.Replaced, "a rider: down " + d.Knockdown.Chance + ", dismount " + d.Dismount);
                var rider2 = Man(4817, 125, 0.0);
                inputs.Mounted.Add(4817);
                logic.ImpactModelHitTaken(even1.Agent, rider2.Agent, CleanHit());
                Check(logic.LastImpact.Stagger.Refusal == ImpactRefusal.Rider, "a rider was staggered");

                // ---- you: ImpactOnPlayerPercent 0 = never; your own kick is not protected
                var you = Man(4820, 125, 0.0);
                logic.SmokePlayer = you.Agent;
                S.Set(SettingsSchema.ImpactOnPlayerPercent, 0, SettingSources.Mcm);
                logic.ImpactModelHitTaken(even1.Agent, you.Agent, KickLands());
                d = logic.LastImpact;
                Check(d.DefenderIsPlayer && d.Knockdown.Chance == 0 && d.ShieldDrop.Chance == 0 && d.Knockdown.Raw > 0.7, "you at 0%: " + d.Knockdown.Chance);
                logic.ImpactModelHitTaken(you.Agent, Man(4821, 125, 0.0).Agent, KickLands());
                Check(logic.LastImpact.AttackerIsPlayer && logic.LastImpact.Knockdown.Chance > 0.7, "your kick lost its chance with ImpactOnPlayerPercent 0");
                S.Set(SettingsSchema.ImpactOnPlayerPercent, 100, SettingSources.Mcm);
                logic.SmokePlayer = null;

                // ---- shadow vs live, live: the mode line, the count
                int shadowBefore = stats.ShadowDecisions;
                S.Set(SettingsSchema.ImpactShadowMode, true, SettingSources.Mcm);
                logic.TickImpact(t += 0.02);
                LogHas("[impact] the model's mode changed mid-mission at 19000.0 s: SHADOW (ImpactShadowMode) - every event decided and logged, nothing applied");
                logic.ImpactModelHitTaken(even1.Agent, Man(4822, 125, 0.3).Agent, KickLands());
                Check(stats.ShadowDecisions == shadowBefore + 1, "a shadow decision not counted");
                S.Set(SettingsSchema.ImpactShadowMode, false, SettingSources.Mcm);
                logic.TickImpact(t += 0.02); // live again (step 51: the spike's own checks run with the model off - this mode line is ours)
                S.Set(SettingsSchema.ImpactSpike, true, SettingSources.Mcm);
                logic.TickImpact(t += 0.02);
                LogHas("SHADOW (the spike is on - ImpactSpike owns the levers) - decided and logged, nothing applied");
                S.Set(SettingsSchema.ImpactSpike, false, SettingSources.Mcm);
                logic.TickImpact(t += 0.02);
                ImpactHooks.RestoreStunCap();

                // ---- the family switch: kicks off = not decided, blocks still are
                int before = stats.Decisions;
                S.Set(SettingsSchema.ImpactKicksEnabled, false, SettingSources.Mcm);
                logic.ImpactModelHitTaken(even1.Agent, Man(4823, 125, 0.3).Agent, KickLands());
                Check(stats.Decisions == before && stats.Skipped[(int)ImpactSkip.FamilyOff] == 1 && stats.SkippedByEvent[(int)ImpactEvent.Kick] == 1, "a kick decided with ImpactKicksEnabled off");
                logic.ImpactModelHitTaken(even1.Agent, Man(4824, 125, 0.3).Agent, Parried());
                Check(stats.Decisions == before + 1, "a parry not decided with only the kicks off");
                S.Set(SettingsSchema.ImpactKicksEnabled, true, SettingSources.Mcm);

                // ---- ImpactEnabled off live: the line, nothing decided; back on
                S.Set(SettingsSchema.ImpactEnabled, false, SettingSources.Mcm);
                logic.TickImpact(t += 0.02);
                LogHas("[impact] the model switched OFF (ImpactEnabled) mid-mission at ");
                before = stats.Decisions;
                logic.ImpactModelHitTaken(even1.Agent, Man(4825, 125, 0.3).Agent, Parried());
                Check(!logic.ImpactModelOn && stats.Decisions == before && stats.Skipped[(int)ImpactSkip.Off] >= 1, "decided with ImpactEnabled off");
                S.Set(SettingsSchema.ImpactEnabled, true, SettingSources.Mcm);
                logic.TickImpact(t += 0.02);
                LogHas("[impact] the model switched ON mid-mission at ");

                // ---- a failing engine read: one [error], counted, the next event fine
                inputs.Throw = true;
                logic.ImpactModelHitTaken(even1.Agent, Man(4826, 125, 0.3).Agent, Parried());
                inputs.Throw = false;
                Check(stats.Skipped[(int)ImpactSkip.ReadFailed] == 1, "a failing read was not counted");
                LogHas("smoke: the weapon read failed");
                before = stats.Decisions;
                logic.ImpactModelHitTaken(even1.Agent, Man(4827, 125, 0.3).Agent, Parried());
                Check(stats.Decisions == before + 1, "the read did not recover");
                // an untracked attacker (a horse charge's rider, a man gone): counted, no read
                int calls = inputs.Calls;
                logic.ImpactModelHitTaken(FakeAgent(4899), even2.Agent, Parried());
                Check(inputs.Calls == calls && stats.Skipped[(int)ImpactSkip.NotTracked] >= 1, "an untracked attacker was read");
                // missiles and horse charges are not events
                before = stats.Decisions;
                var missile = AttackCollisionData.GetAttackCollisionDataForDebugPurpose(false, false, false, true, false, true, false, false, false, false, false, false,
                    CombatCollisionResult.StrikeAgent, 0, 0, 0, 0, default(BoneBodyPartType), 0, default(Agent.UsageDirection), 0, default(CombatHitResultFlags), 0.5f, 0.5f, 0f, 0f, 0f, 0f, 0f, 0f,
                    Vec3.Zero, Vec3.Zero, Vec3.Zero, Vec3.Zero, Vec3.Zero, Vec3.Zero, Vec3.Zero);
                logic.ImpactModelHitTaken(even1.Agent, Man(4828, 125, 0.3).Agent, in missile);
                Check(stats.Decisions == before, "a missile was decided");

                // ---- the summary
                logic.WriteImpactSummary();
                LogHas("[summary] impact (step 48 - the model; MIXED - shadow ");
                LogHas("[summary] impact kicks by the kicked man's bar 100-75% / 75-50% / 50-25% / 25-0%: ");
                LogHas("[summary] impact right-side blocks by the blocker's bar ");
                LogHas("[summary] impact wrong-side blocks by the blocker's bar ");
                LogHas("[summary] impact parries by the blocker's bar ");
                LogHas("[summary] impact clean hits by the hit man's bar ");
                LogHas("[summary] impact shield bashes by the kicked man's bar ");
                LogHas("[summary] impact riposte (a right-side block or parry staggers the ATTACKER; by the attacker's bar ");
                LogHas("[summary] impact limits: refused - on the ground 2");
                LogHas("riders knocked off their horse by the dice 1 (step 53: applied / seen in the 'impact riders' line)");
                LogHas("[summary] impact lengths asked: stagger ");
                LogHas("[summary] impact cost: ");
                LogHas("the engine read failed 1");
                LogHas("2 landed blows decided in the damage decorator's KnockDown seam (one decision per event)");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                ImpactHooks.RestoreStunCap();
                ImpactHooks.KnockDown = null;
                ImpactHooks.BlockStun = null;
                ImpactModelDefaults();
                AthleticsDefaults();
            }
        }

        /// <summary>The cost: a thousand men at every bar, twenty events a tick (kicks and clean hits through the decorator's seam, blocks of
        /// the three kinds, parries) - the decisions allocate nothing and stay small.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ImpactModelCostStaysFlat()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                var inputs = new FakeImpactInputs();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = new FakeImpactBody { Record = false };
                logic.ImpactInputs = inputs;
                logic.ImpactDice = new SeededRandom(48);
                const int men = 1000;
                var all = new TrackedAgent[men];
                for (int i = 0; i < men; i++)
                {
                    var m = logic.Track(FakeAgent(8000 + i))!;
                    m.AthleticsSkill = 50 + (i % 200);
                    SetBar(m, (i % 101) / 100.0);
                    inputs.Weapons[8000 + i] = (0.6 + (i % 5) * 0.6, 80 + (i % 20), (ImpactDamageType)(i % 3), 20 + (i % 280));
                    inputs.Guards[8000 + i] = (1 + (i % 6), 20 + (i % 280));
                    if (i % 3 != 0) inputs.Shields.Add(8000 + i);
                    all[i] = m;
                }
                var kinds = new[] { KickLands(), RightBlock(), WrongBlock(), Parried(), CleanHit(), BashLands() };
                int k = 0;
                void Frame(int f)
                {
                    double t = 21000 + f / 60.0;
                    logic.TickImpact(t);
                    for (int e = 0; e < 20; e++, k++)
                    {
                        var a = all[(k * 7) % men];
                        var v = all[(k * 13 + 1) % men];
                        var cd = kinds[k % kinds.Length];
                        if (cd.CollisionResult == CombatCollisionResult.StrikeAgent && !cd.IsAlternativeAttack || k % 6 == 0) logic.ImpactModelOnLandedBlow(a.Agent, v.Agent, in cd);
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
                var st = logic.ImpactModelStats;
                Console.WriteLine("        step 48 cost: " + men + " men, 20 events a tick - " + ms.ToString("0.000", CultureInfo.InvariantCulture) + " ms a tick, "
                                  + allocated + " bytes over " + ticks + " ticks; " + st.Decisions + " decisions (" + (1000 * st.CostMs / Math.Max(1, st.Decisions)).ToString("0.00", CultureInfo.InvariantCulture)
                                  + " µs each), down " + st.HappenedOf(ImpactEvent.Kick, ImpactOutcome.Knockdown) + " of the kicks");
                Check(ms < 0.5, "cost: 20 impact decisions a tick over " + men + " men took " + ms + " ms (budget 0.5 ms)");
                Check(allocated < 64 * 1024, "cost: the impact model allocates - " + allocated + " bytes over " + ticks + " ticks");
                Check(st.Decisions >= 20 * (600 + ticks) * 9 / 10, "cost: too few decisions - " + st.Decisions);
                logic.WriteImpactSummary();
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                ImpactHooks.RestoreStunCap();
                ImpactHooks.KnockDown = null;
                ImpactHooks.BlockStun = null;
                ImpactModelDefaults();
                AthleticsDefaults();
            }
        }

        /// <summary>The master-switch step's model part: off = nothing decided (counted as "off") and the [impact] line; back on = decided.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ImpactModelFollowsTheMasterSwitch()
        {
            var keep = _logic;
            try
            {
                ImpactModelDefaults();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactInputs = new FakeImpactInputs();
                double t = 22000;
                logic.TickImpact(t);
                var a = logic.Track(FakeAgent(2950))!;
                var d = logic.Track(FakeAgent(2951))!;
                var stats = logic.ImpactModelStats;
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                logic.TickImpact(t + 0.02);
                LogHas("[impact] the model switched OFF (ModEnabled) mid-mission at ");
                logic.ImpactModelHitTaken(a.Agent, d.Agent, Parried());
                Check(stats.Decisions == 0 && stats.Skipped[(int)ImpactSkip.Off] == 1, "mod off: the impact model still decided (step 48)");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                logic.TickImpact(t + 0.04);
                logic.ImpactModelHitTaken(a.Agent, d.Agent, Parried());
                Check(stats.Decisions == 1, "mod back on: the impact model did not decide (step 48)");

                // step 50: a live kick's knockdown answered and watched; the master switch off = nothing answered, every watch let go at once
                var body = new FakeImpactBody();
                logic.ImpactBody = body;
                var dice = new ScriptDice();
                logic.ImpactDice = dice;
                var model = SpikeDamageModel();
                var tired = logic.Track(FakeAgent(2952))!;
                SetBar(tired, 0.0);
                var kick = KickLands();
                var blow = new Blow(a.AgentIndex);
                dice.Next(0.0, 0.999, 0.5);
                int watchesBefore = logic.ImpactLiveWatches; // step 51: the fresh parries above may time a guard
                Check(model.DecideAgentKnockedDownByBlow(a.Agent, tired.Agent, in kick, null!, in blow) && logic.ImpactLiveWatches == watchesBefore + 1, "the live kick (step 50) was not knocked down / watched");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                logic.TickImpact(t + 0.06);
                var tired2 = logic.Track(FakeAgent(2953))!;
                SetBar(tired2, 0.0);
                dice.Next(0.0, 0.999, 0.5);
                Check(logic.ImpactLiveWatches == 0 && !model.DecideAgentKnockedDownByBlow(a.Agent, tired2.Agent, in kick, null!, in blow) && body.Slows.Count == 0,
                    "mod off: the live kicks (step 50) still answered a knockdown or kept a watch");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                logic.TickImpact(t + 0.08);
                dice.Next(0.0, 0.999, 0.5);
                Check(model.DecideAgentKnockedDownByBlow(a.Agent, tired2.Agent, in kick, null!, in blow), "mod back on: the live kick (step 50) not knocked down");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                ImpactHooks.RestoreStunCap();
                ImpactHooks.KnockDown = null;
                ImpactHooks.BlockStun = null;
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
            }
        }
    }
}

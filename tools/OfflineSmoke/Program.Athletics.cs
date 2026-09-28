using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;
using TraxCombat.Models;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for Athletics (steps 5, 5c) - the REAL module code on .NET Framework with the
    /// game's DLLs, no game:
    ///   * <see cref="SpeedPenalty"/> on the game's own <see cref="AgentDrivenProperties"/> (attack,
    ///     run and horse levers each touch only their own values);
    ///   * the REAL <see cref="TraxAgentStatModel"/> decorating a stand-in base model that, like
    ///     Sandbox's and CustomBattle's, assigns fresh values on every recompute - fighters and a
    ///     slowed rider's horse;
    ///   * the REAL <see cref="AthleticsLogic"/>: swings fed through its action observer, the pool
    ///     from the skill (the floor for a fighter without a character), the curves after every
    ///     blow, DESIGN's blow counts, the health cap, hot swap, the read API, the summary, the
    ///     error path; and the REAL damage decorator reading the attacker's f.
    /// The agents are UNINITIALIZED <see cref="Agent"/> objects (no native side) with only their
    /// index, driven properties and health set - so nothing here may call a native member
    /// (IsActive, IsHuman, GetCurrentActionType, MountAgent, UpdateAgentProperties' engine push).
    /// What it cannot check: that the engine honours the multipliers, the poll, the hit events,
    /// the regen pass - PLAYTEST §3 and the [summary]'s attack-speed and run-speed checks prove
    /// those in game.
    /// </summary>
    internal static partial class Program
    {
        private const int ActIdle = (int)Agent.ActionCodeType.Idle;
        private const int ActReady = (int)Agent.ActionCodeType.ReadyMelee;
        private const int ActRelease = (int)Agent.ActionCodeType.ReleaseMelee;

        private static AthleticsLogic? _logic;
        private static TraxAgentStatModel? _statTop;
        private static FreshStatsModel? _statBase;

        /// <summary>Stands in for Sandbox's model: assigns FRESH values on every recompute
        /// (Sandbox's and CustomBattle's UpdateHumanStats / UpdateHorseStats do, with '=').</summary>
        private sealed class FreshStatsModel : AgentStatCalculateModel
        {
            public int Updates;

            public override void InitializeAgentStats(Agent agent, Equipment spawnEquipment, AgentDrivenProperties agentDrivenProperties, AgentBuildData agentBuildData)
            {
            }

            public override void UpdateAgentStats(Agent agent, AgentDrivenProperties p)
            {
                Updates++;
                p.SwingSpeedMultiplier = 1.05f;
                p.ThrustOrRangedReadySpeedMultiplier = 1.02f;
                p.ReloadSpeed = 0.95f;
                p.HandlingMultiplier = 1.1f;
                p.MaxSpeedMultiplier = 0.8f;
                p.CombatMaxSpeedMultiplier = 0.84f;
                p.MountSpeed = 0.9f;
                // step 5e: the AI's decision values, assigned fresh like SetAiRelatedProperties does
                p.AIAttackOnDecideChance = 0.144f;
                p.AIAttackOnParryChance = 0.08f;
                p.AiShootFreq = 0.65f;
                p.AiWaitBeforeShootFactor = 0.6f;
                p.AIDecideOnAttackChance = 0.5f;
                p.AIHoldingReadyMaxDuration = 0.25f;
            }

            public override float GetDifficultyModifier() => 1f;

            public override bool CanAgentRideMount(Agent agent, Agent targetMount) => true;

            public override float GetWeaponDamageMultiplier(Agent agent, WeaponComponentData weapon) => 1f;

            public override float GetEquipmentStealthBonus(Agent agent) => 0f;

            public override float GetSneakAttackMultiplier(Agent agent, WeaponComponentData weapon) => 1f;

            public override float GetKnockBackResistance(Agent agent) => 0f;

            public override float GetKnockDownResistance(Agent agent, StrikeType strikeType = StrikeType.Invalid) => 0f;

            public override float GetDismountResistance(Agent agent) => 0f;

            public override float GetBreatheHoldMaxDuration(Agent agent, float baseBreatheHoldMaxDuration) => baseBreatheHoldMaxDuration;
        }

        private static Action<Agent, int>? _setIndex;
        private static Action<Agent, AgentDrivenProperties>? _setProperties;
        private static Action<Agent, float>? _setHealth;
        private static Action<Agent, float>? _setHealthLimit;

        /// <summary>An Agent object without its native side: index + driven properties only. The
        /// backing fields are written by compiled IL: reflection's FieldInfo.SetValue would run
        /// Agent's static initializer, which calls the engine (ActionIndexCache → MBAnimation) and
        /// breaks the Agent type for the rest of the process.</summary>
        private static Agent FakeAgent(int index)
        {
            _setIndex ??= FieldSetter<int>("<Index>k__BackingField");
            _setProperties ??= FieldSetter<AgentDrivenProperties>("<AgentDrivenProperties>k__BackingField");
            var a = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
            _setIndex(a, index);
            _setProperties(a, new AgentDrivenProperties());
            return a;
        }

        /// <summary>Health and its maximum - both managed fields (the health cap reads them).</summary>
        private static void SetHealth(Agent a, float health, float limit)
        {
            _setHealth ??= FieldSetter<float>("_health");
            _setHealthLimit ??= FieldSetter<float>("<HealthLimit>k__BackingField");
            _setHealth(a, health);
            _setHealthLimit(a, limit);
        }

        private static Action<Agent, T> FieldSetter<T>(string name)
        {
            var field = typeof(Agent).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                        ?? throw new MissingFieldException(typeof(Agent).FullName, name);
            var method = new DynamicMethod("smoke_set" + name.Replace("<", "_").Replace(">", "_"), null, new[] { typeof(Agent), typeof(T) }, typeof(Agent), true);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld, field);
            il.Emit(OpCodes.Ret);
            return (Action<Agent, T>)method.CreateDelegate(typeof(Action<Agent, T>));
        }

        private static bool Near(float a, float b) => Math.Abs(a - b) < 1e-4f;

        private static bool Near(double a, double b) => Math.Abs(a - b) < 1e-6;

        private static AthleticsRules Rules => AthleticsRules.From(S);

        /// <summary>DESIGN's initial numbers, set explicitly - the checks count blows with them
        /// ("5 empty a recruit, 54 a 300-skill party leader"), whatever Anton tunes in defaults.json.</summary>
        private static void AthleticsDefaults()
        {
            S.ResetToDefaults(SettingSources.Defaults);
            S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.AthleticsEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.AthleticsPoolFloor, 50, SettingSources.File);
            S.Set(SettingsSchema.AthleticsPoolPerSkill, 1.0, SettingSources.File);
            S.Set(SettingsSchema.AthleticsPeakPercent, 75, SettingSources.File);
            S.Set(SettingsSchema.HealthCapsAthletics, true, SettingSources.File);
            S.Set(SettingsSchema.CostPerBlow, 10, SettingSources.File);
            S.Set(SettingsSchema.CostPerKickOrBash, 3, SettingSources.File); // step 18
            S.Set(SettingsSchema.CostOnMiss, true, SettingSources.File);
            S.Set(SettingsSchema.HeroCostMultiplier, 0.75, SettingSources.File);
            S.Set(SettingsSchema.PartyLeaderCostMultiplier, 0.75, SettingSources.File);
            S.Set(SettingsSchema.ExhaustedAttackSpeedPercent, 20, SettingSources.File);
            S.Set(SettingsSchema.AttackRateAiDecisions, false, SettingSources.File); // step 13: off
            S.Set(SettingsSchema.AttackRatePaceHold, true, SettingSources.File);
            // step 16: the older smoke steps check the Legacy techniques (NoAttack, the scripted walk) - the new ways
            // have their own step (Program.AiInput.cs)
            S.Set(SettingsSchema.AttackRatePaceByInput, false, SettingSources.File);
            S.Set(SettingsSchema.AiHoldRaiseGuard, true, SettingSources.File);
            S.Set(SettingsSchema.StepBackBackpedal, false, SettingSources.File);
            S.Set(SettingsSchema.AttackRatePlayerTimer, true, SettingSources.File);
            S.Set(SettingsSchema.AttackAnimationMinPercent, 100, SettingSources.File); // step 13: full-speed animations
            S.Set(SettingsSchema.MinMoveSpeedMultiplier, 0.7, SettingSources.File);
            S.Set(SettingsSchema.MountMinSpeedMultiplier, 1.0, SettingSources.File);
            S.Set(SettingsSchema.DamageBonusFollowsAthletics, true, SettingSources.File);
            S.Set(SettingsSchema.RegenDelayBlowTimes, 2, SettingSources.File);
            S.Set(SettingsSchema.BlowTimeSeconds, 1.5, SettingSources.File);
            S.Set(SettingsSchema.FullRegenSecondsStanding, 60, SettingSources.File);
            S.Set(SettingsSchema.RegenRateNearFullPercent, 50, SettingSources.File); // step 14: the refill curve
            S.Set(SettingsSchema.RegenMultiplierAtFullRun, 0.5, SettingSources.File);
            S.Set(SettingsSchema.WalkEffortFraction, 0.4, SettingSources.File);
            S.Set(SettingsSchema.VerboseLogging, false, SettingSources.File);
        }

        // ------------------------------------------------------------------ checks

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SpeedPenaltyScalesOnlyItsOwnValues()
        {
            var slots = Enum.GetValues(typeof(DrivenProperty)).Cast<DrivenProperty>().Where(d => (int)d >= 0 && (int)d < 98)
                .GroupBy(d => (int)d).Select(g => g.First()).ToList();
            void Lever(string name, Action<AgentDrivenProperties> apply, DrivenProperty[] mine, DrivenProperty[]? divided = null)
            {
                var p = new AgentDrivenProperties();
                // every slot gets its own value (the enum has aliases - one slot, several names)
                foreach (var d in slots) p.SetStat(d, 1f + (int)d / 100f);
                var before = slots.ToDictionary(d => d, d => p.GetStat(d));
                apply(p);
                foreach (var pair in before)
                {
                    bool own = mine.Any(m => (int)m == (int)pair.Key);
                    bool over = divided != null && divided.Any(m => (int)m == (int)pair.Key);
                    float now = p.GetStat(pair.Key);
                    if (own) Check(Near(now, pair.Value * 0.2f), name + ": " + pair.Key + " not x0.2: " + pair.Value + " → " + now);
                    else if (over) Check(Near(now, pair.Value / 0.2f), name + ": " + pair.Key + " not ÷0.2: " + pair.Value + " → " + now);
                    else Check(now == pair.Value, name + ": " + pair.Key + " changed: " + pair.Value + " → " + now);
                }
            }
            Lever("attack", p => SpeedPenalty.Scale(p, 0.2f), new[] { DrivenProperty.SwingSpeedMultiplier, DrivenProperty.ThrustOrRangedReadySpeedMultiplier, DrivenProperty.ReloadSpeed });
            Lever("run", p => SpeedPenalty.ScaleRun(p, 0.2f), new[] { DrivenProperty.MaxSpeedMultiplier });
            Lever("horse", p => SpeedPenalty.ScaleMount(p, 0.2f), new[] { DrivenProperty.MountSpeed });
            // step 5e: the AI's chances x m, its aim ÷ m - and not one defence value (handling, blocking, parrying, shields)
            Lever("AI decisions", p => SpeedPenalty.ScaleAiDecisions(p, 0.2f),
                new[] { DrivenProperty.AIAttackOnDecideChance, DrivenProperty.AIAttackOnParryChance, DrivenProperty.AiShootFreq },
                new[] { DrivenProperty.AiWaitBeforeShootFactor });
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DecoratorAppliesEachFightersMultipliers()
        {
            AthleticsDefaults();
            _statBase = new FreshStatsModel();
            _statTop = new TraxAgentStatModel(new AgentStatCalculateModel[] { _statBase });
            _statTop.Initialize(_statBase);
            _logic = new AthleticsLogic();
            SetStatic(typeof(AthleticsLogic), "_current", _logic);
            typeof(AthleticsLogic).GetMethod("StartAthletics", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_logic, null);
            LogHas("[athletics] mission start: ON - pool = the Athletics skill x1.00, at least 50; full strength at 75% of the pool and above; cost per blow 10.0 / hero 7.5 / party leader 5.");
            LogHas("[athletics] party-leader rule: no campaign (custom battle) - the side's general, or every hero of a side without one");

            var a = FakeAgent(3);
            var st = _logic.Track(a)!;
            Check(st != null && ReferenceEquals(_logic.Track(a), st), "Track is not idempotent");
            Check(!st!.IsHero && !st.IsLeader, "an agent without a character was flagged hero/leader");
            Check(st.AthleticsSkill == 0 && !st.SkillKnown && AthleticsMath.PoolPoints(Rules, st) == 50,
                "a fighter without a character did not get the floor pool: skill " + st.AthleticsSkill + ", pool " + AthleticsMath.PoolPoints(Rules, st));
            var p = a.AgentDrivenProperties;

            _statTop.UpdateAgentStats(a, p);
            Check(Near(p.SwingSpeedMultiplier, 1.05f) && Near(p.ThrustOrRangedReadySpeedMultiplier, 1.02f) && Near(p.ReloadSpeed, 0.95f) && Near(p.MaxSpeedMultiplier, 0.8f),
                "a fresh fighter's properties were changed: " + SpeedPenalty.Snapshot.Take(a));

            st.SpeedMultiplier = 0.2f;
            st.RunSpeedMultiplier = 0.3f;
            _statTop.UpdateAgentStats(a, p);
            // step 13 (PAUSE ONLY): the attack animations stay at full speed (AttackAnimationMinPercent 100) -
            // the no-attack timer carries the slow-down; the run multiplier still applies
            Check(Near(p.SwingSpeedMultiplier, 1.05f) && Near(p.ThrustOrRangedReadySpeedMultiplier, 1.02f) && Near(p.ReloadSpeed, 0.95f),
                "the attack animations were slowed with AttackAnimationMinPercent 100: " + SpeedPenalty.Snapshot.Take(a));
            Check(Near(p.MaxSpeedMultiplier, 0.24f), "the run multiplier was not applied: " + p.MaxSpeedMultiplier);
            Check(Near(p.CombatMaxSpeedMultiplier, 0.84f) && Near(p.HandlingMultiplier, 1.1f) && Near(p.MountSpeed, 0.9f),
                "CombatMaxSpeedMultiplier, HandlingMultiplier or MountSpeed was touched on a fighter");
            Check(Near(p.AIAttackOnDecideChance, 0.144f) && Near(p.AiWaitBeforeShootFactor, 0.6f), "the AI decision values moved with AttackRateAiDecisions off (the default)");
            // the A/B switch on: the AI's decisions follow m (chances x0.2, the aim ÷0.2, no defence value)
            S.Set(SettingsSchema.AttackRateAiDecisions, true, SettingSources.Mcm);
            _statTop.UpdateAgentStats(a, p);
            Check(Near(p.AIAttackOnDecideChance, 0.0288f) && Near(p.AIAttackOnParryChance, 0.016f) && Near(p.AiShootFreq, 0.13f) && Near(p.AiWaitBeforeShootFactor, 3.0f),
                "the AI decision values did not follow m: " + p.AIAttackOnDecideChance + " / " + p.AIAttackOnParryChance + " / " + p.AiShootFreq + " / " + p.AiWaitBeforeShootFactor);
            Check(Near(p.AIDecideOnAttackChance, 0.5f) && Near(p.AIHoldingReadyMaxDuration, 0.25f), "a defence value or AIHoldingReady was touched");
            Check(Near(p.SwingSpeedMultiplier, 1.05f), "the AI-decision switch slowed the animations");
            // the animation floor at 0: the old step-5 animations whole (x m), read live by the next recompute
            S.Set(SettingsSchema.AttackAnimationMinPercent, 0, SettingSources.Mcm);
            _statTop.UpdateAgentStats(a, p);
            Check(Near(p.SwingSpeedMultiplier, 0.21f) && Near(p.ThrustOrRangedReadySpeedMultiplier, 0.204f) && Near(p.ReloadSpeed, 0.19f),
                "AttackAnimationMinPercent 0: the animations were not x m: " + SpeedPenalty.Snapshot.Take(a));
            _statTop.UpdateAgentStats(a, p); // a second recompute (weapon switch): the base resets, we scale once
            Check(Near(p.SwingSpeedMultiplier, 0.21f) && Near(p.MaxSpeedMultiplier, 0.24f) && Near(p.AIAttackOnDecideChance, 0.0288f), "the penalties compounded over two recomputes");
            Check(_statBase.Updates == 5, "the base model was not called on every recompute: " + _statBase.Updates);
            // 60: a little slow-mo - max(m, 0.6)
            S.Set(SettingsSchema.AttackAnimationMinPercent, 60, SettingSources.Mcm);
            _statTop.UpdateAgentStats(a, p);
            Check(Near(p.SwingSpeedMultiplier, 0.63f) && Near(p.ThrustOrRangedReadySpeedMultiplier, 0.612f) && Near(p.ReloadSpeed, 0.57f),
                "AttackAnimationMinPercent 60: the animations were not x0.6: " + SpeedPenalty.Snapshot.Take(a));
            S.Set(SettingsSchema.AttackRateAiDecisions, false, SettingSources.Mcm); // the A/B switch, read live by the next recompute
            _statTop.UpdateAgentStats(a, p);
            Check(Near(p.SwingSpeedMultiplier, 0.63f) && Near(p.AIAttackOnDecideChance, 0.144f) && Near(p.AiWaitBeforeShootFactor, 0.6f),
                "AttackRateAiDecisions off: the animation floor must stay, the AI values vanilla");
            S.Set(SettingsSchema.AttackAnimationMinPercent, 100, SettingSources.Mcm);

            S.Set(SettingsSchema.AthleticsEnabled, false, SettingSources.Mcm);
            _statTop.UpdateAgentStats(a, p);
            Check(Near(p.SwingSpeedMultiplier, 1.05f) && Near(p.MaxSpeedMultiplier, 0.8f), "AthleticsEnabled off did not lift the penalties on a recompute");
            S.Set(SettingsSchema.AthleticsEnabled, true, SettingSources.Mcm);

            var twin = FakeAgent(3); // same index, another agent (indices are reused)
            _statTop.UpdateAgentStats(twin, twin.AgentDrivenProperties);
            Check(Near(twin.AgentDrivenProperties.SwingSpeedMultiplier, 1.05f), "an untracked agent with a reused index got the penalty");
            var stranger = FakeAgent(5000);
            _statTop.UpdateAgentStats(stranger, stranger.AgentDrivenProperties);
            Check(Near(stranger.AgentDrivenProperties.SwingSpeedMultiplier, 1.05f), "an untracked agent got a penalty");

            // his horse: registered in the logic's mount table (what ApplyMountSpeed does in game)
            var horse = FakeAgent(40);
            st.MountSpeedMultiplier = 0.5f;
            st.SlowedMount = horse;
            typeof(AthleticsLogic).GetMethod("RegisterMount", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_logic, new object[] { horse, st });
            var hp = horse.AgentDrivenProperties;
            _statTop.UpdateAgentStats(horse, hp);
            Check(Near(hp.MountSpeed, 0.45f), "the rider's horse multiplier was not applied: MountSpeed " + hp.MountSpeed);
            Check(Near(hp.SwingSpeedMultiplier, 1.05f) && Near(hp.MaxSpeedMultiplier, 0.8f), "a horse got a fighter's penalties");
            var otherHorse = FakeAgent(41);
            _statTop.UpdateAgentStats(otherHorse, otherHorse.AgentDrivenProperties);
            Check(Near(otherHorse.AgentDrivenProperties.MountSpeed, 0.9f), "a horse nobody slows got a penalty");
            st.SlowedMount = null; // he dismounted: the table's reference check lets the horse go
            _statTop.UpdateAgentStats(horse, hp);
            Check(Near(hp.MountSpeed, 0.9f), "a horse kept its rider's penalty after he left it");

            SetStatic(typeof(AthleticsLogic), "_current", null);
            _statTop.UpdateAgentStats(a, p);
            Check(Near(p.SwingSpeedMultiplier, 1.05f) && Near(p.MaxSpeedMultiplier, 0.8f), "no mission running, yet a penalty was applied");
            SetStatic(typeof(AthleticsLogic), "_current", _logic);

            var stats = _logic.Stats;
            // attack = the recomputes that slowed the ANIMATIONS (the floor at 0 twice, at 60 twice - never at 100)
            Check(stats.DecoratorAttack == 4 && stats.DecoratorRun == 6 && stats.DecoratorMount == 1,
                "decorator counts attack/run/horse " + stats.DecoratorAttack + "/" + stats.DecoratorRun + "/" + stats.DecoratorMount + ", expected 4/6/1");
            Check(_logic.RateStats.AiScaled == 4, "AI-decision recomputes " + _logic.RateStats.AiScaled + ", expected 4 (on for four of the six)");
            Check(Near(hp.AIAttackOnDecideChance, 0.144f), "a horse got the AI decision scaling");
            st.SpeedMultiplier = 1f;
            st.RunSpeedMultiplier = 1f;
            st.MountSpeedMultiplier = 1f;
        }

        /// <summary>One swing whose phases last as long as the APPLIED attack multiplier makes them
        /// (what the engine would do): ready at the multiplier in effect, release and rest at the one
        /// the charge set. So the attack-speed check sees intervals of 1.3 s ÷ the asked multiplier.</summary>
        private static void Swing(TrackedAgent st, ref double t)
        {
            var r = Rules;
            float before = st.SpeedMultiplier;
            _logic!.ObserveAction(st, ActReady, t, in r);
            t += 0.5 / before;
            _logic.ObserveAction(st, ActRelease, t, in r);
            float after = st.SpeedMultiplier;
            st.SpeedDirty = false; // as if the tick loop had applied it
            t += 0.5 / after;
            _logic.ObserveAction(st, ActIdle, t, in r);
            t += 0.3 / after;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void BlowsThroughTheLogic()
        {
            S.Set(SettingsSchema.VerboseLogging, true, SettingSources.File);
            // index 3 is reused by a new agent: the stale record leaves the loop, a fresh one starts full
            var stale = FirstTracked(3);
            var a = FakeAgent(3);
            var st = _logic!.Track(a)!;
            Check(!ReferenceEquals(st, stale) && stale.Removed && st.Fraction == 1 && ReferenceEquals(FirstTracked(3), st),
                "a reused index did not replace the stale record");
            LogHas("[athletics] pool at spawn: (agent 3) - Athletics skill 0 (not readable) → pool 50 (the floor); 2 blows at full strength, 5 to empty");
            double t = 100;

            // a recruit's bar (the floor, 50): full strength for 2 blows, then every blow slower, empty on the 5th
            Swing(st, ref t);
            Check(Near(st.Fraction, 0.8) && st.SpeedMultiplier == 1f && st.RunSpeedMultiplier == 1f, "blow 1 of 50: " + st.Fraction + ", x" + st.SpeedMultiplier);
            LogHas("[athletics] blow melee (on foot): (agent 3) - cost 10.0, 50.0 → 40.0 of 50 (f 1.00 → 1.00)");
            Swing(st, ref t);
            Check(Near(st.SpeedMultiplier, 0.84f) && Near(st.RunSpeedMultiplier, 0.94f), "blow 2 (f 0.8): attacks x" + st.SpeedMultiplier + ", run x" + st.RunSpeedMultiplier);
            LogHas("40.0 → 30.0 of 50 (f 1.00 → 0.80) - below full strength");
            Swing(st, ref t);
            Check(Near(st.SpeedMultiplier, 0.2f + 0.8f * (20f / 37.5f)) && Near(st.RunSpeedMultiplier, 0.7f + 0.3f * (20f / 37.5f)), "blow 3 (f 0.53): x" + st.SpeedMultiplier);
            Swing(st, ref t);
            Check(!st.Exhausted && Near(st.SpeedMultiplier, 0.2f + 0.8f * (10f / 37.5f)), "blow 4 (f 0.27): x" + st.SpeedMultiplier);
            Swing(st, ref t);
            Check(st.Exhausted && st.Fraction == 0 && st.SpeedMultiplier == 0.2f && Near(st.RunSpeedMultiplier, 0.7f) && st.Blows == 5,
                "5 blows did not empty a recruit exactly at x0.20 / run x0.70: " + st.Fraction + ", x" + st.SpeedMultiplier + ", run x" + st.RunSpeedMultiplier);
            LogHas("10.0 → 0.0 of 50 (f 0.27 → 0.00) - EXHAUSTED");
            LogHas("[athletics] exhausted: (agent 3) at ");
            for (int i = 0; i < 4; i++) Swing(st, ref t); // empty swings: 5x as long
            Check(st.Blows == 9 && st.Exhausted && st.ExhaustionsEntered == 1, "empty swings: blows " + st.Blows + ", entries " + st.ExhaustionsEntered);

            // step 18: a kick, then straight into a bash - each charged (at 0 they drain nothing), never a blow,
            // never an attack timer or a step-back roll (he is empty: an attack would start both);
            // ranged releases are counted by the poll but charged by the shot event
            var r = Rules;
            int timers = AiTimersAsked(), rolls = _logic.StepStats.Rolls;
            _logic.ObserveAction(st, (int)Agent.ActionCodeType.Kick, t, in r);
            _logic.ObserveAction(st, (int)Agent.ActionCodeType.WeaponBash, t + 0.5, in r);
            Check(AiTimersAsked() == timers && _logic.StepStats.Rolls == rolls, "a kick or a bash of an empty fighter started an attack timer or a step-back roll");
            _logic.ObserveAction(st, (int)Agent.ActionCodeType.ReleaseRanged, t + 1.0, in r);
            _logic.ObserveAction(st, ActIdle, t + 1.5, in r);
            t += 2;
            Check(st.Blows == 9 && st.KicksAndBashes == 2 && st.Fraction == 0, "a kick / bash counted as a blow, or a ranged release charged: blows " + st.Blows + ", kicks/bashes " + st.KicksAndBashes);

            // a 300-skill party leader who is a hero: 5.6 a blow, 14 at full strength, empty on the 54th
            var b = FakeAgent(4);
            var lead = _logic.Track(b)!;
            lead.IsHero = true;
            lead.IsLeader = true;
            lead.AthleticsSkill = 300;
            int atFull = 0;
            for (int i = 1; i <= 54; i++)
            {
                if (AthleticsMath.PeakShare(Rules, lead) >= 1.0) atFull++;
                Swing(lead, ref t);
                if (i == 1)
                {
                    LogHas("[athletics] blow melee (on foot): (agent 4) - cost 5.6 (x0.5");
                    LogHas(": hero party leader), 300.0 → 294.4 of 300 (f 1.00 → 1.00)");
                }
                if (i == 53) Check(!lead.Exhausted, "a 300-skill party leader was empty before his 54th blow");
            }
            Check(atFull == 14, "a 300-skill party leader struck " + atFull + " blows at full strength, DESIGN says 14");
            Check(lead.Exhausted && lead.Blows == 54, "a 300-skill party leader was not empty on his 54th blow: " + lead.Blows);
            for (int i = 0; i < 4; i++) Swing(lead, ref t);

            var stats = _logic.Stats;
            Check(stats.Charged(BlowKind.Melee) == 67 && stats.MeleeReleasesSeen == 67 && stats.ExhaustionsEntered == 2 && stats.PeakLeft == 2,
                "stats: melee " + stats.Charged(BlowKind.Melee) + ", releases " + stats.MeleeReleasesSeen + ", exhaustions " + stats.ExhaustionsEntered + ", peak left " + stats.PeakLeft);
            Check(stats.KicksSeen == 1 && stats.BashesSeen == 1 && stats.KicksCharged == 1 && stats.BashesCharged == 1 && stats.KicksSeenUpper == 1
                  && stats.RangedReleasesPolled == 1, "kicks, bashes or ranged releases not counted");
            // step 5e: the attack-rate cycles by f (the Swing helper plays every phase ÷ m: on target)
            var rate = _logic.RateStats;
            Check(rate.CycleCount(AttackKind.Melee, false, 0) == 14 && rate.CycleCount(AttackKind.Melee, false, 3) == 8 && rate.CyclesMixed[0] == 0,
                "melee cycles by f: peak " + rate.CycleCount(AttackKind.Melee, false, 0) + ", empty " + rate.CycleCount(AttackKind.Melee, false, 3) + ", mixed " + rate.CyclesMixed[0]);
            Check(Near(rate.FreshCycle(AttackKind.Melee, false), 1.3) && Near(rate.Ratio(AttackKind.Melee, false, 3), 1.0),
                "the empty cycles are not the fresh 1.3 s ÷ 0.2: fresh " + rate.FreshCycle(AttackKind.Melee, false) + ", ratio " + rate.Ratio(AttackKind.Melee, false, 3));
            Check(rate.PhaseCount(AttackKind.Melee, false, 3, AttackPhase.CleanRelease) == 8 && Near(rate.PhaseMean(AttackKind.Melee, false, 3, AttackPhase.Release), 2.5),
                "empty swings: " + rate.PhaseCount(AttackKind.Melee, false, 3, AttackPhase.CleanRelease) + ", avg " + rate.PhaseMean(AttackKind.Melee, false, 3, AttackPhase.Release));
        }

        /// <summary>AI timers asked so far (every band, melee and ranged) - an attack of a tired AI fighter adds one.</summary>
        private static int AiTimersAsked()
        {
            int n = 0;
            for (int bin = 0; bin < AthleticsMath.PeakBins; bin++)
                n += _logic!.RateStats.Timers(AttackKind.Melee, false, bin) + _logic.RateStats.Timers(AttackKind.Ranged, false, bin);
            return n;
        }

        /// <summary>Step 18: kicks and shield bashes cost CostPerKickOrBash x the blow's multipliers, once each,
        /// when they start - whichever channel shows them, or at their hit when none did.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void KicksAndBashesCost()
        {
            S.Set(SettingsSchema.VerboseLogging, true, SettingSources.File);
            Check(KickBashTracker.ActionKick == (int)Agent.ActionCodeType.Kick && KickBashTracker.ActionKickContinue == (int)Agent.ActionCodeType.KickContinue
                  && KickBashTracker.ActionKickHit == (int)Agent.ActionCodeType.KickHit && KickBashTracker.ActionWeaponBash == (int)Agent.ActionCodeType.WeaponBash,
                "Core's kick / bash action codes are not the game's");
            var st = _logic!.Track(FakeAgent(20))!;
            st.AthleticsSkill = 100; // pool 100, the line at 75: these kicks never leave the peak zone
            var stats = _logic.Stats;
            int charged = stats.KickOrBashCharged, atHit = stats.KickOrBashChargedAtHit;
            var r = Rules;
            double t = 300;

            // a kick on channel 1 that lands (KickHit, its hit) - one charge of 3
            _logic.ObserveAction(st, KickBashTracker.ActionKick, t, in r);
            Check(Near(st.Fraction, 0.97) && st.KicksAndBashes == 1 && st.Blows == 0, "a kick did not cost 3 of 100: " + st.Fraction * 100);
            LogHas("[athletics] kick (on foot): (agent 20) - cost 3.0, 100.0 → 97.0 of 100 (f 1.00 → 1.00)");
            _logic.ObserveAction(st, KickBashTracker.ActionKickHit, t + 0.3, in r);
            _logic.KickOrBashHit(st, t + 0.3, in r, mounted: false);
            _logic.ObserveAction(st, ActIdle, t + 0.8, in r);
            Check(Near(st.Fraction, 0.97) && st.KicksAndBashes == 1, "a kick that landed was charged again: " + st.Fraction * 100);
            Check(st.LastBlowTime == t, "a kick did not restart the refill delay");

            // a kick on channel 0 (the whole body), shown on channel 1 too - one kick
            t += 2;
            _logic.ObserveLowerAction(st, KickBashTracker.ActionKick, t, in r);
            _logic.ObserveAction(st, KickBashTracker.ActionKick, t + 0.02, in r);
            _logic.ObserveLowerAction(st, ActIdle, t + 0.7, in r);
            _logic.ObserveAction(st, ActIdle, t + 0.7, in r);
            Check(Near(st.Fraction, 0.94) && st.KicksAndBashes == 2 && stats.KicksSeenLower == 1, "a kick on both channels was not one kick: " + st.Fraction * 100);

            // a shield bash that lands - once
            t += 2;
            _logic.ObserveAction(st, KickBashTracker.ActionWeaponBash, t, in r);
            _logic.KickOrBashHit(st, t + 0.2, in r, mounted: false);
            _logic.ObserveAction(st, ActIdle, t + 0.6, in r);
            Check(Near(st.Fraction, 0.91) && st.KicksAndBashes == 3, "a bash that landed was not charged exactly once: " + st.Fraction * 100);
            LogHas("[athletics] shield bash (on foot): (agent 20) - cost 3.0, 94.0 → 91.0 of 100");

            // a hit no channel showed: charged at the hit; the poll's late sight of it is not charged again
            t += 2;
            _logic.KickOrBashHit(st, t, in r, mounted: false);
            _logic.ObserveAction(st, KickBashTracker.ActionWeaponBash, t + 0.05, in r);
            _logic.ObserveAction(st, ActIdle, t + 0.5, in r);
            Check(Near(st.Fraction, 0.88) && st.KicksAndBashes == 4, "a kick/bash seen only at its hit: " + st.Fraction * 100);
            LogHas("[athletics] kick/bash at its hit (on foot): (agent 20) - cost 3.0, 91.0 → 88.0 of 100");
            Check(stats.KickOrBashCharged == charged + 4 && stats.KickOrBashChargedAtHit == atHit + 1 && st.Blows == 0,
                "kick/bash charges not counted apart from blows: " + (stats.KickOrBashCharged - charged) + ", at the hit " + (stats.KickOrBashChargedAtHit - atHit));

            // you, a party leader who is a hero: 3 x 0.75 x 0.75 = 1.69 - the YOU line; no attack pause
            var me = _logic.Track(FakeAgent(21))!;
            me.IsHero = true;
            me.IsLeader = true; // the floor: 50
            _logic.SmokePlayer = me.Agent;
            t += 2;
            _logic.ObserveAction(me, KickBashTracker.ActionKick, t, in r);
            Check(!_logic.PlayerTimer.Running && !_logic.PlayerTimer.Holding, "your kick started your attack pause");
            _logic.ObserveAction(me, ActIdle, t + 0.6, in r);
            Check(Near(me.Fraction * 50, 50 - 1.6875), "a hero party leader's kick did not cost 1.69: " + me.Fraction * 50);
            LogHas("[athletics] YOU: kick at ");
            LogHas(" s cost 1.69 Athletics (3.00 x0.56 hero party leader): 50.0 → 48.3 of 50 (f 1.00 → 1.00) - the first this battle: a kick or a shield bash costs CostPerKickOrBash x your hero / party-leader multipliers, once, when it starts; it never starts your attack pause");
            _logic.SmokePlayer = null;

            // live: 0 = free (counted as such); Athletics or the whole mod off = nothing at all
            double before = st.Fraction;
            int free = stats.KickOrBashFree;
            S.Set(SettingsSchema.CostPerKickOrBash, 0, SettingSources.Mcm);
            r = Rules;
            t += 2;
            _logic.ObserveAction(st, KickBashTracker.ActionKick, t, in r);
            _logic.ObserveAction(st, ActIdle, t + 0.6, in r);
            Check(st.Fraction == before && st.KicksAndBashes == 4 && stats.KickOrBashFree == free + 1 && st.LastBlowTime < t,
                "CostPerKickOrBash 0: the kick was not free (or restarted the refill delay)");
            S.Set(SettingsSchema.CostPerKickOrBash, 3, SettingSources.Mcm);
            foreach (var sw in new[] { SettingsSchema.AthleticsEnabled, SettingsSchema.ModEnabled })
            {
                S.Set(sw, false, SettingSources.Mcm);
                r = Rules;
                t += 2;
                _logic.ObserveAction(st, KickBashTracker.ActionWeaponBash, t, in r);
                _logic.ObserveAction(st, ActIdle, t + 0.6, in r);
                Check(st.Fraction == before && st.KicksAndBashes == 4 && stats.KickOrBashFree == free + 1, sw.Key + " off: a bash was charged");
                S.Set(sw, true, SettingSources.Mcm);
            }
            r = Rules;
            t += 2;
            _logic.ObserveAction(st, KickBashTracker.ActionKick, t, in r);
            _logic.ObserveAction(st, ActIdle, t + 0.6, in r);
            Check(Near(st.Fraction, before - 0.03) && st.KicksAndBashes == 5, "back on: a kick was not charged 3 again");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void HealthCapsTheBar()
        {
            var c = FakeAgent(7);
            var w = _logic!.Track(c)!;
            Check(AthleticsLogic.TryGetReading(c, out var fresh) && fresh.UsablePool == 50 && fresh.InPeakZone, "a fighter with unknown health is not full");
            _logic.CheckHealth(w, Rules, 200); // no maximum known: reads as full health
            Check(w.Fraction == 1, "an unknown maximum health cut the bar");

            SetHealth(c, 50f, 100f);
            _logic.CheckHealth(w, Rules, 201);
            var stats = _logic.Stats;
            Check(Near(w.Fraction, 0.5) && Near(w.Health, 0.5) && stats.HealthCuts == 1 && Near(stats.HealthCutMaxPoints, 25.0),
                "50% health did not cap the bar at 25 of 50: " + w.Fraction + ", cuts " + stats.HealthCuts);
            LogHas("[athletics] health cap: (agent 7) at 50% health - Athletics 50.0 → 25.0 of 50 (f 0.67)");
            Check(Near(w.SpeedMultiplier, 0.2f + 0.8f * (0.5f / 0.75f)) && Near(w.RunSpeedMultiplier, 0.7f + 0.3f * (0.5f / 0.75f)) && w.SpeedDirty,
                "the cut did not slow him on the curve: x" + w.SpeedMultiplier + ", run x" + w.RunSpeedMultiplier);
            Check(AthleticsLogic.TryGetReading(c, out var capped) && Near(capped.UsablePool, 25) && Near(capped.Points, 25) && Near(capped.PeakShare, 0.5 / 0.75)
                  && !capped.InPeakZone && Near(capped.PeakFraction, 0.75), "the read API does not show the cap");

            SetHealth(c, 90f, 100f);
            _logic.CheckHealth(w, Rules, 202);
            Check(Near(w.Fraction, 0.5) && stats.HealthCuts == 1, "healing moved the bar (only regen refills)");

            S.Set(SettingsSchema.HealthCapsAthletics, false, SettingSources.Mcm);
            SetHealth(c, 20f, 100f);
            _logic.CheckHealth(w, Rules, 203);
            Check(Near(w.Fraction, 0.5) && stats.HealthCuts == 1, "HealthCapsAthletics off still cut");
            S.Set(SettingsSchema.HealthCapsAthletics, true, SettingSources.Mcm);
            _logic.CheckHealth(w, Rules, 204);
            Check(Near(w.Fraction, 0.2) && stats.HealthCuts == 2 && Near(stats.HealthCutPoints, 40.0), "the cap back on did not cut to 20%: " + w.Fraction);
            SetHealth(c, 0f, 100f); // the killing blow: he leaves the field - no "cut" in the stats
            _logic.CheckHealth(w, Rules, 205);
            Check(Near(w.Fraction, 0.2) && stats.HealthCuts == 2, "a killing blow was counted as a health-cap cut");
            SetHealth(c, 20f, 100f);
            w.SpeedDirty = false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DamageUpsideFollowsTheAttacker()
        {
            DamageDefaults();
            S.Set(SettingsSchema.DamageBonusFollowsAthletics, true, SettingSources.File);
            DamageRandomizer.OnMissionStart();
            LogHas(", upside follows the attacker's Athletics (DamageBonusFollowsAthletics) on - read live on every hit");

            var empty = FirstTracked(3).Agent;      // the recruit, empty (f 0)
            var freshAgent = FakeAgent(8);
            _logic!.Track(freshAgent);               // full (f 1)
            var halfAgent = FakeAgent(10);
            var half = _logic.Track(halfAgent)!;
            SetHealth(halfAgent, 37.5f, 100f);       // the cap puts him at 37.5% of his bar: f 0.5
            _logic.CheckHealth(half, Rules, 300);
            half.SpeedDirty = false;
            var stranger = FakeAgent(11);            // not tracked: the full upside

            System.Threading.Thread.Sleep(200);
            S.Set(SettingsSchema.VerboseLogging, true, SettingSources.File);
            Hit(attacker: empty);
            S.Set(SettingsSchema.VerboseLogging, false, SettingSources.File);
            LogHas(", attacker f 0.00 → up to x1.00)");

            var fromEmpty = Enumerable.Range(0, 2000).Select(_ => Hit(attacker: empty)).ToList();
            var fromFresh = Enumerable.Range(0, 2000).Select(_ => Hit(attacker: freshAgent)).ToList();
            var fromHalf = Enumerable.Range(0, 2000).Select(_ => Hit(attacker: halfAgent)).ToList();
            var fromStranger = Enumerable.Range(0, 2000).Select(_ => Hit(attacker: stranger)).ToList();
            Check(fromEmpty.Max() <= 50f && fromEmpty.Min() < 26f, "an empty attacker's hits left 25..50: " + fromEmpty.Min() + ".." + fromEmpty.Max());
            Check(fromHalf.Max() <= 62.5f && fromHalf.Max() > 61f && fromHalf.Min() < 26f, "a half-strength attacker's hits left 25..62.5: " + fromHalf.Min() + ".." + fromHalf.Max());
            Check(fromFresh.Max() > 74f && fromFresh.Max() <= 75f, "a fresh attacker lost his upside: max " + fromFresh.Max());
            Check(fromStranger.Max() > 74f, "an untracked attacker lost his upside: max " + fromStranger.Max());

            var ds = DamageRandomizer.Stats;
            Check(ds.UpsideCount(3, out _, out float maxEmpty, out double allowedEmpty) == 2001 && maxEmpty <= 1f && allowedEmpty == 0,
                "upside bin empty: max x" + maxEmpty + ", allowed " + allowedEmpty);
            Check(ds.UpsideCount(1, out _, out float maxHalf, out double allowedHalf) == 2000 && maxHalf <= 1.25f && Near(allowedHalf, 0.5),
                "upside bin 0.5-1: max x" + maxHalf + ", allowed " + allowedHalf);
            Check(ds.UpsideCount(0, out _, out _, out _) == 2000 && ds.UpsideCount(4, out _, out _, out _) == 2000 && ds.AboveCeiling == 0,
                "upside bins peak / no pool / above the top: " + ds.UpsideCount(0, out _, out _, out _) + " / " + ds.UpsideCount(4, out _, out _, out _) + " / " + ds.AboveCeiling);
            DamageRandomizer.WriteSummary();
            LogHas("[summary] damage upside by the attacker's Athletics (f = the share of his peak line left; upside = how much of the +p he was allowed): peak (f 1) 2000 hits");
            LogHas("| empty (f 0) 2001 hits avg x0.");
            LogHas("| no pool (attacker not tracked) 2000 hits");
            LogHas("; rolls above their allowed top: 0");

            S.Set(SettingsSchema.DamageBonusFollowsAthletics, false, SettingSources.Mcm); // live: the next hit
            var offRolls = Enumerable.Range(0, 2000).Select(_ => Hit(attacker: empty)).ToList();
            Check(offRolls.Max() > 74f, "DamageBonusFollowsAthletics off: an empty attacker still lost his upside: max " + offRolls.Max());
            S.Set(SettingsSchema.DamageBonusFollowsAthletics, true, SettingSources.Mcm);
            DamageRandomizer.OnMissionStart();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AthleticsHotSwap()
        {
            var a = FirstTracked(3);    // empty recruit
            var lead = FirstTracked(4); // empty 300-skill leader
            var w = FirstTracked(7);    // wounded, 20% of his bar

            S.Set(SettingsSchema.ExhaustedAttackSpeedPercent, 50, SettingSources.Mcm);
            _logic!.ApplySettingsChange(S);
            Check(Near(a.SpeedMultiplier, 0.5f) && Near(lead.SpeedMultiplier, 0.5f), "ExhaustedAttackSpeedPercent 50 did not re-target the empty");
            LogHas("[speed] speed settings now: when empty attacks at 50%, run x0.70, horses x1.00; full strength at 75% of the pool - ");
            LogHas(" fighters get new speeds over the next ticks (at most 50 recomputes a tick)");
            S.Set(SettingsSchema.MinMoveSpeedMultiplier, 0.5, SettingSources.Mcm);
            _logic.ApplySettingsChange(S);
            Check(Near(a.RunSpeedMultiplier, 0.5f), "MinMoveSpeedMultiplier 0.5 did not re-target the run speed: x" + a.RunSpeedMultiplier);
            S.Set(SettingsSchema.ExhaustedAttackSpeedPercent, 20, SettingSources.Mcm);
            S.Set(SettingsSchema.MinMoveSpeedMultiplier, 0.7, SettingSources.Mcm);
            _logic.ApplySettingsChange(S);
            Check(Near(a.SpeedMultiplier, 0.2f) && Near(a.RunSpeedMultiplier, 0.7f), "back to 20% / x0.7 did not re-target");

            S.Set(SettingsSchema.AthleticsPeakPercent, 20, SettingSources.Mcm); // the wounded man (20%) is now at full strength
            _logic.ApplySettingsChange(S);
            Check(w.SpeedMultiplier == 1f && w.RunSpeedMultiplier == 1f && w.SpeedDirty, "a lower peak line did not give full strength back: x" + w.SpeedMultiplier);
            S.Set(SettingsSchema.AthleticsPeakPercent, 75, SettingSources.Mcm);
            _logic.ApplySettingsChange(S);
            Check(w.SpeedMultiplier < 1f, "the peak line back at 75% did not weaken him again");

            S.Set(SettingsSchema.MountMinSpeedMultiplier, 0.5, SettingSources.Mcm);
            _logic.ApplySettingsChange(S);
            Check(Near(a.MountSpeedMultiplier, 0.5f) && a.MountDirty, "MountMinSpeedMultiplier 0.5 did not target an empty rider's horse");
            S.Set(SettingsSchema.MountMinSpeedMultiplier, 1.0, SettingSources.Mcm);
            _logic.ApplySettingsChange(S);
            Check(a.MountSpeedMultiplier == 1f, "horses back to never-slowed did not re-target");
            a.MountDirty = false;

            S.Set(SettingsSchema.AthleticsPoolFloor, 100, SettingSources.Mcm);
            _logic.ApplySettingsChange(S);
            LogHas("[athletics] pool settings now: the Athletics skill x1.00, at least 100 - pools now min 100 / avg ");
            LogHas("; everyone keeps his share (a fighter at 60% stays at 60%)");
            Check(Near(w.Fraction, 0.2) && AthleticsLogic.TryGetReading(w.Agent, out var r100) && r100.Pool == 100 && Near(r100.Points, 20),
                "a pool change did not keep the share / reach the read API");
            S.Set(SettingsSchema.AthleticsPoolPerSkill, 0.5, SettingSources.Mcm);
            Check(AthleticsLogic.TryGetReading(lead.Agent, out var r150) && r150.Pool == 150 && r150.AthleticsSkill == 300, "per-skill 0.5 did not give a 300-skill leader 150");
            S.Set(SettingsSchema.AthleticsPoolFloor, 50, SettingSources.Mcm);
            S.Set(SettingsSchema.AthleticsPoolPerSkill, 1.0, SettingSources.Mcm);
            _logic.ApplySettingsChange(S);

            Check(AthleticsLogic.TryGetReading(a.Agent, out var read) && read.Enabled && read.Exhausted && read.Points == 0 && read.PeakShare == 0
                  && Near(read.SpeedMultiplier, 0.2f) && Near(read.RunSpeedMultiplier, 0.7f), "the read API does not report the empty fighter");
            Check(AthleticsLogic.TryGetPeakShare(a.Agent, out double fa) && fa == 0 && !AthleticsLogic.TryGetPeakShare(FakeAgent(3), out _),
                "TryGetPeakShare wrong for the empty fighter or a stranger with his index");
            Check(!AthleticsLogic.TryGetReading(FakeAgent(3), out _), "the read API answered for an untracked agent with a reused index");

            S.Set(SettingsSchema.AthleticsEnabled, false, SettingSources.Mcm);
            _logic.ApplySettingsChange(S);
            LogHas("[athletics] AthleticsEnabled switched OFF mid-mission: ");
            LogHas(" speed penalties lifted (applied over the next ticks)");
            Check(a.Fraction == 1 && !a.Exhausted && a.SpeedMultiplier == 1f && a.RunSpeedMultiplier == 1f && a.SpeedDirty, "switching off did not refill and lift the penalties");
            Check(AthleticsLogic.TryGetReading(a.Agent, out var off) && !off.Enabled && off.Points == 50 && !off.Exhausted && off.PeakShare == 1,
                "switched off, the read API is not full");
            S.Set(SettingsSchema.AthleticsEnabled, true, SettingSources.Mcm);
            _logic.ApplySettingsChange(S);
            LogHas("[athletics] AthleticsEnabled switched ON mid-mission: everyone starts full (a wound's cap applies again at the next refill step)");
        }

        private static TrackedAgent FirstTracked(int index)
        {
            var byIndex = (TrackedAgent?[])typeof(AthleticsLogic).GetField("_byIndex", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_logic);
            return byIndex[index]!;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AthleticsSummary()
        {
            _logic!.WriteAthleticsSummary();
            var stats = _logic.Stats;
            LogHas("[summary] Athletics settings at the end: ON - pool = the Athletics skill x1.00, at least 50;");
            LogHas("[summary] Athletics pools (the Athletics skill x1.00, at least 50; settings at the end): " + stats.FightersTracked + " fighters - min 50 / avg ");
            LogHas("[summary] Athletics blows charged: 67 (melee swings 67, shots/throws 0, couched/braced hits 0, landed-only swings 0, landed-only shots 0) - by riders 0, on foot 67; + kicks/bashes " + stats.KickOrBashCharged + " (not blows - their own line); Athletics spent ");
            LogHas("[summary] Athletics detection: melee releases seen 67 (mounted 0)");
            LogHas("[summary] Athletics kicks/bashes charged " + stats.KickOrBashCharged + " (");
            LogHas("| seen starting: kicks " + stats.KicksSeen + " (channel 1 " + stats.KicksSeenUpper + ", channel 0 " + stats.KicksSeenLower + "), shield bashes " + stats.BashesSeen);
            LogHas("; each charged once, when it starts; never an attack pause");
            LogHas("[summary] Athletics free (never charged): couched hits within one blow-length of the last ");
            LogHas("[summary] Athletics exhaustions (empty, f 0): 2 entered, 0 left; the peak zone: left 2 times");
            LogHas("[summary] Athletics fighter-time by f (the share of his peak line left): no fighter-time recorded");
            LogHas("[summary] Athletics you: no player fighter this mission");
            LogHas("[summary] Athletics health cap: " + stats.HealthCuts + " cuts (a wound pulled Athletics down to the health left), biggest ");
            Check(!LogText.Contains("[summary] attack speed check"), "the superseded attack speed check lines are still written");
            LogHas("[summary] attack rate settings at the end (DESIGN §2 - the attack RATE follows the attack speed m): ON - PAUSE ONLY: animations at full speed (AttackAnimationMinPercent 100)");
            LogHas("[summary] attack rate, melee, AI, peak (f 1): animations asked x1.00 - wind-up 0.50 + held 0.00, swing 0.51 (clean, hit nothing 0.51), recoil after a block -, pause 0.30 | cycle 1.30 s (n 14), m 1.00 - the fresh reference");
            LogHas("m 0.20 → target 6.50 s: 100% - on target");
            LogHas("[summary] attack rate, melee, AI - verdict: ON TARGET in ");
            LogHas("[summary] attack rate, melee, you: no attacks measured");
            LogHas("[summary] attack rate, ranged, AI, empty (f 0): animations asked x1.00 - draw - + aim -, loose 0.50, reload -, pause - | cycle n/a (n 0) - no fresh reference, no verdict"); // the one ranged release the poll saw
            LogHas("[summary] attack rate, ranged, AI - verdict: no fresh reference (0 cycles at the peak, need 5)");
            LogHas("[summary] attack rate - AI decisions (AttackRateAiDecisions off at the end): scaled in ");
            LogHas("[summary] speed updates: ");
            LogHas("[summary] run speed check, on foot (÷ the fighter's own top speed when fresh), by f: no samples");
            LogHas("[summary] run speed check, horses (÷ the horse's own top speed while its rider was fresh), by the rider's f: MountMinSpeedMultiplier 1.00 = horses never slow - no samples");
            LogHas("[summary] walk vs run speeds (tune WalkEffortFraction, now 0.40): ");
            LogHas("[summary] Athletics errors: none");
            LogHas("[speed] first exhausted fighter ((agent 3)) at mission end: recovered");
        }

        /// <summary>The master switch (ModEnabled, DESIGN §4): off = vanilla at once - the damage
        /// decorator hands back the game's number (recorded for the comparison), the stat
        /// decorator applies no penalty even before the logic's next tick, the logic refills everyone
        /// and lifts every penalty, attacks cost nothing; on again = everyone starts full.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void MasterSwitchIsVanillaLive()
        {
            // --- damage
            DamageDefaults();
            DamageRandomizer.Rng = ThreadSafeRandom.Shared;
            S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
            LogHas("[config] ModEnabled: true → false (source: MCM)");
            DamageRandomizer.OnMissionStart();
            LogHas("[damage] mission start: mod OFF (ModEnabled) - no hit is rolled, the game's own numbers are recorded for comparison; with the mod on: damage randomness ON");
            var off = Enumerable.Range(0, 40).Select(_ => Hit()).Concat(Enumerable.Range(0, 10).Select(_ => Hit(missile: true))).ToList();
            Check(off.All(v => v == 50f), "mod off: a hit was rolled");
            Check(Hit(shield: true) == 50f && Hit(fall: 3f) == 50f, "mod off: shield / fall changed");
            var ds = DamageRandomizer.Stats;
            Check(ds.VanillaHits == 50 && ds.Rolls == 0, "mod off: vanilla hits " + ds.VanillaHits + ", rolls " + ds.Rolls);
            Check(ds.Skips(DamageSkipReason.ShieldToggleOff) == 1 && ds.Skips(DamageSkipReason.FallDamage) == 1, "mod off: the other skip rules no longer decide first");
            DamageRandomizer.WriteSummary();
            LogHas("[summary] damage while the mod was OFF - the game's own numbers, not rolled (factor 1.00): 50 hits (melee 40, ranged 10, mounts 0, shields 0); damage 2500 → 2500 (+0.0%), avg 50.0 per hit");

            // --- Athletics: empty one fighter, then switch the mod off
            AthleticsDefaults();
            S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
            _logic!.ApplySettingsChange(S);
            StepBackDefaults();
            // the summary step before this one ended "the mission", which closes the step back for good
            // (no new ones after the summary) - reopen it for this check
            typeof(AthleticsLogic).GetField("_stepClosed", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_logic, false);
            var stepBody = new FakeStepBody();
            _logic.StepBackBody = stepBody;          // the engine side played by the smoke (step 5d)
            _logic.StepBackDice = new FixedDice(0.999); // only an empty bar says yes
            var a = FirstTracked(3);
            double t = 500;
            for (int i = 0; i < 5; i++) Swing(a, ref t);
            Check(a.Exhausted && Near(a.SpeedMultiplier, 0.2f) && Near(a.RunSpeedMultiplier, 0.7f), "precondition: fighter 3 not empty");
            _logic.TickStepBacks(t);
            Check(_logic.SteppingNow == 1, "precondition: fighter 3 is not stepping back");
            var p = a.Agent.AgentDrivenProperties;
            _statTop!.UpdateAgentStats(a.Agent, p);
            Check(Near(p.SwingSpeedMultiplier, 1.05f) && Near(p.MaxSpeedMultiplier, 0.56f), "precondition: no run penalty on the empty fighter (0.8 x the floor 0.7), or his animations slowed (step 13: full speed)");

            // step 5e: a pace hold running on another tired fighter (f 0.27 - no step back at these dice)
            typeof(AthleticsLogic).GetField("_paceClosed", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_logic, false);
            var paceBody = new FakePaceBody();
            _logic.PaceBody = paceBody;
            var h = _logic.Track(FakeAgent(60))!;
            for (int i = 0; i < 3; i++) Swing(h, ref t);
            var hr = Rules;
            _logic.ObserveAction(h, ActReady, t, in hr);
            t += 0.5 / h.SpeedMultiplier;
            _logic.ObserveAction(h, ActRelease, t, in hr);
            h.SpeedDirty = false;
            t += 0.5 / h.SpeedMultiplier;
            _logic.ObserveAction(h, ActIdle, t, in hr);
            _logic.TickPace(t);
            Check(_logic.HeldNow == 1 && paceBody.Started.Count == 1, "precondition: fighter 60 is not held");

            // step 13: your countdown running too (the fake agent stands in for Mission.MainAgent)
            var me = _logic.Track(FakeAgent(61))!;
            _logic.SmokePlayer = me.Agent;
            for (int i = 0; i < 3; i++) Swing(me, ref t);
            Check(_logic.PlayerTimer.Running, "precondition: your countdown is not running");

            S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
            _statTop.UpdateAgentStats(a.Agent, p); // a recompute before the logic's tick: already vanilla
            Check(Near(p.SwingSpeedMultiplier, 1.05f) && Near(p.MaxSpeedMultiplier, 0.8f), "mod off: the stat decorator still applied a penalty");
            _logic.ApplySettingsChange(S);
            LogHas("[athletics] the whole mod (ModEnabled) switched OFF mid-mission: ");
            int released = stepBody.Released.Count;
            _logic.TickStepBacks(t);
            Check(_logic.SteppingNow == 0 && stepBody.Released.Count == released + 1, "mod off: the step back was not released at once");
            LogHas("[stepback] the whole mod (ModEnabled) switched OFF mid-mission at ");
            _logic.TickPace(t);
            Check(_logic.HeldNow == 0 && paceBody.Released.Count == 1, "mod off: the pace hold was not lifted at once");
            LogHas("[rate] ModEnabled switched OFF mid-mission at ");
            LogHas(" s: 1 held fighters may attack again at once");
            _logic.TickPlayerTimer(t);
            Check(!_logic.PlayerTimer.Holding && _logic.RateStats.PlayerEnded(PlayerTimerEnd.SwitchedOff) == 1, "mod off: your pause was not released at once");
            _logic.SmokePlayer = null;
            Check(a.Fraction == 1 && !a.Exhausted && a.SpeedMultiplier == 1f && a.RunSpeedMultiplier == 1f && a.SpeedDirty, "mod off: not refilled / penalties not lifted");
            Check(AthleticsLogic.TryGetReading(a.Agent, out var read) && !read.Enabled && read.Points == read.Pool, "mod off: the read API is not full and off");
            Check(AthleticsLogic.TryGetPeakShare(a.Agent, out double f) && f == 1, "mod off: the damage decorator would not see full strength");
            // (in game the tick does not even poll swings while off; fed one anyway, it costs nothing)
            int blows = a.Blows;
            int rolls = _logic.StepStats.Rolls;
            Swing(a, ref t);
            Check(a.Blows == blows && a.Fraction == 1, "mod off: a swing was charged");
            int kicks = a.KicksAndBashes;
            var off18 = Rules;
            _logic.ObserveAction(a, KickBashTracker.ActionKick, t, in off18);
            _logic.ObserveAction(a, ActIdle, t + 0.5, in off18);
            t += 1;
            Check(a.KicksAndBashes == kicks && a.Fraction == 1, "mod off: a kick was charged (step 18)");
            Check(_logic.StepStats.Rolls == rolls, "mod off: a swing was rolled for a step back");

            S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
            _logic.ApplySettingsChange(S);
            LogHas("[athletics] the whole mod (ModEnabled) switched ON mid-mission: everyone starts full");
            Swing(a, ref t);
            Check(a.Blows == blows + 1 && a.Fraction < 1, "mod back on: a swing was not charged");
            Check(Enumerable.Range(0, 50).Select(_ => Hit()).Distinct().Count() > 5, "mod back on: damage does not roll");
            Check(AthleticsStats.DescribeRules(AthleticsRules.From(S)).StartsWith("ON - pool = the Athletics skill"), "mod back on: rules not ON");

            // --- the HUD (step 6): the Athletics bar goes the moment the switch goes, and comes back
            HudFollowsTheMasterSwitch(a.Agent);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AthleticsFailSafe()
        {
            int linesBefore = Occurrences(LogText, "[error] athletics.smoke: ");
            for (int i = 0; i < 3; i++) AthleticsLogic.Failed("athletics.smoke", new InvalidOperationException("smoke: Athletics failure"));
            Check(Occurrences(LogText, "[error] athletics.smoke: ") - linesBefore == 1, "expected exactly one [error] athletics.smoke line");
            Check(_logic!.Stats.Errors == 3, "errors counted: " + _logic.Stats.Errors);
            Check(_logic.Stats.SummaryLines(AthleticsRules.From(S), c => c.ToString(), new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, FormationAthleticsStats>>())
                .Contains("Athletics errors: 3 (athletics.smoke 3) - each failed spot fell back to vanilla (no cost, no penalty); the first per place is logged as [error] with its stack"),
                "the summary does not report the errors");

            // Leave things as a player would find them: no mission running, the file's values back.
            SetStatic(typeof(AthleticsLogic), "_current", null);
            ConfigStore.Reload("after the Athletics smoke");
        }
    }
}

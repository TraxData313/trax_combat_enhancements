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
    /// Offline checks for Athletics (step 5) - the REAL module code on .NET Framework with the
    /// game's DLLs, no game:
    ///   * <see cref="SpeedPenalty"/> on the game's own <see cref="AgentDrivenProperties"/>;
    ///   * the REAL <see cref="TraxAgentStatModel"/> decorating a stand-in base model that, like
    ///     Sandbox's and CustomBattle's, assigns fresh attack-speed values on every recompute;
    ///   * the REAL <see cref="AthleticsLogic"/>: swings fed through its action observer, charges,
    ///     exhaustion, the speed target, hot swap, the read API, the summary, the error path.
    /// The agents are UNINITIALIZED <see cref="Agent"/> objects (no native side) with only their
    /// index and driven properties set - so nothing here may call a native member (IsActive,
    /// IsHuman, GetCurrentActionType, UpdateAgentProperties' engine push). What it cannot check:
    /// that the engine honours a 0.2 multiplier, the poll, the hit events - PLAYTEST §3 and the
    /// [summary]'s attack-speed check prove those in game.
    /// </summary>
    internal static partial class Program
    {
        private const int ActIdle = (int)Agent.ActionCodeType.Idle;
        private const int ActReady = (int)Agent.ActionCodeType.ReadyMelee;
        private const int ActRelease = (int)Agent.ActionCodeType.ReleaseMelee;

        private static AthleticsLogic? _logic;
        private static TraxAgentStatModel? _statTop;
        private static FreshStatsModel? _statBase;

        /// <summary>Stands in for Sandbox's model: assigns FRESH attack-speed values on every recompute
        /// (Sandbox's and CustomBattle's UpdateHumanStats do, with '=').</summary>
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

        /// <summary>An Agent object without its native side: index + driven properties only. The two
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

        private static void AthleticsDefaults()
        {
            S.ResetToDefaults(SettingSources.Defaults);
        }

        // ------------------------------------------------------------------ checks

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SpeedPenaltyScalesOnlyTheThree()
        {
            var p = new AgentDrivenProperties();
            // every slot gets its own value (the enum has aliases - one slot, several names)
            var slots = Enum.GetValues(typeof(DrivenProperty)).Cast<DrivenProperty>().Where(d => (int)d >= 0 && (int)d < 98)
                .GroupBy(d => (int)d).Select(g => g.First()).ToList();
            foreach (var d in slots) p.SetStat(d, 1f + (int)d / 100f);
            float swing = p.SwingSpeedMultiplier, thrust = p.ThrustOrRangedReadySpeedMultiplier, reload = p.ReloadSpeed;
            var before = slots.ToDictionary(d => d, d => p.GetStat(d));

            SpeedPenalty.Scale(p, 0.2f);
            Check(Near(p.SwingSpeedMultiplier, swing * 0.2f), "swing not x0.2: " + p.SwingSpeedMultiplier);
            Check(Near(p.ThrustOrRangedReadySpeedMultiplier, thrust * 0.2f), "thrust/draw not x0.2: " + p.ThrustOrRangedReadySpeedMultiplier);
            Check(Near(p.ReloadSpeed, reload * 0.2f), "reload not x0.2: " + p.ReloadSpeed);
            foreach (var pair in before)
            {
                int slot = (int)pair.Key;
                if (slot == (int)DrivenProperty.SwingSpeedMultiplier || slot == (int)DrivenProperty.ThrustOrRangedReadySpeedMultiplier
                    || slot == (int)DrivenProperty.ReloadSpeed) continue;
                Check(p.GetStat(pair.Key) == pair.Value, pair.Key + " changed: " + pair.Value + " → " + p.GetStat(pair.Key));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void DecoratorAppliesEachFightersMultiplier()
        {
            AthleticsDefaults();
            _statBase = new FreshStatsModel();
            _statTop = new TraxAgentStatModel(new AgentStatCalculateModel[] { _statBase });
            _statTop.Initialize(_statBase);
            _logic = new AthleticsLogic();
            SetStatic(typeof(AthleticsLogic), "_current", _logic);
            typeof(AthleticsLogic).GetMethod("StartAthletics", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_logic, null);
            LogHas("[athletics] mission start: ON - pool 100, cost per blow 10.0 / hero 7.5 / party leader 5.");
            LogHas("[athletics] party-leader rule: no campaign (custom battle) - the side's general, or every hero of a side without one");

            var a = FakeAgent(3);
            var st = _logic.Track(a)!;
            Check(st != null && ReferenceEquals(_logic.Track(a), st), "Track is not idempotent");
            Check(!st!.IsHero && !st.IsLeader, "an agent without a character was flagged hero/leader");
            var p = a.AgentDrivenProperties;

            _statTop.UpdateAgentStats(a, p);
            Check(Near(p.SwingSpeedMultiplier, 1.05f) && Near(p.ThrustOrRangedReadySpeedMultiplier, 1.02f) && Near(p.ReloadSpeed, 0.95f),
                "a fresh fighter's properties were changed: " + SpeedPenalty.Snapshot.Take(a));

            st.SpeedMultiplier = 0.2f;
            _statTop.UpdateAgentStats(a, p);
            Check(Near(p.SwingSpeedMultiplier, 0.21f) && Near(p.ThrustOrRangedReadySpeedMultiplier, 0.204f) && Near(p.ReloadSpeed, 0.19f),
                "the multiplier was not applied: " + SpeedPenalty.Snapshot.Take(a));
            Check(Near(p.HandlingMultiplier, 1.1f), "HandlingMultiplier was touched");
            _statTop.UpdateAgentStats(a, p); // a second recompute (weapon switch): the base resets, we scale once
            Check(Near(p.SwingSpeedMultiplier, 0.21f), "the penalty compounded over two recomputes: " + p.SwingSpeedMultiplier);
            Check(_statBase.Updates == 3, "the base model was not called on every recompute: " + _statBase.Updates);

            S.Set(SettingsSchema.AthleticsEnabled, false, SettingSources.Mcm);
            _statTop.UpdateAgentStats(a, p);
            Check(Near(p.SwingSpeedMultiplier, 1.05f), "AthleticsEnabled off did not lift the penalty on a recompute");
            S.Set(SettingsSchema.AthleticsEnabled, true, SettingSources.Mcm);

            var twin = FakeAgent(3); // same index, another agent (indices are reused)
            _statTop.UpdateAgentStats(twin, twin.AgentDrivenProperties);
            Check(Near(twin.AgentDrivenProperties.SwingSpeedMultiplier, 1.05f), "an untracked agent with a reused index got the penalty");
            var stranger = FakeAgent(5000);
            _statTop.UpdateAgentStats(stranger, stranger.AgentDrivenProperties);
            Check(Near(stranger.AgentDrivenProperties.SwingSpeedMultiplier, 1.05f), "an untracked agent got a penalty");

            SetStatic(typeof(AthleticsLogic), "_current", null);
            _statTop.UpdateAgentStats(a, p);
            Check(Near(p.SwingSpeedMultiplier, 1.05f), "no mission running, yet a penalty was applied");
            SetStatic(typeof(AthleticsLogic), "_current", _logic);

            Check(_logic.Stats.DecoratorScaled == 2, "decorator-applied count " + _logic.Stats.DecoratorScaled + ", expected 2");
            st.SpeedMultiplier = 1f;
        }

        private static void Swing(TrackedAgent st, ref double t, double ready, double release, double rest)
        {
            var r = AthleticsRules.From(S);
            _logic!.ObserveAction(st, ActReady, t, in r);
            t += ready;
            _logic.ObserveAction(st, ActRelease, t, in r);
            t += release;
            _logic.ObserveAction(st, ActIdle, t, in r);
            t += rest;
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
            double t = 100;

            Swing(st, ref t, 0.5, 0.5, 0.3);
            Check(Math.Abs(st.Fraction - 0.9) < 1e-9, "one swing did not cost 10 of 100: " + st.Fraction);
            LogHas("[athletics] blow melee (on foot): (agent 3) - cost 10.0, 100.0 → 90.0 of 100");
            for (int i = 0; i < 8; i++) Swing(st, ref t, 0.5, 0.5, 0.3);
            Check(!st.Exhausted && Math.Abs(st.Fraction - 0.1) < 1e-9, "after 9 swings: " + st.Fraction + (st.Exhausted ? " exhausted" : ""));
            Swing(st, ref t, 0.5, 0.5, 0.3);
            Check(st.Exhausted && st.Fraction == 0, "10 swings did not exhaust a soldier");
            Check(Near(st.SpeedMultiplier, 0.2f) && st.SpeedDirty, "exhaustion did not target x0.2 and ask for a recompute");
            LogHas("10.0 → 0.0 of 100 - EXHAUSTED");

            // exhausted swings: slower in every phase (what 20% speed should look like in game)
            for (int i = 0; i < 4; i++) Swing(st, ref t, 2.5, 2.5, 1.0);
            Check(st.Blows == 14 && st.Exhausted && st.ExhaustionsEntered == 1, "exhausted swings: blows " + st.Blows + ", entries " + st.ExhaustionsEntered);

            // free: kicks, bashes; ranged releases are counted by the poll but charged by the shot event
            var r = AthleticsRules.From(S);
            _logic.ObserveAction(st, (int)Agent.ActionCodeType.Kick, t, in r);
            _logic.ObserveAction(st, (int)Agent.ActionCodeType.WeaponBash, t + 0.5, in r);
            _logic.ObserveAction(st, (int)Agent.ActionCodeType.ReleaseRanged, t + 1.0, in r);
            _logic.ObserveAction(st, ActIdle, t + 1.5, in r);
            t += 2;
            Check(st.Blows == 14, "a kick, bash or ranged release was charged");

            var stats = _logic.Stats;
            Check(stats.Charged(BlowKind.Melee) == 14 && stats.MeleeReleasesSeen == 14 && stats.ExhaustionsEntered == 1,
                "stats: melee " + stats.Charged(BlowKind.Melee) + ", releases " + stats.MeleeReleasesSeen + ", exhaustions " + stats.ExhaustionsEntered);
            Check(stats.KicksSeen == 1 && stats.BashesSeen == 1 && stats.RangedReleasesPolled == 1, "free actions not counted");
            Check(stats.MeleeFresh.Count == 9 && stats.MeleeExhausted.Count == 4, "intervals fresh/exhausted " + stats.MeleeFresh.Count + "/" + stats.MeleeExhausted.Count);
            Check(stats.SwingFresh.Count == 9 && stats.SwingExhausted.Count == 4, "swing lengths fresh/exhausted " + stats.SwingFresh.Count + "/" + stats.SwingExhausted.Count);

            // a party leader who is a hero: 5.6 a blow, empty on the 18th
            var b = FakeAgent(4);
            var lead = _logic.Track(b)!;
            lead.IsHero = true;
            lead.IsLeader = true;
            Swing(lead, ref t, 0.5, 0.5, 0.3);
            LogHas("[athletics] blow melee (on foot): (agent 4) - cost 5.6 (x0.5");
            LogHas(": hero party leader), 100.0 → 94.4 of 100");
            for (int i = 0; i < 16; i++) Swing(lead, ref t, 0.5, 0.5, 0.3);
            Check(!lead.Exhausted, "a party leader was empty before his 18th blow");
            Swing(lead, ref t, 0.5, 0.5, 0.3);
            Check(lead.Exhausted && lead.Blows == 18, "a party leader was not empty on his 18th blow: " + lead.Blows);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AthleticsHotSwap()
        {
            var a = FirstTracked(3);
            var lead = FirstTracked(4);

            S.Set(SettingsSchema.ExhaustedAttackSpeedPercent, 50, SettingSources.Mcm);
            _logic!.ApplySettingsChange(S);
            Check(Near(a.SpeedMultiplier, 0.5f) && Near(lead.SpeedMultiplier, 0.5f), "ExhaustedAttackSpeedPercent 50 did not re-target the exhausted");
            LogHas("[speed] ExhaustedAttackSpeedPercent now 50%: 2 exhausted fighters get the new speed on the next tick");
            S.Set(SettingsSchema.ExhaustedAttackSpeedPercent, 20, SettingSources.Mcm);
            _logic.ApplySettingsChange(S);
            Check(Near(a.SpeedMultiplier, 0.2f), "back to 20% did not re-target");

            Check(AthleticsLogic.TryGetReading(a.Agent, out var read) && read.Enabled && read.Exhausted && read.Points == 0 && Near(read.SpeedMultiplier, 0.2f),
                "the read API does not report the exhausted fighter");
            Check(!AthleticsLogic.TryGetReading(FakeAgent(3), out _), "the read API answered for an untracked agent with a reused index");

            S.Set(SettingsSchema.MaxAthletics, 150, SettingSources.Mcm);
            var r150 = AthleticsRules.From(S);
            Check(Math.Abs(AthleticsMath.Points(r150, lead) - 0) < 1e-9 && AthleticsLogic.TryGetReading(lead.Agent, out var r2) && r2.Pool == 150,
                "a pool change did not reach the read API");
            S.Set(SettingsSchema.MaxAthletics, 100, SettingSources.Mcm);

            S.Set(SettingsSchema.AthleticsEnabled, false, SettingSources.Mcm);
            _logic.ApplySettingsChange(S);
            LogHas("[athletics] AthleticsEnabled switched OFF mid-mission: 2 fighters back to full, 2 attack-speed penalties lifted (applied on the next tick)");
            Check(a.Fraction == 1 && !a.Exhausted && a.SpeedMultiplier == 1f && a.SpeedDirty, "switching off did not refill and lift the penalty");
            Check(AthleticsLogic.TryGetReading(a.Agent, out var off) && !off.Enabled && off.Points == 100 && !off.Exhausted, "switched off, the read API is not full");
            S.Set(SettingsSchema.AthleticsEnabled, true, SettingSources.Mcm);
            _logic.ApplySettingsChange(S);
            LogHas("[athletics] AthleticsEnabled switched ON mid-mission: everyone starts full");
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
            LogHas("[summary] Athletics settings at the end: ON - pool 100");
            LogHas("[summary] Athletics blows charged: 32 (melee swings 32, shots/throws 0, couched/braced hits 0, landed-only swings 0, landed-only shots 0) - by riders 0, on foot 32; Athletics spent ");
            LogHas("[summary] Athletics detection: melee releases seen 32 (mounted 0)");
            LogHas("[summary] Athletics free (never charged): kicks 1, shield bashes 1,");
            LogHas("[summary] Athletics exhaustions: 2 entered, 0 left");
            LogHas("[summary] Athletics you: no player fighter this mission");
            LogHas("[summary] attack speed check, melee - time between swings: fresh median ");
            LogHas("- exhausted attacks ARE slower");
            LogHas("[summary] attack speed check, melee - swing length (swings that hit nothing): fresh median ");
            LogHas("[summary] attack speed check, ranged - time between shots: fresh no samples | exhausted no samples - not enough samples to judge");
            LogHas("[summary] speeds for step 5c: ");
            LogHas("[summary] Athletics errors: none");
            LogHas("[speed] first exhausted fighter ((agent 3)) at mission end: recovered");
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

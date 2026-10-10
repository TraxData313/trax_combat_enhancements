using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Models;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for damage randomness (step 4): the REAL <see cref="TraxDamageModel"/>
    /// decorating the game's own <see cref="CustomAgentApplyDamageModel"/> (whose
    /// ApplyGeneralDamageModifiers returns its input - so every change seen here is ours), fed
    /// hits built from the game's own structs (<c>AttackCollisionData.GetAttackCollisionDataForDebugPurpose</c>,
    /// <c>AttackInformation</c>'s public fields) on .NET Framework with the game's DLLs. What it
    /// cannot check: that the engine really calls us (PLAYTEST §2 - the "[damage] first roll this
    /// mission" line proves it in game).
    /// </summary>
    internal static partial class Program
    {
        private static TraxDamageModel? _damageModel;

        private static TraxSettings S => TraxSettings.Shared;

        /// <summary>One hit through the decorator; returns what the game would receive.</summary>
        private static float Hit(bool shield = false, bool agent = true, bool missile = false, bool mount = false,
            float charge = 0f, float fall = 0f, float damage = 50f, Agent? attacker = null)
        {
            var cd = AttackCollisionData.GetAttackCollisionDataForDebugPurpose(
                shield, false, false, agent, false, missile, false, false, !agent, false, false, false,
                default(CombatCollisionResult), 0, 0, 0, 0, default(BoneBodyPartType), 0, default(Agent.UsageDirection), 0,
                default(CombatHitResultFlags), 0.5f, 0.5f, 0f, 0f, 0f, 0f, charge, fall,
                Vec3.Zero, Vec3.Zero, Vec3.Zero, Vec3.Zero, Vec3.Zero, Vec3.Zero, Vec3.Zero);
            var ai = new AttackInformation { IsVictimAgentNull = !agent, IsVictimAgentMount = mount, AttackerAgent = attacker! };
            return _damageModel!.ApplyGeneralDamageModifiers(in ai, in cd, damage);
        }

        private static void DamageDefaults()
        {
            S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.DamageRandomEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.DamageRandomPercent, 50, SettingSources.File);
            S.Set(SettingsSchema.DamageRandomMelee, true, SettingSources.File);
            S.Set(SettingsSchema.DamageRandomRanged, true, SettingSources.File);
            S.Set(SettingsSchema.DamageRandomOnMounts, true, SettingSources.File);
            S.Set(SettingsSchema.DamageRandomOnShields, false, SettingSources.File);
            S.Set(SettingsSchema.DamageBonusFollowsAthletics, true, SettingSources.File);
            S.Set(SettingsSchema.VerboseLogging, false, SettingSources.File);
            // Step 25 has its own step (DamageReductionThroughDecorator): the older checks see the roll alone, to the bit.
            S.Set(SettingsSchema.DamageReductionPercent, 0, SettingSources.File);
        }

        private static void DamageRollsThroughDecorator()
        {
            DamageDefaults();
            _damageModel = new TraxDamageModel();
            _damageModel.Initialize(new CustomAgentApplyDamageModel());
            Check(_damageModel.BaseModelName == typeof(CustomAgentApplyDamageModel).FullName, "decorator base is " + _damageModel.BaseModelName);
            DamageRandomizer.Rng = ThreadSafeRandom.Shared; // the real dice, on .NET Framework
            DamageRandomizer.OnMissionStart();
            LogHas("[damage] mission start: damage randomness ON, spread ±50% (a 50-damage hit lands for 25-75), melee on, ranged on, on mounts on, on shields off, upside follows the attacker's Athletics (DamageBonusFollowsAthletics) on - read live on every hit");

            var values = new List<float>();
            for (int i = 0; i < 4000; i++) values.Add(Hit());
            Check(values.All(v => v >= 25f && v <= 75f), "a melee roll left 25..75: " + values.Min() + ".." + values.Max());
            Check(values.Average() > 49f && values.Average() < 51f, "melee mean " + values.Average() + " is not ~50");
            Check(values.Min() < 26f && values.Max() > 74f, "the whole range is not reached: " + values.Min() + ".." + values.Max());
            Check(values.Select(v => DamageRoll.GameRound(v)).Distinct().Count() > 40, "the rounded damage barely varies");
            LogHas("[damage] first roll this mission - on the main thread: melee on a person: (nobody) → (nobody), unarmed, 50 → ");

            for (int i = 0; i < 300; i++)
            {
                float r = Hit(missile: true), m = Hit(mount: true), c = Hit(charge: 4f);
                Check(r >= 25f && r <= 75f && m >= 25f && m <= 75f && c >= 25f && c <= 75f, "ranged/mount/charge out of range: " + r + " " + m + " " + c);
            }
            S.Set(SettingsSchema.DamageRandomOnShields, true, SettingSources.File);
            var shields = Enumerable.Range(0, 300).Select(_ => Hit(shield: true)).ToList();
            Check(shields.All(v => v >= 25f && v <= 75f) && shields.Distinct().Count() > 50, "shield damage not rolled with DamageRandomOnShields on");
            S.Set(SettingsSchema.DamageRandomOnShields, false, SettingSources.File);

            var st = DamageRandomizer.Stats;
            Check(st.RollsIn(DamageCategory.Melee) == 4300, "melee rolls " + st.RollsIn(DamageCategory.Melee) + " (4000 + 300 charges)");
            Check(st.RollsIn(DamageCategory.Ranged) == 300 && st.RollsIn(DamageCategory.Mount) == 300 && st.RollsIn(DamageCategory.Shield) == 300,
                "per-kind counts " + st.RollsIn(DamageCategory.Ranged) + "/" + st.RollsIn(DamageCategory.Mount) + "/" + st.RollsIn(DamageCategory.Shield));
            st.Factors(out float min, out double mean, out float max);
            Check(min >= 0.5f && max < 1.5f && mean > 0.98 && mean < 1.02, "factor stats " + min + " / " + mean + " / " + max);
            var h = st.Histogram();
            double even = h.Sum() / (double)h.Length;
            Check(h.All(b => b > even * 0.75 && b < even * 1.25), "dice histogram not even: " + string.Join(" ", h));
            Check(st.SkipsTotal == 0 && st.Errors == 0 && st.OffMainThread == 0, "unexpected skips/errors/off-thread");
        }

        private static void DamageSkipRules()
        {
            var st = DamageRandomizer.Stats;
            Check(Hit(shield: true) == 50f, "a shield block was rolled with DamageRandomOnShields off");
            Check(Hit(fall: 6f) == 50f, "fall damage was rolled");
            Check(Hit(agent: false) == 50f, "a hit on an object was rolled");
            Check(Hit(damage: 0.4f) == 0.4f, "a hit the game shows as 0 was changed");
            Check(st.Skips(DamageSkipReason.ShieldToggleOff) == 1 && st.Skips(DamageSkipReason.FallDamage) == 1
                && st.Skips(DamageSkipReason.NotAnAgent) == 1 && st.Skips(DamageSkipReason.ZeroDamage) == 1,
                "skip counts " + st.Skips(DamageSkipReason.ShieldToggleOff) + "/" + st.Skips(DamageSkipReason.FallDamage)
                + "/" + st.Skips(DamageSkipReason.NotAnAgent) + "/" + st.Skips(DamageSkipReason.ZeroDamage));

            // A 1-damage hit rolled at x0.5..1.5 must still land for 1 (the game would round 0.5 to 0).
            var tiny = Enumerable.Range(0, 400).Select(_ => Hit(damage: 1f)).ToList();
            Check(tiny.All(v => v >= 1f && v <= 1.5f), "a positive hit fell below 1: " + tiny.Min());
            Check(tiny.All(v => DamageRoll.GameRound(v) == 1), "a 1-damage hit did not stay 1 on screen");
        }

        private static void DamageHotSwap()
        {
            var st = DamageRandomizer.Stats;
            S.Set(SettingsSchema.DamageRandomPercent, 0, SettingSources.File);
            Check(Hit() == 50f && Hit(missile: true) == 50f, "spread 0 still rolled");
            S.Set(SettingsSchema.DamageRandomPercent, 10, SettingSources.File);
            var narrow = Enumerable.Range(0, 400).Select(_ => Hit()).ToList();
            Check(narrow.All(v => v >= 45f && v <= 55f) && narrow.Min() < 46f && narrow.Max() > 54f, "spread 10 not applied at once: " + narrow.Min() + ".." + narrow.Max());
            S.Set(SettingsSchema.DamageRandomPercent, 50, SettingSources.File);

            S.Set(SettingsSchema.DamageRandomRanged, false, SettingSources.File);
            Check(Hit(missile: true) == 50f, "ranged off: an arrow was rolled");
            Check(Enumerable.Range(0, 50).Select(_ => Hit()).Distinct().Count() > 5, "ranged off stopped melee rolls too");
            S.Set(SettingsSchema.DamageRandomRanged, true, SettingSources.File);

            S.Set(SettingsSchema.DamageRandomOnMounts, false, SettingSources.File);
            Check(Hit(mount: true) == 50f && Hit(mount: true, missile: true) == 50f, "mounts off: a hit on a horse was rolled");
            S.Set(SettingsSchema.DamageRandomOnMounts, true, SettingSources.File);

            S.Set(SettingsSchema.DamageRandomMelee, false, SettingSources.File);
            Check(Hit() == 50f && Hit(charge: 4f) == 50f, "melee off: a sword or a charge bump was rolled");
            Check(Enumerable.Range(0, 50).Select(_ => Hit(missile: true)).Distinct().Count() > 5, "melee off stopped ranged rolls too");
            S.Set(SettingsSchema.DamageRandomMelee, true, SettingSources.File);

            S.Set(SettingsSchema.DamageRandomEnabled, false, SettingSources.File);
            Check(Hit() == 50f && Hit(missile: true) == 50f && Hit(mount: true) == 50f, "master switch off still rolled");
            S.Set(SettingsSchema.DamageRandomEnabled, true, SettingSources.File);
            Check(Enumerable.Range(0, 50).Select(_ => Hit()).Distinct().Count() > 5, "master switch back on: no rolls");

            Check(st.Skips(DamageSkipReason.SpreadZero) == 2 && st.Skips(DamageSkipReason.RangedToggleOff) == 1
                && st.Skips(DamageSkipReason.MountToggleOff) == 2 && st.Skips(DamageSkipReason.MeleeToggleOff) == 2
                && st.Skips(DamageSkipReason.SwitchedOff) == 3, "hot-swap skip counts wrong");
            LogHas("[config] DamageRandomPercent: 50 → 0 (source: file)");
        }

        /// <summary>Dice that throw - stands in for any bug in our part of the hook.</summary>
        private sealed class BrokenDice : IRandomSource
        {
            public double NextDouble() => throw new InvalidOperationException("smoke: broken dice");
        }

        private static void DamageFailSafe()
        {
            int errorsBefore = TraxLog.ErrorCount;
            int linesBefore = Occurrences(LogText, "[error] damage.roll: ");
            DamageRandomizer.Rng = new BrokenDice();
            try
            {
                for (int i = 0; i < 5; i++)
                    Check(Hit() == 50f, "a failing roll did not return the game's value");
            }
            finally
            {
                DamageRandomizer.Rng = ThreadSafeRandom.Shared;
            }
            Check(DamageRandomizer.Stats.Errors == 5, "errors counted: " + DamageRandomizer.Stats.Errors);
            Check(Occurrences(LogText, "[error] damage.roll: ") - linesBefore == 1, "expected exactly one [error] damage.roll line with its stack");
            LogHas("smoke: broken dice");
            Check(TraxLog.ErrorCount - errorsBefore == 1, "the global error count should see the one reported error");
            Check(Hit() != 50f || Hit() != 50f, "rolls did not resume after the dice were fixed");
        }

        private static void DamageLogAndSummary()
        {
            // The earlier flood check emptied the [damage] verbose bucket (40 burst, 20/s refill):
            // one second buys the 20 lines this check needs.
            Thread.Sleep(1000);
            S.Set(SettingsSchema.VerboseLogging, true, SettingSources.File);
            Hit();
            Hit(shield: true);
            Hit(missile: true, mount: true);
            Hit(charge: 4f);
            Hit(fall: 6f);
            Hit(agent: false);
            S.Set(SettingsSchema.VerboseLogging, false, SettingSources.File);
            LogHas("[damage] melee on a person: (nobody) → (nobody), unarmed, 50 → ");
            LogHas("[damage] ranged on a mount: (nobody) → (nobody), unarmed, 50 → ");
            LogHas("[damage] horse charge on a person: (nobody) → (nobody), charge bump, 50 → ");
            LogHas("[damage] not rolled - shield blocks (DamageRandomOnShields off): melee on a shield: (nobody) → (nobody), unarmed, 50");
            LogHas("[damage] not rolled - fall damage: fall on a person: (nobody) → (nobody), no weapon, 50");
            LogHas("[damage] not rolled - objects (doors, siege engines, ships): melee on an object: (nobody) → (object), unarmed, 50");
            int before = Occurrences(LogText, "[damage] melee on a person");
            Hit();
            Check(Occurrences(LogText, "[damage] melee on a person") == before, "a roll line was written with VerboseLogging off");

            DamageRandomizer.WriteSummary();
            LogHas("[summary] damage rolls: ");
            LogHas("[summary] damage by kind: melee ");
            LogHas(" | ranged ");
            LogHas(" | mounts ");
            LogHas(" | shields ");
            LogHas("[summary] damage dice, 10 equal slices from the lowest to the highest possible roll (even = fair): ");
            LogHas("[summary] damage not rolled: ");
            foreach (var reason in new[]
                     {
                         DamageSkipReason.NotAnAgent, DamageSkipReason.FallDamage, DamageSkipReason.ZeroDamage, DamageSkipReason.SwitchedOff,
                         DamageSkipReason.SpreadZero, DamageSkipReason.ShieldToggleOff, DamageSkipReason.MountToggleOff,
                         DamageSkipReason.MeleeToggleOff, DamageSkipReason.RangedToggleOff,
                     })
                LogHas(DamageStats.SkipName(reason) + " ");
            LogHas("[summary] damage roll errors: 5 (damage.roll 5) - each of those hits kept the game's own damage");
            LogHas("[summary] damage rolls ran on the main thread: all ");

            DamageRandomizer.OnMissionStart(); // the next mission starts from zero
            Check(DamageRandomizer.Stats.Rolls == 0 && DamageRandomizer.Stats.SkipsTotal == 0 && DamageRandomizer.Stats.Errors == 0, "stats not reset at mission start");
        }

        private static void DamageOffMainThread()
        {
            float fromWorker = 0;
            var t = new Thread(() => fromWorker = Hit());
            t.Start();
            t.Join();
            Check(fromWorker >= 25f && fromWorker <= 75f, "a roll on a worker thread went wrong: " + fromWorker);
            Check(DamageRandomizer.Stats.OffMainThread == 1, "the off-main-thread roll was not counted");
            LogHas("[damage] first roll this mission - on thread ");
            Check(DamageRandomizer.Stats.SummaryLines().Any(l => l.StartsWith("damage rolls OFF the main thread: 1 of 1", StringComparison.Ordinal)), "summary does not call out the off-thread roll");

            // Leave things as a player would find them: fresh stats, the file's values back in memory.
            DamageRandomizer.OnMissionStart();
            ConfigStore.Reload("after the damage smoke");
        }

        /// <summary>Step 25: the overall damage reduction through the real decorator - after the roll, on
        /// every kind of hit, with the roll off too; never falls / objects / a 0 hit; a positive hit never
        /// below 1; live; the master switch; the log lines and the [summary] line.</summary>
        private static void DamageReductionThroughDecorator()
        {
            DamageDefaults();
            S.Set(SettingsSchema.DamageReductionPercent, 25, SettingSources.File);
            DamageRandomizer.Rng = ThreadSafeRandom.Shared;
            DamageRandomizer.OnMissionStart();
            LogHas("- read live on every hit; overall damage reduction (DamageReductionPercent) 25% after the roll, rolled or not (an average 100 hit lands 75; falls and objects never)");

            // 1. After the roll: a 100 hit rolls 50..150, lands 37.5..112.5, 75 on average.
            var values = Enumerable.Range(0, 4000).Select(_ => Hit(damage: 100f)).ToList();
            Check(values.All(v => v >= 37.49f && v <= 112.51f), "reduced roll out of 37.5..112.5: " + values.Min() + ".." + values.Max());
            Check(values.Average() > 74f && values.Average() < 76f, "reduced mean " + values.Average() + " is not ~75");
            Check(values.Min() < 39f && values.Max() > 111f, "the reduced range is not reached: " + values.Min() + ".." + values.Max());
            LogHas("[damage] first reduced hit this mission - melee on a person: (nobody) → (nobody), unarmed: the game's 100 → after the roll ");
            LogHas("% (DamageReductionPercent) → lands ");
            var st = DamageRandomizer.Stats;
            Check(st.Rolls == 4000 && st.Reduced == 4000, "rolls " + st.Rolls + ", reduced " + st.Reduced);
            st.ReductionDamage(out long game, out long beforeR, out long landed);
            Check(game == 400000 && Math.Abs(landed - beforeR * 0.75) < 4000 * 0.6, "reduction sums " + game + " → " + beforeR + " → " + landed);

            // 2. The roll switched off: still reduced, exactly - every kind of hit, shields too.
            S.Set(SettingsSchema.DamageRandomEnabled, false, SettingSources.File);
            Check(Hit(damage: 100f) == 75f, "roll off: a 100 melee hit is not 75");
            Check(Hit(damage: 100f, missile: true) == 75f && Hit(damage: 100f, mount: true) == 75f
                && Hit(damage: 100f, shield: true) == 75f && Hit(damage: 100f, charge: 4f) == 75f,
                "roll off: ranged / mount / shield / charge not reduced to 75");
            // never: a fall, an object, a 0 hit; a positive hit never below 1
            Check(Hit(damage: 100f, fall: 6f) == 100f, "a fall was reduced");
            Check(Hit(damage: 100f, agent: false) == 100f, "a hit on an object was reduced");
            Check(Hit(damage: 0.4f) == 0.4f, "a hit the game shows as 0 was changed");
            Check(Hit(damage: 1f) == 1f, "a 1-damage hit fell below 1");
            S.Set(SettingsSchema.DamageReductionPercent, 90, SettingSources.File);
            Check(Hit(damage: 5f) == 1f && Hit(damage: 100f) == 10f, "90%: 5 → 1 (the floor), 100 → 10");
            S.Set(SettingsSchema.DamageReductionPercent, 25, SettingSources.File);
            Check(st.ReductionSkips(ReductionSkipReason.FallDamage) == 1 && st.ReductionSkips(ReductionSkipReason.NotAnAgent) == 1
                && st.ReductionSkips(ReductionSkipReason.ZeroDamage) == 1,
                "reduction skips " + st.ReductionSkips(ReductionSkipReason.FallDamage) + "/" + st.ReductionSkips(ReductionSkipReason.NotAnAgent)
                + "/" + st.ReductionSkips(ReductionSkipReason.ZeroDamage));

            // 3. Live: the next hit sees an MCM change; 0 = the game's damage.
            S.Set(SettingsSchema.DamageReductionPercent, 50, SettingSources.Mcm);
            LogHas("[config] DamageReductionPercent: 25 → 50 (source: MCM)");
            Check(Hit(damage: 100f) == 50f, "50% not applied at the next hit");
            S.Set(SettingsSchema.DamageReductionPercent, 0, SettingSources.Mcm);
            Check(Hit(damage: 100f) == 100f && Hit(damage: 100f, missile: true) == 100f, "0%: a hit was still reduced");
            Check(st.ReductionSkips(ReductionSkipReason.PercentZero) == 2, "reduction 0 not counted: " + st.ReductionSkips(ReductionSkipReason.PercentZero));
            S.Set(SettingsSchema.DamageReductionPercent, 25, SettingSources.Mcm);

            // 4. The master switch: off = the game's value (rolled or not), counted; on again = reduced again.
            S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
            Check(Hit(damage: 100f) == 100f && Hit(damage: 100f, shield: true) == 100f, "mod off: a hit was reduced");
            S.Set(SettingsSchema.DamageRandomEnabled, true, SettingSources.File);
            Check(Enumerable.Range(0, 20).All(_ => Hit(damage: 100f) == 100f), "mod off with the roll on: a hit was rolled or reduced");
            Check(st.ReductionSkips(ReductionSkipReason.ModOff) == 22, "mod-off reduction skips " + st.ReductionSkips(ReductionSkipReason.ModOff));
            S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
            S.Set(SettingsSchema.DamageRandomEnabled, false, SettingSources.File);
            Check(Hit(damage: 100f) == 75f, "mod back on: not reduced");

            // 5. The verbose lines say it, the summary proves it.
            Thread.Sleep(1000); // the verbose buckets refill
            S.Set(SettingsSchema.VerboseLogging, true, SettingSources.File);
            Hit(damage: 100f);
            S.Set(SettingsSchema.DamageRandomEnabled, true, SettingSources.File);
            Hit(damage: 100f);
            S.Set(SettingsSchema.VerboseLogging, false, SettingSources.File);
            LogHas("[damage] not rolled - switched off (DamageRandomEnabled): melee on a person: (nobody) → (nobody), unarmed, 100, reduced → 75");
            LogHas("[damage] melee on a person: (nobody) → (nobody), unarmed, 100 → ");
            LogHas("), reduced → ");

            DamageRandomizer.WriteSummary();
            LogHas("[summary] damage reduction (DamageReductionPercent 25-90% (changed mid-battle), after the roll): ");
            LogHas(" from the roll's, ");
            LogHas("; not reduced: objects (doors, siege engines, ships) 1, fall damage 1, 0-damage hits 1, reduction 0 (DamageReductionPercent) 2, mod OFF (ModEnabled) 22");

            // 6. A mission with the reduction at 0 says so.
            S.Set(SettingsSchema.DamageReductionPercent, 0, SettingSources.File);
            DamageRandomizer.OnMissionStart();
            LogHas("overall damage reduction (DamageReductionPercent) 0% - off, the game's damage");

            DamageRandomizer.OnMissionStart();
            ConfigStore.Reload("after the damage reduction smoke");
        }
    }
}

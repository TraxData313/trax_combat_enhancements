using System;
using System.Globalization;
using System.Text;
using System.Threading;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Models
{
    /// <summary>
    /// Feature 1 (DESIGN §1), the game-facing half: turns one hit's
    /// <see cref="AttackInformation"/> / <see cref="AttackCollisionData"/> into Core's
    /// <see cref="HitFacts"/>, asks <see cref="DamageRoll.Decide"/> whether to roll, rolls - the
    /// upside following the attacker's Athletics (DESIGN §2, step 5c: his f from
    /// <see cref="Missions.AthleticsLogic.TryGetPeakShare"/>) - then (step 25) asks
    /// <see cref="DamageReduction.Decide"/> whether to reduce the result, rolled or not, and reduces
    /// it: the game's models → the roll → the reduction → the game rounds. Counts everything in
    /// <see cref="Stats"/> for the mission's <c>[summary]</c>, and logs.
    /// Called by <see cref="TraxDamageModel.ApplyGeneralDamageModifiers"/> with the value every
    /// model below us produced (armor included) - the last step before the game rounds it.
    ///
    /// Hot swap: the settings are read from <see cref="TraxSettings.Shared"/> on EVERY hit, so an
    /// MCM change mid-battle applies to the next one. Nothing is cached per mission.
    ///
    /// Thread: the engine's hit callbacks are <c>[MBCallback(null, false)]</c> (not multi-thread
    /// callable) - the main thread. The dice are per-thread anyway, the stats locked, and every
    /// roll checks its thread against the one <see cref="OnMissionStart"/> ran on; the summary
    /// says "ran on the main thread: all N" (or calls out the exceptions).
    ///
    /// Cost per hit with VerboseLogging off: a few array reads, one draw, one short uncontended
    /// lock - no allocation. Strings are built only for the verbose line and the first roll of a
    /// mission.
    /// </summary>
    internal static class DamageRandomizer
    {
        /// <summary>This mission's numbers - reset by <see cref="OnMissionStart"/>, written by
        /// <see cref="WriteSummary"/> (both from AthleticsLogic).</summary>
        internal static readonly DamageStats Stats = new DamageStats();

        /// <summary>The dice. The offline smoke swaps in a seeded (or a broken) source.</summary>
        internal static IRandomSource Rng = ThreadSafeRandom.Shared;

        private static int _mainThreadId;
        private static int _firstRollLogged;
        private static int _firstReductionLogged;
        private static int _firstTiredLogged;

        /// <summary>Mission start (AthleticsLogic.AfterStart, main thread): zero the stats, note
        /// the main thread, log the damage settings in effect.</summary>
        public static void OnMissionStart()
        {
            Stats.Reset();
            _mainThreadId = Environment.CurrentManagedThreadId;
            Interlocked.Exchange(ref _firstRollLogged, 0);
            Interlocked.Exchange(ref _firstReductionLogged, 0);
            Interlocked.Exchange(ref _firstTiredLogged, 0);
            var r = DamageRules.From(TraxSettings.Shared);
            TraxLog.Info("damage", "mission start: "
                + (r.ModEnabled ? string.Empty : "mod OFF (ModEnabled) - no hit is rolled, the game's own numbers are recorded for comparison; with the mod on: ")
                + "damage randomness " + (r.Enabled ? "ON" : "OFF")
                + ", spread ±" + r.Percent.ToString(CultureInfo.InvariantCulture) + "%"
                + " (a 50-damage hit lands for " + Range(50f, r.Percent) + ")"
                + ", melee " + OnOff(r.Melee) + ", ranged " + OnOff(r.Ranged)
                + ", on mounts " + OnOff(r.OnMounts) + ", on shields " + OnOff(r.OnShields)
                + ", upside follows the attacker's Athletics (DamageBonusFollowsAthletics) " + OnOff(r.UpsideFollowsAthletics)
                + " - read live on every hit; overall damage reduction (DamageReductionPercent) "
                + (DamageReduction.Clamp(r.ReductionPercent) <= 0 ? "0% - off, the game's damage"
                    : DamageReduction.Clamp(r.ReductionPercent).ToString(CultureInfo.InvariantCulture) + "% after the roll, rolled or not ("
                      + DamageReduction.Example(r.ReductionPercent) + "; falls and objects never)")
                + "; tired men hit softer (TiredDamageAtEmptyPercent) " + TiredText(in r)
                + (r.ModEnabled ? string.Empty : " - not while the mod is OFF"));
        }

        /// <summary>Step 89's clause of the mission-start line.</summary>
        private static string TiredText(in DamageRules r)
        {
            var c = CultureInfo.InvariantCulture;
            if (r.TiredAtEmptyPercent >= 100) return "100% - off, tiredness only takes the roll's upside";
            return r.TiredAtEmptyPercent.ToString(c) + "% at an empty bar - the attacker's damage x E + (1 - E) x f after the roll, before the reduction (f 0.5 x"
                + TiredDamage.Multiplier(r.TiredAtEmptyPercent, 0.5).ToString("0.00", c) + ", empty x" + TiredDamage.Multiplier(r.TiredAtEmptyPercent, 0).ToString("0.00", c)
                + "; " + TiredDamage.Example(r.TiredAtEmptyPercent, r.Percent) + "; a charging man's melee blow counts fresh"
                + (r.SprintFullCritical ? string.Empty : " - not while SprintFullCritical is off") + "; falls, objects and shield blocks never)"
                + (r.AthleticsEnabled ? string.Empty : " - not while Athletics is off");
        }

        /// <summary>The per-hit work. Returns the damage to hand back to the game. Throws only on
        /// a bug - the caller catches, logs and returns the game's value.</summary>
        public static float Apply(in AttackInformation ai, in AttackCollisionData cd, float damage)
        {
            var facts = new HitFacts(
                victimIsAgent: cd.IsColliderAgent,
                victimMissing: ai.IsVictimAgentNull,
                fall: cd.IsFallDamage,
                shieldBlocked: cd.AttackBlockedWithShield,
                missile: cd.IsMissile,
                victimIsMount: ai.IsVictimAgentMount,
                horseCharge: cd.IsHorseCharge);
            var rules = DamageRules.From(TraxSettings.Shared); // live: the next hit sees an MCM change
            var reason = DamageRoll.Decide(in facts, damage, in rules);

            if (reason != DamageSkipReason.None)
            {
                // Master switch off: vanilla damage, recorded as a factor-1 hit for the ON/OFF
                // comparison (the summary's "while the mod was OFF" line).
                if (reason == DamageSkipReason.ModOff) Stats.AddVanilla(facts.Category, damage);
                else Stats.AddSkip(reason);
                // Step 89: not rolled, but a tired attacker still hits softer (an Athletics effect - its own rules); the f and the charge read only
                // when the multiplier could apply.
                bool wants = TiredMayApply(in facts, in rules);
                double f = wants ? AttackerPeakShare(in ai) : double.NaN;
                bool chargingSkip = wants && facts.Category == DamageCategory.Melee && rules.SprintFullCritical && AttackerCharging(in ai);
                float tiredSkip = Tire(in facts, in rules, damage, f, chargingSkip, out double mSkip, out bool exemptSkip);
                // Step 25: not rolled, but still reduced (the reduction has its own rules).
                float landed = Reduce(in ai, in cd, in facts, in rules, damage, tiredSkip);
                NoteTired(in ai, in cd, in facts, in rules, damage, tiredSkip, landed, f, mSkip, exemptSkip);
                if (TraxLog.VerboseWants("damage-skip")) LogSkip(in ai, in cd, in facts, reason, damage, landed);
                return landed;
            }

            double attackerF = AttackerPeakShare(in ai);
            // step 62, THE UNCAPPED CRITICAL: a charging attacker's melee blow rolls the full upside whatever his tiredness
            bool melee = facts.Category == DamageCategory.Melee;
            bool charging = melee && rules.SprintFullCritical && AttackerCharging(in ai);
            var roll = DamageRoll.Roll(damage, rules.Percent, Rng, DamageRoll.ChargeUpside(in rules, attackerF, charging, facts.Missile));
            int thread = Environment.CurrentManagedThreadId;
            bool offMain = _mainThreadId != 0 && thread != _mainThreadId;
            Stats.AddRoll(facts.Category, in roll, offMain, attackerF);
            if (melee) Stats.AddMeleeCharge(in roll, charging, DamageRoll.Upside(in rules, attackerF));
            float tired = Tire(in facts, in rules, roll.After, attackerF, charging, out double m, out bool exempt); // step 89: AFTER the roll
            float final = Reduce(in ai, in cd, in facts, in rules, damage, tired); // step 25: AFTER the roll (and step 89's multiplier)
            NoteTired(in ai, in cd, in facts, in rules, roll.After, tired, final, attackerF, m, exempt);

            bool first = Volatile.Read(ref _firstRollLogged) == 0 && Interlocked.CompareExchange(ref _firstRollLogged, 1, 0) == 0;
            if (first || TraxLog.VerboseWants("damage")) LogRoll(in ai, in cd, in facts, in roll, first, thread, attackerF, final, m);
            return final;
        }

        /// <summary>Step 89: could the tired-damage multiplier apply to this hit at all (before the attacker's f is read)?</summary>
        private static bool TiredMayApply(in HitFacts facts, in DamageRules rules) =>
            rules.ModEnabled && rules.AthleticsEnabled && rules.TiredAtEmptyPercent < 100
            && facts.VictimIsAgent && !facts.VictimMissing && !facts.Fall && !facts.ShieldBlocked;

        /// <summary>
        /// Step 89, TIRED MEN HIT SOFTER: <paramref name="value"/> (after the roll, or the game's value when it was skipped) x m = E + (1 - E) x f by
        /// <see cref="TiredDamage"/>'s rules - a skip counted by reason; a CHARGING attacker's melee blow exempt (m 1, <paramref name="exempt"/>).
        /// <paramref name="m"/> is NaN when the multiplier did not apply. Allocation-free.
        /// </summary>
        private static float Tire(in HitFacts facts, in DamageRules rules, float value, double attackerF, bool charging, out double m, out bool exempt)
        {
            m = double.NaN;
            exempt = false;
            var why = TiredDamage.Decide(in facts, value, in rules, attackerF);
            if (why != TiredSkipReason.None)
            {
                Stats.AddTiredSkip(why);
                return value;
            }
            if (TiredDamage.Exempt(in rules, charging, facts.Missile))
            {
                exempt = true;
                return value;
            }
            m = TiredDamage.Multiplier(rules.TiredAtEmptyPercent, attackerF);
            return TiredDamage.Apply(value, m);
        }

        /// <summary>Step 89: the hit into the stats by the attacker's f band (or the charges exempted); the first softer hit of a mission in full.</summary>
        private static void NoteTired(in AttackInformation ai, in AttackCollisionData cd, in HitFacts facts, in DamageRules rules, float before, float tired,
            float landed, double attackerF, double m, bool exempt)
        {
            if (exempt)
            {
                Stats.AddTiredCharge(landed);
                return;
            }
            if (double.IsNaN(m)) return;
            Stats.AddTired(attackerF, m, landed, rules.TiredAtEmptyPercent);
            if (m < 1 && Volatile.Read(ref _firstTiredLogged) == 0 && Interlocked.CompareExchange(ref _firstTiredLogged, 1, 0) == 0)
            {
                try
                {
                    var c = CultureInfo.InvariantCulture;
                    TraxLog.Info("damage", "first tired hit this mission (step 89 - TiredDamageAtEmptyPercent " + rules.TiredAtEmptyPercent.ToString(c) + ") - "
                        + Describe(in ai, in cd, in facts) + ": attacker f " + attackerF.ToString("0.00", c) + " → x" + m.ToString("0.00", c)
                        + ": after the roll " + DamageRoll.GameRound(before).ToString(c) + " → tired " + DamageRoll.GameRound(tired).ToString(c)
                        + " → lands " + DamageRoll.GameRound(landed).ToString(c) + (landed == tired ? string.Empty : " (after the reduction)"));
                }
                catch (Exception e)
                {
                    Failed("damage.log", e);
                }
            }
        }

        /// <summary>
        /// Step 25, the overall damage reduction of one hit: <paramref name="afterRoll"/> (the roll's
        /// result, or the game's value when the roll was skipped) x (1 - DamageReductionPercent / 100),
        /// by <see cref="DamageReduction.Decide"/>'s rules (objects, falls, 0 hits, the setting at 0 and
        /// the master switch leave it alone - counted by reason). The first reduced hit of a mission is
        /// logged in full. Throws only on a bug - the caller's catch returns the game's value.
        /// </summary>
        private static float Reduce(in AttackInformation ai, in AttackCollisionData cd, in HitFacts facts, in DamageRules rules, float game, float afterRoll)
        {
            var why = DamageReduction.Decide(in facts, afterRoll, in rules);
            if (why != ReductionSkipReason.None)
            {
                Stats.AddReductionSkip(why);
                return afterRoll;
            }
            var r = DamageReduction.Reduce(game, afterRoll, rules.ReductionPercent);
            Stats.AddReduction(facts.Category, in r);
            if (Volatile.Read(ref _firstReductionLogged) == 0 && Interlocked.CompareExchange(ref _firstReductionLogged, 1, 0) == 0)
                LogFirstReduction(in ai, in cd, in facts, in r);
            return r.After;
        }

        /// <summary>
        /// The attacker's f (DESIGN §2: the share of his peak line left) for the damage upside - the
        /// RIDER's for a horse charge (the engine's attacker is then the horse). NaN when there is no
        /// tracked attacker (none, a riderless horse, an agent Athletics does not follow): the roll
        /// keeps its full upside.
        /// </summary>
        private static double AttackerPeakShare(in AttackInformation ai)
        {
            if (ai.IsAttackerAgentNull) return double.NaN;
            var attacker = ai.AttackerAgent;
            if (attacker != null && ai.IsAttackerAgentMount) attacker = attacker.RiderAgent;
            return attacker != null && Missions.AthleticsLogic.TryGetPeakShare(attacker, out double f) ? f : double.NaN;
        }

        /// <summary>Step 62: the attacker (a man on foot - never a horse's charge) CHARGES now (the impact model's charge: sprinting, or his
        /// first blow within ImpactChargeCarrySeconds after a sprint) - read, never spent here (the impact decision spends it).</summary>
        private static bool AttackerCharging(in AttackInformation ai)
        {
            if (ai.IsAttackerAgentNull || ai.IsAttackerAgentMount) return false;
            var attacker = ai.AttackerAgent;
            return attacker != null && Missions.AthleticsLogic.TryGetCharging(attacker);
        }

        /// <summary>A caught exception in the damage path: the FIRST per site per mission goes to
        /// the log with its stack (TraxLog.Error, itself rate-limited), the rest are counted and
        /// reported in the summary. Never throws.</summary>
        public static void Failed(string site, Exception e)
        {
            try
            {
                if (Stats.AddError(site)) TraxLog.Error(site, e);
            }
            catch
            {
                // the fallback must not fail
            }
        }

        /// <summary>The [summary] damage lines (AthleticsLogic.WriteSummary, marked spot).</summary>
        public static void WriteSummary()
        {
            foreach (var line in Stats.SummaryLines())
                TraxLog.Info("summary", line);
        }

        // ------------------------------------------------------------------ log lines

        private static void LogRoll(in AttackInformation ai, in AttackCollisionData cd, in HitFacts facts, in RollOutcome roll, bool first, int thread,
            double attackerF, float final, double tiredM)
        {
            try
            {
                string text = Describe(in ai, in cd, in facts) + ", " + roll.BeforeRounded.ToString(CultureInfo.InvariantCulture)
                    + " → " + roll.AfterRounded.ToString(CultureInfo.InvariantCulture)
                    + " (x" + roll.Factor.ToString("0.00", CultureInfo.InvariantCulture)
                    + (double.IsNaN(attackerF) ? ", attacker without a pool: full upside"
                        : ", attacker f " + attackerF.ToString("0.00", CultureInfo.InvariantCulture) + " → up to x"
                          + roll.Ceiling.ToString("0.00", CultureInfo.InvariantCulture)) + ")"
                    + (double.IsNaN(tiredM) || tiredM >= 1 ? string.Empty : ", tired x" + tiredM.ToString("0.00", CultureInfo.InvariantCulture))
                    + ReducedText(roll.After, final);
                if (first)
                {
                    TraxLog.Info("damage", "first roll this mission - "
                        + (_mainThreadId == 0 ? "thread " + thread + " (mission start not seen)"
                            : thread == _mainThreadId ? "on the main thread"
                            : "on thread " + thread + ", NOT the main thread (" + _mainThreadId + ")")
                        + ": " + text);
                }
                else
                {
                    TraxLog.Verbose("damage", text);
                }
            }
            catch (Exception e)
            {
                Failed("damage.log", e);
            }
        }

        private static void LogSkip(in AttackInformation ai, in AttackCollisionData cd, in HitFacts facts, DamageSkipReason reason, float damage, float landed)
        {
            try
            {
                TraxLog.Verbose("damage", "not rolled - " + DamageStats.SkipName(reason) + ": " + Describe(in ai, in cd, in facts)
                    + ", " + DamageRoll.GameRound(damage).ToString(CultureInfo.InvariantCulture) + ReducedText(damage, landed), "damage-skip");
            }
            catch (Exception e)
            {
                Failed("damage.log", e);
            }
        }

        /// <summary>Step 25: the first reduced hit of a mission, always logged (the proof in game, even
        /// with the roll switched off).</summary>
        private static void LogFirstReduction(in AttackInformation ai, in AttackCollisionData cd, in HitFacts facts, in ReductionOutcome r)
        {
            try
            {
                TraxLog.Info("damage", "first reduced hit this mission - " + Describe(in ai, in cd, in facts) + ": the game's "
                    + r.GameRounded.ToString(CultureInfo.InvariantCulture) + " → after the roll " + r.BeforeRounded.ToString(CultureInfo.InvariantCulture)
                    + " → reduced " + r.Percent.ToString(CultureInfo.InvariantCulture) + "% (DamageReductionPercent) → lands "
                    + r.AfterRounded.ToString(CultureInfo.InvariantCulture));
            }
            catch (Exception e)
            {
                Failed("damage.log", e);
            }
        }

        /// <summary>", reduced → 38" when the reduction changed the value, else nothing.</summary>
        private static string ReducedText(float before, float after)
            => after == before ? string.Empty : ", reduced → " + DamageRoll.GameRound(after).ToString(CultureInfo.InvariantCulture);

        /// <summary><c>melee on a person: Vlandian Sergeant → Looter, Falchion</c></summary>
        private static string Describe(in AttackInformation ai, in AttackCollisionData cd, in HitFacts facts)
        {
            var sb = new StringBuilder(96);
            sb.Append(facts.Fall ? "fall" : facts.HorseCharge ? "horse charge" : facts.Missile ? "ranged" : "melee");
            sb.Append(" on ").Append(!facts.VictimIsAgent ? "an object"
                : facts.ShieldBlocked ? "a shield"
                : facts.VictimIsMount ? "a mount"
                : "a person");
            sb.Append(": ").Append(Name(ai.AttackerAgent)).Append(" → ").Append(facts.VictimIsAgent ? Name(ai.VictimAgent) : "(object)");
            sb.Append(", ").Append(Weapon(in ai, in cd, in facts));
            if (ai.IsFriendlyFire) sb.Append(", friendly fire");
            return sb.ToString();
        }

        private static string Name(Agent? a)
        {
            if (a == null) return "(nobody)";
            try
            {
                if (a.IsMount)
                {
                    var rider = a.RiderAgent;
                    return "horse " + a.Name + (rider != null ? " of " + Name(rider) : " (riderless)");
                }
                return a.IsMainAgent ? a.Name + " (you)" : a.Name;
            }
            catch
            {
                return "(agent " + a.Index + ")";
            }
        }

        private static string Weapon(in AttackInformation ai, in AttackCollisionData cd, in HitFacts facts)
        {
            if (facts.HorseCharge) return "charge bump";
            if (facts.Fall) return "no weapon";
            var w = ai.AttackerWeapon;
            if (w.IsEmpty) return cd.IsAlternativeAttack ? "kick" : "unarmed";
            string name = w.Item?.Name?.ToString() ?? "(unnamed weapon)";
            return cd.IsAlternativeAttack ? name + " (bash)" : name;
        }

        private static string OnOff(bool on) => on ? "on" : "off";

        private static string Range(float damage, int percent)
        {
            double p = DamageRoll.Spread(percent);
            return DamageRoll.GameRound(DamageRoll.Apply(damage, DamageRoll.Factor(0, p))).ToString(CultureInfo.InvariantCulture)
                + "-" + DamageRoll.GameRound(DamageRoll.Apply(damage, DamageRoll.Factor(1, p))).ToString(CultureInfo.InvariantCulture);
        }
    }
}

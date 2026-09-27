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
    /// <see cref="Missions.AthleticsLogic.TryGetPeakShare"/>) - counts everything in
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

        /// <summary>Mission start (AthleticsLogic.AfterStart, main thread): zero the stats, note
        /// the main thread, log the damage settings in effect.</summary>
        public static void OnMissionStart()
        {
            Stats.Reset();
            _mainThreadId = Environment.CurrentManagedThreadId;
            Interlocked.Exchange(ref _firstRollLogged, 0);
            var r = DamageRules.From(TraxSettings.Shared);
            TraxLog.Info("damage", "mission start: "
                + (r.ModEnabled ? string.Empty : "mod OFF (ModEnabled) - no hit is rolled, the game's own numbers are recorded for comparison; with the mod on: ")
                + "damage randomness " + (r.Enabled ? "ON" : "OFF")
                + ", spread ±" + r.Percent.ToString(CultureInfo.InvariantCulture) + "%"
                + " (a 50-damage hit lands for " + Range(50f, r.Percent) + ")"
                + ", melee " + OnOff(r.Melee) + ", ranged " + OnOff(r.Ranged)
                + ", on mounts " + OnOff(r.OnMounts) + ", on shields " + OnOff(r.OnShields)
                + ", upside follows the attacker's Athletics (DamageBonusFollowsAthletics) " + OnOff(r.UpsideFollowsAthletics)
                + " - read live on every hit");
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
                if (TraxLog.VerboseOn) LogSkip(in ai, in cd, in facts, reason, damage);
                return damage;
            }

            double attackerF = AttackerPeakShare(in ai);
            var roll = DamageRoll.Roll(damage, rules.Percent, Rng, DamageRoll.Upside(in rules, attackerF));
            int thread = Environment.CurrentManagedThreadId;
            bool offMain = _mainThreadId != 0 && thread != _mainThreadId;
            Stats.AddRoll(facts.Category, in roll, offMain, attackerF);

            bool first = Volatile.Read(ref _firstRollLogged) == 0 && Interlocked.CompareExchange(ref _firstRollLogged, 1, 0) == 0;
            if (first || TraxLog.VerboseOn) LogRoll(in ai, in cd, in facts, in roll, first, thread, attackerF);
            return roll.After;
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
            double attackerF)
        {
            try
            {
                string text = Describe(in ai, in cd, in facts) + ", " + roll.BeforeRounded.ToString(CultureInfo.InvariantCulture)
                    + " → " + roll.AfterRounded.ToString(CultureInfo.InvariantCulture)
                    + " (x" + roll.Factor.ToString("0.00", CultureInfo.InvariantCulture)
                    + (double.IsNaN(attackerF) ? ", attacker without a pool: full upside"
                        : ", attacker f " + attackerF.ToString("0.00", CultureInfo.InvariantCulture) + " → up to x"
                          + roll.Ceiling.ToString("0.00", CultureInfo.InvariantCulture)) + ")";
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

        private static void LogSkip(in AttackInformation ai, in AttackCollisionData cd, in HitFacts facts, DamageSkipReason reason, float damage)
        {
            try
            {
                TraxLog.Verbose("damage", "not rolled - " + DamageStats.SkipName(reason) + ": " + Describe(in ai, in cd, in facts)
                    + ", " + DamageRoll.GameRound(damage).ToString(CultureInfo.InvariantCulture), "damage-skip");
            }
            catch (Exception e)
            {
                Failed("damage.log", e);
            }
        }

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

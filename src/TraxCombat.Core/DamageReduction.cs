namespace TraxCombat.Core
{
    /// <summary>Why a hit was NOT reduced by the overall damage reduction (step 25; the summary
    /// counts each). <see cref="None"/> = reduce it. Not the roll's <see cref="DamageSkipReason"/>:
    /// the reduction has its own, shorter list - it does NOT follow the roll's switches (a hit the
    /// roll skips because DamageRandomEnabled / a toggle is off is still reduced).</summary>
    public enum ReductionSkipReason
    {
        None = 0,

        /// <summary>A door, a siege engine, a ship - not a person or a horse: gates and rams keep
        /// the game's pace (a siege must not drag on because the mod slows the men's fights).</summary>
        NotAnAgent,

        /// <summary>An agent collision without a victim agent (defensive; not expected).</summary>
        NoVictim,

        /// <summary>Falling is not a blow - a jump from a wall hurts as it always did.</summary>
        FallDamage,

        /// <summary>The value rounds to 0 - a 0 hit stays 0 (DESIGN §1).</summary>
        ZeroDamage,

        /// <summary>DamageReductionPercent is 0 - the game's damage.</summary>
        PercentZero,

        /// <summary>The hit WOULD be reduced, but the master switch ModEnabled is off: the game's
        /// value, untouched. Checked LAST (like <see cref="DamageSkipReason.ModOff"/>), so an OFF
        /// battle counts exactly the hits an ON battle would reduce.</summary>
        ModOff,
    }

    /// <summary>One reduced hit: the game's value (before the roll), the value the reduction
    /// started from (after the roll - the same when the roll was skipped), and what lands.</summary>
    public readonly struct ReductionOutcome
    {
        public ReductionOutcome(float game, float before, float after, int percent)
        {
            Game = game;
            Before = before;
            After = after;
            Percent = percent;
        }

        /// <summary>The damage the game (and every model below us) computed - before the roll.</summary>
        public float Game { get; }

        /// <summary>The damage after the roll (= <see cref="Game"/> when the roll was skipped).</summary>
        public float Before { get; }

        /// <summary>What we hand back (the game then rounds it).</summary>
        public float After { get; }

        /// <summary>The reduction applied, in % (clamped to 0..<see cref="DamageReduction.MaxPercent"/>).</summary>
        public int Percent { get; }

        public int GameRounded => DamageRoll.GameRound(Game);

        public int BeforeRounded => DamageRoll.GameRound(Before);

        public int AfterRounded => DamageRoll.GameRound(After);
    }

    /// <summary>
    /// Step 25 (Anton, 2026-10-10), the pure part: an overall damage reduction on every hit that
    /// lands on a person or a horse - shield blocks included - AFTER the ±% roll (DESIGN §1
    /// "Overall damage reduction"). The order per hit is fixed: the game's models (armor
    /// included) → the roll → the reduction → the game rounds. It follows the master switch only,
    /// not the roll's switches.
    /// </summary>
    public static class DamageReduction
    {
        /// <summary>The setting's top (SettingsSchema's range): nothing is ever made unkillable.</summary>
        public const int MaxPercent = 90;

        /// <summary>The percent in force, clamped to 0..<see cref="MaxPercent"/> (a hand edit past the
        /// range is normalized on read anyway - this is the second fence).</summary>
        public static int Clamp(int percent) => percent <= 0 ? 0 : percent >= MaxPercent ? MaxPercent : percent;

        /// <summary>The multiplier: 1 − percent / 100 (25 → 0.75; 0 → 1).</summary>
        public static double Multiplier(int percent) => 1 - Clamp(percent) / 100.0;

        /// <summary>
        /// The skip rules, first match wins: the structural "never" rules (objects, no victim, fall,
        /// a 0 hit), then the setting at 0, LAST the master switch (counted, like the roll's ModOff).
        /// <paramref name="damage"/> is the value the reduction would start from (after the roll).
        /// </summary>
        public static ReductionSkipReason Decide(in HitFacts hit, float damage, in DamageRules rules)
        {
            if (!hit.VictimIsAgent) return ReductionSkipReason.NotAnAgent;
            if (hit.VictimMissing) return ReductionSkipReason.NoVictim;
            if (hit.Fall) return ReductionSkipReason.FallDamage;
            if (DamageRoll.GameRound(damage) <= 0) return ReductionSkipReason.ZeroDamage;
            if (Clamp(rules.ReductionPercent) <= 0) return ReductionSkipReason.PercentZero;
            if (!rules.ModEnabled) return ReductionSkipReason.ModOff;
            return ReductionSkipReason.None;
        }

        /// <summary>
        /// damage × (1 − percent / 100) with DESIGN §1's rounding rule: a value the game would show
        /// as 0 stays exactly as it was, a positive one never lands below 1 (the game rounds our
        /// value, so we return at least 1). Percent 0 = unchanged.
        /// </summary>
        public static float Apply(float damage, int percent)
        {
            if (DamageRoll.GameRound(damage) <= 0) return damage;
            int p = Clamp(percent);
            if (p <= 0) return damage;
            float reduced = (float)(damage * (1 - p / 100.0));
            return reduced < 1f ? 1f : reduced;
        }

        /// <summary>The reduction of one hit the caller has already said "reduce" for (<see cref="Decide"/>).</summary>
        public static ReductionOutcome Reduce(float game, float afterRoll, int percent)
            => new ReductionOutcome(game, afterRoll, Apply(afterRoll, percent), Clamp(percent));

        /// <summary>"an average 100 hit lands 75" - the plain-words example for the log.</summary>
        public static string Example(int percent)
            => "an average 100 hit lands " + DamageRoll.GameRound(Apply(100f, percent)).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}

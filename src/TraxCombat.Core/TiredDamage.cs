namespace TraxCombat.Core
{
    /// <summary>Why a hit was NOT multiplied by step 89's tired-damage factor (the summary counts each). <see cref="None"/> = apply it.</summary>
    public enum TiredSkipReason
    {
        None = 0,

        /// <summary>A door, a siege engine, a ship - like the roll, never.</summary>
        NotAnAgent,

        /// <summary>An agent collision without a victim agent (defensive).</summary>
        NoVictim,

        /// <summary>A fall is not a blow.</summary>
        FallDamage,

        /// <summary>A blow a shield stopped - the multiplier is for blows that land on people and horses (DESIGN decision 82).</summary>
        ShieldBlock,

        /// <summary>The value rounds to 0 - a 0 hit stays 0.</summary>
        ZeroDamage,

        /// <summary>The master switch ModEnabled is off (checked first of the switches).</summary>
        ModOff,

        /// <summary>AthleticsEnabled is off - the multiplier is an Athletics effect.</summary>
        AthleticsOff,

        /// <summary>TiredDamageAtEmptyPercent is 100 - off, the roll's upside alone (as before step 89).</summary>
        SettingOff,

        /// <summary>No tracked attacker (none, a riderless horse, an agent Athletics does not follow) - no f, no multiplier.</summary>
        NoPool,
    }

    /// <summary>
    /// Step 89 - TIRED MEN HIT SOFTER (Toni 2026-10-24: "as the units get more tired their damage output to decrease further"; DESIGN §2,
    /// decision 82). On top of the roll (whose upside already shrinks with f), the attacker's damage is multiplied by
    /// m = E + (1 − E) × f, E = <c>TiredDamageAtEmptyPercent</c> / 100, f = his share of the peak line left (the rider's for a horse charge -
    /// the same f the roll's upside reads). Order per hit: the game's models → the roll → × m → the reduction (step 25) → the game rounds.
    /// A CHARGING attacker's melee blow (step 62's SprintFullCritical path) counts as fresh: m = 1. It follows the master switch and
    /// AthleticsEnabled, NOT the roll's switches (a hit the roll skips is still multiplied). Pure, allocation-free.
    /// </summary>
    public static class TiredDamage
    {
        /// <summary>E = percent / 100, clamped to 0..1 (100 = off).</summary>
        public static double AtEmpty(int percent) => percent <= 0 ? 0 : percent >= 100 ? 1 : percent / 100.0;

        /// <summary>m = E + (1 − E) × f; f clamped to 0..1 (NaN = untracked = 1). 60: f 1 → 1, f 0.5 → 0.8, f 0 → 0.6.</summary>
        public static double Multiplier(int atEmptyPercent, double peakShare)
        {
            double e = AtEmpty(atEmptyPercent);
            double f = double.IsNaN(peakShare) || peakShare >= 1 ? 1 : peakShare <= 0 ? 0 : peakShare;
            return e + (1 - e) * f;
        }

        /// <summary>
        /// The skip rules, first match wins: the structural "never" (objects, no victim, falls, shield blocks, a 0 hit), then the switches -
        /// the master switch FIRST, Athletics, the setting at 100 -, last an attacker without a pool (<paramref name="attackerPeakShare"/> NaN).
        /// The charge's exemption is the caller's (<see cref="Exempt"/>), after this says "apply".
        /// </summary>
        public static TiredSkipReason Decide(in HitFacts hit, float damage, in DamageRules rules, double attackerPeakShare)
        {
            if (!hit.VictimIsAgent) return TiredSkipReason.NotAnAgent;
            if (hit.VictimMissing) return TiredSkipReason.NoVictim;
            if (hit.Fall) return TiredSkipReason.FallDamage;
            if (hit.ShieldBlocked) return TiredSkipReason.ShieldBlock;
            if (DamageRoll.GameRound(damage) <= 0) return TiredSkipReason.ZeroDamage;
            if (!rules.ModEnabled) return TiredSkipReason.ModOff;
            if (!rules.AthleticsEnabled) return TiredSkipReason.AthleticsOff;
            if (rules.TiredAtEmptyPercent >= 100) return TiredSkipReason.SettingOff;
            if (double.IsNaN(attackerPeakShare)) return TiredSkipReason.NoPool;
            return TiredSkipReason.None;
        }

        /// <summary>Step 62's path: a CHARGING attacker's melee blow (SprintFullCritical on, not a missile) is not reduced - his blow counts as fresh.</summary>
        public static bool Exempt(in DamageRules rules, bool charging, bool missile) => rules.SprintFullCritical && charging && !missile;

        /// <summary>damage × m with DESIGN §1's rounding rule: a value the game shows as 0 stays as it was, a positive one never below 1.</summary>
        public static float Apply(float damage, double multiplier)
        {
            if (DamageRoll.GameRound(damage) <= 0) return damage;
            if (multiplier >= 1) return damage;
            float v = (float)(damage * (multiplier <= 0 ? 0 : multiplier));
            return v < 1f ? 1f : v;
        }

        /// <summary>"an empty man's average 100 hit lands 45" - the plain-words example for the log (the roll's average at f = 0, then × m).</summary>
        public static string Example(int atEmptyPercent, int spreadPercent)
        {
            double p = DamageRoll.Spread(spreadPercent);
            double avgRoll = 1 - p / 2; // [1 − p, 1) at f 0
            return "an empty man's average 100 hit lands "
                + DamageRoll.GameRound((float)(100 * avgRoll * Multiplier(atEmptyPercent, 0))).ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " before the reduction (" + DamageRoll.GameRound((float)(100 * avgRoll)).ToString(System.Globalization.CultureInfo.InvariantCulture) + " without it)";
        }
    }
}

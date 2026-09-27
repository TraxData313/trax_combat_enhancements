using System;

namespace TraxCombat.Core
{
    /// <summary>What a rolled hit landed on / came from - the summary counts each. Exactly one
    /// per hit: a shield block wins, then a mount victim, then a missile, else melee (horse-charge
    /// bumps are melee, DESIGN §1).</summary>
    public enum DamageCategory
    {
        Melee = 0,
        Ranged = 1,
        Mount = 2,
        Shield = 3,
    }

    /// <summary>Why a hit was NOT rolled (the summary counts each, so the playtest can see every
    /// skip rule fire). <see cref="None"/> = roll it.</summary>
    public enum DamageSkipReason
    {
        None = 0,

        /// <summary>A door, a siege engine, a ship - not a person or a horse (DESIGN §1: never).</summary>
        NotAnAgent,

        /// <summary>An agent collision without a victim agent (defensive; not expected).</summary>
        NoVictim,

        /// <summary>Falling is not a strike (DESIGN §1: never).</summary>
        FallDamage,

        /// <summary>The game's value rounds to 0 - a 0 hit stays 0 (DESIGN §1).</summary>
        ZeroDamage,

        /// <summary>DamageRandomEnabled is off.</summary>
        SwitchedOff,

        /// <summary>DamageRandomPercent is 0.</summary>
        SpreadZero,

        /// <summary>Blocked by a shield and DamageRandomOnShields is off (the default).</summary>
        ShieldToggleOff,

        /// <summary>Landed on a horse and DamageRandomOnMounts is off.</summary>
        MountToggleOff,

        /// <summary>A melee hit (or horse charge) and DamageRandomMelee is off.</summary>
        MeleeToggleOff,

        /// <summary>An arrow, bolt, stone or thrown weapon and DamageRandomRanged is off.</summary>
        RangedToggleOff,
    }

    /// <summary>
    /// The facts about one hit that the skip rules need - built in the module from the game's
    /// <c>AttackCollisionData</c> / <c>AttackInformation</c>, kept free of game types so the rules
    /// are unit-tested here.
    /// </summary>
    public readonly struct HitFacts
    {
        public HitFacts(bool victimIsAgent, bool victimMissing, bool fall, bool shieldBlocked, bool missile, bool victimIsMount, bool horseCharge)
        {
            VictimIsAgent = victimIsAgent;
            VictimMissing = victimMissing;
            Fall = fall;
            ShieldBlocked = shieldBlocked;
            Missile = missile;
            VictimIsMount = victimIsMount;
            HorseCharge = horseCharge;
        }

        /// <summary><c>collisionData.IsColliderAgent</c> - false for doors, siege engines, ships.</summary>
        public bool VictimIsAgent { get; }

        /// <summary><c>attackInformation.IsVictimAgentNull</c>.</summary>
        public bool VictimMissing { get; }

        /// <summary><c>collisionData.IsFallDamage</c>.</summary>
        public bool Fall { get; }

        /// <summary><c>collisionData.AttackBlockedWithShield</c> - the damage goes to the shield.</summary>
        public bool ShieldBlocked { get; }

        /// <summary><c>collisionData.IsMissile</c> - arrows, bolts, stones, thrown weapons, siege-engine shots.</summary>
        public bool Missile { get; }

        /// <summary><c>attackInformation.IsVictimAgentMount</c>.</summary>
        public bool VictimIsMount { get; }

        /// <summary><c>collisionData.IsHorseCharge</c> - a bump; counts as melee (DESIGN §1).</summary>
        public bool HorseCharge { get; }

        public DamageCategory Category =>
            ShieldBlocked ? DamageCategory.Shield
            : VictimIsMount ? DamageCategory.Mount
            : Missile ? DamageCategory.Ranged
            : DamageCategory.Melee;
    }

    /// <summary>The damage settings for ONE hit - read from <see cref="TraxSettings"/> at the
    /// moment of the hit (hot swap: an MCM change mid-battle applies to the next hit).</summary>
    public readonly struct DamageRules
    {
        public DamageRules(bool enabled, int percent, bool melee, bool ranged, bool onMounts, bool onShields)
        {
            Enabled = enabled;
            Percent = percent;
            Melee = melee;
            Ranged = ranged;
            OnMounts = onMounts;
            OnShields = onShields;
        }

        public bool Enabled { get; }

        /// <summary>± spread in percent (0..100).</summary>
        public int Percent { get; }

        public bool Melee { get; }

        public bool Ranged { get; }

        public bool OnMounts { get; }

        public bool OnShields { get; }

        /// <summary>The live values, read now.</summary>
        public static DamageRules From(TraxSettings s) => new DamageRules(
            s.DamageRandomEnabled, s.DamageRandomPercent, s.DamageRandomMelee, s.DamageRandomRanged,
            s.DamageRandomOnMounts, s.DamageRandomOnShields);
    }

    /// <summary>One roll: the game's value, ours, and the dice.</summary>
    public readonly struct RollOutcome
    {
        public RollOutcome(float before, float after, float factor, double position)
        {
            Before = before;
            After = after;
            Factor = factor;
            Position = position;
        }

        /// <summary>The damage the game (and every model below us) computed.</summary>
        public float Before { get; }

        /// <summary>What we hand back (the game then rounds it).</summary>
        public float After { get; }

        /// <summary>The factor drawn, in [1 − p, 1 + p).</summary>
        public float Factor { get; }

        /// <summary>Where in the range the draw fell, 0 = lowest possible, 1 = highest (the
        /// summary's fairness histogram).</summary>
        public double Position { get; }

        /// <summary>The number the player sees for the game's value.</summary>
        public int BeforeRounded => DamageRoll.GameRound(Before);

        /// <summary>The number the player sees - "Delivered N damage".</summary>
        public int AfterRounded => DamageRoll.GameRound(After);
    }

    /// <summary>
    /// DESIGN §1, the pure part: which hits roll, the factor, the rounding rule. The module's
    /// damage decorator feeds it the game's value AFTER every other model (armor included).
    /// </summary>
    public static class DamageRoll
    {
        /// <summary>
        /// The skip rules, first match wins. Structural "never" rules first (objects, no victim,
        /// fall, a 0 hit), then the switches in the order a player would look for them: master
        /// switch, spread, the TARGET toggles (shield, mount), then the ATTACK toggles (melee,
        /// ranged). The target and attack toggles combine: an arrow into a horse rolls only with
        /// both "ranged" and "on mounts" on.
        /// </summary>
        public static DamageSkipReason Decide(in HitFacts hit, float damage, in DamageRules rules)
        {
            if (!hit.VictimIsAgent) return DamageSkipReason.NotAnAgent;
            if (hit.VictimMissing) return DamageSkipReason.NoVictim;
            if (hit.Fall) return DamageSkipReason.FallDamage;
            if (GameRound(damage) <= 0) return DamageSkipReason.ZeroDamage;
            if (!rules.Enabled) return DamageSkipReason.SwitchedOff;
            if (rules.Percent <= 0) return DamageSkipReason.SpreadZero;
            if (hit.ShieldBlocked && !rules.OnShields) return DamageSkipReason.ShieldToggleOff;
            if (hit.VictimIsMount && !rules.OnMounts) return DamageSkipReason.MountToggleOff;
            if (hit.Missile)
            {
                if (!rules.Ranged) return DamageSkipReason.RangedToggleOff;
            }
            else if (!rules.Melee)
            {
                return DamageSkipReason.MeleeToggleOff;
            }
            return DamageSkipReason.None;
        }

        /// <summary>p = percent / 100, clamped to 0..1.</summary>
        public static double Spread(int percent) => percent <= 0 ? 0 : percent >= 100 ? 1 : percent / 100.0;

        /// <summary>The factor for a uniform draw <paramref name="position"/> in [0, 1): 1 − p + 2p·u,
        /// i.e. uniform over [1 − p, 1 + p).</summary>
        public static float Factor(double position, double spread) => (float)(1 - spread + 2 * spread * position);

        /// <summary>
        /// damage × factor, with DESIGN §1's rounding rule: a hit the game would show as 0 stays
        /// exactly as it was (0 stays 0), a positive hit never lands below 1 (the game rounds our
        /// value, so we return at least 1).
        /// </summary>
        public static float Apply(float damage, float factor)
        {
            if (GameRound(damage) <= 0) return damage;
            float rolled = damage * factor;
            return rolled < 1f ? 1f : rolled;
        }

        /// <summary>Draws the factor and applies it. The caller has already said "roll" via
        /// <see cref="Decide"/>.</summary>
        public static RollOutcome Roll(float damage, int percent, IRandomSource rng)
        {
            double u = rng.NextDouble();
            if (!(u >= 0)) u = 0;                        // NaN or negative from a broken source
            if (u >= 1) u = 0.9999999999;
            float factor = Factor(u, Spread(percent));
            return new RollOutcome(damage, Apply(damage, factor), factor, u);
        }

        /// <summary>The game's own rounding of the final damage (<c>TaleWorlds.Library.MathF.Round</c>
        /// = <c>(int)Math.Round</c>, banker's rounding: 0.5 → 0, 1.5 → 2, 2.5 → 2).</summary>
        public static int GameRound(float value) => (int)Math.Round(value);
    }
}

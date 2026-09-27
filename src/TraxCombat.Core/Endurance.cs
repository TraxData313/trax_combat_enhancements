using System;

namespace TraxCombat.Core
{
    /// <summary>What a charged blow was - the summary counts each kind (DESIGN §2 "What is a blow").</summary>
    public enum BlowKind
    {
        /// <summary>A melee swing or thrust, charged when it is RELEASED (CostOnMiss on).</summary>
        Melee = 0,

        /// <summary>A shot or a throw, charged when it leaves the hand (CostOnMiss on).</summary>
        Ranged = 1,

        /// <summary>A couched lance or braced spear - no swing, so one blow when it LANDS (both modes).</summary>
        Couched = 2,

        /// <summary>A melee swing charged at its first landed hit (CostOnMiss off).</summary>
        LandedMelee = 3,

        /// <summary>A shot or throw charged when it hits a person, horse or shield (CostOnMiss off).</summary>
        LandedRanged = 4,
    }

    /// <summary>
    /// The endurance settings in effect for ONE decision, read live from
    /// <see cref="TraxSettings"/> (<see cref="From"/>) - never cached across a tick, so an MCM change
    /// mid-battle applies to the next blow / regen step. Built fresh where needed (a struct: no
    /// allocation).
    /// </summary>
    public readonly struct EnduranceRules
    {
        public EnduranceRules(bool enabled, int maxEndurance, float costPerBlow, bool costOnMiss, float heroCostMultiplier,
            float partyLeaderCostMultiplier, int exhaustedAttackSpeedPercent, int exhaustedRecoverPercent, float regenDelayBlowTimes,
            float blowTimeSeconds, float fullRegenSecondsStanding, float fullRegenSecondsMoving, float movingSpeedThreshold)
        {
            Enabled = enabled;
            MaxEndurance = maxEndurance;
            CostPerBlow = costPerBlow;
            CostOnMiss = costOnMiss;
            HeroCostMultiplier = heroCostMultiplier;
            PartyLeaderCostMultiplier = partyLeaderCostMultiplier;
            ExhaustedAttackSpeedPercent = exhaustedAttackSpeedPercent;
            ExhaustedRecoverPercent = exhaustedRecoverPercent;
            RegenDelayBlowTimes = regenDelayBlowTimes;
            BlowTimeSeconds = blowTimeSeconds;
            FullRegenSecondsStanding = fullRegenSecondsStanding;
            FullRegenSecondsMoving = fullRegenSecondsMoving;
            MovingSpeedThreshold = movingSpeedThreshold;
        }

        public bool Enabled { get; }

        public int MaxEndurance { get; }

        public float CostPerBlow { get; }

        public bool CostOnMiss { get; }

        public float HeroCostMultiplier { get; }

        public float PartyLeaderCostMultiplier { get; }

        public int ExhaustedAttackSpeedPercent { get; }

        public int ExhaustedRecoverPercent { get; }

        public float RegenDelayBlowTimes { get; }

        public float BlowTimeSeconds { get; }

        public float FullRegenSecondsStanding { get; }

        public float FullRegenSecondsMoving { get; }

        public float MovingSpeedThreshold { get; }

        /// <summary>Seconds without a blow before regeneration starts: 2 × 1.5 = 3 s by default.</summary>
        public double RegenDelaySeconds => (double)RegenDelayBlowTimes * BlowTimeSeconds;

        /// <summary>Once exhausted, speed returns only ABOVE this share of the pool (0 = above empty).</summary>
        public double RecoverAboveFraction => ExhaustedRecoverPercent / 100.0;

        /// <summary>The live values, read now.</summary>
        public static EnduranceRules From(TraxSettings s) => new EnduranceRules(
            s.EnduranceEnabled, s.MaxEndurance, s.CostPerBlow, s.CostOnMiss, s.HeroCostMultiplier, s.PartyLeaderCostMultiplier,
            s.ExhaustedAttackSpeedPercent, s.ExhaustedRecoverPercent, s.RegenDelayBlowTimes, s.BlowTimeSeconds,
            s.FullRegenSecondsStanding, s.FullRegenSecondsMoving, s.MovingSpeedThreshold);
    }

    /// <summary>
    /// One fighter's endurance - pure state, no game types (the module's per-agent record derives
    /// from it). Endurance is stored as a FRACTION of the fighter's pool (1 = full), so changing
    /// the pool size mid-battle keeps everyone's share by construction (DESIGN §2); points =
    /// fraction × <see cref="EnduranceMath.PoolPoints"/>. Mutated only through
    /// <see cref="EnduranceMath"/> (charge, regen, reset) - main thread.
    /// </summary>
    public class Fighter
    {
        /// <summary>0..1 of the pool.</summary>
        public double Fraction { get; internal set; } = 1.0;

        /// <summary>Latched at 0; cleared by recovery above <c>ExhaustedRecoverPercent</c>.</summary>
        public bool Exhausted { get; internal set; }

        /// <summary>Mission time of the last blow (−∞ = none yet) - the regen delay counts from it.</summary>
        public double LastBlowTime { get; internal set; } = double.NegativeInfinity;

        /// <summary>A hero (lord, companion, the player) - cached at spawn, the multiplier is read live.</summary>
        public bool IsHero { get; set; }

        /// <summary>Leads the fighter's own party (custom battle: the side's general, or every hero
        /// of a side without one) - cached at spawn, the multiplier is read live.</summary>
        public bool IsLeader { get; set; }

        /// <summary>Lowest fraction reached this mission (for the summary).</summary>
        public double LowestFraction { get; internal set; } = 1.0;

        public int Blows { get; internal set; }

        public int ExhaustionsEntered { get; internal set; }

        /// <summary>Mission time the current exhaustion began.</summary>
        public double ExhaustedSince { get; internal set; }

        /// <summary>A refill run is under way (since the regen delay ran out); a blow ends it.</summary>
        public bool Regenerating { get; internal set; }

        public double EpisodeStartFraction { get; internal set; }

        public double EpisodeStartTime { get; internal set; }

        public double EpisodeStandingSeconds { get; internal set; }

        public double EpisodeMovingSeconds { get; internal set; }

        /// <summary>
        /// The attack-speed multiplier the stat decorator applies to this fighter RIGHT NOW (1 = none).
        /// Set by the module when it decides a new value (<see cref="EnduranceMath.SpeedUpdateNeeded"/>)
        /// just before it asks the game to recompute the agent's properties - so every recompute the
        /// game does on its own (weapon switch, mount) applies the same value.
        /// </summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>Everyone starts a mission full; EnduranceEnabled turned off puts everyone back to full.</summary>
        public void ResetFull()
        {
            Fraction = 1.0;
            Exhausted = false;
            LastBlowTime = double.NegativeInfinity;
            Regenerating = false;
        }
    }

    /// <summary>One charged blow.</summary>
    public readonly struct BlowOutcome
    {
        public BlowOutcome(bool charged, double cost, double multiplier, double pool, double before, double after, bool enteredExhaustion)
        {
            Charged = charged;
            Cost = cost;
            Multiplier = multiplier;
            Pool = pool;
            Before = before;
            After = after;
            EnteredExhaustion = enteredExhaustion;
        }

        /// <summary>False when endurance is switched off - nothing changed.</summary>
        public bool Charged { get; }

        /// <summary>Points charged (CostPerBlow × the multipliers).</summary>
        public double Cost { get; }

        /// <summary>Hero × leader multiplier applied (1 for a common soldier).</summary>
        public double Multiplier { get; }

        /// <summary>The fighter's pool in points at the time.</summary>
        public double Pool { get; }

        /// <summary>Points before the blow.</summary>
        public double Before { get; }

        /// <summary>Points after (never below 0).</summary>
        public double After { get; }

        /// <summary>This blow emptied the pool of a fighter who was not exhausted.</summary>
        public bool EnteredExhaustion { get; }
    }

    /// <summary>One regen step for one fighter.</summary>
    public readonly struct RegenOutcome
    {
        public RegenOutcome(double gained, double seconds, bool moving, bool recovered, double exhaustedSeconds, bool reachedFull,
            double episodeStartFraction, double episodeStandingSeconds, double episodeMovingSeconds)
        {
            Gained = gained;
            Seconds = seconds;
            Moving = moving;
            Recovered = recovered;
            ExhaustedSeconds = exhaustedSeconds;
            ReachedFull = reachedFull;
            EpisodeStartFraction = episodeStartFraction;
            EpisodeStandingSeconds = episodeStandingSeconds;
            EpisodeMovingSeconds = episodeMovingSeconds;
        }

        /// <summary>Fraction of the pool gained in this step.</summary>
        public double Gained { get; }

        /// <summary>Seconds of this step that actually refilled (after the delay, before full).</summary>
        public double Seconds { get; }

        /// <summary>Counted as moving (the slower refill).</summary>
        public bool Moving { get; }

        /// <summary>Left exhaustion in this step (speed comes back).</summary>
        public bool Recovered { get; }

        /// <summary>How long the exhaustion that just ended lasted.</summary>
        public double ExhaustedSeconds { get; }

        /// <summary>The refill run ended at full in this step (the Episode* values describe it).</summary>
        public bool ReachedFull { get; }

        public double EpisodeStartFraction { get; }

        public double EpisodeStandingSeconds { get; }

        public double EpisodeMovingSeconds { get; }
    }

    /// <summary>What the HUD reads for one fighter (steps 6-9) - a snapshot, no references kept.</summary>
    public readonly struct EnduranceReading
    {
        public EnduranceReading(bool enabled, double points, double pool, double fraction, bool exhausted, bool isHero, bool isLeader, float speedMultiplier)
        {
            Enabled = enabled;
            Points = points;
            Pool = pool;
            Fraction = fraction;
            Exhausted = exhausted;
            IsHero = isHero;
            IsLeader = isLeader;
            SpeedMultiplier = speedMultiplier;
        }

        /// <summary>EnduranceEnabled at the time of reading - off: everyone reads full, unpenalized.</summary>
        public bool Enabled { get; }

        /// <summary>Endurance left, in points.</summary>
        public double Points { get; }

        /// <summary>This fighter's pool, in points.</summary>
        public double Pool { get; }

        /// <summary>0..1.</summary>
        public double Fraction { get; }

        public bool Exhausted { get; }

        public bool IsHero { get; }

        public bool IsLeader { get; }

        /// <summary>The attack-speed multiplier applied now (1 = none).</summary>
        public float SpeedMultiplier { get; }
    }

    /// <summary>
    /// DESIGN §2 as pure functions. EACH RULE IS ONE FUNCTION of (fighter, live rules), so the
    /// planned endurance v2 (DESIGN §2b, step 5c) changes a rule in one place:
    ///   <see cref="PoolPoints"/>        pool size for a fighter (§2: flat MaxEndurance; 5c: Athletics, health cap)
    ///   <see cref="BlowCostPoints"/>    cost of one blow (CostPerBlow × hero × leader)
    ///   <see cref="RegenFractionPerSecond"/> regen rate from speed AND top speed (§2: standing/moving; 5c: effort)
    ///   <see cref="AttackSpeedMultiplier"/>  the float the stat decorator applies (§2: the cliff; 5c: a line)
    ///   <see cref="IsExhausted"/>       exhausted (for logs, bars and the penalty)
    /// Settings are passed in (<see cref="EnduranceRules"/>, read live by the caller); nothing is cached.
    /// </summary>
    public static class EnduranceMath
    {
        /// <summary>Below this share of the pool counts as empty - so 10 blows of 10 empty a 100 pool
        /// exactly, whatever floating point says.</summary>
        public const double Epsilon = 1e-9;

        /// <summary>The smallest change of the speed multiplier worth a recompute of the agent's
        /// properties (engine plumbing, not a gameplay number: the cliff of §2 only ever jumps between
        /// 1 and the exhausted value; 5c's straight line will move in small steps).</summary>
        public const float SpeedUpdateStep = 0.02f;

        // ------------------------------------------------------------------ the rules, one function each

        /// <summary>Pool size in points for this fighter. §2: the flat <c>MaxEndurance</c>.</summary>
        public static double PoolPoints(in EnduranceRules r, Fighter f) => Math.Max(1, r.MaxEndurance);

        /// <summary>Hero × party-leader multiplier for this fighter (they stack: 0.75 × 0.75).</summary>
        public static double CostMultiplier(in EnduranceRules r, Fighter f) =>
            (f.IsHero ? r.HeroCostMultiplier : 1.0) * (f.IsLeader ? r.PartyLeaderCostMultiplier : 1.0);

        /// <summary>Points one blow costs this fighter: CostPerBlow × the multipliers (10 / 7.5 / 5.6).</summary>
        public static double BlowCostPoints(in EnduranceRules r, Fighter f) => Math.Max(0, r.CostPerBlow * CostMultiplier(in r, f));

        /// <summary>Counts as moving: <paramref name="speed"/> (m/s, the horse's for a rider) above
        /// <c>MovingSpeedThreshold</c>.</summary>
        public static bool IsMoving(in EnduranceRules r, float speed) => speed > r.MovingSpeedThreshold;

        /// <summary>
        /// Refill rate in pool fractions per second. §2: full in <c>FullRegenSecondsStanding</c> while
        /// standing, <c>FullRegenSecondsMoving</c> while moving. <paramref name="topSpeed"/> (the
        /// fighter's current top speed, the horse's for a rider) is unused by §2 - it is passed so
        /// 5c's effort rule (speed ÷ top speed) needs no new plumbing.
        /// </summary>
        public static double RegenFractionPerSecond(in EnduranceRules r, Fighter f, float speed, float topSpeed)
        {
            double seconds = IsMoving(in r, speed) ? r.FullRegenSecondsMoving : r.FullRegenSecondsStanding;
            return seconds > 0 ? 1.0 / seconds : 0;
        }

        /// <summary>Exhausted now (the penalty applies). Off → never.</summary>
        public static bool IsExhausted(in EnduranceRules r, Fighter f) => r.Enabled && f.Exhausted;

        /// <summary>The attack-speed multiplier for this fighter now. §2: the cliff -
        /// <c>ExhaustedAttackSpeedPercent</c>/100 while exhausted, else 1.</summary>
        public static float AttackSpeedMultiplier(in EnduranceRules r, Fighter f) =>
            IsExhausted(in r, f) ? Math.Max(0.01f, r.ExhaustedAttackSpeedPercent / 100f) : 1f;

        /// <summary>True when <paramref name="desired"/> differs enough from what is applied to be
        /// worth a recompute: a step of <see cref="SpeedUpdateStep"/>, or any move to or from 1 (so a
        /// recovered fighter always gets his full speed back exactly).</summary>
        public static bool SpeedUpdateNeeded(float applied, float desired) =>
            Math.Abs(applied - desired) >= SpeedUpdateStep || ((applied == 1f) != (desired == 1f));

        // ------------------------------------------------------------------ reads

        /// <summary>Points left now (off → a full pool).</summary>
        public static double Points(in EnduranceRules r, Fighter f) => (r.Enabled ? f.Fraction : 1.0) * PoolPoints(in r, f);

        public static EnduranceReading Read(in EnduranceRules r, Fighter f)
        {
            double pool = PoolPoints(in r, f);
            if (!r.Enabled) return new EnduranceReading(false, pool, pool, 1.0, false, f.IsHero, f.IsLeader, f.SpeedMultiplier);
            return new EnduranceReading(true, f.Fraction * pool, pool, f.Fraction, f.Exhausted, f.IsHero, f.IsLeader, f.SpeedMultiplier);
        }

        // ------------------------------------------------------------------ changes

        /// <summary>
        /// One blow: take <see cref="BlowCostPoints"/> from the pool, never below 0; at 0 the fighter
        /// becomes exhausted (a cliff). Any blow restarts the regen delay and ends a refill run.
        /// Off → nothing happens (<see cref="BlowOutcome.Charged"/> false).
        /// </summary>
        public static BlowOutcome Charge(Fighter f, in EnduranceRules r, double now)
        {
            if (!r.Enabled) return default;
            double pool = PoolPoints(in r, f);
            double multiplier = CostMultiplier(in r, f);
            double cost = BlowCostPoints(in r, f);
            double before = f.Fraction;
            double after = before - cost / pool;
            if (after <= Epsilon) after = 0;
            if (after > 1) after = 1;

            f.Fraction = after;
            f.LastBlowTime = now;
            f.Blows++;
            f.Regenerating = false;
            if (after < f.LowestFraction) f.LowestFraction = after;

            bool entered = false;
            if (after == 0 && !f.Exhausted)
            {
                f.Exhausted = true;
                f.ExhaustionsEntered++;
                f.ExhaustedSince = now;
                entered = true;
            }
            return new BlowOutcome(true, cost, multiplier, pool, before * pool, after * pool, entered);
        }

        /// <summary>
        /// Regeneration over the step (<paramref name="now"/> − <paramref name="dt"/>, <paramref name="now"/>]:
        /// only the part after <c>last blow + RegenDelaySeconds</c> refills, at
        /// <see cref="RegenFractionPerSecond"/> (speed sampled once per step), capped at full. Then the
        /// recovery check - run every step, so a lowered <c>ExhaustedRecoverPercent</c> applies at once.
        /// Off → nothing.
        /// </summary>
        public static RegenOutcome Regen(Fighter f, in EnduranceRules r, double now, double dt, float speed, float topSpeed)
        {
            if (!r.Enabled || dt <= 0) return default;
            bool moving = IsMoving(in r, speed);
            double gained = 0, used = 0;
            bool reachedFull = false;
            double episodeStart = 0, episodeStanding = 0, episodeMoving = 0;

            double regenFrom = f.LastBlowTime + r.RegenDelaySeconds;
            double effective = now - Math.Max(now - dt, regenFrom);
            if (effective > dt) effective = dt;
            if (effective > 0 && f.Fraction < 1.0)
            {
                double rate = RegenFractionPerSecond(in r, f, speed, topSpeed);
                if (rate > 0)
                {
                    if (!f.Regenerating)
                    {
                        f.Regenerating = true;
                        f.EpisodeStartFraction = f.Fraction;
                        f.EpisodeStartTime = now - effective;
                        f.EpisodeStandingSeconds = 0;
                        f.EpisodeMovingSeconds = 0;
                    }
                    double before = f.Fraction;
                    double after = before + rate * effective;
                    used = effective;
                    if (after >= 1.0 - Epsilon)
                    {
                        used = Math.Min(effective, (1.0 - before) / rate);
                        after = 1.0;
                        reachedFull = true;
                    }
                    f.Fraction = after;
                    gained = after - before;
                    if (moving) f.EpisodeMovingSeconds += used;
                    else f.EpisodeStandingSeconds += used;
                    if (reachedFull)
                    {
                        f.Regenerating = false;
                        episodeStart = f.EpisodeStartFraction;
                        episodeStanding = f.EpisodeStandingSeconds;
                        episodeMoving = f.EpisodeMovingSeconds;
                    }
                }
            }

            bool recovered = false;
            double exhaustedFor = 0;
            if (f.Exhausted && f.Fraction > r.RecoverAboveFraction)
            {
                f.Exhausted = false;
                recovered = true;
                exhaustedFor = now - f.ExhaustedSince;
            }
            return new RegenOutcome(gained, used, moving, recovered, exhaustedFor, reachedFull, episodeStart, episodeStanding, episodeMoving);
        }
    }
}

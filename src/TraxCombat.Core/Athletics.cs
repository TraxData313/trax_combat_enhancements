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
    /// The Athletics settings in effect for ONE decision, read live from
    /// <see cref="TraxSettings"/> (<see cref="From"/>) - never cached across a tick, so an MCM change
    /// mid-battle applies to the next blow / regen step. Built fresh where needed (a struct: no
    /// allocation).
    /// </summary>
    public readonly struct AthleticsRules
    {
        public AthleticsRules(bool enabled, int poolFloor, float poolPerSkill, int peakPercent, bool healthCaps,
            float costPerBlow, bool costOnMiss, float heroCostMultiplier, float partyLeaderCostMultiplier, float costPerKickOrBash,
            int exhaustedAttackSpeedPercent, float minMoveSpeedMultiplier, float mountMinSpeedMultiplier, bool damageBonusFollows,
            float regenDelayBlowTimes, float blowTimeSeconds, float fullRegenSecondsStanding, float regenMultiplierAtFullRun,
            float walkEffortFraction, int regenRateNearFullPercent, bool modEnabled = true,
            float costPerShieldBlock = 0, float costPerWrongSideShieldBlock = 0, float costPerWeaponParry = 0)
        {
            ModEnabled = modEnabled;
            CostPerShieldBlock = costPerShieldBlock;
            CostPerWrongSideShieldBlock = costPerWrongSideShieldBlock;
            CostPerWeaponParry = costPerWeaponParry;
            AthleticsEnabled = enabled;
            PoolFloor = poolFloor;
            PoolPerSkill = poolPerSkill;
            PeakPercent = peakPercent;
            HealthCaps = healthCaps;
            CostPerBlow = costPerBlow;
            CostOnMiss = costOnMiss;
            HeroCostMultiplier = heroCostMultiplier;
            PartyLeaderCostMultiplier = partyLeaderCostMultiplier;
            CostPerKickOrBash = costPerKickOrBash;
            ExhaustedAttackSpeedPercent = exhaustedAttackSpeedPercent;
            MinMoveSpeedMultiplier = minMoveSpeedMultiplier;
            MountMinSpeedMultiplier = mountMinSpeedMultiplier;
            DamageBonusFollows = damageBonusFollows;
            RegenDelayBlowTimes = regenDelayBlowTimes;
            BlowTimeSeconds = blowTimeSeconds;
            FullRegenSecondsStanding = fullRegenSecondsStanding;
            RegenMultiplierAtFullRun = regenMultiplierAtFullRun;
            WalkEffortFraction = walkEffortFraction;
            RegenRateNearFullPercent = regenRateNearFullPercent;
        }

        /// <summary>The mod's master switch (ModEnabled).</summary>
        public bool ModEnabled { get; }

        /// <summary>AthleticsEnabled - Athletics' own switch.</summary>
        public bool AthleticsEnabled { get; }

        /// <summary>Athletics is live: the master switch AND its own switch are on. Off → nobody pays,
        /// nobody refills, nobody is slowed, every reading is full (vanilla).</summary>
        public bool Enabled => ModEnabled && AthleticsEnabled;

        /// <summary>Which switch turned it off, for the log: "ModEnabled" (the whole mod) or
        /// "AthleticsEnabled"; null while it is on.</summary>
        public string? OffBecause => !ModEnabled ? "ModEnabled" : !AthleticsEnabled ? "AthleticsEnabled" : null;

        /// <summary>AthleticsPoolFloor - the smallest pool in points (0 = none).</summary>
        public int PoolFloor { get; }

        /// <summary>AthleticsPoolPerSkill - pool points per Athletics skill point.</summary>
        public float PoolPerSkill { get; }

        /// <summary>AthleticsPeakPercent - the full-strength line, % of the FULL pool.</summary>
        public int PeakPercent { get; }

        /// <summary>HealthCapsAthletics.</summary>
        public bool HealthCaps { get; }

        public float CostPerBlow { get; }

        public bool CostOnMiss { get; }

        public float HeroCostMultiplier { get; }

        public float PartyLeaderCostMultiplier { get; }

        /// <summary>CostPerKickOrBash (step 18): points one kick or shield bash costs before the hero and
        /// party-leader multipliers; 0 = free (steps 5-17).</summary>
        public float CostPerKickOrBash { get; }

        /// <summary>CostPerShieldBlock (step 22): points the DEFENDER pays for a melee blow he blocks with his
        /// shield on the correct side, before the hero and party-leader multipliers; 0 = free.</summary>
        public float CostPerShieldBlock { get; }

        /// <summary>CostPerWrongSideShieldBlock (step 22): …with his shield on the wrong side (the engine's
        /// <c>CorrectSideShieldBlock</c> false); 0 = free.</summary>
        public float CostPerWrongSideShieldBlock { get; }

        /// <summary>CostPerWeaponParry (step 22): …parried with a weapon (blocked, no shield - a chamber block
        /// too); 0 = free.</summary>
        public float CostPerWeaponParry { get; }

        /// <summary>Attack speed at 0 Athletics, % of normal (S of the curve).</summary>
        public int ExhaustedAttackSpeedPercent { get; }

        /// <summary>Run speed on foot at 0 Athletics (M of the curve).</summary>
        public float MinMoveSpeedMultiplier { get; }

        /// <summary>Horse speed at the rider's 0 Athletics; 1 = horses are never slowed.</summary>
        public float MountMinSpeedMultiplier { get; }

        /// <summary>DamageBonusFollowsAthletics.</summary>
        public bool DamageBonusFollows { get; }

        public float RegenDelayBlowTimes { get; }

        public float BlowTimeSeconds { get; }

        /// <summary>Seconds from empty to full at rest (standing or walking).</summary>
        public float FullRegenSecondsStanding { get; }

        /// <summary>Regen rate at a full run, times the rate at rest.</summary>
        public float RegenMultiplierAtFullRun { get; }

        /// <summary>Up to this share of the current top speed counts as walking (full regen).</summary>
        public float WalkEffortFraction { get; }

        /// <summary>RegenRateNearFullPercent (step 14): the refill rate near full, % of the rate near
        /// empty - the straight line of <see cref="AthleticsMath.RegenCurve"/>; 100 = the flat refill.</summary>
        public int RegenRateNearFullPercent { get; }

        /// <summary>k of the refill curve: RegenRateNearFullPercent / 100 kept inside 0.01..1 (k 0 would
        /// never reach full; above 1 would refill slower when low).</summary>
        public double RegenNearFullShare => RegenRateNearFullPercent >= 100 ? 1.0 : RegenRateNearFullPercent <= 1 ? 0.01 : RegenRateNearFullPercent / 100.0;

        /// <summary>Seconds without a blow before regeneration starts: 2 × 1.5 = 3 s by default.</summary>
        public double RegenDelaySeconds => (double)RegenDelayBlowTimes * BlowTimeSeconds;

        /// <summary>The peak line as a share of the full pool (0.75), kept inside 0.01..1.</summary>
        public double PeakFraction => PeakPercent <= 1 ? 0.01 : PeakPercent >= 100 ? 1.0 : PeakPercent / 100.0;

        /// <summary>S - the attack-speed multiplier at 0 (never below 0.01: an animation must run).</summary>
        public float AttackSpeedFloor => AttackSpeedFloorOf(ExhaustedAttackSpeedPercent);

        /// <summary>S for an ExhaustedAttackSpeedPercent value (0.01..1) - the stat decorator reads it without building the rules.</summary>
        public static float AttackSpeedFloorOf(int exhaustedAttackSpeedPercent) => Math.Max(0.01f, Math.Min(1f, exhaustedAttackSpeedPercent / 100f));

        /// <summary>M - the run-speed multiplier at 0 on foot (0.01..1).</summary>
        public float RunSpeedFloor => Math.Max(0.01f, Math.Min(1f, MinMoveSpeedMultiplier));

        /// <summary>The horse-speed multiplier at the rider's 0 (0.01..1).</summary>
        public float MountSpeedFloor => Math.Max(0.01f, Math.Min(1f, MountMinSpeedMultiplier));

        /// <summary>Horses follow their riders' Athletics at all (MountMinSpeedMultiplier below 1).</summary>
        public bool MountsSlow => Enabled && MountMinSpeedMultiplier < 1f;

        /// <summary>The live values, read now.</summary>
        public static AthleticsRules From(TraxSettings s) => new AthleticsRules(
            s.AthleticsEnabled, s.AthleticsPoolFloor, s.AthleticsPoolPerSkill, s.AthleticsPeakPercent, s.HealthCapsAthletics,
            s.CostPerBlow, s.CostOnMiss, s.HeroCostMultiplier, s.PartyLeaderCostMultiplier, s.CostPerKickOrBash,
            s.ExhaustedAttackSpeedPercent, s.MinMoveSpeedMultiplier, s.MountMinSpeedMultiplier, s.DamageBonusFollowsAthletics,
            s.RegenDelayBlowTimes, s.BlowTimeSeconds, s.FullRegenSecondsStanding, s.RegenMultiplierAtFullRun,
            s.WalkEffortFraction, s.RegenRateNearFullPercent, s.ModEnabled,
            s.CostPerShieldBlock, s.CostPerWrongSideShieldBlock, s.CostPerWeaponParry);
    }

    /// <summary>
    /// One fighter's Athletics - pure state, no game types (the module's per-agent record derives
    /// from it). Athletics is stored as a FRACTION of the fighter's FULL pool (1 = full), so a
    /// change of the pool settings mid-battle keeps everyone's share by construction (DESIGN §2);
    /// points = fraction × <see cref="AthleticsMath.PoolPoints"/>. A blow's cost is in POINTS and
    /// becomes cost ÷ pool of the fraction at the moment of the blow. The health cap is a ceiling on
    /// the fraction (the health left, 0..1). Mutated only through <see cref="AthleticsMath"/>
    /// (charge, regen, health, reset) - main thread.
    /// </summary>
    public class Fighter
    {
        /// <summary>0..1 of the FULL pool (never above the health cap while it is on).</summary>
        public double Fraction { get; internal set; } = 1.0;

        /// <summary>The Athletics SKILL (character screen), cached at spawn - heroes their real skill.
        /// The pool is computed from it live (<see cref="AthleticsMath.PoolPoints"/>). 0 = unknown.</summary>
        public int AthleticsSkill { get; set; }

        /// <summary>Health left, 0..1 of the maximum - kept current by the module (hits, the regen
        /// pass); 1 in pure tests unless set. Caps the fraction while HealthCapsAthletics is on.</summary>
        public double Health { get; internal set; } = 1.0;

        /// <summary>Athletics is at 0 (E = 0): set by the blow that empties it, cleared by the first
        /// refill above 0. For logs and bars - the penalties follow the curve, not this flag.</summary>
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

        /// <summary>Kicks and shield bashes charged this mission (step 18) - not blows: <see cref="Blows"/>
        /// never counts them.</summary>
        public int KicksAndBashes { get; internal set; }

        /// <summary>Blocked blows this fighter PAID for as the defender this mission (step 22) - not blows,
        /// not kicks: <see cref="Blows"/> never counts them.</summary>
        public int BlocksPaid { get; internal set; }

        public int ExhaustionsEntered { get; internal set; }

        /// <summary>Mission time the current exhaustion began.</summary>
        public double ExhaustedSince { get; internal set; }

        /// <summary>A refill run is under way (since the regen delay ran out); a blow ends it.</summary>
        public bool Regenerating { get; internal set; }

        public double EpisodeStartFraction { get; internal set; }

        public double EpisodeStartTime { get; internal set; }

        /// <summary>Seconds of this refill run that actually refilled.</summary>
        public double EpisodeSeconds { get; internal set; }

        /// <summary>…of them at walking pace or slower (the full rate).</summary>
        public double EpisodeWalkSeconds { get; internal set; }

        /// <summary>Σ rate multiplier × seconds - ÷ <see cref="EpisodeSeconds"/> = the average rate.</summary>
        public double EpisodeRateSeconds { get; internal set; }

        /// <summary>
        /// The attack-speed multiplier the stat decorator applies to this fighter RIGHT NOW (1 = none).
        /// Set by the module when it decides a new value (<see cref="AthleticsMath.SpeedUpdateNeeded"/>)
        /// just before it asks the game to recompute the agent's properties - so every recompute the
        /// game does on its own (weapon switch, mount) applies the same value.
        /// </summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>The run-speed multiplier on foot applied now (MaxSpeedMultiplier), 1 = none.</summary>
        public float RunSpeedMultiplier { get; set; } = 1f;

        /// <summary>The multiplier applied now to the horse this fighter rides (MountSpeed), 1 = none.</summary>
        public float MountSpeedMultiplier { get; set; } = 1f;

        /// <summary>Everyone starts a mission full; Athletics turned off (AthleticsEnabled or the
        /// master switch ModEnabled) puts everyone back to full. A wound's cap applies again at the
        /// next regen step once Athletics is on.</summary>
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
        public BlowOutcome(bool charged, double cost, double multiplier, double pool, double before, double after, bool enteredExhaustion,
            double peakShareBefore, double peakShareAfter)
        {
            Charged = charged;
            Cost = cost;
            Multiplier = multiplier;
            Pool = pool;
            Before = before;
            After = after;
            EnteredExhaustion = enteredExhaustion;
            PeakShareBefore = peakShareBefore;
            PeakShareAfter = peakShareAfter;
        }

        /// <summary>False when Athletics is switched off - nothing changed.</summary>
        public bool Charged { get; }

        /// <summary>Points charged (CostPerBlow - or CostPerKickOrBash for a kick / bash, a block's cost for a
        /// blocked blow - × the multipliers).</summary>
        public double Cost { get; }

        /// <summary>Hero × leader multiplier applied (1 for a common soldier).</summary>
        public double Multiplier { get; }

        /// <summary>The fighter's FULL pool in points at the time.</summary>
        public double Pool { get; }

        /// <summary>Points before the blow.</summary>
        public double Before { get; }

        /// <summary>Points after (never below 0).</summary>
        public double After { get; }

        /// <summary>This blow emptied the pool of a fighter who was not exhausted.</summary>
        public bool EnteredExhaustion { get; }

        /// <summary>f before the blow - the strength this blow was struck with.</summary>
        public double PeakShareBefore { get; }

        /// <summary>f after it.</summary>
        public double PeakShareAfter { get; }

        /// <summary>This blow took the fighter out of the peak zone (f 1 → below 1).</summary>
        public bool LeftPeak => PeakShareBefore >= 1.0 && PeakShareAfter < 1.0;
    }

    /// <summary>One regen step for one fighter.</summary>
    public readonly struct RegenOutcome
    {
        public RegenOutcome(double gained, double seconds, double effort, double rateMultiplier, bool walking, bool recovered,
            double exhaustedSeconds, bool reachedTop, double top, double episodeStartFraction, double episodeSeconds,
            double episodeWalkSeconds, double episodeRateSeconds, bool enteredPeak, double emptyToPeakSeconds = 0)
        {
            EmptyToPeakSeconds = emptyToPeakSeconds;
            Gained = gained;
            Seconds = seconds;
            Effort = effort;
            RateMultiplier = rateMultiplier;
            Walking = walking;
            Recovered = recovered;
            ExhaustedSeconds = exhaustedSeconds;
            ReachedTop = reachedTop;
            Top = top;
            EpisodeStartFraction = episodeStartFraction;
            EpisodeSeconds = episodeSeconds;
            EpisodeWalkSeconds = episodeWalkSeconds;
            EpisodeRateSeconds = episodeRateSeconds;
            EnteredPeak = enteredPeak;
        }

        /// <summary>Fraction of the pool gained in this step.</summary>
        public double Gained { get; }

        /// <summary>Seconds of this step that actually refilled (after the delay, below the top).</summary>
        public double Seconds { get; }

        /// <summary>Speed ÷ current top speed sampled for this step (0 when unknown).</summary>
        public double Effort { get; }

        /// <summary>The rate multiplier the effort gave (1 at a walk or slower).</summary>
        public double RateMultiplier { get; }

        /// <summary>Effort at or below WalkEffortFraction (the full rate).</summary>
        public bool Walking { get; }

        /// <summary>Left exhaustion (0 Athletics) in this step.</summary>
        public bool Recovered { get; }

        /// <summary>How long the exhaustion that just ended lasted.</summary>
        public double ExhaustedSeconds { get; }

        /// <summary>The refill run reached the top in this step - full, or the health cap (the
        /// Episode* values describe the run).</summary>
        public bool ReachedTop { get; }

        /// <summary>The top this refill may reach: 1 unhurt, the health left under the cap.</summary>
        public double Top { get; }

        public double EpisodeStartFraction { get; }

        public double EpisodeSeconds { get; }

        public double EpisodeWalkSeconds { get; }

        public double EpisodeRateSeconds { get; }

        /// <summary>This step lifted the fighter back into the peak zone (f reached 1).</summary>
        public bool EnteredPeak { get; }

        /// <summary>Step 14: when this step crossed the peak line on a refill run that began at EMPTY (no blow
        /// between), the seconds of refill from 0 to the line (exact, to the crossing); 0 otherwise.</summary>
        public double EmptyToPeakSeconds { get; }
    }

    /// <summary>One fresh start (step 19, <see cref="AthleticsMath.FreshStart"/>): the bar before and after, as
    /// shares of the FULL pool.</summary>
    public readonly struct FreshStartOutcome
    {
        public FreshStartOutcome(bool refilled, double pool, double before, double after, bool wasExhausted, double peakShareBefore, double peakShareAfter)
        {
            Refilled = refilled;
            Pool = pool;
            Before = before;
            After = after;
            WasExhausted = wasExhausted;
            PeakShareBefore = peakShareBefore;
            PeakShareAfter = peakShareAfter;
        }

        /// <summary>False while Athletics is off - nothing changed.</summary>
        public bool Refilled { get; }

        /// <summary>His FULL pool in points.</summary>
        public double Pool { get; }

        /// <summary>The fill before, 0..1 of the full pool.</summary>
        public double Before { get; }

        /// <summary>The fill after = the top he can refill to: 1, or the health left under the cap.</summary>
        public double After { get; }

        public bool WasExhausted { get; }

        public double PeakShareBefore { get; }

        public double PeakShareAfter { get; }

        public double BeforePoints => Before * Pool;

        public double AfterPoints => After * Pool;

        /// <summary>Points gained (never negative in practice: the cap was already applied at his last hit).</summary>
        public double GainedPoints => (After - Before) * Pool;

        /// <summary>He was already at the top he can refill to.</summary>
        public bool AlreadyAtTop => Before >= After - AthleticsMath.Epsilon;

        /// <summary>His wounds hold the top below a full bar (HealthCapsAthletics).</summary>
        public bool Capped => After < 1.0 - AthleticsMath.Epsilon;
    }

    /// <summary>What the HUD reads for one fighter (steps 6-9) - a snapshot, no references kept.</summary>
    public readonly struct AthleticsReading
    {
        public AthleticsReading(bool enabled, double points, double pool, double usablePool, double fraction, double usableFraction,
            double peakShare, double peakFraction, bool exhausted, bool isHero, bool isLeader, float speedMultiplier,
            float runSpeedMultiplier, float mountSpeedMultiplier, int athleticsSkill)
        {
            Enabled = enabled;
            Points = points;
            Pool = pool;
            UsablePool = usablePool;
            Fraction = fraction;
            UsableFraction = usableFraction;
            PeakShare = peakShare;
            PeakFraction = peakFraction;
            Exhausted = exhausted;
            IsHero = isHero;
            IsLeader = isLeader;
            SpeedMultiplier = speedMultiplier;
            RunSpeedMultiplier = runSpeedMultiplier;
            MountSpeedMultiplier = mountSpeedMultiplier;
            AthleticsSkill = athleticsSkill;
        }

        /// <summary>Athletics live at the time of reading (ModEnabled AND AthleticsEnabled) - off:
        /// everyone reads full, unpenalized, and the HUD should hide.</summary>
        public bool Enabled { get; }

        /// <summary>Athletics left, in points (E).</summary>
        public double Points { get; }

        /// <summary>This fighter's FULL pool, in points (the bar's length; from his Athletics skill).</summary>
        public double Pool { get; }

        /// <summary>The part of the pool he can use now: pool × health left while HealthCapsAthletics
        /// is on (the bar's reachable end - draw the rest greyed), else the pool.</summary>
        public double UsablePool { get; }

        /// <summary>Points ÷ Pool, 0..1 - the bar fill.</summary>
        public double Fraction { get; }

        /// <summary>UsablePool ÷ Pool, 0..1 - where the grey (wounded) part of the bar starts.</summary>
        public double UsableFraction { get; }

        /// <summary>f of DESIGN §2: the share of the peak line left, 0..1 (1 = full strength, the
        /// peak zone). The bar colours are thresholds on it (step 6).</summary>
        public double PeakShare { get; }

        /// <summary>The peak line as a share of the pool (0.75) - where to draw the marker.</summary>
        public double PeakFraction { get; }

        /// <summary>At or above the peak line: full strength (green).</summary>
        public bool InPeakZone => PeakShare >= 1.0;

        /// <summary>E = 0.</summary>
        public bool Exhausted { get; }

        public bool IsHero { get; }

        public bool IsLeader { get; }

        /// <summary>The attack-speed multiplier applied now (1 = none).</summary>
        public float SpeedMultiplier { get; }

        /// <summary>The run-speed multiplier on foot applied now (1 = none).</summary>
        public float RunSpeedMultiplier { get; }

        /// <summary>The multiplier applied now to his horse (1 = none - the default: horses never slow).</summary>
        public float MountSpeedMultiplier { get; }

        /// <summary>The Athletics skill the pool came from (0 = unknown).</summary>
        public int AthleticsSkill { get; }

        /// <summary>Below the top he can refill to (the usable pool - his wounds may hold the rest):
        /// still refilling. Step 12: outside a battle the player bar stays up while this is true.
        /// False while Athletics is off (everyone reads full).</summary>
        public bool BelowFull => Enabled && Fraction < UsableFraction - FullTolerance;

        /// <summary>How close to the usable top counts as full (a share of the pool; the refill stops
        /// exactly at the top, so this only absorbs rounding).</summary>
        public const double FullTolerance = 1e-6;
    }

    /// <summary>
    /// DESIGN §2 as pure functions. EACH RULE IS ONE FUNCTION of (fighter, live rules):
    ///   <see cref="PoolPoints"/>          pool = max(floor, per-skill × Athletics skill), never below 1
    ///   <see cref="UsableFraction"/>      the health cap (health left, or 1)
    ///   <see cref="PeakShare"/>           f = min(E / (peak% × FULL pool), 1)
    ///   <see cref="BlowCostPoints"/>      cost of one blow in POINTS (CostPerBlow × hero × leader)
    ///   <see cref="KickOrBashCostPoints"/> cost of one kick or shield bash (CostPerKickOrBash × the same)
    ///   <see cref="BlockCostPoints"/>     step 22: what the DEFENDER pays for a blow he blocks, by kind (× the same)
    ///   <see cref="AttackSpeedMultiplier"/>, <see cref="RunSpeedMultiplier"/>, <see cref="MountSpeedMultiplier"/>
    ///                                     the curves floor + (1 − floor) × f
    ///   <see cref="DamageUpside"/>        the f the damage roll's upside follows
    ///   <see cref="RegenRateMultiplier"/> regen by effort (speed ÷ current top speed)
    ///   <see cref="RegenCurve"/>          the refill curve (step 14): faster low, slower full, empty → full
    ///                                     still FullRegenSecondsStanding (<see cref="RegenRateAtEmpty"/>,
    ///                                     <see cref="RefillFrom"/> / <see cref="RefillSeconds"/> - exact)
    ///   <see cref="FreshStart"/>          step 19: the hideout boss fight's refill - to the top the wounds allow
    /// Settings are passed in (<see cref="AthleticsRules"/>, read live by the caller); nothing is cached.
    /// </summary>
    public static class AthleticsMath
    {
        /// <summary>Below this share of the pool counts as empty - so 5 blows of 10 empty a 50 pool
        /// exactly, whatever floating point says.</summary>
        public const double Epsilon = 1e-9;

        /// <summary>
        /// The smallest change of an applied speed multiplier (attack, run or horse) worth a recompute
        /// of the agent's properties (<c>Agent.UpdateAgentProperties</c> - a whole stat-model pass).
        /// 0.05 = 5% of normal speed, below what a player can see; a full refill from empty to the peak
        /// then asks about 16 recomputes per fighter (the attack curve spans 0.2..1), instead of one
        /// per regen step. Moves to or from exactly 1 or exactly the floor always go through, so a fresh
        /// fighter is exactly at full speed and an empty one exactly at the asked minimum. Engine
        /// plumbing, not a gameplay number (step 5c).
        /// </summary>
        public const float SpeedUpdateStep = 0.05f;

        /// <summary>f bins for the summary: peak (f 1), 0.5-1, below 0.5, empty (f 0).</summary>
        public const int PeakBins = 4;

        // ------------------------------------------------------------------ the pool

        /// <summary>Pool in points for an Athletics skill: max(floor, per-skill × skill), never below 1.</summary>
        public static double PoolPointsForSkill(in AthleticsRules r, int skill) =>
            Math.Max(1.0, Math.Max(r.PoolFloor, (double)r.PoolPerSkill * Math.Max(0, skill)));

        /// <summary>This fighter's FULL pool in points (from his cached Athletics skill, live settings).</summary>
        public static double PoolPoints(in AthleticsRules r, Fighter f) => PoolPointsForSkill(in r, f.AthleticsSkill);

        /// <summary>The pool of this skill comes from the floor (the skill alone would give less).</summary>
        public static bool IsAtFloor(in AthleticsRules r, int skill) => (double)r.PoolPerSkill * Math.Max(0, skill) < r.PoolFloor;

        /// <summary>The health cap: the share of the pool he can use - his health left while
        /// HealthCapsAthletics is on, else 1.</summary>
        public static double UsableFraction(in AthleticsRules r, Fighter f) => r.HealthCaps ? Clamp01(f.Health) : 1.0;

        /// <summary>Usable points (pool × the health cap).</summary>
        public static double UsablePoints(in AthleticsRules r, Fighter f) => PoolPoints(in r, f) * UsableFraction(in r, f);

        // ------------------------------------------------------------------ f and the curves

        /// <summary>f for a fraction of the FULL pool: min(fraction ÷ peak line, 1), 0..1.</summary>
        public static double PeakShareOf(in AthleticsRules r, double fraction)
        {
            if (fraction <= Epsilon) return 0;
            double f = fraction / r.PeakFraction;
            return f >= 1.0 - Epsilon ? 1.0 : f;
        }

        /// <summary>f = min(E / (AthleticsPeakPercent% × FULL pool), 1) - the share of the peak line
        /// left, driving every curve. Off → 1 (full strength, vanilla).</summary>
        public static double PeakShare(in AthleticsRules r, Fighter f) => r.Enabled ? PeakShareOf(in r, f.Fraction) : 1.0;

        /// <summary>floor + (1 − floor) × f; exactly 1 at f = 1 and exactly the floor at f = 0.</summary>
        public static float Curve(float floor, double peakShare)
        {
            if (peakShare >= 1.0) return 1f;
            if (peakShare <= 0) return floor;
            return (float)(floor + (1.0 - floor) * peakShare);
        }

        /// <summary>Attack speed: S + (1 − S) × f, S = ExhaustedAttackSpeedPercent/100. Off → 1.</summary>
        public static float AttackSpeedMultiplier(in AthleticsRules r, Fighter f) =>
            r.Enabled ? Curve(r.AttackSpeedFloor, PeakShare(in r, f)) : 1f;

        /// <summary>Run speed on foot: M + (1 − M) × f, M = MinMoveSpeedMultiplier. Off → 1.</summary>
        public static float RunSpeedMultiplier(in AthleticsRules r, Fighter f) =>
            r.Enabled ? Curve(r.RunSpeedFloor, PeakShare(in r, f)) : 1f;

        /// <summary>His horse's speed on the same curve with MountMinSpeedMultiplier; 1 while that is 1
        /// (the default - horses never slow) or Athletics is off.</summary>
        public static float MountSpeedMultiplier(in AthleticsRules r, Fighter f) =>
            r.MountsSlow ? Curve(r.MountSpeedFloor, PeakShare(in r, f)) : 1f;

        /// <summary>The f the damage roll's upside follows for this attacker: his f while
        /// DamageBonusFollowsAthletics is on, else 1 (the full upside).</summary>
        public static double DamageUpside(in AthleticsRules r, Fighter f) => r.DamageBonusFollows ? PeakShare(in r, f) : 1.0;

        /// <summary>The summary's f bin: 0 peak (f 1), 1 = 0.5 to 1, 2 = above 0 to 0.5, 3 = empty (f 0).</summary>
        public static int PeakBin(double peakShare) =>
            peakShare >= 1.0 - Epsilon ? 0 : peakShare >= 0.5 ? 1 : peakShare > Epsilon ? 2 : 3;

        public static string PeakBinName(int bin) => bin switch
        {
            0 => "peak (f 1)",
            1 => "f 0.5-1",
            2 => "f below 0.5",
            _ => "empty (f 0)",
        };

        // ------------------------------------------------------------------ cost

        /// <summary>Hero × party-leader multiplier for this fighter (they stack: 0.75 × 0.75).</summary>
        public static double CostMultiplier(in AthleticsRules r, Fighter f) =>
            (f.IsHero ? r.HeroCostMultiplier : 1.0) * (f.IsLeader ? r.PartyLeaderCostMultiplier : 1.0);

        /// <summary>Points one blow costs this fighter, whatever his pool: CostPerBlow × the multipliers
        /// (10 / 7.5 / 5.6).</summary>
        public static double BlowCostPoints(in AthleticsRules r, Fighter f) => Math.Max(0, r.CostPerBlow * CostMultiplier(in r, f));

        /// <summary>Points one kick or shield bash costs this fighter (step 18): CostPerKickOrBash × the same
        /// hero and party-leader multipliers as a blow (3 / 2.25 / 1.69). 0 = free.</summary>
        public static double KickOrBashCostPoints(in AthleticsRules r, Fighter f) => Math.Max(0, r.CostPerKickOrBash * CostMultiplier(in r, f));

        /// <summary>The setting a blocked blow of this kind costs before the multipliers (step 22): the shield on the
        /// right side CostPerShieldBlock, on the wrong side CostPerWrongSideShieldBlock, a weapon parry
        /// CostPerWeaponParry; None → 0.</summary>
        public static float BlockCostSetting(in AthleticsRules r, BlockKind kind) => kind switch
        {
            BlockKind.ShieldRightSide => r.CostPerShieldBlock,
            BlockKind.ShieldWrongSide => r.CostPerWrongSideShieldBlock,
            BlockKind.WeaponParry => r.CostPerWeaponParry,
            _ => 0f,
        };

        /// <summary>Points the DEFENDER pays for one blow he blocks (step 22): the kind's setting × the same hero and
        /// party-leader multipliers as a blow (1 / 5 / 2 for a soldier; 0.75 / 3.75 / 1.5 a hero; 0.56 / 2.81 / 1.13 a
        /// hero who leads his party). 0 = free.</summary>
        public static double BlockCostPoints(in AthleticsRules r, Fighter f, BlockKind kind) => Math.Max(0, BlockCostSetting(in r, kind) * CostMultiplier(in r, f));

        // ------------------------------------------------------------------ regen by effort

        /// <summary>Effort = speed ÷ current top speed (the horse's for a rider); 0 when the top is unknown.</summary>
        public static double Effort(float speed, float topSpeed) =>
            topSpeed > 0f && speed > 0f && !float.IsNaN(speed) ? speed / (double)topSpeed : 0.0;

        /// <summary>At walking pace or slower: effort up to WalkEffortFraction (the full refill rate).</summary>
        public static bool IsWalking(in AthleticsRules r, double effort) => effort <= Clamp01(r.WalkEffortFraction) || r.WalkEffortFraction >= 1f;

        /// <summary>The regen-rate multiplier for an effort: 1 up to WalkEffortFraction (a walk or
        /// slower), then a straight line down to RegenMultiplierAtFullRun at effort 1 (and beyond).</summary>
        public static double RegenRateMultiplier(in AthleticsRules r, double effort)
        {
            double walk = Clamp01(r.WalkEffortFraction);
            if (effort <= walk || walk >= 1.0) return 1.0;
            double t = (Math.Min(effort, 1.0) - walk) / (1.0 - walk);
            return 1.0 + (Math.Max(0, r.RegenMultiplierAtFullRun) - 1.0) * t;
        }

        /// <summary>The refill rate right now in pool fractions per second: <see cref="RegenRateAtEmpty"/> ×
        /// <see cref="RegenCurve"/> at his fill × <see cref="RegenRateMultiplier"/> for his effort. With
        /// RegenRateNearFullPercent 100 it is the flat 1 / FullRegenSecondsStanding of steps 5c-13.</summary>
        public static double RegenFractionPerSecond(in AthleticsRules r, Fighter f, float speed, float topSpeed) =>
            RegenRateAtEmpty(in r) * RegenCurve(in r, f.Fraction) * RegenRateMultiplier(in r, Effort(speed, topSpeed));

        // ------------------------------------------------------------------ the refill curve (step 14)
        //
        // Anton: "recover faster when it's low and slower as it is fuller ... maybe half linear". The rate
        // is a straight line in the fill x (a share of the FULL pool): dx/dt = r0 × m × (1 − (1 − k) × x),
        // k = RegenRateNearFullPercent / 100, m = the effort multiplier. r0 is chosen so empty → full at a
        // walk still takes T = FullRegenSecondsStanding: ∫0..1 dx / (r0 (1 − a x)) = ln(1/k) / (a r0) = T,
        // a = 1 − k. Each step is integrated EXACTLY (the effort is sampled once per step, so m is constant
        // inside it): x(t) = x0 + (1 − a x0) × (1 − e^(−a r0 m t)) / a - the step length never changes the
        // result, and k = 1 is the old flat rule to the bit (x0 + r0 m t, r0 = 1/T).

        /// <summary>r0: the refill rate of an EMPTY bar at a walk or slower, pool fractions per second, so
        /// that empty → full takes exactly FullRegenSecondsStanding (T): ln(1/k) / ((1 − k) × T); k = 1 →
        /// 1/T (flat). T ≤ 0 → 0 (no refill).</summary>
        public static double RegenRateAtEmpty(in AthleticsRules r)
        {
            if (!(r.FullRegenSecondsStanding > 0)) return 0;
            double k = r.RegenNearFullShare;
            double a = 1.0 - k;
            if (a <= FlatCurve) return 1.0 / r.FullRegenSecondsStanding;
            return Math.Log(1.0 / k) / (a * r.FullRegenSecondsStanding);
        }

        /// <summary>The curve at fill <paramref name="fraction"/> (a share of the FULL pool, clamped to 0..1):
        /// 1 − (1 − k) × x - 1 at empty, k at full; always 1 when k = 1.</summary>
        public static double RegenCurve(in AthleticsRules r, double fraction) => 1.0 - (1.0 - r.RegenNearFullShare) * Clamp01(fraction);

        /// <summary>The fill after <paramref name="seconds"/> of refill from <paramref name="from"/> at the
        /// effort multiplier <paramref name="mult"/> - exact (see the curve's comment); not capped (the
        /// caller stops at the top).</summary>
        public static double RefillFrom(in AthleticsRules r, double from, double seconds, double mult)
        {
            double c = RegenRateAtEmpty(in r) * mult;
            if (!(c > 0) || !(seconds > 0)) return from;
            double a = 1.0 - r.RegenNearFullShare;
            if (a <= FlatCurve) return from + c * seconds;
            return from + (1.0 - a * from) * (1.0 - Math.Exp(-a * c * seconds)) / a;
        }

        /// <summary>Seconds of refill from <paramref name="from"/> up to <paramref name="to"/> at the effort
        /// multiplier <paramref name="mult"/> - exact; 0 when <paramref name="to"/> ≤ <paramref name="from"/>,
        /// +∞ when nothing refills (T ≤ 0 or the multiplier 0). At a walk (mult 1) 0 → 1 is FullRegenSecondsStanding.</summary>
        public static double RefillSeconds(in AthleticsRules r, double from, double to, double mult)
        {
            if (to <= from) return 0;
            double c = RegenRateAtEmpty(in r) * mult;
            if (!(c > 0)) return double.PositiveInfinity;
            double a = 1.0 - r.RegenNearFullShare;
            if (a <= FlatCurve) return (to - from) / c;
            return Math.Log((1.0 - a * from) / (1.0 - a * to)) / (a * c);
        }

        /// <summary>1 − k at or below this is the flat refill (k = 1: RegenRateNearFullPercent 100).</summary>
        private const double FlatCurve = 1e-12;

        // ------------------------------------------------------------------ speed plumbing

        /// <summary>Exhausted now (E = 0). Off → never.</summary>
        public static bool IsExhausted(in AthleticsRules r, Fighter f) => r.Enabled && f.Exhausted;

        /// <summary>True when <paramref name="desired"/> differs enough from what is applied to be
        /// worth a recompute: a step of <see cref="SpeedUpdateStep"/>, or any move to or from exactly 1,
        /// or onto / off exactly <paramref name="floor"/> (the curve's end points).</summary>
        public static bool SpeedUpdateNeeded(float applied, float desired, float floor)
        {
            if (applied == desired) return false;
            return Math.Abs(applied - desired) >= SpeedUpdateStep
                   || (applied == 1f) != (desired == 1f)
                   || (applied == floor) != (desired == floor);
        }

        // ------------------------------------------------------------------ reads

        /// <summary>Points left now (off → a full pool).</summary>
        public static double Points(in AthleticsRules r, Fighter f) => (r.Enabled ? f.Fraction : 1.0) * PoolPoints(in r, f);

        public static AthleticsReading Read(in AthleticsRules r, Fighter f)
        {
            double pool = PoolPoints(in r, f);
            if (!r.Enabled)
                return new AthleticsReading(false, pool, pool, pool, 1.0, 1.0, 1.0, r.PeakFraction, false, f.IsHero, f.IsLeader,
                    f.SpeedMultiplier, f.RunSpeedMultiplier, f.MountSpeedMultiplier, f.AthleticsSkill);
            double usable = UsableFraction(in r, f);
            return new AthleticsReading(true, f.Fraction * pool, pool, usable * pool, f.Fraction, usable, PeakShare(in r, f), r.PeakFraction,
                f.Exhausted, f.IsHero, f.IsLeader, f.SpeedMultiplier, f.RunSpeedMultiplier, f.MountSpeedMultiplier, f.AthleticsSkill);
        }

        // ------------------------------------------------------------------ changes

        /// <summary>
        /// One blow: take <see cref="BlowCostPoints"/> POINTS from the pool (cost ÷ pool of the
        /// fraction), never below 0; at 0 the fighter is exhausted. Any blow restarts the regen delay
        /// and ends a refill run. Off → nothing happens (<see cref="BlowOutcome.Charged"/> false).
        /// </summary>
        public static BlowOutcome Charge(Fighter f, in AthleticsRules r, double now)
        {
            if (!r.Enabled) return default;
            f.Blows++;
            return Drain(f, in r, now, BlowCostPoints(in r, f));
        }

        /// <summary>
        /// One kick or shield bash (step 18, DESIGN §2 "What is a blow"): take
        /// <see cref="KickOrBashCostPoints"/> POINTS, exactly like a blow otherwise - never below 0, at 0 the
        /// fighter is exhausted, the regen delay restarts, a refill run ends. Counted in
        /// <see cref="Fighter.KicksAndBashes"/>, never in <see cref="Fighter.Blows"/>. Nothing happens
        /// (<see cref="BlowOutcome.Charged"/> false) while Athletics is off or the cost is 0 (free, as before
        /// step 18 - not even the regen delay). The attack timer is the caller's business: a kick or bash
        /// never starts one.
        /// </summary>
        public static BlowOutcome ChargeKickOrBash(Fighter f, in AthleticsRules r, double now)
        {
            if (!r.Enabled) return default;
            double cost = KickOrBashCostPoints(in r, f);
            if (!(cost > 0)) return default;
            f.KicksAndBashes++;
            return Drain(f, in r, now, cost);
        }

        /// <summary>
        /// One blocked blow, paid by the DEFENDER (step 22, DESIGN §2 "Defending costs too"): take
        /// <see cref="BlockCostPoints"/> for its kind, exactly like a kick otherwise - never below 0, at 0 the fighter
        /// is exhausted, the regen delay restarts (a block is effort, as a kick is), a refill run ends. Counted in
        /// <see cref="Fighter.BlocksPaid"/>, never in <see cref="Fighter.Blows"/>. Nothing happens
        /// (<see cref="BlowOutcome.Charged"/> false) while Athletics is off, the kind is None or its cost is 0 (free -
        /// not even the regen delay). Once per blocked blow is the caller's business (<see cref="BlockTracker"/>); a
        /// block never starts an attack pause, a step back or a damage roll.
        /// </summary>
        public static BlowOutcome ChargeBlock(Fighter f, in AthleticsRules r, BlockKind kind, double now)
        {
            if (!r.Enabled || kind == BlockKind.None) return default;
            double cost = BlockCostPoints(in r, f, kind);
            if (!(cost > 0)) return default;
            f.BlocksPaid++;
            return Drain(f, in r, now, cost);
        }

        /// <summary>Take <paramref name="cost"/> points (a blow's, a kick's or a block's) - the shared body of the charges.</summary>
        private static BlowOutcome Drain(Fighter f, in AthleticsRules r, double now, double cost)
        {
            double pool = PoolPoints(in r, f);
            double multiplier = CostMultiplier(in r, f);
            double before = f.Fraction;
            double shareBefore = PeakShareOf(in r, before);
            double after = before - cost / pool;
            if (after <= Epsilon) after = 0;
            if (after > 1) after = 1;

            f.Fraction = after;
            f.LastBlowTime = now;
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
            return new BlowOutcome(true, cost, multiplier, pool, before * pool, after * pool, entered, shareBefore, PeakShareOf(in r, after));
        }

        /// <summary>
        /// The health cap (DESIGN §2): records the health left (0..1) and, while HealthCapsAthletics
        /// is on, pulls the fraction down to it at once. Returns the fraction CUT (0 when nothing
        /// changed). The peak line stays on the FULL pool, so f can never climb above
        /// health ÷ peak line again. Off (either switch or the cap) → only records the health.
        /// </summary>
        public static double ApplyHealth(Fighter f, in AthleticsRules r, double health)
        {
            f.Health = double.IsNaN(health) ? 1.0 : Clamp01(health);
            if (!r.Enabled || !r.HealthCaps) return 0;
            if (f.Fraction <= f.Health + Epsilon) return 0;
            double cut = f.Fraction - f.Health;
            f.Fraction = f.Health;
            if (f.Fraction < f.LowestFraction) f.LowestFraction = f.Fraction;
            return cut;
        }

        /// <summary>
        /// A fresh start (step 19, DESIGN §2 "A fresh start in a hideout's boss fight"): the fighter refills AT
        /// ONCE to the top he can refill to - a full bar, or the health left while HealthCapsAthletics is on (the
        /// health is recorded first, so the cap is today's; wounds still cap it) - and starts over: not
        /// exhausted, no refill run, no regen delay. The peak line stays on the full pool, so a badly wounded
        /// man still cannot reach full strength. The attack pause, holds and step backs are the caller's. Off
        /// (either switch) → nothing (<see cref="FreshStartOutcome.Refilled"/> false).
        /// </summary>
        public static FreshStartOutcome FreshStart(Fighter f, in AthleticsRules r, double health)
        {
            if (!r.Enabled) return default;
            f.Health = double.IsNaN(health) ? 1.0 : Clamp01(health);
            double top = UsableFraction(in r, f);
            double before = f.Fraction;
            bool wasExhausted = f.Exhausted;
            f.Fraction = top;
            if (top < f.LowestFraction) f.LowestFraction = top;
            f.Exhausted = false;
            f.Regenerating = false;
            f.LastBlowTime = double.NegativeInfinity;
            return new FreshStartOutcome(true, PoolPoints(in r, f), before, top, wasExhausted, PeakShareOf(in r, before), PeakShareOf(in r, top));
        }

        /// <summary>
        /// Regeneration over the step (<paramref name="now"/> − <paramref name="dt"/>, <paramref name="now"/>]:
        /// only the part after <c>last blow + RegenDelaySeconds</c> refills, along the refill curve
        /// (<see cref="RefillFrom"/> - exact; the effort sampled once per step), never above the top -
        /// the full pool, or the health left under the cap. Then the "left 0" check. Off → nothing.
        /// </summary>
        public static RegenOutcome Regen(Fighter f, in AthleticsRules r, double now, double dt, float speed, float topSpeed)
        {
            if (!r.Enabled || dt <= 0) return default;
            double effort = Effort(speed, topSpeed);
            double mult = RegenRateMultiplier(in r, effort);
            bool walking = IsWalking(in r, effort);
            double top = UsableFraction(in r, f);
            double gained = 0, used = 0, emptyToPeak = 0;
            bool reachedTop = false, enteredPeak = false;
            double episodeStart = 0, episodeSeconds = 0, episodeWalk = 0, episodeRate = 0;

            double regenFrom = f.LastBlowTime + r.RegenDelaySeconds;
            double effective = now - Math.Max(now - dt, regenFrom);
            if (effective > dt) effective = dt;
            if (effective > 0 && f.Fraction < top - Epsilon)
            {
                double rate = RegenFractionPerSecond(in r, f, speed, topSpeed);
                if (rate > 0)
                {
                    if (!f.Regenerating)
                    {
                        f.Regenerating = true;
                        f.EpisodeStartFraction = f.Fraction;
                        f.EpisodeStartTime = now - effective;
                        f.EpisodeSeconds = 0;
                        f.EpisodeWalkSeconds = 0;
                        f.EpisodeRateSeconds = 0;
                    }
                    double before = f.Fraction;
                    double after = RefillFrom(in r, before, effective, mult);
                    used = effective;
                    if (after >= top - Epsilon)
                    {
                        used = Math.Min(effective, RefillSeconds(in r, before, top, mult));
                        after = top;
                        reachedTop = true;
                    }
                    f.Fraction = after;
                    gained = after - before;
                    enteredPeak = PeakShareOf(in r, before) < 1.0 && PeakShareOf(in r, after) >= 1.0;
                    if (enteredPeak && f.EpisodeStartFraction <= Epsilon) // a run from empty: the time to the line
                        emptyToPeak = f.EpisodeSeconds + Math.Min(used, RefillSeconds(in r, before, r.PeakFraction, mult));
                    f.EpisodeSeconds += used;
                    if (walking) f.EpisodeWalkSeconds += used;
                    f.EpisodeRateSeconds += used * mult;
                    if (reachedTop)
                    {
                        f.Regenerating = false;
                        episodeStart = f.EpisodeStartFraction;
                        episodeSeconds = f.EpisodeSeconds;
                        episodeWalk = f.EpisodeWalkSeconds;
                        episodeRate = f.EpisodeRateSeconds;
                    }
                }
            }

            bool recovered = false;
            double exhaustedFor = 0;
            if (f.Exhausted && f.Fraction > Epsilon)
            {
                f.Exhausted = false;
                recovered = true;
                exhaustedFor = now - f.ExhaustedSince;
            }
            return new RegenOutcome(gained, used, effort, mult, walking, recovered, exhaustedFor, reachedTop, top,
                episodeStart, episodeSeconds, episodeWalk, episodeRate, enteredPeak, emptyToPeak);
        }

        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}

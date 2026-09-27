using System;

namespace TraxCombat.Core
{
    /// <summary>Melee or ranged - the two attack cycles the summary measures apart (step 5e).</summary>
    public enum AttackKind
    {
        Melee = 0,
        Ranged = 1,
    }

    /// <summary>
    /// The phases of one attack cycle on action channel 1 (AI_NOTES "Step 5e"): the wind-up (the
    /// ready animation up to full) and the hold after it (a blow held ready, a bow held drawn - the
    /// aim), the release (swing / loose), a clean release (a swing that hit nothing - the purest
    /// animation length), the recoil after a blocked or parried blow, the reload (nocking, winding a
    /// crossbow) and the pause (anything else) before the next ready.
    /// </summary>
    public enum AttackPhase
    {
        WindUp = 0,
        Held = 1,
        Release = 2,
        CleanRelease = 3,
        Recoil = 4,
        Reload = 5,
        Pause = 6,
    }

    /// <summary>Why a tired AI fighter's swing ended WITHOUT a pace hold (counted, step 5e).</summary>
    public enum PaceNotHeld
    {
        /// <summary>m is 1: nothing to hold.</summary>
        FullStrength = 0,

        /// <summary>The player - never held.</summary>
        Player = 1,

        /// <summary>A rider - never held (his rhythm is the horse's pass).</summary>
        Rider = 2,

        /// <summary>His step back (5d) was just asked for - it holds his attacks itself.</summary>
        SteppingBack = 3,

        /// <summary>No fresh cycle known yet - neither his own nor the mission's.</summary>
        NoReference = 4,

        /// <summary>The next ready had already begun when the swing ended (a chained blow).</summary>
        AlreadyReadied = 5,

        /// <summary>The swing and his next ready already fill the target cycle.</summary>
        NotNeeded = 6,

        Count = 7,
    }

    /// <summary>Why a queued hold was not started by the tick (step 5e).</summary>
    public enum PaceRefusal
    {
        /// <summary>He left the field, or the mission closed the holds.</summary>
        Gone = 0,

        /// <summary>Not AI-controlled (the player took him).</summary>
        NotAi = 1,

        /// <summary>The game has a job for him: a scripted frame, a game object, a ladder, a detachment - or he mounted.</summary>
        Busy = 2,

        /// <summary>NoAttack was already set by someone else.</summary>
        AlreadyNoAttack = 3,

        /// <summary>More starts in one tick than <see cref="AttackRateMath.MaxHoldStartsPerTick"/>.</summary>
        TickBudget = 4,

        /// <summary>The flag did not stick.</summary>
        EngineIgnored = 5,

        /// <summary>An exception in our code (released at once).</summary>
        Error = 6,

        /// <summary>The target time had already passed when the tick came.</summary>
        TooLate = 7,

        Count = 8,
    }

    /// <summary>Why a running hold ended (step 5e).</summary>
    public enum PaceEnd
    {
        TimeUp = 0,

        /// <summary>A swing started anyway - NoAttack did not hold it (expected 0).</summary>
        SwingStarted = 1,

        /// <summary>ModEnabled, AthleticsEnabled or AttackRatePaceHold switched off.</summary>
        SwitchedOff = 2,

        LeftField = 3,
        MissionEnd = 4,

        /// <summary>The player took him over.</summary>
        PlayerControl = 5,

        Mounted = 6,
        Error = 7,
        Count = 8,
    }

    /// <summary>What one attempt to lift a hold found (step 5e).</summary>
    public enum PaceRelease
    {
        /// <summary>We cleared NoAttack.</summary>
        ClearedByUs = 0,

        /// <summary>NoAttack was already gone (the game cleared it with a job of its own).</summary>
        ClearedByGame = 1,

        /// <summary>A game job is on him now: we wait (never cancel it), and clear NoAttack once he is free.</summary>
        Waiting = 2,

        /// <summary>He is gone - nothing to do.</summary>
        Gone = 3,
    }

    /// <summary>
    /// The attack-rate settings for ONE decision, read live from <see cref="TraxSettings"/> (step 5e;
    /// DESIGN §2 "Attack speed" = the whole attack cycle follows m). A struct, built where needed.
    /// </summary>
    public readonly struct AttackRateRules
    {
        public AttackRateRules(bool modEnabled, bool athleticsEnabled, bool aiDecisions, bool paceHold)
        {
            ModEnabled = modEnabled;
            AthleticsEnabled = athleticsEnabled;
            AiDecisions = aiDecisions;
            PaceHold = paceHold;
        }

        public bool ModEnabled { get; }

        public bool AthleticsEnabled { get; }

        /// <summary>AttackRateAiDecisions (the switch itself).</summary>
        public bool AiDecisions { get; }

        /// <summary>AttackRatePaceHold (the switch itself).</summary>
        public bool PaceHold { get; }

        /// <summary>Athletics is live (the master switch first).</summary>
        public bool Enabled => ModEnabled && AthleticsEnabled;

        /// <summary>The AI's attack decisions follow m now.</summary>
        public bool AiDecisionsOn => Enabled && AiDecisions;

        /// <summary>Pace holds may run now.</summary>
        public bool PaceOn => Enabled && PaceHold;

        /// <summary>Which switch holds the pace hold off ("ModEnabled", "AthleticsEnabled", "AttackRatePaceHold"), null while on.</summary>
        public string? PaceOffBecause => !ModEnabled ? "ModEnabled" : !AthleticsEnabled ? "AthleticsEnabled" : !PaceHold ? "AttackRatePaceHold" : null;

        public static AttackRateRules From(TraxSettings s) => new AttackRateRules(s.ModEnabled, s.AthleticsEnabled, s.AttackRateAiDecisions, s.AttackRatePaceHold);

        /// <summary>The settings sentence of the mission-start line and the summary.</summary>
        public string Describe()
        {
            if (!ModEnabled) return "OFF - the whole mod is switched off (ModEnabled): vanilla attack rate";
            if (!AthleticsEnabled) return "OFF (AthleticsEnabled): vanilla attack rate";
            return "ON - animations x m always (swing, thrust / bow draw / throw, reload); AI decisions (AttackRateAiDecisions) "
                   + (AiDecisions ? "on: the chance to attack, to riposte and to loose x m, the aim before a shot ÷ m" : "off")
                   + "; pace hold (AttackRatePaceHold) "
                   + (PaceHold ? "on: after a melee swing a tired AI fighter on foot holds his next attack, guard up, until his fresh cycle ÷ m has passed" : "off")
                   + "; blocking and the recoil after a block: untouched";
        }
    }

    /// <summary>
    /// Step 5e as pure functions (AI_NOTES "Step 5e"): how the AI's decision values follow m, the
    /// pace hold's target, which intervals count as a fighting rhythm, and the summary's verdict.
    /// </summary>
    public static class AttackRateMath
    {
        /// <summary>Measured ÷ target within ±15% = "on target" (the brief's tolerance; log only).</summary>
        public const double VerdictTolerance = 0.15;

        /// <summary>A melee cycle (release to release) longer than this ÷ m is a pause, not a fighting
        /// rhythm: left out of the averages and of the fresh reference. Log / reference plumbing, like
        /// step 5c's 30 s interval cap.</summary>
        public const double MeleeCycleCapSeconds = 4.0;

        /// <summary>The same for a ranged cycle (shot to shot - a crossbow reloads for seconds).</summary>
        public const double RangedCycleCapSeconds = 12.0;

        /// <summary>A hold shorter than this is not worth two flag writes (engine plumbing).</summary>
        public const double MinHoldSeconds = 0.1;

        /// <summary>The mission's AI fresh cycle stands in for a fighter's own only after this many samples.</summary>
        public const int MinMissionFreshSamples = 5;

        /// <summary>The summary judges a group only with this many cycles at the peak (its reference)…</summary>
        public const int MinFreshCycles = 5;

        /// <summary>…and a band with this many.</summary>
        public const int MinBandCycles = 3;

        /// <summary>A ready whose progress reaches this is fully wound up (the rest of it is the hold / aim).</summary>
        public const float ReadyFullProgress = 0.98f;

        /// <summary>At most this many holds start in one tick (plumbing, like the step back's 20).</summary>
        public const int MaxHoldStartsPerTick = 100;

        /// <summary>A hold waiting for a game job to end is re-checked this often (seconds).</summary>
        public const double WaitingCheckSeconds = 0.25;

        /// <summary>Under a plain scripted frame (no object, no ladder, no walk to an object) our NoAttack
        /// is lifted anyway after this long: such a job (a strategic area, a duel set-up, a swim)
        /// may want its man to fight - the step back's 1.5 s frame ends before it (plumbing).</summary>
        public const double WaitingMaxSecondsUnderAFrame = 3.0;

        /// <summary>m kept inside 0.01..1 (an animation must run; nothing may divide by 0).</summary>
        public static float SafeM(float m) => float.IsNaN(m) ? 1f : Math.Max(0.01f, Math.Min(1f, m));

        /// <summary>An AI chance (to attack, to riposte, to loose) at m: value × m.</summary>
        public static float ScaleChance(float value, float m) => value * SafeM(m);

        /// <summary>An AI wait (the aim before a shot) at m: value ÷ m (0 stays 0).</summary>
        public static float ScaleWait(float value, float m) => value / SafeM(m);

        /// <summary>The cap on a cycle, phase or pause at m (<see cref="MeleeCycleCapSeconds"/> or
        /// <see cref="RangedCycleCapSeconds"/> ÷ m): longer = not a fighting rhythm.</summary>
        public static double CycleCap(AttackKind kind, double m) =>
            (kind == AttackKind.Melee ? MeleeCycleCapSeconds : RangedCycleCapSeconds) / SafeM((float)m);

        /// <summary>A duration at m that belongs to a fighting rhythm (0 up to the cap).</summary>
        public static bool Countable(AttackKind kind, double seconds, double m) =>
            !double.IsNaN(seconds) && seconds >= 0 && seconds <= CycleCap(kind, m);

        /// <summary>The target cycle at m: the fresh cycle ÷ m (1 s fresh at m 0.5 → 2 s).</summary>
        public static double TargetCycle(double freshCycle, float m) => freshCycle / SafeM(m);

        /// <summary>
        /// The fresh cycle a hold aims from: the fighter's OWN average cycle while m was 1 when he has
        /// one, else the mission's AI average once it has <see cref="MinMissionFreshSamples"/>
        /// samples, else NaN (no hold).
        /// </summary>
        public static double FreshReference(double ownSum, int ownCount, double missionMean, int missionCount, out bool own)
        {
            own = ownCount > 0 && ownSum > 0;
            if (own) return ownSum / ownCount;
            return missionCount >= MinMissionFreshSamples && missionMean > 0 ? missionMean : double.NaN;
        }

        /// <summary>His next ready, expected from his last one (struck at <paramref name="lastAsked"/>),
        /// at m now: a wind-up scales with 1/m. No ready known → 0.</summary>
        public static double ExpectedReady(double lastReady, float lastAsked, float m) =>
            lastReady > 0 ? lastReady * SafeM(lastAsked) / SafeM(m) : 0;

        /// <summary>
        /// When his next ready may begin so that his next release comes no sooner than
        /// <paramref name="freshCycle"/> ÷ m after <paramref name="releaseStart"/>:
        /// release start + fresh ÷ m − the ready he is expected to need.
        /// </summary>
        public static double HoldUntil(double releaseStart, double freshCycle, float m, double expectedReady) =>
            releaseStart + TargetCycle(freshCycle, m) - Math.Max(0, expectedReady);

        /// <summary>A hold is worth starting: at least <see cref="MinHoldSeconds"/> left.</summary>
        public static bool HoldNeeded(double holdUntil, double now) => holdUntil - now >= MinHoldSeconds;

        /// <summary>measured ÷ target → "on target" (±15%), "too fast" (shorter cycles) or "too slow".</summary>
        public static string Verdict(double ratio) =>
            double.IsNaN(ratio) ? "no verdict" : ratio < 1.0 - VerdictTolerance ? "too fast" : ratio > 1.0 + VerdictTolerance ? "too slow" : "on target";
    }
}

using System;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 13 - PAUSE ONLY (Anton's playtest call, DESIGN §2): every attack animation plays at full
    /// speed; the slow-down is a NO-ATTACK TIMER after each attack. After an attack of duration D (its
    /// own wind-up + release; ranged: + the reload that follows) that ends at attack multiplier m, the
    /// fighter may not start another attack for D × (1/m − 1) - so the attacking part of his rhythm runs
    /// at × m (m 0.5: pause = D; m 0.2: pause = 4 × D). Pure functions (AI_NOTES "Step 13"). Step 20b
    /// (AI_NOTES "Step 20b"): the animations slow a little in a straight line by f (AttackAnimationMinPercent,
    /// 85% at empty); D is measured at FULL animation speed, so the pause in seconds is unchanged.
    /// </summary>
    public static class AttackTimerMath
    {
        /// <summary>A timer shorter than this is not started (AI: not worth two flag writes; you: it would
        /// swallow a chained blow just below the peak line for nothing you could see). Plumbing.</summary>
        public const double MinTimerSeconds = 0.1;

        /// <summary>The flash when you press attack too early: this many pulses…</summary>
        public const int FlashPulses = 2;

        /// <summary>…each lit this long (seconds)…</summary>
        public const double FlashOnSeconds = 0.12;

        /// <summary>…with this gap between them (a UI constant, like the bar's colours).</summary>
        public const double FlashOffSeconds = 0.08;

        /// <summary>The whole flash, first light to last dark (0.32 s).</summary>
        public static double FlashSeconds => FlashPulses * (FlashOnSeconds + FlashOffSeconds) - FlashOffSeconds;

        /// <summary>The pause after an attack of <paramref name="duration"/> seconds that ended at attack
        /// multiplier <paramref name="m"/>: D × (1/m − 1). 0 at full strength (m ≥ 1) or for no duration.</summary>
        public static double Pause(double duration, float m)
        {
            if (double.IsNaN(duration) || duration <= 0 || float.IsNaN(m) || m >= 1f) return 0;
            return duration * (1.0 / AttackRateMath.SafeM(m) - 1.0);
        }

        /// <summary>A pause long enough to start a timer for.</summary>
        public static bool Worth(double pause) => pause >= MinTimerSeconds;

        /// <summary>
        /// The attack ANIMATION multiplier (swing, thrust / draw / throw, reload) - step 20b, Anton after his
        /// playtest of 2026-09-28 ("swing speed does get reduced but to 85%"): a STRAIGHT LINE by the peak
        /// share f, like the run speed - A + (1 − A) × f, A = <paramref name="minPercent"/> / 100: full speed at
        /// and above the peak line (f 1), A at empty (f 0), 0.85 → 0.925 halfway. 100 = never slowed (step 13's
        /// full-speed animations). The pure line at 0 reaches 0 at empty, so the setting's range stops at 5
        /// (an animation never freezes). Steps 13-20 used max(m, A) - with 85 that sits at 85% almost at once
        /// (m 0.2 + 0.8 f reaches 0.85 at f ≈ 0.81).
        /// </summary>
        public static float AnimationMultiplier(double peakShare, int minPercent)
        {
            if (double.IsNaN(peakShare) || peakShare >= 1.0) return 1f;
            float floor = Math.Max(0, Math.Min(100, minPercent)) / 100f;
            return AthleticsMath.Curve(floor, peakShare);
        }

        /// <summary>
        /// The f an APPLIED attack multiplier m stands for: (m − S) / (1 − S), S = the attack speed floor
        /// (<see cref="AthleticsRules.AttackSpeedFloor"/>), clamped to 0..1. The stat decorator knows only the m
        /// it applies (0.05-stepped), and the animation line follows that same f - so it moves in the same
        /// recompute steps and every recompute the game does on its own applies the same value. m ≥ 1, or S ≥ 1
        /// (ExhaustedAttackSpeedPercent 100: attacks never slowed) → 1: the animations ride on the attack
        /// slow-down and stay at full speed while it is off.
        /// </summary>
        public static double PeakShareOfAttack(float m, float attackFloor)
        {
            if (float.IsNaN(m) || m >= 1f || float.IsNaN(attackFloor) || attackFloor >= 1f) return 1.0;
            double f = (m - (double)attackFloor) / (1.0 - attackFloor);
            return f <= 0 ? 0 : f >= 1 ? 1 : f;
        }

        /// <summary>The animation multiplier of a fighter whose APPLIED attack multiplier is
        /// <paramref name="m"/> (the stat decorator, the phases, the logs): <see cref="AnimationMultiplier"/> at
        /// <see cref="PeakShareOfAttack"/>.</summary>
        public static float AnimationForAttack(float m, float attackFloor, int minPercent) =>
            AnimationMultiplier(PeakShareOfAttack(m, attackFloor), minPercent);

        /// <summary>
        /// Step 20b: seconds an attack phase took at animation multiplier <paramref name="animation"/>, as they
        /// would be at FULL animation speed (played × animation). D is built from these, so the pause
        /// D × (1/m − 1) stays what it was with full-speed animations - the slower swing only adds its own extra
        /// time to the cycle, never a longer pause. 1 (or anything odd) = the played seconds.
        /// </summary>
        public static double AtFullSpeed(double played, float animation) =>
            float.IsNaN(animation) || animation <= 0f || animation >= 1f ? played : played * animation;

        /// <summary>Seconds left of a timer ending at <paramref name="end"/> (0 once it is over).</summary>
        public static double Remaining(double now, double end) => end > now ? end - now : 0;

        /// <summary>
        /// The countdown's text, "1.3 s": tenths rounded UP, so it never reads "0.0 s" while it still runs
        /// (1.32 s left shows "1.4 s", 0.01 s shows "0.1 s"); the exact numbers are in the log.
        /// </summary>
        public static string CountdownText(double remaining)
        {
            if (double.IsNaN(remaining) || remaining <= 0) return string.Empty;
            double tenths = Math.Ceiling(remaining * 10.0 - 1e-6) / 10.0;
            if (tenths < 0.1) tenths = 0.1;
            return tenths.ToString("0.0", CultureInfo.InvariantCulture) + " s";
        }

        /// <summary>The Attack recovery bar's fill while it refills (amber) and when full / idle (steel) and
        /// its flash overlay - fixed like the Athletics bar's colours (MCM has no colour picker).</summary>
        public const string RecoveringHex = "#E8A33DFF";

        public const string ReadyHex = "#8FA9BDD9";

        public const string FlashHex = "#FFF6DCE6";

        /// <summary>The recovery bar's word.</summary>
        public const string RecoveryLabelText = "Attack recovery";

        /// <summary>The flash is lit <paramref name="sinceStart"/> seconds after it started
        /// (<see cref="FlashPulses"/> pulses of <see cref="FlashOnSeconds"/>).</summary>
        public static bool FlashOn(double sinceStart)
        {
            if (double.IsNaN(sinceStart) || sinceStart < 0 || sinceStart >= FlashSeconds) return false;
            double period = FlashOnSeconds + FlashOffSeconds;
            double into = sinceStart - Math.Floor(sinceStart / period) * period;
            return into < FlashOnSeconds;
        }
    }

    /// <summary>
    /// What the Attack recovery bar shows for the player at one moment (step 13, Anton: "a bar 'attack
    /// recovery' that empties when I attack and until it fills I can't attack; inside it the secs delay
    /// added"): EMPTY from his attack's release (when a pause will follow) to its end, then filling over
    /// the pause with the seconds left inside it; FULL with no text when no pause runs. A snapshot struct.
    /// </summary>
    public readonly struct AttackRecoveryReading
    {
        public AttackRecoveryReading(bool timerOn, bool inAttack, bool running, double remaining, double pause, double duration, float m, double flashStartedAt, bool flashLit)
        {
            TimerOn = timerOn;
            InAttack = inAttack;
            Running = running;
            Remaining = remaining;
            Pause = pause;
            Duration = duration;
            M = m;
            FlashStartedAt = flashStartedAt;
            FlashLit = flashLit;
        }

        /// <summary>AttackRatePlayerTimer on (with the master switch and Athletics).</summary>
        public bool TimerOn { get; }

        /// <summary>His attack runs and a pause will follow it (the bar is empty).</summary>
        public bool InAttack { get; }

        /// <summary>The pause runs (the bar fills).</summary>
        public bool Running { get; }

        public double Remaining { get; }

        public double Pause { get; }

        /// <summary>The attack's D and m the pause came from.</summary>
        public double Duration { get; }

        public float M { get; }

        /// <summary>When the last flash started (NaN = never) - a new value = a new flash.</summary>
        public double FlashStartedAt { get; }

        /// <summary>The flash is lit now (before FlashBarOnEarlyAttack).</summary>
        public bool FlashLit { get; }

        /// <summary>The recovered share 0..1: 0 during the attack, rising over the pause, 1 otherwise.</summary>
        public float Share
        {
            get
            {
                if (Running && Pause > 0) return (float)Math.Max(0.0, Math.Min(1.0, 1.0 - Remaining / Pause));
                return InAttack ? 0f : 1f;
            }
        }

        /// <summary>"1.3 s" while the pause runs, empty otherwise.</summary>
        public string SecondsText => Running ? AttackTimerMath.CountdownText(Remaining) : string.Empty;

        /// <summary>The bar is not full (it shows the recovering colour).</summary>
        public bool Recovering => Running || InAttack;

        public static AttackRecoveryReading From(PlayerAttackTimer t, double now, bool timerOn) =>
            new AttackRecoveryReading(timerOn, timerOn && t.InAttack, timerOn && t.Running, t.Remaining(now), t.Pause, t.Duration, t.M, t.FlashStartedAt, t.FlashLit(now));

        /// <summary>No timer (not tracked as the one holding it, or switched off): full.</summary>
        public static AttackRecoveryReading Full(bool timerOn) =>
            new AttackRecoveryReading(timerOn, false, false, 0, 0, 0, 1f, double.NaN, false);
    }

    /// <summary>Why the player's hold ended before (or instead of) its countdown's natural end.</summary>
    public enum PlayerTimerEnd
    {
        /// <summary>The countdown ran out.</summary>
        TimeUp = 0,

        /// <summary>His hold from the swing's start ended with the attack: its timer came out below
        /// <see cref="AttackTimerMath.MinTimerSeconds"/> (no countdown).</summary>
        TooShort = 1,

        /// <summary>ModEnabled, AthleticsEnabled or AttackRatePlayerTimer switched off.</summary>
        SwitchedOff = 2,

        /// <summary>He is no longer the one you control (another agent, or he left the field).</summary>
        NotYou = 3,

        MissionEnd = 4,

        /// <summary>Step 19: your side's fresh start (a hideout's boss fight began) - the pause is over.</summary>
        FreshStart = 5,

        Count = 6,
    }

    /// <summary>What one frame of the player's input gate decided (<see cref="PlayerAttackTimer.Frame"/>).</summary>
    public readonly struct PlayerGateFrame
    {
        public PlayerGateFrame(bool clear, bool swallowed, bool inAttack, bool flashStarted, bool ended, bool heldAtEnd)
        {
            Clear = clear;
            Swallowed = swallowed;
            InAttack = inAttack;
            FlashStarted = flashStarted;
            Ended = ended;
            HeldAtEnd = heldAtEnd;
        }

        /// <summary>Clear the attack bits this frame (the button is down and the hold is on).</summary>
        public bool Clear { get; }

        /// <summary>A new press was swallowed this frame.</summary>
        public bool Swallowed { get; }

        /// <summary>…during the attack itself (his swing / loose / reload), not the countdown.</summary>
        public bool InAttack { get; }

        /// <summary>That press started a flash (none was running).</summary>
        public bool FlashStarted { get; }

        /// <summary>The countdown ran out this frame.</summary>
        public bool Ended { get; }

        /// <summary>…with the attack button held: the next attack starts now (hold-to-attack).</summary>
        public bool HeldAtEnd { get; }
    }

    /// <summary>
    /// The PLAYER's no-attack timer (step 13) - pure state, driven by the game side: the attack started
    /// (<see cref="AttackStarted"/>, at the release's start - the hold begins there when its timer is sure
    /// to be worth it, so a click during his own swing cannot chain a blow past it), the attack ended
    /// (<see cref="AttackEnded"/> - the countdown starts: D × (1/m − 1)), and every frame the input gate
    /// asks <see cref="Frame"/> with whether the attack button is down: while the hold is on the answer is
    /// "clear the attack bits" (the engine never sees the press - no wind-up, no stuck state), a NEW press
    /// is swallowed and counted and may start a flash; when the countdown ends with the button still
    /// down, the next frame lets it through - the attack starts at once. Blocking, kicks, moving and
    /// weapon switches are never touched (the gate clears the attack bits only). Main thread.
    /// </summary>
    public sealed class PlayerAttackTimer
    {
        private bool _baseline;

        /// <summary>The attack itself is running (from its release's start to its end) and a timer will follow.</summary>
        public bool InAttack { get; private set; }

        /// <summary>The countdown runs.</summary>
        public bool Running { get; private set; }

        /// <summary>The gate clears the attack bits while this is true.</summary>
        public bool Holding => InAttack || Running;

        /// <summary>The attack button as the gate last saw it (while holding).</summary>
        public bool Pressing { get; private set; }

        public AttackKind Kind { get; private set; }

        /// <summary>The running (or last) timer: when it started and ends, its pause, the attack's D and m.</summary>
        public double TimerStart { get; private set; } = -1;

        public double TimerEnd { get; private set; } = -1;

        public double Pause { get; private set; }

        public double Duration { get; private set; }

        public float M { get; private set; } = 1f;

        /// <summary>The expected pause when the hold began at the release's start.</summary>
        public double ExpectedPause { get; private set; }

        /// <summary>When the last countdown ran out (−1 = none yet), and whether the button was down then.</summary>
        public double EndedAt { get; private set; } = -1;

        public bool HeldAtEnd { get; private set; }

        /// <summary>When the last flash started (NaN = never).</summary>
        public double FlashStartedAt { get; private set; } = double.NaN;

        /// <summary>Presses swallowed during the current (or last) hold.</summary>
        public int SwallowedThisHold { get; private set; }

        /// <summary>
        /// An attack's release began. When the timer it will leave is sure to be worth it
        /// (<paramref name="expectedPause"/> ≥ <see cref="AttackTimerMath.MinTimerSeconds"/>), the hold
        /// begins NOW - from here no press reaches the engine (no chained blow). True when it began.
        /// </summary>
        public bool AttackStarted(double now, AttackKind kind, double expectedPause)
        {
            Kind = kind;
            ExpectedPause = expectedPause;
            if (!AttackTimerMath.Worth(expectedPause)) return false;
            if (!Holding)
            {
                _baseline = true; // whatever the button is doing now is not a new press
                SwallowedThisHold = 0;
            }
            Running = false;
            InAttack = true;
            return true;
        }

        /// <summary>
        /// The attack ended (melee: its release; ranged: the reload after the loose): the countdown of
        /// D × (1/m − 1) starts. False (and any hold from the release's start ends) when that is below
        /// <see cref="AttackTimerMath.MinTimerSeconds"/>. <paramref name="endedEarlyHold"/>: a hold from the
        /// release's start ended here without a countdown (<see cref="PlayerTimerEnd.TooShort"/>).
        /// </summary>
        public bool AttackEnded(double now, AttackKind kind, double duration, float m, out bool endedEarlyHold)
        {
            double pause = AttackTimerMath.Pause(duration, m);
            Kind = kind;
            Duration = duration;
            M = m;
            endedEarlyHold = false;
            if (!AttackTimerMath.Worth(pause))
            {
                endedEarlyHold = InAttack;
                InAttack = false;
                Running = false;
                return false;
            }
            if (!Holding)
            {
                _baseline = true;
                SwallowedThisHold = 0;
            }
            InAttack = false;
            Running = true;
            Pause = pause;
            TimerStart = now;
            TimerEnd = now + pause;
            HeldAtEnd = false;
            return true;
        }

        /// <summary>
        /// One frame of the input gate (called only while <see cref="Holding"/>): <paramref name="pressing"/>
        /// = the attack bits the player's controller wrote this frame. The countdown ends first if its time
        /// is up (then nothing is cleared: a held button starts the attack this very frame).
        /// </summary>
        public PlayerGateFrame Frame(double now, bool pressing)
        {
            bool ended = false;
            if (Running && now >= TimerEnd)
            {
                Running = false;
                ended = true;
                EndedAt = now;
                HeldAtEnd = pressing;
            }
            if (!Holding)
            {
                Pressing = pressing;
                _baseline = false;
                return new PlayerGateFrame(false, false, false, false, ended, ended && pressing);
            }
            if (_baseline)
            {
                _baseline = false;
                Pressing = pressing;
                return new PlayerGateFrame(pressing, false, false, false, ended, false);
            }
            bool rising = pressing && !Pressing;
            Pressing = pressing;
            if (!rising) return new PlayerGateFrame(pressing, false, false, false, ended, false);
            SwallowedThisHold++;
            bool flash = double.IsNaN(FlashStartedAt) || now - FlashStartedAt >= AttackTimerMath.FlashSeconds || now < FlashStartedAt;
            if (flash) FlashStartedAt = now;
            return new PlayerGateFrame(true, true, InAttack, flash, ended, false);
        }

        /// <summary>Seconds left on the countdown (0 while none runs).</summary>
        public double Remaining(double now) => Running ? AttackTimerMath.Remaining(now, TimerEnd) : 0;

        /// <summary>The bar's flash is lit now.</summary>
        public bool FlashLit(double now) => !double.IsNaN(FlashStartedAt) && AttackTimerMath.FlashOn(now - FlashStartedAt);

        /// <summary>Ends the hold at once (switched off, not him any more, mission end). True when one was on.</summary>
        public bool Release()
        {
            bool was = Holding;
            InAttack = false;
            Running = false;
            _baseline = false;
            return was;
        }
    }
}

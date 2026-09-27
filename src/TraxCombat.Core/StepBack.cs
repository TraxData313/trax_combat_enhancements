using System;

namespace TraxCombat.Core
{
    /// <summary>
    /// The step-back settings in effect for ONE decision (DESIGN §2, step 5d), read live from
    /// <see cref="TraxSettings"/> (<see cref="From"/>) - a struct, no allocation, never cached across
    /// a tick, so an MCM change mid-battle applies to the next swing / the next tick.
    /// </summary>
    public readonly struct StepBackRules
    {
        public StepBackRules(bool stepBackEnabled, int maxChancePercent, float distance, float seconds, float enemyRange,
            bool holdAttacks, int maxAtOnce, bool athleticsEnabled = true, bool modEnabled = true)
        {
            ModEnabled = modEnabled;
            AthleticsEnabled = athleticsEnabled;
            StepBackEnabled = stepBackEnabled;
            MaxChancePercent = maxChancePercent;
            Distance = distance;
            Seconds = seconds;
            EnemyRange = enemyRange;
            HoldAttacks = holdAttacks;
            MaxAtOnce = maxAtOnce;
        }

        /// <summary>The mod's master switch (ModEnabled).</summary>
        public bool ModEnabled { get; }

        /// <summary>AthleticsEnabled - the step back follows Athletics (no f without it).</summary>
        public bool AthleticsEnabled { get; }

        /// <summary>StepBackEnabled - its own switch.</summary>
        public bool StepBackEnabled { get; }

        /// <summary>Live: the master switch, Athletics AND the step back are all on. Off → no step back
        /// starts and every running one is released at once.</summary>
        public bool Enabled => ModEnabled && AthleticsEnabled && StepBackEnabled;

        /// <summary>Which switch holds it off, for the log ("ModEnabled", "AthleticsEnabled",
        /// "StepBackEnabled"); null while it is on.</summary>
        public string? OffBecause => !ModEnabled ? "ModEnabled" : !AthleticsEnabled ? "AthleticsEnabled" : !StepBackEnabled ? "StepBackEnabled" : null;

        /// <summary>StepBackMaxChancePercent - the chance at an empty bar (f 0).</summary>
        public int MaxChancePercent { get; }

        /// <summary>StepBackDistance - metres, straight away from the enemy.</summary>
        public float Distance { get; }

        /// <summary>StepBackSeconds - the longest a step back lasts.</summary>
        public float Seconds { get; }

        /// <summary>StepBackEnemyRange - the enemy he fights must be at most this far (m).</summary>
        public float EnemyRange { get; }

        /// <summary>StepBackHoldAttacks - no swings while stepping back (the engine's NoAttack flag).</summary>
        public bool HoldAttacks { get; }

        /// <summary>StepBackMaxAtOnce - the most stepping back at the same time, all sides.</summary>
        public int MaxAtOnce { get; }

        /// <summary>The chance at f 0 as a share, 0..1.</summary>
        public double MaxChance => MaxChancePercent <= 0 ? 0 : MaxChancePercent >= 100 ? 1.0 : MaxChancePercent / 100.0;

        /// <summary>The live values, read now.</summary>
        public static StepBackRules From(TraxSettings s) => new StepBackRules(
            s.StepBackEnabled, s.StepBackMaxChancePercent, s.StepBackDistance, s.StepBackSeconds, s.StepBackEnemyRange,
            s.StepBackHoldAttacks, s.StepBackMaxAtOnce, s.AthleticsEnabled, s.ModEnabled);

        /// <summary>One line for the log: "ON - after a melee swing …" or "OFF (StepBackEnabled)".</summary>
        public string Describe() => Enabled
            ? "ON - after a melee swing an AI fighter on foot steps back with chance " + MaxChancePercent + "% x (1 - f) (0 at full strength), "
              + Distance.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture) + " m straight away from his enemy for up to "
              + Seconds.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture) + " s, only with the enemy within "
              + EnemyRange.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture) + " m, "
              + (HoldAttacks ? "no swings while stepping back" : "swings allowed while stepping back") + ", at most " + MaxAtOnce + " at once"
            : "OFF (" + OffBecause + ")";
    }

    /// <summary>Why a swing that could have been a step back was not even rolled (counted by the
    /// summary - the f bins must hold only fighters who could step back).</summary>
    public enum StepBackNotRolled
    {
        /// <summary>The player's own fighter - never.</summary>
        Player = 0,

        /// <summary>A rider - never.</summary>
        Mounted = 1,

        /// <summary>Not a field battle: a tournament or arena fight, a duel, a naval battle, deployment,
        /// a mission that is ending.</summary>
        MissionKind = 2,

        /// <summary>He swung while already stepping back (the summary's "swings while stepping back").</summary>
        AlreadyStepping = 3,
    }

    /// <summary>Why a step back the dice said yes to did not start (the game's checks, from the next
    /// tick). Each is a safety rule of DESIGN §2 / AI_NOTES "Step 5d".</summary>
    public enum StepBackRefusal
    {
        None = 0,

        /// <summary>Not AI-controlled (the player took him over).</summary>
        NotAiControlled,

        /// <summary>No HumanAIComponent - not a normal AI soldier.</summary>
        NoAiComponent,

        /// <summary>The game's own gate (<c>CanBeAssignedForScriptedMovement</c>) said no: detached
        /// (siege engine, ladder, tower, strategic area), in a ladder queue, using or walking to an
        /// object, running away, or already moved by a script.</summary>
        Busy,

        /// <summary>Routing (<c>IsRetreating</c>).</summary>
        Routing,

        /// <summary>His formation stands in a shield wall, square or circle - they exist to hold.</summary>
        HoldingArrangement,

        /// <summary>His formation is ordered to retreat.</summary>
        RetreatOrder,

        /// <summary>No target, or the target is not an active enemy.</summary>
        NoEnemy,

        /// <summary>The enemy he fights is farther than StepBackEnemyRange.</summary>
        EnemyTooFar,

        /// <summary>The spot behind him is off the navmesh.</summary>
        OffNavMesh,

        /// <summary>The spot is more than <see cref="StepBackMath.MaxHeightStep"/> higher or lower (a
        /// wall edge, stairs).</summary>
        NotLevel,

        /// <summary>No straight walk to the spot (a wall, a fence, a parapet, a narrow gap).</summary>
        Blocked,

        /// <summary>StepBackMaxAtOnce fighters are already stepping back.</summary>
        AtOnceCap,

        /// <summary>Too many starts in one tick (plumbing, <see cref="StepBackMath.MaxStartsPerTick"/>).</summary>
        TickBudget,

        /// <summary>He left the field, or the step back was switched off, between the swing and the tick.</summary>
        Gone,

        /// <summary>Riding by the time of the tick, or the mission kind changed.</summary>
        NoLongerEligible,

        /// <summary>The engine call threw (logged as [error]).</summary>
        EngineError,

        /// <summary>The engine did not take the scripted position (GoToPosition not set right after
        /// the call) - disabled again at once, to be safe.</summary>
        EngineIgnored,
    }

    /// <summary>Why a step back ended. <see cref="None"/> = it goes on.</summary>
    public enum StepBackEnd
    {
        None = 0,

        /// <summary>StepBackSeconds ran out - the normal end ("completed").</summary>
        TimeUp,

        /// <summary>He left the field (killed, knocked out, fled) - no engine call on a removed man.</summary>
        LeftField,

        /// <summary>The mission ended (released through the engine before the summary).</summary>
        MissionEnd,

        /// <summary>ModEnabled, AthleticsEnabled or StepBackEnabled turned off - everyone released at once.</summary>
        SwitchedOff,

        /// <summary>His formation got a new movement order or arrangement.</summary>
        OrderChanged,

        /// <summary>He was moved to another formation (or lost his).</summary>
        FormationChanged,

        /// <summary>He was detached from his formation (a siege detachment) - released, the detachment
        /// drives him from there.</summary>
        Detached,

        /// <summary>The player took him over.</summary>
        PlayerControl,

        /// <summary>He mounted a horse.</summary>
        Mounted,

        /// <summary>He routs.</summary>
        Routing,

        /// <summary>The game gave him a job of its own (an object, a ladder queue, a detachment) - our
        /// record is dropped WITHOUT disabling, so the game's job is never cancelled.</summary>
        HandedOver,

        /// <summary>The game cleared the scripted movement itself before our time was up (nothing to
        /// release).</summary>
        ClearedByGame,

        /// <summary>No longer active (not caught by the removal event).</summary>
        NotActive,

        /// <summary>Our code threw for him - released as a precaution.</summary>
        Error,
    }

    /// <summary>
    /// DESIGN §2's step back as pure functions: the chance from f, the roll, the spot straight away
    /// from the enemy, the facing the engine is asked for, and the facing / movement checks the log
    /// uses. No game types - the module feeds positions as numbers.
    /// </summary>
    public static class StepBackMath
    {
        /// <summary>The spot may be at most this much higher or lower than the man (metres) - a wall
        /// edge (the spot resolves to the ground below) or stairs. A safety guard, not a tuning
        /// number (like the engine plumbing constants of steps 5 and 5c).</summary>
        public const float MaxHeightStep = 1.0f;

        /// <summary>At most this many step backs START in one tick (native navmesh checks and the
        /// scripted-movement call each); the rest of that tick's are refused and counted. Plumbing.</summary>
        public const int MaxStartsPerTick = 20;

        /// <summary>Facing checks: within 60° of the enemy counts as facing him (cos ≥ 0.5); more than
        /// 120° away (cos ≤ −0.5) is a turned back; between is side-on. Log plumbing.</summary>
        public const double FacingCos = 0.5;

        /// <summary>Moving away / toward the enemy: faster than this (m/s) along the line to him.
        /// Log plumbing.</summary>
        public const double MovingSpeed = 0.2;

        /// <summary>Within this of the spot (m) counts as having reached it. Log plumbing.</summary>
        public const double ReachedSlack = 0.35;

        /// <summary>Chance of a step back after a melee swing: StepBackMaxChancePercent × (1 − f), 0..1.
        /// 0 at full strength (f 1), the full chance at an empty bar (f 0). Off → 0.</summary>
        public static double Chance(in StepBackRules r, double peakShare)
        {
            if (!r.Enabled || double.IsNaN(peakShare)) return 0;
            double f = peakShare < 0 ? 0 : peakShare > 1 ? 1 : peakShare;
            double c = r.MaxChance * (1.0 - f);
            return c <= AthleticsMath.Epsilon ? 0 : c > 1 ? 1 : c;
        }

        /// <summary>One roll: true when <paramref name="u"/> (uniform 0..1) falls under the chance.
        /// A chance of 0 is never a yes (no dice needed); a chance of 1 always is.</summary>
        public static bool Roll(double chance, double u) => chance > 0 && (chance >= 1.0 || u < chance);

        /// <summary>The spot <paramref name="distance"/> metres straight away from the enemy, on the
        /// ground plane. False when the two stand on the same point (no direction).</summary>
        public static bool AwayFrom(double x, double y, double enemyX, double enemyY, double distance, out double spotX, out double spotY)
        {
            double dx = x - enemyX, dy = y - enemyY;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (!(len > 1e-4) || double.IsNaN(len))
            {
                spotX = x;
                spotY = y;
                return false;
            }
            spotX = x + dx / len * distance;
            spotY = y + dy / len * distance;
            return true;
        }

        /// <summary>The facing the engine is asked for, in the game's convention
        /// (<c>Vec2.RotationInRadians</c> = atan2(−x, y)): looking along (dx, dy).</summary>
        public static float Radians(double dx, double dy) => (float)Math.Atan2(-dx, dy);

        /// <summary>The direction for a facing in the game's convention (<c>Vec2.FromRotation</c>).</summary>
        public static void FromRadians(double radians, out double dx, out double dy)
        {
            dx = -Math.Sin(radians);
            dy = Math.Cos(radians);
        }

        /// <summary>Cosine of the angle between where he looks and the direction to his enemy, on the
        /// ground plane: 1 = straight at him, −1 = his back to him. NaN when either is zero.</summary>
        public static double FacingCosine(double lookX, double lookY, double toEnemyX, double toEnemyY)
        {
            double a = Math.Sqrt(lookX * lookX + lookY * lookY), b = Math.Sqrt(toEnemyX * toEnemyX + toEnemyY * toEnemyY);
            if (!(a > 1e-6) || !(b > 1e-6)) return double.NaN;
            double c = (lookX * toEnemyX + lookY * toEnemyY) / (a * b);
            return c > 1 ? 1 : c < -1 ? -1 : c;
        }

        /// <summary>0 facing him (within 60°), 1 side-on, 2 back turned (more than 120° away), 3 unknown.</summary>
        public static int FacingBin(double cosine) =>
            double.IsNaN(cosine) ? 3 : cosine >= FacingCos ? 0 : cosine <= -FacingCos ? 2 : 1;

        public static string FacingBinName(int bin) => bin switch
        {
            0 => "facing his enemy",
            1 => "side-on",
            2 => "back turned",
            _ => "no enemy to face",
        };

        /// <summary>The angle in degrees for a facing cosine (for the log), NaN stays NaN.</summary>
        public static double Degrees(double cosine) => double.IsNaN(cosine) ? double.NaN : Math.Acos(cosine) * 180.0 / Math.PI;

        /// <summary>Speed away from the enemy (m/s; negative = toward him) of a velocity, on the ground plane.</summary>
        public static double SpeedAway(double vx, double vy, double toEnemyX, double toEnemyY)
        {
            double b = Math.Sqrt(toEnemyX * toEnemyX + toEnemyY * toEnemyY);
            if (!(b > 1e-6)) return double.NaN;
            return -(vx * toEnemyX + vy * toEnemyY) / b;
        }

        /// <summary>0 moving away, 1 standing (or sideways), 2 moving toward the enemy, 3 unknown.</summary>
        public static int MotionBin(double speedAway) =>
            double.IsNaN(speedAway) ? 3 : speedAway >= MovingSpeed ? 0 : speedAway <= -MovingSpeed ? 2 : 1;

        public static string MotionBinName(int bin) => bin switch
        {
            0 => "moving away",
            1 => "standing or sideways",
            2 => "moving toward him",
            _ => "unknown",
        };

        /// <summary>The spot's height is usable: on the navmesh (not NaN) and within
        /// <see cref="MaxHeightStep"/> of the man's.</summary>
        public static bool LevelEnough(double manZ, double spotZ) => !double.IsNaN(spotZ) && Math.Abs(spotZ - manZ) <= MaxHeightStep;

        /// <summary>The step back has run its time: StepBackSeconds read LIVE (a change mid-step applies).</summary>
        public static bool TimeUp(in StepBackRules r, double startedAt, double now) => now - startedAt >= Math.Max(0.0, r.Seconds);

        /// <summary>Distance on the ground plane.</summary>
        public static double Distance2D(double ax, double ay, double bx, double by)
        {
            double dx = ax - bx, dy = ay - by;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}

using System;

namespace TraxCombat.Core
{
    /// <summary>What one filtered frame of a held AI man's input changed (step 16) - flags, counted by the hook.</summary>
    [Flags]
    public enum InputEdit
    {
        None = 0,

        /// <summary>He wanted to attack: the attack bits were taken out.</summary>
        AttackCleared = 1,

        /// <summary>…and held no guard of his own: a guard was raised instead (AiHoldRaiseGuard).</summary>
        GuardRaised = 2,

        /// <summary>…and already held a guard of his own: it was kept as it was.</summary>
        OwnGuardKept = 4,

        /// <summary>…and he was in a ready: the guard bit CANCELS the ready (clearing the bits alone would release it).</summary>
        ReadyCancelled = 8,
    }

    /// <summary>
    /// Step 16 (AI_NOTES "Step 16"): the AI holds through the AI's own input - the pure part. The game calls
    /// <c>AgentComponent.OnAIInputSet(ref EventControlFlag, ref MovementControlFlag, ref Vec2 inputVector)</c> with the
    /// input the AI just decided, before it is used; a held man's attack bits are taken out there (the guard stays up)
    /// and a man stepping back gets a backwards movement in his own frame. No game types here: the bits are the
    /// engine's <c>Agent.MovementControlFlag</c> values (v1.4.8 - the offline smoke checks them against the game's
    /// enum), the vectors plain numbers.
    /// </summary>
    public static class AiInputMath
    {
        /// <summary><c>MovementControlFlag.AttackMask</c> - AttackLeft | AttackRight | AttackUp | AttackDown.</summary>
        public const uint AttackMask = 0x3C0;

        /// <summary><c>MovementControlFlag.DefendMask</c> - the four block directions + DefendAuto.</summary>
        public const uint DefendMask = 0x7C00;

        /// <summary><c>MovementControlFlag.DefendDown</c> - the raised guard (with a shield: the shield). RTS Camera
        /// Command System cancels an AI attack with exactly "clear AttackMask, set DefendDown".</summary>
        public const uint DefendDown = 0x2000;

        /// <summary>The backwards input written while stepping back: full stick, like a player holding S - the engine's
        /// own backpedal speed (and a tired man's lower top speed) decide how fast. Plumbing.</summary>
        public const double BackpedalInput = 1.0;

        /// <summary>How far further back the ground is checked while backpedalling (m) - navmesh, height step,
        /// straight way. Plumbing, like 5d's <see cref="StepBackMath.MaxHeightStep"/>.</summary>
        public const double EdgeLookAhead = 0.6;

        /// <summary>The ground behind a backpedalling man is checked this often (s). Plumbing.</summary>
        public const double EdgeCheckSeconds = 0.25;

        /// <summary>Every step back is sampled this often (s) - facing, distance - for the log. Plumbing.</summary>
        public const double SampleSeconds = 0.25;

        /// <summary>
        /// One frame of a held man's movement flags (his attacks held - the AI timer or a step back): the attack bits
        /// out, everything else his own. If he wanted to attack and holds no guard of his own, a guard is raised when
        /// <paramref name="raiseGuard"/> is on - and always when he is IN a ready (<paramref name="inReady"/>): attack
        /// bits vanishing mid-ready would RELEASE the blow; with a block pressed the ready is cancelled instead.
        /// A frame without attack bits is returned untouched.
        /// </summary>
        public static uint HoldAttacks(uint flags, bool raiseGuard, bool inReady, out InputEdit edit)
        {
            edit = InputEdit.None;
            if ((flags & AttackMask) == 0) return flags;
            uint f = flags & ~AttackMask;
            edit = InputEdit.AttackCleared;
            if ((f & DefendMask) != 0)
            {
                edit |= InputEdit.OwnGuardKept;
                return f;
            }
            if (inReady)
            {
                edit |= InputEdit.ReadyCancelled;
                return f | DefendDown;
            }
            if (raiseGuard)
            {
                edit |= InputEdit.GuardRaised;
                return f | DefendDown;
            }
            return f;
        }

        /// <summary>A world direction on the ground plane in a man's own input frame (x right, y forward), from his body
        /// frame's side axis (<paramref name="sx"/>, <paramref name="sy"/>) and forward axis - <c>Mat3.TransformToLocal</c>
        /// on the ground plane: (s·v, f·v).</summary>
        public static void ToLocal(double vx, double vy, double sx, double sy, double fx, double fy, out double lx, out double ly)
        {
            lx = sx * vx + sy * vy;
            ly = fx * vx + fy * vy;
        }

        /// <summary>
        /// The backwards input for this frame: the unit world direction AWAY from his enemy (<paramref name="awayX"/>,
        /// <paramref name="awayY"/>) in his own frame, scaled to <paramref name="magnitude"/>. Facing his enemy it is
        /// (0, −magnitude) - a straight backpedal; turned, it becomes a side-step that still follows the checked line.
        /// False (and 0, 0) when a direction or an axis is degenerate.
        /// </summary>
        public static bool BackpedalVector(double awayX, double awayY, double sx, double sy, double fx, double fy, double magnitude, out double lx, out double ly)
        {
            lx = ly = 0;
            double a = Math.Sqrt(awayX * awayX + awayY * awayY);
            double s = Math.Sqrt(sx * sx + sy * sy);
            double f = Math.Sqrt(fx * fx + fy * fy);
            if (!(a > 1e-6) || !(s > 1e-6) || !(f > 1e-6) || double.IsNaN(magnitude)) return false;
            ToLocal(awayX / a, awayY / a, sx / s, sy / s, fx / f, fy / f, out double x, out double y);
            double len = Math.Sqrt(x * x + y * y);
            if (!(len > 1e-6)) return false;
            double k = Math.Max(0, magnitude) / len;
            lx = x * k;
            ly = y * k;
            return true;
        }

        /// <summary>Metres covered from the start along the unit direction (<paramref name="dirX"/>, <paramref name="dirY"/>)
        /// - negative when he was pushed toward his enemy. Sideways drift does not count.</summary>
        public static double Covered(double fromX, double fromY, double x, double y, double dirX, double dirY) =>
            (x - fromX) * dirX + (y - fromY) * dirY;

        /// <summary>The step back has covered its distance (StepBackDistance, read live).</summary>
        public static bool Arrived(double covered, double distance) => !double.IsNaN(covered) && covered >= Math.Max(0, distance);

        /// <summary>The unit direction from a point to another on the ground plane; false when they coincide.</summary>
        public static bool Direction(double fromX, double fromY, double toX, double toY, out double dx, out double dy)
        {
            dx = toX - fromX;
            dy = toY - fromY;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (!(len > 1e-6))
            {
                dx = dy = 0;
                return false;
            }
            dx /= len;
            dy /= len;
            return true;
        }

        /// <summary>When a man's attacks are free again: the later of his AI timer's end and his step back's end
        /// (NaN = that one is not running). NaN when neither runs.</summary>
        public static double LaterEnd(double timerEnd, double stepEnd)
        {
            if (double.IsNaN(timerEnd)) return stepEnd;
            if (double.IsNaN(stepEnd)) return timerEnd;
            return Math.Max(timerEnd, stepEnd);
        }

        /// <summary>A deferred hold (the Legacy timer behind a scripted step back) still has a pause worth setting when
        /// the step back ends at <paramref name="now"/>: at least <see cref="AttackTimerMath.MinTimerSeconds"/> left.</summary>
        public static bool DeferredStillWorth(double until, double now) => until - now >= AttackTimerMath.MinTimerSeconds;

        /// <summary>The technique's word for the log: "input" (the AI's own input, step 16) or the old one.</summary>
        public static string TimerTechnique(bool byInput) => byInput ? "input (the attack bits taken out of the AI's own input)" : "NoAttack (the engine's flag)";

        public static string StepTechnique(bool backpedal) => backpedal ? "backpedal (a backwards input, facing his enemy)" : "scripted walk (SetScriptedPositionAndDirection)";
    }
}

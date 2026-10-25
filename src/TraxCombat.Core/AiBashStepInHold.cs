using System;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 97: what reduced the forward input of one step-in frame (the step-in's stick 1.0 toward his enemy) - the [summary] clause "what held
    /// the step back". Ordered: nothing / the press clamp let it through (step 97) / the press clamp still cut it / the rest clamp, then the STRONGER
    /// wishes that replaced the step-in's vector (in the hook's order).
    /// </summary>
    public enum StepInHold : byte
    {
        /// <summary>Nothing of ours reduced it - the full forward stick went to the engine.</summary>
        None,

        /// <summary>Step 77's press clamp would have taken the forward part out - step 97 let it through (his enemy beyond his stop, the line's front ahead).</summary>
        PressLetThrough,

        /// <summary>The press clamp cut it: his enemy within his stop, or he stands at the enemy line's front (within the margin).</summary>
        PressCut,

        /// <summary>Step 78's rest clamp (a rested man's place after a swap, a resting man held back) cut it.</summary>
        RestClamp,

        // ---- a stronger wish replaced the step-in's vector (the hook's order)
        Backpedal,
        WallPull,
        RestWalk,
        PressWalk,
        Swap,
        Regroup,
        Flock,
        Block,

        Count,
    }

    /// <summary>
    /// Step 97 pure (Toni's T5 battle 2026-10-25 13:24: 172 step-ins, 0 arrived, the gap closed 0.05 m; AI_NOTES "Step 97"): THE STEP-IN THROUGH THE
    /// PRESS CLAMP and the per-frame record of what holds the step back.
    /// <para>THE RULE: while a man's step-in runs (step 95), step 77's press clamp lets the step-in's forward part through - the vector points at HIS OWN
    /// enemy - only while (1) that enemy is farther than his stop (AiBashStepInToMetres 0.95, or a held wall man's leash stop - never nearer than 0.95)
    /// AND (2) he stands more than <see cref="PressFrontMarginMetres"/> in front of the enemy line's front. In words: toward the front he may still walk
    /// min(the enemy's distance − the stop, his distance to the front − the margin) - never past the line's front; beyond it the clamp's protection is
    /// untouched. Only the FRONT clamp; the rear-rank side clamp, the press's walk and every other wish are as before.</para>
    /// </summary>
    public static class AiStepInHoldMath
    {
        /// <summary>The step stops this far short of the enemy line's front: the press refreshes his distance to the front every 0.1 s
        /// (<see cref="FearMath.SteerSeconds"/>) and a guarded walk covers ~0.12 m in that time. Plumbing.</summary>
        public const double PressFrontMarginMetres = 0.15;

        /// <summary>The engine verdict needs this many step-ins with every frame clear (none / let through).</summary>
        public const int VerdictMinSteps = 3;

        /// <summary>Below this speed (m/s) with every wish clear and a full stick, the ENGINE holds him. Plumbing (a guarded walk is ~1 m/s).</summary>
        public const double EngineStuckMetresPerSecond = 0.25;

        /// <summary>The room he still has toward the enemy line (m): min(the enemy's distance − his stop, his distance to the front − the margin);
        /// NaN when any input is unknown.</summary>
        public static double PressRoomMetres(double enemyDistance, double stopAt, double frontDistance)
        {
            if (double.IsNaN(enemyDistance) || double.IsNaN(stopAt) || double.IsNaN(frontDistance)) return double.NaN;
            return Math.Min(enemyDistance - stopAt, frontDistance - PressFrontMarginMetres);
        }

        /// <summary>The press clamp lets the step-in's forward part through: he still has room (<see cref="PressRoomMetres"/> &gt; 0).</summary>
        public static bool PressLetsThrough(double enemyDistance, double stopAt, double frontDistance)
        {
            double room = PressRoomMetres(enemyDistance, stopAt, frontDistance);
            return !double.IsNaN(room) && room > 1e-9;
        }

        /// <summary>The forward part of a vector along the step-in's direction (<paramref name="sx"/>, <paramref name="sy"/> - unit), clamped -1..1;
        /// 0 for a degenerate step.</summary>
        public static double Forward(double vx, double vy, double sx, double sy)
        {
            double len = Math.Sqrt(sx * sx + sy * sy);
            if (!(len > 1e-6)) return 0;
            double f = (vx * sx + vy * sy) / len;
            return f > 1 ? 1 : f < -1 ? -1 : f;
        }

        /// <summary>
        /// One step-in frame's reason: a stronger wish that replaced the vector (<paramref name="winner"/>) first; else the press clamp's cut (its
        /// forward part after the clamp below <paramref name="forwardAfterPress"/> 0.999), the rest clamp's, the press clamp let through, nothing.
        /// </summary>
        public static StepInHold Classify(StepInHold winner, bool pressBit, double forwardAfterPress, bool restBit, double forwardAfterRest, bool letThrough)
        {
            if (winner >= StepInHold.Backpedal && winner < StepInHold.Count) return winner;
            if (pressBit && forwardAfterPress < 0.999) return StepInHold.PressCut;
            if (restBit && forwardAfterRest < 0.999) return StepInHold.RestClamp;
            if (letThrough) return StepInHold.PressLetThrough;
            return StepInHold.None;
        }

        /// <summary>A frame with nothing of ours holding the step back (none, or the press clamp let it through).</summary>
        public static bool Clear(StepInHold h) => h == StepInHold.None || h == StepInHold.PressLetThrough;

        public static bool IsStrongerWish(StepInHold h) => h >= StepInHold.Backpedal && h < StepInHold.Count;

        public static string Name(StepInHold h) => h switch
        {
            StepInHold.None => "nothing",
            StepInHold.PressLetThrough => "the press clamp let it through",
            StepInHold.PressCut => "the press clamp cut it",
            StepInHold.RestClamp => "the rest clamp",
            StepInHold.Backpedal => "the step back's backpedal",
            StepInHold.WallPull => "the wall's pull",
            StepInHold.RestWalk => "a rest's walk",
            StepInHold.PressWalk => "the press's walk",
            StepInHold.Swap => "a swap",
            StepInHold.Regroup => "courage's walk back",
            StepInHold.Flock => "the fear flock",
            StepInHold.Block => "the block hold",
            _ => "?",
        };
    }

    /// <summary>
    /// Step 97 per mission: every step-in frame by what reduced its forward input, and every step-in measured from his positions (his walk toward the
    /// enemy along the first line to him, its seconds, the peak speed between ticks, the mean stick out of the filter); the step-ins whose every frame
    /// was clear answer "does the ENGINE move a guarding AI man forward". Fixed arrays, allocation-free; main thread. Its clause ends step 95's line.
    /// </summary>
    public sealed class AiStepInHoldStats
    {
        private readonly long[] _frames = new long[(int)StepInHold.Count];

        /// <summary>Measured step-ins (a walk with a time), their seconds / metres walked toward the enemy / peak speeds / mean stick out of the filter.</summary>
        public int Steps;
        public double Seconds, Covered, PeakSum, OutSum;

        /// <summary>The measured step-ins whose every frame was clear (nothing of ours held the step back).</summary>
        public int ClearSteps;
        public double ClearSeconds, ClearCovered, ClearPeakSum;

        /// <summary>Step-ins started, and those whose stop the shield wall's leash put beyond AiBashStepInToMetres (a held wall man walks at most the leash).</summary>
        public int Started, LeashShortened;

        public long Frames(StepInHold h) => _frames[(int)h];

        public long TotalFrames
        {
            get
            {
                long n = 0;
                foreach (long v in _frames) n += v;
                return n;
            }
        }

        public void NoteStart(bool leashShortened)
        {
            Started++;
            if (leashShortened) LeashShortened++;
        }

        /// <summary>
        /// One step-in over: its frames by reason (<paramref name="frames"/>, indexed by <see cref="StepInHold"/> - added, not kept), the sum of
        /// the forward stick out of the filter over <paramref name="frameCount"/> frames, and the walk measured from his positions (seconds ≤ 0 =
        /// no measurement - the frames still count).
        /// </summary>
        public void NoteStep(int[] frames, int frameCount, double forwardSum, double seconds, double covered, double peak)
        {
            bool clear = frameCount > 0;
            int n = Math.Min(frames.Length, _frames.Length);
            for (int i = 0; i < n; i++)
            {
                _frames[i] += frames[i];
                if (frames[i] > 0 && !AiStepInHoldMath.Clear((StepInHold)i)) clear = false;
            }
            if (!(seconds > 1e-6) || double.IsNaN(covered)) return;
            Steps++;
            Seconds += seconds;
            Covered += covered;
            PeakSum += double.IsNaN(peak) ? 0 : peak;
            OutSum += frameCount > 0 ? forwardSum / frameCount : 0;
            if (!clear) return;
            ClearSteps++;
            ClearSeconds += seconds;
            ClearCovered += covered;
            ClearPeakSum += double.IsNaN(peak) ? 0 : peak;
        }

        /// <summary>The engine's answer in words (also the first-time line's end for one step-in).</summary>
        public static string EngineVerdict(int clearSteps, double clearSeconds, double clearCovered, double clearPeakSum)
        {
            var c = CultureInfo.InvariantCulture;
            if (clearSteps < AiStepInHoldMath.VerdictMinSteps)
                return "too few step-ins with every wish clear (" + clearSteps + ") to judge the engine";
            double v = clearSeconds > 0 ? clearCovered / clearSeconds : 0;
            string walked = clearSteps + " step-ins with every wish clear (a full forward stick out of our filter) walked " + (clearCovered / clearSteps).ToString("0.00", c)
                            + " m toward the enemy in " + (clearSeconds / clearSteps).ToString("0.00", c) + " s on average (" + v.ToString("0.00", c) + " m/s, peak "
                            + (clearPeakSum / clearSteps).ToString("0.00", c) + " m/s)";
            return v < AiStepInHoldMath.EngineStuckMetresPerSecond
                ? "IT'S THE ENGINE: " + walked + " - nothing of ours held them, the engine itself barely moves a guarding AI man forward (its acceleration from a stand - TopSpeedReachDuration ~2.3-2.8 s on foot -, collision or its own hold)"
                : "the engine moves him: " + walked;
        }

        /// <summary>The clause added to step 95's [summary] line.</summary>
        public string Clause()
        {
            var c = CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder("what held the step back (step 97 - each step-in frame's forward stick toward his enemy): ");
            long total = TotalFrames;
            if (total == 0 && Started == 0) return sb.Append("no step-in frame").ToString();
            sb.Append("frames ").Append(total).Append(" - nothing ").Append(Frames(StepInHold.None));
            if (total > 0) sb.Append(" (").Append((100.0 * Frames(StepInHold.None) / total).ToString("0", c)).Append("%)");
            sb.Append(", the press clamp LET THROUGH (it would have cut - step 97) ").Append(Frames(StepInHold.PressLetThrough))
              .Append(", the press clamp still cut (his enemy within his stop / at the enemy line's front) ").Append(Frames(StepInHold.PressCut))
              .Append(", the rest clamp ").Append(Frames(StepInHold.RestClamp));
            long stronger = 0;
            for (int i = (int)StepInHold.Backpedal; i < (int)StepInHold.Count; i++) stronger += _frames[i];
            sb.Append(", a stronger wish ").Append(stronger);
            if (stronger > 0)
            {
                sb.Append(" (");
                bool first = true;
                for (int i = (int)StepInHold.Backpedal; i < (int)StepInHold.Count; i++)
                {
                    if (_frames[i] == 0) continue;
                    if (!first) sb.Append(", ");
                    first = false;
                    sb.Append(AiStepInHoldMath.Name((StepInHold)i)).Append(' ').Append(_frames[i]);
                }
                sb.Append(')');
            }
            sb.Append("; the wall's leash shortened the stop of ").Append(LeashShortened).Append(" of ").Append(Started).Append(" step-ins");
            if (Steps > 0)
                sb.Append(" | per step-in from his positions: he walked ").Append((Covered / Steps).ToString("0.00", c)).Append(" m toward his enemy in ")
                  .Append((Seconds / Steps).ToString("0.00", c)).Append(" s (").Append((Seconds > 0 ? Covered / Seconds : 0).ToString("0.00", c)).Append(" m/s, peak ")
                  .Append((PeakSum / Steps).ToString("0.00", c)).Append(" m/s) with the stick asked ").Append(AiBashStepInMath.Stick.ToString("0.00", c))
                  .Append(", out of our filter ").Append((OutSum / Steps).ToString("0.00", c)).Append(" (n ").Append(Steps).Append("); ");
            else sb.Append(" | no step-in measured; ");
            sb.Append(EngineVerdict(ClearSteps, ClearSeconds, ClearCovered, ClearPeakSum));
            return sb.ToString();
        }
    }
}

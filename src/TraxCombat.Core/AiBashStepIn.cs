using System;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>Step 95: what the step-in did for one asked shield bash (the [summary] "the step-in bash" line's counts).</summary>
    public enum AiStepIn : byte
    {
        /// <summary>The step-in is off (AiBashStepIn off, or AiBashStepInToMetres 0) - the bash from where he stood (step 91).</summary>
        Off,

        /// <summary>The enemy already stood within AiBashStepInToMetres - no step.</summary>
        NotNeeded,

        /// <summary>He stepped in and the enemy came within the stop distance before the key.</summary>
        Arrived,

        /// <summary>He stepped in but the guard (AiBashStepInMaxSeconds) ran out first - the bash from where he got to.</summary>
        TimeRanOut,

        /// <summary>He stepped in but the attempt ended before its key (struck, the path after the guard, the enemy gone, switched off).</summary>
        CutShort,

        /// <summary>A man of his own side stood in the way of the step - no step, the bash from where he stood.</summary>
        RefusedFriend,

        /// <summary>A held shield-wall man under stand firm (or with the wall's leash at 0) - no step, the bash from his slot.</summary>
        RefusedWall,

        Count,
    }

    /// <summary>
    /// Step 95 pure (Toni 2026-10-25: "try making them step in for the bash"; his 20:26 / 09:53 bins: the AI never stands within 1.1 m of the
    /// enemy it fights, and a bash asked from 1.1-1.6 m mostly hits a friend or nothing - the shield bash's real reach is a 0.2 m sphere on the
    /// shield arm, ~1.1 m centre to centre - AI_NOTES "Step 91" / "Step 95"): THE STEP-IN BASH - during a shield bash's guard the shieldman WALKS
    /// IN toward his enemy (a forward movement written into his own AI input - step 16's hook, his facing kept) until the enemy is within
    /// <c>AiBashStepInToMetres</c>, then the kick key. Plumbing below; every number a player tunes is a setting.
    /// </summary>
    public static class AiBashStepInMath
    {
        /// <summary>The step's stick - a full forward push (like you holding W with the guard up); the engine's guarded walk speed and his tired
        /// run cap ride on it. Plumbing (step 16's backpedal uses the same 1.0).</summary>
        public const double Stick = 1.0;

        /// <summary>A friend blocks the STEP when he stands in the walk's corridor - ahead along the line to the enemy (beyond
        /// <see cref="CorridorMinAlong"/> and within the step + <see cref="CorridorAhead"/>) and within <see cref="CorridorHalfWidth"/> of it: two half
        /// bodies (0.2 m each) - a man that close would be walked into. Plumbing.</summary>
        public const double CorridorHalfWidth = 0.4;

        public const double CorridorAhead = 0.4;

        public const double CorridorMinAlong = 0.05;

        /// <summary>The guard the bash holds while he steps: at least the bash's own lead (<see cref="AiKickMath.BashLeadSeconds"/>), at most
        /// <paramref name="maxSeconds"/> (AiBashStepInMaxSeconds - the guard is extended only while he is still walking in).</summary>
        public static double StepGuardSeconds(double maxSeconds) => Math.Max(AiKickMath.BashLeadSeconds, double.IsNaN(maxSeconds) ? 0 : maxSeconds);

        /// <summary>
        /// The step-in for a bash asked with the enemy <paramref name="distance"/> m off: Off (the rules say no), NotNeeded (already within the stop
        /// distance), RefusedWall (a held shield-wall man under stand firm, or the wall's leash 0), else <see cref="AiStepIn.Arrived"/> = "he steps" -
        /// <paramref name="stopAt"/> the distance he walks in to: the stop distance, or for a held wall man (not standing firm) at most the wall's leash
        /// <paramref name="wallLeashMetres"/> (ShieldWallStepOutMetres) closer than now. The friend in the way is the module's read (the corridor).
        /// </summary>
        public static AiStepIn Decide(in AiKickRules r, double distance, bool wallHeld, bool wallFirm, double wallLeashMetres, out double stopAt)
        {
            stopAt = double.NaN;
            if (!r.StepInOn) return AiStepIn.Off;
            if (double.IsNaN(distance) || distance <= r.StepInTo + 1e-9) return AiStepIn.NotNeeded;
            stopAt = r.StepInTo;
            if (wallHeld)
            {
                double leash = double.IsNaN(wallLeashMetres) ? 0 : wallLeashMetres;
                if (wallFirm || !(leash > 0)) return AiStepIn.RefusedWall;
                stopAt = Math.Max(r.StepInTo, distance - leash);
            }
            return AiStepIn.Arrived;
        }

        /// <summary>He is close enough: the enemy within <paramref name="stopAt"/> (NaN = never).</summary>
        public static bool Arrived(double distance, double stopAt) => !double.IsNaN(distance) && !double.IsNaN(stopAt) && distance <= stopAt + 1e-9;

        /// <summary>
        /// The guard is over: at least the bash's lead passed AND (he is not stepping, or he arrived), or the step's guard
        /// (<see cref="StepGuardSeconds"/>) ran out.
        /// </summary>
        public static bool GuardDone(double now, double start, bool stepping, double maxSeconds)
        {
            if (now >= start + StepGuardSeconds(maxSeconds) - 1e-9) return true;
            return !stepping && now >= start + AiKickMath.BashLeadSeconds - 1e-9;
        }

        /// <summary>
        /// The forward step in his own input frame (x right, y forward): the unit world direction TOWARD his enemy (<paramref name="dx"/>,
        /// <paramref name="dy"/> - enemy minus him) through his body frame's side (<paramref name="sx"/>, <paramref name="sy"/>) and forward
        /// (<paramref name="fx"/>, <paramref name="fy"/>) axes, scaled to <see cref="Stick"/>. Facing his enemy it is (0, +1) - a straight walk
        /// forward; turned, it still walks the line to the enemy (his facing is the AI's, never touched). False (0, 0) when degenerate.
        /// </summary>
        public static bool Vector(double dx, double dy, double sx, double sy, double fx, double fy, out double lx, out double ly) =>
            AiInputMath.BackpedalVector(dx, dy, sx, sy, fx, fy, Stick, out lx, out ly);

        /// <summary>
        /// Is a friend at (<paramref name="qx"/>, <paramref name="qy"/>) in the step's corridor - him at (<paramref name="px"/>, <paramref name="py"/>)
        /// walking <paramref name="step"/> m toward the enemy at (<paramref name="tx"/>, <paramref name="ty"/>)? Ahead along that line past
        /// <see cref="CorridorMinAlong"/> and within step + <see cref="CorridorAhead"/>, within <see cref="CorridorHalfWidth"/> of it.
        /// </summary>
        public static bool InCorridor(double px, double py, double tx, double ty, double qx, double qy, double step)
        {
            double ux = tx - px, uy = ty - py;
            double len = Math.Sqrt(ux * ux + uy * uy);
            if (!(len > 1e-6) || double.IsNaN(step)) return false;
            ux /= len;
            uy /= len;
            double dx = qx - px, dy = qy - py;
            double along = dx * ux + dy * uy, side = Math.Abs(-dx * uy + dy * ux);
            return along > CorridorMinAlong && along <= Math.Max(0, step) + CorridorAhead && side <= CorridorHalfWidth;
        }

        public static string Name(AiStepIn s) => s switch
        {
            AiStepIn.Off => "off",
            AiStepIn.NotNeeded => "not needed (already close)",
            AiStepIn.Arrived => "stepped in and arrived",
            AiStepIn.TimeRanOut => "stepped in, the time ran out",
            AiStepIn.CutShort => "stepped in, cut short",
            AiStepIn.RefusedFriend => "refused - a friend in the way",
            AiStepIn.RefusedWall => "refused - held in the shield wall",
            _ => "?",
        };
    }

    /// <summary>
    /// Step 95 per mission: every asked shield bash's step-in (off / not needed / stepped - arrived, the time ran out, cut short / refused by a
    /// friend / by the wall), the gap closed by the stepping ones (the enemy's distance at the decision minus at the bash's moment - his walk and
    /// the enemy's own together), the guards extended, and each bash's result by the enemy's distance AT THE BASH (after the step - step 91's bins
    /// keep the decision's) + the 9th [summary] "ai kicks" line. Fixed arrays, allocation-free; main thread.
    /// </summary>
    public sealed class AiBashStepInStats
    {
        private readonly int[] _outcomes = new int[(int)AiStepIn.Count];
        private readonly int[,] _atBash = new int[AiBashReachStats.BinEdges.Length + 1, (int)AiBashResult.Count];

        /// <summary>The stepping bashes that reached their key: the gap closed (m), the decision's / the bash's distances summed.</summary>
        public int Pressed;
        public double GapClosed;
        public double FromSum;
        public double ToSum;

        /// <summary>Guards held past the bash's own 0.4 s while he walked in, and the extra seconds.</summary>
        public int Extended;
        public double ExtendedSeconds;

        /// <summary>Bashes that never reached their key (no distance at the bash) - their results are step 91's only.</summary>
        public int EndedBeforeKey;

        /// <summary>Step 97: what held each step-in frame back and each step-in's walk measured from his positions - its clause ends the line.</summary>
        public readonly AiStepInHoldStats Hold = new AiStepInHoldStats();

        public void Note(AiStepIn s) => _outcomes[(int)s]++;

        public int Count(AiStepIn s) => _outcomes[(int)s];

        public int Stepped => _outcomes[(int)AiStepIn.Arrived] + _outcomes[(int)AiStepIn.TimeRanOut] + _outcomes[(int)AiStepIn.CutShort];

        public int Total
        {
            get
            {
                int n = 0;
                foreach (int v in _outcomes) n += v;
                return n;
            }
        }

        /// <summary>A stepping bash reached its key: the enemy <paramref name="from"/> m off at the decision, <paramref name="to"/> m now.</summary>
        public void NotePress(double from, double to)
        {
            if (double.IsNaN(from) || double.IsNaN(to)) return;
            Pressed++;
            FromSum += from;
            ToSum += to;
            GapClosed += from - to;
        }

        /// <summary>What a bash came to, filed by the enemy's distance at its KEY (NaN = it never got there - counted apart).</summary>
        public void NoteResult(double atBash, AiBashResult result)
        {
            if (double.IsNaN(atBash))
            {
                EndedBeforeKey++;
                return;
            }
            _atBash[AiBashReachStats.BinOf(atBash), (int)result]++;
        }

        public int Result(int bin, AiBashResult r) => _atBash[bin, (int)r];

        public double MeanStep => Pressed > 0 ? GapClosed / Pressed : double.NaN;

        /// <summary>The friends' share of a bin's bash HITS by the distance at the bash, NaN with none.</summary>
        public double FriendShare(int bin)
        {
            int hits = _atBash[bin, (int)AiBashResult.Enemy] + _atBash[bin, (int)AiBashResult.EnemyShield] + _atBash[bin, (int)AiBashResult.Friend]
                       + _atBash[bin, (int)AiBashResult.Untracked];
            return hits > 0 ? (double)_atBash[bin, (int)AiBashResult.Friend] / hits : double.NaN;
        }

        /// <summary>The 9th [summary] "ai kicks" line, in plain words.</summary>
        public string Line(in AiKickRules r)
        {
            var c = CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder("ai kicks - the step-in bash (step 95): ");
            if (r.StepInOn)
                sb.Append("a shieldman steps in during his bash's guard until his enemy is within ").Append(AiKickMath.F2(r.StepInTo))
                  .Append(" m (AiBashStepInToMetres; the guard held up to ").Append(AiKickMath.F2(AiBashStepInMath.StepGuardSeconds(r.StepInMaxSeconds)))
                  .Append(" s while he walks, never through a friend, a standing shield wall never)");
            else
                sb.Append("the step-in is OFF (").Append(r.StepInOffBecause).Append(") - a shieldman bashes from where he stands");
            int total = Total;
            sb.Append(" | bashes asked ").Append(total).Append(": stepped in ").Append(Stepped).Append(" (arrived ").Append(Count(AiStepIn.Arrived))
              .Append(", the time ran out ").Append(Count(AiStepIn.TimeRanOut)).Append(", cut short ").Append(Count(AiStepIn.CutShort)).Append("), not needed - already close ")
              .Append(Count(AiStepIn.NotNeeded)).Append(", refused - a friend in the way ").Append(Count(AiStepIn.RefusedFriend)).Append(", refused - held in the shield wall ")
              .Append(Count(AiStepIn.RefusedWall));
            if (Count(AiStepIn.Off) > 0) sb.Append(", with the step-in off ").Append(Count(AiStepIn.Off));
            if (Pressed > 0)
                sb.Append("; the gap closed by a step ").Append(MeanStep.ToString("0.00", c)).Append(" m on average (the enemy ").Append((FromSum / Pressed).ToString("0.00", c))
                  .Append(" m off at the decision → ").Append((ToSum / Pressed).ToString("0.00", c)).Append(" m at the bash)");
            if (Extended > 0) sb.Append("; the guard held longer while walking in ").Append(Extended).Append(" times (").Append((ExtendedSeconds / Extended).ToString("0.00", c)).Append(" s more on average)");
            sb.Append(" | bashes by the enemy's distance AT THE BASH (after the step) → on an ENEMY / into his shield / on a FRIEND / missed / horse or untracked / not played: ");
            bool first = true;
            for (int b = 0; b < AiBashReachStats.BinCount; b++)
            {
                int n = 0;
                for (int k = 0; k < (int)AiBashResult.Count; k++) n += _atBash[b, k];
                if (n == 0) continue;
                if (!first) sb.Append("; ");
                first = false;
                sb.Append(AiBashReachStats.BinName(b)).Append(' ').Append(_atBash[b, (int)AiBashResult.Enemy]).Append(" / ").Append(_atBash[b, (int)AiBashResult.EnemyShield])
                  .Append(" / ").Append(_atBash[b, (int)AiBashResult.Friend]).Append(" / ").Append(_atBash[b, (int)AiBashResult.Missed]).Append(" / ")
                  .Append(_atBash[b, (int)AiBashResult.Untracked]).Append(" / ").Append(_atBash[b, (int)AiBashResult.NotPlayed]);
                double fs = FriendShare(b);
                if (!double.IsNaN(fs)) sb.Append(" (friends ").Append((100 * fs).ToString("0", c)).Append("% of the hits)");
            }
            if (first) sb.Append("none");
            sb.Append("; ended before the key ").Append(EndedBeforeKey);
            if (r.StepInOn || Hold.Started > 0 || Hold.TotalFrames > 0) sb.Append(" | ").Append(Hold.Clause()); // step 97
            return sb.ToString();
        }
    }
}

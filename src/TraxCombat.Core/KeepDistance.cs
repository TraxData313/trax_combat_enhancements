using System;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 109 - KEEP YOUR DISTANCE (Toni 2026-11-10: "when they are tired, they come as close to the enemy as they can. Instead, if they are not in a
    /// shield wall, while they wait to gather Athletics, let him stay far ... But time it, so they aren't continuously backing up if I come close";
    /// AI_NOTES "Step 109", DESIGN decision 98). The settings, read live: the master switch FIRST, then the Athletics system (the pause, the swing
    /// stack and the brace that make a man unable to attack are all Athletics'), then <c>KeepDistanceEnabled</c>, then the numbers.
    /// </summary>
    public readonly struct KeepDistanceRules
    {
        public KeepDistanceRules(bool modEnabled, bool athleticsEnabled, bool keepEnabled, double quietMinSeconds = 2, double quietMaxSeconds = 3,
            double weaponLengths = 2, double fallbackLengthMetres = 1, double maxBackMetres = 4, double maxHoldSeconds = 10, double reachMetres = 3)
        {
            ModEnabled = modEnabled;
            AthleticsEnabled = athleticsEnabled;
            KeepEnabled = keepEnabled;
            QuietMinSeconds = Clamp(quietMinSeconds, 0, 10);
            QuietMaxSeconds = Math.Max(QuietMinSeconds, Clamp(quietMaxSeconds, 0, 10));
            WeaponLengths = Clamp(weaponLengths, 0, 5);
            FallbackLengthMetres = Clamp(fallbackLengthMetres, 0.3, 3);
            MaxBackMetres = Clamp(maxBackMetres, 0, 10);
            MaxHoldSeconds = Clamp(maxHoldSeconds, 1, 60);
            ReachMetres = Clamp(reachMetres, 1, 8);
        }

        public static KeepDistanceRules From(TraxSettings s) =>
            new KeepDistanceRules(s.ModEnabled, s.AthleticsEnabled, s.KeepDistanceEnabled, s.KeepDistanceQuietMinSeconds, s.KeepDistanceQuietMaxSeconds,
                s.KeepDistanceWeaponLengths, s.KeepDistanceFallbackLengthMetres, s.KeepDistanceMaxBackMetres, s.KeepDistanceMaxHoldSeconds, s.KeepDistanceReachMetres);

        public bool ModEnabled { get; }
        public bool AthleticsEnabled { get; }

        /// <summary>KeepDistanceEnabled - the player page's switch.</summary>
        public bool KeepEnabled { get; }

        /// <summary>KeepDistanceQuiet*Seconds: each man's QUIET wait, rolled from this range - no action between him and his enemy for that long.</summary>
        public double QuietMinSeconds { get; }
        public double QuietMaxSeconds { get; }

        /// <summary>KeepDistanceWeaponLengths: he backs off this many lengths of his own wielded weapon.</summary>
        public double WeaponLengths { get; }

        /// <summary>KeepDistanceFallbackLengthMetres: the length when his weapon's own is unknown.</summary>
        public double FallbackLengthMetres { get; }

        /// <summary>KeepDistanceMaxBackMetres: one back-off at most this far, whatever the weapon.</summary>
        public double MaxBackMetres { get; }

        /// <summary>KeepDistanceMaxHoldSeconds: his own AI has him again after this long.</summary>
        public double MaxHoldSeconds { get; }

        /// <summary>KeepDistanceReachMetres: he backs off only from the enemy he fights while that enemy stands within this.</summary>
        public double ReachMetres { get; }

        public bool On => ModEnabled && AthleticsEnabled && KeepEnabled;

        public string? OffBecause => !ModEnabled ? "ModEnabled" : !AthleticsEnabled ? "AthleticsEnabled" : !KeepEnabled ? "KeepDistanceEnabled" : null;

        public string Describe() =>
            "keep your distance (step 109): an AI man on foot (not held in a shield wall, not routing, not resting) who cannot attack now (his pause, an empty "
            + "swing stack or a brace) and whose enemy stands within " + F(ReachMetres) + " m backs off after his own quiet wait of " + F(QuietMinSeconds) + "-"
            + F(QuietMaxSeconds) + " s with no swing, kick, bash, block or hit between them: " + F(WeaponLengths) + " x his weapon's length (" + F(FallbackLengthMetres)
            + " m when unknown), at most " + F(MaxBackMetres) + " m, walking backwards facing him, never past the rest's limit behind his own front; he then keeps "
            + "that gap until he can attack again (or an action, a new target, " + F(MaxHoldSeconds) + " s); the rest wins while it runs - "
            + (On ? "ON" : "OFF (" + OffBecause + "): measured only");

        private static double Clamp(double v, double lo, double hi) => double.IsNaN(v) ? lo : v < lo ? lo : v > hi ? hi : v;

        internal static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>Step 109: why a keep-distance ended (the summary's "ends by reason").</summary>
    public enum KeepEnd
    {
        /// <summary>He can attack again (the pause over, a swing back in the stack, the brace over) - his own AI closes in.</summary>
        Ready,

        /// <summary>A swing, kick, bash, block or hit between him and his enemy - he defends as usual.</summary>
        Action,

        /// <summary>The master switch, Athletics or KeepDistanceEnabled went off.</summary>
        SwitchedOff,

        /// <summary>His AI's target changed (or he has none in reach).</summary>
        Target,

        /// <summary><see cref="KeepDistanceRules.MaxHoldSeconds"/> passed.</summary>
        Timeout,

        /// <summary>Step 78c's rest (or the rotation's swap) took him over - the rest wins.</summary>
        Rest,

        /// <summary>Held by a shield wall, routing, mounted, a ranged weapon, the player took him.</summary>
        Excluded,

        LeftField,
        MissionEnd,
        Error,
        Count,
    }

    /// <summary>Step 109: a keeping man's phase.</summary>
    public enum KeepPhase : byte
    {
        None,

        /// <summary>Walking straight back from his enemy, facing him.</summary>
        Backing,

        /// <summary>At his gap: he stands (his own walk toward the enemy taken out).</summary>
        Holding,
    }

    /// <summary>Step 109: one man's keep-distance state (one object per man, made at tracking - nothing allocated in a look or a steer).</summary>
    public sealed class KeepMind
    {
        public KeepPhase Phase;

        /// <summary>When this keep began, and when its current back-off began.</summary>
        public double Since = double.NaN, BackSince = double.NaN;

        /// <summary>The enemy he keeps away from (his AI's target at the start).</summary>
        public object? Target;

        /// <summary>His rolled quiet wait (s) - NaN = roll at the next look (rolled afresh for each quiet spell).</summary>
        public double QuietRoll = double.NaN;

        /// <summary>The quiet clock's other start: a keep's end, the enemy stepping in while he held (the clock runs from the later of this and his last action).</summary>
        public double QuietFrom = double.NegativeInfinity;

        /// <summary>The gap at the back-off's start, its goal (start + the weapon lengths, capped), the gap when he began to hold, where the back-off began.</summary>
        public double StartGap, Goal, HoldGap, StartX, StartY;

        /// <summary>One back-off's length for him (m) - the weapon lengths x his weapon, capped; read at the start (a back-off after a step-in reuses it).</summary>
        public double Back;

        /// <summary>His weapon's length at the start (m; NaN = unknown - the fallback).</summary>
        public double Length = double.NaN;

        /// <summary>The farthest he got from the back-off's start (m) - the summary's "how far".</summary>
        public double Walked;

        /// <summary>A spear (polearm) in his hand at the start - the summary's weapon class.</summary>
        public bool Spear;

        /// <summary>He was a candidate at his last look (cannot attack, enemy in reach, not keeping) and when that look was - the "quiet spells broken" count.</summary>
        public bool Candidate;
        public double LastLook = double.NaN;

        /// <summary>The wish is on (his input hooked, listed for the steering), and its frames at the start.</summary>
        public bool Wish;
        public long MoveAtStart, ClampAtStart;

        /// <summary>The next steer and the next edge check (mission s).</summary>
        public double NextSteer, NextEdge;

        public bool Keeping => Phase != KeepPhase.None;

        public void Reset(double now)
        {
            Phase = KeepPhase.None;
            Target = null;
            QuietRoll = double.NaN;
            QuietFrom = now;
            Since = BackSince = double.NaN;
            Candidate = false;
        }
    }

    /// <summary>Step 109 - the pure part: who backs off, when (the quiet wait), how far, how far still to walk, when it ends.</summary>
    public static class KeepDistanceMath
    {
        /// <summary>Each man is looked at about this often (staggered). Plumbing.</summary>
        public const double PollSeconds = 0.25;

        /// <summary>A keeping man is steered this often. Plumbing.</summary>
        public const double SteerSeconds = 0.1;

        /// <summary>He walks back at this much of the stick at most - a walk, never a run (the rest's). Plumbing.</summary>
        public const double WalkStick = 0.6;

        /// <summary>The back-off is done within this of its goal. Plumbing.</summary>
        public const double ArriveMetres = 0.15;

        /// <summary>Holding: the enemy stepped in this much closer than the gap he held - he stands and defends, and his quiet wait starts again (no
        /// back-off at once - Toni: "he shouldn't just start backing up again"). Plumbing.</summary>
        public const double StepInMetres = 0.75;

        /// <summary>Holding: farther than his goal + this (the enemy walked off) he is not held - his own AI may close to the gap again. Plumbing.</summary>
        public const double HoldSlackMetres = 1.0;

        /// <summary>Holding in a line: more than the rest's limit + this behind his own front (his line moved on) he follows it (his own AI). Plumbing.</summary>
        public const double FollowSlackMetres = 0.5;

        /// <summary>His front is read only when his rhythm look is at most this old (s). Plumbing.</summary>
        public const double FrontFreshSeconds = 2.0;

        /// <summary>The waiting-distance measure counts an enemy at most this far (m). Plumbing.</summary>
        public const double MeasureMaxMetres = 10.0;

        /// <summary>The summary's walk histogram: bins of this (m), this many. Plumbing.</summary>
        public const double WalkBinMetres = 0.25;

        public const int WalkBins = 48;

        /// <summary>His quiet wait from a roll 0..1: min + roll x (max − min).</summary>
        public static double QuietSeconds(in KeepDistanceRules r, double roll)
        {
            double u = double.IsNaN(roll) ? 0.5 : roll < 0 ? 0 : roll > 1 ? 1 : roll;
            return r.QuietMinSeconds + u * (r.QuietMaxSeconds - r.QuietMinSeconds);
        }

        /// <summary>He cannot attack now: his step-13 pause runs, his step-32 stack is empty, or he braces (step 23).</summary>
        public static bool CannotAttack(bool pauseRunning, bool stackEmpty, bool braced) => pauseRunning || stackEmpty || braced;

        /// <summary>The weapon length he counts in (m): his own when known (above 0.1 m), else the fallback.</summary>
        public static double Length(in KeepDistanceRules r, double weaponMetres) =>
            !double.IsNaN(weaponMetres) && weaponMetres > 0.1 ? weaponMetres : r.FallbackLengthMetres;

        /// <summary>How far one back-off takes him: the weapon lengths x his length, capped.</summary>
        public static double BackMetres(in KeepDistanceRules r, double weaponMetres) => Math.Min(r.MaxBackMetres, r.WeaponLengths * Length(in r, weaponMetres));

        /// <summary>The quiet clock's start: the later of his last action and <see cref="KeepMind.QuietFrom"/> (NaN ignored).</summary>
        public static double QuietStart(double lastAction, double quietFrom)
        {
            double a = double.IsNaN(lastAction) ? double.NegativeInfinity : lastAction;
            double b = double.IsNaN(quietFrom) ? double.NegativeInfinity : quietFrom;
            return a > b ? a : b;
        }

        /// <summary>The quiet wait is over.</summary>
        public static bool Quiet(double now, double quietStart, double quietSeconds) =>
            !double.IsNaN(quietSeconds) && (double.IsNegativeInfinity(quietStart) || now - quietStart >= quietSeconds - 1e-9);

        /// <summary>He backs off now: the feature on, he may (eligible: an AI man on foot, not wall-held, not routing, no ranged weapon in hand), the rest
        /// does not have him, he cannot attack, his enemy within reach, the quiet wait over.</summary>
        public static bool Starts(in KeepDistanceRules r, bool eligible, bool resting, bool cannotAttack, double enemyDist, bool quiet) =>
            r.On && Candidate(in r, eligible, resting, cannotAttack, enemyDist) && quiet;

        /// <summary>A candidate (ON or OFF - the OFF looks measure): eligible, not resting, cannot attack, his enemy within reach.</summary>
        public static bool Candidate(in KeepDistanceRules r, bool eligible, bool resting, bool cannotAttack, double enemyDist) =>
            eligible && !resting && cannotAttack && !double.IsNaN(enemyDist) && enemyDist <= r.ReachMetres + 1e-9;

        /// <summary>THE WALK: how far he still backs now - to his goal (+ nothing), never more than the rest's limit behind his own front
        /// (<paramref name="behindFront"/>; NaN = no front known - a duel, a tournament). 0 = he is there (he holds).</summary>
        public static double WalkBack(double gap, double goal, double behindFront, double restLimitMetres)
        {
            if (double.IsNaN(gap) || double.IsNaN(goal)) return 0;
            double left = goal - gap;
            if (left <= ArriveMetres) return 0;
            if (!double.IsNaN(behindFront))
            {
                double room = restLimitMetres - behindFront;
                if (room <= ArriveMetres) return 0;
                if (room < left) left = room;
            }
            return left;
        }

        /// <summary>Holding: he stands (his walk toward the enemy taken out) - unless the enemy walked off past his goal + the slack, or his line moved on
        /// more than the rest's limit + the slack ahead of him (he keeps up with it).</summary>
        public static bool Stands(double gap, double goal, double behindFront, double restLimitMetres) =>
            !double.IsNaN(gap) && gap <= goal + HoldSlackMetres && (double.IsNaN(behindFront) || behindFront <= restLimitMetres + FollowSlackMetres);

        /// <summary>Holding: the enemy stepped in (the gap closed by <see cref="StepInMetres"/> from the one he held).</summary>
        public static bool SteppedIn(double gap, double holdGap) => !double.IsNaN(gap) && !double.IsNaN(holdGap) && gap < holdGap - StepInMetres;

        /// <summary>A running keep's verdict at a look (<see cref="KeepEnd.Count"/> = he keeps on): the switch, the rest, the gates, an action, the target,
        /// ready, the time - in that order.</summary>
        public static KeepEnd Verdict(in KeepDistanceRules r, bool resting, bool eligible, bool actionSinceStart, bool sameTarget, bool cannotAttack, double heldSeconds)
        {
            if (!r.On) return KeepEnd.SwitchedOff;
            if (resting) return KeepEnd.Rest;
            if (!eligible) return KeepEnd.Excluded;
            if (actionSinceStart) return KeepEnd.Action;
            if (!sameTarget) return KeepEnd.Target;
            if (!cannotAttack) return KeepEnd.Ready;
            if (!double.IsNaN(heldSeconds) && heldSeconds > r.MaxHoldSeconds + 1e-9) return KeepEnd.Timeout;
            return KeepEnd.Count;
        }

        public static string EndName(KeepEnd e) => e switch
        {
            KeepEnd.Ready => "ready",
            KeepEnd.Action => "action",
            KeepEnd.SwitchedOff => "switch",
            KeepEnd.Target => "target",
            KeepEnd.Timeout => "timeout",
            KeepEnd.Rest => "rest took over",
            KeepEnd.Excluded => "excluded",
            KeepEnd.LeftField => "left the field",
            KeepEnd.MissionEnd => "mission end",
            KeepEnd.Error => "error",
            _ => e.ToString(),
        };
    }

    /// <summary>Step 109 per mission - the [summary] "keep distance (step 109)" line.</summary>
    public sealed class KeepDistanceStats
    {
        /// <summary>Back-offs started (the first of a keep and each one after the enemy stepped in and it went quiet again), of them with a spear; keeps begun.</summary>
        public long BacksOff, BacksOffSpear, Keeps, ReBacks;

        /// <summary>The goals asked (m) - the sum.</summary>
        public double GoalSum;

        /// <summary>How far each keep took him from its back-off's start - a histogram (p90) and the sums.</summary>
        public readonly long[] WalkBins = new long[KeepDistanceMath.WalkBins + 1];
        public double WalkSum, WalkMax;
        public long WalkN;

        /// <summary>How long each keep lasted.</summary>
        public double HeldSum, HeldMax;
        public long HeldN;

        public readonly long[] Ends = new long[(int)KeepEnd.Count];

        /// <summary>Quiet spells broken by an action (a candidate's clock restarted), the enemy stepping in while he held, would-back-offs with the feature OFF.</summary>
        public long QuietBroken, SteppedIn, WouldOff;

        /// <summary>The distance to his enemy while waiting (his pause or an empty stack), by [0 = OFF, 1 = ON][0 = other, 1 = spear]: sums and looks.</summary>
        public readonly double[,] WaitSum = new double[2, 2];
        public readonly long[,] WaitN = new long[2, 2];

        /// <summary>Input frames: walked back, the clamp bit.</summary>
        public long MoveFrames, ClampFrames;

        public long Looks, Steers, Errors;
        public double LookCostMs, SteerCostMs;

        public void AddBack(double goalMetres, bool spear, bool again)
        {
            BacksOff++;
            if (spear) BacksOffSpear++;
            if (again) ReBacks++;
            if (!double.IsNaN(goalMetres)) GoalSum += goalMetres;
        }

        public void AddWait(bool on, bool spear, double distance)
        {
            if (double.IsNaN(distance) || distance > KeepDistanceMath.MeasureMaxMetres) return;
            int i = on ? 1 : 0, j = spear ? 1 : 0;
            WaitSum[i, j] += distance;
            WaitN[i, j]++;
        }

        /// <summary>A keep's end: why, how long, the farthest from its back-off's start.</summary>
        public void AddEnd(KeepEnd why, double seconds, double walked)
        {
            if (why < KeepEnd.Count) Ends[(int)why]++;
            if (!double.IsNaN(seconds))
            {
                HeldSum += seconds;
                HeldN++;
                if (seconds > HeldMax) HeldMax = seconds;
            }
            if (double.IsNaN(walked)) return;
            WalkSum += walked;
            WalkN++;
            if (walked > WalkMax) WalkMax = walked;
            int b = (int)(walked / KeepDistanceMath.WalkBinMetres);
            WalkBins[b < 0 ? 0 : b > KeepDistanceMath.WalkBins ? KeepDistanceMath.WalkBins : b]++;
        }

        /// <summary>The walk's p-th quantile from the histogram (the bin's upper edge, never past the max).</summary>
        public double WalkQuantile(double p)
        {
            if (WalkN <= 0) return double.NaN;
            long need = (long)Math.Ceiling(p * WalkN), seen = 0;
            for (int i = 0; i <= KeepDistanceMath.WalkBins; i++)
            {
                seen += WalkBins[i];
                if (seen >= need) return Math.Min(WalkMax, (i + 1) * KeepDistanceMath.WalkBinMetres);
            }
            return WalkMax;
        }

        public double WaitMean(bool on, bool spear)
        {
            int i = on ? 1 : 0, j = spear ? 1 : 0;
            return WaitN[i, j] > 0 ? WaitSum[i, j] / WaitN[i, j] : double.NaN;
        }

        public string SummaryLine(in KeepDistanceRules r)
        {
            var sb = new System.Text.StringBuilder(640);
            sb.Append("keep distance (step 109) ").Append(r.On ? "ON" : "OFF (" + r.OffBecause + ")").Append(": backed off ").Append(BacksOff).Append(" (spear ")
              .Append(BacksOffSpear).Append(", again after the enemy stepped in and it went quiet ").Append(ReBacks).Append("; keeps ").Append(Keeps)
              .Append("), the goal mean ").Append(F2(BacksOff > 0 ? GoalSum / BacksOff : double.NaN)).Append(" m; walked mean ").Append(F2(WalkN > 0 ? WalkSum / WalkN : double.NaN))
              .Append(" m, p90 ").Append(F2(WalkQuantile(0.9))).Append(" m, max ").Append(F2(WalkN > 0 ? WalkMax : double.NaN)).Append(" m (cap ").Append(F1(r.MaxBackMetres))
              .Append(" m); held mean ").Append(F1(HeldN > 0 ? HeldSum / HeldN : double.NaN)).Append(" s, max ").Append(F1(HeldN > 0 ? HeldMax : double.NaN)).Append(" s; ends:");
            for (int i = 0; i < (int)KeepEnd.Count; i++)
                if (Ends[i] > 0 || i <= (int)KeepEnd.Rest) sb.Append(' ').Append(KeepDistanceMath.EndName((KeepEnd)i)).Append(' ').Append(Ends[i]).Append(',');
            sb.Append(" quiet spells broken by action ").Append(QuietBroken).Append(", the enemy stepped in while he held ").Append(SteppedIn)
              .Append(", would have backed off with it OFF ").Append(WouldOff)
              .Append("; the distance to his enemy while waiting (his pause or an empty stack): ON spear ").Append(Wait(true, true)).Append(", other ").Append(Wait(true, false))
              .Append("; OFF spear ").Append(Wait(false, true)).Append(", other ").Append(Wait(false, false))
              .Append("; input frames walked ").Append(MoveFrames).Append(", clamped ").Append(ClampFrames)
              .Append("; cost: looks ").Append(F2(LookCostMs)).Append(" ms (").Append(Looks).Append("), steering ").Append(F2(SteerCostMs)).Append(" ms (").Append(Steers)
              .Append("); errors ").Append(Errors);
            return sb.ToString();
        }

        private string Wait(bool on, bool spear)
        {
            int i = on ? 1 : 0, j = spear ? 1 : 0;
            return WaitN[i, j] == 0 ? "-" : F2(WaitSum[i, j] / WaitN[i, j]) + " m (" + WaitN[i, j] + " looks)";
        }

        public static string F1(double v) => double.IsNaN(v) ? "-" : v.ToString("0.0", CultureInfo.InvariantCulture);

        public static string F2(double v) => double.IsNaN(v) ? "-" : v.ToString("0.00", CultureInfo.InvariantCulture);
    }
}

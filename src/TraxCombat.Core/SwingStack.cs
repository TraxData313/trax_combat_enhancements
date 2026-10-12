using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// The swing stack's settings for one decision (step 32, Anton 2026-10-12 - DESIGN §2 "The swing stack"), read live from
    /// <see cref="TraxSettings"/>. A struct, built where needed.
    /// </summary>
    public readonly struct SwingStackRules
    {
        public SwingStackRules(bool modEnabled, bool athleticsEnabled, bool stackEnabled, double pointsPerSwing, int minimum, bool kicksUseSwings)
        {
            ModEnabled = modEnabled;
            AthleticsEnabled = athleticsEnabled;
            StackEnabled = stackEnabled;
            PointsPerSwing = double.IsNaN(pointsPerSwing) || pointsPerSwing < SwingStackMath.MinPointsPerSwing ? SwingStackMath.MinPointsPerSwing : pointsPerSwing;
            Minimum = Math.Max(1, Math.Min(SwingStackMath.MaxMinimum, minimum));
            KicksUseSwings = kicksUseSwings;
        }

        public bool ModEnabled { get; }

        public bool AthleticsEnabled { get; }

        /// <summary>SwingStackEnabled (the switch itself).</summary>
        public bool StackEnabled { get; }

        /// <summary>SwingStackPointsPerSwing - Athletics points per stored swing.</summary>
        public double PointsPerSwing { get; }

        /// <summary>SwingStackMinimum - the stack never holds fewer places than this, even at empty.</summary>
        public int Minimum { get; }

        /// <summary>KicksUseSwings (the switch itself).</summary>
        public bool KicksUseSwings { get; }

        /// <summary>The stack runs now (the master switch first, then Athletics, then its own switch).</summary>
        public bool Enabled => ModEnabled && AthleticsEnabled && StackEnabled;

        /// <summary>Kicks and shield bashes spend (and wait for) a stored swing now.</summary>
        public bool KicksOn => Enabled && KicksUseSwings;

        /// <summary>Which switch holds it off ("ModEnabled", "AthleticsEnabled", "SwingStackEnabled"), null while on.</summary>
        public string? OffBecause => !ModEnabled ? "ModEnabled" : !AthleticsEnabled ? "AthleticsEnabled" : !StackEnabled ? "SwingStackEnabled" : null;

        public static SwingStackRules From(TraxSettings s) =>
            new SwingStackRules(s.ModEnabled, s.AthleticsEnabled, s.SwingStackEnabled, s.SwingStackPointsPerSwing, s.SwingStackMinimum, s.KicksUseSwings);

        /// <summary>The settings sentence of the mission-start line, a change and the summary.</summary>
        public string Describe()
        {
            if (!Enabled)
                return "OFF (" + OffBecause + ")" + (ModEnabled && AthleticsEnabled ? " - step 13's pause after EVERY attack below the peak line" : " - no pause at all");
            var ci = CultureInfo.InvariantCulture;
            return "ON - every fighter stores swings: max(" + Minimum + ", his Athletics points / " + PointsPerSwing.ToString("0.#", ci)
                   + ", rounded) (SwingStackMinimum " + Minimum + ", SwingStackPointsPerSwing " + PointsPerSwing.ToString("0.#", ci) + " - a full 150 pool holds "
                   + SwingStackMath.Cap(150, PointsPerSwing, Minimum) + ", empty " + Minimum + "); an attack spends one and waits only when none is left; one comes back per the pause that attack "
                   + "would have left (D x (1/m - 1) at his CURRENT m, + the AI's battle-pace share) while he is not attacking - an AI melee man's own gap (AiMeleeGapSeconds) after a swing "
                   + "from the stack does not count, so the average rate stays step 13's; kicks and shield bashes (KicksUseSwings) "
                   + (KicksUseSwings ? "spend one too and cannot start at 0" : "are free of it");
        }
    }

    /// <summary>
    /// Step 32 pure functions (AI_NOTES "Step 32"): the stack's size from his Athletics points.
    /// </summary>
    public static class SwingStackMath
    {
        /// <summary>SwingStackPointsPerSwing's floor (the setting's range starts here too; never ÷ 0).</summary>
        public const double MinPointsPerSwing = 1.0;

        /// <summary>SwingStackMinimum's ceiling (the setting's range).</summary>
        public const int MaxMinimum = 10;

        /// <summary>"Full, whatever the cap": the stored count a fighter spawns with (and gets back at a fresh start) until his
        /// cap clamps it. Plumbing.</summary>
        public const int Full = 1 << 20;

        /// <summary>The swing length a refill is timed by before he has finished an attack of his own (a kick first):
        /// the player guide's 0.8 s swing (Anton's logs measured a fresh AI melee attack at 0.78 s). Plumbing - the
        /// first attack's own D replaces it.</summary>
        public const double FallbackSwingSeconds = 0.8;

        /// <summary>Rounds half AWAY from zero (7.5 → 8, 2.5 → 3) - never banker's rounding.</summary>
        public static int RoundAway(double x) => double.IsNaN(x) ? 0 : (int)Math.Round(x, MidpointRounding.AwayFromZero);

        /// <summary>The stack's size at <paramref name="points"/> Athletics points: max(minimum, round(points / per swing)).
        /// Pool 150 full = 8, 75 points = 4, empty = the minimum (2); pool 50 full = 3.</summary>
        public static int Cap(double points, double pointsPerSwing, int minimum)
        {
            int min = Math.Max(1, minimum);
            if (double.IsNaN(points) || points <= 0) return min;
            double per = double.IsNaN(pointsPerSwing) || pointsPerSwing < MinPointsPerSwing ? MinPointsPerSwing : pointsPerSwing;
            int n = RoundAway(points / per);
            return n < min ? min : n;
        }

        /// <summary>This fighter's cap now: his CURRENT points (his share × his full pool).</summary>
        public static int Cap(in SwingStackRules s, in AthleticsRules r, Fighter f) =>
            Cap(f.Fraction * AthleticsMath.PoolPoints(in r, f), s.PointsPerSwing, s.Minimum);

        /// <summary>The summary's burst-length bins: 2, 3, 4, 5-6, 7-9, 10+.</summary>
        public const int BurstBins = 6;

        public static int BurstBin(int length) => length <= 2 ? 0 : length == 3 ? 1 : length == 4 ? 2 : length <= 6 ? 3 : length <= 9 ? 4 : 5;

        public static string BurstBinName(int bin) => bin switch
        {
            0 => "2",
            1 => "3",
            2 => "4",
            3 => "5-6",
            4 => "7-9",
            _ => "10+",
        };

        /// <summary>The count the bar shows: "4/8".</summary>
        public static string CountText(int stored, int cap) =>
            Math.Max(0, Math.Min(stored, cap)).ToString(CultureInfo.InvariantCulture) + "/" + cap.ToString(CultureInfo.InvariantCulture);

        /// <summary>The recovery bar's word with the stack on.</summary>
        public const string LabelWord = "Swings";
    }

    /// <summary>What one <see cref="SwingStack.Spend"/> did.</summary>
    public enum SpendResult
    {
        /// <summary>A stored swing was spent, none had been waited for.</summary>
        FromStack = 0,

        /// <summary>A stored swing was spent that a wait at 0 brought back.</summary>
        AfterWaiting = 1,

        /// <summary>Nothing was stored - he attacked anyway (a chained blow, a gate miss): the refill's progress is lost.</summary>
        AtEmpty = 2,
    }

    /// <summary>
    /// ONE fighter's swing stack (step 32 - Anton, 2026-10-12: "chain swings stack"), pure state driven by the module:
    /// <see cref="Stored"/> swings (spawn full - <see cref="SwingStackMath.Full"/> until a cap clamps it); an attack (or a kick /
    /// shield bash with KicksUseSwings) <see cref="Spend"/>s one; <see cref="Advance"/> brings one back per
    /// <c>refillSeconds</c> (the pause that attack would have left today, at his CURRENT m) of time NOT attacking -
    /// the clock stops during his attacks and, for an AI melee man after a swing taken from the stack, during his own gap
    /// (<see cref="StartGrace"/>); at the cap the clock rests at 0. A refill shorter than
    /// <see cref="AttackTimerMath.MinTimerSeconds"/> (at and above the peak line) fills the stack at once. Allocated once per
    /// fighter; no allocation after.
    /// </summary>
    public sealed class SwingStack
    {
        /// <summary>A refill this close to whole is whole (float sums of dt / refill). Plumbing.</summary>
        public const double ProgressEpsilon = 1e-9;

        /// <summary>Swings stored (may sit above the cap until the next clamp - <see cref="Count"/> is what he has).</summary>
        public int Stored { get; private set; } = SwingStackMath.Full;

        /// <summary>The refill toward the next stored swing, 0..1.</summary>
        public double Progress { get; private set; }

        /// <summary>When the clock was last advanced (NaN = never).</summary>
        public double ClockAt { get; private set; } = double.NaN;

        /// <summary>The clock waits until then (an AI man's own gap after a swing from the stack).</summary>
        public double GraceUntil { get; private set; } = double.NegativeInfinity;

        /// <summary>A stored swing came back (or the stack filled) since his last spend - the burst run ends.</summary>
        public bool GainedSinceSpend { get; private set; } = true;

        /// <summary>He waited at 0 for the swing he holds now (the next spend is "after waiting").</summary>
        public bool Waited { get; private set; }

        /// <summary>The running burst: attacks in a row with no swing back between them.</summary>
        public int Run { get; private set; }

        /// <summary>What he has now under <paramref name="cap"/>.</summary>
        public int Count(int cap) => Stored < cap ? Stored : cap;

        /// <summary>The stored count clamped to <paramref name="cap"/> (his points dropped). True when it was cut.</summary>
        public bool Clamp(int cap)
        {
            if (Stored <= cap) return false;
            Stored = cap;
            Progress = 0;
            return true;
        }

        /// <summary>
        /// The clock up to <paramref name="now"/>: <paramref name="attacking"/> = he was attacking all that time (the clock
        /// stood still). Returns the swings that came back.
        /// </summary>
        public int Advance(double now, double refillSeconds, int cap, bool attacking)
        {
            double from = double.IsNaN(ClockAt) ? now : ClockAt;
            ClockAt = now;
            if (Stored >= cap)
            {
                Stored = cap;
                Progress = 0;
                return 0;
            }
            if (attacking) return 0;
            if (GraceUntil > from) from = GraceUntil;
            double dt = now - from;
            if (dt <= 0) return 0;
            int before = Stored;
            if (double.IsNaN(refillSeconds) || !AttackTimerMath.Worth(refillSeconds))
            {
                Stored = cap; // no pause worth a timer: the stack refills at once
                Progress = 0;
            }
            else
            {
                Progress += dt / refillSeconds;
                while (Progress >= 1.0 - ProgressEpsilon && Stored < cap)
                {
                    Stored++;
                    Progress = Math.Max(0, Progress - 1.0);
                }
                if (Stored >= cap) Progress = 0;
            }
            int gained = Stored - before;
            if (gained > 0) GainedSinceSpend = true;
            return gained;
        }

        /// <summary>The refill share he would show at <paramref name="now"/> without moving the clock (the bar reads it every
        /// frame between advances). 1 at the cap.</summary>
        public double ProgressAt(double now, double refillSeconds, int cap, bool attacking)
        {
            if (Stored >= cap) return 1.0;
            if (attacking || double.IsNaN(ClockAt)) return Progress;
            if (double.IsNaN(refillSeconds) || !AttackTimerMath.Worth(refillSeconds)) return 1.0;
            double from = GraceUntil > ClockAt ? GraceUntil : ClockAt;
            double p = Progress + Math.Max(0, now - from) / refillSeconds;
            return p >= 1.0 ? 1.0 : p;
        }

        /// <summary>Seconds of not attacking until the next stored swing comes back (0 when one is stored or none needs waiting for).</summary>
        public double SecondsToNext(double refillSeconds, int cap)
        {
            if (Count(cap) > 0 || double.IsNaN(refillSeconds) || !AttackTimerMath.Worth(refillSeconds)) return 0;
            return Math.Max(0, (1.0 - Progress) * refillSeconds);
        }

        /// <summary>
        /// One attack (or kick / bash) spends a stored swing - clamped to <paramref name="cap"/> first. At 0 nothing is stored:
        /// he attacked anyway (<see cref="SpendResult.AtEmpty"/>), and the refill so far is lost - an attack is never free.
        /// <paramref name="runEnded"/> = the burst run that this spend closed (≥ 2 = a burst; 0 = none closed).
        /// </summary>
        public SpendResult Spend(int cap, out int runEnded)
        {
            if (Stored > cap) Stored = cap;
            runEnded = 0;
            if (GainedSinceSpend || Run == 0)
            {
                runEnded = Run;
                Run = 1;
            }
            else
            {
                Run++;
            }
            GainedSinceSpend = false;
            if (Stored <= 0)
            {
                Progress = 0;
                Waited = false;
                return SpendResult.AtEmpty;
            }
            Stored--;
            bool waited = Waited;
            Waited = false;
            return waited ? SpendResult.AfterWaiting : SpendResult.FromStack;
        }

        /// <summary>A wait at 0 ended with a swing back: the next spend is "after waiting".</summary>
        public void MarkWaited() => Waited = true;

        /// <summary>The AI's own gap after a swing from the stack: the clock waits until <paramref name="until"/>.</summary>
        public void StartGrace(double until)
        {
            if (until > GraceUntil) GraceUntil = until;
        }

        /// <summary>A fresh start (spawn, the switch back on, the hideout's boss fight): full again; the running burst is
        /// returned (≥ 2 = a burst to count) and closed.</summary>
        public int FillUp()
        {
            int run = Run;
            Stored = SwingStackMath.Full;
            Progress = 0;
            GraceUntil = double.NegativeInfinity;
            GainedSinceSpend = true;
            Waited = false;
            Run = 0;
            return run;
        }

        /// <summary>Closes the running burst (he left the field, the mission ended) - returns its length.</summary>
        public int CloseRun()
        {
            int run = Run;
            Run = 0;
            GainedSinceSpend = true;
            return run;
        }
    }

    /// <summary>
    /// One mission's swing-stack numbers (step 32): attacks from the stack / after waiting / at empty, you and the AI apart; bursts
    /// (≥ 2 attacks in a row with no swing back between) by length; kicks and bashes spent, refused at 0 (your gate, the AI's
    /// input) and slipped through at 0; waits at 0 (holds) and their length; the stack's average fill (count ÷ cap) by f band;
    /// cuts when points dropped; fresh starts. Main thread. Allocation only at construction.
    /// </summary>
    public sealed class SwingStackStats
    {
        private const int Bins = AthleticsMath.PeakBins;

        private readonly int[,] _spent = new int[2, 3];             // [you 1 / AI 0, SpendResult]
        private readonly int[,] _bursts = new int[2, SwingStackMath.BurstBins];
        private readonly int[] _longest = new int[2];
        private readonly double[] _fillTime = new double[Bins];
        private readonly double[] _fillSum = new double[Bins];
        private readonly MeanStd[] _waits = new MeanStd[2];
        private readonly double[] _waitMax = new double[2];

        public int KicksSpent;
        public int KicksSpentYou;
        public int KicksAtEmpty;
        public int KicksRefusedYou;
        public long KicksRefusedAi;
        public int Clamps;
        public int FreshStarts;
        public int Gained;

        private static int Who(bool you) => you ? 1 : 0;

        public void AddSpend(bool you, SpendResult r) => _spent[Who(you), (int)r]++;

        public void AddRun(bool you, int length)
        {
            if (length < 2) return;
            int w = Who(you);
            _bursts[w, SwingStackMath.BurstBin(length)]++;
            if (length > _longest[w]) _longest[w] = length;
        }

        public void AddWait(bool you, double seconds)
        {
            if (double.IsNaN(seconds) || seconds < 0) return;
            int w = Who(you);
            _waits[w].Add(seconds);
            if (seconds > _waitMax[w]) _waitMax[w] = seconds;
        }

        /// <summary>A sample of one fighter's fill (count ÷ cap) at f band <paramref name="bin"/> over <paramref name="seconds"/>.</summary>
        public void AddFill(int bin, double share, double seconds)
        {
            if (bin < 0 || bin >= Bins || seconds <= 0) return;
            _fillTime[bin] += seconds;
            _fillSum[bin] += share * seconds;
        }

        public int Spent(bool you, SpendResult r) => _spent[Who(you), (int)r];

        public int Bursts(bool you)
        {
            int n = 0;
            for (int b = 0; b < SwingStackMath.BurstBins; b++) n += _bursts[Who(you), b];
            return n;
        }

        public int Longest(bool you) => _longest[Who(you)];

        public int Waits(bool you) => _waits[Who(you)].Count;

        /// <summary>The average fill in band <paramref name="bin"/> (NaN with no time there).</summary>
        public double AverageFill(int bin) => _fillTime[bin] > 0 ? _fillSum[bin] / _fillTime[bin] : double.NaN;

        /// <summary>The [summary] "swing stack" lines.</summary>
        public List<string> SummaryLines(in SwingStackRules rules, bool switched)
        {
            var lines = new List<string>(5);
            lines.Add("swing stack: " + rules.Describe() + (switched ? " (SWITCHED mid-battle: the lines mix both)" : string.Empty));
            for (int w = 1; w >= 0; w--)
            {
                bool you = w == 1;
                int from = _spent[w, 0], after = _spent[w, 1], empty = _spent[w, 2];
                var t = new StringBuilder(200);
                t.Append("swing stack - ").Append(you ? "you" : "AI").Append(": attacks ").Append(from + after + empty)
                    .Append(" (from the stack ").Append(from).Append(", after waiting at 0 ").Append(after).Append(", at an empty stack ").Append(empty)
                    .Append(empty > 0 ? " - chained blows or a gate miss" : string.Empty).Append("); bursts (2+ in a row, no swing back between) ").Append(Bursts(you));
                if (Bursts(you) > 0)
                {
                    t.Append(" by length");
                    for (int b = 0; b < SwingStackMath.BurstBins; b++)
                        if (_bursts[w, b] > 0) t.Append(' ').Append(SwingStackMath.BurstBinName(b)).Append(": ").Append(_bursts[w, b]);
                    t.Append(", longest ").Append(_longest[w]);
                }
                var wt = _waits[w];
                t.Append("; waits at 0 ").Append(wt.Count);
                if (wt.Count > 0) t.Append(" (avg ").Append(F2(wt.Mean)).Append(" s, longest ").Append(F2(_waitMax[w])).Append(" s)");
                lines.Add(t.ToString());
            }
            lines.Add("swing stack - kicks and bashes (KicksUseSwings " + (rules.KicksUseSwings ? "on" : "off") + " at the end): spent a swing " + KicksSpent + " (you " + KicksSpentYou
                      + "), refused at 0 - yours " + KicksRefusedYou + " (the gate took the kick key out), the AI's " + KicksRefusedAi + " input frames; started at 0 anyway " + KicksAtEmpty
                      + " (a bash already under way, or an AI kick the hook did not see)");
            var f = new StringBuilder(160);
            f.Append("swing stack - average fill (stored ÷ his cap) by f: ");
            for (int b = 0; b < Bins; b++)
                f.Append(b == 0 ? "" : ", ").Append(AthleticsMath.PeakBinName(b)).Append(' ').Append(double.IsNaN(AverageFill(b)) ? "n/a" : P0(AverageFill(b)));
            f.Append("; swings back ").Append(Gained).Append(", cut by a points drop ").Append(Clamps).Append(", fresh starts ").Append(FreshStarts)
                .Append(" - the attack rate lines' verdicts read over the whole fight now: a burst reads too fast, the waits after it too slow");
            lines.Add(f.ToString());
            return lines;
        }

        private static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        private static string P0(double v) => (v * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
    }
}

using System;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// Running mean and POPULATION standard deviation (Welford's method: one pass, numerically
    /// stable, no stored samples, no allocation). A formation is the whole population we describe,
    /// so the spread divides by n, not n − 1. A struct - keep it in an array and <see cref="Clear"/>
    /// it per refresh.
    /// </summary>
    public struct MeanStd
    {
        private int _count;
        private double _mean;
        private double _m2;

        public int Count => _count;

        /// <summary>NaN when empty.</summary>
        public double Mean => _count > 0 ? _mean : double.NaN;

        /// <summary>Population variance (÷ n); NaN when empty, 0 for one value.</summary>
        public double Variance => _count > 0 ? Math.Max(0, _m2 / _count) : double.NaN;

        /// <summary>Population standard deviation; NaN when empty, 0 for one value.</summary>
        public double StdDev => _count > 0 ? Math.Sqrt(Variance) : double.NaN;

        public void Add(double x)
        {
            _count++;
            double delta = x - _mean;
            _mean += delta / _count;
            _m2 += delta * (x - _mean);
        }

        public void Clear()
        {
            _count = 0;
            _mean = 0;
            _m2 = 0;
        }
    }

    /// <summary>
    /// One formation's Athletics at the last refresh (steps 8-9 draw it): how many fighters, the
    /// mean ± standard deviation in POINTS and as a FRACTION of each fighter's pool (the same thing
    /// while every pool is equal; Athletics v2 gives per-fighter pools), and how many are exhausted.
    /// </summary>
    public readonly struct FormationAthleticsStats
    {
        public FormationAthleticsStats(int count, double meanPoints, double stdPoints, double meanFraction, double stdFraction, int exhausted,
            double meanPeakShare = double.NaN, int inPeak = 0)
        {
            Count = count;
            MeanPoints = meanPoints;
            StdPoints = stdPoints;
            MeanFraction = meanFraction;
            StdFraction = stdFraction;
            Exhausted = exhausted;
            MeanPeakShare = meanPeakShare;
            InPeak = inPeak;
        }

        public static FormationAthleticsStats From(in MeanStd points, in MeanStd fractions, int exhausted) =>
            new FormationAthleticsStats(points.Count, points.Mean, points.StdDev, fractions.Mean, fractions.StdDev, exhausted);

        /// <summary>With the men's f (DESIGN §2) - the squad bar's colour (steps 8-9).</summary>
        public static FormationAthleticsStats From(in MeanStd points, in MeanStd fractions, in MeanStd peakShares, int exhausted, int inPeak) =>
            new FormationAthleticsStats(points.Count, points.Mean, points.StdDev, fractions.Mean, fractions.StdDev, exhausted, peakShares.Mean, inPeak);

        /// <summary>The men's average f (0..1, the share of each man's own peak line left) - colour
        /// the squad bar by it (1 = green). NaN when unknown / empty.</summary>
        public double MeanPeakShare { get; }

        /// <summary>How many men are in their peak zone (f 1).</summary>
        public int InPeak { get; }

        public int Count { get; }

        public double MeanPoints { get; }

        public double StdPoints { get; }

        /// <summary>0..1.</summary>
        public double MeanFraction { get; }

        public double StdFraction { get; }

        public int Exhausted { get; }

        /// <summary>Lower edge of the ± band, <paramref name="stdDevs"/> deviations below the mean, clamped to [0, 1].</summary>
        public double LowFraction(double stdDevs) => Clamp01(MeanFraction - stdDevs * StdFraction);

        /// <summary>Upper edge of the ± band, clamped to [0, 1].</summary>
        public double HighFraction(double stdDevs) => Clamp01(MeanFraction + stdDevs * StdFraction);

        /// <summary><c>72 ± 8 (40 men, 2 exhausted)</c> - the summary's and the orders panel's wording.</summary>
        public string Describe() =>
            Count == 0 ? "empty"
            : MeanPoints.ToString("0", CultureInfo.InvariantCulture) + " ± " + StdPoints.ToString("0", CultureInfo.InvariantCulture)
              + " (" + Count + (Count == 1 ? " man" : " men") + (Exhausted > 0 ? ", " + Exhausted + " exhausted" : string.Empty) + ")";

        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }

    /// <summary>
    /// Durations (attack intervals, swing lengths) as a fixed histogram - count, mean, median,
    /// min, max without storing samples: bins of <see cref="BinSeconds"/> up to
    /// <see cref="CapSeconds"/>; longer values are counted as <see cref="OverCap"/> (a pause, not an
    /// attack rhythm) and left out. Allocation only at construction. Main thread.
    /// </summary>
    public sealed class IntervalStats
    {
        public const double BinSeconds = 0.05;
        public const double CapSeconds = 30.0;

        private readonly int[] _bins = new int[(int)Math.Round(CapSeconds / BinSeconds)];
        private int _count;
        private double _sum;
        private double _min = double.MaxValue;
        private double _max;
        private int _overCap;

        public int Count => _count;

        public int OverCap => _overCap;

        public double Mean => _count > 0 ? _sum / _count : double.NaN;

        public double Min => _count > 0 ? _min : double.NaN;

        public double Max => _count > 0 ? _max : double.NaN;

        /// <summary>The median, to the centre of its bin (±<see cref="BinSeconds"/>/2); NaN when empty.</summary>
        public double Median
        {
            get
            {
                if (_count == 0) return double.NaN;
                int target = (_count + 1) / 2; // the lower median for an even count
                int seen = 0;
                for (int i = 0; i < _bins.Length; i++)
                {
                    seen += _bins[i];
                    if (seen >= target) return (i + 0.5) * BinSeconds;
                }
                return CapSeconds;
            }
        }

        public void Add(double seconds)
        {
            if (double.IsNaN(seconds) || seconds < 0) return;
            if (seconds > CapSeconds)
            {
                _overCap++;
                return;
            }
            int bin = (int)(seconds / BinSeconds);
            if (bin >= _bins.Length) bin = _bins.Length - 1;
            _bins[bin]++;
            _count++;
            _sum += seconds;
            if (seconds < _min) _min = seconds;
            if (seconds > _max) _max = seconds;
        }

        public void Clear()
        {
            Array.Clear(_bins, 0, _bins.Length);
            _count = 0;
            _sum = 0;
            _min = double.MaxValue;
            _max = 0;
            _overCap = 0;
        }

        /// <summary><c>median 1.33 s, avg 1.52 s (n 250)</c> or <c>no samples</c>.</summary>
        public string Describe() =>
            _count == 0 ? "no samples" + (_overCap > 0 ? " (" + _overCap + " longer than " + CapSeconds.ToString("0", CultureInfo.InvariantCulture) + " s left out)" : string.Empty)
            : "median " + S2(Median) + " s, avg " + S2(Mean) + " s (n " + _count + ")";

        private static string S2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The in-game test of the attack-speed penalty (RESEARCH UNVERIFIED #1: does the engine honour
    /// a 0.2 multiplier or clamp it?): compares the median while exhausted with the median while
    /// fresh and says plainly whether exhausted attacks were slower.
    /// </summary>
    public static class SpeedVerdict
    {
        /// <summary>Exhausted must take at least this many times as long to count as "clearly slower".</summary>
        public const double ClearlySlowerRatio = 1.5;

        public const int MinFreshSamples = 5;
        public const int MinExhaustedSamples = 3;

        /// <summary>
        /// <c>fresh median 1.30 s, avg 1.45 s (n 250) | exhausted median 6.10 s, avg 6.40 s (n 30) → x4.69 slower (asked x5.00) - exhausted attacks ARE slower</c>
        /// </summary>
        public static string Describe(IntervalStats fresh, IntervalStats exhausted, int exhaustedPercent)
        {
            double asked = exhaustedPercent > 0 ? 100.0 / exhaustedPercent : double.PositiveInfinity;
            string head = "fresh " + fresh.Describe() + " | exhausted " + exhausted.Describe();
            if (fresh.Count < MinFreshSamples || exhausted.Count < MinExhaustedSamples)
                return head + " - not enough samples to judge (need " + MinFreshSamples + " fresh and " + MinExhaustedSamples + " exhausted)";
            double ratio = exhausted.Median / fresh.Median;
            string cmp = " → x" + ratio.ToString("0.00", CultureInfo.InvariantCulture) + " (asked x" + asked.ToString("0.00", CultureInfo.InvariantCulture) + ")";
            if (asked < ClearlySlowerRatio)
                return head + cmp + " - the setting asks for less than x" + ClearlySlowerRatio.ToString("0.0", CultureInfo.InvariantCulture) + ", no verdict";
            return ratio >= ClearlySlowerRatio
                ? head + cmp + " - exhausted attacks ARE slower"
                : head + cmp + " - exhausted attacks are NOT clearly slower: the engine may clamp the multiplier (RESEARCH UNVERIFIED #1) - tell Claude";
        }
    }

    /// <summary>
    /// Attack timings binned by the fighter's f (DESIGN §2: the share of his peak line left) - the
    /// in-game test of the attack-speed CURVE (step 5c): peak (f 1), 0.5-1, below 0.5, empty (f 0).
    /// Each sample carries the multiplier that was applied (asked), so the line can say "x2.40
    /// slower, asked x2.50". An interval whose two ends fall in different bins is left out
    /// (<see cref="Mixed"/>). Main thread; allocation only at construction.
    /// </summary>
    public sealed class BinnedIntervals
    {
        private readonly IntervalStats[] _bins = new IntervalStats[AthleticsMath.PeakBins];
        private readonly MeanStd[] _asked = new MeanStd[AthleticsMath.PeakBins];

        public BinnedIntervals()
        {
            for (int i = 0; i < _bins.Length; i++) _bins[i] = new IntervalStats();
        }

        /// <summary>Intervals left out because f changed bins between the two ends.</summary>
        public int Mixed;

        public IntervalStats Bin(int bin) => _bins[bin];

        /// <summary>The average attack-speed multiplier applied to the samples of a bin (NaN when none).</summary>
        public double AskedMean(int bin) => _asked[bin].Mean;

        public int Count
        {
            get
            {
                int n = 0;
                foreach (var b in _bins) n += b.Count;
                return n;
            }
        }

        /// <summary>One interval of <paramref name="seconds"/> in <paramref name="bin"/>, struck at the
        /// attack-speed multiplier <paramref name="asked"/>. Pauses longer than the cap are left out.</summary>
        public void Add(int bin, double seconds, float asked)
        {
            if (bin < 0 || bin >= _bins.Length || double.IsNaN(seconds) || seconds < 0) return;
            _bins[bin].Add(seconds);
            if (seconds <= IntervalStats.CapSeconds) _asked[bin].Add(asked);
        }

        /// <summary>
        /// <c>peak (f 1) median 1.35 s (n 250) | f 0.5-1 median 1.50 s x1.11 (asked x1.14, n 80) | … | empty (f 0) median 6.10 s x4.52 (asked x5.00, n 30) - tired attacks ARE slower</c>
        /// The verdict compares the tiredest bin with enough samples (empty, else below 0.5) with the
        /// peak bin: at least x1.5 slower when at least x1.5 was asked = ARE slower.
        /// </summary>
        public string Describe()
        {
            var fresh = _bins[0];
            var sb = new StringBuilder();
            for (int b = 0; b < _bins.Length; b++)
            {
                var s = _bins[b];
                if (b > 0 && s.Count == 0 && s.OverCap == 0) continue;
                if (sb.Length > 0) sb.Append(" | ");
                sb.Append(AthleticsMath.PeakBinName(b)).Append(' ');
                if (s.Count == 0)
                {
                    sb.Append(s.Describe());
                    continue;
                }
                sb.Append("median ").Append(S2(s.Median)).Append(" s");
                if (b > 0 && fresh.Count > 0)
                    sb.Append(" x").Append(S2(s.Median / fresh.Median)).Append(" (asked x").Append(S2(1.0 / _asked[b].Mean)).Append(", n ").Append(s.Count).Append(')');
                else
                    sb.Append(" (n ").Append(s.Count).Append(')');
            }
            return sb + " - " + Verdict();
        }

        private string Verdict()
        {
            var fresh = _bins[0];
            int judged = _bins[3].Count >= SpeedVerdict.MinExhaustedSamples ? 3 : _bins[2].Count >= SpeedVerdict.MinExhaustedSamples ? 2 : -1;
            if (fresh.Count < SpeedVerdict.MinFreshSamples || judged < 0)
                return "not enough samples to judge (need " + SpeedVerdict.MinFreshSamples + " at the peak and " + SpeedVerdict.MinExhaustedSamples
                       + " below 0.5 or empty)";
            double asked = 1.0 / _asked[judged].Mean;
            double ratio = _bins[judged].Median / fresh.Median;
            string which = " (" + AthleticsMath.PeakBinName(judged) + " vs the peak)";
            if (asked < SpeedVerdict.ClearlySlowerRatio)
                return "the setting asks for less than x" + SpeedVerdict.ClearlySlowerRatio.ToString("0.0", CultureInfo.InvariantCulture) + which + ", no verdict";
            return ratio >= SpeedVerdict.ClearlySlowerRatio
                ? "tired attacks ARE slower" + which
                : "tired attacks are NOT clearly slower" + which + ": the engine may clamp the multiplier (RESEARCH UNVERIFIED #1) - tell Claude";
        }

        private static string S2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The in-game test of the run-speed curve (step 5c): samples binned by f, each relative to the
    /// fighter's OWN top speed while fresh (so light and heavy troops compare): the engine's
    /// current top speed ÷ fresh top (does the engine's top follow our multiplier?), the multiplier
    /// asked, and how fast he really moved (speed ÷ fresh top: the 90th percentile and the maximum).
    /// Main thread; allocation only at construction.
    /// </summary>
    public sealed class RunSpeedCheck
    {
        private const double RatioBin = 0.05;
        private const int RatioBins = 30; // 0 .. 1.5
        public const int MinSamples = 20;
        public const double FollowsWithin = 0.05;

        private readonly MeanStd[] _engine = new MeanStd[AthleticsMath.PeakBins];
        private readonly MeanStd[] _asked = new MeanStd[AthleticsMath.PeakBins];
        private readonly int[][] _actual = new int[AthleticsMath.PeakBins][];
        private readonly double[] _actualMax = new double[AthleticsMath.PeakBins];

        public RunSpeedCheck()
        {
            for (int i = 0; i < _actual.Length; i++) _actual[i] = new int[RatioBins + 1];
        }

        public int Samples(int bin) => _engine[bin].Count;

        public double EngineMean(int bin) => _engine[bin].Mean;

        public double AskedMean(int bin) => _asked[bin].Mean;

        public double ActualMax(int bin) => _engine[bin].Count > 0 ? _actualMax[bin] : double.NaN;

        public int Total
        {
            get
            {
                int n = 0;
                foreach (var e in _engine) n += e.Count;
                return n;
            }
        }

        /// <param name="engineTopRatio">The engine's current top speed ÷ the fighter's fresh top.</param>
        /// <param name="asked">The multiplier applied to him (1 = none).</param>
        /// <param name="actualRatio">His speed now ÷ his fresh top.</param>
        public void Add(int bin, double engineTopRatio, double asked, double actualRatio)
        {
            if (bin < 0 || bin >= _engine.Length || double.IsNaN(engineTopRatio) || double.IsNaN(actualRatio)) return;
            _engine[bin].Add(engineTopRatio);
            _asked[bin].Add(asked);
            int k = actualRatio < 0 ? 0 : (int)(actualRatio / RatioBin + 1e-9); // 0.30 / 0.05 is 5.999… in doubles
            _actual[bin][k > RatioBins ? RatioBins : k]++;
            if (actualRatio > _actualMax[bin]) _actualMax[bin] = actualRatio;
        }

        /// <summary>The 90th percentile of speed ÷ fresh top in a bin, as the upper edge of its 0.05
        /// slice ("90% of the samples moved at or below this"); NaN when empty.</summary>
        public double ActualP90(int bin)
        {
            int n = _engine[bin].Count;
            if (n == 0) return double.NaN;
            int target = (int)Math.Ceiling(n * 0.9);
            int seen = 0;
            for (int k = 0; k <= RatioBins; k++)
            {
                seen += _actual[bin][k];
                if (seen >= target) return (k + 1) * RatioBin;
            }
            return (RatioBins + 1) * RatioBin;
        }

        /// <summary>
        /// <c>peak (f 1) engine top x1.00 asked x1.00, moving p90 x0.95 max x1.05 (n 3000) | … - the engine's top speed follows the curve</c>
        /// (p90 = 90% of the samples moved at or below that share of the fresh top speed).
        /// <paramref name="curveApplies"/> false (horses with MountMinSpeedMultiplier 1): the verdict
        /// checks that nothing was slowed instead.
        /// </summary>
        public string Describe(bool curveApplies)
        {
            if (Total == 0) return "no samples";
            var sb = new StringBuilder();
            for (int b = 0; b < _engine.Length; b++)
            {
                int n = _engine[b].Count;
                if (n == 0) continue;
                if (sb.Length > 0) sb.Append(" | ");
                sb.Append(AthleticsMath.PeakBinName(b)).Append(" engine top x").Append(S2(_engine[b].Mean)).Append(" asked x").Append(S2(_asked[b].Mean))
                  .Append(", moving p90 x").Append(S2(ActualP90(b))).Append(" max x").Append(S2(_actualMax[b])).Append(" (n ").Append(n).Append(')');
            }
            return sb + " - " + Verdict(curveApplies);
        }

        private string Verdict(bool curveApplies)
        {
            bool judged = false;
            for (int b = 0; b < _engine.Length; b++)
            {
                if (_engine[b].Count < MinSamples) continue;
                double expected = curveApplies ? _asked[b].Mean : 1.0;
                if (curveApplies && expected > 0.97) continue; // nothing asked in this bin
                judged = true;
                if (Math.Abs(_engine[b].Mean - expected) > FollowsWithin)
                    return curveApplies
                        ? "the engine's top speed does NOT follow the asked curve (" + AthleticsMath.PeakBinName(b) + ": x" + S2(_engine[b].Mean) + " vs asked x"
                          + S2(expected) + ") - compare the moving numbers and tell Claude"
                        : "slowed although nothing was asked (" + AthleticsMath.PeakBinName(b) + ": x" + S2(_engine[b].Mean) + ") - tell Claude";
            }
            if (!judged)
                return curveApplies
                    ? "not enough tired samples to judge (need " + MinSamples + " in a bin below the peak)"
                    : "not enough samples to judge (need " + MinSamples + " in a bin)";
            return curveApplies ? "the engine's top speed follows the curve" : "unaffected, as asked";
        }

        private static string S2(double v) => double.IsNaN(v) ? "n/a" : v.ToString("0.00", CultureInfo.InvariantCulture);
    }
}

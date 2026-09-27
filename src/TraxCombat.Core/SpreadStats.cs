using System;
using System.Globalization;

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
        public FormationAthleticsStats(int count, double meanPoints, double stdPoints, double meanFraction, double stdFraction, int exhausted)
        {
            Count = count;
            MeanPoints = meanPoints;
            StdPoints = stdPoints;
            MeanFraction = meanFraction;
            StdFraction = stdFraction;
            Exhausted = exhausted;
        }

        public static FormationAthleticsStats From(in MeanStd points, in MeanStd fractions, int exhausted) =>
            new FormationAthleticsStats(points.Count, points.Mean, points.StdDev, fractions.Mean, fractions.StdDev, exhausted);

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
}

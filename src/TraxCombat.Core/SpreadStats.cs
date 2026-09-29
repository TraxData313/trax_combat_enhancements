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
    /// while every pool is equal; Athletics v2 gives per-fighter pools), how many are exhausted,
    /// and (step 9) the men's average health left.
    /// </summary>
    public readonly struct FormationAthleticsStats
    {
        public FormationAthleticsStats(int count, double meanPoints, double stdPoints, double meanFraction, double stdFraction, int exhausted,
            double meanPeakShare = double.NaN, int inPeak = 0, double meanHealth = double.NaN, int bracing = 0)
        {
            Bracing = bracing < 0 ? 0 : bracing > count ? count : bracing;
            Count = count;
            MeanPoints = meanPoints;
            StdPoints = stdPoints;
            MeanFraction = meanFraction;
            StdFraction = stdFraction;
            Exhausted = exhausted;
            MeanPeakShare = meanPeakShare;
            InPeak = inPeak;
            MeanHealth = meanHealth;
        }

        public static FormationAthleticsStats From(in MeanStd points, in MeanStd fractions, int exhausted) =>
            new FormationAthleticsStats(points.Count, points.Mean, points.StdDev, fractions.Mean, fractions.StdDev, exhausted);

        /// <summary>With the men's f (DESIGN §2) - the squad bar's colour (steps 8-9).</summary>
        public static FormationAthleticsStats From(in MeanStd points, in MeanStd fractions, in MeanStd peakShares, int exhausted, int inPeak) =>
            new FormationAthleticsStats(points.Count, points.Mean, points.StdDev, fractions.Mean, fractions.StdDev, exhausted, peakShares.Mean, inPeak);

        /// <summary>With the men's health left (0..1 each) too - the strip's "HP 81%" (step 9); step 23: how many brace now.</summary>
        public static FormationAthleticsStats From(in MeanStd points, in MeanStd fractions, in MeanStd peakShares, in MeanStd health, int exhausted, int inPeak, int bracing = 0) =>
            new FormationAthleticsStats(points.Count, points.Mean, points.StdDev, fractions.Mean, fractions.StdDev, exhausted, peakShares.Mean, inPeak, health.Mean, bracing);

        /// <summary>Step 23: how many of <see cref="Count"/> brace now (no melee attacks, guard up - AI men only).</summary>
        public int Bracing { get; }

        /// <summary>Step 23: how many are READY to fight - not bracing (<see cref="Count"/> minus <see cref="Bracing"/>).</summary>
        public int Ready => Count - Bracing;

        /// <summary>Step 23: the formation's size at the refresh (= <see cref="Count"/>).</summary>
        public int Total => Count;

        /// <summary>The men's average health left, 0..1 (health ÷ its maximum, each man counted once).
        /// NaN when unknown / empty.</summary>
        public double MeanHealth { get; }

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

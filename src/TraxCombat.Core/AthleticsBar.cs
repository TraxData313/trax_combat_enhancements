using System;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>
    /// The colour of an Athletics bar (DESIGN §3 additions) - chosen by f, the share of the peak
    /// line left (<see cref="AthleticsMath.PeakShare"/>). The order is the order of alarm.
    /// </summary>
    public enum BarBand
    {
        /// <summary>f = 1: at or above the peak line - full strength.</summary>
        Green = 0,

        /// <summary>Just below the peak line (f above BarYellowBelowPercent).</summary>
        Blue = 1,

        /// <summary>f at or below BarYellowBelowPercent %.</summary>
        Yellow = 2,

        /// <summary>f at or below BarOrangeBelowPercent %.</summary>
        Orange = 3,

        /// <summary>f at or below BarRedBelowPercent % - and always at 0 (exhausted).</summary>
        Red = 4,
    }

    /// <summary>The bar-colour settings in effect for one refresh, read live (a struct: no allocation).</summary>
    public readonly struct BarRules
    {
        public BarRules(int yellowBelowPercent, int orangeBelowPercent, int redBelowPercent)
        {
            YellowBelowPercent = yellowBelowPercent;
            OrangeBelowPercent = orangeBelowPercent;
            RedBelowPercent = redBelowPercent;
        }

        /// <summary>BarYellowBelowPercent - % of the peak line.</summary>
        public int YellowBelowPercent { get; }

        /// <summary>BarOrangeBelowPercent - % of the peak line.</summary>
        public int OrangeBelowPercent { get; }

        /// <summary>BarRedBelowPercent - % of the peak line.</summary>
        public int RedBelowPercent { get; }

        /// <summary>The live values, read now.</summary>
        public static BarRules From(TraxSettings s) => new BarRules(s.BarYellowBelowPercent, s.BarOrangeBelowPercent, s.BarRedBelowPercent);

        /// <summary>"yellow ≤ 75%, orange ≤ 50%, red ≤ 25% of the peak line" - for the log.</summary>
        public string Describe() => "yellow at or below " + YellowBelowPercent + "%, orange " + OrangeBelowPercent + "%, red "
                                    + RedBelowPercent + "% of the peak line (green at or above it, blue just below)";
    }

    /// <summary>
    /// The Athletics bar's maths (step 6; the target bar of step 7 reuses it) - everything the HUD
    /// shows, as pure functions, so the view only copies values into its ViewModel:
    ///   <see cref="Band"/>          the colour from f and the live thresholds
    ///   <see cref="Fill"/>, <see cref="Usable"/>, <see cref="PeakLine"/>  the three shares the
    ///                               prefab's FillBarWidgets draw (0..1 of the bar - pixel-free, so
    ///                               the game's UI scale applies by itself)
    ///   <see cref="DisplayNumbers"/> the "132 / 180" the player reads
    ///   <see cref="ColorHex"/>      the colours (#RRGGBBAA, the game's colour strings)
    /// </summary>
    public static class BarMath
    {
        /// <summary>How many bands there are (the summary's per-band arrays).</summary>
        public const int BandCount = 5;

        // The colours - the look, not a tunable (no MCM colour pickers exist); one place to change.
        public const string GreenHex = "#5CB85CFF";
        public const string BlueHex = "#4A90D9FF";
        public const string YellowHex = "#E8C93AFF";
        public const string OrangeHex = "#EE8A2AFF";
        public const string RedHex = "#D9392BFF";

        /// <summary>The number and the label in normal play.</summary>
        public const string TextHex = "#EDEDEDFF";

        /// <summary>The label, a little quieter than the number.</summary>
        public const string LabelHex = "#BDBDBDFF";

        /// <summary>The bar's thin frame in normal play.</summary>
        public const string FrameHex = "#C8C8C8B4";

        /// <summary>Number, label and frame while the bar is empty (exhausted).</summary>
        public const string ExhaustedHex = "#FF4B3AFF";

        /// <summary>The label in normal play and while empty.</summary>
        public const string LabelText = "Athletics";
        public const string ExhaustedLabelText = "Exhausted";

        /// <summary>
        /// The colour for f (0..1, 1 = at or above the peak line): green at 1; else red / orange /
        /// yellow at or below their percent of the line (the most alarming band that applies wins,
        /// so thresholds set out of order still make sense); else blue. f = 0 is always red (an
        /// empty bar). NaN reads as full strength (fail safe: no false alarm).
        /// </summary>
        public static BarBand Band(in BarRules r, double peakShare)
        {
            if (double.IsNaN(peakShare) || peakShare >= 1.0) return BarBand.Green;
            if (peakShare <= AthleticsMath.Epsilon) return BarBand.Red;
            double pct = peakShare * 100.0;
            if (pct <= r.RedBelowPercent + 1e-9) return BarBand.Red;
            if (pct <= r.OrangeBelowPercent + 1e-9) return BarBand.Orange;
            if (pct <= r.YellowBelowPercent + 1e-9) return BarBand.Yellow;
            return BarBand.Blue;
        }

        /// <summary>"green" … "red".</summary>
        public static string Name(BarBand b) => b switch
        {
            BarBand.Green => "green",
            BarBand.Blue => "blue",
            BarBand.Yellow => "yellow",
            BarBand.Orange => "orange",
            _ => "red",
        };

        /// <summary>"green (peak zone - full strength)" … for the log.</summary>
        public static string Describe(BarBand b) => b switch
        {
            BarBand.Green => "green (peak zone - full strength)",
            BarBand.Blue => "blue (just below the peak line)",
            BarBand.Yellow => "yellow",
            BarBand.Orange => "orange",
            _ => "red",
        };

        /// <summary>The fill colour of a band, #RRGGBBAA.</summary>
        public static string ColorHex(BarBand b) => b switch
        {
            BarBand.Green => GreenHex,
            BarBand.Blue => BlueHex,
            BarBand.Yellow => YellowHex,
            BarBand.Orange => OrangeHex,
            _ => RedHex,
        };

        /// <summary>The fill: points ÷ pool, kept inside 0..1 (NaN → 0: draw nothing rather than a lie).</summary>
        public static float Fill(double fraction) => (float)Clamp01(fraction, 0);

        /// <summary>Where the dark (wounded) part starts: the usable share, 0..1 (NaN → 1: no cap drawn).</summary>
        public static float Usable(double usableFraction) => (float)Clamp01(usableFraction, 1);

        /// <summary>The peak marker's place: the peak line as a share of the bar, 0..1.</summary>
        public static float PeakLine(double peakFraction) => (float)Clamp01(peakFraction, 1);

        /// <summary>The part of the bar the wounds hold (1 − usable), 0..1 - for the log.</summary>
        public static double Capped(double usableFraction) => 1.0 - Clamp01(usableFraction, 1);

        /// <summary>
        /// The two numbers of "132 / 180". The pool is rounded (never below 1). The points are
        /// rounded UP - so the bar reads 0 only when it is really empty (E = 0, exhausted), a fresh
        /// bar reads its pool, and 0.3 points left reads 1 - and never above the shown pool.
        /// </summary>
        public static void DisplayNumbers(double points, double pool, out int shownPoints, out int shownPool)
        {
            shownPool = double.IsNaN(pool) || pool < 1.5 ? 1 : (int)Math.Round(Math.Min(pool, 1e6), MidpointRounding.AwayFromZero);
            if (double.IsNaN(points) || points <= AthleticsMath.Epsilon)
            {
                shownPoints = 0;
                return;
            }
            double up = Math.Ceiling(Math.Min(points, 1e6) - 1e-6);
            shownPoints = up < 1 ? 1 : up > shownPool ? shownPool : (int)up;
        }

        /// <summary>"132 / 180".</summary>
        public static string NumberText(int shownPoints, int shownPool) =>
            shownPoints.ToString(CultureInfo.InvariantCulture) + " / " + shownPool.ToString(CultureInfo.InvariantCulture);

        private static double Clamp01(double v, double ifNaN) => double.IsNaN(v) ? ifNaN : v < 0 ? 0 : v > 1 ? 1 : v;
    }
}

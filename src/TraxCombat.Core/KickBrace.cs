using System;
using System.Collections.Generic;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>Step 65: what became of a landed kick / bash on a man on foot, as the brace decided it.</summary>
    public enum KickBraceOutcome : byte
    {
        /// <summary>Not asked: the brace off, a rider, a man already on the ground, not a kick / bash.</summary>
        NotAsked,

        /// <summary>He BRACED: no knockdown, no shield drop, no knock-back (refused) and what reaction is left cut to
        /// <c>KickBraceReelPercent</c>.</summary>
        Braced,

        /// <summary>The brace failed and his reel is SHORTER than the game's (a fresh man's lucky kick: <c>KickShortReelPercent</c>, growing
        /// with his tiredness).</summary>
        ShortReel,

        /// <summary>The brace failed (or he was spent - no brace at all) and his reel is the game's or longer (step 50's tired reel).</summary>
        FullReel,

        /// <summary>The brace failed and step 50's dice knocked him down.</summary>
        Down,
    }

    /// <summary>
    /// Step 65 (Anton 2026-10-19: "make sure a looter kicking a heavy shielded troop doesn't do too much - maybe now and then a lucky kick can
    /// stagger him for a fast bit"): THE KICKED MAN BRACES - the rules, read live. <see cref="On"/> = the master switch FIRST, then
    /// <c>KickBraceEnabled</c> (the impact model's own gates - Athletics, ImpactEnabled, the kicks' family - come first in its decision).
    /// </summary>
    public readonly struct KickBraceRules
    {
        public KickBraceRules(bool modEnabled, bool enabled, double fromPressure, double toPressure, int minBarPercent, int braceReelPercent,
            int shortReelPercent, bool refusesKnockBack)
        {
            ModEnabled = modEnabled;
            Enabled = enabled;
            FromPressure = double.IsNaN(fromPressure) ? -0.7 : fromPressure;
            ToPressure = double.IsNaN(toPressure) ? 1.0 : toPressure;
            MinBar = Clamp01(minBarPercent / 100.0);
            BraceReel = Math.Max(KickBraceMath.ShortestShare, Math.Min(1, braceReelPercent / 100.0));
            ShortReel = Math.Max(KickBraceMath.ShortestShare, Math.Min(1, shortReelPercent / 100.0));
            RefusesKnockBack = refusesKnockBack;
        }

        public static KickBraceRules From(TraxSettings s) => new KickBraceRules(s.ModEnabled, s.KickBraceEnabled, s.KickBraceFromPressure,
            s.KickBraceToPressure, s.KickBraceMinBarPercent, s.KickBraceReelPercent, s.KickShortReelPercent, s.KickBraceRefusesKnockBack);

        public bool ModEnabled { get; }
        public bool Enabled { get; }

        /// <summary>The brace is asked: the master switch first, then <c>KickBraceEnabled</c>.</summary>
        public bool On => ModEnabled && Enabled;

        /// <summary><c>KickBraceFromPressure</c> - below it he always braces.</summary>
        public double FromPressure { get; }

        /// <summary><c>KickBraceToPressure</c> - at or above it the brace always fails.</summary>
        public double ToPressure { get; }

        /// <summary><c>KickBraceMinBarPercent</c> ÷ 100 - below this bar no brace at all (a spent man).</summary>
        public double MinBar { get; }

        /// <summary><c>KickBraceReelPercent</c> ÷ 100 - a braced man's reaction length (x the game's), never below 1/8.</summary>
        public double BraceReel { get; }

        /// <summary><c>KickShortReelPercent</c> ÷ 100 - a FRESH man's reel when the brace fails (x the game's), never below 1/8.</summary>
        public double ShortReel { get; }

        /// <summary><c>KickBraceRefusesKnockBack</c> - a braced man's knock-back flag refused in the decorator's knock-back seam.</summary>
        public bool RefusesKnockBack { get; }

        public string? OffBecause => !ModEnabled ? "ModEnabled" : !Enabled ? "KickBraceEnabled" : null;

        private static double Clamp01(double v) => double.IsNaN(v) || v < 0 ? 0 : v > 1 ? 1 : v;

        /// <summary>The settings in one sentence (the mission-start line, the summary).</summary>
        public string Describe()
        {
            if (!On) return "OFF (" + OffBecause + ") - every landed kick / bash knocks a man back as step 50 decides";
            return "ON - a kicked / bashed man on foot braces unless the kick's pressure x fails it: never below x " + KickBraceMath.F2(FromPressure) + ", always from x "
                   + KickBraceMath.F2(ToPressure) + " (an S-curve between; the same x as the staggers and knockdowns - his tiredness, both men's strength, rank and skill, "
                   + "a blow from behind); braced = no knockdown, no shield drop, " + (RefusesKnockBack ? "the knock-back REFUSED, " : string.Empty)
                   + "the reaction cut to " + KickBraceMath.P0(BraceReel) + "; failed = a fresh man reels " + KickBraceMath.P0(ShortReel)
                   + " of the game's knock-back, growing to step 50's full (tired) reel as he tires; below " + KickBraceMath.P0(MinBar)
                   + " of his bar no brace at all (KickBraceFromPressure / ToPressure / MinBarPercent / ReelPercent, KickShortReelPercent, KickBraceRefusesKnockBack)";
        }
    }

    /// <summary>Step 65 pure: the brace's failure chance, the reel cut, the bands of the summary.</summary>
    public static class KickBraceMath
    {
        /// <summary>Plumbing: the shortest share of a reaction ever asked (1/8 = the engine asked to play it at 8 times its speed at most -
        /// <see cref="ImpactLiveMath.MaxSpeed"/>).</summary>
        public const double ShortestShare = 0.125;

        /// <summary>The summary's tier bands of the KICKED man, by his toughness (courage's measure, step 62: armour, level, health).</summary>
        public const int TierBandCount = 4;

        /// <summary>The summary's outcomes (braced, short reel, full reel, down).</summary>
        public const int OutcomeCount = 4;

        /// <summary>
        /// The chance the brace FAILS at pressure <paramref name="x"/>: S((x − from) ÷ (to − from)) - 0 below from, 1 at or above to (a "to"
        /// at or below "from" = a step at from). The player's share (<c>ImpactOnPlayerPercent</c>) scales it when the kicked man is you.
        /// </summary>
        public static double FailChance(in KickBraceRules r, double x)
        {
            if (double.IsNaN(x)) return 1;
            if (!(r.ToPressure > r.FromPressure)) return x >= r.FromPressure ? 1 : 0;
            return ImpactMath.S((x - r.FromPressure) / (r.ToPressure - r.FromPressure));
        }

        /// <summary>A spent man never braces: his bar below <c>KickBraceMinBarPercent</c>.</summary>
        public static bool Spent(in KickBraceRules r, double bar) => !double.IsNaN(bar) && bar < r.MinBar;

        /// <summary>The cut on a FAILED brace's reel: short + (1 − short) × T - the short reel fresh (T 0), the game's full reel empty (T 1).</summary>
        public static double ReelCut(in KickBraceRules r, double tired)
        {
            double t = double.IsNaN(tired) ? 0 : Math.Max(0, Math.Min(1, tired));
            return r.ShortReel + (1 - r.ShortReel) * t;
        }

        /// <summary>The outcome of one decided kick: braced; down (step 50's knockdown); a reel shorter than the game's; else full.</summary>
        public static KickBraceOutcome Classify(bool braced, bool knockedDown, double reelFactor)
        {
            if (braced) return KickBraceOutcome.Braced;
            if (knockedDown) return KickBraceOutcome.Down;
            return !double.IsNaN(reelFactor) && reelFactor < 1 - 1e-6 ? KickBraceOutcome.ShortReel : KickBraceOutcome.FullReel;
        }

        /// <summary>The kicked man's tier band by his toughness: light (below 2 - looters, recruits, peasants), medium (2-4), heavy (4-7 -
        /// legionaries, veterans), elite (7+ - plate, lords). Unknown (0 or less) = light.</summary>
        public static int TierBand(double toughness)
        {
            if (double.IsNaN(toughness) || toughness < 2) return 0;
            if (toughness < 4) return 1;
            if (toughness < 7) return 2;
            return 3;
        }

        public static string TierName(int band) => band switch
        {
            0 => "light (toughness < 2)",
            1 => "medium (2-4)",
            2 => "heavy (4-7)",
            _ => "elite (7+)",
        };

        public static string OutcomeName(KickBraceOutcome o) => o switch
        {
            KickBraceOutcome.Braced => "braced",
            KickBraceOutcome.ShortReel => "short reel",
            KickBraceOutcome.FullReel => "full reel",
            KickBraceOutcome.Down => "knocked down",
            _ => "not asked",
        };

        internal static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        internal static string P0(double share) => (100 * share).ToString("0", CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>
    /// Step 65 per mission: every decided kick / bash the brace was asked about - its outcome by the kicked man's tier band and bar band, the
    /// spent men (no brace), the knock-backs refused in the seam, the braced reactions seen and timed (the engine's leftover reaction at the cut
    /// speed) and the short reels timed, you as the kicked man and as the kicker → the [summary] "kick brace" lines. Main thread.
    /// </summary>
    public sealed class KickBraceStats
    {
        private readonly int[,] _byTier = new int[KickBraceMath.TierBandCount, KickBraceMath.OutcomeCount];
        private readonly int[,] _byBar = new int[ImpactMath.BandCount, KickBraceMath.OutcomeCount];
        private readonly double[] _failChance = new double[KickBraceMath.TierBandCount];
        private readonly int[] _asked = new int[KickBraceMath.TierBandCount];

        /// <summary>Decided kicks / bashes the brace was asked about; of them on a spent man (no brace); on YOU / BY you (by outcome).</summary>
        public int Asked, Spent, Bashes;
        public readonly int[] OnYou = new int[KickBraceMath.OutcomeCount];
        public readonly int[] ByYou = new int[KickBraceMath.OutcomeCount];

        /// <summary>The decorator's knock-back seam: kicks asked, the game's knock-back refused for a braced man, kept (not braced / the switch
        /// off), a braced man's knock-back kept because KickBraceRefusesKnockBack is off.</summary>
        public int KnockBackAsked, KnockBackRefused, KnockBackKeptBraceOff;

        /// <summary>The cut reactions watched: a braced man's (seen / not seen - the engine may play nothing once the knock-back is refused) and
        /// a short reel's; their measured lengths (s) and asked factors.</summary>
        public int BracedSeen, BracedNotSeen, ShortSeen, ShortNotSeen, SpeedCalls;
        public MeanStd BracedLength, ShortLength, BracedAsked, ShortAsked;

        /// <summary>Not watched (the pool full / the watch lever off).</summary>
        public int NotWatched;

        public int Outcome(KickBraceOutcome o) => o == KickBraceOutcome.NotAsked ? 0 : SumTier((int)o - 1);

        public int ByTier(int band, KickBraceOutcome o) => o == KickBraceOutcome.NotAsked ? 0 : _byTier[band, (int)o - 1];

        public int ByBar(int band, KickBraceOutcome o) => o == KickBraceOutcome.NotAsked ? 0 : _byBar[band, (int)o - 1];

        private int SumTier(int o)
        {
            int n = 0;
            for (int t = 0; t < KickBraceMath.TierBandCount; t++) n += _byTier[t, o];
            return n;
        }

        /// <summary>One decided kick / bash (its brace asked): by tier and bar band, the failure chance for the mean, you.</summary>
        public void Note(KickBraceOutcome o, double toughness, double bar, double failChance, bool spent, bool bash, bool onYou, bool byYou)
        {
            if (o == KickBraceOutcome.NotAsked) return;
            int k = (int)o - 1;
            int tier = KickBraceMath.TierBand(toughness);
            Asked++;
            if (spent) Spent++;
            if (bash) Bashes++;
            _byTier[tier, k]++;
            _byBar[ImpactMath.BarBand(bar), k]++;
            _asked[tier]++;
            _failChance[tier] += double.IsNaN(failChance) ? 1 : failChance;
            if (onYou) OnYou[k]++;
            if (byYou) ByYou[k]++;
        }

        /// <summary>The [summary] lines.</summary>
        public IEnumerable<string> SummaryLines(in KickBraceRules r)
        {
            var lines = new List<string>();
            if (Asked == 0)
            {
                lines.Add("kick brace (step 65): " + r.Describe() + " - no landed kick / bash on a man on foot was decided");
                return lines;
            }
            lines.Add("kick brace (step 65): " + Asked + " landed kicks / bashes decided (" + Bashes + " bashes): braced " + Cell(Outcome(KickBraceOutcome.Braced), Asked)
                      + ", short reel " + Cell(Outcome(KickBraceOutcome.ShortReel), Asked) + ", full reel " + Cell(Outcome(KickBraceOutcome.FullReel), Asked)
                      + ", knocked down " + Cell(Outcome(KickBraceOutcome.Down), Asked) + "; on a SPENT man (below " + KickBraceMath.P0(r.MinBar) + " of his bar - no brace) "
                      + Spent + "; " + (r.On ? "the brace ON" : "the brace " + r.Describe()));
            var sb = new System.Text.StringBuilder("kick brace by the kicked man's tier (toughness): ");
            for (int t = 0; t < KickBraceMath.TierBandCount; t++)
            {
                if (t > 0) sb.Append(" | ");
                sb.Append(KickBraceMath.TierName(t)).Append(' ').Append(Row(t, true));
                if (_asked[t] > 0) sb.Append(" (the model's mean failure chance ").Append(KickBraceMath.P0(_failChance[t] / _asked[t])).Append(')');
            }
            lines.Add(sb.ToString());
            sb.Clear().Append("kick brace by the kicked man's bar: ");
            for (int b = 0; b < ImpactMath.BandCount; b++)
            {
                if (b > 0) sb.Append(" | ");
                sb.Append(ImpactMath.BandName(b)).Append(' ').Append(Row(b, false));
            }
            lines.Add(sb.ToString());
            lines.Add("kick brace in the game: the knock-back seam asked " + KnockBackAsked + ", the knock-back REFUSED for a braced man " + KnockBackRefused
                      + (KnockBackKeptBraceOff > 0 ? ", kept for a braced man (KickBraceRefusesKnockBack off) " + KnockBackKeptBraceOff : string.Empty)
                      + "; braced reactions seen " + BracedSeen + " (" + Secs(in BracedLength) + " at x" + Mean(in BracedAsked) + "), none shown " + BracedNotSeen
                      + "; short reels seen " + ShortSeen + " (" + Secs(in ShortLength) + " at x" + Mean(in ShortAsked) + "), not seen " + ShortNotSeen
                      + "; sped-up calls " + SpeedCalls + (NotWatched > 0 ? ", not watched (pool full / lever off) " + NotWatched : string.Empty));
            lines.Add("kick brace - YOU: kicked " + YouRow(OnYou) + "; your kicks / bashes on others " + YouRow(ByYou));
            return lines;
        }

        private string Row(int band, bool tier)
        {
            int n = 0;
            for (int o = 0; o < KickBraceMath.OutcomeCount; o++) n += tier ? _byTier[band, o] : _byBar[band, o];
            if (n == 0) return "-";
            var c = CultureInfo.InvariantCulture;
            string P(int o) => (100.0 * (tier ? _byTier[band, o] : _byBar[band, o]) / n).ToString("0", c) + "%";
            return n + ": braced " + P(0) + " / short " + P(1) + " / full " + P(2) + " / down " + P(3);
        }

        private static string YouRow(int[] a)
        {
            int n = a[0] + a[1] + a[2] + a[3];
            if (n == 0) return "none";
            return n + " (braced " + a[0] + ", short reel " + a[1] + ", full reel " + a[2] + ", down " + a[3] + ")";
        }

        private static string Cell(int n, int of) => n + " (" + (of > 0 ? (100.0 * n / of).ToString("0.0", CultureInfo.InvariantCulture) : "0") + "%)";

        private static string Secs(in MeanStd m) => m.Count == 0 ? "-" : "mean " + m.Mean.ToString("0.00", CultureInfo.InvariantCulture) + " s";

        private static string Mean(in MeanStd m) => m.Count == 0 ? "-" : m.Mean.ToString("0.00", CultureInfo.InvariantCulture);
    }
}

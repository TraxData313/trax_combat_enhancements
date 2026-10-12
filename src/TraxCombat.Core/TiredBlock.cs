using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 30 (Anton, 2026-10-12: "the more exhausted an AI man is, the more his block direction is random") - the rules,
    /// read LIVE from <see cref="TraxSettings"/>: the master switch first, then AthleticsEnabled, then
    /// TiredBlockRandomPercent above 0. AI_NOTES "Step 30", RESEARCH §M.
    /// </summary>
    public readonly struct TiredBlockRules
    {
        public TiredBlockRules(bool modEnabled, bool athleticsEnabled, int percent)
        {
            ModEnabled = modEnabled;
            AthleticsEnabled = athleticsEnabled;
            Percent = percent;
        }

        public bool ModEnabled { get; }
        public bool AthleticsEnabled { get; }

        /// <summary>TiredBlockRandomPercent: the share of his picks left to chance at EMPTY (× (1 − f) above it).</summary>
        public int Percent { get; }

        public bool Enabled => ModEnabled && AthleticsEnabled && Percent > 0;

        /// <summary>The switch that holds it off (the master switch first), or null when on.</summary>
        public string? OffBecause => !ModEnabled ? "ModEnabled" : !AthleticsEnabled ? "AthleticsEnabled" : Percent <= 0 ? "TiredBlockRandomPercent 0" : null;

        /// <summary>Percent as a share, clamped 0..1.</summary>
        public float Share => Percent <= 0 ? 0f : Percent >= TiredBlockMath.MaxPercent ? 1f : Percent / 100f;

        public static TiredBlockRules From(TraxSettings s) => new TiredBlockRules(s.ModEnabled, s.AthleticsEnabled, s.TiredBlockRandomPercent);

        /// <summary>The settings sentence for the mission-start line and the summary.</summary>
        public string Describe()
        {
            if (!Enabled)
                return "tired block direction: off (" + OffBecause + ") - every AI man blocks as the game decides";
            return "tired block direction: on - below the peak line an AI man's block direction is partly random: weight w = "
                   + Percent + "% × (1 − f) (TiredBlockRandomPercent), so at f 0.5 " + TiredBlockMath.P0(Share * 0.5) + " of his picks and empty "
                   + TiredBlockMath.P0(Share) + " are a random direction (right 1 in 4 by luck); through the game's own AiRandomizedDefendDirectionChance"
                   + " = own + (1 − own) × w; full strength, you and men of the game's own choice untouched";
        }
    }

    /// <summary>Step 30 pure math: the weight, the chance written, the summary's band numbers.</summary>
    public static class TiredBlockMath
    {
        public const int MaxPercent = 100;

        /// <summary>A uniformly random direction of the four is the right one this often (by luck).</summary>
        public const double RightByLuck = 0.25;

        /// <summary>w = Share × (1 − f) - 0 at or above the peak line, while off, and for the player (his mouse picks).</summary>
        public static float Weight(in TiredBlockRules r, double peakShare, bool isPlayer)
        {
            if (!r.Enabled || isPlayer || double.IsNaN(peakShare)) return 0f;
            if (peakShare >= 1.0 - AthleticsMath.Epsilon) return 0f;
            double f = peakShare <= 0 ? 0 : peakShare;
            return (float)(r.Share * (1.0 - f));
        }

        /// <summary>The fighter's w now: f from the Athletics rules (Athletics off → f 1 → 0).</summary>
        public static float Weight(in TiredBlockRules r, in AthleticsRules ar, Fighter f, bool isPlayer) =>
            Weight(in r, AthleticsMath.PeakShare(in ar, f), isPlayer);

        /// <summary>The game's value as a chance: clamped to 0..1 (a final-round tournament fighter's −1.10 → 0), NaN → 0.</summary>
        public static float OwnChance(float game)
        {
            if (float.IsNaN(game) || game <= 0f) return 0f;
            return game >= 1f ? 1f : game;
        }

        /// <summary>
        /// The chance to write: own + (1 − own) × w = the probability that a pick is random when w of them are ours and the
        /// rest his own AI's (itself random at "own"). w ≤ 0 → <paramref name="game"/> UNCHANGED (the game's value stands,
        /// negative or not - the caller does not write).
        /// </summary>
        public static float Chance(float game, float weight)
        {
            if (!(weight > 0f)) return game;
            float own = OwnChance(game);
            float w = weight >= 1f ? 1f : weight;
            return own + (1f - own) * w;
        }

        /// <summary>The probability a block lands in the right direction, own AI assumed always right: 1 − chance × 3/4.</summary>
        public static double RightShare(float chance) => 1.0 - OwnChance(chance) * (1.0 - RightByLuck);

        internal static string P0(double share) => (share * 100).ToString("0", CultureInfo.InvariantCulture) + "%";

        internal static string P1(double share) => (share * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";

        internal static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Step 30 per mission: the chance raised (men, recomputes, his own → written by f band) and the GUARD of AI men against
    /// melee blows by f band (shield right / WRONG side / weapon parry / landed) + the 2 [summary] lines. Main thread.
    /// </summary>
    public sealed class TiredBlockStats
    {
        private readonly int[] _raised = new int[AthleticsMath.PeakBins];
        private readonly double[] _ownSum = new double[AthleticsMath.PeakBins];
        private readonly double[] _chanceSum = new double[AthleticsMath.PeakBins];
        private readonly int[] _blows = new int[AthleticsMath.PeakBins];
        private readonly int[] _shieldRight = new int[AthleticsMath.PeakBins];
        private readonly int[] _shieldWrong = new int[AthleticsMath.PeakBins];
        private readonly int[] _parry = new int[AthleticsMath.PeakBins];
        private readonly int[] _landed = new int[AthleticsMath.PeakBins];

        /// <summary>AI men whose chance was raised at least once.</summary>
        public int Men { get; set; }

        /// <summary>Recomputes that wrote a raised chance.</summary>
        public int Recomputes { get; private set; }

        /// <summary>…of them with the game's own value below 0 (never random before - the tournament's late rounds).</summary>
        public int NegativeOwn { get; private set; }

        /// <summary>A recompute wrote <paramref name="chance"/> over the game's <paramref name="game"/> for a man in f bin
        /// <paramref name="bin"/>.</summary>
        public void AddRaised(int bin, float game, float chance)
        {
            if (bin < 0 || bin >= AthleticsMath.PeakBins) return;
            Recomputes++;
            if (game < 0f) NegativeOwn++;
            _raised[bin]++;
            _ownSum[bin] += TiredBlockMath.OwnChance(game);
            _chanceSum[bin] += chance;
        }

        /// <summary>A melee blow reached an AI man in f bin <paramref name="bin"/>: <see cref="BlockKind.None"/> = it landed.</summary>
        public void AddGuard(int bin, BlockKind kind)
        {
            if (bin < 0 || bin >= AthleticsMath.PeakBins) return;
            _blows[bin]++;
            switch (kind)
            {
                case BlockKind.ShieldRightSide: _shieldRight[bin]++; break;
                case BlockKind.ShieldWrongSide: _shieldWrong[bin]++; break;
                case BlockKind.WeaponParry: _parry[bin]++; break;
                default: _landed[bin]++; break;
            }
        }

        public int Blows(int bin) => _blows[bin];

        public int Landed(int bin) => _landed[bin];

        public int WrongSide(int bin) => _shieldWrong[bin];

        public int Raised(int bin) => _raised[bin];

        /// <summary>The 2 [summary] lines.</summary>
        public List<string> SummaryLines(in TiredBlockRules r)
        {
            var lines = new List<string>(2);
            var t = new StringBuilder(256);
            if (!r.Enabled && Recomputes == 0)
            {
                t.Append("tired block direction (step 30): off at the end (").Append(r.OffBecause).Append(") and never raised this battle - the game's own blocking");
            }
            else
            {
                t.Append("tired block direction (TiredBlockRandomPercent ").Append(r.Percent).Append(", step 30): the game's random-direction chance raised for ")
                    .Append(Men).Append(" AI men (").Append(Recomputes).Append(" recomputes; ").Append(NegativeOwn)
                    .Append(" of them over a game value below 0 - never random before)");
                if (Recomputes > 0)
                {
                    t.Append("; his own → written, avg by f band:");
                    bool first = true;
                    for (int b = 0; b < AthleticsMath.PeakBins; b++)
                    {
                        if (_raised[b] == 0) continue;
                        t.Append(first ? " " : ", ").Append(AthleticsMath.PeakBinName(b)).Append(' ')
                            .Append(TiredBlockMath.F2(_ownSum[b] / _raised[b])).Append(" → ").Append(TiredBlockMath.F2(_chanceSum[b] / _raised[b]))
                            .Append(" (n ").Append(_raised[b]).Append(')');
                        first = false;
                    }
                }
                if (!r.Enabled) t.Append(" - off at the end (").Append(r.OffBecause).Append(')');
            }
            lines.Add(t.ToString());

            t.Clear();
            t.Append("AI guard by f (melee blows that reached AI men; kicks, bashes, charges and missiles left out - step 30: tired men should block worse):");
            bool any = false;
            for (int b = 0; b < AthleticsMath.PeakBins; b++)
            {
                int n = _blows[b];
                t.Append(b == 0 ? " " : "; ").Append(AthleticsMath.PeakBinName(b)).Append(' ');
                if (n == 0)
                {
                    t.Append("none");
                    continue;
                }
                any = true;
                double inv = 1.0 / n;
                t.Append(n).Append(" blows - blocked ").Append(TiredBlockMath.P1((n - _landed[b]) * inv))
                    .Append(" (shield right side ").Append(TiredBlockMath.P1(_shieldRight[b] * inv))
                    .Append(", WRONG side ").Append(TiredBlockMath.P1(_shieldWrong[b] * inv))
                    .Append(", parry ").Append(TiredBlockMath.P1(_parry[b] * inv))
                    .Append("), landed ").Append(TiredBlockMath.P1(_landed[b] * inv));
            }
            if (!any) t.Append(" (no melee blow reached an AI man)");
            lines.Add(t.ToString());
            return lines;
        }
    }
}

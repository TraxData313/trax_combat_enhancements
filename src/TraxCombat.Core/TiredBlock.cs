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
    /// melee blows by f band (shield right / WRONG side / weapon parry / landed) + the [summary] lines. Step 34 (Anton,
    /// 2026-10-12): the guard split by what the DEFENDER holds when the blow arrives - a shield in hand or not - because a
    /// random direction with a shield is only a wrong-side block (the shield still stops it), without one the blow lands; the
    /// recomputes split the same way. 4 lines: the raised chance, the guard of all, with a shield, without. Main thread.
    /// </summary>
    public sealed class TiredBlockStats
    {
        /// <summary>One guard table: blows and their outcome by f band.</summary>
        private sealed class Guard
        {
            public readonly int[] Blows = new int[AthleticsMath.PeakBins];
            public readonly int[] ShieldRight = new int[AthleticsMath.PeakBins];
            public readonly int[] ShieldWrong = new int[AthleticsMath.PeakBins];
            public readonly int[] Parry = new int[AthleticsMath.PeakBins];
            public readonly int[] Landed = new int[AthleticsMath.PeakBins];

            public void Add(int bin, BlockKind kind)
            {
                Blows[bin]++;
                switch (kind)
                {
                    case BlockKind.ShieldRightSide: ShieldRight[bin]++; break;
                    case BlockKind.ShieldWrongSide: ShieldWrong[bin]++; break;
                    case BlockKind.WeaponParry: Parry[bin]++; break;
                    default: Landed[bin]++; break;
                }
            }

            /// <summary>The bands' text; false when no blow reached anyone.</summary>
            public bool Append(StringBuilder t)
            {
                bool any = false;
                for (int b = 0; b < AthleticsMath.PeakBins; b++)
                {
                    int n = Blows[b];
                    t.Append(b == 0 ? " " : "; ").Append(AthleticsMath.PeakBinName(b)).Append(' ');
                    if (n == 0)
                    {
                        t.Append("none");
                        continue;
                    }
                    any = true;
                    double inv = 1.0 / n;
                    t.Append(n).Append(" blows - blocked ").Append(TiredBlockMath.P1((n - Landed[b]) * inv))
                        .Append(" (shield right side ").Append(TiredBlockMath.P1(ShieldRight[b] * inv))
                        .Append(", WRONG side ").Append(TiredBlockMath.P1(ShieldWrong[b] * inv))
                        .Append(", parry ").Append(TiredBlockMath.P1(Parry[b] * inv))
                        .Append("), landed ").Append(TiredBlockMath.P1(Landed[b] * inv));
                }
                return any;
            }
        }

        private readonly int[] _raised = new int[AthleticsMath.PeakBins];
        private readonly double[] _ownSum = new double[AthleticsMath.PeakBins];
        private readonly double[] _chanceSum = new double[AthleticsMath.PeakBins];
        private readonly Guard _all = new Guard();
        private readonly Guard _withShield = new Guard();
        private readonly Guard _noShield = new Guard();

        /// <summary>AI men whose chance was raised at least once.</summary>
        public int Men { get; set; }

        /// <summary>Recomputes that wrote a raised chance.</summary>
        public int Recomputes { get; private set; }

        /// <summary>…of them on a man with a shield in his off hand at the recompute (step 34).</summary>
        public int RecomputesWithShield { get; private set; }

        /// <summary>…of them with the game's own value below 0 (never random before - the tournament's late rounds).</summary>
        public int NegativeOwn { get; private set; }

        /// <summary>A recompute wrote <paramref name="chance"/> over the game's <paramref name="game"/> for a man in f bin
        /// <paramref name="bin"/>; <paramref name="shield"/> = a shield in his off hand then (step 34).</summary>
        public void AddRaised(int bin, float game, float chance, bool shield)
        {
            if (bin < 0 || bin >= AthleticsMath.PeakBins) return;
            Recomputes++;
            if (shield) RecomputesWithShield++;
            if (game < 0f) NegativeOwn++;
            _raised[bin]++;
            _ownSum[bin] += TiredBlockMath.OwnChance(game);
            _chanceSum[bin] += chance;
        }

        /// <summary>A melee blow reached an AI man in f bin <paramref name="bin"/>: <see cref="BlockKind.None"/> = it landed;
        /// <paramref name="shield"/> = he held a shield in his off hand when it arrived (step 34).</summary>
        public void AddGuard(int bin, BlockKind kind, bool shield)
        {
            if (bin < 0 || bin >= AthleticsMath.PeakBins) return;
            _all.Add(bin, kind);
            (shield ? _withShield : _noShield).Add(bin, kind);
        }

        public int Blows(int bin) => _all.Blows[bin];

        public int Landed(int bin) => _all.Landed[bin];

        public int WrongSide(int bin) => _all.ShieldWrong[bin];

        /// <summary>Step 34: the blows on men with (<paramref name="shield"/>) or without a shield in hand.</summary>
        public int Blows(int bin, bool shield) => (shield ? _withShield : _noShield).Blows[bin];

        public int Landed(int bin, bool shield) => (shield ? _withShield : _noShield).Landed[bin];

        public int WrongSide(int bin, bool shield) => (shield ? _withShield : _noShield).ShieldWrong[bin];

        public int Parried(int bin, bool shield) => (shield ? _withShield : _noShield).Parry[bin];

        public int Raised(int bin) => _raised[bin];

        /// <summary>The 4 [summary] lines: the raised chance, the guard of all AI men, with a shield in hand, without.</summary>
        public List<string> SummaryLines(in TiredBlockRules r)
        {
            var lines = new List<string>(4);
            var t = new StringBuilder(256);
            if (!r.Enabled && Recomputes == 0)
            {
                t.Append("tired block direction (step 30): off at the end (").Append(r.OffBecause).Append(") and never raised this battle - the game's own blocking");
            }
            else
            {
                t.Append("tired block direction (TiredBlockRandomPercent ").Append(r.Percent).Append(", step 30): the game's random-direction chance raised for ")
                    .Append(Men).Append(" AI men (").Append(Recomputes).Append(" recomputes - ").Append(RecomputesWithShield)
                    .Append(" with a shield in hand, ").Append(Recomputes - RecomputesWithShield).Append(" without; ").Append(NegativeOwn)
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
            if (!_all.Append(t)) t.Append(" (no melee blow reached an AI man)");
            lines.Add(t.ToString());

            t.Clear();
            t.Append("AI guard by f WITH a shield in hand (when the blow came - step 34):");
            if (!_withShield.Append(t)) t.Append(" (no melee blow reached an AI man holding a shield)");
            t.Append(" (a wrong direction with a shield = a WRONG-side block: the shield still stops the blow)");
            lines.Add(t.ToString());

            t.Clear();
            t.Append("AI guard by f WITHOUT a shield (when the blow came - step 34):");
            if (!_noShield.Append(t)) t.Append(" (no melee blow reached an AI man without a shield)");
            t.Append(" (a wrong direction without a shield = the blow lands: empty should land far more than peak)");
            lines.Add(t.ToString());
            return lines;
        }
    }
}

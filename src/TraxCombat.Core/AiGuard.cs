using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 96 (Toni 2026-10-25: "more active ... but not so lethal, more defencive - bump up the defencive stuff, reduce the swings") - THE AI
    /// GUARDS BETTER: two of the game's own per-agent AI values, written by the stat decorator after the base model's SetAiRelatedProperties
    /// (assigned with = on every recompute, so nothing compounds): (a) <c>AiRandomizedDefendDirectionChance</c> (the share of his block
    /// directions left to a guess; the base model writes 1 − num³ - a recruit ≈ 1, a 200-skill veteran on realistic ≈ 0.74) × AiGuardGuessPercent,
    /// BEFORE step 30's tired raise; (b) <c>AIBlockOnDecideAbility</c> (the base model: lerp(0.5, 0.99, √num)) and <c>AIParryOnDecideAbility</c>
    /// (lerp(0.5, 0.95, num)) × AiBlockDecidePercent, clamped to the game's own range (0 .. its top 0.99 / 0.95). All three are read by the
    /// NATIVE AI only (no managed reader in v1.4.8 - AI_NOTES "Step 96"). Read LIVE: the master switch first, then AthleticsEnabled (it rides on
    /// the Athletics re-target and the decorator's gate), then each percent away from 100. AI men on foot and riders; never the player.
    /// </summary>
    public readonly struct AiGuardRules
    {
        public AiGuardRules(bool modEnabled, bool athleticsEnabled, int guessPercent, int decidePercent)
        {
            ModEnabled = modEnabled;
            AthleticsEnabled = athleticsEnabled;
            GuessPercent = guessPercent;
            DecidePercent = decidePercent;
        }

        public bool ModEnabled { get; }
        public bool AthleticsEnabled { get; }

        /// <summary>AiGuardGuessPercent: the game's guess chance × this / 100 (100 = off).</summary>
        public int GuessPercent { get; }

        /// <summary>AiBlockDecidePercent: the block- and parry-decide abilities × this / 100 (100 = off).</summary>
        public int DecidePercent { get; }

        private bool Base => ModEnabled && AthleticsEnabled;

        public bool GuessOn => Base && GuessPercent != AiGuardMath.OffPercent;

        public bool DecideOn => Base && DecidePercent != AiGuardMath.OffPercent;

        public bool Enabled => GuessOn || DecideOn;

        /// <summary>The factor on the guess chance (1 while off).</summary>
        public float GuessFactor => GuessOn ? (GuessPercent < 0 ? 0f : GuessPercent / 100f) : 1f;

        /// <summary>The factor on the two decide abilities (1 while off).</summary>
        public float DecideFactor => DecideOn ? (DecidePercent < 0 ? 0f : DecidePercent / 100f) : 1f;

        /// <summary>The switch that holds it off (the master switch first), or null when either lever is on.</summary>
        public string? OffBecause => !ModEnabled ? "ModEnabled" : !AthleticsEnabled ? "AthleticsEnabled"
            : !Enabled ? "AiGuardGuessPercent 100 and AiBlockDecidePercent 100" : null;

        public static AiGuardRules From(TraxSettings s) => new AiGuardRules(s.ModEnabled, s.AthleticsEnabled, s.AiGuardGuessPercent, s.AiBlockDecidePercent);

        /// <summary>The settings sentence for the mission-start line, a change and the summary.</summary>
        public string Describe()
        {
            if (!Enabled)
                return "AI guard (step 96): off (" + OffBecause + ") - the game's own block-direction guesses and block decisions";
            var t = new StringBuilder(320);
            t.Append("AI guard (step 96): on - AI men on foot and riders (never you): ");
            if (GuessOn)
                t.Append("the game's block-direction guess chance x").Append(AiGuardMath.F2(GuessFactor)).Append(" (AiGuardGuessPercent ").Append(GuessPercent)
                    .Append(" - a recruit's 0.99 → ").Append(AiGuardMath.F2(AiGuardMath.Guess(0.99f, GuessFactor)))
                    .Append(", before step 30's tired raise, so a tired man still guesses more)");
            else
                t.Append("the guess chance the game's (AiGuardGuessPercent 100)");
            t.Append("; ");
            if (DecideOn)
                t.Append("the block- and parry-decide abilities x").Append(AiGuardMath.F2(DecideFactor)).Append(" (AiBlockDecidePercent ").Append(DecidePercent)
                    .Append(" - a recruit's 0.62 / 0.53 → ").Append(AiGuardMath.F2(AiGuardMath.Block(0.62f, DecideFactor))).Append(" / ")
                    .Append(AiGuardMath.F2(AiGuardMath.Parry(0.53f, DecideFactor))).Append(", at most the game's own ")
                    .Append(AiGuardMath.F2(AiGuardMath.BlockDecideMax)).Append(" / ").Append(AiGuardMath.F2(AiGuardMath.ParryDecideMax)).Append(')');
            else
                t.Append("the decide abilities the game's (AiBlockDecidePercent 100)");
            t.Append(" - the engine reads all three itself");
            return t.ToString();
        }
    }

    /// <summary>Step 96 pure math: the factors for a man, the values written. No allocation.</summary>
    public static class AiGuardMath
    {
        /// <summary>A percent of 100 = the lever off (the game's value untouched, not written).</summary>
        public const int OffPercent = 100;

        /// <summary>The base model's top for AIBlockOnDecideAbility: lerp(0.5, 0.99, clamp01(√num)).</summary>
        public const float BlockDecideMax = 0.99f;

        /// <summary>The base model's top for AIParryOnDecideAbility: lerp(0.5, 0.95, clamp01(num)).</summary>
        public const float ParryDecideMax = 0.95f;

        /// <summary>His two factors now: (1, 1) for the player and while off.</summary>
        public static void Factors(in AiGuardRules r, bool isPlayer, out float guess, out float decide)
        {
            if (isPlayer)
            {
                guess = decide = 1f;
                return;
            }
            guess = r.GuessFactor;
            decide = r.DecideFactor;
        }

        /// <summary>
        /// The guess chance to write: clamp01(game) × g. g = 1 → <paramref name="game"/> UNCHANGED; a game value at or below 0 (a final-round
        /// tournament fighter's −1.10 - never random) or NaN is left as it is (fewer guesses than never is still never).
        /// </summary>
        public static float Guess(float game, float g)
        {
            if (g == 1f || !(game > 0f) || float.IsNaN(g)) return game;
            float own = game >= 1f ? 1f : game;
            float f = g <= 0f ? 0f : g;
            float v = own * f;
            return v >= 1f ? 1f : v;
        }

        /// <summary>The block-decide ability to write: game × b in 0 .. 0.99 (never lowered by a raise when another mod already wrote more).</summary>
        public static float Block(float game, float b) => Clamped(game, b, BlockDecideMax);

        /// <summary>The parry-decide ability to write: game × b in 0 .. 0.95 (likewise).</summary>
        public static float Parry(float game, float b) => Clamped(game, b, ParryDecideMax);

        private static float Clamped(float game, float b, float max)
        {
            if (b == 1f || float.IsNaN(game) || float.IsNaN(b)) return game;
            float v = game * (b <= 0f ? 0f : b);
            float top = game > max ? game : max; // the game's own range; a higher value someone else wrote is not cut by a raise
            if (v > top) v = top;
            return v < 0f ? 0f : v;
        }

        internal static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Step 96 per mission: the values written by f band (the game's → ours for the guess chance, the block- and parry-decide abilities), men
    /// and recomputes, and the [summary] line beside step 30's "AI guard by f" lines. Main thread; no allocation per note.
    /// </summary>
    public sealed class AiGuardStats
    {
        private readonly int[] _n = new int[AthleticsMath.PeakBins];
        private readonly double[] _guessGame = new double[AthleticsMath.PeakBins];
        private readonly double[] _guessOurs = new double[AthleticsMath.PeakBins];
        private readonly double[] _blockGame = new double[AthleticsMath.PeakBins];
        private readonly double[] _blockOurs = new double[AthleticsMath.PeakBins];
        private readonly double[] _parryGame = new double[AthleticsMath.PeakBins];
        private readonly double[] _parryOurs = new double[AthleticsMath.PeakBins];

        /// <summary>AI men written at least once.</summary>
        public int Men { get; set; }

        /// <summary>Recomputes that wrote the guard.</summary>
        public int Recomputes { get; private set; }

        public int Count(int bin) => bin < 0 || bin >= AthleticsMath.PeakBins ? 0 : _n[bin];

        public double MeanGuessOurs(int bin) => Count(bin) == 0 ? double.NaN : _guessOurs[bin] / _n[bin];

        public double MeanBlockOurs(int bin) => Count(bin) == 0 ? double.NaN : _blockOurs[bin] / _n[bin];

        /// <summary>One recompute of a man in f bin <paramref name="bin"/>: the game's three values and ours (the guess chance before step 30).</summary>
        public void Add(int bin, float guessGame, float guessOurs, float blockGame, float blockOurs, float parryGame, float parryOurs)
        {
            if (bin < 0 || bin >= AthleticsMath.PeakBins) return;
            Recomputes++;
            _n[bin]++;
            _guessGame[bin] += guessGame;
            _guessOurs[bin] += guessOurs;
            _blockGame[bin] += blockGame;
            _blockOurs[bin] += blockOurs;
            _parryGame[bin] += parryGame;
            _parryOurs[bin] += parryOurs;
        }

        /// <summary>The [summary] line.</summary>
        public string SummaryLine(in AiGuardRules r)
        {
            var t = new StringBuilder(384);
            if (!r.Enabled && Recomputes == 0)
                return t.Append("AI guard factors (step 96): off at the end (").Append(r.OffBecause).Append(") and never written this battle - the game's own guard").ToString();
            t.Append("AI guard factors (step 96 - AiGuardGuessPercent ").Append(r.GuessPercent).Append(", AiBlockDecidePercent ").Append(r.DecidePercent)
                .Append("; AI men on foot and riders, never you): written for ").Append(Men).Append(" AI men (").Append(Recomputes).Append(" recomputes)");
            if (Recomputes > 0)
            {
                t.Append("; the game's → ours, avg by f band - the block-direction guess chance (before step 30's tired raise) / the block-decide ability / the parry-decide ability:");
                bool first = true;
                for (int b = 0; b < AthleticsMath.PeakBins; b++)
                {
                    int n = _n[b];
                    if (n == 0) continue;
                    double inv = 1.0 / n;
                    t.Append(first ? " " : "; ").Append(AthleticsMath.PeakBinName(b)).Append(' ')
                        .Append(AiGuardMath.F2(_guessGame[b] * inv)).Append(" → ").Append(AiGuardMath.F2(_guessOurs[b] * inv)).Append(" / ")
                        .Append(AiGuardMath.F2(_blockGame[b] * inv)).Append(" → ").Append(AiGuardMath.F2(_blockOurs[b] * inv)).Append(" / ")
                        .Append(AiGuardMath.F2(_parryGame[b] * inv)).Append(" → ").Append(AiGuardMath.F2(_parryOurs[b] * inv))
                        .Append(" (n ").Append(n).Append(')');
                    first = false;
                }
            }
            if (!r.Enabled) t.Append(" - off at the end (").Append(r.OffBecause).Append(')');
            return t.ToString();
        }
    }
}

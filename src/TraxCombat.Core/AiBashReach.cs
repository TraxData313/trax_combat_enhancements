using System;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>Step 91: what one AI shield bash came to (the [summary] "the shield bash's reach" line's columns).</summary>
    public enum AiBashResult : byte
    {
        /// <summary>Landed on an enemy's body (his target or another).</summary>
        Enemy,

        /// <summary>Stopped by an enemy's shield.</summary>
        EnemyShield,

        /// <summary>Hit one of his own side (the man or his shield) - the game cancels it.</summary>
        Friend,

        /// <summary>Played and hit nothing.</summary>
        Missed,

        /// <summary>Hit a horse or a man the mod does not track.</summary>
        Untracked,

        /// <summary>Never seen played - the engine ignored it, he was struck first, or it ended before its key (the path / the bash reach after
        /// its guard).</summary>
        NotPlayed,

        Count,
    }

    /// <summary>
    /// Step 91 (Toni's 19:56 battle of 2026-10-24: 114 of 150 AI bash hits on a FRIEND, 100 on the basher's shield side - the bash was rolled at the
    /// kick's 1.6 m and its short sweep passed the enemy into the neighbour): THE SHIELD BASH'S OWN REACH, measured - a shieldman's rolls with the
    /// enemy within <c>AiBashReachMetres</c> and beyond it (kick / bash / swing), each bash by the enemy's distance at its decision against what it
    /// came to, and the bashes ended after their guard because the enemy had stepped beyond the reach. Fixed arrays, allocation-free; main thread.
    /// </summary>
    public sealed class AiBashReachStats
    {
        /// <summary>The distance bins' upper edges (m): ≤0.8, 0.8-1.0, 1.0-1.1, 1.1-1.2, 1.2-1.4, 1.4-1.6, the last open (&gt;1.6). 1.1 = the shipped
        /// reach, so a bin never straddles it.</summary>
        public static readonly double[] BinEdges = { 0.8, 1.0, 1.1, 1.2, 1.4, 1.6 };

        public static int BinCount => BinEdges.Length + 1;

        private readonly int[,] _rolled = new int[2, AiKickMath.KindCount];   // [0 within the bash reach / 1 beyond, kind]
        private readonly int[] _bashRolled = new int[BinEdges.Length + 1];
        private readonly int[] _asked = new int[BinEdges.Length + 1];
        private readonly int[,] _results = new int[BinEdges.Length + 1, (int)AiBashResult.Count];

        /// <summary>A bash's path re-read after its guard: the enemy had stepped beyond the bash reach - ended with no key.</summary>
        public int PressBeyond;

        /// <summary>Bashes asked with the enemy beyond the bash reach at the decision (with the reach on this must stay 0).</summary>
        public int AskedBeyond;

        public static int BinOf(double distance)
        {
            if (double.IsNaN(distance)) return BinCount - 1;
            for (int i = 0; i < BinEdges.Length; i++)
                if (distance <= BinEdges[i] + 1e-9) return i;
            return BinCount - 1;
        }

        public static string BinName(int b) => b == 0 ? "≤0.8 m"
            : b >= BinEdges.Length ? ">" + BinEdges[BinEdges.Length - 1].ToString("0.0#", CultureInfo.InvariantCulture) + " m"
            : BinEdges[b - 1].ToString("0.0#", CultureInfo.InvariantCulture) + "-" + BinEdges[b].ToString("0.0#", CultureInfo.InvariantCulture) + " m";

        /// <summary>One shieldman's move in kick reach: what was rolled, within or beyond the bash reach; a rolled bash binned by the distance.</summary>
        public void NoteShieldRoll(bool beyond, AiKickKind rolled, double distance)
        {
            _rolled[beyond ? 1 : 0, (int)rolled]++;
            if (rolled == AiKickKind.Bash) _bashRolled[BinOf(distance)]++;
        }

        /// <summary>A bash ASKED (its attempt begun) with the enemy at <paramref name="distance"/>.</summary>
        public void NoteAsked(double distance, bool beyond)
        {
            _asked[BinOf(distance)]++;
            if (beyond) AskedBeyond++;
        }

        /// <summary>What an asked bash came to, filed by its decision's distance (once per attempt).</summary>
        public void NoteResult(double distance, AiBashResult result) => _results[BinOf(distance), (int)result]++;

        public int Rolled(bool beyond, AiKickKind k) => _rolled[beyond ? 1 : 0, (int)k];

        public int Moves(bool beyond)
        {
            int n = 0;
            for (int k = 0; k < AiKickMath.KindCount; k++) n += _rolled[beyond ? 1 : 0, k];
            return n;
        }

        public int BashRolled(int bin) => _bashRolled[bin];

        public int Asked(int bin) => _asked[bin];

        public int Result(int bin, AiBashResult r) => _results[bin, (int)r];

        public int ResultTotal(AiBashResult r)
        {
            int n = 0;
            for (int b = 0; b < BinCount; b++) n += _results[b, (int)r];
            return n;
        }

        public int AskedTotal()
        {
            int n = 0;
            for (int b = 0; b < BinCount; b++) n += _asked[b];
            return n;
        }

        /// <summary>The friends' share of a bin's bash HITS (enemy + his shield + friend + untracked), NaN with none.</summary>
        public double FriendShare(int bin)
        {
            int hits = _results[bin, (int)AiBashResult.Enemy] + _results[bin, (int)AiBashResult.EnemyShield] + _results[bin, (int)AiBashResult.Friend]
                       + _results[bin, (int)AiBashResult.Untracked];
            return hits > 0 ? (double)_results[bin, (int)AiBashResult.Friend] / hits : double.NaN;
        }

        /// <summary>The 8th [summary] "ai kicks" line.</summary>
        public string Line(in AiKickRules r)
        {
            var c = CultureInfo.InvariantCulture;
            string P(int n, int of) => of > 0 ? (100.0 * n / of).ToString("0.0", c) + "%" : "n/a";
            var sb = new System.Text.StringBuilder("ai kicks - the shield bash's reach (step 91): ");
            if (r.BashReachOn)
                sb.Append("a shieldman bashes only within ").Append(AiKickMath.F2(r.BashReach)).Append(" m (AiBashReachMetres; kicks and pushes keep the ")
                  .Append(AiKickMath.F1(r.Reach)).Append(" m kick reach) - farther off he kicks ").Append(AiKickMath.P0(r.ShieldKickBeyondBashReach)).Append(" / swings ")
                  .Append(AiKickMath.P0(1 - r.ShieldKickBeyondBashReach));
            else
                sb.Append("the bash reach is OFF - a shieldman bashes at the kick reach, anywhere within ").Append(AiKickMath.F1(r.Reach))
                  .Append(" m (AiBashReachMetres 0; the bins below still say where his bashes land)");
            for (int i = 0; i < 2; i++)
            {
                bool beyond = i == 1;
                int n = Moves(beyond);
                if (beyond && !r.BashReachOn && n == 0) continue; // step 91b: off - nothing is beyond a reach that is not there
                sb.Append(beyond ? " | beyond it " : r.BashReachOn ? " | shieldmen's moves within it " : " | shieldmen's moves in the kick reach ").Append(n).Append(" - rolled kick ").Append(P(Rolled(beyond, AiKickKind.Kick), n))
                  .Append(" / bash ").Append(P(Rolled(beyond, AiKickKind.Bash), n)).Append(" / swing ").Append(P(Rolled(beyond, AiKickKind.None), n));
                if (beyond)
                    sb.Append(" (target ").Append(r.BashReachOn ? AiKickMath.P0(r.ShieldKickBeyondBashReach) + " / 0% / " + AiKickMath.P0(1 - r.ShieldKickBeyondBashReach) : "-").Append(')');
                else
                    sb.Append(" (target ").Append(AiKickMath.P0(r.ShieldKick)).Append(" / ").Append(AiKickMath.P0(r.ShieldBash)).Append(" / ").Append(AiKickMath.P0(r.SwingShare(true))).Append(')');
            }
            sb.Append(" | bashes by the enemy's distance at the decision - rolled, asked → on an ENEMY / into his shield / on a FRIEND / missed / horse or untracked / not played: ");
            bool first = true;
            for (int b = 0; b < BinCount; b++)
            {
                if (_bashRolled[b] == 0 && _asked[b] == 0) continue;
                if (!first) sb.Append("; ");
                first = false;
                sb.Append(BinName(b)).Append(' ').Append(_bashRolled[b]).Append(", ").Append(_asked[b]).Append(" → ")
                  .Append(_results[b, (int)AiBashResult.Enemy]).Append(" / ").Append(_results[b, (int)AiBashResult.EnemyShield]).Append(" / ")
                  .Append(_results[b, (int)AiBashResult.Friend]).Append(" / ").Append(_results[b, (int)AiBashResult.Missed]).Append(" / ")
                  .Append(_results[b, (int)AiBashResult.Untracked]).Append(" / ").Append(_results[b, (int)AiBashResult.NotPlayed]);
                double fs = FriendShare(b);
                if (!double.IsNaN(fs)) sb.Append(" (friends ").Append((100 * fs).ToString("0", c)).Append("% of the hits)");
            }
            if (first) sb.Append("none");
            if (!r.BashReachOn && AskedBeyond == 0 && PressBeyond == 0) return sb.ToString(); // step 91b: off - no reach to be beyond
            sb.Append(" | asked beyond the bash reach ").Append(AskedBeyond).Append("; a bash's re-read after its guard found the enemy beyond the bash reach ")
              .Append(PressBeyond).Append(" (ended, no bash)");
            return sb.ToString();
        }
    }
}

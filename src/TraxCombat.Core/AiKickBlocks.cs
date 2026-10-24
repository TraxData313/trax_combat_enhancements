using System;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>Step 87: where the friend in the strike path stands in the formation, against the attacker.</summary>
    public enum AiKickRelation : byte
    {
        /// <summary>The same formation and the same rank - a SHOULDER NEIGHBOUR (the step-87 suspicion).</summary>
        ShoulderNeighbour,

        /// <summary>The same formation, the rank right in front of his (one lower - rank 0 is the front).</summary>
        RankInFront,

        /// <summary>The same formation, any other rank (behind him, two in front).</summary>
        OtherRank,

        /// <summary>Another formation, or one of the two in none.</summary>
        OtherFormation,

        /// <summary>The same formation, but its arrangement has no line ranks (a column) or a rank was not readable (detached).</summary>
        Unknown,

        Count,
    }

    /// <summary>Step 87: which measurement a block / hit is filed under.</summary>
    public enum AiKickBlockCase : byte
    {
        /// <summary>Refused: a friend first in the path within the rear-rank distance (step 80's BehindFriend).</summary>
        BehindFriend,

        /// <summary>Refused: a friend first in the path beyond it (FriendFirst).</summary>
        FriendFirst,

        /// <summary>Refused: the enemy not in the path (no friend - the enemy's own place binned).</summary>
        TargetOffPath,

        /// <summary>A played kick / bash whose first collision was a FRIEND (body or shield) - the friend's place at the hit.</summary>
        FriendHit,

        Count,
    }

    /// <summary>
    /// Step 87: one friend in (or hit by) a strike, in the attacker's frame - along his FACING and off its line (m), where he stands in the
    /// formation against the attacker, and the facing-to-target angle (degrees) of the decision. A plain struct, filled by the module's engine
    /// reads (or the smoke's stand-in) - never allocated.
    /// </summary>
    public struct AiKickBlocker
    {
        /// <summary>A friend was found / located (false = nothing to bin but the angle).</summary>
        public bool Valid;

        /// <summary>Along his facing (m; ≤ 0 = beside / behind his shoulders) and off its line (m, ≥ 0).</summary>
        public double Along;
        public double Side;

        /// <summary>The friend's agent index (−1 = unknown) - the module maps it back for the first-time line.</summary>
        public int FriendIndex;

        public AiKickRelation Relation;

        /// <summary>The two men's file / rank in their formation (−1 = not read) - for the first-time line only.</summary>
        public int File, Rank, FriendFile, FriendRank;

        /// <summary>An empty one (nothing found).</summary>
        public static AiKickBlocker None => new AiKickBlocker { FriendIndex = -1, Relation = AiKickRelation.Unknown, File = -1, Rank = -1, FriendFile = -1, FriendRank = -1 };
    }

    /// <summary>Step 87 pure: the geometry, the relation rule, the bins and their names.</summary>
    public static class AiKickBlockMath
    {
        public const int AlongBins = 6;
        public const int SideBins = 4;
        public const int AngleBins = 5;

        private static readonly string[] AlongNames = { "≤0 (beside / behind his shoulders)", "0-0.2", "0.2-0.4", "0.4-0.8", "0.8-1.2", ">1.2" };
        private static readonly string[] SideNames = { "0-0.3", "0.3-0.6", "0.6-0.75", ">0.75" };
        private static readonly string[] AngleNames = { "0-10°", "10-20°", "20-30°", "30-40°", ">40°" };

        /// <summary>Along his facing: ≤ 0, 0-0.2, 0.2-0.4, 0.4-0.8, 0.8-1.2, &gt; 1.2 m (a bin's top edge belongs to it).</summary>
        public static int AlongBin(double along)
        {
            if (double.IsNaN(along) || along <= 0) return 0;
            if (along <= 0.2 + 1e-9) return 1;
            if (along <= 0.4 + 1e-9) return 2;
            if (along <= 0.8 + 1e-9) return 3;
            if (along <= 1.2 + 1e-9) return 4;
            return 5;
        }

        /// <summary>Off the line of his facing: 0-0.3, 0.3-0.6, 0.6-0.75, &gt; 0.75 m.</summary>
        public static int SideBin(double side)
        {
            side = Math.Abs(side);
            if (double.IsNaN(side) || side <= 0.3 + 1e-9) return 0;
            if (side <= 0.6 + 1e-9) return 1;
            if (side <= 0.75 + 1e-9) return 2;
            return 3;
        }

        /// <summary>The angle between his facing and the line to his target: 0-10, 10-20, 20-30, 30-40, &gt; 40°.</summary>
        public static int AngleBin(double degrees)
        {
            degrees = Math.Abs(degrees);
            if (double.IsNaN(degrees) || degrees <= 10 + 1e-9) return 0;
            if (degrees <= 20 + 1e-9) return 1;
            if (degrees <= 30 + 1e-9) return 2;
            if (degrees <= 40 + 1e-9) return 3;
            return 4;
        }

        public static string AlongName(int bin) => AlongNames[bin];
        public static string SideName(int bin) => SideNames[bin];
        public static string AngleName(int bin) => AngleNames[bin];

        /// <summary>A point in his frame: along his facing (any length; zero = +y) and off its line (≥ 0).</summary>
        public static void Locate(double ax, double ay, double faceX, double faceY, double px, double py, out double along, out double side)
        {
            double fl = Math.Sqrt(faceX * faceX + faceY * faceY);
            if (!(fl > 1e-6))
            {
                faceX = 0;
                faceY = 1;
                fl = 1;
            }
            double ux = faceX / fl, uy = faceY / fl;
            along = (px - ax) * ux + (py - ay) * uy;
            side = Math.Abs(-(px - ax) * uy + (py - ay) * ux);
        }

        /// <summary>
        /// The friend against the attacker: another formation (or none) first; then, in the same formation, no line ranks (a column, a detached man
        /// - a rank below 0) = unknown; the same rank = a SHOULDER NEIGHBOUR; his rank − 1 (rank 0 is the front) = the rank in front; else another
        /// rank. In a square / circle (the game's LineFormation underneath) a rank is a ring, the outer one 0.
        /// </summary>
        public static AiKickRelation RelationOf(bool sameFormation, bool lineRanks, int rank, int friendRank)
        {
            if (!sameFormation) return AiKickRelation.OtherFormation;
            if (!lineRanks || rank < 0 || friendRank < 0) return AiKickRelation.Unknown;
            if (friendRank == rank) return AiKickRelation.ShoulderNeighbour;
            if (friendRank == rank - 1) return AiKickRelation.RankInFront;
            return AiKickRelation.OtherRank;
        }

        public static string RelationName(AiKickRelation r) => r switch
        {
            AiKickRelation.ShoulderNeighbour => "his SHOULDER NEIGHBOUR (same rank)",
            AiKickRelation.RankInFront => "the rank in front",
            AiKickRelation.OtherRank => "another rank",
            AiKickRelation.OtherFormation => "another formation / none",
            _ => "unknown (no line ranks)",
        };

        public static string CaseName(AiKickBlockCase c) => c switch
        {
            AiKickBlockCase.BehindFriend => "REFUSED behind a friend (rear rank)",
            AiKickBlockCase.FriendFirst => "REFUSED a friend first",
            AiKickBlockCase.TargetOffPath => "REFUSED the enemy off his path (the ENEMY's place)",
            _ => "LANDED ON A FRIEND (his place at the hit, the angle at the decision)",
        };

        /// <summary>The refusal's case for a path verdict (Clear has none - −1).</summary>
        public static int CaseOf(AiKickPath p) => p switch
        {
            AiKickPath.BehindFriend => (int)AiKickBlockCase.BehindFriend,
            AiKickPath.FriendFirst => (int)AiKickBlockCase.FriendFirst,
            AiKickPath.TargetOffPath => (int)AiKickBlockCase.TargetOffPath,
            _ => -1,
        };
    }

    /// <summary>
    /// Step 87 (Toni's 5 battles of 2026-10-24 15:05-15:44: 511 of 752 refused AI kicks / bashes were "behind a friend - rear rank", bashes twice as
    /// often as kicks; 56% of the bashes that passed the path still landed on a friend): MEASUREMENT ONLY - where the blocking friend stood at
    /// each strike-path refusal and where the friend stood at each kick / bash that hit one: along / to the side of the attacker's facing, his
    /// place in the formation (a shoulder neighbour in the same rank, the rank in front, ...) and the facing-to-target angle; kicks and bashes
    /// apart; the 7th [summary] "ai kicks" line. Fixed arrays, no allocation per note. Main thread.
    /// </summary>
    public sealed class AiKickBlockStats
    {
        private const int Cases = (int)AiKickBlockCase.Count;
        private const int Rel = (int)AiKickRelation.Count;

        private readonly int[,] _n = new int[Cases, AiKickMath.KindCount];
        private readonly int[,,] _along = new int[Cases, AiKickMath.KindCount, AiKickBlockMath.AlongBins];
        private readonly int[,,] _side = new int[Cases, AiKickMath.KindCount, AiKickBlockMath.SideBins];
        private readonly int[,,] _angle = new int[Cases, AiKickMath.KindCount, AiKickBlockMath.AngleBins];
        private readonly int[,,] _rel = new int[Cases, AiKickMath.KindCount, Rel];
        private readonly double[,,] _relAlong = new double[Cases, AiKickMath.KindCount, Rel];
        private readonly double[,,] _relSide = new double[Cases, AiKickMath.KindCount, Rel];

        /// <summary>Friend hits whose friend could not be located (no engine read) - binned by the angle only.</summary>
        public int HitsNotLocated;

        public int Count(AiKickBlockCase c, AiKickKind k) => _n[(int)c, (int)k];
        public int AlongCount(AiKickBlockCase c, AiKickKind k, int bin) => _along[(int)c, (int)k, bin];
        public int SideCount(AiKickBlockCase c, AiKickKind k, int bin) => _side[(int)c, (int)k, bin];
        public int AngleCount(AiKickBlockCase c, AiKickKind k, int bin) => _angle[(int)c, (int)k, bin];
        public int RelationCount(AiKickBlockCase c, AiKickKind k, AiKickRelation r) => _rel[(int)c, (int)k, (int)r];

        /// <summary>
        /// A strike-path refusal of a <paramref name="kind"/> at the enemy <paramref name="distance"/> m / <paramref name="angleDegrees"/>° off his
        /// facing: a friend first / behind a friend = the blocking friend's place (<paramref name="b"/>); the enemy off the path = the ENEMY's place
        /// (d cos, d sin). Clear = nothing.
        /// </summary>
        public void NoteRefusal(AiKickPath path, AiKickKind kind, in AiKickBlocker b, double distance, double angleDegrees)
        {
            int c = AiKickBlockMath.CaseOf(path);
            if (c < 0 || kind == AiKickKind.None) return;
            int k = (int)kind;
            _n[c, k]++;
            _angle[c, k, AiKickBlockMath.AngleBin(angleDegrees)]++;
            if (c == (int)AiKickBlockCase.TargetOffPath)
            {
                double a = angleDegrees * Math.PI / 180.0;
                _along[c, k, AiKickBlockMath.AlongBin(distance * Math.Cos(a))]++;
                _side[c, k, AiKickBlockMath.SideBin(distance * Math.Sin(a))]++;
                return;
            }
            Place(c, k, in b);
        }

        /// <summary>A played kick / bash whose first collision was a friend: his place at the hit (<paramref name="b"/>), the angle of the decision.</summary>
        public void NoteFriendHit(AiKickKind kind, in AiKickBlocker b, double decisionAngleDegrees)
        {
            if (kind == AiKickKind.None) return;
            int c = (int)AiKickBlockCase.FriendHit, k = (int)kind;
            _n[c, k]++;
            _angle[c, k, AiKickBlockMath.AngleBin(decisionAngleDegrees)]++;
            if (!b.Valid)
            {
                HitsNotLocated++;
                return;
            }
            Place(c, k, in b);
        }

        private void Place(int c, int k, in AiKickBlocker b)
        {
            int r = (int)(b.Valid ? b.Relation : AiKickRelation.Unknown);
            _rel[c, k, r]++;
            if (!b.Valid) return;
            _along[c, k, AiKickBlockMath.AlongBin(b.Along)]++;
            _side[c, k, AiKickBlockMath.SideBin(b.Side)]++;
            _relAlong[c, k, r] += b.Along;
            _relSide[c, k, r] += b.Side;
        }

        /// <summary>The shoulder neighbours' share of a case (all kinds) - the headline.</summary>
        public double ShoulderShare(AiKickBlockCase c)
        {
            int n = 0, s = 0;
            for (int k = 1; k < AiKickMath.KindCount; k++)
            {
                for (int r = 0; r < Rel; r++) n += _rel[(int)c, k, r];
                s += _rel[(int)c, k, (int)AiKickRelation.ShoulderNeighbour];
            }
            return n > 0 ? (double)s / n : double.NaN;
        }

        /// <summary>The [summary] "ai kicks - who blocks the path (step 87 diagnostic)" line.</summary>
        public string Line()
        {
            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder("ai kicks - who blocks the path (step 87 diagnostic): ");
            double behind = ShoulderShare(AiKickBlockCase.BehindFriend), hit = ShoulderShare(AiKickBlockCase.FriendHit);
            sb.Append("shoulder neighbours are ").Append(double.IsNaN(behind) ? "n/a" : (100 * behind).ToString("0", c) + "%")
              .Append(" of the 'behind a friend' refusals and ").Append(double.IsNaN(hit) ? "n/a" : (100 * hit).ToString("0", c) + "%")
              .Append(" of the friends hit (along / to the side of the kicker's FACING, m; the angle = his facing vs the line to his target)");
            for (int ci = 0; ci < Cases; ci++)
            {
                sb.Append(" | ").Append(AiKickBlockMath.CaseName((AiKickBlockCase)ci)).Append(" - ");
                for (int k = 1; k < AiKickMath.KindCount; k++)
                {
                    if (k > 1) sb.Append("; ");
                    Block(sb, ci, k, c);
                }
            }
            if (HitsNotLocated > 0) sb.Append(" | friends hit not located (angle only) ").Append(HitsNotLocated);
            return sb.ToString();
        }

        private void Block(StringBuilder sb, int ci, int k, CultureInfo c)
        {
            int n = _n[ci, k];
            sb.Append(AiKickMath.PluralName((AiKickKind)k)).Append(' ').Append(n);
            if (n == 0) return;
            bool enemy = ci == (int)AiKickBlockCase.TargetOffPath;
            sb.Append(": ahead ");
            for (int b = 0; b < AiKickBlockMath.AlongBins; b++) sb.Append(b > 0 ? ", " : string.Empty).Append(AiKickBlockMath.AlongName(b)).Append(' ').Append(_along[ci, k, b]);
            sb.Append("; to the side ");
            for (int b = 0; b < AiKickBlockMath.SideBins; b++) sb.Append(b > 0 ? ", " : string.Empty).Append(AiKickBlockMath.SideName(b)).Append(' ').Append(_side[ci, k, b]);
            if (!enemy)
            {
                sb.Append("; ");
                for (int r = 0; r < Rel; r++)
                {
                    int m = _rel[ci, k, r];
                    sb.Append(r > 0 ? ", " : string.Empty).Append(AiKickBlockMath.RelationName((AiKickRelation)r)).Append(' ').Append(m);
                    if (m > 0 && r != (int)AiKickRelation.Unknown)
                        sb.Append(" (along ").Append((_relAlong[ci, k, r] / m).ToString("0.00", c)).Append(" / side ").Append((_relSide[ci, k, r] / m).ToString("0.00", c)).Append(')');
                }
            }
            sb.Append("; angle ");
            for (int b = 0; b < AiKickBlockMath.AngleBins; b++) sb.Append(b > 0 ? ", " : string.Empty).Append(AiKickBlockMath.AngleName(b)).Append(' ').Append(_angle[ci, k, b]);
        }
    }
}

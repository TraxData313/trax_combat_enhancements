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

        /// <summary>Step 90: off the line SIGNED (m) - + = on his LEFT (his shield side), − = his right (his weapon side); NaN = not known.
        /// u × (q − p) with u his facing - the game's Vec2.LeftVec() = (−y, x).</summary>
        public double Lateral;

        /// <summary>Step 90 (at the decision): friends within <see cref="AiKickMath.NeighbourMetres"/> of him on his left / right (−1 = not
        /// counted - the path check off or no probe).</summary>
        public int LeftNear, RightNear;

        /// <summary>The friend's agent index (−1 = unknown) - the module maps it back for the first-time line.</summary>
        public int FriendIndex;

        public AiKickRelation Relation;

        /// <summary>The two men's file / rank in their formation (−1 = not read) - for the first-time line only.</summary>
        public int File, Rank, FriendFile, FriendRank;

        /// <summary>An empty one (nothing found).</summary>
        public static AiKickBlocker None => new AiKickBlocker
        {
            FriendIndex = -1, Relation = AiKickRelation.Unknown, File = -1, Rank = -1, FriendFile = -1, FriendRank = -1, Lateral = double.NaN, LeftNear = -1, RightNear = -1,
        };
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

        /// <summary>Step 90: the two sides of the attacker - 0 = his LEFT (his shield side, lateral &gt; 0), 1 = his right (his weapon side, lateral ≤ 0
        /// - one exactly on the line counted there); −1 = not known (NaN).</summary>
        public const int Sides = 2;

        public static int SideOf(double lateral) => double.IsNaN(lateral) ? -1 : lateral > 0 ? 0 : 1;

        public static string SideOfName(int side) => side == 0 ? "his shield side (left)" : "his weapon side (right)";

        /// <summary>Step 90: the friends tight beside him at the decision - 0 none, 1 his shield side (left) only, 2 his weapon side (right) only, 3 both;
        /// −1 not counted.</summary>
        public const int NearCases = 4;

        public static int NearCase(int left, int right) => left < 0 || right < 0 ? -1 : (left > 0 ? 1 : 0) + (right > 0 ? 2 : 0);

        private static readonly string[] NearNames = { "none", "on his shield side only", "on his weapon side only", "on both sides" };

        public static string NearName(int c) => NearNames[c];

        /// <summary>A point in his frame, the side SIGNED (step 90: + = his left).</summary>
        public static void Locate(double ax, double ay, double faceX, double faceY, double px, double py, out double along, out double side, out double lateral)
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
            lateral = ux * (py - ay) - uy * (px - ax);
            side = Math.Abs(lateral);
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
            AiKickBlockCase.BehindFriend => "REFUSED a friend in front of him (step 90; step 80's 'behind a friend')",
            AiKickBlockCase.FriendFirst => "REFUSED a friend in front beyond the rear-rank reach",
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

        // step 90: the SIDE - the blocking / hit friend on the attacker's shield side (left) or weapon side (right), by case x kind; and for every
        // kick / bash / push ASKED and every one that hit a friend, the friends tight beside him at the decision (none / left only / right only / both)
        private readonly int[,,] _bySide = new int[Cases, AiKickMath.KindCount, AiKickBlockMath.Sides];
        private readonly int[,] _askedNear = new int[AiKickMath.KindCount, AiKickBlockMath.NearCases];
        private readonly int[,,] _hitNear = new int[AiKickMath.KindCount, AiKickBlockMath.NearCases, AiKickBlockMath.Sides];
        private readonly long[] _hitLeftSum = new long[AiKickMath.KindCount];
        private readonly long[] _hitRightSum = new long[AiKickMath.KindCount];
        private readonly int[] _hitNearCounted = new int[AiKickMath.KindCount];

        /// <summary>Step 90: the blocking / hit friends of a case on a side (0 his shield side - left, 1 his weapon side - right).</summary>
        public int BySide(AiKickBlockCase c, AiKickKind k, int side) => _bySide[(int)c, (int)k, side];

        /// <summary>Step 90: the moves asked with this near case (0 none, 1 left only, 2 right only, 3 both).</summary>
        public int AskedNear(AiKickKind k, int nearCase) => _askedNear[(int)k, nearCase];

        /// <summary>Step 90: the friend hits with this near case at the decision, by the hit friend's side.</summary>
        public int HitNear(AiKickKind k, int nearCase, int side) => _hitNear[(int)k, nearCase, side];

        /// <summary>Step 90: the friend hits of a kind with the neighbours counted, and their mean neighbours on his left / right.</summary>
        public int HitNearCounted(AiKickKind k) => _hitNearCounted[(int)k];
        public double HitMeanLeft(AiKickKind k) => _hitNearCounted[(int)k] > 0 ? (double)_hitLeftSum[(int)k] / _hitNearCounted[(int)k] : double.NaN;
        public double HitMeanRight(AiKickKind k) => _hitNearCounted[(int)k] > 0 ? (double)_hitRightSum[(int)k] / _hitNearCounted[(int)k] : double.NaN;

        /// <summary>Step 90: a kick / bash / push ASKED (it passed every gate) with <paramref name="leftNear"/> / <paramref name="rightNear"/> friends tight
        /// on his left / right at the decision (−1 = not counted - nothing noted).</summary>
        public void NoteAsked(AiKickKind kind, int leftNear, int rightNear)
        {
            int nc = AiKickBlockMath.NearCase(leftNear, rightNear);
            if (kind == AiKickKind.None || nc < 0) return;
            _askedNear[(int)kind, nc]++;
        }

        /// <summary>Step 90: the share of a kind's located friend hits that were on his shield side (left); NaN = none.</summary>
        public double ShieldSideShare(AiKickKind k)
        {
            int l = _bySide[(int)AiKickBlockCase.FriendHit, (int)k, 0], r = _bySide[(int)AiKickBlockCase.FriendHit, (int)k, 1];
            return l + r > 0 ? (double)l / (l + r) : double.NaN;
        }

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
        public void NoteFriendHit(AiKickKind kind, in AiKickBlocker b, double decisionAngleDegrees) => NoteFriendHit(kind, in b, decisionAngleDegrees, -1, -1);

        /// <summary>Step 90: as above, with the friends tight on his left / right at the DECISION (−1 = not counted).</summary>
        public void NoteFriendHit(AiKickKind kind, in AiKickBlocker b, double decisionAngleDegrees, int leftNear, int rightNear)
        {
            if (kind == AiKickKind.None) return;
            int c = (int)AiKickBlockCase.FriendHit, k = (int)kind;
            _n[c, k]++;
            int nc = AiKickBlockMath.NearCase(leftNear, rightNear);
            if (nc >= 0)
            {
                _hitNearCounted[k]++;
                _hitLeftSum[k] += leftNear;
                _hitRightSum[k] += rightNear;
                int hs = b.Valid ? AiKickBlockMath.SideOf(b.Lateral) : -1;
                if (hs >= 0) _hitNear[k, nc, hs]++;
            }
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
            int s = AiKickBlockMath.SideOf(b.Lateral); // step 90
            if (s >= 0) _bySide[c, k, s]++;
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
            // step 90: the answer first - which side of the attacker the friends his bashes hit stood on
            sb.Append("friend hits by bashes: ").Append(Pct(ShieldSideShare(AiKickKind.Bash), c)).Append(" on his shield side (left; pushes ")
              .Append(Pct(ShieldSideShare(AiKickKind.Push), c)).Append(", kicks ").Append(Pct(ShieldSideShare(AiKickKind.Kick), c)).Append(") | ");
            double behind = ShoulderShare(AiKickBlockCase.BehindFriend), hit = ShoulderShare(AiKickBlockCase.FriendHit);
            sb.Append("shoulder neighbours are ").Append(double.IsNaN(behind) ? "n/a" : (100 * behind).ToString("0", c) + "%")
              .Append(" of the 'a friend in front of him' refusals and ").Append(double.IsNaN(hit) ? "n/a" : (100 * hit).ToString("0", c) + "%")
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
            // step 90: the friends tight beside the basher / pusher at the decision - does a man with a friend on his shield side hit him?
            sb.Append(" | step 90 - friends within ").Append(AiKickMath.NeighbourMetres.ToString("0.0", c)).Append(" m of him at the decision (the sign: + = his left, Vec2.LeftVec)");
            for (int k = (int)AiKickKind.Bash; k < AiKickMath.KindCount; k++)
            {
                sb.Append(k > (int)AiKickKind.Bash ? "; " : ": ").Append(AiKickMath.PluralName((AiKickKind)k)).Append(" asked / hit a friend (on his shield side - weapon side)");
                for (int nc = 0; nc < AiKickBlockMath.NearCases; nc++)
                {
                    int asked = _askedNear[k, nc], l = _hitNear[k, nc, 0], r = _hitNear[k, nc, 1];
                    sb.Append(nc == 0 ? " " : ", ").Append(AiKickBlockMath.NearName(nc)).Append(' ').Append(asked).Append(" / ").Append(l + r)
                      .Append(" (").Append(l).Append(" - ").Append(r).Append(')');
                    if (asked > 0) sb.Append(' ').Append((100.0 * (l + r) / asked).ToString("0", c)).Append('%');
                }
                if (_hitNearCounted[k] > 0)
                    sb.Append("; the men who hit a friend had on average ").Append(HitMeanLeft((AiKickKind)k).ToString("0.00", c)).Append(" left / ")
                      .Append(HitMeanRight((AiKickKind)k).ToString("0.00", c)).Append(" right");
            }
            return sb.ToString();
        }

        private static string Pct(double share, CultureInfo c) => double.IsNaN(share) ? "n/a" : (100 * share).ToString("0", c) + "%";

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
                // step 90: the side - his shield side (left) vs his weapon side (right)
                sb.Append("; ").Append(AiKickBlockMath.SideOfName(0)).Append(' ').Append(_bySide[ci, k, 0]).Append(", ").Append(AiKickBlockMath.SideOfName(1))
                  .Append(' ').Append(_bySide[ci, k, 1]);
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

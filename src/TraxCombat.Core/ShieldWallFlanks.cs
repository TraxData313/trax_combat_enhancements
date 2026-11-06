using System;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>Step 99: which end of his rank a man stands at (the formation's own left / right as it faces).</summary>
    public enum WallFlankSide
    {
        None,
        Left,
        Right,
    }

    /// <summary>
    /// Step 99: why a man step 41 looks at is or is not TURNED to face the flank now - in gate order (the master switch first). A turned man who stops
    /// being turned is released for the verdict he got (the summary's releases by cause).
    /// </summary>
    public enum WallFlankVerdict
    {
        /// <summary>He faces outward: an end man of a standing shield wall.</summary>
        Turned,

        /// <summary>The master switch (ModEnabled) is off.</summary>
        ModOff,

        /// <summary>ShieldWallHoldsSlots is off (nobody is held).</summary>
        HoldOff,

        /// <summary>ShieldWallFlanksFaceOut is off.</summary>
        SwitchedOff,

        /// <summary>Not held by step 41 now (you, a rider, a siege's attacker, naval, a charge or a retreat, the escape, the wall broke ...).</summary>
        NotHeld,

        /// <summary>Step 42's leash freed him to step out at an enemy (forward, as the game does it).</summary>
        SteppingOut,

        /// <summary>His formation is not in a shield wall (a line, a square, a circle ...).</summary>
        NotShieldWall,

        /// <summary>The wall does not stand (it advances, moves, follows, falls back, or a move has not reached its place) - step 75's test.</summary>
        NotStanding,

        /// <summary>The game has no file / rank for him (unpositioned).</summary>
        Unplaced,

        /// <summary>The wall is too narrow to turn its ends (fewer than 2 x the files + 3 files) - its front stays closed.</summary>
        Narrow,

        /// <summary>Not among the outermost files of his rank (or his rank is too short to have two ends).</summary>
        NotAnEnd,

        /// <summary>An enemy close in FRONT of him (within ShieldWallFlankReleaseMetres of his slot, more ahead than aside) - faces forward until
        /// none for <see cref="WallFlankMath.FrontHoldSeconds"/>.</summary>
        FrontEnemy,

        Count,
    }

    /// <summary>Step 99: one man's turn - a struct in the module's per-man wall state (no allocation).</summary>
    public struct WallFlankState
    {
        /// <summary>Facing outward now (the frame direction, the tendency and the shield written by us).</summary>
        public bool Turned;

        public WallFlankSide Side;

        /// <summary>How far he is turned from the wall's front (degrees; 90 = the full flank).</summary>
        public double Degrees;

        /// <summary>When this turn began (mission time) - the angle is sampled once he had time to turn.</summary>
        public double Since;

        /// <summary>An enemy was close in front of him: forward until this time.</summary>
        public double FrontUntil;

        public bool EverTurned;

        /// <summary>The front-rank corner man (turned <c>ShieldWallFlankFrontCornerDegrees</c>).</summary>
        public bool Corner;
    }

    /// <summary>Step 99's settings, read live: the master switch FIRST, then step 41's ShieldWallHoldsSlots, then ShieldWallFlanksFaceOut.</summary>
    public readonly struct WallFlankRules
    {
        public WallFlankRules(bool modEnabled, bool holdsSlots, bool faceOut, int files, double cornerDegrees, double releaseMetres)
        {
            ModEnabled = modEnabled;
            HoldsSlots = holdsSlots;
            FaceOut = faceOut;
            Files = files < WallFlankMath.MinFiles ? WallFlankMath.MinFiles : files > WallFlankMath.MaxFiles ? WallFlankMath.MaxFiles : files;
            CornerDegrees = Clamp(cornerDegrees, 0, 90, 45);
            ReleaseMetres = Clamp(releaseMetres, 0, WallFlankMath.MaxReleaseMetres, 4);
        }

        public bool ModEnabled { get; }
        public bool HoldsSlots { get; }
        public bool FaceOut { get; }

        /// <summary>How many outermost files at each end turn (1-3).</summary>
        public int Files { get; }

        /// <summary>How far the FRONT rank's end men turn (0 = forward, 90 = the full flank).</summary>
        public double CornerDegrees { get; }

        /// <summary>An enemy within this of his slot and in front of the wall turns him forward again (0 = never - the engine alone).</summary>
        public double ReleaseMetres { get; }

        public bool Enabled => ModEnabled && HoldsSlots && FaceOut;

        public string? OffBecause => !ModEnabled ? "ModEnabled" : !HoldsSlots ? "ShieldWallHoldsSlots" : !FaceOut ? "ShieldWallFlanksFaceOut" : null;

        public static WallFlankRules From(TraxSettings s) => new WallFlankRules(s.ModEnabled, s.ShieldWallHoldsSlots, s.ShieldWallFlanksFaceOut,
            s.ShieldWallFlankFiles, s.ShieldWallFlankFrontCornerDegrees, s.ShieldWallFlankReleaseMetres);

        public bool SameAs(in WallFlankRules o) => ModEnabled == o.ModEnabled && HoldsSlots == o.HoldsSlots && FaceOut == o.FaceOut && Files == o.Files
                                                  && CornerDegrees == o.CornerDegrees && ReleaseMetres == o.ReleaseMetres;

        /// <summary>The settings in one sentence (the mission-start and change lines).</summary>
        public string Describe()
        {
            if (!Enabled) return "OFF (" + OffBecause + ") - every man of a shield wall faces forward (the game's own)";
            return "ON (ShieldWallFlanksFaceOut) - in a held shield wall that STANDS (a stop, or a move at its place) the " + Files + " outermost file"
                   + (Files == 1 ? string.Empty : "s") + " at each end face outward: the ranks behind the front 90 degrees, the front corner "
                   + F0(CornerDegrees) + " degrees; the shield to the flank; forward again "
                   + (ReleaseMetres > 0 ? "while an enemy is within " + F1(ReleaseMetres) + " m in front of him (and " + F1(WallFlankMath.FrontHoldSeconds) + " s after)" : "never by us (ShieldWallFlankReleaseMetres 0 - the engine alone)")
                   + ", when he steps out, stops being an end man, or the wall moves; only walls at least 2 x the files + " + WallFlankMath.ExtraFiles + " files wide; never you";
        }

        private static double Clamp(double v, double min, double max, double fallback) => double.IsNaN(v) ? fallback : v < min ? min : v > max ? max : v;

        internal static string F0(double v) => v.ToString("0", CultureInfo.InvariantCulture);
        internal static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Step 99 - THE SHIELD WALL'S ENDS FACE THE FLANKS (Toni 2026-11-06: "can you make it so that troops on the two sides of the shield wall are
    /// facing the flanks not forward?"; AI_NOTES "Step 99"). Vanilla's <c>LineFormation</c> gives every man the formation's direction; the square
    /// faces its side files out by handing the engine a per-man direction (<c>Direction.TransformToParentUnitF(∓Vec2.Side)</c>) - we hand the same
    /// to the outermost files of a STANDING shield wall: the ranks behind the front at 90 degrees, the front corner at 45 (the front keeps a shield).
    /// Pure: which men, how far, the world direction, when they turn forward again; the module writes it.
    /// </summary>
    public static class WallFlankMath
    {
        public const int MinFiles = 1;
        public const int MaxFiles = 3;
        public const double MaxReleaseMetres = 10;

        /// <summary>A wall turns its ends only when at least 2 x the files + this many files wide - the middle keeps a closed front.</summary>
        public const int ExtraFiles = 3;

        /// <summary>After the last enemy close in front of him he stays forward this long (no flicker at the edge of the release distance).</summary>
        public const double FrontHoldSeconds = 2.0;

        /// <summary>His angle is sampled once he has been turned this long (time to turn his body).</summary>
        public const double SettleSeconds = 1.5;

        /// <summary>The full flank (the ranks behind the front).</summary>
        public const double FlankDegrees = 90;

        /// <summary>
        /// Which end he stands at: <paramref name="leftNeighbours"/> / <paramref name="rightNeighbours"/> = how many men of his rank stand to his left /
        /// right, counted at most up to <paramref name="files"/> (the module walks the game's <c>GetNeighborUnitOfLeftSide</c> / <c>RightSide</c>).
        /// Fewer than the files on one side = an end man of that side; on both sides (a short rank) = none.
        /// </summary>
        public static WallFlankSide Side(int leftNeighbours, int rightNeighbours, int files)
        {
            if (files < MinFiles) files = MinFiles;
            bool left = leftNeighbours >= 0 && leftNeighbours < files;
            bool right = rightNeighbours >= 0 && rightNeighbours < files;
            if (left == right) return WallFlankSide.None;
            return left ? WallFlankSide.Left : WallFlankSide.Right;
        }

        /// <summary>The wall is wide enough to turn its ends: at least 2 x the files + <see cref="ExtraFiles"/> files.</summary>
        public static bool WideEnough(int fileCount, int files) => fileCount >= 2 * files + ExtraFiles;

        /// <summary>How far he turns: the front rank (rank 0) the corner's degrees, every rank behind it the full flank.</summary>
        public static double Degrees(in WallFlankRules r, int rank) => rank == 0 ? r.CornerDegrees : FlankDegrees;

        /// <summary>
        /// The direction he faces in the WORLD (the scene's x / y), turned <paramref name="degrees"/> from the wall's direction (<paramref name="dirX"/>,
        /// <paramref name="dirY"/>) toward his side: the local direction (± sin d, cos d) through the game's <c>Vec2.TransformToParentUnitF</c> - (y·a.x +
        /// x·a.y, −x·a.x + y·a.y), where local +x (<c>Vec2.Side</c>) is the formation's RIGHT. 90 on the left = <c>−Vec2.Side</c> (the square's left side).
        /// </summary>
        public static void Outward(double dirX, double dirY, WallFlankSide side, double degrees, out double x, out double y)
        {
            double len = Math.Sqrt(dirX * dirX + dirY * dirY);
            if (len < 1e-9 || side == WallFlankSide.None)
            {
                x = dirX;
                y = dirY;
                return;
            }
            dirX /= len;
            dirY /= len;
            double rad = degrees * Math.PI / 180;
            double ax = (side == WallFlankSide.Left ? -1 : 1) * Math.Sin(rad), ay = Math.Cos(rad);
            x = dirY * ax + dirX * ay;
            y = -dirX * ax + dirY * ay;
        }

        /// <summary>An enemy at (<paramref name="ex"/>, <paramref name="ey"/>) is close in FRONT of the wall for this man: within
        /// <paramref name="metres"/> of his slot and more ahead (along the wall's facing) than aside. 0 metres = never.</summary>
        public static bool EnemyInFront(in WallFacts f, double ex, double ey, double metres)
        {
            if (!(metres > 0) || double.IsNaN(ex) || double.IsNaN(ey)) return false;
            double dx = ex - f.SlotX, dy = ey - f.SlotY;
            if (dx * dx + dy * dy > metres * metres) return false;
            double len = Math.Sqrt(f.DirX * f.DirX + f.DirY * f.DirY);
            if (len < 1e-9) return false;
            double fx = f.DirX / len, fy = f.DirY / len;
            double along = dx * fx + dy * fy;
            double aside = Math.Abs(dx * fy - dy * fx);
            return along > 0 && along > aside;
        }

        /// <summary>
        /// The verdict in gate order: the switches (the master switch first), held by step 41, not stepping out (step 42), a shield wall, standing
        /// (step 75's test), placed, wide enough, an end man, no enemy close in front (<paramref name="frontEnemy"/> = one now or within the hold).
        /// </summary>
        public static WallFlankVerdict Decide(in WallFlankRules r, bool held, bool steppingOut, WallArrangement arrangement, bool stands, bool placed,
            int fileCount, WallFlankSide side, bool frontEnemy)
        {
            if (!r.ModEnabled) return WallFlankVerdict.ModOff;
            if (!r.HoldsSlots) return WallFlankVerdict.HoldOff;
            if (!r.FaceOut) return WallFlankVerdict.SwitchedOff;
            if (!held) return WallFlankVerdict.NotHeld;
            if (steppingOut) return WallFlankVerdict.SteppingOut;
            if (arrangement != WallArrangement.ShieldWall) return WallFlankVerdict.NotShieldWall;
            if (!stands) return WallFlankVerdict.NotStanding;
            if (!placed) return WallFlankVerdict.Unplaced;
            if (!WideEnough(fileCount, r.Files)) return WallFlankVerdict.Narrow;
            if (side == WallFlankSide.None) return WallFlankVerdict.NotAnEnd;
            if (frontEnemy) return WallFlankVerdict.FrontEnemy;
            return WallFlankVerdict.Turned;
        }

        /// <summary>The gates that need no read of his file and rank (the module reads those only when this says "maybe").</summary>
        public static bool MayTurn(in WallFlankRules r, bool held, bool steppingOut, WallArrangement arrangement, bool stands) =>
            r.Enabled && held && !steppingOut && arrangement == WallArrangement.ShieldWall && stands;

        /// <summary>The angle between two directions, 0-180 degrees (NaN for a zero vector).</summary>
        public static double AngleDegrees(double ax, double ay, double bx, double by)
        {
            double la = Math.Sqrt(ax * ax + ay * ay), lb = Math.Sqrt(bx * bx + by * by);
            if (la < 1e-9 || lb < 1e-9) return double.NaN;
            double c = (ax * bx + ay * by) / (la * lb);
            if (c > 1) c = 1;
            else if (c < -1) c = -1;
            return Math.Acos(c) * 180 / Math.PI;
        }

        public static string Name(WallFlankVerdict v) => v switch
        {
            WallFlankVerdict.Turned => "turned",
            WallFlankVerdict.ModOff => "mod off",
            WallFlankVerdict.HoldOff => "the hold off",
            WallFlankVerdict.SwitchedOff => "switched off",
            WallFlankVerdict.NotHeld => "let go by the wall",
            WallFlankVerdict.SteppingOut => "stepped out",
            WallFlankVerdict.NotShieldWall => "not a shield wall",
            WallFlankVerdict.NotStanding => "the wall moved",
            WallFlankVerdict.Unplaced => "unplaced",
            WallFlankVerdict.Narrow => "too narrow",
            WallFlankVerdict.NotAnEnd => "no longer an end man",
            WallFlankVerdict.FrontEnemy => "an enemy in front",
            _ => v.ToString(),
        };

        public static string Name(WallFlankSide s) => s == WallFlankSide.Left ? "left" : s == WallFlankSide.Right ? "right" : "none";
    }

    /// <summary>
    /// Step 99 per-mission: men turned, turns, man-seconds turned, releases by cause, and THE PROOF - the angle between each turned man's real facing
    /// and the one we asked for (front-rank corners and the flank men apart), with his angle from the wall's front beside it: near 0 / near the asked
    /// angle = the engine honours a per-man facing in a line formation; near the asked angle / near 0 = it does not. The [summary] "wall flanks" line.
    /// </summary>
    public sealed class WallFlankStats
    {
        public const int BinDegrees = 5;
        public const int Bins = 180 / BinDegrees + 1;

        private readonly int[][] _offTarget = { new int[Bins], new int[Bins] };
        private readonly double[] _offSum = new double[2], _fromFrontSum = new double[2];
        private readonly int[] _samples = new int[2];
        private readonly int[] _releases = new int[(int)WallFlankVerdict.Count];

        public int MenTurned, MenCorner, Turns, FrontHolds, TurnedSamples, SwitchChanges, Errors;
        public long Writes;
        public bool EverOn;
        public double SampleSeconds = 0.5;

        public void Turn(bool firstForHim, bool corner)
        {
            Turns++;
            if (!firstForHim) return;
            MenTurned++;
            if (corner) MenCorner++;
        }

        public void Release(WallFlankVerdict why)
        {
            if (why >= 0 && why < WallFlankVerdict.Count) _releases[(int)why]++;
        }

        public int Releases(WallFlankVerdict why) => _releases[(int)why];

        /// <summary>One settled turned man: <paramref name="offTarget"/> = his facing against the one asked, <paramref name="fromFront"/> = against the
        /// wall's front.</summary>
        public void AddAngle(bool corner, double offTarget, double fromFront)
        {
            if (double.IsNaN(offTarget)) return;
            int w = corner ? 0 : 1;
            int b = (int)(offTarget / BinDegrees);
            if (b < 0) b = 0;
            if (b >= Bins) b = Bins - 1;
            _offTarget[w][b]++;
            _samples[w]++;
            _offSum[w] += offTarget;
            _fromFrontSum[w] += double.IsNaN(fromFront) ? 0 : fromFront;
        }

        public int Samples(bool corner) => _samples[corner ? 0 : 1];

        /// <summary>The median angle off the asked facing (the 5-degree bin's middle; NaN = no samples).</summary>
        public double MedianOffTarget(bool corner)
        {
            int w = corner ? 0 : 1, n = _samples[w];
            if (n == 0) return double.NaN;
            int half = (n + 1) / 2, run = 0;
            for (int b = 0; b < Bins; b++)
            {
                run += _offTarget[w][b];
                if (run >= half) return b * BinDegrees + BinDegrees / 2.0;
            }
            return 180;
        }

        /// <summary>The share of samples within <paramref name="degrees"/> of the asked facing.</summary>
        public double ShareWithin(bool corner, double degrees)
        {
            int w = corner ? 0 : 1, n = _samples[w];
            if (n == 0) return double.NaN;
            int k = 0;
            for (int b = 0; b < Bins && (b + 1) * BinDegrees <= degrees + 1e-9; b++) k += _offTarget[w][b];
            return (double)k / n;
        }

        public double MeanOffTarget(bool corner) => _samples[corner ? 0 : 1] > 0 ? _offSum[corner ? 0 : 1] / _samples[corner ? 0 : 1] : double.NaN;
        public double MeanFromFront(bool corner) => _samples[corner ? 0 : 1] > 0 ? _fromFrontSum[corner ? 0 : 1] / _samples[corner ? 0 : 1] : double.NaN;

        /// <summary>The [summary] line.</summary>
        public string SummaryLine(in WallFlankRules r)
        {
            var sb = new StringBuilder(640);
            sb.Append("wall flanks (step 99 - ").Append(r.Enabled ? "ShieldWallFlanksFaceOut ON" : "OFF (" + r.OffBecause + ")").Append(" now")
              .Append(EverOn ? string.Empty : ", never on this battle").Append(", ").Append(r.Files).Append(" file(s) a side, the front corner ")
              .Append(WallFlankRules.F0(r.CornerDegrees)).Append(" deg, released by an enemy within ").Append(WallFlankRules.F1(r.ReleaseMetres)).Append(" m in front): ");
            if (Turns == 0) return sb.Append("no end man of a standing shield wall was turned this battle").ToString();
            sb.Append("end men turned ").Append(MenTurned).Append(" (front corners ").Append(MenCorner).Append("), turns ").Append(Turns)
              .Append(", turned ").Append((TurnedSamples * SampleSeconds).ToString("0", CultureInfo.InvariantCulture)).Append(" man-s, facing writes ").Append(Writes)
              .Append(" | THE ENGINE'S ANSWER - his real facing against the one asked (settled ").Append(WallFlankRules.F1(WallFlankMath.SettleSeconds)).Append(" s): ")
              .Append("flank men (90 deg) ").Append(Angles(false)).Append("; front corners (").Append(WallFlankRules.F0(r.CornerDegrees)).Append(" deg) ").Append(Angles(true))
              .Append(" (off the asked near 0 = the engine turns them; near the asked angle and near 0 from the front = it keeps them facing forward)")
              .Append(" | turned forward again: ");
            bool any = false;
            for (int i = 1; i < (int)WallFlankVerdict.Count; i++)
            {
                if (_releases[i] == 0) continue;
                if (any) sb.Append(", ");
                sb.Append(WallFlankMath.Name((WallFlankVerdict)i)).Append(' ').Append(_releases[i]);
                any = true;
            }
            if (!any) sb.Append("none");
            sb.Append("; enemy-in-front holds ").Append(FrontHolds).Append("; switch changes ").Append(SwitchChanges).Append(", errors ").Append(Errors);
            return sb.ToString();
        }

        private string Angles(bool corner)
        {
            int n = Samples(corner);
            if (n == 0) return "no samples";
            return "off the asked: median " + WallFlankRules.F0(MedianOffTarget(corner)) + " deg, mean " + WallFlankRules.F1(MeanOffTarget(corner)) + ", within 20 deg "
                   + (100 * ShareWithin(corner, 20)).ToString("0", CultureInfo.InvariantCulture) + "%; from the wall's front mean " + WallFlankRules.F1(MeanFromFront(corner))
                   + " deg (n " + n + ")";
        }
    }
}

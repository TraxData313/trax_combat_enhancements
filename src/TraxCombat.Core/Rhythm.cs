using System;
using System.Collections.Generic;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 78 - THE BATTLE'S RHYTHM (Toni 2026-10-22, his sketch 1-4: two packs of dogs that do not want to get hurt but will not back down and
    /// will not do reckless things; AI_NOTES "Step 78", DESIGN decision 72): the ROTATION (a tired front man swaps with a fresher man right behind him).
    /// Step 78c removed 78 / 78b's unit-wide LULL (DESIGN decision 74 - Toni: "dont script for all of them to walk away"); its place is each man's own
    /// REST (<see cref="RestRules"/>), which rides the same look and the same input wish. The settings, read live: the master switch FIRST, then the
    /// Athletics system (a bar to read), then each part's own switch, then the numbers.
    /// </summary>
    public readonly struct RhythmRules
    {
        public RhythmRules(bool modEnabled, bool athleticsEnabled, bool rotationEnabled, bool restEnabled = true,
            int tiredPercent = 35, bool useBraceFloor = true, int fresherPercent = 25, double behindMetres = 1.5, double fileMetres = 1.0,
            double contactMetres = 2.5, double besideMetres = 0.7, double approachSeconds = 3, double maxSeconds = 6, double cooldownSeconds = 12,
            double restSeconds = 6)
        {
            ModEnabled = modEnabled;
            AthleticsEnabled = athleticsEnabled;
            RotationEnabled = rotationEnabled;
            RestEnabled = restEnabled;
            TiredPercent = ClampI(tiredPercent, 0, 100);
            UseBraceFloor = useBraceFloor;
            FresherPercent = ClampI(fresherPercent, 5, 100);
            BehindMetres = Clamp(behindMetres, 0.5, 4);
            FileMetres = Clamp(fileMetres, 0.2, 3);
            ContactMetres = Clamp(contactMetres, 1, 6);
            BesideMetres = Clamp(besideMetres, 0.3, 2);
            ApproachSeconds = Clamp(approachSeconds, 0.5, 10);
            MaxSeconds = Clamp(maxSeconds, 2, 20);
            CooldownSeconds = Clamp(cooldownSeconds, 1, 120);
            RestSeconds = Clamp(restSeconds, 0, 30);
        }

        public static RhythmRules From(TraxSettings s) =>
            new RhythmRules(s.ModEnabled, s.AthleticsEnabled, s.RotationEnabled, s.RestEnabled, s.RotationTiredPercent, s.RotationUseBraceFloor,
                s.RotationFresherPercent, s.RotationBehindMetres, s.RotationFileMetres, s.RotationContactMetres, s.RotationBesideMetres, s.RotationApproachSeconds,
                s.RotationMaxSeconds, s.RotationCooldownSeconds, s.RotationRestSeconds);

        public bool ModEnabled { get; }

        /// <summary>The Athletics system - the rhythm reads the men's bars; without it nothing tires, so nothing rotates or rests.</summary>
        public bool AthleticsEnabled { get; }

        /// <summary>RotationEnabled - a tired front man and a fresher mate right behind him swap places.</summary>
        public bool RotationEnabled { get; }

        /// <summary>Step 78c: RestEnabled - each tired man steps back and waits to recover (<see cref="RestRules"/> holds its numbers).</summary>
        public bool RestEnabled { get; }

        /// <summary>RotationTiredPercent: a front man at or below this bar is tired (or at his brace floor, <see cref="UseBraceFloor"/>).</summary>
        public int TiredPercent { get; }

        public bool UseBraceFloor { get; }

        /// <summary>RotationFresherPercent: the mate behind must have this many points of the bar more.</summary>
        public int FresherPercent { get; }

        /// <summary>RotationBehindMetres: the mate stands at most this far behind him (toward their own side).</summary>
        public double BehindMetres { get; }

        /// <summary>RotationFileMetres: ... and at most this far to the side of his file.</summary>
        public double FileMetres { get; }

        /// <summary>RotationContactMetres: an enemy within this = the man is in contact (a front man of the grind).</summary>
        public double ContactMetres { get; }

        /// <summary>RotationBesideMetres: the fresh man steps up this far beside the tired one before the tired one backs off.</summary>
        public double BesideMetres { get; }

        /// <summary>RotationApproachSeconds: the fresh man must be beside within this, else the swap is called off (the tired man never left).</summary>
        public double ApproachSeconds { get; }

        /// <summary>RotationMaxSeconds: a whole swap's longest time.</summary>
        public double MaxSeconds { get; }

        /// <summary>RotationCooldownSeconds: at most one swap per man in this many seconds.</summary>
        public double CooldownSeconds { get; }

        /// <summary>RotationRestSeconds: the man who went back does not walk forward past his new place for this long.</summary>
        public double RestSeconds { get; }

        /// <summary>The rotation acts: the master switch FIRST, then Athletics, then its own switch.</summary>
        public bool RotationOn => ModEnabled && AthleticsEnabled && RotationEnabled;

        /// <summary>Step 78c: the rest acts (the same gates, its own switch).</summary>
        public bool RestOn => ModEnabled && AthleticsEnabled && RestEnabled;

        /// <summary>Either part acts (the input wish's gate).</summary>
        public bool AnyOn => RotationOn || RestOn;

        public string? RotationOffBecause => !ModEnabled ? "ModEnabled" : !AthleticsEnabled ? "AthleticsEnabled" : !RotationEnabled ? "RotationEnabled" : null;

        public string Describe() =>
            "the battle's rhythm (step 78): ROTATION: an AI man on foot in contact (an enemy within " + F(ContactMetres) + " m) whose bar is at or below " + TiredPercent + "%"
            + (UseBraceFloor ? " or his brace floor" : string.Empty) + " swaps places with a mate of his unit standing right behind him (within "
            + F(BehindMetres) + " m behind, " + F(FileMetres) + " m of his file) who has " + FresherPercent + "+ points more - the fresh man steps up "
            + F(BesideMetres) + " m beside him first (within " + F(ApproachSeconds) + " s, else called off) while the tired man holds and guards, then the slots swap "
            + "(the game's Formation.SwitchUnitLocations) and the tired man backs into the fresh man's place facing the enemy (whole swap " + F(MaxSeconds)
            + " s at most); one swap per man per " + F(CooldownSeconds) + " s, never in an attack or a stagger; the man who went back holds his place "
            + F(RestSeconds) + " s - " + (RotationOn ? "ON" : "OFF (" + RotationOffBecause + "): measured only") + "; both sides, never you, never riders";

        private static double Clamp(double v, double lo, double hi) => double.IsNaN(v) ? lo : v < lo ? lo : v > hi ? hi : v;

        private static int ClampI(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

        internal static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>Step 78: where a man stands in a swap.</summary>
    public enum RotationPhase
    {
        None,

        /// <summary>The tired man HOLDS his place and guards (his attacks held) until the fresh man is beside him.</summary>
        Hold,

        /// <summary>The fresh man walks up beside the tired one.</summary>
        Approach,

        /// <summary>Beside: the slots swapped, the tired man backs into the fresh man's place, facing the enemy.</summary>
        Back,

        /// <summary>... and the fresh man steps into the tired man's place.</summary>
        In,
    }

    /// <summary>Step 78: why a tired front man looked at did not swap.</summary>
    public enum RotationRefusal
    {
        /// <summary>Nobody of his unit right behind him (his file, within the distance).</summary>
        NoMate,

        /// <summary>Men behind him, none fresher by the margin.</summary>
        NotFresher,

        /// <summary>A fresher man behind, but swinging, staggered, in a swap / its cooldown, or walked by another system.</summary>
        MateBusy,

        /// <summary>He himself is swinging, staggered or kicking.</summary>
        Busy,

        /// <summary>He himself is walked by another system (a step back, the wall's pull, the press's walk, courage's walk back).</summary>
        OwnWish,

        /// <summary>A held wall man whose slots cannot be swapped (no positioned slot) - he never walks out of the wall.</summary>
        NoSlots,

        /// <summary>No direction to the enemy to tell behind from in front.</summary>
        NoEnemy,

        Count,
    }

    /// <summary>Step 78: how a swap ended.</summary>
    public enum RotationEnd
    {
        /// <summary>Both in their new places.</summary>
        Done,

        /// <summary>The fresh man never came beside in time - called off, the tired man never left.</summary>
        NeverBeside,

        /// <summary>The whole swap took too long (both let go where they stood).</summary>
        Timeout,

        /// <summary>The fresh man was staggered / knocked down on his way up.</summary>
        Staggered,

        /// <summary>One of them fell or left the field.</summary>
        LeftField,

        /// <summary>A system or the master switch went off.</summary>
        SwitchedOff,

        /// <summary>One of them was excluded (fled, the player took him, a charge broke his wall...).</summary>
        Excluded,

        MissionEnd,
        Error,
        Count,
    }

    /// <summary>
    /// Step 78 - the pure part: who is tired, who stands right behind him, who is fresher, busy men, a fresh man BESIDE the tired one, a unit's
    /// front, step 78b's gap from the men (step 78c: a diagnostic only). No game types: the action codes are the engine's
    /// <c>Agent.ActionCodeType</c> values (v1.4.8 - the offline smoke checks them against the game's enum).
    /// </summary>
    public static class RhythmMath
    {
        /// <summary>The man behind must stand at least this far behind (m) - a man at his shoulder is beside him, not behind. Plumbing.</summary>
        public const double MinBehindMetres = 0.3;

        /// <summary>The fresh man is BESIDE when he stands no more than this behind the tired man's depth (m). Plumbing.</summary>
        public const double BesideDepthMetres = 0.4;

        /// <summary>... and within the beside distance + this sideways (m). Plumbing.</summary>
        public const double BesideSlackMetres = 0.6;

        /// <summary>A walk's place is reached within this (m). Plumbing.</summary>
        public const double ArriveMetres = 0.4;

        /// <summary>The forward clamp bites within this of a line (m) - PressMath's look-ahead (a 6 m/s run between two 0.1 s steers). Plumbing.</summary>
        public const double LookaheadMetres = PressMath.LookaheadMetres;

        /// <summary>A mate counts only when his own look was this recent (s) - a man nobody looks at (a rider, a fleeing man) never. Plumbing.</summary>
        public const double MateFreshSeconds = 1.5;

        /// <summary>The table publishes every this (s). Plumbing.</summary>
        public const double PublishSeconds = 0.5;

        /// <summary>A unit not published for this long is stale (s). Plumbing.</summary>
        public const double StaleSeconds = 2.0;

        /// <summary>A unit's front = this quantile of its men's places toward the enemy. Plumbing.</summary>
        public const double FrontQuantile = 0.9;

        public const int MaxSamples = 512;
        public const int Votes = 4;

        /// <summary>Step 78b: the gap between two units is measured in lateral bins this wide (m) along the contact. Plumbing.</summary>
        public const double GapBinMetres = 2.0;

        /// <summary>... this many bins (centred on the pair's middle: 128 m of front). Plumbing.</summary>
        public const int GapBins = 64;

        /// <summary>The gap's measure: this quantile of the bins' gaps (most of the contact, a stuck man or two aside). Plumbing.</summary>
        public const double GapQuantile = 0.25;

        /// <summary>The bar at or below which he is TIRED: the larger of the setting and (with the brace floor counted) his own brace floor.</summary>
        public static double TiredBar(in RhythmRules r, double braceFloor) =>
            Math.Max(r.TiredPercent / 100.0, r.UseBraceFloor && !double.IsNaN(braceFloor) ? braceFloor : 0);

        public static bool IsTired(in RhythmRules r, double bar, double braceFloor) => !double.IsNaN(bar) && bar <= TiredBar(in r, braceFloor) + 1e-9;

        /// <summary>The mate is FRESHER: the margin above the tired man's bar, and not tired himself.</summary>
        public static bool Fresher(in RhythmRules r, double tiredBar, double mateBar, double mateTiredBar) =>
            !double.IsNaN(mateBar) && mateBar >= tiredBar + r.FresherPercent / 100.0 - 1e-9 && mateBar > mateTiredBar + 1e-9;

        /// <summary>A point's place against a man: <paramref name="along"/> toward the enemy (u), <paramref name="across"/> to the side.</summary>
        public static void AlongAcross(double px, double py, double ux, double uy, double x, double y, out double along, out double across)
        {
            double dx = x - px, dy = y - py;
            along = dx * ux + dy * uy;
            across = -dx * uy + dy * ux;
        }

        /// <summary>The mate stands RIGHT BEHIND him: between <see cref="MinBehindMetres"/> and the behind distance toward their own side, within the
        /// file's width to the side.</summary>
        public static bool Behind(in RhythmRules r, double along, double across) =>
            along <= -MinBehindMetres && along >= -r.BehindMetres - 1e-9 && Math.Abs(across) <= r.FileMetres + 1e-9;

        /// <summary>A mate COVERS him (he is not a front-rank man): standing in front of him toward the enemy, short of the enemy (<paramref name="reach"/>),
        /// within half the file's width to the side.</summary>
        public static bool Covers(in RhythmRules r, double along, double across, double reach) =>
            along >= MinBehindMetres && along < reach && Math.Abs(across) <= 0.5 * r.FileMetres + 0.1;

        /// <summary>The engine's action codes a man is BUSY in: an attack (ready / release / parried / blocked), a fall, a kick or bash, a strike
        /// reaction (the stagger family). The game's <c>Agent.ActionCodeType</c>.</summary>
        public const int ActReadyMelee = 19, ActFall = 23, ActKick = 28, ActWeaponBash = 31, ActStrikeLight = 48, ActMountStrike = 52;

        public static bool BusyAction(int action) =>
            (action >= ActReadyMelee && action <= ActFall) || (action >= ActKick && action <= ActWeaponBash) || (action >= ActStrikeLight && action <= ActMountStrike);

        /// <summary>Busy: an attack, a kick or a stagger under way (the action), or reeling / down from an impact outcome, or a kick attempt of ours.</summary>
        public static bool Busy(int action, bool reeling, bool down, bool kicking) => BusyAction(action) || reeling || down || kicking;

        /// <summary>The side the fresh man comes up on (+1 / −1): his own side of the tired man's file, +1 when straight behind.</summary>
        public static int SideOf(double across) => across < -0.15 ? -1 : 1;

        /// <summary>The point BESIDE the tired man (at his depth, <paramref name="beside"/> to the side).</summary>
        public static void BesidePoint(double px, double py, double ux, double uy, int side, double beside, out double bx, out double by)
        {
            double ex = -uy, ey = ux;
            bx = px + side * beside * ex;
            by = py + side * beside * ey;
        }

        /// <summary>The fresh man is BESIDE the tired one: no more than <see cref="BesideDepthMetres"/> behind his depth and within the beside distance +
        /// the slack to the side - the line has its man at the front either way.</summary>
        public static bool IsBeside(in RhythmRules r, double tx, double ty, double fx, double fy, double ux, double uy)
        {
            AlongAcross(tx, ty, ux, uy, fx, fy, out double along, out double across);
            return along >= -BesideDepthMetres && Math.Abs(across) <= r.BesideMetres + BesideSlackMetres;
        }

        /// <summary>A unit's FRONT and its front men's mean bar from its men's places toward the enemy (<paramref name="along"/>, relative to anything)
        /// and their bars: the front = the <see cref="FrontQuantile"/> of the places; the front men = within <paramref name="depth"/> of it.
        /// <paramref name="sorted"/> is scratch. False for nobody.</summary>
        public static bool FrontOf(double[] along, double[] bars, int n, double depth, double[] sorted, out double front, out double frontBar, out int frontMen)
        {
            front = frontBar = double.NaN;
            frontMen = 0;
            if (n <= 0) return false;
            Array.Copy(along, sorted, n);
            Array.Sort(sorted, 0, n);
            front = BlockHoldMath.Quantile(sorted, n, FrontQuantile);
            double sum = 0;
            for (int i = 0; i < n; i++)
            {
                if (along[i] < front - depth) continue;
                sum += bars[i];
                frontMen++;
            }
            frontBar = frontMen > 0 ? sum / frontMen : double.NaN;
            return frontMen > 0;
        }

        /// <summary>
        /// Step 78b - THE GAP FROM THE MEN: two units' men in the pair's frame (along = toward B, lat = across, both relative to the same origin), binned
        /// across in <see cref="GapBinMetres"/> bins; in each bin with men of both, A's front-most and B's rear-most toward A - the gap there = B's - A's
        /// (negative = meshed). Returns the bins counted; out: the mean / the 10th / <see cref="GapQuantile"/> / 90th percentile of the gaps, the contact
        /// PLANE (the median of the bins' midpoints) and each side's FRONT (the median of its nearest men). Scratch arrays of <see cref="GapBins"/>.
        /// Allocation-free.
        /// </summary>
        public static int MeasureGap(double[] aAlong, double[] aLat, int na, double[] bAlong, double[] bLat, int nb, double[] maxA, double[] minB, double[] gaps,
            double[] mids, double[] fa, double[] fb, out double mean, out double q10, out double qHold, out double q90, out double plane, out double frontA, out double frontB)
        {
            mean = q10 = qHold = q90 = plane = frontA = frontB = double.NaN;
            int bins = Math.Min(GapBins, Math.Min(maxA.Length, minB.Length));
            for (int i = 0; i < bins; i++)
            {
                maxA[i] = double.NegativeInfinity;
                minB[i] = double.PositiveInfinity;
            }
            for (int i = 0; i < na; i++)
            {
                int k = (int)Math.Floor(aLat[i] / GapBinMetres) + bins / 2;
                if (k < 0 || k >= bins) continue;
                if (aAlong[i] > maxA[k]) maxA[k] = aAlong[i];
            }
            for (int i = 0; i < nb; i++)
            {
                int k = (int)Math.Floor(bLat[i] / GapBinMetres) + bins / 2;
                if (k < 0 || k >= bins) continue;
                if (bAlong[i] < minB[k]) minB[k] = bAlong[i];
            }
            int n = 0;
            double sum = 0;
            for (int k = 0; k < bins; k++)
            {
                if (double.IsNegativeInfinity(maxA[k]) || double.IsPositiveInfinity(minB[k])) continue;
                double g = minB[k] - maxA[k];
                gaps[n] = g;
                mids[n] = 0.5 * (minB[k] + maxA[k]);
                fa[n] = maxA[k];
                fb[n] = minB[k];
                sum += g;
                n++;
            }
            if (n == 0) return 0;
            mean = sum / n;
            Array.Sort(gaps, 0, n);
            Array.Sort(mids, 0, n);
            Array.Sort(fa, 0, n);
            Array.Sort(fb, 0, n);
            q10 = BlockHoldMath.Quantile(gaps, n, 0.1);
            qHold = BlockHoldMath.Quantile(gaps, n, GapQuantile);
            q90 = BlockHoldMath.Quantile(gaps, n, 0.9);
            plane = BlockHoldMath.Quantile(mids, n, 0.5);
            frontA = BlockHoldMath.Quantile(fa, n, 0.5);
            frontB = BlockHoldMath.Quantile(fb, n, 0.5);
            return n;
        }

        public static string RefusalName(RotationRefusal r) => r switch
        {
            RotationRefusal.NoMate => "nobody right behind",
            RotationRefusal.NotFresher => "nobody fresher behind",
            RotationRefusal.MateBusy => "the fresher man busy",
            RotationRefusal.Busy => "he was swinging / staggered",
            RotationRefusal.OwnWish => "another system walked him",
            RotationRefusal.NoSlots => "a wall man without slots",
            RotationRefusal.NoEnemy => "no enemy direction",
            _ => r.ToString(),
        };

        public static string EndName(RotationEnd e) => e switch
        {
            RotationEnd.Done => "done",
            RotationEnd.NeverBeside => "never beside (called off)",
            RotationEnd.Timeout => "too long",
            RotationEnd.Staggered => "the fresh man staggered",
            RotationEnd.LeftField => "left the field",
            RotationEnd.SwitchedOff => "switched off",
            RotationEnd.Excluded => "excluded",
            RotationEnd.MissionEnd => "mission end",
            RotationEnd.Error => "error",
            _ => e.ToString(),
        };
    }

    /// <summary>Step 78: one man's rhythm - his last look (his unit, the time, eligible as a mate), his swap (role, phase, partner, the places), his
    /// rest after a swap; step 78c: his own REST (his marks, where it began, his nearest enemy, the farthest he went). Allocated once per tracked man.</summary>
    public sealed class RhythmMind
    {
        /// <summary>His unit (FearMath.UnitKey) at his last look, when, and whether that look let him take part (courage's gates / a held wall man).</summary>
        public int Key = -1;
        public double LookedAt = double.NaN;
        public bool Eligible;

        /// <summary>A held wall man at his last look (his slot is the wall's - the swap goes through the slot, the rest stays in it).</summary>
        public bool Held;

        /// <summary>In contact at his last look.</summary>
        public bool Contact;

        /// <summary>The swap: his phase, his partner, since when (the swap and this phase), the side the fresh man comes up on, the direction to the
        /// enemy at the start (u), the tired man's and the fresh man's start places.</summary>
        public RotationPhase Phase;
        public Fighter? Partner;
        public bool Tired;
        public double Since = double.NaN, PhaseSince = double.NaN;
        public int Side = 1;
        public double UX, UY;
        public double TiredX, TiredY, FreshX, FreshY;
        public bool Swapped, Arrived;

        /// <summary>His walk's place now (the swap's) and whether he is walking.</summary>
        public double ToX, ToY;

        /// <summary>A held wall man's slot while he swaps (the wall's walk target agrees with the swap's).</summary>
        public bool SlotOverride;
        public double SlotX, SlotY;

        /// <summary>No swap before this; after going back he holds his new place (no walking forward past it) until <see cref="RestUntil"/>.</summary>
        public double NextRotation = double.NegativeInfinity, RestUntil = double.NegativeInfinity, RestLine = double.NaN;

        // ---- step 78c: his own rest
        /// <summary>Resting now; in a wall slot (he never leaves it); since; where it began; his marks (rolled once per rest - the tired mark at the
        /// end of the last one).</summary>
        public bool Resting, RestWall;
        public double RestSince = double.NaN, RestStartX, RestStartY;
        public double TiredMark = double.NaN, ReadyMark = double.NaN, ReadyCapped = double.NaN;

        /// <summary>His nearest enemy at his last look (place, distance) - the steer backs away from it; his last place (the contagion's neighbours).</summary>
        public double EnemyX = double.NaN, EnemyY = double.NaN, EnemyDist = double.NaN, LastX = double.NaN, LastY = double.NaN;

        /// <summary>The farthest from where the rest began, and behind his own front, this rest (m).</summary>
        public double RestBack, RestBehindFront;
        public bool RestFarCounted;

        /// <summary>His input wish is on (listed in the logic's rhythm men), the next steer, the next ground check, the hook's counters at its start.</summary>
        public bool Wish;
        public double NextSteer, NextEdge;
        public long MoveAtStart, ClampAtStart, HeldAtStart, RestMoveAtStart, RestHeldAtStart;

        public bool EverRotated;

        public bool InSwap => Phase != RotationPhase.None;

        /// <summary>The swap is over for him (the logic keeps the rest and the cooldown).</summary>
        public void EndSwap()
        {
            Phase = RotationPhase.None;
            Partner = null;
            Tired = false;
            SlotOverride = false;
            Swapped = Arrived = false;
            Since = PhaseSince = double.NaN;
        }

        /// <summary>His own rest is over (the tired mark is rolled afresh at his next look).</summary>
        public void EndRest()
        {
            Resting = RestWall = false;
            RestSince = double.NaN;
            TiredMark = ReadyMark = ReadyCapped = double.NaN;
            RestBack = RestBehindFront = 0;
            RestFarCounted = false;
        }
    }

    /// <summary>
    /// Step 78's UNIT TABLE (team x formation, FearMath.UnitKey): every look notes the man (his place, his bar, in contact or not, the unit of his
    /// nearest enemy, his unit's order, the player's unit or not); every <see cref="RhythmMath.PublishSeconds"/> each unit's centroid, its enemy unit
    /// (the one its men see nearest - a held wall's men see none, its attackers' votes pair it), its FRONT toward that enemy (step 78c: the rest's
    /// "behind his own front" measure) and - a DIAGNOSTIC only, never a trigger - the natural gap between two engaged fronts measured from the men
    /// (step 78b's 2 m bins). Step 78c removed 78 / 78b's lull from it. Arrays allocated once per unit and battle; allocation-free after.
    /// </summary>
    public sealed class RhythmUnits
    {
        private const int N = FearMath.UnitCount;
        private const int V = RhythmMath.Votes;
        private const int M = RhythmMath.MaxSamples;
        private const int G = RhythmMath.GapBins;

        /// <summary>A unit's front men stand within this of its front (their mean bar is published). Plumbing.</summary>
        public const double FrontDepthMetres = 1.5;

        /// <summary>Step 78c: the men's directions to their nearest enemies set the unit's axis when their mean is at least this long (they agree) and it
        /// points the centroids' way. Plumbing.</summary>
        public const double AxisAgreement = 0.5;

        /// <summary>A pair is ENGAGED for the gap diagnostic while either had men in contact within this (s) ... Plumbing.</summary>
        public const double EngagedSeconds = 10;

        /// <summary>... and its gap is below this (m) - two units walking up to each other are not a fight yet. Plumbing.</summary>
        public const double EngagedGapMetres = 15;

        // this period
        private readonly int[] _n = new int[N], _contact = new int[N], _held = new int[N];
        private readonly double[]?[] _x = new double[N][], _y = new double[N][], _bar = new double[N][];
        private readonly double[] _sx = new double[N], _sy = new double[N], _ex = new double[N], _ey = new double[N];
        private readonly int[] _en = new int[N];
        private readonly BraceOrderKind[] _kind = new BraceOrderKind[N];
        private readonly bool[] _player = new bool[N];
        private readonly int[] _voteKey = new int[N * V], _voteN = new int[N * V];

        // scratch
        private readonly double[] _alg = new double[M], _sorted = new double[M];
        private readonly double[] _ga = new double[M], _gl = new double[M], _hb = new double[M], _hl = new double[M];
        private readonly double[] _maxA = new double[G], _minB = new double[G], _gaps = new double[G], _mids = new double[G], _fa = new double[G], _fb = new double[G];

        // published - the units
        public readonly bool[] Valid = new bool[N];
        public readonly double[] PublishedAt = new double[N];
        public readonly int[] Men = new int[N], ContactMen = new int[N], FrontMen = new int[N], HeldMen = new int[N];
        public readonly double[] CX = new double[N], CY = new double[N];
        public readonly int[] Enemy = new int[N];

        /// <summary>The direction toward its enemy unit, its front (along it, a world scalar = the front point · u) and the front men's mean bar.</summary>
        public readonly double[] UX = new double[N], UY = new double[N], Front = new double[N], FrontBar = new double[N];
        public readonly BraceOrderKind[] Kind = new BraceOrderKind[N];
        public readonly bool[] Player = new bool[N];

        /// <summary>When the unit last had men in contact.</summary>
        public readonly double[] ContactSeen = new double[N];

        /// <summary>Step 78c's diagnostic: the last measured natural gap toward its enemy (the 25th percentile of the 2 m bins; NaN = not engaged).</summary>
        public readonly double[] Gap = new double[N];

        /// <summary>The diagnostic's sink (the logic's RestStats; null = not counted) and its metres.</summary>
        public RestStats? GapSink;
        public double GapDiagnosticMetres = 2;

        public long Publishes, Pairs, GapMeasures;

        private double _nextPublish = double.NaN;

        public RhythmUnits()
        {
            Reset();
        }

        public void Reset()
        {
            for (int k = 0; k < N; k++)
            {
                ClearPeriod(k);
                Valid[k] = Player[k] = false;
                PublishedAt[k] = ContactSeen[k] = Gap[k] = double.NaN;
                Enemy[k] = -1;
                Men[k] = ContactMen[k] = FrontMen[k] = HeldMen[k] = 0;
                FrontBar[k] = Front[k] = double.NaN;
                Kind[k] = BraceOrderKind.Count;
            }
            _nextPublish = double.NaN;
            Publishes = Pairs = GapMeasures = 0;
        }

        private void ClearPeriod(int k)
        {
            _n[k] = _contact[k] = _held[k] = _en[k] = 0;
            _sx[k] = _sy[k] = _ex[k] = _ey[k] = 0;
            for (int i = 0; i < V; i++)
            {
                _voteKey[k * V + i] = -1;
                _voteN[k * V + i] = 0;
            }
        }

        /// <summary>The unit is published (and fresh).</summary>
        public bool Live(int key, double now) =>
            key >= 0 && key < N && Valid[key] && !double.IsNaN(PublishedAt[key]) && now - PublishedAt[key] <= RhythmMath.StaleSeconds && now >= PublishedAt[key] - 1e-9;

        /// <summary>Step 78c: how far a man stands BEHIND his unit's published front (m; NaN = no front).</summary>
        public double BehindFront(int key, double x, double y, double now)
        {
            if (!Live(key, now) || double.IsNaN(Front[key])) return double.NaN;
            return Front[key] - (x * UX[key] + y * UY[key]);
        }

        /// <summary>One man of the unit at this look (<paramref name="enemyX"/> / <paramref name="enemyY"/>: his nearest enemy, NaN = none near - step 78c:
        /// the unit's direction to its enemy is the mean of its men's, so a meshed fight keeps its axis).</summary>
        public void Note(int key, double x, double y, double bar, bool contact, int foeKey, BraceOrderKind kind, bool player, bool held, double enemyX = double.NaN,
            double enemyY = double.NaN)
        {
            if (key < 0 || key >= N || double.IsNaN(x) || double.IsNaN(y)) return;
            if (!double.IsNaN(enemyX) && !double.IsNaN(enemyY))
            {
                double dx = enemyX - x, dy = enemyY - y, len = Math.Sqrt(dx * dx + dy * dy);
                if (len > 1e-6)
                {
                    _ex[key] += dx / len;
                    _ey[key] += dy / len;
                    _en[key]++;
                }
            }
            _kind[key] = kind;
            _player[key] = player;
            if (contact) _contact[key]++;
            if (held) _held[key]++;
            _sx[key] += x;
            _sy[key] += y;
            if (foeKey >= 0 && foeKey < N && foeKey != key) Vote(key, foeKey);
            int s = _n[key];
            _n[key] = s + 1;
            if (s >= M) return;
            var xs = _x[key];
            if (xs == null)
            {
                xs = _x[key] = new double[M]; // once per unit and battle
                _y[key] = new double[M];
                _bar[key] = new double[M];
            }
            xs[s] = x;
            _y[key]![s] = y;
            _bar[key]![s] = double.IsNaN(bar) ? 1 : bar;
        }

        private void Vote(int key, int enemyKey)
        {
            int o = key * V, free = -1, low = o;
            for (int i = o; i < o + V; i++)
            {
                if (_voteKey[i] == enemyKey)
                {
                    _voteN[i]++;
                    return;
                }
                if (_voteKey[i] < 0 && free < 0) free = i;
                if (_voteN[i] < _voteN[low]) low = i;
            }
            int at = free >= 0 ? free : low;
            _voteKey[at] = enemyKey;
            _voteN[at] = 1;
        }

        public bool Due(double now)
        {
            if (double.IsNaN(_nextPublish))
            {
                _nextPublish = now + RhythmMath.PublishSeconds;
                return false;
            }
            return now >= _nextPublish || now < _nextPublish - 10 * RhythmMath.PublishSeconds;
        }

        /// <summary>
        /// Publishes every unit noted this period: (1) its centroid, men, contact men and votes; (2) its enemy unit - the most voted, else a unit that
        /// votes for it (a held wall sees nobody: its attackers name it), kept while live; its direction toward that enemy, its front and its front men's
        /// bar; (3) step 78c's diagnostic - each engaged pair's natural gap from the men, counted by side. Nothing here moves anybody. Allocation-free.
        /// </summary>
        public void Publish(double now)
        {
            _nextPublish = now + RhythmMath.PublishSeconds;
            Publishes++;
            for (int k = 0; k < N; k++)
            {
                if (_n[k] <= 0) continue;
                Men[k] = _n[k];
                ContactMen[k] = _contact[k];
                HeldMen[k] = _held[k];
                CX[k] = _sx[k] / _n[k];
                CY[k] = _sy[k] / _n[k];
                Kind[k] = _kind[k];
                Player[k] = _player[k];
                Valid[k] = true;
                PublishedAt[k] = now;
                if (_contact[k] > 0) ContactSeen[k] = now;
            }
            // (2) the enemy unit: the most voted; else a live unit voting for this one (a held wall sees none of its attackers)
            for (int k = 0; k < N; k++)
            {
                if (_n[k] <= 0) continue;
                int best = -1, bestN = 0;
                for (int i = k * V; i < k * V + V; i++)
                {
                    int c = _voteKey[i];
                    if (c < 0 || _voteN[i] <= bestN || !Live(c, now)) continue;
                    best = c;
                    bestN = _voteN[i];
                }
                if (best < 0 && Live(Enemy[k], now)) best = Enemy[k];
                Enemy[k] = best;
            }
            for (int k = 0; k < N; k++)
            {
                if (_n[k] <= 0 || Enemy[k] >= 0) continue;
                int best = -1, bestN = 0;
                for (int j = 0; j < N; j++)
                {
                    if (j == k || _n[j] <= 0) continue;
                    for (int i = j * V; i < j * V + V; i++)
                        if (_voteKey[i] == k && _voteN[i] > bestN)
                        {
                            best = j;
                            bestN = _voteN[i];
                        }
                }
                Enemy[k] = best;
            }
            // the front toward the enemy and the front men's bar
            for (int k = 0; k < N; k++)
            {
                if (_n[k] <= 0) continue;
                int e = Enemy[k];
                if (e < 0 || !Valid[e])
                {
                    Front[k] = FrontBar[k] = double.NaN;
                    FrontMen[k] = 0;
                    continue;
                }
                double ux = CX[e] - CX[k], uy = CY[e] - CY[k];
                double len = Math.Sqrt(ux * ux + uy * uy);
                // step 78c: the men's own directions to their nearest enemies when they agree (a meshed fight's centroids sit on top of each other)
                double mx = _ex[k], my = _ey[k], ml = Math.Sqrt(mx * mx + my * my);
                if (_en[k] >= 3 && ml >= AxisAgreement * _en[k] && (len <= 1e-6 || (mx * ux + my * uy) / (ml * len) > 0))
                {
                    ux = mx;
                    uy = my;
                    len = ml;
                }
                if (!(len > 1e-6))
                {
                    Front[k] = FrontBar[k] = double.NaN;
                    FrontMen[k] = 0;
                    continue;
                }
                ux /= len;
                uy /= len;
                UX[k] = ux;
                UY[k] = uy;
                var xs = _x[k];
                var ys = _y[k];
                var bs = _bar[k];
                int m = Math.Min(_n[k], M);
                if (xs == null || ys == null || bs == null) continue;
                for (int i = 0; i < m; i++) _alg[i] = xs[i] * ux + ys[i] * uy;
                RhythmMath.FrontOf(_alg, bs, m, FrontDepthMetres, _sorted, out double front, out double bar, out int men);
                Front[k] = front;
                FrontBar[k] = bar;
                FrontMen[k] = men;
            }
            // (3) the natural gap of each engaged pair (each pair once, from its lower key) - a diagnostic
            for (int k = 0; k < N; k++)
            {
                if (_n[k] <= 0) continue;
                Gap[k] = double.NaN;
                int e = Enemy[k];
                if (e < 0 || e >= N || _n[e] <= 0) continue;
                if (Enemy[e] != k && Enemy[e] >= 0) continue; // e fights another unit
                if (e < k && Enemy[e] == k) continue; // the pair is handled from its lower key
                Pairs++;
                double seen = Math.Max(double.IsNaN(ContactSeen[k]) ? double.NegativeInfinity : ContactSeen[k],
                    double.IsNaN(ContactSeen[e]) ? double.NegativeInfinity : ContactSeen[e]);
                if (now - seen > EngagedSeconds) continue;
                if (!MeasurePair(k, e, out _, out _, out double q25, out _, out _, out _, out _)) continue;
                if (q25 >= EngagedGapMetres) continue;
                GapMeasures++;
                Gap[k] = Gap[e] = q25;
                if (GapSink == null) continue;
                GapSink.AddGap(Player[k], q25, GapDiagnosticMetres);
                GapSink.AddGap(Player[e], q25, GapDiagnosticMetres);
            }
            for (int k = 0; k < N; k++)
                if (_n[k] > 0) ClearPeriod(k);
        }

        /// <summary>
        /// Step 78b - THE GAP FROM THE MEN between a pair (A = <paramref name="a"/>, along A's direction toward B): every sampled man of both in the pair's
        /// frame, binned along the contact (<see cref="RhythmMath.MeasureGap"/>). Out: the mean / 10th / 25th / 90th percentile of the bins' gaps, the
        /// contact plane and each side's nearest men's median - world scalars along A's direction. False = no bin with both. Step 78c: the diagnostic only.
        /// </summary>
        public bool MeasurePair(int a, int b, out double mean, out double q10, out double q25, out double q90, out double plane, out double frontA, out double frontB)
        {
            mean = q10 = q25 = q90 = plane = frontA = frontB = double.NaN;
            if (a < 0 || a >= N || b < 0 || b >= N) return false;
            var xa = _x[a];
            var ya = _y[a];
            var xb = _x[b];
            var yb = _y[b];
            if (xa == null || ya == null || xb == null || yb == null) return false;
            double ux = UX[a], uy = UY[a];
            if (double.IsNaN(ux) || double.IsNaN(uy) || (ux == 0 && uy == 0)) return false;
            double mx = 0.5 * (CX[a] + CX[b]), my = 0.5 * (CY[a] + CY[b]);
            int na = Math.Min(_n[a], M), nb = Math.Min(_n[b], M);
            for (int i = 0; i < na; i++)
            {
                double dx = xa[i] - mx, dy = ya[i] - my;
                _ga[i] = dx * ux + dy * uy;
                _gl[i] = -dx * uy + dy * ux;
            }
            for (int i = 0; i < nb; i++)
            {
                double dx = xb[i] - mx, dy = yb[i] - my;
                _hb[i] = dx * ux + dy * uy;
                _hl[i] = -dx * uy + dy * ux;
            }
            int bins = RhythmMath.MeasureGap(_ga, _gl, na, _hb, _hl, nb, _maxA, _minB, _gaps, _mids, _fa, _fb, out mean, out q10, out q25, out q90, out plane,
                out frontA, out frontB);
            if (bins <= 0) return false;
            double o = mx * ux + my * uy;
            plane += o;
            frontA += o;
            frontB += o;
            return true;
        }
    }

    /// <summary>Step 78 per mission: the measurement (ON and OFF), the swaps' counts and the [summary] "battle rhythm" line (the rotation).</summary>
    public sealed class RhythmStats
    {
        /// <summary>Tired front men looked at (not in a swap or its cooldown), by rotation OFF / ON.</summary>
        public readonly long[] Asked = new long[2];

        /// <summary>... with a fresher mate right behind ready to swap (a CHANCE) - OFF they are only counted.</summary>
        public readonly long[] Chances = new long[2];

        public readonly long[] Refused = new long[(int)RotationRefusal.Count];
        public readonly long[] Ends = new long[(int)RotationEnd.Count];
        public long Started, Swapped, SwapFailed, NoSlotSwap, RotatedMen;
        public double TiredBarSum, FreshBarSum, WaitSum, WaitMax, DoneSecondsSum;
        public long WaitN, DoneN;

        /// <summary>Looks at men in contact and their bars (the front's freshness), by rotation OFF / ON.</summary>
        public readonly long[] ContactLooks = new long[2];
        public readonly double[] ContactBarSum = new double[2];

        /// <summary>The hook's frames: a tired man held while his fresh mate came up, walked, clamped; a rested man's place clamped.</summary>
        public long HoldFrames, MoveFrames, ClampFrames, RestClamps;

        // ---- cost
        public long Ticks, Looks, Scans, Steers, Errors;
        public double LookCostMs, SteerCostMs, PublishCostMs;

        public void AddContact(bool on, double bar)
        {
            int i = on ? 1 : 0;
            ContactLooks[i]++;
            ContactBarSum[i] += double.IsNaN(bar) ? 1 : bar;
        }

        public void AddSummaryLines(List<string> lines, in RhythmRules r, RhythmUnits t)
        {
            var sb = new System.Text.StringBuilder(640);
            sb.Append("battle rhythm (step 78) - ROTATION ").Append(r.RotationOn ? "ON" : "OFF (" + r.RotationOffBecause + ")").Append(": tired front men looked at ON ")
              .Append(Asked[1]).Append(" | OFF ").Append(Asked[0]).Append("; chances (a fresher mate right behind) ON ").Append(Chances[1]).Append(" | OFF ").Append(Chances[0])
              .Append("; swaps asked ").Append(Started).Append(", done ").Append(Ends[(int)RotationEnd.Done]).Append(" (slots swapped ").Append(Swapped)
              .Append(", no slots to swap ").Append(NoSlotSwap).Append(", the game refused ").Append(SwapFailed).Append("), ended:");
            for (int i = 0; i < (int)RotationEnd.Count; i++)
                if (Ends[i] > 0) sb.Append(' ').Append(RhythmMath.EndName((RotationEnd)i)).Append(' ').Append(Ends[i]).Append(',');
            sb.Append(" refused:");
            for (int i = 0; i < (int)RotationRefusal.Count; i++)
                if (Refused[i] > 0) sb.Append(' ').Append(RhythmMath.RefusalName((RotationRefusal)i)).Append(' ').Append(Refused[i]).Append(',');
            sb.Append(" the bar at the swap: tired ").Append(Pct(Started > 0 ? TiredBarSum / Started : double.NaN)).Append(" vs fresh ")
              .Append(Pct(Started > 0 ? FreshBarSum / Started : double.NaN)).Append("; the tired man held until the fresh one was beside him: mean ")
              .Append(F2(WaitN > 0 ? WaitSum / WaitN : double.NaN)).Append(" s (max ").Append(F2(WaitMax)).Append(" s), a whole swap ")
              .Append(F2(DoneN > 0 ? DoneSecondsSum / DoneN : double.NaN)).Append(" s; men rotated ").Append(RotatedMen)
              .Append("; the bar of men IN CONTACT ON ").Append(Pct(ContactLooks[1] > 0 ? ContactBarSum[1] / ContactLooks[1] : double.NaN)).Append(" (").Append(ContactLooks[1])
              .Append(" looks) | OFF ").Append(Pct(ContactLooks[0] > 0 ? ContactBarSum[0] / ContactLooks[0] : double.NaN)).Append(" (").Append(ContactLooks[0])
              .Append(" looks); input frames: held ").Append(HoldFrames).Append(", walked ").Append(MoveFrames).Append(", clamped ").Append(ClampFrames)
              .Append("; cost: looks ").Append(F2(LookCostMs)).Append(" ms (").Append(Looks).Append(", scans ").Append(Scans).Append("), steering ").Append(F2(SteerCostMs))
              .Append(" ms (").Append(Steers).Append("), the unit table ").Append(F2(PublishCostMs)).Append(" ms over ").Append(t.Publishes).Append(" publishes; errors ").Append(Errors);
            lines.Add(sb.ToString());
        }

        public static string Pct(double v) => double.IsNaN(v) ? "-" : (100 * v).ToString("0", CultureInfo.InvariantCulture) + "%";

        public static string F1(double v) => double.IsNaN(v) ? "-" : v.ToString("0.0", CultureInfo.InvariantCulture);

        public static string F2(double v) => double.IsNaN(v) ? "-" : v.ToString("0.00", CultureInfo.InvariantCulture);
    }
}

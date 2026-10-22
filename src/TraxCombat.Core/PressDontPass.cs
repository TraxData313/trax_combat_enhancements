using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 77 - PRESS, DON'T PASS (Toni's battles 2026-10-22 10:2x-10:48: 108 T1 recruits against his 59 T5 shield wall - at contact the AI's
    /// tactical charge, the rear ranks walking round his wall's ends and behind it, "dancing", then through the middle; AI_NOTES "Step 77", DESIGN
    /// decision 71). The settings, read live: the master switch FIRST, then <c>PressDontPass</c>, then the numbers.
    /// </summary>
    public readonly struct PressRules
    {
        public PressRules(bool modEnabled, bool enabled, double toleranceMetres = 0.5, double watchMetres = 4, double gapMetres = 2, double manWidthMetres = 1.2,
            double frontDepthMetres = 1.5, double wrapMarginMetres = 6, int wrapMates = 5, double wrapMatesMetres = 4, double roundEndMetres = 2, double winPercent = 50,
            int lineMinMen = 8, double lineInSlotPercent = 60, double bandMarginMetres = 1, double blockEndMetres = 0, double endSlackMetres = 1)
        {
            ModEnabled = modEnabled;
            Enabled = enabled;
            ToleranceMetres = Clamp(toleranceMetres, 0, 3);
            WatchMetres = Clamp(watchMetres, 0.5, 20);
            GapMetres = Clamp(gapMetres, 0.5, 10);
            ManWidthMetres = Clamp(manWidthMetres, 0.4, 3);
            FrontDepthMetres = Clamp(frontDepthMetres, 0.5, 5);
            WrapMarginMetres = Clamp(wrapMarginMetres, 0, 60);
            WrapMates = wrapMates < 0 ? 0 : wrapMates > 30 ? 30 : wrapMates;
            WrapMatesMetres = Clamp(wrapMatesMetres, 1, 8);
            RoundEndMetres = Clamp(roundEndMetres, 0.5, 10);
            WinPercent = Clamp(winPercent, 0, 100);
            LineMinMen = lineMinMen < 3 ? 3 : lineMinMen > 200 ? 200 : lineMinMen;
            LineInSlotPercent = Clamp(lineInSlotPercent, 0, 100);
            BandMarginMetres = Clamp(bandMarginMetres, 0, 10);
            BlockEndMetres = Clamp(blockEndMetres, 0, 10);
            EndSlackMetres = Clamp(endSlackMetres, 0, 10);
        }

        public static PressRules From(TraxSettings s) =>
            new PressRules(s.ModEnabled, s.PressDontPass, s.PressToleranceMetres, s.PressWatchMetres, s.PressGapMetres, s.PressManWidthMetres, s.PressFrontDepthMetres,
                s.PressWrapMarginMetres, s.PressWrapMates, s.PressWrapMatesMetres, s.PressRoundEndMetres, s.PressWinPercent, s.PressLineMinMen, s.PressLineInSlotPercent,
                s.PressBandMarginMetres, s.PressBlockEndMetres, s.PressEndSlackMetres);

        public bool ModEnabled { get; }

        /// <summary>PressDontPass - the system's switch (the looks measure either way).</summary>
        public bool Enabled { get; }

        /// <summary>PressToleranceMetres: how far past an enemy line's front a man may be carried before he is walked back out.</summary>
        public double ToleranceMetres { get; }

        /// <summary>PressWatchMetres: within this of an enemy line's front a man's movement is clamped to his side of it.</summary>
        public double WatchMetres { get; }

        /// <summary>PressGapMetres: the narrowest empty stretch of a line's front a man may go THROUGH.</summary>
        public double GapMetres { get; }

        /// <summary>PressManWidthMetres: the stretch of the front one front-rank man covers (a gap is what no man covers).</summary>
        public double ManWidthMetres { get; }

        /// <summary>PressFrontDepthMetres: a man within this behind a line's front holds it (a rear man stepping up fills a fallen man's place);
        /// also a man more than this behind his own unit's front is in its rear ranks.</summary>
        public double FrontDepthMetres { get; }

        /// <summary>PressWrapMarginMetres: his unit must be this much wider than the enemy line to go round its ends.</summary>
        public double WrapMarginMetres { get; }

        /// <summary>PressWrapMates: ... and he must have at least this many mates within <see cref="WrapMatesMetres"/> (he moves as part of the mass).</summary>
        public int WrapMates { get; }

        public double WrapMatesMetres { get; }

        /// <summary>PressRoundEndMetres: a man behind a line is walked round its nearest end this far beyond it.</summary>
        public double RoundEndMetres { get; }

        /// <summary>PressWinPercent: his unit winning this much (step 69's win) = the line is beaten - the press lets go.</summary>
        public double WinPercent { get; }

        /// <summary>PressLineMinMen: an enemy formation needs this many men looked at to be a line.</summary>
        public int LineMinMen { get; }

        /// <summary>PressLineInSlotPercent: ... and this share of them within 3 m of their slots (not held by step 41 or a block).</summary>
        public double LineInSlotPercent { get; }

        /// <summary>PressBandMarginMetres: the fear band is never wider than the enemy line it faces + this on each side.</summary>
        public double BandMarginMetres { get; }

        /// <summary>PressBlockEndMetres: in contact the block hold's sideways allowance ends this far beyond the enemy line's ends.</summary>
        public double BlockEndMetres { get; }

        /// <summary>PressEndSlackMetres: a rear-rank man may drift this far sideways past the enemy line's end, no farther.</summary>
        public double EndSlackMetres { get; }

        /// <summary>The master switch FIRST, then the system's own.</summary>
        public bool On => ModEnabled && Enabled;

        public string? OffBecause => !ModEnabled ? "ModEnabled" : !Enabled ? "PressDontPass" : null;

        public string Describe()
        {
            string what = "press, don't pass (step 77): an AI man on foot fighting an enemy LINE (a formation of " + LineMinMen + "+ men held by step 41, a held block, or "
                          + F(LineInSlotPercent) + "%+ in their slots under a hold / move / advance) never walks past its front - within " + F(WatchMetres)
                          + " m of it his movement toward it stops at it (+ " + F(ToleranceMetres) + " m), past it he is walked back out, behind it round its nearest end ("
                          + F(RoundEndMetres) + " m beyond it); THROUGH only at a gap of " + F(GapMetres) + " m+ in its front (a front man covers " + F(ManWidthMetres)
                          + " m, the front " + F(FrontDepthMetres) + " m deep); ROUND an end only when his unit is " + F(WrapMarginMetres) + " m+ wider than the line and he has "
                          + WrapMates + "+ mates within " + F(WrapMatesMetres) + " m; rear-rank men no more than " + F(EndSlackMetres) + " m past its ends; his unit winning "
                          + F(WinPercent) + "% lets go; the fear band never wider than the line + " + F(BandMarginMetres) + " m a side (its surplus deeper); the block hold's "
                          + "sideways room within the line's ends + " + F(BlockEndMetres) + " m in contact; both sides, never you or riders; his attacks his own";
            if (!On) return what + " - OFF (" + OffBecause + "): measured only";
            return what + " - ON";
        }

        private static double Clamp(double v, double lo, double hi) => double.IsNaN(v) ? lo : v < lo ? lo : v > hi ? hi : v;

        private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>Step 77: what made a formation an enemy LINE (its men mostly hold slots).</summary>
    public enum PressLineKind
    {
        /// <summary>Not a line (a charge's crowd, a retreat, too few men, its men out of their slots).</summary>
        None,

        /// <summary>Its men mostly within 3 m of their slots under a hold / move / advance.</summary>
        Slots,

        /// <summary>Its men held by step 41 (a shield wall / square / circle - standing firm too).</summary>
        Wall,

        /// <summary>A block step 74 holds (a low-discipline unit kept a block, even in a charge).</summary>
        Block,

        /// <summary>A square or circle: its front is the whole ring (no ends, no going round).</summary>
        Ring,

        Count,
    }

    /// <summary>Step 77: what one look decided for a man near an enemy line.</summary>
    public enum PressAction
    {
        /// <summary>No enemy line for him.</summary>
        NoLine,

        /// <summary>On his side, farther than the watch distance - his own AI.</summary>
        Far,

        /// <summary>On his side within the watch distance: his movement toward the line clamped at its front (a rear-rank man's sideways too).</summary>
        Clamp,

        /// <summary>Past the front, in the line's body or beside its end: walked straight back out to the front.</summary>
        Push,

        /// <summary>Behind the whole line: walked round its nearest end to his side.</summary>
        Return,

        /// <summary>In a real gap of the front: free to go through.</summary>
        Through,

        /// <summary>Beside the line's end with his mass, his unit genuinely wider: free to go round.</summary>
        Wrap,

        /// <summary>Behind the line, but it is broken (a gap) - left alone.</summary>
        Broken,

        /// <summary>His unit is winning (the line is beaten) - left alone.</summary>
        Winning,

        Count,
    }

    /// <summary>Step 77: what carried a man across an enemy line's front (the diagnostic's columns, in priority order).</summary>
    public enum PressCause
    {
        /// <summary>Courage's walk back (step 49) steered him.</summary>
        Courage,

        /// <summary>The fear flock (step 67) steered him.</summary>
        Fear,

        /// <summary>The block hold (step 74) walked him.</summary>
        Block,

        /// <summary>His AI's target stood past the front (the game's charge sends each man at his own target).</summary>
        ChargeTarget,

        /// <summary>None of ours, no target behind - the game itself.</summary>
        Game,

        Count,
    }

    /// <summary>Step 77: why a press wish ended.</summary>
    public enum PressEnd
    {
        Free,
        NoLine,
        SwitchedOff,
        Excluded,
        LeftField,
        MissionEnd,
        Error,
        Count,
    }

    /// <summary>Step 77: one look's verdict for a man - the action, the clamps and the place a walk aims at (in the line's face frame).</summary>
    public struct PressOrder
    {
        public PressAction Action;

        /// <summary>Clamp the component of his movement toward the line.</summary>
        public bool ClampFront;

        /// <summary>A rear-rank man past the line's end: +1 = his movement outward past the high end clamped, −1 the low end, 0 none.</summary>
        public int Side;

        /// <summary>A walk's place: d in front of the face (positive = his side), l along it.</summary>
        public double ToD, ToL;
    }

    /// <summary>
    /// Step 77's pure rule. An enemy LINE's front is a SEGMENT: in the line's frame (u = its facing, e across it), its front = the median of each
    /// lateral bin's front-most man (bins one man wide), its ends = its front men's extreme laterals ± half a man, its gaps = the stretches no front
    /// man covers (front men = within the front depth behind the front), its depth from the front to its rear; its REAR is a second face the same
    /// way (an attack from behind presses on the rear rank). A man's d = how far he stands in front of the face (his side positive), l = along it.
    /// </summary>
    public static class PressMath
    {
        /// <summary>The most gaps a face keeps (more are counted, not kept).</summary>
        public const int MaxGaps = 8;

        /// <summary>The most men a unit's period keeps (more are measured but not sampled).</summary>
        public const int MaxSamples = 512;

        /// <summary>The table's period (s) - courage's look.</summary>
        public const double PublishSeconds = 0.5;

        /// <summary>A published line stays good this long (s).</summary>
        public const double StaleSeconds = 2.0;

        /// <summary>The votes a unit keeps for the enemy line its men see.</summary>
        public const int Votes = 4;

        /// <summary>The inward clamp bites within this of the front at the 0.1 s steer (a 6 m/s run covers it between two steers - plumbing).</summary>
        public const double LookaheadMetres = 0.6;

        /// <summary>A man more than this past a front has CROSSED it (the diagnostic - a jostle at the front is not a crossing).</summary>
        public const double CrossedMetres = 1.0;

        /// <summary>Past the line's rear by this = behind the whole line (walked round an end, not pushed through).</summary>
        public const double BehindExtraMetres = 1.0;

        /// <summary>A pushed man's place: this far in front of the face.</summary>
        public const double PushToMetres = 0.3;

        /// <summary>A man walked round an end aims this far in front of the face once beyond the end.</summary>
        public const double ReturnFrontMetres = 1.0;

        /// <summary>A face is at least this deep (a single rank).</summary>
        public const double MinDepth = 0.5;

        /// <summary>His unit switches face only when its centre is this much beyond the line's middle past half its depth (no flicker).</summary>
        public const double FaceHysteresisMetres = 1.0;

        /// <summary>A ring's front = this quantile of its men's distance from its centre.</summary>
        public const double RingQuantile = 0.9;

        /// <summary>A unit's width from its men across the line: (q95 − q05) ÷ 0.9 (a uniform spread's full extent).</summary>
        public const double WidthQuantile = 0.05;

        /// <summary>The block hold's range against a line is at least this wide (m).</summary>
        public const double MinBlockRange = 2.0;

        /// <summary>The share of a unit's looked-at men held by step 41 that makes it a wall.</summary>
        public const double HeldShare = 0.5;

        /// <summary>A man within this of his slot holds it (the step-74 diagnostic's out-of-slot distance).</summary>
        public const double InSlotMetres = BlockHoldMath.OutOfSlotMetres;

        /// <summary>
        /// The line test (in this order): too few men = none; a retreat = none; mostly held by step 41 = a wall; a held block = a block; under no
        /// charge with <see cref="PressRules.LineInSlotPercent"/> of the men read within 3 m of their slots (half the men read at least) = slots; a
        /// square / circle of any of those = a ring.
        /// </summary>
        public static PressLineKind LineKind(in PressRules r, int men, int held, int slotReads, int inSlot, FearOrder order, bool blockHeld, bool ring)
        {
            if (men < r.LineMinMen || order == FearOrder.Retreat) return PressLineKind.None;
            var k = PressLineKind.None;
            if (held >= HeldShare * men) k = PressLineKind.Wall;
            else if (blockHeld) k = PressLineKind.Block;
            else if (order != FearOrder.Charge && slotReads >= 0.5 * men && slotReads > 0 && inSlot >= r.LineInSlotPercent / 100.0 * slotReads) k = PressLineKind.Slots;
            if (k != PressLineKind.None && ring) k = PressLineKind.Ring;
            return k;
        }

        /// <summary>
        /// ONE FACE of a line from its men: <paramref name="lat"/> SORTED ascending with <paramref name="alg"/> aligned (along the face's outward
        /// direction). The front = the median of the front-most man of each lateral bin (bins one man wide, each opened at a man); the front men = within
        /// <paramref name="frontDepth"/> behind it; the ends = the extreme front men ± half a man; the GAPS = the stretches between neighbouring front men
        /// no man covers, <paramref name="gapMetres"/> or wider (kept up to <see cref="MaxGaps"/> from <paramref name="gapOffset"/>). Returns the gaps kept.
        /// </summary>
        public static int FrontOf(double[] lat, double[] alg, int n, double manWidth, double frontDepth, double gapMetres, double[] bins,
            double[] gapLo, double[] gapHi, int gapOffset, out double front, out double lo, out double hi, out int frontMen, out int gapsCut)
        {
            front = lo = hi = double.NaN;
            frontMen = gapsCut = 0;
            if (n <= 0) return 0;
            double w = manWidth > 1e-3 ? manWidth : 1;
            int m = 0;
            double start = lat[0], best = alg[0];
            for (int i = 1; i < n; i++)
            {
                if (lat[i] >= start + w)
                {
                    if (m < bins.Length) bins[m++] = best;
                    start = lat[i];
                    best = alg[i];
                }
                else if (alg[i] > best) best = alg[i];
            }
            if (m < bins.Length) bins[m++] = best;
            Array.Sort(bins, 0, m);
            front = m % 2 == 1 ? bins[m / 2] : 0.5 * (bins[m / 2 - 1] + bins[m / 2]);
            double cut = front - Math.Max(0, frontDepth);
            int gaps = 0;
            double prev = double.NaN;
            for (int i = 0; i < n; i++)
            {
                if (alg[i] < cut) continue;
                frontMen++;
                if (double.IsNaN(lo)) lo = lat[i] - w / 2;
                hi = lat[i] + w / 2;
                if (!double.IsNaN(prev))
                {
                    double g = lat[i] - prev - w;
                    if (g >= gapMetres - 1e-9)
                    {
                        if (gaps < MaxGaps)
                        {
                            gapLo[gapOffset + gaps] = prev + w / 2;
                            gapHi[gapOffset + gaps] = lat[i] - w / 2;
                            gaps++;
                        }
                        else gapsCut++;
                    }
                }
                prev = lat[i];
            }
            return gaps;
        }

        /// <summary>His lateral place lies in one of the face's gaps.</summary>
        public static bool InGap(double l, double[] gapLo, double[] gapHi, int offset, int count)
        {
            for (int i = 0; i < count; i++)
                if (l >= gapLo[offset + i] && l <= gapHi[offset + i]) return true;
            return false;
        }

        /// <summary>His unit may go ROUND the line: its men span at least the line's width + the margin.</summary>
        public static bool UnitWraps(in PressRules r, double unitWidth, double lineWidth) =>
            !double.IsNaN(unitWidth) && !double.IsNaN(lineWidth) && unitWidth - lineWidth >= r.WrapMarginMetres;

        /// <summary>He goes round with his mass: his unit wraps and he has enough mates near.</summary>
        public static bool ManWraps(in PressRules r, bool unitWraps, int mates) => unitWraps && mates >= r.WrapMates;

        /// <summary>
        /// One look at a man of a unit fighting a line (its face: <paramref name="lo"/>..<paramref name="hi"/> across, <paramref name="depth"/> deep):
        /// <paramref name="d"/> in front of the face (his side positive), <paramref name="l"/> along it. In order: his unit WINNING lets go; on his side
        /// - in a gap = through, beside an end with his wrapping mass = wrap, within the watch = clamp (a rear-rank man of a unit in contact past the end
        /// slack: his sideways movement outward clamped too), else far; past the front (beyond the tolerance) - in a gap = through, beside an end with
        /// his mass = wrap, within the line's body (its depth + 1 m) = pushed back out to the front at his lateral, behind the whole line - a broken
        /// line (a gap) = left, else walked ROUND the nearest end (sideways beyond it first, behind the line; then forward).
        /// </summary>
        public static PressOrder Decide(in PressRules r, double d, double l, double lo, double hi, double depth, bool inGap, bool unitWraps, int mates,
            bool broken, bool winning, bool rearRank, bool unitNear)
        {
            var o = new PressOrder { Action = PressAction.Far, ToD = d, ToL = l };
            if (winning)
            {
                o.Action = PressAction.Winning;
                return o;
            }
            bool beside = l < lo || l > hi;
            bool wrap = ManWraps(in r, unitWraps, mates);
            if (d >= -r.ToleranceMetres)
            {
                if (inGap)
                {
                    o.Action = PressAction.Through;
                    return o;
                }
                if (beside && wrap)
                {
                    o.Action = PressAction.Wrap;
                    return o;
                }
                o.ClampFront = d <= r.WatchMetres;
                if (rearRank && unitNear && !wrap)
                {
                    if (l > hi + r.EndSlackMetres) o.Side = 1;
                    else if (l < lo - r.EndSlackMetres) o.Side = -1;
                }
                o.Action = o.ClampFront || o.Side != 0 ? PressAction.Clamp : PressAction.Far;
                return o;
            }
            if (inGap)
            {
                o.Action = PressAction.Through;
                return o;
            }
            if (beside && wrap)
            {
                o.Action = PressAction.Wrap;
                return o;
            }
            double body = Math.Max(MinDepth, depth) + BehindExtraMetres;
            if (d >= -body)
            {
                o.Action = PressAction.Push;
                o.ToD = PushToMetres;
                o.ToL = l;
                return o;
            }
            if (broken || wrap)
            {
                o.Action = broken ? PressAction.Broken : PressAction.Wrap;
                return o;
            }
            o.Action = PressAction.Return;
            ReturnPlace(in r, d, l, lo, hi, depth, out o.ToD, out o.ToL);
            return o;
        }

        /// <summary>The place a man BEHIND a line walks to: still behind it (at least its depth + 1 m back), sideways beyond the nearer end by
        /// PressRoundEndMetres; once beyond it, forward to <see cref="ReturnFrontMetres"/> in front of the face.</summary>
        public static void ReturnPlace(in PressRules r, double d, double l, double lo, double hi, double depth, out double toD, out double toL)
        {
            double outLo = lo - r.RoundEndMetres, outHi = hi + r.RoundEndMetres;
            if (l <= outLo || l >= outHi)
            {
                toD = ReturnFrontMetres;
                toL = l;
                return;
            }
            double behind = -(Math.Max(MinDepth, depth) + BehindExtraMetres);
            toD = Math.Min(d, behind);
            toL = (l - lo) <= (hi - l) ? outLo - 0.5 : outHi + 0.5;
        }

        /// <summary>A RING (square / circle): <paramref name="d"/> = his distance from its centre less its radius. Inside beyond the tolerance =
        /// pushed back out (radially); within the watch = clamped; no ends, no going round, no gaps.</summary>
        public static PressOrder DecideRing(in PressRules r, double d, bool winning)
        {
            var o = new PressOrder { Action = PressAction.Far, ToD = d };
            if (winning)
            {
                o.Action = PressAction.Winning;
                return o;
            }
            if (d < -r.ToleranceMetres)
            {
                o.Action = PressAction.Push;
                o.ToD = PushToMetres;
                return o;
            }
            o.ClampFront = d <= r.WatchMetres;
            o.Action = o.ClampFront ? PressAction.Clamp : PressAction.Far;
            return o;
        }

        /// <summary>The input clamp (his own frame): the component of (vx, vy) along the unit vector (nx, ny) taken out when it points that way.
        /// True when something was taken out.</summary>
        public static bool ClampVector(ref float vx, ref float vy, float nx, float ny)
        {
            float a = vx * nx + vy * ny;
            if (!(a > 1e-4f)) return false;
            vx -= a * nx;
            vy -= a * ny;
            return true;
        }

        /// <summary>The fear band's width against the line it faces: at most its frontage + the margin on each side (frontage unknown = as it is).</summary>
        public static double BandWidth(in PressRules r, double width, double frontage)
        {
            if (double.IsNaN(frontage) || !(frontage > 0)) return width;
            double cap = frontage + 2 * r.BandMarginMetres;
            return width > cap ? cap : width;
        }

        /// <summary>The band's ranks once its width is clamped: enough for all its men in its files (its surplus DEEPER, beyond the ranks' cap).</summary>
        public static int BandRanks(int men, int files, int ranks)
        {
            int need = men <= 0 ? 1 : (int)Math.Ceiling(men / (double)Math.Max(1, files) - 1e-9);
            return need > ranks ? need : ranks;
        }

        /// <summary>
        /// The block hold's sideways range in contact (its frame): the symmetric allowance ±<paramref name="side"/> cut to the enemy line's ends
        /// (<paramref name="endA"/>, <paramref name="endB"/> - its two front ends across the block's frame) + PressBlockEndMetres; at least
        /// <see cref="MinBlockRange"/> wide (centred on the line's middle, inside the allowance). True when it is narrower than the allowance.
        /// </summary>
        public static bool BlockRange(in PressRules r, double side, double endA, double endB, out double lo, out double hi)
        {
            lo = -side;
            hi = side;
            if (double.IsNaN(endA) || double.IsNaN(endB)) return false;
            double a = Math.Min(endA, endB) - r.BlockEndMetres, b = Math.Max(endA, endB) + r.BlockEndMetres;
            double nlo = Math.Max(-side, a), nhi = Math.Min(side, b);
            if (nhi - nlo < MinBlockRange)
            {
                double mid = 0.5 * (a + b);
                if (mid < -side + MinBlockRange / 2) mid = -side + MinBlockRange / 2;
                if (mid > side - MinBlockRange / 2) mid = side - MinBlockRange / 2;
                nlo = mid - MinBlockRange / 2;
                nhi = mid + MinBlockRange / 2;
            }
            bool narrower = nlo > lo + 1e-6 || nhi < hi - 1e-6;
            lo = nlo;
            hi = nhi;
            return narrower;
        }

        /// <summary>What carried him across (in priority order): courage's walk, the fear flock, the block hold, a target past the front, the game.</summary>
        public static PressCause CauseOf(bool courageWalking, bool fear, bool block, bool targetPast) =>
            courageWalking ? PressCause.Courage : fear ? PressCause.Fear : block ? PressCause.Block : targetPast ? PressCause.ChargeTarget : PressCause.Game;

        /// <summary>The q-quantile of the first n SORTED values (step 74's).</summary>
        public static double Quantile(double[] sorted, int n, double q) => BlockHoldMath.Quantile(sorted, n, q);

        public static string KindName(PressLineKind k) => k switch
        {
            PressLineKind.Slots => "holding its slots",
            PressLineKind.Wall => "held by step 41",
            PressLineKind.Block => "a held block",
            PressLineKind.Ring => "a square / circle",
            _ => "not a line",
        };

        public static string ActionName(PressAction a) => a switch
        {
            PressAction.NoLine => "no enemy line",
            PressAction.Far => "far",
            PressAction.Clamp => "pressing (clamped)",
            PressAction.Push => "past the front - pushed back out",
            PressAction.Return => "behind the line - walked round its end",
            PressAction.Through => "through a gap",
            PressAction.Wrap => "round the end with his mass",
            PressAction.Broken => "behind a broken line - left",
            PressAction.Winning => "his side winning - free",
            _ => a.ToString(),
        };

        public static string CauseName(PressCause c) => c switch
        {
            PressCause.Courage => "courage",
            PressCause.Fear => "fear",
            PressCause.Block => "block",
            PressCause.ChargeTarget => "charge target",
            PressCause.Game => "the game",
            _ => c.ToString(),
        };

        public static string EndName(PressEnd e) => e switch
        {
            PressEnd.Free => "free again (far, a gap, a wrap, winning)",
            PressEnd.NoLine => "no enemy line",
            PressEnd.SwitchedOff => "switched off",
            PressEnd.Excluded => "no longer looked at",
            PressEnd.LeftField => "left the field",
            PressEnd.MissionEnd => "mission end",
            _ => "error",
        };
    }

    /// <summary>Step 77: one man's press state (the module's TrackedAgent carries one).</summary>
    public sealed class PressMind
    {
        /// <summary>His input wish is on (clamped, pushed or walked).</summary>
        public bool Wish;

        public PressAction Action;

        /// <summary>The line he presses (a unit key), its face (0 front, 1 rear) and a ring.</summary>
        public int Line = -1, Face;
        public bool Ring;

        /// <summary>The look's verdict: clamp the front, a rear-rank man's side clamp, the walk's place in the face frame.</summary>
        public bool ClampFront;
        public int Side;
        public double ToD, ToL;

        public double Since = double.NaN, NextSteer, NextEdge;
        public bool Moving;

        /// <summary>The crossing watch (ON and OFF): the line / face of the last look and whether he stood on his side then.</summary>
        public int SeenLine = -1, SeenFace;
        public bool WasFront;

        /// <summary>The hook's frame counters when the wish began (the stats add the difference at its end).</summary>
        public long ClampAtStart, SideAtStart, MoveAtStart;

        public bool EverPressed, EverPushed, EverReturned;

        public void End()
        {
            Wish = false;
            Moving = false;
            ClampFront = false;
            Side = 0;
            Action = PressAction.NoLine;
            Since = double.NaN;
        }
    }

    /// <summary>
    /// Step 77's LINE TABLE (team x formation, FearMath.UnitKey): every look notes the man (where he stands, his slot distance, held by step 41 or
    /// not, his unit's facing and order) and votes for the enemy unit his nearest enemy belongs to; every <see cref="PressMath.PublishSeconds"/> each
    /// unit is tested as a LINE (<see cref="PressMath.LineKind"/>) and its two faces published (front, ends, gaps; a ring's centre and radius); then
    /// each unit's ENEMY LINE (the line its men see most) and its own width / front across that line's face. Arrays allocated once per unit and battle.
    /// </summary>
    public sealed class PressLines
    {
        private const int N = FearMath.UnitCount;
        private const int G = PressMath.MaxGaps;
        private const int V = PressMath.Votes;

        // this period
        private readonly int[] _n = new int[N], _held = new int[N], _slotReads = new int[N], _inSlot = new int[N];
        private readonly double[]?[] _x = new double[N][], _y = new double[N][];
        private readonly double[] _fx = new double[N], _fy = new double[N];
        private readonly FearOrder[] _order = new FearOrder[N];
        private readonly int[] _voteKey = new int[N * V], _voteN = new int[N * V];

        // scratch (one unit at a time)
        private readonly double[] _lat = new double[PressMath.MaxSamples], _alg = new double[PressMath.MaxSamples], _bins = new double[PressMath.MaxSamples];

        // published - the lines
        public readonly bool[] Valid = new bool[N];
        public readonly PressLineKind[] Kind = new PressLineKind[N];
        public readonly double[] PublishedAt = new double[N];
        public readonly int[] Men = new int[N];
        public readonly double[] UX = new double[N], UY = new double[N], Depth = new double[N];

        /// <summary>Per face (unit x 2 + face): the front (along the face's outward direction), the ends across, the front men, the gaps.</summary>
        public readonly double[] Front = new double[N * 2], Lo = new double[N * 2], Hi = new double[N * 2];
        public readonly int[] FrontMen = new int[N * 2], GapN = new int[N * 2];
        public readonly double[] GapLo = new double[N * 2 * G], GapHi = new double[N * 2 * G];

        /// <summary>A ring's centre and radius.</summary>
        public readonly double[] RingX = new double[N], RingY = new double[N], RingR = new double[N];

        // published - the units fighting a line
        public readonly int[] EnemyLine = new int[N], Face = new int[N];
        public readonly double[] UnitWidth = new double[N], UnitFront = new double[N], AttackAt = new double[N];
        public readonly bool[] Wraps = new bool[N];

        // the diagnostic's counts
        public long Publishes, LinesPublished, FacesWithGaps, GapsFound, GapsCut, WrapUnits;
        public readonly long[] Kinds = new long[(int)PressLineKind.Count];
        public double WidestGap, WidthSum;
        public long WidthN;

        private double _nextPublish = double.NaN;

        public PressLines()
        {
            Reset();
        }

        public void Reset()
        {
            for (int k = 0; k < N; k++)
            {
                ClearPeriod(k);
                Valid[k] = Wraps[k] = false;
                Kind[k] = PressLineKind.None;
                PublishedAt[k] = AttackAt[k] = double.NaN;
                EnemyLine[k] = -1;
                Face[k] = 0;
                Men[k] = 0;
                UnitWidth[k] = UnitFront[k] = double.NaN;
                _fx[k] = _fy[k] = 0;
            }
            _nextPublish = double.NaN;
            Publishes = LinesPublished = FacesWithGaps = GapsFound = GapsCut = WrapUnits = WidthN = 0;
            WidestGap = WidthSum = 0;
            Array.Clear(Kinds, 0, Kinds.Length);
        }

        private void ClearPeriod(int k)
        {
            _n[k] = _held[k] = _slotReads[k] = _inSlot[k] = 0;
            _order[k] = FearOrder.Hold;
            for (int i = 0; i < V; i++)
            {
                _voteKey[k * V + i] = -1;
                _voteN[k * V + i] = 0;
            }
        }

        /// <summary>The unit is a LINE now (published within <see cref="PressMath.StaleSeconds"/>).</summary>
        public bool IsLine(int key, double now) =>
            key >= 0 && key < N && Valid[key] && Kind[key] != PressLineKind.None && !double.IsNaN(PublishedAt[key])
            && now - PublishedAt[key] <= PressMath.StaleSeconds && now >= PublishedAt[key] - 1e-9;

        /// <summary>The unit's enemy line now (−1 = none).</summary>
        public int LineOf(int key, double now)
        {
            if (key < 0 || key >= N || double.IsNaN(AttackAt[key]) || now - AttackAt[key] > PressMath.StaleSeconds) return -1;
            int l = EnemyLine[key];
            return IsLine(l, now) ? l : -1;
        }

        /// <summary>One man of the unit at this look: where he stands, the unit's facing and order, his distance from his slot (NaN = no slot), held by step 41.</summary>
        public void Note(int key, double px, double py, double fx, double fy, FearOrder order, double slotDistance, bool held)
        {
            if (key < 0 || key >= N || double.IsNaN(px) || double.IsNaN(py)) return;
            if (fx * fx + fy * fy > 1e-6)
            {
                _fx[key] = fx;
                _fy[key] = fy;
            }
            _order[key] = order;
            if (held) _held[key]++;
            if (!double.IsNaN(slotDistance))
            {
                _slotReads[key]++;
                if (slotDistance <= PressMath.InSlotMetres) _inSlot[key]++;
            }
            int s = _n[key];
            _n[key] = s + 1;
            if (s >= PressMath.MaxSamples) return;
            var xs = _x[key];
            if (xs == null)
            {
                xs = _x[key] = new double[PressMath.MaxSamples]; // once per unit and battle
                _y[key] = new double[PressMath.MaxSamples];
            }
            xs[s] = px;
            _y[key]![s] = py;
        }

        /// <summary>A man of the unit saw an enemy of <paramref name="enemyKey"/> nearest (courage's scan).</summary>
        public void Vote(int key, int enemyKey)
        {
            if (key < 0 || key >= N || enemyKey < 0 || enemyKey >= N || enemyKey == key) return;
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
                _nextPublish = now + PressMath.PublishSeconds;
                return false;
            }
            return now >= _nextPublish || now < _nextPublish - 10 * PressMath.PublishSeconds;
        }

        /// <summary>
        /// Publishes every unit noted this period: (1) the LINE test and, for a line, its two faces (or its ring); (2) for every unit whose men saw an
        /// enemy line, that line, its face (hysteresis), the unit's width across it ((q95 − q05) ÷ 0.9) and its front (q10 of its men's d), wrapping or
        /// not. <paramref name="blockHeld"/> / <paramref name="ring"/> per unit (the module's). Allocation-free.
        /// </summary>
        public void Publish(double now, in PressRules r, bool[] blockHeld, bool[] ring)
        {
            _nextPublish = now + PressMath.PublishSeconds;
            Publishes++;
            for (int k = 0; k < N; k++)
            {
                int n = _n[k];
                if (n <= 0) continue;
                PublishLine(k, now, in r, blockHeld[k], ring[k]);
            }
            for (int k = 0; k < N; k++)
            {
                if (_n[k] <= 0) continue;
                PublishAttacker(k, now, in r);
            }
            for (int k = 0; k < N; k++)
                if (_n[k] > 0) ClearPeriod(k);
        }

        private void PublishLine(int k, double now, in PressRules r, bool blockHeld, bool ring)
        {
            int n = _n[k];
            var xs = _x[k];
            var ys = _y[k];
            int m = Math.Min(n, PressMath.MaxSamples);
            Men[k] = n;
            var kind = PressMath.LineKind(in r, n, _held[k], _slotReads[k], _inSlot[k], _order[k], blockHeld, ring);
            double ux = _fx[k], uy = _fy[k], len = Math.Sqrt(ux * ux + uy * uy);
            if (xs == null || ys == null || kind == PressLineKind.None || !(len > 1e-6))
            {
                Valid[k] = true;
                Kind[k] = PressLineKind.None;
                PublishedAt[k] = now;
                return;
            }
            ux /= len;
            uy /= len;
            double ex = -uy, ey = ux;
            UX[k] = ux;
            UY[k] = uy;
            if (kind == PressLineKind.Ring)
            {
                double cx = 0, cy = 0;
                for (int i = 0; i < m; i++)
                {
                    cx += xs[i];
                    cy += ys[i];
                }
                cx /= m;
                cy /= m;
                for (int i = 0; i < m; i++) _lat[i] = Math.Sqrt((xs[i] - cx) * (xs[i] - cx) + (ys[i] - cy) * (ys[i] - cy));
                Array.Sort(_lat, 0, m);
                RingX[k] = cx;
                RingY[k] = cy;
                RingR[k] = PressMath.Quantile(_lat, m, PressMath.RingQuantile);
                Depth[k] = Math.Max(PressMath.MinDepth, RingR[k]);
                GapN[2 * k] = GapN[2 * k + 1] = 0;
            }
            else
            {
                for (int i = 0; i < m; i++)
                {
                    _lat[i] = xs[i] * ex + ys[i] * ey;
                    _alg[i] = xs[i] * ux + ys[i] * uy;
                }
                Array.Sort(_lat, _alg, 0, m);
                for (int f = 0; f < 2; f++)
                {
                    if (f == 1)
                        for (int i = 0; i < m; i++) _alg[i] = -_alg[i];
                    int at = 2 * k + f;
                    int gaps = PressMath.FrontOf(_lat, _alg, m, r.ManWidthMetres, r.FrontDepthMetres, r.GapMetres, _bins, GapLo, GapHi, at * G,
                        out double front, out double lo, out double hi, out int frontMen, out int cut);
                    Front[at] = front;
                    Lo[at] = lo;
                    Hi[at] = hi;
                    FrontMen[at] = frontMen;
                    GapN[at] = gaps;
                    GapsCut += cut;
                    if (gaps > 0)
                    {
                        FacesWithGaps++;
                        GapsFound += gaps;
                        for (int g = 0; g < gaps; g++)
                        {
                            double wg = GapHi[at * G + g] - GapLo[at * G + g];
                            if (wg > WidestGap) WidestGap = wg;
                        }
                    }
                }
                Depth[k] = Math.Max(PressMath.MinDepth, Front[2 * k] + Front[2 * k + 1]);
                WidthSum += Hi[2 * k] - Lo[2 * k];
                WidthN++;
            }
            Valid[k] = true;
            Kind[k] = kind;
            PublishedAt[k] = now;
            LinesPublished++;
            Kinds[(int)kind]++;
        }

        private void PublishAttacker(int k, double now, in PressRules r)
        {
            int best = -1, bestN = 0;
            for (int i = k * V; i < k * V + V; i++)
            {
                int c = _voteKey[i];
                if (c < 0 || _voteN[i] <= bestN || !IsLine(c, now)) continue;
                best = c;
                bestN = _voteN[i];
            }
            if (best < 0)
            {
                // no man of it saw a line this period: the last one stands while it is still a line (a man behind it far from any enemy)
                if (!double.IsNaN(AttackAt[k]) && now - AttackAt[k] <= PressMath.StaleSeconds && IsLine(EnemyLine[k], now)) return;
                EnemyLine[k] = -1;
                Wraps[k] = false;
                return;
            }
            var xs = _x[k];
            var ys = _y[k];
            if (xs == null || ys == null) return;
            int m = Math.Min(_n[k], PressMath.MaxSamples);
            bool same = EnemyLine[k] == best && !double.IsNaN(AttackAt[k]) && now - AttackAt[k] <= PressMath.StaleSeconds;
            EnemyLine[k] = best;
            AttackAt[k] = now;
            if (Kind[best] == PressLineKind.Ring)
            {
                Face[k] = 0;
                Wraps[k] = false;
                UnitWidth[k] = double.NaN;
                double rx = RingX[best], ry = RingY[best], rr = RingR[best];
                for (int i = 0; i < m; i++) _alg[i] = Math.Sqrt((xs[i] - rx) * (xs[i] - rx) + (ys[i] - ry) * (ys[i] - ry)) - rr;
                Array.Sort(_alg, 0, m);
                UnitFront[k] = PressMath.Quantile(_alg, m, 0.1);
                return;
            }
            double ux = UX[best], uy = UY[best], ex = -uy, ey = ux;
            double mean = 0;
            for (int i = 0; i < m; i++) mean += xs[i] * ux + ys[i] * uy;
            mean /= m;
            double depth = Depth[best], frontAlong = Front[2 * best], mid = frontAlong - depth / 2;
            int face = mean >= mid ? 0 : 1;
            if (same)
            {
                double swing = depth / 2 + PressMath.FaceHysteresisMetres;
                if (Face[k] == 0 && mean >= mid - swing) face = 0;
                else if (Face[k] == 1 && mean <= mid + swing) face = 1;
            }
            Face[k] = face;
            int at = 2 * best + face;
            for (int i = 0; i < m; i++)
            {
                double a = xs[i] * ux + ys[i] * uy;
                _lat[i] = xs[i] * ex + ys[i] * ey;
                _alg[i] = face == 0 ? a - Front[at] : -a - Front[at];
            }
            Array.Sort(_lat, 0, m);
            Array.Sort(_alg, 0, m);
            double q = PressMath.WidthQuantile;
            UnitWidth[k] = m >= 2 ? (PressMath.Quantile(_lat, m, 1 - q) - PressMath.Quantile(_lat, m, q)) / (1 - 2 * q) : 0;
            UnitFront[k] = PressMath.Quantile(_alg, m, 0.1);
            bool wraps = PressMath.UnitWraps(in r, UnitWidth[k], Hi[at] - Lo[at]);
            if (wraps && !Wraps[k]) WrapUnits++;
            Wraps[k] = wraps;
        }

        /// <summary>A point in a line's face frame: <paramref name="d"/> in front of the face (outward positive), <paramref name="l"/> across.</summary>
        public void Frame(int line, int face, double x, double y, out double d, out double l)
        {
            double ux = UX[line], uy = UY[line];
            double a = x * ux + y * uy;
            l = -uy * x + ux * y;
            d = face == 0 ? a - Front[2 * line] : -a - Front[2 * line + 1];
        }

        /// <summary>The world point of a place in a line's face frame.</summary>
        public void World(int line, int face, double d, double l, out double x, out double y)
        {
            double ux = UX[line], uy = UY[line], ex = -uy, ey = ux;
            double a = face == 0 ? Front[2 * line] + d : -(Front[2 * line + 1] + d);
            x = a * ux + l * ex;
            y = a * uy + l * ey;
        }

        /// <summary>The world direction INTO the line from its face (the clamp's).</summary>
        public void Inward(int line, int face, out double nx, out double ny)
        {
            double s = face == 0 ? -1 : 1;
            nx = s * UX[line];
            ny = s * UY[line];
        }

        /// <summary>The direction along the face's ends (+e = toward the high end).</summary>
        public void Across(int line, out double ex, out double ey)
        {
            ex = -UY[line];
            ey = UX[line];
        }

        /// <summary>A ring: his distance outside its front (negative = inside) and the direction INTO it.</summary>
        public void RingFrame(int line, double x, double y, out double d, out double nx, out double ny)
        {
            double dx = RingX[line] - x, dy = RingY[line] - y, len = Math.Sqrt(dx * dx + dy * dy);
            d = len - RingR[line];
            if (len > 1e-6)
            {
                nx = dx / len;
                ny = dy / len;
            }
            else
            {
                nx = UX[line];
                ny = UY[line];
            }
        }

        /// <summary>The line's two front ends of a face as world points (the block hold's range).</summary>
        public void Ends(int line, int face, out double ax, out double ay, out double bx, out double by)
        {
            int at = 2 * line + face;
            World(line, face, 0, Lo[at], out ax, out ay);
            World(line, face, 0, Hi[at], out bx, out by);
        }
    }

    /// <summary>Step 77 per mission: the measurement (ON and OFF), the press's counts and the [summary] "press, don't pass" lines.</summary>
    public sealed class PressStats
    {
        /// <summary>[on 1 / off 0, round an end 0 / through the middle 1, cause, not allowed 0 / allowed 1].</summary>
        public readonly long[,,,] Crossings = new long[2, 2, (int)PressCause.Count, 2];

        /// <summary>Allowed crossings by why [on/off, gap 0 / wrap 1 / broken or winning 2].</summary>
        public readonly long[,] AllowedBy = new long[2, 3];

        /// <summary>Looks of men with an enemy line within the watch or past it, and those PAST its front by more than 1 m [on/off].</summary>
        public readonly long[] NearLooks = new long[2], PastLooks = new long[2];

        /// <summary>Looks by verdict (ON only - OFF decides the same, counted apart).</summary>
        public readonly long[,] Actions = new long[2, (int)PressAction.Count];

        public readonly long[] Ends = new long[(int)PressEnd.Count];
        public long Wishes, Men, PushedMen, ReturnedMen;
        public long ClampFrames, SideFrames, MoveFrames;
        public double WishSeconds;
        public long BandClamps, BandExtraRanks, BlockRangeLooks;
        public long Ticks, Steers, Stands, Errors;
        public double LookCostMs, SteerCostMs, PublishCostMs;

        public void AddSummaryLines(List<string> lines, in PressRules r, PressLines t)
        {
            var sb = new StringBuilder();
            sb.Append("press, don't pass (step 77): ").Append(r.On ? "ON" : "OFF (" + r.OffBecause + ")").Append(" at the end - lines published ").Append(t.LinesPublished)
              .Append(" (held by step 41 ").Append(t.Kinds[(int)PressLineKind.Wall]).Append(", a held block ").Append(t.Kinds[(int)PressLineKind.Block])
              .Append(", holding their slots ").Append(t.Kinds[(int)PressLineKind.Slots]).Append(", squares / circles ").Append(t.Kinds[(int)PressLineKind.Ring])
              .Append("), their fronts ").Append(F1(t.WidthN > 0 ? t.WidthSum / t.WidthN : double.NaN)).Append(" m wide on average; GAPS found ").Append(t.GapsFound)
              .Append(" in ").Append(t.FacesWithGaps).Append(" face publishes (the widest ").Append(F1(t.WidestGap)).Append(" m");
            if (t.GapsCut > 0) sb.Append(", ").Append(t.GapsCut).Append(" more not kept");
            sb.Append("); units WIDER than their line (wraps allowed) ").Append(t.WrapUnits).Append("; looks ON:");
            AppendActions(sb, 1);
            sb.Append(" | OFF (measured):");
            AppendActions(sb, 0);
            sb.Append("; CLAMPS applied ").Append(ClampFrames).Append(" input frames (the front) + ").Append(SideFrames).Append(" (a rear-rank man's side), walks ")
              .Append(MoveFrames).Append(" frames; wishes ").Append(Wishes).Append(" by ").Append(Men).Append(" men (pushed out ").Append(PushedMen).Append(", walked round an end ")
              .Append(ReturnedMen).Append("), ").Append(F1(WishSeconds)).Append(" man-s; ends:");
            for (int e = 0; e < (int)PressEnd.Count; e++)
            {
                if (Ends[e] == 0) continue;
                sb.Append(' ').Append(PressMath.EndName((PressEnd)e)).Append(' ').Append(Ends[e]).Append(',');
            }
            if (sb[sb.Length - 1] == ',') sb.Length--;
            sb.Append("; the fear band clamped to its enemy line ").Append(BandClamps).Append(" times (+").Append(BandExtraRanks).Append(" ranks deeper); the block hold's sideways range cut to the enemy line's ends ")
              .Append(BlockRangeLooks).Append(" looks; steering ").Append(Steers).Append(" walking / ").Append(Stands).Append(" clamped or standing; cost ")
              .Append(F1(LookCostMs)).Append(" ms looks + ").Append(F1(SteerCostMs)).Append(" ms steering + ").Append(F1(PublishCostMs)).Append(" ms lines (")
              .Append(Ticks > 0 ? ((LookCostMs + SteerCostMs + PublishCostMs) / Ticks).ToString("0.000", CultureInfo.InvariantCulture) : "-").Append(" ms a tick); errors ").Append(Errors);
            lines.Add(sb.ToString());

            var c = new StringBuilder();
            c.Append("press, don't pass (step 77) - men who CROSSED an enemy line's front (more than ").Append(PressMath.CrossedMetres.ToString("0.0", CultureInfo.InvariantCulture))
             .Append(" m past it, on its side at the look before): ON ").Append(CrossText(1)).Append(" | OFF ").Append(CrossText(0))
             .Append("; men past a front at a look (of the looks near a line): ON ").Append(Share(1)).Append(" | OFF ").Append(Share(0));
            lines.Add(c.ToString());
        }

        private void AppendActions(StringBuilder sb, int o)
        {
            bool any = false;
            for (int a = 0; a < (int)PressAction.Count; a++)
            {
                long v = Actions[o, a];
                if (v == 0) continue;
                sb.Append(' ').Append(PressMath.ActionName((PressAction)a)).Append(' ').Append(v).Append(',');
                any = true;
            }
            if (any) sb.Length--;
            else sb.Append(" none");
        }

        private string CrossText(int o)
        {
            long total = 0;
            for (int w = 0; w < 2; w++)
                for (int k = 0; k < (int)PressCause.Count; k++)
                    for (int a = 0; a < 2; a++)
                        total += Crossings[o, w, k, a];
            var sb = new StringBuilder();
            sb.Append(total).Append(" - round an end ").Append(Where(o, 0)).Append(", through the middle ").Append(Where(o, 1)).Append("; allowed: a gap ")
              .Append(AllowedBy[o, 0]).Append(", a wrap ").Append(AllowedBy[o, 1]).Append(", a broken line / winning ").Append(AllowedBy[o, 2]);
            return sb.ToString();
        }

        private string Where(int o, int w)
        {
            long n = 0;
            var sb = new StringBuilder();
            for (int k = 0; k < (int)PressCause.Count; k++)
            {
                long v = Crossings[o, w, k, 0] + Crossings[o, w, k, 1];
                n += v;
                if (v == 0) continue;
                sb.Append(sb.Length == 0 ? " (" : ", ").Append(PressMath.CauseName((PressCause)k)).Append(' ').Append(v);
            }
            if (sb.Length > 0) sb.Append(')');
            return n + sb.ToString();
        }

        private string Share(int o) =>
            NearLooks[o] > 0 ? (100.0 * PastLooks[o] / NearLooks[o]).ToString("0.0", CultureInfo.InvariantCulture) + "% (" + PastLooks[o] + " of " + NearLooks[o] + ")" : "- (no looks)";

        internal static string F1(double v) => double.IsNaN(v) ? "-" : v.ToString("0.0", CultureInfo.InvariantCulture);
    }
}

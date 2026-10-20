using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>Step 70: what the band did to one man's place at a look.</summary>
    public enum FearBandPlaceKind
    {
        /// <summary>No band known for his unit (no direction yet) - step 67's point shelter is used.</summary>
        NoBand,

        /// <summary>His place lies inside the band already - kept.</summary>
        Kept,

        /// <summary>Compressed in depth only (pulled up to the band's rear, or back to its front) - his place along the front kept.</summary>
        Compressed,

        /// <summary>His file is full (more men than the ranks) and he stands behind its ranks: he slides out toward the band's ends.</summary>
        Slid,

        /// <summary>His file is full, he stands behind its ranks, but no file of the band has room - he stays where the depth puts him.</summary>
        Full,

        Count,
    }

    /// <summary>
    /// Step 70 pure math (Toni's 30 v 50 looters, 2026-10-19 15:59: the looter mass stood as a deep, narrow column ~15 men deep - the log's
    /// depth ÷ width in contact 1.44, 3.5 m deep, 2.6 m wide; his sketch: "a wide, shallow mass along the whole line, 4-6 men deep, a few
    /// stepping forward to swing and coming back"). The cause: step 67's shelter was a POINT - every frightened man's place slid toward it,
    /// along the front as much as in depth, and a charge pulled its men right onto it - so the unit drew into a ball, and "step in 1 m behind
    /// the comrade in front" stacked the files. THE BAND: a strip facing the enemy's front near the unit - its direction the enemy mass's
    /// front line, its width the frontage the unit faces (or its men in ranks, whichever is wider, capped), its depth a few ranks by the
    /// pull, its front short of the enemy and pushed back by the pull (creeping at step 67's drift cap). A man keeps his place ALONG the
    /// front and is compressed only in DEPTH; in a file deeper than the ranks the men behind them slide out toward the band's ends.
    /// </summary>
    public static class FearBandMath
    {
        /// <summary>The most files a band holds (a wider band gets wider files).</summary>
        public const int MaxFiles = 64;

        /// <summary>A unit's positions binned into files at each publish - at most this many a period (the moments count every man).</summary>
        public const int MaxSamples = 512;

        /// <summary>A uniform spread's half extent = √3 σ (the men's front from their moments).</summary>
        public const double Sqrt3 = 1.7320508075688772;

        /// <summary>The enemy formation's own facing sets the band's direction when it faces the unit within 120° (its front line, not the line
        /// between the centres).</summary>
        public const double AlignCosine = -0.5;

        /// <summary>A man slides out of a full file only when he stands this many ranks behind its last allowed rank (no flicker at the edge).</summary>
        public const double SlideRankSlack = 0.5;

        /// <summary>The measurement's own files over the men's real span (the shape lines ON and OFF): at most this many.</summary>
        public const int MeasureFiles = 160;

        /// <summary>The band's depth in ranks at this pull: the min at 0, a line to the max at 1, rounded.</summary>
        public static int Ranks(in FearRules r, double pull)
        {
            double lo = r.BandRanksMin, hi = Math.Max(r.BandRanksMin, r.BandRanksMax);
            return (int)Math.Round(lo + (hi - lo) * FearMath.Clamp01(pull), MidpointRounding.AwayFromZero);
        }

        /// <summary>The band's width: the enemy's frontage (NaN = unknown) or his men in <paramref name="ranks"/> ranks of files, whichever is wider,
        /// at least one file, at most BandMaxWidthMetres.</summary>
        public static double Width(in FearRules r, int men, int ranks, double enemyFrontage)
        {
            double fromMen = Math.Max(0, men) / (double)Math.Max(1, ranks) * r.BandFileMetres;
            double w = Math.Max(double.IsNaN(enemyFrontage) || enemyFrontage < 0 ? 0 : enemyFrontage, fromMen);
            double max = Math.Max(r.BandFileMetres, r.BandMaxWidthMetres);
            return w < r.BandFileMetres ? r.BandFileMetres : w > max ? max : w;
        }

        /// <summary>The band's lateral centre: his men's own, slid so the band lies along the enemy's front - inside it when narrower, covering it
        /// when wider (<paramref name="enemyCentre"/> NaN = the men's own).</summary>
        public static double LateralCentre(double menCentre, double enemyCentre, double enemyFrontage, double width)
        {
            if (double.IsNaN(enemyCentre) || double.IsNaN(enemyFrontage) || !(enemyFrontage > 0)) return menCentre;
            double h = enemyFrontage / 2, w2 = width / 2;
            double a = enemyCentre - h + w2, b = enemyCentre + h - w2;
            double lo = Math.Min(a, b), hi = Math.Max(a, b);
            return menCentre < lo ? lo : menCentre > hi ? hi : menCentre;
        }

        /// <summary>Where the band's front WANTS to be (along the direction to the enemy): BandGapMetres short of the enemy's front (unknown = the
        /// men's own front), pushed back ShelterBackMetres × the unit's pull.</summary>
        public static double FrontWanted(in FearRules r, double contactAlong, double ownFront, double pull) =>
            (double.IsNaN(contactAlong) ? ownFront : contactAlong - r.BandGapMetres) - r.ShelterBackMetres * FearMath.Clamp01(pull);

        /// <summary>The front's step toward the wanted line: free forward, back at most <paramref name="maxBack"/> (step 67's drift cap).</summary>
        public static double FrontStep(double front, double wanted, double maxBack)
        {
            if (double.IsNaN(front)) return wanted;
            if (wanted >= front) return wanted;
            double back = maxBack > 0 ? maxBack : 0;
            return Math.Max(wanted, front - back);
        }

        /// <summary>The band's files: its width in files of BandFileMetres, 1 to <see cref="MaxFiles"/>.</summary>
        public static int Files(double width, double fileMetres)
        {
            if (!(width > 0) || !(fileMetres > 0)) return 1;
            int n = (int)Math.Ceiling(width / fileMetres - 1e-9);
            return n < 1 ? 1 : n > MaxFiles ? MaxFiles : n;
        }

        /// <summary>His rank in his file: from its front-most man (1 = the front rank).</summary>
        public static double RankOf(double fileFront, double along, double rankMetres)
        {
            if (double.IsNaN(fileFront) || double.IsInfinity(fileFront) || !(rankMetres > 0)) return 1;
            double r = (fileFront - along) / rankMetres + 1;
            return r < 1 ? 1 : r;
        }

        /// <summary>He slides out: his file holds more men than the ranks and he stands behind them.</summary>
        public static bool Slides(int count, int ranks, double rank) => count > ranks && rank > ranks + SlideRankSlack;

        /// <summary>The nearest file with room (fewer men than the ranks), searching out from <paramref name="from"/>; on a tie the side farther from
        /// the band's middle wins (toward the ends - the front widens). −1 = none.</summary>
        public static int FreeFile(int[] counts, int offset, int files, int from, int ranks)
        {
            double mid = (files - 1) / 2.0;
            for (int d = 1; d < files; d++)
            {
                int a = from - d, b = from + d;
                bool okA = a >= 0 && counts[offset + a] < ranks, okB = b < files && counts[offset + b] < ranks;
                if (okA && okB) return Math.Abs(a - mid) >= Math.Abs(b - mid) ? a : b;
                if (okA) return a;
                if (okB) return b;
            }
            return -1;
        }

        public static string KindName(FearBandPlaceKind k) => k switch
        {
            FearBandPlaceKind.NoBand => "no band yet (the point)",
            FearBandPlaceKind.Kept => "in the band as he was",
            FearBandPlaceKind.Compressed => "compressed in depth",
            FearBandPlaceKind.Slid => "slid out of a full file",
            FearBandPlaceKind.Full => "a full file, no room in the band",
            _ => k.ToString(),
        };
    }

    /// <summary>
    /// Step 70: THE BAND per unit (the unit table's second half - fixed arrays; a unit's position samples allocated once, the first time it is
    /// seen): every look adds his position (its moments, a sample for the files); every publish the band's frame - the direction to the enemy
    /// (the enemy seen by its men in contact, else the enemy formation's centre, aligned with that formation's own front when it faces the
    /// unit; else the unit's facing), the men's own front, the enemy's front, the band's front (creeping back at the drift cap), depth, width,
    /// lateral centre, files - and the files' men and their front-most man; the shape in ranks ON / OFF.
    /// </summary>
    public sealed partial class FearUnits
    {
        private const int Nb = FearMath.UnitCount;
        private const int Fb = FearBandMath.MaxFiles;

        // this period
        private readonly int[] _bn = new int[Nb], _bs = new int[Nb];
        private readonly double[] _bsx = new double[Nb], _bsy = new double[Nb], _bsxx = new double[Nb], _bsxy = new double[Nb], _bsyy = new double[Nb];
        private readonly double[] _lfx = new double[Nb], _lfy = new double[Nb];
        private readonly double[]?[] _sampleX = new double[Nb][], _sampleY = new double[Nb][], _sampleS = new double[Nb][], _sampleBX = new double[Nb][], _sampleBY = new double[Nb][];
        private readonly int[]?[] _sampleId = new int[Nb][], _pubId = new int[Nb][], _pubFile = new int[Nb][], _pubRank = new int[Nb][];

        /// <summary>The published period's samples per unit (their ids, files and ranks stay readable until the next publish).</summary>
        private readonly int[] _ranked = new int[Nb];

        // the enemy formation the unit faces (read by the module once a period)
        private readonly double[] _efAt = new double[Nb], _efx = new double[Nb], _efy = new double[Nb], _efgx = new double[Nb], _efgy = new double[Nb], _efw = new double[Nb], _efd = new double[Nb];

        // the band's front as a world point (it creeps)
        private readonly bool[] _hasFront = new bool[Nb];
        private readonly double[] _bfx = new double[Nb], _bfy = new double[Nb];

        private readonly int[] _measure = new int[FearBandMath.MeasureFiles];

        // step 74: the block the band must sit in (its centre as a world point, its half-width; NaN time = none)
        private readonly double[] _blkAt = new double[Nb], _blkX = new double[Nb], _blkY = new double[Nb], _blkHalf = new double[Nb];

        /// <summary>Step 74: the band publishes that were narrowed / moved to sit inside their unit's block.</summary>
        public long BandBlockClamps;

        /// <summary>Step 74: the unit's block (the block hold's frame): the band sits inside its width around its centre line.</summary>
        public void SetBlock(int key, double now, double cx, double cy, double halfWidth)
        {
            if (key < 0 || key >= Nb || double.IsNaN(cx) || double.IsNaN(cy) || !(halfWidth > 0)) return;
            _blkAt[key] = now;
            _blkX[key] = cx;
            _blkY[key] = cy;
            _blkHalf[key] = halfWidth;
        }

        /// <summary>Step 74: no block for this unit (not held, off).</summary>
        public void ClearBlock(int key)
        {
            if (key >= 0 && key < Nb) _blkAt[key] = double.NaN;
        }

        // published
        public readonly bool[] BandValid = new bool[Nb];
        public readonly double[] BandUX = new double[Nb], BandUY = new double[Nb];
        public readonly double[] BandFront = new double[Nb], BandDepth = new double[Nb], BandWidth = new double[Nb], BandCentre = new double[Nb], BandFileWidth = new double[Nb];
        public readonly int[] BandRanks = new int[Nb], BandFiles = new int[Nb];

        /// <summary>The men's own front (along the direction to the enemy: their mean + √3 σ) and the enemy's front (the enemy their men in contact
        /// see, else the enemy formation's centre less half its depth; NaN = unknown).</summary>
        public readonly double[] OwnFront = new double[Nb], EnemyFrontAlong = new double[Nb];

        /// <summary>The files' men (published, then moved as men slide - a reservation) and each file's front-most man (along).</summary>
        public readonly int[] FileCount = new int[Nb * Fb];
        public readonly double[] FileFront = new double[Nb * Fb];

        private void ResetBand()
        {
            for (int k = 0; k < Nb; k++)
            {
                ClearBandPeriod(k);
                _efAt[k] = double.NaN;
                _blkAt[k] = double.NaN; // step 74
                _hasFront[k] = false;
                BandValid[k] = false;
                OwnFront[k] = EnemyFrontAlong[k] = double.NaN;
                _lfx[k] = _lfy[k] = 0;
            }
        }

        private void ClearBandPeriod(int k)
        {
            _bn[k] = _bs[k] = 0;
            _bsx[k] = _bsy[k] = _bsxx[k] = _bsxy[k] = _bsyy[k] = 0;
        }

        private void NoteBand(int key, double px, double py, double fx, double fy, int man, double binX, double binY)
        {
            if (fx * fx + fy * fy > 1e-6)
            {
                _lfx[key] = fx;
                _lfy[key] = fy;
            }
            if (double.IsNaN(px) || double.IsNaN(py)) return;
            _bn[key]++;
            _bsx[key] += px;
            _bsy[key] += py;
            _bsxx[key] += px * px;
            _bsxy[key] += px * py;
            _bsyy[key] += py * py;
            int s = _bs[key];
            if (s >= FearBandMath.MaxSamples) return;
            var xs = _sampleX[key];
            if (xs == null)
            {
                xs = _sampleX[key] = new double[FearBandMath.MaxSamples]; // once per unit and battle
                _sampleY[key] = new double[FearBandMath.MaxSamples];
                _sampleId[key] = new int[FearBandMath.MaxSamples];
                _sampleS[key] = new double[FearBandMath.MaxSamples];
                _sampleBX[key] = new double[FearBandMath.MaxSamples];
                _sampleBY[key] = new double[FearBandMath.MaxSamples];
                _pubId[key] = new int[FearBandMath.MaxSamples];
                _pubFile[key] = new int[FearBandMath.MaxSamples];
                _pubRank[key] = new int[FearBandMath.MaxSamples];
            }
            xs[s] = px;
            _sampleY[key]![s] = py;
            _sampleId[key]![s] = man;
            bool bound = !double.IsNaN(binX) && !double.IsNaN(binY); // a slid man counts in the file he is bound for (the reservation lasts)
            _sampleBX[key]![s] = bound ? binX : px;
            _sampleBY[key]![s] = bound ? binY : py;
            _bs[key] = s + 1;
        }

        /// <summary>The enemy formation's read is due for this unit (once a publish period).</summary>
        public bool EnemyFrontDue(int key, double now) =>
            key >= 0 && key < Nb && (double.IsNaN(_efAt[key]) || now - _efAt[key] >= FearMath.PublishSeconds || now < _efAt[key]);

        /// <summary>The enemy formation the unit faces: its centre, its facing (a unit vector or 0), its width and depth (m).</summary>
        public void NoteEnemyFront(int key, double now, double x, double y, double gx, double gy, double width, double depth)
        {
            if (key < 0 || key >= Nb) return;
            _efAt[key] = now;
            _efx[key] = x;
            _efy[key] = y;
            _efgx[key] = gx;
            _efgy[key] = gy;
            _efw[key] = width;
            _efd[key] = depth;
        }

        /// <summary>No enemy formation to read for this unit now (its stamp moves on; the band uses the enemy its men see).</summary>
        public void NoEnemyFront(int key, double now)
        {
            if (key < 0 || key >= Nb) return;
            _efAt[key] = now;
            _efw[key] = double.NaN;
        }

        private bool EnemyFrontLive(int k, double now) => !double.IsNaN(_efAt[k]) && !double.IsNaN(_efw[k]) && now - _efAt[k] <= FearMath.StaleSeconds;

        /// <summary>The band is known for this unit now.</summary>
        public bool BandLive(int key, double now) => Live(key, now) && BandValid[key];

        private void PublishBand(int k, double now, double dt, in FearRules r, FearStats? stats, bool on, bool enemySeen, double ax, double ay)
        {
            int n = _bn[k];
            bool had = BandValid[k] && Live(k, now);
            if (n <= 0)
            {
                BandValid[k] = false;
                return;
            }
            double mx = _bsx[k] / n, my = _bsy[k] / n;
            double cxx = Math.Max(0, _bsxx[k] / n - mx * mx), cyy = Math.Max(0, _bsyy[k] / n - my * my), cxy = _bsxy[k] / n - mx * my;
            bool ef = EnemyFrontLive(k, now);
            double ux = 0, uy = 0;
            bool hasU = false;
            if (enemySeen && ax * ax + ay * ay > 1e-6)
            {
                ux = -ax;
                uy = -ay;
                hasU = true;
            }
            else if (ef)
            {
                double dx = _efx[k] - mx, dy = _efy[k] - my, len = Math.Sqrt(dx * dx + dy * dy);
                if (len > 1e-3)
                {
                    ux = dx / len;
                    uy = dy / len;
                    hasU = true;
                }
            }
            if (hasU && ef && _efgx[k] * _efgx[k] + _efgy[k] * _efgy[k] > 1e-6 && _efgx[k] * ux + _efgy[k] * uy < FearBandMath.AlignCosine)
            {
                ux = -_efgx[k]; // the enemy formation faces us: the band lies along its front line
                uy = -_efgy[k];
            }
            if (!hasU && _lfx[k] * _lfx[k] + _lfy[k] * _lfy[k] > 1e-6)
            {
                ux = _lfx[k];
                uy = _lfy[k];
                hasU = true;
            }
            if (!hasU)
            {
                BandValid[k] = false;
                return;
            }
            double ulen = Math.Sqrt(ux * ux + uy * uy);
            ux /= ulen;
            uy /= ulen;
            double ex = -uy, ey = ux;
            double sU = Math.Sqrt(Math.Max(0, ux * ux * cxx + 2 * ux * uy * cxy + uy * uy * cyy));
            double mU = mx * ux + my * uy, mE = mx * ex + my * ey;
            double ownFront = mU + FearBandMath.Sqrt3 * sU;
            double contact = double.NaN;
            if (enemySeen && _en[k] > 0) contact = (_ex[k] / _en[k]) * ux + (_ey[k] / _en[k]) * uy;
            else if (ef) contact = _efx[k] * ux + _efy[k] * uy - Math.Max(0, _efd[k]) / 2;
            OwnFront[k] = ownFront;
            EnemyFrontAlong[k] = contact;
            double pull = FearMath.Clamp01(Pull[k]);
            int ranks = FearBandMath.Ranks(in r, pull);
            double depth = ranks * r.BandRankMetres;
            double wanted = FearBandMath.FrontWanted(in r, contact, ownFront, pull);
            double prev = had && _hasFront[k] ? _bfx[k] * ux + _bfy[k] * uy : ownFront;
            double front = FearBandMath.FrontStep(prev, wanted, FearMath.DriftCap(in r, _order[k], dt));
            _bfx[k] = mx + (front - mU) * ux;
            _bfy[k] = my + (front - mU) * uy;
            _hasFront[k] = true;
            double frontage = ef ? _efw[k] : double.NaN;
            double width = FearBandMath.Width(in r, Men[k], ranks, frontage);
            double centre = ef ? FearBandMath.LateralCentre(mE, _efx[k] * ex + _efy[k] * ey, frontage, width) : mE;
            if (!double.IsNaN(_blkAt[k]) && now - _blkAt[k] <= FearMath.StaleSeconds && now >= _blkAt[k] - 1e-9 && _blkHalf[k] > 0)
            {
                // step 74: the frightened band sits INSIDE its block - never wider than the block, never beyond its sides
                double hw = _blkHalf[k], bc = _blkX[k] * ex + _blkY[k] * ey;
                bool clamped = false;
                if (width > 2 * hw)
                {
                    width = 2 * hw;
                    clamped = true;
                }
                double room = hw - width / 2;
                if (centre < bc - room)
                {
                    centre = bc - room;
                    clamped = true;
                }
                else if (centre > bc + room)
                {
                    centre = bc + room;
                    clamped = true;
                }
                if (clamped) BandBlockClamps++;
            }
            int files = FearBandMath.Files(width, r.BandFileMetres);
            double fileW = width / files;
            BandValid[k] = true;
            BandUX[k] = ux;
            BandUY[k] = uy;
            BandFront[k] = front;
            BandDepth[k] = depth;
            BandWidth[k] = width;
            BandCentre[k] = centre;
            BandFileWidth[k] = fileW;
            BandRanks[k] = ranks;
            BandFiles[k] = files;

            // the files: this period's positions in the new frame; each man's rank in his file (1 = its front-most), published with his id
            int o = k * Fb;
            for (int f = 0; f < Fb; f++)
            {
                FileCount[o + f] = 0;
                FileFront[o + f] = double.NegativeInfinity;
            }
            int samples = _bs[k];
            var xs = _sampleX[k];
            var ys = _sampleY[k];
            var ss = _sampleS[k];
            var ids = _sampleId[k];
            var bxs = _sampleBX[k];
            var bys = _sampleBY[k];
            var pubId = _pubId[k];
            var pubFile = _pubFile[k];
            var pubRank = _pubRank[k];
            double lo = centre - width / 2;
            double minL = double.MaxValue, maxL = double.MinValue;
            bool arrays = xs != null && ys != null && ss != null && ids != null && bxs != null && bys != null && pubId != null && pubFile != null && pubRank != null;
            if (!arrays) samples = 0;
            for (int i = 0; i < samples; i++)
            {
                double l = bxs![i] * ex + bys![i] * ey, s = bxs[i] * ux + bys[i] * uy; // where he is bound (a slid man's file) - the files and ranks
                double realL = xs![i] * ex + ys![i] * ey; // where he stands - the measurement
                int f = (int)Math.Floor((l - lo) / fileW);
                if (f < 0) f = 0;
                else if (f >= files) f = files - 1;
                FileCount[o + f]++;
                if (s > FileFront[o + f]) FileFront[o + f] = s;
                ss![i] = s;
                pubFile![i] = f;
                pubId![i] = ids![i];
                if (realL < minL) minL = realL;
                if (realL > maxL) maxL = realL;
            }
            for (int i = 0; i < samples; i++)
            {
                int f = pubFile![i], rank = 1;
                double s = ss![i];
                for (int j = 0; j < samples; j++)
                    if (j != i && pubFile[j] == f && (ss[j] > s || (ss[j] == s && j < i))) rank++;
                pubRank![i] = rank;
            }
            _ranked[k] = samples;

            // the shape in RANKS, in contact (measurement - its own files of BandFileMetres over the men's real span, ON and OFF)
            if (stats != null && enemySeen && samples >= FearMath.ShapeMinMen && xs != null && ys != null)
            {
                double mw = r.BandFileMetres;
                int mfiles = (int)Math.Ceiling((maxL - minL) / mw + 1e-9);
                if (mfiles < 1) mfiles = 1;
                if (mfiles > FearBandMath.MeasureFiles)
                {
                    mw = (maxL - minL) / FearBandMath.MeasureFiles + 1e-9;
                    mfiles = FearBandMath.MeasureFiles;
                }
                for (int f = 0; f < mfiles; f++) _measure[f] = 0;
                for (int i = 0; i < samples; i++)
                {
                    int f = (int)Math.Floor((xs[i] * ex + ys[i] * ey - minL) / mw);
                    if (f < 0) f = 0;
                    else if (f >= mfiles) f = mfiles - 1;
                    _measure[f]++;
                }
                int occupied = 0, deepest = 0, over = 0;
                for (int f = 0; f < mfiles; f++)
                {
                    int c = _measure[f];
                    if (c <= 0) continue;
                    occupied++;
                    if (c > deepest) deepest = c;
                    if (c > ranks) over++;
                }
                if (occupied > 0) stats.Band.AddShape(on, samples / (double)occupied, deepest, occupied * mw, over, occupied);
            }
        }

        /// <summary>
        /// HIS PLACE IN THE BAND: his place along the front (his slot's - or, under a charge, his own - lateral, clamped into the band; a slid man's
        /// file kept for the spell) and his depth clamped between the band's rear and front; in a file holding more men than the ranks, a man
        /// standing behind them slides out to the nearest file with room (toward the ends on a tie) - the move reserved in the counts so the next
        /// men see it. <paramref name="baseX"/>, <paramref name="baseY"/> = his slot (or his position under a charge), <paramref name="px"/>,
        /// <paramref name="py"/> = where he stands. Returns <see cref="FearBandPlaceKind.NoBand"/> (the target = the base) when no band is known.
        /// </summary>
        public FearBandPlaceKind BandPlace(in FearRules r, int key, double now, double baseX, double baseY, double px, double py, FearMind fm,
            out double tx, out double ty, out double lateral, out bool fileFull, int man = -1)
        {
            tx = baseX;
            ty = baseY;
            lateral = double.NaN;
            fileFull = false;
            if (!BandLive(key, now)) return FearBandPlaceKind.NoBand;
            double ux = BandUX[key], uy = BandUY[key], ex = -uy, ey = ux;
            double front = BandFront[key], rear = front - BandDepth[key], width = BandWidth[key], fileW = BandFileWidth[key];
            int files = BandFiles[key], ranks = BandRanks[key], o = key * Fb;
            double lo = BandCentre[key] - width / 2;
            // his file and his rank in it at the last publish (by his id; else from where he stands and the file's front-most man)
            int fc = -1;
            double rank = double.NaN;
            var pubId = _pubId[key];
            if (man >= 0 && pubId != null)
            {
                for (int i = 0; i < _ranked[key]; i++)
                {
                    if (pubId[i] != man) continue;
                    fc = _pubFile[key]![i];
                    rank = _pubRank[key]![i];
                    break;
                }
            }
            if (fc < 0)
            {
                fc = (int)Math.Floor((px * ex + py * ey - lo) / fileW);
                if (fc < 0) fc = 0;
                else if (fc >= files) fc = files - 1;
                rank = FearBandMath.RankOf(FileFront[o + fc], px * ux + py * uy, r.BandRankMetres);
            }
            bool slid = fm.HasSlide && fm.SlideKey == key;
            double l = slid ? fm.SlideX * ex + fm.SlideY * ey : baseX * ex + baseY * ey;
            double lMin = lo + fileW / 2, lMax = lo + width - fileW / 2;
            double lc = l < lMin ? lMin : l > lMax ? lMax : l;
            double s = baseX * ux + baseY * uy;
            // compressed in DEPTH only: ahead of the band = its front; behind it = his rank's place in the band (never all on its rear line)
            double sc = s > front ? front : s < rear ? Math.Max(rear, front - (Math.Min(rank, ranks) - 0.5) * r.BandRankMetres) : s;
            var kind = Math.Abs(sc - s) > 0.01 || Math.Abs(lc - l) > 0.01 ? FearBandPlaceKind.Compressed : FearBandPlaceKind.Kept;
            int count = FileCount[o + fc];
            fileFull = count >= ranks;
            // a slid man keeps his file - unless it overfilled and he stands behind its ranks there too (he moves on)
            bool overflow = FearBandMath.Slides(count, ranks, rank);
            if (slid && !overflow) kind = FearBandPlaceKind.Slid;
            else if (overflow)
            {
                int j = FearBandMath.FreeFile(FileCount, o, files, fc, ranks);
                if (j >= 0)
                {
                    int before = FileCount[o + j];
                    FileCount[o + fc]--;
                    FileCount[o + j]++;
                    lc = lo + (j + 0.5) * fileW;
                    sc = front - (before + 0.5) * r.BandRankMetres;
                    if (sc < rear) sc = rear;
                    if (sc > front) sc = front;
                    fm.HasSlide = true;
                    fm.SlideKey = key;
                    fm.SlideX = lc * ex + sc * ux;
                    fm.SlideY = lc * ey + sc * uy;
                    kind = FearBandPlaceKind.Slid;
                }
                else kind = slid ? FearBandPlaceKind.Slid : FearBandPlaceKind.Full;
            }
            tx = lc * ex + sc * ux;
            ty = lc * ey + sc * uy;
            lateral = lc;
            return kind;
        }

        /// <summary>Where he stands against his unit's fronts (along the direction to the enemy): metres ahead of his own men's front, metres past
        /// the enemy's front (NaN = unknown). False = no band frame for the unit now.</summary>
        public bool Fronts(int key, double now, double px, double py, out double aheadOfOwn, out double pastEnemy)
        {
            aheadOfOwn = pastEnemy = double.NaN;
            if (!BandLive(key, now)) return false;
            double s = px * BandUX[key] + py * BandUY[key];
            aheadOfOwn = s - OwnFront[key];
            pastEnemy = double.IsNaN(EnemyFrontAlong[key]) ? double.NaN : s - EnemyFrontAlong[key];
            return true;
        }

        /// <summary>A point's position past the enemy's front of this unit (NaN = unknown).</summary>
        public double PastEnemyFront(int key, double now, double x, double y)
        {
            if (!BandLive(key, now) || double.IsNaN(EnemyFrontAlong[key])) return double.NaN;
            return x * BandUX[key] + y * BandUY[key] - EnemyFrontAlong[key];
        }
    }

    /// <summary>
    /// Step 70 per mission: the band measured - in contact, each unit's men per occupied file (the depth in RANKS) and its deepest file, its
    /// width (occupied files × the file) - mean and p90, ON vs OFF - and the files over the band's ranks; how each place was made (kept,
    /// compressed in depth, slid out, full), the steps in behind a comrade kept / refused for a full file, the front men let go to strike and
    /// walked back - the [summary] "fear flock band" line.
    /// </summary>
    public sealed class FearBandStats
    {
        private const int Sides = 2;
        private const int RankBins = 160; // 0.25 rank each, to 40
        private const double RankBin = 0.25;
        private const int WidthBins = 300; // 0.5 m each, to 150 m
        private const double WidthBin = 0.5;

        private readonly long[] _rankHist = new long[Sides * RankBins], _widthHist = new long[Sides * WidthBins];
        private readonly double[] _ranks = new double[Sides], _deepest = new double[Sides], _width = new double[Sides];
        private readonly long[] _samples = new long[Sides], _files = new long[Sides], _over = new long[Sides];
        private readonly long[] _kinds = new long[(int)FearBandPlaceKind.Count];

        public long StepInsKept;
        public long StepInsRefused;
        public long StrikeLooks;
        public long StrikeLeashed;
        public long ChargeLooseInBand;

        public void AddShape(bool on, double menPerFile, int deepest, double width, int overCap, int files)
        {
            int i = on ? 1 : 0;
            _samples[i]++;
            _ranks[i] += menPerFile;
            _deepest[i] += deepest;
            _width[i] += width;
            _files[i] += files;
            _over[i] += overCap;
            int rb = (int)(menPerFile / RankBin);
            _rankHist[i * RankBins + (rb < 0 ? 0 : rb >= RankBins ? RankBins - 1 : rb)]++;
            int wb = (int)(width / WidthBin);
            _widthHist[i * WidthBins + (wb < 0 ? 0 : wb >= WidthBins ? WidthBins - 1 : wb)]++;
        }

        public void AddKind(FearBandPlaceKind k)
        {
            int i = (int)k;
            if (i >= 0 && i < _kinds.Length) _kinds[i]++;
        }

        public long Kind(FearBandPlaceKind k) => _kinds[(int)k];

        public long Samples(bool on) => _samples[on ? 1 : 0];

        public double MeanRanks(bool on) => _samples[on ? 1 : 0] > 0 ? _ranks[on ? 1 : 0] / _samples[on ? 1 : 0] : double.NaN;

        public double MeanDeepest(bool on) => _samples[on ? 1 : 0] > 0 ? _deepest[on ? 1 : 0] / _samples[on ? 1 : 0] : double.NaN;

        public double MeanWidth(bool on) => _samples[on ? 1 : 0] > 0 ? _width[on ? 1 : 0] / _samples[on ? 1 : 0] : double.NaN;

        public double OverCapShare(bool on) => _files[on ? 1 : 0] > 0 ? _over[on ? 1 : 0] / (double)_files[on ? 1 : 0] : double.NaN;

        public double P90Ranks(bool on) => P90(_rankHist, on ? 1 : 0, RankBins, RankBin);

        public double P90Width(bool on) => P90(_widthHist, on ? 1 : 0, WidthBins, WidthBin);

        private double P90(long[] hist, int side, int bins, double bin)
        {
            long total = _samples[side];
            if (total <= 0) return double.NaN;
            long need = (long)Math.Ceiling(total * 0.9), run = 0;
            for (int b = 0; b < bins; b++)
            {
                run += hist[side * bins + b];
                if (run >= need) return (b + 1) * bin;
            }
            return bins * bin;
        }

        private static string N(double v, string f = "0.0") => double.IsNaN(v) ? "-" : v.ToString(f, CultureInfo.InvariantCulture);

        private string Side(bool on) =>
            Samples(on) == 0
                ? "- (0 samples)"
                : N(MeanRanks(on)) + " men per file (p90 " + N(P90Ranks(on), "0.0#") + "), the deepest file " + N(MeanDeepest(on)) + ", width " + N(MeanWidth(on)) + " m (p90 "
                  + N(P90Width(on)) + "), files over the band's ranks " + N(100 * OverCapShare(on), "0") + "% (" + Samples(on) + " samples)";

        public void AddSummaryLines(List<string> lines, in FearRules r)
        {
            var sb = new StringBuilder();
            sb.Append("fear flock band (step 70 - ").Append(r.BandEnabled ? "a wide band along the enemy's front, " + r.BandRanksMin + "-" + r.BandRanksMax + " ranks" : "FearBandEnabled OFF - the point shelter")
              .Append(") - in contact, each unit's men per occupied ").Append(N(r.BandFileMetres, "0.0#")).Append(" m file (the depth in RANKS): ON ").Append(Side(true))
              .Append(" | OFF ").Append(Side(false)).Append("; places: ");
            for (int k = 0; k < (int)FearBandPlaceKind.Count; k++)
            {
                if (k > 0) sb.Append(", ");
                sb.Append(FearBandMath.KindName((FearBandPlaceKind)k)).Append(' ').Append(_kinds[k]);
            }
            sb.Append("; steps in behind a comrade kept ").Append(StepInsKept).Append(", refused (his file full - he slides instead) ").Append(StepInsRefused)
              .Append("; a charge's men left to their AI inside the band ").Append(ChargeLooseInBand).Append("; front men let go to STRIKE ").Append(StrikeLooks)
              .Append(" looks (an enemy within ").Append(N(r.StrikeReachMetres, "0.0#")).Append(" m), walked back past the leash (").Append(N(r.StrikeLeashMetres, "0.0#"))
              .Append(" m ahead of his place) ").Append(StrikeLeashed);
            lines.Add(sb.ToString());
        }
    }
}

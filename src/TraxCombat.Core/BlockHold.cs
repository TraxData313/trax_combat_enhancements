using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 74 - THE GREEN BLOCK STAYS A BLOCK INTO CONTACT (Toni's battle 2026-10-20 20:30: 180 T1 recruits held 5 deep by step 73 advanced as a
    /// block, then just before contact spread into a wide loose crowd with a trail wrapping round his right flank; AI_NOTES "Step 74", DESIGN
    /// decision 68). The settings, read live: the master switch FIRST, step 73's switches (the system; the player's formations by his own), the
    /// block hold's own switch, then the numbers.
    /// </summary>
    public readonly struct BlockHoldRules
    {
        public BlockHoldRules(bool modEnabled, bool lines, bool forPlayer, bool enabled, double slackMetres = 2, double fullAtDepth = 3, double looseMetres = 10,
            double rankMetres = 1.2, double aheadMetres = 3, double strikeReachMetres = 2.5, double strikeLeashMetres = 2, double nearMetres = 30,
            double leadMetres = 1.5, double mopUpWinPercent = 50)
        {
            ModEnabled = modEnabled;
            Lines = lines;
            ForPlayer = forPlayer;
            Enabled = enabled;
            SlackMetres = Clamp(slackMetres, 0, 20);
            FullAtDepth = Clamp(fullAtDepth, BlockHoldMath.MinFullAtDepth, LineDisciplineMath.MaxRanks);
            LooseMetres = Clamp(looseMetres, 0, 60);
            RankMetres = Clamp(rankMetres, 0.5, 3);
            AheadMetres = Clamp(aheadMetres, 0, 20);
            StrikeReachMetres = Clamp(strikeReachMetres, 0, 6);
            StrikeLeashMetres = Clamp(strikeLeashMetres, 0, 10);
            NearMetres = Clamp(nearMetres, 0, 200);
            LeadMetres = Clamp(leadMetres, 0, 6);
            MopUpWinPercent = Clamp(mopUpWinPercent, 0, 100);
        }

        public static BlockHoldRules From(TraxSettings s) =>
            new BlockHoldRules(s.ModEnabled, s.LineDisciplineEnabled, s.LineDisciplineForPlayer, s.LineBlockHoldEnabled, s.LineBlockSlackMetres,
                s.LineBlockFullAtDepth, s.LineBlockLooseMetres, s.LineBlockRankMetres, s.LineBlockAheadMetres, s.LineBlockStrikeReachMetres,
                s.LineBlockStrikeLeashMetres, s.LineBlockNearMetres, s.LineBlockLeadMetres, s.LineBlockMopUpWinPercent);

        public bool ModEnabled { get; }

        /// <summary>LineDisciplineEnabled - step 73's system (the cap the block is held to comes from it).</summary>
        public bool Lines { get; }

        /// <summary>LineDisciplineForPlayer - the player's own formations count (step 73 counts them only then).</summary>
        public bool ForPlayer { get; }

        /// <summary>LineBlockHoldEnabled - the block hold itself.</summary>
        public bool Enabled { get; }

        /// <summary>LineBlockSlackMetres: how far past the block's half-width (and its rear) a man may stand before he is pulled back - at full hold.</summary>
        public double SlackMetres { get; }

        /// <summary>LineBlockFullAtDepth: the minimum depth D at which the hold is full; from D 1 (free) it grows in a straight line to here.</summary>
        public double FullAtDepth { get; }

        /// <summary>LineBlockLooseMetres: the extra slack of a block that is only just held (D a hair above 1) - x (1 − the hold).</summary>
        public double LooseMetres { get; }

        /// <summary>LineBlockRankMetres: one rank's depth - the block is ceil(men ÷ files) ranks of this deep.</summary>
        public double RankMetres { get; }

        /// <summary>LineBlockAheadMetres: how far ahead of the block's front a man may run with no enemy in reach (at full hold).</summary>
        public double AheadMetres { get; }

        /// <summary>LineBlockStrikeReachMetres: a man with an enemy this close is free to fight (0 = never).</summary>
        public double StrikeReachMetres { get; }

        /// <summary>LineBlockStrikeLeashMetres: ... while he is no more than this beyond the block's allowance.</summary>
        public double StrikeLeashMetres { get; }

        /// <summary>LineBlockNearMetres: an advance / move / hold is held once the enemy's front is this close (a charge always).</summary>
        public double NearMetres { get; }

        /// <summary>LineBlockLeadMetres: a pulled man's place leads the advancing block by this much, so he keeps pace with it.</summary>
        public double LeadMetres { get; }

        /// <summary>LineBlockMopUpWinPercent: the block lets go when its men win this much (step 69's win, the unit's mean) - the enemy beaten.</summary>
        public double MopUpWinPercent { get; }

        /// <summary>The master switch FIRST, step 73's system, then the hold's own switch.</summary>
        public bool On => ModEnabled && Lines && Enabled;

        /// <summary>The player's formations: on AND his line-discipline switch (step 73 counts his formations only then).</summary>
        public bool PlayerOn => On && ForPlayer;

        public string? OffBecause => !ModEnabled ? "ModEnabled" : !Lines ? "LineDisciplineEnabled" : !Enabled ? "LineBlockHoldEnabled" : null;

        public bool Same(in BlockHoldRules o) =>
            ModEnabled == o.ModEnabled && Lines == o.Lines && ForPlayer == o.ForPlayer && Enabled == o.Enabled && SlackMetres == o.SlackMetres
            && FullAtDepth == o.FullAtDepth && LooseMetres == o.LooseMetres && RankMetres == o.RankMetres && AheadMetres == o.AheadMetres
            && StrikeReachMetres == o.StrikeReachMetres && StrikeLeashMetres == o.StrikeLeashMetres && NearMetres == o.NearMetres
            && LeadMetres == o.LeadMetres && MopUpWinPercent == o.MopUpWinPercent;

        public string Describe()
        {
            string what = "the block hold (step 74): a formation of low line discipline (step 73's D above 1) keeps its BLOCK in a charge and in the last "
                          + F(NearMetres) + " m of an advance - each man within the block's width (step 73's cap: max files x its spacing) + "
                          + F(SlackMetres) + " m across its front and within its depth (ceil(men / files) ranks of " + F(RankMetres) + " m, + "
                          + F(SlackMetres) + " m behind, " + F(AheadMetres) + " m ahead) around its centre line toward the enemy; a man outside is walked back by "
                          + "his own input, facing the enemy, his place leading the block by " + F(LeadMetres) + " m; the hold full from D " + F(FullAtDepth)
                          + ", looser below (+ " + F(LooseMetres) + " m at D 1), elites (D 1) free; a man with an enemy within " + F(StrikeReachMetres)
                          + " m fights (up to " + F(StrikeLeashMetres) + " m beyond the allowance); the block lets go when its men win "
                          + F(MopUpWinPercent) + "%; the fear flock's band sits inside it; both sides, your men by \"Line discipline: your units too\"";
            if (!On) return what + " - OFF (" + OffBecause + "): the game's own charge";
            return what + " - ON";
        }

        private static double Clamp(double v, double lo, double hi) => double.IsNaN(v) ? lo : v < lo ? lo : v > hi ? hi : v;

        private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>Step 74: why a man is pulled back into his block.</summary>
    public enum BlockPull
    {
        /// <summary>Inside the block (or free) - his own AI.</summary>
        None,

        /// <summary>Beyond the block's half-width + the slack across its front: round the flank, in a trail.</summary>
        Lateral,

        /// <summary>Ahead of the block's front with no enemy in reach: running ahead of it.</summary>
        Ahead,

        /// <summary>Behind the block's rear: straggling.</summary>
        Behind,

        Count,
    }

    /// <summary>Step 74: what one look at a man of a held formation found (the summary's "free" columns).</summary>
    public enum BlockLookKind
    {
        /// <summary>In range, inside the block - his own AI.</summary>
        Inside,

        /// <summary>Pulled back (his input).</summary>
        Pulled,

        /// <summary>An enemy within reach - free to fight.</summary>
        Striking,

        /// <summary>His formation is not held by step 73 (cavalry, a square, the switches, not counted yet).</summary>
        NotCounted,

        /// <summary>D at or below 1 - an elite formation (its cap is its whole count).</summary>
        Elite,

        /// <summary>An advance / move / hold with the enemy farther than LineBlockNearMetres.</summary>
        Far,

        /// <summary>A retreat order, or a wall (the wall holds its own).</summary>
        Retreat,

        /// <summary>The block's men win - the enemy beaten (LineBlockMopUpWinPercent).</summary>
        MopUp,

        /// <summary>Courage's walk back or the fear flock steers him (they go first; both lie inside the block).</summary>
        Yielded,

        /// <summary>The block's frame not known yet (its first half second).</summary>
        NoFrame,

        Count,
    }

    /// <summary>Step 74: why a pull ended.</summary>
    public enum BlockEnd
    {
        BackInside,
        Striking,
        Released,
        Yielded,
        SwitchedOff,
        Excluded,
        LeftField,
        MissionEnd,
        Error,
        Count,
    }

    /// <summary>
    /// Step 74's pure rule. A formation step 73 holds to D ranks (cap F files, width W at its spacing) keeps the BLOCK it marches in: in its frame
    /// (u toward the enemy, e across), its centre line L0 = the MEDIAN of its men's lateral places (a trail does not drag it), its front and rear
    /// around the median along u (ceil(men ÷ F) ranks of LineBlockRankMetres). A man is pulled back when he stands beyond W / 2 + the slack across
    /// it, beyond the front + the ahead allowance with no enemy in reach, or behind the rear + the slack. The hold scales with D: s = (D − 1) ÷
    /// (LineBlockFullAtDepth − 1) clamped 0-1, the slack + (1 − s) x LineBlockLooseMetres; D at or below 1 = free (an elite's cap is its whole
    /// count). A man with an enemy within the strike reach is free while he stands no more than the leash beyond the allowance.
    /// </summary>
    public static class BlockHoldMath
    {
        /// <summary>LineBlockFullAtDepth's floor (the hold must have a span from D 1).</summary>
        public const double MinFullAtDepth = 1.5;

        /// <summary>D at or below this = free (plumbing: a hair above 1 for the rounding of a mean of ones).</summary>
        public const double FreeDepth = 1.0001;

        /// <summary>A pulled man is let go only this far inside the allowance (no tug at the edge - plumbing).</summary>
        public const double BackInsideMetres = 1.0;

        /// <summary>The block's own speed below which it counts as standing (no lead) - m/s (plumbing).</summary>
        public const double MovingSpeed = 0.3;

        /// <summary>The block's speed is never extrapolated beyond this (m/s - a sprint; plumbing).</summary>
        public const double MaxSpeed = 8;

        /// <summary>How long a published frame stays good (s).</summary>
        public const double StaleSeconds = 2.0;

        /// <summary>The unit table's period (s) - every man is looked at once in it (courage's look).</summary>
        public const double PublishSeconds = 0.5;

        /// <summary>The most men one unit's period keeps (more are measured but not sampled).</summary>
        public const int MaxSamples = 512;

        /// <summary>The fewest men for a block frame (a handful is no block).</summary>
        public const int MinMen = 6;

        /// <summary>The diagnostic: a man farther than this from his arrangement slot is "out of his slot" (m).</summary>
        public const double OutOfSlotMetres = 3.0;

        /// <summary>The diagnostic: a man with an enemy this close is in CONTACT (m) - the unit's first contact = its first such look.</summary>
        public const double ContactMetres = 3.0;

        /// <summary>The hold's strength by D: 0 at D 1 (free), 1 from LineBlockFullAtDepth.</summary>
        public static double Strength(in BlockHoldRules r, double depth)
        {
            if (double.IsNaN(depth) || depth <= FreeDepth) return 0;
            double s = (depth - 1) / (r.FullAtDepth - 1);
            return s < 0 ? 0 : s > 1 ? 1 : s;
        }

        /// <summary>The extra room of a block held at strength <paramref name="s"/> (the looser, the more).</summary>
        public static double Loose(in BlockHoldRules r, double s) => (1 - FearMath.Clamp01(s)) * r.LooseMetres;

        /// <summary>The block's ranks: ceil(men ÷ its cap files).</summary>
        public static int Ranks(int men, int capFiles) => men <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(men / (double)Math.Max(1, capFiles) - 1e-9));

        /// <summary>The block's depth (m): its ranks x LineBlockRankMetres.</summary>
        public static double Depth(in BlockHoldRules r, int men, int capFiles) => Ranks(men, capFiles) * r.RankMetres;

        /// <summary>The lateral allowance: W ÷ 2 + the slack + the looseness.</summary>
        public static double Allowance(in BlockHoldRules r, double halfWidth, double s) => Math.Max(0, halfWidth) + r.SlackMetres + Loose(in r, s);

        /// <summary>
        /// One man in the block's frame: <paramref name="lateral"/> = his place across it from the centre line (m), <paramref name="along"/> = along
        /// u from the block's median (m), <paramref name="enemy"/> = his nearest enemy (m; NaN = none in the scan). <paramref name="pulling"/> = he is
        /// being pulled now (the hysteresis: let go only <see cref="BackInsideMetres"/> inside). Returns why he is pulled (None = his own AI) and
        /// his place (<paramref name="toLateral"/>, <paramref name="toAlong"/> in the same frame); <paramref name="striking"/> = free to fight.
        /// </summary>
        public static BlockPull Decide(in BlockHoldRules r, double s, double halfWidth, double depth, double lateral, double along, double enemy, bool pulling,
            out double toLateral, out double toAlong, out bool striking)
        {
            double side = Allowance(in r, halfWidth, s);
            return DecideRange(in r, s, -side, side, depth, lateral, along, enemy, pulling, out toLateral, out toAlong, out striking);
        }

        /// <summary>
        /// Step 77: <see cref="Decide"/> with the lateral allowance as a RANGE (<paramref name="latLo"/> .. <paramref name="latHi"/> across the block's
        /// frame, the slack and the looseness included) - in contact press, don't pass cuts it to the enemy line's ends. A symmetric range = step 74
        /// exactly.
        /// </summary>
        public static BlockPull DecideRange(in BlockHoldRules r, double s, double latLo, double latHi, double depth, double lateral, double along, double enemy, bool pulling,
            out double toLateral, out double toAlong, out bool striking)
        {
            toLateral = lateral;
            toAlong = along;
            striking = false;
            double loose = Loose(in r, s);
            double front = depth / 2 + r.AheadMetres + loose;
            double rear = -depth / 2 - r.SlackMetres - loose;
            double back = pulling ? BackInsideMetres : 0;
            double latOut = Math.Max((latLo + back) - lateral, lateral - (latHi - back));
            double aheadOut = along - (front - back);
            double behindOut = (rear + back) - along;
            if (latOut <= 0 && aheadOut <= 0 && behindOut <= 0) return BlockPull.None;
            // an enemy within reach: he fights - unless he stands more than the leash beyond the allowance (round the flank, behind the enemy)
            if (r.StrikeReachMetres > 0 && !double.IsNaN(enemy) && enemy <= r.StrikeReachMetres
                && Math.Max(latLo - lateral, lateral - latHi) <= r.StrikeLeashMetres && along - front <= r.StrikeLeashMetres)
            {
                striking = true;
                return BlockPull.None;
            }
            double inLo = latLo + BackInsideMetres, inHi = latHi - BackInsideMetres;
            if (inLo > inHi)
            {
                double mid = 0.5 * (latLo + latHi);
                inLo = inHi = mid;
            }
            if (toLateral > inHi) toLateral = inHi;
            else if (toLateral < inLo) toLateral = inLo;
            double hi = Math.Max(rear + BackInsideMetres, front - BackInsideMetres);
            double lo = Math.Min(hi, rear + BackInsideMetres);
            if (toAlong > hi) toAlong = hi;
            else if (toAlong < lo) toAlong = lo;
            if (latOut > 0) return BlockPull.Lateral;
            return aheadOut > 0 ? BlockPull.Ahead : BlockPull.Behind;
        }

        /// <summary>The lead a pulled man's place gets along the block's advance (m): LineBlockLeadMetres while it moves forward, else 0.</summary>
        public static double Lead(in BlockHoldRules r, double speed) => !double.IsNaN(speed) && speed > MovingSpeed ? r.LeadMetres : 0;

        /// <summary>The block's forward speed for the extrapolation (m/s), clamped 0 to <see cref="MaxSpeed"/>.</summary>
        public static double ClampSpeed(double v) => double.IsNaN(v) || v < 0 ? 0 : v > MaxSpeed ? MaxSpeed : v;

        /// <summary>The q-quantile of the first <paramref name="n"/> SORTED values (linear between neighbours; NaN for none).</summary>
        public static double Quantile(double[] sorted, int n, double q)
        {
            if (n <= 0) return double.NaN;
            if (n == 1) return sorted[0];
            double pos = q * (n - 1);
            int i = (int)Math.Floor(pos);
            if (i >= n - 1) return sorted[n - 1];
            double f = pos - i;
            return sorted[i] + (sorted[i + 1] - sorted[i]) * f;
        }

        public static string PullName(BlockPull p) => p switch
        {
            BlockPull.Lateral => "beyond the block's width (round the flank)",
            BlockPull.Ahead => "ahead of its front",
            BlockPull.Behind => "behind its rear",
            _ => "inside",
        };

        public static string EndName(BlockEnd e) => e switch
        {
            BlockEnd.BackInside => "back inside",
            BlockEnd.Striking => "let go to strike",
            BlockEnd.Released => "released (elite / far / a retreat / the mop-up / not counted)",
            BlockEnd.Yielded => "courage's walk / the flock took him",
            BlockEnd.SwitchedOff => "switched off",
            BlockEnd.Excluded => "no longer looked at",
            BlockEnd.LeftField => "left the field",
            BlockEnd.MissionEnd => "mission end",
            _ => "error",
        };
    }

    /// <summary>Step 74: one man's block-hold state (the module's TrackedAgent carries one).</summary>
    public sealed class BlockMind
    {
        /// <summary>His input wish is on (he is being walked back).</summary>
        public bool Wish;

        /// <summary>Why (the last look).</summary>
        public BlockPull Kind;

        /// <summary>The unit he is held in (FearMath.UnitKey) and his place in its frame - across from the centre line, along from its median.</summary>
        public int Key = -1;
        public double ToLateral, ToAlong;

        /// <summary>Since when he is pulled (mission time; NaN = not).</summary>
        public double Since = double.NaN;

        public double NextSteer, NextEdge;
        public bool Moving;

        /// <summary>He was ever pulled (the summary's men).</summary>
        public bool EverPulled;

        /// <summary>Let go to strike at the last look.</summary>
        public bool Striking;

        public void End()
        {
            Wish = false;
            Moving = false;
            Kind = BlockPull.None;
            Since = double.NaN;
        }
    }

    /// <summary>
    /// Step 74's UNIT TABLE (team x formation, FearMath.UnitKey): every look notes the man (where he stands, his slot distance, his nearest enemy),
    /// the module adds the formation's step-73 cap and (once a period) its order / behaviour / arrangement; every <see cref="BlockHoldMath.PublishSeconds"/>
    /// the frame is published - u (the fear band's direction toward the enemy when live, else the formation's facing), the MEDIAN lateral (the centre
    /// line) and along, the 10th / 90th percentiles across and along (the spread), the block's speed, near the enemy or not - and the diagnostic
    /// samples it. Arrays allocated once per unit and battle (the first time it is seen).
    /// </summary>
    public sealed class BlockUnits
    {
        private const int N = FearMath.UnitCount;

        // this period
        private readonly int[] _n = new int[N];
        private readonly double[]?[] _px = new double[N][], _py = new double[N][];
        private readonly double[]?[] _lat = new double[N][], _alg = new double[N][];
        private readonly int[] _slotN = new int[N], _outN = new int[N], _contactN = new int[N];
        private readonly double[] _fx = new double[N], _fy = new double[N];
        private readonly double[] _nearest = new double[N];

        // the cap (step 73) and the formation's reads - kept between periods
        private readonly double[] _capAt = new double[N], _formAt = new double[N];

        // published
        public readonly bool[] Valid = new bool[N];
        public readonly double[] PublishedAt = new double[N];
        public readonly int[] Men = new int[N];
        public readonly double[] UX = new double[N], UY = new double[N];
        public readonly double[] CentreLat = new double[N], MedianAlong = new double[N];
        public readonly double[] LatP10 = new double[N], LatP90 = new double[N], LatMin = new double[N], LatMax = new double[N];
        public readonly double[] AlongP10 = new double[N], AlongP90 = new double[N];
        public readonly double[] Speed = new double[N];
        public readonly double[] OutOfSlot = new double[N];
        public readonly bool[] InContact = new bool[N];
        public readonly double[] FirstContact = new double[N];

        /// <summary>The block's centre point in the world (the median lateral at the median along) - the fear band's clamp reads it.</summary>
        public readonly double[] CentreX = new double[N], CentreY = new double[N];

        /// <summary>The step-73 cap: known, D, files, width (m), men; side and who orders it.</summary>
        public readonly bool[] Counted = new bool[N];
        public readonly double[] Depth = new double[N], CapWidth = new double[N];
        public readonly int[] CapFiles = new int[N], CapMen = new int[N];
        public readonly bool[] PlayerSide = new bool[N], Ai = new bool[N];

        /// <summary>The formation's movement order / active behaviour / arrangement / the game's width (the diagnostic's reads).</summary>
        public readonly string[] Order = new string[N], Behaviour = new string[N], Arrangement = new string[N];
        public readonly double[] GameWidth = new double[N];

        /// <summary>The enemy's front is within LineBlockNearMetres of the block's front (or its men are in contact).</summary>
        public readonly bool[] Near = new bool[N];

        private double _nextPublish = double.NaN, _lastPublish = double.NaN;

        public BlockUnits()
        {
            Reset();
        }

        public void Reset()
        {
            for (int k = 0; k < N; k++)
            {
                ClearPeriod(k);
                Valid[k] = Counted[k] = InContact[k] = Near[k] = PlayerSide[k] = Ai[k] = false;
                PublishedAt[k] = _capAt[k] = _formAt[k] = FirstContact[k] = double.NaN;
                Men[k] = CapFiles[k] = CapMen[k] = 0;
                Depth[k] = CapWidth[k] = GameWidth[k] = Speed[k] = OutOfSlot[k] = double.NaN;
                Order[k] = Behaviour[k] = Arrangement[k] = "-";
                _fx[k] = _fy[k] = 0;
            }
            _nextPublish = _lastPublish = double.NaN;
        }

        private void ClearPeriod(int k)
        {
            _n[k] = _slotN[k] = _outN[k] = _contactN[k] = 0;
            _nearest[k] = double.NaN;
        }

        public bool Live(int key, double now) =>
            key >= 0 && key < N && Valid[key] && !double.IsNaN(PublishedAt[key]) && now - PublishedAt[key] <= BlockHoldMath.StaleSeconds && now >= PublishedAt[key] - 1e-9;

        /// <summary>The cap is due a read for this unit (once a period).</summary>
        public bool CapDue(int key, double now) => key >= 0 && key < N && (double.IsNaN(_capAt[key]) || now - _capAt[key] >= BlockHoldMath.PublishSeconds || now < _capAt[key]);

        /// <summary>The formation's diagnostic reads are due (once a period).</summary>
        public bool FormationDue(int key, double now) => key >= 0 && key < N && (double.IsNaN(_formAt[key]) || now - _formAt[key] >= BlockHoldMath.PublishSeconds || now < _formAt[key]);

        /// <summary>The step-73 cap of the unit (<paramref name="counted"/> false = step 73 does not hold it).</summary>
        public void NoteCap(int key, double now, bool counted, double depth, int capFiles, double capWidth, int men, bool playerSide, bool ai)
        {
            if (key < 0 || key >= N) return;
            _capAt[key] = now;
            Counted[key] = counted && capFiles > 0 && !double.IsNaN(capWidth) && capWidth > 0;
            Depth[key] = depth;
            CapFiles[key] = capFiles;
            CapWidth[key] = capWidth;
            CapMen[key] = men;
            PlayerSide[key] = playerSide;
            Ai[key] = ai;
        }

        /// <summary>The formation's movement order / active behaviour / arrangement names and the game's own width (the diagnostic).</summary>
        public void NoteFormation(int key, double now, string order, string behaviour, string arrangement, double gameWidth)
        {
            if (key < 0 || key >= N) return;
            _formAt[key] = now;
            Order[key] = order ?? "-";
            Behaviour[key] = behaviour ?? "-";
            Arrangement[key] = arrangement ?? "-";
            GameWidth[key] = gameWidth;
        }

        /// <summary>One man of the unit at this look: where he stands, the formation's facing (fallback frame), his distance from his arrangement
        /// slot (NaN = unknown) and his nearest enemy (m; NaN = none in the scan).</summary>
        public void Note(int key, double px, double py, double fx, double fy, double slotDistance, double enemy)
        {
            if (key < 0 || key >= N || double.IsNaN(px) || double.IsNaN(py)) return;
            if (fx * fx + fy * fy > 1e-6)
            {
                _fx[key] = fx;
                _fy[key] = fy;
            }
            if (!double.IsNaN(slotDistance))
            {
                _slotN[key]++;
                if (slotDistance > BlockHoldMath.OutOfSlotMetres) _outN[key]++;
            }
            if (!double.IsNaN(enemy))
            {
                if (enemy <= BlockHoldMath.ContactMetres) _contactN[key]++;
                if (double.IsNaN(_nearest[key]) || enemy < _nearest[key]) _nearest[key] = enemy;
            }
            int s = _n[key];
            if (s >= BlockHoldMath.MaxSamples) return;
            var xs = _px[key];
            if (xs == null)
            {
                xs = _px[key] = new double[BlockHoldMath.MaxSamples]; // once per unit and battle
                _py[key] = new double[BlockHoldMath.MaxSamples];
                _lat[key] = new double[BlockHoldMath.MaxSamples];
                _alg[key] = new double[BlockHoldMath.MaxSamples];
            }
            xs[s] = px;
            _py[key]![s] = py;
            _n[key] = s + 1;
        }

        public bool Due(double now)
        {
            if (double.IsNaN(_nextPublish))
            {
                _nextPublish = now + BlockHoldMath.PublishSeconds;
                return false;
            }
            return now >= _nextPublish || now < _nextPublish - 10 * BlockHoldMath.PublishSeconds;
        }

        /// <summary>
        /// Publishes every unit with men this period: u = the fear band's direction toward the enemy (<paramref name="fear"/>, live) else the
        /// formation's facing; the median / 10th / 90th percentiles across and along; the speed from the last median; near the enemy (the band's
        /// enemy front within LineBlockNearMetres of the block's front, or a man of it with an enemy in the scan within it); the first contact.
        /// Returns the keys published into <paramref name="published"/> (count).
        /// </summary>
        public int Publish(double now, in BlockHoldRules r, FearUnits? fear, int[] published)
        {
            double dt = double.IsNaN(_lastPublish) || now <= _lastPublish ? BlockHoldMath.PublishSeconds : now - _lastPublish;
            _lastPublish = now;
            _nextPublish = now + BlockHoldMath.PublishSeconds;
            int count = 0;
            for (int k = 0; k < N; k++)
            {
                int n = _n[k];
                if (n <= 0) continue;
                var xs = _px[k];
                var ys = _py[k];
                var lat = _lat[k];
                var alg = _alg[k];
                if (xs == null || ys == null || lat == null || alg == null || n < BlockHoldMath.MinMen)
                {
                    Valid[k] = false;
                    ClearPeriod(k);
                    continue;
                }
                double ux = 0, uy = 0;
                if (fear != null && fear.BandLive(k, now))
                {
                    ux = fear.BandUX[k];
                    uy = fear.BandUY[k];
                }
                if (ux * ux + uy * uy < 1e-6)
                {
                    ux = _fx[k];
                    uy = _fy[k];
                }
                double len = Math.Sqrt(ux * ux + uy * uy);
                if (!(len > 1e-6))
                {
                    Valid[k] = false;
                    ClearPeriod(k);
                    continue;
                }
                ux /= len;
                uy /= len;
                double ex = -uy, ey = ux;
                for (int i = 0; i < n; i++)
                {
                    lat[i] = xs[i] * ex + ys[i] * ey;
                    alg[i] = xs[i] * ux + ys[i] * uy;
                }
                Array.Sort(lat, 0, n);
                Array.Sort(alg, 0, n);
                double l50 = BlockHoldMath.Quantile(lat, n, 0.5), a50 = BlockHoldMath.Quantile(alg, n, 0.5);
                double cx = l50 * ex + a50 * ux, cy = l50 * ey + a50 * uy;
                bool had = Live(k, now);
                double speed = 0;
                if (had && dt > 1e-3 && dt <= BlockHoldMath.StaleSeconds) speed = ((cx - CentreX[k]) * ux + (cy - CentreY[k]) * uy) / dt;
                Valid[k] = true;
                Men[k] = n;
                UX[k] = ux;
                UY[k] = uy;
                CentreLat[k] = l50;
                MedianAlong[k] = a50;
                LatP10[k] = BlockHoldMath.Quantile(lat, n, 0.1);
                LatP90[k] = BlockHoldMath.Quantile(lat, n, 0.9);
                LatMin[k] = lat[0];
                LatMax[k] = lat[n - 1];
                AlongP10[k] = BlockHoldMath.Quantile(alg, n, 0.1);
                AlongP90[k] = BlockHoldMath.Quantile(alg, n, 0.9);
                Speed[k] = BlockHoldMath.ClampSpeed(speed);
                CentreX[k] = cx;
                CentreY[k] = cy;
                OutOfSlot[k] = _slotN[k] > 0 ? _outN[k] / (double)_slotN[k] : double.NaN;
                InContact[k] = _contactN[k] > 0;
                if (InContact[k] && double.IsNaN(FirstContact[k])) FirstContact[k] = now;
                // near: the enemy front (the band's frame, when it is ours) within the near distance of the block's front, or a man with an enemy that close
                bool near = !double.IsNaN(_nearest[k]) && _nearest[k] <= r.NearMetres;
                if (!near && fear != null && fear.BandLive(k, now) && !double.IsNaN(fear.EnemyFrontAlong[k]))
                {
                    double front = AlongP90[k];
                    near = fear.EnemyFrontAlong[k] - front <= r.NearMetres;
                }
                Near[k] = near;
                PublishedAt[k] = now;
                if (count < published.Length) published[count++] = k;
                ClearPeriod(k);
            }
            return count;
        }

        /// <summary>A man's place in the unit's published frame now: across from the centre line, along from the median (extrapolated by the
        /// block's speed since the publish).</summary>
        public void Frame(int key, double now, double px, double py, out double lateral, out double along)
        {
            double ux = UX[key], uy = UY[key], ex = -uy, ey = ux;
            double drift = Speed[key] * Math.Max(0, now - PublishedAt[key]);
            lateral = px * ex + py * ey - CentreLat[key];
            along = px * ux + py * uy - (MedianAlong[key] + drift);
        }

        /// <summary>The world point of a place in the unit's frame now (the median extrapolated by the block's speed since the publish).</summary>
        public void World(int key, double now, double lateral, double along, out double x, out double y)
        {
            double ux = UX[key], uy = UY[key], ex = -uy, ey = ux;
            double a = MedianAlong[key] + Speed[key] * Math.Max(0, now - PublishedAt[key]) + along;
            double l = CentreLat[key] + lateral;
            x = l * ex + a * ux;
            y = l * ey + a * uy;
        }
    }

    /// <summary>Step 74 per side (yours / the other): the measurement (ON and OFF) and the hold's counts.</summary>
    public sealed class BlockSideStats
    {
        public const int RatioBins = 80;
        public const double RatioBin = 0.05;

        // [on 1 / off 0, after contact 1 / before 0]
        public readonly long[,] Looks = new long[2, 2];
        public readonly double[,] OffsetSum = new double[2, 2];
        public readonly double[,] HalfSum = new double[2, 2];
        public readonly long[,] Beyond = new long[2, 2];
        public readonly long[,,] RatioHist = new long[2, 2, RatioBins + 1];
        public readonly double[,] OffsetMax = new double[2, 2];

        public readonly long[] LookKinds = new long[(int)BlockLookKind.Count];
        public readonly long[] Pulls = new long[(int)BlockPull.Count];
        public readonly long[] Ends = new long[(int)BlockEnd.Count];
        public long Men, PulledMen;
        public double PulledSeconds;

        public void AddOffset(bool on, bool after, double offset, double halfWidth, double allowance)
        {
            int o = on ? 1 : 0, a = after ? 1 : 0;
            double off = Math.Abs(offset);
            Looks[o, a]++;
            OffsetSum[o, a] += off;
            HalfSum[o, a] += halfWidth;
            if (off > allowance) Beyond[o, a]++;
            if (off > OffsetMax[o, a]) OffsetMax[o, a] = off;
            double ratio = halfWidth > 1e-6 ? off / halfWidth : RatioBins * RatioBin;
            int b = (int)Math.Floor(ratio / RatioBin);
            if (b < 0) b = 0;
            if (b > RatioBins) b = RatioBins;
            RatioHist[o, a, b]++;
        }

        public double MeanOffset(bool on, bool after) => Looks[on ? 1 : 0, after ? 1 : 0] > 0 ? OffsetSum[on ? 1 : 0, after ? 1 : 0] / Looks[on ? 1 : 0, after ? 1 : 0] : double.NaN;

        public double MeanHalf(bool on, bool after) => Looks[on ? 1 : 0, after ? 1 : 0] > 0 ? HalfSum[on ? 1 : 0, after ? 1 : 0] / Looks[on ? 1 : 0, after ? 1 : 0] : double.NaN;

        public double BeyondShare(bool on, bool after) => Looks[on ? 1 : 0, after ? 1 : 0] > 0 ? Beyond[on ? 1 : 0, after ? 1 : 0] / (double)Looks[on ? 1 : 0, after ? 1 : 0] : double.NaN;

        /// <summary>The 90th percentile of |offset| ÷ the half-width (the upper edge of its bin).</summary>
        public double P90Ratio(bool on, bool after)
        {
            int o = on ? 1 : 0, a = after ? 1 : 0;
            long total = Looks[o, a];
            if (total <= 0) return double.NaN;
            long want = (long)Math.Ceiling(0.9 * total), run = 0;
            for (int b = 0; b <= RatioBins; b++)
            {
                run += RatioHist[o, a, b];
                if (run >= want) return (b + 1) * RatioBin;
            }
            return (RatioBins + 1) * RatioBin;
        }
    }

    /// <summary>Step 74 per mission: both sides' hold stats, the fear band's clamps, the steering and the cost - and the [summary] "block hold" lines.</summary>
    public sealed class BlockHoldStats
    {
        public readonly BlockSideStats Yours = new BlockSideStats(), Theirs = new BlockSideStats();
        public long Ticks, Publishes, SteerUpdates, StandUpdates, Errors;
        public double LookCostMs, SteerCostMs, PublishCostMs;
        public long BandClamps;

        public BlockSideStats Side(bool playerSide) => playerSide ? Yours : Theirs;

        public void AddSummaryLines(List<string> lines, in BlockHoldRules r)
        {
            lines.Add("block hold (step 74): " + (r.On ? "ON" : "OFF (" + r.OffBecause + ")") + " at the end - a block of D above 1 kept within step 73's width + "
                      + F1(r.SlackMetres) + " m (full from D " + F1(r.FullAtDepth) + ", + " + F1(r.LooseMetres) + " m at D 1), its depth ranks of " + F1(r.RankMetres)
                      + " m (+ " + F1(r.AheadMetres) + " m ahead), a charge always / an advance within " + F1(r.NearMetres) + " m; strikers within "
                      + F1(r.StrikeReachMetres) + " m free (leash " + F1(r.StrikeLeashMetres) + " m), the mop-up at " + F0(r.MopUpWinPercent) + "%; the fear band clamped to the block "
                      + BandClamps + " times; steering " + SteerUpdates + " walking, " + StandUpdates + " standing; publishes " + Publishes + "; cost "
                      + F1(LookCostMs) + " ms looks + " + F1(SteerCostMs) + " ms steering + " + F1(PublishCostMs) + " ms frames ("
                      + (Ticks > 0 ? ((LookCostMs + SteerCostMs + PublishCostMs) / Ticks).ToString("0.000", CultureInfo.InvariantCulture) : "-") + " ms a tick); errors " + Errors);
            AddSide(lines, "your side", Yours);
            AddSide(lines, "the other side", Theirs);
        }

        private static void AddSide(List<string> lines, string name, BlockSideStats s)
        {
            var sb = new StringBuilder();
            sb.Append("block hold (step 74), ").Append(name).Append(" - each man's lateral offset from his block's centre line vs its half-width (step 73's cap ÷ 2), men of a block in range: ");
            sb.Append("ON before contact ").Append(Offset(s, true, false)).Append(" | ON after contact ").Append(Offset(s, true, true));
            sb.Append(" | OFF before ").Append(Offset(s, false, false)).Append(" | OFF after ").Append(Offset(s, false, true));
            sb.Append("; PULLED IN ").Append(s.Pulls[(int)BlockPull.Lateral] + s.Pulls[(int)BlockPull.Ahead] + s.Pulls[(int)BlockPull.Behind]).Append(" times by ").Append(s.PulledMen)
              .Append(" men (round the flank ").Append(s.Pulls[(int)BlockPull.Lateral]).Append(", ahead of the front ").Append(s.Pulls[(int)BlockPull.Ahead])
              .Append(", behind the rear ").Append(s.Pulls[(int)BlockPull.Behind]).Append("), ").Append(F1(s.PulledSeconds)).Append(" man-s; ends:");
            for (int e = 0; e < (int)BlockEnd.Count; e++)
            {
                if (s.Ends[e] == 0) continue;
                sb.Append(' ').Append(BlockHoldMath.EndName((BlockEnd)e)).Append(' ').Append(s.Ends[e]).Append(',');
            }
            if (sb[sb.Length - 1] == ',') sb.Length--;
            sb.Append("; looks: inside ").Append(s.LookKinds[(int)BlockLookKind.Inside]).Append(", pulled ").Append(s.LookKinds[(int)BlockLookKind.Pulled])
              .Append(", striking (free) ").Append(s.LookKinds[(int)BlockLookKind.Striking]).Append(", elites D 1 (free) ").Append(s.LookKinds[(int)BlockLookKind.Elite])
              .Append(", not held by step 73 ").Append(s.LookKinds[(int)BlockLookKind.NotCounted]).Append(", the enemy far ").Append(s.LookKinds[(int)BlockLookKind.Far])
              .Append(", a retreat / a wall ").Append(s.LookKinds[(int)BlockLookKind.Retreat]).Append(", the mop-up ").Append(s.LookKinds[(int)BlockLookKind.MopUp])
              .Append(", courage's walk / the flock first ").Append(s.LookKinds[(int)BlockLookKind.Yielded]).Append(", no frame yet ").Append(s.LookKinds[(int)BlockLookKind.NoFrame]);
            lines.Add(sb.ToString());
        }

        private static string Offset(BlockSideStats s, bool on, bool after)
        {
            long n = s.Looks[on ? 1 : 0, after ? 1 : 0];
            if (n == 0) return "- (no looks)";
            return "mean " + F1(s.MeanOffset(on, after)) + " m of " + F1(s.MeanHalf(on, after)) + " m, p90 " + s.P90Ratio(on, after).ToString("0.00", CultureInfo.InvariantCulture)
                   + " x the half-width, the farthest " + F1(s.OffsetMax[on ? 1 : 0, after ? 1 : 0]) + " m, beyond the allowance "
                   + (100 * s.BeyondShare(on, after)).ToString("0.0", CultureInfo.InvariantCulture) + "% (" + n + " looks)";
        }

        internal static string F1(double v) => double.IsNaN(v) ? "-" : v.ToString("0.0", CultureInfo.InvariantCulture);

        internal static string F0(double v) => double.IsNaN(v) ? "-" : v.ToString("0", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Step 74 - THE BLOCK DIAGNOSTIC (measurement only): per formation step 73 counts (both sides - the brief: "per counted AI formation"; yours are
    /// marked), every frame publish (0.5 s) one sample - its movement order, its active behaviour, its arrangement, the spread of its men (across:
    /// the 10th-90th percentile width and the whole width; along: the 10th-90th depth), the game's own width and step 73's cap, the share of its men
    /// more than 3 m from their arrangement slots, contact - kept in a ring (20 min) and, at the summary, binned by the time from the formation's
    /// FIRST CONTACT (a man of it within 3 m of an enemy). The order / behaviour changes are logged as they happen (the module).
    /// </summary>
    public sealed class BlockShapeStats
    {
        public const int Capacity = 2400;
        public const int MaxUnits = 16;

        /// <summary>The bins by the time from first contact (s): before −20, −20..−10, −10..−5, −5..0, 0..5, 5..10, 10..20, 20..60, 60+.</summary>
        public static readonly double[] BinEdges = { -20, -10, -5, 0, 5, 10, 20, 60 };

        public sealed class Unit
        {
            public int Key;
            public string Name = "-";
            public bool PlayerSide, Ai;
            public double FirstContact = double.NaN;
            public int Count, Next;
            public readonly double[] T = new double[Capacity];
            public readonly double[] W1090 = new double[Capacity], WAll = new double[Capacity], D1090 = new double[Capacity], Out = new double[Capacity];
            public readonly double[] Game = new double[Capacity], Cap = new double[Capacity];
            public readonly byte[] Order = new byte[Capacity], Behaviour = new byte[Capacity], Arrangement = new byte[Capacity];
            public string LastOrder = "", LastBehaviour = "", LastArrangement = "";
            public int Changes;
            public bool ContactLogged;
        }

        private readonly Unit?[] _units = new Unit?[FearMath.UnitCount];
        private readonly List<Unit> _list = new List<Unit>(MaxUnits);
        private readonly List<string> _names = new List<string>(32);
        public long Samples, ChangeLines, ChangeLinesCut;

        public int UnitCount => _list.Count;

        /// <summary>The unit's record (made once - null when the table is full).</summary>
        public Unit? For(int key, string name, bool playerSide, bool ai)
        {
            if (key < 0 || key >= _units.Length) return null;
            var u = _units[key];
            if (u != null) return u;
            if (_list.Count >= MaxUnits) return null;
            u = new Unit { Key = key, Name = name ?? "-", PlayerSide = playerSide, Ai = ai };
            _units[key] = u;
            _list.Add(u);
            return u;
        }

        public Unit? Get(int key) => key >= 0 && key < _units.Length ? _units[key] : null;

        private byte NameIndex(string s)
        {
            for (int i = 0; i < _names.Count; i++)
                if (ReferenceEquals(_names[i], s) || _names[i] == s) return (byte)i;
            if (_names.Count >= 255) return 254;
            _names.Add(s);
            return (byte)(_names.Count - 1);
        }

        public string NameOf(byte i) => i < _names.Count ? _names[i] : "?";

        /// <summary>One sample from the unit table's published frame.</summary>
        public void Add(Unit u, BlockUnits t, int key, double now)
        {
            int i = u.Next;
            u.T[i] = now;
            u.W1090[i] = t.LatP90[key] - t.LatP10[key];
            u.WAll[i] = t.LatMax[key] - t.LatMin[key];
            u.D1090[i] = t.AlongP90[key] - t.AlongP10[key];
            u.Out[i] = t.OutOfSlot[key];
            u.Game[i] = t.GameWidth[key];
            u.Cap[i] = t.CapWidth[key];
            u.Order[i] = NameIndex(t.Order[key]);
            u.Behaviour[i] = NameIndex(t.Behaviour[key]);
            u.Arrangement[i] = NameIndex(t.Arrangement[key]);
            u.Next = (i + 1) % Capacity;
            if (u.Count < Capacity) u.Count++;
            if (double.IsNaN(u.FirstContact) && !double.IsNaN(t.FirstContact[key])) u.FirstContact = t.FirstContact[key];
            Samples++;
        }

        public static int BinOf(double fromContact)
        {
            if (double.IsNaN(fromContact)) return -1;
            for (int b = 0; b < BinEdges.Length; b++)
                if (fromContact < BinEdges[b]) return b;
            return BinEdges.Length;
        }

        public static string BinName(int b)
        {
            if (b == 0) return "before -20 s";
            if (b == BinEdges.Length) return "after +" + BinEdges[BinEdges.Length - 1].ToString("0", CultureInfo.InvariantCulture) + " s";
            return (BinEdges[b - 1] > 0 ? "+" : "") + BinEdges[b - 1].ToString("0", CultureInfo.InvariantCulture) + ".." + (BinEdges[b] > 0 ? "+" : "")
                   + BinEdges[b].ToString("0", CultureInfo.InvariantCulture) + " s";
        }

        /// <summary>The [summary] "block shape" lines: one per formation (the biggest first), binned by the time from its first contact.</summary>
        public void AddSummaryLines(List<string> lines)
        {
            if (_list.Count == 0)
            {
                lines.Add("block shape (step 74 diagnostic): no formation held by step 73 was seen near an enemy this mission");
                return;
            }
            lines.Add("block shape (step 74 diagnostic - per formation step 73 holds, every 0.5 s: its movement order, its behaviour, its arrangement, its men's width "
                      + "across its front (10th-90th percentile / all), depth (10th-90th), the game's width vs step 73's cap, its men more than "
                      + BlockHoldMath.OutOfSlotMetres.ToString("0", CultureInfo.InvariantCulture) + " m from their slots; binned by the time from its FIRST CONTACT - a man within "
                      + BlockHoldMath.ContactMetres.ToString("0", CultureInfo.InvariantCulture) + " m of an enemy): " + _list.Count + " formations, " + Samples + " samples; order / behaviour changes logged "
                      + ChangeLines + (ChangeLinesCut > 0 ? " (+" + ChangeLinesCut + " not logged)" : ""));
            int bins = BinEdges.Length + 1;
            var n = new int[bins];
            var w = new double[bins];
            var wa = new double[bins];
            var d = new double[bins];
            var o = new double[bins];
            var on = new int[bins];
            var g = new double[bins];
            var c = new double[bins];
            var gn = new int[bins];
            foreach (var u in _list)
            {
                Array.Clear(n, 0, bins);
                Array.Clear(w, 0, bins);
                Array.Clear(wa, 0, bins);
                Array.Clear(d, 0, bins);
                Array.Clear(o, 0, bins);
                Array.Clear(on, 0, bins);
                Array.Clear(g, 0, bins);
                Array.Clear(c, 0, bins);
                Array.Clear(gn, 0, bins);
                var orders = new Dictionary<int, Dictionary<string, int>>();
                int start = u.Count < Capacity ? 0 : u.Next;
                for (int j = 0; j < u.Count; j++)
                {
                    int i = (start + j) % Capacity;
                    int b = double.IsNaN(u.FirstContact) ? 0 : BinOf(u.T[i] - u.FirstContact);
                    if (b < 0) b = 0;
                    n[b]++;
                    w[b] += u.W1090[i];
                    wa[b] += u.WAll[i];
                    d[b] += u.D1090[i];
                    if (!double.IsNaN(u.Out[i]))
                    {
                        o[b] += u.Out[i];
                        on[b]++;
                    }
                    if (!double.IsNaN(u.Game[i]) && !double.IsNaN(u.Cap[i]))
                    {
                        g[b] += u.Game[i];
                        c[b] += u.Cap[i];
                        gn[b]++;
                    }
                    if (!orders.TryGetValue(b, out var map)) orders[b] = map = new Dictionary<string, int>();
                    string key = NameOf(u.Order[i]) + " / " + NameOf(u.Behaviour[i]) + " / " + NameOf(u.Arrangement[i]);
                    map[key] = map.TryGetValue(key, out int k) ? k + 1 : 1;
                }
                var sb = new StringBuilder();
                sb.Append("block shape, ").Append(u.Name).Append(" (").Append(u.PlayerSide ? "your side" : "the other side").Append(", ").Append(u.Ai ? "AI" : "yours").Append("): ");
                sb.Append(double.IsNaN(u.FirstContact) ? "NO CONTACT (all samples in the first bin)" : "first contact at " + u.FirstContact.ToString("0.0", CultureInfo.InvariantCulture) + " s");
                for (int b = 0; b < bins; b++)
                {
                    if (n[b] == 0) continue;
                    sb.Append(" | ").Append(double.IsNaN(u.FirstContact) ? "all" : BinName(b)).Append(": ");
                    var map = orders[b];
                    bool first = true;
                    foreach (var kv in map)
                    {
                        if (!first) sb.Append(", ");
                        first = false;
                        sb.Append(kv.Key).Append(' ').Append((100.0 * kv.Value / n[b]).ToString("0", CultureInfo.InvariantCulture)).Append('%');
                    }
                    sb.Append("; width ").Append(F1(w[b] / n[b])).Append(" m (all ").Append(F1(wa[b] / n[b])).Append(" m), depth ").Append(F1(d[b] / n[b])).Append(" m");
                    if (gn[b] > 0) sb.Append(", the game's ").Append(F1(g[b] / gn[b])).Append(" m vs the cap ").Append(F1(c[b] / gn[b])).Append(" m");
                    if (on[b] > 0) sb.Append(", out of slots ").Append((100 * o[b] / on[b]).ToString("0", CultureInfo.InvariantCulture)).Append('%');
                    sb.Append(" (").Append(n[b]).Append(')');
                }
                lines.Add(sb.ToString());
            }
        }

        private static string F1(double v) => double.IsNaN(v) ? "-" : v.ToString("0.0", CultureInfo.InvariantCulture);
    }
}

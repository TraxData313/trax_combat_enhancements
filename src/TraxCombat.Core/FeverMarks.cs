using System;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>Step 44: why one man in a fever gets no mark this frame - or <see cref="None"/> when he does. In the order the view asks.</summary>
    public enum FeverMarkSkip
    {
        /// <summary>Marked.</summary>
        None = 0,

        /// <summary>Not on the field for the camera (no team, not active) - the source could not place him.</summary>
        NotPlaced = 1,

        /// <summary>The enemy's man while his bars are switched off (FeverMarksShowEnemy, and since step 46 AthleticsMarksShowEnemy too).</summary>
        EnemyOff = 2,

        /// <summary>Behind the camera.</summary>
        Behind = 3,

        /// <summary>Closer to the camera than <see cref="FeverMarkMath.NearMetres"/> (vanilla's own icon is gone there too).</summary>
        TooClose = 4,

        /// <summary>Step 58: the near view - farther from the camera than AltNearMarksMetres.</summary>
        TooFar = 5,

        /// <summary>Projected, but outside the screen (checked BEFORE the pick, so he takes no slot from a man on it).</summary>
        OffScreen = 6,

        /// <summary>More candidates on the screen than the cap - the farthest (the every-man view) or the farthest from the screen's centre (the
        /// near view, step 58) wait.</summary>
        Capped = 7,

        Count = 8,
    }

    /// <summary>Step 44: why no fever mark shows at all - in the order the rules are read, the master switch FIRST.</summary>
    public enum FeverMarksHidden
    {
        None = 0,
        ModOff = 1,
        AthleticsOff = 2,
        FeverOff = 3,
        SwitchedOff = 4,
    }

    /// <summary>
    /// One man in a fever as the view sees him in one frame (AI_NOTES "Step 44"): his side, his fever (armed, or burning with the share
    /// left), the mark's world point (his feet + FeverMarkHeightMetres), the camera's distance to it, whether it is in front of the camera,
    /// and - once projected - the point on the screen in pixels.
    /// </summary>
    public struct FeverMark
    {
        /// <summary>His agent index (log lines).</summary>
        public int Key;

        /// <summary><see cref="MarkerTeam"/> as an int (yours / ally / enemy).</summary>
        public int Side;

        /// <summary>Armed (no enemy met yet - the peak) - else burning.</summary>
        public bool Armed;

        /// <summary>The fever left ÷ full, 0..1 (1 while armed).</summary>
        public float Share;

        /// <summary>The mark's world point.</summary>
        public float WorldX;

        public float WorldY;

        public float WorldZ;

        /// <summary>Camera → the mark's point, metres.</summary>
        public float Distance;

        public bool InFront;

        /// <summary>Step 58: the cosine of the angle between the camera's forward and the line to the mark's point (1 = the screen's centre) -
        /// <see cref="AltNearMath.Facing"/>, set by the source with the distance.</summary>
        public float Facing;

        /// <summary>The projected point, screen pixels (valid once projected).</summary>
        public float ScreenX;

        public float ScreenY;

        public FeverMarkSkip Skip;

        // ---- step 46: one mark = one man's bars under ALT - his Athletics bar and / or his fever bar (AI_NOTES "Step 46")

        /// <summary>His fever runs (armed or burning) - read for every man walked; <see cref="Armed"/> / <see cref="Share"/> only mean something then.</summary>
        public bool InFever;

        /// <summary>Riding (his bars go <see cref="AthleticsMarkMath.RiderLiftMetres"/> higher).</summary>
        public bool Mounted;

        /// <summary>His Athletics as the bar draws it: the fill (points ÷ his FULL pool), the usable share (the wound cap), the band by f.</summary>
        public float BarFill;

        public float BarUsable;

        public BarBand Band;

        /// <summary>Set by <see cref="AthleticsMarkMath.Decide"/>: an Athletics bar drawn for him (the switch, his side).</summary>
        public bool HasBar;

        /// <summary>A fever bar drawn for him (in a fever, the switches, his side).</summary>
        public bool HasFever;

        /// <summary>The fever bar's place kept under his Athletics bar (fever bars can show for his side) - so his Athletics bar never jumps.</summary>
        public bool FeverSlot;

        // ---- step 67: his morale bar (the morale eyes)

        /// <summary>His morale as the game keeps it (0-100; −1 = none - tournaments, arenas), read at the formation-stats refresh.</summary>
        public float Morale;

        /// <summary>Set by <see cref="AthleticsMarkMath.Decide(ref FeverMark, in FeverMarkRules, in AthleticsMarkRules, in MoraleMarkRules)"/>: a morale
        /// bar's place at the bottom of his stack (the switch, his side); drawn when he has a morale.</summary>
        public bool HasMorale;
    }

    /// <summary>
    /// Everything one frame of fever marks works on, REUSED (nothing allocated per frame): the men in fever, the pick's scratch arrays,
    /// the screen and the UI scale, the counts by skip reason. The arrays grow (doubling) only when a battle has more men in fever
    /// than ever before - never per frame once grown.
    /// </summary>
    public sealed class FeverMarkFrame
    {
        /// <summary>The first size of the arrays (they double when a battle needs more) - plumbing.</summary>
        public const int InitialCapacity = 256;

        public FeverMark[] Marks = new FeverMark[InitialCapacity];

        /// <summary>The pick's scratch: the eligible marks' indices and their distances (sorted together when the cap bites).</summary>
        public int[] Order = new int[InitialCapacity];

        public float[] Keys = new float[InitialCapacity];

        public int Count;

        /// <summary>Marks that passed every skip but the cap (the side switched on, in front, close enough, on the screen) - in <see cref="Order"/>.</summary>
        public int Eligible;

        /// <summary>The nearest <see cref="Eligible"/> up to the cap - <see cref="Order"/>[0..Picked).</summary>
        public int Picked;

        public float ScreenWidth;

        public float ScreenHeight;

        /// <summary>Screen pixels per UI pixel of the 1080p layout (the game's UI scale included).</summary>
        public float Scale = 1f;

        /// <summary>This frame's men by skip reason (index = <see cref="FeverMarkSkip"/>).</summary>
        public readonly int[] Skipped = new int[(int)FeverMarkSkip.Count];

        /// <summary>Times the arrays grew (the summary names it).</summary>
        public int Grown;

        public void Clear()
        {
            Count = 0;
            Eligible = 0;
            Picked = 0;
            Array.Clear(Skipped, 0, Skipped.Length);
        }

        /// <summary>A fresh mark at the end (the arrays grow when full).</summary>
        public ref FeverMark Add()
        {
            if (Count == Marks.Length) Grow();
            ref var m = ref Marks[Count++];
            m = default;
            return ref m;
        }

        /// <summary>Step 46: takes back the last <see cref="Add"/> (the walk found no man to mark there).</summary>
        public void DropLast()
        {
            if (Count > 0) Count--;
        }

        /// <summary>Step 46: the pick's order kept in place - the <paramref name="k"/>-th candidate (by distance) is the <paramref name="picked"/>-th drawn.</summary>
        public void Keep(int picked, int index) => Order[picked] = index;

        /// <summary>A mark skipped for <paramref name="why"/>.</summary>
        public void SkipAt(int index, FeverMarkSkip why)
        {
            Marks[index].Skip = why;
            Skipped[(int)why]++;
        }

        /// <summary>A mark that passed the first skips joins the pick.</summary>
        public void Elect(int index) => Elect(index, Marks[index].Distance);

        /// <summary>Step 58: a candidate ranked by <paramref name="key"/> (smaller = picked first): the camera distance in the every-man view, the
        /// angle from the screen's centre (<see cref="AltNearMath.CentreKey"/>) in the near view.</summary>
        public void Elect(int index, float key)
        {
            Order[Eligible] = index;
            Keys[Eligible] = key;
            Eligible++;
        }

        private void Grow()
        {
            int n = Marks.Length * 2;
            Array.Resize(ref Marks, n);
            Array.Resize(ref Order, n);
            Array.Resize(ref Keys, n);
            Grown++;
        }
    }

    /// <summary>Step 46: the engine's projection of one mark onto the screen for <see cref="FeverMarkMath.PickOnScreen"/> - a struct, so the
    /// generic call never boxes (the view's real one wraps its source; the tests play one). True = projected and on the screen.</summary>
    public interface IMarkProjector
    {
        bool ProjectOnScreen(ref FeverMark m);
    }

    /// <summary>
    /// Step 44's settings for one frame, read live: the master switch FIRST, Athletics (the fever is his Athletics), the fever itself,
    /// the marks' own switch; the enemy's switch, the cap, and the layout in UI pixels of the 1080p layout (the game's UI scale applies).
    /// </summary>
    public readonly struct FeverMarkRules
    {
        public FeverMarkRules(bool modEnabled, bool athleticsEnabled, bool feverEnabled, bool show, bool showEnemy, int max, int width, int height,
            double heightMetres)
        {
            ModEnabled = modEnabled;
            AthleticsEnabled = athleticsEnabled;
            FeverEnabled = feverEnabled;
            Show = show;
            ShowEnemy = showEnemy;
            Max = max < 0 ? 0 : max;
            Width = width < 1 ? 1 : width;
            Height = height < 1 ? 1 : height;
            HeightMetres = double.IsNaN(heightMetres) ? 0 : heightMetres;
        }

        public static FeverMarkRules From(TraxSettings s) => new FeverMarkRules(s.ModEnabled, s.AthleticsEnabled, s.FeverEnabled, s.ShowFeverMarks,
            s.FeverMarksShowEnemy, s.FeverMarkMax, s.FeverMarkWidth, s.FeverMarkHeight, s.FeverMarkHeightMetres);

        public bool ModEnabled { get; }
        public bool AthleticsEnabled { get; }
        public bool FeverEnabled { get; }

        /// <summary>ShowFeverMarks.</summary>
        public bool Show { get; }

        /// <summary>FeverMarksShowEnemy.</summary>
        public bool ShowEnemy { get; }

        /// <summary>FeverMarkMax: most marks drawn in one frame (the nearest to the camera).</summary>
        public int Max { get; }

        /// <summary>FeverMarkWidth / FeverMarkHeight, UI pixels (near the camera; smaller far away).</summary>
        public int Width { get; }

        public int Height { get; }

        /// <summary>FeverMarkHeightMetres: the mark's point above his feet, metres.</summary>
        public double HeightMetres { get; }

        /// <summary>Why no mark shows at all - the master switch first.</summary>
        public FeverMarksHidden Hidden =>
            !ModEnabled ? FeverMarksHidden.ModOff
            : !AthleticsEnabled ? FeverMarksHidden.AthleticsOff
            : !FeverEnabled ? FeverMarksHidden.FeverOff
            : !Show ? FeverMarksHidden.SwitchedOff
            : FeverMarksHidden.None;

        /// <summary>"marks 22 x 5 px, 2.4 m above the feet, at most 200 (the nearest), the enemy's too" - log lines.</summary>
        public string Describe() =>
            "marks " + Width + " x " + Height + " UI px (smaller far away; the game's UI scale applies), " + FeverMarkMath.F1(HeightMetres)
            + " m above his feet, at most " + Max + " a frame (FeverMarkMax - the nearest to the camera), "
            + (ShowEnemy ? "the enemy's too" : "yours and your allies' only (FeverMarksShowEnemy off)");
    }

    /// <summary>
    /// Step 44 pure math (AI_NOTES "Step 44"): the mark's colour and alpha by the fever (bright orange armed and at the burn's start, through
    /// yellow as it empties), vanilla's distance fade of its own ALT icon, the smaller size far away, the skips in order, the nearest-N pick,
    /// the placement, the names for the log.
    /// </summary>
    public static class FeverMarkMath
    {
        /// <summary>Vanilla's ALT icon (MissionAgentLabelView): gone closer than this to the camera (engine fact).</summary>
        public const float NearMetres = 1.5f;

        /// <summary>… and faded in up to this distance (engine fact).</summary>
        public const float FarMetres = 8f;

        /// <summary>Full size up to this distance, then smaller in a straight line … (plumbing).</summary>
        public const float FullSizeMetres = 20f;

        /// <summary>… down to <see cref="SmallestSize"/> at this distance and beyond (plumbing).</summary>
        public const float SmallSizeMetres = 60f;

        public const float SmallestSize = 0.6f;

        /// <summary>The mark's alpha with the fever nearly gone (it fades from 1 at a full fever to this; plumbing).</summary>
        public const float MinAlpha = 0.45f;

        /// <summary>The colours (UI constants, like the bars'): bright orange = armed and the burn's start, yellow = nearly burned out;
        /// the dark frame under the bar.</summary>
        public const string OrangeHex = "#FF8C1AFF";

        public const string YellowHex = "#FFE040FF";

        public const string FrameHex = "#141414C8";

        private const float OrangeR = 1f, OrangeG = 0x8C / 255f, OrangeB = 0x1A / 255f;
        private const float YellowR = 1f, YellowG = 0xE0 / 255f, YellowB = 0x40 / 255f;

        /// <summary>The dark frame's own alpha (0xC8).</summary>
        public const float FrameAlpha = 0xC8 / 255f;

        /// <summary>
        /// The mark's colour by the fever: armed = bright orange, full alpha; burning = orange at the burn's start (share 1) through yellow as
        /// it empties (share 0), the alpha from 1 down to <see cref="MinAlpha"/>. Empty = alpha 0 (no mark). The distance fade comes on top.
        /// </summary>
        public static void Tint(bool armed, float share, out float r, out float g, out float b, out float a)
        {
            float s = armed ? 1f : float.IsNaN(share) || share <= 0f ? 0f : share >= 1f ? 1f : share;
            float t = 1f - s;
            r = OrangeR + (YellowR - OrangeR) * t;
            g = OrangeG + (YellowG - OrangeG) * t;
            b = OrangeB + (YellowB - OrangeB) * t;
            a = s <= 0f ? 0f : MinAlpha + (1f - MinAlpha) * s;
        }

        /// <summary>Vanilla's ALT icon fade by the camera's distance: 0 closer than 1.5 m, a straight line to 1 at 8 m, 1 beyond.</summary>
        public static float Fade(float distance)
        {
            if (float.IsNaN(distance) || distance < NearMetres) return 0f;
            if (distance >= FarMetres) return 1f;
            return (distance - NearMetres) / (FarMetres - NearMetres);
        }

        /// <summary>The size factor by the camera's distance: 1 up to 20 m, a straight line to 0.6 at 60 m and beyond.</summary>
        public static float SizeFactor(float distance)
        {
            if (float.IsNaN(distance) || distance <= FullSizeMetres) return 1f;
            if (distance >= SmallSizeMetres) return SmallestSize;
            return 1f - (1f - SmallestSize) * (distance - FullSizeMetres) / (SmallSizeMetres - FullSizeMetres);
        }

        /// <summary>The first skips, in order: the enemy's switch, behind the camera, too close (the fade is 0). <see cref="FeverMarkSkip.None"/> = he
        /// joins the pick.</summary>
        public static FeverMarkSkip PreSkip(in FeverMark m, bool showEnemy)
        {
            if (m.Side == (int)MarkerTeam.Enemy && !showEnemy) return FeverMarkSkip.EnemyOff;
            if (!m.InFront) return FeverMarkSkip.Behind;
            if (!(m.Distance >= NearMetres)) return FeverMarkSkip.TooClose;
            return FeverMarkSkip.None;
        }

        /// <summary>
        /// The NEAREST <paramref name="max"/> of the eligible marks: when more are eligible, <see cref="FeverMarkFrame.Order"/> is sorted by the
        /// camera distance (allocation-free) and the rest are skipped as <see cref="FeverMarkSkip.Capped"/>. Returns how many were picked
        /// (<see cref="FeverMarkFrame.Order"/>[0..picked)).
        /// </summary>
        public static int Pick(FeverMarkFrame f, int max)
        {
            if (max < 0) max = 0;
            int n = f.Eligible;
            if (n > max)
            {
                Array.Sort(f.Keys, f.Order, 0, n);
                for (int k = max; k < n; k++) f.SkipAt(f.Order[k], FeverMarkSkip.Capped);
                n = max;
            }
            f.Picked = n;
            return n;
        }

        /// <summary>
        /// Step 46: the NEAREST <paramref name="max"/> marks ON THE SCREEN, projecting only as many as needed. When more are eligible than the cap,
        /// <see cref="FeverMarkFrame.Order"/> is sorted by the camera distance first (allocation-free); then the candidates are projected nearest
        /// first until the cap is full - a man off the screen is skipped (<see cref="FeverMarkSkip.OffScreen"/>) and never takes a slot (step 44's
        /// fix kept), the rest wait (<see cref="FeverMarkSkip.Capped"/>, never projected). Under the cap: every one projected, in its own order.
        /// With every soldier a candidate (step 46) this keeps the engine's projections near the cap's worth instead of one per man on the field.
        /// Returns how many were picked (<see cref="FeverMarkFrame.Order"/>[0..picked)).
        /// </summary>
        public static int PickOnScreen<T>(FeverMarkFrame f, int max, ref T projector) where T : struct, IMarkProjector
        {
            if (max < 0) max = 0;
            int n = f.Eligible;
            if (n > max) Array.Sort(f.Keys, f.Order, 0, n);
            int picked = 0;
            for (int k = 0; k < n; k++)
            {
                int index = f.Order[k];
                if (picked >= max)
                {
                    f.SkipAt(index, FeverMarkSkip.Capped);
                    continue;
                }
                if (!projector.ProjectOnScreen(ref f.Marks[index]))
                {
                    f.SkipAt(index, FeverMarkSkip.OffScreen);
                    continue;
                }
                f.Keep(picked++, index);
            }
            f.Picked = picked;
            return picked;
        }

        /// <summary>The mark's size in screen pixels: the UI px × the scale × <see cref="SizeFactor"/>, never below 1 px.</summary>
        public static void Size(in FeverMarkRules r, float scale, float distance, out float width, out float height)
        {
            if (!(scale > 0f)) scale = 1f;
            float k = scale * SizeFactor(distance);
            width = Math.Max(1f, r.Width * k);
            height = Math.Max(1f, r.Height * k);
        }

        /// <summary>The mark's top-left in screen pixels: centred on the point, its bottom edge on it.</summary>
        public static void Place(float x, float y, float width, float height, out float left, out float top)
        {
            left = x - width / 2f;
            top = y - height;
        }

        /// <summary>Any part of the mark on the screen.</summary>
        public static bool OnScreen(float left, float top, float width, float height, float screenWidth, float screenHeight) =>
            !float.IsNaN(left) && !float.IsNaN(top) && left + width > 0f && top + height > 0f && left < screenWidth && top < screenHeight;

        public static string SkipName(FeverMarkSkip s) => s switch
        {
            FeverMarkSkip.None => "marked",
            FeverMarkSkip.NotPlaced => "not on the field",
            FeverMarkSkip.EnemyOff => "enemy (its bars switched off)",
            FeverMarkSkip.Behind => "behind the camera",
            FeverMarkSkip.TooClose => "closer than " + F1(NearMetres) + " m",
            FeverMarkSkip.TooFar => "beyond the near range",
            FeverMarkSkip.Capped => "beyond the cap",
            FeverMarkSkip.OffScreen => "off the screen",
            _ => s.ToString(),
        };

        public static string HiddenName(FeverMarksHidden h) => h switch
        {
            FeverMarksHidden.ModOff => "ModEnabled off (the master switch)",
            FeverMarksHidden.AthleticsOff => "AthleticsEnabled off",
            FeverMarksHidden.FeverOff => "FeverEnabled off (no fevers to mark)",
            FeverMarksHidden.SwitchedOff => "ShowFeverMarks off",
            _ => "shown",
        };

        /// <summary>"behind the camera 3, beyond the cap 12" - the reasons with a count, in order (log lines; allocates).</summary>
        public static string DescribeSkips(int[] skipped)
        {
            var sb = new StringBuilder();
            for (int i = 1; i < (int)FeverMarkSkip.Count && i < skipped.Length; i++)
            {
                if (skipped[i] == 0) continue;
                sb.Append(sb.Length > 0 ? ", " : string.Empty).Append(SkipName((FeverMarkSkip)i)).Append(' ').Append(skipped[i]);
            }
            return sb.Length == 0 ? "none" : sb.ToString();
        }

        /// <summary>"yours man 17 at (950, 420), 12.3 m, armed" / "… burning 40% left" - log lines.</summary>
        public static string DescribeMark(in FeverMark m) =>
            AltMarkerMath.TeamName(m.Side) + " man " + m.Key.ToString(CultureInfo.InvariantCulture) + " at (" + OrderStripMath.Px(m.ScreenX) + ", "
            + OrderStripMath.Px(m.ScreenY) + "), " + F1(m.Distance) + " m, " + (m.Armed ? "armed" : "burning " + Math.Round(100 * m.Share).ToString(CultureInfo.InvariantCulture) + "% left");

        internal static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        internal static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// One mission of the ALT bars (step 44's fever marks, step 46's Athletics bars - one view) for the [summary]: how often they came up and
    /// for how long, the men marked per frame (mean / max), the Athletics bars and the fever bars per frame (mean / max) with the Athletics
    /// bars' colours, the frames the cap bit, the most men looked at and in a fever at once, the men skipped by reason (summed over frames),
    /// the frame cost. Main thread.
    /// </summary>
    public sealed class FeverMarkStats
    {
        private readonly long[] _skipped = new long[(int)FeverMarkSkip.Count];
        private readonly long[] _bands = new long[BarMath.BandCount];

        public int Shows { get; private set; }
        public double SecondsOnScreen { get; private set; }
        public long Frames { get; private set; }
        public long MarksDrawn { get; private set; }
        public int MostMarks { get; private set; }
        public long BarsDrawn { get; private set; }
        public int MostBars { get; private set; }
        public long WoundedBars { get; private set; }
        public long FeverBarsDrawn { get; private set; }
        public int MostFeverBars { get; private set; }
        public long CappedFrames { get; private set; }
        public int MostLooked { get; private set; }
        public int MostInFever { get; private set; }
        public int MostYours { get; private set; }
        public int MostEnemy { get; private set; }
        public int FirstCap { get; private set; } = -1;
        public double CostMs { get; private set; }
        public double MostCostMs { get; private set; }
        public int Grown { get; set; }

        public void NoteShown() => Shows++;

        // ---- step 67: the morale bars drawn (the morale eyes)

        public long MoraleBarsDrawn { get; private set; }
        public int MostMoraleBars { get; private set; }
        public double MoraleSum { get; private set; }

        public double MeanMoraleBars => Frames > 0 ? (double)MoraleBarsDrawn / Frames : 0;

        public double MeanMoraleShown => MoraleBarsDrawn > 0 ? MoraleSum / MoraleBarsDrawn : double.NaN;

        // ---- step 58: the view's mode per frame (the near view: the men nearest the screen's centre, close to the camera)

        public long NearFrames { get; private set; }
        public long NearMarks { get; private set; }
        public int MostNear { get; private set; }
        public long EveryoneFrames { get; private set; }

        /// <summary>The near view's cap and range at its first frame (the summary names them).</summary>
        public int NearCap { get; private set; } = -1;

        public double NearMetres { get; private set; }

        public double MeanNear => NearFrames > 0 ? (double)NearMarks / NearFrames : 0;

        /// <summary>Step 58: this frame's mode - near (with the men marked, the cap and the range) or everyone.</summary>
        public void NoteMode(bool near, int drawn, int cap, double metres)
        {
            if (!near)
            {
                EveryoneFrames++;
                return;
            }
            NearFrames++;
            NearMarks += drawn;
            if (drawn > MostNear) MostNear = drawn;
            if (NearCap < 0)
            {
                NearCap = cap;
                NearMetres = metres;
            }
        }

        /// <summary>Step 46: this frame's bars - the Athletics bars drawn (by band; <paramref name="bands"/> indexed by <see cref="BarBand"/>), how many
        /// of them show a wound cap, the fever bars drawn. Call before <see cref="NoteFrame"/> of the same frame (any order works).</summary>
        public void NoteBars(int bars, int[] bands, int wounded, int feverBars, int moraleBars = 0, double moraleSum = 0)
        {
            MoraleBarsDrawn += moraleBars; // step 67
            if (moraleBars > MostMoraleBars) MostMoraleBars = moraleBars;
            MoraleSum += moraleSum;
            BarsDrawn += bars;
            if (bars > MostBars) MostBars = bars;
            for (int i = 0; i < _bands.Length && i < bands.Length; i++) _bands[i] += bands[i];
            WoundedBars += wounded;
            FeverBarsDrawn += feverBars;
            if (feverBars > MostFeverBars) MostFeverBars = feverBars;
        }

        /// <summary>One frame on screen: the men marked, the men looked at (placed - by side) and in a fever, whether the cap bit, the skips, the cost.</summary>
        public void NoteFrame(double dt, int drawn, int looked, int inFever, int yours, int enemy, bool capped, int cap, int[] skipped, double costMs)
        {
            if (dt > 0) SecondsOnScreen += dt;
            Frames++;
            MarksDrawn += drawn;
            if (drawn > MostMarks) MostMarks = drawn;
            if (looked > MostLooked) MostLooked = looked;
            if (inFever > MostInFever) MostInFever = inFever;
            if (yours > MostYours) MostYours = yours;
            if (enemy > MostEnemy) MostEnemy = enemy;
            if (capped)
            {
                CappedFrames++;
                if (FirstCap < 0) FirstCap = cap;
            }
            for (int i = 0; i < _skipped.Length && i < skipped.Length; i++) _skipped[i] += skipped[i];
            if (costMs > 0)
            {
                CostMs += costMs;
                if (costMs > MostCostMs) MostCostMs = costMs;
            }
        }

        public long Skipped(FeverMarkSkip why) => _skipped[(int)why];

        /// <summary>Athletics bars drawn in <paramref name="band"/>, summed over frames.</summary>
        public long BarsIn(BarBand band) => _bands[(int)band];

        public double MeanMarks => Frames > 0 ? (double)MarksDrawn / Frames : 0;

        public double MeanBars => Frames > 0 ? (double)BarsDrawn / Frames : 0;

        public double MeanFeverBars => Frames > 0 ? (double)FeverBarsDrawn / Frames : 0;

        public double MeanCostMs => Frames > 0 ? CostMs / Frames : 0;

        /// <summary>The [summary] line (without the tag); <paramref name="errors"/> = the view's own error count.</summary>
        public string SummaryLine(int errors)
        {
            var sb = new StringBuilder("hud: ALT bars - shown ").Append(Shows).Append('x');
            if (Shows == 0)
                return sb.Append(" (the game's markers never came up with the bars on - ALT never held or the orders menu never opened in a fight, or the Athletics and fever bars off)")
                         .Append("; errors ").Append(errors).ToString();
            sb.Append(", on screen ").Append(FeverMarkMath.F1(SecondsOnScreen)).Append(" s over ").Append(Frames).Append(" frames");
            sb.Append("; men marked a frame: mean ").Append(FeverMarkMath.F1(MeanMarks)).Append(", max ").Append(MostMarks);
            sb.Append("; Athletics bars a frame: mean ").Append(FeverMarkMath.F1(MeanBars)).Append(", max ").Append(MostBars);
            if (BarsDrawn > 0)
            {
                sb.Append(" (summed over frames:");
                for (int i = 0; i < _bands.Length; i++)
                    sb.Append(i == 0 ? " " : ", ").Append(BarMath.Name((BarBand)i)).Append(' ').Append(_bands[i]);
                sb.Append("; wounded ").Append(WoundedBars).Append(')');
            }
            sb.Append("; fever bars a frame: mean ").Append(FeverMarkMath.F1(MeanFeverBars)).Append(", max ").Append(MostFeverBars);
            sb.Append("; most men looked at in a frame ").Append(MostLooked).Append(" (yours and allies ").Append(MostYours).Append(", enemy ").Append(MostEnemy)
              .Append("), most in a fever ").Append(MostInFever);
            sb.Append("; frames at the cap ").Append(CappedFrames);
            if (CappedFrames > 0) sb.Append(" (FeverMarkMax ").Append(FirstCap).Append(" at the first - the nearest drawn)");
            sb.Append("; men skipped, summed over frames: ");
            bool any = false;
            for (int i = 1; i < _skipped.Length; i++)
            {
                if (_skipped[i] == 0) continue;
                sb.Append(any ? ", " : string.Empty).Append(FeverMarkMath.SkipName((FeverMarkSkip)i)).Append(' ').Append(_skipped[i]);
                any = true;
            }
            if (!any) sb.Append("none");
            sb.Append("; cost ").Append((MeanCostMs * 1000).ToString("0", CultureInfo.InvariantCulture)).Append(" µs a frame (most ")
              .Append((MostCostMs * 1000).ToString("0", CultureInfo.InvariantCulture)).Append(" µs)");
            if (Grown > 0) sb.Append(", the frame's arrays grew ").Append(Grown).Append('x');
            if (NearFrames + EveryoneFrames > 0)
            {
                sb.Append("; mode (step 58): near ").Append(NearFrames).Append(" frames");
                if (NearFrames > 0)
                    sb.Append(" (men marked a frame: mean ").Append(FeverMarkMath.F1(MeanNear)).Append(", max ").Append(MostNear).Append(" - up to ").Append(NearCap)
                      .Append(" nearest the screen's centre within ").Append(FeverMarkMath.F1(NearMetres)).Append(" m of the camera at the first)");
                sb.Append(", everyone ").Append(EveryoneFrames).Append(" frames");
            }
            if (MoraleBarsDrawn > 0)
                sb.Append("; morale bars a frame (step 67): mean ").Append(FeverMarkMath.F1(MeanMoraleBars)).Append(", max ").Append(MostMoraleBars)
                  .Append(" (their morale ").Append(FeverMarkMath.F1(MeanMoraleShown)).Append(" mean)");
            return sb.Append("; errors ").Append(errors).ToString();
        }
    }
}

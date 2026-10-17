using System;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>Step 46: why no Athletics bar shows above the men at all - in the order the rules are read, the master switch FIRST.</summary>
    public enum AthleticsMarksHidden
    {
        None = 0,
        ModOff = 1,
        AthleticsOff = 2,
        SwitchedOff = 3,
    }

    /// <summary>
    /// Step 46's settings for one frame, read live (AI_NOTES "Step 46"): the master switch FIRST, Athletics, the bars' own switch, the enemy's
    /// switch, and the layout in UI pixels of the 1080p layout (the game's UI scale applies): the bar's length and thickness, the gap above
    /// the fever bar. The cap (FeverMarkMax), the height above the feet and the fade are the fever mark's - one view, one stack per man.
    /// </summary>
    public readonly struct AthleticsMarkRules
    {
        public AthleticsMarkRules(bool modEnabled, bool athleticsEnabled, bool show, bool showEnemy, int width, int height, int gap)
        {
            ModEnabled = modEnabled;
            AthleticsEnabled = athleticsEnabled;
            Show = show;
            ShowEnemy = showEnemy;
            Width = width < 1 ? 1 : width;
            Height = height < 1 ? 1 : height;
            Gap = gap < 0 ? 0 : gap;
        }

        public static AthleticsMarkRules From(TraxSettings s) => new AthleticsMarkRules(s.ModEnabled, s.AthleticsEnabled, s.ShowAthleticsMarks,
            s.AthleticsMarksShowEnemy, s.AthleticsMarkWidth, s.AthleticsMarkHeight, s.AthleticsMarkGap);

        public bool ModEnabled { get; }
        public bool AthleticsEnabled { get; }

        /// <summary>ShowAthleticsMarks.</summary>
        public bool Show { get; }

        /// <summary>AthleticsMarksShowEnemy.</summary>
        public bool ShowEnemy { get; }

        /// <summary>AthleticsMarkWidth / AthleticsMarkHeight, UI pixels (near the camera; smaller far away).</summary>
        public int Width { get; }

        public int Height { get; }

        /// <summary>AthleticsMarkGap: the fever bar's top → the Athletics bar's bottom, UI pixels.</summary>
        public int Gap { get; }

        /// <summary>Why no Athletics bar shows at all - the master switch first.</summary>
        public AthleticsMarksHidden Hidden =>
            !ModEnabled ? AthleticsMarksHidden.ModOff
            : !AthleticsEnabled ? AthleticsMarksHidden.AthleticsOff
            : !Show ? AthleticsMarksHidden.SwitchedOff
            : AthleticsMarksHidden.None;

        /// <summary>The bars are drawn this frame (for some side).</summary>
        public bool On => Hidden == AthleticsMarksHidden.None;

        /// <summary>"Athletics bars 22 x 5 UI px, 2 px above the fever bar (its place kept while he has none), the enemy's too" - log lines.</summary>
        public string Describe() =>
            "Athletics bars " + Width + " x " + Height + " UI px, " + Gap + " px above the fever bar (its place kept while he has none), "
            + (ShowEnemy ? "the enemy's too" : "yours and your allies' only (AthleticsMarksShowEnemy off)");
    }

    /// <summary>A man's stack of bars on the screen, in screen pixels: the box (its bottom centre on his point), the Athletics bar at its top,
    /// the fever bar at its bottom (both centred). A size of 0 = that bar has no place in the stack.</summary>
    public struct MarkBox
    {
        public float Left;
        public float Top;
        public float Width;
        public float Height;
        public float BarWidth;
        public float BarHeight;
        public float FeverWidth;
        public float FeverHeight;
    }

    /// <summary>
    /// Step 46 pure math (AI_NOTES "Step 46"): who gets which bar (the switches, his side), the Athletics bar as the strip / the player bar read
    /// it (the fill = points ÷ his FULL pool, the wound cap, the colour by f through <see cref="BarMath.Band"/>), the STACK - the Athletics bar
    /// just above the fever bar, at a fixed height while fever bars can show for his side -, a rider's lift, the colours as floats, the log text.
    /// </summary>
    public static class AthleticsMarkMath
    {
        /// <summary>A rider's bars go this much higher than a man on foot's (plumbing: vanilla's own locked camera puts a rider's eyes about a
        /// horse's back - its body capsule's top - above a standing man's; ~1 m for the game's horses. PLAYTEST D15 checks it by eye).</summary>
        public const float RiderLiftMetres = 1.0f;

        /// <summary>The wounded part of the bar - the player bar's own colour (TraxPlayerAthleticsBar.xml's inside).</summary>
        public const string WoundHex = "#4A1C1CFF";

        /// <summary>A bar counts as wounded below this usable share (the summary's "wounded").</summary>
        public const float WoundedBelow = 0.999f;

        private static readonly float[] BandRgb = BuildBandRgb();

        /// <summary>The point his stack stands on: <paramref name="heightMetres"/> above his feet, a rider <see cref="RiderLiftMetres"/> higher.</summary>
        public static float MarkHeight(float heightMetres, bool mounted) => heightMetres + (mounted ? RiderLiftMetres : 0f);

        /// <summary>
        /// Which bars he gets, into <see cref="FeverMark.HasBar"/> / <see cref="FeverMark.HasFever"/> / <see cref="FeverMark.FeverSlot"/>, then the
        /// first skips in order: the enemy with neither bar switched on, behind the camera, too close (vanilla's icon fade is 0 there).
        /// <see cref="FeverMarkSkip.None"/> = he joins the pick. The fever bar needs the fever marks on (master switch, Athletics, FeverEnabled,
        /// ShowFeverMarks) and him in a fever; its PLACE is kept under his Athletics bar whenever fever bars can show for his side.
        /// </summary>
        public static FeverMarkSkip Decide(ref FeverMark m, in FeverMarkRules fever, in AthleticsMarkRules bars)
        {
            bool enemy = m.Side == (int)MarkerTeam.Enemy;
            m.HasBar = bars.On && (!enemy || bars.ShowEnemy);
            m.FeverSlot = fever.Hidden == FeverMarksHidden.None && (!enemy || fever.ShowEnemy);
            m.HasFever = m.FeverSlot && m.InFever;
            if (!m.HasBar && !m.HasFever) return FeverMarkSkip.EnemyOff; // only the enemy can have neither: the walk asks for the rest
            if (!m.InFront) return FeverMarkSkip.Behind;
            if (!(m.Distance >= FeverMarkMath.NearMetres)) return FeverMarkSkip.TooClose;
            return FeverMarkSkip.None;
        }

        /// <summary>His Athletics bar as the strip / the player bar read it: the fill = <paramref name="fraction"/> (points ÷ his FULL pool), the
        /// usable share (the wound cap - the dark red part beyond it), the colour by f (<paramref name="peakShare"/>) through the live bands.</summary>
        public static void Read(ref FeverMark m, in BarRules bands, double fraction, double usableFraction, double peakShare)
        {
            m.BarFill = BarMath.Fill(fraction);
            m.BarUsable = BarMath.Usable(usableFraction);
            if (m.BarFill > m.BarUsable) m.BarFill = m.BarUsable;
            m.Band = BarMath.Band(in bands, peakShare);
        }

        /// <summary>
        /// His stack on the screen: each bar sized from its UI px × the UI scale × <see cref="FeverMarkMath.SizeFactor"/> (never below 1 px), the
        /// box's bottom centre on (<paramref name="x"/>, <paramref name="y"/>). The fever bar at the bottom when he has one OR when its place is kept
        /// (<paramref name="feverSlot"/> - so his Athletics bar keeps its height when his fever ends); the Athletics bar on top of it, the gap between
        /// (scaled like the bars). Only the fever bar: exactly step 44's mark.
        /// </summary>
        public static void Layout(in FeverMarkRules fever, in AthleticsMarkRules bars, float scale, float distance, bool hasBar, bool hasFever, bool feverSlot,
            float x, float y, out MarkBox box)
        {
            if (!(scale > 0f)) scale = 1f;
            float k = scale * FeverMarkMath.SizeFactor(distance);
            bool slot = hasFever || (hasBar && feverSlot);
            box = default;
            if (slot)
            {
                box.FeverWidth = Math.Max(1f, fever.Width * k);
                box.FeverHeight = Math.Max(1f, fever.Height * k);
            }
            if (hasBar)
            {
                box.BarWidth = Math.Max(1f, bars.Width * k);
                box.BarHeight = Math.Max(1f, bars.Height * k);
            }
            float gap = slot && hasBar ? bars.Gap * k : 0f;
            box.Width = Math.Max(box.BarWidth, box.FeverWidth);
            box.Height = box.BarHeight + gap + box.FeverHeight;
            box.Left = x - box.Width / 2f;
            box.Top = y - box.Height;
        }

        /// <summary>The band's fill colour as floats (from <see cref="BarMath.ColorHex"/> - the strip's / the player bar's), alpha 1.</summary>
        public static void BandColor(BarBand band, out float r, out float g, out float b)
        {
            int i = (int)band;
            if (i < 0 || i >= BarMath.BandCount) i = (int)BarBand.Red;
            r = BandRgb[3 * i];
            g = BandRgb[3 * i + 1];
            b = BandRgb[3 * i + 2];
        }

        /// <summary>"#RRGGBBAA" → floats 0..1 (the colour constants; false when it is not one).</summary>
        public static bool ParseHex(string hex, out float r, out float g, out float b, out float a)
        {
            r = g = b = a = 0f;
            if (hex == null || hex.Length != 9 || hex[0] != '#') return false;
            if (!uint.TryParse(hex.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v)) return false;
            r = ((v >> 24) & 0xFF) / 255f;
            g = ((v >> 16) & 0xFF) / 255f;
            b = ((v >> 8) & 0xFF) / 255f;
            a = (v & 0xFF) / 255f;
            return true;
        }

        /// <summary>"yours man 17 at (950, 420), 12.3 m, armed, Athletics 100% green" / "…, calm, Athletics 50% yellow" - a man's whole mark for the
        /// log (step 44's words for his fever when he has one; "calm" when not).</summary>
        public static string DescribeMan(in FeverMark m)
        {
            string fever = FeverMarkMath.DescribeMark(in m);
            if (!m.InFever) fever = fever.Substring(0, fever.LastIndexOf(", ", StringComparison.Ordinal)) + ", calm";
            return m.HasBar ? fever + ", " + DescribeBar(in m) : fever;
        }

        public static bool Wounded(in FeverMark m) => m.HasBar && m.BarUsable < WoundedBelow;

        /// <summary>"Athletics 72% yellow" / "… wounded to 60%" / "no Athletics bar" - log lines.</summary>
        public static string DescribeBar(in FeverMark m) =>
            !m.HasBar ? "no Athletics bar"
            : "Athletics " + Math.Round(100 * m.BarFill).ToString(CultureInfo.InvariantCulture) + "% " + BarMath.Name(m.Band)
              + (Wounded(in m) ? ", wounded to " + Math.Round(100 * m.BarUsable).ToString(CultureInfo.InvariantCulture) + "%" : string.Empty);

        public static string HiddenName(AthleticsMarksHidden h) => h switch
        {
            AthleticsMarksHidden.ModOff => "ModEnabled off (the master switch)",
            AthleticsMarksHidden.AthleticsOff => "AthleticsEnabled off",
            AthleticsMarksHidden.SwitchedOff => "ShowAthleticsMarks off",
            _ => "shown",
        };

        private static float[] BuildBandRgb()
        {
            var rgb = new float[3 * BarMath.BandCount];
            for (int i = 0; i < BarMath.BandCount; i++)
            {
                ParseHex(BarMath.ColorHex((BarBand)i), out rgb[3 * i], out rgb[3 * i + 1], out rgb[3 * i + 2], out _);
            }
            return rgb;
        }
    }
}

using System;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 58 (Anton, 2026-10-18: "this ALT view is too cluttered ... only show them on up to 5 soldiers that I'm directly looking at and are
    /// really close, making it work in tournaments too") - which men get their bars under ALT, read live (AI_NOTES "Step 58"):
    /// the NEAR view (the default) = up to <see cref="NearMax"/> men nearest the CENTRE of the screen (the smallest angle between the camera's
    /// forward and the line to the bar's point) among the men within <see cref="NearMetres"/> of the CAMERA; the EVERY-MAN view
    /// (<c>AltMarksEveryone</c>) = step 46 exactly (the nearest <c>FeverMarkMax</c> on the screen). <c>FeverMarkMax</c> stays the hard ceiling of both.
    /// </summary>
    public readonly struct AltNearRules
    {
        public AltNearRules(bool everyone, int nearMax, double nearMetres, int hardMax)
        {
            Everyone = everyone;
            NearMax = nearMax < 0 ? 0 : nearMax;
            NearMetres = double.IsNaN(nearMetres) || nearMetres < 0 ? 0 : nearMetres;
            HardMax = hardMax < 0 ? 0 : hardMax;
        }

        public static AltNearRules From(TraxSettings s) => new AltNearRules(s.AltMarksEveryone, s.AltNearMarksMax, s.AltNearMarksMetres, s.FeverMarkMax);

        /// <summary>AltMarksEveryone: step 46's view (every man on the screen).</summary>
        public bool Everyone { get; }

        /// <summary>The near view (the default).</summary>
        public bool Near => !Everyone;

        /// <summary>AltNearMarksMax.</summary>
        public int NearMax { get; }

        /// <summary>AltNearMarksMetres - the camera to the bar's point.</summary>
        public double NearMetres { get; }

        /// <summary>FeverMarkMax - the every-man view's cap and the near view's ceiling.</summary>
        public int HardMax { get; }

        /// <summary>The most men marked this frame: FeverMarkMax in the every-man view, min(AltNearMarksMax, FeverMarkMax) in the near view.</summary>
        public int Cap => Everyone ? HardMax : Math.Min(NearMax, HardMax);

        /// <summary>"near" / "everyone" - the summary's word.</summary>
        public string ModeName => Everyone ? "everyone" : "near";

        /// <summary>The log's sentence for the mode.</summary>
        public string Describe() => Everyone
            ? "mode everyone (AltMarksEveryone on): every man on the screen, the nearest " + HardMax + " to the camera (FeverMarkMax)"
            : "mode near: up to " + Cap + " men nearest the screen's centre (AltNearMarksMax " + NearMax + ", FeverMarkMax " + HardMax + " the ceiling) within "
              + FeverMarkMath.F1(NearMetres) + " m of the camera (AltNearMarksMetres), no distance fade";
    }

    /// <summary>Step 58 pure math: how near a man is to the screen's centre (the angle from the camera's forward), the range test, the pick key.</summary>
    public static class AltNearMath
    {
        /// <summary>The cosine of the angle between the camera's forward (<paramref name="fx"/>, <paramref name="fy"/>, <paramref name="fz"/> - any
        /// length) and the line from the camera to the point (<paramref name="dx"/>, <paramref name="dy"/>, <paramref name="dz"/>): 1 = dead centre,
        /// 0 = square to the side, −1 behind. −1 for a zero line or forward (never picked first).</summary>
        public static float Facing(float dx, float dy, float dz, float fx, float fy, float fz)
        {
            double d = Math.Sqrt((double)dx * dx + (double)dy * dy + (double)dz * dz);
            double f = Math.Sqrt((double)fx * fx + (double)fy * fy + (double)fz * fz);
            if (!(d > 0) || !(f > 0)) return -1f;
            double c = ((double)dx * fx + (double)dy * fy + (double)dz * fz) / (d * f);
            if (double.IsNaN(c)) return -1f;
            return c > 1 ? 1f : c < -1 ? -1f : (float)c;
        }

        /// <summary>The pick's key: 1 − the cosine (0 dead centre, growing with the angle; smaller = picked first). NaN → 2 (last).</summary>
        public static float CentreKey(float facing) => float.IsNaN(facing) ? 2f : 1f - Math.Max(-1f, Math.Min(1f, facing));

        /// <summary>The angle in degrees (log lines).</summary>
        public static double AngleDegrees(float facing) =>
            float.IsNaN(facing) ? 180 : Math.Acos(Math.Max(-1f, Math.Min(1f, facing))) * 180.0 / Math.PI;

        /// <summary>Within the near view's range (the camera to the bar's point).</summary>
        public static bool InRange(float distance, double metres) => !float.IsNaN(distance) && distance <= metres;

        /// <summary>The near view's fade: none - full alpha at every candidate distance (step 44's 1.5 → 8 m icon fade would dim the close
        /// opponent you look at); closer than <see cref="FeverMarkMath.NearMetres"/> never gets this far (skipped as too close).</summary>
        public static float Fade(bool near, float distance) => near ? (distance >= FeverMarkMath.NearMetres ? 1f : 0f) : FeverMarkMath.Fade(distance);
    }

    /// <summary>Step 58: why a formation's fever line is not drawn - the master switch FIRST, in the order <see cref="FeverLabelMath.HiddenFor"/> asks.</summary>
    public enum FeverLabelHidden
    {
        None,
        ModOff,
        AthleticsOff,
        FeverOff,
        SwitchedOff,

        /// <summary>No tracked man in the formation.</summary>
        NoMen,

        /// <summary>Nobody in a fever - no line (less noise).</summary>
        NoneInFever,

        Count,
    }

    /// <summary>Step 58: the fever line's rules, read live - the master switch, Athletics, the fever itself, ShowFeverOnLabels.</summary>
    public readonly struct FeverLabelRules
    {
        public FeverLabelRules(bool show, bool modEnabled, bool athleticsEnabled, bool feverEnabled)
        {
            Show = show;
            ModEnabled = modEnabled;
            AthleticsEnabled = athleticsEnabled;
            FeverEnabled = feverEnabled;
        }

        public static FeverLabelRules From(TraxSettings s) => new FeverLabelRules(s.ShowFeverOnLabels, s.ModEnabled, s.AthleticsEnabled, s.FeverEnabled);

        /// <summary>ShowFeverOnLabels.</summary>
        public bool Show { get; }
        public bool ModEnabled { get; }
        public bool AthleticsEnabled { get; }
        public bool FeverEnabled { get; }

        public FeverLabelHidden Hidden =>
            !ModEnabled ? FeverLabelHidden.ModOff
            : !AthleticsEnabled ? FeverLabelHidden.AthleticsOff
            : !FeverEnabled ? FeverLabelHidden.FeverOff
            : !Show ? FeverLabelHidden.SwitchedOff
            : FeverLabelHidden.None;

        /// <summary>"fever share on (shown while a man is in a fever)" / "fever share hidden (FeverEnabled off)" - log lines.</summary>
        public string Describe() => Hidden == FeverLabelHidden.None
            ? "fever share on (\"fever 12/40\", shown while a man of the formation is in a fever)"
            : "fever share hidden (" + FeverLabelMath.HiddenName(Hidden) + ")";
    }

    /// <summary>Step 58: the formation's fever line on its ALT label - "fever 12/40" (men in a fever of the men counted), in the fever bar's orange.</summary>
    public static class FeverLabelMath
    {
        /// <summary>The fever bar's own orange (<see cref="FeverMarkMath.OrangeHex"/>).</summary>
        public const string ColorHex = FeverMarkMath.OrangeHex;

        /// <summary>The rules' reason, else NoMen for an empty formation, NoneInFever with nobody in a fever, else None.</summary>
        public static FeverLabelHidden HiddenFor(in FeverLabelRules rules, in FormationAthleticsStats stats)
        {
            var h = rules.Hidden;
            if (h != FeverLabelHidden.None) return h;
            if (stats.Total <= 0) return FeverLabelHidden.NoMen;
            return stats.InFever <= 0 ? FeverLabelHidden.NoneInFever : FeverLabelHidden.None;
        }

        /// <summary>"fever 12/40" - the ready count's style.</summary>
        public static string Text(int inFever, int total) =>
            "fever " + inFever.ToString(CultureInfo.InvariantCulture) + "/" + total.ToString(CultureInfo.InvariantCulture);

        public static string HiddenName(FeverLabelHidden h) => h switch
        {
            FeverLabelHidden.ModOff => "ModEnabled off",
            FeverLabelHidden.AthleticsOff => "AthleticsEnabled off",
            FeverLabelHidden.FeverOff => "FeverEnabled off",
            FeverLabelHidden.SwitchedOff => "ShowFeverOnLabels off",
            FeverLabelHidden.NoMen => "no men",
            FeverLabelHidden.NoneInFever => "nobody in a fever",
            _ => "shown",
        };
    }

    /// <summary>Step 58: the ALT labels' fever lines over one mission for the [summary] - pushed with a share, the most in a fever (which formation),
    /// hidden by reason. Main thread; nothing allocated per note.</summary>
    public sealed class FeverLabelStats
    {
        private readonly int[] _hidden = new int[(int)FeverLabelHidden.Count];
        private double _mostShare = -1;
        private int _mostIn;
        private int _mostTotal;
        private int _mostFormation = -1;
        private int _mostTeamType = -1;

        public int Shown { get; private set; }

        public int HiddenBy(FeverLabelHidden h) => _hidden[(int)h];

        public void NoteShown(int inFever, int total, int formationIndex, int teamType)
        {
            Shown++;
            double share = total > 0 ? (double)inFever / total : 0;
            if (share > _mostShare)
            {
                _mostShare = share;
                _mostIn = inFever;
                _mostTotal = total;
                _mostFormation = formationIndex;
                _mostTeamType = teamType;
            }
        }

        public void NoteHidden(FeverLabelHidden why)
        {
            int i = (int)why;
            if (i > 0 && i < _hidden.Length) _hidden[i]++;
        }

        /// <summary><c>hud: ALT markers - fever share (step 58): shown 40x, the most in a fever 30/40 (75%, enemy 1 Infantry); hidden 12x (nobody in a fever 12)</c>.</summary>
        public string SummaryLine(string viewName)
        {
            var sb = new StringBuilder("hud: ").Append(viewName).Append(" - fever share (step 58): ");
            if (Shown == 0) sb.Append("never shown");
            else
                sb.Append("shown ").Append(Shown).Append("x, the most in a fever ").Append(_mostIn).Append('/').Append(_mostTotal).Append(" (")
                  .Append((100 * _mostShare).ToString("0", CultureInfo.InvariantCulture)).Append("%, ")
                  .Append(_mostTeamType >= 0 ? AltMarkerMath.TeamName(_mostTeamType) + " " : string.Empty)
                  .Append(OrderStripMath.FormationName(_mostFormation)).Append(')');
            int hidden = 0;
            for (int i = 1; i < _hidden.Length; i++) hidden += _hidden[i];
            if (hidden == 0) return sb.Append(Shown == 0 ? " (no values pushed)" : "; hidden: never").ToString();
            sb.Append("; hidden ").Append(hidden).Append("x (");
            bool first = true;
            for (int i = 1; i < _hidden.Length; i++)
            {
                if (_hidden[i] == 0) continue;
                sb.Append(first ? string.Empty : ", ").Append(FeverLabelMath.HiddenName((FeverLabelHidden)i)).Append(' ').Append(_hidden[i]);
                first = false;
            }
            return sb.Append(')').ToString();
        }
    }
}

using System;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>Step 67: why no morale bar shows above the men at all - the master switch FIRST.</summary>
    public enum MoraleMarksHidden
    {
        None = 0,
        ModOff = 1,
        SwitchedOff = 2,
    }

    /// <summary>
    /// Step 67's MORALE EYES under ALT (Anton 2026-10-19: "give me eyes on their morale too: ALT to see the average morale of the whole group, and
    /// the same 5 closest guys' morale too"; AI_NOTES "Step 67"): a third small bar at the bottom of each marked man's stack (the step-58 near
    /// view's men, or every man), the Athletics bar's size, in its own BLUE; the enemy's follow the Athletics bars' enemy switch. Read live.
    /// </summary>
    public readonly struct MoraleMarkRules
    {
        public MoraleMarkRules(bool modEnabled, bool show, bool showEnemy)
        {
            ModEnabled = modEnabled;
            Show = show;
            ShowEnemy = showEnemy;
        }

        public static MoraleMarkRules From(TraxSettings s) => new MoraleMarkRules(s.ModEnabled, s.ShowMoraleMarks, s.AthleticsMarksShowEnemy);

        /// <summary>No morale bars (the default for the step-46 callers).</summary>
        public static MoraleMarkRules Off => new MoraleMarkRules(false, false, false);

        public bool ModEnabled { get; }

        /// <summary>ShowMoraleMarks.</summary>
        public bool Show { get; }

        /// <summary>AthleticsMarksShowEnemy (the enemy's bars follow the Athletics bars' switch).</summary>
        public bool ShowEnemy { get; }

        public MoraleMarksHidden Hidden => !ModEnabled ? MoraleMarksHidden.ModOff : !Show ? MoraleMarksHidden.SwitchedOff : MoraleMarksHidden.None;

        public bool On => Hidden == MoraleMarksHidden.None;

        public string Describe() => !On ? "morale bars off (" + MoraleMarkMath.HiddenName(Hidden) + ")"
            : "morale bars (blue, 0-100) under the bars, " + (ShowEnemy ? "the enemy's too" : "yours and your allies' only (AthleticsMarksShowEnemy off)");
    }

    /// <summary>Step 67: the morale bar's colour, fill and words.</summary>
    public static class MoraleMarkMath
    {
        /// <summary>The morale bar's fill colour - a clear blue, apart from the Athletics bands (green → red) and the fever (orange → yellow).</summary>
        public const string ColorHex = "#4C9BFFFF";

        /// <summary>The ALT label's morale line colour (a lighter blue - readable on the marker's dark text box).</summary>
        public const string LabelColorHex = "#8CC0FFFF";

        /// <summary>A man has a morale to show (the game's 0-100; −1 / NaN = none - tournaments, arenas).</summary>
        public static bool Known(double morale) => !double.IsNaN(morale) && morale >= 0;

        /// <summary>The bar's fill: morale ÷ 100, 0..1.</summary>
        public static float Fill(double morale) => !Known(morale) ? 0f : morale >= 100 ? 1f : (float)(morale / 100.0);

        public static string HiddenName(MoraleMarksHidden h) => h switch
        {
            MoraleMarksHidden.ModOff => "ModEnabled off (the master switch)",
            MoraleMarksHidden.SwitchedOff => "ShowMoraleMarks off",
            _ => "shown",
        };

        /// <summary>"morale 64" / "no morale" - log lines.</summary>
        public static string Describe(double morale) =>
            Known(morale) ? "morale " + Math.Round(morale).ToString(CultureInfo.InvariantCulture) : "no morale";
    }

    /// <summary>Step 67: why a formation's ALT label has no morale line - the master switch FIRST, in the order asked.</summary>
    public enum MoraleLabelHidden
    {
        None = 0,
        ModOff = 1,
        SwitchedOff = 2,
        NoMorale = 3,
        TooFar = 4,
        Capped = 5,
        Count = 6,
    }

    /// <summary>Step 67: the near formations' morale line ("morale 64, fresh 41%") - the switch, the range, the cap. Read live.</summary>
    public readonly struct MoraleLabelRules
    {
        public MoraleLabelRules(bool modEnabled, bool show, double metres, int max)
        {
            ModEnabled = modEnabled;
            Show = show;
            Metres = double.IsNaN(metres) || metres < 0 ? 0 : metres;
            Max = max < 1 ? 1 : max;
        }

        public static MoraleLabelRules From(TraxSettings s) => new MoraleLabelRules(s.ModEnabled, s.ShowMoraleOnLabels, s.MoraleLabelMetres, s.MoraleLabelMax);

        public bool ModEnabled { get; }

        /// <summary>ShowMoraleOnLabels.</summary>
        public bool Show { get; }

        /// <summary>MoraleLabelMetres - the marker's distance at most.</summary>
        public double Metres { get; }

        /// <summary>MoraleLabelMax - the nearest this many.</summary>
        public int Max { get; }

        public bool On => ModEnabled && Show;

        public string Describe() => !ModEnabled ? "morale line off (ModEnabled)" : !Show ? "morale line off (ShowMoraleOnLabels)"
            : "morale line (\"morale 64, fresh 41%\") on the nearest " + (Max == 1 ? "formation" : Max + " formations") + " within " + Metres.ToString("0", CultureInfo.InvariantCulture) + " m";
    }

    /// <summary>Step 67: the morale line's rules - who gets it (the nearest within the range), its words.</summary>
    public static class MoraleLabelMath
    {
        /// <summary>Why this formation has no morale line, its marker <paramref name="distance"/> away, <paramref name="nearer"/> eligible
        /// formations nearer than it this frame.</summary>
        public static MoraleLabelHidden HiddenFor(in MoraleLabelRules r, double meanMorale, double distance, int nearer)
        {
            if (!r.ModEnabled) return MoraleLabelHidden.ModOff;
            if (!r.Show) return MoraleLabelHidden.SwitchedOff;
            if (!MoraleMarkMath.Known(meanMorale)) return MoraleLabelHidden.NoMorale;
            if (double.IsNaN(distance) || distance > r.Metres) return MoraleLabelHidden.TooFar;
            if (nearer >= r.Max) return MoraleLabelHidden.Capped;
            return MoraleLabelHidden.None;
        }

        /// <summary>Eligible for the nearest-N ranking (the switch, a morale, in range) - the cap is decided by the rank.</summary>
        public static bool Eligible(in MoraleLabelRules r, double meanMorale, double distance) =>
            r.On && MoraleMarkMath.Known(meanMorale) && !double.IsNaN(distance) && distance <= r.Metres;

        public static int Round(double v) => double.IsNaN(v) ? -1 : (int)Math.Round(v, MidpointRounding.AwayFromZero);

        /// <summary>"morale 64, fresh 41%" - the mean morale (0-100) and the mean Athletics (the bar's fill, 0..1).</summary>
        public static string Text(int morale, int freshPercent) =>
            "morale " + morale.ToString(CultureInfo.InvariantCulture) + ", fresh " + freshPercent.ToString(CultureInfo.InvariantCulture) + "%";

        public static string HiddenName(MoraleLabelHidden h) => h switch
        {
            MoraleLabelHidden.ModOff => "ModEnabled off",
            MoraleLabelHidden.SwitchedOff => "ShowMoraleOnLabels off",
            MoraleLabelHidden.NoMorale => "no morale",
            MoraleLabelHidden.TooFar => "too far",
            MoraleLabelHidden.Capped => "beyond the nearest",
            _ => "shown",
        };
    }

    /// <summary>Step 67: the morale line over one mission - shown, the lowest seen, hidden by reason - the [summary] line.</summary>
    public sealed class MoraleLabelStats
    {
        private readonly long[] _hidden = new long[(int)MoraleLabelHidden.Count];
        private long _shown;
        private int _lowest = int.MaxValue;
        private string _lowestWho = string.Empty;
        private long _frames;
        private long _labelsInFrames;

        public long Shown => _shown;

        public void NoteShown(int morale, int fresh, int formationIndex, int teamType)
        {
            _shown++;
            if (morale >= 0 && morale < _lowest)
            {
                _lowest = morale;
                _lowestWho = (teamType == (int)MarkerTeam.Enemy ? "enemy " : teamType == (int)MarkerTeam.Ally ? "ally " : "yours ") + (formationIndex + 1) + ", fresh " + fresh + "%";
            }
        }

        public void NoteHidden(MoraleLabelHidden why)
        {
            int i = (int)why;
            if (i > 0 && i < _hidden.Length) _hidden[i]++;
        }

        public long Hidden(MoraleLabelHidden why) => _hidden[(int)why];

        public void NoteFrame(int shown)
        {
            _frames++;
            _labelsInFrames += shown;
        }

        public string SummaryLine(string viewName)
        {
            var hidden = new System.Text.StringBuilder();
            for (int i = 1; i < (int)MoraleLabelHidden.Count; i++)
            {
                if (_hidden[i] == 0) continue;
                hidden.Append(hidden.Length > 0 ? ", " : string.Empty).Append(MoraleLabelMath.HiddenName((MoraleLabelHidden)i)).Append(' ').Append(_hidden[i]);
            }
            double perFrame = _frames > 0 ? (double)_labelsInFrames / _frames : double.NaN;
            return "hud: " + viewName + " - morale line (step 67): pushed " + _shown + "x, " + (double.IsNaN(perFrame) ? "-" : perFrame.ToString("0.0", CultureInfo.InvariantCulture))
                + " formations a frame on the screen; the lowest " + (_lowest == int.MaxValue ? "-" : "morale " + _lowest + " (" + _lowestWho + ")")
                + "; hidden " + (hidden.Length > 0 ? hidden.ToString() : "never");
        }
    }
}

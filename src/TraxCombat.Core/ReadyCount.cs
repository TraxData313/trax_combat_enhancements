using System;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>Step 24: the ready line's colour - plain while most men are ready, yellow and red as fewer are.</summary>
    public enum ReadyBand
    {
        /// <summary>Above ReadyYellowBelowPercent ready - the text brushes' own colour.</summary>
        Plain,

        /// <summary>At or below ReadyYellowBelowPercent ready.</summary>
        Yellow,

        /// <summary>At or below ReadyRedBelowPercent ready.</summary>
        Red,

        Count,
    }

    /// <summary>Step 24: why a formation's ready line is not drawn (in the order <see cref="ReadyRules.Hidden"/> asks).</summary>
    public enum ReadyHidden
    {
        None,

        /// <summary>The master switch (the views are gone anyway).</summary>
        ModOff,

        /// <summary>AthleticsEnabled off - no bar, no brace.</summary>
        AthleticsOff,

        /// <summary>BraceEnabled off - nobody braces, so everyone is ready: no line (less noise).</summary>
        BraceOff,

        /// <summary>ShowReadyCount off.</summary>
        SwitchedOff,

        /// <summary>No tracked man in the formation.</summary>
        NoMen,

        Count,
    }

    /// <summary>
    /// Step 24 (Anton, 2026-09-29: "some indication above the squad of ready men, that are not resting in their current order
    /// status, somewhere near the athletics info on the ALT and on the formations view") - the ready line's rules, read LIVE
    /// from <see cref="TraxSettings"/>: shown only while the brace can happen at all (the master switch first, then
    /// AthleticsEnabled, BraceEnabled) and ShowReadyCount is on; its two colour thresholds in % of the men ready.
    /// </summary>
    public readonly struct ReadyRules
    {
        public ReadyRules(bool showReadyCount, bool modEnabled, bool athleticsEnabled, bool braceEnabled, int yellowBelowPercent, int redBelowPercent)
        {
            ShowReadyCount = showReadyCount;
            ModEnabled = modEnabled;
            AthleticsEnabled = athleticsEnabled;
            BraceEnabled = braceEnabled;
            YellowBelowPercent = yellowBelowPercent;
            RedBelowPercent = redBelowPercent;
        }

        public static ReadyRules From(TraxSettings s) =>
            new ReadyRules(s.ShowReadyCount, s.ModEnabled, s.AthleticsEnabled, s.BraceEnabled, s.ReadyYellowBelowPercent, s.ReadyRedBelowPercent);

        public bool ShowReadyCount { get; }
        public bool ModEnabled { get; }
        public bool AthleticsEnabled { get; }
        public bool BraceEnabled { get; }

        /// <summary>ReadyYellowBelowPercent - % of the men ready.</summary>
        public int YellowBelowPercent { get; }

        /// <summary>ReadyRedBelowPercent - % of the men ready.</summary>
        public int RedBelowPercent { get; }

        /// <summary>Why no ready line at all (None = shown for every formation with men). The master switch first.</summary>
        public ReadyHidden Hidden =>
            !ModEnabled ? ReadyHidden.ModOff
            : !AthleticsEnabled ? ReadyHidden.AthleticsOff
            : !BraceEnabled ? ReadyHidden.BraceOff
            : !ShowReadyCount ? ReadyHidden.SwitchedOff
            : ReadyHidden.None;

        public bool Shown => Hidden == ReadyHidden.None;

        /// <summary>"ready count on: yellow at or below 75%, red at or below 50% of the men ready" / "ready count hidden (BraceEnabled off - nobody braces)" - log lines.</summary>
        public string Describe() =>
            Shown
                ? "ready count on: yellow at or below " + YellowBelowPercent.ToString(CultureInfo.InvariantCulture) + "%, red at or below "
                  + RedBelowPercent.ToString(CultureInfo.InvariantCulture) + "% of the men ready"
                : "ready count hidden (" + ReadyMath.HiddenName(Hidden) + ")";
    }

    /// <summary>
    /// Step 24 - the ready line's pure logic: whether a formation gets one (<see cref="HiddenFor"/>), its text
    /// ("ready 34/50" - READY = not bracing, step 23's <see cref="FormationAthleticsStats.Ready"/> of its
    /// <see cref="FormationAthleticsStats.Total"/>: the same men the strip's other numbers count, the player left out) and its
    /// colour (<see cref="Band"/>: the most alarming wins).
    /// </summary>
    public static class ReadyMath
    {
        /// <summary>All (or most) ready: the text brushes' own colour (AgentHUD.Interaction.Text and NameMarker.Distance.Text
        /// are both #E8E8E8FF in the game's Mission.xml) - the line looks like its neighbours until it warns.</summary>
        public const string PlainHex = "#E8E8E8FF";

        /// <summary>The ready line's colour by band - the bars' own yellow and red.</summary>
        public static string ColorHex(ReadyBand b) => b switch
        {
            ReadyBand.Yellow => BarMath.YellowHex,
            ReadyBand.Red => BarMath.RedHex,
            _ => PlainHex,
        };

        /// <summary>The rules' reason, else NoMen for an empty formation, else None.</summary>
        public static ReadyHidden HiddenFor(in ReadyRules rules, in FormationAthleticsStats stats)
        {
            var h = rules.Hidden;
            if (h != ReadyHidden.None) return h;
            return stats.Total <= 0 ? ReadyHidden.NoMen : ReadyHidden.None;
        }

        /// <summary>The share of the men ready, 0..100 (100 for an empty formation - nothing to warn about).</summary>
        public static double Percent(int ready, int total) =>
            total <= 0 ? 100 : 100.0 * Math.Max(0, Math.Min(ready, total)) / total;

        /// <summary>Red at or below ReadyRedBelowPercent ready, else yellow at or below ReadyYellowBelowPercent, else plain.</summary>
        public static ReadyBand Band(in ReadyRules rules, int ready, int total)
        {
            if (total <= 0) return ReadyBand.Plain;
            double pct = Percent(ready, total);
            if (pct <= rules.RedBelowPercent + 1e-9) return ReadyBand.Red;
            if (pct <= rules.YellowBelowPercent + 1e-9) return ReadyBand.Yellow;
            return ReadyBand.Plain;
        }

        /// <summary>"ready 34/50".</summary>
        public static string Text(int ready, int total) =>
            "ready " + ready.ToString(CultureInfo.InvariantCulture) + "/" + total.ToString(CultureInfo.InvariantCulture);

        public static string HiddenName(ReadyHidden h) => h switch
        {
            ReadyHidden.ModOff => "ModEnabled off",
            ReadyHidden.AthleticsOff => "AthleticsEnabled off",
            ReadyHidden.BraceOff => "BraceEnabled off - nobody braces",
            ReadyHidden.SwitchedOff => "ShowReadyCount off",
            ReadyHidden.NoMen => "no men",
            _ => "shown",
        };

        public static string BandName(ReadyBand b) => b switch
        {
            ReadyBand.Yellow => "yellow",
            ReadyBand.Red => "red",
            _ => "plain",
        };
    }

    /// <summary>
    /// Step 24 - one view's ready lines over one mission for the [summary]: values shown by colour, the fewest ready seen (the
    /// share, the numbers, which formation), values hidden by reason. Main thread; nothing allocated per note.
    /// </summary>
    public sealed class ReadyCountStats
    {
        private readonly int[] _bands = new int[(int)ReadyBand.Count];
        private readonly int[] _hidden = new int[(int)ReadyHidden.Count];
        private double _lowestShare = double.NaN;
        private int _lowestReady;
        private int _lowestTotal;
        private int _lowestFormation = -1;
        private int _lowestTeamType = -1;

        public int Shown { get; private set; }

        public int ShownIn(ReadyBand b) => _bands[(int)b];

        public int HiddenBy(ReadyHidden h) => _hidden[(int)h];

        /// <summary>A ready line pushed. <paramref name="teamType"/>: -1 for the strip (the player's team), else the marker's
        /// (0 yours, 1 allies, 2 enemy).</summary>
        public void NoteShown(int ready, int total, ReadyBand band, int formationIndex, int teamType = -1)
        {
            Shown++;
            int b = (int)band;
            if (b >= 0 && b < _bands.Length) _bands[b]++;
            double share = ReadyMath.Percent(ready, total);
            if (double.IsNaN(_lowestShare) || share < _lowestShare)
            {
                _lowestShare = share;
                _lowestReady = ready;
                _lowestTotal = total;
                _lowestFormation = formationIndex;
                _lowestTeamType = teamType;
            }
        }

        public void NoteHidden(ReadyHidden why)
        {
            int i = (int)why;
            if (i > 0 && i < _hidden.Length) _hidden[i]++;
        }

        /// <summary><c>hud: orders strip - ready count (men not bracing, step 24): shown 120x (plain 80, yellow 30, red 10), the fewest
        /// ready 12/50 (24%, 1 Infantry); hidden 40x (BraceEnabled off - nobody braces 40)</c> (without the tag).</summary>
        public string SummaryLine(string viewName)
        {
            var sb = new StringBuilder("hud: ").Append(viewName).Append(" - ready count (men not bracing, step 24): ");
            if (Shown == 0) sb.Append("never shown");
            else
            {
                sb.Append("shown ").Append(Shown).Append("x (plain ").Append(_bands[(int)ReadyBand.Plain]).Append(", yellow ").Append(_bands[(int)ReadyBand.Yellow])
                  .Append(", red ").Append(_bands[(int)ReadyBand.Red]).Append("), the fewest ready ").Append(_lowestReady).Append('/').Append(_lowestTotal)
                  .Append(" (").Append(_lowestShare.ToString("0", CultureInfo.InvariantCulture)).Append("%, ")
                  .Append(_lowestTeamType >= 0 ? AltMarkerMath.TeamName(_lowestTeamType) + " " : string.Empty)
                  .Append(OrderStripMath.FormationName(_lowestFormation)).Append(')');
            }
            int hidden = 0;
            for (int i = 1; i < _hidden.Length; i++) hidden += _hidden[i];
            if (hidden == 0) return sb.Append(Shown == 0 ? " (no values pushed)" : "; hidden: never").ToString();
            sb.Append("; hidden ").Append(hidden).Append("x (");
            bool first = true;
            for (int i = 1; i < _hidden.Length; i++)
            {
                if (_hidden[i] == 0) continue;
                sb.Append(first ? string.Empty : ", ").Append(ReadyMath.HiddenName((ReadyHidden)i)).Append(' ').Append(_hidden[i]);
                first = false;
            }
            return sb.Append(')').ToString();
        }
    }
}

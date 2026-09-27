using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// One HUD view's numbers over one mission (steps 6-9) - the [summary] "hud:" lines, so the
    /// final playtest can tell from the log alone whether a bar was on screen, for how long, why
    /// it went away, which colours it showed and whether anything failed. Main thread only (the
    /// view's screen tick); allocation-free per frame.
    /// </summary>
    public sealed class HudStats
    {
        private readonly int[] _removedBy = new int[HudGate.ReasonCount];
        private readonly double[] _hiddenSeconds = new double[HudGate.ReasonCount];
        private readonly double[] _bandSeconds = new double[BarMath.BandCount];
        private readonly bool[] _bandSeen = new bool[BarMath.BandCount];
        private int _lastBand = -1;
        private bool _exhaustedNow;

        public HudStats(string viewName, string movieName, string toggleKey)
        {
            ViewName = viewName;
            MovieName = movieName;
            ToggleKey = toggleKey;
        }

        /// <summary>"player bar".</summary>
        public string ViewName { get; }

        /// <summary>"TraxPlayerAthleticsBar" - the prefab.</summary>
        public string MovieName { get; }

        /// <summary>"ShowPlayerBar" - the view's own switch.</summary>
        public string ToggleKey { get; }

        /// <summary>Unpaused seconds the view was ticked (≈ the mission after the view attached).</summary>
        public double TickedSeconds { get; private set; }

        /// <summary>Seconds its layer was on screen (built and not suspended).</summary>
        public double VisibleSeconds { get; private set; }

        public int LayersCreated { get; private set; }

        public int LayersRemoved { get; private set; }

        public int Refreshes { get; private set; }

        public int Errors { get; private set; }

        /// <summary>An error disabled the view (the site), or null.</summary>
        public string? FailedSite { get; private set; }

        public double FailedAt { get; private set; }

        /// <summary>Why the movie did not load, or null.</summary>
        public string? MovieFailure { get; private set; }

        public int BandChanges { get; private set; }

        public int ExhaustedEntries { get; private set; }

        public double ExhaustedSeconds { get; private set; }

        public double WoundedSeconds { get; private set; }

        /// <summary>The lowest usable share shown (1 = never wounded).</summary>
        public double LowestUsable { get; private set; } = 1.0;

        /// <summary>Layers removed for this reason.</summary>
        public int RemovedBy(HudHide why) => _removedBy[Index(why)];

        /// <summary>Seconds hidden for this reason.</summary>
        public double HiddenSeconds(HudHide why) => _hiddenSeconds[Index(why)];

        /// <summary>Seconds on screen in this colour.</summary>
        public double BandSeconds(BarBand band) => _bandSeconds[(int)band];

        /// <summary>This colour was shown at least once.</summary>
        public bool BandSeen(BarBand band) => _bandSeen[(int)band];

        // ------------------------------------------------------------------ feeding

        /// <summary>One unpaused frame: <paramref name="hide"/> = the gate's answer, <paramref name="onScreen"/>
        /// = the layer is up and not suspended.</summary>
        public void AddTick(double dt, HudHide hide, bool onScreen)
        {
            if (!(dt > 0)) return;
            TickedSeconds += dt;
            if (onScreen) VisibleSeconds += dt;
            else _hiddenSeconds[Index(hide)] += dt;
        }

        /// <summary>One on-screen frame of a bar in <paramref name="band"/>.</summary>
        public void AddBarTime(double dt, BarBand band, bool exhausted, double usable)
        {
            if (!(dt > 0)) return;
            _bandSeconds[(int)band] += dt;
            if (exhausted) ExhaustedSeconds += dt;
            if (usable < 1.0 - AthleticsMath.Epsilon) WoundedSeconds += dt;
        }

        /// <summary>The colour pushed at a refresh. True the FIRST time this colour appears this
        /// mission (the view logs a snapshot then); a change from the last colour is counted.</summary>
        public bool NoteBand(BarBand band)
        {
            int b = (int)band;
            if (_lastBand >= 0 && _lastBand != b) BandChanges++;
            _lastBand = b;
            if (_bandSeen[b]) return false;
            _bandSeen[b] = true;
            return true;
        }

        /// <summary>The empty state pushed at a refresh. True the FIRST time the bar shows empty
        /// this mission; every entry is counted.</summary>
        public bool NoteExhausted(bool exhausted)
        {
            bool entered = exhausted && !_exhaustedNow;
            _exhaustedNow = exhausted;
            if (!entered) return false;
            ExhaustedEntries++;
            return ExhaustedEntries == 1;
        }

        /// <summary>The usable share pushed at a refresh. True the FIRST time a wounded (capped) bar
        /// shows this mission; the lowest is kept.</summary>
        public bool NoteUsable(double usable)
        {
            if (double.IsNaN(usable) || usable >= 1.0 - AthleticsMath.Epsilon) return false;
            bool first = LowestUsable >= 1.0 - AthleticsMath.Epsilon;
            if (usable < LowestUsable) LowestUsable = usable;
            return first;
        }

        public void AddRefresh() => Refreshes++;

        public void LayerCreated() => LayersCreated++;

        public void LayerRemoved(HudHide why)
        {
            LayersRemoved++;
            _removedBy[Index(why)]++;
        }

        public void Failed(string site, double at)
        {
            Errors++;
            if (FailedSite != null) return;
            FailedSite = site;
            FailedAt = at;
        }

        public void MovieFailed(string detail, double at)
        {
            MovieFailure = detail;
            FailedAt = at;
        }

        // ------------------------------------------------------------------ the summary

        /// <summary>The [summary] lines (without the tag): one for the layer, one for the colours
        /// when a bar was on screen at all.</summary>
        public List<string> SummaryLines()
        {
            var lines = new List<string>(2);
            var sb = new StringBuilder();
            sb.Append("hud: ").Append(ViewName).Append(" (movie ").Append(MovieName).Append(") - ");
            if (MovieFailure != null)
                sb.Append("movie FAILED to load at ").Append(S1(FailedAt)).Append(" s: ").Append(MovieFailure).Append(" - never shown; ");
            if (LayersCreated == 0)
            {
                sb.Append("never on screen");
                string hidden = HiddenBreakdown();
                if (hidden.Length > 0) sb.Append(" (hidden: ").Append(hidden).Append(')');
            }
            else
            {
                sb.Append("on screen ").Append(S1(VisibleSeconds)).Append(" s of ").Append(S1(TickedSeconds)).Append(" s (")
                    .Append(Pct(VisibleSeconds, TickedSeconds)).Append("); layer built ").Append(LayersCreated).Append("x, removed ")
                    .Append(LayersRemoved).Append('x');
                string removed = RemovedBreakdown();
                if (removed.Length > 0) sb.Append(" (").Append(removed).Append(')');
                string hidden = HiddenBreakdown();
                if (hidden.Length > 0) sb.Append("; hidden: ").Append(hidden);
                sb.Append("; ").Append(Refreshes).Append(" refreshes");
            }
            sb.Append("; errors ").Append(Errors);
            if (FailedSite != null)
                sb.Append(" - DISABLED at ").Append(S1(FailedAt)).Append(" s after an error in ").Append(FailedSite).Append(" (the battle went on without it)");
            lines.Add(sb.ToString());

            double barTime = 0;
            for (int i = 0; i < _bandSeconds.Length; i++) barTime += _bandSeconds[i];
            if (barTime > 0)
            {
                sb.Clear();
                sb.Append("hud: ").Append(ViewName).Append(" colours on screen - ");
                for (int i = 0; i < _bandSeconds.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(BarMath.Name((BarBand)i)).Append(' ').Append(S1(_bandSeconds[i])).Append(" s");
                    if (_bandSeconds[i] > 0) sb.Append(" (").Append(Pct(_bandSeconds[i], barTime)).Append(')');
                }
                sb.Append("; ").Append(BandChanges).Append(" colour changes; exhausted shown ").Append(ExhaustedEntries).Append('x');
                if (ExhaustedEntries > 0) sb.Append(" (").Append(S1(ExhaustedSeconds)).Append(" s)");
                if (WoundedSeconds > 0)
                    sb.Append("; wounded part shown ").Append(S1(WoundedSeconds)).Append(" s (lowest usable ")
                        .Append(Math.Round(LowestUsable * 100).ToString("0", CultureInfo.InvariantCulture)).Append("% - the last ")
                        .Append(Math.Round((1 - LowestUsable) * 100).ToString("0", CultureInfo.InvariantCulture)).Append("% of the bar dark)");
                else
                    sb.Append("; never wounded (no dark part)");
                lines.Add(sb.ToString());
            }
            return lines;
        }

        private string RemovedBreakdown()
        {
            var sb = new StringBuilder();
            for (int i = 1; i < _removedBy.Length; i++)
            {
                if (_removedBy[i] == 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(HudGate.ShortName((HudHide)i, ToggleKey)).Append(' ').Append(_removedBy[i]);
            }
            return sb.ToString();
        }

        private string HiddenBreakdown()
        {
            var sb = new StringBuilder();
            for (int i = 1; i < _hiddenSeconds.Length; i++)
            {
                if (_hiddenSeconds[i] <= 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(HudGate.ShortName((HudHide)i, ToggleKey)).Append(' ').Append(S1(_hiddenSeconds[i])).Append(" s");
            }
            return sb.ToString();
        }

        private static int Index(HudHide h)
        {
            int i = (int)h;
            return i < 0 || i >= HudGate.ReasonCount ? HudGate.ReasonCount - 1 : i;
        }

        private static string S1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        private static string Pct(double part, double whole) =>
            (whole > 0 ? Math.Round(100.0 * part / whole) : 0).ToString("0", CultureInfo.InvariantCulture) + "%";
    }
}

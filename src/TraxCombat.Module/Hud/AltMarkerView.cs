using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Library;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Hud
{
    /// <summary>
    /// DESIGN §3 item 3 (step 20, Anton: "I see the Athletics and health numbers above the troops when I hold ALT")
    /// - while the game shows its FORMATION MARKERS (the troop count, the icon, the distance - ALT held or the
    /// orders menu open), each marked formation also gets, centred just under its marker, "72% ± 8" (the men's
    /// average Athletics ± spread, in the colour of their average f) and "HP 81%", over an optional slim bar.
    /// Yours and your allies' always; the enemy's with AltMarkersShowEnemy. Step 24: under the bar, "ready 34/50" - the
    /// men NOT bracing (step 23's read API: Ready of Total; in your formations you are left out, so it may read one fewer
    /// than vanilla's count above), plain / yellow / red (ShowReadyCount; hidden while bracing is off - <see cref="ReadyRules"/>).
    ///
    /// WHEN: exactly while vanilla shows them - its own flag (<c>MissionFormationMarkerVM.IsEnabled</c>) read live
    /// every frame; when it cannot be read, vanilla's rule copied (ALT held or the orders menu open). Plus the
    /// common gates (ModEnabled first, AthleticsEnabled, ShowAltMarkerStats, Hide battle UI, photo mode, a fight).
    /// The player need not be on the field: vanilla shows its markers after he falls too.
    ///
    /// ALIGNMENT (AI_NOTES "Step 20"): the markers are READ LIVE, never patched - each target's point as vanilla
    /// projected it THIS frame and its widget's live size (paired by the exact point,
    /// <see cref="AltMarkerMath.Pair"/>); the label goes to the marker's bottom centre (<see cref="AltMarkerMath.Place"/>)
    /// the same frame. A marker vanilla hides (behind the camera, faded, gone) gets no label; a pinned one keeps it.
    /// FALLBACKS (each logged with its reason, counted): widgets not found → the game's points with a nominal
    /// marker size; no marker layer / data → our own projection of the same point (<see cref="ProjectedFormationMarkers"/>)
    /// for the rest of that show. The next show tries the live markers again.
    ///
    /// Log: the first show per mission in full (technique, the markers found - which formations, whose, where -
    /// the labels placed or why none), the first values, every fallback (the first of each reason in full), the
    /// [summary] line. Showing and hiding with the key is quiet after the first (verbose).
    /// </summary>
    public sealed class AltMarkerView : TraxHudView
    {
        /// <summary>The prefab: module\GUI\Prefabs\TraxAltMarkers.xml.</summary>
        public const string Movie = "TraxAltMarkers";

        /// <summary>Seconds (real time) the first show may wait for a laid-out marker before its line is written anyway.</summary>
        private const double FirstLogGraceSeconds = 0.5;

        private const int FallbackKinds = 5;

        private readonly AltMarkerFrame _frame = new AltMarkerFrame();
        private readonly MarkerSight[] _sights = new MarkerSight[AltMarkerFrame.MaxMarkers];
        private readonly bool[] _fallbackLoggedEver = new bool[FallbackKinds];
        private readonly bool[] _fallbackThisShow = new bool[FallbackKinds];
        private IFormationMarkerSource? _live;
        private IFormationMarkerSource? _projection;
        private AltMarkersVM? _vm;
        private bool _projecting;
        private bool _techniqueNoted;
        private AltMarkerTechnique _technique;
        private int _showNo;
        private int _settingsVersion = -1;
        private int _countedVersion = -1;
        private int _shownNow;
        private bool _firstLogged;
        private bool _valuesLogged;
        private double _waited;

        public AltMarkerView()
            : base("ALT markers", Movie, SettingsSchema.ShowAltMarkerStats)
        {
            Stats.ConditionName = "markers hidden";
        }

        /// <summary>The live ViewModel (null while no layer is up) - the offline smoke reads it.</summary>
        internal AltMarkersVM? CurrentViewModel => _vm;

        /// <summary>This mission's numbers ([summary]).</summary>
        internal AltMarkerStats MarkerStats { get; } = new AltMarkerStats();

        /// <summary>Step 24: this mission's ready counts ([summary]).</summary>
        internal ReadyCountStats ReadyStats { get; } = new ReadyCountStats();

        /// <summary>This show uses our own projection.</summary>
        internal bool Projecting => _projecting;

        /// <summary>This show's technique (None before its first frame).</summary>
        internal AltMarkerTechnique Technique => _technique;

        /// <summary>The engine side (real: the attach; the smoke: stand-ins).</summary>
        internal void UseSources(IFormationMarkerSource live, IFormationMarkerSource projection)
        {
            _live = live;
            _projection = projection;
        }

        protected override bool NeedsPlayer => false;

        protected override bool ReadsIndicatorKey => true;

        protected override bool QuietConditionToggles => true;

        protected override string ViewConditionText => "the game's formation markers are hidden (ALT not held, the orders menu closed)";

        protected override string? ViewConditionWhen => "while the game shows its formation markers (ALT held or the orders menu open - read live from its own marker layer)";

        /// <summary>Vanilla's own flag when it can be read; else its rule copied.</summary>
        protected override bool ViewConditionMet(in HudFrame f)
        {
            if (_live != null && _live.TryReadShown(out bool shown)) return shown;
            return f.ShowIndicatorsKey || f.OrderMenuOpen;
        }

        protected override ViewModel CreateDataSource(in HudFrame f)
        {
            if (_live == null || _projection == null)
                throw new InvalidOperationException("no marker sources - the view was not attached through AthleticsLogic");
            _vm = new AltMarkersVM();
            _showNo++;
            MarkerStats.NoteShown();
            _projecting = false;
            _techniqueNoted = false;
            _technique = AltMarkerTechnique.None;
            _settingsVersion = -1;
            _countedVersion = -1;
            _shownNow = 0;
            Array.Clear(_fallbackThisShow, 0, _fallbackThisShow.Length);
            return _vm;
        }

        protected override void OnLayerGone()
        {
            _vm = null;
            _shownNow = 0;
        }

        internal override void AddSummaryLines(List<string> lines)
        {
            lines.Add(MarkerStats.SummaryLine(Stats.Errors));
            lines.Add(ReadyStats.SummaryLine(ViewName));
        }

        // ------------------------------------------------------------------ every frame: read, pair, place

        protected override void OnLayerFrame(in HudFrame f)
        {
            var vm = _vm;
            if (vm == null) return;
            var s = TraxSettings.Shared;
            var layout = AltMarkerLayout.From(s);

            MarkerRead read = MarkerRead.Unavailable;
            if (!_projecting)
            {
                read = _live!.Read(_frame, out var why);
                if (read == MarkerRead.Unavailable)
                {
                    _projecting = true;
                    Fallback(in f, why, _live.Describe() + " - our own projection of the same point (the formation's median + 3 m), the game's rule copied (ALT held or the orders menu open), until the markers go; the next show tries the game's markers again");
                }
            }
            if (_projecting) read = _projection!.Read(_frame, out _);

            if (s.Version != _settingsVersion)
            {
                _settingsVersion = s.Version; // a setting moved (MCM, mid-battle): the layout now, the values below
                vm.SetLayout(in layout, _frame.Scale);
            }

            var technique = SizeMarkers(in f, read);
            if (!_techniqueNoted)
            {
                _techniqueNoted = true;
                _technique = technique;
                MarkerStats.NoteTechnique(technique);
            }

            PlaceLabels(in f, vm, s, in layout);
        }

        /// <summary>Pairs the live widgets (or sets nominal sizes); returns this frame's technique.</summary>
        private AltMarkerTechnique SizeMarkers(in HudFrame f, MarkerRead read)
        {
            switch (read)
            {
                case MarkerRead.Live:
                    int paired = AltMarkerMath.Pair(_frame);
                    if (paired >= _frame.Count) return AltMarkerTechnique.Live;
                    bool distance = _live!.DistanceShown;
                    // the text only for the show's first such frame (no allocation per frame)
                    var missing = _fallbackThisShow[(int)AltMarkerFallback.Unpaired] ? null : new StringBuilder();
                    for (int i = 0; i < _frame.Count; i++)
                    {
                        if (_frame.Markers[i].HasWidget) continue;
                        AltMarkerMath.Nominal(ref _frame.Markers[i], _frame.Scale, distance);
                        missing?.Append(missing.Length > 0 ? " | " : string.Empty).Append(AltMarkerMath.DescribeMarker(in _frame.Markers[i]));
                    }
                    if (missing != null)
                        Fallback(in f, AltMarkerFallback.Unpaired, (_frame.Count - paired) + " of " + _frame.Count + " markers had no widget at their point ("
                            + _live.Describe() + "): " + missing + " - a nominal marker size for those");
                    return AltMarkerTechnique.GamePoints;

                case MarkerRead.PointsOnly:
                    bool shownDistance = _live!.DistanceShown;
                    for (int i = 0; i < _frame.Count; i++) AltMarkerMath.Nominal(ref _frame.Markers[i], _frame.Scale, shownDistance);
                    if (!_fallbackThisShow[(int)AltMarkerFallback.NoWidgets])
                        Fallback(in f, AltMarkerFallback.NoWidgets, _live.Describe() + " - the game's points with a nominal marker size ("
                            + OrderStripMath.Px(AltMarkerMath.NominalMarkerWidth) + " x " + OrderStripMath.Px(shownDistance ? AltMarkerMath.NominalMarkerHeight : AltMarkerMath.NominalMarkerHeightNoDistance)
                            + " UI px) until the markers go");
                    return AltMarkerTechnique.GamePoints;

                default:
                    return AltMarkerTechnique.OwnProjection;
            }
        }

        private void PlaceLabels(in HudFrame f, AltMarkersVM vm, TraxSettings s, in AltMarkerLayout layout)
        {
            bool showEnemy = s.AltMarkersShowEnemy;
            int version = AthleticsLogic.FormationStatsVersion;
            var labels = vm.Labels;
            for (int i = 0; i < labels.Count; i++) labels[i].SeenThisFrame = false;
            int shown = 0, pinned = 0;
            bool pushed = false, notLaidOut = false;
            for (int i = 0; i < _frame.Count; i++)
            {
                ref var m = ref _frame.Markers[i];
                var sight = AltMarkerMath.Sight(in m, showEnemy, _frame.ScreenWidth, _frame.ScreenHeight);
                _sights[i] = sight;
                if (sight == MarkerSight.NotLaidOut) notLaidOut = true;
                if (sight != MarkerSight.Shown && sight != MarkerSight.Pinned) continue;
                var label = vm.LabelFor(m.Key, in layout, _frame.Scale, out _);
                if (label == null) continue;
                AltMarkerMath.Place(in m, sight, in layout, _frame.Scale, out float x, out float y, out float w);
                label.Place(x, y, w);
                if (label.PushedStats != version || label.PushedSettings != s.Version)
                {
                    PushValues(label, in m, s, version);
                    pushed = true;
                }
                label.SeenThisFrame = true;
                label.IsShown = true;
                shown++;
                if (sight == MarkerSight.Pinned) pinned++;
                MarkerStats.NoteLabelled(m.Key, m.TeamType);
            }
            for (int i = 0; i < labels.Count; i++)
                if (!labels[i].SeenThisFrame) labels[i].IsShown = false;
            if (pushed && version != _countedVersion)
            {
                _countedVersion = version;
                MarkerStats.NoteValuesPushed();
            }
            MarkerStats.NoteFrame(shown, pinned, _frame.Dropped);
            MarkerStats.AddOnScreen(f.Dt);
            _shownNow = shown;

            if (_firstLogged) return;
            _waited += f.Dt;
            if ((shown > 0 && !notLaidOut) || _waited > FirstLogGraceSeconds) LogFirstShow(in f, vm, in layout, shown);
        }

        private void PushValues(AltMarkerLabelVM label, in AltMarker m, TraxSettings s, int version)
        {
            var rules = BarRules.From(s);
            float peak = BarMath.PeakLine(s.AthleticsPeakPercent / 100.0);
            bool spread = s.ShowFormationSpread;
            double k = s.FormationSpreadStdDevs;
            var st = m.Stats;
            OrderStripMath.Numbers(in st, spread, k, out int mean, out int sp);
            OrderStripMath.Band(in st, spread, k, out float low, out float high);
            int hp = s.ShowFormationHealth ? OrderStripMath.HealthPercent(st.MeanHealth) : -1;
            label.SetValues(BarMath.Band(in rules, st.MeanPeakShare), BarMath.Fill(st.MeanFraction), low, high, peak, mean, sp, hp);
            var ready = ReadyRules.From(s);
            var hidden = ReadyMath.HiddenFor(in ready, in st);
            if (hidden == ReadyHidden.None)
            {
                var band = ReadyMath.Band(in ready, st.Ready, st.Total);
                label.SetReady(true, st.Ready, st.Total, band);
                ReadyStats.NoteShown(st.Ready, st.Total, band, m.FormationIndex, m.TeamType);
            }
            else
            {
                label.SetReady(false, 0, 0, ReadyBand.Plain);
                ReadyStats.NoteHidden(hidden);
            }
            label.PushedStats = version;
            label.PushedSettings = s.Version;
        }

        /// <summary>A fallback: counted once per show per reason; the first of each reason per mission in full, the
        /// rest verbose.</summary>
        private void Fallback(in HudFrame f, AltMarkerFallback why, string text)
        {
            int i = (int)why;
            if (i <= 0 || i >= FallbackKinds || _fallbackThisShow[i]) return;
            _fallbackThisShow[i] = true;
            MarkerStats.NoteFallback(why, text, f.Now);
            string line = ViewName + ": " + (why == AltMarkerFallback.Unpaired ? "a marker without its widget" : "FALLBACK") + " at " + S1(f.Now)
                + " s (show #" + _showNo + ") - " + text;
            if (!_fallbackLoggedEver[i])
            {
                _fallbackLoggedEver[i] = true;
                TraxLog.Limited("hud", line, "hud-alt");
            }
            else if (TraxLog.VerboseWants("hud-alt-fallback"))
            {
                TraxLog.Verbose("hud", line, "hud-alt-fallback");
            }
        }

        // ------------------------------------------------------------------ every HudRefreshSeconds: the values line

        protected override bool Refresh(in HudFrame f, bool first)
        {
            if (_vm == null || _shownNow == 0) return false;
            if (!_valuesLogged)
            {
                _valuesLogged = true;
                var s = TraxSettings.Shared;
                TraxLog.Limited("hud", ViewName + ": values at " + S1(f.Now) + " s (show #" + _showNo + "): " + DescribeValues()
                    + " - ± is " + (s.ShowFormationSpread ? F2(s.FormationSpreadStdDevs) + " std" : "off (ShowFormationSpread)")
                    + ", health " + (s.ShowFormationHealth ? "on" : "off (ShowFormationHealth)") + ", enemy " + (s.AltMarkersShowEnemy ? "on" : "off (AltMarkersShowEnemy)") + ", " + ReadyRules.From(s).Describe(), "hud-alt-values");
            }
            else if (first && TraxLog.VerboseWants("hud-alt-values"))
            {
                TraxLog.Verbose("hud", ViewName + ": values at " + S1(f.Now) + " s (show #" + _showNo + "): " + DescribeValues(), "hud-alt-values");
            }
            return true;
        }

        // ------------------------------------------------------------------ log text (allocates - log lines only)

        private void LogFirstShow(in HudFrame f, AltMarkersVM vm, in AltMarkerLayout layout, int shown)
        {
            _firstLogged = true;
            string source = _projecting ? _projection!.Describe() : _live!.Describe();
            MarkerStats.Technique = AltMarkerMath.TechniqueName(_technique) + " (" + source + ")";
            var markers = new StringBuilder();
            var placed = new StringBuilder();
            var none = new StringBuilder();
            int yours = 0, allies = 0, enemy = 0;
            for (int i = 0; i < _frame.Count; i++)
            {
                ref var m = ref _frame.Markers[i];
                if (m.TeamType == (int)MarkerTeam.Enemy) enemy++;
                else if (m.TeamType == (int)MarkerTeam.Ally) allies++;
                else yours++;
                markers.Append(markers.Length > 0 ? " | " : string.Empty).Append(AltMarkerMath.DescribeMarker(in m));
                var sight = _sights[i];
                string name = AltMarkerMath.TeamName(m.TeamType) + " " + OrderStripMath.FormationName(m.FormationIndex);
                if (sight == MarkerSight.Shown || sight == MarkerSight.Pinned)
                {
                    var label = FindLabel(vm, m.Key);
                    placed.Append(placed.Length > 0 ? " | " : string.Empty).Append(name);
                    if (label != null)
                        placed.Append(" at (").Append(OrderStripMath.Px(label.PositionX + label.BoxWidth / 2f)).Append(", ").Append(OrderStripMath.Px(label.PositionY)).Append(')');
                    if (sight == MarkerSight.Pinned) placed.Append(" (pinned at the screen's edge)");
                }
                else
                {
                    none.Append(none.Length > 0 ? " | " : string.Empty).Append(name).Append(" - ").Append(AltMarkerMath.SightName(sight));
                }
            }
            TraxLog.Limited("hud", ViewName + ": first shown at " + S1(f.Now) + " s (show #" + _showNo + ") - technique: " + MarkerStats.Technique
                + "; screen " + OrderStripMath.Px(_frame.ScreenWidth) + " x " + OrderStripMath.Px(_frame.ScreenHeight) + " px, UI scale " + F2(_frame.Scale)
                + "; " + _frame.Count + (_frame.Count == 1 ? " marker" : " markers") + " (yours " + yours + ", allies " + allies + ", enemy " + enemy + "): "
                + (markers.Length > 0 ? markers.ToString() : "none")
                + "; labels (" + layout.Describe() + ", centre x / top y): " + (shown > 0 ? placed.ToString() : "none placed after " + F2(_waited) + " s")
                + (none.Length > 0 ? "; no label: " + none : string.Empty), "hud-alt");
        }

        private static AltMarkerLabelVM? FindLabel(AltMarkersVM vm, int key)
        {
            // log only: the label keyed to this formation (LabelFor would add one)
            for (int i = 0; i < vm.Labels.Count; i++)
                if (vm.Labels[i].IsShown && vm.Labels[i].SeenThisFrame && KeyOf(vm, i) == key) return vm.Labels[i];
            return null;
        }

        private static int KeyOf(AltMarkersVM vm, int index) => vm.KeyAt(index);

        private string DescribeValues()
        {
            var vm = _vm!;
            var sb = new StringBuilder();
            for (int i = 0; i < _frame.Count; i++)
            {
                var sight = _sights[i];
                if (sight != MarkerSight.Shown && sight != MarkerSight.Pinned) continue;
                ref var m = ref _frame.Markers[i];
                var label = FindLabel(vm, m.Key);
                if (label == null) continue;
                sb.Append(sb.Length > 0 ? " | " : string.Empty).Append(AltMarkerMath.DescribeValues(in m, label.ShownMean, label.ShownSpread, label.ShownHealth, label.ShownReady, label.ShownTotal));
            }
            return sb.Length == 0 ? "no labelled formation" : sb.ToString();
        }
    }
}

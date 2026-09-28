using System.Collections.Generic;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Mission;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.MountAndBlade.ViewModelCollection.HUD.FormationMarker;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Hud
{
    /// <summary>How a marker source read one frame.</summary>
    internal enum MarkerRead
    {
        /// <summary>The game's markers: their points and their widgets (the view pairs them).</summary>
        Live,

        /// <summary>The game's points, but no marker widget found - the view gives them a nominal size.</summary>
        PointsOnly,

        /// <summary>Our own projection (the fallback source) - nominal sizes already set.</summary>
        Projected,

        /// <summary>Nothing to read (no marker layer / data) - the view falls back to the projection.</summary>
        Unavailable,
    }

    /// <summary>
    /// The engine side of step 20's alignment behind one seam - the offline smoke plays it with made-up markers,
    /// like step 9's <see cref="IOrderCardSource"/>. <see cref="GauntletFormationMarkers"/> reads vanilla's markers;
    /// <see cref="ProjectedFormationMarkers"/> is the fallback.
    /// </summary>
    internal interface IFormationMarkerSource
    {
        /// <summary>Vanilla's own "markers shown" (<c>MissionFormationMarkerVM.IsEnabled</c>: ALT held or the
        /// orders menu open). False when this source cannot tell (then the view copies the rule). Allocation-free.</summary>
        bool TryReadShown(out bool shown);

        /// <summary>One frame into <paramref name="frame"/> (targets with their stats, and the marker widgets
        /// unpaired). <paramref name="why"/> names what fell short when the answer is not <see cref="MarkerRead.Live"/>
        /// / <see cref="MarkerRead.Projected"/>. Allocation-free.</summary>
        MarkerRead Read(AltMarkerFrame frame, out AltMarkerFallback why);

        /// <summary>The game's "Show formation distances" option (the markers' distance row) - for nominal sizes.</summary>
        bool DistanceShown { get; }

        /// <summary>Where it looked and what it found (log lines only - allocates).</summary>
        string Describe();
    }

    /// <summary>
    /// The real reader (AI_NOTES "Step 20"): vanilla's marker layer ("MissionFormationMarker", built by
    /// MissionGauntletFormationMarker) found with the PUBLIC <c>ScreenBase.FindLayer</c>; its movie
    /// ("FormationMarker") through the public <c>GetMovieIdentifier</c>, whose DataSource IS the game's
    /// <see cref="MissionFormationMarkerVM"/> - <c>IsEnabled</c> is vanilla's own "shown", each of its
    /// <c>Targets</c> carries the formation, the point vanilla projected this frame, WSign, the distance, the
    /// team type and the count. The marker widgets (<see cref="FormationMarkerListPanel"/>) are read under the
    /// layer's root every frame: the bound Position, the live Size, AlphaFactor, IsTargetingAFormation and their
    /// own offsets. Nothing is patched, injected or reflected - only read. RTS Camera's four patches on the
    /// markers are followed for free (it hides a marker, fades it, moves the camera - we read the result).
    /// </summary>
    internal sealed class GauntletFormationMarkers : IFormationMarkerSource
    {
        /// <summary>The layer and movie MissionGauntletFormationMarker builds (engine facts).</summary>
        public const string LayerName = "MissionFormationMarker";

        public const string MovieName = "FormationMarker";

        private readonly MissionScreen _screen;
        private readonly List<FormationMarkerListPanel> _panels = new List<FormationMarkerListPanel>(AltMarkerFrame.MaxMarkers);
        private GauntletLayer? _layer;
        private MissionFormationMarkerVM? _vm;
        private AltMarkerFallback _why;
        private int _lastTargets;
        private int _lastWidgets;

        public GauntletFormationMarkers(MissionScreen screen)
        {
            _screen = screen;
        }

        public bool DistanceShown => ManagedOptions.GetConfig(ManagedOptions.ManagedOptionsType.ShowFormationDistances) > 1E-05f;

        /// <summary>The layer on the screen now and its marker ViewModel (re-resolved when the layer changes -
        /// vanilla rebuilds it when Hide battle UI goes off and on).</summary>
        private bool Resolve()
        {
            var layer = _screen.FindLayer<GauntletLayer>(LayerName);
            if (layer == null)
            {
                _layer = null;
                _vm = null;
                _why = AltMarkerFallback.NoLayer;
                return false;
            }
            if (!ReferenceEquals(layer, _layer) || _vm == null)
            {
                _layer = layer;
                _vm = layer.GetMovieIdentifier(MovieName)?.DataSource as MissionFormationMarkerVM;
            }
            if (_vm == null)
            {
                _why = AltMarkerFallback.NoMarkerData;
                return false;
            }
            return true;
        }

        public bool TryReadShown(out bool shown)
        {
            shown = false;
            if (!Resolve()) return false;
            shown = _vm!.IsEnabled;
            return true;
        }

        public MarkerRead Read(AltMarkerFrame frame, out AltMarkerFallback why)
        {
            frame.Clear();
            if (!Resolve())
            {
                why = _why;
                return MarkerRead.Unavailable;
            }
            var context = _layer!.UIContext;
            if (context != null)
            {
                var page = context.EventManager.PageSize;
                frame.ScreenWidth = page.X;
                frame.ScreenHeight = page.Y;
                frame.Scale = context.CustomScale > 0f ? context.CustomScale : 1f;
            }

            var targets = _vm!.Targets;
            int n = targets?.Count ?? 0;
            for (int i = 0; i < n; i++)
            {
                var t = targets![i];
                var formation = t?.Formation;
                if (formation == null) continue;
                if (frame.Count >= AltMarkerFrame.MaxMarkers)
                {
                    frame.Dropped++;
                    continue;
                }
                ref var m = ref frame.Markers[frame.Count++];
                m = default;
                int k = (int)formation.FormationIndex;
                m.Key = AltMarkerMath.Key(formation.Team?.TeamIndex ?? -1, k);
                m.TeamType = t!.TeamType;
                m.FormationIndex = k;
                var p = t.ScreenPosition;
                m.PointX = p.x;
                m.PointY = p.y;
                m.WSign = t.WSign;
                m.Distance = t.Distance;
                m.Men = t.Size;
                if (AthleticsLogic.TryGetFormationStats(formation, out var stats)) m.Stats = stats;
            }

            _panels.Clear();
            Collect(context?.Root);
            for (int j = 0; j < _panels.Count; j++)
            {
                var panel = _panels[j];
                ref var w = ref frame.Widgets[frame.WidgetCount++];
                var pos = panel.Position;
                var size = panel.Size;
                w.PositionX = pos.x;
                w.PositionY = pos.y;
                w.Width = size.X;
                w.Height = size.Y;
                w.Alpha = panel.AlphaFactor; // the distance fade (set even behind the camera); ≤ 0.05 = hidden
                w.Targeting = panel.IsTargetingAFormation;
                w.OffsetX = panel.ScaledPositionXOffset;
                w.OffsetY = panel.ScaledPositionYOffset;
                w.Used = false;
            }
            _lastTargets = frame.Count;
            _lastWidgets = frame.WidgetCount;
            if (frame.Count > 0 && frame.WidgetCount == 0)
            {
                why = AltMarkerFallback.NoWidgets;
                return MarkerRead.PointsOnly;
            }
            why = AltMarkerFallback.None;
            return MarkerRead.Live;
        }

        /// <summary>Depth-first; a marker's own children are never markers.</summary>
        private void Collect(Widget? w)
        {
            if (w == null || _panels.Count >= AltMarkerFrame.MaxMarkers) return;
            if (w is FormationMarkerListPanel panel)
            {
                _panels.Add(panel);
                return;
            }
            for (int i = 0; i < w.ChildCount; i++) Collect(w.GetChild(i));
        }

        public string Describe() =>
            _vm == null
                ? (_why == AltMarkerFallback.NoLayer ? "no " + LayerName + " layer on the screen" : "the " + LayerName + " layer holds no " + MovieName + " movie over the game's marker ViewModel")
                : "layer " + LayerName + ", movie " + MovieName + ": " + _lastTargets + (_lastTargets == 1 ? " marker, " : " markers, ") + _lastWidgets
                  + (_lastWidgets == 1 ? " marker widget" : " marker widgets");
    }

    /// <summary>
    /// The FALLBACK (no marker layer or data - another mod replaced the markers): the same world point vanilla
    /// uses, projected ourselves - every team's formations with men, the median's ground + 3 m through
    /// <c>MBWindowManager.WorldToScreen</c> from the combat camera, WSign, the camera distance, vanilla's nominal
    /// marker size and its distance fade. It cannot tell when vanilla would show markers - the view copies the
    /// rule (ALT held or the orders menu open).
    /// </summary>
    internal sealed class ProjectedFormationMarkers : IFormationMarkerSource
    {
        private static readonly Vec3 HeightOffset = new Vec3(0f, 0f, 3f);

        private readonly MissionScreen _screen;
        private readonly Mission _mission;
        private int _last;

        public ProjectedFormationMarkers(MissionScreen screen, Mission mission)
        {
            _screen = screen;
            _mission = mission;
        }

        public bool DistanceShown => ManagedOptions.GetConfig(ManagedOptions.ManagedOptionsType.ShowFormationDistances) > 1E-05f;

        public bool TryReadShown(out bool shown)
        {
            shown = false;
            return false;
        }

        public MarkerRead Read(AltMarkerFrame frame, out AltMarkerFallback why)
        {
            frame.Clear();
            why = AltMarkerFallback.None;
            ReadScreen(frame);
            var camera = _screen.CombatCamera;
            var teams = _mission.Teams;
            if (camera == null || teams == null) return MarkerRead.Projected;
            bool distance = DistanceShown;
            var player = _mission.PlayerTeam;
            for (int t = 0; t < teams.Count; t++)
            {
                var team = teams[t];
                var formations = team?.FormationsIncludingEmpty;
                if (formations == null) continue;
                int teamType = ReferenceEquals(team, player) || team!.IsPlayerTeam ? (int)MarkerTeam.Yours : team.IsPlayerAlly ? (int)MarkerTeam.Ally : (int)MarkerTeam.Enemy;
                for (int k = 0; k < formations.Count; k++)
                {
                    var f = formations[k];
                    if (f == null || f.CountOfUnits <= 0) continue;
                    if (frame.Count >= AltMarkerFrame.MaxMarkers)
                    {
                        frame.Dropped++;
                        continue;
                    }
                    ref var m = ref frame.Markers[frame.Count++];
                    m = default;
                    m.Key = AltMarkerMath.Key(team!.TeamIndex, (int)f.FormationIndex);
                    m.TeamType = teamType;
                    m.FormationIndex = (int)f.FormationIndex;
                    m.Men = f.CountOfUnits;
                    Project(camera, f, ref m);
                    if (AthleticsLogic.TryGetFormationStats(f, out var stats)) m.Stats = stats;
                    AltMarkerMath.Nominal(ref m, frame.Scale, distance);
                }
            }
            _last = frame.Count;
            return MarkerRead.Projected;
        }

        /// <summary>Vanilla's UpdateMarkerPositions, point for point.</summary>
        private static void Project(TaleWorlds.Engine.Camera camera, Formation f, ref AltMarker m)
        {
            var median = f.CachedMedianPosition;
            if (!median.IsValid)
            {
                m.PointX = m.PointY = -10000f;
                m.WSign = -1;
                m.Distance = 10000f;
                return;
            }
            var ground = median.GetGroundVec3();
            float x = 0f, y = 0f, w = 0f;
            MBWindowManager.WorldToScreen(camera, ground + HeightOffset, ref x, ref y, ref w);
            if (!MathF.IsValidValue(w) || !MathF.IsValidValue(x) || !MathF.IsValidValue(y))
            {
                x = y = -10000f;
                w = -1f;
            }
            m.PointX = x;
            m.PointY = y;
            m.WSign = w < 0f ? -1 : 1;
            m.Distance = camera.Position.Distance(ground);
        }

        /// <summary>The screen in pixels and the UI scale, from any Gauntlet layer (they share them).</summary>
        private void ReadScreen(AltMarkerFrame frame)
        {
            foreach (var l in _screen.Layers)
            {
                if (!(l is GauntletLayer g) || g.UIContext == null) continue;
                var page = g.UIContext.EventManager.PageSize;
                frame.ScreenWidth = page.X;
                frame.ScreenHeight = page.Y;
                frame.Scale = g.UIContext.CustomScale > 0f ? g.UIContext.CustomScale : 1f;
                return;
            }
        }

        public string Describe() => "own projection of every team's formations: " + _last + (_last == 1 ? " marker" : " markers");
    }
}

using System;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>Vanilla's marker colours: <c>MissionFormationMarkerTargetVM.TeamType</c> (engine values).</summary>
    public enum MarkerTeam
    {
        /// <summary>The player's team ("Player" state).</summary>
        Yours = 0,

        /// <summary>An allied team ("Ally").</summary>
        Ally = 1,

        /// <summary>The enemy ("Enemy").</summary>
        Enemy = 2,
    }

    /// <summary>
    /// One of vanilla's formation markers as step 20 read it in one frame (AI_NOTES "Step 20"): the point
    /// vanilla projected (<c>MissionFormationMarkerTargetVM.ScreenPosition</c>, screen PIXELS), whether it is
    /// in front of the camera, the men, and - once <see cref="AltMarkerMath.Pair"/> found its widget - the
    /// marker widget's live size, alpha and own offsets. The formation's Athletics snapshot rides along.
    /// </summary>
    public struct AltMarker
    {
        /// <summary>The label's identity: team index × <see cref="AltMarkerMath.KeyStride"/> + formation index.</summary>
        public int Key;

        /// <summary><see cref="MarkerTeam"/> as vanilla's int.</summary>
        public int TeamType;

        /// <summary>FormationClass 0..9.</summary>
        public int FormationIndex;

        /// <summary>Vanilla's point: the formation's median ground + 3 m, projected (pixels).</summary>
        public float PointX;

        public float PointY;

        /// <summary>+1 in front of the camera, −1 behind it (or no valid median).</summary>
        public int WSign;

        /// <summary>Camera → the formation, metres.</summary>
        public float Distance;

        /// <summary>Vanilla's count (the formation's units, the player included).</summary>
        public int Men;

        /// <summary>The marker widget was found (or, in a fallback, a nominal one was set).</summary>
        public bool HasWidget;

        /// <summary>The size is the widget's own (false: a nominal size - a fallback).</summary>
        public bool LiveSize;

        /// <summary>The marker widget's size, pixels (0 = not laid out yet).</summary>
        public float Width;

        public float Height;

        /// <summary>The marker widget's alpha (distance fade; 0 under 5 m; fading out after a release).</summary>
        public float Alpha;

        /// <summary>Vanilla pins a targeted enemy's marker to the screen's edge (orders menu open).</summary>
        public bool Targeting;

        /// <summary>The marker widget's own top-left (ScaledPositionX/YOffset, pixels) - where a pinned one sits.</summary>
        public float OffsetX;

        public float OffsetY;

        /// <summary>The formation's Athletics (Count 0 = nothing known).</summary>
        public FormationAthleticsStats Stats;

        public bool HasStats => Stats.Count > 0;
    }

    /// <summary>One marker widget as read from vanilla's layer (paired to its target by <see cref="AltMarkerMath.Pair"/>).</summary>
    public struct MarkerWidget
    {
        /// <summary>The widget's bound <c>Position</c> (= its target's ScreenPosition, copied by the binding).</summary>
        public float PositionX;

        public float PositionY;

        public float Width;
        public float Height;
        public float Alpha;
        public bool Targeting;
        public float OffsetX;
        public float OffsetY;

        /// <summary>Taken by a target this frame.</summary>
        public bool Used;
    }

    /// <summary>
    /// Everything read from the game in one frame (reused - nothing allocated per frame): the targets, the marker
    /// widgets, the screen in pixels and the UI scale.
    /// </summary>
    public sealed class AltMarkerFrame
    {
        /// <summary>Most markers handled (4 teams × 10 formations fit; more are dropped and counted) - plumbing.</summary>
        public const int MaxMarkers = 64;

        public readonly AltMarker[] Markers = new AltMarker[MaxMarkers];

        public int Count;

        public readonly MarkerWidget[] Widgets = new MarkerWidget[MaxMarkers];

        public int WidgetCount;

        public float ScreenWidth;

        public float ScreenHeight;

        /// <summary>Screen pixels per UI pixel of the 1080p layout (the game's UI scale included).</summary>
        public float Scale = 1f;

        /// <summary>Markers beyond <see cref="MaxMarkers"/> this frame.</summary>
        public int Dropped;

        public void Clear()
        {
            Count = 0;
            WidgetCount = 0;
            Dropped = 0;
        }
    }

    /// <summary>Why a marker gets no label this frame - or <see cref="Shown"/> / <see cref="Pinned"/> when it does.</summary>
    public enum MarkerSight
    {
        /// <summary>In front of the camera: the label under the marker.</summary>
        Shown = 0,

        /// <summary>Vanilla pinned the marker to the screen's edge (a targeted enemy, orders menu open): the label follows it.</summary>
        Pinned = 1,

        /// <summary>An enemy formation while AltMarkersShowEnemy is off.</summary>
        EnemyOff = 2,

        /// <summary>No tracked man in it (Athletics does not know it yet).</summary>
        NoStats = 3,

        /// <summary>Behind the camera (vanilla hides the marker).</summary>
        Behind = 4,

        /// <summary>The marker is faded out (closer than 5 m, or fading after the key's release).</summary>
        Faded = 5,

        /// <summary>A brand-new marker widget, not laid out yet (one frame).</summary>
        NotLaidOut = 6,
    }

    /// <summary>How the labels found their markers.</summary>
    public enum AltMarkerTechnique
    {
        /// <summary>Not decided yet.</summary>
        None = 0,

        /// <summary>The game's own markers read live: its points and its widgets' sizes.</summary>
        Live = 1,

        /// <summary>The game's points, a nominal marker size (its widgets were not found or not paired).</summary>
        GamePoints = 2,

        /// <summary>Our own projection of the same world point, vanilla's condition copied.</summary>
        OwnProjection = 3,
    }

    /// <summary>Why the live read fell short (each logged with its words, counted for the summary).</summary>
    public enum AltMarkerFallback
    {
        None = 0,

        /// <summary>No "MissionFormationMarker" layer on the screen - own projection.</summary>
        NoLayer = 1,

        /// <summary>The layer has no FormationMarker movie over the game's marker ViewModel - own projection.</summary>
        NoMarkerData = 2,

        /// <summary>The markers' widgets were not found (another prefab?) - the game's points, a nominal size.</summary>
        NoWidgets = 3,

        /// <summary>Some markers had no widget at their point - those get a nominal size.</summary>
        Unpaired = 4,
    }

    /// <summary>
    /// Step 20's label layout in UI pixels of the 1080p layout (the Advanced AltMarker* settings, read live): the
    /// numbers' size, the gap under the marker, and the optional bar.
    /// </summary>
    public readonly struct AltMarkerLayout
    {
        public AltMarkerLayout(int textSize, int offset, int barWidth, int barHeight)
        {
            TextSize = textSize;
            Offset = offset;
            BarWidth = barWidth;
            BarHeight = barHeight;
        }

        public static AltMarkerLayout From(TraxSettings s) => new AltMarkerLayout(s.AltMarkerTextSize, s.AltMarkerOffset, s.AltMarkerBarWidth, s.AltMarkerBarHeight);

        public int TextSize { get; }

        /// <summary>The marker's bottom edge → the top of the label (may be negative).</summary>
        public int Offset { get; }

        public int BarWidth { get; }

        /// <summary>0 = no bar.</summary>
        public int BarHeight { get; }

        public bool Bar => BarHeight > 0;

        public string Describe() =>
            "numbers " + TextSize + " px, " + Offset.ToString(CultureInfo.InvariantCulture) + " px under the marker, "
            + (Bar ? "bar " + BarWidth + " x " + BarHeight + " px" : "no bar (AltMarkerBarHeight 0)") + " (UI pixels - the game's UI scale applies)";
    }

    /// <summary>
    /// DESIGN §3 item 3 (step 20) - the ALT labels' pure logic: pairing vanilla's marker widgets with its targets
    /// (<see cref="Pair"/>), whether a marker gets a label (<see cref="Sight"/>), where the label goes
    /// (<see cref="Place"/>), vanilla's distance fade (<see cref="VanillaAlpha"/>, for the projection fallback) and
    /// the names for the log. The numbers and the ± band are the orders strip's (<see cref="OrderStripMath"/>).
    /// </summary>
    public static class AltMarkerMath
    {
        /// <summary>A label key's stride per team (formations 0..9 fit) - plumbing.</summary>
        public const int KeyStride = 16;

        /// <summary>The label's centring box, UI pixels: wider than any label, nothing drawn in it but the
        /// centred numbers and bar - plumbing.</summary>
        public const float BoxWidth = 200f;

        /// <summary>Vanilla's marker widget with the distance row (count ~22 + icon 50 + distance 36), UI px - the
        /// nominal size of a fallback (engine fact from FormationMarker.xml).</summary>
        public const float NominalMarkerHeight = 108f;

        /// <summary>Without the distance row ("Show formation distances" off): count + icon.</summary>
        public const float NominalMarkerHeightNoDistance = 72f;

        /// <summary>The distance row's width (the widest part), UI px.</summary>
        public const float NominalMarkerWidth = 60f;

        /// <summary>Vanilla's marker is hidden at or below this alpha (FormationMarkerListPanel: IsVisible false).</summary>
        public const float HiddenAlpha = 0.05f;

        // FormationMarker.xml's own values on its FormationMarkerListPanel (engine facts).
        private const float FarAlphaTarget = 0.7f;
        private const float FarDistanceCutoff = 500f;
        private const float CloseDistanceCutoff = 10f;
        private const float ClosestFadeoutRange = 5f;

        public static int Key(int teamIndex, int formationIndex) => teamIndex * KeyStride + formationIndex;

        /// <summary>
        /// Pairs each target with its marker widget: the widget at the same index first (vanilla keeps the item
        /// widgets in the list's order), else the unused widget at EXACTLY the same point (the binding copies it).
        /// Copies the widget's size, alpha, targeting and offsets into the marker; a marker without one keeps
        /// <see cref="AltMarker.HasWidget"/> false. Returns how many were paired. Allocation-free.
        /// </summary>
        public static int Pair(AltMarkerFrame frame)
        {
            for (int j = 0; j < frame.WidgetCount; j++) frame.Widgets[j].Used = false;
            int paired = 0;
            for (int i = 0; i < frame.Count; i++)
            {
                ref var m = ref frame.Markers[i];
                m.HasWidget = false;
                m.LiveSize = false;
                int found = -1;
                if (i < frame.WidgetCount && !frame.Widgets[i].Used && SamePoint(in frame.Widgets[i], in m)) found = i;
                for (int j = 0; found < 0 && j < frame.WidgetCount; j++)
                    if (!frame.Widgets[j].Used && SamePoint(in frame.Widgets[j], in m)) found = j;
                if (found < 0) continue;
                ref var w = ref frame.Widgets[found];
                w.Used = true;
                m.HasWidget = true;
                m.LiveSize = true;
                m.Width = w.Width;
                m.Height = w.Height;
                m.Alpha = w.Alpha;
                m.Targeting = w.Targeting;
                m.OffsetX = w.OffsetX;
                m.OffsetY = w.OffsetY;
                paired++;
            }
            return paired;
        }

        private static bool SamePoint(in MarkerWidget w, in AltMarker m) => w.PositionX == m.PointX && w.PositionY == m.PointY;

        /// <summary>A marker without a widget gets vanilla's nominal size (UI px × <paramref name="scale"/>), full
        /// alpha by distance - a fallback.</summary>
        public static void Nominal(ref AltMarker m, float scale, bool distanceShown)
        {
            if (!(scale > 0f)) scale = 1f;
            m.HasWidget = true;
            m.LiveSize = false;
            m.Width = NominalMarkerWidth * scale;
            m.Height = (distanceShown ? NominalMarkerHeight : NominalMarkerHeightNoDistance) * scale;
            m.Alpha = VanillaAlpha(m.Distance);
            m.Targeting = false;
        }

        /// <summary>
        /// Whether this marker gets a label, and why not. Order: the enemy switch, no stats, faded (too close / after a
        /// release), not laid out, pinned (a targeted marker not fully on screen), behind the camera, shown.
        /// </summary>
        public static MarkerSight Sight(in AltMarker m, bool showEnemy, float screenWidth, float screenHeight)
        {
            if (m.TeamType == (int)MarkerTeam.Enemy && !showEnemy) return MarkerSight.EnemyOff;
            if (!m.HasStats) return MarkerSight.NoStats;
            if (m.HasWidget && m.Alpha <= HiddenAlpha) return MarkerSight.Faded;
            if (m.HasWidget && m.LiveSize && !(m.Width > 1f && m.Height > 1f)) return MarkerSight.NotLaidOut;
            if (m.Targeting && !FullyOnScreen(in m, screenWidth, screenHeight)) return MarkerSight.Pinned;
            if (m.WSign <= 0) return MarkerSight.Behind;
            return MarkerSight.Shown;
        }

        /// <summary>Vanilla's own test (FormationMarkerListPanel.UpdateScreenPosition): in front of the camera and the
        /// whole marker inside the screen.</summary>
        public static bool FullyOnScreen(in AltMarker m, float screenWidth, float screenHeight)
        {
            float left = m.PointX - m.Width / 2f, right = m.PointX + m.Width / 2f;
            float top = m.PointY - m.Height / 2f, bottom = m.PointY + m.Height / 2f;
            return m.WSign > 0 && left > 0f && right < screenWidth && top > 0f && bottom < screenHeight;
        }

        /// <summary>
        /// The label's centring box in screen PIXELS: centred on the marker, its top <see cref="AltMarkerLayout.Offset"/>
        /// UI px × the scale under the marker's bottom edge. Shown: the marker is centred on vanilla's point (exactly as
        /// vanilla centres it); pinned: at the widget's own offsets. <paramref name="boxWidth"/> = <see cref="BoxWidth"/>
        /// × the scale.
        /// </summary>
        public static void Place(in AltMarker m, MarkerSight sight, in AltMarkerLayout layout, float scale, out float x, out float y, out float boxWidth)
        {
            if (!(scale > 0f)) scale = 1f;
            float centre, bottom;
            if (sight == MarkerSight.Pinned)
            {
                centre = m.OffsetX + m.Width / 2f;
                bottom = m.OffsetY + m.Height;
            }
            else
            {
                centre = m.PointX;
                bottom = m.PointY + m.Height / 2f;
            }
            boxWidth = BoxWidth * scale;
            x = centre - boxWidth / 2f;
            y = bottom + layout.Offset * scale;
        }

        /// <summary>Vanilla's marker alpha at a camera distance (FormationMarkerListPanel with FormationMarker.xml's
        /// values): 0.7 beyond 500 m, rising to 1 at 10 m, 0 under 5 m. The projection fallback's "too close".</summary>
        public static float VanillaAlpha(float distance)
        {
            if (distance > FarDistanceCutoff) return FarAlphaTarget;
            if (distance >= CloseDistanceCutoff)
            {
                float amount = (float)Math.Pow((distance - CloseDistanceCutoff) / (FarDistanceCutoff - CloseDistanceCutoff), 1.0 / 3.0);
                float a = 1f + (FarAlphaTarget - 1f) * amount;
                return a < FarAlphaTarget ? FarAlphaTarget : a > 1f ? 1f : a;
            }
            if (distance > CloseDistanceCutoff - ClosestFadeoutRange)
                return (distance - (CloseDistanceCutoff - ClosestFadeoutRange)) / ClosestFadeoutRange;
            return 0f;
        }

        public static string TeamName(int teamType) => teamType switch
        {
            (int)MarkerTeam.Yours => "yours",
            (int)MarkerTeam.Ally => "ally",
            (int)MarkerTeam.Enemy => "enemy",
            _ => "team type " + teamType.ToString(CultureInfo.InvariantCulture),
        };

        public static string SightName(MarkerSight s) => s switch
        {
            MarkerSight.Shown => "shown",
            MarkerSight.Pinned => "pinned at the screen's edge",
            MarkerSight.EnemyOff => "enemy (AltMarkersShowEnemy off)",
            MarkerSight.NoStats => "no tracked men",
            MarkerSight.Behind => "behind the camera",
            MarkerSight.Faded => "faded (closer than 5 m or fading)",
            _ => "not laid out yet",
        };

        public static string TechniqueName(AltMarkerTechnique t) => t switch
        {
            AltMarkerTechnique.Live => "the game's own formation markers read live (their points and their widgets' sizes)",
            AltMarkerTechnique.GamePoints => "the game's marker points with a nominal marker size",
            AltMarkerTechnique.OwnProjection => "our own projection of the same point (the formation's median + 3 m), the game's rule copied (ALT held or the orders menu open)",
            _ => "none yet",
        };

        public static string FallbackName(AltMarkerFallback f) => f switch
        {
            AltMarkerFallback.NoLayer => "no marker layer",
            AltMarkerFallback.NoMarkerData => "no marker data",
            AltMarkerFallback.NoWidgets => "no marker widgets",
            AltMarkerFallback.Unpaired => "a marker without its widget",
            _ => "none",
        };

        /// <summary>"yours 1 Infantry (40 men) at (812, 430) 60 x 108" - log lines only.</summary>
        public static string DescribeMarker(in AltMarker m) =>
            TeamName(m.TeamType) + " " + OrderStripMath.FormationName(m.FormationIndex) + " (" + m.Men + (m.Men == 1 ? " man" : " men") + ") at ("
            + OrderStripMath.Px(m.PointX) + ", " + OrderStripMath.Px(m.PointY) + ")"
            + (m.HasWidget ? " " + OrderStripMath.Px(m.Width) + " x " + OrderStripMath.Px(m.Height) + (m.LiveSize ? string.Empty : " (nominal)") : " (no widget)")
            + ", " + OrderStripMath.Px(m.Distance) + " m";

        /// <summary>"yours 1 Infantry 72% ± 8 HP 81% (40 men, f 0.90)" - log lines only.</summary>
        public static string DescribeValues(in AltMarker m, int meanPercent, int spreadPercent, int healthPercent, int ready = -1, int total = -1) =>
            TeamName(m.TeamType) + " " + OrderStripMath.DescribeValues(m.FormationIndex, m.Stats, meanPercent, spreadPercent, healthPercent, ready, total);
    }

    /// <summary>
    /// One mission of the ALT labels for the [summary]: how often vanilla's markers came up with the labels on and
    /// for how long, the technique(s), the formations labelled per side and the most at once, pinned labels, every
    /// fallback with its reason and the first one's words. Main thread.
    /// </summary>
    public sealed class AltMarkerStats
    {
        private const int Keys = 8 * AltMarkerMath.KeyStride;
        private const int FallbackKinds = 5;

        private readonly bool[] _labelled = new bool[Keys];
        private readonly int[] _byTechnique = new int[4];
        private readonly int[] _fallbacks = new int[FallbackKinds];

        public int Shows { get; private set; }
        public double SecondsOnScreen { get; private set; }
        public int MostAtOnce { get; private set; }
        public int PinnedFrames { get; private set; }
        public int ValuesPushed { get; private set; }
        public int DroppedMarkers { get; private set; }
        public int Yours { get; private set; }
        public int Allies { get; private set; }
        public int Enemy { get; private set; }
        public string? FirstFallbackText { get; private set; }
        public double FirstFallbackAt { get; private set; }

        /// <summary>The technique of the first show ("the game's own formation markers read live (…)").</summary>
        public string? Technique { get; set; }

        public void NoteShown() => Shows++;

        public void AddOnScreen(double dt)
        {
            if (dt > 0) SecondsOnScreen += dt;
        }

        /// <summary>A show used this technique (once per show).</summary>
        public void NoteTechnique(AltMarkerTechnique t)
        {
            int i = (int)t;
            if (i > 0 && i < _byTechnique.Length) _byTechnique[i]++;
        }

        public int ShowsBy(AltMarkerTechnique t)
        {
            int i = (int)t;
            return i > 0 && i < _byTechnique.Length ? _byTechnique[i] : 0;
        }

        /// <summary>One frame's labels: how many shown, how many pinned.</summary>
        public void NoteFrame(int shown, int pinned, int dropped)
        {
            if (shown > MostAtOnce) MostAtOnce = shown;
            if (pinned > 0) PinnedFrames++;
            if (dropped > DroppedMarkers) DroppedMarkers = dropped;
        }

        /// <summary>A formation got a label (counted once per mission per formation). True when it is new.</summary>
        public bool NoteLabelled(int key, int teamType)
        {
            if (key < 0 || key >= _labelled.Length || _labelled[key]) return false;
            _labelled[key] = true;
            if (teamType == (int)MarkerTeam.Enemy) Enemy++;
            else if (teamType == (int)MarkerTeam.Ally) Allies++;
            else Yours++;
            return true;
        }

        public void NoteValuesPushed() => ValuesPushed++;

        public void NoteFallback(AltMarkerFallback why, string text, double at)
        {
            int i = (int)why;
            if (i > 0 && i < _fallbacks.Length) _fallbacks[i]++;
            if (FirstFallbackText == null)
            {
                FirstFallbackText = text;
                FirstFallbackAt = at;
            }
        }

        public int Fallbacks(AltMarkerFallback why)
        {
            int i = (int)why;
            return i > 0 && i < _fallbacks.Length ? _fallbacks[i] : 0;
        }

        /// <summary>The [summary] line (without the tag); <paramref name="errors"/> = the view's own error count.</summary>
        public string SummaryLine(int errors)
        {
            var sb = new StringBuilder("hud: ALT markers - shown ").Append(Shows).Append('x');
            if (Shows == 0)
                return sb.Append(" (vanilla's formation markers never came up with the labels on - ALT never held or the orders menu never opened in a fight)")
                         .Append("; errors ").Append(errors).ToString();
            sb.Append(", on screen ").Append(SecondsOnScreen.ToString("0.0", CultureInfo.InvariantCulture)).Append(" s");
            sb.Append("; technique: ").Append(Technique ?? "never placed");
            if (ShowsBy(AltMarkerTechnique.Live) != Shows)
                sb.Append(" (shows: live ").Append(ShowsBy(AltMarkerTechnique.Live)).Append(", the game's points + a nominal size ")
                  .Append(ShowsBy(AltMarkerTechnique.GamePoints)).Append(", own projection ").Append(ShowsBy(AltMarkerTechnique.OwnProjection)).Append(')');
            sb.Append("; formations labelled: yours ").Append(Yours).Append(", allies ").Append(Allies).Append(", enemy ").Append(Enemy)
              .Append(" (most at once ").Append(MostAtOnce).Append("), frames with a label pinned at the screen's edge ").Append(PinnedFrames)
              .Append(", values pushed ").Append(ValuesPushed);
            if (DroppedMarkers > 0) sb.Append(", markers beyond ").Append(AltMarkerFrame.MaxMarkers).Append(" dropped (most) ").Append(DroppedMarkers);
            int total = 0;
            for (int i = 1; i < _fallbacks.Length; i++) total += _fallbacks[i];
            if (total == 0) sb.Append("; fallbacks: none");
            else
            {
                sb.Append("; fallbacks ").Append(total).Append(" (");
                bool first = true;
                for (int i = 1; i < _fallbacks.Length; i++)
                {
                    if (_fallbacks[i] == 0) continue;
                    sb.Append(first ? string.Empty : ", ").Append(AltMarkerMath.FallbackName((AltMarkerFallback)i)).Append(' ').Append(_fallbacks[i]);
                    first = false;
                }
                sb.Append(") - the first at ").Append(FirstFallbackAt.ToString("0.0", CultureInfo.InvariantCulture)).Append(" s: ").Append(FirstFallbackText);
            }
            return sb.Append("; errors ").Append(errors).ToString();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Hud;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for the numbers under vanilla's formation markers (step 20, hold ALT): the prefab against the
    /// game (the item template against the label ViewModel), and the REAL <see cref="AltMarkerView"/> driven by
    /// made-up frames with stand-in marker sources (<see cref="FakeMarkers"/> - vanilla's layer as the view reads it:
    /// its "shown" flag, its targets' points, its widgets): shown exactly with vanilla's flag (and the copied rule
    /// when it cannot be read), the labels under the markers at any UI scale, keyed by formation through a re-sort,
    /// the colours and values, the enemy switch, the hides (behind, faded, not laid out), a pinned marker, every
    /// fallback (a marker without its widget, no widgets, no layer → our own projection) and the way back, the live
    /// settings, the gates, the log, the summary and the fail safe. What it cannot check: that the game's real
    /// marker layer reports these values and that the labels draw there - PLAYTEST "hold ALT" and the [hud] lines.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>Vanilla's marker layer as the view reads it, played by the smoke.</summary>
        private sealed class FakeMarkers : IFormationMarkerSource
        {
            public bool Readable = true;
            public bool Shown;
            public MarkerRead State = MarkerRead.Live;
            public AltMarkerFallback Why = AltMarkerFallback.NoLayer;
            public bool Throw;
            public bool Distance = true;
            public float Scale = 1f;
            public readonly List<AltMarker> Markers = new List<AltMarker>();
            public readonly List<MarkerWidget> Widgets = new List<MarkerWidget>();
            public int Reads;
            public string Name = "stand-in layer MissionFormationMarker, movie FormationMarker";

            public bool DistanceShown => Distance;

            public bool TryReadShown(out bool shown)
            {
                if (Throw) throw new InvalidOperationException("smoke: the marker read failed");
                shown = Shown;
                return Readable;
            }

            public MarkerRead Read(AltMarkerFrame frame, out AltMarkerFallback why)
            {
                Reads++;
                if (Throw) throw new InvalidOperationException("smoke: the marker read failed");
                frame.Clear();
                why = AltMarkerFallback.None;
                if (State == MarkerRead.Unavailable)
                {
                    why = Why;
                    return State;
                }
                frame.ScreenWidth = 1920 * Scale;
                frame.ScreenHeight = 1080 * Scale;
                frame.Scale = Scale;
                foreach (var m in Markers) frame.Markers[frame.Count++] = m;
                if (State == MarkerRead.Live)
                    foreach (var w in Widgets) frame.Widgets[frame.WidgetCount++] = w;
                if (State == MarkerRead.PointsOnly) why = AltMarkerFallback.NoWidgets;
                if (State == MarkerRead.Projected)
                    for (int i = 0; i < frame.Count; i++) AltMarkerMath.Nominal(ref frame.Markers[i], Scale, Distance);
                return State;
            }

            public string Describe() => State == MarkerRead.Unavailable
                ? "stand-in: no MissionFormationMarker layer on the screen"
                : Name + ": " + Markers.Count + " markers, " + (State == MarkerRead.Live ? Widgets.Count : 0) + " marker widgets";

            /// <summary>One marker (and its widget, 60 x 108 UI px, at its point) at <paramref name="scale"/>.</summary>
            public void Add(int teamIndex, int teamType, int k, float x, float y, int men, double mean, double std, double f, double health,
                float alpha = 1f, int wSign = 1, bool widget = true, float scale = 1f)
            {
                var m = new AltMarker
                {
                    Key = AltMarkerMath.Key(teamIndex, k),
                    TeamType = teamType,
                    FormationIndex = k,
                    PointX = x * scale,
                    PointY = y * scale,
                    WSign = wSign,
                    Distance = 80,
                    Men = men,
                    Stats = new FormationAthleticsStats(men, mean * 100, std * 100, mean, std, 0, f, 0, health),
                };
                Markers.Add(m);
                if (!widget) return;
                Widgets.Add(new MarkerWidget
                {
                    PositionX = m.PointX,
                    PositionY = m.PointY,
                    Width = 60 * scale,
                    Height = 108 * scale,
                    Alpha = alpha,
                    OffsetX = m.PointX - 30 * scale,
                    OffsetY = m.PointY - 54 * scale,
                });
            }

            /// <summary>Your Infantry (72% ± 8, f 0.90, 81%) and Archers (45% ± 20, f 0.60, 60%); the enemy's Infantry
            /// (30% ± 10, f 0.40, 90%) and Cavalry behind the camera.</summary>
            public void Army(float scale = 1f)
            {
                Markers.Clear();
                Widgets.Clear();
                Scale = scale;
                Add(0, 0, 0, 960, 400, 40, 0.72, 0.08, 0.90, 0.81, scale: scale);
                Add(0, 0, 1, 600, 500, 20, 0.45, 0.20, 0.60, 0.60, scale: scale);
                Add(1, 2, 0, 1300, 350, 80, 0.30, 0.10, 0.40, 0.90, scale: scale);
                Add(1, 2, 2, -10000, -10000, 30, 0.90, 0.05, 1.00, 1.00, alpha: 0.7f, wSign: -1, scale: scale);
            }
        }

        private static void AltMarkerDefaults()
        {
            S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.VerboseLogging, false, SettingSources.File);
            S.Set(SettingsSchema.AthleticsEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.AthleticsPeakPercent, 75, SettingSources.File);
            S.Set(SettingsSchema.ShowAltMarkerStats, true, SettingSources.File);
            S.Set(SettingsSchema.AltMarkersShowEnemy, true, SettingSources.File);
            S.Set(SettingsSchema.ShowFormationHealth, true, SettingSources.File);
            S.Set(SettingsSchema.ShowFormationSpread, true, SettingSources.File);
            S.Set(SettingsSchema.FormationSpreadStdDevs, 1.0, SettingSources.File);
            S.Set(SettingsSchema.AltMarkerTextSize, 16, SettingSources.File);
            S.Set(SettingsSchema.AltMarkerOffset, 2, SettingSources.File);
            S.Set(SettingsSchema.AltMarkerBarWidth, 60, SettingSources.File);
            S.Set(SettingsSchema.AltMarkerBarHeight, 3, SettingSources.File);
            S.Set(SettingsSchema.HudRefreshSeconds, 0.1, SettingSources.File);
            S.Set(SettingsSchema.BarYellowBelowPercent, 75, SettingSources.File);
            S.Set(SettingsSchema.BarOrangeBelowPercent, 50, SettingSources.File);
            S.Set(SettingsSchema.BarRedBelowPercent, 25, SettingSources.File);
        }

        private static class AltColors
        {
            public static Color Of(string hex) => Color.ConvertStringToColor(hex);
        }

        // ------------------------------------------------------------------ the prefab

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AltMarkersPrefabIsValid() => PrefabIsValid(AltMarkerView.Movie, typeof(AltMarkersVM), 12, 90);

        // ------------------------------------------------------------------ the view, driven

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AltMarkersThroughTheView()
        {
            AltMarkerDefaults();
            var logic = StripLogic();
            var player = FakeAgent(42);
            var live = new FakeMarkers();
            live.Army();
            var projection = new FakeMarkers { State = MarkerRead.Projected, Name = "stand-in projection" };
            projection.Army();
            var view = new AltMarkerView();
            var layer = new FakeHudLayer();
            view.UseLayer(layer);
            view.UseSources(live, projection);
            int logFrom = LogText.Length;
            double t = 1;

            // the markers hidden (ALT not held): nothing, and the log says why - once
            view.Tick(Frame(t, player));
            Check(!view.IsLayerUp && layer.Created == 0 && view.LastDecision == HudHide.ViewCondition, "the labels are up with vanilla's markers hidden");
            LogHas("[hud] ALT markers: not shown at 1.0 s - the game's formation markers are hidden (ALT not held, the orders menu closed)");

            // 1. vanilla shows its markers (its own flag - the frame's ALT is not even read): labels under them
            live.Shown = true;
            t = 2;
            view.Tick(Frame(t, player));
            var vm = view.CurrentViewModel!;
            Check(layer.Up && view.IsLayerUp && view.Technique == AltMarkerTechnique.Live, "shown: no layer / not live");
            LogHas("[hud] ALT markers: layer created at 2.0 s (mode Battle, was hidden: the game's formation markers are hidden (ALT not held, the orders menu closed))"
                   + " - movie TraxAltMarkers loaded OK (stand-in, 11 widgets) - later builds and removals by \"the game's formation markers are hidden (ALT not held, the orders menu closed)\" go to the verbose log only");
            var l = vm.Labels;
            Check(l.Count == 3 && l[0].IsShown && l[1].IsShown && l[2].IsShown, "shown: 3 labels expected (the enemy's Cavalry is behind the camera), got " + l.Count);
            Check(l[0].PositionX == 860f && l[0].PositionY == 456f && l[0].BoxWidth == 200f, "yours Infantry: the label is not under its marker: " + l[0].PositionX + ", " + l[0].PositionY + " w " + l[0].BoxWidth);
            Check(l[1].PositionX == 500f && l[1].PositionY == 556f && l[2].PositionX == 1200f && l[2].PositionY == 406f, "Archers / the enemy's Infantry misplaced");
            Check(l[0].AthleticsText == "72% ± 8" && l[0].HealthText == "HP 81%" && l[0].HealthShown && l[0].AthleticsColor == AltColors.Of(BarMath.BlueHex)
                  && l[0].FillColor == AltColors.Of(BarMath.BlueHex) && Near(l[0].Fill, 0.72f) && Near(l[0].BandLow, 0.64f) && Near(l[0].BandHigh, 0.80f) && Near(l[0].PeakLine, 0.75f),
                "yours Infantry's values: " + l[0].AthleticsText + " / " + l[0].HealthText + ", fill " + l[0].Fill);
            Check(l[1].AthleticsText == "45% ± 20" && l[1].AthleticsColor == AltColors.Of(BarMath.YellowHex) && l[2].AthleticsText == "30% ± 10"
                  && l[2].HealthText == "HP 90%" && l[2].AthleticsColor == AltColors.Of(BarMath.OrangeHex), "the colours by f (Archers yellow, the enemy's Infantry orange)");
            Check(l[0].TextSize == 16 && l[0].BarShown && l[0].BarWidth == 60f && l[0].BarHeight == 3f, "the layout settings did not reach the labels");
            LogHas("[hud] ALT markers: first shown at 2.0 s (show #1) - technique: the game's own formation markers read live (their points and their widgets' sizes) "
                   + "(stand-in layer MissionFormationMarker, movie FormationMarker: 4 markers, 4 marker widgets); screen 1920 x 1080 px, UI scale 1.00; 4 markers (yours 2, allies 0, enemy 2): "
                   + "yours 1 Infantry (40 men) at (960, 400) 60 x 108, 80 m | yours 2 Archers (20 men) at (600, 500) 60 x 108, 80 m | enemy 1 Infantry (80 men) at (1300, 350) 60 x 108, 80 m"
                   + " | enemy 3 Cavalry (30 men) at (-10000, -10000) 60 x 108, 80 m; labels (numbers 16 px, 2 px under the marker, bar 60 x 3 px (UI pixels - the game's UI scale applies), centre x / top y): "
                   + "yours 1 Infantry at (960, 456) | yours 2 Archers at (600, 556) | enemy 1 Infantry at (1300, 406); no label: enemy 3 Cavalry - behind the camera");
            LogHas("[hud] ALT markers: values at 2.0 s (show #1): yours 1 Infantry 72% ± 8 HP 81% (40 men, f 0.90) | yours 2 Archers 45% ± 20 HP 60% (20 men, f 0.60)"
                   + " | enemy 1 Infantry 30% ± 10 HP 90% (80 men, f 0.40) - ± is 1.00 std, health on, enemy on");

            // 2. the player down: still shown (vanilla's markers show without him)
            view.Tick(Frame(t += 0.05, null));
            Check(layer.Up && view.LastDecision == HudHide.None, "the labels went with the player (vanilla keeps its markers)");

            // 3. another UI scale (2560 x 1440): bigger widgets, the gap and the box scale
            live.Army(4 / 3f);
            view.Tick(Frame(t += 0.05, player));
            static bool NearPx(float a, float b) => Math.Abs(a - b) < 0.01f;
            Check(NearPx(l[0].PositionX, 1280f - 400f / 3f) && NearPx(l[0].PositionY, 400f * 4 / 3f + 72f + 8f / 3f) && NearPx(l[0].BoxWidth, 800f / 3f),
                "UI scale 4/3: the label did not follow: " + l[0].PositionX + ", " + l[0].PositionY + " w " + l[0].BoxWidth);
            live.Army();

            // 4. vanilla re-sorts its markers far-first: the widgets pair by point, each label keeps its formation
            live.Markers.Reverse();
            var w0 = live.Widgets[0];
            live.Widgets.RemoveAt(0);
            live.Widgets.Add(w0);
            view.Tick(Frame(t += 0.05, player));
            Check(l.Count == 3 && l[0].PositionX == 860f && l[0].AthleticsText == "72% ± 8" && l[2].PositionX == 1200f, "a re-sort moved a label to another formation");
            live.Army();

            // 5. a new formation: its widget not laid out yet (size 0) = no label that frame, then its label
            live.Add(0, 0, 2, 1500, 600, 12, 0.10, 0.05, 0.13, 0.95);
            var fresh = live.Widgets[4];
            fresh.Width = fresh.Height = 0;
            live.Widgets[4] = fresh;
            view.Tick(Frame(t += 0.05, player));
            Check(l.Count == 3, "a marker that is not laid out got a label");
            fresh.Width = 60;
            fresh.Height = 108;
            live.Widgets[4] = fresh;
            view.Tick(Frame(t += 0.05, player));
            Check(l.Count == 4 && l[3].IsShown && l[3].PositionX == 1400f && l[3].PositionY == 656f && l[3].AthleticsColor == AltColors.Of(BarMath.RedHex),
                "the new formation's label: " + l.Count + (l.Count > 3 ? " at " + l[3].PositionX + ", " + l[3].PositionY : string.Empty));

            // 6. faded (closer than 5 m / fading): no label; back
            var archers = live.Widgets[1];
            archers.Alpha = 0.03f;
            live.Widgets[1] = archers;
            view.Tick(Frame(t += 0.05, player));
            Check(!l[1].IsShown && l[0].IsShown, "a faded marker kept its label");
            archers.Alpha = 1f;
            live.Widgets[1] = archers;
            view.Tick(Frame(t += 0.05, player));
            Check(l[1].IsShown, "the Archers' label did not come back");

            // 7. a targeted enemy pinned at the screen's edge (orders menu open): the label follows the pinned widget
            var enemy = live.Markers[2];
            enemy.PointX = 2100;
            live.Markers[2] = enemy;
            var pinned = live.Widgets[2];
            pinned.PositionX = 2100;
            pinned.Targeting = true;
            pinned.OffsetX = 1860;
            pinned.OffsetY = 296;
            live.Widgets[2] = pinned;
            view.Tick(Frame(t += 0.05, player, orderMenu: true));
            Check(l[2].IsShown && l[2].PositionX == 1790f && l[2].PositionY == 406f, "the pinned marker's label: " + l[2].PositionX + ", " + l[2].PositionY);
            live.Army();
            live.Add(0, 0, 2, 1500, 600, 12, 0.10, 0.05, 0.13, 0.95);

            // 8. AltMarkersShowEnemy off: the enemy's labels go at once; on: back
            S.Set(SettingsSchema.AltMarkersShowEnemy, false, SettingSources.Mcm);
            view.Tick(Frame(t += 0.05, player));
            Check(l[0].IsShown && l[1].IsShown && !l[2].IsShown && l[3].IsShown, "AltMarkersShowEnemy off: the enemy's label stayed");
            S.Set(SettingsSchema.AltMarkersShowEnemy, true, SettingSources.Mcm);
            view.Tick(Frame(t += 0.05, player));
            Check(l[2].IsShown, "AltMarkersShowEnemy on: the enemy's label did not come back");

            // 9. live settings: health / spread off, no bar, a bigger text - at once (a setting moved re-pushes)
            S.Set(SettingsSchema.ShowFormationHealth, false, SettingSources.Mcm);
            S.Set(SettingsSchema.ShowFormationSpread, false, SettingSources.Mcm);
            S.Set(SettingsSchema.AltMarkerBarHeight, 0, SettingSources.Mcm);
            S.Set(SettingsSchema.AltMarkerTextSize, 20, SettingSources.Mcm);
            view.Tick(Frame(t += 0.05, player));
            Check(!l[0].HealthShown && l[0].AthleticsText == "72%" && !l[0].BarShown && l[0].TextSize == 20 && l[0].BandLow == l[0].BandHigh,
                "the switches did not reach the labels: " + l[0].AthleticsText + ", bar " + l[0].BarShown + ", size " + l[0].TextSize);
            S.Set(SettingsSchema.ShowFormationHealth, true, SettingSources.Mcm);
            S.Set(SettingsSchema.ShowFormationSpread, true, SettingSources.Mcm);
            S.Set(SettingsSchema.AltMarkerBarHeight, 3, SettingSources.Mcm);
            S.Set(SettingsSchema.AltMarkerTextSize, 16, SettingSources.Mcm);
            S.Set(SettingsSchema.AltMarkerOffset, -20, SettingSources.Mcm);
            view.Tick(Frame(t += 0.05, player));
            Check(l[0].HealthShown && l[0].AthleticsText == "72% ± 8" && l[0].BarShown && l[0].PositionY == 434f, "back on / the gap: " + l[0].AthleticsText + ", y " + l[0].PositionY);
            S.Set(SettingsSchema.AltMarkerOffset, 2, SettingSources.Mcm);
            view.Tick(Frame(t += 0.05, player)); // a setting moved: the values are pushed once more

            // 10. new stats reach the labels when the logic's version moves (not before)
            var inf = live.Markers[0];
            inf.Stats = new FormationAthleticsStats(38, 50, 10, 0.50, 0.10, 0, 0.66, 0, 0.70);
            live.Markers[0] = inf;
            view.Tick(Frame(t += 0.05, player));
            Check(l[0].AthleticsText == "72% ± 8", "values re-pushed without a new stats version");
            BumpFormationStats(logic);
            view.Tick(Frame(t += 0.05, player));
            Check(l[0].AthleticsText == "50% ± 10" && l[0].HealthText == "HP 70%" && l[0].AthleticsColor == AltColors.Of(BarMath.YellowHex), "new stats not pushed: " + l[0].AthleticsText);
            live.Army();

            // 11. vanilla hides its markers: removed QUIETLY; shown again: rebuilt quietly, fresh labels
            int text = LogText.Length;
            live.Shown = false;
            view.Tick(Frame(t += 0.05, player));
            Check(!layer.Up && !view.IsLayerUp, "the labels stayed with vanilla's markers hidden");
            live.Shown = true;
            view.Tick(Frame(t += 0.05, player));
            vm = view.CurrentViewModel!;
            l = vm.Labels;
            Check(layer.Up && l.Count == 3 && l[0].IsShown && l[0].AthleticsText == "72% ± 8", "shown again: no fresh labels");
            string again = LogText.Substring(text);
            Check(!again.Contains("ALT markers: layer created") && !again.Contains("ALT markers: layer removed") && !again.Contains("first shown"),
                "hiding and showing again was logged outside the verbose log: " + again);

            // 12. vanilla's flag unreadable: its rule copied - ALT held or the orders menu open
            live.Readable = false;
            view.Tick(Frame(t += 0.05, player));
            Check(!layer.Up, "the copied rule: shown without ALT or the orders menu");
            view.Tick(Frame(t += 0.05, player, alt: true));
            Check(layer.Up, "the copied rule: ALT held did not show the labels");
            view.Tick(Frame(t += 0.05, player, orderMenu: true));
            Check(layer.Up, "the copied rule: the orders menu open did not keep them");
            live.Readable = true;

            // 13. a new show with a marker without its widget: a nominal size for it, logged once
            live.Shown = false;
            view.Tick(Frame(t += 0.05, player));
            live.Shown = true;
            live.Widgets.RemoveAt(1);
            view.Tick(Frame(t += 0.05, player));
            l = view.CurrentViewModel!.Labels;
            Check(view.Technique == AltMarkerTechnique.GamePoints && l[1].IsShown && l[1].PositionX == 500f && l[1].PositionY == 556f, "an unpaired marker: no nominal label");
            LogHas(" s (show #4) - 1 of 4 markers had no widget at their point (stand-in layer MissionFormationMarker, movie FormationMarker: 4 markers, 3 marker widgets): "
                   + "yours 2 Archers (20 men) at (600, 500) 60 x 108 (nominal), 80 m - a nominal marker size for those");
            live.Army();

            // 14. no marker widgets at all: the game's points with a nominal size
            live.Shown = false;
            view.Tick(Frame(t += 0.05, player));
            live.Shown = true;
            live.State = MarkerRead.PointsOnly;
            view.Tick(Frame(t += 0.05, player));
            l = view.CurrentViewModel!.Labels;
            Check(view.Technique == AltMarkerTechnique.GamePoints && l.Count == 3 && l[0].PositionY == 456f, "no widgets: the game's points with a nominal size");
            LogHas("[hud] ALT markers: FALLBACK at ");
            LogHas(" s (show #5) - stand-in layer MissionFormationMarker, movie FormationMarker: 4 markers, 0 marker widgets - the game's points with a nominal marker size (60 x 108 UI px) until the markers go");
            live.State = MarkerRead.Live;

            // 15. no marker layer: our own projection for the rest of the show; the next show tries the game's markers again
            live.Shown = false;
            view.Tick(Frame(t += 0.05, player));
            live.State = MarkerRead.Unavailable;
            live.Readable = false;
            view.Tick(Frame(t += 0.05, player, alt: true));
            l = view.CurrentViewModel!.Labels;
            Check(view.Projecting && view.Technique == AltMarkerTechnique.OwnProjection && l.Count == 3 && l[0].PositionX == 860f && l[0].PositionY == 456f,
                "no layer: not our own projection");
            LogHas(" s (show #6) - stand-in: no MissionFormationMarker layer on the screen - our own projection of the same point (the formation's median + 3 m), the game's rule copied"
                   + " (ALT held or the orders menu open), until the markers go; the next show tries the game's markers again");
            int reads = live.Reads;
            view.Tick(Frame(t += 0.05, player, alt: true));
            Check(live.Reads == reads, "the live source was read again in a projected show");
            live.State = MarkerRead.Live;
            live.Readable = true;
            view.Tick(Frame(t += 0.05, player)); // vanilla's flag readable again, shown = false → gone
            view.Tick(Frame(t += 0.05, player, alt: true));
            Check(!layer.Up, "vanilla's flag (hidden) was not preferred over the copied rule");
            live.Shown = true;
            view.Tick(Frame(t += 0.05, player));
            Check(layer.Up && !view.Projecting && view.Technique == AltMarkerTechnique.Live, "the next show did not try the game's markers again");

            // 16. the usual gates remove it with their reason
            S.Set(SettingsSchema.ShowAltMarkerStats, false, SettingSources.Mcm);
            view.Tick(Frame(t += 0.05, player));
            Check(!layer.Up && view.Stats.RemovedBy(HudHide.ToggleOff) == 1, "ShowAltMarkerStats off: the labels stayed");
            S.Set(SettingsSchema.ShowAltMarkerStats, true, SettingSources.Mcm);
            view.Tick(Frame(t += 0.05, player));
            view.Tick(Frame(t += 0.05, player, hideUi: true));
            Check(!layer.Up && view.Stats.RemovedBy(HudHide.HideBattleUI) == 1, "Hide battle UI: the labels stayed");
            view.Tick(Frame(t += 0.05, player, MissionMode.Deployment));
            Check(!layer.Up, "deployment: the labels are up");
            S.Set(SettingsSchema.AthleticsEnabled, false, SettingSources.Mcm);
            view.Tick(Frame(t += 0.05, player));
            Check(!layer.Up && view.LastDecision == HudHide.AthleticsOff, "Athletics off: the labels are up");
            S.Set(SettingsSchema.AthleticsEnabled, true, SettingSources.Mcm);

            // 17. mission end and the summary, through the logic as at a real mission end
            view.Tick(Frame(t += 0.05, player));
            view.Finish(t += 0.5);
            Check(!layer.Up, "mission end: the labels stayed");
            ((List<TraxHudView>)typeof(AthleticsLogic).GetField("_hudViews", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(logic)!).Add(view);
            typeof(AthleticsLogic).GetMethod("WriteHudSummary", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(logic, null);
            LogHas("[summary] hud: ALT markers (movie TraxAltMarkers) - on screen ");
            LogHas("[summary] hud: ALT markers - shown 9x, on screen ");
            LogHas(" s; technique: the game's own formation markers read live (their points and their widgets' sizes) (stand-in layer MissionFormationMarker, movie FormationMarker: 4 markers, 4 marker widgets)"
                   + " (shows: live 6, the game's points + a nominal size 2, own projection 1); formations labelled: yours 3, allies 0, enemy 1 (most at once 4), frames with a label pinned at the screen's edge 1, values pushed ");
            LogHas("; fallbacks 3 (no marker layer 1, no marker widgets 1, a marker without its widget 1) - the first at ");
            LogHas(" s: 1 of 4 markers had no widget at their point");
            Check(Occurrences(LogText, "[hud] ALT markers: first shown", logFrom) == 1, "the first show was logged more than once");
            SetStatic(typeof(AthleticsLogic), "_current", null);
            AltMarkerDefaults();
        }

        /// <summary>From the master-switch step: the labels go with ModEnabled and come back.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AltMarkersFollowTheMasterSwitch(Agent player)
        {
            AltMarkerDefaults();
            var live = new FakeMarkers { Shown = true };
            live.Army();
            var view = new AltMarkerView();
            var layer = new FakeHudLayer();
            view.UseLayer(layer);
            view.UseSources(live, new FakeMarkers { State = MarkerRead.Projected });
            view.Tick(Frame(920, player));
            Check(layer.Up && view.CurrentViewModel!.Labels.Count == 3, "master switch: precondition - the ALT labels are not up");
            S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
            view.Tick(Frame(921, player));
            Check(!layer.Up && view.LastDecision == HudHide.ModOff, "mod off: the ALT labels stayed on screen");
            LogHas("[hud] ALT markers: layer removed at 921.0 s - ModEnabled off (the master switch)");
            S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
            view.Tick(Frame(922, player));
            Check(layer.Up && layer.Created == 2 && view.CurrentViewModel!.Labels[0].IsShown, "mod back on: the ALT labels did not come back");
            view.Finish(923);
        }

        /// <summary>The fail safe: a failing marker read disables the view for the mission.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AltMarkersFailSafe()
        {
            AltMarkerDefaults();
            var player = FakeAgent(43);
            var live = new FakeMarkers { Shown = true };
            live.Army();
            var view = new AltMarkerView();
            var layer = new FakeHudLayer();
            view.UseLayer(layer);
            view.UseSources(live, new FakeMarkers { State = MarkerRead.Projected });
            view.Tick(Frame(30, player));
            Check(layer.Up, "precondition: the labels are not up");
            live.Throw = true;
            int errors = TraxLog.ErrorCount;
            view.Tick(Frame(31, player));
            view.Tick(Frame(32, player));
            Check(view.IsDisabled && !layer.Up && TraxLog.ErrorCount == errors + 1, "a failing marker read did not disable the labels (or logged twice)");
            // (the [error] line itself: the "hud.tick" site's burst of 3 is spent by the earlier fail-safe steps in this
            // fast run - the count above and the DISABLED line below prove it)
            LogHas("[hud] ALT markers: an error in tick at 31.0 s (InvalidOperationException: smoke: the marker read failed - stack in the [error] line) - DISABLED for the rest of this battle");
            Check(view.MarkerStats.SummaryLine(view.Stats.Errors).EndsWith("; errors 1", StringComparison.Ordinal), "the summary does not count the error");

            // a view never given its sources fails safe too (the copied rule shows it, the build finds no source)
            var orphan = new AltMarkerView();
            orphan.UseLayer(new FakeHudLayer());
            orphan.Tick(Frame(33, player, alt: true));
            Check(orphan.IsDisabled, "an ALT view without sources did not disable itself");
        }
    }
}

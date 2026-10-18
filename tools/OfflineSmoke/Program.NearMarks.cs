using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
    /// Step 58 (Anton, 2026-10-18: "this ALT view is too cluttered. Maybe add those lines as some averages on the group, and only show them on up
    /// to 5 soldiers that I'm directly looking at and are really close, making it work in tournaments too"): the REAL <see cref="FeverMarkView"/>
    /// over the REAL logic's men with step 44's stand-in camera in the NEAR view - the five men nearest the screen's centre within 15 m (a man
    /// nearer the camera but at the screen's edge waits, a dead-centre man beyond the range never counts), the enemy switch, the cap and the range
    /// live, FeverMarkMax the ceiling, no fade, the every-man view unchanged behind AltMarksEveryone, the master switch, the log and the summary;
    /// a TOURNAMENT (no formation markers: the flag unreadable) - the near bars while the game's ALT key is held; the REAL <see cref="AltMarkerView"/>'s
    /// labels with the formation's fever share; the cost - 1000 men on the screen in the near view.
    /// What it cannot check: the spectators of a real arena (Team.Invalid - the real source's Locate; PLAYTEST D27) and the look in the game.
    /// </summary>
    internal static partial class Program
    {
        private static void NearMarkDefaults()
        {
            AthleticsMarkDefaults();                                                            // step 46's bars on (step 44's / 46's pin the every-man view) ...
            S.Set(SettingsSchema.AltMarksEveryone, false, SettingSources.File);                // ... then step 58's near view
            S.Set(SettingsSchema.AltNearMarksMax, 5, SettingSources.File);
            S.Set(SettingsSchema.AltNearMarksMetres, 15.0, SettingSources.File);
            S.Set(SettingsSchema.ShowFeverOnLabels, true, SettingSources.File);
        }

        // ------------------------------------------------------------------ the near view through the real view

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void NearMarksThroughTheView()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                SprintDefaults();
                FeverDefaults();
                NearMarkDefaults();
                S.Set(SettingsSchema.SprintAiEnabled, false, SettingSources.File);
                var body = new FakeSprintBody();
                var logic = NewFeverLogic(body, new FakeWeaponFacts(), 0.99);
                var src = new FakeFeverMarks();
                double t = 9800;
                // the camera at (0, 0, 2.5) looking along +y; each bar's point 2.5 m up - so a man's angle off the centre = atan(x / y)
                FeverMan(logic, body, src, 300, 2, 0f, 10f);                                     // 0°, 10 m - dead centre
                FeverMan(logic, body, src, 301, 2, 1f, 8f);                                      // 7°
                FeverMan(logic, body, src, 302, 2, -2f, 6f);                                     // 18°, 6.3 m
                FeverMan(logic, body, src, 303, 2, 3f, 12f);                                     // 14°
                FeverMan(logic, body, src, 304, 2, -4f, 9f);                                     // 24° - the sixth: waits
                FeverMan(logic, body, src, 305, 2, 5f, 5f);                                      // 45°, 7.1 m - nearer the camera than most, at the edge: waits
                FeverMan(logic, body, src, 306, 2, 0.2f, 25f);                                   // 0.5°, 25 m - beyond the range
                FeverMan(logic, body, src, 307, 2, -6f, 4f);                                     // 56°, 7.2 m - waits
                FeverMan(logic, body, src, 308, 3, 0.5f, 13f, (int)MarkerTeam.Enemy);            // 2°, the enemy's - picked (his switch on)
                var me = FeverMan(logic, body, src, 309, 0, 0f, 3f);
                logic.SmokePlayer = me.Agent;
                var player = me.Agent;

                var view = new FeverMarkView();
                var layer = new FakeHudLayer();
                view.UseLayer(layer);
                view.UseSource(src);
                int logFrom = LogText.Length;
                src.Shown = true;

                // 1. the near view: the five nearest the screen's centre within 15 m, the enemy's too; the rest wait unprojected
                int projections = src.Projections;
                view.Tick(Frame(t, player));
                var fr = view.LastFrame;
                Check(layer.Up && view.DrawnNow == 5 && view.BarsNow == 5, "near: 5 men expected, got " + view.DrawnNow);
                Check(MarkOf(view, 300) != null && MarkOf(view, 308) != null && MarkOf(view, 301) != null && MarkOf(view, 303) != null && MarkOf(view, 302) != null
                      && MarkOf(view, 304) == null && MarkOf(view, 305) == null && MarkOf(view, 307) == null && MarkOf(view, 306) == null && MarkOf(view, 309) == null,
                    "near: not the five centre-most close men (300, 308, 301, 303, 302) - " + Drawn(view));
                Check(fr.Skipped[(int)FeverMarkSkip.TooFar] == 1 && fr.Skipped[(int)FeverMarkSkip.Capped] == 3 && fr.Skipped[(int)FeverMarkSkip.OffScreen] == 0
                      && src.Projections - projections == 5 && fr.Marks[fr.Order[0]].Key == 300 && fr.Marks[fr.Order[1]].Key == 308,
                    "near: the skips / ranking - " + FeverMarkMath.DescribeSkips(fr.Skipped) + ", projected " + (src.Projections - projections));
                var close = MarkOf(view, 302)!;
                Check(SameColor(close.BarColor, BandColorOf(BarBand.Green)), "near: the 6.3 m man's bar is faded (" + close.BarColor.Alpha + ") - the near view has no distance fade");
                LogHas("[hud] ALT bars: first bars drawn at " + TraxHudView.S1(t) + " s (show #1) - 9 men looked at (yours and allies 8, enemy 1), 0 in a fever (armed 0, burning 0); "
                       + "5 men marked (the 5 nearest the screen's centre of 8 candidates within 15.0 m - AltNearMarksMax): 5 Athletics bars (green 5, blue 0, yellow 0, orange 0, red 0; wounded 0), 0 fever bars; "
                       + "skipped: beyond the near range 1, beyond the cap 3; ");
                LogHas("the first drawn: yours man 300 at (960, 540), 10.0 m, calm, Athletics 100% green, 0° off the centre | enemy man 308 at (998, 540), 13.0 m, calm, Athletics 100% green, 2° off the centre");
                LogHas("; source: stand-in camera (pinhole, focal 1000 px); mode near: up to 5 men nearest the screen's centre (AltNearMarksMax 5, FeverMarkMax 200 the ceiling) within 15.0 m of the camera (AltNearMarksMetres), no distance fade");

                // 2. the enemy switch still works: his bar goes, the next man (304) takes the slot
                S.Set(SettingsSchema.AthleticsMarksShowEnemy, false, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                Check(MarkOf(view, 308) == null && MarkOf(view, 304) != null && view.DrawnNow == 5 && view.LastFrame.Skipped[(int)FeverMarkSkip.EnemyOff] == 1,
                    "near: AthleticsMarksShowEnemy off - " + Drawn(view));
                S.Set(SettingsSchema.AthleticsMarksShowEnemy, true, SettingSources.Mcm);

                // 3. the cap and the range live; FeverMarkMax the ceiling
                S.Set(SettingsSchema.AltNearMarksMax, 2, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                Check(view.DrawnNow == 2 && MarkOf(view, 300) != null && MarkOf(view, 308) != null, "near: AltNearMarksMax 2 - " + Drawn(view));
                S.Set(SettingsSchema.AltNearMarksMax, 5, SettingSources.Mcm);
                S.Set(SettingsSchema.AltNearMarksMetres, 30.0, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                Check(view.DrawnNow == 5 && MarkOf(view, 306) != null && MarkOf(view, 302) == null && view.LastFrame.Skipped[(int)FeverMarkSkip.TooFar] == 0,
                    "near: AltNearMarksMetres 30 - the 25 m man at 0.5° not in: " + Drawn(view));
                S.Set(SettingsSchema.AltNearMarksMetres, 15.0, SettingSources.Mcm);
                S.Set(SettingsSchema.FeverMarkMax, 3, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                Check(view.DrawnNow == 3, "near: FeverMarkMax 3 is not the ceiling - " + view.DrawnNow);
                S.Set(SettingsSchema.FeverMarkMax, 200, SettingSources.Mcm);

                // 4. the every-man view (AltMarksEveryone): step 46 exactly - every man on the screen, no range, vanilla's fade
                S.Set(SettingsSchema.AltMarksEveryone, true, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                fr = view.LastFrame;
                close = MarkOf(view, 302)!;
                float fade = ((float)Math.Sqrt(40) - 1.5f) / 6.5f;
                Check(view.DrawnNow == 7 && MarkOf(view, 306) != null && MarkOf(view, 304) != null && fr.Skipped[(int)FeverMarkSkip.OffScreen] == 2
                      && fr.Skipped[(int)FeverMarkSkip.TooFar] == 0 && fr.Skipped[(int)FeverMarkSkip.Capped] == 0 && Math.Abs(close.BarColor.Alpha - fade) < 1e-3f,
                    "everyone: not step 46's view - " + Drawn(view) + ", skipped " + FeverMarkMath.DescribeSkips(fr.Skipped) + ", the 6.3 m fade " + close.BarColor.Alpha);
                S.Set(SettingsSchema.AltMarksEveryone, false, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                Check(view.DrawnNow == 5 && MarkOf(view, 306) == null, "near again: not 5 - " + Drawn(view));

                // 5. the master switch: gone at once, back with the near view
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                Check(!layer.Up && view.LastDecision == HudHide.ModOff, "near: ModEnabled off - the bars stayed");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                Check(layer.Up && view.DrawnNow == 5, "near: ModEnabled on - the near bars did not come back (" + view.DrawnNow + ")");

                // 6. the summary
                view.Finish(t += 0.5);
                WriteSummaryThrough(logic, view);
                LogHas("; mode (step 58): near ");
                LogHas(" frames (men marked a frame: mean ");
                LogHas(", max 5 - up to 5 nearest the screen's centre within 15.0 m of the camera at the first), everyone 1 frames; errors 0");
                Check(Occurrences(LogText, "[hud] ALT bars: first bars drawn", logFrom) == 1, "near: the first bars were logged more than once");
                logic.SmokePlayer = null;
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
                FeverMarkDefaults();
                ConfigStore.Reload("after the near view smoke");
            }
        }

        private static string Drawn(FeverMarkView view)
        {
            var fr = view.LastFrame;
            var keys = new List<string>();
            for (int k = 0; k < view.DrawnNow && k < fr.Picked; k++) keys.Add(fr.Marks[fr.Order[k]].Key.ToString(CultureInfo.InvariantCulture));
            return view.DrawnNow + " drawn (" + string.Join(", ", keys) + ")";
        }

        // ------------------------------------------------------------------ a tournament: no formation markers, the game's ALT key

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void NearMarksInATournament()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                SprintDefaults();
                FeverDefaults();
                NearMarkDefaults();
                S.Set(SettingsSchema.SprintAiEnabled, false, SettingSources.File);
                var body = new FakeSprintBody();
                var logic = NewFeverLogic(body, new FakeWeaponFacts(), 0.99);
                var src = new FakeFeverMarks { Readable = false };                               // no MissionFormationMarker layer in an arena
                double t = 9900;
                // a round of four teams of two: your mate, six opponents of three other teams (all "enemy" to you), all in no formation
                FeverMan(logic, body, src, 320, 0, 1.5f, 5f);                                     // your team mate, close, 17°
                var opponent = FeverMan(logic, body, src, 321, 0, 0.2f, 4f, (int)MarkerTeam.Enemy); // the man you fight: dead ahead
                FeverMan(logic, body, src, 322, 0, -3f, 7f, (int)MarkerTeam.Enemy);              // 23°
                FeverMan(logic, body, src, 323, 0, 4f, 9f, (int)MarkerTeam.Enemy);               // 24°
                FeverMan(logic, body, src, 324, 0, -1f, 12f, (int)MarkerTeam.Enemy);             // 5°
                FeverMan(logic, body, src, 325, 0, 8f, 10f, (int)MarkerTeam.Enemy);              // 39°
                FeverMan(logic, body, src, 326, 0, 0f, 20f, (int)MarkerTeam.Enemy);              // across the arena: beyond 15 m
                var me = FeverMan(logic, body, src, 327, 0, 0f, 1f);
                logic.SmokePlayer = me.Agent;
                var player = me.Agent;
                Blows(opponent, 7, t);                                                            // he is tired: 30% (orange)
                var view = new FeverMarkView();
                var layer = new FakeHudLayer();
                view.UseLayer(layer);
                view.UseSource(src);

                // ALT not held: nothing (the copied rule - the marker flag cannot be read)
                view.Tick(Frame(t, player, MissionMode.Tournament));
                Check(!layer.Up && view.LastDecision == HudHide.ViewCondition, "tournament: the bars are up with ALT not held");
                // ALT held: the near bars - your opponent first, the five nearest the centre within 15 m
                view.Tick(Frame(t += 0.05, player, MissionMode.Tournament, alt: true));
                var fr = view.LastFrame;
                var o = MarkOf(view, 321);
                Check(layer.Up && view.DrawnNow == 5 && o != null && fr.Marks[fr.Order[0]].Key == 321 && MarkOf(view, 325) == null && MarkOf(view, 326) == null,
                    "tournament with ALT held: not the near bars with the opponent first - " + Drawn(view));
                Check(o != null && Math.Abs(o.BarFill - 0.3f) < 1e-5f && SameColor(o.BarColor, BandColorOf(BarBand.Orange)) && o.BarColor.Alpha == 1f,
                    "tournament: the opponent's tiredness does not read - fill " + o?.BarFill + ", alpha " + o?.BarColor.Alpha);
                // let go of ALT: gone
                view.Tick(Frame(t += 0.05, player, MissionMode.Tournament));
                Check(!layer.Up, "tournament: the bars stayed after ALT was let go");
                view.Finish(t += 0.5);
                logic.SmokePlayer = null;
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
                FeverMarkDefaults();
                ConfigStore.Reload("after the tournament near view smoke");
            }
        }

        // ------------------------------------------------------------------ the labels: the group's fever share

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FeverShareUnderTheMarkers()
        {
            AltMarkerDefaults();
            ReadyDefaults();
            S.Set(SettingsSchema.ShowFeverOnLabels, true, SettingSources.File);
            S.Set(SettingsSchema.FeverEnabled, true, SettingSources.File);
            var logic = StripLogic();
            var player = FakeAgent(45);
            var live = new FakeMarkers { Shown = true };
            live.Add(0, 0, 0, 960, 400, 40, 0.72, 0.08, 0.90, 0.81, bracing: 6, inFever: 12);   // yours: a charge - 12 of 40 surging
            live.Add(0, 0, 1, 600, 500, 20, 0.45, 0.20, 0.60, 0.60);                             // yours: calm - no fever line
            live.Add(1, 2, 0, 1300, 350, 80, 0.30, 0.10, 0.40, 0.90, inFever: 60);               // the enemy's charge
            var projection = new FakeMarkers { State = MarkerRead.Projected, Name = "stand-in projection" };
            var view = new AltMarkerView();
            var layer = new FakeHudLayer();
            view.UseLayer(layer);
            view.UseSources(live, projection);
            double t = 1;

            view.Tick(Frame(t, player));
            var l = view.CurrentViewModel!.Labels;
            Check(layer.Up && l.Count == 3, "fever share: 3 labels expected, got " + l.Count);
            Check(l[0].FeverShown && l[0].FeverText == "fever 12/40" && !l[1].FeverShown && l[2].FeverShown && l[2].FeverText == "fever 60/80"
                  && l[0].ReadyText == "ready 34/40" && l[0].AthleticsText == "72% ± 8",
                "fever share: '" + l[0].FeverText + "' (" + l[0].FeverShown + ") / " + l[1].FeverShown + " / '" + l[2].FeverText + "'");
            view.Tick(Frame(t += 0.2, player));
            LogHas(": yours 1 Infantry 72% ± 8 HP 81% ready 34/40 (40 men, f 0.90) fever 12/40 | yours 2 Archers 45% ± 20 HP 60% ready 20/20 (20 men, f 0.60) | enemy 1 Infantry 30% ± 10 HP 90% ready 80/80 (80 men, f 0.40) fever 60/80");
            LogHas(", fever share on (\"fever 12/40\", shown while a man of the formation is in a fever)");

            // the switches live, at the next frame: ShowFeverOnLabels, FeverEnabled; the master switch takes the labels
            S.Set(SettingsSchema.ShowFeverOnLabels, false, SettingSources.Mcm);
            view.Tick(Frame(t += 0.05, player));
            Check(!l[0].FeverShown && !l[2].FeverShown && l[0].IsShown && l[0].ReadyShown, "fever share: ShowFeverOnLabels off - the line stayed (or the label went)");
            S.Set(SettingsSchema.ShowFeverOnLabels, true, SettingSources.Mcm);
            view.Tick(Frame(t += 0.05, player));
            Check(l[0].FeverShown, "fever share: ShowFeverOnLabels on - the line did not come back");
            S.Set(SettingsSchema.FeverEnabled, false, SettingSources.Mcm);
            view.Tick(Frame(t += 0.05, player));
            Check(!l[0].FeverShown && !l[2].FeverShown, "fever share: FeverEnabled off - the line stayed");
            S.Set(SettingsSchema.FeverEnabled, true, SettingSources.Mcm);
            view.Tick(Frame(t += 0.05, player));
            // the surge ends (new stats on the version): the line goes
            var m = live.Markers[0];
            var st = m.Stats;
            m.Stats = new FormationAthleticsStats(st.Count, st.MeanPoints, st.StdPoints, st.MeanFraction, st.StdFraction, st.Exhausted, st.MeanPeakShare, st.InPeak, st.MeanHealth, st.Bracing, 0);
            live.Markers[0] = m;
            BumpFormationStats(logic);
            view.Tick(Frame(t += 0.05, player));
            Check(!l[0].FeverShown && l[2].FeverShown, "fever share: the surge over - the line stayed");
            S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
            view.Tick(Frame(t += 0.2, player));
            Check(!layer.Up && view.LastDecision == HudHide.ModOff, "fever share: ModEnabled off - the labels stayed");
            S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
            view.Tick(Frame(t += 0.2, player));
            Check(layer.Up && view.CurrentViewModel!.Labels[2].FeverText == "fever 60/80", "fever share: ModEnabled on - the line did not come back");

            view.Finish(t += 0.5);
            WriteSummaryThrough(logic, view);
            LogHas("[summary] hud: ALT markers - fever share (step 58): shown ");
            LogHas("x, the most in a fever 60/80 (75%, enemy 1 Infantry); hidden ");
            LogHas("FeverEnabled off 3, ShowFeverOnLabels off 3, nobody in a fever ");
            SetStatic(typeof(AthleticsLogic), "_current", null);
        }

        // ------------------------------------------------------------------ the cost

        /// <summary>The cost: 1000 men ALL on the screen, the near view - the walk and the placement as step 46, the pick projecting about the cap's
        /// worth, nothing allocated once the pool has grown.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void NearMarksCostStaysFlat()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                SprintDefaults();
                FeverDefaults();
                NearMarkDefaults();
                S.Set(SettingsSchema.SprintAiEnabled, false, SettingSources.File);
                var body = new FakeSprintBody();
                var logic = NewFeverLogic(body, new FakeWeaponFacts(), 0.99);
                var src = new FakeFeverMarks { Shown = true };
                const int men = 1000;
                for (int i = 0; i < men; i++)
                {
                    float x = i % 50 * 0.6f - 15f, y = 3f + (i / 50) * 1.2f;                     // a dense crowd 3-26 m ahead, 30 m wide: ~600 in range
                    FeverMan(logic, body, src, 8000 + i, i % 8, x, y, i % 2 == 0 ? (int)MarkerTeam.Yours : (int)MarkerTeam.Enemy);
                }
                var view = new FeverMarkView();
                var layer = new FakeHudLayer();
                view.UseLayer(layer);
                view.UseSource(src);
                double t = 18000;
                for (int k = 0; k < 5; k++) view.Tick(Frame(t += 1 / 60.0, null));
                int candidates = view.LastFrame.Eligible;
                Check(layer.Up && view.DrawnNow == 5 && candidates > 100 && view.LastFrame.Skipped[(int)FeverMarkSkip.TooFar] > 100,
                    "near cost: precondition - not 5 of many candidates: " + view.DrawnNow + " of " + candidates);
                const int frames = 300;
                int projections = src.Projections;
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var sw = Stopwatch.StartNew();
                for (int k = 1; k <= frames; k++)
                {
                    src.CameraX = (k % 20) * 0.05f;
                    view.Tick(Frame(t += 1 / 60.0, null));
                }
                sw.Stop();
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double msPerFrame = sw.Elapsed.TotalMilliseconds / frames;
                double projected = (src.Projections - projections) / (double)frames;
                Console.WriteLine("        step 58 cost: " + men + " men on the screen, " + candidates + " within " + S.AltNearMarksMetres.ToString("0", CultureInfo.InvariantCulture)
                                  + " m, the near view - " + msPerFrame.ToString("0.000", CultureInfo.InvariantCulture) + " ms a frame (the view's own stopwatch: "
                                  + (view.MarkStats.MeanCostMs * 1000).ToString("0", CultureInfo.InvariantCulture) + " µs mean), " + allocated + " bytes allocated over " + frames
                                  + " frames; " + view.DrawnNow + " men marked, " + projected.ToString("0.0", CultureInfo.InvariantCulture) + " projections a frame");
                Check(view.DrawnNow == 5, "near cost: not 5 marked: " + view.DrawnNow);
                Check(projected <= 5.5, "near cost: more than the cap's worth projected a frame: " + projected);
                Check(msPerFrame < 1.0, "near cost: a frame took " + msPerFrame + " ms (budget 1 ms)");
                Check(allocated < 64 * 1024, "near cost: the near view allocates - " + allocated + " bytes over " + frames + " frames");
                view.Finish(t + 1);
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
                FeverMarkDefaults();
                ConfigStore.Reload("after the near view cost smoke");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.Library;
using TraxCombat.Core;
using TraxCombat.Hud;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 24's ready count ("ready 34/50" - the men NOT bracing) through the REAL
    /// <see cref="OrderStripView"/> and <see cref="AltMarkerView"/> with stand-in cards, markers and formation stats (their
    /// Bracing counts): the text and its colour by the share ready, the strip cell's extra row (and the bottom card lifted
    /// only while it is drawn), the fallback panel's rows, new stats on the version, the live switches (BraceEnabled,
    /// ShowReadyCount, the thresholds), the master switch, the values lines and the [summary] lines. The prefabs are
    /// checked by the two prefab steps (every new widget against the game's types, brushes and the VMs' property types).
    /// </summary>
    internal static partial class Program
    {
        // Nested, not on Program: a static field of a game type would load TaleWorlds.Library in Program's type
        // initializer - before Main hooks the resolver that finds the game's DLLs.
        private static class ReadyColors
        {
            public static readonly Color Plain = Color.ConvertStringToColor(ReadyMath.PlainHex);
            public static readonly Color Yellow = Color.ConvertStringToColor(BarMath.YellowHex);
            public static readonly Color Red = Color.ConvertStringToColor(BarMath.RedHex);
        }

        private static void ReadyDefaults()
        {
            S.Set(SettingsSchema.BraceEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.ShowReadyCount, true, SettingSources.File);
            S.Set(SettingsSchema.ReadyYellowBelowPercent, 75, SettingSources.File);
            S.Set(SettingsSchema.ReadyRedBelowPercent, 50, SettingSources.File);
            S.Set(SettingsSchema.OrderStripReadyOffset, 24, SettingSources.File);
        }

        private static void WriteSummaryThrough(AthleticsLogic logic, TraxHudView view)
        {
            ((List<TraxHudView>)typeof(AthleticsLogic).GetField("_hudViews", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(logic)!).Add(view);
            typeof(AthleticsLogic).GetMethod("WriteHudSummary", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(logic, null);
        }

        // ------------------------------------------------------------------ the orders strip

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ReadyCountOnTheStrip()
        {
            StripDefaults();
            ReadyDefaults();
            var logic = StripLogic();
            var player = FakeAgent(43);
            var army = new FakeStripFormations();
            army.Set(0, 40, 0.72, 0.08, 0.90, 0.81, bracing: 4);     // 36/40 = 90%: plain
            army.Set(1, 20, 0.45, 0.20, 0.60, 0.60, bracing: 8);     // 12/20 = 60%: yellow
            army.Set(2, 12, 0.10, 0.05, 0.13, 0.95, 3, bracing: 9);  // 3/12 = 25%: red
            army.Set(3, 9, 0.50, 0.10, 0.66, 0.70);                  // the column's bottom card: 9/9
            var cards = new FakeCards();
            cards.Layout(army, 2, 0);
            var view = new OrderStripView();
            var layer = new FakeHudLayer();
            view.UseLayer(layer);
            view.UseSources(cards, army);
            double t = 1;

            // 1. open: "ready N/M" under each cell's bar, coloured by the share ready; the cell one row deeper
            view.Tick(Frame(t, player, orderMenu: true));
            var vm = view.CurrentViewModel!;
            var c = vm.Cells;
            Check(view.UnderCards && c[0].ReadyShown && c[0].ReadyText == "ready 36/40" && c[0].ReadyColor == ReadyColors.Plain,
                "strip: Infantry's ready count: " + c[0].ReadyShown + " '" + c[0].ReadyText + "'");
            Check(c[1].ReadyText == "ready 12/20" && c[1].ReadyColor == ReadyColors.Yellow && c[2].ReadyText == "ready 3/12" && c[2].ReadyColor == ReadyColors.Red
                  && c[3].ReadyText == "ready 9/9" && c[3].ReadyColor == ReadyColors.Plain, "strip: the colours by the share ready (Archers yellow, Cavalry red)");
            Check(c[0].ReadyMarginTop == 23f && c[0].TextMarginTop == 0f && c[0].BarMarginTop == 19f && c[0].PositionY == 268f,
                "strip: the ready row's place in the cell: margin " + c[0].ReadyMarginTop + ", cell y " + c[0].PositionY);
            Check(c[3].IsShown && Near(c[3].PositionY, 1080f - 38.6f), "strip: the bottom card's deeper cell was not lifted to the screen's edge: y " + c[3].PositionY);
            LogHas("cells (numbers 13 px at card bottom +1, bar 4 px at +20, ready count at +24, 2 px in from the sides (39 px deep; UI pixels - the game's UI scale applies)): ");
            LogHas("4 Horse archers at (20, 1041) w 131 - 1 cell lifted to the screen's bottom edge");
            LogHas(" (open #1, under the cards): 1 Infantry 72% ± 8 HP 81% ready 36/40 (40 men, f 0.90) | 2 Archers 45% ± 20 HP 60% ready 12/20 (20 men, f 0.60)"
                   + " | 3 Cavalry 10% ± 5 HP 95% ready 3/12 (12 men, f 0.13, 3 exhausted) | 4 Horse archers 50% ± 10 HP 70% ready 9/9 (9 men, f 0.66)"
                   + " - ± is 1.00 std, health on, ready count on: yellow at or below 75%, red at or below 50% of the men ready");

            // 2. the fallback panel's rows carry it too (the same cell ViewModels)
            S.Set(SettingsSchema.OrderStripUnderCards, false, SettingSources.Mcm);
            view.Tick(Frame(t += 0.2, player, orderMenu: true));
            Check(view.PanelUp && vm.PanelShown && c[0].ReadyShown && c[2].ReadyText == "ready 3/12", "strip: the panel rows lost the ready count");
            S.Set(SettingsSchema.OrderStripUnderCards, true, SettingSources.Mcm);
            view.Tick(Frame(t += 0.2, player, orderMenu: true));
            Check(view.UnderCards, "strip: not back under the cards");

            // 3. new stats reach it when the logic's version moves: more men bracing = fewer ready, red
            army.Set(0, 40, 0.30, 0.08, 0.40, 0.81, bracing: 25);  // 15/40 = 38%
            BumpFormationStats(logic);
            view.Tick(Frame(t += 0.2, player, orderMenu: true));
            Check(c[0].ReadyText == "ready 15/40" && c[0].ReadyColor == ReadyColors.Red, "strip: new stats not pushed: " + c[0].ReadyText);

            // 4. the thresholds live: yellow at or below 95% turns the full Horse archers... no - 9/9 is 100%; Infantry red → the red line lowered
            S.Set(SettingsSchema.ReadyRedBelowPercent, 30, SettingSources.Mcm);
            view.Tick(Frame(t += 0.2, player, orderMenu: true));
            Check(c[0].ReadyColor == ReadyColors.Yellow && c[2].ReadyColor == ReadyColors.Red, "strip: ReadyRedBelowPercent 30 did not apply at the next refresh");
            S.Set(SettingsSchema.ReadyRedBelowPercent, 50, SettingSources.Mcm);

            // 5. BraceEnabled off: the line goes, the cell back to step 9's depth (the bottom card no longer lifted); on: back
            S.Set(SettingsSchema.BraceEnabled, false, SettingSources.Mcm);
            view.Tick(Frame(t += 0.2, player, orderMenu: true));
            Check(!c[0].ReadyShown && !c[1].ReadyShown && !c[3].ReadyShown && c[3].PositionY == 1057f,
                "strip: BraceEnabled off - the ready count stayed (or the bottom cell stayed lifted: y " + c[3].PositionY + ")");
            S.Set(SettingsSchema.BraceEnabled, true, SettingSources.Mcm);
            view.Tick(Frame(t += 0.2, player, orderMenu: true));
            Check(c[0].ReadyShown && c[0].ReadyText == "ready 15/40" && Near(c[3].PositionY, 1080f - 38.6f), "strip: BraceEnabled on again - the ready count did not come back");

            // 6. ShowReadyCount off / on, live
            S.Set(SettingsSchema.ShowReadyCount, false, SettingSources.Mcm);
            view.Tick(Frame(t += 0.2, player, orderMenu: true));
            Check(!c[0].ReadyShown && c[0].HealthShown, "strip: ShowReadyCount off - the ready count stayed");
            S.Set(SettingsSchema.ShowReadyCount, true, SettingSources.Mcm);
            view.Tick(Frame(t += 0.2, player, orderMenu: true));
            Check(c[0].ReadyShown, "strip: ShowReadyCount on - the ready count did not come back");

            // 7. the master switch: the strip goes (the ready count with it) and comes back with it
            S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
            view.Tick(Frame(t += 0.2, player, orderMenu: true));
            Check(!layer.Up && view.LastDecision == HudHide.ModOff, "strip: ModEnabled off - the strip stayed");
            S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
            view.Tick(Frame(t += 0.2, player, orderMenu: true));
            vm = view.CurrentViewModel!;
            Check(layer.Up && vm.Cells[0].ReadyShown && vm.Cells[0].ReadyText == "ready 15/40", "strip: ModEnabled on - the ready count did not come back");

            // 8. the [summary] line
            view.Finish(t += 0.5);
            WriteSummaryThrough(logic, view);
            LogHas("[summary] hud: orders strip - ready count (men not bracing, step 24): shown ");
            LogHas("), the fewest ready 3/12 (25%, 3 Cavalry); hidden ");
            LogHas("(BraceEnabled off - nobody braces 4, ShowReadyCount off 4)");
            Check(view.ReadyStats.ShownIn(ReadyBand.Red) > 0 && view.ReadyStats.ShownIn(ReadyBand.Yellow) > 0 && view.ReadyStats.ShownIn(ReadyBand.Plain) > 0,
                "strip: the summary's bands were not all counted");
            SetStatic(typeof(AthleticsLogic), "_current", null);
        }

        // ------------------------------------------------------------------ the ALT labels

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ReadyCountUnderTheMarkers()
        {
            AltMarkerDefaults();
            ReadyDefaults();
            var logic = StripLogic();
            var player = FakeAgent(44);
            var live = new FakeMarkers { Shown = true };
            live.Add(0, 0, 0, 960, 400, 40, 0.72, 0.08, 0.90, 0.81, bracing: 4);    // yours: 36/40 plain
            live.Add(0, 0, 1, 600, 500, 20, 0.45, 0.20, 0.60, 0.60, bracing: 8);    // yours: 12/20 yellow
            live.Add(1, 2, 0, 1300, 350, 80, 0.30, 0.10, 0.40, 0.90, bracing: 60);  // the enemy: 20/80 red
            var projection = new FakeMarkers { State = MarkerRead.Projected, Name = "stand-in projection" };
            var view = new AltMarkerView();
            var layer = new FakeHudLayer();
            view.UseLayer(layer);
            view.UseSources(live, projection);
            double t = 1;

            // 1. shown: each label's ready count under its bar, the enemy's too
            view.Tick(Frame(t, player));
            var vm = view.CurrentViewModel!;
            var l = vm.Labels;
            Check(layer.Up && l.Count == 3, "ALT: 3 labels expected, got " + l.Count);
            Check(l[0].ReadyShown && l[0].ReadyText == "ready 36/40" && l[0].ReadyColor == ReadyColors.Plain && l[1].ReadyText == "ready 12/20" && l[1].ReadyColor == ReadyColors.Yellow
                  && l[2].ReadyText == "ready 20/80" && l[2].ReadyColor == ReadyColors.Red, "ALT: the ready counts / colours: '" + l[0].ReadyText + "' '" + l[1].ReadyText + "' '" + l[2].ReadyText + "'");
            view.Tick(Frame(t += 0.2, player));
            LogHas("[hud] ALT markers: values at ");
            LogHas(": yours 1 Infantry 72% ± 8 HP 81% ready 36/40 (40 men, f 0.90) | yours 2 Archers 45% ± 20 HP 60% ready 12/20 (20 men, f 0.60)"
                   + " | enemy 1 Infantry 30% ± 10 HP 90% ready 20/80 (80 men, f 0.40) - ± is 1.00 std, health on, enemy on, ready count on: yellow at or below 75%, red at or below 50% of the men ready");

            // 2. the thresholds live - at the next frame (a settings change re-pushes every label)
            S.Set(SettingsSchema.ReadyYellowBelowPercent, 95, SettingSources.Mcm);
            view.Tick(Frame(t += 0.05, player));
            Check(l[0].ReadyColor == ReadyColors.Yellow, "ALT: ReadyYellowBelowPercent 95 did not turn 90% ready yellow");
            S.Set(SettingsSchema.ReadyYellowBelowPercent, 75, SettingSources.Mcm);
            view.Tick(Frame(t += 0.05, player));
            Check(l[0].ReadyColor == ReadyColors.Plain, "ALT: back to 75 - not plain again");

            // 3. BraceEnabled off / on, ShowReadyCount off / on - at the next frame
            S.Set(SettingsSchema.BraceEnabled, false, SettingSources.Mcm);
            view.Tick(Frame(t += 0.05, player));
            Check(!l[0].ReadyShown && !l[2].ReadyShown && l[0].IsShown, "ALT: BraceEnabled off - the ready count stayed (or the label went)");
            S.Set(SettingsSchema.BraceEnabled, true, SettingSources.Mcm);
            view.Tick(Frame(t += 0.05, player));
            Check(l[0].ReadyShown && l[2].ReadyShown, "ALT: BraceEnabled on - the ready count did not come back");
            S.Set(SettingsSchema.ShowReadyCount, false, SettingSources.Mcm);
            view.Tick(Frame(t += 0.05, player));
            Check(!l[1].ReadyShown, "ALT: ShowReadyCount off - the ready count stayed");
            S.Set(SettingsSchema.ShowReadyCount, true, SettingSources.Mcm);
            view.Tick(Frame(t += 0.05, player));
            Check(l[1].ReadyShown, "ALT: ShowReadyCount on - the ready count did not come back");

            // 4. new stats on the version: the enemy recovers
            live.Markers[2] = WithBracing(live.Markers[2], 10); // 70/80 = 88%: plain
            BumpFormationStats(logic);
            view.Tick(Frame(t += 0.05, player));
            Check(l[2].ReadyText == "ready 70/80" && l[2].ReadyColor == ReadyColors.Plain, "ALT: new stats not pushed: " + l[2].ReadyText);

            // 5. the master switch: the labels go and come back with the ready count
            S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
            view.Tick(Frame(t += 0.2, player));
            Check(!layer.Up && view.LastDecision == HudHide.ModOff, "ALT: ModEnabled off - the labels stayed");
            S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
            view.Tick(Frame(t += 0.2, player));
            vm = view.CurrentViewModel!;
            Check(layer.Up && vm.Labels.Count == 3 && vm.Labels[0].ReadyShown && vm.Labels[0].ReadyText == "ready 36/40", "ALT: ModEnabled on - the ready count did not come back");

            // 6. the [summary] line
            view.Finish(t += 0.5);
            WriteSummaryThrough(logic, view);
            LogHas("[summary] hud: ALT markers - ready count (men not bracing, step 24): shown ");
            LogHas("), the fewest ready 20/80 (25%, enemy 1 Infantry); hidden ");
            LogHas("BraceEnabled off - nobody braces 3, ShowReadyCount off 3)");
            SetStatic(typeof(AthleticsLogic), "_current", null);
        }

        private static AltMarker WithBracing(AltMarker m, int bracing)
        {
            var st = m.Stats;
            m.Stats = new FormationAthleticsStats(st.Count, st.MeanPoints, st.StdPoints, st.MeanFraction, st.StdFraction, st.Exhausted, st.MeanPeakShare, st.InPeak, st.MeanHealth, bracing);
            return m;
        }
    }
}

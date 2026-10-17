using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Hud;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Step 46 (Anton, 2026-10-17: "how tired they are in a bar above that fever bar ... a green bar that gets to red"): the ATHLETICS BAR above
    /// every man under ALT, drawn by step 44's REAL <see cref="FeverMarkView"/> over the REAL logic (its men's Athletics and fevers through the
    /// new allocation-free walk) with step 44's stand-in camera (<see cref="FakeFeverMarks"/>): a calm man = his Athletics bar alone (the fever
    /// bar's place kept), a man in a fever = both; the colours by band (green / blue / yellow / orange / red), the wound cap; a rider higher; you
    /// none; the enemy switch; the cap = the nearest on the screen (an off-screen man nearer takes no slot); vanilla's flag; the switches; the
    /// master switch first; the log and the summary; the fail safe; the cost - 1000 men all on the screen, the cap 200.
    /// What it cannot check: the look over the men's heads in the game - PLAYTEST D15.
    /// </summary>
    internal static partial class Program
    {
        private static void AthleticsMarkDefaults()
        {
            FeverMarkDefaults();                                                               // step 44's (it pins the bars off for its own checks) ...
            S.Set(SettingsSchema.ShowAthleticsMarks, true, SettingSources.File);              // ... then step 46's on
            S.Set(SettingsSchema.AthleticsMarksShowEnemy, true, SettingSources.File);
            S.Set(SettingsSchema.AthleticsMarkWidth, 22, SettingSources.File);
            S.Set(SettingsSchema.AthleticsMarkHeight, 5, SettingSources.File);
            S.Set(SettingsSchema.AthleticsMarkGap, 2, SettingSources.File);
            S.Set(SettingsSchema.BarYellowBelowPercent, 75, SettingSources.File);
            S.Set(SettingsSchema.BarOrangeBelowPercent, 50, SettingSources.File);
            S.Set(SettingsSchema.BarRedBelowPercent, 25, SettingSources.File);
        }

        /// <summary>The VM mark drawn for man <paramref name="key"/> this frame (null = none): mark k = the k-th picked.</summary>
        private static FeverMarkVM? MarkOf(FeverMarkView view, int key)
        {
            var vm = view.CurrentViewModel;
            var fr = view.LastFrame;
            if (vm == null) return null;
            for (int k = 0; k < view.DrawnNow && k < fr.Picked; k++)
                if (fr.Marks[fr.Order[k]].Key == key) return vm.Marks[k];
            return null;
        }

        private static Color BandColorOf(BarBand band, float fade = 1f)
        {
            AthleticsMarkMath.BandColor(band, out float r, out float g, out float b);
            return new Color(r, g, b, fade);
        }

        // ------------------------------------------------------------------ through the view

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AthleticsMarksThroughTheView()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                SprintDefaults();
                FeverDefaults();
                AthleticsMarkDefaults();
                S.Set(SettingsSchema.SprintAiEnabled, false, SettingSources.File);
                S.Set(SettingsSchema.FeverSprintToClose, false, SettingSources.File);
                var body = new FakeSprintBody();
                var logic = NewFeverLogic(body, new FakeWeaponFacts(), 0.99);
                var src = new FakeFeverMarks();
                double t = 9500;

                var charger = FeverMan(logic, body, src, 200, 0, 0f, 20f);                       // a charge: green + his fever bar
                var yellow = FeverMan(logic, body, src, 201, 2, -4f, 15f);                        // holding, half a bar: yellow
                var orange = FeverMan(logic, body, src, 202, 2, 4f, 16f);                         // 30%: orange
                var red = FeverMan(logic, body, src, 203, 3, -6f, 30f, (int)MarkerTeam.Enemy);  // the enemy's, 10%: red
                var hurt = FeverMan(logic, body, src, 204, 2, 3f, 25f, (int)MarkerTeam.Ally);   // an ally at 60% health: blue, capped
                var me = FeverMan(logic, body, src, 205, 0, 0f, 3f);
                logic.SmokePlayer = me.Agent;
                FeverMan(logic, body, src, 206, 2, 0f, -10f);                                     // behind the camera
                FeverMan(logic, body, src, 207, 2, 0f, 1f);                                       // too close
                FeverMan(logic, body, src, 208, 2, 12f, 5f);                                      // off the screen's side, 13 m
                var rider = FeverMan(logic, body, src, 209, 2, 2f, 35f);                          // a rider: his bar 1 m higher
                src.Riders.Add(209);
                FeverMan(logic, body, src, 210, 2, 1f, 12f);
                src.NotPlaced.Add(210);
                var burner = FeverMan(logic, body, src, 211, 1, 6f, 12f);                         // charging, then in contact: burning 0.7
                Blows(yellow, 5, t);
                Blows(orange, 7, t);
                Blows(red, 9, t);
                SetHealth(hurt.Agent, 60f, 100f);
                logic.CheckHealth(hurt, Rules, t);
                for (int s = 0; s < 4; s++) body.Orders[s] = BraceOrderKind.Stop;
                logic.TickSprint(0.1f, t, Rules);
                body.Orders[0] = BraceOrderKind.Charge;
                body.Orders[1] = BraceOrderKind.Charge;
                logic.TickSprint(0.1f, t += 0.25, Rules);
                body.EnemyAt[211] = 2f;
                t = Looks(logic, t, t + 0.25);
                t = Looks(logic, t, t + 3);
                Check(charger.Fever.Phase == FeverPhase.Armed && burner.Fever.Phase == FeverPhase.Burning && Math.Abs(burner.Fever.Share - 0.7) < 1e-6
                      && !yellow.Fever.Running && Math.Abs(yellow.Fraction - 0.5) < 1e-6 && Math.Abs(orange.Fraction - 0.3) < 1e-6 && Math.Abs(red.Fraction - 0.1) < 1e-6
                      && Math.Abs(hurt.Health - 0.6) < 1e-6,
                    "precondition: the men are not as set up (fever " + charger.Fever.Phase + " / " + burner.Fever.Share + ", fractions " + yellow.Fraction + " " + orange.Fraction + " " + red.Fraction + ", health " + hurt.Health + ")");

                var view = new FeverMarkView();
                var layer = new FakeHudLayer();
                view.UseLayer(layer);
                view.UseSource(src);
                int logFrom = LogText.Length;
                var player = me.Agent;
                view.Tick(Frame(t, player));
                Check(!view.IsLayerUp, "the bars are up with vanilla's markers hidden");

                // 1. vanilla shows its markers: every man but you has his Athletics bar; the two in a fever have their fever bar under it
                src.Shown = true;
                view.Tick(Frame(t += 0.05, player));
                var fr = view.LastFrame;
                Check(layer.Up && view.DrawnNow == 7 && view.BarsNow == 7 && view.FeverBarsNow == 2,
                    "shown: 7 men with 7 Athletics bars and 2 fever bars expected, got " + view.DrawnNow + " / " + view.BarsNow + " / " + view.FeverBarsNow);
                Check(fr.Skipped[(int)FeverMarkSkip.Behind] == 1 && fr.Skipped[(int)FeverMarkSkip.TooClose] == 1 && fr.Skipped[(int)FeverMarkSkip.OffScreen] == 1
                      && fr.Skipped[(int)FeverMarkSkip.NotPlaced] == 1 && fr.Skipped[(int)FeverMarkSkip.Capped] == 0 && MarkOf(view, 205) == null,
                    "the skips: " + FeverMarkMath.DescribeSkips(fr.Skipped) + (MarkOf(view, 205) != null ? " - and YOU got a bar" : string.Empty));
                var c = MarkOf(view, 200)!;
                Check(c.BarShown && c.FeverShown && c.BarFill == 1f && c.Fill == 1f && SameColor(c.BarColor, BandColorOf(BarBand.Green))
                      && SameColor(c.FillColor, FeverColors.Of(true, 1f, 1f)) && c.BoxWidth == 22f && c.BoxHeight == 12f && c.PositionX == 949f && c.PositionY == 528f
                      && c.BarWidth == 22f && c.BarHeight == 5f && c.Width == 22f && c.Height == 5f,
                    "the charging man: not a green bar over an orange fever bar in a 22 x 12 box at (949, 528) - box " + c.BoxWidth + " x " + c.BoxHeight + " at (" + c.PositionX + ", " + c.PositionY + ")");
                var y = MarkOf(view, 201)!;
                Check(y.BarShown && !y.FeverShown && Math.Abs(y.BarFill - 0.5f) < 1e-5f && SameColor(y.BarColor, BandColorOf(BarBand.Yellow)) && y.BoxHeight == 12f
                      && y.BarUsable == 1f,
                    "a calm half-bar man: not a yellow 0.5 bar over the kept fever place - fill " + y.BarFill + ", box height " + y.BoxHeight);
                var o = MarkOf(view, 202)!;
                Check(Math.Abs(o.BarFill - 0.3f) < 1e-5f && SameColor(o.BarColor, BandColorOf(BarBand.Orange)), "30%: not orange - " + o.BarColor);
                var r = MarkOf(view, 203)!;
                Check(Math.Abs(r.BarFill - 0.1f) < 1e-5f && SameColor(r.BarColor, BandColorOf(BarBand.Red)) && !r.FeverShown, "the enemy at 10%: not red - " + r.BarColor);
                var h = MarkOf(view, 204)!;
                AthleticsMarkMath.ParseHex(AthleticsMarkMath.WoundHex, out float wr, out float wg, out float wb, out _);
                Check(Math.Abs(h.BarUsable - 0.6f) < 1e-5f && Math.Abs(h.BarFill - 0.6f) < 1e-5f && SameColor(h.BarColor, BandColorOf(BarBand.Blue))
                      && SameColor(h.BarWoundColor, new Color(wr, wg, wb, 1f)),
                    "the wounded ally: not blue to his 60% cap - usable " + h.BarUsable + ", fill " + h.BarFill);
                var ri = MarkOf(view, 209)!;
                float riderBottom = 540f - 1000f * 1f / 35f;                                       // his point 3.5 m up (2.5 + the 1 m lift), the camera at 2.5 m
                Check(ri.BarShown && Math.Abs(ri.PositionY + ri.BoxHeight - riderBottom) < 1e-3f, "the rider's bars are not 1 m higher: bottom " + (ri.PositionY + ri.BoxHeight) + ", expected " + riderBottom);
                var b = MarkOf(view, 211)!;
                Check(b.BarShown && b.FeverShown && Math.Abs(b.Fill - 0.7f) < 1e-5f && SameColor(b.BarColor, BandColorOf(BarBand.Green)), "the burning man: not a green bar over a 0.7 fever bar");
                LogHas("[hud] ALT bars: first bars drawn at " + TraxHudView.S1(t) + " s (show #1) - 11 men looked at (yours and allies 9, enemy 1), 2 in a fever (armed 1, burning 1); "
                       + "7 men marked: 7 Athletics bars (green 3, blue 1, yellow 1, orange 1, red 1; wounded 1), 2 fever bars; skipped: not on the field 1, behind the camera 1, closer than 1.5 m 1, off the screen 1; ");
                LogHas("; Athletics bars 22 x 5 UI px, 2 px above the fever bar (its place kept while he has none), the enemy's too; fever marks 22 x 5 UI px");
                LogHas("the first drawn: yours man 200 at (960, 540), 20.0 m, armed, Athletics 100% green | yours man 201 at (693, 540), 15.5 m, calm, Athletics 50% yellow | "
                       + "yours man 202 at (1210, 540), 16.5 m, calm, Athletics 30% orange; source: stand-in camera");

                // 2. his fever ends (a hold): his fever bar goes, his Athletics bar stays EXACTLY where it was (no jump)
                float before = c.PositionY;
                body.Orders[0] = BraceOrderKind.Stop;
                logic.TickSprint(0.1f, t += 0.25, Rules);
                view.Tick(Frame(t += 0.05, player));
                c = MarkOf(view, 200)!;
                Check(!charger.Fever.Running && c.BarShown && !c.FeverShown && c.PositionY == before && c.BoxHeight == 12f, "the fever ended: the Athletics bar moved or the fever bar stayed (" + c.PositionY + " vs " + before + ")");

                // 3. the enemy's switch: his Athletics bar goes at once; on again: back
                S.Set(SettingsSchema.AthleticsMarksShowEnemy, false, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                Check(MarkOf(view, 203) == null && view.LastFrame.Skipped[(int)FeverMarkSkip.EnemyOff] == 1 && view.DrawnNow == 6, "AthleticsMarksShowEnemy off: the enemy's bar stayed");
                S.Set(SettingsSchema.AthleticsMarksShowEnemy, true, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                Check(MarkOf(view, 203) != null, "AthleticsMarksShowEnemy on: the enemy's bar did not come back");

                // 4. the fever bars switched off: no fever place kept - the Athletics bars drop onto the point; on again: back up
                S.Set(SettingsSchema.ShowFeverMarks, false, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                y = MarkOf(view, 201)!;
                Check(layer.Up && view.FeverBarsNow == 0 && y.BoxHeight == 5f && y.PositionY == 535f, "ShowFeverMarks off: the bars did not drop onto the point (" + y.BoxHeight + " at " + y.PositionY + ")");
                S.Set(SettingsSchema.ShowFeverMarks, true, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                Check(MarkOf(view, 201)!.BoxHeight == 12f && view.FeverBarsNow == 1, "ShowFeverMarks on: the fever place did not come back");

                // 5. the Athletics bars switched off: step 44 exactly - the man in a fever alone, his fever bar alone
                S.Set(SettingsSchema.ShowAthleticsMarks, false, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                b = MarkOf(view, 211)!;
                Check(layer.Up && view.DrawnNow == 1 && view.BarsNow == 0 && b.FeverShown && !b.BarShown && b.BoxHeight == 5f, "ShowAthleticsMarks off: not the fever bars alone (" + view.DrawnNow + ")");
                S.Set(SettingsSchema.ShowAthleticsMarks, true, SettingSources.Mcm);

                // 6. the cap: FeverMarkMax 3 - the nearest three ON THE SCREEN (13.4, 15.5, 16.5 m); the man off the screen's side at 13 m takes no slot
                //    and only four men are projected (the rest wait, never projected)
                S.Set(SettingsSchema.FeverMarkMax, 3, SettingSources.Mcm);
                int projections = src.Projections;
                view.Tick(Frame(t += 0.05, player));
                fr = view.LastFrame;
                Check(view.DrawnNow == 3 && MarkOf(view, 211) != null && MarkOf(view, 201) != null && MarkOf(view, 202) != null && MarkOf(view, 208) == null
                      && fr.Skipped[(int)FeverMarkSkip.OffScreen] == 1 && fr.Skipped[(int)FeverMarkSkip.Capped] == 4 && src.Projections - projections == 4,
                    "the cap of 3: drawn " + view.DrawnNow + ", skipped " + FeverMarkMath.DescribeSkips(fr.Skipped) + ", projected " + (src.Projections - projections));
                S.Set(SettingsSchema.FeverMarkMax, 200, SettingSources.Mcm);

                // 7. the layout live: the gap and the bar's size
                S.Set(SettingsSchema.AthleticsMarkGap, 6, SettingSources.Mcm);
                S.Set(SettingsSchema.AthleticsMarkWidth, 40, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                y = MarkOf(view, 201)!;
                Check(y.BoxHeight == 16f && y.BoxWidth == 40f && y.BarWidth == 40f && y.Width == 22f, "the gap / length live: box " + y.BoxWidth + " x " + y.BoxHeight);
                S.Set(SettingsSchema.AthleticsMarkGap, 2, SettingSources.Mcm);
                S.Set(SettingsSchema.AthleticsMarkWidth, 22, SettingSources.Mcm);

                // 8. the bands live: yellow at or below 70% of the peak line → the half-bar man (f 0.67) stays yellow; at 60%: blue
                S.Set(SettingsSchema.BarYellowBelowPercent, 60, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                Check(SameColor(MarkOf(view, 201)!.BarColor, BandColorOf(BarBand.Blue)), "BarYellowBelowPercent live: the half-bar man did not turn blue");
                S.Set(SettingsSchema.BarYellowBelowPercent, 75, SettingSources.Mcm);

                // 9. the fade close by: vanilla's (5 m) on every colour of the bar
                src.Feet[201] = new Vec3(0f, 5f, 0f);
                view.Tick(Frame(t += 0.05, player));
                y = MarkOf(view, 201)!;
                float fade = (5f - 1.5f) / 6.5f;
                Check(Math.Abs(y.BarColor.Alpha - fade) < 1e-4f && Math.Abs(y.BarWoundColor.Alpha - fade) < 1e-4f && y.BarFrameColor.Alpha < fade, "5 m: the bar does not fade like vanilla's icon: " + y.BarColor.Alpha);
                src.Feet[201] = new Vec3(-4f, 15f, 0f);

                // 10. FeverEnabled off: the Athletics bars stay (said once); with the bars off too there is nothing to draw - the layer goes
                S.Set(SettingsSchema.FeverEnabled, false, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                Check(layer.Up && view.FeverBarsNow == 0 && view.BarsNow == 7, "FeverEnabled off: the Athletics bars went (" + view.BarsNow + ")");
                LogHas("[hud] ALT bars: FeverEnabled switched off at " + TraxHudView.S1(t) + " s - no fevers, the fever bars go (the Athletics bars stay)");
                S.Set(SettingsSchema.ShowAthleticsMarks, false, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                Check(!layer.Up && view.LastDecision == HudHide.ViewCondition, "nothing to draw: the layer stayed");
                S.Set(SettingsSchema.ShowAthleticsMarks, true, SettingSources.Mcm);
                S.Set(SettingsSchema.FeverEnabled, true, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                Check(layer.Up && view.BarsNow == 7, "back on: the bars did not come back");

                // 11. both switches off: ToggleOff; AthleticsEnabled off: gone (no Athletics, no bars); vanilla hides its markers: gone
                S.Set(SettingsSchema.ShowAthleticsMarks, false, SettingSources.Mcm);
                S.Set(SettingsSchema.ShowFeverMarks, false, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                Check(!layer.Up && view.LastDecision == HudHide.ToggleOff, "both switches off: the layer stayed (" + view.LastDecision + ")");
                LogHas("[hud] ALT bars: layer removed at " + TraxHudView.S1(t) + " s - ShowAthleticsMarks / ShowFeverMarks off");
                S.Set(SettingsSchema.ShowAthleticsMarks, true, SettingSources.Mcm);
                S.Set(SettingsSchema.ShowFeverMarks, true, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                S.Set(SettingsSchema.AthleticsEnabled, false, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                Check(!layer.Up && view.LastDecision == HudHide.AthleticsOff, "Athletics off: the bars are up");
                S.Set(SettingsSchema.AthleticsEnabled, true, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                src.Shown = false;
                view.Tick(Frame(t += 0.05, player));
                Check(!layer.Up && view.LastDecision == HudHide.ViewCondition, "vanilla's markers hidden: the bars stayed");
                src.Shown = true;
                view.Tick(Frame(t += 0.05, player));
                Check(layer.Up && view.BarsNow == 7, "shown again: no bars");

                // 12. the summary, through the logic as at a real mission end
                view.Finish(t += 0.5);
                ((List<TraxHudView>)typeof(AthleticsLogic).GetField("_hudViews", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(logic)!).Add(view);
                typeof(AthleticsLogic).GetMethod("WriteHudSummary", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(logic, null);
                LogHas("[summary] hud: ALT bars - shown ");
                LogHas("; Athletics bars a frame: mean ");
                LogHas(", max 7 (summed over frames: green ");
                LogHas("; fever bars a frame: mean ");
                LogHas("; most men looked at in a frame 11 (yours and allies 9, enemy 1), most in a fever 2; frames at the cap 1 (FeverMarkMax 3 at the first - the nearest drawn)");
                Check(Occurrences(LogText, "[hud] ALT bars: first bars drawn", logFrom) == 1, "the first bars were logged more than once");
                logic.SmokePlayer = null;
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
                FeverMarkDefaults();
                ConfigStore.Reload("after the Athletics bars smoke");
            }
        }

        /// <summary>From the master-switch step: the Athletics bars go with ModEnabled and come back.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AthleticsMarksFollowTheMasterSwitch(Agent player)
        {
            var keep = _logic;
            try
            {
                FeverDefaults();
                AthleticsMarkDefaults();
                var body = new FakeSprintBody();
                var logic = NewFeverLogic(body, new FakeWeaponFacts(), 0.99);
                var src = new FakeFeverMarks { Shown = true };
                FeverMan(logic, body, src, 140, 2, 0f, 20f);                                     // a calm man: his Athletics bar only
                var view = new FeverMarkView();
                var layer = new FakeHudLayer();
                view.UseLayer(layer);
                view.UseSource(src);
                view.Tick(Frame(941, player));
                Check(layer.Up && view.BarsNow == 1 && view.FeverBarsNow == 0, "master switch: precondition - the calm man's Athletics bar is not up");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                view.Tick(Frame(942, player));
                Check(!layer.Up && view.LastDecision == HudHide.ModOff, "mod off: the Athletics bar stayed");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                view.Tick(Frame(943, player));
                Check(layer.Up && view.BarsNow == 1, "mod back on: the Athletics bar did not come back");
                view.Finish(944);
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                FeverMarkDefaults();
                FeverOff();
            }
        }

        /// <summary>The fail safe mid-walk: a failing placement disables the view for the mission, one error, nothing thrown.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AthleticsMarksFailSafe()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                FeverDefaults();
                AthleticsMarkDefaults();
                var body = new FakeSprintBody();
                var logic = NewFeverLogic(body, new FakeWeaponFacts(), 0.99);
                var src = new FakeFeverMarks { Shown = true };
                FeverMan(logic, body, src, 150, 2, 0f, 20f);
                var player = FakeAgent(151);
                var view = new FeverMarkView();
                var layer = new FakeHudLayer();
                view.UseLayer(layer);
                view.UseSource(src);
                view.Tick(Frame(50, player));
                Check(layer.Up && view.BarsNow == 1, "precondition: the Athletics bar is not up");
                src.ThrowInLocate = true;
                view.Tick(Frame(51, player));
                view.Tick(Frame(52, player));
                Check(view.IsDisabled && !layer.Up && view.Stats.Errors == 1, "a failing placement did not disable the ALT bars: disabled " + view.IsDisabled + ", errors " + view.Stats.Errors);
                LogHas("[hud] ALT bars: an error in tick at 51.0 s (InvalidOperationException: smoke: the ALT-bar placement failed");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
                FeverMarkDefaults();
                FeverOff();
                ConfigStore.Reload("after the Athletics bars fail safe");
            }
        }

        /// <summary>The cost: 1000 men ALL on the screen in front of the camera (every one a candidate), 400 in a fever, the cap 200 - a frame stays
        /// small, projects about the cap's worth and allocates nothing once the pool has grown.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AthleticsMarksCostStaysFlat()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                SprintDefaults();
                FeverDefaults();
                AthleticsMarkDefaults();
                S.Set(SettingsSchema.SprintAiEnabled, false, SettingSources.File);
                var body = new FakeSprintBody();
                var logic = NewFeverLogic(body, new FakeWeaponFacts(), 0.99);
                var src = new FakeFeverMarks { Shown = true };
                const int men = 1000;
                double t0 = 17000;
                for (int i = 0; i < men; i++)
                {
                    int index = 7000 + i;
                    int slot = i % 10 < 4 ? i % 4 : 4 + i % 4;                                   // 40% charge, the rest hold
                    float x = i % 50 - 25f, y = 30f + (i / 50) * 3f;                             // a 50 x 60 m field 30-87 m ahead: all on the screen
                    var st = FeverMan(logic, body, src, index, slot, x, y, i % 2 == 0 ? (int)MarkerTeam.Yours : (int)MarkerTeam.Enemy);
                    Blows(st, i % 10, t0);                                                        // every band
                }
                for (int s = 0; s < 8; s++) body.Orders[s] = BraceOrderKind.Stop;
                logic.TickSprint(0.1f, t0, Rules);
                for (int s = 0; s < 4; s++) body.Orders[s] = BraceOrderKind.Charge;
                logic.TickSprint(0.1f, t0 + 0.25, Rules);
                Check(logic.FeversRunning == 400, "cost: not 400 men in a fever: " + logic.FeversRunning);
                var view = new FeverMarkView();
                var layer = new FakeHudLayer();
                view.UseLayer(layer);
                view.UseSource(src);
                double t = t0 + 1;
                for (int k = 0; k < 5; k++) view.Tick(Frame(t += 1 / 60.0, null));            // warm up: the pool grows to the most drawn at once
                Check(layer.Up && view.DrawnNow == 200 && view.LastFrame.Eligible == men && view.LastFrame.Skipped[(int)FeverMarkSkip.Capped] == men - 200,
                    "cost: precondition - not 1000 candidates, 200 marked: " + view.DrawnNow + " of " + view.LastFrame.Eligible);
                const int frames = 300;
                int projections = src.Projections;
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var sw = Stopwatch.StartNew();
                for (int k = 1; k <= frames; k++)
                {
                    src.CameraX = (k % 20) * 0.05f;                                                // the camera drifts: every mark moves every frame
                    view.Tick(Frame(t += 1 / 60.0, null));
                }
                sw.Stop();
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double msPerFrame = sw.Elapsed.TotalMilliseconds / frames;
                double projected = (src.Projections - projections) / (double)frames;
                Console.WriteLine("        step 46 cost: " + men + " men all on the screen (" + logic.FeversRunning + " in a fever), cap " + S.FeverMarkMax + " - "
                                  + msPerFrame.ToString("0.000", CultureInfo.InvariantCulture) + " ms a frame (the view's own stopwatch: "
                                  + (view.MarkStats.MeanCostMs * 1000).ToString("0", CultureInfo.InvariantCulture) + " µs mean), " + allocated + " bytes allocated over " + frames
                                  + " frames; " + view.DrawnNow + " men marked (" + view.BarsNow + " Athletics bars, " + view.FeverBarsNow + " fever bars), "
                                  + projected.ToString("0", CultureInfo.InvariantCulture) + " projections a frame");
                Check(view.DrawnNow == 200 && view.BarsNow == 200, "cost: the cap of 200 not filled with bars: " + view.DrawnNow);
                Check(projected <= 200.5, "cost: more than the cap's worth projected a frame: " + projected);
                Check(msPerFrame < 1.0, "cost: an ALT-bars frame took " + msPerFrame + " ms (budget 1 ms)");
                Check(allocated < 64 * 1024, "cost: the ALT bars allocate - " + allocated + " bytes over " + frames + " frames");
                view.Finish(t + 1);
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
                FeverMarkDefaults();
                ConfigStore.Reload("after the Athletics bars cost smoke");
            }
        }
    }
}

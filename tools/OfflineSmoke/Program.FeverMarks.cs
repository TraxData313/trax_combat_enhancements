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
    /// Step 44 (Anton, 2026-10-17): the FEVER MARK under ALT - the prefab against the game, and the REAL <see cref="FeverMarkView"/> over the
    /// REAL logic's fevers (step 43's arming and burn, read through the new allocation-free walk) with a stand-in engine side
    /// (<see cref="FakeFeverMarks"/>: vanilla's flag, a camera at the origin looking along +y, a pinhole projection): armed orange, burning
    /// towards yellow with the fill = the fever left, gone when it fades; calm men, the player, behind the camera, too close, off the screen,
    /// not placed; the enemy switch; the cap picks the nearest (and only they are projected); vanilla's fade and the smaller size far away;
    /// the UI scale; vanilla's flag and the copied rule; FeverEnabled off; the gates; the master switch; the log and the summary; the fail
    /// safe; the cost with 1000 men, 400 in a fever. What it cannot check: that the game's camera projects the marks over the men's heads -
    /// PLAYTEST D14 and the [hud] lines.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>The engine side, played by the smoke: a camera at (CameraX, 0, CameraZ) looking along +y, focal 1000 px × the scale.</summary>
        private sealed class FakeFeverMarks : IFeverMarkSource
        {
            public bool Readable = true;
            public bool Shown;
            public bool Throw;
            public bool Camera = true;
            public float Scale = 1f;
            public float CameraX;
            public float CameraZ = 2.5f;
            public readonly Dictionary<int, Vec3> Feet = new Dictionary<int, Vec3>();
            public readonly Dictionary<int, int> Sides = new Dictionary<int, int>();
            public readonly HashSet<int> NotPlaced = new HashSet<int>();
            public readonly HashSet<int> Riders = new HashSet<int>();                       // step 46: their bars go RiderLiftMetres higher
            public bool ThrowInLocate;                                                        // step 46: the fail safe mid-walk
            public int Projections;
            public int Shows;

            public bool TryReadShown(out bool shown)
            {
                if (Throw) throw new InvalidOperationException("smoke: the fever-mark read failed");
                shown = Shown;
                return Readable;
            }

            public void NewShow() => Shows++;

            public bool BeginFrame(FeverMarkFrame frame)
            {
                frame.ScreenWidth = 1920 * Scale;
                frame.ScreenHeight = 1080 * Scale;
                frame.Scale = Scale;
                return Camera;
            }

            public bool Locate(Agent agent, float heightMetres, ref FeverMark m)
            {
                if (ThrowInLocate) throw new InvalidOperationException("smoke: the ALT-bar placement failed");
                int i = agent.Index;
                if (NotPlaced.Contains(i) || !Feet.TryGetValue(i, out var p)) return false;
                m.Key = i;
                m.Side = Sides.TryGetValue(i, out int side) ? side : (int)MarkerTeam.Yours;
                m.Mounted = Riders.Contains(i);
                m.WorldX = p.x;
                m.WorldY = p.y;
                m.WorldZ = p.z + AthleticsMarkMath.MarkHeight(heightMetres, m.Mounted);
                float dx = m.WorldX - CameraX, dy = m.WorldY, dz = m.WorldZ - CameraZ;
                m.Distance = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                m.InFront = dy > 0f;
                m.Facing = AltNearMath.Facing(dx, dy, dz, 0f, 1f, 0f);                         // step 58: the pinhole looks along +y
                return true;
            }

            public bool Project(ref FeverMark m)
            {
                Projections++;
                if (!(m.WorldY > 0f)) return false;
                float focal = 1000f * Scale;
                m.ScreenX = 960f * Scale + focal * (m.WorldX - CameraX) / m.WorldY;
                m.ScreenY = 540f * Scale - focal * (m.WorldZ - CameraZ) / m.WorldY;
                return true;
            }

            public string Describe() => "stand-in camera (pinhole, focal 1000 px)";
        }

        private static void FeverMarkDefaults()
        {
            S.Set(SettingsSchema.ShowFeverMarks, true, SettingSources.File);
            S.Set(SettingsSchema.FeverMarksShowEnemy, true, SettingSources.File);
            S.Set(SettingsSchema.FeverMarkMax, 200, SettingSources.File);
            S.Set(SettingsSchema.FeverMarkWidth, 22, SettingSources.File);
            S.Set(SettingsSchema.FeverMarkHeight, 5, SettingSources.File);
            S.Set(SettingsSchema.FeverMarkHeightMetres, 2.5, SettingSources.File);
            S.Set(SettingsSchema.HudRefreshSeconds, 0.1, SettingSources.File);
            S.Set(SettingsSchema.ShowAthleticsMarks, false, SettingSources.File);             // step 44's checks run without step 46's bars
            S.Set(SettingsSchema.AltMarksEveryone, true, SettingSources.File);                // ... and in the every-man view (step 58's near view has its own step)
            S.Set(SettingsSchema.AltNearMarksMax, 5, SettingSources.File);
            S.Set(SettingsSchema.AltNearMarksMetres, 15.0, SettingSources.File);
            S.Set(SettingsSchema.ShowMoraleMarks, false, SettingSources.File);                 // step 67: the older checks see the stack without the morale bar
        }

        private static class FeverColors
        {
            public static Color Of(bool armed, float share, float fade)
            {
                FeverMarkMath.Tint(armed, share, out float r, out float g, out float b, out float a);
                return new Color(r, g, b, a * fade);
            }
        }

        private static bool SameColor(Color a, Color b) => Math.Abs(a.Red - b.Red) < 1e-4f && Math.Abs(a.Green - b.Green) < 1e-4f
                                                           && Math.Abs(a.Blue - b.Blue) < 1e-4f && Math.Abs(a.Alpha - b.Alpha) < 1e-4f;

        /// <summary>A man tracked with a 100-point bar (a 10 s fever) in formation slot <paramref name="slot"/>, his feet at (x, y, 0).</summary>
        private static TrackedAgent FeverMan(AthleticsLogic logic, FakeSprintBody body, FakeFeverMarks src, int index, int slot, float x, float y,
            int side = (int)MarkerTeam.Yours)
        {
            var st = logic.Track(FakeAgent(index))!;
            st.AthleticsSkill = 100;
            body.Slots[index] = slot;
            src.Feet[index] = new Vec3(x, y, 0f);
            src.Sides[index] = side;
            return st;
        }

        // ------------------------------------------------------------------ the prefab

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FeverMarksPrefabIsValid() => PrefabIsValid(FeverMarkView.Movie, typeof(FeverMarksVM), 12, 90); // step 46: + the Athletics bar

        // ------------------------------------------------------------------ the view over the real logic's fevers

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FeverMarksThroughTheView()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                SprintDefaults();
                FeverDefaults();
                FeverMarkDefaults();
                S.Set(SettingsSchema.SprintAiEnabled, false, SettingSources.File); // the order poll serves the fever alone
                S.Set(SettingsSchema.FeverSprintToClose, false, SettingSources.File);
                var body = new FakeSprintBody();
                var facts = new FakeWeaponFacts();
                var logic = NewFeverLogic(body, facts, 0.99);
                var src = new FakeFeverMarks();
                double t = 9000;

                var armed = FeverMan(logic, body, src, 100, 0, 0f, 20f);                         // 20 m ahead: the screen's centre
                var burner = FeverMan(logic, body, src, 101, 0, 5f, 10f);                         // 11.2 m: will meet an enemy
                var foe = FeverMan(logic, body, src, 102, 1, -5f, 30f, (int)MarkerTeam.Enemy);   // the enemy's charge, 30.4 m
                var calm = FeverMan(logic, body, src, 103, 2, 2f, 15f);                           // holding: no fever
                var me = FeverMan(logic, body, src, 104, 0, 0f, 3f);                              // you: never a fever
                logic.SmokePlayer = me.Agent;
                var behind = FeverMan(logic, body, src, 105, 0, 0f, -10f);
                var close = FeverMan(logic, body, src, 106, 0, 0f, 1f);
                var aside = FeverMan(logic, body, src, 107, 0, 12f, 5f);                          // off the screen's side, 13 m - nearer than most (step 46: never takes a slot)
                var gone = FeverMan(logic, body, src, 108, 0, 1f, 12f);
                src.NotPlaced.Add(108);
                for (int s = 0; s < 3; s++) body.Orders[s] = BraceOrderKind.Stop;
                logic.TickSprint(0.1f, t, Rules);                                                // the first look records the orders
                body.Orders[0] = BraceOrderKind.Charge;
                body.Orders[1] = BraceOrderKind.Charge;
                logic.TickSprint(0.1f, t += 0.25, Rules);
                Check(armed.Fever.Phase == FeverPhase.Armed && foe.Fever.Running && !calm.Fever.Running && !me.Fever.Running && logic.FeversRunning == 7,
                    "precondition: the charge did not arm 7 fevers (you and the calm man none): " + logic.FeversRunning);

                // the read API: armed = (true, 1); calm / you = false
                Check(AthleticsLogic.TryGetFever(armed.Agent, out bool a1, out double s1) && a1 && s1 == 1.0, "TryGetFever: an armed man did not read (armed, 1)");
                Check(!AthleticsLogic.TryGetFever(calm.Agent, out _, out _) && !AthleticsLogic.TryGetFever(me.Agent, out _, out _), "TryGetFever: a calm man or you read a fever");

                // the burner meets an enemy: 3 s of burn = 0.7 left
                body.EnemyAt[101] = 2f;
                t = Looks(logic, t, t + 0.25);
                t = Looks(logic, t, t + 3);
                Check(burner.Fever.Phase == FeverPhase.Burning && Math.Abs(burner.Fever.Share - 0.7) < 1e-6, "precondition: the burner is not at 0.7: " + burner.Fever.Share);
                Check(AthleticsLogic.TryGetFever(burner.Agent, out bool a2, out double s2) && !a2 && Math.Abs(s2 - 0.7) < 1e-6, "TryGetFever: the burner did not read (burning, 0.7)");

                var view = new FeverMarkView();
                var layer = new FakeHudLayer();
                view.UseLayer(layer);
                view.UseSource(src);
                int logFrom = LogText.Length;
                var player = me.Agent;

                // vanilla's markers hidden: nothing, said once
                view.Tick(Frame(t, player));
                Check(!view.IsLayerUp && layer.Created == 0 && view.LastDecision == HudHide.ViewCondition, "the marks are up with vanilla's markers hidden");
                LogHas("[hud] ALT bars: not shown at " + TraxHudView.S1(t) + " s - the game's markers are hidden (ALT not held, the orders menu closed)");

                // 1. vanilla shows its markers: three marks - yours armed, yours burning, the enemy's - and every skip counted
                src.Shown = true;
                view.Tick(Frame(t += 0.05, player));
                var vm = view.CurrentViewModel!;
                var marks = vm.Marks;
                var fr = view.LastFrame;
                Check(layer.Up && view.DrawnNow == 3 && vm.ShownCount == 3, "shown: 3 marks expected, got " + view.DrawnNow);
                Check(fr.Skipped[(int)FeverMarkSkip.Behind] == 1 && fr.Skipped[(int)FeverMarkSkip.TooClose] == 1 && fr.Skipped[(int)FeverMarkSkip.OffScreen] == 1
                      && fr.Skipped[(int)FeverMarkSkip.NotPlaced] == 1 && fr.Skipped[(int)FeverMarkSkip.Capped] == 0,
                    "the skips: " + FeverMarkMath.DescribeSkips(fr.Skipped));
                var m0 = marks[0];
                Check(m0.PositionX == 949f && m0.PositionY == 535f && m0.Width == 22f && m0.Height == 5f && m0.Fill == 1f
                      && SameColor(m0.FillColor, FeverColors.Of(true, 1f, 1f)) && SameColor(m0.FillColor, Color.ConvertStringToColor(FeverMarkMath.OrangeHex)),
                    "the armed man's mark: at (" + m0.PositionX + ", " + m0.PositionY + ") " + m0.Width + " x " + m0.Height + ", fill " + m0.Fill);
                var m1 = marks[1];
                Check(Math.Abs(m1.Fill - 0.7f) < 1e-5f && SameColor(m1.FillColor, FeverColors.Of(false, 0.7f, 1f)) && m1.FillColor.Green > m0.FillColor.Green
                      && m1.FillColor.Alpha < 0.84f && Math.Abs(m1.PositionX - (960f + 500f - 11f)) < 1e-3f,
                    "the burning man's mark: fill " + m1.Fill + ", colour " + m1.FillColor + " at x " + m1.PositionX);
                Check(Math.Abs(m1.FrameColor.Alpha - FeverMarkMath.FrameAlpha * m1.FillColor.Alpha) < 1e-4f, "the frame does not fade with the fill");
                var m2 = marks[2];
                float far = FeverMarkMath.SizeFactor((float)Math.Sqrt(5 * 5 + 30 * 30));          // 30.4 m: a little smaller (0.9)
                Check(m2.Fill == 1f && far < 0.91f && Math.Abs(m2.Width - 22f * far) < 1e-3f && Math.Abs(m2.PositionX - (960f - 1000f * 5f / 30f - 11f * far)) < 1e-3f,
                    "the enemy's mark misplaced: " + m2.PositionX + " w " + m2.Width);
                LogHas("[hud] ALT bars: layer created at ");
                LogHas("[hud] ALT bars: first bars drawn at " + TraxHudView.S1(t) + " s (show #1) - 7 men looked at (yours and allies 5, enemy 1), 7 in a fever (armed 6, burning 1); "
                       + "3 men marked: 0 Athletics bars (green 0, blue 0, yellow 0, orange 0, red 0; wounded 0), 3 fever bars; "
                       + "skipped: not on the field 1, behind the camera 1, closer than 1.5 m 1, off the screen 1; screen 1920 x 1080 px, UI scale 1.00; "
                       + "Athletics bars off (ShowAthleticsMarks off); "
                       + "fever marks 22 x 5 UI px (smaller far away; the game's UI scale applies), 2.5 m above his feet, at most 200 a frame (FeverMarkMax - the nearest to the camera), the enemy's too; "
                       + "the first drawn: yours man 100 at (960, 540), 20.0 m, armed | yours man 101 at (1460, 540), 11.2 m, burning 70% left | enemy man 102 at (793, 540), 30.4 m, armed; source: stand-in camera");

                // 2. the player down: still shown (vanilla's markers show without him)
                view.Tick(Frame(t += 0.05, null));
                Check(layer.Up && view.DrawnNow == 3, "the marks went with the player");

                // 3. FeverMarksShowEnemy off: the enemy's mark goes at once; on: back
                S.Set(SettingsSchema.FeverMarksShowEnemy, false, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                Check(view.DrawnNow == 2 && view.LastFrame.Skipped[(int)FeverMarkSkip.EnemyOff] == 1 && !marks[2].IsShown, "FeverMarksShowEnemy off: the enemy's mark stayed");
                S.Set(SettingsSchema.FeverMarksShowEnemy, true, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                Check(view.DrawnNow == 3, "FeverMarksShowEnemy on: the enemy's mark did not come back");

                // 4. the cap: FeverMarkMax 2 - the nearest two ON THE SCREEN (11.2 m, 20 m); the man off its side (13 m - nearer than the 20 m
                //    man) is projected and dropped, so he takes no slot; the enemy at 30 m waits and (step 46) is never projected
                S.Set(SettingsSchema.FeverMarkMax, 2, SettingSources.Mcm);
                int projections = src.Projections;
                view.Tick(Frame(t += 0.05, player));
                fr = view.LastFrame;
                Check(view.DrawnNow == 2 && fr.Skipped[(int)FeverMarkSkip.Capped] == 1 && fr.Skipped[(int)FeverMarkSkip.OffScreen] == 1 && src.Projections - projections == 3
                      && Math.Abs(marks[0].PositionX - 1449f) < 1e-3f && marks[1].PositionX == 949f && !marks[2].IsShown,
                    "the cap of 2: drawn " + view.DrawnNow + ", capped " + fr.Skipped[(int)FeverMarkSkip.Capped] + ", projected " + (src.Projections - projections));
                S.Set(SettingsSchema.FeverMarkMax, 200, SettingSources.Mcm);

                // 5. distance: vanilla's fade close by, the smaller size far away; the height setting live
                src.Feet[100] = new Vec3(0f, 5f, 0f);
                view.Tick(Frame(t += 0.05, player));
                Check(Math.Abs(marks[0].FillColor.Alpha - (5f - 1.5f) / 6.5f) < 1e-4f && marks[0].Width == 22f, "5 m: the fade is not vanilla's (5 - 1.5) / 6.5: " + marks[0].FillColor.Alpha);
                src.Feet[100] = new Vec3(0f, 60f, 0f);
                view.Tick(Frame(t += 0.05, player));
                Check(Math.Abs(marks[0].Width - 13.2f) < 1e-3f && Math.Abs(marks[0].Height - 3f) < 1e-3f && marks[0].FillColor.Alpha == 1f, "60 m: not 60% of the size: " + marks[0].Width + " x " + marks[0].Height);
                S.Set(SettingsSchema.FeverMarkHeightMetres, 3.5, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                Check(Math.Abs(marks[0].PositionY - (540f - 1000f * 1f / 60f - 3f)) < 1e-3f, "the height setting did not lift the mark: " + marks[0].PositionY);
                S.Set(SettingsSchema.FeverMarkHeightMetres, 2.5, SettingSources.Mcm);
                S.Set(SettingsSchema.FeverMarkWidth, 30, SettingSources.Mcm);
                src.Feet[100] = new Vec3(0f, 20f, 0f);
                view.Tick(Frame(t += 0.05, player));
                Check(marks[0].Width == 30f && marks[0].PositionX == 945f, "FeverMarkWidth live: " + marks[0].Width);
                S.Set(SettingsSchema.FeverMarkWidth, 22, SettingSources.Mcm);

                // 6. another UI scale (2560 x 1440): the marks and their places follow
                src.Scale = 4 / 3f;
                view.Tick(Frame(t += 0.05, player));
                Check(Math.Abs(marks[0].Width - 22f * 4 / 3f) < 1e-3f && Math.Abs(marks[0].PositionX - (1280f - 11f * 4 / 3f)) < 1e-3f
                      && Math.Abs(marks[0].PositionY - (720f - 5f * 4 / 3f)) < 1e-3f, "UI scale 4/3: " + marks[0].Width + " at " + marks[0].PositionX + ", " + marks[0].PositionY);
                src.Scale = 1f;

                // 7. the burn runs out: the mark fades through yellow, then goes; the others stay
                t = Looks(logic, t, t + 5.5);                                                       // 1.5 s left of 10
                view.Tick(Frame(t += 0.05, player));
                var late = marks[1];
                Check(late.IsShown && late.Fill < 0.2f && late.FillColor.Green > 0.8f && late.FillColor.Alpha < 0.6f,
                    "nearly burned out: not a faint yellow short bar - fill " + late.Fill + ", colour " + late.FillColor);
                int guard = 0;
                while (burner.Fever.Running && guard++ < 40) t = Looks(logic, t, t + 0.25);
                view.Tick(Frame(t += 0.05, player));
                Check(!burner.Fever.Running && view.DrawnNow == 2 && !marks[2].IsShown, "faded: the burner's mark did not go (drawn " + view.DrawnNow + ")");

                // 8. vanilla hides its markers: removed QUIETLY; shown again: a fresh show
                int text = LogText.Length;
                src.Shown = false;
                view.Tick(Frame(t += 0.05, player));
                Check(!layer.Up, "the marks stayed with vanilla's markers hidden");
                src.Shown = true;
                view.Tick(Frame(t += 0.05, player));
                Check(layer.Up && view.CurrentViewModel!.ShownCount == 2 && src.Shows == 2, "shown again: no fresh marks");
                marks = view.CurrentViewModel!.Marks;
                string again = LogText.Substring(text);
                Check(!again.Contains("fever marks: layer created") && !again.Contains("fever marks: layer removed") && !again.Contains("first marks"),
                    "hiding and showing again was logged outside the verbose log: " + again);

                // 9. vanilla's flag unreadable: its rule copied - ALT held or the orders menu open
                src.Readable = false;
                view.Tick(Frame(t += 0.05, player));
                Check(!layer.Up, "the copied rule: shown without ALT or the orders menu");
                view.Tick(Frame(t += 0.05, player, alt: true));
                Check(layer.Up, "the copied rule: ALT held did not show the marks");
                view.Tick(Frame(t += 0.05, player, orderMenu: true));
                Check(layer.Up, "the copied rule: the orders menu open did not keep them");
                src.Readable = true;
                view.Tick(Frame(t += 0.05, player));

                // 10. no camera this frame: no marks, nothing thrown
                src.Camera = false;
                view.Tick(Frame(t += 0.05, player));
                Check(layer.Up && view.DrawnNow == 0 && view.CurrentViewModel!.ShownCount == 0, "no camera: marks drawn");
                src.Camera = true;

                // 11. FeverEnabled off: no fevers, no marks - the layer goes, said once; on again: back
                S.Set(SettingsSchema.FeverEnabled, false, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                Check(!layer.Up && view.LastDecision == HudHide.ViewCondition, "FeverEnabled off: the marks stayed");
                LogHas("[hud] ALT bars: FeverEnabled switched off at " + TraxHudView.S1(t) + " s - no fevers, the fever bars go");
                Check(!AthleticsLogic.TryGetFever(armed.Agent, out _, out _), "TryGetFever read a fever with FeverEnabled off");
                S.Set(SettingsSchema.FeverEnabled, true, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                Check(layer.Up && view.DrawnNow == 2, "FeverEnabled on again: the marks did not come back");

                // 12. a hold order ends the fevers: the marks go (calm men get none)
                body.Orders[0] = BraceOrderKind.Stop;
                body.Orders[1] = BraceOrderKind.Stop;
                logic.TickSprint(0.1f, t += 0.25, Rules);
                view.Tick(Frame(t += 0.05, player));
                Check(logic.FeversRunning == 0 && view.DrawnNow == 0 && view.CurrentViewModel!.ShownCount == 0 && AthleticsLogic.FeverWalkCount == 0,
                    "a hold: fevers or marks left (" + logic.FeversRunning + " / " + view.DrawnNow + ")");

                // 13. the usual gates remove the layer with their reason
                S.Set(SettingsSchema.ShowFeverMarks, false, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                Check(!layer.Up && view.Stats.RemovedBy(HudHide.ToggleOff) == 1, "ShowFeverMarks off: the marks stayed");
                S.Set(SettingsSchema.ShowFeverMarks, true, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                view.Tick(Frame(t += 0.05, player, hideUi: true));
                Check(!layer.Up && view.Stats.RemovedBy(HudHide.HideBattleUI) == 1, "Hide battle UI: the marks stayed");
                view.Tick(Frame(t += 0.05, player, photo: true));
                Check(!layer.Up && view.LastDecision == HudHide.PhotoMode, "photo mode: the marks are up");
                view.Tick(Frame(t += 0.05, player, MissionMode.Deployment));
                Check(!layer.Up, "deployment: the marks are up");
                S.Set(SettingsSchema.AthleticsEnabled, false, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                Check(!layer.Up && view.LastDecision == HudHide.AthleticsOff, "Athletics off: the marks are up");
                S.Set(SettingsSchema.AthleticsEnabled, true, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));

                // 14. mission end and the summary, through the logic as at a real mission end
                view.Finish(t += 0.5);
                Check(!layer.Up, "mission end: the marks stayed");
                ((List<TraxHudView>)typeof(AthleticsLogic).GetField("_hudViews", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(logic)!).Add(view);
                typeof(AthleticsLogic).GetMethod("WriteHudSummary", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(logic, null);
                LogHas("[summary] hud: ALT bars (movie TraxFeverMarks) - on screen ");
                LogHas("[summary] hud: ALT bars - shown 6x, on screen ");
                LogHas("; men marked a frame: mean ");
                LogHas(", max 3; Athletics bars a frame: mean 0.0, max 0; fever bars a frame: mean ");
                LogHas(", max 3; most men looked at in a frame 7 (yours and allies 5, enemy 1), most in a fever 7; frames at the cap 1 (FeverMarkMax 2 at the first - the nearest drawn); men skipped, summed over frames: not on the field ");
                LogHas(", beyond the cap 1; cost ");
                Check(Occurrences(LogText, "[hud] ALT bars: first bars drawn", logFrom) == 1, "the first bars were logged more than once");
                logic.SmokePlayer = null;
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
                FeverMarkDefaults();
                ConfigStore.Reload("after the fever marks smoke"); // the file's values back (the MCM steps build on them)
            }
        }

        /// <summary>From the master-switch step: the marks go with ModEnabled and come back (and the read API answers nothing meanwhile).</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FeverMarksFollowTheMasterSwitch(Agent player)
        {
            var keep = _logic;
            try
            {
                FeverDefaults();                                                                  // the order poll runs for the fever alone (the sprint stays as the step left it)
                FeverMarkDefaults();
                var body = new FakeSprintBody();
                var logic = NewFeverLogic(body, new FakeWeaponFacts(), 0.99);
                var src = new FakeFeverMarks { Shown = true };
                var man = FeverMan(logic, body, src, 130, 0, 0f, 20f);
                body.Orders[0] = BraceOrderKind.Stop;
                logic.TickSprint(0.1f, 930, Rules);
                body.Orders[0] = BraceOrderKind.Charge;
                logic.TickSprint(0.1f, 930.25, Rules);
                var view = new FeverMarkView();
                var layer = new FakeHudLayer();
                view.UseLayer(layer);
                view.UseSource(src);
                view.Tick(Frame(931, player));
                Check(layer.Up && view.DrawnNow == 1, "master switch: precondition - the fever mark is not up");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                view.Tick(Frame(932, player));
                Check(!layer.Up && view.LastDecision == HudHide.ModOff && !AthleticsLogic.TryGetFever(man.Agent, out _, out _), "mod off: the fever mark stayed (or the fever read)");
                LogHas("[hud] ALT bars: layer removed at 932.0 s - ModEnabled off (the master switch)");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                view.Tick(Frame(933, player));
                Check(layer.Up && layer.Created == 2 && view.DrawnNow == 1, "mod back on: the fever mark did not come back");
                view.Finish(934);
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                FeverOff();
            }
        }

        /// <summary>The fail safe: a failing read disables the view for the mission; a view without a source too.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FeverMarksFailSafe()
        {
            FeverMarkDefaults();
            S.Set(SettingsSchema.FeverEnabled, true, SettingSources.File);
            var player = FakeAgent(44);
            var src = new FakeFeverMarks { Shown = true };
            var view = new FeverMarkView();
            var layer = new FakeHudLayer();
            view.UseLayer(layer);
            view.UseSource(src);
            view.Tick(Frame(40, player));
            Check(layer.Up, "precondition: the fever marks are not up");
            src.Throw = true;
            int errors = TraxLog.ErrorCount;
            view.Tick(Frame(41, player));
            view.Tick(Frame(42, player));
            // (AthleticsLogic.Failed reports a site once per mission and the earlier fail-safe steps spent "hud.tick" - the view's own count
            // and the DISABLED line prove the error; never more than one)
            Check(view.IsDisabled && !layer.Up && view.Stats.Errors == 1 && TraxLog.ErrorCount - errors <= 1,
                "a failing read did not disable the fever marks (or logged twice): disabled " + view.IsDisabled + ", layer up " + layer.Up + ", view errors " + view.Stats.Errors);
            LogHas("[hud] ALT bars: an error in tick at 41.0 s (InvalidOperationException: smoke: the fever-mark read failed - stack in the [error] line) - DISABLED for the rest of this battle");
            Check(view.MarkStats.SummaryLine(view.Stats.Errors).EndsWith("; errors 1", StringComparison.Ordinal), "the summary does not count the error");

            var orphan = new FeverMarkView();
            orphan.UseLayer(new FakeHudLayer());
            orphan.Tick(Frame(43, player, alt: true));
            Check(orphan.IsDisabled, "a fever-mark view without a source did not disable itself");
            FeverOff();
        }

        /// <summary>The cost: 1000 men, 400 in a fever (half of them the enemy's, some behind the camera), the cap 200 - a frame stays small and
        /// allocates nothing once the pool has grown.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FeverMarksCostStaysFlat()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                SprintDefaults();
                FeverDefaults();
                FeverMarkDefaults();
                S.Set(SettingsSchema.SprintAiEnabled, false, SettingSources.File);
                var body = new FakeSprintBody();
                var logic = NewFeverLogic(body, new FakeWeaponFacts(), 0.99);
                var src = new FakeFeverMarks { Shown = true };
                const int men = 1000;
                for (int i = 0; i < men; i++)
                {
                    int index = 6000 + i;
                    int slot = i % 10 < 4 ? i % 4 : 4 + i % 4;                                   // 40% in slots 0-3 (they charge), the rest in 4-7 (they hold)
                    float x = i % 50 - 25f, y = (i / 50) * 3f - 6f;                              // a 50 x 60 m field, the first rows behind the camera
                    FeverMan(logic, body, src, index, slot, x, y, i % 2 == 0 ? (int)MarkerTeam.Yours : (int)MarkerTeam.Enemy);
                }
                for (int s = 0; s < 8; s++) body.Orders[s] = BraceOrderKind.Stop;
                logic.TickSprint(0.1f, 16000, Rules);
                for (int s = 0; s < 4; s++) body.Orders[s] = BraceOrderKind.Charge;
                logic.TickSprint(0.1f, 16000.25, Rules);
                Check(logic.FeversRunning == 400, "cost: not 400 men in a fever: " + logic.FeversRunning);
                var view = new FeverMarkView();
                var layer = new FakeHudLayer();
                view.UseLayer(layer);
                view.UseSource(src);
                double t = 16001;
                for (int k = 0; k < 5; k++) view.Tick(Frame(t += 1 / 60.0, null));          // warm up: the pool grows to the most drawn at once
                Check(layer.Up && view.DrawnNow == 200 && view.LastFrame.Skipped[(int)FeverMarkSkip.Capped] > 0, "cost: precondition - not 200 marks at the cap: " + view.DrawnNow);
                const int frames = 300;
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
                var fr = view.LastFrame;
                Console.WriteLine("        step 44 cost: " + men + " men, " + logic.FeversRunning + " in a fever, cap " + S.FeverMarkMax + " - "
                                  + msPerFrame.ToString("0.000", CultureInfo.InvariantCulture) + " ms a frame (the view's own stopwatch: "
                                  + (view.MarkStats.MeanCostMs * 1000).ToString("0", CultureInfo.InvariantCulture) + " µs mean), " + allocated + " bytes allocated over " + frames
                                  + " frames; " + view.DrawnNow + " marks drawn, skipped: " + FeverMarkMath.DescribeSkips(fr.Skipped));
                Check(view.DrawnNow == 200, "cost: the cap of 200 not filled: " + view.DrawnNow);
                Check(msPerFrame < 0.5, "cost: a fever-mark frame took " + msPerFrame + " ms (budget 0.5 ms)");
                Check(allocated < 64 * 1024, "cost: the fever marks allocate - " + allocated + " bytes over " + frames + " frames");
                view.Finish(t + 1);
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
                FeverMarkDefaults();
                ConfigStore.Reload("after the fever marks cost smoke"); // the file's values back (the MCM steps build on them)
            }
        }
    }
}

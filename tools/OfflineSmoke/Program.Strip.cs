using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Offline checks for the orders-menu strip (step 9): its prefab against the game (the item
    /// template against the item ViewModel), and the REAL <see cref="OrderStripView"/> driven by
    /// made-up frames with stand-in cards (<see cref="FakeCards"/> - the vanilla keyboard columns,
    /// the gamepad row, another UI scale, RTS Camera's single set) and stand-in formations: the
    /// alignment maths (cells on the cards' pixels, lifted at the screen's edge), the values, every
    /// fallback to the compact panel and its way back, the live switches, the quiet open/close, the
    /// log, the summary and the fail safe. What it cannot check: that the game's real widgets report
    /// these pixels and that the cells draw there - PLAYTEST "Orders-menu strip" and the [hud] lines.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>The vanilla keyboard layout at 1920 x 1080: slot k's card top-left (UI scale 1).</summary>
        private static readonly float[][] Columns =
        {
            new float[] { 20, 44 }, new float[] { 20, 307 }, new float[] { 20, 570 }, new float[] { 20, 833 },
            new float[] { 1769, 44 }, new float[] { 1769, 307 }, new float[] { 1769, 570 }, new float[] { 1769, 833 },
        };

        /// <summary>The engine side of the card reading, played by the smoke.</summary>
        private sealed class FakeCards : IOrderCardSource
        {
            public bool ScanOk = true;
            public string Detail = "stand-in layer MissionOrder: 16 cards";
            public bool ReadOk = true;
            public bool Throw;
            public int Scans;
            public float Scale = 1f;
            public float ScreenWidth = 1920;
            public float ScreenHeight = 1080;
            public readonly OrderCard[] Cards = new OrderCard[OrderCardFrame.MaxCards];
            public int Count;

            public bool Scan(out string detail)
            {
                Scans++;
                if (Throw) throw new InvalidOperationException("smoke: the card walk failed");
                detail = ScanOk ? Detail : "stand-in: no order layer";
                return ScanOk;
            }

            public bool Read(OrderCardFrame frame)
            {
                if (Throw) throw new InvalidOperationException("smoke: the card walk failed");
                if (!ReadOk) return false;
                Array.Copy(Cards, frame.Cards, Count);
                frame.Count = Count;
                frame.Scale = Scale;
                frame.ScreenWidth = ScreenWidth;
                frame.ScreenHeight = ScreenHeight;
                return true;
            }

            /// <summary><paramref name="sets"/> sets (vanilla 2, RTS Camera 1); set <paramref name="drawn"/>
            /// shows the cards of the formations with men at the keyboard columns (or a top row).</summary>
            public void Layout(FakeStripFormations f, int sets, int drawn, float scale = 1f, bool row = false)
            {
                Count = sets * 8;
                Scale = scale;
                ScreenWidth = 1920 * scale;
                ScreenHeight = 1080 * scale;
                int col = 0;
                for (int s = 0; s < sets; s++)
                    for (int k = 0; k < 8; k++)
                    {
                        bool show = s == drawn && f.Values[k].Members > 0;
                        float x = row ? 700 + 151 * col : Columns[k][0];
                        float y = row ? 60 : Columns[k][1];
                        if (show && row) col++;
                        Cards[s * 8 + k] = new OrderCard
                        {
                            Visible = show,
                            X = x * scale,
                            Y = y * scale,
                            Width = 131 * scale,
                            Height = 223 * scale,
                            Members = show ? f.Values[k].Members : 0,
                        };
                    }
            }
        }

        /// <summary>The player's formations, played by the smoke.</summary>
        private sealed class FakeStripFormations : IStripFormations
        {
            public readonly StripFormation[] Values = new StripFormation[8];

            public FakeStripFormations()
            {
                for (int k = 0; k < 8; k++) Values[k].Exists = true;
            }

            public void Set(int k, int men, double meanFraction, double stdFraction, double meanF, double health, int exhausted = 0, int bracing = 0)
            {
                Values[k].Members = men;
                Values[k].Stats = men > 0 ? new FormationAthleticsStats(men, meanFraction * 100, stdFraction * 100, meanFraction, stdFraction, exhausted, meanF, 0, health, bracing) : default;
            }

            public void Read(StripFormation[] into) => Array.Copy(Values, into, 8);
        }

        private static void StripDefaults()
        {
            S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.VerboseLogging, false, SettingSources.File);
            S.Set(SettingsSchema.AthleticsEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.AthleticsPeakPercent, 75, SettingSources.File);
            S.Set(SettingsSchema.ShowInOrderMenu, true, SettingSources.File);
            S.Set(SettingsSchema.ShowFormationHealth, true, SettingSources.File);
            S.Set(SettingsSchema.OrderStripUnderCards, true, SettingSources.File);
            S.Set(SettingsSchema.ShowReadyCount, false, SettingSources.File); // step 24 has its own step (Program.Ready.cs): the older checks see step 9-23's strip
            S.Set(SettingsSchema.ShowFormationSpread, true, SettingSources.File);
            S.Set(SettingsSchema.FormationSpreadStdDevs, 1.0, SettingSources.File);
            S.Set(SettingsSchema.OrderStripTextSize, 13, SettingSources.File);
            S.Set(SettingsSchema.OrderStripTextOffset, 1, SettingSources.File);
            S.Set(SettingsSchema.OrderStripBarOffset, 20, SettingSources.File);
            S.Set(SettingsSchema.OrderStripBarHeight, 4, SettingSources.File);
            S.Set(SettingsSchema.OrderStripSideMargin, 2, SettingSources.File);
            S.Set(SettingsSchema.OrderPanelOffsetTop, 80, SettingSources.File);
            S.Set(SettingsSchema.OrderPanelWidth, 300, SettingSources.File);
            S.Set(SettingsSchema.HudRefreshSeconds, 0.1, SettingSources.File);
            S.Set(SettingsSchema.BarYellowBelowPercent, 75, SettingSources.File);
            S.Set(SettingsSchema.BarOrangeBelowPercent, 50, SettingSources.File);
            S.Set(SettingsSchema.BarRedBelowPercent, 25, SettingSources.File);
        }

        /// <summary>Infantry 40 (72% ± 8, f 0.90, 81% health), Archers 20 (45% ± 20, f 0.60, 60%),
        /// Cavalry 12 (10% ± 5, f 0.13, 3 empty, 95%).</summary>
        private static FakeStripFormations StripArmy()
        {
            var f = new FakeStripFormations();
            f.Set(0, 40, 0.72, 0.08, 0.90, 0.81);
            f.Set(1, 20, 0.45, 0.20, 0.60, 0.60);
            f.Set(2, 12, 0.10, 0.05, 0.13, 0.95, 3);
            return f;
        }

        /// <summary>A running logic whose formation-stats version the smoke can move (the view re-pushes
        /// values when it moves).</summary>
        private static AthleticsLogic StripLogic()
        {
            var logic = new AthleticsLogic();
            SetStatic(typeof(AthleticsLogic), "_current", logic);
            return logic;
        }

        private static void BumpFormationStats(AthleticsLogic logic)
        {
            var field = typeof(AthleticsLogic).GetField("_formationVersion", BindingFlags.Instance | BindingFlags.NonPublic)!;
            field.SetValue(logic, (int)field.GetValue(logic)! + 1);
        }

        private static int Occurrences(string text, string part, int from)
        {
            int n = 0;
            for (int i = text.IndexOf(part, from, StringComparison.Ordinal); i >= 0; i = text.IndexOf(part, i + part.Length, StringComparison.Ordinal)) n++;
            return n;
        }

        // ------------------------------------------------------------------ the prefab

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void StripPrefabIsValid() => PrefabIsValid(OrderStripView.Movie, typeof(OrderStripVM), 30, 200);

        // ------------------------------------------------------------------ the view, driven

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void StripThroughTheView()
        {
            StripDefaults();
            var logic = StripLogic();
            var player = FakeAgent(40);
            var army = StripArmy();
            var cards = new FakeCards();
            cards.Layout(army, 2, 0);
            var view = new OrderStripView();
            var layer = new FakeHudLayer();
            view.UseLayer(layer);
            view.UseSources(cards, army);
            int logFrom = LogText.Length;
            double t = 1;

            // the menu closed: nothing, and the log says why - once
            view.Tick(Frame(t, player));
            Check(!view.IsLayerUp && layer.Created == 0 && view.LastDecision == HudHide.ViewCondition, "the strip is up with the orders menu closed");
            LogHas("[hud] orders strip: not shown at 1.0 s - the orders menu is closed");

            // 1. open: the layer, the cells on the cards' bottom edges, the values, the log
            t = 2;
            view.Tick(Frame(t, player, orderMenu: true));
            var vm = view.CurrentViewModel!;
            Check(layer.Up && view.IsLayerUp && view.UnderCards && vm.StripShown && !vm.PanelShown, "open: not under the cards");
            LogHas("[hud] orders strip: layer created at 2.0 s (mode Battle, was hidden: the orders menu is closed) - movie TraxOrderStrip loaded OK (stand-in, 11 widgets)"
                   + " - later builds and removals by \"the orders menu is closed\" go to the verbose log only");
            var c = vm.Cells;
            Check(c.Count == 8 && c[0].IsShown && c[1].IsShown && c[2].IsShown && !c[3].IsShown && !c[4].IsShown, "open: the wrong cells shown");
            Check(c[0].PositionX == 20f && c[0].PositionY == 268f && c[0].CellWidth == 131f && c[1].PositionY == 531f && c[2].PositionY == 794f,
                "open: the cells are not on the cards' bottom edges: " + c[0].PositionX + ", " + c[0].PositionY + " w " + c[0].CellWidth);
            Check(c[0].TextSize == 13 && c[0].TextMarginTop == 0f && c[0].BarMarginTop == 19f && c[0].BarHeight == 4f && c[0].SideMargin == 2f
                  && vm.PanelOffsetTop == 80f && vm.PanelWidth == 300f, "open: the layout settings did not reach the ViewModel");
            Check(c[0].AthleticsText == "72% ± 8" && c[0].HealthText == "HP 81%" && c[0].HealthShown && Near(c[0].Fill, 0.72f) && Near(c[0].BandLow, 0.64f)
                  && Near(c[0].BandHigh, 0.80f) && Near(c[0].PeakLine, 0.75f) && c[0].FillColor == Color.ConvertStringToColor(BarMath.BlueHex),
                "open: Infantry's values: " + c[0].AthleticsText + " / " + c[0].HealthText + ", fill " + c[0].Fill + ", band " + c[0].BandLow + ".." + c[0].BandHigh);
            Check(c[1].FillColor == Color.ConvertStringToColor(BarMath.YellowHex) && c[2].FillColor == Color.ConvertStringToColor(BarMath.RedHex)
                  && c[2].AthleticsText == "10% ± 5", "open: the colours by f (Archers yellow, Cavalry red)");
            LogHas("[hud] orders strip: first placement under the cards at 2.0 s (open #1) - technique: the game's own cards read live, stand-in layer MissionOrder: 16 cards"
                   + " = 16 cards in 2 sets, set 1 drawn (the game's side columns - keyboard layout); screen 1920 x 1080 px, UI scale 1.00; cards drawn: "
                   + "1 Infantry at (20, 44) 131 x 223, 40 men (= formation) | 2 Archers at (20, 307) 131 x 223, 20 men (= formation) | 3 Cavalry at (20, 570) 131 x 223, 12 men (= formation); "
                   + "cells (numbers 13 px at card bottom +1, bar 4 px at +20, 2 px in from the sides (23 px deep; UI pixels - the game's UI scale applies)): "
                   + "1 Infantry at (20, 268) w 131 | 2 Archers at (20, 531) w 131 | 3 Cavalry at (20, 794) w 131");
            LogHas("[hud] orders strip: values at 2.0 s (open #1, under the cards): 1 Infantry 72% ± 8 HP 81% (40 men, f 0.90) | 2 Archers 45% ± 20 HP 60% (20 men, f 0.60)"
                   + " | 3 Cavalry 10% ± 5 HP 95% (12 men, f 0.13, 3 exhausted) - ± is 1.00 std, health on");

            // 2. the cells follow the cards every frame: another UI scale (2560 x 1440)
            t += 0.05;
            cards.Layout(army, 2, 0, 4 / 3f);
            view.Tick(Frame(t, player, orderMenu: true));
            Check(Near(c[0].PositionX, 20 * 4 / 3f) && Near(c[0].PositionY, 267 * 4 / 3f + 4 / 3f) && Near(c[0].CellWidth, 131 * 4 / 3f),
                "UI scale 4/3: the cell did not follow: " + c[0].PositionX + ", " + c[0].PositionY + " w " + c[0].CellWidth);
            LogHas(" s (open #1) - 16 cards in 2 sets, set 1 drawn (the game's side columns - keyboard layout); cards drawn: 1 Infantry at (27, 59) 175 x 297");

            // 3. the gamepad row (the second set) and RTS Camera Command System's single set
            t += 0.05;
            cards.Layout(army, 2, 1, 1f, row: true);
            view.Tick(Frame(t, player, orderMenu: true));
            Check(view.UnderCards && c[0].PositionX == 700f && c[1].PositionX == 851f && c[0].PositionY == 284f, "the gamepad row: cells at " + c[0].PositionX + " / " + c[1].PositionX + ", y " + c[0].PositionY);
            LogHas("16 cards in 2 sets, set 2 drawn (the game's top row - gamepad layout)");
            t += 0.05;
            cards.Layout(army, 1, 0);
            view.Tick(Frame(t, player, orderMenu: true));
            Check(view.UnderCards && c[0].PositionY == 268f, "RTS Camera's single set: not under the cards");
            LogHas("8 cards in 1 set, set 1 drawn (one layout - an order-menu mod such as RTS Camera Command System)");

            // 4. a cell that would leave the screen is lifted to its edge (a deeper bar, the bottom card)
            t += 0.05;
            army.Set(3, 9, 0.5, 0.1, 0.66, 0.7);
            cards.Layout(army, 2, 0);
            S.Set(SettingsSchema.OrderStripBarOffset, 30, SettingSources.Mcm);
            view.Tick(Frame(t, player, orderMenu: true));
            Check(c[3].IsShown && Near(c[3].PositionY, 1080f - 33f) && c[3].BarMarginTop == 29f, "the bottom card's cell was not lifted: y " + c[3].PositionY + ", bar margin " + c[3].BarMarginTop);
            LogHas("4 Horse archers at (20, 1047) w 131 - 1 cell lifted to the screen's bottom edge");
            S.Set(SettingsSchema.OrderStripBarOffset, 20, SettingSources.Mcm);
            army.Set(3, 0, 0, 0, 0, 0);
            cards.Layout(army, 2, 0);

            // 5. live switches: health and spread off / on (next refresh), layout at once
            t += 0.2;
            S.Set(SettingsSchema.ShowFormationHealth, false, SettingSources.Mcm);
            S.Set(SettingsSchema.ShowFormationSpread, false, SettingSources.Mcm);
            view.Tick(Frame(t, player, orderMenu: true));
            Check(!c[0].HealthShown && c[0].AthleticsText == "72%" && c[0].BandLow == c[0].BandHigh, "health / spread off did not reach the strip: " + c[0].AthleticsText + ", health " + c[0].HealthShown);
            S.Set(SettingsSchema.ShowFormationHealth, true, SettingSources.Mcm);
            S.Set(SettingsSchema.ShowFormationSpread, true, SettingSources.Mcm);
            S.Set(SettingsSchema.OrderStripTextSize, 16, SettingSources.Mcm);
            t += 0.2;
            view.Tick(Frame(t, player, orderMenu: true));
            Check(c[0].HealthShown && c[0].AthleticsText == "72% ± 8" && c[0].TextSize == 16, "back on: " + c[0].AthleticsText + ", size " + c[0].TextSize);
            S.Set(SettingsSchema.OrderStripTextSize, 13, SettingSources.Mcm);
            view.Tick(Frame(t += 0.2, player, orderMenu: true)); // a settings change re-pushes the values once

            // 6. new stats reach the cells when the logic's version moves (not before)
            army.Set(0, 38, 0.50, 0.10, 0.66, 0.70);
            cards.Layout(army, 2, 0);
            t += 0.2;
            view.Tick(Frame(t, player, orderMenu: true));
            Check(c[0].AthleticsText == "72% ± 8", "values re-pushed without a new stats version");
            BumpFormationStats(logic);
            t += 0.2;
            view.Tick(Frame(t, player, orderMenu: true));
            Check(c[0].AthleticsText == "50% ± 10" && c[0].HealthText == "HP 70%" && c[0].FillColor == Color.ConvertStringToColor(BarMath.YellowHex), "new stats not pushed: " + c[0].AthleticsText);

            // 7. close: removed QUIETLY (the first open was logged in full); reopen: the same cards = no line
            t += 0.5;
            int created = layer.Created;
            int text = LogText.Length;
            view.Tick(Frame(t, player));
            Check(!layer.Up && !view.IsLayerUp, "the strip stayed with the menu closed");
            Check(!LogText.Substring(text).Contains("orders strip: layer removed"), "closing the menu was logged outside the verbose log");
            t += 1;
            view.Tick(Frame(t, player, orderMenu: true));
            vm = view.CurrentViewModel!;
            c = vm.Cells;
            Check(layer.Up && layer.Created == created + 1 && view.UnderCards && c[0].IsShown, "reopen: not under the cards");
            string reopened = LogText.Substring(text);
            Check(!reopened.Contains("orders strip: layer created") && !reopened.Contains("the cards changed") && reopened.Contains("orders strip: values at ")
                  && reopened.Contains("(open #2, under the cards)"), "reopen: the log should hold only the values: " + reopened);

            // 8. a short mismatch (a man fell between two events) is waited out; a long one falls back
            cards.Cards[1].Members = 25;
            for (int i = 0; i < 2; i++)
            {
                t += 0.3;
                view.Tick(Frame(t, player, orderMenu: true, dt: 0.3f));
            }
            Check(view.UnderCards && !vm.PanelShown, "a 0.6 s mismatch already fell back");
            cards.Cards[1].Members = 20;
            t += 0.05;
            view.Tick(Frame(t, player, orderMenu: true));
            Check(view.UnderCards && view.StripStats.ShortMismatches == 1, "the short mismatch was not counted: " + view.StripStats.ShortMismatches);
            cards.Cards[1].Members = 25;
            for (int i = 0; i < 4; i++)
            {
                t += 0.3;
                view.Tick(Frame(t, player, orderMenu: true, dt: 0.3f));
            }
            Check(view.PanelUp && view.Fallback == StripFallback.Mismatch && vm.PanelShown && !vm.StripShown && c[0].IsShown && c[1].IsShown && c[2].IsShown && !c[3].IsShown,
                "a 1.2 s mismatch did not bring the panel with the right rows");
            LogHas("[hud] orders strip: FALLBACK to the compact panel at ");
            LogHas(" s (open #2) - 2 Archers's card counts 25 men, the formation has 20 for more than 1.0 s; the panel lists your formations at the top of the screen (80 px down, 300 px wide) until the menu closes - the next open tries the cards again");
            t += 0.3;
            view.Tick(Frame(t, player, orderMenu: true));
            Check(view.PanelUp, "the panel did not stay for the rest of the open");
            cards.Cards[1].Members = 20;
            view.Tick(Frame(t + 0.1, player));                       // close
            view.Tick(Frame(t + 0.5, player, orderMenu: true));      // the next open tries the cards again
            t += 0.5;
            Check(view.UnderCards, "the next open did not try the cards again");

            // 9. no order layer / not whole sets / never drawn - each brings the panel with its reason
            void OpenWith(Action setUp, StripFallback expected, string logged, int frames = 1, float dt = 0.05f)
            {
                view.Tick(Frame(t += 0.2, player));
                setUp();
                for (int i = 0; i < frames; i++) view.Tick(Frame(t += dt, player, orderMenu: true, dt: dt));
                Check(view.PanelUp && view.Fallback == expected && view.CurrentViewModel!.PanelShown, expected + ": no panel (mode " + (view.UnderCards ? "cards" : "other") + ")");
                LogHas(logged);
            }
            OpenWith(() => cards.ScanOk = false, StripFallback.NoOrderLayer, "open #4) - stand-in: no order layer; the panel lists your formations");
            cards.ScanOk = true;
            OpenWith(() => cards.Count = 12, StripFallback.NotWholeSets, "- 12 cards found - not whole sets of 8 (an order-menu mod with its own cards?)");
            OpenWith(() => cards.Layout(army, 2, -1), StripFallback.NeverShown, "- no card drawn (of 16 found) 0.60 s after the menu opened", frames: 4, dt: 0.15f);
            cards.Layout(army, 2, 0);

            // 10. OrderStripUnderCards off: the panel (no FALLBACK word); on again mid-open: the cards
            S.Set(SettingsSchema.OrderStripUnderCards, false, SettingSources.Mcm);
            OpenWith(() => { }, StripFallback.SwitchedOff, "[hud] orders strip: the compact panel at ");
            LogHas(" - OrderStripUnderCards is off; the panel lists your formations at the top of the screen (80 px down, 300 px wide) until the menu closes");
            S.Set(SettingsSchema.OrderStripUnderCards, true, SettingSources.Mcm);
            view.Tick(Frame(t += 0.05, player, orderMenu: true));
            Check(view.UnderCards && view.CurrentViewModel!.StripShown, "OrderStripUnderCards on again mid-open: not back under the cards");

            // 11. the card tree replaced mid-open (the order type changed): scanned again
            int scans = cards.Scans;
            cards.ReadOk = false;
            view.Tick(Frame(t += 0.05, player, orderMenu: true));
            Check(view.PanelUp && view.Fallback == StripFallback.NoOrderLayer && cards.Scans == scans + 1, "a lost card tree was not re-scanned once, then the panel");
            cards.ReadOk = true;

            // 12. the usual gates remove it with their reason (logged in full, not quietly)
            view.Tick(Frame(t += 0.2, player));
            view.Tick(Frame(t += 0.2, player, orderMenu: true));
            // (the shared "hud-layer" log bucket is spent by now in this fast run - the stats prove it)
            S.Set(SettingsSchema.ShowInOrderMenu, false, SettingSources.Mcm);
            view.Tick(Frame(t += 0.2, player, orderMenu: true));
            Check(!layer.Up && view.Stats.RemovedBy(HudHide.ToggleOff) == 1, "ShowInOrderMenu off: the strip stayed");
            S.Set(SettingsSchema.ShowInOrderMenu, true, SettingSources.Mcm);
            view.Tick(Frame(t += 0.2, player, orderMenu: true));
            view.Tick(Frame(t += 0.2, player, hideUi: true, orderMenu: true));
            Check(!layer.Up && view.Stats.RemovedBy(HudHide.HideBattleUI) == 1, "Hide battle UI: the strip stayed");
            view.Tick(Frame(t += 0.2, player, MissionMode.Deployment, orderMenu: true));
            Check(!layer.Up, "deployment: the strip is up");

            // 13. mission end and the summary, through the logic as at a real mission end
            view.Tick(Frame(t += 0.2, player, orderMenu: true));
            view.Finish(t += 0.5);
            Check(!layer.Up, "mission end: the strip stayed");
            ((List<TraxHudView>)typeof(AthleticsLogic).GetField("_hudViews", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(logic)!).Add(view);
            typeof(AthleticsLogic).GetMethod("WriteHudSummary", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(logic, null);
            LogHas("[summary] hud: orders strip (movie TraxOrderStrip) - on screen ");
            LogHas("[summary] hud: orders strip - opened 10x: under the cards in 7, the compact panel in 5; technique: the live vanilla cards (stand-in layer MissionOrder: 16 cards); "
                   + "card layouts seen: 16 cards in 2 sets, set 1 drawn | 16 cards in 2 sets, set 2 drawn | 8 cards in 1 set, set 1 drawn; cells placed ");
            LogHas(" (lifted to the screen's edge 1), card changes 5, short mismatches 1 (under 1.0 s), card re-scans 1, values pushed ");
            LogHas("; fallbacks 6 (OrderStripUnderCards off 1, no order cards found 2, not whole sets of cards 1, cards never drawn 1, a card disagreed with its formation 1)"
                   + " - the first at ");
            LogHas(" s: 2 Archers's card counts 25 men, the formation has 20 for more than 1.0 s");
            Check(Occurrences(LogText, "[hud] orders strip: first placement under the cards", logFrom) == 1, "the first placement was logged more than once");
            Check(view.StripStats.CellsLifted == 1 && view.StripStats.Opens == 10, "the strip's stats: lifted " + view.StripStats.CellsLifted + ", opens " + view.StripStats.Opens);
            SetStatic(typeof(AthleticsLogic), "_current", null);
        }

        /// <summary>From the master-switch step: the strip goes with ModEnabled and comes back.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void StripFollowsTheMasterSwitch(Agent player)
        {
            StripDefaults();
            var army = StripArmy();
            var cards = new FakeCards();
            cards.Layout(army, 2, 0);
            var view = new OrderStripView();
            var layer = new FakeHudLayer();
            view.UseLayer(layer);
            view.UseSources(cards, army);
            view.Tick(Frame(910, player, orderMenu: true));
            Check(layer.Up && view.UnderCards, "master switch: precondition - the strip is not up");
            S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
            view.Tick(Frame(911, player, orderMenu: true));
            Check(!layer.Up && view.LastDecision == HudHide.ModOff, "mod off: the orders strip stayed on screen");
            LogHas("[hud] orders strip: layer removed at 911.0 s - ModEnabled off (the master switch)");
            S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
            view.Tick(Frame(912, player, orderMenu: true));
            Check(layer.Up && layer.Created == 2 && view.UnderCards, "mod back on: the orders strip did not come back");
            view.Finish(913);
        }

        /// <summary>The strip's fail safe: a failing card walk disables the view for the mission.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void StripFailSafe()
        {
            StripDefaults();
            var player = FakeAgent(41);
            var army = StripArmy();
            var cards = new FakeCards { Throw = true };
            cards.Layout(army, 2, 0);
            var view = new OrderStripView();
            var layer = new FakeHudLayer();
            view.UseLayer(layer);
            view.UseSources(cards, army);
            int errors = TraxLog.ErrorCount;
            view.Tick(Frame(20, player, orderMenu: true));
            view.Tick(Frame(21, player, orderMenu: true));
            Check(view.IsDisabled && !layer.Up && TraxLog.ErrorCount == errors + 1, "a failing card walk did not disable the strip (or logged twice)");
            LogHas("[error] hud.tick: System.InvalidOperationException: smoke: the card walk failed");
            LogHas("[hud] orders strip: an error in tick at 20.0 s (InvalidOperationException: smoke: the card walk failed - stack in the [error] line) - DISABLED for the rest of this battle");

            // a view never given its sources fails safe too
            var orphan = new OrderStripView();
            orphan.UseLayer(new FakeHudLayer());
            orphan.Tick(Frame(22, player, orderMenu: true));
            Check(orphan.IsDisabled, "a strip without sources did not disable itself");
        }
    }
}

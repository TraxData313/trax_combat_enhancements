using System;
using System.Linq;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Step 72 (Toni 2026-10-19: a click or a drag that crossed another unit's banner became a charge at it): NO ACCIDENTAL CHARGE ORDERS through
    /// the real <see cref="OrderClickGuard"/> with a stand-in world and a stand-in of the game's release (vanilla's GauntletOrderUIHandler: on the
    /// release, a focused unit = a targeted charge, else the troop placer's move) - in both frame orders (the screen's release read before and
    /// after the guard's pre-display tick). What it cannot check: the engine's own frame order, the real focus, the real placer (PLAYTEST D37).
    /// </summary>
    internal static partial class Program
    {
        private sealed class FakeOrderWorld : IOrderClickWorld
        {
            public bool Active = true;
            public bool Down;
            public bool Target;
            public double Icon = double.NaN;
            public double Drag;
            public bool Disabled;
            public int Switches;
            public int Presses;

            public bool OrderModeActive => Active;
            public bool ButtonDown => Down;
            public double ScreenHeight => 1080;

            public void Look(bool press, out bool hasTarget, out double iconPixels, out double dragPixels)
            {
                if (press) Presses++;
                hasTarget = Target;
                iconPixels = Target ? Icon : double.NaN;
                dragPixels = press ? 0 : Drag;
            }

            public void SetTargetingDisabled(bool disabled)
            {
                Disabled = disabled;
                Switches++;
            }
        }

        /// <summary>The stand-in game: one frame - the guard's pre-display tick and the orders UI's release read, in either order.</summary>
        private sealed class FakeOrderFrames
        {
            public readonly FakeOrderWorld World = new FakeOrderWorld();
            public readonly OrderClickGuard Guard;
            public int Charges;
            public int Moves;
            private bool _uiPrevDown;
            private double _t = 100;

            public FakeOrderFrames()
            {
                Guard = new OrderClickGuard(World);
                Guard.Start();
            }

            public void Frame(bool down, bool target, double icon, double drag, bool uiFirst)
            {
                World.Down = down;
                World.Target = target;
                World.Icon = icon;
                World.Drag = drag;
                _t += 1 / 60.0;
                if (uiFirst) Ui();
                Guard.Tick(_t);
                if (!uiFirst) Ui();
            }

            /// <summary>Vanilla's release: the focus (none while held off) → a targeted charge, else the placer's move.</summary>
            private void Ui()
            {
                bool released = _uiPrevDown && !World.Down && World.Active;
                _uiPrevDown = World.Down;
                if (!released) return;
                if (World.Target && !World.Disabled)
                {
                    Charges++;
                    Guard.OnOrder(true, false, _t);
                }
                else
                {
                    Moves++;
                    Guard.OnOrder(false, true, _t);
                }
            }

            public void Idle(int frames, bool uiFirst)
            {
                for (int i = 0; i < frames; i++) Frame(false, false, double.NaN, 0, uiFirst);
            }
        }

        private static void Step72Defaults()
        {
            S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.OrderNoAccidentalCharge, true, SettingSources.File);
            S.Set(SettingsSchema.OrderChargeDragPixels, 12, SettingSources.File);
            S.Set(SettingsSchema.OrderChargeIconPixels, 30, SettingSources.File);
        }

        private static void NoAccidentalChargeOrders()
        {
            Step72Defaults();
            foreach (bool uiFirst in new[] { false, true })
            {
                string order = uiFirst ? " (the release read BEFORE the guard's tick)" : " (the guard's tick first)";
                var g = new FakeOrderFrames();

                // 1. a drag from open ground across an enemy banner, released ON it: a move, no charge
                g.Idle(2, uiFirst);
                g.Frame(true, false, double.NaN, 0, uiFirst);
                Check(g.World.Disabled, "step 72: a press off any banner did not hold the targeting off" + order);
                for (int k = 1; k <= 10; k++) g.Frame(true, k >= 5, k >= 5 ? 40 - k * 4 : double.NaN, k * 6, uiFirst);
                Check(g.World.Disabled, "step 72: a drag onto the banner let the targeting back on" + order);
                g.Frame(false, true, 0, 60, uiFirst);
                g.Idle(2, uiFirst);
                Check(g.Charges == 0 && g.Moves == 1, "step 72: a drag across a banner gave " + g.Charges + " charges, " + g.Moves + " moves" + order);
                Check(!g.World.Disabled, "step 72: the targeting not given back after the drag" + order);
                Check(g.Guard.Stats.ConvertedDrag == 1 && g.Guard.Stats.MovesAfterConversion == 1, "step 72: the drag not counted as converted (" + g.Guard.Stats.ConvertedDrag + ")" + order);

                // 2. a plain click ON the icon: the game's targeted charge (vanilla)
                g.Frame(true, true, 8, 0, uiFirst);
                Check(!g.World.Disabled, "step 72: a press on the icon held the targeting off" + order);
                g.Frame(true, true, 9, 2, uiFirst);
                g.Frame(true, true, 9, 3, uiFirst);
                g.Frame(false, true, 9, 3, uiFirst);
                g.Idle(2, uiFirst);
                Check(g.Charges == 1, "step 72: a click on the banner did not charge" + order);
                Check(g.Guard.Stats.ChargesKept == 1, "step 72: the deliberate charge not counted as kept" + order);

                // 3. a click near the banner (vanilla focuses it at 50 px from the centre) - not on its icon: a move
                g.Frame(true, true, 50, 0, uiFirst);
                g.Frame(true, true, 50, 1, uiFirst);
                g.Frame(false, true, 50, 1, uiFirst);
                g.Idle(2, uiFirst);
                Check(g.Charges == 1 && g.Moves == 2, "step 72: a click near the banner charged" + order);
                Check(g.Guard.Stats.ConvertedOffIcon == 1, "step 72: the click near the banner not counted" + order);

                // 4. a click on empty ground: a plain move, nothing held off for long, nothing converted
                g.Frame(true, false, double.NaN, 0, uiFirst);
                g.Frame(false, false, double.NaN, 0, uiFirst);
                g.Idle(2, uiFirst);
                Check(g.Guard.Stats.PlainMoves == 1 && g.Moves == 3, "step 72: a click on the ground not a plain move" + order);

                // 5. no button (the F keys): the targeting never touched however long the cursor rests on a banner
                int switches = g.World.Switches;
                for (int k = 0; k < 30; k++) g.Frame(false, true, 50, 0, uiFirst);
                Check(g.World.Switches == switches && !g.World.Disabled, "step 72: the targeting touched without a click" + order);

                // 6. the orders menu closes mid-hold: let go at once, counted as cut short
                g.Frame(true, true, 80, 0, uiFirst);
                Check(g.World.Disabled, "step 72: a press off the icon not held off" + order);
                g.World.Active = false;
                g.Frame(true, true, 80, 0, uiFirst);
                Check(!g.World.Disabled && g.Guard.Stats.Aborted == 1, "step 72: the menu closing did not give the targeting back" + order);
                g.World.Active = true;
                g.Frame(false, false, double.NaN, 0, uiFirst);

                // 7. the master switch off: vanilla - the drag across the banner charges, the targeting never touched
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                switches = g.World.Switches;
                g.Frame(true, false, double.NaN, 0, uiFirst);
                for (int k = 1; k <= 6; k++) g.Frame(true, true, 5, k * 10, uiFirst);
                g.Frame(false, true, 5, 60, uiFirst);
                g.Idle(2, uiFirst);
                Check(g.Charges == 2 && g.World.Switches == switches, "step 72: ModEnabled off is not vanilla (charges " + g.Charges + ", switches " + (g.World.Switches - switches) + ")" + order);
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);

                // 8. the switch off mid-drag: the targeting back at once
                g.Frame(true, false, double.NaN, 0, uiFirst);
                g.Frame(true, true, 5, 30, uiFirst);
                Check(g.World.Disabled, "step 72: a drag not held off" + order);
                S.Set(SettingsSchema.OrderNoAccidentalCharge, false, SettingSources.Mcm);
                g.Frame(true, true, 5, 40, uiFirst);
                Check(!g.World.Disabled, "step 72: OrderNoAccidentalCharge off mid-drag kept the targeting off" + order);
                g.Frame(false, true, 5, 40, uiFirst);
                g.Idle(2, uiFirst);
                S.Set(SettingsSchema.OrderNoAccidentalCharge, true, SettingSources.Mcm);

                var st = g.Guard.Stats;
                Check(st.Leaks == 0 && st.Errors == 0, "step 72: leaks " + st.Leaks + ", errors " + st.Errors + order);
                string line = g.Guard.SummaryLines().Single();
                Check(line.StartsWith("order clicks (step 72): 4 clicks in the orders menu - 1 deliberate charges kept (a click on the banner), 2 charges turned into moves (1 drags, 1 clicks off the icon; the placer's move seen after 2 of them), 1 plain moves", StringComparison.Ordinal),
                    "step 72: the summary line reads: " + line + order);
                g.Guard.Release();
            }
            LogHas("[orders] mission start: no accidental charge orders (step 72)");
            LogHas("first time the game's unit targeting was held OFF during a click");
            LogHas("first accidental charge turned into a MOVE at");
            LogHas("a drag across a unit (the cursor 60 px from the press) - the game's targeting held off, its troop placer's move went out");
            LogHas("a click near a unit but not on its banner icon - the game's targeting held off, its troop placer's move went out");
            LogHas("first deliberate charge KEPT at");
            LogHas("settings changed mid-mission at");

            // a battle the guard stands down from (naval): never touches the targeting, the summary says why
            var naval = new FakeOrderFrames();
            naval.Guard.StandDown("a naval battle (War Sails drives the game's targeting itself)");
            naval.Frame(true, true, 80, 0, false);
            naval.Frame(false, true, 80, 0, false);
            Check(naval.World.Switches == 0 && naval.Charges == 1, "step 72: a stood-down guard touched the targeting");
            Check(naval.Guard.SummaryLines().Single().StartsWith("order clicks (step 72): not guarded this battle - a naval battle", StringComparison.Ordinal), "step 72: the stood-down summary line");
            LogHas("no accidental charge orders (step 72) NOT guarding this battle: a naval battle");

            // the failure line: a charge while converting (something else issued it)
            var leak = new FakeOrderFrames();
            leak.Frame(true, true, 80, 0, false);
            leak.Guard.OnOrder(true, false, 1);
            leak.Frame(false, true, 80, 0, false);
            leak.Idle(2, false);
            Check(leak.Guard.Stats.Leaks == 1, "step 72: a charge during a conversion not counted as slipped through");
            LogHas("a targeted charge went out although the click was being turned into a move");
        }
    }
}

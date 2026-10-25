using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Step 98 (Toni 2026-10-25: "the bandits far on the left outside the screen are highlighted and I click on them and I'm ordering a charge"):
    /// vanilla's MissionFormationTargetSelectionHandler focuses an enemy unit within 10 m of the camera WHEREVER its banner projects (only
    /// "behind the camera" is refused), so a unit the player cannot see takes the focus and any release charges it. Through the real
    /// <see cref="Missions.OrderClickGuard"/> with step 72's stand-in world and stand-in release: the game's pick an off-screen unit = held off with
    /// no button down, a click (even one whose projected point lands under the cursor, or a press the guard never saw) = a MOVE; an on-screen
    /// icon click still charges; the release frame's menu close counts the click (not "cut short").
    /// </summary>
    internal static partial class Program
    {
        private static void OffScreenFocusNeverCharges()
        {
            Step72Defaults();
            foreach (bool uiFirst in new[] { false, true })
            {
                string order = uiFirst ? " (the release read BEFORE the guard's tick)" : " (the guard's tick first)";
                var g = new FakeOrderFrames();
                g.Idle(2, uiFirst);

                // 1. no button: the game focuses the bandits 7.5 m from the camera, their banner off the left edge - held off (no highlight)
                g.World.OnScreen = false;
                for (int k = 0; k < 10; k++) g.Frame(false, true, 1380, 0, uiFirst);
                Check(g.World.Disabled, "step 98: an off-screen focus with no button down not held off" + order);
                Check(g.Guard.Stats.OffScreenHolds == 1 && g.Guard.Stats.OffScreenHoldFrames == 10,
                    "step 98: the idle hold not counted (" + g.Guard.Stats.OffScreenHolds + " / " + g.Guard.Stats.OffScreenHoldFrames + ")" + order);

                // 2. a click while they keep the focus - even with a projection landing under the cursor (5 px): a MOVE, never a charge
                g.Frame(true, true, 5, 0, uiFirst);
                Check(g.World.Disabled, "step 98: a press with an off-screen focus did not hold the targeting off" + order);
                g.Frame(true, true, 5, 1, uiFirst);
                g.Frame(false, true, 5, 1, uiFirst);
                g.Frame(false, true, 1380, 0, uiFirst);
                Check(g.Charges == 0 && g.Moves == 1, "step 98: a click with an off-screen focus gave " + g.Charges + " charges, " + g.Moves + " moves" + order);
                Check(g.Guard.Stats.ConvertedOffScreen == 1, "step 98: the click not counted as turned from an off-screen charge" + order);

                // 3. a press the guard never saw (blind): the release still finds no unit - the idle hold covers it
                g.World.Blind = true;
                g.Frame(true, true, 1380, 0, uiFirst);
                g.Frame(true, true, 1380, 0, uiFirst);
                g.Frame(false, true, 1380, 0, uiFirst);
                g.World.Blind = false;
                Check(g.Charges == 0 && g.Moves == 2, "step 98: an unseen press with an off-screen focus charged" + order);

                // 4. the unit walks into view: the game's targeting is its own again; a click ON its icon still charges (vanilla)
                g.World.OnScreen = true;
                g.Frame(false, true, 8, 0, uiFirst);
                Check(!g.World.Disabled, "step 98: an on-screen focus still held off with no button down" + order);
                g.Frame(true, true, 8, 0, uiFirst);
                Check(!g.World.Disabled, "step 98: a press on an on-screen icon held off" + order);
                g.Frame(true, true, 9, 2, uiFirst);
                g.Frame(false, true, 9, 2, uiFirst);
                g.Idle(2, uiFirst);
                Check(g.Charges == 1 && g.Guard.Stats.ChargesKept == 1, "step 98: a click on an on-screen icon did not charge" + order);

                // 5. the release closes the orders menu (vanilla, not holding the order key): the click is counted, not "cut short"
                g.CloseOnRelease = true;
                g.Frame(true, true, 8, 0, uiFirst);
                g.Frame(false, true, 8, 0, uiFirst);
                g.Idle(2, uiFirst);
                g.World.Active = true;
                g.CloseOnRelease = false;
                Check(g.Charges == 2 && g.Guard.Stats.ChargesKept == 2 && g.Guard.Stats.Aborted == 0,
                    "step 98: a click whose release closed the menu counted as cut short (kept " + g.Guard.Stats.ChargesKept + ", cut short " + g.Guard.Stats.Aborted + ")" + order);

                // 6. the master switch off: vanilla - the off-screen focus charges, the targeting never touched
                g.World.OnScreen = false;
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                g.Frame(false, true, 1380, 0, uiFirst);
                int switches = g.World.Switches;
                g.Frame(true, true, 1380, 0, uiFirst);
                g.Frame(false, true, 1380, 0, uiFirst);
                g.Idle(2, uiFirst);
                Check(g.Charges == 3 && g.World.Switches == switches && !g.World.Disabled, "step 98: ModEnabled off is not vanilla" + order);
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                g.World.OnScreen = true;

                var st = g.Guard.Stats;
                Check(st.Leaks == 0 && st.Errors == 0, "step 98: leaks " + st.Leaks + ", errors " + st.Errors + order);
                string line = g.Guard.SummaryLines().Single();
                Check(line.Contains("step 98 - a unit OFF the screen had the focus: 1 clicks on it turned into moves, "),
                    "step 98: the summary line reads: " + line + order);
                g.Guard.Release();
            }
            LogHas("first time an off-screen / behind-the-camera unit had the focus - targeting held off at");
            LogHas("(the game's 10 m rule focuses it wherever it is on the screen), its banner at (-420, 560) px, w 1 - OFF the screen (1920 x 1080)");
            LogHas("first click while an off-screen / behind-the-camera unit had the focus at");
        }
    }
}

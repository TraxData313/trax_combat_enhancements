using System;
using TaleWorlds.InputSystem;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Step 85 (Toni 2026-10-24: "when pressing left thumbstick using controller it acts like pressing the CTRL activating the sprint"): THE
    /// SPRINT ON A CONTROLLER BUTTON through the real logic with step 33's stand-in sprint body - every SprintControllerKey name is one of the
    /// game's InputKey values (None = Invalid, never down); the pad's button held starts a sprint exactly as the key does, letting go ends it,
    /// switching from one key to the other keeps it running, None never asks the pad, the order menu / the master switch hold it like the key; the
    /// log line names both keys. What it cannot check: the gamepad's real stick press reaching Input.IsKeyDown (PLAYTEST D50).
    /// </summary>
    internal static partial class Program
    {
        private static void SprintOnAControllerButton()
        {
            var keep = _logic;
            try
            {
                // the game's keys behind the list
                var names = KeyNames.Controller.All;
                Check(GameSprintBody.ControllerKeyFor(SprintKeys.NoController) == InputKey.Invalid, "None is a game key");
                for (int i = 1; i < names.Length; i++)
                {
                    var k = GameSprintBody.ControllerKeyFor(i);
                    Check(k != InputKey.Invalid && k.ToString() == names[i], "the controller name " + names[i] + " is not one of the game's InputKey names (" + k + ")");
                }
                Check(GameSprintBody.ControllerKeyFor(1) == InputKey.ControllerLThumb && (int)InputKey.ControllerLThumb == 252, "ControllerLThumb is not the game's key 252");
                Check(GameSprintBody.ControllerKeyFor(names.Length) == InputKey.Invalid && GameSprintBody.ControllerKeyFor(-1) == InputKey.Invalid, "outside the list is a key");

                AthleticsDefaults();
                SprintDefaults();
                S.Set(SettingsSchema.SprintControllerKey, 1, SettingSources.File); // ControllerLThumb - the shipped default
                var body = new FakeSprintBody();
                var logic = NewSprintLogic(body);
                var stats = logic.SprintStats;
                LogHas("[sprint] mission start: ON - hold LeftControl or ControllerLThumb (on foot, in a fight)");
                double t = 25000;
                void Tick(float dt = 0.1f)
                {
                    t += dt;
                    logic.TickSprint(dt, t, Rules);
                }

                var me = logic.Track(FakeAgent(850))!;
                me.AthleticsSkill = 150;
                logic.SmokePlayer = me.Agent;
                body.Speeds[850] = 3f;
                Tick();
                Check(!me.Sprint.Active && body.LastControllerIndex == 1, "the pad's button asked is not SprintControllerKey's index: " + body.LastControllerIndex);

                // the left stick held: a sprint, exactly as the key's
                body.ControllerKey = true;
                Tick();
                Check(me.Sprint.Active && me.Sprint.ByKey && me.SprintRun == 1.6f, "the pad's button did not start a sprint: active " + me.Sprint.Active + ", x" + me.SprintRun);
                Check(stats.YouStarted == 1, "the pad's sprint not counted as yours");
                for (int i = 0; i < 5; i++) Tick();
                Check(me.Sprint.Active, "holding the pad's button did not keep the sprint");
                // the keyboard key pressed, the stick let go: still one sprint (either key is the sprint key)
                body.Key = true;
                Tick();
                body.ControllerKey = false;
                Tick();
                Check(me.Sprint.Active && stats.YouStarted == 1, "switching from the pad to the key broke the sprint: active " + me.Sprint.Active + ", started " + stats.YouStarted);
                // both let go: ended, the rest stored
                body.Key = false;
                Tick();
                Check(!me.Sprint.Active && stats.Ends(true, SprintEnd.Released) == 1, "letting go of both did not end it");

                // the order menu: the pad held is no sprint, as with the key
                body.CanSprint = false;
                body.ControllerKey = true;
                for (int i = 0; i < 3; i++) Tick();
                Check(!me.Sprint.Active, "the pad's button sprinted with the order menu open");
                body.CanSprint = true;
                body.ControllerKey = false;
                Tick();
                body.ControllerKey = true;
                Tick();
                Check(me.Sprint.Active && stats.YouStarted == 2, "a new press of the pad's button after the menu did not start one");
                body.ControllerKey = false;
                Tick();
                Check(!me.Sprint.Active, "letting go of the pad's button did not end it");
                for (int i = 0; i < 80; i++) Tick(); // refill

                // None: the pad is never asked and its button does nothing; the key still works; the log names the key alone
                S.Set(SettingsSchema.SprintControllerKey, SprintKeys.NoController, SettingSources.Mcm);
                int reads = body.ControllerReads;
                body.ControllerKey = true;
                for (int i = 0; i < 5; i++) Tick();
                Check(!me.Sprint.Active && body.ControllerReads == reads, "None: the pad sprinted or was read (" + (body.ControllerReads - reads) + " reads)");
                body.Key = true;
                Tick();
                Check(me.Sprint.Active, "None: the keyboard key no longer sprints");
                body.Key = false;
                body.ControllerKey = false;
                Tick();
                logic.GetType().GetMethod("NoteSprintSettings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(logic, new object[] { S });
                LogHas("[sprint] settings changed mid-mission at ");
                LogHas(": ON - hold LeftControl (on foot, in a fight)");

                // another button chosen, live: RB
                S.Set(SettingsSchema.SprintControllerKey, 4, SettingSources.Mcm);
                for (int i = 0; i < 80; i++) Tick();
                body.ControllerKey = true;
                Tick();
                Check(me.Sprint.Active && body.LastControllerIndex == 4, "a live change to RB was not read: index " + body.LastControllerIndex);
                body.ControllerKey = false;
                Tick();

                // the master switch first: the pad held sprints nobody
                for (int i = 0; i < 80; i++) Tick();
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                body.ControllerKey = true;
                for (int i = 0; i < 3; i++) Tick();
                Check(!me.Sprint.Active, "the master switch off: the pad's button sprinted");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                body.ControllerKey = false;
                Tick();
                logic.SmokePlayer = null;
            }
            finally
            {
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                S.Set(SettingsSchema.SprintControllerKey, SprintKeys.NoController, SettingSources.File);
                SprintOff();
                _logic = keep;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TraxCombat.Core;
using TraxCombat.Hud;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for the Attack recovery bar (step 13, Anton: "above that bar add a bar 'attack
    /// recovery' that empties when I attack and until it fills I can't attack; inside it add the secs
    /// delay added"): its prefab against the game (like the Athletics bar's), and the REAL
    /// <see cref="AttackRecoveryView"/> + <see cref="AttackRecoveryVM"/> driven by made-up frames over a
    /// real <see cref="AthleticsLogic"/> whose player timer runs (the fake agent stands in for
    /// Mission.MainAgent): full at rest, empty at the attack, filling over the pause with the seconds
    /// inside, the flash on an early press (and not with FlashBarOnEarlyAttack off), its show / hide
    /// switches, the live layout, the log and the summary line.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>Game-typed statics live in a nested class (Program's own statics initialise before the resolver).</summary>
        private static class RecoveryColors
        {
            public static readonly Color Recovering = Color.ConvertStringToColor(AttackTimerMath.RecoveringHex);
            public static readonly Color Ready = Color.ConvertStringToColor(AttackTimerMath.ReadyHex);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RecoveryPrefabIsValid() => PrefabIsValid(AttackRecoveryView.Movie, typeof(AttackRecoveryVM), 8, 50);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RecoveryBarThroughTheView()
        {
            var keep = _logic;
            try
            {
                var logic = HudLogic();
                S.Set(SettingsSchema.ShowAttackRecoveryBar, true, SettingSources.File);
                S.Set(SettingsSchema.FlashBarOnEarlyAttack, true, SettingSources.File);
                S.Set(SettingsSchema.RecoveryBarWidth, 205, SettingSources.File);
                S.Set(SettingsSchema.RecoveryBarHeight, 14, SettingSources.File);
                S.Set(SettingsSchema.RecoveryBarOffsetAbove, 24, SettingSources.File);
                S.Set(SettingsSchema.StepBackEnabled, false, SettingSources.File);
                logic.PaceBody = new FakePaceBody();
                logic.StepBackBody = new FakeStepBody { Allow = false };
                var me = logic.Track(FakeAgent(400))!;
                me.AthleticsSkill = 300;
                logic.SmokePlayer = me.Agent;
                var view = new AttackRecoveryView();
                var layer = new FakeHudLayer();
                view.UseLayer(layer);
                var r = Rules;
                double t = 7000;

                // outside a battle with empty hands and a full bar: not shown; in battle: built, FULL, no text
                view.Tick(Frame(t, me.Agent, MissionMode.StartUp));
                Check(!layer.Up, "shown outside a battle with empty hands and a full bar");
                t += 0.05;
                view.Tick(Frame(t, me.Agent));
                var vm = view.CurrentViewModel;
                Check(layer.Up && vm != null && vm.IsShown && vm.Fill == 1f && !vm.SecondsShown && vm.FillColor == RecoveryColors.Ready && !vm.FlashOn,
                    "in battle at rest the recovery bar is not up, full and quiet");
                Check(vm != null && vm.BarWidth == 205f && vm.BarHeight == 14f && vm.OffsetRight == 62f && vm.OffsetBottom == 78f,
                    "layout: " + (vm == null ? "no VM" : vm.BarWidth + " x " + vm.BarHeight + ", right " + vm.OffsetRight + ", bottom " + vm.OffsetBottom + " (54 + 24)"));
                LogHas("[hud] recovery bar: layer created at ");
                LogHas("[hud] recovery bar: first values pushed at ");
                LogHas(" - full (no pause running); bar 205 x 14 px, 62 px from the right edge and 78 px from the bottom (your Athletics bar's row 54 + 24; UI pixels - the game's UI scale applies); flash on an early attack on");

                // tired (a wound: f 0.53), he swings: at the release the bar EMPTIES
                SetHealth(me.Agent, 40f, 100f);
                logic.CheckHealth(me, Rules, t);
                me.SpeedDirty = false;
                logic.ObserveAction(me, ActReady, t, in r);
                AthleticsLogic.ReadyFull(me, t + 0.32);
                t += 0.4;
                logic.ObserveAction(me, ActRelease, t, in r);
                view.Tick(Frame(t, me.Agent));
                Check(vm!.Fill == 0f && vm.FillColor == RecoveryColors.Recovering && !vm.SecondsShown, "the bar did not empty at the attack: " + vm.Fill);

                // a press during the swing: swallowed - the bar flashes (two pulses), then stops
                logic.GateFrame(me, t + 0.01, false);
                logic.GateFrame(me, t + 0.05, true);
                view.Tick(Frame(t + 0.06, me.Agent));
                Check(vm.FlashOn && view.FlashesShown == 1, "the early press did not flash the bar");
                LogHas("[hud] recovery bar: first flash at ");
                LogHas(" - you pressed attack during your own attack (2 pulses of 0.12 s)");
                view.Tick(Frame(t + 0.2, me.Agent));
                Check(!vm.FlashOn, "the flash did not pause between its pulses");
                view.Tick(Frame(t + 0.27, me.Agent));
                Check(vm.FlashOn, "no second pulse");
                view.Tick(Frame(t + 0.4, me.Agent));
                Check(!vm.FlashOn && view.FlashesShown == 1, "the flash did not end / counted twice");
                logic.GateFrame(me, t + 0.41, false);

                // the attack ends: the pause runs - the bar fills with the seconds inside it, counting down
                t += 0.5;
                logic.ObserveAction(me, ActIdle, t, in r);
                double pause = logic.PlayerTimer.Pause;
                view.Tick(Frame(t, me.Agent));
                Check(vm.Fill == 0f && vm.SecondsShown && vm.SecondsText == AttackTimerMath.CountdownText(pause) && view.PausesShown == 1,
                    "the pause's start: fill " + vm.Fill + ", \"" + vm.SecondsText + "\" (" + pause + " s)");
                LogHas("[hud] recovery bar: first pause shown at ");
                LogHas(" - empty at your attack, now refilling over 0.57 s (D 0.82 s at attack speed x0.59), \"0.6 s\" inside it, counting down");
                view.Tick(Frame(t + pause / 2, me.Agent));
                Check(Math.Abs(vm.Fill - 0.5f) < 0.01f && vm.SecondsText == "0.3 s", "halfway: " + vm.Fill + " \"" + vm.SecondsText + "\"");
                logic.GateFrame(me, t + pause, false);                 // the gate ends it at its time
                view.Tick(Frame(t + pause + 0.02, me.Agent));
                Check(vm.Fill == 1f && !vm.SecondsShown && vm.FillColor == RecoveryColors.Ready, "the bar is not full again after the pause");

                // FlashBarOnEarlyAttack off: a swallowed press flashes nothing
                t += pause + 0.1;
                S.Set(SettingsSchema.FlashBarOnEarlyAttack, false, SettingSources.Mcm);
                logic.ObserveAction(me, ActReady, t, in r);
                AthleticsLogic.ReadyFull(me, t + 0.32);
                t += 0.4;
                logic.ObserveAction(me, ActRelease, t, in r);
                t += 0.5;
                logic.ObserveAction(me, ActIdle, t, in r);
                logic.GateFrame(me, t + 0.01, false);
                logic.GateFrame(me, t + 0.1, true);
                view.Tick(Frame(t + 0.11, me.Agent));
                Check(!vm.FlashOn && view.FlashesShown == 1 && logic.RateStats.SwallowedInTimer == 1, "a flash with FlashBarOnEarlyAttack off");
                S.Set(SettingsSchema.FlashBarOnEarlyAttack, true, SettingSources.Mcm);

                // the layout is live (the refresh): 40 px above your bar's row
                S.Set(SettingsSchema.RecoveryBarOffsetAbove, 40, SettingSources.Mcm);
                t += 0.5;
                view.Tick(Frame(t, me.Agent));
                Check(vm.OffsetBottom == 94f, "the layout is not live: bottom " + vm.OffsetBottom + " (54 + 40)");
                S.Set(SettingsSchema.RecoveryBarOffsetAbove, 24, SettingSources.Mcm);

                // its switches: its own, the Athletics bar's, your pause's, the master switch - each removes it, back on rebuilds it
                void Removed(ParamDef p, string why)
                {
                    int built = layer.Created;
                    S.Set(p, false, SettingSources.Mcm);
                    t += 0.05;
                    view.Tick(Frame(t, me.Agent));
                    Check(!layer.Up, why + " did not remove the recovery bar");
                    S.Set(p, true, SettingSources.Mcm);
                    t += 0.05;
                    view.Tick(Frame(t, me.Agent));
                    Check(layer.Up && layer.Created == built + 1, why + " back on did not rebuild it");
                    vm = view.CurrentViewModel;
                }
                Removed(SettingsSchema.ShowAttackRecoveryBar, "ShowAttackRecoveryBar off");
                Removed(SettingsSchema.ShowPlayerBar, "ShowPlayerBar off (it belongs to the Athletics bar)");
                Removed(SettingsSchema.AttackRatePlayerTimer, "AttackRatePlayerTimer off (nothing to recover)");
                Removed(SettingsSchema.ModEnabled, "ModEnabled off");
                LogHas("[hud] recovery bar: layer removed at ");
                LogHas(" - your Athletics bar (ShowPlayerBar) or your pause (AttackRatePlayerTimer) is off");

                // the summary line
                view.Finish(t);
                var lines = view.Stats.SummaryLines();
                view.AddSummaryLines(lines);
                Check(lines.Exists(l => l.StartsWith("hud: recovery bar - pauses shown 2 (avg ", StringComparison.Ordinal)
                                        && l.EndsWith(" on screen not full; flashes shown 1 (FlashBarOnEarlyAttack on at the end) - your pauses and swallowed presses are in the attack rate lines", StringComparison.Ordinal)),
                    "the summary line: " + string.Join(" // ", lines));
                logic.SmokePlayer = null;
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
                HudDefaults();
                StepBackDefaults();
                ConfigStore.Reload("after the recovery bar smoke"); // the file's values back (the MCM steps build on them)
            }
        }
    }
}

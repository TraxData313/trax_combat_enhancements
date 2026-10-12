using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Hud;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Step 32 (Anton, 2026-10-12 - "chain swings stack"): the swing stack through the REAL logic - you (the fake agent stands in
    /// for Mission.MainAgent): a burst of stored swings with no hold, the hold from the last one's release, the countdown to the
    /// next stored swing (the bar's count, fill and seconds), the kick key refused at 0, hold-to-attack the moment a swing is
    /// back, saving up and bursting again, the switches; the AI by input: a swing from the stack never held (his own gap does
    /// not refill), at 0 held exactly until the next comes back, his kick taken out of his input meanwhile, a kick that spends the
    /// last one holds him too; the master switch; the summary; the recovery bar's view; the cost over a thousand men.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>Step 32 off for the older steps (they check one pause after EVERY attack).</summary>
        private static void SwingStackOff() => S.Set(SettingsSchema.SwingStackEnabled, false, SettingSources.File);

        /// <summary>Step 32 on with DESIGN's initial numbers (20 points a swing, at least 2, kicks spend one).</summary>
        private static void SwingStackOn()
        {
            S.Set(SettingsSchema.SwingStackEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.SwingStackPointsPerSwing, 20, SettingSources.File);
            S.Set(SettingsSchema.SwingStackMinimum, 2, SettingSources.File);
            S.Set(SettingsSchema.KicksUseSwings, true, SettingSources.File);
        }

        /// <summary>The pause of a 0.82 s attack at the attack multiplier of a <paramref name="fraction"/> share of the bar.</summary>
        private static double StackPause(double fraction)
        {
            double f = Math.Min(1.0, fraction / 0.75);
            return AttackTimerMath.Pause(0.82, AthleticsMath.Curve(0.2f, f));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SwingStackThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                StepBackDefaults();
                SwingStackOn();
                S.Set(SettingsSchema.StepBackEnabled, false, SettingSources.File);
                var b = NewRateLogic(new FakePaceBody());
                LogHas("[rate] swing stack (step 32) at mission start: ON - every fighter stores swings: max(2, his Athletics points / 20, rounded) (SwingStackMinimum 2, SwingStackPointsPerSwing 20 - a full 150 pool holds 8, empty 2)");
                var timer = b.PlayerTimer;
                var stats = b.StackStats;
                double t = 12000;
                var me = Wounded(b, 300, 40f, t);   // capped at 40% of a 300 bar: 120 points → 6 stored swings
                me.AthleticsSkill = 300;
                b.SmokePlayer = me.Agent;
                var r = Rules;

                // ---- A: you - a burst of six with no hold at all (stored swings), the cap following the points down
                Check(SwingStackMath.Cap(SwingStackRules.From(S), r, me) == 6, "A: 120 points do not hold 6 swings");
                for (int i = 1; i <= 5; i++)
                {
                    SwingOnce(b, me, ref t);
                    Check(!timer.Holding, "A: held after swing " + i + " with stored swings left");
                    var g = b.GateFrame(me, t, true);
                    Check(!g.Clear, "A: the attack button was cleared with stored swings left");
                }
                Check(me.Swings.Count(4) == 1 && me.Swings.Run == 5 && stats.Spent(true, SpendResult.FromStack) == 5,
                    "A: after five swings: " + me.Swings.Stored + " stored, run " + me.Swings.Run);
                LogHas("[athletics] YOU: first burst this battle at ");
                LogHas(" - a second attack straight after the last from your stored swings (4/6 left; one comes back per ");
                Check(AthleticsLogic.TryGetPlayerRecovery(me.Agent, t, out var mid) && mid.StackOn && mid.Stored == 1 && mid.Cap == 4 && mid.Recovering && mid.CountText == "1/4",
                    "A: the bar's count after five: " + mid.CountText);

                // the sixth spends the last: the hold from its release (no chained blow), the countdown to the next stored swing
                var rr = Rules;
                b.ObserveAction(me, ActReadyMeleeCode, t, in rr);
                AthleticsLogic.ReadyFull(me, t + 0.32);
                t += 0.4;
                b.ObserveAction(me, ActRelease, t, in rr);
                Check(timer.InAttack && me.Swings.Count(3) == 0, "A: the last swing's release did not start the hold");
                LogHas("[athletics] YOU: swing stack empty at ");
                LogHas(" - you spent your last stored swing (1 → 0 of 4 on 70 points) on an attack; the next comes back in ");
                t += 0.5;
                double end = t;
                b.ObserveAction(me, ActIdle, t, in rr);
                double p = StackPause(0.2);         // 60 points of 300: f 0.27, m 0.41 → 1.16 s
                Check(timer.Running && b.PlayerStackWait && Near(timer.Pause, p) && Near(timer.TimerEnd, end + p),
                    "A: the countdown to the next stored swing: " + timer.Pause + " (" + p + ")");
                LogHas(" s - your swing stack is empty: the next stored swing comes back after that much time not attacking (D x (1/m - 1) at your current m), until ");

                // the kick key at 0: refused (on foot), never on a horse; a press swallowed
                Check(b.KickGate(me, end + 0.1, true, mounted: false) && stats.KicksRefusedYou == 1, "A: a kick at 0 was not refused");
                Check(!b.KickGate(me, end + 0.1, true, mounted: true) && !b.KickGate(me, end + 0.1, false, mounted: false), "A: the kick gate touched a rider or no kick");
                LogHas("[athletics] YOU: kick pressed at ");
                LogHas(" with no stored swing (0/3) - refused: the engine never saw it (KicksUseSwings)");
                Check(AthleticsLogic.TryGetPlayerRecovery(me.Agent, end + p / 2, out var half) && half.Stored == 0 && Math.Abs(half.Share - 0.5f) < 0.01f && half.SecondsText.Length > 0,
                    "A: halfway the bar reads " + half.Share + " \"" + half.SecondsText + "\"");
                b.GateFrame(me, end + 0.2, false);
                var sw = b.GateFrame(me, end + p / 2, true);
                Check(sw.Clear && sw.Swallowed, "A: a press at 0 was not swallowed");

                // hold-to-attack: the moment the swing is back the held button goes through
                var fe = b.GateFrame(me, end + p + 0.001, true);
                Check(fe.Ended && fe.HeldAtEnd && !fe.Clear && !timer.Holding && me.Swings.Count(3) == 1 && stats.Waits(true) == 1,
                    "A: the held button did not fire when the swing came back (" + me.Swings.Stored + " stored)");
                Check(!b.KickGate(me, end + p + 0.01, true, false), "A: a kick was refused with a stored swing");

                // ---- B: save up, then burst again: three swings back after 3.2 pauses of rest (the cap 3), a burst of three
                t = end + p + 0.001 + 2.2 * p;
                for (int i = 1; i <= 3; i++)
                {
                    SwingOnce(b, me, ref t);
                    if (i < 3) Check(!timer.Holding, "B: held during the saved-up burst at swing " + i);
                }
                Check(timer.Running && stats.Spent(true, SpendResult.AfterWaiting) == 1 && stats.Spent(true, SpendResult.FromStack) == 8,
                    "B: the saved-up burst: from the stack " + stats.Spent(true, SpendResult.FromStack) + ", after waiting " + stats.Spent(true, SpendResult.AfterWaiting));

                // ---- C: the switches - off = step 13's pause after every attack; the master switch: no hold, no gate, fresh again
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                b.ApplySettingsChange(S);
                b.TickPlayerTimer(t + 0.05);
                Check(!timer.Holding && !b.PlayerStackWait && !b.KickGate(me, t + 0.06, true, false), "C: ModEnabled off left a hold or the kick gate on");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                b.ApplySettingsChange(S);
                Check(me.Swings.Stored == SwingStackMath.Full, "C: back on, the stack is not full again");
                SetHealth(me.Agent, 40f, 100f);
                b.CheckHealth(me, Rules, t);
                me.SpeedDirty = false;
                t += 1;
                SwingStackOff();
                b.ApplySettingsChange(S);
                LogHas("[rate] swing stack switched OFF mid-mission at ");
                SwingOnce(b, me, ref t);
                Check(timer.Running && !b.PlayerStackWait && me.Swings.Stored == SwingStackMath.Full, "C: off: no step-13 pause after the swing, or the stack moved");
                b.TickPlayerTimer(timer.TimerEnd + 0.01);
                b.GateFrame(me, timer.TimerEnd + 0.01, false);
                SwingStackOn();
                b.ApplySettingsChange(S);
                LogHas("[rate] swing stack switched ON mid-mission at ");
                b.SmokePlayer = null;

                // ---- D: the AI by input - a swing from the stack never held, his own gap not refilling; at 0 held until the next
                var facts = new FakeWeaponFacts();
                var input = new FakePaceBody();
                S.Set(SettingsSchema.AttackRatePaceByInput, true, SettingSources.File);
                var logic = NewPaceLogic(input, facts);
                var ai = logic.StackStats;
                t = 13000;
                var y = Wounded(logic, 500, 40f, t);   // the floor 50 at 40%: 20 points → the minimum 2
                SwingOnce(logic, y, ref t);
                double e1 = t;
                logic.TickPace(t);
                Check((y.Pace == null || !y.Pace.Active) && y.Swings.Count(2) == 1 && Near(y.Swings.GraceUntil, e1 + 1.0),
                    "D: a swing from the stack was held, or his own gap (1.0 s) not set: " + y.Swings.GraceUntil);
                double pa = StackPause(0.2);           // 10 points of 50
                logic.StackPass(e1 + 1.0, 0.1);
                Check(Near(y.Swings.Progress, 0), "D: his own gap refilled: " + y.Swings.Progress);
                logic.StackPass(e1 + 1.0 + pa / 2, 0.1);
                Check(Math.Abs(y.Swings.Progress - 0.5) < 1e-6, "D: half a pause after his gap is not half a swing: " + y.Swings.Progress);
                t = e1 + 1.0 + pa / 2;
                SwingOnce(logic, y, ref t);            // spends the last: 0 points now (m 0.2)
                double e2 = t;
                logic.TickPace(t);
                double wait = 0.5 * StackPause(0.0);    // half a swing already refilled, the rest at m 0.2: 0.5 x 3.28
                Check(y.Pace != null && y.Pace.Active && y.Pace.ByInput && y.Pace.StackWait && y.Input!.HoldAttacks && y.Input.HoldKicks && Near(y.Pace.Pause, wait),
                    "D: at 0 the AI timer did not hold him by input for the rest of the refill: " + (y.Pace == null ? "none" : y.Pace.Pause + " (" + wait + ")"));
                Check(AiInputHook.KickEventBit == 0x8000u && AiInputHook.KickEventBit == (uint)Agent.EventControlFlag.Kick, "D: the kick bit is not the game's EventControlFlag.Kick");
                uint ev = AiInputHook.KickEventBit | 0x1u;
                Check(AiInputHook.HoldKick(y.Input!, ref ev) && ev == 0x1u && y.Input!.KicksCleared == 1, "D: his kick was not taken out of his input");
                Check(InputFrame(y, MvAttackDown) == MvDefendDown, "D: his attack wish was not replaced by a guard at 0");
                logic.TickPace(e2 + wait - 0.2);
                Check(y.Pace.Active, "D: the wait ended early");
                logic.TickPace(e2 + wait + 0.01);
                Check(!y.Pace.Active && !y.Input.HoldAttacks && !y.Input.HoldKicks && y.Swings.Count(2) == 1 && ai.Waits(false) == 1 && ai.KicksRefusedAi == 1,
                    "D: the wait did not end when his swing came back");
                t = e2 + wait + 0.02;
                SwingOnce(logic, y, ref t);
                logic.TickPace(t);
                Check(ai.Spent(false, SpendResult.AfterWaiting) == 1 && y.Pace.Active && Math.Abs(y.Pace.Pause - StackPause(0.0)) < 0.05, "D: the swing after the wait / the next full wait");

                // a kick spends one too; a kick that spends the last starts his wait; KicksUseSwings off = free
                var z = Wounded(logic, 501, 40f, t);
                var rz = Rules;
                logic.ObserveAction(z, KickBashTracker.ActionKick, t, in rz);
                logic.ObserveAction(z, ActIdle, t + 0.3, in rz);
                Check(z.Swings.Count(2) == 1 && ai.KicksSpent == 1, "D: a kick did not spend a stored swing");
                logic.ObserveAction(z, KickBashTracker.ActionKick, t + 0.4, in rz);
                logic.TickPace(t + 0.4);
                Check(z.Pace != null && z.Pace.Active && z.Pace.StackWait && ai.KicksSpent == 2, "D: the kick that spent his last swing did not start his wait");
                S.Set(SettingsSchema.KicksUseSwings, false, SettingSources.Mcm);
                var k = Wounded(logic, 502, 40f, t);
                logic.ObserveAction(k, KickBashTracker.ActionKick, t, in rz);
                Check(k.Swings.Count(2) == 2 && ai.KicksSpent == 2, "D: KicksUseSwings off and a kick still spent a swing");
                S.Set(SettingsSchema.KicksUseSwings, true, SettingSources.Mcm);

                // the master switch lifts every wait at once
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                logic.TickPace(t + 0.5);
                Check(!y.Pace.Active && !z.Pace.Active, "D: ModEnabled off left an AI wait running");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                Check(y.Swings.Stored == SwingStackMath.Full && z.Swings.Stored == SwingStackMath.Full, "D: back on, the AI stacks are not full");

                // the summary
                typeof(AthleticsLogic).GetMethod("WriteSwingStackSummary", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(b, null);
                LogHas("[summary] swing stack: ON - every fighter stores swings");
                LogHas("[summary] swing stack - you: attacks 9 (from the stack 8, after waiting at 0 1, at an empty stack 0); bursts (2+ in a row, no swing back between) 2 by length 3: 1, 5-6: 1, longest 6; waits at 0 1 (avg ");
                LogHas("[summary] swing stack - kicks and bashes (KicksUseSwings on at the end): spent a swing 0 (you 0), refused at 0 - yours 1 (the gate took the kick key out)");
                LogHas("[summary] swing stack - average fill (stored ÷ his cap) by f: ");
                typeof(AthleticsLogic).GetMethod("WriteSwingStackSummary", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(logic, null);
                LogHas("[summary] swing stack - AI: attacks 3 (from the stack 2, after waiting at 0 1, at an empty stack 0)");
                LogHas("spent a swing 2 (you 0), refused at 0 - yours 0 (the gate took the kick key out), the AI's 1 input frames");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
                StepBackDefaults();
            }
        }

        /// <summary>Step 32 through the real Attack recovery bar view: "Swings 6/6" at rest, the count down a burst, "Swings 0/3"
        /// amber with the seconds inside while you wait, half full halfway, "Swings 1/3" when one is back; the summary clause.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RecoveryBarShowsTheStack()
        {
            var keep = _logic;
            try
            {
                var logic = HudLogic();
                SwingStackOn();
                S.Set(SettingsSchema.ShowAttackRecoveryBar, true, SettingSources.File);
                S.Set(SettingsSchema.StepBackEnabled, false, SettingSources.File);
                logic.PaceBody = new FakePaceBody();
                logic.StepBackBody = new FakeStepBody { Allow = false };
                var me = logic.Track(FakeAgent(410))!;
                me.AthleticsSkill = 300;
                logic.SmokePlayer = me.Agent;
                SetHealth(me.Agent, 40f, 100f);
                logic.CheckHealth(me, Rules, 0);
                me.SpeedDirty = false;
                var view = new AttackRecoveryView();
                var layer = new FakeHudLayer();
                view.UseLayer(layer);
                double t = 7500;
                view.Tick(Frame(t, me.Agent));
                var vm = view.CurrentViewModel;
                Check(layer.Up && vm != null && vm.LabelText == "Swings 6/6" && vm.Fill == 1f && vm.FillColor == RecoveryColors.Ready && !vm.SecondsShown,
                    "at rest the bar does not read \"Swings 6/6\", full: " + (vm == null ? "no VM" : "\"" + vm.LabelText + "\" " + vm.Fill));
                LogHas("[hud] recovery bar: first values pushed at ");
                LogHas(" - swing stack 6/6, full (no pause running)");
                for (int i = 0; i < 6; i++) SwingOnce(logic, me, ref t);
                view.Tick(Frame(t, me.Agent));
                double p = StackPause(0.2);
                Check(vm!.LabelText == "Swings 0/3" && vm.Fill == 0f && vm.FillColor == RecoveryColors.Recovering && vm.SecondsShown && vm.SecondsText == AttackTimerMath.CountdownText(p),
                    "empty: \"" + vm.LabelText + "\" fill " + vm.Fill + " \"" + vm.SecondsText + "\"");
                LogHas("[hud] recovery bar: first empty swing stack shown at ");
                view.Tick(Frame(t + p / 2, me.Agent));
                Check(Math.Abs(vm.Fill - 0.5f) < 0.01f && vm.LabelText == "Swings 0/3", "halfway: " + vm.Fill);
                logic.GateFrame(me, t + p + 0.001, false);
                view.Tick(Frame(t + p + 0.01, me.Agent));
                Check(vm.LabelText == "Swings 1/3" && vm.Fill < 0.05f && vm.FillColor == RecoveryColors.Recovering && !vm.SecondsShown,
                    "one back: \"" + vm.LabelText + "\" fill " + vm.Fill + " seconds " + vm.SecondsShown);
                SwingStackOff();
                view.Tick(Frame(t + p + 0.02, me.Agent));
                Check(vm.LabelText == AttackTimerMath.RecoveryLabelText, "off: the label is not \"Attack recovery\" again: " + vm.LabelText);
                view.Finish(t + p + 0.1);
                var lines = view.Stats.SummaryLines();
                view.AddSummaryLines(lines);
                Check(lines.Exists(l => l.Contains("; swing stack shown, fewest stored 0 - ")), "the summary clause: " + string.Join(" // ", lines));
                logic.SmokePlayer = null;
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
                HudDefaults();
                StepBackDefaults();
                ConfigStore.Reload("after the swing stack bar smoke");
            }
        }

        /// <summary>Step 32's cost: a thousand AI men waiting at 0 for their next stored swing - the AI timer's tick (every man's clock
        /// moved each tick) and the regen pass's stack part over all of them stay small and allocate nothing.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SwingStackCostStaysFlat()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                StepBackDefaults();
                SwingStackOn();
                S.Set(SettingsSchema.StepBackEnabled, false, SettingSources.File);
                S.Set(SettingsSchema.AttackRatePaceByInput, true, SettingSources.File);
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                const int men = 1000;
                double t0 = 20000;
                for (int i = 0; i < men; i++)
                {
                    var st = Wounded(logic, 3000 + i, 40f, t0);
                    double t = t0;
                    SwingOnce(logic, st, ref t);
                    SwingOnce(logic, st, ref t);
                    logic.TickPace(t);
                }
                Check(logic.HeldNow == men, "cost: not every man waits at 0: " + logic.HeldNow);
                double t1 = t0 + 1.8;
                const int ticks = 500;
                logic.TickPace(t1);
                logic.StackPass(t1, 0.1);
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var sw = Stopwatch.StartNew();
                for (int k = 0; k < ticks; k++) logic.TickPace(t1 + 0.001 * k);
                sw.Stop();
                var sp = Stopwatch.StartNew();
                for (int k = 0; k < ticks; k++) logic.StackPass(t1 + 0.5 + 0.001 * k, 0.1);
                sp.Stop();
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double msTick = sw.Elapsed.TotalMilliseconds / ticks, msPass = sp.Elapsed.TotalMilliseconds / ticks;
                Console.WriteLine("        step 32 cost: " + men + " men waiting for a stored swing - the AI timer's tick " + msTick.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                                  + " ms, the stack pass " + msPass.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " ms, " + allocated + " bytes allocated over " + 2 * ticks + " calls");
                Check(logic.HeldNow == men, "cost: waits ended early");
                Check(msTick < 0.5 && msPass < 0.5, "cost: the tick took " + msTick + " ms, the pass " + msPass + " ms (budget 0.5 ms)");
                Check(allocated < 64 * 1024, "cost: the stack allocates - " + allocated + " bytes");
                logic.TickPace(t1 + 10);
                Check(logic.HeldNow == 0, "cost: the thousand waits did not end when their swings came back");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
                StepBackDefaults();
            }
        }
    }
}

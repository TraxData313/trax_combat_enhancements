using System;
using System.Runtime.CompilerServices;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline check for step 110 (Toni's training field 2026-11-10; DESIGN decision 99) through the REAL logic: (a) the TIRED trainer - no pause, swings
    /// stored, no brace, his bar 32% - backs off after his quiet 2.5 s and keeps the gap until his bar reaches 65% (ready), a man at 60% never starts,
    /// the mark 0 = step 109 (he stares); (b) the training field's fresh start - you and the trainer, tired, swings spent, sprint drained, a pause running
    /// -> full bar (to your health's cap), full stack, full sprint, the pause ended; the switch off = nobody, logged; the master switch first.
    /// </summary>
    internal static partial class Program
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TrainingFieldStep110()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                KeepDistanceDefaults();
                S.Set(SettingsSchema.KeepDistanceTiredBelowPercent, 50, SettingSources.File);
                S.Set(SettingsSchema.KeepDistanceTiredRecoverPercent, 15, SettingSources.File);
                S.Set(SettingsSchema.SwingStackEnabled, true, SettingSources.File);

                // ---- (a) the tired rookie trainer: he COULD swing, he only stares
                var w = new KeepWorld(11000);
                var you = w.Man(0, 0);
                w.Body.You = you.St.Agent;
                w.Logic.SmokePlayer = you.St.Agent;
                w.Logic.SmokePlayerAiControlled = false;
                var a = w.Man(0, 1.0);
                a.Target = you.St;
                you.Target = a.St;
                SetBar(a.St, 0.32);
                a.St.LastReleaseTime = w.T;
                var ks = w.Logic.KeepStats;
                Check(a.St.Pace == null || !a.St.Pace.Active, "the tired trainer is in a pause - the test needs a man who could swing");
                double began = w.RunUntil(() => a.St.Keep.Keeping, 5);
                Check(!double.IsNaN(began) && began >= 2.45 && began <= 2.8 && ks.TiredKeeps == 1,
                    "the tired man who could swing did not back off after his quiet 2.5 s: " + began + " s, tired keeps " + ks.TiredKeeps);
                LogHas("first TIRED man near his enemy who could swing (step 110)");
                LogHas("tired - bar 32.0%, he could swing (step 110)");
                w.RunUntil(() => a.St.Keep.Phase == KeepPhase.Holding, 4);
                double gap = w.Gap(a, you);
                Check(gap >= 2.7 && gap <= 3.05, "the tired man did not hold at ~2.9 m: " + gap);
                SetBar(a.St, 0.60); // above the mark, below the ready mark: he keeps waiting
                for (int i = 0; i < 30; i++) w.Frame();
                Check(a.St.Keep.Keeping, "a tired keep ended at 60% (ready only at 65%)");
                SetBar(a.St, 0.66);
                double ready = w.RunUntil(() => !a.St.Keep.Keeping, 1);
                Check(!double.IsNaN(ready) && ks.Ends[(int)KeepEnd.Ready] >= 1, "at 66% he was not released ready: " + ready);
                // a man at 60% (above the mark) never starts
                var b = new KeepWorld(11100);
                var you2 = b.Man(0, 0);
                b.Body.You = you2.St.Agent;
                b.Logic.SmokePlayer = you2.St.Agent;
                b.Logic.SmokePlayerAiControlled = false;
                var c = b.Man(0, 1.0);
                c.Target = you2.St;
                SetBar(c.St, 0.60);
                b.RunUntil(() => c.St.Keep.Keeping, 5);
                Check(!c.St.Keep.Keeping && b.Logic.KeepStats.Keeps == 0, "a man at 60% (above the 50% mark) kept his distance");
                // the mark 0 = step 109: the tired man who could swing stares
                S.Set(SettingsSchema.KeepDistanceTiredBelowPercent, 0, SettingSources.File);
                SetBar(c.St, 0.20);
                b.RunUntil(() => c.St.Keep.Keeping, 5);
                Check(!c.St.Keep.Keeping, "KeepDistanceTiredBelowPercent 0 still kept a man who could swing");
                S.Set(SettingsSchema.KeepDistanceTiredBelowPercent, 50, SettingSources.File);

                // ---- (b) the training field's fresh start
                var t = new KeepWorld(11200);
                var me = t.Man(0, 0);
                t.Logic.SmokePlayer = me.St.Agent;
                t.Logic.SmokePlayerAiControlled = false;
                var trainer = t.Man(0, 1.5);
                t.Logic.TrainingActiveOf = st => true;
                SetHealth(me.St.Agent, 100, 100);
                SetHealth(trainer.St.Agent, 100, 100);
                SetBar(me.St, 0.05);
                SetBar(trainer.St, 0.30);
                var ar = AthleticsRules.From(S);
                var sr = SwingStackRules.From(S);
                int capT = SwingStackMath.Cap(in sr, in ar, trainer.St);
                while (trainer.St.Swings.Count(capT) > 0) trainer.St.Swings.Spend(capT, out _);
                typeof(SprintStore).GetProperty("Stored")!.GetSetMethod(true)!.Invoke(me.St.Sprint, new object[] { 0.5 });
                KeepWorld.Pause(trainer, true);
                var tr = TrainingFieldRefillRules.From(S);
                Check(tr.On, "TrainingFieldRefill does not ship ON");
                t.Logic.TrainingMoment(TrainingRefillWhy.HealthReset, t.T, in tr, 0.4, 1.0);
                var ts = t.Logic.TrainingFieldStats;
                Check(Math.Abs(me.St.Fraction - 1) < 1e-9 && Math.Abs(trainer.St.Fraction - 1) < 1e-9,
                    "the fresh start did not fill both bars: you " + me.St.Fraction + ", the trainer " + trainer.St.Fraction);
                Check(trainer.St.Swings.Count(SwingStackMath.Cap(in sr, in ar, trainer.St)) == SwingStackMath.Cap(in sr, in ar, trainer.St), "the trainer's swing stack is not full");
                Check(double.IsPositiveInfinity(me.St.Sprint.Stored), "your sprint is not full: " + me.St.Sprint.Stored);
                Check(!trainer.St.Pace!.Active, "the trainer's pause did not end at the fresh start");
                Check(ts.Refills == 1 && ts.MenRefilled == 2 && ts.YouRefilled == 1, "the stats: refills " + ts.Refills + ", men " + ts.MenRefilled + ", you " + ts.YouRefilled);
                LogHas("training field (step 110) at ");
                LogHas("the game reset your health (40% → 100%) - a fresh start for 2 men: YOU ");
                // your wounds still cap it
                SetHealth(me.St.Agent, 60, 100);
                SetBar(me.St, 0.1);
                t.Logic.TrainingMoment(TrainingRefillWhy.BoutBegan, t.T + 5, in tr, double.NaN, double.NaN);
                Check(Math.Abs(me.St.Fraction - 0.6) < 1e-6, "the fresh start went past your health's cap: " + me.St.Fraction);
                // the switch off: nobody, logged
                S.Set(SettingsSchema.TrainingFieldRefill, false, SettingSources.Mcm);
                SetBar(me.St, 0.1);
                var off = TrainingFieldRefillRules.From(S);
                t.Logic.TrainingMoment(TrainingRefillWhy.BoutEnded, t.T + 10, in off, double.NaN, double.NaN);
                Check(Math.Abs(me.St.Fraction - 0.1) < 1e-9 && ts.Off == 1, "TrainingFieldRefill off still refilled: " + me.St.Fraction);
                LogHas("nobody refilled (TrainingFieldRefill is off)");
                S.Set(SettingsSchema.TrainingFieldRefill, true, SettingSources.Mcm);
                // the master switch first
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                var mod = TrainingFieldRefillRules.From(S);
                Check(mod.OffBecause == "ModEnabled", "the master switch is not the training refill's first gate: " + mod.OffBecause);
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                t.Logic.WriteTrainingFieldSummary(); // not a training field mission: no line
                ts.IsTrainingField = true;
                t.Logic.WriteTrainingFieldSummary();
                LogHas("training field fresh start (step 110) ON: moments - your health reset 1, bouts began 1, bouts ended 1; fresh starts 2");
            }
            finally
            {
                S.Set(SettingsSchema.KeepDistanceEnabled, false, SettingSources.File);
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
            }
        }
    }
}

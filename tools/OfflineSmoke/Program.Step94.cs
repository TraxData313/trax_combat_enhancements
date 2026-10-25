using System;
using System.Runtime.CompilerServices;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 94 - A BIGGER BASE POOL, CHEAPER SWINGS (Toni 2026-10-25; AI_NOTES "Step 94", DESIGN decision 86): the SHIPPED
    /// AthleticsPoolBase 100, AthleticsPoolPerSkill 0.5, CostPerBlow 7 and CostPerKickOrBash 6 from the embedded defaults.json, through the REAL
    /// logic - a recruit (skill 20) gets 110 points and swings 15 times from full before the 16th empties him; a 233-skill hero gets 216.5 and
    /// swings it down at his multiplied cost; a T6 rider with 60 is lifted from 130 to his tier's 150, a hero with 60 keeps 130; a live base change re-pools everyone at
    /// his share; the master switch takes the base away; the mission-start, settings and [summary] lines say the new formula.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>Step 94: the step 81 / 88 grinds assert step 81's costs (a swing 10, a kick 8) - the shipped ones are 7 / 6 since step 94.</summary>
        private static void Step81Costs()
        {
            S.Set(SettingsSchema.CostPerBlow, 10.0, SettingSources.File);
            S.Set(SettingsSchema.CostPerKickOrBash, 8.0, SettingSources.File);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void BiggerBasePoolThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults(); // DESIGN's step-5c numbers ...
                foreach (var p in new[] { SettingsSchema.AthleticsPoolBase, SettingsSchema.AthleticsPoolPerSkill, SettingsSchema.CostPerBlow, SettingsSchema.CostPerKickOrBash })
                    S.Set(p, p.Default, SettingSources.File); // ... with step 94's shipped values
                Check(S.AthleticsPoolBase == 100 && Near(S.AthleticsPoolPerSkill, 0.5f) && Near(S.CostPerBlow, 7f) && Near(S.CostPerKickOrBash, 6f),
                    "the shipped step-94 values are not 100 / 0.5 / 7 / 6: " + S.AthleticsPoolBase + " / " + S.AthleticsPoolPerSkill + " / " + S.CostPerBlow + " / " + S.CostPerKickOrBash);
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                LogHas("[athletics] mission start: ON - pool = 100 + the Athletics skill x0.50, at least 50; full strength at 75% of the pool and above; cost per blow 7.0 / hero 5.3 / party leader 3.9 points; a kick or shield bash 6.00 / hero 4.50 / party leader 3.38 points");

                TrackedAgent Man(int index, int level, bool hero, int skill)
                {
                    var m = logic.Track(CharacterAgent(index, level, hero))!;
                    m.AthleticsSkill = skill;
                    return m;
                }

                double PoolOf(TrackedAgent m) => AthleticsLogic.TryGetReading(m.Agent, out var r) ? r.Pool : double.NaN;

                var recruit = Man(9400, 6, false, 20);   // a T1 recruit
                var hero = Man(9401, 31, true, 233);     // a big-skill lord
                var knight = Man(9402, 31, false, 60);   // the T6 Banner Knight
                var lord60 = Man(9403, 31, true, 60);    // a lord with 60
                var champion = Man(9404, 31, false, 140);
                Check(PoolOf(recruit) == 110, "a recruit with skill 20 did not get 110: " + PoolOf(recruit));
                Check(Near(PoolOf(hero), 216.5), "a hero with 233 did not get 216.5: " + PoolOf(hero));
                Check(PoolOf(knight) == 150, "the T6 knight (130 by the formula) was not lifted to 150: " + PoolOf(knight));
                Check(PoolOf(lord60) == 130, "a hero with 60 did not keep the formula's 130: " + PoolOf(lord60));
                Check(PoolOf(champion) == 170, "a champion with 140 did not get 170: " + PoolOf(champion));

                // a recruit from full: 15 swings at 7 fit, the 16th empties him
                double t = 9400;
                int swings = 0;
                for (int i = 0; i < 15; i++) { Swing(recruit, ref t); swings++; }
                Check(!recruit.Exhausted && Near(recruit.Fraction * 110, 5) && recruit.Blows == 15,
                    "15 swings did not leave a recruit 5 of 110: " + recruit.Fraction * 110 + " points, blows " + recruit.Blows);
                Swing(recruit, ref t);
                Check(recruit.Exhausted && recruit.Fraction == 0 && recruit.Blows == 16, "the 16th swing did not empty the recruit: " + recruit.Fraction);
                Console.WriteLine("        step 94: a recruit (skill 20) 110 points, " + swings + " swings from full at 7, the 16th empties him");

                // a hero pays 7 x his multipliers (a lord here: hero x party leader, 3.94): his 216.5 empties on the swing the division says
                double cost = AthleticsMath.BlowCostPoints(Rules, hero);
                int fit = (int)Math.Ceiling(216.5 / cost) - 1;
                Check(Near(cost, 5.25) || Near(cost, 3.9375), "a hero's swing costs " + cost);
                for (int i = 0; i < fit; i++) Swing(hero, ref t);
                Check(!hero.Exhausted && Near(hero.Fraction * 216.5, 216.5 - fit * cost), fit + " hero swings: " + hero.Fraction * 216.5);
                Swing(hero, ref t);
                Check(hero.Exhausted, "swing " + (fit + 1) + " did not empty the hero");
                Console.WriteLine("        step 94: the 233 hero's 216.5 at " + cost.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " a swing - " + fit + " swings fit, swing " + (fit + 1) + " empties him");

                // a live base change re-pools the men on the field - each keeps his share
                SetBar(lord60, 0.6);
                S.Set(SettingsSchema.AthleticsPoolBase, 0, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                Check(AthleticsLogic.TryGetReading(lord60.Agent, out var c0) && c0.Pool == 50 && Near(c0.Points, 30) && Near(lord60.Fraction, 0.6),
                    "base 0 live did not re-pool the lord at his 60%: pool " + c0.Pool + ", points " + c0.Points);
                Check(PoolOf(knight) == 150 && PoolOf(champion) == 150 && PoolOf(recruit) == 50,
                    "base 0: the tier floor / the 50 floor: " + PoolOf(knight) + " / " + PoolOf(champion) + " / " + PoolOf(recruit));
                LogHas("[athletics] pool settings now: the Athletics skill x0.50, at least 50 - pools now min ");
                S.Set(SettingsSchema.AthleticsPoolBase, 100, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                Check(PoolOf(lord60) == 130 && Near(lord60.Fraction, 0.6) && PoolOf(champion) == 170, "base back to 100");
                LogHas("[athletics] pool settings now: 100 + the Athletics skill x0.50, at least 50 - pools now min ");

                // the master switch first: off = no base; back on = the base again
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                Check(PoolOf(champion) == 70 && PoolOf(lord60) == 50, "ModEnabled off kept the base (and the tier floor): " + PoolOf(champion) + " / " + PoolOf(lord60));
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                Check(PoolOf(champion) == 170 && PoolOf(lord60) == 130, "ModEnabled back on");

                logic.WriteAthleticsSummary();
                LogHas("[summary] Athletics settings at the end: ON - pool = 100 + the Athletics skill x0.50, at least 50;");
                LogHas("[summary] Athletics pools (100 + the Athletics skill x0.50, at least 50; settings at the end): ");
            }
            finally
            {
                AthleticsDefaults();
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
            }
        }
    }
}

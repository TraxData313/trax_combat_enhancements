using System;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;
using TraxCombat.Models;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 89 - TIRED MEN HIT SOFTER (Toni 2026-10-24; AI_NOTES "Step 89", DESIGN decision 82): through the REAL damage decorator
    /// over the game's CustomAgentApplyDamageModel and the REAL logic's tracked men - an empty attacker's 100 hit lands ~0.45 × 0.75, a fresh one's
    /// unchanged, a CHARGING empty one's unchanged (step 62's critical, m 1), the roll off still multiplied, arrows and horses too, never a shield
    /// block / a fall, Athletics off / the master switch off = no multiplier, 100 live = off, the log lines and the [summary] line.
    /// </summary>
    internal static partial class Program
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TiredMenHitSofterThroughTheDecorator()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                Step62On();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = new FakeImpactBody();
                logic.ImpactInputs = new FakeImpactInputs();
                double t = 89000;
                logic.TickImpact(t);
                TrackedAgent Man(int index, double bar)
                {
                    var m = logic.Track(FakeAgent(index))!;
                    m.AthleticsSkill = 125; // pool 100
                    SetBar(m, bar);
                    return m;
                }

                DamageDefaults();
                S.Set(SettingsSchema.DamageReductionPercent, 25, SettingSources.File);
                S.Set(SettingsSchema.SprintFullCritical, true, SettingSources.File);
                S.Set(SettingsSchema.TiredDamageAtEmptyPercent, 60, SettingSources.File);
                DamageRandomizer.Rng = ThreadSafeRandom.Shared;
                DamageRandomizer.OnMissionStart();
                LogHas("; tired men hit softer (TiredDamageAtEmptyPercent) 60% at an empty bar - the attacker's damage x E + (1 - E) x f after the roll, before the reduction (f 0.5 x0.80, empty x0.60; an empty man's average 100 hit lands 45 before the reduction (75 without it); a charging man's melee blow counts fresh; falls, objects and shield blocks never)");

                var empty = Man(8900, 0.0);
                var half = Man(8901, 0.375); // f 0.5 (the peak line at 75%)
                var fresh = Man(8902, 1.0);
                var stranger = FakeAgent(8903); // not tracked: x1

                // 1. THE AVERAGES (the roll 0.5-1.5 with the upside by f, then x m, then 25% less)
                var fromEmpty = Enumerable.Range(0, 4000).Select(_ => Hit(damage: 100f, attacker: empty.Agent)).ToList();
                var fromHalf = Enumerable.Range(0, 4000).Select(_ => Hit(damage: 100f, attacker: half.Agent)).ToList();
                var fromFresh = Enumerable.Range(0, 4000).Select(_ => Hit(damage: 100f, attacker: fresh.Agent)).ToList();
                var fromStranger = Enumerable.Range(0, 2000).Select(_ => Hit(damage: 100f, attacker: stranger)).ToList();
                Check(Math.Abs(fromEmpty.Average() - 33.75) < 0.6 && fromEmpty.Max() <= 45.01f && fromEmpty.Min() >= 22.49f && fromEmpty.Min() < 23.5f,
                    "an empty attacker's 100 hit: avg " + fromEmpty.Average() + " (~0.45 x 0.75 = 33.75), " + fromEmpty.Min() + ".." + fromEmpty.Max() + " (22.5..45)");
                Check(Math.Abs(fromHalf.Average() - 52.5) < 0.8 && fromHalf.Max() <= 75.01f, "f 0.5: avg " + fromHalf.Average() + " (~0.70 x 0.75 = 52.5), max " + fromHalf.Max());
                Check(Math.Abs(fromFresh.Average() - 75) < 1.0 && fromFresh.Max() > 111f, "a fresh attacker changed: avg " + fromFresh.Average() + ", max " + fromFresh.Max());
                Check(Math.Abs(fromStranger.Average() - 75) < 1.4 && fromStranger.Max() > 110f, "an untracked attacker changed: avg " + fromStranger.Average());
                LogHas("[damage] first tired hit this mission (step 89 - TiredDamageAtEmptyPercent 60) - melee on a person: ");
                LogHas(": attacker f 0.00 → x0.60: after the roll ");
                var st = DamageRandomizer.Stats;
                int nEmpty = st.TiredCount(3, out double mEmpty, out _), nHalf = st.TiredCount(1, out double mHalf, out _), nPeak = st.TiredCount(0, out _, out _);
                Check(nEmpty == 4000 && Math.Abs(mEmpty - 0.6) < 1e-9 && nHalf == 4000 && Math.Abs(mHalf - 0.8) < 1e-6 && nPeak == 4000
                      && st.TiredSkips(TiredSkipReason.NoPool) == 2000,
                    "the bands: empty " + nEmpty + " x" + mEmpty + ", half " + nHalf + " x" + mHalf + ", peak " + nPeak + ", no pool " + st.TiredSkips(TiredSkipReason.NoPool));

                // 2. THE CHARGE: an EMPTY man charging - the full critical and NO multiplier (his blow counts fresh)
                empty.SprintEndedAt = t - 0.2;
                var charged = Enumerable.Range(0, 4000).Select(_ => Hit(damage: 100f, attacker: empty.Agent)).ToList();
                Check(Math.Abs(charged.Average() - 75) < 1.0 && charged.Max() > 111f, "a charging empty man was reduced: avg " + charged.Average() + ", max " + charged.Max());
                Check(st.TiredCharges == 4000, "charges exempted " + st.TiredCharges);
                S.Set(SettingsSchema.SprintFullCritical, false, SettingSources.Mcm); // off: step 62's path closed - he is multiplied like anyone
                var chargedOff = Enumerable.Range(0, 2000).Select(_ => Hit(damage: 100f, attacker: empty.Agent)).ToList();
                Check(Math.Abs(chargedOff.Average() - 33.75) < 0.8, "SprintFullCritical off: a charging empty man not multiplied - avg " + chargedOff.Average());
                S.Set(SettingsSchema.SprintFullCritical, true, SettingSources.Mcm);
                empty.SprintEndedAt = double.NaN;

                // 3. THE ROLL OFF: still multiplied, exactly; arrows and horses too; a shield block / a fall / an object never
                S.Set(SettingsSchema.DamageRandomEnabled, false, SettingSources.File);
                Check(Hit(damage: 100f, attacker: empty.Agent) == 45f, "roll off: an empty man's 100 is not 45: " + Hit(damage: 100f, attacker: empty.Agent));
                Check(Hit(damage: 100f, attacker: half.Agent) == 60f, "roll off: f 0.5's 100 is not 60");
                Check(Hit(damage: 100f, attacker: fresh.Agent) == 75f, "roll off: a fresh man's 100 is not 75");
                Check(Hit(damage: 100f, missile: true, attacker: empty.Agent) == 45f && Hit(damage: 100f, mount: true, attacker: empty.Agent) == 45f,
                    "roll off: an empty man's arrow / blow on a horse not 45");
                Check(Hit(damage: 100f, shield: true, attacker: empty.Agent) == 75f, "a shield block was multiplied");
                Check(Hit(damage: 100f, fall: 6f, attacker: empty.Agent) == 100f && Hit(damage: 100f, agent: false, attacker: empty.Agent) == 100f, "a fall / an object was multiplied");
                Check(Hit(damage: 1.2f, attacker: empty.Agent) == 1f && Hit(damage: 0.4f, attacker: empty.Agent) == 0.4f, "never below 1 / 0 stays 0");

                // 4. LIVE: 100 = off (the roll's upside alone - an empty man's average 0.75 x 0.75); Athletics off; the master switch
                S.Set(SettingsSchema.TiredDamageAtEmptyPercent, 100, SettingSources.Mcm);
                LogHas("[config] TiredDamageAtEmptyPercent: 60 → 100 (source: MCM)");
                Check(Hit(damage: 100f, attacker: empty.Agent) == 75f, "100: the roll off, an empty man still multiplied");
                S.Set(SettingsSchema.DamageRandomEnabled, true, SettingSources.File);
                var off = Enumerable.Range(0, 4000).Select(_ => Hit(damage: 100f, attacker: empty.Agent)).ToList();
                Check(Math.Abs(off.Average() - 56.25) < 0.7 && off.Max() <= 75.01f, "100 = off: an empty man avg " + off.Average() + " (0.75 x 0.75 = 56.25)");
                S.Set(SettingsSchema.TiredDamageAtEmptyPercent, 60, SettingSources.Mcm);
                S.Set(SettingsSchema.DamageRandomEnabled, false, SettingSources.File);
                S.Set(SettingsSchema.AthleticsEnabled, false, SettingSources.Mcm);
                Check(Hit(damage: 100f, attacker: empty.Agent) == 75f, "Athletics off: still multiplied");
                S.Set(SettingsSchema.AthleticsEnabled, true, SettingSources.Mcm);
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                Check(Hit(damage: 100f, attacker: empty.Agent) == 100f, "mod off: not the game's value");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                Check(Hit(damage: 100f, attacker: empty.Agent) == 45f, "mod back on: not multiplied again");
                Check(st.TiredSkips(TiredSkipReason.SettingOff) >= 4001 && st.TiredSkips(TiredSkipReason.AthleticsOff) == 1 && st.TiredSkips(TiredSkipReason.ModOff) == 1
                      && st.TiredSkips(TiredSkipReason.ShieldBlock) == 1 && st.TiredSkips(TiredSkipReason.FallDamage) == 1 && st.TiredSkips(TiredSkipReason.NotAnAgent) == 1,
                    "skips: off " + st.TiredSkips(TiredSkipReason.SettingOff) + ", Athletics " + st.TiredSkips(TiredSkipReason.AthleticsOff) + ", mod " + st.TiredSkips(TiredSkipReason.ModOff)
                    + ", shield " + st.TiredSkips(TiredSkipReason.ShieldBlock));

                // 5. The summary
                DamageRandomizer.WriteSummary();
                LogHas("[summary] damage by tiredness (step 89 - TiredDamageAtEmptyPercent 60%: the attacker's damage x E + (1 - E) x f after the roll, before the reduction): peak (f 1) ");
                LogHas(" | empty (f 0) ");
                LogHas("; charging hits exempt (counted fresh, x1) 4000, landed avg ");
                LogHas("off (TiredDamageAtEmptyPercent 100) ");
                Console.WriteLine("        step 89: an empty man's 100 hit lands avg " + fromEmpty.Average().ToString("0.0", CultureInfo.InvariantCulture)
                                  + " (f 0.5 " + fromHalf.Average().ToString("0.0", CultureInfo.InvariantCulture) + ", fresh " + fromFresh.Average().ToString("0.0", CultureInfo.InvariantCulture)
                                  + ", an empty man CHARGING " + charged.Average().ToString("0.0", CultureInfo.InvariantCulture) + ", the setting at 100 " + off.Average().ToString("0.0", CultureInfo.InvariantCulture) + ")");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                ImpactModelDefaults();
                AthleticsDefaults();
                DamageDefaults();
                DamageRandomizer.OnMissionStart();
                ConfigStore.Reload("after the step 89 smoke");
            }
        }
    }
}

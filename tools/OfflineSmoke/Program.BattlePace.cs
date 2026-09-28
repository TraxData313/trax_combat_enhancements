using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 21 - BATTLE PACE (AI_NOTES "Step 21"): the REAL <see cref="AthleticsLogic"/> with stand-ins for
    /// the engine sides (the AI timer's input body, the weapon read). What runs for real: the class read at each release, the
    /// pause decision (the tired part + the class's share), the hold through the AI timer by input, the first-of-each-class and
    /// first-timer lines, the model's cycle, every class's counters, hot swap, the master switch, the summary lines - and the
    /// tick's cost with a thousand men held at once (fresh men are held now). What only the game can tell: that the AI really
    /// swings / shoots that much less - PLAYTEST's A/B battle and the [summary] "battle pace" lines.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>The smoke's stand-in for what a fighter holds: a shield per agent index, a ranged weapon per index (a
        /// bow unless set; a throwing release is "other").</summary>
        private sealed class FakeWeaponFacts : IWeaponFacts
        {
            public readonly HashSet<int> Shield = new HashSet<int>();
            public readonly Dictionary<int, RangedWeaponKind> Ranged = new Dictionary<int, RangedWeaponKind>();
            public int Reads;

            public void Read(TrackedAgent st, bool melee, bool throwing, out bool shieldInOffHand, out RangedWeaponKind ranged)
            {
                Reads++;
                shieldInOffHand = melee && Shield.Contains(st.AgentIndex);
                ranged = melee ? RangedWeaponKind.None
                    : throwing ? RangedWeaponKind.Other
                    : Ranged.TryGetValue(st.AgentIndex, out var k) ? k : RangedWeaponKind.Bow;
            }
        }

        private static FakeWeaponFacts? _facts;

        /// <summary>The stand-in every logic of the smoke reads through (set once, before the first logic is built).</summary>
        private static FakeWeaponFacts Facts => _facts ??= new FakeWeaponFacts();

        /// <summary>Step 21: the game's weapon read needs a native agent - the smoke's fake agents have none (a native read
        /// would take the process down), so every logic built from here on reads through the stand-in.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UseFakeWeaponFacts()
        {
            Check(WeaponFactsDefault.Current is GameWeaponFacts, "the default weapon read is not the game's: " + WeaponFactsDefault.Current.GetType().Name);
            WeaponFactsDefault.Current = Facts;
            Check(new AthleticsLogic().WeaponFacts == Facts, "a new logic does not start with the default weapon read");
        }

        /// <summary>Step 21 off for the older steps (they check steps 13-20 to the bit): DESIGN's own numbers are 30 / 15 / 2.0 / 2.5.</summary>
        private static void BattlePaceOff()
        {
            S.Set(SettingsSchema.ShieldInfantrySwingsLessPercent, 0, SettingSources.File);
            S.Set(SettingsSchema.FootMeleeSwingsLessPercent, 0, SettingSources.File);
            S.Set(SettingsSchema.AiMeleeGapSeconds, 1.0, SettingSources.File);
            S.Set(SettingsSchema.ExtraPauseAfterBowShotSeconds, 0, SettingSources.File);
            S.Set(SettingsSchema.ExtraPauseAfterCrossbowShotSeconds, 0, SettingSources.File);
        }

        private static void BattlePaceOn()
        {
            S.Set(SettingsSchema.ShieldInfantrySwingsLessPercent, 30, SettingSources.File);
            S.Set(SettingsSchema.FootMeleeSwingsLessPercent, 15, SettingSources.File);
            S.Set(SettingsSchema.AiMeleeGapSeconds, 1.0, SettingSources.File);
            S.Set(SettingsSchema.ExtraPauseAfterBowShotSeconds, 2.0, SettingSources.File);
            S.Set(SettingsSchema.ExtraPauseAfterCrossbowShotSeconds, 2.5, SettingSources.File);
        }

        private static AthleticsLogic NewPaceLogic(FakePaceBody input, FakeWeaponFacts facts)
        {
            var logic = new AthleticsLogic();
            _logic = logic;
            SetStatic(typeof(AthleticsLogic), "_current", logic);
            typeof(AthleticsLogic).GetMethod("StartAthletics", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(logic, null);
            logic.WeaponFacts = facts;
            logic.PaceBody = new FakePaceBody { Take = false }; // the old technique is never used here
            logic.PaceInputBody = input;
            logic.StepBackDice = new FixedDice(0.999);
            logic.StepBackBody = new FakeStepBody { Allow = false };
            logic.StepBackInputBody = new FakeStepBody { Allow = false, Input = true };
            return logic;
        }

        private static void Mount(TrackedAgent st, int horse)
        {
            _setMount ??= FieldSetter<Agent?>("_cachedMountAgent");
            _setMount(st.Agent, FakeAgent(horse));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void BattlePaceThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                StepBackDefaults();
                S.Set(SettingsSchema.StepBackEnabled, false, SettingSources.File);
                S.Set(SettingsSchema.AttackRatePaceByInput, true, SettingSources.File);
                BattlePaceOn();
                var facts = new FakeWeaponFacts();
                var input = new FakePaceBody();
                var logic = NewPaceLogic(input, facts);
                var rate = logic.RateStats;
                var pace = logic.PaceStats;
                LogHas("[rate] battle pace (step 21) at mission start: ON - AI shield infantry swing 30% less (ShieldInfantrySwingsLessPercent), other AI foot melee swing 15% less (FootMeleeSwingsLessPercent) - after each such swing a pause that stretches his expected cycle (his attack + his tired pause + his own gap AiMeleeGapSeconds 1.00 s) ÷ (1 - the share), at full strength or tired; AI bowmen wait 2.0 s more after each shot (ExtraPauseAfterBowShotSeconds), crossbowmen wait 2.5 s more after each shot (ExtraPauseAfterCrossbowShotSeconds)");
                double t = 7000;

                // ---- A: a FRESH shield man (a 300-skill veteran: full strength for 7 swings) waits the defensive share,
                // guard up (by input): D 0.82 s at m 1 → 0.3 x (0.82 + 1.00) / 0.7 = 0.78 s; the tired part 0
                var sh = Veteran(logic, 500);
                facts.Shield.Add(500);
                Attack(logic, sh, ref t);
                var ps = sh.Pace;
                Check(ps != null && ps.Class == AttackClass.ShieldInfantry && ps.ByInput && Near(ps.Share, 0.78) && ps.Tired == 0 && ps.Percent == 30
                      && Near(ps.Pause, 0.78) && input.Started.Contains(500) && rate.Holds == 1 && pace.PausesShareOnly(AttackClass.ShieldInfantry) == 1,
                    "A: a fresh shield man did not wait the defensive share: " + (ps == null ? "no pause" : ps.Class + ", share " + ps.Share + ", tired " + ps.Tired + ", pause " + ps.Pause));
                Check(rate.NotHeld(PaceNotHeld.FullStrength) == 0, "A: a fresh shield man was counted 'at full strength, not held'");
                LogHas("[rate] battle pace - first shield infantry attack this mission: (agent 500) at ");
                LogHas(" s (AI melee on foot, a shield in the other hand; ShieldInfantrySwingsLessPercent 30) → a pause of 0.78 s asked = the tired pause 0.00 s + the battle-pace share 0.78 s - his expected cycle (his attack 0.82 s + his own gap 1.00 s + the tired pause 0.00 s) 1.82 s ÷ (1 - 0.30) = 2.60 s: 30% fewer swings");
                LogHas("[rate] first AI timer this mission: (agent 500) at ");
                LogHas(" at attack speed x1.00 (f 1.00) → no new attack for 0.78 s = the tired pause 0.00 s (D x (1/m - 1)) + 0.78 s of battle pace (shield infantry swing 30% less), until ");
                Attack(logic, sh, ref t);   // the stand-in AI readies the moment the pause ends: release to release 0.5 + 0.78 + 0.4
                Check(pace.Cycles(AttackClass.ShieldInfantry) == 1 && Near(pace.CycleMean(AttackClass.ShieldInfantry), 1.68)
                      && Near(pace.ModelAt0Mean(AttackClass.ShieldInfantry), 1.82) && Near(pace.ModelWithMean(AttackClass.ShieldInfantry), 2.60)
                      && Near(pace.GapAfterMean(AttackClass.ShieldInfantry), 0),
                    "A: the class cycle / model: cycle " + pace.CycleMean(AttackClass.ShieldInfantry) + " (1.68), at 0 " + pace.ModelAt0Mean(AttackClass.ShieldInfantry)
                    + " (1.82), with " + pace.ModelWithMean(AttackClass.ShieldInfantry) + " (2.60), gap " + pace.GapAfterMean(AttackClass.ShieldInfantry));

                // a TIRED shield man: the tired part D x (1/m - 1) and the share on top, (tired + 0.3 x 1.82) / 0.7
                var ts = Wounded(logic, 501, 40f, t);   // f 0.53; the swing's charge takes it to 0.27 (m 0.41)
                facts.Shield.Add(501);
                SwingOnce(logic, ts, ref t);
                logic.TickPace(t);
                var tp = ts.Pace!;
                double tired = 0.82 * (1.0 / tp.Asked - 1.0);
                Check(tp.Active && tp.Class == AttackClass.ShieldInfantry && Near(tp.Tired, tired) && Near(tp.Pause, (tired + 0.3 * 1.82) / 0.7) && Near(tp.Share, tp.Pause - tired)
                      && pace.PausesTiredAndShare(AttackClass.ShieldInfantry) == 1,
                    "A: a tired shield man's pause: m " + tp.Asked + ", tired " + tp.Tired + " (" + tired + "), pause " + tp.Pause);
                t = tp.Until;
                logic.TickPace(t);

                // ---- B: a fresh man WITHOUT a shield (a two-hander): 15% → 0.15 x 1.82 / 0.85 = 0.321 s
                var th = Veteran(logic, 510);
                Attack(logic, th, ref t);
                Check(th.Pace != null && th.Pace.Class == AttackClass.FootMelee && Near(th.Pace.Share, 0.15 * 1.82 / 0.85) && th.Pace.Percent == 15,
                    "B: a fresh two-hander's share: " + (th.Pace == null ? "none" : th.Pace.Class + " " + th.Pace.Share));
                LogHas("[rate] battle pace - first other foot melee attack this mission: (agent 510) at ");

                // ---- C: riders - fresh: no pause (a shield on horseback is still a rider); tired: the tired pause alone
                var rd = Veteran(logic, 520);
                facts.Shield.Add(520);
                Mount(rd, 920);
                int fullBefore = rate.NotHeld(PaceNotHeld.FullStrength);
                Attack(logic, rd, ref t, pause: 0);
                Check(rd.Pace == null && pace.NoPause(AttackClass.Rider) == 1 && rate.NotHeld(PaceNotHeld.FullStrength) == fullBefore + 1 && pace.Attacks(AttackClass.Rider) == 1,
                    "C: a fresh rider was held (riders have no battle-pace share)");
                LogHas("[rate] battle pace - first riders attack this mission: (agent 520) at ");
                LogHas(" s (AI melee on horseback; no setting - tiredness only) → no pause - at full strength, and no battle-pace share (none for this class, or its setting at 0)");
                var tr = Wounded(logic, 521, 40f, t);
                Mount(tr, 921);
                SwingOnce(logic, tr, ref t);
                logic.TickPace(t);
                Check(tr.Pace != null && tr.Pace.Active && tr.Pace.Class == AttackClass.Rider && tr.Pace.Share == 0 && Near(tr.Pace.Pause, 0.82 * (1.0 / tr.Pace.Asked - 1.0))
                      && pace.PausesTiredOnly(AttackClass.Rider) == 1,
                    "C: a tired rider's pause is not the tired part alone");
                t = tr.Pace!.Until;
                logic.TickPace(t);

                // ---- D: archers - a fresh bowman waits 2.0 s, a tired one D x (1/m - 1) + 2.0; horse archers too; crossbows 2.5;
                // a throw nothing at full strength
                var bw = Veteran(logic, 530);
                Shot(logic, bw, ref t);
                Check(bw.Pace != null && bw.Pace.Active && bw.Pace.Class == AttackClass.Bow && Near(bw.Pace.Pause, 2.0) && bw.Pace.Tired == 0,
                    "D: a fresh bowman did not wait 2.0 s: " + (bw.Pace == null ? "none" : bw.Pace.Class + " " + bw.Pace.Pause));
                LogHas("[rate] battle pace - first bowmen attack this mission: (agent 530) at ");
                LogHas(" s (AI bow shots, on foot and horse archers; ExtraPauseAfterBowShotSeconds 2.0) → a pause of 2.00 s asked = the tired pause 0.00 s + the battle-pace share 2.00 s - the archer's extra seconds on top of the tired pause");
                t = bw.Pace!.Until;
                logic.TickPace(t);
                var tb = Wounded(logic, 531, 40f, t);  // f 0.53 (no charge offline - the shot event needs a mission)
                Shot(logic, tb, ref t);
                var tbp = tb.Pace!;
                Check(tbp.Active && tbp.Class == AttackClass.Bow && Near(tbp.Share, 2.0) && Near(tbp.Tired, 2.33 * (1.0 / tbp.Asked - 1.0)) && Near(tbp.Pause, tbp.Tired + 2.0),
                    "D: a tired bowman's pause: tired " + tbp.Tired + ", share " + tbp.Share + ", pause " + tbp.Pause);
                t = tbp.Until;
                logic.TickPace(t);
                var ha = Veteran(logic, 532);
                Mount(ha, 932);
                int mountedBefore = rate.HoldsMounted;
                Shot(logic, ha, ref t);
                Check(ha.Pace != null && ha.Pace.Class == AttackClass.Bow && Near(ha.Pace.Pause, 2.0) && rate.HoldsMounted == mountedBefore + 1, "D: a horse archer did not wait the bow's extra seconds");
                t = ha.Pace!.Until;
                logic.TickPace(t);
                var xb = Veteran(logic, 533);
                facts.Ranged[533] = RangedWeaponKind.Crossbow;
                Shot(logic, xb, ref t, draw: 0.8, reload: 2.4);
                Check(xb.Pace != null && xb.Pace.Class == AttackClass.Crossbow && Near(xb.Pace.Pause, 2.5), "D: a fresh crossbowman did not wait 2.5 s");
                t = xb.Pace!.Until;
                logic.TickPace(t);
                var jv = Veteran(logic, 534);
                Shot(logic, jv, ref t, draw: 0.9, reload: 0, thrown: true);
                Check(jv.Pace == null && pace.NoPause(AttackClass.OtherRanged) == 1 && pace.Attacks(AttackClass.OtherRanged) == 1, "D: a fresh javelin thrower was held");

                // ---- E: YOU are never slowed by it - no class read, no AI pause, not counted
                var me = Veteran(logic, 540);
                facts.Shield.Add(540);
                logic.SmokePlayer = me.Agent;
                int reads = facts.Reads, attacks = pace.Attacks(AttackClass.ShieldInfantry), holds = rate.Holds;
                Attack(logic, me, ref t);
                Check(me.Pace == null && facts.Reads == reads && pace.Attacks(AttackClass.ShieldInfantry) == attacks && rate.Holds == holds && !logic.PlayerTimer.Running,
                    "E: the player (a fresh man with a shield) got a battle-pace pause, a class read or a count");
                logic.SmokePlayer = null;

                // ---- F: hot swap - a slider change applies at the next swing, logged; 0 = off
                S.Set(SettingsSchema.ShieldInfantrySwingsLessPercent, 0, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                LogHas("[rate] battle pace changed mid-mission at ");
                LogHas("AI shield infantry unchanged (ShieldInfantrySwingsLessPercent 0 = off), other AI foot melee swing 15% less");
                int full = rate.NotHeld(PaceNotHeld.FullStrength);
                Attack(logic, sh, ref t);
                Check(rate.NotHeld(PaceNotHeld.FullStrength) == full + 1 && !sh.Pace!.Active, "F: at 0 a fresh shield man still waited");
                S.Set(SettingsSchema.ShieldInfantrySwingsLessPercent, 50, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                SwingOnce(logic, sh, ref t);
                logic.TickPace(t);
                Check(sh.Pace!.Active && Near(sh.Pace.Pause, 0.5 * 1.82 / 0.5), "F: at 50 the share is not 1.82 s: " + sh.Pace.Pause);

                // ---- G: the master switch lifts a running battle-pace pause at once; back on = pauses again
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                logic.TickPace(t + 0.05);
                Check(!sh.Pace.Active && rate.Ended(PaceEnd.SwitchedOff) >= 1 && input.Released.Contains(500), "G: ModEnabled off did not lift a battle-pace pause at once");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                S.Set(SettingsSchema.ShieldInfantrySwingsLessPercent, 30, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                t += 1;
                logic.TickPace(t);
                Attack(logic, sh, ref t);
                Check(pace.PausesShareOnly(AttackClass.ShieldInfantry) >= 3, "G: back on, a fresh shield man did not wait again");

                // ---- H: the summary lines - each class, the timer rows with the share, the change flagged
                logic.WriteAthleticsSummary();
                LogHas("[summary] battle pace (step 21) settings at the end: ON - AI shield infantry swing 30% less (ShieldInfantrySwingsLessPercent)");
                LogHas(" - a battle-pace setting CHANGED during this battle: the lines below mix both");
                LogHas("[summary] battle pace - shield infantry (AI melee on foot, a shield in the other hand; ShieldInfantrySwingsLessPercent 30 - asks 30% fewer swings): ");
                LogHas("[summary] battle pace - other foot melee (AI melee on foot without a shield - two-handers, polearms, a one-hander alone; FootMeleeSwingsLessPercent 15 - asks 15% fewer swings): 1 attacks by 1 man; pauses 1 - at full strength 1 (the share alone)");
                LogHas("[summary] battle pace - riders (AI melee on horseback; no setting - tiredness only): 2 attacks by 2 men; pauses 1 (tired - no share)");
                LogHas("[summary] battle pace - bowmen (AI bow shots, on foot and horse archers; ExtraPauseAfterBowShotSeconds 2.0 - sized for about 30% fewer shots): 3 shots by 3 men; pauses 3 - at full strength 2 (the share alone), tired 1 (tired + the share), tired only 0");
                LogHas("[summary] battle pace - crossbowmen (AI crossbow shots, on foot and mounted; ExtraPauseAfterCrossbowShotSeconds 2.5 - sized for about 30% fewer shots): 1 shots by 1 man");
                LogHas("[summary] battle pace - thrown and slings (AI javelins, throwing axes and knives, stones, slings; no setting - tiredness only): 1 throws by 1 man; pauses 0; no pause 1");
                LogHas(" s = D x (1/m - 1) + the battle-pace share avg ");
                LogHas("[summary] attack rate - AI timer (AttackRatePaceHold on at the end;");
                LogHas("; by f: peak (f 1) ");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
                StepBackDefaults();
            }
        }

        /// <summary>
        /// Step 21's cost check: fresh men are held now, so a big battle holds far more men at once - the tick over a thousand
        /// running pauses must stay flat: no allocation per tick, a small fixed cost per held man.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void BattlePaceTickCostStaysFlat()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                StepBackDefaults();
                S.Set(SettingsSchema.StepBackEnabled, false, SettingSources.File);
                S.Set(SettingsSchema.AttackRatePaceByInput, true, SettingSources.File);
                BattlePaceOn();
                var facts = new FakeWeaponFacts();
                var input = new FakePaceBody();
                var logic = NewPaceLogic(input, facts);
                const int men = 1000;
                double t0 = 9000;
                var r = Rules;
                for (int i = 0; i < men; i++)
                {
                    var st = Veteran(logic, 2000 + i);
                    facts.Shield.Add(2000 + i);
                    logic.ObserveAction(st, ActReadyMeleeCode, t0, in r);
                    AthleticsLogic.ReadyFull(st, t0 + 0.32);
                    logic.ObserveAction(st, ActRelease, t0 + 0.4, in r);
                    st.SpeedDirty = false;
                    logic.ObserveAction(st, ActIdle, t0 + 0.9, in r);
                    logic.TickPace(t0 + 0.9);           // one attack end a tick, as in a battle (the start cap is 100 a tick)
                }
                Check(logic.HeldNow == men, "cost: not every man is held: " + logic.HeldNow);
                const int ticks = 500;
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var sw = Stopwatch.StartNew();
                for (int k = 0; k < ticks; k++) logic.TickPace(t0 + 0.9 + 0.0005 * k);  // all still inside their 0.78 s
                sw.Stop();
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double msPerTick = sw.Elapsed.TotalMilliseconds / ticks;
                Console.WriteLine("        step 21 cost: " + men + " men held at once - the AI timer's tick " + msPerTick.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                                  + " ms a tick, " + allocated + " bytes allocated over " + ticks + " ticks");
                Check(logic.HeldNow == men, "cost: holds ended early");
                Check(msPerTick < 0.5, "cost: the tick over " + men + " held men took " + msPerTick + " ms (budget 0.5 ms)");
                Check(allocated < 256 * 1024, "cost: the tick allocates - " + allocated + " bytes over " + ticks + " ticks (one object per man per tick would be megabytes)");
                logic.TickPace(t0 + 2.0);
                Check(logic.HeldNow == 0 && input.Released.Count == men, "cost: the thousand pauses did not all end on time");
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Step 69 (Toni's battles 2026-10-19: "some of my men are sitting there in distance while my soldiers are finishing the enemy off"): WINNERS
    /// DON'T HANG BACK through the real logic with courage's stand-in world - a tired green unit winning its fight is calmed and mops up, a losing
    /// one flocks exactly as before (its positions match FearWinCalms off to the metre), no pull without an enemy near; and THE IDLE-MEN
    /// diagnostic: men of a charging unit standing off with an enemy near, counted by who holds them. What it cannot check: whether Toni's
    /// standing men were ours (PLAYTEST D34 - the idle line tells).
    /// </summary>
    internal static partial class Program
    {
        private static void Step69Defaults()
        {
            S.Set(SettingsSchema.FearWinCalms, true, SettingSources.File);
            S.Set(SettingsSchema.FearWinEvenOddsPercent, 100, SettingSources.File);
            S.Set(SettingsSchema.FearWinClearOddsPercent, 200, SettingSources.File);
            S.Set(SettingsSchema.FearWinShakenPercent, 50, SettingSources.File);
            S.Set(SettingsSchema.FearThreatMetres, 12.0, SettingSources.File);
            S.Set(SettingsSchema.FearMopUpWinPercent, 50, SettingSources.File);
        }

        private static AthleticsLogic NewStep69Logic(FakeCourageBody body)
        {
            var logic = NewFearLogic(body);
            Step69Defaults();
            return logic;
        }

        /// <summary>A charging green unit (formation 1): <paramref name="men"/> tired looters (morale 50, 30% of the bar) in rows around (0, 0);
        /// a roamer of it at (<paramref name="roamX"/>, 0).</summary>
        private static List<TrackedAgent> GreenChargers(AthleticsLogic logic, FakeCourageBody body, int index, int men, double roamX, out TrackedAgent roamer)
        {
            body.Orders[1] = FearOrder.Charge;
            var unit = new List<TrackedAgent>();
            for (int i = 0; i < men; i++) unit.Add(FearMan(logic, body, index + i, (i % 4) * 0.8, (i / 4) * 0.8, 1, 1, legionary: false, morale: 50, bar: 0.3, slot: false));
            roamer = FearMan(logic, body, index + men, roamX, 0, 1, 1, legionary: false, morale: 50, bar: 0.3, slot: false);
            return unit;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WinnersDontHangBack()
        {
            var keep = _logic;
            try
            {
                // ---- 1. A WINNING GREEN UNIT IS CALMED: 12 tired looters charging 2 broken looters - no flock, the roamer left to his charge
                var body = new FakeCourageBody();
                var logic = NewStep69Logic(body);
                LogHas("; WINNING calms (step 69): his fear x (1 - win), win 0 at local odds 1.0 (losing below: unchanged) to 1 at 2.0");
                double t = 40000;
                var unit = GreenChargers(logic, body, 7500, 12, 8, out var roamer);
                FearMan(logic, body, 7520, 0.8, 3.2, 2, 2, legionary: false, morale: 10, bar: 0.5);
                FearMan(logic, body, 7521, 1.6, 3.2, 2, 2, legionary: false, morale: 10, bar: 0.5);
                for (int k = 0; k < 4; k++) logic.TickCourage(t += 0.5);
                foreach (var g in unit)
                    Check(!g.Fear.Flocking && (g.Input == null || !g.Input.Flock), "a tired green WINNER still flocks: fear " + F2s(g.Fear.BaseFear) + " → " + F2s(g.Fear.Fear) + ", win " + F2s(g.Fear.Win));
                var front = unit[9];
                Check(front.Fear.BaseFear > 0.5 && front.Fear.Win > 0.99 && front.Fear.Fear < 0.01, "the front green's fear is not calmed by the win: " + F2s(front.Fear.BaseFear) + " / " + F2s(front.Fear.Win) + " / " + F2s(front.Fear.Fear));
                Check(!roamer.Fear.Flocking && (roamer.Input == null || !roamer.Input.Flock), "the winning unit's roamer is still pulled back to the mass");
                Check(logic.FearStats.CalmedByWinLooks > 0, "no look counted as calmed by winning");
                Console.WriteLine("        step 69 winners: the front green's fear " + F2s(front.Fear.BaseFear) + " → " + F2s(front.Fear.Fear) + " (win " + F2s(front.Fear.Win)
                                  + "), the roamer's " + F2s(roamer.Fear.BaseFear) + " → " + F2s(roamer.Fear.Fear) + " (the unit's win " + F2s(roamer.Fear.Win) + ")");

                // the A / B: the same world with FearWinCalms OFF - the greens flock and the roamer is pulled in (step 67 exactly)
                body = new FakeCourageBody();
                logic = NewStep69Logic(body);
                S.Set(SettingsSchema.FearWinCalms, false, SettingSources.File);
                t = 41000;
                unit = GreenChargers(logic, body, 7530, 12, 8, out roamer);
                FearMan(logic, body, 7550, 0.8, 3.2, 2, 2, legionary: false, morale: 10, bar: 0.5);
                FearMan(logic, body, 7551, 1.6, 3.2, 2, 2, legionary: false, morale: 10, bar: 0.5);
                for (int k = 0; k < 4; k++) logic.TickCourage(t += 0.5);
                Check(unit[9].Fear.Flocking && roamer.Fear.Flocking && roamer.Input != null && roamer.Input.Flock && roamer.Input.FlockX < -0.3f,
                    "FearWinCalms off: the green winners do not flock / the roamer is not pulled in (step 67's behaviour)");
                S.Set(SettingsSchema.FearWinCalms, true, SettingSources.Mcm); // live: calmed at the next looks
                for (int k = 0; k < 2; k++) logic.TickCourage(t += 0.5);
                Check(!unit[9].Fear.Flocking && !roamer.Fear.Flocking && !roamer.Input!.Flock && logic.FearStats.Ended(FearEnd.Winning) > 0,
                    "FearWinCalms on mid-battle: the spells did not end 'winning' - " + logic.FearStats.Ended(FearEnd.Winning));

                // ---- 2. THE MOP-UP: a partial win (clear odds 4:1) leaves the greens afraid, but their unit wins - the roamer is not leashed
                body = new FakeCourageBody();
                logic = NewStep69Logic(body);
                S.Set(SettingsSchema.FearWinClearOddsPercent, 400, SettingSources.File);
                S.Set(SettingsSchema.FearWinShakenPercent, 0, SettingSources.File);
                S.Set(SettingsSchema.FearMopUpWinPercent, 20, SettingSources.File);
                t = 42000;
                unit = GreenChargers(logic, body, 7560, 8, 12, out roamer);
                FearMan(logic, body, 7580, 0.8, 3.2, 2, 2, legionary: true, morale: 70, bar: 1.0);
                for (int k = 0; k < 4; k++) logic.TickCourage(t += 0.5);
                double unitWin = logic.FearUnits.WinOf(FearMath.UnitKey(1, 1), t);
                Console.WriteLine("        step 69 mop-up: the unit's win " + F2s(unitWin) + ", the roamer's fear " + F2s(roamer.Fear.BaseFear) + " → " + F2s(roamer.Fear.Fear)
                                  + " (pull " + F2s(roamer.Fear.Pull) + "), mop-up looks " + logic.FearStats.MopUpLooks);
                Check(unitWin >= 0.2 && unitWin < 1, "the mop-up world's unit win is not partial: " + F2s(unitWin));
                Check(roamer.Fear.Flocking && roamer.Fear.MopUp && roamer.Fear.Loose && (roamer.Input == null || !roamer.Input.Flock) && logic.FearStats.MopUpLooks > 0,
                    "a frightened man of a MOPPING-UP charge is still leashed: flocking " + roamer.Fear.Flocking + ", mop-up " + roamer.Fear.MopUp + ", loose " + roamer.Fear.Loose);
                S.Set(SettingsSchema.FearMopUpWinPercent, 0, SettingSources.Mcm); // live: the leash again
                logic.TickCourage(t += 0.5);
                Check(roamer.Fear.Flocking && !roamer.Fear.MopUp && !roamer.Fear.Loose && roamer.Input != null && roamer.Input.Flock,
                    "FearMopUpWinPercent 0: the roamer 12 m out is not pulled back in (the step-67 leash)");

                // ---- 3. A LOSING UNIT FLOCKS EXACTLY AS BEFORE: step 67's frightened rank vs fresh legionaries, every step-69 rule on vs all off
                var place = new double[2][];
                var fears = new double[2][];
                for (int pass = 0; pass < 2; pass++)
                {
                    body = new FakeCourageBody();
                    logic = NewStep69Logic(body);
                    if (pass == 1)
                    {
                        S.Set(SettingsSchema.FearWinCalms, false, SettingSources.File);
                        S.Set(SettingsSchema.FearThreatMetres, 0.0, SettingSources.File);
                        S.Set(SettingsSchema.FearMopUpWinPercent, 0, SettingSources.File);
                    }
                    t = 43000 + pass * 1000;
                    var looters = new List<TrackedAgent>();
                    for (int i = 0; i < 10; i++) looters.Add(FearMan(logic, body, 7600 + i, i, 0, 1, 1, legionary: false, morale: 20, bar: 0.5));
                    for (int i = 0; i < 10; i++) FearMan(logic, body, 7620 + i, (i % 5) * 2 + 0.5, 3 + (i / 5), 2, 2, legionary: true, morale: 70, bar: 1.0);
                    WalkFearWorld(logic, body, t, 10);
                    place[pass] = new double[20];
                    fears[pass] = new double[10];
                    for (int i = 0; i < 10; i++)
                    {
                        var m = body.ByIndex[7600 + i];
                        place[pass][2 * i] = m.X;
                        place[pass][2 * i + 1] = m.Y;
                        fears[pass][i] = looters[i].Fear.Fear;
                    }
                    Check(looters[0].Fear.Flocking, "the losing looters do not flock (pass " + pass + ")");
                }
                double worst = 0;
                for (int i = 0; i < 20; i++) worst = Math.Max(worst, Math.Abs(place[0][i] - place[1][i]));
                for (int i = 0; i < 10; i++) worst = Math.Max(worst, Math.Abs(fears[0][i] - fears[1][i]));
                Check(worst < 1e-9, "a LOSING unit does not flock exactly as step 67: the largest difference " + worst);

                // ---- 4. NO PULL WITHOUT A THREAT: frightened looters with the enemy 20 m off stand to their order; at 10 m they flock
                body = new FakeCourageBody();
                logic = NewStep69Logic(body);
                t = 46000;
                var scared = new List<TrackedAgent>();
                for (int i = 0; i < 6; i++) scared.Add(FearMan(logic, body, 7700 + i, i * 1.5, 0, 1, 1, legionary: false, morale: 10, bar: 0.3));
                var foe = FearMan(logic, body, 7710, 3, 20, 2, 2, legionary: true, morale: 70, bar: 1.0);
                for (int k = 0; k < 3; k++) logic.TickCourage(t += 0.5);
                Check(logic.FearNow == 0 && scared[0].Fear.Fear > 0.5 && !scared[0].Fear.Threat && logic.FearStats.NoThreatLooks > 0 && logic.FearStats.ThreatReads > 0,
                    "frightened men with no enemy within 12 m are pulled: flocking " + logic.FearNow + ", threat " + scared[0].Fear.Threat);
                body.ByIndex[7710].Y = 10; // the enemy closes to 10 m (beyond courage's 8 m scan: one read)
                for (int k = 0; k < 2; k++) logic.TickCourage(t += 0.5);
                Check(logic.FearNow > 0 && scared[0].Fear.Threat, "the enemy at 10 m: the frightened men do not flock");
                body.ByIndex[7710].Y = 25;
                for (int k = 0; k < 2; k++) logic.TickCourage(t += 0.5);
                Check(logic.FearNow == 0 && logic.FearStats.Ended(FearEnd.NoThreat) > 0, "the enemy gone to 25 m: the spells did not end 'no enemy near'");
                S.Set(SettingsSchema.FearThreatMetres, 0.0, SettingSources.Mcm); // live: step 67's pull without a threat
                logic.TickCourage(t += 0.5);
                Check(logic.FearNow > 0, "FearThreatMetres 0: the frightened men do not flock (step 67)");
                logic.WriteCourageSummary();
                LogHas("[summary] fear flock and winning (step 69) by his side's local odds: losing (odds below 1.0) ");
                _ = foe;
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        // ------------------------------------------------------------------ the idle-men diagnostic

        private static void SetCouragePhase(TrackedAgent st, CouragePhase phase) =>
            typeof(CourageMind).GetProperty("Phase", BindingFlags.Instance | BindingFlags.Public)!.SetValue(st.Courage, phase);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void IdleMenDiagnostic()
        {
            var keep = _logic;
            try
            {
                var body = new FakeCourageBody();
                var logic = NewStep69Logic(body);
                double t = 47000;
                // the enemy: one fresh legionary at (0, 10) - 10 m from the line, beyond courage's 8 m scan
                FearMan(logic, body, 7800, 0, 10, 2, 9, legionary: true, morale: 70, bar: 1.0);
                // formation 1 (CHARGE): a calm man (the game holds him), a crossbowman (normal), a bracer, a timid man
                body.Orders[1] = FearOrder.Charge;
                var calm = FearMan(logic, body, 7801, -1, 0, 1, 1, legionary: true, morale: 70, bar: 1.0, slot: false);
                var shooter = FearMan(logic, body, 7802, 0, 0, 1, 1, legionary: true, morale: 70, bar: 1.0, slot: false);
                body.ByIndex[7802].Ranged = true;
                var bracer = FearMan(logic, body, 7803, 1, 0, 1, 1, legionary: true, morale: 70, bar: 1.0, slot: false);
                bracer.Brace = new BraceState { Active = true };
                var timid = FearMan(logic, body, 7804, 2, 0, 1, 1, legionary: true, morale: 70, bar: 1.0, slot: false);
                SetCouragePhase(timid, CouragePhase.Holding); // courage is off here: the phase stays as set (a man courage holds timid)
                var walker = FearMan(logic, body, 7805, 3, 0, 1, 1, legionary: true, morale: 70, bar: 1.0, slot: false);
                // formation 7 (CHARGE): frightened looters - one 5 m from the mass, pulled in by the fear flock (standing: the stand-in never walks him)
                body.Orders[7] = FearOrder.Charge;
                FearMan(logic, body, 7806, -6, 0, 1, 7, legionary: false, morale: 10, bar: 0.3, slot: false);
                FearMan(logic, body, 7807, -6, 1, 1, 7, legionary: false, morale: 10, bar: 0.3, slot: false);
                var held = FearMan(logic, body, 7808, -6, 8, 1, 7, legionary: false, morale: 10, bar: 0.3, slot: false);
                // formation 4: a shield wall on ADVANCE, its man held by step 41
                body.Orders[4] = FearOrder.Wall;
                body.Kinds[4] = BraceOrderKind.Advance;
                var wallMan = FearMan(logic, body, 7809, 4, 0, 1, 4, legionary: true, morale: 70, bar: 1.0);
                wallMan.Wall = new WallState { Held = true };
                // not idle: a man in a fight (an enemy at 2 m), a man of a HOLD order
                body.Orders[5] = FearOrder.Charge;
                var fighter = FearMan(logic, body, 7810, 30, 0, 1, 5, legionary: true, morale: 70, bar: 1.0, slot: false);
                FearMan(logic, body, 7811, 30, 2, 2, 8, legionary: true, morale: 70, bar: 1.0);
                body.Orders[6] = FearOrder.Hold;
                var holder = FearMan(logic, body, 7812, -1, -2, 1, 6, legionary: true, morale: 70, bar: 1.0);
                for (int k = 0; k < 15; k++)
                {
                    body.ByIndex[7805].X += 0.5; // the walker: 1 m/s
                    logic.TickCourage(t += 0.5);
                }
                var idle = logic.IdleStats;
                bool Was(TrackedAgent st, IdleHolder h) => (st.Idle.MenMask & (1 << (int)h)) != 0;
                Check(Was(calm, IdleHolder.Game) && Was(shooter, IdleHolder.Ranged) && Was(bracer, IdleHolder.Brace) && Was(timid, IdleHolder.CourageTimid),
                    "the idle men of the charge are not filed by their holder: calm " + calm.Idle.MenMask + ", shooter " + shooter.Idle.MenMask + ", bracer " + bracer.Idle.MenMask
                    + ", timid " + timid.Idle.MenMask);
                Check(held.Fear.Wish && Was(held, IdleHolder.FearFlock), "the man the fear flock holds standing is not filed under it: wish " + held.Fear.Wish + ", mask " + held.Idle.MenMask);
                Check(Was(wallMan, IdleHolder.WallHold), "the held wall man of an advance is not filed under the wall hold: " + wallMan.Idle.MenMask);
                Check(walker.Idle.MenMask == 0 && fighter.Idle.MenMask == 0 && holder.Idle.MenMask == 0,
                    "a walking man / a man in a fight / a man on a hold was counted idle: " + walker.Idle.MenMask + " / " + fighter.Idle.MenMask + " / " + holder.Idle.MenMask);
                Check(idle.SecondsOf(IdleHolder.Game) > 5 && idle.SecondsOf(IdleHolder.Game) < 30 && idle.EnemyReads > 0,
                    "the man-seconds: the game " + F2s(idle.SecondsOf(IdleHolder.Game)) + ", enemy reads " + idle.EnemyReads);
                Check(Math.Abs(idle.MeanEnemy(IdleHolder.Ranged) - 10) < 0.01, "the shooter's enemy is 10 m off: " + F2s(idle.MeanEnemy(IdleHolder.Ranged)));
                LogHas("[fear] first IDLE man held by the game itself (none of ours) (step 69): ");
                LogHas("[fear] first IDLE man held by the fear flock (step 67) (step 69): ");
                LogHas("[fear] first IDLE man held by the wall hold (step 41) (step 69): ");
                Console.WriteLine("        step 69 idle: game " + F2s(idle.SecondsOf(IdleHolder.Game)) + " man-s (" + idle.MenOf(IdleHolder.Game) + " men), fear flock "
                                  + F2s(idle.SecondsOf(IdleHolder.FearFlock)) + ", courage " + F2s(idle.SecondsOf(IdleHolder.CourageTimid)) + ", brace " + F2s(idle.SecondsOf(IdleHolder.Brace))
                                  + ", wall " + F2s(idle.SecondsOf(IdleHolder.WallHold)) + ", ranged " + F2s(idle.SecondsOf(IdleHolder.Ranged)) + "; enemy reads " + idle.EnemyReads);
                // measurement only: the fear flock switched off - the idle looks go on
                long looks = idle.Looks;
                S.Set(SettingsSchema.FearFlockEnabled, false, SettingSources.Mcm);
                logic.TickCourage(t += 0.5);
                Check(idle.Looks > looks && !held.Fear.Wish, "FearFlockEnabled off: the idle looks stopped (or the flock still steers)");
                S.Set(SettingsSchema.FearFlockEnabled, true, SettingSources.Mcm);
                logic.WriteCourageSummary();
                LogHas("[summary] idle men (step 69 - AI men on foot on a charge / advance, an enemy within 25 m and none within 3 m, standing (below 0.5 m/s) more than 5 s");
                LogHas("the fear flock (step 67) ");
                SetCouragePhase(timid, CouragePhase.Brave);
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        /// <summary>The master switch: off = the flock (winning or not) let go at once, the idle diagnostic still measuring.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Step69FollowsTheMasterSwitch()
        {
            var keep = _logic;
            try
            {
                var body = new FakeCourageBody();
                var logic = NewStep69Logic(body);
                double t = 48000;
                body.Orders[1] = FearOrder.Charge;
                FearMan(logic, body, 7900, 0, 10, 2, 2, legionary: true, morale: 70, bar: 1.0);
                FearMan(logic, body, 7901, -6, 0, 1, 1, legionary: false, morale: 10, bar: 0.3, slot: false);
                FearMan(logic, body, 7902, -6, 1, 1, 1, legionary: false, morale: 10, bar: 0.3, slot: false);
                var a = FearMan(logic, body, 7903, -6, 8, 1, 1, legionary: false, morale: 10, bar: 0.3, slot: false);
                for (int k = 0; k < 3; k++) logic.TickCourage(t += 0.5);
                Check(a.Fear.Flocking && a.Input!.Flock, "precondition: the frightened looter 5 m out is not pulled in");
                long looks = logic.IdleStats.Looks;
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                logic.TickCourage(t += 0.5);
                Check(!a.Fear.Flocking && !a.Input!.Flock && logic.FearNow == 0, "mod off: the flocking man was not let go at once (step 69)");
                Check(logic.IdleStats.Looks > looks, "mod off: the idle diagnostic stopped measuring (it runs ON and OFF)");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                logic.TickCourage(t += 0.5);
                Check(a.Fear.Flocking && a.Input!.Flock, "mod back on: the frightened looter is not pulled in again (step 69)");
            }
            finally
            {
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        /// <summary>The cost: 1000 men charging each other in two blocks (500 legionaries winning, 500 frightened looters losing), every man looked
        /// at every 0.5 s with the win, the threat line and the idle watch (the rear ranks beyond courage's scan read the enemy) - small, no bytes.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Step69CostStaysFlat()
        {
            var keep = _logic;
            try
            {
                var body = new FakeCourageBody();
                var logic = NewStep69Logic(body);
                body.Orders[1] = FearOrder.Charge;
                body.Orders[2] = FearOrder.Charge;
                const int men = 1000;
                double t0 = 49000;
                for (int i = 0; i < men; i++)
                {
                    bool south = i < men / 2;
                    int j = south ? i : i - men / 2;
                    double x = (j % 50) * 1.2, y = (j / 50) * 1.2;
                    FearMan(logic, body, 13000 + i, x, south ? -0.75 - y : 0.75 + y, south ? 1 : 2, south ? 1 : 2, legionary: south, morale: south ? 50 : 20, bar: south ? 0.3 : 0.5,
                        slot: false);
                }
                for (int k = 0; k < 14; k++) logic.TickCourage(t0 += 0.5); // 7 s: the idle men past their 5 s, every first-time line written
                const int ticks = 300;
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var sw = Stopwatch.StartNew();
                for (int k = 1; k <= ticks; k++) logic.TickCourage(t0 + k / 60.0);
                sw.Stop();
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double msPerTick = sw.Elapsed.TotalMilliseconds / ticks;
                Console.WriteLine("        step 69 cost: " + men + " men charging in two blocks (" + logic.FearNow + " in a fear spell, calmed by winning "
                                  + logic.FearStats.CalmedByWinLooks + " looks, idle reads " + logic.IdleStats.EnemyReads + ", threat reads " + logic.FearStats.ThreatReads
                                  + ") - the courage + fear tick " + msPerTick.ToString("0.000", CultureInfo.InvariantCulture) + " ms (the stand-in's scans and enemy reads are plain loops over all 1000), "
                                  + allocated + " bytes over " + ticks + " ticks");
                Check(msPerTick < 3.0, "cost: the courage + fear tick with step 69 over " + men + " men took " + msPerTick + " ms (budget 3 ms with the stand-in's O(n) reads)");
                Check(allocated < 256 * 1024, "cost: step 69 allocates - " + allocated + " bytes over " + ticks + " ticks");
                Check(logic.FearStats.CalmedByWinLooks > 0 && logic.FearNow > 0, "cost: the world is not half winning, half flocking");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }
    }
}

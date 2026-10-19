using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Step 70 (Toni's 30 v 50 looters 2026-10-19 15:59): THE BAND and THE LONE RUNNER through the real logic with courage's stand-in world - a
    /// frightened looter column facing a wide line forms a band at most 6 ranks deep along it (the point shelter of step 67 keeps the column);
    /// a front man with an enemy at arm's length is let go to strike; a fever looter alone behind the enemy's line is walked back (fixes off:
    /// his fever keeps him there), his sprint into contact ends, a charging mass keeps its sprint and its fever; the lone-runner diagnostic
    /// counts by cause and target; the master switch; the cost. What it cannot check: how the engine walks the men (PLAYTEST D35).
    /// </summary>
    internal static partial class Program
    {
        private static void Step70Defaults()
        {
            S.Set(SettingsSchema.FearBandEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.FearBandRanksMin, 4, SettingSources.File);
            S.Set(SettingsSchema.FearBandRanksMax, 6, SettingSources.File);
            S.Set(SettingsSchema.FearBandFileMetres, 1.0, SettingSources.File);
            S.Set(SettingsSchema.FearBandRankMetres, 1.0, SettingSources.File);
            S.Set(SettingsSchema.FearBandMaxWidthMetres, 40.0, SettingSources.File);
            S.Set(SettingsSchema.FearBandGapMetres, 1.5, SettingSources.File);
            S.Set(SettingsSchema.FearStrikeReachMetres, 2.5, SettingSources.File);
            S.Set(SettingsSchema.FearStrikeLeashMetres, 2.0, SettingSources.File);
            S.Set(SettingsSchema.LoneRunnerEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.LoneRunnerAheadMetres, 4.0, SettingSources.File);
        }

        private static AthleticsLogic NewStep70Logic(FakeCourageBody body, bool courage)
        {
            AthleticsDefaults();
            SprintDefaults();
            FeverDefaults();
            CourageDefaults();
            FearDefaults();
            Step70Defaults(); // before the logic: its mission-start lines say step 70
            S.Set(SettingsSchema.CourageEnabled, courage, SettingSources.File);
            return NewCourageLogic(body);
        }

        /// <summary>The band's own files as the logic published them for formation 1: the deepest file and the files with men.</summary>
        private static (int deepest, int occupied) BandFiles(AthleticsLogic logic)
        {
            int key = FearMath.UnitKey(1, 1), deepest = 0, occupied = 0;
            var u = logic.FearUnits;
            for (int f = 0; f < u.BandFiles[key]; f++)
            {
                int c = u.FileCount[key * FearBandMath.MaxFiles + f];
                if (c > deepest) deepest = c;
                if (c > 0) occupied++;
            }
            return (deepest, occupied);
        }

        /// <summary>A unit's shape across the front (x): men per occupied 1 m file, the deepest file, the width (occupied files).</summary>
        private static (double perFile, int deepest, int width) FileShape(FakeCourageBody body, int formation)
        {
            var xs = body.Men.Where(m => m.Formation == formation).Select(m => m.X).ToList();
            double min = xs.Min(), max = xs.Max();
            int files = Math.Max(1, (int)Math.Ceiling(max - min + 1e-9));
            var c = new int[files];
            foreach (double x in xs) c[Math.Min(files - 1, (int)Math.Floor(x - min))]++;
            int occupied = c.Count(n => n > 0);
            return (xs.Count / (double)occupied, c.Max(), occupied);
        }

        /// <summary>Toni's looters in miniature: 40 frightened looters (morale 20, half a bar - the pull 1.0) in a column 3 wide and 14 deep (formation
        /// 1, a hold, their slots where they stand), facing a fresh line of 20 legionaries 20 m wide 8 m north (formation 2) - its frontage read.</summary>
        private static List<TrackedAgent> LooterColumnFacingALine(AthleticsLogic logic, FakeCourageBody body, int index)
        {
            var looters = new List<TrackedAgent>();
            for (int i = 0; i < 40; i++) looters.Add(FearMan(logic, body, index + i, (i % 3) - 1, -(i / 3), 1, 1, legionary: false, morale: 20, bar: 0.5));
            for (int i = 0; i < 20; i++) FearMan(logic, body, index + 50 + i, (i % 10) * 2 - 9, 8 + (i / 10), 2, 2, legionary: true, morale: 70, bar: 1.0);
            body.EnemyFronts[1] = (0, 8.5, 0, -1, 20, 2);
            return looters;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FearBandAlongTheLine()
        {
            var keep = _logic;
            try
            {
                // ---- 1. THE BAND: the column becomes a band at most 6 ranks deep along the line
                var body = new FakeCourageBody();
                var logic = NewStep70Logic(body, courage: false);
                LogHas("; a WIDE BAND (step 70) along the enemy's front, not a point");
                double t = 70000;
                var looters = LooterColumnFacingALine(logic, body, 17000);
                var start = FileShape(body, 1);
                t = WalkFearWorld(logic, body, t, 40);
                var band = FileShape(body, 1);
                int key = FearMath.UnitKey(1, 1);
                var units = logic.FearUnits;
                Console.WriteLine("        step 70 band: the column " + F2s(start.perFile) + " men a file (deepest " + start.deepest + ", " + start.width + " m wide) → the band "
                                  + F2s(band.perFile) + " a file (deepest " + band.deepest + ", " + band.width + " m wide); its frame " + F2s(units.BandWidth[key]) + " m wide, "
                                  + units.BandRanks[key] + " ranks, its front at y " + F2s(units.BandFront[key]) + "; places slid " + logic.FearStats.Band.Kind(FearBandPlaceKind.Slid)
                                  + ", compressed " + logic.FearStats.Band.Kind(FearBandPlaceKind.Compressed) + ", steps in refused " + logic.FearStats.Band.StepInsRefused);
                Check(units.BandLive(key, t) && Math.Abs(units.BandWidth[key] - 20) < 0.01 && units.BandRanks[key] == 6 && Math.Abs(units.BandUY[key] - 1) < 0.05,
                    "the looters' band does not face the line 20 m wide, 6 ranks deep: " + F2s(units.BandWidth[key]) + " m, " + units.BandRanks[key] + " ranks");
                var files = BandFiles(logic);
                Console.WriteLine("        step 70 band, its own files: the deepest " + files.deepest + " men, " + files.occupied + " files with men (the 1 m measure above bins the men "
                                  + "where they stopped - within 0.7 m of their places)");
                Check(files.deepest <= 6, "the frightened column did not become a band at most 6 ranks deep: its deepest file " + files.deepest);
                Check(files.occupied >= 7 && band.width >= 6, "the band is not wide: " + files.occupied + " files, " + band.width + " m");
                Check(band.deepest < start.deepest && band.width > start.width * 2, "the measured shape did not widen: " + band.deepest + " deep, " + band.width + " m");
                Check(logic.FearStats.Band.Kind(FearBandPlaceKind.Slid) > 0, "nobody slid out of a full file");
                Check(looters.Count(l => l.Fear.HasSlide) >= 15, "fewer than 15 men slid out toward the ends: " + looters.Count(l => l.Fear.HasSlide));
                logic.WriteCourageSummary();
                LogHas("[summary] fear flock band (step 70 - a wide band along the enemy's front, 4-6 ranks) - in contact, each unit's men per occupied 1.0 m file (the depth in RANKS): ON ");
                LogHas("[fear] first fear-flock spell this battle: ");

                // ---- 2. THE A / B: the same world with FearBandEnabled OFF - step 67's point keeps (deepens) the column
                body = new FakeCourageBody();
                logic = NewStep70Logic(body, courage: false);
                S.Set(SettingsSchema.FearBandEnabled, false, SettingSources.File);
                t = 71000;
                LooterColumnFacingALine(logic, body, 17100);
                t = WalkFearWorld(logic, body, t, 40);
                var point = FileShape(body, 1);
                Console.WriteLine("        step 70 A / B: the point shelter (FearBandEnabled off) " + F2s(point.perFile) + " a file (deepest " + point.deepest + ", " + point.width
                                  + " m wide) vs the band " + F2s(band.perFile) + " (deepest " + band.deepest + ", " + band.width + " m)");
                Check(point.deepest >= 12 && point.width <= 3, "the point shelter did not keep the deep column (the A / B): deepest " + point.deepest + ", " + point.width + " m wide");
                S.Set(SettingsSchema.FearBandEnabled, true, SettingSources.Mcm); // live: the band at the next looks
                t = WalkFearWorld(logic, body, t, 40);
                var live = BandFiles(logic);
                Check(live.deepest <= 6 && live.occupied >= 7, "FearBandEnabled on mid-battle: no band - deepest " + live.deepest + ", " + live.occupied + " files");

                // ---- 3. A FRONT MAN STRIKES: an enemy at arm's length of a front-rank man - let go (his input off), the leash walks him back after
                body = new FakeCourageBody(); // a fresh world for the strike
                logic = NewStep70Logic(body, courage: false);
                S.Set(SettingsSchema.FearWinCalms, false, SettingSources.File); // a looter mass against one legionary would WIN (step 69) and calm - this check is about a losing front
                t = 72000;
                var column = LooterColumnFacingALine(logic, body, 17200);
                t = WalkFearWorld(logic, body, t, 30);
                var striker = column.Where(l => l.Fear.Flocking && !l.Fear.Covered).OrderByDescending(l => body.ByIndex[l.AgentIndex].Y).First();
                var sm = body.ByIndex[striker.AgentIndex];
                var close = FearMan(logic, body, 17299, sm.X, sm.Y + 1.5, 2, 2, legionary: true, morale: 70, bar: 1.0);
                for (int k = 0; k < 2; k++) logic.TickCourage(t += 0.5);
                Check(striker.Fear.Flocking && striker.Fear.Striking && (striker.Input == null || !striker.Input.Flock) && logic.FearStats.Band.StrikeLooks > 0,
                    "a front man with an enemy at 1.5 m was not let go to strike: striking " + striker.Fear.Striking + ", input " + (striker.Input != null && striker.Input.Flock));
                sm.Y += 3; // he stepped in past the leash (3 m ahead of his place) - walked back
                body.ByIndex[17299].Y = sm.Y + 1.5;
                for (int k = 0; k < 2; k++) logic.TickCourage(t += 0.5);
                Check(!striker.Fear.Striking && striker.Input != null && striker.Input.Flock && logic.FearStats.Band.StrikeLeashed > 0,
                    "a striking man 3 m past his place was not walked back (the leash): striking " + striker.Fear.Striking);
                S.Set(SettingsSchema.FearStrikeReachMetres, 0.0, SettingSources.Mcm); // live: never let go
                sm.Y -= 3;
                body.ByIndex[17299].Y = sm.Y + 1.5;
                for (int k = 0; k < 2; k++) logic.TickCourage(t += 0.5);
                Check(!striker.Fear.Striking, "FearStrikeReachMetres 0: the front man is still let go");
                _ = close;
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        /// <summary>The lone-runner world: a looter group (formation 1, a charge) far behind; a line of 10 fresh legionaries at y 10 (formation 2);
        /// a feverish looter ALONE 3 m BEHIND their line - his target you, 3 m further; a calm one alone 8 m east of him chasing a hero behind it.</summary>
        private static (TrackedAgent lone, TrackedAgent chaser) LoneWorld(AthleticsLogic logic, FakeCourageBody body, int index, double t)
        {
            body.Orders[1] = FearOrder.Charge;
            for (int i = 0; i < 12; i++) FearMan(logic, body, index + i, (i % 4) * 1.5 - 2.25, -3 - (i / 4) * 1.5, 1, 1, legionary: false, morale: 70, bar: 1.0, slot: false);
            for (int i = 0; i < 10; i++) FearMan(logic, body, index + 20 + i, i * 2 - 9, 10, 2, 2, legionary: true, morale: 70, bar: 1.0);
            var lone = FearMan(logic, body, index + 40, 0, 13, 1, 1, legionary: false, morale: 70, bar: 1.0, slot: false);
            lone.Fever.Arm(FeverRules.From(S), SprintOrder.Charge, 100, t, false);
            body.Targets[index + 40] = (0, 16, LoneTarget.Player);
            var chaser = FearMan(logic, body, index + 41, 8, 13, 1, 1, legionary: false, morale: 70, bar: 1.0, slot: false);
            body.Targets[index + 41] = (8, 16, LoneTarget.Hero);
            body.EnemyFronts[1] = (0, 10, 0, -1, 20, 1);
            return (lone, chaser);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void NoLoneRunnerBehindTheLine()
        {
            var keep = _logic;
            try
            {
                // ---- 1. THE FIXES OFF: his fever keeps him behind the line (step 49b) - counted: the fever press, his target you; the chaser: chasing a hero
                var body = new FakeCourageBody();
                var logic = NewStep70Logic(body, courage: true);
                S.Set(SettingsSchema.LoneRunnerEnabled, false, SettingSources.File);
                LogHas("[courage] mission start: a LONE RUNNER (step 70) = an AI man on foot more than 4 m ahead of his own unit's front");
                double t = 73000;
                var (lone, chaser) = LoneWorld(logic, body, 17300, t);
                for (int k = 0; k < 6; k++) logic.TickCourage(t += 0.5);
                var ls = logic.LoneStats;
                Check(lone.Lone.Lone && lone.Lone.Cause == LoneCause.FeverPress && !lone.Courage.Timid && (lone.Input == null || !lone.Input.Regroup),
                    "fixes off: the feverish looter behind the line is not a lone runner held there by his fever: lone " + lone.Lone.Lone + ", cause " + lone.Lone.Cause + ", timid " + lone.Courage.Timid);
                Check(ls.MenOf(LoneCause.FeverPress) == 1 && ls.TargetMen(LoneTarget.Player) == 1 && ls.Seconds(false) > 1, "fixes off: the diagnostic did not count him by the fever press / you as his target");
                Check(chaser.Lone.Lone && ls.TargetMen(LoneTarget.Hero) == 1, "the calm looter alone behind the line chasing a hero is not counted: " + chaser.Lone.Cause);
                Console.WriteLine("        step 70 lone runners, fixes OFF: " + F2s(ls.Seconds(false)) + " man-s - the fever press " + F2s(ls.SecondsOf(LoneCause.FeverPress, false))
                                  + ", " + LoneRunnerMath.CauseName(chaser.Lone.Cause) + " " + F2s(ls.SecondsOf(chaser.Lone.Cause, false)) + "; targets: you " + ls.TargetMen(LoneTarget.Player)
                                  + ", a hero " + ls.TargetMen(LoneTarget.Hero) + "; target reads " + ls.TargetReads);
                LogHas("[courage] first LONE RUNNER by the fever press (step 49b) (step 70): (agent 17340) at ");
                LogHas("his target you (the player) BEHIND the enemy's front (6.0 m past it)");

                // ---- 2. THE FIXES ON (live): no fever press for a man alone - he walks back to his group, round the enemy's line
                S.Set(SettingsSchema.LoneRunnerEnabled, true, SettingSources.Mcm);
                for (int k = 0; k < 2; k++) logic.TickCourage(t += 0.5);
                Check(ls.FeverRefusedLooks > 0 && ls.WalksForced >= 1 && lone.Courage.Phase == CouragePhase.Moving && lone.Input != null && lone.Input.Regroup,
                    "fixes on: the feverish lone looter is not walked back - refused " + ls.FeverRefusedLooks + ", forced " + ls.WalksForced + ", phase " + lone.Courage.Phase);
                LogHas("[courage] first fever press REFUSED for a man alone this battle (step 70): (agent 17340) at ");
                LogHas("[courage] first LONE RUNNER walked back this battle (step 70): (agent 17340) at ");
                var lm = body.ByIndex[17340];
                double before = Math.Sqrt(lm.X * lm.X + (lm.Y + 4.5) * (lm.Y + 4.5));
                t = WalkFearWorld(logic, body, t, 30); // round the line's end (his side kept), then to his group
                double after = Math.Sqrt(lm.X * lm.X + (lm.Y + 4.5) * (lm.Y + 4.5));
                Console.WriteLine("        step 70 lone runners, fixes ON: the feverish looter walked " + F2s(before) + " → " + F2s(after) + " m from his group, now at ("
                                  + F2s(lm.X) + ", " + F2s(lm.Y) + "); walks forced " + ls.WalksForced + ", fever presses refused " + ls.FeverRefusedLooks + " looks");
                Check(after < before && (lm.Y < 10 || Math.Abs(lm.X) > 10), "the lone looter did not come back from behind the line: " + F2s(before) + " → " + F2s(after) + " m, at y " + F2s(lm.Y));

                // ---- 3. HIS SPRINT INTO CONTACT ENDS alone; a charging MASS keeps its sprints and its fever (the first charge's punch)
                body = new FakeCourageBody();
                logic = NewStep70Logic(body, courage: true);
                t = 74000;
                (lone, _) = LoneWorld(logic, body, 17400, t);
                var mass = new List<TrackedAgent>();
                for (int i = 0; i < 6; i++)
                {
                    var m = FearMan(logic, body, 17450 + i, (i % 3) * 1.5 - 8, 6.5 + (i / 3), 1, 1, legionary: false, morale: 70, bar: 1.0, slot: false); // 7 m from the lone man
                    m.Fever.Arm(FeverRules.From(S), SprintOrder.Charge, 100, t, false);
                    mass.Add(m);
                }
                // the first looks with the fixes OFF (his fever keeps him there, no walk, no sprint back), then every sprint into contact starts
                S.Set(SettingsSchema.LoneRunnerEnabled, false, SettingSources.Mcm);
                logic.TickCourage(t += 0.5);
                logic.TickCourage(t += 0.5);
                var sr = SprintRules.From(S);
                var ar = AthleticsRules.From(S);
                foreach (var m in mass.Concat(new[] { lone }))
                {
                    double cap = SprintMath.Capacity(in sr, in ar, m);
                    Check(m.Sprint.TryStartAi(in sr, t, cap, SprintOrder.Charge, intoContact: true) == SprintStart.Started, "precondition: a sprint into contact did not start");
                }
                S.Set(SettingsSchema.LoneRunnerEnabled, true, SettingSources.Mcm);
                logic.TickCourage(t += 0.5);
                for (int k = 0; k < 4; k++) logic.TickSprint(0.1f, t += 0.1, Rules);
                Check(!lone.Sprint.Active && logic.SprintStats.Ends(false, SprintEnd.LoneRunner) == 1 && logic.LoneStats.SprintsEnded == 1,
                    "the lone runner's sprint into contact did not end: active " + lone.Sprint.Active + ", ended lone " + logic.SprintStats.Ends(false, SprintEnd.LoneRunner));
                Check(mass.All(m => m.Sprint.Active), "a charging MASS lost its sprints into contact (the first charge's punch)");
                Check(mass.All(m => !m.Lone.Lone && !m.Courage.Timid) && logic.LoneStats.WalksForced <= 2,
                    "a charging mass in a fever was held back: lone " + mass.Count(m => m.Lone.Lone) + ", timid " + mass.Count(m => m.Courage.Timid));
                Check(logic.LoneStats.MenOf(LoneCause.SprintIntoContact) == 1, "the lone runner's look while sprinting was not filed under the sprint into contact: sprint " + logic.LoneStats.MenOf(LoneCause.SprintIntoContact) + ", fever " + logic.LoneStats.MenOf(LoneCause.FeverPress) + ", chase " + logic.LoneStats.MenOf(LoneCause.ChasingTarget) + ", game " + logic.LoneStats.MenOf(LoneCause.Game) + ", cause now " + lone.Lone.Cause + ", lone " + lone.Lone.Lone);
                LogHas("[sprint] first sprint INTO contact ended for a lone runner this battle (step 70): (agent 17440) at ");
                logic.WriteCourageSummary();
                LogHas("[summary] lone runners (step 70 - the fixes ON; more than 4 m ahead of his unit's front or 1 m past the enemy's front");
                LogHas("the sprint into contact (step 64) ");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        /// <summary>The master switch: off = the lone runner's walk stopped at once (courage off), his fever presses again; the diagnostic still measures.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Step70FollowsTheMasterSwitch()
        {
            var keep = _logic;
            try
            {
                var body = new FakeCourageBody();
                var logic = NewStep70Logic(body, courage: true);
                double t = 75000;
                var (lone, _) = LoneWorld(logic, body, 17500, t);
                for (int k = 0; k < 3; k++) logic.TickCourage(t += 0.5);
                Check(lone.Courage.Phase == CouragePhase.Moving && lone.Input!.Regroup, "precondition: the lone runner is not walked back");
                long looks = logic.LoneStats.Looks;
                int forced = logic.LoneStats.WalksForced;
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                logic.TickCourage(t += 0.5);
                logic.TickCourage(t += 0.5);
                Check(!lone.Courage.Timid && !lone.Input!.Regroup && logic.LoneStats.WalksForced == forced, "mod off: the lone runner's walk back did not stop at once (step 70)");
                Check(logic.LoneStats.Looks > looks, "mod off: the lone-runner diagnostic stopped measuring");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                for (int k = 0; k < 2; k++) logic.TickCourage(t += 0.5);
                Check(lone.Courage.Phase == CouragePhase.Moving && lone.Input!.Regroup, "mod back on: the lone runner is not walked back again (step 70)");
            }
            finally
            {
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        /// <summary>The cost: 1000 men - 500 frightened looters in a deep column against 500 legionaries in a line - the band published and ranked
        /// every 0.5 s, every man's place in it, the lone-runner look - small, no bytes.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Step70CostStaysFlat()
        {
            var keep = _logic;
            try
            {
                var body = new FakeCourageBody();
                var logic = NewStep70Logic(body, courage: true);
                const int men = 1000;
                double t0 = 76000;
                for (int i = 0; i < men / 2; i++)
                    FearMan(logic, body, 18000 + i, (i % 10) * 1.0, -(i / 10) * 1.0, 1, 1, legionary: false, morale: 20, bar: 0.5);
                for (int i = 0; i < men / 2; i++)
                    FearMan(logic, body, 18600 + i, (i % 50) * 1.0 - 20, 6 + (i / 50) * 1.0, 2, 2, legionary: true, morale: 70, bar: 1.0);
                body.EnemyFronts[1] = (4.5, 10.5, 0, -1, 50, 10);
                for (int k = 0; k < 14; k++) logic.TickCourage(t0 += 0.5); // 7 s: every unit's arrays allocated, every first-time line written
                const int ticks = 300;
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var sw = Stopwatch.StartNew();
                for (int k = 1; k <= ticks; k++) logic.TickCourage(t0 + k / 60.0);
                sw.Stop();
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double msPerTick = sw.Elapsed.TotalMilliseconds / ticks;
                var b = logic.FearStats.Band;
                Console.WriteLine("        step 70 cost: " + men + " men (" + logic.FearNow + " in a fear spell, places slid " + b.Kind(FearBandPlaceKind.Slid) + ", compressed "
                                  + b.Kind(FearBandPlaceKind.Compressed) + ", lone looks " + logic.LoneStats.Looks + ") - the courage + fear tick "
                                  + msPerTick.ToString("0.000", CultureInfo.InvariantCulture) + " ms (the stand-in's scans are plain loops over all 1000), " + allocated + " bytes over " + ticks + " ticks");
                Check(msPerTick < 3.0, "cost: the courage + fear tick with step 70 over " + men + " men took " + msPerTick + " ms (budget 3 ms with the stand-in's O(n) reads)");
                Check(allocated < 256 * 1024, "cost: step 70 allocates - " + allocated + " bytes over " + ticks + " ticks");
                Check(logic.FearNow > 0 && b.Kind(FearBandPlaceKind.Compressed) + b.Kind(FearBandPlaceKind.Slid) > 0, "cost: the column is not in the band");
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

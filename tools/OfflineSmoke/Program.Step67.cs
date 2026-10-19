using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using TraxCombat.Core;
using TraxCombat.Hud;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Step 67 (Anton 2026-10-19): THE FEAR FLOCK through the real logic with courage's stand-in world (men on a plane, the stand-in playing
    /// <see cref="IFearBody"/> too) - a fresh unit obeys exactly; frightened looters leave a thin line for their mass and the shelter edges back
    /// at the hold's cap; a fresh comrade in front anchors a man, a tired one lets him go; courage's walk back goes first; a fever press ends a
    /// spell; a far man sprints back; a shield wall holds until its morale breaks; a charge is a loose mass; the switch and the master switch;
    /// the A / B (ON vs OFF spread); the cost. And THE MORALE EYES through the real views - the blue bar under the near men's bars, the near
    /// formations' "morale 64, fresh 41%". What it cannot check: how the engine walks a man on a flock vector among his mates (PLAYTEST D32).
    /// </summary>
    internal static partial class Program
    {
        private static void FearDefaults()
        {
            S.Set(SettingsSchema.FearFlockEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.FearMoraleCalm, 60, SettingSources.File);
            S.Set(SettingsSchema.FearMoraleBroken, 15, SettingSources.File);
            S.Set(SettingsSchema.FearFreshPercent, 75, SettingSources.File);
            S.Set(SettingsSchema.FearMoraleWeightPercent, 100, SettingSources.File);
            S.Set(SettingsSchema.FearTiredWeightPercent, 100, SettingSources.File);
            S.Set(SettingsSchema.DisciplineLevelLow, 6, SettingSources.File);
            S.Set(SettingsSchema.DisciplineLevelHigh, 26, SettingSources.File);
            S.Set(SettingsSchema.DisciplineMaxPercent, 70, SettingSources.File);
            S.Set(SettingsSchema.FearUnitSharePercent, 50, SettingSources.File);
            S.Set(SettingsSchema.FearThinLinePercent, 25, SettingSources.File);
            S.Set(SettingsSchema.FearStartPercent, 15, SettingSources.File);
            S.Set(SettingsSchema.FearFullPercent, 80, SettingSources.File);
            S.Set(SettingsSchema.FearShelterBackMetres, 4.0, SettingSources.File);
            S.Set(SettingsSchema.FearShelterMaxMetres, 12.0, SettingSources.File);
            S.Set(SettingsSchema.FearDriftMetresPerMinute, 6.0, SettingSources.File);
            S.Set(SettingsSchema.FearDriftHoldPercent, 25, SettingSources.File);
            S.Set(SettingsSchema.FearCoverWidthMetres, 0.9, SettingSources.File);
            S.Set(SettingsSchema.FearAnchorFreshPercent, 60, SettingSources.File);
            S.Set(SettingsSchema.FearAnchorSeconds, 20.0, SettingSources.File);
            S.Set(SettingsSchema.FearSprintMetres, 8.0, SettingSources.File);
            S.Set(SettingsSchema.FearSprintChancePercent, 10, SettingSources.File);
            S.Set(SettingsSchema.FearSprintKeepPercent, 50, SettingSources.File);
            S.Set(SettingsSchema.FearWallBreakMorale, 30, SettingSources.File);
            S.Set(SettingsSchema.FearChargeLooseMetres, 12.0, SettingSources.File);
        }

        /// <summary>A fresh fear world: Athletics, the sprint, the fever, courage (OFF unless <paramref name="courage"/> - the flock decides
        /// alone), the fear flock on.</summary>
        private static AthleticsLogic NewFearLogic(FakeCourageBody body, bool courage = false)
        {
            AthleticsDefaults();
            SprintDefaults();
            FeverDefaults();
            CourageDefaults();
            FearDefaults();
            S.Set(SettingsSchema.CourageEnabled, courage, SettingSources.File);
            return NewCourageLogic(body);
        }

        private static TrackedAgent FearMan(AthleticsLogic logic, FakeCourageBody body, int index, double x, double y, int team, int formation, bool legionary,
            double morale, double bar, bool slot = true)
        {
            var st = logic.Track(FakeAgent(index))!;
            st.AthleticsSkill = 100;
            var m = body.Add(st, x, y, team, formation, legionary ? LegionaryArmour : LooterArmour, legionary ? LegionaryLevel : LooterLevel);
            m.Morale = morale;
            if (slot)
            {
                m.SlotX = x;
                m.SlotY = y;
            }
            SetBar(st, bar);
            return st;
        }

        /// <summary>The stand-in's men walk their flock (or walk-back) vectors at 1.5 m/s (their frame = the world's) for <paramref name="seconds"/>.</summary>
        private static double WalkFearWorld(AthleticsLogic logic, FakeCourageBody body, double t, double seconds, double dt = 0.1)
        {
            for (double s = 0; s < seconds - 1e-9; s += dt)
            {
                foreach (var m in body.Men)
                {
                    var input = m.St.Input;
                    if (input == null) continue;
                    if (input.Regroup)
                    {
                        m.X += input.RegroupX * 1.5 * dt;
                        m.Y += input.RegroupY * 1.5 * dt;
                    }
                    else if (input.Flock)
                    {
                        m.X += input.FlockX * 1.5 * dt;
                        m.Y += input.FlockY * 1.5 * dt;
                    }
                }
                logic.TickCourage(t += dt);
            }
            return t;
        }

        private static double Spread(FakeCourageBody body, int formation)
        {
            double sx = 0, sy = 0;
            int n = 0;
            foreach (var m in body.Men)
                if (m.Formation == formation)
                {
                    sx += m.X;
                    sy += m.Y;
                    n++;
                }
            double cx = sx / n, cy = sy / n, d = 0;
            foreach (var m in body.Men)
                if (m.Formation == formation) d += Math.Sqrt((m.X - cx) * (m.X - cx) + (m.Y - cy) * (m.Y - cy));
            return d / n;
        }

        // ------------------------------------------------------------------ the fear flock through the logic

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FearFlockThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                // ---- 1. A FRESH UNIT OBEYS EXACTLY; FRIGHTENED LOOTERS LEAVE THEIR THIN LINE FOR THE MASS, THE SHELTER EDGES BACK AT THE HOLD'S CAP
                var body = new FakeCourageBody();
                var logic = NewFearLogic(body);
                LogHas("[fear] mission start: fear flock: fear = 1 - (1 - 100% x morale below 60 (full at 15)) x (1 - 100% x the bar below 75%), x (1 - discipline: 0 to level 6, 70% from level 26)");
                LogHas("(a looter at morale 20 and half a bar 1.00, a legionary 0.20)");
                double t = 30000;
                var looters = new List<TrackedAgent>();
                for (int i = 0; i < 10; i++) looters.Add(FearMan(logic, body, 6700 + i, i, 0, 1, 1, legionary: false, morale: 20, bar: 0.5)); // a single rank, facing north
                var legion = new List<TrackedAgent>();
                for (int i = 0; i < 10; i++) legion.Add(FearMan(logic, body, 6720 + i, (i % 5) * 2 + 0.5, 3 + (i / 5), 2, 2, legionary: true, morale: 70, bar: 1.0)); // two fresh ranks 3 m north
                double before = Spread(body, 1);
                logic.TickCourage(t += 0.5);
                var first = looters[0];
                Check(first.Fear.Flocking && first.Input != null && first.Input.Flock && first.Input.FlockX > 0.3f,
                    "a frightened looter at the line's end did not head for his mass: " + first.Fear.Phase + " " + (first.Input == null ? "no input" : first.Input.FlockX + ", " + first.Input.FlockY));
                Check(Math.Abs(first.Fear.Fear - 1.0) < 1e-6 && Math.Abs(first.Fear.Pull - 1.0) < 1e-6, "his fear / pull at morale 20, half a bar, nobody behind: " + first.Fear.Fear + " / " + first.Fear.Pull);
                foreach (var l in legion)
                    Check(!l.Fear.Flocking && (l.Input == null || !l.Input.Flock), "a FRESH legionary (morale 70, full bar, discipline 0.7) flocks - a fresh unit obeys exactly: fear " + l.Fear.Fear);
                LogHas("[fear] first fear-flock spell this battle: (agent 6700) at ");
                LogHas(", level 5 (discipline 0.00), an enemy ");
                int key = FearMath.UnitKey(1, 1);
                t = WalkFearWorld(logic, body, t, 20);
                double after = Spread(body, 1);
                Check(after < before * 0.6, "the frightened line did not draw into a mass: spread " + F2s(before) + " → " + F2s(after) + " m");
                var units = logic.FearUnits;
                Check(units.Live(key, t) && units.HasShelter[key] && units.HasEnemy[key] && units.AwayY[key] < -0.99, "the looters' unit: no shelter / enemy direction published");
                double back = -units.ShelterY[key];
                Check(back > 0.3 && back < 0.6, "the shelter did not edge back at the hold's cap (6 m a minute x 25% = 0.5 m in 20 s): " + F2s(back) + " m");
                Check(logic.FearStats.SteerUpdates > 0 && logic.FearStats.StandUpdates >= 0 && logic.FearNow >= 10, "the steering did not run: " + logic.FearStats.SteerUpdates);

                // ---- 2. THE INPUT FILTER: the flock's vector replaces his own; courage's walk back wins over it; off with the switch
                var probe = new AiInputState { Flock = true, FlockX = 0.5f, FlockY = -0.5f };
                uint flags = 0x1u;
                float vx = 0f, vy = 1f;
                AiInputHook.Apply(first, probe, true, ref flags, ref vx, ref vy);
                Check((flags & 0xFu) == 0 && Math.Abs(vx - 0.5f) < 1e-6f && Math.Abs(vy + 0.5f) < 1e-6f, "the flock's frame did not replace his walk: " + vx + ", " + vy);
                probe.Regroup = true;
                probe.RegroupX = -1f;
                probe.RegroupY = 0f;
                S.Set(SettingsSchema.CourageEnabled, true, SettingSources.Mcm);
                vx = 0f;
                vy = 1f;
                AiInputHook.Apply(first, probe, true, ref flags, ref vx, ref vy);
                Check(Math.Abs(vx + 1f) < 1e-6f, "courage's walk back did not win over the flock in the frame: " + vx);
                S.Set(SettingsSchema.CourageEnabled, false, SettingSources.Mcm);
                probe.Regroup = false;
                S.Set(SettingsSchema.FearFlockEnabled, false, SettingSources.Mcm);
                vx = 0f;
                vy = 1f;
                AiInputHook.Apply(first, probe, true, ref flags, ref vx, ref vy);
                Check(Math.Abs(vy - 1f) < 1e-6f, "FearFlockEnabled off: the frame still wrote the flock's vector");
                S.Set(SettingsSchema.FearFlockEnabled, true, SettingSources.Mcm);

                // ---- 3. A FEVER PRESSES: an armed fever (no shock) ends his spell - the charge goes in
                var hot = looters[5];
                Check(hot.Fear.Flocking, "precondition: looter 5 flocks");
                hot.Fever.Arm(FeverRules.From(S), SprintOrder.Charge, 100, t, false);
                logic.TickCourage(t += 0.5);
                Check(!hot.Fear.Flocking && !hot.Input!.Flock && logic.FearStats.Ended(FearEnd.FeverPress) >= 1, "an armed fever did not end the fear spell");
                hot.Fever.Stop();

                // ---- 4. THE SWITCH: off = every spell let go at once (their places again), measured on; on = flocking again
                S.Set(SettingsSchema.FearFlockEnabled, false, SettingSources.Mcm);
                logic.TickCourage(t += 0.02);
                Check(logic.FearNow == 0 && logic.FearSteeringNow == 0 && !first.Input!.Flock && !first.Fear.Flocking, "FearFlockEnabled off: the flock was not let go at once");
                LogHas("[fear] FearFlockEnabled switched OFF mid-mission at ");
                long offLooks = logic.FearStats.Looks(false);
                logic.TickCourage(t += 0.5);
                Check(logic.FearStats.Looks(false) > offLooks, "FearFlockEnabled off: the looks stopped measuring");
                S.Set(SettingsSchema.FearFlockEnabled, true, SettingSources.Mcm);
                logic.TickCourage(t += 0.5);
                Check(first.Fear.Flocking && first.Input!.Flock, "FearFlockEnabled on: the looters do not flock again");
                logic.WriteCourageSummary();
                LogHas("[summary] fear flock shape (every unit every 0.5 s): spread - its men's mean distance from its centre - ON ");
                LogHas("[summary] fear flock drift - a unit's centre away from the enemy near it, metres a minute (+ = back): hold / wall ON ");
                LogHas("[summary] fear flock by discipline: green (discipline < 0.25): ");
                LogHas("[summary] fear flock spells: ");
                LogHas("fever presses 1");

                // ---- 5. AN ANCHOR: a fresh comrade between him and the enemy - he holds his spot; the comrade tires - the pull comes back
                body = new FakeCourageBody();
                logic = NewFearLogic(body);
                t = 31000;
                var behind = FearMan(logic, body, 6800, 0, 0, 1, 1, legionary: false, morale: 20, bar: 0.5);
                var front = FearMan(logic, body, 6801, 0, 1.2, 1, 1, legionary: false, morale: 70, bar: 1.0);
                FearMan(logic, body, 6802, 0, 4, 2, 2, legionary: true, morale: 70, bar: 1.0);
                logic.TickCourage(t += 0.5);
                Check(behind.Fear.Phase == FearPhase.Anchored && behind.Fear.Covered && logic.FearStats.Anchors == 1
                      && behind.Input!.Flock && behind.Input.FlockX == 0f && behind.Input.FlockY == 0f,
                    "a frightened man behind a FRESH comrade is not anchored in his spot: " + behind.Fear.Phase + " covered " + behind.Fear.Covered);
                LogHas("[fear] first ANCHOR this battle: (agent 6800) at ");
                SetBar(front, 0.3);
                logic.TickCourage(t += 0.5);
                Check(behind.Fear.Phase == FearPhase.Flocking && logic.FearStats.AnchorsBroken == 1, "the comrade tired (30%): the anchor did not let him go - " + behind.Fear.Phase);

                // ---- 6. COURAGE'S WALK BACK GOES FIRST: a lone frightened looter among five metal men walks back by courage, the flock yields
                body = new FakeCourageBody();
                logic = NewFearLogic(body, courage: true);
                t = 32000;
                var lone = FearMan(logic, body, 6900, 0, 0, 1, 1, legionary: false, morale: 20, bar: 0.5);
                for (int i = 0; i < 6; i++) FearMan(logic, body, 6901 + i, -2 + i * 0.8, -16 + (i % 2), 1, 1, legionary: false, morale: 20, bar: 0.5);
                for (int i = 0; i < 5; i++) FearMan(logic, body, 6910 + i, -2 + i, 2, 2, 2, legionary: true, morale: 70, bar: 1.0);
                logic.TickCourage(t += 0.5);
                Check(lone.Courage.Phase == CouragePhase.Moving && lone.Input!.Regroup && !lone.Input.Flock && lone.Fear.Flocking && logic.FearStats.YieldedLooks >= 1,
                    "courage's walk back did not take the frightened lone looter (the flock yields): courage " + lone.Courage.Phase + ", flock wish " + lone.Input?.Flock);

                // ---- 7. A FAR MAN SPRINTS BACK to his place among his mates (beyond 8 m), keeping half his store
                body = new FakeCourageBody();
                logic = NewFearLogic(body);
                t = 33000;
                for (int i = 0; i < 8; i++) FearMan(logic, body, 7000 + i, (i % 4) * 0.8, (i / 4) * 0.8, 1, 1, legionary: false, morale: 5, bar: 1.0, slot: false);
                var straggler = FearMan(logic, body, 7010, 0, -16, 1, 1, legionary: false, morale: 5, bar: 1.0, slot: false);
                logic.TickCourage(t += 0.5);
                Check(straggler.Fear.Flocking && straggler.Sprint.Active && straggler.Sprint.Regroup && straggler.Fear.SprintOurs && logic.FearStats.SprintsStarted >= 1,
                    "a frightened man 14 m from his mass did not sprint back: sprint " + straggler.Sprint.Active + ", asked " + logic.FearStats.SprintsAsked);
                Check(Math.Abs(straggler.Sprint.Length - 2.5) < 1e-6, "his sprint is not the half above the 50% reserve (2.5 s of 5): " + straggler.Sprint.Length);
                LogHas("[fear] first fear-flock sprint back this battle: (agent 7010) at ");
                t = WalkFearWorld(logic, body, t, 12);
                Check(!straggler.Sprint.Active, "the flock's sprint did not end (store spent or arrived)");

                // ---- 8. A SHIELD WALL HOLDS UNTIL ITS MORALE BREAKS (below 30); then its frightened men flock
                body = new FakeCourageBody();
                logic = NewFearLogic(body);
                t = 34000;
                body.Orders[3] = FearOrder.Wall;
                var wall = new List<TrackedAgent>();
                for (int i = 0; i < 6; i++) wall.Add(FearMan(logic, body, 7100 + i, i, 0, 1, 3, legionary: false, morale: 40, bar: 1.0));
                for (int i = 0; i < 4; i++) FearMan(logic, body, 7110 + i, i + 1, 2.5, 2, 4, legionary: true, morale: 70, bar: 1.0);
                for (int k = 0; k < 3; k++) logic.TickCourage(t += 0.5);
                Check(!wall[0].Fear.Flocking && wall[0].Fear.Fear > 0.15 && !logic.FearWallBreaks(wall[0], t),
                    "a frightened wall at morale 40 (above 30) did not hold: flocking " + wall[0].Fear.Flocking + ", fear " + wall[0].Fear.Fear);
                foreach (var w in wall) body.ByIndex[w.AgentIndex].Morale = 25;
                for (int k = 0; k < 3; k++) logic.TickCourage(t += 0.5);
                Check(logic.FearWallBreaks(wall[0], t) && wall[0].Fear.Flocking, "the wall at morale 25 did not break into the flock: " + logic.FearWallBreaks(wall[0], t));

                // ---- 9. A CHARGE IS A LOOSE MASS: men near the shelter left to their AI's charge, a man far out pulled in
                body = new FakeCourageBody();
                logic = NewFearLogic(body);
                t = 35000;
                body.Orders[5] = FearOrder.Charge;
                var mass = new List<TrackedAgent>();
                for (int i = 0; i < 6; i++) mass.Add(FearMan(logic, body, 7200 + i, (i % 3) * 0.6, (i / 3) * 0.6, 1, 5, legionary: false, morale: 30, bar: 0.6, slot: false));
                var roamer = FearMan(logic, body, 7210, 9, 0, 1, 5, legionary: false, morale: 30, bar: 0.6, slot: false);
                logic.TickCourage(t += 0.5);
                Check(mass[0].Fear.Flocking && mass[0].Fear.Loose && (mass[0].Input == null || !mass[0].Input.Flock),
                    "a charging man inside the loose mass is steered (he should be left to his AI)");
                Check(roamer.Fear.Flocking && !roamer.Fear.Loose && roamer.Input!.Flock && roamer.Input.FlockX < -0.3f, "a charging man 8 m out was not pulled back in");
                Check(logic.FearStats.LooseLooks >= 6, "the loose looks were not counted: " + logic.FearStats.LooseLooks);
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        /// <summary>The A / B: one frightened single rank of looters facing a fresh line, 30 s with the flock ON (in one logic) and OFF (in another,
        /// the same world) - the summary's spread ON well below OFF.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FearFlockOnAndOff()
        {
            var keep = _logic;
            try
            {
                double spreadOn = 0, spreadOff = 0;
                for (int pass = 0; pass < 2; pass++)
                {
                    bool on = pass == 0;
                    var body = new FakeCourageBody();
                    var logic = NewFearLogic(body);
                    S.Set(SettingsSchema.FearFlockEnabled, on, SettingSources.File);
                    double t = 36000 + pass * 1000;
                    for (int i = 0; i < 12; i++) FearMan(logic, body, 7300 + i, i * 1.0, 0, 1, 1, legionary: false, morale: 30, bar: 0.6);
                    for (int i = 0; i < 12; i++) FearMan(logic, body, 7320 + i, i * 1.0, 2.5, 2, 2, legionary: true, morale: 70, bar: 1.0);
                    WalkFearWorld(logic, body, t, 30);
                    var st = logic.FearStats;
                    if (on) spreadOn = st.MeanSpread(true);
                    else spreadOff = st.MeanSpread(false);
                    Check(st.Looks(on) > 0 && st.Looks(!on) == 0, "the A / B: the looks were not filed by the switch (" + st.Looks(true) + " ON, " + st.Looks(false) + " OFF)");
                }
                Console.WriteLine("        step 67 A / B: a frightened single rank of 12 looters (morale 30, 60% bar) facing 12 fresh legionaries for 30 s - the units' spread ON "
                                  + F2s(spreadOn) + " m, OFF " + F2s(spreadOff) + " m");
                Check(spreadOn < spreadOff * 0.8, "the A / B: the flock did not draw the line together (ON " + F2s(spreadOn) + " m vs OFF " + F2s(spreadOff) + " m)");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        /// <summary>The master switch: off = every flocking man let go at once (his place again); on = flocking again.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FearFollowsTheMasterSwitch()
        {
            var keep = _logic;
            try
            {
                var body = new FakeCourageBody();
                var logic = NewFearLogic(body);
                double t = 38000;
                var a = FearMan(logic, body, 7400, 0, 0, 1, 1, legionary: false, morale: 10, bar: 0.3);
                FearMan(logic, body, 7401, 3, 0, 1, 1, legionary: false, morale: 10, bar: 0.3);
                logic.TickCourage(t += 0.5);
                Check(a.Fear.Flocking && a.Input!.Flock, "precondition: the frightened looter does not flock");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                logic.TickCourage(t += 0.02);
                Check(!a.Fear.Flocking && !a.Input.Flock && logic.FearNow == 0, "mod off: the flocking man was not let go at once (step 67)");
                LogHas("[fear] the whole mod (ModEnabled) switched OFF mid-mission at ");
                uint flags = 0x1u;
                float vx = 0f, vy = 1f;
                var probe = new AiInputState { Flock = true, FlockX = 0.7f };
                Check(!AiInputHook.ApplyFlock(probe, ref flags, ref vx, ref vy), "mod off: the hook still writes the flock's vector");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                logic.TickCourage(t += 0.5);
                Check(a.Fear.Flocking && a.Input.Flock, "mod back on: the frightened looter does not flock again (step 67)");
            }
            finally
            {
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
            }
        }

        /// <summary>The cost: 1000 men in two blocks face to face (half frightened looters flocking, courage on), every man looked at every 0.5 s,
        /// the flockers steered every 0.1 s - small and allocating nothing.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FearCostStaysFlat()
        {
            var keep = _logic;
            try
            {
                var body = new FakeCourageBody();
                var logic = NewFearLogic(body, courage: true);
                const int men = 1000;
                double t0 = 39000;
                for (int i = 0; i < men; i++)
                {
                    bool south = i < men / 2;
                    int j = south ? i : i - men / 2;
                    double x = (j % 50) * 1.2, y = (j / 50) * 1.2;
                    FearMan(logic, body, 12000 + i, x, south ? -0.75 - y : 0.75 + y, south ? 1 : 2, south ? 1 : 2, legionary: !south, morale: south ? 20 : 70, bar: south ? 0.5 : 1.0);
                }
                for (int k = 0; k < 4; k++) logic.TickCourage(t0 += 0.5); // everyone looked at, the first lines written, the lists grown
                const int ticks = 300;
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var sw = Stopwatch.StartNew();
                for (int k = 1; k <= ticks; k++) logic.TickCourage(t0 + k / 60.0);
                sw.Stop();
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double msPerTick = sw.Elapsed.TotalMilliseconds / ticks;
                Console.WriteLine("        step 67 cost: " + men + " men in two blocks (" + logic.FearNow + " in a fear spell, " + logic.FearSteeringNow + " steered, "
                                  + logic.TimidNow + " timid) - the courage + fear tick " + msPerTick.ToString("0.000", CultureInfo.InvariantCulture)
                                  + " ms (the stand-in's scan is a plain loop over all 1000), " + allocated + " bytes over " + ticks + " ticks");
                Check(msPerTick < 3.0, "cost: the courage + fear tick over " + men + " men took " + msPerTick + " ms (budget 3 ms with the stand-in's O(n) scan)");
                Check(allocated < 256 * 1024, "cost: the fear flock allocates - " + allocated + " bytes over " + ticks + " ticks");
                Check(logic.FearNow > 0, "cost: the frightened looters are not flocking");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        // ------------------------------------------------------------------ the morale eyes

        /// <summary>The blue morale bar at the bottom of the near men's stacks, through the real FeverMarkView over the real logic's men.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void MoraleBarsThroughTheView()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                SprintDefaults();
                FeverDefaults();
                NearMarkDefaults();
                S.Set(SettingsSchema.SprintAiEnabled, false, SettingSources.File);
                S.Set(SettingsSchema.ShowMoraleMarks, true, SettingSources.File);
                var body = new FakeSprintBody();
                var logic = NewFeverLogic(body, new FakeWeaponFacts(), 0.99);
                var src = new FakeFeverMarks();
                double t = 9900;
                var a = FeverMan(logic, body, src, 400, 2, 0f, 10f);
                a.Morale = 64;
                var b = FeverMan(logic, body, src, 401, 2, 1f, 8f);
                b.Morale = -1;                                                                   // the game keeps no morale for him
                var e = FeverMan(logic, body, src, 402, 3, 0.5f, 13f, (int)MarkerTeam.Enemy);
                e.Morale = 22;
                var me = FeverMan(logic, body, src, 409, 0, 0f, 3f);
                logic.SmokePlayer = me.Agent;
                var player = me.Agent;
                var view = new FeverMarkView();
                var layer = new FakeHudLayer();
                view.UseLayer(layer);
                view.UseSource(src);
                src.Shown = true;

                view.Tick(Frame(t, player));
                var ma = MarkOf(view, 400)!;
                var mb = MarkOf(view, 401)!;
                var mE = MarkOf(view, 402)!;
                float gap = S.AthleticsMarkGap;
                Check(ma != null && ma.MoraleShown && Math.Abs(ma.MoraleFill - 0.64f) < 1e-4f && Math.Abs(ma.FeverOffsetY + (ma.MoraleHeight + gap)) < 1e-3f
                      && Math.Abs(ma.MoraleWidth - ma.BarWidth) < 1e-3f,
                    "the morale bar: not shown / 0.64 / the fever bar lifted by its height + the gap: " + (ma == null ? "no mark" : ma.MoraleShown + " " + ma.MoraleFill + " " + ma.FeverOffsetY));
                Check(Math.Abs(ma.MoraleColor.Blue - 1f) < 1e-3f && ma.MoraleColor.Red < 0.4f && Math.Abs(ma.MoraleColor.Alpha - 1f) < 1e-3f, "the morale bar is not the clear blue");
                Check(mb != null && !mb.MoraleShown && Math.Abs(mb.BoxHeight - ma.BoxHeight) < 1e-3f, "no morale known: the bar shows, or its place is not kept");
                Check(mE != null && mE.MoraleShown && Math.Abs(mE.MoraleFill - 0.22f) < 1e-4f, "the enemy's morale bar (AthleticsMarksShowEnemy on) is missing");
                Check(view.MoraleBarsNow == 2, "morale bars drawn: " + view.MoraleBarsNow);
                LogHas("; morale bars (blue, 0-100) under the bars, the enemy's too - 2 drawn");
                float withMorale = ma.BoxHeight;

                S.Set(SettingsSchema.AthleticsMarksShowEnemy, false, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                Check(MarkOf(view, 402) == null || !MarkOf(view, 402)!.MoraleShown, "AthleticsMarksShowEnemy off: the enemy's morale bar stayed");
                S.Set(SettingsSchema.AthleticsMarksShowEnemy, true, SettingSources.Mcm);

                S.Set(SettingsSchema.ShowMoraleMarks, false, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                ma = MarkOf(view, 400)!;
                Check(!ma.MoraleShown && ma.FeverOffsetY == 0f && ma.BoxHeight < withMorale - 1f, "ShowMoraleMarks off: the morale bar / its place stayed");
                S.Set(SettingsSchema.ShowMoraleMarks, true, SettingSources.Mcm);
                // only the morale bars on: the view still runs and marks the men
                S.Set(SettingsSchema.ShowAthleticsMarks, false, SettingSources.Mcm);
                S.Set(SettingsSchema.ShowFeverMarks, false, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                ma = MarkOf(view, 400)!;
                Check(layer.Up && ma != null && ma.MoraleShown && !ma.BarShown, "only the morale bars on: no mark");
                S.Set(SettingsSchema.ShowAthleticsMarks, true, SettingSources.Mcm);
                S.Set(SettingsSchema.ShowFeverMarks, true, SettingSources.Mcm);

                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                Check(!layer.Up && view.LastDecision == HudHide.ModOff, "ModEnabled off: the morale bars stayed");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                view.Tick(Frame(t += 0.05, player));
                Check(layer.Up && MarkOf(view, 400)!.MoraleShown, "ModEnabled on: the morale bar did not come back");

                view.Finish(t += 0.5);
                WriteSummaryThrough(logic, view);
                LogHas("; morale bars a frame (step 67): mean ");
                logic.SmokePlayer = null;
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
                FeverMarkDefaults();
                ConfigStore.Reload("after the morale bars smoke");
            }
        }

        /// <summary>The near formations' morale line through the real ALT labels.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void MoraleLineUnderTheMarkers()
        {
            AltMarkerDefaults();
            ReadyDefaults();
            S.Set(SettingsSchema.ShowMoraleOnLabels, true, SettingSources.File);
            S.Set(SettingsSchema.MoraleLabelMetres, 60.0, SettingSources.File);
            S.Set(SettingsSchema.MoraleLabelMax, 1, SettingSources.File);
            var logic = StripLogic();
            var player = FakeAgent(46);
            var live = new FakeMarkers { Shown = true };
            live.Add(0, 0, 0, 960, 400, 40, 0.41, 0.08, 0.50, 0.81);
            live.Add(1, 2, 0, 1300, 350, 80, 0.10, 0.10, 0.10, 0.90);
            live.Add(0, 0, 1, 600, 500, 20, 0.80, 0.05, 1.00, 1.00);
            void Set(int i, float distance, double morale)
            {
                var m = live.Markers[i];
                var s = m.Stats;
                m.Stats = new FormationAthleticsStats(s.Count, s.MeanPoints, s.StdPoints, s.MeanFraction, s.StdFraction, s.Exhausted, s.MeanPeakShare, s.InPeak, s.MeanHealth,
                    s.Bracing, s.InFever, morale);
                m.Distance = distance;
                live.Markers[i] = m;
            }
            Set(0, 30, 64.2);   // yours, the nearest
            Set(1, 45, 22.0);   // the enemy's
            Set(2, 90, 50.0);   // yours, too far
            var projection = new FakeMarkers { State = MarkerRead.Projected, Name = "stand-in projection" };
            var view = new AltMarkerView();
            var layer = new FakeHudLayer();
            view.UseLayer(layer);
            view.UseSources(live, projection);
            double t = 1;
            view.Tick(Frame(t, player));
            var l = view.CurrentViewModel!.Labels;
            Check(layer.Up && l.Count == 3, "morale line: 3 labels expected, got " + l.Count);
            Check(l[0].MoraleShown && l[0].MoraleText == "morale 64, fresh 41%" && !l[1].MoraleShown && !l[2].MoraleShown,
                "morale line, the nearest 1: '" + l[0].MoraleText + "' " + l[0].MoraleShown + " / " + l[1].MoraleShown + " / " + l[2].MoraleShown);
            S.Set(SettingsSchema.MoraleLabelMax, 4, SettingSources.Mcm);
            view.Tick(Frame(t += 0.05, player));
            Check(l[0].MoraleShown && l[1].MoraleShown && l[1].MoraleText == "morale 22, fresh 10%" && !l[2].MoraleShown, "morale line, the nearest 4 within 60 m: '" + l[1].MoraleText + "'");
            S.Set(SettingsSchema.MoraleLabelMetres, 100.0, SettingSources.Mcm);
            view.Tick(Frame(t += 0.05, player));
            Check(l[2].MoraleShown && l[2].MoraleText == "morale 50, fresh 80%", "morale line within 100 m: the 90 m formation missing");
            S.Set(SettingsSchema.ShowMoraleOnLabels, false, SettingSources.Mcm);
            view.Tick(Frame(t += 0.05, player));
            Check(!l[0].MoraleShown && !l[1].MoraleShown && !l[2].MoraleShown && l[0].IsShown, "ShowMoraleOnLabels off: the line stayed (or the label went)");
            S.Set(SettingsSchema.ShowMoraleOnLabels, true, SettingSources.Mcm);
            view.Tick(Frame(t += 0.05, player));
            Check(l[0].MoraleShown, "ShowMoraleOnLabels on: the line did not come back");
            view.Tick(Frame(t += 0.2, player));
            LogHas(", morale line (\"morale 64, fresh 41%\") on the nearest formation within 60 m");
            S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
            view.Tick(Frame(t += 0.2, player));
            Check(!layer.Up && view.LastDecision == HudHide.ModOff, "morale line: ModEnabled off - the labels stayed");
            S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
            view.Tick(Frame(t += 0.2, player));
            Check(layer.Up && view.CurrentViewModel!.Labels[0].MoraleShown, "morale line: ModEnabled on - the line did not come back");
            view.Finish(t += 0.5);
            WriteSummaryThrough(logic, view);
            LogHas("[summary] hud: ALT markers - morale line (step 67): pushed ");
            LogHas("the lowest morale 22 (enemy 1, fresh 10%); hidden ");
            SetStatic(typeof(AthleticsLogic), "_current", null);
            AltMarkerDefaults();
        }
    }
}

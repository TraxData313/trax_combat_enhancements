using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Step 55 (Anton, 2026-10-18): CAVALRY FLOCKING through the real logic with a stand-in world - riders on a plane (their positions in the
    /// courage stand-in's world, so the timid look scans them), their formations' goals and centres, the scripted points they are sent
    /// (AI_NOTES "Step 55").
    /// </summary>
    internal static partial class Program
    {
        private sealed class FakeRider
        {
            public FakeCourageMan Man = null!;
            public bool Mounted = true;
            public bool Ranged, Thrown;
            public bool Charging = true;
            public bool CannotStart;
            public FlockSkip Gate = FlockSkip.Count;
            public bool Scripted;
            public double TargetX = double.NaN, TargetY = double.NaN;
        }

        private sealed class FakeFlockBody : IFlockBody
        {
            public readonly Dictionary<int, FakeRider> Riders = new Dictionary<int, FakeRider>();
            public readonly Dictionary<int, (double x, double y)> Goals = new Dictionary<int, (double, double)>();
            public bool Allows = true;
            public bool NoGround;
            public int Moves, Releases, Tightened, Restored, KindReads;

            public string Lever => "the smoke's stand-in for Agent.SetScriptedPosition";

            public FakeRider Add(FakeCourageMan man, bool ranged = false, bool thrown = false)
            {
                var r = new FakeRider { Man = man, Ranged = ranged, Thrown = thrown };
                Riders[man.St.AgentIndex] = r;
                return r;
            }

            private FakeRider R(TrackedAgent st) => Riders[st.AgentIndex];

            public bool MissionAllows() => Allows;

            public bool IsActive(TrackedAgent st) => Riders.ContainsKey(st.AgentIndex);

            public bool IsMounted(TrackedAgent st) => R(st).Mounted;

            public FlockSkip Gate(TrackedAgent st, bool flocking)
            {
                var r = R(st);
                if (r.Gate != FlockSkip.Count) return r.Gate;
                return r.Charging ? FlockSkip.Count : FlockSkip.NotCharging;
            }

            public void ReadKind(TrackedAgent st, out bool rangedWithAmmo, out bool thrown)
            {
                KindReads++;
                var r = R(st);
                rangedWithAmmo = r.Ranged;
                thrown = r.Thrown;
            }

            public bool Key(TrackedAgent st, out int team, out int formation)
            {
                var m = R(st).Man;
                team = m.Team;
                formation = m.Formation;
                return true;
            }

            public bool Position(TrackedAgent st, out double x, out double y)
            {
                var m = R(st).Man;
                x = m.X;
                y = m.Y;
                return true;
            }

            public bool FormationCentre(TrackedAgent st, out double x, out double y)
            {
                var me = R(st).Man;
                double sx = 0, sy = 0;
                int n = 0;
                foreach (var r in Riders.Values)
                {
                    if (r.Man.Team != me.Team || r.Man.Formation != me.Formation) continue;
                    sx += r.Man.X;
                    sy += r.Man.Y;
                    n++;
                }
                x = n > 0 ? sx / n : double.NaN;
                y = n > 0 ? sy / n : double.NaN;
                return n > 0;
            }

            public bool Goal(TrackedAgent st, out double x, out double y)
            {
                var m = R(st).Man;
                if (Goals.TryGetValue(m.Team * 16 + m.Formation, out var g))
                {
                    x = g.x;
                    y = g.y;
                    return true;
                }
                x = y = double.NaN;
                return false;
            }

            public bool CanStart(TrackedAgent st) => !R(st).CannotStart;

            public bool MoveTo(TrackedAgent st, double x, double y)
            {
                if (NoGround) return false;
                var r = R(st);
                r.Scripted = true;
                r.TargetX = x;
                r.TargetY = y;
                Moves++;
                return true;
            }

            public void Release(TrackedAgent st)
            {
                R(st).Scripted = false;
                Releases++;
            }

            public void Tighten(TrackedAgent st) => Tightened++;

            public void Restore(TrackedAgent st) => Restored++;
        }

        /// <summary>Step 55: every logic built from here on plays flocking's engine side through a stand-in (the game's reads native agents).</summary>
        private static void UseFakeFlockBody()
        {
            Check(FlockBodyDefault.Current is GameFlockBody, "the default flock body is not the game's: " + FlockBodyDefault.Current.GetType().Name);
            CheckLever();
            FlockBodyDefault.Current = new FakeFlockBody { Allows = false };
            Check(new AthleticsLogic().FlockBody == FlockBodyDefault.Current, "a new logic does not start with the default flock body");
        }

        /// <summary>The game's side of the lever: the engine calls the game body makes exist with these signatures in v1.4.8.</summary>
        private static void CheckLever()
        {
            var agent = typeof(TaleWorlds.MountAndBlade.Agent);
            Check(agent.GetMethod("SetScriptedPosition") != null, "Agent.SetScriptedPosition is gone");
            Check(agent.GetMethod("DisableScriptedMovement") != null, "Agent.DisableScriptedMovement is gone");
            Check(agent.GetMethod("CanBeAssignedForScriptedMovement") != null, "Agent.CanBeAssignedForScriptedMovement is gone");
            Check((int)TaleWorlds.MountAndBlade.Agent.AIScriptedFrameFlags.GoToPosition == 1, "AIScriptedFrameFlags.GoToPosition is not 1");
            var formation = typeof(TaleWorlds.MountAndBlade.Formation);
            Check(formation.GetProperty("TargetFormation") != null && formation.GetProperty("CachedClosestEnemyFormation") != null, "the formation's goal reads are gone");
            Check(typeof(TaleWorlds.MountAndBlade.MovementOrder).GetProperty("TargetFormation") != null, "MovementOrder.TargetFormation is gone");
        }

        private static void FlockDefaults()
        {
            S.Set(SettingsSchema.CavalryFlockEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.CavalryFlockFreeEagerPercent, 75, SettingSources.File);
            S.Set(SettingsSchema.CavalryFlockHysteresisPercent, 5, SettingSources.File);
            S.Set(SettingsSchema.CavalryFlockMidEagerPercent, 40, SettingSources.File);
            S.Set(SettingsSchema.CavalryFlockMidGroupSize, 4, SettingSources.File);
            S.Set(SettingsSchema.CavalryFlockExhaustedSharePercent, 65, SettingSources.File);
            S.Set(SettingsSchema.CavalryFlockTimid, true, SettingSources.File);
            S.Set(SettingsSchema.CavalryFlockTimidRadiusMetres, 15.0, SettingSources.File);
            S.Set(SettingsSchema.CavalryFlockGatherRadiusMetres, 25.0, SettingSources.File);
            S.Set(SettingsSchema.CavalryFlockSeparationMetres, 3.5, SettingSources.File);
            S.Set(SettingsSchema.CavalryFlockLeadSeconds, 2.0, SettingSources.File);
            S.Set(SettingsSchema.CavalryFlockTightenWeights, true, SettingSources.File);
            S.Set(SettingsSchema.CavalryFlockWaveSeconds, 10.0, SettingSources.File);
            S.Set(SettingsSchema.CavalryFlockMaxSeconds, 45.0, SettingSources.File);
        }

        private static AthleticsLogic NewFlockLogic(FakeFlockBody body, FakeCourageBody world)
        {
            var logic = NewCourageLogic(world);
            logic.FlockBody = body;
            return logic;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FlockThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                SprintDefaults();
                FeverDefaults();
                CourageDefaults();
                FlockDefaults();
                var world = new FakeCourageBody();
                var body = new FakeFlockBody();
                var logic = NewFlockLogic(body, world);
                LogHas("[cavalry] mission start: cavalry flocking: every AI rider under a CHARGE order");
                LogHas("lever: the smoke's stand-in for Agent.SetScriptedPosition");
                double t = 30000;
                int idx = 7000;
                TrackedAgent Rider(double x, double y, int team, int formation, double bar, bool ranged = false, bool thrown = false)
                {
                    var st = logic.Track(FakeAgent(idx++))!;
                    st.AthleticsSkill = 100;
                    SetBar(st, bar);
                    var m = world.Add(st, x, y, team, formation, LegionaryArmour, LegionaryLevel);
                    body.Add(m, ranged, thrown);
                    return st;
                }
                FakeRider F(TrackedAgent st) => body.Riders[st.AgentIndex];
                void Census() => logic.TickFlock(t += 0.5);

                // ---- A TIRING UNIT: 20 lancers of formation 1, the enemy east at (500, 0)
                body.Goals[1 * 16 + 1] = (500, 0);
                var unit = new List<TrackedAgent>();
                for (int i = 0; i < 20; i++) unit.Add(Rider((i % 5) * 4, (i / 5) * 4, 1, 1, 0.9));
                // 1. the DANCE: every one eager (90%) - free, the game's own, nothing sent
                Census();
                Check(logic.FlockingNow == 0 && body.Moves == 0 && unit.All(u => u.Flock.State == RiderFlockState.Free), "fresh riders were taken out of the dance");
                Check(logic.FlockStats.Riders == 20 && logic.FlockStats.RidersOf(RiderKind.Melee) == 20 && body.KindReads == 20, "the census did not see 20 melee riders (kinds read once)");
                Census();
                Check(body.KindReads == 20, "the kit was read again");

                // 2. five of them tire to 55-60%: they flock (a group of 5 within 25 m), steered, not yet flipping (3 s)
                var five = unit.Take(5).ToList();
                foreach (var u in five) SetBar(u, 0.55);
                SetBar(five[0], 0.60);
                Census();
                Check(logic.FlockingNow == 5 && five.All(u => u.Flock.Flocking && F(u).Scripted), "five riders at 55-60% did not flock and ride: " + logic.FlockingNow);
                Check(logic.FlockStats.Started(FlockReason.Tired) == 5 && body.Tightened == 5, "the starts / the weights");
                Check(unit.Skip(5).All(u => u.Flock.State == RiderFlockState.Free), "the eager fifteen were touched");
                LogHas("[cavalry] first rider flocking this battle: (agent 7000) (melee riders) at ");
                LogHas("tired: eagerness 60% (bar 60%) below the free line 75%");
                Census();
                Check(logic.FlockStats.Flips == 0, "a flip before 3 s of flocking (the hysteresis)");
                int moves = body.Moves;
                for (int k = 0; k < 6; k++) Census();
                // 3. three seconds on: 5 >= 3 wanted at 56% mean eagerness, the leader at 60% (ready 80%) → the FLIP: a wave of 5
                Check(logic.FlockStats.Flips == 1 && logic.FlockStats.LargestWave == 5 && logic.FlockingNow == 0, "the five did not flip as one wave: flips "
                    + logic.FlockStats.Flips + ", largest " + logic.FlockStats.LargestWave);
                Check(five.All(u => u.Flock.State == RiderFlockState.Wave && !F(u).Scripted), "the wave's riders are not released (scripted walk off)");
                Check(body.Releases == 5 && body.Restored >= 5, "the scripted walks / the weights were not given back: " + body.Releases + " / " + body.Restored);
                Check(body.Moves >= moves, "the flock was not steered while it waited");
                LogHas("[cavalry] first FLIP this battle at ");
                LogHas(": 5 melee riders go in together - 5 flocking within 25 m, their mean eagerness 56% wants 3; the leader's eagerness 60% (ready 80%)");
                // the wave runs 10 s: still tired (55%) they stay in it, then decide again
                Census();
                Check(five.All(u => u.Flock.State == RiderFlockState.Wave), "a wave rider flocked again inside his 10 s");
                for (int k = 0; k < 20; k++) Census();
                Check(five.All(u => u.Flock.Flocking), "after the wave the tired five did not flock again");

                // 4. the WHOLE unit exhausted (15%) in two clusters of 10, 100 m apart - lead 5 s and 1 m slots to read the heading
                S.Set(SettingsSchema.CavalryFlockLeadSeconds, 5.0, SettingSources.Mcm);
                S.Set(SettingsSchema.CavalryFlockSeparationMetres, 1.0, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                LogHas("[cavalry] settings changed mid-mission at ");
                for (int i = 0; i < 20; i++)
                {
                    var m = world.ByIndex[unit[i].AgentIndex];
                    m.X = (i % 5) * 3;
                    m.Y = i < 10 ? (i % 10 / 5) * 3 : 100 + (i % 10 / 5) * 3;
                    SetBar(unit[i], 0.15);
                }
                int flipsBefore = logic.FlockStats.Flips;
                for (int k = 0; k < 8; k++) Census();
                Check(logic.FlockingNow == 20 && logic.FlockStats.Flips == flipsBefore, "exhausted riders flipped without a rested leader, or did not flock: "
                    + logic.FlockingNow + " flocking, " + (logic.FlockStats.Flips - flipsBefore) + " flips");
                // the tired cluster drifts toward the unit's centre (north, y 51.5) more than toward the goal (east)
                var a = unit.Take(10).ToList();
                double cx = a.Average(u => world.ByIndex[u.AgentIndex].X), cy = a.Average(u => world.ByIndex[u.AgentIndex].Y);
                double dx = a.Average(u => F(u).TargetX) - cx, dy = a.Average(u => F(u).TargetY) - cy;
                Check(dy > 10 && dx > 0 && dx < dy, "the exhausted cluster's heading does not lean to the unit's centre with a little of the goal: lead " + dx.ToString("0.0") + ", " + dy.ToString("0.0"));
                // one rider of cluster A rests to 45% - the leader: 10 wanted at 18% mean (65% of 20 = 13 at 0), 10 there → a BIG wave
                SetBar(a[3], 0.45);
                for (int k = 0; k < 2; k++) Census();
                Check(logic.FlockStats.Flips == flipsBefore + 1 && logic.FlockStats.LargestWave == 10, "the exhausted cluster led by its rested rider did not flip as a wave of 10: largest "
                    + logic.FlockStats.LargestWave);
                Check(unit.Skip(10).All(u => u.Flock.Flocking), "the other cluster (no leader) went in too");
                Check(logic.FlockStats.MeanWave(0) == 10 && logic.FlockStats.MeanWave(2) == 5, "the waves by eagerness: small fresh, big tired");
                S.Set(SettingsSchema.CavalryFlockLeadSeconds, 2.0, SettingSources.Mcm);
                S.Set(SettingsSchema.CavalryFlockSeparationMetres, 3.5, SettingSources.Mcm);
                logic.ApplySettingsChange(S);

                // ---- A MIXED FORMATION splits by kind: 4 horse archers and 4 lancers in one spot at 50% - two waves of 4, never one of 8
                body.Goals[1 * 16 + 2] = (500, 300);
                var mixed = new List<TrackedAgent>();
                for (int i = 0; i < 8; i++) mixed.Add(Rider(300 + i, 300, 1, 2, 0.5, ranged: i % 2 == 0));
                int flips = logic.FlockStats.Flips;
                for (int k = 0; k < 8; k++) Census();
                Check(mixed.All(m => m.Flock.KindKnown) && mixed.Count(m => m.Flock.Kind == RiderKind.HorseArcher) == 4, "the kinds were not read from the kit");
                Check(mixed[0].Flock.Key != mixed[1].Flock.Key, "horse archers and lancers of one formation share a flock");
                Check(logic.FlockStats.Flips == flips + 2 && mixed.All(m => m.Flock.State == RiderFlockState.Wave), "the mixed formation did not go in as two waves of 4: "
                    + (logic.FlockStats.Flips - flips) + " flips");

                // ---- A LONE TIMID RIDER: fresh (100%) but alone among 4 enemy legionaries → eagerness 50%, he flocks (timid)
                var lone = Rider(2000, 0, 1, 4, 1.0);
                for (int i = 0; i < 4; i++)
                {
                    var e = logic.Track(FakeAgent(idx++))!;
                    world.Add(e, 2000 + 3 * i - 4.5, 4, 2, 9, LegionaryArmour, LegionaryLevel);
                }
                Census();
                Census();
                Check(lone.Flock.Flocking && lone.Flock.Timid && logic.FlockStats.Started(FlockReason.Timid) >= 1, "a lone fresh rider among 4 enemies did not flock (timid): "
                    + lone.Flock.State + " odds " + lone.Flock.Odds);
                // courage off: no timid factor - free again (100% >= 80%)
                S.Set(SettingsSchema.CourageEnabled, false, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                Census();
                Check(lone.Flock.State == RiderFlockState.Free && logic.FlockStats.Ended(FlockEnd.Rested) >= 1, "courage off: the lone rider is not free again");
                S.Set(SettingsSchema.CourageEnabled, true, SettingSources.Mcm);
                logic.ApplySettingsChange(S);

                // ---- THE PLAYER'S HELD CAVALRY (hold / move / follow / advance keep slots) and YOU: never touched, even exhausted
                var held = Rider(3000, 0, 0, 3, 0.1);
                F(held).Charging = false;
                var you = Rider(3010, 0, 0, 3, 0.1);
                logic.SmokePlayer = you.Agent;
                Census();
                Check(!held.Flock.Flocking && !F(held).Scripted && logic.FlockStats.Skipped(FlockSkip.NotCharging) >= 1, "a held formation's rider was flocked");
                Check(!you.Flock.Flocking && logic.FlockStats.Skipped(FlockSkip.NotAi) >= 1, "YOU were flocked");
                // his formation charges: he flocks; it holds again: let go at once
                F(held).Charging = true;
                Census();
                Check(held.Flock.Flocking, "the formation charging: the exhausted rider did not flock");
                F(held).Charging = false;
                int releases = body.Releases;
                Census();
                Check(!held.Flock.Flocking && body.Releases == releases + 1 && logic.FlockStats.Ended(FlockEnd.Excluded) >= 1, "a hold order did not let the flocker go at once");
                logic.SmokePlayer = null;

                // ---- GIVE UP: an exhausted rider alone of his kind (leader 13% - never ready) is released after 45 s, a wave window after
                var alone = Rider(4000, 0, 3, 5, 0.1);
                Census();
                Check(alone.Flock.Flocking, "the exhausted lone rider did not flock");
                for (int k = 0; k < 92; k++) Census();
                Check(alone.Flock.State == RiderFlockState.Wave && logic.FlockStats.Ended(FlockEnd.GaveUp) >= 1, "the rider who never flipped was not released after 45 s: " + alone.Flock.State);

                // ---- NO GROUND: the point and the centre refused - nothing sent, counted
                body.NoGround = true;
                var ng = Rider(5000, 0, 3, 6, 0.3);
                Census();
                Check(ng.Flock.Flocking && !F(ng).Scripted && logic.FlockStats.MovesNoGround >= 1, "no ground: something was sent, or not counted");
                body.NoGround = false;

                // ---- LONE PASSES and the time between passes (ON): the grouped rider's pass is not lone, the alone one's is
                int passes = logic.FlockStats.Passes(true), loneN = logic.FlockStats.LonePasses(true);
                logic.NoteFlockAttack(unit[15], true, t);
                logic.NoteFlockAttack(unit[15], true, t + 1); // the same pass
                logic.NoteFlockAttack(ng, true, t);
                logic.NoteFlockAttack(unit[15], true, t + 20); // a new pass 20 s later
                Check(logic.FlockStats.Passes(true) == passes + 3 && logic.FlockStats.LonePasses(true) == loneN + 1, "the passes / lone passes ON are off: "
                    + (logic.FlockStats.Passes(true) - passes) + " / " + (logic.FlockStats.LonePasses(true) - loneN));
                Check(logic.FlockStats.PassGaps(true, 0) >= 1, "the 20 s between two passes of an exhausted rider were not filed in the <25% band");
                logic.SmokePlayer = you.Agent;
                logic.NoteFlockAttack(you, true, t); // never the player
                logic.SmokePlayer = null;
                Check(logic.FlockStats.Passes(true) == passes + 3, "your attack counted as a rider's pass");

                // ---- the switch: off mid-flock = everyone let go at once, the census keeps measuring (OFF)
                int restored = body.Restored;
                S.Set(SettingsSchema.CavalryFlockEnabled, false, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                logic.TickFlock(t += 0.1);
                Check(logic.FlockingNow == 0 && body.Riders.Values.All(r => !r.Scripted) && body.Restored > restored, "CavalryFlockEnabled off: the flockers were not let go at once");
                LogHas("[cavalry] CavalryFlockEnabled switched OFF mid-mission at ");
                int offCensus = logic.FlockStats.Censuses(false);
                Census();
                Census();
                Check(logic.FlockStats.Censuses(false) > offCensus && logic.FlockStats.SpreadSamples(false) > 0 && logic.FlockingNow == 0, "OFF: the census did not measure (or flocked)");
                logic.NoteFlockAttack(unit[15], true, t + 100);
                Check(logic.FlockStats.Passes(false) >= 1, "OFF: a pass was not measured");
                S.Set(SettingsSchema.CavalryFlockEnabled, true, SettingSources.Mcm);
                logic.ApplySettingsChange(S);

                // ---- Athletics off: everyone eager (100%) - no tired flock
                S.Set(SettingsSchema.AthleticsEnabled, false, SettingSources.Mcm);
                var fr = FlockRules.From(S);
                Check(FlockMath.IsFree(in fr, FlockMath.Eagerness(in fr, 0.05, false), false), "Athletics off: an empty bar still flocks");
                S.Set(SettingsSchema.AthleticsEnabled, true, SettingSources.Mcm);

                // ---- the summary
                logic.WriteFlockSummary();
                LogHas("[summary] cavalry flocking settings: cavalry flocking: every AI rider");
                LogHas("[summary] cavalry flocking: ");
                LogHas("rider time ON: FREE ");
                LogHas("[summary] cavalry flips (a flock released at once toward its goal - a wave): ");
                LogHas("[summary] cavalry time between passes per rider");
                LogHas("[summary] cavalry lone passes (a pass with fewer than 3 of his kind within 30 m): ON ");
                LogHas("[summary] cavalry spread (mean distance of a kind's riders from its centre");
                LogHas("[summary] cavalry lever: the smoke's stand-in for Agent.SetScriptedPosition - points sent ");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                FlockDefaults();
                AthleticsDefaults();
            }
        }

        /// <summary>Step 55: the master switch off = every flocking rider let go at once; back on = flocking again.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FlockFollowsTheMasterSwitch()
        {
            var keep = _logic;
            try
            {
                FlockDefaults();
                var world = new FakeCourageBody();
                var body = new FakeFlockBody();
                var logic = NewFlockLogic(body, world);
                double t = 31000;
                var st = logic.Track(FakeAgent(7900))!;
                SetBar(st, 0.3);
                body.Add(world.Add(st, 0, 0, 1, 1, LegionaryArmour, LegionaryLevel));
                logic.TickFlock(t += 0.5);
                Check(st.Flock.Flocking && body.Riders[st.AgentIndex].Scripted, "precondition: the tired rider is not flocking");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                logic.TickFlock(t += 0.02);
                Check(!st.Flock.Flocking && logic.FlockingNow == 0 && !body.Riders[st.AgentIndex].Scripted && body.Restored == 1, "mod off: the flocking rider was not let go at once (step 55)");
                LogHas("[cavalry] the whole mod (ModEnabled) switched OFF mid-mission at ");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                logic.TickFlock(t += 0.5);
                Check(st.Flock.Flocking, "mod back on: the tired rider does not flock again (step 55)");
            }
            finally
            {
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FlockCostStaysFlat()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                CourageDefaults();
                FlockDefaults();
                var world = new FakeCourageBody();
                var body = new FakeFlockBody();
                var logic = NewFlockLogic(body, world);
                const int men = 1000;
                double t0 = 32000;
                // 500 men on foot and 500 riders (two sides, three kinds, three formations), half of the riders tired
                for (int i = 0; i < men; i++)
                {
                    var st = logic.Track(FakeAgent(10000 + i))!;
                    st.AthleticsSkill = 100;
                    bool south = (i & 1) == 0;
                    double x = (i % 50) * 3, y = (i / 50) * 3;
                    var m = world.Add(st, x, south ? -1 - y : 1 + y, south ? 1 : 2, i % 3, LegionaryArmour, LegionaryLevel);
                    if (i < men / 2) continue; // the first half on foot
                    body.Add(m, ranged: i % 3 == 1, thrown: i % 3 == 2);
                    SetBar(st, i % 4 == 0 ? 0.3 : i % 4 == 1 ? 0.5 : 0.9); // exhausted, half-tired (they can lead), eager
                }
                body.Goals[1 * 16 + 0] = (0, 500);
                body.Goals[2 * 16 + 1] = (0, -500);
                for (int k = 0; k < 10; k++) logic.TickFlock(t0 += 0.5); // everyone seen, the first lines written
                const int ticks = 300;
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var sw = Stopwatch.StartNew();
                for (int k = 1; k <= ticks; k++) logic.TickFlock(t0 + k / 60.0);
                sw.Stop();
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double msPerTick = sw.Elapsed.TotalMilliseconds / ticks;
                int censuses = logic.FlockStats.Censuses(true);
                Console.WriteLine("        step 55 cost: " + men + " men, " + body.Riders.Count + " riders (" + logic.FlockingNow + " flocking, " + logic.FlockStats.Flips
                                  + " flips so far) - the flocking tick " + msPerTick.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " ms on average ("
                                  + censuses + " censuses; the stand-in's timid scan is a plain loop over all 1000), " + allocated + " bytes over " + ticks + " ticks");
                Check(msPerTick < 2.0, "cost: the flocking tick over " + men + " men took " + msPerTick + " ms (budget 2 ms with the stand-in's O(n) scan)");
                Check(allocated < 256 * 1024, "cost: the flocking tick allocates - " + allocated + " bytes over " + ticks + " ticks");
                Check(logic.FlockingNow > 0 || logic.FlockStats.Flips > 0, "cost: the tired riders neither flock nor flip");
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

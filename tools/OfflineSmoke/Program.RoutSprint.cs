using System;
using System.Runtime.CompilerServices;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline check for step 57 - ROUTING MEN SPRINT AWAY (AI_NOTES "Step 57", DESIGN decision 49): the REAL logic's
    /// <c>MissionBehavior.OnAgentFleeing</c> override (the game calls it once per man whose morale broke) with the stand-in sprint body - a
    /// routing AI man on foot sprints his WHOLE store once (no order reserve, never ended near an enemy); a running order sprint is handed
    /// over; an empty store, you, a rider refused and counted; SprintOnRout / SprintAiEnabled / the master switch off = none; the first-rout
    /// line and the [summary] line.
    /// </summary>
    internal static partial class Program
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RoutSprintThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                SprintDefaults();
                S.Set(SettingsSchema.SprintOnRout, true, SettingSources.File);
                S.Set(SettingsSchema.SprintRefillSeconds, 120, SettingSources.File);
                var body = new FakeSprintBody();
                var logic = NewSprintLogic(body);
                var stats = logic.SprintStats;
                LogHas("a routing AI man sprints his WHOLE store once at the rout's start (SprintOnRout)");
                double t = 21000;
                void Tick(float dt = 0.1f)
                {
                    t += dt;
                    logic.TickSprint(dt, t, Rules);
                }
                TrackedAgent Man(int index, int slot)
                {
                    var st = logic.Track(FakeAgent(index))!;
                    st.AthleticsSkill = 100; // 100 points = a 5 s store
                    body.Slots[index] = slot;
                    body.Speeds[index] = 3.5f;
                    return st;
                }
                var a = Man(1201, 7);
                var b = Man(1202, 7);
                var mover = Man(1203, 8);
                body.Orders[7] = BraceOrderKind.Stop;
                body.Orders[8] = BraceOrderKind.Stop;
                t += 0.3;
                Tick();
                Check(Near(a.Sprint.Stored, 5.0), "a 100-point man does not store 5 s: " + a.Sprint.Stored);

                // 1. a man ROUTS: his whole 5 s store, no reserve (a move would keep 80 %), the first line
                body.EnemyNear.Add(1201); // an enemy at his back: a rout's sprint never ends in melee
                body.EnemyAt[1201] = 1.5f;
                logic.OnAgentFleeing(a.Agent);
                Check(a.Sprint.Active && a.Sprint.Rout && Near(a.Sprint.Length, 5.0) && stats.RoutStarted == 1 && stats.RoutsSeen == 1,
                    "the routing man did not sprint his whole store: active " + a.Sprint.Active + ", T " + a.Sprint.Length);
                LogHas("[sprint] first ROUT sprint this battle: ");
                LogHas("broke and routs at ");
                LogHas("he sprints his WHOLE store 5.00 s of 5.00 s for the get-away, no reserve, no end near an enemy (SprintOnRout)");
                for (int i = 0; i < 20; i++) Tick();
                Check(a.Sprint.Active && stats.Ends(false, SprintEnd.InMelee) == 0, "the rout's sprint ended near an enemy");
                for (int i = 0; i < 40 && a.Sprint.Active; i++) Tick();
                Check(!a.Sprint.Active && NearTo(a.Sprint.Stored, 0, 0.05), "the rout's sprint did not run the store dry: " + a.Sprint.Stored);
                // once: the game never calls it twice for one rout, but a second call finds the store empty
                logic.OnAgentFleeing(a.Agent);
                Check(!a.Sprint.Active && stats.RoutEmpty == 1, "a routed man with an empty store sprinted again (empty " + stats.RoutEmpty + ")");

                // 2. a man in the middle of a MOVE order's sprint routs: handed over - the rest of his store, all of it
                body.Orders[8] = BraceOrderKind.Move;
                t += 0.3;
                Tick();
                Check(mover.Sprint.Active && mover.Sprint.Order == SprintOrder.Move, "the move did not start a sprint");
                logic.OnAgentFleeing(mover.Agent);
                Check(mover.Sprint.Active && mover.Sprint.Rout && NearTo(mover.Sprint.Length, mover.Sprint.StoredAtStart, 1e-9) && mover.Sprint.Length > 4.0
                      && stats.RoutReplaced == 1 && stats.Ends(false, SprintEnd.Routed) == 1, "the running move sprint was not handed over: T " + mover.Sprint.Length);

                // 3. you, a rider: never
                var you = Man(1204, 9);
                logic.SmokePlayer = you.Agent;
                logic.OnAgentFleeing(you.Agent);
                Check(!you.Sprint.Active && stats.RoutSkippedPlayer == 1, "you sprinted on a rout");
                logic.SmokePlayer = null;
                var rider = Man(1205, 9);
                body.Mounted.Add(1205);
                logic.OnAgentFleeing(rider.Agent);
                Check(!rider.Sprint.Active && stats.RoutSkippedMounted == 1, "a rider sprinted on a rout");

                // 4. the switches: SprintOnRout off, SprintAiEnabled off, the master switch off - none (live)
                S.Set(SettingsSchema.SprintOnRout, false, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                LogHas("a routing AI man does not sprint (SprintOnRout off)");
                logic.OnAgentFleeing(b.Agent);
                Check(!b.Sprint.Active && stats.RoutOff == 1, "SprintOnRout off: he sprinted");
                S.Set(SettingsSchema.SprintOnRout, true, SettingSources.Mcm);
                S.Set(SettingsSchema.SprintAiEnabled, false, SettingSources.Mcm);
                logic.OnAgentFleeing(b.Agent);
                Check(!b.Sprint.Active && stats.RoutOff == 2, "SprintAiEnabled off: he sprinted");
                S.Set(SettingsSchema.SprintAiEnabled, true, SettingSources.Mcm);
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                logic.OnAgentFleeing(b.Agent);
                Check(!b.Sprint.Active && stats.RoutOff == 3, "the master switch off: he sprinted");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                logic.OnAgentFleeing(b.Agent);
                Check(b.Sprint.Active && b.Sprint.Rout && stats.RoutStarted == 3, "back on: the rout's sprint did not start");

                // 5. the summary
                CallPrivate(logic, "CloseSprints");
                CallPrivate(logic, "WriteSprintSummary");
                LogHas("[summary] sprint on rout (step 57, SprintOnRout on): routs seen 9, sprints started 3 (whole store avg ");
                LogHas("of them taking over a running order sprint 1; refused: empty store 1, already sprinting 0, switched off 3, on a horse 1, yours 1");
                Console.WriteLine("        step 57: rout sprints " + stats.RoutStarted + " (whole store avg " + (stats.RoutSeconds / Math.Max(1, stats.RoutStarted)).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                                  + " s), handed over " + stats.RoutReplaced + ", refused - empty " + stats.RoutEmpty + ", off " + stats.RoutOff + ", rider " + stats.RoutSkippedMounted + ", you " + stats.RoutSkippedPlayer);
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                AthleticsDefaults();
            }
        }
    }
}

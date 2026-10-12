using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Hud;
using TraxCombat.Missions;
using TraxCombat.Models;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 33 - THE SPRINT (AI_NOTES "Step 33"): the REAL <see cref="AthleticsLogic"/> with a stand-in for the
    /// sprint's engine side (<see cref="ISprintBody"/>: the key, motion, formations' orders, "an enemy near"). What runs for real:
    /// the store (points / 20, clamped, refilled 7 s standing), your key (a press starts, letting go ends, refusals), the speed
    /// profile stepped into the run multiplier, no Athletics refill while sprinting, the AI on a new order (charge all, advance
    /// half, hold none; melee, a new order, mounted, not his own AI), every end path, the master switch, the logs and the summary,
    /// the stat decorator putting MaxSpeedMultiplier ABOVE 1, the sprint bar's prefab and view - and the cost of a thousand men on a
    /// charge. What only the game can tell: the engine's top speed above ~1 and the feet (PLAYTEST D10, the summary's speed check).
    /// </summary>
    internal static partial class Program
    {
        /// <summary>The smoke's stand-in for the sprint's engine side.</summary>
        private sealed class FakeSprintBody : ISprintBody
        {
            public readonly HashSet<int> Gone = new HashSet<int>();
            public readonly HashSet<int> NotAi = new HashSet<int>();
            public readonly HashSet<int> Mounted = new HashSet<int>();
            public readonly HashSet<int> EnemyNear = new HashSet<int>();
            public readonly Dictionary<int, int> Slots = new Dictionary<int, int>();
            public readonly Dictionary<int, BraceOrderKind> Orders = new Dictionary<int, BraceOrderKind>();
            public readonly Dictionary<int, float> Speeds = new Dictionary<int, float>();
            public float Speed = 3f;
            public bool Key;
            public bool CanSprint = true;
            public int LastKeyIndex = -1;
            public int OrderReads;

            public bool IsActive(TrackedAgent st) => !Gone.Contains(st.AgentIndex);

            public bool IsAiControlled(TrackedAgent st) => !NotAi.Contains(st.AgentIndex);

            public bool IsMounted(TrackedAgent st) => Mounted.Contains(st.AgentIndex);

            public int FormationSlot(TrackedAgent st) => Slots.TryGetValue(st.AgentIndex, out int s) ? s : -1;

            public int ReadOrders(SprintFormationRead[] into)
            {
                OrderReads++;
                int n = 0;
                foreach (var kv in Orders)
                {
                    if (n >= into.Length) break;
                    into[n].Slot = kv.Key;
                    into[n].Kind = kv.Value;
                    n++;
                }
                return n;
            }

            /// <summary>The engine following the asked speed: a base top of 4 m/s × his run × his sprint multiplier.</summary>
            public void ReadMotion(TrackedAgent st, out float speed, out float topSpeed)
            {
                speed = Speeds.TryGetValue(st.AgentIndex, out float v) ? v : Speed;
                topSpeed = 4f * st.RunSpeedMultiplier * st.SprintMultiplier;
            }

            public bool EnemyWithin(TrackedAgent st, float metres) => EnemyNear.Contains(st.AgentIndex);

            public bool KeyDown(int keyIndex)
            {
                LastKeyIndex = keyIndex;
                return Key;
            }

            public bool PlayerCanSprint() => CanSprint;
        }

        /// <summary>Step 33: every logic built from here on plays the sprint's engine side through a stand-in; the key names are the game's.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UseFakeSprintBody()
        {
            Check(SprintBodyDefault.Current is GameSprintBody, "the default sprint body is not the game's: " + SprintBodyDefault.Current.GetType().Name);
            for (int i = 0; i < KeyNames.All.Length; i++)
                Check(GameSprintBody.KeyFor(i) != InputKey.Invalid, "the key name " + KeyNames.All[i] + " is not one of the game's InputKey names");
            Check(GameSprintBody.KeyFor(0) == InputKey.LeftControl && (int)InputKey.LeftControl == 29, "LeftControl is not the game's key 29");
            Check(GameSprintBody.SlotsPerTeam == (int)FormationClass.NumberOfAllFormations, "formation slots per team");
            SprintBodyDefault.Current = new FakeSprintBody();
            Check(new AthleticsLogic().SprintBody == SprintBodyDefault.Current, "a new logic does not start with the default sprint body");
        }

        /// <summary>DESIGN's initial sprint numbers (tuning defaults.json never breaks the checks).</summary>
        private static void SprintDefaults(bool on = true)
        {
            S.Set(SettingsSchema.SprintEnabled, on, SettingSources.File);
            S.Set(SettingsSchema.SprintKey, 0, SettingSources.File);
            S.Set(SettingsSchema.SprintPointsPerSecond, 20, SettingSources.File);
            S.Set(SettingsSchema.SprintMinStartPercent, 20, SettingSources.File);
            S.Set(SettingsSchema.SprintMaxMultiplier, 1.6, SettingSources.File);
            S.Set(SettingsSchema.SprintMinMultiplier, 1.1, SettingSources.File);
            S.Set(SettingsSchema.SprintFullSpeedSharePercent, 30, SettingSources.File);
            S.Set(SettingsSchema.SprintRefillSeconds, 7, SettingSources.File);
            S.Set(SettingsSchema.SprintRefillMovingFactor, 0.5, SettingSources.File);
            S.Set(SettingsSchema.SprintRefillExhaustedFactor, 0.5, SettingSources.File);
            S.Set(SettingsSchema.SprintAiEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.SprintAiChargeSharePercent, 100, SettingSources.File);
            S.Set(SettingsSchema.SprintAiMoveSharePercent, 50, SettingSources.File);
            S.Set(SettingsSchema.ShowSprintBar, true, SettingSources.File);
            S.Set(SettingsSchema.SprintBarOffsetAbove, 46, SettingSources.File);
        }

        /// <summary>The older steps run with the sprint OFF (their run-speed checks predate it).</summary>
        private static void SprintOff() => S.Set(SettingsSchema.SprintEnabled, false, SettingSources.File);

        private static AthleticsLogic NewSprintLogic(FakeSprintBody body)
        {
            var logic = new AthleticsLogic();
            _logic = logic;
            SetStatic(typeof(AthleticsLogic), "_current", logic);
            typeof(AthleticsLogic).GetMethod("StartAthletics", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(logic, null);
            logic.SprintBody = body;
            return logic;
        }

        private static bool NearTo(double a, double b, double eps) => Math.Abs(a - b) <= eps;

        private static void CallPrivate(AthleticsLogic logic, string method, params object[] args) =>
            typeof(AthleticsLogic).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(logic, args);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SprintThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                SprintDefaults();
                var body = new FakeSprintBody();
                var logic = NewSprintLogic(body);
                var stats = logic.SprintStats;
                LogHas("[sprint] mission start: ON - hold LeftControl (on foot, in a fight); the store = Athletics points / 20 s (150 points = 7.5 s, empty = 0), a start needs 20% of it; speed x1.60 of the CURRENT run speed for the first 30% of the sprint's length, then a straight line to x1.10 (a full 7.5 s sprint: x1.60 until 2.25 s)");
                LogHas("AI ON - on a NEW order: charge 100% of the store, advance / move 50%");
                double t = 15000;
                void Tick(float dt = 0.1f)
                {
                    t += dt;
                    logic.TickSprint(dt, t, Rules);
                }

                // ---------------- YOU: a 150-point bar holds 7.5 s; the key (index 0) read live
                var me = logic.Track(FakeAgent(700))!;
                me.AthleticsSkill = 150;
                logic.SmokePlayer = me.Agent;
                body.Speeds[700] = 0f;
                Tick();
                Check(Near(me.Sprint.Stored, 7.5), "a full 150 bar does not store 7.5 s: " + me.Sprint.Stored);
                Check(body.LastKeyIndex == 0, "the key asked is not SprintKey's index: " + body.LastKeyIndex);

                // the press: full speed at once, his run property above 1 through the real decorator
                me.SpeedDirty = false;
                body.Key = true;
                body.Speeds[700] = 3f;
                Tick();
                Check(me.Sprint.Active && me.Sprint.ByKey && Near(me.Sprint.Length, 7.5) && me.SprintMultiplier == 1.6f && me.SpeedDirty,
                    "the press did not start a full sprint at x1.6: active " + me.Sprint.Active + ", T " + me.Sprint.Length + ", x" + me.SprintMultiplier);
                Check(stats.YouStarted == 1, "your sprint not counted");
                LogHas("[sprint] YOU: first sprint this battle at ");
                LogHas(" - stored 7.50 s of 7.50 s (150 Athletics points / 20.0), so it lasts T 7.50 s of moving: x1.60 of your current run speed (run x1.00 → x1.60 on the run property) until 2.25 s, then a straight line to x1.10 at 7.50 s; it drains only while you move, your Athletics does not refill meanwhile; let go to stop (the rest stays stored)");
                var statBase = new FreshStatsModel();
                var statTop = new TraxAgentStatModel(new AgentStatCalculateModel[] { statBase });
                statTop.Initialize(statBase);
                var p = me.Agent.AgentDrivenProperties;
                statTop.UpdateAgentStats(me.Agent, p);
                Check(Near(p.MaxSpeedMultiplier, 0.8f * 1.6f) && p.MaxSpeedMultiplier > 1f, "the decorator did not put the run property above 1: " + p.MaxSpeedMultiplier + " (0.80 x 1.6)");
                Check(Near(p.CombatMaxSpeedMultiplier, 0.84f) && Near(p.SwingSpeedMultiplier, 1.05f), "the sprint touched another property");
                // tired (run x0.7 at empty would be x0.7): the sprint multiplies the CURRENT run speed
                me.RunSpeedMultiplier = 0.85f;
                statTop.UpdateAgentStats(me.Agent, p);
                Check(Near(p.MaxSpeedMultiplier, 0.8f * 0.85f * 1.6f), "the sprint is not x the current (tired) run speed: " + p.MaxSpeedMultiplier);
                me.RunSpeedMultiplier = 1f;

                // no Athletics refill while sprinting (the regen pass skips him before any engine read)
                AthleticsMath.Charge(me, Rules, t);
                double before = me.Fraction;
                var regen = typeof(AthleticsLogic).GetMethod("RegenPass", BindingFlags.Instance | BindingFlags.NonPublic)!;
                regen.Invoke(logic, new object[] { 1.0, t + 30, Rules });
                Check(me.Fraction == before, "his Athletics refilled while sprinting: " + before + " → " + me.Fraction);

                // the profile, step by step (the recompute is simulated: the engine follows the asked speed)
                while (me.Sprint.Elapsed < 2.2)
                {
                    me.SpeedDirty = false;
                    Tick();
                }
                Check(me.SprintMultiplier == 1.6f, "the full-speed part did not hold x1.6 to 2.25 s: x" + me.SprintMultiplier + " at " + me.Sprint.Elapsed);
                double storedAt = me.Sprint.Stored, elapsedAt = me.Sprint.Elapsed;
                while (me.Sprint.Elapsed < 4.85)
                {
                    me.SpeedDirty = false;
                    Tick();
                }
                Check(me.SprintMultiplier == 1.35f, "halfway down the line (4.875 s) is not x1.35: x" + me.SprintMultiplier + " at " + me.Sprint.Elapsed);
                Check(NearTo(storedAt - me.Sprint.Stored, me.Sprint.Elapsed - elapsedAt, 1e-6),
                    "the store did not drain 1 s a second while moving: " + storedAt + " → " + me.Sprint.Stored + " over " + (me.Sprint.Elapsed - elapsedAt) + " s");
                // standing still holding the key: nothing drains
                double held = me.Sprint.Stored;
                body.Speeds[700] = 0.2f;
                for (int i = 0; i < 10; i++)
                {
                    me.SpeedDirty = false;
                    Tick();
                }
                Check(me.Sprint.Active && Near(me.Sprint.Stored, held), "standing still drained the store: " + held + " → " + me.Sprint.Stored);
                // letting go: ended, the rest stored, the multiplier back to 1
                body.Key = false;
                Tick();
                Check(!me.Sprint.Active && me.SprintMultiplier == 1f && me.Sprint.Stored >= held && me.Sprint.Stored <= held + 0.11, "letting go did not end it keeping the rest (+ this tick's refill): " + held + " → " + me.Sprint.Stored);
                Check(stats.Ends(true, SprintEnd.Released) == 1, "the release not counted");
                LogHas("[sprint] YOU: sprint ended at ");
                LogHas(" (key released) after ");
                Check(stats.EngineFollows == true && stats.SpeedSamples >= 20, "the speed check did not see the engine follow: " + stats.SpeedSamples + " samples, follows " + stats.EngineFollows);

                // refill: standing at 140 / 150 points (f 1 at the peak line 75%) - cap / 7 a second
                double cap = 140 / 20.0;
                double s0 = me.Sprint.Stored;
                body.Speeds[700] = 0f;
                for (int i = 0; i < 10; i++) Tick();
                Check(NearTo(me.Sprint.Stored - s0, cap / 7.0, 1e-3), "standing refill is not cap / 7 a second: " + (me.Sprint.Stored - s0));
                // moving (effort above a walk): half that
                s0 = me.Sprint.Stored;
                body.Speeds[700] = 3f;
                for (int i = 0; i < 10; i++) Tick();
                Check(NearTo(me.Sprint.Stored - s0, cap / 14.0, 1e-3), "moving refill is not half: " + (me.Sprint.Stored - s0));

                // below 20%: refused, logged once in full
                body.Key = true;
                Tick();
                while (me.Sprint.Stored > 1.0)
                {
                    me.SpeedDirty = false;
                    Tick();
                }
                body.Key = false;
                Tick();
                body.Speeds[700] = 0f;
                body.Key = true;
                Tick();
                Check(!me.Sprint.Active && stats.YouRefusedBelowMinimum == 1, "a press below 20% was not refused: active " + me.Sprint.Active);
                LogHas("[sprint] YOU: first sprint refused this battle at ");
                LogHas(" - below the 20% a start needs (1.40 s); the store refills in 7.0 s from empty standing at full strength (x0.50 moving, down to x0.50 at an empty bar)");
                // holding the key while it refills past 20% never starts one by itself - a new press does
                for (int i = 0; i < 20; i++) Tick();
                Check(!me.Sprint.Active && me.Sprint.Stored > 1.4, "holding the key restarted a sprint by itself, or no refill: " + me.Sprint.Stored);
                body.Key = false;
                Tick();
                body.Key = true;
                Tick();
                Check(me.Sprint.Active, "a new press above 20% did not start");
                // the order menu / a menu takes the keys: ended (control changed)
                body.CanSprint = false;
                Tick();
                Check(!me.Sprint.Active && stats.Ends(true, SprintEnd.ControlChanged) == 1, "a menu did not end your sprint");
                body.CanSprint = true;
                body.Key = false;
                Tick();
                // on a horse: refused
                body.Mounted.Add(700);
                body.Key = true;
                Tick();
                Check(!me.Sprint.Active && stats.YouRefusedMounted == 1, "a sprint started on a horse");
                body.Key = false;
                body.Mounted.Remove(700);
                Tick();
                // the AI drives your hero (RTS Camera): no key sprint
                logic.SmokePlayerAiControlled = true;
                body.Key = true;
                Tick();
                Check(!me.Sprint.Active, "a key sprint while the AI drives your hero");
                logic.SmokePlayerAiControlled = false;
                body.Key = false;
                Tick();

                // ---------------- THE AI: a formation (slot 3) of five 100-point men (5 s stores), one mounted, one yours to drive
                var men = new List<TrackedAgent>();
                for (int i = 0; i < 5; i++)
                {
                    var st = logic.Track(FakeAgent(701 + i))!;
                    st.AthleticsSkill = 100;
                    body.Slots[701 + i] = 3;
                    body.Speeds[701 + i] = 3.5f;
                    men.Add(st);
                }
                var rider = logic.Track(FakeAgent(706))!;
                rider.AthleticsSkill = 100;
                body.Slots[706] = 3;
                body.Mounted.Add(706);
                var taken = logic.Track(FakeAgent(707))!;
                taken.AthleticsSkill = 100;
                body.Slots[707] = 3;
                body.NotAi.Add(707);
                var other = logic.Track(FakeAgent(708))!;
                other.AthleticsSkill = 100;
                body.Slots[708] = 4;
                body.Orders[3] = BraceOrderKind.Stop;
                body.Orders[4] = BraceOrderKind.Stop;
                t += 0.3;
                Tick();
                Check(men.TrueForAll(m => !m.Sprint.Active), "the first look at a formation started a sprint");
                // a CHARGE: every AI man on foot in it sprints his whole store
                body.Orders[3] = BraceOrderKind.Charge;
                t += 0.3;
                Tick();
                Check(men.TrueForAll(m => m.Sprint.Active && !m.Sprint.ByKey && m.Sprint.Order == SprintOrder.Charge && Near(m.Sprint.Length, 5.0) && m.SprintMultiplier == 1.6f),
                    "a new charge did not start every man's full sprint at x1.6");
                Check(!rider.Sprint.Active && !taken.Sprint.Active && !other.Sprint.Active, "a rider, a man you drive or another formation sprinted");
                Check(stats.AiStarted(SprintOrder.Charge) == 5 && stats.AiSkippedMounted == 1 && stats.AiSkippedPlayer == 1 && stats.OrdersSeen(SprintOrder.Charge) == 1,
                    "the AI counts: started " + stats.AiStarted(SprintOrder.Charge) + ", mounted " + stats.AiSkippedMounted + ", yours " + stats.AiSkippedPlayer);
                LogHas("[sprint] first new order seen this battle at ");
                LogHas(" s: formation slot 3 Stop → Charge (charge: its AI men on foot sprint for up to 100% of their store)");
                LogHas("[sprint] first AI order sprint this battle: ");
                LogHas(" on a new charge order - stored 5.00 s of 5.00 s, his share 100% → T 5.00 s: x1.60 (run x1.00 → x1.60) until 1.50 s, then to x1.10; ends early on an enemy within 3.0 m or a new order");
                // an enemy within 3 m: he stops at the next look (every 0.25 s)
                body.EnemyNear.Add(701);
                for (int i = 0; i < 3; i++) Tick();
                Check(!men[0].Sprint.Active && stats.Ends(false, SprintEnd.InMelee) == 1 && men[0].SprintMultiplier == 1f, "an enemy within 3 m did not end his sprint");
                Check(men[1].Sprint.Active, "the others stopped too");
                // the order changes again: the running sprints end, an ADVANCE starts half of what is left
                body.Orders[3] = BraceOrderKind.Advance;
                t += 0.3;
                Tick();
                Check(stats.Ends(false, SprintEnd.OrderChanged) == 4, "the new order did not end the running sprints: " + stats.Ends(false, SprintEnd.OrderChanged));
                var m2 = men[1];
                Check(m2.Sprint.Active && m2.Sprint.Order == SprintOrder.AdvanceOrMove && Near(m2.Sprint.Length, 0.5 * m2.Sprint.StoredAtStart), "an advance is not half the store");
                double left = m2.Sprint.StoredAtStart;
                for (int i = 0; i < 40 && m2.Sprint.Active; i++) Tick();
                Check(!m2.Sprint.Active && stats.Ends(false, SprintEnd.LengthDone) >= 1 && NearTo(m2.Sprint.Stored, left / 2, 0.11),
                    "the advance sprint did not end at its share keeping the rest: " + m2.Sprint.Stored + " of " + left);
                // a HOLD order: no sprint
                body.Orders[3] = BraceOrderKind.Stop;
                t += 0.3;
                Tick();
                Check(men.TrueForAll(m => !m.Sprint.Active) && stats.OrdersSeen(SprintOrder.None) == 1, "a hold order started a sprint");
                // SprintAiEnabled off: an AI sprint ends, yours runs on; back on, the first look only records
                for (int i = 0; i < 80; i++) Tick(); // stores refill
                body.Orders[3] = BraceOrderKind.Charge;
                t += 0.3;
                Tick();
                Check(men[2].Sprint.Active, "a second charge did not start");
                body.Key = true;
                body.Speeds[700] = 3f;
                Tick();
                Check(me.Sprint.Active, "your sprint did not start");
                S.Set(SettingsSchema.SprintAiEnabled, false, SettingSources.Mcm);
                logic.ApplySettingsChange(S); // what the tick does on a settings version change: the [sprint] settings line
                Tick();
                Check(!men[2].Sprint.Active && me.Sprint.Active && stats.Ends(false, SprintEnd.SwitchedOff) >= 1, "SprintAiEnabled off did not end only the AI's sprints");
                LogHas("[sprint] SprintAiEnabled switched off at ");
                body.Orders[3] = BraceOrderKind.Advance;
                S.Set(SettingsSchema.SprintAiEnabled, true, SettingSources.Mcm);
                Tick();
                Check(men.TrueForAll(m => !m.Sprint.Active), "turning the AI sprint back on fired an order given while it was off");
                LogHas("[sprint] settings changed mid-mission at ");

                // ---------------- the master switch: every sprint ends at once, the decorator vanilla; on again: nothing restarts
                body.Orders[3] = BraceOrderKind.Charge;
                t += 0.3;
                Tick();
                Check(men[3].Sprint.Active && me.Sprint.Active, "no sprints before the master switch");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                Tick();
                Check(logic.SprintingNow == 0 && !me.Sprint.Active && !men[3].Sprint.Active && me.SprintMultiplier == 1f && men[3].SprintMultiplier == 1f,
                    "ModEnabled off did not end every sprint at once");
                Check(!AthleticsLogic.SpeedFactorsFor(men[3].Agent, out _, out float run, out _, out _) && run == 1f, "the decorator still applies with the mod off");
                LogHas("[sprint] ModEnabled switched off at ");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                Tick();
                Tick();
                Check(logic.SprintingNow == 0, "a sprint restarted by itself after the master switch");
                body.Key = false;
                Tick();

                // ---------------- left the field, the hideout's fresh start, a points drop clamps
                body.Orders[3] = BraceOrderKind.Advance;
                t += 0.3;
                Tick();
                body.Orders[3] = BraceOrderKind.Charge;
                t += 0.3;
                Tick();
                var leaver = men[4];
                Check(leaver.Sprint.Active, "no sprint to leave the field with");
                CallPrivate(logic, "Untrack", leaver.Agent);
                Check(!leaver.Sprint.Active && stats.Ends(false, SprintEnd.LeftField) == 1, "leaving the field did not end his sprint");
                var fresh = men[2];
                CallPrivate(logic, "SprintFreshStart", fresh, t);
                Tick();
                Check(!fresh.Sprint.Active && Near(fresh.Sprint.Stored, 5.0) && stats.FreshStarts == 1, "the fresh start did not fill his store: " + fresh.Sprint.Stored);
                int clampsBefore = stats.Clamps;
                AthleticsMath.Charge(fresh, Rules, t);
                AthleticsMath.Charge(fresh, Rules, t);
                Tick();
                Check(Near(fresh.Sprint.Stored, 4.0) && stats.Clamps == clampsBefore + 1, "a points drop did not clamp the store: " + fresh.Sprint.Stored);
                // an empty bar: no store, a press refused
                for (int i = 0; i < 20; i++) AthleticsMath.Charge(me, Rules, t);
                Tick();
                body.Key = true;
                Tick();
                Check(!me.Sprint.Active && Near(me.Sprint.Stored, 0) && stats.YouRefusedEmpty == 1, "an empty bar sprinted");
                body.Key = false;
                Tick();

                // ---------------- the summary
                CallPrivate(logic, "CloseSprints");
                CallPrivate(logic, "WriteSprintSummary");
                LogHas("[summary] sprint (step 33): ON - hold LeftControl");
                LogHas("[summary] sprint - you: ");
                LogHas("; refused: below 20% stored 1, an empty bar 1, on a horse 1; ends: key released ");
                LogHas("[summary] sprint - AI: new orders seen - charge ");
                LogHas("; mounted ");
                LogHas("[summary] sprint speed check (÷ his top speed at the sprint's start, while it ran): ");
                LogHas(" - the engine's top speed follows the sprint - no cap above the run speed");
                logic.SmokePlayer = null;
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SprintCostStaysFlat()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                SprintDefaults();
                var body = new FakeSprintBody();
                var logic = NewSprintLogic(body);
                const int men = 1000;
                double t = 16000;
                var all = new List<TrackedAgent>(men);
                for (int i = 0; i < men; i++)
                {
                    var st = logic.Track(FakeAgent(4000 + i))!;
                    st.AthleticsSkill = 50 + i % 150;              // stores 2.5 - 9.95 s
                    body.Slots[4000 + i] = i % 10;
                    body.Speeds[4000 + i] = 2.5f + (i % 5) * 0.5f;
                    all.Add(st);
                }
                for (int k = 0; k < 10; k++) body.Orders[k] = BraceOrderKind.Stop;
                logic.TickSprint(0.1f, t, Rules);                    // first look: stores clamped, orders recorded
                for (int k = 0; k < 10; k++) body.Orders[k] = BraceOrderKind.Charge;
                t += 0.3;
                logic.TickSprint(0.1f, t, Rules);                    // the charge: everyone on foot starts
                int sprinting = logic.SprintingNow;
                int marked = 0;
                for (int i = 0; i < men; i++)
                    if (all[i].SpeedDirty) marked++;
                Check(sprinting == men, "cost: not every man sprinted on the charge: " + sprinting);
                const int ticks = 800;
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var sw = Stopwatch.StartNew();
                for (int k = 1; k <= ticks; k++) logic.TickSprint(1f / 60f, t + k / 60.0, Rules); // 13 s at 60 fps: the charge runs out (the longest store is 9.95 s), the stores refill
                sw.Stop();
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double msPerTick = sw.Elapsed.TotalMilliseconds / ticks;
                var stats = logic.SprintStats;
                Console.WriteLine("        step 33 cost: " + men + " men on a charge - " + marked + " marked for a recompute at the start (" + Math.Ceiling(marked / (double)AthleticsLogic.MaxRecomputesPerTick)
                                  + " ticks of the " + AthleticsLogic.MaxRecomputesPerTick + "-a-tick budget), " + stats.Recomputes + " sprint recomputes asked in all (" + (stats.Recomputes / (double)men).ToString("0.0", CultureInfo.InvariantCulture)
                                  + " a man); the sprint's tick " + msPerTick.ToString("0.000", CultureInfo.InvariantCulture) + " ms, " + allocated + " bytes allocated over " + ticks + " ticks");
                Check(marked == men, "cost: the charge did not mark every sprinter for a recompute: " + marked);
                Check(logic.SprintingNow == 0, "cost: sprints still running after 13 s: " + logic.SprintingNow);
                Check(stats.Recomputes <= men * 14, "cost: too many sprint recomputes - the 0.05 steps should give ~12 a man: " + stats.Recomputes);
                Check(msPerTick < 0.5, "cost: the sprint's tick over " + men + " men took " + msPerTick + " ms (budget 0.5 ms)");
                Check(allocated < 64 * 1024, "cost: the sprint's tick allocates - " + allocated + " bytes over " + ticks + " ticks");
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        // ------------------------------------------------------------------ the sprint bar

        private static class SprintColors
        {
            public static readonly Color Ready = Color.ConvertStringToColor(SprintMath.ReadyHex);
            public static readonly Color Below = Color.ConvertStringToColor(SprintMath.BelowStartHex);
            public static readonly Color Sprinting = Color.ConvertStringToColor(SprintMath.SprintingHex);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SprintPrefabIsValid() => PrefabIsValid(SprintBarView.Movie, typeof(SprintBarVM), 8, 40);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SprintBarThroughTheView()
        {
            var keep = _logic;
            try
            {
                var logic = HudLogic();
                SprintDefaults();
                S.Set(SettingsSchema.RecoveryBarWidth, 205, SettingSources.File);
                S.Set(SettingsSchema.RecoveryBarHeight, 14, SettingSources.File);
                var body = new FakeSprintBody();
                logic.SprintBody = body;
                var me = logic.Track(FakeAgent(800))!;
                me.AthleticsSkill = 150;
                logic.SmokePlayer = me.Agent;
                var view = new SprintBarView();
                var layer = new FakeHudLayer();
                view.UseLayer(layer);
                double t = 17000;
                logic.TickSprint(0.1f, t, Rules);

                // outside a battle: never (no sprint there); in battle: built, "Sprint 7.5 s", full, blue, the mark at 20%
                view.Tick(Frame(t, me.Agent, MissionMode.StartUp, weapon: true));
                Check(!layer.Up, "the sprint bar showed outside a battle");
                t += 0.05;
                view.Tick(Frame(t, me.Agent));
                var vm = view.CurrentViewModel;
                Check(layer.Up && vm != null && vm.IsShown && vm.LabelText == "Sprint 7.5 s" && vm.Fill == 1f && vm.FillColor == SprintColors.Ready && vm.MarkShown,
                    "in battle the sprint bar is not up, full, blue: " + (vm == null ? "no VM" : "\"" + vm.LabelText + "\" " + vm.Fill));
                Check(vm!.BarWidth == 205f && vm.BarHeight == 14f && vm.OffsetRight == 62f && vm.OffsetBottom == 100f && Near(vm.MarkOffset, 203f * 0.2f),
                    "layout: " + vm.BarWidth + " x " + vm.BarHeight + ", right " + vm.OffsetRight + ", bottom " + vm.OffsetBottom + " (54 + 46), mark " + vm.MarkOffset);
                LogHas("[hud] sprint bar: layer created at ");
                LogHas("[hud] sprint bar: first values pushed at ");
                LogHas(" - \"Sprint 7.5 s\" of 7.50 s (ready), the mark at 20%; bar 205 x 14 px, 62 px from the right edge and 100 px from the bottom (your Athletics bar's row 54 + 46; UI pixels); key LeftControl");

                // the press: yellow, draining
                body.Key = true;
                body.Speed = 3f;
                for (int i = 0; i < 6; i++)
                {
                    t += 0.1;
                    logic.TickSprint(0.1f, t, Rules);
                }
                view.Tick(Frame(t, me.Agent));
                Check(vm.FillColor == SprintColors.Sprinting && vm.Fill < 1f && vm.LabelText == "Sprint " + SprintMath.SecondsText(me.Sprint.Stored) && view.SprintsShown == 1,
                    "sprinting: \"" + vm.LabelText + "\" " + vm.Fill);
                LogHas("[hud] sprint bar: first sprint shown at ");
                // drained below the mark and let go: grey
                while (me.Sprint.Stored > 1.0)
                {
                    t += 0.1;
                    logic.TickSprint(0.1f, t, Rules);
                }
                body.Key = false;
                body.Speed = 0f;
                t += 0.1;
                logic.TickSprint(0.1f, t, Rules);
                view.Tick(Frame(t, me.Agent));
                Check(vm.FillColor == SprintColors.Below && vm.Fill < 0.2f, "below the start mark the bar is not grey: " + vm.Fill);
                // the mark follows SprintMinStartPercent (the refresh), 0 hides it
                S.Set(SettingsSchema.SprintMinStartPercent, 0, SettingSources.Mcm);
                t += 0.2;
                view.Tick(Frame(t, me.Agent));
                Check(!vm.MarkShown && vm.FillColor == SprintColors.Ready, "SprintMinStartPercent 0: the mark shows or the bar is not ready");
                S.Set(SettingsSchema.SprintMinStartPercent, 20, SettingSources.Mcm);

                // its switches: its own, the Athletics bar's, the sprint's, the master switch
                void Removed(ParamDef p, string why)
                {
                    int built = layer.Created;
                    S.Set(p, false, SettingSources.Mcm);
                    t += 0.05;
                    view.Tick(Frame(t, me.Agent));
                    Check(!layer.Up, why + " did not remove the sprint bar");
                    S.Set(p, true, SettingSources.Mcm);
                    t += 0.05;
                    view.Tick(Frame(t, me.Agent));
                    Check(layer.Up && layer.Created == built + 1, why + " back on did not rebuild it");
                    vm = view.CurrentViewModel!;
                }
                Removed(SettingsSchema.ShowSprintBar, "ShowSprintBar off");
                Removed(SettingsSchema.ShowPlayerBar, "ShowPlayerBar off (it belongs to the Athletics bar)");
                Removed(SettingsSchema.SprintEnabled, "SprintEnabled off");
                Removed(SettingsSchema.ModEnabled, "ModEnabled off");
                LogHas(" - your Athletics bar (ShowPlayerBar) or the sprint (SprintEnabled) is off");

                view.Finish(t);
                var lines = view.Stats.SummaryLines();
                view.AddSummaryLines(lines);
                Check(lines.Exists(l => l.StartsWith("hud: sprint bar - sprints shown 1, ", StringComparison.Ordinal) && l.EndsWith(" - your sprints themselves are in the sprint lines", StringComparison.Ordinal)),
                    "the summary line: " + string.Join(" // ", lines));
                logic.SmokePlayer = null;
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
                HudDefaults();
                ConfigStore.Reload("after the sprint bar smoke"); // the file's values back (the MCM steps build on them)
            }
        }
    }
}

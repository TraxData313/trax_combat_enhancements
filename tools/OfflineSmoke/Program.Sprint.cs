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

            /// <summary>The engine following the asked speed: a fresh top of 4 m/s × his run factor (step 35: the sprint's run factor
            /// in place of his tiredness multiplier while he sprints).</summary>
            public void ReadMotion(TrackedAgent st, out float speed, out float topSpeed)
            {
                speed = Speeds.TryGetValue(st.AgentIndex, out float v) ? v : Speed;
                topSpeed = 4f * (st.SprintRun > 0f ? st.SprintRun : st.RunSpeedMultiplier);
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

        /// <summary>DESIGN's initial sprint numbers (tuning defaults.json never breaks the checks). Step 35's floor and acceleration are
        /// pinned OFF here (SprintMinSeconds 0, SprintTopSpeedReachSeconds 0): the step-33 checks test step 33's store (an empty bar
        /// holds nothing) - <see cref="SprintTuningThroughTheLogic"/> turns them on. The peak-on-the-fresh-run rule has no switch:
        /// for a fresh man it IS step 33's profile, and the one tired check below asks the new rule.</summary>
        private static void SprintDefaults(bool on = true)
        {
            S.Set(SettingsSchema.SprintMinSeconds, 0, SettingSources.File);
            S.Set(SettingsSchema.SprintTopSpeedReachSeconds, 0, SettingSources.File);
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
            S.Set(SettingsSchema.SprintAiChargeKeepPercent, 0, SettingSources.File);
            S.Set(SettingsSchema.SprintAiAdvanceKeepPercent, 50, SettingSources.File);
            S.Set(SettingsSchema.SprintAiMoveKeepPercent, 80, SettingSources.File);
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
                LogHas("[sprint] mission start: ON - hold LeftControl (on foot, in a fight); the store = Athletics points / 20 s (150 points = 7.5 s, empty = 0), a start needs 20% of it; speed x1.60 of his FRESH run speed (whatever his Athletics) for the first 30% of the sprint's length, then a straight line to x1.10 of his CURRENT run speed (a full 7.5 s sprint: x1.60 until 2.25 s); the game's own acceleration (SprintTopSpeedReachSeconds 0)");
                LogHas("AI ON - on a NEW order he sprints what he holds above the order's reserve (stored - keep x his full store), nothing below 0.2 s (no 20% start gate for the AI): charge keeps 0%, advance keeps 50%, move keeps 80%; hold, retreat, fall back, follow none");
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
                Check(me.Sprint.Active && me.Sprint.ByKey && Near(me.Sprint.Length, 7.5) && me.SprintRun == 1.6f && me.SpeedDirty,
                    "the press did not start a full sprint at x1.6: active " + me.Sprint.Active + ", T " + me.Sprint.Length + ", x" + me.SprintRun);
                Check(stats.YouStarted == 1, "your sprint not counted");
                LogHas("[sprint] YOU: first sprint this battle at ");
                LogHas(" - stored 7.50 s of 7.50 s (150 Athletics points / 20.0), so it lasts T 7.50 s of moving: x1.60 of your FRESH run speed (the run property x1.00 → x1.60) until 2.25 s, then a straight line to x1.10 of your current run (x1.10) at 7.50 s; the game's own acceleration (SprintTopSpeedReachSeconds 0); it drains only while you move, your Athletics does not refill meanwhile; let go to stop (the rest stays stored)");
                var statBase = new FreshStatsModel();
                var statTop = new TraxAgentStatModel(new AgentStatCalculateModel[] { statBase });
                statTop.Initialize(statBase);
                var p = me.Agent.AgentDrivenProperties;
                statTop.UpdateAgentStats(me.Agent, p);
                Check(Near(p.MaxSpeedMultiplier, 0.8f * 1.6f) && p.MaxSpeedMultiplier > 1f, "the decorator did not put the run property above 1: " + p.MaxSpeedMultiplier + " (0.80 x 1.6)");
                Check(Near(p.CombatMaxSpeedMultiplier, 0.84f) && Near(p.SwingSpeedMultiplier, 1.05f), "the sprint touched another property");
                // tired (run x0.85): step 35 - the peak is x the FRESH run, whatever his tiredness (it replaces the run multiplier)
                me.RunSpeedMultiplier = 0.85f;
                statTop.UpdateAgentStats(me.Agent, p);
                Check(Near(p.MaxSpeedMultiplier, 0.8f * 1.6f), "the sprint's peak is not x the FRESH run speed (step 35): " + p.MaxSpeedMultiplier + " (0.80 x 1.6)");
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
                Check(me.SprintRun == 1.6f, "the full-speed part did not hold x1.6 to 2.25 s: x" + me.SprintRun + " at " + me.Sprint.Elapsed);
                double storedAt = me.Sprint.Stored, elapsedAt = me.Sprint.Elapsed;
                while (me.Sprint.Elapsed < 4.85)
                {
                    me.SpeedDirty = false;
                    Tick();
                }
                Check(me.SprintRun == 1.35f, "halfway down the line (4.875 s) is not x1.35: x" + me.SprintRun + " at " + me.Sprint.Elapsed);
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
                Check(!me.Sprint.Active && me.SprintRun == 0f && me.Sprint.Stored >= held && me.Sprint.Stored <= held + 0.11, "letting go did not end it keeping the rest (+ this tick's refill): " + held + " → " + me.Sprint.Stored);
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
                Check(men.TrueForAll(m => m.Sprint.Active && !m.Sprint.ByKey && m.Sprint.Order == SprintOrder.Charge && Near(m.Sprint.Length, 5.0) && m.SprintRun == 1.6f),
                    "a new charge did not start every man's full sprint at x1.6");
                Check(!rider.Sprint.Active && !taken.Sprint.Active && !other.Sprint.Active, "a rider, a man you drive or another formation sprinted");
                Check(stats.AiStarted(SprintOrder.Charge) == 5 && stats.AiSkippedMounted == 1 && stats.AiSkippedPlayer == 1 && stats.OrdersSeen(SprintOrder.Charge) == 1,
                    "the AI counts: started " + stats.AiStarted(SprintOrder.Charge) + ", mounted " + stats.AiSkippedMounted + ", yours " + stats.AiSkippedPlayer);
                LogHas("[sprint] first new order seen this battle at ");
                LogHas(" s: formation slot 3 Stop → Charge (charge: its AI men on foot sprint what they hold above a 0% reserve)");
                LogHas("[sprint] first AI order sprint this battle: ");
                LogHas(" on a new charge order - stored 5.00 s of 5.00 s (100%), charge keeps 0% → asked T 5.00 s (stored - keep x 5.00; left after it 0%): x1.60 of the fresh run (run x1.00 → x1.60) until 1.50 s, then to x1.10 of his current run (x1.10); the game's own acceleration (SprintTopSpeedReachSeconds 0); ends early on an enemy within 3.0 m or a new order");
                // an enemy within 3 m: he stops at the next look (every 0.25 s)
                body.EnemyNear.Add(701);
                for (int i = 0; i < 3; i++) Tick();
                Check(!men[0].Sprint.Active && stats.Ends(false, SprintEnd.InMelee) == 1 && men[0].SprintRun == 0f, "an enemy within 3 m did not end his sprint");
                Check(men[1].Sprint.Active, "the others stopped too");
                // the order changes again: the running sprints end, an ADVANCE starts (step 37: what he holds above half his FULL 5 s store)
                body.Orders[3] = BraceOrderKind.Advance;
                t += 0.3;
                Tick();
                Check(stats.Ends(false, SprintEnd.OrderChanged) == 4, "the new order did not end the running sprints: " + stats.Ends(false, SprintEnd.OrderChanged));
                var m2 = men[1];
                Check(m2.Sprint.Active && m2.Sprint.Order == SprintOrder.Advance && Near(m2.Sprint.Length, m2.Sprint.StoredAtStart - 2.5), "an advance did not sprint down to the 50% reserve: T " + m2.Sprint.Length + " from " + m2.Sprint.StoredAtStart);
                double left = m2.Sprint.StoredAtStart;
                double asked = m2.Sprint.Length;
                for (int i = 0; i < 40 && m2.Sprint.Active; i++) Tick();
                Check(!m2.Sprint.Active && stats.Ends(false, SprintEnd.LengthDone) >= 1 && NearTo(m2.Sprint.Stored, left - asked, 0.11),
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
                Check(logic.SprintingNow == 0 && !me.Sprint.Active && !men[3].Sprint.Active && me.SprintRun == 0f && men[3].SprintRun == 0f,
                    "ModEnabled off did not end every sprint at once");
                Check(!AthleticsLogic.SpeedFactorsFor(men[3].Agent, 2.6f, out _, out float run, out _, out _, out float reachOff) && run == 1f && reachOff == 0f, "the decorator still applies with the mod off");
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
                LogHas("[summary] sprint speed check (x his FRESH run speed = his top at the sprint's start ÷ his run multiplier then, while it ran): ");
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

        /// <summary>
        /// Step 35 (Anton, 2026-10-12, after two tournaments): the peak on the FRESH run (an exhausted man's run property 0.8 x 0.3 →
        /// 0.8 x 2.0 at the start, fading to 1.1 x his current run), the store's 2 s floor (an empty bar sprints, refills slowly),
        /// TopSpeedReachDuration capped at 1 s while sprinting and the game's back at the end - through the REAL logic and the REAL
        /// stat decorator; live settings, the master switch, the lines.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SprintTuningThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                SprintDefaults();
                S.Set(SettingsSchema.SprintMaxMultiplier, 2.0, SettingSources.File);
                S.Set(SettingsSchema.SprintMinSeconds, 2.0, SettingSources.File);
                S.Set(SettingsSchema.SprintTopSpeedReachSeconds, 1.0, SettingSources.File);
                var body = new FakeSprintBody();
                var logic = NewSprintLogic(body);
                var stats = logic.SprintStats;
                LogHas("(150 points = 7.5 s, never below 2.0 s (an empty bar still holds 2.0 s)), a start needs 20% of it; speed x2.00 of his FRESH run speed (whatever his Athletics) for the first 30% of the sprint's length, then a straight line to x1.10 of his CURRENT run speed (a full 7.5 s sprint: x2.00 until 2.25 s); top speed reached in at most 1.0 s while sprinting (the game's own acceleration: ~2.3-2.8 s on foot)");
                double t = 18000;
                void Tick(float dt = 0.1f)
                {
                    t += dt;
                    logic.TickSprint(dt, t, Rules);
                }

                // an EXHAUSTED you: points 0, run x0.3 - the spawn recompute shows the game's acceleration 2.6 s
                var me = logic.Track(FakeAgent(900))!;
                me.AthleticsSkill = 150;
                for (int i = 0; i < 60 && me.Fraction > 0; i++) AthleticsMath.Charge(me, Rules, t);
                Check(me.Fraction <= 0, "could not empty your bar: " + me.Fraction);
                me.RunSpeedMultiplier = 0.3f;
                logic.SmokePlayer = me.Agent;
                var statBase = new FreshStatsModel();
                var statTop = new TraxAgentStatModel(new AgentStatCalculateModel[] { statBase });
                statTop.Initialize(statBase);
                var p = me.Agent.AgentDrivenProperties;
                statTop.UpdateAgentStats(me.Agent, p);
                Check(Near(p.MaxSpeedMultiplier, 0.8f * 0.3f) && Near(p.TopSpeedReachDuration, 2.6f) && Near(me.GameReach, 2.6f),
                    "not sprinting: run " + p.MaxSpeedMultiplier + " (0.8 x 0.3), reach " + p.TopSpeedReachDuration + " (the game's 2.6), remembered " + me.GameReach);
                body.Speeds[900] = 0f;
                Tick();
                Check(Near(me.Sprint.Stored, 2.0), "an empty bar does not hold the 2 s floor: " + me.Sprint.Stored);

                // the press: x2.0 of the FRESH run at once (the run property 0.8 x 2.0 = 1.6, not 0.8 x 0.3 x 2.0), reach 1 s
                me.SpeedDirty = false;
                body.Key = true;
                body.Speeds[900] = 3f;
                Tick();
                Check(me.Sprint.Active && Near(me.Sprint.Length, 2.0) && me.SprintRun == 2.0f && me.SpeedDirty && stats.YouOffFloor == 1,
                    "an exhausted press did not start a 2 s sprint at x2.0 of the fresh run off the floor: active " + me.Sprint.Active + ", T " + me.Sprint.Length + ", x" + me.SprintRun + ", off the floor " + stats.YouOffFloor);
                statTop.UpdateAgentStats(me.Agent, p);
                Check(Near(p.MaxSpeedMultiplier, 0.8f * 2.0f) && Near(p.TopSpeedReachDuration, 1.0f),
                    "the decorator at the sprint's start: run " + p.MaxSpeedMultiplier + " (0.8 x 2.0), reach " + p.TopSpeedReachDuration + " (1.0)");
                Check(Near(p.CombatMaxSpeedMultiplier, 0.84f) && Near(p.SwingSpeedMultiplier, 1.05f), "the sprint touched another property");
                LogHas("[sprint] YOU: first sprint this battle at ");
                LogHas(" - stored 2.00 s of 2.00 s (0 Athletics points / 20.0 = 0.00 s - the store's floor SprintMinSeconds 2.0 s holds it up), so it lasts T 2.00 s of moving: x2.00 of your FRESH run speed (the run property x0.30 → x2.00) until 0.60 s, then a straight line to x1.10 of your current run (x0.33) at 2.00 s; top speed reached in 1.00 s instead of the game's 2.60 s; it drains only while you move");
                // the fade: 2.0 held to 0.6 s, then down towards 1.1 x 0.3 = 0.33
                float lowest = 9f;
                bool heldPeak = true;
                while (me.Sprint.Active)
                {
                    me.SpeedDirty = false;
                    Tick();
                    if (!me.Sprint.Active) break;
                    if (me.Sprint.Elapsed <= 0.6 - 1e-6 && me.SprintRun != 2.0f) heldPeak = false;
                    if (me.SprintRun < lowest) lowest = me.SprintRun;
                }
                Check(heldPeak, "the full-speed part did not hold x2.0");
                Check(lowest <= 0.5f && lowest >= 0.35f, "the fade did not run down towards x0.33 of the fresh run: lowest x" + lowest);
                Check(stats.Ends(true, SprintEnd.StoreEmpty) == 1 && me.SprintRun == 0f && me.SpeedDirty, "the 2 s sprint did not end on an empty store, marked for its recompute");
                statTop.UpdateAgentStats(me.Agent, p);
                Check(Near(p.MaxSpeedMultiplier, 0.8f * 0.3f) && Near(p.TopSpeedReachDuration, 2.6f),
                    "after the sprint the game's values are not back: run " + p.MaxSpeedMultiplier + ", reach " + p.TopSpeedReachDuration);

                // the floor refills slowly: standing exhausted 2 / 7 x 0.5 a second; a press at once is below the 20% mark
                body.Key = false;
                body.Speeds[900] = 0f;
                Tick();
                double s0 = me.Sprint.Stored;
                for (int i = 0; i < 10; i++) Tick();
                Check(NearTo(me.Sprint.Stored - s0, 2.0 / 7.0 * 0.5, 1e-3), "the floor's refill standing exhausted is not 2 / 7 x 0.5 a second: " + (me.Sprint.Stored - s0));
                for (int i = 0; i < 30; i++) Tick();   // 0.57 s of 2 stored: above 20%

                // SprintTopSpeedReachSeconds 0 = the game's own; back to 1 mid-sprint = every sprinter recomputed (live)
                S.Set(SettingsSchema.SprintTopSpeedReachSeconds, 0, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                body.Key = true;
                body.Speeds[900] = 3f;
                Tick();
                Check(me.Sprint.Active, "a press above 20% of the floor did not start");
                statTop.UpdateAgentStats(me.Agent, p);
                Check(Near(p.TopSpeedReachDuration, 2.6f) && Near(p.MaxSpeedMultiplier, 0.8f * 2.0f), "reach 0 did not leave the game's acceleration: " + p.TopSpeedReachDuration);
                me.SpeedDirty = false;
                S.Set(SettingsSchema.SprintTopSpeedReachSeconds, 1.0, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                Check(me.SpeedDirty, "a sprint setting change did not mark the sprinter for a recompute");
                statTop.UpdateAgentStats(me.Agent, p);
                Check(Near(p.TopSpeedReachDuration, 1.0f), "reach back to 1 s mid-sprint did not apply: " + p.TopSpeedReachDuration);
                // the master switch: ended at once, the decorator vanilla (the base model's run and acceleration)
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                Tick();
                statTop.UpdateAgentStats(me.Agent, p);
                Check(!me.Sprint.Active && Near(p.MaxSpeedMultiplier, 0.8f) && Near(p.TopSpeedReachDuration, 2.6f),
                    "ModEnabled off: sprint " + me.Sprint.Active + ", run " + p.MaxSpeedMultiplier + ", reach " + p.TopSpeedReachDuration);
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                body.Key = false;
                Tick();

                // THE AI: an exhausted man (a 100 pool, empty) sprints a charge off the 2 s floor
                var man = logic.Track(FakeAgent(901))!;
                man.AthleticsSkill = 100;
                for (int i = 0; i < 60 && man.Fraction > 0; i++) AthleticsMath.Charge(man, Rules, t);
                man.RunSpeedMultiplier = 0.5f;
                body.Slots[901] = 2;
                body.Speeds[901] = 3.5f;
                body.Orders[2] = BraceOrderKind.Stop;
                t += 0.3;
                Tick();
                body.Orders[2] = BraceOrderKind.Charge;
                t += 0.3;
                Tick();
                Check(man.Sprint.Active && Near(man.Sprint.Length, 2.0) && man.SprintRun == 2.0f && stats.AiOffFloor == 1,
                    "a tired AI man did not sprint his 2 s floor at x2.0: T " + man.Sprint.Length + ", x" + man.SprintRun + ", off the floor " + stats.AiOffFloor);
                LogHas(" on a new charge order - stored 2.00 s of 2.00 s (100%) (the 2.0 s floor), charge keeps 0% → asked T 2.00 s (stored - keep x 2.00; left after it 0%): x2.00 of the fresh run (run x0.50 → x2.00) until 0.60 s, then to x1.10 of his current run (x0.55); top speed reached in at most 1.00 s (the game's value not seen yet)");

                // SprintMinSeconds 0 (live): step 33's empty bar - no store, a press refused
                S.Set(SettingsSchema.SprintMinSeconds, 0, SettingSources.Mcm);
                Tick();
                body.Key = true;
                Tick();
                Check(!me.Sprint.Active && Near(me.Sprint.Stored, 0) && stats.YouRefusedEmpty == 1, "SprintMinSeconds 0: an empty bar sprinted: " + me.Sprint.Stored);
                body.Key = false;
                S.Set(SettingsSchema.SprintMinSeconds, 2.0, SettingSources.Mcm);
                Tick();

                // the summary
                CallPrivate(logic, "CloseSprints");
                CallPrivate(logic, "WriteSprintSummary");
                LogHas("; 2 started off the 2.0 s floor (his points alone stored less)");
                LogHas("; off the 2.0 s floor 1");
                LogHas("acceleration while sprinting: the game's top-speed reach avg 2.60 s → ours 1.00 s (sprint starts: 1)");
                logic.SmokePlayer = null;
            }
            finally
            {
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                AthleticsDefaults();
            }
        }

        /// <summary>
        /// Steps 36 / 37 (Anton 2026-10-12, "shock troops behind the enemy keep their sprint for the charge"; step 37: one number per
        /// order, the reserve): through the real logic with the stand-in orders - a MOVE from full sprints 20% of the 5 s store (1 s,
        /// down to the 80% reserve), a second move nothing ("reserve kept"); the real CHARGE then sprints the 80% left; an ADVANCE
        /// half of a full store; the reserve read live; the master switch; the lines and the summary. The refill is slowed
        /// (SprintRefillSeconds 120) so the stores between orders stay near Anton's numbers.
        /// </summary>
        private static void SprintByOrderThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                SprintDefaults();
                S.Set(SettingsSchema.SprintRefillSeconds, 120, SettingSources.File);
                var body = new FakeSprintBody();
                var logic = NewSprintLogic(body);
                var stats = logic.SprintStats;
                LogHas("charge keeps 0%, advance keeps 50%, move keeps 80%");
                double t = 19000;
                void Tick(float dt = 0.1f)
                {
                    t += dt;
                    logic.TickSprint(dt, t, Rules);
                }
                void Order(int slot, BraceOrderKind kind)
                {
                    body.Orders[slot] = kind;
                    t += 0.3;
                    Tick();
                }
                void RunOut(TrackedAgent m)
                {
                    for (int i = 0; i < 60 && m.Sprint.Active; i++) Tick();
                }

                // shock troops (slot 5): three 100-point men = 5 s stores; an advance man (slot 6)
                var men = new List<TrackedAgent>();
                for (int i = 0; i < 3; i++)
                {
                    var st = logic.Track(FakeAgent(1101 + i))!;
                    st.AthleticsSkill = 100;
                    body.Slots[1101 + i] = 5;
                    body.Speeds[1101 + i] = 3.5f;
                    men.Add(st);
                }
                var adv = logic.Track(FakeAgent(1110))!;
                adv.AthleticsSkill = 100;
                body.Slots[1110] = 6;
                body.Speeds[1110] = 3.5f;
                body.Orders[5] = BraceOrderKind.Stop;
                body.Orders[6] = BraceOrderKind.Stop;
                t += 0.3;
                Tick();
                var a = men[0];
                Check(Near(a.Sprint.Stored, 5.0), "a 100-point man does not store 5 s: " + a.Sprint.Stored);

                // a MOVE from full: everything above the 80% reserve = 1 s
                Order(5, BraceOrderKind.Move);
                Check(men.TrueForAll(m => m.Sprint.Active && m.Sprint.Order == SprintOrder.Move && Near(m.Sprint.Length, 1.0)), "a move from full is not 1 s: " + a.Sprint.Length);
                LogHas(" s: formation slot 5 Stop → Move (move: its AI men on foot sprint what they hold above a 80% reserve)");
                RunOut(a);
                Check(!a.Sprint.Active && NearTo(a.Sprint.Stored, 4.0, 0.05) && stats.AiStarted(SprintOrder.Move) == 3, "the move did not stop at 80%: " + a.Sprint.Stored);

                // a second move right after (hold, then move again): nothing above the reserve - no sprint, counted
                Order(5, BraceOrderKind.Stop);
                Order(5, BraceOrderKind.Move);
                Check(men.TrueForAll(m => !m.Sprint.Active) && stats.AiReserveKept(SprintOrder.Move) == 3, "a second move sprinted into the reserve: kept " + stats.AiReserveKept(SprintOrder.Move));
                LogHas("[sprint] first AI sprint held back by a reserve this battle: ");
                LogHas(" on a new move order - stored 4.0");
                LogHas("move keeps 80% - nothing above the reserve, no sprint (it stays for the next order)");

                // the REAL charge: everything that is left - the 80% the moves kept
                Order(5, BraceOrderKind.Charge);
                Check(men.TrueForAll(m => m.Sprint.Active && m.Sprint.Order == SprintOrder.Charge && NearTo(m.Sprint.Length, m.Sprint.StoredAtStart, 1e-6) && NearTo(m.Sprint.Length, 4.0, 0.05)),
                    "the charge did not sprint the reserve: T " + a.Sprint.Length);
                RunOut(a);

                // an ADVANCE from full: down to the 50% reserve - 2.5 s
                Order(6, BraceOrderKind.Advance);
                Check(adv.Sprint.Active && adv.Sprint.Order == SprintOrder.Advance && Near(adv.Sprint.Length, 2.5), "an advance from full is not 2.5 s: " + adv.Sprint.Length);
                RunOut(adv);
                Check(NearTo(adv.Sprint.Stored, 2.5, 0.05), "the advance did not keep half: " + adv.Sprint.Stored);

                // live: the move's reserve to 40 in MCM - from ~50% the next move sprints ~10%
                S.Set(SettingsSchema.SprintAiMoveKeepPercent, 40, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                LogHas("move keeps 40%");
                Order(6, BraceOrderKind.Stop);
                Order(6, BraceOrderKind.Move);
                Check(adv.Sprint.Active && NearTo(adv.Sprint.Length, adv.Sprint.StoredAtStart - 2.0, 1e-6) && NearTo(adv.Sprint.Length, 0.5, 0.05),
                    "the live reserve 40 did not let a move sprint down to 40%: T " + adv.Sprint.Length + " from " + adv.Sprint.StoredAtStart);
                RunOut(adv);
                S.Set(SettingsSchema.SprintAiMoveKeepPercent, 80, SettingSources.Mcm);
                logic.ApplySettingsChange(S);

                // the master switch: an order given while off starts nothing
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                Tick();
                Order(6, BraceOrderKind.Charge);
                Check(!adv.Sprint.Active, "a charge sprinted with the mod off");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                Tick();
                Tick();
                Check(logic.SprintingNow == 0, "a sprint started after the master switch came back");

                // the summary
                CallPrivate(logic, "CloseSprints");
                CallPrivate(logic, "WriteSprintSummary");
                LogHas("[summary] sprint - AI: new orders seen - charge 1, advance 1, move 3, other ");
                LogHas("; sprints - charge (keeps 0%) 3 men (");
                LogHas("advance (keeps 50%) 1 men (2.5 s in all, avg 2.50 s), move (keeps 80%) 4 men (");
                LogHas("; not sprinted: reserve kept 3 (charge 0, advance 0, move 3), less than 0.2 s or empty: charge 0, advance 0, move 0");
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

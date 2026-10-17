using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;
using TraxCombat.Models;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline checks for step 54 - IMPACT I7, THE AI PUNISHES A WINDOW (AI_NOTES "Step 54"): the REAL logic (the fever's look, the impact
    /// decision through OnMeleeHit's part) and the REAL stat decorator with the stand-in bodies - an AI man whose target's block is broken
    /// (an exhausted blocker staggered) gets the raised share (the game's own 0.144 written) for the window + the grace and his tired share
    /// back after; no target read while no window is open anywhere; the punish switch live; the player never raised; the PLAYER staggered is
    /// pressed (the YOU line); a timid man within 3 m steps in like a surge (one at 4 m keeps away); a rider never; the master switch; the
    /// summary - and the cost.
    /// </summary>
    internal static partial class Program
    {
        /// <summary>The smoke's stand-in for the punish's one read: per man his target and the distance (none = no target).</summary>
        private sealed class FakePunishBody : IPunishBody
        {
            public readonly Dictionary<int, (TrackedAgent Target, float Distance)> Targets = new Dictionary<int, (TrackedAgent, float)>();
            public int Reads;

            public Agent? Target(TrackedAgent st, out float distance)
            {
                Reads++;
                if (Targets.TryGetValue(st.AgentIndex, out var t))
                {
                    distance = t.Distance;
                    return t.Target.Agent;
                }
                distance = float.NaN;
                return null;
            }
        }

        /// <summary>Step 54: every logic built from here on reads the punish's target through a stand-in (the game's reads native agents).</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UseFakePunishBody()
        {
            Check(PunishBodyDefault.Current is GamePunishBody, "the default punish body is not the game's: " + PunishBodyDefault.Current.GetType().Name);
            PunishBodyDefault.Current = new FakePunishBody();
            Check(new AthleticsLogic().PunishBody == PunishBodyDefault.Current, "a new logic does not start with the default punish body");
        }

        private static void PunishDefaults()
        {
            S.Set(SettingsSchema.ImpactPunishEnabled, true, SettingSources.File);
            S.Set(SettingsSchema.ImpactPunishAggressionPercent, 100, SettingSources.File);
            S.Set(SettingsSchema.ImpactPunishGraceSeconds, 0.5, SettingSources.File);
            S.Set(SettingsSchema.ImpactPunishRangeMetres, 3.0, SettingSources.File);
        }

        private static void PunishCleanUp(AthleticsLogic? keep)
        {
            _logic = keep;
            SetStatic(typeof(AthleticsLogic), "_current", keep);
            ImpactHooks.RestoreStunCap();
            ImpactHooks.KnockDown = null;
            ImpactHooks.BlockStun = null;
            ImpactHooks.PickupLock = null;
            ImpactHooks.ShrugOff = null;
            ImpactHooks.Dismount = null;
            ImpactModelDefaults();
            PunishDefaults();
            S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
            AthleticsDefaults();
        }

        /// <summary>A logic with the fever's look (the sprint body), the impact stand-ins, the punish stand-in, scripted dice.</summary>
        private static AthleticsLogic NewPunishLogic(FakeSprintBody body, FakePunishBody punish, FakeImpactInputs inputs, ScriptDice dice)
        {
            var logic = NewSprintLogic(body);
            logic.WeaponFacts = new FakeWeaponFacts();
            logic.FeverDice = new FixedDice(0.99);
            logic.ImpactBody = new FakeImpactBody();
            logic.ImpactInputs = inputs;
            logic.ImpactDice = dice;
            logic.PunishBody = punish;
            return logic;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ImpactPunishThroughTheLogic()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                SprintDefaults();
                FeverDefaults();
                ImpactModelDefaults();
                CourageDefaults();
                PunishDefaults();
                S.Set(SettingsSchema.SprintAiEnabled, false, SettingSources.File);
                var body = new FakeSprintBody();
                var punish = new FakePunishBody();
                var inputs = new FakeImpactInputs();
                var dice = new ScriptDice { Rest = 0.0 }; // every outcome of a block on an exhausted blocker happens
                var logic = NewPunishLogic(body, punish, inputs, dice);
                var ps = logic.PunishStats;
                LogHas("[impact] mission start, the AI punishing a window (step 54): ON - an AI man on foot whose enemy reels or lies down");
                var baseModel = new FreshStatsModel();
                var top = new TraxAgentStatModel(new AgentStatCalculateModel[] { baseModel });
                top.Initialize(baseModel);
                double t = 61000;

                TrackedAgent Man(int index, double bar, int slot = 0, float enemyAt = 2f)
                {
                    var m = logic.Track(FakeAgent(index))!;
                    m.AthleticsSkill = 100;
                    SetBar(m, bar);
                    body.Slots[index] = slot;
                    body.EnemyAt[index] = enemyAt;
                    inputs.Weapons[index] = (1.2, 92, ImpactDamageType.Cut, 100);
                    inputs.Guards[index] = (4.0, 100);
                    inputs.Shields.Add(index);
                    return m;
                }
                void Block(TrackedAgent attacker, TrackedAgent blocker)
                {
                    logic.TickImpact(t); // the decision's clock (the mission's time - the impact tick's)
                    logic.ImpactModelHitTaken(attacker.Agent, blocker.Agent, RightBlock());
                }

                // ---- the men: an AI man A fighting V; the player P; B (AI) fighting P; T timid near V; R a rider fighting V
                var a = Man(9100, 1.0);
                var v = Man(9101, 0.0, slot: 1);     // an EXHAUSTED enemy: his block breaks
                var p = Man(9102, 0.0, slot: 2);     // you, exhausted
                var b = Man(9103, 1.0, slot: 1);
                var tm = Man(9104, 1.0);
                var r = Man(9105, 1.0);
                body.Mounted.Add(9105);
                logic.SmokePlayer = p.Agent;
                punish.Targets[9100] = (v, 1.5f);
                punish.Targets[9102] = (v, 1.2f);    // YOU fight V too
                punish.Targets[9103] = (p, 1.5f);
                punish.Targets[9104] = (v, 4.0f);
                punish.Targets[9105] = (v, 2.0f);
                body.Orders[0] = BraceOrderKind.Stop;
                body.Orders[1] = BraceOrderKind.Stop;
                body.Orders[2] = BraceOrderKind.Stop;
                t = Looks(logic, t, t + 0.5);
                Check(Near(a.DecisionFactor, 0.6f) && punish.Reads == 0 && ps.TargetReads == 0, "calm: A not at his 0.6, or a target read while no window is open anywhere: "
                    + a.DecisionFactor + " / " + punish.Reads);
                var pa = a.Agent.AgentDrivenProperties;
                top.UpdateAgentStats(a.Agent, pa);
                Check(Near(pa.AIAttackOnDecideChance, 0.0864f), "calm: the decorator did not write 0.144 x 0.6: " + pa.AIAttackOnDecideChance);

                // ---- 1. A's blow on V's block: V (exhausted) is staggered / knocked down - a WINDOW opened by the real decision
                Block(a, v);
                Check(PunishMath.InWindow(in v.Impact, t, 0.5) && ps.WindowsOnAi == 1 && ps.WindowsOnPlayer == 0, "the broken block opened no window on V: opened "
                    + ps.WindowsOnAi + ", reeling until " + v.Impact.ReelingUntil + ", down until " + v.Impact.DownUntil);
                double end = PunishMath.WindowEnd(in v.Impact); double openedAt = t;
                LogHas("[impact] first window this battle: (agent 9101) ");
                // the next look: A presses - his share raised to 1.0 (the game's own value written)
                t = Looks(logic, t, t + 0.25);
                Check(a.Punishing && Near(a.DecisionFactor, 1.0f) && a.SpeedDirty && ps.Raised >= 1, "A did not press V's window: " + a.Punishing + " x" + a.DecisionFactor);
                top.UpdateAgentStats(a.Agent, pa);
                Check(Near(pa.AIAttackOnDecideChance, 0.144f) && Near(pa.AIAttackOnParryChance, 0.08f), "in the window the decorator did not write the game's own 0.144 / 0.080: "
                    + pa.AIAttackOnDecideChance + " / " + pa.AIAttackOnParryChance);
                LogHas("[impact] first AI man pressing a window this battle: (agent 9100) at ");
                Check(!p.Punishing && p.DecisionFactor == 1f, "YOU were raised (the player punishes with his own hands)");
                Check(!r.Punishing, "a RIDER pressed a window (the decorator keeps riders' values)");
                Check(ps.SeenOnAi == 1, "V's window not seen by an AI enemy in contact: " + ps.SeenOnAi);
                // his attack inside the window: punished; a second attack is not a second punished window
                logic.NotePunishAttack(a, false, t);
                logic.NotePunishAttack(a, false, t + 0.1);
                Check(ps.PunishedOnAi == 1 && ps.Attacks(true, true) == 2, "the attacks inside the window: punished " + ps.PunishedOnAi + ", counted " + ps.Attacks(true, true));
                LogHas("[impact] first window punished this battle: (agent 9100) started an attack at ");
                logic.NotePunishAttack(r, true, t);
                Check(ps.Attacks(true, true) == 2, "a rider's attack was counted");

                // ---- 2. the TIMID man: 4 m off - raised, but keeps his distance (timid); at 2.5 m he steps in like a surge
                var cr = CourageRules.From(S);
                tm.Courage.Look(in cr, t, 0.2, 1.0, 1.0, 6, false);
                Check(tm.Courage.Timid, "precondition: the timid man is not timid");
                t = Looks(logic, t, t + 0.25);
                Check(tm.Punishing && tm.Courage.Timid && ps.TimidPresses == 0, "the timid man 4 m off: not raised, or stepped in beyond 3 m");
                punish.Targets[9104] = (v, 2.5f);
                t = Looks(logic, t, t + 0.25);
                Check(!tm.Courage.Timid && ps.TimidPresses == 1 && logic.CourageStats.Ended(CourageEnd.PunishWindow) == 1 && Math.Abs(tm.Courage.SurgeUntil - (end + 0.5)) < 1e-6,
                    "the timid man 2.5 m off did not step in until the window's end + the grace: timid " + tm.Courage.Timid + ", surge until " + tm.Courage.SurgeUntil + " vs " + (end + 0.5));
                LogHas("[impact] first timid man stepping into a window this battle: (agent 9104) at ");

                // ---- 3. the punish switch live: off = his own share at once (the settings change re-targets everyone); on = raised again
                S.Set(SettingsSchema.ImpactPunishEnabled, false, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                Check(Near(a.DecisionFactor, 0.6f), "ImpactPunishEnabled off: A not back to 0.6 at once: " + a.DecisionFactor);
                LogHas("[impact] punish settings changed mid-mission at ");
                t = Looks(logic, t, t + 0.25);
                Check(!a.Punishing && Near(a.DecisionFactor, 0.6f), "ImpactPunishEnabled off: A pressed at the next look");
                logic.NotePunishAttack(a, false, t);
                Check(ps.Attacks(false, true) == 1, "an attack with the punish OFF on a target in a window not counted in the OFF row");
                S.Set(SettingsSchema.ImpactPunishEnabled, true, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                t = Looks(logic, t, t + 0.25);
                Check(a.Punishing && Near(a.DecisionFactor, 1.0f), "ImpactPunishEnabled on again: A not pressing");

                // ---- 4. the window closes (+ the grace): back to his tired share; the decorator writes 0.086 again
                int guard = 0;
                while (a.Punishing && guard++ < 100) t = Looks(logic, t, t + 0.25);
                Check(!a.Punishing && t >= end + 0.5 - 1e-9 && t < end + 0.5 + 0.26 && Near(a.DecisionFactor, 0.6f), "the window's end: A still pressing, or let go before the grace: t "
                    + t + " vs the end " + end + " + 0.5, x" + a.DecisionFactor);
                top.UpdateAgentStats(a.Agent, pa);
                Check(Near(pa.AIAttackOnDecideChance, 0.0864f), "after the window: not his 0.086 again: " + pa.AIAttackOnDecideChance);
                logic.NotePunishAttack(a, false, t);
                Check(ps.Attacks(true, false) == 1, "an attack after the window not counted as 'target not'");

                // ---- 5. YOU staggered: B (AI) breaks your exhausted block; he presses YOUR window; his attack = your window punished
                Block(b, p);
                Check(ps.WindowsOnPlayer == 1 && PunishMath.InWindow(in p.Impact, t, 0.5), "your broken block opened no window on you");
                t = Looks(logic, t, t + 0.25);
                Check(b.Punishing && Near(b.DecisionFactor, 1.0f) && ps.SeenOnPlayer == 1, "B did not press YOUR window: x" + b.DecisionFactor);
                logic.NotePunishAttack(b, false, t);
                Check(ps.PunishedOnPlayer == 1 && ps.PlayerAttacksInWindow == 1, "B's attack in your window not counted as your window punished");
                LogHas("[impact] YOU: first window of yours punished this battle - (agent 9103) attacked you at ");

                // ---- 6. shadow: decided and kept as "would", never a window, nobody presses
                S.Set(SettingsSchema.ImpactShadowMode, true, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                var v2 = Man(9106, 0.0, slot: 1);
                Block(a, v2);
                Check(ps.WindowsOnAi == 1 && Near(b.DecisionFactor, 0.6f), "shadow: a window opened, or B still raised: opened " + ps.WindowsOnAi + ", B x" + b.DecisionFactor);
                S.Set(SettingsSchema.ImpactShadowMode, false, SettingSources.Mcm);
                logic.ApplySettingsChange(S);

                // ---- 7. the summary
                logic.SmokePlayer = null;
                logic.WritePunishSummary();
                LogHas("[summary] impact punish settings (step 54): ON - an AI man on foot whose enemy reels or lies down");
                LogHas("[summary] impact punish windows: opened 2 (on AI men 1, on you 1) - seen by an AI enemy in contact 2 (on you 1), PUNISHED (an AI melee attack started inside) 2 (on you 1 of 1), wasted 0 (100.0% punished)");
                LogHas("[summary] impact punish proof: AI melee attacks per man-minute in contact (on foot) - punish ON: target reeling / down ");
                LogHas("[summary] impact punish men: raised to the window's share ");
                Console.WriteLine("        step 54: windows " + (ps.WindowsOnAi + ps.WindowsOnPlayer) + ", punished " + (ps.PunishedOnAi + ps.PunishedOnPlayer) + ", raised " + ps.Raised
                    + ", timid presses " + ps.TimidPresses + ", target reads " + ps.TargetReads + " in " + ps.Looks + " looks; the window on V " + (end - openedAt).ToString("0.00", CultureInfo.InvariantCulture) + " s");
            }
            finally
            {
                PunishCleanUp(keep);
            }
        }

        /// <summary>The master-switch step's punish part: off = the game's values at once (no look runs, the factor 1); back on = pressed again.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ImpactPunishFollowsTheMasterSwitch()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                SprintDefaults();
                FeverDefaults();
                ImpactModelDefaults();
                PunishDefaults();
                var body = new FakeSprintBody();
                var punish = new FakePunishBody();
                var inputs = new FakeImpactInputs();
                var logic = NewPunishLogic(body, punish, inputs, new ScriptDice { Rest = 0.0 });
                double t = 62000;
                var a = logic.Track(FakeAgent(9200))!;
                var v = logic.Track(FakeAgent(9201))!;
                a.AthleticsSkill = v.AthleticsSkill = 100;
                SetBar(v, 0.0);
                body.Slots[9200] = 0;
                body.Slots[9201] = 1;
                body.EnemyAt[9200] = 2f;
                inputs.Weapons[9200] = (1.2, 92, ImpactDamageType.Cut, 100);
                inputs.Guards[9201] = (4.0, 100);
                inputs.Shields.Add(9201);
                punish.Targets[9200] = (v, 1.5f);
                logic.TickImpact(t);
                logic.ImpactModelHitTaken(a.Agent, v.Agent, RightBlock());
                t = Looks(logic, t, t + 0.25);
                Check(a.Punishing && Near(a.DecisionFactor, 1.0f), "precondition: A not pressing V's window (step 54)");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                t = Looks(logic, t, t + 0.25);
                Check(a.DecisionFactor == 1f, "mod off: A's factor not the game's (step 54)");
                logic.TickImpact(t);
                logic.ImpactModelHitTaken(a.Agent, v.Agent, RightBlock());
                Check(logic.PunishStats.WindowsOnAi == 1, "mod off: a window opened (step 54)");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
                logic.ApplySettingsChange(S);
                var v2 = logic.Track(FakeAgent(9202))!;
                v2.AthleticsSkill = 100;
                SetBar(v2, 0.0);
                body.Slots[9202] = 1;
                inputs.Guards[9202] = (4.0, 100);
                inputs.Shields.Add(9202);
                punish.Targets[9200] = (v2, 1.5f);
                logic.TickImpact(t);
                logic.ImpactModelHitTaken(a.Agent, v2.Agent, RightBlock());
                t = Looks(logic, t, t + 0.25);
                Check(a.Punishing && Near(a.DecisionFactor, 1.0f) && logic.PunishStats.WindowsOnAi == 2, "mod back on: A not pressing the new window (step 54)");
            }
            finally
            {
                PunishCleanUp(keep);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ImpactPunishCostStaysFlat()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                SprintDefaults();
                FeverDefaults();
                ImpactModelDefaults();
                PunishDefaults();
                var body = new FakeSprintBody();
                var punish = new FakePunishBody();
                var inputs = new FakeImpactInputs();
                var logic = NewPunishLogic(body, punish, inputs, new ScriptDice { Rest = 0.0 });
                const int men = 1000;
                double t0 = 63000;
                var list = new List<TrackedAgent>(men);
                for (int i = 0; i < men; i++)
                {
                    var st = logic.Track(FakeAgent(10000 + i))!;
                    st.AthleticsSkill = 100;
                    body.Slots[10000 + i] = i % 8;
                    body.EnemyAt[10000 + i] = 2f;                       // everyone in contact
                    list.Add(st);
                }
                for (int i = 0; i < men; i++)
                {
                    var target = list[(i + 1) % men];
                    punish.Targets[10000 + i] = (target, 1.5f);
                    if (i % 2 == 1) target.Impact.Reel(t0 + 100, 2);    // half the targets reeling the whole time
                }
                typeof(AthleticsLogic).GetField("_punishAnyUntil", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(logic, t0 + 100);
                logic.TickFever(t0);
                const int ticks = 300;
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var sw = Stopwatch.StartNew();
                for (int k = 1; k <= ticks; k++) logic.TickFever(t0 + k / 60.0);  // five seconds at 60 fps
                sw.Stop();
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double msPerTick = sw.Elapsed.TotalMilliseconds / ticks;
                int pressing = 0;
                foreach (var st in list) if (st.Punishing) pressing++;
                Console.WriteLine("        step 54 cost: " + men + " men in contact, half facing a reeling target - the fever's tick with the punish's look "
                                  + msPerTick.ToString("0.000", CultureInfo.InvariantCulture) + " ms a tick, " + allocated + " bytes allocated over " + ticks + " ticks; "
                                  + logic.PunishStats.TargetReads + " target reads, " + pressing + " men pressing");
                Check(pressing == men / 2, "cost: not every man facing a reeling target presses: " + pressing);
                Check(msPerTick < 0.5, "cost: the fever's tick with the punish over " + men + " men took " + msPerTick + " ms (budget 0.5 ms)");
                Check(allocated < 256 * 1024, "cost: the punish's look allocates - " + allocated + " bytes over " + ticks + " ticks");
            }
            finally
            {
                PunishCleanUp(keep);
            }
        }
    }
}

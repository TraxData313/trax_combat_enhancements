using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;
using TraxCombat.Models;
using TaleWorlds.Core;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline check for step 102 - THE AI TIMES ITS BLOCKS (AI_NOTES "Step 102", DESIGN decision 91 (n)): through the REAL logic and the REAL input
    /// component (<see cref="AiInputComponent.OnAIInputSet"/>) with the guard stand-in - an AI man with an enemy near is watched (his callback
    /// asked on), his input frames feed his clock and come out UNCHANGED, a guard he raised 0.05 s before a blow scores 0.5 (the AI's 50 %), a held
    /// one 0, a flicker is one guard, an unwatched man falls back to the channel-1 read; the watch let go when the enemy leaves, on removal, at the
    /// switch; the [summary] line; and the cost for a thousand men.
    /// </summary>
    internal static partial class Program
    {
        private const double InputFrameSeconds = 1.0 / 120;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AiGuardClockThroughTheLogic()
        {
            var keep = _logic;
            float capKeep = StunCap;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                BlocksStunDefaults();
                Step56BlockModel();
                foreach (var p in new[] { SettingsSchema.TimedBlocksEnabled, SettingsSchema.TimedBlockPerfectSeconds, SettingsSchema.TimedBlockWindowSeconds,
                             SettingsSchema.TimedBlockRiposteMaxPercent, SettingsSchema.TimedBlockStaggerMaxSeconds, SettingsSchema.TimedBlockAgainstYouMaxSeconds,
                             SettingsSchema.TimedBlockParryPercent, SettingsSchema.TimedBlockAiPercent })
                    S.Set(p, p.Default, SettingSources.File);
                S.Set(SettingsSchema.RiposteStunMaxSeconds, 1.0, SettingSources.File);
                Check((uint)Agent.MovementControlFlag.DefendMask == AiGuardClock.DefendMask, "Core's AI clock defend mask is not the game's DefendMask");
                var inputs = new FakeImpactInputs();
                var body = new FakeImpactBody();
                var guard = new FakeGuardBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = body;
                logic.ImpactInputs = inputs;
                logic.GuardBody = guard;
                logic.ImpactDice = new ScriptDice { Rest = 0.5 };
                double t = 90000;
                int next = 7200;

                TrackedAgent Man(bool shield)
                {
                    int i = next++;
                    var m = logic.Track(FakeAgent(i))!;
                    m.AthleticsSkill = 125;
                    SetBar(m, 1.0);
                    inputs.Weapons[i] = (1.2, 92, ImpactDamageType.Cut, 100);
                    inputs.Guards[i] = (shield ? 4.0 : 1.2, 100);
                    if (shield) inputs.Shields.Add(i);
                    return m;
                }
                ImpactDecision Hit(TrackedAgent a, TrackedAgent v)
                {
                    AiGuardClockHook.SmokeNow = t;
                    logic.ImpactModelHitTaken(a.Agent, v.Agent, RightBlock());
                    return logic.LastImpact;
                }

                var you = Man(true);
                logic.SmokePlayer = you.Agent;
                logic.SmokePlayerAiControlled = false;
                var ai = Man(true);
                guard.Contacts[ai.Agent.Index] = (GuardRead.Passive, double.NaN); // the engine's read at contact: a held defend (Toni's 23 of 23)
                var rnd = new Random(102);
                int frames = 0, changed = 0;
                AiInputComponent? comp = null;

                // the engine's input frames for him (every 1/120 s, the logic's tick every 1/60 s): random events / vector / other bits, his defend bits as given
                void Play(double seconds, uint defend)
                {
                    double end = t + seconds;
                    int k = 0;
                    while (t < end - 1e-9)
                    {
                        AiGuardClockHook.SmokeNow = t;
                        if (comp != null)
                        {
                            uint other = (uint)rnd.Next(0, 0x400) & ~AiInputMath.AttackMask; // move / turn bits, never an attack (his own wish is not ours to judge here)
                            var ef = (Agent.EventControlFlag)(uint)rnd.Next(0, 1 << 20);
                            var mf = (Agent.MovementControlFlag)(other | defend);
                            var v = new Vec2((float)rnd.NextDouble() * 2 - 1, (float)rnd.NextDouble() * 2 - 1);
                            var ef0 = ef;
                            var mf0 = mf;
                            var v0 = v;
                            comp.OnAIInputSet(ref ef, ref mf, ref v);
                            frames++;
                            if (ef != ef0 || mf != mf0 || v.x != v0.x || v.y != v0.y) changed++;
                        }
                        if ((k++ & 1) == 0) logic.TickImpact(t);
                        t += InputFrameSeconds;
                    }
                }

                // ---- 1. no enemy near: not watched; an enemy comes within 5 m: watched (his callback asked on, his input state made once)
                Play(0.5, 0);
                Check(ai.Input == null || !ai.Input.GuardWatch, "an AI man with no enemy near is watched");
                guard.Near.Add(ai.Agent.Index);
                Play(0.5, 0);
                Check(ai.Input != null && ai.Input.GuardWatch && guard.Hooks == 1 && logic.AiClockStats.Watched == 1 && logic.AiClockStats.CallbacksTurnedOn == 1,
                    "the near AI man not watched: watch " + (ai.Input?.GuardWatch) + ", hooks " + guard.Hooks + ", watched " + logic.AiClockStats.Watched);
                Check(ai.Input!.Active == false, "the watch made him ACTIVE (a wish) - it must not");
                Check(!guard.Near.Contains(you.Agent.Index) && (you.Input == null || !you.Input.GuardWatch), "the player is watched");
                comp = new AiInputComponent(ai.Agent, ai); // the engine calls it now (the stand-in's Hook added none)

                // ---- 2. his guard up 0.05 s before the blow: the AI clock, score 0.5 (the AI's 50 % of a perfect 1)
                Play(0.5, 0);
                Play(0.05, AiInputMath.DefendDown);
                var d = Hit(Man(false), ai);
                Check(d.TimedSource == TimingSource.AiClock && Close(d.TimedScore, 0.5, 1e-6) && Close(d.TimedAge, 0.05, 0.02),
                    "the AI's guard raised 0.05 s before the blow: source " + d.TimedSource + ", age " + d.TimedAge + ", score " + d.TimedScore);
                Check(d.Riposte.Chance > d.RiposteChance56 + 0.3, "the AI's timed block did not lift the riposte: " + d.Riposte.Chance + " (untimed " + d.RiposteChance56 + ")");

                // ---- 3. the same guard held 1 s (turned to another side on the way - not a new guard): score 0
                Play(0.5, AiInputMath.DefendDown);
                Play(0.5, 0x800);
                d = Hit(Man(false), ai);
                Check(d.TimedSource == TimingSource.AiClock && d.TimedScore == 0 && d.Riposte.Chance == d.RiposteChance56,
                    "the AI's held guard: source " + d.TimedSource + ", score " + d.TimedScore);

                // ---- 4. a flicker (down 0.03 s, our hold's frames or his own) is one guard: still held, score 0
                Play(0.03, 0);
                Play(0.05, AiInputMath.DefendDown);
                d = Hit(Man(false), ai);
                Check(d.TimedSource == TimingSource.AiClock && d.TimedScore == 0, "a flicker read as a fresh guard: score " + d.TimedScore + ", age " + d.TimedAge);

                // ---- 5. a real drop and a raise 0.3 s before the blow: in the window, below perfect - a part score
                Play(0.4, 0);
                Play(0.3, AiInputMath.DefendDown);
                d = Hit(Man(false), ai);
                Check(d.TimedSource == TimingSource.AiClock && d.TimedScore > 0 && d.TimedScore < 0.5, "a guard 0.3 s old: score " + d.TimedScore);

                // ---- 6. THE INPUT UNCHANGED: every frame through the real component came out as it went in
                Check(frames > 200 && changed == 0, "the component changed " + changed + " of " + frames + " input frames of a watched man");

                // ---- 7. the enemy leaves: the watch let go (the callback released), no frames - his clock goes stale: the old channel-1 read (held) answers
                guard.Near.Remove(ai.Agent.Index);
                Play(0.5, AiInputMath.DefendDown);
                Check(!ai.Input!.GuardWatch && guard.Releases == 1 && logic.AiClockStats.Watched == 0, "the watch not let go: releases " + guard.Releases);
                var keepComp = comp;
                comp = null; // the engine no longer calls him
                Play(0.5, 0);
                d = Hit(Man(false), ai);
                Check(d.TimedSource == TimingSource.AiHeld && d.TimedScore == 0, "an unwatched man: source " + d.TimedSource + " (the channel-1 fallback)");
                Check(logic.AiClockStats.ByRead[(int)AiClockRead.NotWatched] == 1, "the unwatched contact not counted");

                // ---- 8. back in contact, then removed: the watch dropped with no engine call
                guard.Near.Add(ai.Agent.Index);
                comp = keepComp;
                Play(0.5, AiInputMath.DefendDown);
                Check(ai.Input.GuardWatch && guard.Hooks == 2, "not watched again");
                int releases = guard.Releases;
                CallPrivate(logic, "Untrack", ai.Agent);
                Check(!ai.Input.GuardWatch && guard.Releases == releases && logic.AiClockStats.Watched == 0 && !ai.AiGuard.Seen,
                    "removal: watch " + ai.Input.GuardWatch + ", releases " + (guard.Releases - releases) + ", watched " + logic.AiClockStats.Watched);
                comp = null;

                // ---- 9. the switch off: every watch let go, the hook's notes stop; the master switch the same
                var ai2 = Man(true);
                guard.Near.Add(ai2.Agent.Index);
                Play(0.5, 0);
                Check(ai2.Input != null && ai2.Input.GuardWatch && AiGuardClockHook.On, "the second man not watched");
                S.Set(SettingsSchema.TimedBlocksEnabled, false, SettingSources.File);
                Play(0.1, 0);
                Check(!ai2.Input!.GuardWatch && !AiGuardClockHook.On && logic.AiClockStats.Watched == 0, "timed blocks OFF: the watch kept / the notes on");
                var comp2 = new AiInputComponent(ai2.Agent, ai2);
                long before = logic.AiClockStats.Frames;
                AiGuardClockHook.SmokeNow = t;
                var e2 = Agent.EventControlFlag.Kick;
                var m2 = Agent.MovementControlFlag.DefendDown;
                var v2 = new Vec2(0.1f, 0.9f);
                comp2.OnAIInputSet(ref e2, ref m2, ref v2);
                Check(logic.AiClockStats.Frames == before && m2 == Agent.MovementControlFlag.DefendDown && e2 == Agent.EventControlFlag.Kick, "a frame noted / changed with timed blocks OFF");
                S.Set(SettingsSchema.TimedBlocksEnabled, true, SettingSources.File);
                Play(0.5, 0);
                Check(ai2.Input.GuardWatch && AiGuardClockHook.On, "not watched again after the switch");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.File);
                Play(0.1, 0);
                Check(!ai2.Input.GuardWatch && !AiGuardClockHook.On, "the MASTER SWITCH off: the watch kept / the notes on");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                Play(0.1, 0);

                // ---- 10. the [summary] line; the logic's stop closes the hook
                string line = logic.AiClockStats.SummaryLine(logic.TimedBlockStats, AthleticsLogic.AiGuardWatchMetres, AthleticsLogic.AiGuardWatchLookSeconds);
                Check(line.Contains("AI correct blocks by timing 0 / 0-0.25 / 0.25-0.5 / 0.5-0.8 / 0.8-1: 3/0/1/1/0"), "the AI's score bands (held, flicker, unwatched 0; 0.3 s ~0.30; 0.05 s 0.5): " + line);
                Check(line.Contains("a known guard age 4 of 5 (80%)") && line.Contains("not watched 1"), "the AI clock's share: " + line);
                CallPrivate(logic, "CloseAiGuardClock");
                Check(!AiGuardClockHook.On && AiGuardClockHook.Stats == null, "the logic's stop left the hook on");
                Console.WriteLine("        step 102: the AI's guard 0.05 s before the blow - score 0.5; held / flicker 0; " + frames + " input frames through the real component, " + changed + " changed");
                Console.WriteLine("        " + line);
            }
            finally
            {
                AiGuardClockHook.SmokeNow = double.NaN;
                AiGuardClockHook.On = false;
                AiGuardClockHook.Stats = null;
                if (_logic != null) _logic.SmokePlayer = null;
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
                ImpactHooks.RestoreStunCap();
                ImpactHooks.KnockDown = null;
                ImpactHooks.BlockStun = null;
                ImpactHooks.PickupLock = null;
                ManagedParameters.SetParameter(ManagedParametersEnum.StunPeriodMax, capKeep);
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                S.Set(SettingsSchema.TimedBlocksEnabled, true, SettingSources.File);
                Step56BlockModel();
            }
        }

        /// <summary>Step 102's cost: a thousand men (600 with an enemy near) - the staggered watch over the ticks and every watched man's input
        /// frame through the real component - small and allocating nothing.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void AiGuardClockCostStaysFlat()
        {
            var keep = _logic;
            try
            {
                AthleticsDefaults();
                S.Set(SettingsSchema.TimedBlocksEnabled, true, SettingSources.File);
                var guard = new FakeGuardBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.GuardBody = guard;
                var tick = (Action<double>)Delegate.CreateDelegate(typeof(Action<double>), logic,
                    typeof(AthleticsLogic).GetMethod("TickAiGuardWatch", BindingFlags.Instance | BindingFlags.NonPublic)!);
                const int men = 1000;
                var comps = new AiInputComponent[men];
                var recs = new TrackedAgent[men];
                for (int i = 0; i < men; i++)
                {
                    int id = 30000 + i;
                    recs[i] = logic.Track(FakeAgent(id))!;
                    comps[i] = new AiInputComponent(recs[i].Agent, recs[i]);
                    if (i % 5 < 3) guard.Near.Add(id);
                }
                double t0 = 50000;
                for (int k = 0; k < 60; k++) tick(t0 + k / 60.0); // every man looked at, the near ones watched (their input state made once)
                Check(logic.AiClockStats.Watched == 600, "cost: watched " + logic.AiClockStats.Watched);
                const int ticks = 600;
                AppDomain.MonitoringIsEnabled = true;
                long bytes = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var swTick = new Stopwatch();
                var swFrames = new Stopwatch();
                long calls = 0;
                for (int k = 60; k < 60 + ticks; k++)
                {
                    double now = t0 + k / 60.0;
                    swTick.Start();
                    tick(now);
                    swTick.Stop();
                    AiGuardClockHook.SmokeNow = now;
                    uint defend = (k / 20) % 2 == 0 ? AiInputMath.DefendDown : 0u;
                    swFrames.Start();
                    for (int i = 0; i < men; i++)
                    {
                        if (recs[i].Input == null || !recs[i].Input!.GuardWatch) continue; // the engine calls only the hooked
                        var ef = Agent.EventControlFlag.None;
                        var mf = (Agent.MovementControlFlag)defend;
                        var v = new Vec2(0f, 1f);
                        comps[i].OnAIInputSet(ref ef, ref mf, ref v);
                        calls++;
                    }
                    swFrames.Stop();
                }
                long allocated = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - bytes;
                double msTick = swTick.Elapsed.TotalMilliseconds / ticks;
                double nsFrame = swFrames.Elapsed.TotalMilliseconds * 1e6 / Math.Max(1, calls);
                Console.WriteLine("        step 102 cost: " + men + " men, " + logic.AiClockStats.Watched + " watched - the watch " + msTick.ToString("0.000", CultureInfo.InvariantCulture)
                                  + " ms a tick (" + logic.AiClockStats.Looks + " looks), an input frame's managed part " + nsFrame.ToString("0", CultureInfo.InvariantCulture)
                                  + " ns (" + calls + " frames); " + allocated + " bytes allocated over " + ticks + " ticks");
                Check(msTick < 0.2, "cost: the watch over " + men + " men took " + msTick + " ms a tick (budget 0.2 ms)");
                Check(nsFrame < 2000, "cost: an input frame took " + nsFrame + " ns (budget 2 µs)");
                Check(allocated < 16 * 1024, "cost: the AI guard clock allocates - " + allocated + " bytes over " + ticks + " ticks");
            }
            finally
            {
                AiGuardClockHook.SmokeNow = double.NaN;
                AiGuardClockHook.On = false;
                AiGuardClockHook.Stats = null;
                _logic = keep;
                SetStatic(typeof(AthleticsLogic), "_current", keep);
            }
        }
    }
}

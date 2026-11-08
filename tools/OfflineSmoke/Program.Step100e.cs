using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;
using TraxCombat.Models;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline check for step 100e - THE SLOW MOTION ON YOUR BEST BLOCKS (AI_NOTES "Step 100" §5, DESIGN decision 91 (e)): through the REAL logic
    /// with a stand-in of the mission's time-speed requests (<see cref="ISlowMoBody"/> - its Remove throws on a missing id, as the game's does):
    /// your perfect block's riposte starts it, the ramp runs on the REAL clock and gives the request back at its end; the cooldown, another
    /// request present, a low timing and the AI's blocks skip it; another request coming in, the switch, the master switch, a pause, a stall and
    /// you leaving the field each end it early with the request given back; the mission end's give-back leaves nothing; the [summary] line.
    /// </summary>
    internal static partial class Program
    {
        private sealed class FakeSlowMoBody : ISlowMoBody
        {
            public readonly Dictionary<int, float> Requests = new Dictionary<int, float>();
            public double Now = 1000;
            public bool IsPaused;
            public bool IsMultiplayer = false;
            public int Sets, Removes;
            public float LowestOurs = 1;

            public double RealTime => Now;
            public bool Paused => IsPaused;
            public bool Multiplayer => IsMultiplayer;

            public double SceneSpeed(Mission m) => Requests.Count == 0 ? 1 : Math.Min(1, Requests.Values.Min());

            public bool VanillaRequestPresent(Mission m) => TimedSlowMoMath.VanillaRequestIds.Any(Requests.ContainsKey);

            public bool Has(Mission m, int id) => Requests.ContainsKey(id);

            public void Set(Mission m, int id, float speed)
            {
                Sets++;
                Requests[id] = speed;
                if (id == TimedSlowMoMath.RequestId && speed < LowestOurs) LowestOurs = speed;
            }

            public bool Remove(Mission m, int id)
            {
                if (!Requests.ContainsKey(id)) return false;
                Requests.Remove(id);
                Removes++;
                return true;
            }

            public bool Ours => Requests.ContainsKey(TimedSlowMoMath.RequestId);
            public float OursSpeed => Requests.TryGetValue(TimedSlowMoMath.RequestId, out float v) ? v : float.NaN;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UseFakeSlowMoBody()
        {
            Check(SlowMoBodyDefault.Current is GameSlowMoBody, "the default slow-mo body is not the game's: " + SlowMoBodyDefault.Current.GetType().Name);
            SlowMoBodyDefault.Current = new FakeSlowMoBody();
            Check(new AthleticsLogic().SlowMoBody == SlowMoBodyDefault.Current, "a new logic does not start with the default slow-mo body");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TimedSlowMoThroughTheLogic()
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
                             SettingsSchema.TimedBlockParryPercent, SettingsSchema.TimedBlockAiPercent, SettingsSchema.TimedBlockSlowMo,
                             SettingsSchema.TimedBlockSlowMoMinScore, SettingsSchema.TimedBlockSlowMoSpeed, SettingsSchema.TimedBlockSlowMoRealSeconds,
                             SettingsSchema.TimedBlockSlowMoCooldownSeconds })
                    S.Set(p, p.Default, SettingSources.File);
                Check(SettingsSchema.TimedBlockSlowMo.Default == 1 && Near((float)SettingsSchema.TimedBlockSlowMoMinScore.Default, 0.8f)
                      && Near((float)SettingsSchema.TimedBlockSlowMoSpeed.Default, 0.3f) && Near((float)SettingsSchema.TimedBlockSlowMoRealSeconds.Default, 0.5f)
                      && Near((float)SettingsSchema.TimedBlockSlowMoCooldownSeconds.Default, 4f),
                    "the embedded slow-mo defaults are not the design's");
                S.Set(SettingsSchema.RiposteStunMaxSeconds, 1.0, SettingSources.File);
                var inputs = new FakeImpactInputs();
                var body = new FakeImpactBody();
                var guard = new FakeGuardBody();
                var slow = new FakeSlowMoBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = body;
                logic.ImpactInputs = inputs;
                logic.GuardBody = guard;
                logic.SlowMoBody = slow;
                logic.ImpactDice = new ScriptDice { Rest = 0.5 };
                double t = 80000;
                logic.TickImpact(t);
                int next = 6600;

                TrackedAgent Man(double bar, bool shield)
                {
                    int i = next++;
                    var m = logic.Track(FakeAgent(i))!;
                    m.AthleticsSkill = 125;
                    SetBar(m, bar);
                    inputs.Weapons[i] = (1.2, 92, ImpactDamageType.Cut, 100);
                    inputs.Guards[i] = (shield ? 4.0 : 1.2, 100);
                    if (shield) inputs.Shields.Add(i);
                    return m;
                }
                void Tick(double seconds = 1.0 / 60) => TickFor(logic, ref t, seconds);
                var you = Man(1.0, true);
                logic.SmokePlayer = you.Agent;
                logic.SmokePlayerAiControlled = false;
                guard.Contacts[you.Agent.Index] = (GuardRead.Active, double.NaN);
                var stats = logic.SlowMoStats;

                // your guard pressed `age` s before a fresh man's blow; the riposte made from the next tick
                void Block(double age)
                {
                    guard.Keys[you.Agent.Index] = 0;
                    Tick(0.1);
                    guard.Keys[you.Agent.Index] = 0x800;
                    Tick(age);
                    var a = Man(1.0, false);
                    logic.ImpactModelHitTaken(a.Agent, you.Agent, RightBlock());
                    Tick();
                }
                void Real(double seconds)
                {
                    double end = slow.Now + seconds;
                    while (slow.Now < end - 1e-9)
                    {
                        slow.Now += Math.Min(1.0 / 60, end - slow.Now);
                        logic.TickSlowMo();
                    }
                }

                // ---- 1. your perfect block: started, the floor reached on the REAL clock, given back at the end
                Block(0.05);
                Check(stats.Played == 1 && logic.SlowMo.Playing && slow.Ours, "your perfect block's riposte did not start the slow motion: played " + stats.Played);
                Real(0.06);
                Check(Math.Abs(slow.OursSpeed - 0.3f) < 0.02f, "the slow motion's floor after the ease in: " + slow.OursSpeed);
                Real(0.5);
                Check(!slow.Ours && !logic.SlowMo.Playing && stats.Ended[(int)SlowMoEnd.Finished] == 1 && Math.Abs(slow.LowestOurs - 0.3f) < 1e-3,
                    "the slow motion's end: request left " + slow.Ours + ", finished " + stats.Ended[(int)SlowMoEnd.Finished] + ", lowest " + slow.LowestOurs);

                // ---- 2. the cooldown (4 real s from the start), another request present, a low timing, the AI's block
                Block(0.05);
                Check(stats.Skipped[(int)SlowMoSkip.Cooldown] == 1 && !slow.Ours, "within the cooldown: " + stats.Skipped[(int)SlowMoSkip.Cooldown]);
                slow.Now += 5;
                slow.Requests[864] = 0.25f; // the order menu's slow-down
                Block(0.05);
                Check(stats.Skipped[(int)SlowMoSkip.OtherRequest] == 1 && !slow.Ours && slow.Requests[864] == 0.25f, "another request present: " + stats.Skipped[(int)SlowMoSkip.OtherRequest]);
                slow.Requests.Remove(864);
                Block(0.27); // a timing of ~0.73: the riposte lands (~62 %) but below the slow motion's 0.8
                Check(stats.Skipped[(int)SlowMoSkip.LowScore] == 1 && !slow.Ours, "a low timing: " + stats.Skipped[(int)SlowMoSkip.LowScore] + " (applied band 3 " + logic.TimedBlockStats.RiposteApplied[3] + ")");
                S.Set(SettingsSchema.TimedBlockAiPercent, 100, SettingSources.File);
                var aiBlocker = Man(1.0, true);
                guard.Contacts[aiBlocker.Agent.Index] = (GuardRead.Active, 0.0);
                int appliedBefore = logic.TimedBlockStats.RiposteApplied[4];
                logic.ImpactModelHitTaken(Man(1.0, false).Agent, aiBlocker.Agent, RightBlock());
                Tick();
                Check(logic.TimedBlockStats.RiposteApplied[4] == appliedBefore + 1 && !slow.Ours && stats.Played == 1, "the AI's perfect block slowed the game");
                S.Set(SettingsSchema.TimedBlockAiPercent, SettingsSchema.TimedBlockAiPercent.Default, SettingSources.File);

                // ---- 3. ended early, the request given back each time: another request comes in, the switch, the master switch, a pause, a stall,
                //         you off the field
                void StartOne()
                {
                    slow.Now += 5;
                    Block(0.05);
                    Real(0.1);
                    Check(slow.Ours && logic.SlowMo.Playing, "the slow motion did not start (played " + stats.Played + ")");
                }
                StartOne();
                slow.Requests[864] = 0.25f;
                Real(1.0 / 60);
                Check(!slow.Ours && slow.Requests.ContainsKey(864) && stats.Ended[(int)SlowMoEnd.OtherRequest] == 1, "another request coming in: ours left " + slow.Ours);
                slow.Requests.Remove(864);
                StartOne();
                S.Set(SettingsSchema.TimedBlockSlowMo, false, SettingSources.File);
                Real(1.0 / 60);
                Check(!slow.Ours && stats.Ended[(int)SlowMoEnd.SwitchedOff] == 1, "the switch off: ours left " + slow.Ours);
                S.Set(SettingsSchema.TimedBlockSlowMo, true, SettingSources.File);
                StartOne();
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.File);
                Real(1.0 / 60);
                Check(!slow.Ours && stats.Ended[(int)SlowMoEnd.SwitchedOff] == 2, "the MASTER SWITCH off: ours left " + slow.Ours);
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                Tick();
                StartOne();
                slow.IsPaused = true;
                Real(1.0 / 60);
                Check(!slow.Ours && stats.Ended[(int)SlowMoEnd.Paused] == 1, "a pause: ours left " + slow.Ours);
                slow.IsPaused = false;
                StartOne();
                slow.Now += 1.0; // a frame stall of 1 real s
                logic.TickSlowMo();
                Check(!slow.Ours && stats.Ended[(int)SlowMoEnd.Paused] == 2, "a stall: ours left " + slow.Ours);
                StartOne();
                logic.SmokePlayer = null;
                Real(1.0 / 60);
                Check(!slow.Ours && stats.Ended[(int)SlowMoEnd.YouGone] == 1, "you off the field: ours left " + slow.Ours);
                logic.SmokePlayer = you.Agent;

                // ---- 4. the mission end's give-back: nothing left, counted, said
                StartOne();
                logic.GiveBackSlowMo("the smoke's mission end");
                Check(!slow.Ours && stats.Ended[(int)SlowMoEnd.MissionEnd] == 1 && stats.CheckedAtEnd && stats.LeftOver == 0 && stats.RemoveRefused == 0 && stats.Errors == 0,
                    "the mission end's give-back: ours left " + slow.Ours + ", left over " + stats.LeftOver + ", refused " + stats.RemoveRefused + ", errors " + stats.Errors);
                logic.GiveBackSlowMo("twice");
                Check(stats.LeftOver == 0 && stats.Ended[(int)SlowMoEnd.MissionEnd] == 1, "a second give-back counted something");

                // ---- 5. the [summary] line
                string line = stats.SummaryLine(TimedSlowMoRules.From(S));
                Check(line.StartsWith("timed slow-mo (step 100e; ON): played 8", StringComparison.Ordinal) && line.Contains("requests left over at the mission end 0 (must be 0)")
                      && line.Contains("finished 1") && line.Contains("the cooldown 1"),
                    "the [summary] timed slow-mo line: " + line);
                Console.WriteLine("        step 100e: your perfect block - the game at 0.30, back to 1 over 0.5 real s; " + slow.Sets + " request writes, " + slow.Removes + " give-backs");
                Console.WriteLine("        " + line);
            }
            finally
            {
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
                S.Set(SettingsSchema.TimedBlockSlowMo, true, SettingSources.File);
                S.Set(SettingsSchema.TimedBlockAiPercent, SettingsSchema.TimedBlockAiPercent.Default, SettingSources.File);
                Step56BlockModel();
            }
        }
    }
}

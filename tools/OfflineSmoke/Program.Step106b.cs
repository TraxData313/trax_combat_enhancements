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
    /// Offline check for step 106b - OPENINGS, THE FORCED GUARD (AI_NOTES "Step 106" §106b, DESIGN decision 95 (i)): through the REAL logic with
    /// stand-ins of the engine (his guard at contact, your swing, your weapon, the forced man's body and pose) and the REAL input frame
    /// (<see cref="AiInputHook.ApplyOpening"/>): with <c>OpeningsEnabled</c> on, his parry of your swing forces his guard to the balance side (his
    /// attack out); your perfect follow-up beats the live roll - the guard HELD; he parries anyway = "blocked anyway" and the next link forces again;
    /// a wrong side gives his guard back; a shield's first link holds (he wins the roll), its second gives way - the shield LOWERED (no guard bit
    /// left), your hit lands, he reels, the force ends; the off-balance time, you / a man you drive never forced, the live switch and the master
    /// switch; THE TEST TOOL with the live switch OFF: the key cycles low / high / left / right / lowered / lowered by CanDefend on the nearest enemy,
    /// his pose read back after 0.5 s and at the end, the flag given back; the [summary] line.
    /// </summary>
    internal static partial class Program
    {
        private sealed class FakeOpeningBody : IOpeningBody
        {
            public readonly HashSet<int> You = new HashSet<int>();
            public readonly HashSet<int> Riding = new HashSet<int>();
            public readonly Dictionary<int, OpeningPose> Poses = new Dictionary<int, OpeningPose>();
            public readonly HashSet<int> NoDefend = new HashSet<int>();
            public int Hooks, Releases, FlagWrites;
            public Agent? Near;

            public bool AiDriven(TrackedAgent st) => !You.Contains(st.AgentIndex);
            public bool OnFoot(TrackedAgent st) => !Riding.Contains(st.AgentIndex);
            public Agent? NearestFoe(Agent you, float metres) => Near;

            public AiInputHook.HookResult Hook(TrackedAgent st)
            {
                Hooks++;
                return AiInputHook.HookResult.AlreadyHooked;
            }

            public void Release(TrackedAgent st) => Releases++;

            public OpeningPose Pose(TrackedAgent st) =>
                Poses.TryGetValue(st.AgentIndex, out var p) ? p : new OpeningPose { Action = "act_idle", Direction = -1, Stage = -1, Read = true };

            public bool GuardShown(TrackedAgent st, int guard) => // step 108: the recovery's cheap read
                Poses.TryGetValue(st.AgentIndex, out var p) && OpeningForceMath.PoseMatches(guard, false, p.Direction, p.Defending);

            public bool CanDefend(TrackedAgent st) => !NoDefend.Contains(st.AgentIndex);

            public void SetCanDefend(TrackedAgent st, bool on)
            {
                FlagWrites++;
                if (on) NoDefend.Remove(st.AgentIndex);
                else NoDefend.Add(st.AgentIndex);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UseFakeOpeningBody()
        {
            Check(OpeningBodyDefault.Current is GameOpeningBody, "the default opening body is not the game's: " + OpeningBodyDefault.Current.GetType().Name);
            OpeningBodyDefault.Current = new FakeOpeningBody();
            Check(new AthleticsLogic().OpeningBody == OpeningBodyDefault.Current, "a new logic does not start with the default opening body");
            Check((uint)Agent.MovementControlFlag.DefendLeft == OpeningForceMath.DefendLeft && (uint)Agent.MovementControlFlag.DefendRight == OpeningForceMath.DefendRight
                  && (uint)Agent.MovementControlFlag.DefendUp == OpeningForceMath.DefendUp && (uint)Agent.MovementControlFlag.DefendDown == OpeningForceMath.DefendDown
                  && (uint)Agent.MovementControlFlag.DefendAuto == OpeningForceMath.DefendAuto && (uint)Agent.MovementControlFlag.DefendBlock == OpeningForceMath.DefendBlock
                  && (uint)Agent.MovementControlFlag.DefendMask == AiInputMath.DefendMask, "the engine's defend bits are not the Core's");
            Check((int)Agent.UsageDirection.DefendUp == OpeningMath.GuardUp && (int)Agent.UsageDirection.DefendDown == OpeningMath.GuardDown
                  && (int)Agent.UsageDirection.DefendLeft == OpeningMath.GuardLeft && (int)Agent.UsageDirection.DefendRight == OpeningMath.GuardRight,
                "the engine's guard directions are not the Core's");
            Check((uint)AgentFlag.CanDefend == 0x10u, "the engine's CanDefend flag moved");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void OpeningForceThroughTheLogic()
        {
            var keep = _logic;
            float capKeep = StunCap;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                BlocksStunDefaults();
                Step56BlockModel();
                foreach (var p in SettingsSchema.All.Where(p => p.Key.StartsWith("Opening", StringComparison.Ordinal) || p.Key.StartsWith("TimedBlock", StringComparison.Ordinal)
                                                             || p.Key.StartsWith("PerfectStrike", StringComparison.Ordinal) || p.Key.StartsWith("ReelingHit", StringComparison.Ordinal)))
                    S.Set(p, p.Default, SettingSources.File);
                S.Set(SettingsSchema.OpeningHoldsEnabled, false, SettingSources.File); // step 107: step 106's path (the hold-block openings off)
                S.Set(SettingsSchema.TimedBlockSlowMo, false, SettingSources.File);
                S.Set(SettingsSchema.ImpactMeetRulesEnabled, false, SettingSources.File);
                Check(SettingsSchema.OpeningGuardTest.Default == 0 && KeyNames.Name(SettingsSchema.OpeningGuardTestKey.Default) == "K",
                    "the embedded test tool's defaults are not off / K");
                S.Set(SettingsSchema.OpeningsEnabled, true, SettingSources.File);
                var inputs = new FakeImpactInputs { Damage = 20 };
                var anim = new FakeAnimBody();
                var body = new FakeOpeningBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = new FakeImpactBody();
                logic.ImpactInputs = inputs;
                logic.GuardBody = new FakeGuardBody();
                logic.SlowMoBody = new FakeSlowMoBody();
                logic.MessageSink = new FakeMessageSink();
                logic.AnimBody = anim;
                logic.OpeningBody = body;
                logic.ImpactDice = new ScriptDice { Rest = 0.999 };
                logic.OpeningDice = new ScriptDice { Rest = 0.0 }; // the first candidate guard; every live roll at 0 (through unless his chance is 100 %)
                var guards = new Dictionary<int, int>();
                int swingDir = OpeningMath.Unknown;
                logic.OpeningGuardRead = a => guards.TryGetValue(a.Index, out int g) ? g : -1;
                logic.OpeningSwingRead = a => swingDir;
                logic.OpeningCommitRead = a => swingDir; // step 106f: the swing already committed at its wind-up (judged there, as before)
                logic.OpeningWeaponRead = a => OpeningMath.AllAttacks;
                double t = 106200;
                logic.TickImpact(t);
                int next = 10620;
                var fs = logic.OpeningForceStats;
                var ar = Rules;

                TrackedAgent Man(bool shield)
                {
                    int i = next++;
                    var m = logic.Track(FakeAgent(i))!;
                    m.AthleticsSkill = 125;
                    SetBar(m, 1.0);
                    inputs.Weapons[i] = (1.2, 92, ImpactDamageType.Cut, 100);
                    inputs.Guards[i] = (shield ? 4.0 : 1.2, 100);
                    if (shield)
                    {
                        inputs.Shields.Add(i);
                        anim.Shields.Add(i);
                    }
                    return m;
                }
                void Tick(double seconds = 1.0 / 60) => TickFor(logic, ref t, seconds);
                double Now() => t - 1.0 / 60;
                void Parried(TrackedAgent a, TrackedAgent v, Agent.UsageDirection d) => logic.ImpactModelHitTaken(a.Agent, v.Agent, Swing(d, false, false, CombatCollisionResult.Parried));
                void ShieldRight(TrackedAgent a, TrackedAgent v, Agent.UsageDirection d) => logic.ImpactModelHitTaken(a.Agent, v.Agent, Swing(d, true, true, CombatCollisionResult.Blocked));
                void Lands(TrackedAgent a, TrackedAgent v, Agent.UsageDirection d)
                {
                    swingDir = (int)d;
                    logic.ImpactModelHitTaken(a.Agent, v.Agent, Swing(d, false, false, CombatCollisionResult.StrikeAgent));
                }
                void WindUp(TrackedAgent you2, int dir)
                {
                    swingDir = dir;
                    logic.ObserveAction(you2, ActReady, Now(), in ar);
                }
                // one frame of the engine asking his hook: the REAL forced-guard frame on what his own AI wanted
                uint Frame(TrackedAgent m, uint his) => m.Input != null && m.Input.Opening ? AiInputHook.ApplyOpening(m.Input, his) : his;
                const uint his = 0x1 | 0x40 | 0x2000; // Forward | AttackLeft | DefendDown - his own wish
                const uint guardBits = 0xFC00;

                var you = Man(false);
                body.You.Add(you.AgentIndex);
                logic.SmokePlayer = you.Agent;
                logic.SmokePlayerAiControlled = false;
                var ai = Man(false);

                // ---- 1. OFF BALANCE, LIVE: your right swing parried by his left guard -> his guard FORCED to the high guard, his attack out
                guards[ai.Agent.Index] = OpeningMath.GuardLeft;
                Parried(you, ai, Agent.UsageDirection.AttackRight);
                Check(ReferenceEquals(logic.OpeningForced, ai) && logic.OpeningForceKind == OpeningForceKind.Balance && fs.Balance == 1 && body.Hooks == 1,
                    "the parried swing did not force his guard (off balance): " + logic.OpeningForceKind);
                uint f1 = Frame(ai, his);
                Check(f1 == (0x1u | 0x1000u), "the off-balance frame is not Forward | DefendUp: 0x" + f1.ToString("X"));

                // ---- 2. your thrust ON the flash: the live roll (chance 0) - through: the guard HELD; he parries anyway = blocked anyway; the next
                //         link forces again
                Tick(0.3);
                WindUp(you, OpeningMath.AttackDown);
                Check(logic.OpeningForceKind == OpeningForceKind.Held && fs.RollsThrough == 1 && fs.Held == 1 && !logic.OpeningForceLowered,
                    "your perfect follow-up did not hold his guard: " + logic.OpeningForceKind + " / " + fs.RollsThrough);
                LogHas("LIVE: he FAILS the block roll (0%)");
                Check(Frame(ai, his) == (0x1u | 0x1000u), "the held frame is not the balance guard");
                Check(body.NoDefend.Contains(ai.AgentIndex) && fs.HoldFlagsOff == 1, "step 106f: the held follow-up did not take his 'can defend' flag too");
                Tick(0.3);
                guards[ai.Agent.Index] = OpeningMath.GuardDown;
                Parried(you, ai, Agent.UsageDirection.AttackDown);
                Check(fs.BlockedAnyway == 1 && fs.Ends[(int)OpeningForceEnd.Resolved] == 1 && logic.OpeningForceKind == OpeningForceKind.Balance && logic.OpeningChain.K == 2,
                    "a held follow-up he parried anyway was not counted / the next link not forced: " + fs.BlockedAnyway);
                Check(!body.NoDefend.Contains(ai.AgentIndex) && fs.Anyway[(int)OpeningAnyway.OwnGuard] == 1,
                    "step 106f: the 'can defend' flag was not given back at the blow / a wrong cause");
                LogHas("BLOCKED ANYWAY");

                // ---- 3. a wrong side: his own guard back
                Tick(0.3);
                WindUp(you, OpeningMath.Opposite(logic.LastOpening.Opening));
                Check(logic.OpeningForced == null && fs.WrongSide == 1 && fs.Ends[(int)OpeningForceEnd.WrongSide] == 1 && !ai.Input!.Opening,
                    "a wrong-side follow-up kept the force");
                Check(Frame(ai, his) == his, "his input was still touched after the force ended");

                // ---- 4. A SHIELD: its first link holds (chance 100 % - he wins the roll), its second gives way - LOWERED; your hit lands, he reels
                Tick(3);
                var sh = Man(true);
                guards[sh.Agent.Index] = OpeningMath.GuardUp;
                ShieldRight(you, sh, Agent.UsageDirection.AttackUp);
                Check(logic.OpeningForceKind == OpeningForceKind.Balance && ReferenceEquals(logic.OpeningForced, sh), "the shield's first block forced nothing");
                Tick(0.3);
                WindUp(you, logic.LastOpening.Opening);
                Check(logic.OpeningForced == null && fs.RollsBlocked == 1 && fs.Ends[(int)OpeningForceEnd.HeBlocks] == 1, "the shield's first link did not hold (he must win the roll)");
                Tick(0.3);
                ShieldRight(you, sh, Agent.UsageDirection.AttackUp);
                Check(logic.OpeningChain.K == 2 && logic.OpeningForceKind == OpeningForceKind.Balance, "the shield's second block forced nothing");
                Tick(0.3);
                WindUp(you, logic.LastOpening.Opening);
                Check(logic.OpeningForceKind == OpeningForceKind.Held && logic.OpeningForceLowered && fs.ShieldLowerings == 1 && sh.Input!.OpeningLower,
                    "the shield's second link was not LOWERED: " + logic.OpeningForceKind);
                LogHas("with his shield LOWERED");
                uint fl = Frame(sh, his | 0x8000 | 0x4000);
                Check((fl & guardBits) == 0 && (fl & 0x3C0u) == 0 && (fl & 0x1u) != 0, "the lowered frame kept a guard / attack bit: 0x" + fl.ToString("X"));
                Tick(0.2);
                Lands(you, sh, (Agent.UsageDirection)logic.LastOpening.Opening);
                // (in game the landed blow is decided in the decorator BEFORE OnMeleeHit - "he reels" ends it; the smoke's order reaches the contact first - "the blow resolved")
                Check(logic.OpeningForced == null && fs.HeldLanded == 1 && logic.LastOpeningHit == OpeningHit.Staggers, "your hit through the lowered shield did not land and end the force: " + logic.LastOpeningHit);

                // ---- 5. the off-balance time runs out
                Tick(3);
                guards[ai.Agent.Index] = OpeningMath.GuardLeft;
                Parried(you, ai, Agent.UsageDirection.AttackRight);
                Check(logic.OpeningForceKind == OpeningForceKind.Balance, "no force for the timeout check");
                Tick(2.5);
                Check(logic.OpeningForced == null && !ai.Input!.Opening, "the force outlived the off-balance time");

                // ---- 5b. step 106 follow-up: SPAMMING into his guard - the attack lock at most 1 s a run, then he is free until 1 s free
                Tick(3);
                int capped = fs.LockCapped, spent = fs.LockSpent;
                Parried(you, ai, Agent.UsageDirection.AttackRight);
                Check(logic.OpeningForceKind == OpeningForceKind.Balance && fs.LockCapped == capped + 1, "the first off-balance force was not capped at 1 s");
                Tick(0.6);
                Parried(you, ai, Agent.UsageDirection.AttackRight);
                Check(logic.OpeningForceKind == OpeningForceKind.Balance && ReferenceEquals(logic.OpeningForced, ai), "the re-armed force (same run) did not hold");
                Tick(0.5);
                Check(logic.OpeningForced == null && !ai.Input!.Opening, "the re-armed force outlived the run's 1 s - spamming still freezes him");
                Parried(you, ai, Agent.UsageDirection.AttackRight);
                Check(logic.OpeningForced == null && fs.LockSpent == spent + 1 && Frame(ai, his) == his, "a block inside the spent run forced him again");
                Tick(1.1);
                Parried(you, ai, Agent.UsageDirection.AttackRight);
                Check(logic.OpeningForceKind == OpeningForceKind.Balance && ReferenceEquals(logic.OpeningForced, ai), "after 1 s free a new run did not force him");
                Tick(3);

                // ---- 6. never a man you drive
                Tick(3);
                body.You.Add(ai.AgentIndex);
                int notAi = fs.NotAi;
                Parried(you, ai, Agent.UsageDirection.AttackRight);
                Check(logic.OpeningForced == null && fs.NotAi == notAi + 1, "a man you drive was forced");
                body.You.Remove(ai.AgentIndex);

                // ---- 7. the live switch and the master switch let go at the next tick
                Tick(3);
                Parried(you, ai, Agent.UsageDirection.AttackRight);
                Check(logic.OpeningForced != null, "no force for the switch check");
                S.Set(SettingsSchema.OpeningsEnabled, false, SettingSources.File);
                Tick();
                Check(logic.OpeningForced == null && !ai.Input!.Opening, "the live switch off kept the force");
                S.Set(SettingsSchema.OpeningsEnabled, true, SettingSources.File);
                Tick(3);
                Parried(you, ai, Agent.UsageDirection.AttackRight);
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.File);
                Tick();
                Check(logic.OpeningForced == null && !ai.Input!.Opening, "the MASTER SWITCH off kept the force");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                Tick(3);

                // ---- 8. THE TEST TOOL, the live switch OFF: six guards on the nearest enemy, read back after 0.5 s and at the end
                S.Set(SettingsSchema.OpeningsEnabled, false, SettingSources.File);
                S.Set(SettingsSchema.OpeningGuardTest, true, SettingSources.File);
                body.Near = ai.Agent;
                body.Poses[ai.AgentIndex] = new OpeningPose { Action = "act_defend_down_1h", Direction = OpeningMath.GuardDown, Stage = 5, Defending = true, Read = true };
                anim.Presses = 1;
                Tick();
                Check(logic.OpeningForceKind == OpeningForceKind.Test && fs.Tests == 1 && Frame(ai, 0x1) == (0x1u | 0x2000u), "the test key did not force the LOW guard");
                LogHas("[guard test 1/6] ");
                Tick(0.6);
                Check(fs.PosesRead == 1 && fs.PosesMatched == 1, "the pose was not read back after 0.5 s as wanted: " + fs.PosesRead + " / " + fs.PosesMatched);
                LogHas("-> AS WANTED");
                Tick(1.6);
                Check(logic.OpeningForced == null && fs.PosesRead == 2 && fs.Ends[(int)OpeningForceEnd.Timeout] >= 1, "the test did not end after 2 s with a read-back");
                for (int i = 0; i < 4; i++)
                {
                    anim.Presses = 1;
                    Tick();
                }
                Check(logic.OpeningForceLowered && ai.Input!.OpeningLower && fs.Tests == 5, "the 5th press was not the LOWERED guard");
                body.Poses[ai.AgentIndex] = new OpeningPose { Action = "act_idle", Direction = -1, Stage = -1, Defending = false, Read = true };
                anim.Presses = 1;
                Tick();
                Check(body.NoDefend.Contains(ai.AgentIndex) && fs.FlagsOff - fs.HoldFlagsOff == 1, "the 6th press did not take the CanDefend flag off");
                Tick(2.5);
                Check(!body.NoDefend.Contains(ai.AgentIndex) && fs.FlagsBack == fs.FlagsOff && logic.OpeningForced == null, "the CanDefend flag was not given back");
                anim.Shift = true;
                anim.Presses = 1;
                Tick();
                Check(logic.OpeningForceLowered, "Left Shift did not go back to the lowered guard");
                anim.Shift = false;
                S.Set(SettingsSchema.OpeningGuardTest, false, SettingSources.File);
                Tick();
                Check(logic.OpeningForced == null, "the test tool off kept the force");
                body.Near = null;
                anim.Presses = 1;
                S.Set(SettingsSchema.OpeningGuardTest, true, SettingSources.File);
                Tick();
                Check(logic.OpeningForced == null && anim.Said.Last().StartsWith("[guard test] nobody to force", StringComparison.Ordinal), "the test with nobody near forced someone");
                S.Set(SettingsSchema.OpeningGuardTest, false, SettingSources.File);

                // ---- 9. no errors; the [summary] line
                Check(fs.Errors == 0 && logic.OpeningStats.Errors == 0, "the forced guard logged errors: " + fs.Errors);
                string line = fs.SummaryLine(OpeningRules.From(S), false);
                Check(line.Contains("held after a won roll 2 (shield / guard LOWERED 1)") && line.Contains("BLOCKED ANYWAY 1") && line.Contains("CanDefend off / back 3 / 3 (held follow-ups 2)"),
                    "the [summary] line: " + line);
                Console.WriteLine("        step 106b: " + fs.Balance + " forced off balance, " + fs.Held + " held, " + fs.Tests + " tests, " + fs.Frames + " frames summed");
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
                ImpactHooks.PushOut = null;
                ImpactHooks.Perfect = null;
                ManagedParameters.SetParameter(ManagedParametersEnum.StunPeriodMax, capKeep);
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                foreach (var p in SettingsSchema.All.Where(p => p.Key.StartsWith("Opening", StringComparison.Ordinal) || p.Key.StartsWith("TimedBlock", StringComparison.Ordinal)
                                                             || p.Key.StartsWith("PerfectStrike", StringComparison.Ordinal) || p.Key.StartsWith("ReelingHit", StringComparison.Ordinal)))
                    S.Set(p, p.Default, SettingSources.File);
                S.Set(SettingsSchema.ImpactMeetRulesEnabled, SettingsSchema.ImpactMeetRulesEnabled.Default, SettingSources.File);
                Step56BlockModel();
            }
        }
    }
}

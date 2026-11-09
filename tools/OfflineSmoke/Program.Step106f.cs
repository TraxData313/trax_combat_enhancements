using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Hud;
using TraxCombat.Missions;
using TraxCombat.Models;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline check for step 106f - THE OPENINGS' FIXES FROM TONI'S FIRST PLAYTEST (AI_NOTES "106f", DESIGN decision 95 (o)), through the REAL logic
    /// with stand-ins of the engine: (1) his parry of your swing forces the balance guard AT ONCE and it is held through your wind-up; the live pose is
    /// read back at 0.2 s (shown) / 0.5 s (another guard = NOT shown), counted; (2) your follow-up is judged from the swing it COMMITS to (its
    /// release) - a wind-up whose first read was another side still opens him, a wind-up read "into the side" that commits elsewhere does not; (3) a
    /// held follow-up takes his 'can defend' flag and gives it back at the blow; blocked anyway is explained (your swing went another way at contact);
    /// (4) the red arrow in the walk-about mode (the training field) while you fight a man off balance, never in a conversation, gone after.
    /// </summary>
    internal static partial class Program
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Openings106fThroughTheLogic()
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
                S.Set(SettingsSchema.TimedBlockSlowMo, false, SettingSources.File);
                S.Set(SettingsSchema.ImpactMeetRulesEnabled, false, SettingSources.File);
                Check(SettingsSchema.OpeningsEnabled.Default == 1 && SettingsSchema.OpeningHoldTakesDefend.Default == 1, "the embedded openings / held-guard flag defaults are not ON");
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
                logic.OpeningDice = new ScriptDice { Rest = 0.0 }; // the first candidate guard; every live roll through
                var guards = new Dictionary<int, int>();
                int firstRead = OpeningMath.Unknown, committed = OpeningMath.Unknown;
                logic.OpeningGuardRead = a => guards.TryGetValue(a.Index, out int g) ? g : -1;
                logic.OpeningSwingRead = a => firstRead;      // the ready's first read
                logic.OpeningCommitRead = a => committed;     // the swing it commits to (unknown until "released")
                logic.OpeningWeaponRead = a => OpeningMath.AllAttacks;
                double t = 106600;
                logic.TickImpact(t);
                int next = 10660;
                var fs = logic.OpeningForceStats;
                var st = logic.OpeningStats;
                var ar = Rules;

                TrackedAgent Man()
                {
                    int i = next++;
                    var m = logic.Track(FakeAgent(i))!;
                    m.AthleticsSkill = 125;
                    SetBar(m, 1.0);
                    inputs.Weapons[i] = (1.2, 92, ImpactDamageType.Cut, 100);
                    inputs.Guards[i] = (1.2, 100);
                    return m;
                }
                void Tick(double seconds = 1.0 / 60) => TickFor(logic, ref t, seconds);
                double Now() => t - 1.0 / 60;
                void Parried(TrackedAgent a, TrackedAgent v, int d) => logic.ImpactModelHitTaken(a.Agent, v.Agent, Swing((Agent.UsageDirection)d, false, false, CombatCollisionResult.Parried));
                void WindUp(TrackedAgent you2) => logic.ObserveAction(you2, ActReady, Now(), in ar);
                OpeningPose Guarding(int g) => new OpeningPose { Action = "act_defend_1h", Direction = g, Stage = 5, Defending = true, Read = true };

                var you = Man();
                body.You.Add(you.AgentIndex);
                logic.SmokePlayer = you.Agent;
                logic.SmokePlayerAiControlled = false;
                var ai = Man();

                // ---- 1. his parry: the balance guard forced AT ONCE, read back shown at 0.2 s
                guards[ai.Agent.Index] = OpeningMath.GuardLeft;
                Parried(you, ai, OpeningMath.AttackRight);
                var plan = logic.LastOpening;
                Check(plan.Opens && logic.OpeningForceKind == OpeningForceKind.Balance && ReferenceEquals(logic.OpeningForced, ai), "his parry did not force the balance guard at once");
                body.Poses[ai.AgentIndex] = Guarding(plan.Guard);
                Tick(0.25);
                Check(fs.PoseWeapon[(int)OpeningPoseResult.Shown] == 1, "the balance pose was not read back as shown at 0.2 s");
                LogHas("openings: the balance pose read back - ");
                LogHas("-> shown at 0.2 s");

                // ---- 2. the wind-up's first read says ANOTHER side, the swing is not committed yet: no verdict, the pose held through the wind-up
                firstRead = OpeningMath.Opposite(plan.Opening);
                committed = OpeningMath.Unknown;
                WindUp(you);
                Tick();
                Check(logic.OpeningForceKind == OpeningForceKind.Balance && fs.WrongSide == 0 && st.JudgedAt.Sum() == 0,
                    "the follow-up was judged from the wind-up's first read / the balance pose let go at the wind-up: " + logic.OpeningForceKind + ", wrong side " + fs.WrongSide);
                //      the swing commits INTO the side (its release): judged there - opened, the live roll through, his guard HELD and 'can defend' off
                committed = plan.Opening;
                Tick();
                Check(st.JudgedAt[(int)OpeningJudgedAt.Release] == 1 && st.WindUpDiffer == 1 && logic.LastOpeningVerdict == OpeningVerdict.Opened,
                    "the committed swing was not judged at the release into the side: " + logic.LastOpeningVerdict);
                Check(logic.OpeningForceKind == OpeningForceKind.Held && fs.RollsThrough == 1 && body.NoDefend.Contains(ai.AgentIndex) && fs.HoldFlagsOff == 1,
                    "the committed follow-up was not held / did not take his 'can defend' flag: " + logic.OpeningForceKind);
                LogHas("(judged at the release; the wind-up first read ");
                LogHas("his 'can defend' flag off until the blow");

                // ---- 3. it meets him from ANOTHER direction and he parries: BLOCKED ANYWAY - cause: your swing went another way; the flag given back
                guards[ai.Agent.Index] = OpeningMath.GuardDown;
                Tick(0.2);
                Parried(you, ai, OpeningMath.Opposite(plan.Opening));
                Check(fs.BlockedAnyway == 1 && fs.Anyway[(int)OpeningAnyway.OtherDirection] == 1 && !body.NoDefend.Contains(ai.AgentIndex),
                    "blocked anyway was not explained by the other direction / the flag not given back: " + fs.BlockedAnyway);
                LogHas("cause: your swing went another way at contact");
                Check(st.DirDiffer >= 1, "the judged direction vs the contact's did not count the difference");

                // ---- 4. a new run: the pose NOT shown (he keeps another guard) - read again at 0.5 s and counted
                Tick(3);
                guards[ai.Agent.Index] = OpeningMath.GuardLeft;
                Parried(you, ai, OpeningMath.AttackRight);
                plan = logic.LastOpening;
                int other = plan.Guard == OpeningMath.GuardLeft ? OpeningMath.GuardRight : OpeningMath.GuardLeft;
                body.Poses[ai.AgentIndex] = Guarding(other);
                Tick(0.25);
                Check(fs.PoseWeapon[(int)OpeningPoseResult.OtherGuard] == 0, "a pose not shown at 0.2 s was judged before the last read");
                Tick(0.3);
                Check(fs.PoseWeapon[(int)OpeningPoseResult.OtherGuard] == 1, "the pose not shown by 0.5 s was not counted: " + string.Join("/", fs.PoseWeapon));
                LogHas("-> NOT shown - defending another way");

                // ---- 5. the wind-up's first read INTO the side, the swing commits elsewhere: another side - his own guard back
                firstRead = plan.Opening;
                committed = OpeningMath.Unknown;
                WindUp(you);
                Tick();
                committed = OpeningMath.Opposite(plan.Opening);
                Tick();
                Check(logic.LastOpeningVerdict == OpeningVerdict.WrongDirection && fs.WrongSide == 1 && logic.OpeningForced == null && st.WindUpDiffer == 2,
                    "a swing that committed elsewhere was still judged into the side: " + logic.LastOpeningVerdict);
                Tick(3);

                // ---- 6. THE RED ARROW ON THE TRAINING GROUND (the walk-about mode): up while you fight a man off balance, never in a conversation
                var view = new OpeningArrowView();
                var layer = new FakeHudLayer();
                view.UseLayer(layer);
                view.Tick(Frame(t, you.Agent, MissionMode.StartUp));
                Check(!layer.Up, "the arrow layer is up in the walk-about mode with no fight");
                guards[ai.Agent.Index] = OpeningMath.GuardLeft;
                Parried(you, ai, OpeningMath.AttackRight);
                double peak = logic.ArrowFlashPeak;
                view.Tick(Frame(t, you.Agent, MissionMode.Conversation));
                Check(!layer.Up, "the arrow layer came up in a conversation");
                view.Tick(Frame(t, you.Agent, MissionMode.StartUp));
                var vm = view.CurrentViewModel;
                Check(layer.Up && vm != null, "the arrow layer is not up on the training ground while he is off balance");
                view.Tick(Frame(peak, you.Agent, MissionMode.StartUp));
                Check(vm!.TopShown || vm.BottomShown || vm.LeftShown || vm.RightShown, "the flash is not shown on the training ground");
                view.Tick(Frame(peak, null, MissionMode.StartUp));
                Check(!layer.Up, "the arrow stayed up with no player on the field");
                Tick(4);
                view.Tick(Frame(t, you.Agent, MissionMode.StartUp));
                Check(!layer.Up, "the arrow layer outlived the fight on the training ground");

                // ---- 7. no errors; the [summary] lines
                Check(fs.Errors == 0 && st.Errors == 0, "errors: " + fs.Errors + " / " + st.Errors);
                string fline = fs.SummaryLine(OpeningRules.From(S), false);
                string oline = st.SummaryLine(OpeningRules.From(S));
                Check(fline.Contains("THE LIVE POSE read back (step 106f") && fline.Contains("BLOCKED ANYWAY by cause your swing went another way at contact 1")
                      && fline.Contains("(held follow-ups 1)"), "the forced guard's [summary] line: " + fline);
                Check(oline.Contains("your follow-up's direction judged (step 106f) at the release 2"), "the openings' [summary] line: " + oline);
                Console.WriteLine("        step 106f: poses " + string.Join("/", fs.PoseWeapon) + ", blocked anyway " + fs.BlockedAnyway + ", judged at the release "
                                  + st.JudgedAt[(int)OpeningJudgedAt.Release] + ", wind-up first read differed " + st.WindUpDiffer);
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

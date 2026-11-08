using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;
using TraxCombat.Models;

namespace TraxCombat.Tools
{
    /// <summary>
    /// Offline check for step 100d - THE TIMED RIPOSTE'S TWO ANIMATIONS (AI_NOTES "Step 100" §4, DESIGN decision 91 (d)): through the REAL logic
    /// with a stand-in of the animation calls (<see cref="IAnimBody"/>): your timed block's riposte starts with the attacker's direction-matched
    /// recoil and the backward stagger follows for the rest (the whole kept); you push your shield out; your bash collision inside the window is
    /// no blow (the decorator's guard, OnMeleeHit, the kick-and-bash start); the window's end clears a push still playing; an AI parry with a
    /// missing bash falls back to his guard's heavy parry; the fallbacks to the single stagger; the switches and the master switch; the test
    /// tool; the [summary] lines.
    /// </summary>
    internal static partial class Program
    {
        private sealed class FakeAnimBody : IAnimBody
        {
            public readonly Dictionary<int, string> Usage = new Dictionary<int, string>();
            public readonly HashSet<int> Shields = new HashSet<int>();
            public readonly HashSet<int> Left = new HashSet<int>();
            public readonly HashSet<int> Riding = new HashSet<int>();
            public readonly HashSet<int> Guard = new HashSet<int>();
            public readonly Dictionary<int, int> Upper = new Dictionary<int, int>();
            public readonly Dictionary<int, string> UpperName = new Dictionary<int, string>();
            public readonly Dictionary<string, (int Index, double Seconds)> Known = new Dictionary<string, (int, double)>();
            public readonly List<(int Man, int Channel, int Index, float Speed, float Start)> Plays = new List<(int, int, int, float, float)>();
            public readonly List<(int Man, float Speed)> Speeds = new List<(int, float)>();
            public readonly List<int> Clears = new List<int>();
            public readonly List<string> Said = new List<string>();
            public bool Refuse;
            public int Presses;
            public bool Shift;
            public Agent? Near;

            public string UsageOf(TrackedAgent st) => Usage.TryGetValue(st.AgentIndex, out var u) ? u : string.Empty;
            public bool ShieldInHand(TrackedAgent st) => Shields.Contains(st.AgentIndex);
            public bool LeftStance(TrackedAgent st) => Left.Contains(st.AgentIndex);
            public bool Mounted(TrackedAgent st) => Riding.Contains(st.AgentIndex);
            public bool Guarding(TrackedAgent st) => Guard.Contains(st.AgentIndex);
            public int CurrentIndex(TrackedAgent st, int channel) => channel == 1 && Upper.TryGetValue(st.AgentIndex, out int i) ? i : -1;
            public string? CurrentName(TrackedAgent st, int channel) => channel == 1 && UpperName.TryGetValue(st.AgentIndex, out var n) ? n : null;

            public bool Find(TrackedAgent st, string name, out int index, out double seconds)
            {
                index = -1;
                seconds = double.NaN;
                if (!Known.TryGetValue(name, out var k)) return false;
                index = k.Index;
                seconds = k.Seconds;
                return true;
            }

            public bool Play(TrackedAgent st, int channel, int index, float speed, float startProgress, float blendIn, float blendOut)
            {
                if (Refuse) return false;
                Plays.Add((st.AgentIndex, channel, index, speed, startProgress));
                if (channel == 1) Upper[st.AgentIndex] = index;
                return true;
            }

            public void SetSpeed(TrackedAgent st, int channel, float speed) => Speeds.Add((st.AgentIndex, speed));

            public bool ClearUpper(TrackedAgent st)
            {
                Clears.Add(st.AgentIndex);
                Upper.Remove(st.AgentIndex);
                return true;
            }

            public bool KeyPressed(string keyName, out bool shift)
            {
                shift = Shift;
                if (Presses <= 0) return false;
                Presses--;
                return true;
            }

            public Agent? Nearest(Agent you, float metres) => Near;
            public void Say(string text) => Said.Add(text);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UseFakeAnimBody()
        {
            Check(AnimBodyDefault.Current is GameAnimBody, "the default animation body is not the game's: " + AnimBodyDefault.Current.GetType().Name);
            AnimBodyDefault.Current = new FakeAnimBody(); // knows no action: every riposte keeps the single stagger, every push-out is skipped
            Check(new AthleticsLogic().AnimBody == AnimBodyDefault.Current, "a new logic does not start with the default animation body");
            Check((int)Agent.UsageDirection.AttackUp == TimedRecoilMath.Up && (int)Agent.UsageDirection.AttackDown == TimedRecoilMath.Down
                  && (int)Agent.UsageDirection.AttackLeft == TimedRecoilMath.Left && (int)Agent.UsageDirection.AttackRight == TimedRecoilMath.Right,
                "the engine's attack directions are not the Core's");
        }

        private static AttackCollisionData Swing(Agent.UsageDirection dir, bool shieldBlock, bool correctSide, CombatCollisionResult result) =>
            AttackCollisionData.GetAttackCollisionDataForDebugPurpose(shieldBlock, correctSide, false, true, false, false, false, false, false, false, false, false,
                result, 0, 0, 0, 0, default(BoneBodyPartType), 0, dir, 0, default(CombatHitResultFlags), 0.5f, 0.5f, 0f, 0f, 0f, 0f, 0f, 0f,
                Vec3.Zero, Vec3.Zero, Vec3.Zero, Vec3.Zero, Vec3.Zero, Vec3.Zero, Vec3.Zero);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TimedAnimsThroughTheLogic()
        {
            var keep = _logic;
            float capKeep = StunCap;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                BlocksStunDefaults();
                Step56BlockModel();
                foreach (var p in SettingsSchema.All.Where(p => p.Key.StartsWith("TimedBlock", StringComparison.Ordinal)))
                    S.Set(p, p.Default, SettingSources.File);
                S.Set(SettingsSchema.TimedBlockSlowMo, false, SettingSources.File);
                Check(SettingsSchema.TimedBlockPushOut.Default == 1 && SettingsSchema.TimedBlockRecoil.Default == 1 && SettingsSchema.TimedBlockAnimTest.Default == 0
                      && TraxSettings.Shared.TimedBlockPushOutShieldAction == "act_shield_bash" && TraxSettings.Shared.TimedBlockPushOutTwoHandedAction == "act_2h_bash"
                      && TraxSettings.Shared.TimedBlockAnimTestKey == "J",
                    "the embedded push-out / recoil defaults are not the design's");
                S.Set(SettingsSchema.RiposteStunMaxSeconds, 1.0, SettingSources.File);
                var inputs = new FakeImpactInputs();
                var impact = new FakeImpactBody();
                var guard = new FakeGuardBody();
                var anim = new FakeAnimBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = impact;
                logic.ImpactInputs = inputs;
                logic.GuardBody = guard;
                logic.SlowMoBody = new FakeSlowMoBody();
                logic.AnimBody = anim;
                logic.ImpactDice = new ScriptDice { Rest = 0.5 };
                double t = 90000;
                logic.TickImpact(t);
                int next = 7700;
                anim.Known["act_blocked_slashleft_1h"] = (501, 0.6);
                anim.Known["act_blocked_overswing_1h"] = (502, 0.6);
                anim.Known["act_shield_bash"] = (601, 1.0);
                anim.Known["act_defend_up_2h_parry_heavy"] = (701, 0.5);

                TrackedAgent Man(bool shield, string usage)
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
                    anim.Usage[i] = usage;
                    return m;
                }
                void Tick(double seconds = 1.0 / 60) => TickFor(logic, ref t, seconds);
                var you = Man(true, "onehanded_shield_swing");
                logic.SmokePlayer = you.Agent;
                logic.SmokePlayerAiControlled = false;
                guard.Contacts[you.Agent.Index] = (GuardRead.Active, double.NaN);
                anim.Guard.Add(you.AgentIndex);
                var st = logic.TimedAnimStats;

                TrackedAgent YourBlock(double age, Agent.UsageDirection dir, string usage = "onehanded_shield_swing")
                {
                    guard.Keys[you.Agent.Index] = 0;
                    Tick(0.1);
                    guard.Keys[you.Agent.Index] = 0x800;
                    Tick(age);
                    var a = Man(false, usage);
                    logic.ImpactModelHitTaken(a.Agent, you.Agent, Swing(dir, true, true, CombatCollisionResult.Blocked));
                    Tick();
                    return a;
                }

                // ---- 1. your perfect block of a left swing: the recoil first, your shield out, then the backward stagger for the rest
                int staggersBefore = impact.Staggers.Count;
                var a1 = YourBlock(0.05, Agent.UsageDirection.AttackLeft);
                var res = logic.LastTimedBlock;
                Check(res.Timed && res.RiposteHappens, "the perfect block did not riposte (timed " + res.Timed + ")");
                Check(st.RecoilPlayed[TimedRecoilMath.Left] == 1 && anim.Plays.Any(p => p.Man == a1.AgentIndex && p.Channel == 1 && p.Index == 501 && Math.Abs(p.Speed - 0.6f / 0.45f) < 1e-3),
                    "the left swing's recoil act_blocked_slashleft_1h was not played on his upper body at x1.33: " + string.Join(", ", anim.Plays));
                Check(impact.Staggers.Count == staggersBefore, "the backward stagger came WITH the recoil, not after it");
                Check(anim.Plays.Any(p => p.Man == you.AgentIndex && p.Channel == 1 && p.Index == 601 && Math.Abs(p.Speed - 2.8f) < 1e-3 && Math.Abs(p.Start - 0.3f) < 1e-6)
                      && st.PushPlayed[(int)PushOutKind.Shield] == 1 && st.PushYou == 1 && logic.PushOutsRunning == 1,
                    "your shield did not push out (act_shield_bash x2.8 from 0.3): played " + st.PushTotal);
                Check(logic.TimedBlockStats.RiposteApplied.Sum() >= 1 && logic.RecoilChainsRunning == 1, "the recoil's riposte was not counted applied / its stagger not queued");
                // your bash collision inside the window: no blow, no hit of ours, no kick cost
                Check(ReferenceEquals(ImpactHooks.PushOut, logic), "the logic is not the decorator's push-out guard");
                Check(ImpactHooks.PushOutBlow(you.Agent, BashLands(), count: true) && st.BashBlowsIgnored == 1, "your push-out's bash collision is a blow");
                Check(!ImpactHooks.PushOutBlow(you.Agent, RightBlock()), "a non-bash collision of yours was guarded");
                Check(!ImpactHooks.PushOutBlow(a1.Agent, BashLands()), "another man's bash was guarded");
                logic.OnMeleeHit(you.Agent, a1.Agent, false, BashLands());
                Check(st.BashHitsSkipped == 1, "OnMeleeHit saw your push-out's bash");
                float barBefore = (float)you.Fraction;
                logic.ObserveLowerAction(you, (int)Agent.ActionCodeType.WeaponBash, t, AthleticsRules.From(S));
                Check(st.BashStartsSkipped == 1 && Math.Abs(you.Fraction - barBefore) < 1e-9, "your push-out's bash start cost Athletics: skipped " + st.BashStartsSkipped);
                // the recoil's time passes: the backward stagger for the rest
                Tick(0.5);
                Check(impact.Staggers.Count == staggersBefore + 1 && st.RecoilStaggerPlayed == 1 && logic.RecoilChainsRunning == 0,
                    "the backward stagger did not follow the recoil: staggers " + (impact.Staggers.Count - staggersBefore));
                double asked = res.Seconds;
                Check(Math.Abs(st.RecoilSecondsSum + st.RecoilStaggerSecondsSum - asked) < 1e-6,
                    "the recoil + the stagger (" + st.RecoilSecondsSum + " + " + st.RecoilStaggerSecondsSum + ") is not the asked " + asked);
                double natural = impact.StaggerSpeeds.Count > 0 ? impact.StaggerSpeeds[impact.StaggerSpeeds.Count - 1].Speed * (asked - 0.45) : double.NaN; // speed = natural ÷ the rest
                Check(natural > 0.8 && natural < 1.2,
                    "the backward stagger's speed is not the rest's (natural ÷ " + F2s(asked - 0.45) + " s): " + impact.StaggerSpeeds.LastOrDefault().Speed + " → natural " + natural);
                // the push-out window's end: our push still on your upper body = cleared
                Check(logic.PushOutsRunning == 0 && anim.Clears.Contains(you.AgentIndex) && st.Unstuck == 1, "the push still playing at the window's end was not cleared");
                Check(!ImpactHooks.PushOutBlow(you.Agent, BashLands()), "your bash is still guarded after the window");

                // ---- 2. the engine's own recoil already playing = slowed in place; the push ended on its own = the guard back
                Tick(3);
                anim.Upper[next] = 502; // the next man's channel 1 shows the overswing's recoil
                var a2 = YourBlock(0.05, Agent.UsageDirection.AttackUp);
                Check(st.RecoilSlowedInPlace == 1 && anim.Speeds.Any(s => s.Man == a2.AgentIndex), "the engine's own recoil was not slowed in place");
                anim.Upper[you.AgentIndex] = 5; // your guard is back on your upper body
                Tick(1);
                Check(st.GuardsBack >= 1 && st.Unstuck == 1, "a push that ended on its own was cleared");

                // ---- 3. the fallbacks to the single stagger: a couched lance (no recoil class), an action his set lacks, no direction
                Tick(3);
                int fallBefore = st.RecoilFell.Sum();
                staggersBefore = impact.Staggers.Count;
                YourBlock(0.05, Agent.UsageDirection.AttackLeft, "polearm_couch");
                Check(st.RecoilFell[(int)RecoilFallback.NoClass] == 1 && impact.Staggers.Count == staggersBefore + 1, "a couched lance's riposte did not fall back to the single stagger");
                Tick(3);
                YourBlock(0.05, Agent.UsageDirection.AttackRight); // act_blocked_slashright_1h unknown
                Check(st.RecoilFell[(int)RecoilFallback.NoAction] == 1, "a recoil missing from his set did not fall back");
                Check(st.RecoilFell.Sum() == fallBefore + 2, "the fallbacks counted wrong: " + (st.RecoilFell.Sum() - fallBefore));

                // ---- 4. an AI's timed PARRY with a two-hander: act_2h_bash missing → his guard's heavy parry
                Tick(3);
                S.Set(SettingsSchema.TimedBlockAiPercent, 100, SettingSources.File);
                var ai = Man(false, "twohanded_block_swing");
                guard.Contacts[ai.Agent.Index] = (GuardRead.Active, 0.0);
                anim.Guard.Add(ai.AgentIndex);
                anim.UpperName[ai.AgentIndex] = "act_defend_up_2h_parry_light";
                logic.ImpactModelHitTaken(Man(false, "onehanded_swing").Agent, ai.Agent, Swing(Agent.UsageDirection.AttackLeft, false, false, CombatCollisionResult.Parried));
                Tick();
                Check(st.PushPlayed[(int)PushOutKind.TwoHanded] == 1 && st.PushFellBackToParryHeavy == 1
                      && anim.Plays.Any(p => p.Man == ai.AgentIndex && p.Index == 701 && p.Start == 0f),
                    "the AI's two-handed parry did not push out with his guard's heavy parry: two-handed " + st.PushPlayed[(int)PushOutKind.TwoHanded]);
                S.Set(SettingsSchema.TimedBlockAiPercent, SettingsSchema.TimedBlockAiPercent.Default, SettingSources.File);

                // ---- 5. the push-out's gates: not on you (switch), not guarding; the switches; the master switch drops a recoil's stagger
                Tick(3);
                S.Set(SettingsSchema.TimedBlockPushOutForYou, false, SettingSources.File);
                YourBlock(0.05, Agent.UsageDirection.AttackLeft);
                Check(st.PushSkipped[(int)PushOutSkip.NotForYou] == 1, "the push-out played on you with its switch off");
                S.Set(SettingsSchema.TimedBlockPushOutForYou, true, SettingSources.File);
                Tick(3);
                anim.Guard.Remove(you.AgentIndex);
                YourBlock(0.05, Agent.UsageDirection.AttackLeft);
                Check(st.PushSkipped[(int)PushOutSkip.NotGuarding] == 1, "the push-out played on a man no longer guarding");
                anim.Guard.Add(you.AgentIndex);
                Tick(3);
                S.Set(SettingsSchema.TimedBlockRecoil, false, SettingSources.File);
                int recoilsBefore = st.RecoilTotal;
                YourBlock(0.05, Agent.UsageDirection.AttackLeft);
                Check(st.RecoilTotal == recoilsBefore && st.RecoilFell[(int)RecoilFallback.Off] == 1, "the recoil played with its switch off");
                S.Set(SettingsSchema.TimedBlockRecoil, true, SettingSources.File);
                Tick(3);
                staggersBefore = impact.Staggers.Count;
                YourBlock(0.05, Agent.UsageDirection.AttackLeft);
                Check(logic.RecoilChainsRunning == 1, "no recoil running before the master switch test");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.File);
                Tick(0.6);
                Check(logic.RecoilChainsRunning == 0 && impact.Staggers.Count == staggersBefore, "the MASTER SWITCH off still played the backward stagger after the recoil");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                Tick(3);
                int pushesBefore = st.PushTotal;
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.File);
                Check(!TimedAnimRules.From(S).PushEnabled && !TimedAnimRules.From(S).RecoilEnabled && !TimedAnimRules.From(S).TestOn, "the master switch does not turn the animations off");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                Check(st.PushTotal == pushesBefore, "a push counted while off");

                // ---- 6. the test tool: off = nothing; on = the candidates on the nearest man, Left Shift goes back
                var subject = Man(true, "onehanded_shield_swing");
                anim.Near = subject.Agent;
                anim.Presses = 1;
                Tick();
                Check(anim.Said.Count == 0, "the test tool played while off");
                S.Set(SettingsSchema.TimedBlockAnimTest, true, SettingSources.File);
                anim.Presses = 1;
                Tick();
                Check(anim.Said.Count == 1 && anim.Said[0].StartsWith("[anim test 1/26]", StringComparison.Ordinal) && anim.Said[0].Contains("act_shield_bash")
                      && anim.Plays.Last().Man == subject.AgentIndex && anim.Plays.Last().Index == 601,
                    "the test tool's first candidate: " + string.Join(" | ", anim.Said));
                anim.Shift = true;
                anim.Presses = 1;
                Tick();
                Check(anim.Said.Last().StartsWith("[anim test 26/26]", StringComparison.Ordinal) && anim.Said.Last().Contains("timed riposte, right swing"), "Left Shift did not go back to the last candidate: " + anim.Said.Last());
                anim.Shift = false;
                for (int k = 0; k < 8; k++)
                {
                    anim.Presses = 1;
                    Tick();
                }
                Check(anim.Said.Last().StartsWith("[anim test 8/26]", StringComparison.Ordinal) && anim.Said.Last().Contains("act_quick_blocked_overswing_1h") && anim.Said.Last().Contains("not in his action set"),
                    "the recoil candidates: " + anim.Said.Last());
                for (int k = 0; k < 15; k++)
                {
                    anim.Presses = 1;
                    Tick();
                }
                Check(anim.Said.Last().StartsWith("[anim test 23/26]", StringComparison.Ordinal) && anim.Said.Last().Contains("timed riposte, overhead") && anim.Said.Last().Contains("then the backward stagger"),
                    "the full timed riposte candidate: " + anim.Said.Last());
                Tick(3);
                S.Set(SettingsSchema.TimedBlockAnimTest, false, SettingSources.File);
                anim.Near = null;

                // ---- 7. the [summary] lines
                var lines = st.SummaryLines(TimedAnimRules.From(S));
                Check(lines[0].StartsWith("timed push-out (step 100d): on - played ", StringComparison.Ordinal) && lines[0].Contains("ignored 1 (the engine's blow)")
                      && lines[1].StartsWith("timed recoils (step 100d): on, the blocked recoil - played ", StringComparison.Ordinal) && lines[1].Contains("not in his action set 1"),
                    "the [summary] lines: " + string.Join(" || ", lines));
                Console.WriteLine("        step 100d: " + st.RecoilTotal + " recoils, " + st.PushTotal + " push-outs, " + st.TestPlays + " test plays");
                Console.WriteLine("        " + lines[0]);
                Console.WriteLine("        " + lines[1]);
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
                ManagedParameters.SetParameter(ManagedParametersEnum.StunPeriodMax, capKeep);
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                foreach (var p in SettingsSchema.All.Where(p => p.Key.StartsWith("TimedBlock", StringComparison.Ordinal)))
                    S.Set(p, p.Default, SettingSources.File);
                Step56BlockModel();
            }
        }
    }
}

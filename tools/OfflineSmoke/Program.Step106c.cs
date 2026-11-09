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
    /// Offline check for step 106c - THE RED ARROW (AI_NOTES "106c - built", DESIGN decision 95 (k)): its prefab against the game, and the REAL
    /// <see cref="OpeningArrowView"/> + <see cref="OpeningArrowVM"/> over the REAL logic: a parry of your swing with the openings OFF flashes
    /// nothing; ON it flashes the BOTTOM arrow (his balance went high - your thrust), full at the flash, half on the way in / out, gone after; the
    /// K test tool's low guard flashes the TOP arrow with the openings off; your perfect block of his RIGHT swing shows the RIGHT arrow steady
    /// while he reels and it goes with the window; an axe after his blocked overhead lights LEFT + RIGHT; the switch takes the layer down; the
    /// [summary] line.
    /// </summary>
    internal static partial class Program
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void OpeningArrowPrefabIsValid() => PrefabIsValid(OpeningArrowView.Movie, typeof(OpeningArrowVM), 5, 40);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void OpeningArrowThroughTheView()
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
                Check(SettingsSchema.OpeningArrowShow.Default == 1 && SettingsSchema.OpeningsEnabled.Default == 1, "the embedded arrow defaults are not ON / openings ON (step 106d)");
                S.Set(SettingsSchema.OpeningsEnabled, false, SettingSources.File); // the step starts with the openings OFF (the arrow's own gates)
                var inputs = new FakeImpactInputs { Damage = 20 };
                var anim = new FakeAnimBody();
                var body = new FakeOpeningBody();
                var guard = new FakeGuardBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = new FakeImpactBody();
                logic.ImpactInputs = inputs;
                logic.GuardBody = guard;
                logic.SlowMoBody = new FakeSlowMoBody();
                logic.MessageSink = new FakeMessageSink();
                logic.AnimBody = anim;
                logic.OpeningBody = body;
                logic.ImpactDice = new ScriptDice { Rest = 0.5 };
                logic.OpeningDice = new ScriptDice { Rest = 0.0 };
                var guards = new Dictionary<int, int>();
                int weapon = OpeningMath.AllAttacks;
                logic.OpeningGuardRead = a => guards.TryGetValue(a.Index, out int g) ? g : -1;
                logic.OpeningSwingRead = a => OpeningMath.Unknown;
                logic.OpeningWeaponRead = a => weapon;
                double t = 106300;
                logic.TickImpact(t);
                int next = 10630;
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
                void Tick(double seconds = 1.0 / 60) => TickFor(logic, ref t, seconds);
                var you = Man(false);
                body.You.Add(you.AgentIndex);
                logic.SmokePlayer = you.Agent;
                logic.SmokePlayerAiControlled = false;
                guard.Contacts[you.Agent.Index] = (GuardRead.Active, double.NaN);
                var ai = Man(false);

                var view = new OpeningArrowView();
                var layer = new FakeHudLayer();
                view.UseLayer(layer);
                view.Tick(Frame(t, you.Agent));
                var vm = view.CurrentViewModel;
                Check(layer.Up && vm != null && !vm.TopShown && !vm.BottomShown && !vm.LeftShown && !vm.RightShown, "the arrow layer is not up and quiet in battle");
                LogHas("[hud] opening arrow: layer up at ");
                string Lit(OpeningArrowVM m) => (m.TopShown ? "T" + m.TopAlpha : "") + (m.BottomShown ? "B" + m.BottomAlpha : "") + (m.LeftShown ? "L" + m.LeftAlpha : "") + (m.RightShown ? "R" + m.RightAlpha : "");

                // ---- 1. the openings OFF (the shadow runs): his parry plans a flash, nothing is shown
                guards[ai.Agent.Index] = OpeningMath.GuardLeft;
                logic.ImpactModelHitTaken(you.Agent, ai.Agent, Swing(Agent.UsageDirection.AttackRight, false, false, CombatCollisionResult.Parried));
                double peak = logic.ArrowFlashPeak;
                Check(logic.ArrowFlashAttack == OpeningMath.AttackDown && Math.Abs(peak - (t + 0.3)) < 0.02, "the parry noted no thrust flash 0.3 s on: " + logic.ArrowFlashAttack + " at " + (peak - t));
                view.Tick(Frame(peak, you.Agent));
                Check(Lit(vm!) == "", "the flash showed with the openings OFF: " + Lit(vm!));
                Tick(3);

                // ---- 2. the openings ON: the BOTTOM arrow flashes, peaking at the flash
                S.Set(SettingsSchema.OpeningsEnabled, true, SettingSources.File);
                logic.ImpactModelHitTaken(you.Agent, ai.Agent, Swing(Agent.UsageDirection.AttackRight, false, false, CombatCollisionResult.Parried));
                peak = logic.ArrowFlashPeak;
                view.Tick(Frame(peak - 0.1, you.Agent));
                Check(Lit(vm) == "", "lit before the fade in: " + Lit(vm));
                view.Tick(Frame(peak - 0.025, you.Agent));
                Check(vm.BottomShown && Math.Abs(vm.BottomAlpha - 0.5f) < 0.02f && !vm.TopShown, "half way in: " + Lit(vm));
                view.Tick(Frame(peak, you.Agent));
                Check(vm.BottomShown && Math.Abs(vm.BottomAlpha - 1f) < 1e-4f, "not full at the flash: " + Lit(vm));
                view.Tick(Frame(peak + 0.075, you.Agent));
                Check(vm.BottomShown && Math.Abs(vm.BottomAlpha - 0.5f) < 0.02f, "half way out: " + Lit(vm));
                view.Tick(Frame(peak + 0.2, you.Agent));
                Check(Lit(vm) == "" && view.FlashesShown == 1, "the flash did not end / count once: " + Lit(vm) + ", " + view.FlashesShown);
                LogHas("[hud] opening arrow: first FLASH at ");
                S.Set(SettingsSchema.OpeningsEnabled, false, SettingSources.File);
                Tick(3);

                // ---- 3. the K test tool (the openings off): the low guard flashes the TOP arrow
                S.Set(SettingsSchema.OpeningGuardTest, true, SettingSources.File);
                body.Near = ai.Agent;
                anim.Presses = 1;
                Tick();
                peak = logic.ArrowFlashPeak;
                Check(logic.OpeningForceKind == OpeningForceKind.Test && logic.ArrowFlashAttack == OpeningMath.AttackUp, "the test key's low guard noted no overhead flash: " + logic.ArrowFlashAttack);
                view.Tick(Frame(peak, you.Agent));
                Check(vm.TopShown && Math.Abs(vm.TopAlpha - 1f) < 1e-4f && !vm.BottomShown, "the test tool's flash is not the top arrow: " + Lit(vm));
                LogHas("; the red arrow flashes top at 0.30 s");
                view.Tick(Frame(peak + 0.3, you.Agent));
                S.Set(SettingsSchema.OpeningGuardTest, false, SettingSources.File);
                body.Near = null;
                Tick(3);

                // ---- 4. the DEFENSIVE game: your perfect block of his RIGHT swing - the RIGHT arrow steady while he reels, gone with the window
                TrackedAgent YourBlock(Agent.UsageDirection dir)
                {
                    guard.Keys[you.Agent.Index] = 0;
                    Tick(0.1);
                    guard.Keys[you.Agent.Index] = 0x800;
                    Tick(0.05);
                    var a = Man(false);
                    logic.ImpactModelHitTaken(a.Agent, you.Agent, Swing(dir, true, true, CombatCollisionResult.Blocked));
                    Tick();
                    return a;
                }
                var a1 = YourBlock(Agent.UsageDirection.AttackRight);
                Check(logic.PerfectStrikeWindowsOpen == 1, "your perfect block opened no window");
                view.Tick(Frame(t, you.Agent));
                Check(vm.RightShown && vm.RightAlpha == 1f && !vm.LeftShown && !vm.TopShown && !vm.BottomShown && view.FinishesShown == 1,
                    "his blocked right swing does not show the RIGHT arrow steady: " + Lit(vm));
                LogHas("[hud] opening arrow: first PERFECT STRIKE side at ");
                Tick(0.4);
                view.Tick(Frame(t, you.Agent));
                Check(vm.RightShown && view.FinishesShown == 1, "the steady arrow did not stay (or counted again) while he reels: " + Lit(vm));
                Tick(4);
                view.Tick(Frame(t, you.Agent));
                Check(Lit(vm) == "", "the arrow outlived the window: " + Lit(vm) + " (open " + logic.PerfectStrikeWindowsOpen + ")");

                // ---- 5. an AXE (no thrust) after his blocked OVERHEAD: left + right
                weapon = OpeningMath.WeaponMask(true, false);
                YourBlock(Agent.UsageDirection.AttackUp);
                view.Tick(Frame(t, you.Agent));
                Check(vm.LeftShown && vm.RightShown && !vm.BottomShown && !vm.TopShown, "an axe after his blocked overhead does not light left + right: " + Lit(vm));
                weapon = OpeningMath.AllAttacks;
                view.Tick(Frame(t, you.Agent));
                Check(vm.BottomShown && !vm.LeftShown && !vm.RightShown, "a sword after his blocked overhead does not light the bottom (your thrust): " + Lit(vm));

                // ---- 6. the switch takes the layer down; the summary line
                S.Set(SettingsSchema.OpeningArrowShow, false, SettingSources.Mcm);
                view.Tick(Frame(t, you.Agent));
                Check(!layer.Up, "OpeningArrowShow off kept the layer");
                S.Set(SettingsSchema.OpeningArrowShow, true, SettingSources.Mcm);
                var lines = new List<string>();
                view.AddSummaryLines(lines);
                Check(lines.Any(l => l.StartsWith("hud: opening arrow (step 106c) - flashes 2", StringComparison.Ordinal)), "the summary line: " + string.Join(" | ", lines));
                Console.WriteLine("        " + lines.Last());
                Tick(4);
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

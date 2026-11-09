using System;
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
    /// Offline check for step 100g - THE PERFECT STRIKE (AI_NOTES "Step 100" - "100g - built", DESIGN decision 91 (j)): through the REAL logic
    /// and the decorator's hook - your perfect block of his RIGHT swing opens a window; your LEFT swing (the wrong side) lands normal damage and
    /// keeps it open; your RIGHT swing (from your right - Toni's example) lands x3 and uses it up (a gold line + the middle of the screen); a
    /// second right swing in the same stagger is normal; a blocked overhead / thrust; a hit into his shield / a kick does not use it; the window
    /// closes when he stops reeling (+ the grace); the AI's timed block opens none; the switch and the master switch; the [summary] line.
    /// </summary>
    internal static partial class Program
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PerfectStrikeThroughTheLogic()
        {
            var keep = _logic;
            float capKeep = StunCap;
            try
            {
                AthleticsDefaults();
                ImpactModelDefaults();
                BlocksStunDefaults();
                Step56BlockModel();
                foreach (var p in SettingsSchema.All.Where(p => p.Key.StartsWith("TimedBlock", StringComparison.Ordinal) || p.Key.StartsWith("PerfectStrike", StringComparison.Ordinal)))
                    S.Set(p, p.Default, SettingSources.File);
                S.Set(SettingsSchema.TimedBlockSlowMo, false, SettingSources.File);
                Check(SettingsSchema.PerfectStrikeEnabled.Default == 1 && Near((float)SettingsSchema.PerfectStrikeMinMultiplier.Default, 2f)
                      && Near((float)SettingsSchema.PerfectStrikeMaxMultiplier.Default, 3f) && Near((float)SettingsSchema.PerfectStrikeGraceSeconds.Default, 0.15f)
                      && SettingsSchema.PerfectStrikeCentreMessage.Default == 1,
                    "the embedded perfect-strike defaults are not the design's");
                S.Set(SettingsSchema.RiposteStunMaxSeconds, 1.0, SettingSources.File);
                var inputs = new FakeImpactInputs();
                var guard = new FakeGuardBody();
                var sink = new FakeMessageSink();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = new FakeImpactBody();
                logic.ImpactInputs = inputs;
                logic.GuardBody = guard;
                logic.SlowMoBody = new FakeSlowMoBody();
                logic.MessageSink = sink;
                logic.ImpactDice = new ScriptDice { Rest = 0.5 };
                double t = 95000;
                logic.TickImpact(t);
                Check(ReferenceEquals(ImpactHooks.Perfect, logic), "the logic is not the decorator's perfect-strike hook");
                int next = 8800;

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
                var you = Man(true);
                logic.SmokePlayer = you.Agent;
                logic.SmokePlayerAiControlled = false;
                guard.Contacts[you.Agent.Index] = (GuardRead.Active, double.NaN);
                var st = logic.PerfectStrikeStats;

                // your guard pressed `age` s before his blow (his own direction `dir`), the riposte from the next tick
                TrackedAgent YourBlock(double age, Agent.UsageDirection dir)
                {
                    guard.Keys[you.Agent.Index] = 0;
                    Tick(0.1);
                    guard.Keys[you.Agent.Index] = 0x800;
                    Tick(age);
                    var a = Man(false);
                    logic.ImpactModelHitTaken(a.Agent, you.Agent, Swing(dir, true, true, CombatCollisionResult.Blocked));
                    Tick();
                    return a;
                }
                float Hit(TrackedAgent on, Agent.UsageDirection dir, float damage = 20f, bool mounted = false) =>
                    ImpactHooks.PerfectStrike(you.Agent, on.Agent, mounted, Swing(dir, false, false, CombatCollisionResult.StrikeAgent), damage);

                // ---- 1. Toni's example: his RIGHT swing blocked (it came at your left) - your LEFT swing is the wrong side, your RIGHT swing the perfect strike
                var a1 = YourBlock(0.05, Agent.UsageDirection.AttackRight);
                var res = logic.LastTimedBlock;
                Check(res.Timed && res.RiposteHappens && Math.Abs(res.Score - 1) < 1e-9, "the perfect block did not riposte (timed " + res.Timed + ", score " + res.Score + ")");
                Check(st.Opened == 1 && logic.PerfectStrikeWindowsOpen == 1, "your perfect block's riposte opened no window: opened " + st.Opened);
                LogHas("[impact] perfect strike: first window at ");
                LogHas("of his right swing (it came at your left - answer with YOUR right swing, from your right)");
                Tick(0.3);
                Check(Near(Hit(a1, Agent.UsageDirection.AttackLeft), 20f) && st.WrongSideTotal == 1 && logic.PerfectStrikeWindowsOpen == 1,
                    "your LEFT swing after his blocked right swing was not a wrong-side hit that keeps the window: wrong " + st.WrongSideTotal + ", open " + logic.PerfectStrikeWindowsOpen);
                LogHas("[impact] perfect strike: first WRONG-SIDE hit at ");
                Check(Near(Hit(a1, Agent.UsageDirection.AttackRight), 60f) && st.LandedTotal == 1 && st.Landed[PerfectStrikeMath.Right] == 1
                      && logic.PerfectStrikeWindowsOpen == 0,
                    "your RIGHT swing (from your right to left) after his blocked right swing did not land x3: landed " + st.LandedTotal);
                LogHas("[impact] YOU: PERFECT STRIKE on ");
                LogHas("your right swing after his blocked right swing");
                LogHas("timing 1.00 → x3.00: 20 → 60 damage (after 1 wrong-side hit(s))");
                Check(sink.Lines.Count(l => l.Text == "PERFECT STRIKE x3.0") == 0, "the line was shown inside the hit callback, not from the tick");
                Tick();
                Check(sink.Lines.Any(l => l.Text == "PERFECT STRIKE x3.0" && l.Hex == PerfectStrikeMath.GoldHex) && sink.Centred.Contains("PERFECT STRIKE x3.0"),
                    "the gold PERFECT STRIKE x3.0 line / the centre line was not shown: " + string.Join(" | ", sink.Lines.Select(l => l.Text)));
                Check(Near(Hit(a1, Agent.UsageDirection.AttackRight), 20f) && st.LandedTotal == 1, "a second strike in the same stagger was perfect too");

                // ---- 2. a blocked LEFT swing: your left swing (from your left) is the perfect one; the multiplier follows the timing (a later guard)
                Tick(3);
                var a2 = YourBlock(0.30, Agent.UsageDirection.AttackLeft);
                double score2 = logic.LastTimedBlock.Score;
                Check(score2 > 0 && score2 < 1 && st.Opened == 2, "the later block did not open a window with a partial timing: " + score2);
                // a hit into his shield / a kick does not use it
                Check(Near(ImpactHooks.PerfectStrike(you.Agent, a2.Agent, false, Swing(Agent.UsageDirection.AttackLeft, true, true, CombatCollisionResult.Blocked), 5f), 5f)
                      && Near(ImpactHooks.PerfectStrike(you.Agent, a2.Agent, false, KickLands(), 5f), 5f) && st.NotOnBody == 2 && logic.PerfectStrikeWindowsOpen == 1,
                    "a blocked swing / a kick used the window: not on body " + st.NotOnBody);
                Check(Near(Hit(a2, Agent.UsageDirection.AttackLeft, 10f, mounted: true), 10f) && st.Mounted == 1, "a strike from horseback counted");
                float k2 = (float)PerfectStrikeMath.Multiplier(score2, 2, 3);
                Check(Math.Abs(Hit(a2, Agent.UsageDirection.AttackLeft, 10f) - 10f * k2) < 1e-3 && st.Landed[PerfectStrikeMath.Left] == 1,
                    "your left swing after his blocked left swing did not land x" + k2);

                // ---- 3. a blocked overhead (step 103, strict opposites): your side swings and your overhead are wrong sides, your thrust from below the perfect one
                Tick(3);
                var a3 = YourBlock(0.05, Agent.UsageDirection.AttackUp);
                Check(Near(Hit(a3, Agent.UsageDirection.AttackLeft), 20f) && Near(Hit(a3, Agent.UsageDirection.AttackRight), 20f) && Near(Hit(a3, Agent.UsageDirection.AttackUp), 20f)
                      && st.WrongSideAfter[PerfectStrikeMath.Up * 4 + PerfectStrikeMath.Left] == 1 && st.WrongSideAfter[PerfectStrikeMath.Up * 4 + PerfectStrikeMath.Right] == 1,
                    "your side swing / overhead after his blocked overhead counted (step 103: only your thrust from below)");
                LogHas("[impact] perfect strike: WRONG-SIDE hit at ");
                LogHas("after his blocked overhead (it came from above - answer from BELOW: YOUR thrust / stab)");
                Check(Near(Hit(a3, Agent.UsageDirection.AttackDown), 60f) && st.Landed[PerfectStrikeMath.Down] == 1, "your thrust from below after his blocked overhead was not perfect (Toni's case)");
                LogHas("your thrust after his blocked overhead");

                // ---- 4. a blocked thrust: only your overhead counts
                Tick(3);
                var a4 = YourBlock(0.05, Agent.UsageDirection.AttackDown);
                Check(Near(Hit(a4, Agent.UsageDirection.AttackLeft), 20f) && st.WrongSideAfter[PerfectStrikeMath.Down * 4 + PerfectStrikeMath.Left] == 1,
                    "your side swing after his blocked thrust counted (step 103: only your overhead)");
                Check(Near(Hit(a4, Agent.UsageDirection.AttackUp), 60f) && st.Landed[PerfectStrikeMath.Up] == 1, "your overhead after his blocked thrust was not perfect");

                // ---- 5. the window closes when he stops reeling (+ the grace) - expired unused
                Tick(3);
                int expiredBefore = st.ExpiredUnused;
                var a5 = YourBlock(0.05, Agent.UsageDirection.AttackRight);
                double until = a5.Impact.ReelingUntil;
                Check(until > t + 0.5, "he does not reel after the riposte: until " + until + " now " + t);
                // the stagger's watch may re-time his reel end (the measured one) - follow it tick by tick to 0.05 s past it
                for (int n = 0; n < 600 && t <= a5.Impact.ReelingUntil + 0.05; n++) Tick();
                Check(logic.PerfectStrikeWindowsOpen == 1, "the window closed inside the grace (his reel ended at " + a5.Impact.ReelingUntil + ", now " + t + ")");
                Tick(0.2);
                Check(logic.PerfectStrikeWindowsOpen == 0 && st.ExpiredUnused == expiredBefore + 1, "the window did not expire after his stagger + the grace: open " + logic.PerfectStrikeWindowsOpen);
                Check(Near(Hit(a5, Agent.UsageDirection.AttackRight), 20f), "a strike after the window was perfect");

                // ---- 6. the AI's timed block opens nothing; the switch and the master switch close windows and give normal damage
                Tick(3);
                S.Set(SettingsSchema.TimedBlockAiPercent, 100, SettingSources.File);
                var aiBlocker = Man(true);
                guard.Contacts[aiBlocker.Agent.Index] = (GuardRead.Active, 0.0);
                int openedBefore = st.Opened;
                logic.ImpactModelHitTaken(Man(false).Agent, aiBlocker.Agent, Swing(Agent.UsageDirection.AttackRight, true, true, CombatCollisionResult.Blocked));
                Tick();
                Check(st.Opened == openedBefore, "the AI's timed block opened a perfect-strike window");
                S.Set(SettingsSchema.TimedBlockAiPercent, SettingsSchema.TimedBlockAiPercent.Default, SettingSources.File);
                Tick(3);
                var a6 = YourBlock(0.05, Agent.UsageDirection.AttackRight);
                Check(logic.PerfectStrikeWindowsOpen == 1, "no window before the switch test");
                S.Set(SettingsSchema.PerfectStrikeEnabled, false, SettingSources.File);
                Check(Near(Hit(a6, Agent.UsageDirection.AttackRight), 20f), "the switch off still multiplied");
                Tick();
                Check(logic.PerfectStrikeWindowsOpen == 0 && st.ClosedBySwitch == 1, "the switch off did not close the window");
                S.Set(SettingsSchema.PerfectStrikeEnabled, true, SettingSources.File);
                Tick(3);
                int openedBeforeOff = st.Opened;
                var a7 = YourBlock(0.05, Agent.UsageDirection.AttackRight);
                Check(st.Opened == openedBeforeOff + 1, "no window before the master switch test");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.File);
                Check(!PerfectStrikeRules.From(S).Enabled && Near(Hit(a7, Agent.UsageDirection.AttackRight), 20f), "the MASTER SWITCH off still multiplied");
                Tick();
                Check(logic.PerfectStrikeWindowsOpen == 0 && st.ClosedBySwitch == 2, "the master switch off did not close the window");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);

                // ---- 7. the [summary] line
                string line = st.SummaryLine(PerfectStrikeRules.From(S));
                Check(line.StartsWith("perfect strike (step 100g; ON): windows opened by your timed blocks ", StringComparison.Ordinal)
                      && line.Contains("PERFECT STRIKES landed 4 (your left swing 1, right swing 1, overhead 1, thrust from below 1")
                      && line.Contains("windows expired unused 1") && line.Contains("wrong-side hits 5") && line.Contains("after his blocked overhead (wanted your thrust): your left 1 / right 1 / overhead 1 / thrust 0"),
                    "the [summary] line: " + line);
                Console.WriteLine("        step 100g: " + st.Opened + " windows, " + st.LandedTotal + " perfect strikes, " + st.WrongSideTotal + " wrong-side hits");
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
                foreach (var p in SettingsSchema.All.Where(p => p.Key.StartsWith("TimedBlock", StringComparison.Ordinal) || p.Key.StartsWith("PerfectStrike", StringComparison.Ordinal)))
                    S.Set(p, p.Default, SettingSources.File);
                Step56BlockModel();
            }
        }
    }
}

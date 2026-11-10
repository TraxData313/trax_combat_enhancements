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
    /// Offline check for step 107 - HOLD-BLOCK OPENINGS (AI_NOTES "Step 107", DESIGN decision 96), through the REAL logic, the damage decorator's
    /// perfect-strike hook and the REAL input frames with stand-ins of the engine: (1) a SWORD - your landed hit forces a hold (the arrow on the
    /// opening, his guard written, 1.2 s at speed 95); a third side lands as a normal hit = a fresh hold; EXACTLY the opening takes his 'can defend'
    /// at the commit and staggers him - the perfect strike x4 at a quick wind-up; (2) an AXE - any side but his guard wins, into his guard he
    /// parries = a link; (3) LINKS GROW the hold (+0.4, the chain-speed bonus, from his block), a block right after the hold ran out is a link
    /// too, the lengthened hold ends in a forced COUNTER-SWING (100 %): ready (his attack bit written), release (every attack / guard bit out),
    /// over; (4) the FIRST hold's counter-swing at 30 % (thrown / not by the dice); (5) a SHIELDMAN: his shield block opens a SHORT hold by his
    /// shield's size, your winning strike LOWERS his shield (GUARD BROKEN) and staggers him; (6) a hit from behind still reels him at once; (7) the
    /// exits - the switch, the master switch, removed, the mission end - release every force, flag and counter-swing; (8) the [summary] line.
    /// </summary>
    internal static partial class Program
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Openings107ThroughTheLogic()
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
                // step 108: step 107's clock from the hit (the recovery cap 0) and its 1.2 s first hold (your swing 1.2, no reading margin) - the step-108
                // smoke checks the recovery, the margin and the words
                S.Set(SettingsSchema.OpeningHoldRecoveryCapSeconds, 0, SettingSources.File);
                S.Set(SettingsSchema.OpeningHoldSwingSeconds, 1.2, SettingSources.File);
                S.Set(SettingsSchema.OpeningHoldReadMarginAtSkill0, 0, SettingSources.File);
                S.Set(SettingsSchema.OpeningHoldReadMarginAtSkill200, 0, SettingSources.File);
                Check(SettingsSchema.OpeningHoldsEnabled.Default == 1 && OpeningHoldRules.From(S).On, "the hold-block openings are not ON by default");
                Check((uint)Agent.MovementControlFlag.AttackLeft == OpeningHoldMath.AttackLeft && (uint)Agent.MovementControlFlag.AttackRight == OpeningHoldMath.AttackRight
                      && (uint)Agent.MovementControlFlag.AttackUp == OpeningHoldMath.AttackUp && (uint)Agent.MovementControlFlag.AttackDown == OpeningHoldMath.AttackDown,
                    "the attack bits differ from the game's MovementControlFlag");
                Check(((uint)Agent.MovementControlFlag.DefendBlock << 1) < AiInputState.CounterReleaseBit, "the counter-swing's phase bits collide with the engine's movement bits");
                var inputs = new FakeImpactInputs { Damage = 20 };
                var anim = new FakeAnimBody();
                var body = new FakeOpeningBody();
                var sink = new FakeMessageSink();
                var impactBody = new FakeImpactBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = impactBody;
                logic.ImpactInputs = inputs;
                logic.GuardBody = new FakeGuardBody();
                logic.SlowMoBody = new FakeSlowMoBody();
                logic.MessageSink = sink;
                logic.AnimBody = anim;
                logic.OpeningBody = body;
                logic.ImpactDice = new ScriptDice { Rest = 0.999 }; // no impact outcome rolls up
                var dice = new ScriptDice { Rest = 0.0 };           // the first candidate guard; every counter-swing roll thrown
                logic.OpeningDice = dice;
                var guards = new System.Collections.Generic.Dictionary<int, int>();
                var shields = new System.Collections.Generic.Dictionary<int, double>();
                int swingDir = OpeningMath.Unknown, weapon = OpeningMath.AllAttacks;
                double speed = 95;
                logic.OpeningGuardRead = a => guards.TryGetValue(a.Index, out int g) ? g : -1;
                logic.OpeningSwingRead = a => swingDir;
                logic.OpeningCommitRead = a => swingDir; // the swing committed at its wind-up
                logic.OpeningWeaponRead = a => weapon;
                logic.OpeningWeaponSpeedRead = a => speed;
                logic.OpeningShieldLengthRead = a => shields.TryGetValue(a.Index, out double l) ? l : double.NaN;
                double t = 107000;
                logic.TickImpact(t);
                int next = 10700;
                var hs = logic.OpeningHoldStats;
                var st = logic.OpeningStats;
                var fs = logic.OpeningForceStats;
                var ar = Rules;
                int sword = OpeningMath.AllAttacks, axe = OpeningMath.WeaponMask(true, false);

                TrackedAgent Man(double shieldLength = double.NaN)
                {
                    int i = next++;
                    var m = logic.Track(FakeAgent(i))!;
                    m.AthleticsSkill = 125;
                    SetBar(m, 1.0);
                    inputs.Weapons[i] = (1.2, 92, ImpactDamageType.Cut, 100);
                    bool shield = !double.IsNaN(shieldLength);
                    inputs.Guards[i] = (shield ? 4.0 : 1.2, 100);
                    if (shield)
                    {
                        inputs.Shields.Add(i);
                        anim.Shields.Add(i);
                        shields[i] = shieldLength;
                    }
                    return m;
                }
                void Tick(double seconds = 1.0 / 60) => TickFor(logic, ref t, seconds);
                double Now() => t - 1.0 / 60;
                void Parried(TrackedAgent a, TrackedAgent v, int d) => logic.ImpactModelHitTaken(a.Agent, v.Agent, Swing((Agent.UsageDirection)d, false, false, CombatCollisionResult.Parried));
                void ShieldRight(TrackedAgent a, TrackedAgent v, int d) => logic.ImpactModelHitTaken(a.Agent, v.Agent, Swing((Agent.UsageDirection)d, true, true, CombatCollisionResult.Blocked));
                void Lands(TrackedAgent a, TrackedAgent v, int d)
                {
                    swingDir = d;
                    logic.ImpactModelHitTaken(a.Agent, v.Agent, Swing((Agent.UsageDirection)d, false, false, CombatCollisionResult.StrikeAgent));
                }
                void WindUp(TrackedAgent you2, int dir)
                {
                    swingDir = dir;
                    logic.ObserveAction(you2, ActReady, Now(), in ar);
                }
                float Strike(TrackedAgent you2, TrackedAgent on, int d, float damage = 20f)
                {
                    float got = ImpactHooks.PerfectStrike(you2.Agent, on.Agent, false, Swing((Agent.UsageDirection)d, false, false, CombatCollisionResult.StrikeAgent), damage);
                    Lands(you2, on, d);
                    return got;
                }
                uint Frame(TrackedAgent m, uint his)
                {
                    var s = m.Input;
                    if (s == null) return his;
                    uint f = his;
                    if (s.Opening) f = AiInputHook.ApplyOpening(s, f);
                    if (s.CounterSwing) f = AiInputHook.ApplyCounter(s, f);
                    return f;
                }
                bool Free(TrackedAgent m) => (m.Input == null || (!m.Input.Opening && !m.Input.CounterSwing)) && !body.NoDefend.Contains(m.AgentIndex);
                ArrowReading Arrow()
                {
                    AthleticsLogic.TryGetOpeningArrow(null, Now(), out var r);
                    return r;
                }

                var you = Man();
                body.You.Add(you.AgentIndex);
                logic.SmokePlayer = you.Agent;
                logic.SmokePlayerAiControlled = false;
                const uint Fwd = 0x1u;

                // ---- 1. A SWORD: your landed hit forces a hold - his high guard (the first candidate), the arrow on your thrust, 1.2 s
                var a1 = Man();
                Lands(you, a1, OpeningMath.AttackRight);
                var h = logic.OpeningHold;
                Check(h.Holding(a1.AgentIndex) && h.Guard == OpeningMath.GuardUp && h.Opening == OpeningMath.AttackDown && Math.Abs(h.Length - 1.2) < 1e-6
                      && logic.OpeningForceKind == OpeningForceKind.Hold && ReferenceEquals(logic.OpeningForced, a1) && !a1.Impact.Reeling(Now()),
                    "your landed hit did not force a 1.2 s hold in the high guard: " + h.Phase + " " + h.Guard + " " + h.Length + " " + logic.OpeningForceKind);
                Check(Frame(a1, Fwd | OpeningHoldMath.AttackLeft | OpeningForceMath.DefendLeft) == (Fwd | OpeningForceMath.DefendUp), "the hold's frame is not his high guard with his attack out");
                var ar1 = Arrow();
                Check(ar1.Cue == ArrowCue.Hold && ar1.Mask == OpeningMath.Bit(OpeningMath.AttackDown) && ar1.Alpha == 1f, "the red arrow does not show the opening (bottom) steady: " + ar1.Cue + " " + ar1.Mask);
                LogHas("[impact] openings: first HOLD-BLOCK at ");
                LogHas("HOLDS his guard in the high guard for 1.20 s");
                //      a THIRD side (your left swing): a normal hit, a fresh hold, no stagger
                Tick(0.3);
                WindUp(you, OpeningMath.AttackLeft);
                Check(logic.LastHoldSwing == HoldSwing.ThirdSide && !body.NoDefend.Contains(a1.AgentIndex), "a third side was not judged so / took his flag: " + logic.LastHoldSwing);
                Tick(0.2);
                Lands(you, a1, OpeningMath.AttackLeft);
                Check(!a1.Impact.Reeling(Now()) && hs.Holds == 2 && hs.FreshAfterThirdSide == 1 && logic.OpeningHold.Holding(a1.AgentIndex) && logic.OpeningHold.Links == 0,
                    "a sword's third side was not a normal hit + a fresh hold: holds " + hs.Holds);
                //      EXACTLY the opening (your thrust), started quickly: his 'can defend' off at the commit, he staggers, the perfect strike x4
                Tick(0.2);
                WindUp(you, OpeningMath.AttackDown);
                Check(logic.LastHoldSwing == HoldSwing.Wins && body.NoDefend.Contains(a1.AgentIndex) && logic.OpeningForceKind == OpeningForceKind.Hold,
                    "your thrust into the opening did not win / take his 'can defend': " + logic.LastHoldSwing);
                LogHas("WINS the hold, his 'can defend' off until the blow");
                Tick(0.3);
                Lands(you, a1, OpeningMath.AttackDown);
                Check(logic.LastOpeningHit == OpeningHit.Staggers && a1.Impact.Reeling(Now()) && a1.Impact.ReelCause == ReelCause.Opening && Math.Abs(logic.LastOpeningReelAsked - 0.8) < 1e-6
                      && hs.Staggers[2] == 1 && st.Staggers[(int)OpeningEntry.Hold] == 1 && logic.OpeningForced == null && !logic.OpeningHold.Active && Free(a1),
                    "your thrust into the opening did not stagger him 0.8 s / let everything go: " + logic.LastOpeningHit + " " + a1.Impact.ReelCause);
                Tick();
                Check(sink.Lines.Any(l => l.Text == AthleticsLogic.OpeningText) && impactBody.Staggers.Any(s => s.Man == a1.AgentIndex), "no OPENING! / no stagger played");
                Tick(0.2);
                float got = Strike(you, a1, OpeningMath.AttackUp);
                Check(Math.Abs(got - 80f) < 1e-3, "your overhead on the reeling man (the quick hold) was not a PERFECT STRIKE x4: " + got);
                Tick(3);

                // ---- 2. AN AXE: his LEFT guard (dice) - any side but his guard wins; into his guard (your right swing) he parries = a link
                weapon = axe;
                speed = 80; // a slow axe: the hold x95/80
                var a2 = Man();
                dice.Next(0.5); // the axe's candidates high-of-low / left / right: 0.5 -> the second, his LEFT guard (blocks your right swing)
                Lands(you, a2, OpeningMath.AttackUp);
                h = logic.OpeningHold;
                Check(h.Holding(a2.AgentIndex) && h.Guard == OpeningMath.GuardLeft && Math.Abs(h.Length - 1.2 * 95 / 80) < 1e-6,
                    "the axe's hold is not his left guard x95/80: " + h.Guard + " " + h.Length);
                Tick(0.3);
                WindUp(you, OpeningMath.AttackRight);
                Check(logic.LastHoldSwing == HoldSwing.IntoGuard, "your right swing into his left guard was not 'into his guard'");
                Tick(0.2);
                Parried(you, a2, OpeningMath.AttackRight);
                h = logic.OpeningHold;
                Check(h.Holding(a2.AgentIndex) && h.Links == 1 && hs.Links == 1 && Math.Abs(h.Length - (1.2 * 95 / 80 + 0.4)) < 1e-6 && Math.Abs(h.Start - Now()) < 1e-6,
                    "his parry of your swing into his guard was not a link (+0.4 from his block): " + h.Links + " " + h.Length);
                LogHas("BLOCKED your right swing into his held guard - LINK 1");
                Tick(0.2);
                WindUp(you, OpeningMath.AttackUp); // 0.2 s after his block: the whole bonus; an overhead is not his guard - it wins
                Check(hs.BonusGiven == 1 && hs.BonusFull == 1 && logic.LastHoldSwing == HoldSwing.Wins, "the chain-speed bonus / the axe's other side: " + hs.BonusGiven + " " + logic.LastHoldSwing);
                Tick(0.3);
                Lands(you, a2, OpeningMath.AttackUp);
                Check(a2.Impact.Reeling(Now()) && hs.Staggers[1] == 1 && Math.Abs(logic.LastOpeningReelAsked - 1.1) < 1e-6,
                    "the axe's overhead did not stagger him at link 2 (1.1 s): " + logic.LastOpeningReelAsked);
                Tick(3);
                weapon = sword;
                speed = 95;

                // ---- 3. LINKS GROW, a block after the hold ran out is a link, the lengthened hold ends in a COUNTER-SWING (100 %)
                var a3 = Man();
                Lands(you, a3, OpeningMath.AttackRight); // high guard: blocks your overhead
                Tick(0.3);
                WindUp(you, OpeningMath.AttackUp);
                Tick(0.2);
                Parried(you, a3, OpeningMath.AttackUp);
                Check(logic.OpeningHold.Links == 1 && Math.Abs(logic.OpeningHold.Length - 1.6) < 1e-6, "link 1 is not 1.6 s: " + logic.OpeningHold.Length);
                Tick(0.7); // your next wind-up 0.7 s after his block: share (1.0 - 0.7) / 0.7 -> +0.17 s
                WindUp(you, OpeningMath.AttackUp);
                double l2 = logic.OpeningHold.Length;
                Check(Math.Abs(l2 - (1.6 + 0.4 * 0.3 / 0.7)) < 0.03, "the partial chain-speed bonus is wrong: " + l2);
                Tick(0.2);
                Parried(you, a3, OpeningMath.AttackUp);
                Check(logic.OpeningHold.Links == 2 && Math.Abs(logic.OpeningHold.Length - Math.Min(2.5, l2 + 0.4)) < 1e-6, "link 2 did not grow the hold: " + logic.OpeningHold.Length);
                //      you stop swinging - the hold runs out: the counter-swing (100 % after a lengthened hold), ready - his attack bit written
                double until = logic.OpeningHold.Until;
                for (int n = 0; n < 400 && logic.OpeningHold.Phase == HoldPhase.Holding; n++) Tick();
                Check(logic.OpeningHold.Phase == HoldPhase.After && Math.Abs(t - until) < 0.05 && logic.OpeningForced == null,
                    "the hold did not run out at its end / let his guard go: " + logic.OpeningHold.Phase + " at " + t + " (until " + until + ")");
                Check(hs.CounterRolledLater == 1 && hs.CounterThrownLater == 1 && ReferenceEquals(logic.OpeningCounterSwinger, a3) && logic.OpeningCounterReady,
                    "the lengthened hold's counter-swing was not thrown (100 %)");
                uint ready = Frame(a3, Fwd | OpeningForceMath.DefendDown);
                Check((ready & OpeningForceMath.GuardBits) == 0 && OpeningMath.IsAttack(OpeningHoldMathAttackOf(ready & AiInputMath.AttackMask)) && (ready & Fwd) != 0,
                    "the counter-swing's READY frame is not one attack bit (his guard out): 0x" + ready.ToString("X"));
                LogHas("he COUNTER-SWINGS - his ");
                Tick(0.45);
                Check(!logic.OpeningCounterReady && Frame(a3, Fwd | ready) == Fwd && hs.CounterReleased == 1, "the counter-swing was not RELEASED (every attack / guard bit out)");
                Tick(0.6);
                Check(logic.OpeningCounterSwinger == null && Free(a3) && Frame(a3, Fwd | OpeningHoldMath.AttackUp) == (Fwd | OpeningHoldMath.AttackUp), "the counter-swing was not let go");
                //      your swing blocked within the link window after the hold ran out: a link, held again
                Parried(you, a3, OpeningMath.AttackUp);
                Check(logic.OpeningHold.Holding(a3.AgentIndex) && logic.OpeningHold.Links == 3 && hs.LinksAfter == 1 && logic.OpeningForceKind == OpeningForceKind.Hold,
                    "a block right after the hold ran out was not a link: " + logic.OpeningHold.Phase + " " + hs.LinksAfter);
                LogHas("right after his hold ran out - LINK 3");
                Tick(3.0); // runs out (2.5 cap) - the counter-swing again, then the link window lapses
                Tick(2.5);
                Check(!logic.OpeningHold.Active && hs.Ends[(int)HoldEnd.Gap] == 1 && hs.CounterThrownLater == 2, "the chain did not lapse after the link window: " + logic.OpeningHold.Phase);

                // ---- 4. THE FIRST HOLD: the counter-swing at 30 % - thrown at 0.29, not at 0.31
                var a4 = Man();
                Lands(you, a4, OpeningMath.AttackRight);
                dice.Next(0.29, 0.0); // the roll, then his direction (his guard was chosen at the hit)
                Tick(1.3);
                Check(hs.CounterRolledFirst == 1 && hs.CounterThrownFirst == 1 && ReferenceEquals(logic.OpeningCounterSwinger, a4), "the first hold's counter-swing at 0.29 < 30 % was not thrown");
                Tick(1.2);
                Tick(2.5);
                Lands(you, a4, OpeningMath.AttackRight);
                dice.Next(0.31); // the roll (his guard was chosen at the hit)
                Tick(1.3);
                Check(hs.CounterRolledFirst == 2 && hs.CounterThrownFirst == 1 && logic.OpeningCounterSwinger == null && Free(a4), "the first hold's counter-swing at 0.31 was thrown");
                LogHas("counter-swing chance 30%: no counter-swing");
                Tick(2.5);

                // ---- 4b (step 107d). A WINNING swing he parries anyway is COUNTED (OnMeleeHit's contact comes before the block's decision - it closed
                //      the flight first, so the count never moved); his counter-swing's wind-up is never rolled into a kick / bash by step 81
                var w1 = Man();
                Lands(you, w1, OpeningMath.AttackRight); // high guard: the opening is your thrust
                Tick(0.2);
                WindUp(you, OpeningMath.AttackDown);
                Check(logic.LastHoldSwing == HoldSwing.Wins && body.NoDefend.Contains(w1.AgentIndex), "107d: the thrust into the opening was not judged winning");
                Tick(0.2);
                int wbBefore = hs.WinBlocked;
                Parried(you, w1, OpeningMath.AttackDown);
                Check(hs.WinBlocked == wbBefore + 1 && logic.OpeningHold.Links == 1 && !body.NoDefend.Contains(w1.AgentIndex),
                    "107d: a winning swing parried anyway was not counted once (a link, his flag back): " + hs.WinBlocked + " " + logic.OpeningHold.Links);
                LogHas("your WINNING swing was stopped by ");
                for (int n = 0; n < 400 && logic.OpeningHold.Phase == HoldPhase.Holding; n++) Tick();
                Check(ReferenceEquals(logic.OpeningCounterSwinger, w1) && logic.OpeningCounterReady, "107d: no counter-swing after the lengthened hold");
                long kickLooks = logic.AiKickStats.Looks;
                logic.DecideAiMove(w1, Now(), AiKickRules.From(S), in ar);
                Check(hs.CounterKickRollsSkipped == 1 && logic.AiKickStats.Looks == kickLooks && w1.AiKickPhase == AiKickPhase.None,
                    "107d: step 81 rolled a kick on the counter-swing's own wind-up");
                Tick(3);
                Tick(2.5);

                // ---- 5. A SHIELDMAN: his shield block opens a SHORT hold (a kite 130 cm: 0.5 s); your winning strike LOWERS his shield, he staggers
                var sh = Man(130);
                ShieldRight(you, sh, OpeningMath.AttackRight);
                h = logic.OpeningHold;
                Check(h.Holding(sh.AgentIndex) && Math.Abs(h.Length - 0.5) < 1e-6 && hs.ShieldHolds == 1 && hs.ShieldHoldsByBlock == 1 && !logic.OpeningForceLowered,
                    "his kite shield's block did not open a 0.5 s hold (shield up): " + h.Length);
                Check(Frame(sh, Fwd | OpeningForceMath.DefendRight) == (Fwd | OpeningForceMath.DefendUp), "the shieldman's hold is not his shield turned to the high guard");
                Tick(0.1);
                WindUp(you, OpeningMath.AttackDown);
                Check(logic.LastHoldSwing == HoldSwing.Wins && logic.OpeningForceLowered && body.NoDefend.Contains(sh.AgentIndex) && hs.ShieldLowered == 1
                      && (Frame(sh, Fwd | OpeningForceMath.DefendRight | OpeningForceMath.DefendBlock) & OpeningForceMath.GuardBits) == 0,
                    "your winning strike did not LOWER his shield / take his flag");
                Tick(0.6); // the hold (0.5 s) is over, but your swing is in the air: his shield stays down
                Check(logic.OpeningHold.Holding(sh.AgentIndex) && logic.OpeningForceLowered, "the hold ended under your swing in the air");
                Lands(you, sh, OpeningMath.AttackDown);
                Check(sh.Impact.Reeling(Now()) && sh.Impact.ReelCause == ReelCause.Opening && Free(sh), "the lowered shieldman did not stagger / everything let go");
                Tick();
                Check(sink.Lines.Any(l => l.Text == AthleticsLogic.GuardBrokenText), "no GUARD BROKEN for the lowered shield");
                Tick(3);
                //      a small shield (60 cm): 1.0 s; your hit opens it too; a strike into his shield = a link
                var sh2 = Man(60);
                Lands(you, sh2, OpeningMath.AttackRight);
                Check(Math.Abs(logic.OpeningHold.Length - 1.0) < 1e-6 && hs.ShieldHolds == 2, "the small shield's hold is not 1.0 s: " + logic.OpeningHold.Length);
                Tick(0.3);
                ShieldRight(you, sh2, OpeningMath.AttackRight);
                Check(logic.OpeningHold.Links == 1 && Math.Abs(logic.OpeningHold.Length - 1.4) < 1e-6, "his shield stopping your strike was not a link: " + logic.OpeningHold.Links);

                // ---- 6. a hit from BEHIND still reels him at once (step 106), the hold ended
                inputs.Angles[sh2.Agent.Index] = 170;
                Lands(you, sh2, OpeningMath.AttackRight);
                inputs.Angles.Remove(sh2.Agent.Index);
                Check(sh2.Impact.Reeling(Now()) && st.Staggers[(int)OpeningEntry.Back] == 1 && !logic.OpeningHold.Active && hs.Ends[(int)HoldEnd.BackHit] == 1 && Free(sh2),
                    "a hit from behind on a holding man did not reel him / end the hold");
                Tick(3);

                // ---- 7. THE EXITS release everything: the switch, the master switch, removed (no engine call), the mission end, a target switch
                var e1 = Man();
                Lands(you, e1, OpeningMath.AttackRight);
                S.Set(SettingsSchema.OpeningHoldsEnabled, false, SettingSources.File);
                Tick();
                Check(!logic.OpeningHold.Active && Free(e1) && logic.OpeningForced == null, "OpeningHoldsEnabled off did not release the hold");
                S.Set(SettingsSchema.OpeningHoldsEnabled, true, SettingSources.File);
                Tick(3);
                Lands(you, e1, OpeningMath.AttackRight);
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.File);
                Tick();
                Check(!logic.OpeningHold.Active && Free(e1), "the master switch did not release the hold");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                Tick(3);
                //      the counter-swing cut by the switch
                var e2 = Man();
                Lands(you, e2, OpeningMath.AttackRight);
                dice.Next(0.0, 0.0);
                Tick(1.3);
                Check(ReferenceEquals(logic.OpeningCounterSwinger, e2), "no counter-swing to cut");
                S.Set(SettingsSchema.OpeningsEnabled, false, SettingSources.File);
                Tick();
                Check(logic.OpeningCounterSwinger == null && Free(e2) && hs.CounterCut >= 1, "OpeningsEnabled off did not cut the counter-swing");
                S.Set(SettingsSchema.OpeningsEnabled, true, SettingSources.File);
                Tick(3);
                //      a target switch: your blow on another man ends the hold
                var e3 = Man();
                var other = Man();
                Lands(you, e3, OpeningMath.AttackRight);
                Tick(0.2);
                Parried(you, other, OpeningMath.AttackRight);
                Check(!logic.OpeningHold.Active && Free(e3) && hs.Ends[(int)HoldEnd.TargetSwitch] >= 1, "a blow on another man did not end the hold");
                Tick(3);
                //      removed: the next tick lets go without an engine call
                var e4 = Man();
                Lands(you, e4, OpeningMath.AttackRight);
                int rel = body.Releases;
                CallPrivate(logic, "Untrack", e4.Agent);
                Tick();
                Check(!logic.OpeningHold.Active && logic.OpeningForced == null && body.Releases == rel, "removed: the hold not let go / an engine call on a removed man");
                Tick(3);
                //      the mission end (before the summary)
                var e5 = Man();
                Lands(you, e5, OpeningMath.AttackRight);
                WindUp(you, OpeningMath.AttackDown);
                CallPrivate(logic, "CloseOpenings", t);
                Check(!logic.OpeningHold.Active && logic.OpeningForced == null && Free(e5), "the mission end left a hold / a flag");

                // ---- 8. no errors; the [summary] line
                Check(hs.Errors == 0 && st.Errors == 0 && fs.Errors == 0, "errors: " + hs.Errors + " / " + st.Errors + " / " + fs.Errors);
                string line = hs.SummaryLine(OpeningHoldRules.From(S));
                Check(line.Contains("openings - hold-block (step 107; ON)") && line.Contains("STAGGERS by weapon (sword / axe-mace / spear) 2 / 1 / 0")
                      && line.Contains("SHIELDMEN: holds 2 (by his shield block 1)") && line.Contains("COUNTER-SWINGS after the first hold rolled"),
                    "the hold-block [summary] line: " + line);
                Console.WriteLine("        step 107: holds " + hs.Holds + ", links " + hs.Links + " (after " + hs.LinksAfter + "), staggers " + string.Join("/", hs.Staggers)
                                  + ", counter-swings thrown " + hs.CounterThrownFirst + " + " + hs.CounterThrownLater + ", shields lowered " + hs.ShieldLowered);
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

        private static int OpeningHoldMathAttackOf(uint bit) => bit switch
        {
            OpeningHoldMath.AttackUp => OpeningMath.AttackUp,
            OpeningHoldMath.AttackDown => OpeningMath.AttackDown,
            OpeningHoldMath.AttackLeft => OpeningMath.AttackLeft,
            OpeningHoldMath.AttackRight => OpeningMath.AttackRight,
            _ => OpeningMath.Unknown,
        };
    }
}

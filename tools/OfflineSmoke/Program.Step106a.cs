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
    /// Offline check for steps 106a / 106a2 - OPENINGS, THE SHADOW of Toni's REVISED design (AI_NOTES "Step 106", DESIGN decision 95): through the
    /// REAL logic (the impact decision's block and clean-hit seams, OnMeleeHit's part, step 13's phases for your wind-up and miss, the tick) with
    /// stand-ins for the engine's three reads (his guard at contact, your swing's direction, your weapon's attacks): unshielded in 3 moves (parried
    /// → off balance, a thrust on the flash lands → the stagger, the overhead → PERFECT STRIKE x4); the shadow block roll and a growing chain; a
    /// late hit still off balance, a reel nobody finished; a shield in 4 moves (its first block still holds the side); a hit from behind; an
    /// unguarded front hit; an axe's relaxed finish (overhead stagger → left, x2); the gap, another man, his hit on you, AI vs AI, a rider; the
    /// shadow switch and the master switch; nothing applied; the [summary] line.
    /// </summary>
    internal static partial class Program
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void OpeningsShadowThroughTheLogic()
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
                Check(SettingsSchema.OpeningsEnabled.Default == 1 && SettingsSchema.OpeningsShadowLog.Default == 1,
                    "the embedded openings defaults are not the design's (live ON since step 106d, the shadow ON)");
                S.Set(SettingsSchema.OpeningsEnabled, false, SettingSources.File); // this step is the SHADOW: the live switch off (step 106b-106d's steps run it on)
                var inputs = new FakeImpactInputs { Damage = 20 };
                var guard = new FakeGuardBody();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = new FakeImpactBody();
                logic.ImpactInputs = inputs;
                logic.GuardBody = guard;
                logic.SlowMoBody = new FakeSlowMoBody();
                logic.MessageSink = new FakeMessageSink();
                logic.ImpactDice = new ScriptDice { Rest = 0.999 }; // no impact outcome rolls up
                logic.OpeningDice = new ScriptDice { Rest = 0.0 }; // the first candidate guard
                var guards = new System.Collections.Generic.Dictionary<int, int>();
                int swingDir = OpeningMath.Unknown, weaponMask = OpeningMath.AllAttacks;
                int guardReads = 0, swingReads = 0;
                logic.OpeningGuardRead = a =>
                {
                    guardReads++;
                    return guards.TryGetValue(a.Index, out int g) ? g : -1;
                };
                logic.OpeningSwingRead = a =>
                {
                    swingReads++;
                    return swingDir;
                };
                logic.OpeningWeaponRead = a => weaponMask;
                double t = 106000;
                logic.TickImpact(t);
                int next = 10600;
                var st = logic.OpeningStats;
                var ar = Rules;

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
                // your attack (direction d) met by him: parried / shield right side / landed
                void Parried(TrackedAgent a, TrackedAgent v, Agent.UsageDirection d) => logic.ImpactModelHitTaken(a.Agent, v.Agent, Swing(d, false, false, CombatCollisionResult.Parried));
                void ShieldRight(TrackedAgent a, TrackedAgent v, Agent.UsageDirection d) => logic.ImpactModelHitTaken(a.Agent, v.Agent, Swing(d, true, true, CombatCollisionResult.Blocked));
                void Lands(TrackedAgent a, TrackedAgent v, Agent.UsageDirection d)
                {
                    swingDir = (int)d; // the swing read at the hit
                    logic.ImpactModelHitTaken(a.Agent, v.Agent, Swing(d, false, false, CombatCollisionResult.StrikeAgent));
                }
                // your wind-up through step 13's phases (the ready), its release and the idle after it (a swing that hit nothing = a miss)
                void WindUp(TrackedAgent you2, int dir)
                {
                    swingDir = dir;
                    logic.ObserveAction(you2, ActReady, Now(), in ar);
                }
                void ReleaseAndMiss(TrackedAgent you2)
                {
                    logic.ObserveAction(you2, ActRelease, Now(), in ar);
                    Tick(0.3);
                    logic.ObserveAction(you2, ActIdle, Now(), in ar);
                }
                // the mission's time as the logic sees it in the smoke: its last tick (TickFor leaves t one frame past it)
                double Now() => t - 1.0 / 60;

                var you = Man(false);
                logic.SmokePlayer = you.Agent;
                logic.SmokePlayerAiControlled = false;
                var ai = Man(false);

                // ---- 1. UNSHIELDED, 3 MOVES: your right swing parried by his left guard - OFF BALANCE (the die 0: the first other guard, the high
                //         guard - the arrow bottom, your thrust); your thrust started ON the flash (timing 1); it lands - he would stagger 0.8 s, the
                //         perfect-strike side = your overhead; your overhead on the reeling man = PERFECT STRIKE x4 (a 4-direction weapon at timing 1)
                guards[ai.Agent.Index] = OpeningMath.GuardLeft;
                Parried(you, ai, Agent.UsageDirection.AttackRight);
                var p1 = logic.LastOpening;
                Check(st.BlocksTotal == 1 && st.Blocks[1] == 1 && st.WeaponBlocks == 1 && p1.Opens && p1.K == 1 && st.Entries[(int)OpeningEntry.Blocked] == 1,
                    "your parried swing put him off balance nowhere at link 1: " + st.BlocksTotal);
                Check(p1.PreviousGuard == OpeningMath.GuardLeft && p1.Guard == OpeningMath.GuardUp && p1.Opening == OpeningMath.AttackDown && p1.Arrow == OpeningArrow.Bottom,
                    "the plan is not left guard → high guard → your thrust, arrow bottom: " + p1.Guard + " / " + p1.Opening);
                Check(Math.Abs(p1.Flash - 0.3) < 1e-6 && Math.Abs(p1.Perfect - 0.15) < 1e-6 && Math.Abs(p1.Window - 1.0) < 1e-6 && !p1.ShieldHolds,
                    "the flash / perfect / window at skill 100, link 1 are not 0.3 / 0.15 / 1.0 s: " + p1.Flash + " / " + p1.Perfect + " / " + p1.Window);
                Check(st.Pairs[OpeningMath.AttackRight * 5 + (OpeningMath.GuardLeft - 4)] == 1 && guardReads == 1, "the side pairing (your right swing, his left guard) was not counted");
                Check(logic.OpeningPending && logic.OpeningChain.OffBalance && logic.OpeningChain.K == 1 && logic.OpeningChain.Target == ai.AgentIndex, "no follow-up awaited / the chain is not on him");
                LogHas("[impact] YOU: openings (shadow): YOUR right swing blocked by ");
                Tick(0.3);
                WindUp(you, OpeningMath.AttackDown);
                Check(logic.LastOpeningVerdict == OpeningVerdict.Opened && Math.Abs(logic.LastOpeningScore - 1) < 1e-6 && st.Scores[0] == 1 && swingReads == 1,
                    "your thrust on the flash was not INTO the side with timing 1: " + logic.LastOpeningVerdict + " " + logic.LastOpeningScore);
                Tick(0.3);
                guards[ai.Agent.Index] = OpeningMath.GuardDown;
                Lands(you, ai, Agent.UsageDirection.AttackDown);
                Check(logic.LastOpeningHit == OpeningHit.Staggers && st.Staggers[(int)OpeningEntry.Blocked] == 1 && logic.OpeningChain.Reeling(Now())
                      && logic.OpeningChain.FinishMask == OpeningMath.Bit(OpeningMath.AttackUp) && Math.Abs(logic.OpeningChain.Score - 1) < 1e-6 && !logic.OpeningPending,
                    "your landed thrust off balance was no stagger with the overhead to finish: " + logic.LastOpeningHit);
                LogHas("he WOULD stagger 0.80 s");
                Tick(0.3);
                Lands(you, ai, Agent.UsageDirection.AttackUp);
                Check(logic.LastOpeningHit == OpeningHit.FinishPerfect && st.FinishPerfect == 1 && Math.Abs(logic.LastOpeningMultiplier - 4.0) < 1e-6 && !logic.OpeningChain.Active
                      && st.PerfectByDirections[2] == 1, "your overhead on the reeling man was no PERFECT STRIKE x4: " + logic.LastOpeningHit + " x" + logic.LastOpeningMultiplier);
                LogHas("WOULD be a PERFECT STRIKE x4.00");

                // ---- 2. THE BLOCK ROLL and a GROWING chain: link 1, your thrust on the flash, he parries it anyway - the shadow roll at 0 % says it WOULD
                //         have got through (link 2); a wrong direction and its miss keep the chain; a late hit still within the off-balance time staggers
                //         (link 2: 1.1 s), and nobody finishes - the reel runs out unused
                Tick(3);
                guards[ai.Agent.Index] = OpeningMath.GuardLeft;
                Parried(you, ai, Agent.UsageDirection.AttackRight);
                Tick(0.3);
                WindUp(you, OpeningMath.AttackDown);
                Tick(0.3);
                guards[ai.Agent.Index] = OpeningMath.GuardDown;
                Parried(you, ai, Agent.UsageDirection.AttackDown);
                Check(st.BlockRolls == 1 && st.WouldGetThrough == 1 && st.ThroughByK[1] == 1 && logic.OpeningChain.K == 2 && Math.Abs(logic.LastOpening.Window - 1.25) < 1e-6,
                    "the perfect follow-up he parried did not roll 'would get through' and grow the chain to link 2 (window 1.25): " + st.BlockRolls + " / " + st.WouldGetThrough);
                LogHas("WOULD have got through");
                Tick(0.3);
                WindUp(you, OpeningMath.Opposite(logic.LastOpening.Opening));
                Check(logic.LastOpeningVerdict == OpeningVerdict.WrongDirection, "the opposite of the side was not the wrong direction: " + logic.LastOpeningVerdict);
                Tick(0.2);
                ReleaseAndMiss(you);
                Check(st.Misses == 1 && logic.OpeningChain.K == 2 && logic.OpeningChain.OffBalance && !logic.OpeningPending, "the miss did not keep the chain: k " + logic.OpeningChain.K);
                Tick(0.5);
                guards[ai.Agent.Index] = OpeningMath.GuardRight;
                Lands(you, ai, Agent.UsageDirection.AttackLeft);
                Check(logic.LastOpeningHit == OpeningHit.Staggers && st.Staggers[(int)OpeningEntry.Blocked] == 2 && Math.Abs(logic.OpeningChain.ReelUntil - Now() - 1.1) < 1e-6,
                    "a late hit still off balance was no 1.1 s stagger at link 2: " + logic.LastOpeningHit);
                Tick(1.5);
                Check(!logic.OpeningChain.Active && st.ReelsUnused == 1 && st.Resets[(int)OpeningReset.ReelOver] == 2, "the unused reel did not end the chain: " + st.ReelsUnused);

                // ---- 3. SHIELDED, 4 MOVES: the first shield block shows the side but the shield still holds it (the roll at 100 %: he blocks); the
                //         second gives way
                Tick(3);
                var sh = Man(true);
                guards[sh.Agent.Index] = OpeningMath.GuardUp;
                ShieldRight(you, sh, Agent.UsageDirection.AttackUp);
                Check(logic.LastOpening.Opens && logic.LastOpening.ShieldHolds && st.ShieldHolds == 1 && st.Entries[(int)OpeningEntry.ShieldBlocked] == 1 && logic.OpeningChain.K == 1,
                    "a shield's first block did not show the side with the shield still holding");
                Tick(0.3);
                WindUp(you, logic.LastOpening.Opening);
                Tick(0.3);
                ShieldRight(you, sh, Agent.UsageDirection.AttackUp);
                Check(st.BlockRolls == 2 && st.WouldGetThrough == 1 && logic.OpeningChain.K == 2 && !logic.LastOpening.ShieldHolds,
                    "the shield's held side was not blocked at 100 % / its second block still holds: " + st.BlockRolls + " / " + st.WouldGetThrough);
                LogHas("his shield still holds");

                // ---- 4. A HIT FROM BEHIND always staggers (the finishing side the opposite: your left); the same side again = no bonus (step 105's roll)
                Tick(3);
                var back = Man(false);
                guards[back.Agent.Index] = OpeningMath.GuardUp;
                inputs.Angles[back.Agent.Index] = 170;
                Lands(you, back, Agent.UsageDirection.AttackRight);
                Check(logic.LastOpeningHit == OpeningHit.Staggers && st.Staggers[(int)OpeningEntry.Back] == 1 && logic.OpeningChain.FinishMask == OpeningMath.Bit(OpeningMath.AttackLeft)
, "a hit from behind was no stagger with the left to finish");
                Tick(0.2);
                Lands(you, back, Agent.UsageDirection.AttackRight);
                Check(logic.LastOpeningHit == OpeningHit.FinishOther && st.FinishOther == 1 && !logic.OpeningChain.Active, "the same side on the reeling man was not 'another side'");
                inputs.Angles.Remove(back.Agent.Index);

                // ---- 5. AN UNGUARDED FRONT HIT puts him off balance (the window x1.5); the next hit staggers; a guarded front hit with no chain = nothing
                Tick(3);
                var open = Man(false);
                guards.Remove(open.Agent.Index);
                Lands(you, open, Agent.UsageDirection.AttackRight);
                Check(logic.LastOpeningHit == OpeningHit.OffBalance && st.UnguardedOffBalance == 1 && logic.LastOpening.Entry == OpeningEntry.FrontUnguarded
                      && Math.Abs(logic.LastOpening.Window - 1.5) < 1e-6 && logic.OpeningPending, "an unguarded front hit did not put him off balance with the 1.5 s window");
                Tick(0.4);
                guards[open.Agent.Index] = OpeningMath.GuardUp;
                Lands(you, open, Agent.UsageDirection.AttackUp);
                Check(logic.LastOpeningHit == OpeningHit.Staggers && st.Staggers[(int)OpeningEntry.FrontUnguarded] == 1, "the next hit after the unguarded one did not stagger");
                Tick(3);
                Lands(you, open, Agent.UsageDirection.AttackUp);
                Check(logic.LastOpeningHit == OpeningHit.Nothing && !logic.OpeningChain.Active, "a guarded front hit with no chain did something: " + logic.LastOpeningHit);

                // ---- 6. A NO-THRUST WEAPON (an axe): an overhead stagger finished by a LEFT swing (the weapon's relaxed side) - x2 at timing 0 (axe 2-3)
                Tick(3);
                weaponMask = OpeningMath.WeaponMask(true, false);
                guards[ai.Agent.Index] = OpeningMath.GuardLeft;
                Parried(you, ai, Agent.UsageDirection.AttackRight);
                Check(logic.LastOpening.Opening != OpeningMath.AttackDown, "an axe was given the thrust side");
                Tick(0.3);
                Lands(you, ai, Agent.UsageDirection.AttackUp);
                Check(logic.OpeningChain.FinishMask == (OpeningMath.Bit(OpeningMath.AttackLeft) | OpeningMath.Bit(OpeningMath.AttackRight)), "an axe's overhead stagger is not finished left or right");
                Tick(0.2);
                Lands(you, ai, Agent.UsageDirection.AttackLeft);
                Check(logic.LastOpeningHit == OpeningHit.FinishPerfect && st.RelaxedFinishes == 1 && Math.Abs(logic.LastOpeningMultiplier - 2.0) < 1e-6 && st.PerfectByDirections[1] == 1,
                    "the axe's left swing was no relaxed PERFECT STRIKE x2: " + logic.LastOpeningHit + " x" + logic.LastOpeningMultiplier);
                weaponMask = OpeningMath.AllAttacks;

                // ---- 7. the GAP (no swing for 2.5 s), ANOTHER MAN (a target switch), HIS HIT ON YOU
                Tick(3);
                int gaps = st.Resets[(int)OpeningReset.Gap], noSwing = st.Verdicts[(int)OpeningVerdict.NoSwing];
                Parried(you, ai, Agent.UsageDirection.AttackRight);
                Tick(2.5);
                Check(st.Verdicts[(int)OpeningVerdict.NoSwing] == noSwing + 1 && st.Resets[(int)OpeningReset.Gap] == gaps + 1 && !logic.OpeningChain.Active && !logic.OpeningPending,
                    "2.5 s without a swing did not close the follow-up (no swing) and reset the chain (gap)");
                int switches = st.Resets[(int)OpeningReset.TargetSwitch];
                Parried(you, ai, Agent.UsageDirection.AttackRight);
                Tick(0.4);
                ShieldRight(you, sh, Agent.UsageDirection.AttackRight);
                Check(st.Resets[(int)OpeningReset.TargetSwitch] == switches + 1 && logic.OpeningChain.Target == sh.AgentIndex && logic.OpeningChain.K == 1, "a block by another man did not restart the chain on him");
                Tick(0.3);
                logic.ImpactModelHitTaken(sh.Agent, you.Agent, CleanHit());
                Check(st.Resets[(int)OpeningReset.HeHitYou] == 1 && !logic.OpeningChain.Active, "his hit on you did not reset the chain");

                // ---- 8. AI vs AI counted only; a RIDER skipped
                var ai2 = Man(false);
                Parried(ai2, ai, Agent.UsageDirection.AttackLeft);
                Check(st.AiVsAi == 1 && !logic.OpeningChain.Active && !logic.OpeningPending, "AI vs AI was not counted only");
                Tick(3);
                inputs.Mounted.Add(ai.Agent.Index);
                Parried(you, ai, Agent.UsageDirection.AttackRight);
                Check(st.Skips[(int)OpeningSkip.Mounted] == 1 && !logic.OpeningPending, "a rider's block was not skipped");
                inputs.Mounted.Remove(ai.Agent.Index);

                // ---- 9. the shadow switch and the master switch: nothing counted; a chain dropped at the next tick
                Tick(3);
                int before = st.BlocksTotal;
                S.Set(SettingsSchema.OpeningsShadowLog, false, SettingSources.File);
                Parried(you, ai, Agent.UsageDirection.AttackRight);
                Check(st.BlocksTotal == before, "the shadow log off still counted a block");
                S.Set(SettingsSchema.OpeningsShadowLog, true, SettingSources.File);
                Parried(you, ai, Agent.UsageDirection.AttackRight);
                Check(logic.OpeningPending, "the shadow back on planned nothing");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.File);
                Tick();
                Check(!logic.OpeningPending && !logic.OpeningChain.Active && st.Resets[(int)OpeningReset.Off] == 1, "the MASTER SWITCH off did not drop the follow-up and the chain");
                int b2 = st.BlocksTotal;
                Parried(you, ai, Agent.UsageDirection.AttackRight);
                Check(st.BlocksTotal == b2, "the MASTER SWITCH off still counted a block");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                Tick();

                // ---- 10. nothing applied (the live switch off for this step); no errors
                Check(!OpeningRules.From(S).Enabled, "the shadow step ran with the live openings on");
                Check(st.Errors == 0, "the openings logged errors: " + st.Errors);

                // ---- 11. the [summary] line
                string line = st.SummaryLine(OpeningRules.From(S));
                Check(line.StartsWith("openings (step 106d; live OFF (OpeningsEnabled) - counted only, the shadow log ON): ", StringComparison.Ordinal)
                      && line.Contains("WOULD have got through 1") && line.Contains("PERFECT STRIKE 2") && line.Contains("from behind 1")
                      && line.Contains("AI vs AI correct blocks on foot 1"), "the [summary] line: " + line);
                Console.WriteLine("        step 106a2: " + st.BlocksTotal + " blocked attacks, " + st.StatesTotal + " off-balance states, " + st.FinishPerfect + " perfect strikes");
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

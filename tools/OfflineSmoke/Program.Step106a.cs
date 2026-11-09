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
    /// Offline check for step 106a - OPENINGS, THE SHADOW (AI_NOTES "Step 106", DESIGN decision 95): through the REAL logic (the impact decision's
    /// block seam, OnMeleeHit's part, step 13's phases for your wind-up and miss, the tick) with stand-ins for the engine's three reads (his guard at
    /// contact, your swing's direction, your weapon's attacks): your parried right swing plans an opening (his left guard → the high guard → your
    /// thrust, arrow bottom); your thrust into it, blocked again (link 2); a wrong direction and a miss (the chain kept); a late swing, then your hit
    /// lands (reset); a shield opens only from link 2 at x0.75; the gap, another man, his hit on you, AI vs AI, a rider; the shadow switch and the
    /// master switch; nothing applied; the [summary] line.
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
                Check(SettingsSchema.OpeningsEnabled.Default == 0 && SettingsSchema.OpeningsShadowLog.Default == 1,
                    "the embedded openings defaults are not the design's (live OFF, the shadow ON)");
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
                void Lands(TrackedAgent a, TrackedAgent v, Agent.UsageDirection d) => logic.ImpactModelHitTaken(a.Agent, v.Agent, Swing(d, false, false, CombatCollisionResult.StrikeAgent));
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

                // ---- 1. your RIGHT swing parried by his LEFT guard: link 1, the high guard forced (the die 0 - the first other guard), your thrust, arrow bottom
                guards[ai.Agent.Index] = OpeningMath.GuardLeft;
                Parried(you, ai, Agent.UsageDirection.AttackRight);
                var p1 = logic.LastOpening;
                Check(st.BlocksTotal == 1 && st.Blocks[1] == 1 && st.WeaponBlocks == 1 && p1.Opens && p1.K == 1, "your parried swing planned no opening at link 1: " + st.BlocksTotal);
                Check(p1.PreviousGuard == OpeningMath.GuardLeft && p1.Guard == OpeningMath.GuardUp && p1.Opening == OpeningMath.AttackDown && p1.Arrow == OpeningArrow.Bottom,
                    "the plan is not left guard → high guard → your thrust, arrow bottom: " + p1.Guard + " / " + p1.Opening);
                Check(Math.Abs(p1.Window - 0.55) < 1e-6, "the window at skill 100, link 1 is not 0.55 s: " + p1.Window);
                Check(st.Pairs[OpeningMath.AttackRight * 5 + (OpeningMath.GuardLeft - 4)] == 1 && guardReads == 1, "the side pairing (your right swing, his left guard) was not counted");
                Check(logic.OpeningPending && logic.OpeningChain.K == 1 && logic.OpeningChain.Target == ai.AgentIndex, "no opening waits / the chain is not on him");
                LogHas("[impact] YOU: openings (shadow): YOUR right swing blocked by ");

                // ---- 2. your THRUST 0.3 s later (into the opening), its contact 0.45 s on: he parries again (low guard) - link 2
                Tick(0.3);
                WindUp(you, OpeningMath.AttackDown);
                Check(logic.LastOpeningVerdict == OpeningVerdict.Opened && st.Verdicts[(int)OpeningVerdict.Opened] == 1 && st.OpenedByK[1] == 1 && swingReads == 1,
                    "your thrust 0.3 s after the block was not INTO the opening: " + logic.LastOpeningVerdict);
                Tick(0.45);
                guards[ai.Agent.Index] = OpeningMath.GuardDown;
                Parried(you, ai, Agent.UsageDirection.AttackDown);
                Check(st.Contacts[(int)OpeningContact.BlockedAgain] == 1 && st.OpenedContacts[(int)OpeningContact.BlockedAgain] == 1 && st.ContactCount == 1
                      && Math.Abs(st.ContactSum - 0.45) < 0.02 && st.DirAgree == 1, "the thrust's contact was not 'blocked again' 0.45 s after its start / the directions agree");
                Check(logic.OpeningChain.K == 2 && logic.LastOpening.K == 2 && Math.Abs(logic.LastOpening.Window - 0.70) < 1e-6, "the second block is not link 2 (window 0.70)");
                LogHas("INTO the opening");

                // ---- 3. a WRONG direction (a left swing - the opening is your thrust again), then it misses: the chain kept (OpeningMissKeepsChain)
                Tick(0.3);
                WindUp(you, OpeningMath.AttackLeft);
                Check(logic.LastOpeningVerdict == OpeningVerdict.WrongDirection, "a left swing into a thrust opening was not the wrong direction: " + logic.LastOpeningVerdict);
                Tick(0.2);
                ReleaseAndMiss(you);
                Check(st.Misses == 1 && st.Contacts[(int)OpeningContact.None] == 1 && logic.OpeningChain.K == 2 && !logic.OpeningPending,
                    "the miss did not close the opening with no contact and keep the chain: misses " + st.Misses + ", k " + logic.OpeningChain.K);

                // ---- 4. link 3, a LATE swing (1.5 s > 0.1 + 0.85), then your hit LANDS on him: the chain resets
                Tick(0.3);
                guards[ai.Agent.Index] = OpeningMath.GuardRight;
                Parried(you, ai, Agent.UsageDirection.AttackLeft);
                Check(logic.OpeningChain.K == 3, "the third block within 2 s is not link 3: " + logic.OpeningChain.K);
                Tick(1.5);
                WindUp(you, OpeningMath.AttackUp);
                Check(logic.LastOpeningVerdict == OpeningVerdict.TooLate, "a swing 1.5 s after the block was not too late: " + logic.LastOpeningVerdict);
                Tick(0.4);
                Lands(you, ai, Agent.UsageDirection.AttackUp);
                Check(st.Contacts[(int)OpeningContact.Landed] == 1 && st.Resets[(int)OpeningReset.HitLanded] == 1 && !logic.OpeningChain.Active,
                    "your landed hit did not close the opening and reset the chain");

                // ---- 5. a TOO EARLY swing (0.05 s, before the force); a SHIELD opens only from link 2, its window x0.75
                Tick(3);
                var sh = Man(true);
                guards[sh.Agent.Index] = OpeningMath.GuardUp;
                ShieldRight(you, sh, Agent.UsageDirection.AttackUp);
                Check(st.Skips[(int)OpeningSkip.ShieldTooEarly] == 1 && st.ShieldBlocks == 1 && !logic.OpeningPending && logic.OpeningChain.K == 1,
                    "a shield's first block opened something (or the chain did not start)");
                Tick(0.5);
                ShieldRight(you, sh, Agent.UsageDirection.AttackUp);
                Check(logic.LastOpening.Opens && logic.LastOpening.K == 2 && Math.Abs(logic.LastOpening.Window - 0.70 * 0.75) < 1e-6
                      && Math.Abs(logic.LastOpening.BreakChance - 0.35) < 1e-6, "the shield's second block is not an opening at x0.75 with a 35 % guard break");
                Tick(0.05);
                WindUp(you, logic.LastOpening.Opening);
                Check(logic.LastOpeningVerdict == OpeningVerdict.TooEarly, "a swing 0.05 s after the block was not too early: " + logic.LastOpeningVerdict);

                // ---- 6. the GAP: link 1 on him, no swing for 2.5 s - no swing, the chain reset
                Tick(3);
                int gaps = st.Resets[(int)OpeningReset.Gap], noSwing = st.Verdicts[(int)OpeningVerdict.NoSwing];
                Parried(you, ai, Agent.UsageDirection.AttackRight);
                Tick(2.5);
                Check(st.Verdicts[(int)OpeningVerdict.NoSwing] == noSwing + 1 && st.Resets[(int)OpeningReset.Gap] == gaps + 1 && !logic.OpeningChain.Active && !logic.OpeningPending,
                    "2.5 s without a swing did not close the opening (no swing) and reset the chain (gap)");

                // ---- 7. ANOTHER MAN: his block after ai's = link 1 on him (target switch); HIS HIT ON YOU resets
                Parried(you, ai, Agent.UsageDirection.AttackRight);
                Tick(0.4);
                ShieldRight(you, sh, Agent.UsageDirection.AttackRight);
                Check(st.Resets[(int)OpeningReset.TargetSwitch] == 1 && logic.OpeningChain.Target == sh.AgentIndex && logic.OpeningChain.K == 1, "a block by another man did not restart the chain on him");
                Tick(0.3);
                logic.ImpactModelHitTaken(sh.Agent, you.Agent, CleanHit());
                Check(st.Resets[(int)OpeningReset.HeHitYou] == 1 && !logic.OpeningChain.Active, "his hit on you did not reset the chain");

                // ---- 8. AI vs AI counted only; a RIDER skipped; a weapon with no other opening
                var ai2 = Man(false);
                Parried(ai2, ai, Agent.UsageDirection.AttackLeft);
                Check(st.AiVsAi == 1 && !logic.OpeningChain.Active && !logic.OpeningPending, "AI vs AI was not counted only");
                Tick(3);
                inputs.Mounted.Add(ai.Agent.Index);
                Parried(you, ai, Agent.UsageDirection.AttackRight);
                Check(st.Skips[(int)OpeningSkip.Mounted] == 1 && !logic.OpeningPending, "a rider's block was not skipped");
                inputs.Mounted.Remove(ai.Agent.Index);
                Tick(3);
                weaponMask = OpeningMath.WeaponMask(false, true); // a pure thrust weapon - he already holds the high guard
                guards[ai.Agent.Index] = OpeningMath.GuardUp;
                Parried(you, ai, Agent.UsageDirection.AttackUp);
                Check(st.Skips[(int)OpeningSkip.NoOpeningForWeapon] == 1, "a thrust-only weapon against a high guard opened something");
                weaponMask = OpeningMath.AllAttacks;

                // ---- 9. the shadow switch and the master switch: nothing counted; a waiting opening dropped at the next tick
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
                Check(!logic.OpeningPending && !logic.OpeningChain.Active && st.Resets[(int)OpeningReset.Off] == 1, "the MASTER SWITCH off did not drop the opening and the chain");
                int b2 = st.BlocksTotal;
                Parried(you, ai, Agent.UsageDirection.AttackRight);
                Check(st.BlocksTotal == b2, "the MASTER SWITCH off still counted a block");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                Tick();

                // ---- 10. nothing applied (106a writes no engine state - the live switch ships off); no errors
                Check(!OpeningRules.From(S).Enabled, "the live openings are on by default (106a is a shadow)");
                Check(st.Errors == 0, "the openings logged errors: " + st.Errors);

                // ---- 11. the [summary] line
                string line = st.SummaryLine(OpeningRules.From(S));
                Check(line.StartsWith("openings (step 106a SHADOW - nothing applied; live OFF (OpeningsEnabled), the shadow log ON): ", StringComparison.Ordinal)
                      && line.Contains("INTO the opening 1") && line.Contains("wrong direction 1") && line.Contains("too early 1") && line.Contains("too late 1")
                      && line.Contains("AI vs AI correct blocks on foot 1"), "the [summary] line: " + line);
                Console.WriteLine("        step 106a: " + st.BlocksTotal + " blocked attacks, " + st.Planned + " openings planned");
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

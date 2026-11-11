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
    /// Offline check for step 111 - THE OPENING CHAIN MAKES SENSE (Toni's training field 2026-11-10, the veteran trainer with a shield; DESIGN decision
    /// 100), through the REAL logic and the REAL input frames: (1) Toni's chain - his shield block opens a hold, your winning thrust lowers it (GUARD
    /// BROKEN) and staggers him, the perfect-strike arrow (top) shows; your overhead begun in the reel is a FLIGHT - his guard down and 'can defend' off,
    /// the window and the arrow kept past the reel - and lands a PERFECT STRIKE after the reel; no hold from it (he is not recovered); (2) the same
    /// overhead met by his shield anyway after the reel: NO new hold (pic 4 - the block route obeys the stagger immunity), the flight over, his guard his
    /// own; (3) you reel (his riposte - pic 9): the hold on him ends, the arrow goes, his next block opens nothing; (4) a held man never kicks (step
    /// 81's roll refused); (5) the "Unbalanced" line waits while your swing is in the air; (6) the master switch releases a flight; (7) the [summary]
    /// clauses.
    /// </summary>
    internal static partial class Program
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Openings111ThroughTheLogic()
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
                S.Set(SettingsSchema.ReelingHitKnockdownEnabled, false, SettingSources.File); // the strike's damage is what is read here, not 105's fall
                var hr0 = OpeningHoldRules.From(S);
                var pr0 = PerfectStrikeRules.From(S);
                Check(hr0.On && hr0.EndsWhenYouReel && Math.Abs(hr0.ArrowSwingMax - 1.5) < 1e-6 && Math.Abs(pr0.FlightSeconds - 0.8) < 1e-6,
                    "step 111's shipped numbers: ends when you reel " + hr0.EndsWhenYouReel + ", arrow latch " + hr0.ArrowSwingMax + ", flight " + pr0.FlightSeconds);
                var inputs = new FakeImpactInputs { Damage = 20 };
                var anim = new FakeAnimBody();
                var body = new FakeOpeningBody();
                var sink = new FakeMessageSink();
                var logic = NewPaceLogic(new FakePaceBody(), new FakeWeaponFacts());
                logic.ImpactBody = new FakeImpactBody();
                logic.ImpactInputs = inputs;
                logic.GuardBody = new FakeGuardBody();
                logic.SlowMoBody = new FakeSlowMoBody();
                logic.MessageSink = sink;
                logic.AnimBody = anim;
                logic.OpeningBody = body;
                logic.ImpactDice = new ScriptDice { Rest = 0.999 }; // no impact outcome rolls up
                logic.OpeningDice = new ScriptDice { Rest = 0.999 }; // the last candidate guard; no counter-swing
                var shields = new System.Collections.Generic.Dictionary<int, double>();
                int swingDir = OpeningMath.Unknown;
                logic.OpeningGuardRead = a => -1;
                logic.OpeningSwingRead = a => swingDir;
                logic.OpeningCommitRead = a => swingDir;
                logic.OpeningWeaponRead = a => OpeningMath.AllAttacks;
                logic.OpeningWeaponSpeedRead = a => 109;
                logic.OpeningShieldLengthRead = a => shields.TryGetValue(a.Index, out double l) ? l : double.NaN;
                logic.OpeningSkillRead = a => 100;
                double t = 111000;
                logic.TickImpact(t);
                int next = 11100;
                var hs = logic.OpeningHoldStats;
                var ps = logic.PerfectStrikeStats;
                var ar = Rules;

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
                void ShieldRight(TrackedAgent a, TrackedAgent v, int d)
                {
                    swingDir = d;
                    logic.ImpactModelHitTaken(a.Agent, v.Agent, Swing((Agent.UsageDirection)d, true, true, CombatCollisionResult.Blocked));
                }
                void Lands(TrackedAgent a, TrackedAgent v, int d)
                {
                    swingDir = d;
                    logic.ImpactModelHitTaken(a.Agent, v.Agent, Swing((Agent.UsageDirection)d, false, false, CombatCollisionResult.StrikeAgent));
                }
                void WindUp(TrackedAgent you2, int dir)
                {
                    swingDir = dir;
                    logic.ObserveAction(you2, ActIdle, Now(), in ar); // a fresh wind-up each time (the last one's ready left behind)
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
                void Shows(TrackedAgent m, int guard) => body.Poses[m.AgentIndex] = new OpeningPose { Action = "act_defend_shield", Direction = guard, Stage = 3, Defending = true, Read = true };
                int Unbalanced() => sink.Lines.Count(l => l.Text.StartsWith("Unbalanced", StringComparison.Ordinal));
                void ReelsOut(TrackedAgent m)
                {
                    for (int n = 0; n < 400 && m.Impact.Reeling(Now()); n++) Tick();
                    // the game measures the reel's end and gives the 2 s immunity from it (BlockWatchMeasured); the stand-in never shows the
                    // stagger, so the watch's "never appeared" path would end it with none - put the measured end back as the game does
                    m.Impact.EndReel(Math.Min(m.Impact.ReelingUntil, Now()), ImpactRules.From(S).StaggerImmunity);
                    Tick(PerfectStrikeRules.From(S).GraceSeconds + 0.05);
                }

                var you = Man();
                body.You.Add(you.AgentIndex);
                logic.SmokePlayer = you.Agent;
                logic.SmokePlayerAiControlled = false;
                const uint Fwd = 0x1u;

                // ---- 1. TONI'S CHAIN: his shield block -> a hold; your winning strike -> GUARD BROKEN, the stagger, the perfect-strike arrow
                var v1 = Man(78);
                ShieldRight(you, v1, OpeningMath.AttackRight);
                var h = logic.OpeningHold;
                Check(h.Active && h.Target == v1.AgentIndex, "his shield block opened no hold: " + h.Phase);
                Shows(v1, h.Guard);
                Tick();
                h = logic.OpeningHold;
                int win = h.Opening;
                Check(h.Holding(v1.AgentIndex) && OpeningMath.IsAttack(win) && Arrow().Cue == ArrowCue.Hold, "no hold with its arrow: " + h.Phase);
                Tick(0.1);
                WindUp(you, win);
                Check(logic.LastHoldSwing == HoldSwing.Wins && logic.OpeningForceLowered, "the winning strike did not lower his shield");
                Tick(0.1);
                Lands(you, v1, win);
                Check(v1.Impact.Reeling(Now()) && v1.Impact.ReelCause == ReelCause.Opening && !logic.OpeningHold.Active, "the winning strike did not stagger him");
                Tick();
                var a = Arrow();
                int finish = a.Mask;
                Check(a.Cue == ArrowCue.Finish && finish != 0 && logic.PerfectStrikeWindowsOpen == 1, "no perfect-strike arrow while he reels: " + a.Cue);
                int finishDir = finish == OpeningMath.Bit(OpeningMath.AttackUp) ? OpeningMath.AttackUp : finish == OpeningMath.Bit(OpeningMath.AttackDown) ? OpeningMath.AttackDown
                              : finish == OpeningMath.Bit(OpeningMath.AttackLeft) ? OpeningMath.AttackLeft : OpeningMath.AttackRight;
                //      no hold while he reels: a block of his now opens nothing (the perfect-strike window)
                int refusedReel = hs.RefusedByBlock[(int)HoldRefusal.HeReels];
                ShieldRight(you, v1, OpeningMath.AttackLeft);
                Check(!logic.OpeningHold.Active && hs.RefusedByBlock[(int)HoldRefusal.HeReels] == refusedReel + 1, "a block during his reel opened a hold");
                LogHas("first hold REFUSED");
                //      your overhead (the shown side) begins IN the reel: the flight - his guard down, 'can defend' off
                Tick(0.2);
                WindUp(you, finishDir);
                Check(logic.PerfectStrikeFlightVictim == v1.AgentIndex && logic.OpeningForceKind == OpeningForceKind.Strike && ReferenceEquals(logic.OpeningForced, v1)
                      && body.NoDefend.Contains(v1.AgentIndex) && ps.Flights == 1,
                    "your swing begun in the reel took no flight: " + logic.OpeningForceKind + " victim " + logic.PerfectStrikeFlightVictim);
                Check(logic.OpeningArrowLatch.Active && logic.OpeningArrowLatch.Mask == finish, "the arrow was not latched on the perfect-strike side at your wind-up");
                LogHas("perfect strike: first swing begun in a window");
                ReelsOut(v1);
                Check(!v1.Impact.Reeling(Now()) && logic.PerfectStrikeWindowsOpen == 1 && Arrow().Mask == finish
                      && (Frame(v1, Fwd | OpeningForceMath.DefendUp | OpeningForceMath.DefendBlock) & OpeningForceMath.GuardBits) == 0 && body.NoDefend.Contains(v1.AgentIndex),
                    "after his reel the flight did not keep the window, the arrow and his guard down");
                // step 113: step 105 still counts him REELING for this swing (the flight's end on his record, his reel itself over)
                Check(ReelingHitMath.InStrikeFlight(Now(), v1.StrikeFlightReelUntil) && !v1.Impact.Reeling(Now()), "step 113: the flight did not keep him counted reeling for step 105");
                //      it lands after the reel: a PERFECT STRIKE, the flight over, his guard his own, NO hold (he is not recovered)
                float got = Strike(you, v1, finishDir);
                Check(got > 20f + 1e-3 && ps.FlightsLanded == 1 && ps.FlightStrikesAfterReel == 1 && ps.FlightsOutlasted == 1 && Free(v1) && logic.PerfectStrikeFlightVictim < 0,
                    "the flight's strike after the reel was not a perfect strike / not let go: " + got + " landed " + ps.FlightsLanded + " after " + ps.FlightStrikesAfterReel + " outlasted " + ps.FlightsOutlasted
                    + " free " + Free(v1) + " victim " + logic.PerfectStrikeFlightVictim + " immune " + (v1.Impact.StaggerImmuneUntil - Now()) + " hold " + logic.OpeningHold.Phase + " reel " + (v1.Impact.ReelingUntil - Now()));
                Check(!logic.OpeningHold.Active && !logic.OpeningArrowLatch.Active, "a hold started from the perfect strike / the latch kept after the contact");
                Check(ps.OpeningLandedAfterReel == 1 && ps.OpeningLandedInReel == 0 && v1.StrikeFlightReelUntil <= Now() + 1e-9,
                    "step 113: the perfect strike after the reel was not counted after it / the flight's reeling outlived the contact: " + ps.OpeningLandedAfterReel + " / " + ps.OpeningLandedInReel);
                Tick(3);

                // ---- 2. PIC 4-5: the overhead met by his shield anyway once the reel ran out - NO new hold, the arrow never turned, his guard back
                S.Set(SettingsSchema.PerfectStrikeFlightSeconds, 0.0, SettingSources.File); // the step-100g window: his shield may come up
                var v2 = Man(78);
                ShieldRight(you, v2, OpeningMath.AttackRight);
                Shows(v2, logic.OpeningHold.Guard);
                Tick();
                win = logic.OpeningHold.Opening;
                WindUp(you, win);
                Tick(0.1);
                Lands(you, v2, win);
                Tick();
                finish = Arrow().Mask;
                Tick(0.3);
                WindUp(you, OpeningMath.AttackUp);
                Check(logic.PerfectStrikeFlightVictim < 0 && logic.OpeningForced == null, "a flight with PerfectStrikeFlightSeconds 0");
                ReelsOut(v2);
                int immune = hs.RefusedByBlock[(int)HoldRefusal.HeImmune];
                ShieldRight(you, v2, OpeningMath.AttackUp); // his raised shield, right as the reel ended (immune 2 s)
                Check(!logic.OpeningHold.Active && hs.RefusedByBlock[(int)HoldRefusal.HeImmune] == immune + 1 && Free(v2), "his block inside the stagger immunity opened a hold (pic 4): immune "
                      + (v2.Impact.StaggerImmuneUntil - Now()) + " reel " + (v2.Impact.ReelingUntil - Now()) + " hold " + logic.OpeningHold.Phase + " refused " + hs.RefusedByBlock[(int)HoldRefusal.HeImmune] + " free " + Free(v2));
                Tick();
                Check(!Arrow().Shows, "an arrow after the window closed with no hold");
                LogHas("he is not recovered (inside the stagger immunity)");
                S.Set(SettingsSchema.PerfectStrikeFlightSeconds, SettingsSchema.PerfectStrikeFlightSeconds.Default, SettingSources.File);
                Tick(3);
                //      recovered (past the immunity): his block opens a hold again (step 108's rule - his guard is visible)
                ShieldRight(you, v2, OpeningMath.AttackRight);
                Check(logic.OpeningHold.Active && logic.OpeningHold.Target == v2.AgentIndex, "his block after he recovered opened no hold");
                Tick(3);

                // ---- 3. PIC 9: YOU REEL (his riposte) - the hold on him ends, the arrow goes; his next block opens nothing while you reel
                var v3 = Man(78);
                ShieldRight(you, v3, OpeningMath.AttackRight);
                Shows(v3, logic.OpeningHold.Guard);
                Tick();
                Check(logic.OpeningHold.Holding(v3.AgentIndex) && Arrow().Cue == ArrowCue.Hold, "no hold to end");
                you.Impact.Reel(Now() + 0.9, 2.0, ReelCause.Riposte);
                Tick();
                Check(!logic.OpeningHold.Active && hs.Ends[(int)HoldEnd.YouReel] == 1 && !Arrow().Shows && Free(v3), "your reel did not end the hold / the arrow");
                LogHas("first hold ENDED because YOU reel");
                int youReel = hs.RefusedByBlock[(int)HoldRefusal.YouReel];
                ShieldRight(you, v3, OpeningMath.AttackLeft);
                Check(!logic.OpeningHold.Active && hs.RefusedByBlock[(int)HoldRefusal.YouReel] == youReel + 1, "his block while you reel opened a hold (pic 10)");
                //      the switch off = step 108 (his block opens a hold while you reel)
                S.Set(SettingsSchema.OpeningHoldEndsWhenYouReel, false, SettingSources.File);
                ShieldRight(you, v3, OpeningMath.AttackLeft);
                Check(logic.OpeningHold.Active, "OpeningHoldEndsWhenYouReel off still refused the hold");
                S.Set(SettingsSchema.OpeningHoldEndsWhenYouReel, true, SettingSources.File);
                Tick(3);

                // ---- 4. PIC 10: a held man never kicks or bashes (step 81's roll refused at his wind-up)
                var v4 = Man(78);
                ShieldRight(you, v4, OpeningMath.AttackRight);
                Check(logic.OpeningHold.Active && logic.OpeningHold.Target == v4.AgentIndex, "no hold for the kick check");
                int kicks = hs.KickRollsRefused;
                logic.DecideAiMove(v4, Now(), AiKickRules.From(S), in ar);
                Check(hs.KickRollsRefused == kicks + 1 && v4.AiKickPhase == AiKickPhase.None, "a held man's kick / bash roll was not refused");
                LogHas("first kick / bash roll REFUSED for a held man");
                Tick(3);

                // ---- 5. THE WORDS WAIT for your swing in the air (the arrow may appear; it never turns)
                var v5 = Man();
                Lands(you, v5, OpeningMath.AttackRight);
                Check(logic.OpeningHold.Recovering(v5.AgentIndex), "no recovery for the words check");
                Tick(0.2);
                WindUp(you, OpeningMath.AttackLeft);
                int lines = Unbalanced(), deferred = hs.MessagesDeferred;
                Shows(v5, logic.OpeningHold.Guard);
                Tick();
                Tick();
                Check(logic.OpeningHold.Holding(v5.AgentIndex) && Arrow().Cue == ArrowCue.Hold && Unbalanced() == lines && hs.MessagesDeferred == deferred + 1,
                    "the 'Unbalanced' line did not wait for your swing (or the arrow did not appear): " + Unbalanced());
                logic.OpeningMiss(Now());
                Tick();
                Check(Unbalanced() == lines + 1 && !logic.OpeningArrowLatch.Active, "the 'Unbalanced' line did not come once your swing missed");
                Tick(3);

                // ---- 6. THE MASTER SWITCH releases a flight at once
                var v6 = Man(78);
                ShieldRight(you, v6, OpeningMath.AttackRight);
                Shows(v6, logic.OpeningHold.Guard);
                Tick();
                win = logic.OpeningHold.Opening;
                WindUp(you, win);
                Tick(0.1);
                Lands(you, v6, win);
                Tick(0.2);
                WindUp(you, OpeningMath.AttackUp);
                Check(logic.OpeningForceKind == OpeningForceKind.Strike && ReferenceEquals(logic.OpeningForced, v6), "no flight to switch off");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.File);
                Tick();
                Check(logic.OpeningForced == null && Free(v6), "the master switch did not release the flight");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                logic.OpeningMiss(Now());
                Tick(3);

                // ---- 7. no errors; the [summary] clauses
                Check(hs.Errors == 0 && logic.OpeningStats.Errors == 0 && logic.OpeningForceStats.Errors == 0 && ps.Errors == 0, "errors: " + hs.Errors + " / " + ps.Errors);
                string line = hs.SummaryLine(OpeningHoldRules.From(S));
                string pline = ps.SummaryLine(PerfectStrikeRules.From(S));
                Check(line.Contains("STEP 111 - no hold (your hit / his block)") && line.Contains("you reeled " + hs.Ends[(int)HoldEnd.YouReel]) && pline.Contains("STEP 111 - your swings begun in a window"),
                    "the step-111 [summary] parts: " + line);
                Console.WriteLine("        step 111: holds refused by his block - he reels " + hs.RefusedByBlock[(int)HoldRefusal.HeReels] + ", not recovered "
                                  + hs.RefusedByBlock[(int)HoldRefusal.HeImmune] + ", you reel " + hs.RefusedByBlock[(int)HoldRefusal.YouReel] + "; flights " + ps.Flights
                                  + " (landed " + ps.FlightsLanded + ", after the reel " + ps.FlightStrikesAfterReel + "); kick rolls refused " + hs.KickRollsRefused
                                  + "; words deferred " + hs.MessagesDeferred);
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

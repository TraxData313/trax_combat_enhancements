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
    /// Offline check for step 108 - READ THE BLOCK, NOT THE ARROW (Toni's training-field playtest 2026-11-10, DESIGN decision 97), through the REAL
    /// logic and the REAL input frames with stand-ins of the engine: (1) your hit starts a RECOVERY - his attack out and his guard pushed (a ready
    /// cancelled by the guard bit, never released), NO arrow, the clock not started - until his pose shows the guard; then the clock: (your swing
    /// 1.0 + the margin at skill 200 0.30) x the weapon factor from that moment, the arrow, "Unbalanced UP (hit from below)" in the feed; the hold
    /// runs out its length after the guard showed; (2) a guard that never shows - the cap starts the clock; (3) a hit while recovering = a fresh
    /// hold; (4) his block while recovering = a link; (5) the arrow / the words off by their switches; (6) his blows on you by phase with their
    /// lines; (7) the master switch releases a recovering man; (8) the [summary] line.
    /// </summary>
    internal static partial class Program
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Openings108ThroughTheLogic()
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
                var hr0 = OpeningHoldRules.From(S);
                Check(hr0.On && Math.Abs(hr0.RecoveryCap - 1.0) < 1e-6 && Math.Abs(hr0.SwingSeconds - 1.0) < 1e-6 && Math.Abs(hr0.MarginAt200 - 0.3) < 1e-6 && hr0.ArrowShow && hr0.Message,
                    "step 108's shipped numbers: cap " + hr0.RecoveryCap + ", swing " + hr0.SwingSeconds + ", margin " + hr0.MarginAt200);
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
                var dice = new ScriptDice { Rest = 0.0 };           // the first candidate guard (high); every counter-swing roll thrown
                logic.OpeningDice = dice;
                int swingDir = OpeningMath.Unknown;
                logic.OpeningGuardRead = a => -1;
                logic.OpeningSwingRead = a => swingDir;
                logic.OpeningCommitRead = a => swingDir;
                logic.OpeningWeaponRead = a => OpeningMath.AllAttacks;
                logic.OpeningWeaponSpeedRead = a => 95;
                logic.OpeningShieldLengthRead = a => double.NaN;
                logic.OpeningSkillRead = a => 200;
                double t = 108000;
                logic.TickImpact(t);
                int next = 10800;
                var hs = logic.OpeningHoldStats;

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
                void Lands(TrackedAgent a, TrackedAgent v, int d)
                {
                    swingDir = d;
                    logic.ImpactModelHitTaken(a.Agent, v.Agent, Swing((Agent.UsageDirection)d, false, false, CombatCollisionResult.StrikeAgent));
                }
                void Parried(TrackedAgent a, TrackedAgent v, int d) => logic.ImpactModelHitTaken(a.Agent, v.Agent, Swing((Agent.UsageDirection)d, false, false, CombatCollisionResult.Parried));
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
                void Shows(TrackedAgent m, int guard) => body.Poses[m.AgentIndex] = new OpeningPose { Action = "act_defend_up_2h_passive", Direction = guard, Stage = 3, Defending = true, Read = true };
                int Unbalanced() => sink.Lines.Count(l => l.Text.StartsWith("Unbalanced", StringComparison.Ordinal));

                var you = Man();
                body.You.Add(you.AgentIndex);
                logic.SmokePlayer = you.Agent;
                logic.SmokePlayerAiControlled = false;
                const uint Fwd = 0x1u;

                // ---- 1. THE RECOVERY: your hit - his attack out, his guard pushed, no arrow, no clock; the guard shows 0.4 s later - the clock starts
                var a1 = Man();
                Lands(you, a1, OpeningMath.AttackRight);
                var h = logic.OpeningHold;
                Check(h.Recovering(a1.AgentIndex) && h.Guard == OpeningMath.GuardUp && Math.Abs(h.Length - 1.3) < 1e-6 && logic.OpeningForceKind == OpeningForceKind.Hold
                      && ReferenceEquals(logic.OpeningForced, a1) && hs.Recoveries == 1,
                    "your hit did not start a RECOVERY toward a 1.3 s hold (swing 1.0 + margin 0.30 at skill 200): " + h.Phase + " " + h.Length);
                Check(Frame(a1, Fwd | OpeningHoldMath.AttackLeft) == (Fwd | OpeningForceMath.DefendUp),
                    "recovering: his held attack is not cancelled by the forced guard (attack out, guard in) - it must never be released");
                Check(!Arrow().Shows, "the red arrow shows before his guard does");
                LogHas("HOLDS his guard in the high guard for 1.30 s FROM THE MOMENT IT SHOWS");
                Tick(0.4);
                Check(logic.OpeningHold.Recovering(a1.AgentIndex) && !Arrow().Shows, "the hold left its recovery without his guard showing");
                Shows(a1, OpeningMath.GuardUp);
                Tick();
                h = logic.OpeningHold;
                double shownAt = Now();
                Check(h.Holding(a1.AgentIndex) && Math.Abs(h.Start - shownAt) < 1e-6 && Math.Abs(h.Until - (shownAt + 1.3)) < 1e-6 && !h.RecoveryCapped
                      && Math.Abs(h.RecoverySeconds - 0.4) < 0.04 && hs.RecoveredSeen == 1,
                    "his visible guard did not start the hold's clock: " + h.Phase + " start " + h.Start + " vs " + shownAt + ", recovery " + h.RecoverySeconds);
                var ar = Arrow();
                Check(ar.Cue == ArrowCue.Hold && ar.Mask == OpeningMath.Bit(OpeningMath.AttackDown), "no steady arrow (bottom) once his guard showed: " + ar.Cue);
                LogHas("is VISIBLE 0.4");
                Tick();
                Check(sink.Lines.Any(l => l.Text == "Unbalanced UP (hit from below)" && l.Hex == AthleticsLogic.UnbalancedHex) && hs.MessagesShown == 1,
                    "no 'Unbalanced UP (hit from below)' in the feed: " + string.Join(" | ", sink.Lines.Select(l => l.Text)));
                //      the hold runs out 1.3 s after the guard SHOWED (not after the hit); the arrow stays up to its end
                for (int n = 0; n < 400 && logic.OpeningHold.Phase == HoldPhase.Holding; n++)
                {
                    Check(Arrow().Cue == ArrowCue.Hold, "the arrow went down before the hold's end at " + (Now() - shownAt));
                    Tick();
                }
                Check(Math.Abs(Now() - (shownAt + 1.3)) < 0.05 && logic.OpeningHold.Phase == HoldPhase.After && !Arrow().Shows,
                    "the hold did not run out 1.3 s after his guard showed: " + (Now() - shownAt));
                //      his counter-swing (thrown by the dice) lands on you: counted by its phase, with its line
                Check(ReferenceEquals(logic.OpeningCounterSwinger, a1), "no counter-swing to land");
                Tick(0.5);
                Lands(a1, you, OpeningMath.AttackRight);
                Check(hs.HitYouAfterCounter == 1 && !logic.OpeningHold.Active, "his counter-swing on you was not counted 'after the hold - his forced counter-swing': " + hs.HitYouAfterCounter);
                LogHas("'s blow LANDED on you after his hold ran out - his FORCED counter-swing");
                Tick(3);

                // ---- 2. A GUARD THAT NEVER SHOWS: the cap (1.0 s) starts the clock anyway
                var a2 = Man();
                Lands(you, a2, OpeningMath.AttackRight);
                Tick(0.9);
                Check(logic.OpeningHold.Recovering(a2.AgentIndex), "the cap came early");
                Tick(0.15);
                h = logic.OpeningHold;
                Check(h.Holding(a2.AgentIndex) && h.RecoveryCapped && hs.RecoveryCapped == 1 && Arrow().Cue == ArrowCue.Hold, "the cap did not start the clock: " + h.Phase);
                LogHas("NOT seen within 1.00 s of your hit (the recovery cap)");
                //      his blow on you while he HOLDS (the forced guard on)
                Tick(0.2);
                Lands(a2, you, OpeningMath.AttackUp);
                Check(hs.HitYouHolding == 1, "his blow on you while holding was not counted");
                LogHas("while he HELD his forced guard");
                Tick(3);

                // ---- 3. A HIT WHILE RECOVERING = a fresh hold (the recovery cut, a new one starts)
                var a3 = Man();
                Lands(you, a3, OpeningMath.AttackRight);
                Tick(0.2);
                int cut = hs.RecoveryCut, holds = hs.Holds;
                Lands(you, a3, OpeningMath.AttackLeft);
                Check(hs.RecoveryCut == cut + 1 && hs.Holds == holds + 1 && logic.OpeningHold.Recovering(a3.AgentIndex), "a hit while recovering was not a fresh hold: " + hs.RecoveryCut);
                Tick(3);

                // ---- 4. HIS BLOCK WHILE RECOVERING = a link (his guard is plainly up): held from his block
                var a4 = Man();
                Lands(you, a4, OpeningMath.AttackRight);
                Tick(0.2);
                int links = hs.Links, seen = hs.RecoveredSeen;
                Parried(you, a4, OpeningMath.AttackUp);
                h = logic.OpeningHold;
                Check(h.Holding(a4.AgentIndex) && h.Links == 1 && hs.Links == links + 1 && hs.RecoveredSeen == seen + 1 && Math.Abs(h.Start - Now()) < 1e-6,
                    "his block while recovering was not a link held from his block: " + h.Phase + " " + h.Links);
                Tick(3);

                // ---- 5. THE SWITCHES: the arrow off (read the block alone), the words off
                S.Set(SettingsSchema.OpeningHoldArrowShow, false, SettingSources.File);
                S.Set(SettingsSchema.OpeningHoldMessage, false, SettingSources.File);
                var a5 = Man();
                int lines = Unbalanced();
                Lands(you, a5, OpeningMath.AttackRight);
                Shows(a5, OpeningMath.GuardUp);
                Tick();
                Tick();
                Check(logic.OpeningHold.Holding(a5.AgentIndex) && !Arrow().Shows && Unbalanced() == lines && hs.ArrowOff >= 1,
                    "the arrow / the words showed with their switches off");
                S.Set(SettingsSchema.OpeningHoldArrowShow, true, SettingSources.File);
                S.Set(SettingsSchema.OpeningHoldMessage, true, SettingSources.File);
                Check(Arrow().Cue == ArrowCue.Hold, "the arrow switched back on mid-hold did not show (read live)");
                Tick(3);

                // ---- 6. THE MASTER SWITCH releases a recovering man at once
                var a6 = Man();
                Lands(you, a6, OpeningMath.AttackRight);
                Check(logic.OpeningHold.Recovering(a6.AgentIndex), "no recovery to switch off");
                S.Set(SettingsSchema.ModEnabled, false, SettingSources.File);
                Tick();
                Check(!logic.OpeningHold.Active && Free(a6) && logic.OpeningForced == null, "the master switch did not release a recovering man");
                S.Set(SettingsSchema.ModEnabled, true, SettingSources.File);
                Tick(3);

                // ---- 7. no errors; the [summary] line
                Check(hs.Errors == 0 && logic.OpeningStats.Errors == 0 && logic.OpeningForceStats.Errors == 0, "errors: " + hs.Errors);
                string line = hs.SummaryLine(OpeningHoldRules.From(S));
                Check(line.Contains("STEP 108 - THE RECOVERY") && line.Contains("started by the cap " + hs.RecoveryCapped) && line.Contains("his forced counter-swing 1"), "the step-108 [summary] part: " + line);
                Console.WriteLine("        step 108: recoveries " + hs.Recoveries + " (seen " + hs.RecoveredSeen + ", capped " + hs.RecoveryCapped + ", cut " + hs.RecoveryCut
                                  + "), mean " + ((hs.RecoveredSeen + hs.RecoveryCapped) > 0 ? (hs.RecoverySum / (hs.RecoveredSeen + hs.RecoveryCapped)).ToString("0.00") : "-")
                                  + " s; words shown " + hs.MessagesShown + "; his blows on you holding / after a counter " + hs.HitYouHolding + " / " + hs.HitYouAfterCounter);
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

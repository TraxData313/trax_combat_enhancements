using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Models;

namespace TraxCombat.Missions
{
    /// <summary>
    /// The attack RATE (DESIGN §2 "Attack speed", step 5e; step 13 PAUSE ONLY - research and choice in
    /// AI_NOTES "Step 5e" and "Step 13"): the attack rate follows the attack multiplier m.
    ///
    /// THE TECHNIQUE (step 13, Anton's playtest call - the animation slow-down read as "slow-mo"):
    ///   the animations play at full speed (the stat decorator scales them only down to
    ///   AttackAnimationMinPercent, 100 = never); after each attack of duration D (its wind-up + release;
    ///   ranged + the reload after the loose), ending at m, the fighter may not START another attack
    ///   for D × (1/m − 1):
    ///   - AI (AttackRatePaceHold, the old "pace hold"): from the attack's end - melee and ranged, on foot
    ///     and mounted. Step 16: by INPUT (AttackRatePaceByInput - only the attack bits taken out of his own
    ///     input, his guard his own; <see cref="InputPaceBody"/>) or by NoAttack (step 13, <see cref="GamePaceBody"/>).
    ///     Asked for when the attack ends (a melee swing: after the step-back roll), started by the tick (never
    ///     inside an engine callback), lifted when its time is up - or at once when a switch goes off, he leaves,
    ///     the player takes him, an attack slips through, the mission ends. THE TIMER SURVIVES A STEP BACK (step
    ///     16, R1's old "a started step back drops the hold" is gone): by input both run at once and his attacks
    ///     stay held until the later end; a NoAttack hold behind a SCRIPTED step back (its frame owns the flag
    ///     word) is deferred and set the tick the step back ends, if time is left.
    ///   - You (AttackRatePlayerTimer): AthleticsLogic.PlayerTimer.cs + the input gate.
    ///   - AttackRateAiDecisions (off by default since step 13 - it double-counts on top of the timer):
    ///     the AI's chance to attack, to riposte and to loose × m, the aim before a shot ÷ m, in the
    ///     stat decorator.
    ///
    /// MEASURED (the playtest reads it without a second run): every action change on channel 1 closes
    /// and opens a PHASE (wind-up + held / draw + aim, release, recoil after a block, reload, the pause
    /// before the next ready), each filed under the f band at its start with the m it ran at; the
    /// animation multiplier asked per attack; each timer's D, m and pause against the measured gap from
    /// the attack's end to the next attack's start (early = started during it); the cycle release-to-
    /// release / shot-to-shot; per band and group (melee / ranged × AI / you) the target (the peak's
    /// cycle ÷ m) and measured ÷ target with a verdict word; the AI's holds; the guard by f.
    ///
    /// LOGS: [rate] mission start (the technique), the FIRST slowed fighter's properties before → after
    /// (every value a technique touches), the FIRST AI timer in full (start and end), switch changes,
    /// switched-off releases; verbose: every AI timer (bucket rate-hold); the [summary] lines of
    /// <see cref="AttackRateStats"/>.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private const int ActionReadyMelee = (int)Agent.ActionCodeType.ReadyMelee;
        private const int ActionReadyRanged = (int)Agent.ActionCodeType.ReadyRanged;
        private const int ActionParried = (int)Agent.ActionCodeType.ParriedMelee;
        private const int ActionBlocked = (int)Agent.ActionCodeType.BlockedMelee;
        private const int ActionReload = (int)Agent.ActionCodeType.Reload;

        private readonly AttackRateStats _rateStats = new AttackRateStats();
        private readonly List<TrackedAgent> _pacePending = new List<TrackedAgent>(64);
        private readonly List<TrackedAgent> _paceHeld = new List<TrackedAgent>(64);
        private readonly List<TrackedAgent> _paceDeferred = new List<TrackedAgent>(16);

        private bool _seenAiDecisions;
        private bool _seenPaceHold;
        private bool _seenPaceByInput;
        private bool _seenRaiseGuard;
        private bool _seenPlayerTimer;
        private int _seenAnimationMin;
        private bool _paceSeenOn;
        private bool _rateSwitched;
        private bool _paceClosed;
        private bool _firstHoldStarted;
        private bool _firstSlowedLogged;

        /// <summary>This mission's attack-rate numbers (the offline smoke reads them).</summary>
        internal AttackRateStats RateStats => _rateStats;

        /// <summary>The engine side of the AI timer - the offline smoke swaps in a stand-in.</summary>
        internal IPaceBody PaceBody { get; set; } = new GamePaceBody();

        /// <summary>Step 16: the engine side of the AI timer by input (AttackRatePaceByInput) - the smoke swaps it too.</summary>
        internal IPaceBody PaceInputBody { get; set; } = new InputPaceBody();

        private IPaceBody PaceBodyOf(PaceState ps) => ps.ByInput ? PaceInputBody : PaceBody;

        /// <summary>NoAttack holds waiting for a scripted step back to end (step 16; the smoke checks it).</summary>
        internal int DeferredNow => _paceDeferred.Count;

        /// <summary>AI timers running now (not those waiting for a game job).</summary>
        internal int HeldNow
        {
            get
            {
                int n = 0;
                foreach (var st in _paceHeld)
                    if (st.Pace != null && st.Pace.Active) n++;
                return n;
            }
        }

        private void StartAttackRate()
        {
            var s = TraxSettings.Shared;
            var rr = AttackRateRules.From(s);
            _seenAiDecisions = s.AttackRateAiDecisions;
            _seenPaceHold = s.AttackRatePaceHold;
            _seenPaceByInput = s.AttackRatePaceByInput;
            _seenRaiseGuard = s.AiHoldRaiseGuard;
            _seenPlayerTimer = s.AttackRatePlayerTimer;
            _seenAnimationMin = s.AttackAnimationMinPercent;
            _paceSeenOn = rr.PaceOn;
            TraxLog.Info("rate", "mission start: " + rr.Describe() + " - read live; the [summary] \"attack rate\" lines measure every phase, each timer against the gap it left, and the cycle by f");
        }

        // ------------------------------------------------------------------ the phases

        /// <summary>The phase an action code belongs to (-1 = none: the pause) and whether it is melee or ranged.</summary>
        private static int PhaseOfAction(int action, out AttackKind kind)
        {
            switch (action)
            {
                case ActionReadyMelee:
                    kind = AttackKind.Melee;
                    return (int)AttackPhase.WindUp;
                case ActionReadyRanged:
                    kind = AttackKind.Ranged;
                    return (int)AttackPhase.WindUp;
                case ActionReleaseMelee:
                    kind = AttackKind.Melee;
                    return (int)AttackPhase.Release;
                case ActionReleaseRanged:
                case ActionReleaseThrowing:
                    kind = AttackKind.Ranged;
                    return (int)AttackPhase.Release;
                case ActionBlocked:
                case ActionParried:
                    kind = AttackKind.Melee;
                    return (int)AttackPhase.Recoil;
                case ActionReload:
                    kind = AttackKind.Ranged;
                    return (int)AttackPhase.Reload;
                default:
                    kind = AttackKind.Melee;
                    return -1;
            }
        }

        /// <summary>A ready or a release, melee or ranged - an attack under way.</summary>
        private static bool IsAttackAction(int action) =>
            action == ActionReadyMelee || action == ActionReadyRanged || action == ActionReleaseMelee || action == ActionReleaseRanged || action == ActionReleaseThrowing;

        private static bool IsReadyAction(int action) => action == ActionReadyMelee || action == ActionReadyRanged;

        /// <summary>
        /// The channel-1 action changed (poll or hit): close the running phase (filed under the band at
        /// its START, with the m it ran at), note the pause between attacks, open the new phase. Step 13:
        /// the attack's D is built here (its wind-up + release, ranged + the reload after the loose) and
        /// the moment an attack ENDS is flagged (<see cref="TrackedAgent.AttackEndedNow"/> - the timer's
        /// decision follows in ObserveAction); an attack's START is the gap measurement after the last
        /// timer. Runs BEFORE this action's own charge, so a release is filed at the band it began in.
        /// May run inside an engine hit callback: managed reads only.
        /// </summary>
        private void PhasesOnAction(TrackedAgent st, int action, double now, in AthleticsRules r)
        {
            try
            {
                if (!r.Enabled) return;
                bool player = IsPlayer(st);
                int bin = AthleticsMath.PeakBin(AthleticsMath.PeakShare(in r, st));
                float m = st.SpeedMultiplier;
                int next = PhaseOfAction(action, out var kind);

                int running = st.PhaseKind;
                if (running >= 0)
                {
                    double d = now - st.PhaseStart;
                    var k = st.PhaseAttack;
                    switch ((AttackPhase)running)
                    {
                        case AttackPhase.WindUp: // a ready: wind-up, then the hold / aim
                            if (next == (int)AttackPhase.Release)
                            {
                                double wind = st.ReadyFullAt >= st.PhaseStart ? st.ReadyFullAt - st.PhaseStart : d;
                                _rateStats.AddPhase(k, player, st.PhaseBin, AttackPhase.WindUp, wind, st.PhaseAsked);
                                _rateStats.AddPhase(k, player, st.PhaseBin, AttackPhase.Held, d - wind, st.PhaseAsked);
                                st.CurrentWindUp = wind;       // D's first part: the wind-up, never the held part
                                st.AttackDuration = wind;
                                st.AttackDurationKnown = true;
                            }
                            else
                            {
                                _rateStats.AddReadyCancelled(k, player);
                            }
                            break;
                        case AttackPhase.Release:
                            _rateStats.AddPhase(k, player, st.PhaseBin, AttackPhase.Release, d, st.PhaseAsked);
                            if (k == AttackKind.Melee && !st.HitThisRelease) _rateStats.AddPhase(k, player, st.PhaseBin, AttackPhase.CleanRelease, d, st.PhaseAsked);
                            st.AttackDuration += d;
                            // melee: the attack ends with its release (a block recoil plays inside the timer);
                            // ranged: with the reload that follows the loose, if one does
                            if (k == AttackKind.Ranged && next == (int)AttackPhase.Reload) st.AwaitReloadEnd = true;
                            else AttackEndsHere(st, k);
                            break;
                        case AttackPhase.Recoil:
                            _rateStats.AddPhase(k, player, st.PhaseBin, AttackPhase.Recoil, d, st.PhaseAsked);
                            break;
                        case AttackPhase.Reload:
                            _rateStats.AddPhase(k, player, st.PhaseBin, AttackPhase.Reload, d, st.PhaseAsked);
                            if (st.AwaitReloadEnd)
                            {
                                st.AwaitReloadEnd = false;
                                st.AttackDuration += d;
                                AttackEndsHere(st, AttackKind.Ranged);
                            }
                            break;
                    }
                }

                // the pause between attacks: from the end of an attack (its release, recoil or reload)
                // to the next ready
                bool attackEnded = running == (int)AttackPhase.Release || running == (int)AttackPhase.Recoil || running == (int)AttackPhase.Reload;
                if (next == (int)AttackPhase.WindUp)
                {
                    if (attackEnded)
                    {
                        _rateStats.AddChained(kind, player);
                        _rateStats.AddPhase(kind, player, bin, AttackPhase.Pause, 0, m);
                    }
                    else if (st.PauseFrom >= 0 && !st.SteppedBackThisCycle) // a step back's pause is not the attack rhythm
                    {
                        _rateStats.AddPhase(st.PauseAttack, player, st.PauseBin, AttackPhase.Pause, now - st.PauseFrom, st.PauseAsked);
                    }
                    st.PauseFrom = -1;
                    AttackBegan(st, player, now, isReady: true);
                }
                else if (next < 0)
                {
                    if (attackEnded)
                    {
                        st.PauseFrom = now;
                        st.PauseAttack = st.PhaseAttack;
                        st.PauseBin = bin;
                        st.PauseAsked = m;
                    }
                }
                else if (next == (int)AttackPhase.Release)
                {
                    st.PauseFrom = -1; // a release without a ready the poll saw: this pause cannot be measured
                    if (running != (int)AttackPhase.WindUp)
                    {
                        // no ready seen: D is its release alone (not measured - no AI timer from it)
                        st.CurrentWindUp = 0;
                        st.AttackDuration = 0;
                        st.AttackDurationKnown = false;
                        AttackBegan(st, player, now, isReady: false);
                    }
                    // the animation it plays at: the multiplier in effect now, before this attack's charge
                    _rateStats.AddAnimation(kind, player, bin, AttackTimerMath.AnimationMultiplier(m, TraxSettings.Shared.AttackAnimationMinPercent));
                }

                if (next >= 0)
                {
                    st.PhaseKind = next;
                    st.PhaseAttack = kind;
                    st.PhaseStart = now;
                    st.PhaseBin = bin;
                    st.PhaseAsked = m;
                    st.ReadyFullAt = -1;
                    st.ReadyPolling = next == (int)AttackPhase.WindUp;
                }
                else
                {
                    st.PhaseKind = -1;
                    st.ReadyPolling = false;
                }
            }
            catch (Exception e)
            {
                Failed("rate.phase", e);
            }
        }

        /// <summary>An attack ended at this action change: its D is handed to the timer's decision
        /// (ObserveAction runs it right after the phases - a melee swing after its step-back roll).</summary>
        private static void AttackEndsHere(TrackedAgent st, AttackKind kind)
        {
            st.AttackEndedNow = true;
            st.EndedKind = kind;
            st.EndedDuration = st.AttackDuration;
            st.EndedDurationKnown = st.AttackDurationKnown;
            st.AttackDuration = 0;
            st.AttackDurationKnown = false;
        }

        /// <summary>
        /// An attack began (a ready; or a release no ready was seen for): the gap since the last timer's
        /// start (the last attack's end) is measured against the pause it asked; an AI hold still on him
        /// was slipped through (the tick lifts it); your hold still on - the gate missed it.
        /// </summary>
        private void AttackBegan(TrackedAgent st, bool player, double now, bool isReady)
        {
            if (st.TimerPending)
            {
                st.TimerPending = false;
                _rateStats.AddNextAttackAfterTimer(st.TimerKind, player, st.TimerBin, now - st.TimerStart, st.TimerAsked, st.TimerM);
            }
            if (player)
            {
                PlayerAttackBegan(st, now);
                return;
            }
            PaceAttackStarted(st);
            if (isReady) PaceReadyStarted(st, now);
        }

        /// <summary>A fighter in an unfinished ready (the tick, native): has the wind-up reached its end?
        /// From then on the ready is the hold / aim.</summary>
        private static void PollReady(TrackedAgent st, Agent a, double now)
        {
            if (a.GetCurrentActionProgress(1) < AttackRateMath.ReadyFullProgress) return;
            st.ReadyFullAt = now;
            st.ReadyPolling = false;
        }

        /// <summary>The wind-up reached its end (the smoke plays what the tick's poll sees in game).</summary>
        internal static void ReadyFull(TrackedAgent st, double now)
        {
            if (!st.ReadyPolling) return;
            st.ReadyFullAt = now;
            st.ReadyPolling = false;
        }

        /// <summary>Athletics (or the mod) switched: nothing measured may span the time it was off.</summary>
        private static void ResetPhases(TrackedAgent st)
        {
            st.PhaseKind = -1;
            st.ReadyPolling = false;
            st.PauseFrom = -1;
            st.LastReleaseTime = -1;
            st.LastShotForInterval = -1;
            st.AttackDuration = 0;
            st.AttackDurationKnown = false;
            st.AwaitReloadEnd = false;
            st.AttackEndedNow = false;
            st.TimerPending = false;
        }

        /// <summary>
        /// One cycle (release to release, shot to shot), classified by the band after the first
        /// attack's charge; the second one must be in the same band (else "mixed").
        /// </summary>
        private void NoteCycle(TrackedAgent st, AttackKind kind, int binNow, int binAfterLast, double seconds, float asked)
        {
            bool player = IsPlayer(st);
            if (binNow != binAfterLast)
            {
                _rateStats.AddMixed(kind, player);
                return;
            }
            // step 16: the AI timer survives a step back, so a cycle with one inside is still his attack rhythm -
            // counted in its band (and shown apart); before step 16 it was left out (the step back dropped the hold)
            _rateStats.AddCycle(kind, player, binNow, seconds, asked, steppedBack: kind == AttackKind.Melee && st.SteppedBackThisCycle);
        }

        // ------------------------------------------------------------------ the attack's end: the timer

        /// <summary>
        /// An attack ENDED (flagged by the phases; a melee swing after its step-back roll): its no-attack
        /// timer, D × (1/m − 1) with m the exact curve value now - yours (the input gate) or an AI
        /// fighter's (NoAttack, queued for the tick). <paramref name="next"/> = the action he went into.
        /// </summary>
        private void AttackEnded(TrackedAgent st, double now, int next, in AthleticsRules r)
        {
            try
            {
                var kind = st.EndedKind;
                double d = st.EndedDuration;
                bool known = st.EndedDurationKnown;
                float m = AthleticsMath.AttackSpeedMultiplier(in r, st);
                int bin = AthleticsMath.PeakBin(AthleticsMath.PeakShare(in r, st));
                if (known)
                {
                    double rest = d - st.CurrentWindUp;
                    if (rest > 0)
                    {
                        if (kind == AttackKind.Melee) st.LastRestMelee = rest;
                        else st.LastRestRanged = rest;
                    }
                }
                if (IsPlayer(st)) PlayerAttackEnded(st, now, kind, d, m, bin, next);
                else AiAttackEnded(st, now, kind, d, known, m, bin, next);
            }
            catch (Exception e)
            {
                Failed("rate.attack-end", e);
            }
        }

        /// <summary>An attack started while he is held: it slipped through NoAttack (counted; the tick
        /// lifts the hold); a hold still queued is moot.</summary>
        private void PaceAttackStarted(TrackedAgent st)
        {
            // step 16: an AI attack that began while a hold or a step back (with its attacks held) had him
            var sbh = st.StepBack;
            if ((st.Pace != null && st.Pace.Active) || (sbh != null && sbh.Active && sbh.HoldAttacks)) _holdStats.AttacksWhileHeld++;
            var ps = st.Pace;
            if (ps == null) return;
            if (ps.Deferred)
            {
                // he attacked behind the scripted step back: this deferred hold is moot (his attack's end asks a new one)
                ps.Deferred = false;
                _paceDeferred.Remove(st);
                _rateStats.AddRefused(PaceRefusal.TooLate);
            }
            if (ps.Pending)
            {
                ps.Pending = false;
                _rateStats.AddRefused(PaceRefusal.TooLate);
            }
            if (ps.Active && !ps.EndAsked)
            {
                ps.EndAsked = true;
                ps.EndReason = PaceEnd.AttackStarted;
            }
        }

        /// <summary>
        /// A tired AI fighter's attack ended (melee after the step-back roll; ranged after its reload):
        /// his timer - NoAttack until the attack's end + D × (1/m − 1) - queued for the next tick.
        /// Melee AND ranged, on foot AND mounted (step 13). May run inside an engine hit callback:
        /// managed reads only.
        /// </summary>
        private void AiAttackEnded(TrackedAgent st, double now, AttackKind kind, double d, bool known, float m, int bin, int next)
        {
            try
            {
                var rr = AttackRateRules.From(TraxSettings.Shared);
                if (!rr.PaceOn || _paceClosed) return;
                if (m >= 1f)
                {
                    _rateStats.AddNotHeld(PaceNotHeld.FullStrength);
                    return;
                }
                // Step 16: a step back - running, or asked for by this very swing - never skips the timer any more
                // (review 10a R1's "a started step back drops the hold" is gone: the timer SURVIVES the step back;
                // TickPace starts it at once by input, or defers a NoAttack hold behind a scripted step).
                var ps = st.Pace;
                if (ps != null && (ps.Pending || ps.Active)) return; // an attack slipped through a hold: the tick is lifting it
                if (ps != null && ps.Deferred)
                {
                    // a newer attack's timer supersedes a deferred one
                    ps.Deferred = false;
                    _paceDeferred.Remove(st);
                }
                if (IsAttackAction(next))
                {
                    _rateStats.AddNotHeld(PaceNotHeld.AlreadyReadied);
                    return;
                }
                if (!known)
                {
                    _rateStats.AddNotHeld(PaceNotHeld.NoDuration);
                    return;
                }
                double pause = AttackTimerMath.Pause(d, m);
                if (!AttackTimerMath.Worth(pause))
                {
                    _rateStats.AddNotHeld(PaceNotHeld.NotNeeded);
                    return;
                }
                ps ??= st.Pace = new PaceState();
                ps.Pending = true;
                ps.PendingAt = now;
                ps.Until = now + pause;
                ps.Bin = bin;
                ps.Asked = m;
                ps.Duration = d;
                ps.Kind = kind;
                ps.AttackEnd = now;
                ps.Pause = pause;
                ps.EndAsked = false;
                _pacePending.Add(st);
            }
            catch (Exception e)
            {
                Failed("rate.pace-ask", e);
            }
        }

        /// <summary>His ready began: how long after his last hold ended (the AI's own re-decision once NoAttack lifts).</summary>
        private void PaceReadyStarted(TrackedAgent st, double now)
        {
            var ps = st.Pace;
            if (ps == null || ps.EndedAt < 0) return;
            _rateStats.AddNextReadyAfterHold(now - ps.EndedAt);
            ps.EndedAt = -1;
        }

        /// <summary>
        /// Every tick, whatever the switches (so switching off lifts every hold at once): the running
        /// holds (asked to end, the player took him, time up), the ones waiting for a game job, then the
        /// queued starts. Internal: the offline smoke drives it with its stand-in body.
        /// </summary>
        internal void TickPace(double now)
        {
            var rr = AttackRateRules.From(TraxSettings.Shared);
            if (rr.PaceOn != _paceSeenOn)
            {
                _paceSeenOn = rr.PaceOn;
                if (!rr.PaceOn)
                {
                    int n = HeldNow;
                    for (int i = _paceHeld.Count - 1; i >= 0; i--)
                    {
                        if (i >= _paceHeld.Count) continue;
                        var st = _paceHeld[i];
                        try
                        {
                            if (st.Pace != null && st.Pace.Active) EndHold(st, st.Pace, PaceEnd.SwitchedOff, now, native: true);
                        }
                        catch (Exception e)
                        {
                            Failed("rate.pace-off", e);
                            SafeEndHold(st, now);
                        }
                    }
                    DropPacePending();
                    DropPaceDeferred(PaceRefusal.Gone);
                    TraxLog.Info("rate", (rr.PaceOffBecause ?? "AttackRatePaceHold") + " switched OFF mid-mission at " + Sec(now) + " s: "
                        + n + " held fighters may attack again at once");
                }
                else
                {
                    TraxLog.Info("rate", "the AI timer is ON again at " + Sec(now) + " s: tired AI fighters wait out their pause after each attack");
                }
            }

            for (int i = _paceHeld.Count - 1; i >= 0; i--)
            {
                if (i >= _paceHeld.Count) continue;
                var st = _paceHeld[i];
                var ps = st.Pace!;
                try
                {
                    if (ps.Waiting)
                    {
                        if (now >= ps.NextCheck) Lift(st, ps, now, native: true, waitingPass: true);
                        continue;
                    }
                    if (ps.EndAsked)
                    {
                        EndHold(st, ps, ps.EndReason, now, native: true);
                        continue;
                    }
                    if (PaceBodyOf(ps).MustEnd(st, out var why))
                    {
                        EndHold(st, ps, why, now, native: true);
                        continue;
                    }
                    if (now >= ps.Until) EndHold(st, ps, PaceEnd.TimeUp, now, native: true);
                }
                catch (Exception e)
                {
                    Failed("rate.pace-tick", e);
                    SafeEndHold(st, now);
                }
            }

            int budget = AttackRateMath.MaxHoldStartsPerTick;
            if (_paceDeferred.Count > 0) budget = TickDeferred(now, in rr, budget);

            if (_pacePending.Count == 0) return;
            for (int i = 0; i < _pacePending.Count; i++)
            {
                var st = _pacePending[i];
                var ps = st.Pace!;
                if (!ps.Pending) continue; // cancelled by an attack meanwhile (counted there)
                ps.Pending = false;
                if (st.Removed || _paceClosed || !rr.PaceOn)
                {
                    RefuseHold(st, ps, PaceRefusal.Gone);
                    continue;
                }
                // Step 16: the timer survives the step back (TickStepBacks ran first this tick). By input it starts
                // now, whatever the step back does - one component holds the attack bits until the later end. A
                // NoAttack hold cannot go on a man under OUR scripted frame (the frame owns the flag word): it waits
                // for the step back to end. (R1: a hold is never skipped for a step back, pending or running.)
                var sb = st.StepBack;
                if (!rr.PaceByInput && sb != null && sb.Active && !sb.ByInput)
                {
                    ps.Deferred = true;
                    _paceDeferred.Add(st);
                    _holdStats.Deferred++;
                    continue;
                }
                // too late: the time is (nearly) up, or his next attack began since this one ended (the
                // poll ran before this in the same tick) - NoAttack must never land on a readied blow
                if (ps.Until - now < AttackTimerMath.MinTimerSeconds || IsAttackAction(st.PrevAction))
                {
                    RefuseHold(st, ps, PaceRefusal.TooLate);
                    continue;
                }
                if (budget <= 0)
                {
                    RefuseHold(st, ps, PaceRefusal.TickBudget);
                    continue;
                }
                budget--;
                StartHold(st, ps, now, rr.PaceByInput);
            }
            _pacePending.Clear();
        }

        /// <summary>
        /// Step 16: the NoAttack holds waiting behind a scripted step back - set the tick the step back ends (this tick's
        /// TickStepBacks ran first, so no frame is lost) if at least 0.1 s of the pause is left; else the step back
        /// covered the whole pause (counted). Gone, switched off or his attack begun since = refused.
        /// </summary>
        private int TickDeferred(double now, in AttackRateRules rr, int budget)
        {
            for (int i = _paceDeferred.Count - 1; i >= 0; i--)
            {
                if (i >= _paceDeferred.Count) continue;
                var st = _paceDeferred[i];
                var ps = st.Pace!;
                var sb = st.StepBack;
                if (sb != null && sb.Active && !sb.ByInput && !st.Removed && !_paceClosed && rr.PaceOn) continue; // still stepping back
                _paceDeferred.RemoveAt(i);
                ps.Deferred = false;
                if (st.Removed || _paceClosed || !rr.PaceOn)
                {
                    RefuseHold(st, ps, PaceRefusal.Gone);
                    continue;
                }
                if (IsAttackAction(st.PrevAction))
                {
                    RefuseHold(st, ps, PaceRefusal.TooLate);
                    continue;
                }
                if (!AiInputMath.DeferredStillWorth(ps.Until, now))
                {
                    _rateStats.AddNotHeld(PaceNotHeld.CoveredByStepBack);
                    _holdStats.DeferredCovered++;
                    continue;
                }
                if (budget <= 0)
                {
                    RefuseHold(st, ps, PaceRefusal.TickBudget);
                    continue;
                }
                budget--;
                _holdStats.DeferredStarted++;
                ps.Overlapped = true;
                StartHold(st, ps, now, byInput: false);
            }
            return budget;
        }

        private void DropPaceDeferred(PaceRefusal why)
        {
            for (int i = 0; i < _paceDeferred.Count; i++)
            {
                var ps = _paceDeferred[i].Pace;
                if (ps == null || !ps.Deferred) continue;
                ps.Deferred = false;
                RefuseHold(_paceDeferred[i], ps, why);
            }
            _paceDeferred.Clear();
        }

        private void StartHold(TrackedAgent st, PaceState ps, double now, bool byInput)
        {
            bool held;
            PaceRefusal why;
            // step 16: the technique is read at the START; a running hold keeps the one it began with. A NoAttack of his
            // last hold still WAITING for a game job to end keeps the technique too (the waiting pass must clear it -
            // a flag nobody tracks would hold him forever)
            if (ps.Waiting) byInput = ps.ByInput;
            ps.ByInput = byInput;
            var body = PaceBodyOf(ps);
            if (byInput) EnsureInput(st);
            try
            {
                held = body.Start(st, ps, out why);
            }
            catch (Exception e)
            {
                Failed("rate.pace-start", e);
                // whatever the engine got, take it back (a NoAttack nobody tracks would never end)
                try { body.Release(st, ps, evenUnderAFrame: false); } catch { /* already failing - logged above */ }
                RefuseHold(st, ps, PaceRefusal.Error);
                return;
            }
            if (!held)
            {
                RefuseHold(st, ps, why);
                return;
            }
            if (byInput)
            {
                var s = st.Input!;
                AiInputHook.SetHold(st, true, now);
                ps.CallsAtStart = s.Calls;
                NoteHooked(st, ps.Hook);
            }
            var sbNow = st.StepBack;
            if (sbNow != null && sbNow.Active) ps.Overlapped = true; // the timer survives the step back running now
            ps.HitsTaken = ps.HitsBlocked = 0;
            // his last hold may still be waiting for a game job - then he is already in the list
            // (review 10a R2: listed twice, the tick would end this hold twice and count it twice)
            bool listed = ps.Waiting;
            ps.Active = true;
            ps.StartedAt = now;
            ps.EndAsked = false;
            ps.Waiting = false;
            ps.First = !_firstHoldStarted;
            _firstHoldStarted = true;
            if (ps.First && byInput)
            {
                var s = st.Input!;
                s.Capture = true;
                s.CapturedFirst = s.CapturedAttack = false;
            }
            if (!listed) _paceHeld.Add(st);
            _rateStats.AddHoldStart(ps.Bin, ps.Asked, ps.Kind, st.Agent.MountAgent != null, byInput);
            _rateStats.AddTimer(ps.Kind, false, ps.Bin, ps.Duration, ps.Asked, ps.Pause);
            // the gap to his next attack is measured from the attack's end, where the timer began
            st.TimerPending = true;
            st.TimerStart = ps.AttackEnd;
            st.TimerAsked = ps.Pause;
            st.TimerM = ps.Asked;
            st.TimerBin = ps.Bin;
            st.TimerKind = ps.Kind;
            if (ps.First) LogHold(st, ps, now, first: true);
            else if (TraxLog.VerboseWants("rate-hold")) LogHold(st, ps, now, first: false);
        }

        private void RefuseHold(TrackedAgent st, PaceState ps, PaceRefusal why)
        {
            ps.Pending = false;
            _rateStats.AddRefused(why);
            if (TraxLog.VerboseWants("rate-hold"))
                TraxLog.Verbose("rate", "AI timer not started: " + Name(st) + " (m " + F2(ps.Asked) + ", " + F2(ps.Pause) + " s asked) - " + why, "rate-hold");
        }

        private void DropPacePending()
        {
            for (int i = 0; i < _pacePending.Count; i++)
            {
                var ps = _pacePending[i].Pace;
                if (ps != null && ps.Pending) RefuseHold(_pacePending[i], ps, PaceRefusal.Gone);
            }
            _pacePending.Clear();
        }

        /// <summary>Ends a running hold and lifts our NoAttack (<paramref name="native"/> false: he left the
        /// field or the agents are gone - no engine call).</summary>
        private void EndHold(TrackedAgent st, PaceState ps, PaceEnd why, double now, bool native)
        {
            ps.Active = false;
            ps.EndAsked = false;
            double held = now - ps.StartedAt;
            _rateStats.AddHoldEnd(why, held);
            if (ps.Overlapped) _holdStats.HoldsOverlappingAStep++;
            ps.Overlapped = false;
            if (ps.ByInput)
            {
                // step 16: the wish goes off on EVERY path (managed only) - the callback writes nothing from the next frame
                AiInputHook.SetHold(st, false, now);
                var s = st.Input;
                if (s != null)
                {
                    if (s.Calls == ps.CallsAtStart && held >= NoCallGraceSeconds && why != PaceEnd.LeftField)
                    {
                        _holdStats.HoldsWithoutACall++;
                        if (_holdStats.HoldsWithoutACall == 1)
                            TraxLog.Info("rate", "WARNING: " + Name(st) + " was held by input for " + F2(held) + " s and the engine never called our input hook - "
                                + "the new way may not work in this game: switch \"Tired AI keep their guard up\" (AttackRatePaceByInput) off and tell Claude");
                    }
                    DrainInputError(st);
                    if (ps.First) s.Capture = false; // (the end line below reads what was captured)
                }
            }
            ps.EndedAt = why == PaceEnd.TimeUp ? now : -1;
            Lift(st, ps, now, native, waitingPass: false);
            if (ps.First) LogHoldEnd(st, ps, why, now, held);
            else if (TraxLog.VerboseWants("rate-hold"))
                TraxLog.Verbose("rate", "AI timer ended: " + Name(st) + " after " + F2(held) + " s - " + why, "rate-hold");
        }

        /// <summary>Takes our NoAttack off - or, with a game job on him, waits (re-checked every
        /// <see cref="AttackRateMath.WaitingCheckSeconds"/>) and never touches the job's flags.</summary>
        private void Lift(TrackedAgent st, PaceState ps, double now, bool native, bool waitingPass)
        {
            bool longFrame = waitingPass && ps.Waiting && now - ps.WaitingSince >= AttackRateMath.WaitingMaxSecondsUnderAFrame;
            var rel = native && !st.Removed ? PaceBodyOf(ps).Release(st, ps, longFrame) : PaceRelease.Gone;
            if (!waitingPass) _rateStats.AddRelease(rel);
            if (rel == PaceRelease.Waiting)
            {
                if (!ps.Waiting) ps.WaitingSince = now;
                ps.Waiting = true;
                ps.NextCheck = now + AttackRateMath.WaitingCheckSeconds;
                return;
            }
            if (waitingPass && rel == PaceRelease.ClearedByUs)
            {
                _rateStats.ClearedAfterWaiting++;
                if (longFrame) _rateStats.ClearedUnderAFrame++;
            }
            ps.Waiting = false;
            _paceHeld.Remove(st);
        }

        private void SafeEndHold(TrackedAgent st, double now)
        {
            try
            {
                var ps = st.Pace;
                if (ps == null) return;
                if (ps.Active) EndHold(st, ps, PaceEnd.Error, now, native: true);
                else Lift(st, ps, now, native: true, waitingPass: true);
            }
            catch (Exception e)
            {
                Failed("rate.pace-release", e);
                _paceHeld.Remove(st);
                try
                {
                    if (st.Input != null && st.Input.HoldAttacks) AiInputHook.SetHold(st, false, now);
                }
                catch
                {
                    // the record is gone; the next hold rewrites the wish
                }
            }
        }

        /// <summary>He left the field: his hold ends without an engine call.</summary>
        private void PaceLeftField(TrackedAgent st)
        {
            var ps = st.Pace;
            if (ps == null) return;
            if (ps.Deferred)
            {
                ps.Deferred = false;
                _paceDeferred.Remove(st);
            }
            if (ps.Active) EndHold(st, ps, PaceEnd.LeftField, SafeNow(), native: false);
            else if (ps.Waiting)
            {
                ps.Waiting = false;
                _paceHeld.Remove(st);
            }
        }

        /// <summary>The mission is over (before the summary): every hold ends - through the engine while
        /// the agents live - and no new one starts; your timer is released too.</summary>
        private void ClosePace(bool agentsAlive)
        {
            if (_paceClosed) return;
            _paceClosed = true;
            double now = SafeNow();
            for (int i = _paceHeld.Count - 1; i >= 0; i--)
            {
                if (i >= _paceHeld.Count) continue;
                var st = _paceHeld[i];
                var ps = st.Pace!;
                try
                {
                    if (ps.Active)
                    {
                        _rateStats.HeldAtMissionEnd++;
                        EndHold(st, ps, PaceEnd.MissionEnd, now, native: agentsAlive);
                    }
                    else if (agentsAlive)
                    {
                        Lift(st, ps, now, native: true, waitingPass: true);
                    }
                }
                catch (Exception e)
                {
                    Failed("rate.pace-close", e);
                }
            }
            _paceHeld.Clear();
            DropPacePending();
            DropPaceDeferred(PaceRefusal.Gone);
            ClosePlayerTimer(now);
        }

        // ------------------------------------------------------------------ the guard, the settings

        /// <summary>A melee collision on a tracked fighter on foot (OnMeleeHit): blocked or landed, by his
        /// f and whether an AI timer is on him - "tired men must not block less".</summary>
        private void RateHitTaken(Agent? victim, bool isCanceled, in AttackCollisionData cd)
        {
            if (victim == null || isCanceled || cd.IsHorseCharge) return;
            var vst = Get(victim);
            if (vst == null || victim.MountAgent != null) return;
            var r = Rules;
            if (!r.Enabled) return;
            var result = cd.CollisionResult;
            bool blocked = cd.AttackBlockedWithShield || result == CombatCollisionResult.Blocked || result == CombatCollisionResult.Parried
                           || result == CombatCollisionResult.ChamberBlocked;
            bool held = vst.Pace != null && vst.Pace.Active;
            _rateStats.AddHitTaken(AthleticsMath.PeakBin(AthleticsMath.PeakShare(in r, vst)), held, blocked);
        }

        /// <summary>
        /// A settings change (from <see cref="ApplySettingsChange"/>): AttackRateAiDecisions or
        /// AttackAnimationMinPercent changed → every tired fighter is recomputed over the next ticks (the
        /// decorator reads both live on each recompute; the budget spreads them); every attack-rate switch
        /// is logged and the summary is told the battle mixed both settings.
        /// </summary>
        private void NoteRateSettings(TraxSettings s)
        {
            bool ai = s.AttackRateAiDecisions != _seenAiDecisions;
            bool anim = s.AttackAnimationMinPercent != _seenAnimationMin;
            if (ai || anim)
            {
                int n = 0;
                for (int i = 0; i < _count; i++)
                {
                    var st = _dense[i];
                    if (st.SpeedMultiplier >= 1f) continue;
                    st.SpeedDirty = true;
                    n++;
                }
                if (ai)
                {
                    _seenAiDecisions = s.AttackRateAiDecisions;
                    _rateSwitched = true;
                    TraxLog.Info("rate", "AttackRateAiDecisions switched " + (s.AttackRateAiDecisions ? "ON" : "OFF") + " mid-mission at " + Sec(SafeNow()) + " s: "
                        + n + " tired fighters get their AI attack values " + (s.AttackRateAiDecisions ? "scaled by their attack speed" : "back") + " over the next ticks (at most "
                        + MaxRecomputesPerTick + " recomputes a tick)");
                }
                if (anim)
                {
                    _seenAnimationMin = s.AttackAnimationMinPercent;
                    _rateSwitched = true;
                    TraxLog.Info("rate", "AttackAnimationMinPercent now " + s.AttackAnimationMinPercent + " at " + Sec(SafeNow()) + " s: " + n
                        + " tired fighters get their attack animations at x max(m, " + F2(s.AttackAnimationMinPercent / 100f) + ") over the next ticks (at most "
                        + MaxRecomputesPerTick + " recomputes a tick)" + (s.AttackAnimationMinPercent >= 100 ? " - full speed, the timer alone slows them" : " - a little slow-mo on top of the timer"));
                }
            }
            if (s.AttackRatePaceHold != _seenPaceHold)
            {
                _seenPaceHold = s.AttackRatePaceHold;
                _rateSwitched = true;
            }
            if (s.AttackRatePaceByInput != _seenPaceByInput)
            {
                _seenPaceByInput = s.AttackRatePaceByInput;
                _rateSwitched = true;
                _holdStats.TimerSwitched = true;
                TraxLog.Info("rate", "AttackRatePaceByInput switched " + (s.AttackRatePaceByInput ? "ON" : "OFF") + " mid-mission at " + Sec(SafeNow()) + " s: new AI timers use "
                    + AiInputMath.TimerTechnique(s.AttackRatePaceByInput) + "; the " + HeldNow + " running now finish the way they began");
            }
            if (s.AiHoldRaiseGuard != _seenRaiseGuard)
            {
                _seenRaiseGuard = s.AiHoldRaiseGuard;
                TraxLog.Info("rate", "AiHoldRaiseGuard switched " + (s.AiHoldRaiseGuard ? "ON" : "OFF") + " mid-mission at " + Sec(SafeNow()) + " s: a held AI man who wants to attack "
                    + (s.AiHoldRaiseGuard ? "raises his guard instead (from the next frame)" : "only has the attack taken out (from the next frame)"));
            }
            if (s.AttackRatePlayerTimer != _seenPlayerTimer)
            {
                _seenPlayerTimer = s.AttackRatePlayerTimer;
                _rateSwitched = true;
                TraxLog.Info("rate", "AttackRatePlayerTimer switched " + (s.AttackRatePlayerTimer ? "ON" : "OFF") + " mid-mission at " + Sec(SafeNow()) + " s: "
                    + (s.AttackRatePlayerTimer ? "your attacks wait out their pause again" : "your attacks are no longer held (a pause running now ends at once)"));
            }
        }

        // ------------------------------------------------------------------ logs

        /// <summary>Once per mission: the first fighter slowed below full strength - every value a
        /// technique touches, before → after the recompute, and whether each took its factor.</summary>
        private void LogFirstSlowed(TrackedAgent st, in SpeedPenalty.Snapshot before, in SpeedPenalty.AiSnapshot aiBefore)
        {
            try
            {
                var s = TraxSettings.Shared;
                float m = st.SpeedMultiplier;
                float anim = AttackTimerMath.AnimationMultiplier(m, s.AttackAnimationMinPercent);
                var after = SpeedPenalty.Snapshot.Take(st.Agent);
                var aiAfter = SpeedPenalty.AiSnapshot.Take(st.Agent);
                bool ai = s.AttackRateAiDecisions;
                bool animations = Close(after.Swing, before.Swing * anim) && Close(after.Thrust, before.Thrust * anim) && Close(after.Reload, before.Reload * anim);
                bool decisions = aiAfter.Took(aiBefore, ai ? m : 1f, ai ? 1f / AttackRateMath.SafeM(m) : 1f);
                TraxLog.Info("rate", "first slowed fighter this mission: " + Name(st) + " at " + Sec(SafeNow()) + " s - attack speed x" + F2(m)
                    + " (f " + F2(AthleticsMath.PeakShare(Rules, st)) + "); animations (AttackAnimationMinPercent " + s.AttackAnimationMinPercent + " → x" + F2(anim)
                    + "): swing " + F3(before.Swing) + " → " + F3(after.Swing) + ", thrust/draw " + F3(before.Thrust) + " → " + F3(after.Thrust) + ", reload "
                    + F3(before.Reload) + " → " + F3(after.Reload) + " (" + (animations ? "each x" + F2(anim) + " as asked" + (anim >= 1f ? " - full speed, no slow-mo" : string.Empty) : "NOT x" + F2(anim) + " - tell Claude")
                    + "); AI decisions (AttackRateAiDecisions " + (ai ? "on" : "off") + "): " + aiAfter.Change(aiBefore) + " ("
                    + (decisions ? (ai ? "chances x" + F2(m) + ", the aim ÷ " + F2(m) + " as asked" : "unchanged, as asked") : "NOT as asked - tell Claude")
                    + "); the timer (you: AttackRatePlayerTimer " + (s.AttackRatePlayerTimer ? "on" : "off") + ", AI: AttackRatePaceHold " + (s.AttackRatePaceHold ? "on" : "off")
                    + ") comes after each attack - the first of each is logged in full; untouched on purpose: handling (blocking), shield defend speed, the recoil after a block, AIHoldingReady");
            }
            catch (Exception e)
            {
                Failed("rate.first-slowed", e);
            }
        }

        private void LogHold(TrackedAgent st, PaceState ps, double now, bool first)
        {
            string text = Name(st) + " at " + Sec(now) + " s - his " + (ps.Kind == AttackKind.Melee ? "melee" : "ranged") + " attack"
                + (st.Agent.MountAgent != null ? " (mounted)" : string.Empty) + " (D " + F2(ps.Duration) + " s: wind-up + " + (ps.Kind == AttackKind.Melee ? "swing" : "loose + reload")
                + ") ended at " + Sec(ps.AttackEnd) + " s at attack speed x" + F2(ps.Asked) + " (f " + F2(AthleticsMath.PeakShare(Rules, st)) + ") → no new attack for "
                + F2(ps.Pause) + " s = D x (1/m - 1), until " + Sec(ps.Until) + " s";
            if (first && ps.ByInput)
                TraxLog.Info("rate", "first AI timer this mission: " + text + "; technique: BY INPUT - " + HookText(ps.Hook)
                    + "; only the attack bits are taken out of his own input while it runs" + (TraxSettings.Shared.AiHoldRaiseGuard ? ", a guard raised when he wants to attack" : "")
                    + "; scripted flags " + ps.FlagsBefore + " (none of ours)" + (st.StepBack != null && st.StepBack.Active ? "; he is stepping back too - his attacks stay held until the later end" : ""));
            else if (first)
                TraxLog.Info("rate", "first AI timer this mission: " + text + "; technique: NoAttack; scripted flags " + ps.FlagsBefore + " → " + ps.FlagsAfter
                    + " (NoAttack " + (((ps.FlagsAfter & (int)Agent.AIScriptedFrameFlags.NoAttack) != 0) ? "set: the engine took it" : "NOT set") + ")");
            else
                TraxLog.Verbose("rate", "AI timer: " + text, "rate-hold");
        }

        private void LogHoldEnd(TrackedAgent st, PaceState ps, PaceEnd why, double now, double held)
        {
            TraxLog.Info("rate", "first AI timer ended at " + Sec(now) + " s after " + F2(held) + " s - " + EndText(why) + "; "
                + (ps.ByInput
                    ? "the input hook: " + InputCaptureText(st.Input, attackWord: "an attack wish while held") + "; melee hits taken while held " + ps.HitsTaken + " (blocked " + ps.HitsBlocked + ")"
                    : "scripted flags now " + ps.FlagsAfter + (ps.Waiting ? " (a game job is on him: our NoAttack comes off once he is free)" : string.Empty)
                      + "; melee hits taken while held " + ps.HitsTaken + " (blocked " + ps.HitsBlocked + ")")
                + "; the gap to his next attack is in the summary (\"timer:\" rows)");
        }

        private static string EndText(PaceEnd why) => why switch
        {
            PaceEnd.TimeUp => "time up",
            PaceEnd.AttackStarted => "an attack started anyway (the hold did not stop it)",
            PaceEnd.SwitchedOff => "switched off",
            PaceEnd.LeftField => "he left the field",
            PaceEnd.MissionEnd => "mission end",
            PaceEnd.PlayerControl => "the player took him",
            _ => "error",
        };

        /// <summary>The summary's attack-rate lines (own try in the caller).</summary>
        private void WriteRateSummary()
        {
            foreach (var line in _rateStats.SummaryLines(AttackRateRules.From(TraxSettings.Shared), _rateSwitched))
                TraxLog.Info("summary", line);
        }
    }
}

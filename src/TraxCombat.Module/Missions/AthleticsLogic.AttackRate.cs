using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Models;

namespace TraxCombat.Missions
{
    /// <summary>
    /// The attack RATE (DESIGN §2 "Attack speed", step 5e; research and choice in AI_NOTES "Step 5e"):
    /// the whole attack cycle follows the attack multiplier m, not only the swing.
    ///
    /// THREE TECHNIQUES
    ///   T1 the animations (steps 5 / 5c): swing, thrust / draw / throw, reload × m - the stat decorator.
    ///   T2 AttackRateAiDecisions: the AI's chance to attack, to riposte and to loose × m, the aim
    ///      before a shot ÷ m - the same decorator pass (<see cref="SpeedPenalty.ScaleAiDecisions"/>);
    ///      a switch change re-applies to every tired fighter over the next ticks (budgeted).
    ///   T3 AttackRatePaceHold: after each MELEE swing of a tired AI fighter on foot, NoAttack (guard up)
    ///      until his next release can come no sooner than his fresh cycle ÷ m after this one. Asked
    ///      for at the swing's END (after the step-back roll), started by the next tick (never inside
    ///      an engine callback), lifted when its time is up - or at once when a switch goes off, he
    ///      leaves, the player takes him, he mounts, a swing slips through, the mission ends. The
    ///      engine side is behind <see cref="IPaceBody"/>: NoAttack only on a man with nothing of the
    ///      game's on him, lifted only while nothing of the game's is on him (else we wait).
    ///
    /// MEASURED (the playtest reads it without a second run): every action change on channel 1 closes
    /// and opens a PHASE (wind-up + held / draw + aim, release, recoil after a block, reload, the pause
    /// before the next ready), each filed under the f band at its start with the m it ran at; the
    /// cycle release-to-release / shot-to-shot; per band and group (melee / ranged × AI / you) the
    /// target (the peak's cycle ÷ m) and measured ÷ target with a verdict word; the holds; the guard
    /// by f (tired men must not block less). A ready's full wind-up is found by polling its progress
    /// (one native call per fighter in an unfinished ready - the tick cost line shows it).
    ///
    /// LOGS: [rate] mission start (the techniques), the FIRST slowed fighter's properties before →
    /// after (every value any technique touches), the FIRST hold in full (start and end), switch
    /// changes, switched-off releases; verbose: every hold (bucket rate-hold); the [summary] lines of
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

        /// <summary>The AI's fresh melee cycles on foot this mission (the hold's fallback reference).</summary>
        private MeanStd _missionFresh;

        private bool _seenAiDecisions;
        private bool _seenPaceHold;
        private bool _paceSeenOn;
        private bool _rateSwitched;
        private bool _paceClosed;
        private bool _firstHoldStarted;
        private bool _firstSlowedLogged;

        /// <summary>This mission's attack-rate numbers (the offline smoke reads them).</summary>
        internal AttackRateStats RateStats => _rateStats;

        /// <summary>The engine side of the pace hold - the offline smoke swaps in a stand-in.</summary>
        internal IPaceBody PaceBody { get; set; } = new GamePaceBody();

        /// <summary>Holds running now (not those waiting for a game job).</summary>
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
            _paceSeenOn = rr.PaceOn;
            TraxLog.Info("rate", "mission start: " + rr.Describe() + " - read live; the [summary] \"attack rate\" lines measure every phase and the cycle by f");
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

        /// <summary>
        /// The channel-1 action changed (poll or hit): close the running phase (filed under the band at
        /// its START, with the m it ran at), note the pause between attacks, open the new phase. Runs
        /// BEFORE this action's own charge, so a release is filed at the band it began in. May run
        /// inside an engine hit callback: managed reads only.
        /// </summary>
        private void PhasesOnAction(TrackedAgent st, int action, double now, in AthleticsRules r)
        {
            try
            {
                if (!r.Enabled) return;
                bool player = st.Agent.IsMainAgent;
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
                                if (k == AttackKind.Melee)
                                {
                                    st.LastReadySeconds = d;
                                    st.LastReadyAsked = st.PhaseAsked;
                                }
                            }
                            else
                            {
                                _rateStats.AddReadyCancelled(k, player);
                            }
                            break;
                        case AttackPhase.Release:
                            _rateStats.AddPhase(k, player, st.PhaseBin, AttackPhase.Release, d, st.PhaseAsked);
                            if (k == AttackKind.Melee && !st.HitThisRelease) _rateStats.AddPhase(k, player, st.PhaseBin, AttackPhase.CleanRelease, d, st.PhaseAsked);
                            break;
                        case AttackPhase.Recoil:
                        case AttackPhase.Reload:
                            _rateStats.AddPhase(k, player, st.PhaseBin, (AttackPhase)running, d, st.PhaseAsked);
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
                    if (kind == AttackKind.Melee) PaceReadyStarted(st, now);
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
        }

        /// <summary>
        /// One cycle (release to release, shot to shot), classified by the band after the first
        /// attack's charge; the second one must be in the same band (else "mixed"). An AI fighter's
        /// fresh melee cycle on foot also feeds the pace hold's references - his own and the mission's.
        /// </summary>
        private void NoteCycle(TrackedAgent st, AttackKind kind, int binNow, int binAfterLast, double seconds, float asked)
        {
            bool player = st.Agent.IsMainAgent;
            if (kind == AttackKind.Melee && st.SteppedBackThisCycle)
            {
                _rateStats.AddSteppedBack(kind, player);
                return;
            }
            if (binNow != binAfterLast)
            {
                _rateStats.AddMixed(kind, player);
                return;
            }
            bool counted = _rateStats.AddCycle(kind, player, binNow, seconds, asked);
            if (counted && kind == AttackKind.Melee && !player && binNow == 0 && asked >= 1f && st.Agent.MountAgent == null)
            {
                st.FreshCycleSum += seconds;
                st.FreshCycleCount++;
                _missionFresh.Add(seconds);
            }
        }

        // ------------------------------------------------------------------ the pace hold (T3)

        /// <summary>A counted melee swing started: while he is held it slipped through NoAttack (counted;
        /// the tick lifts the hold); a hold still queued is moot.</summary>
        private void PaceSwingStarted(TrackedAgent st)
        {
            var ps = st.Pace;
            if (ps == null) return;
            if (ps.Pending)
            {
                ps.Pending = false;
                _rateStats.AddRefused(PaceRefusal.TooLate);
            }
            if (ps.Active && !ps.EndAsked)
            {
                ps.EndAsked = true;
                ps.EndReason = PaceEnd.SwingStarted;
            }
        }

        /// <summary>
        /// A counted melee swing ENDED (after the step-back roll): a tired AI fighter on foot gets a hold
        /// until his next ready may begin - <see cref="AttackRateMath.HoldUntil"/> - queued for the next
        /// tick. May run inside an engine hit callback: managed reads only. Internal: the smoke drives it
        /// through <see cref="ObserveAction"/>.
        /// </summary>
        private void PaceSwingEnded(TrackedAgent st, double now, double releaseStart, int nextAction, in AthleticsRules r)
        {
            try
            {
                var rr = AttackRateRules.From(TraxSettings.Shared);
                if (!rr.PaceOn || _paceClosed) return;
                var a = st.Agent;
                if (a.IsMainAgent)
                {
                    _rateStats.AddNotHeld(PaceNotHeld.Player);
                    return;
                }
                if (a.MountAgent != null)
                {
                    _rateStats.AddNotHeld(PaceNotHeld.Rider);
                    return;
                }
                float m = st.SpeedMultiplier;
                if (m >= 1f)
                {
                    _rateStats.AddNotHeld(PaceNotHeld.FullStrength);
                    return;
                }
                var sb = st.StepBack;
                if (sb != null && (sb.Pending || sb.Active))
                {
                    _rateStats.AddNotHeld(PaceNotHeld.SteppingBack);
                    return;
                }
                var ps = st.Pace;
                if (ps != null && (ps.Pending || ps.Active)) return; // a swing slipped through a hold: the tick is lifting it
                if (nextAction == ActionReadyMelee)
                {
                    _rateStats.AddNotHeld(PaceNotHeld.AlreadyReadied);
                    return;
                }
                double reference = AttackRateMath.FreshReference(st.FreshCycleSum, st.FreshCycleCount, _missionFresh.Mean, _missionFresh.Count, out bool own);
                if (double.IsNaN(reference))
                {
                    _rateStats.AddNotHeld(PaceNotHeld.NoReference);
                    return;
                }
                double ready = AttackRateMath.ExpectedReady(st.LastReadySeconds, st.LastReadyAsked, m);
                double until = AttackRateMath.HoldUntil(releaseStart, reference, m, ready);
                if (!AttackRateMath.HoldNeeded(until, now))
                {
                    _rateStats.AddNotHeld(PaceNotHeld.NotNeeded);
                    return;
                }
                ps ??= st.Pace = new PaceState();
                ps.Pending = true;
                ps.PendingAt = now;
                ps.Until = until;
                ps.Bin = AthleticsMath.PeakBin(AthleticsMath.PeakShare(in r, st));
                ps.Asked = m;
                ps.Reference = reference;
                ps.ReferenceOwn = own;
                ps.ExpectedReady = ready;
                ps.ReleaseStart = releaseStart;
                ps.EndAsked = false;
                _pacePending.Add(st);
            }
            catch (Exception e)
            {
                Failed("rate.pace-ask", e);
            }
        }

        /// <summary>His melee ready began: how long after his last hold ended (near 0 = the hold set his rhythm).</summary>
        private void PaceReadyStarted(TrackedAgent st, double now)
        {
            var ps = st.Pace;
            if (ps == null || ps.EndedAt < 0) return;
            _rateStats.AddNextReadyAfterHold(now - ps.EndedAt);
            ps.EndedAt = -1;
        }

        /// <summary>
        /// Every tick, whatever the switches (so switching off lifts every hold at once): the running
        /// holds (asked to end, the player / a horse, time up), the ones waiting for a game job, then
        /// the queued starts. Internal: the offline smoke drives it with its stand-in body.
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
                    TraxLog.Info("rate", (rr.PaceOffBecause ?? "AttackRatePaceHold") + " switched OFF mid-mission at " + Sec(now) + " s: "
                        + n + " held fighters may attack again at once");
                }
                else
                {
                    TraxLog.Info("rate", "the pace hold is ON again at " + Sec(now) + " s: tired AI fighters are held from their next swing");
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
                    if (PaceBody.MustEnd(st, out var why))
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

            if (_pacePending.Count == 0) return;
            int budget = AttackRateMath.MaxHoldStartsPerTick;
            for (int i = 0; i < _pacePending.Count; i++)
            {
                var st = _pacePending[i];
                var ps = st.Pace!;
                if (!ps.Pending) continue; // cancelled by a swing meanwhile (counted there)
                ps.Pending = false;
                if (st.Removed || _paceClosed || !rr.PaceOn)
                {
                    RefuseHold(st, ps, PaceRefusal.Gone);
                    continue;
                }
                // too late: the time is (nearly) up, or his next ready began since the swing ended (the
                // poll ran before this in the same tick) - NoAttack must never land on a readied blow
                if (ps.Until - now < AttackRateMath.MinHoldSeconds || st.PrevAction == ActionReadyMelee || st.PrevAction == ActionReleaseMelee)
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
                StartHold(st, ps, now);
            }
            _pacePending.Clear();
        }

        private void StartHold(TrackedAgent st, PaceState ps, double now)
        {
            bool held;
            PaceRefusal why;
            try
            {
                held = PaceBody.Start(st, ps, out why);
            }
            catch (Exception e)
            {
                Failed("rate.pace-start", e);
                // whatever the engine got, take it back (a NoAttack nobody tracks would never end)
                try { PaceBody.Release(st, ps, evenUnderAFrame: false); } catch { /* already failing - logged above */ }
                RefuseHold(st, ps, PaceRefusal.Error);
                return;
            }
            if (!held)
            {
                RefuseHold(st, ps, why);
                return;
            }
            ps.Active = true;
            ps.StartedAt = now;
            ps.EndAsked = false;
            ps.Waiting = false;
            ps.First = !_firstHoldStarted;
            _firstHoldStarted = true;
            _paceHeld.Add(st);
            _rateStats.AddHoldStart(ps.Bin, ps.Asked);
            if (ps.First) LogHold(st, ps, now, first: true);
            else if (TraxLog.VerboseOn) LogHold(st, ps, now, first: false);
        }

        private void RefuseHold(TrackedAgent st, PaceState ps, PaceRefusal why)
        {
            ps.Pending = false;
            _rateStats.AddRefused(why);
            if (TraxLog.VerboseOn)
                TraxLog.Verbose("rate", "pace hold not started: " + Name(st) + " (m " + F2(ps.Asked) + ") - " + why, "rate-hold");
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
            ps.EndedAt = why == PaceEnd.TimeUp ? now : -1;
            Lift(st, ps, now, native, waitingPass: false);
            if (ps.First) LogHoldEnd(st, ps, why, now, held);
            else if (TraxLog.VerboseOn)
                TraxLog.Verbose("rate", "pace hold ended: " + Name(st) + " after " + F2(held) + " s - " + why, "rate-hold");
        }

        /// <summary>Takes our NoAttack off - or, with a game job on him, waits (re-checked every
        /// <see cref="AttackRateMath.WaitingCheckSeconds"/>) and never touches the job's flags.</summary>
        private void Lift(TrackedAgent st, PaceState ps, double now, bool native, bool waitingPass)
        {
            bool longFrame = waitingPass && ps.Waiting && now - ps.WaitingSince >= AttackRateMath.WaitingMaxSecondsUnderAFrame;
            var rel = native && !st.Removed ? PaceBody.Release(st, ps, longFrame) : PaceRelease.Gone;
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
            }
        }

        /// <summary>He left the field: his hold ends without an engine call.</summary>
        private void PaceLeftField(TrackedAgent st)
        {
            var ps = st.Pace;
            if (ps == null) return;
            if (ps.Active) EndHold(st, ps, PaceEnd.LeftField, SafeNow(), native: false);
            else if (ps.Waiting)
            {
                ps.Waiting = false;
                _paceHeld.Remove(st);
            }
        }

        /// <summary>The mission is over (before the summary): every hold ends - through the engine while
        /// the agents live - and no new one starts.</summary>
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
        }

        // ------------------------------------------------------------------ the guard, the settings

        /// <summary>A melee collision on a tracked fighter on foot (OnMeleeHit): blocked or landed, by his
        /// f and whether a hold is on him - "tired men must not block less".</summary>
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
        /// A settings change (from <see cref="ApplySettingsChange"/>): AttackRateAiDecisions switched →
        /// every tired fighter is recomputed over the next ticks (the decorator reads the switch live on
        /// each recompute; the budget spreads them); either switch is logged and the summary is told the
        /// battle mixed both settings.
        /// </summary>
        private void NoteRateSettings(TraxSettings s)
        {
            if (s.AttackRateAiDecisions != _seenAiDecisions)
            {
                _seenAiDecisions = s.AttackRateAiDecisions;
                _rateSwitched = true;
                int n = 0;
                for (int i = 0; i < _count; i++)
                {
                    var st = _dense[i];
                    if (st.SpeedMultiplier >= 1f) continue;
                    st.SpeedDirty = true;
                    n++;
                }
                TraxLog.Info("rate", "AttackRateAiDecisions switched " + (s.AttackRateAiDecisions ? "ON" : "OFF") + " mid-mission at " + Sec(SafeNow()) + " s: "
                    + n + " tired fighters get their AI attack values " + (s.AttackRateAiDecisions ? "scaled by their attack speed" : "back") + " over the next ticks (at most "
                    + MaxRecomputesPerTick + " recomputes a tick)");
            }
            if (s.AttackRatePaceHold != _seenPaceHold)
            {
                _seenPaceHold = s.AttackRatePaceHold;
                _rateSwitched = true;
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
                var after = SpeedPenalty.Snapshot.Take(st.Agent);
                var aiAfter = SpeedPenalty.AiSnapshot.Take(st.Agent);
                bool ai = s.AttackRateAiDecisions;
                bool animations = Close(after.Swing, before.Swing * m) && Close(after.Thrust, before.Thrust * m) && Close(after.Reload, before.Reload * m);
                bool decisions = aiAfter.Took(aiBefore, ai ? m : 1f, ai ? 1f / AttackRateMath.SafeM(m) : 1f);
                TraxLog.Info("rate", "first slowed fighter this mission: " + Name(st) + " at " + Sec(SafeNow()) + " s - attacks x" + F2(m)
                    + " (f " + F2(AthleticsMath.PeakShare(Rules, st)) + "); T1 animations: swing " + F3(before.Swing) + " → " + F3(after.Swing)
                    + ", thrust/draw " + F3(before.Thrust) + " → " + F3(after.Thrust) + ", reload " + F3(before.Reload) + " → " + F3(after.Reload)
                    + " (" + (animations ? "each x" + F2(m) + " as asked" : "NOT x" + F2(m) + " - tell Claude") + "); T2 AI decisions (AttackRateAiDecisions "
                    + (ai ? "on" : "off") + "): " + aiAfter.Change(aiBefore) + " (" + (decisions ? (ai ? "chances x" + F2(m) + ", the aim ÷ " + F2(m) + " as asked" : "unchanged, as asked") : "NOT as asked - tell Claude")
                    + "); T3 pace hold (AttackRatePaceHold " + (s.AttackRatePaceHold ? "on" : "off") + "): his holds come after his swings - the first one is logged in full; "
                    + "untouched on purpose: handling (blocking), shield defend speed, the recoil after a block, AIHoldingReady");
            }
            catch (Exception e)
            {
                Failed("rate.first-slowed", e);
            }
        }

        private void LogHold(TrackedAgent st, PaceState ps, double now, bool first)
        {
            string text = Name(st) + " at " + Sec(now) + " s - attacks x" + F2(ps.Asked) + " (f " + F2(AthleticsMath.PeakShare(Rules, st)) + "); fresh cycle "
                + F2(ps.Reference) + " s (" + (ps.ReferenceOwn ? "his own, " + st.FreshCycleCount + " sample" + (st.FreshCycleCount == 1 ? "" : "s") : "the mission's AI average, " + _missionFresh.Count + " samples")
                + ") → target " + F2(AttackRateMath.TargetCycle(ps.Reference, ps.Asked)) + " s from his swing at " + Sec(ps.ReleaseStart) + " s; his next ready expected to take "
                + F2(ps.ExpectedReady) + " s → held " + F2(ps.Until - now) + " s (until " + Sec(ps.Until) + " s)";
            if (first)
                TraxLog.Info("rate", "first pace hold this mission: " + text + "; scripted flags " + ps.FlagsBefore + " → " + ps.FlagsAfter
                    + " (NoAttack " + (((ps.FlagsAfter & (int)Agent.AIScriptedFrameFlags.NoAttack) != 0) ? "set: the engine took it" : "NOT set") + ")");
            else
                TraxLog.Verbose("rate", "pace hold: " + text, "rate-hold");
        }

        private void LogHoldEnd(TrackedAgent st, PaceState ps, PaceEnd why, double now, double held)
        {
            TraxLog.Info("rate", "first pace hold ended at " + Sec(now) + " s after " + F2(held) + " s - " + EndText(why) + "; scripted flags now "
                + ps.FlagsAfter + (ps.Waiting ? " (a game job is on him: our NoAttack comes off once he is free)" : string.Empty)
                + "; his next ready's delay after the hold is in the summary (pace hold ends)");
        }

        private static string EndText(PaceEnd why) => why switch
        {
            PaceEnd.TimeUp => "time up",
            PaceEnd.SwingStarted => "a swing started anyway (NoAttack did not hold it)",
            PaceEnd.SwitchedOff => "switched off",
            PaceEnd.LeftField => "he left the field",
            PaceEnd.MissionEnd => "mission end",
            PaceEnd.PlayerControl => "the player took him",
            PaceEnd.Mounted => "he mounted",
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

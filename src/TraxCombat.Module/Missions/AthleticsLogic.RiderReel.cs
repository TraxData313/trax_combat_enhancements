using System;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>Step 104b: one reel in the saddle - decided in the hit callback, started and ended by the tick (a struct in a fixed pool).</summary>
    internal struct RiderReelRec
    {
        public TrackedAgent St;
        public TrackedAgent? Blocker;
        public ImpactEvent Event;
        public bool Started, You, AiLocked, NoGuard, AnimOurs, AnimCleared, HalfRead;

        /// <summary>Step 104c: the ladder rolled a knock-off (a perfect block) / a rear (a sharp one) on top of the reel; applied at its start
        /// (<see cref="Fell"/> / <see cref="Reared"/>).</summary>
        public bool KnockOff, Rear, Fell, Reared;
        public double DecidedAt, Start, Until, Asked, AnimSeconds, AnimEnd, Horse, Score;
        public int AnimIndex;

        /// <summary>The block's timing - your on-screen line's numbers.</summary>
        public TimingSource Source;
        public double GuardAge, Perfect;
    }

    /// <summary>
    /// Step 104b - THE REEL IN THE SADDLE, LIVE (Toni 2026-11-09: "can you add the reel vs mounted troops as well somehow, like that 3rd round, we
    /// were on horsebacks fighting, I blocked"; DESIGN decision 93 - APPROVED "as suggested"; AI_NOTES "Step 104" / "104b - built").
    ///   - THE DECISION: step 104a's ladder (<see cref="TimedRiderMath.Decide"/>) on every correct block of a RIDER's blow (rider vs rider; a man on
    ///     foot vs a rider behind <c>TimedRiderFromFoot</c>), rolled by the rider dice: the reel at the decision's own timed riposte chance (s 0 =
    ///     today's refusal, no die - <see cref="ImpactMath.Decide"/> still refuses a rider's riposte "a rider"), its length from step 56's blended
    ///     to <c>TimedRiderReelMaxSeconds</c> 1.5 by s (on you at most 1.0). With <c>TimedBlocksOnHorseback</c> ON (and the riposte family live) a
    ///     taken reel is APPLIED: his record reels (<see cref="ImpactState.ReelingUntil"/>, cause <see cref="ReelCause.RiderReel"/> - step 105 refuses
    ///     him "a rider", the usual model gives a rider no reeling bonus) and the reel is queued for the tick. The rear and the knock-off stay
    ///     applied from the tick since step 104c; the perfect strike window / the mounted push-out from <see cref="TimedRiderReelAccepted"/> since 104d.
    ///   - THE TICK STARTS IT (never in the hit callback): THE LOCK - an AI rider's attack AND guard bits out of every frame through step 16's hook
    ///     (<see cref="AiInputHook.ApplyReel"/>), yours through your input gate (<see cref="GatePlayerInput"/>), his steering NEVER locked; THE
    ///     ANIMATION - <c>TimedRiderReelAction</c> (act_stagger_backward x1.0 by default - Toni's J-tool pick later is a setting change) on his
    ///     CHANNEL 1 ONLY (never channel 0 on a rider, never an action on the horse), cut at <c>TimedRiderReelAnimMaxSeconds</c>; THE HORSE - its
    ///     top speed at min(his tired multiplier, <c>TimedRiderHorseCheckPercent</c> 50 %) through the stat decorator (<see cref="TrackedAgent.ReelHorseCheck"/>);
    ///     <see cref="TimedRiderReelAccepted"/> - the slow motion on your best blocks (step 101's), the on-screen timing line.
    ///   - EVERY LOCK GIVEN BACK on every exit (<see cref="RiderReelEnd"/>): its end, death / removal (no engine call), dismounted, a switch off
    ///     (the master switch first), the mission end (before the summary) / the behaviour's removal, an error (all let go, the reel off for the
    ///     battle). The [summary] "timed blocks on horseback (step 104b)" line counts "left over at the mission end" - must be 0.
    /// Main thread; a fixed pool, no allocation per event or tick; the tick's cost is the reeling riders only.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        /// <summary>Riders reeling at once (a pool - a full pool refuses the reel, counted).</summary>
        internal const int RiderReelPool = 64;

        /// <summary>His horse's speed is read this long after the reel's start (s) - the summary's middle measure.</summary>
        private const double RiderReelHalfSeconds = 0.5;

        private readonly RiderReelRec[] _riderReels = new RiderReelRec[RiderReelPool];
        private int _riderReelCount;
        private int _youReelSlot = -1;
        private readonly TimedRiderReelStats _riderReel = new TimedRiderReelStats();
        private bool _riderReelDisabled, _riderReelFirstStart;

        /// <summary>Step 104b: the engine side (the smoke swaps in its stand-in).</summary>
        internal IRiderReelBody RiderReelBody { get; set; } = RiderReelBodyDefault.Current;

        /// <summary>Step 104b: this mission's live reels (the smoke reads them).</summary>
        internal TimedRiderReelStats RiderReelStats => _riderReel;

        /// <summary>Step 104b: riders reeling now (the smoke).</summary>
        internal int RiderReelsActive => _riderReelCount;

        /// <summary>Step 104b: the live reel is on now - the switches read live (the master switch first, through <see cref="TimedRiderRules.Live"/>)
        /// AND the impact system's riposte family applies (not shadow mode).</summary>
        private static bool RiderReelLive(in TimedRiderRules rr, in ImpactRules r) => rr.Live && r.Applies(ImpactFamily.Riposte);

        /// <summary>From <see cref="RiderShadow"/> (the hit callback): a taken reel on a rider APPLIED - his record reels, the tick starts it.
        /// False = not applied (off, the pool full, already in it, the reel off after an error).</summary>
        private bool QueueRiderReel(TrackedAgent a, TrackedAgent v, in TimedRiderShadow o, in TimedRiderRules rr, in ImpactRules r, in ImpactDecision d, double now)
        {
            if (_riderReelDisabled || !o.Reel || o.Combo == TimedRiderCombo.RiderVsFoot || o.Combo == TimedRiderCombo.None) return false;
            if (!RiderReelLive(in rr, in r)) return false;
            if (a.Removed || a.RiderReelSlot >= 0) return false;
            if (_riderReelCount >= _riderReels.Length)
            {
                _riderReel.PoolFull++;
                return false;
            }
            double length = o.ReelSeconds;
            if (!(length > 0)) return false;
            a.Impact.Reel(now + length, r.StaggerImmunity, ReelCause.RiderReel); // step 105 sees him reel - and refuses a rider
            a.Impact.NoteOutcome(now); // the halving / courage count it like a riposte
            int i = _riderReelCount++;
            _riderReels[i] = new RiderReelRec
            {
                St = a,
                Blocker = v,
                Event = d.Event,
                You = IsPlayer(a),
                DecidedAt = now,
                Asked = length,
                AnimSeconds = double.IsNaN(o.AnimSeconds) ? 0 : o.AnimSeconds,
                Horse = double.IsNaN(o.HorseCheck) ? 1 : o.HorseCheck,
                Score = o.Score,
                AnimIndex = -1,
                Start = double.NaN,
                Until = now + length,
                AnimEnd = double.NaN,
                Source = d.TimedSource,
                GuardAge = d.TimedAge,
                Perfect = d.TimedPerfect,
                KnockOff = o.Dismount, // step 104c: applied from the tick at the reel's start
                Rear = o.Rear,
            };
            a.RiderReelSlot = i;
            _riderReel.NoteDecided(o.Score, IsPlayer(v), IsPlayer(a));
            if (o.Dismount) _riderFall.NoteDecided(RiderFallKind.KnockOff);
            if (o.Rear) _riderFall.NoteDecided(RiderFallKind.Rear);
            return true;
        }

        /// <summary>Every tick (from TickImpact, its own try there): the switches read live - off = every reel let go; each queued reel started,
        /// each running one followed (the horse's speed, the animation's part) and ended. Nothing to do with no rider reeling.</summary>
        private void TickRiderReels(double now)
        {
            if (_riderReelCount == 0) return;
            var rr = TimedRiderRules.From(TraxSettings.Shared);
            var r = ImpactRules.From(TraxSettings.Shared);
            if (!RiderReelLive(in rr, in r))
            {
                TraxLog.Info("impact", "rider reel: " + _riderReelCount + " reel(s) in the saddle let go at " + Sec(now) + " s - " + RiderReelOffWhy(in rr, in r) + " (every lock given back)");
                ReleaseAllRiderReels(RiderReelEnd.SwitchedOff, now, engine: true);
                return;
            }
            var body = RiderReelBody;
            for (int i = _riderReelCount - 1; i >= 0; i--)
            {
                try
                {
                    ref var p = ref _riderReels[i];
                    var st = p.St;
                    if (st.Removed || !ImpactBody.IsActive(st))
                    {
                        ReleaseRiderReel(i, RiderReelEnd.Removed, now, engine: false);
                        continue;
                    }
                    if (!body.Riding(st))
                    {
                        ReleaseRiderReel(i, RiderReelEnd.Dismounted, now, engine: true);
                        continue;
                    }
                    if (!p.Started)
                    {
                        StartRiderReel(ref p, in rr, now);
                        continue;
                    }
                    if (!p.HalfRead && now - p.Start >= RiderReelHalfSeconds)
                    {
                        p.HalfRead = true;
                        _riderReel.NoteSpeed(1, body.HorseSpeed(st));
                    }
                    if (p.AnimOurs && !p.AnimCleared && now >= p.AnimEnd && now < p.Until)
                    {
                        p.AnimCleared = true; // the animation's part is over: channel 1 back to him - the lock still holds his hands
                        if (AnimBody.CurrentIndex(st, TimedRiderAnimMath.RiderChannel) == p.AnimIndex && AnimBody.ClearUpper(st)) _riderReel.AnimCleared++;
                    }
                    if (now >= p.Until) ReleaseRiderReel(i, RiderReelEnd.Ended, now, engine: true);
                }
                catch (Exception e)
                {
                    RiderReelFailed(e, now);
                    return;
                }
            }
        }

        /// <summary>The reel's start from the tick: the lock (the AI's hook / your gate), the channel-1 animation, the horse; the seam.</summary>
        private void StartRiderReel(ref RiderReelRec p, in TimedRiderRules rr, double now)
        {
            var st = p.St;
            var body = RiderReelBody;
            p.Started = true;
            p.Start = now;
            p.Until = now + p.Asked;
            st.Impact.Reel(p.Until, ImpactRules.From(TraxSettings.Shared).StaggerImmunity, ReelCause.RiderReel); // re-timed from the real start
            _riderReel.Started++;
            // THE LOCK - his hands, never his steering
            p.NoGuard = rr.NoGuard;
            string lockWords;
            if (p.You)
            {
                _youReelSlot = st.RiderReelSlot;
                _riderReel.LockYou++;
                lockWords = "your input gate (attack" + (p.NoGuard ? " + guard" : string.Empty) + " keys do nothing)";
            }
            else if (body.AiDriven(st))
            {
                EnsureInput(st);
                AiInputHook.SetReel(st, true, p.NoGuard, now);
                NoteHooked(st, body.Hook(st));
                p.AiLocked = true;
                _riderReel.LockAi++;
                lockWords = "the AI's input (attack" + (p.NoGuard ? " + guard" : string.Empty) + " bits out)";
            }
            else
            {
                _riderReel.LockNone++;
                lockWords = "not locked (nobody's hands to hold)";
            }
            // STEP 104c - THE KNOCK-OFF (a perfect block) or, if he stayed on, THE REAR (a sharp one) - before the animation: never a fighting
            // animation on top of the engine's own fall / rear pose (the lock above stays)
            string fallWords = (p.KnockOff || p.Rear) ? ApplyRiderFall(ref p, now) : string.Empty;
            // THE ANIMATION - his upper body only
            string animWords;
            if (TimedRiderFallMath.SkipsReelAnimation(p.Fell, p.Reared))
            {
                _riderFall.AnimSkipped++;
                animWords = "no reel animation - " + (p.Fell ? "he falls (the engine's own)" : "his horse rears (the engine's own rear and his pose)");
            }
            else if (TimedRiderMath.IsNone(rr.ReelAction) || !(p.AnimSeconds > 0))
            {
                _riderReel.AnimNone++;
                animWords = "no animation (the lock only)";
            }
            else if (!AnimBody.Find(st, rr.ReelAction, out int index, out double natural))
            {
                _riderReel.AnimMissing++;
                animWords = rr.ReelAction + " not in his action set (the lock only)";
            }
            else if (AnimBody.Play(st, TimedRiderAnimMath.RiderChannel, index, (float)rr.AnimSpeed, 0f, 0.05f, 0.2f))
            {
                _riderReel.AnimTaken++;
                p.AnimOurs = true;
                p.AnimIndex = index;
                p.AnimEnd = now + p.AnimSeconds;
                animWords = rr.ReelAction + " x" + F2(rr.AnimSpeed) + " on his channel 1 for " + F2(p.AnimSeconds) + " s (its own " + F2(natural) + " s)";
            }
            else
            {
                _riderReel.AnimRefused++;
                animWords = rr.ReelAction + " REFUSED by the engine (the lock only)";
            }
            // THE HORSE - its top speed checked through the stat decorator
            if (p.Horse < 1)
            {
                st.ReelHorseCheck = (float)p.Horse;
                RetargetSpeed(st, Rules, exact: true);
                _riderReel.HorsesChecked++;
                _riderReel.HorseCheckSum += p.Horse;
            }
            _riderReel.NoteSpeed(0, body.HorseSpeed(st));
            TimedRiderReelAccepted(ref p, in rr, now);
            bool yours = p.You || (p.Blocker != null && IsPlayer(p.Blocker));
            string line = Name(st) + " reels in the saddle " + F2(p.Asked) + " s from " + Sec(now) + " s (" + F2(now - p.DecidedAt) + " s after "
                          + (p.Blocker != null ? Name(p.Blocker) : "(someone)") + "'s block, timing " + F2(p.Score) + "): " + lockWords + ", " + animWords
                          + ", his horse x" + F2(st.ReelHorseCheck < 1f ? st.ReelHorseCheck : 1f) + " top speed" + fallWords;
            if (!_riderReelFirstStart)
            {
                _riderReelFirstStart = true;
                TraxLog.Info("impact", "live: first REEL IN THE SADDLE (step 104b) - " + line + "; " + rr.Describe());
            }
            else if (yours) TraxLog.Info("impact", "rider reel: " + line);
            else if (TraxLog.VerboseWants("rider-reel")) TraxLog.Verbose("impact", "rider reel: " + line, "rider-reel");
        }

        /// <summary>THE SEAM (104b): the reel started - the slow motion on YOUR best blocks (step 101's - its own gates, cooldown and give-back
        /// paths); step 104d: a RIDING blocker's push-out (his guard's heavy parry) and YOUR perfect-strike window on the reeling rider (any side,
        /// never after a knock-off); the on-screen line with its timing.</summary>
        private void TimedRiderReelAccepted(ref RiderReelRec p, in TimedRiderRules rr, double now)
        {
            var blocker = p.Blocker != null && !p.Blocker.Removed ? p.Blocker : null;
            if (blocker != null && IsPlayer(blocker) && rr.SlowMoOn && p.Score + 1e-9 >= rr.SlowMoMinScore) _riderReel.SlowMoAsked++;
            MaybeStartSlowMo(blocker, p.Score); // its own try
            MaybePushOut(blocker, p.Event, p.Score, now, riderReel: true); // step 104d: a RIDING blocker's guard snaps out (his heavy parry) - its own try
            MaybeOpenRiderStrike(in p, now); // step 104d: YOUR timed block opens a perfect-strike window on the reeling rider - its own try
            string? text = null;
            if (blocker != null && IsPlayer(blocker) && TraxSettings.Shared.TimedBlockShowTiming)
                text = TimedRiderLines.YourReel(MessageName(p.St), p.Event, p.Asked, rr.ReelMax, p.Source, p.GuardAge,
                    double.IsNaN(p.Perfect) ? TimedBlockRules.From(TraxSettings.Shared).Perfect : p.Perfect);
            if (text != null) text += TimedRiderLines.FallSuffix(p.Fell, p.Reared); // step 104c
            // its own try; on you: the plain "you reel" line; knocked off = step 53's own "knocked off his horse" line when no timing is read
            ImpactMessage(p.Fell ? ImpactMessageKind.KnockedOff : ImpactMessageKind.Riposte, blocker, p.St, text);
        }

        /// <summary>One reel ended: every lock given back - his input (or your gate), the animation still ours when cut early, his horse, his
        /// record; counted by <paramref name="why"/>. <paramref name="engine"/> false = no engine call (he left the field).</summary>
        private void ReleaseRiderReel(int i, RiderReelEnd why, double now, bool engine)
        {
            var p = _riderReels[i];
            var st = p.St;
            try
            {
                if (p.AiLocked)
                {
                    var s = st.Input;
                    if (s != null)
                    {
                        _riderReel.AiFrames += s.ReelFrames;
                        _riderReel.AiAttacksOut += s.ReelAttacksOut;
                        _riderReel.AiGuardsOut += s.ReelGuardsOut;
                        _riderReel.AiReadiesCancelled += s.ReelReadiesCancelled;
                        s.ReelFrames = s.ReelAttacksOut = s.ReelGuardsOut = s.ReelReadiesCancelled = 0;
                    }
                    AiInputHook.SetReel(st, false, false, now);
                }
                if (_youReelSlot == i) _youReelSlot = -1;
                st.ReelHorseCheck = 1f;
                if (!st.Removed && p.Horse < 1) RetargetSpeed(st, Rules, exact: true); // his horse's own (tired) speed back - applied by the tick's budget
                if (st.Impact.ReelCause == ReelCause.RiderReel && st.Impact.Reeling(now))
                    st.Impact.EndReel(now, why == RiderReelEnd.Ended ? ImpactRules.From(TraxSettings.Shared).StaggerImmunity : 0);
                if (engine && !st.Removed)
                {
                    if (p.Started) _riderReel.NoteSpeed(2, RiderReelBody.HorseSpeed(st));
                    if (p.AnimOurs && !p.AnimCleared && why != RiderReelEnd.Ended && why != RiderReelEnd.Dismounted
                        && AnimBody.CurrentIndex(st, TimedRiderAnimMath.RiderChannel) == p.AnimIndex)
                        AnimBody.ClearUpper(st); // cut early (a switch, the mission end, an error): his upper body back to him
                    if (p.AiLocked) RiderReelBody.Release(st);
                }
            }
            catch (Exception e)
            {
                Failed("impact.rider-reel-release", e); // the lock flags above are already given back (managed)
            }
            finally
            {
                st.RiderReelSlot = -1;
                _riderReel.NoteEnded(why, p.Started, p.Asked, p.Started && now >= p.Start ? now - p.Start : double.NaN);
                int last = --_riderReelCount;
                if (i != last)
                {
                    _riderReels[i] = _riderReels[last];
                    _riderReels[i].St.RiderReelSlot = i;
                    if (_youReelSlot == last) _youReelSlot = i;
                }
                _riderReels[last] = default;
                if (_riderReelCount == 0) _youReelSlot = -1;
                if (why != RiderReelEnd.Ended && (p.You || (p.Blocker != null && IsPlayer(p.Blocker))))
                    TraxLog.Info("impact", "rider reel: " + Name(st) + "'s reel in the saddle ended early at " + Sec(now) + " s - " + TimedRiderReelStats.EndName(why)
                        + (p.Started ? " after " + F2(now - p.Start) + " s of " + F2(p.Asked) : " before it started") + "; every lock given back");
            }
        }

        private void ReleaseAllRiderReels(RiderReelEnd why, double now, bool engine)
        {
            while (_riderReelCount > 0) ReleaseRiderReel(_riderReelCount - 1, why, now, engine);
        }

        /// <summary>He left the field (Forget): his reel ended with no engine call.</summary>
        private void RiderReelLeftField(TrackedAgent st)
        {
            int i = st.RiderReelSlot;
            if (i < 0 || i >= _riderReelCount || !ReferenceEquals(_riderReels[i].St, st)) return;
            ReleaseRiderReel(i, RiderReelEnd.Removed, SafeNow(), engine: false);
        }

        /// <summary>The mission end / the behaviour's removal - before the summary, which counts what is left (0).</summary>
        private void CloseRiderReels(string when)
        {
            if (_riderReelCount == 0) return;
            try
            {
                int n = _riderReelCount;
                ReleaseAllRiderReels(RiderReelEnd.MissionEnd, SafeNow(), engine: true);
                TraxLog.Info("impact", "rider reel: " + n + " reel(s) in the saddle let go at " + when + " - every lock given back");
            }
            catch (Exception e)
            {
                Failed("impact.rider-reel-close", e);
            }
        }

        /// <summary>An error: every reel let go at once (no engine call - the lock flags and the horse check are managed), the reel off for the battle.</summary>
        private void RiderReelFailed(Exception e, double now)
        {
            _riderReelDisabled = true;
            Failed("impact.rider-reel", e);
            try
            {
                ReleaseAllRiderReels(RiderReelEnd.Error, now, engine: false);
            }
            catch (Exception e2)
            {
                Failed("impact.rider-reel-error-release", e2);
            }
            TraxLog.Info("impact", "the reel in the saddle (step 104b) is OFF for this battle after an error - every lock given back");
        }

        private static string RiderReelOffWhy(in TimedRiderRules rr, in ImpactRules r) =>
            !rr.ShadowOn ? rr.OffBecause ?? "off" : !rr.Switch ? "TimedBlocksOnHorseback off" : !r.Applies(ImpactFamily.Riposte) ? "the riposte not live (" + r.ModeText() + ")" : "off";

        // ------------------------------------------------------------------ YOU reel: your input gate (every frame)

        /// <summary>From <see cref="GatePlayerInput"/> (every frame, right after the controller wrote your input): while YOU reel in the saddle,
        /// your attack (and guard) keys do nothing - never your steering. Own try: an error lets your input through (the tick ends the reel).</summary>
        private void GateYourReel()
        {
            try
            {
                int i = _youReelSlot;
                if (i < 0 || i >= _riderReelCount) return;
                ref var p = ref _riderReels[i];
                if (!p.Started || !p.You) return;
                var a = Mission?.MainAgent;
                if (a == null || !ReferenceEquals(a, p.St.Agent) || !a.IsActive() || a.IsAIControlled) return;
                uint flags = (uint)a.MovementFlags;
                uint f = YourReelFrame(p.St, flags);
                if (f != flags) a.MovementFlags = (Agent.MovementControlFlag)f;
            }
            catch (Exception e)
            {
                Failed("impact.rider-reel-gate", e);
                _youReelSlot = -1;
            }
        }

        /// <summary>One frame of YOUR lock (managed - the smoke drives it): Core <see cref="AiInputMath.ReelLock"/>, counted.</summary>
        internal uint YourReelFrame(TrackedAgent st, uint flags)
        {
            int i = _youReelSlot;
            if (i < 0 || i >= _riderReelCount || !ReferenceEquals(_riderReels[i].St, st) || !_riderReels[i].Started) return flags;
            uint f = AiInputMath.ReelLock(flags, _riderReels[i].NoGuard, IsReadyAction(st.PrevAction), out var edit);
            _riderReel.NoteFrame(true, edit);
            return f;
        }

        private void WriteRiderReelSummary()
        {
            _riderReel.LeftOver = _riderReelCount; // released before the summary - 0
            TraxLog.Info("summary", _riderReel.SummaryLine(TimedRiderRules.From(TraxSettings.Shared), ImpactRules.From(TraxSettings.Shared).Applies(ImpactFamily.Riposte)));
        }
    }
}

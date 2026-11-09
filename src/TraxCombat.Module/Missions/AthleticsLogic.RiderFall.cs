using System;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>Step 104c: one applied knock-off / rear, followed until SEEN (a struct in a fixed pool - reads only).</summary>
    internal struct RiderFallWatch
    {
        public TrackedAgent St;
        public RiderFallKind Kind;
        public double At;
        public bool Yours;
    }

    /// <summary>
    /// Step 104c - THE REAR AND THE KNOCK-OFF, LIVE (Toni 2026-11-09, approved "as suggested": "rear on a sharp block, knock-off only on a perfect
    /// one"; DESIGN decision 93 (b) / (c), AI_NOTES "104c - built"). The ladder (<see cref="TimedRiderMath.Decide"/>, the rider dice) already
    /// rolled them after a taken reel - a PERFECT block's knock-off first (30 % x a parry's 50 % x his horse's speed band x the match), then, if he
    /// stayed on, a SHARP block's rear (50 % x the timing from 0.8); never on you (the AI's half-strength timing cannot reach them, and the ladder
    /// never rolls them on you). Applied at the reel's START from the tick (<see cref="StartRiderReel"/>), never in the hit callback:
    ///   - THE KNOCK-OFF: step 53's own proven dismount blow (<see cref="IImpactBody.OwnDismountBlow"/> - CanDismount, 1 damage, owned by the
    ///     blocker); his record goes DOWN at once (<see cref="ImpactState.KnockedDown"/> for the game's fall-and-rise, his reel ended by it) -
    ///     so step 105's 80 % refuses him "down" and the usual model refuses every row "down" while he falls (no double-up), exactly like step 53's
    ///     knock-off; followed until SEEN off (2 s - else his ground time taken back); his reel's locks end "dismounted" on the next tick.
    ///   - THE REAR: our 1-damage blow flagged <c>MakesRear</c> on his HORSE (<see cref="IImpactBody.OwnRearBlow"/> - owned by the horse itself,
    ///     refused below 2 health, its 1 health given back at once), followed until SEEN rearing (1.5 s); the lock stays.
    ///   - Under either the reel's channel-1 animation is NOT played (the engine's own fall / rear pose). Each lever (<see cref="ImpactLever.Dismount"/>,
    ///     <see cref="ImpactLever.Rear"/>) disabled alone on its error.
    /// OPEN (Toni's call, not built): "a HIT on a reeling rider knocks him off" - the seam is <see cref="ImpactMath.Decide"/>'s rider row (step 105
    /// refuses a rider "Mounted"; the reeling bonus is foot-only) - nothing here changes it.
    /// Main thread; a fixed pool; no allocation per event or tick.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly RiderFallWatch[] _riderFallWatches = new RiderFallWatch[TimedRiderFallMath.MaxWatches];
        private int _riderFallWatchCount;
        private readonly TimedRiderFallStats _riderFall = new TimedRiderFallStats();
        private bool _riderFallFirstKnockOff, _riderFallFirstRear, _riderFallFirstSeen, _riderFallFirstRearSeen;

        /// <summary>Step 104c: this mission's knock-offs / rears (the smoke reads them).</summary>
        internal TimedRiderFallStats RiderFallStats => _riderFall;

        /// <summary>Step 104c: followed falls / rears now (the smoke).</summary>
        internal int RiderFallWatches => _riderFallWatchCount;

        /// <summary>From <see cref="StartRiderReel"/> (the tick): the rolled knock-off, else the rolled rear - applied, refused (counted by reason)
        /// and followed. Returns the log words (" - KNOCKED OFF ..."); sets <see cref="RiderReelRec.Fell"/> / <see cref="RiderReelRec.Reared"/>.</summary>
        private string ApplyRiderFall(ref RiderReelRec p, double now)
        {
            var st = p.St;
            bool yours = p.Blocker != null && IsPlayer(p.Blocker);
            if (p.KnockOff)
            {
                var why = KnockOffNow(ref p, now);
                if (why != RiderFallRefusal.None)
                {
                    _riderFall.NoteRefused(RiderFallKind.KnockOff, why);
                    return "; the knock-off REFUSED - " + TimedRiderFallMath.RefusalName(why);
                }
                p.Fell = true;
                _riderFall.NoteApplied(RiderFallKind.KnockOff, yours);
                if (!AddRiderFallWatch(st, RiderFallKind.KnockOff, now, yours)) _riderFall.NoteRefused(RiderFallKind.KnockOff, RiderFallRefusal.WatchFull);
                string w = "; KNOCKED OFF his horse (our dismount blow, CanDismount; his record down " + F2(ImpactMath.VanillaFallRiseSeconds) + " s)";
                if (!_riderFallFirstKnockOff)
                {
                    _riderFallFirstKnockOff = true;
                    TraxLog.Info("impact", "live: first KNOCK-OFF by a perfect timed block (step 104c) - " + Name(st) + " at " + Sec(now) + " s after "
                        + (p.Blocker != null ? Name(p.Blocker) : "(someone)") + "'s block (timing " + F2(p.Score) + ") - step 53's dismount blow from the tick, followed until seen off");
                }
                return w;
            }
            if (p.Rear)
            {
                var why = RearNow(ref p, out double lost);
                if (why != RiderFallRefusal.None)
                {
                    _riderFall.NoteRefused(RiderFallKind.Rear, why);
                    return "; the rear REFUSED - " + TimedRiderFallMath.RefusalName(why);
                }
                p.Reared = true;
                _riderFall.NoteApplied(RiderFallKind.Rear, yours);
                _riderFall.NoteHorseDamage(lost);
                if (!AddRiderFallWatch(st, RiderFallKind.Rear, now, yours)) _riderFall.NoteRefused(RiderFallKind.Rear, RiderFallRefusal.WatchFull);
                string w = "; HIS HORSE REARS (our 1-damage MakesRear blow on it, owned by the horse, its health back - lost " + F2(lost) + ")";
                if (!_riderFallFirstRear)
                {
                    _riderFallFirstRear = true;
                    TraxLog.Info("impact", "live: first REAR by a sharp timed block (step 104c) - " + Name(st) + "'s horse at " + Sec(now) + " s after "
                        + (p.Blocker != null ? Name(p.Blocker) : "(someone)") + "'s block (timing " + F2(p.Score) + ") - the game's own rear flag on a 1-damage blow, followed until seen rearing");
                }
                return w;
            }
            return string.Empty;
        }

        private RiderFallRefusal KnockOffNow(ref RiderReelRec p, double now)
        {
            var st = p.St;
            if (p.You) return RiderFallRefusal.OnYou; // a guard: the ladder never rolls it on you
            if (_live.Disabled(ImpactLever.Dismount) || _live.Disabled(ImpactLever.OwnBlow)) return RiderFallRefusal.LeverOff;
            if (st.Removed || !ImpactBody.IsActive(st)) return RiderFallRefusal.Gone;
            if (!ImpactBody.IsMounted(st)) return RiderFallRefusal.NotRiding;
            bool ok;
            try
            {
                var by = p.Blocker != null && !p.Blocker.Removed ? p.Blocker : null;
                ok = ImpactBody.OwnDismountBlow(st, by, SpikeMath.OwnBlowDamage); // step 53's proven seam
            }
            catch (Exception e)
            {
                DisableImpactLever(ImpactLever.Dismount, "impact.rider-knock-off", e);
                return RiderFallRefusal.EngineRefused;
            }
            if (!ok) return RiderFallRefusal.EngineRefused;
            // down at once, like step 53's knock-off: step 105 and the usual model refuse "down" while he falls; his reel ends with it
            st.Impact.KnockedDown(now, now + ImpactMath.VanillaFallRiseSeconds, ImpactRules.From(TraxSettings.Shared).KnockdownImmunity);
            return RiderFallRefusal.None;
        }

        private RiderFallRefusal RearNow(ref RiderReelRec p, out double lost)
        {
            lost = double.NaN;
            var st = p.St;
            if (p.You) return RiderFallRefusal.OnYou; // a guard: the ladder never rolls it on you
            if (_live.Disabled(ImpactLever.Rear)) return RiderFallRefusal.LeverOff;
            if (st.Removed || !ImpactBody.IsActive(st)) return RiderFallRefusal.Gone;
            try
            {
                if (ImpactBody.OwnRearBlow(st, out var why, out lost)) return RiderFallRefusal.None;
                return why == RiderFallRefusal.None ? RiderFallRefusal.EngineRefused : why;
            }
            catch (Exception e)
            {
                DisableImpactLever(ImpactLever.Rear, "impact.rider-rear", e);
                return RiderFallRefusal.EngineRefused;
            }
        }

        private bool AddRiderFallWatch(TrackedAgent st, RiderFallKind kind, double now, bool yours)
        {
            if (_riderFallWatchCount >= _riderFallWatches.Length) return false;
            _riderFallWatches[_riderFallWatchCount++] = new RiderFallWatch { St = st, Kind = kind, At = now, Yours = yours };
            return true;
        }

        /// <summary>From TickImpact (its own try): each applied knock-off followed until he is off his horse (SEEN) or 2 s pass (his ground time
        /// taken back), each rear until his horse shows the Rear type (SEEN) or 1.5 s pass. Reads only.</summary>
        private void TickRiderFalls(double now)
        {
            if (_riderFallWatchCount == 0) return;
            for (int i = 0; i < _riderFallWatchCount; i++)
            {
                ref var w = ref _riderFallWatches[i];
                var st = w.St;
                if (st == null || st.Removed || !ImpactBody.IsActive(st))
                {
                    _riderFall.WatchesGone++;
                    RemoveRiderFallWatch(i--);
                    continue;
                }
                double age = now - w.At;
                if (w.Kind == RiderFallKind.KnockOff)
                {
                    if (!ImpactBody.IsMounted(st))
                    {
                        _riderFall.NoteSeen(RiderFallKind.KnockOff, age);
                        if (!_riderFallFirstSeen || w.Yours)
                        {
                            _riderFallFirstSeen = true;
                            TraxLog.Info("impact", "rider fall: " + Name(st) + " SEEN off his horse " + F2(age) + " s after the knock-off (step 104c)");
                        }
                        RemoveRiderFallWatch(i--);
                        continue;
                    }
                    if (age <= TimedRiderFallMath.WatchSeconds(RiderFallKind.KnockOff)) continue;
                    _riderFall.NoteNotSeen(RiderFallKind.KnockOff);
                    st.Impact.GotUp(now, 0); // he stayed on: no ground time, no immunity from a fall that never came
                    TraxLog.Info("impact", "rider fall: " + Name(st) + " still on his horse " + F1(age) + " s after the knock-off blow (step 104c) - the engine did not play the dismount");
                    RemoveRiderFallWatch(i--);
                    continue;
                }
                if (ImpactBody.MountRearing(st))
                {
                    _riderFall.NoteSeen(RiderFallKind.Rear, age);
                    if (!_riderFallFirstRearSeen || w.Yours)
                    {
                        _riderFallFirstRearSeen = true;
                        TraxLog.Info("impact", "rider fall: " + Name(st) + "'s horse SEEN rearing " + F2(age) + " s after our rear blow (step 104c)");
                    }
                    RemoveRiderFallWatch(i--);
                    continue;
                }
                if (!ImpactBody.IsMounted(st))
                {
                    _riderFall.WatchesGone++; // off his horse some other way - nothing to follow
                    RemoveRiderFallWatch(i--);
                    continue;
                }
                if (age <= TimedRiderFallMath.WatchSeconds(RiderFallKind.Rear)) continue;
                _riderFall.NoteNotSeen(RiderFallKind.Rear);
                if (w.Yours)
                    TraxLog.Info("impact", "rider fall: " + Name(st) + "'s horse NOT seen rearing within " + F1(age) + " s of our rear blow (step 104c) - standing still? the engine's answer");
                RemoveRiderFallWatch(i--);
            }
        }

        private void RemoveRiderFallWatch(int i)
        {
            _riderFallWatches[i] = _riderFallWatches[--_riderFallWatchCount];
            _riderFallWatches[_riderFallWatchCount] = default;
        }

        /// <summary>He left the field (Forget): out of the followed falls - no engine call.</summary>
        private void RiderFallLeftField(TrackedAgent st)
        {
            for (int i = 0; i < _riderFallWatchCount; i++)
            {
                if (!ReferenceEquals(_riderFallWatches[i].St, st)) continue;
                _riderFall.WatchesGone++;
                RemoveRiderFallWatch(i--);
            }
        }

        /// <summary>The mission end / the behaviour's removal: the followed falls dropped (counted) - reads only, nothing to give back.</summary>
        private void CloseRiderFalls()
        {
            _riderFall.WatchesOpenAtEnd += _riderFallWatchCount;
            for (int i = 0; i < _riderFallWatchCount; i++) _riderFallWatches[i] = default;
            _riderFallWatchCount = 0;
        }

        private void WriteRiderFallSummary()
        {
            TraxLog.Info("summary", _riderFall.SummaryLine(TimedRiderRules.From(TraxSettings.Shared), ImpactRules.From(TraxSettings.Shared).Applies(ImpactFamily.Riposte)));
        }
    }
}

using System;
using System.Diagnostics;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Models;

namespace TraxCombat.Missions
{
    /// <summary>Step 50: one watched reaction of a live kick - a struct in the logic's fixed pool (no allocation).</summary>
    internal struct ImpactLiveWatch
    {
        public TrackedAgent St;
        public ImpactWatchPurpose Purpose;
        public ReactionWatch W;
        public double Factor;
        public bool Slow;
    }

    /// <summary>Step 50: a shield drop decided in a hit callback, made from the tick.</summary>
    internal struct ImpactLiveDrop
    {
        public TrackedAgent St;
        public double At;
        public double Bar;
    }

    /// <summary>Step 50: a shield whose drop was queued - left the hand? picked up again after how long?</summary>
    internal struct ImpactLiveShield
    {
        public TrackedAgent St;
        public int Slot;
        public double QueuedAt;
        public double DroppedAt; // NaN = not yet
        public double NextLook;
    }

    /// <summary>
    /// Step 50 - IMPACT I3, KICKS AND BASHES LIVE (docs/IMPACT.md §3.5's kick row, §3.7, §3.9; AI_NOTES "Step 50"). The model's ONE decision
    /// per landed kick / bash (step 48) is now APPLIED through the spike's proven seams (step 47):
    ///   - KNOCKDOWN: the damage decorator's KnockDown seam answers the decision (<see cref="ImpactModelOnLandedBlow"/>) - never on a rider;
    ///   - SHIELD DROP: queued here, made from the TICK (<see cref="IImpactBody.DropShield"/> - never inside the hit callback; never while he is
    ///     in his own release / bash, vanilla's drop rule); the shield stays on the ground, pickable (answer 3 - vanilla's rules; the AI's
    ///     pickup locked for <c>ShieldPickupLockSeconds</c> by <see cref="TraxItemPickupModel"/>, 0 = never);
    ///   - THE LONGER REEL: his knock-back reel at speed 1 ÷ ReelFactor the tick it shows and again on its _continue, MEASURED with Core's
    ///     <see cref="ReactionWatch"/>; the measured end is his state's reel end (<see cref="ImpactState.EndReel"/>);
    ///   - THE SLOWER GET-UP: his fall-and-rise at 1 ÷ GetUpFactor, measured, the end = <see cref="ImpactState.GotUp"/> (the 5 s immunity runs
    ///     from it).
    /// Each lever behind its own try: an error disables that lever alone for the mission (one [error], one [impact] line). Master switch
    /// first (through <see cref="ImpactRules.Applies"/>): off / shadow / kicks off = nothing queued is played, every watch let go (a slowed
    /// reaction ends by itself). Main thread; fixed pools, no allocation per event or tick.
    /// </summary>
    public sealed partial class AthleticsLogic : IShieldPickupLock
    {
        private readonly ImpactLiveStats _live = new ImpactLiveStats();
        private readonly ImpactLiveWatch[] _liveWatches = new ImpactLiveWatch[ImpactLiveMath.MaxWatches];
        private readonly ImpactLiveDrop[] _liveDrops = new ImpactLiveDrop[ImpactLiveMath.MaxDrops];
        private readonly ImpactLiveShield[] _liveShields = new ImpactLiveShield[ImpactLiveMath.MaxShields];
        private readonly bool[] _liveFirst = new bool[12];
        private int _liveWatchCount;
        private int _liveDropCount;
        private int _liveShieldCount;
        private ActionRead _liveRead;
        private bool _kicksLive;

        /// <summary>This mission's live kicks (the smoke reads them).</summary>
        internal ImpactLiveStats ImpactLiveStats => _live;

        internal int ImpactLiveWatches => _liveWatchCount;
        internal int ImpactLiveDrops => _liveDropCount;
        internal int ImpactLiveShields => _liveShieldCount;

        // ------------------------------------------------------------------ the decision applied (the hit callback)

        /// <summary>
        /// A LIVE kick / bash decision on a man on foot (its family applies): counted; the knockdown answered when the decorator's seam asks
        /// (<paramref name="inSeam"/>), its fall watched (slowed by GetUpFactor); else the reel watched (slowed by ReelFactor); the shield drop
        /// queued for the tick; the YOU line. Returns the KnockDown answer for the seam.
        /// </summary>
        private bool ApplyKick(TrackedAgent a, TrackedAgent v, in ImpactDecision d, bool inSeam, double now)
        {
            _live.NoteDecision(in d);
            bool knockDown = false;
            if (d.Knockdown.Happens)
            {
                if (!inSeam) _live.NoteNotApplied(ImpactOutcome.Knockdown, ImpactNotApplied.OutsideSeam);
                else if (_live.Disabled(ImpactLever.KnockDown)) _live.NoteNotApplied(ImpactOutcome.Knockdown, ImpactNotApplied.LeverOff);
                else
                {
                    knockDown = true;
                    _live.NoteApplied(ImpactOutcome.Knockdown, d.Bar);
                }
                if (knockDown) AddLiveWatch(v, ImpactWatchPurpose.GetUp, WatchKind.Fall, now, ImpactLiveMath.FallAppearSeconds, d.GetUpFactor);
                else v.Impact.GotUp(now, 0); // not on the ground after all: no ground time, no immunity from it
            }
            else if (!double.IsNaN(d.ReelFactor))
            {
                AddLiveWatch(v, ImpactWatchPurpose.Reel, WatchKind.Reaction, now, ImpactLiveMath.ReelAppearSeconds, d.ReelFactor);
            }
            if (d.ShieldDrop.Happens)
            {
                if (_live.Disabled(ImpactLever.ShieldDrop)) _live.NoteNotApplied(ImpactOutcome.ShieldDrop, ImpactNotApplied.LeverOff);
                else if (_liveDropCount >= _liveDrops.Length)
                {
                    _live.WatchesFull++;
                    _live.NoteNotApplied(ImpactOutcome.ShieldDrop, ImpactNotApplied.PoolFull);
                }
                else _liveDrops[_liveDropCount++] = new ImpactLiveDrop { St = v, At = now, Bar = d.Bar };
            }
            if (knockDown || d.ShieldDrop.Happens) _live.ShockGiven++;
            if (d.AttackerIsPlayer || d.DefenderIsPlayer) YouKickLine(a, v, in d, knockDown);
            return knockDown;
        }

        /// <summary>The YOU line (IMPACT §6): every live kick / bash you deal or take - what it did, with its chance, or why nothing.</summary>
        private void YouKickLine(TrackedAgent a, TrackedAgent v, in ImpactDecision d, bool knockDown)
        {
            var c = System.Globalization.CultureInfo.InvariantCulture;
            string what = d.AttackerIsPlayer ? "your " + ImpactMath.EventName(d.Event) + " on " + Name(v) : Name(a) + "'s " + ImpactMath.EventName(d.Event) + " on you";
            string s = "YOU: " + what + " (" + (d.DefenderIsPlayer ? "your" : "his") + " bar " + (100 * d.Bar).ToString("0", c) + "%, x " + d.X.ToString("0.00", c) + "): ";
            string outc = string.Empty;
            if (d.Knockdown.Happens) outc += (knockDown ? "knocked down" : "knocked down by the dice, NOT applied") + " (" + Pct0(d.Knockdown.Chance) + ")";
            if (d.ShieldDrop.Happens) outc += (outc.Length > 0 ? ", " : string.Empty) + "shield dropped (" + Pct0(d.ShieldDrop.Chance) + ")";
            if (outc.Length == 0)
                outc = "no knockdown (" + Pct0(d.Knockdown.Chance) + (d.Knockdown.Refusal != ImpactRefusal.None ? " - " + ImpactMath.RefusalName(d.Knockdown.Refusal) : string.Empty)
                       + "), shield kept (" + (d.ShieldDrop.Refusal == ImpactRefusal.NoShield ? "no shield in hand" : Pct0(d.ShieldDrop.Chance)) + ")";
            if (!double.IsNaN(d.GetUpFactor) && knockDown) outc += " - gets up x" + d.GetUpFactor.ToString("0.00", c) + " slower";
            else if (!double.IsNaN(d.ReelFactor)) outc += " - reels x" + d.ReelFactor.ToString("0.00", c);
            TraxLog.Info("impact", s + outc);
        }

        private static string Pct0(double chance) => (100 * chance).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%";

        private void AddLiveWatch(TrackedAgent st, ImpactWatchPurpose purpose, WatchKind kind, double now, double appearWithin, double factor)
        {
            if (_live.Disabled(ImpactLever.Watch)) return;
            int i = 0;
            for (; i < _liveWatchCount; i++) if (ReferenceEquals(_liveWatches[i].St, st)) break; // one watch a man: a newer kick replaces it
            if (i == _liveWatchCount)
            {
                if (_liveWatchCount >= _liveWatches.Length)
                {
                    _live.WatchesFull++;
                    return;
                }
                _liveWatchCount++;
            }
            ref var w = ref _liveWatches[i];
            w.St = st;
            w.Purpose = purpose;
            w.Factor = double.IsNaN(factor) ? 1 : factor;
            w.Slow = ImpactLiveMath.NeedsSlow(factor) && !_live.Disabled(ImpactLever.SlowReaction);
            w.W.Begin(kind, now, appearWithin);
        }

        private void RemoveLiveWatch(int i)
        {
            _liveWatches[i] = _liveWatches[--_liveWatchCount];
            _liveWatches[_liveWatchCount] = default;
        }

        /// <summary>A lever's error: logged ONCE with its stack, the lever off for the rest of the mission, the others run on.</summary>
        private void DisableImpactLever(ImpactLever lever, string site, Exception e)
        {
            Failed(site, e);
            if (!_live.Disable(lever, site)) return;
            if (lever == ImpactLever.Watch) ClearLiveWatches();
            if (lever == ImpactLever.ShieldDrop) DropPendingDrops(ImpactNotApplied.LeverOff);
            TraxLog.Info("impact", "live lever " + ImpactLiveMath.LeverName(lever) + " disabled for this mission after an error at " + site
                + " - the other levers run on (tell Claude: the [error] line above has the stack)");
        }

        // ------------------------------------------------------------------ the tick

        /// <summary>From TickImpact (own try): kicks live? (the master switch first, through Applies) - not = every queued drop and watch let go
        /// at once; else the queued drops (≤ <see cref="ImpactLiveMath.MaxActionsPerTick"/>), the watches, the shields.</summary>
        internal void TickImpactLive(double now)
        {
            long start = Stopwatch.GetTimestamp();
            bool live = ImpactRules.From(TraxSettings.Shared).Applies(ImpactFamily.Kicks);
            if (live != _kicksLive)
            {
                _kicksLive = live;
                if (!live && (_liveDropCount > 0 || _liveWatchCount > 0))
                {
                    int drops = _liveDropCount, watches = _liveWatchCount;
                    DropPendingDrops(ImpactNotApplied.LeverOff);
                    ClearLiveWatches();
                    TraxLog.Info("impact", "kicks no longer live at " + Sec(now) + " s (" + ImpactRules.From(TraxSettings.Shared).ModeText() + "): " + drops
                        + " queued shield drop(s) dropped unplayed, " + watches + " watch(es) let go - a slowed reaction ends by itself");
                }
            }
            if (!live) return;
            int calls = 0;
            if (_liveDropCount > 0) RunLiveDrops(now, ref calls);
            if (_liveWatchCount > 0) TickLiveWatches(now);
            if (_liveShieldCount > 0) TickLiveShields(now);
            _live.AddTick((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
        }

        private void DropPendingDrops(ImpactNotApplied why)
        {
            for (int i = 0; i < _liveDropCount; i++)
            {
                _live.NoteNotApplied(ImpactOutcome.ShieldDrop, why);
                _liveDrops[i] = default;
            }
            _liveDropCount = 0;
        }

        private void ClearLiveWatches()
        {
            for (int i = 0; i < _liveWatchCount; i++) _liveWatches[i] = default;
            _liveWatchCount = 0;
        }

        /// <summary>The queued drops: his shield in hand now → DropShield (vanilla's queued DropItem), followed; not while he is in his own
        /// release / bash (waits up to 1 s); the per-tick cap.</summary>
        private void RunLiveDrops(double now, ref int calls)
        {
            int keep = 0;
            for (int i = 0; i < _liveDropCount; i++)
            {
                var p = _liveDrops[i];
                if (_live.Disabled(ImpactLever.ShieldDrop))
                {
                    _live.NoteNotApplied(ImpactOutcome.ShieldDrop, ImpactNotApplied.LeverOff);
                    continue;
                }
                if (calls >= ImpactLiveMath.MaxActionsPerTick)
                {
                    _live.CallsDeferred++;
                    _liveDrops[keep++] = p;
                    continue;
                }
                if (p.St == null || p.St.Removed || !ImpactBody.IsActive(p.St))
                {
                    _live.NoteNotApplied(ImpactOutcome.ShieldDrop, ImpactNotApplied.LeftField);
                    continue;
                }
                try
                {
                    ImpactBody.Read(p.St, ref _liveRead);
                    if (ImpactActions.RefusesDrop(_liveRead.Type1))
                    {
                        if (now - p.At < ImpactLiveMath.BusyWaitSeconds) _liveDrops[keep++] = p; // vanilla never drops during his own release / bash
                        else _live.NoteNotApplied(ImpactOutcome.ShieldDrop, ImpactNotApplied.Busy);
                        continue;
                    }
                    if (!ImpactBody.TryShieldInHand(p.St, out int slot))
                    {
                        _live.NoteNotApplied(ImpactOutcome.ShieldDrop, ImpactNotApplied.NoShieldAtTick);
                        continue;
                    }
                    calls++;
                    if (!ImpactBody.DropShield(p.St, slot))
                    {
                        _live.NoteNotApplied(ImpactOutcome.ShieldDrop, ImpactNotApplied.LeverOff);
                        continue;
                    }
                    _live.DropsQueued++;
                    _live.NoteApplied(ImpactOutcome.ShieldDrop, p.Bar);
                    if (_liveShieldCount < _liveShields.Length)
                        _liveShields[_liveShieldCount++] = new ImpactLiveShield { St = p.St, Slot = slot, QueuedAt = now, DroppedAt = double.NaN, NextLook = now };
                    else _live.WatchesFull++;
                    if (LiveFirst(0))
                        TraxLog.Info("impact", "live: first shield drop made from the tick at " + Sec(now) + " s - " + Name(p.St) + "'s shield in slot " + slot + ", "
                            + F2(now - p.At) + " s after the kick (DropItem queued the vanilla way; it stays on the ground, pickable"
                            + (TraxSettings.Shared.ShieldPickupLockSeconds > 0 ? " - the AI's pickup locked " + F1(TraxSettings.Shared.ShieldPickupLockSeconds) + " s" : string.Empty) + ")");
                }
                catch (Exception e)
                {
                    DisableImpactLever(ImpactLever.ShieldDrop, "impact.live-drop", e);
                    return; // DisableImpactLever dropped what waited
                }
            }
            for (int i = keep; i < _liveDropCount; i++) _liveDrops[i] = default;
            _liveDropCount = keep;
        }

        private void TickLiveWatches(double now)
        {
            if (_live.Disabled(ImpactLever.Watch)) return;
            for (int i = 0; i < _liveWatchCount; i++)
            {
                ref var w = ref _liveWatches[i];
                var st = w.St;
                if (st == null || st.Removed || !ImpactBody.IsActive(st))
                {
                    _live.LeftField++;
                    RemoveLiveWatch(i--);
                    continue;
                }
                WatchEvent ev;
                double length;
                try
                {
                    ImpactBody.Read(st, ref _liveRead);
                    ev = w.W.Observe(now, in _liveRead, out length);
                }
                catch (Exception e)
                {
                    DisableImpactLever(ImpactLever.Watch, "impact.live-watch", e); // the states keep the model's estimates
                    return;
                }
                switch (ev)
                {
                    case WatchEvent.Appeared:
                        if (w.Purpose == ImpactWatchPurpose.GetUp)
                        {
                            _live.NoteSeen(ImpactOutcome.Knockdown);
                            if (LiveFirst(1))
                                TraxLog.Info("impact", "live: first knockdown seen at " + Sec(now) + " s - " + Name(st) + " fell " + F2(now - w.W.Since) + " s after the kick (channel " + w.W.Channel
                                    + ", " + ImpactActions.Name(w.W.FirstType) + ")" + (w.Slow ? " - his fall-and-rise slowed x" + F2(w.Factor) : " - untouched (x1)"));
                        }
                        else if (ImpactActions.IsFall(w.W.FirstType)) _live.FellInstead++;
                        if (w.Slow) LiveSlow(ref w, now);
                        break;
                    case WatchEvent.NewAction:
                        if (w.Slow && LiveSlow(ref w, now)) _live.SlowReapplied++;
                        break;
                    case WatchEvent.Ended:
                    case WatchEvent.Capped:
                        if (ev == WatchEvent.Capped) _live.Capped++;
                        LiveMeasured(ref w, length, now);
                        RemoveLiveWatch(i--);
                        break;
                    case WatchEvent.NeverAppeared:
                        LiveNeverAppeared(ref w, now);
                        RemoveLiveWatch(i--);
                        break;
                }
            }
        }

        /// <summary>The reaction now on the watch's channel at 1 ÷ factor (own try - an error disables the slow lever only; this reaction plays
        /// on at its own speed and is measured as untouched).</summary>
        private bool LiveSlow(ref ImpactLiveWatch w, double now)
        {
            if (_live.Disabled(ImpactLever.SlowReaction))
            {
                w.Slow = false;
                return false;
            }
            try
            {
                float speed = ImpactLiveMath.SpeedFor(w.Factor);
                ImpactBody.SlowReaction(w.St, w.W.Channel, speed);
                _live.SlowCalls++;
                if (LiveFirst(2))
                    TraxLog.Info("impact", "live: first " + (w.Purpose == ImpactWatchPurpose.GetUp ? "fall-and-rise" : "reel") + " slowed at " + Sec(now) + " s - " + Name(w.St) + "'s "
                        + ImpactActions.Name(_liveRead.Type(w.W.Channel)) + " on channel " + w.W.Channel + " at speed " + F2(speed) + " (x" + F2(w.Factor) + " longer) the tick it showed");
                return true;
            }
            catch (Exception e)
            {
                w.Slow = false;
                DisableImpactLever(ImpactLever.SlowReaction, "impact.live-slow", e);
                return false;
            }
        }

        /// <summary>It ended: the length counted (asked vs measured), and the END written into his state - the reel's immunity and the
        /// get-up's 5 s run from what the engine really played.</summary>
        private void LiveMeasured(ref ImpactLiveWatch w, double length, double now)
        {
            var r = ImpactRules.From(TraxSettings.Shared);
            bool slowed = w.Slow && ImpactLiveMath.NeedsSlow(w.Factor);
            if (w.Purpose == ImpactWatchPurpose.GetUp)
            {
                _live.NoteGetUp(w.Factor, length, slowed);
                w.St.Impact.GotUp(now, r.KnockdownImmunity);
                if (LiveFirst(3))
                    TraxLog.Info("impact", "live: first get-up measured at " + Sec(now) + " s - " + Name(w.St) + " was down " + F2(length) + " s (asked x" + F2(w.Factor)
                        + "); no new knockdown on him for " + F1(r.KnockdownImmunity) + " s");
                return;
            }
            if (ImpactActions.IsFall(w.W.FirstType)) return; // not a reel (the engine knocked him down itself)
            _live.NoteReel(w.Factor, length, slowed);
            if (ImpactLiveMath.NeedsSlow(w.Factor)) w.St.Impact.EndReel(now, r.StaggerImmunity); // the model set a reel only for a lengthened one
            if (slowed && LiveFirst(4))
                TraxLog.Info("impact", "live: first slowed reel measured at " + Sec(now) + " s - " + Name(w.St) + "'s " + ImpactActions.Name(w.W.FirstType) + " lasted " + F2(length)
                    + " s (asked x" + F2(w.Factor) + "; untouched reels so far " + (_live.ReelControl.Count > 0 ? F2(_live.ReelControl.Mean) + " s" : "none") + ")");
        }

        private void LiveNeverAppeared(ref ImpactLiveWatch w, double now)
        {
            if (w.Purpose == ImpactWatchPurpose.GetUp)
            {
                _live.FallNotSeen++;
                w.St.Impact.GotUp(now, 0); // he never went down: no immunity from a fall that did not happen
                if (LiveFirst(5))
                    TraxLog.Info("impact", "live: a knockdown answered at " + Sec(w.W.Since) + " s showed NO Fall within " + F1(ImpactLiveMath.FallAppearSeconds) + " s on " + Name(w.St)
                        + " (channel 0 " + ImpactActions.Name(_liveRead.Type0) + ", channel 1 " + ImpactActions.Name(_liveRead.Type1) + ") - tell Claude if this repeats");
                return;
            }
            _live.ReelNotSeen++;
            if (ImpactLiveMath.NeedsSlow(w.Factor)) w.St.Impact.EndReel(now, 0); // no reel played - not reeling, no immunity
        }

        // ------------------------------------------------------------------ the shields

        private void TickLiveShields(double now)
        {
            for (int i = 0; i < _liveShieldCount; i++)
            {
                ref var s = ref _liveShields[i];
                if (now < s.NextLook) continue;
                s.NextLook = now + ImpactLiveMath.ShieldLookSeconds;
                try
                {
                    bool gone = s.St == null || s.St.Removed || !ImpactBody.IsActive(s.St);
                    if (double.IsNaN(s.DroppedAt))
                    {
                        if (gone)
                        {
                            _live.LeftField++;
                            RemoveLiveShield(i--);
                        }
                        else if (!ImpactBody.SlotHoldsShield(s.St!, s.Slot))
                        {
                            s.DroppedAt = now;
                            s.St!.ShieldDroppedAt = now;
                            _live.Dropped++;
                            _live.NoteSeen(ImpactOutcome.ShieldDrop);
                            if (LiveFirst(6))
                                TraxLog.Info("impact", "live: first shield left the hand at " + Sec(now) + " s - " + Name(s.St) + ", " + F2(now - s.QueuedAt) + " s after the queued drop");
                        }
                        else if (now - s.QueuedAt >= ImpactLiveMath.DropSeenSeconds)
                        {
                            _live.DropNotSeen++;
                            RemoveLiveShield(i--);
                        }
                        continue;
                    }
                    if (gone)
                    {
                        _live.DropperLeftField++;
                        RemoveLiveShield(i--);
                    }
                    else if (ImpactBody.HasAnyShield(s.St!))
                    {
                        _live.PickedUp++;
                        _live.PickupSeconds.Add(now - s.DroppedAt);
                        if (LiveFirst(7))
                            TraxLog.Info("impact", "live: first knocked-away shield picked up again at " + Sec(now) + " s - " + Name(s.St!) + ", " + F1(now - s.DroppedAt) + " s after it fell");
                        RemoveLiveShield(i--);
                    }
                    else if (now - s.DroppedAt >= ImpactLiveMath.PickupWindowSeconds)
                    {
                        _live.StillOnGround++;
                        RemoveLiveShield(i--);
                    }
                }
                catch (Exception e)
                {
                    DisableImpactLever(ImpactLever.ShieldDrop, "impact.live-shield", e);
                    for (int k = 0; k < _liveShieldCount; k++) _liveShields[k] = default;
                    _liveShieldCount = 0;
                    return;
                }
            }
        }

        private void RemoveLiveShield(int i)
        {
            _liveShields[i] = _liveShields[--_liveShieldCount];
            _liveShields[_liveShieldCount] = default;
        }

        /// <summary>IShieldPickupLock (the pickup decorator, main thread): an AI man whose shield a live kick knocked away less than
        /// <c>ShieldPickupLockSeconds</c> ago may not pick a shield up yet (0 = never locked - vanilla; the master switch first). Counted.</summary>
        bool IShieldPickupLock.ShieldPickupLocked(Agent agent)
        {
            var st = Get(agent);
            return st != null && ShieldPickupLocked(st, SpikeNow);
        }

        /// <summary>The lock's rule on a tracked man (the smoke drives it).</summary>
        internal bool ShieldPickupLocked(TrackedAgent st, double now)
        {
            var s = TraxSettings.Shared;
            if (!s.ModEnabled || IsPlayer(st)) return false;
            if (!ImpactLiveMath.PickupLocked(now, st.ShieldDroppedAt, s.ShieldPickupLockSeconds)) return false;
            _live.PickupsLocked++;
            return true;
        }

        // ------------------------------------------------------------------ left the field, the end

        /// <summary>He left the field (Forget): out of every live watch, drop and shield - no engine call on him.</summary>
        private void ImpactLiveLeftField(TrackedAgent st)
        {
            for (int i = 0; i < _liveWatchCount; i++)
            {
                if (!ReferenceEquals(_liveWatches[i].St, st)) continue;
                _live.LeftField++;
                RemoveLiveWatch(i--);
            }
            int keep = 0;
            for (int i = 0; i < _liveDropCount; i++)
            {
                if (ReferenceEquals(_liveDrops[i].St, st)) _live.NoteNotApplied(ImpactOutcome.ShieldDrop, ImpactNotApplied.LeftField);
                else _liveDrops[keep++] = _liveDrops[i];
            }
            for (int i = keep; i < _liveDropCount; i++) _liveDrops[i] = default;
            _liveDropCount = keep;
            for (int i = 0; i < _liveShieldCount; i++)
            {
                if (!ReferenceEquals(_liveShields[i].St, st)) continue;
                if (double.IsNaN(_liveShields[i].DroppedAt)) _live.LeftField++;
                else _live.DropperLeftField++;
                RemoveLiveShield(i--);
            }
        }

        /// <summary>Mission end: every pool let go (shields on the ground counted), the summary lines.</summary>
        private void WriteImpactLiveSummary()
        {
            for (int i = 0; i < _liveShieldCount; i++)
            {
                if (!double.IsNaN(_liveShields[i].DroppedAt)) _live.StillOnGround++;
                _liveShields[i] = default;
            }
            _liveShieldCount = 0;
            DropPendingDrops(ImpactNotApplied.LeftField);
            ClearLiveWatches();
            var r = ImpactRules.From(TraxSettings.Shared);
            foreach (var line in _live.SummaryLines(r.Applies(ImpactFamily.Kicks), r.ModeText())) TraxLog.Info("summary", line);
        }

        /// <summary>The first of each of the live part's first-time lines (true once).</summary>
        private bool LiveFirst(int i)
        {
            if (_liveFirst[i]) return false;
            _liveFirst[i] = true;
            return true;
        }
    }
}

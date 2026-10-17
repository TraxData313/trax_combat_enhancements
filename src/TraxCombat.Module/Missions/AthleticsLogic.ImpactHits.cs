using System;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Models;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 52 - IMPACT I5, CLEAN HITS LIVE + THE REELING BONUS + MOMENTUM (docs/IMPACT.md §3.2, §3.5's clean-hit row, §3.7, §3.9; AI_NOTES
    /// "Step 52"). The model's ONE decision per clean hit (step 48 - made in the damage decorator's KnockDown seam) is APPLIED through the
    /// kicks' proven path (step 50):
    ///   - KNOCKDOWN: the decorator's KnockDown seam answers the decision (OR-ed with vanilla's), its Fall watched and slowed by the decided
    ///     get-up factor, the measured end = <see cref="ImpactState.GotUp"/> (the 5 s immunity from it) - never on a rider (step 53);
    ///   - STAGGER: the engine's OWN flinch slowed (WatchKind.Reaction) to the decided length - its natural length LEARNED from the measured
    ///     flinches (the untouched controls and the slowed ones × their speed); the measured end into <see cref="ImpactState.EndReel"/> (the
    ///     reeling bonus for the next blow, the 2 s immunity) - not a forced stagger;
    ///   - TIRED MEN NEVER SHRUG OFF (<c>ImpactTiredNeverShrugsOff</c>): the damage decorator's <c>DecideAgentShrugOffBlow</c> (MAIN thread -
    ///     Mission.CreateMeleeBlow) asks <see cref="IShrugOffDecider.ShrugOffKept"/> when the game says "shrugged off": the game's threshold ×
    ///     (1 − T) - a tired man flinches.
    /// MOMENTUM (every family): the attacker's speed into the blow is an input of every decision (<see cref="ImpactFacts.AttackerSpeed"/>,
    /// the game's reads in <see cref="GameImpactInputs.SpeedInto"/>); the summary counts every decided blow by his speed band.
    /// Each lever disabled alone by its error (KnockDown / SlowReaction / Watch shared with the kicks; ShrugOff its own). Master switch first
    /// (through <see cref="ImpactRules.Applies"/>): off / shadow / the spike / hits off = nothing answered, the hits' watches let go, every
    /// shrug-off the game's. Main thread; the shared fixed pools, no allocation per event or tick.
    /// </summary>
    public sealed partial class AthleticsLogic : IShrugOffDecider
    {
        private readonly ImpactHitLiveStats _hits = new ImpactHitLiveStats();
        private readonly bool[] _hitsFirst = new bool[12];
        private FlinchNatural _flinchNatural;
        private bool _hitsLive;
        private int _hitControls;
        private bool _seamShrugOff;

        /// <summary>This mission's live clean hits and the momentum of every decided blow (the smoke reads them).</summary>
        internal ImpactHitLiveStats ImpactHitStats => _hits;

        /// <summary>The engine's flinch length at speed 1 as learned so far (the smoke).</summary>
        internal double FlinchNaturalSeconds => _flinchNatural.Seconds;

        // ------------------------------------------------------------------ the shrug-off seam (MAIN thread)

        bool IShrugOffDecider.ShrugOffKept(Agent victim, in AttackCollisionData collision, float gameThreshold)
        {
            if (!_spikeStarted || _spikeClosed || victim == null) return true;
            if (!ImpactEventOf(in collision, out var ev) || ev != ImpactEvent.CleanHit) return true; // a clean melee hit only (never a missile, a kick, a block)
            return ShrugOffKept(Get(victim), collision.InflictedDamage, gameThreshold, SpikeNow);
        }

        /// <summary>
        /// The rule on a tracked man (the smoke drives it): the master switch first (through Applies(Hits)), the switch, on foot (a rider:
        /// step 53) - then the game's threshold × (1 − T): still at or above the damage = kept, else REFUSED (he flinches). Own try: an error =
        /// the game's answer, the lever off for the mission.
        /// </summary>
        internal bool ShrugOffKept(TrackedAgent? st, double damage, double gameThreshold, double now)
        {
            try
            {
                if (_live.Disabled(ImpactLever.ShrugOff)) return true;
                var r = ImpactRules.From(TraxSettings.Shared);
                if (!r.TiredNeverShrugsOff || !r.Applies(ImpactFamily.Hits)) return true;
                if (st == null || st.Removed || ImpactBody.IsMounted(st)) return true;
                var ar = Rules;
                double tired = ImpactHitMath.ShrugTired(AthleticsMath.PeakShare(in ar, st), IsPlayer(st), r.OnPlayer);
                if (!(tired > 0)) return true; // in the green: the game's own threshold (nothing to count)
                bool kept = ImpactHitMath.StillShrugsOff(damage, gameThreshold, tired);
                _hits.NoteShrug(st.Fraction, !kept);
                if (!kept && HitsFirst(0))
                    TraxLog.Info("impact", "live: first shrug-off REFUSED at " + Sec(now) + " s - " + Name(st) + " (bar " + Pct0(st.Fraction) + ", T " + F2(tired) + ") took " + F1(damage)
                        + " damage, the game's threshold " + F1(gameThreshold) + " x (1 - T) = " + F1(ImpactHitMath.ShrugOffThreshold(gameThreshold, tired)) + " - a tired man flinches (ImpactTiredNeverShrugsOff)");
                return kept;
            }
            catch (Exception e)
            {
                DisableImpactLever(ImpactLever.ShrugOff, "impact.live-shrug-off", e);
                return true;
            }
        }

        // ------------------------------------------------------------------ the decision applied (the decorator's seam / the hit callback)

        /// <summary>
        /// A LIVE clean-hit decision on a man on foot (clean hits apply): counted by weapon class and band; the knockdown answered when the
        /// decorator's seam asks (<paramref name="inSeam"/>), its fall watched (slowed by GetUpFactor); else the stagger = his own flinch
        /// watched and slowed to the decided length; no outcome = a few untouched flinches timed (the control, the natural length learned);
        /// the YOU line. Returns the KnockDown answer for the seam.
        /// </summary>
        private bool ApplyHit(TrackedAgent a, TrackedAgent v, in ImpactDecision d, in ImpactFacts f, bool inSeam, double now)
        {
            int cls = ImpactHitMath.ClassOf(in f);
            _hits.NoteDecision(in d, cls);
            bool knockDown = false, staggered = false;
            if (d.Knockdown.Happens)
            {
                if (!inSeam) _hits.NoteNotApplied(0, ImpactNotApplied.OutsideSeam);
                else if (_live.Disabled(ImpactLever.KnockDown)) _hits.NoteNotApplied(0, ImpactNotApplied.LeverOff);
                else
                {
                    knockDown = true;
                    _hits.NoteApplied(cls, d.Bar, 0);
                    if (_seamShrugOff) _hits.KnockdownOnShrugOff++;
                }
                if (knockDown)
                {
                    if (!AddLiveWatch(v, ImpactWatchPurpose.HitGetUp, WatchKind.Fall, now, ImpactLiveMath.FallAppearSeconds, d.GetUpFactor, ev: ImpactEvent.CleanHit)) _hits.WatchesFull++;
                }
                else v.Impact.GotUp(now, 0); // not on the ground after all: no ground time, no immunity from it
            }
            else if (d.Stagger.Happens)
            {
                double factor = ImpactHitMath.FlinchFactor(d.StaggerSeconds, _flinchNatural.Seconds);
                if (_live.Disabled(ImpactLever.SlowReaction) || _live.Disabled(ImpactLever.Watch)) _hits.NoteNotApplied(1, ImpactNotApplied.LeverOff);
                else if (AddLiveWatch(v, ImpactWatchPurpose.HitFlinch, WatchKind.Reaction, now, ImpactHitMath.FlinchAppearSeconds, factor, ev: ImpactEvent.CleanHit, asked: d.StaggerSeconds))
                {
                    staggered = true;
                    _hits.NoteApplied(cls, d.Bar, 1);
                }
                else
                {
                    _hits.WatchesFull++;
                    _hits.NoteNotApplied(1, ImpactNotApplied.PoolFull);
                }
                if (!staggered && v.Impact.Reeling(now)) v.Impact.EndReel(now, 0); // not played: no reeling, no immunity from it
            }
            else if (_hitControls < ImpactHitMath.MaxControlWatches)
            {
                AddLiveWatch(v, ImpactWatchPurpose.HitControl, WatchKind.Reaction, now, ImpactHitMath.FlinchAppearSeconds, 1, replace: false, ev: ImpactEvent.CleanHit);
            }
            if (knockDown) ImpactMessage(ImpactMessageKind.KnockedDown, a, v); // step 53: the on-screen line
            if (knockDown || staggered) _hits.ShockGiven++;
            if ((knockDown || staggered) && (d.AttackerIsPlayer || d.DefenderIsPlayer)) YouHitLine(a, v, in d, knockDown);
            if ((knockDown || staggered) && HitsFirst(knockDown ? 1 : 2))
                TraxLog.Info("impact", "live: first clean hit that " + (knockDown ? "KNOCKS DOWN" : "STAGGERS") + " at " + Sec(now) + " s - " + Name(a) + "'s " + ImpactHitMath.ClassName(cls) + " blow on " + Name(v)
                    + " (his bar " + Pct0(d.Bar) + ", x " + F2(d.X) + (d.Reeling ? ", REELING +" + F2(ImpactRules.From(TraxSettings.Shared).StaggeredBonus) : string.Empty)
                    + (d.Momentum > 1.0001 ? ", momentum x" + F2(d.Momentum) + " at " + F1(d.AttackerSpeed) + " m/s" : string.Empty) + "): "
                    + (knockDown ? "KnockDown answered in the decorator's seam - his fall-and-rise to be slowed x" + F2(d.GetUpFactor)
                       : "his own flinch to be slowed to " + F2(d.StaggerSeconds) + " s (x" + F2(ImpactHitMath.FlinchFactor(d.StaggerSeconds, _flinchNatural.Seconds)) + " the flinch's "
                         + F2(_flinchNatural.Seconds) + " s - " + (_flinchNatural.Count > 0 ? "measured" : "a guess until one is measured") + ")"));
            return knockDown;
        }

        /// <summary>The YOU line (IMPACT §6): a live clean hit that did something to you, or that you did to someone.</summary>
        private void YouHitLine(TrackedAgent a, TrackedAgent v, in ImpactDecision d, bool knockDown)
        {
            int o = knockDown ? 0 : 1;
            var roll = knockDown ? d.Knockdown : d.Stagger;
            string what = d.DefenderIsPlayer ? Name(a) + "'s blow on you: " + ImpactHitMath.YouOutcome(o) : "your blow on " + Name(v) + ": " + ImpactHitMath.HisOutcome(o);
            string len = knockDown ? " - gets up x" + F2(d.GetUpFactor) + " slower" : " - " + F2(d.StaggerSeconds) + " s";
            TraxLog.Info("impact", "YOU: " + what + " (" + Pct0(roll.Chance) + "; " + (d.DefenderIsPlayer ? "your" : "his") + " bar " + Pct0(d.Bar) + ", x " + F2(d.X)
                + (d.Reeling ? ", reeling" : string.Empty) + (d.Momentum > 1.0001 ? ", momentum x" + F2(d.Momentum) : string.Empty) + ")" + len);
        }

        // ------------------------------------------------------------------ the tick: live or not

        /// <summary>From TickImpactLive: clean hits live? (the master switch first, through Applies) - not = the hits' watches let go at once.</summary>
        private bool TickHitsSwitch(double now, in ImpactRules r)
        {
            bool live = r.Applies(ImpactFamily.Hits);
            if (live == _hitsLive) return live;
            _hitsLive = live;
            if (!live)
            {
                int watches = LetGoHitWatches();
                if (watches > 0)
                    TraxLog.Info("impact", "clean hits no longer live at " + Sec(now) + " s (" + r.ModeText() + "): " + watches + " watch(es) let go - a slowed flinch / fall ends by itself");
            }
            return live;
        }

        private static bool IsHitPurpose(ImpactWatchPurpose p) => p == ImpactWatchPurpose.HitFlinch || p == ImpactWatchPurpose.HitGetUp || p == ImpactWatchPurpose.HitControl;

        private int LetGoHitWatches()
        {
            int n = 0;
            for (int i = 0; i < _liveWatchCount; i++)
            {
                if (!IsHitPurpose(_liveWatches[i].Purpose)) continue;
                var w = _liveWatches[i];
                if (w.Purpose == ImpactWatchPurpose.HitFlinch && w.St != null && w.St.Impact.Reeling(SpikeNow) && !w.W.Appeared) w.St.Impact.EndReel(SpikeNow, 0);
                RemoveLiveWatch(i--);
                n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ the watches (from TickLiveWatches)

        private void HitWatchAppeared(ref ImpactLiveWatch w, double now)
        {
            switch (w.Purpose)
            {
                case ImpactWatchPurpose.HitGetUp:
                    _hits.NoteSeen(0);
                    if (HitsFirst(3))
                        TraxLog.Info("impact", "live: first clean-hit knockdown seen at " + Sec(now) + " s - " + Name(w.St) + " fell " + F2(now - w.W.Since) + " s after the blow ("
                            + ImpactActions.Name(w.W.FirstType) + ")" + (w.Slow ? " - his fall-and-rise slowed x" + F2(w.Factor) : " - untouched (x1)"));
                    break;
                case ImpactWatchPurpose.HitFlinch:
                    if (ImpactActions.IsFall(w.W.FirstType))
                    {
                        _hits.FellInstead++;
                        w.Slow = false; // the game knocked him down itself - his fall is not our flinch
                        break;
                    }
                    _hits.NoteSeen(1);
                    if (HitsFirst(4))
                        TraxLog.Info("impact", "live: first clean-hit flinch seen at " + Sec(now) + " s - " + Name(w.St) + "'s " + ImpactActions.Name(w.W.FirstType) + " on channel " + w.W.Channel
                            + ", " + F2(now - w.W.Since) + " s after the blow" + (w.Slow ? " - slowed to speed " + F2(ImpactLiveMath.SpeedFor(w.Factor)) + " (x" + F2(w.Factor) + ")" : " - x1 (not slowed)"));
                    break;
            }
        }

        private void HitWatchMeasured(ref ImpactLiveWatch w, double length, double now, in ImpactRules r, bool slowed)
        {
            switch (w.Purpose)
            {
                case ImpactWatchPurpose.HitGetUp:
                    _hits.NoteGetUp(w.Factor, length, slowed);
                    w.St.Impact.GotUp(now, r.KnockdownImmunity);
                    break;
                case ImpactWatchPurpose.HitFlinch:
                    if (ImpactActions.IsFall(w.W.FirstType))
                    {
                        w.St.Impact.EndReel(now, 0); // the game's own knockdown, not our stagger
                        break;
                    }
                    _hits.NoteFlinch(w.Asked, w.Factor, length, slowed);
                    _flinchNatural.Add(length, slowed ? ImpactLiveMath.SpeedFor(w.Factor) : 1f);
                    w.St.Impact.EndReel(now, r.StaggerImmunity); // the reeling bonus and the 2 s immunity from what the engine really played
                    if (HitsFirst(5))
                        TraxLog.Info("impact", "live: first clean-hit stagger measured at " + Sec(now) + " s - " + Name(w.St) + "'s flinch lasted " + F2(length) + " s (asked " + F2(w.Asked)
                            + " s, x" + F2(w.Factor) + "); he counts as REELING until then - the next blow on him gets the +" + F2(r.StaggeredBonus) + " - and no new stagger for " + F1(r.StaggerImmunity) + " s");
                    break;
                case ImpactWatchPurpose.HitControl:
                    if (ImpactActions.IsFall(w.W.FirstType)) break;
                    _hits.FlinchControl.Add(length);
                    _flinchNatural.Add(length, 1f);
                    break;
            }
        }

        private void HitWatchNeverAppeared(ref ImpactLiveWatch w, double now)
        {
            switch (w.Purpose)
            {
                case ImpactWatchPurpose.HitGetUp:
                    _hits.FallNotSeen++;
                    w.St.Impact.GotUp(now, 0);
                    if (HitsFirst(6))
                        TraxLog.Info("impact", "live: a clean-hit knockdown answered at " + Sec(w.W.Since) + " s showed NO Fall within " + F1(ImpactLiveMath.FallAppearSeconds) + " s on " + Name(w.St)
                            + " (channel 0 " + ImpactActions.Name(_liveRead.Type0) + ", channel 1 " + ImpactActions.Name(_liveRead.Type1) + ") - tell Claude if this repeats");
                    break;
                case ImpactWatchPurpose.HitFlinch:
                    _hits.FlinchNotSeen++;
                    w.St.Impact.EndReel(now, 0); // no flinch played - not reeling, no immunity
                    break;
                case ImpactWatchPurpose.HitControl:
                    _hits.ControlNotSeen++;
                    break;
            }
        }

        /// <summary>The first of each of the hits' first-time lines (true once).</summary>
        private bool HitsFirst(int i)
        {
            if (_hitsFirst[i]) return false;
            _hitsFirst[i] = true;
            return true;
        }
    }
}

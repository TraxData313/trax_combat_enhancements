using System;
using System.Threading;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Models;

namespace TraxCombat.Missions
{
    /// <summary>Step 51: what a queued block action does at the tick.</summary>
    internal enum ImpactBlockAction : byte
    {
        /// <summary>The blocker's rolled stagger (act_stagger_* at the speed for its length).</summary>
        Stagger,

        /// <summary>The riposte: the ATTACKER's forced stagger.</summary>
        Riposte,

        /// <summary>The blocker knocked down: a blow of our own (lever 6).</summary>
        OwnBlow,
    }

    /// <summary>Step 51: a block's outcome decided in the hit callback, made from the next tick - a struct in the logic's fixed pool.</summary>
    internal struct ImpactBlockPending
    {
        public ImpactBlockAction Action;

        /// <summary>The man it happens to (the blocker; the ATTACKER for a riposte) and the other one (the blow's owner for our blow).</summary>
        public TrackedAgent St;
        public TrackedAgent? Other;
        public ImpactEvent Event;
        public double At;

        /// <summary>The stagger's asked seconds, or the get-up factor of our blow's fall.</summary>
        public double Asked;

        /// <summary>The blocker's bar (the summary's band) and the attacker's band (the riposte's).</summary>
        public double Bar;
        public int AttackerBand;
    }

    /// <summary>
    /// Step 51 - IMPACT I4, BLOCKS AND THE RIPOSTE LIVE (docs/IMPACT.md §3.5's block rows, §3.6, §3.7, §3.9; AI_NOTES "Step 51"). The model's
    /// ONE decision per block (step 48) is APPLIED through the spike's proven seams (step 47), the kicks' pools and watches (step 50):
    ///   - THE STUN BY TIREDNESS: the damage decorator's stun seam (<see cref="IBlockStunDecider.SetBlockStun"/> - WORKER threads, read only):
    ///     the blocker's stun x (1 + <c>BlockStunTiredExtraPercent</c> × T), T from <see cref="TrackedAgent.ImpactTired"/> (written here on
    ///     every tick), the game's own stun held at its own cap first, ours at most <c>ImpactStunCapSeconds</c> - the game's
    ///     <c>StunPeriodMax</c> raised to it while blocks are live and given back when they stop; every stun we do not lengthen stays clamped
    ///     to the game's 0.8 (ImpactHooks); the attacker's recoil is the game's (his longer recoil is the riposte);
    ///   - THE ROLLED STAGGER: <see cref="IImpactBody.ForceStagger"/> from the TICK (never in the callback; never a rider - CanForce first),
    ///     with the engine's priority, then over it; at the speed that gives the asked length (the animation's own length LEARNED from the
    ///     measured ones); WATCHED (WatchKind.Stagger) - its measured end into his state (<see cref="ImpactState.EndReel"/>, the 2 s immunity
    ///     from it); blocks made / hits taken from inside it counted (the proof the window is real);
    ///   - WRONG-SIDE SHIELD DROP: step 50's queued drop from the tick;
    ///   - A BLOCKER KNOCKED DOWN: <see cref="IImpactBody.OwnKnockdownBlow"/> from the tick (lever 6), its Fall watched and slowed by the
    ///     get-up factor (step 50's get-up), the measured end = <see cref="ImpactState.GotUp"/>;
    ///   - THE RIPOSTE: a forced stagger of the ATTACKER from the tick (its length = the decision's recoil seconds), watched the same way.
    /// Each lever (<see cref="ImpactLever.BlockStun"/>, <see cref="ImpactLever.ForcedStagger"/>, <see cref="ImpactLever.OwnBlow"/>) disabled
    /// alone by its error. Master switch first (through <see cref="ImpactRules.Applies"/>): off / shadow / the spike / blocks off = nothing
    /// queued is played, the watches let go, the cap back. Main thread except the seam; fixed pools, no allocation per event or tick.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly ImpactBlockLiveStats _blocks = new ImpactBlockLiveStats();
        private readonly ImpactBlockPending[] _blockPending = new ImpactBlockPending[ImpactBlockMath.MaxPending];
        private int _blockPendingCount;
        private int _guardWatches;
        private int _liveStaggers;
        private bool _blocksApply;
        private bool _riposteApplies;
        private volatile bool _stunLive;
        private volatile bool _liveStunOff;
        private bool _blockCapOwned;
        private float _blockCapAt = float.NaN;
        private StaggerNatural _staggerNatural;
        private string? _youBlowName;

        /// <summary>This mission's live blocks (the smoke reads them).</summary>
        internal ImpactBlockLiveStats ImpactBlockStats => _blocks;

        internal int ImpactBlockPendingCount => _blockPendingCount;

        /// <summary>The forced stagger's own length as learned so far (the smoke).</summary>
        internal double StaggerNaturalSeconds => _staggerNatural.Seconds;

        // ------------------------------------------------------------------ the stun seam (ANY thread)

        /// <summary>
        /// The damage decorator's stun seam while the spike is off (ANY THREAD - reads only, Interlocked counters, no dice, no log line, no
        /// engine call): the blocker's stun x (1 + extra × T) from his snapshot; the attacker's recoil untouched. Gated by the tick's
        /// <see cref="_stunLive"/> (blocks apply, the lever on) AND the master switch / the family read live here.
        /// </summary>
        private void BlockStunLive(Agent defender, TraxSettings s, ref float defenderStun)
        {
            if (!_stunLive || _liveStunOff) return;
            if (!s.ModEnabled || !s.ImpactEnabled || !s.ImpactBlocksEnabled || s.ImpactShadowMode) return; // read live - the tick follows
            _blocks.Stun.Call(Thread.CurrentThread.ManagedThreadId != _mainThreadId);
            var st = GetAnyThread(defender);
            if (st == null)
            {
                _blocks.Stun.NoteUntracked();
                return;
            }
            double factor = ImpactMath.BlockStunFactor(s.BlockStunTiredExtraPercent / 100.0, st.ImpactTired);
            if (!ImpactLiveMath.NeedsSlow(factor))
            {
                _blocks.Stun.NoteUntouched();
                return; // a fresh blocker: the game's stun (clamped to the game's cap by the hook)
            }
            float before = defenderStun;
            float cap = s.ImpactStunCapSeconds;
            float vanillaCap = ImpactHooks.VanillaStunCap;
            defenderStun = ImpactMath.LengthenedStun(before, vanillaCap, factor, ImpactHooks.StunCapRaised ? cap : Math.Min(cap, vanillaCap));
            _blocks.Stun.NoteLengthened(before, defenderStun, factor, (float)(Math.Min(before, vanillaCap) * factor) > cap);
        }

        /// <summary>The record of an agent from ANY thread (the dense map read once into a local - a resize swaps the array whole).</summary>
        private TrackedAgent? GetAnyThread(Agent agent)
        {
            var map = _byIndex;
            int i = agent.Index;
            if ((uint)i >= (uint)map.Length) return null;
            var st = map[i];
            return st != null && !st.Removed && ReferenceEquals(st.Agent, agent) ? st : null;
        }

        // ------------------------------------------------------------------ the tick: the switch, the cap, the snapshot

        /// <summary>From TickImpactLive (its try): blocks / the riposte live? Anything that no longer applies let go at once; the stun seam
        /// gated; the game's cap raised / given back; every man's T written for the seam. Returns whether blocks or the riposte are live.</summary>
        private bool TickBlocksSwitch(double now, in ImpactRules r)
        {
            bool blocks = r.Applies(ImpactFamily.Blocks), riposte = r.Applies(ImpactFamily.Riposte);
            if (blocks != _blocksApply || riposte != _riposteApplies)
            {
                bool wasLive = _blocksApply || _riposteApplies;
                _blocksApply = blocks;
                _riposteApplies = riposte;
                int queued = LetGoBlockItems(blocks, riposte, out int watches);
                if (wasLive && (queued > 0 || watches > 0 || !(blocks || riposte)))
                    TraxLog.Info("impact", (blocks || riposte ? "blocks / the riposte partly live" : "blocks and the riposte no longer live") + " at " + Sec(now) + " s (" + r.ModeText() + "): "
                        + queued + " queued action(s) dropped unplayed, " + watches + " watch(es) let go - a started stagger ends by itself");
            }
            bool stun = blocks && !_liveStunOff && !_live.Disabled(ImpactLever.BlockStun);
            if (stun)
            {
                RaiseBlockCap(now, in r);
                if (!_liveStunOff) SnapshotTired(in r);
            }
            else ReleaseBlockCap(now);
            _stunLive = stun && !_liveStunOff;
            return blocks || riposte;
        }

        /// <summary>The game's <c>StunPeriodMax</c> raised to <c>ImpactStunCapSeconds</c> (read live: a new value re-raises it; at or below the
        /// game's own = the game's left alone). The spike never shares it: while it runs blocks do not apply.</summary>
        private void RaiseBlockCap(double now, in ImpactRules r)
        {
            float want = (float)r.StunCap;
            if (want == _blockCapAt) return;
            try
            {
                float game = ImpactHooks.VanillaStunCap;
                if (want > game)
                {
                    ImpactHooks.RaiseStunCap(want);
                    _blockCapOwned = true;
                    _blocks.CapGame = game;
                    _blocks.CapRaisedTo = want;
                    _blocks.CapRaises++;
                    if (LiveFirst(9))
                        TraxLog.Info("impact", "live: blocks live at " + Sec(now) + " s - the game's stun cap (StunPeriodMax) " + F2(game) + " → " + F2(want)
                            + " s while they are (ImpactStunCapSeconds); a block's stun x (1 + " + F2(r.StunExtra) + " × T) from the blocker's tiredness, every other stun keeps the game's cap");
                }
                else if (_blockCapOwned)
                {
                    if (ImpactHooks.RestoreStunCap()) _blocks.CapRestores++;
                    _blockCapOwned = false;
                }
                _blockCapAt = want;
            }
            catch (Exception e)
            {
                DisableImpactLever(ImpactLever.BlockStun, "impact.live-stun-cap", e);
            }
        }

        private void ReleaseBlockCap(double now)
        {
            _blockCapAt = float.NaN;
            if (!_blockCapOwned) return;
            _blockCapOwned = false;
            if (_spikeOn) return; // the spike raised it itself (it owns it now)
            try
            {
                if (ImpactHooks.RestoreStunCap())
                {
                    _blocks.CapRestores++;
                    if (LiveFirst(10))
                        TraxLog.Info("impact", "live: blocks no longer live at " + Sec(now) + " s - the game's stun cap back to " + F2(ImpactHooks.VanillaStunCap) + " s");
                }
            }
            catch (Exception e)
            {
                Failed("impact.live-stun-restore", e);
            }
        }

        /// <summary>The lever went (its error): the seam stops at once, the cap back.</summary>
        private void BlockStunLeverOff()
        {
            _liveStunOff = true;
            _stunLive = false;
            ReleaseBlockCap(SpikeNow);
        }

        /// <summary>Every tracked man's T for the seam: 1 − f, x <c>ImpactOnPlayerPercent</c> for you. Step 53: a rider's too (IMPACT §3.8 - the
        /// stun multipliers apply on a horse; only a forced action never does) - steps 51-52 wrote 0 on a horse.</summary>
        private void SnapshotTired(in ImpactRules r)
        {
            try
            {
                var ar = Rules;
                double you = Math.Max(0, r.OnPlayer);
                for (int i = 0; i < _count; i++)
                {
                    var st = _dense[i];
                    double t = ImpactMath.Tired(AthleticsMath.PeakShare(in ar, st));
                    if (t > 0 && IsPlayer(st)) t *= you;
                    st.ImpactTired = (float)t;
                }
            }
            catch (Exception e)
            {
                DisableImpactLever(ImpactLever.BlockStun, "impact.live-stun-snapshot", e);
            }
        }

        // ------------------------------------------------------------------ the decision applied (the hit callback)

        /// <summary>
        /// A LIVE block decision (its family and / or the riposte applies; never a mounted blocker - checked by the caller): counted; the
        /// knockdown → our blow queued, else the stagger queued; a wrong-side shield drop queued (step 50's drops); the riposte → the
        /// attacker's stagger queued; a block with no outcome → his guard timed (the stun asked vs measured, a few at a time); the YOU lines.
        /// </summary>
        private void ApplyBlock(TrackedAgent a, TrackedAgent v, in ImpactDecision d, double now, bool blocksApplied, bool riposteApplied, int attackerBand)
        {
            _blocks.NoteDecision(in d, attackerBand, blocksApplied, riposteApplied);
            int shock = 0;
            if (blocksApplied)
            {
                if (d.Knockdown.Happens)
                {
                    shock++;
                    QueueBlock(ImpactBlockAction.OwnBlow, v, a, in d, now, d.GetUpFactor, attackerBand);
                }
                else if (d.Stagger.Happens)
                {
                    shock++;
                    QueueBlock(ImpactBlockAction.Stagger, v, a, in d, now, d.StaggerSeconds, attackerBand);
                }
                if (d.ShieldDrop.Happens)
                {
                    shock++;
                    if (_live.Disabled(ImpactLever.ShieldDrop)) _blocks.NoteNotApplied(ImpactOutcome.ShieldDrop, ImpactNotApplied.LeverOff);
                    else if (_liveDropCount >= _liveDrops.Length)
                    {
                        _blocks.PoolFull++;
                        _blocks.NoteNotApplied(ImpactOutcome.ShieldDrop, ImpactNotApplied.PoolFull);
                    }
                    else _liveDrops[_liveDropCount++] = new ImpactLiveDrop { St = v, By = a, At = now, Bar = d.Bar, Block = true, Event = d.Event };
                }
                if (!d.Knockdown.Happens && !d.Stagger.Happens && !double.IsNaN(d.StunFactor) && _guardWatches < ImpactBlockMath.MaxGuardWatches)
                    AddLiveWatch(v, ImpactWatchPurpose.Guard, WatchKind.Guard, now, 0, d.StunFactor, replace: false, ev: d.Event);
            }
            if (riposteApplied && d.Riposte.Happens)
            {
                shock++;
                QueueBlock(ImpactBlockAction.Riposte, a, v, in d, now, d.RiposteStunSeconds, attackerBand);
            }
            _blocks.ShockGiven += shock; // each = one ImpactState.NoteOutcome in the decision: courage counts it once
            if (shock > 0 && (d.AttackerIsPlayer || d.DefenderIsPlayer)) YouBlockLine(a, v, in d, blocksApplied, riposteApplied);
        }

        private void QueueBlock(ImpactBlockAction action, TrackedAgent st, TrackedAgent other, in ImpactDecision d, double now, double asked, int attackerBand)
        {
            var outcome = OutcomeOf(action);
            var lever = LeverOf(action);
            if (_live.Disabled(lever))
            {
                _blocks.NoteNotApplied(outcome, ImpactNotApplied.LeverOff);
                FixState(action, st, now);
                return;
            }
            if (_blockPendingCount >= _blockPending.Length)
            {
                _blocks.PoolFull++;
                _blocks.NoteNotApplied(outcome, ImpactNotApplied.PoolFull);
                FixState(action, st, now);
                return;
            }
            _blockPending[_blockPendingCount++] = new ImpactBlockPending
            {
                Action = action, St = st, Other = other, Event = d.Event, At = now, Asked = asked, Bar = d.Bar, AttackerBand = attackerBand,
            };
        }

        private static ImpactOutcome OutcomeOf(ImpactBlockAction a) => a switch
        {
            ImpactBlockAction.OwnBlow => ImpactOutcome.Knockdown,
            ImpactBlockAction.Riposte => ImpactOutcome.Riposte,
            _ => ImpactOutcome.Stagger,
        };

        private static ImpactLever LeverOf(ImpactBlockAction a) => a == ImpactBlockAction.OwnBlow ? ImpactLever.OwnBlow : ImpactLever.ForcedStagger;

        /// <summary>An outcome the dice gave that will not be played: his record no longer says he reels / lies on the ground (no immunity
        /// from what did not happen; the halving and the shock stay - the outcome was decided).</summary>
        private static void FixState(ImpactBlockAction action, TrackedAgent st, double now)
        {
            if (action == ImpactBlockAction.OwnBlow) st.Impact.GotUp(now, 0);
            else if (st.Impact.Reeling(now)) st.Impact.EndReel(now, 0);
        }

        /// <summary>The YOU lines (IMPACT §6, the brief's words): what a live block did to you or to the man you fought - with its chance.</summary>
        private void YouBlockLine(TrackedAgent a, TrackedAgent v, in ImpactDecision d, bool blocksApplied, bool riposteApplied)
        {
            var c = System.Globalization.CultureInfo.InvariantCulture;
            string blow = _youBlowName ?? "blow";
            string kind = ImpactMath.EventName(d.Event);
            if (blocksApplied && (d.Knockdown.Happens || d.Stagger.Happens || d.ShieldDrop.Happens))
            {
                string outc = string.Empty;
                AddOutcome(ref outc, d.DefenderIsPlayer, ImpactOutcome.Knockdown, in d.Knockdown);
                AddOutcome(ref outc, d.DefenderIsPlayer, ImpactOutcome.Stagger, in d.Stagger);
                AddOutcome(ref outc, d.DefenderIsPlayer, ImpactOutcome.ShieldDrop, in d.ShieldDrop);
                if (d.Stagger.Happens) outc += " - " + d.StaggerSeconds.ToString("0.00", c) + " s";
                if (d.Knockdown.Happens && !double.IsNaN(d.GetUpFactor)) outc += " - gets up x" + d.GetUpFactor.ToString("0.00", c) + " slower";
                string what = d.DefenderIsPlayer ? "your " + kind + " of " + Name(a) + "'s " + blow : Name(v) + "'s " + kind + " of your " + blow;
                TraxLog.Info("impact", "YOU: " + what + " (" + (d.DefenderIsPlayer ? "your" : "his") + " bar " + (100 * d.Bar).ToString("0", c) + "%, x " + d.X.ToString("0.00", c) + "): " + outc);
            }
            if (riposteApplied && d.Riposte.Happens)
            {
                string s = d.AttackerIsPlayer
                    ? Name(v) + " blocked you (" + kind + "): you reel (riposte, " + Pct0(d.Riposte.Chance) + ")"
                    : "your " + kind + " of " + Name(a) + "'s " + blow + ": he reels (your riposte, " + Pct0(d.Riposte.Chance) + ")";
                TraxLog.Info("impact", "YOU: " + s + " - " + d.RiposteStunSeconds.ToString("0.00", c) + " s (x_r " + d.XRiposte.ToString("0.00", c) + ")");
            }
        }

        private static void AddOutcome(ref string outc, bool you, ImpactOutcome o, in ImpactRoll roll)
        {
            if (!roll.Happens) return;
            outc += (outc.Length > 0 ? ", " : string.Empty) + (you ? ImpactBlockMath.YouOutcome(o) : ImpactBlockMath.HisOutcome(o)) + " (" + Pct0(roll.Chance) + ")";
        }

        // ------------------------------------------------------------------ the tick: the queued staggers, ripostes and our blows

        /// <summary>The queued block actions (≤ the per-tick cap shared with the drops): CanForce first (never a rider - no engine call), the
        /// stagger with the engine's priority then over it, our blow; each watched. A lever's error disables it alone (after the loop).</summary>
        private void RunBlockPending(double now, ref int calls)
        {
            int keep = 0;
            Exception? staggerError = null, blowError = null;
            for (int i = 0; i < _blockPendingCount; i++)
            {
                var p = _blockPending[i];
                var outcome = OutcomeOf(p.Action);
                var lever = LeverOf(p.Action);
                bool failed = lever == ImpactLever.OwnBlow ? blowError != null : staggerError != null;
                if (_live.Disabled(lever) || failed)
                {
                    _blocks.NoteNotApplied(outcome, ImpactNotApplied.LeverOff);
                    if (p.St != null) FixState(p.Action, p.St, now);
                    continue;
                }
                if (calls >= ImpactLiveMath.MaxActionsPerTick)
                {
                    _blocks.Deferred++;
                    _blockPending[keep++] = p;
                    continue;
                }
                if (p.St == null || p.St.Removed || !ImpactBody.IsActive(p.St))
                {
                    _blocks.LeftField++;
                    _blocks.NoteNotApplied(outcome, ImpactNotApplied.LeftField);
                    continue;
                }
                try
                {
                    if (ImpactBody.CanForce(p.St) != ForceRefusal.None)
                    {
                        _blocks.NoteNotApplied(outcome, ImpactNotApplied.CannotForce); // never a rider / horse - no engine call
                        FixState(p.Action, p.St, now);
                        continue;
                    }
                    if (p.Action == ImpactBlockAction.OwnBlow) RunOwnBlow(in p, now, ref calls);
                    else RunStagger(in p, now, ref calls);
                }
                catch (Exception e)
                {
                    if (lever == ImpactLever.OwnBlow) blowError = e;
                    else staggerError = e;
                    FixState(p.Action, p.St, now);
                }
            }
            for (int i = keep; i < _blockPendingCount; i++) _blockPending[i] = default;
            _blockPendingCount = keep;
            if (staggerError != null) DisableImpactLever(ImpactLever.ForcedStagger, "impact.live-stagger", staggerError);
            if (blowError != null) DisableImpactLever(ImpactLever.OwnBlow, "impact.live-own-blow", blowError);
        }

        private void RunStagger(in ImpactBlockPending p, double now, ref int calls)
        {
            bool riposte = p.Action == ImpactBlockAction.Riposte;
            var outcome = riposte ? ImpactOutcome.Riposte : ImpactOutcome.Stagger;
            float speed = ImpactBlockMath.StaggerSpeed(p.Asked, _staggerNatural.Seconds);
            calls++;
            bool over = false;
            bool ok = ImpactBody.ForceStagger(p.St, StaggerKind.Backward, speed, false, out int index);
            if (!ok && calls < ImpactLiveMath.MaxActionsPerTick)
            {
                calls++;
                ok = ImpactBody.ForceStagger(p.St, StaggerKind.Backward, speed, true, out index);
                over = ok;
            }
            if (!ok)
            {
                _blocks.NoteNotApplied(outcome, ImpactNotApplied.EngineRefused);
                FixState(p.Action, p.St, now);
                return;
            }
            if (over) _blocks.StaggerAcceptedOverPriority++;
            else _blocks.StaggerAcceptedWithPriority++;
            if (riposte) _blocks.NoteRiposteApplied(p.AttackerBand);
            else _blocks.NoteApplied(p.Event, ImpactOutcome.Stagger, p.Bar);
            ImpactMessage(riposte ? ImpactMessageKind.Riposte : ImpactMessageKind.GuardBroken, p.Other != null && !p.Other.Removed ? p.Other : null, p.St); // step 53
            AddLiveWatch(p.St, riposte ? ImpactWatchPurpose.Riposte : ImpactWatchPurpose.Stagger, WatchKind.Stagger, now, ImpactBlockMath.StaggerAppearSeconds, 1,
                expectIndex: index, ev: p.Event, asked: p.Asked, speed: speed);
            if (LiveFirst(riposte ? 11 : 12))
                TraxLog.Info("impact", "live: first " + (riposte ? "RIPOSTE - the attacker " + Name(p.St) + " staggered after " + (p.Other != null ? Name(p.Other) : "(someone)") + "'s " + ImpactMath.EventName(p.Event)
                        : "block stagger - " + Name(p.St) + " after his " + ImpactMath.EventName(p.Event)) + " - act_stagger_backward from the tick at " + Sec(now) + " s ("
                    + F2(now - p.At) + " s after the block), " + (over ? "OVER the engine's priority" : "with the engine's priority") + ", speed " + F2(speed) + " for " + F2(p.Asked)
                    + " s asked (the animation's own length " + F2(_staggerNatural.Seconds) + " s - " + (_staggerNatural.Count > 0 ? "measured" : "a guess until one is measured") + ")");
        }

        private void RunOwnBlow(in ImpactBlockPending p, double now, ref int calls)
        {
            calls++;
            var by = p.Other != null && !p.Other.Removed ? p.Other : null;
            if (!ImpactBody.OwnKnockdownBlow(p.St, by, SpikeMath.OwnBlowDamage))
            {
                _blocks.NoteNotApplied(ImpactOutcome.Knockdown, ImpactNotApplied.EngineRefused);
                FixState(p.Action, p.St, now);
                return;
            }
            _blocks.OwnBlows++;
            _blocks.NoteApplied(p.Event, ImpactOutcome.Knockdown, p.Bar);
            ImpactMessage(ImpactMessageKind.KnockedDown, by, p.St); // step 53
            AddLiveWatch(p.St, ImpactWatchPurpose.BlockGetUp, WatchKind.Fall, now, ImpactLiveMath.FallAppearSeconds, p.Asked, ev: p.Event);
            if (LiveFirst(13))
                TraxLog.Info("impact", "live: first BLOCKER knocked down - our own blow (RegisterBlow, KnockDown | KnockBack, " + SpikeMath.OwnBlowDamage + " damage) on " + Name(p.St)
                    + " after his " + ImpactMath.EventName(p.Event) + " of " + (by != null ? Name(by) : "(someone)") + "'s blow, from the tick at " + Sec(now) + " s; his fall-and-rise to be slowed x"
                    + F2(double.IsNaN(p.Asked) ? 1 : p.Asked));
        }

        // ------------------------------------------------------------------ the watches (from TickLiveWatches)

        private void BlockWatchAppeared(ref ImpactLiveWatch w, double now)
        {
            switch (w.Purpose)
            {
                case ImpactWatchPurpose.Stagger:
                    _blocks.NoteSeen(w.Event, ImpactOutcome.Stagger);
                    if (LiveFirst(14))
                        TraxLog.Info("impact", "live: first block stagger seen at " + Sec(now) + " s - " + Name(w.St) + "'s act_stagger on channel " + w.W.Channel + " ("
                            + ImpactActions.Name(w.W.FirstType) + "), " + F2(now - w.W.Since) + " s after the call");
                    break;
                case ImpactWatchPurpose.Riposte:
                    _blocks.RiposteSeen++;
                    break;
                case ImpactWatchPurpose.BlockGetUp:
                    _blocks.NoteSeen(w.Event, ImpactOutcome.Knockdown);
                    if (LiveFirst(15))
                        TraxLog.Info("impact", "live: first blocker's Fall seen at " + Sec(now) + " s - " + Name(w.St) + ", " + F2(now - w.W.Since) + " s after our blow"
                            + (w.Slow ? " - his fall-and-rise slowed x" + F2(w.Factor) : " - untouched (x1)"));
                    break;
            }
        }

        private void BlockWatchMeasured(ref ImpactLiveWatch w, double length, double now, in ImpactRules r, bool slowed)
        {
            switch (w.Purpose)
            {
                case ImpactWatchPurpose.Stagger:
                case ImpactWatchPurpose.Riposte:
                    if (w.Purpose == ImpactWatchPurpose.Stagger) _blocks.NoteStagger(w.Asked, length, w.Speed);
                    else _blocks.NoteRiposteLength(w.Asked, length);
                    _staggerNatural.Add(length, w.Speed);
                    w.St.Impact.EndReel(now, r.StaggerImmunity); // the 2 s immunity from what the engine really played
                    if (LiveFirst(w.Purpose == ImpactWatchPurpose.Stagger ? 16 : 17))
                        TraxLog.Info("impact", "live: first " + (w.Purpose == ImpactWatchPurpose.Stagger ? "block stagger" : "riposte stagger") + " measured at " + Sec(now) + " s - " + Name(w.St)
                            + "'s lasted " + F2(length) + " s (asked " + F2(w.Asked) + " s at speed " + F2(w.Speed) + "); no new stagger on him for " + F1(r.StaggerImmunity) + " s");
                    break;
                case ImpactWatchPurpose.BlockGetUp:
                    _blocks.BlockGetUpAsked.Add(w.Factor);
                    _blocks.BlockGetUpMeasured.Add(length);
                    w.St.Impact.GotUp(now, r.KnockdownImmunity);
                    break;
                case ImpactWatchPurpose.Guard:
                    _blocks.NoteGuard(w.Factor, length);
                    break;
            }
        }

        private void BlockWatchNeverAppeared(ref ImpactLiveWatch w, double now)
        {
            switch (w.Purpose)
            {
                case ImpactWatchPurpose.Stagger:
                    _blocks.StaggerNotSeen++;
                    w.St.Impact.EndReel(now, 0);
                    if (LiveFirst(18))
                        TraxLog.Info("impact", "live: a block stagger the engine took at " + Sec(w.W.Since) + " s never showed on " + Name(w.St) + " (channel 0 " + ImpactActions.Name(_liveRead.Type0)
                            + ", channel 1 " + ImpactActions.Name(_liveRead.Type1) + ") - tell Claude if this repeats");
                    break;
                case ImpactWatchPurpose.Riposte:
                    _blocks.RiposteNotSeen++;
                    w.St.Impact.EndReel(now, 0);
                    break;
                case ImpactWatchPurpose.BlockGetUp:
                    _blocks.BlockerFallNotSeen++;
                    w.St.Impact.GotUp(now, 0);
                    break;
                case ImpactWatchPurpose.Guard:
                    _blocks.GuardNotSeen++;
                    break;
            }
        }

        /// <summary>OnMeleeHit (any event on him): a block made / a hit taken while a stagger of ours shows on him - the window's proof.</summary>
        private void NoteInsideLiveStagger(TrackedAgent man, bool blocked)
        {
            for (int i = 0; i < _liveWatchCount; i++)
            {
                ref var w = ref _liveWatches[i];
                if ((w.Purpose != ImpactWatchPurpose.Stagger && w.Purpose != ImpactWatchPurpose.Riposte) || !ReferenceEquals(w.St, man) || !w.W.Appeared) continue;
                if (blocked) _blocks.BlocksFromInside++;
                else _blocks.HitsFromInside++;
                return;
            }
        }

        // ------------------------------------------------------------------ letting go

        /// <summary>What no longer applies let go (no engine call): the queued staggers / our blows / wrong-side drops when blocks stop, the
        /// queued ripostes when the riposte stops, and their watches. Returns the queued ones; <paramref name="watches"/> the watches.</summary>
        private int LetGoBlockItems(bool blocks, bool riposte, out int watches)
        {
            int n = 0;
            int keep = 0;
            for (int i = 0; i < _blockPendingCount; i++)
            {
                var p = _blockPending[i];
                bool live = p.Action == ImpactBlockAction.Riposte ? riposte : blocks;
                if (live)
                {
                    _blockPending[keep++] = p;
                    continue;
                }
                _blocks.NoteNotApplied(OutcomeOf(p.Action), ImpactNotApplied.LeverOff);
                if (p.St != null) FixState(p.Action, p.St, SpikeNow);
                n++;
            }
            for (int i = keep; i < _blockPendingCount; i++) _blockPending[i] = default;
            _blockPendingCount = keep;
            if (!blocks) n += LetGoLiveDrops(blocks: true);
            watches = 0;
            for (int i = 0; i < _liveWatchCount; i++)
            {
                var pu = _liveWatches[i].Purpose;
                bool block = pu == ImpactWatchPurpose.Stagger || pu == ImpactWatchPurpose.BlockGetUp || pu == ImpactWatchPurpose.Guard;
                bool rip = pu == ImpactWatchPurpose.Riposte;
                if ((block && !blocks) || (rip && !riposte))
                {
                    RemoveLiveWatch(i--);
                    watches++;
                }
            }
            return n;
        }

        /// <summary>A lever's error (DisableImpactLever): its queued actions dropped unplayed.</summary>
        private void LetGoBlockPending(ImpactLever lever)
        {
            int keep = 0;
            for (int i = 0; i < _blockPendingCount; i++)
            {
                var p = _blockPending[i];
                if (LeverOf(p.Action) != lever)
                {
                    _blockPending[keep++] = p;
                    continue;
                }
                _blocks.NoteNotApplied(OutcomeOf(p.Action), ImpactNotApplied.LeverOff);
                if (p.St != null) FixState(p.Action, p.St, SpikeNow);
            }
            for (int i = keep; i < _blockPendingCount; i++) _blockPending[i] = default;
            _blockPendingCount = keep;
        }

        /// <summary>He left the field: his queued actions dropped (no engine call); an attacker's queued riposte on HIM too - and the other
        /// man of a queued action that stays forgotten (our blow then has no owner).</summary>
        private void BlockPendingLeftField(TrackedAgent st)
        {
            int keep = 0;
            for (int i = 0; i < _blockPendingCount; i++)
            {
                var p = _blockPending[i];
                if (ReferenceEquals(p.St, st))
                {
                    _blocks.LeftField++;
                    _blocks.NoteNotApplied(OutcomeOf(p.Action), ImpactNotApplied.LeftField);
                    continue;
                }
                if (ReferenceEquals(p.Other, st)) p.Other = null;
                _blockPending[keep++] = p;
            }
            for (int i = keep; i < _blockPendingCount; i++) _blockPending[i] = default;
            _blockPendingCount = keep;
        }

        /// <summary>Mission end: the queue dropped (counted as left the field), the seam off.</summary>
        private void CloseBlockPending()
        {
            for (int i = 0; i < _blockPendingCount; i++)
            {
                _blocks.NoteNotApplied(OutcomeOf(_blockPending[i].Action), ImpactNotApplied.LeftField);
                _blockPending[i] = default;
            }
            _blockPendingCount = 0;
            _stunLive = false;
        }
    }
}

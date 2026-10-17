using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Models;

namespace TraxCombat.Missions
{
    /// <summary>Step 53: a rider knocked off by a BLOCK - our own dismount blow, made from the next tick (a struct in a fixed pool).</summary>
    internal struct ImpactRiderPending
    {
        public TrackedAgent St;
        public TrackedAgent? Other;
        public ImpactEvent Event;
        public double At;
    }

    /// <summary>Step 53: a dismount answered or a dismount blow registered - followed until he is off his horse (seen) or 2 s pass.</summary>
    internal struct ImpactRiderCheck
    {
        public TrackedAgent St;
        public ImpactDismountWay Way;
        public double At;
    }

    /// <summary>Step 53: where the on-screen impact lines go - the game's message feed; the offline smoke collects them.</summary>
    internal interface IImpactMessageSink
    {
        void Show(string text, string hex);
    }

    /// <summary>The game's message feed (InformationManager - the line the game shows for a couched lance's hit, in its colours).</summary>
    internal sealed class GameImpactMessageSink : IImpactMessageSink
    {
        public void Show(string text, string hex) => InformationManager.DisplayMessage(new InformationMessage(text, Color.ConvertStringToColor(hex)));
    }

    /// <summary>The sink every new logic starts with (the smoke swaps in its own once, before any logic is built).</summary>
    internal static class ImpactMessageSinkDefault
    {
        internal static IImpactMessageSink Current = new GameImpactMessageSink();
    }

    /// <summary>
    /// Step 53 - IMPACT I6, RIDERS + THE ON-SCREEN LINES (docs/IMPACT.md §3.8, §6, §8 q4 / q12; AI_NOTES "Step 53"). The model's ONE decision
    /// on a mounted defender (step 48) is APPLIED - Anton's answer to q4:
    ///   - KNOCKED OFF HIS HORSE: the model's knockdown on a rider (× <c>ImpactRiderDismountPercent</c> - <see cref="ImpactDecision.Dismount"/>);
    ///     a LANDED blow is answered in the damage decorator's dismount seam (<see cref="IDismountDecider"/>, MAIN thread - Mission.CreateMeleeBlow
    ///     asks <c>DecideAgentDismountedByBlow</c> INSTEAD of the KnockDown question for a man on a horse; it IS the seam that decides it, the
    ///     KnockDown one never sees a rider); a BLOCK (never registered by the engine) = a blow of our own flagged <c>CanDismount</c> from the
    ///     tick (<see cref="IImpactBody.OwnDismountBlow"/>); each followed until he is off the horse (SEEN) or 2 s pass;
    ///   - his SHIELD can drop (kicks / bashes, wrong-side blocks - the queued drop of step 50; a horse does not hold a shield for him);
    ///   - NEVER a forced stagger or a slowed reaction on a rider (the model refuses his stagger; <see cref="IImpactBody.CanForce"/> refuses
    ///     any forced action on a mounted man) - horses untouched; his block stun x (1 + T) like anyone's (IMPACT §3.8 "stun multipliers apply").
    /// THE ON-SCREEN LINES (q12): what was APPLIED to you / by you (and, behind <c>ImpactMessagesIncludeYourMen</c>, your side's notable moments
    /// with heroes) → Core <see cref="ImpactMessageQueue"/> (rate limit + merge) → the game's message feed from the tick, in the game's colours.
    /// Never in shadow mode (nothing is applied there). Master switch first (through <see cref="ImpactRules.Applies"/> and read live here).
    /// The lever <see cref="ImpactLever.Dismount"/> disabled alone by its error. Main thread; fixed pools, no allocation per event or tick (a line's
    /// strings are built only for the player's events).
    /// </summary>
    public sealed partial class AthleticsLogic : IDismountDecider
    {
        private readonly ImpactRiderLiveStats _riders = new ImpactRiderLiveStats();
        private readonly ImpactRiderPending[] _riderPending = new ImpactRiderPending[ImpactRiderMath.MaxPending];
        private readonly ImpactRiderCheck[] _riderChecks = new ImpactRiderCheck[ImpactRiderMath.MaxChecks];
        private readonly bool[] _ridersFirst = new bool[8];
        private readonly ImpactMessageQueue _messages = new ImpactMessageQueue();
        private int _riderPendingCount;
        private int _riderCheckCount;
        private bool _inDismountSeam;

        /// <summary>This mission's live riders / on-screen lines (the smoke reads them).</summary>
        internal ImpactRiderLiveStats ImpactRiderStats => _riders;

        internal ImpactMessageQueue ImpactMessages => _messages;

        internal int ImpactRiderPendingCount => _riderPendingCount;

        internal int ImpactRiderChecks => _riderCheckCount;

        /// <summary>Where the on-screen lines go (the smoke swaps in its own).</summary>
        internal IImpactMessageSink MessageSink { get; set; } = ImpactMessageSinkDefault.Current;

        // ------------------------------------------------------------------ the dismount seam (MAIN thread)

        bool IDismountDecider.DismountOnBlow(Agent attacker, Agent victim, in AttackCollisionData collision, in Blow blow, bool gameSays)
        {
            _inDismountSeam = true;
            try
            {
                return ImpactModelOnLandedBlow(attacker, victim, in collision); // decided once here; OnMeleeHit takes it
            }
            finally
            {
                _inDismountSeam = false;
            }
        }

        // ------------------------------------------------------------------ the decision applied

        /// <summary>
        /// A LIVE decision on a mounted defender (his family applies): counted; the knockdown = KNOCKED OFF - answered here when the dismount
        /// seam asks (a landed blow), queued as our own dismount blow (a block); the shield drop queued; the YOU line; the on-screen line. Never
        /// a stagger (the model refused it). Returns the dismount answer for the seam.
        /// </summary>
        private bool ApplyRider(TrackedAgent a, TrackedAgent v, in ImpactDecision d, double now)
        {
            _riders.NoteDecision(in d);
            bool answer = false;
            int shock = 0;
            if (d.Knockdown.Happens)
            {
                shock++;
                if (ImpactMath.IsBlock(d.Event)) QueueRiderBlow(v, a, in d, now);
                else if (!_inDismountSeam)
                {
                    _riders.NoteNotApplied(ImpactNotApplied.OutsideSeam);
                    v.Impact.GotUp(now, 0); // never thrown: no ground time, no immunity from it
                }
                else if (_live.Disabled(ImpactLever.Dismount))
                {
                    _riders.NoteNotApplied(ImpactNotApplied.LeverOff);
                    v.Impact.GotUp(now, 0);
                }
                else
                {
                    answer = true;
                    _riders.NoteApplied(ImpactDismountWay.Seam);
                    AddRiderCheck(v, ImpactDismountWay.Seam, now);
                    ImpactMessage(ImpactMessageKind.KnockedOff, a, v);
                    if (RidersFirst(0))
                        TraxLog.Info("impact", "live: first rider KNOCKED OFF his horse at " + Sec(now) + " s - " + Name(a) + "'s " + ImpactMath.EventName(d.Event) + " on " + Name(v)
                            + " (his bar " + Pct0(d.Bar) + ", x " + F2(d.X) + ", the knockdown's chance x " + TraxSettings.Shared.ImpactRiderDismountPercent + "% = " + Pct0(d.Knockdown.Chance)
                            + (d.Momentum > 1.0001 ? ", momentum x" + F2(d.Momentum) : string.Empty) + ") - answered in the damage decorator's dismount seam (DecideAgentDismountedByBlow)");
                }
            }
            if (d.ShieldDrop.Happens)
            {
                shock++;
                if (_live.Disabled(ImpactLever.ShieldDrop)) _riders.NoteShieldNotApplied(ImpactNotApplied.LeverOff);
                else if (_liveDropCount >= _liveDrops.Length)
                {
                    _riders.PoolFull++;
                    _riders.NoteShieldNotApplied(ImpactNotApplied.PoolFull);
                }
                else
                {
                    _riders.ShieldQueued++;
                    _liveDrops[_liveDropCount++] = new ImpactLiveDrop { St = v, By = a, At = now, Bar = d.Bar, Block = ImpactMath.IsBlock(d.Event), Rider = true, Event = d.Event };
                }
            }
            _riders.ShockGiven += shock;
            if (shock > 0 && (d.AttackerIsPlayer || d.DefenderIsPlayer))
            {
                string what = d.DefenderIsPlayer ? Name(a) + "'s " + ImpactMath.EventName(d.Event) + " on you (riding)" : "your " + ImpactMath.EventName(d.Event) + " on " + Name(v) + " (riding)";
                string outc = d.Knockdown.Happens ? (d.DefenderIsPlayer ? "you are knocked off your horse" : "he is knocked off his horse") + " (" + Pct0(d.Knockdown.Chance) + ")"
                                                    + (answer || ImpactMath.IsBlock(d.Event) ? string.Empty : " - NOT applied") : string.Empty;
                if (d.ShieldDrop.Happens) outc += (outc.Length > 0 ? ", " : string.Empty) + (d.DefenderIsPlayer ? "your" : "his") + " shield knocked away (" + Pct0(d.ShieldDrop.Chance) + ")";
                TraxLog.Info("impact", "YOU: " + what + " (" + (d.DefenderIsPlayer ? "your" : "his") + " bar " + Pct0(d.Bar) + ", x " + F2(d.X) + "): " + outc);
            }
            return answer;
        }

        private void QueueRiderBlow(TrackedAgent v, TrackedAgent a, in ImpactDecision d, double now)
        {
            if (_live.Disabled(ImpactLever.Dismount) || _live.Disabled(ImpactLever.OwnBlow))
            {
                _riders.NoteNotApplied(ImpactNotApplied.LeverOff);
                v.Impact.GotUp(now, 0);
                return;
            }
            if (_riderPendingCount >= _riderPending.Length)
            {
                _riders.PoolFull++;
                _riders.NoteNotApplied(ImpactNotApplied.PoolFull);
                v.Impact.GotUp(now, 0);
                return;
            }
            _riderPending[_riderPendingCount++] = new ImpactRiderPending { St = v, Other = a, Event = d.Event, At = now };
        }

        private void AddRiderCheck(TrackedAgent st, ImpactDismountWay way, double now)
        {
            if (_riderCheckCount >= _riderChecks.Length)
            {
                _riders.PoolFull++;
                return;
            }
            _riderChecks[_riderCheckCount++] = new ImpactRiderCheck { St = st, Way = way, At = now };
        }

        // ------------------------------------------------------------------ the tick

        /// <summary>From TickImpactLive (its try): the blocked riders' dismount blows (blocks live, the levers on, ≤ the per-tick cap shared with
        /// the drops and the block actions; a horse still under him), then the followed dismounts (off the horse = seen; 2 s = not seen, his
        /// record's ground time taken back).</summary>
        private void TickImpactRiders(double now, in ImpactRules r, ref int calls)
        {
            if (_riderPendingCount > 0) RunRiderPending(now, r.Applies(ImpactFamily.Blocks), ref calls);
            if (_riderCheckCount > 0) TickRiderChecks(now);
        }

        private void RunRiderPending(double now, bool blocksLive, ref int calls)
        {
            int keep = 0;
            Exception? error = null;
            for (int i = 0; i < _riderPendingCount; i++)
            {
                var p = _riderPending[i];
                if (!blocksLive || error != null || _live.Disabled(ImpactLever.Dismount) || _live.Disabled(ImpactLever.OwnBlow))
                {
                    _riders.NoteNotApplied(ImpactNotApplied.LeverOff);
                    p.St?.Impact.GotUp(now, 0);
                    continue;
                }
                if (calls >= ImpactLiveMath.MaxActionsPerTick)
                {
                    _riderPending[keep++] = p;
                    continue;
                }
                if (p.St == null || p.St.Removed || !ImpactBody.IsActive(p.St))
                {
                    _riders.LeftField++;
                    _riders.NoteNotApplied(ImpactNotApplied.LeftField);
                    continue;
                }
                try
                {
                    if (!ImpactBody.IsMounted(p.St))
                    {
                        _riders.NoteNotApplied(ImpactNotApplied.NoHorse);
                        p.St.Impact.GotUp(now, 0);
                        continue;
                    }
                    calls++;
                    var by = p.Other != null && !p.Other.Removed ? p.Other : null;
                    if (!ImpactBody.OwnDismountBlow(p.St, by, SpikeMath.OwnBlowDamage))
                    {
                        _riders.NoteNotApplied(ImpactNotApplied.EngineRefused);
                        p.St.Impact.GotUp(now, 0);
                        continue;
                    }
                    _riders.NoteApplied(ImpactDismountWay.OwnBlow);
                    AddRiderCheck(p.St, ImpactDismountWay.OwnBlow, now);
                    ImpactMessage(ImpactMessageKind.KnockedOff, by, p.St);
                    if (RidersFirst(1))
                        TraxLog.Info("impact", "live: first BLOCKING rider knocked off his horse - our own blow (RegisterBlow, CanDismount, " + SpikeMath.OwnBlowDamage + " damage) on " + Name(p.St)
                            + " after his " + ImpactMath.EventName(p.Event) + " of " + (by != null ? Name(by) : "(someone)") + "'s blow, from the tick at " + Sec(now) + " s (" + F2(now - p.At) + " s after the block)");
                }
                catch (Exception e)
                {
                    error = e;
                    p.St.Impact.GotUp(now, 0);
                }
            }
            for (int i = keep; i < _riderPendingCount; i++) _riderPending[i] = default;
            _riderPendingCount = keep;
            if (error != null) DisableImpactLever(ImpactLever.Dismount, "impact.live-dismount-blow", error);
        }

        private void TickRiderChecks(double now)
        {
            for (int i = 0; i < _riderCheckCount; i++)
            {
                ref var c = ref _riderChecks[i];
                if (c.St == null || c.St.Removed || !ImpactBody.IsActive(c.St))
                {
                    _riders.LeftField++;
                    RemoveRiderCheck(i--);
                    continue;
                }
                if (!ImpactBody.IsMounted(c.St))
                {
                    _riders.NoteSeen(c.Way, now - c.At);
                    if (RidersFirst(2))
                        TraxLog.Info("impact", "live: first rider SEEN off his horse at " + Sec(now) + " s - " + Name(c.St) + ", " + F2(now - c.At) + " s after "
                            + ImpactRiderMath.WayName(c.Way));
                    RemoveRiderCheck(i--);
                    continue;
                }
                if (now - c.At <= ImpactRiderMath.SeenWithinSeconds) continue;
                _riders.NoteNotSeen(c.Way);
                c.St.Impact.GotUp(now, 0); // he stayed on: no ground time, no immunity from a fall that never came
                if (RidersFirst(3))
                    TraxLog.Info("impact", "live: a rider still on his horse " + F1(ImpactRiderMath.SeenWithinSeconds) + " s after " + ImpactRiderMath.WayName(c.Way) + " (" + Name(c.St)
                        + ", at " + Sec(now) + " s) - the engine did not play the dismount; tell Claude if the summary shows many (PLAYTEST D22)");
                RemoveRiderCheck(i--);
            }
        }

        private void RemoveRiderCheck(int i)
        {
            _riderChecks[i] = _riderChecks[--_riderCheckCount];
            _riderChecks[_riderCheckCount] = default;
        }

        /// <summary>He left the field (Forget): out of the riders' pools - no engine call; a queued blow whose owner left keeps going unowned.</summary>
        private void RidersLeftField(TrackedAgent st)
        {
            int keep = 0;
            for (int i = 0; i < _riderPendingCount; i++)
            {
                var p = _riderPending[i];
                if (ReferenceEquals(p.St, st))
                {
                    _riders.LeftField++;
                    _riders.NoteNotApplied(ImpactNotApplied.LeftField);
                    continue;
                }
                if (ReferenceEquals(p.Other, st)) p.Other = null;
                _riderPending[keep++] = p;
            }
            for (int i = keep; i < _riderPendingCount; i++) _riderPending[i] = default;
            _riderPendingCount = keep;
            for (int i = 0; i < _riderCheckCount; i++)
            {
                if (!ReferenceEquals(_riderChecks[i].St, st)) continue;
                _riders.LeftField++;
                RemoveRiderCheck(i--);
            }
        }

        /// <summary>Mission end: the pools let go (a queued blow counted as left the field), the waiting lines thrown away (counted).</summary>
        private void CloseRiders()
        {
            for (int i = 0; i < _riderPendingCount; i++)
            {
                _riders.NoteNotApplied(ImpactNotApplied.LeftField);
                _riderPending[i] = default;
            }
            _riderPendingCount = 0;
            for (int i = 0; i < _riderCheckCount; i++) _riderChecks[i] = default;
            _riderCheckCount = 0;
            _messages.Clear();
        }

        private void WriteRidersSummary(in ImpactRules r)
        {
            var s = TraxSettings.Shared;
            foreach (var line in _riders.SummaryLines(r.Applies(ImpactFamily.Kicks) || r.Applies(ImpactFamily.Blocks) || r.Applies(ImpactFamily.Hits), r.ModeText(), _live.LeversText(), s.ImpactRiderDismountPercent))
                TraxLog.Info("summary", line);
            TraxLog.Info("summary", _messages.SummaryLine(s.ImpactShowMessages, s.ImpactMessagesIncludeYourMen, s.ImpactMessageMinSeconds));
        }

        private bool RidersFirst(int i)
        {
            if (_ridersFirst[i]) return false;
            _ridersFirst[i] = true;
            return true;
        }

        // ------------------------------------------------------------------ the on-screen lines

        /// <summary>
        /// One APPLIED moment offered to the on-screen lines (called only where an outcome was really carried out - never in shadow mode): to
        /// you (a guard broken on YOUR block is left out - a tired player's every block would speak), by you, or - behind
        /// <c>ImpactMessagesIncludeYourMen</c> - your side's notable moments with a hero (<see cref="ImpactMessageMath.NotableForYourMen"/>).
        /// The line's strings are built only then. Own try: an error = one [error], no line.
        /// </summary>
        private void ImpactMessage(ImpactMessageKind kind, TrackedAgent? by, TrackedAgent to)
        {
            try
            {
                var s = TraxSettings.Shared;
                if (!s.ModEnabled || !s.ImpactShowMessages || to == null) return;
                ImpactMessageRole role;
                if (IsPlayer(to))
                {
                    if (kind == ImpactMessageKind.GuardBroken) return;
                    role = ImpactMessageRole.ToYou;
                }
                else if (by != null && IsPlayer(by)) role = ImpactMessageRole.ByYou;
                else
                {
                    if (!s.ImpactMessagesIncludeYourMen || by == null || to.HeroName == null) return; // notable = a hero on the receiving end
                    if (kind != ImpactMessageKind.KnockedDown && kind != ImpactMessageKind.KnockedOff) return;
                    if (!ImpactMessageMath.NotableForYourMen(kind, ImpactBody.OnPlayerSide(by), ImpactBody.OnPlayerSide(to), true)) return;
                    role = ImpactMessageRole.YourMen;
                }
                _messages.Add(kind, role, ImpactMessageMath.Text(kind, role, by != null ? MessageName(by) : null, MessageName(to)));
            }
            catch (Exception e)
            {
                Failed("impact.message", e);
            }
        }

        /// <summary>From TickImpact (own try): the next merged line when the last is old enough (<c>ImpactMessageMinSeconds</c>, live); the
        /// switch or the master switch off = what waits thrown away.</summary>
        internal void TickImpactMessages(double now)
        {
            if (_messages.Waiting == 0) return;
            var s = TraxSettings.Shared;
            if (!s.ModEnabled || !s.ImpactShowMessages)
            {
                _messages.Clear();
                return;
            }
            if (!_messages.TryTake(now, s.ImpactMessageMinSeconds, out string line, out var role)) return;
            MessageSink.Show(line, ImpactMessageMath.Hex(role));
            TraxLog.Info("impact", "on screen at " + Sec(now) + " s (" + ImpactMessageMath.Hex(role) + "): " + line);
        }

        private static string MessageName(TrackedAgent st) => st.HeroName ?? SafeName(st.Agent);
    }
}

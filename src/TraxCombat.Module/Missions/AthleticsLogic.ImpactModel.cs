using System;
using System.Diagnostics;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 48 - IMPACT I2, THE MODEL in the mission (docs/IMPACT.md §3, §6; AI_NOTES "Step 48"; step 51's blocks: AthleticsLogic.ImpactBlocks.cs): every kick / bash that lands, every block
    /// (right side, parry, wrong side) and every clean weapon hit on a man we track is DECIDED once by Core <see cref="ImpactMath.Decide"/> -
    /// both men's Athletics from our records, the weapon / guard / skills / mounts / health from <see cref="IImpactInputs"/> - and LOGGED
    /// (the first of each kind and of each outcome in full, the rest verbose; the [summary] "impact" lines by event and bar band). A family's
    /// decision is APPLIED once <see cref="ImpactRules.Applies"/> says so (its effect built - <see cref="ImpactMath.EffectsBuilt"/>: step 50
    /// kicks / bashes, AthleticsLogic.ImpactLive.cs; 51 blocks + the riposte; 52 clean hits), through the spike's seams with THIS decision:
    ///   - a LANDED blow (kick, bash, clean hit) on a man on foot is decided in the damage decorator's KnockDown seam
    ///     (<see cref="ImpactModelOnLandedBlow"/> - before the engine registers the blow, so step 50 / 52 answer KnockDown there) and
    ///     remembered for the same callback's OnMeleeHit, which takes it instead of deciding again (one decision per event);
    ///   - step 53: a landed blow on a RIDER is decided the same way in the decorator's DISMOUNT seam (the engine asks it instead of the
    ///     KnockDown question for a man on a horse - AthleticsLogic.ImpactRiders.cs);
    ///   - a BLOCK and anything the decorator did not see is decided in OnMeleeHit (<see cref="ImpactModelHitTaken"/>); step 51 makes the stagger / the drop / our blow from the tick, and the
    ///     stun by tiredness in the stun seam (any thread - from T alone, no dice state).
    /// Step 50: a family decided LIVE whose effect is not built yet decides on COPIES of the men's states (<see cref="ImpactRules.KeepsState"/>)
    /// - a stagger that never happened gives no reeling bonus, immunity, halving or courage shock.
    /// Master switch FIRST (then AthleticsEnabled, ImpactEnabled, the family), read live at every event. Main thread; no allocation per event.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly ImpactStats _impact = new ImpactStats();
        private readonly bool[] _impactFirst = new bool[ImpactMath.EventCount + ImpactMath.OutcomeCount + 2];
        private bool _impactOn;
        private bool _impactShadowSeen;
        private double _impactLastTick = double.NaN;

        // the decision the decorator made for the blow now inside the engine's MeleeHitCallback (its OnMeleeHit takes it)
        private bool _impactPending;
        private TrackedAgent? _pendingAttacker;
        private TrackedAgent? _pendingVictim;
        private ImpactEvent _pendingEvent;
        private double _pendingAt;

        // step 62: the pending blow's decision and its answers - the shrug-off seam (CreateMeleeBlow's FIRST question) may decide it, the
        // KnockDown seam then takes these answers instead of deciding again
        private bool _pendingDecided;
        private bool _pendingKnockDown;
        private bool _pendingStaggered;

        // step 62: the clean hit just applied played a stagger (ApplyHit sets it - the shrug-off seam refuses the shrug-off for it)
        private bool _applyStaggered;

        private readonly ImpactFallStats _falls = new ImpactFallStats();

        /// <summary>Step 62: this mission's decided falls and what became of them (the smoke reads them).</summary>
        internal ImpactFallStats ImpactFallStats => _falls;

        /// <summary>This mission's impact decisions (the smoke reads them).</summary>
        internal ImpactStats ImpactModelStats => _impact;

        /// <summary>The engine's side of the inputs (the smoke swaps in its stand-in).</summary>
        internal IImpactInputs ImpactInputs { get; set; } = ImpactInputsDefault.Current;

        /// <summary>The impact model's dice (thread-safe; the smoke seeds its own).</summary>
        internal IRandomSource ImpactDice { get; set; } = ThreadSafeRandom.Shared;

        /// <summary>The last decision made (the smoke).</summary>
        internal ImpactDecision LastImpact { get; private set; }

        /// <summary>The model ran as of the last tick (the smoke).</summary>
        internal bool ImpactModelOn => _impactOn;

        // ------------------------------------------------------------------ lifecycle

        /// <summary>From StartImpact: the [impact] mission-start line of the model (IMPACT §6: the anchors in words).</summary>
        private void StartImpactModel()
        {
            var r = ImpactRules.From(TraxSettings.Shared);
            _impactOn = r.Enabled;
            _impactShadowSeen = r.ShadowNow;
            _impact.EverOn = r.Enabled;
            TraxLog.Info("impact", "mission start: the model (step 48 - IMPACT I2, docs/IMPACT.md) " + r.Describe() + " - read live; the [summary] \"impact\" lines count every decision by event and bar band (PLAYTEST D17)");
        }

        /// <summary>From TickImpact (own try there): the switch and the mode read live - one [impact] line per change; the on / off time.</summary>
        private void TickImpactModel(double now)
        {
            var r = ImpactRules.From(TraxSettings.Shared);
            double dt = double.IsNaN(_impactLastTick) || now < _impactLastTick ? 0 : now - _impactLastTick;
            _impactLastTick = now;
            if (_impactOn) _impact.OnSeconds += dt;
            else _impact.OffSeconds += dt;
            if (r.Enabled != _impactOn)
            {
                _impactOn = r.Enabled;
                _impact.Switches++;
                if (r.Enabled) _impact.EverOn = true;
                TraxLog.Info("impact", "the model switched " + (r.Enabled ? "ON" : "OFF (" + r.OffBecause + ")") + " mid-mission at " + Sec(now) + " s: " + r.ModeText()
                    + (r.Enabled ? string.Empty : " - nothing is decided (and nothing of it applies) from the next blow"));
                _impactShadowSeen = r.ShadowNow;
            }
            else if (r.Enabled && r.ShadowNow != _impactShadowSeen)
            {
                _impactShadowSeen = r.ShadowNow;
                TraxLog.Info("impact", "the model's mode changed mid-mission at " + Sec(now) + " s: " + r.ModeText());
            }
            _impactPending = false; // a decorator decision OnMeleeHit never took (another mod's model on top?) does not outlive the callback
        }

        /// <summary>The summary's turn (from WriteImpactSummary).</summary>
        private void WriteImpactModelSummary()
        {
            foreach (var line in _impact.SummaryLines(ImpactRules.From(TraxSettings.Shared))) TraxLog.Info("summary", line);
        }

        // ------------------------------------------------------------------ the events

        /// <summary>Which of the six events one melee collision is (none: a missile, a horse charge, a kick into a shield, a blow into nothing).</summary>
        private static bool ImpactEventOf(in AttackCollisionData cd, out ImpactEvent e)
        {
            e = ImpactEvent.CleanHit;
            if (cd.IsMissile || cd.IsHorseCharge) return false;
            bool alt = cd.IsAlternativeAttack;
            bool landed = alt
                ? SpikeMath.KickLanded(true, cd.AttackBlockedWithShield, (int)cd.CollisionResult)
                : !cd.AttackBlockedWithShield && (int)cd.CollisionResult == ImpactActions.ResultStrikeAgent;
            var block = BlockMath.KindOf(cd.IsMissile, alt, cd.IsHorseCharge, cd.CollidedWithShieldOnBack, cd.AttackBlockedWithShield, cd.CorrectSideShieldBlock, (int)cd.CollisionResult);
            return ImpactMath.EventOf(alt, alt && cd.AffectorWeaponSlotOrMissileIndex >= 0, landed, block, out e);
        }

        /// <summary>
        /// The damage decorator's KnockDown seam (MAIN thread, inside the engine's MeleeHitCallback, before the blow is registered): a LANDED
        /// kick / bash / clean hit on a man on foot is decided HERE and remembered for this callback's OnMeleeHit. Returns the knockdown to
        /// OR into the game's answer - only once its family APPLIES (step 50: kicks / bashes; step 52: clean hits). Own try: an error = no knockdown, the
        /// live KnockDown lever off for the mission (one [error]).
        /// </summary>
        internal bool ImpactModelOnLandedBlow(Agent attacker, Agent victim, in AttackCollisionData cd)
        {
            try
            {
                if (!_spikeStarted || _spikeClosed || attacker == null || victim == null) return false;
                if (!ImpactEventOf(in cd, out var ev) || ImpactMath.IsBlock(ev)) return false; // a blocked blow is never registered: OnMeleeHit decides it
                double now = SpikeNow;
                var a = Get(attacker);
                var v = Get(victim);
                // step 62: the shrug-off seam already decided THIS blow (the first of CreateMeleeBlow's questions) - its answer, never a second decision
                // (asked there and refused by a gate: not asked twice - one count per blow)
                if (_impactPending && ReferenceEquals(_pendingAttacker, a) && ReferenceEquals(_pendingVictim, v) && _pendingEvent == ev && Math.Abs(_pendingAt - now) < 1e-6)
                {
                    if (_pendingDecided) _falls.TakenFromShrugSeam++;
                    return _pendingKnockDown;
                }
                // seen here: OnMeleeHit takes it as it is (decided, or counted as not decided) - never a second decision or count
                _impactPending = true;
                _pendingAttacker = a;
                _pendingVictim = v;
                _pendingEvent = ev;
                _pendingAt = now;
                _pendingDecided = false;
                _pendingKnockDown = false;
                _pendingStaggered = false;
                _applyStaggered = false;
                if (!ImpactDecideEvent(a, v, ev, attacker, victim, in cd, now, true, out _, out bool knockDown)) return false;
                _impact.FromDecorator++;
                _pendingDecided = true;
                _pendingKnockDown = knockDown;
                _pendingStaggered = _applyStaggered;
                return knockDown;
            }
            catch (Exception e)
            {
                if (_inDismountSeam) DisableImpactLever(ImpactLever.Dismount, "impact.model-dismount", e); // step 53: the rider's seam
                else DisableImpactLever(ImpactLever.KnockDown, "impact.model-knockdown", e);
                return false;
            }
        }

        /// <summary>OnMeleeHit's part (its own try there): the event's decision - the decorator's for this blow if it made one, else made here.</summary>
        internal void ImpactModelHitTaken(Agent attacker, Agent victim, in AttackCollisionData cd)
        {
            if (!_spikeStarted || _spikeClosed || attacker == null || victim == null) return;
            if (!ImpactEventOf(in cd, out var ev)) return;
            var a = Get(attacker);
            var v = Get(victim);
            double now = SpikeNow;
            if (v != null && _liveStaggers > 0) NoteInsideLiveStagger(v, ImpactMath.IsBlock(ev)); // step 51: a block made / a hit taken from inside our stagger
            if (_impactPending)
            {
                _impactPending = false;
                if (ReferenceEquals(_pendingAttacker, a) && ReferenceEquals(_pendingVictim, v) && _pendingEvent == ev && Math.Abs(_pendingAt - now) < 1e-6) return; // decided once, in the decorator
            }
            ImpactDecideEvent(a, v, ev, attacker, victim, in cd, now, false, out _, out _);
        }

        /// <summary>The gates (the master switch first, Athletics, ImpactEnabled, the family, both men tracked), the engine's inputs, then
        /// <see cref="ImpactDecide"/>. A failing read = one [error] and the event counted "read failed".</summary>
        private bool ImpactDecideEvent(TrackedAgent? a, TrackedAgent? v, ImpactEvent ev, Agent attacker, Agent victim, in AttackCollisionData cd, double now, bool inSeam,
            out ImpactDecision d, out bool knockDown)
        {
            d = default;
            knockDown = false;
            if (!ImpactGate(a, v, ev, out _)) return false;
            var f = new ImpactFacts { Event = ev };
            try
            {
                ImpactInputs.Fill(attacker, victim, in cd, ref f);
                // step 51: the weapon's name for a YOU line about a block (asked only when you are one of the two - it allocates)
                _youBlowName = ImpactMath.IsBlock(ev) && (IsPlayer(a!) || IsPlayer(v!)) ? ImpactInputs.BlowName(attacker, in cd) : null;
            }
            catch (Exception e)
            {
                Failed("impact.read", e);
                _impact.Skip(ImpactSkip.ReadFailed, ev);
                return false;
            }
            if (f.Fatal && ImpactMath.IsLanded(ev)) // step 62: he dies of it - his death plays, not our fall (a decided fall was counted "not seen")
            {
                _impact.Skip(ImpactSkip.Fatal, ev);
                _falls.Fatal++;
                return false;
            }
            try
            {
                return ImpactDecide(a, v, ref f, now, inSeam, out d, out knockDown);
            }
            finally
            {
                _youBlowName = null;
            }
        }

        private bool ImpactGate(TrackedAgent? a, TrackedAgent? v, ImpactEvent ev, out ImpactRules r)
        {
            r = ImpactRules.From(TraxSettings.Shared);
            if (!r.Enabled)
            {
                _impact.Skip(ImpactSkip.Off, ev);
                return false;
            }
            if (!r.DecidesEvent(ev)) // step 51: a right-side block / parry is decided for the riposte alone when only the blocks' switch is off
            {
                _impact.Skip(ImpactSkip.FamilyOff, ev);
                return false;
            }
            if (a == null || v == null || a.Removed || v.Removed)
            {
                _impact.Skip(ImpactSkip.NotTracked, ev);
                return false;
            }
            return true;
        }

        /// <summary>
        /// ONE decision on tracked men (the smoke drives it with made-up engine inputs): the gates again, both men's Athletics into
        /// <paramref name="f"/> (CURRENT points, bar, f, the attacker's swing animation share - step 20b's line -, the Athletics skill for a
        /// kick and for no guard, who is you), <see cref="ImpactMath.Decide"/> on both men's states, counted, logged.
        /// </summary>
        internal bool ImpactDecide(TrackedAgent? a, TrackedAgent? v, ref ImpactFacts f, double now, out ImpactDecision d) => ImpactDecide(a, v, ref f, now, false, out d, out _);

        /// <summary>The decision, and (step 50) its effect when its family applies: <paramref name="inSeam"/> = asked from the decorator's
        /// KnockDown seam (only there can a knockdown be answered); <paramref name="knockDown"/> = the answer for it.</summary>
        internal bool ImpactDecide(TrackedAgent? a, TrackedAgent? v, ref ImpactFacts f, double now, bool inSeam, out ImpactDecision d, out bool knockDown)
        {
            d = default;
            knockDown = false;
            if (!ImpactGate(a, v, f.Event, out var r)) return false;
            long t0 = Stopwatch.GetTimestamp();
            var ar = Rules;
            double fA = AthleticsMath.PeakShare(in ar, a!);
            f.AttackerPoints = AthleticsMath.Points(in ar, a!);
            f.AttackerF = fA;
            f.AttackerAnimation = AttackTimerMath.AnimationMultiplier(fA, TraxSettings.Shared.AttackAnimationMinPercent);
            f.DefenderPoints = AthleticsMath.Points(in ar, v!);
            f.DefenderBar = v!.Fraction;
            f.DefenderF = AthleticsMath.PeakShare(in ar, v);
            if (f.Event == ImpactEvent.Kick) f.AttackerSkill = a!.AthleticsSkill;
            if (f.Event == ImpactEvent.Kick || f.Event == ImpactEvent.Bash || f.Event == ImpactEvent.CleanHit) f.DefenderSkill = v.AthleticsSkill;
            f.AttackerIsPlayer = IsPlayer(a!);
            f.DefenderIsPlayer = IsPlayer(v);
            var family = ImpactMath.Family(f.Event);
            bool block = ImpactMath.IsBlock(f.Event);
            // step 50: what applies writes the states; shadow keeps "would"; a family decided live with no effect built yet decides on
            // copies (it never happened). Step 53: a RIDER's decision applies too - his own path (ApplyRider: knocked off his horse, his shield;
            // never a stagger) - and writes his state like any other.
            // Step 51: a block's own outcomes write the BLOCKER's state, the riposte writes the ATTACKER's (its own switch and family).
            bool applied = r.Applies(family) && !f.DefenderMounted;
            bool riderLive = r.Applies(family) && f.DefenderMounted; // step 53
            bool riposteLive = block && ImpactMath.CanRiposte(f.Event) && r.Applies(ImpactFamily.Riposte) && !f.AttackerMounted;
            bool keep = r.KeepsState(family) && (applied || riderLive || r.ShadowNow);
            bool keepAttacker = block ? r.KeepsState(ImpactFamily.Riposte) && (riposteLive || r.ShadowNow) : keep;
            var vs = v.Impact;
            var ast = a!.Impact;
            d = ImpactMath.Decide(in r, in f, ref vs, ref ast, now, ImpactDice);
            if (keep) v.Impact = vs;
            if (keepAttacker) a.Impact = ast;
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            int attackerBand = ImpactMath.BarBand(a.Fraction);
            _impact.Note(in d, r.ShadowNow, applied || riposteLive || riderLive, ms, attackerBand);
            // step 52: every decided blow by the attacker's speed into it; step 53: a rider's by his horse's, a couched lance apart
            _hits.NoteMomentum(in d, f.AttackerMounted ? ImpactMath.RiderSpeedBand(in r, d.AttackerSpeed) : ImpactMath.SpeedBand(in r, d.AttackerSpeed), f.AttackerMounted, f.AttackerPassive);
            if (block && (applied || riderLive)) _riders.NoteRiposteOnRider(in d);
            LastImpact = d;
            NoteImpactLines(a, v, in d, in f, in r, !(applied || riposteLive || riderLive));
            if (applied && family == ImpactFamily.Kicks) knockDown = ApplyKick(a, v, in d, inSeam, now);
            else if (applied && family == ImpactFamily.Hits) knockDown = ApplyHit(a, v, in d, in f, inSeam, now); // step 52
            else if (riderLive)
            {
                knockDown = ApplyRider(a, v, in d, now); // step 53: knocked off his horse (only the dismount seam answers a landed blow), his shield
                if (riposteLive) ApplyBlock(a, v, in d, now, false, true, attackerBand); // a mounted blocker's riposte on a man on foot
            }
            else if (block && (applied || riposteLive)) ApplyBlock(a, v, in d, now, applied, riposteLive, attackerBand);
            if (!r.ShadowNow) // step 54: a window opened by what was really written (after the apply's own fixes)
            {
                if (keep) NotePunishWindow(v, now);
                if (keepAttacker) NotePunishWindow(a, now);
            }
            return true;
        }

        /// <summary>The first decision of each event kind and the first of each outcome in full (IMPACT §6), the rest verbose (bucket
        /// "impact" - asked before the line is built, R8).</summary>
        private void NoteImpactLines(TrackedAgent a, TrackedAgent v, in ImpactDecision d, in ImpactFacts f, in ImpactRules r, bool shadow)
        {
            int e = (int)d.Event;
            if (!_impactFirst[e])
            {
                _impactFirst[e] = true;
                TraxLog.Info("impact", "first " + ImpactMath.EventName(d.Event) + " decided at " + Sec(d.Now) + " s - " + ImpactMath.Describe(in d, in f, Name(a), Name(v), ModeWords(in d, in r, shadow)));
            }
            else if (FirstOutcome(in d, out var o))
                TraxLog.Info("impact", "first time " + ImpactMath.OutcomeName(o) + (shadow ? " WOULD happen" : " happens") + ", at " + Sec(d.Now) + " s - "
                    + ImpactMath.Describe(in d, in f, Name(a), Name(v), ModeWords(in d, in r, shadow)));
            else if (TraxLog.VerboseWants("impact"))
                TraxLog.Verbose("impact", ImpactMath.Describe(in d, in f, Name(a), Name(v), ModeWords(in d, in r, shadow)));
        }

        /// <summary>The decision's mode in the line's words - built only when a line is written (no allocation per event).</summary>
        private static string ModeWords(in ImpactDecision d, in ImpactRules r, bool notApplied) => notApplied
            ? (r.ShadowNow ? "shadow - nothing applied"
               : r.Applies(ImpactMath.Family(d.Event)) ? "LIVE, the riposte on a rider refused"
               : "decided and logged only - its effect comes in step " + EffectStep(d.Event))
            : "LIVE";

        /// <summary>The first outcome of its kind this mission in this decision (each kind logged once in full).</summary>
        private bool FirstOutcome(in ImpactDecision d, out ImpactOutcome first)
        {
            first = ImpactOutcome.Knockdown;
            bool any = false;
            for (int i = 0; i < ImpactMath.OutcomeCount; i++)
            {
                var o = (ImpactOutcome)i;
                if (!d.Outcome(o).Happens || _impactFirst[ImpactMath.EventCount + i]) continue;
                _impactFirst[ImpactMath.EventCount + i] = true;
                if (!any) first = o;
                any = true;
            }
            return any;
        }

        private static string EffectStep(ImpactEvent e) => ImpactMath.Family(e) switch
        {
            ImpactFamily.Kicks => "50",
            ImpactFamily.Blocks => "51",
            _ => "52",
        };
    }
}

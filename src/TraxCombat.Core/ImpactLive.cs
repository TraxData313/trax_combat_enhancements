using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>Step 50 (IMPACT I3): the engine levers the LIVE kicks / bashes pull - each disabled alone for the mission when it throws (the
    /// spike's pattern, step 47).</summary>
    public enum ImpactLever : byte
    {
        /// <summary>The damage decorator's KnockDown answer for a landed kick / bash.</summary>
        KnockDown,

        /// <summary>The queued drop of the kicked man's shield (DropItem through the tick).</summary>
        ShieldDrop,

        /// <summary>SetCurrentActionSpeed on the kicked man's reel or his fall-and-rise.</summary>
        SlowReaction,

        /// <summary>The channel reads that watch a reel / a fall (and so measure it and write its end into his state).</summary>
        Watch,

        /// <summary>Step 51: the blocker's stun lengthened by his tiredness (the damage decorator's stun seam - WORKER threads) and the raised
        /// stun cap.</summary>
        BlockStun,

        /// <summary>Step 51: the rolled stagger of a blocker and the riposte's stagger of an attacker (SetActionChannel act_stagger_* from the tick).</summary>
        ForcedStagger,

        /// <summary>Step 51: a blocker knocked down - a blow of our own (RegisterBlow, KnockDown, 1 damage) from the tick.</summary>
        OwnBlow,

        /// <summary>Step 52: a tired man's shrug-off refused (the damage decorator's <c>DecideAgentShrugOffBlow</c> - main thread).</summary>
        ShrugOff,
    }

    /// <summary>Step 50: why a live outcome the dice gave was NOT carried out (the summary's "not applied").</summary>
    public enum ImpactNotApplied : byte
    {
        /// <summary>He rides a horse - knocked off / anything on a rider is step 53's; nothing applied, his state untouched.</summary>
        Rider,

        /// <summary>A knockdown decided outside the decorator's KnockDown seam (another mod's damage model on top of ours) - too late to flag.</summary>
        OutsideSeam,

        /// <summary>Its lever was disabled by an error this mission.</summary>
        LeverOff,

        /// <summary>The fixed pool was full (more than <see cref="ImpactLiveMath.MaxDrops"/> drops waiting / watches running).</summary>
        PoolFull,

        /// <summary>The shield was no longer in his hand when the tick came to drop it.</summary>
        NoShieldAtTick,

        /// <summary>He left the field before the tick.</summary>
        LeftField,

        /// <summary>He was in his own release / bash for <see cref="ImpactLiveMath.BusyWaitSeconds"/> - vanilla never drops then.</summary>
        Busy,

        /// <summary>Step 51: refused before any engine call at the tick (<c>IImpactBody.CanForce</c>: already on the ground, a siege engine /
        /// ladder, water, a horse under him by then).</summary>
        CannotForce,

        /// <summary>Step 51: the engine did not take it (SetActionChannel false both with and over its priority / RegisterBlow refused).</summary>
        EngineRefused,
    }

    /// <summary>Step 50: what a live watch measures.</summary>
    public enum ImpactWatchPurpose : byte
    {
        /// <summary>The kicked man's knock-back reel (slowed by the decision's ReelFactor, or a control at factor 1).</summary>
        Reel,

        /// <summary>The knocked-down man's fall-and-rise (slowed by GetUpFactor).</summary>
        GetUp,

        /// <summary>Step 51: a blocker's rolled stagger (our act_stagger_* at the speed for its length) - seen, its length, blocks from inside it.</summary>
        Stagger,

        /// <summary>Step 51: the riposte - the ATTACKER's forced stagger after a fresh man's right-side block or parry.</summary>
        Riposte,

        /// <summary>Step 51: a blocker knocked down by our own blow - its Fall (slowed by GetUpFactor, as a kick's get-up).</summary>
        BlockGetUp,

        /// <summary>Step 51: the blocker's guard frozen by the stun (asked x the stun factor; a fresh blocker's = the control).</summary>
        Guard,

        /// <summary>Step 52: a clean hit's STAGGER - the engine's own flinch slowed to the decided length (WatchKind.Reaction).</summary>
        HitFlinch,

        /// <summary>Step 52: a man knocked down by a clean hit - his fall-and-rise (slowed by GetUpFactor, as a kick's get-up).</summary>
        HitGetUp,

        /// <summary>Step 52: an untouched flinch of a clean hit with no outcome - the control the stagger's lengths compare against and
        /// the flinch's own length is learned from (a few at a time).</summary>
        HitControl,
    }

    /// <summary>
    /// Step 50 (IMPACT I3, AI_NOTES "Step 50"): the pure rules of the live kicks / bashes - the speed a factor asks of the engine, the
    /// plumbing constants (documented, not settings: the spike's proven windows), the shield's pickup lock.
    /// </summary>
    public static class ImpactLiveMath
    {
        public const int LeverCount = 8;
        public const int NotAppliedCount = 9;

        /// <summary>Live watches (reels, falls) at once - 64, the spike's pool.</summary>
        public const int MaxWatches = SpikeMath.MaxWatches;

        /// <summary>Shield drops waiting for the tick / shields followed after the drop.</summary>
        public const int MaxDrops = SpikeMath.MaxPending;

        public const int MaxShields = SpikeMath.MaxShields;

        /// <summary>At most this many engine calls (drops, speed changes) a tick - the rest wait a tick (IMPACT §3.9's ImpactMaxActionsPerTick).</summary>
        public const int MaxActionsPerTick = SpikeMath.MaxActionsPerTick;

        /// <summary>The reel must show within 0.6 s of the kick, the fall within 1.0 s (the spike's windows).</summary>
        public const double ReelAppearSeconds = SpikeMath.AppearSeconds;

        public const double FallAppearSeconds = SpikeMath.FallAppearSeconds;

        /// <summary>The slowest speed we ever ask (a factor above 4 is clamped) - the model's caps keep it far above (x2.5 × 1.3 = 3.25).</summary>
        public const float MinSpeed = 0.25f;

        /// <summary>A factor this close to 1 is not slowed (a fresh man's reel - the control).</summary>
        public const double SlowFrom = 1.001;

        /// <summary>The shield followed for at most 180 s after it left the hand (picked up again? - the spike's window).</summary>
        public const double PickupWindowSeconds = SpikeMath.PickupWindowSeconds;

        public const double DropSeenSeconds = SpikeMath.DropSeenSeconds;

        public const double ShieldLookSeconds = SpikeMath.ShieldLookSeconds;

        /// <summary>A queued drop waits at most this long for his own release / bash to pass (vanilla's drop rule), then gives up.</summary>
        public const double BusyWaitSeconds = 1.0;

        /// <summary>The factor needs the engine's help (above <see cref="SlowFrom"/>).</summary>
        public static bool NeedsSlow(double factor) => !double.IsNaN(factor) && factor > SlowFrom;

        /// <summary>The ABSOLUTE playback speed for a length × <paramref name="factor"/>: 1 ÷ factor, never below <see cref="MinSpeed"/>,
        /// 1 for no factor (NaN, ≤ 1).</summary>
        public static float SpeedFor(double factor)
        {
            if (!NeedsSlow(factor)) return 1f;
            return (float)Math.Max(MinSpeed, 1.0 / factor);
        }

        /// <summary>The pickup lock: a dropped shield may not be picked up again by the AI until <paramref name="droppedAt"/> +
        /// <paramref name="lockSeconds"/> (0 = vanilla - never locked).</summary>
        public static bool PickupLocked(double now, double droppedAt, double lockSeconds)
            => lockSeconds > 0 && !double.IsNaN(droppedAt) && now >= droppedAt && now < droppedAt + lockSeconds;

        public static string LeverName(ImpactLever l) => l switch
        {
            ImpactLever.KnockDown => "the KnockDown answer",
            ImpactLever.ShieldDrop => "the shield drop",
            ImpactLever.SlowReaction => "the slowed reel / get-up",
            ImpactLever.BlockStun => "the block stun by tiredness",
            ImpactLever.ForcedStagger => "the forced stagger (blocks, the riposte)",
            ImpactLever.OwnBlow => "the blocker's knockdown (our own blow)",
            ImpactLever.ShrugOff => "tired men always flinch (the shrug-off refused)",
            _ => "the reaction watch",
        };

        public static string NotAppliedName(ImpactNotApplied n) => n switch
        {
            ImpactNotApplied.Rider => "a rider (step 53)",
            ImpactNotApplied.OutsideSeam => "decided outside the decorator's seam",
            ImpactNotApplied.LeverOff => "its lever disabled",
            ImpactNotApplied.PoolFull => "the pool full",
            ImpactNotApplied.NoShieldAtTick => "the shield no longer in hand at the tick",
            ImpactNotApplied.Busy => "in his own swing / bash for 1 s",
            ImpactNotApplied.CannotForce => "refused before any engine call (down / a siege engine / water)",
            ImpactNotApplied.EngineRefused => "the engine refused it",
            _ => "he left the field",
        };
    }

    /// <summary>
    /// Step 50: one mission's LIVE kicks and bashes (IMPACT §6): per outcome (knockdown, shield drop) × the kicked man's bar band - decided
    /// (the dice said yes), applied (the lever pulled), seen (the Fall showed / the shield left his hand) - and why not; the reel and the
    /// get-up asked vs MEASURED (mean, max; untouched reels as the control); the shields dropped and picked up again (after how long); the
    /// limits that refused or halved a live kick; the levers disabled; the cost of the tick. Main thread only. The [summary] "impact live" lines.
    /// </summary>
    public sealed class ImpactLiveStats
    {
        private const int B = ImpactMath.BandCount;

        /// <summary>[0 = knockdown, 1 = shield drop, band].</summary>
        public readonly int[,] Decided = new int[2, B];
        public readonly int[,] Applied = new int[2, B];
        public readonly int[] Seen = new int[2];
        public readonly int[,] NotApplied = new int[2, ImpactLiveMath.NotAppliedCount];

        public int LiveDecisions;
        public int Reeling;
        public int RefusedDown;
        public int RefusedKnockdownImmune;
        public int Halved;
        public int YouDealt;
        public int YouTook;

        public int FallNotSeen;
        public int ReelNotSeen;
        public int FellInstead;
        public int Capped;
        public int SlowCalls;
        public int SlowReapplied;
        public int CallsDeferred;
        public int WatchesFull;
        public int LeftField;

        public MeanStd ReelAsked, ReelMeasured, ReelControl, GetUpAsked, GetUpMeasured, GetUpControl;
        public double ReelAskedMax, ReelMeasuredMax, GetUpAskedMax, GetUpMeasuredMax;

        public int DropsQueued;
        public int Dropped;
        public int DropNotSeen;
        public int PickedUp;
        public MeanStd PickupSeconds;
        public int StillOnGround;
        public int DropperLeftField;
        public int PickupsLocked;

        public int ShockGiven;

        public double TickMs;
        public double MaxTickMs;
        public int Ticks;

        private readonly bool[] _disabled = new bool[ImpactLiveMath.LeverCount];
        private readonly string?[] _disabledAt = new string?[ImpactLiveMath.LeverCount];

        public bool Disabled(ImpactLever l) => _disabled[(int)l];

        public string? DisabledAt(ImpactLever l) => _disabledAt[(int)l];

        /// <summary>The lever off for the rest of the mission (true = this call disabled it).</summary>
        public bool Disable(ImpactLever l, string site)
        {
            if (_disabled[(int)l]) return false;
            _disabled[(int)l] = true;
            _disabledAt[(int)l] = site;
            return true;
        }

        public int DisabledCount
        {
            get
            {
                int n = 0;
                foreach (var d in _disabled) if (d) n++;
                return n;
            }
        }

        private static int Index(ImpactOutcome o) => o == ImpactOutcome.Knockdown ? 0 : 1;

        /// <summary>One LIVE kick / bash decision (its family applies): what the dice said and what the limits did.</summary>
        public void NoteDecision(in ImpactDecision d)
        {
            LiveDecisions++;
            int b = ImpactMath.BarBand(d.Bar);
            if (d.Knockdown.Happens) Decided[0, b]++;
            if (d.ShieldDrop.Happens) Decided[1, b]++;
            if (d.Reeling) Reeling++;
            if (d.Knockdown.Refusal == ImpactRefusal.Down) RefusedDown++;
            else if (d.Knockdown.Refusal == ImpactRefusal.KnockdownImmune && d.Knockdown.Raw > 0) RefusedKnockdownImmune++;
            if ((d.Knockdown.Halvings > 0 && d.Knockdown.Raw > 0 && d.Knockdown.Refusal == ImpactRefusal.None)
                || (d.ShieldDrop.Halvings > 0 && d.ShieldDrop.Raw > 0 && d.ShieldDrop.Refusal == ImpactRefusal.None))
                Halved++;
            if (d.AnyOutcome)
            {
                if (d.AttackerIsPlayer) YouDealt++;
                if (d.DefenderIsPlayer) YouTook++;
            }
        }

        public void NoteApplied(ImpactOutcome o, double bar) => Applied[Index(o), ImpactMath.BarBand(bar)]++;

        public void NoteSeen(ImpactOutcome o) => Seen[Index(o)]++;

        public void NoteNotApplied(ImpactOutcome o, ImpactNotApplied why) => NotApplied[Index(o), (int)why]++;

        public void NoteReel(double asked, double measured, bool slowed)
        {
            if (slowed)
            {
                ReelAsked.Add(asked);
                ReelMeasured.Add(measured);
                if (asked > ReelAskedMax) ReelAskedMax = asked;
                if (measured > ReelMeasuredMax) ReelMeasuredMax = measured;
            }
            else ReelControl.Add(measured);
        }

        public void NoteGetUp(double asked, double measured, bool slowed)
        {
            if (slowed)
            {
                GetUpAsked.Add(asked);
                GetUpMeasured.Add(measured);
                if (asked > GetUpAskedMax) GetUpAskedMax = asked;
                if (measured > GetUpMeasuredMax) GetUpMeasuredMax = measured;
            }
            else GetUpControl.Add(measured);
        }

        public void AddTick(double ms)
        {
            Ticks++;
            TickMs += ms;
            if (ms > MaxTickMs) MaxTickMs = ms;
        }

        private int Sum(int[,] a, int o)
        {
            int n = 0;
            for (int b = 0; b < a.GetLength(1); b++) n += a[o, b];
            return n;
        }

        public int DecidedOf(ImpactOutcome o) => Sum(Decided, Index(o));

        public int AppliedOf(ImpactOutcome o) => Sum(Applied, Index(o));

        /// <summary>
        /// The [summary] "impact live" lines (step 50): one line when nothing live happened (with why), else the outcomes by band (decided →
        /// applied → seen, not applied by reason), the lengths asked vs measured, the shields, the limits, the levers and the cost.
        /// </summary>
        public IReadOnlyList<string> SummaryLines(bool liveNow, string mode)
        {
            var lines = new List<string>();
            if (LiveDecisions == 0 && DropsQueued == 0 && ReelMeasured.Count == 0 && GetUpMeasured.Count == 0 && DisabledCount == 0)
            {
                lines.Add("impact live (step 50 - kicks and bashes): no live kick or bash this mission (" + mode + ")");
                return lines;
            }
            var s = new StringBuilder("impact live (step 50 - kicks and bashes; " + mode + "): ");
            s.Append(I(LiveDecisions)).Append(" live kicks / bashes by the kicked man's bar ").Append(Bands()).Append(" - ");
            Outcome(s, 0, "knocked down", "Fall seen");
            s.Append("; ");
            Outcome(s, 1, "shield dropped", "left the hand");
            s.Append("; YOU: dealt ").Append(I(YouDealt)).Append(", took ").Append(I(YouTook)).Append("; outcomes counted as courage's SHOCK ").Append(I(ShockGiven));
            lines.Add(s.ToString());

            lines.Add("impact live lengths: the reel asked " + Times(in ReelAsked, ReelAskedMax) + " → measured " + Secs(in ReelMeasured, ReelMeasuredMax) + " vs untouched reels "
                      + Secs(in ReelControl, double.NaN) + Ratio(in ReelMeasured, in ReelControl) + "; the get-up asked " + Times(in GetUpAsked, GetUpAskedMax) + " → the fall-and-rise measured "
                      + Secs(in GetUpMeasured, GetUpMeasuredMax) + (GetUpControl.Count > 0 ? " vs untouched " + Secs(in GetUpControl, double.NaN) + Ratio(in GetUpMeasured, in GetUpControl) : " (vanilla's ~1.5 s untouched)")
                      + "; slowed " + I(SlowCalls) + "x (again on a _continue " + I(SlowReapplied) + "); no reel seen " + I(ReelNotSeen) + ", fell instead " + I(FellInstead)
                      + ", no fall seen after a knockdown " + I(FallNotSeen) + ", at the 8 s cap " + I(Capped) + " - the measured ends are his state's (immunity from them)");

            lines.Add("impact live shields: drops queued from the tick " + I(DropsQueued) + ", left the hand " + I(Dropped) + " (not seen " + I(DropNotSeen) + "), picked up again "
                      + I(PickedUp) + (PickedUp > 0 ? " after " + Secs(in PickupSeconds, double.NaN) : string.Empty) + ", still on the ground " + I(StillOnGround)
                      + ", the man left the field first " + I(DropperLeftField) + ", AI pickups refused by ShieldPickupLockSeconds " + I(PickupsLocked)
                      + " (the kicks' and, since step 51, the wrong-side blocks' drops together)");

            lines.Add("impact live limits: knockdown refused - on the ground " + I(RefusedDown) + ", within the immunity after getting up " + I(RefusedKnockdownImmune)
                      + "; halved by a recent outcome " + I(Halved) + "; kicked while reeling (+bonus) " + I(Reeling)
                      + "; levers disabled " + LeversText() + "; tick " + (Ticks > 0 ? F3(TickMs / Ticks) : "0") + " ms (max " + F3(MaxTickMs) + "), calls deferred " + I(CallsDeferred)
                      + ", pools full " + I(WatchesFull) + ", left the field " + I(LeftField) + (liveNow ? string.Empty : "; NOT live at the end"));
            return lines;
        }

        private void Outcome(StringBuilder s, int o, string name, string seenName)
        {
            var dec = new int[B];
            var app = new int[B];
            int dt = 0, at = 0;
            for (int b = 0; b < B; b++)
            {
                dec[b] = Decided[o, b];
                app[b] = Applied[o, b];
                dt += dec[b];
                at += app[b];
            }
            s.Append(name).Append(" decided ").Append(Row(dec)).Append(" = ").Append(I(dt)).Append(", applied ").Append(Row(app)).Append(" = ").Append(I(at))
                .Append(", ").Append(seenName).Append(' ').Append(I(Seen[o]));
            var not = new StringBuilder();
            for (int n = 0; n < ImpactLiveMath.NotAppliedCount; n++)
            {
                if (NotApplied[o, n] == 0) continue;
                if (not.Length > 0) not.Append(", ");
                not.Append(ImpactLiveMath.NotAppliedName((ImpactNotApplied)n)).Append(' ').Append(I(NotApplied[o, n]));
            }
            if (not.Length > 0) s.Append(" (not applied: ").Append(not).Append(')');
        }

        public string LeversText()
        {
            if (DisabledCount == 0) return "none";
            var s = new StringBuilder();
            for (int i = 0; i < ImpactLiveMath.LeverCount; i++)
            {
                if (!_disabled[i]) continue;
                if (s.Length > 0) s.Append(", ");
                s.Append(ImpactLiveMath.LeverName((ImpactLever)i)).Append(" (at ").Append(_disabledAt[i]).Append(')');
            }
            return s.ToString();
        }

        private static string Bands()
        {
            var s = new StringBuilder();
            for (int b = 0; b < B; b++)
            {
                if (b > 0) s.Append(" / ");
                s.Append(ImpactMath.BandName(b));
            }
            return s.ToString();
        }

        private static string Row(int[] v)
        {
            var s = new StringBuilder();
            for (int b = 0; b < v.Length; b++)
            {
                if (b > 0) s.Append(" / ");
                s.Append(I(v[b]));
            }
            return s.ToString();
        }

        private static string Times(in MeanStd m, double max) => m.Count == 0 ? "none" : "x" + F2(m.Mean) + " (max x" + F2(max) + ", n " + I(m.Count) + ")";

        private static string Secs(in MeanStd m, double max) => m.Count == 0 ? "none"
            : F2(m.Mean) + " s" + (double.IsNaN(max) ? string.Empty : " (max " + F2(max) + " s)") + " (n " + I(m.Count) + ")";

        private static string Ratio(in MeanStd treated, in MeanStd control) => treated.Count > 0 && control.Count > 0 && control.Mean > 0
            ? " = x" + F2(treated.Mean / control.Mean) + " measured"
            : string.Empty;

        private static string I(long v) => v.ToString(CultureInfo.InvariantCulture);

        private static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        private static string F3(double v) => v.ToString("0.000", CultureInfo.InvariantCulture);
    }
}

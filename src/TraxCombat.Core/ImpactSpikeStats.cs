using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 47: lever 2's tally - written by the damage decorator's <c>CalculateDefendedBlowStunMultipliers</c>, which the engine may call on
    /// a WORKER thread (<c>GetDefendCollisionResults</c> is <c>[MBCallback(null, true)]</c>, RESEARCH §R2): Interlocked counters only, no
    /// lock, no allocation; the stun sums in microseconds. Read on the main thread for the summary.
    /// </summary>
    public sealed class BlockStunTally
    {
        private long _calls, _treated, _control, _offMain, _capped;
        private long _defBeforeUs, _defAfterUs, _attBeforeUs, _attAfterUs;
        private long _ctlDefUs, _ctlAttUs;

        public long Calls => Interlocked.Read(ref _calls);
        public long Treated => Interlocked.Read(ref _treated);
        public long Control => Interlocked.Read(ref _control);

        /// <summary>Calls on a thread other than the mission's main thread (the proof of RESEARCH §R2's threading in game).</summary>
        public long OffMainThread => Interlocked.Read(ref _offMain);

        /// <summary>Treated blocks whose x3 hit the raised cap.</summary>
        public long Capped => Interlocked.Read(ref _capped);

        public void Clear()
        {
            Interlocked.Exchange(ref _calls, 0);
            Interlocked.Exchange(ref _treated, 0);
            Interlocked.Exchange(ref _control, 0);
            Interlocked.Exchange(ref _offMain, 0);
            Interlocked.Exchange(ref _capped, 0);
            Interlocked.Exchange(ref _defBeforeUs, 0);
            Interlocked.Exchange(ref _defAfterUs, 0);
            Interlocked.Exchange(ref _attBeforeUs, 0);
            Interlocked.Exchange(ref _attAfterUs, 0);
            Interlocked.Exchange(ref _ctlDefUs, 0);
            Interlocked.Exchange(ref _ctlAttUs, 0);
        }

        /// <summary>One call of the decorator's hook (any thread).</summary>
        public void Call(bool offMainThread)
        {
            Interlocked.Increment(ref _calls);
            if (offMainThread) Interlocked.Increment(ref _offMain);
        }

        /// <summary>A control block (vanilla's stun, measured as the baseline).</summary>
        public void AddControl(float defender, float attacker)
        {
            Interlocked.Increment(ref _control);
            Interlocked.Add(ref _ctlDefUs, Us(defender));
            Interlocked.Add(ref _ctlAttUs, Us(attacker));
        }

        /// <summary>A treated block: the stun before → after (seconds).</summary>
        public void AddTreated(float defBefore, float defAfter, float attBefore, float attAfter, bool capped)
        {
            Interlocked.Increment(ref _treated);
            if (capped) Interlocked.Increment(ref _capped);
            Interlocked.Add(ref _defBeforeUs, Us(defBefore));
            Interlocked.Add(ref _defAfterUs, Us(defAfter));
            Interlocked.Add(ref _attBeforeUs, Us(attBefore));
            Interlocked.Add(ref _attAfterUs, Us(attAfter));
        }

        public double DefenderBefore => Mean(ref _defBeforeUs, Treated);
        public double DefenderAfter => Mean(ref _defAfterUs, Treated);
        public double AttackerBefore => Mean(ref _attBeforeUs, Treated);
        public double AttackerAfter => Mean(ref _attAfterUs, Treated);
        public double ControlDefender => Mean(ref _ctlDefUs, Control);
        public double ControlAttacker => Mean(ref _ctlAttUs, Control);

        private static long Us(float seconds) => float.IsNaN(seconds) || seconds <= 0 ? 0 : (long)Math.Round(seconds * 1e6);

        private static double Mean(ref long sumUs, long n) => n > 0 ? Interlocked.Read(ref sumUs) / 1e6 / n : double.NaN;
    }

    /// <summary>
    /// Step 47: one battle of the spike - per lever what was asked, what the engine showed, the measured lengths (treated vs control),
    /// refusals, errors, the cost - and the [summary] "impact spike" lines with each lever's verdict and what it means for steps 48-53.
    /// Main thread (lever 2's worker-thread part is <see cref="Stun"/>).
    /// </summary>
    public sealed class ImpactSpikeStats
    {
        private readonly bool[] _disabled = new bool[SpikeMath.LeverCount];
        private readonly string?[] _disabledAt = new string?[SpikeMath.LeverCount];

        /// <summary>Lever 2's worker-thread tally.</summary>
        public readonly BlockStunTally Stun = new BlockStunTally();

        // ---- the spike over the battle
        public bool EverOn;
        public double OnSeconds;
        public double OffSeconds;
        public int Switches;
        public int EngineCalls;
        public int MostCallsInTick;
        public int CallsDeferred;
        public int WatchesFull;
        public int PendingFull;
        public int LeftField;
        public MeanStd TickMs;
        public double MaxTickMs;

        // ---- lever 1: KnockDown on a kick
        public int KicksLanded;
        public int Bashes;
        public int KicksZeroDamage;
        public int KicksOnRiders;
        public int KnockDownAsked;     // the decorator answered true for it
        public int KnockDownPlanned;   // the rotation said KnockDown (the OnMeleeHit side)
        public int FallSeen;
        public int FallNotSeen;
        public int ControlKicks;       // kicks NOT asked for a knockdown, watched
        public int ControlFalls;       // ... that fell anyway (vanilla never knocks down with a kick)
        public readonly int[] ControlFirstTypes = new int[64];

        // ---- lever 2: block stun (measured on the main thread)
        public int BlocksSeen;
        public MeanStd RecoilControl;
        public MeanStd RecoilTreated;
        public MeanStd GuardControl;
        public MeanStd GuardTreated;
        public int RecoilNotSeen;
        public int GuardNotSeen;
        public float CapBefore = float.NaN;
        public float CapRaisedTo = float.NaN;
        public int CapRestores;

        // ---- lever 3: reaction speed
        public int SlowCalls;
        public int SlowReapplied;
        public MeanStd ReelControl;
        public MeanStd ReelSlowed;
        public MeanStd FallControl;
        public MeanStd FallSlowed;
        public int ReactionNotSeen;
        public int Capped;

        // ---- lever 4: forced stagger
        public int StaggerAsked;
        public readonly int[] StaggerAskedBy = new int[2];    // [0] with the engine's priority, [1] ignoring it
        public readonly int[] StaggerAcceptedBy = new int[2];
        public readonly int[] StaggerRefused = new int[(int)ForceRefusal.Count];
        public int StaggerSeen;
        public int StaggerNotSeen;
        public MeanStd StaggerLength;
        public int BlocksFromInside;
        public int HitsFromInside;

        // ---- lever 5: shield drop
        public int KickedWithShield;
        public int KickedWithoutShield;
        public int DropsQueued;
        public int Dropped;
        public int DropNotSeen;
        public int PickedUp;
        public MeanStd PickupSeconds;
        public int StillOnGround;
        public int DropperLeftField;

        // ---- lever 6: our own knockdown blow
        public int OwnBlowAsked;
        public int OwnBlowRefusedCall;
        public readonly int[] OwnBlowRefused = new int[(int)ForceRefusal.Count];
        public int OwnFallSeen;
        public int OwnFallNotSeen;
        public int OwnBlowHitsSeen; // OnAgentHit saw our blow (the static mark)

        public bool Disabled(SpikeLever lever) => _disabled[(int)lever];

        public string? DisabledAt(SpikeLever lever) => _disabledAt[(int)lever];

        /// <summary>A lever disabled for the rest of the mission by an error (true = this call disabled it).</summary>
        public bool Disable(SpikeLever lever, string site)
        {
            if (_disabled[(int)lever]) return false;
            _disabled[(int)lever] = true;
            _disabledAt[(int)lever] = site;
            return true;
        }

        public int DisabledCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _disabled.Length; i++) if (_disabled[i]) n++;
                return n;
            }
        }

        public void AddTick(double ms, int calls)
        {
            TickMs.Add(ms);
            if (ms > MaxTickMs) MaxTickMs = ms;
            if (calls > MostCallsInTick) MostCallsInTick = calls;
        }

        /// <summary>The control kicks' first reaction type (for "what a kick really does" - StrikeKnockBack expected).</summary>
        public void NoteControlType(int type)
        {
            if (type >= 0 && type < ControlFirstTypes.Length) ControlFirstTypes[type]++;
        }

        // ------------------------------------------------------------------ verdicts

        public SpikeVerdict Verdict(SpikeLever lever)
        {
            if (Disabled(lever)) return SpikeVerdict.Fails;
            switch (lever)
            {
                case SpikeLever.KickKnockDown:
                    return SpikeMath.CountVerdict(FallSeen + FallNotSeen, FallSeen);
                case SpikeLever.BlockStun:
                    return SpikeMath.Combine(SpikeMath.LengthVerdict(in RecoilControl, in RecoilTreated), SpikeMath.LengthVerdict(in GuardControl, in GuardTreated));
                case SpikeLever.SlowReaction:
                    return SpikeMath.Combine(SpikeMath.LengthVerdict(in ReelControl, in ReelSlowed), SpikeMath.LengthVerdict(in FallControl, in FallSlowed));
                case SpikeLever.ForcedStagger:
                {
                    int accepted = StaggerAcceptedBy[0] + StaggerAcceptedBy[1];
                    var call = SpikeMath.CountVerdict(StaggerAsked, accepted);
                    if (call != SpikeVerdict.Works) return call;
                    return SpikeMath.CountVerdict(StaggerSeen + StaggerNotSeen, StaggerSeen);
                }
                case SpikeLever.ShieldDrop:
                    return SpikeMath.CountVerdict(Dropped + DropNotSeen, Dropped);
                case SpikeLever.OwnBlow:
                    return SpikeMath.CountVerdict(OwnFallSeen + OwnFallNotSeen, OwnFallSeen);
            }
            return SpikeVerdict.Unseen;
        }

        // ------------------------------------------------------------------ the summary

        /// <summary>The [summary] "impact spike" lines (without the tag): one header, then one line per lever ending with its verdict and what
        /// it means for steps 48-53. Never on all battle = one line.</summary>
        public List<string> SummaryLines(in SpikeRules now)
        {
            var lines = new List<string>();
            if (!EverOn)
            {
                lines.Add("impact spike (step 47): " + (now.Spike ? "ImpactSpike on, but the mod was OFF all battle (ModEnabled)" : "off all battle (ImpactSpike - a test tool, Advanced)")
                          + " - nothing asked, nothing changed");
                return lines;
            }
            var sb = new StringBuilder();
            double total = OnSeconds + OffSeconds;
            sb.Append("impact spike (step 47, ImpactSpike - a test tool): on ").Append(P0(total > 0 ? OnSeconds / total : 1)).Append(" of the battle");
            if (Switches > 0) sb.Append(" (switched ").Append(Switches).Append(" time(s))");
            sb.Append("; levers disabled by an error: ");
            if (DisabledCount == 0) sb.Append("none");
            else
            {
                bool first = true;
                for (int i = 0; i < SpikeMath.LeverCount; i++)
                {
                    if (!_disabled[i]) continue;
                    if (!first) sb.Append(", ");
                    first = false;
                    sb.Append(i + 1).Append(" (").Append(_disabledAt[i]).Append(')');
                }
            }
            sb.Append("; engine calls ").Append(EngineCalls).Append(" (most in a tick ").Append(MostCallsInTick).Append(", waited a tick ").Append(CallsDeferred)
              .Append("); watch pool full ").Append(WatchesFull).Append(", queue full ").Append(PendingFull).Append(", watched men who left the field ").Append(LeftField)
              .Append("; cost ").Append(Ms(TickMs.Count > 0 ? TickMs.Mean : 0)).Append(" ms a tick (most ").Append(Ms(MaxTickMs)).Append(')');
            lines.Add(sb.ToString());

            lines.Add(Lever(SpikeLever.KickKnockDown, "landed kicks " + KicksLanded + " (bashes " + Bashes + ", 0 damage - no reaction at all " + KicksZeroDamage
                + ", on riders " + KicksOnRiders + "); KnockDown answered by the decorator " + KnockDownAsked + " (the rotation asked " + KnockDownPlanned
                + ") → Fall seen within " + F1(SpikeMath.FallAppearSeconds) + " s " + FallSeen + ", not seen " + FallNotSeen
                + "; the kicks NOT asked (the control) " + ControlKicks + " - fell anyway " + ControlFalls + ", their reaction " + TypesText(ControlFirstTypes)
                + (KicksLanded > 0 && KnockDownAsked == 0 && KnockDownPlanned > 0 ? " - the decorator was NEVER asked: another damage model on top of ours? (tell Claude)" : string.Empty)));

            lines.Add(Lever(SpikeLever.BlockStun, "the decorator's calls " + Stun.Calls + " (off the main thread " + Stun.OffMainThread + "); treated (an even blocker) "
                + Stun.Treated + ": defender " + S2(Stun.DefenderBefore) + " → " + S2(Stun.DefenderAfter) + " s asked, attacker " + S2(Stun.AttackerBefore) + " → "
                + S2(Stun.AttackerAfter) + " s (" + Stun.Capped + " at the cap); control " + Stun.Control + ": defender " + S2(Stun.ControlDefender) + " s, attacker "
                + S2(Stun.ControlAttacker) + " s; the game's cap " + S2(CapBefore) + " → " + S2(CapRaisedTo) + " s (restored " + CapRestores + "x); MEASURED on "
                + BlocksSeen + " blocks: the attacker's recoil " + Pair(in RecoilControl, in RecoilTreated) + " (not seen " + RecoilNotSeen + "), the blocker's guard held "
                + Pair(in GuardControl, in GuardTreated) + " (not in his guard " + GuardNotSeen + ")"));

            lines.Add(Lever(SpikeLever.SlowReaction, "SetCurrentActionSpeed " + F1(SpikeMath.ReactionSpeed) + " calls " + SlowCalls + " (again on a _continue " + SlowReapplied
                + "); the reel (no knockdown) " + Pair(in ReelControl, in ReelSlowed) + "; the fall-and-rise " + Pair(in FallControl, in FallSlowed)
                + "; reactions never seen " + ReactionNotSeen + ", still on at " + F0(SpikeMath.MeasureCapSeconds) + " s " + Capped));

            int accepted = StaggerAcceptedBy[0] + StaggerAcceptedBy[1];
            lines.Add(Lever(SpikeLever.ForcedStagger, "asked " + StaggerAsked + " (with the engine's priority " + StaggerAskedBy[0] + " → accepted " + StaggerAcceptedBy[0]
                + ", over it " + StaggerAskedBy[1] + " → accepted " + StaggerAcceptedBy[1] + "), accepted " + accepted + " → seen on channel 0 " + StaggerSeen
                + ", not seen " + StaggerNotSeen + ", lasted " + Len(in StaggerLength) + "; blocks made from inside it " + BlocksFromInside + ", hits taken inside it "
                + HitsFromInside + "; never asked (no engine call): " + Refusals(StaggerRefused)));

            lines.Add(Lever(SpikeLever.ShieldDrop, "kicked men holding a shield " + KickedWithShield + " (without " + KickedWithoutShield + "); drops queued " + DropsQueued
                + " → the shield left his hand within " + F1(SpikeMath.DropSeenSeconds) + " s " + Dropped + ", not " + DropNotSeen + "; picked up again " + PickedUp
                + " (after " + Len(in PickupSeconds) + "), still on the ground at the end " + StillOnGround + ", the man left the field first " + DropperLeftField));

            lines.Add(Lever(SpikeLever.OwnBlow, "our blows registered " + OwnBlowAsked + " (seen by OnAgentHit " + OwnBlowHitsSeen + ", the call refused " + OwnBlowRefusedCall
                + ") → Fall seen within " + F1(SpikeMath.FallAppearSeconds) + " s " + OwnFallSeen + ", not seen " + OwnFallNotSeen + "; never asked (no engine call): " + Refusals(OwnBlowRefused)));
            return lines;
        }

        private string Lever(SpikeLever lever, string body)
        {
            var v = Verdict(lever);
            return "impact spike lever " + SpikeMath.Number(lever) + " - " + SpikeMath.Name(lever) + ": " + body + " → " + SpikeMath.VerdictText(v)
                   + (Disabled(lever) ? " (disabled by an error at " + DisabledAt(lever) + ")" : string.Empty) + " - " + SpikeMath.NextStep(lever, v);
        }

        /// <summary>"control 0.42 s (n 5) → treated 0.85 s (n 4), x2.02".</summary>
        public static string Pair(in MeanStd control, in MeanStd treated)
        {
            string ratio = control.Count > 0 && treated.Count > 0 && control.Mean > 0 ? ", x" + F2(treated.Mean / control.Mean) : string.Empty;
            return "control " + Len(in control) + " → treated " + Len(in treated) + ratio;
        }

        /// <summary>"0.42 s (n 5)" or "none".</summary>
        public static string Len(in MeanStd m) => m.Count == 0 ? "none" : S2(m.Mean) + " s (n " + m.Count.ToString(CultureInfo.InvariantCulture) + ")";

        private static string Refusals(int[] counts)
        {
            var sb = new StringBuilder();
            for (int i = 1; i < counts.Length; i++)
            {
                if (counts[i] == 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(SpikeMath.RefusalName((ForceRefusal)i)).Append(' ').Append(counts[i]);
            }
            return sb.Length == 0 ? "none" : sb.ToString();
        }

        private static string TypesText(int[] types)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < types.Length; i++)
            {
                if (types[i] == 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(ImpactActions.Name(i)).Append(' ').Append(types[i]);
            }
            return sb.Length == 0 ? "none seen" : sb.ToString();
        }

        private static string S2(double v) => double.IsNaN(v) ? "-" : v.ToString("0.00", CultureInfo.InvariantCulture);

        private static string F0(double v) => v.ToString("0", CultureInfo.InvariantCulture);

        private static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        private static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        private static string Ms(double v) => v.ToString("0.000", CultureInfo.InvariantCulture);

        private static string P0(double share) => (share * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
    }
}

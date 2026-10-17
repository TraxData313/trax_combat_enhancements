using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 51 (IMPACT I4, AI_NOTES "Step 51"): the pure rules of the LIVE blocks and the riposte - the three block kinds as the summary
    /// counts them, the speed a forced stagger is played at for the length the model asked (the stagger's own length LEARNED from what the
    /// watch measured, a plumbing guess until then), and the plumbing constants (documented, not settings).
    /// </summary>
    public static class ImpactBlockMath
    {
        /// <summary>The block kinds the summary counts: 0 right-side shield, 1 weapon parry, 2 WRONG-side shield.</summary>
        public const int KindCount = 3;

        /// <summary>The outcomes of a block on the BLOCKER (the riposte is counted apart): knockdown, shield drop, stagger -
        /// <see cref="ImpactOutcome"/>'s first three.</summary>
        public const int OutcomeCount = 3;

        /// <summary>act_stagger_backward's own length at speed 1 before the battle has measured one - the spike's stand-in (0.9 s; RBM plays
        /// its staggers at 0.9-1.0). Plumbing: from the first measured stagger on, the measured length x the speed asked replaces it.</summary>
        public const double StaggerNaturalGuessSeconds = 0.9;

        /// <summary>The slowest / fastest playback we ask of a forced stagger (a 2 s stagger of a 0.9 s animation = 0.45; a fresh man's
        /// 0.6 s = 1.5).</summary>
        public const float MinStaggerSpeed = 0.25f;

        public const float MaxStaggerSpeed = 2.0f;

        /// <summary>A forced stagger must show within this (the spike's window).</summary>
        public const double StaggerAppearSeconds = SpikeMath.AppearSeconds;

        /// <summary>Blocks whose guard is timed at once (the stun asked vs measured) - a quarter of the 64 watches: blocks are many, kicks
        /// and staggers precious.</summary>
        public const int MaxGuardWatches = 16;

        /// <summary>Queued staggers / ripostes / our blows waiting for the tick.</summary>
        public const int MaxPending = 64;

        /// <summary>The block kind of an event (−1 = not a block).</summary>
        public static int KindOf(ImpactEvent e) => e switch
        {
            ImpactEvent.RightBlock => 0,
            ImpactEvent.Parry => 1,
            ImpactEvent.WrongBlock => 2,
            _ => -1,
        };

        public static string KindName(int kind) => kind switch
        {
            0 => "right-side shield blocks",
            1 => "parries",
            _ => "WRONG-side shield blocks",
        };

        /// <summary>The ABSOLUTE speed for a forced stagger of <paramref name="askedSeconds"/> when the animation lasts
        /// <paramref name="naturalSeconds"/> at speed 1: natural ÷ asked, held within <see cref="MinStaggerSpeed"/>-<see cref="MaxStaggerSpeed"/>
        /// (1 for a length not known).</summary>
        public static float StaggerSpeed(double askedSeconds, double naturalSeconds)
        {
            if (!(askedSeconds > 0) || !(naturalSeconds > 0)) return 1f;
            double s = naturalSeconds / askedSeconds;
            if (s < MinStaggerSpeed) return MinStaggerSpeed;
            if (s > MaxStaggerSpeed) return MaxStaggerSpeed;
            return (float)s;
        }

        /// <summary>The ceiling words of a block's outcome for the YOU lines ("you are staggered", "your shield dropped", "you are knocked down").</summary>
        public static string YouOutcome(ImpactOutcome o) => o switch
        {
            ImpactOutcome.Knockdown => "you are knocked down",
            ImpactOutcome.ShieldDrop => "your shield dropped",
            _ => "you are staggered",
        };

        public static string HisOutcome(ImpactOutcome o) => o switch
        {
            ImpactOutcome.Knockdown => "he is knocked down",
            ImpactOutcome.ShieldDrop => "his shield dropped",
            _ => "he is staggered",
        };
    }

    /// <summary>
    /// Step 51: the forced stagger's own length at speed 1, learned in the battle: each measured stagger x the speed it was asked at is one
    /// sample (a struct on the logic - main thread). Until one is measured, <see cref="ImpactBlockMath.StaggerNaturalGuessSeconds"/>.
    /// </summary>
    public struct StaggerNatural
    {
        private MeanStd _samples;

        public int Count => _samples.Count;

        /// <summary>The learned length (s at speed 1), or the guess before the first sample.</summary>
        public double Seconds => _samples.Count > 0 && _samples.Mean > 0 ? _samples.Mean : ImpactBlockMath.StaggerNaturalGuessSeconds;

        /// <summary>A stagger played at <paramref name="speed"/> lasted <paramref name="measured"/> s (a capped / zero one is not a sample).</summary>
        public void Add(double measured, double speed)
        {
            if (!(measured > 0.05) || !(speed > 0) || measured >= SpikeMath.MeasureCapSeconds) return;
            _samples.Add(measured * speed);
        }
    }

    /// <summary>
    /// Step 51: the block stun seam's numbers - the damage decorator asks it on WORKER threads (RESEARCH §R2), so Interlocked only: calls,
    /// off the main thread, blockers we do not track, the stuns lengthened (before → after, the factor) and those left at the game's (a
    /// fresh blocker), the ones held at the cap.
    /// </summary>
    public sealed class BlockStunLiveTally
    {
        private long _calls, _offMain, _untracked, _lengthened, _untouched, _capped, _beforeUs, _afterUs, _factorPpm;

        public long Calls => Interlocked.Read(ref _calls);
        public long OffMainThread => Interlocked.Read(ref _offMain);
        public long Untracked => Interlocked.Read(ref _untracked);
        public long Lengthened => Interlocked.Read(ref _lengthened);
        public long Untouched => Interlocked.Read(ref _untouched);
        public long Capped => Interlocked.Read(ref _capped);

        public double MeanBefore => Mean(ref _beforeUs, Lengthened, 1e6);
        public double MeanAfter => Mean(ref _afterUs, Lengthened, 1e6);
        public double MeanFactor => Mean(ref _factorPpm, Lengthened, 1e6);

        public void Call(bool offMainThread)
        {
            Interlocked.Increment(ref _calls);
            if (offMainThread) Interlocked.Increment(ref _offMain);
        }

        public void NoteUntracked() => Interlocked.Increment(ref _untracked);

        public void NoteUntouched() => Interlocked.Increment(ref _untouched);

        public void NoteLengthened(float before, float after, double factor, bool capped)
        {
            Interlocked.Increment(ref _lengthened);
            if (capped) Interlocked.Increment(ref _capped);
            Interlocked.Add(ref _beforeUs, Us(before));
            Interlocked.Add(ref _afterUs, Us(after));
            Interlocked.Add(ref _factorPpm, double.IsNaN(factor) ? 0 : (long)Math.Round(factor * 1e6));
        }

        private static long Us(float seconds) => float.IsNaN(seconds) || seconds <= 0 ? 0 : (long)Math.Round(seconds * 1e6);

        private static double Mean(ref long sum, long n, double unit) => n > 0 ? Interlocked.Read(ref sum) / unit / n : double.NaN;
    }

    /// <summary>
    /// Step 51 (IMPACT §6): one mission's LIVE blocks and ripostes - per block kind (right shield / parry / wrong side) × the blocker's bar
    /// band, per outcome (knocked down, shield dropped, staggered): decided (the dice said yes) → applied (the lever pulled from the tick) →
    /// seen (the stagger / the Fall showed, the shield left his hand) and why not; the staggers asked vs MEASURED (and the speed, the learned
    /// length); BLOCKS MADE FROM INSIDE A STAGGER (the proof a stagger is a real window - should be ~0); the blockers knocked down; the
    /// ripostes by the ATTACKER's bar; the stun asked vs measured (the guard) and the decorator's seam (calls, off the main thread). Main
    /// thread except <see cref="Stun"/>. The [summary] "impact live block" lines.
    /// </summary>
    public sealed class ImpactBlockLiveStats
    {
        private const int K = ImpactBlockMath.KindCount, B = ImpactMath.BandCount, O = ImpactBlockMath.OutcomeCount;

        /// <summary>The decorator's seam (worker threads).</summary>
        public readonly BlockStunLiveTally Stun = new BlockStunLiveTally();

        /// <summary>[kind, band, outcome (ImpactOutcome 0-2)].</summary>
        public readonly int[,,] Decided = new int[K, B, O];
        public readonly int[,,] Applied = new int[K, B, O];
        public readonly int[,] Seen = new int[K, O];
        public readonly int[,] NotApplied = new int[O + 1, ImpactLiveMath.NotAppliedCount]; // [outcome or 3 = the riposte, why]
        public readonly int[,] LiveBlocks = new int[K, B];

        /// <summary>The riposte by the ATTACKER's bar band.</summary>
        public readonly int[] RiposteDecided = new int[B];
        public readonly int[] RiposteApplied = new int[B];
        public int RiposteSeen;

        public int StaggerAcceptedWithPriority;
        public int StaggerAcceptedOverPriority;
        public int StaggerNotSeen;
        public int RiposteNotSeen;
        public MeanStd StaggerAsked, StaggerMeasured, StaggerSpeed, RiposteAsked, RiposteMeasured;
        public double StaggerAskedMax, StaggerMeasuredMax;

        /// <summary>Blocks made / hits taken by a man inside a stagger of ours that showed (a block's or a riposte's).</summary>
        public int BlocksFromInside;
        public int HitsFromInside;

        public int OwnBlows;
        public int BlockerFallNotSeen;
        public MeanStd BlockGetUpAsked, BlockGetUpMeasured;

        public MeanStd GuardAsked, GuardMeasured, GuardControl;
        public int GuardNotSeen;

        public int RefusedStaggerImmune;
        public int RefusedDown;
        public int RefusedKnockdownImmune;
        public int Halved;
        public int YouDealt;
        public int YouTook;
        public int ShockGiven;
        public int Deferred;
        public int PoolFull;
        public int LeftField;

        /// <summary>The game's cap and ours (the summary's words) - written on the main thread when it is raised.</summary>
        public float CapGame = float.NaN;
        public float CapRaisedTo = float.NaN;
        public int CapRaises;
        public int CapRestores;

        public int LiveDecisions
        {
            get
            {
                int n = 0;
                foreach (var v in LiveBlocks) n += v;
                return n;
            }
        }

        /// <summary>One LIVE block decision (its family or the riposte applies): what the dice said and what the limits did.</summary>
        public void NoteDecision(in ImpactDecision d, int attackerBand, bool blocksApplied, bool riposteApplied)
        {
            int k = ImpactBlockMath.KindOf(d.Event);
            if (k < 0) return;
            int b = ImpactMath.BarBand(d.Bar);
            if (blocksApplied)
            {
                LiveBlocks[k, b]++;
                if (d.Knockdown.Happens) Decided[k, b, (int)ImpactOutcome.Knockdown]++;
                if (d.ShieldDrop.Happens) Decided[k, b, (int)ImpactOutcome.ShieldDrop]++;
                if (d.Stagger.Happens) Decided[k, b, (int)ImpactOutcome.Stagger]++;
                if (d.Stagger.Refusal == ImpactRefusal.StaggerImmune && d.Stagger.Raw > 0) RefusedStaggerImmune++;
                if (d.Knockdown.Refusal == ImpactRefusal.Down) RefusedDown++;
                else if (d.Knockdown.Refusal == ImpactRefusal.KnockdownImmune && d.Knockdown.Raw > 0) RefusedKnockdownImmune++;
                if (Cut(in d.Knockdown) || Cut(in d.ShieldDrop) || Cut(in d.Stagger)) Halved++;
            }
            if (riposteApplied && d.Riposte.Happens) RiposteDecided[Math.Max(0, Math.Min(B - 1, attackerBand))]++;
            bool any = (blocksApplied && (d.Knockdown.Happens || d.ShieldDrop.Happens || d.Stagger.Happens)) || (riposteApplied && d.Riposte.Happens);
            if (any)
            {
                if (d.AttackerIsPlayer) YouDealt++;
                if (d.DefenderIsPlayer) YouTook++;
            }
        }

        private static bool Cut(in ImpactRoll o) => o.Halvings > 0 && o.Raw > 0 && o.Refusal == ImpactRefusal.None;

        public void NoteApplied(ImpactEvent e, ImpactOutcome o, double bar)
        {
            int k = ImpactBlockMath.KindOf(e);
            if (k >= 0 && (int)o < O) Applied[k, ImpactMath.BarBand(bar), (int)o]++;
        }

        public void NoteSeen(ImpactEvent e, ImpactOutcome o)
        {
            int k = ImpactBlockMath.KindOf(e);
            if (k >= 0 && (int)o < O) Seen[k, (int)o]++;
        }

        public void NoteNotApplied(ImpactOutcome o, ImpactNotApplied why) => NotApplied[Math.Min((int)o, O), (int)why]++;

        public void NoteRiposteApplied(int attackerBand) => RiposteApplied[Math.Max(0, Math.Min(B - 1, attackerBand))]++;

        public void NoteStagger(double asked, double measured, float speed)
        {
            StaggerAsked.Add(asked);
            StaggerMeasured.Add(measured);
            StaggerSpeed.Add(speed);
            if (asked > StaggerAskedMax) StaggerAskedMax = asked;
            if (measured > StaggerMeasuredMax) StaggerMeasuredMax = measured;
        }

        public void NoteRiposteLength(double asked, double measured)
        {
            RiposteAsked.Add(asked);
            RiposteMeasured.Add(measured);
        }

        /// <summary>A guard timed after a block: lengthened (factor above 1) = asked vs measured; a fresh blocker's = the control.</summary>
        public void NoteGuard(double factor, double measured)
        {
            if (ImpactLiveMath.NeedsSlow(factor))
            {
                GuardAsked.Add(factor);
                GuardMeasured.Add(measured);
            }
            else GuardControl.Add(measured);
        }

        private int Sum3(int[,,] a, int k, int o)
        {
            int n = 0;
            for (int b = 0; b < B; b++) n += a[k, b, o];
            return n;
        }

        public int DecidedOf(ImpactOutcome o)
        {
            int n = 0;
            for (int k = 0; k < K; k++) n += Sum3(Decided, k, (int)o);
            return n;
        }

        public int AppliedOf(ImpactOutcome o)
        {
            int n = 0;
            for (int k = 0; k < K; k++) n += Sum3(Applied, k, (int)o);
            return n;
        }

        public int SeenOf(ImpactOutcome o)
        {
            int n = 0;
            for (int k = 0; k < K; k++) n += Seen[k, (int)o];
            return n;
        }

        public int RiposteDecidedTotal => Total(RiposteDecided);
        public int RiposteAppliedTotal => Total(RiposteApplied);

        private static int Total(int[] a)
        {
            int n = 0;
            foreach (var v in a) n += v;
            return n;
        }

        /// <summary>
        /// The [summary] "impact live block" lines (step 51): one line when no live block happened, else one line per block kind (decided →
        /// applied → seen per outcome by the blocker's bar band, not applied by reason), the staggers (asked vs measured, from inside), the
        /// blockers knocked down, the riposte by the attacker's bar, the stun (the seam and the guard), the limits.
        /// </summary>
        public IReadOnlyList<string> SummaryLines(bool liveNow, string mode, string levers)
        {
            var lines = new List<string>();
            long seam = Stun.Calls;
            if (LiveDecisions == 0 && RiposteDecidedTotal == 0 && seam == 0 && StaggerMeasured.Count == 0)
            {
                lines.Add("impact live blocks (step 51 - blocks and the riposte): no live block this mission (" + mode + ")");
                return lines;
            }
            for (int k = 0; k < K; k++)
            {
                int live = 0;
                for (int b = 0; b < B; b++) live += LiveBlocks[k, b];
                var s = new StringBuilder("impact live " + ImpactBlockMath.KindName(k) + " by the blocker's bar " + Bands() + " (step 51; " + mode + "): ");
                s.Append(I(live)).Append(" live (").Append(Row(LiveBlocks, k)).Append(')');
                Outcome(s, k, ImpactOutcome.Stagger, "staggered", "seen");
                if (k == 2 || Sum3(Decided, k, (int)ImpactOutcome.ShieldDrop) > 0) Outcome(s, k, ImpactOutcome.ShieldDrop, "shield dropped", "left the hand"); // a right-side drop only if its ceiling was raised
                Outcome(s, k, ImpactOutcome.Knockdown, "knocked down (our own blow)", "Fall seen");
                lines.Add(s.ToString());
            }
            lines.Add("impact live block staggers: asked " + Secs(in StaggerAsked, StaggerAskedMax) + " → measured " + Secs(in StaggerMeasured, StaggerMeasuredMax)
                      + " at speed " + (StaggerSpeed.Count > 0 ? "x" + F2(StaggerSpeed.Mean) : "none") + "; the engine took them with its priority " + I(StaggerAcceptedWithPriority)
                      + ", over it " + I(StaggerAcceptedOverPriority) + ", not seen " + I(StaggerNotSeen) + NotText(ImpactOutcome.Stagger)
                      + "; BLOCKS MADE FROM INSIDE A STAGGER " + I(BlocksFromInside) + " (should be ~0 - the proof a stagger is a real window), hits taken inside one " + I(HitsFromInside));
            lines.Add("impact live blockers knocked down: our own blows registered " + I(OwnBlows) + " → Fall seen " + I(SeenOf(ImpactOutcome.Knockdown)) + ", not seen " + I(BlockerFallNotSeen)
                      + NotText(ImpactOutcome.Knockdown) + "; the get-up asked " + Times(in BlockGetUpAsked) + " → the fall-and-rise measured " + Secs(in BlockGetUpMeasured, double.NaN)
                      + "; shields dropped on a wrong-side block: decided " + I(DecidedOf(ImpactOutcome.ShieldDrop)) + ", queued " + I(AppliedOf(ImpactOutcome.ShieldDrop))
                      + ", left the hand " + I(SeenOf(ImpactOutcome.ShieldDrop)) + NotText(ImpactOutcome.ShieldDrop));
            lines.Add("impact live riposte (a fresh blocker staggers the ATTACKER) by the attacker's bar " + Bands() + ": decided " + Row(RiposteDecided) + " = " + I(RiposteDecidedTotal)
                      + ", applied " + Row(RiposteApplied) + " = " + I(RiposteAppliedTotal) + ", seen " + I(RiposteSeen) + ", not seen " + I(RiposteNotSeen) + NotText(ImpactOutcome.Riposte)
                      + "; his stagger asked " + Secs(in RiposteAsked, double.NaN) + " → measured " + Secs(in RiposteMeasured, double.NaN));
            lines.Add("impact live block stun: the decorator's calls " + I(seam) + " (off the main thread " + I(Stun.OffMainThread) + "), lengthened " + I(Stun.Lengthened)
                      + (Stun.Lengthened > 0 ? " (x" + F2(Stun.MeanFactor) + " mean: " + F2(Stun.MeanBefore) + " → " + F2(Stun.MeanAfter) + " s, at the cap " + I(Stun.Capped) + ")" : string.Empty)
                      + ", a fresh blocker's left as the game's " + I(Stun.Untouched) + ", blockers not tracked " + I(Stun.Untracked)
                      + "; the guard measured: asked " + Times(in GuardAsked) + " → " + Secs(in GuardMeasured, double.NaN) + " vs fresh blockers " + Secs(in GuardControl, double.NaN)
                      + Ratio(in GuardMeasured, in GuardControl) + ", no guard seen " + I(GuardNotSeen) + "; the game's cap "
                      + (float.IsNaN(CapGame) ? "never raised" : F2(CapGame) + " → " + F2(CapRaisedTo) + " s (raised " + I(CapRaises) + "x, restored " + I(CapRestores) + "x)"));
            lines.Add("impact live block limits: stagger refused - reeling or within the immunity " + I(RefusedStaggerImmune) + "; knockdown refused - on the ground " + I(RefusedDown)
                      + ", within the immunity after getting up " + I(RefusedKnockdownImmune) + "; halved by a recent outcome " + I(Halved) + "; YOU: dealt " + I(YouDealt) + ", took " + I(YouTook)
                      + "; outcomes counted as courage's SHOCK " + I(ShockGiven) + " (once each); levers disabled " + levers + "; queued calls deferred " + I(Deferred) + ", pool full " + I(PoolFull)
                      + ", left the field " + I(LeftField) + (liveNow ? string.Empty : "; NOT live at the end"));
            return lines;
        }

        private void Outcome(StringBuilder s, int k, ImpactOutcome o, string name, string seenName)
        {
            int dt = Sum3(Decided, k, (int)o), at = Sum3(Applied, k, (int)o);
            s.Append("; ").Append(name).Append(" decided ").Append(Row3(Decided, k, (int)o)).Append(" = ").Append(I(dt))
                .Append(", applied ").Append(Row3(Applied, k, (int)o)).Append(" = ").Append(I(at)).Append(", ").Append(seenName).Append(' ').Append(I(Seen[k, (int)o]));
        }

        private string NotText(ImpactOutcome o)
        {
            int row = Math.Min((int)o, O);
            var not = new StringBuilder();
            for (int n = 0; n < ImpactLiveMath.NotAppliedCount; n++)
            {
                if (NotApplied[row, n] == 0) continue;
                if (not.Length > 0) not.Append(", ");
                not.Append(ImpactLiveMath.NotAppliedName((ImpactNotApplied)n)).Append(' ').Append(I(NotApplied[row, n]));
            }
            return not.Length > 0 ? " (not applied: " + not + ")" : string.Empty;
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

        private static string Row(int[,] a, int k)
        {
            var s = new StringBuilder();
            for (int b = 0; b < B; b++)
            {
                if (b > 0) s.Append(" / ");
                s.Append(I(a[k, b]));
            }
            return s.ToString();
        }

        private static string Row3(int[,,] a, int k, int o)
        {
            var s = new StringBuilder();
            for (int b = 0; b < B; b++)
            {
                if (b > 0) s.Append(" / ");
                s.Append(I(a[k, b, o]));
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

        private static string Times(in MeanStd m) => m.Count == 0 ? "none" : "x" + F2(m.Mean) + " (n " + I(m.Count) + ")";

        private static string Secs(in MeanStd m, double max) => m.Count == 0 ? "none"
            : F2(m.Mean) + " s" + (double.IsNaN(max) ? string.Empty : " (max " + F2(max) + " s)") + " (n " + I(m.Count) + ")";

        private static string Ratio(in MeanStd treated, in MeanStd control) => treated.Count > 0 && control.Count > 0 && control.Mean > 0
            ? " = x" + F2(treated.Mean / control.Mean) + " measured"
            : string.Empty;

        private static string I(long v) => v.ToString(CultureInfo.InvariantCulture);

        private static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    }
}

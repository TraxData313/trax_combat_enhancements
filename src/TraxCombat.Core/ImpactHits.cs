using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 52 (IMPACT I5, AI_NOTES "Step 52"): the pure rules of the LIVE clean hits - the weapon classes the summary counts, the tired
    /// man's shrug-off threshold, the factor a clean hit's stagger asks of the engine's own flinch (its natural length LEARNED from the
    /// measured ones, a plumbing guess until then), and the plumbing constants (documented, not settings).
    /// </summary>
    public static class ImpactHitMath
    {
        /// <summary>The weapon classes of the clean hits' summary: 0 cut, 1 pierce, 2 blunt (one-handed or unknown), 3 two-handed (any type).</summary>
        public const int ClassCount = 4;

        /// <summary>The outcomes a clean hit can have on the hit man: 0 knockdown, 1 stagger (the flinch lengthened).</summary>
        public const int OutcomeCount = 2;

        /// <summary>The engine's flinch at speed 1 before the battle has measured one - the kick reel's estimate (step 48's
        /// <see cref="ImpactMath.VanillaReelSeconds"/>, 0.45 s). Plumbing: from the first measured flinch on, measured × the speed replaces it.</summary>
        public const double FlinchNaturalGuessSeconds = ImpactMath.VanillaReelSeconds;

        /// <summary>Untouched flinches watched at once (the controls) - blows are many, the watch pool is shared with kicks and blocks.</summary>
        public const int MaxControlWatches = 8;

        /// <summary>The flinch must show within this after the blow (the kicks' reel window).</summary>
        public const double FlinchAppearSeconds = SpikeMath.AppearSeconds;

        /// <summary>The class of a clean hit's weapon (<see cref="ClassCount"/>).</summary>
        public static int ClassOf(in ImpactFacts f)
        {
            if (f.TwoHanded) return 3;
            return f.DamageType switch
            {
                ImpactDamageType.Pierce => 1,
                ImpactDamageType.Blunt => 2,
                _ => 0,
            };
        }

        public static string ClassName(int c) => c switch
        {
            0 => "cut",
            1 => "pierce",
            2 => "blunt",
            _ => "two-handed",
        };

        /// <summary>His tiredness as the shrug-off sees it: T = 1 − f, × <c>ImpactOnPlayerPercent</c> for you (0-1).</summary>
        public static double ShrugTired(double f, bool isPlayer, double onPlayer)
        {
            double t = ImpactMath.Tired(f);
            if (isPlayer) t *= Math.Max(0, Math.Min(1, double.IsNaN(onPlayer) ? 1 : onPlayer));
            return t;
        }

        /// <summary>
        /// <c>ImpactTiredNeverShrugsOff</c> (IMPACT §3.5's clean-hit row, "always"): the game's shrug-off threshold × f = × (1 − T) - a man in
        /// the green keeps the game's, an empty man shrugs off nothing (a 0-damage blow plays no reaction anyway).
        /// </summary>
        public static double ShrugOffThreshold(double gameThreshold, double tired)
        {
            if (double.IsNaN(gameThreshold)) return gameThreshold;
            double t = double.IsNaN(tired) ? 0 : Math.Max(0, Math.Min(1, tired));
            return gameThreshold * (1 - t);
        }

        /// <summary>Does he still shrug the blow off (the game said yes): its damage at or under the tired threshold.</summary>
        public static bool StillShrugsOff(double damage, double gameThreshold, double tired) => damage <= ShrugOffThreshold(gameThreshold, tired);

        /// <summary>The factor a clean hit's stagger asks of the engine's own flinch: the decided length ÷ the flinch's natural length, never
        /// below 1 (the stagger never SHORTENS a flinch; 1 for a length not known).</summary>
        public static double FlinchFactor(double askedSeconds, double naturalSeconds)
        {
            if (!(askedSeconds > 0) || !(naturalSeconds > 0)) return 1;
            return Math.Max(1, askedSeconds / naturalSeconds);
        }

        /// <summary>The YOU line's words for a clean hit's outcome.</summary>
        public static string YouOutcome(int outcome) => outcome == 0 ? "you are knocked down" : "you are staggered";

        public static string HisOutcome(int outcome) => outcome == 0 ? "he is knocked down" : "he is staggered";
    }

    /// <summary>Step 52: the engine's own flinch length at speed 1, learned in the battle (each measured flinch × the speed it played at; the
    /// untouched controls at 1). Until one is measured, <see cref="ImpactHitMath.FlinchNaturalGuessSeconds"/>. Main thread.</summary>
    public struct FlinchNatural
    {
        private MeanStd _samples;

        public int Count => _samples.Count;

        public double Seconds => _samples.Count > 0 && _samples.Mean > 0 ? _samples.Mean : ImpactHitMath.FlinchNaturalGuessSeconds;

        /// <summary>A flinch played at <paramref name="speed"/> lasted <paramref name="measured"/> s (a capped or near-zero one is no sample).</summary>
        public void Add(double measured, double speed)
        {
            if (!(measured > 0.05) || !(speed > 0) || measured >= SpikeMath.MeasureCapSeconds) return;
            _samples.Add(measured * speed);
        }
    }

    /// <summary>
    /// Step 52 (IMPACT §6, §7's proof "knockdowns by weapon class and bar band"): one mission's LIVE clean hits - per weapon class × the hit
    /// man's bar band: the live hits, the knockdowns and staggers decided (the dice said yes) → applied (the KnockDown answered / the flinch
    /// watched to be slowed) → seen (the Fall / the flinch showed); knockdowns on REELING men vs the rest; the tired men's shrug-offs refused;
    /// the flinch lengths asked vs MEASURED (and the untouched controls); the get-ups. And MOMENTUM for EVERY decided event (kicks, bashes,
    /// blocks, clean hits - live or shadow): the blows by the attacker's speed band (standing / walking / running / sprinting) with their
    /// outcome rates, all men and the men below half a bar - the proof that a charge throws tired men around. Main thread only.
    /// </summary>
    public sealed class ImpactHitLiveStats
    {
        private const int C = ImpactHitMath.ClassCount, B = ImpactMath.BandCount, O = ImpactHitMath.OutcomeCount, V = ImpactMath.SpeedBandCount;

        /// <summary>[class, band].</summary>
        public readonly int[,] LiveHits = new int[C, B];

        /// <summary>[class, band, outcome (0 knockdown, 1 stagger)].</summary>
        public readonly int[,,] Decided = new int[C, B, O];
        public readonly int[,,] Applied = new int[C, B, O];
        public readonly int[] Seen = new int[O];
        public readonly int[,] NotApplied = new int[O, ImpactLiveMath.NotAppliedCount];

        /// <summary>Live hits / knockdowns decided on a man REELING from an impact stagger [1] and on the rest [0].</summary>
        public readonly int[] HitsByReeling = new int[2];
        public readonly int[] KnockdownsByReeling = new int[2];

        /// <summary>The shrug-off seam by the hit man's band: the game said "shrugged off" on a tracked man on foot while the rule applies →
        /// refused (he flinches) / kept (light enough even for his tired threshold).</summary>
        public readonly int[] ShrugAsked = new int[B];
        public readonly int[] ShrugRefused = new int[B];
        public int KnockdownOnShrugOff;

        public MeanStd FlinchAsked, FlinchMeasured, FlinchFactor, FlinchControl, GetUpAsked, GetUpMeasured, GetUpControl;
        public double FlinchMeasuredMax;
        public int FlinchNotSeen, FellInstead, FallNotSeen, ControlNotSeen, Capped, Controls;

        public int RefusedDown, RefusedKnockdownImmune, RefusedStaggerImmune, Halved;
        public int YouDealt, YouTook, ShockGiven, WatchesFull, LeftField;

        /// <summary>MOMENTUM - every decided event by the attacker's speed band [speed band]: blows, an outcome on the defender (knocked down,
        /// shield dropped or staggered), knocked down, the momentum factor's sum and the speed's; the same for defenders below half a bar.</summary>
        public readonly int[] Blows = new int[V];
        public readonly int[] AnyOutcome = new int[V];
        public readonly int[] Down = new int[V];
        public readonly double[] FactorSum = new double[V];
        public readonly double[] SpeedSum = new double[V];
        public readonly int[] TiredBlows = new int[V];
        public readonly int[] TiredAny = new int[V];
        public readonly int[] TiredDown = new int[V];
        public int RiderBlows;

        public int LiveDecisions
        {
            get
            {
                int n = 0;
                foreach (var v in LiveHits) n += v;
                return n;
            }
        }

        /// <summary>One LIVE clean-hit decision on a man on foot (clean hits apply): what the dice said, the limits, by class and band.</summary>
        public void NoteDecision(in ImpactDecision d, int weaponClass)
        {
            int c = Math.Max(0, Math.Min(C - 1, weaponClass));
            int b = ImpactMath.BarBand(d.Bar);
            LiveHits[c, b]++;
            int r = d.Reeling ? 1 : 0;
            HitsByReeling[r]++;
            if (d.Knockdown.Happens)
            {
                Decided[c, b, 0]++;
                KnockdownsByReeling[r]++;
            }
            if (d.Stagger.Happens) Decided[c, b, 1]++;
            if (d.Knockdown.Refusal == ImpactRefusal.Down) RefusedDown++;
            else if (d.Knockdown.Refusal == ImpactRefusal.KnockdownImmune && d.Knockdown.Raw > 0) RefusedKnockdownImmune++;
            if (d.Stagger.Refusal == ImpactRefusal.StaggerImmune && d.Stagger.Raw > 0) RefusedStaggerImmune++;
            if (Cut(in d.Knockdown) || Cut(in d.Stagger)) Halved++;
            if (d.Knockdown.Happens || d.Stagger.Happens)
            {
                if (d.AttackerIsPlayer) YouDealt++;
                if (d.DefenderIsPlayer) YouTook++;
            }
        }

        private static bool Cut(in ImpactRoll o) => o.Halvings > 0 && o.Raw > 0 && o.Refusal == ImpactRefusal.None;

        public void NoteApplied(int weaponClass, double bar, int outcome) => Applied[Math.Max(0, Math.Min(C - 1, weaponClass)), ImpactMath.BarBand(bar), outcome]++;

        public void NoteSeen(int outcome) => Seen[outcome]++;

        public void NoteNotApplied(int outcome, ImpactNotApplied why) => NotApplied[outcome, (int)why]++;

        /// <summary>The shrug-off seam's answer for a tired man the game let shrug a blow off.</summary>
        public void NoteShrug(double bar, bool refused)
        {
            int b = ImpactMath.BarBand(bar);
            ShrugAsked[b]++;
            if (refused) ShrugRefused[b]++;
        }

        /// <summary>A clean hit's stagger measured: the asked length (s) and factor vs what the engine played (slowed = its speed was set).</summary>
        public void NoteFlinch(double askedSeconds, double factor, double measured, bool slowed)
        {
            if (slowed)
            {
                FlinchAsked.Add(askedSeconds);
                FlinchFactor.Add(factor);
                FlinchMeasured.Add(measured);
                if (measured > FlinchMeasuredMax) FlinchMeasuredMax = measured;
            }
            else FlinchControl.Add(measured);
        }

        public void NoteGetUp(double asked, double measured, bool slowed)
        {
            if (slowed)
            {
                GetUpAsked.Add(asked);
                GetUpMeasured.Add(measured);
            }
            else GetUpControl.Add(measured);
        }

        /// <summary>MOMENTUM: one decided event (any family, live or shadow) by the attacker's speed band. A mounted attacker is counted apart
        /// (his momentum is step 53's).</summary>
        public void NoteMomentum(in ImpactDecision d, int speedBand, bool attackerMounted)
        {
            if (attackerMounted)
            {
                RiderBlows++;
                return;
            }
            int v = Math.Max(0, Math.Min(V - 1, speedBand));
            bool any = d.Knockdown.Happens || d.ShieldDrop.Happens || d.Stagger.Happens;
            Blows[v]++;
            FactorSum[v] += double.IsNaN(d.Momentum) ? 1 : d.Momentum;
            SpeedSum[v] += double.IsNaN(d.AttackerSpeed) ? 0 : Math.Max(0, d.AttackerSpeed);
            if (any) AnyOutcome[v]++;
            if (d.Knockdown.Happens) Down[v]++;
            if (!double.IsNaN(d.Bar) && d.Bar < 0.5)
            {
                TiredBlows[v]++;
                if (any) TiredAny[v]++;
                if (d.Knockdown.Happens) TiredDown[v]++;
            }
        }

        public int DecidedOf(int outcome)
        {
            int n = 0;
            for (int c = 0; c < C; c++)
                for (int b = 0; b < B; b++) n += Decided[c, b, outcome];
            return n;
        }

        public int AppliedOf(int outcome)
        {
            int n = 0;
            for (int c = 0; c < C; c++)
                for (int b = 0; b < B; b++) n += Applied[c, b, outcome];
            return n;
        }

        public int ShrugRefusedTotal => Total(ShrugRefused);
        public int ShrugAskedTotal => Total(ShrugAsked);
        public int MomentumBlows => Total(Blows);

        private static int Total(int[] a)
        {
            int n = 0;
            foreach (var v in a) n += v;
            return n;
        }

        /// <summary>
        /// The [summary] lines (step 52): the momentum line always (it counts every family's decisions); the clean hits' lines - one per
        /// weapon class (decided → applied → seen per outcome by the hit man's bar band), knockdowns on reeling men, the lengths, the
        /// shrug-offs, the limits - or one line when no live clean hit happened.
        /// </summary>
        public IReadOnlyList<string> SummaryLines(bool liveNow, string mode, string levers)
        {
            var lines = new List<string>();
            lines.Add(MomentumLine());
            if (LiveDecisions == 0 && ShrugAskedTotal == 0 && FlinchMeasured.Count == 0 && FlinchControl.Count == 0)
            {
                lines.Add("impact live clean hits (step 52): no live clean hit this mission (" + mode + ")");
                return lines;
            }
            for (int c = 0; c < C; c++)
            {
                int live = 0;
                for (int b = 0; b < B; b++) live += LiveHits[c, b];
                if (live == 0 && c != 0) continue;
                var s = new StringBuilder("impact live clean hits, " + ImpactHitMath.ClassName(c) + ", by the hit man's bar " + Bands() + " (step 52; " + mode + "): ");
                s.Append(I(live)).Append(" live (").Append(Row(LiveHits, c)).Append(')');
                Outcome(s, c, 0, "knocked down", "Fall seen");
                Outcome(s, c, 1, "staggered (his flinch lengthened)", "seen");
                lines.Add(s.ToString());
            }
            lines.Add("impact live clean-hit knockdowns: on men REELING from an impact stagger " + I(KnockdownsByReeling[1]) + " of " + I(HitsByReeling[1]) + " hits (" + Pct(KnockdownsByReeling[1], HitsByReeling[1])
                      + "), on the rest " + I(KnockdownsByReeling[0]) + " of " + I(HitsByReeling[0]) + " (" + Pct(KnockdownsByReeling[0], HitsByReeling[0]) + ")" + NotText(0)
                      + "; Fall seen " + I(Seen[0]) + ", not seen " + I(FallNotSeen) + "; the get-up asked " + Times(in GetUpAsked) + " → the fall-and-rise measured " + Secs(in GetUpMeasured, double.NaN)
                      + (GetUpControl.Count > 0 ? " vs untouched " + Secs(in GetUpControl, double.NaN) : string.Empty) + "; knockdowns answered on a blow the game had shrugged off " + I(KnockdownOnShrugOff));
            lines.Add("impact live clean-hit staggers (the engine's own flinch slowed): asked " + Secs(in FlinchAsked, double.NaN) + " (x" + (FlinchFactor.Count > 0 ? F2(FlinchFactor.Mean) : "-")
                      + " the flinch) → measured " + Secs(in FlinchMeasured, FlinchMeasuredMax) + " vs untouched flinches " + Secs(in FlinchControl, double.NaN) + Ratio(in FlinchMeasured, in FlinchControl)
                      + "; no flinch seen " + I(FlinchNotSeen) + ", fell instead " + I(FellInstead) + ", controls " + I(Controls) + " (not seen " + I(ControlNotSeen) + "), at the 8 s cap " + I(Capped) + NotText(1)
                      + " - the measured ends are his state's (the reeling bonus and the 2 s immunity from them)");
            lines.Add("impact live shrug-offs (ImpactTiredNeverShrugsOff - the game's threshold x f): the game let a tired man shrug a clean hit off " + I(ShrugAskedTotal) + " times by his bar " + Bands() + " "
                      + Row(ShrugAsked) + " → REFUSED (he flinched) " + Row(ShrugRefused) + " = " + I(ShrugRefusedTotal) + " (" + Pct(ShrugRefusedTotal, ShrugAskedTotal) + "), kept (light enough even tired) "
                      + I(ShrugAskedTotal - ShrugRefusedTotal));
            lines.Add("impact live clean-hit limits: knockdown refused - on the ground " + I(RefusedDown) + ", within the immunity after getting up " + I(RefusedKnockdownImmune)
                      + "; stagger refused - reeling or within the immunity " + I(RefusedStaggerImmune) + "; halved by a recent outcome " + I(Halved) + "; YOU: dealt " + I(YouDealt) + ", took " + I(YouTook)
                      + "; outcomes counted as courage's SHOCK " + I(ShockGiven) + "; levers disabled " + levers + "; watch pool full " + I(WatchesFull) + ", left the field " + I(LeftField)
                      + (liveNow ? string.Empty : "; NOT live at the end"));
            return lines;
        }

        /// <summary>The MOMENTUM line: every decided blow by the attacker's speed into it.</summary>
        public string MomentumLine()
        {
            int all = MomentumBlows;
            if (all == 0 && RiderBlows == 0) return "impact momentum (step 52 - the attacker's speed into the blow): no blow decided this mission";
            var s = new StringBuilder("impact momentum (step 52 - every decided kick, bash, block and clean hit by the attacker's speed into the blow; an outcome = knocked down, shield dropped or staggered): ");
            for (int v = 0; v < V; v++)
            {
                if (v > 0) s.Append(" | ");
                s.Append(ImpactMath.SpeedBandName(v)).Append(' ').Append(I(Blows[v]));
                if (Blows[v] == 0) continue;
                s.Append(" (").Append(F1(SpeedSum[v] / Blows[v])).Append(" m/s, impact x").Append(F2(FactorSum[v] / Blows[v])).Append("): an outcome ").Append(Pct(AnyOutcome[v], Blows[v]))
                    .Append(", down ").Append(Pct(Down[v], Blows[v]));
                if (TiredBlows[v] > 0)
                    s.Append("; on men below half a bar ").Append(I(TiredBlows[v])).Append(": an outcome ").Append(Pct(TiredAny[v], TiredBlows[v])).Append(", down ").Append(Pct(TiredDown[v], TiredBlows[v]));
            }
            s.Append(" | riders' blows not counted (step 53) ").Append(I(RiderBlows));
            return s.ToString();
        }

        private void Outcome(StringBuilder s, int c, int o, string name, string seenName)
        {
            int dt = 0, at = 0;
            for (int b = 0; b < B; b++)
            {
                dt += Decided[c, b, o];
                at += Applied[c, b, o];
            }
            s.Append("; ").Append(name).Append(" decided ").Append(Row3(Decided, c, o)).Append(" = ").Append(I(dt))
                .Append(", applied ").Append(Row3(Applied, c, o)).Append(" = ").Append(I(at));
            if (c == 0) s.Append(", ").Append(seenName).Append(" (all classes) ").Append(I(Seen[o]));
        }

        private string NotText(int o)
        {
            var not = new StringBuilder();
            for (int n = 0; n < ImpactLiveMath.NotAppliedCount; n++)
            {
                if (NotApplied[o, n] == 0) continue;
                if (not.Length > 0) not.Append(", ");
                not.Append(ImpactLiveMath.NotAppliedName((ImpactNotApplied)n)).Append(' ').Append(I(NotApplied[o, n]));
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

        private static string Row(int[,] a, int c)
        {
            var s = new StringBuilder();
            for (int b = 0; b < B; b++)
            {
                if (b > 0) s.Append(" / ");
                s.Append(I(a[c, b]));
            }
            return s.ToString();
        }

        private static string Row3(int[,,] a, int c, int o)
        {
            var s = new StringBuilder();
            for (int b = 0; b < B; b++)
            {
                if (b > 0) s.Append(" / ");
                s.Append(I(a[c, b, o]));
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

        private static string Pct(int n, int of) => of > 0 ? (100.0 * n / of).ToString("0", CultureInfo.InvariantCulture) + "%" : "-";

        private static string Times(in MeanStd m) => m.Count == 0 ? "none" : "x" + F2(m.Mean) + " (n " + I(m.Count) + ")";

        private static string Secs(in MeanStd m, double max) => m.Count == 0 ? "none"
            : F2(m.Mean) + " s" + (double.IsNaN(max) ? string.Empty : " (max " + F2(max) + " s)") + " (n " + I(m.Count) + ")";

        private static string Ratio(in MeanStd treated, in MeanStd control) => treated.Count > 0 && control.Count > 0 && control.Mean > 0
            ? " = x" + F2(treated.Mean / control.Mean) + " measured"
            : string.Empty;

        private static string I(long v) => v.ToString(CultureInfo.InvariantCulture);

        private static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        private static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    }
}

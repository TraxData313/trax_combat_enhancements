using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>Step 48: why a melee collision got no decision (the summary's "not decided").</summary>
    public enum ImpactSkip : byte
    {
        /// <summary>The model is off (the master switch, Athletics, ImpactEnabled) - counted, not decided.</summary>
        Off,

        /// <summary>Its family's switch is off.</summary>
        FamilyOff,

        /// <summary>The man who took it is not one we track (a horse, a man who left) - or the attacker is not.</summary>
        NotTracked,

        /// <summary>The engine read of the weapon / guard failed (one [error], counted after).</summary>
        ReadFailed,

        /// <summary>Step 62: a landed blow that KILLS him - nothing to decide (he plays his death, not our fall).</summary>
        Fatal,

        /// <summary>Step 63: a FRIEND's blow (the two men are not enemies) - the engine cancels its damage and plays nothing.</summary>
        NotEnemies,
    }

    /// <summary>
    /// Step 48 (IMPACT I2 §6): one mission's impact decisions - by event × the DEFENDER's bar band (100-75 / 75-50 / 50-25 / 25-0 %):
    /// decided, mean x, and per outcome rolled / mean chance / would apply (shadow) or applied; the refusals by reason, the halvings; the
    /// riposte by the ATTACKER's band; the lengths asked; the cost. Main thread only. The [summary] "impact" lines.
    /// </summary>
    public sealed class ImpactStats
    {
        private const int E = ImpactMath.EventCount, B = ImpactMath.BandCount, O = 3; // outcomes on the defender: knockdown, shield, stagger

        public readonly int[,] Decided = new int[E, B];
        public readonly double[,] SumX = new double[E, B];
        public readonly int[,,] InRow = new int[E, B, O];
        public readonly int[,,] Rolled = new int[E, B, O];
        public readonly double[,,] SumChance = new double[E, B, O];
        public readonly int[,,] Happened = new int[E, B, O];
        public readonly int[] RiposteAsked = new int[B];
        public readonly int[] RiposteRolled = new int[B];
        public readonly double[] RiposteChance = new double[B];
        public readonly int[] RiposteHappened = new int[B];
        public readonly int[] Refused = new int[ImpactMath.RefusalCount];
        public readonly int[] Skipped = new int[6];
        public readonly int[] SkippedByEvent = new int[E];
        public int Halved;
        public long Halvings;
        public int Dismounts;
        /// <summary>Decisions made in shadow mode (ImpactShadowMode or the spike) / in live mode; of the live ones, those whose family's
        /// effect is built and so applied (none in step 48).</summary>
        public int ShadowDecisions;
        public int LiveDecisions;
        public int Applied;
        /// <summary>Step 50: the applied decisions by event (the event lines say "did" for an event whose family applied, "would" else).</summary>
        public readonly int[] AppliedByEvent = new int[E];
        public int FromDecorator;
        public int OnPlayer;
        public int ByPlayer;
        public MeanStd StaggerSeconds;
        public MeanStd ReelFactor;
        public MeanStd GetUpFactor;
        public MeanStd StunFactor;
        public MeanStd RiposteSeconds;
        public double CostMs;
        public double MaxCostMs;
        public double OnSeconds;
        public double OffSeconds;
        public int Switches;
        public bool EverOn;

        public int Decisions
        {
            get
            {
                int n = 0;
                foreach (var v in Decided) n += v;
                return n;
            }
        }

        public int DecidedOf(ImpactEvent e)
        {
            int n = 0;
            for (int b = 0; b < B; b++) n += Decided[(int)e, b];
            return n;
        }

        public int HappenedOf(ImpactEvent e, ImpactOutcome o)
        {
            if (o == ImpactOutcome.Riposte) return e == ImpactEvent.RightBlock || e == ImpactEvent.Parry ? RiposteTotal : 0;
            int n = 0;
            for (int b = 0; b < B; b++) n += Happened[(int)e, b, (int)o];
            return n;
        }

        public int RiposteTotal
        {
            get
            {
                int n = 0;
                foreach (var v in RiposteHappened) n += v;
                return n;
            }
        }

        /// <summary>One decision: made in shadow mode or live, and whether it was applied (live and its family's effect built).</summary>
        public void Note(in ImpactDecision d, bool shadow, bool applied, double costMs, int attackerBand)
        {
            int e = (int)d.Event, b = ImpactMath.BarBand(d.Bar);
            Decided[e, b]++;
            SumX[e, b] += d.X;
            NoteRoll(e, b, (int)ImpactOutcome.Knockdown, in d.Knockdown);
            NoteRoll(e, b, (int)ImpactOutcome.ShieldDrop, in d.ShieldDrop);
            NoteRoll(e, b, (int)ImpactOutcome.Stagger, in d.Stagger);
            if (d.Riposte.InRow)
            {
                int ab = Math.Max(0, Math.Min(B - 1, attackerBand));
                RiposteAsked[ab]++;
                if (d.Riposte.Refusal != ImpactRefusal.None) Refused[(int)d.Riposte.Refusal]++;
                else if (d.Riposte.Rolled)
                {
                    RiposteRolled[ab]++;
                    RiposteChance[ab] += d.Riposte.Chance;
                }
                if (d.Riposte.Happens) RiposteHappened[ab]++;
                if (d.Riposte.Halvings > 0 && d.Riposte.Refusal == ImpactRefusal.None)
                {
                    Halved++;
                    Halvings += d.Riposte.Halvings;
                }
            }
            int h = Math.Max(d.Knockdown.Halvings, Math.Max(d.ShieldDrop.Halvings, d.Stagger.Halvings));
            if (h > 0 && (Live(in d.Knockdown) || Live(in d.ShieldDrop) || Live(in d.Stagger)))
            {
                Halved++;
                Halvings += h;
            }
            if (d.Dismount) Dismounts++;
            if (!double.IsNaN(d.StaggerSeconds)) StaggerSeconds.Add(d.StaggerSeconds);
            if (!double.IsNaN(d.ReelFactor)) ReelFactor.Add(d.ReelFactor);
            if (!double.IsNaN(d.GetUpFactor)) GetUpFactor.Add(d.GetUpFactor);
            if (!double.IsNaN(d.StunFactor)) StunFactor.Add(d.StunFactor);
            if (!double.IsNaN(d.RiposteStunSeconds)) RiposteSeconds.Add(d.RiposteStunSeconds);
            if (shadow) ShadowDecisions++;
            else LiveDecisions++;
            if (applied && !shadow)
            {
                Applied++;
                AppliedByEvent[e]++;
            }
            if (d.DefenderIsPlayer) OnPlayer++;
            if (d.AttackerIsPlayer) ByPlayer++;
            CostMs += costMs;
            if (costMs > MaxCostMs) MaxCostMs = costMs;
        }

        private static bool Live(in ImpactRoll o) => o.InRow && o.Refusal == ImpactRefusal.None && o.Raw > 0;

        private void NoteRoll(int e, int b, int o, in ImpactRoll r)
        {
            if (!r.InRow) return;
            InRow[e, b, o]++;
            if (r.Refusal != ImpactRefusal.None)
            {
                if (r.Raw > 0 || r.Refusal == ImpactRefusal.Down) Refused[(int)r.Refusal]++; // a refusal that cost a chance (or he was down)
                return;
            }
            if (!r.Rolled) return;
            Rolled[e, b, o]++;
            SumChance[e, b, o] += r.Chance;
            if (r.Happens) Happened[e, b, o]++;
        }

        public void Skip(ImpactSkip why, ImpactEvent e)
        {
            Skipped[(int)why]++;
            if (why == ImpactSkip.FamilyOff) SkippedByEvent[(int)e]++;
        }

        // ------------------------------------------------------------------ the [summary] lines

        /// <summary>The [summary] "impact" lines (IMPACT §6): the header, one line per event kind seen (by the defender's bar band), the
        /// riposte, the limits, the lengths asked, the cost. One line when the model never ran.</summary>
        public IReadOnlyList<string> SummaryLines(in ImpactRules now)
        {
            var lines = new List<string>();
            int total = Decisions;
            // "did" per event once its family applied (step 50: kicks / bashes); the riders' own lines are step 53's ImpactRiderLiveStats
            string verb = "would";
            if (!EverOn && total == 0)
            {
                lines.Add("impact (step 48 - the model): OFF all mission (" + (now.OffBecause ?? "on at the end") + ") - nothing decided"
                          + (Skipped[(int)ImpactSkip.Off] > 0 ? " (" + I(Skipped[(int)ImpactSkip.Off]) + " melee events while off)" : string.Empty));
                return lines;
            }
            var head = new StringBuilder();
            head.Append("impact (step 48 - the model; ").Append(ModeWords()).Append("): ").Append(I(total)).Append(" decisions - ");
            bool first = true;
            for (int e = 0; e < E; e++)
            {
                if (!first) head.Append(", ");
                first = false;
                head.Append(ImpactMath.EventPlural((ImpactEvent)e)).Append(' ').Append(I(DecidedOf((ImpactEvent)e)));
            }
            head.Append("; YOU took ").Append(I(OnPlayer)).Append(", dealt ").Append(I(ByPlayer));
            head.Append("; not decided: off ").Append(I(Skipped[(int)ImpactSkip.Off])).Append(", its family switched off ").Append(I(Skipped[(int)ImpactSkip.FamilyOff]))
                .Append(", not tracked (a horse, a man gone) ").Append(I(Skipped[(int)ImpactSkip.NotTracked])).Append(", the engine read failed ").Append(I(Skipped[(int)ImpactSkip.ReadFailed]))
                .Append(", the blow killed him (step 62) ").Append(I(Skipped[(int)ImpactSkip.Fatal]))
                .Append(", a FRIEND's blow (step 63 - the game cancels its damage, nothing plays) ").Append(I(Skipped[(int)ImpactSkip.NotEnemies]));
            if (FromDecorator > 0) head.Append("; ").Append(I(FromDecorator)).Append(" landed blows decided in the damage decorator's KnockDown seam (one decision per event)");
            if (Switches > 0) head.Append("; switched ").Append(I(Switches)).Append("x, on ").Append(F0(OnSeconds)).Append(" s / off ").Append(F0(OffSeconds)).Append(" s");
            lines.Add(head.ToString());

            for (int e = 0; e < E; e++)
            {
                if (DecidedOf((ImpactEvent)e) == 0) continue;
                lines.Add(EventLine((ImpactEvent)e, AppliedByEvent[e] > 0 ? "did" : "would"));
            }

            int ra = 0;
            foreach (var v in RiposteAsked) ra += v;
            if (ra > 0)
            {
                var r = new StringBuilder("impact riposte (a right-side block or parry staggers the ATTACKER; by the attacker's bar ");
                r.Append(BandsHeader()).Append("): asked ").Append(Row(RiposteAsked)).Append("; rolled ").Append(Row(RiposteRolled))
                    .Append(" (mean chance ").Append(MeanRow(RiposteChance, RiposteRolled)).Append("); ").Append(verb).Append(" stagger him ").Append(Row(RiposteHappened))
                    .Append(" = ").Append(I(RiposteTotal)).Append(" (recoil ").Append(Seconds(in RiposteSeconds)).Append(")");
                lines.Add(r.ToString());
            }

            lines.Add("impact limits: refused - on the ground " + I(Refused[(int)ImpactRefusal.Down]) + ", knockdown immunity " + I(Refused[(int)ImpactRefusal.KnockdownImmune])
                      + ", stagger immunity (reeling or just after) " + I(Refused[(int)ImpactRefusal.StaggerImmune]) + ", no shield in hand " + I(Refused[(int)ImpactRefusal.NoShield])
                      + ", a rider (never staggered) " + I(Refused[(int)ImpactRefusal.Rider]) + ", replaced by a knockdown / stagger " + I(Refused[(int)ImpactRefusal.Replaced])
                      + "; diminished: " + I(Halved) + " decisions with a recent outcome (" + (Halved > 0 ? F1((double)Halvings / Halved) : "0") + " halvings each on average)"
                      + "; riders knocked off their horse by the dice " + I(Dismounts) + " (step 53: applied / seen in the 'impact riders' line)");
            lines.Add("impact lengths asked: stagger " + Seconds(in StaggerSeconds) + ", a kicked man's reel " + Times(in ReelFactor) + ", get-up " + Times(in GetUpFactor)
                      + ", the blocker's stun " + Times(in StunFactor) + ", a riposted attacker's recoil " + Seconds(in RiposteSeconds)
                      + " (" + (ImpactMath.EffectsBuilt == ImpactFamily.None ? "asked only - no effect built yet; steps 50-52 measure what the engine plays" : "the kicks' reel and get-up measured in the 'impact live lengths' line; blocks and hits asked only until steps 51-52") + ")");
            lines.Add("impact cost: " + I(total) + " decisions, " + (total > 0 ? F1(1000 * CostMs / total) : "0") + " µs a decision (max " + F1(1000 * MaxCostMs) + " µs) - "
                      + (ShadowDecisions > 0 ? I(ShadowDecisions) + " in shadow" : "none in shadow") + ", " + I(LiveDecisions) + " live (" + I(Applied) + " applied)");
            return lines;
        }

        private string ModeWords()
        {
            if (ShadowDecisions > 0 && LiveDecisions > 0) return "MIXED - shadow " + I(ShadowDecisions) + ", live " + I(LiveDecisions) + " (" + I(Applied) + " applied)";
            if (ShadowDecisions > 0) return "SHADOW - decided and logged, nothing applied";
            if (Applied == 0) return "LIVE, but nothing applied this mission (the built families: " + ImpactMath.FamiliesText(ImpactMath.EffectsBuilt) + ") - decided and logged only";
            return "LIVE - " + ImpactMath.FamiliesText(ImpactMath.EffectsBuilt) + " applied (" + I(Applied) + " decisions)";
        }

        private string EventLine(ImpactEvent ev, string verb)
        {
            int e = (int)ev;
            var s = new StringBuilder("impact ");
            s.Append(ImpactMath.EventPlural(ev)).Append(" by the ").Append(ImpactMath.IsBlock(ev) ? "blocker's" : ImpactMath.IsAlternative(ev) ? "kicked man's" : "hit man's")
                .Append(" bar ").Append(BandsHeader()).Append(": ").Append(I(DecidedOf(ev))).Append(" = ");
            var dec = new int[B];
            var sx = new double[B];
            for (int b = 0; b < B; b++)
            {
                dec[b] = Decided[e, b];
                sx[b] = SumX[e, b];
            }
            s.Append(Row(dec)).Append("; mean x ").Append(MeanRowPlain(sx, dec));
            for (int o = 0; o < O; o++)
            {
                int inRow = 0;
                for (int b = 0; b < B; b++) inRow += InRow[e, b, o];
                if (inRow == 0) continue;
                var rolled = new int[B];
                var chance = new double[B];
                var happened = new int[B];
                int hap = 0;
                for (int b = 0; b < B; b++)
                {
                    rolled[b] = Rolled[e, b, o];
                    chance[b] = SumChance[e, b, o];
                    happened[b] = Happened[e, b, o];
                    hap += happened[b];
                }
                s.Append("; ").Append(verb).Append(' ').Append(Verb((ImpactOutcome)o)).Append(' ').Append(Row(happened)).Append(" = ").Append(I(hap))
                    .Append(" (rolled ").Append(Row(rolled)).Append(", mean chance ").Append(MeanRow(chance, rolled)).Append(')');
            }
            return s.ToString();
        }

        private static string Verb(ImpactOutcome o) => o switch
        {
            ImpactOutcome.Knockdown => "knock down",
            ImpactOutcome.ShieldDrop => "drop a shield",
            _ => "stagger",
        };

        private static string BandsHeader()
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

        /// <summary>Per band: the mean chance in % ("-" when nothing rolled).</summary>
        private static string MeanRow(double[] sum, int[] n)
        {
            var s = new StringBuilder();
            for (int b = 0; b < n.Length; b++)
            {
                if (b > 0) s.Append(" / ");
                s.Append(n[b] > 0 ? F0(100 * sum[b] / n[b]) + "%" : "-");
            }
            return s.ToString();
        }

        private static string MeanRowPlain(double[] sum, int[] n)
        {
            var s = new StringBuilder();
            for (int b = 0; b < n.Length; b++)
            {
                if (b > 0) s.Append(" / ");
                s.Append(n[b] > 0 ? F2(sum[b] / n[b]) : "-");
            }
            return s.ToString();
        }

        private static string Seconds(in MeanStd m) => m.Count == 0 ? "none" : F2(m.Mean) + " ± " + F2(m.StdDev) + " s (n " + I(m.Count) + ")";

        private static string Times(in MeanStd m) => m.Count == 0 ? "none" : "x" + F2(m.Mean) + " ± " + F2(m.StdDev) + " (n " + I(m.Count) + ")";

        private static string I(long v) => v.ToString(CultureInfo.InvariantCulture);

        private static string F0(double v) => v.ToString("0", CultureInfo.InvariantCulture);

        private static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        private static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    }
}

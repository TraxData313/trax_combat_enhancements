using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 43 per mission: fevers armed by order kind (yours / the AI general's), the arm → contact delay, fevers that never met an
    /// enemy, burn lengths with / without a shield in hand, the decision recomputes (per AI man-minute and per fever), fever sprints by
    /// the fever's share left and the refusals by reason, THE PROOF - AI melee attacks per man-minute in contact by bar band: in a
    /// formation without a fever, in a fever, and in no formation (tournaments, arenas, duels) - and the decision values the decorator
    /// really wrote by bar band and in a fever. The [summary] "fever" lines. Main thread.
    /// </summary>
    public sealed class FeverStats
    {
        /// <summary>The proof's groups: an AI man on foot in a formation without a fever, in a fever, in no formation.</summary>
        public const int Calm = 0, InFever = 1, NoFormation = 2, Groups = 3;

        /// <summary>The bar bands: 100-75, 75-50, 50-25, 25-0 %.</summary>
        public const int BarBands = 4;

        /// <summary>The values' states: the four bar bands without a fever, then "in a fever".</summary>
        public const int ValueStates = BarBands + 1;

        private readonly int[,] _armed = new int[(int)SprintOrder.Count, 2]; // [order, by player 1 / AI general 0]
        private readonly int[] _ends = new int[(int)FeverEnd.Count];
        private readonly int[] _neverMet = new int[(int)FeverEnd.Count];
        private readonly int[] _sprintsByBand = new int[FeverMath.ShareBands];
        private readonly int[] _refused = new int[(int)FeverSprintRefusal.Count];
        private readonly double[,] _attacks = new double[Groups, BarBands];
        private readonly double[,] _contactSeconds = new double[Groups, BarBands];
        private readonly MeanStd[] _valGameAttack = new MeanStd[ValueStates];
        private readonly MeanStd[] _valAttack = new MeanStd[ValueStates];
        private readonly MeanStd[] _valGameRiposte = new MeanStd[ValueStates];
        private readonly MeanStd[] _valRiposte = new MeanStd[ValueStates];
        private MeanStd _contactDelay;
        private double _contactDelayMax;
        private MeanStd _burnShield;
        private MeanStd _burnShieldStore;
        private MeanStd _burnPlain;
        private MeanStd _burnPlainStore;
        private MeanStd _recomputes;
        private int _recomputesMax;

        public int Rearmed;
        public int KeptLarger;
        public int Repeats;
        public int NotArmedMounted;
        public int NotArmedNoStore;
        public int NotArmedPlayer;
        public int BurnsStarted;
        public int ShieldSwitchedBurns;
        public int SprintChecks;
        public int SprintsStarted;
        public int MenLooked;
        public double PollCostMs;
        public int Polls;

        /// <summary>Recomputes asked because a man's decision share moved a step (his bar, his fever, a setting).</summary>
        public int DecisionRecomputes;

        /// <summary>AI man-seconds on foot looked at (the recomputes' "per man-minute").</summary>
        public double AiManSeconds;

        /// <summary>The bar band of a bar (points ÷ his full pool): 0 = 100-75 %, 1 = 75-50, 2 = 50-25, 3 = 25-0.</summary>
        public static int BarBand(double bar) => bar > 0.75 ? 0 : bar > 0.5 ? 1 : bar > 0.25 ? 2 : 3;

        public static string BarBandName(int band) => band == 0 ? "100-75%" : band == 1 ? "75-50%" : band == 2 ? "50-25%" : "25-0%";

        public int Armed
        {
            get
            {
                int n = 0;
                for (int o = 0; o < (int)SprintOrder.Count; o++) n += _armed[o, 0] + _armed[o, 1];
                return n;
            }
        }

        public int ArmedBy(SprintOrder order, bool byPlayer) => _armed[(int)order, byPlayer ? 1 : 0];

        public int Ended(FeverEnd e) => _ends[(int)e];

        public int NeverMet(FeverEnd e) => _neverMet[(int)e];

        public int Refused(FeverSprintRefusal r) => _refused[(int)r];

        public int SprintsInBand(int band) => _sprintsByBand[band];

        public double Attacks(int group, int band) => _attacks[group, band];

        public double ContactSeconds(int group, int band) => _contactSeconds[group, band];

        public double ContactSeconds(int group)
        {
            double s = 0;
            for (int b = 0; b < BarBands; b++) s += _contactSeconds[group, b];
            return s;
        }

        public double MeanRecomputes => _recomputes.Mean;

        public void AddArmed(SprintOrder order, bool byPlayer)
        {
            if ((int)order < 0 || (int)order >= (int)SprintOrder.Count) return;
            _armed[(int)order, byPlayer ? 1 : 0]++;
        }

        public void AddBurnStart(double delay)
        {
            BurnsStarted++;
            if (double.IsNaN(delay) || delay < 0) delay = 0;
            _contactDelay.Add(delay);
            if (delay > _contactDelayMax) _contactDelayMax = delay;
        }

        /// <summary>A fever ended: by reason, never met an enemy (still armed), its burn by shield (the larger part of its time), recomputes.</summary>
        public void AddEnd(FeverEnd end, bool burned, double shieldSeconds, double plainSeconds, double storeAtBurn, bool shieldSwitched, int recomputes)
        {
            _ends[(int)end]++;
            if (!burned) _neverMet[(int)end]++;
            else
            {
                double length = shieldSeconds + plainSeconds;
                if (end == FeverEnd.Faded)
                {
                    if (shieldSeconds > plainSeconds)
                    {
                        _burnShield.Add(length);
                        _burnShieldStore.Add(storeAtBurn);
                    }
                    else
                    {
                        _burnPlain.Add(length);
                        _burnPlainStore.Add(storeAtBurn);
                    }
                }
                if (shieldSwitched) ShieldSwitchedBurns++;
            }
            _recomputes.Add(recomputes);
            if (recomputes > _recomputesMax) _recomputesMax = recomputes;
        }

        public void AddSprint(double share)
        {
            SprintsStarted++;
            _sprintsByBand[FeverMath.ShareBand(share)]++;
        }

        public void AddRefusal(FeverSprintRefusal r) => _refused[(int)r]++;

        /// <summary>An AI man on foot's seconds in contact (an enemy within FeverContactMetres at his last look), by group and bar band.</summary>
        public void AddContactTime(int group, int band, double seconds)
        {
            if (seconds > 0) _contactSeconds[group, band] += seconds;
        }

        /// <summary>One AI melee attack (a release on foot) while in contact.</summary>
        public void AddAttack(int group, int band) => _attacks[group, band]++;

        /// <summary>The decorator wrote the two values: the game's → ours, by state (a bar band without a fever, or <see cref="BarBands"/> = in a fever).</summary>
        public void AddValues(int state, float gameAttack, float attack, float gameRiposte, float riposte)
        {
            if (state < 0 || state >= ValueStates) return;
            _valGameAttack[state].Add(gameAttack);
            _valAttack[state].Add(attack);
            _valGameRiposte[state].Add(gameRiposte);
            _valRiposte[state].Add(riposte);
        }

        public int ValuesWritten(int state) => _valAttack[state].Count;

        public double MeanAttackWritten(int state) => _valAttack[state].Mean;

        /// <summary>Attacks per man-minute in contact (NaN with no contact time).</summary>
        public double PerManMinute(int group, int band)
        {
            double s = _contactSeconds[group, band];
            return s > 0 ? _attacks[group, band] / (s / 60.0) : double.NaN;
        }

        /// <summary>Over all bands of a group.</summary>
        public double PerManMinute(int group)
        {
            double s = 0, a = 0;
            for (int b = 0; b < BarBands; b++)
            {
                s += _contactSeconds[group, b];
                a += _attacks[group, b];
            }
            return s > 0 ? a / (s / 60.0) : double.NaN;
        }

        /// <summary>The [summary] "fever" lines (10).</summary>
        public void AddSummaryLines(List<string> lines, in FeverRules r)
        {
            lines.Add("fever settings: " + r.Describe());
            var t = new StringBuilder("fever armed: ").Append(Armed);
            t.Append(" (charge ").Append(ArmedBy(SprintOrder.Charge, true) + ArmedBy(SprintOrder.Charge, false))
                .Append(" - by you ").Append(ArmedBy(SprintOrder.Charge, true)).Append(" / by the AI general ").Append(ArmedBy(SprintOrder.Charge, false))
                .Append("; advance ").Append(ArmedBy(SprintOrder.Advance, true) + ArmedBy(SprintOrder.Advance, false))
                .Append(" - by you ").Append(ArmedBy(SprintOrder.Advance, true)).Append(" / by the AI general ").Append(ArmedBy(SprintOrder.Advance, false))
                .Append("); a different aggressive order on a running fever: re-armed to a fresh one ").Append(Rearmed)
                .Append(", kept (more left than a fresh one) ").Append(KeptLarger).Append("; the same order again ignored ").Append(Repeats)
                .Append("; not armed: on a horse ").Append(NotArmedMounted).Append(", no Athletics ").Append(NotArmedNoStore)
                .Append(", player-controlled ").Append(NotArmedPlayer);
            lines.Add(t.ToString());

            int armed = Armed;
            int never = 0;
            for (int e = 0; e < (int)FeverEnd.Count; e++) never += _neverMet[e];
            t.Clear().Append("fever contact: ").Append(BurnsStarted).Append(" of ").Append(armed).Append(" fevers met an enemy within ")
                .Append(F1(r.ContactMetres)).Append(" m");
            if (_contactDelay.Count > 0) t.Append(" - armed → contact mean ").Append(F1(_contactDelay.Mean)).Append(" s, max ").Append(F1(_contactDelayMax)).Append(" s");
            t.Append("; never met one (ended still armed): ").Append(never);
            if (never > 0)
            {
                t.Append(" (");
                bool first = true;
                for (int e = 0; e < (int)FeverEnd.Count; e++)
                {
                    if (_neverMet[e] == 0) continue;
                    if (!first) t.Append(", ");
                    first = false;
                    t.Append(FeverMath.EndName((FeverEnd)e)).Append(' ').Append(_neverMet[e]);
                }
                t.Append(')');
            }
            lines.Add(t.ToString());

            t.Clear().Append("fever burn: faded ").Append(Ended(FeverEnd.Faded)).Append("; burn length with a shield in hand (most of it) ");
            AppendBurn(t, _burnShield, _burnShieldStore);
            t.Append("; without ");
            AppendBurn(t, _burnPlain, _burnPlainStore);
            t.Append(" (FeverShieldDurationMultiplier ").Append(F2(r.ShieldDurationMultiplier)).Append(" - a shield man's fever lasts that share)")
                .Append("; a shield taken up or put away mid-burn ").Append(ShieldSwitchedBurns).Append("; all ends:");
            for (int e = 0; e < (int)FeverEnd.Count; e++)
                if (_ends[e] > 0) t.Append(' ').Append(FeverMath.EndName((FeverEnd)e)).Append(' ').Append(_ends[e]).Append(',');
            if (t[t.Length - 1] == ',') t.Length--;
            else t.Append(" none");
            lines.Add(t.ToString());

            t.Clear().Append("fever recomputes: ").Append(DecisionRecomputes).Append(" asked for the decision values (a 0.05 step of the share) over ")
                .Append(F1(AiManSeconds / 60.0)).Append(" AI man-minutes on foot = ")
                .Append(AiManSeconds > 0 ? F2(DecisionRecomputes / (AiManSeconds / 60.0)) : "-").Append(" a man-minute; per fever ");
            if (_recomputes.Count == 0) t.Append("- (no fever ended)");
            else t.Append(F1(_recomputes.Mean)).Append(" (max ").Append(_recomputesMax).Append(')');
            lines.Add(t.ToString());

            t.Clear().Append("fever sprints: started ").Append(SprintsStarted).Append(" (fever left");
            for (int b = 0; b < FeverMath.ShareBands; b++) t.Append(b == 0 ? " " : ", ").Append(FeverMath.ShareBandName(b)).Append(": ").Append(_sprintsByBand[b]);
            t.Append(") in ").Append(SprintChecks).Append(" checks; refused:");
            for (int i = 0; i < (int)FeverSprintRefusal.Count; i++)
                t.Append(i == 0 ? " " : ", ").Append(FeverMath.RefusalName((FeverSprintRefusal)i)).Append(' ').Append(_refused[i]);
            lines.Add(t.ToString());

            t.Clear().Append("fever proof: AI melee attacks per man-minute in contact (an enemy within ").Append(F1(r.ContactMetres))
                .Append(" m, on foot), by bar 100-75 / 75-50 / 50-25 / 25-0 %: in a formation, no fever ");
            AppendRates(t, Calm);
            t.Append("; in a fever ");
            AppendRates(t, InFever);
            double c = PerManMinute(Calm), f = PerManMinute(InFever);
            if (c > 0 && f > 0) t.Append(" - in a fever x").Append(F2(f / c)).Append(" the rate without");
            lines.Add(t.ToString());

            t.Clear().Append("fever proof, no formation (tournaments, arenas, duels - no orders, no fever): by bar 100-75 / 75-50 / 50-25 / 25-0 % ");
            AppendRates(t, NoFormation);
            lines.Add(t.ToString());

            t.Clear().Append("fever values written (AI on foot, the game's → ours): attack chance");
            AppendValues(t, _valGameAttack, _valAttack);
            t.Append("; riposte chance");
            AppendValues(t, _valGameRiposte, _valRiposte);
            lines.Add(t.ToString());

            t.Clear().Append("fever cost: ").Append(Polls).Append(" looks over ").Append(MenLooked).Append(" men, ")
                .Append(Polls > 0 ? (PollCostMs / Polls * 1000).ToString("0.0", CultureInfo.InvariantCulture) : "0").Append(" µs a look");
            lines.Add(t.ToString());
        }

        private static void AppendBurn(StringBuilder t, MeanStd burn, MeanStd store)
        {
            if (burn.Count == 0)
            {
                t.Append("none");
                return;
            }
            t.Append(burn.Count).Append(", mean ").Append(F1(burn.Mean)).Append(" s from a mean store of ").Append(F1(store.Mean))
                .Append(" s (x").Append(F2(store.Mean > 0 ? burn.Mean / store.Mean : 0)).Append(')');
        }

        private void AppendRates(StringBuilder t, int group)
        {
            double a = 0, s = 0;
            for (int b = 0; b < BarBands; b++)
            {
                if (b > 0) t.Append(" / ");
                double rate = PerManMinute(group, b);
                t.Append(double.IsNaN(rate) ? "-" : F2(rate));
                a += _attacks[group, b];
                s += _contactSeconds[group, b];
            }
            t.Append(" (").Append(a.ToString("0", CultureInfo.InvariantCulture)).Append(" attacks over ").Append(F1(s / 60.0)).Append(" man-min)");
        }

        private static void AppendValues(StringBuilder t, MeanStd[] game, MeanStd[] ours)
        {
            for (int i = 0; i < ValueStates; i++)
            {
                string name = i < BarBands ? "bar " + BarBandName(i) : "in a fever";
                t.Append(i == 0 ? " " : ", ").Append(name).Append(' ');
                if (ours[i].Count == 0) t.Append("-");
                else
                    t.Append(F3(game[i].Mean)).Append(" → ").Append(F3(ours[i].Mean)).Append(" (x").Append(F2(game[i].Mean > 0 ? ours[i].Mean / game[i].Mean : 1))
                        .Append(", ").Append(ours[i].Count).Append(')');
            }
        }

        private static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        private static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        private static string F3(double v) => v.ToString("0.000", CultureInfo.InvariantCulture);
    }
}

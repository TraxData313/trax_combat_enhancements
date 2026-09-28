using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 21's per-battle numbers (AI_NOTES "Step 21") - what Anton tunes the battle pace from: per attack CLASS (shield
    /// infantry, other foot melee, riders, bowmen, crossbowmen, thrown and slings; AI only) the attacks and the men who made
    /// them, the pauses by reason (the share alone at full strength, tired + the share, tired only) with the tired part and the
    /// share, the measured cycle (release to release, shot to shot - a fighting rhythm only, the attack-rate cap) and so the
    /// attacks a minute per man while fighting, the AI's own gap after a pause, and - for foot melee - the model's cycle with
    /// the setting at 0 and with it (his attack + the pause + his own gap AiMeleeGapSeconds) against the measured one. Main
    /// thread; allocation only at construction and in <see cref="SummaryLines"/>.
    /// </summary>
    public sealed class BattlePaceStats
    {
        private const int Classes = (int)AttackClass.Count;

        private readonly int[] _attacks = new int[Classes];
        private readonly int[] _men = new int[Classes];
        private readonly int[] _pausesShareOnly = new int[Classes];   // at full strength: the share alone
        private readonly int[] _pausesTiredAndShare = new int[Classes];
        private readonly int[] _pausesTiredOnly = new int[Classes];
        private readonly int[] _noPause = new int[Classes];
        private readonly MeanStd[] _tired = new MeanStd[Classes];
        private readonly MeanStd[] _share = new MeanStd[Classes];
        private readonly MeanStd[] _cycle = new MeanStd[Classes];
        private readonly MeanStd[] _cycleShare = new MeanStd[Classes]; // the share inside each counted cycle (0 when none)
        private readonly MeanStd[] _modelAt0 = new MeanStd[Classes];
        private readonly MeanStd[] _modelWith = new MeanStd[Classes];
        private readonly MeanStd[] _cycleModelled = new MeanStd[Classes]; // the measured cycles that have a model beside them
        private readonly MeanStd[] _gapAfter = new MeanStd[Classes];

        /// <summary>An AI attack of class <paramref name="c"/> ended; <paramref name="newMan"/> = his first of this class this battle.</summary>
        public void AddAttack(AttackClass c, bool newMan)
        {
            int i = Index(c);
            if (i < 0) return;
            _attacks[i]++;
            if (newMan) _men[i]++;
        }

        /// <summary>A pause STARTED after an attack of class <paramref name="c"/>: its tired part and its battle-pace share.</summary>
        public void AddPause(AttackClass c, double tired, double share)
        {
            int i = Index(c);
            if (i < 0) return;
            bool t = tired > 0 && !double.IsNaN(tired), x = share > 0 && !double.IsNaN(share);
            if (x && t) _pausesTiredAndShare[i]++;
            else if (x) _pausesShareOnly[i]++;
            else _pausesTiredOnly[i]++;
            _tired[i].Add(t ? tired : 0);
            if (x) _share[i].Add(share);
        }

        /// <summary>An AI attack of class <paramref name="c"/> that got no pause (full strength with no share, under 0.1 s, not
        /// measured, the next attack already readied, refused by the tick).</summary>
        public void AddNoPause(AttackClass c)
        {
            int i = Index(c);
            if (i >= 0) _noPause[i]++;
        }

        /// <summary>
        /// One counted cycle (a fighting rhythm) after an attack of class <paramref name="c"/>: <paramref name="seconds"/> measured,
        /// <paramref name="shareInside"/> = the battle-pace share of the pause inside it (0 when none), and the model's cycle
        /// with the setting at 0 and with it (NaN = no model: ranged, riders, a length not measured).
        /// </summary>
        public void AddCycle(AttackClass c, double seconds, double shareInside, double modelAt0, double modelWith)
        {
            int i = Index(c);
            if (i < 0 || double.IsNaN(seconds) || seconds < 0) return;
            _cycle[i].Add(seconds);
            _cycleShare[i].Add(double.IsNaN(shareInside) || shareInside < 0 ? 0 : shareInside);
            if (double.IsNaN(modelAt0) || double.IsNaN(modelWith)) return;
            _modelAt0[i].Add(modelAt0);
            _modelWith[i].Add(modelWith);
            _cycleModelled[i].Add(seconds);
        }

        /// <summary>After a pause of class <paramref name="c"/> ended, his next attack began <paramref name="seconds"/> later.</summary>
        public void AddGapAfterPause(AttackClass c, double seconds)
        {
            int i = Index(c);
            if (i < 0 || double.IsNaN(seconds) || seconds < 0) return;
            _gapAfter[i].Add(seconds);
        }

        public int Attacks(AttackClass c) => _attacks[Index(c)];

        public int Men(AttackClass c) => _men[Index(c)];

        public int Pauses(AttackClass c)
        {
            int i = Index(c);
            return _pausesShareOnly[i] + _pausesTiredAndShare[i] + _pausesTiredOnly[i];
        }

        public int PausesShareOnly(AttackClass c) => _pausesShareOnly[Index(c)];

        public int PausesTiredAndShare(AttackClass c) => _pausesTiredAndShare[Index(c)];

        public int PausesTiredOnly(AttackClass c) => _pausesTiredOnly[Index(c)];

        public int NoPause(AttackClass c) => _noPause[Index(c)];

        public double ShareMean(AttackClass c) => _share[Index(c)].Mean;

        public double TiredMean(AttackClass c) => _tired[Index(c)].Mean;

        public int Cycles(AttackClass c) => _cycle[Index(c)].Count;

        public double CycleMean(AttackClass c) => _cycle[Index(c)].Mean;

        public double GapAfterMean(AttackClass c) => _gapAfter[Index(c)].Mean;

        public double ModelAt0Mean(AttackClass c) => _modelAt0[Index(c)].Mean;

        public double ModelWithMean(AttackClass c) => _modelWith[Index(c)].Mean;

        /// <summary>The measured cycles that had a model ÷ the model's cycle with the setting (NaN below
        /// <see cref="AttackRateMath.MinBandCycles"/> of them).</summary>
        public double ModelRatio(AttackClass c)
        {
            int i = Index(c);
            var m = _cycleModelled[i];
            return m.Count < AttackRateMath.MinBandCycles || double.IsNaN(_modelWith[i].Mean) || _modelWith[i].Mean <= 0 ? double.NaN : m.Mean / _modelWith[i].Mean;
        }

        // ------------------------------------------------------------------ the summary text

        /// <summary>The <c>[summary]</c> "battle pace" lines (docs/PLAYTEST.md L5b quotes them). <paramref name="paceOn"/> / <paramref name="offBecause"/>:
        /// the AI timer at the end; <paramref name="changedDuringBattle"/>: a battle-pace setting changed mid-battle.</summary>
        public List<string> SummaryLines(in BattlePaceRules r, bool paceOn, string? offBecause, bool changedDuringBattle)
        {
            var lines = new List<string>
            {
                "battle pace (step 21) settings at the end: " + r.Describe(paceOn, offBecause)
                + (changedDuringBattle ? " - a battle-pace setting CHANGED during this battle: the lines below mix both" : string.Empty),
            };
            for (int i = 0; i < Classes; i++) lines.Add(ClassLine(in r, (AttackClass)i));
            return lines;
        }

        private string ClassLine(in BattlePaceRules r, AttackClass c)
        {
            int i = (int)c;
            var sb = new StringBuilder("battle pace - ").Append(BattlePaceMath.Name(c)).Append(" (").Append(BattlePaceMath.What(c)).Append("; ").Append(r.SettingText(c));
            int pct = r.SwingsLessPercent(c);
            if (pct > 0) sb.Append(" - asks ").Append(pct).Append("% fewer swings");
            else if (r.ExtraSeconds(c) > 0) sb.Append(" - sized for about 30% fewer shots");
            sb.Append("): ");
            string unit = BattlePaceMath.Unit(c);
            if (_attacks[i] == 0)
            {
                sb.Append("no attacks");
                return sb.ToString();
            }
            sb.Append(_attacks[i]).Append(' ').Append(BattlePaceMath.IsMelee(c) ? "attacks" : unit).Append(" by ").Append(_men[i]).Append(_men[i] == 1 ? " man" : " men");
            int pauses = Pauses(c);
            sb.Append("; pauses ").Append(pauses);
            if (pauses > 0)
            {
                bool shareClass = pct > 0 || r.ExtraSeconds(c) > 0 || _share[i].Count > 0;
                if (shareClass)
                    sb.Append(" - at full strength ").Append(_pausesShareOnly[i]).Append(" (the share alone), tired ").Append(_pausesTiredAndShare[i])
                      .Append(" (tired + the share), tired only ").Append(_pausesTiredOnly[i]);
                else
                    sb.Append(" (tired - no share)");
                sb.Append("; tired part avg ").Append(N2(_tired[i].Mean)).Append(" s");
                if (_share[i].Count > 0) sb.Append(", the share avg ").Append(N2(_share[i].Mean)).Append(" s");
            }
            sb.Append("; no pause ").Append(_noPause[i]).Append(" (full strength with no share, under 0.1 s, not measured, chained or refused)");

            var cyc = _cycle[i];
            if (cyc.Count == 0)
            {
                sb.Append("; no cycle measured (a fighting rhythm needs two attacks in a row)");
                return sb.ToString();
            }
            sb.Append("; cycle ").Append(N2(cyc.Mean)).Append(" s (n ").Append(cyc.Count).Append(") = ").Append(N1(BattlePaceMath.PerMinute(cyc.Mean)))
              .Append(' ').Append(unit).Append(" a minute per man while ").Append(BattlePaceMath.IsMelee(c) ? "fighting" : "shooting");
            if (_gapAfter[i].Count > 0)
            {
                sb.Append("; his own gap after a pause avg ").Append(N2(_gapAfter[i].Mean)).Append(" s (n ").Append(_gapAfter[i].Count);
                if (c == AttackClass.ShieldInfantry || c == AttackClass.FootMelee)
                    sb.Append(" - the model assumes ").Append(N2(r.AiGapSeconds)).Append(" s, AiMeleeGapSeconds: shorter = part of the pause hid in his own idle time");
                sb.Append(')');
            }

            if (c == AttackClass.ShieldInfantry || c == AttackClass.FootMelee)
            {
                var m0 = _modelAt0[i];
                if (m0.Count == 0)
                {
                    sb.Append("; no model (no attack length measured)");
                    return sb.ToString();
                }
                double at0 = m0.Mean, with = _modelWith[i].Mean;
                sb.Append("; the model (his attack + the pause + his own gap): with the setting at 0 ").Append(N2(at0)).Append(" s = ").Append(N1(BattlePaceMath.PerMinute(at0)))
                  .Append(" a minute");
                if (with - at0 >= 0.005)
                    sb.Append(", with it ").Append(N2(with)).Append(" s = ").Append(N1(BattlePaceMath.PerMinute(with))).Append(" a minute (")
                      .Append(P0(BattlePaceMath.FewerShare(at0, with))).Append(" fewer)");
                double ratio = ModelRatio(c);
                if (double.IsNaN(ratio)) sb.Append(" - too few cycles for a verdict (need ").Append(AttackRateMath.MinBandCycles).Append(')');
                else sb.Append(" - measured ÷ it ").Append(P0(ratio)).Append(": ").Append(AttackRateMath.Verdict(ratio))
                       .Append(pct > 0 ? string.Empty : " (at 0 this checks the model's own gap)");
            }
            else if (c == AttackClass.Bow || c == AttackClass.Crossbow)
            {
                double share = _cycleShare[i].Mean;
                if (share > 0.005)
                {
                    double without = cyc.Mean - share;
                    sb.Append("; the extra inside a cycle avg ").Append(N2(share)).Append(" s - if none of it hid in his own gap, without it the cycle would be ")
                      .Append(N2(without)).Append(" s = ").Append(N1(BattlePaceMath.PerMinute(without))).Append(" a minute: at most ")
                      .Append(P0(BattlePaceMath.FewerShare(without, cyc.Mean))).Append(" fewer ").Append(unit)
                      .Append(" (the same battle with the slider at 0 is the real check)");
                }
            }
            return sb.ToString();
        }

        private static int Index(AttackClass c)
        {
            int i = (int)c;
            return i >= 0 && i < Classes ? i : -1;
        }

        private static string N1(double v) => double.IsNaN(v) ? "n/a" : v.ToString("0.0", CultureInfo.InvariantCulture);

        private static string N2(double v) => double.IsNaN(v) ? "n/a" : v.ToString("0.00", CultureInfo.InvariantCulture);

        private static string P0(double share) => double.IsNaN(share) ? "n/a" : (share * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
    }
}

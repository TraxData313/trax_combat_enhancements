using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace TraxCombat.Core
{
    /// <summary>
    /// One mission's attack-rate numbers (step 5e) - the in-game proof that the whole attack CYCLE
    /// follows m, not only the swing (DESIGN §2): for melee and ranged, AI fighters and you apart,
    /// per f band - every phase's average (wind-up, held / aim, swing / loose, the recoil after a
    /// block, reload, the pause), the cycle (release to release, shot to shot), m, the target (the
    /// group's cycle at the peak × the band's average 1/m) and measured ÷ target with a verdict word;
    /// the pace holds (AttackRatePaceHold); the AI-decision recomputes (AttackRateAiDecisions); and
    /// the guard by f (tired men must not block less - blocking is never slowed). Supersedes step
    /// 5c's "attack speed check" lines. Main thread, except <see cref="AddAiScaled"/> (interlocked).
    /// Allocation only at construction.
    /// </summary>
    public sealed class AttackRateStats
    {
        public const int Groups = 4; // melee AI, melee you, ranged AI, ranged you
        public const int Phases = 7;
        private const int Bins = AthleticsMath.PeakBins;

        private readonly MeanStd[,,] _phases = new MeanStd[Groups, Bins, Phases];
        private readonly MeanStd[,] _cycles = new MeanStd[Groups, Bins];
        private readonly MeanStd[,] _asked = new MeanStd[Groups, Bins];
        private readonly MeanStd[,] _slowdown = new MeanStd[Groups, Bins]; // 1/m per cycle: target = fresh × this

        /// <summary>Cycles left out because f changed bands between their two ends.</summary>
        public readonly int[] CyclesMixed = new int[Groups];

        /// <summary>Cycles, phases and pauses longer than the cap at their m - not a fighting rhythm.</summary>
        public readonly int[] LeftOut = new int[Groups];

        /// <summary>Readies that did not end in an attack (cancelled, feints, interrupted).</summary>
        public readonly int[] ReadiesCancelled = new int[Groups];

        /// <summary>Attacks whose next ready began straight out of the release / recoil / reload (pause 0).</summary>
        public readonly int[] Chained = new int[Groups];

        /// <summary>Cycles (and their pauses) with a step back (5d) in them - the step back's pause, not
        /// the attack rhythm: left out.</summary>
        public readonly int[] SteppedBack = new int[Groups];

        // ---- pace hold (T3)
        private readonly int[] _holdsByBin = new int[Bins];
        private readonly int[] _notHeld = new int[(int)PaceNotHeld.Count];
        private readonly int[] _refused = new int[(int)PaceRefusal.Count];
        private readonly int[] _ended = new int[(int)PaceEnd.Count];
        private readonly int[] _released = new int[4];
        private MeanStd _holdSeconds;
        private double _holdMax;
        private MeanStd _nextReady;
        private MeanStd _holdAsked;

        /// <summary>Holds that ended while a game job was on him and were cleared once he was free.</summary>
        public int ClearedAfterWaiting;

        /// <summary>Holds still running when the mission ended.</summary>
        public int HeldAtMissionEnd;

        // ---- the guard by f (bins, then "while held")
        private readonly int[] _hitsTaken = new int[Bins + 1];
        private readonly int[] _hitsBlocked = new int[Bins + 1];

        // ---- T2
        private int _aiScaled;

        public static int Group(AttackKind kind, bool player) => (int)kind * 2 + (player ? 1 : 0);

        // ------------------------------------------------------------------ counting

        /// <summary>One phase of one attack, struck at m <paramref name="asked"/> in f band
        /// <paramref name="bin"/>. Longer than the cap → left out (counted).</summary>
        public void AddPhase(AttackKind kind, bool player, int bin, AttackPhase phase, double seconds, float asked)
        {
            if (bin < 0 || bin >= Bins || double.IsNaN(seconds) || seconds < 0) return;
            int g = Group(kind, player);
            if (!AttackRateMath.Countable(kind, seconds, asked))
            {
                LeftOut[g]++;
                return;
            }
            _phases[g, bin, (int)phase].Add(seconds);
        }

        /// <summary>One cycle (release to release / shot to shot) that ran at m <paramref name="asked"/>
        /// in band <paramref name="bin"/> - classified by the band after the first attack's charge,
        /// the same at the second one (else <see cref="AddMixed"/>). True when it counted.</summary>
        public bool AddCycle(AttackKind kind, bool player, int bin, double seconds, float asked)
        {
            if (bin < 0 || bin >= Bins || double.IsNaN(seconds) || seconds < 0) return false;
            int g = Group(kind, player);
            if (!AttackRateMath.Countable(kind, seconds, asked))
            {
                LeftOut[g]++;
                return false;
            }
            float m = AttackRateMath.SafeM(asked);
            _cycles[g, bin].Add(seconds);
            _asked[g, bin].Add(m);
            _slowdown[g, bin].Add(1.0 / m);
            return true;
        }

        public void AddMixed(AttackKind kind, bool player) => CyclesMixed[Group(kind, player)]++;

        public void AddReadyCancelled(AttackKind kind, bool player) => ReadiesCancelled[Group(kind, player)]++;

        public void AddChained(AttackKind kind, bool player) => Chained[Group(kind, player)]++;

        public void AddSteppedBack(AttackKind kind, bool player) => SteppedBack[Group(kind, player)]++;

        public int PhaseCount(AttackKind kind, bool player, int bin, AttackPhase phase) => _phases[Group(kind, player), bin, (int)phase].Count;

        public double PhaseMean(AttackKind kind, bool player, int bin, AttackPhase phase) => _phases[Group(kind, player), bin, (int)phase].Mean;

        public int CycleCount(AttackKind kind, bool player, int bin) => _cycles[Group(kind, player), bin].Count;

        public double CycleMean(AttackKind kind, bool player, int bin) => _cycles[Group(kind, player), bin].Mean;

        /// <summary>The group's fresh cycle: its average at the peak (f 1), NaN below <see cref="AttackRateMath.MinFreshCycles"/>.</summary>
        public double FreshCycle(AttackKind kind, bool player)
        {
            var c = _cycles[Group(kind, player), 0];
            return c.Count >= AttackRateMath.MinFreshCycles ? c.Mean : double.NaN;
        }

        /// <summary>The band's target cycle: the fresh cycle × its average 1/m; NaN without a reference.</summary>
        public double TargetCycle(AttackKind kind, bool player, int bin)
        {
            double fresh = FreshCycle(kind, player);
            var s = _slowdown[Group(kind, player), bin];
            return double.IsNaN(fresh) || s.Count == 0 ? double.NaN : fresh * s.Mean;
        }

        /// <summary>measured ÷ target for a band with at least <see cref="AttackRateMath.MinBandCycles"/> cycles; NaN otherwise.</summary>
        public double Ratio(AttackKind kind, bool player, int bin)
        {
            var c = _cycles[Group(kind, player), bin];
            double target = TargetCycle(kind, player, bin);
            return c.Count < AttackRateMath.MinBandCycles || double.IsNaN(target) || target <= 0 ? double.NaN : c.Mean / target;
        }

        // ---- pace hold

        public void AddNotHeld(PaceNotHeld why) => _notHeld[(int)why]++;

        public int NotHeld(PaceNotHeld why) => _notHeld[(int)why];

        public void AddRefused(PaceRefusal why) => _refused[(int)why]++;

        public int Refused(PaceRefusal why) => _refused[(int)why];

        /// <summary>A hold started in band <paramref name="bin"/> at m <paramref name="asked"/>.</summary>
        public void AddHoldStart(int bin, float asked)
        {
            if (bin >= 0 && bin < Bins) _holdsByBin[bin]++;
            _holdAsked.Add(asked);
        }

        public int Holds
        {
            get
            {
                int n = 0;
                foreach (int h in _holdsByBin) n += h;
                return n;
            }
        }

        /// <summary>A hold ended after <paramref name="seconds"/>.</summary>
        public void AddHoldEnd(PaceEnd why, double seconds)
        {
            _ended[(int)why]++;
            if (seconds >= 0 && !double.IsNaN(seconds))
            {
                _holdSeconds.Add(seconds);
                if (seconds > _holdMax) _holdMax = seconds;
            }
        }

        public int Ended(PaceEnd why) => _ended[(int)why];

        public double HoldMeanSeconds => _holdSeconds.Mean;

        public void AddRelease(PaceRelease what) => _released[(int)what]++;

        public int Released(PaceRelease what) => _released[(int)what];

        /// <summary>His next ready began <paramref name="seconds"/> after his hold ended.</summary>
        public void AddNextReadyAfterHold(double seconds)
        {
            if (seconds >= 0 && seconds <= AttackRateMath.MeleeCycleCapSeconds) _nextReady.Add(seconds);
        }

        // ---- guard

        /// <summary>A melee collision on a fighter on foot in band <paramref name="bin"/>: blocked /
        /// parried or not; <paramref name="held"/> = under a pace hold.</summary>
        public void AddHitTaken(int bin, bool held, bool blocked)
        {
            if (bin < 0 || bin >= Bins) return;
            _hitsTaken[bin]++;
            if (blocked) _hitsBlocked[bin]++;
            if (!held) return;
            _hitsTaken[Bins]++;
            if (blocked) _hitsBlocked[Bins]++;
        }

        public int HitsTaken(int bin) => _hitsTaken[bin];

        public int HitsBlocked(int bin) => _hitsBlocked[bin];

        // ---- T2

        /// <summary>The decorator scaled the AI's decision values in one recompute (any thread).</summary>
        public void AddAiScaled() => Interlocked.Increment(ref _aiScaled);

        public int AiScaled => Volatile.Read(ref _aiScaled);

        // ------------------------------------------------------------------ the summary text

        /// <summary>The <c>[summary]</c> attack-rate lines (docs/PLAYTEST.md quotes them).
        /// <paramref name="switchedDuringBattle"/>: an A/B switch changed mid-battle - the numbers mix both.</summary>
        public List<string> SummaryLines(in AttackRateRules r, bool switchedDuringBattle)
        {
            var lines = new List<string>();
            lines.Add("attack rate settings at the end (DESIGN §2 - the whole cycle follows the attack speed m): " + r.Describe()
                      + (switchedDuringBattle ? " - an attack-rate switch CHANGED during this battle: the rows below mix both settings" : string.Empty));
            GroupLines(lines, AttackKind.Melee, player: false);
            GroupLines(lines, AttackKind.Melee, player: true);
            GroupLines(lines, AttackKind.Ranged, player: false);
            GroupLines(lines, AttackKind.Ranged, player: true);

            lines.Add("attack rate - left out: cycles whose two ends fell in different f bands (melee AI / you, ranged AI / you) " + Four(CyclesMixed)
                      + "; longer than " + N0(AttackRateMath.MeleeCycleCapSeconds) + " s ÷ m melee or " + N0(AttackRateMath.RangedCycleCapSeconds)
                      + " s ÷ m ranged (a pause, not a fighting rhythm) " + Four(LeftOut) + "; readies that ended in no attack (cancelled, feints) "
                      + Four(ReadiesCancelled) + "; chained (the next ready straight out of the last attack, pause 0) " + Four(Chained)
                      + "; with a step back in them (its pause, not the attack rhythm) " + Four(SteppedBack));

            lines.Add("attack rate - AI decisions (AttackRateAiDecisions " + (r.AiDecisions ? "on" : "off") + " at the end): scaled in " + AiScaled
                      + " recomputes - the chance to attack and to riposte x m, to loose x m, the aim before a shot ÷ m; the AI rows' pause and aim show whether the native AI follows them");

            var h = new StringBuilder("attack rate - pace hold (AttackRatePaceHold ").Append(r.PaceHold ? "on" : "off")
                .Append(" at the end; tired AI fighters on foot, after a melee swing): ").Append(Holds).Append(" holds");
            if (Holds > 0)
            {
                h.Append(", avg ").Append(N2(_holdSeconds.Mean)).Append(" s, max ").Append(N2(_holdMax)).Append(" s at avg m ").Append(N2(_holdAsked.Mean)).Append("; by f:");
                for (int b = 1; b < Bins; b++) h.Append(b == 1 ? " " : ", ").Append(AthleticsMath.PeakBinName(b)).Append(' ').Append(_holdsByBin[b]);
            }
            lines.Add(h.ToString());

            lines.Add("attack rate - pace hold, not held: at full strength " + NotHeld(PaceNotHeld.FullStrength)
                      + ", not needed (his swing and next ready already fill the target) " + NotHeld(PaceNotHeld.NotNeeded)
                      + ", the next attack already readied at the swing's end " + NotHeld(PaceNotHeld.AlreadyReadied)
                      + ", no fresh cycle known yet " + NotHeld(PaceNotHeld.NoReference) + ", stepping back " + NotHeld(PaceNotHeld.SteppingBack)
                      + ", you " + NotHeld(PaceNotHeld.Player) + ", riders " + NotHeld(PaceNotHeld.Rider)
                      + " | not started by the tick: busy with a game job (scripted, an object, a ladder, detached, mounted) " + Refused(PaceRefusal.Busy)
                      + ", NoAttack already set by the game " + Refused(PaceRefusal.AlreadyNoAttack) + ", not AI-controlled " + Refused(PaceRefusal.NotAi)
                      + ", gone " + Refused(PaceRefusal.Gone) + ", too late (his time nearly up, or his next blow already begun) " + Refused(PaceRefusal.TooLate) + ", per-tick cap " + Refused(PaceRefusal.TickBudget)
                      + ", the engine did not take NoAttack " + Refused(PaceRefusal.EngineIgnored) + ", error " + Refused(PaceRefusal.Error));

            lines.Add("attack rate - pace hold ends: time up " + Ended(PaceEnd.TimeUp) + ", a swing started anyway " + Ended(PaceEnd.SwingStarted)
                      + " (must be about 0 - NoAttack holds swings), switched off " + Ended(PaceEnd.SwitchedOff) + ", left the field " + Ended(PaceEnd.LeftField)
                      + ", mission end " + Ended(PaceEnd.MissionEnd) + ", you took him " + Ended(PaceEnd.PlayerControl) + ", mounted " + Ended(PaceEnd.Mounted)
                      + ", error " + Ended(PaceEnd.Error) + " | NoAttack cleared by us " + Released(PaceRelease.ClearedByUs) + ", already cleared by the game "
                      + Released(PaceRelease.ClearedByGame) + ", a game job on him at the end (left alone, cleared once free: " + ClearedAfterWaiting + ") "
                      + Released(PaceRelease.Waiting) + ", still held at mission end " + HeldAtMissionEnd + " | the next ready came avg "
                      + (_nextReady.Count > 0 ? N2(_nextReady.Mean) + " s after a hold ended (n " + _nextReady.Count + ") - near 0 = the hold set his rhythm" : "n/a (no ready after a hold)"));

            var g = new StringBuilder("attack rate - guard by f (melee hits on fighters on foot that were blocked or parried; blocking is never slowed - tired men must not block less):");
            for (int b = 0; b < Bins; b++)
                g.Append(b == 0 ? " " : " | ").Append(AthleticsMath.PeakBinName(b)).Append(' ').Append(Share(_hitsBlocked[b], _hitsTaken[b]));
            g.Append(" | while held by the pace hold ").Append(Share(_hitsBlocked[Bins], _hitsTaken[Bins]));
            lines.Add(g.ToString());
            return lines;
        }

        private void GroupLines(List<string> lines, AttackKind kind, bool player)
        {
            int g = Group(kind, player);
            string head = "attack rate, " + (kind == AttackKind.Melee ? "melee" : "ranged") + ", " + (player ? "you" : "AI");
            bool any = false;
            for (int b = 0; b < Bins && !any; b++)
            {
                if (_cycles[g, b].Count > 0) any = true;
                for (int p = 0; p < Phases && !any; p++) any = _phases[g, b, p].Count > 0;
            }
            if (!any)
            {
                lines.Add(head + ": no attacks measured");
                return;
            }

            double fresh = FreshCycle(kind, player);
            int judged = 0, onTarget = 0;
            var verdicts = new StringBuilder();
            for (int b = 0; b < Bins; b++)
            {
                bool bandAny = _cycles[g, b].Count > 0;
                for (int p = 0; p < Phases && !bandAny; p++) bandAny = _phases[g, b, p].Count > 0;
                if (!bandAny) continue;

                var sb = new StringBuilder(head).Append(", ").Append(AthleticsMath.PeakBinName(b)).Append(':');
                if (kind == AttackKind.Melee)
                {
                    sb.Append(" wind-up ").Append(Phase(g, b, AttackPhase.WindUp)).Append(" + held ").Append(Phase(g, b, AttackPhase.Held))
                      .Append(", swing ").Append(Phase(g, b, AttackPhase.Release)).Append(" (clean, hit nothing ").Append(Phase(g, b, AttackPhase.CleanRelease))
                      .Append("), recoil after a block ").Append(Phase(g, b, AttackPhase.Recoil)).Append(", pause ").Append(Phase(g, b, AttackPhase.Pause));
                }
                else
                {
                    sb.Append(" draw ").Append(Phase(g, b, AttackPhase.WindUp)).Append(" + aim ").Append(Phase(g, b, AttackPhase.Held))
                      .Append(", loose ").Append(Phase(g, b, AttackPhase.Release)).Append(", reload ").Append(Phase(g, b, AttackPhase.Reload))
                      .Append(", pause ").Append(Phase(g, b, AttackPhase.Pause));
                }

                var c = _cycles[g, b];
                sb.Append(" | cycle ").Append(c.Count > 0 ? N2(c.Mean) + " s" : "n/a").Append(" (n ").Append(c.Count).Append(')');
                if (c.Count > 0) sb.Append(", m ").Append(N2(_asked[g, b].Mean));
                if (b == 0)
                {
                    sb.Append(double.IsNaN(fresh)
                        ? " - the fresh reference needs " + AttackRateMath.MinFreshCycles + " cycles at the peak"
                        : " - the fresh reference");
                }
                else
                {
                    double target = TargetCycle(kind, player, b);
                    double ratio = Ratio(kind, player, b);
                    if (double.IsNaN(target)) sb.Append(" - no fresh reference, no verdict");
                    else if (double.IsNaN(ratio)) sb.Append(" → target ").Append(N2(target)).Append(" s - too few cycles for a verdict (need ").Append(AttackRateMath.MinBandCycles).Append(')');
                    else
                    {
                        string word = AttackRateMath.Verdict(ratio);
                        sb.Append(" → target ").Append(N2(target)).Append(" s: ").Append(P0(ratio)).Append(" - ").Append(word);
                        judged++;
                        if (word == "on target") onTarget++;
                        verdicts.Append(verdicts.Length == 0 ? "" : ", ").Append(AthleticsMath.PeakBinName(b)).Append(' ').Append(P0(ratio)).Append(' ').Append(word);
                    }
                }
                lines.Add(sb.ToString());
            }

            if (double.IsNaN(fresh))
                lines.Add(head + " - verdict: no fresh reference (" + _cycles[g, 0].Count + " cycles at the peak, need " + AttackRateMath.MinFreshCycles + ")");
            else if (judged == 0)
                lines.Add(head + " - verdict: fresh cycle " + N2(fresh) + " s, no tired band with " + AttackRateMath.MinBandCycles + " cycles yet");
            else
                lines.Add(head + " - verdict: " + (onTarget == judged ? "ON TARGET" : "OFF TARGET") + " in " + onTarget + " of " + judged + " tired bands (fresh cycle "
                          + N2(fresh) + " s; " + verdicts + ")");
        }

        /// <summary><c>0.45 (x1.40)</c> - the band's mean and, below the peak, its ratio to the peak's; <c>-</c> when none.</summary>
        private string Phase(int g, int bin, AttackPhase phase)
        {
            var s = _phases[g, bin, (int)phase];
            if (s.Count == 0) return "-";
            var peak = _phases[g, 0, (int)phase];
            string text = N2(s.Mean);
            if (bin > 0 && peak.Count > 0 && peak.Mean > 0) text += " (x" + N2(s.Mean / peak.Mean) + ")";
            return text;
        }

        private static string Four(int[] v) => v[0] + " / " + v[1] + ", " + v[2] + " / " + v[3];

        private static string Share(int part, int total) =>
            total == 0 ? "n/a (n 0)" : (100.0 * part / total).ToString("0", CultureInfo.InvariantCulture) + "% (n " + total + ")";

        private static string N0(double v) => v.ToString("0", CultureInfo.InvariantCulture);

        private static string N2(double v) => double.IsNaN(v) ? "n/a" : v.ToString("0.00", CultureInfo.InvariantCulture);

        private static string P0(double share) => (share * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
    }
}

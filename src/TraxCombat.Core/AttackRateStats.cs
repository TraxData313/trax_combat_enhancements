using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace TraxCombat.Core
{
    /// <summary>
    /// One mission's attack-rate numbers (step 5e; step 13: PAUSE ONLY) - the in-game proof that the
    /// attack RATE follows m (DESIGN §2): for melee and ranged, AI fighters and you apart, per f band -
    /// the animation multiplier asked (step 13: x1.00 by default) and every phase's average (wind-up,
    /// held / aim, swing / loose, the recoil after a block, reload, the pause) with its ratio to the
    /// peak's (the animations as the engine really played them); the no-attack TIMER (D, m, the pause
    /// asked) against the measured gap from the attack's end to the next attack's start, and attacks
    /// that started before their timer ended (must be ~0); the cycle (release to release, shot to
    /// shot), m, the target (the group's cycle at the peak × the band's average 1/m) and measured ÷
    /// target with a verdict word; the AI's timers (the pace hold: AttackRatePaceHold); your timer
    /// (AttackRatePlayerTimer: presses swallowed, the held button firing, releases); the AI-decision
    /// recomputes (AttackRateAiDecisions); and the guard by f (tired men must not block less). Main
    /// thread, except <see cref="AddAiScaled"/> (interlocked). Allocation only at construction.
    /// </summary>
    public sealed class AttackRateStats
    {
        public const int Groups = 4; // melee AI, melee you, ranged AI, ranged you
        public const int Phases = 7;
        private const int Bins = AthleticsMath.PeakBins;

        /// <summary>A next attack that started this much before its timer's end still counts as on time
        /// (a frame or two of slack between the engine and our clock). Log plumbing.</summary>
        public const double EarlyToleranceSeconds = 0.05;

        private readonly MeanStd[,,] _phases = new MeanStd[Groups, Bins, Phases];
        private readonly MeanStd[,] _cycles = new MeanStd[Groups, Bins];
        private readonly MeanStd[,] _asked = new MeanStd[Groups, Bins];
        private readonly MeanStd[,] _slowdown = new MeanStd[Groups, Bins]; // 1/m per cycle: target = fresh × this
        private readonly MeanStd[,] _anim = new MeanStd[Groups, Bins];      // the animation multiplier asked, per attack
        private readonly MeanStd[,] _cyclesStep = new MeanStd[Groups, Bins]; // step 16: the counted cycles with a step back inside
        private readonly MeanStd[,] _cyclesPlain = new MeanStd[Groups, Bins]; // …and without one

        // ---- the no-attack timer (step 13), per group and band
        private readonly MeanStd[,] _timerD = new MeanStd[Groups, Bins];
        private readonly MeanStd[,] _timerM = new MeanStd[Groups, Bins];
        private readonly MeanStd[,] _timerAsked = new MeanStd[Groups, Bins];
        private readonly MeanStd[,] _timerFloor = new MeanStd[Groups, Bins]; // step 16: D / m = the attack + its pause - the shortest cycle the spec allows
        private readonly MeanStd[,] _gap = new MeanStd[Groups, Bins];       // the attack's end → the next attack's start
        private readonly MeanStd[,] _after = new MeanStd[Groups, Bins];     // the timer's end → the next attack's start
        private readonly int[,] _early = new int[Groups, Bins];              // the next attack started before the timer ended
        private readonly double[] _timerMax = new double[Groups];

        /// <summary>Cycles left out because f changed bands between their two ends.</summary>
        public readonly int[] CyclesMixed = new int[Groups];

        /// <summary>Cycles, phases and pauses longer than the cap at their m - not a fighting rhythm.</summary>
        public readonly int[] LeftOut = new int[Groups];

        /// <summary>Readies that did not end in an attack (cancelled, feints, interrupted).</summary>
        public readonly int[] ReadiesCancelled = new int[Groups];

        /// <summary>Attacks whose next ready began straight out of the release / recoil / reload (pause 0).</summary>
        public readonly int[] Chained = new int[Groups];

        /// <summary>Cycles with a step back (5d) inside. Until step 16 they were left out (a started step back dropped
        /// the AI timer); since step 16 the timer survives a step back, so they COUNT in their band and are shown
        /// apart. Their pause phase is still left out (it is the step back's, not the AI's own gap).</summary>
        public readonly int[] SteppedBack = new int[Groups];

        // ---- the AI's timer (the pace hold)
        private readonly int[] _holdsByBin = new int[Bins];
        private readonly int[] _holdsByKind = new int[2];
        private int _holdsMounted;
        private int _holdsByInput;
        private int _holdsByFlag;
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

        /// <summary>…of them lifted under a plain scripted frame that lasted beyond
        /// <see cref="AttackRateMath.WaitingMaxSecondsUnderAFrame"/>.</summary>
        public int ClearedUnderAFrame;

        /// <summary>Holds still running when the mission ended.</summary>
        public int HeldAtMissionEnd;

        // ---- your timer (step 13)
        /// <summary>Your presses swallowed during your own attack (a chained blow refused) / during the countdown.</summary>
        public int SwallowedInAttack;

        public int SwallowedInTimer;

        /// <summary>Flashes of the recovery bar started by a swallowed press (FlashBarOnEarlyAttack).</summary>
        public int Flashes;

        /// <summary>Your holds begun at the release's start (the timer was sure to be worth it).</summary>
        public int PlayerEarlyHolds;

        /// <summary>…that ended with the attack without a countdown (it came out below the minimum).</summary>
        public int PlayerEarlyHoldsTooShort;

        /// <summary>Countdowns that ran out with your attack button held (hold-to-attack) and how soon the attack followed.</summary>
        public int PlayerFiredHeld;

        private MeanStd _firedAfter;

        /// <summary>Your attacks (a ready or a release) that started while your hold was on - the gate missed them (must be 0).</summary>
        public int PlayerStartedAnyway;

        private readonly int[] _playerEnds = new int[(int)PlayerTimerEnd.Count];

        /// <summary>Your timer still running when the mission ended (released then).</summary>
        public int PlayerHeldAtMissionEnd;

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
        public bool AddCycle(AttackKind kind, bool player, int bin, double seconds, float asked, bool steppedBack = false)
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
            if (steppedBack)
            {
                SteppedBack[g]++;
                _cyclesStep[g, bin].Add(seconds);
            }
            else
            {
                _cyclesPlain[g, bin].Add(seconds);
            }
            _asked[g, bin].Add(m);
            _slowdown[g, bin].Add(1.0 / m);
            return true;
        }

        /// <summary>The attack animation multiplier the stat decorator applied to one attack (step 13:
        /// max(m, AttackAnimationMinPercent) - 1 by default).</summary>
        public void AddAnimation(AttackKind kind, bool player, int bin, float animation)
        {
            if (bin < 0 || bin >= Bins || float.IsNaN(animation)) return;
            _anim[Group(kind, player), bin].Add(animation);
        }

        public void AddMixed(AttackKind kind, bool player) => CyclesMixed[Group(kind, player)]++;

        public void AddReadyCancelled(AttackKind kind, bool player) => ReadiesCancelled[Group(kind, player)]++;

        public void AddChained(AttackKind kind, bool player) => Chained[Group(kind, player)]++;

        public void AddSteppedBack(AttackKind kind, bool player) => SteppedBack[Group(kind, player)]++;

        public int CycleCountWithStepBack(AttackKind kind, bool player, int bin) => _cyclesStep[Group(kind, player), bin].Count;

        public double CycleMeanWithStepBack(AttackKind kind, bool player, int bin) => _cyclesStep[Group(kind, player), bin].Mean;

        /// <summary>The band's average timer floor D / m (the attack and its pause) - NaN without timers.</summary>
        public double TimerFloorMean(AttackKind kind, bool player, int bin)
        {
            var f = _timerFloor[Group(kind, player), bin];
            return f.Count == 0 ? double.NaN : f.Mean;
        }

        public int PhaseCount(AttackKind kind, bool player, int bin, AttackPhase phase) => _phases[Group(kind, player), bin, (int)phase].Count;

        public double PhaseMean(AttackKind kind, bool player, int bin, AttackPhase phase) => _phases[Group(kind, player), bin, (int)phase].Mean;

        public int CycleCount(AttackKind kind, bool player, int bin) => _cycles[Group(kind, player), bin].Count;

        public double CycleMean(AttackKind kind, bool player, int bin) => _cycles[Group(kind, player), bin].Mean;

        public double AnimationMean(AttackKind kind, bool player, int bin) => _anim[Group(kind, player), bin].Mean;

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

        // ---- the no-attack timer (step 13)

        /// <summary>A timer started (yours, or an AI's hold): the attack's D, m at its end, the pause asked.</summary>
        public void AddTimer(AttackKind kind, bool player, int bin, double duration, float m, double pause)
        {
            if (bin < 0 || bin >= Bins || double.IsNaN(pause)) return;
            int g = Group(kind, player);
            _timerD[g, bin].Add(duration);
            _timerM[g, bin].Add(m);
            _timerAsked[g, bin].Add(pause);
            if (!double.IsNaN(duration) && duration > 0) _timerFloor[g, bin].Add(duration + pause); // D + D (1/m - 1) = D / m
            if (pause > _timerMax[g]) _timerMax[g] = pause;
        }

        /// <summary>The next attack after a timer began <paramref name="gap"/> seconds after the timer's
        /// start (the last attack's end); it had asked <paramref name="asked"/>. Beyond the cycle cap it is
        /// a lull, not a rhythm (left out); shorter than asked by more than the tolerance = it started
        /// DURING the timer (counted - must be ~0). True when it started early.</summary>
        public bool AddNextAttackAfterTimer(AttackKind kind, bool player, int bin, double gap, double asked, float m)
        {
            if (bin < 0 || bin >= Bins || double.IsNaN(gap) || gap < 0) return false;
            int g = Group(kind, player);
            bool early = gap < asked - EarlyToleranceSeconds;
            if (early) _early[g, bin]++;
            if (!AttackRateMath.Countable(kind, gap, m)) return early;
            _gap[g, bin].Add(gap);
            _after[g, bin].Add(Math.Max(0, gap - asked));
            return early;
        }

        public int Timers(AttackKind kind, bool player, int bin) => _timerAsked[Group(kind, player), bin].Count;

        public double TimerAskedMean(AttackKind kind, bool player, int bin) => _timerAsked[Group(kind, player), bin].Mean;

        public double GapMean(AttackKind kind, bool player, int bin) => _gap[Group(kind, player), bin].Mean;

        public int GapCount(AttackKind kind, bool player, int bin) => _gap[Group(kind, player), bin].Count;

        public int StartedEarly(AttackKind kind, bool player, int bin) => _early[Group(kind, player), bin];

        public int StartedEarly(AttackKind kind, bool player)
        {
            int g = Group(kind, player), n = 0;
            for (int b = 0; b < Bins; b++) n += _early[g, b];
            return n;
        }

        // ---- the AI's timer (the pace hold)

        public void AddNotHeld(PaceNotHeld why) => _notHeld[(int)why]++;

        public int NotHeld(PaceNotHeld why) => _notHeld[(int)why];

        public void AddRefused(PaceRefusal why) => _refused[(int)why]++;

        public int Refused(PaceRefusal why) => _refused[(int)why];

        /// <summary>A hold started in band <paramref name="bin"/> at m <paramref name="asked"/>.</summary>
        public void AddHoldStart(int bin, float asked, AttackKind kind = AttackKind.Melee, bool mounted = false, bool byInput = false)
        {
            if (bin >= 0 && bin < Bins) _holdsByBin[bin]++;
            _holdsByKind[(int)kind]++;
            if (mounted) _holdsMounted++;
            if (byInput) _holdsByInput++;
            else _holdsByFlag++;
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

        public int HoldsOf(AttackKind kind) => _holdsByKind[(int)kind];

        public int HoldsMounted => _holdsMounted;

        /// <summary>Step 16: holds by technique - the AI's own input (AttackRatePaceByInput) and the NoAttack flag.</summary>
        public int HoldsByInput => _holdsByInput;

        public int HoldsByFlag => _holdsByFlag;

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

        // ---- your timer

        public void AddPlayerEnd(PlayerTimerEnd why) => _playerEnds[(int)why]++;

        public int PlayerEnded(PlayerTimerEnd why) => _playerEnds[(int)why];

        /// <summary>Your countdown ran out with the button held; your attack began <paramref name="after"/> s later.</summary>
        public void AddFiredHeld(double after)
        {
            PlayerFiredHeld++;
            if (after >= 0 && after <= AttackRateMath.MeleeCycleCapSeconds) _firedAfter.Add(after);
        }

        public double FiredAfterMean => _firedAfter.Mean;

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
            lines.Add("attack rate settings at the end (DESIGN §2 - the attack RATE follows the attack speed m): " + r.Describe()
                      + (switchedDuringBattle ? " - an attack-rate switch CHANGED during this battle: the rows below mix both settings" : string.Empty));
            GroupLines(lines, AttackKind.Melee, player: false);
            GroupLines(lines, AttackKind.Melee, player: true);
            GroupLines(lines, AttackKind.Ranged, player: false);
            GroupLines(lines, AttackKind.Ranged, player: true);

            lines.Add("attack rate - left out: cycles whose two ends fell in different f bands (melee AI / you, ranged AI / you) " + Four(CyclesMixed)
                      + "; longer than " + N0(AttackRateMath.MeleeCycleCapSeconds) + " s ÷ m melee or " + N0(AttackRateMath.RangedCycleCapSeconds)
                      + " s ÷ m ranged (a pause, not a fighting rhythm) " + Four(LeftOut) + "; readies that ended in no attack (cancelled, feints) "
                      + Four(ReadiesCancelled) + "; chained (the next ready straight out of the last attack, pause 0) " + Four(Chained)
                      + "; with a step back inside (COUNTED since step 16 - the AI timer survives the step back; each band shows them apart) " + Four(SteppedBack));

            lines.Add("attack rate - your timer (AttackRatePlayerTimer " + (r.PlayerTimer ? "on" : "off") + " at the end): "
                      + TimerCount(true) + "; your presses swallowed: " + SwallowedInAttack + " during your own attack (no chained blow), " + SwallowedInTimer
                      + " during the countdown - the recovery bar flashed " + Flashes + "x; the button held through the end " + PlayerFiredHeld + "x"
                      + (_firedAfter.Count > 0 ? ", your attack began avg " + N2(_firedAfter.Mean) + " s after (n " + _firedAfter.Count + ") - near 0 = hold-to-attack works" : string.Empty)
                      + "; attacks that started while held anyway: " + PlayerStartedAnyway + " (must be 0 - the input gate missed them)"
                      + "; holds begun at your swing's start " + PlayerEarlyHolds + " (ended with no countdown, below " + N1(AttackTimerMath.MinTimerSeconds) + " s: " + PlayerEarlyHoldsTooShort + ")"
                      + "; ended early: switched off " + PlayerEnded(PlayerTimerEnd.SwitchedOff) + ", not you any more " + PlayerEnded(PlayerTimerEnd.NotYou)
                      + ", mission end " + PlayerEnded(PlayerTimerEnd.MissionEnd) + " (still running at the end, released: " + PlayerHeldAtMissionEnd + ")");

            lines.Add("attack rate - AI decisions (AttackRateAiDecisions " + (r.AiDecisions ? "on" : "off") + " at the end): scaled in " + AiScaled
                      + " recomputes - the chance to attack and to riposte x m, to loose x m, the aim before a shot ÷ m (off by default since step 13: on top of the timer it double-counts)");

            var h = new StringBuilder("attack rate - AI timer (AttackRatePaceHold ").Append(r.PaceHold ? "on" : "off")
                .Append(" at the end; technique at the end: ").Append(r.PaceTechnique())
                .Append("; after each attack of a tired AI fighter, melee and ranged, on foot and mounted): ").Append(Holds).Append(" holds");
            if (Holds > 0)
            {
                h.Append(" (by input ").Append(_holdsByInput).Append(", by NoAttack ").Append(_holdsByFlag)
                 .Append("; melee ").Append(HoldsOf(AttackKind.Melee)).Append(", ranged ").Append(HoldsOf(AttackKind.Ranged)).Append(", mounted ").Append(_holdsMounted)
                 .Append("), avg ").Append(N2(_holdSeconds.Mean)).Append(" s, max ").Append(N2(_holdMax)).Append(" s at avg m ").Append(N2(_holdAsked.Mean)).Append("; by f:");
                for (int b = 1; b < Bins; b++) h.Append(b == 1 ? " " : ", ").Append(AthleticsMath.PeakBinName(b)).Append(' ').Append(_holdsByBin[b]);
            }
            lines.Add(h.ToString());

            lines.Add("attack rate - AI timer, not held: at full strength " + NotHeld(PaceNotHeld.FullStrength)
                      + ", not needed (below " + N1(AttackTimerMath.MinTimerSeconds) + " s) " + NotHeld(PaceNotHeld.NotNeeded)
                      + ", the next attack already readied at the attack's end " + NotHeld(PaceNotHeld.AlreadyReadied)
                      + ", the attack's length not measured " + NotHeld(PaceNotHeld.NoDuration)
                      + ", covered by a scripted step back (a NoAttack hold waiting for the step to end whose time ran out first) " + NotHeld(PaceNotHeld.CoveredByStepBack)
                      + " | not started by the tick: busy with a game job (scripted, an object, a ladder, detached) " + Refused(PaceRefusal.Busy)
                      + ", NoAttack already set by the game " + Refused(PaceRefusal.AlreadyNoAttack) + ", not AI-controlled " + Refused(PaceRefusal.NotAi)
                      + ", gone " + Refused(PaceRefusal.Gone) + ", too late (his time nearly up, or his next attack already begun) " + Refused(PaceRefusal.TooLate) + ", per-tick cap " + Refused(PaceRefusal.TickBudget)
                      + ", the engine did not take NoAttack " + Refused(PaceRefusal.EngineIgnored) + ", error " + Refused(PaceRefusal.Error));

            lines.Add("attack rate - AI timer ends: time up " + Ended(PaceEnd.TimeUp) + ", an attack started anyway " + Ended(PaceEnd.AttackStarted)
                      + " (must be about 0 - the hold stops attacks), switched off " + Ended(PaceEnd.SwitchedOff) + ", left the field " + Ended(PaceEnd.LeftField)
                      + ", mission end " + Ended(PaceEnd.MissionEnd) + ", you took him " + Ended(PaceEnd.PlayerControl)
                      + ", error " + Ended(PaceEnd.Error) + " | lifted by us " + Released(PaceRelease.ClearedByUs) + ", NoAttack already cleared by the game "
                      + Released(PaceRelease.ClearedByGame) + ", a game job on him at the end (left alone, cleared once free: " + ClearedAfterWaiting
                      + ", of them under a long scripted frame: " + ClearedUnderAFrame + ") "
                      + Released(PaceRelease.Waiting) + ", still held at mission end " + HeldAtMissionEnd + " | the next ready came avg "
                      + (_nextReady.Count > 0 ? N2(_nextReady.Mean) + " s after a hold ended (n " + _nextReady.Count + ") - the AI's own re-decision after the hold lifts (NoAttack cost 1-3 s; by input he readies at once if he still wants to)" : "n/a (no ready after a hold)"));

            var g = new StringBuilder("attack rate - guard by f (melee hits on fighters on foot that were blocked or parried; blocking is never slowed - tired men must not block less):");
            for (int b = 0; b < Bins; b++)
                g.Append(b == 0 ? " " : " | ").Append(AthleticsMath.PeakBinName(b)).Append(' ').Append(Share(_hitsBlocked[b], _hitsTaken[b]));
            g.Append(" | while held by the AI timer ").Append(Share(_hitsBlocked[Bins], _hitsTaken[Bins]));
            lines.Add(g.ToString());
            return lines;
        }

        /// <summary>"12 timers (melee 10, ranged 2), avg asked 0.62 s, max 1.90 s" for you or the AI.</summary>
        private string TimerCount(bool player)
        {
            int melee = 0, ranged = 0;
            for (int b = 0; b < Bins; b++)
            {
                melee += _timerAsked[Group(AttackKind.Melee, player), b].Count;
                ranged += _timerAsked[Group(AttackKind.Ranged, player), b].Count;
            }
            if (melee + ranged == 0) return "0 timers";
            double sum = 0;
            for (int b = 0; b < Bins; b++)
            {
                var mm = _timerAsked[Group(AttackKind.Melee, player), b];
                var rr = _timerAsked[Group(AttackKind.Ranged, player), b];
                if (mm.Count > 0) sum += mm.Mean * mm.Count;
                if (rr.Count > 0) sum += rr.Mean * rr.Count;
            }
            double max = Math.Max(_timerMax[Group(AttackKind.Melee, player)], _timerMax[Group(AttackKind.Ranged, player)]);
            return (melee + ranged) + " timers (melee " + melee + ", ranged " + ranged + "), avg asked " + N2(sum / (melee + ranged)) + " s, max " + N2(max) + " s";
        }

        private void GroupLines(List<string> lines, AttackKind kind, bool player)
        {
            int g = Group(kind, player);
            string head = "attack rate, " + (kind == AttackKind.Melee ? "melee" : "ranged") + ", " + (player ? "you" : "AI");
            bool any = false;
            for (int b = 0; b < Bins && !any; b++)
            {
                if (_cycles[g, b].Count > 0 || _timerAsked[g, b].Count > 0) any = true;
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
                bool bandAny = _cycles[g, b].Count > 0 || _timerAsked[g, b].Count > 0;
                for (int p = 0; p < Phases && !bandAny; p++) bandAny = _phases[g, b, p].Count > 0;
                if (!bandAny) continue;

                var sb = new StringBuilder(head).Append(", ").Append(AthleticsMath.PeakBinName(b)).Append(": animations asked ")
                    .Append(_anim[g, b].Count > 0 ? "x" + N2(_anim[g, b].Mean) : "-").Append(" -");
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
                sb.Append(" | cycle ").Append(c.Count > 0 ? N2(c.Mean) + " s" : "n/a").Append(" (n ").Append(c.Count);
                if (_cyclesStep[g, b].Count > 0)
                    sb.Append("; with a step back inside ").Append(N2(_cyclesStep[g, b].Mean)).Append(" s n ").Append(_cyclesStep[g, b].Count)
                      .Append(", without ").Append(_cyclesPlain[g, b].Count > 0 ? N2(_cyclesPlain[g, b].Mean) + " s" : "-").Append(" n ").Append(_cyclesPlain[g, b].Count);
                sb.Append(')');
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

                if (_timerAsked[g, b].Count > 0 || _early[g, b] > 0)
                {
                    var t = new StringBuilder(head).Append(", ").Append(AthleticsMath.PeakBinName(b)).Append(" - timer: ").Append(_timerAsked[g, b].Count)
                        .Append(" (D avg ").Append(N2(_timerD[g, b].Mean)).Append(" s at m ").Append(N2(_timerM[g, b].Mean))
                        .Append(" → asked avg ").Append(N2(_timerAsked[g, b].Mean)).Append(" s = D x (1/m - 1))");
                    var gap = _gap[g, b];
                    if (gap.Count > 0)
                        t.Append("; measured: the next attack began avg ").Append(N2(gap.Mean)).Append(" s after the attack's end (n ").Append(gap.Count)
                         .Append("), ").Append(N2(_after[g, b].Mean)).Append(" s after the timer ended");
                    else
                        t.Append("; no next attack measured after it");
                    t.Append("; started before the timer ended: ").Append(_early[g, b]).Append(" (must be 0)");
                    var floor = _timerFloor[g, b];
                    if (floor.Count > 0)
                    {
                        // step 16: the spec's own floor - an attack of D and its pause D (1/m - 1) take D / m; a band whose
                        // cycles fall well below it was not really held (before step 16 at empty: a step back dropped the hold)
                        t.Append("; the timer's floor D/m avg ").Append(N2(floor.Mean)).Append(" s - the cycle vs it: ");
                        t.Append(c.Count > 0 ? P0(c.Mean / floor.Mean) + " (n " + c.Count + ")" : "n/a");
                        if (_cyclesStep[g, b].Count > 0)
                            t.Append(", with a step back inside ").Append(P0(_cyclesStep[g, b].Mean / floor.Mean)).Append(", without ")
                             .Append(_cyclesPlain[g, b].Count > 0 ? P0(_cyclesPlain[g, b].Mean / floor.Mean) : "-");
                        t.Append(" (at least ~100% = held as the spec asks)");
                    }
                    lines.Add(t.ToString());
                }
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

        private static string N1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        private static string N2(double v) => double.IsNaN(v) ? "n/a" : v.ToString("0.00", CultureInfo.InvariantCulture);

        private static string P0(double share) => (share * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
    }
}

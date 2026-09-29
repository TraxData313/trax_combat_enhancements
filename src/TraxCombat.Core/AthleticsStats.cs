using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace TraxCombat.Core
{
    /// <summary>
    /// One mission's Athletics numbers for the <c>[summary]</c> block - built so ONE playtest run
    /// proves or disproves every rule of DESIGN §2 (CLAUDE.md, logging): the pools the Athletics
    /// skill gave, blows by kind and by riders vs on foot, the detection cross-checks (releases vs
    /// hits vs shots), kicks and shield bashes (step 18: charged apart, where they were seen), blocks (step 22: what
    /// the defenders paid, by kind and by who), what was
    /// free, exhaustions and the peak zone, fighter-time by f, heroes and
    /// leaders, the player, the formations, the health cap, regen by effort, the measured run speeds
    /// binned by f (does the engine honour the curve? - the attack timings are AttackRateStats', step
    /// 5e), the walk/run speed
    /// ratio that tunes WalkEffortFraction, the recomputes, the tick cost and errors. A new
    /// instance per mission.
    ///
    /// Main thread only (every caller is a mission tick or an engine callback marked not
    /// multi-thread callable), except <see cref="AddDecoratorScaled"/> which is interlocked.
    /// Plain counters are public fields on purpose: the module just counts.
    /// </summary>
    public sealed class AthleticsStats
    {
        public const int ActionSlots = 64;
        public const int EffortBins = 11; // tenths 0.0-0.1 … 0.9-1.0, then above 1
        public const int SkillSlots = 1024; // skills above are counted in the last slot

        private readonly int[] _charged = new int[5];
        private readonly int[] _outsideActions = new int[ActionSlots];
        private readonly double[] _effortSeconds = new double[EffortBins];
        private readonly int[] _skills = new int[SkillSlots];
        private readonly double[] _peakTime = new double[AthleticsMath.PeakBins];
        private readonly Dictionary<string, int> _errors = new Dictionary<string, int>(StringComparer.Ordinal);
        private int _decoratorAttack;
        private int _decoratorRun;
        private int _decoratorMount;

        // ---- pools (the Athletics skill of every fighter tracked, for the distribution at the end)
        public int FightersTracked;
        public int SkillsUnknown;
        public readonly List<KeyValuePair<string, int>> LeaderSkills = new List<KeyValuePair<string, int>>();

        // ---- blows
        public int ChargedMounted;
        public int ChargedOnFoot;
        public double PointsSpent;

        // ---- detection
        public int MeleeReleasesSeen;
        public int MeleeReleasesMounted;
        public int ShotsSeen;
        public int ExtraProjectiles;
        public int RangedReleasesPolled;
        public int MeleeHitsInRelease;
        public int MeleeHitsOutsideRelease;
        public int MeleeHitsMounted;
        public int MeleeHitsOnFoot;
        public int LandedMeleeByTimeFallback;

        // ---- kicks and shield bashes (step 18: charged apart from blows, CostPerKickOrBash)
        /// <summary>New kicks / bashes the poll saw start (<see cref="KickBashTracker.Observe"/>) - each is
        /// decided once: charged, or free (cost 0).</summary>
        public int KicksSeen;
        public int BashesSeen;

        /// <summary>…of them first seen on channel 1 (the upper body) / channel 0 (the whole body).</summary>
        public int KicksSeenUpper;
        public int KicksSeenLower;
        public int BashesSeenUpper;
        public int BashesSeenLower;

        /// <summary>Kick / bash hits (IsAlternativeAttack) by tracked fighters, charged or not.</summary>
        public int KickOrBashHits;

        public int KicksCharged;
        public int BashesCharged;

        /// <summary>Charged at the hit: no channel showed a kick or bash (kind unknown).</summary>
        public int KickOrBashChargedAtHit;
        public int KickOrBashChargedMounted;
        public double KickOrBashPoints;

        /// <summary>Decided free: CostPerKickOrBash 0 (Athletics off: the poll does not run).</summary>
        public int KickOrBashFree;

        public int KickOrBashCharged => KicksCharged + BashesCharged + KickOrBashChargedAtHit;

        // ---- blocks (step 22: the DEFENDER pays for a melee blow he blocks - apart from blows and kicks)
        private readonly int[] _blocks = new int[BlockMath.KindCount];
        private readonly double[] _blockPoints = new double[BlockMath.KindCount];

        /// <summary>Blocks paid by you / by AI heroes (lords, companions) / by every other AI fighter, and their points.</summary>
        public int BlocksByYou;
        public int BlocksByHeroes;
        public int BlocksByOthers;
        public double BlockPointsYou;
        public double BlockPointsHeroes;
        public double BlockPointsOthers;
        public int BlocksMounted;

        /// <summary>Decided free: the kind's cost 0.</summary>
        public int BlocksFree;

        /// <summary>Blocks seen while Athletics (or the mod) was off - never charged.</summary>
        public int BlocksWhileOff;

        /// <summary>The same blocked blow seen again (a second collision of the same swing) - not charged twice.</summary>
        public int BlocksSameBlow;

        /// <summary>Missiles a tracked fighter's shield stopped - free (DESIGN), counted to show it.</summary>
        public int MissilesBlockedByShield;

        public int BlocksCharged => _blocks[1] + _blocks[2] + _blocks[3];

        public double BlockPoints => _blockPoints[1] + _blockPoints[2] + _blockPoints[3];

        public int Blocks(BlockKind kind) => _blocks[(int)kind];

        public double BlockPointsOf(BlockKind kind) => _blockPoints[(int)kind];

        // ---- free / not charged
        public int CouchedWithinBlowTime;
        public int AttacksWhileOff;
        public int ReleasesAwaitingHit;
        public int ShotsAwaitingHit;

        // ---- transitions
        public int ExhaustionsEntered;
        public int ExhaustionsLeft;
        public int PeakLeft;
        public int PeakEntered;

        // ---- recomputes (UpdateAgentProperties)
        public int FighterRecomputes;
        public int HorseRecomputes;
        public int RecomputesDeferred;

        // ---- health cap
        public int HealthCuts;
        public double HealthCutPoints;
        public double HealthCutMaxPoints;

        // ---- regen by effort
        public double RegenWalkSeconds;
        public double RegenFasterSeconds;
        public double RegenFasterRateSeconds; // Σ rate multiplier × seconds while faster than a walk
        public int RefillsToFull;
        public int RefillsToHealthCap;
        public double MaxEffort;

        // ---- the refill curve (step 14): refills from EMPTY to the peak line with no blow between
        public int EmptyToPeakRuns;
        public double EmptyToPeakSecondsTotal;
        public double EmptyToPeakSecondsMin = double.MaxValue;
        public double EmptyToPeakSecondsMax;

        // ---- the run-speed measurements binned by f (step 5c's curves; the attack timings moved to
        // AttackRateStats in step 5e)
        public readonly RunSpeedCheck FootRun = new RunSpeedCheck();
        public readonly RunSpeedCheck HorseRun = new RunSpeedCheck();

        // ---- cost
        public long Ticks;
        public long PolledTotal;
        public int PolledMax;
        public double TickMsTotal;
        public double TickMsMax;

        // ---- heroes, leaders, the player (the module fills the "lowest" and player values before the summary)
        public int HeroesFlagged;
        public readonly List<string> LeaderNames = new List<string>();
        public string? LowestHeroName;
        public double LowestHeroPoints = double.NaN;
        public double LowestHeroPool;
        public bool PlayerSeen;
        public int PlayerSkill;
        public int PlayerBlows;
        public int PlayerExhaustions;
        public double PlayerLowestPoints;
        public double PlayerPool;

        // ---- walk vs run (tunes WalkEffortFraction)
        public MeanStd FootWalk;
        public MeanStd FootTop;
        public MeanStd HorseWalk;
        public MeanStd HorseTop;

        // ------------------------------------------------------------------ counting

        /// <summary>A fighter starts being tracked, with his Athletics skill (0 = unknown / none).</summary>
        public void AddFighter(int skill, bool skillKnown)
        {
            FightersTracked++;
            if (!skillKnown) SkillsUnknown++;
            int s = skill < 0 ? 0 : skill >= SkillSlots ? SkillSlots - 1 : skill;
            _skills[s]++;
        }

        /// <summary>One charged blow; <paramref name="points"/> = what it really drained (0 for a swing
        /// on an empty pool).</summary>
        public void AddCharge(BlowKind kind, bool mounted, double points)
        {
            _charged[(int)kind]++;
            if (mounted) ChargedMounted++;
            else ChargedOnFoot++;
            PointsSpent += points;
        }

        public int Charged(BlowKind kind) => _charged[(int)kind];

        /// <summary>One charged kick or shield bash (step 18) - apart from the blows; <paramref name="points"/> =
        /// what it really drained. <paramref name="kind"/> None = charged at its hit (no channel showed it).</summary>
        public void AddKickOrBashCharge(KickBashKind kind, bool mounted, double points)
        {
            if (kind == KickBashKind.Kick) KicksCharged++;
            else if (kind == KickBashKind.Bash) BashesCharged++;
            else KickOrBashChargedAtHit++;
            if (mounted) KickOrBashChargedMounted++;
            KickOrBashPoints += points;
            PointsSpent += points;
        }

        /// <summary>One blocked blow the DEFENDER paid for (step 22); <paramref name="points"/> = what it really drained.
        /// <paramref name="you"/> the player, else <paramref name="hero"/> an AI hero, else a common AI fighter.</summary>
        public void AddBlockCharge(BlockKind kind, bool you, bool hero, bool mounted, double points)
        {
            int k = (int)kind;
            if (k <= 0 || k >= BlockMath.KindCount) return;
            _blocks[k]++;
            _blockPoints[k] += points;
            if (you)
            {
                BlocksByYou++;
                BlockPointsYou += points;
            }
            else if (hero)
            {
                BlocksByHeroes++;
                BlockPointsHeroes += points;
            }
            else
            {
                BlocksByOthers++;
                BlockPointsOthers += points;
            }
            if (mounted) BlocksMounted++;
            PointsSpent += points;
        }

        /// <summary>A kick or bash the poll saw start, on channel 0 (<paramref name="lowerChannel"/>) or 1.</summary>
        public void AddKickOrBashSeen(KickBashKind kind, bool lowerChannel)
        {
            if (kind == KickBashKind.Kick)
            {
                KicksSeen++;
                if (lowerChannel) KicksSeenLower++;
                else KicksSeenUpper++;
            }
            else if (kind == KickBashKind.Bash)
            {
                BashesSeen++;
                if (lowerChannel) BashesSeenLower++;
                else BashesSeenUpper++;
            }
        }

        public int ChargedTotal
        {
            get
            {
                int n = 0;
                foreach (int c in _charged) n += c;
                return n;
            }
        }

        /// <summary>A melee hit that arrived while the attacker was NOT in a release we had counted -
        /// recorded with the action type he was in (the proof, or not, that every swing shows up as
        /// a release on channel 1).</summary>
        public void AddHitOutsideRelease(int actionCode)
        {
            MeleeHitsOutsideRelease++;
            if (actionCode >= 0 && actionCode < ActionSlots) _outsideActions[actionCode]++;
        }

        /// <summary>One regen sample: <paramref name="seconds"/> spent at <paramref name="effort"/>
        /// (speed ÷ current top speed) while refilling.</summary>
        public void AddEffort(double effort, double seconds)
        {
            if (double.IsNaN(effort) || effort < 0 || !(seconds > 0)) return;
            int bin = effort > 1.0 ? EffortBins - 1 : Math.Min(EffortBins - 2, (int)(effort * 10));
            _effortSeconds[bin] += seconds;
            if (effort > MaxEffort) MaxEffort = effort;
        }

        /// <summary>A refill from empty reached the peak line after <paramref name="seconds"/> of refill
        /// (<see cref="RegenOutcome.EmptyToPeakSeconds"/>).</summary>
        public void AddEmptyToPeak(double seconds)
        {
            if (!(seconds > 0)) return;
            EmptyToPeakRuns++;
            EmptyToPeakSecondsTotal += seconds;
            if (seconds < EmptyToPeakSecondsMin) EmptyToPeakSecondsMin = seconds;
            if (seconds > EmptyToPeakSecondsMax) EmptyToPeakSecondsMax = seconds;
        }

        public double EffortSeconds
        {
            get
            {
                double n = 0;
                foreach (double e in _effortSeconds) n += e;
                return n;
            }
        }

        /// <summary>A fighter spent <paramref name="seconds"/> at f (for the share of fighter-time in
        /// each f bin, the peak zone first).</summary>
        public void AddPeakTime(double peakShare, double seconds)
        {
            if (!(seconds > 0)) return;
            _peakTime[AthleticsMath.PeakBin(peakShare)] += seconds;
        }

        public double PeakTime(int bin) => _peakTime[bin];

        /// <summary>A wound cut <paramref name="points"/> off a fighter's Athletics (the health cap).</summary>
        public void AddHealthCut(double points)
        {
            HealthCuts++;
            HealthCutPoints += points;
            if (points > HealthCutMaxPoints) HealthCutMaxPoints = points;
        }

        public void AddTick(int polled, double ms)
        {
            Ticks++;
            PolledTotal += polled;
            if (polled > PolledMax) PolledMax = polled;
            TickMsTotal += ms;
            if (ms > TickMsMax) TickMsMax = ms;
        }

        /// <summary>The stat decorator applied a penalty in one recompute (any thread): attack speed,
        /// run speed on foot, or a horse's speed.</summary>
        public void AddDecoratorScaled(bool attack, bool run, bool mount)
        {
            if (attack) Interlocked.Increment(ref _decoratorAttack);
            if (run) Interlocked.Increment(ref _decoratorRun);
            if (mount) Interlocked.Increment(ref _decoratorMount);
        }

        public int DecoratorAttack => Volatile.Read(ref _decoratorAttack);

        public int DecoratorRun => Volatile.Read(ref _decoratorRun);

        public int DecoratorMount => Volatile.Read(ref _decoratorMount);

        /// <summary>Counts a caught exception at <paramref name="site"/>. True the FIRST time per site -
        /// log that one with its stack, count the rest.</summary>
        public bool AddError(string site)
        {
            lock (_errors)
            {
                _errors.TryGetValue(site, out int n);
                _errors[site] = n + 1;
                return n == 0;
            }
        }

        public int Errors
        {
            get
            {
                lock (_errors)
                {
                    int n = 0;
                    foreach (var pair in _errors) n += pair.Value;
                    return n;
                }
            }
        }

        // ------------------------------------------------------------------ pools

        /// <summary>The pools of every fighter tracked, with the given (live) rules: min, mean, max and how
        /// many came from the floor. False when nobody was tracked.</summary>
        public bool Pools(in AthleticsRules r, out double min, out double mean, out double max, out int atFloor)
        {
            min = double.MaxValue;
            max = 0;
            mean = 0;
            atFloor = 0;
            int n = 0;
            double sum = 0;
            for (int s = 0; s < SkillSlots; s++)
            {
                int c = _skills[s];
                if (c == 0) continue;
                double pool = AthleticsMath.PoolPointsForSkill(in r, s);
                n += c;
                sum += pool * c;
                if (pool < min) min = pool;
                if (pool > max) max = pool;
                if (AthleticsMath.IsAtFloor(in r, s)) atFloor += c;
            }
            if (n == 0)
            {
                min = max = mean = double.NaN;
                return false;
            }
            mean = sum / n;
            return true;
        }

        // ------------------------------------------------------------------ the summary text

        /// <summary>
        /// The <c>[summary]</c> Athletics lines, plain words (docs/PLAYTEST.md §3 quotes them).
        /// <paramref name="actionName"/> names an engine action code; <paramref name="formations"/> are
        /// the player's formations at the end (name, stats).
        /// </summary>
        public List<string> SummaryLines(in AthleticsRules r, Func<int, string> actionName, IList<KeyValuePair<string, FormationAthleticsStats>> formations)
        {
            var lines = new List<string>();
            lines.Add("Athletics settings at the end: " + DescribeRules(in r));

            lines.Add(PoolsLine(in r));

            lines.Add("Athletics blows charged: " + ChargedTotal
                + " (melee swings " + Charged(BlowKind.Melee) + ", shots/throws " + Charged(BlowKind.Ranged)
                + ", couched/braced hits " + Charged(BlowKind.Couched) + ", landed-only swings " + Charged(BlowKind.LandedMelee)
                + ", landed-only shots " + Charged(BlowKind.LandedRanged) + ") - by riders " + ChargedMounted + ", on foot " + ChargedOnFoot
                + "; + kicks/bashes " + KickOrBashCharged + " and blocks " + BlocksCharged + " (not blows - their own lines)"
                + "; Athletics spent " + N0(PointsSpent) + " points (kicks/bashes " + N0(KickOrBashPoints) + ", blocks " + N0(BlockPoints) + " of them)");

            var sb = new StringBuilder("Athletics detection: melee releases seen ").Append(MeleeReleasesSeen)
                .Append(" (mounted ").Append(MeleeReleasesMounted).Append(") | shots seen ").Append(ShotsSeen)
                .Append(" (+").Append(ExtraProjectiles).Append(" extra projectiles of the same shot ignored) | ranged releases seen by the poll ")
                .Append(RangedReleasesPolled).Append(" | melee hits by fighters ").Append(MeleeHitsInRelease + MeleeHitsOutsideRelease)
                .Append(" (on foot ").Append(MeleeHitsOnFoot).Append(", mounted ").Append(MeleeHitsMounted).Append("): during a counted release ")
                .Append(MeleeHitsInRelease).Append(", outside one ").Append(MeleeHitsOutsideRelease);
            if (MeleeHitsOutsideRelease > 0)
            {
                sb.Append(" [in action:");
                bool first = true;
                for (int i = 0; i < _outsideActions.Length; i++)
                {
                    if (_outsideActions[i] == 0) continue;
                    sb.Append(first ? " " : ", ").Append(actionName(i)).Append(' ').Append(_outsideActions[i]);
                    first = false;
                }
                sb.Append(']');
            }
            if (LandedMeleeByTimeFallback > 0) sb.Append(" | landed swings charged by the time fallback ").Append(LandedMeleeByTimeFallback);
            lines.Add(sb.ToString());

            lines.Add("Athletics kicks/bashes charged " + KickOrBashCharged + " (" + N1(KickOrBashPoints) + " points; by riders " + KickOrBashChargedMounted
                + "): kicks " + KicksCharged + ", shield bashes " + BashesCharged + ", at their hit with no kick or bash seen " + KickOrBashChargedAtHit
                + " | seen starting: kicks " + KicksSeen + " (channel 1 " + KicksSeenUpper + ", channel 0 " + KicksSeenLower + "), shield bashes " + BashesSeen
                + " (channel 1 " + BashesSeenUpper + ", channel 0 " + BashesSeenLower + "); kick/bash hits " + KickOrBashHits
                + "; free (CostPerKickOrBash 0) " + KickOrBashFree + "; each charged once, when it starts; never an attack pause");

            lines.Add(BlocksLine());

            lines.Add("Athletics free (never charged): couched hits within one blow-length of the last " + CouchedWithinBlowTime + ", attacks while Athletics was off " + AttacksWhileOff
                + ", releases / shots waiting for a landed hit (misses cost: no) " + ReleasesAwaitingHit + " / " + ShotsAwaitingHit);

            lines.Add("Athletics exhaustions (empty, f 0): " + ExhaustionsEntered + " entered, " + ExhaustionsLeft + " left; the peak zone: left "
                + PeakLeft + " times (a blow took a fighter below his line), re-entered " + PeakEntered + " times (by refill)");

            double time = 0;
            for (int b = 0; b < AthleticsMath.PeakBins; b++) time += _peakTime[b];
            if (time <= 0)
            {
                lines.Add("Athletics fighter-time by f (the share of his peak line left): no fighter-time recorded");
            }
            else
            {
                var t = new StringBuilder("Athletics fighter-time by f (the share of his peak line left): ");
                for (int b = 0; b < AthleticsMath.PeakBins; b++)
                    t.Append(b == 0 ? "" : ", ").Append(AthleticsMath.PeakBinName(b)).Append(' ').Append(P1(_peakTime[b] / time));
                t.Append(" of ").Append(N0(time)).Append(" fighter-seconds");
                lines.Add(t.ToString());
            }

            string heroes = "Athletics heroes: " + HeroesFlagged + " flagged, " + LeaderNames.Count + " party leader" + (LeaderNames.Count == 1 ? "" : "s");
            if (LeaderNames.Count > 0)
            {
                const int shown = 20;
                heroes += " (" + string.Join(", ", LeaderNames.GetRange(0, Math.Min(shown, LeaderNames.Count)))
                          + (LeaderNames.Count > shown ? ", +" + (LeaderNames.Count - shown) + " more" : string.Empty) + ")";
            }
            heroes += LowestHeroName != null && !double.IsNaN(LowestHeroPoints)
                ? "; lowest a hero reached: " + LowestHeroName + " " + N1(LowestHeroPoints) + " of " + N0(LowestHeroPool)
                : "; lowest a hero reached: n/a (no heroes)";
            lines.Add(heroes);

            lines.Add(PlayerSeen
                ? "Athletics you: skill " + PlayerSkill + " → pool " + N0(PlayerPool) + "; " + PlayerBlows + " blows, " + PlayerExhaustions + " exhaustion"
                  + (PlayerExhaustions == 1 ? "" : "s") + ", lowest " + N1(PlayerLowestPoints) + " of " + N0(PlayerPool)
                : "Athletics you: no player fighter this mission");

            if (formations.Count == 0)
            {
                lines.Add("Athletics your formations at the end: none with men in them");
            }
            else
            {
                var f = new StringBuilder("Athletics your formations at the end:");
                for (int i = 0; i < formations.Count; i++)
                {
                    var st = formations[i].Value;
                    f.Append(i == 0 ? " " : " | ").Append(formations[i].Key).Append(' ').Append(st.Describe());
                    if (!double.IsNaN(st.MeanPeakShare)) f.Append(" f avg ").Append(N2(st.MeanPeakShare)).Append(", ").Append(st.InPeak).Append(" at full strength");
                    if (!double.IsNaN(st.MeanHealth)) f.Append(", health avg ").Append(Math.Round(st.MeanHealth * 100).ToString("0", CultureInfo.InvariantCulture)).Append('%');
                }
                lines.Add(f.ToString());
            }

            lines.Add(!r.HealthCaps
                ? "Athletics health cap: off (HealthCapsAthletics) - " + HealthCuts + " cuts while it was on"
                : "Athletics health cap: " + HealthCuts + " cuts (a wound pulled Athletics down to the health left), biggest " + N1(HealthCutMaxPoints)
                  + " points, " + N0(HealthCutPoints) + " points in all");

            double regen = RegenWalkSeconds + RegenFasterSeconds;
            lines.Add("Athletics regen: " + N0(regen) + " fighter-seconds refilling - at a walk or slower (effort up to " + N2(r.WalkEffortFraction) + ") "
                + N0(RegenWalkSeconds) + " s at the walking rate (x1), faster " + N0(RegenFasterSeconds) + " s at avg x"
                + (RegenFasterSeconds > 0 ? N2(RegenFasterRateSeconds / RegenFasterSeconds) : "n/a") + "; refills to the top: "
                + RefillsToFull + " to full, " + RefillsToHealthCap + " to a wound's cap; refill curve: " + RefillCurveShort(in r));

            double walkToPeak = AthleticsMath.RefillSeconds(in r, 0, r.PeakFraction, 1.0);
            string atWalk = double.IsInfinity(walkToPeak) ? "no refill at a walk with these settings" : N1(walkToPeak) + " s at a walk or slower with these settings";
            lines.Add(EmptyToPeakRuns == 0
                ? "Athletics refill from empty to the peak line (no blow between): none this mission (" + atWalk + ")"
                : "Athletics refill from empty to the peak line (no blow between): " + EmptyToPeakRuns + " run" + (EmptyToPeakRuns == 1 ? "" : "s")
                  + ", avg " + N1(EmptyToPeakSecondsTotal / EmptyToPeakRuns) + " s (fastest " + N1(EmptyToPeakSecondsMin) + " s, slowest "
                  + N1(EmptyToPeakSecondsMax) + " s) - " + atWalk + ", longer while moving faster than a walk");

            var e = new StringBuilder("Athletics refill effort (speed ÷ current top speed), seconds per tenth (0-0.1 … 0.9-1, above 1): ");
            for (int i = 0; i < _effortSeconds.Length; i++) e.Append(i == 0 ? "" : " ").Append(N0(_effortSeconds[i]));
            e.Append(", max ").Append(N2(MaxEffort));
            lines.Add(e.ToString());

            lines.Add("speed updates: " + (FighterRecomputes + HorseRecomputes) + " recomputes asked (UpdateAgentProperties: fighters " + FighterRecomputes
                + ", horses " + HorseRecomputes + "; a change below x" + N2(AthleticsMath.SpeedUpdateStep) + " waits; " + RecomputesDeferred
                + " held a tick by the per-tick budget), the decorator applied attack penalties in " + DecoratorAttack + " recomputes, run penalties in "
                + DecoratorRun + ", horse penalties in " + DecoratorMount + " (the attack timings by f: the \"attack rate\" lines)");

            lines.Add("run speed check, on foot (÷ the fighter's own top speed when fresh), by f: " + FootRun.Describe(curveApplies: true));
            lines.Add("run speed check, horses (÷ the horse's own top speed while its rider was fresh), by the rider's f: "
                + (r.MountsSlow ? string.Empty : "MountMinSpeedMultiplier " + N2(r.MountMinSpeedMultiplier) + " = horses never slow - ")
                + HorseRun.Describe(curveApplies: r.MountsSlow));

            var w = new StringBuilder("walk vs run speeds (tune WalkEffortFraction, now ").Append(N2(r.WalkEffortFraction)).Append("): on foot walk limit ")
                .Append(Speed(FootWalk)).Append(", top ").Append(Speed(FootTop)).Append(Ratio(FootWalk, FootTop)).Append("; horses walk ")
                .Append(Speed(HorseWalk)).Append(", top ").Append(Speed(HorseTop)).Append(Ratio(HorseWalk, HorseTop));
            lines.Add(w.ToString());

            lines.Add(Ticks == 0
                ? "Athletics tick cost: no ticks"
                : "Athletics tick cost: avg " + N3(TickMsTotal / Ticks) + " ms, max " + N3(TickMsMax) + " ms per tick over " + Ticks
                  + " ticks; fighters polled avg " + N0((double)PolledTotal / Ticks) + ", max " + PolledMax);

            lock (_errors)
            {
                if (_errors.Count == 0)
                {
                    lines.Add("Athletics errors: none");
                }
                else
                {
                    var er = new StringBuilder("Athletics errors: ");
                    int n = 0;
                    foreach (var pair in _errors) n += pair.Value;
                    er.Append(n).Append(" (");
                    bool first = true;
                    foreach (var pair in _errors)
                    {
                        if (!first) er.Append(", ");
                        first = false;
                        er.Append(pair.Key).Append(' ').Append(pair.Value);
                    }
                    er.Append(") - each failed spot fell back to vanilla (no cost, no penalty); the first per place is logged as [error] with its stack");
                    lines.Add(er.ToString());
                }
            }
            return lines;
        }

        private string PoolsLine(in AthleticsRules r)
        {
            if (!Pools(in r, out double min, out double mean, out double max, out int atFloor))
                return "Athletics pools (the Athletics skill, settings at the end): no fighters tracked";
            var sb = new StringBuilder("Athletics pools (the Athletics skill x").Append(N2(r.PoolPerSkill)).Append(", at least ").Append(r.PoolFloor)
                .Append("; settings at the end): ").Append(FightersTracked).Append(" fighters - min ").Append(N0(min)).Append(" / avg ").Append(N1(mean))
                .Append(" / max ").Append(N0(max)).Append("; ").Append(atFloor).Append(" at the floor");
            if (SkillsUnknown > 0) sb.Append(", ").Append(SkillsUnknown).Append(" whose skill could not be read (the floor)");
            if (PlayerSeen) sb.Append("; you ").Append(N0(AthleticsMath.PoolPointsForSkill(in r, PlayerSkill))).Append(" (skill ").Append(PlayerSkill).Append(')');
            if (LeaderSkills.Count > 0)
            {
                sb.Append("; party leaders:");
                const int shown = 20;
                for (int i = 0; i < LeaderSkills.Count && i < shown; i++)
                    sb.Append(i == 0 ? " " : ", ").Append(LeaderSkills[i].Key).Append(' ').Append(N0(AthleticsMath.PoolPointsForSkill(in r, LeaderSkills[i].Value)));
                if (LeaderSkills.Count > shown) sb.Append(", +").Append(LeaderSkills.Count - shown).Append(" more");
            }
            return sb.ToString();
        }

        /// <summary>The <c>[summary]</c> blocks line (step 22): what the defenders paid, by kind and by who.</summary>
        public string BlocksLine() =>
            "Athletics blocks paid by the defender " + BlocksCharged + " (" + N1(BlockPoints) + " points; by riders " + BlocksMounted + "): shield right side "
            + Blocks(BlockKind.ShieldRightSide) + " (" + N1(BlockPointsOf(BlockKind.ShieldRightSide)) + "), shield WRONG side " + Blocks(BlockKind.ShieldWrongSide)
            + " (" + N1(BlockPointsOf(BlockKind.ShieldWrongSide)) + "), weapon parries " + Blocks(BlockKind.WeaponParry) + " (" + N1(BlockPointsOf(BlockKind.WeaponParry))
            + ") | by you " + BlocksByYou + " (" + N1(BlockPointsYou) + "), AI heroes " + BlocksByHeroes + " (" + N1(BlockPointsHeroes) + "), other AI "
            + BlocksByOthers + " (" + N1(BlockPointsOthers) + ") | free (cost 0) " + BlocksFree + ", while Athletics was off " + BlocksWhileOff
            + ", the same blow seen again (not charged twice) " + BlocksSameBlow + ", missiles stopped by a shield (free) " + MissilesBlockedByShield
            + "; each blocked blow charged once to the defender; never an attack pause or a step back";

        /// <summary><c>ON - pool = the Athletics skill x1.00, at least 50; …</c> or <c>OFF …</c> - the
        /// settings sentence of the mission-start line and the summary.</summary>
        public static string DescribeRules(in AthleticsRules r)
        {
            if (!r.ModEnabled) return "OFF - the whole mod is switched off (ModEnabled) - everyone full, no penalty";
            if (!r.Enabled) return "OFF (AthleticsEnabled) - everyone full, no penalty";
            var soldier = new Fighter();
            var hero = new Fighter { IsHero = true };
            var leader = new Fighter { IsHero = true, IsLeader = true };
            return "ON - pool = the Athletics skill x" + N2(r.PoolPerSkill) + ", at least " + r.PoolFloor
                + "; full strength at " + r.PeakPercent + "% of the pool and above"
                + "; cost per blow " + N1(AthleticsMath.BlowCostPoints(in r, soldier))
                + " / hero " + N1(AthleticsMath.BlowCostPoints(in r, hero))
                + " / party leader " + N1(AthleticsMath.BlowCostPoints(in r, leader)) + " points"
                + "; a kick or shield bash " + N2(AthleticsMath.KickOrBashCostPoints(in r, soldier))
                + " / hero " + N2(AthleticsMath.KickOrBashCostPoints(in r, hero))
                + " / party leader " + N2(AthleticsMath.KickOrBashCostPoints(in r, leader)) + " points"
                + "; a blocked blow costs the defender: shield right side " + N2(AthleticsMath.BlockCostPoints(in r, soldier, BlockKind.ShieldRightSide))
                + " / wrong side " + N2(AthleticsMath.BlockCostPoints(in r, soldier, BlockKind.ShieldWrongSide))
                + " / weapon parry " + N2(AthleticsMath.BlockCostPoints(in r, soldier, BlockKind.WeaponParry)) + " points (a hero x"
                + N2(AthleticsMath.CostMultiplier(in r, hero)) + ", a party leader x" + N2(AthleticsMath.CostMultiplier(in r, leader)) + ")"
                + ", misses cost: " + (r.CostOnMiss ? "yes" : "no (landed blows only)")
                + "; when empty: attacks at " + r.ExhaustedAttackSpeedPercent + "%, run x" + N2(r.RunSpeedFloor)
                + ", horses x" + N2(r.MountsSlow ? r.MountSpeedFloor : 1f) + (r.MountsSlow ? string.Empty : " (never slowed)")
                + "; damage upside follows Athletics: " + (r.DamageBonusFollows ? "yes" : "no")
                + "; wounds cap the pool: " + (r.HealthCaps ? "yes" : "no")
                + "; refill after " + N1(r.RegenDelaySeconds) + " s rest: empty to full in " + N0(r.FullRegenSecondsStanding)
                + " s at a walk or slower (up to " + N2(r.WalkEffortFraction) + " of top speed), x" + N2(r.RegenMultiplierAtFullRun) + " at a full run"
                + ", " + RefillCurveText(in r);
        }

        /// <summary>The refill curve in words (step 14): <c>near full at 50% of the rate near empty (at a walk:
        /// half the bar in 25 s, the peak line in 41 s)</c>, or <c>the same rate all the way</c> at 100.</summary>
        public static string RefillCurveText(in AthleticsRules r)
        {
            if (r.RegenNearFullShare >= 1.0) return "the same rate all the way (RegenRateNearFullPercent 100)";
            string text = "near full at " + N0(r.RegenNearFullShare * 100) + "% of the rate near empty";
            double half = AthleticsMath.RefillSeconds(in r, 0, 0.5, 1.0);
            double peak = AthleticsMath.RefillSeconds(in r, 0, r.PeakFraction, 1.0);
            if (double.IsInfinity(half) || double.IsInfinity(peak)) return text;
            return text + " (at a walk: half the bar in " + N0(half) + " s, the peak line in " + N0(peak) + " s)";
        }

        /// <summary>The curve for the summary's regen line: <c>near full x0.50 of near empty (x1.39 → x0.69 of a flat refill)</c>.</summary>
        public static string RefillCurveShort(in AthleticsRules r)
        {
            if (r.RegenNearFullShare >= 1.0) return "flat (RegenRateNearFullPercent 100)";
            double flat = r.FullRegenSecondsStanding > 0 ? 1.0 / r.FullRegenSecondsStanding : 0;
            string text = "near full x" + N2(r.RegenNearFullShare) + " of near empty (RegenRateNearFullPercent " + r.RegenRateNearFullPercent + ")";
            if (!(flat > 0)) return text;
            double empty = AthleticsMath.RegenRateAtEmpty(in r) / flat;
            return text + ", x" + N2(empty) + " → x" + N2(empty * r.RegenNearFullShare) + " of a flat refill";
        }

        private static string Speed(MeanStd s) => s.Count == 0 ? "n/a" : "avg " + N2(s.Mean) + " m/s (n " + s.Count + ")";

        private static string Ratio(MeanStd walk, MeanStd top) =>
            walk.Count == 0 || top.Count == 0 || top.Mean <= 0 ? string.Empty : " → walk/top " + N2(walk.Mean / top.Mean);

        private static string N0(double v) => v.ToString("0", CultureInfo.InvariantCulture);

        private static string N1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        private static string N2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        private static string N3(double v) => v.ToString("0.000", CultureInfo.InvariantCulture);

        private static string P1(double share) => (share * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace TraxCombat.Core
{
    /// <summary>
    /// One mission's endurance numbers for the <c>[summary]</c> block - built so ONE playtest run
    /// proves or disproves every behaviour of DESIGN §2 (CLAUDE.md, logging): blows by kind and by
    /// riders vs on foot, the detection cross-checks (releases vs hits vs shots), what was free,
    /// exhaustions, heroes and leaders, the player, the formations, regen standing vs moving, the
    /// measured attack intervals fresh vs exhausted (the engine-clamp test), the tick cost, the
    /// speeds step 5c needs, and errors. A new instance per mission.
    ///
    /// Main thread only (every caller is a mission tick or an engine callback marked not
    /// multi-thread callable), except <see cref="AddDecoratorScaled"/> which is interlocked.
    /// Plain counters are public fields on purpose: the module just counts.
    /// </summary>
    public sealed class EnduranceStats
    {
        public const int ActionSlots = 64;
        public const int EffortBins = 11; // tenths 0.0-0.1 … 0.9-1.0, then above 1

        private readonly int[] _charged = new int[5];
        private readonly int[] _outsideActions = new int[ActionSlots];
        private readonly int[] _effort = new int[EffortBins];
        private readonly Dictionary<string, int> _errors = new Dictionary<string, int>(StringComparer.Ordinal);
        private int _decoratorScaled;

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

        // ---- free / not charged
        public int KicksSeen;
        public int BashesSeen;
        public int KickOrBashHits;
        public int CouchedWithinBlowTime;
        public int AttacksWhileOff;
        public int ReleasesAwaitingHit;
        public int ShotsAwaitingHit;

        // ---- transitions and speed
        public int ExhaustionsEntered;
        public int ExhaustionsLeft;
        public int SpeedUpdates;

        // ---- regen
        public double RegenStandingSeconds;
        public double RegenMovingSeconds;
        public int RefillsToFull;
        public double MaxEffort;

        // ---- the attack-speed measurement (RESEARCH UNVERIFIED #1)
        public readonly IntervalStats MeleeFresh = new IntervalStats();
        public readonly IntervalStats MeleeExhausted = new IntervalStats();
        public readonly IntervalStats SwingFresh = new IntervalStats();
        public readonly IntervalStats SwingExhausted = new IntervalStats();
        public readonly IntervalStats RangedFresh = new IntervalStats();
        public readonly IntervalStats RangedExhausted = new IntervalStats();
        public int IntervalsMixed;

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
        public int PlayerBlows;
        public int PlayerExhaustions;
        public double PlayerLowestPoints;
        public double PlayerPool;

        // ---- speeds for step 5c (walk vs top speed)
        public MeanStd FootWalk;
        public MeanStd FootTop;
        public MeanStd HorseWalk;
        public MeanStd HorseTop;

        // ------------------------------------------------------------------ counting

        public void AddCharge(BlowKind kind, bool mounted, double points)
        {
            _charged[(int)kind]++;
            if (mounted) ChargedMounted++;
            else ChargedOnFoot++;
            PointsSpent += points;
        }

        public int Charged(BlowKind kind) => _charged[(int)kind];

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

        /// <summary>Speed ÷ top speed of one refill sample (step 5c's effort).</summary>
        public void AddEffort(double effort)
        {
            if (double.IsNaN(effort) || effort < 0) return;
            int bin = effort > 1.0 ? EffortBins - 1 : Math.Min(EffortBins - 2, (int)(effort * 10));
            _effort[bin]++;
            if (effort > MaxEffort) MaxEffort = effort;
        }

        public int EffortSamples
        {
            get
            {
                int n = 0;
                foreach (int e in _effort) n += e;
                return n;
            }
        }

        public void AddTick(int polled, double ms)
        {
            Ticks++;
            PolledTotal += polled;
            if (polled > PolledMax) PolledMax = polled;
            TickMsTotal += ms;
            if (ms > TickMsMax) TickMsMax = ms;
        }

        /// <summary>The stat decorator applied a penalty in one recompute (any thread).</summary>
        public void AddDecoratorScaled() => Interlocked.Increment(ref _decoratorScaled);

        public int DecoratorScaled => Volatile.Read(ref _decoratorScaled);

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

        // ------------------------------------------------------------------ the summary text

        /// <summary>
        /// The <c>[summary]</c> endurance lines, plain words (docs/PLAYTEST.md §3 quotes them).
        /// <paramref name="actionName"/> names an engine action code; <paramref name="formations"/> are
        /// the player's formations at the end (name, stats).
        /// </summary>
        public List<string> SummaryLines(in EnduranceRules r, Func<int, string> actionName, IList<KeyValuePair<string, FormationEnduranceStats>> formations)
        {
            var lines = new List<string>();
            var soldier = new Fighter();
            var hero = new Fighter { IsHero = true };
            var leader = new Fighter { IsHero = true, IsLeader = true };
            lines.Add(r.Enabled
                ? "endurance settings at the end: ON - pool " + N0(EnduranceMath.PoolPoints(in r, soldier))
                  + ", cost per blow " + N1(EnduranceMath.BlowCostPoints(in r, soldier))
                  + " / hero " + N1(EnduranceMath.BlowCostPoints(in r, hero))
                  + " / party leader " + N1(EnduranceMath.BlowCostPoints(in r, leader))
                  + ", misses cost: " + (r.CostOnMiss ? "yes" : "no (landed blows only)")
                  + ", exhausted attacks at " + r.ExhaustedAttackSpeedPercent + "% (recover above " + r.ExhaustedRecoverPercent + "%)"
                  + ", refill after " + N1(r.RegenDelaySeconds) + " s rest: full in " + N0(r.FullRegenSecondsStanding) + " s standing / "
                  + N0(r.FullRegenSecondsMoving) + " s moving (above " + N1(r.MovingSpeedThreshold) + " m/s)"
                : "endurance settings at the end: OFF (EnduranceEnabled) - everyone full, no penalty");

            lines.Add("endurance blows charged: " + ChargedTotal
                + " (melee swings " + Charged(BlowKind.Melee) + ", shots/throws " + Charged(BlowKind.Ranged)
                + ", couched/braced hits " + Charged(BlowKind.Couched) + ", landed-only swings " + Charged(BlowKind.LandedMelee)
                + ", landed-only shots " + Charged(BlowKind.LandedRanged) + ") - by riders " + ChargedMounted + ", on foot " + ChargedOnFoot
                + "; endurance spent " + N0(PointsSpent) + " points");

            var sb = new StringBuilder("endurance detection: melee releases seen ").Append(MeleeReleasesSeen)
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

            lines.Add("endurance free (never charged): kicks " + KicksSeen + ", shield bashes " + BashesSeen + ", kick/bash hits " + KickOrBashHits
                + ", couched hits within one blow-length of the last " + CouchedWithinBlowTime + ", attacks while endurance was off " + AttacksWhileOff
                + ", releases / shots waiting for a landed hit (misses cost: no) " + ReleasesAwaitingHit + " / " + ShotsAwaitingHit);

            lines.Add("endurance exhaustions: " + ExhaustionsEntered + " entered, " + ExhaustionsLeft + " left");

            string heroes = "endurance heroes: " + HeroesFlagged + " flagged, " + LeaderNames.Count + " party leader" + (LeaderNames.Count == 1 ? "" : "s");
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
                ? "endurance you: " + PlayerBlows + " blows, " + PlayerExhaustions + " exhaustion" + (PlayerExhaustions == 1 ? "" : "s")
                  + ", lowest " + N1(PlayerLowestPoints) + " of " + N0(PlayerPool)
                : "endurance you: no player fighter this mission");

            if (formations.Count == 0)
            {
                lines.Add("endurance your formations at the end: none with men in them");
            }
            else
            {
                var f = new StringBuilder("endurance your formations at the end:");
                for (int i = 0; i < formations.Count; i++)
                    f.Append(i == 0 ? " " : " | ").Append(formations[i].Key).Append(' ').Append(formations[i].Value.Describe());
                lines.Add(f.ToString());
            }

            lines.Add("endurance regen: " + N0(RegenStandingSeconds) + " fighter-seconds standing, " + N0(RegenMovingSeconds)
                + " moving; " + RefillsToFull + " refills to full");

            int percent = r.ExhaustedAttackSpeedPercent;
            lines.Add("attack speed check, melee - time between swings: " + SpeedVerdict.Describe(MeleeFresh, MeleeExhausted, percent));
            lines.Add("attack speed check, melee - swing length (swings that hit nothing): " + SpeedVerdict.Describe(SwingFresh, SwingExhausted, percent));
            lines.Add("attack speed check, ranged - time between shots: " + SpeedVerdict.Describe(RangedFresh, RangedExhausted, percent));
            lines.Add("attack speed updates: " + SpeedUpdates + " recomputes asked (UpdateAgentProperties), the decorator applied a penalty in "
                + DecoratorScaled + " recomputes; " + IntervalsMixed + " intervals spanning a change of state left out");

            lines.Add(Ticks == 0
                ? "endurance tick cost: no ticks"
                : "endurance tick cost: avg " + N3(TickMsTotal / Ticks) + " ms, max " + N3(TickMsMax) + " ms per tick over " + Ticks
                  + " ticks; fighters polled avg " + N0((double)PolledTotal / Ticks) + ", max " + PolledMax);

            var e = new StringBuilder("speeds for step 5c: on foot walk limit ").Append(Speed(FootWalk)).Append(", top ").Append(Speed(FootTop))
                .Append(Ratio(FootWalk, FootTop)).Append("; horses walk ").Append(Speed(HorseWalk)).Append(", top ").Append(Speed(HorseTop))
                .Append(Ratio(HorseWalk, HorseTop)).Append("; refill samples speed/top in tenths (0-0.1 … 0.9-1, above 1): ");
            e.Append(string.Join(" ", Array.ConvertAll(_effort, n => n.ToString(CultureInfo.InvariantCulture))));
            e.Append(", max ").Append(N2(MaxEffort));
            lines.Add(e.ToString());

            lock (_errors)
            {
                if (_errors.Count == 0)
                {
                    lines.Add("endurance errors: none");
                }
                else
                {
                    var er = new StringBuilder("endurance errors: ");
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

        private static string Speed(MeanStd s) => s.Count == 0 ? "n/a" : "avg " + N2(s.Mean) + " m/s (n " + s.Count + ")";

        private static string Ratio(MeanStd walk, MeanStd top) =>
            walk.Count == 0 || top.Count == 0 || top.Mean <= 0 ? string.Empty : " → walk/top " + N2(walk.Mean / top.Mean);

        private static string N0(double v) => v.ToString("0", CultureInfo.InvariantCulture);

        private static string N1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        private static string N2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        private static string N3(double v) => v.ToString("0.000", CultureInfo.InvariantCulture);
    }
}

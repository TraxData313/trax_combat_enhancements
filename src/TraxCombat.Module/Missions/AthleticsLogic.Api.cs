using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// The READ API for the HUD steps 6-9 - static, allocation-free, main thread (call it from a
    /// MissionView's tick). Everything returns a snapshot struct; nothing hands out our records.
    ///
    ///   <see cref="IsRunning"/>                 an Athletics logic is live in this mission
    ///   <see cref="TryGetReading"/>(agent)      one fighter: points, pool, usable pool (health cap),
    ///                                          fraction, f (PeakShare - the colours), the peak line,
    ///                                          exhausted, hero / leader, skill, the speed multipliers
    ///                                          applied. False for a horse (ask for its RiderAgent), an
    ///                                          agent we do not track, or between missions. With
    ///                                          Athletics (or the mod) off it reads full, unpenalized.
    ///   <see cref="TryGetPeakShare"/>(agent)    just f (the damage decorator's per-hit read).
    ///   <see cref="TryGetFormationStats"/>(f)   any team's formation (step 20: every side - the ALT
    ///                                          labels show the enemy's too; step 9 read the player's
    ///                                          team only): count, mean ± standard deviation (points and
    ///                                          fraction), the men's average f and how many are at full
    ///                                          strength, exhausted, their average health (step 9), how
    ///                                          many brace now and how many are ready (step 23). The
    ///                                          player himself is left out of his own formation, as the
    ///                                          orders menu's cards leave him out; everyone else on
    ///                                          every side counts. Refreshed every
    ///                                          FormationStatsRefreshSeconds (live). False for an empty
    ///                                          formation or a team past <see cref="MaxStatTeams"/>.
    ///   <see cref="FormationStatsVersion"/>     bumps at every refresh - redraw squad bars when it moves.
    ///   <see cref="TryGetFever"/>(agent)        step 44: one AI man's fever (step 43) - armed, or burning with the share
    ///                                          left; false for a calm man, the player, or the fever off. The fever-mark
    ///                                          view walks the same answer allocation-free (FeverWalkCount / TryGetFeverAt).
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        /// <summary>Teams whose formations get stats, by <c>Team.TeamIndex</c> (a field battle has 2-4; plumbing).</summary>
        internal const int MaxStatTeams = 8;

        private const int FormationsPerTeam = (int)FormationClass.NumberOfAllFormations;
        private const int StatSlots = MaxStatTeams * FormationsPerTeam;

        private readonly MeanStd[] _formationPoints = new MeanStd[StatSlots];
        private readonly MeanStd[] _formationFractions = new MeanStd[StatSlots];
        private readonly MeanStd[] _formationPeakShares = new MeanStd[StatSlots];
        private readonly MeanStd[] _formationHealth = new MeanStd[StatSlots];
        private readonly int[] _formationExhausted = new int[StatSlots];
        private readonly int[] _formationInPeak = new int[StatSlots];
        private readonly int[] _formationBracing = new int[StatSlots];
        private readonly int[] _formationInFever = new int[StatSlots]; // step 58: the ALT label's "fever 12/40"
        private readonly FormationAthleticsStats[] _formationSnapshot = new FormationAthleticsStats[StatSlots];
        private double _formationRefreshedAt = double.NegativeInfinity;
        private int _formationVersion;

        /// <summary>Tracked fighters left out of the last refresh because their team's index is past
        /// <see cref="MaxStatTeams"/> (0 in every vanilla battle; the summary names it when not).</summary>
        private int _formationTeamsSkipped;

        /// <summary>True while an Athletics logic runs in the current mission.</summary>
        public static bool IsRunning => _current != null;

        /// <summary>One fighter's Athletics now. See the class doc.</summary>
        public static bool TryGetReading(Agent agent, out AthleticsReading reading)
        {
            reading = default;
            var logic = _current;
            if (logic == null || agent == null) return false;
            var st = logic.Get(agent);
            if (st == null) return false;
            reading = AthleticsMath.Read(AthleticsRules.From(TraxSettings.Shared), st);
            return true;
        }

        /// <summary>f of one fighter (DESIGN §2: the share of his peak line left, 0..1; 1 while Athletics
        /// or the mod is off). False for an agent we do not track (a horse - pass its rider -, or
        /// none running): the caller treats that as "no pool" (the damage roll keeps its full upside).
        /// Allocation-free, main thread.</summary>
        public static bool TryGetPeakShare(Agent agent, out double peakShare)
        {
            peakShare = 1.0;
            var logic = _current;
            if (logic == null || agent == null) return false;
            var st = logic.Get(agent);
            if (st == null) return false;
            peakShare = AthleticsMath.PeakShare(AthleticsRules.From(TraxSettings.Shared), st);
            return true;
        }

        /// <summary>The last snapshot of one formation (any team). See the class doc.</summary>
        public static bool TryGetFormationStats(Formation formation, out FormationAthleticsStats stats)
        {
            stats = default;
            var logic = _current;
            if (logic == null || formation == null) return false;
            int slot = StatSlot(formation.Team, (int)formation.FormationIndex);
            if (slot < 0) return false;
            stats = logic._formationSnapshot[slot];
            return stats.Count > 0;
        }

        /// <summary>
        /// Step 44: one AI man's FEVER now (step 43) - true while it runs: <paramref name="armed"/> = armed (no enemy met yet, the peak),
        /// else burning with <paramref name="share"/> = the store left ÷ full (1 while armed). False for a calm man, the player (never a
        /// fever), an agent we do not track, or while the fever is off (the master switch, Athletics, FeverEnabled - read live).
        /// Allocation-free, main thread.
        /// </summary>
        public static bool TryGetFever(Agent agent, out bool armed, out double share)
        {
            armed = false;
            share = 0;
            var logic = _current;
            if (logic == null || agent == null || logic._feverRunning <= 0) return false;
            var st = logic.Get(agent);
            return st != null && logic.FeverOf(st, out armed, out share);
        }

        /// <summary>Step 44: how many tracked fighters <see cref="TryGetFeverAt"/> walks this frame (0 while no fever runs - the fever-mark
        /// view's loop then costs nothing).</summary>
        internal static int FeverWalkCount
        {
            get
            {
                var logic = _current;
                return logic == null || logic._feverRunning <= 0 ? 0 : logic._count;
            }
        }

        /// <summary>Step 44: the tracked fighter at <paramref name="index"/> (0 .. <see cref="FeverWalkCount"/>) when he is in a fever - the
        /// fever-mark view's allocation-free walk (no lookup by agent). See <see cref="TryGetFever"/>.</summary>
        internal static bool TryGetFeverAt(int index, out Agent? agent, out bool armed, out double share)
        {
            agent = null;
            armed = false;
            share = 0;
            var logic = _current;
            if (logic == null || index < 0 || index >= logic._count) return false;
            var st = logic._dense[index];
            if (st == null || !logic.FeverOf(st, out armed, out share)) return false;
            agent = st.Agent;
            return true;
        }

        /// <summary>Step 46: how many tracked fighters <see cref="TryGetMarkAt"/> walks this frame - every one when <paramref name="everyone"/>
        /// (the Athletics bars), else only while a fever runs (step 44's walk: 0 = the loop costs nothing).</summary>
        internal static int MarkWalkCount(bool everyone)
        {
            var logic = _current;
            if (logic == null) return 0;
            return everyone || logic._feverRunning > 0 ? logic._count : 0;
        }

        /// <summary>
        /// Step 46: the tracked fighter at <paramref name="index"/> as the ALT bars need him - his fever (<see cref="FeverMark.InFever"/>, Armed,
        /// Share) and, when <paramref name="everyone"/>, his Athletics bar as the strip and the player bar read it (BarFill = points ÷ his FULL
        /// pool, BarUsable = the wound cap, Band by f) - written into <paramref name="m"/>. False for the player (his own bar), a man gone, and - when
        /// not <paramref name="everyone"/> - a man not in a fever. Allocation-free, main thread; the rules are read once per frame by the caller.
        /// </summary>
        internal static bool TryGetMarkAt(int index, bool everyone, in AthleticsRules rules, in BarRules bands, ref FeverMark m, out Agent? agent)
        {
            agent = null;
            var logic = _current;
            if (logic == null || index < 0 || index >= logic._count) return false;
            var st = logic._dense[index];
            if (st == null || st.Removed || logic.IsPlayer(st)) return false;
            bool fever = logic.FeverOf(st, out bool armed, out double share);
            if (!fever && !everyone) return false;
            m.InFever = fever;
            m.Armed = armed;
            m.Share = (float)share;
            if (everyone)
                AthleticsMarkMath.Read(ref m, in bands, rules.Enabled ? st.Fraction : 1.0, AthleticsMath.UsableFraction(in rules, st),
                    AthleticsMath.PeakShare(in rules, st));
            agent = st.Agent;
            return true;
        }

        private bool FeverOf(TrackedAgent st, out bool armed, out double share)
        {
            armed = false;
            share = 0;
            var fv = st.Fever;
            if (st.Removed || !fv.Running) return false;
            var s = TraxSettings.Shared; // FeverRules.Enabled's three switches, read without building the rules (a hot walk)
            if (!s.ModEnabled || !s.AthleticsEnabled || !s.FeverEnabled || IsPlayer(st)) return false;
            armed = fv.Phase == FeverPhase.Armed;
            share = armed ? 1.0 : fv.Share;
            return armed || share > 0;
        }

        /// <summary>Bumps each time the formation snapshot is refreshed.</summary>
        public static int FormationStatsVersion => _current?._formationVersion ?? 0;

        /// <summary>A formation's slot: its team's index × 10 + its index; −1 when out of range.</summary>
        private static int StatSlot(Team? team, int formationIndex)
        {
            if (team == null || formationIndex < 0 || formationIndex >= FormationsPerTeam) return -1;
            int t = team.TeamIndex;
            return t >= 0 && t < MaxStatTeams ? t * FormationsPerTeam + formationIndex : -1;
        }

        /// <summary>One O(N) pass over the tracked fighters of EVERY team (from the tick, every
        /// FormationStatsRefreshSeconds; and once for the summary), each filed by his team and formation.
        /// The player himself is left out (he has his own bar; the orders menu's cards count the men
        /// under his command). Health is read live (managed Health ÷ HealthLimit), whatever the switches.
        /// No allocation.</summary>
        private void RefreshFormationStats(double now, in AthleticsRules r)
        {
            _formationRefreshedAt = now;
            for (int k = 0; k < _formationSnapshot.Length; k++)
            {
                _formationPoints[k].Clear();
                _formationFractions[k].Clear();
                _formationPeakShares[k].Clear();
                _formationHealth[k].Clear();
                _formationExhausted[k] = 0;
                _formationInPeak[k] = 0;
                _formationBracing[k] = 0;
                _formationInFever[k] = 0;
            }
            int skipped = 0;
            for (int i = 0; i < _count; i++)
            {
                var st = _dense[i];
                var a = st.Agent;
                var formation = a.Formation; // managed reads first; IsActive reads the engine's state
                if (formation == null) continue;
                int k = StatSlot(a.Team, (int)formation.FormationIndex);
                if (k < 0)
                {
                    if (a.Team != null && a.Team.TeamIndex >= MaxStatTeams) skipped++;
                    continue;
                }
                if (a.IsMainAgent || !a.IsActive()) continue;
                double fraction = r.Enabled ? st.Fraction : 1.0;
                _formationPoints[k].Add(fraction * AthleticsMath.PoolPoints(in r, st));
                _formationFractions[k].Add(fraction);
                double share = AthleticsMath.PeakShare(in r, st);
                _formationPeakShares[k].Add(share);
                _formationHealth[k].Add(HealthOf(a));
                if (share >= 1.0) _formationInPeak[k]++;
                if (AthleticsMath.IsExhausted(in r, st)) _formationExhausted[k]++;
                var bs = st.Brace;
                if (bs != null && bs.Active) _formationBracing[k]++; // step 23: the ready count = Count - Bracing
                if (_feverRunning > 0 && FeverOf(st, out _, out _)) _formationInFever[k]++; // step 58: the fever share (the switches read inside)
            }
            _formationTeamsSkipped = skipped;
            for (int k = 0; k < _formationSnapshot.Length; k++)
                _formationSnapshot[k] = FormationAthleticsStats.From(in _formationPoints[k], in _formationFractions[k], in _formationPeakShares[k],
                    in _formationHealth[k], _formationExhausted[k], _formationInPeak[k], _formationBracing[k], _formationInFever[k]);
            _formationVersion++;
        }

        /// <summary>The player team's formations at the last refresh (the summary's "your formations").</summary>
        private void AddPlayerFormations(List<KeyValuePair<string, FormationAthleticsStats>> into)
        {
            int t = Mission?.PlayerTeam?.TeamIndex ?? -1;
            if (t < 0 || t >= MaxStatTeams) return;
            for (int k = 0; k < FormationsPerTeam; k++)
            {
                var s = _formationSnapshot[t * FormationsPerTeam + k];
                if (s.Count > 0) into.Add(new KeyValuePair<string, FormationAthleticsStats>(FormationName(k), s));
            }
        }
    }
}

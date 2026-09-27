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
    ///   <see cref="TryGetFormationStats"/>(f)   one of the PLAYER TEAM's formations: count, mean ±
    ///                                          standard deviation (points and fraction), the men's
    ///                                          average f and how many are at full strength,
    ///                                          exhausted - refreshed every FormationStatsRefreshSeconds
    ///                                          (live). False for another team's formation or an empty one.
    ///   <see cref="FormationStatsVersion"/>     bumps at every refresh - redraw squad bars when it moves.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly MeanStd[] _formationPoints = new MeanStd[(int)FormationClass.NumberOfAllFormations];
        private readonly MeanStd[] _formationFractions = new MeanStd[(int)FormationClass.NumberOfAllFormations];
        private readonly MeanStd[] _formationPeakShares = new MeanStd[(int)FormationClass.NumberOfAllFormations];
        private readonly int[] _formationExhausted = new int[(int)FormationClass.NumberOfAllFormations];
        private readonly int[] _formationInPeak = new int[(int)FormationClass.NumberOfAllFormations];
        private readonly FormationAthleticsStats[] _formationSnapshot = new FormationAthleticsStats[(int)FormationClass.NumberOfAllFormations];
        private double _formationRefreshedAt = double.NegativeInfinity;
        private int _formationVersion;

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

        /// <summary>The last snapshot of one of the player team's formations. See the class doc.</summary>
        public static bool TryGetFormationStats(Formation formation, out FormationAthleticsStats stats)
        {
            stats = default;
            var logic = _current;
            if (logic == null || formation == null) return false;
            var team = logic.Mission?.PlayerTeam;
            if (team == null || !ReferenceEquals(formation.Team, team)) return false;
            int k = (int)formation.FormationIndex;
            if (k < 0 || k >= logic._formationSnapshot.Length) return false;
            stats = logic._formationSnapshot[k];
            return stats.Count > 0;
        }

        /// <summary>Bumps each time the formation snapshot is refreshed.</summary>
        public static int FormationStatsVersion => _current?._formationVersion ?? 0;

        /// <summary>One O(N) pass over the tracked fighters of the player's team (from the tick,
        /// every FormationStatsRefreshSeconds; and once for the summary).</summary>
        private void RefreshFormationStats(double now, in AthleticsRules r)
        {
            _formationRefreshedAt = now;
            for (int k = 0; k < _formationSnapshot.Length; k++)
            {
                _formationPoints[k].Clear();
                _formationFractions[k].Clear();
                _formationPeakShares[k].Clear();
                _formationExhausted[k] = 0;
                _formationInPeak[k] = 0;
            }
            var team = Mission.PlayerTeam;
            if (team != null)
            {
                for (int i = 0; i < _count; i++)
                {
                    var st = _dense[i];
                    var a = st.Agent;
                    if (!ReferenceEquals(a.Team, team) || !a.IsActive()) continue;
                    var formation = a.Formation;
                    if (formation == null) continue;
                    int k = (int)formation.FormationIndex;
                    if (k < 0 || k >= _formationSnapshot.Length) continue;
                    double fraction = r.Enabled ? st.Fraction : 1.0;
                    _formationPoints[k].Add(fraction * AthleticsMath.PoolPoints(in r, st));
                    _formationFractions[k].Add(fraction);
                    double share = AthleticsMath.PeakShare(in r, st);
                    _formationPeakShares[k].Add(share);
                    if (share >= 1.0) _formationInPeak[k]++;
                    if (AthleticsMath.IsExhausted(in r, st)) _formationExhausted[k]++;
                }
            }
            for (int k = 0; k < _formationSnapshot.Length; k++)
                _formationSnapshot[k] = FormationAthleticsStats.From(in _formationPoints[k], in _formationFractions[k], in _formationPeakShares[k],
                    _formationExhausted[k], _formationInPeak[k]);
            _formationVersion++;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Models;

namespace TraxCombat.Missions
{
    /// <summary>
    /// The endurance log lines (CLAUDE.md, logging - one playtest must prove every behaviour):
    ///   always   [endurance] mission start (rules), leader rule, party leaders at spawn (limited),
    ///            first tick (fighters tracked), YOUR exhaustion / recovery / back-to-full (limited),
    ///            EnduranceEnabled switched mid-mission; [speed] stat model on top, the FIRST
    ///            exhaustion's properties before → after → at recovery → restored (once per
    ///            mission), ExhaustedAttackSpeedPercent changed mid-mission; the [summary] block.
    ///   verbose  per blow (bucket endurance-blow), everyone's exhaustion / recovery
    ///            (endurance-exhaust), back to full (endurance-regen), heroes at spawn
    ///            (endurance-hero), each speed recompute (speed-update) - each kind rate-limited in
    ///            its own bucket so one cannot starve the others.
    /// Strings are built only when the line will be written (VerboseOn checked first).
    /// </summary>
    public sealed partial class EnduranceLogic
    {
        private void LogBlow(TrackedAgent st, BlowKind kind, in BlowOutcome o, bool mounted)
        {
            try
            {
                var sb = new StringBuilder(128);
                sb.Append("blow ").Append(KindName(kind)).Append(mounted ? " (mounted)" : " (on foot)").Append(": ").Append(Name(st))
                  .Append(" - cost ").Append(F1(o.Cost));
                if (st.IsHero || st.IsLeader)
                {
                    sb.Append(" (x").Append(F2(o.Multiplier)).Append(':');
                    if (st.IsHero) sb.Append(" hero");
                    if (st.IsLeader) sb.Append(" party leader");
                    sb.Append(')');
                }
                sb.Append(", ").Append(F1(o.Before)).Append(" → ").Append(F1(o.After)).Append(" of ").Append(F0(o.Pool));
                if (o.EnteredExhaustion) sb.Append(" - EXHAUSTED");
                TraxLog.Verbose("endurance", sb.ToString(), "endurance-blow");
            }
            catch (Exception e)
            {
                Failed("endurance.log", e);
            }
        }

        /// <param name="agentsAlive">False on the teardown fallback: the agents' native side is gone,
        /// so the formations come from the last tick's snapshot and nothing is sampled.</param>
        internal void WriteEnduranceSummary(bool agentsAlive = true)
        {
            var r = EnduranceRules.From(TraxSettings.Shared);

            // the lowest any hero reached (heroes' records outlive their removal from the loop)
            TrackedAgent? lowest = null;
            double lowestPoints = double.MaxValue;
            foreach (var h in _heroes)
            {
                double p = h.LowestFraction * EnduranceMath.PoolPoints(in r, h);
                if (p < lowestPoints)
                {
                    lowestPoints = p;
                    lowest = h;
                }
            }
            if (lowest != null)
            {
                _stats.LowestHeroName = Name(lowest);
                _stats.LowestHeroPoints = lowestPoints;
                _stats.LowestHeroPool = EnduranceMath.PoolPoints(in r, lowest);
            }

            TrackPlayer();
            if (_player != null)
            {
                double pool = EnduranceMath.PoolPoints(in r, _player);
                _stats.PlayerSeen = true;
                _stats.PlayerBlows = _player.Blows;
                _stats.PlayerExhaustions = _player.ExhaustionsEntered;
                _stats.PlayerLowestPoints = _player.LowestFraction * pool;
                _stats.PlayerPool = pool;
            }

            var formations = new List<KeyValuePair<string, FormationEnduranceStats>>();
            if (Mission != null)
            {
                if (agentsAlive) RefreshFormationStats(Mission.CurrentTime, in r);
                for (int k = 0; k < _formationSnapshot.Length; k++)
                    if (_formationSnapshot[k].Count > 0)
                        formations.Add(new KeyValuePair<string, FormationEnduranceStats>(FormationName(k), _formationSnapshot[k]));
            }

            if (Mission != null && agentsAlive) SampleSpeeds("mission end");

            if (_firstExhausted != null && !_firstDone && !_firstExhausted.Removed && agentsAlive)
            {
                var now = SpeedPenalty.Snapshot.Take(_firstExhausted.Agent);
                TraxLog.Info("speed", "first exhausted fighter (" + Name(_firstExhausted) + ") at mission end: "
                    + (_firstExhausted.Exhausted ? "still exhausted" : "recovered") + ", properties " + now + " (" + now.RatioTo(_firstBefore) + " of the fresh values)");
            }
            else if (_firstExhausted == null)
            {
                TraxLog.Info("speed", "nobody became exhausted this mission - the attack-speed penalty was never needed");
            }

            foreach (var line in _stats.SummaryLines(in r, ActionName, formations))
                TraxLog.Info("summary", line);
        }

        // ------------------------------------------------------------------ wording

        private static string KindName(BlowKind kind) => kind switch
        {
            BlowKind.Melee => "melee",
            BlowKind.Ranged => "ranged",
            BlowKind.Couched => "couched/braced",
            BlowKind.LandedMelee => "melee (landed)",
            BlowKind.LandedRanged => "ranged (landed)",
            _ => kind.ToString(),
        };

        /// <summary>The game's group numbers and names (F1-F8 in the orders menu). FormationClass has
        /// alias values (4 is also NumberOfDefaultFormations), so ToString() cannot be trusted.</summary>
        private static readonly string[] FormationNames =
        {
            "1 Infantry", "2 Archers", "3 Cavalry", "4 Horse archers", "5 Skirmishers", "6 Heavy infantry",
            "7 Light cavalry", "8 Heavy cavalry", "General", "Bodyguard",
        };

        private static string FormationName(int index) =>
            index >= 0 && index < FormationNames.Length ? FormationNames[index] : "formation " + index;

        private static string[]? _actionNames;

        /// <summary><c>ReadyMelee(19)</c> - the FIRST declared name per value (ActionCodeType has aliases
        /// such as AttackMeleeAllBegin = 19, so ToString() may pick the wrong one). Summary only.</summary>
        private static string ActionName(int code)
        {
            try
            {
                if (_actionNames == null)
                {
                    var names = new string[EnduranceStats.ActionSlots];
                    foreach (var f in typeof(Agent.ActionCodeType).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
                    {
                        int v = Convert.ToInt32(f.GetValue(null), CultureInfo.InvariantCulture);
                        if (v >= 0 && v < names.Length && names[v] == null) names[v] = f.Name;
                    }
                    _actionNames = names;
                }
                string? name = code >= 0 && code < _actionNames.Length ? _actionNames[code] : null;
                return (name ?? "action") + "(" + code.ToString(CultureInfo.InvariantCulture) + ")";
            }
            catch
            {
                return code.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>Agent.Name allocates - only for heroes at spawn and for lines that will be written.</summary>
        private static string Name(TrackedAgent st)
        {
            string name = st.HeroName ?? SafeName(st.Agent);
            return st.Agent.IsMainAgent ? name + " (you)" : name;
        }

        private static string SafeName(Agent a)
        {
            try
            {
                return a.Name ?? "(agent " + a.Index + ")";
            }
            catch
            {
                return "(agent " + a.Index + ")";
            }
        }

        private static string Sec(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        private static string F0(double v) => v.ToString("0", CultureInfo.InvariantCulture);

        private static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        private static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    }
}

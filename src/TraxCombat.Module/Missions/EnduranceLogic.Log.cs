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

        internal void WriteEnduranceSummary()
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
                RefreshFormationStats(Mission.CurrentTime, in r);
                for (int k = 0; k < _formationSnapshot.Length; k++)
                    if (_formationSnapshot[k].Count > 0)
                        formations.Add(new KeyValuePair<string, FormationEnduranceStats>(((FormationClass)k).ToString(), _formationSnapshot[k]));
            }

            if (Mission != null) SampleSpeeds("mission end");

            if (_firstExhausted != null && !_firstDone && !_firstExhausted.Removed)
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

        private static string ActionName(int code)
        {
            try
            {
                return ((Agent.ActionCodeType)code).ToString();
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

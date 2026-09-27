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
    /// The Athletics log lines (CLAUDE.md, logging - one playtest must prove every behaviour):
    ///   always   [athletics] mission start (rules), leader rule, party leaders at spawn with their
    ///            pools (limited), first tick (fighters tracked), YOUR pool, dropping below / back to
    ///            full strength, exhaustion, leaving 0, back to full, wounds (limited), Athletics or
    ///            the mod switched mid-mission, pool settings changed; [speed] stat model on top, the
    ///            FIRST exhaustion's properties before → after → leaving 0 → back at full strength,
    ///            the first horse slowed (once per mission each), speed settings changed; the
    ///            [summary] block.
    ///   verbose  per-fighter pool at spawn (athletics-pool), per blow with f (athletics-blow),
    ///            everyone's exhaustion / leaving 0 (athletics-exhaust), back to full
    ///            (athletics-regen), heroes at spawn (athletics-hero), health-cap cuts
    ///            (athletics-health), each speed recompute - fighters and horses (speed-update) -
    ///            each kind rate-limited in its own bucket so one cannot starve the others.
    /// Strings are built only when the line will be written: each call site asks
    /// TraxLog.VerboseWants(its bucket) first - VerboseLogging on AND room in the rate limit (review R8).
    /// </summary>
    public sealed partial class AthleticsLogic
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
                sb.Append(", ").Append(F1(o.Before)).Append(" → ").Append(F1(o.After)).Append(" of ").Append(F0(o.Pool))
                  .Append(" (f ").Append(F2(o.PeakShareBefore)).Append(" → ").Append(F2(o.PeakShareAfter)).Append(')');
                if (o.LeftPeak) sb.Append(" - below full strength");
                if (o.EnteredExhaustion) sb.Append(" - EXHAUSTED");
                TraxLog.Verbose("athletics", sb.ToString(), "athletics-blow");
            }
            catch (Exception e)
            {
                Failed("athletics.log", e);
            }
        }

        /// <summary>Verbose: a common soldier's pool at spawn (heroes and leaders have their own lines).</summary>
        private void LogPoolAtSpawn(TrackedAgent st)
        {
            try
            {
                var r = AthleticsRules.From(TraxSettings.Shared);
                TraxLog.Verbose("athletics", "pool at spawn: " + Name(st) + " - Athletics skill " + st.AthleticsSkill + (st.SkillKnown ? "" : " (not readable)")
                    + " → pool " + F0(AthleticsMath.PoolPoints(in r, st)) + (AthleticsMath.IsAtFloor(in r, st.AthleticsSkill) ? " (the floor)" : "")
                    + "; " + BlowsAtFullStrength(in r, st) + " blows at full strength, " + BlowsToEmpty(in r, st) + " to empty", "athletics-pool");
            }
            catch (Exception e)
            {
                Failed("athletics.log", e);
            }
        }

        /// <param name="agentsAlive">False on the teardown fallback: the agents' native side is gone,
        /// so the formations come from the last tick's snapshot and nothing is sampled.</param>
        internal void WriteAthleticsSummary(bool agentsAlive = true)
        {
            var r = AthleticsRules.From(TraxSettings.Shared);

            // Step 5d: nobody may stay scripted past the mission - release everyone first (through the
            // engine while the agents live), then the summary counts it.
            try
            {
                CloseStepBacks(agentsAlive);
            }
            catch (Exception e)
            {
                Failed("stepback.close", e);
            }
            // Step 5e: likewise every pace hold (our NoAttack) comes off before the summary.
            try
            {
                ClosePace(agentsAlive);
            }
            catch (Exception e)
            {
                Failed("rate.pace-close", e);
            }

            // the lowest any hero reached (heroes' records outlive their removal from the loop)
            TrackedAgent? lowest = null;
            double lowestPoints = double.MaxValue;
            foreach (var h in _heroes)
            {
                double p = h.LowestFraction * AthleticsMath.PoolPoints(in r, h);
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
                _stats.LowestHeroPool = AthleticsMath.PoolPoints(in r, lowest);
            }

            TrackPlayer(in r);
            if (_player != null)
            {
                double pool = AthleticsMath.PoolPoints(in r, _player);
                _stats.PlayerSeen = true;
                _stats.PlayerSkill = _player.AthleticsSkill;
                _stats.PlayerBlows = _player.Blows;
                _stats.PlayerExhaustions = _player.ExhaustionsEntered;
                _stats.PlayerLowestPoints = _player.LowestFraction * pool;
                _stats.PlayerPool = pool;
            }

            var formations = new List<KeyValuePair<string, FormationAthleticsStats>>();
            if (Mission != null)
            {
                if (agentsAlive) RefreshFormationStats(Mission.CurrentTime, in r);
                for (int k = 0; k < _formationSnapshot.Length; k++)
                    if (_formationSnapshot[k].Count > 0)
                        formations.Add(new KeyValuePair<string, FormationAthleticsStats>(FormationName(k), _formationSnapshot[k]));
            }

            if (Mission != null && agentsAlive) SampleSpeeds("mission end");

            if (_firstExhausted != null && !_firstDone && !_firstExhausted.Removed && agentsAlive)
            {
                var now = SpeedPenalty.Snapshot.Take(_firstExhausted.Agent);
                TraxLog.Info("speed", "first exhausted fighter (" + Name(_firstExhausted) + ") at mission end: "
                    + (_firstExhausted.Exhausted ? "still exhausted" : "recovered") + ", properties " + now + " (" + now.RatioTo(FirstFresh) + " of his fresh values)");
            }
            else if (_firstExhausted == null)
            {
                TraxLog.Info("speed", "nobody became exhausted this mission - the attack-speed penalty was never needed");
            }

            foreach (var line in _stats.SummaryLines(in r, ActionName, formations))
                TraxLog.Info("summary", line);

            // Step 5e: the attack rate - every phase and the cycle by f, the holds, the guard. Own try.
            try
            {
                WriteRateSummary();
            }
            catch (Exception e)
            {
                Failed("rate.summary", e);
            }

            // Step 5d: own try - a bug there must not cost the rest of the summary.
            try
            {
                foreach (var line in _stepStats.SummaryLines(StepBackRules.From(TraxSettings.Shared)))
                    TraxLog.Info("summary", line);
            }
            catch (Exception e)
            {
                Failed("stepback.summary", e);
            }

            // Step 16: the AI holds - the techniques, the guard by state, the input hook, the timer surviving a step back.
            try
            {
                WriteHoldSummary();
            }
            catch (Exception e)
            {
                Failed("hold.summary", e);
            }
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

        /// <summary>The game's group numbers and names (F1-F8 in the orders menu; one table in Core,
        /// shared with the orders-menu strip). FormationClass has alias values (4 is also
        /// NumberOfDefaultFormations), so ToString() cannot be trusted.</summary>
        private static string FormationName(int index) => OrderStripMath.FormationName(index);

        private static string[]? _actionNames;

        /// <summary><c>ReadyMelee(19)</c> - the FIRST declared name per value (ActionCodeType has aliases
        /// such as AttackMeleeAllBegin = 19, so ToString() may pick the wrong one). Summary only.</summary>
        private static string ActionName(int code)
        {
            try
            {
                if (_actionNames == null)
                {
                    var names = new string[AthleticsStats.ActionSlots];
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

        private static string F3(double v) => v.ToString("0.000", CultureInfo.InvariantCulture);

        private static string P0(double share) => (share * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
    }
}

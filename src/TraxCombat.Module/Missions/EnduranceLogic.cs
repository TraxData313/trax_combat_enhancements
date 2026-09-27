using System;
using System.Globalization;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TraxCombat.Models;

namespace TraxCombat.Missions
{
    /// <summary>
    /// The mod's per-mission brain, attached to EVERY singleplayer mission in
    /// SubModule.OnMissionBehaviorInitialize (cheap when nobody fights). Lives and dies with the
    /// mission, so the mod adds nothing to the campaign save (save-safe).
    ///
    /// This file: the lifecycle and the log frame -
    ///   [mission] start: scene, mode, battle kind, combat type, game type (AfterStart)
    ///   [mission] first tick / deployment finished: agents on the field
    ///   [summary] block at the end (OnEndMissionInternal - fires for every ending, victory,
    ///             defeat, retreat or leaving, with the agents still present; RESEARCH §K)
    /// The endurance engine (DESIGN §2, step 5) lives in EnduranceLogic.Engine.cs (tracking,
    /// blow detection, regen, the speed penalty, hot swap), its read API for the HUD steps 6-9 in
    /// EnduranceLogic.Api.cs, its log lines and summary in EnduranceLogic.Log.cs.
    ///
    /// Every hook is wrapped: an exception is logged as [error] with its stack and swallowed -
    /// the mission carries on as vanilla (no cost, no penalty).
    /// </summary>
    public sealed partial class EnduranceLogic : MissionLogic
    {
        private int _built;
        private int _builtHumans;
        private int _builtMounts;
        private int _builtHeroes;
        private int _killed;
        private int _unconscious;
        private int _routed;
        private int _otherRemoved;
        private int _errorsAtStart;
        private bool _firstTickDone;
        private bool _summaryWritten;
        private string _label = "(unknown mission)";

        public override void AfterStart()
        {
            try
            {
                base.AfterStart();
                _errorsAtStart = TraxLog.ErrorCount;
                var m = Mission;
                _label = "scene " + Safe(() => m.SceneName) + ", " + Kind(m);
                TraxLog.Info("mission", "start: " + _label
                    + ", mode " + Safe(() => m.Mode.ToString())
                    + ", combat type " + Safe(() => m.CombatType.ToString())
                    + ", game " + Safe(() => Game.Current?.GameType?.GetType().Name ?? "none")
                    + ", agents so far " + Safe(() => m.Agents.Count.ToString(CultureInfo.InvariantCulture)));
            }
            catch (Exception e)
            {
                TraxLog.Error("mission.AfterStart", e);
            }
            try
            {
                // Feature 1: zero the damage stats (the damage model outlives missions), note the
                // main thread, log the damage settings in effect. Before any hit can land.
                DamageRandomizer.OnMissionStart();
            }
            catch (Exception e)
            {
                TraxLog.Error("damage.mission-start", e);
            }
            try
            {
                // Feature 2: become the running endurance logic (the stat decorator and the HUD
                // find us through it), log the rules in effect and which stat model is on top.
                StartEndurance();
            }
            catch (Exception e)
            {
                Failed("endurance.mission-start", e);
            }
        }

        public override void OnMissionTick(float dt)
        {
            if (!_firstTickDone)
            {
                try
                {
                    _firstTickDone = true;
                    TraxLog.Info("mission", "first tick: " + Mission.Agents.Count + " agents active, mode " + Mission.Mode);
                    SweepAgents();
                }
                catch (Exception e)
                {
                    TraxLog.Error("mission.OnMissionTick", e);
                }
            }
            try
            {
                TickEndurance(dt);
            }
            catch (Exception e)
            {
                Failed("endurance.tick", e);
            }
        }

        public override void OnDeploymentFinished()
        {
            try
            {
                TraxLog.Info("mission", "deployment finished: " + Mission.Agents.Count + " agents active, mode " + Mission.Mode);
                SampleSpeeds("deployment finished");
            }
            catch (Exception e)
            {
                TraxLog.Error("mission.OnDeploymentFinished", e);
            }
        }

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            try
            {
                if (agent == null) return;
                _built++;
                if (agent.IsHuman)
                {
                    _builtHumans++;
                    if (agent.IsHero) _builtHeroes++;
                }
                else if (agent.IsMount)
                {
                    _builtMounts++;
                }
            }
            catch (Exception e)
            {
                TraxLog.Error("mission.OnAgentBuild", e);
            }
            try
            {
                if (agent != null && agent.IsHuman) Track(agent);
            }
            catch (Exception e)
            {
                Failed("endurance.agent-build", e);
            }
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            try
            {
                if (affectedAgent == null || !affectedAgent.IsHuman) return;
                switch (agentState)
                {
                    case AgentState.Killed: _killed++; break;
                    case AgentState.Unconscious: _unconscious++; break;
                    case AgentState.Routed: _routed++; break;
                    default: _otherRemoved++; break;
                }
            }
            catch (Exception e)
            {
                TraxLog.Error("mission.OnAgentRemoved", e);
            }
            try
            {
                if (affectedAgent != null && affectedAgent.IsHuman) Untrack(affectedAgent);
            }
            catch (Exception e)
            {
                Failed("endurance.agent-removed", e);
            }
        }

        public override void OnEndMissionInternal()
        {
            try
            {
                WriteSummary("mission end");
            }
            catch (Exception e)
            {
                TraxLog.Error("mission.OnEndMissionInternal", e);
            }
        }

        /// <summary>Fallback: a mission torn down without OnEndMissionInternal still gets its summary.
        /// Also the moment we stop being the running endurance logic.</summary>
        public override void OnRemoveBehavior()
        {
            try
            {
                WriteSummary("behaviour removed");
                StopEndurance();
                base.OnRemoveBehavior();
            }
            catch (Exception e)
            {
                TraxLog.Error("mission.OnRemoveBehavior", e);
            }
        }

        private void WriteSummary(string when)
        {
            if (_summaryWritten) return;
            _summaryWritten = true;
            var m = Mission;

            int activeHumans = 0, activeMounts = 0, playerSide = 0, otherSide = 0;
            if (m != null)
            {
                var playerTeam = m.PlayerTeam;
                foreach (var a in m.Agents)
                {
                    if (a == null) continue;
                    if (a.IsHuman)
                    {
                        activeHumans++;
                        if (playerTeam != null && a.Team != null)
                        {
                            if (a.Team.Side == playerTeam.Side) playerSide++;
                            else otherSide++;
                        }
                    }
                    else if (a.IsMount)
                    {
                        activeMounts++;
                    }
                }
            }

            TraxLog.Info("summary", "==== " + _label + " - ended (" + when + ") after "
                + Safe(() => m!.CurrentTime.ToString("0", CultureInfo.InvariantCulture)) + " s, result: " + Result(m) + " ====");
            TraxLog.Info("summary", "agents built: " + _built + " (" + _builtHumans + " people incl. " + _builtHeroes
                + " heroes, " + _builtMounts + " mounts)");
            TraxLog.Info("summary", "people removed: " + _killed + " killed, " + _unconscious + " knocked out, "
                + _routed + " fled, " + _otherRemoved + " other");
            TraxLog.Info("summary", "still on the field: " + activeHumans + " people (" + playerSide + " on the player's side, "
                + otherSide + " others), " + activeMounts + " mounts");
            // Feature 1 (step 4): rolls per kind, min/avg/max factor, damage before → after, the
            // dice histogram, skips by reason, errors, thread. Own try: a bug there must not cost
            // the rest of the summary.
            try
            {
                DamageRandomizer.WriteSummary();
            }
            catch (Exception e)
            {
                TraxLog.Error("damage.summary", e);
            }
            // Feature 2 (step 5): blows, detection cross-checks, exhaustions, heroes and leaders,
            // the player, formations, regen, the attack-speed measurement, cost. Own try too.
            try
            {
                // Only at the real mission end are the agents still alive (Mission.EndMissionInternal
                // clears their native pointers right after); the fallback reads nothing from them.
                WriteEnduranceSummary(agentsAlive: when == "mission end");
            }
            catch (Exception e)
            {
                Failed("endurance.summary", e);
            }
            TraxLog.Info("summary", "errors logged during this mission: " + (TraxLog.ErrorCount - _errorsAtStart));
            TraxLog.FlushSuppressedCounts();
            TraxLog.Info("summary", "==== end of summary ====");
        }

        private static string Kind(Mission m)
        {
            try
            {
                if (m.IsNavalBattle) return "naval battle";
                if (m.IsNavalRaidBattle) return "naval raid";
                if (m.IsSiegeBattle) return "siege battle";
                if (m.IsSallyOutBattle) return "sally-out battle";
                if (m.IsFieldBattle) return "field battle";
                return "other mission";
            }
            catch
            {
                return "unknown kind";
            }
        }

        private static string Result(Mission? m)
        {
            try
            {
                var r = m?.MissionResult;
                if (r == null) return "none declared (retreat, leave or not a battle)";
                if (r.PlayerVictory) return "player victory";
                if (r.PlayerDefeated) return "player defeated";
                return r.BattleState.ToString();
            }
            catch
            {
                return "unknown";
            }
        }

        private static string Safe(Func<string> read)
        {
            try
            {
                return read() ?? "null";
            }
            catch (Exception e)
            {
                return "(" + e.GetType().Name + ")";
            }
        }
    }
}

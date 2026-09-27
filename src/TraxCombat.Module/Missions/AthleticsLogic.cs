using System;
using System.Globalization;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
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
    ///   the MASTER SWITCH (ModEnabled): "mod ON/OFF" on the start line and the summary header,
    ///             each toggle mid-battle as a [mission] line with its time. This logic stays
    ///             attached and logging while the mod is off, so an OFF battle can be compared
    ///             with an ON one (DESIGN §4).
    /// The Athletics engine (DESIGN §2, step 5) lives in AthleticsLogic.Engine.cs (tracking,
    /// blow detection, regen, the speed penalty, hot swap), its read API for the HUD steps 6-9 in
    /// AthleticsLogic.Api.cs, its log lines and summary in AthleticsLogic.Log.cs, the HUD views'
    /// attach (first tick) and their [summary] lines in AthleticsLogic.Hud.cs (step 6).
    ///
    /// Every hook is wrapped: an exception is logged as [error] with its stack and swallowed -
    /// the mission carries on as vanilla (no cost, no penalty).
    /// </summary>
    public sealed partial class AthleticsLogic : MissionLogic
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
        private readonly ModSwitchLog _modSwitch = new ModSwitchLog();

        public override void AfterStart()
        {
            try
            {
                base.AfterStart();
                _errorsAtStart = TraxLog.ErrorCount;
                var m = Mission;
                _label = "scene " + Safe(() => m.SceneName) + ", " + Kind(m);
                bool modOn = TraxSettings.Shared.ModEnabled;
                _modSwitch.Start(m.CurrentTime, modOn);
                TraxLog.Info("mission", "start: " + _label
                    + ", mode " + Safe(() => m.Mode.ToString())
                    + ", combat type " + Safe(() => m.CombatType.ToString())
                    + ", game " + Safe(() => Game.Current?.GameType?.GetType().Name ?? "none")
                    + ", agents so far " + Safe(() => m.Agents.Count.ToString(CultureInfo.InvariantCulture))
                    + (modOn ? ", mod ON" : ", mod OFF (ModEnabled) - this battle runs as vanilla; the log still records it for comparison"));
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
                // Feature 2: become the running Athletics logic (the stat decorator and the HUD
                // find us through it), log the rules in effect and which stat model is on top.
                StartAthletics();
            }
            catch (Exception e)
            {
                Failed("athletics.mission-start", e);
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
                    NoteStepBackMission();
                }
                catch (Exception e)
                {
                    TraxLog.Error("mission.OnMissionTick", e);
                }
                try
                {
                    // Steps 6-9: the HUD views join the mission screen now (not earlier - RESEARCH §G).
                    AttachHud();
                }
                catch (Exception e)
                {
                    Failed("hud.attach", e);
                }
            }
            try
            {
                // The master switch, read live every tick (MCM flips it from the Escape menu).
                double now = Mission.CurrentTime;
                if (_modSwitch.Observe(now, TraxSettings.Shared.ModEnabled))
                    TraxLog.Info("mission", ModSwitchLog.ToggleText(_modSwitch.IsOn, now));
            }
            catch (Exception e)
            {
                TraxLog.Error("mission.mod-switch", e);
            }
            try
            {
                ReclaimCurrent();
                TickAthletics(dt);
            }
            catch (Exception e)
            {
                Failed("athletics.tick", e);
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
                Failed("athletics.agent-build", e);
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
                Failed("athletics.agent-removed", e);
            }
        }

        /// <summary>Backstop (review 10a R5): an agent deleted without a removal we saw is forgotten too
        /// (no engine call on him). After OnAgentRemoved - the normal path - this finds nothing.</summary>
        public override void OnAgentDeleted(Agent affectedAgent)
        {
            try
            {
                if (affectedAgent != null) Untrack(affectedAgent);
            }
            catch (Exception e)
            {
                Failed("athletics.agent-deleted", e);
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
        /// Also the moment we stop being the running Athletics logic.</summary>
        public override void OnRemoveBehavior()
        {
            try
            {
                WriteSummary("behaviour removed");
            }
            catch (Exception e)
            {
                TraxLog.Error("mission.OnRemoveBehavior", e);
            }
            finally
            {
                // always: a failed fallback summary must not leave this dead mission as "the running
                // Athletics logic" for the decorators and the HUD (review 10a R3)
                StopAthletics();
            }
            base.OnRemoveBehavior();
        }

        private void WriteSummary(string when)
        {
            if (_summaryWritten) return;
            _summaryWritten = true;
            var m = Mission;

            // The header has its own try (review 10a R4): the flag above is already set, so an
            // exception here used to cost the WHOLE summary of the battle.
            try
            {
                int activeHumans = 0, activeMounts = 0, playerSide = 0, otherSide = 0;
                string field;
                try
                {
                    // the native side is read only at the real mission end (the teardown fallback's
                    // agents may be gone - see WriteAthleticsSummary)
                    if (m != null && when == "mission end" && m.Agents != null)
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
                        field = activeHumans + " people (" + playerSide + " on the player's side, " + otherSide + " others), " + activeMounts + " mounts";
                    }
                    else
                    {
                        field = "not read (" + when + ": the agents are gone)";
                    }
                }
                catch (Exception e)
                {
                    field = "not read (" + e.GetType().Name + ")";
                }

                TraxLog.Info("summary", "==== " + _label + " - ended (" + when + ") after "
                    + Safe(() => m!.CurrentTime.ToString("0", CultureInfo.InvariantCulture)) + " s, result: " + Result(m)
                    + ", " + Safe(() => _modSwitch.Describe(m != null ? m.CurrentTime : 0)) + " ====");
                TraxLog.Info("summary", "agents built: " + _built + " (" + _builtHumans + " people incl. " + _builtHeroes
                    + " heroes, " + _builtMounts + " mounts)");
                TraxLog.Info("summary", "people removed: " + _killed + " killed, " + _unconscious + " knocked out, "
                    + _routed + " fled, " + _otherRemoved + " other");
                TraxLog.Info("summary", "still on the field: " + field);
            }
            catch (Exception e)
            {
                TraxLog.Error("mission.summary-header", e);
            }
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
                WriteAthleticsSummary(agentsAlive: when == "mission end");
            }
            catch (Exception e)
            {
                Failed("athletics.summary", e);
            }
            // The HUD (step 6 on): per view - time on screen, layer builds and why they went,
            // colours shown, errors. The screen finalized the views before this runs.
            try
            {
                WriteHudSummary();
            }
            catch (Exception e)
            {
                Failed("hud.summary", e);
            }
            TraxLog.Info("summary", "errors logged during this mission: " + (TraxLog.ErrorCount - _errorsAtStart));
            TraxLog.FlushSuppressedCounts();
            TraxLog.Info("summary", "==== end of summary ====");
            TraxLog.Release(); // between battles nothing holds the log file (the next line reopens it)
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

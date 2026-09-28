using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Missions.MissionLogics;
using TaleWorlds.MountAndBlade.Missions.Objectives;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 19 (Anton, 2026-09-28: "they will come fresh and we will be tired"; DESIGN §2 "A fresh start in a
    /// hideout's boss fight", research in AI_NOTES "Step 19"): the moment a hideout's boss fight BEGINS - the duel
    /// or the battle - every living fighter on the player's side refills to the top he can refill to (wounds
    /// still cap it) and whatever runs on him ends (the attack pause - running, queued or deferred -, the step
    /// back, your own pause).
    ///
    /// THE HOOK (public, polled, no Harmony): both hideout missions start SandBox's DefeatHideoutBossObjective on
    /// the mission's <see cref="MissionObjectiveLogic"/> at the fight's first moment, in the same call that sets the
    /// teams - so the tick sees <see cref="MissionObjectiveLogic.CurrentObjective"/> turn into an objective whose
    /// UniqueId is <see cref="HideoutBossFightMath.BossObjectiveId"/> (one reference compare a tick). Duel or
    /// battle from its name's raw text (<c>TextObject.Value</c> - the text id, any language). Who refills: the
    /// game's own teams at that moment - in a duel the game moves your men to Team.Invalid, so only you refill.
    /// The boss's side is spawned fresh in the intro (tracked full at spawn) and only measured. The intro itself
    /// (the mode turning CutScene in a hideout mission) is noted so the summary can say plainly when a boss phase
    /// played but the hook never fired.
    ///
    /// Fail safe: the watch or the side reads throw → [error] once, NOTHING refilled, the fight goes on as vanilla
    /// Athletics would have it; the summary says so. Gated live: ModEnabled FIRST, AthleticsEnabled,
    /// HideoutBossFightRefill - the moment is always logged, the refill only runs with all three on.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private HideoutBossFightStats _hideout = new HideoutBossFightStats();
        private MissionObjectiveLogic? _objectives;
        private MissionObjective? _lastObjective;
        private bool _hideoutDead;
        private readonly List<TrackedAgent> _freshSide = new List<TrackedAgent>();
        private readonly List<double> _freshHealth = new List<double>();
        private Func<TrackedAgent, HideoutSide>? _sideInGame;

        /// <summary>This mission's hideout boss phase (the smoke reads it).</summary>
        internal HideoutBossFightStats HideoutStats => _hideout;

        /// <summary>The smoke's stand-in for the game's teams (null in game: <see cref="SideInGame"/>).</summary>
        internal Func<TrackedAgent, HideoutSide>? HideoutSideOf { get; set; }

        /// <summary>First tick: a hideout mission (its controller by type NAME - the module does not reference
        /// SandBox)? And the mission's objective logic, watched in every mission (cheap) - the boss objective's id is
        /// the contract, whoever runs the mission.</summary>
        private void NoteHideoutMission()
        {
            try
            {
                var m = Mission;
                foreach (var b in m.MissionBehaviors)
                {
                    string name = b?.GetType().Name ?? string.Empty;
                    if (!HideoutBossFightMath.IsHideoutController(name)) continue;
                    _hideout.Controller = name;
                    break;
                }
                _objectives = m.GetMissionBehavior<MissionObjectiveLogic>();
                if (_hideout.Controller == null) return;
                TraxLog.Info("athletics", "hideout mission (" + _hideout.Controller + "): "
                    + (_objectives != null
                        ? "watching for the boss fight's start (the game's \"Win the Duel\" / \"Win the Fight\" objective)"
                        : "WARNING: no MissionObjectiveLogic - the boss fight's start cannot be seen, nobody will refill (tell Claude)")
                    + " - then the player's side starts fresh (HideoutBossFightRefill " + (TraxSettings.Shared.HideoutBossFightRefill ? "on" : "off") + ", read then)");
            }
            catch (Exception e)
            {
                _hideoutDead = true;
                Failed("hideout.mission", e);
            }
        }

        /// <summary>Every tick, at the top of the Athletics tick (so the releases land before this tick's step-back
        /// and pause passes): the intro by the mission mode (hideouts only), the objective by reference.</summary>
        private void TickHideout(double now)
        {
            if (_hideoutDead) return;
            try
            {
                if (_hideout.Controller != null && !_hideout.IntroSeen && Mission.Mode == MissionMode.CutScene) NoteBossIntro(now);
                var o = _objectives?.CurrentObjective;
                if (ReferenceEquals(o, _lastObjective)) return;
                _lastObjective = o;
                ObserveObjective(o, now);
            }
            catch (Exception e)
            {
                _hideoutDead = true; // the watch stops for this mission: nothing refilled from here on (the summary says so)
                Failed("hideout.watch", e);
            }
        }

        /// <summary>The boss intro began (the game's cutscene mode in a hideout mission).</summary>
        internal void NoteBossIntro(double now)
        {
            if (_hideout.IntroSeen) return;
            _hideout.NoteIntro(now);
            TraxLog.Info("athletics", "hideout: the boss intro began at " + Sec(now) + " s (the cutscene) - the boss and his men spawn now, fresh; "
                + "the player's side starts fresh when the fight itself begins (the duel or the battle)");
        }

        /// <summary>The mission's current objective changed: the boss fight's own objective = the fight began.
        /// Internal: the smoke hands it the game's objective type.</summary>
        internal void ObserveObjective(MissionObjective? o, double now)
        {
            if (o == null || !HideoutBossFightMath.IsBossObjective(o.UniqueId)) return;
            string? raw;
            try
            {
                raw = o.Name?.Value;
            }
            catch
            {
                raw = null; // the kind is only a word in the log - who refills comes from the teams
            }
            BossFightBegan(HideoutBossFightMath.KindOf(raw), now);
        }

        /// <summary>
        /// The boss fight began: the player's side starts fresh. Two passes: the side reads first (engine reads -
        /// an exception there = [error], NOTHING refilled), then the refill (pure) and the releases (each on its own
        /// fail-safe path). Once per mission. Internal: the smoke drives it with its stand-in sides.
        /// </summary>
        internal void BossFightBegan(BossFightKind kind, double now)
        {
            if (!_hideout.BeginFight(now, kind))
            {
                TraxLog.Limited("athletics", "hideout: the boss objective came back at " + Sec(now) + " s - ignored (one fresh start per mission)", "hideout");
                return;
            }
            var settings = TraxSettings.Shared;
            var r = AthleticsRules.From(settings);
            string? off = HideoutBossFightMath.RefillOffBecause(settings);
            var sideOf = HideoutSideOf ?? (_sideInGame ??= SideInGame);
            _freshSide.Clear();
            _freshHealth.Clear();
            try
            {
                for (int i = 0; i < _count; i++)
                {
                    var st = _dense[i];
                    switch (sideOf(st))
                    {
                        case HideoutSide.Player:
                            _hideout.AddPlayerSide(IsPlayer(st), st.Fraction, AthleticsMath.PoolPoints(in r, st));
                            _freshSide.Add(st);
                            double health = HealthOf(st.Agent);
                            _freshHealth.Add(health > 0 ? health : st.Health); // a killing blow in flight: his last known health
                            break;
                        case HideoutSide.Boss:
                            _hideout.AddBossSide(st.Fraction, AthleticsMath.UsableFraction(in r, st));
                            break;
                        case HideoutSide.Aside:
                            _hideout.AddAside();
                            break;
                        default:
                            _hideout.AddGone();
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                _freshSide.Clear();
                _hideout.Failed("hideout.refill");
                Failed("hideout.refill", e);
                TraxLog.Info("athletics", _hideout.FightLine());
                return;
            }

            if (off != null)
            {
                _hideout.NotRefilled(off);
                _freshSide.Clear();
                TraxLog.Info("athletics", _hideout.FightLine());
                return;
            }

            for (int i = 0; i < _freshSide.Count; i++)
            {
                var st = _freshSide[i];
                var o = AthleticsMath.FreshStart(st, in r, _freshHealth[i]);
                _hideout.AddRefill(in o, IsPlayer(st));
                ReleaseForFreshStart(st, now);
                ResetPhases(st);                   // nothing measured spans the fresh start (a cycle, a timer's gap)
                RetargetSpeed(st, in r, exact: true); // full speed back at once (applied by the tick, within its budget)
            }
            _freshSide.Clear();
            TraxLog.Info("athletics", _hideout.FightLine());
        }

        /// <summary>Whatever runs on a man who starts fresh ends, each on its own path: his step back (released to his
        /// formation; a queued one refused), his AI pause (lifted; a queued or deferred one dropped), your pause.</summary>
        private void ReleaseForFreshStart(TrackedAgent st, double now)
        {
            var sb = st.StepBack;
            if (sb != null)
            {
                if (sb.Active)
                {
                    try
                    {
                        Finish(st, StepBackEnd.FreshStart, now, native: true);
                    }
                    catch (Exception e)
                    {
                        Failed("hideout.stepback", e);
                        SafeFinish(st, StepBackEnd.Error, now);
                    }
                    _hideout.StepBacksReleased++;
                }
                if (sb.Pending)
                {
                    _stepPending.Remove(st);
                    Refuse(st, sb, StepBackRefusal.NoLongerEligible);
                    _hideout.StepBacksDropped++;
                }
            }
            var ps = st.Pace;
            if (ps != null)
            {
                if (ps.Active)
                {
                    try
                    {
                        EndHold(st, ps, PaceEnd.FreshStart, now, native: true);
                    }
                    catch (Exception e)
                    {
                        Failed("hideout.pause", e);
                        SafeEndHold(st, now);
                    }
                    _hideout.PausesReleased++;
                }
                if (ps.Pending)
                {
                    ps.Pending = false; // the tick's queue skips it
                    _hideout.PausesDropped++;
                }
                if (ps.Deferred)
                {
                    ps.Deferred = false;
                    _paceDeferred.Remove(st);
                    _hideout.PausesDropped++;
                }
            }
            if (_playerTimer.Holding && ReferenceEquals(_playerTimerOwner, st))
            {
                ReleasePlayerTimer(PlayerTimerEnd.FreshStart, now);
                _hideout.YourPauseReleased = true;
            }
        }

        /// <summary>The game's teams at the fight's start: the player's side (his team or an allied one), the boss's (a
        /// real team that is not the player's), or aside (Team.Invalid - the duel's onlookers). Native reads.</summary>
        private static HideoutSide SideInGame(TrackedAgent st)
        {
            var a = st.Agent;
            if (st.Removed || !a.IsActive()) return HideoutSide.Gone;
            var team = a.Team;
            if (team == null || team.Side == BattleSideEnum.None || !team.IsValid) return HideoutSide.Aside;
            return team.IsPlayerAlly ? HideoutSide.Player : HideoutSide.Boss;
        }

        /// <summary>The [summary] line(s) of the hideout boss phase (none outside a hideout). Internal: the smoke reads them.</summary>
        internal void WriteHideoutSummary()
        {
            foreach (var line in _hideout.SummaryLines()) TraxLog.Info("summary", line);
        }
    }
}

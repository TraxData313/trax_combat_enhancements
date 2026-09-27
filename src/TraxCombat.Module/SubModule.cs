using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ComponentInterfaces;
using TraxCombat.Mcm;
using TraxCombat.Missions;
using TraxCombat.Models;

namespace TraxCombat
{
    /// <summary>
    /// The mod's entry point. What happens when:
    ///   OnSubModuleLoad          - [load] versions/modules/paths; config.json created or read;
    ///                              every setting logged.
    ///   main menu (OnBeforeInitialModuleScreenSetAsRoot) - MCM page registered (if MCM is there).
    ///   OnApplicationTick        - MCM registration retry (1/s until ready); the in-game error notice.
    ///   OnGameStart              - config.json re-read (hand edits); the two model DECORATORS
    ///                              registered (damage, agent stats) - one registration covers
    ///                              campaign, custom battle and naval custom battle (RESEARCH §A).
    ///   OnMissionBehaviorInitialize - config.json re-read; EnduranceLogic attached (SP only).
    /// Every hook is wrapped: an exception is logged as [error] and the game carries on.
    /// </summary>
    public sealed class SubModule : MBSubModuleBase
    {
        private static readonly Stopwatch NoticeClock = Stopwatch.StartNew();
        private static double _lastNoticeAt = -1000;
        private bool _announced;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            try
            {
                LogLoad();
                ConfigStore.Initialize();
            }
            catch (Exception e)
            {
                TraxLog.Error("load.OnSubModuleLoad", e);
            }
        }

        protected override void OnSubModuleUnloaded()
        {
            try
            {
                TraxLog.Info("load", "unloaded (game closing)");
            }
            catch
            {
                // nothing to save
            }
            base.OnSubModuleUnloaded();
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            try
            {
                McmBridge.TryRegister("main menu");
                if (!_announced)
                {
                    _announced = true;
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Trax Combat Enhancements " + ModVersion() + " loaded"
                        + (McmBridge.IsRegistered ? " - settings in Mod Options." : " - settings in config.json.")));
                }
            }
            catch (Exception e)
            {
                TraxLog.Error("load.OnBeforeInitialModuleScreenSetAsRoot", e);
            }
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            try
            {
                McmBridge.Tick();
                ShowErrorNoticeIfAny();
            }
            catch (Exception e)
            {
                TraxLog.Error("load.OnApplicationTick", e);
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            try
            {
                TraxLog.Info("load", "game start: " + (game?.GameType?.GetType().Name ?? "unknown game type"));
                ConfigStore.Reload("game start");
                McmBridge.TryRegister("game start");
                RegisterModels(gameStarterObject);
            }
            catch (Exception e)
            {
                TraxLog.Error("load.OnGameStart", e);
            }
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            try
            {
                if (mission == null || GameNetwork.IsMultiplayer) return;
                ConfigStore.Reload("mission start");
                mission.AddMissionBehavior(new EnduranceLogic());
                TraxLog.Info("mission", "attached: EnduranceLogic (views arrive with steps 6-9)");
            }
            catch (Exception e)
            {
                TraxLog.Error("mission.OnMissionBehaviorInitialize", e);
            }
        }

        // ------------------------------------------------------------------ models

        /// <summary>
        /// The two decorators, each only when the game type already has that model (so
        /// BaseModel is never null). AddModel hands us the previously registered model as
        /// BaseModel; MissionGameModels then picks the last registered - us.
        /// </summary>
        private static void RegisterModels(IGameStarter starter)
        {
            var existing = starter.Models.ToList();

            try
            {
                if (existing.Any(m => m is AgentApplyDamageModel))
                {
                    var damage = new TraxDamageModel();
                    starter.AddModel<AgentApplyDamageModel>(damage);
                    TraxLog.Info("damage", "damage model decorator registered over " + damage.BaseModelName + " (pass-through until step 4)");
                }
                else
                {
                    TraxLog.Info("damage", "this game type has no AgentApplyDamageModel - damage decorator not registered");
                }
            }
            catch (Exception e)
            {
                TraxLog.Error("damage.register", e);
            }

            try
            {
                var statModels = existing.OfType<AgentStatCalculateModel>().ToList();
                if (statModels.Count > 0)
                {
                    var stats = new TraxAgentStatModel(statModels);
                    starter.AddModel<AgentStatCalculateModel>(stats);
                    TraxLog.Info("speed", "agent stat model decorator registered over " + stats.BaseModelName
                        + " (pass-through until step 5); tournament AI-level fix "
                        + (TraxAgentStatModel.AiLevelFixAvailable ? "active over " + statModels.Count + " base model(s)" : "UNAVAILABLE (field not found)"));
                }
                else
                {
                    TraxLog.Info("speed", "this game type has no AgentStatCalculateModel - stat decorator not registered");
                }
            }
            catch (Exception e)
            {
                TraxLog.Error("speed.register", e);
            }
        }

        // ------------------------------------------------------------------ load log

        private static void LogLoad()
        {
            var asm = typeof(SubModule).Assembly;
            TraxLog.Info("load", "==================== Trax Combat Enhancements " + ModVersion() + " ====================");
            TraxLog.Info("load", "dll: " + asm.Location + " (built " + Safe(() => File.GetLastWriteTime(asm.Location).ToString("yyyy.MM.dd HH:mm:ss")) + ")");
            TraxLog.Info("load", "game: " + Safe(() => ApplicationVersion.FromParametersFile().ToString()));
            TraxLog.Info("load", "log: " + ModPaths.LogFilePath);

            string[] modules = Array.Empty<string>();
            try
            {
                modules = Utilities.GetModulesNames() ?? Array.Empty<string>();
            }
            catch (Exception e)
            {
                TraxLog.Error("load.modules", e);
            }
            TraxLog.Info("load", "modules (" + modules.Length + "): " + string.Join(", ", modules));
            if (modules.Any(m => string.Equals(m, "RBM", StringComparison.OrdinalIgnoreCase)))
                TraxLog.Info("load", "WARNING: Realistic Battle Mod (RBM) is enabled - it has its own stamina and posture and patches combat; play with it OFF when testing this mod (DESIGN interpretation 10).");
            int ours = modules.Count(m => m.StartsWith("TraxCombatEnhancements", StringComparison.OrdinalIgnoreCase));
            if (ours > 1)
                TraxLog.Info("load", "WARNING: more than one copy of this mod is enabled (" + string.Join(", ", modules.Where(m => m.StartsWith("TraxCombatEnhancements", StringComparison.OrdinalIgnoreCase)))
                    + ") - enable only one.");
        }

        private static string ModVersion()
        {
            var asm = typeof(SubModule).Assembly;
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            return string.IsNullOrEmpty(info) ? asm.GetName().Version.ToString() : info!;
        }

        /// <summary>One in-game line after an [error] was logged (at most every 30 s), so the
        /// playtester notices and can note the time. Main thread only - hence the tick.</summary>
        private static void ShowErrorNoticeIfAny()
        {
            double now = NoticeClock.Elapsed.TotalSeconds;
            if (now - _lastNoticeAt < 30) return; // the pending flag waits for the next window
            if (!TraxLog.TakeErrorNotice()) return;
            _lastNoticeAt = now;
            InformationManager.DisplayMessage(new InformationMessage(
                "Trax Combat Enhancements: an error was caught and logged (" + TraxLog.ErrorCount + " so far) - see trax_combat.log.",
                Colors.Red));
        }

        private static string Safe(Func<string> read)
        {
            try
            {
                return read() ?? "null";
            }
            catch (Exception e)
            {
                return "(" + e.GetType().Name + ": " + e.Message + ")";
            }
        }
    }
}

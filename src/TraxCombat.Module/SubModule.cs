using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ComponentInterfaces;
using TraxCombat.Core;
using TraxCombat.Mcm;
using Path = System.IO.Path;
using TraxCombat.Missions;
using TraxCombat.Models;

namespace TraxCombat
{
    /// <summary>
    /// The mod's entry point. What happens when:
    ///   OnSubModuleLoad          - [load] versions/modules/paths; config.json created or read;
    ///                              every setting logged.
    ///   main menu (OnBeforeInitialModuleScreenSetAsRoot) - MCM page registered (if MCM is there);
    ///                              the one RBM-incompatibility message (DESIGN §5), if RBM is on;
    ///                              the one "two copies enabled" message (step 11), if another stood down.
    ///   OnApplicationTick        - MCM registration retry after a "not ready" at the main menu (1/s,
    ///                              at most McmPlan.MaxRetries - step 12); the in-game error notice.
    ///   OnGameStart              - config.json re-read (hand edits); the two model DECORATORS
    ///                              registered (damage, agent stats) - one registration covers
    ///                              campaign, custom battle and naval custom battle (RESEARCH §A).
    ///   OnMissionBehaviorInitialize - config.json re-read; AthleticsLogic attached (SP only).
    /// Every hook is wrapped: an exception is logged as [error] and the game carries on.
    ///
    /// ONE COPY RUNS (step 11, Core SingleCopy): with the dev and the release copy both enabled, the
    /// first to load claims the process-wide slot in OnSubModuleLoad and runs; the other is refused and
    /// every hook of it returns at once (<see cref="_inert"/>) - no log line, no config, no MCM page, no
    /// models, no mission logic. The running copy reports it: one [compat] line + one message at the main
    /// menu (or the first game start). An INSTANCE flag, not a static: with the same version both modules
    /// share one assembly (the game's Assembly.LoadFrom), so the statics are shared too.
    /// </summary>
    public sealed class SubModule : MBSubModuleBase
    {
        private static readonly Stopwatch NoticeClock = Stopwatch.StartNew();
        private static double _lastNoticeAt = -1000;
        private static bool _rbmEnabled;
        private static bool _compatNoticeShown;
        private static IReadOnlyList<string> _ourCopies = Array.Empty<string>();
        private static string _selfId = "this copy";
        private static bool _copiesReported;
        private bool _announced;

        /// <summary>This instance is a second copy of the mod and stands down (step 11).</summary>
        private bool _inert;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            try
            {
                // FIRST, before any file is touched: a second copy must not even write the log.
                if (!SingleCopy.TryClaim(this))
                {
                    _inert = true;
                    return;
                }
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
            if (_inert)
            {
                base.OnSubModuleUnloaded();
                return;
            }
            try
            {
                TraxLog.Info("load", "unloaded (game closing)");
                TraxLog.Release();
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
            if (_inert) return;
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
                ShowCompatNoticeOnce("main menu");
                ReportCopiesOnce("main menu");
            }
            catch (Exception e)
            {
                TraxLog.Error("load.OnBeforeInitialModuleScreenSetAsRoot", e);
            }
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            if (_inert) return;
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
            if (_inert) return;
            try
            {
                TraxLog.Info("load", "game start: " + (game?.GameType?.GetType().Name ?? "unknown game type"));
                ConfigStore.Reload("game start");
                McmBridge.TryRegister("game start");
                ShowCompatNoticeOnce("game start");
                ReportCopiesOnce("game start");
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
            if (_inert) return;
            try
            {
                if (mission == null || GameNetwork.IsMultiplayer) return;
                ConfigStore.Reload("mission start");
                mission.AddMissionBehavior(new AthleticsLogic());
                TraxLog.Info("mission", "attached: AthleticsLogic (its HUD views join the mission screen on its first tick - [hud] attached: lines)");
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
                    TraxLog.Info("damage", "damage model decorator registered over " + damage.BaseModelName
                        + " - damage randomness rolls on its result (ApplyGeneralDamageModifiers)");
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
                        + " - scales swing / thrust-and-draw / reload speed by each fighter's Athletics multiplier; tournament AI-level fix "
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
            _selfId = SelfModuleId(asm.Location) ?? "this copy";
            TraxLog.Info("load", "module: " + _selfId + (_selfId == SingleCopy.ReleaseId ? " (the release)" : _selfId.EndsWith(".Dev", StringComparison.OrdinalIgnoreCase) ? " (the dev install - tools\\deploy.ps1)" : ""));
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
            McmBridge.UseModuleList(modules); // step 12: MCM's module off = no page, no retries
            var rbm = modules.Where(m => string.Equals(m, "RBM", StringComparison.OrdinalIgnoreCase)
                                         || m.StartsWith("RBM_", StringComparison.OrdinalIgnoreCase)).ToList();
            _rbmEnabled = rbm.Count > 0;
            TraxLog.Info("compat", _rbmEnabled
                ? "Realistic Battle Mod is ENABLED (" + string.Join(", ", rbm) + ") - NOT compatible (DESIGN §5): it has its own posture and stamina and patches the same combat. The mod still runs; results with both on are not meaningful."
                : "Realistic Battle Mod (RBM) not enabled - good");
            _ourCopies = SingleCopy.CopiesIn(modules);
            var copiesLine = SingleCopy.LoadLine(_ourCopies, _selfId);
            if (copiesLine != null) TraxLog.Info("compat", copiesLine);
        }

        /// <summary>Step 11: at most once per session (main menu, else the first game start), the
        /// running copy says whether another copy of the mod stood down - one [compat] line and one
        /// on-screen message. Nothing when this copy is alone.</summary>
        private static void ReportCopiesOnce(string when)
        {
            if (_copiesReported) return;
            int refused = SingleCopy.Refused();
            var line = SingleCopy.ReportLine(_ourCopies, _selfId, refused);
            if (line == null) return;
            _copiesReported = true;
            TraxLog.Info("compat", line + " (reported at " + when + ")");
            var message = SingleCopy.Message(_ourCopies, _selfId, refused);
            if (message != null) InformationManager.DisplayMessage(new InformationMessage(message, Colors.Yellow));
        }

        /// <summary>The Id in the SubModule.xml of the module this DLL was loaded from (the folder two
        /// levels above bin\Win64_Shipping_Client) - a Workshop copy's folder is a number, so the folder
        /// name alone would not say. Null when it cannot be read (e.g. the offline smoke's build folder).</summary>
        internal static string? SelfModuleId(string dllPath)
        {
            try
            {
                var binDir = Path.GetDirectoryName(dllPath);
                var moduleDir = Path.GetDirectoryName(Path.GetDirectoryName(binDir ?? string.Empty) ?? string.Empty);
                if (string.IsNullOrEmpty(moduleDir)) return null;
                var manifest = Path.Combine(moduleDir, "SubModule.xml");
                return File.Exists(manifest) ? SingleCopy.IdFromManifest(File.ReadAllText(manifest)) : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>DESIGN §5: if RBM is enabled, ONE on-screen message per session - at the main
        /// menu, or at the first game start if the menu was somehow skipped. Never refuses to run.</summary>
        private static void ShowCompatNoticeOnce(string when)
        {
            if (!_rbmEnabled || _compatNoticeShown) return;
            _compatNoticeShown = true;
            InformationManager.DisplayMessage(new InformationMessage(
                "Trax Combat Enhancements is not compatible with Realistic Battle Mod - disable one of them.",
                Colors.Yellow));
            TraxLog.Info("compat", "RBM incompatibility message shown at " + when);
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

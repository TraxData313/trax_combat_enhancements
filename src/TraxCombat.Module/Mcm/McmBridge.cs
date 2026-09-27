using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using TraxCombat.Core;

namespace TraxCombat.Mcm
{
    /// <summary>
    /// The in-game settings page, built with MCM v5's FLUENT builder - a soft dependency.
    ///
    /// THE RULE THIS FILE LIVES BY (CLAUDE.md hard requirement; the sibling mod learned it the
    /// expensive way): the game calls Assembly.GetTypes() on our DLL at startup, and any type
    /// whose base type, interface or FIELD type comes from a missing assembly kills the whole
    /// mod for players without MCM. So MCM types appear in METHOD BODIES ONLY - never as a
    /// base class (no AttributeGlobalSettings), never as a field (the built settings object is
    /// held as <c>object</c>). And no LAMBDA ever takes an MCM-typed parameter: the compiler
    /// caches a non-capturing lambda in a static field of its hidden <c>&lt;&gt;c</c> class,
    /// typed <c>Func&lt;MCM type,…&gt;</c> - an MCM field after all. The builder callbacks are
    /// therefore instance methods taking <c>object</c>, handed over as <c>Action&lt;object&gt;</c>
    /// (delegate contravariance makes that an <c>Action&lt;ISettingsPropertyGroupBuilder&gt;</c>).
    /// tools\AssemblyGuard fails the deploy if any of this slips.
    ///
    /// How it works (RESEARCH §H, §J): every setting is a <c>ProxyRef</c> whose getter reads
    /// <see cref="TraxSettings.Shared"/> and whose setter writes it with source "MCM" - MCM calls
    /// the setter the instant a slider moves, so every change is LIVE (logged as
    /// <c>[config] X: old → new (source: MCM)</c>). Cancel replays the old values through the
    /// same setters. Done raises "SAVE_TRIGGERED" → <see cref="ConfigStore.SaveAfterMcm"/>
    /// writes config.json. Format "none": MCM keeps no copy of its own, so config.json is the
    /// one store and cannot drift from a second file.
    ///
    /// The "default" preset: MCM's page Reset and per-setting reset both apply the preset with
    /// id "default" (MCM.UI SettingsVM.ResetSettings / ResetSettingsValue) - remove it and Reset
    /// silently does nothing. MCM's fluent builder fills that preset with the CURRENT values at
    /// BuildAsGlobal - but its preset builder keeps the FIRST value set per key, so we fill it
    /// with the mod's defaults (defaults.json, via ParamDef.Default) before building: Reset then
    /// really means "the mod's defaults".
    ///
    /// The "Defaults" group (DESIGN §2c, step 5b): two BUTTONS, verified against MCMv5 5.12.3
    /// (the Workshop DLL and its decompile): <c>ISettingsPropertyGroupBuilder.AddButton(id, name,
    /// IRef, content, builder)</c>, the IRef a <c>ProxyRef&lt;Action&gt;</c> with a null setter -
    /// MCM.UI's <c>SettingsPropertyVM.OnValueClick</c> invokes the Action it reads, and a preset or
    /// Reset writing the button's value is ignored (no setter). After a revert the page must show
    /// the new values: every SettingsPropertyVM listens to the settings object's PropertyChanged
    /// and re-reads its value on any name but SAVE_TRIGGERED - so <see cref="RefreshPage"/> raises
    /// one. The revert is not in MCM's undo stack: Cancel does not undo it (the hint says so).
    ///
    /// When it tries (step 12, Core <see cref="McmPlan"/> - Anton's playtest: another mod carried an
    /// MCMv5 DLL with MCM's module off, and the bridge retried "not ready" every second all session):
    /// the first attempt is the main-menu hook (or the first game start) - MCM builds its services in
    /// its own main-menu hook, so earlier tries cannot succeed; no attempt at all when the module list
    /// (<see cref="UseModuleList"/>, from the load) says MCM's module is off - one line; after a "not
    /// ready" the tick retries once a second, at most <see cref="McmPlan.MaxRetries"/> times - then one
    /// line and silence. The mod runs on config.json in every one of these cases.
    /// </summary>
    internal static class McmBridge
    {
        public const string SettingsId = "TraxCombatEnhancements_v1";
        public const string DisplayName = "Trax Combat Enhancements";
        private const string McmAssemblyName = "MCMv5";
        /// <summary>MCM's own id for the preset its Reset buttons apply (BaseSettings.DefaultPresetId).</summary>
        private const string DefaultPresetId = "default";

        /// <summary>The buttons' group (not a schema group - no settings): after every settings group
        /// but Advanced, which stays last (step 10b). MCM's group order is 2 × the schema's
        /// <see cref="ParamGroup.Order"/>, the buttons one less than Advanced's.</summary>
        public const string DefaultsGroupTitle = "Defaults";
        public const string RevertButtonId = "TraxRevertAllToDefaults";
        public const string RevertButtonName = "Revert all to defaults";
        public const string ExportButtonId = "TraxSaveDefaultsFile";
        public const string ExportButtonName = "Save current values as a defaults file";

        /// <summary>A settings group's place on the MCM page: 2 × its schema order, leaving a gap.</summary>
        public static int GroupOrder(ParamGroup group) => 2 * group.Order;

        /// <summary>The Defaults buttons' place: just before Advanced (the last group).</summary>
        public static int DefaultsGroupOrder => GroupOrder(SettingsSchema.AdvancedGroup) - 1;

        /// <summary>What the page raises after a revert so MCM re-reads every value (anything but SAVE_TRIGGERED).</summary>
        private const string RefreshEvent = "TRAX_VALUES_RESET";

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static bool _done;
        private static int _attempts;
        private static int _notReady;
        private static bool _waiting;
        private static double _nextTryAt;

        /// <summary>The enabled modules at load (null = not known - then the bridge just tries).</summary>
        private static IReadOnlyCollection<string>? _modules;

        /// <summary>The built FluentGlobalSettings - deliberately typed object (see the class doc).</summary>
        private static object? _settings;

        public static bool IsRegistered => _settings != null;

        /// <summary>The enabled module ids, read once at load (<c>Utilities.GetModulesNames</c>) - MCM's
        /// own module missing from it means MCM never runs (step 12).</summary>
        public static void UseModuleList(IReadOnlyCollection<string>? modules) => _modules = modules;

        /// <summary>
        /// Registers the page if MCM is loaded, its module enabled, and MCM ready. Call from
        /// OnBeforeInitialModuleScreenSetAsRoot and OnGameStart; after a "not ready" (its builder
        /// factory returns null until its services are up) <see cref="Tick"/> retries once a second, at
        /// most <see cref="McmPlan.MaxRetries"/> times. Without MCM, or with its module off: one line,
        /// never again. Any exception: logged, and the mod runs on the file.
        /// </summary>
        public static void TryRegister(string when)
        {
            if (_done) return;
            try
            {
                var mcm = FindMcm();
                switch (McmPlan.Decide(mcm != null, McmPlan.ModuleEnabled(_modules)))
                {
                    case McmVerdict.NotLoaded:
                        Stop();
                        TraxLog.Info("mcm", "MCM (Mod Configuration Menu) is not loaded - no settings page; the mod runs on config.json alone. That is fine.");
                        return;
                    case McmVerdict.ModuleNotEnabled:
                        Stop();
                        TraxLog.Info("mcm", McmPlan.ModuleNotEnabledLine(Version(mcm)));
                        return;
                }

                _attempts++;
                if (Build())
                {
                    Stop();
                    TraxLog.Info("mcm", "settings page registered at " + when + " (attempt " + _attempts + "): MCM "
                        + Version(mcm) + ", page \"" + DisplayName + "\", " + SettingsSchema.All.Count
                        + " settings in " + SettingsSchema.Groups.Count + " groups, format \"none\" (config.json is the only store), "
                        + "Default preset = the mod's defaults (defaults.json); group \"" + DefaultsGroupTitle + "\": buttons \""
                        + RevertButtonName + "\" and \"" + ExportButtonName + "\".");
                    return;
                }

                _notReady++;
                if (McmPlan.GiveUp(_notReady))
                {
                    Stop();
                    TraxLog.Info("mcm", McmPlan.GiveUpLine(Version(mcm), _attempts));
                    return;
                }
                _waiting = true;
                _nextTryAt = Clock.Elapsed.TotalSeconds + McmPlan.RetrySeconds;
                if (_notReady == 1) TraxLog.Info("mcm", McmPlan.NotReadyLine(Version(mcm), when));
            }
            catch (Exception e)
            {
                Stop();
                _settings = null;
                TraxLog.Error("mcm.register", e);
                TraxLog.Info("mcm", "the settings page could not be built - the mod runs on config.json alone.");
            }
        }

        /// <summary>Cheap retry, from OnApplicationTick: only after an attempt found MCM not ready (never
        /// before the main menu - see the class doc), one a second, until done or given up.</summary>
        public static void Tick()
        {
            if (_done || !_waiting) return;
            double now = Clock.Elapsed.TotalSeconds;
            if (now < _nextTryAt) return;
            _nextTryAt = now + McmPlan.RetrySeconds;
            TryRegister("retry");
        }

        /// <summary>No more attempts, ever (this session).</summary>
        private static void Stop()
        {
            _done = true;
            _waiting = false;
        }

        private static string Version(Assembly? mcm) => mcm?.GetName().Version?.ToString() ?? "?";

        private static Assembly? FindMcm() =>
            AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, McmAssemblyName, StringComparison.OrdinalIgnoreCase));

        /// <summary>The only method that NAMES MCM types (plus the fillers' bodies below). Never
        /// inlined, called only after the MCM assembly is confirmed loaded - so the JIT never
        /// resolves an MCM type on a machine without MCM.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Build()
        {
            var builder = MCM.Abstractions.FluentBuilder.BaseSettingsBuilder.Create(SettingsId, DisplayName);
            if (builder == null) return false; // MCM's services are not up yet - retry later

            builder = builder
                .SetFormat("none")
                .SetFolderName(ModPaths.FolderName)
                .SetOnPropertyChanged(OnSettingsPropertyChanged);

            foreach (var group in SettingsSchema.Groups)
                builder = builder.CreateGroup(group.Title, (Action<object>)new GroupFiller(group).Fill);
            builder = builder.CreateGroup(DefaultsGroupTitle, (Action<object>)new DefaultsButtons().Fill);

            // Fills the EXISTING "default" preset (the name argument is ignored for it) before
            // BuildAsGlobal would fill it with the current values - first value per key wins.
            builder = builder.CreatePreset(DefaultPresetId, "Default", (Action<object>)new PresetFiller().Fill);

            var settings = builder.BuildAsGlobal();
            settings.Register();
            _settings = settings;
            return true;
        }

        /// <summary>MCM's notifications: "SAVE_TRIGGERED" after Done (write the file);
        /// "LOADING_COMPLETE" once at registration (nothing to do - format "none" loads nothing).</summary>
        private static void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            try
            {
                if (e?.PropertyName == "SAVE_TRIGGERED")
                {
                    TraxLog.Info("mcm", "Done pressed - writing config.json");
                    ConfigStore.SaveAfterMcm();
                }
            }
            catch (Exception ex)
            {
                TraxLog.Error("mcm.save", ex);
            }
        }

        // ------------------------------------------------------------------ the two buttons

        /// <summary>"Revert all to defaults" clicked (main thread, MCM's UI): every setting back to
        /// defaults.json, live; config.json rewritten; the page re-reads its values.</summary>
        private static void RevertPressed()
        {
            try
            {
                TraxLog.Info("mcm", "\"" + RevertButtonName + "\" pressed");
                int changed = ConfigStore.RevertAllToDefaults();
                RefreshPage();
                Notify(changed < 0
                        ? "Trax Combat Enhancements: could not revert to the defaults - see trax_combat.log."
                        : "Trax Combat Enhancements: every setting is back to its default (" + changed + " changed) - applied now; config.json saved.",
                    changed < 0);
            }
            catch (Exception e)
            {
                TraxLog.Error("mcm.revert", e);
            }
        }

        /// <summary>"Save current values as a defaults file" clicked: defaults.json beside config.json.</summary>
        private static void ExportPressed()
        {
            try
            {
                TraxLog.Info("mcm", "\"" + ExportButtonName + "\" pressed");
                string? path = ConfigStore.ExportDefaults();
                Notify(path == null
                        ? "Trax Combat Enhancements: could not save the defaults file - see trax_combat.log."
                        : "Trax Combat Enhancements: current values saved as a defaults file: " + path,
                    path == null);
            }
            catch (Exception e)
            {
                TraxLog.Error("mcm.export-defaults", e);
            }
        }

        /// <summary>Every SettingsPropertyVM re-reads its value when the settings object raises
        /// PropertyChanged with any name but SAVE_TRIGGERED (MCM.UI 5.12.3).</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RefreshPage()
        {
            if (_settings is MCM.Abstractions.Base.BaseSettings settings)
                settings.OnPropertyChanged(RefreshEvent);
        }

        private static void Notify(string text, bool failed)
        {
            try
            {
                TaleWorlds.Library.InformationManager.DisplayMessage(new TaleWorlds.Library.InformationMessage(
                    text, failed ? TaleWorlds.Library.Colors.Red : TaleWorlds.Library.Colors.Green));
            }
            catch (Exception e)
            {
                TraxLog.Error("mcm.notify", e);
            }
        }

        /// <summary>The "Defaults" group: the two buttons. Each value is a ProxyRef&lt;Action&gt; whose
        /// getter hands MCM the click handler (instance methods - no MCM-typed lambdas).</summary>
        private sealed class DefaultsButtons
        {
            public void Fill(object groupBuilder)
            {
                var g = (MCM.Abstractions.FluentBuilder.ISettingsPropertyGroupBuilder)groupBuilder;
                g.SetGroupOrder(DefaultsGroupOrder);
                g.AddButton(RevertButtonId, RevertButtonName, new MCM.Common.ProxyRef<Action>(RevertAction, null), "Revert",
                    (Action<object>)ConfigureRevert);
                g.AddButton(ExportButtonId, ExportButtonName, new MCM.Common.ProxyRef<Action>(ExportAction, null), "Save",
                    (Action<object>)ConfigureExport);
            }

            private Action RevertAction() => RevertPressed;

            private Action ExportAction() => ExportPressed;

            public void ConfigureRevert(object b) =>
                ((MCM.Abstractions.FluentBuilder.Models.ISettingsPropertyButtonBuilder)b).SetOrder(0).SetRequireRestart(false).SetHintText(
                    "Puts EVERY setting back to its default - the values in the mod's defaults.json - at once, even mid-battle, "
                    + "and rewrites config.json. Cancel does not undo it.");

            public void ConfigureExport(object b) =>
                ((MCM.Abstractions.FluentBuilder.Models.ISettingsPropertyButtonBuilder)b).SetOrder(1).SetRequireRestart(false).SetHintText(
                    "Writes the values you are playing with as a defaults file (defaults.json, with every explanation) next to "
                    + "config.json in " + ConfigFile.FolderForHumans + " - copy it over the mod's defaults.json to make them the "
                    + "defaults. Changes nothing in the game.");
        }

        /// <summary>Fills one MCM group with its settings, in schema order.</summary>
        private sealed class GroupFiller
        {
            private readonly ParamGroup _group;

            public GroupFiller(ParamGroup group)
            {
                _group = group;
            }

            public void Fill(object groupBuilder)
            {
                var g = (MCM.Abstractions.FluentBuilder.ISettingsPropertyGroupBuilder)groupBuilder;
                g.SetGroupOrder(GroupOrder(_group));
                int order = 0;
                foreach (var p in SettingsSchema.InGroup(_group))
                {
                    var prop = new PropertyFiller(p, order++);
                    switch (p.Type)
                    {
                        case ParamType.Bool:
                            g.AddBool(p.Key, p.Label,
                                new MCM.Common.ProxyRef<bool>(prop.GetBool, prop.SetBool),
                                (Action<object>)prop.Configure);
                            break;
                        case ParamType.Int:
                            g.AddInteger(p.Key, p.Label, (int)p.Min, (int)p.Max,
                                new MCM.Common.ProxyRef<int>(prop.GetInt, prop.SetInt),
                                (Action<object>)prop.Configure);
                            break;
                        default:
                            g.AddFloatingInteger(p.Key, p.Label, (float)p.Min, (float)p.Max,
                                new MCM.Common.ProxyRef<float>(prop.GetFloat, prop.SetFloat),
                                (Action<object>)prop.Configure);
                            break;
                    }
                }
            }
        }

        /// <summary>One setting's getter/setter pair and its MCM property options. The getters
        /// read the live value every time MCM looks; the setters write it at once (hot swap).</summary>
        private sealed class PropertyFiller
        {
            private readonly ParamDef _p;
            private readonly int _order;

            public PropertyFiller(ParamDef p, int order)
            {
                _p = p;
                _order = order;
            }

            public bool GetBool() => TraxSettings.Shared.GetBool(_p);

            public int GetInt() => TraxSettings.Shared.GetInt(_p);

            public float GetFloat() => TraxSettings.Shared.GetFloat(_p);

            public void SetBool(bool value) => Set(value ? 1.0 : 0.0);

            public void SetInt(int value) => Set(value);

            public void SetFloat(float value) => Set(value);

            private void Set(double value)
            {
                try
                {
                    TraxSettings.Shared.Set(_p, value, SettingSources.Mcm);
                }
                catch (Exception e)
                {
                    TraxLog.Error("mcm.set " + _p.Key, e);
                }
            }

            public void Configure(object propertyBuilder)
            {
                switch (_p.Type)
                {
                    case ParamType.Bool:
                        ((MCM.Abstractions.FluentBuilder.Models.ISettingsPropertyBoolBuilder)propertyBuilder)
                            .SetOrder(_order).SetRequireRestart(false).SetHintText(_p.HintText);
                        break;
                    case ParamType.Int:
                        ((MCM.Abstractions.FluentBuilder.Models.ISettingsPropertyIntegerBuilder)propertyBuilder)
                            .SetOrder(_order).SetRequireRestart(false).SetHintText(_p.HintText);
                        break;
                    default:
                        var f = (MCM.Abstractions.FluentBuilder.Models.ISettingsPropertyFloatingIntegerBuilder)propertyBuilder;
                        f.SetOrder(_order).SetRequireRestart(false).SetHintText(_p.HintText);
                        f.AddValueFormat("0.00");
                        break;
                }
            }
        }

        /// <summary>The "Default" preset: DESIGN's defaults, boxed as the exact CLR type each
        /// ProxyRef expects (MCM silently ignores a value of the wrong type).</summary>
        private sealed class PresetFiller
        {
            public void Fill(object presetBuilder)
            {
                var b = (MCM.Abstractions.FluentBuilder.ISettingsPresetBuilder)presetBuilder;
                foreach (var p in SettingsSchema.All)
                {
                    object value = p.Type switch
                    {
                        ParamType.Bool => p.Default != 0,
                        ParamType.Int => (int)p.Default,
                        _ => (float)p.Default,
                    };
                    b.SetPropertyValue(p.Key, value);
                }
            }
        }
    }
}

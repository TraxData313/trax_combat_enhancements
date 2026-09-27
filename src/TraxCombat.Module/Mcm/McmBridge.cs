using System;
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
    /// with DESIGN's defaults before building: Reset then really means "the mod's defaults".
    /// </summary>
    internal static class McmBridge
    {
        public const string SettingsId = "TraxCombatEnhancements_v1";
        public const string DisplayName = "Trax Combat Enhancements";
        private const string McmAssemblyName = "MCMv5";
        /// <summary>MCM's own id for the preset its Reset buttons apply (BaseSettings.DefaultPresetId).</summary>
        private const string DefaultPresetId = "default";
        private const double RetrySeconds = 1.0;

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static bool _done;
        private static int _attempts;
        private static double _nextTryAt;

        /// <summary>The built FluentGlobalSettings - deliberately typed object (see the class doc).</summary>
        private static object? _settings;

        public static bool IsRegistered => _settings != null;

        /// <summary>
        /// Registers the page if MCM is loaded and ready. Call from OnBeforeInitialModuleScreenSetAsRoot
        /// and OnGameStart; <see cref="Tick"/> retries while MCM is loaded but not ready yet
        /// (its builder factory returns null until its services are up). Without MCM: logs that
        /// once and never tries again. Any exception: logged, and the mod runs on the file.
        /// </summary>
        public static void TryRegister(string when)
        {
            if (_done) return;
            try
            {
                var mcm = FindMcm();
                if (mcm == null)
                {
                    _done = true;
                    TraxLog.Info("mcm", "MCM (Mod Configuration Menu) is not loaded - no settings page; the mod runs on config.json alone. That is fine.");
                    return;
                }

                _attempts++;
                if (Build())
                {
                    _done = true;
                    TraxLog.Info("mcm", "settings page registered at " + when + " (attempt " + _attempts + "): MCM "
                        + mcm.GetName().Version + ", page \"" + DisplayName + "\", " + SettingsSchema.All.Count
                        + " settings in " + SettingsSchema.Groups.Count + " groups, format \"none\" (config.json is the only store), "
                        + "Default preset = the mod's defaults.");
                }
                else if (_attempts == 1)
                {
                    TraxLog.Info("mcm", "MCM " + mcm.GetName().Version + " is loaded but not ready yet at " + when + " - retrying every " + RetrySeconds + " s.");
                }
            }
            catch (Exception e)
            {
                _done = true;
                _settings = null;
                TraxLog.Error("mcm.register", e);
                TraxLog.Info("mcm", "the settings page could not be built - the mod runs on config.json alone.");
            }
        }

        /// <summary>Cheap retry, from OnApplicationTick: one attempt per second until done.</summary>
        public static void Tick()
        {
            if (_done) return;
            double now = Clock.Elapsed.TotalSeconds;
            if (now < _nextTryAt) return;
            _nextTryAt = now + RetrySeconds;
            TryRegister("retry");
        }

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
                g.SetGroupOrder(_group.Order);
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

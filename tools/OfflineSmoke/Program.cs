using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using TraxCombat.Core;
using TraxCombat.Mcm;

namespace TraxCombat.Tools
{
    /// <summary>
    /// OFFLINE SMOKE TEST - see OfflineSmoke.csproj. Usage:
    ///   dotnet run --project tools\OfflineSmoke -c Release -- [gameFolder] [mcmBinFolder]
    /// Exit 0 = all checks passed, 1 = a check failed (the temp folder with config.json and the
    /// log is kept and printed for a look), 2 = could not start.
    ///
    /// Order matters: MCM is refused by the assembly resolver until phase 2, exactly like a
    /// player's game without MCM; once loaded it cannot be unloaded.
    /// </summary>
    internal static partial class Program
    {
        private static readonly List<string> Failures = new List<string>();
        private static string _gameFolder = @"C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord";
        private static string _mcmBin = @"C:\Program Files (x86)\Steam\steamapps\workshop\content\261550\2859238197\bin\Win64_Shipping_Client";
        private static bool _allowMcm;
        private static string _dir = string.Empty;

        private static string ConfigPath => Path.Combine(_dir, ConfigFile.FileName);

        private static string LogText => File.Exists(Path.Combine(_dir, ConfigFile.LogFileName))
            ? File.ReadAllText(Path.Combine(_dir, ConfigFile.LogFileName), Encoding.UTF8)
            : string.Empty;

        private static int Main(string[] args)
        {
            if (args.Length > 0) _gameFolder = args[0];
            if (args.Length > 1) _mcmBin = args[1];
            if (!Directory.Exists(_gameFolder))
            {
                Console.Error.WriteLine("game folder not found: " + _gameFolder);
                return 2;
            }
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            _dir = Path.Combine(Path.GetTempPath(), "trax_offline_smoke_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(_dir);
            Console.WriteLine("offline smoke test - temp folder " + _dir);

            // Phase 1 - a player WITHOUT MCM.
            Step("types load without MCM (the game's GetTypes() scan)", TypesLoadWithoutMcm);
            Step("paths point at the temp folder", RedirectPaths);
            Step("MCM absent: the bridge logs it once and stands down", McmAbsent);
            Step("first run: config.json created, every key explained", FirstRun);
            Step("MCM save keeps a hand edit made meanwhile (the rewrite rule)", HandEditSurvivesMcmSave);
            Step("mission-start re-read applies the hand edit", ReloadAppliesHandEdit);
            Step("broken file: kept values, backed up before the next write", BrokenFile);
            Step("missing keys defaulted and added, unknown key kept and reported", MissingAndUnknownKeys);
            Step("log rate limit: a verbose flood is capped", VerboseFloodCapped);
            Step("tournament AI-level fix reads the private field and pushes it down the chain", TournamentAiLevelFix);
            // Damage randomness (step 4) - the real decorator over the game's own custom-battle
            // model, fed the game's own AttackCollisionData / AttackInformation structs.
            Step("damage: every roll lands in [1-p, 1+p] of the game's value, mean ~1, all four kinds roll", DamageRollsThroughDecorator);
            Step("damage: never rolled - shield (toggle off), fall, objects, a 0 hit; a positive hit never below 1", DamageSkipRules);
            Step("damage: settings changed mid-battle apply to the very next hit (hot swap)", DamageHotSwap);
            Step("damage: a bug in the roll keeps the game's value and logs ONE [error] per mission", DamageFailSafe);
            Step("damage: verbose roll/skip lines and the [summary] damage block", DamageLogAndSummary);
            Step("damage: a roll off the main thread is detected and reported", DamageOffMainThread);
            Step("MCM still not loaded after phase 1", () => Check(!McmLoaded(), "MCMv5 got loaded during phase 1"));

            // Phase 2 - a player WITH MCM.
            _allowMcm = true;
            Step("MCM present: the settings page builds through MCM's real fluent builder", McmPageBuilds);
            Step("MCM page: every setting, right type, range, hint, group", McmPageMatchesSchema);
            Step("MCM sliders write the live settings at once (hot swap)", McmSlidersAreLive);
            Step("MCM Reset (the 'default' preset) restores DESIGN's defaults, not the values at build time", McmPresetRestoresDefaults);
            Step("MCM Done writes config.json", McmDoneWritesFile);

            Console.WriteLine();
            if (Failures.Count == 0)
            {
                Console.WriteLine("OFFLINE SMOKE: all checks passed.");
                // TRAX_SMOKE_KEEP=1 keeps the folder, to read the log lines the checks produced.
                if (Environment.GetEnvironmentVariable("TRAX_SMOKE_KEEP") == "1") Console.WriteLine("kept: " + _dir);
                else try { Directory.Delete(_dir, true); } catch { /* temp */ }
                return 0;
            }
            Console.WriteLine("OFFLINE SMOKE: " + Failures.Count + " check(s) FAILED - files kept in " + _dir);
            foreach (var f in Failures) Console.WriteLine("  - " + f);
            return 1;
        }

        // ------------------------------------------------------------------ plumbing

        private static void Step(string name, Action check)
        {
            int before = Failures.Count;
            try
            {
                check();
            }
            catch (Exception e)
            {
                Failures.Add(name + ": threw " + e);
            }
            Console.WriteLine((Failures.Count == before ? "  ok    " : "  FAIL  ") + name);
            for (int i = before; i < Failures.Count; i++) Console.WriteLine("        " + Failures[i]);
        }

        private static void Check(bool condition, string failure)
        {
            if (!condition) Failures.Add(failure);
        }

        private static void LogHas(string text) => Check(LogText.Contains(text), "log lacks: " + text);

        private static bool McmLoaded() =>
            AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "MCMv5");

        /// <summary>Game DLLs from the game's bin and Native's bin; MCM only in phase 2.</summary>
        private static Assembly? Resolve(object sender, ResolveEventArgs e)
        {
            string name = new AssemblyName(e.Name).Name;
            if (name.StartsWith("MCM", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Bannerlord.", StringComparison.OrdinalIgnoreCase)
                || name == "0Harmony")
            {
                if (!_allowMcm) return null; // like a player without MCM (and its Harmony dependency module)
                // MCM's own folder, then its dependency Bannerlord.Harmony (Workshop 2859188632),
                // a sibling folder of MCM's in the Workshop tree.
                string harmonyBin = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(_mcmBin)!)!)!,
                    "2859188632", "bin", "Win64_Shipping_Client");
                foreach (var dir in new[] { _mcmBin, harmonyBin })
                {
                    string candidate = Path.Combine(dir, name + ".dll");
                    if (File.Exists(candidate)) return Assembly.LoadFrom(candidate);
                }
                return null;
            }
            foreach (var dir in new[]
                     {
                         Path.Combine(_gameFolder, "bin", "Win64_Shipping_Client"),
                         Path.Combine(_gameFolder, "Modules", "Native", "bin", "Win64_Shipping_Client"),
                     })
            {
                string candidate = Path.Combine(dir, name + ".dll");
                if (File.Exists(candidate)) return Assembly.LoadFrom(candidate);
            }
            return null;
        }

        private static object? GetStatic(Type t, string field) =>
            t.GetField(field, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(null);

        private static void SetStatic(Type t, string field, object? value) =>
            t.GetField(field, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!.SetValue(null, value);

        // ------------------------------------------------------------------ phase 1

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TypesLoadWithoutMcm()
        {
            var module = typeof(SubModule).Assembly;
            Type[] types;
            try
            {
                types = module.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                Failures.Add("GetTypes() threw - the mod would not load without MCM: "
                    + string.Join("; ", e.LoaderExceptions.Select(x => x?.Message).Distinct()));
                return;
            }
            Check(types.Length > 5, "suspiciously few types: " + types.Length);
            Check(types.Any(t => t.FullName == "TraxCombat.SubModule"), "SubModule type missing");
            Check(!McmLoaded(), "GetTypes() pulled MCMv5 in");
            Check(typeof(TraxSettings).Assembly.GetTypes().Length > 5, "Core types did not load");
        }

        private static void RedirectPaths()
        {
            SetStatic(typeof(ModPaths), "_configDir", _dir);
            Check(ModPaths.ConfigFilePath == ConfigPath, "ModPaths did not take the temp folder");
        }

        private static void McmAbsent()
        {
            McmBridge.TryRegister("smoke without MCM");
            Check(!McmBridge.IsRegistered, "bridge claims a page without MCM");
            LogHas("[mcm] MCM (Mod Configuration Menu) is not loaded");
            McmBridge.TryRegister("second call");
            Check(Occurrences(LogText, "is not loaded") == 1, "the 'MCM absent' line should be logged once");
        }

        private static void FirstRun()
        {
            ConfigStore.Initialize();
            Check(File.Exists(ConfigPath), "config.json not created");
            string text = File.ReadAllText(ConfigPath, Encoding.UTF8);
            var read = ConfigFile.Read(text);
            Check(read.Ok && read.Values.Count == SettingsSchema.All.Count && read.Issues.Count == 0,
                "created file does not read back cleanly: " + read.Error);
            Check(text.Contains("// Trax Combat Enhancements - settings"), "header missing");
            Check(Occurrences(text, "  // (default ") == SettingsSchema.All.Count, "not every key has its default/range comment");
            LogHas("[config] first run: created config.json");
            LogHas("[config] settings in effect (" + SettingsSchema.All.Count);
            LogHas("[config]   DamageRandomPercent = 50");
            LogHas("[config]   VerboseLogging = false");
        }

        private static void HandEditSurvivesMcmSave()
        {
            // The player edits the file by hand while the game runs...
            File.WriteAllText(ConfigPath, File.ReadAllText(ConfigPath).Replace("\"DamageRandomPercent\": 50", "\"DamageRandomPercent\": 30"));
            // ...then flips another setting in MCM and presses Done.
            TraxSettings.Shared.Set(SettingsSchema.VerboseLogging, true, SettingSources.Mcm);
            LogHas("[config] VerboseLogging: false → true (source: MCM)");
            ConfigStore.SaveAfterMcm();

            var disk = ConfigFile.Read(File.ReadAllText(ConfigPath));
            Check(disk.Values["DamageRandomPercent"] == 30, "the hand edit was lost by the MCM save");
            Check(disk.Values["VerboseLogging"] == 1, "the MCM change was not written");
            Check(TraxSettings.Shared.DamageRandomPercent == 50, "memory took the hand edit before the next battle");
            LogHas("from MCM VerboseLogging; kept hand edit(s) from the file that apply at the next battle start: DamageRandomPercent = 30 (now 50)");
        }

        private static void ReloadAppliesHandEdit()
        {
            ConfigStore.Reload("mission start");
            Check(TraxSettings.Shared.DamageRandomPercent == 30, "hand edit not applied at mission start");
            LogHas("[config] DamageRandomPercent: 50 → 30 (source: file)");
            LogHas("[config] config.json re-read at mission start: 1 change(s)");
            ConfigStore.Reload("mission start");
            LogHas("[config] config.json re-read at mission start: no changes");
        }

        private static void BrokenFile()
        {
            File.WriteAllText(ConfigPath, "{\r\n  \"HeroCostMultiplier\": 0,75\r\n}\r\n");
            int version = TraxSettings.Shared.Version;
            ConfigStore.Reload("mission start");
            Check(TraxSettings.Shared.Version == version, "a broken file changed values");
            LogHas("[config] could not read config.json at mission start (line");

            TraxSettings.Shared.Set(SettingsSchema.ShowTargetBar, false, SettingSources.Mcm);
            ConfigStore.SaveAfterMcm();
            Check(Directory.GetFiles(_dir, ConfigFile.FileName + ".broken-*").Length == 1, "broken file not backed up");
            var disk = ConfigFile.Read(File.ReadAllText(ConfigPath));
            Check(disk.Ok && disk.Values.Count == SettingsSchema.All.Count, "fresh file after a broken one is not complete");
            Check(disk.Values["ShowTargetBar"] == 0 && disk.Values["DamageRandomPercent"] == 30, "fresh file lost the values in effect");
            LogHas("did not parse");
        }

        private static void MissingAndUnknownKeys()
        {
            File.WriteAllText(ConfigPath, "{ \"damageRandomPercent\": 20, \"DamageRandomPercnt\": 5, \"ConfigVersion\": 1 }");
            ConfigStore.Reload("game start");
            Check(TraxSettings.Shared.DamageRandomPercent == 20, "value with odd key casing not applied");
            Check(TraxSettings.Shared.ShowTargetBar, "missing key did not go back to its default");
            Check(!TraxSettings.Shared.VerboseLogging, "missing key did not go back to its default (VerboseLogging)");
            LogHas("\"DamageRandomPercnt\" is not a setting of this version");
            LogHas("added " + (SettingsSchema.All.Count - 1) + " missing setting(s)");
            var disk = ConfigFile.Read(File.ReadAllText(ConfigPath));
            Check(disk.Values.Count == SettingsSchema.All.Count && disk.Values["DamageRandomPercent"] == 20, "missing keys not written in");
            Check(disk.Unknown.Count == 1 && disk.Unknown[0].Key == "DamageRandomPercnt", "unknown key not carried along");
        }

        private static void VerboseFloodCapped()
        {
            TraxSettings.Shared.Set(SettingsSchema.VerboseLogging, true, SettingSources.File);
            int before = Occurrences(LogText, "[damage] smoke roll");
            for (int i = 0; i < 5000; i++) TraxLog.Verbose("damage", "smoke roll " + i);
            int written = Occurrences(LogText, "[damage] smoke roll") - before;
            Check(written >= 40 && written < 200, "verbose flood not capped: " + written + " lines");
            TraxLog.FlushSuppressedCounts();
            LogHas("more verbose [damage] lines were suppressed by the rate limit");
            TraxSettings.Shared.Set(SettingsSchema.VerboseLogging, false, SettingSources.File);
            TraxLog.Verbose("damage", "must not appear");
            Check(!LogText.Contains("must not appear"), "verbose line written with VerboseLogging off");
            ConfigStore.Reload("after flood"); // file and memory in step again
        }

        /// <summary>TournamentBehavior calls SetAILevelMultiplier on the TOP stat model (us); the
        /// decorator must read its own private _AILevelMultiplier (compiled accessor against the
        /// real TaleWorlds.MountAndBlade.dll) and push it into every model below it.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TournamentAiLevelFix()
        {
            Check(Models.TraxAgentStatModel.AiLevelFixAvailable, "the _AILevelMultiplier accessor could not be built");
            var below = new Models.TraxAgentStatModel(Array.Empty<TaleWorlds.MountAndBlade.AgentStatCalculateModel>()); // stands in for Sandbox's model
            var top = new Models.TraxAgentStatModel(new TaleWorlds.MountAndBlade.AgentStatCalculateModel[] { below });
            var read = (Func<TaleWorlds.MountAndBlade.AgentStatCalculateModel, float>)GetStatic(typeof(Models.TraxAgentStatModel), "ReadAiLevel")!;
            var sync = typeof(Models.TraxAgentStatModel).GetMethod("SyncAiLevel", BindingFlags.Instance | BindingFlags.NonPublic)!;

            top.SetAILevelMultiplier(1f + 2f / 3f); // what TournamentBehavior does at round 2
            Check(Math.Abs(read(top) - 1.6667f) < 1e-3, "accessor read " + read(top));
            Check(Math.Abs(read(below) - 1f) < 1e-6, "base model changed before the sync");
            sync.Invoke(top, null);
            Check(Math.Abs(read(below) - 1.6667f) < 1e-3, "multiplier not pushed to the base model: " + read(below));
            LogHas("[speed] tournament AI level multiplier 1 → 1.67 passed on to 1 base stat model(s)");

            top.ResetAILevelMultiplier(); // tournament over
            sync.Invoke(top, null);
            Check(Math.Abs(read(below) - 1f) < 1e-6, "reset not pushed to the base model");
        }

        private static int Occurrences(string text, string what)
        {
            int n = 0, at = 0;
            while ((at = text.IndexOf(what, at, StringComparison.Ordinal)) >= 0) { n++; at += what.Length; }
            return n;
        }

        // ------------------------------------------------------------------ phase 2 (MCM present)

        private static void McmPageBuilds()
        {
            // Precondition for the Reset check: a non-default value is live while the page is
            // built (MCM would otherwise bake it into its "default" preset).
            Check(TraxSettings.Shared.DamageRandomPercent != (int)SettingsSchema.DamageRandomPercent.Default, "precondition: expected a non-default value before the build");
            Assembly.LoadFrom(Path.Combine(_mcmBin, "MCMv5.dll"));
            McmHarness.InstallServices();
            SetStatic(typeof(McmBridge), "_done", false); // phase 1 stood it down
            McmBridge.TryRegister("smoke with MCM");
            Check(McmBridge.IsRegistered, "page not registered with MCM present");
            LogHas("[mcm] settings page registered at smoke with MCM (attempt 1): MCM 5.");
        }

        private static void McmPageMatchesSchema() => McmHarness.PageMatchesSchema(GetStatic(typeof(McmBridge), "_settings")!);

        private static void McmSlidersAreLive() => McmHarness.SlidersAreLive(GetStatic(typeof(McmBridge), "_settings")!, LogHas);

        private static void McmPresetRestoresDefaults() => McmHarness.PresetRestoresDefaults(GetStatic(typeof(McmBridge), "_settings")!);

        private static void McmDoneWritesFile()
        {
            McmHarness.PressDone(GetStatic(typeof(McmBridge), "_settings")!);
            LogHas("[mcm] Done pressed - writing config.json");
            var disk = ConfigFile.Read(File.ReadAllText(ConfigPath));
            foreach (var p in SettingsSchema.All)
                Check(Math.Abs(disk.Values[p.Key] - TraxSettings.Shared.Get(p)) < 1e-9, "after Done the file differs from memory at " + p.Key);
        }

        /// <summary>Everything that NAMES an MCM type - JIT-compiled only in phase 2.</summary>
        private static class McmHarness
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void InstallServices()
            {
                var gsp = typeof(BUTR.DependencyInjection.GenericServiceProvider);
                gsp.GetField("GlobalServiceProvider", BindingFlags.Static | BindingFlags.NonPublic)!
                    .SetValue(null, new MiniProvider());
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void PageMatchesSchema(object settingsObject)
            {
                var settings = (MCM.Abstractions.Base.BaseSettings)settingsObject;
                Check(settings.Id == McmBridge.SettingsId, "settings id " + settings.Id);
                Check(settings.FormatType == "none", "format is " + settings.FormatType + ", not none");
                var defs = MCM.Abstractions.BaseSettingsExtensions.GetAllSettingPropertyDefinitions(settings).ToList();
                Check(defs.Count == SettingsSchema.All.Count, "MCM page has " + defs.Count + " settings, schema " + SettingsSchema.All.Count);
                foreach (var p in SettingsSchema.All)
                {
                    var d = defs.FirstOrDefault(x => x.Id == p.Key);
                    if (d == null) { Failures.Add("MCM page lacks " + p.Key); continue; }
                    var expected = p.Type switch
                    {
                        ParamType.Bool => MCM.Abstractions.SettingType.Bool,
                        ParamType.Int => MCM.Abstractions.SettingType.Int,
                        _ => MCM.Abstractions.SettingType.Float,
                    };
                    Check(d.SettingType == expected, p.Key + ": MCM type " + d.SettingType);
                    Check(d.DisplayName == p.Label, p.Key + ": label " + d.DisplayName);
                    Check(d.HintText == p.HintText, p.Key + ": hint differs");
                    Check(!d.RequireRestart, p.Key + ": marked require-restart");
                    Check(d.GroupName == p.Group.Title, p.Key + ": group " + d.GroupName);
                    Check(Math.Abs(Convert.ToDouble(d.PropertyReference.Value) - TraxSettings.Shared.Get(p)) < 1e-4,
                        p.Key + ": getter does not read the live value");
                    if (p.Type != ParamType.Bool)
                        Check((double)d.MinValue == p.Min && (double)d.MaxValue == p.Max, p.Key + ": range " + d.MinValue + ".." + d.MaxValue);
                }
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void SlidersAreLive(object settingsObject, Action<string> logHas)
            {
                var settings = (MCM.Abstractions.Base.BaseSettings)settingsObject;
                var defs = MCM.Abstractions.BaseSettingsExtensions.GetAllSettingPropertyDefinitions(settings).ToDictionary(d => d.Id);

                int oldPercent = TraxSettings.Shared.DamageRandomPercent;
                defs["DamageRandomPercent"].PropertyReference.Value = 40;          // an int slider
                Check(TraxSettings.Shared.DamageRandomPercent == 40, "int slider did not reach the live settings");
                logHas("[config] DamageRandomPercent: " + oldPercent + " → 40 (source: MCM)");

                defs["HeroCostMultiplier"].PropertyReference.Value = 0.5f;         // a float slider
                Check(Math.Abs(TraxSettings.Shared.HeroCostMultiplier - 0.5f) < 1e-6, "float slider did not reach the live settings");

                defs["ShowPlayerBar"].PropertyReference.Value = false;             // a checkbox
                Check(!TraxSettings.Shared.ShowPlayerBar, "checkbox did not reach the live settings");

                Check(Convert.ToInt32(defs["DamageRandomPercent"].PropertyReference.Value) == 40, "getter does not read back the live value");
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void PresetRestoresDefaults(object settingsObject)
            {
                var settings = (MCM.Abstractions.Base.BaseSettings)settingsObject;
                var presets = settings.GetBuiltInPresets().ToList();
                // MCM.UI's Reset = ChangePreset("default"): the preset with that id, applied onto the page.
                var reset = presets.SingleOrDefault(p => p.Id == "default");
                if (reset == null) { Failures.Add("no \"default\" preset - MCM's Reset buttons would do nothing"); return; }
                MCM.Abstractions.SettingsUtils.OverrideValues(settings, reset.LoadPreset());
                foreach (var p in SettingsSchema.All)
                    Check(Math.Abs(TraxSettings.Shared.Get(p) - p.Default) < 1e-6, "preset left " + p.Key + " at " + p.Format(TraxSettings.Shared.Get(p)));
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void PressDone(object settingsObject)
            {
                var settings = (MCM.Abstractions.Base.BaseSettings)settingsObject;
                settings.OnPropertyChanged("SAVE_TRIGGERED"); // what BaseSettingsContainer.SaveSettings raises after Done
            }

            /// <summary>Just enough of MCM's service container: its own (internal) fluent builder
            /// factory, and the fluent property discoverer the menu uses to list a page's settings.
            /// Everything else asked for is "not available" (so Register() is a no-op here).</summary>
            private sealed class MiniProvider : BUTR.DependencyInjection.IGenericServiceProvider
            {
                private static readonly Assembly Mcm = typeof(MCM.Abstractions.FluentBuilder.ISettingsBuilder).Assembly;

                private readonly object _factory = Activator.CreateInstance(
                    Mcm.GetType("MCM.Implementation.FluentBuilder.DefaultSettingsBuilderFactory", true)!, true)!;

                private readonly MCM.Abstractions.Properties.ISettingsPropertyDiscoverer[] _discoverers =
                {
                    (MCM.Abstractions.Properties.ISettingsPropertyDiscoverer)Activator.CreateInstance(
                        Mcm.GetType("MCM.Implementation.FluentSettingsPropertyDiscoverer", true)!, true)!,
                };

                public BUTR.DependencyInjection.IGenericServiceProviderScope CreateScope() => throw new NotSupportedException();

                public TService? GetService<TService>() where TService : class
                {
                    if (typeof(TService) == typeof(MCM.Abstractions.FluentBuilder.ISettingsBuilderFactory)) return (TService)_factory;
                    if (typeof(TService) == typeof(IEnumerable<MCM.Abstractions.Properties.ISettingsPropertyDiscoverer>)) return (TService)(object)_discoverers;
                    return null;
                }

                public void Dispose()
                {
                }
            }
        }
    }
}

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
            Step("defaults: every default from the defaults.json embedded in the DLL, logged, in the first-run file", DefaultsFromTheEmbeddedFile);
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
            // Athletics (steps 5, 5c) - the real speed penalties, stat decorator, Athletics logic and
            // damage decorator, fed uninitialized Agent objects (no native side).
            Step("Athletics: the attack, run and horse levers each scale only their own AgentDrivenProperties", SpeedPenaltyScalesOnlyItsOwnValues);
            Step("Athletics: the stat decorator applies each fighter's attack and run multipliers and a slowed rider's horse's, on every recompute, never compounds, lifts them when off", DecoratorAppliesEachFightersMultipliers);
            Step("Athletics: swings through the real logic - a recruit (floor 50) 2 blows at full strength, slower every blow, empty on the 5th; a 300-skill party leader 14 and 54; kicks, bashes, ranged releases free; the attack-speed check by f", BlowsThroughTheLogic);
            Step("Athletics: health caps the bar at once, the peak line stays on the full pool, the cap switch works live", HealthCapsTheBar);
            Step("Athletics: the damage upside follows the attacker's f through the real damage decorator (empty: never above x1.00; untracked: full)", DamageUpsideFollowsTheAttacker);
            Step("Athletics: mid-battle settings - speeds, peak line, horses, pool size (shares kept) re-target live; switching off refills and lifts", AthleticsHotSwap);
            Step("Athletics: the [summary] Athletics block", AthleticsSummary);
            // The step back (step 5d) - the real logic's bookkeeping with a stand-in for the engine side.
            Step("step back: rolled at every AI swing's end by f (0% at full strength, 100% empty), started from the tick, timed live, capped, refused by the safety checks, released on every path (time, order, hand-over untouched, left the field, switched off, mission end), logged and summarised", StepBackThroughTheLogic);
            Step("master switch: ModEnabled off is vanilla at once - damage unrolled but recorded, penalties lifted, no costs, every step back released, the Athletics bar gone; on = everyone full, the bar back", MasterSwitchIsVanillaLive);
            Step("Athletics: a failure is logged once per site, counted, and reported in the summary", AthleticsFailSafe);
            // The HUD (step 6) - the prefab against the game's own types and files, the real view and
            // ViewModel driven by made-up frames with a stand-in layer.
            Step("HUD prefab: well-formed; every element a widget type of the game, every attribute a real property with a valid value, only vanilla brushes and sprites, every @binding typed exactly like its ViewModel property, every ViewModel property drawn", HudPrefabIsValid);
            Step("HUD player bar: through the real view - hidden outside fights, built in battle with you on the field, number / fill / colours green → blue → yellow → orange → red / peak marker / wounded part / Exhausted pushed and logged, refreshed every HudRefreshSeconds (live), layout live, removed and rebuilt with its reason (Hide battle UI, ShowPlayerBar, AthleticsEnabled, photo mode, no player, not a fight, ModEnabled), stealth/tournament/duel are fights, suspended, paused, mission end, the [summary] hud lines", HudPlayerBarThroughTheView);
            Step("HUD fail safe: an exception, a movie that does not load, a failure with the bar up or a view never attached - the view is disabled for the mission, its layer removed, [error] logged, nothing thrown", HudFailSafe);
            Step("MCM still not loaded after phase 1", () => Check(!McmLoaded(), "MCMv5 got loaded during phase 1"));

            // Phase 2 - a player WITH MCM.
            _allowMcm = true;
            Step("MCM present: the settings page builds through MCM's real fluent builder", McmPageBuilds);
            Step("MCM page: every setting, right type, range, hint, group", McmPageMatchesSchema);
            Step("MCM sliders write the live settings at once (hot swap)", McmSlidersAreLive);
            Step("MCM Reset (the 'default' preset) restores the mod's defaults (defaults.json), not the values at build time", McmPresetRestoresDefaults);
            Step("MCM Done writes config.json", McmDoneWritesFile);
            Step("MCM buttons: \"Save current values as a defaults file\" writes a clean defaults.json beside config.json; \"Revert all to defaults\" is live, logged, rewrites config.json, refreshes the page", McmButtons);

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

        // The config checks below work for ANY default in defaults.json (Anton tunes it): they
        // read the default and pick hand-edit values that differ from it.
        private static readonly ParamDef Pct = SettingsSchema.DamageRandomPercent;
        private static int PctDefault => (int)Pct.Default;
        private static int HandPct => PctDefault == 30 ? 35 : 30;
        private static bool VerboseDefault => SettingsSchema.VerboseLogging.Default != 0;
        private static string OnOff(bool b) => b ? "true" : "false";

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
            LogHas("[config]   DamageRandomPercent = " + PctDefault);
            LogHas("[config]   VerboseLogging = " + OnOff(VerboseDefault));
        }

        /// <summary>DESIGN 2c: the real DLL takes every default from the defaults.json embedded in
        /// TraxCombat.Core.dll, logs it, and the first-run file carries exactly those values.</summary>
        private static void DefaultsFromTheEmbeddedFile()
        {
            Check(DefaultsFile.Problems.Count == 0, "embedded defaults.json problems: " + string.Join("; ", DefaultsFile.Problems));
            LogHas("[config] defaults: defaults.json (embedded in TraxCombat.Core.dll), " + SettingsSchema.All.Count
                + " keys for " + SettingsSchema.All.Count + " settings - every default read from it");
            var embedded = DefaultsFile.Check(DefaultsFile.ReadEmbeddedText());
            Check(embedded.Ok, "the embedded defaults.json does not check clean: " + embedded.Describe());
            var created = ConfigFile.Read(File.ReadAllText(ConfigPath, Encoding.UTF8));
            foreach (var p in SettingsSchema.All)
            {
                if (!embedded.Values.TryGetValue(p.Key, out double v)) continue;
                Check(Math.Abs(p.Default - v) < 1e-9, p.Key + ": schema default " + p.Format(p.Default) + " vs defaults.json " + p.Format(v));
                Check(created.Values.TryGetValue(p.Key, out double c) && Math.Abs(c - v) < 1e-9, p.Key + ": first-run config.json does not hold the defaults.json value");
            }
        }

        private static void HandEditSurvivesMcmSave()
        {
            // The player edits the file by hand while the game runs...
            File.WriteAllText(ConfigPath, File.ReadAllText(ConfigPath).Replace("\"DamageRandomPercent\": " + PctDefault, "\"DamageRandomPercent\": " + HandPct));
            // ...then flips another setting in MCM and presses Done.
            TraxSettings.Shared.Set(SettingsSchema.VerboseLogging, !VerboseDefault, SettingSources.Mcm);
            LogHas("[config] VerboseLogging: " + OnOff(VerboseDefault) + " → " + OnOff(!VerboseDefault) + " (source: MCM)");
            ConfigStore.SaveAfterMcm();

            var disk = ConfigFile.Read(File.ReadAllText(ConfigPath));
            Check(disk.Values["DamageRandomPercent"] == HandPct, "the hand edit was lost by the MCM save");
            Check(disk.Values["VerboseLogging"] == (VerboseDefault ? 0 : 1), "the MCM change was not written");
            Check(TraxSettings.Shared.DamageRandomPercent == PctDefault, "memory took the hand edit before the next battle");
            LogHas("from MCM VerboseLogging; kept hand edit(s) from the file that apply at the next battle start: DamageRandomPercent = " + HandPct + " (now " + PctDefault + ")");
        }

        private static void ReloadAppliesHandEdit()
        {
            ConfigStore.Reload("mission start");
            Check(TraxSettings.Shared.DamageRandomPercent == HandPct, "hand edit not applied at mission start");
            LogHas("[config] DamageRandomPercent: " + PctDefault + " → " + HandPct + " (source: file)");
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
            Check(disk.Values["ShowTargetBar"] == 0 && disk.Values["DamageRandomPercent"] == HandPct, "fresh file lost the values in effect");
            LogHas("did not parse");
        }

        private static void MissingAndUnknownKeys()
        {
            int odd = PctDefault == 20 ? 25 : 20; // must differ from the default (McmPageBuilds needs a non-default value)
            File.WriteAllText(ConfigPath, "{ \"damageRandomPercent\": " + odd + ", \"DamageRandomPercnt\": 5, \"ConfigVersion\": 1 }");
            ConfigStore.Reload("game start");
            Check(TraxSettings.Shared.DamageRandomPercent == odd, "value with odd key casing not applied");
            Check(TraxSettings.Shared.ShowTargetBar == (SettingsSchema.ShowTargetBar.Default != 0), "missing key did not go back to its default");
            Check(TraxSettings.Shared.VerboseLogging == VerboseDefault, "missing key did not go back to its default (VerboseLogging)");
            LogHas("\"DamageRandomPercnt\" is not a setting of this version");
            LogHas("added " + (SettingsSchema.All.Count - 1) + " missing setting(s)");
            var disk = ConfigFile.Read(File.ReadAllText(ConfigPath));
            Check(disk.Values.Count == SettingsSchema.All.Count && disk.Values["DamageRandomPercent"] == odd, "missing keys not written in");
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

        private static void McmButtons() =>
            McmHarness.Buttons(GetStatic(typeof(McmBridge), "_settings")!, ConfigPath, _dir, LogHas);

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
                var all = MCM.Abstractions.BaseSettingsExtensions.GetAllSettingPropertyDefinitions(settings).ToList();
                var defs = all.Where(d => d.SettingType != MCM.Abstractions.SettingType.Button).ToList();
                Check(defs.Count == SettingsSchema.All.Count, "MCM page has " + defs.Count + " settings, schema " + SettingsSchema.All.Count);
                // DESIGN 2c: the "Defaults" group with its two buttons, after every settings group.
                var buttons = all.Where(d => d.SettingType == MCM.Abstractions.SettingType.Button).ToList();
                Check(buttons.Count == 2, "MCM page has " + buttons.Count + " buttons, expected 2");
                var revert = buttons.FirstOrDefault(b => b.Id == McmBridge.RevertButtonId);
                var export = buttons.FirstOrDefault(b => b.Id == McmBridge.ExportButtonId);
                Check(revert != null && revert.DisplayName == McmBridge.RevertButtonName && revert.Content == "Revert"
                    && revert.GroupName == McmBridge.DefaultsGroupTitle && revert.HintText.Contains("defaults.json"), "the revert button is missing or wrong");
                Check(export != null && export.DisplayName == McmBridge.ExportButtonName && export.Content == "Save"
                    && export.GroupName == McmBridge.DefaultsGroupTitle && export.HintText.Contains("defaults.json"), "the export button is missing or wrong");
                Check(buttons.All(b => b.PropertyReference.Value is Action), "a button does not hand MCM an Action to invoke");
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
                int slid = oldPercent == 40 ? 45 : 40;
                defs["DamageRandomPercent"].PropertyReference.Value = slid;        // an int slider
                Check(TraxSettings.Shared.DamageRandomPercent == slid, "int slider did not reach the live settings");
                logHas("[config] DamageRandomPercent: " + oldPercent + " → " + slid + " (source: MCM)");

                defs["HeroCostMultiplier"].PropertyReference.Value = 0.5f;         // a float slider
                Check(Math.Abs(TraxSettings.Shared.HeroCostMultiplier - 0.5f) < 1e-6, "float slider did not reach the live settings");

                defs["ShowPlayerBar"].PropertyReference.Value = false;             // a checkbox
                Check(!TraxSettings.Shared.ShowPlayerBar, "checkbox did not reach the live settings");

                Check(Convert.ToInt32(defs["DamageRandomPercent"].PropertyReference.Value) == slid, "getter does not read back the live value");
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

            /// <summary>Clicks a button the way MCM.UI does (SettingsPropertyVM.OnValueClick).</summary>
            private static void Click(MCM.Abstractions.Base.BaseSettings settings, string id)
            {
                var def = MCM.Abstractions.BaseSettingsExtensions.GetAllSettingPropertyDefinitions(settings).First(d => d.Id == id);
                if (def.PropertyReference.Value is Action action) action();
                else Failures.Add("button " + id + " holds no Action");
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void Buttons(object settingsObject, string configPath, string dir, Action<string> logHas)
            {
                var settings = (MCM.Abstractions.Base.BaseSettings)settingsObject;
                var defs = MCM.Abstractions.BaseSettingsExtensions.GetAllSettingPropertyDefinitions(settings).ToDictionary(d => d.Id);
                var s = TraxSettings.Shared;

                // A tuning found in game: two sliders moved away from the defaults.
                int pct = (int)SettingsSchema.DamageRandomPercent.Default == 35 ? 45 : 35;
                defs["DamageRandomPercent"].PropertyReference.Value = pct;
                defs["ShowPlayerBar"].PropertyReference.Value = SettingsSchema.ShowPlayerBar.Default == 0;

                // --- Save current values as a defaults file
                Click(settings, McmBridge.ExportButtonId);
                string exported = Path.Combine(dir, DefaultsFile.FileName);
                Check(File.Exists(exported), "no defaults.json beside config.json after the export");
                var check = DefaultsFile.Check(File.Exists(exported) ? File.ReadAllText(exported, Encoding.UTF8) : string.Empty);
                Check(check.Ok, "the exported defaults.json does not check clean: " + check.Describe());
                foreach (var p in SettingsSchema.All)
                    Check(check.Values.TryGetValue(p.Key, out double v) && Math.Abs(v - s.Get(p)) < 1e-9, "export: " + p.Key + " is not the value in effect");
                Check(s.DamageRandomPercent == pct, "the export changed a value");
                logHas("[mcm] \"" + McmBridge.ExportButtonName + "\" pressed");
                logHas("[config] saved the current values as a defaults file: " + exported + " - ");
                logHas("DamageRandomPercent " + pct + " (built-in " + SettingsSchema.DamageRandomPercent.Format(SettingsSchema.DamageRandomPercent.Default) + ")");

                // --- Revert all to defaults: beats MCM edits AND a hand edit waiting in config.json
                var disk = ConfigFile.Read(File.ReadAllText(configPath, Encoding.UTF8));
                double hero = SettingsSchema.HeroCostMultiplier.Default == 0.5 ? 0.6 : 0.5;
                File.WriteAllText(configPath, ConfigFile.Write(new Dictionary<string, double>(disk.Values) { ["HeroCostMultiplier"] = hero }), Encoding.UTF8);
                bool refreshed = false;
                System.ComponentModel.PropertyChangedEventHandler onChanged = (o, e) => { if (e.PropertyName != "SAVE_TRIGGERED") refreshed = true; };
                settings.PropertyChanged += onChanged;
                Click(settings, McmBridge.RevertButtonId);
                settings.PropertyChanged -= onChanged;

                foreach (var p in SettingsSchema.All)
                    Check(Math.Abs(s.Get(p) - p.Default) < 1e-9, "revert left " + p.Key + " at " + p.Format(s.Get(p)));
                var after = ConfigFile.Read(File.ReadAllText(configPath, Encoding.UTF8));
                foreach (var p in SettingsSchema.All)
                    Check(after.Values.TryGetValue(p.Key, out double v) && Math.Abs(v - p.Default) < 1e-9, "revert: config.json holds " + p.Key + " = " + p.Format(v));
                Check(refreshed, "the page was not told to re-read its values after the revert");
                Check(Convert.ToInt32(defs["DamageRandomPercent"].PropertyReference.Value) == (int)SettingsSchema.DamageRandomPercent.Default, "the page reads a stale value");
                logHas("[mcm] \"" + McmBridge.RevertButtonName + "\" pressed");
                logHas("[config] DamageRandomPercent: " + pct + " → " + SettingsSchema.DamageRandomPercent.Format(SettingsSchema.DamageRandomPercent.Default) + " (source: defaults)");
                logHas("[config] wrote config.json (reverted to defaults): every value as it is in effect now");
                logHas("[config] reverted all " + SettingsSchema.All.Count + " settings to their defaults (defaults.json (embedded in TraxCombat.Core.dll)): ");

                Click(settings, McmBridge.RevertButtonId); // nothing left to change
                logHas("settings to their defaults (defaults.json (embedded in TraxCombat.Core.dll)): 0 changed, applied live");
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

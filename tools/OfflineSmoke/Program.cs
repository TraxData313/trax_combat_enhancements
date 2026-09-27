using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
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
            ? ReadShared(Path.Combine(_dir, ConfigFile.LogFileName))
            : string.Empty;

        /// <summary>A file read the way an editor reads trax_combat.log while the game holds it open
        /// for writing (review 10a R6: TraxLog keeps one handle, shared for reading and writing).</summary>
        private static string ReadShared(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                return reader.ReadToEnd();
        }

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
            Step("log file (review 10a R6): one handle kept open - readable meanwhile, follows a new path, released (the file can go, the next line makes a new one), trimmed to the newest half past 2 MB", LogWriterKeepsTheFileUsable);
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
            Step("Athletics: the stat decorator applies each fighter's run multiplier and a slowed rider's horse's, the attack ANIMATIONS only down to AttackAnimationMinPercent (100 = full speed - step 13; 0 and 60 checked), the AI decisions only while their A/B switch is on, on every recompute, never compounds, lifts them when off", DecoratorAppliesEachFightersMultipliers);
            Step("Athletics: swings through the real logic - a recruit (floor 50) 2 blows at full strength, slower every blow, empty on the 5th; a 300-skill party leader 14 and 54; kicks, bashes, ranged releases free; the attack-speed check by f", BlowsThroughTheLogic);
            Step("Athletics: health caps the bar at once, the peak line stays on the full pool, the cap switch works live", HealthCapsTheBar);
            Step("Athletics: the damage upside follows the attacker's f through the real damage decorator (empty: never above x1.00; untracked: full)", DamageUpsideFollowsTheAttacker);
            Step("Athletics: mid-battle settings - speeds, peak line, horses, pool size (shares kept) re-target live; switching off refills and lifts", AthleticsHotSwap);
            Step("Athletics: the [summary] Athletics block", AthleticsSummary);
            // The step back (step 5d) - the real logic's bookkeeping with a stand-in for the engine side.
            Step("step back: rolled at every AI swing's end by f (0% at full strength, 100% empty), started from the tick, timed live, capped, refused by the safety checks, released on every path (time, order, hand-over untouched, left the field, switched off, mission end), logged and summarised", StepBackThroughTheLogic);
            Step("stale records (review 10a R5): an index reused, or an agent deleted unseen - his step back, pace hold and slowed horse end like a man leaving the field, no engine call on him", StaleRecordsAreForgotten);
            // The attack rate (step 5e; step 13 PAUSE ONLY) - phases, D, the no-attack timer (AI with a stand-in
            // engine side; yours through the input gate's managed decision), cycles and verdicts.
            Step("attack rate (step 13): full-speed animations, the AI timer D x (1/m - 1) after every attack - none at full strength or below 0.1 s, melee (D = wind-up + swing), ranged (from the reload's end, + the reload; a throw without), riders held; the gap it leaves = the timer, none early; lifted on every path (time, an attack slipping through, a game job waited out, a long frame, the player took him, left the field, switched off, mission end), refusals, chained blows, R1 / R2, the AI-decision and animation-floor switches re-applied; YOUR timer - the hold from the release, presses swallowed (during the swing and the countdown), the flash, hold-to-attack, the recovery read, an attack the gate missed, AttackRatePlayerTimer and ModEnabled release it, ranged from the reload; the summary", AttackRateThroughTheLogic);
            Step("attack rate (step 13): your input gate goes FIRST in the behaviour list, so the reverse pre-tick loop runs it right after MissionMainAgentController; with no pause it touches nothing", GateGoesFirstInTheBehaviourList);
            Step("attack rate: the first slowed fighter's every touched value before → after, each checked against its factor (step 13: animations x1.00, AI decisions unchanged)", FirstSlowedIsLogged);
            Step("master switch: ModEnabled off is vanilla at once - damage unrolled but recorded, penalties lifted, no costs, every step back released, the Athletics bar gone; on = everyone full, the bar back", MasterSwitchIsVanillaLive);
            Step("Athletics: a failure is logged once per site, counted, and reported in the summary", AthleticsFailSafe);
            // The HUD (step 6) - the prefab against the game's own types and files, the real view and
            // ViewModel driven by made-up frames with a stand-in layer.
            Step("HUD prefab: well-formed; every element a widget type of the game, every attribute a real property with a valid value, only vanilla brushes and sprites, every @binding typed exactly like its ViewModel property, every ViewModel property drawn", HudPrefabIsValid);
            Step("HUD player bar: through the real view - hidden outside fights, built in battle with you on the field, number / fill / colours green → blue → yellow → orange → red / peak marker / wounded part / Exhausted pushed and logged, refreshed every HudRefreshSeconds (live), layout live, removed and rebuilt with its reason (Hide battle UI, ShowPlayerBar, AthleticsEnabled, photo mode, no player, not a fight, ModEnabled), stealth/tournament/duel are fights, suspended, paused, mission end, the [summary] hud lines", HudPlayerBarThroughTheView);
            Step("HUD player bar outside a battle (step 12): the walk-about mode - hidden with empty hands and a full bar, built with a weapon drawn, a 1 s grace (a weapon switch does not rebuild it), kept while Athletics refills, gone once full (a wound's cap counts as full); never in a conversation, barter, cutscene or deployment; ShowPlayerBarOutsideBattles, no player, an untracked player, the master switch; fights unchanged; every build / removal logged with its reason; the attach text; the [summary] clause", HudPlayerBarOutsideBattles);
            Step("HUD fail safe: an exception, a movie that does not load, a failure with the bar up or a view never attached - the view is disabled for the mission, its layer removed, [error] logged, nothing thrown", HudFailSafe);
            // The Attack recovery bar (step 13, Anton's "attack recovery" bar above the Athletics bar).
            Step("recovery bar prefab: well-formed; every element a widget type, every attribute a real property, vanilla brushes and sprites only, every @binding typed exactly like AttackRecoveryVM, every ViewModel property drawn", RecoveryPrefabIsValid);
            Step("recovery bar: through the real view over a running player timer - hidden outside a battle at rest, full and quiet in battle, EMPTY at the attack, a press during the swing flashes it (two pulses), filling over the pause with the seconds inside (\"0.6 s\" → \"0.3 s\"), full again after; no flash with FlashBarOnEarlyAttack off; the layout live; removed by its switch, ShowPlayerBar, AttackRatePlayerTimer and ModEnabled and rebuilt; the log lines and the summary line", RecoveryBarThroughTheView);
            Step("orders strip prefab: well-formed; every element a widget type, every attribute a real property, vanilla brushes and sprites only, the {Cells} item template bound against OrderStripCellVM, every @binding typed exactly, every ViewModel property drawn", StripPrefabIsValid);
            Step("orders strip: through the real view with stand-in cards - hidden while the menu is closed; open: cells on the cards' bottom edges (keyboard columns, another UI scale, the gamepad row, RTS Camera's one set), lifted at the screen's edge, values / colours by f / ± band / health, live switches, new stats on the version; closing and reopening quiet; a short mismatch waited out, a long one → the panel; no order layer, not whole sets, never drawn, OrderStripUnderCards off (and on again mid-open), a lost card tree re-scanned; the usual gates; the [summary] lines", StripThroughTheView);
            Step("orders strip fail safe: a failing card walk or a view without sources - disabled for the mission, its layer removed, [error] logged, nothing thrown", StripFailSafe);
            Step("MCM still not loaded after phase 1", () => Check(!McmLoaded(), "MCMv5 got loaded during phase 1"));

            // Phase 2 - a player WITH MCM.
            _allowMcm = true;
            Step("MCM's DLL loaded by another mod, MCM's module not enabled (step 12): one line, no page, no attempt, no retry", McmModuleNotEnabled);
            Step("MCM's module on but MCM never ready (step 12): no try before the main menu, the first + " + McmPlan.MaxRetries + " retries, then one line and silence", McmNeverReadyIsCapped);
            Step("MCM present: the settings page builds through MCM's real fluent builder", McmPageBuilds);
            Step("MCM page: every setting, right type, range, hint, group", McmPageMatchesSchema);
            Step("MCM sliders write the live settings at once (hot swap)", McmSlidersAreLive);
            Step("MCM Reset (the 'default' preset) restores the mod's defaults (defaults.json), not the values at build time", McmPresetRestoresDefaults);
            Step("MCM Done writes config.json", McmDoneWritesFile);
            Step("MCM buttons: \"Save current values as a defaults file\" writes a clean defaults.json beside config.json; \"Revert all to defaults\" is live, logged, rewrites config.json, refreshes the page", McmButtons);

            // Step 11 - LAST (it claims the one-copy slot for good and runs a game start).
            Step("two copies enabled (dev + release): the second SubModule stands down - no log line, no model, no mission logic, no message; the running one registers ONE decorator of each kind and reports the other once ([compat] + one message)", TwoCopiesOneRuns);

            Console.WriteLine();
            TraxLog.Release(); // the log handle (R6) - the temp folder can go
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

            TraxSettings.Shared.Set(SettingsSchema.ShowFormationHealth, false, SettingSources.Mcm);
            ConfigStore.SaveAfterMcm();
            Check(Directory.GetFiles(_dir, ConfigFile.FileName + ".broken-*").Length == 1, "broken file not backed up");
            var disk = ConfigFile.Read(File.ReadAllText(ConfigPath));
            Check(disk.Ok && disk.Values.Count == SettingsSchema.All.Count, "fresh file after a broken one is not complete");
            Check(disk.Values["ShowFormationHealth"] == 0 && disk.Values["DamageRandomPercent"] == HandPct, "fresh file lost the values in effect");
            LogHas("did not parse");
        }

        private static void MissingAndUnknownKeys()
        {
            int odd = PctDefault == 20 ? 25 : 20; // must differ from the default (McmPageBuilds needs a non-default value)
            File.WriteAllText(ConfigPath, "{ \"damageRandomPercent\": " + odd + ", \"DamageRandomPercnt\": 5, \"ConfigVersion\": 1 }");
            ConfigStore.Reload("game start");
            Check(TraxSettings.Shared.DamageRandomPercent == odd, "value with odd key casing not applied");
            Check(TraxSettings.Shared.ShowFormationHealth == (SettingsSchema.ShowFormationHealth.Default != 0), "missing key did not go back to its default");
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
            LogHas(" ~[damage] smoke roll "); // step 10b: a verbose line carries the mark the trim reads
            TraxLog.FlushSuppressedCounts();
            LogHas("more verbose [damage] lines were suppressed by the rate limit");

            // Review R8 (step 10b): the hot paths ask VerboseWants(bucket) BEFORE building a line. A line
            // never built is counted as suppressed exactly like one built and dropped.
            const int asks = 5000;
            for (int i = 0; i < asks; i++)
                if (TraxLog.VerboseWants("smoke-peek")) TraxLog.Verbose("damage", "smoke peek " + i, "smoke-peek");
            TraxLog.FlushSuppressedCounts();
            string text = LogText;
            var peekLines = text.Split('\n').Where(l => l.Contains("] smoke peek ")).ToList();
            int reported = peekLines.Sum(l => { var m = Regex.Match(l, @"\(\+(\d+) similar lines suppressed\)"); return m.Success ? int.Parse(m.Groups[1].Value) : 0; });
            var drained = Regex.Match(text, @"\[log\] (\d+) more verbose \[smoke-peek\] lines were suppressed");
            int suppressed = reported + (drained.Success ? int.Parse(drained.Groups[1].Value) : 0);
            Check(peekLines.Count >= 40 && peekLines.Count < 200, "VerboseWants did not cap the flood: " + peekLines.Count + " lines");
            Check(peekLines.Count + suppressed == asks, "VerboseWants lost count: " + peekLines.Count + " written + " + suppressed + " suppressed != " + asks);

            TraxSettings.Shared.Set(SettingsSchema.VerboseLogging, false, SettingSources.File);
            TraxLog.Verbose("damage", "must not appear");
            Check(!LogText.Contains("must not appear"), "verbose line written with VerboseLogging off");
            Check(!TraxLog.VerboseWants("smoke-off"), "VerboseWants said yes with VerboseLogging off");
            ConfigStore.Reload("after flood"); // file and memory in step again
        }

        /// <summary>Review 10a R6: TraxLog keeps ONE handle (AutoFlush) instead of an open / append / close
        /// per line. The log must stay usable: readable while held, following a changed path, gone
        /// cleanly after Release (the next line starts a new file), and trimmed past LogMaxMegabytes -
        /// step 10b (R7): only the oldest VERBOSE lines go, every other line stays.</summary>
        private static void LogWriterKeepsTheFileUsable()
        {
            TraxLog.Info("log", "smoke: a line while the handle is held");
            LogHas("smoke: a line while the handle is held"); // read (shared) while TraxLog holds the file

            string sub = Path.Combine(_dir, "log_writer");
            Directory.CreateDirectory(sub);
            string subLog = Path.Combine(sub, ConfigFile.LogFileName);
            SetStatic(typeof(ModPaths), "_configDir", sub);
            try
            {
                TraxLog.Info("log", "smoke: first line in the second folder");
                Check(File.Exists(subLog) && ReadShared(subLog).Contains("smoke: first line in the second folder"), "the log did not follow the new path");
                TraxLog.Release();
                File.Delete(subLog);
                Check(!File.Exists(subLog), "released, the log could not be deleted");
                TraxLog.Info("log", "smoke: after the release");
                string fresh = ReadShared(subLog);
                Check(fresh.Contains("smoke: after the release") && !fresh.Contains("first line in the second folder"), "the next line did not start a new file");

                // Step 10b (R7): at a 1 MB cap, 2.1 MB of verbose lines around the lines the playtest is
                // read from - a summary, a first-time line, an error with its stack.
                TraxSettings.Shared.Set(SettingsSchema.LogMaxMegabytes, 1, SettingSources.File);
                TraxSettings.Shared.Set(SettingsSchema.VerboseLogging, true, SettingSources.File);
                Check(TraxLog.MaxBytes == 1024 * 1024, "LogMaxMegabytes not read live: " + TraxLog.MaxBytes);
                TraxLog.Info("summary", "smoke: an early summary line");
                TraxLog.Limited("hud", "smoke: an early first-time line", "smoke-first");
                TraxLog.Error("smoke.trim", new InvalidOperationException("smoke: an early error"));
                string filler = new string('x', 1000);
                for (int i = 0; i < 2100; i++) TraxLog.Verbose("damage", "smoke filler " + i + " " + filler, "smoke-filler-" + i); // own buckets: all pass
                long size = new FileInfo(subLog).Length;
                string text = ReadShared(subLog);
                Check(size < TraxLog.MaxBytes && size > TraxLog.MaxBytes / 4, "the log was not trimmed to about half its limit: " + size + " bytes");
                Check(text.Contains("[log] " + LogTrim.NoteStart) && text.Contains("smoke filler 2099 ") && !text.Contains("smoke filler 5 "),
                    "the trim did not keep the newest verbose lines (with its note)");
                Check(text.Contains("[summary] smoke: an early summary line") && text.Contains("[hud] smoke: an early first-time line")
                    && text.Contains("[error] smoke.trim: System.InvalidOperationException: smoke: an early error") && text.Contains("    System.InvalidOperationException: smoke: an early error"),
                    "the trim cut a line that must be kept (a summary, a first-time line, an error and its stack)");
                Check(text.Contains("smoke: after the release"), "the trim cut a non-verbose line");
                Check(text.Split('\n').All(l => l.Length == 0 || l.StartsWith("20", StringComparison.Ordinal) || l.StartsWith("    ", StringComparison.Ordinal)), "the trim left a half line");
            }
            finally
            {
                TraxLog.Release();
                SetStatic(typeof(ModPaths), "_configDir", _dir);
                TraxSettings.Shared.Set(SettingsSchema.LogMaxMegabytes, SettingsSchema.LogMaxMegabytes.Default, SettingSources.File);
                TraxSettings.Shared.Set(SettingsSchema.VerboseLogging, VerboseDefault, SettingSources.File);
                ConfigStore.Reload("after the log trim"); // file and memory in step again
            }
            TraxLog.Info("log", "smoke: back in the main log");
            LogHas("smoke: back in the main log");
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

        /// <summary>The bridge as a fresh game start finds it, with this enabled-module list (null = unknown).</summary>
        private static void ResetMcmBridge(string[]? modules)
        {
            SetStatic(typeof(McmBridge), "_done", false);
            SetStatic(typeof(McmBridge), "_waiting", false);
            SetStatic(typeof(McmBridge), "_attempts", 0);
            SetStatic(typeof(McmBridge), "_notReady", 0);
            SetStatic(typeof(McmBridge), "_nextTryAt", 0.0);
            McmBridge.UseModuleList(modules);
        }

        /// <summary>Step 12 (Anton's log 21:08): another mod carries an MCMv5 DLL, MCM's own module is
        /// off - one line, no page, no attempt, no retry.</summary>
        private static void McmModuleNotEnabled()
        {
            Assembly.LoadFrom(Path.Combine(_mcmBin, "MCMv5.dll"));
            Check(McmLoaded(), "precondition: MCMv5 not loaded");
            ResetMcmBridge(new[] { "Bannerlord.Harmony", "Bannerlord.ButterLib", "Bannerlord.UIExtenderEx", "Native", "ImmersiveAI.Dev", "TraxCombatEnhancements.Dev" });
            McmBridge.TryRegister("main menu");
            Check(!McmBridge.IsRegistered, "a page with MCM's module off");
            LogHas("[mcm] MCM's module is not enabled - no settings page; config.json only. (An MCM 5.");
            McmBridge.TryRegister("game start");
            for (int i = 0; i < 5; i++)
            {
                SetStatic(typeof(McmBridge), "_nextTryAt", 0.0);
                McmBridge.Tick();
            }
            Check(Occurrences(LogText, "MCM's module is not enabled") == 1, "the 'module not enabled' line should be logged once");
            Check((int)GetStatic(typeof(McmBridge), "_attempts")! == 0, "the bridge tried MCM with its module off");
            Check(Occurrences(LogText, "not ready yet") == 0, "a 'not ready' retry with MCM's module off");
        }

        /// <summary>Step 12: MCM's module on but MCM never ready (its services never built - here: no
        /// main-menu hook of MCM ran) - no attempt before the main menu, the first + MaxRetries retries,
        /// then one line and silence.</summary>
        private static void McmNeverReadyIsCapped()
        {
            ResetMcmBridge(new[] { "Bannerlord.Harmony", McmPlan.ModuleId, "Native", "TraxCombatEnhancements.Dev" });
            McmBridge.Tick();
            Check((int)GetStatic(typeof(McmBridge), "_attempts")! == 0, "the tick tried MCM before the main menu");
            McmBridge.TryRegister("main menu");
            LogHas(" is loaded but not ready yet at main menu - retrying every 1 s, at most " + McmPlan.MaxRetries + " times.");
            for (int i = 0; i < 3 * McmPlan.MaxRetries; i++)
            {
                SetStatic(typeof(McmBridge), "_nextTryAt", 0.0); // a second passed
                McmBridge.Tick();
            }
            int attempts = (int)GetStatic(typeof(McmBridge), "_attempts")!;
            Check(attempts == McmPlan.MaxRetries + 1, "attempts " + attempts + ", expected the first + " + McmPlan.MaxRetries + " retries");
            Check(!McmBridge.IsRegistered, "a page without MCM's services");
            LogHas(" never became ready - gave up after " + (McmPlan.MaxRetries + 1) + " attempts (the first + " + McmPlan.MaxRetries
                   + " retries, one a second) - no settings page; config.json only.");
            McmBridge.TryRegister("game start");
            Check(Occurrences(LogText, "not ready yet") == 1 && Occurrences(LogText, "never became ready") == 1
                  && (int)GetStatic(typeof(McmBridge), "_attempts")! == attempts, "the bridge kept going after it gave up");
        }

        private static void McmPageBuilds()
        {
            // Precondition for the Reset check: a non-default value is live while the page is
            // built (MCM would otherwise bake it into its "default" preset).
            Check(TraxSettings.Shared.DamageRandomPercent != (int)SettingsSchema.DamageRandomPercent.Default, "precondition: expected a non-default value before the build");
            Assembly.LoadFrom(Path.Combine(_mcmBin, "MCMv5.dll"));
            McmHarness.InstallServices();
            ResetMcmBridge(null); // phase 1 and the step-12 checks stood it down; the module list unknown = just try
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
                // Step 10b: the page's group order. MCM's UI (Bannerlord.MBOptionScreen for 1.4.8,
                // UISettingsUtils.SettingsPropertyGroupVMComparer) shows groups by Order ASCENDING:
                // Master switch first, the schema's order, the Defaults buttons, Advanced last.
                var groupOrder = MCM.Abstractions.BaseSettingsExtensions.GetSettingPropertyGroups(settings)
                    .OrderBy(g => g.Order).Select(g => g.GroupName).ToList();
                var expectedGroups = SettingsSchema.Groups.Select(g => g.Title).ToList();
                expectedGroups.Insert(expectedGroups.Count - 1, McmBridge.DefaultsGroupTitle);
                Check(groupOrder.SequenceEqual(expectedGroups), "MCM group order is " + string.Join(", ", groupOrder) + " - expected " + string.Join(", ", expectedGroups));
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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TraxCombat.Core;

namespace TraxCombat
{
    /// <summary>
    /// config.json on disk ↔ <see cref="TraxSettings.Shared"/> in memory.
    ///
    /// When the file is READ (hand edits take effect): once at startup
    /// (<see cref="Initialize"/>), then at every game start/load and every mission start
    /// (<see cref="Reload"/>). Each read makes memory match the file - valid values set,
    /// missing or invalid ones back to their default - and every real change is logged
    /// <c>[config] X: old → new (source: file)</c>.
    ///
    /// When the file is WRITTEN:
    ///   - first run (no file): every default, with the explanations;
    ///   - after MCM's Done (<see cref="SaveAfterMcm"/>);
    ///   - at a read that found settings MISSING (a newer version added some) and nothing
    ///     invalid - to add them; a file with an invalid value is left alone for the player to fix.
    /// Every write while a file exists follows THE REWRITE RULE (<see cref="ConfigMerge"/>):
    /// re-read the disk file at write time; MCM's value for each key MCM changed; the disk's
    /// value for every other key (so a hand edit made while the game runs is never lost); memory
    /// only where the disk has nothing usable; unknown keys carried along. A disk file that does
    /// not parse is copied to <c>config.json.broken-&lt;time&gt;</c> first, then replaced.
    /// Writes go through a temp file, so a crash mid-write cannot leave half a file.
    /// </summary>
    internal static class ConfigStore
    {
        private static readonly EditTracker McmEdits = new EditTracker();
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        private static readonly object Gate = new object();
        private static bool _initialized;
        private static bool _quiet;

        private static TraxSettings Settings => TraxSettings.Shared;

        /// <summary>Once, at OnSubModuleLoad: hooks the change log, creates or reads the file,
        /// then logs every setting in effect.</summary>
        public static void Initialize()
        {
            lock (Gate)
            {
                if (_initialized) return;
                _initialized = true;
                Settings.Changed += OnChanged;
                Settings.HandlerFailed += e => TraxLog.Error("config.changed-handler", e);

                string path = ModPaths.ConfigFilePath;
                TraxLog.Info("config", "config file: " + path + (ModPaths.UsedFallback ? " (game path API unavailable - fallback path used)" : string.Empty));
                _quiet = true; // the full dump below says it all; no per-key change lines on the first read
                try
                {
                    if (!File.Exists(path))
                    {
                        WriteText(path, ConfigFile.Write(Settings.Snapshot()));
                        TraxLog.Info("config", "first run: created config.json with every default and a plain-words explanation beside each value");
                    }
                    else
                    {
                        ReadAndApply(path, "startup");
                    }
                }
                catch (Exception e)
                {
                    TraxLog.Error("config.initialize", e);
                }
                finally
                {
                    _quiet = false;
                }

                TraxLog.Info("config", "settings in effect (" + SettingsSchema.All.Count + ", version " + Settings.Version + "):");
                foreach (var p in SettingsSchema.All)
                    TraxLog.Info("config", "  " + Settings.Describe(p));
            }
        }

        /// <summary>Re-reads config.json so hand edits apply (game start/load, every mission
        /// start). Logs each change, or one "no changes" line.</summary>
        public static void Reload(string when)
        {
            lock (Gate)
            {
                if (!_initialized)
                {
                    Initialize();
                    return;
                }
                try
                {
                    // MCM edits that never reached the file (a failed save) must not be undone by
                    // the read below - write them first.
                    if (McmEdits.ChangedKeys(Settings).Count > 0)
                        WriteMerged("unsaved MCM changes before the re-read at " + when);

                    string path = ModPaths.ConfigFilePath;
                    if (!File.Exists(path))
                    {
                        WriteText(path, ConfigFile.Write(Settings.Snapshot()));
                        TraxLog.Info("config", "config.json was missing at " + when + " - wrote a fresh one with the values in effect");
                        return;
                    }
                    int before = Settings.Version;
                    if (ReadAndApply(path, when))
                        McmEdits.Clear(); // memory now matches the file - no MCM edit is pending
                    int changes = Settings.Version - before;
                    TraxLog.Info("config", "config.json re-read at " + when + ": "
                        + (changes == 0 ? "no changes" : changes + " change(s), settings version " + Settings.Version));
                }
                catch (Exception e)
                {
                    TraxLog.Error("config.reload", e);
                }
            }
        }

        /// <summary>MCM's Done: write the file by the rewrite rule.</summary>
        public static void SaveAfterMcm()
        {
            lock (Gate)
            {
                try
                {
                    WriteMerged("MCM Done");
                }
                catch (Exception e)
                {
                    TraxLog.Error("config.save", e);
                }
            }
        }

        // ------------------------------------------------------------------ internals

        private static void OnChanged(SettingChange c)
        {
            if (c.Source == SettingSources.Mcm) McmEdits.Note(c.Param, c.OldValue);
            if (!_quiet) TraxLog.Info("config", c.ToLogText());
        }

        /// <summary>Reads the file, reports its problems, applies it, and adds missing keys.
        /// False: the file did not parse and nothing was applied.</summary>
        private static bool ReadAndApply(string path, string when)
        {
            var read = ConfigFile.Read(File.ReadAllText(path, Encoding.UTF8));
            if (!read.Ok)
            {
                TraxLog.Info("config", "could not read config.json at " + when + " (" + read.Error
                    + ") - keeping the values in effect. Fix the file, or delete it to get the defaults back.");
                return false;
            }

            if (read.FileVersion == null)
                TraxLog.Info("config", "config.json has no " + ConfigFile.VersionKey + " stamp - read as version " + ConfigFile.FormatVersion);
            else if (read.FileVersion > ConfigFile.FormatVersion)
                TraxLog.Info("config", "config.json is format " + read.FileVersion + ", newer than this build (" + ConfigFile.FormatVersion + ") - reading what it knows");
            foreach (var issue in read.Issues)
                TraxLog.Info("config", "file problem: " + issue);
            foreach (var pair in read.Unknown)
                TraxLog.Info("config", "file problem: \"" + pair.Key + "\" is not a setting of this version - ignored (typo?)");
            var absent = read.Missing.Where(p => read.Issues.All(i => i.Key != p.Key)).ToList();
            if (absent.Count > 0)
                TraxLog.Info("config", "not in the file, default used: " + string.Join(", ", absent.Select(p => p.Key)));

            ConfigFile.Apply(read, Settings);

            // A newer version added settings the file lacks: write them in, keeping every valid
            // value the file has. Not when something in it is invalid - rewriting would replace
            // the player's broken text with the default before he has seen the log line.
            if (absent.Count > 0 && !read.HasInvalid)
            {
                var plan = ConfigMerge.ForWrite(read, Settings.Snapshot(), Array.Empty<string>());
                WriteText(path, ConfigFile.Write(plan.Values, plan.Unknown));
                TraxLog.Info("config", "added " + absent.Count + " missing setting(s) to config.json with their defaults");
            }
            return true;
        }

        private static void WriteMerged(string reason)
        {
            string path = ModPaths.ConfigFilePath;
            var changedInMcm = McmEdits.ChangedKeys(Settings);
            ConfigReadResult? disk = null;
            if (File.Exists(path))
            {
                disk = ConfigFile.Read(File.ReadAllText(path, Encoding.UTF8));
                if (!disk.Ok)
                {
                    string backup = path + ".broken-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                    File.Copy(path, backup, true);
                    TraxLog.Info("config", "config.json did not parse (" + disk.Error + ") - saved it as " + Path.GetFileName(backup)
                        + " and writing a fresh one from the values in effect");
                }
            }

            var plan = ConfigMerge.ForWrite(disk, Settings.Snapshot(), changedInMcm);
            WriteText(path, ConfigFile.Write(plan.Values, plan.Unknown));
            McmEdits.Clear();

            TraxLog.Info("config", "wrote config.json (" + reason + "): "
                + (changedInMcm.Count == 0 ? "no values changed in MCM" : "from MCM " + string.Join(", ", changedInMcm))
                + (plan.KeptFromDisk.Count == 0 ? string.Empty
                    : "; kept hand edit(s) from the file that apply at the next battle start: "
                      + string.Join(", ", plan.KeptFromDisk.Select(k => DescribeKept(k, plan.Values[k])))));
        }

        private static string DescribeKept(string key, double fileValue)
        {
            SettingsSchema.TryGet(key, out var p);
            return key + " = " + p.Format(fileValue) + " (now " + p.Format(Settings.Get(p)) + ")";
        }

        /// <summary>Temp file + replace: a crash mid-write leaves the old file, never half a new one.</summary>
        private static void WriteText(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, text, Utf8NoBom);
            if (File.Exists(path))
            {
                try
                {
                    File.Replace(tmp, path, null);
                    return;
                }
                catch (Exception)
                {
                    // some file systems refuse Replace - fall back to copy + delete
                }
                File.Copy(tmp, path, true);
                File.Delete(tmp);
            }
            else
            {
                File.Move(tmp, path);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TraxCombat.Core
{
    /// <summary>What a config-file line had wrong, for the <c>[config]</c> log.</summary>
    public enum ConfigIssueKind
    {
        /// <summary>A value the setting cannot take ("abc" for a number) - the key counts as missing.</summary>
        Invalid,

        /// <summary>A number outside the range - clamped into it.</summary>
        Clamped,

        /// <summary>A fraction given for a whole-number setting - rounded.</summary>
        Rounded,

        /// <summary>The same key twice - the last one wins.</summary>
        Duplicate,
    }

    public sealed class ConfigIssue
    {
        public ConfigIssue(string key, ConfigIssueKind kind, string message)
        {
            Key = key;
            Kind = kind;
            Message = message;
        }

        public string Key { get; }

        public ConfigIssueKind Kind { get; }

        public string Message { get; }

        public override string ToString() => Key + ": " + Message;
    }

    /// <summary>Everything read from one config.json text - see <see cref="ConfigFile.Read"/>.</summary>
    public sealed class ConfigReadResult
    {
        /// <summary>Null when the text parsed; otherwise why not (with line and position). A
        /// failed read carries no values - the caller keeps what it had.</summary>
        public string? Error { get; internal set; }

        public bool Ok => Error == null;

        /// <summary>Valid values by canonical key, already clamped and rounded.</summary>
        public Dictionary<string, double> Values { get; } = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Settings with no usable value in the file (absent or invalid) - they take
        /// their default.</summary>
        public List<ParamDef> Missing { get; } = new List<ParamDef>();

        /// <summary>Keys that are not settings, with their raw JSON value - ignored, reported,
        /// and carried along on a rewrite so a typo is never silently eaten.</summary>
        public List<KeyValuePair<string, string>> Unknown { get; } = new List<KeyValuePair<string, string>>();

        public List<ConfigIssue> Issues { get; } = new List<ConfigIssue>();

        /// <summary>The file's <c>ConfigVersion</c> stamp, if it had one.</summary>
        public int? FileVersion { get; internal set; }

        /// <summary>What <see cref="ConfigFile.Migrate"/> changed ("Key: old → new (why)"), empty when nothing.</summary>
        public List<string> Migrated { get; } = new List<string>();

        public bool HasInvalid => Issues.Any(i => i.Kind == ConfigIssueKind.Invalid);
    }

    /// <summary>
    /// The config.json TEXT - pure, no disk. <see cref="Write"/> produces the commented file
    /// (a <c>// plain words</c> explanation above every key, grouped under section headings, a
    /// header saying where the file lives and how edits apply). Newtonsoft cannot WRITE
    /// comments, so the text is generated here; it READS them fine (the game's own 13.0.1,
    /// tested in step 2), and also tolerates trailing commas and any key casing.
    /// </summary>
    public static class ConfigFile
    {
        /// <summary>The format stamp written into every file. Bump it (and migrate in
        /// <see cref="Migrate"/>) when a later version must change the meaning of an existing key or
        /// push a new default into files that already carry the old one. 2 = step 13 (PAUSE ONLY),
        /// 3 = step 14 (the run-speed floor 0.3 → 0.7), 4 = step 20b (Anton's tuning after his playtest: the
        /// run-speed floor 0.7 → 0.6, the attack animation at empty 100 → 85), 5 = step 22 (Anton, for slower battles:
        /// the refill a straight line again - RegenRateNearFullPercent 50 → 100 - and the run-speed floor 0.6 → 0.3).</summary>
        public const int FormatVersion = 5;

        /// <summary>The meta key holding <see cref="FormatVersion"/> - not a setting.</summary>
        public const string VersionKey = "ConfigVersion";

        public const string FileName = "config.json";

        public const string LogFileName = "trax_combat.log";

        /// <summary>Where the file lives, as the player sees it (the header's wording).</summary>
        public const string FolderForHumans = @"Documents\Mount and Blade II Bannerlord\Configs\TraxCombatEnhancements\";

        private const int WrapAt = 96;
        private const string Nl = "\r\n";

        // ------------------------------------------------------------------ write

        /// <summary>
        /// The whole file: header, then every setting of <see cref="SettingsSchema.All"/> in
        /// group order with its explanation, then (if any) the unrecognised keys carried over
        /// from the old file. Values missing from <paramref name="values"/> take the default.
        /// </summary>
        public static string Write(IReadOnlyDictionary<string, double> values,
            IEnumerable<KeyValuePair<string, string>>? unknown = null)
        {
            var sb = new StringBuilder(8192);
            WriteHeader(sb);
            sb.Append('{').Append(Nl);
            sb.Append("  // Format stamp of this file - leave it alone.").Append(Nl);
            sb.Append("  \"").Append(VersionKey).Append("\": ").Append(FormatVersion.ToString(CultureInfo.InvariantCulture));

            AppendSettings(sb, values, p => p.DefaultAndRangeText, afterAnEntry: true);

            var extra = unknown?.ToList() ?? new List<KeyValuePair<string, string>>();
            if (extra.Count > 0)
            {
                sb.Append(',').Append(Nl).Append(Nl);
                sb.Append("  // ==================== Not recognised - ignored ====================").Append(Nl);
                sb.Append("  // These keys are not settings of this version (a typo, or an old name?). They do").Append(Nl);
                sb.Append("  // nothing; they are kept here only so an edit is never silently lost. Fix or delete them.");
                for (int i = 0; i < extra.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(Nl);
                    sb.Append("  ").Append(JsonConvert.ToString(extra[i].Key)).Append(": ").Append(extra[i].Value);
                }
            }

            sb.Append(Nl).Append('}').Append(Nl);
            return sb.ToString();
        }

        /// <summary>
        /// Every setting of <see cref="SettingsSchema.All"/> in group order - a heading per group,
        /// the plain-words description as // lines above each key, then "(<paramref name="tail"/>)"
        /// and the key with its value (missing from <paramref name="values"/> → the default). Shared
        /// by config.json and defaults.json (<see cref="DefaultsFile.Write"/>), so the two never
        /// word a setting differently. <paramref name="afterAnEntry"/>: a key was already written
        /// (the first group then starts with a comma).
        /// </summary>
        internal static void AppendSettings(StringBuilder sb, IReadOnlyDictionary<string, double>? values, Func<ParamDef, string> tail, bool afterAnEntry)
        {
            bool needComma = afterAnEntry;
            foreach (var group in SettingsSchema.Groups)
            {
                if (needComma) sb.Append(',').Append(Nl).Append(Nl);
                needComma = true;
                sb.Append("  // ==================== ").Append(group.Title).Append(" ====================");
                bool first = true;
                foreach (var p in SettingsSchema.InGroup(group))
                {
                    if (!first) sb.Append(',');
                    sb.Append(Nl);
                    if (!first) sb.Append(Nl);
                    first = false;
                    foreach (var line in Wrap(p.Description + (p.Timing == ApplyTiming.NextBattle ? " Applies from the next battle." : string.Empty), WrapAt - 5))
                        sb.Append("  // ").Append(line).Append(Nl);
                    sb.Append("  // (").Append(tail(p)).Append(')').Append(Nl);
                    double v = values != null && values.TryGetValue(p.Key, out var found) ? p.Normalize(found) : p.Default;
                    sb.Append("  \"").Append(p.Key).Append("\": ").Append(p.Format(v));
                }
            }
        }

        private static void WriteHeader(StringBuilder sb)
        {
            string[] header =
            {
                "Trax Combat Enhancements - settings",
                "",
                "This file lives in " + FolderForHumans + FileName,
                "The mod's log, " + LogFileName + ", is in the same folder.",
                "",
                "Every line starting with // is an explanation; the game ignores it. Change a value after",
                "the colon (true/false, or a number with a dot: 0.75) and save.",
                "",
                "- Edits made here are picked up at the next battle start (or when you load a game) -",
                "  no restart needed.",
                "- The Mod Configuration Menu (MCM), if you have it installed, edits this same file:",
                "  changes made there apply at once, even mid-battle, and are written here when you",
                "  press Done. Your hand edits of other values are kept when MCM writes the file.",
                "- A number outside its range is clamped into it; a key the mod does not know is ignored",
                "  and reported in the log. Your own // comments are not kept when the file is rewritten.",
                "",
                "Back to the defaults (the mod's defaults.json, built into it):",
                "- one setting: delete its line (the key and its value) - it takes its default at the next",
                "  battle start and is written back in;",
                "- everything: delete this file - every setting takes its default at the next battle start",
                "  (or game start) and a fresh file is written;",
                "- with MCM: Mod Options > Trax Combat Enhancements > Defaults > \"Revert all to defaults\"",
                "  (applies at once, even mid-battle, and rewrites this file).",
            };
            foreach (var line in header)
                sb.Append("// ").Append(line).TrimEndSpaces().Append(Nl);
        }

        private static StringBuilder TrimEndSpaces(this StringBuilder sb)
        {
            while (sb.Length > 0 && sb[sb.Length - 1] == ' ') sb.Length--;
            return sb;
        }

        /// <summary>Greedy word wrap - no word is ever split.</summary>
        public static IEnumerable<string> Wrap(string text, int width)
        {
            var line = new StringBuilder();
            foreach (var word in text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length > 0 && line.Length + 1 + word.Length > width)
                {
                    yield return line.ToString();
                    line.Clear();
                }
                if (line.Length > 0) line.Append(' ');
                line.Append(word);
            }
            if (line.Length > 0) yield return line.ToString();
        }

        // ------------------------------------------------------------------ read

        /// <summary>
        /// Parses config text. Never throws. Comments, trailing commas and any key casing are
        /// fine. Per key: valid → <see cref="ConfigReadResult.Values"/> (clamped/rounded, with an
        /// issue noted); unusable ("abc", a list) → an Invalid issue and the key counts as
        /// missing; not a setting → <see cref="ConfigReadResult.Unknown"/>. A text that is not a
        /// JSON object at all → <see cref="ConfigReadResult.Error"/> and nothing else.
        /// </summary>
        public static ConfigReadResult Read(string text)
        {
            var result = new ConfigReadResult();
            JObject root;
            try
            {
                var token = JToken.Parse(text ?? string.Empty, new JsonLoadSettings
                {
                    CommentHandling = CommentHandling.Ignore,
                    LineInfoHandling = LineInfoHandling.Load,
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Replace,
                });
                if (token is not JObject obj)
                {
                    result.Error = "the file is not a { ... } object";
                    return result;
                }
                root = obj;
            }
            catch (JsonReaderException e)
            {
                result.Error = "line " + e.LineNumber + ", position " + e.LinePosition + ": " + e.Message;
                return result;
            }
            catch (Exception e)
            {
                result.Error = e.GetType().Name + ": " + e.Message;
                return result;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in root.Properties())
            {
                string name = prop.Name;
                if (string.Equals(name, VersionKey, StringComparison.OrdinalIgnoreCase))
                {
                    if (TryNumber(prop.Value, out double ver) && !double.IsInfinity(ver))
                        result.FileVersion = (int)Math.Round(ver);
                    continue;
                }
                if (!SettingsSchema.TryGet(name, out var p))
                {
                    result.Unknown.Add(new KeyValuePair<string, string>(name, prop.Value.ToString(Formatting.None)));
                    continue;
                }
                if (!seen.Add(p.Key))
                    result.Issues.Add(new ConfigIssue(p.Key, ConfigIssueKind.Duplicate, "appears more than once - the last one is used"));

                if (!TryConvert(p, prop.Value, out double raw, out string? why))
                {
                    result.Values.Remove(p.Key);
                    result.Issues.Add(new ConfigIssue(p.Key, ConfigIssueKind.Invalid,
                        "\"" + prop.Value.ToString(Formatting.None) + "\" is not " + why + " - using the default " + p.Format(p.Default)));
                    continue;
                }
                double v = p.Normalize(raw, out bool clamped, out bool rounded);
                if (clamped)
                    result.Issues.Add(new ConfigIssue(p.Key, ConfigIssueKind.Clamped,
                        Show(raw) + " is outside " + p.Format(p.Min) + ".." + p.Format(p.Max) + " - using " + p.Format(v)));
                else if (rounded)
                    result.Issues.Add(new ConfigIssue(p.Key, ConfigIssueKind.Rounded,
                        Show(raw) + " must be a whole number - using " + p.Format(v)));
                result.Values[p.Key] = v;
            }

            foreach (var p in SettingsSchema.All)
                if (!result.Values.ContainsKey(p.Key))
                    result.Missing.Add(p);
            return result;
        }

        /// <summary>
        /// Pushes the defaults a newer format changed into a file of an older format that still carries
        /// the OLD default (a value the player set himself is kept - a hand-set value equal to the old
        /// default cannot be told apart, and moves too). Idempotent; a file without a stamp is taken as
        /// the current format (it was written by hand). Fills <see cref="ConfigReadResult.Migrated"/>.
        /// Format 2 (step 13, PAUSE ONLY): <c>AttackRateAiDecisions</c> true → the new default (off: on
        /// top of the no-attack timer it double-counts); <c>PlayerBarOffsetBottom</c> 54 → the new default
        /// (30: the Attack recovery bar sits above your bar, both under the vanilla health bar).
        /// Format 3 (step 14, Anton after his 240v240): <c>MinMoveSpeedMultiplier</c> 0.3 → the new default
        /// (0.7: an empty man runs at 70% of his pace - 0.3 was "too slow, unrealistic"). A format-1 file
        /// gets both steps. Format 4 (step 20b, Anton after his playtest of 2026-09-28): <c>MinMoveSpeedMultiplier</c>
        /// 0.7 → the new default (0.6, "the sweet spot") in a format-3 file (the one format whose default was 0.7 -
        /// an older file's 0.3 went straight to the new default above), <c>AttackAnimationMinPercent</c> 100 → the new
        /// default (85: the swing slows in a straight line to 85% at empty) in a format 2-3 file (the key came with
        /// format 2). Format 5 (step 22, Anton 2026-09-29, for slower battles): <c>RegenRateNearFullPercent</c> 50 → the
        /// new default (100: the straight-line refill, empty → full in FullRegenSecondsStanding) in a format 3-4 file (the key
        /// came with format 3); <c>MinMoveSpeedMultiplier</c> 0.6 → the new default (0.3) in a format-4 file (the one
        /// format whose default was 0.6 - a format-3 file's 0.7 and an older file's 0.3 go straight to it by the rules
        /// above). The rewrite is ConfigStore's (logged "rewrote config.json as format 5 …").
        /// </summary>
        public static List<string> Migrate(ConfigReadResult read) => Migrate(read, p => p.Default);

        /// <summary><see cref="Migrate(ConfigReadResult)"/> with the new defaults given - the unit tests run on
        /// DESIGN's INITIAL values, where a tuned default (step 20b's 0.6 and 85) does not exist, so they hand in
        /// defaults.json's.</summary>
        public static List<string> Migrate(ConfigReadResult read, Func<ParamDef, double> defaultOf)
        {
            if (read == null || !read.Ok || defaultOf == null) return new List<string>();
            int from = read.FileVersion ?? FormatVersion;
            if (from < 2)
            {
                MoveOldDefault(read, SettingsSchema.AttackRateAiDecisions, 1,
                    "step 13: the no-attack timer carries the slow-down - the AI-decision scaling on top of it double-counts", from, defaultOf);
                MoveOldDefault(read, SettingsSchema.PlayerBarOffsetBottom, 54,
                    "step 13: the Attack recovery bar sits above your bar, so the pair moved down under the vanilla health bar", from, defaultOf);
            }
            if (from < 3)
            {
                MoveOldDefault(read, SettingsSchema.MinMoveSpeedMultiplier, 0.3,
                    "step 14: an empty man runs at 70% of his pace - 0.3 was too slow", from, defaultOf);
            }
            if (from < 4)
            {
                // the old default as THAT file's format knew it: 0.7 only in format 3, 100 since format 2
                if (from >= 3)
                    MoveOldDefault(read, SettingsSchema.MinMoveSpeedMultiplier, 0.7,
                        "step 20b, Anton after his playtest: an empty man runs at 60% of his pace - the sweet spot", from, defaultOf);
                if (from >= 2)
                    MoveOldDefault(read, SettingsSchema.AttackAnimationMinPercent, 100,
                        "step 20b, Anton after his playtest: a tired man's attack animations slow in a straight line to 85% at empty - the pause in seconds stays as it was", from, defaultOf);
            }
            if (from < 5)
            {
                // step 22: the old default as THAT file's format knew it - 50 since format 3 (the key's birth), 0.6 only in format 4
                if (from >= 3)
                    MoveOldDefault(read, SettingsSchema.RegenRateNearFullPercent, 50,
                        "step 22, Anton for slower battles: the refill is a straight line again - empty to full in the refill time at a walk, no faster when low", from, defaultOf);
                if (from >= 4)
                    MoveOldDefault(read, SettingsSchema.MinMoveSpeedMultiplier, 0.6,
                        "step 22, Anton for slower battles: an empty man runs at 30% of his pace again", from, defaultOf);
            }
            return read.Migrated;
        }

        private static void MoveOldDefault(ConfigReadResult read, ParamDef p, double oldDefault, string why, int from, Func<ParamDef, double> defaultOf)
        {
            if (!read.Values.TryGetValue(p.Key, out double v) || Math.Abs(v - oldDefault) > 1e-9) return;
            double now = defaultOf(p);
            if (double.IsNaN(now) || Math.Abs(now - oldDefault) < 1e-9) return; // the default is back where it was: nothing to push
            read.Values[p.Key] = now;
            read.Migrated.Add(p.Key + ": " + p.Format(oldDefault) + " → " + p.Format(now) + " (format " + from + " → " + FormatVersion + ", the old default; " + why + ")");
        }

        /// <summary>
        /// Makes <paramref name="settings"/> match a read file: every valid value is set, every
        /// missing or invalid one goes back to its default ("missing → default"), all with
        /// <paramref name="source"/> (each real change raises Changed → one [config] line). A
        /// failed read changes nothing. Returns how many values changed.
        /// </summary>
        public static int Apply(ConfigReadResult read, TraxSettings settings, string source = SettingSources.File)
        {
            if (read == null || !read.Ok) return 0;
            int changed = 0;
            foreach (var p in SettingsSchema.All)
            {
                double v = read.Values.TryGetValue(p.Key, out var found) ? found : p.Default;
                if (settings.Set(p, v, source).Changed) changed++;
            }
            return changed;
        }

        private static string Show(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);

        private static bool TryConvert(ParamDef p, JToken token, out double value, out string? why)
        {
            value = 0;
            why = null;
            if (p.Type == ParamType.Bool)
            {
                why = "true or false";
                switch (token.Type)
                {
                    case JTokenType.Boolean:
                        value = token.Value<bool>() ? 1 : 0;
                        return true;
                    case JTokenType.Integer:
                        long n = token.Value<long>();
                        if (n != 0 && n != 1) return false;
                        value = n;
                        return true;
                    case JTokenType.String:
                        switch ((token.Value<string>() ?? string.Empty).Trim().ToLowerInvariant())
                        {
                            case "true": case "yes": case "on": case "1": value = 1; return true;
                            case "false": case "no": case "off": case "0": value = 0; return true;
                            default: return false;
                        }
                    default:
                        return false;
                }
            }

            why = p.Type == ParamType.Int ? "a whole number" : "a number";
            if (!TryNumber(token, out value)) return false;
            if (double.IsNaN(value) || double.IsInfinity(value)) return false;
            return true;
        }

        /// <summary>A JSON number, or a string holding one ("0.75", and the decimal comma
        /// "0,75" a Bulgarian keyboard types).</summary>
        private static bool TryNumber(JToken token, out double value)
        {
            value = 0;
            switch (token.Type)
            {
                case JTokenType.Integer:
                case JTokenType.Float:
                    value = token.Value<double>();
                    return true;
                case JTokenType.String:
                    var s = (token.Value<string>() ?? string.Empty).Trim().Replace(',', '.');
                    return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
                default:
                    return false;
            }
        }
    }
}

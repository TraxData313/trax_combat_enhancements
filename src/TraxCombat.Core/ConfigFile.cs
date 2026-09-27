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
        /// <summary>The format stamp written into every file. Bump it (and migrate in the
        /// reader) when a later version must change the meaning of an existing key or push a
        /// new default into files that already carry the old one.</summary>
        public const int FormatVersion = 1;

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

            foreach (var group in SettingsSchema.Groups)
            {
                sb.Append(',').Append(Nl).Append(Nl);
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
                    sb.Append("  // (").Append(p.DefaultAndRangeText).Append(')').Append(Nl);
                    double v = values != null && values.TryGetValue(p.Key, out var found) ? p.Normalize(found) : p.Default;
                    sb.Append("  \"").Append(p.Key).Append("\": ").Append(p.Format(v));
                }
            }

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
                "- Delete this file to get every default back.",
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

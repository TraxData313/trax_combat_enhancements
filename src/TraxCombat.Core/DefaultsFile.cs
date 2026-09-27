using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TraxCombat.Core
{
    /// <summary>What <see cref="DefaultsFile.Check"/> found in one defaults.json text.</summary>
    public sealed class DefaultsCheck
    {
        /// <summary>Null when the text parsed as one { ... } object (comments are fine).</summary>
        public string? ParseError { get; internal set; }

        /// <summary>Every value that is valid (right type, inside its range), by canonical key.</summary>
        public Dictionary<string, double> Values { get; } = new Dictionary<string, double>(StringComparer.Ordinal);

        /// <summary>Settings of the schema that have no key in the file (exact casing).</summary>
        public List<string> Missing { get; } = new List<string>();

        /// <summary>Keys that are not settings (a typo, an old name, a wrong casing).</summary>
        public List<string> Unknown { get; } = new List<string>();

        /// <summary>Values of the wrong type, outside the range or too precise - "Key: why".</summary>
        public List<string> Problems { get; } = new List<string>();

        /// <summary>Null when every // line, group heading and key is where and as
        /// <see cref="DefaultsFile.Write"/> would put it (values themselves are not compared);
        /// otherwise the first difference, with its line number.</summary>
        public string? CommentsDifference { get; internal set; }

        public bool ValuesOk => ParseError == null && Missing.Count == 0 && Unknown.Count == 0 && Problems.Count == 0;

        public bool Ok => ValuesOk && CommentsDifference == null;

        /// <summary>A plain report, one finding per line - what the tests and tools/DefaultsTool print.</summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            if (ParseError != null) sb.Append("does not parse: ").Append(ParseError).Append('\n');
            foreach (var k in Missing) sb.Append("missing: \"").Append(k).Append("\" - add it with its default value\n");
            foreach (var k in Unknown) sb.Append("not a setting: \"").Append(k).Append("\" - rename or delete it\n");
            foreach (var p in Problems) sb.Append("bad value: ").Append(p).Append('\n');
            if (CommentsDifference != null)
                sb.Append("comments/layout out of step with the schema - ").Append(CommentsDifference)
                  .Append("\n  fix: ").Append(DefaultsFile.RefreshCommand).Append("   (rewrites every comment and the order, keeps every value)\n");
            return sb.Length == 0 ? "defaults.json is complete and in step with the schema" : sb.ToString().TrimEnd('\n');
        }
    }

    /// <summary>
    /// <c>defaults.json</c> - THE ONE TRUTH for every setting's default value (DESIGN §2c).
    ///
    /// The file sits at the repo root, where Anton tunes it. The build embeds it in
    /// TraxCombat.Core.dll (resource <see cref="ResourceName"/>), and every <see cref="ParamDef"/>
    /// takes its <see cref="ParamDef.Default"/> from that embedded copy while the schema is built -
    /// <see cref="SettingsSchema"/> declares types, ranges, groups, labels and descriptions but no
    /// default values. Everything that says "default" reads ParamDef.Default: a new
    /// <see cref="TraxSettings"/>, the first-run config.json, a key missing from config.json, MCM's
    /// Default preset (its Reset buttons), MCM's "Revert all to defaults", the "(default …)" in the
    /// config file's comments, the MCM hints and the log.
    ///
    /// The text: a header, then every setting in file/MCM order under its group heading, its
    /// plain-words description as // lines above it and its range - produced by <see cref="Write"/>,
    /// which MCM's "Save current values as a defaults file" button uses too, so an exported file can
    /// be copied over the repo's as it is. <see cref="Check"/> is what the tests and
    /// tools/DefaultsTool run: every key present, nothing unknown, each value of the right JSON
    /// type (true/false, a whole number, a number) inside its range, and the comments exactly as
    /// <see cref="Write"/> would write them (values are not compared - any number formatting is
    /// fine).
    ///
    /// Fail safe: a setting the EMBEDDED file lacks or holds badly (the tests make that
    /// impossible, but a hand-built DLL could) takes the bottom of its range (false for a switch),
    /// or its value clamped into the range, and the problem is listed in <see cref="Problems"/> -
    /// the mod logs each at load and the offline smoke fails on any.
    ///
    /// Unit tests do not run on this file: the test assembly hands DESIGN.md's Default column
    /// (the INITIAL values) in through <see cref="UseValuesForTests"/> before the schema is built,
    /// so tuning defaults.json never breaks a test that counts blows.
    /// </summary>
    public static class DefaultsFile
    {
        public const string FileName = "defaults.json";

        /// <summary>The manifest resource name the Core project gives the embedded copy.</summary>
        public const string ResourceName = "TraxCombat.Core.defaults.json";

        /// <summary>From the repo root: rewrites every comment, the order and the layout of
        /// defaults.json from the schema and keeps every value.</summary>
        public const string RefreshCommand = "dotnet run --project tools/DefaultsTool -- refresh";

        private const string Nl = "\r\n";

        private static readonly object Gate = new object();
        private static Source? _source;
        private static bool _used;

        private sealed class Source
        {
            public Source(string name)
            {
                Name = name;
            }

            public string Name { get; }

            public Dictionary<string, JToken> Tokens { get; } = new Dictionary<string, JToken>(StringComparer.Ordinal);

            public List<string> Problems { get; } = new List<string>();
        }

        // ------------------------------------------------------------------ the source the schema reads

        /// <summary>Where the schema's defaults came from - "defaults.json (embedded in
        /// TraxCombat.Core.dll)" in the game.</summary>
        public static string SourceName => Current.Name;

        /// <summary>How many keys the source held.</summary>
        public static int KeysRead => Current.Tokens.Count;

        /// <summary>Everything wrong with the source the schema took its defaults from: missing
        /// keys, bad values (each with the value used instead), keys that are not settings, a file
        /// that did not load. Empty = every default came straight from the file.</summary>
        public static IReadOnlyList<string> Problems
        {
            get
            {
                var src = Current;
                var all = SettingsSchema.All; // builds the schema first (it records its own problems)
                var list = new List<string>();
                lock (Gate)
                {
                    list.AddRange(src.Problems);
                    foreach (var key in src.Tokens.Keys)
                        if (!all.Any(p => p.Key == key))
                            list.Add("\"" + key + "\" in " + src.Name + " is not a setting of this version - ignored");
                }
                return list;
            }
        }

        /// <summary>The embedded copy exactly as the build stored it ("" if missing) - read
        /// straight from the resource, whatever source the schema uses.</summary>
        public static string ReadEmbeddedText()
        {
            using var stream = typeof(DefaultsFile).Assembly.GetManifestResourceStream(ResourceName);
            if (stream == null) return string.Empty;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }

        /// <summary>
        /// Tests only: the schema takes its defaults from <paramref name="values"/> (typed as the
        /// JSON would be: bools as 1/0 for switches, whole numbers, numbers) instead of the embedded
        /// file. Must run before anything touches <see cref="SettingsSchema"/>.
        /// </summary>
        internal static void UseValuesForTests(IReadOnlyDictionary<string, JToken> values, string name)
        {
            lock (Gate)
            {
                if (_used) throw new InvalidOperationException("the schema has already taken its defaults - call UseValuesForTests before anything touches SettingsSchema");
                var src = new Source(name);
                foreach (var pair in values) src.Tokens[pair.Key] = pair.Value;
                _source = src;
            }
        }

        /// <summary>One setting's default, called by the <see cref="ParamDef"/> constructor while the
        /// schema is built. Never throws; a problem is recorded and a safe value used.</summary>
        internal static double DefaultFor(string key, ParamType type, double min, double max)
        {
            var src = Current;
            lock (Gate)
            {
                _used = true;
                return Resolve(src, key, type, min, max);
            }
        }

        /// <summary>
        /// Tests: what the game would make of <paramref name="text"/> as its embedded
        /// defaults.json - the very parse and per-setting resolution the schema runs at load,
        /// fallbacks and all. <paramref name="problems"/> lists everything
        /// <see cref="Problems"/> would.
        /// </summary>
        internal static Dictionary<string, double> ResolveAll(string text, out List<string> problems)
        {
            var src = LoadSource("the text", text);
            var values = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var p in SettingsSchema.All)
                values[p.Key] = Resolve(src, p.Key, p.Type, p.Min, p.Max);
            problems = new List<string>(src.Problems);
            foreach (var key in src.Tokens.Keys)
                if (!SettingsSchema.All.Any(p => p.Key == key))
                    problems.Add("\"" + key + "\" in " + src.Name + " is not a setting of this version - ignored");
            return values;
        }

        private static double Resolve(Source src, string key, ParamType type, double min, double max)
        {
            if (!src.Tokens.TryGetValue(key, out var token))
            {
                double fallback = Fallback(type, min);
                src.Problems.Add("\"" + key + "\" is missing from " + src.Name + " - using " + Show(type, fallback));
                return fallback;
            }
            if (!TryValue(token, type, min, max, out double value, out string? problem))
            {
                double fallback = Fallback(type, min);
                src.Problems.Add(key + ": " + problem + " - using " + Show(type, fallback));
                return fallback;
            }
            if (problem != null) src.Problems.Add(key + ": " + problem + " - using " + Show(type, value));
            return value;
        }

        private static Source Current
        {
            get
            {
                lock (Gate) return _source ??= LoadSource("defaults.json (embedded in TraxCombat.Core.dll)", ReadEmbeddedText());
            }
        }

        private static Source LoadSource(string name, string text)
        {
            var src = new Source(name);
            try
            {
                if (text.Length == 0)
                {
                    src.Problems.Add(name + " is empty or missing (a build problem) - every setting falls back to the bottom of its range");
                    return src;
                }
                var token = JToken.Parse(text, new JsonLoadSettings
                {
                    CommentHandling = CommentHandling.Ignore,
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Replace,
                });
                if (token is JObject obj)
                {
                    foreach (var prop in obj.Properties()) src.Tokens[prop.Name] = prop.Value;
                }
                else
                {
                    src.Problems.Add(name + " is not a { ... } object - every setting falls back to the bottom of its range");
                }
            }
            catch (Exception e)
            {
                src.Problems.Add(name + " did not parse (" + e.Message + ") - every setting falls back to the bottom of its range");
            }
            return src;
        }

        private static double Fallback(ParamType type, double min) => type == ParamType.Bool ? 0 : min;

        // ------------------------------------------------------------------ one value, strictly

        /// <summary>
        /// A defaults.json value for a setting of <paramref name="type"/>: switches must be JSON
        /// true/false, whole-number settings a JSON integer, the rest any JSON number. False = the
        /// value is unusable (<paramref name="problem"/> says why). True with a problem = usable but
        /// adjusted: clamped into [min, max], or rounded to <see cref="ParamDef.FloatDecimals"/>.
        /// </summary>
        public static bool TryValue(JToken token, ParamType type, double min, double max, out double value, out string? problem)
        {
            value = 0;
            problem = null;
            string raw = token.ToString(Formatting.None);
            switch (type)
            {
                case ParamType.Bool:
                    if (token.Type != JTokenType.Boolean)
                    {
                        problem = raw + " is not true or false";
                        return false;
                    }
                    value = token.Value<bool>() ? 1 : 0;
                    return true;
                case ParamType.Int:
                    if (token.Type != JTokenType.Integer)
                    {
                        problem = raw + " is not a whole number (like 50)";
                        return false;
                    }
                    value = token.Value<double>();
                    break;
                default:
                    if (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)
                    {
                        problem = raw + " is not a number (like 0.75)";
                        return false;
                    }
                    value = token.Value<double>();
                    if (double.IsNaN(value) || double.IsInfinity(value))
                    {
                        problem = raw + " is not a number (like 0.75)";
                        return false;
                    }
                    double rounded = Math.Round(value, ParamDef.FloatDecimals, MidpointRounding.AwayFromZero);
                    if (Math.Abs(rounded - value) > 1e-12)
                    {
                        problem = raw + " has more than " + ParamDef.FloatDecimals + " decimals";
                        value = rounded;
                    }
                    break;
            }
            if (value < min || value > max)
            {
                problem = raw + " is outside its range " + Show(type, min) + " to " + Show(type, max);
                value = value < min ? min : max;
            }
            return true;
        }

        private static string Show(ParamType type, double v) =>
            type == ParamType.Bool ? (v != 0 ? "true" : "false")
            : type == ParamType.Int ? ((long)Math.Round(v)).ToString(CultureInfo.InvariantCulture)
            : v.ToString("0.0###", CultureInfo.InvariantCulture);

        // ------------------------------------------------------------------ write

        /// <summary>The header of every defaults.json - the repo's and an exported one alike.</summary>
        private static readonly string[] Header =
        {
            "Trax Combat Enhancements - DEFAULTS (defaults.json)",
            "",
            "The one place every setting's default lives. The build embeds this file in the mod: a first",
            "run writes these values into config.json, a key missing from config.json falls back to them,",
            "and MCM's Reset and \"Revert all to defaults\" restore them.",
            "",
            "To change a default: edit the value after the colon (true/false, or a number with a dot:",
            "0.75), save, build and deploy. Keep every key - the tests check each value's type and range.",
            "",
            "The // lines are written by the mod from its list of settings. When a setting's wording,",
            "range or place changes in the code, rewrite them (every value is kept) from the repo root:",
            "    " + RefreshCommand,
            "",
            "The game never reads this file for a player's own values - those live in config.json in",
            ConfigFile.FolderForHumans,
            "MCM's \"Save current values as a defaults file\" writes the values in play as a file like this",
            "one, next to config.json - copy it over the repo's defaults.json to make them the defaults.",
        };

        /// <summary>
        /// The whole defaults.json text for <paramref name="values"/> (a key missing there takes
        /// <see cref="ParamDef.Default"/>): the header, then every setting in group order with its
        /// description and range, exactly the layout <see cref="Check"/> expects.
        /// </summary>
        public static string Write(IReadOnlyDictionary<string, double> values)
        {
            var sb = new StringBuilder(8192);
            foreach (var line in Header)
                sb.Append(("// " + line).TrimEnd()).Append(Nl);
            sb.Append('{').Append(Nl);
            ConfigFile.AppendSettings(sb, values, RangeText, afterAnEntry: false);
            sb.Append(Nl).Append('}').Append(Nl);
            return sb.ToString();
        }

        /// <summary>"(range 0 to 100)" / "(true or false)" - the last // line above a key.</summary>
        public static string RangeText(ParamDef p) =>
            p.Type == ParamType.Bool ? "true or false" : "range " + p.Format(p.Min) + " to " + p.Format(p.Max);

        // ------------------------------------------------------------------ check

        /// <summary>Checks a defaults.json text against the schema - see <see cref="DefaultsCheck"/>.
        /// Never throws.</summary>
        public static DefaultsCheck Check(string text)
        {
            var result = new DefaultsCheck();
            JObject root;
            try
            {
                var token = JToken.Parse(text ?? string.Empty, new JsonLoadSettings
                {
                    CommentHandling = CommentHandling.Ignore,
                    LineInfoHandling = LineInfoHandling.Load,
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                });
                if (token is not JObject obj)
                {
                    result.ParseError = "the file is not a { ... } object";
                    return result;
                }
                root = obj;
            }
            catch (JsonReaderException e)
            {
                result.ParseError = "line " + e.LineNumber + ", position " + e.LinePosition + ": " + e.Message;
                return result;
            }
            catch (Exception e)
            {
                result.ParseError = e.GetType().Name + ": " + e.Message;
                return result;
            }

            var present = new HashSet<string>(StringComparer.Ordinal);
            foreach (var prop in root.Properties())
            {
                if (!SettingsSchema.TryGet(prop.Name, out var p) || p.Key != prop.Name)
                {
                    result.Unknown.Add(prop.Name + (p != null && p.Key != prop.Name ? " (keys are case-sensitive here: " + p.Key + ")" : string.Empty));
                    continue;
                }
                present.Add(p.Key);
                if (!TryValue(prop.Value, p.Type, p.Min, p.Max, out double v, out string? problem) || problem != null)
                {
                    result.Problems.Add(p.Key + ": " + problem);
                    continue;
                }
                result.Values[p.Key] = v;
            }
            foreach (var p in SettingsSchema.All)
                if (!present.Contains(p.Key)) result.Missing.Add(p.Key);

            result.CommentsDifference = FirstDifference(Shape(text ?? string.Empty), Shape(Write(result.Values)));
            return result;
        }

        private static readonly Regex ValueLine = new Regex("^(\\s*)\"([^\"]*)\"\\s*:\\s*(.*?)\\s*(,?)$", RegexOptions.CultureInvariant);

        /// <summary>The text as lines with every value blanked out (the key, its indentation and
        /// the trailing comma stay), trailing spaces and trailing empty lines dropped.</summary>
        private static List<string> Shape(string text)
        {
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Select(l => l.TrimEnd()).ToList();
            for (int i = 0; i < lines.Count; i++)
            {
                var m = ValueLine.Match(lines[i]);
                if (m.Success && !lines[i].TrimStart().StartsWith("//", StringComparison.Ordinal))
                    lines[i] = m.Groups[1].Value + "\"" + m.Groups[2].Value + "\": <value>" + m.Groups[4].Value;
            }
            while (lines.Count > 0 && lines[lines.Count - 1].Length == 0) lines.RemoveAt(lines.Count - 1);
            return lines;
        }

        private static string? FirstDifference(List<string> actual, List<string> expected)
        {
            int n = Math.Max(actual.Count, expected.Count);
            for (int i = 0; i < n; i++)
            {
                string? a = i < actual.Count ? actual[i] : null;
                string? e = i < expected.Count ? expected[i] : null;
                if (a == e) continue;
                return "line " + (i + 1) + ": expected " + (e == null ? "(end of file)" : "\"" + e + "\"")
                    + ", found " + (a == null ? "(end of file)" : "\"" + a + "\"");
            }
            return null;
        }
    }
}

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
        /// the refill a straight line again - RegenRateNearFullPercent 50 → 100 - and the run-speed floor 0.6 → 0.3), 6 = step 36
        /// (the AI sprint by order: SprintAiChargeSharePercent renamed SprintAiChargeUsePercent, SprintAiMoveSharePercent dropped),
        /// 7 = step 37 (Anton: one number per order, the reserve - the three SprintAi*UsePercent keys dropped, and step 33's two
        /// share keys with them; SprintAiAdvanceKeepPercent 0 → 50 and SprintAiMoveKeepPercent 70 → 80 in a format-6 file - the
        /// drops by <see cref="Migrate(ConfigReadResult, Func{ParamDef, double})"/> for a file of any format), 8 = step 43 (Anton: cheaper
        /// swings and wrong-side blocks - CostPerBlow 10 → 3 and CostPerWrongSideShieldBlock 5 → 2 in any older file still holding them),
        /// 9 = step 46 (Anton's playtest: CostPerBlow 3 → 7 in a format-8 file still holding step 43's default; a tuned value kept and
        /// noted; an older file's 10 goes straight to 7 through step 43's move), 10 = step 56 (Anton's tournament: right blocks favour the
        /// defender - RightBlockStaggerMaxPercent 80 → 30, RightBlockKnockdownMaxPercent 40 → 10, ParryStaggerMaxPercent 80 → 30,
        /// ParryKnockdownMaxPercent 40 → 10, RiposteStaggerMaxPercent 50 → 70 and BlockStunTiredExtraPercent 100 → 0 - the spike's lever 2
        /// failed - in an older file still holding them; a tuned value kept and noted), 11 = step 63 (Anton's battle of 2026-10-19 11:02:
        /// ImpactFallBackupSeconds 0.5 → 0.2 - the game plays its falls at the blow, so the backup comes sooner - in a file still holding
        /// step 62's default; ImpactRearPercent 200 → 100 and ImpactFlankPercent 140 → 100 - direction is pressure now, ImpactRearPressure /
        /// ImpactFlankPressure; a tuned value kept and noted), 12 = step 64 (Anton's looter battles of 2026-10-19: the fever sprint starts
        /// nearer - FeverSprintMinMetres 3 → 4, FeverSprintMaxMetres 12 → 8 - so a small store lasts to the enemy; the charge carries longer -
        /// ImpactChargeCarrySeconds 1.5 → 3 - to a slow swing's blow; a tuned value kept and noted), 13 = step 66 (Anton's looter battle of 2026-10-19 14:16 - no AI kick seen: CostPerKickOrBash
        /// 3 → 5, AiKicksPerMinute 0.1 → 1.2, AiKickCooldownSeconds 10 → 8 in a file still holding them; a tuned value kept and noted), 14 = step 68
        /// (Toni's 100 v 100 of 2026-10-19 - "tire the men more to slow down the combat again a bit after the first impact": CostPerBlow 7 → 8,
        /// CostPerWrongSideShieldBlock 2 → 5, CostPerShieldBlock 1 → 2 in a file still holding them; a tuned value kept and noted), 15 = step 71
        /// (Toni, 2026-10-19 - "they need to tire faster still": CostPerBlow 8 → 10, CostPerKickOrBash 5 → 6, CostPerWrongSideShieldBlock 5 → 6,
        /// CostPerShieldBlock 2 → 3, CostPerWeaponParry 2 → 3 in a file still holding them; a tuned value kept and noted; the refill
        /// unchanged), 16 = step 74 (the line-discipline map one rank deeper), 17 = step 78b (Toni's 225 v 229 battle of 2026-10-22 - "between the
        /// two fronts, never, not for a sec": a lull lasts until the unit's men in the fight have regained more - LullRecoverChargePercent 10 → 20,
        /// LullRecoverAdvancePercent 15 → 25, LullRecoverHoldPercent 20 → 30 in a file still holding them; a tuned value kept and noted; the wrap's
        /// own switch PressWrapAllowed is a new key, off). Step 78c removed the lull and its 29 keys without a new format: the reader ignores an
        /// unknown key anyway, and <c>DropRetiredLullKeys</c> drops them from any file (one note each) - nothing to move. 18 = step 80 (AI kicks land
        /// on enemies, not friends: AiKickFriendClearMetres - now the kick's strike path half-width along his facing - 0.5 → 0.6 in a file of format
        /// 14-17 still holding it; a tuned value kept and noted), 19 = step 81 (Toni 2026-10-22 - kicks and bashes as a share of the melee moves:
        /// SwingStackMinimum 2 → 3, CostPerKickOrBash 6 → 8 (a format 15-18 file), AiKickCooldownSeconds 8 → 2 - now the one minimum between two
        /// kicks or bashes (a format 13-18 file) in a file still holding them; a tuned value kept and noted; the 14 retired AI-kick keys -
        /// the rate, its factors, the bar gate, the wait for reach, the bash credit and the bash's own cooldown - dropped from any file by
        /// <c>DropRetiredKickKeys</c>, one note each), 20 = step 88 (Toni 2026-10-24 - a weapon push for men without a shield and new shares:
        /// AiShieldmanKickPercent 20 → 25, AiShieldmanBashPercent 35 → 40 in a format-19 file still holding them; a tuned value kept and noted; the
        /// new AiNoShieldPushPercent (20) is a new key - a file without it gets the default like any missing key), 21 = step 90 (Toni's 17:39
        /// battle of 2026-10-24 - only a man really in front blocks a kick / bash / push, and the strike path narrows: AiBashPathHalfWidthMetres
        /// 0.75 → 0.5 (bashes and pushes), AiKickFriendClearMetres 0.6 → 0.45 in a format 18-20 file still holding them; a tuned value kept and
        /// noted; AiKickBlockAheadMetres / AiKickBlockSideMetres are new keys), 22 = step 91b (Toni's 20:26 battle of 2026-10-24 - with step 91's
        /// AiBashReachMetres 1.1 the enemy stood within it in only 3 of 792 shieldman moves, the AI fights at 1.1-1.6 m, so no bash at all:
        /// AiBashReachMetres 1.1 → 0 - off, a bash anywhere in the kick reach as step 90 - in a format-21 file still holding it; a tuned value kept
        /// and noted), 23 = step 94 (Toni 2026-10-25 - a bigger base pool, cheaper swings: AthleticsPoolPerSkill 1.0 → 0.5 (any format) under the
        /// new AthleticsPoolBase 100 (a new key), CostPerBlow 10 → 7 (a format 15-22 file), CostPerKickOrBash 8 → 6 (a format 19-22 file); a tuned
        /// value kept and noted; an older file's cost went through steps 71 / 81 above straight to today's value), 24 = step 95 (Toni 2026-10-25 -
        /// "swing 30% of the time": AiShieldmanKickPercent 25 → 27, AiShieldmanBashPercent 40 → 43 (a format 20-23 file - a format-19 file's
        /// 20 / 35 went through step 88's block straight to today's value), AiNoShieldKickPercent 35 → 45 (19-23), AiNoShieldPushPercent 20 → 25
        /// (20-23); a tuned value kept and noted; the step-in bash's three keys are new), 25 = step 96 (Toni 2026-10-25 -
        /// "more defencive ... reduce the swings": AiAggressionFreshPercent 60 → 45, AiAggressionExhaustedPercent 20 → 15, ShieldInfantrySwingsLessPercent
        /// 30 → 40, FootMeleeSwingsLessPercent 15 → 25 (any older file - the four never moved before); a tuned value kept and noted; the guard's two keys are new).</summary>
        public const int FormatVersion = 25;

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
            SettingsPage? page = null;
            foreach (var group in SettingsSchema.Groups)
            {
                if (needComma) sb.Append(',').Append(Nl).Append(Nl);
                needComma = true;
                if (page != group.Page)
                {
                    // Step 59: a banner where each MCM page begins - the player page's settings first, then the Advanced page's.
                    page = group.Page;
                    sb.Append("  // ######## ").Append(PageBanner(group.Page)).Append(" ########").Append(Nl).Append(Nl);
                }
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
                    sb.Append("  \"").Append(p.Key).Append("\": ").Append(p.FormatJson(v));
                }
            }
        }

        /// <summary>Step 59: the banner above each MCM page's settings in config.json and defaults.json.</summary>
        public static string PageBanner(SettingsPage page) =>
            page == SettingsPage.Player
                ? "THE MAIN SETTINGS - MCM page \"" + SettingsSchema.PageTitle(page) + "\""
                : "ADVANCED - for tuning: MCM page \"" + SettingsSchema.PageTitle(page) + "\"";

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
                "- The main settings come first (MCM's page \"Trax Combat Enhancements\"); everything",
                "  else follows under ADVANCED (MCM's page \"Trax Combat Enhancements - Advanced\").",
                "",
                "Back to the defaults (the mod's defaults.json, built into it):",
                "- one setting: delete its line (the key and its value) - it takes its default at the next",
                "  battle start and is written back in;",
                "- everything: delete this file - every setting takes its default at the next battle start",
                "  (or game start) and a fresh file is written;",
                "- with MCM: Mod Options > Trax Combat Enhancements (or ... - Advanced) > Defaults >",
                "  \"Revert all to defaults\" - every setting of BOTH pages (applies at once, even",
                "  mid-battle, and rewrites this file).",
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
            if (from == 6)
            {
                // step 37: the reserves' old defaults existed only in format 6 (step 36 brought the keys)
                MoveOldDefault(read, SettingsSchema.SprintAiAdvanceKeepPercent, 0,
                    "step 37, Anton: one number per order - an advance sprints what he holds above a half-store reserve", from, defaultOf);
                MoveOldDefault(read, SettingsSchema.SprintAiMoveKeepPercent, 70,
                    "step 37, Anton: one number per order - a move sprints what he holds above an 80% reserve", from, defaultOf);
            }
            if (from < 8)
            {
                // step 43: both old defaults go back to step 5c (CostPerBlow) and step 22 (CostPerWrongSideShieldBlock) - every older
                // format knew them; a file without the key simply gets the new default
                // (step 46: such a file goes straight to TODAY's default - 7 since Anton's playtest - not through step 43's 3)
                MoveOldDefault(read, SettingsSchema.CostPerBlow, 10,
                    "steps 43 / 46, Anton: a swing or shot costs less - the AI's eagerness now follows its bar (step 43's 3, raised to 7 after his playtest)", from, defaultOf);
                MoveOldDefault(read, SettingsSchema.CostPerWrongSideShieldBlock, 5,
                    "step 43, Anton: a wrong-side shield block costs 2, like a weapon parry", from, defaultOf);
            }
            if (from == 8)
            {
                // step 46: step 43's 3 was the default only in format 8 (an older file's 3 was his own); his own value is kept - said once
                const string why = "step 46, Anton's playtest: a swing or shot costs 7 Athletics (step 43's 3 was too cheap)";
                if (!MoveOldDefault(read, SettingsSchema.CostPerBlow, 3, why, from, defaultOf))
                    KeptTuned(read, SettingsSchema.CostPerBlow, 3, why, from, defaultOf);
            }
            if (from < 10)
            {
                // step 56: the impact keys came with step 48 (format 9), so only a format-9 file can hold them - an older file without
                // the key simply gets the new default; a hand-set value is kept and said once
                const string block = "step 56, Anton's tournament: a correct block is mostly the ATTACKER's risk - the blocker is broken only on a big mismatch";
                MoveOrKeep(read, SettingsSchema.RightBlockStaggerMaxPercent, 80, block, from, defaultOf);
                MoveOrKeep(read, SettingsSchema.RightBlockKnockdownMaxPercent, 40, block, from, defaultOf);
                MoveOrKeep(read, SettingsSchema.ParryStaggerMaxPercent, 80, block, from, defaultOf);
                MoveOrKeep(read, SettingsSchema.ParryKnockdownMaxPercent, 40, block, from, defaultOf);
                MoveOrKeep(read, SettingsSchema.RiposteStaggerMaxPercent, 50,
                    "step 56, Anton's tournament: a tired man flailing into a good guard reels - the riposte up to 70%", from, defaultOf);
                MoveOrKeep(read, SettingsSchema.BlockStunTiredExtraPercent, 100,
                    "step 56: the impact spike's lever 2 FAILED - a longer block stun changes nothing the engine plays; the stagger is the window", from, defaultOf);
            }
            if (from < 11)
            {
                // step 63: step 62's keys came in format 10 (no format bump then), so only a format-10 file can hold them - an older file
                // without the key simply gets the new default; a hand-set value is kept and said once
                MoveOrKeep(read, SettingsSchema.ImpactFallBackupSeconds, 0.5,
                    "step 63, Anton's battle: the game plays a decided fall at the blow itself (friends' blows are no longer decided) - the backup comes after 0.2 s", from, defaultOf);
                const string rear = "step 63, Anton's battle: a blow from behind now adds PRESSURE straight (ImpactRearPressure / ImpactFlankPressure) - the multiplier at x1, or the direction counts twice";
                MoveOrKeep(read, SettingsSchema.ImpactRearPercent, 200, rear, from, defaultOf);
                MoveOrKeep(read, SettingsSchema.ImpactFlankPercent, 140, rear, from, defaultOf);
            }
            if (from < 12)
            {
                // step 64: a fever sprint from 12 m spent a looter's 2 s store before the enemy's line - it starts at 4-8 m now (and runs into contact)
                const string fever = "step 64, Anton's looter battles: a fever sprint starts nearer, so a small store lasts to the enemy (and runs into contact)";
                MoveOrKeep(read, SettingsSchema.FeverSprintMinMetres, 3, fever, from, defaultOf);
                MoveOrKeep(read, SettingsSchema.FeverSprintMaxMetres, 12, fever, from, defaultOf);
                MoveOrKeep(read, SettingsSchema.ImpactChargeCarrySeconds, 1.5,
                    "step 64, Anton's looter battles: the charge carries 3 s after a sprint (and an attack begun in it stays a charge to its blow)", from, defaultOf);
            }
            if (from < 13)
            {
                // step 66: CostPerKickOrBash came with step 18 (any older file may hold its 3); the AI kick keys came with step 65 (format 12, no
                // bump then) - a file without a key simply gets the new default; a hand-set value is kept and said once
                MoveOrKeep(read, SettingsSchema.CostPerKickOrBash, 3,
                    "step 66, Anton: a kick or a shield bash takes more of the energy - 5 Athletics points", from, defaultOf);
                const string kicks = "step 66, Anton's looter battle: no AI kick was ever seen at 0.1 a minute - a fresh man in contact now kicks about once a minute (shieldmen bash with their swings)";
                // (AiKicksPerMinute 0.1 → 1.2 was moved here until step 81 retired the key - DropRetiredKickKeys below)
                MoveOrKeep(read, SettingsSchema.AiKickCooldownSeconds, 10, kicks, from, defaultOf);
            }
            if (from < 14)
            {
                // step 68: CostPerBlow's 7 is the default since format 9 (an older file's 10 / 3 went through steps 43 / 46 above, and a format-8
                // file's own value was said there already); CostPerWrongSideShieldBlock's 2 since format 8 (an older file's 5 is today's default
                // again); CostPerShieldBlock's 1 since step 22 - a file without a key simply gets the new default; a hand-set value is kept and said once
                const string tire = "step 68, Toni: tire the men more to slow the combat again a bit after the first impact";
                if (from >= 9) MoveOrKeep(read, SettingsSchema.CostPerBlow, 7, tire + " - a swing or shot costs 8", from, defaultOf);
                if (from >= 8) MoveOrKeep(read, SettingsSchema.CostPerWrongSideShieldBlock, 2, tire + " - a wrong-side shield block costs 5 (a clumsy block tires)", from, defaultOf);
                MoveOrKeep(read, SettingsSchema.CostPerShieldBlock, 1, tire + " - a right-side shield block costs 2", from, defaultOf);
            }
            if (from < 15)
            {
                // step 71: CostPerBlow's 8, CostPerWrongSideShieldBlock's 5 and CostPerShieldBlock's 2 are the defaults since format 14 (an older
                // file's 7 / 2 / 1 went through step 68 above straight to today's values); CostPerKickOrBash's 5 since format 13 (an older file's 3
                // went through step 66 above); CostPerWeaponParry's 2 since step 22, in every format - a file without a key simply gets the new
                // default; a hand-set value is kept and said once; the refill (FullRegenSecondsStanding) is not touched
                const string faster = "step 71, Toni: the men tire faster still";
                if (from >= 14)
                {
                    MoveOrKeepIfMoved(read, SettingsSchema.CostPerBlow, 8, faster + " - a swing or shot costs 10", from, defaultOf);
                    MoveOrKeepIfMoved(read, SettingsSchema.CostPerWrongSideShieldBlock, 5, faster + " - a wrong-side shield block costs 6", from, defaultOf);
                    MoveOrKeepIfMoved(read, SettingsSchema.CostPerShieldBlock, 2, faster + " - a right-side shield block costs 3", from, defaultOf);
                }
                if (from >= 13) MoveOrKeepIfMoved(read, SettingsSchema.CostPerKickOrBash, 5, faster + " - a kick or a shield bash costs 6", from, defaultOf);
                MoveOrKeepIfMoved(read, SettingsSchema.CostPerWeaponParry, 2, faster + " - a weapon parry costs 3", from, defaultOf);
            }
            if (from < 16)
            {
                // step 74: the line-discipline map one rank deeper below T6 (Toni 2026-10-20: T0 7, T1 6, T2 5, T3 4, T4 3, T5 2, T6 1 - T6 stays);
                // the keys exist since format 15 (step 73 added them with no migration) - a file without a key simply gets the new default; a
                // hand-set value is kept and said once
                const string deeper = "step 74, Toni: the tier map one rank deeper (T6 stays 1)";
                MoveOrKeepIfMoved(read, SettingsSchema.LineDisciplineRanksT0, 6, deeper + " - a tier-0 man holds 7 ranks", from, defaultOf);
                MoveOrKeepIfMoved(read, SettingsSchema.LineDisciplineRanksT1, 5, deeper + " - a recruit holds 6 ranks (120 recruits: at most 20 files)", from, defaultOf);
                MoveOrKeepIfMoved(read, SettingsSchema.LineDisciplineRanksT2, 4, deeper + " - a tier-2 man holds 5 ranks", from, defaultOf);
                MoveOrKeepIfMoved(read, SettingsSchema.LineDisciplineRanksT3, 3, deeper + " - a tier-3 man holds 4 ranks", from, defaultOf);
                MoveOrKeepIfMoved(read, SettingsSchema.LineDisciplineRanksT4, 2, deeper + " - a tier-4 man holds 3 ranks", from, defaultOf);
                MoveOrKeepIfMoved(read, SettingsSchema.LineDisciplineRanksT5, 1, deeper + " - a veteran holds 2 ranks (60 of them: at most 30 files)", from, defaultOf);
            }
            if (from < 18 && from >= 14)
            {
                // step 80: AiKickFriendClearMetres (step 68's key, since format 14) is now the KICK's strike path half-width, measured from the line
                // of his facing - 0.5 → 0.6 (a body centre 0.6 m off his foot's line is still touched); a hand-set value is kept and said once
                MoveOrKeepIfMoved(read, SettingsSchema.AiKickFriendClearMetres, 0.5,
                    "step 80, Toni: AI kicks land on enemies, not friends - the kick's strike path along his facing (0.6 m either side; 0.45 since step 90)", from, defaultOf);
            }
            if (from < 19)
            {
                // step 81 (Toni 2026-10-22): the swing stack holds at least 3 (SwingStackMinimum since step 32 - any older file); a kick or bash costs 8
                // (6 since format 15 - an older file's 3 / 5 went through steps 66 / 71 above straight to today's value); AiKickCooldownSeconds (8 since
                // format 13) is now THE minimum between two kicks or bashes, 2 s (step 66's bash cooldown, retired); a hand-set value is kept and said once
                const string shares = "step 81, Toni: kicks and bashes as a share of the melee moves";
                MoveOrKeepIfMoved(read, SettingsSchema.SwingStackMinimum, 2, shares + " - the swing stack holds at least 3, and kicks / bashes spend from it", from, defaultOf);
                if (from >= 15) MoveOrKeepIfMoved(read, SettingsSchema.CostPerKickOrBash, 6, shares + " - a kick or a shield bash costs 8", from, defaultOf);
                if (from >= 13) MoveOrKeepIfMoved(read, SettingsSchema.AiKickCooldownSeconds, 8, shares + " - at least 2 s between two kicks or bashes (the shares are the frequency now)", from, defaultOf);
            }
            if (from < 20 && from >= 19)
            {
                // step 88 (Toni 2026-10-24): the shieldman's shares (step 81's keys, since format 19) kick 20 → 25, bash 35 → 40 - he swings 35 % of his
                // moves in kick reach; a man without a shield keeps his kick 35 and gets the new push share (a new key); a hand-set value is kept and said once
                const string push = "step 88, Toni: a weapon push for men without a shield and new shares";
                MoveOrKeepIfMoved(read, SettingsSchema.AiShieldmanKickPercent, 20, push + " - a shieldman kicks 25% of his moves in kick reach", from, defaultOf);
                MoveOrKeepIfMoved(read, SettingsSchema.AiShieldmanBashPercent, 35, push + " - a shieldman bashes 40% of his moves in kick reach (he swings 35%)", from, defaultOf);
            }
            if (from < 21 && from >= 18)
            {
                // step 90 (Toni 2026-10-24): the strike path narrows - a bash's / push's half-width (step 80's key, since format 18) 0.75 → 0.5, a kick's
                // (step 68's key; 0.6 since format 18 - an older file's 0.5 went through step 80's block above straight to today's value) 0.6 → 0.45;
                // only a man really in front blocks now (two new keys); a hand-set value is kept and said once
                const string front = "step 90, Toni: only a man really in front blocks a kick, bash or push";
                MoveOrKeepIfMoved(read, SettingsSchema.AiBashPathHalfWidthMetres, 0.75, front + " - a bash's / push's strike path is 0.5 m either side", from, defaultOf);
                MoveOrKeepIfMoved(read, SettingsSchema.AiKickFriendClearMetres, 0.6, front + " - a kick's strike path is 0.45 m either side", from, defaultOf);
            }
            if (from < 22 && from >= 21)
            {
                // step 91b (Toni 2026-10-24, his 20:26 battle): step 91's AiBashReachMetres (a new key in format 21, shipped 1.1) back to 0 - off, a bash
                // anywhere in the kick reach (step 90); the AI fights at 1.1-1.6 m, so 1.1 left no bash at all; a hand-set value is kept and said once
                MoveOrKeepIfMoved(read, SettingsSchema.AiBashReachMetres, 1.1,
                    "step 91b, Toni: with 1.1 m the enemy stood that close in only 3 of 792 shieldman moves - no bash at all; 0 = off, a shieldman bashes anywhere in the kick reach again", from, defaultOf);
            }
            if (from < 23)
            {
                // step 94 (Toni 2026-10-25: "no action at all for T1 troops in the early stages"): the pool = AthleticsPoolBase (a new key, 100) +
                // AthleticsPoolPerSkill × skill - the per-skill 1.0 → 0.5 (1.0 since step 5c, any format); CostPerBlow 10 → 7 (10 since format 15 - an
                // older file's 8 went through step 71's block above straight to today's value), CostPerKickOrBash 8 → 6 (8 since format 19 - an older
                // file's went through step 81's block); the block costs untouched; a hand-set value is kept and said once
                const string pool = "step 94, Toni: a bigger base pool, cheaper swings - the bar is 100 + half the Athletics skill";
                MoveOrKeepIfMoved(read, SettingsSchema.AthleticsPoolPerSkill, 1.0, pool + " - each skill point adds 0.5 to the base", from, defaultOf);
                if (from >= 15) MoveOrKeepIfMoved(read, SettingsSchema.CostPerBlow, 10, pool + " - a swing or shot costs 7", from, defaultOf);
                if (from >= 19) MoveOrKeepIfMoved(read, SettingsSchema.CostPerKickOrBash, 8, pool + " - a kick, a shield bash or a push costs 6", from, defaultOf);
            }
            if (from < 24 && from >= 19)
            {
                // step 95 (Toni 2026-10-25: "swing 30% of the time"): the shares of the melee moves in kick reach - a shieldman kick 25 → 27, bash 40 → 43
                // (25 / 40 since format 20; a format-19 file's 20 / 35 went through step 88's block above straight to today's value), a man without a shield
                // kick 35 → 45 (since format 19), push 20 → 25 (a new key in format 20); both swing 30%; a hand-set value is kept and said once
                const string shares = "step 95, Toni: swing 30% of the time - more kicks, bashes and pushes";
                if (from >= 20) MoveOrKeepIfMoved(read, SettingsSchema.AiShieldmanKickPercent, 25, shares + " - a shieldman kicks 27% of his moves in kick reach", from, defaultOf);
                if (from >= 20) MoveOrKeepIfMoved(read, SettingsSchema.AiShieldmanBashPercent, 40, shares + " - a shieldman bashes 43% of his moves in kick reach (he swings 30%)", from, defaultOf);
                MoveOrKeepIfMoved(read, SettingsSchema.AiNoShieldKickPercent, 35, shares + " - a man without a shield kicks 45% of his moves in kick reach", from, defaultOf);
                if (from >= 20) MoveOrKeepIfMoved(read, SettingsSchema.AiNoShieldPushPercent, 20, shares + " - a man without a shield pushes 25% of his moves in kick reach (he swings 30%)", from, defaultOf);
            }
            if (from < 25)
            {
                // step 96 (Toni 2026-10-25: "more active ... but not so lethal, more defencive"): fewer attack decisions and longer pauses after a swing -
                // the four defaults since steps 21 / 43, never moved before; a hand-set value is kept and said once
                const string calmer = "step 96, Toni: more defensive, less lethal";
                MoveOrKeepIfMoved(read, SettingsSchema.AiAggressionFreshPercent, 60, calmer + " - a fresh AI man decides to attack at 45% of the game's rate", from, defaultOf);
                MoveOrKeepIfMoved(read, SettingsSchema.AiAggressionExhaustedPercent, 20, calmer + " - an exhausted one at 15%", from, defaultOf);
                MoveOrKeepIfMoved(read, SettingsSchema.ShieldInfantrySwingsLessPercent, 30, calmer + " - shield infantry swing 40% less", from, defaultOf);
                MoveOrKeepIfMoved(read, SettingsSchema.FootMeleeSwingsLessPercent, 15, calmer + " - other foot melee swing 25% less", from, defaultOf);
            }
            // format 17 (step 78b) moved the lull's LullRecover*Percent 10 / 15 / 20 → 20 / 25 / 30; step 78c removed the lull and its keys, so a
            // format 16 file's are dropped below with the rest of them - nothing to move any more (78c kept format 17; step 80 made it 18)
            // steps 36 / 37: the retired AI sprint keys - in ANY format (they are only ever steps 33-36's, and a hand-pasted one
            // goes the same way)
            DropRetiredSprintKeys(read, from);
            // step 78c: the lull's keys (steps 78 / 78b) - in ANY format (they are only ever formats 16-17's)
            DropRetiredLullKeys(read, from);
            // step 81: the AI kicks' retired keys (steps 65 / 66) - in ANY format (a hand-pasted one goes the same way)
            DropRetiredKickKeys(read, from);
            return read.Migrated;
        }

        /// <summary>Step 33's key - renamed by step 36 to SprintAiChargeUsePercent, which step 37 dropped.</summary>
        public const string OldSprintAiChargeShareKey = "SprintAiChargeSharePercent";

        /// <summary>Step 33's key, dropped by step 36 (split into the advance and move settings).</summary>
        public const string OldSprintAiMoveShareKey = "SprintAiMoveSharePercent";

        /// <summary>Step 36's keys, dropped by step 37 (one number per order: the reserve).</summary>
        public static readonly string[] OldSprintAiUseKeys = { "SprintAiChargeUsePercent", "SprintAiAdvanceUsePercent", "SprintAiMoveUsePercent" };

        /// <summary>
        /// Steps 36 / 37: the retired AI sprint keys leave <see cref="ConfigReadResult.Unknown"/> - one note each - so the rewrite
        /// does not carry them under "Not recognised" forever. Step 33's two shares and step 36's three uses: an AI man now sprints
        /// everything he holds above the order's reserve (SprintAi*KeepPercent), so none of them has a home. Idempotent.
        /// </summary>
        private static void DropRetiredSprintKeys(ConfigReadResult read, int from)
        {
            for (int i = read.Unknown.Count - 1; i >= 0; i--)
            {
                var pair = read.Unknown[i];
                string? why = null;
                if (string.Equals(pair.Key, OldSprintAiChargeShareKey, StringComparison.OrdinalIgnoreCase))
                    why = "step 33's charge share - a charge now sprints everything above SprintAiChargeKeepPercent";
                else if (string.Equals(pair.Key, OldSprintAiMoveShareKey, StringComparison.OrdinalIgnoreCase))
                    why = "step 33's advance-and-move share - now SprintAiAdvanceKeepPercent and SprintAiMoveKeepPercent, the reserves";
                else
                    foreach (var k in OldSprintAiUseKeys)
                        if (string.Equals(pair.Key, k, StringComparison.OrdinalIgnoreCase))
                            why = "step 37, Anton: one number per order - he sprints everything above the order's reserve (its KeepPercent)";
                if (why == null) continue;
                read.Unknown.RemoveAt(i);
                read.Migrated.Add(pair.Key + " " + pair.Value + " dropped (format " + from + " → " + FormatVersion + "; " + why + ")");
            }
        }

        /// <summary>Step 78c: the retired keys of 78 / 78b's unit-wide lull (Toni: "remove that scripted event") - each man rests on his own now
        /// (RestEnabled and the Rest* numbers).</summary>
        public static readonly string[] RetiredLullKeys =
        {
            "LullEnabled", "LullForPlayerUnits", "LullContactMen", "LullMinContactSeconds", "LullStartChargePercent", "LullStartAdvancePercent",
            "LullStartHoldPercent", "LullRecoverChargePercent", "LullRecoverAdvancePercent", "LullRecoverHoldPercent", "LullEaseMetres", "LullFrontDepthMetres",
            "LullMaxSeconds", "LullCooldownSeconds", "LullRotates", "LullClockChargeMinSeconds", "LullClockChargeMaxSeconds", "LullClockAdvanceMinSeconds",
            "LullClockAdvanceMaxSeconds", "LullClockHoldMinSeconds", "LullClockHoldMaxSeconds", "LullClockChargePercent", "LullClockAdvancePercent",
            "LullClockHoldPercent", "LullAttackRateFallenPercent", "LullUnitDepthMetres", "LullChargeGraceSeconds", "LullGapMetres", "LullMaxBackMetres",
        };

        /// <summary>
        /// Step 78c: the lull's retired keys leave <see cref="ConfigReadResult.Unknown"/> - one note each - so the rewrite does not carry them under
        /// "Not recognised" forever (the reader already ignores an unknown key: a file holding them loads as before). No format change: the drop is
        /// idempotent, like the sprint's (steps 36 / 37).
        /// </summary>
        private static void DropRetiredLullKeys(ConfigReadResult read, int from)
        {
            for (int i = read.Unknown.Count - 1; i >= 0; i--)
            {
                var pair = read.Unknown[i];
                bool retired = false;
                foreach (var k in RetiredLullKeys)
                    if (string.Equals(pair.Key, k, StringComparison.OrdinalIgnoreCase))
                        retired = true;
                if (!retired) continue;
                read.Unknown.RemoveAt(i);
                read.Migrated.Add(pair.Key + " " + pair.Value + " dropped (format " + from + " → " + FormatVersion
                                  + "; step 78c, Toni: no scripted lull - each tired man steps back and rests on his own: RestEnabled and the Rest* settings)");
            }
        }

        /// <summary>Step 81: the retired keys of steps 65 / 66's AI kick RATE (Toni: kicks and bashes as a share of the melee moves) - the rate and its
        /// skill / bar / turtle / fever factors, the bar gate, the wait for reach, the bash credit per swing and the bash's own cooldown. The shares
        /// (AiShieldmanKickPercent, AiShieldmanBashPercent, AiNoShieldKickPercent), the swing stack and the cost are the brakes now.</summary>
        public static readonly string[] RetiredKickKeys =
        {
            "AiKicksPerMinute", "AiKickTurtleMultiplier", "AiKickTurtleSeconds", "AiKickTurtleBlocks", "AiKickFeverMultiplier", "AiKickSkillLowPercent",
            "AiKickSkillHighPercent", "AiKickMinBarPercent", "AiKickFullBarPercent", "AiKickWaitSeconds", "AiKickWaitMetres", "AiBashShareOfSwingsPercent",
            "AiBashDelaySeconds", "AiBashCooldownSeconds",
        };

        /// <summary>
        /// Step 81: the AI kick rate's retired keys leave <see cref="ConfigReadResult.Unknown"/> - one note each - so the rewrite does not carry them
        /// under "Not recognised" forever (78c's pattern - <c>DropRetiredLullKeys</c>). Idempotent.
        /// </summary>
        private static void DropRetiredKickKeys(ConfigReadResult read, int from)
        {
            for (int i = read.Unknown.Count - 1; i >= 0; i--)
            {
                var pair = read.Unknown[i];
                bool retired = false;
                foreach (var k in RetiredKickKeys)
                    if (string.Equals(pair.Key, k, StringComparison.OrdinalIgnoreCase))
                        retired = true;
                if (!retired) continue;
                read.Unknown.RemoveAt(i);
                read.Migrated.Add(pair.Key + " " + pair.Value + " dropped (format " + from + " → " + FormatVersion
                                  + "; step 81, Toni: AI kicks and bashes are a share of the melee moves now - AiShieldmanKickPercent, AiShieldmanBashPercent, AiNoShieldKickPercent; the swing stack and the Athletics cost are the brakes)");
            }
        }

        /// <summary>Step 71: <see cref="MoveOrKeep"/> only when today's default is not the old one - a default that never moved (DESIGN's initial
        /// values in the tests, an earlier step's shipped values) says nothing about a hand-set value.</summary>
        private static void MoveOrKeepIfMoved(ConfigReadResult read, ParamDef p, double oldDefault, string why, int from, Func<ParamDef, double> defaultOf)
        {
            double now = defaultOf(p);
            if (double.IsNaN(now) || Math.Abs(now - oldDefault) < 1e-9) return;
            MoveOrKeep(read, p, oldDefault, why, from, defaultOf);
        }

        /// <summary>Step 56: <see cref="MoveOldDefault"/>, else <see cref="KeptTuned"/>.</summary>
        private static void MoveOrKeep(ConfigReadResult read, ParamDef p, double oldDefault, string why, int from, Func<ParamDef, double> defaultOf)
        {
            if (!MoveOldDefault(read, p, oldDefault, why, from, defaultOf))
                KeptTuned(read, p, oldDefault, why, from, defaultOf);
        }

        /// <returns>True when the value was moved.</returns>
        private static bool MoveOldDefault(ConfigReadResult read, ParamDef p, double oldDefault, string why, int from, Func<ParamDef, double> defaultOf)
        {
            if (!read.Values.TryGetValue(p.Key, out double v) || Math.Abs(v - oldDefault) > 1e-9) return false;
            double now = defaultOf(p);
            if (double.IsNaN(now) || Math.Abs(now - oldDefault) < 1e-9) return false; // the default is back where it was: nothing to push
            read.Values[p.Key] = now;
            read.Migrated.Add(p.Key + ": " + p.Format(oldDefault) + " → " + p.Format(now) + " (format " + from + " → " + FormatVersion + ", the old default; " + why + ")");
            return true;
        }

        /// <summary>Step 46: a tuned value the migration leaves alone, logged once (the file is rewritten as the new format right after, so
        /// the note comes once): "CostPerBlow: 5.0 kept - your own value (format 8 → 9; the new default 7.0 …)". Nothing when the key is
        /// missing, holds the old default, or already holds the new one.</summary>
        private static void KeptTuned(ConfigReadResult read, ParamDef p, double oldDefault, string why, int from, Func<ParamDef, double> defaultOf)
        {
            if (!read.Values.TryGetValue(p.Key, out double v) || Math.Abs(v - oldDefault) < 1e-9) return;
            double now = defaultOf(p);
            if (double.IsNaN(now) || Math.Abs(v - now) < 1e-9) return;
            read.Migrated.Add(p.Key + ": " + p.Format(v) + " kept - your own value (format " + from + " → " + FormatVersion + "; the new default " + p.Format(now)
                              + " is not pushed over it; " + why + ")");
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

            if (p.Type == ParamType.Key)
            {
                // step 33: a key by name ("LeftControl", "left ctrl")
                why = "a key name (one of " + p.Keys.ListText + ")";
                if (token.Type != JTokenType.String || !p.Keys.TryIndex(token.Value<string>(), out int keyIndex)) return false;
                value = keyIndex;
                return true;
            }

            if (p.Type == ParamType.Text)
            {
                // step 84: a short text (an item id, "adarga"; "" = none)
                why = TextValues.RuleText;
                if (token.Type != JTokenType.String || !TextValues.TryIndex(token.Value<string>(), out int textIndex)) return false;
                value = textIndex;
                return true;
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

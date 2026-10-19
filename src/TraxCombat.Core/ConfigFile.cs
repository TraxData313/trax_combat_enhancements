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
        /// CostPerWrongSideShieldBlock 2 → 5, CostPerShieldBlock 1 → 2 in a file still holding them; a tuned value kept and noted).</summary>
        public const int FormatVersion = 14;

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
                MoveOrKeep(read, SettingsSchema.AiKicksPerMinute, 0.1, kicks, from, defaultOf);
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
            // steps 36 / 37: the retired AI sprint keys - in ANY format (they are only ever steps 33-36's, and a hand-pasted one
            // goes the same way)
            DropRetiredSprintKeys(read, from);
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
                why = "a key name (one of " + KeyNames.ListText + ")";
                if (token.Type != JTokenType.String || !KeyNames.TryIndex(token.Value<string>(), out int keyIndex)) return false;
                value = keyIndex;
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

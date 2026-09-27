using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// The schema against docs/DESIGN.md's Parameters table - the contract. Any key added,
/// dropped or re-defaulted on one side only fails here, so the doc and the code cannot drift.
/// </summary>
public class SchemaTests
{
    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Could not find " + relative + " above " + AppContext.BaseDirectory);
    }

    /// <summary>Key → default text, from the rows of DESIGN.md's "## Parameters" table.</summary>
    private static Dictionary<string, string> DesignTable()
    {
        var lines = File.ReadAllLines(RepoFile(Path.Combine("docs", "DESIGN.md")));
        var rows = new Dictionary<string, string>(StringComparer.Ordinal);
        bool inSection = false;
        var row = new Regex(@"^\|\s*`(?<key>[A-Za-z0-9]+)`\s*\|\s*(?<def>[^|]+?)\s*\|");
        foreach (var line in lines)
        {
            if (line.StartsWith("## ")) inSection = line.Trim() == "## Parameters";
            if (!inSection) continue;
            var m = row.Match(line);
            if (m.Success) rows.Add(m.Groups["key"].Value, m.Groups["def"].Value);
        }
        Assert.True(rows.Count > 0, "no parameter rows found in DESIGN.md");
        return rows;
    }

    private static double ParseDesignDefault(string text) => text switch
    {
        "true" => 1,
        "false" => 0,
        _ => double.Parse(text, CultureInfo.InvariantCulture),
    };

    [Fact]
    public void Every_design_parameter_is_in_the_schema_with_the_same_default()
    {
        foreach (var (key, defText) in DesignTable())
        {
            Assert.True(SettingsSchema.TryGet(key, out var p), key + " is in DESIGN.md but not in SettingsSchema");
            Assert.Equal(key, p.Key); // exact casing
            Assert.True(Math.Abs(ParseDesignDefault(defText) - p.Default) < 1e-9,
                key + ": DESIGN default " + defText + " vs schema " + p.Format(p.Default));
        }
    }

    [Fact]
    public void Every_schema_parameter_is_in_the_design_table()
    {
        var design = DesignTable();
        foreach (var p in SettingsSchema.All)
            Assert.True(design.ContainsKey(p.Key), p.Key + " is in SettingsSchema but not in DESIGN.md's table");
        Assert.Equal(design.Count, SettingsSchema.All.Count);
    }

    [Fact]
    public void Bool_parameters_match_bool_defaults_in_design()
    {
        foreach (var (key, defText) in DesignTable())
        {
            SettingsSchema.TryGet(key, out var p);
            bool designIsBool = defText is "true" or "false";
            Assert.True(designIsBool == (p.Type == ParamType.Bool), key + " type does not match its DESIGN default " + defText);
        }
    }

    [Fact]
    public void Indexes_are_positions_and_keys_are_unique()
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < SettingsSchema.All.Count; i++)
        {
            Assert.Equal(i, SettingsSchema.All[i].Index);
            Assert.True(keys.Add(SettingsSchema.All[i].Key), "duplicate key " + SettingsSchema.All[i].Key);
        }
        Assert.DoesNotContain(ConfigFile.VersionKey, keys);
    }

    [Fact]
    public void Every_parameter_is_complete_and_in_a_listed_group()
    {
        foreach (var p in SettingsSchema.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Label), p.Key + " has no MCM label");
            Assert.True(p.Description.Length >= 20, p.Key + " needs a real plain-words description");
            Assert.EndsWith(".", p.Description.TrimEnd('"'));
            Assert.Contains(p.Group, SettingsSchema.Groups);
            Assert.InRange(p.Default, p.Min, p.Max);
            // The target for every setting is live (CLAUDE.md hot swap); a NextBattle one must
            // be a deliberate, documented exception.
            Assert.Equal(ApplyTiming.Live, p.Timing);
        }
    }

    [Fact]
    public void Group_titles_are_safe_for_MCM_and_labels_unique_within_a_group()
    {
        foreach (var g in SettingsSchema.Groups)
        {
            Assert.DoesNotContain("/", g.Title); // MCM's sub-group delimiter
            var labels = SettingsSchema.InGroup(g).Select(p => p.Label).ToList();
            Assert.NotEmpty(labels);
            Assert.Equal(labels.Count, labels.Distinct().Count()); // MCM keys properties by label
        }
        Assert.Equal(SettingsSchema.Groups.Count, SettingsSchema.Groups.Select(g => g.Order).Distinct().Count());
    }

    [Fact]
    public void Every_parameter_has_a_typed_property_of_the_same_name_reading_its_slot()
    {
        var settings = new TraxSettings();
        foreach (var p in SettingsSchema.All)
        {
            var prop = typeof(TraxSettings).GetProperty(p.Key, BindingFlags.Public | BindingFlags.Instance);
            Assert.True(prop != null, "TraxSettings has no property " + p.Key);
            var expectedType = p.Type switch
            {
                ParamType.Bool => typeof(bool),
                ParamType.Int => typeof(int),
                _ => typeof(float),
            };
            Assert.Equal(expectedType, prop!.PropertyType);

            // Move the value to a non-default spot and read it back through the property.
            double target = p.Type == ParamType.Bool ? 1 - p.Default : (p.Default == p.Max ? p.Min : p.Max);
            settings.Set(p, target, "test");
            double read = Convert.ToDouble(prop.GetValue(settings), CultureInfo.InvariantCulture);
            Assert.True(Math.Abs(read - target) < 1e-4, p.Key + " property reads the wrong slot");
        }
    }

    [Fact]
    public void Hint_and_default_text_carry_the_default()
    {
        Assert.Contains("Default: 50.", SettingsSchema.DamageRandomPercent.HintText);
        Assert.Equal("default 50, range 0 to 100", SettingsSchema.DamageRandomPercent.DefaultAndRangeText);
        Assert.Equal("default false", SettingsSchema.DamageRandomOnShields.DefaultAndRangeText);
        Assert.Equal("0.75", SettingsSchema.HeroCostMultiplier.Format(0.75));
        Assert.Equal("1.0", SettingsSchema.FormationSpreadStdDevs.Format(1));
    }
}

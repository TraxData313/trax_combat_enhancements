using System.Globalization;
using System.Reflection;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// The schema against docs/DESIGN.md's Parameters table - the contract. Any key added or
/// dropped on one side only, or a type that disagrees, fails here, so the doc and the code
/// cannot drift. DESIGN's Default column is the INITIAL value (DESIGN §2c): the defaults the
/// mod ships come from defaults.json (checked in DefaultsFileTests) and are NOT compared with
/// DESIGN - Anton tunes defaults.json without touching DESIGN. The unit tests themselves run on
/// DESIGN's values (TestDefaults), so a tuned default never breaks them.
/// </summary>
public class SchemaTests
{
    [Fact]
    public void Every_design_parameter_is_in_the_schema_with_the_same_casing()
    {
        foreach (var key in DesignTable.Rows().Keys)
        {
            Assert.True(SettingsSchema.TryGet(key, out var p), key + " is in DESIGN.md but not in SettingsSchema");
            Assert.Equal(key, p.Key); // exact casing
        }
    }

    [Fact]
    public void Every_schema_parameter_is_in_the_design_table()
    {
        var design = DesignTable.Rows();
        foreach (var p in SettingsSchema.All)
            Assert.True(design.ContainsKey(p.Key), p.Key + " is in SettingsSchema but not in DESIGN.md's table");
        Assert.Equal(design.Count, SettingsSchema.All.Count);
    }

    [Fact]
    public void Design_initial_values_have_the_schema_types()
    {
        foreach (var (key, text) in DesignTable.Rows())
        {
            SettingsSchema.TryGet(key, out var p);
            bool isBool = text is "true" or "false";
            bool isWhole = long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
            bool isNumber = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
            switch (p.Type)
            {
                case ParamType.Bool:
                    Assert.True(isBool, key + " is a switch - DESIGN's value must be true or false, not " + text);
                    break;
                case ParamType.Int:
                    Assert.True(isWhole, key + " is a whole number - DESIGN's value must be one, not " + text);
                    break;
                default:
                    Assert.True(isNumber && !isBool, key + " is a number - DESIGN's value must be one, not " + text);
                    break;
            }
        }
    }

    [Fact]
    public void The_unit_tests_run_on_the_design_initial_values()
    {
        Assert.Equal(TestDefaults.SourceName, DefaultsFile.SourceName);
        Assert.True(DefaultsFile.Problems.Count == 0, "DESIGN's Default column: " + string.Join("; ", DefaultsFile.Problems));
        foreach (var (key, text) in DesignTable.Rows())
        {
            SettingsSchema.TryGet(key, out var p);
            var token = DesignTable.ToToken(text);
            double expected = token.Type == Newtonsoft.Json.Linq.JTokenType.Boolean ? ((bool)token ? 1 : 0) : (double)token;
            Assert.True(Math.Abs(expected - p.Default) < 1e-9, key + ": test default " + p.Format(p.Default) + " vs DESIGN " + text);
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

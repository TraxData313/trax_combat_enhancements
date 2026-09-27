using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// defaults.json - the one truth for default values (DESIGN §2c). The repo's file must hold
/// every schema key and nothing else, each value of the right type inside its range, and the
/// // lines exactly as the mod writes them (<see cref="DefaultsFile.RefreshCommand"/> rewrites
/// them and keeps the values); the build must have embedded that very file; and the game's
/// load-time resolution must take every value from it without a problem.
/// </summary>
public class DefaultsFileTests
{
    private static string RepoText() => File.ReadAllText(DesignTable.RepoFile(DefaultsFile.FileName));

    private static string Lf(string s) => s.Replace("\r\n", "\n");

    // ------------------------------------------------------------------ the repo's defaults.json

    [Fact]
    public void Repo_file_has_every_schema_key_and_no_other()
    {
        var c = DefaultsFile.Check(RepoText());
        Assert.True(c.ParseError == null && c.Missing.Count == 0 && c.Unknown.Count == 0, "defaults.json: " + c.Describe());
        Assert.Equal(SettingsSchema.All.Count, c.Values.Count + c.Problems.Count);
    }

    [Fact]
    public void Repo_file_values_have_the_right_type_and_are_inside_their_range()
    {
        var c = DefaultsFile.Check(RepoText());
        Assert.True(c.ParseError == null && c.Problems.Count == 0, "defaults.json: " + c.Describe());
    }

    [Fact]
    public void Repo_file_comments_are_in_step_with_the_schema()
    {
        var c = DefaultsFile.Check(RepoText());
        Assert.True(c.CommentsDifference == null,
            "defaults.json's comments/layout no longer match the schema (a description, range, group or order changed). "
            + "From the repo root run:  " + DefaultsFile.RefreshCommand + "  - it rewrites every comment and keeps every value.\n"
            + c.Describe());
    }

    [Fact]
    public void The_build_embedded_this_very_file()
    {
        Assert.True(Lf(RepoText()) == Lf(DefaultsFile.ReadEmbeddedText()),
            "the defaults.json embedded in TraxCombat.Core.dll differs from the repo's - rebuild (dotnet build -c Release)");
    }

    [Fact]
    public void The_game_resolves_every_default_from_the_embedded_file_without_a_problem()
    {
        var resolved = DefaultsFile.ResolveAll(DefaultsFile.ReadEmbeddedText(), out var problems);
        Assert.True(problems.Count == 0, string.Join("\n", problems));
        var c = DefaultsFile.Check(RepoText());
        foreach (var p in SettingsSchema.All)
            Assert.Equal(c.Values[p.Key], resolved[p.Key]);
    }

    // ------------------------------------------------------------------ write / check

    private static Dictionary<string, double> SomeValues()
    {
        var v = new TraxSettings().Snapshot().ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        v["DamageRandomPercent"] = 35;
        v["HeroCostMultiplier"] = 0.6;
        v["DamageRandomOnShields"] = 1;
        v["FullRegenSecondsStanding"] = 45.25;
        return v;
    }

    [Fact]
    public void Written_text_checks_clean_and_reads_back_every_value()
    {
        var values = SomeValues();
        var c = DefaultsFile.Check(DefaultsFile.Write(values));
        Assert.True(c.Ok, c.Describe());
        foreach (var p in SettingsSchema.All)
            Assert.Equal(values[p.Key], c.Values[p.Key]);
    }

    [Fact]
    public void Written_text_has_the_header_every_group_and_a_range_line_per_key()
    {
        string text = DefaultsFile.Write(SomeValues());
        Assert.StartsWith("// Trax Combat Enhancements - DEFAULTS (defaults.json)", text);
        Assert.Contains(DefaultsFile.RefreshCommand, text);
        Assert.Contains("\"Save current values as a defaults file\"", text);
        int last = -1;
        foreach (var g in SettingsSchema.Groups)
        {
            int at = text.IndexOf("==================== " + g.Title + " ====================", StringComparison.Ordinal);
            Assert.True(at > last, "section " + g.Title + " missing or out of order");
            last = at;
        }
        Assert.Contains("  // (range 0 to 100)\r\n  \"DamageRandomPercent\": 35,", text);
        Assert.Contains("  // (true or false)\r\n  \"DamageRandomOnShields\": true,", text);
        Assert.DoesNotContain("ConfigVersion", text); // a defaults file, not a config file
        Assert.DoesNotContain("  \r\n", text);
    }

    [Fact]
    public void Number_formatting_is_free_values_are_not_compared_as_text()
    {
        string text = DefaultsFile.Write(SomeValues())
            .Replace("\"FullRegenSecondsStanding\": 45.25", "\"FullRegenSecondsStanding\":45.250")
            .Replace("\"HeroCostMultiplier\": 0.6", "\"HeroCostMultiplier\": 6e-1");
        var c = DefaultsFile.Check(text);
        Assert.True(c.ValuesOk, c.Describe());
        Assert.Equal(45.25, c.Values["FullRegenSecondsStanding"]);
        Assert.Equal(0.6, c.Values["HeroCostMultiplier"], 9);
    }

    [Fact]
    public void Check_names_a_missing_key()
    {
        string text = DefaultsFile.Write(SomeValues()).Replace("  \"DamageRandomMelee\": true,\r\n", string.Empty);
        var c = DefaultsFile.Check(text);
        Assert.Equal(new[] { "DamageRandomMelee" }, c.Missing);
        Assert.NotNull(c.CommentsDifference);
        Assert.Contains("missing: \"DamageRandomMelee\"", c.Describe());
    }

    [Fact]
    public void Check_names_an_unknown_key_and_a_wrong_casing()
    {
        string text = DefaultsFile.Write(SomeValues())
            .Replace("\"DamageRandomPercent\": 35", "\"damageRandomPercent\": 35")
            .Replace("{\r\n", "{\r\n  \"NoSuchSetting\": 1,\r\n");
        var c = DefaultsFile.Check(text);
        Assert.Contains("NoSuchSetting", c.Unknown);
        Assert.Contains(c.Unknown, u => u.StartsWith("damageRandomPercent (keys are case-sensitive here: DamageRandomPercent)"));
        Assert.Contains("DamageRandomPercent", c.Missing);
        Assert.False(c.Ok);
    }

    [Theory]
    [InlineData("\"DamageRandomOnShields\": true", "\"DamageRandomOnShields\": 1", "DamageRandomOnShields: 1 is not true or false")]
    [InlineData("\"DamageRandomPercent\": 35", "\"DamageRandomPercent\": 35.5", "DamageRandomPercent: 35.5 is not a whole number")]
    [InlineData("\"DamageRandomPercent\": 35", "\"DamageRandomPercent\": \"35\"", "DamageRandomPercent: \"35\" is not a whole number")]
    [InlineData("\"HeroCostMultiplier\": 0.6", "\"HeroCostMultiplier\": \"0.6\"", "HeroCostMultiplier: \"0.6\" is not a number")]
    [InlineData("\"DamageRandomPercent\": 35", "\"DamageRandomPercent\": 500", "DamageRandomPercent: 500 is outside its range 0 to 100")]
    [InlineData("\"HeroCostMultiplier\": 0.6", "\"HeroCostMultiplier\": 0.123456", "HeroCostMultiplier: 0.123456 has more than 4 decimals")]
    public void Check_names_a_bad_value(string good, string bad, string expected)
    {
        var c = DefaultsFile.Check(DefaultsFile.Write(SomeValues()).Replace(good, bad));
        Assert.False(c.ValuesOk);
        Assert.Contains(c.Problems, p => p.StartsWith(expected, StringComparison.Ordinal));
        Assert.Null(c.CommentsDifference); // the layout is fine - only the value is wrong
    }

    [Fact]
    public void Check_refuses_a_duplicate_key_and_text_that_is_not_an_object()
    {
        string text = DefaultsFile.Write(SomeValues()).Replace("{\r\n", "{\r\n  \"DamageRandomPercent\": 20,\r\n");
        Assert.Contains("DamageRandomPercent", DefaultsFile.Check(text).ParseError);
        Assert.NotNull(DefaultsFile.Check("[1, 2]").ParseError);
        Assert.NotNull(DefaultsFile.Check("{ nope").ParseError);
    }

    [Fact]
    public void Check_points_at_the_first_comment_that_drifted_and_names_the_fix()
    {
        string text = DefaultsFile.Write(SomeValues()).Replace("// Randomize hits by arrows", "// Randomise hits by arrows");
        var c = DefaultsFile.Check(text);
        Assert.True(c.ValuesOk, c.Describe());
        Assert.NotNull(c.CommentsDifference);
        Assert.Contains("Randomize hits by arrows", c.CommentsDifference);
        Assert.Contains(DefaultsFile.RefreshCommand, c.Describe());
    }

    // ------------------------------------------------------------------ the load-time fallbacks

    [Fact]
    public void A_bad_embedded_file_never_throws_and_every_problem_is_named()
    {
        string text = DefaultsFile.Write(SomeValues())
            .Replace("  \"DamageRandomEnabled\": true,\r\n", string.Empty)
            .Replace("\"DamageRandomPercent\": 35", "\"DamageRandomPercent\": 500")
            .Replace("\"HeroCostMultiplier\": 0.6", "\"HeroCostMultiplier\": \"x\"")
            .Replace("{\r\n", "{\r\n  \"Typo\": 3,\r\n");
        var values = DefaultsFile.ResolveAll(text, out var problems);
        Assert.Equal(0, values["DamageRandomEnabled"]);           // missing switch → off
        Assert.Equal(100, values["DamageRandomPercent"]);         // clamped into its range
        Assert.Equal(SettingsSchema.HeroCostMultiplier.Min, values["HeroCostMultiplier"]); // unusable → bottom of the range
        Assert.Contains(problems, p => p.StartsWith("\"DamageRandomEnabled\" is missing from the text - using false"));
        Assert.Contains(problems, p => p.StartsWith("DamageRandomPercent: 500 is outside its range 0 to 100 - using 100"));
        Assert.Contains(problems, p => p.StartsWith("HeroCostMultiplier: \"x\" is not a number (like 0.75) - using 0.0"));
        Assert.Contains(problems, p => p.StartsWith("\"Typo\" in the text is not a setting of this version"));
        Assert.Equal(1, values["DamageRandomOnShields"]);         // the good ones still read
    }

    [Fact]
    public void An_unreadable_embedded_file_falls_back_for_every_setting()
    {
        var values = DefaultsFile.ResolveAll("{ nope", out var problems);
        Assert.Contains(problems, p => p.Contains("did not parse"));
        foreach (var p in SettingsSchema.All)
            Assert.Equal(p.Type == ParamType.Bool ? 0 : p.Min, values[p.Key]);
        DefaultsFile.ResolveAll(string.Empty, out var empty);
        Assert.Contains(empty, p => p.Contains("empty or missing"));
    }
}

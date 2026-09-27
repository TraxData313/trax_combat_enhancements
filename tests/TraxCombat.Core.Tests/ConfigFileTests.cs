using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

public class ConfigFileTests
{
    private static string DefaultsText() => ConfigFile.Write(new TraxSettings().Snapshot());

    [Fact]
    public void Defaults_round_trip_exactly_with_no_issues()
    {
        var read = ConfigFile.Read(DefaultsText());
        Assert.True(read.Ok, read.Error);
        Assert.Empty(read.Issues);
        Assert.Empty(read.Missing);
        Assert.Empty(read.Unknown);
        Assert.Equal(ConfigFile.FormatVersion, read.FileVersion);
        foreach (var p in SettingsSchema.All)
            Assert.Equal(p.Default, read.Values[p.Key]);
    }

    [Fact]
    public void A_format_1_file_carrying_the_old_step_13_defaults_gets_the_new_ones_once()
    {
        // Anton's config.json from before step 13: format 1, the AI decisions on, the bar at 54
        string old = "{ \"ConfigVersion\": 1, \"AttackRateAiDecisions\": true, \"PlayerBarOffsetBottom\": 54, \"DamageRandomPercent\": 40 }";
        var read = ConfigFile.Read(old);
        Assert.Equal(1, read.FileVersion);
        var notes = ConfigFile.Migrate(read);
        Assert.Equal(2, notes.Count);
        Assert.Equal(0, read.Values["AttackRateAiDecisions"]);
        Assert.Equal(SettingsSchema.PlayerBarOffsetBottom.Default, read.Values["PlayerBarOffsetBottom"]);
        Assert.Equal(40, read.Values["DamageRandomPercent"]);   // anything else is left alone
        Assert.Contains(notes, n => n.StartsWith("AttackRateAiDecisions: true → false (format 1 → 2, the old default; step 13:", StringComparison.Ordinal));
        Assert.Contains(notes, n => n.StartsWith("PlayerBarOffsetBottom: 54 → 30 (format 1 → 2, the old default;", StringComparison.Ordinal));
        Assert.Equal(2, ConfigFile.Migrate(read).Count);         // idempotent: nothing new the second time

        // a value the player chose himself (not the old default) stays
        var own = ConfigFile.Read("{ \"ConfigVersion\": 1, \"AttackRateAiDecisions\": false, \"PlayerBarOffsetBottom\": 70 }");
        Assert.Empty(ConfigFile.Migrate(own));
        Assert.Equal(70, own.Values["PlayerBarOffsetBottom"]);

        // a format-2 file (written by this version) and a hand-made file with no stamp are never migrated
        var now = ConfigFile.Read("{ \"ConfigVersion\": 2, \"AttackRateAiDecisions\": true, \"PlayerBarOffsetBottom\": 54 }");
        Assert.Empty(ConfigFile.Migrate(now));
        Assert.Equal(1, now.Values["AttackRateAiDecisions"]);
        var hand = ConfigFile.Read("{ \"AttackRateAiDecisions\": true }");
        Assert.Empty(ConfigFile.Migrate(hand));

        // a file that did not parse: nothing
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ nope")));
        Assert.Equal(2, ConfigFile.FormatVersion);
    }

    [Fact]
    public void Changed_values_round_trip()
    {
        var s = new TraxSettings();
        s.Set("DamageRandomPercent", 35, "test");
        s.Set("HeroCostMultiplier", 0.5, "test");
        s.Set("ShowFormationSpread", 0, "test");
        s.Set("HudRefreshSeconds", 0.05, "test");

        var read = ConfigFile.Read(ConfigFile.Write(s.Snapshot()));

        var fresh = new TraxSettings();
        ConfigFile.Apply(read, fresh);
        foreach (var p in SettingsSchema.All)
            Assert.Equal(s.Get(p), fresh.Get(p));
    }

    [Fact]
    public void Every_key_has_a_plain_words_comment_directly_above_it()
    {
        var lines = DefaultsText().Split("\r\n");
        foreach (var p in SettingsSchema.All)
        {
            int at = Array.FindIndex(lines, l => l.TrimStart().StartsWith("\"" + p.Key + "\":"));
            Assert.True(at > 0, p.Key + " not written");
            Assert.StartsWith("//", lines[at - 1].TrimStart());
            // The comment block above the key carries the description's opening words and the default.
            int top = at - 1;
            while (top > 0 && lines[top - 1].TrimStart().StartsWith("//") && !lines[top - 1].Contains("====")) top--;
            string comment = string.Join(" ", lines[top..at].Select(l => l.Trim().TrimStart('/').Trim()));
            Assert.Contains(string.Join(" ", p.Description.Split(' ').Take(4)), comment);
            Assert.Contains("default " + p.Format(p.Default), comment);
        }
    }

    [Fact]
    public void Sections_follow_the_groups_and_the_header_explains_the_file()
    {
        string text = DefaultsText();
        int last = -1;
        foreach (var g in SettingsSchema.Groups)
        {
            int at = text.IndexOf("==================== " + g.Title + " ====================", StringComparison.Ordinal);
            Assert.True(at > last, "section " + g.Title + " missing or out of order");
            last = at;
        }
        Assert.Contains(@"Configs\TraxCombatEnhancements\config.json", text);
        Assert.Contains("trax_combat.log", text);
        Assert.Contains("Mod Configuration Menu (MCM)", text);
        Assert.Contains("next battle start", text);
        // How to get the defaults back, with and without MCM (DESIGN §2c, §4).
        Assert.Contains("delete its line", text);
        Assert.Contains("delete this file", text);
        Assert.Contains("\"Revert all to defaults\"", text);
        Assert.DoesNotContain("  \r\n", text); // no trailing spaces
    }

    [Fact]
    public void Comments_trailing_commas_and_key_casing_are_tolerated()
    {
        const string text =
            "// a comment\r\n"
            + "{\r\n"
            + "  /* block */ \"damagerandompercent\": 30, // trailing comment\r\n"
            + "  \"SHOWPLAYERBAR\": false,\r\n"
            + "}\r\n";
        var read = ConfigFile.Read(text);
        Assert.True(read.Ok, read.Error);
        Assert.Equal(30, read.Values["DamageRandomPercent"]);
        Assert.Equal(0, read.Values["ShowPlayerBar"]);
    }

    [Fact]
    public void Missing_keys_are_listed_and_apply_their_default()
    {
        var read = ConfigFile.Read("{ \"DamageRandomPercent\": 30 }");
        Assert.True(read.Ok);
        Assert.Equal(SettingsSchema.All.Count - 1, read.Missing.Count);
        Assert.Null(read.FileVersion);

        var s = new TraxSettings();
        s.Set("AthleticsPoolFloor", 200, "test"); // not in the file → goes back to the default
        ConfigFile.Apply(read, s);
        Assert.Equal(30, s.DamageRandomPercent);
        Assert.Equal(50, s.AthleticsPoolFloor);
    }

    [Fact]
    public void Unknown_keys_are_ignored_and_reported()
    {
        var read = ConfigFile.Read("{ \"DamageRandomPercnt\": 30, \"Extra\": { \"a\": [1, 2] } }");
        Assert.True(read.Ok);
        Assert.Equal(2, read.Unknown.Count);
        Assert.Equal("DamageRandomPercnt", read.Unknown[0].Key);
        Assert.Equal("30", read.Unknown[0].Value);
        Assert.Equal("{\"a\":[1,2]}", read.Unknown[1].Value);
        Assert.DoesNotContain("DamageRandomPercent", read.Values.Keys);

        var s = new TraxSettings();
        ConfigFile.Apply(read, s);
        Assert.Equal(50, s.DamageRandomPercent); // the typo did nothing
    }

    [Fact]
    public void Unknown_keys_survive_a_rewrite()
    {
        var first = ConfigFile.Read("{ \"DamageRandomPercnt\": 30 }");
        string rewritten = ConfigFile.Write(new TraxSettings().Snapshot(), first.Unknown);
        Assert.Contains("Not recognised", rewritten);
        var again = ConfigFile.Read(rewritten);
        Assert.True(again.Ok, again.Error);
        Assert.Single(again.Unknown);
        Assert.Equal("DamageRandomPercnt", again.Unknown[0].Key);
        Assert.Empty(again.Missing);
    }

    [Fact]
    public void Invalid_values_count_as_missing_with_an_issue()
    {
        var read = ConfigFile.Read("{ \"DamageRandomPercent\": \"lots\", \"ShowPlayerBar\": 3, \"CostPerBlow\": [1] }");
        Assert.True(read.Ok);
        Assert.True(read.HasInvalid);
        Assert.Equal(3, read.Issues.Count(i => i.Kind == ConfigIssueKind.Invalid));
        Assert.Contains(SettingsSchema.DamageRandomPercent, read.Missing);
        Assert.Contains(SettingsSchema.ShowPlayerBar, read.Missing);
        Assert.Contains(SettingsSchema.CostPerBlow, read.Missing);
    }

    [Fact]
    public void Out_of_range_is_clamped_and_fractions_of_whole_numbers_rounded()
    {
        var read = ConfigFile.Read("{ \"DamageRandomPercent\": 500, \"AthleticsPoolFloor\": 49.6 }");
        Assert.Equal(100, read.Values["DamageRandomPercent"]);
        Assert.Equal(50, read.Values["AthleticsPoolFloor"]);
        Assert.Contains(read.Issues, i => i.Key == "DamageRandomPercent" && i.Kind == ConfigIssueKind.Clamped);
        Assert.Contains(read.Issues, i => i.Key == "AthleticsPoolFloor" && i.Kind == ConfigIssueKind.Rounded);
    }

    [Fact]
    public void Strings_holding_values_are_accepted_including_a_decimal_comma()
    {
        var read = ConfigFile.Read("{ \"HeroCostMultiplier\": \"0,5\", \"CostOnMiss\": \"off\", \"AthleticsPoolFloor\": \"150\" }");
        Assert.Empty(read.Issues);
        Assert.Equal(0.5, read.Values["HeroCostMultiplier"]);
        Assert.Equal(0, read.Values["CostOnMiss"]);
        Assert.Equal(150, read.Values["AthleticsPoolFloor"]);
    }

    [Fact]
    public void Broken_json_is_an_error_with_a_line_number_and_applies_nothing()
    {
        // The classic hand-edit slip: a decimal comma in a bare number.
        var read = ConfigFile.Read("{\r\n  \"HeroCostMultiplier\": 0,75\r\n}");
        Assert.False(read.Ok);
        Assert.Contains("line", read.Error);
        Assert.Empty(read.Values);

        var s = new TraxSettings();
        s.Set("DamageRandomPercent", 30, "test");
        Assert.Equal(0, ConfigFile.Apply(read, s));
        Assert.Equal(30, s.DamageRandomPercent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[1, 2]")]
    [InlineData("true")]
    public void Not_an_object_is_an_error(string text)
    {
        Assert.False(ConfigFile.Read(text).Ok);
    }

    [Fact]
    public void Wrap_never_splits_words_and_respects_the_width()
    {
        var lines = ConfigFile.Wrap(SettingsSchema.VerboseLogging.Description, 40).ToList();
        Assert.True(lines.Count > 1);
        Assert.All(lines, l => Assert.True(l.Length <= 40, l));
        Assert.Equal(SettingsSchema.VerboseLogging.Description, string.Join(" ", lines));
    }
}

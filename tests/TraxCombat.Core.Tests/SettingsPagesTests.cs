using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 59 (Anton, 2026-10-18: "reorganise the MCM menu ... leave a few options for me and the players"): TWO MCM pages - the
/// short PLAYER page ("Trax Combat Enhancements") and the ADVANCED page ("Trax Combat Enhancements - Advanced"). Each setting on
/// exactly one page, no key lost, the player page's exact list and order, the groups' order (the player page's first, Advanced
/// last), plain words on the player page, and config.json / defaults.json written player page first under a banner per page.
/// </summary>
public class SettingsPagesTests
{
    /// <summary>The player page, in order - (group, key, label). A change here is a deliberate change of the page.</summary>
    private static readonly (string Group, string Key, string Label)[] PlayerPage =
    {
        ("Master switch", "ModEnabled", "Mod enabled"),
        ("How hard hits land", "DamageRandomPercent", "Damage roll (± %)"),
        ("How hard hits land", "DamageReductionPercent", "Damage reduction (%)"),
        ("Tiredness (Athletics)", "AthleticsEnabled", "Athletics (stamina)"),
        ("Tiredness (Athletics)", "CostPerBlow", "Cost per swing (points)"),
        ("Tiredness (Athletics)", "HeroCostMultiplier", "Heroes' cost (x)"),
        ("How the AI fights", "AiAggressionFreshPercent", "AI aggression when fresh (%)"),
        ("How the AI fights", "AiAggressionExhaustedPercent", "AI aggression when exhausted (%)"),
        ("How the AI fights", "FeverEnabled", "Fever on a charge or advance order"),
        ("How the AI fights", "ShieldWallHoldsSlots", "Shield walls hold their slots"),
        ("How the AI fights", "ShieldWallStepOutMetres", "Shield walls: a step out to strike (m)"),
        ("How the AI fights", "CourageEnabled", "Courage and cohesion"),
        ("How the AI fights", "CavalryFlockEnabled", "Cavalry flocking"),
        ("Staggers and knockdowns", "ImpactEnabled", "Staggers, shield drops, knockdowns"),
        ("Staggers and knockdowns", "ImpactOnPlayerPercent", "Against you (%)"),
        ("Sprinting", "SprintEnabled", "Sprint"),
        ("Sprinting", "SprintKey", "Sprint key"),
        ("On screen", "ShowPlayerBar", "Your Athletics bar"),
        ("On screen", "AltMarksEveryone", "Hold ALT: bars on every man"),
        ("On screen", "ImpactShowMessages", "On-screen lines: knocked down, shield lost"),
    };

    [Fact]
    public void Every_setting_is_on_exactly_one_page_and_no_key_is_lost()
    {
        var player = SettingsSchema.OnPage(SettingsPage.Player);
        var advanced = SettingsSchema.OnPage(SettingsPage.Advanced);
        Assert.Equal(275, SettingsSchema.All.Count);
        Assert.Equal(SettingsSchema.All.Count, player.Count + advanced.Count);
        Assert.Empty(player.Intersect(advanced));
        Assert.Equal(SettingsSchema.All.OrderBy(p => p.Key), player.Concat(advanced).OrderBy(p => p.Key));
        foreach (var p in SettingsSchema.All)
            Assert.Equal(p.Group.Page == SettingsPage.Player, player.Contains(p));
        // The file / MCM order IS the schema's: the player page's settings are the first ones.
        Assert.Equal(SettingsSchema.All.Take(player.Count), player);
        Assert.Equal(SettingsSchema.All.Skip(player.Count), advanced);
    }

    [Fact]
    public void The_player_page_is_short_and_exactly_this_list_in_this_order()
    {
        var page = SettingsSchema.PlayerPage;
        Assert.InRange(page.Count, 12, 20);                                      // Anton: "a few options" - not a wall
        Assert.Equal(PlayerPage.Select(r => r.Key), page.Select(p => p.Key));
        Assert.Equal(PlayerPage.Select(r => r.Label), page.Select(p => p.Label));
        Assert.Equal(PlayerPage.Select(r => r.Group), page.Select(p => p.Group.Title));
        Assert.Same(SettingsSchema.ModEnabled, page[0]);                         // the master switch first, still
        Assert.Same(SettingsSchema.ModEnabled, SettingsSchema.All[0]);
    }

    [Fact]
    public void The_groups_run_player_page_first_then_advanced_with_Advanced_last()
    {
        var groups = SettingsSchema.Groups;
        Assert.Equal(Enumerable.Range(0, groups.Count), groups.Select(g => g.Order));   // the order = the list's, no gaps
        var players = SettingsSchema.GroupsOn(SettingsPage.Player);
        var advanced = SettingsSchema.GroupsOn(SettingsPage.Advanced);
        Assert.Equal(7, players.Count);
        Assert.Equal(16, advanced.Count);                                         // every group of before step 59 but the master switch
        Assert.Equal(groups.Take(players.Count), players);
        Assert.Equal(groups.Skip(players.Count), advanced);
        Assert.Same(SettingsSchema.MasterGroup, groups[0]);
        Assert.Same(SettingsSchema.DamageGroup, advanced[0]);
        Assert.Same(SettingsSchema.AdvancedGroup, advanced[advanced.Count - 1]); // the logging and test tools (the spike) last
        Assert.Contains(SettingsSchema.ImpactSpike, SettingsSchema.InGroup(SettingsSchema.AdvancedGroup));
        Assert.Contains(SettingsSchema.VerboseLogging, SettingsSchema.InGroup(SettingsSchema.AdvancedGroup));
        // One file, so every section heading must be unique - a player group never shares an Advanced group's title.
        Assert.Equal(groups.Count, groups.Select(g => g.Title).Distinct().Count());
        foreach (var g in groups)
            Assert.NotEmpty(SettingsSchema.InGroup(g));
    }

    [Fact]
    public void The_player_page_speaks_plainly()
    {
        // Internal words never reach a player: no "f", seams, levers, the spike, shadow mode as a setting, step numbers.
        string[] internalWords = { " f ", "seam", "lever", "spike", "Step ", "step 5", "x (1", "T ×" };
        foreach (var p in SettingsSchema.PlayerPage)
        {
            Assert.DoesNotContain("Shadow", p.Label);
            foreach (var w in internalWords)
            {
                Assert.DoesNotContain(w, p.Label);
                Assert.DoesNotContain(w, p.Description);
            }
            // A hint that points at a setting not on this page names where it is.
            Assert.DoesNotContain("below)", p.Description);
            Assert.DoesNotContain("the next two settings", p.Description);
            Assert.DoesNotContain("the points per second below", p.Description);
        }
        Assert.Equal("Trax Combat Enhancements", SettingsSchema.PageTitle(SettingsPage.Player));
        Assert.Equal("Trax Combat Enhancements - Advanced", SettingsSchema.PageTitle(SettingsPage.Advanced));
    }

    [Fact]
    public void The_files_put_the_player_page_first_under_a_banner_per_page()
    {
        var values = SettingsSchema.All.ToDictionary(p => p.Key, p => p.Default);
        foreach (var text in new[] { ConfigFile.Write(values), DefaultsFile.Write(values) })
        {
            int playerBanner = text.IndexOf("######## " + ConfigFile.PageBanner(SettingsPage.Player) + " ########", StringComparison.Ordinal);
            int advancedBanner = text.IndexOf("######## " + ConfigFile.PageBanner(SettingsPage.Advanced) + " ########", StringComparison.Ordinal);
            Assert.True(playerBanner > 0, "no player-page banner");
            Assert.True(advancedBanner > playerBanner, "the Advanced banner is missing or above the player page's");
            foreach (var p in SettingsSchema.All)
            {
                int at = text.IndexOf("\"" + p.Key + "\":", StringComparison.Ordinal);
                Assert.True(at > 0, p.Key + " missing from the file");
                if (p.Group.Page == SettingsPage.Player) Assert.InRange(at, playerBanner, advancedBanner);
                else Assert.True(at > advancedBanner, p.Key + " is above the Advanced banner");
            }
            Assert.Contains("Trax Combat Enhancements - Advanced", text);
        }
        // The config file still reads back every key and value - only the order and the banners changed (no migration).
        var read = ConfigFile.Read(ConfigFile.Write(values));
        foreach (var p in SettingsSchema.All)
            Assert.Equal(p.Default, read.Values[p.Key], 9);
        string header = ConfigFile.Write(values);
        Assert.Contains("MCM's page \"Trax Combat Enhancements\"", header);
        Assert.Contains("\"Trax Combat Enhancements - Advanced\"", header);
        Assert.Contains("\"Revert all to defaults\"", header);
    }
}

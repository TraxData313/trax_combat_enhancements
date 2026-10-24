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
        ("How hard hits land", "TiredDamageAtEmptyPercent", "Tired men's damage at an empty bar (%)"), // step 89 - one number that changes how the game feels (DESIGN decision 82)
        ("Tiredness (Athletics)", "AthleticsEnabled", "Athletics (stamina)"),
        ("Tiredness (Athletics)", "CostPerBlow", "Cost per swing (points)"),
        ("Tiredness (Athletics)", "HeroCostMultiplier", "Heroes' cost (x)"),
        ("How the AI fights", "AiAggressionFreshPercent", "AI aggression when fresh (%)"),
        ("How the AI fights", "AiAggressionExhaustedPercent", "AI aggression when exhausted (%)"),
        ("How the AI fights", "FeverEnabled", "Fever on a charge or advance order"),
        ("How the AI fights", "ShieldWallHoldsSlots", "Shield walls hold their slots"),
        ("How the AI fights", "ShieldWallStepOutMetres", "Shield walls: a step out to strike (m)"),
        ("How the AI fights", "ShieldWallStandFirm", "Shield walls stand firm on a hold order"), // step 75 - Toni's own toggle to test on and off (DESIGN decision 69)
        ("How the AI fights", "SpearPreferenceEnabled", "Spearmen keep their spears"), // step 76 - a new AI system's switch (DESIGN decision 70)
        ("How the AI fights", "PressDontPass", "Press, don't pass"), // step 77 - a new AI system's switch Toni tests on and off (DESIGN decision 71)
        ("How the AI fights", "RotationEnabled", "Tired front men swap with fresh ones"), // step 78 - a new AI system's switch (DESIGN decision 72)
        ("How the AI fights", "RestEnabled", "Tired men step back and rest"), // step 78c - it replaced step 78's two lull switches (DESIGN decision 74)
        ("How the AI fights", "WarCriesEnabled", "War cries"), // step 79 - a new system's switch (sound only; DESIGN decision 75)
        ("How the AI fights", "CourageEnabled", "Courage and cohesion"),
        ("How the AI fights", "CavalryFlockEnabled", "Cavalry flocking"),
        ("How the AI fights", "LineDisciplineForPlayer", "Line discipline: your units too"), // step 73 - Toni's own toggle (DESIGN decision 67 f)
        ("Staggers and knockdowns", "ImpactEnabled", "Staggers, shield drops, knockdowns"),
        ("Staggers and knockdowns", "ImpactOnPlayerPercent", "Against you (%)"),
        ("Staggers and knockdowns", "ImpactMeetRulesEnabled", "Meeting kicks and bashes"), // step 82 - a new rule's switch (DESIGN decision 78)
        ("Sprinting", "SprintEnabled", "Sprint"),
        ("Sprinting", "SprintKey", "Sprint key"),
        ("Sprinting", "SprintControllerKey", "Sprint button (controller)"), // step 85 - Toni's gamepad sprint (the left stick press), beside the key it mirrors
        ("On screen", "ShowPlayerBar", "Your Athletics bar"),
        ("On screen", "AltMarksEveryone", "Hold ALT: bars on every man"),
        ("On screen", "ImpactShowMessages", "On-screen lines: knocked down, shield lost"),
        ("Unit changes", "GiveTier1Shields", "Give Tier 1 units shields"), // step 84 - a new system's switch, its own section (DESIGN decision 80)
    };

    [Fact]
    public void Every_setting_is_on_exactly_one_page_and_no_key_is_lost()
    {
        var player = SettingsSchema.OnPage(SettingsPage.Player);
        var advanced = SettingsSchema.OnPage(SettingsPage.Advanced);
        Assert.Equal(514, SettingsSchema.All.Count); // step 89: + 1 (TiredDamageAtEmptyPercent on the player page, after DamageReductionPercent in "How hard hits land"); step 88: + 1 (AiNoShieldPushPercent, Advanced in the Impact group, after AiNoShieldKickPercent); step 86: + 1 (SprintShowKeyHint, Advanced "Your Athletics bar", after ShowSprintBar); step 85: + 1 (SprintControllerKey on the player page, after SprintKey in "Sprinting"); step 84: + 11 (GiveTier1Shields on the player page in its own "Unit changes", 6 UnitShield* + 4 UnitSpear* Advanced in "Unit changes"); step 83: + 1 (LineMinWidthToDepthRatio, Advanced "Line discipline by tier"); step 82: + 20 (the meet rules - ImpactMeetRulesEnabled on the player page, 16 ceilings and 3 for the AI's read Advanced in the Impact group); step 81: - 14 (the AI kick rate, its factors, the bar gate, the wait, the bash credit and cooldown) + 3 (the shares - AiShieldmanKickPercent, AiShieldmanBashPercent, AiNoShieldKickPercent, Advanced in the Impact group); step 80: + 2 (the strike path - AiBashPathHalfWidthMetres, AiKickRearRankMetres, Advanced in the Impact group); step 79: + 11 (war cries - WarCriesEnabled on the player page, 10 WarCry* Advanced in "Courage and cohesion (AI)"); step 78c: - 29 (the lull's: 2 on the player page, 27 Advanced) + 16 (each man's rest - RestEnabled on the player page, 15 Advanced in "Courage and cohesion (AI)"); step 78b: + 16 (the breather clock and the gap - 14 Lull*, the wrap's switch and the wing's bend, all Advanced in "Courage and cohesion (AI)"); step 78: + 27 (the battle's rhythm - 3 on the player page, 24 Advanced in "Courage and cohesion (AI)"); step 77: + 16 (press, don't pass - 1 on the player page, 15 Advanced in "Courage and cohesion (AI)"); step 76: + 21 (the spear preference - 1 on the player page, 17 Advanced - and thrusts past a friend, 3 Advanced); step 75: + 6 (a standing wall stands firm - 1 on the player page, 5 Advanced); step 74: + 11 (the block hold, Advanced "Line discipline by tier"); step 73: + 15 (line discipline by tier - 1 on the player page); step 72: + 3 (no accidental charge orders); step 70: + 9 (the band, the strike) + 2 (the lone runner); step 69: + 6 (winning calms, the threat line, the mop-up); step 68: + 4 (the wall advances as one, the walk back after a kick) + 1 (no kick through a friend); step 67: + 25 (the fear flock) + 4 (the morale eyes); step 66: + 5 (the wait for reach, the bashes per swing); step 65: + 14 (the AI kicks) + 7 (the brace); step 64: + 2 (the shield front arc) + 3 (the charge sprint into contact); step 63: + 2 (the rear / flank pressure) + 1 (the wall's walk back); step 62: + 2 (the lost falls) + 2 (toughness, a ranged man in melee) + 4 (direction) + 4 (the charge) + 1 (the critical) + 1 (the cap)
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
        Assert.InRange(page.Count, 12, 31);                                      // Anton: "a few options" - not a wall (step 73: 20 → 21 on purpose, the line discipline's switch for your units; step 75: 22, the stand-firm switch Toni tests on and off; step 76: 23, the spear preference - a new AI system's switch; step 77: 24, press, don't pass; step 78: 27, the rotation, the lull and your units in it; step 78c: 26, the lull's two out, the rest's switch in; step 79: 27, war cries; step 82: 28, meeting kicks and bashes; step 84: 29, the tier-1 shields; step 85: 30, the controller sprint button beside the sprint key; step 89: 31, tired men's damage)
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
        Assert.Equal(8, players.Count);                                           // step 84: + "Unit changes"
        Assert.Equal(19, advanced.Count);                                         // step 84: + "Unit changes"                                         // every group of before step 59 but the master switch, + step 72's orders group, + step 73's line discipline
        Assert.Equal(groups.Take(players.Count), players);
        Assert.Equal(groups.Skip(players.Count), advanced);
        Assert.Same(SettingsSchema.MasterGroup, groups[0]);
        Assert.Same(SettingsSchema.DamageGroup, advanced[0]);
        Assert.Same(SettingsSchema.AdvancedGroup, advanced[advanced.Count - 1]); // the logging and test tools (the spike) last
        Assert.Contains(SettingsSchema.ImpactSpike, SettingsSchema.InGroup(SettingsSchema.AdvancedGroup));
        Assert.Contains(SettingsSchema.VerboseLogging, SettingsSchema.InGroup(SettingsSchema.AdvancedGroup));
        // Unique titles on each page (MCM keys a page's groups by title); step 84's "Unit changes" is on both pages (Toni: the switch on the
        // player page, the items on Advanced) - the file's sections sit under a banner per page, so the two headings never mix.
        Assert.Equal(players.Count, players.Select(g => g.Title).Distinct().Count());
        Assert.Equal(advanced.Count, advanced.Select(g => g.Title).Distinct().Count());
        Assert.Equal(SettingsSchema.PlayerUnitsGroup.Title, SettingsSchema.UnitsGroup.Title);
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

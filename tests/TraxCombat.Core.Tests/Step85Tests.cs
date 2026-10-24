using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 85 - SPRINT ON A CONTROLLER BUTTON (Toni 2026-10-24: "when pressing left thumbstick using controller it acts like pressing the CTRL
/// activating the sprint"; AI_NOTES "Step 85"): the setting (placed right after SprintKey, the controller list, ControllerLThumb by default,
/// None), its names (loose and short forms, the keyboard's list untouched), the file / defaults.json / formatting on ITS list, either key
/// sprinting (Core SprintKeys), the sentence with both keys, the master switch first.
/// </summary>
public class Step85Tests
{
    [Fact]
    public void The_setting_sits_right_after_the_sprint_key_on_the_player_page()
    {
        var p = SettingsSchema.SprintControllerKey;
        Assert.Equal(ParamType.Key, p.Type);
        Assert.Same(KeyNames.Controller, p.Keys);
        Assert.Same(KeyNames.Keyboard, SettingsSchema.SprintKey.Keys);
        Assert.Equal(SettingsSchema.SprintKey.Index + 1, p.Index);
        Assert.Same(SettingsSchema.SprintKey.Group, p.Group);
        Assert.Equal(SettingsPage.Player, p.Group.Page);
        Assert.Equal(ApplyTiming.Live, p.Timing);
        Assert.Equal(0, p.Min);
        Assert.Equal(KeyNames.Controller.All.Length - 1, p.Max);
        Assert.Equal("ControllerLThumb", p.Format(p.Default));
        Assert.Equal("ControllerLThumb", new TraxSettings().SprintControllerKey);
        Assert.Contains("Camera toggle", p.Description); // the vanilla binding the player has to move
    }

    [Fact]
    public void The_choices_are_the_game_controller_buttons_with_none_first()
    {
        var all = KeyNames.Controller.All;
        Assert.Equal("None", all[0]);
        Assert.Equal(new[]
        {
            "None",
            "ControllerLThumb", "ControllerRThumb", "ControllerLBumper", "ControllerRBumper", "ControllerLTrigger", "ControllerRTrigger",
            "ControllerRUp", "ControllerRDown", "ControllerRLeft", "ControllerRRight",
            "ControllerLUp", "ControllerLDown", "ControllerLLeft", "ControllerLRight",
        }, all);
        Assert.Equal(all.Length, all.Distinct().Count());
        Assert.DoesNotContain(all, n => n.Contains("Stick")); // the stick DIRECTIONS move you - never offered
        Assert.DoesNotContain(all, n => n.Contains("Option"));
    }

    [Theory]
    [InlineData("ControllerLThumb", "ControllerLThumb")]
    [InlineData("controller l thumb", "ControllerLThumb")]
    [InlineData("LThumb", "ControllerLThumb")]
    [InlineData("L3", "ControllerLThumb")]
    [InlineData("left stick", "ControllerLThumb")]
    [InlineData("R3", "ControllerRThumb")]
    [InlineData("LB", "ControllerLBumper")]
    [InlineData("rb", "ControllerRBumper")]
    [InlineData("LT", "ControllerLTrigger")]
    [InlineData("R2", "ControllerRTrigger")]
    [InlineData("A", "ControllerRDown")]
    [InlineData("Y", "ControllerRUp")]
    [InlineData("X", "ControllerRLeft")]
    [InlineData("B", "ControllerRRight")]
    [InlineData("dpad-up", "ControllerLUp")]
    [InlineData("LRight", "ControllerLRight")]
    [InlineData("none", "None")]
    [InlineData("OFF", "None")]
    public void Controller_names_match_loosely(string typed, string expected)
    {
        Assert.True(KeyNames.Controller.TryIndex(typed, out int i), typed);
        Assert.Equal(expected, KeyNames.Controller.Name(i));
    }

    [Fact]
    public void The_two_lists_never_take_each_other_names()
    {
        Assert.False(KeyNames.Controller.TryIndex("LeftControl", out _));
        Assert.False(KeyNames.Controller.TryIndex("Banana", out _));
        Assert.False(KeyNames.Controller.TryIndex("", out _));
        Assert.False(KeyNames.Controller.TryIndex(null, out _));
        Assert.False(KeyNames.Controller.TryIndex("ControllerLStickUp", out _));
        Assert.False(KeyNames.TryIndex("ControllerLThumb", out _));
        Assert.False(KeyNames.TryIndex("None", out _));
        // the keyboard's own forms unchanged (step 33)
        Assert.True(KeyNames.TryIndex("left ctrl", out int lc));
        Assert.Equal("LeftControl", KeyNames.Name(lc));
        Assert.True(KeyNames.TryIndex("Y", out int y));
        Assert.Equal("Y", KeyNames.Name(y));
    }

    [Fact]
    public void The_button_round_trips_through_the_config_file_on_its_own_list()
    {
        var p = SettingsSchema.SprintControllerKey;
        Assert.Equal("\"ControllerRBumper\"", p.FormatJson(4));
        Assert.Contains("one of None, ControllerLThumb", p.DefaultAndRangeText);

        var t = new TraxSettings();
        KeyNames.Controller.TryIndex("ControllerRBumper", out int rb);
        t.Set(p, rb, "test");
        var text = ConfigFile.Write(t.Snapshot());
        Assert.Contains("\"SprintControllerKey\": \"ControllerRBumper\"", text);
        Assert.Contains("\"SprintKey\": \"LeftControl\"", text);
        var read = ConfigFile.Read(text);
        Assert.True(read.Ok);
        Assert.Equal(rb, read.Values["SprintControllerKey"]);

        var loose = ConfigFile.Read("{ \"SprintControllerKey\": \"L3\", \"SprintKey\": \"left alt\" }");
        Assert.Equal(1, loose.Values["SprintControllerKey"]);
        KeyNames.TryIndex("LeftAlt", out int la);
        Assert.Equal(la, loose.Values["SprintKey"]);

        var none = ConfigFile.Read("{ \"SprintControllerKey\": \"None\" }");
        Assert.Equal(0, none.Values["SprintControllerKey"]);

        var bad = ConfigFile.Read("{ \"SprintControllerKey\": \"LeftControl\" }"); // a keyboard key is not a controller button
        Assert.Contains(bad.Issues, i => i.Key == "SprintControllerKey");
        Assert.False(bad.Values.ContainsKey("SprintControllerKey") && bad.Values["SprintControllerKey"] != p.Default);
    }

    [Fact]
    public void Defaults_file_reads_a_button_by_name_on_its_list()
    {
        var p = SettingsSchema.SprintControllerKey;
        Assert.True(DefaultsFile.TryValue(new JValue("ControllerLThumb"), p.Type, p.Min, p.Max, out double v, out string? problem, p.Keys));
        Assert.Null(problem);
        Assert.Equal(1, v);
        Assert.False(DefaultsFile.TryValue(new JValue("LeftControl"), p.Type, p.Min, p.Max, out _, out problem, p.Keys));
        Assert.Contains("None, ControllerLThumb", problem);
        Assert.False(DefaultsFile.TryValue(new JValue(1), p.Type, p.Min, p.Max, out _, out _, p.Keys));
        // the old overload = the keyboard's list (step 33)
        Assert.True(DefaultsFile.TryValue(new JValue("CapsLock"), ParamType.Key, 0, KeyNames.All.Length - 1, out _, out _));
    }

    [Theory]
    [InlineData(false, 1, false, false)]
    [InlineData(true, 1, false, true)]   // the keyboard alone
    [InlineData(false, 1, true, true)]   // the left stick alone - Toni's ask
    [InlineData(true, 1, true, true)]    // both
    [InlineData(false, 0, true, false)]  // None: the pad is never asked
    [InlineData(true, 0, true, true)]
    [InlineData(false, 99, true, false)] // out of the list = None
    [InlineData(false, -1, true, false)]
    public void Either_key_sprints(bool keyDown, int controllerIndex, bool controllerDown, bool held)
    {
        Assert.Equal(held, SprintKeys.Held(keyDown, controllerIndex, controllerDown));
    }

    [Fact]
    public void Both_keys_are_named_in_the_text_and_none_names_the_key_alone()
    {
        Assert.True(SprintKeys.HasController(1));
        Assert.False(SprintKeys.HasController(SprintKeys.NoController));
        Assert.Equal("LeftControl or ControllerLThumb", SprintKeys.Text("LeftControl", 1));
        Assert.Equal("LeftControl", SprintKeys.Text("LeftControl", 0));

        var t = new TraxSettings();
        Assert.Equal("LeftControl or ControllerLThumb", t.SprintKeysText);
        Assert.Contains("hold LeftControl or ControllerLThumb", SprintRules.From(t).Describe(t.SprintKeysText));
        t.Set(SettingsSchema.SprintControllerKey, 0, "test");
        Assert.Equal("None", t.SprintControllerKey);
        Assert.Equal("LeftControl", t.SprintKeysText);
    }

    [Fact]
    public void The_master_switch_comes_first()
    {
        var t = new TraxSettings();
        Assert.True(SprintRules.From(t).Enabled);
        t.Set(SettingsSchema.ModEnabled, false, "test");
        var r = SprintRules.From(t);
        Assert.False(r.Enabled);
        Assert.StartsWith("OFF", r.Describe(t.SprintKeysText)); // nobody sprints, whichever key is held
    }
}

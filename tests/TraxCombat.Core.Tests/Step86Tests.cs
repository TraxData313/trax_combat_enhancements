using System;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 86 - THE SPRINT BAR NAMES ITS KEY (Toni 2026-10-24, his HUD screenshot: "(Cntr)" beside "Sprint 2.6 s"; AI_NOTES "Step 86"): the
/// setting (Advanced, "Your Athletics bar", right after ShowSprintBar, on, live), the keyboard short names, the controller short names (step
/// 85's), the glyph's sprite name, and the hint chosen by the device in use - the master switch first, then the switch, None / out of the
/// list = nothing, the glyph when the game has it, else the text.
/// </summary>
public class Step86Tests
{
    private static readonly Func<string, bool> HasGlyph = _ => true;
    private static readonly Func<string, bool> NoGlyph = _ => false;

    private static int Kb(string name)
    {
        Assert.True(KeyNames.Keyboard.TryIndex(name, out int i));
        return i;
    }

    private static int Pad(string name)
    {
        Assert.True(KeyNames.Controller.TryIndex(name, out int i));
        return i;
    }

    [Fact]
    public void The_setting_sits_after_the_sprint_bar_on_the_advanced_page_and_is_on()
    {
        var p = SettingsSchema.SprintShowKeyHint;
        Assert.Equal(ParamType.Bool, p.Type);
        Assert.Equal(SettingsSchema.ShowSprintBar.Index + 1, p.Index);
        Assert.Same(SettingsSchema.ShowSprintBar.Group, p.Group);
        Assert.Equal(SettingsPage.Advanced, p.Group.Page);
        Assert.Equal(ApplyTiming.Live, p.Timing);
        Assert.Equal(1.0, p.Default);
        Assert.True(new TraxSettings().SprintShowKeyHint);
    }

    [Theory]
    [InlineData("LeftControl", "Ctrl")]
    [InlineData("RightControl", "Ctrl")]
    [InlineData("LeftShift", "Shift")]
    [InlineData("RightShift", "Shift")]
    [InlineData("LeftAlt", "Alt")]
    [InlineData("RightAlt", "Alt")]
    [InlineData("CapsLock", "Caps")]
    [InlineData("Tab", "Tab")]
    [InlineData("Q", "Q")]
    [InlineData("C", "C")]
    [InlineData("MiddleMouseButton", "Mouse 3")]
    [InlineData("X1MouseButton", "Mouse 4")]
    [InlineData("X2MouseButton", "Mouse 5")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Keyboard_short_names(string? key, string expected) => Assert.Equal(expected, SprintKeyHints.KeyboardShort(key));

    [Fact]
    public void Every_keyboard_key_has_a_short_name_of_at_most_seven_letters()
    {
        foreach (var k in KeyNames.Keyboard.All)
        {
            var s = SprintKeyHints.KeyboardShort(k);
            Assert.False(string.IsNullOrEmpty(s), k);
            Assert.True(s.Length <= 7, k + " -> " + s);
        }
    }

    [Theory]
    [InlineData("ControllerLThumb", "L3")]
    [InlineData("ControllerRThumb", "R3")]
    [InlineData("ControllerLBumper", "LB")]
    [InlineData("ControllerRBumper", "RB")]
    [InlineData("ControllerLTrigger", "LT")]
    [InlineData("ControllerRTrigger", "RT")]
    [InlineData("ControllerRUp", "Y")]
    [InlineData("ControllerRDown", "A")]
    [InlineData("ControllerRLeft", "X")]
    [InlineData("ControllerRRight", "B")]
    [InlineData("ControllerLUp", "D-pad up")]
    [InlineData("ControllerLRight", "D-pad right")]
    [InlineData("None", "")]
    [InlineData(null, "")]
    public void Controller_short_names(string? key, string expected) => Assert.Equal(expected, SprintKeyHints.ControllerShort(key));

    [Fact]
    public void Every_controller_short_name_reads_back_as_its_button()
    {
        // the bar's short name is one the setting itself accepts (step 85's aliases) - the d-pad's spelt with a space and a dash
        for (int i = 1; i < KeyNames.Controller.All.Length; i++)
        {
            var name = KeyNames.Controller.All[i];
            var s = SprintKeyHints.ControllerShort(name);
            Assert.True(KeyNames.Controller.TryIndex(s.Replace("D-pad ", "Dpad"), out int back), name + " -> " + s);
            Assert.Equal(i, back);
        }
    }

    [Fact]
    public void The_glyph_sprite_is_vanillas_input_keys_name()
    {
        Assert.Equal("General\\InputKeys\\controllerlthumb", SprintKeyHints.GlyphSprite("ControllerLThumb"));
        Assert.Equal("General\\InputKeys\\controllerrbumper", SprintKeyHints.GlyphSprite("ControllerRBumper"));
    }

    [Fact]
    public void On_mouse_and_keyboard_the_key_in_brackets()
    {
        var h = SprintKeyHints.Decide(true, true, false, Kb("LeftControl"), Pad("L3"), HasGlyph);
        Assert.Equal(SprintHintKind.Text, h.Kind);
        Assert.Equal("(Ctrl)", h.Text);
        Assert.Equal(string.Empty, h.GlyphKey);
        Assert.Equal("(Shift)", SprintKeyHints.Decide(true, true, false, Kb("LeftShift"), 0, null).Text);
        Assert.Equal("(Q)", SprintKeyHints.Decide(true, true, false, Kb("Q"), 0, null).Text);
        // the keyboard never asks for a glyph
        Assert.Equal("(Alt)", SprintKeyHints.Decide(true, true, false, Kb("LeftAlt"), Pad("L3"), _ => throw new InvalidOperationException()).Text);
    }

    [Fact]
    public void On_a_pad_the_game_glyph_for_the_controller_button()
    {
        var h = SprintKeyHints.Decide(true, true, true, Kb("LeftControl"), Pad("L3"), HasGlyph);
        Assert.Equal(SprintHintKind.Glyph, h.Kind);
        Assert.Equal("ControllerLThumb", h.GlyphKey);
        Assert.Equal(string.Empty, h.Text);
        Assert.Equal("the game's glyph for ControllerLThumb", h.Describe());
        string? asked = null;
        SprintKeyHints.Decide(true, true, true, 0, Pad("RB"), k => { asked = k; return true; });
        Assert.Equal("ControllerRBumper", asked);
    }

    [Fact]
    public void On_a_pad_without_the_glyph_the_short_text()
    {
        Assert.Equal("(L3)", SprintKeyHints.Decide(true, true, true, 0, Pad("L3"), NoGlyph).Text);
        Assert.Equal("(RB)", SprintKeyHints.Decide(true, true, true, 0, Pad("RB"), null).Text);
        Assert.Equal("(A)", SprintKeyHints.Decide(true, true, true, 0, Pad("A"), _ => throw new InvalidOperationException()).Text); // fail safe
    }

    [Fact]
    public void Nothing_when_off_none_or_out_of_the_list()
    {
        Assert.Equal(SprintHintKind.None, SprintKeyHints.Decide(false, true, false, Kb("LeftControl"), Pad("L3"), HasGlyph).Kind); // the master switch FIRST
        Assert.Equal(SprintHintKind.None, SprintKeyHints.Decide(false, true, true, Kb("LeftControl"), Pad("L3"), HasGlyph).Kind);
        Assert.Equal(SprintHintKind.None, SprintKeyHints.Decide(true, false, false, Kb("LeftControl"), Pad("L3"), HasGlyph).Kind);
        Assert.Equal(SprintHintKind.None, SprintKeyHints.Decide(true, false, true, Kb("LeftControl"), Pad("L3"), HasGlyph).Kind);
        Assert.Equal(SprintHintKind.None, SprintKeyHints.Decide(true, true, true, Kb("LeftControl"), SprintKeys.NoController, HasGlyph).Kind); // a pad, None
        Assert.Equal(SprintHintKind.None, SprintKeyHints.Decide(true, true, true, 0, 99, HasGlyph).Kind);
        Assert.Equal(SprintHintKind.None, SprintKeyHints.Decide(true, true, false, -1, Pad("L3"), HasGlyph).Kind);
        Assert.Equal(SprintHintKind.None, SprintKeyHints.Decide(true, true, false, KeyNames.Keyboard.All.Length, Pad("L3"), HasGlyph).Kind);
        // the keyboard key still shows on the keyboard with the controller button None
        Assert.Equal("(Ctrl)", SprintKeyHints.Decide(true, true, false, Kb("LeftControl"), SprintKeys.NoController, HasGlyph).Text);
        Assert.Equal("nothing", SprintKeyHint.Nothing.Describe());
    }

    [Fact]
    public void The_device_switches_the_hint_with_the_same_settings()
    {
        int key = Kb("LeftControl"), pad = Pad("L3");
        Assert.Equal(SprintHintKind.Text, SprintKeyHints.Decide(true, true, false, key, pad, HasGlyph).Kind);
        Assert.Equal(SprintHintKind.Glyph, SprintKeyHints.Decide(true, true, true, key, pad, HasGlyph).Kind);
        Assert.Equal(SprintHintKind.Text, SprintKeyHints.Decide(true, true, false, key, pad, HasGlyph).Kind);
    }
}

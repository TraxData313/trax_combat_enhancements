using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 112 (Toni 2026-11-10: "Make the PERFECT STRIKE [arrow] gold"; AI_NOTES "Step 112", DESIGN decision 101): the arrow's colour by its role -
/// the opening to hit red, the perfect-strike side gold - both settings, and the role never changes while your swing is in the air.
/// </summary>
public class Step112Tests
{
    [Fact]
    public void The_two_colours_sit_after_the_flight_seconds_with_their_defaults_and_are_live()
    {
        var all = SettingsSchema.All.ToList();
        int d = all.IndexOf(SettingsSchema.PerfectStrikeFlightSeconds);
        Assert.Equal(d + 1, all.IndexOf(SettingsSchema.OpeningArrowColor));
        Assert.Equal(d + 2, all.IndexOf(SettingsSchema.PerfectStrikeArrowColor));
        foreach (var p in new[] { SettingsSchema.OpeningArrowColor, SettingsSchema.PerfectStrikeArrowColor })
        {
            Assert.DoesNotContain(p, SettingsSchema.PlayerPage);
            Assert.Same(SettingsSchema.PerfectStrikeFlightSeconds.Group, p.Group);
            Assert.Equal(ApplyTiming.Live, p.Timing);
            Assert.Equal(ParamType.Text, p.Type);
        }
        var s = new TraxSettings();
        Assert.Equal("F01E1E", s.OpeningArrowColor);
        Assert.Equal("FFD700", s.PerfectStrikeArrowColor);
        // the gold is the PERFECT STRIKE line's own (#FFD700FF)
        Assert.StartsWith("#" + s.PerfectStrikeArrowColor, PerfectStrikeMath.GoldHex);
    }

    [Fact]
    public void The_role_picks_the_colour_the_perfect_strike_gold_everything_else_red()
    {
        Assert.Equal(ArrowRole.PerfectStrike, ArrowColours.RoleOf(ArrowCue.Finish));
        Assert.Equal(ArrowRole.Opening, ArrowColours.RoleOf(ArrowCue.Hold));
        Assert.Equal(ArrowRole.Opening, ArrowColours.RoleOf(ArrowCue.Flash));
        Assert.Equal(ArrowRole.Opening, ArrowColours.RoleOf(ArrowCue.None));

        var s = new TraxSettings();
        ArrowColours.Resolve(ArrowRole.Opening, s, out float r, out float g, out float b, out float a, out bool def);
        Assert.False(def);
        Assert.Equal(0xF0 / 255f, r, 5);
        Assert.Equal(0x1E / 255f, g, 5);
        Assert.Equal(0x1E / 255f, b, 5);
        Assert.Equal(1f, a, 5);
        ArrowColours.Resolve(ArrowRole.PerfectStrike, s, out r, out g, out b, out a, out def);
        Assert.False(def);
        Assert.Equal(1f, r, 5);
        Assert.Equal(0xD7 / 255f, g, 5);
        Assert.Equal(0f, b, 5);

        // a typed colour is live; '#' is accepted; not a colour = the default
        Assert.True(TextValues.TryIndex("#00FF0080", out int green));
        s.Set(SettingsSchema.PerfectStrikeArrowColor, green, SettingSources.File);
        ArrowColours.Resolve(ArrowRole.PerfectStrike, s, out r, out g, out b, out a, out def);
        Assert.False(def);
        Assert.Equal(0f, r, 5);
        Assert.Equal(1f, g, 5);
        Assert.Equal(0x80 / 255f, a, 5);
        Assert.True(TextValues.TryIndex("gold", out int word));
        s.Set(SettingsSchema.PerfectStrikeArrowColor, word, SettingSources.File);
        ArrowColours.Resolve(ArrowRole.PerfectStrike, s, out r, out g, out b, out _, out def);
        Assert.True(def);
        Assert.Equal(0xD7 / 255f, g, 5);
    }

    [Theory]
    [InlineData("FFD700", true)]
    [InlineData("#ffd700", true)]
    [InlineData("  F01E1EFF ", true)]
    [InlineData("FFD70", false)]
    [InlineData("FFD7000", false)]
    [InlineData("GGD700", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Hex_colours_parse_six_or_eight_digits(string? text, bool ok)
    {
        Assert.Equal(ok, ArrowColours.TryParse(text, out _, out _, out _, out _));
    }

    [Fact]
    public void The_colour_never_changes_mid_swing_the_latch_holds_the_role_on_the_same_side()
    {
        var hold = new ArrowReading { Cue = ArrowCue.Hold, Mask = OpeningMath.Bit(OpeningMath.AttackUp), Alpha = 1f };
        var finish = new ArrowReading { Cue = ArrowCue.Finish, Mask = hold.Mask, Alpha = 1f };
        var latch = new ArrowLatch();
        latch.Begin(10, in hold); // your swing on the hold's red arrow
        var shown = latch.Filter(10.2, 1.5, in finish, out bool held); // the same side turns into a perfect-strike side while the swing is in the air
        Assert.True(held);
        Assert.Equal(ArrowCue.Hold, shown.Cue); // still red
        Assert.Equal(ArrowRole.Opening, ArrowColours.RoleOf(shown.Cue));
        latch.End(); // the swing landed: now the role (and the colour) follows
        shown = latch.Filter(10.4, 1.5, in finish, out held);
        Assert.False(held);
        Assert.Equal(ArrowRole.PerfectStrike, ArrowColours.RoleOf(shown.Cue));

        // the same role on the same side is never a hold
        latch.Begin(20, in finish);
        shown = latch.Filter(20.1, 1.5, in finish, out held);
        Assert.False(held);
        Assert.Equal(ArrowCue.Finish, shown.Cue);
    }

    [Fact]
    public void Resolving_a_colour_allocates_nothing()
    {
        var s = new TraxSettings();
        long bytes = NoAllocation.Bytes(() =>
        {
            for (int i = 0; i < 1000; i++)
            {
                var role = ArrowColours.RoleOf(i % 2 == 0 ? ArrowCue.Finish : ArrowCue.Hold);
                ArrowColours.TryParse(ArrowColours.TextFor(role, s), out _, out _, out _, out _);
            }
        });
        Assert.Equal(0, bytes);
    }
}

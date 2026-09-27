using System.Text.RegularExpressions;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>Step 6's bar maths (DESIGN §3 additions): the colour band from f and the live
/// thresholds, the shares the prefab draws, and the "132 / 180" the player reads - checked on
/// DESIGN's own fighters (a recruit's five blows, a wounded man who can never turn green).</summary>
public class AthleticsBarTests
{
    /// <summary>DESIGN's initial thresholds: yellow ≤ 75%, orange ≤ 50%, red ≤ 25% of the peak line.</summary>
    private static readonly BarRules Design = new(75, 50, 25);

    private static AthleticsRules Athletics()
        => new(true, 50, 1.0f, 75, true, 10, true, 0.75f, 0.75f, 20, 0.3f, 1.0f, true, 2, 1.5f, 60, 0.5f, 0.4f, true);

    // ------------------------------------------------------------------ settings

    [Fact]
    public void Rules_read_the_live_settings()
    {
        var s = new TraxSettings();
        var r = BarRules.From(s);
        Assert.Equal(75, r.YellowBelowPercent);
        Assert.Equal(50, r.OrangeBelowPercent);
        Assert.Equal(25, r.RedBelowPercent);

        s.Set(SettingsSchema.BarYellowBelowPercent, 90, SettingSources.Mcm);
        s.Set(SettingsSchema.BarRedBelowPercent, 10, SettingSources.Mcm);
        r = BarRules.From(s);
        Assert.Equal(90, r.YellowBelowPercent);
        Assert.Equal(10, r.RedBelowPercent);
        Assert.Equal(BarBand.Yellow, BarMath.Band(in r, 0.85)); // live: the next refresh uses it
        Assert.Contains("yellow at or below 90%, orange 50%, red 10% of the peak line", r.Describe());
    }

    // ------------------------------------------------------------------ bands

    [Theory]
    [InlineData(1.0, BarBand.Green)]
    [InlineData(0.99, BarBand.Blue)]
    [InlineData(0.76, BarBand.Blue)]
    [InlineData(0.75, BarBand.Yellow)]   // "at or below"
    [InlineData(0.51, BarBand.Yellow)]
    [InlineData(0.50, BarBand.Orange)]
    [InlineData(0.26, BarBand.Orange)]
    [InlineData(0.25, BarBand.Red)]
    [InlineData(0.01, BarBand.Red)]
    [InlineData(0.0, BarBand.Red)]
    public void Band_follows_f_at_the_design_thresholds(double f, BarBand expected)
    {
        Assert.Equal(expected, BarMath.Band(in Design, f));
    }

    [Fact]
    public void Nan_reads_as_full_strength_and_an_empty_bar_is_always_red()
    {
        Assert.Equal(BarBand.Green, BarMath.Band(in Design, double.NaN));
        var noRed = new BarRules(75, 50, 0);
        Assert.Equal(BarBand.Red, BarMath.Band(in noRed, 0));        // empty: red even with red at 0%
        Assert.Equal(BarBand.Orange, BarMath.Band(in noRed, 0.001)); // anything left: not red
    }

    [Fact]
    public void Thresholds_out_of_order_still_pick_the_most_alarming_band()
    {
        var odd = new BarRules(40, 50, 60);          // red above orange above yellow
        Assert.Equal(BarBand.Red, BarMath.Band(in odd, 0.55));
        Assert.Equal(BarBand.Blue, BarMath.Band(in odd, 0.65));
        var allYellow = new BarRules(100, 50, 25);   // no blue at all
        Assert.Equal(BarBand.Yellow, BarMath.Band(in allYellow, 0.99));
        Assert.Equal(BarBand.Green, BarMath.Band(in allYellow, 1.0));
    }

    [Fact]
    public void A_recruits_five_blows_walk_through_every_colour()
    {
        // DESIGN §2: a recruit (skill 20 → the floor, 50 points) has 2 blows at full strength and
        // is empty on the 5th - on the bar: green, green, blue, yellow, orange, red.
        var r = Athletics();
        var f = new Fighter { AthleticsSkill = 20 };
        var seen = new List<BarBand> { BarMath.Band(in Design, AthleticsMath.PeakShare(in r, f)) };
        for (int i = 0; i < 5; i++)
        {
            AthleticsMath.Charge(f, in r, i);
            seen.Add(BarMath.Band(in Design, AthleticsMath.PeakShare(in r, f)));
        }
        Assert.Equal(new[] { BarBand.Green, BarBand.Green, BarBand.Blue, BarBand.Yellow, BarBand.Orange, BarBand.Red }, seen);
        Assert.True(f.Exhausted);
    }

    [Fact]
    public void A_badly_wounded_fighter_is_never_green_or_blue()
    {
        // Health caps the pool at 50%; the peak line stays at 75% of the FULL pool → f ≤ 0.67.
        var r = Athletics();
        var f = new Fighter { AthleticsSkill = 180 };
        AthleticsMath.ApplyHealth(f, in r, 0.5);
        var reading = AthleticsMath.Read(in r, f);
        Assert.Equal(0.5, reading.UsableFraction, 9);
        Assert.Equal(BarBand.Yellow, BarMath.Band(in Design, reading.PeakShare));
        Assert.Equal(0.5, BarMath.Capped(reading.UsableFraction), 9);
        Assert.Equal(0.5f, BarMath.Usable(reading.UsableFraction), 6);
    }

    // ------------------------------------------------------------------ what the prefab draws

    [Fact]
    public void Shares_are_clamped_and_nan_safe()
    {
        Assert.Equal(0.42f, BarMath.Fill(0.42), 6);
        Assert.Equal(0f, BarMath.Fill(-0.1));
        Assert.Equal(1f, BarMath.Fill(1.2));
        Assert.Equal(0f, BarMath.Fill(double.NaN));        // draw nothing rather than a lie
        Assert.Equal(1f, BarMath.Usable(double.NaN));      // no dark part rather than a fake wound
        Assert.Equal(0.75f, BarMath.PeakLine(0.75), 6);
        Assert.Equal(1f, BarMath.PeakLine(1.5));
        Assert.Equal(0.0, BarMath.Capped(1.0), 9);
        Assert.Equal(0.38, BarMath.Capped(0.62), 9);
    }

    [Theory]
    [InlineData(180.0, 180.0, 180, 180)]    // a fresh bar reads its pool
    [InlineData(174.4, 180.0, 175, 180)]    // rounded UP ...
    [InlineData(0.3, 50.0, 1, 50)]          // ... so a sliver left is never "0"
    [InlineData(0.0, 50.0, 0, 50)]          // 0 only when really empty
    [InlineData(1e-12, 50.0, 0, 50)]
    [InlineData(180.4, 180.4, 180, 180)]    // never above the shown pool
    [InlineData(0.2, 0.4, 1, 1)]            // a pool never reads below 1
    [InlineData(double.NaN, 100.0, 0, 100)]
    public void Numbers_read_like_the_skill_screen(double points, double pool, int shownPoints, int shownPool)
    {
        BarMath.DisplayNumbers(points, pool, out int p, out int q);
        Assert.Equal(shownPoints, p);
        Assert.Equal(shownPool, q);
    }

    [Fact]
    public void Number_text_is_points_slash_pool()
    {
        Assert.Equal("132 / 180", BarMath.NumberText(132, 180));
        Assert.Equal("0 / 50", BarMath.NumberText(0, 50));
    }

    [Fact]
    public void Colours_are_game_colour_strings_one_per_band()
    {
        var rgba = new Regex("^#[0-9A-F]{8}$");
        var all = Enum.GetValues<BarBand>().Select(BarMath.ColorHex).ToList();
        Assert.Equal(BarMath.BandCount, all.Count);
        Assert.Equal(all.Count, all.Distinct().Count());
        foreach (var c in all.Concat(new[] { BarMath.TextHex, BarMath.LabelHex, BarMath.FrameHex, BarMath.ExhaustedHex }))
            Assert.Matches(rgba, c);
        Assert.Equal("green", BarMath.Name(BarBand.Green));
        Assert.Equal("red", BarMath.Name(BarBand.Red));
        Assert.StartsWith("green (peak zone", BarMath.Describe(BarBand.Green));
        Assert.Equal("Athletics", BarMath.LabelText);
        Assert.Equal("Exhausted", BarMath.ExhaustedLabelText);
    }
}

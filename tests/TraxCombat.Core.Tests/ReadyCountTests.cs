using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 24 - the ready count ("ready 34/50", the men NOT bracing) on the orders-menu strip and under the formation markers:
/// when it is hidden (the master switch first, Athletics, bracing, its own switch, no men), its text, its colour bands (the
/// most alarming wins), the strip cell growing by one row only while it is drawn (and lifted at the screen's edge), the log
/// fragment and the [summary] line.
/// </summary>
public class ReadyCountTests
{
    private static ReadyRules Rules(bool show = true, bool mod = true, bool athletics = true, bool brace = true, int yellow = 75, int red = 50) =>
        new ReadyRules(show, mod, athletics, brace, yellow, red);

    /// <summary>A formation of <paramref name="men"/>, <paramref name="bracing"/> of them bracing.</summary>
    private static FormationAthleticsStats Squad(int men, int bracing) =>
        new FormationAthleticsStats(men, 50, 10, 0.5, 0.1, 0, 0.6, 0, 0.8, bracing);

    [Fact]
    public void Rules_read_the_live_settings_with_DESIGNs_defaults()
    {
        var s = new TraxSettings();
        var r = ReadyRules.From(s);
        Assert.True(r.ShowReadyCount);
        Assert.True(r.Shown);
        Assert.Equal(75, r.YellowBelowPercent);
        Assert.Equal(50, r.RedBelowPercent);
        Assert.Equal("ready count on: yellow at or below 75%, red at or below 50% of the men ready", r.Describe());

        s.Set(SettingsSchema.ReadyYellowBelowPercent, 90, SettingSources.Mcm);
        s.Set(SettingsSchema.ReadyRedBelowPercent, 30, SettingSources.Mcm);
        r = ReadyRules.From(s);
        Assert.Equal(90, r.YellowBelowPercent);
        Assert.Equal(30, r.RedBelowPercent);

        s.Set(SettingsSchema.BraceEnabled, false, SettingSources.Mcm);
        Assert.Equal(ReadyHidden.BraceOff, ReadyRules.From(s).Hidden); // live: the next push hides it
        Assert.Equal("ready count hidden (BraceEnabled off - nobody braces)", ReadyRules.From(s).Describe());
    }

    [Fact]
    public void Hidden_the_master_switch_first_then_Athletics_bracing_its_switch_and_no_men()
    {
        Assert.Equal(ReadyHidden.ModOff, Rules(show: false, mod: false, athletics: false, brace: false).Hidden);
        Assert.Equal(ReadyHidden.AthleticsOff, Rules(show: false, athletics: false, brace: false).Hidden);
        Assert.Equal(ReadyHidden.BraceOff, Rules(show: false, brace: false).Hidden);
        Assert.Equal(ReadyHidden.SwitchedOff, Rules(show: false).Hidden);
        Assert.Equal(ReadyHidden.None, Rules().Hidden);

        var on = Rules();
        Assert.Equal(ReadyHidden.None, ReadyMath.HiddenFor(in on, Squad(50, 16)));
        Assert.Equal(ReadyHidden.NoMen, ReadyMath.HiddenFor(in on, default));
        var off = Rules(brace: false);
        Assert.Equal(ReadyHidden.BraceOff, ReadyMath.HiddenFor(in off, Squad(50, 0)));
        Assert.Equal("ShowReadyCount off", ReadyMath.HiddenName(ReadyHidden.SwitchedOff));
        Assert.Equal("ModEnabled off", ReadyMath.HiddenName(ReadyHidden.ModOff));
    }

    [Fact]
    public void The_counts_are_the_formation_stats_Ready_of_Total()
    {
        var s = Squad(50, 16);
        Assert.Equal(34, s.Ready);
        Assert.Equal(50, s.Total);
        Assert.Equal("ready 34/50", ReadyMath.Text(s.Ready, s.Total));
        Assert.Equal("ready 0/12", ReadyMath.Text(0, 12));
        Assert.Equal("ready 150/150", ReadyMath.Text(150, 150));
        // more bracing than men cannot happen - clamped
        Assert.Equal(0, Squad(10, 12).Ready);
    }

    [Theory]
    [InlineData(50, 50, ReadyBand.Plain)]   // all ready
    [InlineData(38, 50, ReadyBand.Plain)]   // 76%
    [InlineData(37, 50, ReadyBand.Yellow)]  // 74%
    [InlineData(3, 4, ReadyBand.Yellow)]    // 75% exactly: "at or below"
    [InlineData(26, 50, ReadyBand.Yellow)]  // 52%
    [InlineData(25, 50, ReadyBand.Red)]     // 50% exactly
    [InlineData(0, 50, ReadyBand.Red)]
    [InlineData(0, 0, ReadyBand.Plain)]     // no men: nothing to warn about
    public void Bands_by_the_share_ready(int ready, int total, ReadyBand expected)
    {
        var r = Rules();
        Assert.Equal(expected, ReadyMath.Band(in r, ready, total));
    }

    [Fact]
    public void The_most_alarming_band_wins_and_the_thresholds_are_live_values()
    {
        var upside = Rules(yellow: 40, red: 60); // set the wrong way round: red covers yellow
        Assert.Equal(ReadyBand.Red, ReadyMath.Band(in upside, 30, 100));
        Assert.Equal(ReadyBand.Red, ReadyMath.Band(in upside, 55, 100));
        Assert.Equal(ReadyBand.Plain, ReadyMath.Band(in upside, 61, 100));
        var never = Rules(yellow: 0, red: 0);
        Assert.Equal(ReadyBand.Plain, ReadyMath.Band(in never, 1, 100));
        Assert.Equal(ReadyBand.Red, ReadyMath.Band(in never, 0, 100)); // nobody ready is always "at or below 0"
        var always = Rules(yellow: 100, red: 0);
        Assert.Equal(ReadyBand.Yellow, ReadyMath.Band(in always, 100, 100));
    }

    [Fact]
    public void Colours_plain_is_the_text_brushes_own_then_the_bars_yellow_and_red()
    {
        Assert.Equal("#E8E8E8FF", ReadyMath.ColorHex(ReadyBand.Plain));
        Assert.Equal(BarMath.YellowHex, ReadyMath.ColorHex(ReadyBand.Yellow));
        Assert.Equal(BarMath.RedHex, ReadyMath.ColorHex(ReadyBand.Red));
    }

    [Fact]
    public void The_strip_cell_grows_by_the_ready_row_only_while_it_is_drawn()
    {
        var without = new StripLayout(13, 1, 20, 4, 2, 24, readyRow: false);
        Assert.Equal(1f, without.Top);
        Assert.Equal(23f, without.Depth, 3); // step 9's cell, unchanged with bracing off
        Assert.DoesNotContain("ready", without.Describe());

        var with = new StripLayout(13, 1, 20, 4, 2, 24, readyRow: true);
        Assert.Equal(1f, with.Top);
        Assert.Equal(23f, with.ReadyMarginTop);
        Assert.Equal(19f, with.BarMarginTop);
        Assert.Equal(24 + 13 * 1.2f - 1, with.Depth, 3); // 38.6: the 40 px gap to the next stacked card
        Assert.Equal("numbers 13 px at card bottom +1, bar 4 px at +20, ready count at +24, 2 px in from the sides (39 px deep; UI pixels - the game's UI scale applies)",
            with.Describe());

        // a ready row set ABOVE the numbers becomes the cell's top - no negative margin
        var above = new StripLayout(13, 1, 20, 4, 2, -10, readyRow: true);
        Assert.Equal(-10f, above.Top);
        Assert.Equal(0f, above.ReadyMarginTop);
        Assert.Equal(11f, above.TextMarginTop);
    }

    [Fact]
    public void Layout_from_the_live_settings_follows_the_ready_rules()
    {
        var s = new TraxSettings();
        var l = StripLayout.From(s);
        Assert.True(l.ReadyRow);
        Assert.Equal(24, l.ReadyOffset);
        s.Set(SettingsSchema.BraceEnabled, false, SettingSources.Mcm);
        Assert.False(StripLayout.From(s).ReadyRow);
        s.Set(SettingsSchema.BraceEnabled, true, SettingSources.Mcm);
        s.Set(SettingsSchema.ShowReadyCount, false, SettingSources.Mcm);
        Assert.False(StripLayout.From(s).ReadyRow);
        s.Set(SettingsSchema.ShowReadyCount, true, SettingSources.Mcm);
        s.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
        Assert.False(StripLayout.From(s).ReadyRow);
    }

    [Fact]
    public void The_bottom_card_of_a_1080p_column_is_lifted_only_with_the_ready_row()
    {
        var card = new OrderCard { Visible = true, X = 20, Y = 833, Width = 131, Height = 223 }; // bottom at 1056: 24 px to the edge
        OrderStripMath.PlaceCell(card, new StripLayout(13, 1, 20, 4, 2, 24, false), 1f, 1080, out _, out float y, out _, out bool lifted);
        Assert.False(lifted);
        Assert.Equal(1057f, y);
        OrderStripMath.PlaceCell(card, new StripLayout(13, 1, 20, 4, 2, 24, true), 1f, 1080, out _, out y, out _, out lifted);
        Assert.True(lifted);
        Assert.Equal(1080f - 38.6f, y, 3);
        // a card higher up: never lifted
        OrderStripMath.PlaceCell(new OrderCard { Y = 44, Height = 223 }, new StripLayout(13, 1, 20, 4, 2, 24, true), 1f, 1080, out _, out y, out _, out lifted);
        Assert.False(lifted);
        Assert.Equal(268f, y);
    }

    [Fact]
    public void The_values_line_names_the_ready_count_when_shown()
    {
        var s = Squad(40, 6);
        Assert.Equal("1 Infantry 50% ± 10 HP 80% ready 34/40 (40 men, f 0.60)", OrderStripMath.DescribeValues(0, s, 50, 10, 80, 34, 40));
        Assert.Equal("1 Infantry 50% ± 10 HP 80% (40 men, f 0.60)", OrderStripMath.DescribeValues(0, s, 50, 10, 80));
        var m = new AltMarker { TeamType = (int)MarkerTeam.Enemy, FormationIndex = 2, Stats = s };
        Assert.Equal("enemy 3 Cavalry 50% ± 10 HP 80% ready 34/40 (40 men, f 0.60)", AltMarkerMath.DescribeValues(in m, 50, 10, 80, 34, 40));
    }

    [Fact]
    public void Summary_line_counts_bands_the_fewest_ready_and_hidden_reasons()
    {
        var never = new ReadyCountStats();
        Assert.Equal("hud: orders strip - ready count (men not bracing, step 24): never shown (no values pushed)", never.SummaryLine("orders strip"));

        var off = new ReadyCountStats();
        off.NoteHidden(ReadyHidden.BraceOff);
        off.NoteHidden(ReadyHidden.BraceOff);
        Assert.Equal("hud: orders strip - ready count (men not bracing, step 24): never shown; hidden 2x (BraceEnabled off - nobody braces 2)", off.SummaryLine("orders strip"));

        var strip = new ReadyCountStats();
        strip.NoteShown(40, 40, ReadyBand.Plain, 0);
        strip.NoteShown(30, 40, ReadyBand.Yellow, 0);
        strip.NoteShown(3, 12, ReadyBand.Red, 2);
        strip.NoteShown(10, 12, ReadyBand.Plain, 2);
        strip.NoteHidden(ReadyHidden.SwitchedOff);
        Assert.Equal(4, strip.Shown);
        Assert.Equal(1, strip.ShownIn(ReadyBand.Red));
        Assert.Equal("hud: orders strip - ready count (men not bracing, step 24): shown 4x (plain 2, yellow 1, red 1), the fewest ready 3/12 (25%, 3 Cavalry); hidden 1x (ShowReadyCount off 1)",
            strip.SummaryLine("orders strip"));

        var alt = new ReadyCountStats();
        alt.NoteShown(20, 80, ReadyBand.Red, 0, (int)MarkerTeam.Enemy);
        Assert.Equal("hud: ALT markers - ready count (men not bracing, step 24): shown 1x (plain 0, yellow 0, red 1), the fewest ready 20/80 (25%, enemy 1 Infantry); hidden: never",
            alt.SummaryLine("ALT markers"));
    }
}

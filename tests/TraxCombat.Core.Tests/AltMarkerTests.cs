using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 20 - the numbers under vanilla's formation markers (hold ALT): pairing the marker widgets with their
/// targets (vanilla's order, a re-sort, a missing widget, two markers behind the camera), whether a marker gets a
/// label and why not, where the label goes at any UI scale (and under a pinned marker), vanilla's distance fade,
/// the nominal size of a fallback, the layout, the texts and the [summary] line.
/// </summary>
public class AltMarkerTests
{
    private static readonly FormationAthleticsStats Tired = new(40, 72, 8, 0.72, 0.08, 0, 0.9, 5, 0.81);

    /// <summary>A marker at (x, y) with a live 60 x 108 widget, in front of the camera, 80 m away.</summary>
    private static AltMarker Marker(int team, int formation, float x, float y, bool stats = true) => new()
    {
        Key = AltMarkerMath.Key(team == 2 ? 1 : 0, formation),
        TeamType = team,
        FormationIndex = formation,
        PointX = x,
        PointY = y,
        WSign = 1,
        Distance = 80,
        Men = 40,
        HasWidget = true,
        LiveSize = true,
        Width = 60,
        Height = 108,
        Alpha = 1,
        Stats = stats ? Tired : default,
    };

    private static MarkerWidget Widget(float x, float y, float w = 60, float h = 108, float alpha = 1) => new()
    {
        PositionX = x,
        PositionY = y,
        Width = w,
        Height = h,
        Alpha = alpha,
        OffsetX = x - w / 2,
        OffsetY = y - h / 2,
    };

    private static AltMarkerFrame Frame(params (float X, float Y)[] points)
    {
        var f = new AltMarkerFrame { ScreenWidth = 1920, ScreenHeight = 1080, Scale = 1 };
        for (int i = 0; i < points.Length; i++)
        {
            f.Markers[i] = new AltMarker { Key = i, PointX = points[i].X, PointY = points[i].Y, WSign = 1, Distance = 50 };
        }
        f.Count = points.Length;
        return f;
    }

    // ------------------------------------------------------------------ pairing

    [Fact]
    public void Widgets_in_the_lists_order_pair_by_index()
    {
        var f = Frame((100, 200), (500, 300), (900, 400));
        f.Widgets[0] = Widget(100, 200, 60, 100, 0.9f);
        f.Widgets[1] = Widget(500, 300, 60, 108);
        f.Widgets[2] = Widget(900, 400, 64, 110);
        f.WidgetCount = 3;
        Assert.Equal(3, AltMarkerMath.Pair(f));
        Assert.True(f.Markers[0].HasWidget && f.Markers[0].LiveSize);
        Assert.Equal((60f, 100f, 0.9f), (f.Markers[0].Width, f.Markers[0].Height, f.Markers[0].Alpha));
        Assert.Equal((64f, 110f), (f.Markers[2].Width, f.Markers[2].Height));
        Assert.Equal((868f, 345f), (f.Markers[2].OffsetX, f.Markers[2].OffsetY));
    }

    [Fact]
    public void A_resorted_list_still_pairs_by_the_exact_point()
    {
        // vanilla re-sorts the targets far-first every frame; if the widgets lag a frame they still pair by point
        var f = Frame((100, 200), (500, 300), (900, 400));
        f.Widgets[0] = Widget(900, 400, 70);
        f.Widgets[1] = Widget(100, 200, 50);
        f.Widgets[2] = Widget(500, 300, 60);
        f.WidgetCount = 3;
        Assert.Equal(3, AltMarkerMath.Pair(f));
        Assert.Equal(50f, f.Markers[0].Width);
        Assert.Equal(60f, f.Markers[1].Width);
        Assert.Equal(70f, f.Markers[2].Width);
    }

    [Fact]
    public void A_marker_without_its_widget_is_left_unpaired_and_duplicates_take_one_each()
    {
        var f = Frame((100, 200), (-10000, -10000), (-10000, -10000));
        f.Widgets[0] = Widget(-10000, -10000, 61);
        f.Widgets[1] = Widget(-10000, -10000, 62);
        f.WidgetCount = 2;
        Assert.Equal(2, AltMarkerMath.Pair(f));
        Assert.False(f.Markers[0].HasWidget);
        Assert.True(f.Markers[1].HasWidget && f.Markers[2].HasWidget);
        Assert.NotEqual(f.Markers[1].Width, f.Markers[2].Width);
        // a second pairing of the same frame starts clean
        Assert.Equal(2, AltMarkerMath.Pair(f));
    }

    [Fact]
    public void Nominal_size_follows_the_distance_row_and_the_scale()
    {
        var m = new AltMarker { Distance = 80 };
        AltMarkerMath.Nominal(ref m, 1f, distanceShown: true);
        Assert.True(m.HasWidget);
        Assert.False(m.LiveSize);
        Assert.Equal((60f, 108f), (m.Width, m.Height));
        AltMarkerMath.Nominal(ref m, 1.5f, distanceShown: false);
        Assert.Equal((90f, 108f), (m.Width, m.Height));
        Assert.Equal(AltMarkerMath.VanillaAlpha(80), m.Alpha);
    }

    // ------------------------------------------------------------------ sight

    [Fact]
    public void Sight_says_why_a_marker_gets_no_label()
    {
        var yours = Marker(0, 0, 960, 400);
        Assert.Equal(MarkerSight.Shown, AltMarkerMath.Sight(yours, true, 1920, 1080));
        var ally = Marker(1, 2, 960, 400);
        Assert.Equal(MarkerSight.Shown, AltMarkerMath.Sight(ally, false, 1920, 1080)); // allies count with yours

        var enemy = Marker(2, 0, 960, 400);
        Assert.Equal(MarkerSight.Shown, AltMarkerMath.Sight(enemy, true, 1920, 1080));
        Assert.Equal(MarkerSight.EnemyOff, AltMarkerMath.Sight(enemy, false, 1920, 1080));

        Assert.Equal(MarkerSight.NoStats, AltMarkerMath.Sight(Marker(0, 1, 960, 400, stats: false), true, 1920, 1080));

        var faded = yours;
        faded.Alpha = 0.05f;
        Assert.Equal(MarkerSight.Faded, AltMarkerMath.Sight(faded, true, 1920, 1080));
        faded.Alpha = 0.06f;
        Assert.Equal(MarkerSight.Shown, AltMarkerMath.Sight(faded, true, 1920, 1080));

        var fresh = yours;
        fresh.Width = fresh.Height = 0;
        Assert.Equal(MarkerSight.NotLaidOut, AltMarkerMath.Sight(fresh, true, 1920, 1080));

        var behind = yours;
        behind.WSign = -1;
        Assert.Equal(MarkerSight.Behind, AltMarkerMath.Sight(behind, true, 1920, 1080));
    }

    [Fact]
    public void A_targeted_marker_off_the_screen_is_pinned_on_it_shown()
    {
        var m = Marker(2, 0, 2100, 400);
        m.Targeting = true;
        Assert.Equal(MarkerSight.Pinned, AltMarkerMath.Sight(m, true, 1920, 1080));
        m.WSign = -1;
        Assert.Equal(MarkerSight.Pinned, AltMarkerMath.Sight(m, true, 1920, 1080)); // vanilla pins it behind the camera too
        m.PointX = 960;
        m.WSign = 1;
        Assert.Equal(MarkerSight.Shown, AltMarkerMath.Sight(m, true, 1920, 1080));
        m.Targeting = false;
        m.PointX = 2100;
        Assert.Equal(MarkerSight.Shown, AltMarkerMath.Sight(m, true, 1920, 1080)); // untargeted: drawn off-screen, harmless
    }

    [Fact]
    public void Fully_on_screen_is_vanillas_own_test()
    {
        var m = Marker(0, 0, 960, 400);
        Assert.True(AltMarkerMath.FullyOnScreen(m, 1920, 1080));
        m.PointX = 29; // left edge at -1
        Assert.False(AltMarkerMath.FullyOnScreen(m, 1920, 1080));
        m.PointX = 960;
        m.PointY = 1030; // bottom at 1084
        Assert.False(AltMarkerMath.FullyOnScreen(m, 1920, 1080));
        m.PointY = 400;
        m.WSign = -1;
        Assert.False(AltMarkerMath.FullyOnScreen(m, 1920, 1080));
    }

    // ------------------------------------------------------------------ placement

    [Fact]
    public void The_label_goes_under_the_marker_centred_at_any_scale()
    {
        var layout = new AltMarkerLayout(13, 2, 60, 3);
        var m = Marker(0, 0, 960, 400);
        AltMarkerMath.Place(m, MarkerSight.Shown, layout, 1f, out float x, out float y, out float w);
        Assert.Equal((860f, 456f, 200f), (x, y, w)); // 400 + 108/2 + 2
        // 2560 x 1440 (UI scale 4/3): the widget is bigger, the gap and the box scale
        m.Width = 80;
        m.Height = 144;
        AltMarkerMath.Place(m, MarkerSight.Shown, layout, 4 / 3f, out x, out y, out w);
        Assert.Equal(960f - 400f / 3f, x, 3);
        Assert.Equal(400f + 72f + 8f / 3f, y, 3);
        Assert.Equal(800f / 3f, w, 3);
        // a negative gap lifts it over the marker's bottom
        AltMarkerMath.Place(Marker(0, 0, 960, 400), MarkerSight.Shown, new AltMarkerLayout(13, -20, 60, 3), 1f, out _, out y, out _);
        Assert.Equal(434f, y);
    }

    [Fact]
    public void A_pinned_label_follows_the_widgets_own_offsets()
    {
        var m = Marker(2, 3, 2500, 400);
        m.Targeting = true;
        m.OffsetX = 1860;
        m.OffsetY = 300;
        AltMarkerMath.Place(m, MarkerSight.Pinned, new AltMarkerLayout(13, 2, 60, 0), 1f, out float x, out float y, out _);
        Assert.Equal((1790f, 410f), (x, y)); // centre 1890, bottom 408
    }

    [Fact]
    public void Vanillas_distance_fade()
    {
        Assert.Equal(0.7f, AltMarkerMath.VanillaAlpha(900));
        Assert.Equal(0.7f, AltMarkerMath.VanillaAlpha(500), 4);
        Assert.Equal(1f, AltMarkerMath.VanillaAlpha(10), 4);
        float mid = AltMarkerMath.VanillaAlpha(100);
        Assert.InRange(mid, 0.7f, 1f);
        Assert.Equal(0.5f, AltMarkerMath.VanillaAlpha(7.5f), 4);
        Assert.Equal(0f, AltMarkerMath.VanillaAlpha(5));
        Assert.Equal(0f, AltMarkerMath.VanillaAlpha(2));
    }

    // ------------------------------------------------------------------ layout, texts, keys

    [Fact]
    public void Layout_reads_the_settings_and_describes_itself()
    {
        var layout = AltMarkerLayout.From(TraxSettings.Shared);
        Assert.Equal((16, 2, 60, 3), (layout.TextSize, layout.Offset, layout.BarWidth, layout.BarHeight));
        Assert.True(layout.Bar);
        Assert.Equal("numbers 16 px, 2 px under the marker, bar 60 x 3 px (UI pixels - the game's UI scale applies)", layout.Describe());
        Assert.Equal("numbers 13 px, -5 px under the marker, no bar (AltMarkerBarHeight 0) (UI pixels - the game's UI scale applies)",
            new AltMarkerLayout(13, -5, 60, 0).Describe());
    }

    [Fact]
    public void Keys_names_and_log_texts()
    {
        Assert.Equal(0, AltMarkerMath.Key(0, 0));
        Assert.Equal(19, AltMarkerMath.Key(1, 3));
        Assert.Equal("yours", AltMarkerMath.TeamName(0));
        Assert.Equal("ally", AltMarkerMath.TeamName(1));
        Assert.Equal("enemy", AltMarkerMath.TeamName(2));
        var m = Marker(2, 0, 812.4f, 430.6f);
        Assert.Equal("enemy 1 Infantry (40 men) at (812, 431) 60 x 108, 80 m", AltMarkerMath.DescribeMarker(m));
        m.LiveSize = false;
        Assert.Equal("enemy 1 Infantry (40 men) at (812, 431) 60 x 108 (nominal), 80 m", AltMarkerMath.DescribeMarker(m));
        OrderStripMath.Numbers(Tired, true, 1.0, out int mean, out int spread);
        Assert.Equal("enemy 1 Infantry 72% ± 8 HP 81% (40 men, f 0.90)", AltMarkerMath.DescribeValues(m, mean, spread, 81));
        Assert.Equal("behind the camera", AltMarkerMath.SightName(MarkerSight.Behind));
        Assert.StartsWith("the game's own formation markers read live", AltMarkerMath.TechniqueName(AltMarkerTechnique.Live));
    }

    // ------------------------------------------------------------------ the summary

    [Fact]
    public void Summary_when_the_markers_never_came_up()
    {
        var s = new AltMarkerStats();
        Assert.Equal("hud: ALT markers - shown 0x (vanilla's formation markers never came up with the labels on - ALT never held or the orders menu never opened in a fight); errors 0",
            s.SummaryLine(0));
    }

    [Fact]
    public void Summary_counts_shows_time_formations_per_side_and_fallbacks()
    {
        var s = new AltMarkerStats { Technique = AltMarkerMath.TechniqueName(AltMarkerTechnique.Live) };
        s.NoteShown();
        s.NoteTechnique(AltMarkerTechnique.Live);
        s.AddOnScreen(1.25);
        s.AddOnScreen(-1); // ignored
        Assert.True(s.NoteLabelled(AltMarkerMath.Key(0, 0), 0));
        Assert.False(s.NoteLabelled(AltMarkerMath.Key(0, 0), 0)); // once per formation
        s.NoteLabelled(AltMarkerMath.Key(0, 1), 0);
        s.NoteLabelled(AltMarkerMath.Key(2, 0), 1);
        s.NoteLabelled(AltMarkerMath.Key(1, 0), 2);
        s.NoteFrame(4, 0, 0);
        s.NoteFrame(3, 1, 0);
        s.NoteValuesPushed();
        Assert.Equal("hud: ALT markers - shown 1x, on screen 1.3 s; technique: the game's own formation markers read live (their points and their widgets' sizes); "
                     + "formations labelled: yours 2, allies 1, enemy 1 (most at once 4), frames with a label pinned at the screen's edge 1, values pushed 1; fallbacks: none; errors 0",
            s.SummaryLine(0));

        s.NoteShown();
        s.NoteTechnique(AltMarkerTechnique.OwnProjection);
        s.NoteFallback(AltMarkerFallback.NoLayer, "no MissionFormationMarker layer on the screen", 42.0);
        s.NoteFallback(AltMarkerFallback.Unpaired, "later", 50.0);
        s.NoteFrame(2, 0, 3);
        string line = s.SummaryLine(1);
        Assert.Contains("(shows: live 1, the game's points + a nominal size 0, own projection 1)", line);
        Assert.Contains(", markers beyond 64 dropped (most) 3", line);
        Assert.EndsWith("; fallbacks 2 (no marker layer 1, a marker without its widget 1) - the first at 42.0 s: no MissionFormationMarker layer on the screen; errors 1", line);
        Assert.Equal(1, s.Fallbacks(AltMarkerFallback.NoLayer));
        Assert.Equal(0, s.Fallbacks(AltMarkerFallback.NoWidgets));
    }
}

using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 9 - the orders-menu strip's pure logic: which card set is drawn and whether slot k is
/// formation k (vanilla's two layouts, RTS Camera Command System's one), where a cell goes at any
/// scale (and lifted at the screen's edge), what it says, the ± band, the average health in the
/// formation stats, and the [summary] line.
/// </summary>
public class OrderStripTests
{
    // The vanilla keyboard layout at 1920 x 1080: slot k's card top-left (UI scale 1).
    private static readonly (float X, float Y)[] VanillaColumns =
    {
        (20, 44), (20, 307), (20, 570), (20, 833), (1769, 44), (1769, 307), (1769, 570), (1769, 833),
    };

    /// <summary>Formations 0..7: the given member counts (0 = empty), stats for those with men.</summary>
    private static StripFormation[] Formations(params int[] members)
    {
        var f = new StripFormation[8];
        for (int k = 0; k < 8; k++)
        {
            int n = k < members.Length ? members[k] : 0;
            f[k] = new StripFormation
            {
                Exists = true,
                Members = n,
                Stats = n > 0 ? new FormationAthleticsStats(n, 72, 8, 0.72, 0.08, 0, 0.9, 1, 0.81) : default,
            };
        }
        return f;
    }

    /// <summary>A frame of <paramref name="sets"/> sets; set <paramref name="drawn"/> shows the cards
    /// of the formations with men, counting them.</summary>
    private static OrderCardFrame Cards(int sets, int drawn, StripFormation[] formations, float scale = 1f)
    {
        var frame = new OrderCardFrame { Count = sets * 8, ScreenWidth = 1920 * scale, ScreenHeight = 1080 * scale, Scale = scale };
        for (int s = 0; s < sets; s++)
            for (int k = 0; k < 8; k++)
            {
                bool show = s == drawn && formations[k].Members > 0;
                frame.Cards[s * 8 + k] = new OrderCard
                {
                    Visible = show,
                    X = VanillaColumns[k].X * scale,
                    Y = VanillaColumns[k].Y * scale,
                    Width = 131 * scale,
                    Height = 223 * scale,
                    Members = show ? formations[k].Members : 0,
                };
            }
        return frame;
    }

    [Fact]
    public void Vanilla_keyboard_layout_aligns_on_the_first_set()
    {
        var f = Formations(40, 20, 12);
        var m = OrderStripMath.Match(Cards(2, 0, f), f);
        Assert.Equal(StripAlignment.Aligned, m.Result);
        Assert.Equal(0, m.Set);
        Assert.Equal(2, m.Sets);
        Assert.Equal(3, m.VisibleCards);
        Assert.Equal(StripFallback.None, m.Fallback);
        Assert.StartsWith("aligned: set 1 of 2, 3 cards drawn", m.Describe());
    }

    [Fact]
    public void Gamepad_row_aligns_on_the_second_set_and_rts_camera_on_its_only_set()
    {
        var f = Formations(40, 0, 12, 0, 0, 0, 0, 9);
        var row = OrderStripMath.Match(Cards(2, 1, f), f);
        Assert.Equal(StripAlignment.Aligned, row.Result);
        Assert.Equal(1, row.Set);
        var rts = OrderStripMath.Match(Cards(1, 0, f), f);
        Assert.Equal(StripAlignment.Aligned, rts.Result);
        Assert.Equal(0, rts.Set);
        Assert.Equal(1, rts.Sets);
    }

    [Fact]
    public void The_player_joining_or_leaving_a_formation_is_tolerated_but_not_more()
    {
        var f = Formations(40, 20);
        var frame = Cards(2, 0, f);
        frame.Cards[0].Members = 39; // the player is in Infantry: the card counts one fewer
        Assert.Equal(StripAlignment.Aligned, OrderStripMath.Match(frame, f).Result);
        frame.Cards[1].Members = 25; // Archers' card says 25, the formation has 20
        var m = OrderStripMath.Match(frame, f);
        Assert.Equal(StripAlignment.Mismatch, m.Result);
        Assert.Equal(StripIssue.MembersDisagree, m.Issue);
        Assert.Equal(1, m.Slot);
        Assert.Equal(StripFallback.Mismatch, m.Fallback);
        Assert.Equal("2 Archers's card counts 25 men, the formation has 20", m.Describe());
    }

    [Fact]
    public void Swapped_cards_are_caught_by_their_counts()
    {
        // An order-menu mod that drew Archers in slot 0 and Infantry in slot 1: the counts disagree.
        var f = Formations(40, 20);
        var frame = Cards(1, 0, f);
        frame.Cards[0].Members = 20;
        frame.Cards[1].Members = 40;
        var m = OrderStripMath.Match(frame, f);
        Assert.Equal(StripIssue.MembersDisagree, m.Issue);
        Assert.Equal(0, m.Slot);
    }

    [Fact]
    public void A_formation_with_men_but_no_card_drawn_is_a_mismatch_an_emptied_one_is_not()
    {
        var f = Formations(40, 20, 12);
        var frame = Cards(2, 0, f);
        frame.Cards[2].Visible = false;
        var m = OrderStripMath.Match(frame, f);
        Assert.Equal(StripIssue.CardHidden, m.Issue);
        Assert.Equal("3 Cavalry has 12 men but its card is not drawn", m.Describe());

        // vanilla keeps the card of a formation that lost all its men: count 0 on both sides
        var emptied = Formations(40, 0, 12);
        var cards = Cards(2, 0, Formations(40, 20, 12));
        cards.Cards[1].Members = 0;
        Assert.Equal(StripAlignment.Aligned, OrderStripMath.Match(cards, emptied).Result);
    }

    [Fact]
    public void Not_yet_drawn_or_laid_out_waits_and_broken_card_sets_are_problems()
    {
        var f = Formations(40, 20);
        var closed = Cards(2, -1, f);
        var m = OrderStripMath.Match(closed, f);
        Assert.Equal(StripAlignment.NotYet, m.Result);
        Assert.Equal(StripIssue.NoneVisible, m.Issue);
        Assert.Equal(StripFallback.NeverShown, m.Fallback);

        var first = Cards(2, 0, f);
        first.Cards[0].Width = 0; // drawn, not laid out yet (a first open)
        m = OrderStripMath.Match(first, f);
        Assert.Equal(StripIssue.NotLaidOut, m.Issue);
        Assert.Equal(StripAlignment.NotYet, m.Result);

        Assert.Equal(StripIssue.NoCards, OrderStripMath.Match(new OrderCardFrame(), f).Issue);
        var twelve = Cards(2, 0, f);
        twelve.Count = 12;
        m = OrderStripMath.Match(twelve, f);
        Assert.Equal(StripAlignment.Problem, m.Result);
        Assert.Equal(StripIssue.NotWholeSets, m.Issue);
        Assert.Equal("12 cards found - not whole sets of 8 (an order-menu mod with its own cards?)", m.Describe());

        var both = Cards(2, 0, f);
        both.Cards[8] = both.Cards[0];
        m = OrderStripMath.Match(both, f);
        Assert.Equal(StripIssue.TwoSetsVisible, m.Issue);
        Assert.Equal(StripFallback.TwoSetsVisible, m.Fallback);
    }

    [Fact]
    public void The_default_cell_sits_under_the_card_and_fits_the_1080p_bottom_card_exactly()
    {
        var layout = new StripLayout(13, 1, 20, 4, 2);
        Assert.Equal(1f, layout.Top);
        Assert.Equal(0f, layout.TextMarginTop);
        Assert.Equal(19f, layout.BarMarginTop);
        Assert.Equal(23f, layout.Depth, 3); // max(1 + 15.6, 20 + 4) - 1
        Assert.Contains("numbers 13 px at card bottom +1, bar 4 px at +20, 2 px in from the sides (23 px deep", layout.Describe());

        var card = new OrderCard { Visible = true, X = 20, Y = 307, Width = 131, Height = 223 };
        OrderStripMath.PlaceCell(card, layout, 1f, 1080, out float x, out float y, out float w, out bool lifted);
        Assert.Equal((20f, 531f, 131f, false), (x, y, w, lifted));

        // the bottom card of a vanilla column at 1080p ends at 1056: the cell ends at 1080 - no lift
        card.Y = 833;
        OrderStripMath.PlaceCell(card, layout, 1f, 1080, out _, out y, out _, out lifted);
        Assert.Equal(1057f, y);
        Assert.False(lifted);
    }

    [Fact]
    public void Cells_follow_the_ui_scale_and_are_lifted_at_the_screen_edge()
    {
        var layout = new StripLayout(13, 1, 20, 4, 2);
        // 2560 x 1440: scale 4/3 - everything in pixels, the offsets scaled
        var card = new OrderCard { Visible = true, X = 20 * 4 / 3f, Y = 833 * 4 / 3f, Width = 131 * 4 / 3f, Height = 223 * 4 / 3f };
        OrderStripMath.PlaceCell(card, layout, 4 / 3f, 1440, out float x, out float y, out float w, out bool lifted);
        Assert.Equal(26.667f, x, 2);
        Assert.Equal(174.667f, w, 2);
        Assert.Equal(1057 * 4 / 3f, y, 2);
        Assert.False(lifted);

        // a bigger bar offset would leave the screen: lifted to the edge
        var deep = new StripLayout(13, 1, 30, 4, 2);
        card = new OrderCard { Visible = true, X = 20, Y = 833, Width = 131, Height = 223 };
        OrderStripMath.PlaceCell(card, deep, 1f, 1080, out _, out y, out _, out lifted);
        Assert.True(lifted);
        Assert.Equal(1080f - deep.Depth, y, 3);

        // numbers moved INTO the card (negative offset): the cell starts there, no negative margin
        var inside = new StripLayout(13, -16, 20, 4, 2);
        Assert.Equal(-16f, inside.Top);
        Assert.Equal(0f, inside.TextMarginTop);
        Assert.Equal(36f, inside.BarMarginTop);
        OrderStripMath.PlaceCell(new OrderCard { Y = 44, Height = 223 }, inside, 1f, 1080, out _, out y, out _, out _);
        Assert.Equal(251f, y);
    }

    [Fact]
    public void Numbers_and_texts()
    {
        var s = new FormationAthleticsStats(40, 90, 10, 0.72, 0.08, 0, 0.9, 5, 0.81);
        OrderStripMath.Numbers(s, true, 1.0, out int mean, out int spread);
        Assert.Equal((72, 8), (mean, spread));
        Assert.Equal("72% ± 8", OrderStripMath.AthleticsText(mean, spread));
        OrderStripMath.Numbers(s, true, 2.0, out _, out spread);
        Assert.Equal(16, spread); // the band's half-width: 2 std
        OrderStripMath.Numbers(s, false, 1.0, out _, out spread);
        Assert.Equal("72%", OrderStripMath.AthleticsText(72, spread));
        var one = new FormationAthleticsStats(1, 50, 0, 0.5, 0, 0, 0.66, 0, 1.0);
        OrderStripMath.Numbers(one, true, 1.0, out mean, out spread);
        Assert.Equal("50%", OrderStripMath.AthleticsText(mean, spread)); // one man: no spread

        Assert.Equal(81, OrderStripMath.HealthPercent(s.MeanHealth));
        Assert.Equal("HP 81%", OrderStripMath.HealthText(81));
        Assert.Equal(-1, OrderStripMath.HealthPercent(double.NaN));
        Assert.Equal(string.Empty, OrderStripMath.HealthText(-1));
        Assert.Equal("1 Infantry", OrderStripMath.FormationName(0));
        Assert.Equal("8 Heavy cavalry", OrderStripMath.FormationName(7));
        Assert.Equal("1 Infantry 72% ± 8 HP 81% (40 men, f 0.90)", OrderStripMath.DescribeValues(0, s, 72, 8, 81));
        Assert.Equal("1 Infantry at (20, 833) 131 x 223, 40 men", OrderStripMath.DescribeCard(0, new OrderCard { X = 20, Y = 833, Width = 131, Height = 223, Members = 40 }));
    }

    [Fact]
    public void The_band_is_mean_plus_minus_k_std_clamped_or_absent()
    {
        var s = new FormationAthleticsStats(40, 90, 10, 0.72, 0.08, 0, 0.9, 5, 0.81);
        OrderStripMath.Band(s, true, 1.0, out float low, out float high);
        Assert.Equal(0.64f, low, 4);
        Assert.Equal(0.80f, high, 4);
        OrderStripMath.Band(s, true, 3.0, out low, out high);
        Assert.Equal(0.48f, low, 4);
        Assert.Equal(0.96f, high, 4);
        var wide = new FormationAthleticsStats(40, 90, 10, 0.9, 0.3, 0, 0.9, 5, 0.81);
        OrderStripMath.Band(wide, true, 1.0, out low, out high);
        Assert.Equal((0.6f, 1f), (low, high));
        OrderStripMath.Band(s, false, 1.0, out low, out high);
        Assert.Equal((0.72f, 0.72f), (low, high));
        OrderStripMath.Band(s, true, 0.0, out low, out high);
        Assert.Equal(low, high);
    }

    [Fact]
    public void The_signature_moves_with_the_cards_only()
    {
        var f = Formations(40, 20);
        var frame = Cards(2, 0, f);
        int a = OrderStripMath.Signature(frame, 0);
        Assert.Equal(a, OrderStripMath.Signature(Cards(2, 0, f), 0));
        frame.Cards[1].Y += 263;
        Assert.NotEqual(a, OrderStripMath.Signature(frame, 0));
        Assert.NotEqual(a, OrderStripMath.Signature(Cards(2, 0, Formations(40, 20, 5)), 0));
    }

    [Fact]
    public void Formation_stats_carry_the_average_health_and_the_summary_names_it()
    {
        var points = new MeanStd();
        var fractions = new MeanStd();
        var shares = new MeanStd();
        var health = new MeanStd();
        foreach (var (p, h) in new[] { (64.0, 1.0), (80.0, 0.62) })
        {
            points.Add(p);
            fractions.Add(p / 100);
            shares.Add(Math.Min(1, p / 75));
            health.Add(h);
        }
        var st = FormationAthleticsStats.From(points, fractions, shares, health, 0, 1);
        Assert.Equal(0.81, st.MeanHealth, 9);
        Assert.Equal(2, st.Count);
        Assert.True(double.IsNaN(FormationAthleticsStats.From(points, fractions, 0).MeanHealth));

        var lines = new AthleticsStats().SummaryLines(AthleticsRules.From(new TraxSettings()), c => c.ToString(),
            new List<KeyValuePair<string, FormationAthleticsStats>> { new("1 Infantry", st) });
        Assert.Contains("Athletics your formations at the end: 1 Infantry 72 ± 8 (2 men) f avg 0.93, 1 at full strength, health avg 81%", lines);
    }

    [Fact]
    public void The_summary_line_counts_opens_techniques_and_fallbacks()
    {
        var s = new OrderStripStats();
        Assert.Equal("hud: orders strip - opened 0x (the orders menu was never opened with the strip on)", s.SummaryLine());
        s.NoteOpen();
        s.NoteUnderCards();
        s.Technique = "the live vanilla cards (layer MissionOrder)";
        Assert.True(s.NoteLayout("16 cards in 2 sets, set 1 drawn"));
        Assert.False(s.NoteLayout("16 cards in 2 sets, set 1 drawn"));
        s.NoteCells(3, 0);
        s.NoteValuesPushed();
        Assert.Equal("hud: orders strip - opened 1x: under the cards in 1, the compact panel in 0; technique: the live vanilla cards (layer MissionOrder); "
                     + "card layouts seen: 16 cards in 2 sets, set 1 drawn; cells placed 3 (lifted to the screen's edge 0), card changes 0, short mismatches 0 (under 1.0 s), "
                     + "card re-scans 0, values pushed 1; fallbacks: none", s.SummaryLine());
        s.NoteOpen();
        s.NoteFallback(StripFallback.Mismatch, "2 Archers's card counts 25 men, the formation has 20", 12.34);
        s.NoteOpen();
        s.NoteFallback(StripFallback.SwitchedOff, "OrderStripUnderCards off", 20);
        Assert.Equal(1, s.Fallbacks(StripFallback.Mismatch));
        Assert.EndsWith("; fallbacks 2 (OrderStripUnderCards off 1, a card disagreed with its formation 1) - the first at 12.3 s: 2 Archers's card counts 25 men, the formation has 20",
            s.SummaryLine());
        Assert.Contains("under the cards in 1, the compact panel in 2", s.SummaryLine());
    }
}

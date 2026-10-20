using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 73 - LINE DISCIPLINE BY TIER (Toni 2026-10-20: 60 tier-5 men against 120 tier-1 recruits - the recruits came on a very broad front
/// about 2 deep and engulfed him; "a low tier untrained troop cannot keep that disciplined line"; AI_NOTES "Step 73", DESIGN decision 67):
/// the settings, the master switch first, the mean depth, the width math with fractions, Toni's examples, which formations count, the
/// summary lines.
/// </summary>
public class Step73Tests
{
    private static LineDisciplineRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return LineDisciplineRules.From(s);
    }

    private static readonly ParamDef[] Advanced =
    {
        SettingsSchema.LineDisciplineEnabled, SettingsSchema.LineDisciplineRanksT0, SettingsSchema.LineDisciplineRanksT1, SettingsSchema.LineDisciplineRanksT2,
        SettingsSchema.LineDisciplineRanksT3, SettingsSchema.LineDisciplineRanksT4, SettingsSchema.LineDisciplineRanksT5, SettingsSchema.LineDisciplineRanksT6,
        SettingsSchema.LineDisciplineHeroTier, SettingsSchema.LineDisciplineRangedToo, SettingsSchema.LineDisciplineInSieges, SettingsSchema.LineDisciplinePreviewCap,
        SettingsSchema.LineDisciplineShowMessage, SettingsSchema.LineDisciplinePollSeconds,
    };

    /// <summary>The mean of the map over a set of men: (tier, count) pairs.</summary>
    private static double Depth(in LineDisciplineRules r, params (int Tier, int Men)[] men)
    {
        double sum = 0;
        int n = 0;
        foreach (var (tier, count) in men)
        {
            sum += r.RanksOf(LineDisciplineMath.TierOf(tier, false, in r)) * count;
            n += count;
        }
        return LineDisciplineMath.MinDepth(sum, n);
    }

    /// <summary>Vanilla's FormOrder Wide (FormOrder.GetMaxFileCountAux): round(√(men ÷ 16)) × 16 files, at least 16.</summary>
    private static int VanillaWideFiles(int men) => Math.Max((int)Math.Round(Math.Sqrt(men / 16.0), MidpointRounding.ToEven), 1) * 16;

    [Fact]
    public void The_settings_the_player_switch_on_the_player_page_the_rest_in_their_own_Advanced_group_before_Advanced_all_live()
    {
        var all = SettingsSchema.All.ToList();
        Assert.Equal(all.IndexOf(SettingsSchema.CavalryFlockEnabled) + 1, all.IndexOf(SettingsSchema.LineDisciplineForPlayer));
        Assert.Same(SettingsSchema.PlayerAiGroup, SettingsSchema.LineDisciplineForPlayer.Group);
        Assert.Contains(SettingsSchema.LineDisciplineForPlayer, SettingsSchema.PlayerPage);
        int first = all.IndexOf(SettingsSchema.OrderChargeIconPixels) + 1;
        for (int i = 0; i < Advanced.Length; i++)
        {
            Assert.Equal(first + i, all.IndexOf(Advanced[i]));
            Assert.Same(SettingsSchema.LineDisciplineGroup, Advanced[i].Group);
            Assert.Equal(SettingsPage.Advanced, Advanced[i].Group.Page);
            Assert.Equal(ApplyTiming.Live, Advanced[i].Timing);
            Assert.DoesNotContain(Advanced[i], SettingsSchema.PlayerPage);
        }
        var groups = SettingsSchema.Groups.ToList();
        Assert.Equal(groups.Count - 2, groups.IndexOf(SettingsSchema.LineDisciplineGroup)); // right before "Advanced"

        var r = Rules();
        Assert.True(r.On);
        Assert.True(r.PlayerOn);
        Assert.Equal(new[] { 6.0, 5, 4, 3, 2, 1, 1 }, Enumerable.Range(0, 7).Select(t => r.RanksOf(t)));
        Assert.Equal(6, r.HeroTier);
        Assert.True(r.RangedToo);
        Assert.False(r.InSieges);
        Assert.True(r.PreviewCap);
        Assert.True(r.ShowMessage);
        Assert.Equal(1.0, r.PollSeconds, 6);
        Assert.Equal("T0 6.0 / T1 5.0 / T2 4.0 / T3 3.0 / T4 2.0 / T5 1.0 / T6 1.0", r.MapText());
    }

    [Fact]
    public void The_master_switch_comes_first_then_the_system_then_the_players_own()
    {
        var f = new LineFacts { Men = 120, Shape = LineShape.Line, PlayerOrdered = true };
        var off = Rules(s => s.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm));
        Assert.False(off.On);
        Assert.False(off.PlayerOn);
        Assert.Equal("ModEnabled", off.OffBecause);
        Assert.Equal(LineSkip.ModOff, LineDisciplineMath.Verdict(in off, LineMission.Naval, in f)); // before anything else
        var sys = Rules(s => s.Set(SettingsSchema.LineDisciplineEnabled, false, SettingSources.Mcm));
        Assert.Equal("LineDisciplineEnabled", sys.OffBecause);
        Assert.False(sys.PlayerOn);
        Assert.Equal(LineSkip.SystemOff, LineDisciplineMath.Verdict(in sys, LineMission.Field, in f));
        var mine = Rules(s => s.Set(SettingsSchema.LineDisciplineForPlayer, false, SettingSources.Mcm));
        Assert.True(mine.On);
        Assert.False(mine.PlayerOn);
        Assert.Equal(LineSkip.PlayerOff, LineDisciplineMath.Verdict(in mine, LineMission.Field, in f));
        var ai = f;
        ai.PlayerOrdered = false;
        Assert.Equal(LineSkip.None, LineDisciplineMath.Verdict(in mine, LineMission.Field, in ai)); // the AI keeps to it
        Assert.Contains("(step 73)", Rules().Describe());
        Assert.Contains("- ON for the AI; your own formations too (the order drag's preview stops at the cap", Rules().Describe());
        Assert.Contains("- OFF (ModEnabled): the game's own widths", off.Describe());
        Assert.Contains("NOT (LineDisciplineForPlayer off)", mine.Describe());
    }

    [Fact]
    public void Which_formations_count_infantry_and_ranged_lines_not_cavalry_squares_circles_columns_wedges_naval_or_sieges_by_default()
    {
        var r = Rules();
        LineFacts F(LineShape shape, bool mounted = false, bool ranged = false, int men = 50) => new LineFacts { Men = men, Shape = shape, Mounted = mounted, Ranged = ranged };
        foreach (var shape in new[] { LineShape.Line, LineShape.ShieldWall, LineShape.Loose, LineShape.Scatter })
            Assert.Equal(LineSkip.None, LineDisciplineMath.Verdict(in r, LineMission.Field, F(shape)));
        foreach (var shape in new[] { LineShape.Square, LineShape.Circle, LineShape.Column, LineShape.Skein, LineShape.Other })
            Assert.Equal(LineSkip.Shape, LineDisciplineMath.Verdict(in r, LineMission.Field, F(shape)));
        Assert.Equal(LineSkip.Mounted, LineDisciplineMath.Verdict(in r, LineMission.Field, F(LineShape.Line, mounted: true)));
        Assert.Equal(LineSkip.None, LineDisciplineMath.Verdict(in r, LineMission.Field, F(LineShape.Loose, ranged: true)));
        var noRanged = Rules(s => s.Set(SettingsSchema.LineDisciplineRangedToo, false, SettingSources.Mcm));
        Assert.Equal(LineSkip.Ranged, LineDisciplineMath.Verdict(in noRanged, LineMission.Field, F(LineShape.Loose, ranged: true)));
        Assert.Equal(LineSkip.Empty, LineDisciplineMath.Verdict(in r, LineMission.Field, F(LineShape.Line, men: 0)));
        Assert.Equal(LineSkip.Naval, LineDisciplineMath.Verdict(in r, LineMission.Naval, F(LineShape.Line)));
        Assert.Equal(LineSkip.Siege, LineDisciplineMath.Verdict(in r, LineMission.Siege, F(LineShape.Line)));
        var sieges = Rules(s => s.Set(SettingsSchema.LineDisciplineInSieges, true, SettingSources.Mcm));
        Assert.Equal(LineSkip.None, LineDisciplineMath.Verdict(in sieges, LineMission.Siege, F(LineShape.Line)));
        Assert.Equal(LineSkip.Naval, LineDisciplineMath.Verdict(in sieges, LineMission.Naval, F(LineShape.Line))); // never naval
    }

    [Fact]
    public void A_mans_tier_clamps_to_0_6_and_a_hero_counts_as_the_hero_tier()
    {
        var r = Rules();
        Assert.Equal(0, LineDisciplineMath.TierOf(-1, false, in r));
        Assert.Equal(6, LineDisciplineMath.TierOf(7, false, in r));   // the game's battle tier goes to 7
        Assert.Equal(6, LineDisciplineMath.TierOf(12, false, in r));  // a mod's higher tier
        Assert.Equal(1, LineDisciplineMath.TierOf(1, false, in r));
        Assert.Equal(6, LineDisciplineMath.TierOf(1, true, in r));    // a level-6 companion holds like an elite
        var heroes2 = Rules(s => s.Set(SettingsSchema.LineDisciplineHeroTier, 2, SettingSources.Mcm));
        Assert.Equal(2, LineDisciplineMath.TierOf(6, true, in heroes2));
        Assert.Equal(4.0, heroes2.RanksOf(LineDisciplineMath.TierOf(6, true, in heroes2)), 9);
        var odd = new LineDisciplineRules(true, true, true, t0: 0, t1: 99, pollSeconds: 0);
        Assert.Equal(1, odd.T0);   // clamped into 1-12
        Assert.Equal(12, odd.T1);
        Assert.Equal(0.25, odd.PollSeconds, 9);
    }

    [Fact]
    public void The_minimum_depth_is_the_mean_of_the_mens_ranks()
    {
        var r = Rules();
        Assert.Equal(5.0, Depth(in r, (1, 120)), 9);                       // 120 recruits
        Assert.Equal(1.0, Depth(in r, (5, 60)), 9);                        // 60 veterans
        Assert.Equal(460.0 / 120, Depth(in r, (1, 70), (3, 30), (5, 20)), 9); // a mixed unit: 3.83
        Assert.Equal(3.5, Depth(in r, (2, 50), (3, 50)), 9);               // Toni's 3.5
        Assert.Equal(1.0, LineDisciplineMath.MinDepth(0, 0), 9);           // nobody: no limit
        Assert.Equal(1.0, LineDisciplineMath.MinDepth(0.5, 1), 9);         // never below one rank
    }

    [Fact]
    public void The_width_math_ceil_men_over_D_files_and_a_fraction_gives_a_partly_filled_last_rank()
    {
        // 120 recruits: D 5 → 24 files, 5 full ranks
        Assert.Equal(24, LineDisciplineMath.MaxFiles(120, 5));
        Assert.Equal(5, LineDisciplineMath.Ranks(120, 24));
        Assert.Equal(24, LineDisciplineMath.LastRankMen(120, 24));
        // 100 men at 3.5: 29 files, 3 full ranks + 13 - the 4th rank 45 % full (Toni: "the 4th line at most 1/2")
        Assert.Equal(29, LineDisciplineMath.MaxFiles(100, 3.5));
        Assert.Equal(4, LineDisciplineMath.Ranks(100, 29));
        Assert.Equal(13, LineDisciplineMath.LastRankMen(100, 29));
        Assert.InRange(LineDisciplineMath.LastRankMen(100, 29) / 29.0, 0.0, 0.5);
        // for every D the last rank's fill is at most D's fraction, and the depth men / files never exceeds D
        for (double d = 1; d <= 6; d += 0.25)
        for (int men = 1; men <= 300; men++)
        {
            int files = LineDisciplineMath.MaxFiles(men, d);
            Assert.True(LineDisciplineMath.Depth(men, files) <= d + 1e-9, men + " men at " + d);
            Assert.True(files == 1 || LineDisciplineMath.Depth(men, files - 1) > d - 1e-9, men + " men at " + d + ": not the narrowest");
        }
        // a hair under a whole number counts as it
        Assert.Equal(24, LineDisciplineMath.MaxFiles(120, 5.0000000001));
        Assert.Equal(25, LineDisciplineMath.MaxFiles(121, 5));
        // never below the game's own minimum (√men files) or 1
        Assert.Equal(2, LineDisciplineMath.MaxFiles(9, 6));
        Assert.Equal(3, LineDisciplineMath.GameMinFiles(9));
        Assert.Equal(3, LineDisciplineMath.CapFiles(9, 6));
        Assert.Equal(10, LineDisciplineMath.GameMinFiles(120));
        Assert.Equal(1, LineDisciplineMath.MaxFiles(0, 5));
        Assert.Equal(1, LineDisciplineMath.CapFiles(1, 6));
        Assert.Equal("29 files x 4 ranks (the last 13 of 29), 3.45 deep", LineDisciplineMath.ShapeText(100, 29));
    }

    [Fact]
    public void The_cap_width_is_the_games_own_and_the_game_floors_it_back_to_exactly_the_cap()
    {
        foreach (var (interval, diameter) in new[] { (0.2, 0.4), (0.6, 0.4), (1.2, 0.4), (0.0, 0.4), (3.5, 0.4) })
        {
            for (int files = 1; files <= 200; files++)
            {
                Assert.Equal(files, LineDisciplineMath.FilesOf(LineDisciplineMath.Width(files, interval, diameter), interval, diameter));
                Assert.Equal(files, LineDisciplineMath.FilesOf(LineDisciplineMath.CapWidth(files, interval, diameter), interval, diameter));
                Assert.Equal(files, LineDisciplineMath.FilesOf((float)LineDisciplineMath.CapWidth(files, interval, diameter), interval, diameter)); // the game's float
            }
        }
        Assert.Equal(0.4 + 23 * 1.0, LineDisciplineMath.Width(24, 0.6, 0.4), 9);
    }

    [Fact]
    public void Tonis_battle_the_recruits_wide_line_is_narrowed_his_veterans_never()
    {
        var r = Rules();
        // 120 tier-1 recruits under the AI's advance (FormOrder Wide): 48 files, 2.5 deep - wider than his 60 men
        int wide = VanillaWideFiles(120);
        Assert.Equal(48, wide);
        double d = Depth(in r, (1, 120));
        int cap = LineDisciplineMath.CapFiles(120, d);
        Assert.Equal(24, cap);
        Assert.True(wide > cap);                                            // clamped
        Assert.Equal("24 files x 5 ranks, 5.0 deep", LineDisciplineMath.ShapeText(120, cap));
        // his 60 tier-5 men: D 1 → 60 files - the widest the game ever makes them (Wide: 32) stands
        double dv = Depth(in r, (5, 60));
        int capV = LineDisciplineMath.CapFiles(60, dv);
        Assert.Equal(60, capV);
        Assert.True(VanillaWideFiles(60) <= capV);
        // the recruits lose 20 men: 100 at D 5 → 20 files (the 24-file line would be 4.2 deep) - auto-shrink
        Assert.Equal(20, LineDisciplineMath.CapFiles(100, d));
        Assert.True(LineDisciplineMath.Depth(100, 24) < d);
        // the mixed unit: 70 T1 + 30 T3 + 20 T5 → D 3.83 → 32 files, 3 ranks + 24
        double dm = Depth(in r, (1, 70), (3, 30), (5, 20));
        Assert.Equal(32, LineDisciplineMath.CapFiles(120, dm));
        Assert.Equal(24, LineDisciplineMath.LastRankMen(120, 32));
    }

    [Fact]
    public void The_summary_lines_say_each_side_the_clamps_the_orders_and_whether_the_preview_was_capped()
    {
        var st = new LineDisciplineStats();
        Assert.Contains("not installed", st.PreviewVerdict());
        st.Other.Counted = 2;
        st.Other.Capped = 1;
        st.Other.AiWidenClamps = 3;
        st.Other.AiAutoShrinks = 2;
        st.Other.Records.Add(new LineRecord
        {
            Name = "the enemy's Infantry", MenAtFirstCap = 120, DepthAtFirstCap = 5, CapFilesAtFirstCap = 24, CapWidthAtFirstCap = 23.45,
            WidestAskedFiles = 48, WidestAskedWidth = 47.4, Clamps = 5, AutoShrinks = 2, LastMen = 90, LastDepth = 5, LastCapFiles = 18,
        });
        st.PlayerLineOrders = 4;
        st.PlayerAskedWider = 2;
        st.PlayerOrderAtCap = 2;
        st.PreviewInstalls = 1;
        st.PreviewCapReads = 300;
        st.Ticks = 10;
        st.TickMs = 0.1;
        var lines = st.SummaryLines(Rules());
        Assert.Equal(4, lines.Count);
        Assert.StartsWith("line discipline (step 73): ON at the end - the map T0 6.0 / T1 5.0", lines[0]);
        Assert.Contains("cost avg 0.010 ms a tick", lines[0]);
        Assert.StartsWith("line discipline, your side (step 73): 0 formations counted, 0 capped | clamps applied", lines[1]);
        Assert.Contains("the other side (step 73): 2 formations counted, 1 capped - the enemy's Infantry: 120 men, D 5.0 → max 24 files 23.5 m vs asked up to 48 files 47.4 m, 5 clamps (2 auto-shrinks); at the end 90 men D 5.0 max 18 files", lines[2]);
        Assert.Contains("clamps applied: by the AI's own widening 3, on your orders 0 | auto-shrinks (losses / the depth changed): AI 2, yours 0 | errors 0", lines[2]);
        Assert.Contains("4 line orders (a click or a drag), 2 asked wider than the men hold → 2 came out at the cap", lines[3]);
        Assert.Contains("PREVIEW CAPPED (the drag stops growing at the cap)", lines[3]);
        Assert.Contains("OFF (ModEnabled)", st.SummaryLines(Rules(s => s.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm)))[0]);
    }
}

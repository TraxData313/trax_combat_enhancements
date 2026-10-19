using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 57 (Anton, 2026-10-18: "when they are broken and begin to rout, use their available sprint for the first get-away"; AI_NOTES "Step 57",
/// DESIGN decision 49): a routing AI man spends his WHOLE sprint store once (<see cref="SprintStore.TryStartRout"/> - no order reserve, no
/// start gate), behind <c>SprintOnRout</c>, the AI sprint, the sprint, Athletics and the master switch; the [summary] "sprint on rout" line.
/// </summary>
public class RoutSprintTests
{
    private static SprintRules Sprint(bool rout = true, bool ai = true, bool on = true, bool mod = true, bool athletics = true, int moveKeep = 80) =>
        new(mod, athletics, on, ai, 20, 20, 1.6, 1.1, 30, 7, 0.5, 0.5, 0, 50, moveKeep, 2, 1, rout);

    [Fact]
    public void The_setting_is_on_live_in_the_sprint_group_after_the_AI_reserves()
    {
        var keys = SettingsSchema.InGroup(SettingsSchema.SprintGroup).Select(p => p.Key).ToList();
        Assert.Equal(keys.IndexOf("SprintAiMoveKeepPercent") + 1, keys.IndexOf("SprintOnRout"));
        Assert.Equal(ApplyTiming.Live, SettingsSchema.SprintOnRout.Timing);
        var s = new TraxSettings();
        Assert.True(s.SprintOnRout);
        Assert.True(SprintRules.From(s).OnRout);
        Assert.True(SprintRules.From(s).RoutOn);
        Assert.Contains("a routing AI man sprints his WHOLE store once at the rout's start (SprintOnRout)", SprintRules.From(s).Describe("LeftControl"));
    }

    [Fact]
    public void A_routing_man_sprints_his_whole_store_once_with_no_reserve()
    {
        var s = Sprint();
        var sp = new SprintStore();
        Assert.Equal(SprintStart.Started, sp.TryStartRout(in s, 100, 6.0)); // a full store of 6 s (the first look clamps +inf to it)
        Assert.True(sp.Active && sp.Rout && !sp.Regroup && !sp.ByKey);
        Assert.Equal(6.0, sp.Length, 9);       // the WHOLE store - a move order would keep 80 %
        Assert.Equal(6.0, sp.StoredAtStart, 9);
        Assert.Equal(SprintOrder.None, sp.Order);
        // once: a second ask while it runs is refused
        Assert.Equal(SprintStart.Running, sp.TryStartRout(in s, 100.5, 6.0));
        // a half store: all of it, below SprintMinStartPercent's gate too
        var low = new SprintStore();
        low.Clamp(1.0); // 1 s stored of (later) a 6 s store = 17 %, below the 20 % start gate
        Assert.Equal(SprintStart.Started, low.TryStartRout(in s, 100, 6.0));
        Assert.Equal(1.0, low.Length, 9);
        // it drains to empty while he runs and ends there
        Assert.False(sp.Advance(3.0, true, out _));
        Assert.True(sp.Advance(3.0, true, out var end));
        Assert.Equal(SprintEnd.StoreEmpty, end);
        Assert.Equal(0, sp.Stored, 9);
    }

    [Fact]
    public void An_empty_store_or_no_store_is_refused_and_the_other_starts_clear_the_rout_mark()
    {
        var s = Sprint();
        var empty = new SprintStore();
        empty.Clamp(0.1); // less than AiMinSeconds 0.2
        Assert.Equal(SprintStart.BelowMinimum, empty.TryStartRout(in s, 100, 6.0));
        Assert.Equal(SprintStart.NoStore, new SprintStore().TryStartRout(in s, 100, 0));
        // an order sprint after a rout's is not a rout
        var sp = new SprintStore();
        sp.TryStartRout(in s, 100, 6.0);
        sp.Stop();
        var all = Sprint(moveKeep: 0);
        Assert.Equal(SprintStart.Started, sp.TryStartAi(in all, 101, 6.0, SprintOrder.Move));
        Assert.False(sp.Rout);
    }

    [Theory]
    [InlineData(false, true, true, true, true)]   // SprintOnRout off
    [InlineData(true, false, true, true, true)]   // SprintAiEnabled off
    [InlineData(true, true, false, true, true)]   // SprintEnabled off
    [InlineData(true, true, true, false, true)]   // the master switch off
    [InlineData(true, true, true, true, false)]   // AthleticsEnabled off
    public void Every_switch_holds_it_off_the_master_switch_first(bool rout, bool ai, bool on, bool mod, bool athletics)
    {
        var s = Sprint(rout, ai, on, mod, athletics);
        Assert.False(s.RoutOn);
        var sp = new SprintStore();
        Assert.Equal(SprintStart.Off, sp.TryStartRout(in s, 100, 6.0));
        Assert.False(sp.Active);
        if (!mod) Assert.Equal("ModEnabled", s.OffBecause);
    }

    [Fact]
    public void The_summary_counts_routs_sprints_and_the_refusals_by_reason()
    {
        var stats = new SprintStats();
        stats.RoutsSeen = 7;
        stats.AddRoutStart(6.0);
        stats.AddRoutStart(2.0);
        stats.RoutReplaced = 1;
        stats.AddRoutRefused(SprintStart.BelowMinimum);
        stats.AddRoutRefused(SprintStart.NoStore);
        stats.AddRoutRefused(SprintStart.Running);
        stats.AddRoutRefused(SprintStart.Mounted);
        stats.AddRoutRefused(SprintStart.Off);
        var lines = new List<string>();
        stats.AddSummaryLines(lines, Sprint(), "LeftControl");
        Assert.Equal(6, lines.Count); // step 64: + the into-contact line after it
        Assert.Equal("sprint on rout (step 57, SprintOnRout on): routs seen 7, sprints started 2 (whole store avg 4.00 s), of them taking over a running order sprint 1; "
                     + "refused: empty store 2, already sprinting 1, switched off 1, on a horse 1, yours 0", lines[4]);
        var off = new List<string>();
        new SprintStats().AddSummaryLines(off, Sprint(ai: false), "LeftControl");
        Assert.StartsWith("sprint on rout (step 57, SprintOnRout on - held off by SprintAiEnabled): routs seen 0, sprints started 0, of them", off[4]);
        Assert.Equal("handed to his rout's sprint", SprintMath.EndName(SprintEnd.Routed));
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 78c (Toni 2026-10-22, after playing 78b: "dont script for all of them to walk away or to push into each other, only per men rule, if tired - step
/// back and wait to recover ... if guy next to you rushes, there is a chance to make you go in too"; AI_NOTES "Step 78c", DESIGN decision 74) - EACH MAN
/// RESTS ON HIS OWN: the settings (the switch on the player page where the lull's two were, fifteen Advanced after the rotation's), the master switch first,
/// THE MARKS (wide spreads, by order, the wound cap), the start, THE CONTAGION CHANCE, THE WALK-BACK LIMIT, the attack hold, the lull's keys dropped from an
/// old config.json without a format change, the natural-gap diagnostic by side (it moves nobody), the summary lines.
/// </summary>
public class Step78cTests
{
    private static readonly ParamDef[] Advanced =
    {
        SettingsSchema.RestTiredMinPercent, SettingsSchema.RestTiredMaxPercent, SettingsSchema.RestReadyChargeMinPercent, SettingsSchema.RestReadyChargeMaxPercent,
        SettingsSchema.RestReadyAdvanceMinPercent, SettingsSchema.RestReadyAdvanceMaxPercent, SettingsSchema.RestReadyHoldMinPercent, SettingsSchema.RestReadyHoldMaxPercent,
        SettingsSchema.RestReachMetres, SettingsSchema.RestMaxBackMetres, SettingsSchema.RestDefendMetres, SettingsSchema.RestContagionMetres,
        SettingsSchema.RestContagionMaxPercent, SettingsSchema.RestContagionFloorPoints, SettingsSchema.RestGapDiagnosticMetres,
    };

    private static RestRules Rules(bool mod = true, bool athletics = true, bool rest = true) => new RestRules(mod, athletics, rest);

    [Fact]
    public void The_switch_replaces_the_lulls_on_the_player_page_and_the_numbers_follow_the_rotation()
    {
        var all = SettingsSchema.All.ToList();
        Assert.Equal(all.IndexOf(SettingsSchema.RotationEnabled) + 1, all.IndexOf(SettingsSchema.RestEnabled));
        Assert.Contains(SettingsSchema.RestEnabled, SettingsSchema.PlayerPage);
        Assert.Equal(1, SettingsSchema.RestEnabled.Default, 9);
        Assert.DoesNotMatch(@"[Ss]tep \d", SettingsSchema.RestEnabled.Description);
        int first = all.IndexOf(SettingsSchema.RotationRestSeconds) + 1;
        for (int i = 0; i < Advanced.Length; i++)
        {
            Assert.Equal(first + i, all.IndexOf(Advanced[i]));
            Assert.Same(SettingsSchema.CourageGroup, Advanced[i].Group);
            Assert.Equal(ApplyTiming.Live, Advanced[i].Timing);
        }
        Assert.DoesNotContain(all, p => p.Key.StartsWith("Lull", StringComparison.Ordinal));
        var r = RestRules.From(new TraxSettings());
        Assert.Equal(20, r.TiredMinPercent);
        Assert.Equal(45, r.TiredMaxPercent);
        Assert.Equal(55, r.ReadyChargeMinPercent);
        Assert.Equal(95, r.ReadyChargeMaxPercent);
        Assert.Equal(60, r.ReadyAdvanceMinPercent);
        Assert.Equal(65, r.ReadyHoldMinPercent);
        Assert.Equal(3, r.ReachMetres, 9);
        Assert.Equal(4, r.MaxBackMetres, 9);
        Assert.Equal(60, r.ContagionMaxPercent);
        Assert.Equal(10, r.ContagionFloorPoints);
        var s = new TraxSettings();
        s.Set(SettingsSchema.RestMaxBackMetres, 2.5, SettingSources.Mcm);
        Assert.Equal(2.5, RestRules.From(s).MaxBackMetres, 9);   // live
        Assert.Equal(30, new RestRules(true, true, true, tiredMinPercent: 30, tiredMaxPercent: 10).TiredMaxPercent); // a max below its min = the min
    }

    [Fact]
    public void The_master_switch_first_then_Athletics_then_its_own()
    {
        Assert.True(Rules().On);
        Assert.Equal("ModEnabled", Rules(mod: false, athletics: false, rest: false).OffBecause);
        Assert.Equal("AthleticsEnabled", Rules(athletics: false, rest: false).OffBecause);
        Assert.Equal("RestEnabled", Rules(rest: false).OffBecause);
        Assert.Contains("OFF (RestEnabled)", Rules(rest: false).Describe());
        Assert.Contains("never leaves it", Rules().Describe());
        Assert.Contains("no unit-wide rule", Rules().Describe());
    }

    [Fact]
    public void The_marks_spread_wide_by_order_and_the_wounds_cap_the_ready_mark()
    {
        var r = Rules();
        Assert.Equal(0.20, RestMath.TiredMark(in r, 0), 9);
        Assert.Equal(0.45, RestMath.TiredMark(in r, 1), 9);
        Assert.Equal(0.325, RestMath.TiredMark(in r, 0.5), 9);
        Assert.Equal(0.55, RestMath.ReadyMark(in r, BraceOrder.Charge, 0), 9);
        Assert.Equal(0.95, RestMath.ReadyMark(in r, BraceOrder.Charge, 1), 9);
        Assert.Equal(0.60, RestMath.ReadyMark(in r, BraceOrder.Advance, 0), 9);
        Assert.Equal(0.65, RestMath.ReadyMark(in r, BraceOrder.Hold, 0), 9);
        Assert.Equal(0.55, RestMath.ReadyMark(in r, BraceOrder.NoFormation, 0), 9); // no formation = a charge's
        Assert.Equal(0.75, RestMath.ReadyMark(in r, BraceOrder.Charge, 0.5), 9);
        // 1000 rolls: every mark inside its range and spread over it (men never move together)
        var dice = new Random(78);
        var tired = Enumerable.Range(0, 1000).Select(_ => RestMath.TiredMark(in r, dice.NextDouble())).ToList();
        Assert.All(tired, t => Assert.InRange(t, 0.20, 0.45));
        Assert.True(tired.Max() - tired.Min() > 0.22);
        // the wound cap: a man with 60% health cannot reach 95% - he goes in at 58%
        Assert.Equal(0.58, RestMath.CappedReady(0.95, 0.6), 9);
        Assert.Equal(0.7, RestMath.CappedReady(0.7, 1.0), 9);
        Assert.Equal(0.7, RestMath.CappedReady(0.7, double.NaN), 9);
    }

    [Fact]
    public void He_rests_below_his_tired_mark_at_the_fight_with_a_ready_mark_he_can_reach_and_goes_in_at_it()
    {
        Assert.True(RestMath.Starts(0.25, 0.30, 0.80, true));
        Assert.False(RestMath.Starts(0.25, 0.30, 0.80, false));    // not at the fight
        Assert.False(RestMath.Starts(0.30, 0.30, 0.80, true));     // at his mark, not below it
        Assert.False(RestMath.Starts(0.25, 0.30, 0.33, true));     // his wounds leave no room to recover: no rest
        Assert.False(RestMath.Starts(double.NaN, 0.30, 0.80, true));
        Assert.False(RestMath.Ready(0.79, 0.80));
        Assert.True(RestMath.Ready(0.80, 0.80));
    }

    [Fact]
    public void The_contagion_chance_grows_toward_his_mark_and_is_zero_below_the_floor()
    {
        var r = Rules(); // 60% at his mark, the floor his tired mark + 10 points
        Assert.Equal(0, RestMath.ContagionChance(in r, 0.25, 0.30, 0.80), 9);   // below his tired mark
        Assert.Equal(0, RestMath.ContagionChance(in r, 0.39, 0.30, 0.80), 9);   // below the floor (0.40)
        Assert.Equal(0, RestMath.ContagionChance(in r, 0.40, 0.30, 0.80), 9);   // at the floor
        Assert.Equal(0.30, RestMath.ContagionChance(in r, 0.60, 0.30, 0.80), 9); // half way
        Assert.Equal(0.60, RestMath.ContagionChance(in r, 0.80, 0.30, 0.80), 9); // at his mark
        Assert.Equal(0.60, RestMath.ContagionChance(in r, 0.90, 0.30, 0.80), 9); // never above the most
        double last = -1;
        for (double bar = 0.40; bar <= 0.80; bar += 0.05)
        {
            double p = RestMath.ContagionChance(in r, bar, 0.30, 0.80);
            Assert.True(p >= last - 1e-12);
            last = p;
        }
        Assert.Equal(0.6, RestMath.ContagionChance(in r, 0.42, 0.30, 0.38), 9); // a ready mark under the floor: the most once above the floor
        Assert.Equal(0, RestMath.ContagionChance(in r, double.NaN, 0.3, 0.8), 9);
        Assert.Equal(0, RestMath.ContagionChance(new RestRules(true, true, true, contagionMaxPercent: 0), 0.8, 0.3, 0.8), 9);
    }

    [Fact]
    public void The_walk_back_ends_out_of_reach_and_never_beyond_the_limit_from_his_start()
    {
        var r = Rules(); // out of reach at 3 m, at most 4 m from his start
        Assert.Equal(3.3 - 1.0, RestMath.WalkBack(in r, 1.0, 0), 9);   // an enemy 1 m off: back 2.3 m (to 3 m + a little)
        Assert.Equal(0, RestMath.WalkBack(in r, 3.0, 0), 9);           // out of reach: he stands
        Assert.Equal(0, RestMath.WalkBack(in r, 5.0, 1), 9);
        Assert.Equal(0, RestMath.WalkBack(in r, double.NaN, 0), 9);    // no enemy known: he stands
        Assert.Equal(1.0, RestMath.WalkBack(in r, 0.5, 3.0), 9);       // the enemy follows: only the 1 m left of his 4 m
        Assert.Equal(0, RestMath.WalkBack(in r, 0.5, 4.0), 9);         // at his limit: he stands and defends himself
        Assert.Equal(0, RestMath.WalkBack(in r, 0.5, 6.0), 9);
        // a fresh enemy chasing him step by step: his start-to-here never exceeds the limit
        double from = 0, enemy = 1.0;
        for (int i = 0; i < 100; i++)
        {
            double b = RestMath.WalkBack(in r, enemy, from);
            from += b;
            enemy = Math.Max(0.5, enemy + b - 1.5); // the enemy closes 1.5 m between two steers
            Assert.True(from <= r.MaxBackMetres + 1e-9, "walked " + from);
        }
        Assert.Equal(0, RestMath.WalkBack(new RestRules(true, true, true, maxBackMetres: 0), 0.5, 0), 9); // 0 = no walk at all
        // ... nor more than the limit behind his own front: a third-rank man 2 m behind it backs 2 m at most, a man 4 m behind it not at all
        Assert.Equal(2.0, RestMath.WalkBack(in r, 0.5, 0, 2.0), 9);
        Assert.Equal(1.5, RestMath.WalkBack(in r, 0.5, 0, 2.5), 9);
        Assert.Equal(0, RestMath.WalkBack(in r, 0.5, 0, 4.0), 9);
        // held back at the edge of reach (+ 1 m), free beyond it - he keeps up with his unit
        Assert.True(RestMath.HoldsBack(in r, 3.5));
        Assert.False(RestMath.HoldsBack(in r, 4.5));
        Assert.False(RestMath.HoldsBack(in r, double.NaN));
        Assert.True(RestMath.HoldsBack(in r, 2.0, 4.4));   // 4.4 m behind his front: still held
        Assert.False(RestMath.HoldsBack(in r, 2.0, 4.6));  // his line moved on: he follows it
        Assert.Equal(2.3, RestMath.WalkBack(in r, 1.0, 0, -1.0), 9);   // ahead of his front (in the enemy's ranks): the reach decides
    }

    [Fact]
    public void His_attacks_are_held_unless_an_enemy_is_in_his_defend_reach_and_always_in_a_wall()
    {
        var r = Rules(); // 2 m
        Assert.True(RestMath.HoldsAttacks(in r, false, 3.0));
        Assert.False(RestMath.HoldsAttacks(in r, false, 1.5));
        Assert.True(RestMath.HoldsAttacks(in r, false, double.NaN));
        Assert.True(RestMath.HoldsAttacks(in r, true, 1.0)); // in a wall slot he only guards
    }

    [Fact]
    public void The_lulls_keys_in_an_old_config_load_and_are_dropped_once_without_a_new_format()
    {
        Assert.Equal(22, ConfigFile.FormatVersion);          // 78c added no format (step 80 made it 18)
        Assert.Equal(29, ConfigFile.RetiredLullKeys.Length);
        foreach (int v in new[] { 16, 17 })
        {
            var read = ConfigFile.Read("{ \"ConfigVersion\": " + v + ", \"LullEnabled\": true, \"LullForPlayerUnits\": false, \"LullRecoverChargePercent\": 10, "
                                       + "\"LullGapMetres\": 3.5, \"RotationEnabled\": false, \"PressWrapMarginMetres\": 6.0 }");
            Assert.True(read.Ok);
            Assert.Equal(4, read.Unknown.Count);
            var notes = ConfigFile.Migrate(read, p => p.Default);
            Assert.Equal(4, notes.Count);
            Assert.All(notes, n => Assert.Contains("dropped (format " + v + " → 22; step 78c", n));
            Assert.Empty(read.Unknown);
            Assert.Equal(0, read.Values["RotationEnabled"], 9);   // the rest of the file untouched
            Assert.Equal(6, read.Values["PressWrapMarginMetres"], 9);
            Assert.Equal(4, ConfigFile.Migrate(read, p => p.Default).Count); // idempotent: no note added a second time
        }
        var other = ConfigFile.Read("{ \"ConfigVersion\": 17, \"SomethingElse\": 1 }");
        ConfigFile.Migrate(other, p => p.Default);
        Assert.Single(other.Unknown);                          // only the lull's keys are dropped
    }

    [Fact]
    public void The_natural_gap_is_counted_by_side_for_engaged_pairs_only_and_moves_nobody()
    {
        var t = new RhythmUnits();
        var sink = new RestStats();
        t.GapSink = sink;
        t.GapDiagnosticMetres = 2;
        int a = Step78Tests.A, b = Step78Tests.B;
        // in contact (the gap 1 m < 2 m): counted for both sides, no gap
        double now = Step78Tests.Run(t, 100, 2, 1.0, 0.5, 0.5, BraceOrderKind.Charge, BraceOrderKind.Charge, playerA: true);
        Assert.True(sink.GapLooks[1] >= 1 && sink.GapLooks[0] >= 1);
        Assert.Equal(0, sink.GapClear[0] + sink.GapClear[1]);
        Assert.Equal(1.0, t.Gap[a], 6);
        // both stepped back to a 3 m gap with nobody in contact: still engaged (contact within 10 s) - a 2 m+ gap counted
        long looks = sink.GapLooks[1];
        for (int i = 0; i < 4; i++)
        {
            now += 0.5;
            for (int k = 0; k < 3; k++)
                for (int f = 0; f < 10; f++)
                {
                    t.Note(a, f, -k, 0.5, false, -1, BraceOrderKind.Charge, true, false);
                    t.Note(b, f, 3 + k, 0.5, false, -1, BraceOrderKind.Charge, false, false);
                }
            if (t.Due(now)) t.Publish(now);
        }
        Assert.True(sink.GapLooks[1] > looks);
        Assert.True(sink.GapClear[1] >= 1 && sink.GapClear[0] >= 1);
        Assert.Equal(3.0, t.Gap[a], 6);
        // 30 s later with no contact: not engaged - nothing counted
        long before = sink.GapLooks[0] + sink.GapLooks[1];
        now += 30;
        for (int k = 0; k < 3; k++)
            for (int f = 0; f < 10; f++)
            {
                t.Note(a, f, -k, 0.5, false, b, BraceOrderKind.Charge, true, false);
                t.Note(b, f, 3 + k, 0.5, false, a, BraceOrderKind.Charge, false, false);
            }
        if (t.Due(now)) t.Publish(now);
        Assert.Equal(before, sink.GapLooks[0] + sink.GapLooks[1]);
    }

    [Fact]
    public void The_summary_lines()
    {
        var s = new RestStats();
        s.Started = 40;
        s.StartedInWall = 6;
        s.AddEnd(RestIn.OwnMark, 20, 2.0);
        s.AddEnd(RestIn.OwnMark, 30, 3.0);
        s.AddEnd(RestIn.Contagion, 10, 1.0);
        s.AddEnd(RestIn.Rotated, 2, 0.2);
        s.ContagionRolls = 9;
        s.FacingSamples = 300;
        s.WallLooks = 50;
        s.AddTired(0.25);
        s.AddTired(0.40);
        s.AddReady(BraceOrder.Charge, 0.7);
        s.AddReady(BraceOrder.Hold, 0.9);
        s.AddGap(true, 2.5, 2);
        s.AddGap(true, 0.5, 2);
        s.AddGap(false, 2.5, 2);
        var lines = new List<string>();
        s.AddSummaryLines(lines, Rules());
        Assert.Equal(2, lines.Count);
        Assert.Contains("EACH MAN ON HIS OWN ON: rests started 40 (in a wall slot 6", lines[0]);
        Assert.Contains("lasted mean 15.5 s (max 30.0 s)", lines[0]);
        Assert.Contains("by his own mark 2, by contagion 1 (rolls 9", lines[0]);
        Assert.Contains("rotated (a fresher man took his place) 1", lines[0]);
        Assert.Contains("stepped back from his start mean 1.55 m, p90 3.00 m, max 3.00 m (limit 4.0 m)", lines[0]);
        Assert.Contains("TURNED AWAY while resting 0 of 300", lines[0]);
        Assert.Contains("WALL men resting in their slots: 50 looks, more than 0.5 m out of the slot 0", lines[0]);
        Assert.Contains("tired mean 33% (25%-40%), ready charge 70% / advance - / hold 90%", lines[0]);
        Assert.Contains("THE NATURAL GAP", lines[1]);
        Assert.Contains("your team a gap of 2.0 m+ in 50% of 2 unit looks", lines[1]);
        Assert.Contains("the other sides a gap of 2.0 m+ in 100% of 1 unit looks", lines[1]);
        var off = new List<string>();
        new RestStats().AddSummaryLines(off, Rules(rest: false));
        Assert.Contains("OFF (RestEnabled)", off[0]);
        Assert.Contains("no engaged looks", off[1]);
    }
}

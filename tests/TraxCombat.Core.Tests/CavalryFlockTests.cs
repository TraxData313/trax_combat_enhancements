using System;
using System.Collections.Generic;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 55 (cavalry flocking - AI_NOTES "Step 55", Anton's continuous model): the 14 settings; the master switch first; the kinds and the flock
/// keys; EAGERNESS (the bar, the timid half, Athletics off) and the free line with its hysteresis - a fresh rider stays free; THE GROUP-SIZE
/// CURVE; THE HEADING BLEND; THE FLIP (size, leader, 3 s) and a mixed-eagerness flock that flips only when its rested rider leads; the sunflower
/// slots and the steer point; a rider's spell and wave; the hash grid and the table; the summary lines.
/// </summary>
public class CavalryFlockTests
{
    private static FlockRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return FlockRules.From(s);
    }

    private static readonly string[] Keys =
    {
        "CavalryFlockEnabled", "CavalryFlockFreeEagerPercent", "CavalryFlockHysteresisPercent", "CavalryFlockMidEagerPercent", "CavalryFlockMidGroupSize",
        "CavalryFlockExhaustedSharePercent", "CavalryFlockTimid", "CavalryFlockTimidRadiusMetres", "CavalryFlockGatherRadiusMetres", "CavalryFlockSeparationMetres",
        "CavalryFlockLeadSeconds", "CavalryFlockTightenWeights", "CavalryFlockWaveSeconds", "CavalryFlockMaxSeconds",
    };

    [Fact]
    public void The_14_settings_sit_in_their_own_group_after_courage_live_with_their_defaults()
    {
        Assert.Equal(SettingsSchema.CourageGroup.Order + 1, SettingsSchema.CavalryFlockGroup.Order);
        Assert.Equal(SettingsSchema.CavalryFlockGroup.Order + 1, SettingsSchema.ImpactGroup.Order);
        Assert.Equal(Keys.Skip(1), SettingsSchema.InGroup(SettingsSchema.CavalryFlockGroup).Select(p => p.Key).ToArray()); // the switch: the player page (step 59)
        Assert.Same(SettingsSchema.PlayerAiGroup, SettingsSchema.CavalryFlockEnabled.Group);
        foreach (var k in Keys)
        {
            Assert.True(SettingsSchema.TryGet(k, out var p), k);
            Assert.Equal(ApplyTiming.Live, p.Timing);
        }
        var s = new TraxSettings();
        Assert.True(s.CavalryFlockEnabled);
        Assert.Equal(75, s.CavalryFlockFreeEagerPercent);
        Assert.Equal(5, s.CavalryFlockHysteresisPercent);
        Assert.Equal(40, s.CavalryFlockMidEagerPercent);
        Assert.Equal(4, s.CavalryFlockMidGroupSize);
        Assert.Equal(65, s.CavalryFlockExhaustedSharePercent);
        Assert.True(s.CavalryFlockTimid);
        Assert.Equal(15f, s.CavalryFlockTimidRadiusMetres);
        Assert.Equal(25f, s.CavalryFlockGatherRadiusMetres);
        Assert.Equal(3.5f, s.CavalryFlockSeparationMetres);
        Assert.Equal(2f, s.CavalryFlockLeadSeconds);
        Assert.True(s.CavalryFlockTightenWeights);
        Assert.Equal(10f, s.CavalryFlockWaveSeconds);
        Assert.Equal(45f, s.CavalryFlockMaxSeconds);
        string d = Rules().Describe();
        Assert.Contains("at 75% or more he is FREE", d);
        Assert.Contains("1 at 75%, 4 at 40%, 65% of his kind at 0", d);
        Assert.Contains("Agent.SetScriptedPosition", d);
    }

    [Fact]
    public void The_master_switch_comes_first_then_its_own_switch_and_courage_only_lends_the_timid_factor()
    {
        var off = Rules(s => s.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm));
        Assert.False(off.Enabled);
        Assert.Equal("ModEnabled", off.OffBecause);
        Assert.Contains("cavalry flocking OFF (ModEnabled)", off.Describe());
        var own = Rules(s => s.Set(SettingsSchema.CavalryFlockEnabled, false, SettingSources.Mcm));
        Assert.Equal("CavalryFlockEnabled", own.OffBecause);
        var noCourage = Rules(s => s.Set(SettingsSchema.CourageEnabled, false, SettingSources.Mcm));
        Assert.True(noCourage.Enabled);
        Assert.False(noCourage.TimidOn);
        Assert.True(Rules().TimidOn);
    }

    [Fact]
    public void The_kind_comes_from_the_kit_and_each_kind_of_each_formation_is_its_own_flock()
    {
        Assert.Equal(RiderKind.HorseArcher, FlockMath.KindOf(true, true));
        Assert.Equal(RiderKind.Skirmisher, FlockMath.KindOf(false, true));
        Assert.Equal(RiderKind.Melee, FlockMath.KindOf(false, false));
        var keys = new HashSet<int>();
        for (int t = 0; t < FlockMath.MaxTeams; t++)
            for (int f = 0; f < FlockMath.MaxFormations; f++)
                for (int k = 0; k < 3; k++)
                {
                    int key = FlockMath.Key(t, f, (RiderKind)k);
                    Assert.InRange(key, 0, FlockMath.Keys - 1);
                    Assert.True(keys.Add(key));
                    Assert.Equal((RiderKind)k, FlockMath.KindOfKey(key));
                }
        Assert.Equal(-1, FlockMath.Key(8, 0, RiderKind.Melee));
        Assert.Equal(-1, FlockMath.Key(0, -1, RiderKind.Melee));
    }

    [Fact]
    public void Eagerness_is_the_bar_halved_when_timid_and_a_fresh_rider_stays_free_with_the_hysteresis()
    {
        var r = Rules();
        Assert.Equal(0.6, FlockMath.Eagerness(in r, 0.6, false), 9);
        Assert.Equal(0.3, FlockMath.Eagerness(in r, 0.6, true), 9);
        var noAth = Rules(s => s.Set(SettingsSchema.AthleticsEnabled, false, SettingSources.Mcm));
        Assert.Equal(1.0, FlockMath.Eagerness(in noAth, 0.1, false), 9);
        Assert.Equal(0.5, FlockMath.Eagerness(in noAth, 0.1, true), 9);
        Assert.True(FlockMath.IsFree(in r, 1.0, false)); // a fresh rider: the dance
        Assert.True(FlockMath.IsFree(in r, 0.75, false));
        Assert.False(FlockMath.IsFree(in r, 0.74, false));
        Assert.False(FlockMath.IsFree(in r, 0.78, true)); // a flocker needs 80
        Assert.True(FlockMath.IsFree(in r, 0.80, true));
        Assert.False(FlockMath.IsFree(in r, FlockMath.Eagerness(in r, 0.9, true), false)); // a fresh but lone rider flocks
        Assert.Equal(1.0, FlockMath.Readiness(in r, 0.9), 9);
        Assert.Equal(0.5, FlockMath.Readiness(in r, 0.375), 9);
    }

    [Fact]
    public void The_group_size_curve_grows_from_one_at_the_free_line_to_most_of_his_kind_when_exhausted()
    {
        var r = Rules();
        Assert.Equal(1, FlockMath.GroupNeeded(in r, 0.9, 20));
        Assert.Equal(1, FlockMath.GroupNeeded(in r, 0.75, 20));
        Assert.Equal(3, FlockMath.GroupNeeded(in r, 0.575, 20)); // halfway between 1 and 4 = 2.5, rounded up
        Assert.Equal(4, FlockMath.GroupNeeded(in r, 0.40, 20));
        Assert.Equal(9, FlockMath.GroupNeeded(in r, 0.20, 20));  // 4 + (13 - 4) x 0.5 = 8.5
        Assert.Equal(13, FlockMath.GroupNeeded(in r, 0.0, 20));  // 65% of 20
        Assert.Equal(4, FlockMath.GroupNeeded(in r, 0.0, 5));    // never below the middle size ...
        Assert.Equal(3, FlockMath.GroupNeeded(in r, 0.1, 3));    // ... and never more than his kind has
        int last = 0;
        for (int e = 100; e >= 0; e--)
        {
            int n = FlockMath.GroupNeeded(in r, e / 100.0, 40);
            Assert.True(n >= last, "the curve falls at " + e);
            last = n;
        }
        var tuned = Rules(s =>
        {
            s.Set(SettingsSchema.CavalryFlockMidGroupSize, 6, SettingSources.Mcm);
            s.Set(SettingsSchema.CavalryFlockExhaustedSharePercent, 100, SettingSources.Mcm);
        });
        Assert.Equal(6, FlockMath.GroupNeeded(in tuned, 0.40, 20));
        Assert.Equal(20, FlockMath.GroupNeeded(in tuned, 0.0, 20));
    }

    [Fact]
    public void The_heading_blends_the_rested_riders_pull_to_the_goal_and_the_tired_riders_pull_to_the_unit()
    {
        // the goal east (1, 0), the unit's centre north (0, 1)
        Assert.True(FlockMath.Heading(4, 0, 1, 0, 0, 1, out double hx, out double hy));
        Assert.Equal(1, hx, 6);
        Assert.Equal(0, hy, 6);
        FlockMath.Heading(0, 4, 1, 0, 0, 1, out hx, out hy);
        Assert.Equal(0, hx, 6);
        Assert.Equal(1, hy, 6);
        FlockMath.Heading(1, 3, 1, 0, 0, 1, out hx, out hy); // mostly tired: mostly toward the unit, bent toward the goal
        Assert.True(hy > hx && hx > 0);
        Assert.Equal(1, Math.Sqrt(hx * hx + hy * hy), 6);
        FlockMath.Heading(3, 1, 1, 0, 0, 1, out hx, out hy); // mostly rested: bends toward the enemy
        Assert.True(hx > hy && hy > 0);
        Assert.False(FlockMath.Heading(2, 2, 0, 0, 0, 0, out _, out _)); // nothing to pull toward
        Assert.Equal(0.75, FlockMath.GoalVote(3, 1), 9);
        FlockMath.Unit(0, 0, 3, 4, out double ux, out double uy);
        Assert.Equal(0.6, ux, 9);
        Assert.Equal(0.8, uy, 9);
        FlockMath.Unit(0, 0, double.NaN, 1, out ux, out uy);
        Assert.Equal(0, ux);
    }

    [Fact]
    public void The_flip_needs_the_size_the_mean_eagerness_wants_a_ready_leader_and_three_seconds()
    {
        Assert.True(FlockMath.Flips(4, 4, 0.6, 3.0));
        Assert.False(FlockMath.Flips(3, 4, 1.0, 10));  // too few
        Assert.False(FlockMath.Flips(4, 4, 0.4, 10));  // nobody rested enough to lead
        Assert.False(FlockMath.Flips(4, 4, 1.0, 2.0)); // just joined (the hysteresis)
    }

    [Fact]
    public void A_mixed_eagerness_flock_flips_only_when_its_rested_rider_leads()
    {
        var r = Rules();
        // four flocking lancers of a 6-rider kind: all tired (30-35%) - the size is there (4), but nobody leads
        double[] tired = { 0.35, 0.30, 0.30, 0.32 };
        double mean = tired.Average();
        int need = FlockMath.GroupNeeded(in r, mean, 6);
        Assert.Equal(4, need);
        Assert.False(FlockMath.Flips(tired.Length, need, FlockMath.Readiness(in r, tired.Max()), 5));
        // one of them has rested to 50%: he leads, the flock flips
        double[] led = { 0.50, 0.30, 0.30, 0.32 };
        need = FlockMath.GroupNeeded(in r, led.Average(), 6);
        Assert.True(FlockMath.Flips(led.Length, need, FlockMath.Readiness(in r, led.Max()), 5));
        // the heading of the led flock leans further toward the goal than the tired one's
        double Ready(double[] es) => es.Sum(e => FlockMath.Readiness(in r, e));
        FlockMath.Heading(Ready(tired), tired.Length - Ready(tired), 1, 0, 0, 1, out double tx, out _);
        FlockMath.Heading(Ready(led), led.Length - Ready(led), 1, 0, 0, 1, out double lx, out _);
        Assert.True(lx > tx);
        // half-tired riders (55%) want 3 - a small wave; exhausted ones (10%) of a 20-rider kind want 11 - a big one
        Assert.Equal(3, FlockMath.GroupNeeded(in r, 0.55, 20));
        Assert.Equal(11, FlockMath.GroupNeeded(in r, 0.10, 20));
    }

    [Fact]
    public void Sunflower_slots_never_stack_and_the_steer_point_leads_along_the_heading()
    {
        var pts = new List<(double x, double y)>();
        for (int i = 0; i < 40; i++)
        {
            FlockMath.SlotOffset(i, 3.5, out double dx, out double dy);
            pts.Add((dx, dy));
        }
        Assert.Equal((0.0, 0.0), pts[0]);
        double nearest = double.MaxValue;
        for (int i = 0; i < pts.Count; i++)
            for (int j = i + 1; j < pts.Count; j++)
                nearest = Math.Min(nearest, Math.Sqrt(Math.Pow(pts[i].x - pts[j].x, 2) + Math.Pow(pts[i].y - pts[j].y, 2)));
        Assert.True(nearest > 1.5, "two slots " + nearest + " m apart");
        FlockMath.SlotOffset(39, 3.5, out double fx, out double fy);
        Assert.InRange(Math.Sqrt(fx * fx + fy * fy), 10, 14); // 40 riders in a ~12 m disc
        FlockMath.SteerPoint(100, 50, 1, 0, 8, 2, 0, 3.5, out double tx, out double ty);
        Assert.Equal(116, tx, 9);
        Assert.Equal(50, ty, 9);
        FlockMath.SteerPoint(100, 50, 0, 1, 0, 2, 0, 3.5, out tx, out ty); // standing still: never below a canter
        Assert.Equal(100 + 0, tx, 9);
        Assert.Equal(50 + FlockMath.MinLeadSpeed * 2, ty, 9);
        Assert.True(FlockMath.Reissue(double.NaN, 0, 0, 1, 1, 10));
        Assert.False(FlockMath.Reissue(10, 0, 0, 1, 1, 10.5));
        Assert.True(FlockMath.Reissue(10, 0, 0, 3, 0, 10.5));
        Assert.True(FlockMath.Reissue(10, 0, 0, 0, 0, 12));
    }

    [Fact]
    public void A_riders_spell_the_wave_and_his_census_velocity()
    {
        var rf = new RiderFlock();
        Assert.Equal(RiderFlockState.Free, rf.State);
        rf.Begin(FlockReason.Tired, 100, 3);
        Assert.True(rf.Flocking);
        Assert.Equal(3, rf.Slot);
        Assert.Equal(12, rf.End(112, 10), 9);
        Assert.Equal(RiderFlockState.Wave, rf.State);
        Assert.False(rf.WaveOver(121));
        Assert.True(rf.WaveOver(122));
        Assert.Equal(RiderFlockState.Free, rf.State);
        rf.Begin(FlockReason.Timid, 130, 0);
        rf.End(131, 0);
        Assert.Equal(RiderFlockState.Free, rf.State);
        rf.See(0, 0, 10);
        rf.See(4, 3, 10.5);
        Assert.Equal(8, rf.VelX, 9);
        Assert.Equal(6, rf.VelY, 9);
        rf.See(100, 0, 13); // a long gap: no velocity
        Assert.Equal(0, rf.VelX);
        FlockMath.Velocity(0, 0, 0, 100, 0, 1, out double vx, out _);
        Assert.Equal(FlockMath.MaxSpeed, vx, 9);
    }

    [Fact]
    public void The_grid_finds_the_riders_of_his_flock_within_the_radius_across_cells_and_grows()
    {
        var g = new FlockGrid();
        g.Clear(25);
        g.Add(0, 7, 0, 0);
        g.Add(1, 7, 24, 0);    // the next cell, within 25
        g.Add(2, 7, 26, 0);    // beyond
        g.Add(3, 8, 1, 1);     // another flock
        g.Add(4, 7, -10, -20); // a cell down-left, within
        var buf = new int[16];
        int n = g.Query(7, 0, 0, 25, buf);
        Assert.Equal(new[] { 0, 1, 4 }, buf.Take(n).OrderBy(i => i).ToArray());
        g.Clear(25);
        Assert.Equal(0, g.Query(7, 0, 0, 25, buf));
        for (int i = 0; i < 300; i++) g.Add(i, 1, i * 0.1, 0);
        Assert.Equal(300, g.Count);
        var big = new int[512];
        Assert.Equal(251, g.Query(1, 0, 0, 25, big)); // 0 .. 25.0 m
    }

    [Fact]
    public void The_table_keeps_each_flocks_centre_and_restarts_its_slots_when_nobody_flocks()
    {
        var t = new FlockTable();
        t.Begin();
        t.Add(5, 0, 0, true);
        t.Add(5, 10, 0, false);
        t.Add(6, 50, 50, false);
        t.Finish();
        Assert.Equal(2, t.Riders[5]);
        Assert.Equal(5, t.CentreX[5], 9);
        Assert.Equal(5, t.DistanceFromCentre(5, 0, 0), 9);
        Assert.Equal(0, t.TakeSlot(5));
        Assert.Equal(1, t.TakeSlot(5));
        t.Begin();
        t.Add(5, 0, 0, false);
        t.Finish();
        Assert.Equal(0, t.NextSlot[5]);
        Assert.False(t.Touched(6));
        Assert.True(double.IsNaN(t.DistanceFromCentre(6, 0, 0)));
    }

    [Fact]
    public void The_summary_reads_the_shares_the_flips_by_eagerness_the_time_between_passes_and_the_lone_passes()
    {
        var st = new CavalryFlockStats();
        var lines = new List<string>();
        st.AddSummaryLines(lines, Rules(), "lever", 1);
        Assert.Equal(2, lines.Count);
        Assert.Contains("no AI rider under a charge was seen", lines[1]);
        st.AddRider(RiderKind.Melee);
        st.AddRider(RiderKind.HorseArcher);
        st.AddCensus(true, 2, 60);
        st.AddCensus(false, 2, 60);
        st.AddState(RiderFlockState.Free, 60);
        st.AddState(RiderFlockState.Flocking, 40);
        st.AddState(RiderFlockState.Wave, 20);
        st.AddStart(FlockReason.Tired, RiderKind.Melee);
        st.AddEnd(FlockEnd.Wave, 12);
        st.AddFlip(4, 0.6, 0.7);
        st.AddFlip(9, 0.2, 0.45);
        st.AddPass(true, false, 20, 0.9);
        st.AddPass(true, false, 45, 0.3);
        st.AddPass(false, true, 15, 0.3);
        st.AddPass(false, true, double.NaN, 0.9);
        st.AddSpread(true, 12);
        st.AddSpread(false, 30);
        Assert.Equal(0.5, st.StateShare(RiderFlockState.Free), 9);
        Assert.Equal(2, st.Flips);
        Assert.Equal(4, st.MeanWave(2), 9);
        Assert.Equal(9, st.MeanWave(0), 9);
        Assert.Equal(45, st.MeanPassGap(true, 1), 9);
        Assert.Equal(20, st.MeanPassGap(true, 3), 9);
        Assert.Equal(0.0, st.LoneShare(true), 9);
        Assert.Equal(1.0, st.LoneShare(false), 9);
        lines.Clear();
        st.AddSummaryLines(lines, Rules(), "Agent.SetScriptedPosition", 2);
        Assert.Equal(7, lines.Count);
        Assert.StartsWith("cavalry flocking settings: cavalry flocking: every AI rider", lines[0]);
        Assert.Contains("FREE 50.0%, FLOCKING 33.3%, IN A WAVE 16.7%", lines[1]);
        Assert.Contains("cavalry flips (a flock released at once toward its goal - a wave): 2 (1.00 a minute) of 13 riders, max 9", lines[2]);
        Assert.Contains("<25% 9.0 riders (1)", lines[2]);
        Assert.Contains("50-75% 4.0 riders (1)", lines[2]);
        Assert.Contains("ON <25% - s (0), 25-50% 45.0 s (1)", lines[3]);
        Assert.Contains("cavalry lone passes (a pass with fewer than 3 of his kind within 30 m): ON 0 of 2 (0.0%", lines[4]);
        Assert.Contains("OFF 2 of 2 (100.0%", lines[4]);
        Assert.Contains("ON 12.0 m (1 flock-censuses), OFF 30.0 m (1)", lines[5]);
        Assert.StartsWith("cavalry lever: Agent.SetScriptedPosition - points sent 0", lines[6]);
    }
}

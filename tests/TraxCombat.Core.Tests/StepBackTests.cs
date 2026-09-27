using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>DESIGN §2's step back (step 5d): the rules read live, the chance from f (0 in the
/// peak zone, 50% halfway, the full chance at empty), the roll, the spot straight away from the
/// enemy, the facing convention the engine uses, the log's facing / motion checks, and the
/// per-mission bookkeeping + summary text.</summary>
public class StepBackTests
{
    /// <summary>DESIGN's initial values.</summary>
    private static StepBackRules Defaults(bool enabled = true, int chance = 100, float distance = 2f, float seconds = 1.5f, float range = 4f,
        bool hold = true, int atOnce = 50, bool athletics = true, bool mod = true)
        => new(enabled, chance, distance, seconds, range, hold, atOnce, athletics, mod);

    private static AthleticsRules Athletics(bool enabled = true, bool mod = true)
        => new(enabled, 50, 1.0f, 75, true, 10, true, 0.75f, 0.75f, 20, 0.3f, 1.0f, true, 2, 1.5f, 60, 0.5f, 0.4f, mod);

    // ------------------------------------------------------------------ settings

    [Fact]
    public void Rules_read_the_live_settings_and_every_switch_turns_it_off()
    {
        var s = new TraxSettings();
        var r = StepBackRules.From(s);
        Assert.True(r.Enabled);
        Assert.Null(r.OffBecause);
        Assert.Equal(100, r.MaxChancePercent);
        Assert.Equal(1.0, r.MaxChance, 9);
        Assert.Equal(2.0f, r.Distance, 5);
        Assert.Equal(1.5f, r.Seconds, 5);
        Assert.Equal(4.0f, r.EnemyRange, 5);
        Assert.True(r.HoldAttacks);
        Assert.Equal(50, r.MaxAtOnce);

        s.Set(SettingsSchema.StepBackMaxChancePercent, 40, SettingSources.Mcm);
        s.Set(SettingsSchema.StepBackDistance, 3.5, SettingSources.Mcm);
        Assert.Equal(0.4, StepBackRules.From(s).MaxChance, 9);
        Assert.Equal(3.5f, StepBackRules.From(s).Distance, 5);

        s.Set(SettingsSchema.StepBackEnabled, false, SettingSources.Mcm);
        Assert.False(StepBackRules.From(s).Enabled);
        Assert.Equal("StepBackEnabled", StepBackRules.From(s).OffBecause);
        s.Set(SettingsSchema.StepBackEnabled, true, SettingSources.Mcm);

        s.Set(SettingsSchema.AthleticsEnabled, false, SettingSources.Mcm);
        Assert.False(StepBackRules.From(s).Enabled);
        Assert.Equal("AthleticsEnabled", StepBackRules.From(s).OffBecause);

        s.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm); // the master switch names itself first
        Assert.Equal("ModEnabled", StepBackRules.From(s).OffBecause);
        Assert.StartsWith("OFF (ModEnabled)", StepBackRules.From(s).Describe());
    }

    [Fact]
    public void Describe_names_every_number()
    {
        string text = Defaults().Describe();
        Assert.StartsWith("ON - after a melee swing an AI fighter on foot steps back with chance 100% x (1 - f) (0 at full strength), 2.0 m", text);
        Assert.Contains("for up to 1.5 s", text);
        Assert.Contains("only with the enemy within 4.0 m", text);
        Assert.Contains("no swings while stepping back", text);
        Assert.Contains("at most 50 at once", text);
        Assert.Contains("swings allowed", Defaults(hold: false).Describe());
    }

    // ------------------------------------------------------------------ the chance

    [Theory]
    [InlineData(1.0, 0.0)]    // the peak zone: never
    [InlineData(0.5, 0.5)]    // halfway down to 0: 50%
    [InlineData(0.0, 1.0)]    // empty: every swing
    [InlineData(0.8, 0.2)]
    [InlineData(1.7, 0.0)]    // f is at most 1 - clamped
    [InlineData(-0.2, 1.0)]
    public void Chance_is_the_max_times_one_minus_f(double f, double expected)
    {
        Assert.Equal(expected, StepBackMath.Chance(Defaults(), f), 9);
    }

    [Fact]
    public void Chance_scales_with_the_max_and_is_zero_when_off()
    {
        Assert.Equal(0.3, StepBackMath.Chance(Defaults(chance: 60), 0.5), 9);
        Assert.Equal(0.0, StepBackMath.Chance(Defaults(chance: 0), 0.0), 9);
        Assert.Equal(0.0, StepBackMath.Chance(Defaults(enabled: false), 0.0), 9);
        Assert.Equal(0.0, StepBackMath.Chance(Defaults(athletics: false), 0.0), 9);
        Assert.Equal(0.0, StepBackMath.Chance(Defaults(mod: false), 0.0), 9);
        Assert.Equal(0.0, StepBackMath.Chance(Defaults(), double.NaN), 9);
    }

    [Fact]
    public void Chance_follows_the_fighters_f_along_his_bar()
    {
        // A recruit (pool 50, peak line 37.5): 2 swings at full strength, then the chance climbs.
        var r = Athletics();
        var fighter = new Fighter { AthleticsSkill = 20 };
        var chances = new List<double>();
        for (int i = 1; i <= 5; i++)
        {
            AthleticsMath.Charge(fighter, r, i);
            chances.Add(StepBackMath.Chance(Defaults(), AthleticsMath.PeakShare(r, fighter)));
        }
        Assert.Equal(0.0, chances[0], 9);               // 40 left: still above the 37.5 line
        Assert.Equal(1 - 30 / 37.5, chances[1], 9);     // 30 left: f 0.8 → 20%
        Assert.True(chances[2] > chances[1] && chances[3] > chances[2]);
        Assert.Equal(1.0, chances[4], 9);               // empty
    }

    [Fact]
    public void Roll_never_says_yes_at_zero_and_always_at_one()
    {
        Assert.False(StepBackMath.Roll(0.0, 0.0));
        Assert.False(StepBackMath.Roll(0.0, 0.999));
        Assert.True(StepBackMath.Roll(1.0, 0.999999));
        Assert.True(StepBackMath.Roll(0.3, 0.29));
        Assert.False(StepBackMath.Roll(0.3, 0.3));
    }

    [Fact]
    public void Rolls_with_dice_land_near_the_chance_in_every_f_bin()
    {
        var dice = new SeededRandom(17);
        var stats = new StepBackStats();
        var r = Defaults();
        foreach (double f in new[] { 1.0, 0.75, 0.25, 0.0 })
        {
            for (int i = 0; i < 20000; i++)
            {
                double c = StepBackMath.Chance(r, f);
                stats.AddRoll(f, c, StepBackMath.Roll(c, dice.NextDouble()));
            }
        }
        Assert.Equal(0, stats.Yes(0));                                    // peak: 0%
        Assert.InRange(stats.Yes(1) / 20000.0, 0.23, 0.27);               // f 0.75 → 25%
        Assert.InRange(stats.Yes(2) / 20000.0, 0.73, 0.77);               // f 0.25 → 75%
        Assert.Equal(20000, stats.Yes(3));                                // empty: 100%
        Assert.Equal(0.25, stats.MeanChance(1), 9);
        Assert.Equal(80000, stats.Rolls);
    }

    // ------------------------------------------------------------------ geometry

    [Fact]
    public void The_spot_is_straight_away_from_the_enemy()
    {
        Assert.True(StepBackMath.AwayFrom(10, 10, 10, 13, 2, out double x, out double y));
        Assert.Equal(10, x, 9);
        Assert.Equal(8, y, 9);
        Assert.True(StepBackMath.AwayFrom(0, 0, 3, 4, 5, out x, out y));
        Assert.Equal(-3, x, 9);
        Assert.Equal(-4, y, 9);
        Assert.False(StepBackMath.AwayFrom(1, 1, 1, 1, 2, out x, out y)); // on top of each other: no direction
        Assert.Equal(1, x, 9);
    }

    [Fact]
    public void Radians_use_the_games_convention()
    {
        // Vec2.RotationInRadians = atan2(-x, y); Vec2.FromRotation(r) = (-sin r, cos r).
        Assert.Equal(0f, StepBackMath.Radians(0, 1), 5);
        Assert.Equal((float)(Math.PI / 2), StepBackMath.Radians(-1, 0), 5);
        foreach (var (dx, dy) in new[] { (0.3, 0.9), (-0.8, 0.1), (0.5, -0.5), (-0.2, -1.0) })
        {
            StepBackMath.FromRadians(StepBackMath.Radians(dx, dy), out double bx, out double by);
            double len = Math.Sqrt(dx * dx + dy * dy);
            Assert.Equal(dx / len, bx, 5);
            Assert.Equal(dy / len, by, 5);
        }
    }

    [Fact]
    public void Facing_and_motion_bins()
    {
        Assert.Equal(1.0, StepBackMath.FacingCosine(0, 2, 0, 5), 9);
        Assert.Equal(-1.0, StepBackMath.FacingCosine(0, 1, 0, -3), 9);
        Assert.True(double.IsNaN(StepBackMath.FacingCosine(0, 0, 1, 0)));
        Assert.Equal(0, StepBackMath.FacingBin(0.9));     // within 60°
        Assert.Equal(1, StepBackMath.FacingBin(0.0));     // side-on
        Assert.Equal(2, StepBackMath.FacingBin(-0.8));    // back turned
        Assert.Equal(3, StepBackMath.FacingBin(double.NaN));
        Assert.Equal(60, StepBackMath.Degrees(0.5), 6);

        // enemy to the north (0, 1): moving south at 1 m/s = moving away
        Assert.Equal(1.0, StepBackMath.SpeedAway(0, -1, 0, 4), 9);
        Assert.Equal(0, StepBackMath.MotionBin(1.0));
        Assert.Equal(1, StepBackMath.MotionBin(StepBackMath.SpeedAway(1, 0, 0, 4))); // sideways
        Assert.Equal(2, StepBackMath.MotionBin(StepBackMath.SpeedAway(0, 0.5, 0, 4)));
        Assert.Equal(3, StepBackMath.MotionBin(double.NaN));
    }

    [Fact]
    public void Level_and_time_checks()
    {
        Assert.True(StepBackMath.LevelEnough(10.0, 10.8));
        Assert.False(StepBackMath.LevelEnough(10.0, 7.0));   // a wall edge: the ground below
        Assert.False(StepBackMath.LevelEnough(10.0, double.NaN)); // off the navmesh

        var r = Defaults();
        Assert.False(StepBackMath.TimeUp(r, 100, 101.4));
        Assert.True(StepBackMath.TimeUp(r, 100, 101.5));
        Assert.True(StepBackMath.TimeUp(Defaults(seconds: 0.5f), 100, 100.6)); // read live: a shorter time applies mid-step
    }

    // ------------------------------------------------------------------ the summary

    [Fact]
    public void Stats_count_starts_ends_and_the_guard()
    {
        var st = new StepBackStats();
        st.AddStart(movementState: 1, facingBin: 0, askedDistance: 2);
        st.AddStart(movementState: 0, facingBin: 1, askedDistance: 2);
        st.AddStart(movementState: -1, facingBin: 0, askedDistance: 2);
        Assert.Equal(3, st.PeakAtOnce);
        st.AddEnd(StepBackEnd.TimeUp, 1.5, moved: 1.9, toSpot: 0.1, endFacingBin: 0);
        st.AddEnd(StepBackEnd.LeftField, 0.7, moved: double.NaN, toSpot: double.NaN, endFacingBin: -1);
        st.AddEnd(StepBackEnd.OrderChanged, 0.4, moved: 0.5, toSpot: 1.5, endFacingBin: 2);
        st.AddStart(movementState: 1, facingBin: 0, askedDistance: 2);
        Assert.Equal(3, st.PeakAtOnce);
        Assert.Equal(1, st.NowStepping);
        Assert.Equal((2, 1, 1), (st.StartedHolding, st.StartedCharging, st.StartedNoFormation));
        Assert.Equal(3, st.EndedTotal);
        Assert.Equal(2, st.CutShort);
        Assert.Equal(1, st.ReachedSpot);
        Assert.Equal(1.2, st.Moved.Mean, 9);
        Assert.Equal(0.5, st.MovedMin, 9);

        st.AddHitTaken(stepping: true, blocked: true);
        st.AddHitTaken(stepping: true, blocked: false);
        st.AddHitTaken(stepping: false, blocked: true);
        Assert.Equal(2, st.HitsWhileStepping);
        Assert.Equal(1, st.BlockedWhileStepping);
        Assert.Equal(1, st.HitsOthersOnFoot);
    }

    [Fact]
    public void Summary_lines_prove_each_claim()
    {
        var st = new StepBackStats();
        st.AddRoll(1.0, 0.0, false);
        st.AddRoll(1.0, 0.0, false);
        st.AddRoll(0.0, 1.0, true);
        st.AddNotRolled(StepBackNotRolled.Player);
        st.AddNotRolled(StepBackNotRolled.Mounted);
        st.AddRefused(StepBackRefusal.HoldingArrangement);
        st.AddStart(1, 0, 2.0);
        st.AddMidSample(0, 0);
        st.AddEnd(StepBackEnd.TimeUp, 1.5, 1.25, 0.8, 0);
        st.Releases = 1;
        var lines = st.SummaryLines(Defaults());

        Assert.StartsWith("step back - technique: a scripted step", lines[0]);
        Assert.Contains("settings at the end: ON - after a melee swing", lines[0]);
        Assert.Contains("peak (f 1) 2 swings, chance avg 0%, dice yes 0 (0%)", lines[1]);
        Assert.Contains("empty (f 0) 1 swings, chance avg 100%, dice yes 1 (100%)", lines[1]);
        Assert.Contains("f 0.5-1 0 swings |", lines[1]);
        Assert.Contains("not rolled: you 1, riders 1, not a field battle", lines[1]);
        Assert.Contains("step back starts: dice yes 1 → started 1 (holding a line 1, charging 0, no formation 0), most at once 1; not started 1 (shield wall/square/circle 1)", lines[2]);
        Assert.StartsWith("step back ends: 1 - completed (time up) 1, cut short 0", lines[3]);
        Assert.Contains("avg 1.25 m of 2.00 asked (min 1.25, max 1.25, reached the spot 0 of 1), lasted avg 1.50 s (n 1)", lines[4]);
        Assert.Contains("at the start: facing his enemy 1 (100%), side-on 0, back turned 0 | mid-step (1 sampled): facing his enemy 1 (100%)", lines[5]);
        Assert.Contains("moving away 1", lines[5]);
        Assert.Contains("swings started while stepping back 0 (0 expected: StepBackHoldAttacks is on)", lines[6]);
        Assert.StartsWith("step back release check: 1 released through the engine - scripted movement still on right after 0 (must be 0)", lines[7]);
        Assert.Contains("overdue (past their time) 0 (must be 0)", lines[7]);
    }

    [Fact]
    public void Summary_names_cut_short_reasons_and_refusals()
    {
        var st = new StepBackStats();
        st.AddStart(0, 0, 2);
        st.AddStart(0, 0, 2);
        st.AddEnd(StepBackEnd.SwitchedOff, 0.3, 0.2, 1.8, 0);
        st.AddEnd(StepBackEnd.HandedOver, 0.3, double.NaN, double.NaN, -1);
        st.AddRefused(StepBackRefusal.AtOnceCap);
        st.AddRefused(StepBackRefusal.AtOnceCap);
        st.AddRefused(StepBackRefusal.NotLevel);
        var lines = st.SummaryLines(Defaults(hold: false));
        Assert.Contains("not started 3 (spot not level (wall edge, stairs) 1, StepBackMaxAtOnce reached 2)", lines[2]);
        Assert.Contains("completed (time up) 0, cut short 2 (switched off 1, handed over to the game (not disabled) 1)", lines[3]);
        Assert.Contains("(StepBackHoldAttacks is off: allowed)", lines[6]);
    }
}

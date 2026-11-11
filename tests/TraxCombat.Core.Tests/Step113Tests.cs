using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 113 - THE OPENING MUST MAKE HIM REEL (Toni's training field 2026-11-11, vs the Training Master: "now he doesn't reel"; AI_NOTES "Step 113",
/// DESIGN decision 102): (A) the opening's reel 2.0 s + 0.25 a link, at most 2.5 (config format 31 moves untouched values once); (B) a perfect-strike
/// swing begun in his reel still meets a REELING man for step 105 (the flight) - his reel and immunity untouched; (C) his parry / wrong-side shield
/// block starts a hold (OpeningHoldOnParry); (D) the [summary] lines.
/// </summary>
public class Step113Tests
{
    private sealed class Dice : IRandomSource
    {
        private readonly double _v;
        public Dice(double v) => _v = v;
        public double NextDouble() => _v;
    }

    // ------------------------------------------------------------------ A. the longer reel, config format 31

    private static double Shipped(ParamDef p) =>
        p == SettingsSchema.OpeningReelBaseSeconds ? 2.0 : p == SettingsSchema.OpeningReelPerChainSeconds ? 0.25 : p == SettingsSchema.OpeningReelMaxSeconds ? 2.5
        : p == SettingsSchema.OpeningsEnabled ? 1 : p.Default;

    [Fact]
    public void Defaults_json_ships_the_two_second_reel()
    {
        string text = DefaultsFile.ReadEmbeddedText();
        Assert.Contains("\"OpeningReelBaseSeconds\": 2.0,", text, StringComparison.Ordinal);
        Assert.Contains("\"OpeningReelPerChainSeconds\": 0.25,", text, StringComparison.Ordinal);
        Assert.Contains("\"OpeningReelMaxSeconds\": 2.5,", text, StringComparison.Ordinal);
        Assert.Contains("\"OpeningHoldOnParry\": true,", text, StringComparison.Ordinal);
        var s = new TraxSettings();
        s.Set(SettingsSchema.OpeningReelBaseSeconds, 2.0, "test");
        s.Set(SettingsSchema.OpeningReelPerChainSeconds, 0.25, "test");
        s.Set(SettingsSchema.OpeningReelMaxSeconds, 2.5, "test");
        var r = OpeningRules.From(s);
        Assert.Equal(2.0, OpeningMath.ReelSeconds(in r, 1), 9);
        Assert.Equal(2.25, OpeningMath.ReelSeconds(in r, 2), 9);
        Assert.Equal(2.5, OpeningMath.ReelSeconds(in r, 3), 9);
        Assert.Equal(2.5, OpeningMath.ReelSeconds(in r, 4), 9);
        // the stagger animation is SLOWED to fit (natural ~1.0 s -> x0.5 for 2 s, x0.4 for 2.5 s), never below the floor
        Assert.Equal(0.5f, ImpactBlockMath.StaggerSpeed(2.0, 1.0), 3);
        Assert.True(ImpactBlockMath.StaggerSpeed(2.5, 1.0) > ImpactBlockMath.MinStaggerSpeed);
    }

    [Theory]
    [InlineData(29)]
    [InlineData(30)]
    public void Format_31_moves_an_untouched_reel_once_and_keeps_a_tuned_one(int from)
    {
        Assert.Equal(31, ConfigFile.FormatVersion);
        var read = ConfigFile.Read("{ \"ConfigVersion\": " + from + ", \"OpeningReelBaseSeconds\": 0.8, \"OpeningReelPerChainSeconds\": 0.3, \"OpeningReelMaxSeconds\": 2.0 }");
        var notes = ConfigFile.Migrate(read, Shipped);
        Assert.Equal(2.0, read.Values["OpeningReelBaseSeconds"], 9);
        Assert.Equal(0.25, read.Values["OpeningReelPerChainSeconds"], 9);
        Assert.Equal(2.5, read.Values["OpeningReelMaxSeconds"], 9);
        Assert.Contains(notes, n => n.StartsWith("OpeningReelBaseSeconds: 0.8 → 2.0 (format " + from + " → 31, the old default; step 113", StringComparison.Ordinal));
        // a tuned value is kept and said once
        var mine = ConfigFile.Read("{ \"ConfigVersion\": " + from + ", \"OpeningReelBaseSeconds\": 1.2 }");
        var kept = ConfigFile.Migrate(mine, Shipped);
        Assert.Equal(1.2, mine.Values["OpeningReelBaseSeconds"], 9);
        Assert.Contains(kept, n => n.StartsWith("OpeningReelBaseSeconds: 1.2 kept", StringComparison.Ordinal));
        // a format-31 file keeps whatever it holds; a format-28 file never had the keys
        var now = ConfigFile.Read("{ \"ConfigVersion\": 31, \"OpeningReelBaseSeconds\": 0.8 }");
        Assert.Empty(ConfigFile.Migrate(now, Shipped));
        Assert.Equal(0.8, now.Values["OpeningReelBaseSeconds"], 9);
        Assert.DoesNotContain(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 28 }"), Shipped), n => n.StartsWith("OpeningReel", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------ B. the perfect strike's flight counts as reeling for step 105

    private static ImpactRules Rules()
    {
        var s = new TraxSettings();
        s.Set(SettingsSchema.ImpactOnPlayerPercent, 100, "test");
        s.Set(SettingsSchema.ReelingHitKnockdownEnabled, 1, "test");
        s.Set(SettingsSchema.ReelingHitKnockdownPercent, 80, "test");
        return ImpactRules.From(s);
    }

    private static ImpactFacts Hit(bool flight, bool mounted = false) => new ImpactFacts
    {
        Event = ImpactEvent.CleanHit,
        AttackerPoints = 100, AttackerF = 1, AttackerAnimation = 1, AttackerSkill = 100,
        DefenderPoints = 100, DefenderBar = 1, DefenderF = 1, DefenderSkill = 100,
        WeaponWeight = 1.2, SwingSpeed = 92, DamageType = ImpactDamageType.Cut, DefenderHasShield = true, DefenderMaxHealth = 100, Damage = 20,
        AttackerIsPlayer = true, DefenderMounted = mounted, DefenderInStrikeFlight = flight,
    };

    /// <summary>His opening reel ENDED at t = 99.6 (the perfect strike lands at 100, 0.4 s later - Toni's 1.40 s after a 0.8 s reel + grace).</summary>
    private static ImpactState ReelEnded(ReelCause cause = ReelCause.Opening)
    {
        var s = default(ImpactState);
        s.Reel(99.6, 2, cause);
        return s;
    }

    [Theory]
    [InlineData(0.79, true)]
    [InlineData(0.81, false)]
    public void A_swing_begun_in_his_reel_rolls_the_80_percent_knockdown_after_the_reel_ended(double die, bool down)
    {
        var r = Rules();
        var st = ReelEnded();
        double reelEnd = st.ReelingUntil, immune = st.StaggerImmuneUntil;
        var a = default(ImpactState);
        var d = ImpactMath.Decide(in r, Hit(flight: true), ref st, ref a, 100, new Dice(die));
        Assert.True(d.ReelingHit.Asked);
        Assert.True(d.ReelingHit.ByFlight);
        Assert.Equal(ReelCause.Opening, d.ReelingHit.Cause);
        Assert.Equal(0.8, d.ReelingHit.Chance, 9);
        Assert.Equal(down, d.Knockdown.Happens);
        // his reel and the stagger immunity are NOT lengthened by the flight (only a fall may end them)
        if (!down)
        {
            Assert.Equal(reelEnd, st.ReelingUntil, 9);
            Assert.True(st.StaggerImmuneUntil >= immune);
        }
    }

    [Fact]
    public void Without_the_flight_the_same_late_blow_is_not_asked_and_a_rider_never_is()
    {
        var r = Rules();
        var st = ReelEnded();
        var a = default(ImpactState);
        var d = ImpactMath.Decide(in r, Hit(flight: false), ref st, ref a, 100, new Dice(0.0));
        Assert.False(d.ReelingHit.Asked);
        st = ReelEnded(ReelCause.RiderReel);
        d = ImpactMath.Decide(in r, Hit(flight: true), ref st, ref a, 100, new Dice(0.0));
        Assert.False(d.ReelingHit.ByFlight);
        st = ReelEnded();
        d = ImpactMath.Decide(in r, Hit(flight: true, mounted: true), ref st, ref a, 100, new Dice(0.0));
        Assert.False(d.ReelingHit.ByFlight);
        // a blow while he still reels is step 105's own, not the flight's
        st = default;
        st.Reel(101, 2, ReelCause.Opening);
        d = ImpactMath.Decide(in r, Hit(flight: true), ref st, ref a, 100, new Dice(0.0));
        Assert.True(d.ReelingHit.Asked);
        Assert.False(d.ReelingHit.ByFlight);
    }

    [Fact]
    public void The_flight_window_is_inclusive_and_zero_is_none()
    {
        Assert.True(ReelingHitMath.InStrikeFlight(100, 100));
        Assert.True(ReelingHitMath.InStrikeFlight(99.5, 100));
        Assert.False(ReelingHitMath.InStrikeFlight(100.01, 100));
        Assert.False(ReelingHitMath.InStrikeFlight(0, 0));
    }

    // ------------------------------------------------------------------ C. his parry / block starts a hold

    [Fact]
    public void OpeningHoldOnParry_sits_after_the_arrow_colours_on_by_default_live_and_advanced()
    {
        var all = SettingsSchema.All.ToList();
        Assert.Equal(all.IndexOf(SettingsSchema.PerfectStrikeArrowColor) + 1, all.IndexOf(SettingsSchema.OpeningHoldOnParry));
        var p = SettingsSchema.OpeningHoldOnParry;
        Assert.DoesNotContain(p, SettingsSchema.PlayerPage);
        Assert.Same(SettingsSchema.OpeningHoldEndsWhenYouReel.Group, p.Group);
        Assert.Equal(ApplyTiming.Live, p.Timing);
        Assert.Equal(ParamType.Bool, p.Type);
        var s = new TraxSettings();
        s.Set(SettingsSchema.OpeningHoldOnParry, 1, "test");
        Assert.True(OpeningHoldRules.From(s).HoldOnParry);
        s.Set(SettingsSchema.OpeningHoldOnParry, 0, "test");
        Assert.False(OpeningHoldRules.From(s).HoldOnParry);
    }

    // ------------------------------------------------------------------ 113b. the impact system's own stagger on the same hit: the longer wins

    [Theory]
    [InlineData(2.0, 0.95, true)]   // Toni's 08:29 log: 0.95 s carried the opening - ours (2 s) replaces it
    [InlineData(2.25, 2.0, true)]
    [InlineData(2.0, 2.0, false)]   // a tie: the impact system's carries it (no second stagger)
    [InlineData(2.0, 1.97, false)]  // within 0.05 s: no swap for nothing
    [InlineData(2.0, 2.4, false)]   // its own is longer: it carries it
    [InlineData(2.0, double.NaN, true)]
    [InlineData(double.NaN, 1.0, false)]
    public void The_longer_of_the_opening_reel_and_the_impact_stagger_wins(double opening, double impact, bool openingTakesOver) =>
        Assert.Equal(openingTakesOver, OpeningMath.OpeningOutlastsImpact(opening, impact));

    [Fact]
    public void Lengthening_his_reel_moves_its_end_and_its_immunity_and_the_cause_is_the_opening()
    {
        // the impact decision's own 0.95 s stagger, then the opening's 2.0 s on the same hit (QueueOpeningStagger's Reel): the end, the cause
        // (step 105 / the window read it) and the immunity after it follow the longer reel - never stacked (2.0, not 2.95)
        var s = default(ImpactState);
        s.Reel(100 + 0.95, 2, ReelCause.HitStagger);
        s.Reel(100 + 2.0, 2, ReelCause.Opening);
        Assert.Equal(102.0, s.ReelingUntil, 9);
        Assert.Equal(ReelCause.Opening, s.ReelCause);
        Assert.Equal(104.0, s.StaggerImmuneUntil, 9);
        Assert.True(s.Reeling(101.9));
        Assert.True(PerfectStrikeMath.Open(101.9, s.ReelingUntil, 0.15));
    }

    // ------------------------------------------------------------------ D. the [summary] lines

    [Fact]
    public void The_summary_lines_show_the_reel_the_strikes_in_and_after_it_the_knockdowns_and_the_holds_by_parry()
    {
        var s = new TraxSettings();
        s.Set(SettingsSchema.OpeningReelBaseSeconds, 2.0, "test");
        s.Set(SettingsSchema.OpeningHoldOnParry, 1, "test");
        var open = new OpeningStats();
        open.LiveAsked = 2;
        open.NoteReelAsked(2.0);
        open.NoteReelAsked(2.25);
        open.NoteLiveMeasured(2.0, 1.95);
        open.LiveLengthenedImpact = 1;
        open.LiveLengthenedFromSum = 0.95;
        open.LiveOnModStagger = 2;
        string line = open.SummaryLine(OpeningRules.From(s));
        Assert.Contains("the impact system's own shorter stagger on the same hit LENGTHENED to the opening's 1 (from 0.95 s on average), carried by a longer one of its own 2", line, StringComparison.Ordinal);
        Assert.Contains("STEP 113 - HIS REEL from an opening (2.0 s", line, StringComparison.Ordinal);
        Assert.Contains("asked 2 (mean 2.13 s, longest 2.25 s), measured 1 (mean 1.95 s, longest 1.95 s)", line, StringComparison.Ordinal);

        var strike = new PerfectStrikeStats();
        strike.OpeningLandedInReel = 3;
        strike.OpeningLandedAfterReel = 1;
        strike.NoteOpeningReelHit(true, false);
        strike.NoteOpeningReelHit(true, true);
        strike.NoteOpeningReelHit(false, false);
        line = strike.SummaryLine(PerfectStrikeRules.From(s));
        Assert.Contains("STEP 113 - perfect strikes after an OPENING landed during his reel 3, after it (the flight) 1; your blows on a man reeling from an opening 3, KNOCKED HIM DOWN 2 (counted reeling by the flight 1",
            line, StringComparison.Ordinal);

        var holds = new OpeningHoldStats { HoldsByParry = 4, HoldsByWrongBlock = 1, ShieldHoldsByBlock = 2 };
        line = holds.SummaryLine(OpeningHoldRules.From(s));
        Assert.Contains("STEP 113 - holds started by his block of your attack: his parry 4, his wrong-side shield block 1, his correct shield block 2 (on)", line, StringComparison.Ordinal);

        var reel = new ReelingHitStats();
        var h = ReelingHitResult.None;
        h.Asked = true;
        h.Kind = ReelingHitKind.Swing;
        h.Cause = ReelCause.Opening;
        h.Happens = true;
        h.Chance = 0.8;
        h.Die = 0.1;
        h.ByFlight = true;
        reel.Note(in h, true, false, true);
        Assert.Contains("STEP 113 - a perfect-strike swing begun in his reel that landed after it ended (still counted reeling) 1, KNOCKED DOWN 1",
            reel.SummaryLine(ImpactRules.From(s)), StringComparison.Ordinal);
    }
}

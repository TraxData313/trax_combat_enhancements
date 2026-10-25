using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 89 - TIRED MEN HIT SOFTER (Toni 2026-10-24: "as the units get more tired their damage output to decrease further ... are we lowering the
/// base damage?"; AI_NOTES "Step 89", DESIGN decision 82): the setting placed / shipped / live, m = E + (1 − E) × f at f 1 / 0.5 / 0 for 60 and 100,
/// the gates (the master switch first, Athletics, the setting at 100, an untracked attacker, never falls / objects / shield blocks, not the roll's
/// switches), the charge exempt, never below 1, 0 stays 0, the averages, the summary line.
/// </summary>
public class Step89Tests
{
    private static readonly HitFacts Melee = new HitFacts(true, false, false, false, false, false, false);
    private static readonly HitFacts Arrow = new HitFacts(true, false, false, false, true, false, false);
    private static readonly HitFacts OnHorse = new HitFacts(true, false, false, false, false, true, false);

    private static DamageRules Rules(int tired = 60, bool mod = true, bool athletics = true, bool rollOn = true, bool critical = true) =>
        new DamageRules(rollOn, 50, true, true, true, false, mod, true, 25, critical, tired, athletics);

    [Fact]
    public void The_setting_sits_on_the_player_page_after_the_reduction_ships_60_and_is_live()
    {
        var all = SettingsSchema.All.ToList();
        var p = SettingsSchema.TiredDamageAtEmptyPercent;
        Assert.Equal(all.IndexOf(SettingsSchema.DamageReductionPercent) + 1, all.IndexOf(p));
        Assert.Same(SettingsSchema.PlayerDamageGroup, p.Group);
        Assert.Equal(SettingsPage.Player, p.Group.Page);
        Assert.Equal(ApplyTiming.Live, p.Timing);
        Assert.Equal(0, p.Min, 9);
        Assert.Equal(100, p.Max, 9);
        Assert.Equal(60, p.Default, 9);
        Assert.Equal("Tired men's damage at an empty bar (%)", p.Label);
        Assert.DoesNotContain(" f ", p.Description, StringComparison.Ordinal); // player words
        Assert.Contains("\"TiredDamageAtEmptyPercent\": 60,", DefaultsFile.ReadEmbeddedText(), StringComparison.Ordinal);
        var s = new TraxSettings();
        Assert.Equal(60, DamageRules.From(s).TiredAtEmptyPercent);
        s.Set(p, 100, SettingSources.Mcm);
        Assert.Equal(100, DamageRules.From(s).TiredAtEmptyPercent); // live: read at the hit
        s.Set(SettingsSchema.AthleticsEnabled, false, SettingSources.Mcm);
        Assert.False(DamageRules.From(s).AthleticsEnabled);
        Assert.Equal(24, ConfigFile.FormatVersion); // a NEW setting needs no migration
    }

    [Theory]
    [InlineData(60, 1.0, 1.0)]
    [InlineData(60, 0.5, 0.8)]
    [InlineData(60, 0.0, 0.6)]
    [InlineData(100, 1.0, 1.0)]
    [InlineData(100, 0.5, 1.0)]
    [InlineData(100, 0.0, 1.0)]
    [InlineData(0, 0.0, 0.0)]
    [InlineData(0, 0.25, 0.25)]
    [InlineData(60, 1.7, 1.0)]  // f clamped
    [InlineData(60, -0.3, 0.6)]
    [InlineData(150, 0.0, 1.0)] // a hand edit past the range = off
    public void The_multiplier_is_a_straight_line_from_E_at_empty_to_1_at_the_peak(int percent, double f, double m)
    {
        Assert.Equal(m, TiredDamage.Multiplier(percent, f), 9);
    }

    [Fact]
    public void An_untracked_attacker_keeps_x1()
    {
        Assert.Equal(1.0, TiredDamage.Multiplier(60, double.NaN), 9);
        Assert.Equal(TiredSkipReason.NoPool, TiredDamage.Decide(in Melee, 100f, Rules(), double.NaN));
    }

    [Fact]
    public void The_gates_never_falls_objects_shields_or_0_then_the_master_switch_first_then_Athletics_then_the_setting()
    {
        var r = Rules();
        Assert.Equal(TiredSkipReason.NotAnAgent, TiredDamage.Decide(new HitFacts(false, false, false, false, false, false, false), 100f, r, 0));
        Assert.Equal(TiredSkipReason.NoVictim, TiredDamage.Decide(new HitFacts(true, true, false, false, false, false, false), 100f, r, 0));
        Assert.Equal(TiredSkipReason.FallDamage, TiredDamage.Decide(new HitFacts(true, false, true, false, false, false, false), 100f, r, 0));
        Assert.Equal(TiredSkipReason.ShieldBlock, TiredDamage.Decide(new HitFacts(true, false, false, true, false, false, false), 100f, r, 0));
        Assert.Equal(TiredSkipReason.ZeroDamage, TiredDamage.Decide(in Melee, 0.4f, r, 0));
        // the master switch FIRST of the switches: off with Athletics off and the setting at 100 still says ModOff
        Assert.Equal(TiredSkipReason.ModOff, TiredDamage.Decide(in Melee, 100f, Rules(tired: 100, mod: false, athletics: false), 0));
        Assert.Equal(TiredSkipReason.AthleticsOff, TiredDamage.Decide(in Melee, 100f, Rules(tired: 100, athletics: false), 0));
        Assert.Equal(TiredSkipReason.SettingOff, TiredDamage.Decide(in Melee, 100f, Rules(tired: 100), 0));
        // melee and ranged blows, on people and horses
        Assert.Equal(TiredSkipReason.None, TiredDamage.Decide(in Melee, 100f, r, 0));
        Assert.Equal(TiredSkipReason.None, TiredDamage.Decide(in Arrow, 100f, r, 0));
        Assert.Equal(TiredSkipReason.None, TiredDamage.Decide(in OnHorse, 100f, r, 0));
        // NOT the roll's switches: the roll off, the multiplier still applies
        Assert.Equal(TiredSkipReason.None, TiredDamage.Decide(in Melee, 100f, Rules(rollOn: false), 0));
    }

    [Fact]
    public void A_charging_mans_melee_blow_counts_fresh_on_step_62s_path_only()
    {
        Assert.True(TiredDamage.Exempt(Rules(), charging: true, missile: false));
        Assert.False(TiredDamage.Exempt(Rules(), charging: false, missile: false));
        Assert.False(TiredDamage.Exempt(Rules(), charging: true, missile: true));                    // a missile never
        Assert.False(TiredDamage.Exempt(Rules(critical: false), charging: true, missile: false));    // SprintFullCritical off = multiplied like anyone
    }

    [Fact]
    public void Never_below_1_and_0_stays_0()
    {
        Assert.Equal(1f, TiredDamage.Apply(1.2f, 0.6));        // 0.72 → 1
        Assert.Equal(1f, TiredDamage.Apply(1f, 0.0));
        Assert.Equal(0.4f, TiredDamage.Apply(0.4f, 0.6));      // shown as 0 - untouched
        Assert.Equal(0f, TiredDamage.Apply(0f, 0.6));
        Assert.Equal(100f, TiredDamage.Apply(100f, 1.0));      // fresh: to the bit
        Assert.Equal(60f, TiredDamage.Apply(100f, 0.6), 3);
        Assert.Equal(80f, TiredDamage.Apply(100f, 0.8), 3);
    }

    [Theory]
    [InlineData(1.0, 1.00, 0.50, 1.50)] // fresh: 0.5-1.5, avg 1.00 - unchanged
    [InlineData(0.5, 0.70, 0.40, 1.00)] // f 0.5: (0.5-1.25) x 0.8
    [InlineData(0.0, 0.45, 0.30, 0.60)] // empty: (0.5-1.0) x 0.6
    public void The_roll_then_the_multiplier_gives_Tonis_ranges_and_averages(double f, double avg, double lo, double hi)
    {
        var dice = new SeededRandom(89);
        var r = Rules();
        double up = DamageRoll.Upside(in r, f), m = TiredDamage.Multiplier(r.TiredAtEmptyPercent, f), sum = 0, min = double.MaxValue, max = 0;
        const int N = 200000;
        for (int i = 0; i < N; i++)
        {
            var roll = DamageRoll.Roll(1000f, 50, dice, up);
            double v = TiredDamage.Apply(roll.After, m) / 1000.0;
            sum += v;
            min = Math.Min(min, v);
            max = Math.Max(max, v);
        }
        Assert.Equal(avg, sum / N, 2);
        Assert.InRange(min, lo - 1e-6, lo + 0.005);
        Assert.InRange(max, hi - 0.005, hi + 1e-6);
        // and step 25's reduction after it: an empty man's average 100 hit lands ~34, a fresh one's 75
        Assert.Equal(f == 0 ? 33.75 : f == 1 ? 75 : 52.5, 100 * avg * DamageReduction.Multiplier(25), 6);
    }

    [Fact]
    public void The_log_example_says_45_for_an_empty_man()
    {
        Assert.Equal("an empty man's average 100 hit lands 45 before the reduction (75 without it)", TiredDamage.Example(60, 50));
    }

    [Fact]
    public void The_summary_line_counts_hits_by_band_the_charges_and_the_skips()
    {
        var s = new DamageStats();
        Assert.Contains(s.SummaryLines(), l => l == "damage by tiredness (step 89 - TiredDamageAtEmptyPercent): no hit multiplied this mission; not multiplied: 0 hits");
        s.AddTired(1.0, 1.0, 75f, 60);
        s.AddTired(0.5, 0.8, 52f, 60);
        s.AddTired(0.0, 0.6, 34f, 60);
        s.AddTired(0.0, 0.6, 33f, 60);
        s.AddTiredCharge(80f);
        s.AddTiredSkip(TiredSkipReason.ShieldBlock);
        s.AddTiredSkip(TiredSkipReason.NoPool);
        s.AddTiredSkip(TiredSkipReason.NoPool);
        Assert.Equal(2, s.TiredCount(3, out double mEmpty, out double landedEmpty));
        Assert.Equal(0.6, mEmpty, 9);
        Assert.Equal(33.5, landedEmpty, 9);
        Assert.Equal(1, s.TiredCharges);
        Assert.Equal(2, s.TiredSkips(TiredSkipReason.NoPool));
        string line = s.SummaryLines().Single(l => l.StartsWith("damage by tiredness", StringComparison.Ordinal));
        Assert.Equal("damage by tiredness (step 89 - TiredDamageAtEmptyPercent 60%: the attacker's damage x E + (1 - E) x f after the roll, before the reduction):"
            + " peak (f 1) 1 hits avg x1.000, landed avg 75.0 | f 0.5-1 1 hits avg x0.800, landed avg 52.0 | empty (f 0) 2 hits avg x0.600, landed avg 33.5;"
            + " charging hits exempt (counted fresh, x1) 1, landed avg 80.0; not multiplied: shield blocks 1, attacker not tracked (no pool) 2", line);
        s.Reset();
        Assert.Equal(0, s.TiredCount(3, out _, out _));
        Assert.Equal(0, s.TiredCharges);
    }
}

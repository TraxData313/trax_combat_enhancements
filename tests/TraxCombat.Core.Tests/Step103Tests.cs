using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 103 - CROUCH REST (Toni 2026-11-09: "when I crouch (Z) increase the Athletics regeneration x2 (player only, but let this be there)";
/// AI_NOTES "Step 103", DESIGN decision 92): the rules (the master switch first), who it applies to, the x2 inside the refill - the delay after
/// a blow and the wounds' cap still hold -, the bar's "Resting" label, the settings' places, the summary line, no allocation.
/// (Part A of step 103 - the strict perfect-strike opposites - is in PerfectStrikeTests.)
/// </summary>
public class Step103Tests
{
    private static TraxSettings Settings(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        s.Set(SettingsSchema.ModEnabled, 1, "test");
        s.Set(SettingsSchema.AthleticsEnabled, 1, "test");
        s.Set(SettingsSchema.CrouchRefillMultiplier, 2.0, "test");
        s.Set(SettingsSchema.CrouchRefillForPlayer, 1, "test");
        s.Set(SettingsSchema.CrouchRefillForAi, 0, "test");
        s.Set(SettingsSchema.RegenDelayBlowTimes, 2, "test");
        s.Set(SettingsSchema.BlowTimeSeconds, 1.5, "test");
        s.Set(SettingsSchema.FullRegenSecondsStanding, 60, "test");
        s.Set(SettingsSchema.RegenRateNearFullPercent, 100, "test");
        tune?.Invoke(s);
        return s;
    }

    private static CrouchRestRules Rules(Action<TraxSettings>? tune = null) => CrouchRestRules.From(Settings(tune));

    [Fact]
    public void The_rules_follow_the_master_switch_athletics_the_multiplier_and_the_two_switches()
    {
        Assert.True(Rules().Enabled);
        Assert.Equal(2.0, Rules().Multiplier, 9);
        Assert.Equal("ModEnabled", Rules(s => { s.Set(SettingsSchema.ModEnabled, 0, "test"); s.Set(SettingsSchema.AthleticsEnabled, 0, "test"); }).OffBecause);
        Assert.Equal("AthleticsEnabled", Rules(s => s.Set(SettingsSchema.AthleticsEnabled, 0, "test")).OffBecause);
        Assert.Equal("CrouchRefillMultiplier 1", Rules(s => s.Set(SettingsSchema.CrouchRefillMultiplier, 1, "test")).OffBecause);
        Assert.False(Rules(s => s.Set(SettingsSchema.CrouchRefillForPlayer, 0, "test")).Enabled);
        Assert.True(Rules(s => { s.Set(SettingsSchema.CrouchRefillForPlayer, 0, "test"); s.Set(SettingsSchema.CrouchRefillForAi, 1, "test"); }).Enabled);
        Assert.Contains("crouch rest ON: crouched (Z) the Athletics refill runs x2.0 for you", Rules().Describe());
        Assert.Contains("crouch rest OFF (ModEnabled)", Rules(s => s.Set(SettingsSchema.ModEnabled, 0, "test")).Describe());
    }

    [Fact]
    public void Player_only_by_default_never_mounted_never_standing()
    {
        var r = Rules();
        Assert.Equal(2.0, CrouchRestMath.Multiplier(in r, isPlayer: true, mounted: false, crouched: true), 9);
        Assert.Equal(1.0, CrouchRestMath.Multiplier(in r, isPlayer: true, mounted: false, crouched: false), 9);
        Assert.Equal(1.0, CrouchRestMath.Multiplier(in r, isPlayer: true, mounted: true, crouched: true), 9);
        Assert.Equal(1.0, CrouchRestMath.Multiplier(in r, isPlayer: false, mounted: false, crouched: true), 9); // the AI: off by default
        var ai = Rules(s => s.Set(SettingsSchema.CrouchRefillForAi, 1, "test"));
        Assert.Equal(2.0, CrouchRestMath.Multiplier(in ai, isPlayer: false, mounted: false, crouched: true), 9);
        Assert.False(CrouchRestMath.Applies(in r, isPlayer: false, mounted: false)); // the AI's crouch is never even read
        Assert.Equal(5.0, CrouchRestMath.ClampMultiplier(9), 9);
        Assert.Equal(1.0, CrouchRestMath.ClampMultiplier(double.NaN), 9);
    }

    [Fact]
    public void Crouched_the_bar_refills_twice_as_fast_from_empty()
    {
        var s = Settings();
        var r = AthleticsRules.From(s);
        var cr = CrouchRestRules.From(s);
        double Refill(bool crouched)
        {
            var f = new Fighter { AthleticsSkill = 100 };
            f.Fraction = 0;
            f.LastBlowTime = 0;
            double k = CrouchRestMath.Multiplier(in cr, true, false, crouched), t = r.RegenDelaySeconds;
            while (f.Fraction < 1 - 1e-9 && t < 1000)
            {
                t += 0.1;
                AthleticsMath.Regen(f, in r, t, 0.1, 0f, 5f, k);
            }
            return t - r.RegenDelaySeconds;
        }
        double standing = Refill(false), crouching = Refill(true);
        Assert.InRange(standing, 59.9, 60.1);
        Assert.InRange(crouching, 29.9, 30.1);
    }

    [Fact]
    public void The_rest_after_a_blow_and_the_wounds_cap_still_hold_while_crouched()
    {
        var s = Settings();
        var r = AthleticsRules.From(s);
        var f = new Fighter { AthleticsSkill = 100 };
        f.Fraction = 0.2;
        f.LastBlowTime = 10;
        // inside the 3 s rest after a blow: nothing, crouched or not
        Assert.Equal(0.0, AthleticsMath.Regen(f, in r, 12, 1, 0f, 5f, 2.0).Gained);
        // wounded to 60%: crouched it still stops at the cap
        AthleticsMath.ApplyHealth(f, in r, 0.6);
        double t = 13;
        for (int i = 0; i < 1000; i++) AthleticsMath.Regen(f, in r, t += 0.1, 0.1, 0f, 5f, 2.0);
        Assert.Equal(0.6, f.Fraction, 9);
        // a multiplier at or below 1 changes nothing
        var a = new Fighter { AthleticsSkill = 100, Fraction = 0.2, LastBlowTime = 0 };
        var b = new Fighter { AthleticsSkill = 100, Fraction = 0.2, LastBlowTime = 0 };
        AthleticsMath.Regen(a, in r, 10, 1, 0f, 5f);
        AthleticsMath.Regen(b, in r, 10, 1, 0f, 5f, 0.5);
        Assert.Equal(a.Fraction, b.Fraction, 12);
    }

    [Fact]
    public void Running_crouched_keeps_the_pace_rule_times_the_crouch()
    {
        var s = Settings(x => x.Set(SettingsSchema.RegenMultiplierAtFullRun, 0.5, "test"));
        var r = AthleticsRules.From(s);
        var o = AthleticsMath.Regen(new Fighter { AthleticsSkill = 100, Fraction = 0.1, LastBlowTime = 0 }, in r, 10, 1, 5f, 5f, 2.0);
        Assert.Equal(1.0, o.RateMultiplier, 9); // a full run 0.5 x the crouch 2
    }

    [Fact]
    public void The_bar_label_reads_resting_while_it_refills_crouched()
    {
        Assert.Equal("Resting", BarMath.Label(exhausted: false, resting: true));
        Assert.Equal("Exhausted", BarMath.Label(exhausted: true, resting: true));
        Assert.Equal("Athletics", BarMath.Label(exhausted: false, resting: false));
    }

    [Fact]
    public void The_settings_sit_where_the_design_puts_them()
    {
        var all = SettingsSchema.All.ToList();
        Assert.Equal(all.IndexOf(SettingsSchema.HeroCostMultiplier) + 1, all.IndexOf(SettingsSchema.CrouchRefillForPlayer));
        Assert.Contains(SettingsSchema.CrouchRefillForPlayer, SettingsSchema.PlayerPage);
        Assert.DoesNotMatch(@"[Ss]tep \d", SettingsSchema.CrouchRefillForPlayer.Description);
        Assert.Equal(all.IndexOf(SettingsSchema.HideoutBossFightRefill) + 1, all.IndexOf(SettingsSchema.CrouchRefillMultiplier));
        Assert.Equal(all.IndexOf(SettingsSchema.CrouchRefillMultiplier) + 1, all.IndexOf(SettingsSchema.CrouchRefillForAi));
        Assert.Equal(SettingsPage.Advanced, SettingsSchema.CrouchRefillMultiplier.Group.Page);
        Assert.Equal(SettingsSchema.RefillGroup, SettingsSchema.CrouchRefillForAi.Group);
        Assert.Equal(2.0, SettingsSchema.CrouchRefillMultiplier.Default, 5);
        Assert.Equal(1, SettingsSchema.CrouchRefillForPlayer.Default, 5);
        Assert.Equal(0, SettingsSchema.CrouchRefillForAi.Default, 5);
    }

    [Fact]
    public void The_summary_line_counts_your_crouched_refill()
    {
        var st = new CrouchRestStats();
        st.PlayerCrouches = 2;
        st.Note(true, 10, 40);
        st.PlayerIdleSeconds = 3;
        string line = st.SummaryLine(Rules());
        Assert.Contains("crouch rest (step 103; ON, x2.0): you crouched 2 time(s), refilling crouched 10.0 s for 40.0 points (20.0 of them the crouch's extra)", line);
        Assert.Contains("crouched without a refill (the rest after a blow / full) 3.0 s", line);
        Assert.Contains("the AI off", line);
        Assert.Contains("no AI man crouched", st.SummaryLine(Rules(s => s.Set(SettingsSchema.CrouchRefillForAi, 1, "test"))));
    }

    [Fact]
    public void The_crouch_multiplier_allocates_nothing()
    {
        var r = Rules();
        double sum = 0;
        long bytes = NoAllocation.Bytes(() =>
        {
            for (int i = 0; i < 1000; i++) sum += CrouchRestMath.Multiplier(in r, (i & 1) == 0, false, (i & 2) == 0);
        });
        Assert.Equal(0, bytes);
        Assert.True(sum > 0);
    }
}

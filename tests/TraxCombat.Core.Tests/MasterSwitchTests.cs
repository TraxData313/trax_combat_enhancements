using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// The master switch ModEnabled (Anton, 2026-09-27; DESIGN §4): off = the whole mod steps aside
/// live - no damage roll (hits recorded unrolled for the comparison), no Athletics costs, refill
/// or penalty - while the mission log keeps recording; back on = everyone starts full.
/// </summary>
public class MasterSwitchTests
{
    private static HitFacts Melee() => new(true, false, false, false, false, false, false);
    private static HitFacts Arrow() => new(true, false, false, false, missile: true, false, false);
    private static HitFacts Shield() => new(true, false, false, shieldBlocked: true, false, false, false);
    private static HitFacts Fall() => new(true, false, fall: true, false, false, false, false);

    [Fact]
    public void It_is_the_first_setting_in_its_own_first_group()
    {
        Assert.Same(SettingsSchema.ModEnabled, SettingsSchema.All[0]);
        Assert.Same(SettingsSchema.MasterGroup, SettingsSchema.Groups[0]);
        Assert.Equal(ParamType.Bool, SettingsSchema.ModEnabled.Type);
        Assert.Equal(1, SettingsSchema.ModEnabled.Default); // DESIGN's initial value: on
        Assert.True(new TraxSettings().ModEnabled);
    }

    // ------------------------------------------------------------------ damage

    [Fact]
    public void Off_a_hit_that_would_roll_keeps_the_game_value_as_ModOff()
    {
        var s = new TraxSettings();
        s.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
        var r = DamageRules.From(s);
        Assert.False(r.ModEnabled);
        Assert.Equal(DamageSkipReason.ModOff, DamageRoll.Decide(Melee(), 30, r));
        Assert.Equal(DamageSkipReason.ModOff, DamageRoll.Decide(Arrow(), 30, r));
        s.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm); // live: the very next hit rolls
        Assert.Equal(DamageSkipReason.None, DamageRoll.Decide(Melee(), 30, DamageRules.From(s)));
    }

    [Fact]
    public void Off_every_other_rule_still_decides_first_so_both_runs_count_the_same_hits()
    {
        var off = new DamageRules(true, 50, true, true, true, false, modEnabled: false);
        Assert.Equal(DamageSkipReason.ShieldToggleOff, DamageRoll.Decide(Shield(), 30, off));
        Assert.Equal(DamageSkipReason.FallDamage, DamageRoll.Decide(Fall(), 30, off));
        Assert.Equal(DamageSkipReason.ZeroDamage, DamageRoll.Decide(Melee(), 0.4f, off));
        var offNoRanged = new DamageRules(true, 50, true, false, true, false, modEnabled: false);
        Assert.Equal(DamageSkipReason.RangedToggleOff, DamageRoll.Decide(Arrow(), 30, offNoRanged));
        Assert.Equal("mod OFF (ModEnabled)", DamageStats.SkipName(DamageSkipReason.ModOff));
    }

    [Fact]
    public void Off_hits_are_recorded_unrolled_and_the_summary_compares_them()
    {
        var s = new DamageStats();
        s.AddVanilla(DamageCategory.Melee, 40.4f);  // shows 40
        s.AddVanilla(DamageCategory.Ranged, 29.6f); // shows 30
        s.AddVanilla(DamageCategory.Mount, 11f);
        Assert.Equal(3, s.VanillaHits);
        Assert.Equal(0, s.Rolls);
        Assert.Equal(0, s.SkipsTotal);
        var lines = s.SummaryLines();
        Assert.Equal("damage rolls: none this mission", lines[0]);
        Assert.Contains("damage while the mod was OFF - the game's own numbers, not rolled (factor 1.00): 3 hits (melee 1, ranged 1, mounts 1, shields 0); damage 81 → 81 (+0.0%), avg 27.0 per hit", lines);
        Assert.Contains("damage not rolled: 0 hits", lines);
        Assert.DoesNotContain(lines, l => l.StartsWith("damage avg per hit"));

        // toggled mid-battle: both halves, side by side
        float factor = 1.5f;
        s.AddRoll(DamageCategory.Melee, new RollOutcome(40f, DamageRoll.Apply(40f, factor), factor, 0.99));
        Assert.Contains("damage avg per hit: rolled (mod ON) 60.0 (the game's 40.0 before the roll) | mod OFF 27.0", s.SummaryLines());

        s.Reset();
        Assert.Equal(0, s.VanillaHits);
    }

    // ------------------------------------------------------------------ Athletics

    [Fact]
    public void Off_Athletics_is_off_whatever_its_own_switch_says()
    {
        var s = new TraxSettings();
        Assert.True(AthleticsRules.From(s).Enabled);
        Assert.Null(AthleticsRules.From(s).OffBecause);

        s.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
        var r = AthleticsRules.From(s);
        Assert.False(r.Enabled);
        Assert.True(r.AthleticsEnabled);
        Assert.Equal("ModEnabled", r.OffBecause);

        var f = new Fighter();
        Assert.False(AthleticsMath.Charge(f, r, 1.0).Charged);           // no cost
        Assert.Equal(1.0, f.Fraction);
        Assert.Equal(0.0, AthleticsMath.Regen(f, r, 10, 1, 0f, 5f).Gained); // no refill
        Assert.Equal(1f, AthleticsMath.AttackSpeedMultiplier(r, f));     // no penalty
        var read = AthleticsMath.Read(r, f);
        Assert.False(read.Enabled);
        Assert.Equal(read.Pool, read.Points);

        s.Set(SettingsSchema.AthleticsEnabled, false, SettingSources.Mcm);
        Assert.Equal("ModEnabled", AthleticsRules.From(s).OffBecause);   // the master switch is named first
        s.Set(SettingsSchema.ModEnabled, true, SettingSources.Mcm);
        Assert.Equal("AthleticsEnabled", AthleticsRules.From(s).OffBecause);
    }

    [Fact]
    public void An_exhausted_fighter_is_not_penalized_while_the_mod_is_off()
    {
        var on = AthleticsRules.From(new TraxSettings());
        var f = new Fighter();
        for (int i = 1; i <= 10; i++) AthleticsMath.Charge(f, on, i);
        Assert.True(AthleticsMath.IsExhausted(on, f));
        Assert.True(AthleticsMath.AttackSpeedMultiplier(on, f) < 1f);

        var s = new TraxSettings();
        s.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
        var off = AthleticsRules.From(s);
        Assert.False(AthleticsMath.IsExhausted(off, f));
        Assert.Equal(1f, AthleticsMath.AttackSpeedMultiplier(off, f));
    }

    [Fact]
    public void The_summary_names_the_master_switch()
    {
        var s = new TraxSettings();
        s.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
        Assert.Equal("OFF - the whole mod is switched off (ModEnabled) - everyone full, no penalty", AthleticsStats.DescribeRules(AthleticsRules.From(s)));
    }

    // ------------------------------------------------------------------ the mission's switch log

    [Fact]
    public void A_battle_that_never_toggles_says_ON_or_OFF()
    {
        var on = new ModSwitchLog();
        on.Start(0, true);
        for (int t = 1; t < 100; t++) Assert.False(on.Observe(t, true));
        Assert.Equal("mod ON", on.Describe(100));
        Assert.Equal(1.0, on.OnShare(100));

        var off = new ModSwitchLog();
        off.Start(0, false);
        Assert.False(off.Observe(50, false));
        Assert.Equal("mod OFF", off.Describe(100));
        Assert.Equal(0.0, off.OnShare(100));
    }

    [Fact]
    public void Toggles_are_timed_and_the_on_share_is_measured()
    {
        var log = new ModSwitchLog();
        log.Start(0, true);
        Assert.False(log.Observe(20, true));
        Assert.True(log.Observe(40.1, false));   // off at 40.1 s
        Assert.False(log.Observe(60, false));
        Assert.True(log.Observe(80.3, true));    // on again at 80.3 s
        Assert.Equal(2, log.Toggles);
        Assert.True(log.IsOn && log.StartedOn);
        // on 0-40.1 and 80.3-100 = 59.8 s of 100
        Assert.Equal(0.598, log.OnShare(100), 3);
        Assert.Equal("mod was on for 60% of the battle (started ON; OFF at 40.1 s, ON at 80.3 s)", log.Describe(100));
    }

    [Fact]
    public void A_long_toggle_list_is_cut_and_counted()
    {
        var log = new ModSwitchLog();
        log.Start(0, false);
        for (int i = 1; i <= ModSwitchLog.ListedToggles + 3; i++) Assert.True(log.Observe(i, i % 2 == 1));
        Assert.EndsWith(", +3 more)", log.Describe(20));
        Assert.StartsWith("mod was on for ", log.Describe(20));
    }

    [Fact]
    public void The_toggle_line_says_what_changed()
    {
        Assert.StartsWith("mod switched OFF (ModEnabled) at 42.3 s - vanilla from now on: no damage rolls", ModSwitchLog.ToggleText(false, 42.25));
        Assert.StartsWith("mod switched ON (ModEnabled) at 80.0 s - everything back on", ModSwitchLog.ToggleText(true, 80));
        Assert.Contains("everyone starts with a full bar", ModSwitchLog.ToggleText(true, 80));
    }
}

using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

public class SettingsTests
{
    [Fact]
    public void A_new_settings_object_holds_every_default()
    {
        var s = new TraxSettings();
        foreach (var p in SettingsSchema.All)
            Assert.Equal(p.Default, s.Get(p));
        Assert.Equal(0, s.Version);
        Assert.True(s.DamageRandomEnabled);
        Assert.Equal(50, s.DamageRandomPercent);
        Assert.Equal(0.75f, s.HeroCostMultiplier);
        Assert.False(s.VerboseLogging);
    }

    [Theory]
    [InlineData(150, 100, true)]
    [InlineData(-5, 0, true)]
    [InlineData(30, 30, false)]
    public void Set_clamps_into_the_range(double requested, double expected, bool clamped)
    {
        var s = new TraxSettings();
        var r = s.Set("DamageRandomPercent", requested, "test");
        Assert.True(r.Known);
        Assert.Equal(clamped, r.Clamped);
        Assert.Equal(expected, s.DamageRandomPercent);
    }

    [Fact]
    public void Ints_round_bools_normalise_floats_keep_four_decimals()
    {
        var s = new TraxSettings();
        s.Set(SettingsSchema.MaxEndurance, 120.6, "test");
        Assert.Equal(121, s.MaxEndurance);

        s.Set(SettingsSchema.ShowPlayerBar, 0, "test");
        Assert.False(s.ShowPlayerBar);
        s.Set(SettingsSchema.ShowPlayerBar, 7, "test");
        Assert.True(s.ShowPlayerBar);
        Assert.Equal(1, s.Get(SettingsSchema.ShowPlayerBar));

        // MCM's float slider hands over 0.1f = 0.100000001490116...
        s.Set(SettingsSchema.HudRefreshSeconds, 0.1f, "MCM");
        Assert.Equal(0.1, s.Get(SettingsSchema.HudRefreshSeconds));
    }

    [Fact]
    public void NaN_falls_back_to_the_default()
    {
        var s = new TraxSettings();
        s.Set(SettingsSchema.CostPerBlow, 20, "test");
        s.Set(SettingsSchema.CostPerBlow, double.NaN, "test");
        Assert.Equal(10f, s.CostPerBlow);
    }

    [Fact]
    public void Unknown_key_changes_nothing()
    {
        var s = new TraxSettings();
        int raised = 0;
        s.Changed += _ => raised++;
        var r = s.Set("NoSuchSetting", 5, "test");
        Assert.False(r.Known);
        Assert.Equal(0, raised);
        Assert.Equal(0, s.Version);
    }

    [Fact]
    public void Keys_are_matched_ignoring_case()
    {
        var s = new TraxSettings();
        Assert.True(s.Set("damagerandompercent", 25, "test").Changed);
        Assert.Equal(25, s.DamageRandomPercent);
    }

    [Fact]
    public void Changed_reports_old_new_source_and_bumps_version()
    {
        var s = new TraxSettings();
        var seen = new List<SettingChange>();
        s.Changed += seen.Add;

        s.Set("DamageRandomPercent", 40, SettingSources.Mcm);

        var c = Assert.Single(seen);
        Assert.Same(SettingsSchema.DamageRandomPercent, c.Param);
        Assert.Equal(50, c.OldValue);
        Assert.Equal(40, c.NewValue);
        Assert.Equal("MCM", c.Source);
        Assert.Equal(1, c.Version);
        Assert.Equal(1, s.Version);
        Assert.Equal("DamageRandomPercent: 50 → 40 (source: MCM)", c.ToLogText());
    }

    [Fact]
    public void Setting_the_same_value_is_silent()
    {
        var s = new TraxSettings();
        int raised = 0;
        s.Changed += _ => raised++;
        var r = s.Set("DamageRandomPercent", 50, "test");
        Assert.True(r.Known);
        Assert.False(r.Changed);
        Assert.Equal(0, raised);
        Assert.Equal(0, s.Version);
    }

    [Fact]
    public void Clamped_changes_say_so_in_the_log_text()
    {
        var s = new TraxSettings();
        SettingChange? c = null;
        s.Changed += x => c = x;
        s.Set("ExhaustedAttackSpeedPercent", 1, SettingSources.File);
        Assert.NotNull(c);
        Assert.True(c!.Clamped);
        Assert.Equal("ExhaustedAttackSpeedPercent: 20 → 5 (source: file) [clamped into 5..100]", c.ToLogText());
    }

    [Fact]
    public void A_throwing_handler_does_not_stop_the_change_or_the_other_handlers()
    {
        var s = new TraxSettings();
        Exception? reported = null;
        int second = 0;
        s.HandlerFailed += e => reported = e;
        s.Changed += _ => throw new InvalidOperationException("boom");
        s.Changed += _ => second++;

        s.Set("VerboseLogging", 1, "test");

        Assert.True(s.VerboseLogging);
        Assert.Equal(1, second);
        Assert.IsType<InvalidOperationException>(reported);
    }

    [Fact]
    public void Reset_to_defaults_restores_everything_and_reports_each_change()
    {
        var s = new TraxSettings();
        s.Set("DamageRandomPercent", 10, "test");
        s.Set("ShowTargetBar", 0, "test");
        var seen = new List<SettingChange>();
        s.Changed += seen.Add;

        s.ResetToDefaults(SettingSources.Defaults);

        Assert.Equal(2, seen.Count);
        foreach (var p in SettingsSchema.All)
            Assert.Equal(p.Default, s.Get(p));
    }

    [Fact]
    public void Describe_marks_values_that_differ_from_the_default()
    {
        var s = new TraxSettings();
        Assert.Equal("MaxEndurance = 100", s.Describe(SettingsSchema.MaxEndurance));
        s.Set(SettingsSchema.DamageRandomPercent, 30, "test");
        Assert.Equal("DamageRandomPercent = 30 (default 50)", s.Describe(SettingsSchema.DamageRandomPercent));
    }
}

using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 100f - the independent review of step 100 and Toni's first timed-block playtest (2026-11-08 17:41, a tournament, polearm against
/// polearm): the direction-matched recoil ships OFF (format 27 moves a format-26 file's true once), the timing in your on-screen lines.
/// </summary>
public class Step100fTests
{
    /// <summary>The shipped value a migration moves to (the tests' own defaults are DESIGN's initial ones - the recoil's is true).</summary>
    private static double ShippedDefault(ParamDef p) => p == SettingsSchema.TimedBlockRecoil ? 0 : p.Default;

    [Fact]
    public void The_recoil_ships_off_and_format_27_moves_a_format_26_files_true_once()
    {
        Assert.Equal(27, ConfigFile.FormatVersion);
        Assert.Contains("\"TimedBlockRecoil\": false,", DefaultsFile.ReadEmbeddedText(), StringComparison.Ordinal);
        // a format-26 file (step 100d wrote true into it) → false, said once
        var read = ConfigFile.Read("{ \"ConfigVersion\": 26, \"TimedBlockRecoil\": true }");
        var notes = ConfigFile.Migrate(read, ShippedDefault);
        Assert.Equal(0, read.Values["TimedBlockRecoil"], 9);
        Assert.StartsWith("TimedBlockRecoil: true → false (format 26 → 27, the old default; step 100f", Assert.Single(notes), StringComparison.Ordinal);
        // a format-27 file keeps whatever it holds (the switch turned back on by hand stays on)
        var mine = ConfigFile.Read("{ \"ConfigVersion\": 27, \"TimedBlockRecoil\": true }");
        Assert.Empty(ConfigFile.Migrate(mine, ShippedDefault));
        Assert.Equal(1, mine.Values["TimedBlockRecoil"], 9);
        // an older file never had the key - nothing to move
        var old = ConfigFile.Read("{ \"ConfigVersion\": 25 }");
        Assert.DoesNotContain(ConfigFile.Migrate(old, ShippedDefault), n => n.StartsWith("TimedBlockRecoil", StringComparison.Ordinal));
    }

    [Fact]
    public void Your_block_on_screen_carries_the_guard_against_the_perfect_and_the_stagger_against_the_perfect_length()
    {
        // Toni's two forms: a riposte and a block he held
        Assert.Equal("Your riposte - Polmarc reels 1.46 s / 2.00 (guard 0.30 s / 0.15)",
            TimedBlockMath.YourBlockLine("Polmarc", ImpactEvent.Parry, true, 1.46, 2.0, TimingSource.PlayerClock, 0.30, 0.15));
        Assert.Equal("Your parry - Polmarc holds (guard 0.05 s / 0.15)",
            TimedBlockMath.YourBlockLine("Polmarc", ImpactEvent.Parry, false, double.NaN, 2.0, TimingSource.PlayerClock, 0.05, 0.15));
        Assert.Equal("Your block - Polmarc holds (guard 0.70 s / 0.15)",
            TimedBlockMath.YourBlockLine("Polmarc", ImpactEvent.RightBlock, false, double.NaN, 2.0, TimingSource.PlayerClock, 0.70, 0.15));
        // a held guard: capped, the ordinary (untimed) length
        Assert.Equal("Your riposte - Polmarc reels 0.81 s / 2.00 (guard >1 s / 0.15)",
            TimedBlockMath.YourBlockLine("Polmarc", ImpactEvent.RightBlock, true, 0.81, 2.0, TimingSource.PlayerClock, 3.7, 0.15));
        Assert.Equal("guard >1 s / 0.15", TimedBlockMath.GuardText(TimingSource.AiHeld, double.PositiveInfinity, 0.15));
        Assert.Equal("guard 1.00 s / 0.15", TimedBlockMath.GuardText(TimingSource.PlayerClock, 1.0, 0.15));
        Assert.Equal("a chamber", TimedBlockMath.GuardText(TimingSource.Chamber, 0, 0.15));
        // no timing read (timed blocks off): the plain line
        Assert.Null(TimedBlockMath.YourBlockLine("Polmarc", ImpactEvent.Parry, true, 1.0, 2.0, TimingSource.None, double.NaN, 0.15));
        // the plain held line exists for the merge queue's words
        Assert.Equal("Your block - X holds", ImpactMessageMath.Text(ImpactMessageKind.BlockHeld, ImpactMessageRole.ByYou, "you", "X"));
    }

    [Fact]
    public void The_timing_switch_is_an_advanced_impact_setting_shipped_on_and_read_live()
    {
        var p = SettingsSchema.TimedBlockShowTiming;
        Assert.Equal(SettingsPage.Advanced, p.Group.Page);
        Assert.Equal(SettingsSchema.ImpactGroup, p.Group);
        var all = SettingsSchema.All.ToList();
        Assert.Equal(all.IndexOf(SettingsSchema.PerfectStrikeCentreMessage) + 1, all.IndexOf(p));
        Assert.Contains("\"TimedBlockShowTiming\": true,", DefaultsFile.ReadEmbeddedText(), StringComparison.Ordinal);
        var s = new TraxSettings();
        s.Set(p, 0, "test");
        Assert.False(s.TimedBlockShowTiming);
        s.Set(p, 1, "test");
        Assert.True(s.TimedBlockShowTiming);
    }

    [Fact]
    public void The_recoil_switch_off_leaves_the_push_out_and_the_test_tool_alone()
    {
        var s = new TraxSettings();
        s.Set(SettingsSchema.TimedBlockRecoil, 0, "test");
        s.Set(SettingsSchema.TimedBlockAnimTest, 1, "test");
        var r = TimedAnimRules.From(s);
        Assert.False(r.RecoilEnabled);
        Assert.True(r.PushEnabled);
        Assert.True(r.TestOn); // the J tool still plays the recoils
    }
}

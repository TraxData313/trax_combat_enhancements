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

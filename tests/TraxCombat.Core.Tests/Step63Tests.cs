using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 63 - FALLS ON THE HIT (Anton's battle of 2026-10-19 11:02; AI_NOTES "Step 63"): 126 of 198 decided falls were played only by our
/// backup blow, every one on a 0-damage blow - a FRIEND's (the engine cancels a friend's damage, and a 0-damage blow plays no reaction). A
/// friend's blow is never decided; the backup comes after 0.2 s (config format 11 moves step 62's 0.5 once).
/// </summary>
public class FallsOnTheHitTests
{
    /// <summary>defaults.json's step-63 value - the tests run on DESIGN's initial 0.5, where nothing moves.</summary>
    private static double Shipped(ParamDef p) => p == SettingsSchema.ImpactFallBackupSeconds ? 0.2 : p.Default;

    [Fact]
    public void A_format_10_file_at_step_62s_backup_gets_0_2_once_a_tuned_one_is_kept_and_said()
    {
        Assert.Equal(11, ConfigFile.FormatVersion);
        var read = ConfigFile.Read("{ \"ConfigVersion\": 10, \"ImpactFallBackupSeconds\": 0.5 }");
        var notes = ConfigFile.Migrate(read, Shipped);
        Assert.Single(notes);
        Assert.StartsWith("ImpactFallBackupSeconds: 0.5 → 0.2 (format 10 → 11, the old default; step 63, Anton's battle", notes[0], StringComparison.Ordinal);
        Assert.Equal(0.2, read.Values["ImpactFallBackupSeconds"], 9);
        var mine = ConfigFile.Read("{ \"ConfigVersion\": 10, \"ImpactFallBackupSeconds\": 1.0 }");
        var kept = ConfigFile.Migrate(mine, Shipped);
        Assert.Single(kept);
        Assert.StartsWith("ImpactFallBackupSeconds: 1.0 kept - your own value (format 10 → 11; the new default 0.2", kept[0], StringComparison.Ordinal);
        Assert.Equal(1.0, mine.Values["ImpactFallBackupSeconds"], 9);
        // a format-11 file is never touched; a file without the key gets nothing to say; on DESIGN's initial 0.5 nothing moves
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 11, \"ImpactFallBackupSeconds\": 0.5 }"), Shipped));
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 10, \"CostPerBlow\": 7.0 }"), Shipped));
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 10, \"ImpactFallBackupSeconds\": 0.5 }")));
    }

    [Fact]
    public void The_backup_is_due_after_0_2_s_with_the_new_default()
    {
        Assert.False(ImpactFallMath.BackupDue(10.15, 10, 0.2, false, false));
        Assert.True(ImpactFallMath.BackupDue(10.21, 10, 0.2, false, false));
        Assert.False(ImpactFallMath.BackupDue(10.3, 10, 0.2, true, false));  // the game played it
        Assert.False(ImpactFallMath.BackupDue(10.3, 10, 0.2, false, true));  // sent once
    }

    [Fact]
    public void A_friends_blow_is_a_fact_of_its_own_off_by_default_and_counted_in_both_summary_lines()
    {
        Assert.False(new ImpactFacts().NotEnemies);
        var stats = new ImpactStats();
        stats.EverOn = true;
        stats.Skip(ImpactSkip.NotEnemies, ImpactEvent.CleanHit);
        stats.Skip(ImpactSkip.NotEnemies, ImpactEvent.Parry);
        var d = new ImpactDecision { Event = ImpactEvent.CleanHit, Bar = 0.5, Knockdown = ImpactRoll.None, ShieldDrop = ImpactRoll.None, Stagger = ImpactRoll.None, Riposte = ImpactRoll.None };
        stats.Note(in d, false, true, 0.001, 0);
        Assert.Contains(stats.SummaryLines(ImpactRules.From(new TraxSettings())), l => l.Contains("a FRIEND's blow (step 63 - the game cancels its damage, nothing plays) 2"));
        var falls = new ImpactFallStats();
        falls.Friendly = 126;
        string line = falls.SummaryLine(true, 0.2, true);
        Assert.DoesNotContain("no knockdown decided", line); // friends' blows alone are worth the line
        Assert.Contains("because a FRIEND struck them (step 63 - the game cancels a friend's damage: nothing to play) 126", line);
        Assert.Contains("backups (ImpactFallBackupSeconds 0.2 s)", line);
    }
}

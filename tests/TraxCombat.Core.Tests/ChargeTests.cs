using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 62 - MORE MEN FALL, AND THE CHARGE MATTERS (Anton 2026-10-19; AI_NOTES "Step 62", DESIGN decision 52): the lost falls (the shrug-off
/// yields to a decided fall, the backup blow, a killing blow never decided) and the falls' summary.
/// </summary>
public class ChargeTests
{
    private static ImpactRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return ImpactRules.From(s);
    }

    // ------------------------------------------------------------------ the lost falls

    [Fact]
    public void The_lost_falls_settings_sit_after_the_punish_ones_live_on_by_default_with_a_half_second_backup()
    {
        var keys = SettingsSchema.InGroup(SettingsSchema.ImpactGroup).Select(p => p.Key).ToList();
        int i = keys.IndexOf("ImpactPunishRangeMetres");
        Assert.Equal(new[] { "ImpactShrugOffYieldsToImpact", "ImpactFallBackupSeconds" }, keys.Skip(i + 1).Take(2));
        var r = Rules();
        Assert.True(r.ShrugOffYields);
        Assert.Equal(0.5, r.FallBackupSeconds, 6);
        Assert.Equal(SettingsPage.Advanced, SettingsSchema.ImpactFallBackupSeconds.Group.Page);
        Assert.False(Rules(s => s.Set(SettingsSchema.ImpactShrugOffYieldsToImpact, false, SettingSources.Mcm)).ShrugOffYields);
        Assert.Equal(0, Rules(s => s.Set(SettingsSchema.ImpactFallBackupSeconds, 0.0, SettingSources.Mcm)).FallBackupSeconds);
        Assert.Equal(ImpactFallMath.MaxBackupSeconds, SettingsSchema.ImpactFallBackupSeconds.Max);
    }

    [Fact]
    public void The_backup_is_due_once_its_wait_is_over_and_never_after_a_fall_showed_or_a_backup_went()
    {
        Assert.False(ImpactFallMath.BackupDue(100.4, 100, 0.5, appeared: false, sent: false));
        Assert.True(ImpactFallMath.BackupDue(100.5, 100, 0.5, appeared: false, sent: false));
        Assert.True(ImpactFallMath.BackupDue(101.2, 100, 0.5, appeared: false, sent: false));
        Assert.False(ImpactFallMath.BackupDue(101.2, 100, 0.5, appeared: true, sent: false));  // the game's fall showed
        Assert.False(ImpactFallMath.BackupDue(101.2, 100, 0.5, appeared: false, sent: true));  // once
        Assert.False(ImpactFallMath.BackupDue(101.2, 100, 0, appeared: false, sent: false));   // 0 = no backup
        Assert.False(ImpactFallMath.BackupDue(101.2, double.NaN, 0.5, appeared: false, sent: false));
    }

    [Fact]
    public void A_blow_that_leaves_him_under_one_hit_point_kills_and_is_never_decided()
    {
        Assert.True(ImpactFallMath.Fatal(10, 10));
        Assert.True(ImpactFallMath.Fatal(10, 9.5));
        Assert.False(ImpactFallMath.Fatal(10, 9));
        Assert.False(ImpactFallMath.Fatal(100, 0));
        Assert.False(ImpactFallMath.Fatal(double.NaN, 5));
        var stats = new ImpactStats();
        stats.Skip(ImpactSkip.Fatal, ImpactEvent.CleanHit);
        stats.EverOn = true;
        var d = new ImpactDecision { Event = ImpactEvent.CleanHit, Bar = 0.5, Knockdown = ImpactRoll.None, ShieldDrop = ImpactRoll.None, Stagger = ImpactRoll.None, Riposte = ImpactRoll.None };
        stats.Note(in d, false, true, 0.001, 0);
        Assert.Contains(stats.SummaryLines(Rules()), l => l.Contains("the blow killed him (step 62) 1"));
    }

    [Fact]
    public void The_falls_line_counts_decided_played_by_the_game_by_the_backup_not_seen_and_the_shrug_offs_refused()
    {
        var f = new ImpactFallStats();
        Assert.Contains("no knockdown decided on a landed blow this mission", f.SummaryLine(true, 0.5, true));
        for (int i = 0; i < 5; i++) f.NoteDecided(ImpactEvent.CleanHit, 12);
        f.NoteDecided(ImpactEvent.CleanHit, 0);
        f.NoteDecided(ImpactEvent.Kick, 2);
        f.NotePlayed(ImpactEvent.CleanHit, false, 0.02);
        f.NotePlayed(ImpactEvent.CleanHit, false, 0.04);
        f.NotePlayed(ImpactEvent.CleanHit, true, 0.55);
        f.NotePlayed(ImpactEvent.Kick, false, 0.0);
        f.NoteNotSeen(ImpactEvent.CleanHit);
        f.NoteLeftField(ImpactEvent.CleanHit);
        f.BackupsSent = 2;
        f.ShrugRefusedForFall = 3;
        f.ShrugRefusedForStagger = 4;
        f.Fatal = 9;
        string line = f.SummaryLine(true, 0.5, true);
        Assert.Contains("decided 7 (kicks / bashes 1, clean hits 6) → played by the game 3 (0.02 s after the blow), by our backup blow 1 (0.55 s after the blow), STILL NOT SEEN 1, the man left the field / died first 1", line);
        Assert.Contains("clean hits 2 / 1 / 1", line);
        Assert.Contains("on a 0-damage blow (the engine plays no reaction) 1", line);
        Assert.Contains("backups (ImpactFallBackupSeconds 0.5 s) sent 2", line);
        Assert.Contains("the game's shrug-off REFUSED for a decided fall 3 / stagger 4", line);
        Assert.Contains("(ImpactShrugOffYieldsToImpact on;", line);
        Assert.Contains("landed blows not decided because they KILLED 9", line);
        Assert.Contains("ImpactFallBackupSeconds 0 - off", f.SummaryLine(true, 0, false));
        Assert.Contains("ImpactShrugOffYieldsToImpact OFF", f.SummaryLine(true, 0, false));
    }
}

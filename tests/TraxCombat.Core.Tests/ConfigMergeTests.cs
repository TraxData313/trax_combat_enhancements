using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>The file-rewrite rule (ConfigMerge's class doc): MCM wins for what it touched,
/// the disk wins for everything else, memory fills the gaps.</summary>
public class ConfigMergeTests
{
    private static ConfigReadResult Disk(string json) => ConfigFile.Read(json);

    [Fact]
    public void A_hand_edit_on_disk_survives_an_MCM_save_of_another_key()
    {
        var memory = new TraxSettings();
        var tracker = new EditTracker();
        memory.Changed += c => { if (c.Source == SettingSources.Mcm) tracker.Note(c.Param, c.OldValue); };

        // Game loaded the defaults; the player then hand-edits DamageRandomPercent to 30 on disk…
        var disk = Disk(ConfigFile.Write(memory.Snapshot()).Replace("\"DamageRandomPercent\": 50", "\"DamageRandomPercent\": 30"));
        // …and changes VerboseLogging in MCM, then presses Done.
        memory.Set(SettingsSchema.VerboseLogging, true, SettingSources.Mcm);

        var plan = ConfigMerge.ForWrite(disk, memory.Snapshot(), tracker.ChangedKeys(memory));

        Assert.Equal(30, plan.Values["DamageRandomPercent"]); // hand edit kept
        Assert.Equal(1, plan.Values["VerboseLogging"]);       // MCM change written
        Assert.Equal(new[] { "DamageRandomPercent" }, plan.KeptFromDisk);
        Assert.Equal(50, memory.DamageRandomPercent);         // memory waits for the next battle start
    }

    [Fact]
    public void MCM_wins_for_the_key_it_changed_even_over_a_hand_edit()
    {
        var memory = new TraxSettings();
        var disk = Disk("{ \"DamageRandomPercent\": 30 }");
        memory.Set(SettingsSchema.DamageRandomPercent, 40, SettingSources.Mcm);

        var plan = ConfigMerge.ForWrite(disk, memory.Snapshot(), new[] { "DamageRandomPercent" });

        Assert.Equal(40, plan.Values["DamageRandomPercent"]);
    }

    [Fact]
    public void Keys_missing_or_invalid_on_disk_take_memory()
    {
        var memory = new TraxSettings();
        memory.Set(SettingsSchema.MaxAthletics, 150, SettingSources.File);
        var disk = Disk("{ \"CostPerBlow\": \"abc\" }");

        var plan = ConfigMerge.ForWrite(disk, memory.Snapshot(), Array.Empty<string>());

        Assert.Equal(150, plan.Values["MaxAthletics"]);
        Assert.Equal(10, plan.Values["CostPerBlow"]);
        Assert.Equal(SettingsSchema.All.Count, plan.Values.Count);
    }

    [Fact]
    public void A_broken_or_absent_disk_file_means_memory_everywhere()
    {
        var memory = new TraxSettings();
        memory.Set(SettingsSchema.DamageRandomPercent, 20, SettingSources.Mcm);

        foreach (var disk in new[] { null, Disk("{ broken") })
        {
            var plan = ConfigMerge.ForWrite(disk, memory.Snapshot(), Array.Empty<string>());
            Assert.Equal(20, plan.Values["DamageRandomPercent"]);
            Assert.Empty(plan.Unknown);
            Assert.Empty(plan.KeptFromDisk);
        }
    }

    [Fact]
    public void Unknown_keys_are_carried_along()
    {
        var plan = ConfigMerge.ForWrite(Disk("{ \"Typo\": 1 }"), new TraxSettings().Snapshot(), Array.Empty<string>());
        Assert.Equal("Typo", Assert.Single(plan.Unknown).Key);
    }

    [Fact]
    public void Written_plan_reads_back_to_the_same_values()
    {
        var memory = new TraxSettings();
        memory.Set(SettingsSchema.ShowTargetBar, false, SettingSources.Mcm);
        var plan = ConfigMerge.ForWrite(Disk("{ \"DamageRandomPercent\": 30, \"Old\": true }"), memory.Snapshot(), new[] { "ShowTargetBar" });

        var reread = ConfigFile.Read(ConfigFile.Write(plan.Values, plan.Unknown));

        Assert.True(reread.Ok, reread.Error);
        Assert.Empty(reread.Missing);
        Assert.Equal(30, reread.Values["DamageRandomPercent"]);
        Assert.Equal(0, reread.Values["ShowTargetBar"]);
        Assert.Equal("Old", Assert.Single(reread.Unknown).Key);
    }
}

public class EditTrackerTests
{
    [Fact]
    public void A_changed_key_is_reported_until_cleared()
    {
        var s = new TraxSettings();
        var t = new EditTracker();
        t.Note(SettingsSchema.CostPerBlow, s.Get(SettingsSchema.CostPerBlow));
        s.Set(SettingsSchema.CostPerBlow, 12, SettingSources.Mcm);

        Assert.True(t.HasEdits);
        Assert.Equal(new[] { "CostPerBlow" }, t.ChangedKeys(s));

        t.Clear();
        Assert.False(t.HasEdits);
        Assert.Empty(t.ChangedKeys(s));
    }

    [Fact]
    public void Moved_and_moved_back_or_cancelled_is_not_a_change()
    {
        var s = new TraxSettings();
        var t = new EditTracker();
        s.Changed += c => t.Note(c.Param, c.OldValue);

        s.Set(SettingsSchema.DamageRandomPercent, 60, SettingSources.Mcm);
        s.Set(SettingsSchema.DamageRandomPercent, 70, SettingSources.Mcm);
        s.Set(SettingsSchema.DamageRandomPercent, 50, SettingSources.Mcm); // MCM Cancel replays the original

        Assert.Empty(t.ChangedKeys(s));
    }
}

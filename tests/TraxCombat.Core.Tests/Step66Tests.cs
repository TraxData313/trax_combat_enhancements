using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 66 - MORE AI KICKS AND BASHES, EACH COSTING MORE (Anton's looter battle 2026-10-19 14:16: "ai kicks: asked 0" in 49.4 man-minutes of
/// contact; AI_NOTES "Step 66", DESIGN decision 59): the format-13 migration of the kick's cost and cooldown. Step 81 retired the rate, the wait
/// for reach and the bash credit (their tests went with them - Step81Tests holds the shares).
/// </summary>
public class Step66Tests
{
    /// <summary>defaults.json's values today (the tests run on DESIGN's initial 3 / 10): the cost 8, the cooldown 2 (step 81).</summary>
    private static double Shipped(ParamDef p) =>
        p == SettingsSchema.CostPerKickOrBash ? 8.0 : p == SettingsSchema.AiKickCooldownSeconds ? 2.0 : p == SettingsSchema.SwingStackMinimum ? 3 : p.Default;

    [Fact]
    public void Format_13_moves_the_old_kick_cost_and_cooldown_once_keeps_a_tuned_value_and_drops_the_retired_rate()
    {
        Assert.Equal(20, ConfigFile.FormatVersion);
        var read = ConfigFile.Read("{ \"ConfigVersion\": 12, \"CostPerKickOrBash\": 3.0, \"AiKicksPerMinute\": 0.1, \"AiKickCooldownSeconds\": 10.0 }");
        var notes = ConfigFile.Migrate(read, Shipped);
        Assert.Equal(3, notes.Count);
        Assert.StartsWith("CostPerKickOrBash: 3.0 → 8.0 (format 12 → 20, the old default; step 66, Anton: a kick or a shield bash takes more of the energy", notes[0], StringComparison.Ordinal);
        Assert.StartsWith("AiKickCooldownSeconds: 10.0 → 2.0 (format 12 → 20, the old default; step 66", notes[1], StringComparison.Ordinal);
        Assert.StartsWith("AiKicksPerMinute 0.1 dropped (format 12 → 20; step 81, Toni: AI kicks and bashes are a share of the melee moves now", notes[2], StringComparison.Ordinal);
        Assert.Equal(8, read.Values["CostPerKickOrBash"], 9);
        Assert.Equal(2, read.Values["AiKickCooldownSeconds"], 9);
        Assert.False(read.Values.ContainsKey("AiKicksPerMinute"));
        // a hand-set value is kept and said once; an older file's 3 moves too (the key since step 18)
        var mine = ConfigFile.Read("{ \"ConfigVersion\": 12, \"CostPerKickOrBash\": 4.0 }");
        var kept = ConfigFile.Migrate(mine, Shipped);
        Assert.Single(kept);
        Assert.StartsWith("CostPerKickOrBash: 4.0 kept - your own value (format 12 → 20; the new default 8.0", kept[0], StringComparison.Ordinal);
        Assert.Equal(4, mine.Values["CostPerKickOrBash"], 9);
        var old = ConfigFile.Read("{ \"ConfigVersion\": 5, \"CostPerKickOrBash\": 3.0 }");
        Assert.Contains(ConfigFile.Migrate(old, Shipped), n => n.StartsWith("CostPerKickOrBash: 3.0 → 8.0 (format 5 → 20", StringComparison.Ordinal));
        // a file without the keys gets the defaults (no note)
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 12 }"), Shipped));
    }
}

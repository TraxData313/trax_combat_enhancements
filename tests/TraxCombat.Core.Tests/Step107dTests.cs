using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 107d - the hold-block openings' review: the [summary] line names the two counts the review fixed (a winning swing stopped anyway - counted at
/// the contact now; step 81's kick roll skipped on the forced counter-swing's own wind-up). The module paths are proven by the offline smoke (4b).
/// </summary>
public class Step107dTests
{
    private static OpeningHoldRules Rules()
    {
        var s = new TraxSettings();
        s.Set(SettingsSchema.OpeningsEnabled, true, SettingSources.File);
        return OpeningHoldRules.From(s);
    }

    [Fact]
    public void The_summary_line_counts_the_blocked_wins_and_the_skipped_kick_rolls()
    {
        var r = Rules();
        var s = new OpeningHoldStats { WinBlocked = 2, CounterKickRollsSkipped = 3, CounterReleased = 4 };
        string line = s.SummaryLine(in r);
        Assert.Contains("a winning swing blocked anyway 2", line);
        Assert.Contains("(released 4, cut short 0, refused 0, step 81's kick roll skipped for it 3)", line);
    }
}

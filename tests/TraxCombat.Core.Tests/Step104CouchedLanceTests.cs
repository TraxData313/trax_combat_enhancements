using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 104 follow-up (Toni 2026-11-09): a COUCHED LANCE (the engine's passive attack - a braced spear too) is never a perfect strike,
/// on foot or mounted; a normal swing / thrust still counts (AI_NOTES "104 follow-up", DESIGN decision 93).
/// </summary>
public class Step104CouchedLanceTests
{
    [Theory]
    [InlineData(false, false, true, 20f)]  // a clean couch on his body
    [InlineData(true, false, true, 20f)]   // whatever else the collision says, the couch decides first
    [InlineData(false, true, true, 20f)]
    [InlineData(false, false, false, 0f)]
    public void A_couched_lance_is_refused_first(bool alternative, bool shield, bool struck, float damage)
    {
        Assert.Equal(StrikeGate.Couched, PerfectStrikeMath.Gate(true, alternative, shield, struck, damage));
    }

    [Fact]
    public void A_normal_swing_or_thrust_on_his_body_still_counts()
    {
        Assert.Equal(StrikeGate.Counts, PerfectStrikeMath.Gate(false, false, false, true, 20f));
        // and its verdict is untouched: any side on a reeling rider, strict opposites on foot
        Assert.Equal(StrikeVerdict.Perfect, PerfectStrikeMath.Verdict(true, true, PerfectStrikeMath.Right, PerfectStrikeMath.Left));
        Assert.Equal(StrikeVerdict.Perfect, PerfectStrikeMath.Verdict(false, true, PerfectStrikeMath.Right, PerfectStrikeMath.Right));
        Assert.Equal(StrikeVerdict.WrongSide, PerfectStrikeMath.Verdict(false, true, PerfectStrikeMath.Right, PerfectStrikeMath.Left));
    }

    [Theory]
    [InlineData(true, false, true, 20f)]   // a kick / bash
    [InlineData(false, true, true, 20f)]   // into his shield
    [InlineData(false, false, false, 20f)] // blocked / parried
    [InlineData(false, false, true, 0f)]   // no damage
    public void Off_his_body_stays_not_on_body(bool alternative, bool shield, bool struck, float damage)
    {
        Assert.Equal(StrikeGate.NotOnBody, PerfectStrikeMath.Gate(false, alternative, shield, struck, damage));
    }

    [Fact]
    public void The_summary_names_couched_hits_only_when_there_were_some()
    {
        var r = PerfectStrikeRules.From(new TraxSettings());
        var st = new PerfectStrikeStats();
        Assert.DoesNotContain("couched", st.SummaryLine(in r));
        st.Couched = 2;
        Assert.Contains("your couched-lance hits on a window man (never a perfect strike, the window kept) 2", st.SummaryLine(in r));
    }
}

using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>Step 30 (Anton, 2026-10-12: "the more exhausted an AI man is, the more his block direction is random"): w =
/// TiredBlockRandomPercent / 100 × (1 − f), written into the game's own AiRandomizedDefendDirectionChance as
/// own + (1 − own) × w; 0 = the game's value untouched. AI_NOTES "Step 30".</summary>
public class TiredBlockTests
{
    private static TiredBlockRules On(int percent = 100, bool mod = true, bool athletics = true) => new(mod, athletics, percent);

    /// <summary>Peak line 75%, pool 100 at skill 100.</summary>
    private static AthleticsRules Athletics(bool mod = true, bool enabled = true)
        => new(enabled, 50, 1.0f, 75, true, 10, true, 0.75f, 0.75f, 3, 20, 0.3f, 1.0f, true, 2, 1.5f, 60, 0.5f, 0.4f, 100, mod, 1, 5, 2, true);

    [Theory]
    [InlineData(100, 1.0, 0.0)]
    [InlineData(100, 0.5, 0.5)]
    [InlineData(100, 0.0, 1.0)]
    [InlineData(50, 1.0, 0.0)]
    [InlineData(50, 0.5, 0.25)]
    [InlineData(50, 0.0, 0.5)]
    [InlineData(0, 1.0, 0.0)]
    [InlineData(0, 0.5, 0.0)]
    [InlineData(0, 0.0, 0.0)]
    public void The_weight_is_the_setting_times_how_far_below_the_peak_line(int percent, double f, double expected)
    {
        Assert.Equal(expected, TiredBlockMath.Weight(On(percent), f, isPlayer: false), 6);
    }

    [Fact]
    public void Never_the_player_and_never_while_off_master_switch_first()
    {
        Assert.Equal(0f, TiredBlockMath.Weight(On(), 0.0, isPlayer: true));
        Assert.Equal(0f, TiredBlockMath.Weight(On(mod: false), 0.0, false));
        Assert.Equal(0f, TiredBlockMath.Weight(On(athletics: false), 0.0, false));
        Assert.Equal("ModEnabled", On(0, mod: false, athletics: false).OffBecause);
        Assert.Equal("AthleticsEnabled", On(100, athletics: false).OffBecause);
        Assert.Equal("TiredBlockRandomPercent 0", On(0).OffBecause);
        Assert.Null(On().OffBecause);
        Assert.Equal(0f, TiredBlockMath.Weight(On(), double.NaN, false));
        Assert.Equal(1f, TiredBlockMath.Weight(On(), -0.2, false)); // below 0 reads as empty
    }

    [Fact]
    public void The_fighters_weight_reads_f_off_his_bar_and_athletics_off_means_full_strength()
    {
        var ar = Athletics();
        var man = new Fighter { AthleticsSkill = 100 };
        Assert.Equal(0f, TiredBlockMath.Weight(On(), ar, man, false));       // full
        AthleticsMath.Charge(man, ar, 0);                                     // 100 → 90: f = 1 (above the 75 line)
        Assert.Equal(0f, TiredBlockMath.Weight(On(), ar, man, false));
        for (int i = 0; i < 5; i++) AthleticsMath.Charge(man, ar, i + 1);    // 90 − 5 × 10 = 40 → f = 40 / 75
        Assert.Equal(1.0 - 40.0 / 75.0, TiredBlockMath.Weight(On(), ar, man, false), 5);
        Assert.Equal(0f, TiredBlockMath.Weight(On(), Athletics(enabled: false), man, false));
        Assert.Equal(0f, TiredBlockMath.Weight(On(), Athletics(mod: false), man, false));
    }

    [Fact]
    public void The_chance_is_own_plus_the_rest_times_w_and_w_0_leaves_the_games_value()
    {
        Assert.Equal(-1.1f, TiredBlockMath.Chance(-1.1f, 0f));               // untouched, even negative
        Assert.Equal(0.3f, TiredBlockMath.Chance(0.3f, 0f));
        Assert.Equal(0.5f, TiredBlockMath.Chance(-1.1f, 0.5f), 6);           // the tournament's −1.10 counts as 0
        Assert.Equal(1f, TiredBlockMath.Chance(-1.1f, 1f), 6);
        Assert.Equal(0.3f + 0.7f * 0.5f, TiredBlockMath.Chance(0.3f, 0.5f), 6);
        Assert.Equal(1f, TiredBlockMath.Chance(0.3f, 1f), 6);
        Assert.Equal(1f, TiredBlockMath.Chance(0.3f, 7f), 6);                // w clamped
        Assert.Equal(1f, TiredBlockMath.Chance(1.4f, 0.2f), 6);              // own clamped
        Assert.Equal(0.2f, TiredBlockMath.Chance(float.NaN, 0.2f), 6);
        // Anton's f 0.5 at 100: half his picks random → right 1 − 0.5 × 3/4 = 62.5% (own AI always right)
        Assert.Equal(0.625, TiredBlockMath.RightShare(TiredBlockMath.Chance(0f, 0.5f)), 6);
        Assert.Equal(0.25, TiredBlockMath.RightShare(1f), 6);
    }

    [Fact]
    public void The_setting_is_live_and_in_the_tired_group_after_the_damage_upside()
    {
        var s = new TraxSettings();
        Assert.Equal(100, s.TiredBlockRandomPercent);                         // DESIGN's initial value
        Assert.True(TiredBlockRules.From(s).Enabled);
        s.Set(SettingsSchema.TiredBlockRandomPercent, 50, SettingSources.Mcm);
        Assert.Equal(0.5f, TiredBlockRules.From(s).Share);
        s.Set(SettingsSchema.TiredBlockRandomPercent, 0, SettingSources.Mcm);
        Assert.False(TiredBlockRules.From(s).Enabled);
        Assert.Equal(SettingsSchema.TiredGroup, SettingsSchema.TiredBlockRandomPercent.Group);
        Assert.Equal(SettingsSchema.DamageBonusFollowsAthletics.Index + 1, SettingsSchema.TiredBlockRandomPercent.Index);
        Assert.Equal(0, SettingsSchema.TiredBlockRandomPercent.Min);
        Assert.Equal(100, SettingsSchema.TiredBlockRandomPercent.Max);
    }

    [Fact]
    public void The_sentence_says_on_or_off_and_why()
    {
        Assert.Contains("weight w = 100% × (1 − f)", On().Describe());
        Assert.Contains("at f 0.5 50% of his picks and empty 100%", On().Describe());
        Assert.Contains("at f 0.5 25% of his picks and empty 50%", On(50).Describe());
        Assert.Equal("tired block direction: off (ModEnabled) - every AI man blocks as the game decides", On(mod: false).Describe());
        Assert.Contains("off (TiredBlockRandomPercent 0)", On(0).Describe());
    }

    [Fact]
    public void The_summary_counts_the_raised_chance_and_the_guard_by_f_band()
    {
        var st = new TiredBlockStats { Men = 2 };
        st.AddRaised(1, -1.1f, 0.5f, true);
        st.AddRaised(1, 0.2f, 0.6f, false);
        st.AddRaised(3, -1.1f, 1f, false);
        st.AddRaised(7, 0f, 1f, true); // a bad bin is ignored
        for (int i = 0; i < 8; i++) st.AddGuard(0, BlockKind.ShieldRightSide, true);
        st.AddGuard(0, BlockKind.WeaponParry, false);
        st.AddGuard(0, BlockKind.None, false);
        st.AddGuard(3, BlockKind.ShieldWrongSide, true);
        st.AddGuard(3, BlockKind.None, false);
        st.AddGuard(3, BlockKind.None, false);
        st.AddGuard(3, BlockKind.ShieldRightSide, true);
        Assert.Equal(3, st.Recomputes);
        Assert.Equal(1, st.RecomputesWithShield);
        Assert.Equal(2, st.NegativeOwn);
        Assert.Equal(2, st.Raised(1));
        Assert.Equal(2, st.Landed(3));
        Assert.Equal(1, st.WrongSide(3));

        var lines = st.SummaryLines(On());
        Assert.Equal(4, lines.Count);
        Assert.Equal("tired block direction (TiredBlockRandomPercent 100, step 30): the game's random-direction chance raised for 2 AI men (3 recomputes - "
                     + "1 with a shield in hand, 2 without; "
                     + "2 of them over a game value below 0 - never random before); his own → written, avg by f band: f 0.5-1 0.10 → 0.55 (n 2), empty (f 0) 0.00 → 1.00 (n 1)",
            lines[0]);
        Assert.Equal("AI guard by f (melee blows that reached AI men; kicks, bashes, charges and missiles left out - step 30: tired men should block worse): "
                     + "peak (f 1) 10 blows - blocked 90.0% (shield right side 80.0%, WRONG side 0.0%, parry 10.0%), landed 10.0%; f 0.5-1 none; f below 0.5 none; "
                     + "empty (f 0) 4 blows - blocked 50.0% (shield right side 25.0%, WRONG side 25.0%, parry 0.0%), landed 50.0%",
            lines[1]);
        Assert.Equal("AI guard by f WITH a shield in hand (when the blow came - step 34): "
                     + "peak (f 1) 8 blows - blocked 100.0% (shield right side 100.0%, WRONG side 0.0%, parry 0.0%), landed 0.0%; f 0.5-1 none; f below 0.5 none; "
                     + "empty (f 0) 2 blows - blocked 100.0% (shield right side 50.0%, WRONG side 50.0%, parry 0.0%), landed 0.0% "
                     + "(a wrong direction with a shield = a WRONG-side block: the shield still stops the blow)",
            lines[2]);
        Assert.Equal("AI guard by f WITHOUT a shield (when the blow came - step 34): "
                     + "peak (f 1) 2 blows - blocked 50.0% (shield right side 0.0%, WRONG side 0.0%, parry 50.0%), landed 50.0%; f 0.5-1 none; f below 0.5 none; "
                     + "empty (f 0) 2 blows - blocked 0.0% (shield right side 0.0%, WRONG side 0.0%, parry 0.0%), landed 100.0% "
                     + "(a wrong direction without a shield = the blow lands: empty should land far more than peak)",
            lines[3]);
    }

    [Fact]
    public void The_guard_split_by_a_shield_in_hand_adds_up_to_the_total()
    {
        var st = new TiredBlockStats();
        st.AddGuard(2, BlockKind.ShieldWrongSide, true);
        st.AddGuard(2, BlockKind.WeaponParry, false);
        st.AddGuard(2, BlockKind.None, false);
        st.AddGuard(2, BlockKind.None, true);
        st.AddGuard(-1, BlockKind.None, true); // a bad bin is ignored in every table
        Assert.Equal(4, st.Blows(2));
        Assert.Equal(2, st.Blows(2, true));
        Assert.Equal(2, st.Blows(2, false));
        Assert.Equal(st.Landed(2), st.Landed(2, true) + st.Landed(2, false));
        Assert.Equal(1, st.WrongSide(2, true));
        Assert.Equal(0, st.WrongSide(2, false));
        Assert.Equal(1, st.Parried(2, false));
        Assert.Equal(0, st.Blows(0, true) + st.Blows(0, false));
    }

    [Fact]
    public void The_summary_when_off_and_never_raised_says_so()
    {
        var lines = new TiredBlockStats().SummaryLines(On(0));
        Assert.Equal("tired block direction (step 30): off at the end (TiredBlockRandomPercent 0) and never raised this battle - the game's own blocking", lines[0]);
        Assert.EndsWith("(no melee blow reached an AI man)", lines[1]);
        Assert.EndsWith("none (no melee blow reached an AI man holding a shield) (a wrong direction with a shield = a WRONG-side block: the shield still stops the blow)", lines[2]);
        Assert.EndsWith("none (no melee blow reached an AI man without a shield) (a wrong direction without a shield = the blow lands: empty should land far more than peak)", lines[3]);
        var raisedThenOff = new TiredBlockStats { Men = 1 };
        raisedThenOff.AddRaised(2, 0f, 0.7f, false);
        Assert.EndsWith(" - off at the end (ModEnabled)", raisedThenOff.SummaryLines(On(mod: false))[0]);
    }
}

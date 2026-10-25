using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 91 - SHIELD BASHES GET THEIR OWN SHORTER REACH (Toni's 19:56 battle of 2026-10-24: 114 of 150 AI bash hits on a FRIEND, 100 on the basher's
/// shield side - the bash was rolled at the kick's 1.6 m and its short sweep passed the enemy into the neighbour; AI_NOTES "Step 91", DESIGN decision
/// 84): the setting placed / shipped / live, 0 = off, beyond the bash reach a shieldman's bash share split between kick and swing (25 / 35 → 41.7 /
/// 58.3), within it 25 / 40 / 35, the measurement and the 8th summary line, 0 bytes.
/// </summary>
public class Step91Tests
{
    private const double Reach = 1.6;

    /// <summary>defaults.json's shipped kick rules (steps 88 / 90) with the bash reach <paramref name="bashReach"/>.</summary>
    private static AiKickRules Shipped(double bashReach = 1.1) =>
        new AiKickRules(true, true, true, Reach, 40, 2, true, 25, 40, 35, 0.45, 0.5, 1.2, 20, 0.5, 0.4, bashReach);

    [Fact]
    public void The_setting_sits_after_the_kick_reach_on_the_Advanced_page_live_and_ships_off_since_step_91b()
    {
        var all = SettingsSchema.All.ToList();
        var p = SettingsSchema.AiBashReachMetres;
        Assert.Equal(all.IndexOf(SettingsSchema.AiKickReachMetres) + 1, all.IndexOf(p));
        Assert.Same(SettingsSchema.ImpactGroup, p.Group);
        Assert.Equal(SettingsPage.Advanced, p.Group.Page);
        Assert.Equal(ApplyTiming.Live, p.Timing);
        Assert.Equal(ParamType.Float, p.Type);
        Assert.Equal(0, p.Min, 9);
        Assert.Equal(4, p.Max, 9);
        Assert.Equal(1.1, p.Default, 9); // DESIGN's initial value (the tests' defaults)
        Assert.Contains("\"AiBashReachMetres\": 0.0,", DefaultsFile.ReadEmbeddedText(), StringComparison.Ordinal); // step 91b: shipped 0 (off)
        // live: read at use time through the rules
        var s = new TraxSettings();
        Assert.Equal(1.1, AiKickRules.From(s).BashReach, 6);
        Assert.True(AiKickRules.From(s).BashReachOn);
        s.Set(SettingsSchema.AiBashReachMetres, 0.9, "test");
        Assert.Equal(0.9, AiKickRules.From(s).BashReach, 6);
        Assert.Contains("step 91: a shield bash only within 0.90 m (AiBashReachMetres)", AiKickRules.From(s).Describe(), StringComparison.Ordinal);
        // the master switch still comes first - the reach never turns the kicks on
        s.Set(SettingsSchema.ModEnabled, false, "test");
        Assert.False(AiKickRules.From(s).Enabled);
        Assert.Equal("ModEnabled", AiKickRules.From(s).OffBecause);
    }

    [Fact]
    public void Zero_or_a_reach_at_the_kick_reach_is_off_and_rolls_exactly_as_step_81()
    {
        foreach (double setting in new[] { 0.0, 1.6, 3.0, double.NaN, -1 })
        {
            var r = Shipped(setting);
            Assert.False(r.BashReachOn);
            Assert.Equal(Reach, r.BashReach, 9);
            Assert.False(r.BeyondBashReach(1.5));
            for (double u = 0; u < 1; u += 0.01)
                foreach (bool shield in new[] { true, false })
                    Assert.Equal(AiKickMath.Roll(in r, shield, u), AiKickMath.Roll(in r, shield, u, 1.5));
            Assert.Contains("step 91: the shield bash at the kick reach (AiBashReachMetres off)", r.Describe(), StringComparison.Ordinal);
        }
        // the old constructor (no bash reach) = off
        Assert.False(new AiKickRules(true, true, true, Reach, 40, 2, true, 25, 40, 35).BashReachOn);
    }

    [Fact]
    public void Beyond_the_bash_reach_the_bash_share_splits_into_kick_and_swing_in_their_own_proportion()
    {
        var r = Shipped();
        Assert.Equal(25.0 / 60.0, r.ShieldKickBeyondBashReach, 9); // 41.7 / 58.3
        Assert.False(r.BeyondBashReach(1.1)); // the edge is within
        Assert.True(r.BeyondBashReach(1.1001));
        // the boundaries: within = step 81 / 88's die, beyond = kick below 0.4167
        Assert.Equal(AiKickKind.Bash, AiKickMath.Roll(in r, true, 0.30, 1.0));
        Assert.Equal(AiKickKind.Kick, AiKickMath.Roll(in r, true, 0.30, 1.3));
        Assert.Equal(AiKickKind.Kick, AiKickMath.Roll(in r, true, 0.4166, 1.3));
        Assert.Equal(AiKickKind.None, AiKickMath.Roll(in r, true, 0.4168, 1.3));
        Assert.Equal(AiKickKind.None, AiKickMath.Roll(in r, true, 0.66, 1.3));
        Assert.Equal(AiKickKind.None, AiKickMath.Roll(in r, true, double.NaN, 1.3));
        // a man without a shield pushes and kicks at the kick reach as before
        Assert.Equal(AiKickKind.Push, AiKickMath.Roll(in r, false, 0.40, 1.5));
        // a 100% bash has nothing to split: beyond the reach he swings
        var allBash = new AiKickRules(true, true, true, Reach, 40, 2, true, 0, 100, 35, 0, 0, 0, 0, 0, 0, 1.1);
        Assert.Equal(0, allBash.ShieldKickBeyondBashReach, 9);
        Assert.Equal(AiKickKind.None, AiKickMath.Roll(in allBash, true, 0.01, 1.3));
        Assert.Contains("farther off, to the kick reach, a shieldman kicks 42% / swings 58%", r.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void Seeded_rolls_land_near_41_7_and_58_3_beyond_and_25_40_35_within()
    {
        var r = Shipped();
        const int n = 100000;
        var dice = new SeededRandom(91);
        int[] beyond = new int[AiKickMath.KindCount], within = new int[AiKickMath.KindCount], noShield = new int[AiKickMath.KindCount];
        for (int i = 0; i < n; i++)
        {
            beyond[(int)AiKickMath.Roll(in r, true, dice.NextDouble(), 1.35)]++;
            within[(int)AiKickMath.Roll(in r, true, dice.NextDouble(), 0.95)]++;
            noShield[(int)AiKickMath.Roll(in r, false, dice.NextDouble(), 1.35)]++;
        }
        double P(int c) => 100.0 * c / n;
        Assert.InRange(P(beyond[(int)AiKickKind.Kick]), 41.2, 42.2);
        Assert.Equal(0, beyond[(int)AiKickKind.Bash]);
        Assert.InRange(P(beyond[(int)AiKickKind.None]), 57.8, 58.8);
        Assert.InRange(P(within[(int)AiKickKind.Kick]), 24.5, 25.5);
        Assert.InRange(P(within[(int)AiKickKind.Bash]), 39.5, 40.5);
        Assert.InRange(P(within[(int)AiKickKind.None]), 34.5, 35.5);
        Assert.InRange(P(noShield[(int)AiKickKind.Kick]), 34.5, 35.5); // no shield: 35 / 20 / 45 wherever in kick reach
        Assert.InRange(P(noShield[(int)AiKickKind.Push]), 19.5, 20.5);
    }

    [Fact]
    public void The_stats_bin_each_bash_by_its_distance_and_the_8th_line_says_it_in_plain_words()
    {
        Assert.Equal(7, AiBashReachStats.BinCount);
        Assert.Equal(0, AiBashReachStats.BinOf(0.8));
        Assert.Equal(1, AiBashReachStats.BinOf(0.81));
        Assert.Equal(2, AiBashReachStats.BinOf(1.1));
        Assert.Equal(3, AiBashReachStats.BinOf(1.15));
        Assert.Equal(5, AiBashReachStats.BinOf(1.6));
        Assert.Equal(6, AiBashReachStats.BinOf(1.7));
        Assert.Equal("1.0-1.1 m", AiBashReachStats.BinName(2));
        Assert.Equal(">1.6 m", AiBashReachStats.BinName(6));
        var st = new AiKickStats();
        var b = st.BashReach;
        b.NoteShieldRoll(false, AiKickKind.Bash, 0.9);
        b.NoteShieldRoll(false, AiKickKind.Kick, 0.9);
        b.NoteShieldRoll(true, AiKickKind.Kick, 1.3);
        b.NoteShieldRoll(true, AiKickKind.None, 1.3);
        b.NoteAsked(0.9, false);
        b.NoteResult(0.9, AiBashResult.Friend);
        b.NoteAsked(0.75, false);
        b.NoteResult(0.75, AiBashResult.Enemy);
        b.PressBeyond = 2;
        Assert.Equal(1, b.Asked(1));
        Assert.Equal(1, b.Result(1, AiBashResult.Friend));
        Assert.Equal(1.0, b.FriendShare(1), 9);
        Assert.Equal(2, b.AskedTotal());
        var lines = st.SummaryLines(Shipped()).ToList();
        Assert.Equal(9, lines.Count); // step 95: + the step-in bash (the 9th)
        string line = lines[7];
        Assert.StartsWith("ai kicks - the shield bash's reach (step 91): a shieldman bashes only within 1.10 m (AiBashReachMetres; kicks and pushes keep the 1.6 m kick reach) - farther off he kicks 42% / swings 58%", line, StringComparison.Ordinal);
        Assert.Contains(" | shieldmen's moves within it 2 - rolled kick 50.0% / bash 50.0% / swing 0.0% (target 25% / 40% / 35%)", line, StringComparison.Ordinal);
        Assert.Contains(" | beyond it 2 - rolled kick 50.0% / bash 0.0% / swing 50.0% (target 42% / 0% / 58%)", line, StringComparison.Ordinal);
        Assert.Contains("bashes by the enemy's distance at the decision - rolled, asked → on an ENEMY / into his shield / on a FRIEND / missed / horse or untracked / not played: ≤0.8 m 0, 1 → 1 / 0 / 0 / 0 / 0 / 0 (friends 0% of the hits); 0.8-1.0 m 1, 1 → 0 / 0 / 1 / 0 / 0 / 0 (friends 100% of the hits)", line, StringComparison.Ordinal);
        Assert.EndsWith(" | asked beyond the bash reach 0; a bash's re-read after its guard found the enemy beyond the bash reach 2 (ended, no bash)", line, StringComparison.Ordinal);
        // off: said plainly
        Assert.StartsWith("ai kicks - the shield bash's reach (step 91): the bash reach is OFF - a shieldman bashes at the kick reach, anywhere within 1.6 m (AiBashReachMetres 0; the bins below still say where his bashes land)", new AiKickStats().BashReach.Line(Shipped(0)), StringComparison.Ordinal);
        Assert.Contains("bashes by the enemy's distance at the decision - rolled, asked → on an ENEMY / into his shield / on a FRIEND / missed / horse or untracked / not played: none", new AiKickStats().BashReach.Line(Shipped(0)), StringComparison.Ordinal);
    }

    [Fact]
    public void The_roll_and_the_notes_allocate_nothing()
    {
        var r = Shipped();
        var b = new AiBashReachStats();
        var dice = new SeededRandom(7);
        AiKickMath.Roll(in r, true, 0.5, 1.3);
        b.NoteShieldRoll(true, AiKickKind.Kick, 1.3);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++)
        {
            double d = 0.6 + (i % 11) * 0.1;
            var k = AiKickMath.Roll(in r, true, dice.NextDouble(), d);
            b.NoteShieldRoll(r.BeyondBashReach(d), k, d);
            if (k == AiKickKind.Bash)
            {
                b.NoteAsked(d, r.BeyondBashReach(d));
                b.NoteResult(d, AiBashResult.Enemy);
            }
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(0, b.AskedBeyond);
    }
}

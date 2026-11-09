using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 100g - THE PERFECT STRIKE (Toni 2026-11-08: "if I attack in the opposite way (say I blocked his right attack, coming from my left and then
/// swing from my right to left) after I blocked him and he is staggered I do 2-3x damage"; AI_NOTES "Step 100" - "100g - built", DESIGN decision
/// 91 (j)): the direction mapping (Toni's exact example first), the multiplier, the window, the book, the rules and the summary line.
/// </summary>
public class PerfectStrikeTests
{
    private const int Up = PerfectStrikeMath.Up, Down = PerfectStrikeMath.Down, Left = PerfectStrikeMath.Left, Right = PerfectStrikeMath.Right;

    private static TraxSettings Settings(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        s.Set(SettingsSchema.ModEnabled, 1, "test");
        s.Set(SettingsSchema.AthleticsEnabled, 1, "test");
        s.Set(SettingsSchema.ImpactEnabled, 1, "test");
        s.Set(SettingsSchema.TimedBlocksEnabled, 1, "test");
        s.Set(SettingsSchema.PerfectStrikeEnabled, 1, "test");
        s.Set(SettingsSchema.PerfectStrikeThreeWayMin, 2, "test"); // step 106d: the old flat x2-x3 = today's no-thrust range (Axe below)
        s.Set(SettingsSchema.PerfectStrikeThreeWayMax, 3, "test");
        s.Set(SettingsSchema.PerfectStrikeGraceSeconds, 0.15, "test");
        s.Set(SettingsSchema.PerfectStrikeCentreMessage, 1, "test");
        tune?.Invoke(s);
        return s;
    }

    private static PerfectStrikeRules Rules(Action<TraxSettings>? tune = null) => PerfectStrikeRules.From(Settings(tune));

    /// <summary>Step 106d: a no-thrust weapon (3 directions) - its range is the old x2-x3.</summary>
    private static readonly int Axe = OpeningMath.WeaponMask(true, false);

    // ------------------------------------------------------------------ the direction mapping

    [Fact]
    public void Tonis_example_his_blocked_right_attack_answered_by_your_swing_from_your_right_to_left_is_perfect()
    {
        // he attacks with HIS right swing (Agent.UsageDirection.AttackRight 3): it comes at YOUR left (the training field: AttackRight → "Defend from left")
        Assert.Equal(BodySide.Left, PerfectStrikeMath.ArrivesAtDefenderSide(Right));
        // you swing from YOUR right to your left: that is YOUR AttackRight 3 (wound up on your own right)
        Assert.Equal(BodySide.Right, PerfectStrikeMath.FromOwnSide(Right));
        // opposite sides in your frame = the SAME engine value - perfect
        Assert.Equal(StrikeVerdict.Perfect, PerfectStrikeMath.Verdict(blocked: Right, strike: Right));
        // swinging from your left (your AttackLeft 2) goes back along his blocked swing's side - wrong side
        Assert.Equal(StrikeVerdict.WrongSide, PerfectStrikeMath.Verdict(blocked: Right, strike: Left));
    }

    [Fact]
    public void The_mirror_case_his_left_attack_is_answered_by_your_left_swing()
    {
        Assert.Equal(BodySide.Right, PerfectStrikeMath.ArrivesAtDefenderSide(Left));
        Assert.Equal(StrikeVerdict.Perfect, PerfectStrikeMath.Verdict(Left, Left));
        Assert.Equal(StrikeVerdict.WrongSide, PerfectStrikeMath.Verdict(Left, Right));
    }

    [Fact]
    public void After_a_blocked_side_swing_your_overhead_and_thrust_do_not_count()
    {
        foreach (int blocked in new[] { Left, Right })
        {
            Assert.Equal(StrikeVerdict.WrongSide, PerfectStrikeMath.Verdict(blocked, Up));
            Assert.Equal(StrikeVerdict.WrongSide, PerfectStrikeMath.Verdict(blocked, Down));
        }
    }

    [Fact]
    public void Tonis_case_step_103_a_blocked_overhead_is_answered_by_your_thrust_from_below_x2_to_x3()
    {
        // Toni 2026-11-09: "when I block a top swing and then hit from the bottom it doesn't perfect strike" - now it does
        Assert.Equal(Down, PerfectStrikeMath.Opposite(Up));
        Assert.Equal(StrikeVerdict.Perfect, PerfectStrikeMath.Verdict(blocked: Up, strike: Down));
        var r = Rules();
        Assert.Equal(2.0, PerfectStrikeMath.Multiplier(0, in r, Axe), 9);
        Assert.Equal(3.0, PerfectStrikeMath.Multiplier(1, in r, Axe), 9);
        Assert.Equal(60f, PerfectStrikeMath.Apply(20f, PerfectStrikeMath.Multiplier(1, in r, Axe)), 3);
    }

    [Fact]
    public void Strict_opposites_a_blocked_overhead_counts_only_your_thrust_not_side_swings_or_an_overhead()
    {
        // Toni's correction 2026-11-09: "top-down, down-top, not top/down-left/right"
        Assert.Equal(StrikeVerdict.WrongSide, PerfectStrikeMath.Verdict(Up, Left));
        Assert.Equal(StrikeVerdict.WrongSide, PerfectStrikeMath.Verdict(Up, Right));
        Assert.Equal(StrikeVerdict.WrongSide, PerfectStrikeMath.Verdict(Up, Up));
    }

    [Fact]
    public void Strict_opposites_a_blocked_thrust_counts_only_your_overhead()
    {
        Assert.Equal(Up, PerfectStrikeMath.Opposite(Down));
        Assert.Equal(StrikeVerdict.Perfect, PerfectStrikeMath.Verdict(Down, Up));
        Assert.Equal(StrikeVerdict.WrongSide, PerfectStrikeMath.Verdict(Down, Left));
        Assert.Equal(StrikeVerdict.WrongSide, PerfectStrikeMath.Verdict(Down, Right));
        Assert.Equal(StrikeVerdict.WrongSide, PerfectStrikeMath.Verdict(Down, Down));
    }

    [Fact]
    public void Side_swings_keep_the_same_engine_value_as_their_opposite()
    {
        Assert.Equal(Left, PerfectStrikeMath.Opposite(Left));
        Assert.Equal(Right, PerfectStrikeMath.Opposite(Right));
        Assert.Equal(-1, PerfectStrikeMath.Opposite(-1));
    }

    [Fact]
    public void An_unknown_blocked_direction_counts_nothing()
    {
        foreach (int blocked in new[] { -1, 7 })
            foreach (int strike in new[] { Up, Down, Left, Right })
                Assert.Equal(StrikeVerdict.BlockedUnknown, PerfectStrikeMath.Verdict(blocked, strike));
    }

    [Fact]
    public void An_unreadable_strike_direction_is_neither_perfect_nor_a_wrong_side()
    {
        foreach (int blocked in new[] { Up, Down, Left, Right, -1 })
        {
            Assert.Equal(StrikeVerdict.Unknown, PerfectStrikeMath.Verdict(blocked, -1));
            Assert.Equal(StrikeVerdict.Unknown, PerfectStrikeMath.Verdict(blocked, 4)); // a defend direction is no attack
        }
    }

    [Fact]
    public void The_directions_are_the_engines_and_the_recoils()
    {
        Assert.Equal(0, Up);
        Assert.Equal(1, Down);
        Assert.Equal(2, Left);
        Assert.Equal(3, Right);
        Assert.Equal(TimedRecoilMath.Right, Right);
        Assert.Equal("right swing", PerfectStrikeMath.Name(Right));
        Assert.Equal(BodySide.None, PerfectStrikeMath.FromOwnSide(Up));
        Assert.Equal(BodySide.None, PerfectStrikeMath.ArrivesAtDefenderSide(Down));
    }

    // ------------------------------------------------------------------ the multiplier and the window

    [Fact]
    public void The_multiplier_is_2_at_timing_0_rising_straight_to_3_at_timing_1()
    {
        Assert.Equal(2.0, PerfectStrikeMath.Multiplier(0, 2, 3), 9);
        Assert.Equal(2.5, PerfectStrikeMath.Multiplier(0.5, 2, 3), 9);
        Assert.Equal(2.8, PerfectStrikeMath.Multiplier(0.8, 2, 3), 9);
        Assert.Equal(3.0, PerfectStrikeMath.Multiplier(1, 2, 3), 9);
        Assert.Equal(3.0, PerfectStrikeMath.Multiplier(1.7, 2, 3), 9); // clamped
        Assert.Equal(2.0, PerfectStrikeMath.Multiplier(-1, 2, 3), 9);
        Assert.Equal(2.0, PerfectStrikeMath.Multiplier(double.NaN, 2, 3), 9);
        Assert.Equal(5.0, PerfectStrikeMath.Multiplier(1, 2, 99), 9); // the range 1-5
        Assert.Equal(1.0, PerfectStrikeMath.Multiplier(0, 0.2, 3), 9);
        var r = Rules();
        Assert.Equal(2.8, PerfectStrikeMath.Multiplier(0.8, in r, Axe), 5);
        Assert.Equal("PERFECT STRIKE x2.8", PerfectStrikeMath.Text(2.8));
    }

    [Fact]
    public void Applied_last_the_hit_lands_exactly_k_times_and_0_stays_0()
    {
        Assert.Equal(75f, PerfectStrikeMath.Apply(25f, 3), 4);
        Assert.Equal(56f, PerfectStrikeMath.Apply(20f, 2.8), 4);
        Assert.Equal(0f, PerfectStrikeMath.Apply(0f, 3));
        Assert.Equal(-1f, PerfectStrikeMath.Apply(-1f, 3));
        Assert.Equal(20f, PerfectStrikeMath.Apply(20f, double.NaN));
        // the pipeline's steps are multiplications, so the multiplier last = k x the same hit: roll 1.2, the reduction 25 %, x2
        float game = 40f;
        float without = DamageReduction.Apply(game * 1.2f, 25);
        float with = PerfectStrikeMath.Apply(without, 2);
        Assert.Equal(without * 2, with, 4);
    }

    [Fact]
    public void The_window_is_open_while_he_reels_plus_the_grace()
    {
        Assert.True(PerfectStrikeMath.Open(10.0, 11.5, 0.15));
        Assert.True(PerfectStrikeMath.Open(11.5, 11.5, 0));
        Assert.True(PerfectStrikeMath.Open(11.65, 11.5, 0.15));
        Assert.False(PerfectStrikeMath.Open(11.7, 11.5, 0.15));
        Assert.False(PerfectStrikeMath.Open(11.6, 11.5, 0));
        Assert.False(PerfectStrikeMath.Open(10, double.NaN, 1));
    }

    [Fact]
    public void The_book_keeps_one_window_a_man_replaces_the_oldest_when_full_and_removes_cleanly()
    {
        var b = new PerfectStrikeBook();
        b.Open(5, Right, 0.8, 1.0, out bool dropped);
        Assert.False(dropped);
        Assert.Equal(1, b.Count);
        Assert.Equal(Right, b[b.IndexOf(5)].Blocked);
        b.Open(5, Left, 0.4, 2.0, out dropped); // he is staggered again before you struck: the new one replaces it
        Assert.True(dropped);
        Assert.Equal(1, b.Count);
        Assert.Equal(Left, b[0].Blocked);
        b.Open(6, 9, 0.5, 2.0, out _);
        Assert.Equal(-1, b[b.IndexOf(6)].Blocked); // an unreadable direction kept as unknown
        for (int i = 0; i < PerfectStrikeMath.MaxWindows; i++) b.Open(100 + i, Up, 1, 3.0 + i, out dropped);
        Assert.Equal(PerfectStrikeMath.MaxWindows, b.Count);
        Assert.True(dropped);
        Assert.Equal(-1, b.IndexOf(5)); // the oldest went first
        b.RemoveAt(b.IndexOf(101));
        Assert.Equal(PerfectStrikeMath.MaxWindows - 1, b.Count);
        Assert.Equal(-1, b.IndexOf(101));
        b.RemoveAt(99);
        b.Clear();
        Assert.Equal(0, b.Count);
    }

    // ------------------------------------------------------------------ the rules and the line

    [Fact]
    public void The_master_switch_comes_first_then_the_impact_system_timed_blocks_and_its_own_switch()
    {
        Assert.True(Rules().Enabled);
        Assert.Null(Rules().OffBecause);
        Assert.Equal("ModEnabled", Rules(s => { s.Set(SettingsSchema.ModEnabled, 0, "test"); s.Set(SettingsSchema.PerfectStrikeEnabled, 0, "test"); }).OffBecause);
        Assert.Equal("the impact system off", Rules(s => s.Set(SettingsSchema.ImpactEnabled, 0, "test")).OffBecause);
        Assert.Equal("the impact system off", Rules(s => s.Set(SettingsSchema.AthleticsEnabled, 0, "test")).OffBecause);
        Assert.Equal("TimedBlocksEnabled", Rules(s => s.Set(SettingsSchema.TimedBlocksEnabled, 0, "test")).OffBecause);
        Assert.Equal("PerfectStrikeEnabled", Rules(s => s.Set(SettingsSchema.PerfectStrikeEnabled, 0, "test")).OffBecause);
        Assert.Contains("perfect strike ON", Rules().Describe());
        Assert.Contains("x2.0-4.0 / x2.0-3.0 / x1.5-2.0 damage by your weapon's 4 / 3 / 2 attack directions", Rules().Describe()); // step 106d
        Assert.Contains("perfect strike OFF (PerfectStrikeEnabled)", Rules(s => s.Set(SettingsSchema.PerfectStrikeEnabled, 0, "test")).Describe());
        Assert.Equal(0.15, Rules().GraceSeconds, 5);
        Assert.True(Rules().CentreMessage);
    }

    [Fact]
    public void The_summary_line_counts_windows_strikes_by_side_the_mean_multiplier_expired_and_wrong_side_hits()
    {
        var st = new PerfectStrikeStats();
        Assert.Contains("PERFECT STRIKES landed 0", st.SummaryLine(Rules()));
        Assert.Contains("mean multiplier -", st.SummaryLine(Rules()));
        st.Opened = 3;
        st.NoteLanded(Right, Right, 0.8, 2.8, 20, 56);
        st.NoteLanded(Left, Up, 0.2, 2.2, 10, 22);
        st.NoteWrongSide(Left);
        st.ExpiredUnused = 1;
        string line = st.SummaryLine(Rules());
        Assert.Contains("windows opened 3 (by an opening's stagger 0), PERFECT STRIKES landed 2 (after an opening 0; by your weapon's directions 2 / 3 / 4: 0 / 0 / 0, by the weapon's relaxed sides 0) (your left swing 1, right swing 1, overhead 0, thrust from below 0", line);
        Assert.Contains("after his blocked left 0 / right 1 / overhead 1 / thrust 0 / unknown 0", line);
        Assert.Contains("mean multiplier x2.50 (x2.20-x2.80, mean timing 0.50), damage 30 → 78", line);
        Assert.Contains("windows expired unused 1", line);
        Assert.Contains("wrong-side hits 1 (left 1, right 0", line);
        Assert.Contains("OFF (TimedBlocksEnabled)", st.SummaryLine(Rules(s => s.Set(SettingsSchema.TimedBlocksEnabled, 0, "test"))));
    }

    [Fact]
    public void Step_103_the_summary_splits_the_mismatches_by_the_blocked_direction()
    {
        var st = new PerfectStrikeStats();
        st.NoteWrongSide(Left, Up);   // a side swing after his blocked overhead - no longer counts
        st.NoteWrongSide(Up, Up);
        st.NoteWrongSide(Right, Down);
        st.BlockedUnknown = 2;
        string line = st.SummaryLine(Rules());
        Assert.Contains("wrong-side hits 3 (left 1, right 1, overhead 1, thrust 0", line);
        Assert.Contains("after his blocked overhead (wanted your thrust): your left 1 / right 0 / overhead 1 / thrust 0", line);
        Assert.Contains("after his blocked thrust (wanted your overhead): your left 0 / right 1 / overhead 0 / thrust 0", line);
        Assert.DoesNotContain("after his blocked left swing (", line);
        Assert.Contains("the opposite by your weapon since step 106d", line);
        Assert.Contains("unknown direction (nothing counts) 2", line);
        Assert.Equal(1, st.WrongSideAfter[Up * 4 + Left]);
    }

    [Fact]
    public void The_settings_sit_where_the_design_puts_them()
    {
        var all = SettingsSchema.All.ToList();
        Assert.Equal(all.IndexOf(SettingsSchema.TimedBlockSlowMo) + 1, all.IndexOf(SettingsSchema.PerfectStrikeEnabled));
        Assert.Contains(SettingsSchema.PerfectStrikeEnabled, SettingsSchema.PlayerPage);
        Assert.DoesNotMatch(@"[Ss]tep \d", SettingsSchema.PerfectStrikeEnabled.Description);
        var advanced = new[]
        {
            SettingsSchema.PerfectStrikeGraceSeconds, SettingsSchema.PerfectStrikeCentreMessage, // step 106d: the flat Min / Max multipliers retired
        };
        int first = all.IndexOf(SettingsSchema.TimedBlockAnimTestKey) + 1;
        for (int i = 0; i < advanced.Length; i++)
        {
            Assert.Equal(first + i, all.IndexOf(advanced[i]));
            Assert.Equal(SettingsPage.Advanced, advanced[i].Group.Page);
        }
        Assert.Equal(1, SettingsSchema.PerfectStrikeEnabled.Default, 5);
        Assert.Equal(0.15, SettingsSchema.PerfectStrikeGraceSeconds.Default, 5);
        Assert.Equal(1, SettingsSchema.PerfectStrikeCentreMessage.Default, 5);
    }
}

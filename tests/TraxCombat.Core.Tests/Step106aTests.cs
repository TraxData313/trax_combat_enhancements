using System;
using System.Linq;
using TraxCombat.Core;
using Xunit.Abstractions;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 106a - OPENINGS, the pure math (Toni 2026-11-09; AI_NOTES "Step 106", DESIGN decision 95): the guard → opening → arrow mapping (crossed
/// and same-named sides), the guard choice (the other three, only openings the weapon can make, uniform by the die), the window by link / skill /
/// shield, the reel, the shield's guard break, the verdict on your next swing, the chain and its resets, the rules' gates, the settings, the
/// [summary] line, no allocation.
/// </summary>
public class Step106aTests
{
    private readonly ITestOutputHelper _out;

    public Step106aTests(ITestOutputHelper output) => _out = output;

    private sealed class Dice : IRandomSource
    {
        private readonly double _v;
        public int Draws;
        public Dice(double v) => _v = v;

        public double NextDouble()
        {
            Draws++;
            return _v;
        }
    }

    private static TraxSettings Settings(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return s;
    }

    private static OpeningRules Rules(Action<TraxSettings>? tune = null) => OpeningRules.From(Settings(tune));

    private const int Up = OpeningMath.AttackUp, Down = OpeningMath.AttackDown, Left = OpeningMath.AttackLeft, Right = OpeningMath.AttackRight;
    private const int GUp = OpeningMath.GuardUp, GDown = OpeningMath.GuardDown, GLeft = OpeningMath.GuardLeft, GRight = OpeningMath.GuardRight;

    // ------------------------------------------------------------------ A. the defaults and the gates

    [Fact]
    public void The_defaults_are_the_design_ones_and_the_live_switch_ships_off()
    {
        var r = Rules();
        Assert.False(r.Switch);
        Assert.False(r.Enabled);
        Assert.True(r.ShadowOn);
        Assert.Equal("OpeningsEnabled", r.OffBecause);
        Assert.Equal(0.1, r.ForceDelay, 9);
        Assert.Equal(0.45, r.WindowBase, 9);
        Assert.Equal(0.15, r.WindowPerChain, 9);
        Assert.Equal(2.0, r.SkillWeight, 9);
        Assert.Equal(4, r.ChainMax);
        Assert.Equal(2.0, r.ChainReset, 9);
        Assert.True(r.MissKeepsChain);
        Assert.Equal(1.5, r.HoldMax, 9);
        Assert.True(r.SidesCrossed);
        Assert.Equal(2, r.ShieldMinChain);
        Assert.Equal(0.75, r.ShieldWindow, 9);
        Assert.False(r.GivesPerfectStrike);
        Assert.True(r.ArrowShow);
        Assert.True(r.ArrowAtCrosshair);
        Assert.False(r.AiVsAi);
    }

    [Fact]
    public void The_master_switch_comes_first_for_the_live_part_and_the_shadow()
    {
        var on = Rules(s => s.Set(SettingsSchema.OpeningsEnabled, 1, "test"));
        Assert.True(on.Enabled);
        var off = Rules(s =>
        {
            s.Set(SettingsSchema.OpeningsEnabled, 1, "test");
            s.Set(SettingsSchema.ModEnabled, 0, "test");
        });
        Assert.False(off.Enabled);
        Assert.False(off.ShadowOn);
        Assert.Equal("ModEnabled", off.OffBecause);
        Assert.False(Rules(s => s.Set(SettingsSchema.OpeningsShadowLog, 0, "test")).ShadowOn);
        Assert.Contains("openings OFF (OpeningsEnabled), the shadow log ON", Rules().Describe());
    }

    // ------------------------------------------------------------------ B. the mapping

    [Theory]
    [InlineData(GDown, Up, OpeningArrow.Top)]      // Toni's example: he drops low - the arrow at the top, your overhead
    [InlineData(GUp, Down, OpeningArrow.Bottom)]   // he goes high - your thrust from below
    [InlineData(GLeft, Left, OpeningArrow.Left)]   // crossed sides (the brief's mapping)
    [InlineData(GRight, Right, OpeningArrow.Right)]
    public void A_forced_guard_opens_the_brief_s_attack_with_crossed_sides(int guard, int opening, OpeningArrow arrow)
    {
        Assert.Equal(opening, OpeningMath.OpeningFor(guard, true));
        Assert.Equal(arrow, OpeningMath.ArrowFor(OpeningMath.OpeningFor(guard, true)));
    }

    [Fact]
    public void Same_named_sides_swap_only_the_side_openings()
    {
        Assert.Equal(Up, OpeningMath.OpeningFor(GDown, false));
        Assert.Equal(Down, OpeningMath.OpeningFor(GUp, false));
        Assert.Equal(Right, OpeningMath.OpeningFor(GLeft, false));
        Assert.Equal(Left, OpeningMath.OpeningFor(GRight, false));
    }

    [Fact]
    public void A_guard_never_opens_the_attack_it_blocks_and_the_pairing_inverts()
    {
        foreach (bool crossed in new[] { true, false })
            for (int g = GUp; g <= GRight; g++)
            {
                int blocks = OpeningMath.AttackBlockedBy(g, crossed);
                Assert.Equal(g, OpeningMath.GuardBlocking(blocks, crossed));
                Assert.NotEqual(blocks, OpeningMath.OpeningFor(g, crossed));
                Assert.Equal(OpeningMath.Opposite(blocks), OpeningMath.OpeningFor(g, crossed));
            }
        Assert.Equal(OpeningMath.Unknown, OpeningMath.OpeningFor(OpeningMath.Unknown, true));
        Assert.Equal(OpeningArrow.None, OpeningMath.ArrowFor(9));
    }

    [Fact]
    public void The_previous_guard_is_the_read_one_else_the_one_that_blocks_your_attack()
    {
        Assert.Equal(GLeft, OpeningMath.PreviousGuard(GLeft, Up, true));
        Assert.Equal(GUp, OpeningMath.PreviousGuard(OpeningMath.Unknown, Up, true));
        Assert.Equal(GLeft, OpeningMath.PreviousGuard(8, Right, true)); // DefendAny is no guard of the four: derived from your right swing
        Assert.Equal(OpeningMath.Unknown, OpeningMath.PreviousGuard(OpeningMath.Unknown, OpeningMath.Unknown, true));
    }

    // ------------------------------------------------------------------ C. the guard choice

    [Fact]
    public void The_guard_is_one_of_the_other_three_uniformly_by_the_die()
    {
        int all = OpeningMath.AllAttacks;
        Assert.Equal(3, OpeningMath.CandidateCount(GUp, all, true));
        Assert.Equal(GDown, OpeningMath.ChooseGuard(GUp, all, true, 0.0));
        Assert.Equal(GDown, OpeningMath.ChooseGuard(GUp, all, true, 0.33));
        Assert.Equal(GLeft, OpeningMath.ChooseGuard(GUp, all, true, 0.34));
        Assert.Equal(GRight, OpeningMath.ChooseGuard(GUp, all, true, 0.999));
        Assert.Equal(GRight, OpeningMath.ChooseGuard(GUp, all, true, 1.0)); // a die of 1 (never drawn) stays inside
        var seen = new int[8];
        var rng = new SeededRandom(106);
        for (int i = 0; i < 30000; i++) seen[OpeningMath.ChooseGuard(GLeft, all, true, rng.NextDouble())]++;
        Assert.Equal(0, seen[GLeft]);
        foreach (int g in new[] { GUp, GDown, GRight }) Assert.InRange(seen[g], 9500, 10500);
    }

    [Fact]
    public void A_weapon_without_a_thrust_never_gets_the_high_guard_opening()
    {
        int axe = OpeningMath.WeaponMask(swing: true, thrust: false); // a two-handed axe (step 103: no attack_down)
        Assert.Equal(0, axe & OpeningMath.Bit(Down));
        Assert.Equal(2, OpeningMath.CandidateCount(GDown, axe, true)); // after a low guard: left / right only (high = your thrust - none)
        for (double die = 0; die < 1; die += 0.05) Assert.NotEqual(GUp, OpeningMath.ChooseGuard(GLeft, axe, true, die));
        int spear = OpeningMath.WeaponMask(swing: false, thrust: true);
        Assert.Equal(1, OpeningMath.CandidateCount(GLeft, spear, true));
        Assert.Equal(GUp, OpeningMath.ChooseGuard(GLeft, spear, true, 0.9));
        Assert.Equal(0, OpeningMath.CandidateCount(GUp, spear, true)); // he already holds the one guard a pure thrust could open
        Assert.Equal(OpeningMath.Unknown, OpeningMath.ChooseGuard(GUp, spear, true, 0.5));
        Assert.Equal(OpeningMath.AllAttacks, OpeningMath.WeaponMask(false, false)); // unknown capabilities = all
    }

    // ------------------------------------------------------------------ D. the window, the reel, the guard break

    [Fact]
    public void The_window_grows_by_link_and_by_skill_and_a_shield_cuts_it()
    {
        var r = Rules();
        // skill 100: the timed block's perfect window 0.15 - the skill-0 0.10 = 0.05 → +0.10 s
        Assert.Equal(0.55, OpeningMath.Window(in r, 1, 100, false), 6);
        Assert.Equal(0.70, OpeningMath.Window(in r, 2, 100, false), 6);
        Assert.Equal(0.85, OpeningMath.Window(in r, 3, 100, false), 6);
        Assert.Equal(1.00, OpeningMath.Window(in r, 4, 100, false), 6);
        Assert.Equal(1.00, OpeningMath.Window(in r, 9, 100, false), 6); // held at the chain's last link
        Assert.Equal(0.45, OpeningMath.Window(in r, 1, 0, false), 6);
        Assert.Equal(0.75, OpeningMath.Window(in r, 1, 300, false), 6);
        Assert.Equal(0.45, OpeningMath.Window(in r, 1, double.NaN, false), 6);
        Assert.Equal(0, OpeningMath.Window(in r, 1, 100, true)); // a shield: nothing at link 1
        Assert.Equal(0.70 * 0.75, OpeningMath.Window(in r, 2, 100, true), 6);
        var noSkill = Rules(s => s.Set(SettingsSchema.OpeningWindowSkillWeight, 0.0, "test"));
        Assert.Equal(0.45, OpeningMath.Window(in noSkill, 1, 300, false), 6);
    }

    [Fact]
    public void The_reel_is_0_8_1_1_1_4_1_7_and_capped()
    {
        var r = Rules();
        Assert.Equal(new[] { 0.8, 1.1, 1.4, 1.7 }, Enumerable.Range(1, 4).Select(k => Math.Round(OpeningMath.ReelSeconds(in r, k), 6)));
        var longChain = Rules(s =>
        {
            s.Set(SettingsSchema.OpeningChainMax, 8, "test");
            s.Set(SettingsSchema.OpeningReelMaxSeconds, 2.0, "test");
        });
        Assert.Equal(2.0, OpeningMath.ReelSeconds(in longChain, 8), 6);
    }

    [Fact]
    public void A_shield_breaks_35_50_65_percent_with_a_0_6_to_1_0_s_stagger()
    {
        var r = Rules();
        Assert.Equal(0, OpeningMath.GuardBreakChance(in r, 1));
        Assert.Equal(0.35, OpeningMath.GuardBreakChance(in r, 2), 6);
        Assert.Equal(0.50, OpeningMath.GuardBreakChance(in r, 3), 6);
        Assert.Equal(0.65, OpeningMath.GuardBreakChance(in r, 4), 6);
        Assert.Equal(0.6, OpeningMath.GuardBreakStagger(in r, 2), 6);
        Assert.Equal(0.8, OpeningMath.GuardBreakStagger(in r, 3), 6);
        Assert.Equal(1.0, OpeningMath.GuardBreakStagger(in r, 4), 6);
        Assert.Equal(0, OpeningMath.GuardBreakStagger(in r, 1));
    }

    // ------------------------------------------------------------------ E. the plan and the verdict

    [Fact]
    public void The_plan_for_Toni_s_example()
    {
        var r = Rules();
        var dice = new Dice(0.0);
        // your right swing blocked by his weapon (guard read: right... crossed = his LEFT guard took it); the die picks the first other guard
        var p = OpeningMath.Plan(in r, 1, 100, false, false, GLeft, Right, OpeningMath.AllAttacks, dice);
        Assert.True(p.Opens);
        Assert.Equal(GLeft, p.PreviousGuard);
        Assert.Equal(3, p.Candidates);
        Assert.Equal(GUp, p.Guard);
        Assert.Equal(Down, p.Opening);
        Assert.Equal(OpeningArrow.Bottom, p.Arrow);
        Assert.Equal(0.55, p.Window, 6);
        Assert.Equal(0.8, p.Reel, 6);
        Assert.Equal(1, dice.Draws);
        var low = OpeningMath.Plan(in r, 2, 100, false, false, GUp, Up, OpeningMath.AllAttacks, new Dice(0.0));
        Assert.Equal(GDown, low.Guard); // he drops low → the arrow at the top, your overhead
        Assert.Equal(Up, low.Opening);
        Assert.Equal(OpeningArrow.Top, low.Arrow);
    }

    [Fact]
    public void The_plan_skips_riders_early_shields_and_weapons_without_an_opening()
    {
        var r = Rules();
        var d = new Dice(0.5);
        Assert.Equal(OpeningSkip.Mounted, OpeningMath.Plan(in r, 1, 100, false, true, GUp, Up, OpeningMath.AllAttacks, d).Skip);
        var early = OpeningMath.Plan(in r, 1, 100, true, false, GUp, Up, OpeningMath.AllAttacks, d);
        Assert.Equal(OpeningSkip.ShieldTooEarly, early.Skip);
        Assert.False(early.Opens);
        var second = OpeningMath.Plan(in r, 2, 100, true, false, GUp, Up, OpeningMath.AllAttacks, d);
        Assert.True(second.Opens);
        Assert.Equal(0.35, second.BreakChance, 6);
        Assert.Equal(0.6, second.BreakStagger, 6);
        int spear = OpeningMath.WeaponMask(false, true);
        Assert.Equal(OpeningSkip.NoOpeningForWeapon, OpeningMath.Plan(in r, 1, 100, false, false, GUp, Down, spear, d).Skip);
        Assert.Equal(1, d.Draws); // only the shield's second link drew a die - never a skip
        var one = new Dice(0.7);
        Assert.Equal(GUp, OpeningMath.Plan(in r, 1, 100, false, false, GLeft, Right, spear, one).Guard);
        Assert.Equal(0, one.Draws); // one candidate - no die
    }

    [Fact]
    public void The_verdict_on_your_next_swing()
    {
        var r = Rules();
        var p = OpeningMath.Plan(in r, 1, 100, false, false, GUp, Up, OpeningMath.AllAttacks, new Dice(0.0)); // low guard → your overhead, window 0.55
        Assert.Equal(OpeningVerdict.TooEarly, OpeningMath.Verdict(in r, in p, 0.05, Up));
        Assert.Equal(OpeningVerdict.Opened, OpeningMath.Verdict(in r, in p, 0.10, Up));
        Assert.Equal(OpeningVerdict.Opened, OpeningMath.Verdict(in r, in p, 0.65, Up));
        Assert.Equal(OpeningVerdict.TooLate, OpeningMath.Verdict(in r, in p, 0.66, Up));
        Assert.Equal(OpeningVerdict.WrongDirection, OpeningMath.Verdict(in r, in p, 0.3, Left));
        Assert.Equal(OpeningVerdict.UnknownDirection, OpeningMath.Verdict(in r, in p, 0.3, OpeningMath.Unknown));
        Assert.Equal(OpeningVerdict.NoSwing, OpeningMath.Verdict(in r, in p, double.NaN, Up));
        Assert.Equal(2.0, OpeningMath.HoldEnds(in r, 0.5), 9);
    }

    // ------------------------------------------------------------------ F. the chain

    [Fact]
    public void The_chain_counts_up_to_the_4th_and_resets_on_a_gap_or_another_man()
    {
        var r = Rules();
        var c = OpeningChain.None;
        Assert.False(c.Active);
        Assert.Equal(1, c.NoteBlock(7, 10.0, in r, out var reset));
        Assert.Equal(OpeningReset.None, reset);
        Assert.Equal(2, c.NoteBlock(7, 11.0, in r, out _));
        Assert.Equal(3, c.NoteBlock(7, 12.5, in r, out _));
        Assert.Equal(4, c.NoteBlock(7, 14.0, in r, out _));
        Assert.Equal(4, c.NoteBlock(7, 15.0, in r, out _)); // stays at the 4th
        Assert.False(c.Expired(17.0, in r));
        Assert.True(c.Expired(17.01, in r));
        Assert.Equal(1, c.NoteBlock(7, 17.5, in r, out reset));
        Assert.Equal(OpeningReset.Gap, reset);
        Assert.Equal(2, c.NoteBlock(7, 18.0, in r, out _));
        Assert.Equal(1, c.NoteBlock(9, 18.5, in r, out reset));
        Assert.Equal(OpeningReset.TargetSwitch, reset);
        Assert.Equal(9, c.Target);
        c.Reset();
        Assert.False(c.Active);
        Assert.Equal(1, c.NoteBlock(9, 19.0, in r, out reset));
        Assert.Equal(OpeningReset.None, reset);
    }

    // ------------------------------------------------------------------ G. the stats and the [summary] line

    [Fact]
    public void The_summary_line_counts_the_shadow()
    {
        var r = Rules();
        var st = new OpeningStats();
        var p1 = OpeningMath.Plan(in r, 1, 100, false, false, GUp, Up, OpeningMath.AllAttacks, new Dice(0.0)); // low guard
        st.NoteBlock(in p1);
        st.NotePair(Up, GUp);
        st.NotePair(Right, GLeft);
        st.NotePair(Left, OpeningMath.Unknown);
        st.NoteVerdict(OpeningVerdict.Opened, 1, 0.35);
        st.NoteContact(OpeningContact.BlockedAgain, 0.6, true);
        st.NoteDirection(Up, Up);
        var p2 = OpeningMath.Plan(in r, 1, 100, true, false, GUp, Up, OpeningMath.AllAttacks, new Dice(0.0)); // shield, link 1
        st.NoteBlock(in p2);
        st.NoteVerdict(OpeningVerdict.NoSwing, 1, double.NaN);
        st.NoteReset(OpeningReset.Gap);
        st.NoteReset(OpeningReset.None);
        st.AiVsAi = 3;
        string line = st.SummaryLine(in r);
        _out.WriteLine(line);
        Assert.StartsWith("openings (step 106a SHADOW - nothing applied; live OFF (OpeningsEnabled), the shadow log ON): your attacks the AI blocked correctly 2 (shield 1, weapon 1) by link 2 / 0 / 0 / 0", line);
        Assert.Contains("openings planned 1 - forced low / high / left / right guard 1 / 0 / 0 / 0", line);
        Assert.Contains("shield before link 2 1", line);
        Assert.Contains("INTO the opening 1 (by link 1 / 0 / 0 / 0, mean 0.35 s after the block)", line);
        Assert.Contains("no swing 1", line);
        Assert.Contains("block -> your wind-up mean 0.35 s (0.35-0.35)", line);
        Assert.Contains("wind-up -> contact mean 0.60 s", line);
        Assert.Contains("the swings into the opening: landed 0, blocked again 1", line);
        Assert.Contains("overhead 1/0/0/0/0; thrust 0/0/0/0/0; left swing 0/0/0/0/1; right swing 0/0/1/0/0", line);
        Assert.Contains("sides now CROSSED", line);
        Assert.Contains("agree 1, differ 0, unread 0", line);
        Assert.Contains("chain resets: hit landed 0, gap 1", line);
        Assert.Contains("AI vs AI correct blocks on foot 3 (counted only)", line);
        Assert.Equal(1, st.DelayBand[OpeningStats.Band(0.35)]);
        Assert.Equal(0, OpeningStats.Band(0.05));
        Assert.Equal(OpeningStats.DelayBands - 1, OpeningStats.Band(5));
    }

    [Fact]
    public void Planning_and_counting_allocate_nothing()
    {
        var r = Rules();
        var st = new OpeningStats();
        var dice = new SeededRandom(3);
        var c = OpeningChain.None;
        // warm up
        var w = OpeningMath.Plan(in r, 1, 100, false, false, GUp, Up, OpeningMath.AllAttacks, dice);
        st.NoteBlock(in w);
        long bytes = NoAllocation.Bytes(() =>
        {
            double now = 0;
            for (int i = 0; i < 2000; i++)
            {
                now += 0.7;
                int k = c.NoteBlock(i % 3, now, in r, out var reset);
                st.NoteReset(reset);
                var p = OpeningMath.Plan(in r, k, 150, i % 2 == 0, false, OpeningMath.Unknown, i % 4, OpeningMath.AllAttacks, dice);
                st.NoteBlock(in p);
                st.NotePair(i % 4, OpeningMath.GuardUp + i % 4);
                var v = OpeningMath.Verdict(in r, in p, 0.3, i % 4);
                st.NoteVerdict(v, k, 0.3);
                st.NoteContact(OpeningContact.Landed, 0.5, v == OpeningVerdict.Opened);
                st.NoteDirection(i % 4, i % 4);
            }
        });
        Assert.Equal(0, bytes);
    }

    // ------------------------------------------------------------------ H. the settings' places

    [Fact]
    public void The_settings_sit_where_the_design_puts_them()
    {
        Assert.Equal(SettingsPage.Player, SettingsSchema.OpeningsEnabled.Group.Page);
        Assert.Equal("Staggers and knockdowns", SettingsSchema.OpeningsEnabled.Group.Title);
        var advanced = SettingsSchema.All.Where(p => p.Key.StartsWith("Opening", StringComparison.Ordinal) && p != SettingsSchema.OpeningsEnabled).ToList();
        Assert.Equal(23, advanced.Count);
        Assert.All(advanced, p => Assert.Same(SettingsSchema.ImpactGroup, p.Group));
        int after = SettingsSchema.All.ToList().IndexOf(SettingsSchema.TimedBlockPushOutMounted);
        Assert.Equal(after + 1, SettingsSchema.All.ToList().IndexOf(SettingsSchema.OpeningsShadowLog));
        Assert.Equal(0, SettingsSchema.OpeningsEnabled.Default);
        Assert.Equal(1, SettingsSchema.OpeningsShadowLog.Default);
    }
}

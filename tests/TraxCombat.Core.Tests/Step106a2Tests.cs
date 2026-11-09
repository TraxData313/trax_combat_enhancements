using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 106a2 - OPENINGS, Toni's REVISED design, the pure math (AI_NOTES "Step 106", DESIGN decision 95): the balance side (the mapping, the
/// guard choice by the weapon), the follow-up's timing score around the flash and his block chance from it, the plan's entries and the shield's
/// first link, THE CHAIN's state machine (off balance → the stagger → the finish), the finishing sides by the weapon (no-thrust: overhead → left
/// or right; spears: a side stagger → either stab), the perfect strike's range by the weapon's directions, the stats' [summary] line, the
/// settings and the retired keys, no allocation.
/// </summary>
public class Step106a2Tests
{
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

    private static OpeningRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return OpeningRules.From(s);
    }

    private const int Up = OpeningMath.AttackUp, Down = OpeningMath.AttackDown, Left = OpeningMath.AttackLeft, Right = OpeningMath.AttackRight;
    private const int GUp = OpeningMath.GuardUp, GDown = OpeningMath.GuardDown, GLeft = OpeningMath.GuardLeft, GRight = OpeningMath.GuardRight;
    private static readonly int Sword = OpeningMath.WeaponMask(true, true), Axe = OpeningMath.WeaponMask(true, false), Spear = OpeningMath.WeaponMask(false, true);

    // ------------------------------------------------------------------ A. the defaults and the gates

    [Fact]
    public void The_defaults_are_the_revised_design_and_the_live_switch_ships_off()
    {
        var r = Rules();
        Assert.False(r.Enabled);
        Assert.True(r.ShadowOn);
        Assert.Equal("OpeningsEnabled", r.OffBecause);
        Assert.Equal(0.3, r.FlashDelay, 9);
        Assert.Equal(1.0, r.PerfectScale, 9);
        Assert.Equal(1.0, r.WindowBase, 9);
        Assert.Equal(0.25, r.WindowPerLink, 9);
        Assert.Equal(1.5, r.UnguardedWindow, 9);
        Assert.Equal(0.2, r.BlockPerLink, 9);
        Assert.Equal(4, r.ChainMax);
        Assert.Equal(2, r.ShieldMinChain);
        Assert.True(r.BackHitStaggers);
        Assert.True(r.UnguardedHitOpens);
        Assert.True(r.WeaponFinishes);
        Assert.Equal((2.0, 4.0), (r.FourMin, r.FourMax));
        Assert.Equal((2.0, 3.0), (r.ThreeMin, r.ThreeMax));
        Assert.Equal((1.5, 2.0), (r.TwoMin, r.TwoMax));
        Assert.Contains("openings (revised design) OFF (OpeningsEnabled)", r.Describe());
    }

    [Fact]
    public void The_master_switch_comes_first()
    {
        var r = Rules(s =>
        {
            s.Set(SettingsSchema.OpeningsEnabled, true, SettingSources.File);
            s.Set(SettingsSchema.ModEnabled, false, SettingSources.File);
        });
        Assert.False(r.Enabled);
        Assert.False(r.ShadowOn);
        Assert.Equal("ModEnabled", r.OffBecause);
    }

    // ------------------------------------------------------------------ B. the weapon and the mapping

    [Fact]
    public void A_weapon_has_four_three_or_two_directions()
    {
        Assert.Equal(4, OpeningMath.DirectionCount(Sword));
        Assert.Equal(3, OpeningMath.DirectionCount(Axe));
        Assert.Equal(2, OpeningMath.DirectionCount(Spear));
        Assert.Equal(OpeningMath.Bit(Up) | OpeningMath.Bit(Down), Spear); // the overhead stab and the thrust
        Assert.Equal(0, Axe & OpeningMath.Bit(Down));
        Assert.Equal(OpeningMath.AllAttacks, OpeningMath.WeaponMask(false, false)); // unknown = all
    }

    [Theory]
    [InlineData(true, GDown, Up)]
    [InlineData(true, GUp, Down)]
    [InlineData(true, GLeft, Left)]
    [InlineData(true, GRight, Right)]
    [InlineData(false, GLeft, Right)]
    [InlineData(false, GRight, Left)]
    public void The_balance_guard_opens_the_attack_opposite_to_what_it_blocks(bool crossed, int guard, int opening)
    {
        Assert.Equal(opening, OpeningMath.OpeningFor(guard, crossed));
        Assert.Equal(guard, OpeningMath.GuardBlocking(OpeningMath.Opposite(opening), crossed));
    }

    [Fact]
    public void The_balance_goes_only_where_the_weapon_can_follow_uniformly()
    {
        // an axe (no thrust) never gets the high-guard balance (its opening would be the thrust)
        Assert.False(OpeningMath.Candidate(GUp, GLeft, Axe, true));
        Assert.Equal(2, OpeningMath.CandidateCount(GLeft, Axe, true));
        // a spear against a high guard: only the low guard (its overhead stab) is left
        Assert.Equal(1, OpeningMath.CandidateCount(GUp, Spear, true));
        Assert.Equal(GDown, OpeningMath.ChooseGuard(GUp, Spear, true, 0.9));
        var counts = new int[8];
        var rnd = new SeededRandom(7);
        for (int i = 0; i < 30000; i++) counts[OpeningMath.ChooseGuard(GLeft, Sword, true, rnd.NextDouble())]++;
        Assert.Equal(0, counts[GLeft]);
        foreach (int g in new[] { GUp, GDown, GRight }) Assert.InRange(counts[g], 9500, 10500);
    }

    // ------------------------------------------------------------------ C. the timing and the block chance

    [Fact]
    public void The_follow_up_scores_like_the_defensive_game_around_the_flash()
    {
        var r = Rules();
        var p = OpeningMath.Plan(in r, 1, OpeningEntry.Blocked, 100, false, GLeft, Right, Sword, new Dice(0));
        Assert.Equal(0.3, p.Flash, 9);
        Assert.Equal(0.15, p.Perfect, 9); // the timed block's perfect window at skill 100
        Assert.Equal(1.0, p.Window, 9);
        Assert.Equal(1.0, OpeningMath.TimingScore(in r, in p, 0.3), 9);
        Assert.Equal(1.0, OpeningMath.TimingScore(in r, in p, 0.45), 9);
        Assert.Equal(1.0, OpeningMath.TimingScore(in r, in p, 0.16), 9);
        Assert.Equal(0.0, OpeningMath.TimingScore(in r, in p, 1.3), 9); // the window's end
        Assert.Equal(0.0, OpeningMath.TimingScore(in r, in p, 0.0), 9); // at the block - too early
        double mid = OpeningMath.TimingScore(in r, in p, 0.3 + 0.15 + 0.85 / 2);
        Assert.Equal(0.5, mid, 6);
        Assert.Equal(0.0, OpeningMath.TimingScore(in r, in p, double.NaN), 9);
        // skill widens the perfect: skill 300 = 0.25 s
        var p300 = OpeningMath.Plan(in r, 1, OpeningEntry.Blocked, 300, false, GLeft, Right, Sword, new Dice(0));
        Assert.Equal(0.25, p300.Perfect, 9);
        Assert.Equal(1.0, OpeningMath.TimingScore(in r, in p300, 0.54), 9);
        Assert.True(OpeningMath.TimingScore(in r, in p, 0.54) < 1);
    }

    [Fact]
    public void The_window_grows_by_link_and_after_an_unguarded_hit()
    {
        var r = Rules();
        Assert.Equal(new[] { 1.0, 1.25, 1.5, 1.75, 1.75 }, Enumerable.Range(1, 5).Select(k => Math.Round(OpeningMath.Window(in r, k, false), 9)));
        Assert.Equal(1.5, OpeningMath.Window(in r, 1, true), 9);
        Assert.Equal(new[] { 0.8, 1.1, 1.4, 1.7 }, Enumerable.Range(1, 4).Select(k => Math.Round(OpeningMath.ReelSeconds(in r, k), 9)));
    }

    [Fact]
    public void His_block_chance_is_zero_at_perfect_his_normal_at_the_end_and_easier_each_link()
    {
        var r = Rules();
        Assert.Equal(0, OpeningMath.BlockChance(in r, 1, 1, false), 9);
        Assert.Equal(1, OpeningMath.BlockChance(in r, 0, 1, false), 9);
        Assert.Equal(0.5, OpeningMath.BlockChance(in r, 0.5, 1, false), 9);
        Assert.Equal(new[] { 1.0, 0.8, 0.6, 0.4 }, Enumerable.Range(1, 4).Select(k => Math.Round(OpeningMath.BlockChance(in r, 0, k, false), 9)));
        Assert.Equal(1, OpeningMath.BlockChance(in r, 1, 1, true), 9); // a shield still holding blocks as usual
    }

    // ------------------------------------------------------------------ D. the plan

    [Fact]
    public void A_shield_shows_the_side_at_link_1_and_gives_way_from_link_2()
    {
        var r = Rules();
        var p1 = OpeningMath.Plan(in r, 1, OpeningEntry.ShieldBlocked, 100, false, GUp, Up, Sword, new Dice(0));
        Assert.True(p1.Opens);
        Assert.True(p1.Shield);
        Assert.True(p1.ShieldHolds);
        var p2 = OpeningMath.Plan(in r, 2, OpeningEntry.ShieldBlocked, 100, false, GUp, Up, Sword, new Dice(0));
        Assert.False(p2.ShieldHolds);
        Assert.Equal(1.25, p2.Window, 9);
        var w = OpeningMath.Plan(in r, 1, OpeningEntry.Blocked, 100, false, GUp, Up, Sword, new Dice(0));
        Assert.False(w.ShieldHolds);
    }

    [Fact]
    public void The_plan_skips_riders_and_weapons_with_no_side()
    {
        var r = Rules();
        var dice = new Dice(0.5);
        var m = OpeningMath.Plan(in r, 1, OpeningEntry.Blocked, 100, true, GUp, Up, Sword, dice);
        Assert.Equal(OpeningSkip.Mounted, m.Skip);
        Assert.Equal(0, dice.Draws);
        // a thrust-only weapon whose single stab... both of a spear's openings closed: no candidate when only the thrust is possible against a high guard
        var n = OpeningMath.Plan(in r, 1, OpeningEntry.Blocked, 100, false, GUp, Up, OpeningMath.Bit(Down), dice);
        Assert.Equal(OpeningSkip.NoOpeningForWeapon, n.Skip);
        var u = OpeningMath.Plan(in r, 1, OpeningEntry.FrontUnguarded, 100, false, OpeningMath.Unknown, Right, Sword, new Dice(0));
        Assert.Equal(1.5, u.Window, 9);
        Assert.Equal(OpeningEntry.FrontUnguarded, u.Entry);
    }

    [Fact]
    public void The_verdict_is_the_direction_only()
    {
        var r = Rules();
        var p = OpeningMath.Plan(in r, 1, OpeningEntry.Blocked, 100, false, GLeft, Right, Sword, new Dice(0));
        Assert.Equal(GUp, p.Guard);
        Assert.Equal(Down, p.Opening);
        Assert.Equal(OpeningArrow.Bottom, p.Arrow);
        Assert.Equal(OpeningVerdict.Opened, OpeningMath.Verdict(in p, 0.3, Down));
        Assert.Equal(OpeningVerdict.WrongDirection, OpeningMath.Verdict(in p, 0.3, Left));
        Assert.Equal(OpeningVerdict.UnknownDirection, OpeningMath.Verdict(in p, 0.3, -1));
        Assert.Equal(OpeningVerdict.NoSwing, OpeningMath.Verdict(in p, double.NaN, Down));
    }

    // ------------------------------------------------------------------ E. the chain - the state machine

    [Fact]
    public void Unshielded_best_case_three_moves_blocked_hit_perfect_strike()
    {
        var r = Rules();
        var c = OpeningChain.None;
        Assert.Equal(1, c.NoteBlock(7, 10, false, in r, out var reset));
        Assert.Equal(OpeningReset.None, reset);
        Assert.True(c.OffBalance);
        Assert.Equal(OpeningEntry.Blocked, c.Entry);
        // move 2: your hit lands while he is off balance - he staggers; the finishing side = the opposite of your swing
        Assert.Equal(OpeningHit.Staggers, c.NoteLanded(7, 10.6, in r, false, true, Left, Sword, 1, out reset));
        Assert.True(c.Reeling(10.6));
        Assert.Equal(10.6 + 0.8, c.ReelUntil, 9);
        Assert.Equal(OpeningMath.Bit(Right), c.FinishMask);
        Assert.Equal(1, c.Score, 9);
        // move 3: the perfect strike from the shown side
        Assert.Equal(OpeningHit.FinishPerfect, c.NoteLanded(7, 11.0, in r, false, false, Right, Sword, 0, out reset));
        Assert.Equal(OpeningReset.ReelOver, reset);
        Assert.False(c.Active);
    }

    [Fact]
    public void Failing_grows_the_chain_and_another_side_on_the_reeling_man_is_no_perfect_strike()
    {
        var r = Rules();
        var c = OpeningChain.None;
        c.NoteBlock(7, 10, true, in r, out _);
        Assert.Equal(2, c.NoteBlock(7, 11, true, in r, out _));
        Assert.Equal(3, c.NoteBlock(7, 12, true, in r, out _));
        Assert.Equal(4, c.NoteBlock(7, 13, true, in r, out _));
        Assert.Equal(4, c.NoteBlock(7, 14, true, in r, out _)); // capped
        Assert.Equal(OpeningEntry.ShieldBlocked, c.Entry);
        Assert.Equal(OpeningHit.Staggers, c.NoteLanded(7, 14.5, in r, false, true, Up, Sword, 0.4, out _));
        Assert.Equal(14.5 + 1.7, c.ReelUntil, 9);
        Assert.Equal(OpeningMath.Bit(Down), c.FinishMask);
        Assert.Equal(OpeningHit.FinishOther, c.NoteLanded(7, 15, in r, false, false, Left, Sword, 0, out _));
        Assert.False(c.Active);
    }

    [Fact]
    public void A_back_hit_always_staggers_an_unguarded_front_hit_puts_him_off_balance_a_guarded_one_does_nothing()
    {
        var r = Rules();
        var c = OpeningChain.None;
        Assert.Equal(OpeningHit.Nothing, c.NoteLanded(3, 1, in r, false, true, Right, Sword, 0, out _));
        Assert.False(c.Active);
        Assert.Equal(OpeningHit.OffBalance, c.NoteLanded(3, 2, in r, false, false, Right, Sword, 0, out _));
        Assert.Equal(OpeningEntry.FrontUnguarded, c.Entry);
        Assert.Equal(1, c.K);
        // the next hit within the off-balance time staggers him
        Assert.Equal(OpeningHit.Staggers, c.NoteLanded(3, 3, in r, false, true, Up, Sword, 0, out _));
        Assert.Equal(OpeningEntry.FrontUnguarded, c.Entry);
        c.Reset();
        Assert.Equal(OpeningHit.Staggers, c.NoteLanded(4, 5, in r, true, true, Right, Sword, 0, out _));
        Assert.Equal(OpeningEntry.Back, c.Entry);
        Assert.Equal(OpeningMath.Bit(Left), c.FinishMask);
        // the switches
        var off = Rules(s =>
        {
            s.Set(SettingsSchema.OpeningBackHitStaggers, false, SettingSources.File);
            s.Set(SettingsSchema.OpeningUnguardedHitOpens, false, SettingSources.File);
        });
        var d = OpeningChain.None;
        Assert.Equal(OpeningHit.Nothing, d.NoteLanded(4, 5, in off, true, false, Right, Sword, 0, out _));
    }

    [Fact]
    public void The_chain_resets_by_gap_by_target_and_when_the_reel_ends()
    {
        var r = Rules();
        var c = OpeningChain.None;
        c.NoteBlock(1, 0, false, in r, out _);
        c.NoteBlock(1, 1, false, in r, out _);
        Assert.Equal(1, c.NoteBlock(2, 1.5, false, in r, out var reset));
        Assert.Equal(OpeningReset.TargetSwitch, reset);
        Assert.Equal(1, c.NoteBlock(2, 4, false, in r, out reset));
        Assert.Equal(OpeningReset.Gap, reset);
        // past the off-balance time a landed hit is an ordinary one
        Assert.Equal(OpeningHit.Nothing, c.NoteLanded(2, 6.5, in r, false, true, Up, Sword, 0, out reset));
        Assert.Equal(OpeningReset.Gap, reset);
        // a reel nobody finished
        c.NoteBlock(2, 10, false, in r, out _);
        c.NoteLanded(2, 10.5, in r, false, true, Up, Sword, 0, out _);
        Assert.True(c.Expired(11.4, in r));
        Assert.Equal(1, c.NoteBlock(2, 11.4, false, in r, out reset));
        Assert.Equal(OpeningReset.ReelOver, reset);
    }

    // ------------------------------------------------------------------ F. the finishing sides and the multipliers

    [Fact]
    public void The_finishing_side_follows_the_weapon()
    {
        // a sword: the strict opposite
        Assert.Equal(OpeningMath.Bit(Down), OpeningMath.FinishAfterYourSwing(Up, Sword, true));
        Assert.Equal(OpeningMath.Bit(Left), OpeningMath.FinishAfterYourSwing(Right, Sword, true));
        // an axe after an OVERHEAD stagger (wanted the thrust it lacks): left OR right
        Assert.Equal(OpeningMath.Bit(Left) | OpeningMath.Bit(Right), OpeningMath.FinishAfterYourSwing(Up, Axe, true));
        // ... the defensive game's: his blocked overhead, step 103's wanted = your thrust → an axe answers left or right
        Assert.Equal(OpeningMath.Bit(Left) | OpeningMath.Bit(Right), OpeningMath.FinishMask(PerfectStrikeMath.Opposite(PerfectStrikeMath.Up), Axe, true));
        // an axe after a side stagger: the other side
        Assert.Equal(OpeningMath.Bit(Right), OpeningMath.FinishAfterYourSwing(Left, Axe, true));
        // a spear: a vertical stagger → the other stab; a side stagger (wanted a side it lacks) → either stab
        Assert.Equal(OpeningMath.Bit(Up), OpeningMath.FinishAfterYourSwing(Down, Spear, true));
        Assert.Equal(Spear, OpeningMath.FinishMask(Left, Spear, true));
        // strict opposites with the switch off; an unknown swing finishes nothing
        Assert.Equal(OpeningMath.Bit(Down), OpeningMath.FinishAfterYourSwing(Up, Axe, false));
        Assert.Equal(0, OpeningMath.FinishAfterYourSwing(-1, Sword, true));
        Assert.Equal(OpeningArrow.Left, OpeningMath.FinishArrow(OpeningMath.Bit(Left) | OpeningMath.Bit(Right)));
    }

    [Fact]
    public void The_perfect_strike_ranges_by_the_weapons_directions()
    {
        var r = Rules();
        Assert.Equal(2.0, OpeningMath.StrikeMultiplier(in r, Sword, 0), 9);
        Assert.Equal(4.0, OpeningMath.StrikeMultiplier(in r, Sword, 1), 9);
        Assert.Equal(3.0, OpeningMath.StrikeMultiplier(in r, Sword, 0.5), 9);
        Assert.Equal(2.0, OpeningMath.StrikeMultiplier(in r, Axe, 0), 9);
        Assert.Equal(3.0, OpeningMath.StrikeMultiplier(in r, Axe, 1), 9);
        Assert.Equal(1.5, OpeningMath.StrikeMultiplier(in r, Spear, 0), 9);
        Assert.Equal(2.0, OpeningMath.StrikeMultiplier(in r, Spear, 1), 9);
        Assert.Equal(2.0, OpeningMath.StrikeMultiplier(in r, OpeningMath.Bit(Down), 1), 9); // a single direction counts as the spear's range
    }

    // ------------------------------------------------------------------ G. the stats, allocation, the settings

    [Fact]
    public void The_summary_line_counts_the_revised_chain()
    {
        var r = Rules();
        var st = new OpeningStats();
        var p = OpeningMath.Plan(in r, 1, OpeningEntry.Blocked, 100, false, GLeft, Right, Sword, new Dice(0));
        st.NoteBlock(in p);
        st.NotePair(Right, GLeft);
        st.NoteFollowUp(OpeningVerdict.Opened, 1, 0.32, 1);
        st.NoteBlockRoll(0, true, 1);
        st.NoteStagger(OpeningEntry.Blocked, true);
        st.NoteFinish(true, 3.5, Sword, false);
        st.NoteFinish(false, 0, Sword, false);
        st.AiVsAi = 2;
        string line = st.SummaryLine(in r);
        Assert.StartsWith("openings (step 106d; live OFF (OpeningsEnabled) - counted only, the shadow log ON): ", line);
        Assert.Contains("OFF BALANCE 1 (weapon block 1", line);
        Assert.Contains("INTO the balance side 1 (by link 1 / 0 / 0 / 0)", line);
        Assert.Contains("TIMING perfect / good / weak / none 1 / 0 / 0 / 0 (mean 1.00)", line);
        Assert.Contains("WOULD have got through 1", line);
        Assert.Contains("STAGGERS weapon-block chain 1", line);
        Assert.Contains("the mod's own stagger on the same hit 1", line);
        Assert.Contains("PERFECT STRIKE 1 (mean x3.50, weapons with 2 / 3 / 4 directions 0 / 0 / 1", line);
        Assert.Contains("another side (step 105's knockdown roll) 1", line);
        Assert.Contains("right swing 0/0/1/0/0", line);
        Assert.Contains("AI vs AI correct blocks on foot 2 (counted only)", line);
    }

    [Fact]
    public void The_chain_and_the_stats_allocate_nothing()
    {
        var r = Rules();
        var st = new OpeningStats();
        var dice = new SeededRandom(3);
        var c = OpeningChain.None;
        var w = OpeningMath.Plan(in r, 1, OpeningEntry.Blocked, 100, false, GUp, Up, Sword, dice);
        st.NoteBlock(in w);
        long bytes = NoAllocation.Bytes(() =>
        {
            double now = 0;
            for (int i = 0; i < 2000; i++)
            {
                now += 0.7;
                int k = c.NoteBlock(i % 3, now, i % 2 == 0, in r, out var reset);
                st.NoteReset(reset);
                var p = OpeningMath.Plan(in r, k, i % 2 == 0 ? OpeningEntry.ShieldBlocked : OpeningEntry.Blocked, 150, false, OpeningMath.Unknown, i % 4, Sword, dice);
                st.NoteBlock(in p);
                double s = OpeningMath.TimingScore(in r, in p, 0.3 + (i % 10) * 0.1);
                st.NoteFollowUp(OpeningMath.Verdict(in p, 0.3, i % 4), k, 0.3, s);
                st.NoteBlockRoll(OpeningMath.BlockChance(in r, s, k, p.ShieldHolds), i % 3 == 0, k);
                var hit = c.NoteLanded(i % 3, now + 0.2, in r, i % 5 == 0, i % 2 == 0, i % 4, Axe, s, out reset);
                if (hit == OpeningHit.Staggers) st.NoteStagger(c.Entry, false);
                st.NoteFinish(true, OpeningMath.StrikeMultiplier(in r, Axe, s), Axe, true);
            }
        });
        Assert.Equal(0, bytes);
    }

    [Fact]
    public void The_settings_sit_where_the_design_puts_them()
    {
        Assert.Equal(SettingsPage.Player, SettingsSchema.OpeningsEnabled.Group.Page);
        Assert.Equal("Staggers and knockdowns", SettingsSchema.OpeningsEnabled.Group.Title);
        var all = SettingsSchema.All.ToList();
        int first = all.IndexOf(SettingsSchema.OpeningsShadowLog);
        Assert.Equal(all.IndexOf(SettingsSchema.TimedBlockPushOutMounted) + 1, first);
        Assert.Equal(first + 26, all.IndexOf(SettingsSchema.PerfectStrikeWeaponFinishes)); // step 106f: + OpeningHoldTakesDefend; step 106c: OpeningArrowShow moved to the player page
        Assert.All(all.Skip(first).Take(27), p => Assert.Same(SettingsSchema.ImpactGroup, p.Group));
        Assert.Equal(0, SettingsSchema.OpeningsEnabled.Default);
        Assert.Equal(1, SettingsSchema.OpeningsShadowLog.Default);
        foreach (var k in ConfigFile.RetiredOpeningKeys) Assert.DoesNotContain(all, p => p.Key == k);
    }

    [Fact]
    public void The_first_designs_keys_are_dropped_from_a_config_file_once_with_a_note()
    {
        var read = ConfigFile.Read("{ \"ConfigVersion\": 29, \"OpeningForceDelaySeconds\": 0.1, \"OpeningGivesPerfectStrike\": false, \"OpeningChainMax\": 3 }");
        var notes = ConfigFile.Migrate(read);
        Assert.Contains(notes, n => n.StartsWith("OpeningForceDelaySeconds 0.1 dropped", StringComparison.Ordinal));
        Assert.Contains(notes, n => n.StartsWith("OpeningGivesPerfectStrike false dropped", StringComparison.Ordinal));
        Assert.DoesNotContain(read.Unknown, u => u.Key.StartsWith("OpeningForce", StringComparison.Ordinal));
    }
}

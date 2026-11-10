using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 107 - HOLD-BLOCK OPENINGS (Toni 2026-11-10, DESIGN decision 96): the hold's length by your weapon's speed (and a shieldman's by his shield's
/// size), the links, the chain-speed bonus, the cap; who wins a hold by the weapon; the counter-swing roll and its frame; the defensive perfect strike
/// x2-x3 for every weapon; the settings' places; no allocation.
/// </summary>
public class Step107Tests
{
    private const int Up = OpeningMath.AttackUp, Down = OpeningMath.AttackDown, Left = OpeningMath.AttackLeft, Right = OpeningMath.AttackRight;
    private const int GUp = OpeningMath.GuardUp, GDown = OpeningMath.GuardDown, GLeft = OpeningMath.GuardLeft, GRight = OpeningMath.GuardRight;
    private static readonly int Sword = OpeningMath.WeaponMask(true, true), Axe = OpeningMath.WeaponMask(true, false), Spear = OpeningMath.WeaponMask(false, true);

    // the tests run on DESIGN's initial defaults - OpeningsEnabled shipped OFF there (ON in defaults.json since 106d)
    private static TraxSettings On()
    {
        var s = new TraxSettings();
        s.Set(SettingsSchema.OpeningsEnabled, true, SettingSources.File);
        return s;
    }

    private static OpeningHoldRules Rules() => OpeningHoldRules.From(On());

    [Fact]
    public void The_defaults_are_tonis_numbers()
    {
        var r = Rules();
        Assert.True(r.On);
        Assert.Equal(1.0, r.SwingSeconds, 6); // step 108 (step 107: OpeningHoldFirstSeconds 1.2, retired)
        Assert.Equal(95, r.SpeedReference);
        Assert.Equal(0.8, r.FactorMin, 6);
        Assert.Equal(1.5, r.FactorMax, 6);
        Assert.Equal(0.4, r.PerLink, 6);
        Assert.Equal(0.4, r.BonusMax, 6);
        Assert.Equal(0.3, r.BonusFull, 6);
        Assert.Equal(1.0, r.BonusZero, 6);
        Assert.Equal(2.5, r.Cap, 6);
        Assert.Equal(0.3, r.CounterFirst, 6);
        Assert.Equal(1.0, r.CounterLater, 6);
        Assert.Equal(1.0, r.ShieldSmallSeconds, 6);
        Assert.Equal(0.5, r.ShieldBigSeconds, 6);
        Assert.Equal(60, r.ShieldSmallSize, 6);
        Assert.Equal(130, r.ShieldBigSize, 6);
        var p = PerfectStrikeRules.From(new TraxSettings());
        Assert.Equal(2.0, p.DefensiveMin, 6);
        Assert.Equal(3.0, p.DefensiveMax, 6);
    }

    [Fact]
    public void The_master_switch_comes_first_then_the_openings_then_the_holds()
    {
        var s = On();
        s.Set(SettingsSchema.OpeningHoldsEnabled, false, SettingSources.File);
        Assert.False(OpeningHoldRules.From(s).On);
        Assert.Equal("OpeningHoldsEnabled", OpeningHoldRules.From(s).OffBecause);
        s.Set(SettingsSchema.OpeningHoldsEnabled, true, SettingSources.File);
        s.Set(SettingsSchema.OpeningsEnabled, false, SettingSources.File);
        Assert.Equal("OpeningsEnabled", OpeningHoldRules.From(s).OffBecause);
        s.Set(SettingsSchema.ModEnabled, false, SettingSources.File);
        Assert.Equal("ModEnabled", OpeningHoldRules.From(s).OffBecause);
        Assert.Contains("OFF (ModEnabled)", OpeningHoldRules.From(s).Describe());
    }

    [Fact]
    public void The_first_hold_follows_your_weapons_speed_clamped()
    {
        var r = Rules(); // step 108: the base = your swing 1.0 + the reading margin at skill 0 (0.15)
        Assert.Equal(1.15, OpeningHoldMath.FirstHold(in r, 95, 0), 6);          // the reference
        Assert.Equal(1.15 * 95 / 80.0, OpeningHoldMath.FirstHold(in r, 80, 0), 6); // a slow axe: longer
        Assert.Equal(1.15 * 1.5, OpeningHoldMath.FirstHold(in r, 40, 0), 6);    // very slow: the ceiling x1.5
        Assert.Equal(1.15 * 0.8, OpeningHoldMath.FirstHold(in r, 130, 0), 6);   // very fast: the floor x0.8
        Assert.Equal(1.15, OpeningHoldMath.FirstHold(in r, double.NaN, 0), 6);  // unknown: x1
        Assert.Equal(1.15, OpeningHoldMath.FirstHold(in r, 0, 0), 6);
    }

    [Fact]
    public void A_shieldmans_first_hold_is_short_by_his_shields_size()
    {
        var r = Rules();
        Assert.Equal(1.0, OpeningHoldMath.ShieldFirstHold(in r, 60, 95), 6);   // a small round shield
        Assert.Equal(1.0, OpeningHoldMath.ShieldFirstHold(in r, 40, 95), 6);   // smaller: clamped
        Assert.Equal(0.5, OpeningHoldMath.ShieldFirstHold(in r, 130, 95), 6);  // a kite shield
        Assert.Equal(0.5, OpeningHoldMath.ShieldFirstHold(in r, 435, 95), 6);  // the long southern oval: clamped
        Assert.Equal(0.75, OpeningHoldMath.ShieldFirstHold(in r, 95, 95), 6);  // half way
        Assert.Equal(0.75, OpeningHoldMath.ShieldFirstHold(in r, double.NaN, 95), 6); // unknown: the middle
        Assert.Equal(0.5 * 95 / 80.0, OpeningHoldMath.ShieldFirstHold(in r, 130, 80), 6); // a slow weapon: the same factor
    }

    [Fact]
    public void The_chain_speed_share_and_bonus()
    {
        var r = Rules();
        Assert.Equal(1, OpeningHoldMath.ChainSpeedShare(in r, 0.1), 6);
        Assert.Equal(1, OpeningHoldMath.ChainSpeedShare(in r, 0.3), 6);
        Assert.Equal(0.5, OpeningHoldMath.ChainSpeedShare(in r, 0.65), 6);
        Assert.Equal(0, OpeningHoldMath.ChainSpeedShare(in r, 1.0), 6);
        Assert.Equal(0, OpeningHoldMath.ChainSpeedShare(in r, 3), 6);
        Assert.Equal(0, OpeningHoldMath.ChainSpeedShare(in r, double.NaN), 6);
        Assert.Equal(0.4, OpeningHoldMath.ChainBonus(in r, 0.2), 6);
        Assert.Equal(0.2, OpeningHoldMath.ChainBonus(in r, 0.65), 6);
    }

    [Fact]
    public void Links_grow_the_hold_with_the_bonus_and_the_cap()
    {
        var r = Rules();
        var h = OpeningHold.None;
        h.Begin(5, GLeft, true, 10.0, 1.2);
        Assert.True(h.Holding(5));
        Assert.Equal(OpeningMath.OpeningFor(GLeft, true), h.Opening);
        Assert.Equal(11.2, h.Until, 6);
        Assert.Equal(0, h.NoteWindUp(10.2, in r), 6);    // the first hold: no bonus, the score only
        Assert.Equal(1, h.Score, 6);
        Assert.Equal(1.6, h.Link(11.0, in r), 6);        // he blocks your strike into G: re-held from the block, + 0.4
        Assert.Equal(12.6, h.Until, 6);
        Assert.Equal(1, h.Links);
        Assert.Equal(0.4, h.NoteWindUp(11.2, in r), 6);  // your next wind-up 0.2 s after his block: the whole bonus
        Assert.Equal(2.0, h.Length, 6);
        Assert.Equal(13.0, h.Until, 6);
        Assert.Equal(0, h.NoteWindUp(11.4, in r), 6);    // once a hold
        Assert.Equal(2.4, h.Link(12.0, in r), 6);
        Assert.Equal(0.1, h.NoteWindUp(12.1, in r), 6);  // capped at 2.5
        Assert.Equal(2.5, h.Length, 6);
        Assert.Equal(2.5, h.Link(13.0, in r), 6);        // stays at the cap
        Assert.Equal(0, h.NoteWindUp(14.5, in r), 6);    // too slow: nothing
    }

    [Fact]
    public void A_hold_runs_out_then_a_block_within_the_link_window_is_still_a_link()
    {
        var r = Rules();
        var h = OpeningHold.None;
        h.Begin(5, GUp, true, 0, 1.2);
        Assert.False(h.HoldOver(1.1));
        Assert.True(h.HoldOver(1.2));
        h.EndHold(1.2, in r);
        Assert.Equal(HoldPhase.After, h.Phase);
        Assert.False(h.Holding(5));
        Assert.True(h.LinkOpen(5, 3.1));
        Assert.False(h.LinkOpen(6, 2.0));                // another man
        Assert.False(h.LinkOpen(5, 3.3));                // past 2 s
        Assert.True(h.Lapsed(3.3));
        Assert.Equal(1.6, h.Link(3.0, in r), 6);
        Assert.True(h.Holding(5));
    }

    [Fact]
    public void A_sword_must_strike_exactly_the_opening()
    {
        // his held LEFT guard (sides crossed) blocks your RIGHT swing and opens your LEFT swing
        Assert.Equal(Right, OpeningMath.AttackBlockedBy(GLeft, true));
        Assert.Equal(Left, OpeningMath.OpeningFor(GLeft, true));
        Assert.Equal(HoldSwing.Wins, OpeningHoldMath.Classify(GLeft, Left, Sword, true));
        Assert.Equal(HoldSwing.IntoGuard, OpeningHoldMath.Classify(GLeft, Right, Sword, true));
        Assert.Equal(HoldSwing.ThirdSide, OpeningHoldMath.Classify(GLeft, Up, Sword, true));
        Assert.Equal(HoldSwing.ThirdSide, OpeningHoldMath.Classify(GLeft, Down, Sword, true));
        Assert.Equal(HoldSwing.Unknown, OpeningHoldMath.Classify(GLeft, -1, Sword, true));
        Assert.Equal(OpeningMath.Bit(Left), OpeningHoldMath.WinMask(GLeft, Sword, true));
        // a low guard opens the overhead
        Assert.Equal(HoldSwing.Wins, OpeningHoldMath.Classify(GDown, Up, Sword, true));
        Assert.Equal(HoldSwing.IntoGuard, OpeningHoldMath.Classify(GDown, Down, Sword, true));
    }

    [Fact]
    public void An_axe_or_a_spear_wins_with_any_side_but_his_guard()
    {
        Assert.Equal(HoldSwing.Wins, OpeningHoldMath.Classify(GLeft, Left, Axe, true));
        Assert.Equal(HoldSwing.Wins, OpeningHoldMath.Classify(GLeft, Up, Axe, true));
        Assert.Equal(HoldSwing.IntoGuard, OpeningHoldMath.Classify(GLeft, Right, Axe, true));
        Assert.Equal(OpeningMath.Bit(Up) | OpeningMath.Bit(Left), OpeningHoldMath.WinMask(GLeft, Axe, true));
        Assert.Equal(HoldSwing.Wins, OpeningHoldMath.Classify(GUp, Down, Spear, true));
        Assert.Equal(HoldSwing.IntoGuard, OpeningHoldMath.Classify(GUp, Up, Spear, true));
        Assert.Equal(OpeningMath.Bit(Down), OpeningHoldMath.WinMask(GUp, Spear, true));
        Assert.Equal(0, OpeningHoldMath.ClassIndex(Spear));
        Assert.Equal(1, OpeningHoldMath.ClassIndex(Axe));
        Assert.Equal(2, OpeningHoldMath.ClassIndex(Sword));
    }

    [Fact]
    public void The_counter_swing_30_after_the_first_hold_100_after_a_lengthened_one()
    {
        var r = Rules();
        Assert.Equal(0.3, OpeningHoldMath.CounterChance(in r, 0), 6);
        Assert.Equal(1.0, OpeningHoldMath.CounterChance(in r, 1), 6);
        Assert.Equal(1.0, OpeningHoldMath.CounterChance(in r, 3), 6);
        Assert.True(OpeningHoldMath.CounterThrown(0.29, 0.3));
        Assert.False(OpeningHoldMath.CounterThrown(0.3, 0.3));
        Assert.True(OpeningHoldMath.CounterThrown(0.9999, 1.0));
        Assert.True(OpeningHoldMath.CounterThrown(double.NaN, 1.0));
        Assert.False(OpeningHoldMath.CounterThrown(0.0, 0.0));
        // his direction: one of HIS weapon's directions
        Assert.Equal(Up, OpeningHoldMath.CounterDirection(Axe, 0.0));
        Assert.Equal(Left, OpeningHoldMath.CounterDirection(Axe, 0.5));
        Assert.Equal(Right, OpeningHoldMath.CounterDirection(Axe, 0.99));
        Assert.Equal(Down, OpeningHoldMath.CounterDirection(Spear, 0.99));
        Assert.Equal(Right, OpeningHoldMath.CounterDirection(0, 0.99));
    }

    [Fact]
    public void The_counter_swing_frame_readies_then_releases()
    {
        Assert.Equal(AiInputMath.AttackMask, OpeningHoldMath.AttackLeft | OpeningHoldMath.AttackRight | OpeningHoldMath.AttackUp | OpeningHoldMath.AttackDown);
        Assert.Equal(OpeningHoldMath.AttackUp, OpeningHoldMath.AttackFlag(Up));
        Assert.Equal(0u, OpeningHoldMath.AttackFlag(-1));
        uint move = 0x1u; // forward
        uint guarding = move | OpeningForceMath.DefendLeft;
        uint f = OpeningHoldMath.CounterFrame(guarding, OpeningHoldMath.AttackRight, true, out var e);
        Assert.Equal(move | OpeningHoldMath.AttackRight, f);
        Assert.True((e & InputEdit.GuardCleared) != 0 && (e & InputEdit.GuardRaised) != 0);
        f = OpeningHoldMath.CounterFrame(move | OpeningHoldMath.AttackRight | OpeningForceMath.DefendBlock, OpeningHoldMath.AttackRight, false, out e);
        Assert.Equal(move, f);                           // the release: every attack and guard bit out
        Assert.True((e & InputEdit.AttackCleared) != 0);
    }

    [Fact]
    public void The_defensive_perfect_strike_is_2_to_3_for_every_weapon_the_openings_keep_the_table()
    {
        var p = PerfectStrikeRules.From(new TraxSettings());
        foreach (int w in new[] { Sword, Axe, Spear })
        {
            Assert.Equal(2.0, PerfectStrikeMath.WindowMultiplier(0, in p, w, false), 6);
            Assert.Equal(3.0, PerfectStrikeMath.WindowMultiplier(1, in p, w, false), 6);
        }
        Assert.Equal(4.0, PerfectStrikeMath.WindowMultiplier(1, in p, Sword, true), 6);
        Assert.Equal(3.0, PerfectStrikeMath.WindowMultiplier(1, in p, Axe, true), 6);
        Assert.Equal(2.0, PerfectStrikeMath.WindowMultiplier(1, in p, Spear, true), 6);
        Assert.Equal(1.5, PerfectStrikeMath.WindowMultiplier(0, in p, Spear, true), 6);
    }

    [Fact]
    public void A_hold_stagger_reels_by_its_link_with_the_finishing_side_of_the_swing()
    {
        var r = OpeningRules.From(new TraxSettings());
        var c = OpeningChain.None;
        c.HoldStagger(5, 10.0, in r, 2, Left, Sword, 0.75);
        Assert.Equal(OpeningPhase.Reeling, c.Phase);
        Assert.Equal(OpeningEntry.Hold, c.Entry);
        Assert.Equal(11.1, c.ReelUntil, 6);                 // 0.8 + 0.3
        Assert.Equal(OpeningMath.Bit(Right), c.FinishMask);  // the opposite of your left swing
        Assert.Equal(0.75, c.Score, 6);
        var hit = c.NoteLanded(5, 10.5, in r, false, false, Right, Sword, 0, out _);
        Assert.Equal(OpeningHit.FinishPerfect, hit);
        var s = new OpeningStats();
        s.NoteStagger(OpeningEntry.Hold, false);
        Assert.Contains("from a hold-block (step 107) 1", s.SummaryLine(in r));
    }

    [Fact]
    public void The_summary_line_counts_everything()
    {
        var r = Rules();
        var s = new OpeningHoldStats();
        s.Holds = 3;
        s.Links = 2;
        s.LinksAfter = 1;
        s.NoteLength(1.2);
        s.NoteLength(2.0);
        s.NoteBonus(0.4, 0.4);
        s.NoteBonus(0, 0.4);
        s.NoteSwing(HoldSwing.Wins, Sword);
        s.NoteSwing(HoldSwing.IntoGuard, Axe);
        s.Staggers[2]++;
        s.CounterRolledFirst = 2;
        s.CounterThrownFirst = 1;
        s.CounterRolledLater = 1;
        s.CounterThrownLater = 1;
        s.ShieldHolds = 1;
        s.ShieldFirstSum = 0.5;
        s.NoteEnd(HoldEnd.Timeout);
        string line = s.SummaryLine(in r);
        Assert.Contains("openings - hold-block (step 107; ON): holds started 3", line);
        Assert.Contains("LINKS 2 (after the hold ran out 1)", line);
        Assert.Contains("hold lengths mean 1.60 s, longest 2.00 s", line);
        Assert.Contains("chain-speed bonus given 1 (full 1, mean +0.40 s), too slow for any 1", line);
        Assert.Contains("WINS 1 / 0 / 0, into his guard 0 / 1 / 0", line);
        Assert.Contains("STAGGERS by weapon (sword / axe-mace / spear) 1 / 0 / 0", line);
        Assert.Contains("COUNTER-SWINGS after the first hold rolled 2 / thrown 1, after a lengthened hold rolled 1 / thrown 1", line);
        Assert.Contains("SHIELDMEN: holds 1 (by his shield block 0), first hold mean 0.50 s", line);
        Assert.Contains("the hold ran out 1", line);
    }

    [Fact]
    public void The_settings_sit_in_the_impact_group_after_the_test_tool()
    {
        var all = SettingsSchema.All.ToList();
        int at = all.IndexOf(SettingsSchema.OpeningGuardTestSeconds);
        Assert.Equal(at + 1, all.IndexOf(SettingsSchema.OpeningHoldsEnabled));
        Assert.Equal(at + 21, all.IndexOf(SettingsSchema.PerfectStrikeDefensiveMax)); // step 108: the first hold's 1 key became 4
        foreach (var p in all.Skip(at + 1).Take(21))
        {
            Assert.Same(SettingsSchema.ImpactGroup, p.Group);
            Assert.Equal(SettingsPage.Advanced, p.Group.Page);
        }
    }

    [Fact]
    public void The_math_and_the_state_allocate_nothing()
    {
        var r = Rules();
        var h = OpeningHold.None;
        var stats = new OpeningHoldStats();
        double sink = 0;
        long bytes = NoAllocation.Bytes(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                h.Begin(1, GLeft, true, i, OpeningHoldMath.FirstHold(in r, 90, 50));
                sink += h.NoteWindUp(i + 0.2, in r) + h.Link(i + 0.5, in r) + OpeningHoldMath.ShieldFirstHold(in r, 100, 90);
                stats.NoteSwing(OpeningHoldMath.Classify(GLeft, i & 3, Sword, true), Sword);
                stats.NoteLength(h.Length);
                sink += OpeningHoldMath.CounterFrame(0x400u, OpeningHoldMath.AttackUp, (i & 1) == 0, out _);
            }
        });
        Assert.Equal(0, bytes);
        Assert.True(sink > 0);
    }
}

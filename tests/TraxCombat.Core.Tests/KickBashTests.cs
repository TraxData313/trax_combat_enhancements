using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>Step 18 (Anton, 2026-09-28): kicks and shield bashes cost CostPerKickOrBash (3) × the blow's
/// hero / party-leader multipliers, once each, when they start - never a blow, never free unless the cost
/// is 0; <see cref="KickBashTracker"/> makes one decision per action across both channels and the hit.</summary>
public class KickBashTests
{
    private static AthleticsRules Rules(float kick = 3, bool enabled = true, bool mod = true)
        => new(enabled, 50, 1.0f, 75, true, 10, true, 0.75f, 0.75f, kick, 20, 0.7f, 1.0f, true, 2, 1.5f, 60, 0.5f, 0.4f, 50, mod);

    private static Fighter Troop(int skill = 50, bool hero = false, bool leader = false) => new() { AthleticsSkill = skill, IsHero = hero, IsLeader = leader };

    // ------------------------------------------------------------------ the cost

    [Fact]
    public void A_kick_costs_3_points_2_25_for_a_hero_1_7_for_a_party_leader_whatever_the_pool()
    {
        var r = Rules();
        foreach (int skill in new[] { 20, 130, 300 })
        {
            Assert.Equal(3.0, AthleticsMath.KickOrBashCostPoints(r, Troop(skill)), 9);
            Assert.Equal(2.25, AthleticsMath.KickOrBashCostPoints(r, Troop(skill, hero: true)), 9);
            Assert.Equal(1.6875, AthleticsMath.KickOrBashCostPoints(r, Troop(skill, hero: true, leader: true)), 9);
        }
        Assert.Equal(0.0, AthleticsMath.KickOrBashCostPoints(Rules(kick: 0), Troop()), 9);
        Assert.Equal(20.0, AthleticsMath.KickOrBashCostPoints(Rules(kick: 20), Troop()), 9);
        Assert.Equal(10.0, AthleticsMath.BlowCostPoints(r, Troop()), 9); // the blow's own price is untouched
    }

    [Fact]
    public void The_rule_is_read_live_from_the_settings()
    {
        var s = new TraxSettings();
        Assert.Equal(3f, AthleticsRules.From(s).CostPerKickOrBash);
        s.Set(SettingsSchema.CostPerKickOrBash, 5, SettingSources.Mcm);
        Assert.Equal(5f, AthleticsRules.From(s).CostPerKickOrBash);
        s.Set(SettingsSchema.CostPerKickOrBash, 25, SettingSources.Mcm); // above the range: clamped to 20
        Assert.Equal(20f, AthleticsRules.From(s).CostPerKickOrBash);
    }

    [Fact]
    public void Three_kicks_take_3_points_each_and_are_never_counted_as_blows()
    {
        var r = Rules();
        var f = Troop(); // the floor: 50
        for (int i = 1; i <= 3; i++)
        {
            var o = AthleticsMath.ChargeKickOrBash(f, r, 10 + i);
            Assert.True(o.Charged);
            Assert.Equal(3.0, o.Cost, 9);
            Assert.Equal(1.0, o.Multiplier, 9);
            Assert.Equal(50 - 3 * (i - 1), o.Before, 9);
            Assert.Equal(50 - 3 * i, o.After, 9);
        }
        Assert.Equal(41.0 / 50, f.Fraction, 9);
        Assert.Equal(0, f.Blows);
        Assert.Equal(3, f.KicksAndBashes);
        Assert.Equal(13, f.LastBlowTime); // effort: the refill delay restarts like after a blow
        Assert.False(f.Regenerating);

        var lead = Troop(300, hero: true, leader: true);
        var l = AthleticsMath.ChargeKickOrBash(lead, r, 1);
        Assert.Equal(1.6875, l.Cost, 9);
        Assert.Equal(300 - 1.6875, l.After, 9);

        var blow = AthleticsMath.Charge(f, r, 20);
        Assert.Equal(10.0, blow.Cost, 9);
        Assert.Equal(1, f.Blows);
        Assert.Equal(3, f.KicksAndBashes);
    }

    [Fact]
    public void Free_at_cost_0_and_nothing_while_Athletics_or_the_mod_is_off()
    {
        foreach (var r in new[] { Rules(kick: 0), Rules(enabled: false), Rules(mod: false) })
        {
            var f = Troop();
            var o = AthleticsMath.ChargeKickOrBash(f, r, 5);
            Assert.False(o.Charged);
            Assert.Equal(1.0, f.Fraction, 9);
            Assert.Equal(0, f.KicksAndBashes);
            Assert.Equal(double.NegativeInfinity, f.LastBlowTime); // not even the refill delay
        }
    }

    [Fact]
    public void A_kick_can_leave_the_peak_zone_and_empty_the_bar_but_never_goes_below_0()
    {
        var r = Rules();
        var f = Troop();
        for (int i = 0; i < 4; i++) AthleticsMath.Charge(f, r, i); // 50 → 10
        var edge = Troop();
        AthleticsMath.Charge(edge, r, 0); // 40, f 1 (the line is 37.5)
        var left = AthleticsMath.ChargeKickOrBash(edge, r, 1); // 37 < 37.5
        Assert.True(left.LeftPeak);

        AthleticsMath.ChargeKickOrBash(f, r, 5); // 7
        AthleticsMath.ChargeKickOrBash(f, r, 6); // 4
        AthleticsMath.ChargeKickOrBash(f, r, 7); // 1
        var last = AthleticsMath.ChargeKickOrBash(f, r, 8); // 1 → 0
        Assert.True(last.EnteredExhaustion);
        Assert.Equal(0.0, last.After, 9);
        Assert.True(f.Exhausted);
        var atZero = AthleticsMath.ChargeKickOrBash(f, r, 9);
        Assert.True(atZero.Charged);
        Assert.Equal(0.0, atZero.Before - atZero.After, 9); // drains nothing at 0
        Assert.False(atZero.EnteredExhaustion);
        Assert.Equal(1, f.ExhaustionsEntered);
    }

    // ------------------------------------------------------------------ one decision per action

    private const int Idle = 35, ReleaseMelee = 20, Kick = KickBashTracker.ActionKick, KickHit = KickBashTracker.ActionKickHit,
        KickContinue = KickBashTracker.ActionKickContinue, Bash = KickBashTracker.ActionWeaponBash;

    [Fact]
    public void The_engine_codes_map_to_kicks_and_bashes()
    {
        Assert.Equal(KickBashKind.Kick, KickBashTracker.KindOf(28));
        Assert.Equal(KickBashKind.Kick, KickBashTracker.KindOf(29));
        Assert.Equal(KickBashKind.Kick, KickBashTracker.KindOf(30));
        Assert.Equal(KickBashKind.Bash, KickBashTracker.KindOf(31));
        Assert.Equal(KickBashKind.None, KickBashTracker.KindOf(ReleaseMelee));
        Assert.Equal(KickBashKind.None, KickBashTracker.KindOf(Idle));
        Assert.Equal(KickBashKind.None, KickBashTracker.KindOf(-1));
    }

    [Fact]
    public void A_kick_is_decided_once_at_its_start_whatever_it_turns_into()
    {
        var t = new KickBashTracker();
        Assert.Equal(KickBashKind.Kick, t.Observe(false, Kick, 1.0));
        Assert.True(t.Running);
        Assert.Equal(KickBashKind.None, t.Observe(false, KickHit, 1.2));      // it landed: the same kick
        Assert.Equal(KickBashKind.None, t.Observe(false, KickContinue, 1.3));
        Assert.False(t.Hit(1.25));                                             // its hit: already paid
        Assert.Equal(KickBashKind.None, t.Observe(false, Idle, 1.6));
        Assert.False(t.Running);
        Assert.False(t.Hit(1.61));                                             // a hit a frame after its end: the same one
        Assert.Equal(KickBashKind.Kick, t.Observe(false, Kick, 1.7));          // the next kick: a new one
        Assert.Equal(KickBashKind.None, t.Observe(false, ReleaseMelee, 2.5));
        Assert.Equal(KickBashKind.Bash, t.Observe(false, Bash, 3.0));
        Assert.Equal(KickBashKind.Kick, t.Observe(false, Kick, 3.4));          // straight from a bash into a kick: new
    }

    [Fact]
    public void One_kick_on_both_channels_is_one_kick()
    {
        var t = new KickBashTracker();
        Assert.Equal(KickBashKind.Kick, t.Observe(true, Kick, 1.0));           // the whole-body channel first
        Assert.Equal(KickBashKind.None, t.Observe(false, Kick, 1.02));         // the upper body shows the same kick
        Assert.Equal(KickBashKind.None, t.Observe(false, Idle, 1.4));          // the upper body done, the legs not yet
        Assert.Equal(KickBashKind.None, t.Observe(false, Kick, 1.45));         // …still that kick
        Assert.Equal(KickBashKind.None, t.Observe(true, Idle, 1.6));
        Assert.Equal(KickBashKind.None, t.Observe(false, Idle, 1.6));
        Assert.Equal(KickBashKind.Kick, t.Observe(false, Kick, 2.0));          // then a new one, seen on channel 1
    }

    [Fact]
    public void A_kick_or_bash_no_channel_showed_is_decided_at_its_hit_once()
    {
        var t = new KickBashTracker();
        Assert.True(t.Hit(5.0));                                               // the poll missed it: charged at the hit
        Assert.True(t.DecidedByHit);
        Assert.False(t.Hit(5.1));                                              // a second hit of the same bash
        Assert.Equal(KickBashKind.None, t.Observe(false, Bash, 5.05));        // the poll sees it after the hit: paid already
        Assert.False(t.DecidedByHit);
        Assert.Equal(KickBashKind.None, t.Observe(false, Idle, 5.5));
        Assert.Equal(KickBashKind.Bash, t.Observe(false, Bash, 6.5));          // the next one: charged at its start

        var late = new KickBashTracker();
        Assert.True(late.Hit(1.0));
        Assert.Equal(KickBashKind.Kick, late.Observe(true, Kick, 1.0 + KickBashTracker.SameActionSeconds)); // too late to be that one
        Assert.True(new KickBashTracker().Hit(0));
        var again = new KickBashTracker();
        Assert.True(again.Hit(1.0));
        Assert.True(again.Hit(2.5));                                           // hits far apart: two kicks the poll never saw
    }
}

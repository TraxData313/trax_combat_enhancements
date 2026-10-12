using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>Step 23 (Anton, 2026-09-29): brace by orders - the floor by the formation's order, the hysteresis (floor → floor +
/// margin), each man's own margin rolled once per battle (± the spread), the wound cap, an order change mid-brace, the off
/// switches, the shield step, the input frame and the summary lines.</summary>
public class BraceTests
{
    private static BraceRules Rules(int charge = 20, int advance = 40, int hold = 60, int recover = 20, int spread = 0, bool on = true,
        bool mod = true, bool athletics = true, bool wield = true, bool raise = true, int noFormation = 20, int floorSpread = 0) =>
        new(mod, athletics, on, charge, advance, hold, recover, wield, raise, spread, noFormation, floorSpread);

    // ------------------------------------------------------------------ the floor by order

    [Fact]
    public void Each_order_falls_in_its_floor_group()
    {
        Assert.Equal(BraceOrder.Charge, BraceMath.Group(BraceOrderKind.Charge));
        Assert.Equal(BraceOrder.Charge, BraceMath.Group(BraceOrderKind.ChargeToTarget));
        Assert.Equal(BraceOrder.Charge, BraceMath.Group(BraceOrderKind.AttackEntity));
        Assert.Equal(BraceOrder.Charge, BraceMath.Group(BraceOrderKind.AiCharge));
        Assert.Equal(BraceOrder.Advance, BraceMath.Group(BraceOrderKind.Advance));
        Assert.Equal(BraceOrder.Advance, BraceMath.Group(BraceOrderKind.AiAdvance));
        foreach (var k in new[] { BraceOrderKind.Stop, BraceOrderKind.Move, BraceOrderKind.Retreat, BraceOrderKind.FallBack, BraceOrderKind.Follow,
                     BraceOrderKind.FollowEntity, BraceOrderKind.NoOrder })
            Assert.Equal(BraceOrder.Hold, BraceMath.Group(k));
        Assert.Equal(BraceOrder.NoFormation, BraceMath.Group(BraceOrderKind.NoFormation)); // step 28: its own floor
    }

    [Theory]
    [InlineData(BraceOrder.Charge, 0.20, 0.40)]
    [InlineData(BraceOrder.Advance, 0.40, 0.60)]
    [InlineData(BraceOrder.Hold, 0.60, 0.80)]
    [InlineData(BraceOrder.NoFormation, 0.20, 0.40)]
    public void The_band_is_the_orders_floor_to_floor_plus_the_margin(BraceOrder order, double floor, double target)
    {
        var b = BraceMath.Band(Rules(), order, 1.0);
        Assert.Equal(floor, b.Floor, 9);
        Assert.Equal(target, b.Target, 9);
        Assert.False(b.Capped);
        Assert.True(BraceMath.Enter(b, floor));              // at the floor
        Assert.True(BraceMath.Enter(b, floor - 0.1));        // below it
        Assert.False(BraceMath.Enter(b, floor + 0.001));     // above it
        Assert.False(BraceMath.Leave(b, target - 0.001));    // the hysteresis: still bracing just under the target
        Assert.True(BraceMath.Leave(b, target));
    }

    [Fact]
    public void A_hold_brace_runs_from_60_to_80_and_not_before()
    {
        var b = BraceMath.Band(Rules(), BraceOrder.Hold, 1.0);
        bool bracing = false;
        var seen = new System.Collections.Generic.List<(double, bool)>();
        foreach (double x in new[] { 1.0, 0.7, 0.61, 0.60, 0.5, 0.65, 0.75, 0.79, 0.80, 0.7, 0.61 })
        {
            if (!bracing && BraceMath.Enter(b, x)) bracing = true;
            else if (bracing && BraceMath.Leave(b, x)) bracing = false;
            seen.Add((x, bracing));
        }
        Assert.Equal(new[] { false, false, false, true, true, true, true, true, false, false, false }, seen.ConvertAll(s => s.Item2).ToArray());
    }

    // ------------------------------------------------------------------ wounds

    [Fact]
    public void A_wound_caps_the_target_and_slides_the_band_down_so_he_is_never_stuck()
    {
        // top 0.5 under a hold floor of 60: he cannot reach 80 - the band keeps its width 20 under his cap: 30 → 50
        var b = BraceMath.Band(Rules(), BraceOrder.Hold, 0.5);
        Assert.Equal(0.30, b.Floor, 9);
        Assert.Equal(0.50, b.Target, 9);
        Assert.True(b.Capped);
        Assert.Equal(0.60, b.SetFloor, 9);
        Assert.Equal(0.80, b.SetTarget, 9);
        Assert.False(BraceMath.Enter(b, 0.5));   // full to his cap: never braces
        Assert.True(BraceMath.Enter(b, 0.3));
        Assert.True(BraceMath.Leave(b, 0.5));    // refilled to his cap = done
        Assert.Equal(BraceEnd.WoundCap, BraceMath.LeaveReason(BraceOrder.Hold, BraceOrder.Hold, b));

        // a slight wound (top 0.7): the target 70, the floor stays 50 below it
        var slight = BraceMath.Band(Rules(), BraceOrder.Hold, 0.7);
        Assert.Equal(0.50, slight.Floor, 9);
        Assert.Equal(0.70, slight.Target, 9);

        // a wound deeper than the margin (top 0.1): from empty to his cap
        var deep = BraceMath.Band(Rules(), BraceOrder.Hold, 0.1);
        Assert.Equal(0.0, deep.Floor, 9);
        Assert.Equal(0.1, deep.Target, 9);
        Assert.True(BraceMath.Enter(deep, 0.0));
        Assert.False(BraceMath.Enter(deep, 0.05));

        // a wound that leaves floor + margin reachable changes nothing
        var fine = BraceMath.Band(Rules(), BraceOrder.Charge, 0.9);
        Assert.Equal(0.20, fine.Floor, 9);
        Assert.Equal(0.40, fine.Target, 9);
        Assert.False(fine.Capped);
    }

    // ------------------------------------------------------------------ the order changes mid-brace

    [Fact]
    public void A_new_order_applies_at_once()
    {
        // bracing under hold at 55%: the order becomes charge (target 40) → he leaves at once, "the order changed"
        var charge = BraceMath.Band(Rules(), BraceOrder.Charge, 1.0);
        Assert.True(BraceMath.Leave(charge, 0.55));
        Assert.Equal(BraceEnd.OrderChanged, BraceMath.LeaveReason(BraceOrder.Hold, BraceOrder.Charge, charge));
        // at 35% under charge (not bracing) the order becomes hold → he braces at once
        var hold = BraceMath.Band(Rules(), BraceOrder.Hold, 1.0);
        Assert.False(BraceMath.Enter(charge, 0.35));
        Assert.True(BraceMath.Enter(hold, 0.35));
        // a plain refill under the same order is "refilled"
        Assert.Equal(BraceEnd.Refilled, BraceMath.LeaveReason(BraceOrder.Hold, BraceOrder.Hold, hold));
    }

    // ------------------------------------------------------------------ each man's own margin

    [Fact]
    public void Each_man_rolls_his_margin_once_and_the_spread_is_read_live()
    {
        var dice = new SeededRandom(23);
        double lo = 1, hi = -1;
        for (int i = 0; i < 2000; i++)
        {
            double u = BraceMath.RollUnit(dice);
            Assert.InRange(u, -1.0, 1.0);
            lo = System.Math.Min(lo, u);
            hi = System.Math.Max(hi, u);
        }
        Assert.True(lo < -0.95 && hi > 0.95, "the roll does not cover [-1, 1): " + lo + " .. " + hi);

        // ± 5 points: a man at +1 needs 25 above his floor, one at -1 only 15, one at 0 exactly 20
        var r = Rules(spread: 5);
        Assert.Equal(0.85, BraceMath.Band(r, BraceOrder.Hold, 1.0, 1.0).Target, 9);
        Assert.Equal(0.75, BraceMath.Band(r, BraceOrder.Hold, 1.0, -1.0).Target, 9);
        Assert.Equal(0.80, BraceMath.Band(r, BraceOrder.Hold, 1.0, 0.0).Target, 9);
        Assert.Equal(0.025, BraceMath.Offset(r, 0.5), 9);
        // the slider rescales everyone at once (his roll kept): at 10 the same man at +0.5 gets +5
        Assert.Equal(0.05, BraceMath.Offset(Rules(spread: 10), 0.5), 9);
        Assert.Equal(0.0, BraceMath.Offset(Rules(spread: 0), 0.9), 9);
        // never below floor + 1 point: margin 3 - spread 5 at the bottom of the roll
        var tight = Rules(recover: 3, spread: 5);
        Assert.Equal(BraceMath.MinMargin, BraceMath.Margin(tight, -1.0), 9);
        Assert.Equal(0.61, BraceMath.Band(tight, BraceOrder.Hold, 1.0, -1.0).Target, 9);
        // the margin also sets the band's width under a wound cap
        var capped = BraceMath.Band(r, BraceOrder.Hold, 0.5, 1.0);   // margin 25
        Assert.Equal(0.25, capped.Floor, 9);
        Assert.Equal(0.50, capped.Target, 9);
    }

    // ------------------------------------------------------------------ step 28: no formation, each man's own floor

    [Fact]
    public void A_man_in_no_formation_braces_on_his_own_floor_not_the_holds()
    {
        // Anton's tournament log: no formation used the hold floor 60 - now its own 20 → 40
        var b = BraceMath.Band(Rules(), BraceOrder.NoFormation, 1.0);
        Assert.Equal(0.20, b.Floor, 9);
        Assert.Equal(0.40, b.Target, 9);
        Assert.False(BraceMath.Enter(b, 0.59));                 // the old hold floor no longer applies
        Assert.False(BraceMath.Enter(b, 0.21));
        Assert.True(BraceMath.Enter(b, 0.20));
        Assert.Equal(0.35, BraceMath.Band(Rules(noFormation: 35), BraceOrder.NoFormation, 1.0).Floor, 9);   // live, its own slider
        Assert.Equal(0.60, BraceMath.Band(Rules(noFormation: 35), BraceOrder.Hold, 1.0).Floor, 9);          // the hold floor untouched
        Assert.Equal("no formation", BraceMath.Name(BraceOrder.NoFormation));
    }

    [Fact]
    public void Each_man_rolls_his_floor_once_apart_from_his_margin_and_the_spread_is_read_live()
    {
        var r = Rules(spread: 5, floorSpread: 10);
        // his floor = the order's ± up to 10, by his own floor roll: +1 → 70, −1 → 50, 0 → 60 (hold)
        Assert.Equal(0.70, BraceMath.Band(r, BraceOrder.Hold, 1.0, 0, 1.0).Floor, 9);
        Assert.Equal(0.50, BraceMath.Band(r, BraceOrder.Hold, 1.0, 0, -1.0).Floor, 9);
        Assert.Equal(0.60, BraceMath.Band(r, BraceOrder.Hold, 1.0, 0, 0).Floor, 9);
        Assert.Equal(0.25, BraceMath.Band(r, BraceOrder.NoFormation, 1.0, 0, 0.5).Floor, 9);     // everywhere, no formation too
        Assert.Equal(0.15, BraceMath.Band(r, BraceOrder.Charge, 1.0, 0, -0.5).Floor, 9);
        // independent: the floor roll moves the floor, the margin roll the width - the target = his floor + his margin
        var both = BraceMath.Band(r, BraceOrder.Hold, 1.0, 1.0, -1.0);   // margin 25, floor 50
        Assert.Equal(0.50, both.Floor, 9);
        Assert.Equal(0.75, both.Target, 9);
        Assert.Equal(0.50, both.SetFloor, 9);
        var other = BraceMath.Band(r, BraceOrder.Hold, 1.0, -1.0, 1.0);  // margin 15, floor 70
        Assert.Equal(0.70, other.Floor, 9);
        Assert.Equal(0.85, other.Target, 9);
        // the slider rescales everyone's roll live (each keeping his place); 0 = exactly the order's floor
        Assert.Equal(0.05, BraceMath.FloorOffset(r, 0.5), 9);
        Assert.Equal(0.15, BraceMath.FloorOffset(Rules(floorSpread: 30), 0.5), 9);
        Assert.Equal(0.0, BraceMath.FloorOffset(Rules(floorSpread: 0), -0.9), 9);
        Assert.Equal(0.60, BraceMath.Band(Rules(floorSpread: 0), BraceOrder.Hold, 1.0, 0, -0.9).Floor, 9);
        Assert.Equal(0.0, BraceMath.FloorOffset(r, double.NaN), 9);
        Assert.Equal(0.10, BraceMath.FloorOffset(r, 7.0), 9);   // a unit out of range clamped to ±1

        // the two rolls come from separate dice in the game; with one source they are still two draws, never the same number
        var dice = new SeededRandom(28);
        int same = 0;
        for (int i = 0; i < 500; i++)
            if (BraceMath.RollUnit(dice) == BraceMath.RollUnit(dice)) same++;
        Assert.Equal(0, same);
    }

    [Fact]
    public void His_floor_is_clamped_to_0_100_and_the_hysteresis_holds()
    {
        // a floor of 5 with -10: never negative - he braces only when empty, and stops at 0 + his margin
        var low = BraceMath.Band(Rules(charge: 5, floorSpread: 10), BraceOrder.Charge, 1.0, 0, -1.0);
        Assert.Equal(0.0, low.Floor, 9);
        Assert.Equal(0.0, low.SetFloor, 9);
        Assert.Equal(0.20, low.Target, 9);
        Assert.True(BraceMath.Enter(low, 0.0));
        Assert.False(BraceMath.Enter(low, 0.01));
        Assert.False(BraceMath.Leave(low, 0.19));
        Assert.True(BraceMath.Leave(low, 0.20));
        // a floor of 95 with +10: 100 at most, the target 100 (the bar's top) - and like any band at the top it keeps its
        // width M below it (step 23's rule): he braces at 80 and stops at full (never stuck: full is reachable)
        var high = BraceMath.Band(Rules(hold: 95, floorSpread: 10), BraceOrder.Hold, 1.0, 0, 1.0);
        Assert.Equal(1.0, high.SetFloor, 9);
        Assert.Equal(0.8, high.Floor, 9);
        Assert.Equal(1.0, high.Target, 9);
        Assert.False(BraceMath.Enter(high, 1.0));     // a full man never braces
        Assert.False(BraceMath.Enter(high, 0.9));
        Assert.True(BraceMath.Enter(high, 0.8));
        // every floor roll and every margin roll: the target is above the floor by at least 1 point, until the bar's top
        var dice = new SeededRandom(5);
        for (int i = 0; i < 2000; i++)
        {
            var rr = Rules(charge: dice.NextDouble() < 0.5 ? 0 : 3, recover: 3, spread: 5, floorSpread: 30);
            var band = BraceMath.Band(rr, BraceOrder.Charge, 1.0, BraceMath.RollUnit(dice), BraceMath.RollUnit(dice));
            Assert.InRange(band.Floor, 0.0, 1.0);
            Assert.True(band.Target >= band.Floor + BraceMath.MinMargin - 1e-9 || band.Target >= 1.0 - 1e-9);
        }
    }

    [Fact]
    public void A_wound_cap_slides_his_own_band_down_never_stuck()
    {
        // hold 60 + his floor +10 = 70, margin 20 → 70 → 90; capped at 50: the band keeps its width under the cap, 30 → 50
        var r = Rules(floorSpread: 10);
        var b = BraceMath.Band(r, BraceOrder.Hold, 0.5, 0, 1.0);
        Assert.True(b.Capped);
        Assert.Equal(0.30, b.Floor, 9);
        Assert.Equal(0.50, b.Target, 9);
        Assert.Equal(0.70, b.SetFloor, 9);
        Assert.False(BraceMath.Enter(b, 0.5));   // full to his cap: no brace
        Assert.True(BraceMath.Enter(b, 0.3));
        Assert.True(BraceMath.Leave(b, 0.5));    // he reaches his target by refilling
        // a no-formation man with his floor -10 (10 → 30) under a cap of 25: 5 → 25
        var nf = BraceMath.Band(r, BraceOrder.NoFormation, 0.25, 0, -1.0);
        Assert.Equal(0.05, nf.Floor, 9);
        Assert.Equal(0.25, nf.Target, 9);
        // a deep wound: from empty to his cap
        var deep = BraceMath.Band(r, BraceOrder.NoFormation, 0.1, 0, 1.0);
        Assert.Equal(0.0, deep.Floor, 9);
        Assert.Equal(0.1, deep.Target, 9);
    }

    // ------------------------------------------------------------------ switches

    [Fact]
    public void The_master_switch_first_then_athletics_then_the_brace()
    {
        Assert.True(Rules().Enabled);
        Assert.Null(Rules().OffBecause);
        Assert.Equal("ModEnabled", Rules(mod: false, athletics: false, on: false).OffBecause);
        Assert.Equal("AthleticsEnabled", Rules(athletics: false, on: false).OffBecause);
        Assert.Equal("BraceEnabled", Rules(on: false).OffBecause);
        Assert.False(Rules(on: false).Enabled);
        Assert.StartsWith("OFF (ModEnabled) - AI men swing at any Athletics", Rules(mod: false).Describe());
    }

    [Fact]
    public void The_rules_are_read_live_from_the_settings()
    {
        var s = new TraxSettings();
        var r = BraceRules.From(s);
        Assert.True(r.Enabled);
        Assert.Equal(20, r.FloorChargePercent);
        Assert.Equal(40, r.FloorAdvancePercent);
        Assert.Equal(60, r.FloorHoldPercent);
        Assert.Equal(20, r.RecoverPercent);
        Assert.Equal(5, r.RecoverSpreadPercent);
        Assert.Equal(20, r.FloorNoFormationPercent);
        Assert.Equal(10, r.FloorSpreadPercent);
        Assert.True(r.WieldShield);
        Assert.True(r.RaiseShield);
        s.Set(SettingsSchema.BraceFloorHoldPercent, 70, SettingSources.Mcm);
        s.Set(SettingsSchema.BraceRecoverSpreadPercent, 80, SettingSources.Mcm); // above its range: clamped to 50
        var after = BraceRules.From(s);
        Assert.Equal(70, after.FloorHoldPercent);
        Assert.Equal(50, after.RecoverSpreadPercent);
        Assert.False(after.SameAs(r));
        Assert.True(r.SameAs(Rules(spread: 5, floorSpread: 10)));
        Assert.False(r.SameAs(Rules(spread: 5, floorSpread: 9)));
        Assert.False(r.SameAs(Rules(spread: 5, floorSpread: 10, noFormation: 30)));
        s.Set(SettingsSchema.BraceFloorSpreadPercent, 99, SettingSources.Mcm);       // above its range: clamped to 30
        s.Set(SettingsSchema.BraceFloorNoFormationPercent, 35, SettingSources.Mcm);
        Assert.Equal(30, BraceRules.From(s).FloorSpreadPercent);
        Assert.Equal(35, BraceRules.From(s).FloorNoFormationPercent);
        Assert.Equal(35, BraceRules.From(s).FloorPercent(BraceOrder.NoFormation));
        s.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
        Assert.Equal("ModEnabled", BraceRules.From(s).OffBecause);
        Assert.Same(SettingsSchema.BraceGroup, SettingsSchema.BraceEnabled.Group);
        Assert.Equal(SettingsSchema.BattlePaceGroup.Order + 1, SettingsSchema.BraceGroup.Order);
        Assert.Same(SettingsSchema.AdvancedGroup, SettingsSchema.Groups[SettingsSchema.Groups.Count - 1]);
    }

    [Fact]
    public void The_settings_sentence()
    {
        string on = Rules(spread: 5).Describe();
        Assert.StartsWith("ON - an AI man whose Athletics bar (points ÷ his full pool, what the bar shows) is at or below his formation's order floor stops attacking in melee and only defends (guard up) until he refills to floor + 20% (BraceRecoverPercent) ± his own 5 (BraceRecoverSpreadPercent, rolled once per man and battle): charge 20% → 40% (BraceFloorChargePercent), advance 40% → 60% (BraceFloorAdvancePercent), hold / halt / retreat / move / follow / no order 60% → 80% (BraceFloorHoldPercent), no formation (tournaments, arenas, duels) 20% → 40% (BraceFloorNoFormationPercent)", on);
        Assert.DoesNotContain("BraceFloorSpreadPercent", on);
        Assert.StartsWith("ON - an AI man whose Athletics bar (points ÷ his full pool, what the bar shows) is at or below his formation's order floor ± his own 10 (BraceFloorSpreadPercent, rolled once per man and battle, apart from the margin's roll) stops attacking", Rules(floorSpread: 10).Describe());
        Assert.Contains("bows, crossbows and throws go on; the shield taken out if he carries one (BraceWieldShield), held up while he has no guard of his own (BraceRaiseShield); AI heroes and riders too, never you", on);
        Assert.Contains("exactly (BraceRecoverSpreadPercent 0)", Rules().Describe());
        Assert.Contains("the shield left as it is (BraceWieldShield off), raised only when he wants to attack (BraceRaiseShield off)", Rules(wield: false, raise: false).Describe());
    }

    // ------------------------------------------------------------------ the shield

    [Fact]
    public void The_shield_step_by_what_he_holds()
    {
        var none = HandFacts.None;
        Assert.Equal(ShieldStep.NoShield, BraceMath.ShieldStepFor(none));
        var h = new HandFacts { ShieldSlot = 1, OneHandedSlot = 0, ShieldInOffHand = true };
        Assert.Equal(ShieldStep.InHand, BraceMath.ShieldStepFor(h));
        h.ShieldInOffHand = false;
        Assert.Equal(ShieldStep.WieldShield, BraceMath.ShieldStepFor(h));              // a sword in hand: the shield goes with it
        h.MainEmpty = true;
        Assert.Equal(ShieldStep.WieldShield, BraceMath.ShieldStepFor(h));              // an empty hand too
        h.MainEmpty = false;
        h.MainNeedsBothHands = true;
        Assert.Equal(ShieldStep.WieldOneHanded, BraceMath.ShieldStepFor(h));           // a two-hander: the sword first
        h.OneHandedSlot = -1;
        Assert.Equal(ShieldStep.NoOneHandedWeapon, BraceMath.ShieldStepFor(h));
        h.MainRanged = true;
        Assert.Equal(ShieldStep.RangedInHand, BraceMath.ShieldStepFor(h));             // a bow in hand is left alone
    }

    [Fact]
    public void The_shield_is_raised_only_when_he_holds_no_guard_and_no_attack()
    {
        Assert.Equal(AiInputMath.DefendDown | 0x1u, AiInputMath.RaiseShield(0x1u, out bool raised));   // walking forward, no guard: raised
        Assert.True(raised);
        uint ownGuard = 0x400u; // DefendLeft
        Assert.Equal(ownGuard, AiInputMath.RaiseShield(ownGuard, out raised));
        Assert.False(raised);
        // after HoldAttacks an attack wish is already a guard: nothing more to do
        uint held = AiInputMath.HoldAttacks(0x100u, true, false, out _);
        Assert.Equal(held, AiInputMath.RaiseShield(held, out raised));
        Assert.False(raised);
    }

    // ------------------------------------------------------------------ the summary

    [Fact]
    public void The_summary_lines_answer_whether_lines_turtle()
    {
        var r = Rules(spread: 5);
        var st = new BraceStats();
        Assert.Equal("brace: no AI fighter was looked at (the feature was off, or nobody fought)", st.SummaryLines(r)[1]);
        st.AddMan(-0.8, 0.9);
        st.AddMan(0.0, -0.5);
        st.AddMan(0.6, 0.1);
        st.AiManSeconds = 300;
        st.AddStart(BraceOrderKind.Stop, true);
        st.AddStart(BraceOrderKind.Stop, false);
        st.AddStart(BraceOrderKind.Charge, true);
        st.AddShieldAtStart(ShieldStep.WieldShield);
        st.AddShieldAtStart(ShieldStep.NoShield);
        st.AddShieldAtStart(ShieldStep.InHand);
        st.ShieldCalls = 1;
        st.ShieldHeld = 1;
        st.AddEnd(BraceEnd.Refilled, 12, -0.8, "(agent 1)", 0.9);
        st.AddEnd(BraceEnd.OrderChanged, 3, 0.0, "(agent 2)", -0.5);
        st.AddEnd(BraceEnd.MissionEnd, 45, 0.6, "(agent 3)", 0.9);
        st.AddHit(true);
        st.AddHit(false);
        st.MeleeWhileBracing = 0;
        st.RangedWhileBracing = 2;
        var lines = st.SummaryLines(r);
        Assert.Equal(8, lines.Count);
        Assert.StartsWith("brace by orders (step 23): ON - ", lines[0]);
        Assert.Equal("brace: 2 of 3 AI fighters braced, 3 braces - by order: charge 1, stop / hold 2; by floor: charge 1, advance 0, hold and the rest 2, no formation 0; time bracing 60 man-seconds = 20.0% of the AI fighters' time on the field (300 man-seconds), avg 20.0 s a brace, 30.0 s per man who braced, the longest 45.0 s ((agent 3))", lines[1]);
        Assert.StartsWith("brace lengths: under 5 s 1, 5-15 s 1, 15-30 s 0, 30-60 s 1, 60 s or more 0; still bracing when the battle ended 1 (1 of them for 30 s or more)", lines[2]);
        Assert.Equal("brace ends: refilled to floor + recover 1, the order changed 1, refilled to his wound cap 0, switched off 0, fell or left the field 0, the player took him 0, the hideout boss fight's fresh start 0, the battle ended 1, an error (lifted to stay safe) 0", lines[3]);
        Assert.Equal("brace margins (BraceRecoverSpreadPercent 5): 3 men rolled, their offsets now min -4.0 / avg -0.3 / max +3.0 points on 20%; braces by his own margin: short (−) 1 (avg 12.0 s), middle 1 (avg 3.0 s), long (+) 1 (avg 45.0 s)", lines[4]);
        // step 28: the floor offsets (spread 0 in Rules(spread: 5) - shown with 10 below)
        Assert.Equal("brace floors (BraceFloorSpreadPercent 0, step 28): 3 men rolled, their floor offsets now min 0.0 / avg 0.0 / max 0.0 points on the order's floor; braces by his own floor: low (−, fights on longer) 1 (avg 3.0 s), middle 0, high (+, braces sooner) 2 (avg 28.5 s)", lines[5]);
        Assert.StartsWith("brace floors (BraceFloorSpreadPercent 10, step 28): 3 men rolled, their floor offsets now min -5.0 / avg +1.7 / max +9.0 points", st.SummaryLines(Rules(spread: 5, floorSpread: 10))[5]);
        Assert.Contains("already in hand 1, taken out 1, a one-hander first 0, no shield 1", lines[6]);
        Assert.Contains("the shield seen in his hand after we asked 1, the AI put it away again while bracing 0", lines[6]);
        Assert.StartsWith("brace GUARD: melee hits taken while bracing 2, blocked 1 (50.0%", lines[7]);
        Assert.Contains("ranged shots while bracing 2 (allowed)", lines[7]);
        st.AddStart(BraceOrderKind.NoFormation, true);
        Assert.Contains("by order: charge 1, stop / hold 2, no formation 1; by floor: charge 1, advance 0, hold and the rest 2, no formation 1;", st.SummaryLines(r)[1]);
        st.SettingsChanged = true;
        Assert.EndsWith("its settings CHANGED during this battle (the lines mix both)", st.SummaryLines(r)[0]);
        Assert.Equal(0, BraceStats.LengthBin(4.9));
        Assert.Equal(4, BraceStats.LengthBin(61));
        Assert.Equal(0, BraceStats.UnitBand(-0.5));
        Assert.Equal(1, BraceStats.UnitBand(0.3));
        Assert.Equal(2, BraceStats.UnitBand(0.5));
    }

    [Fact]
    public void The_formation_stats_count_the_men_bracing_and_the_ready()
    {
        var s = new FormationAthleticsStats(40, 50, 5, 0.5, 0.1, 2, 0.7, 10, 0.9, 12);
        Assert.Equal(12, s.Bracing);
        Assert.Equal(28, s.Ready);
        Assert.Equal(40, s.Total);
        Assert.Equal(0, new FormationAthleticsStats(3, 1, 0, 1, 0, 0).Bracing);
        Assert.Equal(3, new FormationAthleticsStats(3, 1, 0, 1, 0, 0, bracing: 9).Bracing); // never more than the men
    }
}

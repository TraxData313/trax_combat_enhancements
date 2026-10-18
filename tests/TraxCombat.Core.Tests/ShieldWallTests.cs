using System;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>Step 41 (Anton, 2026-10-17): the shield wall holds its slots - the gates in order with the master switch first, the
/// arrangements and orders that hold, the escape (far from his slot AND behind the line), "ahead of his slot" on any facing,
/// the live switch and the summary lines.</summary>
public class ShieldWallTests
{
    private static WallRules On => new WallRules(true, true);

    /// <summary>A man held in every way: an AI man on foot in a halted shield wall facing +y, 0.5 m ahead of his slot, the
    /// formation's centre 2 m behind him, speed 3.5 m/s.</summary>
    private static WallFacts Held() => new WallFacts
    {
        Ai = true,
        HasFormation = true,
        Arrangement = WallArrangement.ShieldWall,
        Order = BraceOrderKind.Stop,
        HasPositions = true,
        AgentX = 10, AgentY = 20.5,
        SlotX = 10, SlotY = 20,
        DirX = 0, DirY = 1,
        CentreX = 10, CentreY = 18.5,
        AvgSpeed = 3.5,
        Deviation = 1.2,
    };

    // ------------------------------------------------------------------ the gates

    [Fact]
    public void A_man_in_a_halted_shield_wall_is_held()
    {
        var f = Held();
        Assert.Equal(WallVerdict.Hold, WallMath.Eligible(f));
        Assert.Equal(WallVerdict.Hold, WallMath.Decide(On, f));
    }

    [Fact]
    public void The_master_switch_decides_first_then_the_switch()
    {
        // every other gate fails too - the master switch still answers first
        var bad = new WallFacts { Naval = true, SiegeAttacker = true, Mounted = true, Retreating = true };
        Assert.Equal(WallVerdict.ModOff, WallMath.Decide(new WallRules(false, true), bad));
        Assert.Equal(WallVerdict.ModOff, WallMath.Decide(new WallRules(false, false), Held()));
        Assert.Equal(WallVerdict.SwitchedOff, WallMath.Decide(new WallRules(true, false), bad));
        Assert.Equal(WallVerdict.SwitchedOff, WallMath.Decide(new WallRules(true, false), Held()));
        Assert.Equal("ModEnabled", new WallRules(false, false).OffBecause);
        Assert.Equal("ShieldWallHoldsSlots", new WallRules(true, false).OffBecause);
        Assert.Null(On.OffBecause);
    }

    [Fact]
    public void Each_gate_in_order_names_its_reason()
    {
        // break the gates one by one from the LAST to the first: each new break is the answer (it comes earlier)
        var f = Held();
        f.AgentY = 20 - 30; // 30 m behind his slot (and the line): the escape
        Assert.Equal(WallVerdict.FarBehind, WallMath.Decide(On, f));
        f.Retreating = true;
        Assert.Equal(WallVerdict.Retreating, WallMath.Decide(On, f));
        f.Order = BraceOrderKind.Charge;
        Assert.Equal(WallVerdict.ChargeOrder, WallMath.Decide(On, f));
        f.Arrangement = WallArrangement.Other;
        Assert.Equal(WallVerdict.Arrangement, WallMath.Decide(On, f));
        f.Detached = true;
        Assert.Equal(WallVerdict.Detached, WallMath.Decide(On, f));
        f.HasFormation = false;
        Assert.Equal(WallVerdict.NoFormation, WallMath.Decide(On, f));
        f.Mounted = true;
        Assert.Equal(WallVerdict.Mounted, WallMath.Decide(On, f));
        f.Ai = false;
        Assert.Equal(WallVerdict.NotAi, WallMath.Decide(On, f));
        f.SiegeAttacker = true;
        Assert.Equal(WallVerdict.SiegeAttacker, WallMath.Decide(On, f));
        f.Naval = true;
        Assert.Equal(WallVerdict.Naval, WallMath.Decide(On, f));
        Assert.Equal(WallVerdict.SwitchedOff, WallMath.Decide(new WallRules(true, false), f));
        Assert.Equal(WallVerdict.ModOff, WallMath.Decide(new WallRules(false, true), f));
    }

    [Theory]
    [InlineData(WallArrangement.ShieldWall, WallVerdict.Hold)]
    [InlineData(WallArrangement.Square, WallVerdict.Hold)]
    [InlineData(WallArrangement.Circle, WallVerdict.Hold)]
    [InlineData(WallArrangement.Column, WallVerdict.Arrangement)]
    [InlineData(WallArrangement.Other, WallVerdict.Arrangement)]
    public void Only_the_three_defensive_arrangements_hold(WallArrangement a, WallVerdict expected)
    {
        var f = Held();
        f.Arrangement = a;
        Assert.Equal(expected, WallMath.Decide(On, f));
        Assert.Equal(expected == WallVerdict.Hold, WallMath.IsDefensive(a));
    }

    [Theory]
    [InlineData(BraceOrderKind.Stop, WallVerdict.Hold)]
    [InlineData(BraceOrderKind.Move, WallVerdict.Hold)]
    [InlineData(BraceOrderKind.Advance, WallVerdict.Hold)]
    [InlineData(BraceOrderKind.AiAdvance, WallVerdict.Hold)]
    [InlineData(BraceOrderKind.FallBack, WallVerdict.Hold)]
    [InlineData(BraceOrderKind.Follow, WallVerdict.Hold)]
    [InlineData(BraceOrderKind.FollowEntity, WallVerdict.Hold)]
    [InlineData(BraceOrderKind.Charge, WallVerdict.ChargeOrder)]
    [InlineData(BraceOrderKind.ChargeToTarget, WallVerdict.ChargeOrder)]
    [InlineData(BraceOrderKind.AiCharge, WallVerdict.ChargeOrder)]
    [InlineData(BraceOrderKind.AttackEntity, WallVerdict.ChargeOrder)]
    [InlineData(BraceOrderKind.Retreat, WallVerdict.RetreatOrder)]
    [InlineData(BraceOrderKind.NoOrder, WallVerdict.NoOrder)]
    public void Every_order_but_a_charge_or_a_retreat_holds(BraceOrderKind order, WallVerdict expected)
    {
        var f = Held();
        f.Order = order;
        Assert.Equal(expected, WallMath.Decide(On, f));
    }

    [Fact]
    public void Eligible_ignores_the_switches_and_the_escape()
    {
        // the log samples a holding wall's men whichever way the switch stands, the escaped too (they stand behind)
        var f = Held();
        f.AgentY = -100;
        Assert.Equal(WallVerdict.Hold, WallMath.Eligible(f));
        Assert.Equal(WallVerdict.FarBehind, WallMath.Decide(On, f));
        Assert.Equal(WallVerdict.SwitchedOff, WallMath.Decide(new WallRules(true, false), f));
    }

    // ------------------------------------------------------------------ the escape

    [Fact]
    public void Far_is_vanillas_catch_up_radius()
    {
        Assert.Equal(6.0, WallMath.CatchUpSpeeds);
        Assert.False(WallMath.FarFromSlot(20.9, 3.5));
        Assert.True(WallMath.FarFromSlot(21.0, 3.5));
        Assert.False(WallMath.FarFromSlot(500, 0)); // the game's cache still empty: never "far"
    }

    [Fact]
    public void Only_far_AND_behind_escapes()
    {
        var f = Held();
        f.AgentY = 20 - 25;                    // 25 m behind his slot, behind the centre
        Assert.True(WallMath.Escapes(f));
        f.AgentY = 20 + 25;                    // 25 m AHEAD of his slot: held (walked back)
        Assert.False(WallMath.Escapes(f));
        Assert.Equal(WallVerdict.Hold, WallMath.Decide(On, f));
        f.AgentY = 20 - 5;                     // behind, but near: held
        Assert.False(WallMath.Escapes(f));
        f.AgentY = 20 - 25;
        f.HasPositions = false;                // positions not read: never escapes
        Assert.False(WallMath.Escapes(f));
    }

    [Fact]
    public void Behind_the_line_is_measured_from_the_formations_centre()
    {
        var f = Held();
        // far to the side of his slot (a wide square) but level with the centre's front: not behind
        f.AgentX = 40; f.AgentY = 19;
        Assert.False(WallMath.BehindLine(f));
        f.AgentY = 18;
        Assert.True(WallMath.BehindLine(f));
    }

    // ------------------------------------------------------------------ ahead of his slot

    [Theory]
    [InlineData(0)]
    [InlineData(37)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(251)]
    public void Ahead_is_along_the_facing_on_any_direction(double degrees)
    {
        double a = degrees * Math.PI / 180;
        double dx = Math.Cos(a), dy = Math.Sin(a);
        var f = Held();
        f.SlotX = 100; f.SlotY = -50;
        f.DirX = dx; f.DirY = dy;
        // 1.5 m forward and 3 m to his left of the slot
        f.AgentX = f.SlotX + 1.5 * dx - 3 * dy;
        f.AgentY = f.SlotY + 1.5 * dy + 3 * dx;
        Assert.Equal(1.5, WallMath.Ahead(f), 9);
        // 2 m behind
        f.AgentX = f.SlotX - 2 * dx;
        f.AgentY = f.SlotY - 2 * dy;
        Assert.Equal(-2.0, WallMath.Ahead(f), 9);
        // a facing that is not a unit vector reads the same
        f.DirX = 5 * dx; f.DirY = 5 * dy;
        Assert.Equal(-2.0, WallMath.Ahead(f), 9);
    }

    [Fact]
    public void A_zero_facing_reads_zero()
    {
        var f = Held();
        f.DirX = 0; f.DirY = 0;
        Assert.Equal(0.0, WallMath.Ahead(f));
        Assert.Equal(0.5, WallMath.DistanceToSlot(Held()), 9);
    }

    // ------------------------------------------------------------------ live settings

    [Fact]
    public void The_switch_is_read_live_and_defaults_on()
    {
        var s = new TraxSettings();
        Assert.True(s.ShieldWallHoldsSlots);
        Assert.True(WallRules.From(s).Enabled);
        Assert.Equal(WallVerdict.Hold, WallMath.Decide(WallRules.From(s), Held()));
        s.Set(SettingsSchema.ShieldWallHoldsSlots, false, "test");
        Assert.Equal(WallVerdict.SwitchedOff, WallMath.Decide(WallRules.From(s), Held()));
        s.Set(SettingsSchema.ShieldWallHoldsSlots, true, "test");
        s.Set(SettingsSchema.ModEnabled, false, "test");
        Assert.Equal(WallVerdict.ModOff, WallMath.Decide(WallRules.From(s), Held()));
        Assert.False(WallRules.From(s).SameAs(On));
        Assert.Equal(SettingsSchema.PlayerAiGroup, SettingsSchema.ShieldWallHoldsSlots.Group); // the player page since step 59
    }

    [Fact]
    public void The_sentence_says_on_and_off()
    {
        Assert.StartsWith("ON (ShieldWallHoldsSlots) - every AI man on foot in a shield wall, square or circle keeps his slot", On.Describe());
        Assert.Contains("far (6 x the formation's speed) from his slot AND behind the line", On.Describe());
        Assert.StartsWith("OFF (ModEnabled) - vanilla", new WallRules(false, true).Describe());
        Assert.StartsWith("OFF (ShieldWallHoldsSlots) - vanilla", new WallRules(true, false).Describe());
    }

    // ------------------------------------------------------------------ the summary

    [Fact]
    public void The_samples_count_only_the_part_ahead()
    {
        var s = new WallAheadSamples();
        s.Add(0.5, false);
        s.Add(1.5, true);
        s.Add(2.5, true);
        s.Add(-3.0, false); // behind counts 0
        Assert.Equal(4, s.Samples);
        Assert.Equal(2, s.OverNear);
        Assert.Equal(1, s.OverFar);
        Assert.Equal(4.5, s.AheadSum, 9);
        Assert.Equal(2.5, s.Max, 9);
        Assert.Equal(2, s.Contact);
        Assert.Equal(2, s.ContactOverNear);
        Assert.Equal(0.5, s.ShareNear, 9);
        Assert.Equal(1.0, s.ContactShareNear, 9);
        Assert.Equal("4 samples of AI men in a holding shield wall / square / circle: more than 1 m ahead of his slot 50.0%, more than 2 m 25.0%, mean ahead 1.13 m (behind his slot counts 0), the farthest 2.5 m; in contact (an enemy within 5 m) 2 samples: more than 1 m ahead 100.0%, more than 2 m 50.0%, mean ahead 2.00 m, the farthest 2.5 m", s.Text());
    }

    [Fact]
    public void The_summary_lines_split_on_and_off()
    {
        var st = new WallStats();
        Assert.Equal(2, st.SummaryLines(On).Count); // nobody looked at
        st.MenLooked = 40;
        st.MenHeld = 30;
        for (int i = 0; i < 100; i++) st.AddLook(WallVerdict.Hold);
        st.AddLook(WallVerdict.Detached);
        st.AddLook(WallVerdict.FarBehind);
        st.AddLook(WallVerdict.Arrangement);
        st.AddWrite(WallWriteKind.AfterVanilla);
        st.AddWrite(WallWriteKind.AfterVanilla);
        st.AddWrite(WallWriteKind.Poll);
        st.AddVanillaUpdate(double.NaN);
        st.AddVanillaUpdate(0.5);
        st.AddVanillaUpdate(0.4);
        st.AddVanillaUpdate(9); // a gap: not an interval
        st.AddRelease(WallVerdict.SwitchedOff);
        st.SwitchChanges = 1;
        st.AddSample(true, 0.2, true);
        st.AddSample(false, 2.4, true);
        st.AddTick(0.01);
        var lines = st.SummaryLines(On);
        Assert.Equal(6, lines.Count);
        Assert.Contains("switched 1 time(s) during this battle", lines[0]);
        Assert.Equal("wall: 40 men looked at, 30 held in their slots at some point; looks - held 100, left to vanilla: detached 1, another arrangement 1, far from his slot and behind the line 1", lines[1]);
        Assert.StartsWith("wall writes: 3 \"keep your slot\" writes - right after vanilla's own update 2, by the 0.25 s look 1, by the fallback 0, by the step-out leash 0; vanilla's own updates seen on held men 4 (one every 0.45 s per man", lines[2]);
        Assert.Contains("released to vanilla's own value 1 (switched off 1)", lines[2]);
        Assert.StartsWith("wall slots ON: 1 samples", lines[3]);
        Assert.Contains("more than 1 m ahead of his slot 0.0%", lines[3]);
        Assert.StartsWith("wall slots OFF (vanilla): 1 samples", lines[4]);
        Assert.Contains("more than 2 m 100.0%", lines[4]);
    }
}

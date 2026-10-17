using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>Step 42 (Anton, 2026-10-17, his playtest of step 41: held men reached and stopped, rarely hitting an enemy who stepped
/// back - "give them just a bit of a step ... but not as far"): the leash - 0 = the hard hold, free below L with an enemy close in
/// front, held at L, back in the slot before the next step, no enemy = held, the geometry (a shield wall's facing, a square's
/// outward side), the setting live and placed, the master switch, the sentence and the summary.</summary>
public class ShieldWallLeashTests
{
    private const double L = 1.0;

    /// <summary>A man in a halted shield wall facing +y, his slot at (10, 20), the centre 1.5 m behind, <paramref name="outM"/> m
    /// out of his slot.</summary>
    private static WallFacts Man(double outM = 0, WallArrangement a = WallArrangement.ShieldWall) => new WallFacts
    {
        Ai = true,
        HasFormation = true,
        Arrangement = a,
        Order = BraceOrderKind.Stop,
        HasPositions = true,
        AgentX = 10, AgentY = 20 + outM,
        SlotX = 10, SlotY = 20,
        DirX = 0, DirY = 1,
        CentreX = 10, CentreY = 18.5,
        AvgSpeed = 3.5,
        Deviation = 1.0,
    };

    /// <summary>A man in his slot, armed (as the first look in his slot leaves him).</summary>
    private static WallLeash Armed() => new WallLeash { Armed = true };

    // ------------------------------------------------------------------ the state machine

    [Fact]
    public void Leash_zero_is_the_hard_hold()
    {
        var s = Armed();
        Assert.Equal(LeashMove.None, LeashMath.Step(ref s, 0, 0, enemyClose: true, now: 1));
        Assert.False(s.Free);
        Assert.False(LeashMath.NeedsEnemy(in s, 0, 0)); // no enemy read at all at 0
        Assert.False(LeashMath.EnemyCloseInFront(Man(), 10, 21, 0, false));
        // a man out on the leash when it is set to 0: held at once, his state cleared
        var free = new WallLeash { Free = true, MaxOut = 0.6, StartedAt = 3 };
        Assert.Equal(LeashMove.LeashOff, LeashMath.Step(ref free, 0, 0.6, true, 4));
        Assert.False(free.Free);
        Assert.Equal(default(WallLeash).MaxOut, free.MaxOut);
    }

    [Fact]
    public void Freed_below_the_leash_with_an_enemy_close()
    {
        var s = Armed();
        Assert.True(LeashMath.NeedsEnemy(in s, L, 0.1));
        Assert.Equal(LeashMove.Freed, LeashMath.Step(ref s, L, 0.1, enemyClose: true, now: 5));
        Assert.True(s.Free);
        Assert.False(s.Armed);
        Assert.Equal(5, s.StartedAt);
        Assert.Equal(0.1, s.StartOut, 9);
        // still inside the leash, enemy still close: stays out, the farthest followed
        Assert.Equal(LeashMove.None, LeashMath.Step(ref s, L, 0.7, true, 5.2));
        Assert.True(s.Free);
        Assert.Equal(0.7, s.MaxOut, 9);
    }

    [Fact]
    public void Held_at_the_leash()
    {
        var s = Armed();
        LeashMath.Step(ref s, L, 0, true, 0);
        Assert.Equal(LeashMove.None, LeashMath.Step(ref s, L, 0.99, true, 0.3));
        Assert.Equal(LeashMove.Reined, LeashMath.Step(ref s, L, 1.12, true, 0.4));
        Assert.False(s.Free);
        Assert.True(s.Returning);
        Assert.Equal(1.12, s.MaxOut, 9);
        Assert.False(LeashMath.NeedsEnemy(in s, L, 1.12)); // held at the leash: no read until he is back
    }

    [Fact]
    public void Back_in_his_slot_before_the_next_step()
    {
        var s = Armed();
        LeashMath.Step(ref s, L, 0, true, 0);
        LeashMath.Step(ref s, L, 1.0, true, 0.5); // reined
        // the hysteresis: walking back, the enemy still close - no new step until he is back within 0.3 m
        Assert.Equal(LeashMove.None, LeashMath.Step(ref s, L, 0.8, true, 0.7));
        Assert.Equal(LeashMove.None, LeashMath.Step(ref s, L, 0.31, true, 0.9));
        Assert.False(s.Free);
        Assert.Equal(LeashMove.BackInSlot, LeashMath.Step(ref s, L, 0.3, true, 1.0));
        Assert.True(s.Armed);
        Assert.False(s.Free); // the return is its own look
        Assert.Equal(LeashMove.Freed, LeashMath.Step(ref s, L, 0.25, true, 1.25));
    }

    [Fact]
    public void Back_in_slot_is_at_most_half_the_leash()
    {
        Assert.Equal(0.3, LeashMath.RearmAt(1.0), 9);
        Assert.Equal(0.3, LeashMath.RearmAt(3.0), 9);
        Assert.Equal(0.2, LeashMath.RearmAt(0.4), 9);
        Assert.Equal(0.05, LeashMath.RearmAt(0.1), 9);
        // a short leash still has a gap between the two edges: reined at 0.4, back at 0.2
        var s = Armed();
        LeashMath.Step(ref s, 0.4, 0, true, 0);
        Assert.Equal(LeashMove.Reined, LeashMath.Step(ref s, 0.4, 0.4, true, 0.2));
        Assert.Equal(LeashMove.None, LeashMath.Step(ref s, 0.4, 0.25, true, 0.4));
        Assert.Equal(LeashMove.BackInSlot, LeashMath.Step(ref s, 0.4, 0.2, true, 0.6));
    }

    [Fact]
    public void No_enemy_close_is_held()
    {
        var s = Armed();
        Assert.Equal(LeashMove.None, LeashMath.Step(ref s, L, 0, enemyClose: false, now: 0));
        Assert.False(s.Free);
        LeashMath.Step(ref s, L, 0, true, 1);
        // the enemy gone half way out: held, he walks back before stepping again
        Assert.Equal(LeashMove.EnemyGone, LeashMath.Step(ref s, L, 0.6, false, 1.3));
        Assert.False(s.Free);
        Assert.False(s.Armed);
        Assert.True(s.Returning);
        // the enemy gone while he is still in his slot: armed at once (the enemy may come back)
        var t = Armed();
        LeashMath.Step(ref t, L, 0, true, 0);
        Assert.Equal(LeashMove.EnemyGone, LeashMath.Step(ref t, L, 0.1, false, 0.1));
        Assert.True(t.Armed);
        Assert.False(t.Returning);
        Assert.Equal(LeashMove.Freed, LeashMath.Step(ref t, L, 0.1, true, 0.2));
    }

    [Fact]
    public void A_man_first_seen_in_his_slot_may_step_at_once()
    {
        var s = default(WallLeash);
        Assert.True(LeashMath.NeedsEnemy(in s, L, 0.1));
        Assert.Equal(LeashMove.Freed, LeashMath.Step(ref s, L, 0.1, true, 0));
        // first seen well out of his slot (pushed out, walking back): not before he is back
        var t = default(WallLeash);
        Assert.False(LeashMath.NeedsEnemy(in t, L, 0.6));
        Assert.Equal(LeashMove.None, LeashMath.Step(ref t, L, 0.6, true, 0));
        Assert.False(t.Free);
    }

    [Fact]
    public void Live_leash_changes_bite_at_the_next_look()
    {
        var s = Armed();
        LeashMath.Step(ref s, 2.0, 0, true, 0);
        Assert.Equal(LeashMove.None, LeashMath.Step(ref s, 2.0, 1.5, true, 0.5));
        // the slider down to 1.0 while he is 1.5 m out: reined at the next look
        Assert.Equal(LeashMove.Reined, LeashMath.Step(ref s, 1.0, 1.5, true, 0.6));
    }

    [Fact]
    public void A_walk_out_at_two_metres_a_second_is_reined_within_a_fifth_of_a_metre()
    {
        // the man looked at every LookSeconds, walking out at 2 m/s: the leash bites at most 2 x 0.1 m past L
        var s = Armed();
        double outM = 0, t = 0, reinedAt = double.NaN;
        LeashMath.Step(ref s, L, outM, true, t);
        for (int k = 0; k < 100 && double.IsNaN(reinedAt); k++)
        {
            t += LeashMath.LookSeconds;
            outM += 2.0 * LeashMath.LookSeconds;
            if (LeashMath.Step(ref s, L, outM, true, t) == LeashMove.Reined) reinedAt = outM;
        }
        Assert.InRange(reinedAt, L, L + 2.0 * LeashMath.LookSeconds + 1e-9);
    }

    // ------------------------------------------------------------------ the geometry

    [Fact]
    public void An_enemy_close_in_front_is_within_the_leash_and_a_strike_of_the_slot()
    {
        var f = Man();
        Assert.Equal(2.0, LeashMath.StrikeReachMetres);
        Assert.True(LeashMath.EnemyCloseInFront(f, 10, 20 + 2.9, L, false));     // 2.9 m in front of his slot
        Assert.False(LeashMath.EnemyCloseInFront(f, 10, 20 + 3.1, L, false));    // beyond L + 2 m
        Assert.True(LeashMath.EnemyCloseInFront(f, 10, 20 + 3.4, L, true));      // stepping: + 0.5 m of hysteresis
        Assert.False(LeashMath.EnemyCloseInFront(f, 10, 20 + 3.6, L, true));
        Assert.False(LeashMath.EnemyCloseInFront(f, 10, 20 - 1.0, L, false));    // behind his slot
        Assert.False(LeashMath.EnemyCloseInFront(f, 12, 20, L, false));          // level with his slot, beside him
        Assert.True(LeashMath.EnemyCloseInFront(f, 12, 21.5, L, false));         // a diagonal one, 2.5 m off
        Assert.False(LeashMath.EnemyCloseInFront(f, double.NaN, 21, L, false));
        var none = f;
        none.HasPositions = false;
        Assert.False(LeashMath.EnemyCloseInFront(none, 10, 21, L, false));
    }

    [Fact]
    public void Out_is_the_facing_for_a_wall_and_away_from_the_centre_for_a_square_or_circle()
    {
        // a man on the LEFT side of a square: slot (5, 20), centre (10, 20), the formation facing +y
        var f = Man(a: WallArrangement.Square);
        f.SlotX = 5; f.SlotY = 20; f.CentreX = 10; f.CentreY = 20;
        f.AgentX = 4; f.AgentY = 20;                       // 1 m out to the left
        Assert.Equal(1.0, LeashMath.Out(f), 9);
        Assert.True(LeashMath.EnemyCloseInFront(f, 2.5, 20, L, false)); // an enemy 2.5 m to the left of his slot
        Assert.Equal(0.0, WallMath.Ahead(f), 9);           // step 41's measure stays along the facing
        f.Arrangement = WallArrangement.Circle;
        Assert.Equal(1.0, LeashMath.Out(f), 9);
        // the same man in a shield wall: out = along the facing, the enemy to his left is not in front
        f.Arrangement = WallArrangement.ShieldWall;
        Assert.Equal(0.0, LeashMath.Out(f), 9);
        Assert.False(LeashMath.EnemyCloseInFront(f, 2.5, 20, L, false));
        // a square's man at its centre: the facing again
        var c = Man(0.5, WallArrangement.Square);
        c.CentreX = 10; c.CentreY = 20.2;
        Assert.Equal(0.5, LeashMath.Out(c), 9);
    }

    // ------------------------------------------------------------------ the setting, live

    [Fact]
    public void The_setting_is_live_placed_and_defaults_to_a_metre()
    {
        var s = new TraxSettings();
        Assert.Equal(1.0, s.ShieldWallStepOutMetres, 6);
        Assert.Equal(1.0, WallRules.From(s).StepOutMetres, 6);
        Assert.Equal(1.0, WallRules.From(s).Leash, 6);
        var before = WallRules.From(s);
        s.Set(SettingsSchema.ShieldWallStepOutMetres, 2.0, "test");
        Assert.Equal(2.0, WallRules.From(s).StepOutMetres, 6);
        Assert.False(WallRules.From(s).SameAs(before));
        s.Set(SettingsSchema.ShieldWallStepOutMetres, 0.0, "test");
        Assert.Equal(0.0, WallRules.From(s).Leash);
        var all = SettingsSchema.All.ToList();
        Assert.Same(SettingsSchema.ShieldWallStepOutMetres, all[all.IndexOf(SettingsSchema.ShieldWallHoldsSlots) + 1]);
        Assert.Equal(SettingsSchema.BattlePaceGroup, SettingsSchema.ShieldWallStepOutMetres.Group);
        Assert.Equal(ParamType.Float, SettingsSchema.ShieldWallStepOutMetres.Type);
        Assert.Equal(ApplyTiming.Live, SettingsSchema.ShieldWallStepOutMetres.Timing);
        Assert.Equal(0.0, SettingsSchema.ShieldWallStepOutMetres.Min);
        Assert.Equal(LeashMath.MaxMetres, SettingsSchema.ShieldWallStepOutMetres.Max);
    }

    [Fact]
    public void The_master_switch_and_the_hold_switch_take_the_leash_to_zero()
    {
        Assert.Equal(0.0, new WallRules(false, true, 1.0).Leash);
        Assert.Equal(0.0, new WallRules(true, false, 1.0).Leash);
        Assert.Equal(1.0, new WallRules(true, true, 1.0).Leash);
        Assert.Equal(WallVerdict.ModOff, WallMath.Decide(new WallRules(false, true, 1.0), Man()));
        // the two-argument rules (step 41) = the hard hold
        Assert.Equal(0.0, new WallRules(true, true).StepOutMetres);
        // clamped to the setting's range
        Assert.Equal(3.0, new WallRules(true, true, 7).StepOutMetres);
        Assert.Equal(0.0, new WallRules(true, true, -1).StepOutMetres);
        Assert.Equal(0.0, new WallRules(true, true, double.NaN).StepOutMetres);
    }

    [Fact]
    public void The_sentence_names_the_leash()
    {
        Assert.EndsWith("a held man may step up to 1.0 m out of his slot at an enemy close in front of it (within 1.0 + 2.0 m of the slot), then is held again "
                        + "and walks back - he may step again once back within 0.30 m of his slot (ShieldWallStepOutMetres)", new WallRules(true, true, 1.0).Describe());
        Assert.EndsWith("no step out (ShieldWallStepOutMetres 0 - the hard hold of step 41)", new WallRules(true, true, 0).Describe());
        Assert.StartsWith("ON (ShieldWallHoldsSlots) - every AI man on foot", new WallRules(true, true, 1.0).Describe());
    }

    // ------------------------------------------------------------------ the summary

    [Fact]
    public void The_summary_line_counts_the_steps()
    {
        var ls = new WallLeashStats();
        Assert.Equal("wall step out (step 42): leash 0.0 m now - the leash was 0 (the hard hold of step 41) the whole battle: no steps", ls.SummaryLine(0));
        ls.EverOn = true;
        ls.HeldSeconds = 600; // 10 man-minutes
        Assert.StartsWith("wall step out (step 42): leash 1.0 m now - no man stepped out", ls.SummaryLine(1));
        ls.MenStepped = 3;
        for (int i = 0; i < 5; i++) ls.AddStart();
        ls.AddEnd(LeashEnd.ReachedLeash, 1.2, 1.0, 1.0);
        ls.AddEnd(LeashEnd.ReachedLeash, 1.0, 1.0, 1.0);
        ls.AddEnd(LeashEnd.EnemyGone, 0.4, 2.0, 1.0);
        ls.AddEnd(LeashEnd.LetGo, 0.2, 0.5, 1.0);
        ls.AddEnd(LeashEnd.StillOut, 0.2, 1.5, 1.0);
        ls.BackInSlot = 2;
        ls.AddSample(0.5, 1.0);
        ls.AddSample(1.1, 1.0);
        ls.AddSample(1.6, 1.0);
        ls.AddSample(0.0, 1.0);
        ls.AddSample(5.0, 0.0); // the leash at 0: not a leash sample
        Assert.Equal(4, ls.LeashSamples);
        Assert.Equal("wall step out (step 42): leash 1.0 m now - 5 steps out by 3 men (0.50 a man a minute while held, out of their slots 1.0% of the held time); "
                     + "each step's farthest out: mean 0.60 m, the farthest 1.20 m; ended: reached the leash 2, enemy gone 1, let go to vanilla 1, still out at the end 1; "
                     + "reined at the leash 0.10 m past it on average, at most 0.20 m (the 0.1 s look); back in their slots after a step 2; in contact past the leash 50.0%, "
                     + "past it + 0.5 m 25.0% (of 4 in-contact samples with the leash on - the overshoot: should be near 0)", ls.SummaryLine(1));
    }
}

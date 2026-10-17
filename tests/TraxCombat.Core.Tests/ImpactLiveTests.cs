using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 50 (IMPACT I3, kicks and bashes LIVE - AI_NOTES "Step 50"): when the kicks family applies (every switch, the master switch first,
/// shadow = log only, the spike = shadow), which decisions write the men's states (applied and shadow yes, a live family with no effect built
/// no), the factors' ranges (the reel x1 fresh → up to x2.5 x (1 ± 30%) empty, the get-up 1-2, the randomness growing with T), the speed asked
/// of the engine, the anti-abuse on the measured ends (2 s after a reel, 5 s after getting up, the 8 s halving), the pickup lock, vanilla's
/// drop rule, and the [summary] "impact live" lines.
/// </summary>
public class ImpactLiveTests
{
    private sealed class Dice : IRandomSource
    {
        private readonly double[] _v;
        private int _i;
        public double Rest = 0.999;

        public Dice(params double[] values) => _v = values;

        public double NextDouble() => _i < _v.Length ? _v[_i++] : Rest;
    }

    private static ImpactRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return ImpactRules.From(s);
    }

    /// <summary>A kick by a fresh 100-point man on an even man at <paramref name="bar"/> (pool 100, the peak line at 75 %).</summary>
    private static ImpactFacts Kick(double bar, bool shield = true, bool player = false, bool mounted = false) => new ImpactFacts
    {
        Event = ImpactEvent.Kick,
        AttackerPoints = 100,
        AttackerF = 1,
        AttackerAnimation = 1,
        AttackerSkill = 100,
        DefenderPoints = 100 * bar,
        DefenderBar = bar,
        DefenderF = Math.Min(1, bar / 0.75),
        DefenderSkill = 100,
        DefenderHasShield = shield,
        DefenderIsPlayer = player,
        DefenderMounted = mounted,
        DefenderMaxHealth = 100,
    };

    [Fact]
    public void Kicks_apply_only_with_every_switch_on_the_master_switch_first()
    {
        Assert.True(Rules().Applies(ImpactFamily.Kicks));
        foreach (var off in new Action<TraxSettings>[]
                 {
                     s => s.Set(SettingsSchema.ModEnabled, false, "test"),
                     s => s.Set(SettingsSchema.AthleticsEnabled, false, "test"),
                     s => s.Set(SettingsSchema.ImpactEnabled, false, "test"),
                     s => s.Set(SettingsSchema.ImpactKicksEnabled, false, "test"),
                     s => s.Set(SettingsSchema.ImpactShadowMode, true, "test"),
                     s => s.Set(SettingsSchema.ImpactSpike, true, "test"),
                 })
            Assert.False(Rules(off).Applies(ImpactFamily.Kicks));
        // the master switch wins over everything else on
        Assert.Equal("ModEnabled", Rules(s =>
        {
            s.Set(SettingsSchema.ModEnabled, false, "test");
            s.Set(SettingsSchema.ImpactShadowMode, true, "test");
        }).OffBecause);
        // the blocks' switch does not touch the kicks
        Assert.True(Rules(s => s.Set(SettingsSchema.ImpactBlocksEnabled, false, "test")).Applies(ImpactFamily.Kicks));
    }

    [Fact]
    public void Shadow_keeps_every_state_live_keeps_only_what_applies()
    {
        var shadow = Rules(s => s.Set(SettingsSchema.ImpactShadowMode, true, "test"));
        foreach (var f in new[] { ImpactFamily.Kicks, ImpactFamily.Blocks, ImpactFamily.Hits, ImpactFamily.Riposte })
        {
            Assert.False(shadow.Applies(f));
            Assert.True(shadow.KeepsState(f)); // "would" alike - step 48's reading
        }
        var live = Rules();
        Assert.True(live.KeepsState(ImpactFamily.Kicks));
        Assert.True(live.KeepsState(ImpactFamily.Blocks)); // step 51: blocks live
        Assert.False(live.KeepsState(ImpactFamily.Hits)); // step 52's
        Assert.True(live.KeepsState(ImpactFamily.Riposte)); // step 51: the riposte live
        Assert.False(Rules(s => s.Set(SettingsSchema.ImpactKicksEnabled, false, "test")).KeepsState(ImpactFamily.Kicks));
        Assert.False(Rules(s => s.Set(SettingsSchema.ModEnabled, false, "test")).KeepsState(ImpactFamily.Kicks));
        Assert.StartsWith("LIVE - kicks / bashes, blocks, the riposte applied", live.Describe()); // step 51
    }

    [Fact]
    public void The_reel_is_x1_fresh_and_up_to_x2_5_empty_its_spread_growing_with_tiredness()
    {
        var r = Rules();
        var dice = new SeededRandom(50);
        foreach (double bar in new[] { 1.0, 0.75, 0.5, 0.25, 0.0 })
        {
            double t = 1 - Math.Min(1, bar / 0.75);
            double lo = double.MaxValue, hi = 0;
            for (int i = 0; i < 2000; i++)
            {
                var d = default(ImpactState);
                var a = default(ImpactState);
                var dec = ImpactMath.Decide(in r, Kick(bar), ref d, ref a, 100, dice);
                if (dec.Knockdown.Happens)
                {
                    Assert.True(dec.GetUpFactor >= 1 && dec.GetUpFactor <= 2.0 + 1e-9, "get-up x" + dec.GetUpFactor);
                    Assert.True(double.IsNaN(dec.ReelFactor)); // a man on the ground does not reel
                    continue;
                }
                lo = Math.Min(lo, dec.ReelFactor);
                hi = Math.Max(hi, dec.ReelFactor);
                Assert.True(dec.ReelFactor >= 1, "reel x" + dec.ReelFactor);
                Assert.True(dec.ReelFactor <= (1 + 1.5 * t) * (1 + 0.3 * t) + 1e-9, "reel above its top at T " + t + ": x" + dec.ReelFactor);
                Assert.True(dec.ReelFactor >= (1 + 1.5 * t) * (1 - 0.3 * t) - 1e-9, "reel below its bottom at T " + t + ": x" + dec.ReelFactor);
            }
            if (t == 0) Assert.Equal(1.0, hi, 9); // fresh: exactly vanilla's reel, no dice in it
            if (t == 1) Assert.True(hi - lo > 1.2, "an empty man's reel is not random enough: x" + lo + " - x" + hi); // 1.75 - 3.25
        }
    }

    [Fact]
    public void The_get_up_is_capped_at_GetUpMaxSlowdown_and_the_speed_asked_is_one_over_the_factor()
    {
        var r = Rules();
        var dice = new SeededRandom(7);
        int downs = 0;
        double max = 0;
        for (int i = 0; i < 3000; i++)
        {
            var d = default(ImpactState);
            var a = default(ImpactState);
            var dec = ImpactMath.Decide(in r, Kick(0.0), ref d, ref a, 100, dice);
            if (!dec.Knockdown.Happens) continue;
            downs++;
            max = Math.Max(max, dec.GetUpFactor);
            Assert.InRange(dec.GetUpFactor, 1.0, 2.0);
            Assert.InRange(ImpactLiveMath.SpeedFor(dec.GetUpFactor), 0.5f, 1f);
        }
        Assert.InRange(downs, 2200, 2600); // 80% of 3000
        Assert.True(max > 1.95, "the get-up never reached its cap: x" + max);
        Assert.Equal(1f, ImpactLiveMath.SpeedFor(1.0));
        Assert.Equal(1f, ImpactLiveMath.SpeedFor(double.NaN));
        Assert.Equal(0.4f, ImpactLiveMath.SpeedFor(2.5), 5);
        Assert.Equal(ImpactLiveMath.MinSpeed, ImpactLiveMath.SpeedFor(10));
        Assert.False(ImpactLiveMath.NeedsSlow(1.0005));
        Assert.True(ImpactLiveMath.NeedsSlow(1.01));
    }

    [Fact]
    public void Anti_abuse_runs_from_the_measured_ends_down_immune_and_halved()
    {
        var r = Rules();
        var v = default(ImpactState);
        var a = default(ImpactState);
        // an empty man kicked down at 100 s (the dice: down, the shield kept, the get-up roll)
        var d1 = ImpactMath.Decide(in r, Kick(0.0, shield: false), ref v, ref a, 100, new Dice(0.0, 0.5));
        Assert.True(d1.Knockdown.Happens);
        Assert.True(v.Down(100.5));
        // the watch measures him up at 101.5 s (the real fall-and-rise) - immune to a new knockdown until 106.5
        v.GotUp(101.5, r.KnockdownImmunity);
        Assert.False(v.Down(101.6));
        var d2 = ImpactMath.Decide(in r, Kick(0.0, shield: false), ref v, ref a, 104, new Dice(0.0));
        Assert.Equal(ImpactRefusal.KnockdownImmune, d2.Knockdown.Refusal);
        Assert.False(d2.Knockdown.Happens);
        // the reel of that second kick (no knockdown) - measured end 104.9: reeling until then, no new stagger until 106.9
        Assert.True(d2.ReelFactor > 1);
        v.EndReel(104.9, r.StaggerImmunity);
        Assert.True(v.Reeling(104.8) && !v.Reeling(105.0));
        Assert.True(v.StaggerImmune(106.8) && !v.StaggerImmune(107.0));
        // after the immunity: rolled again, but HALVED by the recent knockdown (within 8 s of 100)
        var d3 = ImpactMath.Decide(in r, Kick(0.0, shield: false), ref v, ref a, 107.0, new Dice(0.999));
        Assert.Equal(ImpactRefusal.None, d3.Knockdown.Refusal);
        Assert.Equal(1, d3.Knockdown.Halvings);
        Assert.Equal(0.40, d3.Knockdown.Chance, 3);
        // a kick while he is on the ground: refused (down)
        var w = default(ImpactState);
        ImpactMath.Decide(in r, Kick(0.0, shield: false), ref w, ref a, 200, new Dice(0.0, 0.5));
        var onGround = ImpactMath.Decide(in r, Kick(0.0), ref w, ref a, 200.3, new Dice(0.0));
        Assert.Equal(ImpactRefusal.Down, onGround.Knockdown.Refusal);
        Assert.Equal(ImpactRefusal.Down, onGround.ShieldDrop.Refusal);
    }

    [Fact]
    public void The_player_takes_the_same_rules_times_ImpactOnPlayerPercent()
    {
        var half = Rules(s => s.Set(SettingsSchema.ImpactOnPlayerPercent, 50, "test"));
        var d = default(ImpactState);
        var a = default(ImpactState);
        var you = ImpactMath.Decide(in half, Kick(0.0, player: true), ref d, ref a, 100, new Dice());
        Assert.Equal(0.40, you.Knockdown.Chance, 3);
        Assert.Equal(0.45, you.ShieldDrop.Chance, 3);
        d = default;
        var him = ImpactMath.Decide(in half, Kick(0.0), ref d, ref a, 100, new Dice());
        Assert.Equal(0.80, him.Knockdown.Chance, 3);
    }

    [Fact]
    public void The_pickup_lock_and_vanillas_drop_rule()
    {
        Assert.False(ImpactLiveMath.PickupLocked(110, 100, 0)); // 0 = vanilla: never locked
        Assert.True(ImpactLiveMath.PickupLocked(110, 100, 15));
        Assert.False(ImpactLiveMath.PickupLocked(115.01, 100, 15));
        Assert.False(ImpactLiveMath.PickupLocked(110, double.NaN, 15)); // never dropped
        foreach (int t in new[] { ImpactActions.ReleaseMelee, ImpactActions.ReleaseRanged, ImpactActions.ReleaseThrowing, ImpactActions.WeaponBash })
            Assert.True(ImpactActions.RefusesDrop(t));
        Assert.False(ImpactActions.RefusesDrop(ImpactActions.StrikeKnockBack));
        Assert.False(ImpactActions.RefusesDrop(ImpactActions.Fall));
    }

    [Fact]
    public void The_summary_reads_decided_applied_seen_the_lengths_the_shields_and_the_levers()
    {
        var stats = new ImpactLiveStats();
        Assert.Equal("impact live (step 50 - kicks and bashes): no live kick or bash this mission (LIVE)", stats.SummaryLines(true, "LIVE").Single());
        var r = Rules();
        var v = default(ImpactState);
        var a = default(ImpactState);
        var d = ImpactMath.Decide(in r, Kick(0.1), ref v, ref a, 100, new Dice(0.0, 0.0, 0.5));
        stats.NoteDecision(in d);
        stats.NoteApplied(ImpactOutcome.Knockdown, d.Bar);
        stats.NoteSeen(ImpactOutcome.Knockdown);
        stats.NoteNotApplied(ImpactOutcome.ShieldDrop, ImpactNotApplied.NoShieldAtTick);
        stats.NoteReel(2.0, 0.84, slowed: true);
        stats.NoteReel(1.0, 0.42, slowed: false);
        stats.NoteGetUp(1.8, 2.7, slowed: true);
        stats.Dropped = 1;
        stats.PickedUp = 1;
        stats.PickupSeconds.Add(21.5);
        stats.Disable(ImpactLever.SlowReaction, "impact.live-slow");
        Assert.False(stats.Disable(ImpactLever.SlowReaction, "again"));
        var lines = stats.SummaryLines(true, "LIVE");
        Assert.Equal(4, lines.Count);
        Assert.StartsWith("impact live (step 50 - kicks and bashes; LIVE): 1 live kicks / bashes by the kicked man's bar 100-75% / 75-50% / 50-25% / 25-0% - knocked down decided 0 / 0 / 0 / 1 = 1, applied 0 / 0 / 0 / 1 = 1, Fall seen 1; shield dropped decided 0 / 0 / 0 / 1 = 1, applied 0 / 0 / 0 / 0 = 0, left the hand 0 (not applied: the shield no longer in hand at the tick 1)", lines[0]);
        Assert.StartsWith("impact live lengths: the reel asked x2.00 (max x2.00, n 1) → measured 0.84 s (max 0.84 s) (n 1) vs untouched reels 0.42 s (n 1) = x2.00 measured; the get-up asked x1.80", lines[1]);
        Assert.Contains("picked up again 1 after 21.50 s (n 1)", lines[2]);
        Assert.Contains("levers disabled the slowed reel / get-up (at impact.live-slow)", lines[3]);
    }
}

using System.Collections.Generic;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 104e - the independent review of step 104 (AI_NOTES "104e - review"): the fixes it made, pinned.
/// </summary>
public class Step104eTests
{
    private sealed class Script : IRandomSource
    {
        private readonly Queue<double> _q;
        public Script(params double[] v) => _q = new Queue<double>(v);
        public double NextDouble() => _q.Count > 0 ? _q.Dequeue() : 0.5;
    }

    private static ImpactFacts CleanHit(bool mounted) => new ImpactFacts
    {
        Event = ImpactEvent.CleanHit,
        AttackerPoints = 100, AttackerF = 1, AttackerAnimation = 1, AttackerSkill = 100,
        DefenderPoints = 100, DefenderBar = 1, DefenderF = 1, DefenderSkill = 100,
        WeaponWeight = 1.2, SwingSpeed = 92, DamageType = ImpactDamageType.Cut, GuardWeight = 1.2, DefenderMaxHealth = 100, Damage = 30,
        DefenderMounted = mounted,
    };

    /// <summary>A reel IN THE SADDLE stays a rider's in the moment between the engine putting him on foot (his horse killed, thrown) and the
    /// tick ending it "dismounted": a hit then is refused step 105's 80 % ("a rider") and gets no reeling bonus - no double-up with a fall.</summary>
    [Fact]
    public void A_saddle_reel_on_a_man_just_put_on_foot_never_gets_the_80_percent_or_the_bonus()
    {
        var ir = ImpactRules.From(new TraxSettings());
        Assert.True(ir.ReelingHit.On);
        var saddle = default(ImpactState);
        saddle.Reel(101, 2, ReelCause.RiderReel);
        var calm = default(ImpactState);
        var a1 = default(ImpactState);
        var a2 = default(ImpactState);
        var f = CleanHit(mounted: false); // the engine already has him on foot
        var d1 = ImpactMath.Decide(in ir, in f, ref saddle, ref a1, 100, new Script(0.01, 0.01, 0.01, 0.01));
        var d2 = ImpactMath.Decide(in ir, in f, ref calm, ref a2, 100, new Script(0.01, 0.01, 0.01, 0.01));
        Assert.True(d1.ReelingHit.Asked);
        Assert.Equal(ReelingHitRefusal.Mounted, d1.ReelingHit.Refusal);
        Assert.False(d1.ReelingHit.Happens);
        Assert.False(d1.Reeling);
        Assert.Equal(d2.X, d1.X, 12);
        Assert.Equal(d2.Knockdown.Chance, d1.Knockdown.Chance, 12);

        // a man on foot reeling from any other cause keeps step 105 exactly
        var foot = default(ImpactState);
        foot.Reel(101, 2, ReelCause.TimedRiposte);
        var a3 = default(ImpactState);
        var d3 = ImpactMath.Decide(in ir, in f, ref foot, ref a3, 100, new Script(0.1, 0.1, 0.1, 0.1));
        Assert.True(d3.Reeling);
        Assert.Equal(ReelingHitRefusal.None, d3.ReelingHit.Refusal);
    }

    [Fact]
    public void The_pure_roll_refuses_a_saddle_reel_whatever_the_mounted_flag()
    {
        var r = ImpactRules.From(new TraxSettings()).ReelingHit;
        var kd = ImpactRoll.None;
        var o = ReelingHitMath.Decide(in r, ImpactEvent.CleanHit, false, true, false, true, ReelCause.RiderReel, false, false, false, 1, ref kd, new Script(0.0));
        Assert.Equal(ReelingHitRefusal.Mounted, o.Refusal);
        Assert.False(o.Happens);
        var kd2 = ImpactRoll.None;
        var o2 = ReelingHitMath.Decide(in r, ImpactEvent.CleanHit, false, true, false, true, ReelCause.HitStagger, false, false, false, 1, ref kd2, new Script(0.0));
        Assert.Equal(ReelingHitRefusal.None, o2.Refusal);
        Assert.True(o2.Happens);
    }
}

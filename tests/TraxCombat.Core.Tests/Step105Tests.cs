using System;
using System.Linq;
using TraxCombat.Core;
using Xunit.Abstractions;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 105 - A HIT ON A REELING MAN KNOCKS HIM DOWN (Toni 2026-11-09: "when hitting (swing/kick/bash) an opponent that is staggering has 80%
/// chance to knock him down no matter what or who caused the stagger"; AI_NOTES "Step 105", DESIGN decision 94): the pure roll, its refusals,
/// its place inside ImpactMath.Decide (the usual roll replaced, the brace / immunity / halving ignored, the stagger replaced, nothing changes
/// for a man who does not reel), the reel's cause, the settings, the [summary] line.
/// </summary>
public class Step105Tests
{
    private readonly ITestOutputHelper _out;

    public Step105Tests(ITestOutputHelper output) => _out = output;

    private sealed class Dice : IRandomSource
    {
        private readonly double _v;
        public Dice(double v) => _v = v;
        public double NextDouble() => _v;
    }

    private static TraxSettings Settings(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        s.Set(SettingsSchema.ImpactOnPlayerPercent, 100, "test");
        s.Set(SettingsSchema.ReelingHitKnockdownEnabled, 1, "test");
        s.Set(SettingsSchema.ReelingHitKnockdownPercent, 80, "test");
        tune?.Invoke(s);
        return s;
    }

    private static ImpactRules Rules(Action<TraxSettings>? tune = null) => ImpactRules.From(Settings(tune));

    private static ImpactFacts Landed(ImpactEvent e, double bar = 1, bool player = false, bool mounted = false, bool thrust = false) => new ImpactFacts
    {
        Event = e,
        AttackerPoints = 100,
        AttackerF = 1,
        AttackerAnimation = 1,
        AttackerSkill = 100,
        DefenderPoints = 100 * bar,
        DefenderBar = bar,
        DefenderF = Math.Min(1, bar / 0.75),
        DefenderSkill = 100,
        WeaponWeight = e == ImpactEvent.Bash ? 4.0 : 1.2,
        SwingSpeed = 92,
        DamageType = ImpactDamageType.Cut,
        DefenderHasShield = true,
        DefenderMaxHealth = 100,
        Damage = 20,
        DefenderIsPlayer = player,
        DefenderMounted = mounted,
        Thrust = thrust,
    };

    /// <summary>A man reeling from <paramref name="cause"/> until t = 101, knockdown-immune and with recent outcomes (the halving) - what step 105 ignores.</summary>
    private static ImpactState Reeling(ReelCause cause = ReelCause.TimedRiposte)
    {
        var s = default(ImpactState);
        s.Reel(101, 2, cause);
        s.KnockdownImmuneUntil = 110; // as if he got up a moment ago
        s.DownUntil = 0;
        s.NoteOutcome(99.5);
        s.NoteOutcome(99.8);
        return s;
    }

    private static ImpactDecision Decide(in ImpactFacts f, in ImpactRules r, ImpactState defender, double die)
    {
        var a = default(ImpactState);
        return ImpactMath.Decide(in r, in f, ref defender, ref a, 100, new Dice(die));
    }

    // ------------------------------------------------------------------ A. the pure roll

    [Fact]
    public void Not_reeling_or_not_landed_is_not_asked_and_the_usual_roll_is_untouched()
    {
        var r = ReelingHitRules.From(Settings());
        var usual = new ImpactRoll { InRow = true, Chance = 0.1, Die = 0.5 };
        var k = usual;
        var o = ReelingHitMath.Decide(in r, ImpactEvent.CleanHit, false, true, false, false, ReelCause.Kick, false, false, false, 1, ref k, new Dice(0.1));
        Assert.False(o.Asked);
        Assert.Equal(usual.Chance, k.Chance);
        o = ReelingHitMath.Decide(in r, ImpactEvent.RightBlock, false, true, false, true, ReelCause.Kick, false, false, false, 1, ref k, new Dice(0.1));
        Assert.False(o.Asked);
        o = ReelingHitMath.Decide(in r, ImpactEvent.Bash, false, false, false, true, ReelCause.Kick, false, false, false, 1, ref k, new Dice(0.1)); // the shield stopped it
        Assert.False(o.Asked);
        Assert.False(k.Happens);
    }

    [Theory]
    [InlineData(0.79, true)]
    [InlineData(0.80, false)]
    [InlineData(0.0, true)]
    [InlineData(0.99, false)]
    public void A_landed_hit_on_a_reeling_man_rolls_80_percent(double die, bool down)
    {
        var r = ReelingHitRules.From(Settings());
        var k = ImpactRoll.None;
        var o = ReelingHitMath.Decide(in r, ImpactEvent.CleanHit, false, true, false, true, ReelCause.TimedRiposte, false, false, false, 1, ref k, new Dice(die));
        Assert.True(o.Asked);
        Assert.Equal(ReelingHitRefusal.None, o.Refusal);
        Assert.Equal(0.8, o.Chance, 12);
        Assert.Equal(down, o.Happens);
        Assert.Equal(down, k.Happens);
        Assert.Equal(0.8, k.Chance, 12);
        Assert.Equal(ImpactRefusal.None, k.Refusal);
        Assert.Equal(ReelCause.TimedRiposte, o.Cause);
    }

    [Fact]
    public void The_usual_roll_stands_when_it_came_up_or_was_higher_and_is_replaced_when_refused()
    {
        var r = ReelingHitRules.From(Settings());
        var up = new ImpactRoll { InRow = true, Chance = 0.3, Die = 0.1, Happens = true };
        var o = ReelingHitMath.Decide(in r, ImpactEvent.Kick, false, true, false, true, ReelCause.Kick, false, false, false, 1, ref up, new Dice(0.99));
        Assert.True(o.UsualStood && o.Happens && up.Happens);
        var higher = new ImpactRoll { InRow = true, Chance = 0.9, Die = 0.85, Happens = true };
        o = ReelingHitMath.Decide(in r, ImpactEvent.Kick, false, true, false, true, ReelCause.Kick, false, false, false, 1, ref higher, new Dice(0.99));
        Assert.True(o.UsualStood);
        Assert.Equal(0.9, o.Chance, 12);
        // refused by the immunity / the brace: ignored - the reeling roll decides ("no matter what")
        var immune = new ImpactRoll { InRow = true, Chance = 0.95, Die = double.NaN, Refusal = ImpactRefusal.KnockdownImmune };
        o = ReelingHitMath.Decide(in r, ImpactEvent.Kick, false, true, false, true, ReelCause.Kick, false, false, false, 1, ref immune, new Dice(0.5));
        Assert.False(o.UsualStood);
        Assert.True(o.Happens && immune.Happens);
        Assert.Equal(ImpactRefusal.None, immune.Refusal);
        Assert.Equal(0.8, immune.Chance, 12);
    }

    [Fact]
    public void The_hard_refusals_and_you()
    {
        var r = ReelingHitRules.From(Settings());
        ReelingHitRefusal Why(bool mounted, bool down, bool player, double onPlayer, bool guarded = false, ReelingHitRules? rules = null)
        {
            var rr = rules ?? r;
            var k = ImpactRoll.None;
            var o = ReelingHitMath.Decide(in rr, ImpactEvent.CleanHit, false, true, guarded, true, ReelCause.Kick, mounted, down, player, onPlayer, ref k, new Dice(0));
            Assert.Equal(o.Refusal == ReelingHitRefusal.None, k.Happens);
            return o.Refusal;
        }
        Assert.Equal(ReelingHitRefusal.Mounted, Why(true, false, false, 1));
        Assert.Equal(ReelingHitRefusal.Down, Why(false, true, false, 1));
        Assert.Equal(ReelingHitRefusal.YouExempt, Why(false, false, true, 0));
        Assert.Equal(ReelingHitRefusal.Guarded, Why(false, false, false, 1, guarded: true));
        Assert.Equal(ReelingHitRefusal.Off, Why(false, false, false, 1, rules: ReelingHitRules.From(Settings(s => s.Set(SettingsSchema.ReelingHitKnockdownEnabled, 0, "test")))));
        Assert.Equal(ReelingHitRefusal.Off, Why(false, false, false, 1, rules: ReelingHitRules.From(Settings(s => s.Set(SettingsSchema.ReelingHitKnockdownPercent, 0, "test")))));
        Assert.Equal(ReelingHitRefusal.None, Why(false, false, true, 1));
        // you at ImpactOnPlayerPercent 50: 40 %
        Assert.Equal(0.4, ReelingHitMath.ChanceOn(in r, true, 0.5), 12);
        Assert.Equal(0.8, ReelingHitMath.ChanceOn(in r, false, 0.5), 12);
    }

    [Fact]
    public void Kinds_and_causes()
    {
        Assert.Equal(ReelingHitKind.Swing, ReelingHitMath.KindOf(ImpactEvent.CleanHit, false));
        Assert.Equal(ReelingHitKind.Thrust, ReelingHitMath.KindOf(ImpactEvent.CleanHit, true));
        Assert.Equal(ReelingHitKind.Kick, ReelingHitMath.KindOf(ImpactEvent.Kick, false));
        Assert.Equal(ReelingHitKind.Bash, ReelingHitMath.KindOf(ImpactEvent.Bash, true));
        Assert.Equal(ReelCause.HitStagger, ReelingHitMath.CauseOfDefenderReel(ImpactEvent.CleanHit));
        Assert.Equal(ReelCause.BlockStagger, ReelingHitMath.CauseOfDefenderReel(ImpactEvent.WrongBlock));
        Assert.Equal(ReelCause.Kick, ReelingHitMath.CauseOfDefenderReel(ImpactEvent.Kick));
        Assert.Equal(ReelCause.Bash, ReelingHitMath.CauseOfDefenderReel(ImpactEvent.Bash));
    }

    // ------------------------------------------------------------------ B. inside ImpactMath.Decide

    [Theory]
    [InlineData(ImpactEvent.CleanHit, false)]
    [InlineData(ImpactEvent.CleanHit, true)]
    [InlineData(ImpactEvent.Kick, false)]
    [InlineData(ImpactEvent.Bash, false)]
    public void A_fresh_even_reeling_man_falls_whatever_his_immunity_and_halving(ImpactEvent e, bool thrust)
    {
        var r = Rules();
        var f = Landed(e, thrust: thrust);
        var d = Decide(in f, in r, Reeling(), 0.5);
        Assert.True(d.ReelingHit.Asked);
        Assert.True(d.ReelingHit.Happens);
        Assert.True(d.Knockdown.Happens);
        Assert.Equal(ReelingHitMath.KindOf(e, thrust), d.ReelingHit.Kind);
        Assert.Equal(ReelCause.TimedRiposte, d.ReelingHit.Cause);
        Assert.False(d.Stagger.Happens); // the knockdown replaces the stagger
        Assert.False(d.Braced); // a fallen man did not brace
        Assert.False(double.IsNaN(d.GetUpFactor)); // the usual get-up length
        // a die above 80 %: he stays up (the usual chance for a fresh even man is far below)
        var up = Decide(in f, in r, Reeling(), 0.95);
        Assert.True(up.ReelingHit.Asked);
        Assert.False(up.Knockdown.Happens);
        _out.WriteLine(e + (thrust ? " thrust" : string.Empty) + ": chance " + d.ReelingHit.Chance + ", usual stood " + up.ReelingHit.UsualStood);
    }

    [Fact]
    public void The_state_records_the_fall()
    {
        var r = Rules();
        var f = Landed(ImpactEvent.CleanHit);
        var st = Reeling();
        var a = default(ImpactState);
        var d = ImpactMath.Decide(in r, in f, ref st, ref a, 100, new Dice(0.5));
        Assert.True(d.Knockdown.Happens);
        Assert.True(st.Down(100.1));
        Assert.False(st.Reeling(100.1)); // a man on the ground no longer reels
        // a second hit on the man now down: refused (Down), never asked as reeling
        var d2 = ImpactMath.Decide(in r, in f, ref st, ref a, 100.2, new Dice(0.0));
        Assert.False(d2.ReelingHit.Asked);
    }

    [Fact]
    public void Not_reeling_decides_exactly_as_before_and_the_switch_off_too()
    {
        var on = Rules();
        var off = Rules(s => s.Set(SettingsSchema.ReelingHitKnockdownEnabled, 0, "test"));
        foreach (var e in new[] { ImpactEvent.CleanHit, ImpactEvent.Kick, ImpactEvent.Bash, ImpactEvent.RightBlock, ImpactEvent.WrongBlock, ImpactEvent.Parry })
        foreach (var bar in new[] { 1.0, 0.4, 0.05 })
        foreach (var die in new[] { 0.01, 0.3, 0.7 })
        {
            var f = Landed(e, bar);
            var a = Decide(in f, in on, default, die);
            var b = Decide(in f, in off, default, die);
            Assert.False(a.ReelingHit.Asked);
            Assert.Equal(b.Knockdown.Happens, a.Knockdown.Happens);
            Assert.Equal(b.Knockdown.Chance, a.Knockdown.Chance, 12);
            Assert.Equal(b.Stagger.Happens, a.Stagger.Happens);
            // the switch off on a reeling man: the usual decision exactly, the refusal counted
            if (!ImpactMath.IsLanded(e)) continue;
            var ro = Decide(in f, in off, Reeling(), die);
            Assert.True(ro.ReelingHit.Asked);
            Assert.Equal(ReelingHitRefusal.Off, ro.ReelingHit.Refusal);
        }
    }

    [Fact]
    public void A_rider_is_never_given_it()
    {
        var f = Landed(ImpactEvent.CleanHit, mounted: true);
        var d = Decide(in f, Rules(), Reeling(), 0.0);
        Assert.Equal(ReelingHitRefusal.Mounted, d.ReelingHit.Refusal);
    }

    [Fact]
    public void You_follow_ImpactOnPlayerPercent()
    {
        var f = Landed(ImpactEvent.CleanHit, player: true);
        var half = Rules(s => s.Set(SettingsSchema.ImpactOnPlayerPercent, 50, "test"));
        var d = Decide(in f, in half, Reeling(), 0.39);
        Assert.Equal(0.4, d.ReelingHit.Chance, 6);
        Assert.True(d.Knockdown.Happens);
        Assert.False(Decide(in f, in half, Reeling(), 0.41).Knockdown.Happens);
        var zero = Rules(s => s.Set(SettingsSchema.ImpactOnPlayerPercent, 0, "test"));
        Assert.Equal(ReelingHitRefusal.YouExempt, Decide(in f, in zero, Reeling(), 0.0).ReelingHit.Refusal);
    }

    [Fact]
    public void Eighty_percent_over_many_hits()
    {
        var r = Rules();
        var dice = new SeededRandom(105);
        int down = 0, n = 20000;
        var f = Landed(ImpactEvent.CleanHit);
        for (int i = 0; i < n; i++)
        {
            var st = Reeling();
            var a = default(ImpactState);
            if (ImpactMath.Decide(in r, in f, ref st, ref a, 100, dice).Knockdown.Happens) down++;
        }
        double share = (double)down / n;
        _out.WriteLine("knocked down " + down + " of " + n + " = " + share);
        Assert.InRange(share, 0.78, 0.83);
    }

    // ------------------------------------------------------------------ C. the reel's cause

    [Fact]
    public void Decide_writes_what_made_him_reel()
    {
        var r = Rules(s =>
        {
            s.Set(SettingsSchema.RiposteStaggerMaxPercent, 100, "test");
            s.Set(SettingsSchema.TimedBlocksEnabled, 1, "test");
        });
        // a riposte: his blow on a perfectly timed right block
        var f = Landed(ImpactEvent.RightBlock);
        f.TimingSource = TimingSource.PlayerClock;
        f.GuardAge = 0.05;
        f.DefenderIsPlayer = true;
        var def = default(ImpactState);
        var att = default(ImpactState);
        var d = ImpactMath.Decide(in r, in f, ref def, ref att, 100, new Dice(0.0));
        Assert.True(d.Riposte.Happens);
        Assert.Equal(ReelCause.TimedRiposte, att.ReelCause);
        Assert.True(att.Reeling(100.1));
        // the measured re-timing keeps the cause
        att.EndReel(100.8, 2);
        Assert.Equal(ReelCause.TimedRiposte, att.ReelCause);
        // a kick's lengthened reel
        var k = Landed(ImpactEvent.Kick, 0.05);
        var kd = default(ImpactState);
        var ka = default(ImpactState);
        var d2 = ImpactMath.Decide(in r, in k, ref kd, ref ka, 100, new Dice(0.99));
        if (kd.Reeling(100.01)) Assert.Equal(ReelCause.Kick, kd.ReelCause);
        _out.WriteLine("kick: reel x" + d2.ReelFactor + ", cause " + kd.ReelCause);
    }

    // ------------------------------------------------------------------ D. the settings and the summary

    [Fact]
    public void The_settings_are_where_the_design_says()
    {
        var all = SettingsSchema.All.ToList();
        Assert.Equal(all.IndexOf(SettingsSchema.PerfectStrikeEnabled) + 1, all.IndexOf(SettingsSchema.ReelingHitKnockdownEnabled));
        Assert.Contains(SettingsSchema.ReelingHitKnockdownEnabled, SettingsSchema.PlayerPage);
        Assert.DoesNotMatch(@"[Ss]tep \d", SettingsSchema.ReelingHitKnockdownEnabled.Description);
        Assert.Equal(all.IndexOf(SettingsSchema.TimedBlockShowTiming) + 1, all.IndexOf(SettingsSchema.ReelingHitKnockdownPercent));
        Assert.Equal(SettingsPage.Advanced, SettingsSchema.ReelingHitKnockdownPercent.Group.Page);
        Assert.Same(SettingsSchema.ImpactGroup, SettingsSchema.ReelingHitKnockdownPercent.Group);
        Assert.Equal(1, SettingsSchema.ReelingHitKnockdownEnabled.Default, 5);
        Assert.Equal(80, SettingsSchema.ReelingHitKnockdownPercent.Default, 5);
        var r = ReelingHitRules.From(new TraxSettings());
        Assert.True(r.On);
        Assert.Equal(0.8, r.Chance, 12);
    }

    [Fact]
    public void The_summary_line_counts_hits_downs_kinds_causes_and_refusals()
    {
        var st = new ReelingHitStats();
        var down = new ReelingHitResult { Asked = true, Kind = ReelingHitKind.Swing, Cause = ReelCause.TimedRiposte, Chance = 0.8, Die = 0.3, Happens = true };
        var up = new ReelingHitResult { Asked = true, Kind = ReelingHitKind.Kick, Cause = ReelCause.Kick, Chance = 0.8, Die = 0.9 };
        var refused = new ReelingHitResult { Asked = true, Kind = ReelingHitKind.Bash, Cause = ReelCause.HitStagger, Refusal = ReelingHitRefusal.Down, Chance = double.NaN, Die = double.NaN };
        st.Note(in down, true, false, true);
        st.Note(in down, false, false, false); // came up, not applied
        st.Note(in up, false, true, false);
        st.Note(in refused, false, false, false);
        st.NoteFatal(ReelingHitKind.Thrust, ReelCause.Riposte);
        Assert.Equal(5, st.HitsTotal);
        Assert.Equal(1, st.DownsTotal);
        Assert.Equal(1, st.Refused[(int)ReelingHitRefusal.NotApplied]);
        Assert.Equal(1, st.Refused[(int)ReelingHitRefusal.Fatal]);
        string line = st.SummaryLine(ImpactRules.From(Settings()));
        _out.WriteLine(line);
        Assert.StartsWith("hits on reeling men (step 105; ON, 80 %): landed 5, KNOCKED DOWN 1 (rolled 3 → 2 came up = 67 %", line);
        Assert.Contains("by hit: swing 1/2, thrust 0/1, kick 0/1, bash 0/1", line);
        Assert.Contains("a timed riposte 1/2", line);
        Assert.Contains("refused: already down 1, killed by it 1, not applied 1", line);
        Assert.Contains("on you 0/1, by you 1/1", line);
    }
}

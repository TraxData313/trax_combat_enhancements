using System;
using System.Linq;
using System.Threading.Tasks;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>Step 47 (IMPACT I1, the spike): the switch with the master switch first, the kick rotation and the stun A/B split, the timing,
/// the watch state machine over channel reads, the verdicts (count and length), what each verdict means for steps 48-53, the thread-safe
/// stun tally and the [summary] "impact spike" lines.</summary>
public class ImpactSpikeTests
{
    private static ActionRead Read(int type0 = 35, int index0 = 100, int type1 = 35, int index1 = 200) => new ActionRead(type0, index0, type1, index1);

    // ------------------------------------------------------------------ the switch

    [Fact]
    public void The_master_switch_decides_first_then_ImpactSpike()
    {
        Assert.True(new SpikeRules(true, true).Enabled);
        Assert.False(new SpikeRules(false, true).Enabled);
        Assert.False(new SpikeRules(true, false).Enabled);
        Assert.Equal("ModEnabled", new SpikeRules(false, false).OffBecause);
        Assert.Equal("ModEnabled", new SpikeRules(false, true).OffBecause);
        Assert.Equal("ImpactSpike", new SpikeRules(true, false).OffBecause);
        Assert.Null(new SpikeRules(true, true).OffBecause);
    }

    [Fact]
    public void The_setting_is_an_advanced_switch_off_by_default_and_read_live()
    {
        var s = new TraxSettings();
        Assert.False(s.ImpactSpike);
        Assert.False(SpikeRules.From(s).Enabled);
        s.Set(SettingsSchema.ImpactSpike, true, "test");
        Assert.True(SpikeRules.From(s).Enabled);
        s.Set(SettingsSchema.ModEnabled, false, "test");
        Assert.False(SpikeRules.From(s).Enabled);
        Assert.Same(SettingsSchema.AdvancedGroup, SettingsSchema.ImpactSpike.Group);
        Assert.Equal(ParamType.Bool, SettingsSchema.ImpactSpike.Type);
        Assert.Equal(ApplyTiming.Live, SettingsSchema.ImpactSpike.Timing);
        int i = SettingsSchema.All.ToList().IndexOf(SettingsSchema.ImpactSpike);
        Assert.Same(SettingsSchema.VerboseLogging, SettingsSchema.All[i + 1]);
    }

    [Fact]
    public void The_sentence_names_every_lever_and_the_off_reason()
    {
        string on = new SpikeRules(true, true).Describe();
        Assert.StartsWith("SPIKE ON (ImpactSpike - a test tool, not gameplay)", on);
        foreach (var part in new[] { "KnockDown", "x3", "0.8 s raised to 2.0 s", "speed 0.5", "every 10 s", "act_stagger_backward", "never a rider", "drops it", "KnockDown, 1 damage", "every 20 s", "each lever's verdict" })
            Assert.Contains(part, on);
        Assert.StartsWith("OFF (ModEnabled) - nothing of the impact system runs", new SpikeRules(false, true).Describe());
        Assert.StartsWith("OFF (ImpactSpike)", new SpikeRules(true, false).Describe());
    }

    // ------------------------------------------------------------------ the rotation, the split, the timing

    [Fact]
    public void Kicks_rotate_through_four_kinds_so_each_lever_has_its_own_control()
    {
        var plans = Enumerable.Range(1, 8).Select(n => SpikeMath.Kick(n, true, true)).ToArray();
        Assert.Equal(new[] { true, false, true, false, true, false, true, false }, plans.Select(p => p.KnockDown));
        Assert.Equal(new[] { false, false, true, true, false, false, true, true }, plans.Select(p => p.Slow));
        Assert.Equal(Enumerable.Range(1, 8), plans.Select(p => p.Number));
        // a disabled lever = its part off, the other's rotation unchanged
        Assert.All(Enumerable.Range(1, 8), n => Assert.False(SpikeMath.Kick(n, false, true).KnockDown));
        Assert.Equal(SpikeMath.Kick(3, true, true).Slow, SpikeMath.Kick(3, false, true).Slow);
        Assert.All(Enumerable.Range(1, 8), n => Assert.False(SpikeMath.Kick(n, true, false).Slow));
        Assert.True(SpikeMath.Kick(0, true, true).Slow); // never negative indices - 0 is the 4th of the previous round
    }

    [Fact]
    public void The_stun_split_is_by_the_blockers_index_and_x3_stops_at_the_cap()
    {
        Assert.True(SpikeMath.StunTreated(0));
        Assert.True(SpikeMath.StunTreated(812));
        Assert.False(SpikeMath.StunTreated(813));
        Assert.Equal(0.63f, SpikeMath.StunAsked(0.21f, 2f), 4);
        Assert.Equal(2f, SpikeMath.StunAsked(0.8f, 2f));
        Assert.Equal(0.8f, SpikeMath.StunAsked(0.5f, 0.8f));
        Assert.Equal(0f, SpikeMath.StunAsked(0f, 2f));
        Assert.True(float.IsNaN(SpikeMath.StunAsked(float.NaN, 2f)));
    }

    [Fact]
    public void A_periodic_lever_fires_first_at_once_then_every_period()
    {
        Assert.True(SpikeMath.Due(5, double.NaN, 10));
        Assert.False(SpikeMath.Due(14.9, 5, 10));
        Assert.True(SpikeMath.Due(15, 5, 10));
        Assert.True(SpikeMath.Due(3, 5, 10)); // the clock went back (a new mission time) - never stuck
        Assert.False(SpikeMath.IgnorePriority(1));
        Assert.True(SpikeMath.IgnorePriority(2));
        Assert.False(SpikeMath.IgnorePriority(3));
        Assert.Equal(StaggerKind.Backward, SpikeMath.StaggerFor());
    }

    [Fact]
    public void Only_a_kick_that_lands_on_a_man_counts()
    {
        Assert.True(SpikeMath.KickLanded(true, false, ImpactActions.ResultStrikeAgent));
        Assert.False(SpikeMath.KickLanded(false, false, ImpactActions.ResultStrikeAgent)); // a swing
        Assert.False(SpikeMath.KickLanded(true, true, ImpactActions.ResultStrikeAgent));  // into the shield
        Assert.False(SpikeMath.KickLanded(true, false, BlockMath.ResultBlocked));
        Assert.False(SpikeMath.KickLanded(true, false, 2));                               // the world
        var k = SpikeHit.LandedKick(4, bash: true);
        Assert.True(k.Kick && k.Bash && k.Landed && k.Block == BlockKind.None && k.Damage == 4);
        var b = SpikeHit.Blocked(BlockKind.WeaponParry);
        Assert.True(!b.Kick && !b.Landed && b.Block == BlockKind.WeaponParry);
    }

    [Fact]
    public void The_action_codes_and_their_names()
    {
        Assert.True(ImpactActions.IsStrike(48) && ImpactActions.IsStrike(52) && !ImpactActions.IsStrike(47) && !ImpactActions.IsStrike(53));
        Assert.True(ImpactActions.IsFall(23) && !ImpactActions.IsFall(22));
        Assert.True(ImpactActions.IsRecoil(21) && ImpactActions.IsRecoil(22) && !ImpactActions.IsRecoil(20));
        Assert.True(ImpactActions.IsDefend(1) && ImpactActions.IsDefend(14) && !ImpactActions.IsDefend(15) && !ImpactActions.IsDefend(0));
        Assert.Equal("StrikeKnockBack", ImpactActions.Name(51));
        Assert.Equal("Fall", ImpactActions.Name(23));
        Assert.Equal("Defend(2)", ImpactActions.Name(2));
        Assert.Equal("type 35", ImpactActions.Name(35));
    }

    // ------------------------------------------------------------------ the watch

    [Fact]
    public void A_kick_reel_shows_on_either_channel_is_followed_through_a_continue_and_is_timed()
    {
        var w = new ReactionWatch();
        w.Begin(WatchKind.Reaction, 10, SpikeMath.AppearSeconds);
        Assert.Equal(WatchEvent.None, w.Observe(10.02, Read(), out _));
        Assert.Equal(WatchEvent.Appeared, w.Observe(10.03, Read(type0: 51, index0: 700), out _));
        Assert.True(w.Appeared);
        Assert.Equal(0, w.Channel);
        Assert.Equal(51, w.FirstType);
        Assert.Equal(WatchEvent.None, w.Observe(10.3, Read(type0: 51, index0: 700), out _));
        Assert.Equal(WatchEvent.NewAction, w.Observe(10.4, Read(type0: 49, index0: 701), out _)); // a second strike action
        Assert.Equal(WatchEvent.Ended, w.Observe(10.48, Read(type0: 35, index0: 100), out double len));
        Assert.Equal(0.45, len, 6);

        w.Begin(WatchKind.Reaction, 20, SpikeMath.AppearSeconds);
        Assert.Equal(WatchEvent.Appeared, w.Observe(20.02, Read(type1: 48, index1: 300), out _));
        Assert.Equal(1, w.Channel);
    }

    [Fact]
    public void A_reaction_that_never_shows_is_given_up_after_its_window()
    {
        var w = new ReactionWatch();
        w.Begin(WatchKind.Reaction, 10, 0.6);
        Assert.Equal(WatchEvent.None, w.Observe(10.5, Read(), out _));
        Assert.Equal(WatchEvent.NeverAppeared, w.Observe(10.6, Read(), out _));
        w.Begin(WatchKind.Fall, 10, 1.0);
        Assert.Equal(WatchEvent.None, w.Observe(10.2, Read(type0: 51, index0: 5), out _)); // a reel is not a fall
        Assert.Equal(WatchEvent.Appeared, w.Observe(10.3, Read(type0: 23, index0: 900), out _));
        Assert.Equal(WatchEvent.NewAction, w.Observe(11.5, Read(type0: 23, index0: 901), out _)); // the _continue (the rise)
        Assert.Equal(WatchEvent.Ended, w.Observe(12.3, Read(), out double len));
        Assert.Equal(2.0, len, 6);
    }

    [Fact]
    public void A_recoil_a_guard_and_a_stagger_have_their_own_rules()
    {
        var w = new ReactionWatch();
        w.Begin(WatchKind.Recoil, 5, 0.6);
        Assert.Equal(WatchEvent.Appeared, w.Observe(5.02, Read(type1: 22, index1: 40), out _));
        Assert.Equal(WatchEvent.Ended, w.Observe(5.32, Read(type1: 19, index1: 41), out double recoil));
        Assert.Equal(0.30, recoil, 6);

        // the guard: in it at once (else never), timed from the BLOCK, ends when his action changes even to another defend
        w.Begin(WatchKind.Guard, 5, 0);
        Assert.Equal(WatchEvent.Appeared, w.Observe(5.02, Read(type1: 2, index1: 60), out _));
        Assert.Equal(WatchEvent.None, w.Observe(5.4, Read(type1: 2, index1: 60), out _));
        Assert.Equal(WatchEvent.Ended, w.Observe(5.6, Read(type1: 2, index1: 61), out double guard));
        Assert.Equal(0.6, guard, 6);
        w.Begin(WatchKind.Guard, 5, 0);
        Assert.Equal(WatchEvent.NeverAppeared, w.Observe(5.02, Read(type1: 20, index1: 3), out _)); // he was swinging, not guarding

        // the stagger: the very action we started; another strike after it = it ended
        w.Begin(WatchKind.Stagger, 7, 0.6, expectIndex: 1234);
        Assert.Equal(WatchEvent.None, w.Observe(7.02, Read(type0: 48, index0: 999), out _));
        Assert.Equal(WatchEvent.Appeared, w.Observe(7.03, Read(type0: 48, index0: 1234), out _));
        Assert.Equal(WatchEvent.Ended, w.Observe(7.93, Read(type0: 48, index0: 1235), out double stagger));
        Assert.Equal(0.9, stagger, 6);
    }

    [Fact]
    public void A_reaction_still_on_at_the_cap_is_counted_at_the_cap()
    {
        var w = new ReactionWatch();
        w.Begin(WatchKind.Fall, 0, 1);
        w.Observe(0.1, Read(type0: 23, index0: 9), out _);
        Assert.Equal(WatchEvent.None, w.Observe(8.0, Read(type0: 23, index0: 9), out _));
        Assert.Equal(WatchEvent.Capped, w.Observe(8.2, Read(type0: 23, index0: 9), out double len));
        Assert.Equal(8.1, len, 6);
    }

    // ------------------------------------------------------------------ the verdicts

    [Fact]
    public void A_count_verdict_needs_half_seen_or_enough_asked_to_fail()
    {
        Assert.Equal(SpikeVerdict.Unseen, SpikeMath.CountVerdict(0, 0));
        Assert.Equal(SpikeVerdict.Works, SpikeMath.CountVerdict(1, 1));
        Assert.Equal(SpikeVerdict.Works, SpikeMath.CountVerdict(4, 2));
        Assert.Equal(SpikeVerdict.Unseen, SpikeMath.CountVerdict(2, 0));
        Assert.Equal(SpikeVerdict.Fails, SpikeMath.CountVerdict(3, 0));
        Assert.Equal(SpikeVerdict.Fails, SpikeMath.CountVerdict(10, 4));
    }

    private static MeanStd Of(params double[] xs)
    {
        var m = new MeanStd();
        foreach (var x in xs) m.Add(x);
        return m;
    }

    [Fact]
    public void A_length_verdict_compares_treated_with_its_control()
    {
        Assert.Equal(SpikeVerdict.Works, SpikeMath.LengthVerdict(Of(0.4, 0.44), Of(0.8, 0.9)));
        Assert.Equal(SpikeVerdict.Fails, SpikeMath.LengthVerdict(Of(0.4, 0.42, 0.41), Of(0.42, 0.4, 0.43)));
        Assert.Equal(SpikeVerdict.Unseen, SpikeMath.LengthVerdict(Of(0.4, 0.42), Of(0.42, 0.4)));     // too few to fail
        Assert.Equal(SpikeVerdict.Unseen, SpikeMath.LengthVerdict(Of(0.4), Of(0.9, 0.9)));            // one control
        Assert.Equal(SpikeVerdict.Unseen, SpikeMath.LengthVerdict(Of(0.4, 0.4, 0.4), Of(0.5, 0.5, 0.5))); // 1.25: unclear
        Assert.Equal(SpikeVerdict.Fails, SpikeMath.Combine(SpikeVerdict.Works, SpikeVerdict.Fails));
        Assert.Equal(SpikeVerdict.Works, SpikeMath.Combine(SpikeVerdict.Unseen, SpikeVerdict.Works));
        Assert.Equal(SpikeVerdict.Unseen, SpikeMath.Combine(SpikeVerdict.Unseen, SpikeVerdict.Unseen));
    }

    [Fact]
    public void Every_lever_and_verdict_says_what_it_means_for_steps_48_to_53()
    {
        foreach (SpikeLever l in Enum.GetValues(typeof(SpikeLever)))
        {
            Assert.False(string.IsNullOrEmpty(SpikeMath.Name(l)));
            Assert.False(string.IsNullOrEmpty(SpikeMath.Short(l)));
            var texts = new[] { SpikeVerdict.Works, SpikeVerdict.Fails, SpikeVerdict.Unseen }.Select(v => SpikeMath.NextStep(l, v)).ToArray();
            Assert.All(texts, t => Assert.False(string.IsNullOrEmpty(t)));
            Assert.Equal(3, texts.Distinct().Count());
        }
        Assert.Contains("SetOverridenStrikeAndDeathAction", SpikeMath.NextStep(SpikeLever.SlowReaction, SpikeVerdict.Fails));
        Assert.Contains("crush-through", SpikeMath.NextStep(SpikeLever.OwnBlow, SpikeVerdict.Fails));
        Assert.Contains("forced stagger (lever 4)", SpikeMath.NextStep(SpikeLever.BlockStun, SpikeVerdict.Fails));
        Assert.Contains("own blow after the kick (lever 6)", SpikeMath.NextStep(SpikeLever.KickKnockDown, SpikeVerdict.Fails));
        Assert.Equal(1, SpikeMath.Number(SpikeLever.KickKnockDown));
        Assert.Equal(6, SpikeMath.Number(SpikeLever.OwnBlow));
        Assert.Equal("a rider", SpikeMath.RefusalName(ForceRefusal.Mounted));
    }

    // ------------------------------------------------------------------ the stats and the summary

    [Fact]
    public void The_stun_tally_adds_up_from_many_threads()
    {
        var t = new BlockStunTally();
        Parallel.For(0, 4000, i =>
        {
            t.Call(i % 2 == 0);
            if (i % 4 == 0) t.AddControl(0.2f, 0.1f);
            else t.AddTreated(0.2f, 0.6f, 0.1f, 0.3f, i % 8 == 1);
        });
        Assert.Equal(4000, t.Calls);
        Assert.Equal(2000, t.OffMainThread);
        Assert.Equal(1000, t.Control);
        Assert.Equal(3000, t.Treated);
        Assert.Equal(500, t.Capped);
        Assert.Equal(0.2, t.DefenderBefore, 6);
        Assert.Equal(0.6, t.DefenderAfter, 6);
        Assert.Equal(0.3, t.AttackerAfter, 6);
        Assert.Equal(0.2, t.ControlDefender, 6);
        t.Clear();
        Assert.Equal(0, t.Calls);
        Assert.True(double.IsNaN(t.DefenderAfter));
    }

    [Fact]
    public void Each_lever_gets_its_verdict_from_its_own_numbers()
    {
        var s = new ImpactSpikeStats();
        Assert.All(Enum.GetValues(typeof(SpikeLever)).Cast<SpikeLever>(), l => Assert.Equal(SpikeVerdict.Unseen, s.Verdict(l)));
        s.FallSeen = 3; s.FallNotSeen = 1;
        Assert.Equal(SpikeVerdict.Works, s.Verdict(SpikeLever.KickKnockDown));
        s.RecoilControl = Of(0.2, 0.2); s.RecoilTreated = Of(0.6, 0.55);
        Assert.Equal(SpikeVerdict.Works, s.Verdict(SpikeLever.BlockStun));
        s.GuardControl = Of(0.3, 0.3, 0.3); s.GuardTreated = Of(0.3, 0.31, 0.3);
        Assert.Equal(SpikeVerdict.Fails, s.Verdict(SpikeLever.BlockStun)); // one part fails: the lever cannot be trusted
        s.StaggerAsked = 4; s.StaggerAcceptedBy[0] = 0; s.StaggerAcceptedBy[1] = 0;
        Assert.Equal(SpikeVerdict.Fails, s.Verdict(SpikeLever.ForcedStagger));      // never accepted
        s.StaggerAcceptedBy[1] = 2; s.StaggerSeen = 2;
        Assert.Equal(SpikeVerdict.Works, s.Verdict(SpikeLever.ForcedStagger));
        s.Dropped = 0; s.DropNotSeen = 3;
        Assert.Equal(SpikeVerdict.Fails, s.Verdict(SpikeLever.ShieldDrop));
        s.OwnFallSeen = 1;
        Assert.Equal(SpikeVerdict.Works, s.Verdict(SpikeLever.OwnBlow));
        // a lever disabled by an error reads FAILS whatever it measured
        Assert.True(s.Disable(SpikeLever.KickKnockDown, "impact.knockdown"));
        Assert.False(s.Disable(SpikeLever.KickKnockDown, "again"));
        Assert.Equal(SpikeVerdict.Fails, s.Verdict(SpikeLever.KickKnockDown));
        Assert.Equal("impact.knockdown", s.DisabledAt(SpikeLever.KickKnockDown));
        Assert.Equal(1, s.DisabledCount);
    }

    [Fact]
    public void The_summary_has_a_header_and_one_line_per_lever_with_its_verdict()
    {
        var s = new ImpactSpikeStats { EverOn = true, OnSeconds = 90, OffSeconds = 10, Switches = 2, EngineCalls = 12, MostCallsInTick = 2 };
        s.KicksLanded = 8; s.Bashes = 2; s.KnockDownAsked = 4; s.KnockDownPlanned = 4; s.FallSeen = 4; s.ControlKicks = 4;
        s.NoteControlType(ImpactActions.StrikeKnockBack); s.NoteControlType(ImpactActions.StrikeKnockBack);
        s.ReelControl = Of(0.42, 0.40); s.ReelSlowed = Of(0.85, 0.83);
        s.Disable(SpikeLever.ShieldDrop, "impact.shield-drop");
        var lines = s.SummaryLines(new SpikeRules(true, true));
        Assert.Equal(7, lines.Count);
        Assert.StartsWith("impact spike (step 47, ImpactSpike - a test tool): on 90% of the battle (switched 2 time(s)); levers disabled by an error: 5 (impact.shield-drop)", lines[0]);
        Assert.StartsWith("impact spike lever 1 - KnockDown on a landed kick / bash", lines[1]);
        Assert.Contains("landed kicks 8 (bashes 2", lines[1]);
        Assert.Contains("→ Fall seen within 1.0 s 4, not seen 0", lines[1]);
        Assert.Contains("their reaction StrikeKnockBack 2", lines[1]);
        Assert.Contains("→ WORKS - I3 / I5 knock men down", lines[1]);
        Assert.Contains("the reel (no knockdown) control 0.41 s (n 2) → treated 0.84 s (n 2), x2.05", lines[3]);
        Assert.Contains("→ WORKS - I3's longer reel", lines[3]);
        Assert.Contains("→ UNSEEN - fight a few blocks more than 10 s apart", lines[4]);
        Assert.Contains("→ FAILS (disabled by an error at impact.shield-drop)", lines[5]);
        Assert.StartsWith("impact spike lever 6 - our own knockdown blow", lines[6]);
        // the decorator never asked while kicks landed and the rotation wanted it: another damage model on top
        var t = new ImpactSpikeStats { EverOn = true, KicksLanded = 3, KnockDownPlanned = 2 };
        Assert.Contains("the decorator was NEVER asked", t.SummaryLines(new SpikeRules(true, true))[1]);
    }

    [Fact]
    public void A_battle_with_the_spike_never_on_gets_one_line()
    {
        var s = new ImpactSpikeStats();
        var off = s.SummaryLines(new SpikeRules(true, false));
        Assert.Single(off);
        Assert.Equal("impact spike (step 47): off all battle (ImpactSpike - a test tool, Advanced) - nothing asked, nothing changed", off[0]);
        Assert.Contains("the mod was OFF all battle (ModEnabled)", s.SummaryLines(new SpikeRules(false, true))[0]);
    }
}

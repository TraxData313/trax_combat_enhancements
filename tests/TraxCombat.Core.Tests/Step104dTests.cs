using System;
using System.Text;
using TraxCombat.Core;
using Xunit.Abstractions;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 104d - THE PERFECT STRIKE AND THE PUSH-OUT ON HORSEBACK (DESIGN decision 93 (e), AI_NOTES "104d - built"): a window on a RIDER who reels
/// in the saddle takes ANY landed hit on his body (Toni's answer 4 - a mounted swing is dictated by the pass), x2-x3 by the block's timing as on
/// foot; strict opposites stay for men on foot (step 103); the switches in order (the master switch first, TimedBlocksOnHorseback,
/// PerfectStrikeOnHorseback); the window book keeps the rider flag; a RIDING blocker's push-out is always his guard's own heavy parry (the
/// mounted horseback guard names too), skipped and counted when it is missing; step 105 still refuses a rider; the [summary] lines; no allocation.
/// </summary>
public class Step104dTests
{
    private readonly ITestOutputHelper _out;

    public Step104dTests(ITestOutputHelper output) => _out = output;

    private static TraxSettings Live(Action<TraxSettings>? more = null)
    {
        var s = new TraxSettings();
        s.Set(SettingsSchema.TimedBlocksOnHorseback, 1, "test"); // as shipped since step 104b (DESIGN's Default column keeps the initial OFF)
        more?.Invoke(s);
        return s;
    }

    // ------------------------------------------------------------------ A. the verdict

    [Fact]
    public void On_a_reeling_rider_any_side_counts_whatever_you_swing()
    {
        int[] dirs = { PerfectStrikeMath.Up, PerfectStrikeMath.Down, PerfectStrikeMath.Left, PerfectStrikeMath.Right, -1 };
        foreach (int blocked in dirs)
            foreach (int strike in dirs)
                Assert.Equal(StrikeVerdict.Perfect, PerfectStrikeMath.Verdict(riderWindow: true, anySide: true, blocked, strike));
    }

    [Fact]
    public void Strict_opposites_stay_for_men_on_foot_and_for_a_rider_with_any_side_off()
    {
        int[] dirs = { PerfectStrikeMath.Up, PerfectStrikeMath.Down, PerfectStrikeMath.Left, PerfectStrikeMath.Right, -1 };
        foreach (int blocked in dirs)
            foreach (int strike in dirs)
            {
                var strict = PerfectStrikeMath.Verdict(blocked, strike);
                Assert.Equal(strict, PerfectStrikeMath.Verdict(riderWindow: false, anySide: true, blocked, strike)); // a man on foot: step 103 exactly
                Assert.Equal(strict, PerfectStrikeMath.Verdict(riderWindow: true, anySide: false, blocked, strike));
            }
        Assert.Equal(StrikeVerdict.WrongSide, PerfectStrikeMath.Verdict(false, true, PerfectStrikeMath.Up, PerfectStrikeMath.Left)); // step 103's overhead ↔ thrust
    }

    [Theory]
    [InlineData(1.0, 3.0)]
    [InlineData(0.5, 2.5)]
    [InlineData(0.0, 2.0)]
    public void The_multiplier_on_a_rider_is_the_foot_one(double score, double k) // step 106d: by the weapon - a no-thrust one's x2-x3
    {
        var r = PerfectStrikeRules.From(Live());
        Assert.Equal(k, PerfectStrikeMath.Multiplier(score, in r, OpeningMath.WeaponMask(true, false)), 9);
        Assert.Equal(20f * (float)k, PerfectStrikeMath.Apply(20f, PerfectStrikeMath.Multiplier(score, in r, OpeningMath.WeaponMask(true, false))), 4);
    }

    // ------------------------------------------------------------------ B. the switches

    [Fact]
    public void The_settings_are_the_designs_and_ship_on()
    {
        Assert.Equal(1, SettingsSchema.PerfectStrikeOnHorseback.Default, 9);
        Assert.Equal(1, SettingsSchema.PerfectStrikeHorsebackAnySide.Default, 9);
        Assert.Equal(1, SettingsSchema.TimedBlockPushOutMounted.Default, 9);
        var r = PerfectStrikeRules.From(Live());
        Assert.True(r.MountedOn);
        Assert.True(r.HorsebackAnySide);
        Assert.Contains("on horseback (step 104d) ON", r.Describe());
        Assert.Contains("ANY side", r.Describe());
        Assert.True(TimedAnimRules.From(Live()).PushMountedOn);
        Assert.Contains("a mounted blocker his guard's heavy parry (step 104d)", TimedAnimRules.From(Live()).Describe());
        Assert.Contains("the perfect strike and the mounted push-out LIVE (step 104d)", TimedRiderRules.From(Live()).Describe());
    }

    [Fact]
    public void The_master_switch_comes_first_then_the_horseback_switch_then_its_own()
    {
        var off = PerfectStrikeRules.From(Live(s => s.Set(SettingsSchema.ModEnabled, 0, "test")));
        Assert.False(off.MountedOn);
        Assert.Equal("ModEnabled", off.MountedOffBecause);
        var horse = PerfectStrikeRules.From(new TraxSettings()); // TimedBlocksOnHorseback's initial OFF
        Assert.False(horse.MountedOn);
        Assert.Equal("TimedBlocksOnHorseback", horse.MountedOffBecause);
        var own = PerfectStrikeRules.From(Live(s => s.Set(SettingsSchema.PerfectStrikeOnHorseback, 0, "test")));
        Assert.False(own.MountedOn);
        Assert.True(own.Enabled); // on foot unchanged
        Assert.Equal("PerfectStrikeOnHorseback", own.MountedOffBecause);
        var strikeOff = PerfectStrikeRules.From(Live(s => s.Set(SettingsSchema.PerfectStrikeEnabled, 0, "test")));
        Assert.False(strikeOff.MountedOn);
        Assert.Equal("PerfectStrikeEnabled", strikeOff.MountedOffBecause);

        Assert.False(TimedAnimRules.From(Live(s => s.Set(SettingsSchema.ModEnabled, 0, "test"))).PushMountedOn);
        Assert.False(TimedAnimRules.From(new TraxSettings()).PushMountedOn);
        Assert.False(TimedAnimRules.From(Live(s => s.Set(SettingsSchema.TimedBlockPushOutMounted, 0, "test"))).PushMountedOn);
        Assert.False(TimedAnimRules.From(Live(s => s.Set(SettingsSchema.TimedBlockPushOut, 0, "test"))).PushMountedOn);
    }

    // ------------------------------------------------------------------ C. the window book

    [Fact]
    public void The_book_keeps_the_rider_flag_and_says_what_it_replaced()
    {
        var b = new PerfectStrikeBook();
        b.Open(7, PerfectStrikeMath.Right, 1, 10, true, out bool dropped, out bool droppedRider);
        Assert.False(dropped);
        Assert.True(b[b.IndexOf(7)].Rider);
        b.Open(7, PerfectStrikeMath.Left, 0.5, 11, false, out dropped, out droppedRider);
        Assert.True(dropped);
        Assert.True(droppedRider);
        Assert.False(b[b.IndexOf(7)].Rider);
        b.Open(8, -1, 0.5, 12, out dropped); // the step-100g call: a man on foot
        Assert.False(dropped);
        Assert.False(b[b.IndexOf(8)].Rider);
        Assert.Equal(-1, b[b.IndexOf(8)].Blocked);
    }

    // ------------------------------------------------------------------ D. the mounted push-out

    [Fact]
    public void A_riding_blocker_plays_with_the_mounted_switch_only()
    {
        var on = TimedAnimRules.From(Live());
        Assert.Equal(PushOutSkip.Play, TimedPushOutMath.Decide(in on, 1, true, false, true, true, false));
        Assert.Equal(PushOutSkip.Play, TimedPushOutMath.Decide(in on, 0.2, true, true, true, true, false)); // yours too (TimedBlockPushOutForYou on)
        Assert.Equal(PushOutSkip.NotGuarding, TimedPushOutMath.Decide(in on, 1, true, false, true, false, false));
        Assert.Equal(PushOutSkip.Busy, TimedPushOutMath.Decide(in on, 1, true, false, true, true, true));
        Assert.Equal(PushOutSkip.LowScore, TimedPushOutMath.Decide(in on, 0, true, false, true, true, false)); // an untimed block never
        var off = TimedAnimRules.From(Live(s => s.Set(SettingsSchema.TimedBlockPushOutMounted, 0, "test")));
        Assert.Equal(PushOutSkip.Mounted, TimedPushOutMath.Decide(in off, 1, true, false, true, true, false));
        Assert.Equal(PushOutSkip.Play, TimedPushOutMath.Decide(in off, 1, true, false, false, true, false)); // on foot unchanged
        var master = TimedAnimRules.From(Live(s => s.Set(SettingsSchema.ModEnabled, 0, "test")));
        Assert.Equal(PushOutSkip.Off, TimedPushOutMath.Decide(in master, 1, true, false, true, true, false));
    }

    [Fact]
    public void A_riding_blocker_always_pushes_with_his_guards_heavy_parry_never_a_bash()
    {
        var r = TimedAnimRules.From(Live());
        foreach (PushOutKind k in Enum.GetValues(typeof(PushOutKind)))
        {
            Assert.Equal(TimedPushOutMath.ParryHeavy, TimedPushOutMath.ActionFor(k, in r, mounted: true));
            Assert.Equal(TimedPushOutMath.ActionFor(k, in r), TimedPushOutMath.ActionFor(k, in r, mounted: false));
        }
        // the guards the engine plays on riders (item_usage_sets.xml): the staff's horseback defends, the shield's and the 1h / 2h foot ones
        Assert.Equal("act_defend_up_staff_parry_heavy_horseback", TimedPushOutMath.ParryHeavyOf("act_defend_up_staff_active_horseback"));
        Assert.Equal("act_defend_right_staff_parry_heavy_horseback", TimedPushOutMath.ParryHeavyOf("act_defend_right_staff_passive_horseback"));
        Assert.Equal("act_defend_shield_right_1h_parry_heavy_down", TimedPushOutMath.ParryHeavyOf("act_defend_shield_right_1h_passive_down"));
        Assert.Equal("act_defend_up_1h_parry_heavy", TimedPushOutMath.ParryHeavyOf("act_defend_up_1h_active"));
        Assert.Null(TimedPushOutMath.ParryHeavyOf("act_horseback_swing_right_1h")); // no guard on his upper body = nothing to take it from
        Assert.Equal("mounted - no heavy parry for his guard", TimedPushOutMath.SkipName(PushOutSkip.MountedNoParry));
    }

    [Fact]
    public void The_push_out_line_counts_riding_blockers_apart()
    {
        var st = new TimedAnimStats();
        Assert.Equal(TimedAnimStats.SkipCount, Enum.GetValues(typeof(PushOutSkip)).Length);
        st.NotePush(PushOutKind.Shield, true, true, false, false, mounted: true);
        st.NotePush(PushOutKind.OneHanded, false, false, false, false);
        st.NoteSkip(PushOutSkip.MountedNoParry, mounted: true);
        st.NoteSkip(PushOutSkip.NotGuarding, mounted: true);
        st.NoteSkip(PushOutSkip.NotGuarding);
        Assert.Equal(1, st.MountedPlayed);
        Assert.Equal(1, st.MountedYou);
        Assert.Equal(2, st.PushTotal);
        Assert.Equal(2, st.PushSkipped[(int)PushOutSkip.NotGuarding]);
        Assert.Equal(1, st.MountedSkipped[(int)PushOutSkip.NotGuarding]);
        string line = st.SummaryLines(TimedAnimRules.From(Live()))[0];
        _out.WriteLine(line);
        Assert.Contains("; mounted blockers (step 104d, his guard's heavy parry - on): played 1 (yours 1), skipped: not guarding any more 1, mounted - no heavy parry for his guard 1", line);
        Assert.Contains("skipped: not guarding any more 2, mounted - no heavy parry for his guard 1;", line);
    }

    // ------------------------------------------------------------------ E. step 105 and 104c stay as they are

    [Fact]
    public void Step_105_still_refuses_a_reeling_rider()
    {
        var r = ImpactRules.From(new TraxSettings()).ReelingHit;
        var usual = default(ImpactRoll);
        var o = ReelingHitMath.Decide(in r, ImpactEvent.CleanHit, false, true, false, true, ReelCause.RiderReel, mounted: true, down: false, isPlayer: false, onPlayer: 1,
            ref usual, new FixedDice(0.0));
        Assert.True(o.Asked);
        Assert.Equal(ReelingHitRefusal.Mounted, o.Refusal); // a perfect strike on a reeling rider never brings the 80 % knockdown
        Assert.False(o.Happens);
    }

    private sealed class FixedDice : IRandomSource
    {
        private readonly double _v;
        public FixedDice(double v) => _v = v;
        public double NextDouble() => _v;
    }

    // ------------------------------------------------------------------ F. the [summary] line

    [Fact]
    public void The_summary_line()
    {
        var st = new PerfectStrikeStats();
        st.RiderOpened = 3;
        st.RiderKnockedOffNoWindow = 1;
        st.NoteRiderLanded(1, 3, 20, 60, fromHorseback: true);
        st.NoteRiderLanded(0.5, 2.5, 10, 25, fromHorseback: false);
        st.RiderExpiredUnused = 1;
        st.HorseHits = 2;
        st.FootLandedFromHorseback = 1;
        st.FootWrongSideFromHorseback = 1;
        string line = st.MountedSummaryLine(PerfectStrikeRules.From(Live()));
        _out.WriteLine(line);
        Assert.StartsWith("perfect strike on horseback (step 104d; ON, any side on a rider): windows on riders reeling in the saddle after your timed block 3 (none - knocked off his horse 1), PERFECT STRIKES on them 2 (you riding 1, on foot 1)", line);
        Assert.Contains("mean multiplier x2.75 (x2.50-x3.00, mean timing 0.75), damage 30 → 85", line);
        Assert.Contains("rider windows expired unused 1", line);
        Assert.Contains("your hits on such a rider's HORSE (ignored, the window kept) 2", line);
        Assert.Contains("your strikes from horseback on men on foot (strict opposites): landed 1, wrong side 1", line);
        string off = new PerfectStrikeStats().MountedSummaryLine(PerfectStrikeRules.From(new TraxSettings()));
        Assert.StartsWith("perfect strike on horseback (step 104d; OFF (TimedBlocksOnHorseback))", off);
    }

    [Fact]
    public void The_counts_allocate_nothing()
    {
        var st = new PerfectStrikeStats();
        var an = new TimedAnimStats();
        var b = new PerfectStrikeBook();
        long bytes = NoAllocation.Bytes(() =>
        {
            for (int i = 0; i < 1000; i++)
            {
                _ = PerfectStrikeMath.Verdict(i % 2 == 0, true, i % 4, (i + 1) % 4);
                st.NoteRiderLanded(1, 3, 20, 60, i % 2 == 0);
                an.NoteSkip(PushOutSkip.MountedNoParry, true);
                an.NotePush(PushOutKind.Shield, false, true, false, false, true);
                b.Open(i % 4, i % 4, 1, i, i % 2 == 0, out _, out _);
                if (b.Count > 2) b.RemoveAt(0);
            }
        });
        Assert.Equal(0, bytes);
    }
}

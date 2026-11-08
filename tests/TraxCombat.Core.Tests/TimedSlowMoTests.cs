using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 100e - THE SLOW MOTION ON YOUR BEST BLOCKS (Toni 2026-11-08: "Maybe even try a slow mo when the player does it nicely"; AI_NOTES "Step
/// 100" §5, DESIGN decision 91 (e)): the speed profile on the real clock, the trigger decision, "someone else is slowing the game", the state's
/// give-back and the summary line.
/// </summary>
public class TimedSlowMoTests
{
    private static TraxSettings Settings(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        s.Set(SettingsSchema.ModEnabled, 1, "test");
        s.Set(SettingsSchema.AthleticsEnabled, 1, "test");
        s.Set(SettingsSchema.ImpactEnabled, 1, "test");
        s.Set(SettingsSchema.TimedBlocksEnabled, 1, "test");
        s.Set(SettingsSchema.TimedBlockSlowMo, 1, "test");
        s.Set(SettingsSchema.TimedBlockSlowMoMinScore, 0.8, "test");
        s.Set(SettingsSchema.TimedBlockSlowMoSpeed, 0.3, "test");
        s.Set(SettingsSchema.TimedBlockSlowMoRealSeconds, 0.5, "test");
        s.Set(SettingsSchema.TimedBlockSlowMoCooldownSeconds, 4, "test");
        tune?.Invoke(s);
        return s;
    }

    private static TimedSlowMoRules Rules(Action<TraxSettings>? tune = null) => TimedSlowMoRules.From(Settings(tune));

    // ------------------------------------------------------------------ the profile

    [Fact]
    public void The_speed_is_1_outside_the_slow_motion_and_never_below_the_floor_inside()
    {
        Assert.Equal(1, TimedSlowMoMath.Speed(-0.1, 0.3, 0.5));
        Assert.Equal(1, TimedSlowMoMath.Speed(0, 0.3, 0.5));
        Assert.Equal(1, TimedSlowMoMath.Speed(0.5, 0.3, 0.5));
        Assert.Equal(1, TimedSlowMoMath.Speed(3, 0.3, 0.5));
        Assert.Equal(1, TimedSlowMoMath.Speed(double.NaN, 0.3, 0.5));
        Assert.Equal(0.3, TimedSlowMoMath.Speed(0.05, 0.3, 0.5), 9); // the floor reached at the end of the ease in
        for (double t = 0.001; t < 0.5; t += 0.001)
        {
            double v = TimedSlowMoMath.Speed(t, 0.3, 0.5);
            Assert.InRange(v, 0.3 - 1e-12, 1);
        }
    }

    [Fact]
    public void The_speed_is_continuous_down_then_up()
    {
        double prev = 1;
        bool rising = false;
        for (double t = 0; t <= 0.6; t += 0.0005)
        {
            double v = TimedSlowMoMath.Speed(t, 0.3, 0.5);
            Assert.True(Math.Abs(v - prev) < 0.03, "a jump at " + t + ": " + prev + " -> " + v);
            if (v > prev + 1e-12) rising = true;
            if (rising) Assert.True(v >= prev - 1e-12, "falls again after rising at " + t);
            prev = v;
        }
        Assert.Equal(1, prev);
    }

    [Fact]
    public void The_ease_back_is_gentle_and_the_battle_time_about_a_third_of_a_second()
    {
        // flat at the floor just after the ease in, flat at 1 just before the end (smoothstep)
        Assert.True(TimedSlowMoMath.Speed(0.051, 0.3, 0.5) - 0.3 < 1e-4);
        Assert.True(1 - TimedSlowMoMath.Speed(0.499, 0.3, 0.5) < 1e-4);
        double mission = TimedSlowMoMath.MissionSeconds(0.3, 0.5);
        Assert.InRange(mission, 0.30, 0.35);
        Assert.Equal(0.5, TimedSlowMoMath.MissionSeconds(1, 0.5), 6); // speed 1 = no slow motion
    }

    [Fact]
    public void A_short_slow_motion_eases_in_over_a_tenth_and_bad_inputs_give_1()
    {
        Assert.Equal(0.3, TimedSlowMoMath.Speed(0.02, 0.3, 0.2), 9); // ease in = min(0.05, 0.2 / 10) = 0.02
        Assert.Equal(1, TimedSlowMoMath.Speed(0.1, 0.3, 0));
        Assert.Equal(1, TimedSlowMoMath.Speed(0.1, 0.3, double.NaN));
        Assert.Equal(1, TimedSlowMoMath.Speed(0.1, double.NaN, 0.5));
        Assert.True(TimedSlowMoMath.Done(0.5, 0.5));
        Assert.False(TimedSlowMoMath.Done(0.49, 0.5));
    }

    // ------------------------------------------------------------------ the trigger

    [Fact]
    public void Only_your_sharp_block_starts_it()
    {
        var r = Rules();
        Assert.Equal(SlowMoSkip.Start, TimedSlowMoMath.Decide(in r, true, 0.8, false, false, false, 10, double.NaN, false));
        Assert.Equal(SlowMoSkip.Start, TimedSlowMoMath.Decide(in r, true, 1, false, false, false, 10, double.NaN, false));
        Assert.Equal(SlowMoSkip.NotYou, TimedSlowMoMath.Decide(in r, false, 1, false, false, false, 10, double.NaN, false));
        Assert.Equal(SlowMoSkip.LowScore, TimedSlowMoMath.Decide(in r, true, 0.79, false, false, false, 10, double.NaN, false));
        Assert.Equal(SlowMoSkip.LowScore, TimedSlowMoMath.Decide(in r, true, 0, false, false, false, 10, double.NaN, false));
        Assert.Equal(SlowMoSkip.LowScore, TimedSlowMoMath.Decide(in r, true, double.NaN, false, false, false, 10, double.NaN, false));
        // a minimum of 0 still needs a timed block (score above 0)
        var any = Rules(s => s.Set(SettingsSchema.TimedBlockSlowMoMinScore, 0, "test"));
        Assert.Equal(SlowMoSkip.LowScore, TimedSlowMoMath.Decide(in any, true, 0, false, false, false, 10, double.NaN, false));
        Assert.Equal(SlowMoSkip.Start, TimedSlowMoMath.Decide(in any, true, 0.01, false, false, false, 10, double.NaN, false));
    }

    [Fact]
    public void Multiplayer_a_pause_one_playing_the_cooldown_and_another_request_each_skip()
    {
        var r = Rules();
        Assert.Equal(SlowMoSkip.Multiplayer, TimedSlowMoMath.Decide(in r, true, 1, true, false, false, 10, double.NaN, false));
        Assert.Equal(SlowMoSkip.Paused, TimedSlowMoMath.Decide(in r, true, 1, false, true, false, 10, double.NaN, false));
        Assert.Equal(SlowMoSkip.Playing, TimedSlowMoMath.Decide(in r, true, 1, false, false, true, 10, 9.9, false));
        Assert.Equal(SlowMoSkip.Cooldown, TimedSlowMoMath.Decide(in r, true, 1, false, false, false, 13.9, 10, false));
        Assert.Equal(SlowMoSkip.Start, TimedSlowMoMath.Decide(in r, true, 1, false, false, false, 14.0, 10, false));
        Assert.Equal(SlowMoSkip.OtherRequest, TimedSlowMoMath.Decide(in r, true, 1, false, false, false, 14.0, 10, true));
        var noCool = Rules(s => s.Set(SettingsSchema.TimedBlockSlowMoCooldownSeconds, 0, "test"));
        Assert.Equal(SlowMoSkip.Start, TimedSlowMoMath.Decide(in noCool, true, 1, false, false, false, 10, 10, false));
    }

    [Fact]
    public void Every_switch_off_turns_it_off_the_master_switch_first()
    {
        foreach (var key in new[] { SettingsSchema.ModEnabled, SettingsSchema.AthleticsEnabled, SettingsSchema.ImpactEnabled, SettingsSchema.TimedBlocksEnabled, SettingsSchema.TimedBlockSlowMo })
        {
            var r = Rules(s => s.Set(key, 0, "test"));
            Assert.False(r.Enabled);
            Assert.Equal(SlowMoSkip.Off, TimedSlowMoMath.Decide(in r, true, 1, false, false, false, 10, double.NaN, false));
            Assert.Contains("OFF", r.Describe());
        }
        var both = Rules(s =>
        {
            s.Set(SettingsSchema.ModEnabled, 0, "test");
            s.Set(SettingsSchema.TimedBlockSlowMo, 0, "test");
        });
        Assert.Equal("ModEnabled", both.OffBecause);
        Assert.True(Rules().Enabled);
        Assert.Null(Rules().OffBecause);
    }

    [Fact]
    public void Another_request_is_seen_by_its_id_or_by_a_scene_slower_than_ours()
    {
        Assert.True(TimedSlowMoMath.OtherSlowing(true, 1, 1));
        Assert.False(TimedSlowMoMath.OtherSlowing(false, 1, 1));
        Assert.True(TimedSlowMoMath.OtherSlowing(false, 0.5, 1)); // another mod's slow-down
        Assert.False(TimedSlowMoMath.OtherSlowing(false, 0.3, 0.3)); // our own
        Assert.False(TimedSlowMoMath.OtherSlowing(false, 0.295, 0.3)); // within the tolerance
        Assert.True(TimedSlowMoMath.OtherSlowing(false, 0.25, 0.3)); // the order menu's 0.25 under our 0.3
        Assert.False(TimedSlowMoMath.OtherSlowing(false, double.NaN, 1));
        Assert.False(TimedSlowMoMath.OtherSlowing(false, 1, double.NaN));
        Assert.False(TimedSlowMoMath.OtherSlowing(false, 2, 1)); // a fast-forward is not a slow-down
        Assert.Contains(864, TimedSlowMoMath.VanillaRequestIds);
        Assert.DoesNotContain(TimedSlowMoMath.RequestId, TimedSlowMoMath.VanillaRequestIds);
    }

    // ------------------------------------------------------------------ the state

    [Fact]
    public void The_state_ramps_on_real_time_and_ends_at_the_total()
    {
        var r = Rules();
        var m = new TimedSlowMo();
        Assert.True(double.IsNaN(m.Step(5, in r)));
        m.Start(100, 0.95);
        Assert.True(m.Playing);
        Assert.Equal(100, m.LastStartReal);
        Assert.Equal(0.3, m.Step(100.05, in r), 9);
        Assert.False(m.Stalled(100.1));
        Assert.True(m.Stalled(100.05 + TimedSlowMoMath.StallSeconds + 0.01));
        Assert.True(double.IsNaN(m.Step(100.5, in r)));
        m.Stop();
        Assert.False(m.Playing);
        Assert.Equal(100, m.LastStartReal); // the cooldown remembers
        Assert.False(m.Stalled(200));
    }

    [Fact]
    public void A_live_setting_change_applies_to_the_slow_motion_playing()
    {
        var m = new TimedSlowMo();
        m.Start(0, 1);
        var longer = Rules(s => s.Set(SettingsSchema.TimedBlockSlowMoRealSeconds, 2, "test"));
        Assert.False(double.IsNaN(m.Step(1.0, in longer)));
        Assert.True(double.IsNaN(m.Step(1.0, Rules())));
    }

    // ------------------------------------------------------------------ the summary + the settings

    [Fact]
    public void The_summary_counts_starts_skips_ends_and_the_left_over_check()
    {
        var st = new TimedSlowMoStats();
        st.NoteSkip(SlowMoSkip.Start);
        st.NoteStart(0.9);
        st.NoteAsked(0.31);
        st.NoteAsked(0.3);
        st.NoteEnd(SlowMoEnd.Finished, 0.5);
        st.NoteSkip(SlowMoSkip.Cooldown);
        st.NoteSkip(SlowMoSkip.OtherRequest);
        string before = st.SummaryLine(Rules());
        Assert.StartsWith("timed slow-mo (step 100e; ON): played 1 (mean real 0.50 s, mean score 0.90, the deepest speed asked 0.30)", before);
        Assert.Contains("the cooldown 1", before);
        Assert.Contains("another slow-down active 1", before);
        Assert.Contains("ended: finished 1", before);
        Assert.Contains("left over at the mission end not checked yet", before);
        st.CheckedAtEnd = true;
        Assert.Contains("requests left over at the mission end 0 (must be 0)", st.SummaryLine(Rules()));
        Assert.Contains("OFF (TimedBlockSlowMo)", st.SummaryLine(Rules(s => s.Set(SettingsSchema.TimedBlockSlowMo, 0, "test"))));
    }

    [Fact]
    public void The_settings_sit_where_the_design_puts_them()
    {
        var all = SettingsSchema.All.ToList();
        Assert.Equal(all.IndexOf(SettingsSchema.TimedBlocksEnabled) + 1, all.IndexOf(SettingsSchema.TimedBlockSlowMo));
        Assert.Contains(SettingsSchema.TimedBlockSlowMo, SettingsSchema.PlayerPage);
        Assert.DoesNotMatch(@"[Ss]tep \d", SettingsSchema.TimedBlockSlowMo.Description);
        var advanced = new[]
        {
            SettingsSchema.TimedBlockSlowMoMinScore, SettingsSchema.TimedBlockSlowMoSpeed, SettingsSchema.TimedBlockSlowMoRealSeconds,
            SettingsSchema.TimedBlockSlowMoCooldownSeconds,
        };
        int first = all.IndexOf(SettingsSchema.TimedBlockAiPercent) + 1;
        for (int i = 0; i < advanced.Length; i++)
        {
            Assert.Equal(first + i, all.IndexOf(advanced[i]));
            Assert.Equal(SettingsPage.Advanced, advanced[i].Group.Page);
        }
        Assert.Equal(1, SettingsSchema.TimedBlockSlowMo.Default, 5);
        Assert.Equal(0.8, SettingsSchema.TimedBlockSlowMoMinScore.Default, 5);
        Assert.Equal(0.3, SettingsSchema.TimedBlockSlowMoSpeed.Default, 5);
        Assert.Equal(0.5, SettingsSchema.TimedBlockSlowMoRealSeconds.Default, 5);
        Assert.Equal(4, SettingsSchema.TimedBlockSlowMoCooldownSeconds.Default, 5);
        Assert.Equal(50, SettingsSchema.TimedBlockAiPercent.Default, 5); // 100b's AI share untouched
    }
}

using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 100d - THE PUSH-OUT AND THE DIRECTION-MATCHED RECOIL (Toni 2026-11-08; AI_NOTES "Step 100" §4, DESIGN decision 91 (d)): the gates (the
/// master switch first), the blocker's kind and action, the heavy-parry fallback's name, the push's speed and guard window; the attacker's
/// recoil class, the names (as item_usage_sets.xml wires them), the split of the stagger (the whole kept), the summary lines.
/// </summary>
public class TimedAnimTests
{
    private static TraxSettings Settings(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        s.Set(SettingsSchema.ModEnabled, 1, "test");
        s.Set(SettingsSchema.AthleticsEnabled, 1, "test");
        s.Set(SettingsSchema.ImpactEnabled, 1, "test");
        s.Set(SettingsSchema.TimedBlocksEnabled, 1, "test");
        s.Set(SettingsSchema.TimedBlockPushOut, 1, "test");
        s.Set(SettingsSchema.TimedBlockPushOutForYou, 1, "test");
        s.Set(SettingsSchema.TimedBlockPushOutMinScore, 0, "test");
        s.Set(SettingsSchema.TimedBlockPushOutSeconds, 0.25, "test");
        s.Set(SettingsSchema.TimedBlockPushOutStartProgress, 0.3, "test");
        s.Set(SettingsSchema.TimedBlockPushOutGuardSeconds, 0.5, "test");
        Text(s, SettingsSchema.TimedBlockPushOutShieldAction, "act_shield_bash");
        Text(s, SettingsSchema.TimedBlockPushOutOneHandedAction, "act_1h_bash");
        Text(s, SettingsSchema.TimedBlockPushOutTwoHandedAction, "act_2h_bash");
        Text(s, SettingsSchema.TimedBlockPushOutPolearmAction, "act_staff_bash");
        s.Set(SettingsSchema.TimedBlockRecoil, 1, "test");
        s.Set(SettingsSchema.TimedBlockRecoilSeconds, 0.45, "test");
        s.Set(SettingsSchema.TimedBlockRecoilMaxPercent, 50, "test");
        s.Set(SettingsSchema.TimedBlockRecoilVariant, 0, "test");
        tune?.Invoke(s);
        return s;
    }

    private static void Text(TraxSettings s, ParamDef p, string text)
    {
        Assert.True(TextValues.TryIndex(text, out int i));
        s.Set(p, i, "test");
    }

    private static TimedAnimRules Rules(Action<TraxSettings>? tune = null) => TimedAnimRules.From(Settings(tune));

    // ------------------------------------------------------------------ the gates

    [Fact]
    public void The_master_switch_comes_first_then_the_impact_system_timed_blocks_and_each_part()
    {
        Assert.True(Rules().PushEnabled);
        Assert.True(Rules().RecoilEnabled);
        var off = Rules(s => s.Set(SettingsSchema.ModEnabled, 0, "test"));
        Assert.False(off.PushEnabled);
        Assert.False(off.RecoilEnabled);
        Assert.Equal("ModEnabled", off.PushOffBecause);
        Assert.Equal("ModEnabled", off.RecoilOffBecause);
        Assert.Equal("the impact system off", Rules(s => s.Set(SettingsSchema.ImpactEnabled, 0, "test")).PushOffBecause);
        Assert.Equal("TimedBlocksEnabled", Rules(s => s.Set(SettingsSchema.TimedBlocksEnabled, 0, "test")).RecoilOffBecause);
        var pushOff = Rules(s => s.Set(SettingsSchema.TimedBlockPushOut, 0, "test"));
        Assert.False(pushOff.PushEnabled);
        Assert.True(pushOff.RecoilEnabled);
        var recoilOff = Rules(s => s.Set(SettingsSchema.TimedBlockRecoil, 0, "test"));
        Assert.True(recoilOff.PushEnabled);
        Assert.False(recoilOff.RecoilEnabled);
        Assert.True(Rules(s => s.Set(SettingsSchema.TimedBlockAnimTest, 1, "test")).TestOn);
        Assert.False(Rules(s =>
        {
            s.Set(SettingsSchema.TimedBlockAnimTest, 1, "test");
            s.Set(SettingsSchema.ModEnabled, 0, "test");
        }).TestOn);
    }

    [Fact]
    public void The_push_out_decision_in_order()
    {
        var r = Rules();
        Assert.Equal(PushOutSkip.Play, TimedPushOutMath.Decide(in r, 0.3, true, false, false, true, false));
        Assert.Equal(PushOutSkip.Off, TimedPushOutMath.Decide(Rules(s => s.Set(SettingsSchema.ModEnabled, 0, "test")), 1, true, false, false, true, false));
        Assert.Equal(PushOutSkip.LowScore, TimedPushOutMath.Decide(in r, 0, true, false, false, true, false)); // an untimed riposte never
        Assert.Equal(PushOutSkip.LowScore, TimedPushOutMath.Decide(in r, double.NaN, true, false, false, true, false));
        var half = Rules(s => s.Set(SettingsSchema.TimedBlockPushOutMinScore, 0.5, "test"));
        Assert.Equal(PushOutSkip.LowScore, TimedPushOutMath.Decide(in half, 0.49, true, false, false, true, false));
        Assert.Equal(PushOutSkip.Play, TimedPushOutMath.Decide(in half, 0.5, true, false, false, true, false));
        Assert.Equal(PushOutSkip.Gone, TimedPushOutMath.Decide(in r, 1, false, false, false, true, false));
        Assert.Equal(PushOutSkip.Play, TimedPushOutMath.Decide(in r, 1, true, true, false, true, false));
        var notYou = Rules(s => s.Set(SettingsSchema.TimedBlockPushOutForYou, 0, "test"));
        Assert.Equal(PushOutSkip.NotForYou, TimedPushOutMath.Decide(in notYou, 1, true, true, false, true, false));
        Assert.Equal(PushOutSkip.Play, TimedPushOutMath.Decide(in notYou, 1, true, false, false, true, false)); // the AI's still
        Assert.Equal(PushOutSkip.Mounted, TimedPushOutMath.Decide(in r, 1, true, false, true, true, false));
        Assert.Equal(PushOutSkip.Busy, TimedPushOutMath.Decide(in r, 1, true, false, false, true, true));
        Assert.Equal(PushOutSkip.NotGuarding, TimedPushOutMath.Decide(in r, 1, true, false, false, false, false));
    }

    // ------------------------------------------------------------------ the push-out

    [Fact]
    public void The_blocker_kind_and_its_action()
    {
        var r = Rules();
        Assert.Equal(PushOutKind.Shield, TimedPushOutMath.KindOf(true, "onehanded_shield_swing"));
        Assert.Equal(PushOutKind.Shield, TimedPushOutMath.KindOf(true, null));
        Assert.Equal(PushOutKind.OneHanded, TimedPushOutMath.KindOf(false, "onehanded_swing"));
        Assert.Equal(PushOutKind.OneHanded, TimedPushOutMath.KindOf(false, "onehanded_shield_dagger"));
        Assert.Equal(PushOutKind.OneHanded, TimedPushOutMath.KindOf(false, "flail_block_shield_swing"));
        Assert.Equal(PushOutKind.TwoHanded, TimedPushOutMath.KindOf(false, "twohanded_block_swing_thrust"));
        Assert.Equal(PushOutKind.TwoHanded, TimedPushOutMath.KindOf(false, "twohanded_widegrip_axe"));
        Assert.Equal(PushOutKind.Polearm, TimedPushOutMath.KindOf(false, "polearm_block_swing_thrust"));
        Assert.Equal(PushOutKind.Polearm, TimedPushOutMath.KindOf(false, "onehanded_polearm_block_shield_thrust"));
        Assert.Equal(PushOutKind.None, TimedPushOutMath.KindOf(false, ""));
        Assert.Equal(PushOutKind.None, TimedPushOutMath.KindOf(false, "bow"));
        Assert.Equal("act_shield_bash", TimedPushOutMath.ActionFor(PushOutKind.Shield, in r));
        Assert.Equal("act_1h_bash", TimedPushOutMath.ActionFor(PushOutKind.OneHanded, in r));
        Assert.Equal("act_2h_bash", TimedPushOutMath.ActionFor(PushOutKind.TwoHanded, in r));
        Assert.Equal("act_staff_bash", TimedPushOutMath.ActionFor(PushOutKind.Polearm, in r));
        Assert.Equal(string.Empty, TimedPushOutMath.ActionFor(PushOutKind.None, in r));
        var parry = Rules(s => Text(s, SettingsSchema.TimedBlockPushOutShieldAction, TimedPushOutMath.ParryHeavy));
        Assert.Equal("parry_heavy", TimedPushOutMath.ActionFor(PushOutKind.Shield, in parry));
        Assert.Contains(TimedPushOutMath.ParryHeavy, TimedPushOutMath.Candidates);
    }

    [Fact]
    public void The_guards_own_heavy_parry_is_named_from_his_defend_action()
    {
        Assert.Equal("act_defend_up_1h_parry_heavy", TimedPushOutMath.ParryHeavyOf("act_defend_up_1h_active"));
        Assert.Equal("act_defend_up_1h_parry_heavy", TimedPushOutMath.ParryHeavyOf("act_defend_up_1h_passive"));
        Assert.Equal("act_defend_up_1h_parry_heavy", TimedPushOutMath.ParryHeavyOf("act_defend_up_1h_parry_light"));
        Assert.Equal("act_defend_up_1h_parry_heavy", TimedPushOutMath.ParryHeavyOf("act_defend_up_1h_parry_heavy"));
        Assert.Equal("act_defend_shield_right_1h_parry_heavy_down", TimedPushOutMath.ParryHeavyOf("act_defend_shield_right_1h_passive_down"));
        Assert.Equal("act_defend_shield_up_spear_parry_heavy_up", TimedPushOutMath.ParryHeavyOf("act_defend_shield_up_spear_active_up"));
        Assert.Equal("act_defend_up_1h_parry_heavy_left_stance", TimedPushOutMath.ParryHeavyOf("act_defend_up_1h_passive_left_stance"));
        Assert.Null(TimedPushOutMath.ParryHeavyOf("act_shield_bash"));
        Assert.Null(TimedPushOutMath.ParryHeavyOf(null));
        Assert.Null(TimedPushOutMath.ParryHeavyOf("act_defend_something_else"));
        Assert.Null(TimedPushOutMath.ParryHeavyOf("act_defend_up_1h_activex")); // a whole word only
    }

    [Fact]
    public void The_push_speed_skips_the_windup_and_fits_the_asked_length()
    {
        // a 1.0 s bash from 0.3 in 0.25 s = 0.7 / 0.25 = 2.8
        Assert.Equal(2.8f, TimedPushOutMath.Speed(1.0, 0.3, 0.25), 4);
        Assert.Equal(0.25, TimedPushOutMath.PlaySeconds(1.0, 0.3, 2.8f), 4);
        Assert.Equal(TimedPushOutMath.MaxSpeed, TimedPushOutMath.Speed(10, 0, 0.1));
        Assert.Equal(TimedPushOutMath.MinSpeed, TimedPushOutMath.Speed(0.1, 0.5, 1));
        Assert.Equal(2.8f, TimedPushOutMath.Speed(double.NaN, 0.3, 0.25), 4); // an unknown length counts as 1 s
        Assert.Equal(2.8f, TimedPushOutMath.Speed(0, 0.3, 0.25), 4);
        Assert.Equal(TimedPushOutMath.MaxSpeed, TimedPushOutMath.Speed(1, 0.3, 0));
        // start progress capped at 0.9
        Assert.Equal(0.4f, TimedPushOutMath.Speed(1, 5, 0.25), 4);
    }

    [Fact]
    public void The_guard_window_is_never_shorter_than_the_push()
    {
        var r = Rules();
        Assert.Equal(0.5, TimedPushOutMath.GuardWindow(in r, 0.25), 9);
        Assert.Equal(0.9, TimedPushOutMath.GuardWindow(in r, 0.8), 9);
        var zero = Rules(s => s.Set(SettingsSchema.TimedBlockPushOutGuardSeconds, 0, "test"));
        Assert.Equal(0.35, TimedPushOutMath.GuardWindow(in zero, 0.25), 9);
        Assert.Equal(0.1, TimedPushOutMath.GuardWindow(in zero, double.NaN), 9);
    }

    // ------------------------------------------------------------------ the recoil

    [Fact]
    public void The_recoil_class_follows_the_item_usage_set()
    {
        Assert.Equal(RecoilClass.OneHanded, TimedRecoilMath.ClassOf("onehanded_shield_swing", true));
        Assert.Equal(RecoilClass.OneHanded, TimedRecoilMath.ClassOf("onehanded_swing_thrust", false));
        Assert.Equal(RecoilClass.Dagger, TimedRecoilMath.ClassOf("onehanded_shield_dagger", false));
        Assert.Equal(RecoilClass.Flail, TimedRecoilMath.ClassOf("flail_block_shield_swing", true));
        Assert.Equal(RecoilClass.Lance, TimedRecoilMath.ClassOf("onehanded_polearm_block_shield_thrust", true));
        Assert.Equal(RecoilClass.TwoHanded, TimedRecoilMath.ClassOf("twohanded_widegrip_axe", false));
        Assert.Equal(RecoilClass.Staff, TimedRecoilMath.ClassOf("polearm_block_swing_thrust", false));
        Assert.Equal(RecoilClass.Staff, TimedRecoilMath.ClassOf("polearm_block_shield_swing_thrust", false));
        Assert.Equal(RecoilClass.Lance, TimedRecoilMath.ClassOf("polearm_block_shield_swing_thrust", true));
        Assert.Equal(RecoilClass.Spear, TimedRecoilMath.ClassOf("polearm_block_thrust", false));
        Assert.Equal(RecoilClass.Spear, TimedRecoilMath.ClassOf("polearm_pike", false));
        Assert.Equal(RecoilClass.Spear, TimedRecoilMath.ClassOf("polearm_block_shield_thrust", false));
        Assert.Equal(RecoilClass.Lance, TimedRecoilMath.ClassOf("polearm_block_shield_thrust", true));
        Assert.Equal(RecoilClass.None, TimedRecoilMath.ClassOf("polearm_bracing", false));
        Assert.Equal(RecoilClass.None, TimedRecoilMath.ClassOf("polearm_couch", false));
        Assert.Equal(RecoilClass.None, TimedRecoilMath.ClassOf("bow", false));
        Assert.Equal(RecoilClass.Fist, TimedRecoilMath.ClassOf(null, false));
        Assert.Equal(RecoilClass.Fist, TimedRecoilMath.ClassOf("no_weapon", false));
        Assert.Equal(RecoilClass.FistShield, TimedRecoilMath.ClassOf("", true));
    }

    [Fact]
    public void The_recoil_names_are_the_games_own_for_each_direction_weapon_and_stance()
    {
        Assert.Equal("act_blocked_overswing_1h", TimedRecoilMath.Names(RecoilClass.OneHanded, TimedRecoilMath.Up, 0, false)[0]);
        Assert.Equal("act_blocked_thrust_1h", TimedRecoilMath.Names(RecoilClass.OneHanded, TimedRecoilMath.Down, 0, false)[0]);
        Assert.Equal("act_blocked_slashleft_1h", TimedRecoilMath.Names(RecoilClass.OneHanded, TimedRecoilMath.Left, 0, false)[0]);
        Assert.Equal("act_blocked_slashright_1h", TimedRecoilMath.Names(RecoilClass.OneHanded, TimedRecoilMath.Right, 0, false)[0]);
        Assert.Equal(new[] { "act_blocked_slashleft_2h_left_stance", "act_blocked_slashleft_2h" },
            TimedRecoilMath.Names(RecoilClass.TwoHanded, TimedRecoilMath.Left, 0, true));
        Assert.Equal(new[] { "act_quick_blocked_thrust_staff_left_stance", "act_quick_blocked_thrust_staff", "act_blocked_thrust_staff_left_stance", "act_blocked_thrust_staff" },
            TimedRecoilMath.Names(RecoilClass.Staff, TimedRecoilMath.Down, 1, true));
        Assert.Equal(new[] { "act_blocked_overswing_1h_balanced", "act_blocked_overswing_1h" },
            TimedRecoilMath.Names(RecoilClass.OneHanded, TimedRecoilMath.Up, 2, false));
        Assert.Equal("act_quick_blocked_overswing_2h_balanced_left_stance", TimedRecoilMath.Names(RecoilClass.TwoHanded, TimedRecoilMath.Up, 3, true)[0]);
        // the game's spelling of a dagger's side swings in a left stance, its backstab, no balanced dagger
        Assert.Equal("act_blocked_slashleft_dagger_leftstance", TimedRecoilMath.Names(RecoilClass.Dagger, TimedRecoilMath.Left, 0, true)[0]);
        Assert.Equal("act_blocked_backstab_1h_left_stance", TimedRecoilMath.Names(RecoilClass.Dagger, TimedRecoilMath.Up, 0, true)[0]);
        Assert.Equal("act_blocked_slashright_dagger", TimedRecoilMath.Names(RecoilClass.Dagger, TimedRecoilMath.Right, 2, false)[0]);
        Assert.Empty(TimedRecoilMath.Names(RecoilClass.Dagger, TimedRecoilMath.Down, 0, false));
        Assert.Equal("act_blocked_overswing_spear", TimedRecoilMath.Names(RecoilClass.Spear, TimedRecoilMath.Up, 0, false)[0]);
        Assert.Equal("act_blocked_thrust_staff", TimedRecoilMath.Names(RecoilClass.Spear, TimedRecoilMath.Down, 0, false)[0]);
        Assert.Empty(TimedRecoilMath.Names(RecoilClass.Spear, TimedRecoilMath.Left, 0, false));
        Assert.Equal("act_blocked_upperthrust_1h_lance", TimedRecoilMath.Names(RecoilClass.Lance, TimedRecoilMath.Up, 0, false)[0]);
        Assert.Equal("act_blocked_thrust_1h_lance", TimedRecoilMath.Names(RecoilClass.Lance, TimedRecoilMath.Down, 0, false)[0]);
        Assert.Equal("act_blocked_swingright_fist_shield", TimedRecoilMath.Names(RecoilClass.FistShield, TimedRecoilMath.Right, 0, false)[0]);
        Assert.Equal("act_blocked_uppercut_fist", TimedRecoilMath.Names(RecoilClass.Fist, TimedRecoilMath.Down, 0, false)[0]);
        Assert.Empty(TimedRecoilMath.Names(RecoilClass.None, TimedRecoilMath.Up, 0, false));
        Assert.Empty(TimedRecoilMath.Names(RecoilClass.OneHanded, -1, 0, false));
        Assert.Empty(TimedRecoilMath.Names(RecoilClass.OneHanded, 4, 0, false));
        Assert.Equal("act_blocked_overswing_1h", TimedRecoilMath.Names(RecoilClass.OneHanded, TimedRecoilMath.Up, 99, false)[0]); // a bad variant = plain
        // the same array every time (precomputed - nothing allocated per riposte)
        Assert.Same(TimedRecoilMath.Names(RecoilClass.OneHanded, 0, 0, true), TimedRecoilMath.Names(RecoilClass.OneHanded, 0, 0, true));
        // every name is a plain action name and starts the game's way
        foreach (RecoilClass c in Enum.GetValues(typeof(RecoilClass)))
            for (int d = 0; d < 4; d++)
                for (int v = 0; v < 4; v++)
                    foreach (bool l in new[] { false, true })
                        foreach (var n in TimedRecoilMath.Names(c, d, v, l))
                            Assert.Matches("^act_(quick_)?blocked_[a-z0-9_]+$", n);
    }

    [Fact]
    public void The_direction_is_the_engines_attack_direction()
    {
        Assert.Equal(0, TimedRecoilMath.DirectionOf(0));
        Assert.Equal(3, TimedRecoilMath.DirectionOf(3));
        Assert.Equal(-1, TimedRecoilMath.DirectionOf(-1));
        Assert.Equal(-1, TimedRecoilMath.DirectionOf(4)); // a defend direction
        Assert.Equal("overhead", TimedRecoilMath.DirectionName(0));
        Assert.Equal("thrust", TimedRecoilMath.DirectionName(1));
    }

    [Fact]
    public void The_split_keeps_the_whole_stagger_and_falls_back_when_too_short()
    {
        var r = Rules();
        // 2.0 s timed stagger, a 0.6 s recoil animation: the recoil gets 0.45 s (speed 1.333), the backward stagger 1.55 s
        var p = TimedRecoilMath.Plan(2.0, 0.6, in r);
        Assert.True(p.Use);
        Assert.Equal(0.6f / 0.45f, p.RecoilSpeed, 4);
        Assert.Equal(0.45, p.RecoilSeconds, 4);
        Assert.Equal(2.0, p.RecoilSeconds + p.StaggerSeconds, 9);
        // a 0.8 s stagger: at most 50 % = 0.4 s recoil, the rest 0.4
        var q = TimedRecoilMath.Plan(0.8, 0.6, in r);
        Assert.True(q.Use);
        Assert.Equal(0.4, q.RecoilSeconds, 4);
        Assert.Equal(0.8, q.RecoilSeconds + q.StaggerSeconds, 9);
        // the speed clamped: a 3 s animation in 0.45 s would be x6.7 → 2.5, its real length 1.2 s, the rest 0.8
        var c = TimedRecoilMath.Plan(2.0, 3.0, in r);
        Assert.True(c.Use);
        Assert.Equal(TimedRecoilMath.MaxSpeed, c.RecoilSpeed);
        Assert.Equal(1.2, c.RecoilSeconds, 4);
        Assert.Equal(2.0, c.RecoilSeconds + c.StaggerSeconds, 9);
        // too short to split
        Assert.False(TimedRecoilMath.Plan(0.4, 0.6, in r).Use); // recoil 0.2 → rest 0.2 < 0.25
        Assert.False(TimedRecoilMath.Plan(0.15, 0.6, in r).Use);
        Assert.False(TimedRecoilMath.Plan(2.0, 10, in r).Use); // the recoil alone 4 s at the cap - longer than the stagger
        Assert.False(TimedRecoilMath.Plan(double.NaN, 0.6, in r).Use);
        Assert.False(TimedRecoilMath.Plan(2.0, double.NaN, in r).Use);
        Assert.False(TimedRecoilMath.Plan(2.0, 0, in r).Use);
        Assert.False(TimedRecoilMath.Plan(double.PositiveInfinity, 0.6, in r).Use);
        // never longer than the whole for any input
        for (double t = 0.3; t <= 3; t += 0.1)
            for (double a = 0.1; a <= 2; a += 0.1)
            {
                var x = TimedRecoilMath.Plan(t, a, in r);
                if (!x.Use) continue;
                Assert.InRange(x.StaggerSeconds, TimedRecoilMath.MinStaggerSeconds - 1e-9, t);
                Assert.Equal(t, x.RecoilSeconds + x.StaggerSeconds, 9);
                Assert.InRange(x.RecoilSpeed, TimedRecoilMath.MinSpeed, TimedRecoilMath.MaxSpeed);
            }
    }

    // ------------------------------------------------------------------ the summary

    [Fact]
    public void The_summary_counts_push_outs_by_kind_and_skip_and_recoils_by_direction_and_fallback()
    {
        var st = new TimedAnimStats();
        st.NotePush(PushOutKind.Shield, true, false, false, false);
        st.NotePush(PushOutKind.TwoHanded, false, true, true, true);
        st.NoteSkip(PushOutSkip.NotGuarding);
        st.NoteSkip(PushOutSkip.NotGuarding);
        st.NoteRecoil(TimedRecoilMath.Left, 0.45, 1.55, false);
        st.NoteRecoil(TimedRecoilMath.Down, 0.4, 0.4, true);
        st.NoteFallback(RecoilFallback.NoClass);
        st.NoteFallback(RecoilFallback.None); // not counted
        st.BashBlowsIgnored = 3;
        Assert.Equal(2, st.PushTotal);
        Assert.Equal(2, st.RecoilTotal);
        var lines = st.SummaryLines(Rules());
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("timed push-out (step 100d): on - played 2 (shield 1 / one-handed 0 / two-handed 1 / polearm 0; yours 1", lines[0]);
        Assert.Contains("not guarding any more 2", lines[0]);
        Assert.Contains("ignored 3 (the engine's blow)", lines[0]);
        Assert.StartsWith("timed recoils (step 100d): on, the blocked recoil - played 2 (overhead 0 / thrust 1 / left swing 1 / right swing 0", lines[1]);
        Assert.Contains("no recoil for his weapon / direction 1", lines[1]);
        Assert.Contains("mean 0.43 s recoil + 0.98 s backward stagger", lines[1]);
        var off = new TimedAnimStats().SummaryLines(Rules(s => s.Set(SettingsSchema.ModEnabled, 0, "test")));
        Assert.Contains("off (ModEnabled)", off[0]);
        Assert.Contains("skipped: none", off[0]);
        Assert.Contains("fell back to the single stagger: none", off[1]);
    }
}

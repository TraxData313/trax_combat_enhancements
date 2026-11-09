using System;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 106c - THE RED ARROW, the pure part (AI_NOTES "106c - built", DESIGN decision 95 (k)): vanilla's crosshair mapping (AttackUp → top,
/// AttackRight → right, AttackDown → bottom, AttackLeft → left - MissionGauntletCrosshair's table), the flash's fade around its peak, the K test
/// tool's flash by the forced guard (OpeningSidesCrossed both ways), the defensive game's finishing sides by the weapon (106a2's no-thrust and
/// spear rules), a rider's any-side window = nothing, the gates (the flash only with OpeningsEnabled or the test tool), the setting on the
/// player page, no allocation.
/// </summary>
public class Step106cTests
{
    private static OpeningRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return OpeningRules.From(s);
    }

    private static int B(int attack) => OpeningMath.Bit(attack);

    private const int Sword = OpeningMath.AllAttacks;
    private static readonly int Axe = OpeningMath.WeaponMask(true, false);
    private static readonly int Spear = OpeningMath.WeaponMask(false, true);

    [Fact]
    public void The_arrows_sit_where_vanilla_lights_your_attack_direction()
    {
        // MissionGauntletCrosshair: UsageDirection 0 → [0] top, 3 → [1] right, 1 → [2] bottom, 2 → [3] left (SetArrowProperties(top, right, bottom, left))
        Assert.Equal(OpeningArrow.Top, OpeningMath.ArrowFor(0));
        Assert.Equal(OpeningArrow.Right, OpeningMath.ArrowFor(3));
        Assert.Equal(OpeningArrow.Bottom, OpeningMath.ArrowFor(1));
        Assert.Equal(OpeningArrow.Left, OpeningMath.ArrowFor(2));
        foreach (OpeningArrow a in new[] { OpeningArrow.Top, OpeningArrow.Bottom, OpeningArrow.Left, OpeningArrow.Right })
        {
            Assert.Equal(a, OpeningMath.ArrowFor(OpeningArrowMath.AttackOf(a)));
            Assert.True(OpeningArrowMath.Lit(B(OpeningArrowMath.AttackOf(a)), a));
        }
        Assert.Equal(OpeningMath.Unknown, OpeningArrowMath.AttackOf(OpeningArrow.None));
        Assert.False(OpeningArrowMath.Lit(OpeningMath.AllAttacks, OpeningArrow.None));
        Assert.Equal("left + right", OpeningArrowMath.ArrowsName(B(OpeningMath.AttackLeft) | B(OpeningMath.AttackRight)));
        Assert.Equal("none", OpeningArrowMath.ArrowsName(0));
        Assert.Equal((131, 31, 350), (OpeningArrowMath.ArrowLength, OpeningArrowMath.ArrowThickness, OpeningArrowMath.ArrowOffset));
    }

    [Fact]
    public void The_flash_fades_in_to_full_at_the_perfect_moment_and_out_after()
    {
        const double peak = 10.3;
        Assert.Equal(0f, OpeningArrowMath.FlashAlpha(10.2, peak));
        Assert.Equal(0f, OpeningArrowMath.FlashAlpha(peak - 0.051, peak));
        Assert.Equal(0.5f, OpeningArrowMath.FlashAlpha(peak - 0.025, peak), 3);
        Assert.Equal(1f, OpeningArrowMath.FlashAlpha(peak, peak), 5);
        Assert.Equal(0.5f, OpeningArrowMath.FlashAlpha(peak + 0.075, peak), 3);
        Assert.Equal(0f, OpeningArrowMath.FlashAlpha(peak + 0.151, peak));
        Assert.Equal(0f, OpeningArrowMath.FlashAlpha(peak, double.NaN));
        Assert.False(OpeningArrowMath.FlashOver(peak + 0.15, peak));
        Assert.True(OpeningArrowMath.FlashOver(peak + 0.16, peak));
        Assert.True(OpeningArrowMath.FlashOver(peak, double.NaN));
    }

    [Fact]
    public void The_test_tools_flash_is_the_side_the_forced_guard_leaves_open()
    {
        Assert.Equal(OpeningMath.AttackUp, OpeningArrowMath.TestFlashAttack(OpeningMath.GuardDown, true)); // low guard → top
        Assert.Equal(OpeningMath.AttackDown, OpeningArrowMath.TestFlashAttack(OpeningMath.GuardUp, true)); // high guard → bottom
        Assert.Equal(OpeningMath.AttackLeft, OpeningArrowMath.TestFlashAttack(OpeningMath.GuardLeft, true)); // crossed: the same-named side
        Assert.Equal(OpeningMath.AttackRight, OpeningArrowMath.TestFlashAttack(OpeningMath.GuardRight, true));
        Assert.Equal(OpeningMath.AttackRight, OpeningArrowMath.TestFlashAttack(OpeningMath.GuardLeft, false)); // same-named sides: the other
        Assert.Equal(OpeningMath.AttackLeft, OpeningArrowMath.TestFlashAttack(OpeningMath.GuardRight, false));
        Assert.Equal(OpeningMath.Unknown, OpeningArrowMath.TestFlashAttack(OpeningMath.Unknown, true)); // a lowered mode: no flash
    }

    [Fact]
    public void The_defensive_games_side_is_your_strict_answer_by_your_weapon()
    {
        // a sword: step 103's strict opposites (his overhead → your thrust → bottom; his left swing → your left → left)
        Assert.Equal(B(OpeningMath.AttackDown), OpeningArrowMath.DefensiveFinishMask(PerfectStrikeMath.Up, Sword, true));
        Assert.Equal(B(OpeningMath.AttackUp), OpeningArrowMath.DefensiveFinishMask(PerfectStrikeMath.Down, Sword, true));
        Assert.Equal(B(OpeningMath.AttackLeft), OpeningArrowMath.DefensiveFinishMask(PerfectStrikeMath.Left, Sword, true));
        Assert.Equal(B(OpeningMath.AttackRight), OpeningArrowMath.DefensiveFinishMask(PerfectStrikeMath.Right, Sword, true));
        // an axe (no thrust): his overhead → your left OR right (both arrows); off = the thrust alone
        Assert.Equal(B(OpeningMath.AttackLeft) | B(OpeningMath.AttackRight), OpeningArrowMath.DefensiveFinishMask(PerfectStrikeMath.Up, Axe, true));
        Assert.Equal(B(OpeningMath.AttackDown), OpeningArrowMath.DefensiveFinishMask(PerfectStrikeMath.Up, Axe, false));
        Assert.Equal(B(OpeningMath.AttackLeft), OpeningArrowMath.DefensiveFinishMask(PerfectStrikeMath.Left, Axe, true));
        // a spear: a side swing → either stab (top + bottom); a vertical one → the other stab
        Assert.Equal(B(OpeningMath.AttackUp) | B(OpeningMath.AttackDown), OpeningArrowMath.DefensiveFinishMask(PerfectStrikeMath.Right, Spear, true));
        Assert.Equal(B(OpeningMath.AttackDown), OpeningArrowMath.DefensiveFinishMask(PerfectStrikeMath.Up, Spear, true));
        // unknown blocked direction: nothing
        Assert.Equal(0, OpeningArrowMath.DefensiveFinishMask(-1, Sword, true));
        // a rider's window: any side = nothing to point at; strict = the foot rule
        Assert.Equal(0, OpeningArrowMath.WindowFinishMask(true, true, PerfectStrikeMath.Left, Sword, true));
        Assert.Equal(B(OpeningMath.AttackLeft), OpeningArrowMath.WindowFinishMask(true, false, PerfectStrikeMath.Left, Sword, true));
        Assert.Equal(B(OpeningMath.AttackLeft), OpeningArrowMath.WindowFinishMask(false, true, PerfectStrikeMath.Left, Sword, true));
    }

    [Fact]
    public void The_flash_needs_the_live_openings_or_the_test_tool_the_steady_arrow_only_its_switch()
    {
        var r = Rules(); // OpeningsEnabled ships OFF, OpeningArrowShow ON
        Assert.True(r.ArrowShow);
        Assert.False(OpeningArrowMath.ShowsFlash(in r, false));
        Assert.True(OpeningArrowMath.ShowsFlash(in r, true));
        Assert.True(OpeningArrowMath.ShowsFinish(in r));
        r = Rules(s => s.Set(SettingsSchema.OpeningsEnabled, true, SettingSources.File));
        Assert.True(OpeningArrowMath.ShowsFlash(in r, false));
        r = Rules(s => s.Set(SettingsSchema.OpeningArrowShow, false, SettingSources.File));
        Assert.False(OpeningArrowMath.ShowsFlash(in r, true));
        Assert.False(OpeningArrowMath.ShowsFinish(in r));
        r = Rules(s => s.Set(SettingsSchema.ModEnabled, false, SettingSources.File));
        Assert.False(OpeningArrowMath.ShowsFlash(in r, true));
        Assert.False(OpeningArrowMath.ShowsFinish(in r));
    }

    [Fact]
    public void The_arrows_switch_is_on_the_player_page_and_on()
    {
        Assert.Equal(SettingsPage.Player, SettingsSchema.OpeningArrowShow.Group.Page);
        Assert.True(new TraxSettings().OpeningArrowShow);
    }

    [Fact]
    public void Reading_the_arrow_allocates_nothing()
    {
        var r = Rules();
        long bytes = NoAllocation.Bytes(() =>
        {
            for (int i = 0; i < 1000; i++)
            {
                var a = new ArrowReading { Cue = ArrowCue.Flash, Mask = OpeningMath.Bit(i & 3), Alpha = OpeningArrowMath.FlashAlpha(i * 0.001, 0.3) };
                _ = a.Shows && OpeningArrowMath.Lit(a.Mask, OpeningArrow.Top) && OpeningArrowMath.ShowsFlash(in r, false);
                _ = OpeningArrowMath.WindowFinishMask(false, true, i & 3, Axe, true);
                _ = OpeningArrowMath.TestFlashAttack(OpeningMath.GuardUp + (i & 3), true);
            }
        });
        Assert.Equal(0, bytes);
    }
}

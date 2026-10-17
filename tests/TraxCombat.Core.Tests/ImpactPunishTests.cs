using System;
using System.Collections.Generic;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 54 (IMPACT I7, the AI punishes a window - AI_NOTES "Step 54"): the four settings; the rules' gates (the master switch first, never in
/// shadow / the spike); the window (reeling or down + the grace); the share raised during it - never lowered, never the player, 0.05 steps;
/// a timid man's press within the range (never in a panic); a real decision's stagger / knockdown opens one; the summary lines.
/// </summary>
public class ImpactPunishTests
{
    private sealed class Dice : IRandomSource
    {
        public double NextDouble() => 0.0;
    }

    private static PunishRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return PunishRules.From(s);
    }

    private static ImpactState ReelingUntil(double until)
    {
        var st = default(ImpactState);
        st.Reel(until, 2);
        return st;
    }

    [Fact]
    public void The_four_step_54_settings_sit_after_the_on_screen_lines_live_with_their_defaults()
    {
        var keys = SettingsSchema.InGroup(SettingsSchema.ImpactGroup).Select(p => p.Key).ToList();
        int i = keys.IndexOf("ImpactMessageMinSeconds");
        var mine = new[] { "ImpactPunishEnabled", "ImpactPunishAggressionPercent", "ImpactPunishGraceSeconds", "ImpactPunishRangeMetres" };
        Assert.Equal(mine, keys.Skip(i + 1).Take(4));
        foreach (var k in mine)
        {
            Assert.True(SettingsSchema.TryGet(k, out var p));
            Assert.Equal(ApplyTiming.Live, p.Timing);
        }
        var s = new TraxSettings();
        Assert.True(s.ImpactPunishEnabled);
        Assert.Equal(100, s.ImpactPunishAggressionPercent);
        Assert.Equal(0.5f, s.ImpactPunishGraceSeconds);
        Assert.Equal(3.0f, s.ImpactPunishRangeMetres);
        var r = Rules();
        Assert.True(r.Enabled && r.WindowsLive);
        Assert.Equal(1.0, r.Share, 9);
        Assert.Contains("decides to attack at 100% of the game's AI", r.Describe());
        Assert.Contains("+ 0.5 s; a timid man within 3.0 m steps in", r.Describe());
    }

    [Fact]
    public void The_master_switch_comes_first_and_shadow_or_the_spike_means_no_window()
    {
        var off = Rules(s => { s.Set(SettingsSchema.ModEnabled, false, SettingSources.File); s.Set(SettingsSchema.ImpactEnabled, false, SettingSources.File); });
        Assert.False(off.WindowsLive);
        Assert.Equal("ModEnabled", off.OffBecause);
        Assert.Equal("AthleticsEnabled", Rules(s => s.Set(SettingsSchema.AthleticsEnabled, false, SettingSources.File)).OffBecause);
        Assert.Equal("ImpactEnabled", Rules(s => s.Set(SettingsSchema.ImpactEnabled, false, SettingSources.File)).OffBecause);
        var shadow = Rules(s => s.Set(SettingsSchema.ImpactShadowMode, true, SettingSources.File));
        Assert.False(shadow.WindowsLive);
        Assert.Equal("ImpactShadowMode", shadow.OffBecause);
        Assert.False(Rules(s => s.Set(SettingsSchema.ImpactSpike, true, SettingSources.File)).WindowsLive);
        var none = Rules(s =>
        {
            s.Set(SettingsSchema.ImpactKicksEnabled, false, SettingSources.File);
            s.Set(SettingsSchema.ImpactBlocksEnabled, false, SettingSources.File);
            s.Set(SettingsSchema.ImpactHitsEnabled, false, SettingSources.File);
            s.Set(SettingsSchema.ImpactRiposteEnabled, false, SettingSources.File);
        });
        Assert.False(none.WindowsLive);
        Assert.True(Rules(s => s.Set(SettingsSchema.ImpactKicksEnabled, false, SettingSources.File)).WindowsLive); // one family is enough
        var punishOff = Rules(s => s.Set(SettingsSchema.ImpactPunishEnabled, false, SettingSources.File));
        Assert.True(punishOff.WindowsLive); // measured with the punish off too (the ON / OFF proof)
        Assert.False(punishOff.Enabled);
        Assert.Equal("ImpactPunishEnabled", punishOff.OffBecause);
        Assert.Contains("the windows are still measured", punishOff.Describe());
    }

    [Fact]
    public void A_window_is_reeling_or_down_plus_the_grace()
    {
        var reel = ReelingUntil(10);
        Assert.True(PunishMath.InWindow(in reel, 9.0, 0.5));
        Assert.True(PunishMath.InWindow(in reel, 10.4, 0.5));  // the grace
        Assert.False(PunishMath.InWindow(in reel, 10.5, 0.5));
        Assert.False(PunishMath.InWindow(in reel, 10.0, 0));   // no grace: ends with the reel
        var down = default(ImpactState);
        down.KnockedDown(5, 8, 5);
        Assert.True(PunishMath.InWindow(in down, 7.9, 0));
        Assert.True(PunishMath.InWindow(in down, 8.2, 0.5));
        Assert.False(PunishMath.InWindow(in down, 8.6, 0.5));
        down.GotUp(6, 5);                                       // measured up early: the window closes with it
        Assert.False(PunishMath.InWindow(in down, 6.6, 0.5));
        var never = default(ImpactState);
        Assert.False(PunishMath.InWindow(in never, 0, 0.5));    // nothing happened = never a window, even at time 0
        Assert.Equal(10, PunishMath.WindowEnd(in reel));
    }

    [Fact]
    public void The_share_is_raised_during_a_window_never_lowered_never_the_player()
    {
        var r = Rules();
        Assert.Equal(1.0f, PunishMath.Raise(0.6f, in r, true, false));  // a fresh man's 60% → the game's full eagerness
        Assert.Equal(1.0f, PunishMath.Raise(0.2f, in r, true, false));  // an exhausted man's 20%
        Assert.Equal(1.0f, PunishMath.Raise(0.8f, in r, true, false));  // a fever's peak
        Assert.Equal(0.6f, PunishMath.Raise(0.6f, in r, false, false)); // not punishing: his own
        Assert.Equal(1.0f, PunishMath.Raise(1.0f, in r, true, true));   // the player: untouched (his factor is the game's 1)
        Assert.Equal(0.4f, PunishMath.Raise(0.4f, in r, true, true));
        var half = Rules(s => s.Set(SettingsSchema.ImpactPunishAggressionPercent, 50, SettingSources.Mcm));
        Assert.Equal(0.6f, PunishMath.Raise(0.6f, in half, true, false)); // never lowered
        Assert.Equal(0.5f, PunishMath.Raise(0.3f, in half, true, false));
        var odd = Rules(s => s.Set(SettingsSchema.ImpactPunishAggressionPercent, 72, SettingSources.Mcm));
        Assert.Equal(0.7f, PunishMath.Raise(0.2f, in odd, true, false), 5); // 0.05 steps
        var off = Rules(s => s.Set(SettingsSchema.ImpactPunishEnabled, false, SettingSources.Mcm));
        Assert.Equal(0.6f, PunishMath.Raise(0.6f, in off, true, false));
        var master = Rules(s => s.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm));
        Assert.Equal(0.6f, PunishMath.Raise(0.6f, in master, true, false));
        var big = Rules(s => s.Set(SettingsSchema.ImpactPunishAggressionPercent, 200, SettingSources.Mcm));
        Assert.Equal(2.0f, PunishMath.Raise(0.6f, in big, true, false)); // the decorator's ApplyChance clamps the chance to 1
    }

    [Fact]
    public void A_timid_man_presses_only_within_the_range_and_never_in_a_panic()
    {
        var r = Rules();
        Assert.True(PunishMath.TimidPresses(in r, false, 2.9));
        Assert.True(PunishMath.TimidPresses(in r, false, 3.0));
        Assert.False(PunishMath.TimidPresses(in r, false, 3.1));
        Assert.False(PunishMath.TimidPresses(in r, true, 1.0));
        Assert.False(PunishMath.TimidPresses(in r, false, double.NaN));
        Assert.False(PunishMath.TimidPresses(Rules(s => s.Set(SettingsSchema.ImpactPunishRangeMetres, 0.0, SettingSources.Mcm)), false, 0.0));
        Assert.False(PunishMath.TimidPresses(Rules(s => s.Set(SettingsSchema.ImpactPunishEnabled, false, SettingSources.Mcm)), false, 1.0));
        Assert.True(PunishMath.TimidPresses(Rules(s => s.Set(SettingsSchema.ImpactPunishRangeMetres, 6.0, SettingSources.Mcm)), false, 5.0));
    }

    [Fact]
    public void A_real_decision_on_an_exhausted_blocker_opens_a_window_a_fresh_one_none()
    {
        var ir = ImpactRules.From(new TraxSettings());
        var pr = Rules();
        ImpactFacts Block(double bar) => new ImpactFacts
        {
            Event = ImpactEvent.RightBlock,
            AttackerPoints = 100, AttackerF = 1, AttackerAnimation = 1, AttackerSkill = 100,
            DefenderPoints = 100 * bar, DefenderBar = bar, DefenderF = Math.Min(1, bar / 0.75), DefenderSkill = 100,
            WeaponWeight = 1.2, SwingSpeed = 92, DamageType = ImpactDamageType.Cut, GuardWeight = 4.0, DefenderHasShield = true, DefenderMaxHealth = 100,
        };
        var tired = default(ImpactState);
        var attacker = default(ImpactState);
        var f = Block(0.0);
        var d = ImpactMath.Decide(in ir, in f, ref tired, ref attacker, 100, new Dice());
        Assert.True(d.Stagger.Happens || d.Knockdown.Happens, "the dice at 0 on an exhausted blocker: no stagger / knockdown");
        Assert.True(PunishMath.InWindow(in tired, 100.1, pr.GraceSeconds));
        Assert.True(PunishMath.WindowEnd(in tired) > 100.5);
        var fresh = default(ImpactState);
        var a2 = default(ImpactState);
        var f2 = Block(1.0);
        ImpactMath.Decide(in ir, in f2, ref fresh, ref a2, 100, new Dice());
        Assert.False(PunishMath.InWindow(in fresh, 100.1, pr.GraceSeconds)); // a fresh blocker: no outcome, no window
    }

    [Fact]
    public void The_summary_counts_windows_punished_and_the_rates_on_and_off()
    {
        var st = new PunishStats();
        var lines = new List<string>();
        st.AddSummaryLines(lines, Rules());
        Assert.Equal(2, lines.Count);
        Assert.Contains("nothing measured", lines[1]);

        st = new PunishStats { EverLive = true };
        st.Opened(false);
        st.Opened(false);
        st.Opened(true);
        st.Seen(false);
        st.Seen(true);
        st.Punished(true);
        st.AddTime(true, true, 30);   // half a man-minute facing a reeling man
        st.AddTime(true, false, 120);
        st.AddAttack(true, true);
        st.AddAttack(true, true);
        st.AddAttack(true, false);
        st.AddTime(false, true, 60);
        st.AddAttack(false, true);
        Assert.Equal(4.0, st.Rate(true, true), 9);
        Assert.Equal(0.5, st.Rate(true, false), 9);
        Assert.Equal(1.0, st.Rate(false, true), 9);
        Assert.True(double.IsNaN(st.Rate(false, false)));
        lines.Clear();
        st.AddSummaryLines(lines, Rules());
        Assert.Equal(4, lines.Count);
        Assert.Equal("impact punish windows: opened 3 (on AI men 2, on you 1) - seen by an AI enemy in contact 2 (on you 1), PUNISHED (an AI melee attack started inside) 1 (on you 1 of 1), wasted 2 (33.3% punished)", lines[1]);
        Assert.Equal("impact punish proof: AI melee attacks per man-minute in contact (on foot) - punish ON: target reeling / down 4.00 (2 attacks / 0.50 man-min), target not 0.50 (1 attacks / 2.00 man-min); punish OFF: target reeling / down 1.00 (1 attacks / 1.00 man-min), target not - (no time)", lines[2]);
        Assert.StartsWith("impact punish men: raised to the window's share 0 times", lines[3]);
    }
}

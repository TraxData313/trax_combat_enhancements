using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 108 - READ THE BLOCK, NOT THE ARROW (Toni's training-field playtest 2026-11-10, DESIGN decision 97): the hold's clock from his VISIBLE guard
/// (the recovery and its cap), the first hold = your swing + a reading margin by your skill, x the weapon factor; the "Unbalanced ..." words in the
/// player's view; the retired first-hold key; the settings' places; the summary's new part; no allocation.
/// </summary>
public class Step108Tests
{
    private const int Up = OpeningMath.AttackUp, Down = OpeningMath.AttackDown, Left = OpeningMath.AttackLeft, Right = OpeningMath.AttackRight;
    private const int GUp = OpeningMath.GuardUp, GDown = OpeningMath.GuardDown, GLeft = OpeningMath.GuardLeft, GRight = OpeningMath.GuardRight;
    private static readonly int Sword = OpeningMath.WeaponMask(true, true), Axe = OpeningMath.WeaponMask(true, false), Spear = OpeningMath.WeaponMask(false, true);

    private static TraxSettings On()
    {
        var s = new TraxSettings();
        s.Set(SettingsSchema.OpeningsEnabled, true, SettingSources.File);
        return s;
    }

    private static OpeningHoldRules Rules() => OpeningHoldRules.From(On());

    [Fact]
    public void The_defaults_are_the_briefs_numbers()
    {
        var r = Rules();
        Assert.Equal(1.0, r.SwingSeconds, 6);
        Assert.Equal(0.15, r.MarginAt0, 6);
        Assert.Equal(0.30, r.MarginAt200, 6);
        Assert.Equal(1.0, r.RecoveryCap, 6);
        Assert.True(r.ArrowShow);
        Assert.True(r.Message);
        Assert.Equal(5000, r.InfoLines);
        Assert.Contains("until it SHOWS", r.Describe());
    }

    [Fact]
    public void The_reading_margin_follows_your_skill_clamped()
    {
        var r = Rules();
        Assert.Equal(0.15, OpeningHoldMath.ReadMargin(in r, 0), 6);
        Assert.Equal(0.225, OpeningHoldMath.ReadMargin(in r, 100), 6);
        Assert.Equal(0.30, OpeningHoldMath.ReadMargin(in r, 200), 6);
        Assert.Equal(0.30, OpeningHoldMath.ReadMargin(in r, 330), 6);       // clamped above 200
        Assert.Equal(0.15, OpeningHoldMath.ReadMargin(in r, -10), 6);
        Assert.Equal(0.15, OpeningHoldMath.ReadMargin(in r, double.NaN), 6); // unread = skill 0
    }

    [Fact]
    public void The_first_hold_is_your_swing_plus_the_margin_times_the_weapon_factor()
    {
        var r = Rules();
        Assert.Equal(1.30, OpeningHoldMath.FirstHold(in r, 95, 200), 6);                 // the reference weapon, skill 200
        Assert.Equal((1.0 + 0.165) * 95 / 109.0, OpeningHoldMath.FirstHold(in r, 109, 20), 6); // Toni's sword (speed 109, skill 20): 1.02 s
        Assert.Equal(1.15 * 1.5, OpeningHoldMath.FirstHold(in r, 40, 0), 6);              // a very slow weapon: x1.5
        var s = On();
        s.Set(SettingsSchema.OpeningHoldCapSeconds, 1.2, SettingSources.File);
        var capped = OpeningHoldRules.From(s);
        Assert.Equal(1.2, OpeningHoldMath.FirstHold(in capped, 40, 200), 6);             // capped as before
        // the shield's first holds keep their numbers
        Assert.Equal(0.5, OpeningHoldMath.ShieldFirstHold(in r, 130, 95), 6);
        Assert.Equal(1.0, OpeningHoldMath.ShieldFirstHold(in r, 60, 95), 6);
    }

    [Fact]
    public void A_fresh_hold_recovers_until_his_guard_shows_then_its_clock_starts()
    {
        var r = Rules();
        var h = OpeningHold.None;
        h.Begin(7, GUp, true, 10.0, 1.2, r.RecoveryCap);
        Assert.Equal(HoldPhase.Recovering, h.Phase);
        Assert.True(h.Recovering(7) && h.Forced(7) && !h.Holding(7));
        Assert.True(h.LinkOpen(7, 10.2));                // a block while recovering shows the guard - a link
        Assert.False(h.HoldOver(10.0 + 5));              // no clock yet
        Assert.Equal(10.0 + 1.0 + 1.2, h.Until, 6);      // the force's bound: the cap + the length
        Assert.True(double.IsNaN(h.RecoverySeconds));
        Assert.Equal(0, h.NoteWindUp(10.1, in r));       // a wind-up before the guard shows changes nothing
        Assert.False(OpeningHoldMath.RecoveryOver(in r, 10.0, 10.4, false));
        Assert.True(OpeningHoldMath.RecoveryOver(in r, 10.0, 10.4, true));
        h.GuardShown(10.4, false);
        Assert.True(h.Holding(7) && h.Forced(7));
        Assert.Equal(10.4, h.Start, 6);
        Assert.Equal(11.6, h.Until, 6);
        Assert.Equal(0.4, h.RecoverySeconds, 6);
        Assert.False(h.RecoveryCapped);
        Assert.False(h.HoldOver(11.5));
        Assert.True(h.HoldOver(11.6));
        h.GuardShown(12, true);                          // only from Recovering
        Assert.Equal(10.4, h.Start, 6);
    }

    [Fact]
    public void The_cap_starts_the_clock_when_the_guard_never_shows_and_cap_0_is_step_107()
    {
        var r = Rules();
        Assert.True(OpeningHoldMath.RecoveryOver(in r, 10.0, 11.0, false));
        var h = OpeningHold.None;
        h.Begin(3, GDown, true, 10.0, 1.0, r.RecoveryCap);
        h.GuardShown(11.0, true);
        Assert.True(h.RecoveryCapped);
        Assert.Equal(1.0, h.RecoverySeconds, 6);
        Assert.Equal(12.0, h.Until, 6);
        var old = OpeningHold.None;
        old.Begin(3, GDown, true, 10.0, 1.0, 0);
        Assert.True(old.Holding(3));
        Assert.Equal(11.0, old.Until, 6);
        Assert.Equal(0, old.RecoverySeconds, 6);
    }

    [Fact]
    public void The_words_name_his_guard_as_you_see_it_and_the_strike_from_the_arrows_side()
    {
        // a sword, sides crossed (Toni's setting - the engine's DefendLeft blocks your RIGHT swing): the arrow / the strike = the opening
        Assert.Equal("Unbalanced DOWN (hit from top down)", OpeningHoldMath.UnbalancedText(GDown, Sword, true));
        Assert.Equal("Unbalanced UP (hit from below)", OpeningHoldMath.UnbalancedText(GUp, Sword, true));
        Assert.Equal(Left, OpeningMath.OpeningFor(GLeft, true));
        Assert.Equal("Unbalanced RIGHT (hit from the left)", OpeningHoldMath.UnbalancedText(GLeft, Sword, true));
        Assert.Equal("Unbalanced LEFT (hit from the right)", OpeningHoldMath.UnbalancedText(GRight, Sword, true));
        // same-named sides: the engine name changes, the words follow the opening - always the arrow's side
        Assert.Equal(Right, OpeningMath.OpeningFor(GLeft, false));
        Assert.Equal("Unbalanced LEFT (hit from the right)", OpeningHoldMath.UnbalancedText(GLeft, Sword, false));
        // the arrow sits where the strike comes from
        foreach (int g in new[] { GUp, GDown, GLeft, GRight })
        {
            int o = OpeningMath.OpeningFor(g, true);
            string arrow = OpeningMath.ArrowName(OpeningMath.ArrowFor(o));
            string text = OpeningHoldMath.UnbalancedText(g, Sword, true)!;
            Assert.Contains(arrow == "top" ? "from top down" : arrow == "bottom" ? "from below" : "from the " + arrow, text);
        }
        // an axe / a mace wins on any side but his guard
        Assert.Equal("Unbalanced - guard LOW, hit any other side", OpeningHoldMath.UnbalancedText(GDown, Axe, true));
        Assert.Equal("Unbalanced - guard RIGHT, hit any other side", OpeningHoldMath.UnbalancedText(GLeft, Axe, true)); // covers your right swing
        // a spear: two stabs - the one his guard does not block
        Assert.Equal("Unbalanced UP (hit from below)", OpeningHoldMath.UnbalancedText(GUp, Spear, true));
        Assert.Equal("Unbalanced DOWN (hit from top down)", OpeningHoldMath.UnbalancedText(GDown, Spear, true));
        Assert.Null(OpeningHoldMath.UnbalancedText(Up, Sword, true));
        Assert.Same(OpeningHoldMath.UnbalancedText(GDown, Sword, true), OpeningHoldMath.UnbalancedText(GDown, Sword, true)); // constants
    }

    [Fact]
    public void The_old_first_hold_key_is_dropped_with_a_note()
    {
        Assert.DoesNotContain("OpeningHoldFirstSeconds", DefaultsFile.ReadEmbeddedText(), StringComparison.Ordinal);
        Assert.False(SettingsSchema.TryGet("OpeningHoldFirstSeconds", out _));
        var read = ConfigFile.Read("{ \"ConfigVersion\": 30, \"OpeningHoldFirstSeconds\": 1.5 }");
        var notes = ConfigFile.Migrate(read, p => p.Default);
        Assert.Contains(notes, n => n.StartsWith("OpeningHoldFirstSeconds 1.5 dropped (format 30 → 30; step 108", StringComparison.Ordinal));
        Assert.DoesNotContain(read.Unknown, u => u.Key == "OpeningHoldFirstSeconds");
        Assert.Equal(30, ConfigFile.FormatVersion);
    }

    [Fact]
    public void The_settings_sit_in_the_impact_group()
    {
        var all = SettingsSchema.All.ToList();
        int at = all.IndexOf(SettingsSchema.OpeningHoldsEnabled);
        Assert.Equal(at + 1, all.IndexOf(SettingsSchema.OpeningHoldSwingSeconds));
        Assert.Equal(at + 2, all.IndexOf(SettingsSchema.OpeningHoldReadMarginAtSkill0));
        Assert.Equal(at + 3, all.IndexOf(SettingsSchema.OpeningHoldReadMarginAtSkill200));
        Assert.Equal(at + 4, all.IndexOf(SettingsSchema.OpeningHoldRecoveryCapSeconds));
        int d = all.IndexOf(SettingsSchema.PerfectStrikeDefensiveMax);
        Assert.Equal(d + 1, all.IndexOf(SettingsSchema.OpeningHoldArrowShow));
        Assert.Equal(d + 2, all.IndexOf(SettingsSchema.OpeningHoldMessage));
        Assert.Equal(d + 3, all.IndexOf(SettingsSchema.OpeningLogInfoLines));
        foreach (var p in new[] { SettingsSchema.OpeningHoldSwingSeconds, SettingsSchema.OpeningHoldReadMarginAtSkill0, SettingsSchema.OpeningHoldReadMarginAtSkill200,
                                  SettingsSchema.OpeningHoldRecoveryCapSeconds, SettingsSchema.OpeningHoldArrowShow, SettingsSchema.OpeningHoldMessage, SettingsSchema.OpeningLogInfoLines })
        {
            Assert.Same(SettingsSchema.ImpactGroup, p.Group);
            Assert.Equal(SettingsPage.Advanced, p.Group.Page);
        }
    }

    [Fact]
    public void Live_changes_are_read_at_use()
    {
        var s = On();
        s.Set(SettingsSchema.OpeningHoldArrowShow, false, SettingSources.Mcm);
        s.Set(SettingsSchema.OpeningHoldMessage, false, SettingSources.Mcm);
        s.Set(SettingsSchema.OpeningHoldRecoveryCapSeconds, 0, SettingSources.Mcm);
        var r = OpeningHoldRules.From(s);
        Assert.False(r.ArrowShow);
        Assert.False(r.Message);
        Assert.Equal(0, r.RecoveryCap, 6);
        Assert.Contains("OFF (OpeningHoldArrowShow)", r.Describe());
        s.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm);
        Assert.False(OpeningHoldRules.From(s).On); // the master switch first
    }

    [Fact]
    public void The_summary_line_tells_the_recovery_and_his_blows_by_phase()
    {
        var st = new OpeningHoldStats { Recoveries = 5, RecoveryCut = 1, MessagesShown = 4, HitYouRecovering = 1, HitYouHolding = 0, HitYouAfterCounter = 2, HitYouAfterOwn = 3, ArrowOff = 1 };
        st.NoteRecovery(0.3, false);
        st.NoteRecovery(0.5, false);
        st.NoteRecovery(1.0, true);
        Assert.Equal(2, st.RecoveredSeen);
        Assert.Equal(1, st.RecoveryCapped);
        string line = st.SummaryLine(Rules());
        Assert.Contains("STEP 108 - THE RECOVERY (hit -> his guard visible, the hold's clock from then): waited 5, guard SEEN 2 (mean 0.60 s, longest 1.00 s), started by the cap 1 (1.0 s), cut before it showed 1; 'Unbalanced ...' shown 4", line);
        Assert.Contains("HIS BLOWS ON YOU in a chain: while recovering 1, while holding 0, after the hold - his forced counter-swing 2, his own attack 3", line);
        Assert.Contains("off by its switch 1", line);
    }

    [Fact]
    public void The_recovery_and_the_words_allocate_nothing()
    {
        var r = Rules();
        var h = OpeningHold.None;
        var stats = new OpeningHoldStats();
        double sink = 0;
        long bytes = NoAllocation.Bytes(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                h.Begin(1, GLeft + (i & 1), true, i, OpeningHoldMath.FirstHold(in r, 90, i), r.RecoveryCap);
                if (OpeningHoldMath.RecoveryOver(in r, h.HitAt, i + 0.3, (i & 2) == 0)) h.GuardShown(i + 0.3, false);
                stats.NoteRecovery(h.RecoverySeconds, false);
                sink += h.Until + OpeningHoldMath.ReadMargin(in r, i) + (OpeningHoldMath.UnbalancedText(h.Guard, Sword, true)?.Length ?? 0);
            }
        });
        Assert.Equal(0, bytes);
        Assert.True(sink > 0);
    }
}

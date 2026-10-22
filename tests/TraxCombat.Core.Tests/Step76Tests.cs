using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 76 (Toni 2026-10-22: "my shield wall-ers have spears but they keep using their swords, even though the spear has a longer reach"; AI_NOTES
/// "Step 76", DESIGN decision 70) - THE AI PREFERS THE SPEAR + A THRUST PASSES A FRIEND: the settings (one on the player page, twenty Advanced), the
/// loadout classification (RBM's), RBM's two timers (the hysteresis), the close radius from the spear's length, the back-rank rule, the facing-a-
/// polearm rule, the favors, the wield fallback's bits, the collision decision, the stun, the summary lines.
/// </summary>
public class Step76Tests
{
    private static readonly ParamDef[] Advanced =
    {
        SettingsSchema.SpearPolearmFavor, SettingsSchema.SpearSidearmFavor, SettingsSchema.SpearSidearmAfterSeconds, SettingsSchema.SpearPolearmBackAfterSeconds,
        SettingsSchema.SpearPolearmBackMountedSeconds, SettingsSchema.SpearRidingFastMetresPerSecond, SettingsSchema.SpearCloseFromLength, SettingsSchema.SpearCloseBaseMetres,
        SettingsSchema.SpearCloseLengthPercent, SettingsSchema.SpearCloseMinMetres, SettingsSchema.SpearCloseMaxMetres, SettingsSchema.SpearCloseFallbackMetres,
        SettingsSchema.SpearBackRankKeeps, SettingsSchema.SpearBackRankInStandingLine, SettingsSchema.SpearKeepVsPolearm, SettingsSchema.SpearLookSeconds,
        SettingsSchema.SpearWieldOverride, SettingsSchema.ThrustsPassFriends, SettingsSchema.ThrustsPassPolearmsOnly, SettingsSchema.FriendlyHitStunSeconds,
    };

    private static SpearRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return SpearRules.From(s);
    }

    private static ThrustPassRules Thrust(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return ThrustPassRules.From(s);
    }

    private static SpearSlot Spear(int cm = 200) => new SpearSlot { Melee = true, Polearm = true, LengthCm = cm };
    private static SpearSlot Sword() => new SpearSlot { Melee = true, LengthCm = 0 };
    private static SpearSlot Javelin() => new SpearSlot { Melee = true, Polearm = true, Throwable = true, LengthCm = 120 };
    private static SpearSlot Shield() => new SpearSlot { Melee = false };
    private static SpearSlot Bow() => new SpearSlot { Throwable = true };
    private static SpearSlot Empty() => new SpearSlot { Empty = true };

    private static SpearContext Wall(int rank, bool inFront = true, BraceOrderKind order = BraceOrderKind.Stop, SpearArrangement arr = SpearArrangement.Wall, bool firm = false)
        => new SpearContext { Arrangement = arr, Order = order, Rank = rank, ManInFront = inFront, StandsFirm = firm };

    [Fact]
    public void The_switch_is_on_the_player_page_after_stand_firm_and_twenty_numbers_on_Advanced_after_the_stand_firm_ones()
    {
        var all = SettingsSchema.All.ToList();
        var sw = SettingsSchema.SpearPreferenceEnabled;
        Assert.Equal(all.IndexOf(SettingsSchema.ShieldWallStandFirm) + 1, all.IndexOf(sw));
        Assert.Contains(sw, SettingsSchema.PlayerPage);
        Assert.Equal(SettingsPage.Player, sw.Group.Page);
        Assert.Equal(ParamType.Bool, sw.Type);
        Assert.Equal(ApplyTiming.Live, sw.Timing);
        Assert.Equal(1, sw.Default, 9);
        Assert.DoesNotMatch(@"[Ss]tep \d", sw.Description);
        Assert.DoesNotContain("RBM", sw.Description);
        int first = all.IndexOf(SettingsSchema.ShieldWallStandFirmTightTolerance) + 1;
        for (int i = 0; i < Advanced.Length; i++)
        {
            Assert.Equal(first + i, all.IndexOf(Advanced[i]));
            Assert.Same(SettingsSchema.ShieldWallStandFirmTightTolerance.Group, Advanced[i].Group);
            Assert.Equal(SettingsPage.Advanced, Advanced[i].Group.Page);
            Assert.Equal(ApplyTiming.Live, Advanced[i].Timing);
        }
        var r = Rules();
        Assert.True(r.On);
        Assert.Equal(35, r.PolearmFavor, 6);
        Assert.Equal(55, r.SidearmFavor, 6);
        Assert.Equal(2, r.SidearmAfterSeconds, 6);
        Assert.Equal(4, r.PolearmBackAfterSeconds, 6);
        Assert.Equal(2, r.PolearmBackMountedSeconds, 6);
        Assert.Equal(4, r.RidingFastMetresPerSecond, 6);
        Assert.True(r.CloseFromLength && r.BackRankKeeps && r.BackRankInStandingLine && r.KeepVsPolearm);
        Assert.False(r.WieldOverride); // the fallback is a test switch, off
        Assert.Equal(0.5, r.LookSeconds, 6);
        var t = Thrust();
        Assert.True(t.On);
        Assert.False(t.PolearmsOnly);
        Assert.Equal(0.1, t.StunSeconds, 6);
    }

    [Fact]
    public void The_master_switch_comes_first_for_both_rules()
    {
        var mod = Rules(s => s.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm));
        Assert.False(mod.On);
        Assert.Equal("ModEnabled", mod.OffBecause);
        var off = Rules(s => s.Set(SettingsSchema.SpearPreferenceEnabled, false, SettingSources.Mcm));
        Assert.False(off.On);
        Assert.Equal("SpearPreferenceEnabled", off.OffBecause);
        Assert.False(Rules(s => s.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm)).WieldOn);
        Assert.True(Rules(s => s.Set(SettingsSchema.SpearWieldOverride, true, SettingSources.Mcm)).WieldOn);

        var f = new ThrustFacts { HasDefender = true, Friend = true, Thrust = true, Polearm = true };
        var tm = Thrust(s => { s.Set(SettingsSchema.ModEnabled, false, SettingSources.Mcm); s.Set(SettingsSchema.ThrustsPassFriends, false, SettingSources.Mcm); });
        Assert.Equal(ThrustVerdict.ModOff, ThrustPassMath.Decide(in tm, in f)); // the master switch before the switch
        var ts = Thrust(s => s.Set(SettingsSchema.ThrustsPassFriends, false, SettingSources.Mcm));
        Assert.Equal(ThrustVerdict.SwitchedOff, ThrustPassMath.Decide(in ts, in f));
        Assert.True(double.IsNaN(ThrustPassMath.StunWanted(in ts))); // off = the game's own stun back
        Assert.True(double.IsNaN(ThrustPassMath.StunWanted(in tm)));
        Assert.Equal(0.1, ThrustPassMath.StunWanted(Thrust()), 6);
    }

    [Fact]
    public void The_loadout_needs_a_melee_polearm_and_a_melee_sidearm_thrown_weapons_are_neither()
    {
        var l = SpearMath.Classify(new[] { Spear(210), Sword(), Shield(), Empty() });
        Assert.True(l.Both);
        Assert.Equal(0, l.PolearmSlot);
        Assert.Equal(210, l.LengthCm);
        Assert.Equal(1, l.PolearmMask);
        Assert.Equal(2, l.SidearmMask);

        Assert.False(SpearMath.Classify(new[] { Spear(), Shield(), Empty(), Empty() }).Both);     // a spear alone - nothing to switch to
        Assert.False(SpearMath.Classify(new[] { Sword(), Shield(), Javelin(), Empty() }).Both);   // javelins are not a spear
        Assert.False(SpearMath.Classify(new[] { Javelin(), Sword(), Bow(), Empty() }).Both);
        Assert.False(SpearMath.Classify(new[] { Empty(), Empty(), Empty(), Empty() }).Both);

        // the longest polearm is his: a short spear in slot 0, a pike in slot 2
        var two = SpearMath.Classify(new[] { Spear(150), Sword(), Spear(300), Javelin() });
        Assert.True(two.Both);
        Assert.Equal(2, two.PolearmSlot);
        Assert.Equal(300, two.LengthCm);
        Assert.Equal(1 | 4, two.PolearmMask);
        Assert.Equal(0, SpearMath.HeldOf(-1, in two) == SpearHeld.Other ? 0 : 1);
        Assert.Equal(SpearHeld.Polearm, SpearMath.HeldOf(0, in two));
        Assert.Equal(SpearHeld.Sidearm, SpearMath.HeldOf(1, in two));
        Assert.Equal(SpearHeld.Polearm, SpearMath.HeldOf(2, in two));
        Assert.Equal(SpearHeld.Other, SpearMath.HeldOf(3, in two)); // the javelins
    }

    [Fact]
    public void The_close_radius_follows_the_spears_length_clamped_and_falls_back_to_RBMs()
    {
        var r = Rules();
        Assert.Equal(1.9, SpearMath.CloseRadius(in r, 150), 6);
        Assert.Equal(2.2, SpearMath.CloseRadius(in r, 200), 6);
        Assert.Equal(2.5, SpearMath.CloseRadius(in r, 250), 6);
        Assert.Equal(2.8, SpearMath.CloseRadius(in r, 300), 6);
        Assert.Equal(1.5, SpearMath.CloseRadius(in r, 40), 6);   // a very short one - the minimum
        Assert.Equal(3.5, SpearMath.CloseRadius(in r, 500), 6);  // a giant pike - the maximum
        Assert.Equal(2.5, SpearMath.CloseRadius(in r, 0), 6);    // a length not known - RBM's 2.5
        var off = Rules(s => s.Set(SettingsSchema.SpearCloseFromLength, false, SettingSources.Mcm));
        Assert.Equal(2.5, SpearMath.CloseRadius(in off, 300), 6);
        var tuned = Rules(s => { s.Set(SettingsSchema.SpearCloseMinMetres, 3.0, SettingSources.Mcm); s.Set(SettingsSchema.SpearCloseMaxMetres, 2.0, SettingSources.Mcm); });
        Assert.Equal(3.0, tuned.CloseMaxMetres, 6); // the maximum never below the minimum
        Assert.Equal(3.0, SpearMath.CloseRadius(in tuned, 200), 6);
        Assert.Contains("2.0 m 2.20", r.Describe());
    }

    [Fact]
    public void RBMs_two_timers_the_sidearm_after_2_s_close_the_polearm_4_s_after_the_last_close_enemy_2_s_mounted()
    {
        var r = Rules();
        var m = SpearMind.Fresh;
        // an enemy close at 0, 0.5, 1.0, 1.5: not yet
        for (double t = 0; t < 1.9; t += 0.5)
            Assert.Equal(SpearFlip.None, SpearMath.Step(in r, ref m, t, SpearCondition.Close, false));
        Assert.False(m.Close);
        Assert.Equal(SpearFlip.ToSidearm, SpearMath.Step(in r, ref m, 2.0, SpearCondition.Close, false));
        Assert.True(m.Close);
        // a passing enemy: one close look breaks the spell
        var p = SpearMind.Fresh;
        SpearMath.Step(in r, ref p, 0, SpearCondition.Close, false);
        SpearMath.Step(in r, ref p, 1, SpearCondition.NotClose, false);
        Assert.Equal(SpearFlip.None, SpearMath.Step(in r, ref p, 2, SpearCondition.Close, false));
        Assert.Equal(SpearFlip.None, SpearMath.Step(in r, ref p, 3.5, SpearCondition.Close, false));
        Assert.Equal(SpearFlip.ToSidearm, SpearMath.Step(in r, ref p, 4.0, SpearCondition.Close, false));
        // the sidearm out: back to the polearm 4 s after the LAST close look (the last at 2.0)
        Assert.Equal(SpearFlip.None, SpearMath.Step(in r, ref m, 3.0, SpearCondition.NotClose, false));
        Assert.Equal(SpearFlip.None, SpearMath.Step(in r, ref m, 5.5, SpearCondition.NotClose, false));
        Assert.Equal(SpearFlip.ToPolearm, SpearMath.Step(in r, ref m, 6.0, SpearCondition.NotClose, false));
        Assert.False(m.Close);
        // an enemy drifting in and out keeps the sidearm out (each close look renews the 4 s)
        var d = SpearMind.Fresh;
        for (double t = 0; t <= 2.0; t += 0.5) SpearMath.Step(in r, ref d, t, SpearCondition.Close, false);
        Assert.True(d.Close);
        for (double t = 2.5; t < 12; t += 0.5)
            Assert.Equal(SpearFlip.None, SpearMath.Step(in r, ref d, t, ((int)(t * 2) % 6 == 0) ? SpearCondition.Close : SpearCondition.NotClose, false));
        // mounted: 2 s
        var h = SpearMind.Fresh;
        for (double t = 0; t <= 2.0; t += 0.5) SpearMath.Step(in r, ref h, t, SpearCondition.Close, true);
        Assert.True(h.Close);
        Assert.Equal(SpearFlip.None, SpearMath.Step(in r, ref h, 3.5, SpearCondition.Riding, true));
        Assert.Equal(SpearFlip.ToPolearm, SpearMath.Step(in r, ref h, 4.0, SpearCondition.Riding, true));
        // facing only polearms counts as not close
        var f = SpearMind.Fresh;
        for (double t = 0; t <= 5; t += 0.5) Assert.Equal(SpearFlip.None, SpearMath.Step(in r, ref f, t, SpearCondition.FacingPolearm, false));
        // the delays live
        var quick = Rules(s => s.Set(SettingsSchema.SpearSidearmAfterSeconds, 0.0, SettingSources.Mcm));
        var q = SpearMind.Fresh;
        Assert.Equal(SpearFlip.ToSidearm, SpearMath.Step(in quick, ref q, 0, SpearCondition.Close, false));
    }

    [Fact]
    public void A_back_rank_man_of_a_wall_or_a_halted_line_keeps_the_spear_at_once()
    {
        var r = Rules();
        Assert.True(SpearMath.BackRank(in r, Wall(1)));
        Assert.True(SpearMath.BackRank(in r, Wall(3)));
        Assert.False(SpearMath.BackRank(in r, Wall(0)));                     // the front rank fights with what is best up close
        Assert.False(SpearMath.BackRank(in r, Wall(1, inFront: false)));     // a thinned file - nobody in front of him: he IS the front
        Assert.False(SpearMath.BackRank(in r, Wall(-1)));                    // not known
        Assert.True(SpearMath.BackRank(in r, Wall(1, order: BraceOrderKind.Move)));
        Assert.True(SpearMath.BackRank(in r, Wall(1, order: BraceOrderKind.Advance)));
        Assert.False(SpearMath.BackRank(in r, Wall(1, order: BraceOrderKind.Charge)));   // a charge lets the wall go
        Assert.False(SpearMath.BackRank(in r, Wall(1, order: BraceOrderKind.Retreat)));
        // a plain line: only halted (a stop) or a man standing firm
        Assert.True(SpearMath.BackRank(in r, Wall(1, arr: SpearArrangement.Line)));
        Assert.False(SpearMath.BackRank(in r, Wall(1, arr: SpearArrangement.Line, order: BraceOrderKind.Advance)));
        Assert.True(SpearMath.BackRank(in r, Wall(1, arr: SpearArrangement.Line, order: BraceOrderKind.Move, firm: true)));
        Assert.False(SpearMath.BackRank(in r, Wall(1, arr: SpearArrangement.Other)));
        var noLine = Rules(s => s.Set(SettingsSchema.SpearBackRankInStandingLine, false, SettingSources.Mcm));
        Assert.False(SpearMath.BackRank(in noLine, Wall(1, arr: SpearArrangement.Line)));
        Assert.True(SpearMath.BackRank(in noLine, Wall(1)));
        var off = Rules(s => s.Set(SettingsSchema.SpearBackRankKeeps, false, SettingSources.Mcm));
        Assert.False(SpearMath.BackRank(in off, Wall(2)));

        // the condition: the back rank wins over a close enemy; riding fast over everything
        Assert.Equal(SpearCondition.BackRank, SpearMath.Condition(in r, false, true, 3, 0));
        Assert.Equal(SpearCondition.Riding, SpearMath.Condition(in r, true, true, 3, 0));
        // a man who had drawn his sidearm and steps into a back rank: the polearm at once, the spell cleared
        var m = SpearMind.Fresh;
        for (double t = 0; t <= 2; t += 0.5) SpearMath.Step(in r, ref m, t, SpearCondition.Close, false);
        Assert.True(m.Close);
        Assert.Equal(SpearFlip.ToPolearmBackRank, SpearMath.Step(in r, ref m, 2.5, SpearCondition.BackRank, false));
        Assert.False(m.Close);
        Assert.True(double.IsNaN(m.LastClose));
        Assert.Equal(SpearFlip.None, SpearMath.Step(in r, ref m, 3, SpearCondition.BackRank, false));
        // back in the front rank: a fresh 2 s spell before the sidearm
        Assert.Equal(SpearFlip.None, SpearMath.Step(in r, ref m, 3.5, SpearCondition.Close, false));
        Assert.Equal(SpearFlip.ToSidearm, SpearMath.Step(in r, ref m, 5.5, SpearCondition.Close, false));
    }

    [Fact]
    public void A_man_facing_only_polearms_keeps_his_one_other_enemy_close_counts()
    {
        var r = Rules();
        Assert.Equal(SpearCondition.FacingPolearm, SpearMath.Condition(in r, false, false, 0, 2));
        Assert.Equal(SpearCondition.Close, SpearMath.Condition(in r, false, false, 1, 2));   // a swordsman among them
        Assert.Equal(SpearCondition.NotClose, SpearMath.Condition(in r, false, false, 0, 0));
        var off = Rules(s => s.Set(SettingsSchema.SpearKeepVsPolearm, false, SettingSources.Mcm));
        Assert.Equal(SpearCondition.Close, SpearMath.Condition(in off, false, false, 0, 2)); // RBM: every close enemy
    }

    [Fact]
    public void The_favors_RBMs_numbers_and_the_riding_gate()
    {
        var r = Rules();
        SpearMath.Favors(in r, false, out double p, out double m);
        Assert.Equal(35, p, 6);
        Assert.Equal(1, m, 6);
        SpearMath.Favors(in r, true, out p, out m);
        Assert.Equal(1, p, 6);
        Assert.Equal(55, m, 6);
        var tuned = Rules(s => { s.Set(SettingsSchema.SpearPolearmFavor, 10.0, SettingSources.Mcm); s.Set(SettingsSchema.SpearSidearmFavor, 20.0, SettingSources.Mcm); });
        Assert.False(r.SameFavors(in tuned));
        SpearMath.Favors(in tuned, false, out p, out _);
        Assert.Equal(10, p, 6);
        Assert.True(SpearMath.RidingFast(in r, new SpearContext { Mounted = true, MountSpeed = 5 }));
        Assert.False(SpearMath.RidingFast(in r, new SpearContext { Mounted = true, MountSpeed = 3.9 }));
        Assert.False(SpearMath.RidingFast(in r, new SpearContext { Mounted = false, MountSpeed = 9 }));
        // the looks: jittered 0.75-1.25 x, deterministic
        for (int i = 0; i < 200; i++)
        {
            double n = SpearMath.NextLook(in r, 10, i, i * 7);
            Assert.InRange(n - 10, 0.375 - 1e-9, 0.625 + 1e-9);
            Assert.Equal(n, SpearMath.NextLook(in r, 10, i, i * 7));
        }
    }

    [Fact]
    public void The_wield_fallback_turns_a_sidearm_wield_into_the_polearms_and_leaves_everything_else()
    {
        const uint wield1 = 0x20u, wield0 = 0x10u, wield2 = 0x40u, kick = 0x8000u;
        uint ev = wield1 | kick;
        Assert.True(SpearMath.WieldRewrite(ref ev, 0, 1 << 1));
        Assert.Equal(wield0 | kick, ev);
        ev = wield2 | kick; // a wield of the shield / another slot - not a sidearm: left
        Assert.False(SpearMath.WieldRewrite(ref ev, 0, 1 << 1));
        Assert.Equal(wield2 | kick, ev);
        ev = kick;
        Assert.False(SpearMath.WieldRewrite(ref ev, 0, 1 << 1));
        ev = wield1;
        Assert.False(SpearMath.WieldRewrite(ref ev, -1, 1 << 1));
        ev = wield1;
        Assert.True(SpearMath.WieldRewrite(ref ev, 3, 1 << 1));
        Assert.Equal(0x80u, ev);
    }

    [Fact]
    public void A_thrust_into_a_friend_carries_on_a_swing_kick_or_bash_still_bounces_an_enemy_is_never_touched()
    {
        var r = Thrust();
        var thrust = new ThrustFacts { HasDefender = true, Friend = true, Thrust = true };
        Assert.Equal(ThrustVerdict.Passed, ThrustPassMath.Decide(in r, in thrust));
        var swing = thrust;
        swing.Thrust = false;
        Assert.Equal(ThrustVerdict.Swing, ThrustPassMath.Decide(in r, in swing));
        var kick = thrust;
        kick.Alternative = true;
        Assert.Equal(ThrustVerdict.KickOrBash, ThrustPassMath.Decide(in r, in kick));
        var enemy = thrust;
        enemy.Friend = false;
        Assert.Equal(ThrustVerdict.NotFriend, ThrustPassMath.Decide(in r, in enemy));
        var none = thrust;
        none.HasDefender = false;
        Assert.Equal(ThrustVerdict.NotFriend, ThrustPassMath.Decide(in r, in none));
        // polearms only: a sword's thrust bounces, a spear's passes
        var po = Thrust(s => s.Set(SettingsSchema.ThrustsPassPolearmsOnly, true, SettingSources.Mcm));
        Assert.Equal(ThrustVerdict.NotPolearm, ThrustPassMath.Decide(in po, in thrust));
        var spear = thrust;
        spear.Polearm = true;
        Assert.Equal(ThrustVerdict.Passed, ThrustPassMath.Decide(in po, in spear));
        Assert.Equal(1, ThrustPassMath.ThrustStrikeType);
        Assert.Equal(0.4, ThrustPassMath.VanillaStunSeconds, 6);
        Assert.Contains("0.40 s → 0.10 s", r.Describe());
    }

    [Fact]
    public void The_summary_lines_say_what_the_battle_did()
    {
        var r = Rules();
        var s = new SpearStats { EverOn = true, MenBoth = 12, MenWithout = 30, LoadoutReads = 42 };
        s.AddRadius(2.2, true);
        s.AddTime(true, SpearCondition.BackRank, SpearHeld.Polearm, 90);
        s.AddTime(true, SpearCondition.Close, SpearHeld.Sidearm, 30);
        s.AddTime(true, SpearCondition.Close, SpearHeld.Polearm, 10);
        s.AddTime(false, SpearCondition.Close, SpearHeld.Sidearm, 40);
        s.Flip(SpearFlip.ToSidearm);
        s.BackRankAttacks = 6;
        s.BackRankAttacksPolearm = 6;
        var lines = s.SummaryLines(in r);
        Assert.Equal(2, lines.Length);
        Assert.Contains("12 AI men carry a melee polearm AND a sidearm", lines[0]);
        Assert.Contains("a back rank 90 s: polearm 100%", lines[0]);
        Assert.Contains("an enemy close 40 s: polearm 25%, sidearm 75%", lines[0]);
        Assert.Contains("OFF 40 man-s", lines[0]);
        Assert.Contains("to the sidearm 1", lines[1]);
        Assert.Contains("by back-rank men 6 (with the polearm 6, 4.0 a man-minute", lines[1]);
        Assert.Contains("no AI man was looked at", new SpearStats().SummaryLines(in r)[0]);

        var t = new ThrustPassStats { EverOn = true, FollowedAny = 3, FollowedLanded = 2, FollowedBlocked = 1, StunSet = 0.1, StunWrites = 1 };
        for (int i = 0; i < 4; i++) t.Add(ThrustVerdict.Passed);
        t.Add(ThrustVerdict.Swing);
        t.Add(ThrustVerdict.NotFriend);
        Assert.Equal(5, t.FriendlyContacts());
        string line = t.SummaryLine(Thrust());
        Assert.Contains("friendly melee contacts 5", line);
        Assert.Contains("a thrust passed 4", line);
        Assert.Contains("a swing (bounced) 1", line);
        Assert.Contains("within 0.5 s: 3 of 4 (75%", line);
        Assert.Contains("0.10 s (the game's 0.40 s", line);
    }
}

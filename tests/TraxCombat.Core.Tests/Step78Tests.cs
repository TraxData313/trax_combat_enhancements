using System;
using System.Collections.Generic;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 78 (Toni 2026-10-22, sketch 1-4: two packs that won't be reckless and won't back down; AI_NOTES "Step 78", DESIGN decision 72) - THE BATTLE'S
/// RHYTHM's ROTATION: the settings (its switch on the player page, eleven Advanced), the master switch first then Athletics then each part, tired /
/// fresher / right behind / covered / beside, the busy actions, the unit table (fronts, front bars, the enemy unit - a held wall paired by its attackers'
/// votes), the summary line. Step 78c removed the lull and its tests (Step78cTests holds the rest's).
/// </summary>
public class Step78Tests
{
    private static readonly ParamDef[] Player = { SettingsSchema.RotationEnabled, SettingsSchema.RestEnabled };

    private static readonly ParamDef[] Advanced =
    {
        SettingsSchema.RotationTiredPercent, SettingsSchema.RotationUseBraceFloor, SettingsSchema.RotationFresherPercent, SettingsSchema.RotationBehindMetres,
        SettingsSchema.RotationFileMetres, SettingsSchema.RotationContactMetres, SettingsSchema.RotationBesideMetres, SettingsSchema.RotationApproachSeconds,
        SettingsSchema.RotationMaxSeconds, SettingsSchema.RotationCooldownSeconds, SettingsSchema.RotationRestSeconds,
    };

    private static RhythmRules Rules(bool rotation = true, bool rest = true, bool mod = true, bool athletics = true) =>
        new RhythmRules(mod, athletics, rotation, rest);

    [Fact]
    public void The_switches_are_on_the_player_page_after_the_press_and_the_numbers_on_Advanced_after_it()
    {
        var all = SettingsSchema.All.ToList();
        int p = all.IndexOf(SettingsSchema.PressDontPass);
        for (int i = 0; i < Player.Length; i++)
        {
            Assert.Equal(p + 1 + i, all.IndexOf(Player[i]));
            Assert.Contains(Player[i], SettingsSchema.PlayerPage);
            Assert.Equal(1, Player[i].Default, 9);
            Assert.DoesNotMatch(@"[Ss]tep \d", Player[i].Description);
        }
        int first = all.IndexOf(SettingsSchema.PressWingBendDegrees) + 1; // step 78b: the wrap's switch and the wing's bend after the press's slack
        for (int i = 0; i < Advanced.Length; i++)
        {
            Assert.Equal(first + i, all.IndexOf(Advanced[i]));
            Assert.Same(SettingsSchema.CourageGroup, Advanced[i].Group);
            Assert.Equal(ApplyTiming.Live, Advanced[i].Timing);
        }
        var r = RhythmRules.From(new TraxSettings());
        Assert.Equal(35, r.TiredPercent);
        Assert.Equal(25, r.FresherPercent);
        Assert.True(r.RestEnabled);
    }

    [Fact]
    public void The_master_switch_first_then_Athletics_then_each_part()
    {
        Assert.True(Rules().RotationOn && Rules().RestOn);
        Assert.Equal("ModEnabled", Rules(mod: false).RotationOffBecause);
        Assert.False(Rules(mod: false).RestOn);
        Assert.False(Rules(athletics: false).RestOn);
        Assert.Equal("AthleticsEnabled", Rules(athletics: false).RotationOffBecause);
        Assert.Equal("RotationEnabled", Rules(rotation: false).RotationOffBecause);
        Assert.True(Rules(rotation: false).RestOn && Rules(rotation: false).AnyOn);
        Assert.False(Rules(rotation: false, rest: false).AnyOn);
        Assert.Contains("OFF (RotationEnabled)", Rules(rotation: false).Describe());
        Assert.Contains("SwitchUnitLocations", Rules().Describe());
        Assert.DoesNotContain("LULL", Rules().Describe());
    }

    [Fact]
    public void Tired_at_the_setting_or_his_brace_floor_and_fresher_by_the_margin()
    {
        var r = Rules();
        Assert.Equal(0.35, RhythmMath.TiredBar(in r, double.NaN), 9);
        Assert.Equal(0.60, RhythmMath.TiredBar(in r, 0.60), 9);          // a hold's floor: tired sooner
        Assert.Equal(0.35, RhythmMath.TiredBar(in r, 0.20), 9);          // a charge's floor: the setting
        Assert.True(RhythmMath.IsTired(in r, 0.35, double.NaN));
        Assert.False(RhythmMath.IsTired(in r, 0.36, double.NaN));
        Assert.True(RhythmMath.IsTired(in r, 0.55, 0.60));
        var noFloor = new RhythmRules(true, true, true, true, useBraceFloor: false);
        Assert.False(RhythmMath.IsTired(in noFloor, 0.55, 0.60));
        Assert.True(RhythmMath.Fresher(in r, 0.25, 0.50, 0.35));
        Assert.False(RhythmMath.Fresher(in r, 0.25, 0.49, 0.35));        // not by 25 points
        Assert.False(RhythmMath.Fresher(in r, 0.10, 0.35, 0.35));        // fresher, but tired himself
    }

    [Fact]
    public void Right_behind_in_his_file_covered_and_beside()
    {
        var r = Rules();
        // facing north (u = (0, 1)): a man at (0, -1) is right behind, (0.9, -1) still in the file, (1.2, -1) not, (0, -0.2) beside, (0, -2) too far
        RhythmMath.AlongAcross(0, 0, 0, 1, 0, -1, out double a, out double c);
        Assert.Equal(-1, a, 9);
        Assert.Equal(0, c, 9);
        Assert.True(RhythmMath.Behind(in r, a, c));
        RhythmMath.AlongAcross(0, 0, 0, 1, 0.9, -1, out a, out c);
        Assert.True(RhythmMath.Behind(in r, a, c));
        RhythmMath.AlongAcross(0, 0, 0, 1, 1.2, -1, out a, out c);
        Assert.False(RhythmMath.Behind(in r, a, c));
        Assert.False(RhythmMath.Behind(in r, -0.2, 0));
        Assert.False(RhythmMath.Behind(in r, -2.0, 0));
        // covered: a mate in front of him short of the enemy = not a front-rank man
        Assert.True(RhythmMath.Covers(in r, 1.0, 0.1, 2.0));
        Assert.False(RhythmMath.Covers(in r, 1.0, 0.8, 2.0));
        Assert.False(RhythmMath.Covers(in r, 2.5, 0, 2.0));
        // the beside point and the beside test (the line keeps its man)
        RhythmMath.BesidePoint(0, 0, 0, 1, 1, 0.7, out double bx, out double by);
        Assert.Equal(-0.7, bx, 9); // e = (−uy, ux) = (−1, 0)
        Assert.Equal(0, by, 9);
        Assert.True(RhythmMath.IsBeside(in r, 0, 0, -0.7, -0.3, 0, 1));
        Assert.False(RhythmMath.IsBeside(in r, 0, 0, -0.7, -0.6, 0, 1));   // still behind his depth
        Assert.False(RhythmMath.IsBeside(in r, 0, 0, -1.5, 0, 0, 1));      // too far to the side
        Assert.Equal(-1, RhythmMath.SideOf(-0.5));
        Assert.Equal(1, RhythmMath.SideOf(0.05));
    }

    [Fact]
    public void Busy_in_an_attack_a_kick_a_stagger_or_a_fall_never_in_a_guard()
    {
        Assert.True(RhythmMath.BusyAction(19));  // ReadyMelee
        Assert.True(RhythmMath.BusyAction(20));  // ReleaseMelee
        Assert.True(RhythmMath.BusyAction(23));  // Fall
        Assert.True(RhythmMath.BusyAction(28));  // Kick
        Assert.True(RhythmMath.BusyAction(31));  // WeaponBash
        Assert.True(RhythmMath.BusyAction(51));  // StrikeKnockBack
        Assert.False(RhythmMath.BusyAction(2));  // DefendShield
        Assert.False(RhythmMath.BusyAction(36)); // Guard
        Assert.False(RhythmMath.BusyAction(-1));
        Assert.True(RhythmMath.Busy(-1, true, false, false));
        Assert.True(RhythmMath.Busy(-1, false, true, false));
        Assert.True(RhythmMath.Busy(-1, false, false, true));
        Assert.False(RhythmMath.Busy(-1, false, false, false));
    }

    [Fact]
    public void A_units_front_is_its_front_rank_and_its_bar_the_front_mens()
    {
        // three ranks along +y at 0, -1, -2 (4 men each): the front 0, the front men within 1.5 = two ranks
        var along = new double[] { 0, 0, 0, 0, -1, -1, -1, -1, -2, -2, -2, -2 };
        var bars = new double[] { 0.2, 0.2, 0.2, 0.2, 0.4, 0.4, 0.4, 0.4, 0.9, 0.9, 0.9, 0.9 };
        var scratch = new double[16];
        Assert.True(RhythmMath.FrontOf(along, bars, 12, 1.5, scratch, out double front, out double bar, out int men));
        Assert.Equal(0, front, 9);
        Assert.Equal(8, men);
        Assert.Equal(0.3, bar, 9);
        Assert.True(RhythmMath.FrontOf(along, bars, 12, 0.5, scratch, out _, out bar, out men));
        Assert.Equal(4, men);
        Assert.Equal(0.2, bar, 9);
        Assert.False(RhythmMath.FrontOf(along, bars, 0, 1.5, scratch, out _, out _, out _));
    }

    /// <summary>Two lines facing each other: unit A (team 0) at y ≤ 0 facing north, unit B (team 1) from y = gap facing south; 3 ranks of 10.</summary>
    internal static void Note(RhythmUnits t, int a, int b, double gap, double barA, double barB, BraceOrderKind ka, BraceOrderKind kb, bool heldB = false,
        bool playerA = false, int menB = 10)
    {
        for (int k = 0; k < 3; k++)
            for (int f = 0; f < 10; f++)
            {
                t.Note(a, f, -k, barA, k == 0, k == 0 ? b : -1, ka, playerA, false);
                if (f < menB) t.Note(b, f, gap + k, barB, !heldB && k == 0, !heldB && k == 0 ? a : -1, kb, false, heldB);
            }
    }

    internal static int A => FearMath.UnitKey(0, 1);
    internal static int B => FearMath.UnitKey(1, 2);

    internal static double Run(RhythmUnits t, double now, double seconds, double gap, double barA, double barB, BraceOrderKind ka, BraceOrderKind kb,
        bool heldB = false, bool playerA = false, int menB = 10)
    {
        for (double s = 0; s < seconds - 1e-9; s += 0.5)
        {
            now += 0.5;
            Note(t, A, B, gap, barA, barB, ka, kb, heldB, playerA, menB);
            if (t.Due(now)) t.Publish(now);
        }
        return now;
    }

    [Fact]
    public void The_table_pairs_a_held_wall_by_its_attackers_votes_and_reads_both_fronts()
    {
        var t = new RhythmUnits();
        Run(t, 100, 2, 1.0, 0.8, 0.8, BraceOrderKind.Charge, BraceOrderKind.Stop, heldB: true);
        Assert.Equal(B, t.Enemy[A]);
        Assert.Equal(A, t.Enemy[B]);       // the wall's men voted for nobody - its attackers' votes pair it
        Assert.Equal(10, t.ContactMen[A]);
        Assert.Equal(0, t.ContactMen[B]);
        Assert.Equal(30, t.HeldMen[B]);
        Assert.Equal(0, t.UX[A], 6);
        Assert.Equal(1, t.UY[A], 6);
        Assert.Equal(-1, t.UY[B], 6);
        Assert.Equal(0.8, t.FrontBar[A], 6);
        Assert.Equal(0, t.BehindFront(A, 3, 0, 102.0), 6);   // step 78c: a front-rank man stands at his front
        Assert.Equal(2, t.BehindFront(A, 3, -2, 102.0), 6);  // ... the third rank 2 m behind it
    }

    [Fact]
    public void The_summary_lines()
    {
        var s = new RhythmStats();
        var t = new RhythmUnits();
        s.Asked[1] = 40;
        s.Chances[1] = 30;
        s.Started = 25;
        s.Ends[(int)RotationEnd.Done] = 22;
        s.Ends[(int)RotationEnd.NeverBeside] = 3;
        s.Refused[(int)RotationRefusal.NotFresher] = 10;
        s.TiredBarSum = 25 * 0.3;
        s.FreshBarSum = 25 * 0.8;
        s.AddContact(true, 0.6);
        s.AddContact(false, 0.4);
        var lines = new List<string>();
        s.AddSummaryLines(lines, Rules(), t);
        Assert.Single(lines); // step 78c: the lull's lines are gone
        Assert.Contains("ROTATION ON", lines[0]);
        Assert.Contains("swaps asked 25, done 22", lines[0]);
        Assert.Contains("never beside (called off) 3", lines[0]);
        Assert.Contains("nobody fresher behind 10", lines[0]);
        Assert.Contains("tired 30% vs fresh 80%", lines[0]);
        Assert.Contains("IN CONTACT ON 60%", lines[0]);
        Assert.DoesNotContain("lull", lines[0], StringComparison.OrdinalIgnoreCase);
        var off = new List<string>();
        s.AddSummaryLines(off, Rules(rotation: false, rest: false), t);
        Assert.Contains("ROTATION OFF (RotationEnabled)", off[0]);
    }
}

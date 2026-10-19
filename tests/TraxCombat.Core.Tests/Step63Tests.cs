using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 63 - FALLS ON THE HIT (Anton's battle of 2026-10-19 11:02; AI_NOTES "Step 63"): 126 of 198 decided falls were played only by our
/// backup blow, every one on a 0-damage blow - a FRIEND's (the engine cancels a friend's damage, and a 0-damage blow plays no reaction). A
/// friend's blow is never decided; the backup comes after 0.2 s (config format 11 moves step 62's 0.5 once).
/// </summary>
public class FallsOnTheHitTests
{
    /// <summary>defaults.json's step-63 value - the tests run on DESIGN's initial 0.5, where nothing moves.</summary>
    private static double Shipped(ParamDef p) => p == SettingsSchema.ImpactFallBackupSeconds ? 0.2 : p.Default;

    [Fact]
    public void A_format_10_file_at_step_62s_backup_gets_0_2_once_a_tuned_one_is_kept_and_said()
    {
        Assert.Equal(11, ConfigFile.FormatVersion);
        var read = ConfigFile.Read("{ \"ConfigVersion\": 10, \"ImpactFallBackupSeconds\": 0.5 }");
        var notes = ConfigFile.Migrate(read, Shipped);
        Assert.Single(notes);
        Assert.StartsWith("ImpactFallBackupSeconds: 0.5 → 0.2 (format 10 → 11, the old default; step 63, Anton's battle", notes[0], StringComparison.Ordinal);
        Assert.Equal(0.2, read.Values["ImpactFallBackupSeconds"], 9);
        var mine = ConfigFile.Read("{ \"ConfigVersion\": 10, \"ImpactFallBackupSeconds\": 1.0 }");
        var kept = ConfigFile.Migrate(mine, Shipped);
        Assert.Single(kept);
        Assert.StartsWith("ImpactFallBackupSeconds: 1.0 kept - your own value (format 10 → 11; the new default 0.2", kept[0], StringComparison.Ordinal);
        Assert.Equal(1.0, mine.Values["ImpactFallBackupSeconds"], 9);
        // a format-11 file is never touched; a file without the key gets nothing to say; on DESIGN's initial 0.5 nothing moves
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 11, \"ImpactFallBackupSeconds\": 0.5 }"), Shipped));
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 10, \"CostPerBlow\": 7.0 }"), Shipped));
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 10, \"ImpactFallBackupSeconds\": 0.5 }")));
    }

    [Fact]
    public void The_backup_is_due_after_0_2_s_with_the_new_default()
    {
        Assert.False(ImpactFallMath.BackupDue(10.15, 10, 0.2, false, false));
        Assert.True(ImpactFallMath.BackupDue(10.21, 10, 0.2, false, false));
        Assert.False(ImpactFallMath.BackupDue(10.3, 10, 0.2, true, false));  // the game played it
        Assert.False(ImpactFallMath.BackupDue(10.3, 10, 0.2, false, true));  // sent once
    }

    [Fact]
    public void A_friends_blow_is_a_fact_of_its_own_off_by_default_and_counted_in_both_summary_lines()
    {
        Assert.False(new ImpactFacts().NotEnemies);
        var stats = new ImpactStats();
        stats.EverOn = true;
        stats.Skip(ImpactSkip.NotEnemies, ImpactEvent.CleanHit);
        stats.Skip(ImpactSkip.NotEnemies, ImpactEvent.Parry);
        var d = new ImpactDecision { Event = ImpactEvent.CleanHit, Bar = 0.5, Knockdown = ImpactRoll.None, ShieldDrop = ImpactRoll.None, Stagger = ImpactRoll.None, Riposte = ImpactRoll.None };
        stats.Note(in d, false, true, 0.001, 0);
        Assert.Contains(stats.SummaryLines(ImpactRules.From(new TraxSettings())), l => l.Contains("a FRIEND's blow (step 63 - the game cancels its damage, nothing plays) 2"));
        var falls = new ImpactFallStats();
        falls.Friendly = 126;
        string line = falls.SummaryLine(true, 0.2, true);
        Assert.DoesNotContain("no knockdown decided", line); // friends' blows alone are worth the line
        Assert.Contains("because a FRIEND struck them (step 63 - the game cancels a friend's damage: nothing to play) 126", line);
        Assert.Contains("backups (ImpactFallBackupSeconds 0.2 s)", line);
    }
}

/// <summary>
/// Step 63 - A STRONG REAR BLOW (AI_NOTES "Step 63", DESIGN decision 53): a landed blow's direction adds PRESSURE to x straight, like
/// tiredness - <c>ImpactRearPressure</c> 0.5 from behind, <c>ImpactFlankPressure</c> 0.2 from the side - and the step-62 multipliers ship at
/// x1 (both would count the direction twice). The WORKED EXAMPLES on the shipped values (DESIGN's initial values with defaults.json's
/// step-56 block model and step 63's x1 multipliers) - docs/MECHANICS.md §11 "A blow from behind".
/// </summary>
public class RearBlowTests
{
    private sealed class Never : IRandomSource
    {
        public double NextDouble() => 0.999;
    }

    private readonly record struct Troop(double Pool, double Skill, double Kg, double Speed, double Armour, int Level)
    {
        public double Toughness => Armour < 0 ? 0 : CourageMath.BaseToughness(200, 100, Armour, Level);
    }

    private static readonly Troop Legionary = new(130, 130, 1.3, 92, 110, 26);
    private static readonly Troop Archer = new(50, 40, 1.3, 92, 15, 11);
    private static readonly Troop Even = new(100, 100, 1.2, 92, -1, 0); // toughness unknown = even

    private static ImpactRules Shipped(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        s.Set(SettingsSchema.RightBlockStaggerMaxPercent, 30, "test");
        s.Set(SettingsSchema.RightBlockKnockdownMaxPercent, 10, "test");
        s.Set(SettingsSchema.ParryStaggerMaxPercent, 30, "test");
        s.Set(SettingsSchema.ParryKnockdownMaxPercent, 10, "test");
        s.Set(SettingsSchema.RiposteStaggerMaxPercent, 70, "test");
        s.Set(SettingsSchema.BlockStunTiredExtraPercent, 0, "test");
        s.Set(SettingsSchema.ImpactRearPercent, 100, "test"); // step 63's defaults.json
        s.Set(SettingsSchema.ImpactFlankPercent, 100, "test");
        tune?.Invoke(s);
        return ImpactRules.From(s);
    }

    private static ImpactFacts Blow(ImpactEvent e, Troop a, Troop d, double dBar, bool charging = false, double angle = 0, bool bow = false)
    {
        return new ImpactFacts
        {
            Event = e,
            AttackerPoints = a.Pool,
            AttackerF = 1,
            AttackerAnimation = 1,
            AttackerSkill = a.Skill,
            DefenderPoints = d.Pool * dBar,
            DefenderBar = dBar,
            DefenderF = Math.Min(1, dBar / 0.75),
            DefenderSkill = d.Skill,
            WeaponWeight = a.Kg,
            SwingSpeed = a.Speed,
            DamageType = ImpactDamageType.Cut,
            GuardWeight = ImpactMath.IsBlock(e) ? 4.0 : 0,
            DefenderHasShield = ImpactMath.IsBlock(e),
            DefenderMaxHealth = 100,
            AttackerSpeed = charging ? 3.0 : 1.0,
            AttackerCharging = charging,
            AttackAngleDegrees = angle,
            DefenderRangedInHand = bow,
            AttackerToughness = a.Toughness,
            DefenderToughness = d.Toughness,
        };
    }

    private static ImpactDecision Decide(in ImpactFacts f, ImpactRules? rules = null)
    {
        var r = rules ?? Shipped();
        var d = default(ImpactState);
        var a = default(ImpactState);
        return ImpactMath.Decide(in r, in f, ref d, ref a, 100, new Never());
    }

    private static void Near(double expected, double actual, double tol, string what) =>
        Assert.True(Math.Abs(expected - actual) <= tol, what + ": " + (100 * actual).ToString("0.0") + "% vs " + (100 * expected).ToString("0.0") + "%");

    [Fact]
    public void The_two_settings_sit_after_the_angles_on_the_Advanced_page_live_and_default_0_5_and_0_2()
    {
        var all = SettingsSchema.All.ToList();
        int i = all.IndexOf(SettingsSchema.ImpactFlankDegrees);
        Assert.Same(SettingsSchema.ImpactRearPressure, all[i + 1]);
        Assert.Same(SettingsSchema.ImpactFlankPressure, all[i + 2]);
        Assert.Same(SettingsSchema.ImpactGroup, SettingsSchema.ImpactRearPressure.Group);
        Assert.Equal(SettingsPage.Advanced, SettingsSchema.ImpactRearPressure.Group.Page);
        Assert.Equal(ApplyTiming.Live, SettingsSchema.ImpactFlankPressure.Timing);
        var r = Shipped();
        Assert.Equal(0.5, r.RearPressure, 6);
        Assert.Equal(0.2, r.FlankPressure, 6);
        Assert.Equal(1.0, r.RearFactor, 9);
        Assert.Equal(1.0, r.FlankFactor, 9);
        Assert.Contains("from behind (> 120°) +0.50 pressure, from the side (60°+) +0.20", r.Describe());
        var tuned = Shipped(s => s.Set(SettingsSchema.ImpactRearPressure, 1.0, SettingSources.Mcm));
        Assert.Equal(1.0, ImpactMath.DirectionPressure(in tuned, ImpactEvent.CleanHit, 170), 6);
    }

    [Fact]
    public void Direction_adds_pressure_straight_to_landed_blows_only_never_a_block_never_the_front()
    {
        var r = Shipped();
        Assert.Equal(0.5, ImpactMath.DirectionPressure(in r, ImpactEvent.CleanHit, 160), 6);
        Assert.Equal(0.5, ImpactMath.DirectionPressure(in r, ImpactEvent.Kick, 180), 6);
        Assert.Equal(0.2, ImpactMath.DirectionPressure(in r, ImpactEvent.Bash, 90), 6);
        Assert.Equal(0.0, ImpactMath.DirectionPressure(in r, ImpactEvent.CleanHit, 30), 6);
        Assert.Equal(0.0, ImpactMath.DirectionPressure(in r, ImpactEvent.CleanHit, double.NaN), 6);
        Assert.Equal(0.0, ImpactMath.DirectionPressure(in r, ImpactEvent.WrongBlock, 170), 6);
        var front = Decide(Blow(ImpactEvent.CleanHit, Even, Even, 0.5));
        var rear = Decide(Blow(ImpactEvent.CleanHit, Even, Even, 0.5, angle: 160));
        Assert.Equal(0.5, rear.X - front.X, 9);       // straight into x
        Assert.Equal(front.Log2R, rear.Log2R, 9);     // the mismatch untouched: no double count
        Assert.Equal(0.5, rear.DirectionPart, 9);
        Assert.Contains("from BEHIND (160°, +0.50 pressure)", ImpactMath.Describe(in rear, Blow(ImpactEvent.CleanHit, Even, Even, 0.5, angle: 160), "A", "B", "LIVE"));
        // 0 = off: the front's chances
        var off = Decide(Blow(ImpactEvent.CleanHit, Even, Even, 0.5, angle: 160), Shipped(s => s.Set(SettingsSchema.ImpactRearPressure, 0, "test")));
        Assert.Equal(front.X, off.X, 9);
    }

    [Fact]
    public void An_even_man_behind_an_even_fresh_man_can_floor_him_the_front_is_unchanged()
    {
        var front = Decide(Blow(ImpactEvent.CleanHit, Even, Even, 1.0));
        Assert.Equal(0, front.Knockdown.Chance);      // two fresh even men: nothing, as ever
        Assert.Equal(0, front.Stagger.Chance);
        var side = Decide(Blow(ImpactEvent.CleanHit, Even, Even, 1.0, angle: 90));
        Assert.Equal(0, side.Knockdown.Chance);
        Near(0.083, side.Stagger.Chance, 0.005, "an even fresh man from the side - staggered");
        var rear = Decide(Blow(ImpactEvent.CleanHit, Even, Even, 1.0, angle: 160));
        Near(0.068, rear.Knockdown.Chance, 0.005, "an even fresh man from behind - down");
        Near(0.40, rear.Stagger.Chance, 0.005, "an even fresh man from behind - staggered");
        // at half a bar: the front 0 / 21 %, behind 42 / 74 %
        var halfFront = Decide(Blow(ImpactEvent.CleanHit, Even, Even, 0.5));
        Near(0.0, halfFront.Knockdown.Chance, 0.001, "half a bar, front - down");
        Near(0.207, halfFront.Stagger.Chance, 0.005, "half a bar, front - staggered");
        var halfRear = Decide(Blow(ImpactEvent.CleanHit, Even, Even, 0.5, angle: 160));
        Near(0.418, halfRear.Knockdown.Chance, 0.005, "half a bar, behind - down");
        Near(0.741, halfRear.Stagger.Chance, 0.005, "half a bar, behind - staggered");
    }

    [Fact]
    public void A_legionary_standing_or_charging_behind_a_fresh_peasant_archer()
    {
        // the front unchanged by step 63: down 1 %, staggered 29 %
        var front = Decide(Blow(ImpactEvent.CleanHit, Legionary, Archer, 1.0, bow: true));
        Near(0.010, front.Knockdown.Chance, 0.005, "standing, front - down");
        Near(0.286, front.Stagger.Chance, 0.01, "standing, front - staggered");
        // STANDING behind him: 47 % down (step 62's x2 read 23 %)
        var behind = Decide(Blow(ImpactEvent.CleanHit, Legionary, Archer, 1.0, angle: 160, bow: true));
        Near(0.470, behind.Knockdown.Chance, 0.01, "standing, behind - down");
        // the side: 17 %
        Near(0.169, Decide(Blow(ImpactEvent.CleanHit, Legionary, Archer, 1.0, angle: 90, bow: true)).Knockdown.Chance, 0.01, "standing, side - down");
        // CHARGING into his back: the clean hit's ceiling, 50 %
        var charge = Decide(Blow(ImpactEvent.CleanHit, Legionary, Archer, 1.0, charging: true, angle: 160, bow: true));
        Near(0.50, charge.Knockdown.Chance, 0.001, "charging, behind - down");
        // charging into his FRONT: 14 %
        Near(0.136, Decide(Blow(ImpactEvent.CleanHit, Legionary, Archer, 1.0, charging: true, bow: true)).Knockdown.Chance, 0.01, "charging, front - down");
    }

    [Fact]
    public void The_cap_pressure_is_no_multiplier_and_the_ceiling_holds()
    {
        // a charging legionary into a tired archer's back: every multiplier and the pressure - the chance stops at the clean hit's ceiling
        var r = Shipped();
        var tired = Decide(Blow(ImpactEvent.CleanHit, Legionary, Archer, 0.1, charging: true, angle: 160, bow: true));
        Assert.Equal(r.HitKnockdown, tired.Knockdown.Chance, 9);
        Assert.True(tired.X > 2);
        // the multipliers' cap (x8) never sees the pressure: Extra and R the same from the front and from behind
        var front = Decide(Blow(ImpactEvent.CleanHit, Legionary, Archer, 0.1, charging: true, bow: true));
        Assert.Equal(front.Extra, tired.Extra, 9);
        Assert.Equal(front.R, tired.R, 9);
        Assert.Equal(front.Capped, tired.Capped);
        // even a pressure of 2 never passes the ceilings
        var hard = Decide(Blow(ImpactEvent.CleanHit, Even, Even, 1.0, angle: 170), Shipped(s => s.Set(SettingsSchema.ImpactRearPressure, 2, "test")));
        Assert.Equal(r.HitKnockdown, hard.Knockdown.Chance, 9);
        Assert.Equal(r.HitStagger, hard.Stagger.Chance, 9);
    }

    /// <summary>defaults.json's step-63 values - the tests run on DESIGN's initial 200 / 140, where nothing moves.</summary>
    private static double Shipped63(ParamDef p) =>
        p == SettingsSchema.ImpactRearPercent || p == SettingsSchema.ImpactFlankPercent ? 100
        : p == SettingsSchema.ImpactFallBackupSeconds ? 0.2
        : p.Default;

    [Fact]
    public void A_format_10_file_at_step_62s_direction_multipliers_gets_x1_once_tuned_kept()
    {
        var read = ConfigFile.Read("{ \"ConfigVersion\": 10, \"ImpactRearPercent\": 200, \"ImpactFlankPercent\": 140, \"ImpactRearDegrees\": 120 }");
        var notes = ConfigFile.Migrate(read, Shipped63);
        Assert.Equal(2, notes.Count);
        Assert.StartsWith("ImpactRearPercent: 200 → 100 (format 10 → 11, the old default; step 63, Anton's battle: a blow from behind now adds PRESSURE", notes[0], StringComparison.Ordinal);
        Assert.StartsWith("ImpactFlankPercent: 140 → 100 (format 10 → 11, the old default; step 63", notes[1], StringComparison.Ordinal);
        Assert.Equal(100, read.Values["ImpactRearPercent"], 9);
        Assert.Equal(100, read.Values["ImpactFlankPercent"], 9);
        var mine = ConfigFile.Read("{ \"ConfigVersion\": 10, \"ImpactRearPercent\": 300 }");
        var kept = ConfigFile.Migrate(mine, Shipped63);
        Assert.Single(kept);
        Assert.StartsWith("ImpactRearPercent: 300 kept - your own value (format 10 → 11; the new default 100", kept[0], StringComparison.Ordinal);
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 11, \"ImpactRearPercent\": 200 }"), Shipped63));
    }
}

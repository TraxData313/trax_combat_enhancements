using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 62 - MORE MEN FALL, AND THE CHARGE MATTERS (Anton 2026-10-19; AI_NOTES "Step 62", DESIGN decision 52): the lost falls (the shrug-off
/// yields to a decided fall, the backup blow, a killing blow never decided) and the falls' summary.
/// </summary>
public class ChargeTests
{
    private sealed class Dice : IRandomSource
    {
        public double NextDouble() => 0.999;
    }

    private static ImpactRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return ImpactRules.From(s);
    }

    /// <summary>Courage's toughness of the worked examples' troops (step 49): a looter 1.35, an imperial legionary 5.97.</summary>
    private static readonly double Looter = CourageMath.BaseToughness(200, 100, CourageMath.ExampleLooterArmour, CourageMath.ExampleLooterLevel);

    private static readonly double Legionary = CourageMath.BaseToughness(200, 100, CourageMath.ExampleLegionaryArmour, CourageMath.ExampleLegionaryLevel);

    /// <summary>One event between two men of pool 100 and skill 100 (a 1.2 kg sword at 92, a 4 kg shield / a 1.2 kg parrying sword), the
    /// peak line at 75 %, the attacker's tired swing (0.85 + 0.15 f) - with step 62's inputs.</summary>
    private static ImpactFacts Facts(ImpactEvent e, double bar, double attackerBar = 1.0, double tA = 0, double tD = 0, double speed = 0,
        bool ranged = false, double angle = 0, bool charging = false)
    {
        double fA = Math.Min(1, attackerBar / 0.75);
        return new ImpactFacts
        {
            Event = e,
            AttackerPoints = 100 * attackerBar,
            AttackerF = fA,
            AttackerAnimation = 0.85 + 0.15 * fA,
            AttackerSkill = 100,
            DefenderPoints = 100 * bar,
            DefenderBar = bar,
            DefenderF = Math.Min(1, bar / 0.75),
            DefenderSkill = 100,
            WeaponWeight = 1.2,
            SwingSpeed = 92,
            GuardWeight = e == ImpactEvent.Parry ? 1.2 : ImpactMath.IsBlock(e) ? 4.0 : 0,
            DefenderHasShield = e != ImpactEvent.Parry,
            DefenderMaxHealth = 100,
            AttackerSpeed = speed,
            AttackerToughness = tA,
            DefenderToughness = tD,
            DefenderRangedInHand = ranged,
        };
    }

    private static ImpactDecision Decide(in ImpactFacts f, ImpactRules? rules = null)
    {
        var r = rules ?? Rules();
        var d = default(ImpactState);
        var a = default(ImpactState);
        return ImpactMath.Decide(in r, in f, ref d, ref a, 100, new Dice());
    }

    // ------------------------------------------------------------------ toughness, a ranged man in melee

    [Fact]
    public void Toughness_counts_strongly_not_absolutely_a_legionary_on_a_looter_x2_and_the_looter_on_him_half()
    {
        Assert.Equal(1.35, Looter, 2);
        Assert.Equal(5.97, Legionary, 2);
        var r = Rules();
        Assert.Equal(0.5, r.ToughnessWeight, 6);
        Assert.Equal(2.10, ImpactMath.ToughnessFactor(in r, Legionary, Looter), 2);
        Assert.Equal(1 / 2.10, ImpactMath.ToughnessFactor(in r, Looter, Legionary), 2);
        Assert.Equal(1.0, ImpactMath.ToughnessFactor(in r, Legionary, Legionary), 9);
        Assert.Equal(1.0, ImpactMath.ToughnessFactor(in r, 0, Looter), 9);                 // unknown = even
        Assert.Equal(1.0, ImpactMath.ToughnessFactor(Rules(s => s.Set(SettingsSchema.ImpactToughnessWeight, 0.0, SettingSources.Mcm)), Legionary, Looter), 9);
        // in R: one doubling more (+0.25 x at the shipped mismatch weight) - the same blow, with and without
        var even = Decide(Facts(ImpactEvent.CleanHit, 0.5));
        var tough = Decide(Facts(ImpactEvent.CleanHit, 0.5, tA: Legionary, tD: Looter));
        Assert.Equal(Math.Log(2.10, 2), tough.Log2R - even.Log2R, 2);
        Assert.Equal(2.10, tough.ToughnessFactor, 2);
        Assert.Equal(Legionary / Looter, tough.ToughnessRatio, 6);
        Assert.True(double.IsNaN(even.ToughnessRatio));
        Assert.True(tough.X > even.X + 0.2, "x " + tough.X + " vs " + even.X);
    }

    [Fact]
    public void Two_fresh_men_of_the_same_toughness_still_never_stagger_each_other()
    {
        var d = Decide(Facts(ImpactEvent.CleanHit, 1.0, tA: Legionary, tD: Legionary));
        Assert.Equal(0, d.Knockdown.Chance);
        Assert.True(d.Stagger.Chance < 0.005, "stagger " + d.Stagger.Chance);
        var p = Decide(Facts(ImpactEvent.CleanHit, 1.0, tA: Looter, tD: Looter));
        Assert.Equal(0, p.Knockdown.Chance);
    }

    [Fact]
    public void The_riposte_reads_toughness_turned_round_a_tough_blocker_reels_a_weak_attacker_more()
    {
        var weakIntoTough = Decide(Facts(ImpactEvent.RightBlock, 1.0, attackerBar: 0.5, tA: Looter, tD: Legionary));
        var even = Decide(Facts(ImpactEvent.RightBlock, 1.0, attackerBar: 0.5));
        Assert.True(weakIntoTough.XRiposte > even.XRiposte + 0.2, "x_r " + weakIntoTough.XRiposte + " vs " + even.XRiposte);
        Assert.True(weakIntoTough.X <= even.X, "the tough blocker was pressed harder");
    }

    [Fact]
    public void A_man_with_a_bow_in_his_hand_takes_a_landed_blow_harder_never_a_block()
    {
        var r = Rules();
        Assert.Equal(1.3, r.RangedInHand, 6);
        Assert.Equal(1.3, ImpactMath.RangedFactor(in r, ImpactEvent.CleanHit, true), 6);
        Assert.Equal(1.3, ImpactMath.RangedFactor(in r, ImpactEvent.Kick, true), 6);
        Assert.Equal(1.0, ImpactMath.RangedFactor(in r, ImpactEvent.Parry, true), 6);
        Assert.Equal(1.0, ImpactMath.RangedFactor(in r, ImpactEvent.CleanHit, false), 6);
        Assert.Equal(1.0, ImpactMath.RangedFactor(Rules(s => s.Set(SettingsSchema.ImpactRangedInHandPercent, 100, SettingSources.Mcm)), ImpactEvent.CleanHit, true), 6);
        var archer = Decide(Facts(ImpactEvent.CleanHit, 0.5, ranged: true));
        var plain = Decide(Facts(ImpactEvent.CleanHit, 0.5));
        Assert.Equal(Math.Log(1.3, 2), archer.Log2R - plain.Log2R, 3);
    }

    // ------------------------------------------------------------------ the lost falls

    [Fact]
    public void The_lost_falls_settings_sit_after_the_punish_ones_live_on_by_default_with_a_half_second_backup()
    {
        var keys = SettingsSchema.InGroup(SettingsSchema.ImpactGroup).Select(p => p.Key).ToList();
        int i = keys.IndexOf("ImpactPunishRangeMetres");
        Assert.Equal(new[] { "ImpactShrugOffYieldsToImpact", "ImpactFallBackupSeconds" }, keys.Skip(i + 1).Take(2));
        var r = Rules();
        Assert.True(r.ShrugOffYields);
        Assert.Equal(0.5, r.FallBackupSeconds, 6);
        Assert.Equal(SettingsPage.Advanced, SettingsSchema.ImpactFallBackupSeconds.Group.Page);
        Assert.False(Rules(s => s.Set(SettingsSchema.ImpactShrugOffYieldsToImpact, false, SettingSources.Mcm)).ShrugOffYields);
        Assert.Equal(0, Rules(s => s.Set(SettingsSchema.ImpactFallBackupSeconds, 0.0, SettingSources.Mcm)).FallBackupSeconds);
        Assert.Equal(ImpactFallMath.MaxBackupSeconds, SettingsSchema.ImpactFallBackupSeconds.Max);
    }

    [Fact]
    public void The_backup_is_due_once_its_wait_is_over_and_never_after_a_fall_showed_or_a_backup_went()
    {
        Assert.False(ImpactFallMath.BackupDue(100.4, 100, 0.5, appeared: false, sent: false));
        Assert.True(ImpactFallMath.BackupDue(100.5, 100, 0.5, appeared: false, sent: false));
        Assert.True(ImpactFallMath.BackupDue(101.2, 100, 0.5, appeared: false, sent: false));
        Assert.False(ImpactFallMath.BackupDue(101.2, 100, 0.5, appeared: true, sent: false));  // the game's fall showed
        Assert.False(ImpactFallMath.BackupDue(101.2, 100, 0.5, appeared: false, sent: true));  // once
        Assert.False(ImpactFallMath.BackupDue(101.2, 100, 0, appeared: false, sent: false));   // 0 = no backup
        Assert.False(ImpactFallMath.BackupDue(101.2, double.NaN, 0.5, appeared: false, sent: false));
    }

    [Fact]
    public void A_blow_that_leaves_him_under_one_hit_point_kills_and_is_never_decided()
    {
        Assert.True(ImpactFallMath.Fatal(10, 10));
        Assert.True(ImpactFallMath.Fatal(10, 9.5));
        Assert.False(ImpactFallMath.Fatal(10, 9));
        Assert.False(ImpactFallMath.Fatal(100, 0));
        Assert.False(ImpactFallMath.Fatal(double.NaN, 5));
        var stats = new ImpactStats();
        stats.Skip(ImpactSkip.Fatal, ImpactEvent.CleanHit);
        stats.EverOn = true;
        var d = new ImpactDecision { Event = ImpactEvent.CleanHit, Bar = 0.5, Knockdown = ImpactRoll.None, ShieldDrop = ImpactRoll.None, Stagger = ImpactRoll.None, Riposte = ImpactRoll.None };
        stats.Note(in d, false, true, 0.001, 0);
        Assert.Contains(stats.SummaryLines(Rules()), l => l.Contains("the blow killed him (step 62) 1"));
    }

    [Fact]
    public void The_falls_line_counts_decided_played_by_the_game_by_the_backup_not_seen_and_the_shrug_offs_refused()
    {
        var f = new ImpactFallStats();
        Assert.Contains("no knockdown decided on a landed blow this mission", f.SummaryLine(true, 0.5, true));
        for (int i = 0; i < 5; i++) f.NoteDecided(ImpactEvent.CleanHit, 12);
        f.NoteDecided(ImpactEvent.CleanHit, 0);
        f.NoteDecided(ImpactEvent.Kick, 2);
        f.NotePlayed(ImpactEvent.CleanHit, false, 0.02);
        f.NotePlayed(ImpactEvent.CleanHit, false, 0.04);
        f.NotePlayed(ImpactEvent.CleanHit, true, 0.55);
        f.NotePlayed(ImpactEvent.Kick, false, 0.0);
        f.NoteNotSeen(ImpactEvent.CleanHit);
        f.NoteLeftField(ImpactEvent.CleanHit);
        f.BackupsSent = 2;
        f.ShrugRefusedForFall = 3;
        f.ShrugRefusedForStagger = 4;
        f.Fatal = 9;
        string line = f.SummaryLine(true, 0.5, true);
        Assert.Contains("decided 7 (kicks / bashes 1, clean hits 6) → played by the game 3 (0.02 s after the blow), by our backup blow 1 (0.55 s after the blow), STILL NOT SEEN 1, the man left the field / died first 1", line);
        Assert.Contains("clean hits 2 / 1 / 1", line);
        Assert.Contains("on a 0-damage blow (the engine plays no reaction) 1", line);
        Assert.Contains("backups (ImpactFallBackupSeconds 0.5 s) sent 2", line);
        Assert.Contains("the game's shrug-off REFUSED for a decided fall 3 / stagger 4", line);
        Assert.Contains("(ImpactShrugOffYieldsToImpact on;", line);
        Assert.Contains("landed blows not decided because they KILLED 9", line);
        Assert.Contains("ImpactFallBackupSeconds 0 - off", f.SummaryLine(true, 0, false));
        Assert.Contains("ImpactShrugOffYieldsToImpact OFF", f.SummaryLine(true, 0, false));
    }
}

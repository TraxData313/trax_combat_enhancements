using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 53 (IMPACT I6, riders + the on-screen lines - AI_NOTES "Step 53"): the six settings; a rider's knockdown = KNOCKED OFF at
/// <c>ImpactRiderDismountPercent</c> of the chance on every family, never a stagger, his shield at the full chance; the rider's MOMENTUM -
/// his horse's speed at the rider settings (none to a walk, +30% at a gallop), never a couched lance, the foot settings untouched; the horse
/// speed bands; the cavalry charge (a galloping rider into a tired line: more, never everything; a fresh line: nothing); the on-screen lines'
/// words, colours, the notable rule, the RATE LIMIT and the MERGE; the riders' summary lines.
/// </summary>
public class ImpactRidersTests
{
    private sealed class Dice : IRandomSource
    {
        private readonly double[] _v;
        private int _i;
        public double Rest = 0.999;

        public Dice(params double[] values) => _v = values;

        public double NextDouble() => _i < _v.Length ? _v[_i++] : Rest;
    }

    private static ImpactRules Rules(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        tune?.Invoke(s);
        return ImpactRules.From(s);
    }

    /// <summary>Two men of pool 100 and skill 100 (a 1.2 kg sword at 92, a 4 kg shield / a 1.2 kg parrying sword), the peak line at 75 %.</summary>
    private static ImpactFacts Facts(ImpactEvent e, double bar, double attackerBar = 1.0, double speed = 0, bool defenderMounted = false, bool attackerMounted = false,
        bool passive = false)
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
            WeaponWeight = e == ImpactEvent.Bash ? 4.0 : 1.2,
            SwingSpeed = 92,
            DamageType = ImpactDamageType.Cut,
            GuardWeight = e == ImpactEvent.Parry ? 1.2 : ImpactMath.IsBlock(e) ? 4.0 : 0,
            DefenderHasShield = e != ImpactEvent.Parry,
            DefenderMaxHealth = 100,
            AttackerSpeed = speed,
            DefenderMounted = defenderMounted,
            AttackerMounted = attackerMounted,
            AttackerPassive = passive,
        };
    }

    private static ImpactDecision Decide(in ImpactFacts f, ImpactRules? rules = null, IRandomSource? dice = null)
    {
        var r = rules ?? Rules();
        var d = default(ImpactState);
        var a = default(ImpactState);
        return ImpactMath.Decide(in r, in f, ref d, ref a, 100, dice ?? new Dice());
    }

    private static readonly ImpactEvent[] Every = { ImpactEvent.Kick, ImpactEvent.Bash, ImpactEvent.RightBlock, ImpactEvent.Parry, ImpactEvent.WrongBlock, ImpactEvent.CleanHit };

    // ------------------------------------------------------------------ the settings

    [Fact]
    public void The_six_step_53_settings_sit_after_the_momentum_ones_live_with_their_defaults()
    {
        var keys = SettingsSchema.InGroup(SettingsSchema.ImpactGroup).Select(p => p.Key).ToList();
        int i = keys.IndexOf("ImpactMomentumSprintMetresPerSecond");
        var mine = new[]
        {
            "ImpactMomentumRiderPercent", "ImpactMomentumRiderWalkMetresPerSecond", "ImpactMomentumRiderGallopMetresPerSecond",
            "ImpactShowMessages", "ImpactMessagesIncludeYourMen", "ImpactMessageMinSeconds",
        };
        Assert.Equal(mine, keys.Skip(i + 1).Take(6));
        foreach (var k in mine)
        {
            Assert.True(SettingsSchema.TryGet(k, out var p));
            Assert.Equal(ApplyTiming.Live, p.Timing);
        }
        var s = new TraxSettings();
        Assert.Equal(30, s.ImpactMomentumRiderPercent);
        Assert.Equal(3.0f, s.ImpactMomentumRiderWalkMetresPerSecond);
        Assert.Equal(10.0f, s.ImpactMomentumRiderGallopMetresPerSecond);
        Assert.True(s.ImpactShowMessages);
        Assert.False(s.ImpactMessagesIncludeYourMen);
        Assert.Equal(1.5f, s.ImpactMessageMinSeconds);
        Assert.Contains("a rider's horse +30% at 10.0 m/s, none at 3.0 m/s (a couched lance never)", Rules().Describe());
    }

    // ------------------------------------------------------------------ the rider's momentum

    [Fact]
    public void A_riders_momentum_is_his_horses_speed_none_to_a_walk_plus_30_at_a_gallop_never_a_couched_lance()
    {
        var r = Rules();
        Assert.Equal(1.0, ImpactMath.MomentumFactor(in r, 2.9, true), 9);
        Assert.Equal(1.0, ImpactMath.MomentumFactor(in r, 3.0, true), 9);
        Assert.Equal(1.15, ImpactMath.MomentumFactor(in r, 6.5, true), 9); // half way: a canter
        Assert.Equal(1.3, ImpactMath.MomentumFactor(in r, 10.0, true), 9); // a gallop
        Assert.Equal(1.3, ImpactMath.MomentumFactor(in r, 15.0, true), 9); // clamped
        Assert.Equal(1.0, ImpactMath.MomentumFactor(in r, -10.0, true), 9); // riding AWAY adds nothing
        Assert.Equal(1.0, ImpactMath.MomentumFactor(in r, 12.0, true, passive: true), 9); // a couched lance: the game's own big hit
        Assert.Equal(1.6, ImpactMath.MomentumFactor(in r, 12.0, false, passive: true), 9); // on foot "passive" changes nothing (a braced spear stands)
        // the foot settings do not move a rider, the rider settings do not move a man on foot
        Assert.Equal(1.3, ImpactMath.MomentumFactor(Rules(s => s.Set(SettingsSchema.ImpactMomentumPercent, 0, "test")), 10.0, true), 9);
        Assert.Equal(1.6, ImpactMath.MomentumFactor(Rules(s => s.Set(SettingsSchema.ImpactMomentumRiderPercent, 0, "test")), 7.0, false), 9);
        Assert.Equal(1.0, ImpactMath.MomentumFactor(Rules(s => s.Set(SettingsSchema.ImpactMomentumRiderPercent, 0, "test")), 10.0, true), 9);
        Assert.Equal(1.0, ImpactMath.MomentumFactor(Rules(s => s.Set(SettingsSchema.ImpactMomentumEnabled, false, "test")), 10.0, true), 9); // the one switch for both
        Assert.Equal(1.6, ImpactMath.MomentumFactor(Rules(s => s.Set(SettingsSchema.ImpactMomentumRiderPercent, 60, "test")), 10.0, true), 9); // live
        Assert.Equal(1.3, ImpactMath.MomentumFactor(Rules(s => s.Set(SettingsSchema.ImpactMomentumRiderGallopMetresPerSecond, 8f, "test")), 8.0, true), 9);
    }

    [Fact]
    public void The_horse_speed_bands_are_standing_walking_trotting_galloping()
    {
        var r = Rules();
        Assert.Equal(0, ImpactMath.RiderSpeedBand(in r, 0.3));
        Assert.Equal(1, ImpactMath.RiderSpeedBand(in r, 2.5));
        Assert.Equal(2, ImpactMath.RiderSpeedBand(in r, 5.0));
        Assert.Equal(2, ImpactMath.RiderSpeedBand(in r, 9.9));
        Assert.Equal(3, ImpactMath.RiderSpeedBand(in r, 10.0));
        Assert.Equal(0, ImpactMath.RiderSpeedBand(in r, double.NaN));
        Assert.Equal("trotting / cantering", ImpactMath.RiderSpeedBandName(2));
        Assert.Equal("galloping", ImpactMath.RiderSpeedBandName(3));
        Assert.Equal(2, ImpactMath.SpeedBand(in r, 5.0)); // the same 5 m/s on foot: running
    }

    [Fact]
    public void A_riders_momentum_is_in_the_decision_and_a_couched_lance_carries_none()
    {
        var swing = Decide(Facts(ImpactEvent.CleanHit, 0.4, speed: 10, attackerMounted: true));
        Assert.Equal(1.3, swing.Momentum, 9);
        var standing = Decide(Facts(ImpactEvent.CleanHit, 0.4, speed: 0, attackerMounted: true));
        Assert.Equal(1.3 * standing.Impact, swing.Impact, 9);
        var lance = Decide(Facts(ImpactEvent.CleanHit, 0.4, speed: 12, attackerMounted: true, passive: true));
        Assert.Equal(1.0, lance.Momentum, 9);
        Assert.Equal(standing.Impact, lance.Impact, 9);
        Assert.Contains("(a couched lance - no momentum)", ImpactMath.Describe(in lance, Facts(ImpactEvent.CleanHit, 0.4, speed: 12, attackerMounted: true, passive: true), "A", "B", "LIVE"));
        Assert.Contains("incl. momentum x1.30 (his horse at 10.0 m/s into the blow)", ImpactMath.Describe(in swing, Facts(ImpactEvent.CleanHit, 0.4, speed: 10, attackerMounted: true), "A", "B", "LIVE"));
    }

    [Fact]
    public void The_cavalry_charge_throws_a_tired_line_harder_but_never_fells_everyone_and_a_fresh_line_not_at_all()
    {
        // a fresh rider with a 1.2 kg sword into a man at 40 % of his bar, standing vs galloping
        var stand = Decide(Facts(ImpactEvent.CleanHit, 0.4, speed: 0, attackerMounted: true));
        var gallop = Decide(Facts(ImpactEvent.CleanHit, 0.4, speed: 10, attackerMounted: true));
        var sprint = Decide(Facts(ImpactEvent.CleanHit, 0.4, speed: 7)); // a man on foot at a full sprint
        Assert.True(gallop.Knockdown.Chance > stand.Knockdown.Chance, gallop.Knockdown.Chance + " vs " + stand.Knockdown.Chance);
        Assert.True(gallop.Stagger.Chance > stand.Stagger.Chance);
        Assert.True(gallop.Knockdown.Chance < sprint.Knockdown.Chance, "a galloping rider's swing should carry less than a sprinting man's (+30 vs +60 %)");
        Assert.True(gallop.Knockdown.Chance < 0.5 * 0.8, "a cavalry charge fells everyone: " + gallop.Knockdown.Chance);
        // a FRESH line: the even band holds even at a gallop
        var fresh = Decide(Facts(ImpactEvent.CleanHit, 1.0, speed: 10, attackerMounted: true));
        Assert.Equal(0.0, fresh.Knockdown.Chance, 9);
        Assert.Equal(0.0, fresh.Stagger.Chance, 9);
    }

    // ------------------------------------------------------------------ a rider on the receiving end

    [Fact]
    public void A_riders_knockdown_is_knocked_off_at_half_the_chance_on_every_family_and_never_a_stagger()
    {
        foreach (var e in Every)
        {
            var foot = Decide(Facts(e, 0.0));
            var rider = Decide(Facts(e, 0.0, defenderMounted: true));
            Assert.Equal(0.5 * foot.Knockdown.Chance, rider.Knockdown.Chance, 9);
            if (foot.Stagger.InRow)
            {
                Assert.False(rider.Stagger.Happens);
                Assert.Equal(ImpactRefusal.Rider, rider.Stagger.Refusal);
            }
            // the shield: a horse does not hold it for him - the same chance as on foot
            Assert.Equal(foot.ShieldDrop.Chance, rider.ShieldDrop.Chance, 9);
        }
        // the dice: a rider's knockdown that happens = a dismount
        var d = Decide(Facts(ImpactEvent.Kick, 0.0, defenderMounted: true), dice: new Dice(0.0, 0.999, 0.5));
        Assert.True(d.Knockdown.Happens && d.Dismount && d.DefenderMounted);
        Assert.Equal(0.4, d.Knockdown.Chance, 9); // 80 % x 50 %
        // the share is a setting: 0 = never knocked off, 100 = as on foot
        Assert.Equal(0.0, Decide(Facts(ImpactEvent.Kick, 0.0, defenderMounted: true), Rules(s => s.Set(SettingsSchema.ImpactRiderDismountPercent, 0, "test"))).Knockdown.Chance, 9);
        Assert.Equal(0.8, Decide(Facts(ImpactEvent.Kick, 0.0, defenderMounted: true), Rules(s => s.Set(SettingsSchema.ImpactRiderDismountPercent, 100, "test"))).Knockdown.Chance, 9);
        // a fresh rider: nothing
        Assert.Equal(0.0, Decide(Facts(ImpactEvent.CleanHit, 1.0, defenderMounted: true)).Knockdown.Chance, 9);
    }

    [Fact]
    public void A_dismounted_rider_is_immune_to_the_next_knockdown_and_a_mounted_attacker_is_never_riposted()
    {
        var r = Rules();
        var v = default(ImpactState);
        var a = default(ImpactState);
        var first = ImpactMath.Decide(in r, Facts(ImpactEvent.CleanHit, 0.0, defenderMounted: true), ref v, ref a, 100, new Dice(0.0, 0.5));
        Assert.True(first.Dismount);
        Assert.True(v.Down(100.5)); // on the ground (the estimated fall-and-rise)
        v.GotUp(101.5, r.KnockdownImmunity);
        var again = ImpactMath.Decide(in r, Facts(ImpactEvent.CleanHit, 0.0, defenderMounted: true), ref v, ref a, 103, new Dice(0.0, 0.5));
        Assert.Equal(ImpactRefusal.KnockdownImmune, again.Knockdown.Refusal);
        // the riposte never staggers a man on a horse
        var rip = Decide(Facts(ImpactEvent.RightBlock, 1.0, attackerBar: 0.0, attackerMounted: true));
        Assert.Equal(ImpactRefusal.Rider, rip.Riposte.Refusal);
        Assert.False(rip.Riposte.Happens);
    }

    // ------------------------------------------------------------------ the on-screen lines

    [Fact]
    public void The_lines_say_what_happened_in_the_games_colours()
    {
        Assert.Equal("You are knocked down", ImpactMessageMath.Text(ImpactMessageKind.KnockedDown, ImpactMessageRole.ToYou, "Aldric", "you"));
        Assert.Equal("You are knocked off your horse", ImpactMessageMath.Text(ImpactMessageKind.KnockedOff, ImpactMessageRole.ToYou, "Aldric", "you"));
        Assert.Equal("Your shield is knocked from your hand", ImpactMessageMath.Text(ImpactMessageKind.ShieldLost, ImpactMessageRole.ToYou, null, "you"));
        Assert.Equal("Aldric ripostes - you reel", ImpactMessageMath.Text(ImpactMessageKind.Riposte, ImpactMessageRole.ToYou, "Aldric", "you"));
        Assert.Equal("You knock Aldric down", ImpactMessageMath.Text(ImpactMessageKind.KnockedDown, ImpactMessageRole.ByYou, "you", "Aldric"));
        Assert.Equal("You knock Aldric off his horse", ImpactMessageMath.Text(ImpactMessageKind.KnockedOff, ImpactMessageRole.ByYou, "you", "Aldric"));
        Assert.Equal("You break Aldric's guard", ImpactMessageMath.Text(ImpactMessageKind.GuardBroken, ImpactMessageRole.ByYou, "you", "Aldric"));
        Assert.Equal("You knock the shield from Aldric's hand", ImpactMessageMath.Text(ImpactMessageKind.ShieldLost, ImpactMessageRole.ByYou, "you", "Aldric"));
        Assert.Equal("Your riposte - Aldric reels", ImpactMessageMath.Text(ImpactMessageKind.Riposte, ImpactMessageRole.ByYou, "you", "Aldric"));
        Assert.Equal("Rhagaea knocks Aldric off his horse", ImpactMessageMath.Text(ImpactMessageKind.KnockedOff, ImpactMessageRole.YourMen, "Rhagaea", "Aldric"));
        Assert.Equal("someone knocks Aldric down", ImpactMessageMath.Text(ImpactMessageKind.KnockedDown, ImpactMessageRole.YourMen, null, "Aldric"));
        // the game's own combat-line colours (Mission.cs: received / delivered couched lance damage)
        Assert.Equal("#D65252FF", ImpactMessageMath.Hex(ImpactMessageRole.ToYou));
        Assert.Equal("#AE4AD9FF", ImpactMessageMath.Hex(ImpactMessageRole.ByYou));
        Assert.Equal("#FFFFFFFF", ImpactMessageMath.Hex(ImpactMessageRole.YourMen));
        Assert.Equal(ImpactMessageRole.ToYou, ImpactMessageMath.Stronger(ImpactMessageRole.ByYou, ImpactMessageRole.ToYou));
        Assert.Equal(ImpactMessageRole.ByYou, ImpactMessageMath.Stronger(ImpactMessageRole.YourMen, ImpactMessageRole.ByYou));
    }

    [Fact]
    public void Your_mens_lines_are_only_the_notable_ones_with_a_hero()
    {
        Assert.True(ImpactMessageMath.NotableForYourMen(ImpactMessageKind.KnockedDown, attackerYours: true, victimYours: false, victimHero: true)); // your man fells an enemy lord
        Assert.True(ImpactMessageMath.NotableForYourMen(ImpactMessageKind.KnockedOff, attackerYours: false, victimYours: true, victimHero: true)); // your companion unhorsed
        Assert.False(ImpactMessageMath.NotableForYourMen(ImpactMessageKind.KnockedDown, true, false, victimHero: false)); // a common enemy
        Assert.False(ImpactMessageMath.NotableForYourMen(ImpactMessageKind.KnockedDown, false, false, true)); // two enemies' business
        Assert.False(ImpactMessageMath.NotableForYourMen(ImpactMessageKind.GuardBroken, true, false, true)); // knocked down / off only
        Assert.False(ImpactMessageMath.NotableForYourMen(ImpactMessageKind.KnockedDown, true, true, true)); // friendly fire is not a moment
    }

    [Fact]
    public void One_line_at_most_every_1_5_s_what_comes_meanwhile_merged_into_the_next()
    {
        var q = new ImpactMessageQueue();
        Assert.False(q.TryTake(10, 1.5, out _, out _)); // nothing waits
        q.Add(ImpactMessageKind.KnockedDown, ImpactMessageRole.ByYou, "You knock Aldric down");
        Assert.True(q.TryTake(10, 1.5, out var line, out var role)); // the first at once
        Assert.Equal("You knock Aldric down", line);
        Assert.Equal(ImpactMessageRole.ByYou, role);
        // within 1.5 s: two more wait, merged - "You" mid-line becomes "you", a name keeps its capital; the colour: to you wins
        q.Add(ImpactMessageKind.ShieldLost, ImpactMessageRole.ToYou, "Your shield is knocked from your hand");
        q.Add(ImpactMessageKind.KnockedDown, ImpactMessageRole.YourMen, "Rhagaea knocks Derthert down");
        Assert.False(q.TryTake(10.5, 1.5, out _, out _));
        Assert.False(q.TryTake(11.49, 1.5, out _, out _));
        Assert.True(q.TryTake(11.5, 1.5, out line, out role));
        Assert.Equal("Your shield is knocked from your hand; Rhagaea knocks Derthert down", line);
        Assert.Equal(ImpactMessageRole.ToYou, role);
        Assert.Equal(2, q.Shown);
        Assert.Equal(1, q.Merged);
        // the same part twice in one line once; beyond 3 parts "(+N more)"
        q.Add(ImpactMessageKind.KnockedDown, ImpactMessageRole.ByYou, "You knock A down");
        q.Add(ImpactMessageKind.KnockedDown, ImpactMessageRole.ByYou, "You knock A down");
        q.Add(ImpactMessageKind.KnockedDown, ImpactMessageRole.ByYou, "You knock B down");
        q.Add(ImpactMessageKind.KnockedOff, ImpactMessageRole.ByYou, "You knock C off his horse");
        q.Add(ImpactMessageKind.GuardBroken, ImpactMessageRole.ByYou, "You break D's guard");
        q.Add(ImpactMessageKind.GuardBroken, ImpactMessageRole.ByYou, "You break E's guard");
        Assert.True(q.TryTake(13.0, 1.5, out line, out _));
        Assert.Equal("You knock A down; you knock B down; you knock C off his horse (+2 more)", line);
        Assert.Equal(1, q.Duplicates);
        Assert.Equal(2, q.Overflow);
        Assert.Equal(1 + 4, q.Merged);
        Assert.Equal(9, q.Offered);
        Assert.Equal(0, q.Waiting);
        // a new mission's clock (time going back) is old enough; a gap of 0 = every tick
        q.Add(ImpactMessageKind.KnockedDown, ImpactMessageRole.ToYou, "You are knocked down");
        Assert.True(q.TryTake(2.0, 1.5, out _, out _));
        q.Add(ImpactMessageKind.KnockedDown, ImpactMessageRole.ToYou, "You are knocked down");
        Assert.True(q.TryTake(2.0, 0, out _, out _));
        // the switch off / the mission's end: thrown away, counted
        q.Add(ImpactMessageKind.KnockedDown, ImpactMessageRole.ToYou, "You are knocked down");
        q.Clear();
        Assert.Equal(1, q.Discarded);
        Assert.False(q.TryTake(100, 1.5, out _, out _));
        Assert.Equal(3, q.ByKind[(int)ImpactMessageKind.GuardBroken] + q.ByKind[(int)ImpactMessageKind.KnockedOff]);
        Assert.StartsWith("impact messages (step 53 - on-screen lines, ImpactShowMessages on, only yours, at most one every 1.5 s): offered 12 (to you 4, by you 7, your men 1;",
            q.SummaryLine(true, false, 1.5));
        Assert.Contains("→ lines SHOWN 5, parts MERGED into a line 5 (beyond 3 a line: 2), the same part twice 1, unshown at the end / switched off 1", q.SummaryLine(true, false, 1.5));
    }

    // ------------------------------------------------------------------ the summary

    [Fact]
    public void The_riders_summary_reads_decided_applied_and_seen()
    {
        var stats = new ImpactRiderLiveStats();
        Assert.StartsWith("impact riders (step 53): no live decision on a rider this mission (LIVE; a rider's knockdown = knocked off his horse at 50% of it)", stats.SummaryLines(true, "LIVE", "none", 50)[0]);
        var r = Rules();
        var kick = Decide(Facts(ImpactEvent.Kick, 0.0, defenderMounted: true), dice: new Dice(0.0, 0.0, 0.5)); // knocked off + the shield
        var block = Decide(Facts(ImpactEvent.WrongBlock, 0.1, defenderMounted: true), dice: new Dice(0.0, 0.999, 0.5));
        var hit = Decide(Facts(ImpactEvent.CleanHit, 0.6, defenderMounted: true));
        Assert.True(kick.Dismount && kick.ShieldDrop.Happens && block.Dismount);
        stats.NoteDecision(in kick);
        stats.NoteDecision(in block);
        stats.NoteDecision(in hit);
        stats.NoteApplied(ImpactDismountWay.Seam);
        stats.NoteApplied(ImpactDismountWay.OwnBlow);
        stats.NoteSeen(ImpactDismountWay.Seam, 0.1);
        stats.NoteNotSeen(ImpactDismountWay.OwnBlow);
        stats.NoteNotApplied(ImpactNotApplied.NoHorse);
        stats.ShieldQueued++;
        stats.ShieldDropped++;
        var lines = stats.SummaryLines(true, "LIVE", "none", 50);
        Assert.Equal(2, lines.Count);
        Assert.StartsWith("impact riders (step 53 - a knockdown on a rider = KNOCKED OFF his horse at 50% of its chance, never a stagger; LIVE): live decisions on riders kicks / bashes 1 / blocks 1 / clean hits 1", lines[0]);
        Assert.Contains("dismounts DECIDED 2 (by the rider's bar 100-75% / 75-50% / 50-25% / 25-0%: kicks / bashes 0 / 0 / 0 / 1; blocks 0 / 0 / 0 / 1; clean hits 0 / 0 / 0 / 0)", lines[0]);
        Assert.Contains("→ APPLIED 1 in the decorator's dismount seam + 1 by our own blow (blocks) → SEEN off the horse within 2.0 s 1 + 0, still mounted 0 + 1 (off after 0.10 s on average) (not applied: no horse under him by the tick 1)", lines[0]);
        Assert.StartsWith("impact riders' shields and limits: shields knocked from a rider's hand decided 1 (kicks / bashes 1, wrong-side blocks 0) → queued 1 → dropped 1", lines[1]);
        Assert.Contains("staggers REFUSED on riders (never on a horse) 1", lines[1]); // the wrong-side block's stagger had a chance
        Assert.Equal(9, ImpactLiveMath.LeverCount);
        Assert.Equal("a rider knocked off his horse", ImpactLiveMath.LeverName(ImpactLever.Dismount));
        Assert.Equal("no horse under him by the tick", ImpactLiveMath.NotAppliedName(ImpactNotApplied.NoHorse));
        Assert.Equal(1, ImpactRiderMath.FamilyIndex(ImpactEvent.Parry));
        Assert.Equal(0, ImpactRiderMath.FamilyIndex(ImpactEvent.Bash));
        Assert.Equal(2, ImpactRiderMath.FamilyIndex(ImpactEvent.CleanHit));
    }
}

using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

public class AthleticsStatsTests
{
    private static readonly AthleticsRules Defaults = AthleticsRules.From(new TraxSettings());

    private static string Action(int code) => "action" + code;

    private static List<string> Lines(AthleticsStats s, params KeyValuePair<string, FormationAthleticsStats>[] formations)
        => s.SummaryLines(Defaults, Action, formations.ToList());

    [Fact]
    public void An_empty_mission_says_so_line_by_line()
    {
        var lines = Lines(new AthleticsStats());
        Assert.Equal("Athletics settings at the end: ON - pool = the Athletics skill x1.00, at least 50; full strength at 75% of the pool and above; "
            + "cost per blow 10.0 / hero 7.5 / party leader 5.6 points; a kick or shield bash 3.00 / hero 2.25 / party leader 1.69 points; "
            + "a blocked blow costs the defender: shield right side 1.00 / wrong side 5.00 / weapon parry 2.00 points (a hero x0.75, a party leader x0.56), misses cost: yes; when empty: attacks at 20%, run x0.70, horses x1.00 (never slowed); "
            + "damage upside follows Athletics: yes; wounds cap the pool: yes; refill after 3.0 s rest: empty to full in 60 s at a walk or slower "
            + "(up to 0.40 of top speed), x0.50 at a full run, near full at 50% of the rate near empty (at a walk: half the bar in 25 s, the peak line in 41 s)", lines[0]);
        Assert.Contains("Athletics refill from empty to the peak line (no blow between): none this mission (40.7 s at a walk or slower with these settings)", lines);
        Assert.Contains("Athletics pools (the Athletics skill, settings at the end): no fighters tracked", lines);
        Assert.Contains("Athletics blows charged: 0 (melee swings 0, shots/throws 0, couched/braced hits 0, landed-only swings 0, landed-only shots 0) - by riders 0, on foot 0; + kicks/bashes 0 and blocks 0 (not blows - their own lines); Athletics spent 0 points (kicks/bashes 0, blocks 0 of them)", lines);
        Assert.Contains("Athletics blocks paid by the defender 0 (0.0 points; by riders 0): shield right side 0 (0.0), shield WRONG side 0 (0.0), weapon parries 0 (0.0) | by you 0 (0.0), AI heroes 0 (0.0), other AI 0 (0.0) | free (cost 0) 0, while Athletics was off 0, the same blow seen again (not charged twice) 0, missiles stopped by a shield (free) 0; each blocked blow charged once to the defender; never an attack pause or a step back", lines);
        Assert.Contains("Athletics kicks/bashes charged 0 (0.0 points; by riders 0): kicks 0, shield bashes 0, at their hit with no kick or bash seen 0 | seen starting: kicks 0 (channel 1 0, channel 0 0), shield bashes 0 (channel 1 0, channel 0 0); kick/bash hits 0; free (CostPerKickOrBash 0) 0; each charged once, when it starts; never an attack pause", lines);
        Assert.Contains("Athletics exhaustions (empty, f 0): 0 entered, 0 left; the peak zone: left 0 times (a blow took a fighter below his line), re-entered 0 times (by refill)", lines);
        Assert.Contains("Athletics fighter-time by f (the share of his peak line left): no fighter-time recorded", lines);
        Assert.Contains("Athletics heroes: 0 flagged, 0 party leaders; lowest a hero reached: n/a (no heroes)", lines);
        Assert.Contains("Athletics you: no player fighter this mission", lines);
        Assert.Contains("Athletics your formations at the end: none with men in them", lines);
        Assert.Contains("Athletics health cap: 0 cuts (a wound pulled Athletics down to the health left), biggest 0.0 points, 0 points in all", lines);
        Assert.Contains("Athletics tick cost: no ticks", lines);
        Assert.Contains("Athletics errors: none", lines);
        Assert.DoesNotContain(lines, l => l.StartsWith("attack speed check", StringComparison.Ordinal)); // superseded by the attack-rate lines (step 5e)
        Assert.Contains(lines, l => l.StartsWith("speed updates: 0 recomputes asked", StringComparison.Ordinal) && l.EndsWith("(the attack timings by f: the \"attack rate\" lines)", StringComparison.Ordinal));
        Assert.Contains("run speed check, on foot (÷ the fighter's own top speed when fresh), by f: no samples", lines);
        Assert.Contains("run speed check, horses (÷ the horse's own top speed while its rider was fresh), by the rider's f: MountMinSpeedMultiplier 1.00 = horses never slow - no samples", lines);
    }

    [Fact]
    public void Switched_off_is_said_first()
    {
        var s = new TraxSettings();
        s.Set(SettingsSchema.AthleticsEnabled, false, SettingSources.Mcm);
        var lines = new AthleticsStats().SummaryLines(AthleticsRules.From(s), Action, new List<KeyValuePair<string, FormationAthleticsStats>>());
        Assert.Equal("Athletics settings at the end: OFF (AthleticsEnabled) - everyone full, no penalty", lines[0]);
    }

    [Fact]
    public void Pools_come_from_the_skill_with_the_rules_at_the_end()
    {
        var s = new AthleticsStats();
        foreach (int skill in new[] { 20, 20, 40, 130, 170 }) s.AddFighter(skill, skillKnown: true);
        s.AddFighter(0, skillKnown: false);
        s.PlayerSeen = true;
        s.PlayerSkill = 180;
        s.LeaderSkills.Add(new KeyValuePair<string, int>("Derthert", 250));
        s.LeaderSkills.Add(new KeyValuePair<string, int>("you", 180));
        Assert.True(s.Pools(Defaults, out double min, out double mean, out double max, out int atFloor));
        Assert.Equal(50, min);
        Assert.Equal(170, max);
        Assert.Equal((50 + 50 + 50 + 130 + 170 + 50) / 6.0, mean, 9);
        Assert.Equal(4, atFloor);
        Assert.Contains("Athletics pools (the Athletics skill x1.00, at least 50; settings at the end): 6 fighters - min 50 / avg 83.3 / max 170; "
            + "4 at the floor, 1 whose skill could not be read (the floor); you 180 (skill 180); party leaders: Derthert 250, you 180", Lines(s));

        var s2 = new TraxSettings();
        s2.Set(SettingsSchema.AthleticsPoolFloor, 0, SettingSources.Mcm);
        Assert.True(s.Pools(AthleticsRules.From(s2), out min, out _, out _, out atFloor));
        Assert.Equal(1, min);      // the unknown skill: never below 1 point
        Assert.Equal(0, atFloor);
    }

    [Fact]
    public void Blows_detection_heroes_player_formations_regen_and_cost_are_reported()
    {
        var s = new AthleticsStats();
        s.AddCharge(BlowKind.Melee, mounted: false, 10);
        s.AddCharge(BlowKind.Melee, mounted: true, 5.625);
        s.AddCharge(BlowKind.Ranged, mounted: false, 7.5);
        s.AddCharge(BlowKind.Couched, mounted: true, 10);
        s.MeleeReleasesSeen = 2;
        s.MeleeReleasesMounted = 1;
        s.ShotsSeen = 1;
        s.ExtraProjectiles = 2;
        s.RangedReleasesPolled = 1;
        s.MeleeHitsInRelease = 3;
        s.MeleeHitsOnFoot = 2;
        s.MeleeHitsMounted = 2;
        s.AddHitOutsideRelease(35);
        s.AddKickOrBashSeen(KickBashKind.Kick, lowerChannel: true);
        s.AddKickOrBashSeen(KickBashKind.Bash, lowerChannel: false);
        s.AddKickOrBashSeen(KickBashKind.Bash, lowerChannel: false);
        s.AddKickOrBashSeen(KickBashKind.None, lowerChannel: false); // not a kick or bash: not counted
        s.AddKickOrBashCharge(KickBashKind.Kick, mounted: false, 3);
        s.AddKickOrBashCharge(KickBashKind.Bash, mounted: false, 1.6875);
        s.AddKickOrBashCharge(KickBashKind.None, mounted: false, 2.25); // at its hit, no channel showed it
        s.KickOrBashFree = 1;
        s.AddBlockCharge(BlockKind.ShieldRightSide, you: true, hero: true, mounted: false, 0.5625);   // you, a hero party leader
        s.AddBlockCharge(BlockKind.ShieldWrongSide, you: false, hero: false, mounted: false, 5);
        s.AddBlockCharge(BlockKind.WeaponParry, you: false, hero: true, mounted: true, 1.5);          // an AI hero on a horse
        s.AddBlockCharge(BlockKind.None, you: false, hero: false, mounted: false, 9);                 // not a block: not counted
        s.BlocksFree = 2;
        s.BlocksWhileOff = 3;
        s.BlocksSameBlow = 4;
        s.MissilesBlockedByShield = 5;
        s.KickOrBashHits = 3;
        s.ExhaustionsEntered = 2;
        s.ExhaustionsLeft = 1;
        s.PeakLeft = 7;
        s.PeakEntered = 4;
        s.HeroesFlagged = 3;
        s.LeaderNames.Add("Derthert");
        s.LeaderNames.Add("you");
        s.LowestHeroName = "Rhagaea";
        s.LowestHeroPoints = 12.5;
        s.LowestHeroPool = 100;
        s.PlayerSeen = true;
        s.PlayerSkill = 180;
        s.PlayerBlows = 18;
        s.PlayerExhaustions = 1;
        s.PlayerLowestPoints = 0;
        s.PlayerPool = 180;
        s.RegenWalkSeconds = 120.4;
        s.RegenFasterSeconds = 30;
        s.RegenFasterRateSeconds = 21;
        s.RefillsToFull = 4;
        s.RefillsToHealthCap = 1;
        s.AddEmptyToPeak(40.8);
        s.AddEmptyToPeak(44.7);
        s.AddEmptyToPeak(55.5);
        s.AddEmptyToPeak(0);     // not a run: ignored
        s.AddTick(500, 0.2);
        s.AddTick(1000, 0.6);
        s.FighterRecomputes = 3;
        s.HorseRecomputes = 1;
        s.AddDecoratorScaled(attack: true, run: true, mount: false);
        s.AddDecoratorScaled(attack: false, run: false, mount: true);

        var inf = new MeanStd();
        inf.Add(64);
        inf.Add(80);
        var infF = new MeanStd();
        infF.Add(0.64);
        infF.Add(0.80);
        var infShare = new MeanStd();
        infShare.Add(0.8533);
        infShare.Add(1.0);
        var lines = Lines(s,
            new KeyValuePair<string, FormationAthleticsStats>("Infantry", FormationAthleticsStats.From(inf, infF, infShare, 0, 1)),
            new KeyValuePair<string, FormationAthleticsStats>("Archers", FormationAthleticsStats.From(inf, infF, 0)));

        Assert.Contains("Athletics blows charged: 4 (melee swings 2, shots/throws 1, couched/braced hits 1, landed-only swings 0, landed-only shots 0) - by riders 2, on foot 2; + kicks/bashes 3 and blocks 3 (not blows - their own lines); Athletics spent 47 points (kicks/bashes 7, blocks 7 of them)", lines);
        Assert.Contains("Athletics blocks paid by the defender 3 (7.1 points; by riders 1): shield right side 1 (0.6), shield WRONG side 1 (5.0), weapon parries 1 (1.5) | by you 1 (0.6), AI heroes 1 (1.5), other AI 1 (5.0) | free (cost 0) 2, while Athletics was off 3, the same blow seen again (not charged twice) 4, missiles stopped by a shield (free) 5; each blocked blow charged once to the defender; never an attack pause or a step back", lines);
        Assert.Equal(3, s.BlocksCharged);
        Assert.Equal(1, s.Blocks(BlockKind.ShieldWrongSide));
        Assert.Contains("Athletics detection: melee releases seen 2 (mounted 1) | shots seen 1 (+2 extra projectiles of the same shot ignored) | ranged releases seen by the poll 1 | melee hits by fighters 4 (on foot 2, mounted 2): during a counted release 3, outside one 1 [in action: action35 1]", lines);
        Assert.Contains("Athletics kicks/bashes charged 3 (6.9 points; by riders 0): kicks 1, shield bashes 1, at their hit with no kick or bash seen 1 | seen starting: kicks 1 (channel 1 0, channel 0 1), shield bashes 2 (channel 1 2, channel 0 0); kick/bash hits 3; free (CostPerKickOrBash 0) 1; each charged once, when it starts; never an attack pause", lines);
        Assert.Equal(3, s.KickOrBashCharged);
        Assert.Equal(4, s.ChargedTotal); // blows only
        Assert.Contains("Athletics free (never charged): couched hits within one blow-length of the last 0, attacks while Athletics was off 0, releases / shots waiting for a landed hit (misses cost: no) 0 / 0", lines);
        Assert.Contains("Athletics exhaustions (empty, f 0): 2 entered, 1 left; the peak zone: left 7 times (a blow took a fighter below his line), re-entered 4 times (by refill)", lines);
        Assert.Contains("Athletics heroes: 3 flagged, 2 party leaders (Derthert, you); lowest a hero reached: Rhagaea 12.5 of 100", lines);
        Assert.Contains("Athletics you: skill 180 → pool 180; 18 blows, 1 exhaustion, lowest 0.0 of 180", lines);
        Assert.Contains("Athletics your formations at the end: Infantry 72 ± 8 (2 men) f avg 0.93, 1 at full strength | Archers 72 ± 8 (2 men)", lines);
        Assert.Contains("Athletics regen: 150 fighter-seconds refilling - at a walk or slower (effort up to 0.40) 120 s at the walking rate (x1), faster 30 s at avg x0.70; refills to the top: 4 to full, 1 to a wound's cap; "
            + "refill curve: near full x0.50 of near empty (RegenRateNearFullPercent 50), x1.39 → x0.69 of a flat refill", lines);
        Assert.Contains("Athletics refill from empty to the peak line (no blow between): 3 runs, avg 47.0 s (fastest 40.8 s, slowest 55.5 s) - 40.7 s at a walk or slower with these settings, longer while moving faster than a walk", lines);
        Assert.Contains(lines, l => l.StartsWith("speed updates: 4 recomputes asked (UpdateAgentProperties: fighters 3, horses 1; a change below x0.05 waits; 0 held a tick by the per-tick budget), "
            + "the decorator applied attack penalties in 1 recomputes, run penalties in 1, horse penalties in 1 (the attack timings by f: the \"attack rate\" lines)", StringComparison.Ordinal));
        Assert.Contains("Athletics tick cost: avg 0.400 ms, max 0.600 ms per tick over 2 ticks; fighters polled avg 750, max 1000", lines);
    }

    [Fact]
    public void The_refill_curve_in_words_curved_and_flat()
    {
        var s = new TraxSettings();
        Assert.Equal("near full at 50% of the rate near empty (at a walk: half the bar in 25 s, the peak line in 41 s)", AthleticsStats.RefillCurveText(AthleticsRules.From(s)));
        s.Set(SettingsSchema.RegenRateNearFullPercent, 100, SettingSources.Mcm);
        var flat = AthleticsRules.From(s);
        Assert.Equal("the same rate all the way (RegenRateNearFullPercent 100)", AthleticsStats.RefillCurveText(flat));
        Assert.Equal("flat (RegenRateNearFullPercent 100)", AthleticsStats.RefillCurveShort(flat));
        Assert.EndsWith("x0.50 at a full run, the same rate all the way (RegenRateNearFullPercent 100)", AthleticsStats.DescribeRules(flat), StringComparison.Ordinal);
        Assert.Contains("Athletics refill from empty to the peak line (no blow between): none this mission (45.0 s at a walk or slower with these settings)",
            new AthleticsStats().SummaryLines(flat, c => "a" + c, new List<KeyValuePair<string, FormationAthleticsStats>>()));
        s.Set(SettingsSchema.RegenRateNearFullPercent, 25, SettingSources.Mcm);          // a deeper curve: faster at first
        Assert.Equal("near full at 25% of the rate near empty (at a walk: half the bar in 20 s, the peak line in 36 s)", AthleticsStats.RefillCurveText(AthleticsRules.From(s)));
    }

    [Fact]
    public void Fighter_time_by_f_and_the_health_cap()
    {
        var s = new AthleticsStats();
        s.AddPeakTime(1.0, 60);
        s.AddPeakTime(0.7, 20);
        s.AddPeakTime(0.2, 15);
        s.AddPeakTime(0.0, 5);
        s.AddPeakTime(0.5, 0);   // no time: ignored
        s.AddHealthCut(12.4);
        s.AddHealthCut(40);
        var lines = Lines(s);
        Assert.Contains("Athletics fighter-time by f (the share of his peak line left): peak (f 1) 60.0%, f 0.5-1 20.0%, f below 0.5 15.0%, empty (f 0) 5.0% of 100 fighter-seconds", lines);
        Assert.Contains("Athletics health cap: 2 cuts (a wound pulled Athletics down to the health left), biggest 40.0 points, 52 points in all", lines);

        var off = new TraxSettings();
        off.Set(SettingsSchema.HealthCapsAthletics, false, SettingSources.Mcm);
        Assert.Contains("Athletics health cap: off (HealthCapsAthletics) - 2 cuts while it was on",
            s.SummaryLines(AthleticsRules.From(off), Action, new List<KeyValuePair<string, FormationAthleticsStats>>()));
    }

    [Fact]
    public void Walk_vs_run_speeds_and_the_effort_seconds()
    {
        var s = new AthleticsStats();
        s.FootWalk.Add(1.8);
        s.FootWalk.Add(1.8);
        s.FootTop.Add(4.4);
        s.FootTop.Add(4.6);
        s.AddEffort(0.05, 2.0);
        s.AddEffort(0.39, 3.0);
        s.AddEffort(0.95, 1.0);
        s.AddEffort(1.0, 1.0);
        s.AddEffort(1.2, 1.0);
        s.AddEffort(-1, 1);   // ignored
        s.AddEffort(0.5, 0);  // no time: ignored
        Assert.Equal(8.0, s.EffortSeconds, 9);
        var lines = Lines(s);
        Assert.Contains("walk vs run speeds (tune WalkEffortFraction, now 0.40): on foot walk limit avg 1.80 m/s (n 2), top avg 4.50 m/s (n 2) → walk/top 0.40; horses walk n/a, top n/a", lines);
        Assert.Contains("Athletics refill effort (speed ÷ current top speed), seconds per tenth (0-0.1 … 0.9-1, above 1): 2 0 0 3 0 0 0 0 0 2 1, max 1.20", lines);
    }

    [Fact]
    public void Errors_log_the_first_per_site_and_count_the_rest()
    {
        var s = new AthleticsStats();
        Assert.True(s.AddError("athletics.tick"));
        Assert.False(s.AddError("athletics.tick"));
        Assert.True(s.AddError("speed.decorator"));
        Assert.Equal(3, s.Errors);
        Assert.Contains("Athletics errors: 3 (athletics.tick 2, speed.decorator 1) - each failed spot fell back to vanilla (no cost, no penalty); the first per place is logged as [error] with its stack", Lines(s));
    }
}

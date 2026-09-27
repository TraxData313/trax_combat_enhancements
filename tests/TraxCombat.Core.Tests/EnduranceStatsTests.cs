using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

public class EnduranceStatsTests
{
    private static readonly EnduranceRules Defaults = EnduranceRules.From(new TraxSettings());

    private static string Action(int code) => "action" + code;

    private static List<string> Lines(EnduranceStats s, params KeyValuePair<string, FormationEnduranceStats>[] formations)
        => s.SummaryLines(Defaults, Action, formations.ToList());

    [Fact]
    public void An_empty_mission_says_so_line_by_line()
    {
        var lines = Lines(new EnduranceStats());
        Assert.Equal("endurance settings at the end: ON - pool 100, cost per blow 10.0 / hero 7.5 / party leader 5.6, misses cost: yes, "
            + "exhausted attacks at 20% (recover above 0%), refill after 3.0 s rest: full in 60 s standing / 120 s moving (above 0.5 m/s)", lines[0]);
        Assert.Contains("endurance blows charged: 0 (melee swings 0, shots/throws 0, couched/braced hits 0, landed-only swings 0, landed-only shots 0) - by riders 0, on foot 0; endurance spent 0 points", lines);
        Assert.Contains("endurance exhaustions: 0 entered, 0 left", lines);
        Assert.Contains("endurance heroes: 0 flagged, 0 party leaders; lowest a hero reached: n/a (no heroes)", lines);
        Assert.Contains("endurance you: no player fighter this mission", lines);
        Assert.Contains("endurance your formations at the end: none with men in them", lines);
        Assert.Contains("endurance tick cost: no ticks", lines);
        Assert.Contains("endurance errors: none", lines);
        Assert.Contains(lines, l => l.StartsWith("attack speed check, melee - time between swings: fresh no samples | exhausted no samples - not enough samples", StringComparison.Ordinal));
    }

    [Fact]
    public void Switched_off_is_said_first()
    {
        var s = new TraxSettings();
        s.Set(SettingsSchema.EnduranceEnabled, false, SettingSources.Mcm);
        var lines = new EnduranceStats().SummaryLines(EnduranceRules.From(s), Action, new List<KeyValuePair<string, FormationEnduranceStats>>());
        Assert.Equal("endurance settings at the end: OFF (EnduranceEnabled) - everyone full, no penalty", lines[0]);
    }

    [Fact]
    public void Blows_detection_heroes_player_formations_regen_and_cost_are_reported()
    {
        var s = new EnduranceStats();
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
        s.KicksSeen = 1;
        s.BashesSeen = 2;
        s.KickOrBashHits = 3;
        s.ExhaustionsEntered = 2;
        s.ExhaustionsLeft = 1;
        s.HeroesFlagged = 3;
        s.LeaderNames.Add("Derthert");
        s.LeaderNames.Add("you");
        s.LowestHeroName = "Rhagaea";
        s.LowestHeroPoints = 12.5;
        s.LowestHeroPool = 100;
        s.PlayerSeen = true;
        s.PlayerBlows = 18;
        s.PlayerExhaustions = 1;
        s.PlayerLowestPoints = 0;
        s.PlayerPool = 100;
        s.RegenStandingSeconds = 120.4;
        s.RegenMovingSeconds = 30;
        s.RefillsToFull = 4;
        s.AddTick(500, 0.2);
        s.AddTick(1000, 0.6);
        s.SpeedUpdates = 3;
        s.AddDecoratorScaled();

        var inf = new MeanStd();
        inf.Add(64);
        inf.Add(80);
        var infF = new MeanStd();
        infF.Add(0.64);
        infF.Add(0.80);
        var lines = Lines(s, new KeyValuePair<string, FormationEnduranceStats>("Infantry", FormationEnduranceStats.From(inf, infF, 0)));

        Assert.Contains("endurance blows charged: 4 (melee swings 2, shots/throws 1, couched/braced hits 1, landed-only swings 0, landed-only shots 0) - by riders 2, on foot 2; endurance spent 33 points", lines);
        Assert.Contains("endurance detection: melee releases seen 2 (mounted 1) | shots seen 1 (+2 extra projectiles of the same shot ignored) | ranged releases seen by the poll 1 | melee hits by fighters 4 (on foot 2, mounted 2): during a counted release 3, outside one 1 [in action: action35 1]", lines);
        Assert.Contains("endurance free (never charged): kicks 1, shield bashes 2, kick/bash hits 3, couched hits within one blow-length of the last 0, attacks while endurance was off 0, releases / shots waiting for a landed hit (misses cost: no) 0 / 0", lines);
        Assert.Contains("endurance exhaustions: 2 entered, 1 left", lines);
        Assert.Contains("endurance heroes: 3 flagged, 2 party leaders (Derthert, you); lowest a hero reached: Rhagaea 12.5 of 100", lines);
        Assert.Contains("endurance you: 18 blows, 1 exhaustion, lowest 0.0 of 100", lines);
        Assert.Contains("endurance your formations at the end: Infantry 72 ± 8 (2 men)", lines);
        Assert.Contains("endurance regen: 120 fighter-seconds standing, 30 moving; 4 refills to full", lines);
        Assert.Contains("attack speed updates: 3 recomputes asked (UpdateAgentProperties), the decorator applied a penalty in 1 recomputes; 0 intervals spanning a change of state left out", lines);
        Assert.Contains("endurance tick cost: avg 0.400 ms, max 0.600 ms per tick over 2 ticks; fighters polled avg 750, max 1000", lines);
    }

    [Fact]
    public void Speeds_for_step_5c_and_the_effort_histogram()
    {
        var s = new EnduranceStats();
        s.FootWalk.Add(1.5);
        s.FootWalk.Add(1.7);
        s.FootTop.Add(4.0);
        s.FootTop.Add(4.0);
        s.AddEffort(0.05);
        s.AddEffort(0.95);
        s.AddEffort(1.0);
        s.AddEffort(1.2);
        s.AddEffort(-1); // ignored
        Assert.Equal(4, s.EffortSamples);
        var line = Lines(s).Single(l => l.StartsWith("speeds for step 5c", StringComparison.Ordinal));
        Assert.Equal("speeds for step 5c: on foot walk limit avg 1.60 m/s (n 2), top avg 4.00 m/s (n 2) → walk/top 0.40; horses walk n/a, top n/a; "
            + "refill samples speed/top in tenths (0-0.1 … 0.9-1, above 1): 1 0 0 0 0 0 0 0 0 2 1, max 1.20", line);
    }

    [Fact]
    public void Errors_log_the_first_per_site_and_count_the_rest()
    {
        var s = new EnduranceStats();
        Assert.True(s.AddError("endurance.tick"));
        Assert.False(s.AddError("endurance.tick"));
        Assert.True(s.AddError("speed.decorator"));
        Assert.Equal(3, s.Errors);
        Assert.Contains("endurance errors: 3 (endurance.tick 2, speed.decorator 1) - each failed spot fell back to vanilla (no cost, no penalty); the first per place is logged as [error] with its stack", Lines(s));
    }
}

using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 106d - OPENINGS, THE PAYOFF LIVE, the pure part (AI_NOTES "106d - built", DESIGN decision 95 (l)): the perfect strike's multiplier by the
/// STRIKER's weapon everywhere (4 → x2-x4, 3 → x2-x3, 2 → x1.5-x2), the defensive verdict by the weapon = the red arrow's sides (every blocked
/// direction x every strike x sword / axe / spear, on and off), an opening's own verdict by its noted sides, the book's opening window, the
/// switches (an opening's window needs OpeningsEnabled, not TimedBlocksEnabled), the opening's reel cause and step 105 on it, config format 30
/// (OpeningsEnabled on once, the flat multipliers dropped), the [summary] lines, no allocation.
/// </summary>
public class Step106dTests
{
    private static TraxSettings Settings(Action<TraxSettings>? tune = null)
    {
        var s = new TraxSettings();
        s.Set(SettingsSchema.ModEnabled, 1, "test");
        s.Set(SettingsSchema.AthleticsEnabled, 1, "test");
        s.Set(SettingsSchema.ImpactEnabled, 1, "test");
        s.Set(SettingsSchema.TimedBlocksEnabled, 1, "test");
        s.Set(SettingsSchema.PerfectStrikeEnabled, 1, "test");
        s.Set(SettingsSchema.OpeningsEnabled, 1, "test");
        tune?.Invoke(s);
        return s;
    }

    private static PerfectStrikeRules Rules(Action<TraxSettings>? tune = null) => PerfectStrikeRules.From(Settings(tune));

    private const int Up = PerfectStrikeMath.Up, Down = PerfectStrikeMath.Down, Left = PerfectStrikeMath.Left, Right = PerfectStrikeMath.Right;
    private const int Sword = OpeningMath.AllAttacks;
    private static readonly int Axe = OpeningMath.WeaponMask(true, false);
    private static readonly int Spear = OpeningMath.WeaponMask(false, true);

    private static int B(int attack) => OpeningMath.Bit(attack);

    // ------------------------------------------------------------------ A. the multiplier by the weapon, everywhere

    [Theory]
    [InlineData(OpeningMath.AllAttacks, 0.0, 2.0)]
    [InlineData(OpeningMath.AllAttacks, 0.5, 3.0)]
    [InlineData(OpeningMath.AllAttacks, 1.0, 4.0)]
    [InlineData(0b1101, 0.0, 2.0)] // an axe: overhead + left + right
    [InlineData(0b1101, 1.0, 3.0)]
    [InlineData(0b0011, 0.0, 1.5)] // a spear: its two stabs
    [InlineData(0b0011, 1.0, 2.0)]
    public void The_multiplier_follows_the_strikers_weapon_directions(int weapon, double score, double k)
    {
        Assert.Equal(Axe, 0b1101);
        Assert.Equal(Spear, 0b0011);
        var r = Rules();
        Assert.Equal(k, PerfectStrikeMath.Multiplier(score, in r, weapon), 9);
        // the openings' own math reads the same numbers
        Assert.Equal(k, OpeningMath.StrikeMultiplier(OpeningRules.From(Settings()), weapon, score), 9);
    }

    [Fact]
    public void The_ranges_are_settings_and_never_upside_down()
    {
        var r = Rules(s =>
        {
            s.Set(SettingsSchema.PerfectStrikeFourWayMin, 3, "test");
            s.Set(SettingsSchema.PerfectStrikeFourWayMax, 2, "test"); // below min - held at min
        });
        Assert.Equal(3, r.FourMax, 9);
        Assert.Equal(3, PerfectStrikeMath.Multiplier(1, in r, Sword), 9);
        Assert.Contains("x2.0-4.0 / x2.0-3.0 / x1.5-2.0", Rules().Describe());
        Assert.DoesNotContain(SettingsSchema.All, p => p.Key == "PerfectStrikeMinMultiplier" || p.Key == "PerfectStrikeMaxMultiplier");
    }

    // ------------------------------------------------------------------ B. the defensive verdict = the arrow's sides

    [Fact]
    public void The_defensive_verdict_scores_exactly_the_sides_the_arrow_shows()
    {
        foreach (bool finishes in new[] { true, false })
            foreach (int weapon in new[] { Sword, Axe, Spear })
                for (int blocked = -1; blocked <= Right; blocked++)
                {
                    int arrow = OpeningArrowMath.WindowFinishMask(false, true, blocked, weapon, finishes);
                    for (int strike = Up; strike <= Right; strike++)
                    {
                        var v = PerfectStrikeMath.Verdict(false, true, blocked, strike, weapon, finishes);
                        if (blocked < 0) Assert.Equal(StrikeVerdict.BlockedUnknown, v);
                        else Assert.Equal((arrow & B(strike)) != 0, v == StrikeVerdict.Perfect);
                    }
                }
    }

    [Fact]
    public void An_axe_finishes_his_blocked_overhead_with_left_or_right_a_spear_his_side_swing_with_either_stab()
    {
        // an axe cannot thrust: his overhead (wanted: your thrust) is finished by left OR right, never the overhead
        Assert.Equal(StrikeVerdict.Perfect, PerfectStrikeMath.Verdict(Up, Left, Axe, true));
        Assert.Equal(StrikeVerdict.Perfect, PerfectStrikeMath.Verdict(Up, Right, Axe, true));
        Assert.Equal(StrikeVerdict.WrongSide, PerfectStrikeMath.Verdict(Up, Up, Axe, true));
        Assert.Equal(StrikeVerdict.WrongSide, PerfectStrikeMath.Verdict(Up, Left, Axe, false)); // off = strict opposites
        // a sword keeps step 103's strict opposites (it can thrust)
        Assert.Equal(StrikeVerdict.Perfect, PerfectStrikeMath.Verdict(Up, Down, Sword, true));
        Assert.Equal(StrikeVerdict.WrongSide, PerfectStrikeMath.Verdict(Up, Left, Sword, true));
        // a spear: his right swing (wanted: your right swing) - either stab; his overhead - the other stab
        Assert.Equal(StrikeVerdict.Perfect, PerfectStrikeMath.Verdict(Right, Up, Spear, true));
        Assert.Equal(StrikeVerdict.Perfect, PerfectStrikeMath.Verdict(Right, Down, Spear, true));
        Assert.Equal(StrikeVerdict.Perfect, PerfectStrikeMath.Verdict(Up, Down, Spear, true));
        Assert.Equal(StrikeVerdict.WrongSide, PerfectStrikeMath.Verdict(Up, Up, Spear, true));
        // a rider with any side - any hit
        Assert.Equal(StrikeVerdict.Perfect, PerfectStrikeMath.Verdict(true, true, Up, Up, Sword, true));
        Assert.Equal(StrikeVerdict.Unknown, PerfectStrikeMath.Verdict(Up, -1, Sword, true));
    }

    // ------------------------------------------------------------------ C. the opening's window

    [Fact]
    public void An_openings_window_scores_its_noted_sides()
    {
        // your right swing staggered him: the opposite is your left swing (a sword); an axe's overhead: left or right
        int sword = OpeningMath.FinishAfterYourSwing(OpeningMath.AttackRight, Sword, true);
        Assert.Equal(B(Left), sword);
        Assert.Equal(StrikeVerdict.Perfect, PerfectStrikeMath.OpeningVerdict(sword, Left));
        Assert.Equal(StrikeVerdict.WrongSide, PerfectStrikeMath.OpeningVerdict(sword, Right));
        int axe = OpeningMath.FinishAfterYourSwing(OpeningMath.AttackUp, Axe, true);
        Assert.Equal(B(Left) | B(Right), axe);
        Assert.Equal(StrikeVerdict.Perfect, PerfectStrikeMath.OpeningVerdict(axe, Right));
        Assert.Equal(StrikeVerdict.Unknown, PerfectStrikeMath.OpeningVerdict(axe, -1));
        Assert.Equal(StrikeVerdict.BlockedUnknown, PerfectStrikeMath.OpeningVerdict(0, Left));

        var book = new PerfectStrikeBook();
        book.Open(5, Right, 1, 10, out _);
        book.OpenOpening(5, axe, 0.5, 11, out bool dropped, out _);
        Assert.True(dropped); // the same man staggered again - the old window replaced
        Assert.Equal(1, book.Count);
        Assert.True(book[0].Opening);
        Assert.Equal(axe, book[0].FinishMask);
        Assert.Equal(-1, book[0].Blocked);
        Assert.Equal(0.5, book[0].Score, 9);
        book.Open(5, Right, 1, 12, out _); // a defensive window on him again: not an opening any more
        Assert.False(book[0].Opening);
        Assert.Equal(0, book[0].FinishMask);
    }

    [Fact]
    public void An_openings_window_needs_the_openings_and_the_perfect_strike_but_not_timed_blocks()
    {
        Assert.True(Rules().OpeningOn);
        Assert.True(Rules(s => s.Set(SettingsSchema.TimedBlocksEnabled, 0, "test")).OpeningOn);
        Assert.False(Rules(s => s.Set(SettingsSchema.TimedBlocksEnabled, 0, "test")).Enabled);
        Assert.False(Rules(s => s.Set(SettingsSchema.OpeningsEnabled, 0, "test")).OpeningOn);
        Assert.False(Rules(s => s.Set(SettingsSchema.PerfectStrikeEnabled, 0, "test")).OpeningOn);
        Assert.False(Rules(s => s.Set(SettingsSchema.ImpactEnabled, 0, "test")).OpeningOn);
        var off = Rules(s => s.Set(SettingsSchema.ModEnabled, 0, "test"));
        Assert.False(off.OpeningOn);
        Assert.Equal("ModEnabled", off.OpeningOffBecause); // the master switch first
    }

    // ------------------------------------------------------------------ D. the reel's cause and step 105

    [Fact]
    public void The_openings_reel_has_its_own_cause_and_step_105_knocks_him_down_on_any_other_hit()
    {
        Assert.Equal(ReelingHitMath.CauseCount, Enum.GetValues(typeof(ReelCause)).Length);
        Assert.Equal("an opening", ReelingHitMath.CauseName(ReelCause.Opening));
        var st = default(ImpactState);
        st.Reel(101.3, 2, ReelCause.Opening);
        Assert.True(st.Reeling(100.5));
        Assert.Equal(ReelCause.Opening, st.ReelCause);
        var r = ReelingHitRules.From(Settings(s => s.Set(SettingsSchema.ReelingHitKnockdownEnabled, 1, "test")));
        var k = new ImpactRoll { InRow = true, Chance = 0.1, Die = 0.5 };
        var o = ReelingHitMath.Decide(in r, ImpactEvent.Kick, false, true, false, true, ReelCause.Opening, false, false, false, 1, ref k, new SeededRandom(1));
        Assert.True(o.Asked);
        Assert.Equal(ReelingHitRefusal.None, o.Refusal);
    }

    // ------------------------------------------------------------------ E. config format 30

    private static double Shipped(ParamDef p) => p == SettingsSchema.OpeningsEnabled ? 1 : p.Default;

    [Fact]
    public void Format_30_turns_the_openings_on_once_and_drops_the_flat_multipliers()
    {
        Assert.Equal(30, ConfigFile.FormatVersion);
        Assert.Contains("\"OpeningsEnabled\": true,", DefaultsFile.ReadEmbeddedText(), StringComparison.Ordinal);
        Assert.DoesNotContain("PerfectStrikeMinMultiplier", DefaultsFile.ReadEmbeddedText(), StringComparison.Ordinal);
        var read = ConfigFile.Read("{ \"ConfigVersion\": 29, \"OpeningsEnabled\": false, \"PerfectStrikeMinMultiplier\": 2.5, \"PerfectStrikeMaxMultiplier\": 3 }");
        var notes = ConfigFile.Migrate(read, Shipped);
        Assert.Equal(1, read.Values["OpeningsEnabled"], 9);
        Assert.Contains(notes, n => n.StartsWith("OpeningsEnabled: false → true (format 29 → 30, the old default; step 106d", StringComparison.Ordinal));
        Assert.Contains(notes, n => n.StartsWith("PerfectStrikeMinMultiplier 2.5 dropped (format 29 → 30; step 106d", StringComparison.Ordinal));
        Assert.Contains(notes, n => n.StartsWith("PerfectStrikeMaxMultiplier 3 dropped", StringComparison.Ordinal));
        Assert.DoesNotContain(read.Unknown, u => u.Key.StartsWith("PerfectStrikeM", StringComparison.Ordinal));
        // a format-30 file keeps whatever it holds (turned off by hand stays off)
        var mine = ConfigFile.Read("{ \"ConfigVersion\": 30, \"OpeningsEnabled\": false }");
        Assert.Empty(ConfigFile.Migrate(mine, Shipped));
        Assert.Equal(0, mine.Values["OpeningsEnabled"], 9);
        // an older file never had the key - nothing to move
        Assert.DoesNotContain(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 28 }"), Shipped), n => n.StartsWith("OpeningsEnabled", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------ F. the [summary] lines, no allocation

    [Fact]
    public void The_summary_lines_count_the_live_payoff()
    {
        var o = new OpeningStats { LiveAsked = 3, LivePlayed = 3, LiveSeen = 2, LiveNotSeen = 1, LiveOnModStagger = 1, LiveKnockedDown = 1, LiveWindows = 4, OpeningShown = 4, GuardBrokenShown = 1 };
        o.NoteLiveMeasured(1.1, 1.0);
        string line = o.SummaryLine(OpeningRules.From(Settings()));
        Assert.StartsWith("openings (step 106d; live ON - the forced guard, the block roll, the stagger and the perfect strike, the shadow log ON): ", line);
        Assert.Contains("LIVE STAGGERS asked 3 (played 3, seen 2, never seen 1, not applied 0, asked 1.10 s -> measured 1.00 s), carried by the mod's own stagger 1, none - knocked down by the same hit 1", line);
        Assert.Contains("perfect-strike windows opened 4, OPENING! shown 4, GUARD BROKEN shown 1", line);

        var p = new PerfectStrikeStats { Opened = 4, OpeningOpened = 3, OpeningLanded = 2, OpeningWrongSide = 1 };
        p.NoteLanded(Left, -1, 1, 4, 10, 40);
        p.NoteWeapon(Sword, false);
        p.NoteLanded(Right, -1, 0, 2, 10, 20);
        p.NoteWeapon(Axe, true);
        string pl = p.SummaryLine(Rules());
        Assert.Contains("openings ON; x by your weapon's directions 4 / 3 / 2: x2.0-4.0 / x2.0-3.0 / x1.5-2.0", pl);
        Assert.Contains("windows opened 4 (by an opening's stagger 3), PERFECT STRIKES landed 2 (after an opening 2; by your weapon's directions 2 / 3 / 4: 0 / 1 / 1, by the weapon's relaxed sides 1)", pl);
        Assert.Contains("wrong-side hits on a man reeling from an opening 1", pl);
    }

    [Fact]
    public void The_verdicts_and_the_multiplier_allocate_nothing()
    {
        var r = Rules();
        var book = new PerfectStrikeBook();
        long bytes = NoAllocation.Bytes(() =>
        {
            for (int i = 0; i < 1000; i++)
            {
                PerfectStrikeMath.Verdict(false, true, i & 3, (i >> 2) & 3, Axe, true);
                PerfectStrikeMath.OpeningVerdict(B(Left) | B(Right), i & 3);
                PerfectStrikeMath.Multiplier(0.5, in r, Spear);
                book.OpenOpening(i & 7, B(Left), 0.5, i, out _, out _);
            }
        });
        Assert.Equal(0, bytes);
    }
}

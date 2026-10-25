using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 95 - THE STEP-IN BASH + SWING 30% OF THE TIME (Toni 2026-10-25: "try making them step in for the bash, but ... swing 30% of the time"; his
/// 20:26 / 09:53 bins: the AI never stands within 1.1 m of the enemy it fights, and a bash asked from 1.1-1.6 m mostly hits a friend or nothing;
/// AI_NOTES "Step 95", DESIGN decision 87): the three settings placed / shipped / live with the master switch first, the step's vector in his own
/// frame and its stop distance, the wall's rule, no step through a friend, the guard's clock, the new shares on seeded rolls (27 / 43 / 30 and
/// 45 / 25 / 30), the format-24 migration, the 9th summary line in plain words, 0 bytes.
/// </summary>
public class Step95Tests
{
    /// <summary>defaults.json's shipped kick rules (steps 88-95): the new shares, the narrower path, the bash reach off, the step-in on.</summary>
    private static AiKickRules Shipped(bool stepIn = true, double to = 0.95, double max = 0.6) =>
        new AiKickRules(true, true, true, 1.6, 40, 2, true, 27, 43, 45, 0.45, 0.5, 1.2, 25, 0.5, 0.4, 0, stepIn, to, max);

    /// <summary>The shipped values a migration moves to (the tests' own defaults are DESIGN's initial ones).</summary>
    private static double ShippedDefault(ParamDef p) =>
        p == SettingsSchema.AiShieldmanKickPercent ? 27 : p == SettingsSchema.AiShieldmanBashPercent ? 43 : p == SettingsSchema.AiNoShieldKickPercent ? 45
        : p == SettingsSchema.AiNoShieldPushPercent ? 25 : p == SettingsSchema.AiBashReachMetres ? 0 : p == SettingsSchema.AthleticsPoolPerSkill ? 0.5
        : p == SettingsSchema.CostPerBlow ? 7 : p == SettingsSchema.CostPerKickOrBash ? 6 : p.Default;

    [Fact]
    public void The_three_settings_sit_after_the_bash_reach_on_the_Advanced_page_ship_on_and_are_live_with_the_master_switch_first()
    {
        var all = SettingsSchema.All.ToList();
        int reach = all.IndexOf(SettingsSchema.AiBashReachMetres);
        var keys = new[] { SettingsSchema.AiBashStepIn, SettingsSchema.AiBashStepInToMetres, SettingsSchema.AiBashStepInMaxSeconds };
        for (int i = 0; i < keys.Length; i++)
        {
            Assert.Equal(reach + 1 + i, all.IndexOf(keys[i]));
            Assert.Same(SettingsSchema.ImpactGroup, keys[i].Group);
            Assert.Equal(SettingsPage.Advanced, keys[i].Group.Page);
            Assert.Equal(ApplyTiming.Live, keys[i].Timing);
        }
        Assert.Equal(ParamType.Bool, SettingsSchema.AiBashStepIn.Type);
        Assert.Equal(ParamType.Float, SettingsSchema.AiBashStepInToMetres.Type);
        Assert.Equal(2, SettingsSchema.AiBashStepInToMetres.Max, 9);
        Assert.Equal(0.95, SettingsSchema.AiBashStepInToMetres.Default, 9); // DESIGN's initial value = the shipped one
        Assert.Equal(0.6, SettingsSchema.AiBashStepInMaxSeconds.Default, 9);
        string text = DefaultsFile.ReadEmbeddedText();
        foreach (var line in new[]
                 {
                     "\"AiBashStepIn\": true,", "\"AiBashStepInToMetres\": 0.95,", "\"AiBashStepInMaxSeconds\": 0.6,", "\"AiBashReachMetres\": 0.0,",
                     "\"AiShieldmanKickPercent\": 27,", "\"AiShieldmanBashPercent\": 43,", "\"AiNoShieldKickPercent\": 45,", "\"AiNoShieldPushPercent\": 25,",
                 })
            Assert.Contains(line, text, StringComparison.Ordinal);
        var s = new TraxSettings();
        var r = AiKickRules.From(s);
        Assert.True(r.StepInOn);
        Assert.Equal(0.95, r.StepInTo, 6);
        Assert.Contains("step 95: a shieldman STEPS IN during his bash's guard until the enemy is within 0.95 m", r.Describe(), StringComparison.Ordinal);
        s.Set(SettingsSchema.AiBashStepInToMetres, 0.8, "test");
        Assert.Equal(0.8, AiKickRules.From(s).StepInTo, 6); // live
        s.Set(SettingsSchema.AiBashStepIn, false, "test");
        Assert.False(AiKickRules.From(s).StepInOn);
        Assert.Equal("AiBashStepIn off", AiKickRules.From(s).StepInOffBecause);
        s.Set(SettingsSchema.AiBashStepIn, true, "test");
        s.Set(SettingsSchema.AiBashStepInToMetres, 0, "test");
        Assert.Equal("AiBashStepInToMetres 0", AiKickRules.From(s).StepInOffBecause);
        s.Set(SettingsSchema.AiBashStepInToMetres, 0.95, "test");
        // the master switch first: off = no step (and no kick at all)
        s.Set(SettingsSchema.ModEnabled, false, "test");
        Assert.False(AiKickRules.From(s).StepInOn);
        Assert.Equal("ModEnabled", AiKickRules.From(s).StepInOffBecause);
        s.Set(SettingsSchema.ModEnabled, true, "test");
        s.Set(SettingsSchema.AiKicksEnabled, false, "test");
        Assert.False(AiKickRules.From(s).StepInOn);
        // the old constructor = no step-in (steps 65-91's callers unchanged)
        Assert.False(new AiKickRules(true, true, true, 1.6, 40, 2, true, 25, 40, 35).StepInOn);
    }

    [Fact]
    public void The_step_is_a_forward_walk_toward_the_enemy_in_his_own_frame_and_stops_at_the_stop_distance()
    {
        // facing +y (forward (0, 1), right (1, 0)), the enemy straight ahead: a full forward push (0, 1)
        Assert.True(AiBashStepInMath.Vector(0, 1.3, 1, 0, 0, 1, out double x, out double y));
        Assert.Equal(0, x, 6);
        Assert.Equal(1, y, 6);
        // facing +x (forward (1, 0), right (0, -1)), the enemy ahead at +x: the same (0, 1) - his own frame, whichever way he faces
        Assert.True(AiBashStepInMath.Vector(1.4, 0, 0, -1, 1, 0, out x, out y));
        Assert.Equal(0, x, 6);
        Assert.Equal(1, y, 6);
        // facing +y with the enemy ahead-left: the walk follows the line to him (left and forward), his facing untouched
        Assert.True(AiBashStepInMath.Vector(-1, 1, 1, 0, 0, 1, out x, out y));
        Assert.Equal(-Math.Sqrt(0.5), x, 6);
        Assert.Equal(Math.Sqrt(0.5), y, 6);
        Assert.False(AiBashStepInMath.Vector(0, 0, 1, 0, 0, 1, out _, out _)); // degenerate: no walk
        // the stop distance
        var r = Shipped();
        Assert.Equal(AiStepIn.Arrived, AiBashStepInMath.Decide(in r, 1.3, false, false, 1.0, out double stopAt)); // "he steps"
        Assert.Equal(0.95, stopAt, 9);
        Assert.Equal(AiStepIn.NotNeeded, AiBashStepInMath.Decide(in r, 0.95, false, false, 1.0, out _));
        Assert.Equal(AiStepIn.NotNeeded, AiBashStepInMath.Decide(in r, 0.7, false, false, 1.0, out _));
        Assert.Equal(AiStepIn.Arrived, AiBashStepInMath.Decide(in r, 0.96, false, false, 1.0, out _));
        Assert.False(AiBashStepInMath.Arrived(0.96, 0.95));
        Assert.True(AiBashStepInMath.Arrived(0.95, 0.95));
        Assert.False(AiBashStepInMath.Arrived(double.NaN, 0.95));
        Assert.Equal(AiStepIn.Off, AiBashStepInMath.Decide(Shipped(stepIn: false), 1.3, false, false, 1.0, out _));
        Assert.Equal(AiStepIn.Off, AiBashStepInMath.Decide(Shipped(to: 0), 1.3, false, false, 1.0, out _));
        // the shield wall: standing firm (or the leash 0) never steps; else at most the wall's leash closer than now
        Assert.Equal(AiStepIn.RefusedWall, AiBashStepInMath.Decide(in r, 1.5, true, true, 1.0, out _));
        Assert.Equal(AiStepIn.RefusedWall, AiBashStepInMath.Decide(in r, 1.5, true, false, 0, out _));
        Assert.Equal(AiStepIn.Arrived, AiBashStepInMath.Decide(in r, 1.5, true, false, 1.0, out stopAt));
        Assert.Equal(0.95, stopAt, 9);
        Assert.Equal(AiStepIn.Arrived, AiBashStepInMath.Decide(in r, 1.5, true, false, 0.2, out stopAt));
        Assert.Equal(1.3, stopAt, 9);
        Assert.Equal(AiStepIn.NotNeeded, AiBashStepInMath.Decide(in r, 0.9, true, true, 1.0, out _)); // close enough: no step to refuse
    }

    [Fact]
    public void The_guard_is_the_bash_lead_unless_he_is_still_walking_then_at_most_the_step_guard()
    {
        Assert.Equal(0.6, AiBashStepInMath.StepGuardSeconds(0.6), 9);
        Assert.Equal(AiKickMath.BashLeadSeconds, AiBashStepInMath.StepGuardSeconds(0.3), 9); // never shorter than the bash's own lead
        Assert.Equal(AiKickMath.BashLeadSeconds, AiBashStepInMath.StepGuardSeconds(double.NaN), 9);
        const double start = 10;
        Assert.False(AiBashStepInMath.GuardDone(start + 0.39, start, false, 0.6));
        Assert.True(AiBashStepInMath.GuardDone(start + 0.40, start, false, 0.6)); // not stepping (or arrived): the 0.4 s lead
        Assert.False(AiBashStepInMath.GuardDone(start + 0.50, start, true, 0.6)); // still walking: held
        Assert.True(AiBashStepInMath.GuardDone(start + 0.60, start, true, 0.6)); // the step guard ran out
        Assert.True(AiBashStepInMath.GuardDone(start + 0.40, start, true, 0.4)); // 0.4 = never held longer
    }

    [Fact]
    public void No_step_through_a_friend_the_walks_corridor_is_two_half_bodies_wide()
    {
        // him at the origin, the enemy 1.3 m ahead (+y), a 0.35 m step
        const double step = 0.35;
        Assert.True(AiBashStepInMath.InCorridor(0, 0, 0, 1.3, 0, 0.3, step)); // a friend right in front of him
        Assert.True(AiBashStepInMath.InCorridor(0, 0, 0, 1.3, 0.35, 0.6, step)); // ahead, 0.35 m aside - he would walk into him
        Assert.False(AiBashStepInMath.InCorridor(0, 0, 0, 1.3, 0.64, 0.35, step)); // step 90's shoulder neighbour (0.35 ahead, 0.64 aside) - clear
        Assert.False(AiBashStepInMath.InCorridor(0, 0, 0, 1.3, 0.1, -0.5, step)); // behind him
        Assert.False(AiBashStepInMath.InCorridor(0, 0, 0, 1.3, 0.0, 0.03, step)); // on his own spot (himself / his horse)
        Assert.False(AiBashStepInMath.InCorridor(0, 0, 0, 1.3, 0.0, 0.8, step)); // beyond the step + 0.4
        Assert.True(AiBashStepInMath.InCorridor(0, 0, 0, 1.3, 0.0, 0.74, step)); // within it
        // the corridor follows the line to the ENEMY, not his facing: the enemy to the right (+x), a friend at (0.5, 0.1) blocks, at (0, 0.5) not
        Assert.True(AiBashStepInMath.InCorridor(0, 0, 1.3, 0, 0.5, 0.1, step));
        Assert.False(AiBashStepInMath.InCorridor(0, 0, 1.3, 0, 0, 0.5, step));
        Assert.False(AiBashStepInMath.InCorridor(0, 0, 0, 0, 0, 0.3, step)); // degenerate - never blocks
    }

    [Fact]
    public void The_new_shares_on_seeded_rolls_are_27_43_30_with_a_shield_and_45_25_30_without()
    {
        var r = Shipped();
        Assert.False(r.ShieldSharesCut);
        Assert.False(r.NoShieldSharesCut);
        Assert.Equal(0.30, r.SwingShare(true), 9);
        Assert.Equal(0.30, r.SwingShare(false), 9);
        var dice = new SeededRandom(95);
        const int n = 100000;
        var sh = new int[AiKickMath.KindCount];
        var ns = new int[AiKickMath.KindCount];
        for (int i = 0; i < n; i++)
        {
            sh[(int)AiKickMath.Roll(in r, true, dice.NextDouble(), 1.3)]++; // the bash reach off: a bash anywhere in the kick reach
            ns[(int)AiKickMath.Roll(in r, false, dice.NextDouble(), 1.3)]++;
        }
        Near(sh[(int)AiKickKind.Kick], 27);
        Near(sh[(int)AiKickKind.Bash], 43);
        Near(sh[(int)AiKickKind.None], 30);
        Assert.Equal(0, sh[(int)AiKickKind.Push]);
        Near(ns[(int)AiKickKind.Kick], 45);
        Near(ns[(int)AiKickKind.Push], 25);
        Near(ns[(int)AiKickKind.None], 30);
        Assert.Equal(0, ns[(int)AiKickKind.Bash]);
        Assert.Contains("a SHIELDMAN kick 27% / shield bash 43% / swing 30%, a man with NO shield kick 45% / weapon push 25% / swing 30%", r.Describe(), StringComparison.Ordinal);

        static void Near(int count, double target) => Assert.InRange(100.0 * count / n, target - 0.5, target + 0.5);
    }

    [Fact]
    public void Format_24_moves_the_untouched_shares_once_keeps_a_tuned_one_and_chains_older_files()
    {
        Assert.Equal(24, ConfigFile.FormatVersion);
        var read = ConfigFile.Read("{ \"ConfigVersion\": 23, \"AiShieldmanKickPercent\": 25, \"AiShieldmanBashPercent\": 40, \"AiNoShieldKickPercent\": 35, \"AiNoShieldPushPercent\": 20 }");
        var notes = ConfigFile.Migrate(read, ShippedDefault);
        Assert.Equal(4, notes.Count);
        Assert.Equal(27, read.Values["AiShieldmanKickPercent"], 9);
        Assert.Equal(43, read.Values["AiShieldmanBashPercent"], 9);
        Assert.Equal(45, read.Values["AiNoShieldKickPercent"], 9);
        Assert.Equal(25, read.Values["AiNoShieldPushPercent"], 9);
        Assert.Equal("AiShieldmanKickPercent: 25 → 27 (format 23 → 24, the old default; step 95, Toni: swing 30% of the time - more kicks, bashes and pushes - a shieldman kicks 27% of his moves in kick reach)", notes[0]);
        Assert.StartsWith("AiShieldmanBashPercent: 40 → 43 (format 23 → 24, the old default; step 95", notes[1], StringComparison.Ordinal);
        Assert.StartsWith("AiNoShieldKickPercent: 35 → 45 (format 23 → 24, the old default; step 95", notes[2], StringComparison.Ordinal);
        Assert.StartsWith("AiNoShieldPushPercent: 20 → 25 (format 23 → 24, the old default; step 95", notes[3], StringComparison.Ordinal);
        // a tuned value is kept and said once
        var tuned = ConfigFile.Read("{ \"ConfigVersion\": 23, \"AiShieldmanKickPercent\": 30, \"AiShieldmanBashPercent\": 40 }");
        var tn = ConfigFile.Migrate(tuned, ShippedDefault);
        Assert.Equal(30, tuned.Values["AiShieldmanKickPercent"], 9);
        Assert.Equal(43, tuned.Values["AiShieldmanBashPercent"], 9);
        Assert.Equal(2, tn.Count);
        Assert.StartsWith("AiShieldmanKickPercent: 30 kept - your own value (format 23 → 24; the new default 27 is not pushed over it; step 95", tn[0], StringComparison.Ordinal);
        // a format-19 file's step-81 shares go through step 88's block straight to today's (one note each); its no-shield kick 35 → 45; no push key = nothing
        var old = ConfigFile.Read("{ \"ConfigVersion\": 19, \"AiShieldmanKickPercent\": 20, \"AiShieldmanBashPercent\": 35, \"AiNoShieldKickPercent\": 35 }");
        var on = ConfigFile.Migrate(old, ShippedDefault);
        Assert.Equal(27, old.Values["AiShieldmanKickPercent"], 9);
        Assert.Equal(43, old.Values["AiShieldmanBashPercent"], 9);
        Assert.Equal(45, old.Values["AiNoShieldKickPercent"], 9);
        Assert.Equal(3, on.Count(n => n.StartsWith("AiShieldmanKickPercent", StringComparison.Ordinal) || n.StartsWith("AiShieldmanBashPercent", StringComparison.Ordinal)
                                      || n.StartsWith("AiNoShieldKickPercent", StringComparison.Ordinal)));
        Assert.False(old.Values.ContainsKey("AiNoShieldPushPercent"));
        // a format-24 file is never touched (its 25 is the player's own); idempotent once rewritten
        var mine = ConfigFile.Read("{ \"ConfigVersion\": 24, \"AiShieldmanKickPercent\": 25, \"AiNoShieldKickPercent\": 35 }");
        Assert.Empty(ConfigFile.Migrate(mine, ShippedDefault));
        Assert.Equal(25, mine.Values["AiShieldmanKickPercent"], 9);
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 23, \"AiShieldmanKickPercent\": 27 }"), ShippedDefault));
    }

    [Fact]
    public void The_summary_says_the_step_in_in_plain_words_and_bins_each_bash_by_its_distance_at_the_bash()
    {
        var st = new AiKickStats();
        var si = st.StepIn;
        si.Note(AiStepIn.Arrived);
        si.Note(AiStepIn.Arrived);
        si.Note(AiStepIn.TimeRanOut);
        si.Note(AiStepIn.NotNeeded);
        si.Note(AiStepIn.RefusedFriend);
        si.Note(AiStepIn.RefusedWall);
        si.Note(AiStepIn.CutShort);
        si.NotePress(1.35, 0.95);
        si.NotePress(1.25, 0.93);
        si.NotePress(1.45, 1.05);
        si.Extended = 2;
        si.ExtendedSeconds = 0.3;
        si.NoteResult(0.95, AiBashResult.Enemy);
        si.NoteResult(0.93, AiBashResult.EnemyShield);
        si.NoteResult(1.05, AiBashResult.Friend);
        si.NoteResult(0.9, AiBashResult.Missed);
        si.NoteResult(double.NaN, AiBashResult.NotPlayed);
        Assert.Equal(7, si.Total);
        Assert.Equal(4, si.Stepped);
        Assert.Equal(1, si.EndedBeforeKey);
        Assert.Equal((0.40 + 0.32 + 0.40) / 3, si.MeanStep, 9);
        Assert.Equal(1.0, si.FriendShare(AiBashReachStats.BinOf(1.05)), 9);
        var lines = st.SummaryLines(Shipped()).ToList();
        Assert.Equal(9, lines.Count);
        string line = lines[8];
        Assert.Equal(si.Line(Shipped()), line);
        Assert.StartsWith("ai kicks - the step-in bash (step 95): a shieldman steps in during his bash's guard until his enemy is within 0.95 m (AiBashStepInToMetres; "
                          + "the guard held up to 0.60 s while he walks, never through a friend, a standing shield wall never) | bashes asked 7: stepped in 4 (arrived 2, "
                          + "the time ran out 1, cut short 1), not needed - already close 1, refused - a friend in the way 1, refused - held in the shield wall 1; the gap "
                          + "closed by a step 0.37 m on average (the enemy 1.35 m off at the decision → 0.98 m at the bash); the guard held longer while walking in 2 times (0.15 s more on average)",
            line, StringComparison.Ordinal);
        Assert.Contains("| bashes by the enemy's distance AT THE BASH (after the step) → on an ENEMY / into his shield / on a FRIEND / missed / horse or untracked / not played: "
                        + "0.8-1.0 m 1 / 1 / 0 / 1 / 0 / 0 (friends 0% of the hits); 1.0-1.1 m 0 / 0 / 1 / 0 / 0 / 0 (friends 100% of the hits); ended before the key 1", line, StringComparison.Ordinal);
        // off, in plain words
        var off = new AiKickStats().StepIn.Line(Shipped(stepIn: false));
        Assert.StartsWith("ai kicks - the step-in bash (step 95): the step-in is OFF (AiBashStepIn off) - a shieldman bashes from where he stands | bashes asked 0", off, StringComparison.Ordinal);
        Assert.EndsWith("none; ended before the key 0", off, StringComparison.Ordinal);
    }

    [Fact]
    public void The_step_ins_reads_and_notes_allocate_nothing()
    {
        var r = Shipped();
        var si = new AiBashStepInStats();
        // warm up
        AiBashStepInMath.Decide(in r, 1.3, false, false, 1, out _);
        AiBashStepInMath.Vector(0, 1, 1, 0, 0, 1, out _, out _);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++)
        {
            double d = 0.8 + (i % 80) * 0.01;
            var s = AiBashStepInMath.Decide(in r, d, i % 7 == 0, i % 14 == 0, 1.0, out double stop);
            AiBashStepInMath.Vector(0.1, d, 1, 0, 0, 1, out _, out _);
            AiBashStepInMath.InCorridor(0, 0, 0, d, 0.2, 0.3, d - 0.95);
            AiBashStepInMath.Arrived(d, stop);
            si.Note(s);
            si.NotePress(d, 0.95);
            si.NoteResult(d, AiBashResult.Enemy);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}

using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 97 - THE STEP-IN THROUGH THE PRESS CLAMP + A SHORTER KICK COOLDOWN (Toni's T5 battle 2026-10-25 13:24: 172 step-ins, 0 arrived, the gap closed
/// 0.05 m; the 2 s cooldown refused 145 kicks / bashes; AI_NOTES "Step 97", DESIGN decision 89): the press clamp's exemption (through toward his own
/// enemy while he is beyond his stop, cut at 0.95 m, never past the enemy line's front), the hold-back reasons, the per-step measurement and the engine
/// verdict, the cooldown shipped 1 s and the format-26 migration, the summary clause, 0 bytes.
/// </summary>
public class Step97Tests
{
    private static AiKickRules Shipped(bool stepIn = true) =>
        new AiKickRules(true, true, true, 1.6, 40, 1, true, 27, 43, 45, 0.45, 0.5, 1.2, 25, 0.5, 0.4, 0, stepIn, 0.95, 0.6);

    /// <summary>The shipped values a migration moves to (the tests' own defaults are DESIGN's initial ones - the cooldown's is 10).</summary>
    private static double ShippedDefault(ParamDef p) => p == SettingsSchema.AiKickCooldownSeconds ? 1 : p.Default;

    [Fact]
    public void The_press_clamp_lets_the_step_through_toward_his_enemy_beyond_his_stop_and_never_past_the_line_front()
    {
        // through: the enemy 1.3 m off, his stop 0.95, the line's front 0.5 m ahead - room min(0.35, 0.35) = 0.35
        Assert.True(AiStepInHoldMath.PressLetsThrough(1.3, 0.95, 0.5));
        Assert.Equal(0.35, AiStepInHoldMath.PressRoomMetres(1.3, 0.95, 0.5), 9);
        // cut at 0.95: the enemy within his stop
        Assert.False(AiStepInHoldMath.PressLetsThrough(0.95, 0.95, 0.5));
        Assert.False(AiStepInHoldMath.PressLetsThrough(0.9, 0.95, 0.5));
        Assert.True(AiStepInHoldMath.PressLetsThrough(0.96, 0.95, 0.5));
        // never past the enemy line's front: the room is the smaller of the two - the front's margin 0.15 m
        Assert.Equal(0.15, AiStepInHoldMath.PressFrontMarginMetres, 9);
        Assert.Equal(0.15, AiStepInHoldMath.PressRoomMetres(1.6, 0.95, 0.3), 9);
        Assert.False(AiStepInHoldMath.PressLetsThrough(1.6, 0.95, 0.15));
        Assert.False(AiStepInHoldMath.PressLetsThrough(1.6, 0.95, 0.0));
        Assert.False(AiStepInHoldMath.PressLetsThrough(1.6, 0.95, -0.4)); // past the front: the clamp's protection untouched
        Assert.True(AiStepInHoldMath.PressLetsThrough(1.6, 0.95, 0.16));
        // a held wall man's leash stop (1.1) counts: never nearer than it
        Assert.False(AiStepInHoldMath.PressLetsThrough(1.05, 1.1, 0.5));
        // unknown = the clamp as always
        Assert.False(AiStepInHoldMath.PressLetsThrough(double.NaN, 0.95, 0.5));
        Assert.False(AiStepInHoldMath.PressLetsThrough(1.3, double.NaN, 0.5));
        Assert.False(AiStepInHoldMath.PressLetsThrough(1.3, 0.95, double.NaN));
        Assert.True(double.IsNaN(AiStepInHoldMath.PressRoomMetres(1.3, 0.95, double.NaN)));
    }

    [Fact]
    public void Each_frame_is_filed_by_what_held_the_step_back_a_stronger_wish_first()
    {
        // the forward part along the step's direction, clamped
        Assert.Equal(1, AiStepInHoldMath.Forward(0, 1, 0, 1), 9);
        Assert.Equal(0, AiStepInHoldMath.Forward(1, 0, 0, 1), 9);
        Assert.Equal(-1, AiStepInHoldMath.Forward(0, -1, 0, 1), 9);
        Assert.Equal(1, AiStepInHoldMath.Forward(0, 2, 0, 1), 9);
        Assert.Equal(0, AiStepInHoldMath.Forward(0, 1, 0, 0), 9); // degenerate step
        // a stronger wish wins whatever the clamps did
        foreach (var w in new[] { StepInHold.Backpedal, StepInHold.WallPull, StepInHold.RestWalk, StepInHold.PressWalk, StepInHold.Swap, StepInHold.Regroup, StepInHold.Flock, StepInHold.Block })
        {
            Assert.True(AiStepInHoldMath.IsStrongerWish(w));
            Assert.Equal(w, AiStepInHoldMath.Classify(w, true, 0, true, 0, false));
        }
        // the press clamp cut it - only when the forward part really fell
        Assert.Equal(StepInHold.PressCut, AiStepInHoldMath.Classify(StepInHold.None, true, 0, false, 0, false));
        Assert.Equal(StepInHold.None, AiStepInHoldMath.Classify(StepInHold.None, true, 1, false, 1, false)); // the side clamp, the forward kept
        // the rest clamp
        Assert.Equal(StepInHold.RestClamp, AiStepInHoldMath.Classify(StepInHold.None, false, 1, true, 0.2, false));
        // let through by step 97 / nothing
        Assert.Equal(StepInHold.PressLetThrough, AiStepInHoldMath.Classify(StepInHold.None, false, 1, false, 1, true));
        Assert.Equal(StepInHold.None, AiStepInHoldMath.Classify(StepInHold.None, false, 1, false, 1, false));
        Assert.True(AiStepInHoldMath.Clear(StepInHold.None) && AiStepInHoldMath.Clear(StepInHold.PressLetThrough));
        Assert.False(AiStepInHoldMath.Clear(StepInHold.PressCut) || AiStepInHoldMath.Clear(StepInHold.RestClamp) || AiStepInHoldMath.Clear(StepInHold.Block));
        Assert.Equal("the press clamp let it through", AiStepInHoldMath.Name(StepInHold.PressLetThrough));
        Assert.Equal("the wall's pull", AiStepInHoldMath.Name(StepInHold.WallPull));
    }

    [Fact]
    public void The_step_ins_are_measured_and_the_clear_ones_say_whether_the_engine_moves_a_guarding_man()
    {
        var h = new AiStepInHoldStats();
        var frames = new int[(int)StepInHold.Count];
        // three clear step-ins (the press let two frames through) that barely moved: 0.03 m in 0.6 s
        for (int k = 0; k < 3; k++)
        {
            h.NoteStart(false);
            Array.Clear(frames, 0, frames.Length);
            frames[(int)StepInHold.None] = 28;
            frames[(int)StepInHold.PressLetThrough] = 2;
            h.NoteStep(frames, 30, 30, 0.6, 0.03, 0.1);
        }
        Assert.Equal(3, h.ClearSteps);
        Assert.Equal(90, h.TotalFrames);
        Assert.StartsWith("IT'S THE ENGINE: 3 step-ins with every wish clear (a full forward stick out of our filter) walked 0.03 m toward the enemy in 0.60 s on average (0.05 m/s, peak 0.10 m/s)",
            AiStepInHoldStats.EngineVerdict(h.ClearSteps, h.ClearSeconds, h.ClearCovered, h.ClearPeakSum), StringComparison.Ordinal);
        // one held back by the block hold and the press clamp - not clear; a leash-shortened stop
        h.NoteStart(true);
        Array.Clear(frames, 0, frames.Length);
        frames[(int)StepInHold.Block] = 10;
        frames[(int)StepInHold.PressCut] = 5;
        h.NoteStep(frames, 15, 2, 0.3, 0.02, 0.05);
        Assert.Equal(4, h.Steps);
        Assert.Equal(3, h.ClearSteps);
        Assert.Equal(1, h.LeashShortened);
        // a step with no time (the frames still count, no measurement)
        Array.Clear(frames, 0, frames.Length);
        frames[(int)StepInHold.RestClamp] = 1;
        h.NoteStep(frames, 1, 0, 0, double.NaN, 0);
        Assert.Equal(4, h.Steps);
        Assert.Equal(1, h.Frames(StepInHold.RestClamp));
        string clause = h.Clause();
        Assert.Equal("what held the step back (step 97 - each step-in frame's forward stick toward his enemy): frames 106 - nothing 84 (79%), the press clamp LET THROUGH "
                     + "(it would have cut - step 97) 6, the press clamp still cut (his enemy within his stop / at the enemy line's front) 5, the rest clamp 1, a stronger wish 10 "
                     + "(the block hold 10); the wall's leash shortened the stop of 1 of 4 step-ins | per step-in from his positions: he walked 0.03 m toward his enemy in 0.53 s "
                     + "(0.05 m/s, peak 0.09 m/s) with the stick asked 1.00, out of our filter 0.78 (n 4); IT'S THE ENGINE: 3 step-ins with every wish clear",
            clause.Substring(0, clause.IndexOf(" (a full forward stick", StringComparison.Ordinal)));
        // the engine moves him: a clear walk at 1 m/s
        var moving = new AiStepInHoldStats();
        Array.Clear(frames, 0, frames.Length);
        frames[(int)StepInHold.None] = 20;
        for (int k = 0; k < 3; k++) moving.NoteStep(frames, 20, 20, 0.4, 0.4, 1.3);
        Assert.StartsWith("the engine moves him: 3 step-ins with every wish clear", AiStepInHoldStats.EngineVerdict(moving.ClearSteps, moving.ClearSeconds, moving.ClearCovered, moving.ClearPeakSum), StringComparison.Ordinal);
        Assert.Contains("(1.00 m/s, peak 1.30 m/s)", moving.Clause(), StringComparison.Ordinal);
        // too few to judge; nothing at all
        Assert.Equal("too few step-ins with every wish clear (2) to judge the engine", AiStepInHoldStats.EngineVerdict(2, 1, 0.1, 0.2));
        Assert.Equal("what held the step back (step 97 - each step-in frame's forward stick toward his enemy): no step-in frame", new AiStepInHoldStats().Clause());
    }

    [Fact]
    public void The_clause_ends_step_95s_line_while_the_step_in_is_on()
    {
        var st = new AiKickStats();
        var frames = new int[(int)StepInHold.Count];
        frames[(int)StepInHold.PressLetThrough] = 3;
        st.StepIn.Hold.NoteStart(false);
        st.StepIn.Hold.NoteStep(frames, 3, 3, 0.05, 0.05, 1.0);
        string line = st.SummaryLines(Shipped()).ToList()[8];
        Assert.StartsWith("ai kicks - the step-in bash (step 95): ", line, StringComparison.Ordinal);
        Assert.Contains("; ended before the key 0 | what held the step back (step 97 - each step-in frame's forward stick toward his enemy): frames 3 - nothing 0 (0%), the press clamp LET THROUGH (it would have cut - step 97) 3",
            line, StringComparison.Ordinal);
        Assert.EndsWith("too few step-ins with every wish clear (1) to judge the engine", line, StringComparison.Ordinal);
        // off and never stepped: step 95's OFF line unchanged
        Assert.EndsWith("none; ended before the key 0", new AiKickStats().StepIn.Line(Shipped(stepIn: false)), StringComparison.Ordinal);
    }

    [Fact]
    public void The_cooldown_ships_1_s_for_every_kind_and_format_26_moves_an_untouched_2()
    {
        string text = DefaultsFile.ReadEmbeddedText();
        Assert.Contains("\"AiKickCooldownSeconds\": 1.0,", text, StringComparison.Ordinal);
        Assert.Contains("a weapon push", SettingsSchema.AiKickCooldownSeconds.Description, StringComparison.Ordinal);
        Assert.Contains("1 s since step 97 (2 s before)", SettingsSchema.AiKickCooldownSeconds.Description, StringComparison.Ordinal);
        Assert.Equal(26, ConfigFile.FormatVersion);
        var s = new TraxSettings();
        s.Set(SettingsSchema.AiKickCooldownSeconds, 1.0, "test");
        Assert.Equal(1.0, AiKickRules.From(s).Cooldown, 9); // one cooldown, read live, for kicks, bashes and pushes
        // a format-25 file's untouched 2 → 1, said once
        var read = ConfigFile.Read("{ \"ConfigVersion\": 25, \"AiKickCooldownSeconds\": 2.0 }");
        var notes = ConfigFile.Migrate(read, ShippedDefault);
        Assert.Equal(1.0, read.Values["AiKickCooldownSeconds"], 9);
        Assert.Equal("AiKickCooldownSeconds: 2.0 → 1.0 (format 25 → 26, the old default; step 97, Toni: a shorter kick cooldown - at least 1 s between two kicks, bashes or pushes of one man)",
            Assert.Single(notes));
        // a format-19 file too
        var nineteen = ConfigFile.Read("{ \"ConfigVersion\": 19, \"AiKickCooldownSeconds\": 2.0 }");
        ConfigFile.Migrate(nineteen, ShippedDefault);
        Assert.Equal(1.0, nineteen.Values["AiKickCooldownSeconds"], 9);
        // a tuned value is kept and said once
        var tuned = ConfigFile.Read("{ \"ConfigVersion\": 25, \"AiKickCooldownSeconds\": 3.0 }");
        var tn = ConfigFile.Migrate(tuned, ShippedDefault);
        Assert.Equal(3.0, tuned.Values["AiKickCooldownSeconds"], 9);
        Assert.StartsWith("AiKickCooldownSeconds: 3.0 kept - your own value (format 25 → 26; the new default 1.0 is not pushed over it; step 97", Assert.Single(tn), StringComparison.Ordinal);
        // an older file's 8 goes through step 81's block straight to today's 1, one note
        var old = ConfigFile.Read("{ \"ConfigVersion\": 18, \"AiKickCooldownSeconds\": 8.0 }");
        var on = ConfigFile.Migrate(old, ShippedDefault);
        Assert.Equal(1.0, old.Values["AiKickCooldownSeconds"], 9);
        Assert.Single(on, n => n.StartsWith("AiKickCooldownSeconds", StringComparison.Ordinal));
        // format 26 never touched; idempotent; the rewritten file is format 26
        var mine = ConfigFile.Read("{ \"ConfigVersion\": 26, \"AiKickCooldownSeconds\": 2.0 }");
        Assert.Empty(ConfigFile.Migrate(mine, ShippedDefault));
        Assert.Equal(2.0, mine.Values["AiKickCooldownSeconds"], 9);
        string written = ConfigFile.Write(read.Values, read.Unknown);
        Assert.Contains("\"ConfigVersion\": 26", written, StringComparison.Ordinal);
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read(written), ShippedDefault));
    }

    [Fact]
    public void The_rule_the_filing_and_the_notes_allocate_nothing()
    {
        var h = new AiStepInHoldStats();
        var frames = new int[(int)StepInHold.Count];
        AiStepInHoldMath.PressLetsThrough(1.3, 0.95, 0.5);
        h.NoteStep(frames, 1, 1, 0.1, 0.1, 1);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++)
        {
            double d = 0.8 + (i % 80) * 0.01;
            bool pass = AiStepInHoldMath.PressLetsThrough(d, 0.95, 0.6 - (i % 9) * 0.1);
            double f = AiStepInHoldMath.Forward(0.1, d - 0.8, 0, 1);
            var why = AiStepInHoldMath.Classify((StepInHold)(i % (int)StepInHold.Count), i % 3 == 0, f, i % 5 == 0, f, pass);
            frames[(int)why]++;
            if (i % 30 == 29)
            {
                h.NoteStart(i % 60 == 59);
                h.NoteStep(frames, 30, 25, 0.6, 0.1, 0.4);
                Array.Clear(frames, 0, frames.Length);
            }
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}

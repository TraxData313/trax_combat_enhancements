using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 96 - MORE DEFENSIVE, LESS LETHAL (Toni 2026-10-25: "more active ... but not so lethal, more defencive - bump up the defencive stuff, reduce
/// the swings"; AI_NOTES "Step 96", DESIGN decision 88): the two guard settings placed / shipped / live, the four calmer defaults shipped, the guess
/// chance cut BEFORE step 30's tired raise at f 1 / 0.5 / 0, the block- / parry-decide clamp to the game's 0.99 / 0.95, 100 = off, never the player,
/// the master switch first, the format-25 migration, the summary line, 0 bytes.
/// </summary>
public class Step96Tests
{
    private static double ShippedDefault(ParamDef p) =>
        p == SettingsSchema.AiAggressionFreshPercent ? 45 : p == SettingsSchema.AiAggressionExhaustedPercent ? 15
        : p == SettingsSchema.ShieldInfantrySwingsLessPercent ? 40 : p == SettingsSchema.FootMeleeSwingsLessPercent ? 25 : p.Default;

    private static AiGuardRules Shipped(int guess = 60, int decide = 120) => new AiGuardRules(true, true, guess, decide);

    [Fact]
    public void The_two_settings_sit_after_the_tired_block_on_the_Advanced_page_ship_60_and_120_and_are_live()
    {
        var all = SettingsSchema.All.ToList();
        int tired = all.IndexOf(SettingsSchema.TiredBlockRandomPercent);
        Assert.Equal(tired + 1, all.IndexOf(SettingsSchema.AiGuardGuessPercent));
        Assert.Equal(tired + 2, all.IndexOf(SettingsSchema.AiBlockDecidePercent));
        foreach (var p in new[] { SettingsSchema.AiGuardGuessPercent, SettingsSchema.AiBlockDecidePercent })
        {
            Assert.Same(SettingsSchema.TiredGroup, p.Group);
            Assert.Equal(SettingsPage.Advanced, p.Group.Page);
            Assert.Equal(ApplyTiming.Live, p.Timing);
            Assert.Equal(ParamType.Int, p.Type);
        }
        Assert.Equal(100, SettingsSchema.AiGuardGuessPercent.Max, 9);
        Assert.Equal(200, SettingsSchema.AiBlockDecidePercent.Max, 9);
        Assert.Equal(60, SettingsSchema.AiGuardGuessPercent.Default, 9);
        Assert.Equal(120, SettingsSchema.AiBlockDecidePercent.Default, 9);
        string text = DefaultsFile.ReadEmbeddedText();
        foreach (var line in new[]
                 {
                     "\"AiGuardGuessPercent\": 60,", "\"AiBlockDecidePercent\": 120,", "\"AiAggressionFreshPercent\": 45,", "\"AiAggressionExhaustedPercent\": 15,",
                     "\"ShieldInfantrySwingsLessPercent\": 40,", "\"FootMeleeSwingsLessPercent\": 25,",
                 })
            Assert.Contains(line, text, StringComparison.Ordinal);

        var s = new TraxSettings();
        var r = AiGuardRules.From(s);
        Assert.True(r.GuessOn && r.DecideOn);
        Assert.Equal(0.6f, r.GuessFactor, 6);
        Assert.Equal(1.2f, r.DecideFactor, 6);
        Assert.StartsWith("AI guard (step 96): on - AI men on foot and riders (never you): the game's block-direction guess chance x0.60 (AiGuardGuessPercent 60 - a recruit's 0.99 → 0.59",
            r.Describe(), StringComparison.Ordinal);
        Assert.Contains("the block- and parry-decide abilities x1.20 (AiBlockDecidePercent 120 - a recruit's 0.62 / 0.53 → 0.74 / 0.64, at most the game's own 0.99 / 0.95)", r.Describe(), StringComparison.Ordinal);
        s.Set(SettingsSchema.AiGuardGuessPercent, 80, "test");
        Assert.Equal(0.8f, AiGuardRules.From(s).GuessFactor, 6); // live
        s.Set(SettingsSchema.AiGuardGuessPercent, 100, "test");
        Assert.False(AiGuardRules.From(s).GuessOn);
        Assert.Equal(1f, AiGuardRules.From(s).GuessFactor);
        Assert.True(AiGuardRules.From(s).Enabled); // the decide lever still on
        s.Set(SettingsSchema.AiBlockDecidePercent, 100, "test");
        Assert.False(AiGuardRules.From(s).Enabled);
        Assert.Equal("AiGuardGuessPercent 100 and AiBlockDecidePercent 100", AiGuardRules.From(s).OffBecause);
        Assert.Equal("AI guard (step 96): off (AiGuardGuessPercent 100 and AiBlockDecidePercent 100) - the game's own block-direction guesses and block decisions", AiGuardRules.From(s).Describe());
        s.Set(SettingsSchema.AiGuardGuessPercent, 60, "test");
        s.Set(SettingsSchema.AiBlockDecidePercent, 120, "test");
        // the master switch first, then Athletics
        s.Set(SettingsSchema.AthleticsEnabled, false, "test");
        Assert.Equal("AthleticsEnabled", AiGuardRules.From(s).OffBecause);
        s.Set(SettingsSchema.ModEnabled, false, "test");
        var off = AiGuardRules.From(s);
        Assert.False(off.Enabled);
        Assert.Equal("ModEnabled", off.OffBecause);
        AiGuardMath.Factors(in off, false, out float g, out float b);
        Assert.Equal(1f, g);
        Assert.Equal(1f, b);
    }

    [Fact]
    public void The_guess_chance_is_cut_before_step_30s_tired_raise_so_a_tired_man_still_guesses_more()
    {
        var tired = new TiredBlockRules(true, true, 100);
        var r = Shipped();
        AiGuardMath.Factors(in r, false, out float g, out _);
        foreach (var own in new[] { 1.0f, 0.74f, 0.2f })
        {
            float cut = AiGuardMath.Guess(own, g);
            Assert.Equal(own * 0.6f, cut, 6);
            // f 1: no tired raise - the cut value stands
            float w1 = TiredBlockMath.Weight(in tired, 1.0, false);
            Assert.Equal(0f, w1);
            Assert.Equal(cut, TiredBlockMath.Chance(cut, w1), 6);
            // f 0.5: own' + (1 − own') × 0.5
            float w5 = TiredBlockMath.Weight(in tired, 0.5, false);
            Assert.Equal(cut + (1 - cut) * 0.5f, TiredBlockMath.Chance(cut, w5), 6);
            Assert.True(TiredBlockMath.Chance(cut, w5) > cut);
            // f 0: every pick a guess, the guard or not
            float w0 = TiredBlockMath.Weight(in tired, 0.0, false);
            Assert.Equal(1f, TiredBlockMath.Chance(cut, w0), 6);
        }
        // the recruit's 1.0: 0.60 fresh, 0.80 at f 0.5, 1 at empty (vanilla 1 / 1 / 1)
        Assert.Equal(0.8f, TiredBlockMath.Chance(AiGuardMath.Guess(1f, 0.6f), 0.5f), 6);
        // a game value at or below 0 (a final-round tournament fighter) is left alone, NaN too; > 1 clamped first
        Assert.Equal(-1.1f, AiGuardMath.Guess(-1.1f, 0.6f));
        Assert.Equal(0f, AiGuardMath.Guess(0f, 0.6f));
        Assert.True(float.IsNaN(AiGuardMath.Guess(float.NaN, 0.6f)));
        Assert.Equal(0.6f, AiGuardMath.Guess(1.4f, 0.6f), 6);
        Assert.Equal(0f, AiGuardMath.Guess(0.7f, 0f));
        // 100 = off: untouched
        Assert.Equal(0.93f, AiGuardMath.Guess(0.93f, 1f));
    }

    [Fact]
    public void The_block_and_parry_decide_abilities_are_raised_and_clamped_to_the_games_own_range()
    {
        // a recruit (num 0.064): block lerp(0.5, 0.99, 0.253) 0.624, parry lerp(0.5, 0.95, 0.064) 0.529
        Assert.Equal(0.624f * 1.2f, AiGuardMath.Block(0.624f, 1.2f), 5);
        Assert.Equal(0.529f * 1.2f, AiGuardMath.Parry(0.529f, 1.2f), 5);
        // a veteran (num 0.64): block 0.892 → 1.07 clamped to 0.99, parry 0.788 → 0.946 (under 0.95)
        Assert.Equal(0.99f, AiGuardMath.Block(0.892f, 1.2f), 6);
        Assert.Equal(0.788f * 1.2f, AiGuardMath.Parry(0.788f, 1.2f), 5);
        Assert.Equal(0.95f, AiGuardMath.Parry(0.9f, 1.2f), 6);
        Assert.Equal(0.99f, AiGuardMath.Block(0.99f, 2f), 6);
        // lowered below 100, never below 0
        Assert.Equal(0.4f, AiGuardMath.Block(0.8f, 0.5f), 6);
        Assert.Equal(0f, AiGuardMath.Block(0.8f, 0f));
        Assert.Equal(0f, AiGuardMath.Parry(-0.2f, 1.2f));
        // a higher value another mod wrote is not cut by a raise; 1 = untouched; NaN untouched
        Assert.Equal(1.0f, AiGuardMath.Block(1.0f, 1.2f), 6);
        Assert.Equal(0.97f, AiGuardMath.Parry(0.97f, 1.2f), 6);
        Assert.Equal(0.5f, AiGuardMath.Block(0.5f, 1f));
        Assert.True(float.IsNaN(AiGuardMath.Parry(float.NaN, 1.2f)));
    }

    [Fact]
    public void Never_the_player_and_100_is_off()
    {
        var r = Shipped();
        AiGuardMath.Factors(in r, true, out float g, out float b);
        Assert.Equal(1f, g);
        Assert.Equal(1f, b);
        AiGuardMath.Factors(in r, false, out g, out b);
        Assert.Equal(0.6f, g, 6);
        Assert.Equal(1.2f, b, 6);
        var none = Shipped(100, 100);
        Assert.False(none.Enabled);
        AiGuardMath.Factors(in none, false, out g, out b);
        Assert.Equal(1f, g);
        Assert.Equal(1f, b);
        var guessOnly = Shipped(60, 100);
        AiGuardMath.Factors(in guessOnly, false, out g, out b);
        Assert.Equal(0.6f, g, 6);
        Assert.Equal(1f, b);
        Assert.Contains("the decide abilities the game's (AiBlockDecidePercent 100)", guessOnly.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void Format_25_moves_the_four_untouched_values_once_keeps_a_tuned_one_and_leaves_format_25_alone()
    {
        Assert.Equal(25, ConfigFile.FormatVersion);
        var read = ConfigFile.Read("{ \"ConfigVersion\": 24, \"AiAggressionFreshPercent\": 60, \"AiAggressionExhaustedPercent\": 20, \"ShieldInfantrySwingsLessPercent\": 30, \"FootMeleeSwingsLessPercent\": 15 }");
        var notes = ConfigFile.Migrate(read, ShippedDefault);
        Assert.Equal(4, notes.Count);
        Assert.Equal(45, read.Values["AiAggressionFreshPercent"], 9);
        Assert.Equal(15, read.Values["AiAggressionExhaustedPercent"], 9);
        Assert.Equal(40, read.Values["ShieldInfantrySwingsLessPercent"], 9);
        Assert.Equal(25, read.Values["FootMeleeSwingsLessPercent"], 9);
        Assert.Equal("AiAggressionFreshPercent: 60 → 45 (format 24 → 25, the old default; step 96, Toni: more defensive, less lethal - a fresh AI man decides to attack at 45% of the game's rate)", notes[0]);
        Assert.StartsWith("AiAggressionExhaustedPercent: 20 → 15 (format 24 → 25, the old default; step 96", notes[1], StringComparison.Ordinal);
        Assert.StartsWith("ShieldInfantrySwingsLessPercent: 30 → 40 (format 24 → 25, the old default; step 96", notes[2], StringComparison.Ordinal);
        Assert.StartsWith("FootMeleeSwingsLessPercent: 15 → 25 (format 24 → 25, the old default; step 96", notes[3], StringComparison.Ordinal);
        // a tuned value is kept and said once
        var tuned = ConfigFile.Read("{ \"ConfigVersion\": 24, \"AiAggressionFreshPercent\": 70, \"FootMeleeSwingsLessPercent\": 15 }");
        var tn = ConfigFile.Migrate(tuned, ShippedDefault);
        Assert.Equal(70, tuned.Values["AiAggressionFreshPercent"], 9);
        Assert.Equal(25, tuned.Values["FootMeleeSwingsLessPercent"], 9);
        Assert.Equal(2, tn.Count);
        Assert.StartsWith("AiAggressionFreshPercent: 70 kept - your own value (format 24 → 25; the new default 45 is not pushed over it; step 96", tn[0], StringComparison.Ordinal);
        // an old file (format 8) gets them too, in one note each
        var old = ConfigFile.Read("{ \"ConfigVersion\": 8, \"AiAggressionFreshPercent\": 60, \"ShieldInfantrySwingsLessPercent\": 30 }");
        var on = ConfigFile.Migrate(old, ShippedDefault);
        Assert.Equal(45, old.Values["AiAggressionFreshPercent"], 9);
        Assert.Equal(40, old.Values["ShieldInfantrySwingsLessPercent"], 9);
        Assert.Single(on, n => n.StartsWith("AiAggressionFreshPercent", StringComparison.Ordinal));
        // a format-25 file is never touched; idempotent; DESIGN's values (60 / 20 / 30 / 15 = the old ones) move nothing
        var mine = ConfigFile.Read("{ \"ConfigVersion\": 25, \"AiAggressionFreshPercent\": 60, \"ShieldInfantrySwingsLessPercent\": 30 }");
        Assert.Empty(ConfigFile.Migrate(mine, ShippedDefault));
        Assert.Equal(60, mine.Values["AiAggressionFreshPercent"], 9);
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 24, \"AiAggressionFreshPercent\": 45 }"), ShippedDefault));
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 24, \"AiAggressionFreshPercent\": 60 }")));
    }

    [Fact]
    public void The_summary_names_the_guard_factors_and_the_values_written_by_f_band()
    {
        var st = new AiGuardStats();
        st.Men = 2;
        st.Add(0, 1.0f, 0.6f, 0.62f, 0.744f, 0.53f, 0.636f);
        st.Add(0, 0.8f, 0.48f, 0.7f, 0.84f, 0.61f, 0.72f);
        st.Add(3, 1.0f, 0.6f, 0.62f, 0.744f, 0.53f, 0.636f);
        Assert.Equal(3, st.Recomputes);
        Assert.Equal(2, st.Count(0));
        Assert.Equal(0.54, st.MeanGuessOurs(0), 6);
        Assert.Equal("AI guard factors (step 96 - AiGuardGuessPercent 60, AiBlockDecidePercent 120; AI men on foot and riders, never you): written for 2 AI men (3 recomputes); "
                     + "the game's → ours, avg by f band - the block-direction guess chance (before step 30's tired raise) / the block-decide ability / the parry-decide ability: "
                     + AthleticsMath.PeakBinName(0) + " 0.90 → 0.54 / 0.66 → 0.79 / 0.57 → 0.68 (n 2); "
                     + AthleticsMath.PeakBinName(3) + " 1.00 → 0.60 / 0.62 → 0.74 / 0.53 → 0.64 (n 1)",
            st.SummaryLine(Shipped()));
        Assert.Equal("AI guard factors (step 96): off at the end (AiGuardGuessPercent 100 and AiBlockDecidePercent 100) and never written this battle - the game's own guard",
            new AiGuardStats().SummaryLine(Shipped(100, 100)));
        Assert.EndsWith(" - off at the end (ModEnabled)", st.SummaryLine(new AiGuardRules(false, true, 60, 120)), StringComparison.Ordinal);
    }

    [Fact]
    public void The_guard_math_and_its_notes_allocate_nothing()
    {
        var r = Shipped();
        var st = new AiGuardStats();
        AiGuardMath.Factors(in r, false, out float g, out float b);
        st.Add(0, 1, AiGuardMath.Guess(1, g), 0.6f, AiGuardMath.Block(0.6f, b), 0.5f, AiGuardMath.Parry(0.5f, b));
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++)
        {
            float game = 0.5f + (i % 50) * 0.01f;
            AiGuardMath.Factors(in r, i % 9 == 0, out g, out b);
            float guess = AiGuardMath.Guess(game, g);
            float block = AiGuardMath.Block(game, b);
            float parry = AiGuardMath.Parry(game, b);
            st.Add(i % AthleticsMath.PeakBins, game, guess, game, block, game, parry);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}

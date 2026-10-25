using System;
using System.Linq;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// Step 91b - THE BASH REACH SHIPS OFF AGAIN (Toni's 20:26 battle of 2026-10-24: with step 91's AiBashReachMetres 1.1 the enemy stood within
/// it in only 3 of 792 shieldman moves - the AI fights at 1.1-1.6 m - so no shieldman bashed; AI_NOTES "Step 91b"): the default 0, the config
/// format 22 moving a format-21 file's untouched 1.1 to 0 and keeping a tuned value, and the step-91 summary line reading sensibly while off.
/// </summary>
public class Step91bTests
{
    /// <summary>defaults.json's value (the tests run on DESIGN's initial 1.1 - DesignTable).</summary>
    private static double Shipped(ParamDef p) => p == SettingsSchema.AiBashReachMetres ? 0 : p.Default;

    [Fact]
    public void Format_22_moves_an_untouched_1_1_to_0_keeps_a_tuned_reach_and_leaves_older_files_alone()
    {
        Assert.Equal(23, ConfigFile.FormatVersion);
        Assert.Contains("\"AiBashReachMetres\": 0.0,", DefaultsFile.ReadEmbeddedText(), StringComparison.Ordinal);
        // a format-21 file written by the game (step 91's shipped 1.1) moves to 0
        var read = ConfigFile.Read("{ \"ConfigVersion\": 21, \"AiBashReachMetres\": 1.1, \"AiKickReachMetres\": 1.6 }");
        var notes = ConfigFile.Migrate(read, Shipped);
        Assert.Equal(0, read.Values["AiBashReachMetres"], 9);
        Assert.Equal(1.6, read.Values["AiKickReachMetres"], 9);
        var note = Assert.Single(notes);
        Assert.StartsWith("AiBashReachMetres: 1.1 → 0.0 (format 21 → 23, the old default; step 91b, Toni: with 1.1 m the enemy stood that close in only 3 of 792 shieldman moves", note, StringComparison.Ordinal);
        // a tuned reach is kept and said once
        var tuned = ConfigFile.Read("{ \"ConfigVersion\": 21, \"AiBashReachMetres\": 1.4 }");
        var tn = ConfigFile.Migrate(tuned, Shipped);
        Assert.Equal(1.4, tuned.Values["AiBashReachMetres"], 9);
        Assert.StartsWith("AiBashReachMetres: 1.4 kept - your own value (format 21 → 23; the new default 0.0 is not pushed over it; step 91b", Assert.Single(tn), StringComparison.Ordinal);
        // a hand-set 0 is already today's: nothing to say
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 21, \"AiBashReachMetres\": 0 }"), Shipped));
        // an older file never had the key (step 91 added it in format 21); a format-22 file is not touched - a 1.1 there is the player's own
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read("{ \"ConfigVersion\": 20, \"AiShieldmanBashPercent\": 40 }"), Shipped));
        var mine = ConfigFile.Read("{ \"ConfigVersion\": 22, \"AiBashReachMetres\": 1.1 }");
        Assert.Empty(ConfigFile.Migrate(mine, Shipped));
        Assert.Equal(1.1, mine.Values["AiBashReachMetres"], 9);
        // the rewritten file is format 22 and idempotent
        string written = ConfigFile.Write(read.Values, read.Unknown);
        Assert.Contains("\"ConfigVersion\": 23", written, StringComparison.Ordinal);
        Assert.Empty(ConfigFile.Migrate(ConfigFile.Read(written), Shipped));
    }

    [Fact]
    public void While_off_the_summary_line_says_the_bash_is_at_the_kick_reach_and_still_bins_every_bash()
    {
        var r = new AiKickRules(true, true, true, 1.6, 40, 2, true, 25, 40, 35, 0.45, 0.5, 1.2, 20, 0.5, 0.4, 0);
        Assert.False(r.BashReachOn);
        var st = new AiKickStats();
        var b = st.BashReach;
        // off: no man is ever beyond the reach - every roll counts in the kick reach, every bash binned by its distance
        foreach (double d in new[] { 0.9, 1.3, 1.5 })
        {
            Assert.False(r.BeyondBashReach(d));
            b.NoteShieldRoll(r.BeyondBashReach(d), AiKickKind.Bash, d);
            b.NoteAsked(d, r.BeyondBashReach(d));
        }
        b.NoteResult(0.9, AiBashResult.Enemy);
        b.NoteResult(1.3, AiBashResult.Friend);
        b.NoteResult(1.5, AiBashResult.Missed);
        string line = st.SummaryLines(r).ToList()[7];
        Assert.StartsWith("ai kicks - the shield bash's reach (step 91): the bash reach is OFF - a shieldman bashes at the kick reach, anywhere within 1.6 m (AiBashReachMetres 0; the bins below still say where his bashes land)", line, StringComparison.Ordinal);
        Assert.Contains(" | shieldmen's moves in the kick reach 3 - rolled kick 0.0% / bash 100.0% / swing 0.0% (target 25% / 40% / 35%)", line, StringComparison.Ordinal);
        Assert.Contains("0.8-1.0 m 1, 1 → 1 / 0 / 0 / 0 / 0 / 0 (friends 0% of the hits); 1.2-1.4 m 1, 1 → 0 / 0 / 1 / 0 / 0 / 0 (friends 100% of the hits); 1.4-1.6 m 1, 1 → 0 / 0 / 0 / 1 / 0 / 0", line, StringComparison.Ordinal);
        Assert.DoesNotContain("only within", line, StringComparison.Ordinal);
        Assert.DoesNotContain("0.00 m", line, StringComparison.Ordinal);
        Assert.DoesNotContain("beyond it", line, StringComparison.Ordinal);
        Assert.DoesNotContain("asked beyond the bash reach", line, StringComparison.Ordinal);
        Assert.EndsWith("(friends 0% of the hits)", line.Substring(0, line.IndexOf("; 1.2-1.4", StringComparison.Ordinal)), StringComparison.Ordinal);
    }
}

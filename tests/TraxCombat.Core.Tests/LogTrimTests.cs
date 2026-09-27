using System.Text;
using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

/// <summary>
/// The log's trim rule (step 10b, review R7 - the manager's decision): a trim never loses the
/// lines the playtest is read from. Verbose lines (the <c>~[tag]</c> mark) are cut oldest first;
/// every other line - and any [load] [compat] [config] [mcm] [mission] [summary] [error] line -
/// stays; only when those alone pass the target do the oldest of them go too.
/// </summary>
public class LogTrimTests
{
    private const string Nl = "\r\n";
    private const string Now = "2026.09.30 21:00:00.000";

    private static string Stamp(int second) => "2026.09.30 20:" + (second / 60).ToString("00") + ":" + (second % 60).ToString("00") + ".000";

    private static string Line(int second, string tag, string message, bool verbose = false) =>
        Stamp(second) + (verbose ? " ~[" : " [") + tag + "] " + message + Nl;

    private static string Trim(string text, long target, out LogTrimResult r) => LogTrim.Apply(text, target, Now, Nl, out r);

    private static long Bytes(string s) => Encoding.UTF8.GetByteCount(s);

    /// <summary>A battle: its start lines, a flood of verbose lines, its summary; then another.</summary>
    private static string TwoBattles(int verbosePerBattle = 400)
    {
        var sb = new StringBuilder();
        sb.Append(Line(0, "load", "==================== Trax Combat Enhancements 0.1.0 ===================="));
        sb.Append(Line(1, "config", "settings in effect (57, version 0):"));
        sb.Append(Line(1, "mcm", "settings page registered at main menu (attempt 1)"));
        int t = 10;
        for (int battle = 1; battle <= 2; battle++)
        {
            sb.Append(Line(t, "mission", "start: scene battle_" + battle + ", field battle, mod ON"));
            sb.Append(Line(t, "damage", "mission start: damage randomness ON, spread ±50% - battle " + battle));
            sb.Append(Line(t, "hud", "orders strip: first placement under the cards at 35.2 s - battle " + battle));
            for (int i = 0; i < verbosePerBattle; i++)
                sb.Append(Line(t + 1 + i / 20, "athletics", "blow melee (on foot): Imperial Recruit " + i + " - cost 10.0, 50.0 → 40.0 of 50 (f 1.00 → 1.00) - battle " + battle, verbose: true));
            t += 1 + verbosePerBattle / 20 + 1;
            sb.Append(Line(t, "summary", "==== scene battle_" + battle + ", field battle - ended after 90 s, mod ON ===="));
            sb.Append(Line(t, "summary", "damage rolls: 812 hits; factor min 0.50 / avg 1.002 / max 1.50 - battle " + battle));
            sb.Append(Line(t, "log", "120 more verbose [athletics-blow] lines were suppressed by the rate limit"));
            t += 5;
        }
        return sb.ToString();
    }

    [Fact]
    public void Every_non_verbose_line_survives_and_the_newest_verbose_lines_fill_the_rest()
    {
        string log = TwoBattles();
        string trimmed = Trim(log, Bytes(log) / 2, out var r);

        Assert.True(r.BytesAfter <= Bytes(log) / 2, "over the target: " + r.BytesAfter);
        Assert.Equal(Bytes(trimmed), r.BytesAfter);
        Assert.True(r.ChattyCut > 0);
        Assert.Equal(0, r.KeptCut);
        foreach (var line in log.Split(Nl))
            if (line.Length > 0 && !LogTrim.IsVerboseLine(line))
                Assert.Contains(line + Nl, trimmed); // every kept line, whole
        // the newest verbose line stays, the oldest goes
        Assert.Contains("Imperial Recruit 399 - cost 10.0, 50.0 → 40.0 of 50 (f 1.00 → 1.00) - battle 2", trimmed);
        Assert.DoesNotContain("Imperial Recruit 0 - cost 10.0, 50.0 → 40.0 of 50 (f 1.00 → 1.00) - battle 1", trimmed);
    }

    [Fact]
    public void The_order_of_the_lines_is_kept_and_the_note_sits_on_top()
    {
        string log = TwoBattles();
        string trimmed = Trim(log, Bytes(log) / 2, out var r);
        var lines = trimmed.Split(Nl, StringSplitOptions.RemoveEmptyEntries);

        Assert.StartsWith(Now + " [log] " + LogTrim.NoteStart + Now + ": " + r.ChattyCut + " older verbose lines cut, up to ", lines[0]);
        Assert.EndsWith("; every other line (load, settings, battle start and end, first-time events, summaries, errors) is kept)", lines[0]);
        // after the note: the original order (stamps never go backwards)
        for (int i = 2; i < lines.Length; i++)
            Assert.True(string.CompareOrdinal(lines[i - 1], 0, lines[i], 0, 23) <= 0, "out of order at " + i);
        Assert.True(trimmed.IndexOf("battle_1, field battle, mod ON", StringComparison.Ordinal) < trimmed.IndexOf("battle_2, field battle, mod ON", StringComparison.Ordinal));
    }

    [Fact]
    public void Verbose_lines_are_cut_at_one_point_in_time()
    {
        // A big newer verbose line that does not fit must not let a smaller OLDER one stay.
        var sb = new StringBuilder();
        sb.Append(Line(0, "athletics", "small old line", verbose: true));
        sb.Append(Line(1, "athletics", "big " + new string('x', 2000), verbose: true));
        sb.Append(Line(2, "athletics", "small new line", verbose: true));
        sb.Append(Line(3, "summary", "the summary"));
        string trimmed = Trim(sb.ToString(), 1000, out var r);

        Assert.Contains("small new line", trimmed);
        Assert.Contains("the summary", trimmed);
        Assert.DoesNotContain("big xxx", trimmed);
        Assert.DoesNotContain("small old line", trimmed);
        Assert.Equal(2, r.ChattyCut);
        Assert.Contains("up to " + Stamp(1), trimmed); // the newest line cut
    }

    [Fact]
    public void An_error_keeps_its_stack_and_a_torn_message_keeps_its_second_line()
    {
        var sb = new StringBuilder();
        sb.Append(Line(0, "athletics", "flood " + new string('x', 3000), verbose: true));
        sb.Append(Stamp(1) + " [error] damage.roll: System.InvalidOperationException: broken" + Nl
            + "    System.InvalidOperationException: broken" + Nl
            + "       at TraxCombat.Models.DamageRandomizer.Roll()" + Nl);
        sb.Append(Stamp(2) + " [config] file problem: a message" + Nl + "with a line break in it" + Nl);
        sb.Append(Line(3, "athletics", "flood again " + new string('y', 3000), verbose: true));
        string trimmed = Trim(sb.ToString(), 1500, out var r);

        Assert.Contains("[error] damage.roll: System.InvalidOperationException: broken" + Nl + "    System.InvalidOperationException: broken" + Nl + "       at TraxCombat.Models.DamageRandomizer.Roll()" + Nl, trimmed);
        Assert.Contains("[config] file problem: a message" + Nl + "with a line break in it" + Nl, trimmed);
        Assert.Equal(2, r.ChattyCut);
        Assert.Equal(4, r.EntriesBefore); // a stamped line and the unstamped lines under it are one entry
    }

    [Fact]
    public void A_verbose_line_with_a_kept_tag_is_kept()
    {
        var sb = new StringBuilder();
        foreach (var tag in LogTrim.KeptTags)
            sb.Append(Line(0, tag, "verbose but " + tag, verbose: true));
        sb.Append(Line(1, "damage", "a verbose roll " + new string('x', 2000), verbose: true));
        string trimmed = Trim(sb.ToString(), 1500, out var r);

        foreach (var tag in LogTrim.KeptTags)
            Assert.Contains("~[" + tag + "] verbose but " + tag, trimmed);
        Assert.DoesNotContain("a verbose roll", trimmed);
        Assert.Equal(1, r.ChattyCut);
    }

    [Fact]
    public void Kept_lines_alone_over_the_target_lose_their_oldest_the_file_stays_bounded()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 200; i++) sb.Append(Line(i, "summary", "battle " + i + " " + new string('s', 80)));
        sb.Append(Line(300, "damage", "one verbose line", verbose: true));
        string log = sb.ToString();
        string trimmed = Trim(log, 5000, out var r);

        Assert.True(r.BytesAfter <= 5000);
        Assert.True(r.KeptCut > 0);
        Assert.Equal(1, r.ChattyCut);
        Assert.Contains("battle 199 ", trimmed);  // the newest stay
        Assert.DoesNotContain("battle 0 ", trimmed);
        Assert.Contains(", and the other lines alone did not fit, so their oldest " + r.KeptCut + " went too, up to ", trimmed);
    }

    [Fact]
    public void Earlier_notes_are_replaced_by_one_note()
    {
        var sb = new StringBuilder();
        sb.Append(Stamp(0) + " [log] (older lines trimmed - the log keeps about the newest 1000 KB)" + Nl); // the 10a note
        sb.Append(Stamp(1) + " [log] " + LogTrim.NoteStart + Stamp(1) + ": 5 older verbose lines cut)" + Nl);
        sb.Append(Line(2, "log", "120 more verbose [damage] lines were suppressed by the rate limit"));
        sb.Append(Line(3, "damage", "roll " + new string('x', 3000), verbose: true));
        string trimmed = Trim(sb.ToString(), 1000, out var r);

        Assert.Equal(1, CountOf(trimmed, "[log] " + LogTrim.NoteStart));
        Assert.DoesNotContain("older lines trimmed", trimmed);
        Assert.Contains("120 more verbose [damage] lines were suppressed", trimmed); // other [log] lines are kept
        Assert.Equal(2, r.EntriesBefore); // the notes do not count
    }

    [Fact]
    public void A_line_without_a_stamp_at_the_top_is_chatty_and_one_giant_line_goes()
    {
        string log = new string('z', 5000) + Nl + Line(1, "mission", "start") + Line(2, "damage", "roll", verbose: true);
        string trimmed = Trim(log, 1000, out var r);

        Assert.DoesNotContain("zzzz", trimmed);
        Assert.Contains("[mission] start", trimmed);
        Assert.Contains("~[damage] roll", trimmed);
        Assert.True(r.BytesAfter <= 1000);
    }

    [Fact]
    public void Nothing_to_cut_under_the_target_keeps_everything()
    {
        string log = Line(0, "load", "a") + Line(1, "damage", "b", verbose: true);
        string trimmed = Trim(log, 100_000, out var r);

        Assert.EndsWith(log, trimmed);
        Assert.Equal(0, r.ChattyCut + r.KeptCut);
        Assert.Contains(": nothing to cut)", trimmed);
    }

    [Fact]
    public void Line_kind_and_tag_are_read_from_the_stamp_and_the_mark()
    {
        Assert.True(LogTrim.IsVerboseLine(Line(0, "damage", "x", verbose: true)));
        Assert.False(LogTrim.IsVerboseLine(Line(0, "damage", "x")));
        Assert.False(LogTrim.IsVerboseLine("~[damage] no stamp"));
        Assert.Equal("damage", LogTrim.TagOf(Line(0, "damage", "x", verbose: true)));
        Assert.Equal("summary", LogTrim.TagOf(Line(0, "summary", "x")));
        Assert.Null(LogTrim.TagOf("no stamp [damage] x"));
    }

    [Fact]
    public void Sizes_are_utf8_bytes()
    {
        Assert.Equal(Bytes("a→±—é"), LogTrim.Utf8Bytes("a→±—é", 0, 5));
        Assert.Equal(4, LogTrim.Utf8Bytes("\U0001F600", 0, 2)); // a surrogate pair
    }

    private static int CountOf(string text, string what)
    {
        int n = 0, at = 0;
        while ((at = text.IndexOf(what, at, StringComparison.Ordinal)) >= 0) { n++; at += what.Length; }
        return n;
    }
}

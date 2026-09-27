using System;
using System.Collections.Generic;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>What one <see cref="LogTrim.Apply"/> did - for its note line and the tests.</summary>
    public readonly struct LogTrimResult
    {
        public LogTrimResult(int entriesBefore, int chattyCut, int keptCut, long bytesBefore, long bytesAfter)
        {
            EntriesBefore = entriesBefore;
            ChattyCut = chattyCut;
            KeptCut = keptCut;
            BytesBefore = bytesBefore;
            BytesAfter = bytesAfter;
        }

        /// <summary>Log entries in the text before the trim (a line and its indented continuation
        /// lines, such as an error's stack, count as one).</summary>
        public int EntriesBefore { get; }

        /// <summary>Verbose entries cut (the oldest ones).</summary>
        public int ChattyCut { get; }

        /// <summary>Kept-kind entries cut - only when they ALONE passed the target (the last resort,
        /// many sessions' worth of battles: the file carries over from one game start to the next).</summary>
        public int KeptCut { get; }

        /// <summary>UTF-8 size before and after (the note line included).</summary>
        public long BytesBefore { get; }

        public long BytesAfter { get; }
    }

    /// <summary>
    /// The trim rule of trax_combat.log (step 10b; review R7, the manager's decision): trimming
    /// must NEVER lose the lines the playtest is read from. Every line the mod writes is one of two
    /// kinds:
    /// - KEPT: every line written whether or not VerboseLogging is on - load, compat, config, mcm,
    ///   mission, summary and error lines, and every feature's "mission start" / "first time this
    ///   battle" / "YOU …" line (those prove the features in the playtest) - plus any line tagged
    ///   [load] [compat] [config] [mcm] [mission] [summary] [error], even one written verbose;
    /// - CHATTY: the verbose lines (VerboseLogging on - every roll, blow, step back…), which carry
    ///   <see cref="VerboseMark"/> before their tag: <c>2026.09.30 20:15:02.201 ~[damage] melee on …</c>.
    /// A trim keeps every KEPT line and fills the rest of the target with the NEWEST chatty lines;
    /// the chatty lines are cut oldest first at one point in time (never a newer one cut while an
    /// older one stays). Only when the kept lines ALONE pass the target are the oldest of those cut
    /// too, so the file stays bounded. The note of the last trim sits at the top of the file
    /// (earlier notes are dropped). Pure - the module's TraxLog reads and writes the file.
    /// </summary>
    public static class LogTrim
    {
        /// <summary>The mark before the tag of a verbose (chatty) line.</summary>
        public const char VerboseMark = '~';

        /// <summary>Tags whose lines are kept through a trim even when written verbose.</summary>
        public static readonly IReadOnlyList<string> KeptTags = new[] { "load", "compat", "config", "mcm", "mission", "summary", "error" };

        /// <summary>The start of the trim note's message (after "[log] ").</summary>
        public const string NoteStart = "(log trimmed at ";

        /// <summary>The note of the step 3-10a trimmer ("(older lines trimmed - …)") - dropped too.</summary>
        private const string OldNoteStart = "(older lines trimmed";

        /// <summary>Room kept for the note line within the target.</summary>
        private const int NoteReserveBytes = 400;

        /// <summary>"yyyy.MM.dd HH:mm:ss.fff" - the stamp every line starts with.</summary>
        private const int StampLength = 23;

        private struct Entry
        {
            public int Start;
            public int End; // exclusive, after its last line break
            public bool Kept;
            public bool IsNote;
            public long Bytes;
        }

        /// <summary>
        /// Trims <paramref name="text"/> (the whole log) to at most <paramref name="targetBytes"/>
        /// UTF-8 bytes by the rule above. <paramref name="stamp"/> is the note line's time stamp
        /// ("yyyy.MM.dd HH:mm:ss.fff"), <paramref name="newLine"/> its line break. Returns the new text,
        /// the note at its top.
        /// </summary>
        public static string Apply(string text, long targetBytes, string stamp, string newLine, out LogTrimResult result)
        {
            text ??= string.Empty;
            var entries = Split(text);
            long before = Utf8Bytes(text, 0, text.Length);

            long keptBytes = 0;
            int realEntries = 0;
            foreach (var e in entries)
            {
                if (e.IsNote) continue;
                realEntries++;
                if (e.Kept) keptBytes += e.Bytes;
            }

            long budget = Math.Max(0, targetBytes - NoteReserveBytes);
            var keep = new bool[entries.Count];
            int chattyCut = 0, keptCut = 0;
            string? cutUpTo = null;

            if (keptBytes <= budget)
            {
                // Every kept line stays; the newest chatty lines fill what is left, cut at ONE point.
                long room = budget - keptBytes;
                bool cutting = false;
                for (int i = entries.Count - 1; i >= 0; i--)
                {
                    var e = entries[i];
                    if (e.IsNote) continue;
                    if (e.Kept) { keep[i] = true; continue; }
                    if (!cutting && e.Bytes <= room)
                    {
                        keep[i] = true;
                        room -= e.Bytes;
                        continue;
                    }
                    if (!cutting) cutUpTo = StampOf(text, e);
                    cutting = true;
                    chattyCut++;
                }
            }
            else
            {
                // Last resort: the kept lines alone pass the target - every chatty line goes, and the
                // oldest kept lines go too (cut at one point).
                long room = budget;
                bool cutting = false;
                for (int i = entries.Count - 1; i >= 0; i--)
                {
                    var e = entries[i];
                    if (e.IsNote) continue;
                    if (!e.Kept) { chattyCut++; continue; }
                    if (!cutting && e.Bytes <= room)
                    {
                        keep[i] = true;
                        room -= e.Bytes;
                        continue;
                    }
                    if (!cutting) cutUpTo = StampOf(text, e);
                    cutting = true;
                    keptCut++;
                }
            }

            string note = stamp + " [log] " + NoteStart + stamp + ": "
                + (chattyCut == 0 && keptCut == 0
                    ? "nothing to cut"
                    : chattyCut + " older verbose line" + (chattyCut == 1 ? "" : "s") + " cut"
                      + (keptCut > 0
                          ? ", and the other lines alone did not fit, so their oldest " + keptCut + " went too, up to " + (cutUpTo ?? "?")
                          : (cutUpTo != null ? ", up to " + cutUpTo : "") + "; every other line (load, settings, battle start and end, first-time events, summaries, errors) is kept"))
                + ")" + newLine;

            var sb = new StringBuilder((int)Math.Min(int.MaxValue, Math.Max(256, targetBytes)));
            sb.Append(note);
            for (int i = 0; i < entries.Count; i++)
                if (keep[i]) sb.Append(text, entries[i].Start, entries[i].End - entries[i].Start);
            string kept = sb.ToString();
            result = new LogTrimResult(realEntries, chattyCut, keptCut, before, Utf8Bytes(kept, 0, kept.Length));
            return kept;
        }

        /// <summary>True for a line written verbose: the stamp, a space, <see cref="VerboseMark"/>, "[".</summary>
        public static bool IsVerboseLine(string line) =>
            line != null && line.Length > StampLength + 2 && LooksStamped(line, 0) && line[StampLength + 1] == VerboseMark && line[StampLength + 2] == '[';

        /// <summary>The tag of a line ("damage" for "… ~[damage] …"), or null when it has none.</summary>
        public static string? TagOf(string line) => line == null ? null : TagAt(line, 0, line.Length, out _);

        // ------------------------------------------------------------------ parsing

        private static List<Entry> Split(string text)
        {
            var list = new List<Entry>();
            int pos = 0;
            while (pos < text.Length)
            {
                int lineEnd = NextLineEnd(text, pos);
                // A line that does not start with a stamp belongs to the entry above it: an error's
                // indented stack, a message with a line break in it, an empty line.
                bool continuation = list.Count > 0 && !LooksStamped(text, pos);
                if (continuation)
                {
                    var last = list[list.Count - 1];
                    last.End = lineEnd;
                    last.Bytes += Utf8Bytes(text, pos, lineEnd);
                    list[list.Count - 1] = last;
                }
                else
                {
                    string? tag = TagAt(text, pos, lineEnd, out bool verbose);
                    bool isNote = tag == "log" && MessageStartsWith(text, pos, lineEnd, verbose);
                    bool keptTag = tag != null && Contains(KeptTags, tag);
                    list.Add(new Entry
                    {
                        Start = pos,
                        End = lineEnd,
                        // A line without a stamp and tag (a torn or foreign line) counts as chatty.
                        Kept = tag != null && (!verbose || keptTag),
                        IsNote = isNote,
                        Bytes = Utf8Bytes(text, pos, lineEnd),
                    });
                }
                pos = lineEnd;
            }
            return list;
        }

        private static bool MessageStartsWith(string text, int pos, int end, bool verbose)
        {
            int msg = pos + StampLength + 1 + (verbose ? 1 : 0) + "[log] ".Length;
            return StartsAt(text, msg, end, NoteStart) || StartsAt(text, msg, end, OldNoteStart);
        }

        private static bool StartsAt(string text, int at, int end, string what) =>
            at + what.Length <= end && string.CompareOrdinal(text, at, what, 0, what.Length) == 0;

        private static int NextLineEnd(string text, int pos)
        {
            int nl = text.IndexOf('\n', pos);
            return nl < 0 ? text.Length : nl + 1;
        }

        /// <summary>The tag right after the stamp (and the verbose mark), or null.</summary>
        private static string? TagAt(string text, int pos, int end, out bool verbose)
        {
            verbose = false;
            if (end - pos < StampLength + 3 || !LooksStamped(text, pos) || text[pos + StampLength] != ' ') return null;
            int open = pos + StampLength + 1;
            if (text[open] == VerboseMark)
            {
                verbose = true;
                open++;
            }
            if (open >= end || text[open] != '[') return null;
            int close = text.IndexOf(']', open + 1, Math.Min(32, end - open - 1));
            if (close < 0) return null;
            return text.Substring(open + 1, close - open - 1);
        }

        /// <summary>"2026.09.30 20:15:02.117" at <paramref name="pos"/>.</summary>
        private static bool LooksStamped(string s, int pos)
        {
            if (s.Length < pos + StampLength) return false;
            for (int i = 0; i < StampLength; i++)
            {
                char c = s[pos + i];
                switch (i)
                {
                    case 4:
                    case 7:
                    case 19:
                        if (c != '.') return false;
                        break;
                    case 10:
                        if (c != ' ') return false;
                        break;
                    case 13:
                    case 16:
                        if (c != ':') return false;
                        break;
                    default:
                        if (c < '0' || c > '9') return false;
                        break;
                }
            }
            return true;
        }

        private static string? StampOf(string text, Entry e) =>
            LooksStamped(text, e.Start) ? text.Substring(e.Start, StampLength) : null;

        private static bool Contains(IReadOnlyList<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i], value, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>UTF-8 bytes of text[start, end) without allocating (a surrogate pair = 4).</summary>
        public static long Utf8Bytes(string text, int start, int end)
        {
            long n = 0;
            for (int i = start; i < end; i++)
            {
                char c = text[i];
                if (c < 0x80) n += 1;
                else if (c < 0x800) n += 2;
                else if (char.IsHighSurrogate(c) && i + 1 < end && char.IsLowSurrogate(text[i + 1])) { n += 4; i++; }
                else n += 3;
            }
            return n;
        }
    }
}

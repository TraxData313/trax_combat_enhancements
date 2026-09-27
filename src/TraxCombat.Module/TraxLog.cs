using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using TraxCombat.Core;

namespace TraxCombat
{
    /// <summary>
    /// The mod's one rolling log: <c>trax_combat.log</c> beside config.json. Past LogMaxMegabytes
    /// (read live) it is trimmed to about half by <see cref="LogTrim"/>: only the oldest VERBOSE
    /// lines are cut - every other line (load, settings, mission, summaries, errors, first-time
    /// lines) survives (step 10b, review R7). Built for ONE big playtest at the end (CLAUDE.md): every
    /// line is timestamped and tagged by area so the file greps clean - verbose lines carry a
    /// <c>~</c> before their tag (<c>~[damage]</c>) so the trim can tell them apart -
    ///   [load] versions, modules, paths      [config] every value on load, every change
    ///   [compat] RBM detected or not (DESIGN §5)
    ///   [mcm] the menu bridge                [mission] start / end / behaviours attached
    ///   [summary] the per-battle block       [damage] [athletics] [speed] [stepback] [hud] per feature
    ///   [error] every caught exception, with its stack (rate-limited per place)
    ///   [log] notes about the log itself (suppressed-line counts)
    /// Chatty per-event lines go through <see cref="Verbose"/>: only when VerboseLogging is on
    /// (read live) and rate-limited per tag, so a 1000-agent battle cannot flood the file.
    /// One handle stays open between lines (AutoFlush: each line reaches the OS at once), shared
    /// for reading, writing and deleting; <see cref="Release"/> closes it at every mission end.
    /// Best-effort by design: a failed write never costs gameplay, so everything swallows.
    /// Thread-safe (engine callbacks may run off the main thread).
    /// </summary>
    internal static class TraxLog
    {
        private const long BytesPerMegabyte = 1024 * 1024;

        /// <summary>After a failed trim (the file held by another program), how much more is written
        /// before the next try - so a locked file is not re-read at every line.</summary>
        private const long TrimRetryBytes = 1024 * 1024;

        /// <summary>The size cap now: LogMaxMegabytes (read live, at least 1 MB).</summary>
        public static long MaxBytes => Math.Max(1, TraxSettings.Shared.LogMaxMegabytes) * BytesPerMegabyte;

        private static readonly object Gate = new object();
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        // Verbose: a burst of 40 lines per tag, then 20 a second. Errors: 3 per place, then one
        // every 10 s - the first ones carry the stack, the count of the rest is reported.
        private static readonly RateLimiter VerboseLimiter = new RateLimiter(40, 20);
        private static readonly RateLimiter ErrorLimiter = new RateLimiter(3, 0.1);

        // Always-on but rate-limited lines (the player's own exhaustion, party leaders at spawn):
        // a burst of 30 per bucket, then one a second.
        private static readonly RateLimiter NoticeLimiter = new RateLimiter(30, 1);

        private static long _approxBytes = -1;
        private static long _trimRetryAt;
        private static int _errorCount;
        private static int _errorNoticePending;

        // The open log (review 10a R6): one handle kept between lines, AutoFlush - every line reaches
        // the OS at once (nothing lost if the game crashes right after), for ~7 µs a line instead of
        // ~100 µs for an open / append / close per line on the main thread (measured, Documents, 2 MB
        // file). Shared for reading, writing and deleting, so an editor can read the log while the
        // game runs; released at every mission end (Release) and at unload, reopened by the next line.
        private static StreamWriter? _writer;
        private static string? _writerPath;

        private static double Now => Clock.Elapsed.TotalSeconds;

        /// <summary>Errors caught since the game started (the mission summary reports its share).</summary>
        public static int ErrorCount => Volatile.Read(ref _errorCount);

        /// <summary>True when verbose lines would be written at all. On a hot path prefer
        /// <see cref="VerboseWants"/>, which also asks the rate limit.</summary>
        public static bool VerboseOn => TraxSettings.Shared.VerboseLogging;

        /// <summary>
        /// True when a verbose line of <paramref name="bucket"/> would be written NOW - ask it BEFORE
        /// building the line, then pass the same bucket to <see cref="Verbose(string,string,string)"/>
        /// (review R8: with VerboseLogging on most per-hit lines of a big battle are dropped by the rate
        /// limit, and building them first was string work and garbage for nothing). False when
        /// VerboseLogging is off (nothing counted), or when the bucket has no room - then the line
        /// counts as suppressed exactly as if it had been built and dropped.
        /// </summary>
        public static bool VerboseWants(string bucket) => VerboseOn && VerboseLimiter.Peek(bucket, Now);

        public static void Info(string tag, string message) => Write(tag, message, verbose: false);

        /// <summary>A chatty per-event line: dropped unless VerboseLogging is on, rate-limited per tag.</summary>
        public static void Verbose(string tag, string message) => Verbose(tag, message, tag);

        /// <summary>As <see cref="Verbose(string,string)"/>, but rate-limited in its own
        /// <paramref name="bucket"/> - e.g. "damage-skip", so skipped-hit lines never eat the
        /// budget of the roll lines that share the [damage] tag. Written with the verbose mark
        /// (<c>~[tag]</c>): these are the only lines a trim cuts.</summary>
        public static void Verbose(string tag, string message, string bucket)
        {
            if (!VerboseOn) return;
            if (!VerboseLimiter.TryPass(bucket, Now, out int dropped)) return;
            Write(tag, dropped > 0 ? message + " (+" + dropped + " similar lines suppressed)" : message, verbose: true);
        }

        /// <summary>A line written whether or not VerboseLogging is on, but rate-limited in its own
        /// <paramref name="bucket"/> - for events that matter to every playtest yet could repeat
        /// (the player's exhaustion, each party leader at spawn in a huge battle).</summary>
        public static void Limited(string tag, string message, string bucket)
        {
            if (!NoticeLimiter.TryPass(bucket, Now, out int dropped)) return;
            Write(tag, dropped > 0 ? message + " (+" + dropped + " similar lines suppressed)" : message, verbose: false);
        }

        /// <summary>
        /// A caught exception: <c>[error] where: Type: message</c> then the full stack, indented.
        /// Rate-limited per <paramref name="where"/> so a bug in a per-hit hook logs a few
        /// stacks and a count, not a million lines. Also raises the in-game notice (shown on the
        /// main thread by <see cref="TakeErrorNotice"/>).
        /// </summary>
        public static void Error(string where, Exception e)
        {
            Interlocked.Increment(ref _errorCount);
            Interlocked.Exchange(ref _errorNoticePending, 1);
            try
            {
                if (!ErrorLimiter.TryPass(where, Now, out int dropped)) return;
                var sb = new StringBuilder();
                sb.Append(where).Append(": ").Append(e.GetType().FullName).Append(": ").Append(e.Message);
                if (dropped > 0) sb.Append(" (+").Append(dropped).Append(" more here since the last report, suppressed)");
                foreach (var line in e.ToString().Split('\n'))
                    sb.Append(Environment.NewLine).Append("    ").Append(line.TrimEnd('\r'));
                Write("error", sb.ToString(), verbose: false);
            }
            catch
            {
                // never let logging an error cause another
            }
        }

        /// <summary>True once after one or more errors were logged - the SubModule shows a
        /// single in-game line on the main thread so the playtester notices and notes the time.</summary>
        public static bool TakeErrorNotice() => Interlocked.Exchange(ref _errorNoticePending, 0) == 1;

        /// <summary>Writes "[log] N [damage] lines were suppressed" for every tag the limiters
        /// dropped lines of since their last report - called at mission end so the summary
        /// says how chatty the battle really was.</summary>
        public static void FlushSuppressedCounts()
        {
            foreach (var pair in VerboseLimiter.DrainSuppressed())
                Write("log", pair.Value + " more verbose [" + pair.Key + "] lines were suppressed by the rate limit", verbose: false);
            foreach (var pair in NoticeLimiter.DrainSuppressed())
                Write("log", pair.Value + " more [" + pair.Key + "] lines were suppressed by the rate limit", verbose: false);
            foreach (var pair in ErrorLimiter.DrainSuppressed())
                Write("log", pair.Value + " more [error] reports from " + pair.Key + " were suppressed by the rate limit", verbose: false);
        }

        /// <summary>Closes the log file (the next line reopens it) - at every mission end and at unload,
        /// so between battles nothing holds the file. Never throws.</summary>
        public static void Release()
        {
            lock (Gate) CloseWriter();
        }

        private static string Stamp() => DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

        private static void Write(string tag, string message, bool verbose)
        {
            try
            {
                string line = Stamp() + (verbose ? " " + LogTrim.VerboseMark + "[" : " [") + tag + "] " + message + Environment.NewLine;
                lock (Gate)
                {
                    try
                    {
                        string path = ModPaths.LogFilePath;
                        var writer = _writer != null && string.Equals(path, _writerPath, StringComparison.Ordinal) ? _writer : OpenWriter(path);
                        writer.Write(line);
                        _approxBytes += Utf8NoBom.GetByteCount(line);
                        if (_approxBytes > MaxBytes && _approxBytes >= _trimRetryAt) Trim(path);
                    }
                    catch
                    {
                        CloseWriter(); // a failed write drops the handle - the next line opens a fresh one
                    }
                }
            }
            catch
            {
                // the log is a luxury, the game is not
            }
        }

        private static StreamWriter OpenWriter(string path)
        {
            CloseWriter();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            _approxBytes = stream.Length;
            _writer = new StreamWriter(stream, Utf8NoBom) { AutoFlush = true };
            _writerPath = path;
            return _writer;
        }

        private static void CloseWriter()
        {
            var writer = _writer;
            _writer = null;
            _writerPath = null;
            try
            {
                writer?.Dispose();
            }
            catch
            {
                // closing a broken handle - nothing to save
            }
        }

        /// <summary>Trims the file to about half of <see cref="MaxBytes"/> by <see cref="LogTrim"/>'s
        /// rule: the oldest verbose lines go, every other line stays, whole entries only (an error's
        /// stack goes with its line). The handle is closed first (the next line reopens it). A trim
        /// that fails (another program holds the file) is retried only after another
        /// <see cref="TrimRetryBytes"/>, not at every line.</summary>
        private static void Trim(string path)
        {
            CloseWriter();
            try
            {
                string text = ReadShared(path);
                string kept = LogTrim.Apply(text, MaxBytes / 2, Stamp(), Environment.NewLine, out var result);
                File.WriteAllText(path, kept, Utf8NoBom);
                _approxBytes = result.BytesAfter;
                _trimRetryAt = 0;
            }
            catch
            {
                _trimRetryAt = _approxBytes + TrimRetryBytes;
                throw;
            }
        }

        /// <summary>The whole file, read the way an editor would while another handle may write it.</summary>
        private static string ReadShared(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, Utf8NoBom))
                return reader.ReadToEnd();
        }
    }
}

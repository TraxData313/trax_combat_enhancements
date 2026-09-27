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
    /// The mod's one rolling log: <c>trax_combat.log</c> beside config.json, trimmed to its
    /// newest half once it tops ~2 MB. Built for ONE big playtest at the end (CLAUDE.md): every
    /// line is timestamped and tagged by area so the file greps clean -
    ///   [load] versions, modules, paths      [config] every value on load, every change
    ///   [compat] RBM detected or not (DESIGN §5)
    ///   [mcm] the menu bridge                [mission] start / end / behaviours attached
    ///   [summary] the per-battle block       [damage] [endurance] [speed] [hud] per feature
    ///   [error] every caught exception, with its stack (rate-limited per place)
    ///   [log] notes about the log itself (suppressed-line counts)
    /// Chatty per-event lines go through <see cref="Verbose"/>: only when VerboseLogging is on
    /// (read live) and rate-limited per tag, so a 1000-agent battle cannot flood the file.
    /// Best-effort by design: a failed write never costs gameplay, so everything swallows.
    /// Thread-safe (engine callbacks may run off the main thread).
    /// </summary>
    internal static class TraxLog
    {
        public const long TrimAtBytes = 2_000_000;

        private static readonly object Gate = new object();
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        // Verbose: a burst of 40 lines per tag, then 20 a second. Errors: 3 per place, then one
        // every 10 s - the first ones carry the stack, the count of the rest is reported.
        private static readonly RateLimiter VerboseLimiter = new RateLimiter(40, 20);
        private static readonly RateLimiter ErrorLimiter = new RateLimiter(3, 0.1);

        private static long _approxBytes = -1;
        private static int _errorCount;
        private static int _errorNoticePending;

        private static double Now => Clock.Elapsed.TotalSeconds;

        /// <summary>Errors caught since the game started (the mission summary reports its share).</summary>
        public static int ErrorCount => Volatile.Read(ref _errorCount);

        /// <summary>True when verbose lines would be written - check it BEFORE building a
        /// verbose message string, so the per-hit path allocates nothing when it is off.</summary>
        public static bool VerboseOn => TraxSettings.Shared.VerboseLogging;

        public static void Info(string tag, string message) => Write(tag, message);

        /// <summary>A chatty per-event line: dropped unless VerboseLogging is on, rate-limited per tag.</summary>
        public static void Verbose(string tag, string message)
        {
            if (!VerboseOn) return;
            if (!VerboseLimiter.TryPass(tag, Now, out int dropped)) return;
            Write(tag, dropped > 0 ? message + " (+" + dropped + " similar lines suppressed)" : message);
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
                Write("error", sb.ToString());
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
                Write("log", pair.Value + " more verbose [" + pair.Key + "] lines were suppressed by the rate limit");
            foreach (var pair in ErrorLimiter.DrainSuppressed())
                Write("log", pair.Value + " more [error] reports from " + pair.Key + " were suppressed by the rate limit");
        }

        private static void Write(string tag, string message)
        {
            try
            {
                string line = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss.fff", CultureInfo.InvariantCulture)
                    + " [" + tag + "] " + message + Environment.NewLine;
                lock (Gate)
                {
                    string path = ModPaths.LogFilePath;
                    if (_approxBytes < 0)
                    {
                        Directory.CreateDirectory(ModPaths.ConfigDir);
                        _approxBytes = File.Exists(path) ? new FileInfo(path).Length : 0;
                    }
                    File.AppendAllText(path, line, Utf8NoBom);
                    _approxBytes += line.Length;
                    if (_approxBytes > TrimAtBytes) Trim(path);
                }
            }
            catch
            {
                // the log is a luxury, the game is not
            }
        }

        /// <summary>Keeps the newest half, cut at a line break so no half-line survives.</summary>
        private static void Trim(string path)
        {
            string text = File.ReadAllText(path, Utf8NoBom);
            int cut = text.IndexOf('\n', text.Length / 2);
            if (cut < 0) return;
            string kept = DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss.fff", CultureInfo.InvariantCulture)
                + " [log] (older lines trimmed - the log keeps about the newest " + (TrimAtBytes / 2 / 1000) + " KB)"
                + Environment.NewLine + text.Substring(cut + 1);
            File.WriteAllText(path, kept, Utf8NoBom);
            _approxBytes = kept.Length;
        }
    }
}

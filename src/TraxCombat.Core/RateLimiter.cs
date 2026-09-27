using System;
using System.Collections.Generic;

namespace TraxCombat.Core
{
    /// <summary>
    /// A token bucket per tag, so chatty log lines (verbose rolls and blows, a repeating error
    /// in a per-hit hook) cannot flood trax_combat.log in a 1000-agent battle. Each tag may
    /// burst <c>burst</c> lines, then gets <c>perSecond</c> more per second. What is dropped is
    /// counted and reported on the next line that passes ("… (+37 similar lines suppressed)"),
    /// so the log still says how much happened. Time is passed in (seconds, any monotonic
    /// clock) - testable, and no clock calls on the hot path. Thread-safe.
    /// </summary>
    public sealed class RateLimiter
    {
        private sealed class Bucket
        {
            public double Tokens;
            public double LastTime;
            public int Suppressed;
        }

        private readonly double _burst;
        private readonly double _perSecond;
        private readonly Dictionary<string, Bucket> _buckets = new Dictionary<string, Bucket>(StringComparer.Ordinal);
        private readonly object _gate = new object();

        public RateLimiter(double burst, double perSecond)
        {
            if (burst < 1) throw new ArgumentOutOfRangeException(nameof(burst));
            if (perSecond < 0) throw new ArgumentOutOfRangeException(nameof(perSecond));
            _burst = burst;
            _perSecond = perSecond;
        }

        /// <summary>True: write the line - and mention <paramref name="suppressedBefore"/> lines
        /// of this tag that were dropped since the last one that passed. False: drop it (counted).</summary>
        public bool TryPass(string tag, double nowSeconds, out int suppressedBefore)
        {
            lock (_gate)
            {
                if (!_buckets.TryGetValue(tag, out var b))
                {
                    b = new Bucket { Tokens = _burst, LastTime = nowSeconds };
                    _buckets[tag] = b;
                }
                double elapsed = nowSeconds - b.LastTime;
                if (elapsed > 0)
                {
                    b.Tokens = Math.Min(_burst, b.Tokens + elapsed * _perSecond);
                    b.LastTime = nowSeconds;
                }
                if (b.Tokens >= 1)
                {
                    b.Tokens -= 1;
                    suppressedBefore = b.Suppressed;
                    b.Suppressed = 0;
                    return true;
                }
                b.Suppressed++;
                suppressedBefore = 0;
                return false;
            }
        }

        /// <summary>Lines of <paramref name="tag"/> dropped and not yet reported.</summary>
        public int PendingSuppressed(string tag)
        {
            lock (_gate) return _buckets.TryGetValue(tag, out var b) ? b.Suppressed : 0;
        }

        /// <summary>Every tag with dropped lines not yet reported (for a final "N suppressed"
        /// note at mission end), and resets those counts.</summary>
        public List<KeyValuePair<string, int>> DrainSuppressed()
        {
            var list = new List<KeyValuePair<string, int>>();
            lock (_gate)
            {
                foreach (var pair in _buckets)
                {
                    if (pair.Value.Suppressed > 0)
                    {
                        list.Add(new KeyValuePair<string, int>(pair.Key, pair.Value.Suppressed));
                        pair.Value.Suppressed = 0;
                    }
                }
            }
            return list;
        }
    }
}

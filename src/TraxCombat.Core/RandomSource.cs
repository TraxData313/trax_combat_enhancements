using System;
using System.Threading;

namespace TraxCombat.Core
{
    /// <summary>A uniform draw in [0, 1). Injected wherever the mod rolls dice, so tests (and
    /// the offline smoke) can make every roll deterministic.</summary>
    public interface IRandomSource
    {
        /// <summary>A uniform number, 0 inclusive to 1 exclusive.</summary>
        double NextDouble();
    }

    /// <summary>
    /// The game's dice: one <see cref="Random"/> PER THREAD ([ThreadStatic]), each seeded
    /// differently, so it is safe from any thread without a lock. The engine's hit callbacks are
    /// registered as NOT multi-thread callable (<c>[MBCallback(null, false)]</c> on
    /// <c>Mission.MeleeHitCallback</c> / <c>MissileHitCallback</c> / <c>ChargeDamageCallback</c> …,
    /// v1.4.8), i.e. they run on the main thread - but a mod must not bet a crash on that, and
    /// System.Random shared across threads silently breaks (it starts returning 0 forever).
    /// </summary>
    public sealed class ThreadSafeRandom : IRandomSource
    {
        /// <summary>The one instance the game uses.</summary>
        public static readonly ThreadSafeRandom Shared = new ThreadSafeRandom();

        [ThreadStatic]
        private static Random? _local;

        private static int _seedCounter = Environment.TickCount;

        public double NextDouble()
        {
            var r = _local;
            if (r == null)
            {
                // Distinct seeds even for threads created in the same millisecond.
                int seed = unchecked(Interlocked.Increment(ref _seedCounter) * 486187739 + Environment.CurrentManagedThreadId);
                r = new Random(seed);
                _local = r;
            }
            return r.NextDouble();
        }
    }

    /// <summary>A seeded, repeatable source (tests, offline smoke). Locked, so it is safe to
    /// share, but meant for one thread.</summary>
    public sealed class SeededRandom : IRandomSource
    {
        private readonly Random _random;
        private readonly object _gate = new object();

        public SeededRandom(int seed)
        {
            _random = new Random(seed);
        }

        public double NextDouble()
        {
            lock (_gate) return _random.NextDouble();
        }
    }
}

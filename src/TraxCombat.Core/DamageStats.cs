using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// Per-mission damage-roll statistics for the <c>[summary]</c> block (CLAUDE.md, logging):
    /// rolls per category, min / avg / max factor, total damage before → after, a 10-slice
    /// histogram of where the dice fell (flat = fair), every skip by reason, errors by place, and
    /// how many rolls ran off the main thread. Reset at mission start (the damage model outlives
    /// missions). Thread-safe - one short uncontended lock per hit, no allocation.
    /// </summary>
    public sealed class DamageStats
    {
        /// <summary>Histogram slices between the lowest and the highest possible factor.</summary>
        public const int Bins = 10;

        private static readonly int CategoryCount = Enum.GetValues(typeof(DamageCategory)).Length;
        private static readonly int ReasonCount = Enum.GetValues(typeof(DamageSkipReason)).Length;

        private readonly object _gate = new object();
        private readonly Bucket[] _categories;
        private readonly int[] _bins = new int[Bins];
        private readonly int[] _skips = new int[ReasonCount];
        private readonly Dictionary<string, int> _errors = new Dictionary<string, int>(StringComparer.Ordinal);
        private int _offMainThread;

        public DamageStats()
        {
            _categories = new Bucket[CategoryCount];
            for (int i = 0; i < _categories.Length; i++) _categories[i] = new Bucket();
        }

        private sealed class Bucket
        {
            public int Count;
            public double FactorSum;
            public float Min = float.MaxValue;
            public float Max = float.MinValue;
            public long Before;
            public long After;

            public void Clear()
            {
                Count = 0;
                FactorSum = 0;
                Min = float.MaxValue;
                Max = float.MinValue;
                Before = 0;
                After = 0;
            }

            public void Add(in RollOutcome r)
            {
                Count++;
                FactorSum += r.Factor;
                if (r.Factor < Min) Min = r.Factor;
                if (r.Factor > Max) Max = r.Factor;
                Before += r.BeforeRounded;
                After += r.AfterRounded;
            }

            public void AddTo(Bucket total)
            {
                if (Count == 0) return;
                total.Count += Count;
                total.FactorSum += FactorSum;
                if (Min < total.Min) total.Min = Min;
                if (Max > total.Max) total.Max = Max;
                total.Before += Before;
                total.After += After;
            }
        }

        /// <summary>Back to zero - at every mission start.</summary>
        public void Reset()
        {
            lock (_gate)
            {
                foreach (var b in _categories) b.Clear();
                Array.Clear(_bins, 0, _bins.Length);
                Array.Clear(_skips, 0, _skips.Length);
                _errors.Clear();
                _offMainThread = 0;
            }
        }

        public void AddRoll(DamageCategory category, in RollOutcome roll, bool offMainThread = false)
        {
            int bin = (int)(roll.Position * Bins);
            if (bin < 0) bin = 0;
            if (bin >= Bins) bin = Bins - 1;
            lock (_gate)
            {
                _categories[(int)category].Add(in roll);
                _bins[bin]++;
                if (offMainThread) _offMainThread++;
            }
        }

        public void AddSkip(DamageSkipReason reason)
        {
            if (reason == DamageSkipReason.None) return;
            lock (_gate) _skips[(int)reason]++;
        }

        /// <summary>Counts a caught exception at <paramref name="site"/>. True the FIRST time per
        /// site since the last <see cref="Reset"/> - log that one with its stack, count the rest.</summary>
        public bool AddError(string site)
        {
            lock (_gate)
            {
                _errors.TryGetValue(site, out int n);
                _errors[site] = n + 1;
                return n == 0;
            }
        }

        // ------------------------------------------------------------------ reading

        public int Rolls
        {
            get
            {
                lock (_gate)
                {
                    int n = 0;
                    foreach (var b in _categories) n += b.Count;
                    return n;
                }
            }
        }

        public int RollsIn(DamageCategory category)
        {
            lock (_gate) return _categories[(int)category].Count;
        }

        public int Skips(DamageSkipReason reason)
        {
            lock (_gate) return _skips[(int)reason];
        }

        public int SkipsTotal
        {
            get
            {
                lock (_gate)
                {
                    int n = 0;
                    foreach (int s in _skips) n += s;
                    return n;
                }
            }
        }

        public int Errors
        {
            get
            {
                lock (_gate)
                {
                    int n = 0;
                    foreach (var pair in _errors) n += pair.Value;
                    return n;
                }
            }
        }

        public int OffMainThread
        {
            get { lock (_gate) return _offMainThread; }
        }

        /// <summary>A copy of the histogram (index 0 = the lowest slice of the range).</summary>
        public int[] Histogram()
        {
            lock (_gate) return (int[])_bins.Clone();
        }

        /// <summary>Lowest, mean, highest factor over every roll (NaN when none).</summary>
        public void Factors(out float min, out double mean, out float max)
        {
            lock (_gate)
            {
                var total = Total();
                min = total.Count > 0 ? total.Min : float.NaN;
                max = total.Count > 0 ? total.Max : float.NaN;
                mean = total.Count > 0 ? total.FactorSum / total.Count : double.NaN;
            }
        }

        /// <summary>Sum of the rounded damage before and after the rolls (what the player saw).</summary>
        public void Damage(out long before, out long after)
        {
            lock (_gate)
            {
                var total = Total();
                before = total.Before;
                after = total.After;
            }
        }

        private Bucket Total()
        {
            var total = new Bucket();
            foreach (var b in _categories) b.AddTo(total);
            return total;
        }

        // ------------------------------------------------------------------ the summary text

        /// <summary>The <c>[summary]</c> damage lines, plain words (docs/PLAYTEST.md quotes them).</summary>
        public List<string> SummaryLines()
        {
            var lines = new List<string>();
            lock (_gate)
            {
                var total = Total();
                if (total.Count == 0)
                {
                    lines.Add("damage rolls: none this mission");
                }
                else
                {
                    lines.Add("damage rolls: " + total.Count + " hits (melee " + _categories[(int)DamageCategory.Melee].Count
                        + ", ranged " + _categories[(int)DamageCategory.Ranged].Count
                        + ", mounts " + _categories[(int)DamageCategory.Mount].Count
                        + ", shields " + _categories[(int)DamageCategory.Shield].Count
                        + "); factor min " + F2(total.Min) + " / avg " + F3(total.FactorSum / total.Count) + " / max " + F2(total.Max)
                        + "; damage " + total.Before + " → " + total.After + " (" + Percent(total.Before, total.After) + ")");

                    var sb = new StringBuilder("damage by kind:");
                    bool first = true;
                    foreach (DamageCategory c in new[] { DamageCategory.Melee, DamageCategory.Ranged, DamageCategory.Mount, DamageCategory.Shield })
                    {
                        var b = _categories[(int)c];
                        if (b.Count == 0) continue;
                        sb.Append(first ? " " : " | ");
                        first = false;
                        sb.Append(CategoryName(c)).Append(' ').Append(b.Count)
                          .Append(" x").Append(F2(b.Min)).Append("..").Append(F2(b.Max))
                          .Append(" avg ").Append(F3(b.FactorSum / b.Count))
                          .Append(" (").Append(b.Before).Append(" → ").Append(b.After).Append(')');
                    }
                    lines.Add(sb.ToString());

                    lines.Add("damage dice, " + Bins + " equal slices from the lowest to the highest possible roll (even = fair): "
                        + string.Join(" ", Array.ConvertAll(_bins, n => n.ToString(CultureInfo.InvariantCulture))));
                }

                int skipped = 0;
                foreach (int s in _skips) skipped += s;
                if (skipped == 0)
                {
                    lines.Add("damage not rolled: 0 hits");
                }
                else
                {
                    var sb = new StringBuilder("damage not rolled: ").Append(skipped).Append(" hits -");
                    bool first = true;
                    for (int r = 1; r < _skips.Length; r++)
                    {
                        if (_skips[r] == 0) continue;
                        sb.Append(first ? " " : ", ");
                        first = false;
                        sb.Append(SkipName((DamageSkipReason)r)).Append(' ').Append(_skips[r]);
                    }
                    lines.Add(sb.ToString());
                }

                if (_errors.Count == 0)
                {
                    lines.Add("damage roll errors: none");
                }
                else
                {
                    var sb = new StringBuilder("damage roll errors: ");
                    int n = 0;
                    foreach (var pair in _errors) n += pair.Value;
                    sb.Append(n).Append(" (");
                    bool first = true;
                    foreach (var pair in _errors)
                    {
                        if (!first) sb.Append(", ");
                        first = false;
                        sb.Append(pair.Key).Append(' ').Append(pair.Value);
                    }
                    sb.Append(") - each of those hits kept the game's own damage; the first per place is logged as [error] with its stack");
                    lines.Add(sb.ToString());
                }

                if (total.Count > 0)
                {
                    lines.Add(_offMainThread == 0
                        ? "damage rolls ran on the main thread: all " + total.Count
                        : "damage rolls OFF the main thread: " + _offMainThread + " of " + total.Count + " (the dice are per-thread, so this is safe - but tell Claude)");
                }
            }
            return lines;
        }

        public static string CategoryName(DamageCategory c) => c switch
        {
            DamageCategory.Melee => "melee",
            DamageCategory.Ranged => "ranged",
            DamageCategory.Mount => "mounts",
            DamageCategory.Shield => "shields",
            _ => c.ToString(),
        };

        /// <summary>Plain words, naming the setting when a setting caused it.</summary>
        public static string SkipName(DamageSkipReason r) => r switch
        {
            DamageSkipReason.NotAnAgent => "objects (doors, siege engines, ships)",
            DamageSkipReason.NoVictim => "no victim",
            DamageSkipReason.FallDamage => "fall damage",
            DamageSkipReason.ZeroDamage => "0-damage hits",
            DamageSkipReason.SwitchedOff => "switched off (DamageRandomEnabled)",
            DamageSkipReason.SpreadZero => "spread 0 (DamageRandomPercent)",
            DamageSkipReason.ShieldToggleOff => "shield blocks (DamageRandomOnShields off)",
            DamageSkipReason.MountToggleOff => "on mounts (DamageRandomOnMounts off)",
            DamageSkipReason.MeleeToggleOff => "melee (DamageRandomMelee off)",
            DamageSkipReason.RangedToggleOff => "ranged (DamageRandomRanged off)",
            _ => r.ToString(),
        };

        private static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        private static string F3(double v) => v.ToString("0.000", CultureInfo.InvariantCulture);

        private static string Percent(long before, long after)
        {
            if (before <= 0) return "n/a";
            double pct = (after - before) * 100.0 / before;
            return (pct >= 0 ? "+" : string.Empty) + pct.ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }
    }
}

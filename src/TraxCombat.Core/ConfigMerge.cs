using System;
using System.Collections.Generic;

namespace TraxCombat.Core
{
    /// <summary>
    /// THE FILE-REWRITE RULE - what goes into config.json when the mod writes it while a file
    /// already exists (after MCM's Done, or to add settings a newer version introduced).
    ///
    /// The danger it answers: the player hand-edits config.json while the game runs (hand
    /// edits are only read at the next battle start), then changes something else in MCM and
    /// presses Done. Writing the file from memory would silently undo the hand edit.
    ///
    /// The rule, per setting:
    ///   1. changed in MCM since the last write     → memory's value (MCM wins for what it touched);
    ///   2. otherwise, a valid value in the file on DISK NOW (re-read at write time)
    ///                                              → the file's value (the hand edit survives);
    ///   3. otherwise (absent or unusable on disk)  → memory's value.
    /// Unknown keys on disk are carried along in a trailing "not recognised" section. Memory
    /// is NOT updated from the disk values here - hand edits still take effect at the next
    /// battle start, as the file's header promises. A disk file that does not parse at all is
    /// the caller's business (it is backed up, then the file is written from memory).
    /// </summary>
    public static class ConfigMerge
    {
        public sealed class Plan
        {
            internal Plan(Dictionary<string, double> values, List<KeyValuePair<string, string>> unknown,
                List<string> keptFromDisk)
            {
                Values = values;
                Unknown = unknown;
                KeptFromDisk = keptFromDisk;
            }

            /// <summary>The value to write for every setting.</summary>
            public Dictionary<string, double> Values { get; }

            /// <summary>Unrecognised keys carried over from the disk file.</summary>
            public List<KeyValuePair<string, string>> Unknown { get; }

            /// <summary>Settings whose disk value differs from memory and was kept (pending
            /// hand edits) - for the log.</summary>
            public List<string> KeptFromDisk { get; }
        }

        /// <param name="disk">The file as read from disk right now; null or failed = no usable file.</param>
        /// <param name="memory">The live values.</param>
        /// <param name="changedInMcm">Keys MCM changed since the last write (<see cref="EditTracker.ChangedKeys"/>).</param>
        public static Plan ForWrite(ConfigReadResult? disk, IReadOnlyDictionary<string, double> memory,
            ICollection<string> changedInMcm)
        {
            var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var kept = new List<string>();
            var mcm = new HashSet<string>(changedInMcm ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            bool diskUsable = disk != null && disk.Ok;

            foreach (var p in SettingsSchema.All)
            {
                double mem = memory != null && memory.TryGetValue(p.Key, out var m) ? m : p.Default;
                if (mcm.Contains(p.Key))
                {
                    values[p.Key] = mem;
                }
                else if (diskUsable && disk!.Values.TryGetValue(p.Key, out var onDisk))
                {
                    values[p.Key] = onDisk;
                    if (Math.Abs(onDisk - mem) > 1e-9) kept.Add(p.Key);
                }
                else
                {
                    values[p.Key] = mem;
                }
            }

            var unknown = diskUsable ? new List<KeyValuePair<string, string>>(disk!.Unknown) : new List<KeyValuePair<string, string>>();
            return new Plan(values, unknown, kept);
        }
    }

    /// <summary>
    /// Remembers which settings MCM changed since config.json was last written, and what each
    /// held before the first such change - so a slider moved and moved back (or MCM's Cancel,
    /// which replays the old values through the same setters) counts as NOT changed and cannot
    /// overwrite a hand edit of that key.
    /// </summary>
    public sealed class EditTracker
    {
        private readonly Dictionary<string, double> _baseline = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private readonly object _gate = new object();

        /// <summary>Call on every MCM change with the value it replaced.</summary>
        public void Note(ParamDef p, double oldValue)
        {
            lock (_gate)
            {
                if (!_baseline.ContainsKey(p.Key)) _baseline[p.Key] = oldValue;
            }
        }

        /// <summary>Keys whose live value now differs from their value before the first MCM edit.</summary>
        public List<string> ChangedKeys(TraxSettings settings)
        {
            var keys = new List<string>();
            lock (_gate)
            {
                foreach (var pair in _baseline)
                {
                    if (SettingsSchema.TryGet(pair.Key, out var p) && Math.Abs(settings.Get(p) - pair.Value) > 1e-9)
                        keys.Add(p.Key);
                }
            }
            return keys;
        }

        public bool HasEdits
        {
            get { lock (_gate) return _baseline.Count > 0; }
        }

        /// <summary>After a successful write - the file now holds everything MCM did.</summary>
        public void Clear()
        {
            lock (_gate) _baseline.Clear();
        }
    }
}

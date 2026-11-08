using System;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>Step 102: what one input frame did to an AI man's guard clock (the hook counts them).</summary>
    public enum AiGuardNote : byte
    {
        /// <summary>Nothing new (still up, still down, or the guard went down).</summary>
        None,

        /// <summary>His guard went up, seen from the frame before: the clock starts.</summary>
        Rise,

        /// <summary>His guard came back up within <see cref="AiGuardClock.DropSeconds"/> of going down: the old guard goes on (no new start).</summary>
        Flicker,

        /// <summary>The first frame seen after a gap (the callback was off) with his guard up: when it went up is unknown.</summary>
        UnseenStart,

        Count,
    }

    /// <summary>Step 102: what an AI man's guard clock answers at contact.</summary>
    public enum AiClockRead : byte
    {
        /// <summary>His guard went up in a frame the clock saw: the age is known.</summary>
        Known,

        /// <summary>His guard was already up when the clock first saw him (after a gap): unknown.</summary>
        UnseenStart,

        /// <summary>His last input frames held no defend bit (the engine blocked with a guard his input had let go of).</summary>
        NoGuardInInput,

        /// <summary>No input frame of his within <see cref="AiGuardClock.MaxGapSeconds"/> (not watched, his callback off).</summary>
        NotWatched,

        Count,
    }

    /// <summary>
    /// Step 102 (Toni 2026-11-08: the AI fights as before, but a guard it HAPPENS to raise just before the blow lands counts - DESIGN decision 91 (n),
    /// AI_NOTES "Step 102"): ONE AI man's guard clock, fed with the movement flags his AI decided, read (never changed) in step 16's
    /// <c>AgentComponent.OnAIInputSet</c> - the engine's callback runs every frame (~110-130 a second a man, Toni's logs) BEFORE the input is used.
    /// The guard is UP while any defend bit is set (DefendLeft / Right / Up / Down / DefendAuto - <see cref="DefendMask"/>); the clock starts on a
    /// rise seen from the frame before. Like the player's AUTOMATIC block direction, a change of direction while up is NOT a new guard (the AI
    /// turns its guard to the blow by itself). Two guards against false "perfect" reads: a guard back up within <see cref="DropSeconds"/> of going
    /// down goes on from its first rise (a flicker of the bits, ours or his, is not a fresh guard); a frame after a gap of more than
    /// <see cref="MaxGapSeconds"/> (the callback was off) says nothing about when the guard went up (<see cref="AiClockRead.UnseenStart"/>).
    /// A struct on his record - no allocation.
    /// </summary>
    public struct AiGuardClock
    {
        /// <summary><c>MovementControlFlag.DefendMask</c> - DefendLeft | Right | Up | Down | DefendAuto (the smoke checks it against the game's).</summary>
        public const uint DefendMask = 0x7C00;

        /// <summary>Frames further apart than this (s) break the clock's chain: what happened between is unknown. Plumbing (a frame is ~8-33 ms).</summary>
        public const double MaxGapSeconds = 0.25;

        /// <summary>A guard down for less than this (s) and back up is the same guard (a flicker of the bits - the engine's guard does not drop and
        /// rise again in a frame). Plumbing.</summary>
        public const double DropSeconds = 0.1;

        /// <summary>The defend bits of his last frame (0 = no guard).</summary>
        public uint Key;

        /// <summary>His last frame was seen (<see cref="SeenAt"/> valid).</summary>
        public bool Seen;
        public double SeenAt;

        /// <summary>The current guard's rise was seen (<see cref="RaisedAt"/> valid).</summary>
        public bool RaisedKnown;
        public double RaisedAt;

        /// <summary>His guard went down at <see cref="DownSince"/> (valid while <see cref="DownKnown"/>).</summary>
        public bool DownKnown;
        public double DownSince;

        /// <summary>One input frame (his final movement flags) at <paramref name="now"/> (mission s).</summary>
        public AiGuardNote Note(uint movementFlags, double now)
        {
            uint key = movementFlags & DefendMask;
            bool continuous = Seen && now >= SeenAt && now - SeenAt <= MaxGapSeconds;
            Seen = true;
            SeenAt = now;
            if (!continuous)
            {
                Key = key;
                RaisedKnown = false;
                DownKnown = key == 0; // down from now at least: a rise within DropSeconds stays unknown
                DownSince = now;
                return key != 0 ? AiGuardNote.UnseenStart : AiGuardNote.None;
            }
            if (key == 0)
            {
                if (Key != 0)
                {
                    DownKnown = true;
                    DownSince = now;
                }
                Key = 0;
                return AiGuardNote.None;
            }
            var result = AiGuardNote.None;
            if (Key == 0)
            {
                if (DownKnown && now - DownSince < DropSeconds)
                {
                    result = AiGuardNote.Flicker; // the old guard goes on (its rise known or not)
                }
                else
                {
                    RaisedAt = now;
                    RaisedKnown = true;
                    result = AiGuardNote.Rise;
                }
                DownKnown = false;
            }
            Key = key;
            return result;
        }

        /// <summary>At contact (<paramref name="now"/>): the guard's age (s) when it is <see cref="AiClockRead.Known"/>, else NaN and why not. A guard
        /// whose bits went down less than <see cref="DropSeconds"/> ago still counts (the engine's guard is still there). A contact read a little
        /// BEFORE his last frame (another clock's tick time) is in time; an age below 0 reads 0.</summary>
        public AiClockRead Read(double now, out double age)
        {
            age = double.NaN;
            if (!Seen || Math.Abs(now - SeenAt) > MaxGapSeconds) return AiClockRead.NotWatched;
            if (Key == 0 && !(DownKnown && now - DownSince < DropSeconds)) return AiClockRead.NoGuardInInput;
            if (!RaisedKnown) return AiClockRead.UnseenStart;
            age = Math.Max(0, now - RaisedAt);
            return AiClockRead.Known;
        }

        public void Reset() => this = default;
    }

    /// <summary>
    /// Step 102: one mission's AI guard clock - what the clock answered at each AI correct block, the known ages (median, bands), the watch (men
    /// watched, the callbacks turned on for it, the looks and their cost) and the input frames read. The [summary] "timed blocks - the AI's guard
    /// clock" line. Main thread only.
    /// </summary>
    public sealed class AiGuardClockStats
    {
        private const int MaxAges = 1024;

        public readonly int[] ByRead = new int[(int)AiClockRead.Count];
        public readonly int[] AgeBands = new int[TimedBlockMath.AgeBands];
        private readonly double[] _ages = new double[MaxAges];
        private int _ageCount;

        /// <summary>The watch: looks made, watches started / ended, the most men watched at once, callbacks turned on by us for it, the looks' cost.</summary>
        public long Looks;
        public int WatchStarts, WatchEnds, Watched, WatchedPeak, CallbacksTurnedOn;
        public double LookCostMs;

        /// <summary>Input frames read and what they did (indexed by <see cref="AiGuardNote"/>).</summary>
        public long Frames;
        public readonly long[] Notes = new long[(int)AiGuardNote.Count];

        /// <summary>One AI correct block: what the clock answered (and the age when known).</summary>
        public void NoteContact(AiClockRead read, double age)
        {
            ByRead[(int)read]++;
            if (read != AiClockRead.Known || double.IsNaN(age)) return;
            AgeBands[TimedBlockMath.AgeBand(age)]++;
            if (_ageCount < MaxAges) _ages[_ageCount++] = age;
        }

        public void NoteWatch(bool on)
        {
            if (on)
            {
                WatchStarts++;
                Watched++;
                if (Watched > WatchedPeak) WatchedPeak = Watched;
            }
            else
            {
                WatchEnds++;
                if (Watched > 0) Watched--;
            }
        }

        public int Contacts
        {
            get
            {
                int n = 0;
                foreach (var c in ByRead) n += c;
                return n;
            }
        }

        /// <summary>The known AI guard ages' median (s; NaN = none). Summary only (allocates).</summary>
        public double Median()
        {
            if (_ageCount == 0) return double.NaN;
            var a = new double[_ageCount];
            Array.Copy(_ages, a, _ageCount);
            Array.Sort(a);
            return _ageCount % 2 == 1 ? a[_ageCount / 2] : 0.5 * (a[_ageCount / 2 - 1] + a[_ageCount / 2]);
        }

        /// <summary>The [summary] line - the AI's correct blocks by score band (from <paramref name="t"/>, shield + parry), the share with a known
        /// guard age and why the rest had none, the median AI guard age and its bands, the watch, the frames.</summary>
        public string SummaryLine(TimedBlockStats t, double watchMetres, double lookSeconds)
        {
            var c = CultureInfo.InvariantCulture;
            var s = new StringBuilder();
            s.Append("timed blocks - the AI's guard clock (step 102; the defend bits of his own input, READ in the input hook, never changed) - AI correct blocks by timing 0 / 0-0.25 / 0.25-0.5 / 0.5-0.8 / 0.8-1: ");
            for (int b = 0; b < TimedBlockMath.Bands; b++) s.Append(b > 0 ? "/" : string.Empty).Append((t.ByBand[0, 1, b] + t.ByBand[1, 1, b]).ToString(c));
            s.Append(" (mean score ").Append(t.Scored[1] > 0 ? (t.ScoreSum[1] / t.Scored[1]).ToString("0.00", c) : "-").Append(')');
            int n = Contacts, known = ByRead[(int)AiClockRead.Known];
            s.Append("; a known guard age ").Append(known.ToString(c)).Append(" of ").Append(n.ToString(c))
             .Append(n > 0 ? " (" + (100.0 * known / n).ToString("0", c) + "%)" : string.Empty)
             .Append(" - the rest: up before his clock saw it ").Append(ByRead[(int)AiClockRead.UnseenStart].ToString(c))
             .Append(", no guard in his input at contact ").Append(ByRead[(int)AiClockRead.NoGuardInInput].ToString(c))
             .Append(", not watched ").Append(ByRead[(int)AiClockRead.NotWatched].ToString(c));
            double med = Median();
            s.Append("; median AI guard age ").Append(double.IsNaN(med) ? "-" : (1000 * med).ToString("0", c) + " ms").Append(" (");
            for (int b = 1; b < TimedBlockMath.AgeBands; b++) s.Append(b > 1 ? ", " : string.Empty).Append(TimedBlockMath.AgeBandName(b)).Append(' ').Append(AgeBands[b].ToString(c));
            s.Append("); the watch (an enemy within ").Append(watchMetres.ToString("0.#", c)).Append(" m, looked every ").Append(lookSeconds.ToString("0.##", c))
             .Append(" s): men watched at most ").Append(WatchedPeak.ToString(c)).Append(" at once, started ").Append(WatchStarts.ToString(c)).Append(", ended ").Append(WatchEnds.ToString(c))
             .Append(", input callbacks turned on for it ").Append(CallbacksTurnedOn.ToString(c)).Append(", looks ").Append(Looks.ToString(c))
             .Append(" (").Append(LookCostMs.ToString("0.0", c)).Append(" ms in all); input frames read ").Append(Frames.ToString(c))
             .Append(" (guard rises ").Append(Notes[(int)AiGuardNote.Rise].ToString(c)).Append(", flickers kept as one guard ").Append(Notes[(int)AiGuardNote.Flicker].ToString(c))
             .Append(", up when first seen ").Append(Notes[(int)AiGuardNote.UnseenStart].ToString(c)).Append(')');
            return s.ToString();
        }
    }
}

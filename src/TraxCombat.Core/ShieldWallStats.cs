using System;
using System.Collections.Generic;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 41: where the men of a holding shield wall / square / circle stood against their slots, over many samples - how many
    /// more than 1 m and 2 m AHEAD of the slot along the formation's facing, the mean of the part ahead (a man behind his slot
    /// counts 0) and the farthest; all samples and those "in contact" (an enemy within <see cref="WallMath.ContactMetres"/>).
    /// </summary>
    public sealed class WallAheadSamples
    {
        public int Samples;
        public int OverNear;
        public int OverFar;
        public double AheadSum;
        public double Max;
        public int Contact;
        public int ContactOverNear;
        public int ContactOverFar;
        public double ContactAheadSum;
        public double ContactMax;

        public void Add(double ahead, bool contact)
        {
            double a = ahead > 0 && !double.IsNaN(ahead) ? ahead : 0;
            Samples++;
            if (a > WallMath.AheadNearMetres) OverNear++;
            if (a > WallMath.AheadFarMetres) OverFar++;
            AheadSum += a;
            if (a > Max) Max = a;
            if (!contact) return;
            Contact++;
            if (a > WallMath.AheadNearMetres) ContactOverNear++;
            if (a > WallMath.AheadFarMetres) ContactOverFar++;
            ContactAheadSum += a;
            if (a > ContactMax) ContactMax = a;
        }

        /// <summary>The share of all samples more than 1 m ahead (0-1).</summary>
        public double ShareNear => Samples > 0 ? (double)OverNear / Samples : 0;

        public double ShareFar => Samples > 0 ? (double)OverFar / Samples : 0;

        public double ContactShareNear => Contact > 0 ? (double)ContactOverNear / Contact : 0;

        public string Text()
        {
            if (Samples == 0) return "no samples (no AI man stood in a holding shield wall, square or circle while it was so)";
            return Samples + " samples of AI men in a holding shield wall / square / circle: more than " + F0(WallMath.AheadNearMetres) + " m ahead of his slot "
                   + Pct(ShareNear) + ", more than " + F0(WallMath.AheadFarMetres) + " m " + Pct(ShareFar) + ", mean ahead " + F2(AheadSum / Samples)
                   + " m (behind his slot counts 0), the farthest " + F1(Max) + " m; in contact (an enemy within " + F0(WallMath.ContactMetres) + " m) "
                   + (Contact == 0
                       ? "none"
                       : Contact + " samples: more than " + F0(WallMath.AheadNearMetres) + " m ahead " + Pct(ContactShareNear) + ", more than "
                         + F0(WallMath.AheadFarMetres) + " m " + Pct((double)ContactOverFar / Contact) + ", mean ahead " + F2(ContactAheadSum / Contact)
                         + " m, the farthest " + F1(ContactMax) + " m");
        }

        private static string F0(double v) => v.ToString("0", CultureInfo.InvariantCulture);
        private static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
        private static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        private static string Pct(double v) => (v * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>How a "keep your slot" write came about.</summary>
    public enum WallWriteKind
    {
        /// <summary>The staggered look (every <see cref="WallMath.PollSeconds"/>) - a man newly held, and the backstop.</summary>
        Poll,

        /// <summary>Right after vanilla's own update rewrote his flag (its timer moved) - the write that matters.</summary>
        AfterVanilla,

        /// <summary>Vanilla's timer unreadable: every <see cref="WallMath.FallbackRewriteSeconds"/>.</summary>
        Fallback,

        /// <summary>Step 42: a man stepping out held again by his <see cref="LeashMath.LookSeconds"/> look (the leash, or his enemy
        /// gone).</summary>
        Leash,

        Count,
    }

    /// <summary>
    /// Step 41 per-mission numbers and the [summary] "wall" lines: the men looked at and held, every look's verdict, the writes by
    /// kind, vanilla's own updates seen (and how often), the releases to vanilla's value by reason, the slot samples split by the
    /// switch at the sample (ON vs OFF in one battle), the cost. Main thread (the logic's tick).
    /// </summary>
    public sealed class WallStats
    {
        private readonly int[] _looks = new int[(int)WallVerdict.Count];
        private readonly int[] _releases = new int[(int)WallVerdict.Count];
        private readonly int[] _writes = new int[(int)WallWriteKind.Count];

        public int MenLooked;
        public int MenHeld;
        public int VanillaUpdates;
        public double VanillaIntervalSum;
        public int VanillaIntervals;
        public int MenWithoutTimer;
        public int LeftFieldHeld;
        public int HeldAtEnd;
        public int SwitchChanges;
        public int Errors;
        public int Ticks;
        public double TickMsSum;
        public double TickMsMax;
        public bool SettingsChanged;

        /// <summary>The samples while the hold was ON (the switch and the master switch on) and while it was OFF.</summary>
        public readonly WallAheadSamples On = new WallAheadSamples();
        public readonly WallAheadSamples Off = new WallAheadSamples();

        /// <summary>Step 42: the steps out of the wall (the leash).</summary>
        public readonly WallLeashStats Leash = new WallLeashStats();

        public int Looks(WallVerdict v) => _looks[(int)v];
        public int Releases(WallVerdict v) => _releases[(int)v];
        public int Writes(WallWriteKind k) => _writes[(int)k];

        public int WritesTotal
        {
            get
            {
                int s = 0;
                foreach (int v in _writes) s += v;
                return s;
            }
        }

        public int ReleasesTotal
        {
            get
            {
                int s = 0;
                foreach (int v in _releases) s += v;
                return s;
            }
        }

        public void AddLook(WallVerdict v) => _looks[(int)v]++;

        public void AddWrite(WallWriteKind k) => _writes[(int)k]++;

        public void AddRelease(WallVerdict why) => _releases[(int)why]++;

        /// <summary>Vanilla's own update seen on a held man; <paramref name="interval"/> = seconds since the last one seen on him
        /// (NaN for the first).</summary>
        public void AddVanillaUpdate(double interval)
        {
            VanillaUpdates++;
            if (double.IsNaN(interval) || interval <= 0 || interval > 2) return; // a gap = he was not held meanwhile
            VanillaIntervalSum += interval;
            VanillaIntervals++;
        }

        public void AddSample(bool on, double ahead, bool contact) => (on ? On : Off).Add(ahead, contact);

        public void AddTick(double ms)
        {
            Ticks++;
            TickMsSum += ms;
            if (ms > TickMsMax) TickMsMax = ms;
        }

        /// <summary>The [summary] "wall" lines.</summary>
        public List<string> SummaryLines(in WallRules r)
        {
            var lines = new List<string>(5);
            lines.Add("shield wall holds its slots (step 41): " + r.Describe()
                      + (SwitchChanges > 0 ? " - switched " + SwitchChanges + " time(s) during this battle (the ON / OFF lines below split the samples by it)" : string.Empty));
            if (MenLooked == 0)
            {
                lines.Add("wall: nobody was looked at (no fighter on the field)");
                return lines;
            }
            lines.Add("wall: " + MenLooked + " men looked at, " + MenHeld + " held in their slots at some point; looks - held " + Looks(WallVerdict.Hold)
                      + ", left to vanilla: " + ReasonsText(_looks, skipHold: true));
            lines.Add("wall writes: " + WritesTotal + " \"keep your slot\" writes - right after vanilla's own update " + Writes(WallWriteKind.AfterVanilla)
                      + ", by the " + F2(WallMath.PollSeconds) + " s look " + Writes(WallWriteKind.Poll) + ", by the fallback " + Writes(WallWriteKind.Fallback)
                      + ", by the step-out leash " + Writes(WallWriteKind.Leash)
                      + "; vanilla's own updates seen on held men " + VanillaUpdates
                      + (VanillaIntervals > 0 ? " (one every " + F2(VanillaIntervalSum / VanillaIntervals) + " s per man - the game's timer is 0.45-0.55 s)" : string.Empty)
                      + ", men whose update timer could not be read " + MenWithoutTimer + " (should be 0 - else the fallback)"
                      + "; released to vanilla's own value " + ReleasesTotal + (ReleasesTotal > 0 ? " (" + ReasonsText(_releases, skipHold: true) + ")" : string.Empty)
                      + "; held men who fell or left the field " + LeftFieldHeld + ", still held when the battle ended " + HeldAtEnd
                      + "; errors " + Errors + "; cost " + (Ticks > 0 ? "avg " + F3(TickMsSum / Ticks) + " ms a tick, max " + F2(TickMsMax) + " ms" : "no tick"));
            lines.Add("wall slots ON: " + On.Text());
            lines.Add("wall slots OFF (vanilla): " + Off.Text() + " - compare with the ON line: the hold works when ON's \"more than 1 m ahead\" (in contact above all) is well below OFF's");
            lines.Add(Leash.SummaryLine(r.StepOutMetres));
            return lines;
        }

        private static string ReasonsText(int[] counts, bool skipHold)
        {
            var parts = new List<string>();
            for (int i = 0; i < counts.Length; i++)
            {
                if (skipHold && i == (int)WallVerdict.Hold) continue;
                if (counts[i] > 0) parts.Add(WallMath.Name((WallVerdict)i) + " " + counts[i]);
            }
            return parts.Count == 0 ? "none" : string.Join(", ", parts);
        }

        private static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        private static string F3(double v) => v.ToString("0.000", CultureInfo.InvariantCulture);
    }
}

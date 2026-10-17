using System;
using System.Collections.Generic;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 49 - one mission's courage numbers and the [summary] "courage" lines (AI_NOTES "Step 49"): Anton's playtest proof - how long
    /// AI men stand ISOLATED among enemies and how far they roam from their formation's centre, with courage ON and OFF (the switch read at
    /// each look - flip it mid-battle to compare), the timid spells, the walks back and the sprints, who is brave or timid at which local
    /// odds, the behaviour weights, the cost. Plain counters - the logic's main thread only.
    /// </summary>
    public sealed class CourageStats
    {
        /// <summary>The distance histogram: 1 m bins, the last one open (the p90).</summary>
        public const int DistanceBins = 61;

        private readonly double[] _manSeconds = new double[2];
        private readonly double[] _isolatedSeconds = new double[2];
        private readonly double[] _contactSeconds = new double[2];
        private readonly long[][] _distance = { new long[DistanceBins], new long[DistanceBins] };
        private readonly double[] _distanceSum = new double[2];
        private readonly long[] _distanceLooks = new long[2];
        private readonly long[,] _oddsContact = new long[CourageMath.OddsBands, 2];
        private readonly long[,] _oddsOut = new long[CourageMath.OddsBands, 2];
        private readonly int[] _skips = new int[(int)CourageSkip.Count];
        private readonly int[] _ends = new int[(int)CourageEnd.Count];
        private readonly int[] _regroupEnds = new int[(int)RegroupEnd.Count];
        private readonly int[] _refusals = new int[(int)CourageSprintRefusal.Count];
        private MeanStd _regroupSeconds;
        private double _regroupMax;
        private MeanStd _toughness;
        private readonly MeanStd[] _compressFrom = new MeanStd[2];
        private readonly MeanStd[] _compressTo = new MeanStd[2];
        private double _toughMin = double.NaN, _toughMax = double.NaN;

        public int MenLooked;
        public long Looks;
        public int TimidStarts;
        public int TimidByShock;
        public int WalksToGroup;
        public int WalksToCentre;
        public int PanicWalks;
        public int PanicSprints;
        public int TimidMen;
        public int StartedMoving;
        public int StartedHolding;
        public int MovedAgain;
        public int Wobbles;
        public int SprintsAsked;
        public int SprintsStarted;
        public double SprintSeconds;
        public int WeightsSet;
        public long WeightsReapplied;
        public int WeightsRestored;
        public int WeightErrors;
        public long SteerFrames;
        public int ScanTruncated;
        public double PollCostMs;
        public double SteerCostMs;
        public long Ticks;

        // ---- the looks

        /// <summary>One look's time since his last (gap), by the switch then (on): his total, isolated and in-contact seconds.</summary>
        public void AddLook(bool on, double gap, bool isolated, bool contact)
        {
            Looks++;
            if (!(gap > 0)) return;
            int k = on ? 1 : 0;
            _manSeconds[k] += gap;
            if (isolated) _isolatedSeconds[k] += gap;
            if (contact) _contactSeconds[k] += gap;
        }

        /// <summary>His distance from his formation's centre at a look, by the switch then.</summary>
        public void AddDistance(bool on, double metres)
        {
            if (double.IsNaN(metres) || metres < 0) return;
            int k = on ? 1 : 0;
            int bin = (int)metres;
            if (bin >= DistanceBins) bin = DistanceBins - 1;
            _distance[k][bin]++;
            _distanceSum[k] += metres;
            _distanceLooks[k]++;
        }

        /// <summary>A look in contact (courage on): brave or timid at his local odds; <paramref name="outOfFormation"/> = far from its centre.</summary>
        public void AddOdds(double odds, bool timid, bool outOfFormation)
        {
            int band = CourageMath.OddsBand(odds);
            _oddsContact[band, timid ? 1 : 0]++;
            if (outOfFormation) _oddsOut[band, timid ? 1 : 0]++;
        }

        public void AddSkip(CourageSkip s)
        {
            if (s >= 0 && s < CourageSkip.Count) _skips[(int)s]++;
        }

        public void AddToughness(double t)
        {
            if (double.IsNaN(t)) return;
            _toughness.Add(t);
            if (double.IsNaN(_toughMin) || t < _toughMin) _toughMin = t;
            if (double.IsNaN(_toughMax) || t > _toughMax) _toughMax = t;
        }

        public void AddEnd(CourageEnd e)
        {
            if (e >= 0 && e < CourageEnd.Count) _ends[(int)e]++;
        }

        /// <summary>A walk back ended: why, and how long it took (arrived / among mates go into the time to regroup).</summary>
        public void AddRegroupEnd(RegroupEnd e, double seconds)
        {
            if (e < 0 || e >= RegroupEnd.Count) return;
            _regroupEnds[(int)e]++;
            if ((e == RegroupEnd.Arrived || e == RegroupEnd.AmongMates) && !double.IsNaN(seconds))
            {
                _regroupSeconds.Add(seconds);
                if (seconds > _regroupMax) _regroupMax = seconds;
            }
        }

        /// <summary>A walk back's distance from his formation's centre at its start and at its end, by shock (the compression: a shocked
        /// side packing in shows as the "after" well below the "before").</summary>
        public void AddCompression(bool shocked, double before, double after)
        {
            if (double.IsNaN(before) || double.IsNaN(after)) return;
            int k = shocked ? 1 : 0;
            _compressFrom[k].Add(before);
            _compressTo[k].Add(after);
        }

        public MeanStd CompressFrom(bool shocked) => _compressFrom[shocked ? 1 : 0];
        public MeanStd CompressTo(bool shocked) => _compressTo[shocked ? 1 : 0];

        public void AddRefusal(CourageSprintRefusal r)
        {
            if (r >= 0 && r < CourageSprintRefusal.Count) _refusals[(int)r]++;
        }

        // ---- reads (tests, smoke)

        public double ManSeconds(bool on) => _manSeconds[on ? 1 : 0];
        public double IsolatedSeconds(bool on) => _isolatedSeconds[on ? 1 : 0];
        public double ContactSeconds(bool on) => _contactSeconds[on ? 1 : 0];
        public long DistanceLooks(bool on) => _distanceLooks[on ? 1 : 0];
        public int Skipped(CourageSkip s) => _skips[(int)s];
        public int Ended(CourageEnd e) => _ends[(int)e];
        public int RegroupEnded(RegroupEnd e) => _regroupEnds[(int)e];
        public int Refused(CourageSprintRefusal r) => _refusals[(int)r];
        public long OddsLooks(int band, bool timid) => _oddsContact[band, timid ? 1 : 0];
        public long OddsOutLooks(int band, bool timid) => _oddsOut[band, timid ? 1 : 0];
        public MeanStd RegroupSeconds => _regroupSeconds;

        /// <summary>Isolated seconds per man-minute looked (NaN with no time).</summary>
        public double IsolatedPerManMinute(bool on)
        {
            double t = _manSeconds[on ? 1 : 0];
            return t > 0 ? _isolatedSeconds[on ? 1 : 0] / (t / 60.0) : double.NaN;
        }

        public double MeanDistance(bool on)
        {
            int k = on ? 1 : 0;
            return _distanceLooks[k] > 0 ? _distanceSum[k] / _distanceLooks[k] : double.NaN;
        }

        /// <summary>The distance at or below which <paramref name="share"/> of the looks fell (the bin's upper edge, 1 m bins).</summary>
        public double DistancePercentile(bool on, double share)
        {
            int k = on ? 1 : 0;
            long n = _distanceLooks[k];
            if (n == 0) return double.NaN;
            long need = (long)Math.Ceiling(share * n);
            if (need < 1) need = 1;
            long acc = 0;
            for (int b = 0; b < DistanceBins; b++)
            {
                acc += _distance[k][b];
                if (acc >= need) return b + 1;
            }
            return DistanceBins;
        }

        // ---- the summary

        private static string N(double v, string f) => double.IsNaN(v) ? "-" : v.ToString(f, CultureInfo.InvariantCulture);

        /// <summary>The [summary] "courage" lines.</summary>
        public void AddSummaryLines(List<string> lines, in CourageRules r)
        {
            lines.Add("courage settings: " + r.Describe());
            if (MenLooked == 0)
            {
                lines.Add("courage: no AI man on foot in a formation was looked at in this mission (a tournament, an arena, a duel, a siege's attackers only, or nobody fought)");
                return;
            }
            string radius = N(r.RadiusMetres, "0.0");
            lines.Add("courage isolated (fewer than " + CourageMath.IsolatedMates + " mates within " + radius + " m while an enemy is within it): "
                      + "ON " + N(IsolatedPerManMinute(true), "0.00") + " man-seconds per AI man-minute (" + N(_isolatedSeconds[1], "0") + " s of " + N(_manSeconds[1] / 60, "0.0")
                      + " man-minutes looked, " + N(_contactSeconds[1] / 60, "0.0") + " of them in contact), OFF " + N(IsolatedPerManMinute(false), "0.00") + " ("
                      + N(_isolatedSeconds[0], "0") + " s of " + N(_manSeconds[0] / 60, "0.0") + ", " + N(_contactSeconds[0] / 60, "0.0") + " in contact) - "
                      + "ON well below OFF = men stay with their mates");
            lines.Add("courage distance from the formation's centre (its men's centre of mass, every look): ON mean " + N(MeanDistance(true), "0.0") + " m, p90 "
                      + N(DistancePercentile(true, 0.9), "0") + " m (" + _distanceLooks[1] + " looks); OFF mean " + N(MeanDistance(false), "0.0") + " m, p90 "
                      + N(DistancePercentile(false, 0.9), "0") + " m (" + _distanceLooks[0] + " looks)");
            lines.Add("courage timid: " + TimidStarts + " times by " + TimidMen + " of " + MenLooked + " men looked (" + StartedMoving + " walked back at once, " + StartedHolding
                      + " held among their mates, " + MovedAgain + " later walked again); caused by SHOCK (a landed hit within the shock window) " + TimidByShock
                      + ", by the odds alone " + (TimidStarts - TimidByShock) + "; wobbles (a flip within " + N(CourageMath.WobbleSeconds, "0") + " s of the last) " + Wobbles
                      + "; ends: " + Join(_ends, i => CourageMath.EndName((CourageEnd)i)) + "; not looked at: " + Join(_skips, i => CourageMath.SkipName((CourageSkip)i)));
            lines.Add("courage regroup: walks back ended " + Join(_regroupEnds, i => CourageMath.RegroupEndName((RegroupEnd)i)) + "; time to regroup (arrived / among mates) mean "
                      + N(_regroupSeconds.Mean, "0.0") + " s, max " + N(_regroupSeconds.Count > 0 ? _regroupMax : double.NaN, "0.0") + " s; sprinted back " + SprintsStarted + " of "
                      + SprintsAsked + " asked (" + N(SprintSeconds, "0.0") + " s of sprint; refused: " + Join(_refusals, i => CourageMath.RefusalName((CourageSprintRefusal)i))
                      + "); steered " + SteerFrames + " frames");
            lines.Add("courage flocking and panic: walks to the nearest bigger group of their side (" + r.FlockMinMen + "+ allies within " + N(r.FlockRadiusMetres, "0")
                      + " m) " + WalksToGroup + ", to the formation's centre " + WalksToCentre + "; PANIC walks (shocked, odds below " + r.PanicOddsPercent + "% of the required) "
                      + PanicWalks + ", panic sprints " + PanicSprints + " (keeping " + r.PanicSprintKeepPercent + "% instead of " + r.SprintKeepPercent + "%); compression - "
                      + "distance from the formation's centre at a walk's start → end: shocked " + N(_compressFrom[1].Mean, "0.0") + " → " + N(_compressTo[1].Mean, "0.0")
                      + " m (" + _compressFrom[1].Count + " walks), by the odds alone " + N(_compressFrom[0].Mean, "0.0") + " → " + N(_compressTo[0].Mean, "0.0") + " m ("
                      + _compressFrom[0].Count + ")");
            lines.Add("courage by local odds, in contact (an enemy within " + radius + " m, courage on): " + OddsText(_oddsContact));
            lines.Add("courage by local odds, men OUT of their formation (more than " + N(r.RegroupRadiusMetres * CourageMath.OutOfFormationRadii, "0.0")
                      + " m from its centre - the flankers and roamers): " + OddsText(_oddsOut));
            lines.Add("courage toughness (per man, read once): min " + N(_toughMin, "0.00") + ", mean " + N(_toughness.Mean, "0.00") + ", max " + N(_toughMax, "0.00")
                      + " (" + _toughness.Count + " men); behaviour weights: set " + WeightsSet + ", re-applied " + WeightsReapplied + " (the game resets them on every order), given back "
                      + WeightsRestored + ", errors " + WeightErrors + (ScanTruncated > 0 ? "; crowded looks cut at the scan's size " + ScanTruncated : string.Empty));
            lines.Add("courage cost: " + Looks + " looks, " + N(Ticks > 0 ? PollCostMs / Ticks : double.NaN, "0.000") + " ms a tick looking, "
                      + N(Ticks > 0 ? SteerCostMs / Ticks : double.NaN, "0.000") + " ms a tick steering");
        }

        private static string OddsText(long[,] table)
        {
            var parts = new List<string>(CourageMath.OddsBands);
            for (int b = 0; b < CourageMath.OddsBands; b++)
            {
                long brave = table[b, 0], timid = table[b, 1];
                long all = brave + timid;
                parts.Add(CourageMath.OddsBandName(b) + ": " + (all == 0 ? "-" : (100.0 * timid / all).ToString("0", CultureInfo.InvariantCulture) + "% timid of " + all));
            }
            return string.Join(", ", parts) + " (looks)";
        }

        private static string Join(int[] counts, Func<int, string> name)
        {
            var parts = new List<string>();
            for (int i = 0; i < counts.Length; i++)
                if (counts[i] > 0) parts.Add(name(i) + " " + counts[i]);
            return parts.Count == 0 ? "none" : string.Join(", ", parts);
        }
    }
}

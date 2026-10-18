using System;
using System.Collections.Generic;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 55: cavalry flocking over one mission - the census ON and OFF, the riders by kind, the share of rider time FREE / FLOCKING / IN A WAVE,
    /// flocks started and ended, the FLIPS (a flock released at once) with their size and the leader's eagerness by the flock's mean eagerness, the
    /// TIME BETWEEN PASSES per rider by eagerness band and the LONE PASSES ON vs OFF - the proof against "one by one" -, the spread, the lever's
    /// calls, the cost. Main thread.
    /// </summary>
    public sealed class CavalryFlockStats
    {
        private readonly int[] _ridersByKind = new int[FlockMath.KindCount];
        private readonly int[] _skips = new int[(int)FlockSkip.Count];
        private readonly int[] _starts = new int[(int)FlockReason.Count];
        private readonly int[] _startsByKind = new int[FlockMath.KindCount];
        private readonly int[] _ends = new int[(int)FlockEnd.Count];
        private readonly double[] _riderSeconds = new double[2];
        private readonly double[] _stateSeconds = new double[3];
        private readonly int[] _censuses = new int[2];
        private readonly int[] _passes = new int[2];
        private readonly int[] _lone = new int[2];
        private readonly MeanStd[] _passGapOn = new MeanStd[FlockMath.EagerBands];
        private readonly MeanStd[] _passGapOff = new MeanStd[FlockMath.EagerBands];
        private readonly MeanStd[] _waveByBand = new MeanStd[FlockMath.EagerBands];
        private MeanStd _leader;
        private MeanStd _spellSeconds;
        private double _spellMax;
        private MeanStd _spreadOn, _spreadOff;

        public int Riders { get; private set; }
        public int Flips { get; private set; }
        public int WaveRiders { get; private set; }
        public int LargestWave { get; private set; }
        public int MovesIssued;
        public int MovesReissued;
        public int MovesNoGround;
        public int CannotStart;
        public int Releases;
        public int LeverErrors;
        public int WeightsSet;
        public int WeightsReapplied;
        public int WeightsRestored;
        public int TimidLooks;
        public int TimidFound;
        public int GoalMissing;
        public int ScanTruncated;
        public double CostMs;
        public double MaxCostMs;

        public int Started(FlockReason r) => _starts[(int)r];
        public int Ended(FlockEnd e) => _ends[(int)e];
        public int Skipped(FlockSkip s) => _skips[(int)s];
        public int RidersOf(RiderKind k) => _ridersByKind[(int)k];
        public int StartsTotal { get { int n = 0; foreach (var v in _starts) n += v; return n; } }
        public int Censuses(bool on) => _censuses[on ? 1 : 0];
        public double RiderSeconds(bool on) => _riderSeconds[on ? 1 : 0];
        public double StateSeconds(RiderFlockState s) => _stateSeconds[(int)s];
        public int Passes(bool on) => _passes[on ? 1 : 0];
        public int LonePasses(bool on) => _lone[on ? 1 : 0];
        public double MeanSpread(bool on) => on ? _spreadOn.Mean : _spreadOff.Mean;
        public int SpreadSamples(bool on) => on ? _spreadOn.Count : _spreadOff.Count;
        public double MeanWave(int band) => _waveByBand[band].Mean;
        public int WavesIn(int band) => _waveByBand[band].Count;
        public double MeanLeader => _leader.Mean;
        public double MeanPassGap(bool on, int band) => on ? _passGapOn[band].Mean : _passGapOff[band].Mean;
        public int PassGaps(bool on, int band) => on ? _passGapOn[band].Count : _passGapOff[band].Count;

        /// <summary>The lone share of the passes (NaN = none).</summary>
        public double LoneShare(bool on) => _passes[on ? 1 : 0] > 0 ? (double)_lone[on ? 1 : 0] / _passes[on ? 1 : 0] : double.NaN;

        /// <summary>Lone passes per rider-minute (NaN = no rider time).</summary>
        public double LonePerRiderMinute(bool on) => _riderSeconds[on ? 1 : 0] > 0 ? _lone[on ? 1 : 0] / (_riderSeconds[on ? 1 : 0] / 60) : double.NaN;

        public double FlipsPerMinute(double battleMinutes) => battleMinutes > 0 ? Flips / battleMinutes : double.NaN;

        public double StateShare(RiderFlockState s)
        {
            double all = _stateSeconds[0] + _stateSeconds[1] + _stateSeconds[2];
            return all > 0 ? _stateSeconds[(int)s] / all : double.NaN;
        }

        public void AddRider(RiderKind k)
        {
            Riders++;
            _ridersByKind[(int)k]++;
        }

        public void AddSkip(FlockSkip s)
        {
            if (s < FlockSkip.Count) _skips[(int)s]++;
        }

        public void AddCensus(bool on, int riders, double dt)
        {
            _censuses[on ? 1 : 0]++;
            if (dt > 0) _riderSeconds[on ? 1 : 0] += riders * dt;
        }

        /// <summary>One rider's state for one census interval (ON only).</summary>
        public void AddState(RiderFlockState s, double dt)
        {
            if (dt > 0) _stateSeconds[(int)s] += dt;
        }

        public void AddSpread(bool on, double metres)
        {
            if (double.IsNaN(metres)) return;
            if (on) _spreadOn.Add(metres);
            else _spreadOff.Add(metres);
        }

        public void AddStart(FlockReason r, RiderKind k)
        {
            if (r < FlockReason.Count) _starts[(int)r]++;
            _startsByKind[(int)k]++;
        }

        public void AddEnd(FlockEnd e, double seconds)
        {
            if (e < FlockEnd.Count) _ends[(int)e]++;
            if (seconds > 0)
            {
                _spellSeconds.Add(seconds);
                if (seconds > _spellMax) _spellMax = seconds;
            }
        }

        /// <summary>A FLIP: its size, the flock's mean eagerness (the band), its leader's.</summary>
        public void AddFlip(int size, double meanEagerness, double leaderEagerness)
        {
            if (size <= 0) return;
            Flips++;
            WaveRiders += size;
            if (size > LargestWave) LargestWave = size;
            _waveByBand[FlockMath.EagerBand(meanEagerness)].Add(size);
            if (!double.IsNaN(leaderEagerness)) _leader.Add(leaderEagerness);
        }

        /// <summary>A PASS (an AI rider's attack after a quiet gap): lone or not; the gap since his last pass, filed by his eagerness now.</summary>
        public void AddPass(bool on, bool lone, double gapSeconds, double eagerness)
        {
            _passes[on ? 1 : 0]++;
            if (lone) _lone[on ? 1 : 0]++;
            if (!double.IsNaN(gapSeconds) && gapSeconds > 0)
            {
                int b = FlockMath.EagerBand(eagerness);
                if (on) _passGapOn[b].Add(gapSeconds);
                else _passGapOff[b].Add(gapSeconds);
            }
        }

        public void AddCost(double ms)
        {
            CostMs += ms;
            if (ms > MaxCostMs) MaxCostMs = ms;
        }

        private static string N(double v, string f) => double.IsNaN(v) ? "-" : v.ToString(f, CultureInfo.InvariantCulture);

        private static string Pct(double share) => double.IsNaN(share) ? "-" : (100 * share).ToString("0.0", CultureInfo.InvariantCulture) + "%";

        private static string Join(int[] counts, Func<int, string> name)
        {
            var parts = new List<string>();
            for (int i = 0; i < counts.Length; i++)
                if (counts[i] > 0) parts.Add(name(i) + " " + counts[i]);
            return parts.Count == 0 ? "none" : string.Join(", ", parts);
        }

        private static string Bands(MeanStd[] by, string unit, string f)
        {
            var parts = new List<string>();
            for (int b = 0; b < by.Length; b++) parts.Add(FlockMath.BandName(b) + " " + N(by[b].Mean, f) + unit + " (" + by[b].Count + ")");
            return string.Join(", ", parts);
        }

        /// <summary>The [summary] "cavalry" lines (7, or 2 when no AI rider on a charge was ever seen).</summary>
        public void AddSummaryLines(List<string> lines, in FlockRules r, string lever, double battleMinutes)
        {
            lines.Add("cavalry flocking settings: " + r.Describe());
            if (Riders == 0)
            {
                lines.Add("cavalry flocking: no AI rider under a charge was seen in this mission (no cavalry, or it never charged; not looked at: "
                          + Join(_skips, i => FlockMath.SkipName((FlockSkip)i)) + ")");
                return;
            }
            lines.Add("cavalry flocking: " + Riders + " AI riders seen (" + Join(_ridersByKind, i => FlockMath.KindName((RiderKind)i)) + "), " + N(RiderSeconds(true) / 60, "0.0")
                      + " rider-minutes ON / " + N(RiderSeconds(false) / 60, "0.0") + " OFF; rider time ON: FREE " + Pct(StateShare(RiderFlockState.Free)) + ", FLOCKING "
                      + Pct(StateShare(RiderFlockState.Flocking)) + ", IN A WAVE " + Pct(StateShare(RiderFlockState.Wave)) + "; flocks started " + StartsTotal + " ("
                      + Join(_starts, i => FlockMath.ReasonName((FlockReason)i)) + "; by kind " + Join(_startsByKind, i => FlockMath.KindName((RiderKind)i)) + "); ended: "
                      + Join(_ends, i => FlockMath.EndName((FlockEnd)i)) + "; a spell mean " + N(_spellSeconds.Mean, "0.0") + " s, max "
                      + N(_spellSeconds.Count > 0 ? _spellMax : double.NaN, "0.0") + " s; could not start (the game's scripted-movement gate) " + CannotStart + "; not looked at: "
                      + Join(_skips, i => FlockMath.SkipName((FlockSkip)i)));
            lines.Add("cavalry flips (a flock released at once toward its goal - a wave): " + Flips + " (" + N(FlipsPerMinute(battleMinutes), "0.00") + " a minute) of " + WaveRiders
                      + " riders, max " + LargestWave + "; wave size by the flock's mean eagerness: " + Bands(_waveByBand, " riders", "0.0") + " - small waves fresh, big waves tired; "
                      + "the leader's eagerness mean " + Pct(_leader.Mean));
            lines.Add("cavalry time between passes per rider (an attack after " + N(FlockMath.PassGapSeconds, "0") + " s of none), by his eagerness at the pass: ON " + Bands(_passGapOn, " s", "0.0")
                      + "; OFF " + Bands(_passGapOff, " s", "0.0") + " - ON: it should grow as they tire");
            lines.Add("cavalry lone passes (a pass with fewer than " + FlockMath.LonePassMates + " of his kind within " + N(FlockMath.LonePassMetres, "0") + " m): ON "
                      + _lone[1] + " of " + _passes[1] + " (" + Pct(LoneShare(true)) + ", " + N(LonePerRiderMinute(true), "0.00") + " per rider-minute), OFF " + _lone[0] + " of "
                      + _passes[0] + " (" + Pct(LoneShare(false)) + ", " + N(LonePerRiderMinute(false), "0.00") + ") - ON below OFF = not one by one");
            lines.Add("cavalry spread (mean distance of a kind's riders from its centre, every census, flocks of 2+): ON " + N(_spreadOn.Mean, "0.0") + " m (" + _spreadOn.Count
                      + " flock-censuses), OFF " + N(_spreadOff.Mean, "0.0") + " m (" + _spreadOff.Count + ")");
            int censuses = Censuses(true) + Censuses(false);
            lines.Add("cavalry lever: " + lever + " - points sent " + MovesIssued + " (" + MovesReissued + " re-sent as the flock moved), refused (no ground there) "
                      + MovesNoGround + ", released (DisableScriptedMovement) " + Releases + ", errors " + LeverErrors + "; horseback weights set " + WeightsSet + " / re-applied "
                      + WeightsReapplied + " / given back " + WeightsRestored + "; no goal read (the unit's centre only) " + GoalMissing + " flock-censuses; timid looks " + TimidLooks
                      + " (" + TimidFound + " timid" + (ScanTruncated > 0 ? ", " + ScanTruncated + " cut at the scan's capacity" : "") + "); cost "
                      + N(censuses > 0 ? CostMs / censuses : double.NaN, "0.000") + " ms a census (max " + N(MaxCostMs, "0.000") + ", " + censuses + " censuses)");
        }
    }
}

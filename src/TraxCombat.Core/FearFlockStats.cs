using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 67 per mission (AI_NOTES "Step 67"): the fear flock measured ON and OFF from the same looks - the units' spread (mean distance
    /// from their centre) and depth ÷ width (the thickening), the centre's drift back a minute by order, men with a comrade between them
    /// and the nearest enemy, nobody behind; fear and the pull by discipline band; the spells, their ends, the anchors, the sprints back,
    /// courage's walks taking over, the walls broken, the steering, the cost - the 6 [summary] "fear flock" lines.
    /// </summary>
    public sealed class FearStats
    {
        private const int Sides = 2; // [0] OFF, [1] ON

        private readonly long[] _looks = new long[Sides], _contact = new long[Sides], _covered = new long[Sides], _alone = new long[Sides], _nobodyBehind = new long[Sides];
        private MeanStd _spreadOn, _spreadOff, _shapeOn, _shapeOff, _depthOn, _depthOff, _widthOn, _widthOff;
        private readonly double[] _driftBack = new double[Sides * FearMath.DriftGroups], _driftSeconds = new double[Sides * FearMath.DriftGroups];
        private readonly double[] _bandFear = new double[FearMath.DisciplineBands];
        private readonly long[] _bandLooks = new long[FearMath.DisciplineBands], _bandAfraid = new long[FearMath.DisciplineBands];
        private readonly double[] _bandPull = new double[FearMath.DisciplineBands];
        private readonly int[] _ends = new int[(int)FearEnd.Count];
        private readonly int[] _refusals = new int[8];
        private readonly long[] _flockLooksByOrder = new long[(int)FearOrder.Count];
        private MeanStd _anchorSeconds;

        public int MenLooked;
        public int Spells;
        public int SpellMen;
        public int Anchors;
        public int AnchorsBroken;
        public int SprintsAsked;
        public int SprintsStarted;
        public int SprintsByChance;
        public int SprintsAlone;
        public long YieldedLooks;
        public int WallsBroken;
        public int WallBreakMen;
        public long SteerUpdates;
        public long StandUpdates;
        public long LooseLooks;
        public long CoverSeeks;
        public double ShelterBackMetres;
        public double LookCostMs;
        public double SteerCostMs;
        public long Ticks;
        public int Errors;

        public void AddLook(bool on, bool inContact, bool covered, bool somebodyBehind, bool alone)
        {
            int i = on ? 1 : 0;
            _looks[i]++;
            if (!inContact) return;
            _contact[i]++;
            if (covered) _covered[i]++;
            if (!somebodyBehind) _nobodyBehind[i]++;
            if (alone) _alone[i]++;
        }

        public void AddFear(double discipline, double fear, double pull, bool afraid)
        {
            int b = FearMath.DisciplineBand(double.IsNaN(discipline) ? 0 : discipline);
            _bandLooks[b]++;
            _bandFear[b] += fear;
            _bandPull[b] += pull;
            if (afraid) _bandAfraid[b]++;
        }

        public void AddFlockLook(FearOrder order)
        {
            int o = (int)order;
            if (o >= 0 && o < _flockLooksByOrder.Length) _flockLooksByOrder[o]++;
        }

        /// <summary>A unit's sample at a publish: its spread and, in contact, depth ÷ width.</summary>
        public void AddShape(bool on, bool inContact, double spread, double depth, double width)
        {
            if (on) _spreadOn.Add(spread);
            else _spreadOff.Add(spread);
            if (!inContact || !(width > 0.05)) return;
            if (on)
            {
                _shapeOn.Add(depth / width);
                _depthOn.Add(depth);
                _widthOn.Add(width);
            }
            else
            {
                _shapeOff.Add(depth / width);
                _depthOff.Add(depth);
                _widthOff.Add(width);
            }
        }

        /// <summary>A unit's centre moved <paramref name="back"/> metres away from the enemy (negative = toward it) in <paramref name="dt"/> s.</summary>
        public void AddDrift(bool on, int group, double back, double dt)
        {
            if (group < 0 || group >= FearMath.DriftGroups || double.IsNaN(back) || !(dt > 0)) return;
            int i = (on ? 1 : 0) * FearMath.DriftGroups + group;
            _driftBack[i] += back;
            _driftSeconds[i] += dt;
        }

        public void NoteShelterBack(double metres)
        {
            if (metres > 0) ShelterBackMetres += metres;
        }

        public void AddEnd(FearEnd why)
        {
            int i = (int)why;
            if (i >= 0 && i < _ends.Length) _ends[i]++;
        }

        public void AddAnchorEnd(double seconds, bool broken)
        {
            if (seconds >= 0) _anchorSeconds.Add(seconds);
            if (broken) AnchorsBroken++;
        }

        public void AddRefusal(CourageSprintRefusal why)
        {
            int i = (int)why;
            if (i >= 0 && i < _refusals.Length) _refusals[i]++;
        }

        public int Ended(FearEnd why) => _ends[(int)why];

        public long Looks(bool on) => _looks[on ? 1 : 0];

        public long ContactLooks(bool on) => _contact[on ? 1 : 0];

        /// <summary>The share of in-contact looks with a comrade between him and the nearest enemy.</summary>
        public double CoveredShare(bool on) => Share(_covered[on ? 1 : 0], _contact[on ? 1 : 0]);

        public double NobodyBehindShare(bool on) => Share(_nobodyBehind[on ? 1 : 0], _contact[on ? 1 : 0]);

        public double MeanSpread(bool on) => on ? _spreadOn.Mean : _spreadOff.Mean;

        public double DepthPerWidth(bool on) => on ? _shapeOn.Mean : _shapeOff.Mean;

        /// <summary>The centre's drift back, metres a minute, by drift group (NaN = no time measured).</summary>
        public double DriftPerMinute(bool on, int group)
        {
            int i = (on ? 1 : 0) * FearMath.DriftGroups + group;
            return _driftSeconds[i] > 0 ? 60 * _driftBack[i] / _driftSeconds[i] : double.NaN;
        }

        public double MeanFear(int band) => _bandLooks[band] > 0 ? _bandFear[band] / _bandLooks[band] : double.NaN;

        public double MeanPull(int band) => _bandLooks[band] > 0 ? _bandPull[band] / _bandLooks[band] : double.NaN;

        public double AfraidShare(int band) => Share(_bandAfraid[band], _bandLooks[band]);

        private static double Share(long part, long whole) => whole > 0 ? (double)part / whole : double.NaN;

        private static string N(double v, string f = "0.##") => double.IsNaN(v) ? "-" : v.ToString(f, CultureInfo.InvariantCulture);

        private static string P(double share) => double.IsNaN(share) ? "-" : (100 * share).ToString("0.0", CultureInfo.InvariantCulture) + "%";

        public void AddSummaryLines(List<string> lines, in FearRules r)
        {
            lines.Add("fear flock settings: " + r.Describe());
            if (MenLooked == 0)
            {
                lines.Add("fear flock: no AI man on foot in a formation was looked at in this mission (a tournament, an arena, a duel, a siege's attackers only, or nobody fought)");
                return;
            }
            lines.Add("fear flock shape (every unit every " + N(FearMath.PublishSeconds, "0.0") + " s): spread - its men's mean distance from its centre - ON " + N(MeanSpread(true), "0.0")
                      + " m (" + _spreadOn.Count + " samples), OFF " + N(MeanSpread(false), "0.0") + " m (" + _spreadOff.Count + "); depth ÷ width in contact ON "
                      + N(DepthPerWidth(true), "0.00") + " (depth " + N(_depthOn.Mean, "0.0") + " m, width " + N(_widthOn.Mean, "0.0") + " m, " + _shapeOn.Count + " samples), OFF "
                      + N(DepthPerWidth(false), "0.00") + " (" + N(_depthOff.Mean, "0.0") + " / " + N(_widthOff.Mean, "0.0") + " m, " + _shapeOff.Count
                      + ") - ON tighter and deeper = the mass forms, thin lines thicken");
            var drift = new StringBuilder();
            for (int g = 0; g < FearMath.DriftGroups; g++)
            {
                if (g > 0) drift.Append("; ");
                drift.Append(FearMath.DriftGroupName(g)).Append(" ON ").Append(N(DriftPerMinute(true, g), "0.0")).Append(" / OFF ").Append(N(DriftPerMinute(false, g), "0.0"));
            }
            lines.Add("fear flock drift - a unit's centre away from the enemy near it, metres a minute (+ = back): " + drift + "; the shelters stepped back "
                      + N(ShelterBackMetres, "0") + " m in all (capped at " + N(r.DriftMetresPerMinute, "0.#") + " m a minute, " + r.DriftHoldPercent + "% under a hold / a wall)");
            lines.Add("fear flock cover (in contact - an enemy within 8 m): a comrade between him and the nearest enemy ON " + P(CoveredShare(true)) + " of " + _contact[1]
                      + " looks, OFF " + P(CoveredShare(false)) + " of " + _contact[0] + "; nobody behind him ON " + P(NobodyBehindShare(true)) + ", OFF " + P(NobodyBehindShare(false))
                      + "; alone (fewer than " + FearMath.AloneMates + " mates) ON " + P(Share(_alone[1], _contact[1])) + ", OFF " + P(Share(_alone[0], _contact[0])));
            var bands = new StringBuilder();
            for (int b = 0; b < FearMath.DisciplineBands; b++)
            {
                if (b > 0) bands.Append("; ");
                bands.Append(FearMath.DisciplineBandName(b)).Append(": ").Append(_bandLooks[b]).Append(" looks, fear ").Append(N(MeanFear(b), "0.00"))
                    .Append(", pull ").Append(N(MeanPull(b), "0.00")).Append(", afraid ").Append(P(AfraidShare(b)));
            }
            lines.Add("fear flock by discipline: " + bands);
            var ends = new StringBuilder();
            for (int i = 0; i < _ends.Length; i++)
            {
                if (_ends[i] == 0) continue;
                ends.Append(ends.Length > 0 ? ", " : string.Empty).Append(FearMath.EndName((FearEnd)i)).Append(' ').Append(_ends[i]);
            }
            var orders = new StringBuilder();
            for (int o = 0; o < _flockLooksByOrder.Length; o++)
            {
                if (_flockLooksByOrder[o] == 0) continue;
                orders.Append(orders.Length > 0 ? ", " : string.Empty).Append(FearMath.OrderName((FearOrder)o)).Append(' ').Append(_flockLooksByOrder[o]);
            }
            var refusals = new StringBuilder();
            for (int i = 0; i < _refusals.Length; i++)
            {
                if (_refusals[i] == 0) continue;
                refusals.Append(refusals.Length > 0 ? ", " : string.Empty).Append(CourageMath.RefusalName((CourageSprintRefusal)i)).Append(' ').Append(_refusals[i]);
            }
            lines.Add("fear flock spells: " + Spells + " by " + SpellMen + " of " + MenLooked + " men looked; ends " + (ends.Length > 0 ? ends.ToString() : "none")
                      + "; flocking looks by order " + (orders.Length > 0 ? orders.ToString() : "none") + "; ANCHORED behind a fresh comrade " + Anchors + " times (held "
                      + N(_anchorSeconds.Mean, "0.0") + " s mean, " + AnchorsBroken + " let go early - the comrade tired); stepped in behind a comrade " + CoverSeeks
                      + " looks; sprints back " + SprintsStarted + " of " + SprintsAsked + " asked (alone " + SprintsAlone + ", by chance " + SprintsByChance + "; refused "
                      + (refusals.Length > 0 ? refusals.ToString() : "none") + ")");
            double perTick = Ticks > 0 ? (LookCostMs + SteerCostMs) / Ticks : double.NaN;
            lines.Add("fear flock vs courage and walls: courage's walk back took the man " + YieldedLooks + " looks (it goes first); walls broken (morale below "
                      + r.WallBreakMorale + ") " + WallsBroken + " times, " + WallBreakMen + " men let go into the flock; charge men left to their AI inside the loose mass "
                      + LooseLooks + " looks; steering updates " + SteerUpdates + " walking, " + StandUpdates + " standing; cost " + N(LookCostMs, "0.0") + " ms looks + "
                      + N(SteerCostMs, "0.0") + " ms steering (" + N(perTick, "0.000") + " ms a tick); errors " + Errors);
        }
    }
}

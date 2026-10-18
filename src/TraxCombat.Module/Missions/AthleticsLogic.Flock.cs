using System;
using System.Collections.Generic;
using System.Diagnostics;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 55 - CAVALRY FLOCKING (Anton, 2026-10-18; AI_NOTES "Step 55", DESIGN §2 "Cavalry flocking"). Every
    /// <see cref="FlockMath.CensusSeconds"/> one CENSUS over every tracked AI rider whose formation is on a charge: his kind (read once), his flock
    /// (team × formation × kind), his position and velocity, his EAGERNESS (his bar, halved when courage says he is alone among enemies). Eager
    /// riders are FREE - the game's own dance. Below the free line a rider FLOCKS: his group = the flocking riders of his kind within the gather
    /// radius (a hash grid); he rides (Agent.SetScriptedPosition - vanilla's way of riding an AI horse) to the group's centre led ahead along a
    /// heading that the group's rested riders pull toward the formation's goal and its tired ones toward the unit's centre, in his own sunflower
    /// slot; when the group has the size its MEAN eagerness wants and its most eager rider (the leader) is ready, the group FLIPS - all of it
    /// released at once, a wave that charges as the game's own riders for CavalryFlockWaveSeconds, then each rider decides again. The census runs
    /// ON and OFF (OFF = only the measures). No allocation per census.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly CavalryFlockStats _flockStats = new CavalryFlockStats();
        private readonly FlockTable _flockTable = new FlockTable();
        private readonly FlockGrid _flockGrid = new FlockGrid();
        private readonly List<TrackedAgent> _flockRiders = new List<TrackedAgent>(128);
        private readonly int[] _flockIds = new int[256];
        private readonly int[] _flockersOf = new int[FlockMath.Keys];
        private readonly double[] _spreadSum = new double[FlockMath.Keys];
        private readonly int[] _spreadN = new int[FlockMath.Keys];
        private string _seenFlockText = string.Empty;
        private double _flockLastCensus = double.NaN;
        private double _flockFirstCensus = double.NaN;
        private int _flockingNow;
        private int _inWaveNow;
        private bool _firstFlockLogged;
        private bool _firstFlipLogged;
        private bool _flockClosed;

        /// <summary>This mission's flocking numbers (the offline smoke reads them).</summary>
        internal CavalryFlockStats FlockStats => _flockStats;

        /// <summary>The engine side of flocking (the offline smoke swaps in a stand-in).</summary>
        internal IFlockBody FlockBody { get; set; } = FlockBodyDefault.Current;

        /// <summary>Riders flocking now.</summary>
        internal int FlockingNow => _flockingNow;

        private static string FlockText(TraxSettings s) => FlockRules.From(s).Describe();

        private void StartFlock()
        {
            _seenFlockText = FlockText(TraxSettings.Shared);
            TraxLog.Info("cavalry", "mission start: " + _seenFlockText + " - read live; a census of every AI rider every " + F1(FlockMath.CensusSeconds)
                + " s; lever: " + FlockBody.Lever + "; the [summary] \"cavalry\" lines compare lone passes, time between passes and spread with flocking ON and OFF");
        }

        /// <summary>A flocking setting changed (from ApplySettingsChange): one line; applied at the next census.</summary>
        private void NoteFlockSettings(TraxSettings s)
        {
            string now = FlockText(s);
            if (now == _seenFlockText) return;
            _seenFlockText = now;
            TraxLog.Info("cavalry", "settings changed mid-mission at " + Sec(SafeNow()) + " s: " + now + " - at the next census (within " + F1(FlockMath.CensusSeconds) + " s)");
        }

        // ------------------------------------------------------------------ the tick

        /// <summary>Every tick: switched off → every flocking rider let go at once (the scripted walk off, the game's weights back); then the census
        /// every <see cref="FlockMath.CensusSeconds"/> (ON and OFF).</summary>
        internal void TickFlock(double now)
        {
            var fr = FlockRules.From(TraxSettings.Shared);
            if (!fr.Enabled && (_flockingNow > 0 || _inWaveNow > 0)) EndAllFlocking(FlockEnd.SwitchedOff, now, native: true, fr.OffBecause);
            var body = FlockBody;
            if (!body.MissionAllows()) return;
            if (!double.IsNaN(_flockLastCensus) && now >= _flockLastCensus && now < _flockLastCensus + FlockMath.CensusSeconds - 1e-9) return;
            double dt = double.IsNaN(_flockLastCensus) || now < _flockLastCensus ? FlockMath.CensusSeconds : Math.Min(now - _flockLastCensus, 2 * FlockMath.CensusSeconds);
            _flockLastCensus = now;
            if (double.IsNaN(_flockFirstCensus)) _flockFirstCensus = now;
            long start = Stopwatch.GetTimestamp();
            Census(now, dt, in fr);
            _flockStats.AddCost((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
        }

        private void Census(double now, double dt, in FlockRules fr)
        {
            var body = FlockBody;
            _flockTable.Begin();
            for (int i = 0; i < _flockRiders.Count; i++)
            {
                var r0 = _flockRiders[i].Flock;
                r0.Eligible = false;
                r0.ListIndex = -1;
            }
            _flockRiders.Clear();
            bool on = fr.Enabled;

            // ---- pass 1: who rides, where, his flock
            for (int i = 0; i < _count; i++)
            {
                var st = _dense[i];
                if (st.Removed) continue;
                var rf = st.Flock;
                rf.Done = false;
                try
                {
                    if (!body.IsActive(st) || !body.IsMounted(st))
                    {
                        if (rf.State != RiderFlockState.Free) EndFlocking(st, FlockEnd.Excluded, now, native: true, wave: 0);
                        continue;
                    }
                    var skip = IsPlayer(st) ? FlockSkip.NotAi : body.Gate(st, rf.Flocking);
                    if (skip == FlockSkip.Count)
                    {
                        if (!rf.KindKnown)
                        {
                            bool ranged = false, thrown = false;
                            try
                            {
                                body.ReadKind(st, out ranged, out thrown);
                            }
                            catch (Exception e)
                            {
                                Failed("cavalry.kind", e); // a melee rider then
                            }
                            rf.Kind = FlockMath.KindOf(ranged, thrown);
                            rf.KindKnown = true;
                        }
                        if (!body.Key(st, out int team, out int formation) || FlockMath.Key(team, formation, rf.Kind) < 0) skip = FlockSkip.NoFormation;
                        else rf.Key = FlockMath.Key(team, formation, rf.Kind);
                    }
                    if (skip != FlockSkip.Count)
                    {
                        _flockStats.AddSkip(skip);
                        if (rf.State != RiderFlockState.Free) EndFlocking(st, FlockEnd.Excluded, now, native: true, wave: 0);
                        continue;
                    }
                    body.Position(st, out double x, out double y);
                    rf.See(x, y, now);
                    rf.Eligible = true;
                    rf.ListIndex = _flockRiders.Count;
                    _flockRiders.Add(st);
                    if (!rf.Seen)
                    {
                        rf.Seen = true;
                        _flockStats.AddRider(rf.Kind);
                    }
                    _flockTable.Add(rf.Key, x, y, rf.Flocking);
                }
                catch (Exception e)
                {
                    Failed("cavalry.census", e);
                    SafeEndFlocking(st, now);
                }
            }
            _flockTable.Finish();
            _flockStats.AddCensus(on, _flockRiders.Count, dt);
            NoteSpread(on);
            if (!on) return;

            // ---- pass 2: each rider's eagerness → free / flocking / the wave
            var cr = CourageRules.From(TraxSettings.Shared);
            bool impactLive = fr.TimidOn && ImpactLive();
            for (int i = 0; i < _flockTable.TouchedCount; i++) _flockersOf[_flockTable.TouchedKey(i)] = 0;
            for (int i = 0; i < _flockRiders.Count; i++)
            {
                var st = _flockRiders[i];
                try
                {
                    DecideRider(st, now, in fr, in cr, impactLive);
                    var rf = st.Flock;
                    _flockStats.AddState(rf.State, dt);
                    if (rf.Flocking) _flockersOf[rf.Key]++;
                }
                catch (Exception e)
                {
                    Failed("cavalry.decide", e);
                    SafeEndFlocking(st, now);
                }
            }

            // ---- pass 3: the groups - steer, or FLIP
            _flockGrid.Clear(fr.GatherRadiusMetres);
            for (int i = 0; i < _flockRiders.Count; i++)
            {
                var rf = _flockRiders[i].Flock;
                if (rf.Flocking) _flockGrid.Add(i, rf.Key, rf.X, rf.Y);
            }
            if (_flockGrid.Count == 0) return;
            for (int i = 0; i < _flockRiders.Count; i++)
            {
                var st = _flockRiders[i];
                var rf = st.Flock;
                if (!rf.Flocking || rf.Done) continue;
                try
                {
                    GroupLook(st, now, in fr);
                }
                catch (Exception e)
                {
                    Failed("cavalry.group", e);
                    SafeEndFlocking(st, now);
                }
            }
        }

        /// <summary>The spread: each flock's riders' mean distance from its centre (flocks of 2+), ON and OFF.</summary>
        private void NoteSpread(bool on)
        {
            for (int i = 0; i < _flockTable.TouchedCount; i++)
            {
                int k = _flockTable.TouchedKey(i);
                _spreadSum[k] = 0;
                _spreadN[k] = 0;
            }
            for (int i = 0; i < _flockRiders.Count; i++)
            {
                var rf = _flockRiders[i].Flock;
                double d = _flockTable.DistanceFromCentre(rf.Key, rf.X, rf.Y);
                if (double.IsNaN(d)) continue;
                _spreadSum[rf.Key] += d;
                _spreadN[rf.Key]++;
            }
            for (int i = 0; i < _flockTable.TouchedCount; i++)
            {
                int k = _flockTable.TouchedKey(i);
                if (_spreadN[k] >= 2) _flockStats.AddSpread(on, _spreadSum[k] / _spreadN[k]);
            }
        }

        private void DecideRider(TrackedAgent st, double now, in FlockRules fr, in CourageRules cr, bool impactLive)
        {
            var rf = st.Flock;
            if (rf.WaveOver(now) && _inWaveNow > 0) _inWaveNow--;
            if (rf.State == RiderFlockState.Wave) return;
            if (!fr.TimidOn) rf.Timid = false;
            else if (now >= rf.NextTimidLook) rf.Timid = RiderTimid(st, now, in fr, in cr, impactLive);
            double e = FlockMath.Eagerness(in fr, st.Fraction, rf.Timid);
            rf.Eagerness = e;
            if (rf.Flocking)
            {
                if (FlockMath.IsFree(in fr, e, flocking: true)) EndFlocking(st, FlockEnd.Rested, now, native: true, wave: 0);
                else if (FlockMath.GaveUp(in fr, rf.Since, now)) EndFlocking(st, FlockEnd.GaveUp, now, native: true, wave: fr.WaveSeconds);
                else if (fr.TightenWeights) TightenFlock(st, rf);
                else if (rf.WeightsSet) RestoreFlock(st, rf);
                return;
            }
            if (FlockMath.IsFree(in fr, e, flocking: false)) return;
            bool byTimid = rf.Timid && FlockMath.IsFree(in fr, FlockMath.Eagerness(in fr, st.Fraction, false), false);
            BeginFlocking(st, rf, byTimid ? FlockReason.Timid : FlockReason.Tired, now, in fr);
        }

        /// <summary>Courage's odds for a rider (step 49's toughness sums within CavalryFlockTimidRadiusMetres; one proximity query, at most every
        /// <see cref="FlockMath.TimidLookSeconds"/>): an enemy near and the odds below what he needs = timid.</summary>
        private bool RiderTimid(TrackedAgent st, double now, in FlockRules fr, in CourageRules cr, bool impactLive)
        {
            var rf = st.Flock;
            rf.NextTimidLook = now + FlockMath.TimidLookSeconds;
            _flockStats.TimidLooks++;
            var cb = CourageBody;
            double allies = CourageToughness(st, in cr), enemies = 0;
            int foes = 0;
            float radius = (float)fr.TimidRadiusMetres;
            cb.Scan(st, radius, _courageScan);
            if (_courageScan.Truncated) _flockStats.ScanTruncated++;
            for (int i = 0; i < _courageScan.Count; i++)
            {
                var o = _courageScan.Men[i];
                if (o == null || ReferenceEquals(o, st)) continue;
                double w = CourageMath.Weight(_courageScan.Dist[i], radius);
                if (!(w > 0)) continue;
                double t = CourageToughness(o, in cr);
                if (_courageScan.Enemy[i])
                {
                    enemies += w * CourageMath.EnemyToughness(in cr, t, o.CourageHeavy);
                    foes++;
                }
                else allies += w * t;
            }
            _courageScan.Clear();
            if (foes == 0)
            {
                rf.Odds = double.NaN;
                return false;
            }
            double shock = CourageMath.ShockShare(in cr, ShockEvents(st, now, in cr, impactLive));
            rf.Odds = CourageMath.Odds(allies, enemies);
            rf.Required = CourageMath.Required(in cr, st.Fraction, cb.Morale(st), 0, st.IsHero, shock, out _);
            bool timid = CourageMath.Timid(rf.Odds, rf.Required);
            if (timid) _flockStats.TimidFound++;
            return timid;
        }

        private void BeginFlocking(TrackedAgent st, RiderFlock rf, FlockReason reason, double now, in FlockRules fr)
        {
            bool can;
            try
            {
                can = FlockBody.CanStart(st);
            }
            catch (Exception e)
            {
                Failed("cavalry.can-start", e);
                can = false;
            }
            if (!can)
            {
                _flockStats.CannotStart++;
                return;
            }
            rf.Begin(reason, now, _flockTable.TakeSlot(rf.Key));
            _flockingNow++;
            _flockStats.AddStart(reason, rf.Kind);
            if (fr.TightenWeights) TightenFlock(st, rf);
            if (!_firstFlockLogged)
            {
                _firstFlockLogged = true;
                TraxLog.Info("cavalry", "first rider flocking this battle: " + Name(st) + " (" + FlockMath.KindName(rf.Kind) + ") at " + Sec(now) + " s - "
                    + FlockMath.ReasonName(reason) + ": eagerness " + F0(100 * rf.Eagerness) + "% (bar " + F0(100 * st.Fraction) + "%" + (rf.Timid ? ", timid: odds " + F2(rf.Odds)
                    + " < " + F2(rf.Required) : string.Empty) + ") below the free line " + fr.FreeEagerPercent + "%; his flock has " + _flockTable.Riders[rf.Key]
                    + " riders of his kind; slot " + rf.Slot);
            }
            else if (TraxLog.VerboseWants("cavalry"))
            {
                TraxLog.Verbose("cavalry", Name(st) + " flocks at " + Sec(now) + " s (" + FlockMath.ReasonName(reason) + ", eagerness " + F0(100 * rf.Eagerness) + "%)", "cavalry");
            }
        }

        /// <summary>His group: the flocking riders of his flock within the gather radius (him included) - their centre, speed, mean eagerness, the
        /// leader; the heading blend; the FLIP or the ride.</summary>
        private void GroupLook(TrackedAgent st, double now, in FlockRules fr)
        {
            var rf = st.Flock;
            int key = rf.Key;
            int n = _flockGrid.Query(key, rf.X, rf.Y, fr.GatherRadiusMetres, _flockIds);
            if (n == 0)
            {
                _flockIds[0] = rf.ListIndex;
                n = 1;
            }
            double sx = 0, sy = 0, svx = 0, svy = 0, se = 0, ready = 0, tired = 0, leaderE = -1;
            int count = 0;
            for (int i = 0; i < n; i++)
            {
                var o = _flockRiders[_flockIds[i]].Flock;
                if (!o.Flocking) continue;
                count++;
                sx += o.X;
                sy += o.Y;
                svx += o.VelX;
                svy += o.VelY;
                se += o.Eagerness;
                double r = FlockMath.Readiness(in fr, o.Eagerness);
                ready += r;
                tired += 1 - r;
                if (o.Eagerness > leaderE) leaderE = o.Eagerness;
            }
            if (count == 0) return;
            double cx = sx / count, cy = sy / count, vx = svx / count, vy = svy / count, meanE = se / count;
            double gx = 0, gy = 0, mx = 0, my = 0;
            if (Goal(st, key, out double goalX, out double goalY)) FlockMath.Unit(cx, cy, goalX, goalY, out gx, out gy);
            else _flockStats.GoalMissing++;
            if (Mass(st, key, out double massX, out double massY)) FlockMath.Unit(cx, cy, massX, massY, out mx, out my);
            if (!FlockMath.Heading(ready, tired, gx, gy, mx, my, out double hx, out double hy)) FlockMath.Unit(0, 0, vx, vy, out hx, out hy);
            int needed = FlockMath.GroupNeeded(in fr, meanE, _flockTable.Riders[key]);
            int flockers = _flockersOf[key];
            if (flockers > 0 && needed > flockers) needed = flockers; // never more than his kind has flocking
            if (FlockMath.Flips(count, needed, FlockMath.Readiness(in fr, leaderE), now - rf.Since))
            {
                Flip(st, n, now, in fr, meanE, leaderE, needed, FlockMath.GoalVote(ready, tired), count);
                return;
            }
            FlockMath.SteerPoint(cx, cy, hx, hy, Math.Sqrt(vx * vx + vy * vy), fr.LeadSeconds, rf.Slot, fr.SeparationMetres, out double tx, out double ty);
            Issue(st, rf, tx, ty, cx, cy, now);
        }

        private bool Goal(TrackedAgent st, int key, out double x, out double y)
        {
            var t = _flockTable;
            if (t.GoalRead[key] == 0)
            {
                bool ok = false;
                double gx = double.NaN, gy = double.NaN;
                try
                {
                    ok = FlockBody.Goal(st, out gx, out gy);
                }
                catch (Exception e)
                {
                    Failed("cavalry.goal", e);
                }
                t.GoalRead[key] = (sbyte)(ok ? 1 : -1);
                t.GoalX[key] = gx;
                t.GoalY[key] = gy;
            }
            x = t.GoalX[key];
            y = t.GoalY[key];
            return t.GoalRead[key] > 0;
        }

        private bool Mass(TrackedAgent st, int key, out double x, out double y)
        {
            var t = _flockTable;
            if (t.MassRead[key] == 0)
            {
                bool ok = false;
                double mx = double.NaN, my = double.NaN;
                try
                {
                    ok = FlockBody.FormationCentre(st, out mx, out my);
                }
                catch (Exception e)
                {
                    Failed("cavalry.centre", e);
                }
                t.MassRead[key] = (sbyte)(ok ? 1 : -1);
                t.MassX[key] = mx;
                t.MassY[key] = my;
            }
            x = t.MassX[key];
            y = t.MassY[key];
            return t.MassRead[key] > 0;
        }

        /// <summary>The FLIP: every flocking rider of the group released at once - a wave.</summary>
        private void Flip(TrackedAgent st, int n, double now, in FlockRules fr, double meanE, double leaderE, int needed, double vote, int count)
        {
            int size = 0;
            for (int i = 0; i < n; i++)
            {
                var o = _flockRiders[_flockIds[i]];
                var of = o.Flock;
                if (!of.Flocking || of.Done) continue;
                EndFlocking(o, FlockEnd.Wave, now, native: true, wave: fr.WaveSeconds);
                of.Done = true;
                size++;
            }
            _flockStats.AddFlip(size, meanE, leaderE);
            if (!_firstFlipLogged)
            {
                _firstFlipLogged = true;
                TraxLog.Info("cavalry", "first FLIP this battle at " + Sec(now) + " s: " + size + " " + FlockMath.KindName(st.Flock.Kind) + " go in together - " + count
                    + " flocking within " + F0(fr.GatherRadiusMetres) + " m, their mean eagerness " + F0(100 * meanE) + "% wants " + needed + "; the leader's eagerness "
                    + F0(100 * leaderE) + "% (ready " + F0(100 * FlockMath.Readiness(in fr, leaderE)) + "%), the goal's share of the pull " + F0(100 * vote) + "%; a wave for "
                    + F1(fr.WaveSeconds) + " s (riders keep the game's own aggression - step 43's fever is for men on foot)");
            }
            else if (TraxLog.VerboseWants("cavalry"))
            {
                TraxLog.Verbose("cavalry", "flip at " + Sec(now) + " s: " + size + " " + FlockMath.KindName(st.Flock.Kind) + ", mean eagerness " + F0(100 * meanE) + "%, leader "
                    + F0(100 * leaderE) + "%", "cavalry");
            }
        }

        /// <summary>The scripted point, sent when it moved (or aged); no ground there = his group's centre instead, else nothing.</summary>
        private void Issue(TrackedAgent st, RiderFlock rf, double tx, double ty, double cx, double cy, double now)
        {
            if (!FlockMath.Reissue(rf.IssuedAt, rf.IssuedX, rf.IssuedY, tx, ty, now)) return;
            try
            {
                bool ok = FlockBody.MoveTo(st, tx, ty);
                if (!ok)
                {
                    tx = cx;
                    ty = cy;
                    ok = FlockBody.MoveTo(st, tx, ty);
                }
                if (!ok)
                {
                    _flockStats.MovesNoGround++;
                    return;
                }
                if (rf.Moving) _flockStats.MovesReissued++;
                _flockStats.MovesIssued++;
                rf.Moving = true;
                rf.IssuedX = tx;
                rf.IssuedY = ty;
                rf.IssuedAt = now;
            }
            catch (Exception e)
            {
                _flockStats.LeverErrors++;
                Failed("cavalry.move", e);
            }
        }

        // ------------------------------------------------------------------ the weights

        private void TightenFlock(TrackedAgent st, RiderFlock rf)
        {
            try
            {
                FlockBody.Tighten(st);
                if (rf.WeightsSet) _flockStats.WeightsReapplied++;
                else _flockStats.WeightsSet++;
                rf.WeightsSet = true;
            }
            catch (Exception e)
            {
                _flockStats.LeverErrors++;
                Failed("cavalry.weights", e);
            }
        }

        private void RestoreFlock(TrackedAgent st, RiderFlock rf)
        {
            rf.WeightsSet = false;
            try
            {
                FlockBody.Restore(st);
                _flockStats.WeightsRestored++;
            }
            catch (Exception e)
            {
                _flockStats.LeverErrors++;
                Failed("cavalry.restore", e);
            }
        }

        // ------------------------------------------------------------------ the end

        /// <summary>His flocking spell ends (any path): the scripted walk off and the game's weights back (<paramref name="native"/>), counted; a
        /// wave window when <paramref name="wave"/> > 0. A rider in a wave who is excluded / switched off is simply free again.</summary>
        private void EndFlocking(TrackedAgent st, FlockEnd why, double now, bool native, double wave)
        {
            var rf = st.Flock;
            if (!rf.Flocking)
            {
                if (rf.State == RiderFlockState.Wave && why != FlockEnd.Wave)
                {
                    rf.Reset();
                    if (_inWaveNow > 0) _inWaveNow--;
                }
                return;
            }
            double len = rf.End(now, wave);
            if (_flockingNow > 0) _flockingNow--;
            if (rf.State == RiderFlockState.Wave) _inWaveNow++;
            if (rf.Moving)
            {
                rf.Moving = false;
                if (native && !st.Removed)
                {
                    try
                    {
                        FlockBody.Release(st);
                        _flockStats.Releases++;
                    }
                    catch (Exception e)
                    {
                        _flockStats.LeverErrors++;
                        Failed("cavalry.release", e);
                    }
                }
            }
            if (rf.WeightsSet)
            {
                if (native && !st.Removed) RestoreFlock(st, rf);
                else rf.WeightsSet = false;
            }
            _flockStats.AddEnd(why, len);
            if (TraxLog.VerboseWants("cavalry") && why != FlockEnd.Wave)
                TraxLog.Verbose("cavalry", Name(st) + " " + FlockMath.EndName(why) + " at " + Sec(now) + " s after " + F1(len) + " s", "cavalry");
        }

        private void SafeEndFlocking(TrackedAgent st, double now)
        {
            try
            {
                if (st.Flock.State != RiderFlockState.Free) EndFlocking(st, FlockEnd.Error, now, native: true, wave: 0);
            }
            catch
            {
                var rf = st.Flock;
                if (rf.Flocking && _flockingNow > 0) _flockingNow--;
                rf.Reset();
                rf.Moving = false;
                rf.WeightsSet = false;
            }
        }

        private void EndAllFlocking(FlockEnd why, double now, bool native, string? because)
        {
            int ended = 0;
            for (int i = 0; i < _count; i++)
            {
                var st = _dense[i];
                if (st.Flock.State == RiderFlockState.Free) continue;
                try
                {
                    if (st.Flock.Flocking) ended++;
                    EndFlocking(st, why, now, native, wave: 0);
                }
                catch (Exception e)
                {
                    Failed("cavalry.end-all", e);
                    SafeEndFlocking(st, now);
                }
            }
            _flockingNow = 0;
            _inWaveNow = 0;
            if (why == FlockEnd.SwitchedOff && ended > 0)
                TraxLog.Info("cavalry", (because == "ModEnabled" ? "the whole mod (ModEnabled)" : because ?? "cavalry flocking") + " switched OFF mid-mission at " + Sec(now) + " s: "
                    + ended + " flocking riders let go at once - the game's own riders; the census keeps measuring");
        }

        /// <summary>He left the field: his spell ends without an engine call.</summary>
        private void FlockLeftField(TrackedAgent st)
        {
            try
            {
                if (st.Flock.State != RiderFlockState.Free) EndFlocking(st, FlockEnd.LeftField, SafeNow(), native: false, wave: 0);
                st.Flock.Eligible = false;
            }
            catch (Exception e)
            {
                Failed("cavalry.left", e);
            }
        }

        /// <summary>An AI rider's attack (a melee release, a shot): a new PASS after <see cref="FlockMath.PassGapSeconds"/> of none - lone or not
        /// (his kind within 30 m at the census), the time since his last pass by his eagerness. ON and OFF.</summary>
        internal void NoteFlockAttack(TrackedAgent st, bool mounted, double now)
        {
            try
            {
                if (!mounted || IsPlayer(st)) return;
                var rf = st.Flock;
                if (!rf.Eligible || rf.Key < 0) return;
                bool pass = double.IsNaN(rf.LastAttack) || now < rf.LastAttack || now - rf.LastAttack >= FlockMath.PassGapSeconds;
                rf.LastAttack = now;
                if (!pass) return;
                var fr = FlockRules.From(TraxSettings.Shared);
                int mates = 0;
                double r2 = FlockMath.LonePassMetres * FlockMath.LonePassMetres;
                for (int i = 0; i < _flockRiders.Count; i++)
                {
                    var o = _flockRiders[i];
                    if (ReferenceEquals(o, st)) continue;
                    var of = o.Flock;
                    if (of.Key != rf.Key) continue;
                    double dx = of.X - rf.X, dy = of.Y - rf.Y;
                    if (dx * dx + dy * dy <= r2) mates++;
                }
                double gap = double.IsNaN(rf.LastPass) || now < rf.LastPass ? double.NaN : now - rf.LastPass;
                rf.LastPass = now;
                _flockStats.AddPass(fr.Enabled, FlockMath.LonePass(mates), gap, FlockMath.Eagerness(in fr, st.Fraction, fr.Enabled && rf.Timid));
            }
            catch (Exception e)
            {
                Failed("cavalry.attack", e);
            }
        }

        /// <summary>The battle's end: every flocking rider let go (the scripted walk off while the agents live) before the summary counts.</summary>
        private void CloseFlock(bool agentsAlive)
        {
            if (_flockClosed) return;
            _flockClosed = true;
            if (_flockingNow > 0 || _inWaveNow > 0) EndAllFlocking(FlockEnd.MissionEnd, SafeNow(), agentsAlive, null);
        }

        internal void WriteFlockSummary()
        {
            var lines = new List<string>(8);
            double minutes = double.IsNaN(_flockFirstCensus) || double.IsNaN(_flockLastCensus) ? 0 : (_flockLastCensus - _flockFirstCensus) / 60;
            _flockStats.AddSummaryLines(lines, FlockRules.From(TraxSettings.Shared), FlockBody.Lever, minutes);
            foreach (var line in lines) TraxLog.Info("summary", line);
        }
    }
}

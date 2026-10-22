using System;
using System.Collections.Generic;
using System.Diagnostics;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 78c - EACH MAN RESTS ON HIS OWN (Toni 2026-10-22, after playing 78b: "dont script for all of them to walk away or to push into each other,
    /// only per men rule, if tired - step back and wait to recover ... if guy next to you rushes, there is a chance to make you go in too"; AI_NOTES
    /// "Step 78c", DESIGN decision 74). It rides the rhythm's look (courage's, about every 0.5 s - a held wall man through FearSkipped) and the rhythm's
    /// input wish: an AI man on foot at the fight whose bar falls below his own TIRED mark backs out of reach facing the enemy (a walk, at most
    /// RestMaxBackMetres from where he began), guard up, then is held there (the clamp - the game's charge never drags him in) until his own READY mark;
    /// a mate within reach going back in may take him along (contagion, the only social rule). A man in a shield wall rests IN his slot: no walk, the
    /// leash 0, only his guard. No unit-wide rule, no shared clock. The master switch FIRST, then Athletics, then RestEnabled - all read live.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly RestStats _restStats = new RestStats();
        private readonly List<TrackedAgent> _restGoIn = new List<TrackedAgent>(32);
        private RestRules _restRules = new RestRules(false, false, false);
        private string _seenRestText = string.Empty;
        private bool _restGoInBusy, _firstRest, _firstRestWall, _firstRestWalk, _firstRestIn, _firstContagion, _firstTurned, _firstWallOut, _firstFarBehind,
            _firstRestOff;

        internal RestStats RestStats => _restStats;

        /// <summary>The rest's dice - the marks and the contagion rolls (the offline smoke seeds it).</summary>
        internal IRandomSource RestDice { get; set; } = ThreadSafeRandom.Shared;

        private void StartRest()
        {
            _restRules = RestRules.From(TraxSettings.Shared);
            _seenRestText = _restRules.Describe();
            TraxLog.Info("rhythm", "mission start: " + _seenRestText + " - read live; the [rhythm] lines log the first rest, walk back, wall rest, going back in and "
                + "contagion; the two [summary] \"rest (step 78c)\" lines (rests, how far, facing, the wall men's slots, the natural gap by side)");
        }

        private void NoteRestSettings(TraxSettings s)
        {
            string now = RestRules.From(s).Describe();
            if (now == _seenRestText) return;
            _seenRestText = now;
            TraxLog.Info("rhythm", "rest settings changed mid-mission at " + Sec(SafeNow()) + " s: " + now + " - at every man's next look");
        }

        /// <summary>Every tick: the rest off (or Athletics, or the master switch) ends every rest at once.</summary>
        private void TickRestSwitch(double now)
        {
            bool was = _restRules.On;
            _restRules = RestRules.From(TraxSettings.Shared);
            if (was && !_restRules.On)
                TraxLog.Info("rhythm", "the rest switched OFF at " + Sec(now) + " s (" + _restRules.OffBecause + "): " + EndAllRests(RestIn.SwitchedOff, now, native: true)
                    + " rests ended at once");
        }

        /// <summary>A man resting in a shield wall's slot now (the wall's leash is 0 for him - he never steps out of it).</summary>
        private static bool RestingInWall(TrackedAgent st) => st.Rhythm.Resting && st.Rhythm.RestWall;

        // ------------------------------------------------------------------ the look (from RhythmLookCore)

        private void RestLook(TrackedAgent st, RhythmMind m, double now, int key, double px, double py, double enemyDist, double ex, double ey, bool held, bool pressing,
            BraceOrderKind kind, double bar)
        {
            long t0 = Stopwatch.GetTimestamp();
            var rr = _restRules;
            try
            {
                _restStats.Looks++;
                m.LastX = px;
                m.LastY = py;
                if (m.InSwap || IsPlayer(st)) return;
                bool atFight = held || pressing || (!double.IsNaN(enemyDist) && enemyDist <= rr.ReachMetres);
                if (!rr.On)
                {
                    if (m.Resting) EndRest(st, RestIn.SwitchedOff, now, native: true);
                    if (atFight && !held && bar < 0.01 * (rr.TiredMinPercent + rr.TiredMaxPercent) / 2)
                    {
                        _restStats.WouldRestOff++;
                        if (!_firstRestOff)
                        {
                            _firstRestOff = true;
                            TraxLog.Info("rhythm", "first tired man at the fight with the rest OFF at " + Sec(now) + " s: " + Name(st) + " at " + RestStats.Pct(bar)
                                + " - counted only (" + rr.OffBecause + ")");
                        }
                    }
                    return;
                }
                if (double.IsNaN(m.TiredMark))
                {
                    m.TiredMark = RestMath.TiredMark(in rr, RestDice.NextDouble()); // once per rest - rolled afresh after each
                    _restStats.AddTired(m.TiredMark);
                }
                if (!double.IsNaN(enemyDist))
                {
                    m.EnemyX = ex;
                    m.EnemyY = ey;
                    m.EnemyDist = enemyDist;
                }
                else m.EnemyX = m.EnemyY = m.EnemyDist = double.NaN;
                double cap = AthleticsMath.UsableFraction(AthleticsRules.From(TraxSettings.Shared), st);
                if (!m.Resting)
                {
                    if (!(bar < m.TiredMark) || !atFight) return;
                    var g = BraceMath.Group(kind == BraceOrderKind.Count ? BraceOrderKind.Stop : kind);
                    double ready = RestMath.ReadyMark(in rr, g, RestDice.NextDouble());
                    double capped = RestMath.CappedReady(ready, cap);
                    if (!RestMath.Starts(bar, m.TiredMark, capped, atFight))
                    {
                        _restStats.NoBand++;
                        return;
                    }
                    _restStats.AddReady(g, ready);
                    BeginRest(st, m, now, px, py, held, ready, capped, bar, g);
                    return;
                }
                // resting: his wounds may have lowered his cap; a wall man may have been let go by the wall (then he rests in the open)
                m.ReadyCapped = RestMath.CappedReady(m.ReadyMark, cap);
                if (m.RestWall != held)
                {
                    m.RestWall = held;
                    m.RestStartX = px;
                    m.RestStartY = py;
                    RelookWall(st, now);
                }
                if (!held && !double.IsNaN(m.EnemyX)) FacingLook(st, m, px, py, now);
                double behind = _rhythmUnits.BehindFront(key, px, py, now);
                if (!double.IsNaN(behind) && behind > m.RestBehindFront) m.RestBehindFront = behind;
                if (held) WallSlotLook(st, px, py, now);
                if (RestMath.Ready(bar, m.ReadyCapped)) GoIn(st, RestIn.OwnMark, now);
            }
            catch (Exception e)
            {
                _restStats.Errors++;
                Failed("rest.look", e);
                SafeEndRhythm(st, now);
            }
            _restStats.LookCostMs += (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
        }

        private void BeginRest(TrackedAgent st, RhythmMind m, double now, double px, double py, bool held, double ready, double capped, double bar, BraceOrder g)
        {
            m.Resting = true;
            m.RestWall = held;
            m.RestSince = now;
            m.RestStartX = px;
            m.RestStartY = py;
            m.ReadyMark = ready;
            m.ReadyCapped = capped;
            m.RestBack = m.RestBehindFront = 0;
            m.RestFarCounted = false;
            if (!EnsureRhythmWish(st, now))
            {
                EndRest(st, RestIn.Error, now, native: true);
                return;
            }
            m.NextSteer = now;
            _restStats.Started++;
            if (held)
            {
                _restStats.StartedInWall++;
                RelookWall(st, now); // the leash 0 at once - he never steps out of his slot
            }
            if (!_firstRest && !held)
            {
                _firstRest = true;
                TraxLog.Info("rhythm", "first REST at " + Sec(now) + " s: " + Name(st) + " at " + RestStats.Pct(bar) + " (his tired mark " + RestStats.Pct(m.TiredMark)
                    + ", " + BraceMath.Name(g) + ") with an enemy " + F1(m.EnemyDist) + " m off - he backs out of reach (" + F1(_restRules.ReachMetres) + " m, at most "
                    + F1(_restRules.MaxBackMetres) + " m) facing the enemy, guard up, and waits for his ready mark " + RestStats.Pct(ready)
                    + (capped < ready - 1e-9 ? " (his wounds: " + RestStats.Pct(capped) + ")" : string.Empty));
            }
            if (!_firstRestWall && held)
            {
                _firstRestWall = true;
                TraxLog.Info("rhythm", "first REST IN A WALL SLOT at " + Sec(now) + " s: " + Name(st) + " at " + RestStats.Pct(bar) + " - he stays in his slot (the leash 0), "
                    + "guard only, until " + RestStats.Pct(ready) + "; the wall moves only as one");
            }
            SteerRhythmMan(st, m, now);
        }

        /// <summary>A resting man's facing against the direction to his nearest enemy - turned away = more than 90° off (should never happen: he backs
        /// FACING the enemy).</summary>
        private void FacingLook(TrackedAgent st, RhythmMind m, double px, double py, double now)
        {
            if (!RhythmBody.Facing(st, out double fx, out double fy)) return;
            double dx = m.EnemyX - px, dy = m.EnemyY - py, len = Math.Sqrt(dx * dx + dy * dy);
            if (!(len > 1e-6)) return;
            double cos = (fx * dx + fy * dy) / len;
            _restStats.FacingSamples++;
            if (cos >= 0) return;
            _restStats.TurnedAway++;
            if (_firstTurned) return;
            _firstTurned = true;
            TraxLog.Info("rhythm", "first resting man TURNED AWAY from his enemy at " + Sec(now) + " s: " + Name(st) + " - " + F1(Math.Acos(Math.Max(-1, Math.Min(1, cos))) * 180 / Math.PI)
                + "° off; it should not happen (he backs facing the enemy) - tell Claude");
        }

        /// <summary>A wall man resting in his slot: how far out of it (Toni: a man in a shield wall never leaves it).</summary>
        private void WallSlotLook(TrackedAgent st, double px, double py, double now)
        {
            if (!FearBody.Slot(st, out double sx, out double sy)) return;
            double d = Math.Sqrt((px - sx) * (px - sx) + (py - sy) * (py - sy));
            _restStats.WallLooks++;
            if (d <= RestMath.WallSlotSlackMetres) return;
            _restStats.WallOut++;
            if (d > _restStats.WallOutMax) _restStats.WallOutMax = d;
            if (_firstWallOut) return;
            _firstWallOut = true;
            TraxLog.Info("rhythm", "first resting WALL man more than " + F1(RestMath.WallSlotSlackMetres) + " m out of his slot at " + Sec(now) + " s: " + Name(st) + " "
                + F2(d) + " m - the rest never moves a wall man (his leash is 0); pushed by the press? - tell Claude");
        }

        // ------------------------------------------------------------------ the steer (from SteerRhythmMan, every 0.1 s)

        /// <summary>One resting man's steer: out of reach of his last-seen enemy (a walk straight back toward his own side - his unit's axis - facing
        /// the enemy, at most the limit from his start and behind his own front), else held back at the edge of reach (the clamp along the same line); his
        /// attacks held unless an enemy is within the defend distance. A wall man: his guard only - the wall holds him in his slot.</summary>
        private void SteerRest(TrackedAgent st, RhythmMind m, AiInputState s, double px, double py, double now)
        {
            long t0 = Stopwatch.GetTimestamp();
            var rr = _restRules;
            m.LastX = px;
            m.LastY = py;
            double fromStart = Math.Sqrt((px - m.RestStartX) * (px - m.RestStartX) + (py - m.RestStartY) * (py - m.RestStartY));
            if (fromStart > m.RestBack) m.RestBack = fromStart;
            s.RestMove = false;
            s.RestX = s.RestY = 0f;
            if (m.RestWall)
            {
                s.RestHoldAttacks = true; // in his slot: he guards, he does not push or strike forward
                _restStats.SteerCostMs += (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                return;
            }
            double ed = double.NaN, ux = 0, uy = 0;
            if (!double.IsNaN(m.EnemyX))
            {
                double dx = m.EnemyX - px, dy = m.EnemyY - py;
                ed = Math.Sqrt(dx * dx + dy * dy);
                if (ed > 1e-6)
                {
                    ux = dx / ed;
                    uy = dy / ed;
                }
                else ed = double.NaN;
            }
            s.RestHoldAttacks = RestMath.HoldsAttacks(in rr, false, ed);
            if (!double.IsNaN(ed))
            {
                // STRAIGHT BACK toward his own side - along his unit's axis to its enemy (the men's own directions - a meshed fight keeps it); away from
                // his nearest enemy only when the unit table has no axis (walking away from one enemy in a mesh can take a man sideways or through)
                var u = _rhythmUnits;
                double behind = u.BehindFront(m.Key, px, py, now);
                if (!double.IsNaN(behind))
                {
                    ux = u.UX[m.Key];
                    uy = u.UY[m.Key];
                }
                if (behind > m.RestBehindFront) m.RestBehindFront = behind;
                double back = RestMath.WalkBack(in rr, ed, fromStart, behind);
                if (back > RhythmMath.ArriveMetres * 0.5)
                {
                    WalkTo(st, m, s, px, py, px - ux * back, py - uy * back, now, rest: true);
                    if (!_firstRestWalk)
                    {
                        _firstRestWalk = true;
                        TraxLog.Info("rhythm", "first resting man WALKED BACK at " + Sec(now) + " s: " + Name(st) + " " + F1(ed) + " m from his enemy - backing " + F1(back)
                            + " m facing him (a walk), " + F1(fromStart) + " m from where his rest began");
                    }
                }
                else if (RestMath.HoldsBack(in rr, ed, behind))
                {
                    // held at the edge of reach: he STANDS (the rest's own vector, zero - over the game's charge, the press's, the swap's, courage's and the
                    // block's walks; a plain forward clamp let the charge slide him sideways through a meshed fight); farther off his own AI keeps him up with
                    // his unit, and so does it once his own front has moved on more than the limit ahead of him (he stays with his line); the forward clamp too,
                    // in case a stronger wish (the backpedal, the wall's pull, the flock) moves him
                    s.RestMove = true;
                    s.RestX = s.RestY = 0f;
                    SetClamp(st, s, ux, uy);
                    _restStats.ClampFrames++;
                }
            }
            _restStats.SteerCostMs += (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
        }

        // ------------------------------------------------------------------ the ends

        /// <summary>He goes back in (his own mark): his rest ends, then CONTAGION - every resting mate of his unit within the contagion distance rolls
        /// his own chance to go with him (and those who go may take their neighbours: a cascade through the reused queue, never recursion).</summary>
        private void GoIn(TrackedAgent st, RestIn why, double now)
        {
            EndRest(st, why, now, native: true);
            if (!_firstRestIn)
            {
                _firstRestIn = true;
                TraxLog.Info("rhythm", "first man BACK IN after a rest at " + Sec(now) + " s: " + Name(st) + " at " + RestStats.Pct(st.Fraction) + " - "
                    + RestMath.InName(why) + "; the standard AI has him again");
            }
            var rr = _restRules;
            if (rr.ContagionMetres <= 0 || rr.ContagionMaxPercent <= 0) return;
            if (_restGoInBusy)
            {
                _restGoIn.Add(st);
                return;
            }
            _restGoInBusy = true;
            try
            {
                _restGoIn.Clear();
                _restGoIn.Add(st);
                double reach2 = rr.ContagionMetres * rr.ContagionMetres;
                for (int q = 0; q < _restGoIn.Count; q++)
                {
                    var src = _restGoIn[q];
                    var sm = src.Rhythm;
                    for (int i = _rhythmMen.Count - 1; i >= 0; i--)
                    {
                        if (i >= _rhythmMen.Count) continue;
                        var o = _rhythmMen[i];
                        var om = o.Rhythm;
                        if (!om.Resting || ReferenceEquals(o, src) || om.Key != sm.Key || o.Removed) continue;
                        double dx = om.LastX - sm.LastX, dy = om.LastY - sm.LastY;
                        if (!(dx * dx + dy * dy <= reach2)) continue;
                        double p = RestMath.ContagionChance(in rr, o.Fraction, om.TiredMark, om.ReadyCapped);
                        if (p <= 0)
                        {
                            _restStats.ContagionNoChance++;
                            continue;
                        }
                        _restStats.ContagionRolls++;
                        if (RestDice.NextDouble() >= p) continue;
                        if (!_firstContagion)
                        {
                            _firstContagion = true;
                            TraxLog.Info("rhythm", "first man IN BY CONTAGION at " + Sec(now) + " s: " + Name(o) + " at " + RestStats.Pct(o.Fraction) + " (his ready mark "
                                + RestStats.Pct(om.ReadyCapped) + ", the chance " + RestStats.Pct(p) + ") went in with " + Name(src) + " " + F1(Math.Sqrt(dx * dx + dy * dy)) + " m away");
                        }
                        EndRest(o, RestIn.Contagion, now, native: true);
                        _restGoIn.Add(o);
                    }
                }
            }
            finally
            {
                _restGoInBusy = false;
                _restGoIn.Clear();
            }
        }

        /// <summary>His rest ends (counted once): how long, how far he went, behind his front; the input's rest parts off; the wish let go unless a swap's
        /// rest still holds it; a wall man looked at again (his leash back).</summary>
        private void EndRest(TrackedAgent st, RestIn why, double now, bool native)
        {
            var m = st.Rhythm;
            if (!m.Resting) return;
            _restStats.AddEnd(why, double.IsNaN(m.RestSince) ? double.NaN : now - m.RestSince, m.RestBack);
            if (m.RestBehindFront > _restStats.BehindFrontMax) _restStats.BehindFrontMax = m.RestBehindFront;
            if (m.RestBehindFront > RestMath.FarBehindFrontMetres)
            {
                _restStats.FarBehindFront++;
                if (!_firstFarBehind)
                {
                    _firstFarBehind = true;
                    TraxLog.Info("rhythm", "first resting man more than " + F1(RestMath.FarBehindFrontMetres) + " m behind his own front at " + Sec(now) + " s: " + Name(st) + " "
                        + F1(m.RestBehindFront) + " m (he backed " + F1(m.RestBack) + " m) - his whole unit was pushed back?");
                }
            }
            bool wall = m.RestWall;
            m.EndRest();
            var s = st.Input;
            if (s != null)
            {
                s.RestMove = s.RestHoldAttacks = false;
                s.RestX = s.RestY = 0f;
                s.RhythmClamp = false;
            }
            bool swapRest = now < m.RestUntil && _rhythmRules.RotationOn && !m.Held && !double.IsNaN(m.RestLine);
            if (!m.InSwap && !swapRest) ReleaseRhythmWish(st, now, native && !st.Removed);
            if (wall && native && !st.Removed) RelookWall(st, now);
        }

        private int EndAllRests(RestIn why, double now, bool native)
        {
            int n = 0;
            for (int i = _rhythmMen.Count - 1; i >= 0; i--)
            {
                if (i >= _rhythmMen.Count) continue;
                var st = _rhythmMen[i];
                if (!st.Rhythm.Resting) continue;
                try
                {
                    EndRest(st, why, now, native && !st.Removed);
                    n++;
                }
                catch (Exception e)
                {
                    Failed("rest.end-all", e);
                    st.Rhythm.EndRest();
                }
            }
            return n;
        }
    }
}

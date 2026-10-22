using System;
using System.Collections.Generic;
using System.Diagnostics;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 78 - THE BATTLE'S RHYTHM, part 1 (Toni 2026-10-22, sketch 1-4: "two packs of dogs - they don't want to get hurt but they won't back down,
    /// and they won't do reckless things"; AI_NOTES "Step 78", DESIGN decision 72). It rides courage's looks (every AI man on foot in a formation about
    /// every 0.5 s, ON and OFF - the OFF looks measure; a held wall man through the fear flock's FearSkipped note):
    /// ROTATION - a tired front man in contact and a fresher mate right behind him SWAP: the fresh man walks up beside him while the tired one holds and
    /// guards (an eighth input wish, <see cref="AiInputState.Rhythm"/>), then the game's own <c>Formation.SwitchUnitLocations</c> swaps their slots and the
    /// tired man backs into the fresh man's place facing the enemy; a held wall man's slot follows the swap's places (<see cref="RhythmWallSlot"/>), so the
    /// wall hold and stand firm agree with it. THE LULL - the unit table (<see cref="RhythmUnits"/>, every 0.5 s) pairs facing units; both fronts spent =
    /// each eases back to its lull line (a held wall's slots shifted back, everyone else walked back and clamped at it), the tired swap to the back, until a
    /// front recovers, a new charge order, a side breaks or time. The master switch FIRST, then Athletics, then each part's switch.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly RhythmUnits _rhythmUnits = new RhythmUnits();
        private readonly RhythmStats _rhythmStats = new RhythmStats();
        private readonly List<TrackedAgent> _rhythmMen = new List<TrackedAgent>(64);
        private readonly CourageScan _rhythmScan = new CourageScan();
        private readonly double[] _rhythmUnitAt = new double[FearMath.UnitCount];
        private readonly BraceOrderKind[] _rhythmKind = new BraceOrderKind[FearMath.UnitCount];
        private readonly bool[] _rhythmPlayer = new bool[FearMath.UnitCount];
        private RhythmRules _rhythmRules = new RhythmRules(false, false, false, false);
        private string _seenRhythmText = string.Empty;
        private bool _firstSwapAsked, _firstSwapBeside, _firstSwapDone, _firstSwapOff, _firstChanceOff, _firstRefusal, _firstLull, _firstLullEnd, _firstSlotShift,
            _firstLullWalk, _firstLullRotation;
        private bool _rhythmClosed;

        internal RhythmUnits RhythmUnits => _rhythmUnits;

        internal RhythmStats RhythmStats => _rhythmStats;

        /// <summary>Men with a rhythm wish now (a swap, a lull's line, a rest).</summary>
        internal int RhythmMenNow => _rhythmMen.Count;

        /// <summary>The engine side of the rhythm's own calls (the offline smoke swaps in a stand-in).</summary>
        internal IRhythmBody RhythmBody { get; set; } = RhythmBodyDefault.Current;

        private static string RhythmText(TraxSettings s) => RhythmRules.From(s).Describe();

        private void StartRhythm()
        {
            _rhythmScan.Lookup = Get;
            for (int k = 0; k < _rhythmUnitAt.Length; k++) _rhythmUnitAt[k] = double.NegativeInfinity;
            _rhythmRules = RhythmRules.From(TraxSettings.Shared);
            _seenRhythmText = RhythmText(TraxSettings.Shared);
            TraxLog.Info("rhythm", "mission start: " + _seenRhythmText + " - read live; it rides courage's looks (every " + F1(CourageMath.PollSeconds)
                + " s), the units' fronts every " + F1(RhythmMath.PublishSeconds) + " s; the [rhythm] lines log the first chance, swap, lull and wall shift; the two [summary] \"battle rhythm\" lines");
        }

        private void NoteRhythmSettings(TraxSettings s)
        {
            string now = RhythmText(s);
            if (now == _seenRhythmText) return;
            _seenRhythmText = now;
            TraxLog.Info("rhythm", "settings changed mid-mission at " + Sec(SafeNow()) + " s: " + now + " - at every man's next look");
        }

        // ------------------------------------------------------------------ the tick (from TickCourage)

        /// <summary>The switches, every tick: the rotation off → every swap ends at once; both off → every wish let go (a lull ends at the next publish).</summary>
        private void TickRhythmSwitch(double now)
        {
            var r = RhythmRules.From(TraxSettings.Shared);
            bool rotWas = _rhythmRules.RotationOn, lullWas = _rhythmRules.LullOn;
            _rhythmRules = r;
            if (rotWas && !r.RotationOn)
                TraxLog.Info("rhythm", "the rotation switched OFF at " + Sec(now) + " s (" + r.RotationOffBecause + "): " + EndAllSwaps(RotationEnd.SwitchedOff, now) + " swaps ended at once");
            if (lullWas && !r.LullOn)
                TraxLog.Info("rhythm", "the lull switched OFF at " + Sec(now) + " s (" + r.LullOffBecause + "): the walls' slots back at their next look, the lull lines let go now");
            if (!r.AnyOn && _rhythmMen.Count > 0) EndAllRhythm(RotationEnd.SwitchedOff, now, native: true);
        }

        /// <summary>The unit table published every 0.5 s - the lulls' starts and ends noted and logged.</summary>
        private void TickRhythmUnits(double now)
        {
            _rhythmStats.Ticks++;
            if (!_rhythmUnits.Due(now)) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var r = _rhythmRules;
                _rhythmUnits.Publish(now, in r);
                NoteLullEvents(now);
            }
            catch (Exception e)
            {
                _rhythmStats.Errors++;
                Failed("rhythm.publish", e);
            }
            _rhythmStats.PublishCostMs += (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
        }

        private void NoteLullEvents(double now)
        {
            var u = _rhythmUnits;
            for (int i = 0; i < u.StartedN; i++)
            {
                int a = u.Started[i], b = u.LullPartner[a];
                _rhythmStats.Lulls++;
                _rhythmStats.LullUnits += 2;
                _rhythmStats.AddStartBar(u.LullStartBar[a]);
                if (b >= 0) _rhythmStats.AddStartBar(u.LullStartBar[b]);
                if (_firstLull) continue;
                _firstLull = true;
                TraxLog.Info("rhythm", "first LULL at " + Sec(now) + " s: " + PressUnitName(a) + " (" + BraceMath.Name(u.Kind[a]) + ", front bar " + RhythmStats.Pct(u.LullStartBar[a])
                    + ", " + u.FrontMen[a] + " front men) and " + (b >= 0 ? PressUnitName(b) + " (" + BraceMath.Name(u.Kind[b]) + ", front bar " + RhythmStats.Pct(u.LullStartBar[b]) + ", "
                    + u.FrontMen[b] + " front men)" : "?") + " - both fronts spent after " + F1(now - u.ContactSince[a]) + " s in contact; each eases back " + F1(u.LullEase[a])
                    + " m (the gap between the fronts " + F1(u.LullStartGap[a]) + " m now)");
            }
            u.StartedN = 0;
            for (int i = 0; i < u.EndedN; i++)
            {
                var why = u.EndedWhy[i];
                _rhythmStats.AddLullEnd(why, u.EndedSeconds[i], u.EndedOpened[i]);
                if (_firstLullEnd) continue;
                _firstLullEnd = true;
                TraxLog.Info("rhythm", "first lull ENDED at " + Sec(now) + " s: " + PressUnitName(u.Ended[i]) + " - " + RhythmMath.LullEndName(why) + " after " + F1(u.EndedSeconds[i])
                    + " s, the gap opened by " + F1(u.EndedOpened[i]) + " m; the lines let go, the next lull of the pair no sooner than " + F1(_rhythmRules.LullCooldownSeconds) + " s");
            }
            u.EndedN = 0;
        }

        /// <summary>Step 79's seam: a wave ends its unit's lull (and its partner's) now. False = no lull.</summary>
        internal bool EndLullByWave(int key, double now)
        {
            if (!_rhythmUnits.EndLull(key, LullEnd.Wave, now)) return false;
            NoteLullEvents(now);
            return true;
        }

        /// <summary>Every tick while on: each man with a rhythm wish steered every 0.1 s.</summary>
        private void SteerRhythm(double now)
        {
            if (_rhythmMen.Count == 0) return;
            long start = Stopwatch.GetTimestamp();
            for (int i = _rhythmMen.Count - 1; i >= 0; i--)
            {
                if (i >= _rhythmMen.Count) continue;
                var st = _rhythmMen[i];
                var m = st.Rhythm;
                try
                {
                    if (st.Removed || !CourageBody.IsActive(st))
                    {
                        if (m.InSwap) EndSwap(st, RotationEnd.LeftField, now);
                        ReleaseRhythmWish(st, now, native: false);
                        continue;
                    }
                    if (now < m.NextSteer) continue;
                    m.NextSteer = now + FearMath.SteerSeconds;
                    SteerRhythmMan(st, m, now);
                }
                catch (Exception e)
                {
                    _rhythmStats.Errors++;
                    Failed("rhythm.steer", e);
                    SafeEndRhythm(st, now);
                }
            }
            _rhythmStats.SteerCostMs += (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }

        // ------------------------------------------------------------------ the look (from FearLook / FearSkipped)

        /// <summary>A man courage looked at (not held by the wall): his nearest enemy from courage's scan.</summary>
        private void RhythmLook(TrackedAgent st, double now, in FearNear near)
        {
            if (!FearBody.Unit(st, out int key, out _, out _, out _, out _, out _))
            {
                RhythmSkipped(st, now);
                return;
            }
            RhythmLookAt(st, now, key, near.Px, near.Py, near.InContact, near.InContact ? near.EnemyDist : double.NaN, near.EnemyX, near.EnemyY, near.FoeKey, held: false);
        }

        /// <summary>A held wall man (courage never looks at him): noted in his unit, his contact from the rotation's own scan only when he is tired.</summary>
        private void RhythmLookHeld(TrackedAgent st, double now, int key, double px, double py) =>
            RhythmLookAt(st, now, key, px, py, false, double.NaN, double.NaN, double.NaN, -1, held: true);

        /// <summary>A man courage's gates left out (fled, the player took him, a rider...): his swap and his lull wish end.</summary>
        private void RhythmSkipped(TrackedAgent st, double now)
        {
            var m = st.Rhythm;
            m.Eligible = false;
            if (!m.InSwap && !m.LullWish) return;
            try
            {
                if (m.InSwap) EndSwap(st, RotationEnd.Excluded, now);
                m.LullWish = false;
                m.RestUntil = double.NegativeInfinity;
                ReleaseRhythmWish(st, now, native: true);
            }
            catch (Exception e)
            {
                _rhythmStats.Errors++;
                Failed("rhythm.skip", e);
                SafeEndRhythm(st, now);
            }
        }

        private void RhythmLookAt(TrackedAgent st, double now, int key, double px, double py, bool contactKnown, double enemyDist, double ex, double ey, int foeKey, bool held)
        {
            long start = Stopwatch.GetTimestamp();
            try
            {
                RhythmLookCore(st, now, key, px, py, contactKnown, enemyDist, ex, ey, foeKey, held);
            }
            catch (Exception e)
            {
                _rhythmStats.Errors++;
                Failed("rhythm.look", e);
                SafeEndRhythm(st, now);
            }
            _rhythmStats.LookCostMs += (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }

        /// <summary>His unit's order and whether it is the player's - read once a unit a period (native reads).</summary>
        private void RhythmUnitFacts(TrackedAgent st, int key, double now, out BraceOrderKind kind, out bool player)
        {
            if (!(now - _rhythmUnitAt[key] < RhythmMath.PublishSeconds) || now < _rhythmUnitAt[key])
            {
                _rhythmUnitAt[key] = now;
                _rhythmKind[key] = FearBody.OrderKind(st);
                _rhythmPlayer[key] = RhythmBody.PlayerUnit(st);
            }
            kind = _rhythmKind[key];
            player = _rhythmPlayer[key];
        }

        private static bool RhythmBusy(TrackedAgent st, double now) =>
            RhythmMath.Busy(st.PrevAction, st.Impact.Reeling(now), st.Impact.Down(now), st.AiKickPhase != AiKickPhase.None);

        /// <summary>Another system walks him now (a step back, the wall's pull, the press's walk, courage's walk back).</summary>
        private static bool RhythmOwnWish(TrackedAgent st)
        {
            var s = st.Input;
            return s != null && (s.Backpedal || s.WallPull || (s.Press && s.PressMove) || s.Regroup);
        }

        /// <summary>His brace floor (his order's, his own roll) - NaN while the brace is off.</summary>
        private static double RhythmBraceFloor(TrackedAgent st, BraceOrderKind kind)
        {
            var br = BraceRules.From(TraxSettings.Shared);
            if (!br.Enabled) return double.NaN;
            return BraceMath.Floor(in br, BraceMath.Group(kind == BraceOrderKind.Count ? BraceOrderKind.Stop : kind), st.Brace?.FloorUnit ?? 0);
        }

        private void RhythmLookCore(TrackedAgent st, double now, int key, double px, double py, bool contactKnown, double enemyDist, double ex, double ey, int foeKey, bool held)
        {
            var r = _rhythmRules;
            var m = st.Rhythm;
            var u = _rhythmUnits;
            m.Key = key;
            m.LookedAt = now;
            m.Eligible = true;
            m.Held = held;
            _rhythmStats.Looks++;
            RhythmUnitFacts(st, key, now, out var kind, out bool player);
            double bar = r.AthleticsEnabled ? st.Fraction : 1.0;
            bool contact = contactKnown && !double.IsNaN(enemyDist) && enemyDist <= r.ContactMetres;
            u.Note(key, px, py, bar, contact, foeKey, kind, player, held);
            if (!r.ModEnabled || !r.AthleticsEnabled) return; // no bars - nothing tires, nothing to measure
            if (contact) _rhythmStats.AddContact(r.RotationOn, bar);

            // ---- the lull: his unit's line holds him (a held wall man through his slot)
            bool lull = u.LullApplies(key, in r);
            if (lull && !held && !m.LullWish)
            {
                m.LullWish = true;
                m.LullKey = key;
                if (!m.InSwap) EnsureRhythmWish(st, now);
            }

            // ---- the rotation
            if (m.InSwap || now < m.NextRotation) return;
            bool lullFront = lull && r.LullRotates && u.IsFrontMan(key, px, py, r.LullFrontDepthMetres, now);
            if (!contact && !lullFront && !held) return;
            double floor = RhythmBraceFloor(st, kind);
            if (!RhythmMath.IsTired(in r, bar, floor)) return;
            FindMate(st, m, now, key, px, py, bar, contact || lullFront, ex, ey, lull && lullFront, held);
        }

        /// <summary>A tired front man: one scan of his neighbours - contact (a held man), the direction to the enemy, the freshest mate right behind him.</summary>
        private void FindMate(TrackedAgent st, RhythmMind m, double now, int key, double px, double py, double bar, bool front, double ex, double ey, bool inLull, bool held)
        {
            var r = _rhythmRules;
            float radius = (float)Math.Max(r.ContactMetres, r.BehindMetres + r.FileMetres) + 0.2f;
            var scan = _rhythmScan;
            CourageBody.Scan(st, radius, scan);
            _rhythmStats.Scans++;
            try
            {
                // the nearest enemy in the scan (a held man's contact; the direction when courage gave none)
                double nd = double.NaN, nx = double.NaN, ny = double.NaN;
                for (int i = 0; i < scan.Count; i++)
                {
                    if (!scan.Enemy[i] || scan.Men[i] == null) continue;
                    if (double.IsNaN(nd) || scan.Dist[i] < nd)
                    {
                        nd = scan.Dist[i];
                        nx = scan.X[i];
                        ny = scan.Y[i];
                    }
                }
                if (!front)
                {
                    if (double.IsNaN(nd) || nd > r.ContactMetres) return; // a held man not in contact
                    _rhythmStats.AddContact(r.RotationOn, bar);
                }
                double ux = double.NaN, uy = double.NaN;
                if (!double.IsNaN(ex) && !double.IsNaN(ey)) AiInputMath.Direction(px, py, ex, ey, out ux, out uy);
                else if (!double.IsNaN(nx)) AiInputMath.Direction(px, py, nx, ny, out ux, out uy);
                if ((double.IsNaN(ux) || (ux == 0 && uy == 0)) && _rhythmUnits.Live(key, now) && _rhythmUnits.Enemy[key] >= 0)
                {
                    ux = _rhythmUnits.UX[key];
                    uy = _rhythmUnits.UY[key];
                }
                int on = r.RotationOn ? 1 : 0;
                if (double.IsNaN(ux) || (ux == 0 && uy == 0))
                {
                    _rhythmStats.Asked[on]++;
                    Refuse(st, RotationRefusal.NoEnemy, now);
                    return;
                }
                // a FRONT-rank man: no mate of his file between him and the enemy (a second-rank man near the fight is not the front)
                double reach = double.IsNaN(nd) ? r.ContactMetres : Math.Min(nd, r.ContactMetres);
                for (int i = 0; i < scan.Count; i++)
                {
                    var o = scan.Men[i];
                    if (o == null || scan.Enemy[i] || ReferenceEquals(o, st) || o.Rhythm.Key != key) continue;
                    RhythmMath.AlongAcross(px, py, ux, uy, scan.X[i], scan.Y[i], out double along, out double across);
                    if (RhythmMath.Covers(in r, along, across, reach)) return;
                }
                _rhythmStats.Asked[on]++;
                if (RhythmBusy(st, now))
                {
                    Refuse(st, RotationRefusal.Busy, now);
                    return;
                }
                if (RhythmOwnWish(st))
                {
                    Refuse(st, RotationRefusal.OwnWish, now);
                    return;
                }
                var br = BraceRules.From(TraxSettings.Shared);
                bool anyBehind = false, anyFresher = false;
                TrackedAgent? best = null;
                double bestBar = double.NegativeInfinity;
                for (int i = 0; i < scan.Count; i++)
                {
                    var o = scan.Men[i];
                    if (o == null || scan.Enemy[i] || ReferenceEquals(o, st)) continue;
                    var om = o.Rhythm;
                    if (om.Key != key || !om.Eligible || double.IsNaN(om.LookedAt) || now - om.LookedAt > RhythmMath.MateFreshSeconds || IsPlayer(o)) continue;
                    RhythmMath.AlongAcross(px, py, ux, uy, scan.X[i], scan.Y[i], out double along, out double across);
                    if (!RhythmMath.Behind(in r, along, across)) continue;
                    anyBehind = true;
                    double ofloor = br.Enabled ? BraceMath.Floor(in br, BraceMath.Group(_rhythmKind[key]), o.Brace?.FloorUnit ?? 0) : double.NaN;
                    if (!RhythmMath.Fresher(in r, bar, o.Fraction, RhythmMath.TiredBar(in r, ofloor))) continue;
                    anyFresher = true;
                    if (om.InSwap || now < om.NextRotation || RhythmBusy(o, now) || RhythmOwnWish(o)) continue;
                    if (o.Fraction > bestBar)
                    {
                        bestBar = o.Fraction;
                        best = o;
                    }
                }
                if (best == null)
                {
                    Refuse(st, !anyBehind ? RotationRefusal.NoMate : !anyFresher ? RotationRefusal.NotFresher : RotationRefusal.MateBusy, now);
                    return;
                }
                _rhythmStats.Chances[on]++;
                if (on == 0)
                {
                    if (!_firstChanceOff)
                    {
                        _firstChanceOff = true;
                        TraxLog.Info("rhythm", "first CHANCE to swap with the rotation OFF at " + Sec(now) + " s: " + Name(st) + " at " + RhythmStats.Pct(bar) + " in contact, "
                            + Name(best) + " at " + RhythmStats.Pct(best.Fraction) + " right behind him - counted only (" + r.RotationOffBecause + ")");
                    }
                    return;
                }
                if ((held || best.Rhythm.Held) && !RhythmBody.CanSwap(st, best))
                {
                    Refuse(st, RotationRefusal.NoSlots, now);
                    return;
                }
                StartSwap(st, best, ux, uy, inLull, now);
            }
            finally
            {
                scan.Clear();
            }
        }

        private void Refuse(TrackedAgent st, RotationRefusal why, double now)
        {
            _rhythmStats.Refused[(int)why]++;
            if (_firstRefusal) return;
            _firstRefusal = true;
            TraxLog.Info("rhythm", "first tired front man who could not swap at " + Sec(now) + " s: " + Name(st) + " at " + RhythmStats.Pct(st.Fraction) + " - "
                + RhythmMath.RefusalName(why) + " (the summary counts every reason)");
        }

        // ------------------------------------------------------------------ the swap

        private void StartSwap(TrackedAgent t, TrackedAgent f, double ux, double uy, bool inLull, double now)
        {
            var r = _rhythmRules;
            var mt = t.Rhythm;
            var mf = f.Rhythm;
            CourageBody.Position(t, out double tx, out double ty);
            CourageBody.Position(f, out double fx, out double fy);
            RhythmMath.AlongAcross(tx, ty, ux, uy, fx, fy, out _, out double across);
            int side = RhythmMath.SideOf(across);
            Begin(mt, RotationPhase.Hold, f, true, now, ux, uy, side, tx, ty, fx, fy, inLull);
            Begin(mf, RotationPhase.Approach, t, false, now, ux, uy, side, tx, ty, fx, fy, inLull);
            if (mt.Held)
            {
                mt.SlotOverride = true;
                mt.SlotX = tx;
                mt.SlotY = ty;
            }
            RhythmMath.BesidePoint(tx, ty, ux, uy, side, r.BesideMetres, out double bx, out double by);
            if (mf.Held)
            {
                mf.SlotOverride = true;
                mf.SlotX = bx;
                mf.SlotY = by;
            }
            if (!EnsureRhythmWish(t, now) || !EnsureRhythmWish(f, now))
            {
                EndSwap(t, RotationEnd.Error, now);
                return;
            }
            _rhythmStats.Started++;
            _rhythmStats.TiredBarSum += t.Fraction;
            _rhythmStats.FreshBarSum += f.Fraction;
            if (inLull) _rhythmStats.InLull++;
            if (!_firstSwapAsked)
            {
                _firstSwapAsked = true;
                TraxLog.Info("rhythm", "first SWAP asked at " + Sec(now) + " s: " + Name(t) + " (" + RhythmStats.Pct(t.Fraction) + (mt.Held ? ", a held wall man" : "") + ") at the front"
                    + (inLull ? " in a lull" : " in contact") + ", " + Name(f) + " (" + RhythmStats.Pct(f.Fraction) + ") right behind him - he steps up " + F1(r.BesideMetres)
                    + " m beside him while the tired man holds and guards");
            }
            if (inLull && !_firstLullRotation)
            {
                _firstLullRotation = true;
                TraxLog.Info("rhythm", "first swap DURING A LULL at " + Sec(now) + " s: " + Name(t) + " (" + RhythmStats.Pct(t.Fraction) + ") goes to the back, " + Name(f) + " ("
                    + RhythmStats.Pct(f.Fraction) + ") comes forward - no enemy in reach");
            }
            SteerSwap(t, mt, now);
            RelookWall(t, now);
            RelookWall(f, now);
        }

        private static void Begin(RhythmMind m, RotationPhase phase, Fighter partner, bool tired, double now, double ux, double uy, int side, double tx, double ty, double fx, double fy, bool inLull)
        {
            m.Phase = phase;
            m.Partner = partner;
            m.Tired = tired;
            m.Since = m.PhaseSince = now;
            m.UX = ux;
            m.UY = uy;
            m.Side = side;
            m.TiredX = tx;
            m.TiredY = ty;
            m.FreshX = fx;
            m.FreshY = fy;
            m.DuringLull = inLull;
            m.Swapped = m.Arrived = false;
            m.SlotOverride = false;
            m.ToX = m.ToY = double.NaN;
        }

        /// <summary>One swap's step (driven from the tired man's record): HOLD until the fresh man is BESIDE him, then the slots swap and both walk to each
        /// other's start; done when the tired man stands in the fresh man's place.</summary>
        private void SteerSwap(TrackedAgent t, RhythmMind mt, double now)
        {
            var r = _rhythmRules;
            var f = mt.Partner as TrackedAgent;
            if (f == null || f.Removed || !CourageBody.IsActive(f))
            {
                EndSwap(t, RotationEnd.LeftField, now);
                return;
            }
            if (!r.RotationOn)
            {
                EndSwap(t, RotationEnd.SwitchedOff, now);
                return;
            }
            var mf = f.Rhythm;
            var st = t.Input;
            var sf = f.Input;
            if (st == null || sf == null)
            {
                EndSwap(t, RotationEnd.Error, now);
                return;
            }
            CourageBody.Position(t, out double tx, out double ty);
            CourageBody.Position(f, out double fx, out double fy);
            if (now - mt.Since > r.MaxSeconds)
            {
                EndSwap(t, RotationEnd.Timeout, now);
                return;
            }
            st.RhythmHoldAttacks = true; // the tired man guards only, the whole swap
            if (mt.Phase == RotationPhase.Hold)
            {
                if (RhythmMath.IsBeside(in r, tx, ty, fx, fy, mt.UX, mt.UY))
                {
                    double wait = now - mt.Since;
                    _rhythmStats.WaitSum += wait;
                    _rhythmStats.WaitN++;
                    if (wait > _rhythmStats.WaitMax) _rhythmStats.WaitMax = wait;
                    bool swapped = false;
                    try
                    {
                        if (RhythmBody.CanSwap(t, f)) swapped = RhythmBody.SwapSlots(t, f);
                        else _rhythmStats.NoSlotSwap++;
                    }
                    catch (Exception e)
                    {
                        _rhythmStats.SwapFailed++;
                        Failed("rhythm.swap-slots", e);
                    }
                    if (swapped) _rhythmStats.Swapped++;
                    mt.Swapped = mf.Swapped = swapped;
                    mt.Phase = RotationPhase.Back;
                    mf.Phase = RotationPhase.In;
                    mt.PhaseSince = mf.PhaseSince = now;
                    mt.ToX = mt.FreshX;
                    mt.ToY = mt.FreshY;
                    mf.ToX = mt.TiredX;
                    mf.ToY = mt.TiredY;
                    if (mt.Held)
                    {
                        mt.SlotOverride = true;
                        mt.SlotX = mt.ToX;
                        mt.SlotY = mt.ToY;
                    }
                    if (mf.Held)
                    {
                        mf.SlotOverride = true;
                        mf.SlotX = mf.ToX;
                        mf.SlotY = mf.ToY;
                    }
                    if (!_firstSwapBeside)
                    {
                        _firstSwapBeside = true;
                        TraxLog.Info("rhythm", "first fresh man BESIDE the tired one at " + Sec(now) + " s after " + F2(wait) + " s: " + Name(f) + " beside " + Name(t) + " - the slots "
                            + (swapped ? "swapped (Formation.SwitchUnitLocations)" : "not swapped (no slots - walked only)") + "; the tired man backs into the fresh man's place, facing the enemy");
                    }
                    RelookWall(t, now);
                    RelookWall(f, now);
                }
                else if (now - mt.Since > r.ApproachSeconds)
                {
                    EndSwap(t, RotationEnd.NeverBeside, now);
                    return;
                }
                else if (f.Impact.Reeling(now) || f.Impact.Down(now))
                {
                    EndSwap(t, RotationEnd.Staggered, now);
                    return;
                }
                else
                {
                    st.RhythmMove = true; // the tired man HOLDS his place - the line keeps its man
                    st.RhythmX = st.RhythmY = 0f;
                    RhythmMath.BesidePoint(tx, ty, mt.UX, mt.UY, mt.Side, r.BesideMetres, out double bx, out double by);
                    if (mf.Held)
                    {
                        mf.SlotX = bx;
                        mf.SlotY = by;
                    }
                    WalkTo(f, mf, sf, fx, fy, bx, by, now);
                    return;
                }
            }
            // back / in
            double tdx = mt.ToX - tx, tdy = mt.ToY - ty;
            bool tArrived = Math.Sqrt(tdx * tdx + tdy * tdy) <= RhythmMath.ArriveMetres || (tx - mt.ToX) * mt.UX + (ty - mt.ToY) * mt.UY <= -0.2;
            double fdx = mf.ToX - fx, fdy = mf.ToY - fy;
            if (!mf.Arrived && Math.Sqrt(fdx * fdx + fdy * fdy) <= RhythmMath.ArriveMetres) mf.Arrived = true;
            if (tArrived && (mf.Arrived || now - mt.PhaseSince > 1.5))
            {
                EndSwap(t, RotationEnd.Done, now);
                return;
            }
            if (tArrived)
            {
                st.RhythmMove = true;
                st.RhythmX = st.RhythmY = 0f;
            }
            else WalkTo(t, mt, st, tx, ty, mt.ToX, mt.ToY, now);
            if (mf.Arrived)
            {
                sf.RhythmMove = false; // in the front slot: his own AI (he fights)
            }
            else WalkTo(f, mf, sf, fx, fy, mf.ToX, mf.ToY, now);
        }

        /// <summary>His walk this steer toward a point (his own frame), slower near it, standing when the ground ahead ends or he is there.</summary>
        private void WalkTo(TrackedAgent st, RhythmMind m, AiInputState s, double px, double py, double tx, double ty, double now)
        {
            double dx = tx - px, dy = ty - py, dist = Math.Sqrt(dx * dx + dy * dy);
            s.RhythmMove = true;
            if (dist <= RhythmMath.ArriveMetres * 0.5 || !(dist > 1e-6))
            {
                s.RhythmX = s.RhythmY = 0f;
                return;
            }
            dx /= dist;
            dy /= dist;
            if (now >= m.NextEdge)
            {
                m.NextEdge = now + AiInputMath.EdgeCheckSeconds;
                if (!CourageBody.GroundOk(st, dx, dy))
                {
                    s.RhythmX = s.RhythmY = 0f;
                    return;
                }
            }
            if (CourageBody.ToLocal(st, dx, dy, out float lx, out float ly))
            {
                float k = (float)FearMath.Stick(dist);
                s.RhythmX = lx * k;
                s.RhythmY = ly * k;
                _rhythmStats.Steers++;
            }
            else s.RhythmX = s.RhythmY = 0f;
        }

        /// <summary>The swap ends for both men (counted once): the cooldown, the tired man's rest after a done swap, the walls' slots back.</summary>
        private void EndSwap(TrackedAgent st, RotationEnd why, double now)
        {
            var m = st.Rhythm;
            if (!m.InSwap) return;
            var other = m.Partner as TrackedAgent;
            var t = m.Tired ? st : other;
            var mt = t?.Rhythm;
            _rhythmStats.Ends[(int)why]++;
            var r = _rhythmRules;
            if (why == RotationEnd.Done && t != null && mt != null)
            {
                _rhythmStats.RotatedMen++;
                _rhythmStats.DoneSecondsSum += now - mt.Since;
                _rhythmStats.DoneN++;
                if (!mt.Held && r.RestSeconds > 0)
                {
                    mt.RestUntil = now + r.RestSeconds;
                    mt.RestLine = mt.FreshX * mt.UX + mt.FreshY * mt.UY;
                }
                if (!_firstSwapDone)
                {
                    _firstSwapDone = true;
                    TraxLog.Info("rhythm", "first SWAP DONE at " + Sec(now) + " s in " + F2(now - mt.Since) + " s: " + Name(t) + " (" + RhythmStats.Pct(t.Fraction) + ") in the second rank, "
                        + (other != null && !ReferenceEquals(other, t) ? Name(other) : Name(st)) + " at the front; " + (mt.Swapped ? "slots swapped" : "no slots") + "; he holds his new place "
                        + F1(r.RestSeconds) + " s, neither swaps again for " + F1(r.CooldownSeconds) + " s");
                }
            }
            else if (!_firstSwapOff && why != RotationEnd.Done)
            {
                _firstSwapOff = true;
                TraxLog.Info("rhythm", "first swap NOT finished at " + Sec(now) + " s: " + Name(st) + " - " + RhythmMath.EndName(why));
            }
            Finish(st, m, now, r);
            if (other != null && other.Rhythm.InSwap && ReferenceEquals(other.Rhythm.Partner, st)) Finish(other, other.Rhythm, now, r);
        }

        private void Finish(TrackedAgent st, RhythmMind m, double now, in RhythmRules r)
        {
            bool held = m.SlotOverride;
            m.EndSwap();
            m.NextRotation = now + r.CooldownSeconds;
            var s = st.Input;
            if (s != null)
            {
                s.RhythmMove = false;
                s.RhythmHoldAttacks = false;
                s.RhythmX = s.RhythmY = 0f;
            }
            if (held) RelookWall(st, now);
        }

        private int EndAllSwaps(RotationEnd why, double now)
        {
            int n = 0;
            for (int i = _rhythmMen.Count - 1; i >= 0; i--)
            {
                if (i >= _rhythmMen.Count) continue;
                var st = _rhythmMen[i];
                if (!st.Rhythm.InSwap) continue;
                try
                {
                    EndSwap(st, why, now);
                    n++;
                }
                catch (Exception e)
                {
                    Failed("rhythm.end-swaps", e);
                    st.Rhythm.EndSwap();
                }
            }
            return n;
        }

        // ------------------------------------------------------------------ a man's wish: the lull's line and the rest

        /// <summary>Not in a swap: his lull line (walked back to it, clamped at it) and his rest after going back (clamped at his new place).</summary>
        private void SteerRhythmMan(TrackedAgent st, RhythmMind m, double now)
        {
            var s = st.Input;
            if (s == null)
            {
                ReleaseRhythmWish(st, now, native: true);
                return;
            }
            if (m.InSwap)
            {
                if (m.Tired) SteerSwap(st, m, now);
                else if (!(m.Partner is TrackedAgent p) || !p.Rhythm.InSwap) EndSwap(st, RotationEnd.LeftField, now);
                if (m.InSwap)
                {
                    LullClamp(st, m, s, now); // the lull's line still holds a swapping pair back
                    return;
                }
            }
            var r = _rhythmRules;
            var u = _rhythmUnits;
            if (m.LullWish && (m.Held || !u.LullApplies(m.LullKey, in r))) m.LullWish = false;
            bool rest = now < m.RestUntil && r.RotationOn && !m.Held && !double.IsNaN(m.RestLine);
            if (!m.LullWish && !rest)
            {
                ReleaseRhythmWish(st, now, native: true);
                return;
            }
            CourageBody.Position(st, out double px, out double py);
            s.RhythmMove = false;
            s.RhythmClamp = false;
            if (m.LullWish)
            {
                int k = m.LullKey;
                double ahead = u.AheadOfLullLine(k, px, py);
                int act = RhythmMath.LullAction(ahead);
                if (act == 2)
                {
                    if (!m.LullCounted)
                    {
                        m.LullCounted = true;
                        _rhythmStats.MenWalkedBack++;
                    }
                    if (!m.EverWalkedBack) m.EverWalkedBack = true;
                    double ux = u.LullUX[k], uy = u.LullUY[k];
                    WalkTo(st, m, s, px, py, px - ux * ahead, py - uy * ahead, now);
                    if (!_firstLullWalk)
                    {
                        _firstLullWalk = true;
                        TraxLog.Info("rhythm", "first man WALKED BACK in a lull at " + Sec(now) + " s: " + Name(st) + " of " + PressUnitName(k) + ", " + F1(ahead)
                            + " m ahead of his unit's lull line - backing to it facing the enemy, then held there");
                    }
                }
                else if (act == 1) SetClamp(st, s, u.LullUX[k], u.LullUY[k]);
            }
            if (rest && !s.RhythmClamp && !s.RhythmMove)
            {
                double a = px * m.UX + py * m.UY - m.RestLine;
                if (a > -RhythmMath.LookaheadMetres)
                {
                    SetClamp(st, s, m.UX, m.UY);
                    _rhythmStats.RestClamps++;
                }
            }
        }

        /// <summary>A swapping man of a unit in a lull: the lull's clamp at its line (the walk is the swap's).</summary>
        private void LullClamp(TrackedAgent st, RhythmMind m, AiInputState s, double now)
        {
            s.RhythmClamp = false;
            if (!m.LullWish) return;
            var r = _rhythmRules;
            var u = _rhythmUnits;
            if (!u.LullApplies(m.LullKey, in r))
            {
                m.LullWish = false;
                return;
            }
            CourageBody.Position(st, out double px, out double py);
            double ahead = u.AheadOfLullLine(m.LullKey, px, py);
            if (ahead > -RhythmMath.LookaheadMetres) SetClamp(st, s, u.LullUX[m.LullKey], u.LullUY[m.LullKey]);
        }

        private void SetClamp(TrackedAgent st, AiInputState s, double ux, double uy)
        {
            if (!CourageBody.ToLocal(st, ux, uy, out float lx, out float ly)) return;
            float len = (float)Math.Sqrt(lx * lx + ly * ly);
            if (!(len > 1e-6f)) return;
            s.RhythmClamp = true;
            s.RhythmNX = lx / len;
            s.RhythmNY = ly / len;
        }

        /// <summary>His input wish on (the component hooked) - listed for the steering. False = the hook failed.</summary>
        private bool EnsureRhythmWish(TrackedAgent st, double now)
        {
            var m = st.Rhythm;
            if (m.Wish) return true;
            EnsureInput(st);
            AiInputHook.HookResult hook;
            try
            {
                hook = CourageBody.Hook(st);
            }
            catch (Exception e)
            {
                _rhythmStats.Errors++;
                Failed("rhythm.hook", e);
                return false;
            }
            NoteHooked(st, hook);
            AiInputHook.SetRhythm(st, true, now);
            var s = st.Input!;
            m.MoveAtStart = s.RhythmMoveFrames;
            m.ClampAtStart = s.RhythmClampFrames;
            m.HeldAtStart = s.RhythmHeldFrames;
            m.Wish = true;
            m.NextSteer = now;
            m.NextEdge = now;
            _rhythmMen.Add(st);
            return true;
        }

        private void ReleaseRhythmWish(TrackedAgent st, double now, bool native)
        {
            var m = st.Rhythm;
            m.LullCounted = false;
            if (!m.Wish) return;
            m.Wish = false;
            _rhythmMen.Remove(st);
            var s = st.Input;
            if (s == null) return;
            _rhythmStats.MoveFrames += s.RhythmMoveFrames - m.MoveAtStart;
            _rhythmStats.ClampFrames += s.RhythmClampFrames - m.ClampAtStart;
            _rhythmStats.HoldFrames += s.RhythmHeldFrames - m.HeldAtStart;
            if (!s.Rhythm) return;
            AiInputHook.SetRhythm(st, false, now);
            if (native && !st.Removed)
            {
                try
                {
                    CourageBody.Release(st);
                }
                catch (Exception e)
                {
                    Failed("rhythm.release", e);
                }
            }
            DrainInputError(st);
        }

        private void SafeEndRhythm(TrackedAgent st, double now)
        {
            try
            {
                var m = st.Rhythm;
                if (m.InSwap) EndSwap(st, RotationEnd.Error, now);
                m.LullWish = false;
                m.RestUntil = double.NegativeInfinity;
                ReleaseRhythmWish(st, now, native: true);
            }
            catch
            {
                st.Rhythm.EndSwap();
                st.Rhythm.Wish = false;
                _rhythmMen.Remove(st);
                if (st.Input != null) st.Input.Rhythm = st.Input.RhythmMove = st.Input.RhythmClamp = st.Input.RhythmHoldAttacks = false;
            }
        }

        private void EndAllRhythm(RotationEnd why, double now, bool native)
        {
            EndAllSwaps(why, now);
            for (int i = _rhythmMen.Count - 1; i >= 0; i--)
            {
                if (i >= _rhythmMen.Count) continue;
                var st = _rhythmMen[i];
                try
                {
                    st.Rhythm.LullWish = false;
                    st.Rhythm.RestUntil = double.NegativeInfinity;
                    ReleaseRhythmWish(st, now, native && !st.Removed);
                }
                catch (Exception e)
                {
                    Failed("rhythm.end-all", e);
                    SafeEndRhythm(st, now);
                }
            }
            _rhythmMen.Clear();
        }

        // ------------------------------------------------------------------ the wall (from LookWall)

        /// <summary>
        /// A held wall man's slot as the rhythm wants it (from LookWall, right after the facts are read): in a swap his slot is the swap's place (beside
        /// the tired man / the other's start) - the wall hold, stand firm's pinned frame and the walk back all agree with the swap; in a lull his slot moves
        /// back by the lull's ease along his unit's direction to the enemy - stand firm walks him back to it, facing the enemy. True = changed.
        /// </summary>
        private bool RhythmWallSlot(TrackedAgent st, ref WallFacts f, double now)
        {
            if (!f.HasPositions) return false;
            var r = _rhythmRules;
            if (!r.AnyOn) return false;
            var m = st.Rhythm;
            if (m.SlotOverride && m.InSwap && r.RotationOn)
            {
                f.SlotX = m.SlotX;
                f.SlotY = m.SlotY;
                return true;
            }
            int k = m.Key;
            var u = _rhythmUnits;
            if (k < 0 || !u.LullApplies(k, in r)) return false;
            double e = u.LullEase[k];
            f.SlotX -= u.LullUX[k] * e;
            f.SlotY -= u.LullUY[k] * e;
            _rhythmStats.SlotShifts++;
            if (!_firstSlotShift)
            {
                _firstSlotShift = true;
                TraxLog.Info("rhythm", "first WALL SLOT shifted back in a lull at " + Sec(now) + " s: " + Name(st) + " of " + PressUnitName(k) + " - his slot " + F1(e)
                    + " m back toward his side; the wall hold and stand firm walk him to it facing the enemy");
            }
            return true;
        }

        /// <summary>The engine's walk target pinned to the rhythm's slot (step 68's call) - not a stand-firm man (his own pin does it).</summary>
        private void RhythmFrameLook(TrackedAgent st, WallState ws, in WallFacts f)
        {
            try
            {
                if (WallBody.Pace(st, f.SlotX, f.SlotY)) ws.Paced = true;
            }
            catch (Exception e)
            {
                _rhythmStats.Errors++;
                Failed("rhythm.wall-frame", e);
            }
        }

        /// <summary>A held man looked at by the wall at once (his swap's slot written now, not at vanilla's next update).</summary>
        private void RelookWall(TrackedAgent st, double now)
        {
            var ws = st.Wall;
            if (ws == null || !ws.Held || ws.Leash.Free || !_wallStarted || _wallClosed || st.Removed) return;
            try
            {
                var wr = WallRules.From(TraxSettings.Shared);
                LookWall(st, ws, now, in wr, WallWriteKind.Poll, sample: false);
            }
            catch (Exception e)
            {
                WallFailed(st, ws, e, now);
            }
        }

        // ------------------------------------------------------------------ the end

        private void RhythmLeftField(TrackedAgent st)
        {
            try
            {
                double now = SafeNow();
                var m = st.Rhythm;
                if (m.InSwap) EndSwap(st, RotationEnd.LeftField, now);
                m.LullWish = false;
                m.RestUntil = double.NegativeInfinity;
                m.Eligible = false;
                ReleaseRhythmWish(st, now, native: false);
            }
            catch (Exception e)
            {
                Failed("rhythm.left", e);
            }
        }

        private void CloseRhythm(bool agentsAlive)
        {
            if (_rhythmClosed) return;
            _rhythmClosed = true;
            double now = SafeNow();
            EndAllRhythm(RotationEnd.MissionEnd, now, agentsAlive);
            _rhythmUnits.EndAll(LullEnd.MissionEnd, now);
            NoteLullEvents(now);
        }

        internal void WriteRhythmSummary()
        {
            var lines = new List<string>(2);
            _rhythmStats.AddSummaryLines(lines, RhythmRules.From(TraxSettings.Shared), _rhythmUnits);
            foreach (var line in lines) TraxLog.Info("summary", line);
        }
    }
}

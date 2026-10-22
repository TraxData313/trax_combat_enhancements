using System;
using System.Collections.Generic;
using System.Diagnostics;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 77 - PRESS, DON'T PASS (Toni's battles 2026-10-22 10:2x-10:48: 108 T1 recruits against his 59 T5 shield wall - at contact the AI's
    /// tactical charge, the rear ranks walking round his wall's ends and behind it, "dancing", then through the middle; AI_NOTES "Step 77", DESIGN
    /// decision 71). It rides courage's looks (every AI man on foot in a formation, about every 0.5 s, ON and OFF - the OFF looks only measure):
    /// the LINE TABLE (<see cref="PressLines"/>) learns each formation's men (the block hold's reads - one set of native reads) and every 0.5 s
    /// publishes which formations are enemy LINES and their two faces (front, ends, gaps); each unit's enemy line is the one its men see nearest.
    /// A man near a line gets a seventh input wish (<see cref="AiInputState.Press"/>): THE CLAMP takes the part of his final movement toward the line
    /// out of his own input - LAST, whoever wrote it; past its front he is walked straight back out, behind it round its nearest end - after a step
    /// back's backpedal and the wall's pull, BEFORE courage's walk back, the fear flock and the block hold (being inside or behind the enemy is the
    /// most wrong place). Through only at a gap, round an end only with a genuinely wider mass. His attacks his own. The master switch FIRST, then
    /// <c>PressDontPass</c> - off = every wish let go at once, the looks keep measuring.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly PressLines _pressLines = new PressLines();
        private readonly PressStats _pressStats = new PressStats();
        private readonly List<TrackedAgent> _pressing = new List<TrackedAgent>(128);
        private readonly bool[] _pressBlockHeld = new bool[FearMath.UnitCount], _pressRing = new bool[FearMath.UnitCount];
        private readonly double[] _pressBodyWidth = new double[FearMath.UnitCount];
        private PressRules _pressRules = new PressRules(false, false);
        private string _seenPressText = string.Empty;
        private bool _firstPressLine, _firstPressClamp, _firstPressPush, _firstPressReturn, _firstPressGap, _firstPressWrap, _firstCrossOn, _firstCrossOff;
        private bool _pressClosed;

        internal PressLines PressLines => _pressLines;

        internal PressStats PressStats => _pressStats;

        /// <summary>Men with a press wish now (clamped, pushed or walked).</summary>
        internal int PressingNow => _pressing.Count;

        private static string PressText(TraxSettings s) => PressRules.From(s).Describe();

        private void StartPress()
        {
            _pressRules = PressRules.From(TraxSettings.Shared);
            _seenPressText = PressText(TraxSettings.Shared);
            TraxLog.Info("press", "mission start: " + _seenPressText + " - read live; it rides courage's looks (every " + F1(CourageMath.PollSeconds)
                + " s), the enemy lines' fronts every " + F1(PressMath.PublishSeconds) + " s; the [press] lines log the first line, clamp, push, walk round an end, gap, wrap and crossing; the [summary] \"press, don't pass\" lines");
        }

        private void NotePressSettings(TraxSettings s)
        {
            string now = PressText(s);
            if (now == _seenPressText) return;
            _seenPressText = now;
            TraxLog.Info("press", "settings changed mid-mission at " + Sec(SafeNow()) + " s: " + now + " - at every man's next look");
        }

        // ------------------------------------------------------------------ the tick (from TickCourage)

        /// <summary>The switches, every tick: off → every wish let go at once.</summary>
        private void TickPressSwitch(double now)
        {
            var r = PressRules.From(TraxSettings.Shared);
            bool wasOn = _pressRules.On;
            _pressRules = r;
            if (!r.On && _pressing.Count > 0) EndAllPress(PressEnd.SwitchedOff, now, native: true, r.OffBecause);
            else if (wasOn && !r.On) TraxLog.Info("press", "press, don't pass switched OFF at " + Sec(now) + " s (" + r.OffBecause + "): the game's own; the looks keep measuring");
        }

        /// <summary>The line table published every 0.5 s (after the blocks' frames - a held block is a line).</summary>
        private void TickPressLines(double now)
        {
            _pressStats.Ticks++;
            if (!_pressLines.Due(now)) return;
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var br = _blockRules;
                var bu = _blockUnits;
                for (int k = 0; k < FearMath.UnitCount; k++)
                {
                    _pressBlockHeld[k] = br.On && bu.Counted[k] && bu.Live(k, now) && BlockHoldMath.Strength(in br, bu.Depth[k]) > 0;
                    _pressBodyWidth[k] = _pressBlockHeld[k] ? bu.CapWidth[k] : double.NaN;
                    string a = bu.Arrangement[k];
                    _pressRing[k] = a == "Square" || a == "Circle";
                }
                var r = _pressRules;
                _pressLines.Publish(now, in r, _pressBlockHeld, _pressRing, _pressBodyWidth);
                if (!_firstPressLine) FirstPressLine(now);
                if (!_firstPressGap) FirstPressGap(now);
            }
            catch (Exception e)
            {
                _pressStats.Errors++;
                Failed("press.publish", e);
            }
            _pressStats.PublishCostMs += (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
        }

        private string PressUnitName(int key)
        {
            var rep = key >= 0 && key < FearMath.UnitCount ? _blockRep[key] : null;
            return rep != null ? SafeBlockName(rep) : "unit " + key;
        }

        private string PressFaceText(int line, int face)
        {
            var t = _pressLines;
            if (t.Kind[line] == PressLineKind.Ring) return "a ring of radius " + F1(t.RingR[line]) + " m";
            int at = 2 * line + face;
            return (face == 0 ? "its front" : "its rear") + " " + F1(t.Hi[at] - t.Lo[at]) + " m wide (" + t.FrontMen[at] + " men hold it), " + F1(t.Depth[line]) + " m deep, "
                   + (t.GapN[at] > 0 ? t.GapN[at] + " gap(s) of " + F1(t.GapHi[at * PressMath.MaxGaps] - t.GapLo[at * PressMath.MaxGaps]) + " m+" : "no gap");
        }

        private void FirstPressLine(double now)
        {
            for (int k = 0; k < FearMath.UnitCount; k++)
            {
                if (!_pressLines.IsLine(k, now)) continue;
                _firstPressLine = true;
                TraxLog.Info("press", "first enemy LINE at " + Sec(now) + " s: " + PressUnitName(k) + " - " + PressMath.KindName(_pressLines.Kind[k]) + ", "
                    + _pressLines.Men[k] + " men looked at; " + PressFaceText(k, 0));
                return;
            }
        }

        private void FirstPressGap(double now)
        {
            for (int k = 0; k < FearMath.UnitCount; k++)
            {
                if (!_pressLines.IsLine(k, now)) continue;
                for (int f = 0; f < 2; f++)
                {
                    int at = 2 * k + f;
                    if (_pressLines.GapN[at] <= 0) continue;
                    _firstPressGap = true;
                    TraxLog.Info("press", "first GAP in a line at " + Sec(now) + " s: " + PressUnitName(k) + " - " + PressFaceText(k, f) + "; men may go through it, a man behind it is left alone (a broken line)");
                    return;
                }
            }
        }

        /// <summary>Every tick while on: the pressing men's clamps / walks refreshed every <see cref="FearMath.SteerSeconds"/> each.</summary>
        private void SteerPressers(double now)
        {
            if (_pressing.Count == 0) return;
            long start = Stopwatch.GetTimestamp();
            for (int i = _pressing.Count - 1; i >= 0; i--)
            {
                if (i >= _pressing.Count) continue;
                var st = _pressing[i];
                var pm = st.Press;
                try
                {
                    if (st.Removed || !pm.Wish || !CourageBody.IsActive(st))
                    {
                        _pressing.RemoveAt(i);
                        continue;
                    }
                    if (now >= pm.NextSteer) SteerPresser(st, pm, now);
                }
                catch (Exception e)
                {
                    _pressStats.Errors++;
                    Failed("press.steer", e);
                    SafeEndPress(st, now);
                }
            }
            _pressStats.SteerCostMs += (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }

        // ------------------------------------------------------------------ the look (from FearLook - every courage path)

        private void PressLook(TrackedAgent st, double now, in FearNear near, FearCourage courage)
        {
            long start = Stopwatch.GetTimestamp();
            try
            {
                PressLookCore(st, now, in near);
            }
            catch (Exception e)
            {
                _pressStats.Errors++;
                Failed("press.look", e);
                SafeEndPress(st, now);
            }
            _pressStats.LookCostMs += (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }

        /// <summary>The unit's line, the face he stands before, his place in its frame: d in front of it (his side positive), l along it.</summary>
        private bool PressFrame(int key, int line, double px, double py, double now, out int face, out double d, out double l)
        {
            var t = _pressLines;
            face = 0;
            l = 0;
            if (t.Kind[line] == PressLineKind.Ring)
            {
                t.RingFrame(line, px, py, out d, out _, out _);
                return true;
            }
            if (t.LineOf(key, now) == line) face = t.Face[key];
            else
            {
                t.Frame(line, 0, px, py, out double d0, out _);
                face = d0 >= -t.Depth[line] / 2 ? 0 : 1; // a man of a unit not fighting it: the face he stands nearer
            }
            t.Frame(line, face, px, py, out d, out l);
            return true;
        }

        private void PressLookCore(TrackedAgent st, double now, in FearNear near)
        {
            var r = _pressRules;
            var pm = st.Press;
            var t = _pressLines;
            if (!FearBody.Unit(st, out int key, out _, out _, out _, out _, out _))
            {
                if (pm.Wish) EndPress(st, PressEnd.Excluded, now, native: true);
                pm.SeenLine = -1;
                return;
            }
            if (near.FoeKey >= 0 && near.FoeKey != key) t.Vote(key, near.FoeKey);
            int unitLine = t.LineOf(key, now);
            int line = near.FoeKey >= 0 && near.FoeKey != key && t.IsLine(near.FoeKey, now) ? near.FoeKey : unitLine;
            if (line < 0)
            {
                if (pm.Wish) EndPress(st, PressEnd.NoLine, now, native: true);
                pm.SeenLine = -1;
                return;
            }
            bool ofUnit = line == unitLine;
            bool ring = t.Kind[line] == PressLineKind.Ring;
            PressFrame(key, line, near.Px, near.Py, now, out int face, out double d, out double l);
            int at = 2 * line + face;
            double lo = t.Lo[at], hi = t.Hi[at], depth = t.Depth[line];
            bool inGap = !ring && PressMath.InGap(l, t.GapLo, t.GapHi, at * PressMath.MaxGaps, t.GapN[at]);
            bool broken = !ring && t.GapN[at] > 0;
            double win = _fearUnits.WinOf(key, now);
            bool winning = !double.IsNaN(win) && 100 * win >= r.WinPercent;
            bool unitWraps = ofUnit && !ring && t.Wraps[key];
            double unitFront = ofUnit ? t.UnitFront[key] : double.NaN;
            bool rearRank = !double.IsNaN(unitFront) && d > unitFront + r.FrontDepthMetres;
            bool unitNear = !double.IsNaN(unitFront) && unitFront <= r.WatchMetres;
            var o = ring ? PressMath.DecideRing(in r, d, winning)
                : PressMath.Decide(in r, d, l, lo, hi, depth, inGap, unitWraps, near.PressMates, broken, winning, rearRank, unitNear);
            int on = r.On ? 1 : 0;

            // ---- the measurement (ON and OFF)
            if (d <= r.WatchMetres)
            {
                _pressStats.NearLooks[on]++;
                if (d < -PressMath.CrossedMetres) _pressStats.PastLooks[on]++;
            }
            if (o.Action != PressAction.Far) _pressStats.Actions[on, (int)o.Action]++;
            if (pm.SeenLine != line || pm.SeenFace != face)
            {
                pm.SeenLine = line;
                pm.SeenFace = face;
                pm.WasFront = d >= 0;
            }
            else if (pm.WasFront && d < -PressMath.CrossedMetres)
            {
                pm.WasFront = false;
                NoteCrossing(st, now, on, line, face, key, l, lo, hi, ring, o.Action);
            }
            else if (d >= 0) pm.WasFront = true;
            if (r.On && !_firstPressWrap && o.Action == PressAction.Wrap)
            {
                _firstPressWrap = true;
                TraxLog.Info("press", "first man let ROUND a line's end with his mass at " + Sec(now) + " s: " + Name(st) + " of " + PressUnitName(key) + " ("
                    + F1(t.UnitWidth[key]) + " m wide) on " + PressUnitName(line) + " (" + PressFaceText(line, face) + "), " + near.PressMates + " mates within "
                    + F1(r.WrapMatesMetres) + " m, " + F1(d) + " m in front of it, " + F1(l) + " m along it");
            }

            // ---- the press
            if (!r.On)
            {
                if (pm.Wish) EndPress(st, PressEnd.SwitchedOff, now, native: true);
                return;
            }
            bool wants = o.Action == PressAction.Clamp || o.Action == PressAction.Push || o.Action == PressAction.Return;
            if (!wants)
            {
                if (pm.Wish) EndPress(st, PressEnd.Free, now, native: true);
                return;
            }
            var before = pm.Wish ? pm.Action : PressAction.NoLine;
            pm.Line = line;
            pm.Face = face;
            pm.Ring = ring;
            pm.Action = o.Action;
            pm.ClampFront = o.ClampFront;
            pm.Side = o.Side;
            pm.ToD = o.ToD;
            pm.ToL = o.ToL;
            if (!pm.Wish && !StartPressInput(st, pm, now)) return;
            if (o.Action == PressAction.Push && before != PressAction.Push)
            {
                if (!pm.EverPushed)
                {
                    pm.EverPushed = true;
                    _pressStats.PushedMen++;
                }
                if (!_firstPressPush)
                {
                    _firstPressPush = true;
                    TraxLog.Info("press", "first man PUSHED back out of an enemy line at " + Sec(now) + " s: " + Name(st) + " of " + PressUnitName(key) + ", " + F1(-d)
                        + " m past the front of " + PressUnitName(line) + " (" + PressFaceText(line, face) + ") - walked straight back out, facing it");
                }
            }
            else if (o.Action == PressAction.Return && before != PressAction.Return)
            {
                if (!pm.EverReturned)
                {
                    pm.EverReturned = true;
                    _pressStats.ReturnedMen++;
                }
                if (!_firstPressReturn)
                {
                    _firstPressReturn = true;
                    TraxLog.Info("press", "first man walked ROUND a line's end at " + Sec(now) + " s: " + Name(st) + " of " + PressUnitName(key) + ", " + F1(-d)
                        + " m behind the front of " + PressUnitName(line) + " (" + PressFaceText(line, face) + "), " + F1(l) + " m along it - toward " + F1(o.ToL)
                        + " (its " + (o.ToL > hi ? "high" : "low") + " end + " + F1(r.RoundEndMetres) + " m), then forward to his side");
                }
            }
            else if (o.Action == PressAction.Clamp && !_firstPressClamp)
            {
                _firstPressClamp = true;
                TraxLog.Info("press", "first man PRESSING an enemy line at " + Sec(now) + " s: " + Name(st) + " of " + PressUnitName(key) + ", " + F1(d) + " m in front of "
                    + PressUnitName(line) + " (" + PressFaceText(line, face) + "; " + PressMath.KindName(t.Kind[line]) + "), " + F1(l) + " m along it - his movement toward it stops at it"
                    + (o.Side != 0 ? "; a rear-rank man past its end - his sideways drift outward stopped" : "") + (unitWraps ? "; his unit is wider (" + F1(t.UnitWidth[key]) + " m)" : ""));
            }
            SteerPresser(st, pm, now);
        }

        /// <summary>A man was past a line's front by more than 1 m at this look and on its side at the last: counted by where, cause, allowed or not.</summary>
        private void NoteCrossing(TrackedAgent st, double now, int on, int line, int face, int key, double l, double lo, double hi, bool ring, PressAction action)
        {
            int where = ring || (l >= lo && l <= hi) ? 1 : 0;
            bool courageWalking = st.Input != null && st.Input.Regroup;
            bool fear = st.Fear.Wish;
            bool block = st.BlockHold.Wish;
            bool targetPast = false;
            if (!courageWalking && !fear && !block)
            {
                try
                {
                    if (FearBody.Target(st, out double tx, out double ty) != LoneTarget.None && !double.IsNaN(tx))
                    {
                        if (ring)
                        {
                            _pressLines.RingFrame(line, tx, ty, out double td, out _, out _);
                            targetPast = td < 0;
                        }
                        else
                        {
                            _pressLines.Frame(line, face, tx, ty, out double td, out _);
                            targetPast = td < 0;
                        }
                    }
                }
                catch (Exception e)
                {
                    Failed("press.target", e);
                }
            }
            var cause = PressMath.CauseOf(courageWalking, fear, block, targetPast);
            int allowedBy = action == PressAction.Through ? 0 : action == PressAction.Wrap ? 1 : action == PressAction.Broken || action == PressAction.Winning ? 2 : -1;
            _pressStats.Crossings[on, where, (int)cause, allowedBy >= 0 ? 1 : 0]++;
            if (allowedBy >= 0) _pressStats.AllowedBy[on, allowedBy]++;
            if (on == 1 ? _firstCrossOn : _firstCrossOff) return;
            if (on == 1) _firstCrossOn = true;
            else _firstCrossOff = true;
            TraxLog.Info("press", "first man CROSSED an enemy line's front with the press " + (on == 1 ? "ON" : "OFF") + " at " + Sec(now) + " s: " + Name(st) + " of " + PressUnitName(key)
                + " - " + (where == 1 ? "through the middle" : "round an end") + " of " + PressUnitName(line) + " (" + PressFaceText(line, face) + "), cause " + PressMath.CauseName(cause)
                + (allowedBy >= 0 ? ", allowed (" + PressMath.ActionName(action) + ")" : ", NOT allowed"));
        }

        // ------------------------------------------------------------------ the lever

        private bool StartPressInput(TrackedAgent st, PressMind pm, double now)
        {
            EnsureInput(st);
            AiInputHook.HookResult hook;
            try
            {
                hook = CourageBody.Hook(st);
            }
            catch (Exception e)
            {
                _pressStats.Errors++;
                Failed("press.hook", e);
                return false;
            }
            NoteHooked(st, hook);
            AiInputHook.SetPress(st, true, now);
            var s = st.Input!;
            pm.ClampAtStart = s.PressClampFrames;
            pm.SideAtStart = s.PressSideFrames;
            pm.MoveAtStart = s.PressMoveFrames;
            pm.Wish = true;
            pm.Moving = true;
            pm.Since = now;
            pm.NextSteer = now;
            pm.NextEdge = now + AiInputMath.EdgeCheckSeconds;
            _pressing.Add(st);
            _pressStats.Wishes++;
            if (!pm.EverPressed)
            {
                pm.EverPressed = true;
                _pressStats.Men++;
            }
            return true;
        }

        /// <summary>His input now: a walk (pushed out / round the end) toward its place in the line's frame, the stick slower near it, the ground ahead
        /// checked; once on his side - or for a pressing man - the clamp's direction into the line (his own frame) while he stands within 0.6 m of
        /// its front, a rear-rank man's outward sideways direction past its end.</summary>
        private void SteerPresser(TrackedAgent st, PressMind pm, double now)
        {
            pm.NextSteer = now + FearMath.SteerSeconds;
            var s = st.Input;
            if (s == null) return;
            var t = _pressLines;
            if (!t.IsLine(pm.Line, now))
            {
                EndPress(st, PressEnd.NoLine, now, native: true);
                return;
            }
            var body = CourageBody;
            var r = _pressRules;
            body.Position(st, out double px, out double py);
            double d, l = 0, nx, ny;
            if (pm.Ring) t.RingFrame(pm.Line, px, py, out d, out nx, out ny);
            else
            {
                t.Frame(pm.Line, pm.Face, px, py, out d, out l);
                t.Inward(pm.Line, pm.Face, out nx, out ny);
            }
            int at = 2 * pm.Line + pm.Face;
            if (pm.Action == PressAction.Push || pm.Action == PressAction.Return)
            {
                if (d >= 0)
                {
                    pm.Action = PressAction.Clamp; // back on his side: the clamp holds him there
                    pm.ClampFront = true;
                    pm.Side = 0;
                }
                else
                {
                    double tx, ty;
                    if (pm.Ring)
                    {
                        double outBy = PressMath.PushToMetres - d; // radially out to the ring + 0.3 m
                        tx = px - nx * outBy;
                        ty = py - ny * outBy;
                    }
                    else
                    {
                        if (pm.Action == PressAction.Return) PressMath.ReturnPlace(in r, d, l, t.Lo[at], t.Hi[at], t.Depth[pm.Line], out pm.ToD, out pm.ToL);
                        t.World(pm.Line, pm.Face, pm.ToD, pm.ToL, out tx, out ty);
                    }
                    double dx = tx - px, dy = ty - py, dist = Math.Sqrt(dx * dx + dy * dy);
                    s.PressFront = s.PressSide = false;
                    if (pm.Moving && dist <= FearMath.ArriveMetres) pm.Moving = false;
                    else if (!pm.Moving && dist > FearMath.MoveAgainMetres) pm.Moving = true;
                    if (pm.Moving && dist > 1e-6)
                    {
                        dx /= dist;
                        dy /= dist;
                        if (now >= pm.NextEdge)
                        {
                            pm.NextEdge = now + AiInputMath.EdgeCheckSeconds;
                            if (!body.GroundOk(st, dx, dy)) pm.Moving = false; // the ground ahead ends: he stands
                        }
                    }
                    if (pm.Moving && body.ToLocal(st, dx, dy, out float lx, out float ly))
                    {
                        float k = (float)FearMath.Stick(dist);
                        s.PressMove = true;
                        s.PressX = lx * k;
                        s.PressY = ly * k;
                        _pressStats.Steers++;
                    }
                    else
                    {
                        s.PressMove = true;
                        s.PressX = 0f;
                        s.PressY = 0f;
                        _pressStats.Stands++;
                    }
                    return;
                }
            }
            // the clamp
            s.PressMove = false;
            s.PressX = s.PressY = 0f;
            bool front = pm.ClampFront && d <= PressMath.LookaheadMetres;
            if (front && body.ToLocal(st, nx, ny, out float fx, out float fy))
            {
                Normalize(ref fx, ref fy);
                s.PressFront = true;
                s.PressNX = fx;
                s.PressNY = fy;
            }
            else s.PressFront = false;
            int side = 0;
            if (pm.Side != 0 && !pm.Ring)
            {
                if (l > t.Hi[at] + r.EndSlackMetres) side = 1;
                else if (l < t.Lo[at] - r.EndSlackMetres) side = -1;
            }
            if (side != 0)
            {
                t.Across(pm.Line, out double ex, out double ey);
                if (body.ToLocal(st, side * ex, side * ey, out float sx, out float sy))
                {
                    Normalize(ref sx, ref sy);
                    s.PressSide = true;
                    s.PressSX = sx;
                    s.PressSY = sy;
                }
                else s.PressSide = false;
            }
            else s.PressSide = false;
            _pressStats.Stands++;
        }

        private static void Normalize(ref float x, ref float y)
        {
            float len = (float)Math.Sqrt(x * x + y * y);
            if (len > 1e-6f)
            {
                x /= len;
                y /= len;
            }
        }

        private void EndPress(TrackedAgent st, PressEnd why, double now, bool native)
        {
            var pm = st.Press;
            if (!pm.Wish) return;
            if (!double.IsNaN(pm.Since) && now >= pm.Since) _pressStats.WishSeconds += now - pm.Since;
            _pressStats.Ends[(int)why]++;
            var s = st.Input;
            if (s != null)
            {
                _pressStats.ClampFrames += s.PressClampFrames - pm.ClampAtStart;
                _pressStats.SideFrames += s.PressSideFrames - pm.SideAtStart;
                _pressStats.MoveFrames += s.PressMoveFrames - pm.MoveAtStart;
            }
            pm.End();
            _pressing.Remove(st);
            if (s != null && s.Press)
            {
                AiInputHook.SetPress(st, false, now);
                if (native && !st.Removed)
                {
                    try
                    {
                        CourageBody.Release(st);
                    }
                    catch (Exception e)
                    {
                        Failed("press.release", e);
                    }
                }
                DrainInputError(st);
            }
        }

        private void SafeEndPress(TrackedAgent st, double now)
        {
            try
            {
                if (st.Press.Wish) EndPress(st, PressEnd.Error, now, native: true);
            }
            catch
            {
                st.Press.End();
                _pressing.Remove(st);
                if (st.Input != null) st.Input.Press = st.Input.PressFront = st.Input.PressSide = st.Input.PressMove = false;
            }
        }

        private void EndAllPress(PressEnd why, double now, bool native, string? because)
        {
            int ended = 0;
            for (int i = _pressing.Count - 1; i >= 0; i--)
            {
                if (i >= _pressing.Count) continue;
                var st = _pressing[i];
                try
                {
                    EndPress(st, why, now, native);
                    ended++;
                }
                catch (Exception e)
                {
                    Failed("press.end-all", e);
                    SafeEndPress(st, now);
                }
            }
            _pressing.Clear();
            if (why == PressEnd.SwitchedOff && ended > 0)
                TraxLog.Info("press", (because ?? "press, don't pass") + " switched OFF mid-mission at " + Sec(now) + " s: " + ended
                    + " men pressing / walked out of an enemy line let go at once - the game's own; the looks keep measuring");
        }

        private void PressLeftField(TrackedAgent st)
        {
            try
            {
                if (st.Press.Wish) EndPress(st, PressEnd.LeftField, SafeNow(), native: false);
            }
            catch (Exception e)
            {
                Failed("press.left", e);
            }
        }

        private void ClosePress(bool agentsAlive)
        {
            if (_pressClosed) return;
            _pressClosed = true;
            if (_pressing.Count > 0) EndAllPress(PressEnd.MissionEnd, SafeNow(), agentsAlive, null);
        }

        /// <summary>Step 77 part of the block hold's look: in contact the block's sideways range is cut to its enemy line's front ends (in the
        /// block's frame) + PressBlockEndMetres.</summary>
        private void PressBlockRange(int key, double now, double side, ref double latLo, ref double latHi)
        {
            var t = _pressLines;
            int line = t.LineOf(key, now);
            if (line < 0 || t.Kind[line] == PressLineKind.Ring) return;
            t.Ends(line, t.Face[key], out double ax, out double ay, out double bx, out double by);
            var bu = _blockUnits;
            double ex = -bu.UY[key], ey = bu.UX[key];
            double a = ax * ex + ay * ey - bu.CentreLat[key], b = bx * ex + by * ey - bu.CentreLat[key];
            var r = _pressRules;
            if (PressMath.BlockRange(in r, side, a, b, out double lo, out double hi))
            {
                latLo = lo;
                latHi = hi;
                _pressStats.BlockRangeLooks++;
            }
        }

        internal void WritePressSummary()
        {
            var lines = new List<string>(3);
            _pressStats.BandClamps = _fearUnits.PressBandClamps;
            _pressStats.BandExtraRanks = _fearUnits.PressBandExtraRanks;
            _pressStats.AddSummaryLines(lines, PressRules.From(TraxSettings.Shared), _pressLines);
            foreach (var line in lines) TraxLog.Info("summary", line);
        }
    }
}

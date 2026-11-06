using System;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 99 - THE ENDS OF A STANDING SHIELD WALL FACE THE FLANKS (Toni 2026-11-06: "can you make it so that troops on the two sides of the shield wall
    /// are facing the flanks not forward?"; AI_NOTES "Step 99", DESIGN decision 90). From each HOLD look of step 41 (right after vanilla's ~0.5 s update of
    /// the man and at the staggered look): a held man of a STANDING shield wall (step 75's test) among the outermost files of his rank is TURNED - his
    /// <see cref="WallState.Flank"/> set, so every frame write of ours (step 68's pace, step 75's pin, step 78's swap, the frame given back) sends HIS
    /// facing (<see cref="GameWallBody.FacingOf"/>), the frame written here when none of those did, the tendency 1 and the front shield pose. Released -
    /// the game's frame, tendency and shield back at once - by an enemy close in front, step 42's step out, no longer an end man, the wall moving / let
    /// go, the switch. No allocation.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly WallFlankStats _flank = new WallFlankStats();
        private WallFlankRules _seenFlank;
        private bool _firstFlankLogged, _firstFlankReleaseLogged, _firstFlankAngleLogged;

        /// <summary>This mission's wall-flank numbers (the offline smoke reads them).</summary>
        internal WallFlankStats WallFlankStats => _flank;

        private void StartFlanks()
        {
            _seenFlank = WallFlankRules.From(TraxSettings.Shared);
            if (_seenFlank.Enabled) _flank.EverOn = true;
            _flank.SampleSeconds = WallMath.SampleSeconds;
            TraxLog.Info("wall", "wall flanks (step 99): " + _seenFlank.Describe() + " - read live; the [summary] \"wall flanks\" line says whether the engine turned them");
        }

        /// <summary>From <see cref="TickWall"/>: a settings change logged (the men follow at their next look, within 0.25 s).</summary>
        private void TickFlankSettings(double now)
        {
            var r = WallFlankRules.From(TraxSettings.Shared);
            if (r.Enabled) _flank.EverOn = true;
            if (r.SameAs(in _seenFlank)) return;
            bool was = _seenFlank.Enabled;
            _seenFlank = r;
            if (was != r.Enabled) _flank.SwitchChanges++;
            TraxLog.Info("wall", "wall flanks (step 99) changed mid-mission at " + Sec(now) + " s: " + r.Describe() + " - the men follow within " + F2(WallMath.PollSeconds) + " s");
        }

        /// <summary>
        /// From a HOLD look (the flag just written), BEFORE the frame looks: the verdict for him now - turned (side and angle set) or released. A release
        /// gives vanilla's tendency and shield back here; the frame follows in <see cref="FlankFrame"/> (after the pace / pin had their say).
        /// </summary>
        private void FlankLook(TrackedAgent st, WallState ws, in WallFacts f, double now)
        {
            var r = WallFlankRules.From(TraxSettings.Shared);
            if (!ws.Flank.Turned && !WallFlankMath.MayTurn(in r, true, false, f.Arrangement, ws.FirmCond)) return; // the common case: no read at all
            try
            {
                var side = WallFlankSide.None;
                int rank = -1, files = -1;
                bool placed = false, front = false;
                if (WallFlankMath.MayTurn(in r, true, false, f.Arrangement, ws.FirmCond))
                {
                    placed = WallBody.ReadFlank(st, r.Files, out rank, out files, out int left, out int right);
                    if (placed) side = WallFlankMath.Side(left, right, r.Files);
                    if (placed && side != WallFlankSide.None && WallFlankMath.WideEnough(files, r.Files) && r.ReleaseMetres > 0 && f.HasPositions
                        && WallBody.TryEnemy(st, out double ex, out double ey) && WallFlankMath.EnemyInFront(in f, ex, ey, r.ReleaseMetres))
                    {
                        if (now >= ws.Flank.FrontUntil && ws.Flank.Turned) _flank.FrontHolds++;
                        ws.Flank.FrontUntil = now + WallFlankMath.FrontHoldSeconds;
                    }
                    front = now < ws.Flank.FrontUntil;
                }
                var v = WallFlankMath.Decide(in r, true, false, f.Arrangement, ws.FirmCond, placed, files, side, front);
                if (v != WallFlankVerdict.Turned)
                {
                    if (ws.Flank.Turned) FlankRelease(st, ws, v, now, engine: true);
                    return;
                }
                double deg = WallFlankMath.Degrees(in r, rank);
                if (!ws.Flank.Turned || ws.Flank.Side != side || ws.Flank.Degrees != deg)
                {
                    bool first = !ws.Flank.EverTurned;
                    bool begins = !ws.Flank.Turned;
                    ws.Flank.Turned = true;
                    ws.Flank.Side = side;
                    ws.Flank.Degrees = deg;
                    ws.Flank.Corner = rank == 0;
                    ws.Flank.EverTurned = true;
                    if (begins)
                    {
                        ws.Flank.Since = now;
                        _flank.Turn(first, rank == 0);
                    }
                    if (!_firstFlankLogged)
                    {
                        _firstFlankLogged = true;
                        TraxLog.Info("wall", "first END MAN TURNED to the flank this mission (step 99): " + Name(st) + " at " + Sec(now) + " s - the " + WallFlankMath.Name(side)
                            + " end of rank " + (rank + 1) + " of a standing shield wall " + files + " files wide, turned " + F1(deg) + " degrees outward"
                            + (rank == 0 ? " (the front corner)" : string.Empty) + " - his own frame direction, the direction-change tendency 1, the shield to the flank");
                    }
                }
                WallBody.Turn(st, true); // vanilla's update wrote its tendency and shield again
            }
            catch (Exception e)
            {
                _flank.Errors++;
                Failed("wall.flank", e);
                if (ws.Flank.Turned) FlankRelease(st, ws, WallFlankVerdict.NotHeld, now, engine: false);
            }
        }

        /// <summary>After the frame looks: a turned man whose frame none of ours wrote this look gets it here (the game's place, his facing); a man
        /// just released whose frame none wrote gets the game's frame back.</summary>
        private void FlankFrame(TrackedAgent st, WallState ws, bool released)
        {
            if (!ws.Flank.Turned && !released) return;
            try
            {
                if (!ws.Paced) WallBody.Unpace(st); // the game's place with HIS facing (the game's direction once released)
                if (ws.Flank.Turned) _flank.Writes++;
            }
            catch (Exception e)
            {
                _flank.Errors++;
                Failed("wall.flank-frame", e);
            }
        }

        /// <summary>He faces forward again: counted, the state cleared, and (<paramref name="engine"/>) vanilla's tendency and shield written back; the
        /// frame is the caller's (<see cref="FlankFrame"/>, or the let-go paths' own).</summary>
        private void FlankRelease(TrackedAgent st, WallState ws, WallFlankVerdict why, double now, bool engine)
        {
            if (!ws.Flank.Turned) return;
            ws.Flank.Turned = false;
            _flank.Release(why);
            _flankReleasedThisLook = true;
            if (engine && !st.Removed)
            {
                try
                {
                    WallBody.Turn(st, false);
                }
                catch (Exception e)
                {
                    _flank.Errors++;
                    Failed("wall.flank-release", e);
                }
            }
            if (!_firstFlankReleaseLogged)
            {
                _firstFlankReleaseLogged = true;
                TraxLog.Info("wall", "first turned end man FACES FORWARD again (step 99): " + Name(st) + " at " + Sec(now) + " s - " + WallFlankMath.Name(why)
                    + ", " + F1(now - ws.Flank.Since) + " s after he turned - the game's frame, tendency and shield direction given back");
            }
        }

        private bool _flankReleasedThisLook;

        /// <summary>A let-go path (not held, the switch, an error): released with the game's frame back at once (unless a let-go writes it).</summary>
        private void FlankLetGo(TrackedAgent st, WallState ws, WallFlankVerdict why, double now, bool engine)
        {
            if (!ws.Flank.Turned) return;
            FlankRelease(st, ws, why, now, engine);
            if (!engine || st.Removed || ws.Paced) return; // a paced man's Unpace follows in the caller (with the game's direction now)
            try
            {
                WallBody.Unpace(st);
            }
            catch (Exception e)
            {
                _flank.Errors++;
                Failed("wall.flank-frame", e);
            }
        }

        /// <summary>From the sample (every ~0.5 s): a settled turned man's real facing against the asked one - the engine's answer.</summary>
        private void FlankSample(TrackedAgent st, WallState ws, double now)
        {
            if (!ws.Flank.Turned) return;
            _flank.TurnedSamples++;
            if (now - ws.Flank.Since < WallFlankMath.SettleSeconds) return;
            try
            {
                if (!WallBody.TryLook(st, out double fx, out double fy, out double wx, out double wy)) return;
                WallFlankMath.Outward(wx, wy, ws.Flank.Side, ws.Flank.Degrees, out double tx, out double ty);
                double off = WallFlankMath.AngleDegrees(fx, fy, tx, ty), fromFront = WallFlankMath.AngleDegrees(fx, fy, wx, wy);
                _flank.AddAngle(ws.Flank.Corner, off, fromFront);
                if (!_firstFlankAngleLogged && !double.IsNaN(off))
                {
                    _firstFlankAngleLogged = true;
                    TraxLog.Info("wall", "first turned end man's facing measured (step 99): " + Name(st) + " at " + Sec(now) + " s, " + F1(now - ws.Flank.Since)
                        + " s after he turned " + F1(ws.Flank.Degrees) + " degrees " + WallFlankMath.Name(ws.Flank.Side) + " - his body faces " + F1(off)
                        + " degrees off the asked direction, " + F1(fromFront) + " degrees from the wall's front (near 0 off = the engine turns him)");
                }
            }
            catch (Exception e)
            {
                _flank.Errors++;
                Failed("wall.flank-look", e);
            }
        }

        private void WriteFlankSummary() => TraxLog.Info("summary", _flank.SummaryLine(WallFlankRules.From(TraxSettings.Shared)));
    }
}

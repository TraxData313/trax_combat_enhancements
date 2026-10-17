using System;
using System.Collections.Generic;
using System.Diagnostics;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 41 - THE SHIELD WALL HOLDS ITS SLOTS (Anton, 2026-10-17: a fresh shield wall waiting for the enemy - "men step 2-3 m
    /// forward out of the wall to swing at an enemy, then walk back to their slot ... every man does this, so the wall bulges and
    /// breaks"; AI_NOTES "Step 41", DESIGN §2 "The shield wall holds its slots", RESEARCH §O). Vanilla's
    /// <c>HumanAIComponent.ParallelUpdateFormationMovement</c> hands the engine, about every 0.5 s per AI man (his own staggered
    /// timer), the formation's integrity data with "keep with the formation instead of moving to the agent" FALSE for every
    /// shield-wall infantryman - so the native AI walks him at the enemy he targets. We write the same call with the flag TRUE for
    /// every man <see cref="WallMath.Decide"/> holds, from <see cref="TickWall"/> in OnPreMissionTick - the first managed point
    /// after the agent tick that ran vanilla's update: each held man is rewritten in the very next frame after vanilla's update
    /// (his timer moved), and a staggered look every <see cref="WallMath.PollSeconds"/> finds the men newly held or let go and
    /// re-asserts the rest (vanilla also writes at once on every order through the order controller). Off - by the switch or
    /// the master switch - writes VANILLA's own value back for every held man at once. The log samples every man of a holding
    /// shield wall / square / circle every <see cref="WallMath.SampleSeconds"/>, ON or OFF, for the [summary] "wall" lines.
    /// No allocation per tick (one <see cref="WallState"/> per man, once).
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly WallStats _wallStats = new WallStats();
        private readonly List<TrackedAgent> _walled = new List<TrackedAgent>(64);
        private WallRules _seenWall;
        private bool _wallOn;
        private bool _wallStarted;
        private bool _wallClosed;
        private bool _firstWallLogged;
        private bool _noTimerLogged;
        private int _wallCursor;
        private double _wallLastTick = double.NaN;
        private double _wallCarry;

        /// <summary>This mission's wall numbers (the offline smoke reads them).</summary>
        internal WallStats WallStats => _wallStats;

        /// <summary>The engine side of the hold (the offline smoke swaps in a stand-in).</summary>
        internal IWallBody WallBody { get; set; } = WallBodyDefault.Current;

        /// <summary>Men held in their slots right now.</summary>
        internal int WalledNow => _walled.Count;

        /// <summary>Mission start: the settings line, read live from now on.</summary>
        private void StartWall()
        {
            _seenWall = WallRules.From(TraxSettings.Shared);
            _wallOn = _seenWall.Enabled;
            _wallOffBecause = _seenWall.OffBecause;
            _wallStarted = true;
            TraxLog.Info("wall", "mission start: " + _seenWall.Describe() + " - read live; written right after vanilla's own update of each man (his ~0.5 s timer) and at a look every "
                + F2(WallMath.PollSeconds) + " s; the [summary] \"wall\" lines say how far the men of a holding shield wall / square / circle stood ahead of their slots, ON vs OFF");
        }

        /// <summary>
        /// Every pre-tick (OnPreMissionTick): switched off → every held man given vanilla's value at once; else every held man whose
        /// vanilla update ran since our write is rewritten now, and a slice of the fighters is looked at so each is seen about every
        /// <see cref="WallMath.PollSeconds"/> (a call after a longer gap - or the first - looks at everyone once). No allocation.
        /// </summary>
        internal void TickWall(double now)
        {
            if (!_wallStarted || _wallClosed) return;
            long start = Stopwatch.GetTimestamp();
            var r = WallRules.From(TraxSettings.Shared);
            if (!r.SameAs(in _seenWall))
            {
                bool wasOn = _seenWall.Enabled;
                _seenWall = r;
                if (wasOn != r.Enabled)
                {
                    _wallStats.SwitchChanges++;
                    if (!r.Enabled)
                    {
                        int released = ReleaseAllWalls(r.ModEnabled ? WallVerdict.SwitchedOff : WallVerdict.ModOff);
                        TraxLog.Info("wall", (r.ModEnabled ? "ShieldWallHoldsSlots" : "the whole mod (ModEnabled)") + " switched OFF mid-mission at " + Sec(now)
                            + " s: " + released + " men given vanilla's own value back at once - they may step out at their enemies again");
                    }
                    else
                    {
                        TraxLog.Info("wall", (_wallOffBecause == "ModEnabled" ? "the whole mod (ModEnabled)" : "ShieldWallHoldsSlots") + " switched ON mid-mission at " + Sec(now)
                            + " s: every man of a holding shield wall / square / circle is held again within " + F2(WallMath.PollSeconds) + " s");
                    }
                }
            }
            _wallOn = r.Enabled;
            _wallOffBecause = r.OffBecause;

            // 1. every frame: a held man whose vanilla update ran since our write (his timer moved) is rewritten now
            if (r.Enabled)
            {
                var body = WallBody;
                for (int i = _walled.Count - 1; i >= 0; i--)
                {
                    if (i >= _walled.Count) continue; // a look below may have dropped more than one
                    var st = _walled[i];
                    var ws = st.Wall!;
                    try
                    {
                        if (body.TryStamp(st, ws, out float stamp))
                        {
                            if (stamp == ws.LastStamp) continue;
                            double gap = double.IsNaN(ws.LastVanillaAt) ? double.NaN : now - ws.LastVanillaAt;
                            ws.LastVanillaAt = now;
                            _wallStats.AddVanillaUpdate(gap);
                            LookWall(st, ws, now, in r, WallWriteKind.AfterVanilla, sample: false);
                        }
                        else if (now >= ws.NextRewrite)
                        {
                            LookWall(st, ws, now, in r, WallWriteKind.Fallback, sample: false);
                        }
                    }
                    catch (Exception e)
                    {
                        WallFailed(st, ws, e);
                    }
                }
            }

            // 2. the staggered look: everyone about every PollSeconds (the gates, the newly held, the let go, the samples)
            double dt = double.IsNaN(_wallLastTick) || now < _wallLastTick ? WallMath.PollSeconds : now - _wallLastTick;
            _wallLastTick = now;
            if (dt > WallMath.PollSeconds) dt = WallMath.PollSeconds;
            _wallCarry += _count * dt / WallMath.PollSeconds;
            int n = (int)(_wallCarry + 1e-9);
            _wallCarry -= n;
            if (_wallCarry < 0) _wallCarry = 0;
            if (n > _count) n = _count;
            for (int k = 0; k < n; k++)
            {
                if (_wallCursor >= _count) _wallCursor = 0;
                var st = _dense[_wallCursor++];
                var ws = st.Wall;
                try
                {
                    if (st.Removed || !WallBody.IsActive(st)) continue;
                    if (ws == null)
                    {
                        ws = st.Wall = new WallState();
                        _wallStats.MenLooked++;
                    }
                    LookWall(st, ws, now, in r, WallWriteKind.Poll, sample: true);
                }
                catch (Exception e)
                {
                    WallFailed(st, ws, e);
                }
            }
            _wallStats.AddTick((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
        }

        private string? _wallOffBecause;

        /// <summary>One look at one man: his facts, the verdict, held (written now) or let go (vanilla's value back), and - from the
        /// staggered look - a sample of where he stands against his slot.</summary>
        private void LookWall(TrackedAgent st, WallState ws, double now, in WallRules r, WallWriteKind kind, bool sample)
        {
            var body = WallBody;
            var f = body.Read(st);
            if (IsPlayer(st)) f.Ai = false;
            var v = WallMath.Decide(in r, in f);
            _wallStats.AddLook(v);
            if (v == WallVerdict.Hold)
            {
                body.Hold(st, in f);
                _wallStats.AddWrite(kind);
                if (body.TryStamp(st, ws, out float stamp))
                {
                    ws.LastStamp = stamp; // vanilla's next update moves it again
                }
                else
                {
                    ws.NextRewrite = now + WallMath.FallbackRewriteSeconds;
                    if (!_noTimerLogged)
                    {
                        _noTimerLogged = true;
                        TraxLog.Info("wall", "vanilla's update timer (Agent." + GameWallBody.TimerFieldName + ") cannot be read on " + Name(st)
                            + " - a game update renamed it? Held men are rewritten every " + F2(WallMath.FallbackRewriteSeconds) + " s instead (tell Claude)");
                    }
                }
                if (!ws.Held)
                {
                    ws.Held = true;
                    ws.LastVanillaAt = double.NaN;
                    ws.ListIndex = _walled.Count;
                    _walled.Add(st);
                    if (!ws.EverHeld)
                    {
                        ws.EverHeld = true;
                        _wallStats.MenHeld++;
                        if (ws.Timer == null) _wallStats.MenWithoutTimer++;
                    }
                    if (!_firstWallLogged)
                    {
                        _firstWallLogged = true;
                        TraxLog.Info("wall", "first man held in his slot this mission: " + WallText(st, in f, now, ws));
                    }
                }
            }
            else if (ws.Held)
            {
                Unwall(st, ws);
                body.Release(st);
                _wallStats.AddRelease(v);
                if (TraxLog.VerboseWants("wall"))
                    TraxLog.Verbose("wall", Name(st) + " let go at " + Sec(now) + " s: " + WallMath.Name(v) + " - vanilla's own value given back", "wall");
            }

            if (!sample || now < ws.NextSample) return;
            ws.NextSample = now + WallMath.SampleSeconds - 0.5 * WallMath.PollSeconds; // every second look (a look comes every ~0.25 s, a hair early or late)
            if (!f.HasPositions || WallMath.Eligible(in f) != WallVerdict.Hold) return;
            _wallStats.AddSample(r.Enabled, WallMath.Ahead(in f), body.EnemyWithin(st, WallMath.ContactMetres));
        }

        /// <summary>Out of the held list (swap-remove, no allocation); no engine call.</summary>
        private void Unwall(TrackedAgent st, WallState ws)
        {
            ws.Held = false;
            int i = ws.ListIndex;
            ws.ListIndex = -1;
            if (i < 0 || i >= _walled.Count || !ReferenceEquals(_walled[i], st)) return;
            int last = _walled.Count - 1;
            if (i != last)
            {
                var moved = _walled[last];
                _walled[i] = moved;
                moved.Wall!.ListIndex = i;
            }
            _walled.RemoveAt(last);
        }

        /// <summary>Switched off: every held man given vanilla's own value at once (vanilla would not do it under a halt - it writes
        /// nothing there).</summary>
        private int ReleaseAllWalls(WallVerdict why)
        {
            int released = 0;
            for (int i = _walled.Count - 1; i >= 0; i--)
            {
                var st = _walled[i];
                var ws = st.Wall!;
                ws.Held = false;
                ws.ListIndex = -1;
                try
                {
                    if (!st.Removed && WallBody.IsActive(st))
                    {
                        WallBody.Release(st);
                        released++;
                    }
                }
                catch (Exception e)
                {
                    _wallStats.Errors++;
                    Failed("wall.release", e);
                }
                _wallStats.AddRelease(why);
            }
            _walled.Clear();
            return released;
        }

        /// <summary>He fell, fled or left the field: out of the list, no engine call (from <see cref="Forget"/>).</summary>
        private void WallLeftField(TrackedAgent st)
        {
            var ws = st.Wall;
            if (ws == null || !ws.Held) return;
            Unwall(st, ws);
            _wallStats.LeftFieldHeld++;
        }

        /// <summary>An exception on one man: logged once per site, counted, and he is let go (vanilla's value, best effort).</summary>
        private void WallFailed(TrackedAgent st, WallState? ws, Exception e)
        {
            _wallStats.Errors++;
            Failed("wall.look", e);
            if (ws == null || !ws.Held) return;
            Unwall(st, ws);
            try
            {
                if (!st.Removed) WallBody.Release(st);
            }
            catch
            {
                // already counted - stay safe
            }
        }

        /// <summary>Mission end: the men still held are counted; nothing written (the agents go with the mission).</summary>
        private void CloseWalls()
        {
            if (_wallClosed) return;
            _wallClosed = true;
            _wallStats.HeldAtEnd = _walled.Count;
            foreach (var st in _walled)
                if (st.Wall != null)
                {
                    st.Wall.Held = false;
                    st.Wall.ListIndex = -1;
                }
            _walled.Clear();
        }

        internal void WriteWallSummary()
        {
            CloseWalls();
            foreach (var line in _wallStats.SummaryLines(WallRules.From(TraxSettings.Shared)))
                TraxLog.Info("summary", line);
        }

        /// <summary>A held man in words: who, his formation and order, his slot, where he stands, the formation's numbers.</summary>
        private string WallText(TrackedAgent st, in WallFacts f, double now, WallState ws) =>
            Name(st) + " at " + Sec(now) + " s - his formation in a " + WallMath.Name(f.Arrangement) + " under the order " + BraceMath.Name(f.Order)
            + "; his slot (" + F1(f.SlotX) + ", " + F1(f.SlotY) + "), he stands " + F2(WallMath.DistanceToSlot(in f)) + " m from it (" + F2(WallMath.Ahead(in f))
            + " m ahead along the facing (" + F2(f.DirX) + ", " + F2(f.DirY) + ")); the formation's speed " + F2(f.AvgSpeed) + " m/s (far = "
            + F1(WallMath.CatchUpSpeeds * f.AvgSpeed) + " m), the spread of its men " + F2(f.Deviation) + " m - \"keep with the formation instead of moving to the agent\" written TRUE "
            + (ws.Timer != null ? "(vanilla's update timer read: he is rewritten right after each of vanilla's own updates)" : "(vanilla's update timer NOT readable: the fallback rewrites every " + F2(WallMath.FallbackRewriteSeconds) + " s)");
    }
}

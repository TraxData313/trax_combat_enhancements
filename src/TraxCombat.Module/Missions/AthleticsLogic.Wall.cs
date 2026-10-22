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
    /// Step 42 - THE LEASH (AI_NOTES "Step 42", <see cref="LeashMath"/>): with <c>ShieldWallStepOutMetres</c> L above 0 a held man
    /// whose enemy is close in front of his slot steps out - vanilla's own value given back, he stays in the held list but nothing is
    /// written - and is looked at every <see cref="LeashMath.LookSeconds"/> until he stands L out of his slot or his enemy is gone:
    /// then held again (written TRUE) and he walks back; back in his slot he may step again. L = 0 runs step 41's path exactly.
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
            if (_seenWall.Leash > 0) _wallStats.Leash.EverOn = true;
            _wallStarted = true;
            _seenWallPace = WallPaceRules.From(TraxSettings.Shared);
            _seenKickReturn = KickReturn(in _seenWall);
            if (_seenWallPace.Enabled) _wallStats.Pace.EverOn = true;
            StartStandFirm(); // step 75
            TraxLog.Info("wall", "mission start: " + _seenWall.Describe() + "; step 63: " + WallPullMath.Describe(PullSeconds(in _seenWall), _seenWall.StepOutMetres)
                + "; step 68: " + _seenWallPace.Describe() + "; " + KickReturnText(_seenKickReturn)
                + " - read live; written right after vanilla's own update of each man (his ~0.5 s timer) and at a look every "
                + F2(WallMath.PollSeconds) + " s; the [summary] \"wall\" lines say how far the men of a holding shield wall / square / circle stood ahead of their slots, ON vs OFF (and by order)");
        }

        private WallPaceRules _seenWallPace;
        private double _seenKickReturn;
        private bool _firstPaceLogged, _firstKickReturnLogged;

        /// <summary>Step 68: <c>ShieldWallKickReturnMetres</c> while the hold is on, else 0.</summary>
        private static double KickReturn(in WallRules r)
        {
            if (!r.Enabled) return 0;
            double m = TraxSettings.Shared.ShieldWallKickReturnMetres;
            return double.IsNaN(m) || m < 0 ? 0 : m;
        }

        private static string KickReturnText(double metres) => metres > 0
            ? "after his OWN kick / bash (or stepping in at a staggered enemy) a held man more than " + F2(metres) + " m ahead of his slot walks straight back to it as soon as he can move (ShieldWallKickReturnMetres)"
            : "no walk back after a kick (ShieldWallKickReturnMetres 0 - step 63's rule only)";

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
                double wasLeash = _seenWall.StepOutMetres;
                _seenWall = r;
                if (wasLeash != r.StepOutMetres)
                {
                    _wallStats.SettingsChanged = true;
                    TraxLog.Info("wall", "ShieldWallStepOutMetres " + F1(wasLeash) + " → " + F1(r.StepOutMetres) + " m mid-mission at " + Sec(now) + " s: "
                        + LeashMath.Describe(r.StepOutMetres) + " - a man stepping out now is looked at again within " + F2(LeashMath.LookSeconds) + " s"
                        + (r.Enabled ? string.Empty : " (the hold itself is off now - the leash waits for it)"));
                }
                if (wasOn != r.Enabled)
                {
                    _wallStats.SwitchChanges++;
                    if (!r.Enabled)
                    {
                        int released = ReleaseAllWalls(r.ModEnabled ? WallVerdict.SwitchedOff : WallVerdict.ModOff, now);
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
            if (r.Leash > 0) _wallStats.Leash.EverOn = true;
            var pr = WallPaceRules.From(TraxSettings.Shared); // step 68: the pace and the kick return, read live
            if (pr.Enabled) _wallStats.Pace.EverOn = true;
            if (!pr.SameAs(in _seenWallPace))
            {
                bool wasOn = _seenWallPace.Enabled;
                _seenWallPace = pr;
                int given = !pr.Enabled && wasOn ? UnpaceAll() : 0;
                TraxLog.Info("wall", "advance as one (step 68) changed mid-mission at " + Sec(now) + " s: " + pr.Describe() + (given > 0 ? " - " + given + " men given the game's own frame back at once" : string.Empty));
            }
            double kr = KickReturn(in r);
            if (kr != _seenKickReturn)
            {
                _seenKickReturn = kr;
                TraxLog.Info("wall", "the walk back after a kick (step 68) changed mid-mission at " + Sec(now) + " s: " + KickReturnText(kr));
            }

            TickStandFirmSettings(now); // step 75

            double dt = double.IsNaN(_wallLastTick) || now < _wallLastTick ? WallMath.PollSeconds : now - _wallLastTick;
            _wallLastTick = now;
            if (dt > WallMath.PollSeconds) dt = WallMath.PollSeconds;

            // 1. every frame: a held man whose vanilla update ran since our write (his timer moved) is rewritten now; a man stepping
            //    out (step 42) is looked at every LeashMath.LookSeconds instead - vanilla's own value is on him, nothing to rewrite
            if (r.Enabled)
            {
                _wallStats.Leash.HeldSeconds += dt * _walled.Count;
                var body = WallBody;
                bool outLooks = PullSeconds(in r) > 0 || KickReturn(in r) > 0 || _seenFirm.Enabled; // step 63: a man out of place looked at every 0.1 s (0 = step 42 exactly); step 68's kick return and step 75's stand firm too
                for (int i = _walled.Count - 1; i >= 0; i--)
                {
                    if (i >= _walled.Count) continue; // a look below may have dropped more than one
                    var st = _walled[i];
                    var ws = st.Wall!;
                    try
                    {
                        if (ws.Leash.Free)
                        {
                            if (now >= ws.NextLeashLook) LookWall(st, ws, now, in r, WallWriteKind.Leash, sample: false);
                            continue;
                        }
                        bool looked = false;
                        if (body.TryStamp(st, ws, out float stamp))
                        {
                            if (stamp != ws.LastStamp)
                            {
                                double gap = double.IsNaN(ws.LastVanillaAt) ? double.NaN : now - ws.LastVanillaAt;
                                ws.LastVanillaAt = now;
                                _wallStats.AddVanillaUpdate(gap);
                                LookWall(st, ws, now, in r, WallWriteKind.AfterVanilla, sample: false);
                                looked = true;
                            }
                        }
                        else if (now >= ws.NextRewrite)
                        {
                            LookWall(st, ws, now, in r, WallWriteKind.Fallback, sample: false);
                            looked = true;
                        }
                        // step 63: a man out of place - held again at his 0.1 s look (and walked back from it)
                        if (!looked && outLooks && ws.Out.Out && ws.Held && now >= ws.NextLeashLook) LookWall(st, ws, now, in r, WallWriteKind.OutOfPlace, sample: false);
                    }
                    catch (Exception e)
                    {
                        WallFailed(st, ws, e, now);
                    }
                }
            }

            // 2. the staggered look: everyone about every PollSeconds (the gates, the newly held, the let go, the samples)
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
                    WallFailed(st, ws, e, now);
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
            bool rhythm = RhythmWallSlot(st, ref f, now); // step 78: a swap's place / a lull's slot shifted back
            var v = WallMath.Decide(in r, in f);
            if (v == WallVerdict.Hold && FearWallBreaks(st, now)) v = WallVerdict.Breaking; // step 67: a frightened wall breaks
            _wallStats.AddLook(v);
            bool firm = StandFirmLook(st, ws, in f, v, now); // step 75: a standing wall stands firm
            if (v == WallVerdict.Hold)
            {
                // step 42: the leash - only while it is above 0 or a man is still out (at 0 this is step 41's path exactly: no read); 0 while he stands firm (step 75)
                double leash = StandFirmMath.Leash(firm, r.StepOutMetres);
                double outNow = f.HasPositions ? LeashMath.Out(in f) : double.NaN; // step 63: out of place?
                ws.FirmOut = outNow;
                if (firm) FirmWouldStep(st, ws, in f, r.StepOutMetres, now); // step 75: the steps refused
                if (leash > 0 || ws.Leash.Free)
                {
                    double outM = f.HasPositions ? LeashMath.Out(in f) : 0;
                    bool close = false;
                    double enemyOut = double.NaN;
                    if (f.HasPositions && LeashMath.NeedsEnemy(in ws.Leash, leash, outM) && body.TryEnemy(st, out double ex, out double ey))
                    {
                        close = LeashMath.EnemyCloseInFront(in f, ex, ey, leash, ws.Leash.Free);
                        enemyOut = LeashMath.OutOf(in f, ex, ey);
                    }
                    var before = ws.Leash;
                    var move = LeashMath.Step(ref ws.Leash, leash, outM, close, now);
                    if (move != LeashMove.None) NoteLeash(st, ws, move, in before, outM, enemyOut, leash, now);
                    if (ws.Leash.Free)
                    {
                        // stepping out: vanilla's own value is his (given back now if ours was on him) - the native AI may close on
                        // his enemy; nothing is written until the leash or his enemy ends the step
                        ws.NextLeashLook = now + LeashMath.LookSeconds;
                        if (move == LeashMove.Freed && ws.Held) body.Release(st);
                        if (move == LeashMove.Freed) EndOutOfPlace(st, ws, WallOutEnd.LetGo, now, engine: true); // step 63: the leash has him now
                        if (!ws.Held) Manage(st, ws);
                        PaceLook(st, ws, in f, now); // step 68: a man stepping out of a moving wall keeps the paced frame (the game's would send him on)
                        goto Sample;
                    }
                }

                if (firm)
                {
                    var hf = f; // step 75: the tolerance the engine is told - at most the slack
                    hf.Deviation = FirmTolerance(in f);
                    body.Hold(st, in hf);
                }
                else
                {
                    body.Hold(st, in f);
                }
                _wallStats.AddWrite(kind);
                if (firm) FirmFrameLook(st, ws, in f, now); // step 75: the engine's walk target = his slot
                else if (rhythm) RhythmFrameLook(st, ws, in f); // step 78: the engine's walk target = the rhythm's slot
                else PaceLook(st, ws, in f, now); // step 68: a moving wall walks as one
                OutOfPlaceLook(st, ws, in f, outNow, now, in r, firm ? _seenFirm.SlackMetres : 0); // step 63 (step 75: from the slack, at once)
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
                if (!ws.Held) Manage(st, ws);
                if (!_firstWallLogged)
                {
                    _firstWallLogged = true;
                    TraxLog.Info("wall", "first man held in his slot this mission: " + WallText(st, in f, now, ws));
                }
            }
            else if (ws.Held)
            {
                if (ws.Leash.Free) EndStep(st, ws, LeashEnd.LetGo, ws.Leash.MaxOut, now, r.StepOutMetres);
                EndOutOfPlace(st, ws, WallOutEnd.LetGo, now, engine: true); // step 63
                ws.Leash = default;
                Unwall(st, ws);
                body.Release(st);
                if (ws.Paced) Unpace(st, ws); // step 68: the game's own frame back (a charge order: the game's charge)
                _wallStats.AddRelease(v);
                if (v == WallVerdict.Breaking) NoteWallBreakMan(st, now); // step 67
                if (TraxLog.VerboseWants("wall"))
                    TraxLog.Verbose("wall", Name(st) + " let go at " + Sec(now) + " s: " + WallMath.Name(v) + " - vanilla's own value given back", "wall");
            }

            Sample:
            if (!sample || now < ws.NextSample) return;
            ws.NextSample = now + WallMath.SampleSeconds - 0.5 * WallMath.PollSeconds; // every second look (a look comes every ~0.25 s, a hair early or late)
            if (!f.HasPositions || WallMath.Eligible(in f) != WallVerdict.Hold) return;
            bool contact = body.EnemyWithin(st, WallMath.ContactMetres);
            double ahead = WallMath.Ahead(in f);
            _wallStats.AddSample(r.Enabled, ahead, contact);
            if (r.Enabled) _wallStats.Pace.AddSample(f.Order, ahead, contact); // step 68: the ON samples by order group
            if (contact && r.Leash > 0) _wallStats.Leash.AddSample(LeashMath.Out(in f), r.Leash); // step 42: past the leash?
            if (ws.FirmCond) _firm.AddSample(ws.Firm, LeashMath.Out(in f), contact); // step 75: the same men ON vs OFF
        }

        /// <summary>Into the held list (held, or stepping out on the leash) - his first time counted.</summary>
        private void Manage(TrackedAgent st, WallState ws)
        {
            if (!ws.TimerLooked) WallBody.TryStamp(st, ws, out _); // a man first seen stepping out: is his timer readable?
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
        }

        /// <summary>Step 42: a leash move - counted, the first step and the first end in full, the rest verbose.</summary>
        private void NoteLeash(TrackedAgent st, WallState ws, LeashMove move, in WallLeash before, double outM, double enemyOut, double leash, double now)
        {
            var ls = _wallStats.Leash;
            switch (move)
            {
                case LeashMove.Freed:
                    ls.AddStart();
                    ws.LastVanillaAt = double.NaN; // vanilla's updates are not followed while he is out
                    if (!ws.EverStepped)
                    {
                        ws.EverStepped = true;
                        ls.MenStepped++;
                    }
                    if (!_firstStepLogged)
                    {
                        _firstStepLogged = true;
                        TraxLog.Info("wall", "first step out of the wall this mission: " + Name(st) + " at " + Sec(now) + " s - his enemy " + F2(enemyOut)
                            + " m out of his slot (close: within the leash " + F1(leash) + " + " + F1(LeashMath.StrikeReachMetres) + " m of the slot), he stood "
                            + F2(outM) + " m out; vanilla's own value given for the step (the native AI may close on his enemy) - looked at every "
                            + F2(LeashMath.LookSeconds) + " s, held again at " + F1(leash) + " m out of his slot or when no enemy is close");
                    }
                    else if (TraxLog.VerboseWants("wall-step"))
                    {
                        TraxLog.Verbose("wall", Name(st) + " steps out at " + Sec(now) + " s: his enemy " + F2(enemyOut) + " m out of his slot, he " + F2(outM) + " m", "wall-step");
                    }
                    break;
                case LeashMove.Reined:
                    EndStep(st, ws, LeashEnd.ReachedLeash, Math.Max(before.MaxOut, outM), now, leash, before.StartedAt);
                    break;
                case LeashMove.EnemyGone:
                    EndStep(st, ws, LeashEnd.EnemyGone, Math.Max(before.MaxOut, outM), now, leash, before.StartedAt);
                    break;
                case LeashMove.LeashOff:
                    EndStep(st, ws, LeashEnd.LeashOff, Math.Max(before.MaxOut, outM), now, leash, before.StartedAt);
                    break;
                case LeashMove.BackInSlot:
                    ls.BackInSlot++;
                    break;
            }
        }

        /// <summary>Step 42: a step out ended - counted; the first one in full.</summary>
        private void EndStep(TrackedAgent st, WallState ws, LeashEnd how, double maxOut, double now, double leash, double startedAt = double.NaN)
        {
            if (double.IsNaN(startedAt)) startedAt = ws.Leash.StartedAt;
            double seconds = double.IsNaN(now) ? 0 : now - startedAt;
            _wallStats.Leash.AddEnd(how, maxOut, seconds, leash);
            if (!_firstStepEndLogged)
            {
                _firstStepEndLogged = true;
                TraxLog.Info("wall", "first step out ended: " + Name(st) + " at " + Sec(now) + " s - " + LeashMath.Name(how) + ": the farthest " + F2(maxOut)
                    + " m out of his slot (the leash " + F1(leash) + " m" + (how == LeashEnd.ReachedLeash ? ", " + F2(Math.Max(0, maxOut - leash)) + " m past it" : string.Empty)
                    + "), " + F2(seconds) + " s after it began"
                    + (how == LeashEnd.ReachedLeash || how == LeashEnd.EnemyGone || how == LeashEnd.LeashOff ? " - held again (\"keep your slot\" written TRUE), he walks back" : " - left to vanilla"));
            }
            else if (TraxLog.VerboseWants("wall-step"))
            {
                TraxLog.Verbose("wall", Name(st) + " step out ended at " + Sec(now) + " s: " + LeashMath.Name(how) + ", the farthest " + F2(maxOut) + " m, " + F2(seconds) + " s", "wall-step");
            }
        }

        private bool _firstStepLogged;
        private bool _firstStepEndLogged;

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
        private int ReleaseAllWalls(WallVerdict why, double now)
        {
            int released = 0;
            for (int i = _walled.Count - 1; i >= 0; i--)
            {
                var st = _walled[i];
                var ws = st.Wall!;
                ws.Held = false;
                ws.ListIndex = -1;
                FirmForget(ws); // step 75
                if (ws.Leash.Free) EndStep(st, ws, LeashEnd.LetGo, ws.Leash.MaxOut, now, _seenWall.StepOutMetres);
                ws.Leash = default;
                EndOutOfPlace(st, ws, WallOutEnd.LetGo, now, engine: !st.Removed); // step 63: the walk back's wish off at once
                try
                {
                    if (!st.Removed && WallBody.IsActive(st))
                    {
                        WallBody.Release(st);
                        if (ws.Paced) Unpace(st, ws); // step 68
                        released++;
                    }
                    ws.Paced = false;
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
            if (ws.Leash.Free) EndStep(st, ws, LeashEnd.LeftField, ws.Leash.MaxOut, _wallLastTick, _seenWall.StepOutMetres);
            ws.Leash = default;
            EndOutOfPlace(st, ws, WallOutEnd.LeftField, _wallLastTick, engine: false); // step 63: no engine call for a man gone
            ws.Paced = false; // step 68: no engine call either
            FirmForget(ws); // step 75
            Unwall(st, ws);
            _wallStats.LeftFieldHeld++;
        }

        /// <summary>An exception on one man: logged once per site, counted, and he is let go (vanilla's value, best effort).</summary>
        private void WallFailed(TrackedAgent st, WallState? ws, Exception e, double now)
        {
            _wallStats.Errors++;
            Failed("wall.look", e);
            if (ws == null || !ws.Held) return;
            FirmForget(ws); // step 75
            if (ws.Leash.Free) _wallStats.Leash.AddEnd(LeashEnd.LetGo, ws.Leash.MaxOut, now - ws.Leash.StartedAt, _seenWall.StepOutMetres);
            ws.Leash = default;
            try
            {
                EndOutOfPlace(st, ws, WallOutEnd.LetGo, now, engine: !st.Removed); // step 63
            }
            catch
            {
                // already counted - stay safe
            }
            Unwall(st, ws);
            try
            {
                if (!st.Removed) WallBody.Release(st);
                if (!st.Removed && ws.Paced) WallBody.Unpace(st); // step 68
            }
            catch
            {
                // already counted - stay safe
            }
            ws.Paced = false;
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
                    if (st.Wall.Leash.Free)
                        _wallStats.Leash.AddEnd(LeashEnd.StillOut, st.Wall.Leash.MaxOut, _wallLastTick - st.Wall.Leash.StartedAt, _seenWall.StepOutMetres);
                    st.Wall.Leash = default;
                    if (st.Wall.Out.Out) // step 63: still out at the end - the wish dropped with the mission (no engine call)
                    {
                        st.Wall.Out.Out = false;
                        _wallStats.Pull.Ended(in st.Wall.Out, WallOutEnd.StillOut, _wallLastTick);
                        if (st.Wall.Out.Pulling && st.Input != null) st.Input.WallPull = false;
                        st.Wall.Out.Pulling = false;
                    }
                    st.Wall.Held = false;
                    st.Wall.ListIndex = -1;
                    st.Wall.Paced = false; // step 68: no engine call - the agents go with the mission
                    FirmForget(st.Wall); // step 75
                }
            _walled.Clear();
        }

        internal void WriteWallSummary()
        {
            CloseWalls();
            foreach (var line in _wallStats.SummaryLines(WallRules.From(TraxSettings.Shared), TraxSettings.Shared.ShieldWallPullBackSeconds))
                TraxLog.Info("summary", line);
            TraxLog.Info("summary", _wallStats.Pace.ByOrderLine()); // step 68: the in-contact leak by order
            TraxLog.Info("summary", _wallStats.Pace.PaceLine(WallPaceRules.From(TraxSettings.Shared)));
            WriteStandFirmSummary(); // step 75
        }

        // ------------------------------------------------------------------ step 68: the wall pushes as one

        /// <summary>
        /// Step 68, from every write on a held man (and each 0.1 s look of one stepping out): a man of a MOVING order (advance, the AI's advance, move,
        /// fall back, follow) near the enemy is sent to his slot in the line as it stands + at most <c>ShieldWallAdvanceStepMetres</c> toward the game's
        /// order slot (<see cref="WallPaceMath.Pace"/>) - the frame the engine walks him to - instead of the game's order slot at the enemy's line; any
        /// other case gives the game's own frame back if ours was on him. The game's next ~0.5 s update rewrites its own; the write right after it
        /// (step 41's timing) and the staggered look put ours back within a frame.
        /// </summary>
        private void PaceLook(TrackedAgent st, WallState ws, in WallFacts f, double now)
        {
            var pr = WallPaceRules.From(TraxSettings.Shared);
            var ps = _wallStats.Pace;
            try
            {
                if (WallPaceMath.Pace(in pr, in f, out double px, out double py))
                {
                    if (!WallBody.Pace(st, px, py))
                    {
                        ps.Skip(WallPaceSkip.NoOrderSlot);
                        return;
                    }
                    ws.Paced = true;
                    if (!ws.EverPaced)
                    {
                        ws.EverPaced = true;
                        ps.MenPaced++;
                    }
                    double lead = WallPaceMath.OrderLead(in f);
                    ps.Paced(WallPaceMath.GroupOf(f.Order), lead, f.EnemyGap);
                    if (!_firstPaceLogged)
                    {
                        _firstPaceLogged = true;
                        TraxLog.Info("wall", "first man of a moving wall sent AS ONE LINE (step 68): " + Name(st) + " at " + Sec(now) + " s - his formation in a " + WallMath.Name(f.Arrangement)
                            + " under the order " + BraceMath.Name(f.Order) + "; the game's order slot lay " + F2(lead) + " m from his slot in the line (the enemy's front "
                            + F1(f.EnemyGap) + " m away) - sent " + F2(pr.StepMetres) + " m toward it instead (ShieldWallAdvanceStepMetres), the line's own pace");
                    }
                    return;
                }
                if (WallPaceMath.Paces(f.Order)) ps.Skip(WallPaceMath.Why(in pr, in f));
                if (ws.Paced) Unpace(st, ws);
            }
            catch (Exception e)
            {
                ps.Errors++;
                Failed("wall.pace", e);
                ws.Paced = false;
            }
        }

        /// <summary>Step 68: the game's own frame back on him now (counted).</summary>
        private void Unpace(TrackedAgent st, WallState ws)
        {
            ws.Paced = false;
            _wallStats.Pace.Restored++;
            try
            {
                if (!st.Removed) WallBody.Unpace(st);
            }
            catch (Exception e)
            {
                _wallStats.Pace.Errors++;
                Failed("wall.unpace", e);
            }
        }

        /// <summary>Step 68 switched off (or the hold / the master switch): every paced held man gets the game's frame back at once.</summary>
        private int UnpaceAll()
        {
            int n = 0;
            foreach (var st in _walled)
            {
                var ws = st.Wall;
                if (ws == null || !ws.Paced) continue;
                Unpace(st, ws);
                n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ step 63: the wall pulls men back

        /// <summary>Step 63: <c>ShieldWallPullBackSeconds</c> while the hold is on, else 0.</summary>
        private static double PullSeconds(in WallRules r)
        {
            if (!r.Enabled) return 0;
            double s = TraxSettings.Shared.ShieldWallPullBackSeconds;
            return double.IsNaN(s) || s < 0 ? 0 : s;
        }

        private bool _firstOutLogged, _firstPullLogged, _firstBackLogged;

        /// <summary>
        /// Step 63, from a HOLD look (the flag just written): is he out of place - more than <see cref="WallPullMath.Beyond"/> ahead of his
        /// slot - and, once that has lasted <c>ShieldWallPullBackSeconds</c> while he can move (not down, not reeling from an impact), walk
        /// him back: a backpedal toward his slot through step 16's input hook, the vector refreshed at each 0.1 s look. Measured always (the
        /// summary's "wall pulled back" line); the extra looks and the walk only with the setting above 0.
        /// </summary>
        private void OutOfPlaceLook(TrackedAgent st, WallState ws, in WallFacts f, double outM, double now, in WallRules r, double firmSlack)
        {
            double dt = double.IsNaN(ws.LastOutLook) || now < ws.LastOutLook ? 0 : now - ws.LastOutLook;
            ws.LastOutLook = now;
            if (double.IsNaN(outM)) return;
            double pull = PullSeconds(in r);
            var imp = st.Impact;
            bool down = imp.Down(now), reeling = imp.Reeling(now);
            bool impact = down || reeling || imp.Recent(now, WallPullMath.ImpactWindowSeconds) > 0;
            bool wasPulling = ws.Out.Pulling;
            // step 68: his OWN kick / bash (running, or begun within the window) or a punish press (step 54's step-in) - walked back at once from
            // ShieldWallKickReturnMetres; never walked while his kick plays (its input would be fought)
            bool kicking = st.KickBash.Running || st.AiKickPhase != AiKickPhase.None;
            bool ownKick = kicking || now - st.KickBash.DecidedAt <= WallPullMath.KickWindowSeconds || st.Punishing;
            double kickReturn = KickReturn(in r);
            var move = WallPullMath.Step(ref ws.Out, r.StepOutMetres, outM, !down && !reeling && !kicking, impact, pull, now, dt, kickReturn, ownKick, firmSlack);
            switch (move)
            {
                case WallOutMove.Began:
                    _wallStats.Pull.Began(impact, ws.Out.AfterOwnKick);
                    if (ws.Out.StandFirm) FirmSpellBegan(st, ws, outM, impact, now); // step 75
                    if (ws.Out.AfterOwnKick && !_firstKickReturnLogged)
                    {
                        _firstKickReturnLogged = true;
                        TraxLog.Info("wall", "first held man out of place after his OWN kick / bash or a step-in (step 68): " + Name(st) + " at " + Sec(now) + " s stands " + F2(outM)
                            + " m ahead of his slot (more than " + F2(kickReturn) + " m) - walked straight back as soon as he can move (ShieldWallKickReturnMetres)");
                    }
                    if (!_firstOutLogged)
                    {
                        _firstOutLogged = true;
                        TraxLog.Info("wall", "first man OUT OF PLACE this mission (step 63): " + Name(st) + " at " + Sec(now) + " s stands " + F2(outM) + " m ahead of his slot (beyond "
                            + F2(WallPullMath.Beyond(r.StepOutMetres)) + " m)" + (impact ? " - after an impact (" + (down ? "down" : reeling ? "reeling" : "an outcome within " + F1(WallPullMath.ImpactWindowSeconds) + " s") + ")" : string.Empty)
                            + "; held again, looked at every " + F2(LeashMath.LookSeconds) + " s" + (pull > 0 ? ", walked back if still out after " + F1(pull) + " s able to move" : " (ShieldWallPullBackSeconds 0: the game's pull alone)"));
                    }
                    break;
                case WallOutMove.Back:
                    _wallStats.Pull.Ended(in ws.Out, WallOutEnd.Back, now);
                    if (ws.Out.StandFirm) FirmSpellBack(st, ws, now); // step 75
                    if (wasPulling) StopWallPull(st, now, engine: true);
                    if (!_firstBackLogged)
                    {
                        _firstBackLogged = true;
                        TraxLog.Info("wall", "first man back in place (step 63): " + Name(st) + " at " + Sec(now) + " s, " + F2(now - ws.Out.Since) + " s after he stood out (the farthest "
                            + F2(ws.Out.MaxOut) + " m)" + (ws.Out.Pulled ? " - walked back by us" : " - by the game's flag alone"));
                    }
                    break;
                case WallOutMove.PullStarted:
                    if (!StartWallPull(st, ws, in f, now))
                    {
                        ws.Out.Pulling = false; // not hooked = no walk (the flag pulls him)
                        ws.Out.PullSeconds = WallPullMath.MaxPullSeconds;
                    }
                    break;
                case WallOutMove.PullStopped:
                    StopWallPull(st, now, engine: true);
                    break;
            }
            if (!ws.Out.Out) return;
            ws.NextLeashLook = now + LeashMath.LookSeconds;
            if (ws.Out.Pulling) SteerWallPull(st, in f);
        }

        /// <summary>The walk back's wish on: the input hook, the vector.</summary>
        private bool StartWallPull(TrackedAgent st, WallState ws, in WallFacts f, double now)
        {
            EnsureInput(st);
            try
            {
                NoteHooked(st, WallBody.Hook(st));
            }
            catch (Exception e)
            {
                Failed("wall.pull-hook", e);
                return false;
            }
            AiInputHook.SetWallPull(st, true, now);
            _wallStats.Pull.Pulls++;
            SteerWallPull(st, in f);
            if (!_firstPullLogged)
            {
                _firstPullLogged = true;
                TraxLog.Info("wall", "first man WALKED BACK to his slot (step 63): " + Name(st) + " at " + Sec(now) + " s, out of place " + F2(now - ws.Out.Since) + " s, "
                    + F2(LeashMath.Out(in f)) + " m ahead - a backpedal toward his slot, facing the enemy (step 16's input hook), until within "
                    + F2(WallPullMath.BackAt(_seenWall.StepOutMetres)) + " m (at most " + F1(WallPullMath.MaxPullSeconds) + " s)");
            }
            return true;
        }

        /// <summary>The vector toward his slot in his own frame (from the look's facts).</summary>
        private void SteerWallPull(TrackedAgent st, in WallFacts f)
        {
            var s = st.Input;
            if (s == null || !s.WallPull) return;
            double dx = f.SlotX - f.AgentX, dy = f.SlotY - f.AgentY;
            if (WallBody.ToLocal(st, dx, dy, out float lx, out float ly))
            {
                s.WallPullX = lx;
                s.WallPullY = ly;
            }
        }

        /// <summary>The walk back's wish off (the callback released if he is idle - never for a man gone).</summary>
        private void StopWallPull(TrackedAgent st, double now, bool engine)
        {
            var s = st.Input;
            if (s == null || !s.WallPull) return;
            _wallStats.Pull.PullFrames += (int)Math.Min(int.MaxValue, s.WallPullFrames);
            s.WallPullFrames = 0;
            AiInputHook.SetWallPull(st, false, now);
            if (engine && !st.Removed)
            {
                try
                {
                    WallBody.ReleaseInput(st);
                }
                catch (Exception e)
                {
                    Failed("wall.pull-release", e);
                }
            }
            DrainInputError(st);
        }

        /// <summary>A spell out of place ended by a let-go path (freed on the leash, a gate, the switch, he left) - counted, the wish off.</summary>
        private void EndOutOfPlace(TrackedAgent st, WallState ws, WallOutEnd how, double now, bool engine)
        {
            if (!ws.Out.Out) return;
            bool pulling = ws.Out.Pulling;
            ws.Out.Out = false;
            ws.Out.Pulling = false;
            _wallStats.Pull.Ended(in ws.Out, how, now);
            if (pulling) StopWallPull(st, now, engine);
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

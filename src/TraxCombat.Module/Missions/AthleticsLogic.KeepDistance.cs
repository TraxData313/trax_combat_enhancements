using System;
using System.Collections.Generic;
using System.Diagnostics;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 109 - KEEP YOUR DISTANCE (Toni 2026-11-10: "when they are tired, they come as close to the enemy as they can. Instead, if they are not in a
    /// shield wall, while they wait to gather Athletics, let him stay far ... after 2-3 seconds of no action, let the fighters back off. If I come while he
    /// is gathering Athletics, he shouldn't just start backing up again; he stands there and defends like normal"; AI_NOTES "Step 109", DESIGN decision 98).
    /// Its own staggered look (every AI man about every 0.25 s - tournaments and duels too, where courage never looks) and its own input wish (the softest
    /// movement wish of step 16's component): a man on foot who CANNOT ATTACK now (his step-13 pause, an empty step-32 stack, a step-23 brace), not held by
    /// a shield wall, not routing, not resting (step 78c's rest wins while it runs), whose enemy stands within reach, rolls his own quiet wait (2-3 s) and,
    /// once it passes with no swing, kick, bash, block or hit between them, walks straight back from that enemy facing him (a walk) for 2 of his own weapon's
    /// lengths (capped; never past the rest's limit behind his own front), then STANDS at that gap (his own walk toward the enemy taken out) until he can
    /// attack again. Any action ends it; the enemy stepping in only restarts his wait (he stands and defends). The master switch FIRST, then Athletics, then
    /// KeepDistanceEnabled - all read live; off ends every keep at once.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly KeepDistanceStats _keepStats = new KeepDistanceStats();
        private readonly List<TrackedAgent> _keeping = new List<TrackedAgent>(32);
        private KeepDistanceRules _keepRules = new KeepDistanceRules(false, false, false);
        private string _seenKeepText = string.Empty;
        private int _keepCursor;
        private double _keepLastTick = double.NaN, _keepCarry;
        private bool _keepClosed, _firstKeepTired, _firstKeep, _firstKeepHold, _firstKeepEnd, _firstKeepReady, _firstKeepStepIn, _firstKeepOff, _firstKeepAgain;

        internal KeepDistanceStats KeepStats => _keepStats;

        /// <summary>Men keeping their distance now.</summary>
        internal int KeepingNow => _keeping.Count;

        /// <summary>The engine side of the look (the offline smoke swaps in a stand-in).</summary>
        internal IKeepBody KeepBody { get; set; } = KeepBodyDefault.Current;

        /// <summary>The dice for each man's quiet wait (the offline smoke seeds it).</summary>
        internal IRandomSource KeepDice { get; set; } = ThreadSafeRandom.Shared;

        private void StartKeep()
        {
            _keepRules = KeepDistanceRules.From(TraxSettings.Shared);
            _seenKeepText = _keepRules.Describe();
            TraxLog.Info("keep", "mission start: " + _seenKeepText + " - read live; every AI man looked at about every " + F2(KeepDistanceMath.PollSeconds)
                + " s; the [keep] lines log the first back-off, hold, step-in and end; the [summary] \"keep distance (step 109)\" line");
        }

        private void NoteKeepSettings(TraxSettings s)
        {
            string now = KeepDistanceRules.From(s).Describe();
            if (now == _seenKeepText) return;
            _seenKeepText = now;
            TraxLog.Info("keep", "settings changed mid-mission at " + Sec(SafeNow()) + " s: " + now + " - at every man's next look");
        }

        // ------------------------------------------------------------------ the tick

        /// <summary>Every tick: switched off → every keep ends at once; then a slice of the men looked at (each about every 0.25 s - ON and OFF, the OFF
        /// looks measure) and, while on, the keeping men steered every 0.1 s. No allocation.</summary>
        internal void TickKeep(double now)
        {
            var settings = TraxSettings.Shared;
            var r = KeepDistanceRules.From(settings);
            bool was = _keepRules.On;
            _keepRules = r;
            if (!r.On && _keeping.Count > 0)
            {
                int n0 = EndAllKeeps(KeepEnd.SwitchedOff, now, native: true);
                if (was) TraxLog.Info("keep", "keep your distance switched OFF at " + Sec(now) + " s (" + r.OffBecause + "): " + n0 + " keeps ended at once");
            }
            if (!r.ModEnabled || !r.AthleticsEnabled || _keepClosed)
            {
                _keepLastTick = now;
                return; // no bars, no pauses - nothing to keep away from, nothing to measure
            }
            var ar = Rules;
            var sr = SwingStackRules.From(settings);
            double dt = double.IsNaN(_keepLastTick) || now < _keepLastTick ? KeepDistanceMath.PollSeconds : now - _keepLastTick;
            _keepLastTick = now;
            if (dt > KeepDistanceMath.PollSeconds) dt = KeepDistanceMath.PollSeconds;
            _keepCarry += _count * dt / KeepDistanceMath.PollSeconds;
            int n = (int)(_keepCarry + 1e-9);
            _keepCarry -= n;
            if (_keepCarry < 0) _keepCarry = 0;
            if (n > _count) n = _count;
            long t0 = Stopwatch.GetTimestamp();
            for (int k = 0; k < n; k++)
            {
                if (_keepCursor >= _count) _keepCursor = 0;
                var st = _dense[_keepCursor++];
                try
                {
                    KeepLook(st, now, in r, in ar, in sr);
                }
                catch (Exception e)
                {
                    _keepStats.Errors++;
                    Failed("keep.look", e);
                    SafeEndKeep(st, now);
                }
            }
            long t1 = Stopwatch.GetTimestamp();
            _keepStats.LookCostMs += (t1 - t0) * 1000.0 / Stopwatch.Frequency;
            if (r.On && _keeping.Count > 0) SteerKeepers(now);
            _keepStats.SteerCostMs += (Stopwatch.GetTimestamp() - t1) * 1000.0 / Stopwatch.Frequency;
        }

        // ------------------------------------------------------------------ the look

        /// <summary>His pause runs (step 13), his stack is empty (step 32), he braces (step 23) - managed reads only.</summary>
        private static void KeepWaits(TrackedAgent st, in AthleticsRules ar, in SwingStackRules sr, out bool pause, out bool empty, out bool braced)
        {
            pause = st.Pace != null && st.Pace.Active;
            empty = sr.Enabled && st.Swings.Count(SwingStackMath.Cap(in sr, in ar, st)) == 0;
            braced = st.Brace != null && st.Brace.Active;
        }

        /// <summary>The last action between him and his enemy (mission s): a melee hit or block he gave or took, his or the enemy's release or kick / bash -
        /// and NOW while either is in an attack (a ready or a release) or his kick runs.</summary>
        private static double KeepLastAction(TrackedAgent st, TrackedAgent? foe, double now)
        {
            double t = Max(st.KeepActionAt, Max(st.LastReleaseTime, st.KickBash.DecidedAt));
            if (IsAttackAction(st.PrevAction) || st.AiKickPhase != AiKickPhase.None) return now;
            if (foe == null) return t;
            t = Max(t, Max(foe.LastReleaseTime, foe.KickBash.DecidedAt));
            if (IsAttackAction(foe.PrevAction) || foe.AiKickPhase != AiKickPhase.None) return now;
            return t;
        }

        private static double Max(double a, double b) => double.IsNaN(a) ? b : double.IsNaN(b) ? a : a > b ? a : b;

        private void KeepLook(TrackedAgent st, double now, in KeepDistanceRules r, in AthleticsRules ar, in SwingStackRules sr)
        {
            if (st.Removed) return;
            var m = st.Keep;
            KeepWaits(st, in ar, in sr, out bool pause, out bool empty, out bool braced);
            bool cannot = KeepDistanceMath.CannotAttack(pause, empty, braced);
            // Step 110: a TIRED man who could swing but stands quiet waits too (managed reads: his bar, his wound cap)
            double bar = st.Fraction;
            bool tired = KeepDistanceMath.TiredWaits(in r, bar, AthleticsMath.UsableFraction(in ar, st), m.Keeping && m.ForTired);
            bool waiting = cannot || tired;
            if (!waiting && !m.Keeping)
            {
                m.Candidate = false; // he can attack - the game's own (nothing read)
                return;
            }
            var body = KeepBody;
            if (!body.IsActive(st))
            {
                if (m.Keeping) EndKeep(st, KeepEnd.LeftField, now, native: false);
                return;
            }
            if (IsPlayer(st) || !body.IsAiControlled(st))
            {
                if (m.Keeping) EndKeep(st, KeepEnd.Excluded, now, native: true);
                m.Candidate = false;
                return;
            }
            _keepStats.Looks++;
            body.Kit(st, out bool mounted, out bool routing, out bool ranged, out double length, out bool spear);
            bool eligible = !mounted && !routing && !ranged && !(st.Wall != null && st.Wall.Held);
            bool resting = st.Rhythm.Resting || st.Rhythm.InSwap;
            var foe = body.Target(st, out double px, out double py, out double ex, out double ey);
            double dist = double.NaN;
            if (foe != null)
            {
                double dx = ex - px, dy = ey - py;
                dist = Math.Sqrt(dx * dx + dy * dy);
            }
            if ((pause || empty) && eligible && foe != null) _keepStats.AddWait(r.On, spear, dist); // the waiting distance, ON vs OFF
            var foeSt = foe != null ? Get(foe) : null;
            double last = KeepLastAction(st, foeSt, now);
            if (m.Keeping)
            {
                if (tired) m.ForTired = true; // a keep begun for a pause that finds him tired waits for his bar too
                var v = KeepDistanceMath.Verdict(in r, resting, eligible, last > m.Since + 1e-9, foe != null && ReferenceEquals(foe, m.Target), waiting, now - m.Since);
                if (v != KeepEnd.Count) EndKeep(st, v, now, native: true);
                return;
            }
            if (!KeepDistanceMath.Candidate(in r, eligible, resting, waiting, dist))
            {
                m.Candidate = false;
                m.LastLook = now;
                return;
            }
            _keepStats.CandidateLooks[pause ? 0 : empty ? 1 : braced ? 2 : 3]++;
            if (tired && !cannot && !_firstKeepTired)
            {
                _firstKeepTired = true;
                TraxLog.Info("keep", "first TIRED man near his enemy who could swing (step 110) at " + Sec(now) + " s: " + Name(st) + " - bar " + Pct(bar) + " (at or below "
                    + F1(r.TiredBelowPercent) + "%; no pause, swings stored, no brace), his enemy " + F2(dist) + " m off; he backs off after his quiet "
                    + F1(KeepDistanceMath.QuietSeconds(in r, 0)) + "-" + F1(KeepDistanceMath.QuietSeconds(in r, 1)) + " s and comes back at " + Pct(KeepDistanceMath.TiredReadyAt(in r, AthleticsMath.UsableFraction(in ar, st)))
                    + (r.On ? string.Empty : " - OFF (" + r.OffBecause + "): counted only"));
            }
            if (m.Candidate && !double.IsNaN(m.LastLook) && last > m.LastLook + 1e-9)
            {
                _keepStats.QuietBroken++; // an action restarted a waiting man's clock
                m.QuietRoll = double.NaN;
            }
            if (double.IsNaN(m.QuietRoll)) m.QuietRoll = KeepDistanceMath.QuietSeconds(in r, KeepDice.NextDouble());
            m.Candidate = true;
            m.LastLook = now;
            if (!KeepDistanceMath.Quiet(now, KeepDistanceMath.QuietStart(last, m.QuietFrom), m.QuietRoll)) return;
            _keepStats.QuietDone++;
            if (!r.On)
            {
                _keepStats.WouldOff++;
                m.QuietFrom = now; // counted once a quiet spell
                m.QuietRoll = double.NaN;
                if (!_firstKeepOff)
                {
                    _firstKeepOff = true;
                    TraxLog.Info("keep", "first man who would back off with keep your distance OFF at " + Sec(now) + " s: " + Name(st) + " " + F2(dist) + " m from his enemy"
                        + " - counted only (" + r.OffBecause + ")");
                }
                return;
            }
            BeginKeep(st, m, foe!, dist, px, py, length, spear, pause, empty, braced, now, in r);
            if (m.Keeping)
            {
                m.ForTired = tired;
                if (tired && !cannot) _keepStats.TiredKeeps++;
            }
        }

        private void BeginKeep(TrackedAgent st, KeepMind m, Agent foe, double dist, double px, double py, double length, bool spear, bool pause, bool empty,
            bool braced, double now, in KeepDistanceRules r)
        {
            if (!EnsureKeepWish(st, now)) return;
            m.Phase = KeepPhase.Backing;
            m.Since = m.BackSince = now;
            m.Target = foe;
            m.Spear = spear;
            m.Length = length;
            m.Back = KeepDistanceMath.BackMetres(in r, length);
            m.StartGap = dist;
            m.Goal = dist + m.Back;
            m.HoldGap = double.NaN;
            m.StartX = px;
            m.StartY = py;
            m.Walked = 0;
            m.Candidate = false;
            m.QuietRoll = double.NaN;
            _keepStats.Keeps++;
            _keepStats.AddBack(m.Back, spear, again: false);
            if (!_firstKeep)
            {
                _firstKeep = true;
                TraxLog.Info("keep", "first man KEEPS HIS DISTANCE at " + Sec(now) + " s: " + Name(st) + " (" + (pause ? "his pause" : empty ? "an empty swing stack" : braced ? "bracing" : "tired - bar " + Pct(st.Fraction) + ", he could swing (step 110)")
                    + (braced && pause ? " + bracing" : string.Empty) + ", " + (spear ? "a spear" : "not a spear") + " " + (double.IsNaN(length) ? "length unknown" : F2(length) + " m")
                    + ") " + F2(dist) + " m from his enemy after a quiet " + F1(now - KeepDistanceMath.QuietStart(KeepLastAction(st, Get(foe), now), m.QuietFrom)) + " s - he walks back "
                    + F2(m.Back) + " m facing him, to " + F2(m.Goal) + " m");
            }
            m.NextSteer = now;
            SteerKeeper(st, m, now);
        }

        // ------------------------------------------------------------------ the steer

        private void SteerKeepers(double now)
        {
            for (int i = _keeping.Count - 1; i >= 0; i--)
            {
                if (i >= _keeping.Count) continue;
                var st = _keeping[i];
                var m = st.Keep;
                try
                {
                    if (st.Removed)
                    {
                        EndKeep(st, KeepEnd.LeftField, now, native: false);
                        continue;
                    }
                    if (now < m.NextSteer) continue;
                    m.NextSteer = now + KeepDistanceMath.SteerSeconds;
                    SteerKeeper(st, m, now);
                }
                catch (Exception e)
                {
                    _keepStats.Errors++;
                    Failed("keep.steer", e);
                    SafeEndKeep(st, now);
                }
            }
        }

        /// <summary>His own front (the rhythm's unit table, when his rhythm look is fresh) - NaN in a duel, a tournament, or before any look.</summary>
        private double KeepBehindFront(TrackedAgent st, double px, double py, double now)
        {
            var rm = st.Rhythm;
            if (rm.Key < 0 || double.IsNaN(rm.LookedAt) || now - rm.LookedAt > KeepDistanceMath.FrontFreshSeconds) return double.NaN;
            return _rhythmUnits.BehindFront(rm.Key, px, py, now);
        }

        /// <summary>One keeping man's steer: BACKING - straight back from his enemy (his own frame, a walk) until the goal or the rest's limit behind his
        /// front; HOLDING - he stands (zero) with his walk toward the enemy clamped; the enemy stepping in restarts his quiet wait, and when it passes he backs
        /// off again from where he stands.</summary>
        private void SteerKeeper(TrackedAgent st, KeepMind m, double now)
        {
            var s = st.Input;
            if (s == null)
            {
                EndKeep(st, KeepEnd.Error, now, native: true);
                return;
            }
            var foe = KeepBody.Target(st, out double px, out double py, out double ex, out double ey);
            if (foe == null || !ReferenceEquals(foe, m.Target))
            {
                EndKeep(st, KeepEnd.Target, now, native: true);
                return;
            }
            _keepStats.Steers++;
            double dx = px - ex, dy = py - ey, gap = Math.Sqrt(dx * dx + dy * dy);
            double wx = px - m.StartX, wy = py - m.StartY, walked = Math.Sqrt(wx * wx + wy * wy);
            if (walked > m.Walked) m.Walked = walked;
            s.KeepMove = s.KeepClamp = false;
            s.KeepX = s.KeepY = 0f;
            if (!(gap > 1e-6)) return;
            double ux = dx / gap, uy = dy / gap; // away from him
            double behind = KeepBehindFront(st, px, py, now);
            double limit = _restRules.MaxBackMetres;
            var r = _keepRules;
            if (m.Phase == KeepPhase.Holding)
            {
                if (KeepDistanceMath.SteppedIn(gap, m.HoldGap))
                {
                    // Toni: "If I come while he is gathering Athletics, he shouldn't just start backing up again; he stands there and defends" - the wait again
                    m.HoldGap = gap;
                    m.QuietFrom = now;
                    m.QuietRoll = KeepDistanceMath.QuietSeconds(in r, KeepDice.NextDouble());
                    _keepStats.SteppedIn++;
                    if (!_firstKeepStepIn)
                    {
                        _firstKeepStepIn = true;
                        TraxLog.Info("keep", "first enemy STEPPED IN on a keeping man at " + Sec(now) + " s: " + Name(st) + " now " + F2(gap) + " m off - he stands and defends; he backs off "
                            + "again only after another quiet " + F1(m.QuietRoll) + " s");
                    }
                }
                else if (m.QuietFrom > m.BackSince && KeepDistanceMath.Quiet(now, m.QuietFrom, m.QuietRoll))
                {
                    // both stood quiet after the step-in: he backs off again from where he stands
                    m.Phase = KeepPhase.Backing;
                    m.BackSince = now;
                    m.Goal = gap + m.Back;
                    m.QuietRoll = double.NaN;
                    _keepStats.AddBack(m.Back, m.Spear, again: true);
                    if (!_firstKeepAgain)
                    {
                        _firstKeepAgain = true;
                        TraxLog.Info("keep", "first keeping man BACKED OFF AGAIN at " + Sec(now) + " s after the enemy stepped in and it went quiet: " + Name(st) + " from " + F2(gap) + " m to "
                            + F2(m.Goal) + " m");
                    }
                }
            }
            if (m.Phase == KeepPhase.Backing)
            {
                double back = KeepDistanceMath.WalkBack(gap, m.Goal, behind, limit);
                if (back > 0)
                {
                    bool ground = true;
                    if (now >= m.NextEdge)
                    {
                        m.NextEdge = now + AiInputMath.EdgeCheckSeconds;
                        ground = CourageBody.GroundOk(st, ux, uy);
                    }
                    if (ground && CourageBody.ToLocal(st, ux, uy, out float lx, out float ly))
                    {
                        float k = (float)Math.Min(FearMath.Stick(back), KeepDistanceMath.WalkStick); // a walk, never a run
                        s.KeepMove = true;
                        s.KeepX = lx * k;
                        s.KeepY = ly * k;
                        return;
                    }
                }
                m.Phase = KeepPhase.Holding; // there, at the rest's limit, or no ground behind him
                m.HoldGap = gap;
                if (!_firstKeepHold)
                {
                    _firstKeepHold = true;
                    TraxLog.Info("keep", "first keeping man HOLDS his gap at " + Sec(now) + " s: " + Name(st) + " " + F2(gap) + " m from his enemy (goal " + F2(m.Goal) + " m, walked "
                        + F2(m.Walked) + " m" + (double.IsNaN(behind) ? ", no front of his own" : ", " + F1(behind) + " m behind his front") + ") - he stands, guard up, until he can attack");
                }
            }
            if (KeepDistanceMath.Stands(gap, m.Goal, behind, limit))
            {
                s.KeepMove = true; // he STANDS (zero) - the game's walk in never moves him
                if (CourageBody.ToLocal(st, -ux, -uy, out float cx, out float cy))
                {
                    float len = (float)Math.Sqrt(cx * cx + cy * cy);
                    if (len > 1e-6f)
                    {
                        s.KeepClamp = true; // and whoever moves him (the block, the flock) never closes the gap
                        s.KeepNX = cx / len;
                        s.KeepNY = cy / len;
                    }
                }
            }
        }

        // ------------------------------------------------------------------ the wish and the ends

        private bool EnsureKeepWish(TrackedAgent st, double now)
        {
            var m = st.Keep;
            if (m.Wish) return true;
            EnsureInput(st);
            AiInputHook.HookResult hook;
            try
            {
                hook = CourageBody.Hook(st);
            }
            catch (Exception e)
            {
                _keepStats.Errors++;
                Failed("keep.hook", e);
                return false;
            }
            NoteHooked(st, hook);
            AiInputHook.SetKeep(st, true, now);
            var s = st.Input!;
            m.MoveAtStart = s.KeepMoveFrames;
            m.ClampAtStart = s.KeepClampFrames;
            m.Wish = true;
            m.NextEdge = now;
            _keeping.Add(st);
            return true;
        }

        private void ReleaseKeepWish(TrackedAgent st, double now, bool native)
        {
            var m = st.Keep;
            if (!m.Wish) return;
            m.Wish = false;
            _keeping.Remove(st);
            var s = st.Input;
            if (s == null) return;
            _keepStats.MoveFrames += s.KeepMoveFrames - m.MoveAtStart;
            _keepStats.ClampFrames += s.KeepClampFrames - m.ClampAtStart;
            AiInputHook.SetKeep(st, false, now);
            if (native && !st.Removed)
            {
                try
                {
                    CourageBody.Release(st);
                }
                catch (Exception e)
                {
                    Failed("keep.release", e);
                }
            }
            DrainInputError(st);
        }

        /// <summary>His keep ends (counted once): how long, how far he got; the wish off (every engine change given back); his quiet clock starts again.</summary>
        private void EndKeep(TrackedAgent st, KeepEnd why, double now, bool native)
        {
            var m = st.Keep;
            if (!m.Keeping) return;
            _keepStats.AddEnd(why, double.IsNaN(m.Since) ? double.NaN : now - m.Since, m.Walked);
            if (!_firstKeepEnd)
            {
                _firstKeepEnd = true;
                TraxLog.Info("keep", "first keep ENDED at " + Sec(now) + " s: " + Name(st) + " after " + F1(now - m.Since) + " s, walked " + F2(m.Walked) + " m - "
                    + KeepDistanceMath.EndName(why) + " (the summary counts every reason)");
            }
            if (why == KeepEnd.Ready && !_firstKeepReady)
            {
                _firstKeepReady = true;
                TraxLog.Info("keep", "first keeping man READY again at " + Sec(now) + " s: " + Name(st) + " after " + F1(now - m.Since) + " s - his own AI closes in and swings");
            }
            ReleaseKeepWish(st, now, native);
            m.Reset(now);
        }

        private int EndAllKeeps(KeepEnd why, double now, bool native)
        {
            int n = 0;
            for (int i = _keeping.Count - 1; i >= 0; i--)
            {
                if (i >= _keeping.Count) continue;
                var st = _keeping[i];
                try
                {
                    EndKeep(st, why, now, native && !st.Removed);
                    n++;
                }
                catch (Exception e)
                {
                    Failed("keep.end-all", e);
                    SafeEndKeep(st, now);
                }
            }
            return n;
        }

        private void SafeEndKeep(TrackedAgent st, double now)
        {
            try
            {
                if (st.Keep.Keeping) EndKeep(st, KeepEnd.Error, now, native: !st.Removed);
                else ReleaseKeepWish(st, now, native: !st.Removed);
            }
            catch
            {
                st.Keep.Wish = false;
                st.Keep.Reset(now);
                _keeping.Remove(st);
                if (st.Input != null)
                {
                    st.Input.Keep = st.Input.KeepMove = st.Input.KeepClamp = false;
                }
            }
        }

        /// <summary>Step 78c's rest (or the rotation's swap) takes him: his keep ends first - the rest wins, never a double walk.</summary>
        private void KeepYieldToRest(TrackedAgent st, double now)
        {
            try
            {
                if (st.Keep.Keeping) EndKeep(st, KeepEnd.Rest, now, native: true);
            }
            catch (Exception e)
            {
                _keepStats.Errors++;
                Failed("keep.rest", e);
                SafeEndKeep(st, now);
            }
        }

        /// <summary>A melee collision (OnMeleeHit - a hit, a block, a parry, a kick): both men had an action now (their quiet clocks restart).</summary>
        private void KeepHit(Agent? attacker, Agent? victim, double now)
        {
            var a = Get(attacker);
            if (a != null) a.KeepActionAt = now;
            var v = Get(victim);
            if (v != null) v.KeepActionAt = now;
        }

        private void KeepLeftField(TrackedAgent st)
        {
            try
            {
                if (st.Keep.Keeping) EndKeep(st, KeepEnd.LeftField, SafeNow(), native: false);
                else ReleaseKeepWish(st, SafeNow(), native: false);
            }
            catch (Exception e)
            {
                Failed("keep.left", e);
            }
        }

        private void CloseKeep(bool agentsAlive)
        {
            if (_keepClosed) return;
            _keepClosed = true;
            EndAllKeeps(KeepEnd.MissionEnd, SafeNow(), agentsAlive);
            _keeping.Clear();
        }

        internal void WriteKeepSummary() => TraxLog.Info("summary", _keepStats.SummaryLine(KeepDistanceRules.From(TraxSettings.Shared)));
    }
}

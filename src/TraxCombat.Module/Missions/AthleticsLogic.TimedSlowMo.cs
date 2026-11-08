using System;
using System.Globalization;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 100e - THE SLOW MOTION ON YOUR BEST BLOCKS (Toni 2026-11-08: "Maybe even try a slow mo when the player does it nicely"; AI_NOTES "Step
    /// 100" §5, DESIGN decision 91 (e)):
    ///   - STARTED from 100b's seam <see cref="TimedRiposteAccepted"/> (the engine took the riposte stagger) when YOU blocked and the timing scored at
    ///     least <c>TimedBlockSlowMoMinScore</c> - Core <see cref="TimedSlowMoMath.Decide"/>: single player, not paused, none playing, past the
    ///     cooldown, and NOBODY ELSE slowing the game (a vanilla request - the order menu's 0.25, the radial / equip menus, the developer slow-mo -
    ///     or the scene slower than 1: another mod) - we never fight another request.
    ///   - RAMPED every tick on the REAL clock (<see cref="ISlowMoBody.RealTime"/>; the mission dt is already scaled by our own speed):
    ///     <see cref="TimedSlowMoMath.Speed"/> - our request (id 31100) replaced with the new value each frame, the developer slow-mo's way.
    ///   - GIVEN BACK on every exit, each counted: the ramp's end, the switches off (the master switch first), a pause or a real-time stall, another
    ///     request coming in under ours, you off the field, the mission end (<see cref="OnEndMissionInternal"/>, before the summary - which then
    ///     checks that nothing of ours is left) / the behaviour removed (<see cref="OnRemoveBehavior"/>), a caught error (disabled for the battle).
    ///     The removal asks first (<c>RemoveTimeSpeedRequest</c> throws on a missing id).
    /// Main thread; nothing allocated per tick; idle = one bool read a tick.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private readonly TimedSlowMo _slowMo = new TimedSlowMo();
        private readonly TimedSlowMoStats _slowMoStats = new TimedSlowMoStats();
        private bool _slowMoFirstLine;

        /// <summary>Step 100e: the time-speed reads (the smoke swaps in its stand-in).</summary>
        internal ISlowMoBody SlowMoBody { get; set; } = SlowMoBodyDefault.Current;

        internal TimedSlowMo SlowMo => _slowMo;

        internal TimedSlowMoStats SlowMoStats => _slowMoStats;

        /// <summary>From <see cref="TimedRiposteAccepted"/>: a timed riposte the engine took - start the slow motion if it is yours and sharp
        /// enough. Its own try: an error disables the slow motion, never the riposte.</summary>
        private void MaybeStartSlowMo(TrackedAgent? blocker, double score)
        {
            if (_slowMo.Disabled || !(score > 0)) return;
            try
            {
                var r = TimedSlowMoRules.From(TraxSettings.Shared);
                bool yours = blocker != null && IsPlayer(blocker) && YouDrive(blocker);
                var body = SlowMoBody;
                var m = Mission;
                double now = body.RealTime;
                // the engine reads (another request, the pause) only when the cheap gates pass
                bool cheap = r.Enabled && yours && score >= r.MinScore;
                bool other = cheap && TimedSlowMoMath.OtherSlowing(body.VanillaRequestPresent(m), body.SceneSpeed(m), _slowMo.Playing ? _slowMo.Asked : 1);
                var skip = TimedSlowMoMath.Decide(in r, yours, score, cheap && body.Multiplayer, cheap && body.Paused, _slowMo.Playing, now,
                    _slowMo.LastStartReal, other);
                if (skip == SlowMoSkip.NotYou || skip == SlowMoSkip.Off) return; // not counted: the AI's ripostes / the switch off are not candidates
                _slowMoStats.NoteSkip(skip);
                if (skip != SlowMoSkip.Start)
                {
                    if (skip != SlowMoSkip.LowScore && skip != SlowMoSkip.Cooldown)
                        TraxLog.Info("impact", "slow-mo skipped at " + Sec(now) + " real s: " + TimedSlowMoMath.SkipName(skip) + " (your timing " + F2(score) + ")");
                    return;
                }
                _slowMo.Start(now, score);
                _slowMoStats.NoteStart(score);
                double speed = TimedSlowMoMath.Speed(0, r.Speed, r.HoldSeconds, r.EaseOutSeconds); // step 101: the floor at once
                body.Set(m, TimedSlowMoMath.RequestId, (float)speed);
                _slowMo.NoteAsked(speed);
                _slowMoStats.NoteAsked(speed);
                if (!_slowMoFirstLine)
                {
                    _slowMoFirstLine = true;
                    TraxLog.Info("impact", "live: first SLOW MOTION - your timed block (timing " + F2(score) + "); " + r.Describe() + "; about "
                        + TimedSlowMoMath.MissionSeconds(r.Speed, r.HoldSeconds, r.EaseOutSeconds).ToString("0.00", CultureInfo.InvariantCulture) + " s of battle time in "
                        + r.RealSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " real s");
                }
            }
            catch (Exception e)
            {
                SlowMoFailed("impact.slowmo-start", e);
            }
        }

        /// <summary>Every tick (OnMissionTick, its own try, whatever the switches - so a switch going off gives the request back): the ramp.</summary>
        internal void TickSlowMo()
        {
            if (!_slowMo.Playing) return;
            try
            {
                var body = SlowMoBody;
                var m = Mission;
                double now = body.RealTime;
                var r = TimedSlowMoRules.From(TraxSettings.Shared);
                SlowMoEnd? end = null;
                if (!r.Enabled) end = SlowMoEnd.SwitchedOff;
                else if (body.Paused || _slowMo.Stalled(now)) end = SlowMoEnd.Paused;
                else if (YouGone()) end = SlowMoEnd.YouGone;
                else if (TimedSlowMoMath.OtherSlowing(body.VanillaRequestPresent(m), body.SceneSpeed(m), _slowMo.Asked)) end = SlowMoEnd.OtherRequest;
                if (end != null)
                {
                    EndSlowMo(end.Value, now);
                    return;
                }
                double speed = _slowMo.Step(now, in r);
                if (double.IsNaN(speed))
                {
                    EndSlowMo(SlowMoEnd.Finished, now);
                    return;
                }
                body.Set(m, TimedSlowMoMath.RequestId, (float)speed);
                _slowMo.NoteAsked(speed);
                _slowMoStats.NoteAsked(speed);
            }
            catch (Exception e)
            {
                SlowMoFailed("impact.slowmo-tick", e);
            }
        }

        private bool YouGone()
        {
            var you = PlayerRecord();
            return you == null || you.Removed;
        }

        /// <summary>The request given back (asked first), the end counted, the state cleared.</summary>
        private void EndSlowMo(SlowMoEnd end, double nowReal)
        {
            double held = _slowMo.Playing ? nowReal - _slowMo.StartReal : double.NaN;
            bool wasPlaying = _slowMo.Playing;
            _slowMo.Stop();
            _slowMo.NoteAsked(double.NaN);
            bool removed = SlowMoBody.Remove(Mission, TimedSlowMoMath.RequestId);
            if (wasPlaying)
            {
                _slowMoStats.NoteEnd(end, held);
                if (!removed) _slowMoStats.RemoveRefused++;
                if (end != SlowMoEnd.Finished)
                    TraxLog.Info("impact", "slow-mo ended early: " + TimedSlowMoMath.EndName(end) + " after " + F2(held) + " real s - the game's speed given back");
            }
        }

        /// <summary>The mission end / the behaviour removed: give back whatever is ours and check that nothing is left (the summary's "must be 0").
        /// Safe to call twice; its own try.</summary>
        internal void GiveBackSlowMo(string when)
        {
            try
            {
                var body = SlowMoBody;
                var m = Mission;
                double now = SafeRealTime(body);
                if (_slowMo.Playing) EndSlowMo(SlowMoEnd.MissionEnd, now);
                else if (body.Has(m, TimedSlowMoMath.RequestId))
                {
                    body.Remove(m, TimedSlowMoMath.RequestId); // a request not mirrored (should never happen) - given back and counted
                    _slowMoStats.LeftOver++;
                    TraxLog.Info("impact", "slow-mo: a request of ours was still in the game at " + when + " - removed");
                }
                if (body.Has(m, TimedSlowMoMath.RequestId)) _slowMoStats.LeftOver++;
                _slowMoStats.CheckedAtEnd = true;
            }
            catch (Exception e)
            {
                _slowMoStats.Errors++;
                TraxLog.Error("impact.slowmo-giveback", e);
            }
        }

        private static double SafeRealTime(ISlowMoBody body)
        {
            try
            {
                return body.RealTime;
            }
            catch
            {
                return double.NaN;
            }
        }

        /// <summary>A caught error: disabled for the battle, the request given back (asked first, its own try).</summary>
        private void SlowMoFailed(string where, Exception e)
        {
            _slowMoStats.Errors++;
            _slowMo.Disabled = true;
            TraxLog.Error(where, e);
            try
            {
                EndSlowMo(SlowMoEnd.Error, SafeRealTime(SlowMoBody));
            }
            catch (Exception e2)
            {
                TraxLog.Error(where + ".giveback", e2);
            }
        }

        private void WriteSlowMoSummary() => TraxLog.Info("summary", _slowMoStats.SummaryLine(TimedSlowMoRules.From(TraxSettings.Shared)));
    }
}

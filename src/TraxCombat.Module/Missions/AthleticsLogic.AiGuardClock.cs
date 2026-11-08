using System;
using System.Diagnostics;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 102 - THE AI TIMES ITS BLOCKS (Toni 2026-11-08: the AI fights as before; a guard it happens to raise just before the blow lands counts
    /// at <c>TimedBlockAiPercent</c> 50 % with the player's skill-scaled window; AI_NOTES "Step 102", DESIGN decision 91 (n)).
    ///   - THE WATCH: a staggered look (every man every <see cref="AiGuardWatchLookSeconds"/>, like the fever's poll) keeps step 16's input
    ///     callback on for every AI man with an enemy within <see cref="AiGuardWatchMetres"/> (<see cref="AiInputState.GuardWatch"/> - not a wish:
    ///     his input is never changed for it) and lets it go when none is near (if nothing else wants it).
    ///   - THE CLOCK: <see cref="AiGuardClockHook.Note"/> reads the final defend bits of each of his input frames into <see cref="TrackedAgent.AiGuard"/>
    ///     - for every hooked man, watched or held by another wish.
    ///   - AT CONTACT (<see cref="FillTiming"/>): a known age = <see cref="TimingSource.AiClock"/>; otherwise the old channel-1 read (kept as the
    ///     fallback and the diagnostic's read - it never produced a timing in Toni's 2026-11-08 log).
    /// Master switch first (the timed blocks' rules): off = the watch let go, the hook's notes stop. Removal: the record forgotten, no engine call.
    /// Main thread; no allocation per tick (the first watch of a man creates his input state and component once, like every step-16 wish).
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        /// <summary>An enemy this near (m) keeps a man watched - beyond any weapon's reach plus a look's approach. Plumbing.</summary>
        internal const float AiGuardWatchMetres = 5f;

        /// <summary>Every man is looked at this often (s), staggered over the ticks. Plumbing.</summary>
        internal const double AiGuardWatchLookSeconds = 0.25;

        private readonly AiGuardClockStats _aiClock = new AiGuardClockStats();
        private bool _aiWatchOn;
        private double _aiWatchLastTick = double.NaN, _aiWatchCarry;
        private int _aiWatchCursor;

        internal AiGuardClockStats AiClockStats => _aiClock;

        /// <summary>Every tick while timed blocks can apply (from <see cref="TickTimedBlocks"/>): the clock on, a share of the men looked at.</summary>
        private void TickAiGuardWatch(double now)
        {
            if (!_aiWatchOn)
            {
                _aiWatchOn = true;
                _aiWatchLastTick = double.NaN;
                TraxLog.Info("impact", "timed blocks: the AI guard clock runs from " + Sec(now) + " s - every AI man with an enemy within "
                    + AiGuardWatchMetres.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " m has his input's defend bits read each frame (never changed); his guard's age at a correct block counts at the AI's share");
            }
            AiGuardClockHook.Stats = _aiClock;
            AiGuardClockHook.On = true;
            double dt = double.IsNaN(_aiWatchLastTick) || now < _aiWatchLastTick ? AiGuardWatchLookSeconds : now - _aiWatchLastTick;
            _aiWatchLastTick = now;
            if (dt > AiGuardWatchLookSeconds) dt = AiGuardWatchLookSeconds;
            _aiWatchCarry += _count * dt / AiGuardWatchLookSeconds;
            int n = (int)(_aiWatchCarry + 1e-9);
            _aiWatchCarry -= n;
            if (_aiWatchCarry < 0) _aiWatchCarry = 0;
            if (n > _count) n = _count;
            if (n == 0) return;
            long start = Stopwatch.GetTimestamp();
            var body = GuardBody;
            for (int k = 0; k < n; k++)
            {
                if (_aiWatchCursor >= _count) _aiWatchCursor = 0;
                var st = _dense[_aiWatchCursor++];
                try
                {
                    LookAiGuard(st, body);
                }
                catch (Exception e)
                {
                    Failed("impact.ai-guard-watch", e);
                }
            }
            _aiClock.Looks += n;
            _aiClock.LookCostMs += (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }

        /// <summary>One man's look: watched while an enemy is near, let go when none is.</summary>
        private void LookAiGuard(TrackedAgent st, IGuardBody body)
        {
            if (st.Removed) return;
            bool watched = st.Input != null && st.Input.GuardWatch;
            bool want = !IsPlayer(st) && body.WatchWanted(st, AiGuardWatchMetres);
            if (want == watched) return;
            if (want)
            {
                EnsureInput(st);
                st.Input!.GuardWatch = true;
                var r = body.Hook(st);
                if (r == AiInputHook.HookResult.FirstHookTurnedOn || r == AiInputHook.HookResult.TurnedOnAgain) _aiClock.CallbacksTurnedOn++;
                _aiClock.NoteWatch(true);
            }
            else
            {
                st.Input!.GuardWatch = false;
                body.Release(st);
                _aiClock.NoteWatch(false);
            }
            DrainInputError(st);
        }

        /// <summary>Timed blocks off (a switch, the master switch, the impact system): every watch let go, the hook's notes stopped.</summary>
        private void StopAiGuardWatch(double now, string why)
        {
            AiGuardClockHook.On = false;
            if (!_aiWatchOn) return;
            _aiWatchOn = false;
            var body = GuardBody;
            int released = 0;
            for (int i = 0; i < _count; i++)
            {
                var st = _dense[i];
                if (st == null || st.Input == null || !st.Input.GuardWatch) continue;
                try
                {
                    st.Input.GuardWatch = false;
                    _aiClock.NoteWatch(false);
                    released++;
                    body.Release(st);
                }
                catch (Exception e)
                {
                    Failed("impact.ai-guard-release", e);
                }
            }
            TraxLog.Info("impact", "timed blocks: the AI guard clock stopped at " + Sec(now) + " s (" + why + ") - " + released + " watched men let go");
        }

        /// <summary>He left the field (Forget): his watch and clock dropped - no engine call.</summary>
        private void AiGuardLeftField(TrackedAgent st)
        {
            if (st.Input != null && st.Input.GuardWatch)
            {
                st.Input.GuardWatch = false;
                _aiClock.NoteWatch(false);
            }
            st.AiGuard.Reset();
        }

        /// <summary>The mission's end / the logic's stop: the hook's notes stop (the static slot never outlives the running logic).</summary>
        private void CloseAiGuardClock()
        {
            if (ReferenceEquals(AiGuardClockHook.Stats, _aiClock))
            {
                AiGuardClockHook.On = false;
                AiGuardClockHook.Stats = null;
            }
            _aiWatchOn = false;
        }

        /// <summary>The [summary] line (after the timed blocks' three).</summary>
        private string AiGuardClockLine() => _aiClock.SummaryLine(_timed, AiGuardWatchMetres, AiGuardWatchLookSeconds);
    }
}

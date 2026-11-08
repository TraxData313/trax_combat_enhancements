using System;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 102 (AI_NOTES "Step 102", DESIGN decision 91 (n)): the AI guard clock's side of step 16's input hook. <see cref="AiInputComponent"/>
    /// calls <see cref="Note"/> with the man's FINAL movement flags (after every edit of ours, passed by value - nothing is written back), every
    /// input frame, while <see cref="On"/> (the running logic's timed blocks on). A struct write and a few counts - no allocation, no engine call
    /// (the mission time is the mission's cached field). Main thread (<c>Agent.OnAIInputSet</c> is an MBCallback with isMultiThreadCallable false).
    /// </summary>
    internal static class AiGuardClockHook
    {
        /// <summary>The clock runs (the logic's tick sets it; off at the switch, the mission's end, the logic's stop).</summary>
        internal static bool On;

        /// <summary>The stats the frames are counted into (the running logic's; null = not counted).</summary>
        internal static AiGuardClockStats? Stats;

        /// <summary>The offline smoke's mission time (NaN = the game's <c>Mission.Current.CurrentTime</c>).</summary>
        internal static double SmokeNow = double.NaN;

        internal static double Now()
        {
            if (!double.IsNaN(SmokeNow)) return SmokeNow;
            var m = Mission.Current;
            return m == null ? double.NaN : m.CurrentTime;
        }

        /// <summary>One input frame of his: the flags into his clock. Never throws to the engine (an error is kept for the tick's log).</summary>
        internal static void Note(TrackedAgent st, AiInputState s, uint finalFlags)
        {
            try
            {
                double now = Now();
                if (double.IsNaN(now) || st.Removed) return;
                var n = st.AiGuard.Note(finalFlags, now);
                var stats = Stats;
                if (stats == null) return;
                stats.Frames++;
                stats.Notes[(int)n]++;
            }
            catch (Exception e)
            {
                s.Errors++;
                s.PendingError ??= e;
            }
        }
    }
}

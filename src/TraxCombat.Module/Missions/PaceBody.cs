using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>One fighter's pace hold (DESIGN §2 attack rate, step 5e): asked for by a swing's end
    /// (pending until the tick), running (NoAttack set by us), or waiting for a game job to end before
    /// our NoAttack can be lifted. Allocated once per fighter who is ever held - reused (no per-tick
    /// allocation). Main thread.</summary>
    internal sealed class PaceState
    {
        // ---- asked for by a swing's end, started by the next tick
        public bool Pending;
        public double PendingAt;
        public double Until;
        public int Bin;
        public float Asked = 1f;
        public double Reference;
        public bool ReferenceOwn;
        public double ExpectedReady;
        public double ReleaseStart;

        // ---- running
        public bool Active;
        public double StartedAt;
        public int FlagsBefore;
        public int FlagsAfter;

        /// <summary>A swing started while held (inside a hit callback, maybe) - the tick ends the hold.</summary>
        public bool EndAsked;
        public PaceEnd EndReason;

        // ---- lifted, but a game job was on him: our NoAttack comes off once he is free
        public bool Waiting;
        public double NextCheck;

        /// <summary>When his last hold ended (−1 = none since his last ready) - the next ready's delay after it is measured.</summary>
        public double EndedAt = -1;

        /// <summary>The mission's first hold - its start and end are logged in full.</summary>
        public bool First;
    }

    /// <summary>
    /// Every GAME call of the pace hold behind one seam (the step-5d pattern): the logic keeps the
    /// bookkeeping (target, queue, timers, every end path, the stats), this touches the engine. The
    /// real one is <see cref="GamePaceBody"/>; the offline smoke plays a stand-in (its agents have no
    /// native side).
    /// </summary>
    internal interface IPaceBody
    {
        /// <summary>Sets NoAttack on him when nothing of the game's is on him. True = held (flags
        /// before / after filled); false = <paramref name="refusal"/> says why not.</summary>
        bool Start(TrackedAgent st, PaceState ps, out PaceRefusal refusal);

        /// <summary>Lifts our NoAttack if he is free of game jobs; else reports <see cref="PaceRelease.Waiting"/>
        /// and leaves every flag alone.</summary>
        PaceRelease Release(TrackedAgent st, PaceState ps);

        /// <summary>A running hold must end now, before its time: the player took him, he mounted.
        /// Managed reads only - it runs every tick for every held man.</summary>
        bool MustEnd(TrackedAgent st, out PaceEnd why);
    }

    /// <summary>
    /// The engine side (AI_NOTES "Step 5e"): <c>SetScriptedFlags(GetScriptedFlags() | NoAttack)</c>
    /// with no scripted position - vanilla's own "do not attack" (Agent.UseGameObject). The flag word
    /// is shared with the step back, item pickup and siege objects, so NoAttack is set only on a man
    /// with neither GoToPosition nor NoAttack already and no game object / ladder / detachment, and
    /// lifted only while none of those is on him. Main thread (the logic's tick), never inside an
    /// engine callback.
    /// </summary>
    internal sealed class GamePaceBody : IPaceBody
    {
        private const int GoToPosition = (int)Agent.AIScriptedFrameFlags.GoToPosition;
        private const int NoAttack = (int)Agent.AIScriptedFrameFlags.NoAttack;

        public bool Start(TrackedAgent st, PaceState ps, out PaceRefusal refusal)
        {
            var a = st.Agent;
            refusal = PaceRefusal.Gone;
            if (!a.IsActive()) return false;
            refusal = PaceRefusal.NotAi;
            if (a.IsMainAgent || !a.IsAIControlled) return false;
            refusal = PaceRefusal.Busy;
            if (a.MountAgent != null || GameJob(a)) return false;
            int flags = (int)a.GetScriptedFlags();
            if ((flags & GoToPosition) != 0) return false;
            refusal = PaceRefusal.AlreadyNoAttack;
            if ((flags & NoAttack) != 0) return false;
            ps.FlagsBefore = flags;
            a.SetScriptedFlags((Agent.AIScriptedFrameFlags)(flags | NoAttack));
            int after = (int)a.GetScriptedFlags();
            ps.FlagsAfter = after;
            refusal = PaceRefusal.EngineIgnored;
            return (after & NoAttack) != 0;
        }

        public PaceRelease Release(TrackedAgent st, PaceState ps)
        {
            var a = st.Agent;
            if (!a.IsActive()) return PaceRelease.Gone;
            int flags = (int)a.GetScriptedFlags();
            if ((flags & NoAttack) == 0) return PaceRelease.ClearedByGame;
            // a game job on him (a scripted frame - the step back's too -, an object, a ladder, a
            // detachment): its NoAttack may be its own - never lift it under the job; wait
            if ((flags & GoToPosition) != 0 || GameJob(a)) return PaceRelease.Waiting;
            a.SetScriptedFlags((Agent.AIScriptedFrameFlags)(flags & ~NoAttack));
            ps.FlagsAfter = (int)a.GetScriptedFlags();
            return PaceRelease.ClearedByUs;
        }

        public bool MustEnd(TrackedAgent st, out PaceEnd why)
        {
            var a = st.Agent;
            why = a.IsMainAgent ? PaceEnd.PlayerControl : PaceEnd.Mounted;
            return a.IsMainAgent || a.MountAgent != null;
        }

        /// <summary>The game's own jobs that script a man (managed reads, plus the move-to-object state).</summary>
        private static bool GameJob(Agent a) =>
            a.IsUsingGameObject || a.IsInLadderQueue || a.IsDetachedFromFormation || a.AIMoveToGameObjectIsEnabled();
    }
}

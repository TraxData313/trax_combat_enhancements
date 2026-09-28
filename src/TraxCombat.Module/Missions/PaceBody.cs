using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>One AI fighter's no-attack timer (DESIGN §2, step 5e's pace hold; step 13: after every
    /// attack, melee and ranged, on foot and mounted, for D × (1/m − 1)): asked for by an attack's end
    /// (pending until the tick), running (NoAttack set by us), or waiting for a game job to end before
    /// our NoAttack can be lifted. Allocated once per fighter who is ever held - reused (no per-tick
    /// allocation). Main thread.</summary>
    internal sealed class PaceState
    {
        // ---- asked for by an attack's end, started by the next tick
        public bool Pending;
        public double PendingAt;
        public double Until;
        public int Bin;
        public float Asked = 1f;

        /// <summary>The attack's own duration D (step 20b: at full animation speed), its kind, when it ended (the timer
        /// runs from there) and the pause asked; Played = the attack as it really played (a slower swing is longer).</summary>
        public double Duration;
        public double Played;
        public AttackKind Kind;
        public double AttackEnd;
        public double Pause;

        /// <summary>Step 21: the pause's parts - the tired part (D × (1/m − 1)) and the battle-pace share on top - the class of
        /// the attack and the "swing less" % that sized a melee share (0 for archers and classes without one).</summary>
        public double Tired;
        public double Share;
        public AttackClass Class;
        public int Percent;

        // ---- running
        public bool Active;
        public double StartedAt;
        public int FlagsBefore;
        public int FlagsAfter;

        /// <summary>An attack started while held (inside a hit callback, maybe) - the tick ends the hold.</summary>
        public bool EndAsked;
        public PaceEnd EndReason;

        // ---- lifted, but a game job was on him: our NoAttack comes off once he is free
        public bool Waiting;
        public double WaitingSince;
        public double NextCheck;

        /// <summary>When his last hold ended (−1 = none since his last ready) - the next ready's delay after it is measured.</summary>
        public double EndedAt = -1;

        /// <summary>The mission's first hold - its start and end are logged in full.</summary>
        public bool First;

        // ---- step 16
        /// <summary>This hold runs through the AI's own input (AttackRatePaceByInput when it started), not NoAttack.</summary>
        public bool ByInput;

        /// <summary>A step back ran during this hold (both at once) - counted once when the hold ends.</summary>
        public bool Overlapped;

        /// <summary>A NoAttack hold waiting for a SCRIPTED step back to end (its frame owns the flag word).</summary>
        public bool Deferred;

        /// <summary>The input hook's call count when the hold started - none by its end = the engine never called us.</summary>
        public long CallsAtStart;

        /// <summary>What hooking him did (the first hold's log line).</summary>
        public AiInputHook.HookResult Hook;

        /// <summary>Melee hits he took during this hold and how many he blocked (the first hold's log line).</summary>
        public int HitsTaken;
        public int HitsBlocked;
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
        /// and leaves every flag alone. <paramref name="evenUnderAFrame"/>: a plain scripted frame (no
        /// object, ladder or walk to an object) has lasted long enough - lift it under that frame too.</summary>
        PaceRelease Release(TrackedAgent st, PaceState ps, bool evenUnderAFrame);

        /// <summary>A running hold must end now, before its time: the player took him. (Mounting no
        /// longer ends it - step 13 holds riders too.) Managed reads only - it runs every tick for every
        /// held man.</summary>
        bool MustEnd(TrackedAgent st, out PaceEnd why);
    }

    /// <summary>
    /// The engine side (AI_NOTES "Step 5e"): <c>SetScriptedFlags(GetScriptedFlags() | NoAttack)</c>
    /// with no scripted position - vanilla's own "do not attack" (Agent.UseGameObject). The flag word
    /// is shared with the step back, item pickup and siege objects, so NoAttack is set only on a man
    /// with neither GoToPosition nor NoAttack already and no game object / ladder / detachment (a rider
    /// too - step 13: without the animation technique a tired rider would otherwise attack at the full
    /// rate), and
    /// lifted only while no object, ladder or walk to an object is on him (their NoAttack may be their
    /// own) - and no scripted frame, unless that frame has lasted
    /// <see cref="AttackRateMath.WaitingMaxSecondsUnderAFrame"/> (a job that may want its man to
    /// fight). Main thread (the logic's tick), never inside an engine callback.
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
            if (GameJob(a)) return false;
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

        public PaceRelease Release(TrackedAgent st, PaceState ps, bool evenUnderAFrame)
        {
            var a = st.Agent;
            if (!a.IsActive()) return PaceRelease.Gone;
            int flags = (int)a.GetScriptedFlags();
            if ((flags & NoAttack) == 0) return PaceRelease.ClearedByGame;
            // a game job on him: an object, a ladder, a walk to an object - their NoAttack may be
            // their own: never lift it under them, wait. A plain scripted frame (the step back's too)
            // likewise - until it has lasted long enough to be a job that wants him to fight.
            if (a.IsUsingGameObject || a.IsInLadderQueue || a.AIMoveToGameObjectIsEnabled()) return PaceRelease.Waiting;
            if ((flags & GoToPosition) != 0 && !evenUnderAFrame) return PaceRelease.Waiting;
            a.SetScriptedFlags((Agent.AIScriptedFrameFlags)(flags & ~NoAttack));
            ps.FlagsAfter = (int)a.GetScriptedFlags();
            return PaceRelease.ClearedByUs;
        }

        public bool MustEnd(TrackedAgent st, out PaceEnd why)
        {
            var a = st.Agent;
            why = PaceEnd.PlayerControl;
            return a.IsMainAgent;
        }

        /// <summary>The game's own jobs that script a man (managed reads, plus the move-to-object state).</summary>
        internal static bool GameJob(Agent a) =>
            a.IsUsingGameObject || a.IsInLadderQueue || a.IsDetachedFromFormation || a.AIMoveToGameObjectIsEnabled();
    }

    /// <summary>
    /// Step 16's engine side of the AI timer (AttackRatePaceByInput, AI_NOTES "Step 16"): no flag at all - the man is
    /// hooked (<see cref="AiInputHook.Hook"/>: our component, the engine's callback on) and the logic's wish
    /// (<see cref="AiInputState.HoldAttacks"/>) makes the component take only the attack bits out of his own input,
    /// so his guard, parries and moves stay his. The same men as the NoAttack body are refused (not AI, a game job on
    /// him) so the A/B arms hold the same men; nothing is shared with the game, so nothing ever waits. Release: the
    /// callback flag off again if he is idle and it was ours. Main thread (the logic's tick).
    /// </summary>
    internal sealed class InputPaceBody : IPaceBody
    {
        public bool Start(TrackedAgent st, PaceState ps, out PaceRefusal refusal)
        {
            var a = st.Agent;
            refusal = PaceRefusal.Gone;
            if (!a.IsActive()) return false;
            refusal = PaceRefusal.NotAi;
            if (a.IsMainAgent || !a.IsAIControlled) return false;
            refusal = PaceRefusal.Busy;
            if (GamePaceBody.GameJob(a)) return false;
            st.Input ??= new AiInputState();
            ps.Hook = AiInputHook.Hook(st);
            ps.FlagsBefore = ps.FlagsAfter = (int)a.GetScriptedFlags();
            refusal = PaceRefusal.Error;
            return true;
        }

        public PaceRelease Release(TrackedAgent st, PaceState ps, bool evenUnderAFrame)
        {
            // the logic took the wish back before this call; the callback flag goes off if he is idle and it was ours
            if (st.Removed || !st.Agent.IsActive()) return PaceRelease.Gone;
            AiInputHook.UnhookIfIdle(st);
            return PaceRelease.ClearedByUs;
        }

        public bool MustEnd(TrackedAgent st, out PaceEnd why)
        {
            why = PaceEnd.PlayerControl;
            return st.Agent.IsMainAgent;
        }
    }
}

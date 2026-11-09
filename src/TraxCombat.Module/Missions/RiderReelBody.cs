using TaleWorlds.MountAndBlade;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 104b (AI_NOTES "104b - built", DESIGN decision 93): the engine side of the reel in the saddle - is he still riding, does the AI
    /// drive him, his horse's speed (the summary's measure of the horse check), and step 16's input hook on him. The channel-1 animation goes
    /// through <see cref="IAnimBody"/> (step 100d's) and the horse check through the stat decorator's mount multiplier. Main thread (the tick).
    /// The offline smoke plays a stand-in.
    /// </summary>
    internal interface IRiderReelBody
    {
        /// <summary>He is active and on an active horse.</summary>
        bool Riding(TrackedAgent st);

        /// <summary>The AI drives him (not you, not a man you took over) - his lock goes through step 16's hook.</summary>
        bool AiDriven(TrackedAgent st);

        /// <summary>His horse's speed now (m/s; NaN = no horse) - a native read, riders only.</summary>
        double HorseSpeed(TrackedAgent st);

        /// <summary>Step 16's hook on him (the component + the engine's callback).</summary>
        AiInputHook.HookResult Hook(TrackedAgent st);

        /// <summary>The callback off again when no wish is left (never for a removed man).</summary>
        void Release(TrackedAgent st);
    }

    internal sealed class GameRiderReelBody : IRiderReelBody
    {
        public bool Riding(TrackedAgent st)
        {
            var a = st.Agent;
            if (!a.IsActive()) return false;
            var m = a.MountAgent;
            return m != null && m.IsActive();
        }

        public bool AiDriven(TrackedAgent st)
        {
            var a = st.Agent;
            return a.IsAIControlled && !a.IsMainAgent;
        }

        public double HorseSpeed(TrackedAgent st)
        {
            var m = st.Agent.MountAgent;
            return m != null && m.IsActive() ? m.GetCurrentVelocity().Length : double.NaN;
        }

        public AiInputHook.HookResult Hook(TrackedAgent st) => AiInputHook.Hook(st);

        public void Release(TrackedAgent st)
        {
            if (!st.Removed) AiInputHook.UnhookIfIdle(st);
        }
    }

    /// <summary>The body every new <see cref="AthleticsLogic"/> starts with - the game's; the offline smoke swaps in its stand-in.</summary>
    internal static class RiderReelBodyDefault
    {
        internal static IRiderReelBody Current = new GameRiderReelBody();
    }
}

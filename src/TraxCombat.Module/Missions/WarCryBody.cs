using TaleWorlds.MountAndBlade;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 79: the war cries' engine side behind one seam (the smoke plays a counting stand-in - it cannot play sound). Main thread only (the logic's
    /// tick drains the queue).
    /// </summary>
    internal interface IWarCryBody
    {
        /// <summary>He may shout: on the field, an AI-driven man, not the player, not running away or retreating (routed / fleeing).</summary>
        bool MayShout(TrackedAgent st);

        /// <summary>He rides a horse (the game's own yell picks HorseRally then).</summary>
        bool IsMounted(TrackedAgent st);

        /// <summary>The cry - <c>Agent.MakeVoice</c>; <paramref name="now"/> only for the smoke's counting.</summary>
        void Shout(TrackedAgent st, bool mounted, double now);
    }

    /// <summary>
    /// The game: vanilla's own yell (Agent.OnTickParallel's <c>_wantsToYell</c>, the charge order's YellingBehaviour) plays
    /// <c>SkinVoiceManager.VoiceType.Yell</c> on foot and <c>HorseRally</c> on a horse, NoPrediction - so do we.
    /// </summary>
    internal sealed class GameWarCryBody : IWarCryBody
    {
        public bool MayShout(TrackedAgent st)
        {
            var a = st.Agent;
            return a.IsActive() && a.IsHuman && a.IsAIControlled && !a.IsMainAgent && !a.IsRunningAway && !a.IsRetreating();
        }

        public bool IsMounted(TrackedAgent st) => st.Agent.MountAgent != null;

        public void Shout(TrackedAgent st, bool mounted, double now) =>
            st.Agent.MakeVoice(mounted ? SkinVoiceManager.VoiceType.HorseRally : SkinVoiceManager.VoiceType.Yell,
                SkinVoiceManager.CombatVoiceNetworkPredictionType.NoPrediction);
    }

    internal static class WarCryBodyDefault
    {
        internal static IWarCryBody Current = new GameWarCryBody();
    }
}

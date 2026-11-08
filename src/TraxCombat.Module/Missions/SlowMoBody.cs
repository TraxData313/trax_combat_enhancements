using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 100e (AI_NOTES "Step 100" §5): the engine side of the player's slow motion - the game's own time-speed requests on the mission
    /// (<c>Mission.AddTimeSpeedRequest</c> / <c>RemoveTimeSpeedRequest</c> / <c>GetRequestedTimeSpeed</c>; the LOWEST request wins each frame,
    /// <c>Mission.UpdateSceneTimeSpeed</c>), the real clock (<c>TaleWorlds.Engine.Time.ApplicationTime</c> - never scaled), the pause
    /// (<c>MissionState.Paused</c> / <c>MBCommon.IsPaused</c> - the mission dt is 0 then) and multiplayer. <c>RemoveTimeSpeedRequest</c> THROWS on a
    /// missing id (<c>RemoveAt(-1)</c>), so <see cref="Remove"/> asks first. Main thread. The offline smoke plays a stand-in.
    /// </summary>
    internal interface ISlowMoBody
    {
        /// <summary>Real seconds (the engine's application clock).</summary>
        double RealTime { get; }

        bool Paused { get; }

        bool Multiplayer { get; }

        /// <summary>The scene's time speed now (NaN = no scene).</summary>
        double SceneSpeed(Mission m);

        /// <summary>A known vanilla request (624 / 813 / 864 / 1121) is in the mission.</summary>
        bool VanillaRequestPresent(Mission m);

        /// <summary>Our request id is in the mission.</summary>
        bool Has(Mission m, int id);

        /// <summary>Our request at <paramref name="speed"/> - the old one replaced (Remove + Add, the developer slow-mo's own ramp).</summary>
        void Set(Mission m, int id, float speed);

        /// <summary>Our request removed - only when present. Returns whether there was one.</summary>
        bool Remove(Mission m, int id);
    }

    internal sealed class GameSlowMoBody : ISlowMoBody
    {
        public double RealTime => TaleWorlds.Engine.Time.ApplicationTime;

        public bool Paused => MBCommon.IsPaused || (MissionState.Current?.Paused ?? false);

        public bool Multiplayer => GameNetwork.IsSessionActive;

        public double SceneSpeed(Mission m)
        {
            var scene = m?.Scene;
            return scene != null ? scene.TimeSpeed : double.NaN;
        }

        public bool VanillaRequestPresent(Mission m)
        {
            if (m == null) return false;
            foreach (int id in TimedSlowMoMath.VanillaRequestIds)
                if (m.GetRequestedTimeSpeed(id, out _)) return true;
            return false;
        }

        public bool Has(Mission m, int id) => m != null && m.GetRequestedTimeSpeed(id, out _);

        public void Set(Mission m, int id, float speed)
        {
            if (m == null) return;
            if (m.GetRequestedTimeSpeed(id, out _)) m.RemoveTimeSpeedRequest(id);
            m.AddTimeSpeedRequest(new Mission.TimeSpeedRequest(speed, id));
        }

        public bool Remove(Mission m, int id)
        {
            if (m == null || !m.GetRequestedTimeSpeed(id, out _)) return false;
            m.RemoveTimeSpeedRequest(id);
            return true;
        }
    }

    /// <summary>Step 100e: the time-speed reads every new logic starts with (the smoke swaps in its stand-in once).</summary>
    internal static class SlowMoBodyDefault
    {
        internal static ISlowMoBody Current = new GameSlowMoBody();
    }
}

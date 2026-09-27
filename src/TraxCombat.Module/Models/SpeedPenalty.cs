using System.Globalization;
using TaleWorlds.MountAndBlade;

namespace TraxCombat.Models
{
    /// <summary>
    /// The attack-speed penalty on an agent's driven properties (DESIGN §2 "Exhausted", RESEARCH §C):
    /// <see cref="AgentDrivenProperties.SwingSpeedMultiplier"/> (melee swings),
    /// <see cref="AgentDrivenProperties.ThrustOrRangedReadySpeedMultiplier"/> (thrusts, bow draw,
    /// throws) and <see cref="AgentDrivenProperties.ReloadSpeed"/> (crossbow reload), all times the
    /// same factor. Applied by <see cref="TraxAgentStatModel.UpdateAgentStats"/> AFTER the base model,
    /// which assigns these three fresh on every recompute (verified: Sandbox's and CustomBattle's
    /// UpdateHumanStats set them with <c>=</c>), so scaling never compounds.
    /// Left alone on purpose: HandlingMultiplier (defence, not attack speed) and the two Bipedal*
    /// values (global managed parameters, meaning unverified - scaling them too could square the
    /// penalty; RESEARCH §C).
    /// </summary>
    internal static class SpeedPenalty
    {
        public static void Scale(AgentDrivenProperties p, float factor)
        {
            p.SwingSpeedMultiplier *= factor;
            p.ThrustOrRangedReadySpeedMultiplier *= factor;
            p.ReloadSpeed *= factor;
        }

        /// <summary>The three values as the managed side holds them (what was last sent to the engine).</summary>
        public readonly struct Snapshot
        {
            public Snapshot(float swing, float thrust, float reload)
            {
                Swing = swing;
                Thrust = thrust;
                Reload = reload;
            }

            public float Swing { get; }

            public float Thrust { get; }

            public float Reload { get; }

            public bool Valid => Swing > 0 || Thrust > 0 || Reload > 0;

            public static Snapshot Take(Agent agent)
            {
                var p = agent?.AgentDrivenProperties;
                return p == null ? default : new Snapshot(p.SwingSpeedMultiplier, p.ThrustOrRangedReadySpeedMultiplier, p.ReloadSpeed);
            }

            /// <summary>Each value divided by <paramref name="before"/>'s - the factor that really stuck.</summary>
            public string RatioTo(Snapshot before) =>
                "x" + R(Swing, before.Swing) + " / x" + R(Thrust, before.Thrust) + " / x" + R(Reload, before.Reload);

            public override string ToString() =>
                "swing " + F(Swing) + ", thrust/draw " + F(Thrust) + ", reload " + F(Reload);

            private static string F(float v) => v.ToString("0.000", CultureInfo.InvariantCulture);

            private static string R(float now, float before) =>
                before == 0 ? "?" : (now / before).ToString("0.00", CultureInfo.InvariantCulture);
        }
    }
}

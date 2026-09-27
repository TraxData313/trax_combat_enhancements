using System.Globalization;
using TaleWorlds.MountAndBlade;

namespace TraxCombat.Models
{
    /// <summary>
    /// The Athletics penalties on an agent's driven properties (DESIGN §2, RESEARCH §C), applied by
    /// <see cref="TraxAgentStatModel.UpdateAgentStats"/> AFTER the base model:
    ///   ATTACK SPEED (<see cref="Scale"/>): <see cref="AgentDrivenProperties.SwingSpeedMultiplier"/>
    ///     (melee swings), <see cref="AgentDrivenProperties.ThrustOrRangedReadySpeedMultiplier"/>
    ///     (thrusts, bow draw, throws) and <see cref="AgentDrivenProperties.ReloadSpeed"/> (crossbow
    ///     reload), all times the same factor;
    ///   RUN SPEED on foot (<see cref="ScaleRun"/>): <see cref="AgentDrivenProperties.MaxSpeedMultiplier"/>
    ///     only (step 5c);
    ///   A HORSE's speed (<see cref="ScaleMount"/>): <see cref="AgentDrivenProperties.MountSpeed"/> on
    ///     the MOUNT agent's own properties (step 5c; only while MountMinSpeedMultiplier is below 1).
    /// The base models assign every one of these fresh on every recompute (verified: Sandbox's and
    /// CustomBattle's UpdateHumanStats / UpdateHorseStats set them with <c>=</c>; War Sails' naval
    /// model multiplies inside its own BaseModel-first pass, still before us), so scaling never
    /// compounds.
    /// Left alone on purpose: HandlingMultiplier (defence, not attack speed), the two Bipedal*
    /// values (global managed parameters, meaning unverified - scaling them too could square the
    /// penalty; RESEARCH §C) and CombatMaxSpeedMultiplier (a factor ≤ 1 the base models clamp - the
    /// share of the top speed kept in combat stance, so scaling it as well would square the run
    /// penalty while fighting).
    /// </summary>
    internal static class SpeedPenalty
    {
        public static void Scale(AgentDrivenProperties p, float factor)
        {
            p.SwingSpeedMultiplier *= factor;
            p.ThrustOrRangedReadySpeedMultiplier *= factor;
            p.ReloadSpeed *= factor;
        }

        public static void ScaleRun(AgentDrivenProperties p, float factor) => p.MaxSpeedMultiplier *= factor;

        public static void ScaleMount(AgentDrivenProperties p, float factor) => p.MountSpeed *= factor;

        /// <summary>The values as the managed side holds them (what was last sent to the engine).</summary>
        public readonly struct Snapshot
        {
            public Snapshot(float swing, float thrust, float reload, float run)
            {
                Swing = swing;
                Thrust = thrust;
                Reload = reload;
                Run = run;
            }

            public float Swing { get; }

            public float Thrust { get; }

            public float Reload { get; }

            /// <summary>MaxSpeedMultiplier (run speed on foot).</summary>
            public float Run { get; }

            public bool Valid => Swing > 0 || Thrust > 0 || Reload > 0;

            public static Snapshot Take(Agent agent)
            {
                var p = agent?.AgentDrivenProperties;
                return p == null ? default : new Snapshot(p.SwingSpeedMultiplier, p.ThrustOrRangedReadySpeedMultiplier, p.ReloadSpeed, p.MaxSpeedMultiplier);
            }

            /// <summary>Each value divided by <paramref name="before"/>'s - the factor that really stuck.</summary>
            public string RatioTo(Snapshot before) =>
                "x" + R(Swing, before.Swing) + " / x" + R(Thrust, before.Thrust) + " / x" + R(Reload, before.Reload) + ", run x" + R(Run, before.Run);

            public override string ToString() =>
                "swing " + F(Swing) + ", thrust/draw " + F(Thrust) + ", reload " + F(Reload) + ", run " + F(Run);

            private static string F(float v) => v.ToString("0.000", CultureInfo.InvariantCulture);

            private static string R(float now, float before) =>
                before == 0 ? "?" : (now / before).ToString("0.00", CultureInfo.InvariantCulture);
        }
    }
}

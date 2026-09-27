using System;
using System.Globalization;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

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

        /// <summary>
        /// Step 5e, <c>AttackRateAiDecisions</c> (AI_NOTES "Step 5e"): the AI's own attack-decision
        /// values follow the attack multiplier m, so its PAUSE between attacks grows with the
        /// animations: the chance to attack at a decision (<see cref="AgentDrivenProperties.AIAttackOnDecideChance"/>
        /// - the value vanilla lowers itself for defensive orders), to riposte after a parry
        /// (<see cref="AgentDrivenProperties.AIAttackOnParryChance"/>) and to loose
        /// (<see cref="AgentDrivenProperties.AiShootFreq"/>) × m; the aim before a shot
        /// (<see cref="AgentDrivenProperties.AiWaitBeforeShootFactor"/>) ÷ m (0 stays 0). Every one is
        /// assigned with <c>=</c> by the base models' SetAiRelatedProperties on every recompute, so
        /// this never compounds. Left alone on purpose: every defence value, and
        /// AIHoldingReadyMaxDuration (a longer hold keeps the weapon up and the guard down).
        /// </summary>
        public static void ScaleAiDecisions(AgentDrivenProperties p, float m)
        {
            p.AIAttackOnDecideChance = AttackRateMath.ScaleChance(p.AIAttackOnDecideChance, m);
            p.AIAttackOnParryChance = AttackRateMath.ScaleChance(p.AIAttackOnParryChance, m);
            p.AiShootFreq = AttackRateMath.ScaleChance(p.AiShootFreq, m);
            p.AiWaitBeforeShootFactor = AttackRateMath.ScaleWait(p.AiWaitBeforeShootFactor, m);
        }

        /// <summary>The AI decision values (step 5e) as the managed side holds them.</summary>
        public readonly struct AiSnapshot
        {
            public AiSnapshot(float attack, float riposte, float shoot, float aim)
            {
                Attack = attack;
                Riposte = riposte;
                Shoot = shoot;
                Aim = aim;
            }

            /// <summary>AIAttackOnDecideChance.</summary>
            public float Attack { get; }

            /// <summary>AIAttackOnParryChance.</summary>
            public float Riposte { get; }

            /// <summary>AiShootFreq.</summary>
            public float Shoot { get; }

            /// <summary>AiWaitBeforeShootFactor.</summary>
            public float Aim { get; }

            public static AiSnapshot Take(Agent agent)
            {
                var p = agent?.AgentDrivenProperties;
                return p == null ? default : new AiSnapshot(p.AIAttackOnDecideChance, p.AIAttackOnParryChance, p.AiShootFreq, p.AiWaitBeforeShootFactor);
            }

            /// <summary><c>attack chance 0.144 → 0.121 (x0.84), …</c> - before → after with the factor that stuck.</summary>
            public string Change(AiSnapshot before) =>
                "attack chance " + F(before.Attack) + " → " + F(Attack) + " (x" + R(Attack, before.Attack) + "), riposte chance " + F(before.Riposte) + " → " + F(Riposte)
                + " (x" + R(Riposte, before.Riposte) + "), shoot chance " + F(before.Shoot) + " → " + F(Shoot) + " (x" + R(Shoot, before.Shoot)
                + "), aim before a shot " + F(before.Aim) + " → " + F(Aim) + " (x" + R(Aim, before.Aim) + ")";

            /// <summary>Every value moved by its asked factor (chances × <paramref name="chance"/>, the aim × <paramref name="wait"/>);
            /// a value that was 0 before must stay 0.</summary>
            public bool Took(AiSnapshot before, float chance, float wait) =>
                Close(Attack, before.Attack * chance) && Close(Riposte, before.Riposte * chance) && Close(Shoot, before.Shoot * chance) && Close(Aim, before.Aim * wait);

            private static bool Close(float a, float b) => Math.Abs(a - b) <= 0.01f * Math.Max(0.01f, Math.Abs(b)) + 1e-6f;

            private static string F(float v) => v.ToString("0.000", CultureInfo.InvariantCulture);

            private static string R(float now, float before) =>
                before == 0 ? (now == 0 ? "1 (0 stays 0)" : "?") : (now / before).ToString("0.00", CultureInfo.InvariantCulture);
        }

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

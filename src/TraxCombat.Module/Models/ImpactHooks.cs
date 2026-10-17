using System;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TraxCombat.Models
{
    /// <summary>
    /// Step 47: the KnockDown decision on a landed blow - asked by <see cref="TraxDamageModel.DecideAgentKnockedDownByBlow"/> after the base
    /// model (vanilla's answer kept: ours is OR-ed on top). MAIN thread (inside the engine's <c>MeleeHitCallback</c>, before every
    /// behaviour's <c>OnMeleeHit</c> - RESEARCH §R1). Asked for EVERY collision on a man on foot, blocked or not (vanilla throws a blocked
    /// blow's answer away). The spike (step 47) is its first user; I3 / I5's model plug in here.
    /// </summary>
    internal interface IKnockDownDecider
    {
        bool KnockDownOnBlow(Agent attacker, Agent victim, in AttackCollisionData collision, in Blow blow, bool gameSays);
    }

    /// <summary>
    /// Step 47: the stun of a DEFENDED blow - asked by <see cref="TraxDamageModel.CalculateDefendedBlowStunMultipliers"/> after the base
    /// model, before the engine's cap (<c>StunPeriodMax</c>). ANY THREAD (<c>GetDefendCollisionResults</c> is multi-thread callable,
    /// RESEARCH §R2): read-only state, Interlocked counters, no log line, no engine call. The spike's x3 is its first user; I4's stun by
    /// tiredness and the riposte plug in here.
    /// </summary>
    internal interface IBlockStunDecider
    {
        void SetBlockStun(Agent attacker, Agent defender, CombatCollisionResult result, ref float attackerStun, ref float defenderStun);
    }

    /// <summary>
    /// Step 47: the impact system's MODEL seams, shared by the two decorators and the running mission logic. The logic plugs its deciders
    /// in at mission start (and takes them out at its end); the decorators ask them and stay vanilla when none is plugged or one throws.
    /// The game's stun cap is raised / restored here - <c>ManagedParameters</c> is one global array, so a raised cap is put back at the
    /// mission's end and whenever the spike goes off, and every blow nobody lengthened keeps the game's own cap (clamped here).
    /// </summary>
    internal static class ImpactHooks
    {
        private static volatile IKnockDownDecider? _knockDown;
        private static volatile IBlockStunDecider? _blockStun;

        // the stun cap: the game's own value while we hold it raised (NaN = not raised); written on the main thread only
        private static float _vanillaCap = float.NaN;
        private static volatile bool _capRaised;

        // the first exception thrown by a stun decider on any thread - the main thread logs it and disables the lever
        private static Exception? _stunFailure;
        private static int _stunFailures;

        /// <summary>Our own blow is being registered now (main thread): the behaviours' OnAgentHit see it inside this.</summary>
        [ThreadStatic] private static int _ownBlowDepth;

        internal static IKnockDownDecider? KnockDown
        {
            get => _knockDown;
            set => _knockDown = value;
        }

        internal static IBlockStunDecider? BlockStun
        {
            get => _blockStun;
            set => _blockStun = value;
        }

        /// <summary>A blow of ours is inside <c>Agent.RegisterBlow</c> right now (this thread).</summary>
        internal static bool InOwnBlow => _ownBlowDepth > 0;

        internal static void BeginOwnBlow() => _ownBlowDepth++;

        internal static void EndOwnBlow()
        {
            if (_ownBlowDepth > 0) _ownBlowDepth--;
        }

        internal static bool StunCapRaised => _capRaised;

        /// <summary>The game's own cap while ours is raised (else the live value).</summary>
        internal static float VanillaStunCap => _capRaised ? _vanillaCap : ManagedParameters.GetParameter(ManagedParametersEnum.StunPeriodMax);

        /// <summary>Raises the game's <c>StunPeriodMax</c> to <paramref name="seconds"/> (main thread), remembering its own value once; a
        /// cap already above it is left alone. Returns the game's value.</summary>
        internal static float RaiseStunCap(float seconds)
        {
            if (!_capRaised)
            {
                _vanillaCap = ManagedParameters.GetParameter(ManagedParametersEnum.StunPeriodMax);
                if (!(seconds > _vanillaCap)) return _vanillaCap;
                _capRaised = true;
            }
            ManagedParameters.SetParameter(ManagedParametersEnum.StunPeriodMax, seconds);
            return _vanillaCap;
        }

        /// <summary>The game's own cap back (main thread). True = it was raised.</summary>
        internal static bool RestoreStunCap()
        {
            if (!_capRaised) return false;
            ManagedParameters.SetParameter(ManagedParametersEnum.StunPeriodMax, _vanillaCap);
            _capRaised = false;
            return true;
        }

        /// <summary>The decorator's KnockDown question: the game's answer OR the decider's. A throwing decider = the game's answer, logged
        /// once per site by the logic's fail-safe.</summary>
        internal static bool KnockDownOnBlow(Agent attacker, Agent victim, in AttackCollisionData collision, in Blow blow, bool gameSays)
        {
            var d = _knockDown;
            if (d == null || victim == null) return gameSays;
            try
            {
                return d.KnockDownOnBlow(attacker, victim, in collision, in blow, gameSays) || gameSays;
            }
            catch (Exception e)
            {
                TraxCombat.Missions.AthleticsLogic.Failed("impact.knockdown-hook", e);
                return gameSays;
            }
        }

        /// <summary>
        /// The decorator's stun step (any thread): the decider (if any) on the base model's stun; then, while the cap is raised, every stun
        /// the decider did not lengthen keeps the game's own cap. A throwing decider = the base's stun, the exception kept for the main
        /// thread (<see cref="TakeStunFailure"/>).
        /// </summary>
        internal static void AfterDefendedBlowStun(Agent attacker, Agent defender, CombatCollisionResult result, ref float attackerStun, ref float defenderStun)
        {
            float a0 = attackerStun, d0 = defenderStun;
            var d = _blockStun;
            if (d != null)
            {
                try
                {
                    d.SetBlockStun(attacker, defender, result, ref attackerStun, ref defenderStun);
                }
                catch (Exception e)
                {
                    attackerStun = a0;
                    defenderStun = d0;
                    if (Interlocked.Increment(ref _stunFailures) == 1) Interlocked.CompareExchange(ref _stunFailure, e, null);
                }
            }
            if (_capRaised)
            {
                float cap = _vanillaCap;
                if (attackerStun == a0 && attackerStun > cap) attackerStun = cap;
                if (defenderStun == d0 && defenderStun > cap) defenderStun = cap;
            }
        }

        /// <summary>The stun decider's first exception since the last take (main thread), or null; the count of failures with it.</summary>
        internal static Exception? TakeStunFailure(out int failures)
        {
            failures = Interlocked.Exchange(ref _stunFailures, 0);
            return Interlocked.Exchange(ref _stunFailure, null);
        }
    }
}

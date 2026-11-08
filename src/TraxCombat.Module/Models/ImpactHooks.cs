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
    /// Step 53: a RIDER knocked off his horse - asked by <see cref="TraxDamageModel.DecideAgentDismountedByBlow"/> after the base model
    /// (vanilla's answer kept: ours is OR-ed on top). MAIN thread (inside <c>Mission.CreateMeleeBlow</c> of the engine's <c>MeleeHitCallback</c>
    /// - Mission.cs 5564, where the engine asks it INSTEAD of the KnockDown question for a man on a horse; the missile path 5870 never ours).
    /// </summary>
    internal interface IDismountDecider
    {
        bool DismountOnBlow(Agent attacker, Agent victim, in AttackCollisionData collision, in Blow blow, bool gameSays);
    }

    /// <summary>
    /// Step 52: <c>ImpactTiredNeverShrugsOff</c> - asked by <see cref="TraxDamageModel.DecideAgentShrugOffBlow"/> only when the game says the
    /// blow is SHRUGGED OFF (no flinch). MAIN thread (inside <c>Mission.CreateMeleeBlow</c> of the engine's <c>MeleeHitCallback</c> - checked
    /// in the decompile, Mission.cs 5555; the missile path 5861 is the main thread too and never ours). <paramref name="gameThreshold"/> = the
    /// base model's own threshold for this blow. True = he still shrugs it off; false = refused (a tired man flinches). Step 62: the blow
    /// itself too (its <c>OwnerId</c> = the attacker) - the shrug-off is the FIRST question of <c>CreateMeleeBlow</c>, so the impact model
    /// decides the blow here and refuses the shrug-off when it knocks him down or staggers him (else the engine plays no reaction).
    /// </summary>
    internal interface IShrugOffDecider
    {
        bool ShrugOffKept(Agent victim, in AttackCollisionData collision, in Blow blow, float gameThreshold);
    }

    /// <summary>
    /// Step 65: THE BRACE - asked by <see cref="TraxDamageModel.DecideAgentKnockedBackByBlow"/> after the base model, only when the game says
    /// KNOCKED BACK (every landed kick / bash, a horse charge, a weapon that knocks back). MAIN thread (inside <c>Mission.CreateMeleeBlow</c> of
    /// the engine's <c>MeleeHitCallback</c> - Mission.cs 5571, after the shrug-off question and before the knockdown one). True = keep the
    /// game's knock-back; false = refuse it (a braced man).
    /// </summary>
    internal interface IKnockBackDecider
    {
        bool KnockBackKept(Agent attacker, Agent victim, in AttackCollisionData collision, in Blow blow);
    }

    /// <summary>
    /// Step 50: the AI's pickup of a dropped SHIELD - asked by <see cref="TraxItemPickupModel.IsItemAvailableForAgent"/> after the base model
    /// (MAIN thread: HumanAIComponent.ItemPickupTick runs in OnTick). True = refuse this pickup (<c>ShieldPickupLockSeconds</c> after a live
    /// kick / bash made his shield leave his hand). The player picks items up with the use key - no model asked, so never locked.
    /// </summary>
    internal interface IShieldPickupLock
    {
        bool ShieldPickupLocked(Agent agent);
    }

    /// <summary>
    /// Step 100d: THE PUSH-OUT'S BASH GUARD - a blocker pushing out on a timed riposte plays his own bash action (actt_weapon_bash), which the
    /// engine may turn into a real bash collision. While his push-out window runs, every alternative-attack collision OWNED BY HIM is no blow:
    /// the damage decorator ignores its damage (<c>IsDamageIgnored</c>), refuses its knock-back / knockdown / dismount and lets it be shrugged
    /// off (no reaction). MAIN thread (inside the engine's <c>MeleeHitCallback</c>). <paramref name="count"/> = count it once (the damage question).
    /// </summary>
    internal interface IPushOutGuard
    {
        bool PushOutBash(int attackerIndex, bool count);
    }

    /// <summary>
    /// Step 100g: THE PERFECT STRIKE - asked by the damage decorator LAST, after the roll, tiredness and the reduction
    /// (<see cref="DamageRandomizer.Apply"/>), for every melee hit of a man on a man (no missile, fall, horse charge, no horse on either end).
    /// Returns the damage to land: x2-x3 for YOUR first swing from the opposite side on a man your timed block staggered, else unchanged.
    /// MAIN thread (inside the engine's <c>MeleeHitCallback</c>).
    /// </summary>
    internal interface IPerfectStrike
    {
        float PerfectStrike(Agent attacker, Agent victim, bool attackerMounted, in AttackCollisionData collision, float damage);
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

        private static volatile IShrugOffDecider? _shrugOff;

        /// <summary>Step 52: the running logic while it runs (the damage decorator's shrug-off asks it).</summary>
        internal static IShrugOffDecider? ShrugOff
        {
            get => _shrugOff;
            set => _shrugOff = value;
        }

        /// <summary>
        /// Step 52: the decorator's shrug-off question after the game said YES: kept, or refused by the decider (a tired man flinches). The
        /// game's threshold is asked of <paramref name="baseModel"/> only when a decider is plugged in; a throwing decider = the game's answer
        /// (kept), logged by the logic's fail-safe.
        /// </summary>
        internal static bool ShrugOffKept(Agent victim, in AttackCollisionData collision, in Blow blow, TaleWorlds.MountAndBlade.ComponentInterfaces.AgentApplyDamageModel baseModel)
        {
            var d = _shrugOff;
            if (d == null || victim == null || baseModel == null) return true;
            try
            {
                return d.ShrugOffKept(victim, in collision, in blow, baseModel.CalculateStaggerThresholdDamage(victim, in blow));
            }
            catch (Exception e)
            {
                TraxCombat.Missions.AthleticsLogic.Failed("impact.shrug-off-hook", e);
                return true;
            }
        }

        private static volatile IDismountDecider? _dismount;

        /// <summary>Step 53: the running logic while it runs (the damage decorator's dismount question asks it).</summary>
        internal static IDismountDecider? Dismount
        {
            get => _dismount;
            set => _dismount = value;
        }

        /// <summary>Step 53: the decorator's dismount question: the game's answer OR the decider's. A throwing decider = the game's answer,
        /// logged by the logic's fail-safe.</summary>
        internal static bool DismountOnBlow(Agent attacker, Agent victim, in AttackCollisionData collision, in Blow blow, bool gameSays)
        {
            var d = _dismount;
            if (d == null || victim == null) return gameSays;
            try
            {
                return d.DismountOnBlow(attacker, victim, in collision, in blow, gameSays) || gameSays;
            }
            catch (Exception e)
            {
                TraxCombat.Missions.AthleticsLogic.Failed("impact.dismount-hook", e);
                return gameSays;
            }
        }

        private static volatile IKnockBackDecider? _knockBack;

        /// <summary>Step 65: the running logic while it runs (the damage decorator's knock-back question asks it).</summary>
        internal static IKnockBackDecider? KnockBack
        {
            get => _knockBack;
            set => _knockBack = value;
        }

        /// <summary>Step 65: the decorator's knock-back question: the game's NO stands; a YES asked of the decider (a braced man's refused). A
        /// throwing decider = the game's answer, logged by the logic's fail-safe.</summary>
        internal static bool KnockBackOnBlow(Agent attacker, Agent victim, in AttackCollisionData collision, in Blow blow, bool gameSays)
        {
            var d = _knockBack;
            if (!gameSays || d == null || victim == null || attacker == null) return gameSays;
            try
            {
                return d.KnockBackKept(attacker, victim, in collision, in blow);
            }
            catch (Exception e)
            {
                TraxCombat.Missions.AthleticsLogic.Failed("impact.knockback-hook", e);
                return gameSays;
            }
        }

        private static volatile IPushOutGuard? _pushOut;

        /// <summary>Step 100d: the running logic while it runs (the damage decorator's push-out bash guard asks it).</summary>
        internal static IPushOutGuard? PushOut
        {
            get => _pushOut;
            set => _pushOut = value;
        }

        /// <summary>Step 100d: is this collision a pushing blocker's own bash (an alternative attack by a man inside his push-out window)? A
        /// throwing guard = no (the game's answer), logged by the logic's fail-safe.</summary>
        internal static bool PushOutBlow(Agent? attacker, in AttackCollisionData collision, bool count = false)
            => attacker != null && PushOutBlow(attacker.Index, in collision, count);

        /// <summary>The same by the blow owner's agent index (the shrug-off question has only the blow).</summary>
        internal static bool PushOutBlow(int attackerIndex, in AttackCollisionData collision, bool count = false)
        {
            var g = _pushOut;
            if (g == null || attackerIndex < 0 || !collision.IsAlternativeAttack) return false;
            try
            {
                return g.PushOutBash(attackerIndex, count);
            }
            catch (Exception e)
            {
                TraxCombat.Missions.AthleticsLogic.Failed("impact.pushout-guard", e);
                return false;
            }
        }

        private static volatile IPerfectStrike? _perfect;

        /// <summary>Step 100g: the running logic while it runs (the damage decorator asks it last).</summary>
        internal static IPerfectStrike? Perfect
        {
            get => _perfect;
            set => _perfect = value;
        }

        /// <summary>Step 100g: the damage to land after the perfect strike's question; a throwing logic = unchanged (logged by its fail-safe).</summary>
        internal static float PerfectStrike(Agent? attacker, Agent? victim, bool attackerMounted, in AttackCollisionData collision, float damage)
        {
            var g = _perfect;
            if (g == null || attacker == null || victim == null) return damage;
            try
            {
                return g.PerfectStrike(attacker, victim, attackerMounted, in collision, damage);
            }
            catch (Exception e)
            {
                TraxCombat.Missions.AthleticsLogic.Failed("impact.perfect-strike-hook", e);
                return damage;
            }
        }

        private static volatile IShieldPickupLock? _pickupLock;

        /// <summary>Step 50: the running logic while it runs (the pickup decorator asks it).</summary>
        internal static IShieldPickupLock? PickupLock
        {
            get => _pickupLock;
            set => _pickupLock = value;
        }

        /// <summary>Step 50: the pickup decorator's question for a SHIELD on the ground: refused? A throwing lock = the game's answer.</summary>
        internal static bool ShieldPickupLocked(Agent agent)
        {
            var l = _pickupLock;
            if (l == null || agent == null) return false;
            try
            {
                return l.ShieldPickupLocked(agent);
            }
            catch (Exception e)
            {
                TraxCombat.Missions.AthleticsLogic.Failed("impact.pickup-lock", e);
                return false;
            }
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

using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Missions;

namespace TraxCombat.Models
{
    /// <summary>Step 76: the two engine reads of the thrust rule - the smoke plays them (its agents have no native side).</summary>
    internal interface IThrustPassReads
    {
        /// <summary><c>Agent.IsFriendOf</c> (native) - the game's own test in <c>CancelsDamageAndBlocksAttackBecauseOfNonEnemyCase</c>.</summary>
        bool Friends(Agent attacker, Agent defender);

        /// <summary>The weapon of the blow is a melee polearm (its usage's class: one / two-handed / low-grip polearm).</summary>
        bool Polearm(in MissionWeapon weapon);
    }

    internal sealed class GameThrustPassReads : IThrustPassReads
    {
        public bool Friends(Agent attacker, Agent defender) => attacker.IsFriendOf(defender);

        public bool Polearm(in MissionWeapon weapon)
        {
            if (weapon.IsEmpty) return false;
            var u = weapon.CurrentUsageItem;
            return u != null && u.IsMeleeWeapon && GameSpearBody.IsPolearmClass(u.WeaponClass);
        }
    }

    /// <summary>
    /// Step 76 - A THRUST PASSES A FRIEND (AI_NOTES "Step 76", DESIGN decision 70). The game (v1.4.8 decompile): <c>Mission.MeleeHitCallback</c>
    /// cancels a FRIEND's damage (<c>CancelsDamageAndBlocksAttackBecauseOfNonEnemyCase</c>: single player, the attacker AI or not, a friend) and sets
    /// the attacker's stun to <c>StunPeriodAttackerFriendlyFire</c> (0.4 s), then asks the damage model's <c>DecideWeaponCollisionReaction</c>, whose
    /// default (<c>MissionCombatMechanicsHelper</c>) answers <c>Bounced</c> for a 0-damage blow - a rear-rank spear stops on its own front man. The
    /// damage decorator asks <see cref="Passes"/> BEFORE the base model: a THRUST (StrikeType thrust, not a kick / bash) into a friend answers
    /// <c>ContinueChecking</c> - the engine keeps checking the weapon's path, so the spear may reach the enemy behind. Swings still bounce. The
    /// friendly-hit stun is the game's global ManagedParameters value: held at <c>FriendlyHitStunSeconds</c> while the rule is on, the game's own given
    /// back at the battle's end / switched off (like step 47's <c>StunPeriodMax</c>). Main thread (an MBCallback); a throw = the game's answer.
    /// </summary>
    internal static class ThrustPass
    {
        /// <summary>The engine reads (the smoke swaps in its stand-in).</summary>
        internal static IThrustPassReads Reads = new GameThrustPassReads();

        private static float _gameStun = float.NaN;
        private static bool _stunHeld;
        private static float _stunNow = float.NaN;

        /// <summary>The decorator's question: true = answer ContinueChecking (the base model is not asked). Counts every friendly contact and every
        /// enemy thrust of a man who just passed one (the running logic's numbers).</summary>
        internal static bool Passes(in AttackCollisionData cd, Agent attacker, Agent defender, in MissionWeapon weapon, bool landed)
        {
            try
            {
                if (attacker == null || defender == null || !cd.IsColliderAgent || cd.IsMissile) return false;
                bool thrust = cd.StrikeType == ThrustPassMath.ThrustStrikeType;
                bool friend = Reads.Friends(attacker, defender);
                var logic = AthleticsLogic.Current;
                if (!friend)
                {
                    if (thrust && !cd.IsAlternativeAttack && logic != null)
                    {
                        bool blocked = cd.AttackBlockedWithShield || cd.CollisionResult == CombatCollisionResult.Blocked
                                       || cd.CollisionResult == CombatCollisionResult.Parried || cd.CollisionResult == CombatCollisionResult.ChamberBlocked;
                        logic.NoteEnemyThrust(attacker, landed && !blocked, blocked);
                    }
                    return false;
                }
                var r = ThrustPassRules.From(TraxSettings.Shared);
                bool polearm = thrust && Reads.Polearm(in weapon);
                var f = new ThrustFacts { HasDefender = true, Friend = true, Alternative = cd.IsAlternativeAttack, Thrust = thrust, Polearm = polearm };
                var v = ThrustPassMath.Decide(in r, in f);
                logic?.NoteFriendlyContact(attacker, defender, v, polearm, cd.AttackBlockedWithShield);
                return v == ThrustVerdict.Passed;
            }
            catch (Exception e)
            {
                AthleticsLogic.Failed("thrust.decide", e);
                return false;
            }
        }

        /// <summary>The friendly-hit stun the game holds now (s).</summary>
        internal static float StunNow => ManagedParameters.GetParameter(ManagedParametersEnum.StunPeriodAttackerFriendlyFire);

        /// <summary>The game's own value while ours is held (else NaN).</summary>
        internal static float GameStun => _stunHeld ? _gameStun : float.NaN;

        internal static bool StunHeld => _stunHeld;

        /// <summary>Holds the game's <c>StunPeriodAttackerFriendlyFire</c> at <paramref name="seconds"/> (main thread), remembering its own value once.
        /// True when it wrote.</summary>
        internal static bool HoldStun(float seconds)
        {
            if (!_stunHeld)
            {
                _gameStun = StunNow;
                _stunHeld = true;
            }
            else if (_stunNow == seconds) return false;
            ManagedParameters.SetParameter(ManagedParametersEnum.StunPeriodAttackerFriendlyFire, seconds);
            _stunNow = seconds;
            return true;
        }

        /// <summary>The game's own value back (main thread). True = it was held.</summary>
        internal static bool RestoreStun()
        {
            if (!_stunHeld) return false;
            ManagedParameters.SetParameter(ManagedParametersEnum.StunPeriodAttackerFriendlyFire, _gameStun);
            _stunHeld = false;
            _stunNow = float.NaN;
            return true;
        }
    }
}

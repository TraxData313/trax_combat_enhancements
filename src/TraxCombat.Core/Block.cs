using System;

namespace TraxCombat.Core
{
    /// <summary>How a melee blow was blocked - what the DEFENDER pays for it (step 22, DESIGN §2 "Defending costs too").</summary>
    public enum BlockKind
    {
        /// <summary>Not a block (a landed blow, a miss, a missile, a kick or bash, a horse charge…) - nothing to pay.</summary>
        None = 0,

        /// <summary>Blocked with the shield, on the correct side (the engine's <c>CorrectSideShieldBlock</c>) - CostPerShieldBlock.</summary>
        ShieldRightSide = 1,

        /// <summary>Blocked with the shield on the wrong side - CostPerWrongSideShieldBlock.</summary>
        ShieldWrongSide = 2,

        /// <summary>Blocked, parried or chamber-blocked with a weapon (no shield) - CostPerWeaponParry.</summary>
        WeaponParry = 3,
    }

    /// <summary>
    /// Step 22 (Anton, 2026-09-29: "make defending cost some athletics: defending with shield in the right direction 1,
    /// with shield - wrong direction 5, without shield - 2"): which collisions are blocks and what they are called.
    /// Pure - the module hands in the engine's <c>AttackCollisionData</c> fields (v1.4.8, verified in the decompiled
    /// <c>Mission.MeleeHitCallback</c>: <c>OnMeleeHit</c> fires once per melee collision; a shield block has
    /// <c>AttackBlockedWithShield</c>, its side <c>CorrectSideShieldBlock</c>; a weapon block has the collision result
    /// Blocked / Parried / ChamberBlocked without the shield flag).
    /// </summary>
    public static class BlockMath
    {
        /// <summary>The engine's <c>CombatCollisionResult</c> values (the smoke checks them against the game's enum).</summary>
        public const int ResultBlocked = 3;
        public const int ResultParried = 4;
        public const int ResultChamberBlocked = 5;

        /// <summary>
        /// The block kind of one melee collision. Only a real MELEE BLOW counts: a missile (blocked missiles are free -
        /// DESIGN), a kick or shield bash (<paramref name="isAlternativeAttack"/> - not blows since step 18), a horse
        /// charge and a hit that the shield ON THE BACK stopped (he did not defend) are None. Then: the shield flag →
        /// right or wrong side by <paramref name="correctSideShieldBlock"/>; else a blocked / parried / chamber-blocked
        /// result → a weapon parry; anything else (a landed blow, the world) → None.
        /// </summary>
        public static BlockKind KindOf(bool isMissile, bool isAlternativeAttack, bool isHorseCharge, bool collidedWithShieldOnBack,
            bool attackBlockedWithShield, bool correctSideShieldBlock, int collisionResult)
        {
            if (isMissile || isAlternativeAttack || isHorseCharge || collidedWithShieldOnBack) return BlockKind.None;
            if (attackBlockedWithShield) return correctSideShieldBlock ? BlockKind.ShieldRightSide : BlockKind.ShieldWrongSide;
            return collisionResult == ResultBlocked || collisionResult == ResultParried || collisionResult == ResultChamberBlocked
                ? BlockKind.WeaponParry
                : BlockKind.None;
        }

        /// <summary>The kind in the log's words.</summary>
        public static string Name(BlockKind kind) => kind switch
        {
            BlockKind.ShieldRightSide => "shield block (right side)",
            BlockKind.ShieldWrongSide => "shield block (WRONG side)",
            BlockKind.WeaponParry => "weapon parry",
            _ => "no block",
        };

        /// <summary>The setting's key for a kind (the log names the rule).</summary>
        public static string SettingKey(BlockKind kind) => kind switch
        {
            BlockKind.ShieldRightSide => SettingsSchema.CostPerShieldBlock.Key,
            BlockKind.ShieldWrongSide => SettingsSchema.CostPerWrongSideShieldBlock.Key,
            BlockKind.WeaponParry => SettingsSchema.CostPerWeaponParry.Key,
            _ => "none",
        };

        /// <summary>Kinds, for arrays indexed by <see cref="BlockKind"/> (0 = None, unused).</summary>
        public const int KindCount = 4;
    }

    /// <summary>
    /// ONE charge per blocked blow for one defender (step 22). The engine calls <c>OnMeleeHit</c> once per collision,
    /// but a swing can touch the same guard twice (a blade and its follow-through, a couched lance along a shield) -
    /// so a second block of the SAME attacker's SAME swing is not charged again. Keyed on the attacker (his agent
    /// index) and his swing (the module's per-attacker release counter; 0 = unknown - a couched lance, an untracked
    /// attacker): the same attacker within <see cref="SameBlowSeconds"/> with the same swing (or no swing known) is
    /// the same blow; a new swing, another attacker or a later time is a new one. A struct kept on the defender's
    /// record - no allocation.
    /// </summary>
    public struct BlockTracker
    {
        /// <summary>A block of the same attacker's same swing within this many seconds is the same blow (a swing's
        /// release is well under a second; a chained blow has a new swing number anyway). Plumbing, like step 18's
        /// SameActionSeconds.</summary>
        public const double SameBlowSeconds = 1.0;

        private int _attacker;
        private int _swing;
        private double _time;
        private bool _any;

        /// <summary>True when this block is a NEW blow (and remembers it); false for the same blow seen again.</summary>
        public bool IsNew(int attackerIndex, int attackerSwing, double now)
        {
            bool same = _any && attackerIndex == _attacker && now - _time < SameBlowSeconds && now >= _time
                        && (attackerSwing <= 0 || attackerSwing == _swing);
            if (same) return false;
            _any = true;
            _attacker = attackerIndex;
            _swing = attackerSwing;
            _time = now;
            return true;
        }

        /// <summary>Forget the last blow (a fresh record).</summary>
        public void Reset() => _any = false;
    }
}

using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ComponentInterfaces;

namespace TraxCombat.Models
{
    /// <summary>
    /// The damage-randomness hook (feature 1, step 4) - a DECORATOR over whatever
    /// <see cref="AgentApplyDamageModel"/> was registered before us (Sandbox's in a campaign,
    /// CustomBattle's in a custom battle, War Sails' naval one on top of those). NEVER a
    /// subclass of a Default*/Sandbox* model: AddModel replaces by base type, so a subclass
    /// would silently drop War Sails' and other mods' versions (CLAUDE.md).
    ///
    /// Step 3: a pure pass-through - every member forwards to <c>BaseModel</c>, so vanilla
    /// damage is untouched and the registration chain is proven before the feature lands.
    /// Step 4 changes <see cref="ApplyGeneralDamageModifiers"/> only - the last step of the
    /// game's non-virtual <c>CalculateDamage</c>, after armor (RESEARCH §A).
    ///
    /// Registered in SubModule.OnGameStart only when a damage model already exists, so
    /// BaseModel is never null.
    /// </summary>
    public sealed class TraxDamageModel : AgentApplyDamageModel
    {
        /// <summary>The model we wrap, for the load log.</summary>
        internal string BaseModelName => BaseModel?.GetType().FullName ?? "(none)";

        public override bool IsDamageIgnored(in AttackInformation attackInformation, in AttackCollisionData collisionData)
            => BaseModel.IsDamageIgnored(in attackInformation, in collisionData);

        public override float ApplyDamageAmplifications(in AttackInformation attackInformation, in AttackCollisionData collisionData, float baseDamage)
            => BaseModel.ApplyDamageAmplifications(in attackInformation, in collisionData, baseDamage);

        public override float ApplyDamageScaling(in AttackInformation attackInformation, in AttackCollisionData collisionData, float baseDamage)
            => BaseModel.ApplyDamageScaling(in attackInformation, in collisionData, baseDamage);

        public override float ApplyDamageReductions(in AttackInformation attackInformation, in AttackCollisionData collisionData, float baseDamage)
            => BaseModel.ApplyDamageReductions(in attackInformation, in collisionData, baseDamage);

        /// <summary>Step 4 multiplies the random factor in here (after the base model).</summary>
        public override float ApplyGeneralDamageModifiers(in AttackInformation attackInformation, in AttackCollisionData collisionData, float baseDamage)
            => BaseModel.ApplyGeneralDamageModifiers(in attackInformation, in collisionData, baseDamage);

        public override void DecideMissileWeaponFlags(Agent attackerAgent, in MissionWeapon missileWeapon, ref WeaponFlags missileWeaponFlags)
            => BaseModel.DecideMissileWeaponFlags(attackerAgent, in missileWeapon, ref missileWeaponFlags);

        public override void CalculateDefendedBlowStunMultipliers(Agent attackerAgent, Agent defenderAgent, CombatCollisionResult collisionResult, WeaponComponentData attackerWeapon, WeaponComponentData defenderWeapon, ref float attackerStunPeriod, ref float defenderStunPeriod)
            => BaseModel.CalculateDefendedBlowStunMultipliers(attackerAgent, defenderAgent, collisionResult, attackerWeapon, defenderWeapon, ref attackerStunPeriod, ref defenderStunPeriod);

        public override float CalculateStaggerThresholdDamage(Agent defenderAgent, in Blow blow)
            => BaseModel.CalculateStaggerThresholdDamage(defenderAgent, in blow);

        public override float CalculateAlternativeAttackDamage(in AttackInformation attackInformation, in AttackCollisionData collisionData, WeaponComponentData weapon)
            => BaseModel.CalculateAlternativeAttackDamage(in attackInformation, in collisionData, weapon);

        public override float CalculatePassiveAttackDamage(BasicCharacterObject attackerCharacter, in AttackCollisionData collisionData, float baseDamage)
            => BaseModel.CalculatePassiveAttackDamage(attackerCharacter, in collisionData, baseDamage);

        public override MeleeCollisionReaction DecidePassiveAttackCollisionReaction(Agent attacker, Agent defender, bool isFatalHit)
            => BaseModel.DecidePassiveAttackCollisionReaction(attacker, defender, isFatalHit);

        public override void DecideWeaponCollisionReaction(in Blow registeredBlow, in AttackCollisionData collisionData, Agent attacker, Agent defender, in MissionWeapon attackerWeapon, bool isFatalHit, bool isShruggedOff, float momentumRemaining, out MeleeCollisionReaction colReaction)
            => BaseModel.DecideWeaponCollisionReaction(in registeredBlow, in collisionData, attacker, defender, in attackerWeapon, isFatalHit, isShruggedOff, momentumRemaining, out colReaction);

        public override float CalculateShieldDamage(in AttackInformation attackInformation, float baseDamage)
            => BaseModel.CalculateShieldDamage(in attackInformation, baseDamage);

        public override float CalculateSailFireDamage(Agent attackerAgent, IShipOrigin shipOrigin, float baseDamage, bool damageFromShipMachine)
            => BaseModel.CalculateSailFireDamage(attackerAgent, shipOrigin, baseDamage, damageFromShipMachine);

        public override float CalculateHullFireDamage(float baseFireDamage, IShipOrigin shipOrigin)
            => BaseModel.CalculateHullFireDamage(baseFireDamage, shipOrigin);

        public override float GetDamageMultiplierForBodyPart(BoneBodyPartType bodyPart, DamageTypes type, bool isHuman, bool isMissile)
            => BaseModel.GetDamageMultiplierForBodyPart(bodyPart, type, isHuman, isMissile);

        public override bool CanWeaponIgnoreFriendlyFireChecks(WeaponComponentData weapon)
            => BaseModel.CanWeaponIgnoreFriendlyFireChecks(weapon);

        public override bool CanWeaponDealSneakAttack(in AttackInformation attackInformation, WeaponComponentData weapon)
            => BaseModel.CanWeaponDealSneakAttack(in attackInformation, weapon);

        public override bool CanWeaponDismount(Agent attackerAgent, WeaponComponentData attackerWeapon, in Blow blow, in AttackCollisionData collisionData)
            => BaseModel.CanWeaponDismount(attackerAgent, attackerWeapon, in blow, in collisionData);

        public override bool CanWeaponKnockback(Agent attackerAgent, WeaponComponentData attackerWeapon, in Blow blow, in AttackCollisionData collisionData)
            => BaseModel.CanWeaponKnockback(attackerAgent, attackerWeapon, in blow, in collisionData);

        public override bool CanWeaponKnockDown(Agent attackerAgent, Agent victimAgent, WeaponComponentData attackerWeapon, in Blow blow, in AttackCollisionData collisionData)
            => BaseModel.CanWeaponKnockDown(attackerAgent, victimAgent, attackerWeapon, in blow, in collisionData);

        public override bool DecideCrushedThrough(Agent attackerAgent, Agent defenderAgent, float totalAttackEnergy, Agent.UsageDirection attackDirection, StrikeType strikeType, WeaponComponentData defendItem, bool isPassiveUsageHit)
            => BaseModel.DecideCrushedThrough(attackerAgent, defenderAgent, totalAttackEnergy, attackDirection, strikeType, defendItem, isPassiveUsageHit);

        public override float CalculateRemainingMomentum(float originalMomentum, in Blow b, in AttackCollisionData collisionData, Agent attacker, Agent victim, in MissionWeapon attackerWeapon, bool isCrushThrough)
            => BaseModel.CalculateRemainingMomentum(originalMomentum, in b, in collisionData, attacker, victim, in attackerWeapon, isCrushThrough);

        public override bool DecideAgentShrugOffBlow(Agent victimAgent, in AttackCollisionData collisionData, in Blow blow)
            => BaseModel.DecideAgentShrugOffBlow(victimAgent, in collisionData, in blow);

        public override bool DecideAgentDismountedByBlow(Agent attackerAgent, Agent victimAgent, in AttackCollisionData collisionData, WeaponComponentData attackerWeapon, in Blow blow)
            => BaseModel.DecideAgentDismountedByBlow(attackerAgent, victimAgent, in collisionData, attackerWeapon, in blow);

        public override bool DecideAgentKnockedBackByBlow(Agent attackerAgent, Agent victimAgent, in AttackCollisionData collisionData, WeaponComponentData attackerWeapon, in Blow blow)
            => BaseModel.DecideAgentKnockedBackByBlow(attackerAgent, victimAgent, in collisionData, attackerWeapon, in blow);

        public override bool DecideAgentKnockedDownByBlow(Agent attackerAgent, Agent victimAgent, in AttackCollisionData collisionData, WeaponComponentData attackerWeapon, in Blow blow)
            => BaseModel.DecideAgentKnockedDownByBlow(attackerAgent, victimAgent, in collisionData, attackerWeapon, in blow);

        public override bool DecideMountRearedByBlow(Agent attackerAgent, Agent victimAgent, in AttackCollisionData collisionData, WeaponComponentData attackerWeapon, in Blow blow)
            => BaseModel.DecideMountRearedByBlow(attackerAgent, victimAgent, in collisionData, attackerWeapon, in blow);

        public override bool ShouldMissilePassThroughAfterShieldBreak(Agent attackerAgent, WeaponComponentData attackerWeapon)
            => BaseModel.ShouldMissilePassThroughAfterShieldBreak(attackerAgent, attackerWeapon);

        public override float GetDismountPenetration(Agent attackerAgent, WeaponComponentData attackerWeapon, in Blow blow, in AttackCollisionData collisionData)
            => BaseModel.GetDismountPenetration(attackerAgent, attackerWeapon, in blow, in collisionData);

        public override float GetKnockBackPenetration(Agent attackerAgent, WeaponComponentData attackerWeapon, in Blow blow, in AttackCollisionData collisionData)
            => BaseModel.GetKnockBackPenetration(attackerAgent, attackerWeapon, in blow, in collisionData);

        public override float GetKnockDownPenetration(Agent attackerAgent, WeaponComponentData attackerWeapon, in Blow blow, in AttackCollisionData collisionData)
            => BaseModel.GetKnockDownPenetration(attackerAgent, attackerWeapon, in blow, in collisionData);

        public override float GetHorseChargePenetration()
            => BaseModel.GetHorseChargePenetration();
    }
}

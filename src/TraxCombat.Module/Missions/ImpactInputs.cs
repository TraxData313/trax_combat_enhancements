using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 48 (IMPACT I2): the impact model's ENGINE inputs for one melee event - what the attacker struck with, the defender's guard,
    /// the skills that go with them, a shield in his hand, both mounts, the damage and his full health (RESEARCH §R7). The logic fills the
    /// Athletics parts (points, bar, f, the swing's animation share, the Athletics skill for a kick / no guard) itself. MAIN thread (the hit
    /// callback, the damage decorator's KnockDown seam). The smoke plays it (a native read on a fake agent would crash).
    /// </summary>
    internal interface IImpactInputs
    {
        /// <summary>Fills the engine's side of <paramref name="f"/> (its <see cref="ImpactFacts.Event"/> already set).</summary>
        void Fill(Agent attacker, Agent victim, in AttackCollisionData cd, ref ImpactFacts f);

        /// <summary>Step 51: what the attacker struck with, in words, for a YOU line ("Sturgian Axe"; null = unknown). Asked only when a YOU
        /// line may be written (it allocates the name's string).</summary>
        string? BlowName(Agent attacker, in AttackCollisionData cd);

        /// <summary>Step 62: a man's toughness kit (courage's measure - his armour sum and level; read once a man, cached by the logic).</summary>
        void Kit(Agent a, out double armour, out int level);

        /// <summary>Step 62: his health ÷ its limit now (the toughness's health term).</summary>
        double HealthShare(Agent a);
    }

    /// <summary>
    /// Step 48: the game's reads - all managed (the wielded indices are pointer reads, the items and their usages managed objects, the
    /// collision a struct): the weapon in the collision's slot (<c>AffectorWeaponSlotOrMissileIndex</c>) - its item weight, the modified
    /// swing or thrust speed for the strike type (the collision's <c>StrikeType</c>), the collision's damage type (Cut / Pierce / Blunt), the
    /// attacker's skill in its relevant skill; a bash = the shield in that slot and one-handed; the defender's off-hand shield (right / wrong
    /// side, one-handed) or main-hand weapon (a parry, its relevant skill); <c>MountAgent</c>; <c>HealthLimit</c>; the collision's
    /// <c>InflictedDamage</c>. Step 52: the attacker's speed into the blow (<see cref="SpeedInto"/> - engine reads of both men's position and
    /// his velocity, main thread) and a two-handed grip. Nothing allocates.
    /// </summary>
    internal sealed class GameImpactInputs : IImpactInputs
    {
        public void Fill(Agent attacker, Agent victim, in AttackCollisionData cd, ref ImpactFacts f)
        {
            f.AttackerMounted = attacker.MountAgent != null;
            f.DefenderMounted = victim.MountAgent != null;
            // step 52: MOMENTUM; step 53: a rider's = his horse's (vanilla's rider velocity), never a couched lance's (a passive attack)
            f.AttackerSpeed = f.AttackerMounted ? RiderSpeedInto(attacker, victim) : SpeedInto(attacker, victim);
            f.AttackerPassive = f.AttackerMounted && attacker.IsDoingPassiveAttack;
            var off = victim.WieldedOffhandWeapon;
            var offUsage = off.CurrentUsageItem;
            bool shield = offUsage != null && offUsage.IsShield;
            f.DefenderHasShield = shield;
            f.Damage = cd.InflictedDamage;
            f.DefenderMaxHealth = victim.HealthLimit;
            f.Fatal = ImpactMath.IsLanded(f.Event) && ImpactFallMath.Fatal(victim.Health, cd.InflictedDamage); // step 62: he dies of it
            f.DamageType = TypeOf(cd.DamageType);
            int slot = cd.AffectorWeaponSlotOrMissileIndex;
            if (f.Event == ImpactEvent.Bash)
            {
                if (slot >= 0) f.WeaponWeight = WeightOf(attacker.Equipment[slot]);
                f.AttackerSkill = Skill(attacker, DefaultSkills.OneHanded);
            }
            else if (f.Event != ImpactEvent.Kick && slot >= 0)
            {
                var w = attacker.Equipment[slot];
                var usage = w.CurrentUsageItem;
                if (usage != null)
                {
                    f.WeaponWeight = WeightOf(w);
                    f.SwingSpeed = cd.StrikeType == (int)StrikeType.Thrust ? w.GetModifiedThrustSpeedForCurrentUsage() : w.GetModifiedSwingSpeedForCurrentUsage();
                    f.TwoHanded = usage.IsTwoHanded || usage.IsPolearm; // step 52: the clean hits' summary class (a two-handed grip)
                    var skill = usage.RelevantSkill;
                    if (skill != null) f.AttackerSkill = Skill(attacker, skill);
                }
            }
            if (ImpactMath.IsLanded(f.Event)) // step 62: a ranged man caught in melee (a bow / crossbow / sling in his hand - not a thrown weapon)
            {
                var held = victim.WieldedWeapon.CurrentUsageItem;
                f.DefenderRangedInHand = held != null && held.IsRangedWeapon && !held.IsConsumable;
                f.AttackAngleDegrees = AngleFrom(victim, attacker); // step 62: front / side / behind
            }
            switch (f.Event)
            {
                case ImpactEvent.RightBlock:
                case ImpactEvent.WrongBlock:
                    if (shield) f.GuardWeight = WeightOf(off);
                    f.DefenderSkill = Skill(victim, DefaultSkills.OneHanded);
                    break;
                case ImpactEvent.Parry:
                    var main = victim.WieldedWeapon;
                    var mainUsage = main.CurrentUsageItem;
                    if (mainUsage != null)
                    {
                        f.GuardWeight = WeightOf(main);
                        var skill = mainUsage.RelevantSkill;
                        if (skill != null) f.DefenderSkill = Skill(victim, skill);
                    }
                    break;
            }
        }

        public void Kit(Agent a, out double armour, out int level) => GameCourageBody.ArmourAndLevel(a, out armour, out level);

        public double HealthShare(Agent a) => GameCourageBody.HealthShareOf(a);

        public string? BlowName(Agent attacker, in AttackCollisionData cd)
        {
            int slot = cd.AffectorWeaponSlotOrMissileIndex;
            if (slot < 0 || slot >= (int)EquipmentIndex.NumAllWeaponSlots) return null;
            var item = attacker.Equipment[slot].Item;
            return item?.Name?.ToString();
        }

        /// <summary>
        /// Step 52, MOMENTUM: the attacker's own speed INTO the blow (m/s) - his ground velocity the way vanilla's combat code builds it
        /// (MissionCombatMechanicsHelper.GetAgentVelocityContribution: <c>MovementVelocity</c> turned by <c>MovementDirectionAsAngle</c>)
        /// along the line to the victim. Negative (moving away) is returned as is - the model clamps it to none. Four engine reads, per event.
        /// </summary>
        internal static double SpeedInto(Agent attacker, Agent victim)
        {
            Vec2 v = attacker.MovementVelocity;
            v.RotateCCW(attacker.MovementDirectionAsAngle);
            Vec2 to = victim.Position.AsVec2 - attacker.Position.AsVec2;
            float len = to.Normalize();
            if (!(len > 1e-4f)) return 0;
            return Vec2.DotProduct(v, to);
        }

        /// <summary>
        /// Step 53, a RIDER's momentum: his speed into the blow the way vanilla builds a mounted agent's velocity
        /// (MissionCombatMechanicsHelper.GetAgentVelocityContribution: the rider's <c>MovementVelocity.y</c> × his mount's
        /// <c>GetMovementDirection()</c>) along the line to the victim. Engine reads, main thread.
        /// </summary>
        internal static double RiderSpeedInto(Agent attacker, Agent victim)
        {
            var mount = attacker.MountAgent;
            if (mount == null) return SpeedInto(attacker, victim);
            Vec2 v = attacker.MovementVelocity.y * mount.GetMovementDirection();
            Vec2 to = victim.Position.AsVec2 - attacker.Position.AsVec2;
            float len = to.Normalize();
            if (!(len > 1e-4f)) return 0;
            return Vec2.DotProduct(v, to);
        }

        /// <summary>
        /// Step 62, DIRECTION: the angle (degrees, 0-180) between the victim's body facing (<c>GetMovementDirection()</c> - the direction his
        /// body turns to, from the native <c>MovementDirectionAsAngle</c>) and the line from him to the attacker: 0 = struck from the front,
        /// 180 = from straight behind. Engine reads, main thread; 0 when either line is degenerate.
        /// </summary>
        internal static double AngleFrom(Agent victim, Agent attacker)
        {
            Vec2 facing = victim.GetMovementDirection();
            Vec2 to = attacker.Position.AsVec2 - victim.Position.AsVec2;
            float len = to.Normalize();
            float fl = facing.Normalize();
            if (!(len > 1e-4f) || !(fl > 1e-4f)) return 0;
            double cos = Vec2.DotProduct(facing, to);
            if (cos > 1) cos = 1;
            if (cos < -1) cos = -1;
            return Math.Acos(cos) * 180.0 / Math.PI;
        }

        private static double WeightOf(MissionWeapon w) => w.Item != null ? w.Item.Weight : 0;

        private static double Skill(Agent a, SkillObject skill)
        {
            var c = a.Character;
            return c != null ? c.GetSkillValue(skill) : 0;
        }

        /// <summary>The engine's DamageTypes (Cut 0, Pierce 1, Blunt 2; Invalid = a cut).</summary>
        internal static ImpactDamageType TypeOf(int damageType) => damageType switch
        {
            (int)DamageTypes.Pierce => ImpactDamageType.Pierce,
            (int)DamageTypes.Blunt => ImpactDamageType.Blunt,
            _ => ImpactDamageType.Cut,
        };
    }

    /// <summary>Step 48: the inputs every new logic starts with (the smoke swaps in its stand-in once).</summary>
    internal static class ImpactInputsDefault
    {
        internal static IImpactInputs Current = new GameImpactInputs();
    }
}

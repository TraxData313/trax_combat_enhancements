using TaleWorlds.Core;
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
    }

    /// <summary>
    /// Step 48: the game's reads - all managed (the wielded indices are pointer reads, the items and their usages managed objects, the
    /// collision a struct): the weapon in the collision's slot (<c>AffectorWeaponSlotOrMissileIndex</c>) - its item weight, the modified
    /// swing or thrust speed for the strike type (the collision's <c>StrikeType</c>), the collision's damage type (Cut / Pierce / Blunt), the
    /// attacker's skill in its relevant skill; a bash = the shield in that slot and one-handed; the defender's off-hand shield (right / wrong
    /// side, one-handed) or main-hand weapon (a parry, its relevant skill); <c>MountAgent</c>; <c>HealthLimit</c>; the collision's
    /// <c>InflictedDamage</c>. Nothing allocates.
    /// </summary>
    internal sealed class GameImpactInputs : IImpactInputs
    {
        public void Fill(Agent attacker, Agent victim, in AttackCollisionData cd, ref ImpactFacts f)
        {
            f.AttackerMounted = attacker.MountAgent != null;
            f.DefenderMounted = victim.MountAgent != null;
            var off = victim.WieldedOffhandWeapon;
            var offUsage = off.CurrentUsageItem;
            bool shield = offUsage != null && offUsage.IsShield;
            f.DefenderHasShield = shield;
            f.Damage = cd.InflictedDamage;
            f.DefenderMaxHealth = victim.HealthLimit;
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
                    var skill = usage.RelevantSkill;
                    if (skill != null) f.AttackerSkill = Skill(attacker, skill);
                }
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

using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Models;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 47: every GAME call of the impact system's ACTION levers behind one seam (the step-23 / step-41 pattern - BraceBody, WallBody):
    /// the logic keeps the rules, the timing, the watch list and the stats; this touches the engine. The spike (step 47) is the first user,
    /// steps 48-53 (the impact model) the next - so each method is one lever, named for what it does, with its refusals inside. The real
    /// one is <see cref="GameImpactBody"/>; the offline smoke plays a stand-in (its agents have no native side). MAIN THREAD - from the
    /// logic's tick or an engine callback (never the decorators' worker threads).
    /// </summary>
    internal interface IImpactBody
    {
        /// <summary>He is on the field (the engine's state).</summary>
        bool IsActive(TrackedAgent st);

        /// <summary>He rides a horse (managed read).</summary>
        bool IsMounted(TrackedAgent st);

        /// <summary>May an action of ours start on him? <see cref="ForceRefusal.None"/> = yes; never a horse, a rider, a man on a siege engine
        /// / ladder, in water, or already down (RBM crashed a horse with a forced stagger - RESEARCH §R3.4).</summary>
        ForceRefusal CanForce(TrackedAgent st);

        /// <summary>His two channels now: the action TYPE and the action INDEX on channel 0 (whole body) and 1 (upper body).</summary>
        void Read(TrackedAgent st, ref ActionRead read);

        /// <summary>SlowReaction: the action now on <paramref name="channel"/> plays at <paramref name="speed"/> (absolute; 0.5 = twice as
        /// long) - <c>Agent.SetCurrentActionSpeed</c>, for a reaction the ENGINE started (a reel, a fall-and-rise) the tick after it showed.</summary>
        void SlowReaction(TrackedAgent st, int channel, float speed);

        /// <summary>ForceStagger: <c>act_stagger_*</c> on channel 0 at <paramref name="speed"/>; true = the engine took it, its action index
        /// out. Refuses (false, no engine call) whatever <see cref="CanForce"/> refuses.</summary>
        bool ForceStagger(TrackedAgent st, StaggerKind kind, float speed, bool ignorePriority, out int actionIndex);

        /// <summary>The shield in his off hand: its equipment slot (false = no shield in hand).</summary>
        bool TryShieldInHand(TrackedAgent st, out int slot);

        /// <summary>That slot still holds a shield.</summary>
        bool SlotHoldsShield(TrackedAgent st, int slot);

        /// <summary>Any shield anywhere in his equipment (picked one up again).</summary>
        bool HasAnyShield(TrackedAgent st);

        /// <summary>DropShield: the item in <paramref name="slot"/> drops to the ground as a pickable item - QUEUED with
        /// <c>Mission.AddTickAction(DropItem)</c>, vanilla's way from a callback (BannerBearerLogic). True = queued.</summary>
        bool DropShield(TrackedAgent st, int slot);

        /// <summary>OwnKnockdownBlow: a blow of our own on <paramref name="victim"/> - KnockDown | KnockBack, <paramref name="damage"/> (≥ 1,
        /// or the engine plays nothing), owned by <paramref name="attacker"/> (the kill credit if it kills) - vanilla's drowning-blow recipe
        /// through the public <c>Agent.RegisterBlow</c>. The behaviours' OnAgentHit see it inside <see cref="ImpactHooks.InOwnBlow"/>. True =
        /// registered.</summary>
        bool OwnKnockdownBlow(TrackedAgent victim, TrackedAgent? attacker, int damage);
    }

    /// <summary>
    /// The engine side (RESEARCH §R3-§R6, verified in the v1.4.8 decompile): <c>GetCurrentActionType</c> (native) and
    /// <c>GetCurrentAction(ch).Index</c> (a pointer read), <c>SetCurrentActionSpeed</c>, <c>SetActionChannel(0, act_stagger_*)</c>,
    /// <c>GetOffhandWieldedItemIndex</c> + the managed equipment, <c>Mission.AddTickAction(DropItem)</c>, <c>Agent.RegisterBlow</c>.
    /// </summary>
    internal sealed class GameImpactBody : IImpactBody
    {
        public bool IsActive(TrackedAgent st) => st.Agent.IsActive();

        public bool IsMounted(TrackedAgent st) => st.Agent.MountAgent != null;

        public ForceRefusal CanForce(TrackedAgent st)
        {
            var a = st.Agent;
            if (st.Removed || !a.IsActive()) return ForceRefusal.Gone;
            if (!a.IsHuman) return ForceRefusal.NotHuman;
            if (a.MountAgent != null) return ForceRefusal.Mounted;
            if (a.IsUsingGameObject) return ForceRefusal.GameObject;
            if (!a.IsOnLand()) return ForceRefusal.Water;
            if ((int)a.GetCurrentActionType(0) == ImpactActions.Fall) return ForceRefusal.Down;
            return ForceRefusal.None;
        }

        public void Read(TrackedAgent st, ref ActionRead read)
        {
            var a = st.Agent;
            read.Type0 = (int)a.GetCurrentActionType(0);
            read.Index0 = a.GetCurrentAction(0).Index;
            read.Type1 = (int)a.GetCurrentActionType(1);
            read.Index1 = a.GetCurrentAction(1).Index;
        }

        public void SlowReaction(TrackedAgent st, int channel, float speed) => st.Agent.SetCurrentActionSpeed(channel, speed);

        public bool ForceStagger(TrackedAgent st, StaggerKind kind, float speed, bool ignorePriority, out int actionIndex)
        {
            actionIndex = -1;
            if (CanForce(st) != ForceRefusal.None) return false; // never a horse, a rider, … - checked again right before the call
            var act = kind switch
            {
                StaggerKind.Forward => ActionIndexCache.act_stagger_forward,
                StaggerKind.Left => ActionIndexCache.act_stagger_left,
                StaggerKind.Right => ActionIndexCache.act_stagger_right,
                _ => ActionIndexCache.act_stagger_backward,
            };
            actionIndex = act.Index;
            if (actionIndex < 0) return false; // not in this action set
            return st.Agent.SetActionChannel(0, in act, ignorePriority, (AnimFlags)0uL, 0f, speed);
        }

        public bool TryShieldInHand(TrackedAgent st, out int slot)
        {
            slot = -1;
            var a = st.Agent;
            var i = a.GetOffhandWieldedItemIndex();
            if (i < EquipmentIndex.WeaponItemBeginSlot || i >= EquipmentIndex.NumAllWeaponSlots) return false;
            if (!IsShield(a, (int)i)) return false;
            slot = (int)i;
            return true;
        }

        public bool SlotHoldsShield(TrackedAgent st, int slot) => IsShield(st.Agent, slot);

        public bool HasAnyShield(TrackedAgent st)
        {
            for (int i = (int)EquipmentIndex.WeaponItemBeginSlot; i < (int)EquipmentIndex.NumAllWeaponSlots; i++)
                if (IsShield(st.Agent, i)) return true;
            return false;
        }

        private static bool IsShield(Agent a, int slot)
        {
            if (slot < (int)EquipmentIndex.WeaponItemBeginSlot || slot >= (int)EquipmentIndex.NumAllWeaponSlots) return false;
            var w = a.Equipment[(EquipmentIndex)slot];
            if (w.IsEmpty) return false;
            var usage = w.CurrentUsageItem;
            return usage != null && usage.IsShield;
        }

        public bool DropShield(TrackedAgent st, int slot)
        {
            var m = Mission.Current;
            if (m == null || slot < 0) return false;
            m.AddTickAction(Mission.MissionTickAction.DropItem, st.Agent, slot, 0);
            return true;
        }

        public bool OwnKnockdownBlow(TrackedAgent victim, TrackedAgent? attacker, int damage)
        {
            var v = victim.Agent;
            if (CanForce(victim) != ForceRefusal.None) return false;
            if (damage < 1) damage = 1; // 0 plays no reaction (Agent.HandleBlow)
            var by = attacker != null && !attacker.Removed && attacker.Agent.IsActive() ? attacker.Agent : null;
            // vanilla's drowning blow (Mission.cs 3668-3683), owned by the attacker, KnockDown | KnockBack instead of a plain hit
            var blow = new Blow(by?.Index ?? v.Index);
            blow.DamageType = DamageTypes.Blunt;
            blow.BoneIndex = v.Monster.HeadLookDirectionBoneIndex;
            blow.BaseMagnitude = 10f;
            blow.GlobalPosition = v.Position;
            blow.GlobalPosition.z += v.GetEyeGlobalHeight();
            blow.DamagedPercentage = 1f;
            blow.WeaponRecord.FillAsMeleeBlow(null, null, -1, -1);
            Vec3 dir;
            if (by != null)
            {
                dir = v.Position - by.Position;
                dir.z = 0f;
                if (dir.LengthSquared < 1e-4f) dir = -v.LookDirection;
            }
            else
            {
                dir = -v.LookDirection;
            }
            dir.Normalize();
            blow.SwingDirection = dir;
            blow.Direction = dir;
            blow.InflictedDamage = damage;
            blow.DamageCalculated = true;
            blow.BlowFlag = BlowFlags.KnockDown | BlowFlags.KnockBack;
            sbyte hand = v.Monster.MainHandItemBoneIndex;
            AttackCollisionData cd = AttackCollisionData.GetAttackCollisionDataForDebugPurpose(false, false, false, true, false, false, false, false, false, false, false, false,
                CombatCollisionResult.StrikeAgent, -1, 0, 2, blow.BoneIndex, BoneBodyPartType.Head, hand, Agent.UsageDirection.AttackLeft, -1,
                CombatHitResultFlags.NormalHit, 0.5f, 1f, 0f, 0f, 0f, 0f, 0f, 0f, Vec3.Up, blow.Direction, blow.GlobalPosition, Vec3.Zero, Vec3.Zero, v.Velocity, Vec3.Up);
            ImpactHooks.BeginOwnBlow();
            try
            {
                v.RegisterBlow(blow, in cd);
            }
            finally
            {
                ImpactHooks.EndOwnBlow();
            }
            return true;
        }
    }

    /// <summary>The impact body every new <see cref="AthleticsLogic"/> starts with - the game's; the offline smoke swaps in its stand-in once,
    /// before any logic is built (a native read on its fake agents would crash the process).</summary>
    internal static class ImpactBodyDefault
    {
        internal static IImpactBody Current = new GameImpactBody();
    }
}

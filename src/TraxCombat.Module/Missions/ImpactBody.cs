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

        /// <summary>Step 53, OwnDismountBlow: the same blow of our own on a RIDER, flagged <c>CanDismount</c> (what vanilla's own
        /// <c>CreateMeleeBlow</c> sets when its dismount question says yes) - knocked off his horse; never on a man on foot, a horse, a man on a
        /// siege engine. True = registered.</summary>
        bool OwnDismountBlow(TrackedAgent victim, TrackedAgent? attacker, int damage);

        /// <summary>Step 53: he fights on the player's side (the on-screen lines of your men - <c>Team.IsPlayerAlly</c>).</summary>
        bool OnPlayerSide(TrackedAgent st);

        /// <summary>Step 104a (the J test tool's rear preview) / step 104c (THE LIVE REAR): our own 1-damage blow flagged <c>MakesRear</c>
        /// registered on the RIDER's HORSE - the engine plays the horse's rear and the rider's pose itself (vanilla's braced-pike rear:
        /// Mission.CreateMeleeBlow 5581-5584). NO action of ours on the horse. The blow is OWNED BY THE HORSE ITSELF (vanilla's drowning blow -
        /// Mission.cs 3668: nobody's attack, no kill credit, no hit reward - a horse has no Character); refused on a horse below
        /// <see cref="TimedRiderFallMath.MinHorseHealthForRear"/> health (the blow can never kill it) and its 1 health WRITTEN BACK right after
        /// (<paramref name="horseDamage"/> = what it lost after that - 0). Refused (false, <paramref name="why"/>) without a horse, a horse that
        /// cannot rear (<c>AgentFlag.CanRear</c>), one already rearing. True = registered.</summary>
        bool OwnRearBlow(TrackedAgent rider, out RiderFallRefusal why, out double horseDamage);

        /// <summary>Step 104a: his horse plays the rear now (its channel 0's action type - a read only).</summary>
        bool MountRearing(TrackedAgent rider);
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
            if (CanForce(victim) != ForceRefusal.None) return false;
            return RegisterOwnBlow(victim, attacker, damage, BlowFlags.KnockDown | BlowFlags.KnockBack);
        }

        public bool OwnDismountBlow(TrackedAgent victim, TrackedAgent? attacker, int damage)
        {
            var v = victim.Agent;
            if (victim.Removed || !v.IsActive() || !v.IsHuman || v.MountAgent == null || v.IsUsingGameObject) return false;
            return RegisterOwnBlow(victim, attacker, damage, BlowFlags.CanDismount); // vanilla's flag for a rider (Mission.CreateMeleeBlow 5566)
        }

        public bool OwnRearBlow(TrackedAgent rider, out RiderFallRefusal why, out double horseDamage)
        {
            why = RiderFallRefusal.None;
            horseDamage = double.NaN;
            var a = rider.Agent;
            if (rider.Removed || !a.IsActive() || !a.IsHuman)
            {
                why = RiderFallRefusal.Gone;
                return false;
            }
            var horse = a.MountAgent;
            if (horse == null || !horse.IsActive() || !horse.IsMount)
            {
                why = RiderFallRefusal.NotRiding;
                return false;
            }
            if (!horse.GetAgentFlags().HasAnyFlag(AgentFlag.CanRear))
            {
                why = RiderFallRefusal.CannotRear;
                return false;
            }
            if (horse.GetCurrentActionType(0) == Agent.ActionCodeType.Rear)
            {
                why = RiderFallRefusal.AlreadyRearing;
                return false;
            }
            if (horse.Monster.HeadLookDirectionBoneIndex < 0)
            {
                why = RiderFallRefusal.NoHeadBone;
                return false;
            }
            double before = horse.Health;
            if (!TimedRiderFallMath.RearBlowSafe(before))
            {
                why = RiderFallRefusal.HorseTooWeak; // the 1-damage blow could kill it (Agent.HandleBlow: Health < 1 -> Die)
                return false;
            }
            // the engine decides and plays the rear - never SetActionChannel on a horse; owned by the horse itself (vanilla's drowning blow):
            // nobody's attack - no kill credit, no hit reward (BattleAgentLogic / TournamentFightMissionController need a Character - a horse has
            // none), and its RiderAgent is set, so CommonAIComponent.OnHit never panics it (that needs a riderless horse)
            RegisterOwnBlowOn(horse, null, 1, BlowFlags.MakesRear);
            if (horse.IsActive())
            {
                double after = horse.Health;
                double back = TimedRiderFallMath.Restored(before, after, horse.HealthLimit);
                if (back > after) horse.Health = (float)back; // its 1 health given back at once
                horseDamage = TimedRiderFallMath.NetDamage(before, horse.Health);
            }
            return true;
        }

        public bool MountRearing(TrackedAgent rider)
        {
            var h = rider.Agent.MountAgent;
            return h != null && h.IsActive() && h.GetCurrentActionType(0) == Agent.ActionCodeType.Rear;
        }

        public bool OnPlayerSide(TrackedAgent st)
        {
            var team = st.Agent.Team;
            return team != null && team.IsPlayerAlly;
        }

        private static bool RegisterOwnBlow(TrackedAgent victim, TrackedAgent? attacker, int damage, BlowFlags flags)
            => RegisterOwnBlowOn(victim.Agent, attacker != null && !attacker.Removed && attacker.Agent.IsActive() ? attacker.Agent : null, damage, flags);

        /// <summary>The blow itself on any agent (step 104a: a rider's horse for the rear).</summary>
        private static bool RegisterOwnBlowOn(Agent v, Agent? by, int damage, BlowFlags flags)
        {
            if (damage < 1) damage = 1; // 0 plays no reaction (Agent.HandleBlow)
            // vanilla's drowning blow (Mission.cs 3668-3683), owned by the attacker, with our flags (KnockDown | KnockBack; step 53: CanDismount) instead of a plain hit
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
            blow.BlowFlag = flags;
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

using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 23: one AI fighter's brace (AI_NOTES "Step 23") - his roll for the margin (once per battle), whether he braces now,
    /// under which order it began, and the shield's progress. Allocated once per AI man at his first look, reused - no per-tick
    /// allocation. Main thread (the logic's tick; hit callbacks read it).
    /// </summary>
    internal sealed class BraceState
    {
        /// <summary>His roll for the margin spread, uniform in [−1, 1) - rolled at his first look, never again this battle.</summary>
        public double Unit;

        /// <summary>His last look (mission time) - the AI fighter-time the summary compares the braces with.</summary>
        public double LastPoll = double.NaN;

        // ---- the brace running now
        public bool Active;
        public double Since;
        public BraceOrderKind KindAtStart;
        public BraceOrder GroupAtStart;
        public BraceOrderKind KindNow;
        public double FractionAtStart;
        public BraceBand BandAtStart;
        public bool EverBraced;

        /// <summary>The input hook's call count at the brace's start - none by its end = the engine never called us.</summary>
        public long CallsAtStart;

        // ---- the shield
        public ShieldStep StepAtStart;
        public bool ShieldAsked;
        public bool ShieldSeen;
        public bool SwitchedBackCounted;
        public int WieldCalls;
        public double NextShieldCheck;

        /// <summary>Melee hits he took during this brace and how many he blocked (the first brace's end line).</summary>
        public int HitsTaken;
        public int HitsBlocked;

        /// <summary>The mission's first brace - its start and end are logged in full.</summary>
        public bool First;
    }

    /// <summary>
    /// Every GAME call of the brace behind one seam (the step-5d pattern): the logic keeps the bookkeeping (the band, the order
    /// floors, every end path, the stats), this touches the engine. The real one is <see cref="GameBraceBody"/>; the offline
    /// smoke plays a stand-in (its agents have no native side).
    /// </summary>
    internal interface IBraceBody
    {
        /// <summary>He is on the field (the engine's state).</summary>
        bool IsActive(TrackedAgent st);

        /// <summary>His own AI drives him (not a soldier the player took over - RTS Camera; the player's agent is the logic's
        /// check).</summary>
        bool IsAiControlled(TrackedAgent st);

        /// <summary>His formation's movement order now (managed reads).</summary>
        BraceOrderKind ReadOrder(TrackedAgent st);

        /// <summary>His hands and weapon slots now.</summary>
        HandFacts ReadHands(TrackedAgent st);

        /// <summary>Asks the engine to wield the weapon in <paramref name="slot"/> (with its animation).</summary>
        void Wield(TrackedAgent st, int slot);

        /// <summary>Our input component on him and the engine's callback on (step 16's <see cref="AiInputHook.Hook"/>).</summary>
        AiInputHook.HookResult Hook(TrackedAgent st);

        /// <summary>The brace's wish is off: the callback flag goes off if he is idle and it was ours.</summary>
        void Release(TrackedAgent st);
    }

    /// <summary>
    /// The engine side (AI_NOTES "Step 23"): the order from <c>Formation.GetReadonlyMovementOrderReference().OrderEnum</c>, an
    /// AI-commanded formation's Move read through its active behaviour (an advance / a tactical charge); the hands from the
    /// wielded slot indices (the agent's own memory) and the managed equipment; the wield through
    /// <c>Agent.TryToWieldWeaponInSlot(slot, WithAnimation, false)</c> - the call vanilla's own victory cheer and scene
    /// animations use. Main thread (the logic's tick).
    /// </summary>
    internal sealed class GameBraceBody : IBraceBody
    {
        public bool IsActive(TrackedAgent st) => st.Agent.IsActive();

        public bool IsAiControlled(TrackedAgent st) => st.Agent.IsAIControlled;

        public BraceOrderKind ReadOrder(TrackedAgent st)
        {
            var f = st.Agent.Formation;
            if (f == null) return BraceOrderKind.NoFormation;
            switch (f.GetReadonlyMovementOrderReference().OrderEnum)
            {
                case MovementOrder.MovementOrderEnum.Charge: return BraceOrderKind.Charge;
                case MovementOrder.MovementOrderEnum.ChargeToTarget: return BraceOrderKind.ChargeToTarget;
                case MovementOrder.MovementOrderEnum.AttackEntity: return BraceOrderKind.AttackEntity;
                case MovementOrder.MovementOrderEnum.Advance: return BraceOrderKind.Advance;
                case MovementOrder.MovementOrderEnum.Stop: return BraceOrderKind.Stop;
                case MovementOrder.MovementOrderEnum.Retreat: return BraceOrderKind.Retreat;
                case MovementOrder.MovementOrderEnum.FallBack: return BraceOrderKind.FallBack;
                case MovementOrder.MovementOrderEnum.Follow: return BraceOrderKind.Follow;
                case MovementOrder.MovementOrderEnum.FollowEntity: return BraceOrderKind.FollowEntity;
                case MovementOrder.MovementOrderEnum.Move:
                    if (f.IsAIControlled)
                    {
                        // the team AI walks a formation into the enemy on a plain Move while its behaviour advances
                        var b = f.AI?.ActiveBehavior;
                        if (b is BehaviorAdvance || b is BehaviorCautiousAdvance || b is BehaviorVanguard) return BraceOrderKind.AiAdvance;
                        if (b is BehaviorTacticalCharge) return BraceOrderKind.AiCharge;
                    }
                    return BraceOrderKind.Move;
                default:
                    return BraceOrderKind.NoOrder;
            }
        }

        public HandFacts ReadHands(TrackedAgent st)
        {
            var a = st.Agent;
            var h = HandFacts.None;
            var eq = a.Equipment;
            if (eq == null) return h;
            var off = a.GetOffhandWieldedItemIndex();
            var main = a.GetPrimaryWieldedItemIndex();
            if (off >= EquipmentIndex.WeaponItemBeginSlot && off < EquipmentIndex.NumAllWeaponSlots)
            {
                var w = eq[off];
                h.ShieldInOffHand = !w.IsEmpty && w.IsShield();
            }
            if (main >= EquipmentIndex.WeaponItemBeginSlot && main < EquipmentIndex.NumAllWeaponSlots)
            {
                var item = eq[main].CurrentUsageItem;
                if (item != null)
                {
                    h.MainEmpty = false;
                    h.MainRanged = item.IsRangedWeapon;
                    h.MainNeedsBothHands = (item.WeaponFlags & WeaponFlags.NotUsableWithOneHand) != 0;
                }
            }
            for (var i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.ExtraWeaponSlot; i++)
            {
                var w = eq[i];
                if (w.IsEmpty) continue;
                if (w.IsShield())
                {
                    if (h.ShieldSlot < 0) h.ShieldSlot = (int)i;
                    continue;
                }
                if (h.OneHandedSlot < 0 && OneHandedMelee(w)) h.OneHandedSlot = (int)i;
            }
            return h;
        }

        /// <summary>A melee weapon one of whose usages goes in one hand (a sword, an axe, a one-handed spear).</summary>
        private static bool OneHandedMelee(MissionWeapon w)
        {
            var list = w.Item?.Weapons;
            if (list == null) return false;
            for (int k = 0; k < list.Count; k++)
            {
                var u = list[k];
                if (u != null && u.IsMeleeWeapon && !u.IsShield && (u.WeaponFlags & WeaponFlags.NotUsableWithOneHand) == 0) return true;
            }
            return false;
        }

        public void Wield(TrackedAgent st, int slot) =>
            st.Agent.TryToWieldWeaponInSlot((EquipmentIndex)slot, Agent.WeaponWieldActionType.WithAnimation, false);

        public AiInputHook.HookResult Hook(TrackedAgent st) => AiInputHook.Hook(st);

        public void Release(TrackedAgent st)
        {
            if (st.Removed || !st.Agent.IsActive()) return;
            AiInputHook.UnhookIfIdle(st);
        }
    }

    /// <summary>The brace body every new <see cref="AthleticsLogic"/> starts with - the game's; the offline smoke swaps in its
    /// stand-in once, before any logic is built (a native read on its fake agents would crash the process).</summary>
    internal static class BraceBodyDefault
    {
        internal static IBraceBody Current = new GameBraceBody();
    }
}

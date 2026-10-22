using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 76 (the AI prefers the spear - AI_NOTES "Step 76"): the engine side of a spear look - who is looked at, his four slots, his
    /// formation's arrangement / order / his rank, the enemies within his close radius, what he holds. Main thread (the logic's tick), never
    /// inside the stat model (RBM's Agent.Build deadlock). The offline smoke plays it.
    /// </summary>
    internal interface ISpearBody
    {
        /// <summary>The mission has finished deploying (RBM looks only after deployment).</summary>
        bool Ready();

        /// <summary>An active AI-controlled human with a team - never the player's agent (or a man the player took over).</summary>
        bool Gate(TrackedAgent st);

        /// <summary>His four weapon slots into <paramref name="slots"/> - false (nothing written) when the four items are the ones read last time
        /// (RBM's loadout cache: the slots only change on a pickup or a drop).</summary>
        bool ReadLoadout(TrackedAgent st, SpearSlot[] slots);

        /// <summary>His horse and its speed, his formation's arrangement and order, his rank and whether a man of his stands in front of him.</summary>
        void Context(TrackedAgent st, out SpearContext c);

        /// <summary>The enemies within <paramref name="radius"/> m (not running away): <paramref name="other"/> not wielding a melee polearm,
        /// <paramref name="polearm"/> wielding one.</summary>
        void CloseEnemies(TrackedAgent st, double radius, out int other, out int polearm);

        /// <summary>His wielded main-hand slot (0-3; -1 = none).</summary>
        int WieldedSlot(TrackedAgent st);

        /// <summary>Step 16's hook on him (the wield fallback) - from the tick.</summary>
        AiInputHook.HookResult Hook(TrackedAgent st);

        /// <summary>His callback released if no wish is left (never for a man gone).</summary>
        void Release(TrackedAgent st);
    }

    /// <summary>The game's side: managed equipment / formation reads, the game's proximity search into a reused list, a few native reads.</summary>
    internal sealed class GameSpearBody : ISpearBody
    {
        private readonly MBList<Agent> _near = new MBList<Agent>(); // reused - no allocation per look

        public bool Ready()
        {
            var m = Mission.Current;
            return m != null && m.IsDeploymentFinished;
        }

        public bool Gate(TrackedAgent st)
        {
            var a = st.Agent;
            return a.IsActive() && a.IsHuman && a.IsAIControlled && !a.IsMainAgent && a.Team != null;
        }

        public bool ReadLoadout(TrackedAgent st, SpearSlot[] slots)
        {
            var eq = st.Agent.Equipment;
            object? i0 = null, i1 = null, i2 = null, i3 = null;
            if (eq != null)
            {
                i0 = ItemOf(eq[EquipmentIndex.Weapon0]);
                i1 = ItemOf(eq[EquipmentIndex.Weapon1]);
                i2 = ItemOf(eq[EquipmentIndex.Weapon2]);
                i3 = ItemOf(eq[EquipmentIndex.Weapon3]);
            }
            if (st.Spear.Read && ReferenceEquals(i0, st.SpearItem0) && ReferenceEquals(i1, st.SpearItem1) && ReferenceEquals(i2, st.SpearItem2)
                && ReferenceEquals(i3, st.SpearItem3)) return false;
            st.SpearItem0 = i0;
            st.SpearItem1 = i1;
            st.SpearItem2 = i2;
            st.SpearItem3 = i3;
            slots[0] = SlotOf(i0 as ItemObject);
            slots[1] = SlotOf(i1 as ItemObject);
            slots[2] = SlotOf(i2 as ItemObject);
            slots[3] = SlotOf(i3 as ItemObject);
            return true;
        }

        private static object? ItemOf(MissionWeapon w) => w.IsEmpty ? null : w.Item;

        /// <summary>RBM's rule per item: thrown / consumable / ranged = neither; melee with a polearm usage = a polearm (its longest melee
        /// polearm usage's length); other melee = a sidearm.</summary>
        internal static SpearSlot SlotOf(ItemObject? item)
        {
            var s = new SpearSlot();
            if (item == null)
            {
                s.Empty = true;
                return s;
            }
            if (item.ItemType == ItemObject.ItemTypeEnum.Thrown) s.Throwable = true;
            var usages = item.Weapons;
            if (usages == null) return s;
            for (int i = 0; i < usages.Count; i++)
            {
                var u = usages[i];
                if (u == null) continue;
                if (u.IsConsumable || u.IsRangedWeapon) s.Throwable = true;
                if (!u.IsMeleeWeapon) continue;
                s.Melee = true;
                if (IsPolearmClass(u.WeaponClass))
                {
                    s.Polearm = true;
                    if (u.WeaponLength > s.LengthCm) s.LengthCm = u.WeaponLength;
                }
            }
            return s;
        }

        /// <summary>The weapon classes whose relevant skill is Polearm (<c>WeaponComponentData.GetRelevantSkillFromWeaponClass</c>).</summary>
        internal static bool IsPolearmClass(WeaponClass c) => c == WeaponClass.OneHandedPolearm || c == WeaponClass.TwoHandedPolearm || c == WeaponClass.LowGripPolearm;

        public void Context(TrackedAgent st, out SpearContext c)
        {
            c = new SpearContext { Rank = -1, Arrangement = SpearArrangement.Other, Order = BraceOrderKind.NoFormation };
            var a = st.Agent;
            var mount = a.MountAgent;
            if (mount != null)
            {
                c.Mounted = true;
                c.MountSpeed = mount.GetCurrentVelocity().Length; // native - riders only
            }
            c.StandsFirm = st.Wall != null && st.Wall.Firm;
            var f = a.Formation;
            if (f == null) return;
            c.Order = GameBraceBody.KindOf(f);
            c.Arrangement = f.ArrangementOrder.OrderEnum switch
            {
                ArrangementOrder.ArrangementOrderEnum.ShieldWall => SpearArrangement.Wall,
                ArrangementOrder.ArrangementOrderEnum.Square => SpearArrangement.Wall,
                ArrangementOrder.ArrangementOrderEnum.Circle => SpearArrangement.Wall,
                ArrangementOrder.ArrangementOrderEnum.Line => SpearArrangement.Line,
                _ => SpearArrangement.Other,
            };
            if (a.IsDetachedFromFormation) return;
            a.GetFormationFileAndRankInfo(out int file, out int rank);
            c.Rank = rank;
            if (rank < 1 || file < 0 || !(f.Arrangement is LineFormation lf)) return;
            lf.GetFormationInfo(out int files, out int ranks);
            if (rank - 1 >= ranks) return;
            for (int df = -1; df <= 1; df++)
            {
                int ff = file + df;
                if (ff < 0 || ff >= files) continue;
                if (lf.GetUnit(ff, rank - 1) != null)
                {
                    c.ManInFront = true;
                    return;
                }
            }
        }

        public void CloseEnemies(TrackedAgent st, double radius, out int other, out int polearm)
        {
            other = polearm = 0;
            var a = st.Agent;
            var m = Mission.Current;
            if (m == null || a.Team == null) return;
            m.GetNearbyEnemyAgents(a.Position.AsVec2, (float)radius, a.Team, _near);
            for (int i = 0; i < _near.Count; i++)
            {
                var e = _near[i];
                if (e == null || !e.IsHuman || !e.IsActive() || e.IsRunningAway) continue;
                var u = e.WieldedWeapon.CurrentUsageItem;
                if (u != null && u.IsMeleeWeapon && IsPolearmClass(u.WeaponClass)) polearm++;
                else other++;
            }
            _near.Clear();
        }

        public int WieldedSlot(TrackedAgent st)
        {
            var i = st.Agent.GetPrimaryWieldedItemIndex();
            return i >= EquipmentIndex.Weapon0 && i <= EquipmentIndex.Weapon3 ? (int)i : -1;
        }

        public AiInputHook.HookResult Hook(TrackedAgent st) => AiInputHook.Hook(st);

        public void Release(TrackedAgent st)
        {
            if (!st.Removed) AiInputHook.UnhookIfIdle(st);
        }
    }

    /// <summary>The body every new <see cref="AthleticsLogic"/> starts with - the game's; the offline smoke sets its stand-in on its own logic.</summary>
    internal static class SpearBodyDefault
    {
        internal static ISpearBody Current = new GameSpearBody();
    }
}

using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 84: the ENGINE side of the tier-1 shields (the smoke plays it): the mission's kind, the spawning man read into Core's
    /// <see cref="KitAgent"/> / <see cref="KitSlot"/>, the gear given, and the game's item table (<see cref="IKitItems"/>).
    /// </summary>
    internal interface IUnitKitBody
    {
        /// <summary>A mission whose gear is fixed or not a fight's (tournaments, arenas, civilian scenes) - null = gear may be given, else
        /// why not. Decided once a mission.</summary>
        string? FixedEquipment(Mission? mission);

        /// <summary>The man as Core reads him; false = not a man we look at (not human, no character).</summary>
        bool Read(Agent agent, ref KitAgent man, KitSlot[] slots);

        /// <summary>Plays the order: the shield into its free slot, the spear into the two-hander's slot (when there is one), both in his
        /// spawn equipment too (the game's own WieldInitialWeapons picks from it), re-wielded when he already holds something.</summary>
        bool Give(Agent agent, in KitOrder order, Banner? banner);

        IKitItems Items { get; }
    }

    internal sealed class GameUnitKitBody : IUnitKitBody
    {
        private Mission? _kindOf;
        private string? _kind;

        public IKitItems Items { get; } = new GameKitItems();

        public string? FixedEquipment(Mission? mission)
        {
            if (mission == null || ReferenceEquals(_kindOf, mission)) return mission == null ? null : _kind;
            _kindOf = mission;
            _kind = null;
            try
            {
                if (mission.DoesMissionRequireCivilianEquipment) _kind = "a civilian scene";
                else if (mission.CombatType != Mission.MissionCombatType.Combat) _kind = "combat type " + mission.CombatType;
                else
                {
                    foreach (var b in mission.MissionBehaviors)
                    {
                        string name = b?.GetType().Name ?? string.Empty;
                        if (name.IndexOf("Tournament", StringComparison.Ordinal) >= 0 || name.IndexOf("Arena", StringComparison.Ordinal) >= 0)
                        {
                            _kind = "a tournament or arena fight (" + name + ")";
                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _kind = "unknown (" + e.GetType().Name + ")";
            }
            return _kind;
        }

        public bool Read(Agent agent, ref KitAgent man, KitSlot[] slots)
        {
            if (agent == null || !agent.IsHuman) return false;
            var ch = agent.Character;
            if (ch == null) return false;
            man.Player = agent.IsMainAgent || agent.Controller == AgentControllerType.Player;
            man.Hero = agent.IsHero || ch.IsHero;
            man.Mounted = agent.MountAgent != null || (agent.SpawnEquipment != null && !agent.SpawnEquipment[EquipmentIndex.Horse].IsEmpty);
            man.Tier = ch.GetBattleTier();
            man.Culture = ch.Culture?.StringId;
            for (int i = 0; i < UnitKitMath.Slots && i < slots.Length; i++)
            {
                var w = agent.Equipment[(EquipmentIndex)i];
                ReadSlot(w.IsEmpty ? null : w.Item, ref slots[i]);
            }
            return true;
        }

        internal static void ReadSlot(ItemObject? item, ref KitSlot slot)
        {
            slot = default;
            if (item == null)
            {
                slot.Empty = true;
                return;
            }
            slot.Id = item.StringId;
            if (item.ItemType == ItemObject.ItemTypeEnum.Shield)
            {
                slot.Shield = true;
                return;
            }
            var weapons = item.Weapons;
            if (weapons == null) return;
            for (int i = 0; i < weapons.Count; i++)
            {
                var u = weapons[i];
                if (u == null || u.IsShield || !u.IsMeleeWeapon || u.IsConsumable) continue;
                slot.Melee = true;
                if ((u.WeaponFlags & WeaponFlags.NotUsableWithOneHand) == 0) slot.OneHand = true;
            }
        }

        public bool Give(Agent agent, in KitOrder order, Banner? banner)
        {
            var shield = order.Shield as ItemObject;
            if (shield == null) return false;
            bool wielding = agent.GetPrimaryWieldedItemIndex() != EquipmentIndex.None || agent.GetOffhandWieldedItemIndex() != EquipmentIndex.None;
            var sw = new MissionWeapon(shield, null, banner);
            agent.EquipWeaponWithNewEntity((EquipmentIndex)order.ShieldSlot, ref sw);
            var spawn = agent.SpawnEquipment;
            if (spawn != null) spawn[(EquipmentIndex)order.ShieldSlot] = new EquipmentElement(shield);
            if (order.SwapSlot >= 0 && order.Spear is ItemObject spear)
            {
                var pw = new MissionWeapon(spear, null, banner);
                agent.EquipWeaponWithNewEntity((EquipmentIndex)order.SwapSlot, ref pw);
                if (spawn != null) spawn[(EquipmentIndex)order.SwapSlot] = new EquipmentElement(spear);
            }
            // He held something already (a mission that wields at spawn): pick again from the spawn equipment - the spear and the shield.
            // Otherwise the game's own WieldInitialWeapons (or his AI) picks from the same spawn equipment later.
            if (wielding) agent.WieldInitialWeapons(Agent.WeaponWieldActionType.InstantAfterPickUp);
            return true;
        }
    }

    /// <summary>Step 84: the game's item table - MBObjectManager's ItemObjects; the cheapest per culture computed once and kept.</summary>
    internal sealed class GameKitItems : IKitItems
    {
        private readonly Dictionary<string, ItemObject?> _cheapestShield = new Dictionary<string, ItemObject?>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ItemObject?> _cheapestSpear = new Dictionary<string, ItemObject?>(StringComparer.OrdinalIgnoreCase);

        public bool TryFind(string id, out object item, out KitItemFacts facts)
        {
            item = null!;
            facts = default;
            if (string.IsNullOrEmpty(id)) return false;
            var found = MBObjectManager.Instance?.GetObject<ItemObject>(id);
            if (found == null) return false;
            item = found;
            facts = FactsOf(found);
            return true;
        }

        public bool TryCheapest(string culture, bool shield, out object item, out KitItemFacts facts)
        {
            item = null!;
            facts = default;
            var cache = shield ? _cheapestShield : _cheapestSpear;
            if (!cache.TryGetValue(culture, out var best))
            {
                best = null;
                var all = MBObjectManager.Instance?.GetObjectTypeList<ItemObject>();
                if (all != null)
                {
                    for (int i = 0; i < all.Count; i++)
                    {
                        var it = all[i];
                        if (it == null || it.NotMerchandise || it.Culture == null) continue;
                        if (!string.Equals(it.Culture.StringId, culture, StringComparison.OrdinalIgnoreCase)) continue;
                        var f = FactsOf(it);
                        bool fits = shield ? f.Shield : (f.Polearm && f.OneHandMelee && !f.Shield);
                        if (!fits) continue;
                        if (best == null || it.Value < best.Value) best = it;
                    }
                }
                cache[culture] = best;
            }
            if (best == null) return false;
            item = best;
            facts = FactsOf(best);
            return true;
        }

        internal static KitItemFacts FactsOf(ItemObject it)
        {
            var f = new KitItemFacts
            {
                Id = it.StringId,
                Name = it.Name?.ToString() ?? it.StringId,
                Culture = it.Culture?.StringId,
                Merchandise = !it.NotMerchandise,
                Value = it.Value,
                Weight = it.Weight,
            };
            if (it.ItemType == ItemObject.ItemTypeEnum.Shield)
            {
                f.Shield = true;
                f.HitPoints = it.PrimaryWeapon?.MaxDataValue ?? 0;
                return f;
            }
            var weapons = it.Weapons;
            if (weapons == null) return f;
            bool thrown = false;
            for (int i = 0; i < weapons.Count; i++)
            {
                var u = weapons[i];
                if (u == null) continue;
                if (u.IsConsumable || u.IsRangedWeapon) thrown = true;
                if (u.IsShield || !u.IsMeleeWeapon) continue;
                if ((u.WeaponFlags & WeaponFlags.NotUsableWithOneHand) == 0) f.OneHandMelee = true;
                if (it.ItemType == ItemObject.ItemTypeEnum.Polearm) f.Polearm = true;
            }
            if (thrown) f.Polearm = false; // a throwing spear is never the fallback's spear
            return f;
        }
    }

    internal static class UnitKitBodyDefault
    {
        internal static IUnitKitBody Current = new GameUnitKitBody();
    }
}

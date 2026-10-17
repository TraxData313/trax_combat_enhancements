using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ComponentInterfaces;

namespace TraxCombat.Models
{
    /// <summary>
    /// Step 50 (IMPACT I3, §3.9 / §8 answer 3): a DECORATOR over whatever <see cref="ItemPickupModel"/> was registered before us (never a
    /// subclass of DefaultItemPickupModel - CLAUDE.md). Every member forwards to <c>BaseModel</c>; <see cref="IsItemAvailableForAgent"/>
    /// also refuses a SHIELD to an AI man whose shield a live kick / bash knocked from his hand less than <c>ShieldPickupLockSeconds</c> ago
    /// (0 = never - vanilla, the default: Anton's answer 3). Only the AI asks this model (HumanAIComponent.ItemPickupTick / SelectPickableItem,
    /// main thread); the player's use key does not - the lock never touches you. Any error in our part = the base model's answer.
    /// </summary>
    public sealed class TraxItemPickupModel : ItemPickupModel
    {
        internal string BaseModelName => BaseModel?.GetType().FullName ?? "(none)";

        public override float GetItemScoreForAgent(SpawnedItemEntity item, Agent agent) => BaseModel.GetItemScoreForAgent(item, agent);

        public override bool IsAgentEquipmentSuitableForPickUpAvailability(Agent agent) => BaseModel.IsAgentEquipmentSuitableForPickUpAvailability(agent);

        public override bool IsItemAvailableForAgent(SpawnedItemEntity item, Agent agent, EquipmentIndex slotToPickUp)
        {
            bool game = BaseModel.IsItemAvailableForAgent(item, agent, slotToPickUp);
            if (!game) return false;
            try
            {
                var usage = item?.WeaponCopy.CurrentUsageItem;
                if (usage == null || !usage.IsShield) return true;
                return !ImpactHooks.ShieldPickupLocked(agent);
            }
            catch (Exception e)
            {
                TraxCombat.Missions.AthleticsLogic.Failed("impact.pickup-model", e);
                return game;
            }
        }
    }
}

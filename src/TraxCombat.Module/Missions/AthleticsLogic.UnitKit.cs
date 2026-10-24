using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 84 - TIER-1 MEN GET A CHEAP CULTURE SHIELD (Toni 2026-10-24; AI_NOTES "Step 84", DESIGN decision 80): the attach point of Core
    /// <see cref="UnitKit"/> - built at the mission's start (each culture's items resolved and said once), asked in OnAgentBuild (after the
    /// game's Agent.Build and its EquipItemsFromSpawnEquipment, before anything of ours tracks him - never inside Agent.Build or the stat
    /// model: RBM's Agent.Build deadlock), the gear played through <see cref="IUnitKitBody"/>; nothing in the save. The [summary] line.
    /// </summary>
    public sealed partial class AthleticsLogic
    {
        private UnitKit? _unitKit;
        private readonly KitSlot[] _kitSlots = new KitSlot[UnitKitMath.Slots];

        internal IUnitKitBody UnitKitBody { get; set; } = UnitKitBodyDefault.Current;

        /// <summary>This mission's unit kit (the offline smoke reads it), null before the start.</summary>
        internal UnitKit? UnitKit => _unitKit;

        internal void StartUnitKit()
        {
            try
            {
                _unitKit = new UnitKit(UnitKitBody.Items, line => TraxLog.Info("units", line));
                var s = TraxSettings.Shared;
                string? kind = UnitKitBody.FixedEquipment(Mission);
                TraxLog.Info("units", "tier-1 shields (step 84): " + (s.GiveTier1Shields ? "on" : "OFF (GiveTier1Shields)")
                    + (kind != null ? " - not in this mission: " + kind : " - every tier-1 soldier on foot without a shield gets his culture's, a two-hander becomes its spear"));
                _unitKit.Start(s);
            }
            catch (Exception e)
            {
                _unitKit = null;
                Failed("units.start", e);
                TraxLog.Info("units", "could not start the tier-1 shields - the game's own gear this battle (see the [error] above)");
            }
        }

        /// <summary>From OnAgentBuild: one spawning man, decided and given. Allocation-free on every path but a culture's first man.</summary>
        internal void GiveUnitKit(Agent agent, Banner? banner)
        {
            var kit = _unitKit;
            if (kit == null || agent == null) return; // OnAgentBuild asked IsHuman (a native read)
            var s = TraxSettings.Shared;
            // the cheap gates first: no engine read at all while the switch or the master switch is off
            if (!s.ModEnabled || !s.GiveTier1Shields)
            {
                var none = default(KitAgent);
                var skipOrder = default(KitOrder);
                kit.Decide(s, false, in none, _kitSlots, ref skipOrder);
                return;
            }
            var body = UnitKitBody;
            var man = default(KitAgent);
            if (!body.Read(agent, ref man, _kitSlots)) return;
            var order = default(KitOrder);
            bool fixedGear = body.FixedEquipment(Mission) != null;
            if (kit.Decide(s, fixedGear, in man, _kitSlots, ref order) != KitSkip.Given) return;
            bool ok = false;
            try
            {
                ok = body.Give(agent, in order, banner);
            }
            catch (Exception e)
            {
                Failed("units.give", e);
            }
            kit.Done(in order, ok);
        }

        private void WriteUnitKitSummary()
        {
            if (_unitKit == null)
            {
                TraxLog.Info("summary", "unit changes (step 84): not started this mission (see the [error] at its start)");
                return;
            }
            TraxLog.Info("summary", _unitKit.SummaryLine());
        }
    }
}

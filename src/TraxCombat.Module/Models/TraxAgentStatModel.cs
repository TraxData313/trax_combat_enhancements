using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TraxCombat.Missions;

namespace TraxCombat.Models
{
    /// <summary>
    /// The attack-speed hook (feature 2, step 5) - a DECORATOR over whatever
    /// <see cref="AgentStatCalculateModel"/> was registered before us (Sandbox's, CustomBattle's,
    /// War Sails' naval one). Never a Default*/Sandbox* subclass (CLAUDE.md).
    ///
    /// EVERY abstract AND virtual member forwards to BaseModel - a virtual left un-overridden
    /// would run the abstract class's default body instead of the sandbox logic
    /// (GetEffectiveMaxHealth, GetEffectiveSkill, …; RESEARCH §C). The one change (step 5):
    /// <see cref="UpdateAgentStats"/> scales the attack-speed properties by the agent's endurance
    /// multiplier (<see cref="SpeedPenalty"/>).
    ///
    /// THE TOURNAMENT FIX (RESEARCH §C): TournamentBehavior raises the AI level each round with
    /// <c>MissionGameModels.Current.AgentStatCalculateModel.SetAILevelMultiplier(x)</c> - a
    /// NON-virtual method writing a PRIVATE field of the TOP model (us), while the base models
    /// read their OWN copy of that field. Any decorator therefore swallows tournament AI
    /// scaling (War Sails' already does in vanilla). Before every call that reaches the base
    /// models' AI setup, <see cref="SyncAiLevel"/> reads our field and, when it changed, pushes
    /// it into every other stat model registered before us.
    /// </summary>
    public sealed class TraxAgentStatModel : AgentStatCalculateModel
    {
        private static readonly Func<AgentStatCalculateModel, float>? ReadAiLevel = BuildAiLevelReader();

        private readonly List<AgentStatCalculateModel> _chain;
        private float _pushedAiLevel = 1f;

        /// <param name="earlierModels">Every AgentStatCalculateModel registered before us
        /// (the decorator chain below us) - they all get the tournament multiplier.</param>
        public TraxAgentStatModel(IEnumerable<AgentStatCalculateModel> earlierModels)
        {
            _chain = earlierModels.Where(m => m != null && !ReferenceEquals(m, this)).ToList();
        }

        /// <summary>The model we wrap, for the load log.</summary>
        internal string BaseModelName => BaseModel?.GetType().FullName ?? "(none)";

        /// <summary>False when the private field could not be read (a game update renamed it)
        /// - logged once at load; tournaments then behave as vanilla-with-a-decorator.</summary>
        internal static bool AiLevelFixAvailable => ReadAiLevel != null;

        // ------------------------------------------------------------------ the tournament fix

        private void SyncAiLevel()
        {
            if (ReadAiLevel == null) return;
            try
            {
                float level = ReadAiLevel(this);
                if (Math.Abs(level - _pushedAiLevel) < 1e-6f) return;
                foreach (var model in _chain)
                    model.SetAILevelMultiplier(level);
                TraxLog.Info("speed", "tournament AI level multiplier " + _pushedAiLevel.ToString("0.##") + " → "
                    + level.ToString("0.##") + " passed on to " + _chain.Count + " base stat model(s)");
                _pushedAiLevel = level;
            }
            catch (Exception e)
            {
                TraxLog.Error("speed.ai-level-sync", e);
            }
        }

        /// <summary>A compiled read of AgentStatCalculateModel._AILevelMultiplier (private) -
        /// no boxing, no reflection per call. Null if the field is gone.</summary>
        private static Func<AgentStatCalculateModel, float>? BuildAiLevelReader()
        {
            try
            {
                var field = typeof(AgentStatCalculateModel).GetField("_AILevelMultiplier", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null || field.FieldType != typeof(float)) return null;
                var method = new DynamicMethod("Trax_ReadAILevelMultiplier", typeof(float),
                    new[] { typeof(AgentStatCalculateModel) }, typeof(AgentStatCalculateModel), true);
                var il = method.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, field);
                il.Emit(OpCodes.Ret);
                return (Func<AgentStatCalculateModel, float>)method.CreateDelegate(typeof(Func<AgentStatCalculateModel, float>));
            }
            catch
            {
                return null;
            }
        }

        // ------------------------------------------------------------------ abstract members

        public override void InitializeAgentStats(Agent agent, Equipment spawnEquipment, AgentDrivenProperties agentDrivenProperties, AgentBuildData agentBuildData)
        {
            SyncAiLevel();
            BaseModel.InitializeAgentStats(agent, spawnEquipment, agentDrivenProperties, agentBuildData);
        }

        /// <summary>
        /// Every recompute of an agent's properties passes here (spawn, weapon switch, mount, and the
        /// ones <see cref="EnduranceLogic"/> asks for on an exhaustion change). The base model first
        /// (outside our try - its exceptions are the game's own), then the attack-speed penalty: the
        /// agent's CURRENT multiplier from the endurance logic (a float per fighter, 1 = none; the
        /// cliff of DESIGN §2 today, a curve after step 5c), read at this moment - so any recompute
        /// the game does on its own keeps the penalty. Our exception → the base values stand
        /// (no penalty), counted, the first per mission logged.
        /// </summary>
        public override void UpdateAgentStats(Agent agent, AgentDrivenProperties agentDrivenProperties)
        {
            SyncAiLevel();
            BaseModel.UpdateAgentStats(agent, agentDrivenProperties);
            try
            {
                float factor = EnduranceLogic.SpeedMultiplierFor(agent);
                if (factor != 1f)
                {
                    SpeedPenalty.Scale(agentDrivenProperties, factor);
                    EnduranceLogic.NoteDecoratorScaled();
                }
            }
            catch (Exception e)
            {
                EnduranceLogic.Failed("speed.decorator", e);
            }
        }

        public override float GetDifficultyModifier() => BaseModel.GetDifficultyModifier();

        public override bool CanAgentRideMount(Agent agent, Agent targetMount) => BaseModel.CanAgentRideMount(agent, targetMount);

        public override float GetWeaponDamageMultiplier(Agent agent, WeaponComponentData weapon) => BaseModel.GetWeaponDamageMultiplier(agent, weapon);

        public override float GetEquipmentStealthBonus(Agent agent) => BaseModel.GetEquipmentStealthBonus(agent);

        public override float GetSneakAttackMultiplier(Agent agent, WeaponComponentData weapon) => BaseModel.GetSneakAttackMultiplier(agent, weapon);

        public override float GetKnockBackResistance(Agent agent) => BaseModel.GetKnockBackResistance(agent);

        public override float GetKnockDownResistance(Agent agent, StrikeType strikeType = StrikeType.Invalid) => BaseModel.GetKnockDownResistance(agent, strikeType);

        public override float GetDismountResistance(Agent agent) => BaseModel.GetDismountResistance(agent);

        public override float GetBreatheHoldMaxDuration(Agent agent, float baseBreatheHoldMaxDuration) => BaseModel.GetBreatheHoldMaxDuration(agent, baseBreatheHoldMaxDuration);

        // ------------------------------------------------------------------ virtual members (forward ALL)

        public override void InitializeMissionEquipment(Agent agent) => BaseModel.InitializeMissionEquipment(agent);

        public override void InitializeAgentStatsAfterDeploymentFinished(Agent agent)
        {
            SyncAiLevel();
            BaseModel.InitializeAgentStatsAfterDeploymentFinished(agent);
        }

        public override void InitializeMissionEquipmentAfterDeploymentFinished(Agent agent) => BaseModel.InitializeMissionEquipmentAfterDeploymentFinished(agent);

        public override bool HasHeavyArmor(Agent agent) => BaseModel.HasHeavyArmor(agent);

        public override float GetEffectiveArmorEncumbrance(Agent agent, Equipment equipment) => BaseModel.GetEffectiveArmorEncumbrance(agent, equipment);

        public override float GetEffectiveMaxHealth(Agent agent) => BaseModel.GetEffectiveMaxHealth(agent);

        public override float GetEnvironmentSpeedFactor(Agent agent) => BaseModel.GetEnvironmentSpeedFactor(agent);

        public override float GetWeaponInaccuracy(Agent agent, WeaponComponentData weapon, int weaponSkill) => BaseModel.GetWeaponInaccuracy(agent, weapon, weaponSkill);

        public override float GetDetachmentCostMultiplierOfAgent(Agent agent, IDetachment detachment) => BaseModel.GetDetachmentCostMultiplierOfAgent(agent, detachment);

        public override float GetInteractionDistance(Agent agent) => BaseModel.GetInteractionDistance(agent);

        public override float GetMaxCameraZoom(Agent agent) => BaseModel.GetMaxCameraZoom(agent);

        public override int GetEffectiveSkill(Agent agent, SkillObject skill) => BaseModel.GetEffectiveSkill(agent, skill);

        public override int GetEffectiveSkillForWeapon(Agent agent, WeaponComponentData weapon) => BaseModel.GetEffectiveSkillForWeapon(agent, weapon);

        public override string GetMissionDebugInfoForAgent(Agent agent) => BaseModel.GetMissionDebugInfoForAgent(agent);
    }
}

using System;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 13 - the player's attack INPUT GATE (AI_NOTES "Step 13"). MissionMainAgentController writes
    /// the player's input into his agent's MovementFlags in its OnPreMissionTick, and the engine reads
    /// them right after the mission's pre-tick. Behaviours pre-tick in REVERSE list order and a mod's
    /// logic is appended LAST (it would run before the controller and be overwritten), so this tiny
    /// behaviour is added with the public AddMissionBehavior and then moved to index 0 of the public
    /// Mission.MissionBehaviors - it pre-ticks after everyone, i.e. after the controller, and asks
    /// <see cref="AthleticsLogic.GatePlayerInput"/> to clear the attack bits while the player's
    /// no-attack timer holds. It does nothing else, in no other hook: no native call at all while no
    /// timer runs. No Harmony, nothing patched.
    /// </summary>
    internal sealed class PlayerAttackGate : MissionLogic
    {
        private readonly AthleticsLogic _logic;

        private PlayerAttackGate(AthleticsLogic logic)
        {
            _logic = logic;
        }

        public override void OnPreMissionTick(float dt)
        {
            _logic.GatePlayerInput(); // never throws (its own try: an exception releases the hold)
        }

        /// <summary>
        /// Adds the gate for <paramref name="logic"/>'s mission and moves it to the front of the behaviour
        /// list (inside SubModule.OnMissionBehaviorInitialize - Mission.AfterStart has not begun enumerating
        /// the list for EarlyStart yet). Returns the log text: where it sits and where the controller is.
        /// </summary>
        internal static string Attach(Mission mission, AthleticsLogic logic)
        {
            var gate = new PlayerAttackGate(logic);
            mission.AddMissionBehavior(gate);
            var list = mission.MissionBehaviors;
            list.Remove(gate);
            list.Insert(0, gate);
            logic.Gate = gate;
            int controller = list.FindIndex(b => b is MissionMainAgentController);
            return "attached: your attack gate FIRST in the behaviour list (0 of " + list.Count + "; behaviours pre-tick from the end, so it runs right after "
                   + (controller >= 0 ? "MissionMainAgentController at " + controller : "the player controller - none in the list yet, checked again at your first pause")
                   + ") - while your attack pause holds it clears only the attack bits of your input; blocking, kicks, moving, weapon switches untouched";
        }
    }
}

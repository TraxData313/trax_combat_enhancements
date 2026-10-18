using System;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 55: every GAME call of cavalry flocking behind one seam (the step-23 / 41 / 49 pattern): the logic keeps the flocks, the eagerness, the
    /// flips, every end path and the stats; this touches the engine. The real one is <see cref="GameFlockBody"/>; the offline smoke plays a stand-in.
    /// </summary>
    internal interface IFlockBody
    {
        /// <summary>This mission has flocking: a battle mode, not a tournament / arena (by behaviour name), not naval.</summary>
        bool MissionAllows();

        bool IsActive(TrackedAgent st);

        bool IsMounted(TrackedAgent st);

        /// <summary>Why the rider is not part of it (<see cref="FlockSkip.Count"/> = he is); <paramref name="flocking"/> = our own scripted walk is
        /// on him (then the game's GoToPosition flag is ours).</summary>
        FlockSkip Gate(TrackedAgent st, bool flocking);

        /// <summary>His kit, read once: a bow / crossbow / sling with ammo; throwing weapons with some left.</summary>
        void ReadKind(TrackedAgent st, out bool rangedWithAmmo, out bool thrown);

        /// <summary>His team's index and his formation's index (false = none).</summary>
        bool Key(TrackedAgent st, out int team, out int formation);

        bool Position(TrackedAgent st, out double x, out double y);

        /// <summary>The whole unit's centre of mass (his formation's <c>CachedAveragePosition</c>).</summary>
        bool FormationCentre(TrackedAgent st, out double x, out double y);

        /// <summary>His formation's GOAL: the charge-to-target order's target formation, else the formation's own target formation, else the closest
        /// enemy formation the game caches - its centre (false = none).</summary>
        bool Goal(TrackedAgent st, out double x, out double y);

        /// <summary>The game lets us script him now (<c>CanBeAssignedForScriptedMovement</c>).</summary>
        bool CanStart(TrackedAgent st);

        /// <summary>Ride to (x, y): <c>Agent.SetScriptedPosition</c> (false = no ground there - nothing sent).</summary>
        bool MoveTo(TrackedAgent st, double x, double y);

        /// <summary>Our scripted walk off (<c>DisableScriptedMovement</c>, only while the game's GoToPosition flag is still on).</summary>
        void Release(TrackedAgent st);

        /// <summary>The flocking rider's horseback weights (<c>HumanAIComponent.OverrideBehaviorParams</c>).</summary>
        void Tighten(TrackedAgent st);

        /// <summary>The game's own weights back (<c>Agent.RefreshBehaviorValues</c> with his formation's order and arrangement).</summary>
        void Restore(TrackedAgent st);

        /// <summary>The steering lever's name for the log.</summary>
        string Lever { get; }
    }

    /// <summary>
    /// The engine side (AI_NOTES "Step 55", RESEARCH §T): the rider is ridden with <c>Agent.SetScriptedPosition</c> - vanilla's own way of riding an
    /// AI horse (TownHorseRaceAgentController 53 / 70 / 83, JoustingAgentController 264-293); released with <c>DisableScriptedMovement</c>; the goal
    /// from <c>MovementOrder.TargetFormation</c> / <c>Formation.TargetFormation</c> / <c>Formation.CachedClosestEnemyFormation</c>. Main thread.
    /// </summary>
    internal sealed class GameFlockBody : IFlockBody
    {
        private Mission? _kindOf;
        private bool _kindOk;

        public string Lever => "Agent.SetScriptedPosition (vanilla's own way of riding an AI horse - the horse race and the joust)";

        public bool MissionAllows()
        {
            var m = Mission.Current;
            if (m == null || m.Mode != MissionMode.Battle || m.MissionEnded) return false;
            if (!ReferenceEquals(_kindOf, m))
            {
                _kindOf = m;
                _kindOk = true;
                try
                {
                    if (m.IsNavalBattle || m.IsNavalRaidBattle) _kindOk = false;
                    else
                        foreach (var b in m.MissionBehaviors)
                        {
                            string name = b?.GetType().Name ?? string.Empty;
                            if (name.IndexOf("Tournament", StringComparison.Ordinal) >= 0 || name.IndexOf("Arena", StringComparison.Ordinal) >= 0)
                            {
                                _kindOk = false;
                                break;
                            }
                        }
                }
                catch
                {
                    _kindOk = false;
                }
            }
            return _kindOk;
        }

        public bool IsActive(TrackedAgent st) => st.Agent.IsActive();

        public bool IsMounted(TrackedAgent st) => st.Agent.MountAgent != null;

        public FlockSkip Gate(TrackedAgent st, bool flocking)
        {
            var a = st.Agent;
            if (!a.IsAIControlled) return FlockSkip.NotAi;
            var f = a.Formation;
            if (f == null) return FlockSkip.NoFormation;
            if (a.IsDetachedFromFormation || a.IsUsingGameObject || a.IsInLadderQueue || a.IsRunningAway || a.AIMoveToGameObjectIsEnabled()) return FlockSkip.Busy;
            if (!flocking && (a.GetScriptedFlags() & Agent.AIScriptedFrameFlags.GoToPosition) != 0) return FlockSkip.Busy;
            var order = f.GetReadonlyMovementOrderReference().OrderEnum;
            if (order != MovementOrder.MovementOrderEnum.Charge && order != MovementOrder.MovementOrderEnum.ChargeToTarget) return FlockSkip.NotCharging;
            return FlockSkip.Count;
        }

        public void ReadKind(TrackedAgent st, out bool rangedWithAmmo, out bool thrown)
        {
            rangedWithAmmo = thrown = false;
            var eq = st.Agent.Equipment;
            if (eq == null) return;
            bool launcher = false, ammo = false;
            for (var i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.ExtraWeaponSlot; i++)
            {
                var w = eq[i];
                if (w.IsEmpty) continue;
                var list = w.Item?.Weapons;
                if (list == null) continue;
                for (int k = 0; k < list.Count; k++)
                {
                    var u = list[k];
                    if (u == null) continue;
                    switch (u.WeaponClass)
                    {
                        case WeaponClass.Bow:
                        case WeaponClass.Crossbow:
                        case WeaponClass.Sling:
                            launcher = true;
                            break;
                        case WeaponClass.Arrow:
                        case WeaponClass.Bolt:
                        case WeaponClass.SlingStone:
                            if (w.Amount > 0) ammo = true;
                            break;
                        case WeaponClass.Javelin:
                        case WeaponClass.ThrowingAxe:
                        case WeaponClass.ThrowingKnife:
                            if (w.Amount > 0) thrown = true;
                            break;
                    }
                }
            }
            rangedWithAmmo = launcher && ammo;
        }

        public bool Key(TrackedAgent st, out int team, out int formation)
        {
            var a = st.Agent;
            team = a.Team?.TeamIndex ?? -1;
            var f = a.Formation;
            formation = f != null ? (int)f.FormationIndex : -1;
            return team >= 0 && formation >= 0;
        }

        public bool Position(TrackedAgent st, out double x, out double y)
        {
            var p = st.Agent.Position;
            x = p.x;
            y = p.y;
            return true;
        }

        public bool FormationCentre(TrackedAgent st, out double x, out double y)
        {
            var f = st.Agent.Formation;
            x = y = double.NaN;
            if (f == null || f.CountOfUnits <= 0) return false;
            var c = f.CachedAveragePosition;
            if (!c.IsValid) return false;
            x = c.x;
            y = c.y;
            return true;
        }

        public bool Goal(TrackedAgent st, out double x, out double y)
        {
            x = y = double.NaN;
            var f = st.Agent.Formation;
            if (f == null) return false;
            var target = f.GetReadonlyMovementOrderReference().TargetFormation;
            if (target == null || target.CountOfUnits <= 0) target = f.TargetFormation;
            if (target == null || target.CountOfUnits <= 0) target = f.CachedClosestEnemyFormation?.Formation;
            if (target == null || target.CountOfUnits <= 0) return false;
            var c = target.CachedAveragePosition;
            if (!c.IsValid) return false;
            x = c.x;
            y = c.y;
            return true;
        }

        public bool CanStart(TrackedAgent st) => st.Agent.CanBeAssignedForScriptedMovement();

        public bool MoveTo(TrackedAgent st, double x, double y)
        {
            var a = st.Agent;
            var to = new Vec2((float)x, (float)y);
            WorldPosition wp = a.GetWorldPosition();
            wp.SetVec2(to);
            if (float.IsNaN(wp.GetNavMeshZ())) return false;
            a.SetScriptedPosition(ref wp, false, Agent.AIScriptedFrameFlags.None);
            return true;
        }

        public void Release(TrackedAgent st)
        {
            var a = st.Agent;
            if (st.Removed || !a.IsActive()) return;
            if ((a.GetScriptedFlags() & Agent.AIScriptedFrameFlags.GoToPosition) != 0) a.DisableScriptedMovement();
        }

        public void Tighten(TrackedAgent st)
        {
            var h = st.Agent.HumanAIComponent;
            if (h == null) return;
            h.OverrideBehaviorParams(HumanAIComponent.AISimpleBehaviorKind.ChargeHorseback, FlockMath.FlockChargeY1, FlockMath.FlockChargeX2, FlockMath.FlockChargeY2,
                FlockMath.FlockChargeX3, FlockMath.FlockChargeY3);
            h.OverrideBehaviorParams(HumanAIComponent.AISimpleBehaviorKind.RangedHorseback, FlockMath.FlockRangedY1, FlockMath.FlockRangedX2, FlockMath.FlockRangedY2,
                FlockMath.FlockRangedX3, FlockMath.FlockRangedY3);
        }

        public void Restore(TrackedAgent st)
        {
            var a = st.Agent;
            if (st.Removed || !a.IsActive()) return;
            var f = a.Formation;
            if (f != null) a.RefreshBehaviorValues(f.GetReadonlyMovementOrderReference().OrderEnum, f.ArrangementOrder.OrderEnum);
            else a.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.Default);
        }
    }

    /// <summary>The flock body every new <see cref="AthleticsLogic"/> starts with - the game's; the offline smoke swaps in its stand-in once,
    /// before any logic is built (a native read on its fake agents would crash the process).</summary>
    internal static class FlockBodyDefault
    {
        internal static IFlockBody Current = new GameFlockBody();
    }
}

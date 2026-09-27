using System;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>One fighter's step back (DESIGN §2, step 5d): the swing that asked for it (pending
    /// until the next tick) and, once started, the plan and what the log measures. Allocated once per
    /// fighter who ever rolls a yes - reused after (no per-tick allocation). Main thread.</summary>
    internal sealed class StepBackState
    {
        // ---- asked for by a swing's end, started by the next tick
        public bool Pending;
        public double PendingAt;
        public double PendingF;
        public double PendingChance;

        // ---- running
        public bool Active;
        public double StartedAt;
        public StepBackPlan Plan;
        public bool MidSampled;
        public StepBackSnapshot Mid;
        public int HitsTaken;
        public int HitsBlocked;
        public int Swings;

        /// <summary>The mission's first step back - its start and end are logged in full.</summary>
        public bool First;
    }

    /// <summary>What a step back was started with (filled by <see cref="IStepBackBody.Probe"/>).</summary>
    internal struct StepBackPlan
    {
        public Agent? Enemy;
        public Vec3 From;
        public Vec3 Spot;
        public WorldPosition WorldSpot;
        public float Radians;
        public double EnemyDistance;
        public double StartFacingCos;
        public Formation? Formation;
        public int MovementOrder;
        public int Arrangement;
        public int MovementState;
        public int FlagsBefore;
        public int FlagsAfter;

        /// <summary>The scripted flags WE added (not set before): cleared by hand if the release leaves them.</summary>
        public int OurFlags;
    }

    /// <summary>Where a stepping-back man is and how he stands (mid-step and at the end).</summary>
    internal struct StepBackSnapshot
    {
        public bool Valid;
        public Vec3 Position;
        public double FacingCos;
        public double SpeedAway;
        public double EnemyDistance;
    }

    /// <summary>What the release did.</summary>
    internal struct StepBackRelease
    {
        public bool Released;
        public bool StillScripted;
        public bool FlagsCleared;
        public int FlagsAfter;
        public StepBackSnapshot End;
    }

    /// <summary>
    /// Every GAME call of the step back, behind one seam: the logic keeps the bookkeeping (dice,
    /// queue, cap, timers, every release path, the stats), this does the engine work. The real one is
    /// <see cref="GameStepBackBody"/>; the offline smoke hands the logic a stand-in (its agents have no
    /// native side) so the whole bookkeeping runs without the game.
    /// </summary>
    internal interface IStepBackBody
    {
        /// <summary>This mission allows step backs NOW (a field-battle mode, not a tournament / arena /
        /// duel / naval battle, not ending). Managed reads - it runs at every AI swing's end.</summary>
        bool MissionAllows(Mission? mission);

        /// <summary>One line for the log: what kind of mission this is for the step back.</summary>
        string MissionNote(Mission? mission);

        /// <summary>The game's safety checks for this man now, and the plan (the spot, the facing).</summary>
        StepBackRefusal Probe(TrackedAgent st, in StepBackRules r, ref StepBackPlan plan);

        /// <summary>Issues the scripted step. False: the engine did not take it (then it was disabled
        /// again at once).</summary>
        bool Start(TrackedAgent st, ref StepBackPlan plan, in StepBackRules r);

        /// <summary>Why it must end now, before its time (<see cref="StepBackEnd.None"/> = go on).</summary>
        StepBackEnd Check(TrackedAgent st, in StepBackPlan plan);

        /// <summary>Where he is and how he stands now. False when it cannot be read.</summary>
        bool Sample(TrackedAgent st, in StepBackPlan plan, out StepBackSnapshot s);

        /// <summary>Releases him to his formation (<c>DisableScriptedMovement</c>) and checks nothing
        /// of ours is left.</summary>
        StepBackRelease Release(TrackedAgent st, in StepBackPlan plan);
    }

    /// <summary>
    /// The engine side (AI_NOTES "Step 5d"): vanilla's own gate <c>CanBeAssignedForScriptedMovement</c>,
    /// the formation's orders, the target, the navmesh (<c>GetNavMeshZ</c>, <c>CanMoveDirectlyToPosition</c>),
    /// then <c>SetScriptedPositionAndDirection</c> (walk, + NoAttack while StepBackHoldAttacks) and
    /// <c>DisableScriptedMovement</c>. Main thread (the logic's tick), never inside an engine callback.
    /// </summary>
    internal sealed class GameStepBackBody : IStepBackBody
    {
        private const int GoToPosition = (int)Agent.AIScriptedFrameFlags.GoToPosition;
        private const int NoAttack = (int)Agent.AIScriptedFrameFlags.NoAttack;
        private const int DoNotRun = (int)Agent.AIScriptedFrameFlags.DoNotRun;
        private const int ConsiderRotation = (int)Agent.AIScriptedFrameFlags.ConsiderRotation;

        private Mission? _kindOf;
        private string? _kindBlock;

        // ------------------------------------------------------------------ the mission

        public bool MissionAllows(Mission? mission)
        {
            // (IsTeleportingAgents: the engine would TELEPORT a scripted man instead of walking him)
            if (mission == null || mission.Mode != MissionMode.Battle || mission.MissionEnded || mission.IsTeleportingAgents) return false;
            return KindBlock(mission) == null;
        }

        public string MissionNote(Mission? mission)
        {
            if (mission == null) return "no mission";
            string? block = KindBlock(mission);
            return block != null
                ? "this mission is " + block + " - no step backs here (vanilla AI)"
                : "this mission allows step backs while it is in battle mode (now: " + mission.Mode + ")";
        }

        /// <summary>The fixed part, decided once per mission: naval battles, tournaments / arena fights
        /// (they run in battle mode too - told apart by their behaviours, by type NAME: no SandBox
        /// reference), duels. Null = allowed.</summary>
        private string? KindBlock(Mission mission)
        {
            if (ReferenceEquals(_kindOf, mission)) return _kindBlock;
            _kindOf = mission;
            _kindBlock = null;
            try
            {
                if (mission.IsNavalBattle || mission.IsNavalRaidBattle) _kindBlock = "a naval battle (moving decks)";
                else
                {
                    foreach (var b in mission.MissionBehaviors)
                    {
                        string name = b?.GetType().Name ?? string.Empty;
                        if (name.IndexOf("Tournament", StringComparison.Ordinal) >= 0 || name.IndexOf("Arena", StringComparison.Ordinal) >= 0)
                        {
                            _kindBlock = "a tournament or arena fight (" + name + ")";
                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _kindBlock = "unknown (" + e.GetType().Name + ")";
            }
            return _kindBlock;
        }

        // ------------------------------------------------------------------ before the start

        public StepBackRefusal Probe(TrackedAgent st, in StepBackRules r, ref StepBackPlan plan)
        {
            var a = st.Agent;
            if (!a.IsActive()) return StepBackRefusal.Gone;
            if (a.IsMainAgent || a.MountAgent != null) return StepBackRefusal.NoLongerEligible;
            if (!a.IsAIControlled) return StepBackRefusal.NotAiControlled;
            if (a.HumanAIComponent == null) return StepBackRefusal.NoAiComponent;
            // vanilla's own gate: detached (siege engines, ladders, towers, strategic areas), ladder
            // queue, an object, running away, already scripted (incl. already stepping back)
            if (!a.CanBeAssignedForScriptedMovement()) return StepBackRefusal.Busy;
            if (a.IsRetreating()) return StepBackRefusal.Routing;

            var formation = a.Formation;
            plan.Formation = formation;
            plan.MovementOrder = plan.Arrangement = plan.MovementState = -1;
            if (formation != null)
            {
                ref readonly var order = ref formation.GetReadonlyMovementOrderReference();
                plan.MovementOrder = (int)order.OrderEnum;
                plan.MovementState = (int)order.MovementState;
                var arrangement = formation.ArrangementOrder.OrderEnum;
                plan.Arrangement = (int)arrangement;
                if (arrangement == ArrangementOrder.ArrangementOrderEnum.ShieldWall || arrangement == ArrangementOrder.ArrangementOrderEnum.Square
                    || arrangement == ArrangementOrder.ArrangementOrderEnum.Circle)
                    return StepBackRefusal.HoldingArrangement;
                if (order.OrderEnum == MovementOrder.MovementOrderEnum.Retreat || order.MovementState == MovementOrder.MovementStateEnum.Retreat)
                    return StepBackRefusal.RetreatOrder;
            }

            var enemy = a.GetTargetAgent();
            if (enemy == null || !enemy.IsActive() || !enemy.IsEnemyOf(a)) return StepBackRefusal.NoEnemy;
            Vec3 pos = a.Position, ep = enemy.Position;
            double dist = StepBackMath.Distance2D(pos.x, pos.y, ep.x, ep.y);
            if (dist > r.EnemyRange) return StepBackRefusal.EnemyTooFar;
            if (!StepBackMath.AwayFrom(pos.x, pos.y, ep.x, ep.y, r.Distance, out double sx, out double sy)) return StepBackRefusal.Blocked;

            // the spot on the navmesh, starting from his own face: NaN = off the navmesh; a big height
            // step = a wall edge or stairs; then a straight, clear walk to it
            var spot2 = new Vec2((float)sx, (float)sy);
            WorldPosition wp = a.GetWorldPosition();
            wp.SetVec2(spot2);
            float z = wp.GetNavMeshZ();
            if (float.IsNaN(z)) return StepBackRefusal.OffNavMesh;
            if (!StepBackMath.LevelEnough(pos.z, z)) return StepBackRefusal.NotLevel;
            if (!a.CanMoveDirectlyToPosition(in spot2)) return StepBackRefusal.Blocked;

            Vec3 look = a.LookDirection;
            plan.Enemy = enemy;
            plan.From = pos;
            plan.Spot = new Vec3(spot2.x, spot2.y, z);
            plan.WorldSpot = wp;
            plan.Radians = StepBackMath.Radians(ep.x - sx, ep.y - sy); // face the enemy from the spot
            plan.EnemyDistance = dist;
            plan.StartFacingCos = StepBackMath.FacingCosine(look.x, look.y, ep.x - pos.x, ep.y - pos.y);
            plan.FlagsBefore = (int)a.GetScriptedFlags();
            return StepBackRefusal.None;
        }

        // ------------------------------------------------------------------ start

        public bool Start(TrackedAgent st, ref StepBackPlan plan, in StepBackRules r)
        {
            var a = st.Agent;
            var flags = Agent.AIScriptedFrameFlags.DoNotRun;
            if (r.HoldAttacks) flags |= Agent.AIScriptedFrameFlags.NoAttack;
            var wp = plan.WorldSpot;
            a.SetScriptedPositionAndDirection(ref wp, plan.Radians, addHumanLikeDelay: false, flags);
            int after = (int)a.GetScriptedFlags();
            plan.FlagsAfter = after;
            plan.OurFlags = ((int)flags | ConsiderRotation) & ~plan.FlagsBefore;
            if ((after & GoToPosition) != 0) return true;
            // not taken: make sure nothing of ours lingers (a late apply would never be released)
            a.DisableScriptedMovement();
            ClearOurFlags(a, plan.OurFlags);
            return false;
        }

        // ------------------------------------------------------------------ while it runs

        public StepBackEnd Check(TrackedAgent st, in StepBackPlan plan)
        {
            var a = st.Agent;
            if (!a.IsActive()) return StepBackEnd.NotActive;
            if (a.IsMainAgent || !a.IsAIControlled) return StepBackEnd.PlayerControl;
            if (a.MountAgent != null) return StepBackEnd.Mounted;
            if (a.IsRunningAway || a.IsRetreating()) return StepBackEnd.Routing;
            // the game's own scripted jobs: leave them alone (never cancel them)
            if (a.IsInLadderQueue || a.IsUsingGameObject || a.AIMoveToGameObjectIsEnabled()) return StepBackEnd.HandedOver;
            if (a.IsDetachedFromFormation) return StepBackEnd.Detached;
            var formation = a.Formation;
            if (!ReferenceEquals(formation, plan.Formation)) return StepBackEnd.FormationChanged;
            if (formation != null)
            {
                ref readonly var order = ref formation.GetReadonlyMovementOrderReference();
                if ((int)order.OrderEnum != plan.MovementOrder || (int)formation.ArrangementOrder.OrderEnum != plan.Arrangement)
                    return StepBackEnd.OrderChanged;
            }
            if (((int)a.GetScriptedFlags() & GoToPosition) == 0) return StepBackEnd.ClearedByGame;
            return StepBackEnd.None;
        }

        public bool Sample(TrackedAgent st, in StepBackPlan plan, out StepBackSnapshot s)
        {
            s = default;
            var a = st.Agent;
            if (!a.IsActive()) return false;
            Vec3 p = a.Position;
            s.Valid = true;
            s.Position = p;
            s.FacingCos = double.NaN;
            s.SpeedAway = double.NaN;
            s.EnemyDistance = double.NaN;
            var enemy = plan.Enemy != null && plan.Enemy.IsActive() ? plan.Enemy : a.GetTargetAgent();
            if (enemy == null || !enemy.IsActive()) return true;
            Vec3 ep = enemy.Position, look = a.LookDirection, v = a.Velocity;
            double tx = ep.x - p.x, ty = ep.y - p.y;
            s.FacingCos = StepBackMath.FacingCosine(look.x, look.y, tx, ty);
            s.SpeedAway = StepBackMath.SpeedAway(v.x, v.y, tx, ty);
            s.EnemyDistance = StepBackMath.Distance2D(p.x, p.y, ep.x, ep.y);
            return true;
        }

        // ------------------------------------------------------------------ release

        public StepBackRelease Release(TrackedAgent st, in StepBackPlan plan)
        {
            var rel = new StepBackRelease();
            var a = st.Agent;
            if (!a.IsActive()) return rel;
            Sample(st, in plan, out rel.End);
            a.DisableScriptedMovement();
            rel.Released = true;
            int flags = (int)a.GetScriptedFlags();
            rel.StillScripted = (flags & GoToPosition) != 0;
            rel.FlagsCleared = (flags & plan.OurFlags) != 0 && ClearOurFlags(a, plan.OurFlags);
            rel.FlagsAfter = (int)a.GetScriptedFlags();
            return rel;
        }

        /// <summary>Clears the flags we added if they outlived the scripted frame (the engine is
        /// expected to drop them with it - item pickup's NoAttack goes that way; counted if not).</summary>
        private static bool ClearOurFlags(Agent a, int ours)
        {
            int flags = (int)a.GetScriptedFlags();
            int left = flags & ours & (NoAttack | DoNotRun | ConsiderRotation);
            if (left == 0) return false;
            a.SetScriptedFlags((Agent.AIScriptedFrameFlags)(flags & ~left));
            return true;
        }
    }
}

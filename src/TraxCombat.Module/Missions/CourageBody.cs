using System;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 49: one look's neighbours (the courage circle), filled by <see cref="ICourageBody.Scan"/> into REUSED arrays - no allocation per
    /// look. Each entry: the man's record (only men we track), his distance, whether he is an enemy of the looker, where he stands.
    /// </summary>
    internal sealed class CourageScan
    {
        /// <summary>The most neighbours one look keeps (a crowd beyond it is cut and counted - plumbing).</summary>
        internal const int Capacity = 128;

        public readonly TrackedAgent?[] Men = new TrackedAgent?[Capacity];
        public readonly float[] Dist = new float[Capacity];
        public readonly float[] X = new float[Capacity];
        public readonly float[] Y = new float[Capacity];
        public readonly bool[] Enemy = new bool[Capacity];
        public int Count;
        public bool Truncated;

        /// <summary>The logic's record lookup (a delegate made once per logic).</summary>
        public Func<Agent, TrackedAgent?>? Lookup;

        public void Clear()
        {
            for (int i = 0; i < Count; i++) Men[i] = null;
            Count = 0;
            Truncated = false;
        }

        /// <summary>Adds one neighbour (false when full - counted as truncated).</summary>
        public bool Add(TrackedAgent man, float dist, float x, float y, bool enemy)
        {
            if (Count >= Capacity)
            {
                Truncated = true;
                return false;
            }
            Men[Count] = man;
            Dist[Count] = dist;
            X[Count] = x;
            Y[Count] = y;
            Enemy[Count] = enemy;
            Count++;
            return true;
        }
    }

    /// <summary>
    /// Every GAME call of courage behind one seam (the step-23 / 33 / 41 pattern): the logic keeps the minds, the odds, every end path and
    /// the stats; this touches the engine. The real one is <see cref="GameCourageBody"/>; the offline smoke plays a stand-in.
    /// </summary>
    internal interface ICourageBody
    {
        /// <summary>This mission has courage: a battle mode, not a tournament / arena (by behaviour name), not naval.</summary>
        bool MissionAllows();

        bool IsActive(TrackedAgent st);

        /// <summary>Why he is not looked at (<see cref="CourageSkip.Count"/> = he is). The logic adds the player and the wall's held men.</summary>
        CourageSkip Gate(TrackedAgent st);

        /// <summary>His kit, read once: armour (head + body + legs + arms), level, a two-hander / polearm and no shield.</summary>
        void ReadKit(TrackedAgent st, out double armour, out int level, out bool heavy);

        /// <summary>Health ÷ its maximum (0..1).</summary>
        double HealthShare(TrackedAgent st);

        /// <summary>The game's morale (0-100; −1 = none).</summary>
        double Morale(TrackedAgent st);

        bool Position(TrackedAgent st, out double x, out double y);

        /// <summary>His formation's centre of mass (the game's <c>CachedAveragePosition</c>).</summary>
        bool FormationCentre(TrackedAgent st, out double x, out double y);

        /// <summary>The tracked men within <paramref name="radius"/> of him (not him), into <paramref name="into"/> (cleared first).</summary>
        void Scan(TrackedAgent st, float radius, CourageScan into);

        /// <summary>His side's men (not him) within <paramref name="radius"/>: how many, and their centroid.</summary>
        int Flock(TrackedAgent st, float radius, out double x, out double y);

        /// <summary>A world direction as his input vector (his own frame, full stick).</summary>
        bool ToLocal(TrackedAgent st, double dx, double dy, out float lx, out float ly);

        /// <summary>The ground a little further along the way is walkable (step 16's edge check).</summary>
        bool GroundOk(TrackedAgent st, double dx, double dy);

        /// <summary>The timid man's behaviour weights (HumanAIComponent.OverrideBehaviorParams).</summary>
        void Tighten(TrackedAgent st, double reach);

        /// <summary>The game's own behaviour weights back (Agent.RefreshBehaviorValues with his formation's order and arrangement).</summary>
        void Restore(TrackedAgent st);

        AiInputHook.HookResult Hook(TrackedAgent st);

        /// <summary>The walk's wish is off: the callback flag goes off if he is idle and it was ours.</summary>
        void Release(TrackedAgent st);
    }

    /// <summary>
    /// The engine side (AI_NOTES "Step 49", RESEARCH §S): <c>Mission.GetNearbyAgents</c> / <c>GetNearbyAllyAgents</c> into one reused
    /// MBList (the game clears it first), <c>Formation.CachedAveragePosition</c> (the mean of its non-detached men - Formation.cs
    /// CacheAverageAndMedianPositionAndVelocity), <c>Agent.GetMorale()</c> (CommonAIComponent.Morale, 0-100), the kit from
    /// <c>SpawnEquipment</c>'s armour sums and <c>Character.Level</c>, <c>HumanAIComponent.OverrideBehaviorParams</c> and
    /// <c>Agent.RefreshBehaviorValues</c>. Main thread (the logic's tick).
    /// </summary>
    internal sealed class GameCourageBody : ICourageBody
    {
        private readonly MBList<Agent> _list = new MBList<Agent>(64);
        private Mission? _kindOf;
        private bool _kindOk;

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

        public CourageSkip Gate(TrackedAgent st)
        {
            var a = st.Agent;
            if (!a.IsAIControlled) return CourageSkip.NotAi;
            if (a.MountAgent != null) return CourageSkip.Mounted;
            if (a.Formation == null) return CourageSkip.NoFormation;
            if (a.IsDetachedFromFormation || a.IsUsingGameObject || a.IsInLadderQueue || a.IsRunningAway || a.AIMoveToGameObjectIsEnabled()) return CourageSkip.Busy;
            var m = Mission.Current;
            if (m != null && m.IsSiegeBattle && a.Team != null && a.Team.IsAttacker) return CourageSkip.SiegeAttacker;
            return CourageSkip.Count;
        }

        public void ReadKit(TrackedAgent st, out double armour, out int level, out bool heavy)
        {
            var a = st.Agent;
            heavy = false;
            ArmourAndLevel(a, out armour, out level);
            var eq = a.Equipment;
            if (eq == null) return;
            bool shield = false, big = false;
            for (var i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.ExtraWeaponSlot; i++)
            {
                var w = eq[i];
                if (w.IsEmpty) continue;
                if (w.IsShield())
                {
                    shield = true;
                    continue;
                }
                var list = w.Item?.Weapons;
                if (list == null) continue;
                for (int k = 0; k < list.Count; k++)
                {
                    var u = list[k];
                    if (u != null && u.IsMeleeWeapon && (u.IsTwoHanded || u.IsPolearm)) big = true;
                }
            }
            heavy = big && !shield;
        }

        public double HealthShare(TrackedAgent st) => HealthShareOf(st.Agent);

        /// <summary>Step 62 shares it (the impact model's toughness): health ÷ its limit (managed fields).</summary>
        internal static double HealthShareOf(Agent a)
        {
            float limit = a.HealthLimit;
            return limit > 0 ? a.Health / limit : 1;
        }

        /// <summary>His toughness's kit - the spawn equipment's head + body + legs + arms armour and his character's level (managed reads;
        /// step 62's impact toughness shares it).</summary>
        internal static void ArmourAndLevel(Agent a, out double armour, out int level)
        {
            armour = 0;
            level = 0;
            var spawn = a.SpawnEquipment;
            if (spawn != null) armour = spawn.GetHeadArmorSum() + spawn.GetHumanBodyArmorSum() + spawn.GetLegArmorSum() + spawn.GetArmArmorSum();
            var c = a.Character;
            if (c != null) level = c.Level;
        }

        public double Morale(TrackedAgent st) => st.Agent.GetMorale();

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
            if (f == null || f.CountOfUnits <= 0)
            {
                x = y = double.NaN;
                return false;
            }
            var c = f.CachedAveragePosition;
            x = c.x;
            y = c.y;
            return c.IsValid;
        }

        public void Scan(TrackedAgent st, float radius, CourageScan into)
        {
            into.Clear();
            var m = Mission.Current;
            var lookup = into.Lookup;
            if (m == null || lookup == null) return;
            var a = st.Agent;
            var p = a.Position;
            m.GetNearbyAgents(p.AsVec2, radius, _list);
            for (int i = 0; i < _list.Count; i++)
            {
                var o = _list[i];
                if (o == null || ReferenceEquals(o, a) || !o.IsHuman || !o.IsActive()) continue;
                var t = lookup(o);
                if (t == null) continue;
                var op = o.Position;
                float dx = op.x - p.x, dy = op.y - p.y;
                if (!into.Add(t, (float)Math.Sqrt(dx * dx + dy * dy), op.x, op.y, o.IsEnemyOf(a))) break;
            }
            _list.Clear();
        }

        public int Flock(TrackedAgent st, float radius, out double x, out double y)
        {
            x = y = double.NaN;
            var m = Mission.Current;
            var a = st.Agent;
            if (m == null || a.Team == null) return 0;
            m.GetNearbyAllyAgents(a.Position.AsVec2, radius, a.Team, _list);
            int n = 0;
            double sx = 0, sy = 0;
            for (int i = 0; i < _list.Count; i++)
            {
                var o = _list[i];
                if (o == null || ReferenceEquals(o, a) || !o.IsHuman || !o.IsActive()) continue;
                var op = o.Position;
                sx += op.x;
                sy += op.y;
                n++;
            }
            _list.Clear();
            if (n > 0)
            {
                x = sx / n;
                y = sy / n;
            }
            return n;
        }

        public bool ToLocal(TrackedAgent st, double dx, double dy, out float lx, out float ly)
        {
            lx = 0f;
            ly = 0f;
            var rot = st.Agent.Frame.rotation;
            if (!AiInputMath.BackpedalVector(dx, dy, rot.s.x, rot.s.y, rot.f.x, rot.f.y, AiInputMath.BackpedalInput, out double x, out double y)) return false;
            lx = (float)x;
            ly = (float)y;
            return true;
        }

        public bool GroundOk(TrackedAgent st, double dx, double dy)
        {
            var a = st.Agent;
            var p = a.Position;
            var ahead = new Vec2((float)(p.x + dx * AiInputMath.EdgeLookAhead), (float)(p.y + dy * AiInputMath.EdgeLookAhead));
            WorldPosition wp = a.GetWorldPosition();
            wp.SetVec2(ahead);
            float z = wp.GetNavMeshZ();
            if (float.IsNaN(z) || !StepBackMath.LevelEnough(p.z, z)) return false;
            return a.CanMoveDirectlyToPosition(in ahead);
        }

        public void Tighten(TrackedAgent st, double reach)
        {
            var h = st.Agent.HumanAIComponent;
            if (h == null) return;
            CourageMath.TimidMelee(reach, out float y1, out float x2, out float y2, out float x3, out float y3);
            h.OverrideBehaviorParams(HumanAIComponent.AISimpleBehaviorKind.Melee, y1, x2, y2, x3, y3);
            h.OverrideBehaviorParams(HumanAIComponent.AISimpleBehaviorKind.GoToPos, CourageMath.TimidGoToY1, CourageMath.TimidGoToX2, CourageMath.TimidGoToY2,
                CourageMath.TimidGoToX3, CourageMath.TimidGoToY3);
        }

        public void Restore(TrackedAgent st)
        {
            var a = st.Agent;
            if (st.Removed || !a.IsActive()) return;
            var f = a.Formation;
            if (f != null) a.RefreshBehaviorValues(f.GetReadonlyMovementOrderReference().OrderEnum, f.ArrangementOrder.OrderEnum);
            else a.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.Default);
        }

        public AiInputHook.HookResult Hook(TrackedAgent st) => AiInputHook.Hook(st);

        public void Release(TrackedAgent st)
        {
            if (st.Removed || !st.Agent.IsActive()) return;
            AiInputHook.UnhookIfIdle(st);
        }
    }

    /// <summary>The courage body every new <see cref="AthleticsLogic"/> starts with - the game's; the offline smoke swaps in its stand-in once,
    /// before any logic is built (a native read on its fake agents would crash the process).</summary>
    internal static class CourageBodyDefault
    {
        internal static ICourageBody Current = new GameCourageBody();
    }
}

using System;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 41: one fighter's shield-wall hold (AI_NOTES "Step 41") - held now or not, his slot in the held list, what vanilla's
    /// update timer last read, his next sample. Allocated once per man at his first look, reused - no per-tick allocation. Main
    /// thread (the logic's pre-tick).
    /// </summary>
    internal sealed class WallState
    {
        /// <summary>In the held list: the "keep your slot" flag is ours - or, while <see cref="Leash"/> is Free (step 42), he is
        /// stepping out on vanilla's own value and will be held again by the leash.</summary>
        public bool Held;

        /// <summary>His index in the logic's held list (−1 = not in it).</summary>
        public int ListIndex = -1;

        public bool EverHeld;

        /// <summary>The engine side's handle on vanilla's update timer (the game's <c>TaleWorlds.Core.Timer</c>; null = not
        /// readable) and whether it was looked for yet.</summary>
        public object? Timer;
        public bool TimerLooked;

        /// <summary>The timer's start as last read - it moves exactly when vanilla's own update ran (RESEARCH §O).</summary>
        public float LastStamp = float.NaN;

        /// <summary>When vanilla's last update on him was seen (mission time; NaN = none yet).</summary>
        public double LastVanillaAt = double.NaN;

        /// <summary>The fallback's next rewrite (only without a readable timer).</summary>
        public double NextRewrite;

        public double NextSample;

        /// <summary>Step 42: his leash - stepping out now (then he is in the held list but carries vanilla's value), back in his
        /// slot since his last step; the stepping man's next look; whether he ever stepped out this mission.</summary>
        public WallLeash Leash;
        public double NextLeashLook;
        public bool EverStepped;

        /// <summary>Step 63: out of place (held, beyond the leash ahead of his slot) and our walk back; the last look's time (the pull's
        /// budget).</summary>
        public WallOutOfPlace Out;
        public double LastOutLook = double.NaN;

        /// <summary>Step 68: the engine walks him to OUR paced frame now (not the game's order slot); ever paced this mission.</summary>
        public bool Paced;
        public bool EverPaced;

        /// <summary>Step 75: he STANDS FIRM now (held, his formation on a stop or a move at its place, the switch on); the order conditions alone (the
        /// log's ON / OFF compare); ever this mission; a move's "at its place" latch; the step-out leash step 42 WOULD run on him (the refused steps);
        /// how far out he stood at his last look (the strikes "from the slot"); step 68's pace refused for him already (counted once a spell).</summary>
        public bool Firm;
        public bool FirmCond;
        public bool EverFirm;
        public StandFirmLatch FirmLatch;
        public WallLeash FirmLeash;
        public double FirmOut = double.NaN;
        public bool FirmPaceRefused;
    }

    /// <summary>
    /// Every GAME call of the shield wall's hold behind one seam (the step-23 / step-33 pattern): the logic keeps the gates, the
    /// list, the timing and the stats, this touches the engine. The real one is <see cref="GameWallBody"/>; the offline smoke
    /// plays a stand-in (its agents have no native side).
    /// </summary>
    internal interface IWallBody
    {
        /// <summary>He is on the field (the engine's state).</summary>
        bool IsActive(TrackedAgent st);

        /// <summary>His gates and - in a defensive arrangement, not detached - the positions (the logic adds "not the player").</summary>
        WallFacts Read(TrackedAgent st);

        /// <summary>Vanilla's per-man update timer: true with its start now, false = not readable (the fallback rewrites).</summary>
        bool TryStamp(TrackedAgent st, WallState ws, out float stamp);

        /// <summary>The "keep your slot" write from the facts just read: vanilla's own call and numbers, the flag TRUE.</summary>
        void Hold(TrackedAgent st, in WallFacts f);

        /// <summary>Gives him back VANILLA's own value now (its formula - he is no longer held).</summary>
        void Release(TrackedAgent st);

        /// <summary>An enemy within <paramref name="metres"/> of him (the log's "in contact").</summary>
        bool EnemyWithin(TrackedAgent st, double metres);

        /// <summary>Step 42: the enemy he fights - the native AI's target, else his immediate enemy - and where he stands (the
        /// scene's x / y); false = none (or not an active enemy).</summary>
        bool TryEnemy(TrackedAgent st, out double x, out double y);

        /// <summary>Step 63: a world direction (the scene's x / y) as the backpedal's input in his own frame (false = no frame).</summary>
        bool ToLocal(TrackedAgent st, double dx, double dy, out float lx, out float ly);

        /// <summary>Step 63: step 16's input hook on him (the walk back's wish needs it).</summary>
        AiInputHook.HookResult Hook(TrackedAgent st);

        /// <summary>Step 63: the walk back's wish is off - the callback goes off if he is idle and it was ours.</summary>
        void ReleaseInput(TrackedAgent st);

        /// <summary>Step 68: the frame the engine walks him to = the point (<paramref name="x"/>, <paramref name="y"/>) with the game's own
        /// direction (instead of the game's order slot); false = the game gave no frame (nothing written).</summary>
        bool Pace(TrackedAgent st, double x, double y);

        /// <summary>Step 68: the game's own frame given back now (its order slot - what its next ~0.5 s update writes anyway).</summary>
        void Unpace(TrackedAgent st);
    }

    /// <summary>
    /// The engine side (AI_NOTES "Step 41", RESEARCH §O). Reads: the mission kind (<c>IsNavalBattle</c> / <c>IsNavalRaidBattle</c>,
    /// <c>IsSiegeBattle</c> + <c>Team.IsAttacker</c>), <c>IsAIControlled</c>, <c>MountAgent</c>, <c>Formation</c>,
    /// <c>IsDetachedFromFormation</c>, <c>ArrangementOrder.OrderEnum</c>, the order by step 23's <see cref="GameBraceBody.KindOf"/>,
    /// <c>IsRetreating()</c>; the slot = <c>Formation.GetCurrentGlobalPositionOfUnit(agent, blendWithOrderDirection: true)</c> - the
    /// very point vanilla hands the engine -, <c>CurrentDirection</c>, <c>CachedAveragePosition</c>,
    /// <c>CachedFormationIntegrityData</c>. Writes: <c>Agent.SetFormationIntegrityData</c> (public, straight to native). The timer:
    /// <c>Agent._cachedAndFormationValuesUpdateTimer</c> (private, read ONCE per man by reflection, then its public
    /// <c>StartTime</c>). Main thread (the logic's pre-tick - no agent job runs then).
    /// </summary>
    internal sealed class GameWallBody : IWallBody
    {
        /// <summary>The private field of vanilla's update timer (v1.4.8 name).</summary>
        internal const string TimerFieldName = "_cachedAndFormationValuesUpdateTimer";

        private static FieldInfo? _timerField;
        private static bool _timerFieldLooked;

        /// <summary>The timer field, looked up once (null = renamed by a game update - the fallback runs).</summary>
        internal static FieldInfo? TimerField
        {
            get
            {
                if (!_timerFieldLooked)
                {
                    _timerFieldLooked = true;
                    try
                    {
                        var f = typeof(Agent).GetField(TimerFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                        _timerField = f != null && f.FieldType == typeof(Timer) ? f : null;
                    }
                    catch
                    {
                        _timerField = null;
                    }
                }
                return _timerField;
            }
        }

        public bool IsActive(TrackedAgent st) => st.Agent.IsActive();

        public WallFacts Read(TrackedAgent st)
        {
            var a = st.Agent;
            var f = new WallFacts();
            var m = Mission.Current;
            if (m != null)
            {
                f.Naval = m.IsNavalBattle || m.IsNavalRaidBattle;
                f.SiegeAttacker = m.IsSiegeBattle && a.Team != null && a.Team.IsAttacker;
            }
            f.Ai = a.IsAIControlled;
            f.Mounted = a.MountAgent != null;
            var form = a.Formation;
            f.HasFormation = form != null;
            if (form == null) return f;
            f.Detached = a.IsDetachedFromFormation;
            f.Arrangement = Map(form.ArrangementOrder.OrderEnum);
            f.Order = GameBraceBody.KindOf(form);
            if (f.Detached || !WallMath.IsDefensive(f.Arrangement) || WallMath.OrderVerdict(f.Order) != WallVerdict.Hold) return f;
            f.Retreating = a.IsRetreating(); // native - only for a man the gates above let through
            var pos = a.Position.AsVec2;
            var slot = form.GetCurrentGlobalPositionOfUnit(a, true);
            var dir = form.CurrentDirection;
            var centre = form.CachedAveragePosition;
            var integrity = form.CachedFormationIntegrityData;
            f.HasPositions = true;
            f.AgentX = pos.x;
            f.AgentY = pos.y;
            f.SlotX = slot.x;
            f.SlotY = slot.y;
            f.DirX = dir.x;
            f.DirY = dir.y;
            f.CentreX = centre.x;
            f.CentreY = centre.y;
            f.AvgSpeed = integrity.AverageMaxUnlimitedSpeedExcludeFarAgents;
            f.VelX = integrity.AverageVelocityExcludeFarAgents.x;
            f.VelY = integrity.AverageVelocityExcludeFarAgents.y;
            f.Deviation = integrity.DeviationOfPositionsExcludeFarAgents;
            f.EnemyGap = double.NaN;
            if (f.Order == BraceOrderKind.Move)
            {
                // step 75: the formation's own place against its order position (a move "at its place" stands firm)
                var here = form.CurrentPosition;
                var there = form.OrderPosition;
                f.HasFormationOrder = form.OrderPositionIsValid;
                f.FormationX = here.x;
                f.FormationY = here.y;
                f.FormationOrderX = there.x;
                f.FormationOrderY = there.y;
            }
            if (WallPaceMath.Paces(f.Order))
            {
                // step 68: the game's ORDER slot (where vanilla walks him - under an advance at the enemy's line) and the gap to the enemy's front
                var order = form.GetOrderPositionOfUnit(a);
                if (order.IsValid)
                {
                    var o = order.AsVec2;
                    f.HasOrderSlot = true;
                    f.OrderX = o.x;
                    f.OrderY = o.y;
                }
                var enemy = form.CachedClosestEnemyFormation?.Formation;
                if (enemy != null)
                    f.EnemyGap = WallPaceMath.FrontGap(form.CachedMedianPosition.AsVec2.Distance(enemy.CachedMedianPosition.AsVec2), form.Depth, enemy.Depth);
            }
            return f;
        }

        internal static WallArrangement Map(ArrangementOrder.ArrangementOrderEnum e) => e switch
        {
            ArrangementOrder.ArrangementOrderEnum.ShieldWall => WallArrangement.ShieldWall,
            ArrangementOrder.ArrangementOrderEnum.Square => WallArrangement.Square,
            ArrangementOrder.ArrangementOrderEnum.Circle => WallArrangement.Circle,
            ArrangementOrder.ArrangementOrderEnum.Column => WallArrangement.Column,
            _ => WallArrangement.Other,
        };

        public bool TryStamp(TrackedAgent st, WallState ws, out float stamp)
        {
            if (!ws.TimerLooked)
            {
                ws.TimerLooked = true;
                var field = TimerField;
                ws.Timer = field?.GetValue(st.Agent) as Timer; // a reference: nothing boxed; once per man
            }
            if (ws.Timer is Timer t)
            {
                stamp = t.StartTime;
                return true;
            }
            stamp = 0f;
            return false;
        }

        public void Hold(TrackedAgent st, in WallFacts f) =>
            st.Agent.SetFormationIntegrityData(new Vec2((float)f.SlotX, (float)f.SlotY), new Vec2((float)f.DirX, (float)f.DirY),
                new Vec2((float)f.VelX, (float)f.VelY), (float)f.AvgSpeed, (float)f.Deviation, true);

        /// <summary>Vanilla's own formula (<c>HumanAIComponent.ParallelUpdateFormationMovement</c>, v1.4.8): a scattered formation
        /// (deviation above 3 x speed) = zeros and false; else the slot when within the catch-up radius (else zero) and the flag only
        /// for a man with something in the 5th slot of a mostly ranged formation.</summary>
        public void Release(TrackedAgent st)
        {
            var a = st.Agent;
            if (st.Removed || !a.IsActive()) return;
            var form = a.Formation;
            if (form == null) return;
            var integrity = form.CachedFormationIntegrityData;
            float limit = integrity.AverageMaxUnlimitedSpeedExcludeFarAgents * 3f;
            if (integrity.DeviationOfPositionsExcludeFarAgents > limit)
            {
                a.SetFormationIntegrityData(Vec2.Zero, Vec2.Zero, Vec2.Zero, 0f, 0f, false);
                return;
            }
            var slot = form.GetCurrentGlobalPositionOfUnit(a, true);
            bool catchUp = a.Position.AsVec2.Distance(slot) < limit * 2f;
            bool keep = catchUp && a.GetOffhandWieldedItemIndex() == EquipmentIndex.ExtraWeaponSlot
                        && form.QuerySystem.RangedUnitRatioReadOnly + form.QuerySystem.RangedCavalryUnitRatioReadOnly >= 0.5f;
            a.SetFormationIntegrityData(catchUp ? slot : Vec2.Zero, form.CurrentDirection, integrity.AverageVelocityExcludeFarAgents,
                integrity.AverageMaxUnlimitedSpeedExcludeFarAgents, integrity.DeviationOfPositionsExcludeFarAgents, keep);
        }

        public bool EnemyWithin(TrackedAgent st, double metres)
        {
            var a = st.Agent;
            var m = Mission.Current;
            if (m == null || a.Team == null) return false;
            return m.GetNearbyEnemyAgentCount(a.Team, a.Position.AsVec2, (float)metres) > 0;
        }

        /// <summary>Step 42: <c>Agent.GetTargetAgent()</c> (the native AI's own target - the man vanilla walks him at), else
        /// <c>Agent.ImmediateEnemy</c>; only an active enemy counts. Native reads - called only for a man who could step out now
        /// or is stepping out (LeashMath.NeedsEnemy), and never while the leash is 0.</summary>
        public bool TryEnemy(TrackedAgent st, out double x, out double y)
        {
            var a = st.Agent;
            var e = a.GetTargetAgent();
            if (!IsEnemy(a, e)) e = a.ImmediateEnemy;
            if (!IsEnemy(a, e))
            {
                x = y = double.NaN;
                return false;
            }
            var p = e!.Position;
            x = p.x;
            y = p.y;
            return true;
        }

        private static bool IsEnemy(Agent a, Agent? e) => e != null && e.IsActive() && a.IsEnemyOf(e);

        /// <summary>Step 63: his body frame (step 16's / courage's way).</summary>
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

        public AiInputHook.HookResult Hook(TrackedAgent st) => AiInputHook.Hook(st);

        public void ReleaseInput(TrackedAgent st)
        {
            if (st.Removed || !st.Agent.IsActive()) return;
            AiInputHook.UnhookIfIdle(st);
        }

        /// <summary>Step 68: vanilla's own call (<c>HumanAIComponent.ParallelUpdateFormationMovement</c> → <c>Agent.TrySetFormationFrame</c>) with OUR
        /// point - his world position moved there (<c>WorldPosition.SetVec2</c>, vanilla's own way in <c>MovementOrder.GetPositionAux</c>) and the
        /// game's direction for his place (<c>GetBaseFormationFrame</c>). Main thread (the pre-tick - no agent job runs then).</summary>
        public bool Pace(TrackedAgent st, double x, double y)
        {
            var a = st.Agent;
            if (st.Removed || !a.IsActive() || !a.GetBaseFormationFrame(out _, out var dir)) return false;
            var wp = a.GetWorldPosition();
            wp.SetVec2(new Vec2((float)x, (float)y));
            return a.TrySetFormationFrame(in wp, in dir);
        }

        public void Unpace(TrackedAgent st)
        {
            var a = st.Agent;
            if (st.Removed || !a.IsActive()) return;
            if (a.GetBaseFormationFrame(out var pos, out var dir)) a.TrySetFormationFrame(in pos, in dir);
            else a.SetFormationFrameDisabled();
        }
    }

    /// <summary>The wall body every new <see cref="AthleticsLogic"/> starts with - the game's; the offline smoke swaps in its
    /// stand-in once, before any logic is built (a native read on its fake agents would crash the process).</summary>
    internal static class WallBodyDefault
    {
        internal static IWallBody Current = new GameWallBody();
    }
}

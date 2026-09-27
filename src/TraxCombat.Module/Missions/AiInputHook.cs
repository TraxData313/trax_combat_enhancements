using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// One AI fighter's step-16 input hold (AI_NOTES "Step 16"): what the logic's tick wants done to his input
    /// (his attacks held - by the AI timer or a step back - and a backwards movement while he backpedals) and what the
    /// hook did. The logic WRITES the wishes in its tick; <see cref="AiInputComponent"/> READS them when the engine
    /// hands it the AI's input. Both run on the main thread (<c>Agent.OnAIInputSet</c> is an MBCallback with
    /// isMultiThreadCallable false). Allocated once per man who is ever held by input - reused, no per-tick allocation.
    /// </summary>
    internal sealed class AiInputState
    {
        // ---- the wishes (the logic's tick)
        /// <summary>The AI timer holds his attacks (AttackRatePaceByInput).</summary>
        public bool HoldAttacks;

        /// <summary>A backpedal holds his attacks (StepBackHoldAttacks).</summary>
        public bool StepHoldAttacks;

        /// <summary>He backpedals: <see cref="BackX"/>, <see cref="BackY"/> replace the AI's movement input.</summary>
        public bool Backpedal;

        /// <summary>The backwards input in his own frame (x right, y forward), refreshed every tick from his body frame.</summary>
        public float BackX, BackY = -1f;

        public bool Active => HoldAttacks || StepHoldAttacks || Backpedal;

        /// <summary>Since when the state has been active (mission time), for the calls-per-second figure.</summary>
        public double ActiveSince = -1;
        public double ActiveSeconds;

        // ---- the hook
        public AiInputComponent? Component;

        /// <summary>The engine's callback flag was on before we hooked him the first time (another mod's component).</summary>
        public bool CallbackWasOn;

        /// <summary>We turned the callback on and it is still on - ours to turn off when he is idle.</summary>
        public bool CallbackOurs;

        /// <summary>How often we turned his callback on, and off again when he was idle.</summary>
        public int TurnedOn;
        public int TurnedOff;

        // ---- what the hook did (the callback counts; the summary sums every man)
        public long Calls;
        public long AttackCleared;
        public long GuardRaised;
        public long OwnGuardKept;
        public long ReadyCancelled;
        public long BackpedalFrames;
        public long NotAi;
        public int Errors;

        /// <summary>The first exception the callback swallowed - the tick logs it through Failed (never inside the callback).</summary>
        public Exception? PendingError;

        // ---- the mission's first held man / first backpedal: frames captured in full for the log
        public bool Capture;
        public bool CapturedFirst;
        public uint FirstBefore;
        public uint FirstAfter;
        public float FirstVecX, FirstVecY, FirstWroteX, FirstWroteY;
        public bool CapturedAttack;
        public uint AttackBefore;
        public uint AttackAfter;
        public InputEdit AttackEdit;
    }

    /// <summary>
    /// The per-man <see cref="AgentComponent"/> of step 16: the engine calls <see cref="OnAIInputSet"/> with the input
    /// his AI just decided, before it is used (only while <c>Agent.SetHasOnAiInputSetCallback(true)</c>). Idle - one
    /// bool and out. Active: never the player's agent (or one the player commands - not AI-controlled); the attack bits
    /// out while held (<see cref="AiInputMath.HoldAttacks"/> - his guard, parries, moves and every event flag stay his);
    /// the backwards input while backpedalling. Added lazily by <see cref="AiInputHook.Hook"/> from the logic's tick
    /// (never inside a callback: the engine iterates the list here). Added after RTS Camera Command System's own
    /// component (it adds one to every agent at creation), so ours sees its edits and writes last. Nothing thrown to
    /// the engine: an exception is counted and logged by the next tick.
    /// </summary>
    internal sealed class AiInputComponent : AgentComponent
    {
        private readonly TrackedAgent _st;

        public AiInputComponent(Agent agent, TrackedAgent st)
            : base(agent)
        {
            _st = st;
        }

        public override void OnAIInputSet(ref Agent.EventControlFlag eventFlag, ref Agent.MovementControlFlag movementFlag, ref Vec2 inputVector)
        {
            var s = _st.Input;
            if (s == null || !s.Active) return;
            try
            {
                s.Calls++;
                var settings = TraxSettings.Shared;
                if (!settings.ModEnabled || !settings.AthleticsEnabled) return; // the master switch first (the tick clears the wishes too)
                if (Agent.IsMainAgent || !Agent.IsAIControlled)
                {
                    s.NotAi++; // the player's agent, or a soldier the player took over (RTS Camera): never touched
                    return;
                }
                uint flags = (uint)movementFlag;
                float vx = inputVector.x, vy = inputVector.y;
                AiInputHook.Apply(_st, s, settings.AiHoldRaiseGuard, ref flags, ref vx, ref vy);
                movementFlag = (Agent.MovementControlFlag)flags;
                if (s.Backpedal) inputVector = new Vec2(vx, vy);
            }
            catch (Exception e)
            {
                s.Errors++;
                s.PendingError ??= e;
            }
        }
    }

    /// <summary>
    /// Step 16's hook: attaching the component (lazily, from the tick), the engine's callback flag (on when we hook a
    /// man; off again only when he is idle AND we turned it on AND no other component on him overrides OnAIInputSet -
    /// RTS Camera's is never switched off), and <see cref="Apply"/> - the managed filter the component runs (the
    /// offline smoke drives it directly: its agents have no native side).
    /// </summary>
    internal static class AiInputHook
    {
        private const int ActReadyMelee = (int)Agent.ActionCodeType.ReadyMelee;
        private const int ActReadyRanged = (int)Agent.ActionCodeType.ReadyRanged;

        private static readonly Dictionary<Type, bool> Overrides = new Dictionary<Type, bool>();

        private static readonly Type[] InputSetArgs =
        {
            typeof(Agent.EventControlFlag).MakeByRefType(), typeof(Agent.MovementControlFlag).MakeByRefType(), typeof(Vec2).MakeByRefType(),
        };

        /// <summary>What <see cref="Hook"/> did, for the counts.</summary>
        internal enum HookResult
        {
            AlreadyHooked,
            FirstHookCallbackWasOn,
            FirstHookTurnedOn,
            TurnedOnAgain,
        }

        /// <summary>
        /// Makes sure the man has our component and the engine calls it (ENGINE - main thread, the logic's tick, never
        /// inside a callback). The state must exist (the logic creates it). Returns what it did.
        /// </summary>
        internal static HookResult Hook(TrackedAgent st)
        {
            var s = st.Input!;
            var a = st.Agent;
            bool first = s.Component == null;
            if (first)
            {
                var c = new AiInputComponent(a, st);
                a.AddComponent(c);
                s.Component = c;
            }
            if (s.CallbackOurs) return HookResult.AlreadyHooked;
            if (a.GetHasOnAiInputSetCallback())
            {
                if (first) s.CallbackWasOn = true;
                return first ? HookResult.FirstHookCallbackWasOn : HookResult.AlreadyHooked;
            }
            a.SetHasOnAiInputSetCallback(true);
            s.CallbackOurs = true;
            s.TurnedOn++;
            return first ? HookResult.FirstHookTurnedOn : HookResult.TurnedOnAgain;
        }

        /// <summary>
        /// He is idle now (no hold, no backpedal): the callback flag goes off if WE turned it on and no other component
        /// on him overrides OnAIInputSet (then it is left on and no longer counted as ours). ENGINE; never for a removed
        /// man. True when it was turned off.
        /// </summary>
        internal static bool UnhookIfIdle(TrackedAgent st)
        {
            var s = st.Input;
            if (s == null || s.Active || !s.CallbackOurs) return false;
            var a = st.Agent;
            s.CallbackOurs = false;
            if (st.Removed || !a.IsActive()) return false;
            if (OthersWantTheCallback(a, s.Component)) return false;
            a.SetHasOnAiInputSetCallback(false);
            s.TurnedOff++;
            return true;
        }

        /// <summary>Another component on him overrides OnAIInputSet (RTS Camera Command System's, or any mod's).</summary>
        private static bool OthersWantTheCallback(Agent a, AgentComponent? ours)
        {
            var list = a.Components;
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                if (c != null && !ReferenceEquals(c, ours) && OverridesInputSet(c.GetType())) return true;
            }
            return false;
        }

        /// <summary>The type (or a base below AgentComponent) overrides OnAIInputSet - cached per type (main thread).</summary>
        internal static bool OverridesInputSet(Type t)
        {
            if (Overrides.TryGetValue(t, out bool o)) return o;
            var m = t.GetMethod("OnAIInputSet", BindingFlags.Public | BindingFlags.Instance, null, InputSetArgs, null);
            o = m != null && m.DeclaringType != typeof(AgentComponent);
            Overrides[t] = o;
            return o;
        }

        /// <summary>Turns the timer's wish on or off (keeps the active time for the calls-per-second figure).</summary>
        internal static void SetHold(TrackedAgent st, bool on, double now)
        {
            var s = st.Input ??= new AiInputState();
            bool was = s.Active;
            s.HoldAttacks = on;
            Account(s, was, now);
        }

        /// <summary>Turns the backpedal on (with its attack hold) or off.</summary>
        internal static void SetBackpedal(TrackedAgent st, bool on, bool holdAttacks, double now)
        {
            var s = st.Input ??= new AiInputState();
            bool was = s.Active;
            s.Backpedal = on;
            s.StepHoldAttacks = on && holdAttacks;
            if (!on)
            {
                s.BackX = 0f;
                s.BackY = -1f;
            }
            Account(s, was, now);
        }

        /// <summary>Every wish off at once (switched off, left the field, mission end).</summary>
        internal static void Clear(TrackedAgent st, double now)
        {
            var s = st.Input;
            if (s == null) return;
            bool was = s.Active;
            s.HoldAttacks = s.StepHoldAttacks = s.Backpedal = false;
            Account(s, was, now);
        }

        private static void Account(AiInputState s, bool was, double now)
        {
            bool isNow = s.Active;
            if (!was && isNow) s.ActiveSince = now;
            else if (was && !isNow && s.ActiveSince >= 0)
            {
                if (now > s.ActiveSince) s.ActiveSeconds += now - s.ActiveSince;
                s.ActiveSince = -1;
            }
        }

        /// <summary>
        /// One frame of an ACTIVE man's input (the component's work, managed only): his attacks held → the attack bits
        /// out (a guard raised when he wanted to attack and <paramref name="raiseGuard"/>, or when he is in a ready -
        /// cancel, never release); backpedalling → his movement input replaced by the backwards vector. Counted; the
        /// mission's first held man / first backpedal captured in full.
        /// </summary>
        internal static void Apply(TrackedAgent st, AiInputState s, bool raiseGuard, ref uint flags, ref float vx, ref float vy)
        {
            uint before = flags;
            float bx = vx, by = vy;
            var edit = InputEdit.None;
            if (s.HoldAttacks || s.StepHoldAttacks)
            {
                int action = st.PrevAction;
                bool inReady = action == ActReadyMelee || action == ActReadyRanged;
                flags = AiInputMath.HoldAttacks(flags, raiseGuard, inReady, out edit);
                if ((edit & InputEdit.AttackCleared) != 0)
                {
                    s.AttackCleared++;
                    if ((edit & InputEdit.GuardRaised) != 0) s.GuardRaised++;
                    if ((edit & InputEdit.OwnGuardKept) != 0) s.OwnGuardKept++;
                    if ((edit & InputEdit.ReadyCancelled) != 0) s.ReadyCancelled++;
                }
            }
            if (s.Backpedal)
            {
                flags = AiInputMath.Backpedal(flags); // his own move bits out: only the backwards vector moves him
                vx = s.BackX;
                vy = s.BackY;
                s.BackpedalFrames++;
            }
            if (s.Capture && !s.CapturedAttack && (edit & InputEdit.AttackCleared) != 0)
            {
                s.CapturedAttack = true;
                s.AttackBefore = before;
                s.AttackAfter = flags;
                s.AttackEdit = edit;
            }
            if (s.Capture && !s.CapturedFirst)
            {
                s.CapturedFirst = true;
                s.FirstBefore = before;
                s.FirstAfter = flags;
                s.FirstVecX = bx;
                s.FirstVecY = by;
                s.FirstWroteX = vx;
                s.FirstWroteY = vy;
            }
        }

        /// <summary>Movement flags as the log shows them: "AttackDown|DefendDown" (none = "none").</summary>
        internal static string FlagNames(uint flags)
        {
            if (flags == 0) return "none";
            var parts = new List<string>();
            for (int bit = 0; bit < 17; bit++)
            {
                uint v = 1u << bit;
                if ((flags & v) != 0) parts.Add(Enum.GetName(typeof(Agent.MovementControlFlag), (Agent.MovementControlFlag)v) ?? ("0x" + v.ToString("X")));
            }
            return string.Join("|", parts);
        }
    }
}

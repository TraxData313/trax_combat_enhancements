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

        /// <summary>Step 23: he braces - his MELEE attacks held (a ranged weapon in hand is let through), the shield kept up.</summary>
        public bool BraceHoldAttacks;

        /// <summary>Step 23, refreshed by the brace poll: a bow, crossbow or throwing weapon in his hand (let through).</summary>
        public bool BraceRanged;

        /// <summary>Step 23, refreshed by the brace poll: his shield is in his other hand (BraceRaiseShield keeps it up).</summary>
        public bool BraceShieldInHand;

        /// <summary>Step 32: while the timer holds him for the swing stack (KicksUseSwings), his kick is taken out too.</summary>
        public bool HoldKicks;

        /// <summary>Step 32: input frames whose kick the hook took out (the summary sums them).</summary>
        public long KicksCleared;

        /// <summary>Step 49: a timid man walks back to his group: <see cref="RegroupX"/>, <see cref="RegroupY"/> replace the AI's movement
        /// input (in his own frame, refreshed every tick). A step back's backpedal wins while it runs (it is short and faces the enemy too).
        /// Works with the Athletics system off (courage does not need it).</summary>
        public bool Regroup;
        public float RegroupX, RegroupY = -1f;
        public long RegroupFrames;

        /// <summary>Step 63: a shield-wall man out of place is walked back to his slot: <see cref="WallPullX"/>, <see cref="WallPullY"/>
        /// replace the AI's movement input (his own frame, refreshed at each 0.1 s look). A step back's backpedal wins (walls never step back -
        /// belt and braces). Works with the Athletics system off (the wall hold does not need it).</summary>
        public bool WallPull;
        public float WallPullX, WallPullY = -1f;
        public long WallPullFrames;

        /// <summary>Step 67: a fearful man walks to (or stands at) his place in the FEAR FLOCK: <see cref="FlockX"/>, <see cref="FlockY"/> replace
        /// the AI's movement input (his own frame; 0, 0 = he stands where he is). Courage's walk back, the wall's pull and a step back's backpedal
        /// win while they run. Works with the Athletics system off.</summary>
        public bool Flock;
        public float FlockX, FlockY;
        public long FlockFrames;

        /// <summary>Step 74: a man outside his block (round the flank, ahead of its front, behind its rear) is walked back into it: <see cref="BlockX"/>,
        /// <see cref="BlockY"/> replace the AI's movement input (his own frame). The LAST of the movement wishes: a step back's backpedal, the wall's
        /// pull, courage's walk back and the fear flock win while they run. Works with the Athletics system off.</summary>
        public bool Block;
        public float BlockX, BlockY;
        public long BlockFrames;

        /// <summary>Step 65: an AI kick / bash attempt's phase (Core <see cref="AiKickPhase"/> - None = none) and kind: the hook writes the kick
        /// key / the guard / the commit (Core <see cref="AiKickMath.Frame"/>) - only with Athletics on (the logic ends attempts otherwise).</summary>
        public AiKickPhase KickPhase;
        public AiKickKind KickKind;
        public long KickFrames, KickPresses, KickHeldNoSwing;

        /// <summary>Step 76: THE WIELD FALLBACK (SpearWieldOverride, off by default) - while his polearm is favoured, the AI's own Wield event for a
        /// sidearm slot (<see cref="SpearSidearmMask"/>) becomes the Wield of <see cref="SpearPolearmSlot"/> (Core <see cref="SpearMath.WieldRewrite"/>).
        /// Works with the Athletics system off.</summary>
        public bool SpearWield;
        public int SpearPolearmSlot = -1, SpearSidearmMask;
        public long SpearRewrites;

        /// <summary>Any wish at all - a man is held while ANY of them wants him held (one ending never lifts another).</summary>
        public bool Active => HoldAttacks || StepHoldAttacks || Backpedal || BraceHoldAttacks || Regroup || WallPull || Flock || Block || SpearWield || KickPhase != AiKickPhase.None;

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

        // ---- step 23: what the hook did while he braced (summed into the brace summary)
        public long BraceCleared;
        public long BraceGuardRaised;
        public long BraceShieldRaised;
        public long BraceRangedPassed;

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
                if (!settings.ModEnabled) return; // the master switch first (the tick clears the wishes too)
                if (s.SpearWield && !Agent.IsMainAgent && Agent.IsAIControlled) // step 76: the wield fallback (works without Athletics)
                {
                    uint ev = (uint)eventFlag;
                    if (AiInputHook.ApplySpearWield(s, ref ev)) eventFlag = (Agent.EventControlFlag)ev;
                }
                if (!settings.AthleticsEnabled && !(s.Regroup && settings.CourageEnabled) && !(s.WallPull && settings.ShieldWallHoldsSlots) && !(s.Flock && settings.FearFlockEnabled) && !(s.Block && AiInputHook.BlockOn(settings))) return; // steps 49 / 63 / 67 / 74: courage, the wall, the flock and the block work without Athletics
                if (Agent.IsMainAgent || !Agent.IsAIControlled)
                {
                    s.NotAi++; // the player's agent, or a soldier the player took over (RTS Camera): never touched
                    return;
                }
                if (!settings.AthleticsEnabled)
                {
                    uint only = (uint)movementFlag;
                    float ox = inputVector.x, oy = inputVector.y;
                    if (AiInputHook.ApplyWallPull(s, ref only, ref ox, ref oy) || AiInputHook.ApplyRegroup(s, ref only, ref ox, ref oy) || AiInputHook.ApplyFlock(s, ref only, ref ox, ref oy)
                        || AiInputHook.ApplyBlock(s, ref only, ref ox, ref oy))
                    {
                        movementFlag = (Agent.MovementControlFlag)only;
                        inputVector = new Vec2(ox, oy);
                    }
                    return;
                }
                uint flags = (uint)movementFlag;
                if (s.HoldKicks && s.HoldAttacks)
                {
                    uint ev = (uint)eventFlag;
                    if (AiInputHook.HoldKick(s, ref ev)) eventFlag = (Agent.EventControlFlag)ev;
                }
                float vx = inputVector.x, vy = inputVector.y;
                AiInputHook.Apply(_st, s, settings.AiHoldRaiseGuard, ref flags, ref vx, ref vy, settings.BraceRaiseShield);
                bool moved = s.Backpedal || (s.Regroup && settings.CourageEnabled) || (s.WallPull && settings.ShieldWallHoldsSlots) || (s.Flock && settings.FearFlockEnabled) || (s.Block && AiInputHook.BlockOn(settings));
                if (s.KickPhase != AiKickPhase.None && settings.AiKicksEnabled) // step 65: the kick / bash written LAST (after a held man's kick-out)
                {
                    uint ev = (uint)eventFlag;
                    if (AiInputHook.ApplyKick(s, ref ev, ref flags, ref vx, ref vy)) moved = true;
                    eventFlag = (Agent.EventControlFlag)ev;
                }
                movementFlag = (Agent.MovementControlFlag)flags;
                if (moved) inputVector = new Vec2(vx, vy);
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
        private const int ActReleaseRanged = (int)Agent.ActionCodeType.ReleaseRanged;
        private const int ActReleaseThrowing = (int)Agent.ActionCodeType.ReleaseThrowing;
        private const int ActReload = (int)Agent.ActionCodeType.Reload;

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
            if (!on) s.HoldKicks = false;
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

        /// <summary>Step 49: turns the regroup walk's wish on or off (the other wishes are left as they are).</summary>
        internal static void SetRegroup(TrackedAgent st, bool on, double now)
        {
            var s = st.Input ??= new AiInputState();
            bool was = s.Active;
            s.Regroup = on;
            if (!on)
            {
                s.RegroupX = 0f;
                s.RegroupY = -1f;
            }
            Account(s, was, now);
        }

        /// <summary>Step 49: a regrouping man's frame - his own move bits out, the walk's vector in (never while a step back's backpedal runs;
        /// never with CourageEnabled off). True when it wrote. Managed - the smoke drives it.</summary>
        internal static bool ApplyRegroup(AiInputState s, ref uint flags, ref float vx, ref float vy)
        {
            if (!s.Regroup || s.Backpedal || !TraxSettings.Shared.CourageEnabled) return false;
            flags = AiInputMath.Backpedal(flags);
            vx = s.RegroupX;
            vy = s.RegroupY;
            s.RegroupFrames++;
            return true;
        }

        /// <summary>Step 63: turns the wall's walk-back wish on or off (the other wishes are left as they are).</summary>
        internal static void SetWallPull(TrackedAgent st, bool on, double now)
        {
            var s = st.Input ??= new AiInputState();
            bool was = s.Active;
            s.WallPull = on;
            if (!on)
            {
                s.WallPullX = 0f;
                s.WallPullY = -1f;
            }
            Account(s, was, now);
        }

        /// <summary>Step 63: a wall man's walk back - his own move bits out, the vector in (never while a step back's backpedal runs; never with
        /// the master switch or ShieldWallHoldsSlots off). True when it wrote. Managed - the smoke drives it.</summary>
        internal static bool ApplyWallPull(AiInputState s, ref uint flags, ref float vx, ref float vy)
        {
            if (!s.WallPull || s.Backpedal) return false;
            var settings = TraxSettings.Shared;
            if (!settings.ModEnabled || !settings.ShieldWallHoldsSlots) return false;
            flags = AiInputMath.Backpedal(flags);
            vx = s.WallPullX;
            vy = s.WallPullY;
            s.WallPullFrames++;
            return true;
        }

        /// <summary>Step 67: turns the fear flock's wish on or off (the other wishes are left as they are).</summary>
        internal static void SetFlock(TrackedAgent st, bool on, double now)
        {
            var s = st.Input ??= new AiInputState();
            bool was = s.Active;
            s.Flock = on;
            if (!on)
            {
                s.FlockX = 0f;
                s.FlockY = 0f;
            }
            Account(s, was, now);
        }

        /// <summary>Step 67: a flocking man's frame - his own move bits out, the flock's vector in (0, 0 = he stands); never while a step back's
        /// backpedal, the wall's pull or courage's walk back runs (they win), never with the master switch or FearFlockEnabled off. True when it
        /// wrote. Managed - the smoke drives it.</summary>
        internal static bool ApplyFlock(AiInputState s, ref uint flags, ref float vx, ref float vy)
        {
            if (!s.Flock || s.Backpedal || s.WallPull) return false;
            var settings = TraxSettings.Shared;
            if (!settings.ModEnabled || !settings.FearFlockEnabled) return false;
            if (s.Regroup && settings.CourageEnabled) return false;
            flags = AiInputMath.Backpedal(flags);
            vx = s.FlockX;
            vy = s.FlockY;
            s.FlockFrames++;
            return true;
        }

        /// <summary>Step 74: the block hold's switches - the master switch FIRST, step 73's system, the hold's own.</summary>
        internal static bool BlockOn(TraxSettings settings) => settings.ModEnabled && settings.LineDisciplineEnabled && settings.LineBlockHoldEnabled;

        /// <summary>Step 74: turns the block hold's wish on or off (the other wishes are left as they are).</summary>
        internal static void SetBlock(TrackedAgent st, bool on, double now)
        {
            var s = st.Input ??= new AiInputState();
            bool was = s.Active;
            s.Block = on;
            if (!on)
            {
                s.BlockX = 0f;
                s.BlockY = 0f;
            }
            Account(s, was, now);
        }

        /// <summary>Step 74: a man walked back into his block - his own move bits out, the vector in; never while a step back's backpedal, the
        /// wall's pull, courage's walk back or the fear flock runs (they win), never with the block hold's switches off. True when it wrote.
        /// Managed - the smoke drives it.</summary>
        internal static bool ApplyBlock(AiInputState s, ref uint flags, ref float vx, ref float vy)
        {
            if (!s.Block || s.Backpedal || s.WallPull) return false;
            var settings = TraxSettings.Shared;
            if (!BlockOn(settings)) return false;
            if (s.Regroup && settings.CourageEnabled) return false;
            if (s.Flock && settings.FearFlockEnabled) return false;
            flags = AiInputMath.Backpedal(flags);
            vx = s.BlockX;
            vy = s.BlockY;
            s.BlockFrames++;
            return true;
        }

        /// <summary>Step 76: turns the wield fallback's wish on (with his polearm's slot and his sidearms) or off.</summary>
        internal static void SetSpearWield(TrackedAgent st, bool on, int polearmSlot, int sidearmMask, double now)
        {
            var s = st.Input ??= new AiInputState();
            bool was = s.Active;
            s.SpearWield = on;
            s.SpearPolearmSlot = on ? polearmSlot : -1;
            s.SpearSidearmMask = on ? sidearmMask : 0;
            Account(s, was, now);
        }

        /// <summary>Step 76: one frame of the wield fallback (managed - the smoke drives it): never with the master switch, the preference or its own
        /// switch off. True when a sidearm wield was turned into the polearm's.</summary>
        internal static bool ApplySpearWield(AiInputState s, ref uint eventFlags)
        {
            if (!s.SpearWield) return false;
            var settings = TraxSettings.Shared;
            if (!settings.ModEnabled || !settings.SpearPreferenceEnabled || !settings.SpearWieldOverride) return false;
            if (!SpearMath.WieldRewrite(ref eventFlags, s.SpearPolearmSlot, s.SpearSidearmMask)) return false;
            s.SpearRewrites++;
            return true;
        }

        /// <summary>The engine's Wield0 event bit (the smoke checks Core's against it).</summary>
        internal const uint Wield0EventBit = (uint)Agent.EventControlFlag.Wield0;

        /// <summary>Step 23: turns the brace's wish on or off (the timer's and the step back's are left as they are).</summary>
        internal static void SetBrace(TrackedAgent st, bool on, double now)
        {
            var s = st.Input ??= new AiInputState();
            bool was = s.Active;
            s.BraceHoldAttacks = on;
            if (!on) s.BraceRanged = s.BraceShieldInHand = false;
            Account(s, was, now);
        }

        /// <summary>Step 65: turns an AI kick attempt's wish to <paramref name="phase"/> (None = off; the other wishes are left as they are).</summary>
        internal static void SetKick(TrackedAgent st, AiKickPhase phase, AiKickKind kind, double now)
        {
            var s = st.Input ??= new AiInputState();
            bool was = s.Active;
            s.KickPhase = phase;
            s.KickKind = phase == AiKickPhase.None ? AiKickKind.None : kind;
            Account(s, was, now);
        }

        /// <summary>
        /// Step 65: one frame of a man's kick attempt (managed - the smoke drives it): <see cref="AiKickMath.Frame"/> - his own attack / guard
        /// bits and event flags out, the guard up for a bash, the kick key in the Press phase (NOT while step 32 holds his kick - no stored
        /// swing; counted), standing still in the Commit (his movement left alone while a backpedal / walk back moves him). True = the movement
        /// vector was rewritten.
        /// </summary>
        internal static bool ApplyKick(AiInputState s, ref uint eventFlags, ref uint flags, ref float vx, ref float vy)
        {
            if (s.KickPhase == AiKickPhase.None) return false;
            uint ev = eventFlags;
            flags = AiKickMath.Frame(s.KickPhase, s.KickKind, flags, ref ev, out bool stop);
            if ((ev & AiKickMath.KickEventBit) != 0)
            {
                if (s.HoldKicks && s.HoldAttacks)
                {
                    ev &= ~AiKickMath.KickEventBit;
                    s.KickHeldNoSwing++;
                }
                else s.KickPresses++;
            }
            eventFlags = ev;
            s.KickFrames++;
            if (!stop || s.Backpedal || s.Regroup || s.WallPull || s.Flock || s.Block) return false;
            flags = AiInputMath.Backpedal(flags); // his own move bits out
            vx = 0f;
            vy = 0f;
            return true;
        }

        /// <summary>The engine's kick event bit (<c>Agent.EventControlFlag.Kick</c>; the smoke checks it).</summary>
        internal const uint KickEventBit = (uint)Agent.EventControlFlag.Kick;

        /// <summary>Step 32: a held man's kick taken out of his event flags while he waits for a stored swing (managed - the smoke
        /// drives it). True when it was there.</summary>
        internal static bool HoldKick(AiInputState s, ref uint eventFlags)
        {
            if ((eventFlags & KickEventBit) == 0) return false;
            eventFlags &= ~KickEventBit;
            s.KicksCleared++;
            return true;
        }

        /// <summary>Every wish off at once (switched off, left the field, mission end).</summary>
        internal static void Clear(TrackedAgent st, double now)
        {
            var s = st.Input;
            if (s == null) return;
            bool was = s.Active;
            s.HoldAttacks = s.StepHoldAttacks = s.Backpedal = s.BraceHoldAttacks = s.HoldKicks = s.Regroup = s.Flock = s.Block = s.SpearWield = false;
            s.KickPhase = AiKickPhase.None; // step 65
            s.KickKind = AiKickKind.None;
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
        internal static void Apply(TrackedAgent st, AiInputState s, bool raiseGuard, ref uint flags, ref float vx, ref float vy, bool raiseShield = false)
        {
            uint before = flags;
            float bx = vx, by = vy;
            var edit = InputEdit.None;
            int action = st.PrevAction;
            // step 23: the brace holds MELEE only - a ranged weapon in hand (the poll's read) or a ranged action under way goes on
            bool braceMelee = false;
            if (s.BraceHoldAttacks)
            {
                bool ranged = s.BraceRanged || action == ActReadyRanged || action == ActReleaseRanged || action == ActReleaseThrowing || action == ActReload;
                if (!ranged) braceMelee = true;
                else if ((flags & AiInputMath.AttackMask) != 0) s.BraceRangedPassed++;
            }
            if (s.HoldAttacks || s.StepHoldAttacks || braceMelee)
            {
                bool inReady = action == ActReadyMelee || action == ActReadyRanged;
                flags = AiInputMath.HoldAttacks(flags, raiseGuard, inReady, out edit);
                if ((edit & InputEdit.AttackCleared) != 0)
                {
                    s.AttackCleared++;
                    if ((edit & InputEdit.GuardRaised) != 0) s.GuardRaised++;
                    if ((edit & InputEdit.OwnGuardKept) != 0) s.OwnGuardKept++;
                    if ((edit & InputEdit.ReadyCancelled) != 0) s.ReadyCancelled++;
                    if (braceMelee)
                    {
                        s.BraceCleared++;
                        if ((edit & (InputEdit.GuardRaised | InputEdit.ReadyCancelled)) != 0) s.BraceGuardRaised++;
                    }
                }
            }
            if (braceMelee && raiseShield && s.BraceShieldInHand)
            {
                flags = AiInputMath.RaiseShield(flags, out bool raised);
                if (raised) s.BraceShieldRaised++;
            }
            if (s.Backpedal)
            {
                flags = AiInputMath.Backpedal(flags); // his own move bits out: only the backwards vector moves him
                vx = s.BackX;
                vy = s.BackY;
                s.BackpedalFrames++;
            }
            else if (!ApplyWallPull(s, ref flags, ref vx, ref vy)) // step 63: a wall man out of place walked back
            {
                if (!ApplyRegroup(s, ref flags, ref vx, ref vy)) // step 49: the walk back (a step back's backpedal wins while it runs)
                {
                    if (!ApplyFlock(s, ref flags, ref vx, ref vy)) // step 67: the fear flock - courage's walk back goes first
                        ApplyBlock(s, ref flags, ref vx, ref vy); // step 74: the block hold - the last
                }
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

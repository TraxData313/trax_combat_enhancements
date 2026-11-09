using System;
using System.Collections.Generic;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 100d (AI_NOTES "Step 100" §4, "100d - built"): every GAME call of the timed riposte's two animations - the blocker's push-out on his
    /// channel 1 and the attacker's direction-matched "blocked" recoil - and of the test tool (its key, the nearest man). Action names are
    /// resolved once each (<c>ActionIndexCache.Create</c>) and checked in the man's own action set once per (set, action)
    /// (<c>MBActionSet.CheckActionAnimationClipExists</c> + its length). Main thread (the tick). The offline smoke plays a stand-in (a native
    /// call on a fake agent would crash).
    /// </summary>
    internal interface IAnimBody
    {
        /// <summary>His wielded main-hand weapon's item usage id ("" = nothing in hand).</summary>
        string UsageOf(TrackedAgent st);

        /// <summary>A shield in his off hand.</summary>
        bool ShieldInHand(TrackedAgent st);

        bool LeftStance(TrackedAgent st);

        bool Mounted(TrackedAgent st);

        /// <summary>His upper body is in a defend stage (a guard / a parry reaction) - not attacking or reloading.</summary>
        bool Guarding(TrackedAgent st);

        /// <summary>Channel <paramref name="channel"/>'s current action index (-1 none).</summary>
        int CurrentIndex(TrackedAgent st, int channel);

        /// <summary>Channel <paramref name="channel"/>'s current action name (null none; the name read once per index).</summary>
        string? CurrentName(TrackedAgent st, int channel);

        /// <summary>The named action exists in his action set: its index and its animation's length (s; NaN unknown).</summary>
        bool Find(TrackedAgent st, string name, out int index, out double seconds);

        /// <summary>Plays action <paramref name="index"/> on <paramref name="channel"/> over the engine's priority (true = taken).</summary>
        bool Play(TrackedAgent st, int channel, int index, float speed, float startProgress, float blendIn, float blendOut);

        /// <summary>The action now on <paramref name="channel"/> at <paramref name="speed"/>.</summary>
        void SetSpeed(TrackedAgent st, int channel, float speed);

        /// <summary>Channel 1 cleared (act_none over the priority) - his own input takes the upper body back.</summary>
        bool ClearUpper(TrackedAgent st);

        /// <summary>The test tool: the key with that name went down this frame (and whether Left Shift is held).</summary>
        bool KeyPressed(string keyName, out bool shift);

        /// <summary>The test tool: the nearest active human (not <paramref name="you"/>) - on foot or riding - within <paramref name="metres"/>;
        /// none = the nearest RIDER within <paramref name="riderMetres"/> (step 104a: riders come and go fast); or null.</summary>
        Agent? Nearest(Agent you, float metres, float riderMetres);

        /// <summary>The test tool's on-screen line.</summary>
        void Say(string text);
    }

    internal sealed class GameAnimBody : IAnimBody
    {
        private readonly Dictionary<string, ActionIndexCache> _actions = new Dictionary<string, ActionIndexCache>(StringComparer.Ordinal);
        private readonly Dictionary<long, (bool Exists, double Seconds)> _clips = new Dictionary<long, (bool, double)>();
        private readonly Dictionary<int, string> _names = new Dictionary<int, string>();
        private readonly Dictionary<int, ActionIndexCache> _byIndex = new Dictionary<int, ActionIndexCache>();
        private readonly MBList<Agent> _near = new MBList<Agent>();

        public string UsageOf(TrackedAgent st)
        {
            var w = st.Agent.WieldedWeapon;
            if (w.IsEmpty) return string.Empty;
            return w.CurrentUsageItem?.ItemUsage ?? string.Empty;
        }

        public bool ShieldInHand(TrackedAgent st)
        {
            var a = st.Agent;
            var i = a.GetOffhandWieldedItemIndex();
            if (i < TaleWorlds.Core.EquipmentIndex.WeaponItemBeginSlot || i >= TaleWorlds.Core.EquipmentIndex.NumAllWeaponSlots) return false;
            var w = a.Equipment[i];
            return !w.IsEmpty && w.CurrentUsageItem != null && w.CurrentUsageItem.IsShield;
        }

        public bool LeftStance(TrackedAgent st) => st.Agent.GetIsLeftStance();

        public bool Mounted(TrackedAgent st) => st.Agent.MountAgent != null;

        public bool Guarding(TrackedAgent st)
        {
            var stage = st.Agent.GetCurrentActionStage(1);
            return stage == Agent.ActionStage.Defend || stage == Agent.ActionStage.DefendParry;
        }

        public int CurrentIndex(TrackedAgent st, int channel) => st.Agent.GetCurrentAction(channel).Index;

        public string? CurrentName(TrackedAgent st, int channel)
        {
            var act = st.Agent.GetCurrentAction(channel);
            if (act.Index < 0) return null;
            if (!_names.TryGetValue(act.Index, out var name))
            {
                name = act.GetName();
                _names[act.Index] = name;
            }
            return name;
        }

        public bool Find(TrackedAgent st, string name, out int index, out double seconds)
        {
            index = -1;
            seconds = double.NaN;
            if (string.IsNullOrEmpty(name)) return false;
            if (!_actions.TryGetValue(name, out var act))
            {
                act = ActionIndexCache.Create(name);
                _actions[name] = act;
                if (act.Index >= 0) _byIndex[act.Index] = act;
            }
            if (act.Index < 0) return false;
            var set = st.Agent.ActionSet;
            long key = ((long)set.GetHashCode() << 32) | (uint)act.Index;
            if (!_clips.TryGetValue(key, out var clip))
            {
                bool exists = MBActionSet.CheckActionAnimationClipExists(set, in act);
                double len = exists ? MBActionSet.GetActionAnimationDuration(set, in act) : double.NaN;
                clip = (exists, len);
                _clips[key] = clip;
            }
            if (!clip.Exists) return false;
            index = act.Index;
            seconds = clip.Seconds;
            return true;
        }

        public bool Play(TrackedAgent st, int channel, int index, float speed, float startProgress, float blendIn, float blendOut)
        {
            if (index < 0) return false;
            var act = ActionOf(index);
            return st.Agent.SetActionChannel(channel, in act, ignorePriority: true, (AnimFlags)0uL, 0f, speed, blendIn, blendOut, startProgress);
        }

        public void SetSpeed(TrackedAgent st, int channel, float speed) => st.Agent.SetCurrentActionSpeed(channel, speed);

        public bool ClearUpper(TrackedAgent st) => st.Agent.SetActionChannel(1, in ActionIndexCache.act_none, ignorePriority: true, (AnimFlags)0uL, 0f, 1f, 0.1f);

        public bool KeyPressed(string keyName, out bool shift)
        {
            shift = false;
            if (!Enum.TryParse(keyName, false, out InputKey key) || key == InputKey.Invalid) return false;
            if (!Input.IsKeyPressed(key)) return false;
            shift = Input.IsKeyDown(InputKey.LeftShift);
            return true;
        }

        public Agent? Nearest(Agent you, float metres, float riderMetres)
        {
            var m = Mission.Current;
            if (m == null || you == null) return null;
            _near.Clear();
            m.GetNearbyAgents(you.Position.AsVec2, Math.Max(metres, riderMetres), _near);
            Agent? best = null, bestRider = null;
            float bestD = float.MaxValue, bestRiderD = float.MaxValue;
            float near2 = metres * metres;
            foreach (var a in _near)
            {
                if (a == you || !a.IsActive() || !a.IsHuman) continue;
                float d = a.Position.DistanceSquared(you.Position);
                if (d <= near2 && d < bestD)
                {
                    bestD = d;
                    best = a;
                }
                if (a.MountAgent != null && d < bestRiderD)
                {
                    bestRiderD = d;
                    bestRider = a;
                }
            }
            _near.Clear();
            return best ?? bestRider;
        }

        public void Say(string text) => InformationManager.DisplayMessage(new InformationMessage(text, Color.ConvertStringToColor("#9FD8E8FF")));

        private ActionIndexCache ActionOf(int index) => _byIndex.TryGetValue(index, out var act) ? act : ActionIndexCache.act_none;
    }

    /// <summary>Step 100d: the body every new logic starts with (the smoke swaps in its stand-in once).</summary>
    internal static class AnimBodyDefault
    {
        internal static IAnimBody Current = new GameAnimBody();
    }
}

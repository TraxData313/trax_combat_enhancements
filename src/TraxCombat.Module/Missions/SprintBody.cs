using System;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;
using TraxCombat.Hud;

namespace TraxCombat.Missions
{
    /// <summary>Step 33: one formation's order as the sprint's poll reads it - its slot (team index × formations per team +
    /// its index) and its kind (step 23's read).</summary>
    internal struct SprintFormationRead
    {
        public int Slot;
        public BraceOrderKind Kind;
    }

    /// <summary>
    /// Every GAME read of the sprint behind one seam (the step-5d / 23 pattern): the logic keeps the store, the profile, every end
    /// path and the stats; this touches the engine. The real one is <see cref="GameSprintBody"/>; the offline smoke plays a
    /// stand-in (its agents have no native side). The speed itself needs no engine call here: the stat decorator multiplies the
    /// run property on the recompute the logic already asks for.
    /// </summary>
    internal interface ISprintBody
    {
        /// <summary>He is on the field.</summary>
        bool IsActive(TrackedAgent st);

        /// <summary>His own AI drives him.</summary>
        bool IsAiControlled(TrackedAgent st);

        /// <summary>He rides a horse.</summary>
        bool IsMounted(TrackedAgent st);

        /// <summary>His formation's slot (as in <see cref="ReadOrders"/>), −1 with none.</summary>
        int FormationSlot(TrackedAgent st);

        /// <summary>Every formation with men in it, every team: its slot and its order kind now. Returns how many were written.</summary>
        int ReadOrders(SprintFormationRead[] into);

        /// <summary>His speed now and his top speed (m/s).</summary>
        void ReadMotion(TrackedAgent st, out float speed, out float topSpeed);

        /// <summary>An enemy within <paramref name="metres"/> of him.</summary>
        bool EnemyWithin(TrackedAgent st, float metres);

        /// <summary>The key <paramref name="keyIndex"/> (<see cref="KeyNames"/>) is held - through the scene's input context, which
        /// answers false while a menu holds the keys.</summary>
        bool KeyDown(int keyIndex);

        /// <summary>The player may sprint now: a fight mode (battle, duel, tournament, stealth), the order menu closed.</summary>
        bool PlayerCanSprint();
    }

    /// <summary>
    /// The engine side (AI_NOTES "Step 33", RESEARCH §N). The order from step 23's <see cref="GameBraceBody.KindOf"/>; the formations
    /// from <c>Mission.Teams</c> → <c>FormationsIncludingEmpty</c> (index loops - no enumerator); motion as the regen pass reads it;
    /// "in melee" = <c>Mission.GetNearbyEnemyAgentCount</c> (native, the proximity map, no allocation); the key =
    /// <c>Mission.InputManager.IsKeyDown(InputKey)</c>. Main thread (the logic's tick).
    /// </summary>
    internal sealed class GameSprintBody : ISprintBody
    {
        /// <summary>Formation slots per team (the game's formation classes).</summary>
        internal const int SlotsPerTeam = (int)FormationClass.NumberOfAllFormations;

        /// <summary>Teams whose formations are polled (as the formation stats - step 20; plumbing).</summary>
        internal const int MaxTeams = 8;

        /// <summary><see cref="KeyNames.All"/> as the game's keys (parsed once; a name the game does not know = Invalid, never down).</summary>
        private static readonly InputKey[] Keys = BuildKeys();

        private static InputKey[] BuildKeys()
        {
            var keys = new InputKey[KeyNames.All.Length];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = Enum.TryParse(KeyNames.All[i], false, out InputKey k) ? k : InputKey.Invalid;
            return keys;
        }

        /// <summary>The game key behind a <see cref="KeyNames"/> index (the smoke checks every name).</summary>
        internal static InputKey KeyFor(int keyIndex) => keyIndex >= 0 && keyIndex < Keys.Length ? Keys[keyIndex] : InputKey.Invalid;

        public bool IsActive(TrackedAgent st) => st.Agent.IsActive();

        public bool IsAiControlled(TrackedAgent st) => st.Agent.IsAIControlled;

        public bool IsMounted(TrackedAgent st) => st.Agent.MountAgent != null;

        public int FormationSlot(TrackedAgent st)
        {
            var a = st.Agent;
            var f = a.Formation;
            return f == null ? -1 : SlotOf(a.Team, (int)f.FormationIndex);
        }

        internal static int SlotOf(Team? team, int formationIndex)
        {
            if (team == null || formationIndex < 0 || formationIndex >= SlotsPerTeam) return -1;
            int t = team.TeamIndex;
            return t >= 0 && t < MaxTeams ? t * SlotsPerTeam + formationIndex : -1;
        }

        public int ReadOrders(SprintFormationRead[] into)
        {
            var m = Mission.Current;
            if (m == null) return 0;
            var teams = m.Teams;
            int n = 0;
            for (int t = 0; t < teams.Count; t++)
            {
                var team = teams[t];
                if (team == null || team.TeamIndex < 0 || team.TeamIndex >= MaxTeams) continue;
                var list = team.FormationsIncludingEmpty;
                if (list == null) continue;
                for (int i = 0; i < list.Count && n < into.Length; i++)
                {
                    var f = list[i];
                    if (f == null || f.CountOfUnits <= 0) continue;
                    int slot = SlotOf(team, (int)f.FormationIndex);
                    if (slot < 0) continue;
                    into[n].Slot = slot;
                    into[n].Kind = GameBraceBody.KindOf(f);
                    n++;
                }
            }
            return n;
        }

        public void ReadMotion(TrackedAgent st, out float speed, out float topSpeed)
        {
            var a = st.Agent;
            speed = a.MovementVelocity.Length;
            topSpeed = a.GetMaximumForwardUnlimitedSpeed();
        }

        public bool EnemyWithin(TrackedAgent st, float metres)
        {
            var a = st.Agent;
            var m = Mission.Current;
            if (m == null || a.Team == null) return false;
            return m.GetNearbyEnemyAgentCount(a.Team, a.Position.AsVec2, metres) > 0;
        }

        public bool KeyDown(int keyIndex)
        {
            var key = KeyFor(keyIndex);
            if (key == InputKey.Invalid) return false;
            var input = Mission.Current?.InputManager;
            return input != null && input.IsKeyDown(key);
        }

        public bool PlayerCanSprint()
        {
            var m = Mission.Current;
            return m != null && HudFrame.IsFightMode((int)m.Mode) && !m.IsOrderMenuOpen;
        }
    }

    /// <summary>The sprint body every new <see cref="AthleticsLogic"/> starts with - the game's; the offline smoke swaps in its
    /// stand-in once, before any logic is built (a native read on its fake agents would crash the process).</summary>
    internal static class SprintBodyDefault
    {
        internal static ISprintBody Current = new GameSprintBody();
    }
}

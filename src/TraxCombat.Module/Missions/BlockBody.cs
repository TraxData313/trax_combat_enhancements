using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 74: the block hold's GAME reads behind one seam (the step-49 pattern; the offline smoke plays a stand-in): the step-73 cap of his
    /// formation and, for the diagnostic, its movement order, active behaviour, arrangement and the game's own width, its name. Everything else
    /// (his position, his frame, the hook, his slot) is courage's / the fear flock's.
    /// </summary>
    internal interface IBlockBody
    {
        /// <summary>The cap step 73 holds his formation to (<paramref name="lines"/> = the logic's line discipline): D, the cap's files and width (m),
        /// its men, its side, whether the AI orders it. False = not counted.</summary>
        bool Cap(TrackedAgent st, LineDiscipline? lines, out double depth, out int capFiles, out double capWidth, out int men, out bool playerSide, out bool ai);

        /// <summary>His formation's movement order, active behaviour (its type's name, "-" = none) and arrangement, and the game's own width (m).</summary>
        void Formation(TrackedAgent st, out string order, out string behaviour, out string arrangement, out double gameWidth);

        /// <summary>His formation for the log: "the enemy's Infantry".</summary>
        string Name(TrackedAgent st);
    }

    /// <summary>The engine side (AI_NOTES "Step 74"): managed reads of his formation - <c>GetReadonlyMovementOrderReference().OrderEnum</c>,
    /// <c>AI.ActiveBehavior</c> (its type), <c>ArrangementOrder.OrderEnum</c>, <c>Width</c>; the step-73 slot = the team's place in
    /// <c>Mission.Teams</c> × 8 + the formation's index (GameLineWorld's). Main thread (courage's look). The names are the enums' and types' own
    /// strings, cached - no allocation per read.</summary>
    internal sealed class GameBlockBody : IBlockBody
    {
        private static readonly string[] OrderNames = Enum.GetNames(typeof(MovementOrder.MovementOrderEnum));
        private static readonly string[] ArrangementNames = Enum.GetNames(typeof(ArrangementOrder.ArrangementOrderEnum));
        private readonly Dictionary<Type, string> _behaviourNames = new Dictionary<Type, string>();

        public bool Cap(TrackedAgent st, LineDiscipline? lines, out double depth, out int capFiles, out double capWidth, out int men, out bool playerSide, out bool ai)
        {
            depth = capWidth = double.NaN;
            capFiles = men = 0;
            playerSide = false;
            ai = true;
            if (lines == null) return false; // no line discipline this mission - nothing held (and no engine read)
            var a = st.Agent;
            var f = a.Formation;
            var m = Mission.Current;
            if (lines == null || f == null || m == null || a.Team == null) return false;
            int fi = (int)f.FormationIndex;
            if (fi < 0 || fi >= GameLineWorld.PerTeam) return false;
            int t = -1;
            var teams = m.Teams;
            for (int i = 0; i < teams.Count; i++)
                if (ReferenceEquals(teams[i], a.Team))
                {
                    t = i;
                    break;
                }
            if (t < 0) return false;
            if (!lines.TryCap(t * GameLineWorld.PerTeam + fi, out depth, out capFiles, out capWidth, out men, out playerSide, out bool player)) return false;
            ai = !player;
            return true;
        }

        public void Formation(TrackedAgent st, out string order, out string behaviour, out string arrangement, out double gameWidth)
        {
            order = behaviour = arrangement = "-";
            gameWidth = double.NaN;
            var f = st.Agent.Formation;
            if (f == null) return;
            int o = (int)f.GetReadonlyMovementOrderReference().OrderEnum;
            order = o >= 0 && o < OrderNames.Length ? OrderNames[o] : "?";
            int ar = (int)f.ArrangementOrder.OrderEnum;
            arrangement = ar >= 0 && ar < ArrangementNames.Length ? ArrangementNames[ar] : "?";
            gameWidth = f.Width;
            if (!f.IsAIControlled)
            {
                behaviour = "(your orders)";
                return;
            }
            var b = f.AI?.ActiveBehavior;
            if (b == null) return;
            var type = b.GetType();
            if (!_behaviourNames.TryGetValue(type, out var name))
            {
                name = type.Name;
                _behaviourNames[type] = name;
            }
            behaviour = name;
        }

        public string Name(TrackedAgent st)
        {
            var f = st.Agent.Formation;
            if (f == null) return "(no formation)";
            var team = f.Team;
            string who = team == null ? "" : team.IsPlayerTeam ? "your " : team.IsPlayerAlly ? "the ally's " : "the enemy's ";
            return who + f.FormationIndex.GetName();
        }
    }

    /// <summary>The block body every new <see cref="AthleticsLogic"/> starts with - the game's; the offline smoke swaps in its stand-in once.</summary>
    internal static class BlockBodyDefault
    {
        internal static IBlockBody Current = new GameBlockBody();
    }
}

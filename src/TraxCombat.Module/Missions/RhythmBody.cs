using TaleWorlds.MountAndBlade;

namespace TraxCombat.Missions
{
    /// <summary>
    /// Step 78: the battle's rhythm's own GAME calls behind one seam (the rest - positions, the scan, his frame, the hook - are courage's and the fear
    /// flock's bodies). The real one is <see cref="GameRhythmBody"/>; the offline smoke plays a stand-in.
    /// </summary>
    internal interface IRhythmBody
    {
        /// <summary>His unit is the player's (his team is the player's team) - step 78c's gap diagnostic counts by side.</summary>
        bool PlayerUnit(TrackedAgent st);

        /// <summary>Step 78c: where he LOOKS on the ground (the engine's look direction, unit length) - a resting man must face the enemy. False = unknown.</summary>
        bool Facing(TrackedAgent st, out double fx, out double fy);

        /// <summary>The two men's slots can be swapped: the same formation, neither detached, both with a place in its arrangement.</summary>
        bool CanSwap(TrackedAgent a, TrackedAgent b);

        /// <summary>The game's own <c>Formation.SwitchUnitLocations(a, b)</c> - each takes the other's place in the arrangement (the slot the engine and
        /// the wall hold walk him to). True = swapped.</summary>
        bool SwapSlots(TrackedAgent a, TrackedAgent b);
    }

    /// <summary>
    /// The engine side (AI_NOTES "Step 78"): <c>Formation.SwitchUnitLocations(Agent, Agent)</c> - PUBLIC in v1.4.8 (Formation.cs 2245: both not
    /// detached, at least one positioned → the arrangement's SwitchUnitLocations - <c>LineFormation</c> swaps the two in its unit grid and their file /
    /// rank indexes; the game itself calls it for its banner bearers and to keep front-unit types in front). <c>IFormationUnit.FormationFileIndex</c>
    /// (−1 = unpositioned). Main thread (courage's tick).
    /// </summary>
    internal sealed class GameRhythmBody : IRhythmBody
    {
        public bool PlayerUnit(TrackedAgent st)
        {
            var a = st.Agent;
            var m = Mission.Current;
            return m != null && a.Team != null && ReferenceEquals(a.Team, m.PlayerTeam);
        }

        public bool Facing(TrackedAgent st, out double fx, out double fy)
        {
            var d = st.Agent.LookDirection;
            double len = System.Math.Sqrt(d.x * d.x + d.y * d.y);
            fx = fy = 0;
            if (!(len > 1e-6)) return false;
            fx = d.x / len;
            fy = d.y / len;
            return true;
        }

        public bool CanSwap(TrackedAgent a, TrackedAgent b)
        {
            var fa = a.Agent.Formation;
            if (fa == null || !ReferenceEquals(fa, b.Agent.Formation)) return false;
            if (a.Agent.IsDetachedFromFormation || b.Agent.IsDetachedFromFormation) return false;
            return ((IFormationUnit)a.Agent).FormationFileIndex >= 0 && ((IFormationUnit)b.Agent).FormationFileIndex >= 0;
        }

        public bool SwapSlots(TrackedAgent a, TrackedAgent b)
        {
            if (!CanSwap(a, b)) return false;
            a.Agent.Formation.SwitchUnitLocations(a.Agent, b.Agent);
            return true;
        }
    }

    /// <summary>The rhythm body every new <see cref="AthleticsLogic"/> starts with - the game's; the offline smoke swaps in its stand-in once.</summary>
    internal static class RhythmBodyDefault
    {
        internal static IRhythmBody Current = new GameRhythmBody();
    }
}

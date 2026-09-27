using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// One fighter's record in <see cref="AthleticsLogic"/>: Core's pure <see cref="Fighter"/> state
    /// (Athletics, exhaustion, the speed multiplier the decorator applies) plus what blow
    /// detection needs from the game side. One object per human agent, created at spawn, kept in
    /// an array by <see cref="Agent.Index"/> (reference-checked - indices are reused) and a dense
    /// list for the tick loop. Plain fields: the tick loop touches them for ~1000 agents a frame.
    /// Main thread only.
    /// </summary>
    internal sealed class TrackedAgent : Fighter
    {
        public const int NoAction = -1;
        public const int MissileSlots = 4;

        public TrackedAgent(Agent agent)
        {
            Agent = agent;
            AgentIndex = agent.Index;
        }

        public readonly Agent Agent;

        /// <summary>The index at tracking time (the key in the by-index array).</summary>
        public readonly int AgentIndex;

        /// <summary>Position in the dense list (swap-removal).</summary>
        public int DenseSlot = -1;

        /// <summary>Heroes only (for the summary and the log) - read once at spawn.</summary>
        public string? HeroName;

        /// <summary>Out of the tick loop (removed from the field); a hero's record is kept for the summary.</summary>
        public bool Removed;

        // ---- melee: the channel-1 action type as last seen (poll or hit), for the rising edge into ReleaseMelee
        public int PrevAction = NoAction;

        public int ReleaseSerial;
        public double ReleaseStart = -1;
        public bool ReleaseStartPenalized;
        public bool ReleaseMixed;
        public bool HitThisRelease;
        public double LastReleaseTime = -1;
        public bool PenalizedAfterLastRelease;

        // ---- landed-only mode (CostOnMiss off) and couched lances
        public int LandedSerial;
        public double LastLandedCharge = double.NegativeInfinity;
        public double LastPassiveCharge = double.NegativeInfinity;

        // ---- ranged
        public double LastShotTime = double.NegativeInfinity;
        public bool PenalizedAfterLastShot;
        public double LastShotForInterval = -1;

        /// <summary>The last few missiles this fighter shot (their indices) - a landed-only shot is
        /// charged when one of THESE hits; siege-engine missiles never get here.</summary>
        public int Missile0 = -1, Missile1 = -1, Missile2 = -1, Missile3 = -1;
        public int MissileNext;

        /// <summary>The speed multiplier changed - recompute the agent's properties on the next
        /// pass of the tick loop (never inside an engine hit callback).</summary>
        public bool SpeedDirty;

        public void RememberMissile(int index)
        {
            switch (MissileNext)
            {
                case 0: Missile0 = index; break;
                case 1: Missile1 = index; break;
                case 2: Missile2 = index; break;
                default: Missile3 = index; break;
            }
            MissileNext = (MissileNext + 1) % MissileSlots;
        }

        /// <summary>True (once) when <paramref name="index"/> is one of this fighter's recent shots.</summary>
        public bool TakeMissile(int index)
        {
            if (index < 0) return false;
            if (Missile0 == index) { Missile0 = -1; return true; }
            if (Missile1 == index) { Missile1 = -1; return true; }
            if (Missile2 == index) { Missile2 = -1; return true; }
            if (Missile3 == index) { Missile3 = -1; return true; }
            return false;
        }
    }
}

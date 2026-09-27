using TaleWorlds.MountAndBlade;
using TraxCombat.Core;

namespace TraxCombat.Missions
{
    /// <summary>
    /// One fighter's record in <see cref="AthleticsLogic"/>: Core's pure <see cref="Fighter"/> state
    /// (Athletics as a fraction of his pool, his Athletics skill, health, exhaustion, the speed
    /// multipliers the decorator applies) plus what blow detection and the measurements need from
    /// the game side. One object per human agent, created at spawn, kept in
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

        /// <summary>The Athletics skill was read from his character at spawn (false: none - the floor).</summary>
        public bool SkillKnown;

        // ---- melee: the channel-1 action type as last seen (poll or hit), for the rising edge into ReleaseMelee
        public int PrevAction = NoAction;

        public int ReleaseSerial;
        public double ReleaseStart = -1;
        public bool ReleaseMixed;
        public bool HitThisRelease;
        public double LastReleaseTime = -1;

        // ---- the attack-speed check by f: the f bin and the attack multiplier in effect after the
        // last release / shot (what governs the interval up to the next one) and during this swing
        public int BinAfterLastRelease;
        public float AskedAfterLastRelease = 1f;
        public int ReleaseBin;
        public float ReleaseAsked = 1f;
        public int BinAfterLastShot;
        public float AskedAfterLastShot = 1f;

        // ---- the run-speed check: his own top speed (and his horse's) while no penalty applied
        public float FreshTop;
        public float FreshMountTop;
        public Agent? FreshMountOf;

        /// <summary>The horse whose speed the stat decorator scales for this rider now (registered in
        /// the logic's mount table by the horse's index), or null.</summary>
        public Agent? SlowedMount;

        /// <summary>His horse's multiplier changed, or he mounted / dismounted while one applied -
        /// recompute the horse(s) on the next pass of the tick loop.</summary>
        public bool MountDirty;

        /// <summary>The peak-zone state last logged for the player (his "below the line" lines).</summary>
        public bool PlayerBelowPeakLogged;

        // ---- landed-only mode (CostOnMiss off) and couched lances
        public int LandedSerial;
        public double LastLandedCharge = double.NegativeInfinity;
        public double LastPassiveCharge = double.NegativeInfinity;

        // ---- ranged
        public double LastShotTime = double.NegativeInfinity;
        public double LastShotForInterval = -1;

        /// <summary>The last few missiles this fighter shot (their indices) - a landed-only shot is
        /// charged when one of THESE hits; siege-engine missiles never get here.</summary>
        public int Missile0 = -1, Missile1 = -1, Missile2 = -1, Missile3 = -1;
        public int MissileNext;

        /// <summary>His attack or run multiplier changed - recompute the agent's properties on the
        /// next pass of the tick loop (never inside an engine hit callback).</summary>
        public bool SpeedDirty;

        /// <summary>His step back (step 5d) - queued or running; null until his first yes.</summary>
        public StepBackState? StepBack;

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

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
        public bool HitThisRelease;
        public double LastReleaseTime = -1;

        // ---- the attack-rate cycle by f (step 5e): the f bin and the attack multiplier in effect after
        // the last release / shot (what governs the cycle up to the next one)
        public int BinAfterLastRelease;
        public float AskedAfterLastRelease = 1f;
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

        // ---- step 5e: the attack cycle's phases on channel 1 (AttackPhase, or -1 = none running)
        public int PhaseKind = -1;
        public AttackKind PhaseAttack;
        public double PhaseStart;
        public int PhaseBin;
        public float PhaseAsked = 1f;

        /// <summary>In a ready that has not reached full wind-up yet: the tick polls its progress.</summary>
        public bool ReadyPolling;

        /// <summary>When the running ready reached full wind-up (−1 = not yet): the rest is the hold / aim.</summary>
        public double ReadyFullAt = -1;

        /// <summary>An attack (its release, recoil or reload) ended here; the next ready closes the pause (−1 = none).</summary>
        public double PauseFrom = -1;
        public AttackKind PauseAttack;
        public int PauseBin;
        public float PauseAsked = 1f;

        // ---- step 13: the no-attack timer. D of the attack running now (its wind-up up to full, then +
        // its release; ranged + the reload after the loose), filled by the phases.
        public double AttackDuration;

        /// <summary>The running attack's wind-up was seen (else D is only its release - "not measured").</summary>
        public bool AttackDurationKnown;

        /// <summary>The running attack's wind-up (seconds) - D's first part, for the log and his rest.</summary>
        public double CurrentWindUp;

        /// <summary>A ranged attack's loose ended into a reload: the attack ends when the reload does.</summary>
        public bool AwaitReloadEnd;

        /// <summary>An attack ended at this action change (set by the phases, taken by the timer's decision).</summary>
        public bool AttackEndedNow;
        public AttackKind EndedKind;
        public double EndedDuration;
        public bool EndedDurationKnown;

        /// <summary>His last measured rest of an attack (release, + reload for ranged) by kind - the
        /// player's hold from the release's start estimates his timer from it.</summary>
        public double LastRestMelee = -1;
        public double LastRestRanged = -1;

        /// <summary>His last timer (the player's or an AI hold) whose next attack is still to come - the
        /// summary measures the gap from its start (the attack's end) to that next attack.</summary>
        public bool TimerPending;
        public double TimerStart;
        public double TimerAsked;
        public float TimerM = 1f;
        public int TimerBin;
        public AttackKind TimerKind;

        /// <summary>A step back (5d) started since his last release: this cycle and its pause are the step
        /// back's, not his attack rhythm - left out of the attack-rate numbers.</summary>
        public bool SteppedBackThisCycle;

        /// <summary>His pace hold (step 5e) - queued, running or waiting for a game job; null until his first.</summary>
        public PaceState? Pace;

        /// <summary>Step 16: his input hold (the AI timer and the backpedal through his own input) and its component;
        /// null until he is first held that way.</summary>
        public AiInputState? Input;

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

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

        /// <summary>Step 18: the channel-0 (whole body) action type as last seen - polled on foot for kicks.</summary>
        public int PrevLowerAction = NoAction;

        /// <summary>Step 18: his kicks and shield bashes - one decision (charge) per action, both channels and
        /// the hit fallback deduped (Core <see cref="KickBashTracker"/>).</summary>
        public readonly KickBashTracker KickBash = new KickBashTracker();

        /// <summary>Step 22: the last blow he BLOCKED (attacker + swing + time) - one charge per blocked blow
        /// (Core <see cref="BlockTracker"/>, a struct: mutated in place through this field).</summary>
        public BlockTracker Block;

        /// <summary>Step 48: his impact state (Core <see cref="ImpactState"/>, a struct mutated in place through this field) - reeling,
        /// immunities, his recent outcomes; in shadow mode what WOULD have happened.</summary>
        public ImpactState Impact;

        /// <summary>Step 50: when a LIVE kick / bash last made his shield leave his hand (mission seconds; NaN = never) - the AI pickup lock
        /// (<c>ShieldPickupLockSeconds</c>) reads it.</summary>
        public double ShieldDroppedAt = double.NaN;

        /// <summary>Step 51: his tiredness T = 1 − f as the block stun reads it - written by the MAIN thread on every tick while blocks are
        /// live (× <c>ImpactOnPlayerPercent</c> for you, 0 on a horse - riders are step 53's), read by the damage decorator's stun seam on
        /// WORKER threads (a float write / read is atomic; a one-tick-old value is fine). 0 = fresh (never written yet).</summary>
        public volatile float ImpactTired;

        /// <summary>His melee releases so far - a blow's number for the defender's block dedupe (step 22) too.</summary>
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

        /// <summary>Step 30: f when <see cref="Fighter.BlockRandomWeight"/> was last re-targeted (the summary's band of the
        /// chance the decorator writes), and whether his chance was ever raised this mission (the summary's men).</summary>
        public double BlockRandomPeakShare = 1.0;
        public bool BlockRandomSeen;

        /// <summary>His step back (step 5d) - queued or running; null until his first yes.</summary>
        public StepBackState? StepBack;

        // ---- step 5e: the attack cycle's phases on channel 1 (AttackPhase, or -1 = none running)
        public int PhaseKind = -1;
        public AttackKind PhaseAttack;
        public double PhaseStart;
        public int PhaseBin;
        public float PhaseAsked = 1f;

        /// <summary>Step 20b: the attack animation multiplier the running phase plays at (from its m at the start) -
        /// its played seconds × this = the seconds at full animation speed that D is built from.</summary>
        public float PhaseAnimation = 1f;

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
        // its release; ranged + the reload after the loose), filled by the phases. Step 20b: at FULL
        // animation speed (each phase's played seconds × the animation multiplier it played at), so the
        // pause D × (1/m − 1) does not grow with a slower swing; AttackPlayed = the same attack on the clock.
        public double AttackDuration;
        public double AttackPlayed;

        /// <summary>The running attack's wind-up was seen (else D is only its release - "not measured").</summary>
        public bool AttackDurationKnown;

        /// <summary>The running attack's wind-up (seconds at full animation speed - step 20b) - D's first part, for the log and his rest.</summary>
        public double CurrentWindUp;

        /// <summary>A ranged attack's loose ended into a reload: the attack ends when the reload does.</summary>
        public bool AwaitReloadEnd;

        /// <summary>An attack ended at this action change (set by the phases, taken by the timer's decision).</summary>
        public bool AttackEndedNow;
        public AttackKind EndedKind;
        public double EndedDuration;
        public double EndedPlayed;
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

        // ---- step 21: battle pace (AI_NOTES "Step 21")
        /// <summary>The class of his attack under way, read at its release from what he really holds (AI only).</summary>
        public AttackClass AttackClass = AttackClass.FootMelee;

        /// <summary>His last ended AI attack, for the next cycle note (valid once): its class and kind, the battle-pace share of
        /// the pause after it (0 = none) and the model's cycle with the setting at 0 and with it (NaN = no model).</summary>
        public bool PaceLastValid;
        public AttackClass PaceLastClass;
        public AttackKind PaceLastKind;
        public double PaceLastShare;
        public double PaceModelAt0 = double.NaN;
        public double PaceModelWith = double.NaN;

        /// <summary>The classes he attacked with this battle (a bit per class) - the summary's "by N men".</summary>
        public int PaceClassesSeen;

        /// <summary>The class of his last AI timer - the gap after it is filed under it.</summary>
        public AttackClass TimerClass;

        /// <summary>Step 23: his brace (his margin's roll, bracing now, the shield) - null until his first look as an AI man.</summary>
        public BraceState? Brace;

        /// <summary>Step 41: his shield-wall hold (held now, vanilla's update timer, the next sample) - null until his first look.</summary>
        public WallState? Wall;

        /// <summary>Step 32: his swing stack (spawn full).</summary>
        public readonly SwingStack Swings = new SwingStack();

        /// <summary>Step 32: the D (at full animation speed) of his last measured attack - the refill is timed by it (NaN = none yet:
        /// <see cref="SwingStackMath.FallbackSwingSeconds"/>).</summary>
        public double StackD = double.NaN;

        /// <summary>Step 33: his sprint store (full until its first clamp) and the sprint running now.</summary>
        public readonly SprintStore Sprint = new SprintStore();

        /// <summary>Step 35 (was step 33's SprintMultiplier): the sprint's RUN FACTOR applied (quantised in 0.05 steps; 0 = no
        /// sprint) - × his FRESH run speed: while it is set the stat decorator writes it on his run property INSTEAD of his tiredness
        /// run multiplier (Core <see cref="TraxCombat.Core.SprintMath.RunFactor"/>), and caps his TopSpeedReachDuration.</summary>
        public float SprintRun;

        /// <summary>Step 35: the game's TopSpeedReachDuration as the base model last wrote it (the stat decorator's every recompute;
        /// 0 = not seen yet) - the first-sprint line and the summary's acceleration.</summary>
        public float GameReach;

        /// <summary>Step 33: his top speed and run multiplier at the sprint's start (the speed check's base; 0 = no base).</summary>
        public float SprintBaseTop;

        public float SprintBaseRun = 1f;

        /// <summary>Step 33: an AI sprinter's next "enemy within 3 m" look (mission time).</summary>
        public double SprintNextMelee;

        /// <summary>Step 43: his fever (armed at a new charge / advance order, burning from contact, fading to his tired share).</summary>
        public readonly FeverStore Fever = new FeverStore();

        /// <summary>Step 43: the factor applied on the AI's two attack-decision chances (his share by his bar, lifted by a fever, 0.05 steps; 1 = the game's) - the
        /// stat decorator writes it on every recompute; a change marks him for one.</summary>
        public float DecisionFactor = 1f;

        /// <summary>Step 96: the factors the stat decorator applies on the game's block-direction guess chance and its block- / parry-decide abilities
        /// (1 = the game's; the player 1) - re-targeted at tracking and with the speeds (a settings change marks him for a recompute).</summary>
        public float GuardGuess = 1f, GuardDecide = 1f;

        /// <summary>Step 96: the guard was written for him at least once (the summary's men).</summary>
        public bool GuardSeen;

        /// <summary>Step 43: an enemy was within FeverContactMetres at his last fever look (the summary's "in contact"), and when.</summary>
        public bool FeverContact;

        public double FeverLastLook = double.NaN;

        /// <summary>Step 43: in no formation at his last fever look (tournaments, arenas, duels - the summary's own group).</summary>
        public bool NoFormation;

        /// <summary>Step 54 (the AI punishes a window), as the PUNISHER: his share raised to ImpactPunishAggressionPercent (his target is in a
        /// window at his last look - RetargetDecision reads it); the target read at his last look (null when none was read - no window open
        /// anywhere, not in contact); the window's state at that look and whether the punish was on / the windows live then (the proof's time).</summary>
        public bool Punishing;
        public TrackedAgent? PunishTarget;
        public bool PunishInWindow;
        public bool PunishOn;
        public bool PunishContact;

        /// <summary>Step 54, as the VICTIM: the end of his last counted window (+ the grace; NaN = none yet), whether an AI enemy in contact saw
        /// it and whether an AI melee attack started inside it (the summary's opened / seen / punished).</summary>
        public double PunishWindowEnd = double.NaN;
        public bool PunishWindowSeen;
        public bool PunishWindowHit;

        /// <summary>Step 49: his courage - brave / timid (walking back or holding), the walk's target and direction, the flips.</summary>
        public readonly CourageMind Courage = new CourageMind();

        /// <summary>Step 49: his toughness at full health (armour and level, read once - NaN until first needed) and whether he carries a
        /// two-hander / polearm and no shield (an enemy who does weighs CourageHeavyWeaponPercent).</summary>
        public double CourageBase = double.NaN;
        public bool CourageHeavy;

        /// <summary>Step 67: his troop level from the same kit read (−1 until read) - his DISCIPLINE.</summary>
        public int CourageLevel = -1;

        /// <summary>Step 67: his fear-flock state - calm / flocking / anchored, his place, the steering.</summary>
        public readonly FearMind Fear = new FearMind();

        /// <summary>Step 69: his idle watch (the idle-men diagnostic - standing off while his unit charges; measurement only).</summary>
        public readonly IdleMind Idle = new IdleMind();

        /// <summary>Step 70: his lone-runner watch (alone at the last look, out beyond his line, the cause).</summary>
        public readonly LoneMind Lone = new LoneMind();

        /// <summary>Step 74: his block-hold state (walked back into his block or not, his place in it).</summary>
        public readonly BlockMind BlockHold = new BlockMind();

        /// <summary>Step 77: his press state (pressing an enemy line, pushed out of it, walked round its end; the crossing watch).</summary>
        public readonly PressMind Press = new PressMind();

        /// <summary>Step 78: his rhythm - his last look (unit, time), his swap, his rest, his lull wish (Core <see cref="RhythmMind"/>).</summary>
        public readonly RhythmMind Rhythm = new RhythmMind();

        /// <summary>Step 79: he may shout again from this time on (his own war-cry limit), when he last shouted, and a cry of his waits in the queue.</summary>
        public double WarCryNext = double.NegativeInfinity, WarCryLast = double.NaN;
        public bool WarCryQueued;

        /// <summary>Step 76: his spear preference (RBM's two timers, his loadout, his look clock) - the stat decorator reads <c>Spear.Favored</c> /
        /// <c>Spear.Close</c>.</summary>
        public SpearMind Spear = SpearMind.Fresh;

        /// <summary>Step 76: the four slot items his loadout was read from (RBM's loadout cache - re-read only when one changes).</summary>
        public object? SpearItem0, SpearItem1, SpearItem2, SpearItem3;

        /// <summary>Step 76: counted in the summary's men (with both weapons / without).</summary>
        public bool SpearCounted, SpearCountedBoth;

        /// <summary>Step 76: what he held at his last spear look (an attack's weapon for the summary).</summary>
        public SpearHeld SpearHeldNow = SpearHeld.Other;

        /// <summary>Step 76: when his last thrust passed a friend (NaN = none pending) - an enemy hit by him within 0.5 s is "the spear reached".</summary>
        public double ThrustPassAt = double.NaN;

        /// <summary>Step 67: his morale as the game keeps it (0-100; −1 none; NaN = not read yet) - read at every formation-stats refresh
        /// (the ALT morale bar and the labels' mean) and at courage's looks.</summary>
        public double Morale = double.NaN;

        /// <summary>Step 62: his toughness kit for the impact model (armour sum, level) - read once (NaN = not yet); the toughness itself is
        /// computed at each decision from courage's weights (live) and his health now.</summary>
        public double ImpactArmour = double.NaN;
        public int ImpactLevel;

        /// <summary>Step 62, THE CHARGE: when his last sprint toward the enemy ended (mission seconds; NaN = none, or its carry was spent on a
        /// blow) - his first blow within <c>ImpactChargeCarrySeconds</c> after it is a charge. A sprint back or a rout never sets it.</summary>
        public double SprintEndedAt = double.NaN;

        /// <summary>Step 64: a charge order's sprint WAITS for an enemy within SprintChargeWaitMetres (his store was below
        /// SprintChargeWaitBelowSeconds) - looked at every <c>SprintMath.MeleeCheckSeconds</c> by the sprint pass; a new order ends the wait.</summary>
        public bool ChargeSprintWaiting;

        /// <summary>Step 64: the attack he is making BEGAN while he charged (sprinting, or within <c>ImpactChargeCarrySeconds</c> after a sprint) -
        /// its blow is a charge however long the swing; cleared when the attack ends or its blow is decided.</summary>
        public bool ChargeAttack;

        /// <summary>Step 49: looked at for courage at least once (the summary's men).</summary>
        public bool CourageSeen;

        /// <summary>Step 49: his last landed hits (OnAgentHit) - the SHOCK window reads them (a struct mutated in place through this field).</summary>
        public ShockTimes Hits;

        /// <summary>Step 55: his cavalry flocking - kind, flock, eagerness, free / flocking / in a wave, his slot, the scripted point, his passes.</summary>
        public readonly RiderFlock Flock = new RiderFlock();

        /// <summary>Step 65: his AI kick / bash attempt - the phase (None = none), the kind, when it began, the phase's end, the man-type column
        /// (step 81: 0 a shieldman, 1 no shield), the action seen (played), landed (a hit counted), the target; the cooldown's end; his last
        /// contact sample. Step 81: AiMoveQueued - his melee wind-up waits in the pending queue for the tick's roll.</summary>
        public AiKickPhase AiKickPhase;
        public AiKickKind AiKickKind;
        public double AiKickStart = double.NaN;
        public double AiKickPhaseEnd = double.NaN;
        public int AiKickReason;
        public bool AiKickSeen;
        public bool AiKickLanded;
        public TrackedAgent? AiKickTarget;
        public double AiKickNext = double.NegativeInfinity;
        public double AiKickLastLook = double.NaN;
        public double AiKickAngle = double.NaN; // step 87: the facing-to-target angle at the decision
        public double AiKickDistance = double.NaN; // step 91: the enemy's distance at the decision (a bash's result is binned by it)
        public int AiKickLeftNear = -1, AiKickRightNear = -1; // step 90: friends within 0.9 m on his left / right at the decision (-1 = not counted)
        public bool AiMoveQueued;
        public AiStepIn AiStepIn; // step 95: what the step-in did for his running bash (Arrived while still walking = "he steps")
        public bool AiStepping; // step 95: walking in now (the guard phase)
        public double AiStepStop = double.NaN; // step 95: the enemy's distance he walks in to
        public double AiKickPressDistance = double.NaN; // step 95: the enemy's distance at the bash's key (NaN = never got there)

        /// <summary>Step 82: his READ of an incoming kick / bash - reading (rolled yes, in the readers' pool), the kind (a bash = guard right),
        /// when it began, the guard up, the man it comes from; MeetReadRolledFor = the attacker whose start he already rolled for (once).</summary>
        public bool MeetReading;
        public bool MeetReadBash;

        /// <summary>Step 88: the read kick / bash is a weapon PUSH - the read guard is UP.</summary>
        public bool MeetReadPush;
        public bool MeetGuardUp;
        public double MeetReadStart = double.NaN;
        public TrackedAgent? MeetReadBy;
        public TrackedAgent? MeetReadRolledFor;
        public double MeetReadRolledAt = double.NaN;

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

using System;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 47 (IMPACT I1, docs/IMPACT.md §7): the six engine levers the impact system will pull, proven one by one by the SPIKE - a
    /// test tool (Advanced <c>ImpactSpike</c>, off) that applies fixed loud values in a real battle and logs per lever whether the engine
    /// did what was asked. The levers are the reusable seams of steps 48-53 (the module's <c>IImpactBody</c> + <c>ImpactHooks</c>).
    /// </summary>
    public enum SpikeLever
    {
        /// <summary>1: a landed kick / bash gets the KnockDown decision (the damage decorator's <c>DecideAgentKnockedDownByBlow</c>).</summary>
        KickKnockDown = 0,

        /// <summary>2: every block's stun x3, the game's 0.8 s cap raised to 2 s (<c>CalculateDefendedBlowStunMultipliers</c> - worker threads).</summary>
        BlockStun = 1,

        /// <summary>3: a kicked man's reel and a knockdown's fall-and-rise played at speed 0.5 (<c>SetCurrentActionSpeed</c> the tick after).</summary>
        SlowReaction = 2,

        /// <summary>4: the first block every 10 s → a forced <c>act_stagger_backward</c> on the blocker (never a rider or a horse).</summary>
        ForcedStagger = 3,

        /// <summary>5: a kicked man holding a shield drops it (<c>DropItem</c> queued with <c>Mission.AddTickAction</c>, vanilla's way).</summary>
        ShieldDrop = 4,

        /// <summary>6: a blow of our own (<c>RegisterBlow</c>, KnockDown, 1 damage - vanilla's drowning-blow recipe) on a blocker every 20 s.</summary>
        OwnBlow = 5,
    }

    /// <summary>Step 47: what a lever's numbers say.</summary>
    public enum SpikeVerdict
    {
        /// <summary>Not enough seen to tell (nothing asked, or too few) - play more of that event.</summary>
        Unseen = 0,

        /// <summary>The engine did what was asked.</summary>
        Works = 1,

        /// <summary>Asked often enough and the engine did not follow (or the lever was disabled by an error) - steps 48-53 use the fallback.</summary>
        Fails = 2,
    }

    /// <summary>Step 47: a stagger's direction - <c>act_stagger_{backward|forward|left|right}</c> (light; the _2 / _3 kinds are I4's).</summary>
    public enum StaggerKind
    {
        Backward = 0,
        Forward = 1,
        Left = 2,
        Right = 3,
    }

    /// <summary>Step 47: why the impact body refuses to start an action on a man (in the order it checks) - RBM crashed a horse with a forced
    /// stagger, so a rider or a horse is refused BEFORE any engine call.</summary>
    public enum ForceRefusal
    {
        None = 0,

        /// <summary>He left the field (not active).</summary>
        Gone,

        /// <summary>Not a human (a horse).</summary>
        NotHuman,

        /// <summary>He rides a horse (the horse's legs share channel 0).</summary>
        Mounted,

        /// <summary>He uses a game object (a siege engine, a ladder).</summary>
        GameObject,

        /// <summary>He is in water.</summary>
        Water,

        /// <summary>He is already on the ground (a Fall on channel 0).</summary>
        Down,

        Count,
    }

    /// <summary>Step 47: what a watched reaction must look like (the channel action types the engine reports).</summary>
    public enum WatchKind
    {
        /// <summary>The kicked man's reaction: any strike type (48-52) or a Fall (23), on channel 0 or 1.</summary>
        Reaction = 0,

        /// <summary>A knockdown: a Fall (23) - the fall and the rise are one action (+ its _continue).</summary>
        Fall = 1,

        /// <summary>The attacker's recoil after a block: BlockedMelee (22) / ParriedMelee (21).</summary>
        Recoil = 2,

        /// <summary>The blocker's guard frozen by the stun: his defend action (1-14) on channel 1 at the block, until it changes.</summary>
        Guard = 3,

        /// <summary>A forced stagger: the very action we started, on channel 0.</summary>
        Stagger = 4,
    }

    /// <summary>Step 47: one step of a watch.</summary>
    public enum WatchEvent
    {
        None = 0,

        /// <summary>The reaction showed (the module slows it now if asked).</summary>
        Appeared,

        /// <summary>A new action of the same kind followed on its channel (a _continue - slowed again if asked).</summary>
        NewAction,

        /// <summary>It ended - the length is out.</summary>
        Ended,

        /// <summary>It never showed within its window.</summary>
        NeverAppeared,

        /// <summary>Still on after <see cref="SpikeMath.MeasureCapSeconds"/> - counted at the cap.</summary>
        Capped,
    }

    /// <summary>Step 47: one man's actions as read in one tick - the action TYPE (the engine's ActionCodeType) and the action INDEX on the
    /// whole-body channel 0 and the upper-body channel 1.</summary>
    public struct ActionRead
    {
        public int Type0;
        public int Index0;
        public int Type1;
        public int Index1;

        public ActionRead(int type0, int index0, int type1, int index1)
        {
            Type0 = type0;
            Index0 = index0;
            Type1 = type1;
            Index1 = index1;
        }

        public int Type(int channel) => channel == 0 ? Type0 : Type1;

        public int Index(int channel) => channel == 0 ? Index0 : Index1;
    }

    /// <summary>Step 47: one melee collision as the spike sees it (from the engine's <c>AttackCollisionData</c> in OnMeleeHit; the smoke makes
    /// them up).</summary>
    public readonly struct SpikeHit
    {
        public SpikeHit(bool kick, bool bash, bool landed, BlockKind block, int damage)
        {
            Kick = kick;
            Bash = bash;
            Landed = landed;
            Block = block;
            Damage = damage;
        }

        /// <summary>A kick or a shield bash (the engine's alternative attack).</summary>
        public bool Kick { get; }

        /// <summary>A bash (an alternative attack with a weapon / shield) rather than a kick.</summary>
        public bool Bash { get; }

        /// <summary>The blow lands on him (the engine registers it).</summary>
        public bool Landed { get; }

        /// <summary>He blocked a melee blow (step 22's kinds; a kick / bash is never a block).</summary>
        public BlockKind Block { get; }

        /// <summary>The damage the engine will inflict (0 = no reaction plays at all).</summary>
        public int Damage { get; }

        public static SpikeHit LandedKick(int damage = 3, bool bash = false) => new SpikeHit(true, bash, true, BlockKind.None, damage);

        public static SpikeHit Blocked(BlockKind kind = BlockKind.ShieldRightSide) => new SpikeHit(false, false, false, kind, 0);
    }

    /// <summary>
    /// Step 47: the engine's action codes the spike reads (v1.4.8 <c>Agent.ActionCodeType</c>, <c>CombatCollisionResult</c> - the smoke checks
    /// them against the game's enums).
    /// </summary>
    public static class ImpactActions
    {
        public const int DefendFirst = 1;
        public const int DefendLast = 14;
        public const int ParriedMelee = 21;
        public const int BlockedMelee = 22;
        public const int Fall = 23;
        public const int StrikeLight = 48;
        public const int StrikeMedium = 49;
        public const int StrikeHeavy = 50;
        public const int StrikeKnockBack = 51;
        public const int MountStrike = 52;

        /// <summary><c>CombatCollisionResult.StrikeAgent</c> - the collision landed on a man (a kick that hit the shield is Blocked).</summary>
        public const int ResultStrikeAgent = 1;

        public static bool IsStrike(int type) => type >= StrikeLight && type <= MountStrike;

        public static bool IsFall(int type) => type == Fall;

        public static bool IsRecoil(int type) => type == ParriedMelee || type == BlockedMelee;

        public static bool IsDefend(int type) => type >= DefendFirst && type <= DefendLast;

        /// <summary>Step 50: his own release / bash on channel 1 - vanilla's drop key refuses a drop then (Agent.HandleDropWeapon 4446), so
        /// the live shield drop waits for it to pass.</summary>
        public const int ReleaseRanged = 16;
        public const int ReleaseThrowing = 17;
        public const int ReleaseMelee = 20;
        public const int WeaponBash = 31;

        public static bool RefusesDrop(int type1) => type1 == ReleaseRanged || type1 == ReleaseThrowing || type1 == ReleaseMelee || type1 == WeaponBash;

        /// <summary>The type in the log's words.</summary>
        public static string Name(int type) => type switch
        {
            Fall => "Fall",
            ParriedMelee => "ParriedMelee",
            BlockedMelee => "BlockedMelee",
            StrikeLight => "StrikeLight",
            StrikeMedium => "StrikeMedium",
            StrikeHeavy => "StrikeHeavy",
            StrikeKnockBack => "StrikeKnockBack",
            MountStrike => "MountStrike",
            _ => IsDefend(type) ? "Defend(" + type.ToString(CultureInfo.InvariantCulture) + ")" : "type " + type.ToString(CultureInfo.InvariantCulture),
        };
    }

    /// <summary>
    /// Step 47: the spike's switch, read live. <see cref="Enabled"/> = the master switch FIRST, then <c>ImpactSpike</c>. Nothing else of the
    /// impact model (that is step 48's).
    /// </summary>
    public readonly struct SpikeRules
    {
        public SpikeRules(bool modEnabled, bool spike)
        {
            ModEnabled = modEnabled;
            Spike = spike;
        }

        public bool ModEnabled { get; }
        public bool Spike { get; }
        public bool Enabled => ModEnabled && Spike;

        /// <summary>The switch that holds it off (the master switch first), or null when on.</summary>
        public string? OffBecause => !ModEnabled ? "ModEnabled" : !Spike ? "ImpactSpike" : null;

        public static SpikeRules From(TraxSettings s) => new SpikeRules(s.ModEnabled, s.ImpactSpike);

        /// <summary>The settings in one sentence (the mission-start and change lines, the summary).</summary>
        public string Describe()
        {
            if (!Enabled) return "OFF (" + OffBecause + ") - nothing of the impact system runs (vanilla)";
            return "SPIKE ON (ImpactSpike - a test tool, not gameplay): fixed loud values prove each engine lever - "
                   + "1) half the landed kicks / bashes get the KnockDown decision, "
                   + "2) every block's stun x" + SpikeMath.StunFactor.ToString("0", CultureInfo.InvariantCulture) + " for blockers of an EVEN agent index (odd = vanilla, the control), the game's stun cap "
                   + SpikeMath.VanillaStunCapText + " raised to " + SpikeMath.StunCapSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s, "
                   + "3) every other kick's reel / knockdown played at speed " + SpikeMath.ReactionSpeed.ToString("0.0", CultureInfo.InvariantCulture) + " (the rest untouched, the control), "
                   + "4) the first block every " + SpikeMath.StaggerEverySeconds.ToString("0", CultureInfo.InvariantCulture) + " s → a forced act_stagger_backward on the blocker (never a rider), "
                   + "5) every kicked man holding a shield drops it, "
                   + "6) a blow of our own (KnockDown, " + SpikeMath.OwnBlowDamage.ToString(CultureInfo.InvariantCulture) + " damage) on a blocker every "
                   + SpikeMath.OwnBlowEverySeconds.ToString("0", CultureInfo.InvariantCulture) + " s; the [summary] \"impact spike\" lines give each lever's verdict";
        }
    }

    /// <summary>Step 47: what the spike does with the n-th landed kick / bash of the battle (a 4-kick rotation so each lever has its own
    /// untouched control in the same battle).</summary>
    public readonly struct KickPlan
    {
        public KickPlan(int number, bool knockDown, bool slow)
        {
            Number = number;
            KnockDown = knockDown;
            Slow = slow;
        }

        /// <summary>The kick's number in the battle (1 = the first).</summary>
        public int Number { get; }

        /// <summary>Lever 1: the decorator answers KnockDown for it.</summary>
        public bool KnockDown { get; }

        /// <summary>Lever 3: its reaction (the reel, or the fall-and-rise) is played at <see cref="SpikeMath.ReactionSpeed"/>.</summary>
        public bool Slow { get; }
    }

    /// <summary>
    /// Step 47: the spike's pure rules - the rotation, the A/B split, the timing, the verdicts, the words. The fixed loud values are
    /// DOCUMENTED CONSTANTS of a test tool (IMPACT §7: "fixed, loud values"), not settings: the spike proves the engine, it tunes nothing.
    /// </summary>
    public static class SpikeMath
    {
        public const int LeverCount = 6;

        /// <summary>Lever 2: the blockers' (and their attackers') stun x this.</summary>
        public const float StunFactor = 3f;

        /// <summary>Lever 2: the game's <c>StunPeriodMax</c> raised to this while the spike runs (the game's own value, 0.8 s, is restored after).</summary>
        public const float StunCapSeconds = 2f;

        /// <summary>The game's own stun cap (managed_core_parameters.xml) - only for the sentences; the code reads the live value.</summary>
        public const string VanillaStunCapText = "0.8 s";

        /// <summary>Lever 3: the playback speed of a slowed reaction (0.5 = twice as long, if the engine follows).</summary>
        public const float ReactionSpeed = 0.5f;

        /// <summary>Lever 4: a forced stagger at most this often (the first block after it).</summary>
        public const double StaggerEverySeconds = 10;

        /// <summary>Lever 4: the forced stagger's speed (1 = its natural length, which the spike measures).</summary>
        public const float StaggerSpeed = 1f;

        /// <summary>Lever 6: a blow of our own at most this often (the first block after it).</summary>
        public const double OwnBlowEverySeconds = 20;

        /// <summary>Lever 6: our blow's damage - at least 1, or the engine plays no reaction at all (Agent.HandleBlow returns at 0).</summary>
        public const int OwnBlowDamage = 1;

        /// <summary>A reaction must show within this after its event (a kick's reel, a recoil, the stagger we started).</summary>
        public const double AppearSeconds = 0.6;

        /// <summary>A knockdown's Fall must show within this after the blow.</summary>
        public const double FallAppearSeconds = 1.0;

        /// <summary>A reaction still on after this is counted at this length (a stuck watch never blocks a slot).</summary>
        public const double MeasureCapSeconds = 8;

        /// <summary>Lever 5: the queued drop must have happened within this.</summary>
        public const double DropSeenSeconds = 1.5;

        /// <summary>Lever 5: a dropped shield is watched this long for a pick-up.</summary>
        public const double PickupWindowSeconds = 180;

        /// <summary>Lever 5: a dropped shield's man looked at this often.</summary>
        public const double ShieldLookSeconds = 0.5;

        /// <summary>Plumbing: watches at once (a full pool = the new one is counted, not watched), queued actions, shields followed.</summary>
        public const int MaxWatches = 64;
        public const int MaxPending = 64;
        public const int MaxShields = 32;

        /// <summary>Plumbing (IMPACT §3.9's <c>ImpactMaxActionsPerTick</c>): engine calls a tick (forced actions, drops, own blows) - the rest wait.</summary>
        public const int MaxActionsPerTick = 20;

        /// <summary>A count verdict needs this many asked before "never" means FAILS.</summary>
        public const int MinAsked = 3;

        /// <summary>A length verdict: treated ÷ control at or above this = the lever lengthens (0.5 speed should give ~2, x3 stun up to 3).</summary>
        public const double WorksRatio = 1.4;

        /// <summary>A length verdict: below this with <see cref="MinAsked"/> samples each side = it does not.</summary>
        public const double FailsRatio = 1.15;

        /// <summary>The n-th landed kick (1-based): 1 = KnockDown, untouched (the fall's own length); 2 = untouched (the vanilla reel); 3 =
        /// KnockDown, slowed (the slower rise); 4 = slowed reel; then again. A lever disabled = its part off.</summary>
        public static KickPlan Kick(int number, bool knockDownLever, bool slowLever)
        {
            int k = ((number - 1) % 4 + 4) % 4;
            bool knock = k == 0 || k == 2;
            bool slow = k >= 2;
            return new KickPlan(number, knockDownLever && knock, slowLever && slow);
        }

        /// <summary>Lever 2's A/B split by the BLOCKER's agent index - even = x3, odd = vanilla. Deterministic, so the worker thread (the
        /// decorator) and the main thread (the measurement) agree without sharing a write.</summary>
        public static bool StunTreated(int defenderIndex) => (defenderIndex & 1) == 0;

        /// <summary>The stun asked for a treated block: x<see cref="StunFactor"/>, never above <paramref name="cap"/>.</summary>
        public static float StunAsked(float vanilla, float cap)
        {
            if (float.IsNaN(vanilla) || vanilla <= 0f) return vanilla;
            float v = vanilla * StunFactor;
            return v > cap ? cap : v;
        }

        /// <summary>A periodic lever is due: never fired yet, or <paramref name="every"/> seconds since.</summary>
        public static bool Due(double now, double last, double every) => double.IsNaN(last) || now - last >= every || now < last;

        /// <summary>Lever 4 alternates the engine's priority rule: odd attempts (1st, 3rd…) ask with it (ignorePriority false - would the
        /// engine take a stagger over what he does?), even attempts over it (true).</summary>
        public static bool IgnorePriority(int attempt) => attempt % 2 == 0;

        /// <summary>A landed kick / bash for the spike - the engine registers the blow (not blocked, on a man).</summary>
        public static bool KickLanded(bool isAlternativeAttack, bool blockedWithShield, int collisionResult)
            => isAlternativeAttack && !blockedWithShield && collisionResult == ImpactActions.ResultStrikeAgent;

        /// <summary>The stagger away from the blow: the attacker in front → backward (the spike always uses backward - RESEARCH §R3).</summary>
        public static StaggerKind StaggerFor() => StaggerKind.Backward;

        /// <summary>asked / seen → a verdict: nothing asked = UNSEEN; at least half seen = WORKS; fewer than half after
        /// <see cref="MinAsked"/> = FAILS; fewer asked = UNSEEN (too few).</summary>
        public static SpikeVerdict CountVerdict(int asked, int seen)
        {
            if (asked <= 0) return SpikeVerdict.Unseen;
            if (seen > 0 && seen * 2 >= asked) return SpikeVerdict.Works;
            return asked >= MinAsked ? SpikeVerdict.Fails : SpikeVerdict.Unseen;
        }

        /// <summary>A measured length, treated vs control: ratio ≥ <see cref="WorksRatio"/> with 2+ samples each = WORKS; below
        /// <see cref="FailsRatio"/> with <see cref="MinAsked"/>+ each = FAILS; else UNSEEN.</summary>
        public static SpikeVerdict LengthVerdict(in MeanStd control, in MeanStd treated)
        {
            if (control.Count < 2 || treated.Count < 2 || !(control.Mean > 0)) return SpikeVerdict.Unseen;
            double ratio = treated.Mean / control.Mean;
            if (ratio >= WorksRatio) return SpikeVerdict.Works;
            if (ratio < FailsRatio && control.Count >= MinAsked && treated.Count >= MinAsked) return SpikeVerdict.Fails;
            return SpikeVerdict.Unseen;
        }

        /// <summary>Two parts of one lever: a FAIL wins (the lever cannot be trusted), then a WORKS, else UNSEEN.</summary>
        public static SpikeVerdict Combine(SpikeVerdict a, SpikeVerdict b)
        {
            if (a == SpikeVerdict.Fails || b == SpikeVerdict.Fails) return SpikeVerdict.Fails;
            if (a == SpikeVerdict.Works || b == SpikeVerdict.Works) return SpikeVerdict.Works;
            return SpikeVerdict.Unseen;
        }

        public static string VerdictText(SpikeVerdict v) => v switch
        {
            SpikeVerdict.Works => "WORKS",
            SpikeVerdict.Fails => "FAILS",
            _ => "UNSEEN",
        };

        /// <summary>The lever's number (1-6) as the log and PLAYTEST D16 count them.</summary>
        public static int Number(SpikeLever lever) => (int)lever + 1;

        /// <summary>The lever in the log's words.</summary>
        public static string Name(SpikeLever lever) => lever switch
        {
            SpikeLever.KickKnockDown => "KnockDown on a landed kick / bash (the damage decorator's decision)",
            SpikeLever.BlockStun => "block stun x3, cap 2 s (CalculateDefendedBlowStunMultipliers)",
            SpikeLever.SlowReaction => "reaction speed 0.5 (SetCurrentActionSpeed the tick after)",
            SpikeLever.ForcedStagger => "forced stagger (SetActionChannel act_stagger_backward)",
            SpikeLever.ShieldDrop => "shield dropped (DropItem queued with AddTickAction)",
            SpikeLever.OwnBlow => "our own knockdown blow (RegisterBlow, KnockDown, 1 damage)",
            _ => "lever " + ((int)lever + 1).ToString(CultureInfo.InvariantCulture),
        };

        /// <summary>The lever's short name (the "lever N disabled" line, the smoke).</summary>
        public static string Short(SpikeLever lever) => lever switch
        {
            SpikeLever.KickKnockDown => "kick knockdown",
            SpikeLever.BlockStun => "block stun",
            SpikeLever.SlowReaction => "slow reaction",
            SpikeLever.ForcedStagger => "forced stagger",
            SpikeLever.ShieldDrop => "shield drop",
            SpikeLever.OwnBlow => "own knockdown blow",
            _ => "lever",
        };

        /// <summary>What the verdict means for steps 48-53 (IMPACT §7's fallbacks) - the end of each lever's summary line, PLAYTEST D16.</summary>
        public static string NextStep(SpikeLever lever, SpikeVerdict v)
        {
            switch (lever)
            {
                case SpikeLever.KickKnockDown:
                    return v == SpikeVerdict.Works ? "I3 / I5 knock men down through the decorator's DecideAgentKnockedDownByBlow"
                        : v == SpikeVerdict.Fails ? "the engine ignores KnockDown on a kick: I3 knocks down with our own blow after the kick (lever 6), else Harmony on CreateMeleeBlow - Anton's call"
                        : "kick or shield-bash a few men (a tournament) and read it again";
                case SpikeLever.BlockStun:
                    return v == SpikeVerdict.Works ? "I4 lengthens the blocker's stun and the riposted attacker's recoil through CalculateDefendedBlowStunMultipliers"
                        : v == SpikeVerdict.Fails ? "the stun does not lengthen what the engine plays: I4's longer block stun and the riposte use a forced stagger (lever 4) instead"
                        : "block and get blocked more (a 50v50 gives hundreds) and read it again";
                case SpikeLever.SlowReaction:
                    return v == SpikeVerdict.Works ? "I3's longer reel and slower get-up = SetCurrentActionSpeed the tick after"
                        : v == SpikeVerdict.Fails ? "IMPACT §7 fallback 1: SetOverridenStrikeAndDeathAction (a per-man swap of the reaction the engine plays) for I3's reel / get-up"
                        : "kick a few men (4 kicks = one of each kind) and read it again";
                case SpikeLever.ForcedStagger:
                    return v == SpikeVerdict.Works ? "I4's rolled stagger = act_stagger_* on channel 0 at the speed for its length"
                        : v == SpikeVerdict.Fails ? "I4's stagger falls back to the longer block stun (lever 2) - no animation of ours"
                        : "fight a few blocks more than 10 s apart and read it again";
                case SpikeLever.ShieldDrop:
                    return v == SpikeVerdict.Works ? "I3 / I4 drop shields with DropItem queued, vanilla's way"
                        : v == SpikeVerdict.Fails ? "the queued drop does not happen: try DropItem from our tick, else RemoveEquippedWeapon (the shield gone, not on the ground) - Anton's call"
                        : "kick a man who holds a shield and read it again";
                case SpikeLever.OwnBlow:
                    return v == SpikeVerdict.Works ? "I4 knocks a blocker down with a blow of our own"
                        : v == SpikeVerdict.Fails ? "IMPACT §7 fallback 2: a crush-through (DecideCrushedThrough) knocks a blocker down instead"
                        : "fight a few blocks more than 20 s apart and read it again";
            }
            return string.Empty;
        }

        /// <summary>A refusal in the log's words.</summary>
        public static string RefusalName(ForceRefusal r) => r switch
        {
            ForceRefusal.Gone => "left the field",
            ForceRefusal.NotHuman => "a horse",
            ForceRefusal.Mounted => "a rider",
            ForceRefusal.GameObject => "using a siege engine / ladder",
            ForceRefusal.Water => "in water",
            ForceRefusal.Down => "already on the ground",
            _ => "none",
        };
    }

    /// <summary>
    /// Step 47: one reaction watched tick by tick - a pure state machine over the channel reads (<see cref="ActionRead"/>), so the rules
    /// of "it showed", "a new action of it", "it ended" and the length are unit-tested. A struct in the logic's fixed pool (no allocation).
    /// </summary>
    public struct ReactionWatch
    {
        public WatchKind Kind;

        /// <summary>The event's time (the blow, the block, our call).</summary>
        public double Since;

        /// <summary>It must show by then.</summary>
        public double Deadline;

        /// <summary>The channel it showed on (−1 = not yet).</summary>
        public int Channel;

        /// <summary>The action index now on that channel (a change within the kind = a new action of it).</summary>
        public int Index;

        /// <summary>The type it showed with (the summary names what a kick really did).</summary>
        public int FirstType;

        /// <summary>When it showed (the length runs from here; a Guard's from the block).</summary>
        public double SeenAt;

        /// <summary>A Stagger's own action index (what we started).</summary>
        public int Expect;

        public void Begin(WatchKind kind, double now, double appearWithin, int expectIndex = -1)
        {
            Kind = kind;
            Since = now;
            Deadline = now + appearWithin;
            Channel = -1;
            Index = -1;
            FirstType = -1;
            SeenAt = double.NaN;
            Expect = expectIndex;
        }

        public bool Appeared => Channel >= 0;

        /// <summary>One tick: <paramref name="length"/> is set on Ended / Capped (seconds; a Guard from the block, else from first seen).</summary>
        public WatchEvent Observe(double now, in ActionRead a, out double length)
        {
            length = 0;
            if (Channel < 0)
            {
                int ch = Match(in a);
                if (ch >= 0)
                {
                    Channel = ch;
                    Index = a.Index(ch);
                    FirstType = a.Type(ch);
                    SeenAt = now;
                    return WatchEvent.Appeared;
                }
                if (Kind == WatchKind.Guard || now >= Deadline) return WatchEvent.NeverAppeared; // a Guard is in his guard at once, or not at all
                return WatchEvent.None;
            }
            int type = a.Type(Channel), index = a.Index(Channel);
            double from = Kind == WatchKind.Guard ? Since : SeenAt;
            if (now - SeenAt >= SpikeMath.MeasureCapSeconds)
            {
                length = now - from;
                return WatchEvent.Capped;
            }
            if (index == Index && Fits(type)) return WatchEvent.None;
            if (index != Index && Fits(type) && Kind != WatchKind.Guard && Kind != WatchKind.Stagger)
            {
                Index = index; // the _continue of a fall, a second strike reaction: still the reaction
                return WatchEvent.NewAction;
            }
            length = now - from;
            return WatchEvent.Ended;
        }

        /// <summary>The channel a reaction of this kind shows on now (channel 0 first), or −1.</summary>
        private int Match(in ActionRead a)
        {
            switch (Kind)
            {
                case WatchKind.Stagger:
                    return a.Index0 == Expect && Expect >= 0 ? 0 : a.Index1 == Expect && Expect >= 0 ? 1 : -1;
                case WatchKind.Guard:
                    return ImpactActions.IsDefend(a.Type1) ? 1 : -1;
                default:
                    return Fits(a.Type0) ? 0 : Fits(a.Type1) ? 1 : -1;
            }
        }

        private bool Fits(int type) => Kind switch
        {
            WatchKind.Reaction => ImpactActions.IsStrike(type) || ImpactActions.IsFall(type),
            WatchKind.Fall => ImpactActions.IsFall(type),
            WatchKind.Recoil => ImpactActions.IsRecoil(type),
            WatchKind.Guard => ImpactActions.IsDefend(type),
            WatchKind.Stagger => ImpactActions.IsStrike(type),
            _ => false,
        };
    }
}

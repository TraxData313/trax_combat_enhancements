using System;
using System.Collections.Generic;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>Step 65: a kick or a shield bash (what the AI is asked to do).</summary>
    public enum AiKickKind : byte
    {
        None,
        Kick,
        Bash,
    }

    /// <summary>Step 65: where one AI man's kick attempt is (the input wish the hook writes).</summary>
    public enum AiKickPhase : byte
    {
        /// <summary>No attempt.</summary>
        None,

        /// <summary>A bash's lead: his guard held up (a direction bit) for <see cref="AiKickMath.BashLeadSeconds"/> - the engine's kick input
        /// while blocking is a bash.</summary>
        Guard,

        /// <summary>The kick input (<c>EventControlFlag.Kick</c>) pressed for <see cref="AiKickMath.PressSeconds"/>.</summary>
        Press,

        /// <summary>The press is over: his own attack / guard / event input held off and he stands still until the action has played out
        /// (or <see cref="AiKickMath.WatchSeconds"/> passed) - the native AI's own input would cancel it before the hit frame.</summary>
        Commit,
    }

    /// <summary>Step 65: why an AI man in a look did NOT try a kick (the summary's "why not" - in the order asked).</summary>
    public enum AiKickSkip : byte
    {
        /// <summary>Not his turn yet: the cooldown after his last one.</summary>
        Cooldown,

        /// <summary>His bar below <c>AiKickMinBarPercent</c>.</summary>
        Tired,

        /// <summary>Fewer Athletics points than the kick costs.</summary>
        NoAthletics,

        /// <summary>No stored swing (step 32 holds his kick while he waits).</summary>
        NoSwing,

        /// <summary>Reeling, staggered or on the ground.</summary>
        Staggered,

        /// <summary>In his own attack or another kick, or busy stepping back / walking back.</summary>
        Busy,

        /// <summary>The dice said no (the common case - counted, never logged).</summary>
        Dice,

        /// <summary>On a horse.</summary>
        Mounted,

        /// <summary>A bow / crossbow / throwing weapon in his hand.</summary>
        Ranged,

        /// <summary>Running away (a rout).</summary>
        Routing,

        /// <summary>No enemy he fights (no target, a friend, gone, on a horse, on the ground).</summary>
        NoTarget,

        /// <summary>His target beyond <c>AiKickReachMetres</c>.</summary>
        TooFar,

        /// <summary>His target outside <c>AiKickFacingDegrees</c> of his facing.</summary>
        NotFacing,

        /// <summary>The attempt pool is full (plumbing - <see cref="AiKickMath.MaxAttempts"/>).</summary>
        PoolFull,

        /// <summary>Step 66: decided, waiting for the enemy to come in reach / in front (<c>AiKickWaitSeconds</c>).</summary>
        Waiting,
    }

    /// <summary>Step 65: how an attempt ended.</summary>
    public enum AiKickEnd : byte
    {
        /// <summary>The engine played a kick / bash and it is over.</summary>
        Played,

        /// <summary>Nothing showed within <see cref="AiKickMath.WatchSeconds"/> - the engine ignored the input (or played it too briefly to be seen).</summary>
        NotPlayed,

        /// <summary>He was struck / staggered / knocked down meanwhile.</summary>
        Interrupted,

        /// <summary>He left the field, was taken over by the player, or got on a horse.</summary>
        Gone,

        /// <summary>The switch, Athletics or the master switch went off.</summary>
        SwitchedOff,

        /// <summary>The mission ended.</summary>
        MissionEnd,
    }

    /// <summary>
    /// Step 65 (Anton 2026-10-19: "make the AI do a few shield bashes and kicks now and then - the game has it but the NPCs never use them"):
    /// THE AI KICKS AND BASHES - the rules, read live. <see cref="Enabled"/> = the master switch FIRST, then AthleticsEnabled (a kick costs
    /// Athletics, and the kick is SEEN through the Athletics action poll), then <c>AiKicksEnabled</c>.
    /// </summary>
    public readonly struct AiKickRules
    {
        public AiKickRules(bool modEnabled, bool athleticsEnabled, bool enabled, double perMinute, double reachMetres, double facingDegrees, double cooldownSeconds,
            double turtleMultiplier, double turtleSeconds, double turtleBlocks, double feverMultiplier, int skillLowPercent, int skillHighPercent,
            int minBarPercent, int fullBarPercent, bool kicksARaisedShield, double waitSeconds = 0, double waitMetres = 0, int bashSharePercent = 0,
            double bashDelaySeconds = 1, double bashCooldownSeconds = 0)
        {
            ModEnabled = modEnabled;
            AthleticsEnabled = athleticsEnabled;
            KicksEnabled = enabled;
            PerMinute = Pos(perMinute);
            Reach = Pos(reachMetres);
            Facing = Math.Max(0, Math.Min(180, double.IsNaN(facingDegrees) ? 0 : facingDegrees));
            Cooldown = Pos(cooldownSeconds);
            Turtle = Math.Max(1, double.IsNaN(turtleMultiplier) ? 1 : turtleMultiplier);
            TurtleSeconds = Math.Max(0.1, double.IsNaN(turtleSeconds) ? 4 : turtleSeconds);
            TurtleBlocks = Math.Max(0.1, double.IsNaN(turtleBlocks) ? 2 : turtleBlocks);
            Fever = Math.Max(1, double.IsNaN(feverMultiplier) ? 1 : feverMultiplier);
            SkillLow = Math.Max(0, skillLowPercent) / 100.0;
            SkillHigh = Math.Max(0, skillHighPercent) / 100.0;
            MinBar = Math.Max(0, Math.Min(100, minBarPercent)) / 100.0;
            FullBar = Math.Max(0, Math.Min(100, fullBarPercent)) / 100.0;
            KicksARaisedShield = kicksARaisedShield;
            WaitSeconds = Pos(waitSeconds);
            WaitMetres = Pos(waitMetres);
            BashShare = Math.Max(0, bashSharePercent) / 100.0;
            BashDelay = Math.Max(0.1, double.IsNaN(bashDelaySeconds) ? 1 : bashDelaySeconds);
            BashCooldown = Pos(bashCooldownSeconds);
        }

        public static AiKickRules From(TraxSettings s) => new AiKickRules(s.ModEnabled, s.AthleticsEnabled, s.AiKicksEnabled, s.AiKicksPerMinute, s.AiKickReachMetres,
            s.AiKickFacingDegrees, s.AiKickCooldownSeconds, s.AiKickTurtleMultiplier, s.AiKickTurtleSeconds, s.AiKickTurtleBlocks, s.AiKickFeverMultiplier,
            s.AiKickSkillLowPercent, s.AiKickSkillHighPercent, s.AiKickMinBarPercent, s.AiKickFullBarPercent, s.AiKickKicksARaisedShield,
            s.AiKickWaitSeconds, s.AiKickWaitMetres, s.AiBashShareOfSwingsPercent, s.AiBashDelaySeconds, s.AiBashCooldownSeconds);

        public bool ModEnabled { get; }
        public bool AthleticsEnabled { get; }
        public bool KicksEnabled { get; }

        /// <summary>The AI kicks: the master switch FIRST, Athletics, AiKicksEnabled, and a kick rate or a bash share above 0 (step 66).</summary>
        public bool Enabled => ModEnabled && AthleticsEnabled && KicksEnabled && (PerMinute > 0 || BashShare > 0);

        public double PerMinute { get; }
        public double Reach { get; }
        public double Facing { get; }
        public double Cooldown { get; }
        public double Turtle { get; }
        public double TurtleSeconds { get; }
        public double TurtleBlocks { get; }
        public double Fever { get; }
        public double SkillLow { get; }
        public double SkillHigh { get; }
        public double MinBar { get; }
        public double FullBar { get; }
        public bool KicksARaisedShield { get; }

        /// <summary>Step 66: a decision taken with the enemy he fights within <see cref="WaitMetres"/> but not yet in reach / in front waits
        /// this long for it (0 = only an enemy already in reach).</summary>
        public double WaitSeconds { get; }
        public double WaitMetres { get; }

        /// <summary>Step 66: a shieldman's bashes per melee swing (AiBashShareOfSwingsPercent ÷ 100) - each swing earns this much of a bash.</summary>
        public double BashShare { get; }

        /// <summary>Step 66: the mean wait (s) from an earned bash to the bash at a full bar (÷ the bar factor as he tires).</summary>
        public double BashDelay { get; }

        /// <summary>Step 66: at least this long after a BASH before his next kick or bash (a kick keeps <see cref="Cooldown"/>).</summary>
        public double BashCooldown { get; }

        public string? OffBecause => !ModEnabled ? "ModEnabled" : !AthleticsEnabled ? "AthleticsEnabled" : !KicksEnabled ? "AiKicksEnabled"
            : !(PerMinute > 0) && !(BashShare > 0) ? "AiKicksPerMinute 0 and AiBashShareOfSwingsPercent 0" : null;

        private static double Pos(double v) => double.IsNaN(v) || v < 0 ? 0 : v;

        /// <summary>The settings in one sentence (the mission-start line, the summary).</summary>
        public string Describe()
        {
            if (!Enabled) return "OFF (" + OffBecause + ") - the game's AI, which never kicks";
            string kicks = PerMinute > 0
                ? "KICKS him (no shield in hand) at " + AiKickMath.F2(PerMinute) + " a minute of contact x his Athletics skill (" + AiKickMath.P0(SkillLow) + " at 0 → "
                  + AiKickMath.P0(SkillHigh) + " at 300) x his bar (none below " + AiKickMath.P0(MinBar) + ", full from " + AiKickMath.P0(FullBar) + ") x up to "
                  + AiKickMath.F1(Turtle) + " against a TURTLE (" + AiKickMath.F1(TurtleBlocks) + " blocks in the last ~" + AiKickMath.F1(TurtleSeconds)
                  + " s, his guard up now counting one) x " + AiKickMath.F1(Fever) + " in a fever, at least " + AiKickMath.F1(Cooldown) + " s between two"
                : "never kicks a man without a shield (AiKicksPerMinute 0)";
            string bashes = BashShare > 0
                ? "a SHIELDMAN bashes him " + AiKickMath.P0(BashShare) + " as often as he swings (each of his melee swings earns " + AiKickMath.F2(BashShare)
                  + " of a bash, at most " + AiKickMath.F0(AiKickMath.BashCreditCap) + " saved, spent within ~" + AiKickMath.F1(BashDelay) + " s at a full bar - slower as he tires, "
                  + "the same bar gate), at least " + AiKickMath.F1(BashCooldown) + " s after a bash"
                : "a shieldman never bashes (AiBashShareOfSwingsPercent 0)";
            return "ON - an AI man on foot within " + AiKickMath.F1(Reach) + " m of the enemy he fights (within " + AiKickMath.F0(Facing) + "° of his facing) " + kicks + "; " + bashes
                   + "; a decision with that enemy within " + AiKickMath.F1(WaitMetres) + " m but not yet in reach waits up to " + AiKickMath.F1(WaitSeconds)
                   + " s for it; never staggered, down, routing, mounted, with a ranged weapon, without the Athletics or a stored swing for it"
                   + (KicksARaisedShield ? "; a shieldman KICKS a raised shield facing him" : string.Empty)
                   + " - through the AI's own input (the engine's kick key; a bash = the kick key with the guard up)";
        }
    }

    /// <summary>Step 65 pure: the rate, its factors, the per-look chance, the kind, the phases' timing, the turtle memory.</summary>
    public static class AiKickMath
    {
        // ---- plumbing (documented, not settings - RBM's AiKickBash proven numbers, its AiModule/Agents/AiKickBash.cs)

        /// <summary>Each AI man is looked at about this often (staggered, the fever's / the brace's pattern).</summary>
        public const double LookSeconds = 0.25;

        /// <summary>A bash's lead: the guard held up this long before the kick key (RBM's BlockSeconds).</summary>
        public const double BashLeadSeconds = 0.4;

        /// <summary>The kick key held this long (RBM's KickSeconds).</summary>
        public const double PressSeconds = 0.15;

        /// <summary>His own input held off at most this long after the press, waiting for the action to play out (RBM's WatchSeconds).</summary>
        public const double WatchSeconds = 1.5;

        /// <summary>Attempts at once (a 1000-man battle has a few dozen at a time; a full pool refuses - counted).</summary>
        public const int MaxAttempts = 256; // step 66: shieldmen bash as often as they swing - a 1000-man battle can run ~100 at once

        /// <summary>The skill at which the skill factor reaches <c>AiKickSkillHighPercent</c>.</summary>
        public const double SkillCap = 300;

        /// <summary>A raised shield facing the kicker within this angle is kicked, not bashed (RBM's ShieldFacingCosine 0.5 = 60°).</summary>
        public const double RaisedShieldDegrees = 60;

        public const int SkipCount = 15;
        public const int EndCount = 6;

        /// <summary>His own action (channel 1) keeps him busy: a ready / release of a melee or ranged attack, a reload, a kick / bash, a weapon
        /// change (the engine's <c>ActionCodeType</c> 15-20, 28-31, 33-34) - taking his attack bits out mid-ready would RELEASE the blow (step 16).</summary>
        public static bool IsBusyAction(int action) => (action >= 15 && action <= 20) || (action >= 28 && action <= 31) || action == 33 || action == 34;

        /// <summary>The skill factor: low + (high − low) × min(1, skill ÷ 300).</summary>
        public static double SkillFactor(in AiKickRules r, double skill)
        {
            double s = double.IsNaN(skill) ? 0 : Math.Max(0, Math.Min(1, skill / SkillCap));
            return r.SkillLow + (r.SkillHigh - r.SkillLow) * s;
        }

        /// <summary>The tiredness factor by his bar: 0 below MinBar, 1 at or above FullBar, a straight line between (a FullBar at or below MinBar =
        /// a step at MinBar).</summary>
        public static double BarFactor(in AiKickRules r, double bar)
        {
            if (double.IsNaN(bar)) bar = 1;
            if (bar < r.MinBar) return 0;
            if (!(r.FullBar > r.MinBar)) return 1;
            return Math.Max(0, Math.Min(1, (bar - r.MinBar) / (r.FullBar - r.MinBar)));
        }

        /// <summary>The turtle share 0-1 of a target: (his remembered blocks + 1 if his guard is up now) ÷ TurtleBlocks, at most 1.</summary>
        public static double TurtleShare(in AiKickRules r, double recentBlocks, bool guardUpNow)
        {
            double b = (double.IsNaN(recentBlocks) || recentBlocks < 0 ? 0 : recentBlocks) + (guardUpNow ? 1 : 0);
            return Math.Max(0, Math.Min(1, b / r.TurtleBlocks));
        }

        /// <summary>The turtle factor: 1 + (Turtle − 1) × share.</summary>
        public static double TurtleFactor(in AiKickRules r, double share) => 1 + (r.Turtle - 1) * Math.Max(0, Math.Min(1, double.IsNaN(share) ? 0 : share));

        /// <summary>The kicks a minute for one man now (all factors in).</summary>
        public static double PerMinute(in AiKickRules r, double skill, double bar, bool fever, double turtleShare) =>
            r.PerMinute * SkillFactor(in r, skill) * BarFactor(in r, bar) * (fever ? r.Fever : 1) * TurtleFactor(in r, turtleShare);

        /// <summary>The chance of a kick in one look <paramref name="dt"/> seconds after his last: 1 − e^(−rate × dt) (the rate per second).</summary>
        public static double LookChance(double perMinute, double dt)
        {
            if (!(perMinute > 0) || !(dt > 0)) return 0;
            return 1 - Math.Exp(-perMinute / 60.0 * dt);
        }

        /// <summary>A look's time since the last: the gap, clamped to 0..4 looks (a longer gap = the feature was off).</summary>
        public static double LookGap(double now, double last)
        {
            if (double.IsNaN(last) || now < last) return LookSeconds;
            double g = now - last;
            return g > 4 * LookSeconds ? LookSeconds : g;
        }

        /// <summary>What he does: a shield in hand → a bash, unless the target's own shield is raised toward him and the rule says kick it;
        /// no shield → a kick.</summary>
        public static AiKickKind KindFor(in AiKickRules r, bool shieldInHand, bool targetShieldRaisedFacing) =>
            !shieldInHand ? AiKickKind.Kick : r.KicksARaisedShield && targetShieldRaisedFacing ? AiKickKind.Kick : AiKickKind.Bash;

        /// <summary>The first phase of an attempt: a bash holds the guard first, a kick presses at once.</summary>
        public static AiKickPhase FirstPhase(AiKickKind kind) => kind == AiKickKind.Bash ? AiKickPhase.Guard : AiKickPhase.Press;

        // ---- step 66: a shieldman's swings earn his bashes; a decision waits for reach

        /// <summary>The most bashes a man can have saved up (plumbing: a burst of swings does not become a burst of bashes).</summary>
        public const double BashCreditCap = 2;

        /// <summary>His bash credit after one of his melee swings: + the share, at most <see cref="BashCreditCap"/>.</summary>
        public static double AddBashCredit(in AiKickRules r, double credit)
        {
            double c = double.IsNaN(credit) || credit < 0 ? 0 : credit;
            return Math.Min(BashCreditCap, c + r.BashShare);
        }

        /// <summary>A shieldman's bash rate a minute now: with a whole bash earned, 60 × his bar factor ÷ <c>AiBashDelaySeconds</c> (the mean wait
        /// from the swing - longer as he tires, none below the bar gate); nothing earned = 0.</summary>
        public static double BashPerMinute(in AiKickRules r, double credit, double bar)
        {
            if (!(r.BashShare > 0) || !(credit >= 1 - 1e-9)) return 0;
            return 60.0 * BarFactor(in r, bar) / r.BashDelay;
        }

        /// <summary>The cooldown after an attempt of this kind (a bash its own, short one).</summary>
        public static double CooldownFor(in AiKickRules r, AiKickKind kind) => kind == AiKickKind.Bash ? r.BashCooldown : r.Cooldown;

        /// <summary>A rate a minute with a cooldown after each (from the ask): 1 ÷ (cooldown + 1 ÷ rate) a minute - what a man in steady contact
        /// really does (the model of the summary and the examples).</summary>
        public static double WithCooldown(double perMinute, double cooldownSeconds)
        {
            if (!(perMinute > 0)) return 0;
            return 1.0 / (Math.Max(0, cooldownSeconds) / 60.0 + 1.0 / perMinute);
        }

        /// <summary>Does a decision with the enemy at <paramref name="distance"/> m (out of reach or not in front) wait for reach?</summary>
        public static bool Waits(in AiKickRules r, double distance) => r.WaitSeconds > 0 && distance <= r.WaitMetres;

        /// <summary>The end of a phase begun at <paramref name="start"/>.</summary>
        public static double PhaseEnd(AiKickPhase phase, double start) => phase switch
        {
            AiKickPhase.Guard => start + BashLeadSeconds,
            AiKickPhase.Press => start + PressSeconds,
            AiKickPhase.Commit => start + WatchSeconds,
            _ => start,
        };

        /// <summary>The next phase once one ends (Guard → Press → Commit → None).</summary>
        public static AiKickPhase Next(AiKickPhase phase) => phase switch
        {
            AiKickPhase.Guard => AiKickPhase.Press,
            AiKickPhase.Press => AiKickPhase.Commit,
            _ => AiKickPhase.None,
        };

        /// <summary>
        /// One frame of an attempting man's input (the engine's bit values - <see cref="AiInputMath"/>'s): every phase takes his own attack and
        /// guard bits out and his own event flags (a weapon switch, a jump - anything would cancel the kick); Guard = DefendUp (a direction: the
        /// guard raised, the bash's context); Press = the kick event bit (+ DefendUp for a bash); Commit = nothing more (he stands still -
        /// <paramref name="stopMoving"/>). Returns the new movement flags; <paramref name="eventFlags"/> rewritten.
        /// </summary>
        public static uint Frame(AiKickPhase phase, AiKickKind kind, uint movementFlags, ref uint eventFlags, out bool stopMoving)
        {
            stopMoving = phase == AiKickPhase.Commit;
            if (phase == AiKickPhase.None) return movementFlags;
            uint f = movementFlags & ~(AiInputMath.AttackMask | AiInputMath.DefendMask);
            eventFlags = 0;
            bool bash = kind == AiKickKind.Bash;
            if (bash && phase != AiKickPhase.Commit) f |= DefendUp;
            if (phase == AiKickPhase.Press) eventFlags = KickEventBit;
            return f;
        }

        /// <summary><c>MovementControlFlag.DefendUp</c> (the smoke checks it).</summary>
        public const uint DefendUp = 0x1000;

        /// <summary><c>EventControlFlag.Kick</c> (the smoke checks it).</summary>
        public const uint KickEventBit = 0x8000;

        /// <summary>The turtle memory: <paramref name="value"/> blocks as of <paramref name="at"/>, faded to <paramref name="now"/> (e^(−dt ÷ τ)).</summary>
        public static double Faded(double value, double at, double now, double tau)
        {
            if (!(value > 0) || double.IsNaN(at)) return 0;
            double dt = now - at;
            if (dt <= 0) return value;
            return value * Math.Exp(-dt / Math.Max(0.1, tau));
        }

        /// <summary>One more block on him at <paramref name="now"/>: the memory faded to now + 1.</summary>
        public static void NoteBlock(ref double value, ref double at, double now, double tau)
        {
            value = Faded(value, at, now, tau) + 1;
            at = now;
        }

        public static string SkipName(AiKickSkip s) => s switch
        {
            AiKickSkip.Cooldown => "cooldown",
            AiKickSkip.Tired => "too tired",
            AiKickSkip.NoAthletics => "no Athletics for it",
            AiKickSkip.NoSwing => "no stored swing",
            AiKickSkip.Staggered => "staggered / down",
            AiKickSkip.Busy => "busy (attacking / kicking / stepping back)",
            AiKickSkip.Dice => "the dice",
            AiKickSkip.Mounted => "mounted",
            AiKickSkip.Ranged => "a ranged weapon in hand",
            AiKickSkip.Routing => "routing",
            AiKickSkip.NoTarget => "no enemy target",
            AiKickSkip.TooFar => "target out of reach",
            AiKickSkip.NotFacing => "target not in front",
            AiKickSkip.Waiting => "waiting for reach",
            _ => "the attempt pool full",
        };

        public static string EndName(AiKickEnd e) => e switch
        {
            AiKickEnd.Played => "played",
            AiKickEnd.NotPlayed => "NOT played (nothing showed within 1.5 s)",
            AiKickEnd.Interrupted => "interrupted (struck / staggered)",
            AiKickEnd.Gone => "gone (left / taken over / mounted)",
            AiKickEnd.SwitchedOff => "switched off",
            _ => "mission end",
        };

        public static string KindName(AiKickKind k) => k == AiKickKind.Bash ? "shield bash" : "kick";

        internal static string F0(double v) => v.ToString("0", CultureInfo.InvariantCulture);

        internal static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        internal static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        internal static string P0(double share) => (100 * share).ToString("0", CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>
    /// Step 65 per mission: the AI's kicks / bashes - the looks (AI men on foot, their contact time), the asks by kind and by reason (a turtle
    /// target, in a fever, plain), PLAYED (the engine showed the action) → LANDED (on the man / into a shield / on nothing), the ends, why not
    /// by reason, the Athletics they cost, the look's cost + the [summary] "ai kicks" lines. Main thread.
    /// </summary>
    public sealed class AiKickStats
    {
        /// <summary>The reason columns: against a turtle (share ≥ 0.5), in a fever, both, plain.</summary>
        public const int ReasonCount = 4;

        private readonly int[,] _asked = new int[3, ReasonCount];
        private readonly int[,] _played = new int[3, ReasonCount];
        private readonly int[,] _landed = new int[3, ReasonCount];
        private readonly int[,] _blocked = new int[3, ReasonCount];
        public readonly int[] Skips = new int[AiKickMath.SkipCount];
        public readonly int[] Ends = new int[AiKickMath.EndCount];

        /// <summary>Looks at AI men (on foot, the rules on); the man-seconds looked at while IN CONTACT (the fever's contact - an enemy within
        /// <c>FeverContactMetres</c> - when the fever's look runs).</summary>
        public long Looks;
        public double ContactManSeconds;

        /// <summary>Played as the other kind (asked a bash, the engine kicked - or the other way round).</summary>
        public int PlayedOtherKind;

        /// <summary>Step 66: the shieldmen's melee swings (AI, on foot) that earned bash credit, the credit earned, the credit lost at the cap.</summary>
        public int ShieldSwings;
        public double CreditEarned;
        public double CreditLost;

        /// <summary>Step 66: decisions that waited for reach - started, kicked / bashed after it (their wait), ran out, dropped (the enemy gone or
        /// beyond <c>AiKickWaitMetres</c>, he got on a horse / took a ranged weapon / broke).</summary>
        public int WaitsStarted;
        public int WaitsDone;
        public double WaitSeconds;
        public int WaitsExpired;
        public int WaitsDropped;

        /// <summary>Athletics spent on the AI's kicks (points), the native reads made, the look's cost (ms).</summary>
        public double PointsSpent;
        public int NativeReads;
        public double CostMs;
        public double PlayedDelay;
        public int PlayedDelayCount;

        public static int Reason(bool turtle, bool fever) => turtle && fever ? 2 : turtle ? 0 : fever ? 1 : 3;

        public static string ReasonName(int r) => r switch
        {
            0 => "vs a turtle",
            1 => "in a fever",
            2 => "turtle + fever",
            _ => "plain",
        };

        public void Asked(AiKickKind k, int reason) => _asked[(int)k, reason]++;

        public void Played(AiKickKind k, int reason, double delay)
        {
            _played[(int)k, reason]++;
            if (delay >= 0)
            {
                PlayedDelay += delay;
                PlayedDelayCount++;
            }
        }

        public void Landed(AiKickKind k, int reason, bool intoShield)
        {
            if (intoShield) _blocked[(int)k, reason]++;
            else _landed[(int)k, reason]++;
        }

        public int AskedCount(AiKickKind k) => Sum(_asked, k);
        public int PlayedCount(AiKickKind k) => Sum(_played, k);
        public int LandedCount(AiKickKind k) => Sum(_landed, k);
        public int BlockedCount(AiKickKind k) => Sum(_blocked, k);
        public int AskedFor(int reason) => _asked[1, reason] + _asked[2, reason];

        private static int Sum(int[,] a, AiKickKind k)
        {
            int n = 0;
            for (int r = 0; r < ReasonCount; r++) n += a[(int)k, r];
            return n;
        }

        public IEnumerable<string> SummaryLines(in AiKickRules r)
        {
            var lines = new List<string>();
            var c = CultureInfo.InvariantCulture;
            int asked = AskedCount(AiKickKind.Kick) + AskedCount(AiKickKind.Bash);
            int played = PlayedCount(AiKickKind.Kick) + PlayedCount(AiKickKind.Bash);
            int landed = LandedCount(AiKickKind.Kick) + LandedCount(AiKickKind.Bash);
            int blocked = BlockedCount(AiKickKind.Kick) + BlockedCount(AiKickKind.Bash);
            double minutes = ContactManSeconds / 60.0;
            lines.Add("ai kicks (step 65): " + r.Describe());
            lines.Add("ai kicks: asked " + asked + " (kicks " + AskedCount(AiKickKind.Kick) + ", bashes " + AskedCount(AiKickKind.Bash) + ") → PLAYED " + played
                      + (asked > 0 ? " (" + (100.0 * played / asked).ToString("0", c) + "%)" : string.Empty) + (PlayedDelayCount > 0 ? ", seen " + (PlayedDelay / PlayedDelayCount).ToString("0.00", c) + " s after the ask" : string.Empty)
                      + " → landed on the man " + landed + ", into a shield " + blocked + (PlayedOtherKind > 0 ? "; played as the other kind " + PlayedOtherKind : string.Empty)
                      + "; " + (minutes > 0 ? (asked / minutes).ToString("0.00", c) + " asked a man-minute in contact (" + minutes.ToString("0.0", c) + " man-minutes)" : "no man-minute in contact measured (the fever's look off)")
                      + "; Athletics spent on them " + PointsSpent.ToString("0", c));
            var sb = new System.Text.StringBuilder("ai kicks by reason (asked → played → landed / into a shield): ");
            for (int k = 0; k < ReasonCount; k++)
            {
                if (k > 0) sb.Append(" | ");
                int a = _asked[1, k] + _asked[2, k], p = _played[1, k] + _played[2, k], l = _landed[1, k] + _landed[2, k], b = _blocked[1, k] + _blocked[2, k];
                sb.Append(ReasonName(k)).Append(' ').Append(a).Append(" → ").Append(p).Append(" → ").Append(l).Append(" / ").Append(b);
            }
            lines.Add(sb.ToString());
            sb.Clear().Append("ai kicks - ends: ");
            for (int e = 0; e < AiKickMath.EndCount; e++)
            {
                if (e > 0) sb.Append(", ");
                sb.Append(AiKickMath.EndName((AiKickEnd)e)).Append(' ').Append(Ends[e]);
            }
            sb.Append("; why not at a look (").Append(Looks).Append(" looks): ");
            bool first = true;
            for (int s = 0; s < AiKickMath.SkipCount; s++)
            {
                if (Skips[s] == 0) continue;
                if (!first) sb.Append(", ");
                first = false;
                sb.Append(AiKickMath.SkipName((AiKickSkip)s)).Append(' ').Append(Skips[s]);
            }
            if (first) sb.Append("none");
            sb.Append("; native reads ").Append(NativeReads).Append(", the look's cost ").Append(CostMs.ToString("0.000", c)).Append(" ms");
            lines.Add(sb.ToString());
            int bashes = AskedCount(AiKickKind.Bash);
            lines.Add("ai kicks - bashes per swing and the wait for reach (step 66): the shieldmen's melee swings (AI, on foot) " + ShieldSwings + " earned "
                      + CreditEarned.ToString("0.0", c) + " bashes (AiBashShareOfSwingsPercent " + AiKickMath.P0(r.BashShare) + "; lost at the cap of "
                      + AiKickMath.F0(AiKickMath.BashCreditCap) + " " + CreditLost.ToString("0.0", c) + ") → bashes asked " + bashes
                      + (ShieldSwings > 0 ? " = " + ((double)bashes / ShieldSwings).ToString("0.00", c) + " a swing" : string.Empty)
                      + "; decisions that waited for reach " + WaitsStarted + " → kicked / bashed " + WaitsDone
                      + (WaitsDone > 0 ? " (" + (WaitSeconds / WaitsDone).ToString("0.00", c) + " s after the decision)" : string.Empty)
                      + ", ran out (" + AiKickMath.F1(r.WaitSeconds) + " s) " + WaitsExpired + ", dropped (the enemy gone or beyond " + AiKickMath.F1(r.WaitMetres)
                      + " m, mounted, a ranged weapon, routing) " + WaitsDropped);
            return lines;
        }
    }
}

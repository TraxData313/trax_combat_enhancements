using System;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>Step 106: where the red arrow points at - the side of the screen the opening is on (attacker-relative).</summary>
    public enum OpeningArrow : byte
    {
        None,

        /// <summary>At the top, pointing down - his balance went low: your overhead.</summary>
        Top,

        /// <summary>At the bottom, pointing up - his balance went high: your thrust from below.</summary>
        Bottom,

        /// <summary>On the left - your swing from your left (the engine's AttackLeft).</summary>
        Left,

        /// <summary>On the right - your swing from your right (the engine's AttackRight).</summary>
        Right,
    }

    /// <summary>Step 106a2: why a correct block of your attack put him off balance nowhere.</summary>
    public enum OpeningSkip : byte
    {
        None,

        /// <summary>You or he rides (on foot only).</summary>
        Mounted,

        /// <summary>No other guard opens an attack your weapon can make.</summary>
        NoOpeningForWeapon,
    }

    /// <summary>Step 106a2 (Toni's revised design): how a chain began or grew - the three entries of the one chain.</summary>
    public enum OpeningEntry : byte
    {
        /// <summary>Your attack blocked correctly by his WEAPON (a parry) - an unshielded man.</summary>
        Blocked,

        /// <summary>Your attack blocked correctly by his SHIELD - the side is shown, but his shield can still block it until link <c>OpeningShieldMinChain</c>.</summary>
        ShieldBlocked,

        /// <summary>Your hit landed in FRONT on a man not guarding - off balance, the window x <c>OpeningUnguardedWindowPercent</c>.</summary>
        FrontUnguarded,

        /// <summary>Your hit landed from BEHIND - always a stagger and a perfect strike (<c>OpeningBackHitStaggers</c>).</summary>
        Back,

        /// <summary>Step 107: your winning strike on a man HOLDING a forced block (<see cref="OpeningHold"/>).</summary>
        Hold,
    }

    /// <summary>Step 106a2: where the chain stands.</summary>
    public enum OpeningPhase : byte
    {
        /// <summary>No chain.</summary>
        None,

        /// <summary>Off balance: the balance side flashes; a follow-up into it in time beats his block, any landed hit staggers him.</summary>
        OffBalance,

        /// <summary>He reels: the PERFECT STRIKE side is shown for the whole reel.</summary>
        Reeling,
    }

    /// <summary>Step 106a2: what a landed hit of yours did to the chain.</summary>
    public enum OpeningHit : byte
    {
        /// <summary>Nothing (a guarded front hit on a man in no chain, the feature off for it).</summary>
        Nothing,

        /// <summary>He staggers: off balance, or from behind - the perfect strike opens.</summary>
        Staggers,

        /// <summary>A front hit on a man not guarding: off balance now (the longer window).</summary>
        OffBalance,

        /// <summary>A hit on the reeling man from the shown side: PERFECT STRIKE.</summary>
        FinishPerfect,

        /// <summary>A hit on the reeling man from another side: step 105's knockdown roll, no bonus.</summary>
        FinishOther,

        /// <summary>Step 106e: a back hit / an unguarded front hit on a man still reeling or inside his stagger immunity - no new opening (the
        /// chain untouched): what keeps a man from being re-staggered by every other hit.</summary>
        Immune,
    }

    /// <summary>
    /// Step 106f (Toni's first playtest: the wind-up's direction and the contact's differed 14 of 33): WHERE a follow-up's direction was judged.
    /// The swing is only COMMITTED at its release - the engine's ready can still change direction (a re-ready, a late mouse flick) - so the
    /// direction is read from the release; the wind-up only when the swing was already released when it was seen (one poll), the contact's
    /// collision when no release was seen before it (then the live block roll is too late to matter).
    /// </summary>
    public enum OpeningJudgedAt : byte
    {
        WindUp,
        Release,
        Contact,
        Count,
    }

    /// <summary>Step 106: what came of one off-balance state - the DIRECTION of your next swing (its timing is a score, <see cref="OpeningMath.TimingScore"/>).</summary>
    public enum OpeningVerdict : byte
    {
        /// <summary>Your swing went to the balance side (the arrow's).</summary>
        Opened,

        /// <summary>Another direction.</summary>
        WrongDirection,

        /// <summary>The swing's direction could not be read.</summary>
        UnknownDirection,

        /// <summary>No swing of yours followed (the chain's gap ran out, the chain reset first, the mission ended).</summary>
        NoSwing,
    }

    /// <summary>Step 106: why the chain started over.</summary>
    public enum OpeningReset : byte
    {
        None,

        /// <summary>The reel ended (finished, or no hit came during it).</summary>
        ReelOver,

        /// <summary>More than <c>OpeningChainResetSeconds</c> since the last link.</summary>
        Gap,

        /// <summary>His blow landed on you.</summary>
        HeHitYou,

        /// <summary>Your blow (or block) met someone else.</summary>
        TargetSwitch,

        /// <summary>A missed swing, with <c>OpeningMissKeepsChain</c> off.</summary>
        Miss,

        /// <summary>The feature or the master switch off, the man gone.</summary>
        Off,
    }

    /// <summary>
    /// Step 106 - OPENINGS (Toni 2026-11-09, REVISED the same day; AI_NOTES "Step 106", DESIGN decision 95): the settings, read live.
    /// <see cref="Enabled"/> = the master switch FIRST, then <c>OpeningsEnabled</c> (the live parts - none built yet, the switch ships OFF);
    /// <see cref="ShadowOn"/> = the master switch, then <c>OpeningsShadowLog</c>. The perfect window reads the timed block's own window by skill.
    /// </summary>
    public readonly struct OpeningRules
    {
        public OpeningRules(TraxSettings s)
        {
            ModEnabled = s.ModEnabled;
            Switch = s.OpeningsEnabled;
            ShadowLog = s.OpeningsShadowLog;
            FlashDelay = Pos(s.OpeningFlashDelaySeconds);
            PerfectScale = Pos(s.OpeningPerfectScalePercent / 100.0);
            WindowBase = Pos(s.OpeningWindowSeconds);
            WindowPerLink = Pos(s.OpeningWindowPerLinkSeconds);
            UnguardedWindow = Math.Max(1, Pos(s.OpeningUnguardedWindowPercent / 100.0));
            BlockPerLink = Clamp01(s.OpeningBlockPerLinkPercent / 100.0);
            ChainMax = Math.Max(1, Math.Min(OpeningMath.MaxChain, s.OpeningChainMax));
            ChainReset = Pos(s.OpeningChainResetSeconds);
            MissKeepsChain = s.OpeningMissKeepsChain;
            HoldMax = Pos(s.OpeningHoldMaxSeconds);
            SidesCrossed = s.OpeningSidesCrossed;
            ReelBase = Pos(s.OpeningReelBaseSeconds);
            ReelPerChain = Pos(s.OpeningReelPerChainSeconds);
            ReelMax = Pos(s.OpeningReelMaxSeconds);
            ShieldMinChain = Math.Max(1, s.OpeningShieldMinChain);
            BackHitStaggers = s.OpeningBackHitStaggers;
            UnguardedHitOpens = s.OpeningUnguardedHitOpens;
            ArrowShow = s.OpeningArrowShow;
            AiVsAi = s.OpeningAiVsAi;
            FourMin = PerfectStrikeMath.ClampMultiplier(s.PerfectStrikeFourWayMin);
            FourMax = Math.Max(FourMin, PerfectStrikeMath.ClampMultiplier(s.PerfectStrikeFourWayMax));
            ThreeMin = PerfectStrikeMath.ClampMultiplier(s.PerfectStrikeThreeWayMin);
            ThreeMax = Math.Max(ThreeMin, PerfectStrikeMath.ClampMultiplier(s.PerfectStrikeThreeWayMax));
            TwoMin = PerfectStrikeMath.ClampMultiplier(s.PerfectStrikeTwoWayMin);
            TwoMax = Math.Max(TwoMin, PerfectStrikeMath.ClampMultiplier(s.PerfectStrikeTwoWayMax));
            WeaponFinishes = s.PerfectStrikeWeaponFinishes;
            Timed = TimedBlockRules.From(s);
        }

        public static OpeningRules From(TraxSettings s) => new OpeningRules(s);

        public bool ModEnabled { get; }
        public bool Switch { get; }
        public bool ShadowLog { get; }

        /// <summary>The live openings (106b-106d): the master switch first, then <c>OpeningsEnabled</c>.</summary>
        public bool Enabled => ModEnabled && Switch;

        /// <summary>The shadow log runs: the master switch first, then <c>OpeningsShadowLog</c>.</summary>
        public bool ShadowOn => ModEnabled && ShadowLog;

        /// <summary>Step 106b: the chain is followed at all - the master switch first, then the shadow log OR the live switch (the live parts
        /// need the chain even with the shadow log off).</summary>
        public bool Runs => ModEnabled && (ShadowLog || Switch);

        /// <summary>The flash after his block (s) - the PERFECT moment to start your follow-up.</summary>
        public double FlashDelay { get; }

        /// <summary>The perfect half-width = the timed block's perfect window at your weapon skill x this.</summary>
        public double PerfectScale { get; }

        /// <summary>The window's end after the flash (s) at link 1 - the score falls to 0 there; + <see cref="WindowPerLink"/> a link.</summary>
        public double WindowBase { get; }
        public double WindowPerLink { get; }

        /// <summary>The window x this after a front hit on a man not guarding (1.5).</summary>
        public double UnguardedWindow { get; }

        /// <summary>His block chance is cut by this share per link beyond the first (0.2: x1 / 0.8 / 0.6 / 0.4).</summary>
        public double BlockPerLink { get; }

        public int ChainMax { get; }
        public double ChainReset { get; }
        public bool MissKeepsChain { get; }
        public double HoldMax { get; }
        public bool SidesCrossed { get; }
        public double ReelBase { get; }
        public double ReelPerChain { get; }
        public double ReelMax { get; }

        /// <summary>A SHIELD can still block the balance side below this link (2: the first shield block only shows the side).</summary>
        public int ShieldMinChain { get; }

        public bool BackHitStaggers { get; }
        public bool UnguardedHitOpens { get; }
        public bool ArrowShow { get; }
        public bool AiVsAi { get; }

        /// <summary>The PERFECT STRIKE's range by the striker's weapon's attack directions: 4 (swords), 3 (no thrust - axes, maces), 2 (spears).</summary>
        public double FourMin { get; }
        public double FourMax { get; }
        public double ThreeMin { get; }
        public double ThreeMax { get; }
        public double TwoMin { get; }
        public double TwoMax { get; }

        /// <summary>The finishing side follows the weapon: the opposite when the weapon can make it, else any of its other directions.</summary>
        public bool WeaponFinishes { get; }

        /// <summary>The timed block's rules - the perfect window by skill.</summary>
        public TimedBlockRules Timed { get; }

        public string? OffBecause => !ModEnabled ? "ModEnabled" : !Switch ? "OpeningsEnabled" : null;

        public string Describe()
        {
            var c = CultureInfo.InvariantCulture;
            return "openings (revised design) " + (Enabled ? "ON" : "OFF (" + OffBecause + ")") + (ShadowOn ? ", the shadow log ON" : ", the shadow log OFF")
                   + ": a chain of up to " + ChainMax.ToString(c) + " links on one man (reset after " + ChainReset.ToString("0.0#", c) + " s"
                   + (MissKeepsChain ? ", a miss keeps it" : ", a miss resets it") + "); off balance to one of his other 3 guards ("
                   + (SidesCrossed ? "sides crossed" : "sides same-named") + "), the flash " + FlashDelay.ToString("0.00", c) + " s after his block, perfect = the timed block's window x"
                   + PerfectScale.ToString("0.##", c) + ", the window " + WindowBase.ToString("0.00", c) + " + " + WindowPerLink.ToString("0.00", c) + " x (k-1) s (x"
                   + UnguardedWindow.ToString("0.0#", c) + " after an unguarded front hit); his block chance (1 - score) x (1 - " + BlockPerLink.ToString("0.00", c)
                   + " x (k-1)), a shield blocks as usual before link " + ShieldMinChain.ToString(c) + "; the reel " + ReelBase.ToString("0.0#", c) + " + "
                   + ReelPerChain.ToString("0.0#", c) + " x (k-1), at most " + ReelMax.ToString("0.0#", c) + " s; back hits " + (BackHitStaggers ? "stagger" : "do nothing")
                   + ", unguarded front hits " + (UnguardedHitOpens ? "put him off balance" : "do nothing") + "; PERFECT STRIKE x" + FourMin.ToString("0.0#", c) + "-"
                   + FourMax.ToString("0.0#", c) + " / " + ThreeMin.ToString("0.0#", c) + "-" + ThreeMax.ToString("0.0#", c) + " / " + TwoMin.ToString("0.0#", c) + "-"
                   + TwoMax.ToString("0.0#", c) + " (4 / 3 / 2 directions)" + (WeaponFinishes ? ", the finishing side by the weapon" : ", strict opposites")
                   + "; " + (AiVsAi ? "AI vs AI too" : "you only") + ", on foot";
        }

        private static double Pos(double v) => double.IsNaN(v) ? 0 : Math.Max(0, Math.Round(v, 5));

        private static double Clamp01(double v) => double.IsNaN(v) ? 0 : Math.Max(0, Math.Min(1, v));
    }

    /// <summary>
    /// Step 106a2: one OFF-BALANCE state as the rules plan it (a correct block of your attack, or your landed front hit on a man not guarding):
    /// where his balance went (the guard), the attack it opens and the arrow, the perfect half-width, the window, whether his shield still holds
    /// the side, the reel a hit would give. <see cref="Skip"/> None = a state.
    /// </summary>
    public struct OpeningPlan
    {
        public int K;
        public bool Shield;
        public OpeningEntry Entry;
        public OpeningSkip Skip;

        /// <summary>The guard that blocked you (read at contact, or derived from your attack) - <see cref="OpeningMath.Unknown"/> if neither.</summary>
        public int PreviousGuard;

        /// <summary>The balance guard (an engine <c>UsageDirection</c> 4-7) and the attack it opens (0-3); <see cref="OpeningMath.Unknown"/> = none.</summary>
        public int Guard, Opening;

        public OpeningArrow Arrow;

        /// <summary>The flash after the block (s), the perfect half-width around it (s), the window's end after the flash (s), the reel a hit gives (s).</summary>
        public double Flash, Perfect, Window, Reel;

        /// <summary>His shield still blocks the side as usual (a shield below <c>OpeningShieldMinChain</c>).</summary>
        public bool ShieldHolds;

        /// <summary>The die that picked the guard (NaN = none rolled).</summary>
        public double Die;

        /// <summary>How many guards it could have been.</summary>
        public int Candidates;

        public bool Opens => Skip == OpeningSkip.None && Opening != OpeningMath.Unknown;
    }

    /// <summary>
    /// Step 106 - OPENINGS, the pure math (AI_NOTES "Step 106", DESIGN decision 95 - Toni's REVISED design). Directions are the engine's
    /// <c>Agent.UsageDirection</c>: AttackUp 0 (overhead), AttackDown 1 (thrust from below), AttackLeft 2, AttackRight 3, DefendUp 4,
    /// DefendDown 5, DefendLeft 6, DefendRight 7. A guard opens the attack OPPOSITE to the one it blocks (a low guard - your overhead; a high
    /// guard - your thrust; the sides by <see cref="OpeningRules.SidesCrossed"/>). No allocation.
    /// </summary>
    public static class OpeningMath
    {
        public const int Unknown = -1;
        public const int AttackUp = 0, AttackDown = 1, AttackLeft = 2, AttackRight = 3;
        public const int GuardUp = 4, GuardDown = 5, GuardLeft = 6, GuardRight = 7;

        /// <summary>Step 106e: was he reeling / stagger-immune BEFORE this landed hit? <paramref name="immuneNow"/> is read after the impact decision
        /// wrote his record, so a stagger that decision rolled (<paramref name="decisionStaggered"/>) proves he was not (it refuses a stagger inside
        /// the immunity).</summary>
        public static bool ImmuneBeforeHit(bool immuneNow, bool decisionStaggered) => immuneNow && !decisionStaggered;

        /// <summary>
        /// Step 106e (decided in the review - AI_NOTES "106e - review"): can the impact system CARRY a live opening? The stagger is played through
        /// its riposte path (the riposte family LIVE - not shadow, not the spike) and the landed hit is only seen when its hits are decided. When it
        /// cannot, the openings run as a SHADOW even with <c>OpeningsEnabled</c> on - no forced guard, no lowered shield, no GUARD BROKEN, no flash -
        /// rather than half of them live with a stagger that can never come.
        /// </summary>
        public static bool ImpactCarries(in ImpactRules ir) => ir.Applies(ImpactFamily.Riposte) && ir.Decides(ImpactFamily.Hits);

        /// <summary>The longest chain the settings may ask (the stats' arrays are this long + 1).</summary>
        public const int MaxChain = 8;

        /// <summary>Every attack direction (the weapon mask when its capabilities are unknown).</summary>
        public const int AllAttacks = 0xF;

        public static bool IsAttack(int d) => d >= AttackUp && d <= AttackRight;

        public static bool IsGuard(int d) => d >= GuardUp && d <= GuardRight;

        public static int Bit(int attack) => IsAttack(attack) ? 1 << attack : 0;

        /// <summary>
        /// The attacks a weapon can make: a swing gives the overhead and both sides, a thrust the attack from below; a THRUST-ONLY weapon (a spear)
        /// = its two stabs, the overhead stab (AttackUp) and the thrust (AttackDown); neither known = all. 4 directions = a sword, 3 = no thrust
        /// (axes, maces), 2 = a spear.
        /// </summary>
        public static int WeaponMask(bool swing, bool thrust)
        {
            if (swing && thrust) return AllAttacks;
            if (swing) return Bit(AttackUp) | Bit(AttackLeft) | Bit(AttackRight);
            if (thrust) return Bit(AttackUp) | Bit(AttackDown);
            return AllAttacks;
        }

        /// <summary>How many attack directions a mask holds (0-4).</summary>
        public static int DirectionCount(int mask)
        {
            int n = 0;
            for (int d = AttackUp; d <= AttackRight; d++)
                if ((mask & Bit(d)) != 0) n++;
            return n;
        }

        /// <summary>The opposite attack: overhead ↔ thrust from below, left ↔ right.</summary>
        public static int Opposite(int attack) => attack switch
        {
            AttackUp => AttackDown,
            AttackDown => AttackUp,
            AttackLeft => AttackRight,
            AttackRight => AttackLeft,
            _ => Unknown,
        };

        /// <summary>The guard that blocks <paramref name="attack"/>: the vertical pairs by name; the sides crossed or same-named.</summary>
        public static int GuardBlocking(int attack, bool sidesCrossed) => attack switch
        {
            AttackUp => GuardUp,
            AttackDown => GuardDown,
            AttackLeft => sidesCrossed ? GuardRight : GuardLeft,
            AttackRight => sidesCrossed ? GuardLeft : GuardRight,
            _ => Unknown,
        };

        /// <summary>The attack <paramref name="guard"/> blocks (the inverse of <see cref="GuardBlocking"/>).</summary>
        public static int AttackBlockedBy(int guard, bool sidesCrossed) => guard switch
        {
            GuardUp => AttackUp,
            GuardDown => AttackDown,
            GuardLeft => sidesCrossed ? AttackRight : AttackLeft,
            GuardRight => sidesCrossed ? AttackLeft : AttackRight,
            _ => Unknown,
        };

        /// <summary>THE MAPPING: the attack a balance <paramref name="guard"/> leaves open - the one opposite to what it blocks. Crossed sides:
        /// DefendDown → AttackUp, DefendUp → AttackDown, DefendLeft → AttackLeft, DefendRight → AttackRight.</summary>
        public static int OpeningFor(int guard, bool sidesCrossed) => Opposite(AttackBlockedBy(guard, sidesCrossed));

        /// <summary>Where the arrow shows for an attack (attacker-relative).</summary>
        public static OpeningArrow ArrowFor(int attack) => attack switch
        {
            AttackUp => OpeningArrow.Top,
            AttackDown => OpeningArrow.Bottom,
            AttackLeft => OpeningArrow.Left,
            AttackRight => OpeningArrow.Right,
            _ => OpeningArrow.None,
        };

        /// <summary>The guard that just blocked you: the one read at contact if it is a guard, else the one that blocks your attack, else unknown.</summary>
        public static int PreviousGuard(int guardAtContact, int yourAttack, bool sidesCrossed) =>
            IsGuard(guardAtContact) ? guardAtContact : GuardBlocking(yourAttack, sidesCrossed);

        /// <summary>A guard his balance may go to: not the one he holds, and its opening is an attack your weapon can make.</summary>
        public static bool Candidate(int guard, int previous, int weaponMask, bool sidesCrossed) =>
            IsGuard(guard) && guard != previous && (Bit(OpeningFor(guard, sidesCrossed)) & weaponMask) != 0;

        public static int CandidateCount(int previous, int weaponMask, bool sidesCrossed)
        {
            int n = 0;
            for (int g = GuardUp; g <= GuardRight; g++)
                if (Candidate(g, previous, weaponMask, sidesCrossed)) n++;
            return n;
        }

        /// <summary>One of the candidate guards, uniformly by <paramref name="die"/> (0 ≤ die &lt; 1); <see cref="Unknown"/> when there is none.</summary>
        public static int ChooseGuard(int previous, int weaponMask, bool sidesCrossed, double die)
        {
            int n = CandidateCount(previous, weaponMask, sidesCrossed);
            if (n == 0) return Unknown;
            int pick = double.IsNaN(die) ? 0 : (int)Math.Floor(Math.Max(0, Math.Min(0.999999999, die)) * n);
            for (int g = GuardUp; g <= GuardRight; g++)
            {
                if (!Candidate(g, previous, weaponMask, sidesCrossed)) continue;
                if (pick == 0) return g;
                pick--;
            }
            return Unknown;
        }

        /// <summary>The perfect half-width (s) around the flash: the timed block's perfect window at your weapon skill (step 101) x <c>OpeningPerfectScalePercent</c>.</summary>
        public static double Perfect(in OpeningRules r, double skill)
        {
            var t = r.Timed;
            double p = double.IsNaN(skill) || double.IsInfinity(skill) ? t.PerfectAt0 : TimedBlockMath.PerfectFor(in t, skill);
            return Math.Max(0, p * r.PerfectScale);
        }

        /// <summary>THE WINDOW's end after the flash (s), link <paramref name="k"/>: base + per link x (k − 1); x <c>OpeningUnguardedWindowPercent</c> after an unguarded front hit.</summary>
        public static double Window(in OpeningRules r, int k, bool unguarded)
        {
            k = Math.Max(1, Math.Min(r.ChainMax, k));
            double w = r.WindowBase + r.WindowPerLink * (k - 1);
            if (unguarded) w *= r.UnguardedWindow;
            return Math.Max(0, w);
        }

        /// <summary>
        /// THE TIMING SCORE (0-1) of your follow-up, started <paramref name="sinceBlock"/> s after his block (or your unguarded hit): the defensive
        /// game's shape (<see cref="TimedBlockMath.Score"/>) around the FLASH - 1 within the perfect half-width of it, a smoothstep down to 0 at the
        /// block itself (too early) and at the window's end (too late). NaN = 0.
        /// </summary>
        public static double TimingScore(in OpeningRules r, in OpeningPlan p, double sinceBlock)
        {
            if (double.IsNaN(sinceBlock) || double.IsInfinity(sinceBlock)) return 0;
            double dt = sinceBlock - p.Flash;
            return dt < 0 ? TimedBlockMath.Score(-dt, p.Perfect, p.Flash) : TimedBlockMath.Score(dt, p.Perfect, p.Window);
        }

        /// <summary>The block chance's cut by link: 1 − <c>OpeningBlockPerLinkPercent</c> x (k − 1), never below 0.</summary>
        public static double LinkFactor(in OpeningRules r, int k)
        {
            k = Math.Max(1, Math.Min(r.ChainMax, k));
            return Math.Max(0, 1 - r.BlockPerLink * (k - 1));
        }

        /// <summary>
        /// HIS BLOCK CHANCE (0-1) against your follow-up when the AI WOULD block it correctly: 0 at a perfect start, rising with (1 − score) to his
        /// normal behaviour (1) at the window's end, x the link's cut; a shield below <c>OpeningShieldMinChain</c> blocks as usual (1).
        /// </summary>
        public static double BlockChance(in OpeningRules r, double score, int k, bool shieldHolds)
        {
            if (shieldHolds) return 1;
            double s = double.IsNaN(score) ? 0 : Math.Max(0, Math.Min(1, score));
            return Math.Max(0, Math.Min(1, (1 - s) * LinkFactor(in r, k)));
        }

        /// <summary>The reel a hit off balance gives (s): base + per link x (k − 1), at most <c>OpeningReelMaxSeconds</c>.</summary>
        public static double ReelSeconds(in OpeningRules r, int k)
        {
            k = Math.Max(1, Math.Min(r.ChainMax, k));
            return Math.Min(r.ReelMax, r.ReelBase + r.ReelPerChain * (k - 1));
        }

        /// <summary>When a committed swing's forced guard is let go at the latest (the swing's start + <c>OpeningHoldMaxSeconds</c>).</summary>
        public static double HoldEnds(in OpeningRules r, double windUpAt) => windUpAt + r.HoldMax;

        /// <summary>
        /// THE FINISHING SIDES (a mask of attacks): <paramref name="wanted"/> is the strict opposite (step 103); with
        /// <c>PerfectStrikeWeaponFinishes</c>, when the weapon cannot make it, ANY of the weapon's directions but the one opposite to the wanted -
        /// a no-thrust weapon (axe, mace) finishes an OVERHEAD stagger (wanted: the thrust) with its left OR right swing; a spear (overhead stab +
        /// thrust) finishes a SIDE stagger with either stab, a vertical one with the other stab (= the strict opposite). Unknown wanted = none.
        /// </summary>
        public static int FinishMask(int wanted, int weaponMask, bool weaponFinishes)
        {
            if (!IsAttack(wanted)) return 0;
            if (!weaponFinishes || (weaponMask & Bit(wanted)) != 0) return Bit(wanted);
            return weaponMask & AllAttacks & ~Bit(Opposite(wanted));
        }

        /// <summary>The finishing sides after YOUR staggering swing (an opening's reel): its opposite (left ↔ right, overhead ↔ thrust), by the weapon.</summary>
        public static int FinishAfterYourSwing(int yourSwing, int weaponMask, bool weaponFinishes) =>
            FinishMask(Opposite(yourSwing), weaponMask, weaponFinishes);

        /// <summary>The arrow for a finishing mask: the single side's, else (two sides) the first by top / bottom / left / right.</summary>
        public static OpeningArrow FinishArrow(int finishMask)
        {
            if ((finishMask & Bit(AttackUp)) != 0) return OpeningArrow.Top;
            if ((finishMask & Bit(AttackDown)) != 0) return OpeningArrow.Bottom;
            if ((finishMask & Bit(AttackLeft)) != 0) return OpeningArrow.Left;
            if ((finishMask & Bit(AttackRight)) != 0) return OpeningArrow.Right;
            return OpeningArrow.None;
        }

        /// <summary>The PERFECT STRIKE's range for a weapon with <paramref name="directions"/> attack directions: 4 → x2-x4, 3 (no thrust) → x2-x3,
        /// 2 or fewer (spears) → x1.5-x2 (the defaults).</summary>
        public static void MultiplierRange(in OpeningRules r, int directions, out double min, out double max)
        {
            if (directions >= 4)
            {
                min = r.FourMin;
                max = r.FourMax;
            }
            else if (directions == 3)
            {
                min = r.ThreeMin;
                max = r.ThreeMax;
            }
            else
            {
                min = r.TwoMin;
                max = r.TwoMax;
            }
        }

        /// <summary>The PERFECT STRIKE's multiplier: the weapon's range, min at timing 0, a straight line to max at 1 (step 100g's shape).</summary>
        public static double StrikeMultiplier(in OpeningRules r, int weaponMask, double score)
        {
            MultiplierRange(in r, DirectionCount(weaponMask), out double min, out double max);
            return PerfectStrikeMath.Multiplier(score, min, max);
        }

        /// <summary>
        /// THE OFF-BALANCE PLAN: link <paramref name="k"/>, the entry, your weapon's skill and the attacks it can make, his guard at contact and
        /// your attack's direction (to know the guard he held). One die is drawn only when there is a choice.
        /// </summary>
        public static OpeningPlan Plan(in OpeningRules r, int k, OpeningEntry entry, double skill, bool mounted, int guardAtContact, int yourAttack, int weaponMask,
            IRandomSource? dice)
        {
            bool shield = entry == OpeningEntry.ShieldBlocked;
            var p = new OpeningPlan
            {
                K = Math.Max(1, Math.Min(r.ChainMax, k)),
                Shield = shield,
                Entry = entry,
                Guard = Unknown,
                Opening = Unknown,
                Die = double.NaN,
                PreviousGuard = PreviousGuard(guardAtContact, yourAttack, r.SidesCrossed),
            };
            if (mounted)
            {
                p.Skip = OpeningSkip.Mounted;
                return p;
            }
            p.Candidates = CandidateCount(p.PreviousGuard, weaponMask, r.SidesCrossed);
            if (p.Candidates == 0)
            {
                p.Skip = OpeningSkip.NoOpeningForWeapon;
                return p;
            }
            if (p.Candidates > 1 && dice != null) p.Die = dice.NextDouble();
            p.Guard = ChooseGuard(p.PreviousGuard, weaponMask, r.SidesCrossed, double.IsNaN(p.Die) ? 0 : p.Die);
            p.Opening = OpeningFor(p.Guard, r.SidesCrossed);
            p.Arrow = ArrowFor(p.Opening);
            p.Flash = r.FlashDelay;
            p.Perfect = Perfect(in r, skill);
            p.Window = Window(in r, p.K, entry == OpeningEntry.FrontUnguarded);
            p.Reel = ReelSeconds(in r, p.K);
            p.ShieldHolds = shield && p.K < r.ShieldMinChain;
            return p;
        }

        /// <summary>The DIRECTION verdict on your follow-up (<paramref name="direction"/> an attack 0-3; anything else = unknown; NaN start = no swing).</summary>
        public static OpeningVerdict Verdict(in OpeningPlan p, double sinceBlock, int direction)
        {
            if (double.IsNaN(sinceBlock)) return OpeningVerdict.NoSwing;
            if (!IsAttack(direction)) return OpeningVerdict.UnknownDirection;
            return direction == p.Opening ? OpeningVerdict.Opened : OpeningVerdict.WrongDirection;
        }

        public static string AttackName(int d) => d switch
        {
            AttackUp => "overhead",
            AttackDown => "thrust",
            AttackLeft => "left swing",
            AttackRight => "right swing",
            _ => "unknown",
        };

        public static string GuardName(int d) => d switch
        {
            GuardUp => "high guard",
            GuardDown => "low guard",
            GuardLeft => "left guard",
            GuardRight => "right guard",
            _ => "unknown guard",
        };

        public static string ArrowName(OpeningArrow a) => a switch
        {
            OpeningArrow.Top => "top",
            OpeningArrow.Bottom => "bottom",
            OpeningArrow.Left => "left",
            OpeningArrow.Right => "right",
            _ => "none",
        };

        public static string MaskName(int mask)
        {
            if ((mask & AllAttacks) == 0) return "none";
            if ((mask & AllAttacks) == AllAttacks) return "any";
            return (mask & Bit(AttackUp)) != 0 && (mask & Bit(AttackDown)) != 0 && DirectionCount(mask) == 2 ? "overhead or thrust"
                : (mask & Bit(AttackLeft)) != 0 && (mask & Bit(AttackRight)) != 0 && DirectionCount(mask) == 2 ? "left or right swing"
                : DirectionCount(mask) == 1 ? AttackName(FirstAttack(mask))
                : DirectionCount(mask).ToString(CultureInfo.InvariantCulture) + " sides";
        }

        private static int FirstAttack(int mask)
        {
            for (int d = AttackUp; d <= AttackRight; d++)
                if ((mask & Bit(d)) != 0) return d;
            return Unknown;
        }

        public static string VerdictName(OpeningVerdict v) => v switch
        {
            OpeningVerdict.Opened => "INTO the balance side",
            OpeningVerdict.WrongDirection => "wrong direction",
            OpeningVerdict.UnknownDirection => "direction unread",
            _ => "no swing",
        };

        public static string EntryName(OpeningEntry e) => e switch
        {
            OpeningEntry.Blocked => "weapon block",
            OpeningEntry.ShieldBlocked => "shield block",
            OpeningEntry.FrontUnguarded => "unguarded front hit",
            OpeningEntry.Hold => "hold-block",
            _ => "back hit",
        };

        public static string ResetName(OpeningReset r) => r switch
        {
            OpeningReset.ReelOver => "the reel over",
            OpeningReset.Gap => "a gap",
            OpeningReset.HeHitYou => "he hit you",
            OpeningReset.TargetSwitch => "another target",
            OpeningReset.Miss => "a miss",
            OpeningReset.Off => "off / gone",
            _ => "none",
        };
    }

    /// <summary>
    /// Step 106a2: YOUR chain against one man - THE STATE MACHINE (a struct the logic keeps; pure). None → OFF BALANCE (a correct block of your
    /// attack: link k + 1; a landed front hit on a man not guarding: link 1, the longer window) → REELING (a landed hit while he is off balance,
    /// or any landed hit from behind: the reel by link, the PERFECT STRIKE side = the opposite of the staggering swing, by the weapon) → None (a
    /// hit during the reel - a perfect strike or the other side's knockdown roll - or the reel's end). Off balance lasts until
    /// <c>OpeningChainResetSeconds</c> after its last link; another man or the switches reset it.
    /// </summary>
    public struct OpeningChain
    {
        /// <summary>The man the chain is on (an agent index; −1 = none).</summary>
        public int Target;

        /// <summary>The link reached (0 = no chain).</summary>
        public int K;

        public OpeningPhase Phase;
        public OpeningEntry Entry;

        /// <summary>The last link's time (a block, or an unguarded hit).</summary>
        public double LastBlockAt;

        /// <summary>Reeling: until when; the swing that staggered him; the finishing sides; the follow-up's timing score (the multiplier's).</summary>
        public double ReelUntil;
        public int StaggerSwing;
        public int FinishMask;
        public double Score;

        public bool Active => Phase != OpeningPhase.None && Target >= 0;

        public bool OffBalance => Phase == OpeningPhase.OffBalance && Target >= 0;

        public bool Reeling(double now) => Phase == OpeningPhase.Reeling && Target >= 0 && now <= ReelUntil + 1e-9;

        /// <summary>Off balance past <c>OpeningChainResetSeconds</c> since the last link, or the reel over.</summary>
        public bool Expired(double now, in OpeningRules r) =>
            Phase == OpeningPhase.OffBalance ? now - LastBlockAt > r.ChainReset + 1e-9
            : Phase == OpeningPhase.Reeling && now > ReelUntil + 1e-9;

        /// <summary>
        /// A correct block of your attack by <paramref name="target"/>: the next link (at most <c>OpeningChainMax</c>), off balance - or link 1
        /// after a reset, whose reason goes to <paramref name="reset"/> (another man, the gap). A block during a reel ends the reel (link 1).
        /// </summary>
        public int NoteBlock(int target, double now, bool shield, in OpeningRules r, out OpeningReset reset)
        {
            reset = OpeningReset.None;
            if (Active && target != Target) reset = OpeningReset.TargetSwitch;
            else if (Expired(now, in r)) reset = Phase == OpeningPhase.Reeling ? OpeningReset.ReelOver : OpeningReset.Gap;
            if (!Active || reset != OpeningReset.None || Phase == OpeningPhase.Reeling) K = 0;
            Target = target;
            K = Math.Min(r.ChainMax, K + 1);
            Phase = OpeningPhase.OffBalance;
            Entry = shield ? OpeningEntry.ShieldBlocked : OpeningEntry.Blocked;
            LastBlockAt = now;
            return K;
        }

        /// <summary>
        /// Your hit LANDED on <paramref name="target"/> (<paramref name="swing"/> your direction; <paramref name="fromBehind"/> step 62's rear band;
        /// <paramref name="guarding"/> his guard up at contact; <paramref name="score"/> the follow-up's timing, 0 for none): the reel's finish, a
        /// stagger (off balance, or from behind), off balance (an unguarded front hit), else nothing (the chain on another man is left to the
        /// caller's target-switch reset). <paramref name="reset"/> names a chain this ended for another reason.
        /// Step 106e: <paramref name="staggerImmune"/> = he was reeling or inside the impact system's stagger immunity BEFORE this hit
        /// (<c>ImpactStaggerImmunitySeconds</c> after a reel). The entries the player did not EARN with a block - a back hit on a man who is not
        /// off balance, an unguarded front hit - open nothing then: otherwise a reeling
        /// man (who never guards) could be re-staggered by every other hit forever (<see cref="OpeningHit.Immune"/>, the chain untouched). A chain
        /// earned with a block still staggers as designed.
        /// </summary>
        public OpeningHit NoteLanded(int target, double now, in OpeningRules r, bool fromBehind, bool guarding, int swing, int weaponMask, double score,
            out OpeningReset reset, bool staggerImmune = false)
        {
            reset = OpeningReset.None;
            if (Active && target == Target && Expired(now, in r))
            {
                reset = Phase == OpeningPhase.Reeling ? OpeningReset.ReelOver : OpeningReset.Gap;
                Reset();
            }
            if (Phase == OpeningPhase.Reeling && target == Target)
            {
                bool perfect = (FinishMask & OpeningMath.Bit(swing)) != 0;
                Reset();
                reset = OpeningReset.ReelOver;
                return perfect ? OpeningHit.FinishPerfect : OpeningHit.FinishOther;
            }
            bool offOnHim = Phase == OpeningPhase.OffBalance && target == Target;
            if (staggerImmune && !offOnHim && (fromBehind ? r.BackHitStaggers : !guarding && r.UnguardedHitOpens)) return OpeningHit.Immune; // step 106e
            if (fromBehind && r.BackHitStaggers)
            {
                if (Active && target != Target) reset = OpeningReset.TargetSwitch;
                int k = offOnHim ? K : 1;
                Stagger(target, now, in r, k, OpeningEntry.Back, swing, weaponMask, offOnHim ? score : 0);
                return OpeningHit.Staggers;
            }
            if (offOnHim)
            {
                Stagger(target, now, in r, K, Entry, swing, weaponMask, score);
                return OpeningHit.Staggers;
            }
            if (!guarding && r.UnguardedHitOpens)
            {
                if (Active && target != Target) reset = OpeningReset.TargetSwitch;
                Target = target;
                K = 1;
                Phase = OpeningPhase.OffBalance;
                Entry = OpeningEntry.FrontUnguarded;
                LastBlockAt = now;
                return OpeningHit.OffBalance;
            }
            return OpeningHit.Nothing;
        }

        /// <summary>
        /// Step 107: your WINNING strike landed on a man holding a forced block - he reels (link <paramref name="k"/>'s reel), the PERFECT STRIKE side =
        /// the opposite of that swing by the weapon, the timing <paramref name="score"/> (the hold's chain speed). Any earlier chain is replaced.
        /// </summary>
        public void HoldStagger(int target, double now, in OpeningRules r, int k, int swing, int weaponMask, double score) =>
            Stagger(target, now, in r, Math.Max(1, Math.Min(r.ChainMax, k)), OpeningEntry.Hold, swing, weaponMask, score);

        private void Stagger(int target, double now, in OpeningRules r, int k, OpeningEntry entry, int swing, int weaponMask, double score)
        {
            Target = target;
            K = Math.Max(1, k);
            Entry = entry;
            Phase = OpeningPhase.Reeling;
            ReelUntil = now + OpeningMath.ReelSeconds(in r, K);
            StaggerSwing = swing;
            FinishMask = OpeningMath.FinishAfterYourSwing(swing, weaponMask, r.WeaponFinishes);
            Score = double.IsNaN(score) ? 0 : Math.Max(0, Math.Min(1, score));
        }

        public void Reset()
        {
            Target = -1;
            K = 0;
            Phase = OpeningPhase.None;
            LastBlockAt = 0;
            ReelUntil = 0;
            StaggerSwing = OpeningMath.Unknown;
            FinishMask = 0;
            Score = 0;
        }

        public static OpeningChain None => new OpeningChain { Target = -1, StaggerSwing = OpeningMath.Unknown };
    }

    /// <summary>Step 106a2: the openings' shadow over one battle - the [summary] "openings" line. Main thread; no allocation per note.</summary>
    public sealed class OpeningStats
    {
        public const int DelayBands = 7;

        /// <summary>The block → wind-up delay bands' upper edges (s): &lt;0.1, 0.1-0.3, 0.3-0.5, 0.5-0.8, 0.8-1.2, 1.2-2, 2+.</summary>
        public static readonly double[] DelayEdges = { 0.1, 0.3, 0.5, 0.8, 1.2, 2.0 };

        /// <summary>Timing score bands: perfect (1), good (0.5-1), late / early (0-0.5), none (0).</summary>
        public const int ScoreBands = 4;

        /// <summary>Correct blocks of YOUR attack by an AI man, by the chain's link (1..<see cref="OpeningMath.MaxChain"/>); shield / weapon.</summary>
        public readonly int[] Blocks = new int[OpeningMath.MaxChain + 1];
        public int ShieldBlocks, WeaponBlocks;

        /// <summary>Off-balance states by entry (<see cref="OpeningEntry"/>), the balance guards, skips by <see cref="OpeningSkip"/>; shield states that still hold.</summary>
        public readonly int[] Entries = new int[5];
        public readonly int[] BalanceGuards = new int[4];
        public readonly int[] Skips = new int[3];
        public int ShieldHolds;

        /// <summary>THE ENGINE FACT: your attack's direction (4) × his guard read at contact (4 guards + unread) - the side pairing.</summary>
        public readonly int[] Pairs = new int[4 * 5];

        /// <summary>Your follow-up by direction verdict; its timing score band (all follow-ups that started); into the side by link.</summary>
        public readonly int[] Verdicts = new int[4];
        public readonly int[] Scores = new int[ScoreBands];
        public readonly int[] OpenedByK = new int[OpeningMath.MaxChain + 1];
        public double ScoreSum;
        public int ScoreCount;

        /// <summary>Block → your next wind-up: bands, sum, count, min, max (s).</summary>
        public readonly int[] DelayBand = new int[DelayBands];
        public double DelaySum, DelayMin = double.PositiveInfinity, DelayMax;
        public int DelayCount;

        /// <summary>The follow-up's JUDGED direction (step 106f: the swing it committed to - its release, else the contact) vs the direction the
        /// contact reported: agree / differ / unread.</summary>
        public int DirAgree, DirDiffer, DirUnread;

        /// <summary>Step 106f: where the follow-up's direction was judged (<see cref="OpeningJudgedAt"/>), and the wind-up's first read vs the
        /// committed direction (agree / differ / unread at the wind-up) - the 106e build judged at the wind-up and misread 14 of 33.</summary>
        public readonly int[] JudgedAt = new int[(int)OpeningJudgedAt.Count];
        public int WindUpAgree, WindUpDiffer, WindUpUnread;

        /// <summary>Step 106f: one follow-up's direction judged <paramref name="at"/> - <paramref name="windUp"/> the wind-up's first read,
        /// <paramref name="committed"/> the direction judged.</summary>
        public void NoteJudged(OpeningJudgedAt at, int windUp, int committed)
        {
            if (at >= 0 && at < OpeningJudgedAt.Count) JudgedAt[(int)at]++;
            if (!OpeningMath.IsAttack(committed)) return;
            if (!OpeningMath.IsAttack(windUp)) WindUpUnread++;
            else if (windUp == committed) WindUpAgree++;
            else WindUpDiffer++;
        }

        /// <summary>THE BLOCK ROLL (shadow dice): follow-ups into the side he blocked correctly anyway - rolled, WOULD have got through (his guard
        /// forced), the chance's sum; by link.</summary>
        public int BlockRolls, WouldGetThrough;
        public double BlockChanceSum;
        public readonly int[] ThroughByK = new int[OpeningMath.MaxChain + 1];

        /// <summary>Landed hits: staggers by entry, the mod's own stagger on the same hit too, off balance from an unguarded front hit.</summary>
        public readonly int[] Staggers = new int[5];
        public int ModStaggerToo, UnguardedOffBalance;

        /// <summary>Step 106e: back / unguarded hits on a man still reeling or stagger-immune - no new opening.</summary>
        public int ImmuneRefused;

        /// <summary>Finishes during the reel: perfect / other side (step 105's roll); the reels that ended with no hit; the perfect multipliers'
        /// sum; perfects by the weapon's directions (2 / 3 / 4 → index 0 / 1 / 2); the no-thrust / spear relaxed finishes.</summary>
        public int FinishPerfect, FinishOther, ReelsUnused, RelaxedFinishes;
        public double MultiplierSum;
        public readonly int[] PerfectByDirections = new int[3];

        /// <summary>Chain resets by <see cref="OpeningReset"/>; missed swings seen in a chain.</summary>
        public readonly int[] Resets = new int[7];
        public int Misses;

        /// <summary>AI vs AI correct blocks on foot (counted only); a man's guard read failed; errors.</summary>
        public int AiVsAi, GuardReadFailed, Errors;

        /// <summary>
        /// Step 106d - THE LIVE PAYOFF: opening staggers asked (our forced stagger queued, <see cref="ReelCause.Opening"/>), carried by the mod's own
        /// stagger on the same hit (no second one), not given (the same hit knocked him down / he was already down / the impact system's riposte
        /// family not live / a full queue); the forced stagger played by the engine, seen, never seen; the perfect-strike windows they opened;
        /// "GUARD BROKEN" (a shield lowered by a won roll) and "OPENING!" shown.
        /// </summary>
        public int LiveAsked, LiveOnModStagger, LiveKnockedDown, LiveAlreadyDown, LiveNotLive, LiveNotApplied, LivePlayed, LiveSeen, LiveNotSeen, LiveWindows,
            GuardBrokenShown, OpeningShown;

        /// <summary>The live staggers' asked and measured seconds (sums / counts).</summary>
        public double LiveAskedSum, LiveMeasuredSum;
        public int LiveMeasured;

        public void NoteLiveMeasured(double asked, double measured)
        {
            if (double.IsNaN(measured)) return;
            LiveMeasured++;
            LiveAskedSum += double.IsNaN(asked) ? 0 : asked;
            LiveMeasuredSum += measured;
        }

        public int StatesTotal
        {
            get
            {
                int n = 0;
                foreach (var v in Entries) n += v;
                return n;
            }
        }

        public int BlocksTotal
        {
            get
            {
                int n = 0;
                foreach (var v in Blocks) n += v;
                return n;
            }
        }

        public void NoteBlock(in OpeningPlan p)
        {
            int k = Math.Max(0, Math.Min(OpeningMath.MaxChain, p.K));
            Blocks[k]++;
            if (p.Shield) ShieldBlocks++;
            else WeaponBlocks++;
            NoteState(in p);
        }

        /// <summary>An off-balance state planned (or skipped) - a block's or an unguarded hit's.</summary>
        public void NoteState(in OpeningPlan p)
        {
            if (!p.Opens)
            {
                Skips[(int)p.Skip]++;
                return;
            }
            Entries[(int)p.Entry]++;
            if (p.ShieldHolds) ShieldHolds++;
            if (OpeningMath.IsGuard(p.Guard)) BalanceGuards[p.Guard - OpeningMath.GuardUp]++;
        }

        public void NotePair(int yourAttack, int guardAtContact)
        {
            if (!OpeningMath.IsAttack(yourAttack)) return;
            int g = OpeningMath.IsGuard(guardAtContact) ? guardAtContact - OpeningMath.GuardUp : 4;
            Pairs[yourAttack * 5 + g]++;
        }

        public static int Band(double delay)
        {
            for (int i = 0; i < DelayEdges.Length; i++)
                if (delay < DelayEdges[i]) return i;
            return DelayBands - 1;
        }

        public static int ScoreBand(double s) => s >= TimedBlockMath.PerfectScore ? 0 : s >= 0.5 ? 1 : s > 0 ? 2 : 3;

        /// <summary>One follow-up judged (its direction verdict, its timing score, its delay from the link).</summary>
        public void NoteFollowUp(OpeningVerdict v, int k, double delay, double score)
        {
            Verdicts[(int)v]++;
            if (v == OpeningVerdict.Opened) OpenedByK[Math.Max(0, Math.Min(OpeningMath.MaxChain, k))]++;
            if (double.IsNaN(delay) || v == OpeningVerdict.NoSwing) return;
            double s = double.IsNaN(score) ? 0 : score;
            Scores[ScoreBand(s)]++;
            ScoreSum += s;
            ScoreCount++;
            DelayBand[Band(delay)]++;
            DelaySum += delay;
            DelayCount++;
            if (delay < DelayMin) DelayMin = delay;
            if (delay > DelayMax) DelayMax = delay;
        }

        public void NoteDirection(int judged, int atContact)
        {
            if (!OpeningMath.IsAttack(atContact)) return;
            if (!OpeningMath.IsAttack(judged)) DirUnread++;
            else if (judged == atContact) DirAgree++;
            else DirDiffer++;
        }

        /// <summary>A follow-up into the balance side blocked correctly: the shadow roll - would his block have failed (his guard forced)?</summary>
        public void NoteBlockRoll(double chance, bool wouldGetThrough, int k)
        {
            BlockRolls++;
            BlockChanceSum += double.IsNaN(chance) ? 0 : chance;
            if (!wouldGetThrough) return;
            WouldGetThrough++;
            ThroughByK[Math.Max(0, Math.Min(OpeningMath.MaxChain, k))]++;
        }

        public void NoteStagger(OpeningEntry e, bool modStaggerToo)
        {
            Staggers[(int)e]++;
            if (modStaggerToo) ModStaggerToo++;
        }

        public void NoteFinish(bool perfect, double multiplier, int weaponMask, bool relaxed)
        {
            if (!perfect)
            {
                FinishOther++;
                return;
            }
            FinishPerfect++;
            MultiplierSum += multiplier;
            int n = OpeningMath.DirectionCount(weaponMask);
            PerfectByDirections[n >= 4 ? 2 : n == 3 ? 1 : 0]++;
            if (relaxed) RelaxedFinishes++;
        }

        public void NoteReset(OpeningReset r)
        {
            if (r != OpeningReset.None) Resets[(int)r]++;
        }

        /// <summary>The [summary] line.</summary>
        public string SummaryLine(in OpeningRules r)
        {
            var c = CultureInfo.InvariantCulture;
            var t = new StringBuilder();
            t.Append("openings (step 106d; live ").Append(r.Enabled ? "ON - the forced guard, the block roll, the stagger and the perfect strike" : "OFF (" + r.OffBecause + ") - counted only")
             .Append(", the shadow log ").Append(r.ShadowOn ? "ON" : "OFF").Append("): your attacks the AI blocked correctly ").Append(BlocksTotal.ToString(c))
             .Append(" (shield ").Append(ShieldBlocks.ToString(c)).Append(", weapon ").Append(WeaponBlocks.ToString(c)).Append(") by link");
            for (int k = 1; k <= r.ChainMax; k++) t.Append(k == 1 ? " " : " / ").Append(Blocks[k].ToString(c));
            t.Append("; OFF BALANCE ").Append(StatesTotal.ToString(c)).Append(" (weapon block ").Append(Entries[(int)OpeningEntry.Blocked].ToString(c))
             .Append(", shield block ").Append(Entries[(int)OpeningEntry.ShieldBlocked].ToString(c)).Append(" - its shield still holding ").Append(ShieldHolds.ToString(c))
             .Append(", unguarded front hit ").Append(Entries[(int)OpeningEntry.FrontUnguarded].ToString(c)).Append(") - balance to low / high / left / right guard ")
             .Append(BalanceGuards[OpeningMath.GuardDown - 4].ToString(c)).Append(" / ").Append(BalanceGuards[OpeningMath.GuardUp - 4].ToString(c)).Append(" / ")
             .Append(BalanceGuards[OpeningMath.GuardLeft - 4].ToString(c)).Append(" / ").Append(BalanceGuards[OpeningMath.GuardRight - 4].ToString(c));
            if (Skips[1] + Skips[2] > 0)
                t.Append("; none: mounted ").Append(Skips[(int)OpeningSkip.Mounted].ToString(c)).Append(", no side your weapon can make ").Append(Skips[(int)OpeningSkip.NoOpeningForWeapon].ToString(c));
            t.Append("; your follow-up: INTO the balance side ").Append(Verdicts[(int)OpeningVerdict.Opened].ToString(c));
            if (Verdicts[(int)OpeningVerdict.Opened] > 0)
            {
                t.Append(" (by link");
                for (int k = 1; k <= r.ChainMax; k++) t.Append(k == 1 ? " " : " / ").Append(OpenedByK[k].ToString(c));
                t.Append(')');
            }
            t.Append(", wrong direction ").Append(Verdicts[(int)OpeningVerdict.WrongDirection].ToString(c))
             .Append(", direction unread ").Append(Verdicts[(int)OpeningVerdict.UnknownDirection].ToString(c))
             .Append(", no swing ").Append(Verdicts[(int)OpeningVerdict.NoSwing].ToString(c));
            t.Append("; its TIMING perfect / good / weak / none ").Append(Scores[0].ToString(c)).Append(" / ").Append(Scores[1].ToString(c)).Append(" / ")
             .Append(Scores[2].ToString(c)).Append(" / ").Append(Scores[3].ToString(c));
            if (ScoreCount > 0) t.Append(" (mean ").Append((ScoreSum / ScoreCount).ToString("0.00", c)).Append(')');
            t.Append("; link -> your wind-up");
            if (DelayCount > 0)
            {
                t.Append(" mean ").Append((DelaySum / DelayCount).ToString("0.00", c)).Append(" s (").Append(DelayMin.ToString("0.00", c)).Append("-")
                 .Append(DelayMax.ToString("0.00", c)).Append("), bands <0.1 / 0.1-0.3 / 0.3-0.5 / 0.5-0.8 / 0.8-1.2 / 1.2-2 / 2+ s:");
                for (int i = 0; i < DelayBands; i++) t.Append(i == 0 ? " " : " / ").Append(DelayBand[i].ToString(c));
            }
            else t.Append(" none seen");
            t.Append("; follow-ups into the side he blocked anyway ").Append(BlockRolls.ToString(c));
            if (BlockRolls > 0)
            {
                t.Append(" - mean block chance ").Append((100 * BlockChanceSum / BlockRolls).ToString("0", c)).Append("%, WOULD have got through ")
                 .Append(WouldGetThrough.ToString(c)).Append(" (by link");
                for (int k = 1; k <= r.ChainMax; k++) t.Append(k == 1 ? " " : " / ").Append(ThroughByK[k].ToString(c));
                t.Append(')');
            }
            t.Append("; STAGGERS weapon-block chain ").Append(Staggers[(int)OpeningEntry.Blocked].ToString(c)).Append(", shield chain ")
             .Append(Staggers[(int)OpeningEntry.ShieldBlocked].ToString(c)).Append(", after an unguarded hit ").Append(Staggers[(int)OpeningEntry.FrontUnguarded].ToString(c))
             .Append(", from behind ").Append(Staggers[(int)OpeningEntry.Back].ToString(c)).Append(", from a hold-block (step 107) ").Append(Staggers[(int)OpeningEntry.Hold].ToString(c)).Append(" (the mod's own stagger on the same hit ").Append(ModStaggerToo.ToString(c))
             .Append("); unguarded front hits -> off balance ").Append(UnguardedOffBalance.ToString(c))
             .Append("; back / unguarded hits on a man still reeling or stagger-immune (no new opening) ").Append(ImmuneRefused.ToString(c));
            t.Append("; during the reel: PERFECT STRIKE ").Append(FinishPerfect.ToString(c));
            if (FinishPerfect > 0)
                t.Append(" (mean x").Append((MultiplierSum / FinishPerfect).ToString("0.00", c)).Append(", weapons with 2 / 3 / 4 directions ")
                 .Append(PerfectByDirections[0].ToString(c)).Append(" / ").Append(PerfectByDirections[1].ToString(c)).Append(" / ").Append(PerfectByDirections[2].ToString(c))
                 .Append(", by the weapon's relaxed sides ").Append(RelaxedFinishes.ToString(c)).Append(')');
            t.Append(", another side (step 105's knockdown roll) ").Append(FinishOther.ToString(c)).Append(", no hit before the reel ended ").Append(ReelsUnused.ToString(c));
            t.Append("; LIVE STAGGERS asked ").Append(LiveAsked.ToString(c)).Append(" (played ").Append(LivePlayed.ToString(c)).Append(", seen ").Append(LiveSeen.ToString(c))
             .Append(", never seen ").Append(LiveNotSeen.ToString(c)).Append(", not applied ").Append(LiveNotApplied.ToString(c));
            if (LiveMeasured > 0)
                t.Append(", asked ").Append((LiveAskedSum / LiveMeasured).ToString("0.00", c)).Append(" s -> measured ").Append((LiveMeasuredSum / LiveMeasured).ToString("0.00", c)).Append(" s");
            t.Append("), carried by the mod's own stagger ").Append(LiveOnModStagger.ToString(c)).Append(", none - knocked down by the same hit ").Append(LiveKnockedDown.ToString(c))
             .Append(" / already down ").Append(LiveAlreadyDown.ToString(c)).Append(" / the impact system's riposte not live ").Append(LiveNotLive.ToString(c))
             .Append("; perfect-strike windows opened ").Append(LiveWindows.ToString(c)).Append(", OPENING! shown ").Append(OpeningShown.ToString(c))
             .Append(", GUARD BROKEN shown ").Append(GuardBrokenShown.ToString(c));
            t.Append("; THE SIDE PAIRING (your attack -> his guard at contact: high / low / left / right / unread):");
            for (int a = 0; a < 4; a++)
            {
                t.Append(a == 0 ? " " : "; ").Append(OpeningMath.AttackName(a));
                for (int g = 0; g < 5; g++) t.Append(g == 0 ? " " : "/").Append(Pairs[a * 5 + g].ToString(c));
            }
            t.Append(" (sides now ").Append(r.SidesCrossed ? "CROSSED - your left swing met by his RIGHT guard" : "same-named - your left swing met by his LEFT guard").Append(')');
            t.Append("; your follow-up's direction judged (step 106f) at the release ").Append(JudgedAt[(int)OpeningJudgedAt.Release].ToString(c))
             .Append(", already at the wind-up ").Append(JudgedAt[(int)OpeningJudgedAt.WindUp].ToString(c)).Append(", only at the contact ")
             .Append(JudgedAt[(int)OpeningJudgedAt.Contact].ToString(c)).Append(" - the wind-up's first read vs the committed swing: agree ")
             .Append(WindUpAgree.ToString(c)).Append(", differ ").Append(WindUpDiffer.ToString(c)).Append(", unread ").Append(WindUpUnread.ToString(c));
            t.Append("; the judged direction vs the contact's: agree ").Append(DirAgree.ToString(c)).Append(", differ ").Append(DirDiffer.ToString(c))
             .Append(", unread ").Append(DirUnread.ToString(c));
            t.Append("; chain resets: reel over ").Append(Resets[(int)OpeningReset.ReelOver].ToString(c)).Append(", gap ").Append(Resets[(int)OpeningReset.Gap].ToString(c))
             .Append(", he hit you ").Append(Resets[(int)OpeningReset.HeHitYou].ToString(c)).Append(", another target ").Append(Resets[(int)OpeningReset.TargetSwitch].ToString(c))
             .Append(", a miss ").Append(Resets[(int)OpeningReset.Miss].ToString(c)).Append(", off ").Append(Resets[(int)OpeningReset.Off].ToString(c))
             .Append("; misses in a chain ").Append(Misses.ToString(c)).Append(r.MissKeepsChain ? " (kept it)" : " (reset it)");
            t.Append("; AI vs AI correct blocks on foot ").Append(AiVsAi.ToString(c)).Append(r.AiVsAi ? " (AI vs AI on - not built)" : " (counted only)");
            if (GuardReadFailed > 0) t.Append("; his guard unread at contact ").Append(GuardReadFailed.ToString(c));
            if (Errors > 0) t.Append("; ERRORS ").Append(Errors.ToString(c));
            return t.ToString();
        }
    }
}

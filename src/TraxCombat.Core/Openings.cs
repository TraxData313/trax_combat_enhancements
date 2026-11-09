using System;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>Step 106: where the red arrow points at - the side of the screen the opening is on (attacker-relative).</summary>
    public enum OpeningArrow : byte
    {
        None,

        /// <summary>At the top, pointing down - his guard dropped low: your overhead.</summary>
        Top,

        /// <summary>At the bottom, pointing up - his guard went high: your thrust from below.</summary>
        Bottom,

        /// <summary>On the left - your swing from your left (the engine's AttackLeft).</summary>
        Left,

        /// <summary>On the right - your swing from your right (the engine's AttackRight).</summary>
        Right,
    }

    /// <summary>Step 106: why a correct block of your attack opened nothing.</summary>
    public enum OpeningSkip : byte
    {
        None,

        /// <summary>You or he rides (v1: on foot only).</summary>
        Mounted,

        /// <summary>A shield block before the chain reached <c>OpeningShieldMinChain</c>.</summary>
        ShieldTooEarly,

        /// <summary>No other guard opens an attack your weapon can make.</summary>
        NoOpeningForWeapon,
    }

    /// <summary>Step 106: what came of one opening - the start of your next swing against it.</summary>
    public enum OpeningVerdict : byte
    {
        /// <summary>Your swing started inside the window, in the arrow's direction: into the opening.</summary>
        Opened,

        /// <summary>Inside the window, the wrong direction (the force released, the chain kept).</summary>
        WrongDirection,

        /// <summary>Started before his guard was forced (no reaction to the arrow possible).</summary>
        TooEarly,

        /// <summary>Started after the window closed.</summary>
        TooLate,

        /// <summary>The swing's direction could not be read.</summary>
        UnknownDirection,

        /// <summary>No swing of yours followed (the chain's gap ran out, the chain reset first, the mission ended).</summary>
        NoSwing,
    }

    /// <summary>Step 106: why the chain started over.</summary>
    public enum OpeningReset : byte
    {
        None,

        /// <summary>A blow of yours landed on him.</summary>
        HitLanded,

        /// <summary>More than <c>OpeningChainResetSeconds</c> between your attacks.</summary>
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

    /// <summary>Step 106: what your swing after an opening met (the shadow's time-to-contact line).</summary>
    public enum OpeningContact : byte
    {
        /// <summary>Your blow landed on him.</summary>
        Landed,

        /// <summary>He blocked it correctly again (the chain grows).</summary>
        BlockedAgain,

        /// <summary>His shield took it on the wrong side.</summary>
        WrongBlock,

        /// <summary>Your blow met someone else.</summary>
        OtherTarget,

        /// <summary>No contact (a miss, or nothing within the hold's time).</summary>
        None,
    }

    /// <summary>
    /// Step 106 - OPENINGS (Toni 2026-11-09; AI_NOTES "Step 106", DESIGN decision 95): the settings, read live. <see cref="Enabled"/> = the master
    /// switch FIRST, then <c>OpeningsEnabled</c> (the live parts - none built in 106a, the switch ships OFF); <see cref="ShadowOn"/> = the master
    /// switch, then <c>OpeningsShadowLog</c> (106a's log of what openings WOULD do). The skill's help reads the timed block's own window by skill.
    /// </summary>
    public readonly struct OpeningRules
    {
        public OpeningRules(TraxSettings s)
        {
            ModEnabled = s.ModEnabled;
            Switch = s.OpeningsEnabled;
            ShadowLog = s.OpeningsShadowLog;
            ForceDelay = Pos(s.OpeningForceDelaySeconds);
            WindowBase = Pos(s.OpeningWindowBaseSeconds);
            WindowPerChain = Pos(s.OpeningWindowPerChainSeconds);
            SkillWeight = Pos(s.OpeningWindowSkillWeight);
            ChainMax = Math.Max(1, Math.Min(OpeningMath.MaxChain, s.OpeningChainMax));
            ChainReset = Pos(s.OpeningChainResetSeconds);
            MissKeepsChain = s.OpeningMissKeepsChain;
            HoldMax = Pos(s.OpeningHoldMaxSeconds);
            SidesCrossed = s.OpeningSidesCrossed;
            ReelBase = Pos(s.OpeningReelBaseSeconds);
            ReelPerChain = Pos(s.OpeningReelPerChainSeconds);
            ReelMax = Pos(s.OpeningReelMaxSeconds);
            ShieldMinChain = Math.Max(1, s.OpeningShieldMinChain);
            ShieldWindow = Clamp01(s.OpeningShieldWindowPercent / 100.0);
            BreakBase = Clamp01(s.OpeningGuardBreakBasePercent / 100.0);
            BreakPerChain = Clamp01(s.OpeningGuardBreakPerChainPercent / 100.0);
            BreakStaggerMin = Pos(s.OpeningGuardBreakStaggerMinSeconds);
            BreakStaggerMax = Math.Max(BreakStaggerMin, Pos(s.OpeningGuardBreakStaggerMaxSeconds));
            GivesPerfectStrike = s.OpeningGivesPerfectStrike;
            ArrowShow = s.OpeningArrowShow;
            ArrowAtCrosshair = s.OpeningArrowAtCrosshair;
            AiVsAi = s.OpeningAiVsAi;
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

        public double ForceDelay { get; }
        public double WindowBase { get; }
        public double WindowPerChain { get; }
        public double SkillWeight { get; }
        public int ChainMax { get; }
        public double ChainReset { get; }
        public bool MissKeepsChain { get; }
        public double HoldMax { get; }
        public bool SidesCrossed { get; }
        public double ReelBase { get; }
        public double ReelPerChain { get; }
        public double ReelMax { get; }
        public int ShieldMinChain { get; }
        public double ShieldWindow { get; }
        public double BreakBase { get; }
        public double BreakPerChain { get; }
        public double BreakStaggerMin { get; }
        public double BreakStaggerMax { get; }
        public bool GivesPerfectStrike { get; }
        public bool ArrowShow { get; }
        public bool ArrowAtCrosshair { get; }
        public bool AiVsAi { get; }

        /// <summary>The timed block's rules - the perfect window by skill the window's skill help is measured with.</summary>
        public TimedBlockRules Timed { get; }

        public string? OffBecause => !ModEnabled ? "ModEnabled" : !Switch ? "OpeningsEnabled" : null;

        public string Describe()
        {
            var c = CultureInfo.InvariantCulture;
            return "openings " + (Enabled ? "ON" : "OFF (" + OffBecause + ")") + (ShadowOn ? ", the shadow log ON" : ", the shadow log OFF")
                   + ": a chain of up to " + ChainMax.ToString(c) + " of your attacks one man blocks (reset after " + ChainReset.ToString("0.0#", c) + " s"
                   + (MissKeepsChain ? ", a miss keeps it" : ", a miss resets it") + "); " + ForceDelay.ToString("0.00", c) + " s after his block his guard goes to one of the other 3 ("
                   + (SidesCrossed ? "sides crossed" : "sides same-named") + "); the window to start your swing " + WindowBase.ToString("0.00", c) + " + "
                   + WindowPerChain.ToString("0.00", c) + " x (k-1) + " + SkillWeight.ToString("0.0#", c) + " x your skill's perfect-window gain s; the reel "
                   + ReelBase.ToString("0.0#", c) + " + " + ReelPerChain.ToString("0.0#", c) + " x (k-1), at most " + ReelMax.ToString("0.0#", c) + " s; shields from k "
                   + ShieldMinChain.ToString(c) + ", the window x" + ShieldWindow.ToString("0.00", c) + ", a guard break " + (100 * BreakBase).ToString("0", c) + "% + "
                   + (100 * BreakPerChain).ToString("0", c) + " a link; " + (GivesPerfectStrike ? "a perfect strike too" : "no perfect strike") + "; "
                   + (AiVsAi ? "AI vs AI too" : "you only") + ", on foot";
        }

        private static double Pos(float v) => float.IsNaN(v) ? 0 : Math.Max(0, Math.Round((double)v, 5));

        private static double Clamp01(double v) => double.IsNaN(v) ? 0 : Math.Max(0, Math.Min(1, v));
    }

    /// <summary>
    /// Step 106: one opening as the rules plan it for a correct block of your attack - the guard forced, the attack it opens, the arrow, your
    /// window to start the swing, the reel it would give, a shield's guard-break chance and its stagger. <see cref="Skip"/> None = an opening.
    /// </summary>
    public struct OpeningPlan
    {
        public int K;
        public bool Shield;
        public OpeningSkip Skip;

        /// <summary>The guard that blocked you (read at contact, or derived from your attack) - <see cref="OpeningMath.Unknown"/> if neither.</summary>
        public int PreviousGuard;

        /// <summary>The guard he is forced to (an engine <c>UsageDirection</c> 4-7) and the attack it opens (0-3); <see cref="OpeningMath.Unknown"/> = none.</summary>
        public int Guard, Opening;

        public OpeningArrow Arrow;

        /// <summary>The window to START your swing, from the force (s); the reel a hit into it gives (s); a shield's guard-break chance and stagger.</summary>
        public double Window, Reel, BreakChance, BreakStagger;

        /// <summary>The die that picked the guard (NaN = none rolled).</summary>
        public double Die;

        /// <summary>How many guards it could have been.</summary>
        public int Candidates;

        public bool Opens => Skip == OpeningSkip.None && Opening != OpeningMath.Unknown;
    }

    /// <summary>
    /// Step 106 - OPENINGS, the pure math (AI_NOTES "Step 106", DESIGN decision 95). Directions are the engine's <c>Agent.UsageDirection</c>:
    /// AttackUp 0 (overhead), AttackDown 1 (thrust from below), AttackLeft 2, AttackRight 3, DefendUp 4, DefendDown 5, DefendLeft 6,
    /// DefendRight 7. A guard opens the attack OPPOSITE to the one it blocks (a low guard - your overhead; a high guard - your thrust; the sides
    /// by <see cref="OpeningRules.SidesCrossed"/>). No allocation.
    /// </summary>
    public static class OpeningMath
    {
        public const int Unknown = -1;
        public const int AttackUp = 0, AttackDown = 1, AttackLeft = 2, AttackRight = 3;
        public const int GuardUp = 4, GuardDown = 5, GuardLeft = 6, GuardRight = 7;

        /// <summary>The longest chain the settings may ask (the stats' arrays are this long + 1).</summary>
        public const int MaxChain = 8;

        /// <summary>Every attack direction (the weapon mask when its capabilities are unknown).</summary>
        public const int AllAttacks = 0xF;

        public static bool IsAttack(int d) => d >= AttackUp && d <= AttackRight;

        public static bool IsGuard(int d) => d >= GuardUp && d <= GuardRight;

        public static int Bit(int attack) => IsAttack(attack) ? 1 << attack : 0;

        /// <summary>The attacks a weapon can make: a swing gives the overhead and both sides, a thrust the attack from below; neither known = all.</summary>
        public static int WeaponMask(bool swing, bool thrust)
        {
            int m = (swing ? Bit(AttackUp) | Bit(AttackLeft) | Bit(AttackRight) : 0) | (thrust ? Bit(AttackDown) : 0);
            return m == 0 ? AllAttacks : m;
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

        /// <summary>The guard that blocks <paramref name="attack"/>: the vertical pairs by name; the sides crossed (his DefendLeft takes your
        /// AttackRight, which comes at his left) or same-named.</summary>
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

        /// <summary>THE MAPPING: the attack a forced <paramref name="guard"/> leaves open - the one opposite to what it blocks. Crossed sides
        /// (the brief's): DefendDown → AttackUp, DefendUp → AttackDown, DefendLeft → AttackLeft, DefendRight → AttackRight.</summary>
        public static int OpeningFor(int guard, bool sidesCrossed) => Opposite(AttackBlockedBy(guard, sidesCrossed));

        /// <summary>Where the arrow shows for an opened attack (attacker-relative).</summary>
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

        /// <summary>A guard may be forced: not the one he holds, and its opening is an attack your weapon can make.</summary>
        public static bool Candidate(int guard, int previous, int weaponMask, bool sidesCrossed) =>
            IsGuard(guard) && guard != previous && (Bit(OpeningFor(guard, sidesCrossed)) & weaponMask) != 0;

        /// <summary>How many guards may be forced.</summary>
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

        /// <summary>The skill's help (s): <c>OpeningWindowSkillWeight</c> x (the timed block's perfect window at this skill − at skill 0); NaN skill = 0.</summary>
        public static double SkillBonus(in OpeningRules r, double skill)
        {
            if (double.IsNaN(skill) || double.IsInfinity(skill)) return 0;
            var t = r.Timed;
            return r.SkillWeight * Math.Max(0, TimedBlockMath.PerfectFor(in t, skill) - t.PerfectAt0);
        }

        /// <summary>THE WINDOW (s) to start your swing after the force, link <paramref name="k"/> of the chain: base + per link x (k − 1) + the
        /// skill's help; a shield x <c>OpeningShieldWindowPercent</c>, and 0 below <c>OpeningShieldMinChain</c>.</summary>
        public static double Window(in OpeningRules r, int k, double skill, bool shield)
        {
            k = Math.Max(1, Math.Min(r.ChainMax, k));
            if (shield && k < r.ShieldMinChain) return 0;
            double w = r.WindowBase + r.WindowPerChain * (k - 1) + SkillBonus(in r, skill);
            if (shield) w *= r.ShieldWindow;
            return Math.Max(0, w);
        }

        /// <summary>The reel a hit into the opening gives (s): base + per link x (k − 1), at most <c>OpeningReelMaxSeconds</c>.</summary>
        public static double ReelSeconds(in OpeningRules r, int k)
        {
            k = Math.Max(1, Math.Min(r.ChainMax, k));
            return Math.Min(r.ReelMax, r.ReelBase + r.ReelPerChain * (k - 1));
        }

        /// <summary>A shield's guard-break chance at link k (0-1): 0 below the shield's minimum link, else base + per link x (k − min).</summary>
        public static double GuardBreakChance(in OpeningRules r, int k)
        {
            k = Math.Max(1, Math.Min(r.ChainMax, k));
            if (k < r.ShieldMinChain) return 0;
            return Math.Max(0, Math.Min(1, r.BreakBase + r.BreakPerChain * (k - r.ShieldMinChain)));
        }

        /// <summary>The broken guard's stagger (s): the minimum at the shield's first link, the maximum at the chain's last, linear between.</summary>
        public static double GuardBreakStagger(in OpeningRules r, int k)
        {
            k = Math.Max(1, Math.Min(r.ChainMax, k));
            if (k < r.ShieldMinChain) return 0;
            int span = r.ChainMax - r.ShieldMinChain;
            double t = span <= 0 ? 1 : (double)(k - r.ShieldMinChain) / span;
            return r.BreakStaggerMin + (r.BreakStaggerMax - r.BreakStaggerMin) * Math.Max(0, Math.Min(1, t));
        }

        /// <summary>When a committed swing's forced guard is let go at the latest (the swing's start + <c>OpeningHoldMaxSeconds</c>).</summary>
        public static double HoldEnds(in OpeningRules r, double windUpAt) => windUpAt + r.HoldMax;

        /// <summary>
        /// THE PLAN for a correct block of your attack: link <paramref name="k"/>, a shield or a weapon, your weapon's skill and the attacks it can
        /// make, his guard at contact and your attack's direction (to know the guard he held). One die is drawn only when there is a choice.
        /// </summary>
        public static OpeningPlan Plan(in OpeningRules r, int k, double skill, bool shield, bool mounted, int guardAtContact, int yourAttack, int weaponMask,
            IRandomSource? dice)
        {
            var p = new OpeningPlan
            {
                K = Math.Max(1, Math.Min(r.ChainMax, k)),
                Shield = shield,
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
            if (shield && p.K < r.ShieldMinChain)
            {
                p.Skip = OpeningSkip.ShieldTooEarly;
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
            p.Window = Window(in r, p.K, skill, shield);
            p.Reel = ReelSeconds(in r, p.K);
            if (shield)
            {
                p.BreakChance = GuardBreakChance(in r, p.K);
                p.BreakStagger = GuardBreakStagger(in r, p.K);
            }
            return p;
        }

        /// <summary>
        /// THE VERDICT on your next swing: it started <paramref name="sinceBlock"/> s after the block in <paramref name="direction"/> (an attack 0-3;
        /// anything else = unknown). Before the force: too early; after the force + the window: too late; else the direction decides.
        /// </summary>
        public static OpeningVerdict Verdict(in OpeningRules r, in OpeningPlan p, double sinceBlock, int direction)
        {
            if (double.IsNaN(sinceBlock)) return OpeningVerdict.NoSwing;
            if (sinceBlock < r.ForceDelay - 1e-9) return OpeningVerdict.TooEarly;
            if (sinceBlock > r.ForceDelay + p.Window + 1e-9) return OpeningVerdict.TooLate;
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

        public static string VerdictName(OpeningVerdict v) => v switch
        {
            OpeningVerdict.Opened => "INTO the opening",
            OpeningVerdict.WrongDirection => "wrong direction",
            OpeningVerdict.TooEarly => "too early",
            OpeningVerdict.TooLate => "too late",
            OpeningVerdict.UnknownDirection => "direction unread",
            _ => "no swing",
        };

        public static string ResetName(OpeningReset r) => r switch
        {
            OpeningReset.HitLanded => "your hit landed",
            OpeningReset.Gap => "a gap",
            OpeningReset.HeHitYou => "he hit you",
            OpeningReset.TargetSwitch => "another target",
            OpeningReset.Miss => "a miss",
            OpeningReset.Off => "off / gone",
            _ => "none",
        };

        public static string ContactName(OpeningContact c) => c switch
        {
            OpeningContact.Landed => "landed",
            OpeningContact.BlockedAgain => "blocked again",
            OpeningContact.WrongBlock => "wrong-side shield",
            OpeningContact.OtherTarget => "someone else",
            _ => "no contact",
        };
    }

    /// <summary>
    /// Step 106: YOUR chain against one man (a struct the logic keeps): the man, the link k and when the last block was. Pure.
    /// </summary>
    public struct OpeningChain
    {
        /// <summary>The man the chain is on (an agent index; −1 = none).</summary>
        public int Target;

        /// <summary>The link reached (0 = no chain).</summary>
        public int K;

        public double LastBlockAt;

        public bool Active => K > 0 && Target >= 0;

        /// <summary>More than <c>OpeningChainResetSeconds</c> since the last block.</summary>
        public bool Expired(double now, in OpeningRules r) => Active && now - LastBlockAt > r.ChainReset + 1e-9;

        /// <summary>
        /// A correct block of your attack by <paramref name="target"/> at <paramref name="now"/>: the next link (at most <c>OpeningChainMax</c>) - or
        /// link 1 after a reset, whose reason goes to <paramref name="reset"/> (another man, the gap).
        /// </summary>
        public int NoteBlock(int target, double now, in OpeningRules r, out OpeningReset reset)
        {
            reset = OpeningReset.None;
            if (Active && target != Target) reset = OpeningReset.TargetSwitch;
            else if (Expired(now, in r)) reset = OpeningReset.Gap;
            if (!Active || reset != OpeningReset.None) K = 0;
            Target = target;
            K = Math.Min(r.ChainMax, K + 1);
            LastBlockAt = now;
            return K;
        }

        public void Reset()
        {
            Target = -1;
            K = 0;
            LastBlockAt = 0;
        }

        public static OpeningChain None => new OpeningChain { Target = -1 };
    }

    /// <summary>Step 106a: the openings' shadow over one battle - the [summary] "openings" line. Main thread; no allocation per note.</summary>
    public sealed class OpeningStats
    {
        public const int DelayBands = 7;

        /// <summary>The block → wind-up delay bands' upper edges (s): &lt;0.1, 0.1-0.3, 0.3-0.5, 0.5-0.8, 0.8-1.2, 1.2-2, 2+.</summary>
        public static readonly double[] DelayEdges = { 0.1, 0.3, 0.5, 0.8, 1.2, 2.0 };

        /// <summary>Correct blocks of YOUR attack by an AI man, by the chain's link (1..<see cref="OpeningMath.MaxChain"/>); shield / weapon.</summary>
        public readonly int[] Blocks = new int[OpeningMath.MaxChain + 1];
        public int ShieldBlocks, WeaponBlocks;

        /// <summary>Planned openings by the link, and the guard / arrow they would force.</summary>
        public readonly int[] Openings = new int[OpeningMath.MaxChain + 1];
        public readonly int[] ForcedGuards = new int[4];

        /// <summary>Correct blocks that opened nothing, by <see cref="OpeningSkip"/>.</summary>
        public readonly int[] Skips = new int[4];

        /// <summary>THE ENGINE FACT: your attack's direction (4) × his guard read at contact (4 guards + unread) - the side pairing.</summary>
        public readonly int[] Pairs = new int[4 * 5];

        /// <summary>What came of each opening, by <see cref="OpeningVerdict"/>; into the opening by the link.</summary>
        public readonly int[] Verdicts = new int[6];
        public readonly int[] OpenedByK = new int[OpeningMath.MaxChain + 1];

        /// <summary>Block → your next wind-up: bands, sum, count, min, max (s); the same for the swings into the opening.</summary>
        public readonly int[] DelayBand = new int[DelayBands];
        public double DelaySum, DelayMin = double.PositiveInfinity, DelayMax;
        public int DelayCount;
        public double OpenedDelaySum;

        /// <summary>Your wind-up → contact: sum, count, min, max (s); what the swing met; what the swings into the opening met.</summary>
        public double ContactSum, ContactMin = double.PositiveInfinity, ContactMax;
        public int ContactCount;
        public readonly int[] Contacts = new int[5];
        public readonly int[] OpenedContacts = new int[5];

        /// <summary>Your wind-up's direction read vs the direction the contact reported: agree / differ / unread at the wind-up.</summary>
        public int DirAgree, DirDiffer, DirUnread;

        /// <summary>Chain resets by <see cref="OpeningReset"/>; missed swings seen in a chain.</summary>
        public readonly int[] Resets = new int[7];
        public int Misses;

        /// <summary>AI vs AI correct blocks on foot (counted only - the openings are yours); a man's guard read failed; errors.</summary>
        public int AiVsAi, GuardReadFailed, Errors;

        public int Planned
        {
            get
            {
                int n = 0;
                foreach (var v in Openings) n += v;
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
            if (!p.Opens)
            {
                Skips[(int)p.Skip]++;
                return;
            }
            Openings[k]++;
            if (OpeningMath.IsGuard(p.Guard)) ForcedGuards[p.Guard - OpeningMath.GuardUp]++;
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

        /// <summary>One opening judged (your next swing's start, or none).</summary>
        public void NoteVerdict(OpeningVerdict v, int k, double delay)
        {
            Verdicts[(int)v]++;
            if (v == OpeningVerdict.Opened)
            {
                OpenedByK[Math.Max(0, Math.Min(OpeningMath.MaxChain, k))]++;
                OpenedDelaySum += delay;
            }
            if (double.IsNaN(delay) || v == OpeningVerdict.NoSwing) return;
            DelayBand[Band(delay)]++;
            DelaySum += delay;
            DelayCount++;
            if (delay < DelayMin) DelayMin = delay;
            if (delay > DelayMax) DelayMax = delay;
        }

        public void NoteContact(OpeningContact c, double windUpToContact, bool opened)
        {
            Contacts[(int)c]++;
            if (opened) OpenedContacts[(int)c]++;
            if (double.IsNaN(windUpToContact) || c == OpeningContact.None) return;
            ContactSum += windUpToContact;
            ContactCount++;
            if (windUpToContact < ContactMin) ContactMin = windUpToContact;
            if (windUpToContact > ContactMax) ContactMax = windUpToContact;
        }

        public void NoteDirection(int atWindUp, int atContact)
        {
            if (!OpeningMath.IsAttack(atContact)) return;
            if (!OpeningMath.IsAttack(atWindUp)) DirUnread++;
            else if (atWindUp == atContact) DirAgree++;
            else DirDiffer++;
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
            t.Append("openings (step 106a SHADOW - nothing applied; live ").Append(r.Enabled ? "ON (not built yet)" : "OFF (" + r.OffBecause + ")")
             .Append(", the shadow log ").Append(r.ShadowOn ? "ON" : "OFF").Append("): your attacks the AI blocked correctly ").Append(BlocksTotal.ToString(c))
             .Append(" (shield ").Append(ShieldBlocks.ToString(c)).Append(", weapon ").Append(WeaponBlocks.ToString(c)).Append(") by link");
            for (int k = 1; k <= r.ChainMax; k++) t.Append(k == 1 ? " " : " / ").Append(Blocks[k].ToString(c));
            t.Append("; openings planned ").Append(Planned.ToString(c)).Append(" - forced low / high / left / right guard ")
             .Append(ForcedGuards[OpeningMath.GuardDown - 4].ToString(c)).Append(" / ").Append(ForcedGuards[OpeningMath.GuardUp - 4].ToString(c)).Append(" / ")
             .Append(ForcedGuards[OpeningMath.GuardLeft - 4].ToString(c)).Append(" / ").Append(ForcedGuards[OpeningMath.GuardRight - 4].ToString(c));
            int skipped = Skips[1] + Skips[2] + Skips[3];
            if (skipped > 0)
                t.Append("; none opened: mounted ").Append(Skips[(int)OpeningSkip.Mounted].ToString(c)).Append(", shield before link ").Append(r.ShieldMinChain.ToString(c)).Append(' ')
                 .Append(Skips[(int)OpeningSkip.ShieldTooEarly].ToString(c)).Append(", no opening your weapon can make ").Append(Skips[(int)OpeningSkip.NoOpeningForWeapon].ToString(c));
            t.Append("; your next swing: INTO the opening ").Append(Verdicts[(int)OpeningVerdict.Opened].ToString(c));
            if (Verdicts[(int)OpeningVerdict.Opened] > 0)
            {
                t.Append(" (by link");
                for (int k = 1; k <= r.ChainMax; k++) t.Append(k == 1 ? " " : " / ").Append(OpenedByK[k].ToString(c));
                t.Append(", mean ").Append((OpenedDelaySum / Verdicts[(int)OpeningVerdict.Opened]).ToString("0.00", c)).Append(" s after the block)");
            }
            t.Append(", wrong direction ").Append(Verdicts[(int)OpeningVerdict.WrongDirection].ToString(c))
             .Append(", too early ").Append(Verdicts[(int)OpeningVerdict.TooEarly].ToString(c))
             .Append(", too late ").Append(Verdicts[(int)OpeningVerdict.TooLate].ToString(c))
             .Append(", direction unread ").Append(Verdicts[(int)OpeningVerdict.UnknownDirection].ToString(c))
             .Append(", no swing ").Append(Verdicts[(int)OpeningVerdict.NoSwing].ToString(c));
            t.Append("; block -> your wind-up");
            if (DelayCount > 0)
            {
                t.Append(" mean ").Append((DelaySum / DelayCount).ToString("0.00", c)).Append(" s (").Append(DelayMin.ToString("0.00", c)).Append("-")
                 .Append(DelayMax.ToString("0.00", c)).Append("), bands <0.1 / 0.1-0.3 / 0.3-0.5 / 0.5-0.8 / 0.8-1.2 / 1.2-2 / 2+ s:");
                for (int i = 0; i < DelayBands; i++) t.Append(i == 0 ? " " : " / ").Append(DelayBand[i].ToString(c));
            }
            else t.Append(" none seen");
            t.Append("; wind-up -> contact");
            if (ContactCount > 0)
                t.Append(" mean ").Append((ContactSum / ContactCount).ToString("0.00", c)).Append(" s (").Append(ContactMin.ToString("0.00", c)).Append("-")
                 .Append(ContactMax.ToString("0.00", c)).Append(")");
            else t.Append(" none seen");
            t.Append(", it met: landed ").Append(Contacts[0].ToString(c)).Append(", blocked again ").Append(Contacts[1].ToString(c)).Append(", wrong-side shield ")
             .Append(Contacts[2].ToString(c)).Append(", someone else ").Append(Contacts[3].ToString(c)).Append(", nothing ").Append(Contacts[4].ToString(c));
            if (Verdicts[(int)OpeningVerdict.Opened] > 0)
                t.Append(" (the swings into the opening: landed ").Append(OpenedContacts[0].ToString(c)).Append(", blocked again ").Append(OpenedContacts[1].ToString(c))
                 .Append(" - nothing was forced, so his own guard still blocked those)");
            t.Append("; THE SIDE PAIRING (your attack -> his guard at contact: high / low / left / right / unread):");
            for (int a = 0; a < 4; a++)
            {
                t.Append(a == 0 ? " " : "; ").Append(OpeningMath.AttackName(a));
                for (int g = 0; g < 5; g++) t.Append(g == 0 ? " " : "/").Append(Pairs[a * 5 + g].ToString(c));
            }
            t.Append(" (sides now ").Append(r.SidesCrossed ? "CROSSED - your left swing met by his RIGHT guard" : "same-named - your left swing met by his LEFT guard").Append(')');
            t.Append("; your wind-up's direction vs the contact's: agree ").Append(DirAgree.ToString(c)).Append(", differ ").Append(DirDiffer.ToString(c))
             .Append(", unread ").Append(DirUnread.ToString(c));
            t.Append("; chain resets: hit landed ").Append(Resets[(int)OpeningReset.HitLanded].ToString(c)).Append(", gap ").Append(Resets[(int)OpeningReset.Gap].ToString(c))
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

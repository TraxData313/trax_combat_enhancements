using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 100d (Toni 2026-11-08: "add an animation where the blocker shield or weapon goes out as the attack lands making the attacker stagger
    /// cause it looks strange now - the blocker just sits while the attacker gets staggered"; and yes, the attacker's stagger should match the
    /// blocked direction; AI_NOTES "Step 100" §4, DESIGN decision 91 (d)): the two animations of a TIMED riposte, read live.
    /// <see cref="PushEnabled"/> / <see cref="RecoilEnabled"/> = the master switch FIRST, then Athletics and the impact system, then
    /// <c>TimedBlocksEnabled</c>, then the part's own switch. The test tool (<see cref="TestOn"/>) needs only the master switch and its own.
    /// </summary>
    public readonly struct TimedAnimRules
    {
        public TimedAnimRules(TraxSettings s)
        {
            ModEnabled = s.ModEnabled;
            ImpactOn = s.AthleticsEnabled && s.ImpactEnabled;
            TimedOn = s.TimedBlocksEnabled;
            PushSwitch = s.TimedBlockPushOut;
            PushForYou = s.TimedBlockPushOutForYou;
            PushMinScore = Clamp01(F(s.TimedBlockPushOutMinScore));
            PushSeconds = Math.Max(TimedPushOutMath.MinPushSeconds, Nan(F(s.TimedBlockPushOutSeconds), 0.25));
            PushStart = Math.Max(0, Math.Min(TimedPushOutMath.MaxStartProgress, Nan(F(s.TimedBlockPushOutStartProgress), 0)));
            PushGuardSeconds = Math.Max(0, Nan(F(s.TimedBlockPushOutGuardSeconds), 0.5));
            ShieldAction = s.TimedBlockPushOutShieldAction;
            OneHandedAction = s.TimedBlockPushOutOneHandedAction;
            TwoHandedAction = s.TimedBlockPushOutTwoHandedAction;
            PolearmAction = s.TimedBlockPushOutPolearmAction;
            RecoilSwitch = s.TimedBlockRecoil;
            RecoilSeconds = Math.Max(0, Nan(F(s.TimedBlockRecoilSeconds), 0.45));
            RecoilMaxShare = Clamp01(s.TimedBlockRecoilMaxPercent / 100.0);
            RecoilVariant = Math.Max(0, Math.Min(TimedRecoilMath.VariantCount - 1, s.TimedBlockRecoilVariant));
            TestSwitch = s.TimedBlockAnimTest;
            TestKey = s.TimedBlockAnimTestKey;
            HorsebackSwitch = s.TimedBlocksOnHorseback;
            PushMountedSwitch = s.TimedBlockPushOutMounted;
        }

        public static TimedAnimRules From(TraxSettings s) => new TimedAnimRules(s);

        public bool ModEnabled { get; }
        public bool ImpactOn { get; }
        public bool TimedOn { get; }
        public bool PushSwitch { get; }
        public bool PushEnabled => ModEnabled && ImpactOn && TimedOn && PushSwitch;

        /// <summary>Step 104d: <c>TimedBlocksOnHorseback</c> (the player page).</summary>
        public bool HorsebackSwitch { get; }

        /// <summary>Step 104d: <c>TimedBlockPushOutMounted</c> (Advanced).</summary>
        public bool PushMountedSwitch { get; }

        /// <summary>Step 104d - THE MOUNTED PUSH-OUT: a RIDING blocker's push-out is played - his guard's own heavy parry on channel 1 (no mounted
        /// bash exists in the game's usage sets). The push-out's chain first (the master switch FIRST), then <c>TimedBlocksOnHorseback</c>, then
        /// <c>TimedBlockPushOutMounted</c>.</summary>
        public bool PushMountedOn => PushEnabled && HorsebackSwitch && PushMountedSwitch;

        /// <summary>The push-out also on YOUR timed blocks (your guard drops for its length).</summary>
        public bool PushForYou { get; }

        /// <summary>The timing a block needs for the push-out (0 = any timed riposte; an untimed one never).</summary>
        public double PushMinScore { get; }

        /// <summary>The push-out's length in battle seconds (its speed is computed from the animation's own length).</summary>
        public double PushSeconds { get; }

        /// <summary>Where in the animation it starts (0-0.9: the windup skipped).</summary>
        public double PushStart { get; }

        /// <summary>His own bash collision ignored and his guard checked back this long (battle s; never shorter than the push itself).</summary>
        public double PushGuardSeconds { get; }

        public string ShieldAction { get; }
        public string OneHandedAction { get; }
        public string TwoHandedAction { get; }
        public string PolearmAction { get; }

        public bool RecoilSwitch { get; }
        public bool RecoilEnabled => ModEnabled && ImpactOn && TimedOn && RecoilSwitch;

        /// <summary>The direction-matched recoil's share of the timed stagger, in battle seconds (the backward stagger plays the rest).</summary>
        public double RecoilSeconds { get; }

        /// <summary>The recoil takes at most this share of the whole stagger (0-1).</summary>
        public double RecoilMaxShare { get; }

        /// <summary>0 the "blocked" recoil, 1 the "quick blocked", 2 / 3 their "balanced" forms (<see cref="TimedRecoilMath.VariantName"/>).</summary>
        public int RecoilVariant { get; }

        public bool TestSwitch { get; }
        public bool TestOn => ModEnabled && TestSwitch;

        /// <summary>The test tool's key - its index in <see cref="KeyNames.Keyboard"/>.</summary>
        public string TestKey { get; }

        public string? PushOffBecause => !ModEnabled ? "ModEnabled" : !ImpactOn ? "the impact system off" : !TimedOn ? "TimedBlocksEnabled" : !PushSwitch ? "TimedBlockPushOut" : null;

        public string? RecoilOffBecause => !ModEnabled ? "ModEnabled" : !ImpactOn ? "the impact system off" : !TimedOn ? "TimedBlocksEnabled" : !RecoilSwitch ? "TimedBlockRecoil" : null;

        public string Describe()
        {
            var c = CultureInfo.InvariantCulture;
            string push = PushEnabled
                ? "push-out ON (" + (PushForYou ? "you too" : "not on you") + ", timing " + PushMinScore.ToString("0.00", c) + "+): shield " + Show(ShieldAction) + ", one-handed "
                  + Show(OneHandedAction) + ", two-handed " + Show(TwoHandedAction) + ", polearm " + Show(PolearmAction) + " - " + PushSeconds.ToString("0.00", c) + " s from "
                  + PushStart.ToString("0.00", c) + ", his own bash ignored " + PushGuardSeconds.ToString("0.00", c) + " s; a mounted blocker "
                  + (PushMountedOn ? "his guard's heavy parry (step 104d)" : "none (" + (!HorsebackSwitch ? "TimedBlocksOnHorseback" : "TimedBlockPushOutMounted") + " off)")
                : "push-out OFF (" + PushOffBecause + ")";
            string recoil = RecoilEnabled
                ? "recoil ON: the " + TimedRecoilMath.VariantName(RecoilVariant) + " recoil for " + RecoilSeconds.ToString("0.00", c) + " s (at most "
                  + (100 * RecoilMaxShare).ToString("0", c) + "% of the stagger), then the backward stagger"
                : "recoil OFF (" + RecoilOffBecause + ")";
            return push + "; " + recoil;
        }

        private static string Show(string a) => string.IsNullOrEmpty(a) ? "(none)" : a;

        private static double Clamp01(double v) => double.IsNaN(v) ? 0 : Math.Max(0, Math.Min(1, v));

        private static double Nan(double v, double fallback) => double.IsNaN(v) || double.IsInfinity(v) ? fallback : v;

        /// <summary>A float setting as the decimal it was written as (0.5f must compare as 0.5).</summary>
        private static double F(float v) => float.IsNaN(v) ? double.NaN : Math.Round((double)v, 5);
    }

    /// <summary>Step 100d: what the blocker pushes out with.</summary>
    public enum PushOutKind : byte
    {
        None,
        Shield,
        OneHanded,
        TwoHanded,
        Polearm,
    }

    /// <summary>Step 100d: why a timed riposte's blocker did NOT push out (<see cref="Play"/> = he did). In the order asked.</summary>
    public enum PushOutSkip : byte
    {
        Play,

        /// <summary>The rules off (the master switch, the impact system, timed blocks, the push-out's switch).</summary>
        Off,

        /// <summary>An untimed riposte (score 0) or below <c>TimedBlockPushOutMinScore</c>.</summary>
        LowScore,

        /// <summary>No blocker on the field any more.</summary>
        Gone,

        /// <summary>Your block and <c>TimedBlockPushOutForYou</c> off.</summary>
        NotForYou,

        /// <summary>He rides and the mounted push-out is off (<c>TimedBlocksOnHorseback</c> / <c>TimedBlockPushOutMounted</c>) - never touched.</summary>
        Mounted,

        /// <summary>His upper body is not guarding any more (he attacks / reloads already).</summary>
        NotGuarding,

        /// <summary>Already pushing out.</summary>
        Busy,

        /// <summary>No action for his kind (the setting empty, no weapon) or none of the names exists in his action set.</summary>
        NoAction,

        /// <summary>The engine refused the action.</summary>
        Refused,

        /// <summary>Step 104d: he rides and his guard has no heavy parry in his action set (or no defend action is on his upper body to take it
        /// from) - skipped, counted apart. Never a bash on a rider.</summary>
        MountedNoParry,
    }

    /// <summary>
    /// Step 100d - THE BLOCKER'S PUSH-OUT (AI_NOTES "Step 100" §4): on the blocker's channel 1 (the upper body; the legs keep the stance) his
    /// shield's / weapon's own bash, its windup skipped (<c>TimedBlockPushOutStartProgress</c>), sped up to <c>TimedBlockPushOutSeconds</c>; or
    /// <see cref="ParryHeavy"/> = his guard's own <c>_parry_heavy</c> defend action (a defend - no collision at all). Pure: the choice, the speed,
    /// the gates, the window.
    /// </summary>
    public static class TimedPushOutMath
    {
        /// <summary>The setting's word for "his guard's own heavy parry" instead of an action name.</summary>
        public const string ParryHeavy = "parry_heavy";

        public const double MinPushSeconds = 0.05;
        public const double MaxStartProgress = 0.9;
        public const float MinSpeed = 0.25f;
        public const float MaxSpeed = 4f;

        /// <summary>The window after the push's own end within which his guard is checked back (battle s).</summary>
        public const double GuardMargin = 0.1;

        /// <summary>The test tool's push-out candidates, in its order (each tried with "_left_stance" first in a left stance).</summary>
        public static readonly string[] Candidates =
        {
            "act_shield_bash", "act_hand_shield_bash", "act_1h_bash", "act_2h_bash", "act_staff_bash", ParryHeavy,
        };

        /// <summary>His kind: a right-side SHIELD block pushes the shield out; a parry his weapon (by its item usage id: "onehanded…" / a
        /// flail / a dagger = one-handed, "twohanded…" = two-handed, "polearm…" = polearm); nothing in hand = none.</summary>
        public static PushOutKind KindOf(bool shieldBlock, string? usageId)
        {
            if (shieldBlock) return PushOutKind.Shield;
            if (string.IsNullOrEmpty(usageId)) return PushOutKind.None;
            string u = usageId!;
            if (u.StartsWith("twohanded", StringComparison.Ordinal)) return PushOutKind.TwoHanded;
            if (u.StartsWith("polearm", StringComparison.Ordinal) || u.StartsWith("onehanded_polearm", StringComparison.Ordinal)) return PushOutKind.Polearm;
            if (u.StartsWith("onehanded", StringComparison.Ordinal) || u.StartsWith("flail", StringComparison.Ordinal)) return PushOutKind.OneHanded;
            return PushOutKind.None;
        }

        /// <summary>The chosen action (a name or <see cref="ParryHeavy"/>) for his kind; "" = none.</summary>
        public static string ActionFor(PushOutKind kind, in TimedAnimRules r) => kind switch
        {
            PushOutKind.Shield => r.ShieldAction ?? string.Empty,
            PushOutKind.OneHanded => r.OneHandedAction ?? string.Empty,
            PushOutKind.TwoHanded => r.TwoHandedAction ?? string.Empty,
            PushOutKind.Polearm => r.PolearmAction ?? string.Empty,
            _ => string.Empty,
        };

        /// <summary>The heavy parry of the defend action <paramref name="defendName"/> ("act_defend_up_1h_active" → "act_defend_up_1h_parry_heavy",
        /// "act_defend_shield_right_1h_passive_down" → "act_defend_shield_right_1h_parry_heavy_down"); null when it is not a defend action.</summary>
        public static string? ParryHeavyOf(string? defendName)
        {
            if (string.IsNullOrEmpty(defendName) || !defendName!.StartsWith("act_defend_", StringComparison.Ordinal)) return null;
            foreach (var token in DefendTokens)
            {
                int i = defendName.IndexOf(token, StringComparison.Ordinal);
                if (i < 0) continue;
                int end = i + token.Length;
                if (end < defendName.Length && defendName[end] != '_') continue; // "_active" must be a whole word
                return defendName.Substring(0, i) + "_parry_heavy" + defendName.Substring(end);
            }
            return null;
        }

        private static readonly string[] DefendTokens = { "_parry_heavy", "_parry_light", "_passive", "_active" };

        /// <summary>The push's speed: the animation's part from <paramref name="start"/> on, played in <paramref name="seconds"/> - within
        /// <see cref="MinSpeed"/>-<see cref="MaxSpeed"/>; an unknown length (NaN / 0) counts as 1 s.</summary>
        public static float Speed(double animSeconds, double start, double seconds)
        {
            double len = double.IsNaN(animSeconds) || animSeconds <= 0 ? 1 : animSeconds;
            double part = len * (1 - Math.Max(0, Math.Min(MaxStartProgress, double.IsNaN(start) ? 0 : start)));
            double s = seconds > 0 ? part / seconds : MaxSpeed;
            if (double.IsNaN(s)) return 1f;
            return (float)Math.Max(MinSpeed, Math.Min(MaxSpeed, s));
        }

        /// <summary>How long it really plays at <paramref name="speed"/> (battle s).</summary>
        public static double PlaySeconds(double animSeconds, double start, float speed)
        {
            double len = double.IsNaN(animSeconds) || animSeconds <= 0 ? 1 : animSeconds;
            return len * (1 - Math.Max(0, Math.Min(MaxStartProgress, start))) / Math.Max(MinSpeed, speed);
        }

        /// <summary>The window his own bash collision is ignored and his guard checked back: the push's own length + a margin, never
        /// shorter than <c>TimedBlockPushOutGuardSeconds</c>.</summary>
        public static double GuardWindow(in TimedAnimRules r, double playSeconds) => Math.Max(r.PushGuardSeconds, (double.IsNaN(playSeconds) ? 0 : playSeconds) + GuardMargin);

        /// <summary>The gates in order (the engine reads - guarding, busy - asked only when the cheap ones pass). A RIDING blocker plays only
        /// with <see cref="TimedAnimRules.PushMountedOn"/> (step 104d - then always his guard's heavy parry, <see cref="MountedAction"/>).</summary>
        public static PushOutSkip Decide(in TimedAnimRules r, double score, bool blockerOnField, bool yours, bool mounted, bool guarding, bool busy)
        {
            if (!r.PushEnabled) return PushOutSkip.Off;
            if (!(score > 0) || score + 1e-9 < r.PushMinScore) return PushOutSkip.LowScore;
            if (!blockerOnField) return PushOutSkip.Gone;
            if (yours && !r.PushForYou) return PushOutSkip.NotForYou;
            if (mounted && !r.PushMountedOn) return PushOutSkip.Mounted;
            if (busy) return PushOutSkip.Busy;
            if (!guarding) return PushOutSkip.NotGuarding;
            return PushOutSkip.Play;
        }

        /// <summary>Step 104d: a RIDING blocker's push-out is always his guard's own heavy parry (a defend action the engine plays on riders -
        /// <c>act_defend_shield_*_parry_heavy*</c>, <c>act_defend_*_staff_parry_heavy_horseback</c>, the 1h / 2h ones), whatever his kind's
        /// setting says (the bashes are foot usages only).</summary>
        public static string MountedAction => ParryHeavy;

        /// <summary>The action a blocker pushes out with: a rider's heavy parry (step 104d), else his kind's setting.</summary>
        public static string ActionFor(PushOutKind kind, in TimedAnimRules r, bool mounted) => mounted ? MountedAction : ActionFor(kind, in r);

        public static string KindName(PushOutKind k) => k switch
        {
            PushOutKind.Shield => "shield",
            PushOutKind.OneHanded => "one-handed",
            PushOutKind.TwoHanded => "two-handed",
            PushOutKind.Polearm => "polearm",
            _ => "none",
        };

        public static string SkipName(PushOutSkip s) => s switch
        {
            PushOutSkip.Play => "played",
            PushOutSkip.Off => "off",
            PushOutSkip.LowScore => "timing too low",
            PushOutSkip.Gone => "blocker gone",
            PushOutSkip.NotForYou => "not on you (switch)",
            PushOutSkip.Mounted => "mounted (the mounted push-out off)",
            PushOutSkip.NotGuarding => "not guarding any more",
            PushOutSkip.Busy => "already pushing",
            PushOutSkip.NoAction => "no action for his kind",
            PushOutSkip.Refused => "refused by the engine",
            PushOutSkip.MountedNoParry => "mounted - no heavy parry for his guard",
            _ => "?",
        };
    }

    /// <summary>Step 100d: the attacker's weapon as the game's "blocked" recoils name it.</summary>
    public enum RecoilClass : byte
    {
        None,
        OneHanded,
        Dagger,
        Flail,
        TwoHanded,
        Staff,
        Spear,
        Lance,
        Fist,
        FistShield,
    }

    /// <summary>Step 100d: why a timed riposte played the old single stagger instead of the recoil first.</summary>
    public enum RecoilFallback : byte
    {
        /// <summary>The recoil was played (not a fallback).</summary>
        None,

        /// <summary>The recoil switch off (or the rules).</summary>
        Off,

        /// <summary>His attack's direction not read at contact (or too old).</summary>
        NoDirection,

        /// <summary>No recoil for his weapon / direction (a couched lance, a bracing pike, a dagger's thrust...).</summary>
        NoClass,

        /// <summary>None of the names exists in his action set.</summary>
        NoAction,

        /// <summary>The stagger too short to split (the recoil or the rest under its floor).</summary>
        TooShort,

        /// <summary>The engine refused the recoil (the single stagger played instead).</summary>
        Refused,
    }

    /// <summary>Step 100d: the split of one timed stagger - the recoil first, then the backward stagger.</summary>
    public readonly struct RecoilPlan
    {
        public RecoilPlan(bool use, double recoilSeconds, float recoilSpeed, double staggerSeconds)
        {
            Use = use;
            RecoilSeconds = recoilSeconds;
            RecoilSpeed = recoilSpeed;
            StaggerSeconds = staggerSeconds;
        }

        public bool Use { get; }

        /// <summary>The recoil's real length at its speed (battle s) - the backward stagger starts then.</summary>
        public double RecoilSeconds { get; }

        public float RecoilSpeed { get; }

        /// <summary>The backward stagger's asked length = the whole − the recoil.</summary>
        public double StaggerSeconds { get; }
    }

    /// <summary>
    /// Step 100d - THE DIRECTION-MATCHED RECOIL (Toni: "Yes" - the stagger matches the blocked direction): the game's own "blocked" recoil of the
    /// attacker's swing - <c>act_[quick_]blocked_&lt;overswing|thrust|slashleft|slashright…&gt;_&lt;1h|2h|staff…&gt;[_balanced][_left_stance]</c>, the
    /// names exactly as item_usage_sets.xml (v1.4.8) wires them for a man on foot - played first, slowed to <c>TimedBlockRecoilSeconds</c>, then
    /// the backward stagger for the rest of the asked length (the whole unchanged). Pure: the class, the names, the split.
    /// </summary>
    public static class TimedRecoilMath
    {
        /// <summary>The engine's attack directions (<c>Agent.UsageDirection</c>): up = an overhead swing, down = a thrust, left / right =
        /// the side swings.</summary>
        public const int Up = 0, Down = 1, Left = 2, Right = 3, DirectionCount = 4;

        /// <summary>0 blocked, 1 quick blocked, 2 blocked balanced, 3 quick blocked balanced.</summary>
        public const int VariantCount = 4;

        public const double MinRecoilSeconds = 0.1;
        public const double MinStaggerSeconds = 0.25;
        public const float MinSpeed = 0.2f;
        public const float MaxSpeed = 2.5f;

        /// <summary>A direction read at contact older than this is not used (battle s).</summary>
        public const double DirectionMaxAge = 1.0;

        private const int ClassCount = 10;
        private static readonly string[] Empty = new string[0]; // before the table (static fields initialise in text order)
        private static readonly string[][][][] Table = Build(); // [class][direction][variant*2 + leftStance] → candidates, best first

        /// <summary>His class from the wielded weapon's item usage id (and whether a shield is in his other hand - a polearm held with a
        /// shield thrusts like a lance; fists with a shield have their own recoils). Null / "" / "no_weapon" = fists.</summary>
        public static RecoilClass ClassOf(string? usageId, bool shieldInHand)
        {
            if (string.IsNullOrEmpty(usageId) || usageId == "no_weapon") return shieldInHand ? RecoilClass.FistShield : RecoilClass.Fist;
            string u = usageId!;
            if (u == "onehanded_shield_dagger") return RecoilClass.Dagger;
            if (u.StartsWith("flail", StringComparison.Ordinal)) return RecoilClass.Flail;
            if (u.StartsWith("onehanded_polearm", StringComparison.Ordinal)) return RecoilClass.Lance;
            if (u.StartsWith("onehanded", StringComparison.Ordinal)) return RecoilClass.OneHanded;
            if (u.StartsWith("twohanded", StringComparison.Ordinal)) return RecoilClass.TwoHanded;
            if (u == "polearm_bracing" || u == "polearm_couch") return RecoilClass.None; // their own single "parried" action
            if (u == "polearm_block_thrust" || u == "polearm_pike") return RecoilClass.Spear;
            if (u == "polearm_block_shield_thrust") return shieldInHand ? RecoilClass.Lance : RecoilClass.Spear;
            if (u.StartsWith("polearm", StringComparison.Ordinal)) return shieldInHand && u.Contains("thrust") ? RecoilClass.Lance : RecoilClass.Staff;
            return RecoilClass.None;
        }

        /// <summary>The names to try for this recoil, best first (the chosen variant in his stance, the same without the stance, the plain
        /// "blocked" one in his stance, without); an empty array when his class has none for that direction (then the old single stagger).
        /// A precomputed table - nothing allocated.</summary>
        public static string[] Names(RecoilClass cls, int direction, int variant, bool leftStance)
        {
            if ((int)cls <= 0 || (int)cls >= ClassCount || direction < 0 || direction >= DirectionCount) return Empty;
            if (variant < 0 || variant >= VariantCount) variant = 0;
            return Table[(int)cls][direction][variant * 2 + (leftStance ? 1 : 0)];
        }

        /// <summary>The base name (no "act_[quick_]blocked_", no variant, no stance): "overswing_1h", "thrust_staff"...; null = none.</summary>
        public static string? Core(RecoilClass cls, int direction)
        {
            switch (cls)
            {
                case RecoilClass.OneHanded: return direction switch { Up => "overswing_1h", Down => "thrust_1h", Left => "slashleft_1h", Right => "slashright_1h", _ => null };
                case RecoilClass.Dagger: return direction switch { Up => "backstab_1h", Left => "slashleft_dagger", Right => "slashright_dagger", _ => null };
                case RecoilClass.Flail: return direction switch { Up => "overswing_flail", Left => "slashleft_flail", Right => "slashright_flail", _ => null };
                case RecoilClass.TwoHanded: return direction switch { Up => "overswing_2h", Down => "thrust_2h", Left => "slashleft_2h", Right => "slashright_2h", _ => null };
                case RecoilClass.Staff: return direction switch { Up => "overswing_staff", Down => "thrust_staff", Left => "slashleft_staff", Right => "slashright_staff", _ => null };
                case RecoilClass.Spear: return direction switch { Up => "overswing_spear", Down => "thrust_staff", _ => null };
                case RecoilClass.Lance: return direction switch { Up => "upperthrust_1h_lance", Down => "thrust_1h_lance", _ => null };
                case RecoilClass.Fist: return direction switch { Up => "direct_fist", Down => "uppercut_fist", Left => "swingleft_fist", Right => "swingright_fist", _ => null };
                case RecoilClass.FistShield: return direction switch { Up => "direct_fist_shield", Down => "uppercut_fist_shield", Left => "swingleft_fist_shield", Right => "swingright_fist_shield", _ => null };
                default: return null;
            }
        }

        /// <summary>One full name: <c>act_[quick_]blocked_&lt;core&gt;[_balanced][_left_stance]</c> (a dagger's side swings spell the stance
        /// "_leftstance", as the game does).</summary>
        public static string Name(RecoilClass cls, string core, int variant, bool leftStance)
        {
            bool quick = variant == 1 || variant == 3;
            bool balanced = variant >= 2 && cls != RecoilClass.Dagger;
            string stance = !leftStance ? string.Empty : cls == RecoilClass.Dagger && core.EndsWith("_dagger", StringComparison.Ordinal) ? "_leftstance" : "_left_stance";
            return (quick ? "act_quick_blocked_" : "act_blocked_") + core + (balanced ? "_balanced" : string.Empty) + stance;
        }

        /// <summary>The split: the recoil gets min(<c>TimedBlockRecoilSeconds</c>, the whole × the max share), its speed = its own length ÷
        /// that (within <see cref="MinSpeed"/>-<see cref="MaxSpeed"/>, its real length recomputed), the backward stagger the rest. Not used
        /// (the old single stagger) when the recoil or the rest would be under its floor, or a length is unknown.</summary>
        public static RecoilPlan Plan(double totalSeconds, double recoilAnimSeconds, in TimedAnimRules r)
        {
            if (!(totalSeconds > 0) || !(recoilAnimSeconds > 0) || double.IsInfinity(totalSeconds)) return default;
            double want = Math.Min(r.RecoilSeconds, totalSeconds * r.RecoilMaxShare);
            if (!(want >= MinRecoilSeconds)) return default;
            double s = recoilAnimSeconds / want;
            float speed = (float)Math.Max(MinSpeed, Math.Min(MaxSpeed, s));
            double real = recoilAnimSeconds / speed;
            double rest = totalSeconds - real;
            if (real < MinRecoilSeconds - 1e-9 || rest < MinStaggerSeconds - 1e-9) return default;
            return new RecoilPlan(true, real, speed, rest);
        }

        /// <summary>The direction from the engine's attack direction at contact (-1 = none / not an attack direction).</summary>
        public static int DirectionOf(int usageDirection) => usageDirection >= Up && usageDirection <= Right ? usageDirection : -1;

        public static string DirectionName(int d) => d switch
        {
            Up => "overhead",
            Down => "thrust",
            Left => "left swing",
            Right => "right swing",
            _ => "unknown",
        };

        public static string VariantName(int v) => v switch
        {
            1 => "quick blocked",
            2 => "blocked balanced",
            3 => "quick blocked balanced",
            _ => "blocked",
        };

        public static string ClassName(RecoilClass c) => c switch
        {
            RecoilClass.OneHanded => "one-handed",
            RecoilClass.Dagger => "dagger",
            RecoilClass.Flail => "flail",
            RecoilClass.TwoHanded => "two-handed",
            RecoilClass.Staff => "staff",
            RecoilClass.Spear => "spear",
            RecoilClass.Lance => "lance (one-handed polearm)",
            RecoilClass.Fist => "fists",
            RecoilClass.FistShield => "fists + shield",
            _ => "none",
        };

        public static string FallbackName(RecoilFallback f) => f switch
        {
            RecoilFallback.Off => "off",
            RecoilFallback.NoDirection => "no direction read",
            RecoilFallback.NoClass => "no recoil for his weapon / direction",
            RecoilFallback.NoAction => "not in his action set",
            RecoilFallback.TooShort => "the stagger too short to split",
            RecoilFallback.Refused => "refused by the engine",
            _ => "none",
        };

        private static string[][][][] Build()
        {
            var t = new string[ClassCount][][][];
            for (int c = 0; c < ClassCount; c++)
            {
                t[c] = new string[DirectionCount][][];
                for (int d = 0; d < DirectionCount; d++)
                {
                    t[c][d] = new string[VariantCount * 2][];
                    string? core = Core((RecoilClass)c, d);
                    for (int v = 0; v < VariantCount; v++)
                        for (int l = 0; l < 2; l++)
                        {
                            if (core == null)
                            {
                                t[c][d][v * 2 + l] = Empty;
                                continue;
                            }
                            var list = new List<string>(4);
                            void Add(string n)
                            {
                                if (!list.Contains(n)) list.Add(n);
                            }
                            var cls = (RecoilClass)c;
                            if (l == 1) Add(Name(cls, core, v, true));
                            Add(Name(cls, core, v, false));
                            if (l == 1) Add(Name(cls, core, 0, true));
                            Add(Name(cls, core, 0, false));
                            t[c][d][v * 2 + l] = list.ToArray();
                        }
                }
            }
            return t;
        }
    }

    /// <summary>Step 100d: the [summary] lines - push-outs played / skipped by reason, the bash collisions ignored, the guards checked back;
    /// recoils by direction, the fallbacks to the single stagger by reason; the test tool's plays.</summary>
    public sealed class TimedAnimStats
    {
        public readonly int[] PushPlayed = new int[5];          // by PushOutKind
        public readonly int[] PushSkipped = new int[SkipCount];  // by PushOutSkip

        /// <summary>Step 104d: the push-outs of RIDING blockers - played (yours), skipped by <see cref="PushOutSkip"/>.</summary>
        public readonly int[] MountedSkipped = new int[SkipCount];
        public int MountedPlayed, MountedYou;

        /// <summary>The <see cref="PushOutSkip"/> values (the arrays' length).</summary>
        public const int SkipCount = (int)PushOutSkip.MountedNoParry + 1;
        public int PushYou, PushParryHeavy, PushLeftStance, PushFellBackToParryHeavy;
        public int BashBlowsIgnored, BashHitsSkipped, BashStartsSkipped;
        public int GuardsBack, Unstuck, PoolFull;
        public readonly int[] RecoilPlayed = new int[TimedRecoilMath.DirectionCount];
        public readonly int[] RecoilFell = new int[7];          // by RecoilFallback
        public int RecoilSlowedInPlace, RecoilStaggerPlayed, RecoilStaggerRefused, RecoilLeftField, RecoilChainFull;
        public double RecoilSecondsSum, RecoilStaggerSecondsSum;
        public int TestPlays, TestMissing;

        public void NotePush(PushOutKind kind, bool you, bool parryHeavy, bool leftStance, bool fellBack, bool mounted = false)
        {
            if (mounted)
            {
                MountedPlayed++;
                if (you) MountedYou++;
            }
            PushPlayed[(int)kind]++;
            if (you) PushYou++;
            if (parryHeavy) PushParryHeavy++;
            if (leftStance) PushLeftStance++;
            if (fellBack) PushFellBackToParryHeavy++;
        }

        public void NoteSkip(PushOutSkip s, bool mounted = false)
        {
            int i = (int)s;
            if (i < 0 || i >= PushSkipped.Length) return;
            PushSkipped[i]++;
            if (mounted) MountedSkipped[i]++;
        }

        /// <summary>Step 104d: the mounted part of the push-out line ("; mounted blockers (step 104d, his guard's heavy parry - on): played 2
        /// (yours 1), skipped: ...").</summary>
        public void AppendMounted(StringBuilder sb, in TimedAnimRules r)
        {
            var c = CultureInfo.InvariantCulture;
            sb.Append("; mounted blockers (step 104d, his guard's heavy parry - ").Append(r.PushMountedOn ? "on" : "off").Append("): played ")
              .Append(MountedPlayed.ToString(c)).Append(" (yours ").Append(MountedYou.ToString(c)).Append("), skipped: ");
            bool any = false;
            for (int i = 1; i < MountedSkipped.Length; i++)
            {
                if (MountedSkipped[i] == 0) continue;
                if (any) sb.Append(", ");
                sb.Append(TimedPushOutMath.SkipName((PushOutSkip)i)).Append(' ').Append(MountedSkipped[i].ToString(c));
                any = true;
            }
            if (!any) sb.Append("none");
        }

        public void NoteRecoil(int direction, double recoilSeconds, double staggerSeconds, bool inPlace)
        {
            if (direction >= 0 && direction < RecoilPlayed.Length) RecoilPlayed[direction]++;
            RecoilSecondsSum += recoilSeconds;
            RecoilStaggerSecondsSum += staggerSeconds;
            if (inPlace) RecoilSlowedInPlace++;
        }

        public void NoteFallback(RecoilFallback f)
        {
            int i = (int)f;
            if (i > 0 && i < RecoilFell.Length) RecoilFell[i]++;
        }

        public int PushTotal
        {
            get
            {
                int n = 0;
                foreach (var v in PushPlayed) n += v;
                return n;
            }
        }

        public int RecoilTotal
        {
            get
            {
                int n = 0;
                foreach (var v in RecoilPlayed) n += v;
                return n;
            }
        }

        public string[] SummaryLines(in TimedAnimRules r)
        {
            var lines = new string[2];
            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("timed push-out (step 100d): ").Append(r.PushEnabled ? "on" : "off (" + r.PushOffBecause + ")")
              .Append(" - played ").Append(PushTotal.ToString(c))
              .Append(" (shield ").Append(PushPlayed[(int)PushOutKind.Shield].ToString(c))
              .Append(" / one-handed ").Append(PushPlayed[(int)PushOutKind.OneHanded].ToString(c))
              .Append(" / two-handed ").Append(PushPlayed[(int)PushOutKind.TwoHanded].ToString(c))
              .Append(" / polearm ").Append(PushPlayed[(int)PushOutKind.Polearm].ToString(c))
              .Append("; yours ").Append(PushYou.ToString(c)).Append(", as the guard's heavy parry ").Append(PushParryHeavy.ToString(c))
              .Append(" (of them the bash missing ").Append(PushFellBackToParryHeavy.ToString(c)).Append("), left stance ").Append(PushLeftStance.ToString(c))
              .Append("); skipped: ");
            bool any = false;
            for (int i = 1; i < PushSkipped.Length; i++)
            {
                if (PushSkipped[i] == 0) continue;
                if (any) sb.Append(", ");
                sb.Append(TimedPushOutMath.SkipName((PushOutSkip)i)).Append(' ').Append(PushSkipped[i].ToString(c));
                any = true;
            }
            if (!any) sb.Append("none");
            sb.Append("; their own bash collisions ignored ").Append(BashBlowsIgnored.ToString(c)).Append(" (the engine's blow) / ").Append(BashHitsSkipped.ToString(c))
              .Append(" (our hit systems) / ").Append(BashStartsSkipped.ToString(c)).Append(" (our kick-and-bash start: no cost, no meet read) - want 0 blows landing")
              .Append("; guards back ").Append(GuardsBack.ToString(c)).Append(", a push still playing at its window's end cleared ").Append(Unstuck.ToString(c))
              .Append(", pool full ").Append(PoolFull.ToString(c));
            AppendMounted(sb, in r);
            lines[0] = sb.ToString();

            sb.Clear();
            int rec = RecoilTotal;
            sb.Append("timed recoils (step 100d): ").Append(r.RecoilEnabled ? "on, the " + TimedRecoilMath.VariantName(r.RecoilVariant) + " recoil" : "off (" + r.RecoilOffBecause + ")")
              .Append(" - played ").Append(rec.ToString(c)).Append(" (overhead ").Append(RecoilPlayed[TimedRecoilMath.Up].ToString(c))
              .Append(" / thrust ").Append(RecoilPlayed[TimedRecoilMath.Down].ToString(c))
              .Append(" / left swing ").Append(RecoilPlayed[TimedRecoilMath.Left].ToString(c))
              .Append(" / right swing ").Append(RecoilPlayed[TimedRecoilMath.Right].ToString(c))
              .Append("; the engine's own recoil slowed in place ").Append(RecoilSlowedInPlace.ToString(c))
              .Append("), mean ").Append(rec > 0 ? (RecoilSecondsSum / rec).ToString("0.00", c) : "-").Append(" s recoil + ")
              .Append(rec > 0 ? (RecoilStaggerSecondsSum / rec).ToString("0.00", c) : "-").Append(" s backward stagger; then the stagger played ")
              .Append(RecoilStaggerPlayed.ToString(c)).Append(", refused ").Append(RecoilStaggerRefused.ToString(c)).Append(", he left the field ").Append(RecoilLeftField.ToString(c))
              .Append(", chain pool full ").Append(RecoilChainFull.ToString(c)).Append("; fell back to the single stagger: ");
            any = false;
            for (int i = 1; i < RecoilFell.Length; i++)
            {
                if (RecoilFell[i] == 0) continue;
                if (any) sb.Append(", ");
                sb.Append(TimedRecoilMath.FallbackName((RecoilFallback)i)).Append(' ').Append(RecoilFell[i].ToString(c));
                any = true;
            }
            if (!any) sb.Append("none");
            sb.Append("; untimed ripostes keep step 56's single stagger");
            if (TestPlays > 0 || TestMissing > 0) sb.Append("; the test tool played ").Append(TestPlays.ToString(c)).Append(" (").Append(TestMissing.ToString(c)).Append(" not in the action set)");
            lines[1] = sb.ToString();
            return lines;
        }
    }
}

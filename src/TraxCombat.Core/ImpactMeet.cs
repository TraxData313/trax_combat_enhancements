using System;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>Step 82: what the defender held up at the moment a kick or bash met him (read from his channel-1 action at contact).</summary>
    public enum MeetGuardKind : byte
    {
        /// <summary>Not read (the read failed, a man not tracked).</summary>
        Unknown,

        /// <summary>No guard up (idle, walking, a reaction).</summary>
        None,

        /// <summary>He was attacking (a wind-up, a release, his own kick or bash, a shot).</summary>
        Swinging,

        /// <summary>His shield raised (<c>ActionCodeType.DefendShield</c>).</summary>
        Shield,

        /// <summary>A weapon guard (<c>DefendForward/Up/Right/Left</c> 1h / 2h / staff, <c>DefendFist</c>).</summary>
        Weapon,
    }

    /// <summary>Step 82: the guard's direction (the engine's <c>Agent.UsageDirection</c> Defend* - the side the player's mouse picks).</summary>
    public enum MeetDir : byte
    {
        Unknown,
        Up,
        Down,
        Left,
        Right,
    }

    /// <summary>Step 82: how a kick or bash was MET - the row of IMPACT §3.5's meet table (Toni 2026-10-22).</summary>
    public enum ImpactMeet : byte
    {
        /// <summary>Not judged: the meet rules off, not a kick / bash, a rider - the step 50 / 65 decision as it was.</summary>
        NotJudged,

        /// <summary>A bash met by a raised shield, guard RIGHT - the correct meet: the BASHER is at risk.</summary>
        BashShieldRight,

        /// <summary>A bash met by a weapon guard to the right - the correct meet: the basher is at risk.</summary>
        BashWeaponRight,

        /// <summary>A bash met by a raised shield in any other direction - the blocker is at risk.</summary>
        BashShieldWrong,

        /// <summary>A bash met by a weapon guard in any other direction - the blocker is at risk.</summary>
        BashWeaponWrong,

        /// <summary>A bash on a man with no guard up, or caught mid-swing - he is at risk.</summary>
        BashOpen,

        /// <summary>A kick met by a raised shield, guard DOWN - the correct meet: the KICKER is at risk.</summary>
        KickShieldDown,

        /// <summary>A kick met by a weapon guard down - the correct meet: the kicker is at risk.</summary>
        KickWeaponDown,

        /// <summary>A kick met any other way - the kicked man as before (step 65's brace, then the reel / fall / shield drop).</summary>
        KickOther,

        /// <summary>Step 88: a weapon PUSH (a man without a shield, his guard up and the kick key) met by a raised shield guarding UP - the
        /// correct meet: the PUSHER is at risk (the ceilings of a bash met by a shield guarding right).</summary>
        PushShieldUp,

        /// <summary>Step 88: a push met by a weapon guard UP - the correct meet: the pusher is at risk (a bash's weapon-right ceilings).</summary>
        PushWeaponUp,

        /// <summary>Step 88: a push met by a raised shield in any other direction - the blocker is at risk (a bash's shield-wrong row).</summary>
        PushShieldWrong,

        /// <summary>Step 88: a push met by a weapon guard in any other direction - the blocker is at risk (a bash's weapon-wrong row).</summary>
        PushWeaponWrong,

        /// <summary>Step 88: a push on a man with no guard up, or caught mid-swing - he is at risk (a bash's open row).</summary>
        PushOpen,
    }

    /// <summary>
    /// Step 82 (Toni 2026-10-22, "a valid way to transfer your body's kinetic energy ... if met properly the initiator should be caught off
    /// balance"): the meet rules, read live. <see cref="Enabled"/> = the master switch FIRST, then <c>ImpactMeetRulesEnabled</c> (the impact
    /// model's own gates - Athletics, ImpactEnabled, the kicks' family - come first in its decision). Each ceiling 0-1.
    /// </summary>
    public readonly struct ImpactMeetRules
    {
        public ImpactMeetRules(TraxSettings s)
        {
            ModEnabled = s.ModEnabled;
            On = s.ImpactMeetRulesEnabled;
            BashShieldRightStagger = P(s.MeetBashShieldRightStaggerPercent);
            BashShieldRightFall = P(s.MeetBashShieldRightFallPercent);
            BashWeaponRightStagger = P(s.MeetBashWeaponRightStaggerPercent);
            BashWeaponRightFall = P(s.MeetBashWeaponRightFallPercent);
            BashShieldWrongStagger = P(s.MeetBashShieldWrongStaggerPercent);
            BashShieldWrongFall = P(s.MeetBashShieldWrongFallPercent);
            BashShieldWrongDrop = P(s.MeetBashShieldWrongShieldDropPercent);
            BashWeaponWrongStagger = P(s.MeetBashWeaponWrongStaggerPercent);
            BashWeaponWrongFall = P(s.MeetBashWeaponWrongFallPercent);
            BashOpenStagger = P(s.MeetBashOpenStaggerPercent);
            BashOpenFall = P(s.MeetBashOpenFallPercent);
            BashOpenDrop = P(s.MeetBashOpenShieldDropPercent);
            KickShieldDownStagger = P(s.MeetKickShieldDownStaggerPercent);
            KickShieldDownFall = P(s.MeetKickShieldDownFallPercent);
            KickWeaponDownStagger = P(s.MeetKickWeaponDownStaggerPercent);
            KickWeaponDownFall = P(s.MeetKickWeaponDownFallPercent);
            ReadScale = Math.Max(0, s.MeetAiReadScalePercent / 100.0);
            ReactionSeconds = Math.Max(0, (double)s.MeetAiReadReactionSeconds);
            HoldSeconds = Math.Max(0.1, (double)s.MeetAiReadMaxHoldSeconds);
        }

        public static ImpactMeetRules From(TraxSettings s) => new ImpactMeetRules(s);

        public bool ModEnabled { get; }

        /// <summary><c>ImpactMeetRulesEnabled</c> as set.</summary>
        public bool On { get; }

        /// <summary>The meet is judged: the master switch first, then <c>ImpactMeetRulesEnabled</c>.</summary>
        public bool Enabled => ModEnabled && On;

        /// <summary>The AI reads an incoming kick / bash: the rules on and <c>MeetAiReadScalePercent</c> above 0.</summary>
        public bool AiReads => Enabled && ReadScale > 0;

        public double BashShieldRightStagger { get; }
        public double BashShieldRightFall { get; }
        public double BashWeaponRightStagger { get; }
        public double BashWeaponRightFall { get; }
        public double BashShieldWrongStagger { get; }
        public double BashShieldWrongFall { get; }
        public double BashShieldWrongDrop { get; }
        public double BashWeaponWrongStagger { get; }
        public double BashWeaponWrongFall { get; }
        public double BashOpenStagger { get; }
        public double BashOpenFall { get; }
        public double BashOpenDrop { get; }
        public double KickShieldDownStagger { get; }
        public double KickShieldDownFall { get; }
        public double KickWeaponDownStagger { get; }
        public double KickWeaponDownFall { get; }

        /// <summary><c>MeetAiReadScalePercent</c> ÷ 100 - the AI's read chance × this (1 = the chance he blocks swings with).</summary>
        public double ReadScale { get; }

        /// <summary><c>MeetAiReadReactionSeconds</c> - a man who reads it raises the guard this long after the kick / bash began; a hit sooner = too late.</summary>
        public double ReactionSeconds { get; }

        /// <summary><c>MeetAiReadMaxHoldSeconds</c> - the read guard is let go after this long if no hit came (a miss, a kick that never played).</summary>
        public double HoldSeconds { get; }

        public string? OffBecause => !ModEnabled ? "ModEnabled" : !On ? "ImpactMeetRulesEnabled" : null;

        private static double P(int percent) => Math.Max(0, Math.Min(1, percent / 100.0));

        /// <summary>The settings in one sentence (the mission-start line).</summary>
        public string Describe()
        {
            if (!Enabled) return "OFF (" + OffBecause + ") - every kick / bash decided as step 65 left it (a bash into a raised shield decides nothing)";
            return "ON - a bash met by a RIGHT guard staggers / fells the BASHER (shield " + ImpactMeetMath.P0(BashShieldRightStagger) + " / " + ImpactMeetMath.P0(BashShieldRightFall)
                   + ", weapon " + ImpactMeetMath.P0(BashWeaponRightStagger) + " / " + ImpactMeetMath.P0(BashWeaponRightFall) + "); a wrong guard risks the blocker (shield "
                   + ImpactMeetMath.P0(BashShieldWrongStagger) + " / " + ImpactMeetMath.P0(BashShieldWrongFall) + " / drop " + ImpactMeetMath.P0(BashShieldWrongDrop) + ", weapon "
                   + ImpactMeetMath.P0(BashWeaponWrongStagger) + " / " + ImpactMeetMath.P0(BashWeaponWrongFall) + "); no guard or mid-swing " + ImpactMeetMath.P0(BashOpenStagger) + " / "
                   + ImpactMeetMath.P0(BashOpenFall) + " / drop " + ImpactMeetMath.P0(BashOpenDrop) + "; a kick met by a DOWN guard staggers / fells the KICKER (shield "
                   + ImpactMeetMath.P0(KickShieldDownStagger) + " / " + ImpactMeetMath.P0(KickShieldDownFall) + ", weapon " + ImpactMeetMath.P0(KickWeaponDownStagger) + " / "
                   + ImpactMeetMath.P0(KickWeaponDownFall) + "); a weapon PUSH (step 88) as a bash, but met correctly by a guard UP (the pusher at risk on the right-guard ceilings), any other guard - a kick's - as step 65 - the ceilings x the S-curve of the pressure (the attacker's like the riposte's); "
                   + "the AI " + (AiReads ? "READS an incoming kick / bash at " + ImpactMeetMath.P0(ReadScale) + " of the chance he blocks a swing with, the guard up "
                                         + ImpactMeetMath.F2(ReactionSeconds) + " s after it began (sooner = too late), held at most " + ImpactMeetMath.F2(HoldSeconds) + " s"
                                         : "never reads it (MeetAiReadScalePercent 0)");
        }
    }

    /// <summary>Step 82 pure: the guard read, the row, its ceilings, the AI's read chance and its guard frame.</summary>
    public static class ImpactMeetMath
    {
        /// <summary>The rows (<see cref="ImpactMeet"/> values, NotJudged included).</summary>
        public const int MeetCount = 14; // step 88: + the five push rows

        // the engine's ActionCodeType values (v1.4.8 - the smoke checks them against the game's enum)
        public const int ActDefendFist = 1;
        public const int ActDefendShield = 2;
        public const int ActDefendLeftStaff = 14;
        public const int ActReadyRanged = 15;
        public const int ActReleaseMelee = 20;
        public const int ActReadyMelee = 19;
        public const int ActKick = 28;
        public const int ActWeaponBash = 31;

        // the engine's UsageDirection Defend* values
        public const int DirDefendUp = 4;
        public const int DirDefendDown = 5;
        public const int DirDefendLeft = 6;
        public const int DirDefendRight = 7;

        // the engine's MovementControlFlag Defend* bits
        public const uint FlagDefendLeft = 0x400;
        public const uint FlagDefendRight = 0x800;
        public const uint FlagDefendUp = 0x1000;
        public const uint FlagDefendDown = 0x2000;

        /// <summary>
        /// The guard from the defender's channel-1 action at contact: the shield raised (its direction from the action's own direction, else the
        /// engine's defend flag); a weapon guard (the action names its side - Forward = DOWN, the low / thrust guard); his own attack (a wind-up,
        /// a release, a kick, a bash, a shot) = Swinging; anything else = no guard.
        /// </summary>
        public static MeetGuardKind GuardOf(int actionType, int actionDirection, uint defendFlag, out MeetDir dir)
        {
            dir = MeetDir.Unknown;
            if (actionType == ActDefendShield || actionType == ActDefendFist)
            {
                dir = DirOf(actionDirection, defendFlag);
                return actionType == ActDefendShield ? MeetGuardKind.Shield : MeetGuardKind.Weapon;
            }
            if (actionType > ActDefendShield && actionType <= ActDefendLeftStaff)
            {
                switch ((actionType - 3) % 4) // 3/7/11 Forward, 4/8/12 Up, 5/9/13 Right, 6/10/14 Left
                {
                    case 0: dir = MeetDir.Down; break;
                    case 1: dir = MeetDir.Up; break;
                    case 2: dir = MeetDir.Right; break;
                    default: dir = MeetDir.Left; break;
                }
                return MeetGuardKind.Weapon;
            }
            if ((actionType >= ActReadyRanged && actionType <= ActReleaseMelee) || (actionType >= ActKick && actionType <= ActWeaponBash)) return MeetGuardKind.Swinging;
            return MeetGuardKind.None;
        }

        /// <summary>A guard's direction: the action's own Defend* direction, else the engine's defend flag, else unknown.</summary>
        public static MeetDir DirOf(int actionDirection, uint defendFlag)
        {
            switch (actionDirection)
            {
                case DirDefendUp: return MeetDir.Up;
                case DirDefendDown: return MeetDir.Down;
                case DirDefendLeft: return MeetDir.Left;
                case DirDefendRight: return MeetDir.Right;
            }
            if ((defendFlag & FlagDefendRight) != 0) return MeetDir.Right;
            if ((defendFlag & FlagDefendDown) != 0) return MeetDir.Down;
            if ((defendFlag & FlagDefendUp) != 0) return MeetDir.Up;
            if ((defendFlag & FlagDefendLeft) != 0) return MeetDir.Left;
            return MeetDir.Unknown;
        }

        /// <summary>
        /// The row. A bash the ENGINE stopped with the shield (<paramref name="blockedWithShield"/>) is a shield meet whatever the read says
        /// (right = correct, anything else = wrong); a bash otherwise by the read (a raised shield / a weapon to the right = correct, any other
        /// direction = wrong, no guard / mid-swing / unknown = open). A kick: a shield / a weapon guard DOWN = correct, anything else = as before.
        /// </summary>
        public static ImpactMeet Classify(bool bash, bool blockedWithShield, MeetGuardKind kind, MeetDir dir) => Classify(bash, blockedWithShield, kind, dir, push: false);

        /// <summary>
        /// Step 88 (Toni 2026-10-24: "make them as the shield bash, but make shield up the correct block for them"): a weapon PUSH
        /// (<paramref name="push"/>, with <paramref name="bash"/>) is judged by the bash's rows, but its CORRECT meet is a guard UP (a raised shield
        /// or a weapon guarding up), not right; a push the shield stopped = a shield meet (up = correct, else wrong) like a bash's.
        /// </summary>
        public static ImpactMeet Classify(bool bash, bool blockedWithShield, MeetGuardKind kind, MeetDir dir, bool push)
        {
            if (bash && push)
            {
                if (blockedWithShield || kind == MeetGuardKind.Shield) return dir == MeetDir.Up ? ImpactMeet.PushShieldUp : ImpactMeet.PushShieldWrong;
                if (kind == MeetGuardKind.Weapon) return dir == MeetDir.Up ? ImpactMeet.PushWeaponUp : ImpactMeet.PushWeaponWrong;
                return ImpactMeet.PushOpen;
            }
            if (bash)
            {
                if (blockedWithShield || kind == MeetGuardKind.Shield) return dir == MeetDir.Right ? ImpactMeet.BashShieldRight : ImpactMeet.BashShieldWrong;
                if (kind == MeetGuardKind.Weapon) return dir == MeetDir.Right ? ImpactMeet.BashWeaponRight : ImpactMeet.BashWeaponWrong;
                return ImpactMeet.BashOpen;
            }
            if (dir == MeetDir.Down)
            {
                if (kind == MeetGuardKind.Shield || (blockedWithShield && kind != MeetGuardKind.Weapon)) return ImpactMeet.KickShieldDown;
                if (kind == MeetGuardKind.Weapon) return ImpactMeet.KickWeaponDown;
            }
            return ImpactMeet.KickOther;
        }

        /// <summary>The correct meets - the ATTACKER is at risk, the defender takes nothing from the blow.</summary>
        public static bool AttackerAtRisk(ImpactMeet m) =>
            m == ImpactMeet.BashShieldRight || m == ImpactMeet.BashWeaponRight || m == ImpactMeet.KickShieldDown || m == ImpactMeet.KickWeaponDown
            || m == ImpactMeet.PushShieldUp || m == ImpactMeet.PushWeaponUp;

        /// <summary>Step 88: a push's row.</summary>
        public static bool IsPush(ImpactMeet m) => m >= ImpactMeet.PushShieldUp && m <= ImpactMeet.PushOpen;

        /// <summary>A row with its own ceilings (every bash row and the two correct kick rows; a kick met any other way keeps step 65's row).</summary>
        public static bool HasRow(ImpactMeet m) => m != ImpactMeet.NotJudged && m != ImpactMeet.KickOther;

        /// <summary>The meet's guard by a shield (else a weapon).</summary>
        public static bool ByShield(ImpactMeet m) => m == ImpactMeet.BashShieldRight || m == ImpactMeet.BashShieldWrong || m == ImpactMeet.KickShieldDown
            || m == ImpactMeet.PushShieldUp || m == ImpactMeet.PushShieldWrong;

        /// <summary>Step 88: the bash row whose ceilings a push's row uses (a push = a bash with the guard UP as its correct meet; Claude's call:
        /// the bash's ceilings shared, not ten new settings); any other row itself.</summary>
        public static ImpactMeet CeilingRow(ImpactMeet m) => m switch
        {
            ImpactMeet.PushShieldUp => ImpactMeet.BashShieldRight,
            ImpactMeet.PushWeaponUp => ImpactMeet.BashWeaponRight,
            ImpactMeet.PushShieldWrong => ImpactMeet.BashShieldWrong,
            ImpactMeet.PushWeaponWrong => ImpactMeet.BashWeaponWrong,
            ImpactMeet.PushOpen => ImpactMeet.BashOpen,
            _ => m,
        };

        /// <summary>The row's ceilings (0-1): the stagger, the fall, the shield drop (0 = the row has none).</summary>
        public static void Row(in ImpactMeetRules r, ImpactMeet m, out double stagger, out double fall, out double drop)
        {
            drop = 0;
            switch (CeilingRow(m))
            {
                case ImpactMeet.BashShieldRight: stagger = r.BashShieldRightStagger; fall = r.BashShieldRightFall; return;
                case ImpactMeet.BashWeaponRight: stagger = r.BashWeaponRightStagger; fall = r.BashWeaponRightFall; return;
                case ImpactMeet.BashShieldWrong: stagger = r.BashShieldWrongStagger; fall = r.BashShieldWrongFall; drop = r.BashShieldWrongDrop; return;
                case ImpactMeet.BashWeaponWrong: stagger = r.BashWeaponWrongStagger; fall = r.BashWeaponWrongFall; return;
                case ImpactMeet.BashOpen: stagger = r.BashOpenStagger; fall = r.BashOpenFall; drop = r.BashOpenDrop; return;
                case ImpactMeet.KickShieldDown: stagger = r.KickShieldDownStagger; fall = r.KickShieldDownFall; return;
                case ImpactMeet.KickWeaponDown: stagger = r.KickWeaponDownStagger; fall = r.KickWeaponDownFall; return;
                default: stagger = 0; fall = 0; return;
            }
        }

        /// <summary>The defender's guard G of a CORRECT meet (IMPACT §3.3's guards): a shield = <c>ImpactGuardShieldRight</c> × the shield's
        /// weight factor, a weapon = <c>ImpactGuardParry</c> × the weapon's. Never below 0.01.</summary>
        public static double Guard(in ImpactRules r, ImpactMeet m, double guardKg)
        {
            double g = ByShield(m) ? r.GuardRight * ImpactMath.WeightFactor(in r, guardKg) : r.GuardParry * ImpactMath.WeightFactor(in r, guardKg);
            return Math.Max(0.01, g);
        }

        // ------------------------------------------------------------------ the AI's read

        /// <summary>
        /// The chance an AI man READS an incoming kick / bash = the chance he blocks a swing correctly: the engine's own decision to block
        /// (<c>AIBlockOnDecideAbility</c>, by his skill and the difficulty) × the share of his guards in the right direction
        /// (<see cref="TiredBlockMath.RightShare"/> of <c>AiRandomizedDefendDirectionChance</c> - his skill, and since step 30 his bar: a tired man
        /// guesses) × <c>MeetAiReadScalePercent</c>. Clamped 0-1.
        /// </summary>
        public static double ReadChance(double scale, float blockOnDecide, float randomizedDirection)
        {
            double b = float.IsNaN(blockOnDecide) ? 0 : Math.Max(0, Math.Min(1, blockOnDecide));
            double c = Math.Max(0, scale) * b * TiredBlockMath.RightShare(randomizedDirection);
            return Math.Max(0, Math.Min(1, c));
        }

        /// <summary>The same from the game's own direction chance and step 30's tired weight w (what the stat decorator writes) - the read chance by bar.</summary>
        public static double ReadChanceAt(double scale, float blockOnDecide, float gameRandomizedDirection, float tiredWeight) =>
            ReadChance(scale, blockOnDecide, TiredBlockMath.Chance(gameRandomizedDirection, tiredWeight));

        /// <summary>The guard a reader raises: RIGHT against a bash, DOWN against a kick (the movement flag bit).</summary>
        public static uint GuardFlag(bool bash) => bash ? FlagDefendRight : FlagDefendDown;

        /// <summary>Step 88: ... and UP against a weapon push.</summary>
        public static uint GuardFlag(bool bash, bool push) => bash && push ? FlagDefendUp : GuardFlag(bash);

        /// <summary>One frame of a reader's input: his attack bits and his own guard out, the read guard in.</summary>
        public static uint GuardFrame(uint flags, uint guard) => (flags & ~(AiInputMath.AttackMask | AiInputMath.DefendMask)) | guard;

        /// <summary>The guard is up: the reaction time has passed since the kick / bash began.</summary>
        public static bool GuardUp(double now, double start, double reaction) => now >= start + Math.Max(0, reaction) - 1e-9;

        /// <summary>The hit came before he could react (sooner than the reaction time after the start) - a read too late.</summary>
        public static bool TooLate(double start, double hit, double reaction) => hit - start < Math.Max(0, reaction) - 1e-9;

        public static string MeetName(ImpactMeet m) => m switch
        {
            ImpactMeet.BashShieldRight => "bash - shield, guard right",
            ImpactMeet.BashWeaponRight => "bash - weapon, guard right",
            ImpactMeet.BashShieldWrong => "bash - shield, wrong guard",
            ImpactMeet.BashWeaponWrong => "bash - weapon, wrong guard",
            ImpactMeet.BashOpen => "bash - no guard or mid-swing",
            ImpactMeet.KickShieldDown => "kick - shield, guard down",
            ImpactMeet.KickWeaponDown => "kick - weapon, guard down",
            ImpactMeet.KickOther => "kick - any other guard",
            ImpactMeet.PushShieldUp => "push - shield, guard up",
            ImpactMeet.PushWeaponUp => "push - weapon, guard up",
            ImpactMeet.PushShieldWrong => "push - shield, wrong guard",
            ImpactMeet.PushWeaponWrong => "push - weapon, wrong guard",
            ImpactMeet.PushOpen => "push - no guard or mid-swing",
            _ => "not judged",
        };

        public static string GuardName(MeetGuardKind k, MeetDir d) => k switch
        {
            MeetGuardKind.Shield => "shield " + DirName(d),
            MeetGuardKind.Weapon => "weapon " + DirName(d),
            MeetGuardKind.Swinging => "mid-swing",
            MeetGuardKind.None => "no guard",
            _ => "not read",
        };

        public static string DirName(MeetDir d) => d switch
        {
            MeetDir.Up => "up",
            MeetDir.Down => "down",
            MeetDir.Left => "left",
            MeetDir.Right => "right",
            _ => "(direction unknown)",
        };

        internal static string P0(double share) => (share * 100).ToString("0", CultureInfo.InvariantCulture) + "%";

        internal static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>Step 82: the summary's numbers - each meeting by row, its outcomes on the attacker vs the defender, the AI's reads by bar band.</summary>
    public sealed class ImpactMeetStats
    {
        private const int M = ImpactMeetMath.MeetCount, B = ImpactMath.BandCount;

        public readonly int[] Decided = new int[M];
        public readonly int[] YouIn = new int[M];
        public readonly int[] AttackerStaggered = new int[M];
        public readonly int[] AttackerDown = new int[M];
        public readonly int[] DefenderStaggered = new int[M];
        public readonly int[] DefenderDown = new int[M];
        public readonly int[] DefenderDropped = new int[M];
        public readonly int[] DefenderHeld = new int[M];
        public readonly int[] ByReader = new int[M];

        /// <summary>The guard reads at contact by what he held (the read itself - for the playtest: does the engine tell the direction?).</summary>
        public readonly int[] GuardKinds = new int[5];
        public int DirectionUnknown;
        public int BlockedBashes, BlockedKicks, BlockedKicksNotJudged;
        public int ReadFailed;

        /// <summary>The engine's own verdict on a kick / bash the shield stopped (<c>CorrectSideShieldBlock</c>) against our read (the guard
        /// right / down), and the attack direction the collision carried (-1 none, 0-3 up / down / left / right, other) - does the engine judge the side?</summary>
        public int EngineCorrect, EngineCorrectOursCorrect, EngineWrongOursCorrect;
        public readonly int[] AttackDirections = new int[6];

        public void NoteEngineSide(bool engineCorrect, bool oursCorrect, int attackDirection)
        {
            if (engineCorrect)
            {
                EngineCorrect++;
                if (oursCorrect) EngineCorrectOursCorrect++;
            }
            else if (oursCorrect) EngineWrongOursCorrect++;
            AttackDirections[attackDirection < 0 ? 0 : attackDirection <= 3 ? attackDirection + 1 : 5]++;
        }

        // the AI's read
        public readonly int[] ReadRolled = new int[B];
        public readonly int[] ReadYes = new int[B];
        public readonly int[] ReadTooLate = new int[B];
        public readonly int[] ReadInTime = new int[B];
        public readonly int[] ReadMet = new int[B];
        public readonly double[] ChanceSum = new double[B];
        public int ReadSkippedBusy, ReadSkippedPool, ReadEndedHit, ReadEndedTimeout, ReadEndedGone, ReadGuardsRaised;
        public long ReadFrames;
        public int SwapsToKick, BashesKeptIntoWrongShield;

        public void NoteDecision(ImpactMeet m, bool attackerStaggered, bool attackerDown, bool defenderStaggered, bool defenderDown, bool defenderDropped, bool defenderHeld, bool you, bool reader)
        {
            int i = (int)m;
            if (i <= 0 || i >= M) return;
            Decided[i]++;
            if (you) YouIn[i]++;
            if (attackerStaggered) AttackerStaggered[i]++;
            if (attackerDown) AttackerDown[i]++;
            if (defenderStaggered) DefenderStaggered[i]++;
            if (defenderDown) DefenderDown[i]++;
            if (defenderDropped) DefenderDropped[i]++;
            if (defenderHeld) DefenderHeld[i]++;
            if (reader) ByReader[i]++;
        }

        public void NoteGuard(MeetGuardKind k, MeetDir d)
        {
            GuardKinds[Math.Min(4, (int)k)]++;
            if ((k == MeetGuardKind.Shield || k == MeetGuardKind.Weapon) && d == MeetDir.Unknown) DirectionUnknown++;
        }

        public void NoteRead(int band, double chance, bool yes)
        {
            int b = Math.Max(0, Math.Min(B - 1, band));
            ReadRolled[b]++;
            ChanceSum[b] += chance;
            if (yes) ReadYes[b]++;
        }

        public void NoteReadHit(int band, bool tooLate, bool met)
        {
            int b = Math.Max(0, Math.Min(B - 1, band));
            if (tooLate) ReadTooLate[b]++;
            else ReadInTime[b]++;
            if (met) ReadMet[b]++;
        }

        public int Total
        {
            get
            {
                int n = 0;
                for (int i = 1; i < M; i++) n += Decided[i];
                return n;
            }
        }

        /// <summary>The [summary] lines (step 82): the meetings by row with the outcomes on each man, the guard reads, the AI's reads by bar band.</summary>
        public string[] SummaryLines(in ImpactMeetRules r)
        {
            var c = CultureInfo.InvariantCulture;
            var t = new StringBuilder();
            t.Append("impact meets (step 82 / 88) - each kick / bash / push by how it was met (").Append(r.Enabled ? "ON" : "OFF (" + r.OffBecause + ")").Append("): ");
            for (int i = 1; i < M; i++)
            {
                var m = (ImpactMeet)i;
                if (i > 1) t.Append(" | ");
                if (m == ImpactMeet.PushShieldUp) t.Append("PUSHES (step 88 - the bash's ceilings, a guard UP the correct meet): ");
                t.Append(ImpactMeetMath.MeetName(m)).Append(' ').Append(Decided[i].ToString(c));
                if (Decided[i] == 0) continue;
                if (ImpactMeetMath.AttackerAtRisk(m))
                    t.Append(" → the ATTACKER staggered ").Append(Pct(AttackerStaggered[i], Decided[i])).Append(", down ").Append(Pct(AttackerDown[i], Decided[i]))
                     .Append("; the defender held ").Append(Pct(DefenderHeld[i], Decided[i]));
                else
                    t.Append(" → the defender staggered ").Append(Pct(DefenderStaggered[i], Decided[i])).Append(", down ").Append(Pct(DefenderDown[i], Decided[i]))
                     .Append(", shield dropped ").Append(Pct(DefenderDropped[i], Decided[i])).Append(", held ").Append(Pct(DefenderHeld[i], Decided[i]));
                if (ByReader[i] > 0) t.Append(" (an AI read it: ").Append(ByReader[i].ToString(c)).Append(')');
                if (YouIn[i] > 0) t.Append(" (you in ").Append(YouIn[i].ToString(c)).Append(')');
            }
            t.Append("; bashes the shield stopped ").Append(BlockedBashes.ToString(c)).Append(", kicks the shield stopped ").Append(BlockedKicks.ToString(c))
             .Append(" (not judged - any other guard - ").Append(BlockedKicksNotJudged.ToString(c)).Append(')');

            var g = new StringBuilder();
            g.Append("impact meets - the guard read at contact (step 82; PLAYTEST D47 - does the engine tell the direction?): no guard ").Append(GuardKinds[(int)MeetGuardKind.None].ToString(c))
             .Append(", mid-swing ").Append(GuardKinds[(int)MeetGuardKind.Swinging].ToString(c)).Append(", a shield ").Append(GuardKinds[(int)MeetGuardKind.Shield].ToString(c))
             .Append(", a weapon ").Append(GuardKinds[(int)MeetGuardKind.Weapon].ToString(c)).Append(", not read ").Append(GuardKinds[(int)MeetGuardKind.Unknown].ToString(c))
             .Append("; the stopped ones the ENGINE called correct-side ").Append(EngineCorrect.ToString(c)).Append(" (ours correct too ").Append(EngineCorrectOursCorrect.ToString(c))
             .Append(", ours correct where the engine said wrong ").Append(EngineWrongOursCorrect.ToString(c)).Append("), their attack direction none / up / down / left / right / other ")
             .Append(AttackDirections[0].ToString(c)).Append(" / ").Append(AttackDirections[1].ToString(c)).Append(" / ").Append(AttackDirections[2].ToString(c)).Append(" / ")
             .Append(AttackDirections[3].ToString(c)).Append(" / ").Append(AttackDirections[4].ToString(c)).Append(" / ").Append(AttackDirections[5].ToString(c))
             .Append("; a guard with NO direction read ").Append(DirectionUnknown.ToString(c)).Append(" (counted wrong); reads failed ").Append(ReadFailed.ToString(c));

            var a = new StringBuilder();
            a.Append("impact meets - the AI reads an incoming kick / bash (step 82): by the reader's bar ");
            for (int b = 0; b < B; b++)
            {
                if (b > 0) a.Append(" | ");
                a.Append(ImpactMath.BandName(b)).Append(": rolled ").Append(ReadRolled[b].ToString(c));
                if (ReadRolled[b] == 0) continue;
                a.Append(" (chance avg ").Append((100 * ChanceSum[b] / ReadRolled[b]).ToString("0", c)).Append("%) → READ ").Append(Pct(ReadYes[b], ReadRolled[b]))
                 .Append("; of the reads hit: in time ").Append(ReadInTime[b].ToString(c)).Append(", TOO LATE ").Append(ReadTooLate[b].ToString(c))
                 .Append(", met correctly ").Append(ReadMet[b].ToString(c));
            }
            a.Append("; not rolled: busy (his own kick, down, staggered, reading already) ").Append(ReadSkippedBusy.ToString(c)).Append(", the pool full ").Append(ReadSkippedPool.ToString(c))
             .Append("; guards raised ").Append(ReadGuardsRaised.ToString(c)).Append(" (").Append(ReadFrames.ToString(c)).Append(" frames written)")
             .Append("; reads ended by the hit ").Append(ReadEndedHit.ToString(c)).Append(", no hit within the hold ").Append(ReadEndedTimeout.ToString(c))
             .Append(", gone ").Append(ReadEndedGone.ToString(c))
             .Append("; AI bashes into a raised shield guarding RIGHT swapped to a kick ").Append(SwapsToKick.ToString(c))
             .Append(", kept as a bash into a WRONG-guarded shield ").Append(BashesKeptIntoWrongShield.ToString(c));
            return new[] { t.ToString(), g.ToString(), a.ToString() };
        }

        private static string Pct(int n, int of) => of <= 0 ? "-" : n.ToString(CultureInfo.InvariantCulture) + " (" + (100.0 * n / of).ToString("0", CultureInfo.InvariantCulture) + "%)";
    }
}

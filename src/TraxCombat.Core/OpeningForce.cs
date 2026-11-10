using System;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>Step 106b: why an AI man's guard is forced right now.</summary>
    public enum OpeningForceKind : byte
    {
        None,

        /// <summary>OFF BALANCE: his guard written to the balance side from your blocked attack (or unguarded hit) until your follow-up's wind-up.</summary>
        Balance,

        /// <summary>Your follow-up into the side beat his block roll at its wind-up: the guard kept away from it (a shield lowered from
        /// <c>OpeningShieldMinChain</c>) until the blow resolves, at most <c>OpeningHoldMaxSeconds</c>.</summary>
        Held,

        /// <summary>The test tool (<c>OpeningGuardTest</c>): a chosen guard for <c>OpeningGuardTestSeconds</c>.</summary>
        Test,

        /// <summary>Step 107: a HOLD-BLOCK opening - his guard held in G for the hold (lowered for your winning strike if he has a shield). Not under
        /// <see cref="OpeningAttackLock"/>.</summary>
        Hold,

        /// <summary>Step 111: YOUR perfect-strike swing begun while he reels is in the air - every guard bit out and his 'can defend' off until it
        /// lands / misses (<c>PerfectStrikeFlightSeconds</c> past the window at most). Not under <see cref="OpeningAttackLock"/>; no pose read.</summary>
        Strike,
    }

    /// <summary>Step 106b: how a forced guard ended (the summary counts them).</summary>
    public enum OpeningForceEnd : byte
    {
        /// <summary>Your follow-up's wind-up: he won the block roll (his own AI guards again).</summary>
        HeBlocks,

        /// <summary>Your follow-up's wind-up went another way than the balance side (his own AI guards again).</summary>
        WrongSide,

        /// <summary>The blow resolved (your contact with him, a miss).</summary>
        Resolved,

        /// <summary>The time ran out (the off-balance time, the hold's cap, the test's seconds).</summary>
        Timeout,

        /// <summary>He reels now (your hit staggered him - the reel's own locks take over in 106d).</summary>
        Reels,

        /// <summary>The chain ended or moved on (another link replaced it, the chain reset).</summary>
        ChainEnded,

        /// <summary>A switch (the master switch, <c>OpeningsEnabled</c>, the test tool), he left the field, the mission ended, an error.</summary>
        Off,

        Count,
    }

    /// <summary>
    /// Step 106f (Toni's first playtest: "poses read back 0" - the live force was never checked): what his upper body showed when a LIVE forced
    /// guard was read back - at <see cref="OpeningForceMath.PoseFirstReadSeconds"/> after the force began and, when not yet, again at
    /// <see cref="OpeningForceMath.PoseLastReadSeconds"/> (his parry's own reaction may still play at the first read).
    /// </summary>
    public enum OpeningPoseResult : byte
    {
        /// <summary>The wanted guard (or no guard at all for a lowered one) at the first read.</summary>
        Shown,

        /// <summary>Not yet at the first read, the wanted pose at the last one.</summary>
        ShownLate,

        /// <summary>He was defending, but another direction (his own guard or his parry's reaction) - the input not honoured.</summary>
        OtherGuard,

        /// <summary>No defend stage at all (an attack, a reaction, idle) - the wanted guard never came up.</summary>
        NotDefending,

        /// <summary>Lowered wanted, but a guard still up at the last read.</summary>
        StillGuarding,

        /// <summary>The force ended before the last read (and the first one did not show it).</summary>
        EndedFirst,

        Count,
    }

    /// <summary>
    /// Step 106f (Toni's first playtest: "BLOCKED ANYWAY" 2 of 4 held follow-ups): WHY a held follow-up was blocked after all - read at the block
    /// itself from the collision, his guard at contact and what was forced.
    /// </summary>
    public enum OpeningAnyway : byte
    {
        /// <summary>Your swing at contact was not the side the follow-up was judged into (the 106e build judged at the wind-up).</summary>
        OtherDirection,

        /// <summary>His shield stopped it while it was still meant to hold (not lowered).</summary>
        ShieldHeld,

        /// <summary>His shield stopped it although it was lowered - it did not come down in time.</summary>
        ShieldNotDown,

        /// <summary>A weapon guard, and his guard at contact WAS the forced one - the engine parried anyway.</summary>
        ForcedGuardParried,

        /// <summary>A weapon guard, his guard at contact another one - his own guard came back (the input not honoured in time).</summary>
        OwnGuard,

        /// <summary>His guard at contact unread.</summary>
        Unread,

        Count,
    }

    /// <summary>
    /// Step 106b: the test tool's guards, cycled by its key - the four directions (the engine's DefendDown / Up / Left / Right bits), the shield
    /// LOWERED by the input (every guard bit out of his frame) and the shield lowered by the game's own route (the agent's <c>CanDefend</c> flag
    /// taken off - what vanilla does to climbers; given back at the end) on top of the input.
    /// </summary>
    public enum OpeningTestMode : byte
    {
        Low,
        High,
        Left,
        Right,
        Lowered,
        LoweredByFlag,
        Count,
    }

    /// <summary>
    /// Step 106b - THE FORCED GUARD (AI_NOTES "Step 106" §106b, DESIGN decision 95): the pure part. The frame goes into step 16's input hook
    /// (<c>AiInputComponent.Filter</c>) after step 82's read guard and before a kick; step 104b's reel lock stays last. The bits are the engine's
    /// <c>Agent.MovementControlFlag</c> values (v1.4.8 - the offline smoke checks them against the game's enum). No allocation.
    /// </summary>
    public static class OpeningForceMath
    {
        public const uint DefendLeft = 0x400, DefendRight = 0x800, DefendUp = 0x1000, DefendDown = 0x2000, DefendAuto = 0x4000;

        /// <summary><c>MovementControlFlag.DefendBlock</c> (0x8000) - NOT in the engine's DefendMask; taken out too when the guard is lowered.</summary>
        public const uint DefendBlock = 0x8000;

        /// <summary>Every guard bit (<see cref="AiInputMath.DefendMask"/> + <see cref="DefendBlock"/>).</summary>
        public const uint GuardBits = AiInputMath.DefendMask | DefendBlock;

        /// <summary>A guard (the engine's <c>UsageDirection</c> DefendUp 4 .. DefendRight 7) as its movement bit; anything else 0.</summary>
        public static uint GuardFlag(int guard) => guard switch
        {
            OpeningMath.GuardUp => DefendUp,
            OpeningMath.GuardDown => DefendDown,
            OpeningMath.GuardLeft => DefendLeft,
            OpeningMath.GuardRight => DefendRight,
            _ => 0u,
        };

        /// <summary>The guard a movement bit raises (<see cref="OpeningMath.Unknown"/> for none / several / DefendAuto).</summary>
        public static int GuardOfFlag(uint flag) => (flag & GuardBits) switch
        {
            DefendUp => OpeningMath.GuardUp,
            DefendDown => OpeningMath.GuardDown,
            DefendLeft => OpeningMath.GuardLeft,
            DefendRight => OpeningMath.GuardRight,
            _ => OpeningMath.Unknown,
        };

        /// <summary>
        /// ONE FRAME of a forced man's input: his attack bits out (he does not swing while off balance); his guard bits replaced by
        /// <paramref name="guardFlag"/> - or, with <paramref name="lower"/>, EVERY guard bit out (no guard at all: a shield is lowered - a raised
        /// shield stops a blow from any side, step 64, so a forced shield direction alone opens nothing). The move bits, the turn bits and every
        /// other bit stay his. <paramref name="edit"/>: AttackCleared / GuardCleared (a guard of his own taken out) / GuardRaised (ours written).
        /// </summary>
        public static uint Frame(uint flags, uint guardFlag, bool lower, out InputEdit edit)
        {
            edit = InputEdit.None;
            uint f = flags;
            if ((f & AiInputMath.AttackMask) != 0)
            {
                f &= ~AiInputMath.AttackMask;
                edit |= InputEdit.AttackCleared;
            }
            uint own = f & GuardBits;
            f &= ~GuardBits;
            if (lower || guardFlag == 0)
            {
                if (own != 0) edit |= InputEdit.GuardCleared;
                return f;
            }
            if (own != guardFlag) edit |= InputEdit.GuardRaised;
            return f | (guardFlag & GuardBits);
        }

        /// <summary>The shield gives way: he holds a shield and the plan says it no longer holds the side (from link <c>OpeningShieldMinChain</c>;
        /// an unguarded front hit's state on a shieldman too - he was not guarding).</summary>
        public static bool Lowers(bool shieldInHand, in OpeningPlan p) => shieldInHand && !p.ShieldHolds;

        /// <summary>THE LIVE BLOCK ROLL at your follow-up's wind-up: through (he fails to block) when the die is at or above his block chance.</summary>
        public static bool Through(double die, double blockChance) => !double.IsNaN(die) && die >= blockChance;

        /// <summary>The test tool's next mode (<paramref name="back"/>: the one before); the first press gives <see cref="OpeningTestMode.Low"/>.</summary>
        public static OpeningTestMode Next(int last, bool back)
        {
            int n = (int)OpeningTestMode.Count;
            if (last < 0 || last >= n) return back ? (OpeningTestMode)(n - 1) : OpeningTestMode.Low;
            return (OpeningTestMode)(back ? (last + n - 1) % n : (last + 1) % n);
        }

        /// <summary>The test mode's guard (<see cref="OpeningMath.GuardDown"/> = low ...); the lowered modes = <see cref="OpeningMath.Unknown"/>.</summary>
        public static int TestGuard(OpeningTestMode m) => m switch
        {
            OpeningTestMode.Low => OpeningMath.GuardDown,
            OpeningTestMode.High => OpeningMath.GuardUp,
            OpeningTestMode.Left => OpeningMath.GuardLeft,
            OpeningTestMode.Right => OpeningMath.GuardRight,
            _ => OpeningMath.Unknown,
        };

        public static bool TestLowers(OpeningTestMode m) => m == OpeningTestMode.Lowered || m == OpeningTestMode.LoweredByFlag;

        public static string TestName(OpeningTestMode m) => m switch
        {
            OpeningTestMode.Low => "LOW guard (DefendDown)",
            OpeningTestMode.High => "HIGH guard (DefendUp)",
            OpeningTestMode.Left => "LEFT guard (DefendLeft)",
            OpeningTestMode.Right => "RIGHT guard (DefendRight)",
            OpeningTestMode.Lowered => "SHIELD / GUARD LOWERED (every guard bit out of his input)",
            OpeningTestMode.LoweredByFlag => "SHIELD / GUARD LOWERED by the game's CanDefend flag (+ the input)",
            _ => "?",
        };

        /// <summary>
        /// The pose read back: did his guard become the wanted one? A wanted direction = his channel-1 action direction is that guard while in a
        /// defend stage; lowered = no defend stage and no guard direction. <paramref name="direction"/> = the engine's <c>UsageDirection</c>
        /// (-1 none), <paramref name="defending"/> = channel 1 in Defend / DefendParry.
        /// </summary>
        public static bool PoseMatches(int wantedGuard, bool lowered, int direction, bool defending)
        {
            if (lowered) return !defending && !OpeningMath.IsGuard(direction);
            return defending && direction == wantedGuard;
        }

        /// <summary>Step 106f: the live forced guard's first read-back (s after the force began) - Toni's "e.g. 0.2 s".</summary>
        public const double PoseFirstReadSeconds = 0.2;

        /// <summary>Step 106f: the last read-back when the first did not show it (his parry's own reaction is over by then).</summary>
        public const double PoseLastReadSeconds = 0.5;

        /// <summary>
        /// Step 106f: one live read-back's verdict; <paramref name="first"/> = the first read - a miss there returns null (wait for the last one).
        /// </summary>
        public static OpeningPoseResult? PoseVerdict(int wantedGuard, bool lowered, int direction, bool defending, bool first)
        {
            if (PoseMatches(wantedGuard, lowered, direction, defending)) return first ? OpeningPoseResult.Shown : OpeningPoseResult.ShownLate;
            if (first) return null;
            if (lowered) return OpeningPoseResult.StillGuarding;
            return defending ? OpeningPoseResult.OtherGuard : OpeningPoseResult.NotDefending;
        }

        public static string PoseName(OpeningPoseResult r) => r switch
        {
            OpeningPoseResult.Shown => "shown at 0.2 s",
            OpeningPoseResult.ShownLate => "shown by 0.5 s",
            OpeningPoseResult.OtherGuard => "NOT shown - defending another way",
            OpeningPoseResult.NotDefending => "NOT shown - not defending",
            OpeningPoseResult.StillGuarding => "NOT lowered - a guard still up",
            OpeningPoseResult.EndedFirst => "ended before the read",
            _ => "?",
        };

        /// <summary>
        /// Step 106f: why a held follow-up was blocked anyway. <paramref name="judged"/> = the side it was judged into, <paramref name="atContact"/>
        /// = the collision's direction (unknown = not compared), <paramref name="shieldBlock"/> = his shield stopped it, <paramref name="lowered"/> =
        /// the force lowered his guard, <paramref name="guardAtContact"/> / <paramref name="forcedGuard"/> = channel 1's guard then / the one forced.
        /// </summary>
        public static OpeningAnyway AnywayCause(int judged, int atContact, bool shieldBlock, bool lowered, int guardAtContact, int forcedGuard)
        {
            if (OpeningMath.IsAttack(judged) && OpeningMath.IsAttack(atContact) && judged != atContact) return OpeningAnyway.OtherDirection;
            if (shieldBlock) return lowered ? OpeningAnyway.ShieldNotDown : OpeningAnyway.ShieldHeld;
            if (!OpeningMath.IsGuard(guardAtContact)) return OpeningAnyway.Unread;
            return guardAtContact == forcedGuard ? OpeningAnyway.ForcedGuardParried : OpeningAnyway.OwnGuard;
        }

        public static string AnywayName(OpeningAnyway a) => a switch
        {
            OpeningAnyway.OtherDirection => "your swing went another way at contact",
            OpeningAnyway.ShieldHeld => "his shield still held (not lowered)",
            OpeningAnyway.ShieldNotDown => "his shield did not come down in time",
            OpeningAnyway.ForcedGuardParried => "his guard WAS the forced one - the engine parried anyway",
            OpeningAnyway.OwnGuard => "his own guard came back (the input not honoured in time)",
            OpeningAnyway.Unread => "his guard unread",
            _ => "?",
        };

        public static string EndName(OpeningForceEnd e) => e switch
        {
            OpeningForceEnd.HeBlocks => "he won the block roll",
            OpeningForceEnd.WrongSide => "your follow-up went another way",
            OpeningForceEnd.Resolved => "the blow resolved",
            OpeningForceEnd.Timeout => "the time ran out",
            OpeningForceEnd.Reels => "he reels",
            OpeningForceEnd.ChainEnded => "the chain ended",
            OpeningForceEnd.Off => "a switch / gone / the end",
            _ => "?",
        };
    }

    /// <summary>
    /// Step 106 follow-up (Toni 2026-11-09, DESIGN 95 (n)): THE OFF-BALANCE ATTACK LOCK IS CAPPED AT <see cref="MaxSeconds"/> (1 s). Before, an off-balance
    /// man's attack bits stayed out until your next wind-up or <c>OpeningChainResetSeconds</c> (2 s), and each correct block re-armed it - a player
    /// who kept swinging into his guard froze him. Now one RUN of forces on the same man (back-to-back off-balance states and the held follow-ups
    /// between them) locks him at most 1 s from its first frame; after that no new off-balance force starts on him until he has been free
    /// <see cref="MaxSeconds"/> (his own AI may attack again even if you keep swinging). The chain, the arrow and the stagger on a hit are untouched -
    /// only the forced guard (and so its attack lock) is skipped. A struct the logic keeps; pure; no allocation.
    /// </summary>
    public struct OpeningAttackLock
    {
        /// <summary>The cap on one run's attack lock (s), and the free time that starts a new run. A constant, not a setting (Toni's decision).</summary>
        public const double MaxSeconds = 1.0;

        private bool _has;

        /// <summary>The man of the current run (valid while <see cref="Has"/>), when it began, when its last force ended.</summary>
        public int Target;
        public double RunStart, LastEnd;

        /// <summary>A force of the run is on now.</summary>
        public bool Active;

        public bool Has => _has;

        /// <summary>
        /// A new OFF-BALANCE force on <paramref name="target"/> at <paramref name="now"/> that would last <paramref name="wanted"/> s: true with
        /// <paramref name="until"/> = min(now + wanted, the run's start + 1 s); false (spent - no force) when the run's second is used up. The same
        /// run = the same man while a force is on or within 1 s of the last one's end; anything else starts a new run.
        /// </summary>
        public bool Begin(int target, double now, double wanted, out double until)
        {
            bool same = _has && target == Target && (Active || now - LastEnd < MaxSeconds - 1e-9);
            if (!same)
            {
                _has = true;
                Target = target;
                RunStart = now;
                Active = false;
            }
            until = Math.Min(now + Math.Max(0, wanted), RunStart + MaxSeconds);
            if (until <= now + 1e-9) return false;
            Active = true;
            return true;
        }

        /// <summary>The run's force let go at <paramref name="now"/> (the free time counts from here).</summary>
        public void End(double now)
        {
            if (!Active) return;
            Active = false;
            LastEnd = now;
        }

        /// <summary>A fresh start (the mission's start / end).</summary>
        public void Reset() => this = default;
    }

    /// <summary>
    /// Step 106b: what the forced guard did in one battle - the [summary] line "openings - the forced guard". Counters only, no allocation per
    /// event.
    /// </summary>
    public sealed class OpeningForceStats
    {
        /// <summary>Forced states started: off balance, held after a won roll, the test tool.</summary>
        public int Balance, Held, Tests;

        /// <summary>Of the holds, the ones with the shield (or guard) LOWERED.</summary>
        public int ShieldLowerings;

        /// <summary>The live block rolls at your wind-up into the side: he blocked / your follow-up got through; wind-ups another way.</summary>
        public int RollsBlocked, RollsThrough, WrongSide;

        /// <summary>A held follow-up that he blocked ANYWAY (the forcing did not stop him - an engine finding).</summary>
        public int BlockedAnyway;

        /// <summary>A held follow-up that landed.</summary>
        public int HeldLanded;

        /// <summary>Step 106 follow-up: off-balance forces cut short by the 1 s attack lock / not started because the run's second was spent.</summary>
        public int LockCapped, LockSpent;

        /// <summary>Not forced: you / a man you drive, a rider.</summary>
        public int NotAi;

        /// <summary>The input frames written / with his attack bits out / with his own guard replaced or taken out / lowered.</summary>
        public long Frames, AttacksOut, GuardsWritten, GuardsCleared, LoweredFrames;

        /// <summary>The test tool's poses read back: how many, how many matched the wanted guard, how many changed from before.</summary>
        public int PosesRead, PosesMatched, PosesChanged;

        /// <summary>The CanDefend flag taken off / given back (the test tool's 6th mode).</summary>
        public int FlagsOff, FlagsBack;

        /// <summary>Step 106f: of <see cref="FlagsOff"/>, the held follow-ups' (<c>OpeningHoldTakesDefend</c>).</summary>
        public int HoldFlagsOff;

        /// <summary>Step 106f: the LIVE read-backs - the off-balance pose with a weapon / with a shield, a lowered hold - by <see cref="OpeningPoseResult"/>.</summary>
        public readonly int[] PoseWeapon = new int[(int)OpeningPoseResult.Count];
        public readonly int[] PoseShield = new int[(int)OpeningPoseResult.Count];
        public readonly int[] PoseLowered = new int[(int)OpeningPoseResult.Count];

        /// <summary>Step 106f: BLOCKED ANYWAY by cause.</summary>
        public readonly int[] Anyway = new int[(int)OpeningAnyway.Count];

        public void NoteLivePose(OpeningPoseResult r, bool shield, bool lowered)
        {
            if (r >= OpeningPoseResult.Count) return;
            var a = lowered ? PoseLowered : shield ? PoseShield : PoseWeapon;
            a[(int)r]++;
        }

        public void NoteAnyway(OpeningAnyway a)
        {
            BlockedAnyway++;
            if (a < OpeningAnyway.Count) Anyway[(int)a]++;
        }

        private static void PoseText(StringBuilder t, string name, int[] a, CultureInfo c)
        {
            t.Append(name);
            for (int i = 0; i < a.Length; i++)
                t.Append(i == 0 ? " " : ", ").Append(OpeningForceMath.PoseName((OpeningPoseResult)i)).Append(' ').Append(a[i].ToString(c));
        }

        public readonly int[] Ends = new int[(int)OpeningForceEnd.Count];
        public int Errors;

        public void NoteEnd(OpeningForceEnd e)
        {
            if (e >= 0 && e < OpeningForceEnd.Count) Ends[(int)e]++;
        }

        public void NoteFrames(long frames, long attacksOut, long guardsWritten, long guardsCleared, long loweredFrames)
        {
            Frames += frames;
            AttacksOut += attacksOut;
            GuardsWritten += guardsWritten;
            GuardsCleared += guardsCleared;
            LoweredFrames += loweredFrames;
        }

        public void NotePose(bool matched, bool changed)
        {
            PosesRead++;
            if (matched) PosesMatched++;
            if (changed) PosesChanged++;
        }

        public string SummaryLine(in OpeningRules r, bool testOn)
        {
            var c = CultureInfo.InvariantCulture;
            var t = new StringBuilder();
            t.Append("openings - the forced guard (step 106b; live ").Append(r.Enabled ? "ON" : "OFF (" + r.OffBecause + ")")
             .Append(", the test tool ").Append(testOn ? "ON" : "OFF").Append("): forced off balance ").Append(Balance.ToString(c))
             .Append(", held after a won roll ").Append(Held.ToString(c)).Append(" (shield / guard LOWERED ").Append(ShieldLowerings.ToString(c))
             .Append("), test ").Append(Tests.ToString(c));
            t.Append("; the block roll at your wind-up: he blocks ").Append(RollsBlocked.ToString(c)).Append(", through ").Append(RollsThrough.ToString(c))
             .Append(" (landed ").Append(HeldLanded.ToString(c)).Append(", BLOCKED ANYWAY ").Append(BlockedAnyway.ToString(c)).Append("), another side ")
             .Append(WrongSide.ToString(c));
            t.Append("; the attack lock (at most ").Append(OpeningAttackLock.MaxSeconds.ToString("0.0", c)).Append(" s a run): cut short ")
             .Append(LockCapped.ToString(c)).Append(", spent - not forced ").Append(LockSpent.ToString(c));
            t.Append("; frames written ").Append(Frames.ToString(c)).Append(" (attack out ").Append(AttacksOut.ToString(c)).Append(", his guard replaced ")
             .Append(GuardsWritten.ToString(c)).Append(", taken out ").Append(GuardsCleared.ToString(c)).Append(", lowered ").Append(LoweredFrames.ToString(c)).Append(')');
            t.Append("; THE LIVE POSE read back (step 106f, ").Append(OpeningForceMath.PoseFirstReadSeconds.ToString("0.0", c)).Append(" s / ")
             .Append(OpeningForceMath.PoseLastReadSeconds.ToString("0.0", c)).Append(" s after forcing): ");
            PoseText(t, "the balance guard with a weapon -", PoseWeapon, c);
            PoseText(t, "; with a shield -", PoseShield, c);
            PoseText(t, "; lowered for a held follow-up -", PoseLowered, c);
            t.Append("; BLOCKED ANYWAY by cause");
            for (int i = 0; i < (int)OpeningAnyway.Count; i++)
                t.Append(i == 0 ? " " : ", ").Append(OpeningForceMath.AnywayName((OpeningAnyway)i)).Append(' ').Append(Anyway[i].ToString(c));
            t.Append("; the test tool's poses read back ").Append(PosesRead.ToString(c)).Append(" (as wanted ").Append(PosesMatched.ToString(c)).Append(", changed ")
             .Append(PosesChanged.ToString(c)).Append(')');
            if (FlagsOff + FlagsBack > 0)
                t.Append("; CanDefend off / back ").Append(FlagsOff.ToString(c)).Append(" / ").Append(FlagsBack.ToString(c)).Append(" (held follow-ups ")
                 .Append(HoldFlagsOff.ToString(c)).Append(')');
            t.Append("; ended by");
            for (int i = 0; i < (int)OpeningForceEnd.Count; i++)
                t.Append(i == 0 ? " " : ", ").Append(OpeningForceMath.EndName((OpeningForceEnd)i)).Append(' ').Append(Ends[i].ToString(c));
            if (NotAi > 0) t.Append("; not forced (not the AI's) ").Append(NotAi.ToString(c));
            if (Errors > 0) t.Append("; errors ").Append(Errors.ToString(c));
            return t.ToString();
        }
    }
}

using System;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>Step 107: what YOUR swing is to a man holding a forced block in guard G.</summary>
    public enum HoldSwing : byte
    {
        /// <summary>The direction unread.</summary>
        Unknown,

        /// <summary>It beats the hold: a 4-direction weapon (a sword) exactly into the opening O; any other weapon any direction G does not block.</summary>
        Wins,

        /// <summary>Into his held guard G - he blocks it (a link).</summary>
        IntoGuard,

        /// <summary>A sword's third side (neither O nor G): a normal hit, no stagger.</summary>
        ThirdSide,

        Count,
    }

    /// <summary>Step 107: how a hold (or its chain) ended.</summary>
    public enum HoldEnd : byte
    {
        /// <summary>Your winning strike landed: he staggers (the step-106 reel, <see cref="ReelCause.Opening"/>).</summary>
        Staggered,

        /// <summary>The hold ran out without a stagger (the counter-swing roll follows).</summary>
        Timeout,

        /// <summary>A landed hit that did not win started a fresh hold.</summary>
        FreshHold,

        /// <summary>His blow landed on you.</summary>
        HeHitYou,

        /// <summary>Your blow met someone else.</summary>
        TargetSwitch,

        /// <summary>A hit from behind staggered him (step 106's back hit).</summary>
        BackHit,

        /// <summary>The chain's time after a hold ran out (no link in <c>OpeningChainResetSeconds</c>).</summary>
        Gap,

        /// <summary>A switch (the master switch, <c>OpeningsEnabled</c>, <c>OpeningHoldsEnabled</c>, the impact system), he left / died / mounted, the
        /// mission ended, an error.</summary>
        Off,

        Count,
    }

    /// <summary>Step 107: where the hold chain on one man stands.</summary>
    public enum HoldPhase : byte
    {
        None,

        /// <summary>He holds the forced block now.</summary>
        Holding,

        /// <summary>The hold ran out; a block of yours within <c>OpeningChainResetSeconds</c> is still a link.</summary>
        After,
    }

    /// <summary>
    /// Step 107 - HOLD-BLOCK OPENINGS (Toni 2026-11-10, AI_NOTES "Step 107", DESIGN decision 96): the settings, read live. <see cref="On"/> = the
    /// master switch FIRST, then <c>OpeningsEnabled</c>, then <c>OpeningHoldsEnabled</c> (the module adds: the openings live - decision 95 (m)).
    /// </summary>
    public readonly struct OpeningHoldRules
    {
        public OpeningHoldRules(TraxSettings s)
        {
            ModEnabled = s.ModEnabled;
            OpeningsSwitch = s.OpeningsEnabled;
            Switch = s.OpeningHoldsEnabled;
            FirstSeconds = Pos(s.OpeningHoldFirstSeconds);
            SpeedReference = Math.Max(1, s.OpeningHoldSpeedReference);
            FactorMin = Pos(s.OpeningHoldSpeedFactorMin);
            FactorMax = Math.Max(FactorMin, Pos(s.OpeningHoldSpeedFactorMax));
            PerLink = Pos(s.OpeningHoldPerLinkSeconds);
            BonusMax = Pos(s.OpeningHoldChainBonusSeconds);
            BonusFull = Pos(s.OpeningHoldChainBonusFullSeconds);
            BonusZero = Math.Max(BonusFull + 0.01, Pos(s.OpeningHoldChainBonusZeroSeconds));
            Cap = Math.Max(0.1, Pos(s.OpeningHoldCapSeconds));
            CounterFirst = Clamp01(s.OpeningCounterSwingFirstPercent / 100.0);
            CounterLater = Clamp01(s.OpeningCounterSwingLaterPercent / 100.0);
            LinkWindow = Pos(s.OpeningChainResetSeconds);
            FlightMax = Pos(s.OpeningHoldMaxSeconds);
            SidesCrossed = s.OpeningSidesCrossed;
            ShieldSmallSeconds = Pos(s.OpeningHoldShieldSmallSeconds);
            ShieldBigSeconds = Pos(s.OpeningHoldShieldBigSeconds);
            ShieldSmallSize = Pos(s.OpeningHoldShieldSmallLength);
            ShieldBigSize = Math.Max(ShieldSmallSize + 1, Pos(s.OpeningHoldShieldBigLength));
        }

        public static OpeningHoldRules From(TraxSettings s) => new OpeningHoldRules(s);

        public bool ModEnabled { get; }
        public bool OpeningsSwitch { get; }
        public bool Switch { get; }

        /// <summary>The master switch FIRST, then <c>OpeningsEnabled</c>, then <c>OpeningHoldsEnabled</c>.</summary>
        public bool On => ModEnabled && OpeningsSwitch && Switch;

        public string? OffBecause => !ModEnabled ? "ModEnabled" : !OpeningsSwitch ? "OpeningsEnabled" : !Switch ? "OpeningHoldsEnabled" : null;

        /// <summary>The first hold at the reference weapon speed (s).</summary>
        public double FirstSeconds { get; }

        public int SpeedReference { get; }
        public double FactorMin { get; }
        public double FactorMax { get; }

        /// <summary>Added to the last hold's length at each link (s).</summary>
        public double PerLink { get; }

        /// <summary>The chain-speed bonus: at most (s), full within (s), none from (s).</summary>
        public double BonusMax { get; }
        public double BonusFull { get; }
        public double BonusZero { get; }

        /// <summary>No hold longer (s).</summary>
        public double Cap { get; }

        /// <summary>The counter-swing chance after the first hold / after a hold the chain lengthened (0-1).</summary>
        public double CounterFirst { get; }
        public double CounterLater { get; }

        /// <summary>After a hold ran out, a block of yours within this (s) is still a link (<c>OpeningChainResetSeconds</c>).</summary>
        public double LinkWindow { get; }

        /// <summary>A winning swing in the air keeps his guard held (and his 'can defend' off) at most this long after it commits (<c>OpeningHoldMaxSeconds</c>).</summary>
        public double FlightMax { get; }

        public bool SidesCrossed { get; }

        /// <summary>Toni's addition 2026-11-10: a SHIELDMAN's first hold at the reference weapon speed - a small shield (its length at or below
        /// <see cref="ShieldSmallSize"/>) / a big one (at or above <see cref="ShieldBigSize"/>), a straight line between (s; the shield's
        /// <c>WeaponLength</c>, cm).</summary>
        public double ShieldSmallSeconds { get; }
        public double ShieldBigSeconds { get; }
        public double ShieldSmallSize { get; }
        public double ShieldBigSize { get; }

        public string Describe()
        {
            var c = CultureInfo.InvariantCulture;
            if (!On) return "hold-block openings OFF (" + OffBecause + ") - unshielded men follow step 106 (off balance after his block)";
            return "hold-block openings ON (step 107): your landed front hit on a man WITHOUT a shield -> he HOLDS a block in a random direction for "
                   + FirstSeconds.ToString("0.0#", c) + " s x clamp(" + SpeedReference.ToString(c) + " / your weapon's speed, " + FactorMin.ToString("0.0#", c) + "-"
                   + FactorMax.ToString("0.0#", c) + "), the red arrow on the opening; a sword must strike exactly the opening, any other weapon any side but his guard"
                   + " -> he staggers; into his guard = a block = a link: re-held for + " + PerLink.ToString("0.0#", c) + " s (+ up to " + BonusMax.ToString("0.0#", c)
                   + " s for your next wind-up within " + BonusFull.ToString("0.0#", c) + "-" + BonusZero.ToString("0.0#", c) + " s), at most " + Cap.ToString("0.0#", c)
                   + " s; a hold over without a stagger -> his counter-swing " + (100 * CounterFirst).ToString("0", c) + "% (first hold) / "
                   + (100 * CounterLater).ToString("0", c) + "% (a lengthened one); a SHIELDMAN (your hit, or his correct shield block of your attack) holds his shield "
                   + "in the unbalanced guard for " + ShieldSmallSeconds.ToString("0.0#", c) + " s (a small shield, length " + ShieldSmallSize.ToString("0", c) + ") to "
                   + ShieldBigSeconds.ToString("0.0#", c) + " s (a big one, " + ShieldBigSize.ToString("0", c) + ") x the same weapon factor, links as above; a winning "
                   + "strike LOWERS his shield (GUARD BROKEN)";
        }

        private static double Pos(double v) => double.IsNaN(v) ? 0 : Math.Max(0, Math.Round(v, 5));

        private static double Clamp01(double v) => double.IsNaN(v) ? 0 : Math.Max(0, Math.Min(1, v));
    }

    /// <summary>
    /// Step 107 - HOLD-BLOCK OPENINGS, the pure math (AI_NOTES "Step 107", DESIGN decision 96). Directions are the engine's <c>UsageDirection</c>
    /// (<see cref="OpeningMath"/>). No allocation.
    /// </summary>
    public static class OpeningHoldMath
    {
        /// <summary>The engine's <c>MovementControlFlag</c> attack bits (v1.4.8 - the offline smoke checks them against the game's enum).</summary>
        public const uint AttackLeft = 0x40, AttackRight = 0x80, AttackUp = 0x100, AttackDown = 0x200;

        /// <summary>The forced counter-swing's READY (his attack bit held) before it is let go - the release (s). A constant (his AI readies ~0.3-0.5 s).</summary>
        public const double CounterReadySeconds = 0.4;

        /// <summary>After the release his attack and guard bits stay out this long (s), so his own AI cannot cancel the swing; then he is his own.</summary>
        public const double CounterReleaseSeconds = 0.5;

        /// <summary>The weapon-speed factor on the first hold: reference / speed clamped to [min, max]; an unknown speed (≤ 0, NaN) = 1.</summary>
        public static double SpeedFactor(in OpeningHoldRules r, double weaponSpeed)
        {
            if (double.IsNaN(weaponSpeed) || double.IsInfinity(weaponSpeed) || weaponSpeed <= 0) return Math.Max(r.FactorMin, Math.Min(r.FactorMax, 1.0));
            return Math.Max(r.FactorMin, Math.Min(r.FactorMax, r.SpeedReference / weaponSpeed));
        }

        /// <summary>THE FIRST HOLD (s): <c>OpeningHoldFirstSeconds</c> x the weapon-speed factor, at most the cap.</summary>
        public static double FirstHold(in OpeningHoldRules r, double weaponSpeed) => Math.Min(r.Cap, r.FirstSeconds * SpeedFactor(in r, weaponSpeed));

        /// <summary>The shield's place between small (0) and big (1) by its length (cm; the engine's <c>WeaponLength</c>); unknown = 0.5.</summary>
        public static double ShieldSizeShare(in OpeningHoldRules r, double shieldLength)
        {
            if (double.IsNaN(shieldLength) || shieldLength <= 0) return 0.5;
            return Math.Max(0, Math.Min(1, (shieldLength - r.ShieldSmallSize) / (r.ShieldBigSize - r.ShieldSmallSize)));
        }

        /// <summary>A SHIELDMAN's FIRST HOLD (s): small → big shield seconds by its length, x the weapon-speed factor, at most the cap - short, so the
        /// shield keeps its advantage ("almost impossible to hit the first time").</summary>
        public static double ShieldFirstHold(in OpeningHoldRules r, double shieldLength, double weaponSpeed)
        {
            double share = ShieldSizeShare(in r, shieldLength);
            double at95 = r.ShieldSmallSeconds + (r.ShieldBigSeconds - r.ShieldSmallSeconds) * share;
            return Math.Min(r.Cap, Math.Max(0, at95) * SpeedFactor(in r, weaponSpeed));
        }

        /// <summary>The speed share of your next strike: 1 when it starts within <c>...FullSeconds</c> of the event (his block / the hold's start), 0 from
        /// <c>...ZeroSeconds</c>, a straight line between; NaN / negative = 0.</summary>
        public static double ChainSpeedShare(in OpeningHoldRules r, double delay)
        {
            if (double.IsNaN(delay) || delay < 0) return 0;
            if (delay <= r.BonusFull + 1e-9) return 1;
            if (delay >= r.BonusZero) return 0;
            return Math.Max(0, Math.Min(1, (r.BonusZero - delay) / (r.BonusZero - r.BonusFull)));
        }

        /// <summary>THE CHAIN-SPEED BONUS (s) for your next wind-up <paramref name="delay"/> s after his block.</summary>
        public static double ChainBonus(in OpeningHoldRules r, double delay) => r.BonusMax * ChainSpeedShare(in r, delay);

        /// <summary>A LINK's hold (s): the last hold's length + <c>OpeningHoldPerLinkSeconds</c>, at most the cap.</summary>
        public static double LinkHold(in OpeningHoldRules r, double lastLength) => Math.Min(r.Cap, Math.Max(0, lastLength) + r.PerLink);

        /// <summary>A hold lengthened by <paramref name="bonus"/> (s), at most the cap.</summary>
        public static double Extend(in OpeningHoldRules r, double length, double bonus) => Math.Min(r.Cap, Math.Max(0, length) + Math.Max(0, bonus));

        /// <summary>A 4-direction weapon (a sword - swing and thrust): it must hit the opening EXACTLY.</summary>
        public static bool ExactSide(int weaponMask) => OpeningMath.DirectionCount(weaponMask) >= 4;

        /// <summary>
        /// WHO WINS THE HOLD: your <paramref name="strike"/> against his held <paramref name="guard"/> with your weapon's <paramref name="weaponMask"/>. Into
        /// the attack G blocks = <see cref="HoldSwing.IntoGuard"/>; a 4-direction weapon wins only EXACTLY on the opening O (else a third side); any other
        /// weapon (axes, maces - no thrust; spears - two stabs) wins with any direction G does not block.
        /// </summary>
        public static HoldSwing Classify(int guard, int strike, int weaponMask, bool sidesCrossed)
        {
            if (!OpeningMath.IsGuard(guard) || !OpeningMath.IsAttack(strike)) return HoldSwing.Unknown;
            if (strike == OpeningMath.AttackBlockedBy(guard, sidesCrossed)) return HoldSwing.IntoGuard;
            if (!ExactSide(weaponMask)) return HoldSwing.Wins;
            return strike == OpeningMath.OpeningFor(guard, sidesCrossed) ? HoldSwing.Wins : HoldSwing.ThirdSide;
        }

        /// <summary>The sides that win against <paramref name="guard"/> with this weapon (a mask of attacks) - the exact opening for a sword, every one of the
        /// weapon's directions but the guarded one otherwise.</summary>
        public static int WinMask(int guard, int weaponMask, bool sidesCrossed)
        {
            if (!OpeningMath.IsGuard(guard)) return 0;
            if (ExactSide(weaponMask)) return OpeningMath.Bit(OpeningMath.OpeningFor(guard, sidesCrossed));
            return weaponMask & OpeningMath.AllAttacks & ~OpeningMath.Bit(OpeningMath.AttackBlockedBy(guard, sidesCrossed));
        }

        /// <summary>THE COUNTER-SWING CHANCE when a hold ends without a stagger: the first hold of a chain (no link) / a hold the chain lengthened.</summary>
        public static double CounterChance(in OpeningHoldRules r, int links) => links > 0 ? r.CounterLater : r.CounterFirst;

        /// <summary>The die throws the counter-swing: below the chance (a 100 % chance always, a 0 % never).</summary>
        public static bool CounterThrown(double die, double chance) => chance >= 1 || (!double.IsNaN(die) && die < chance);

        /// <summary>His counter-swing's direction: one of HIS weapon's attack directions, uniformly by <paramref name="die"/>; an empty mask = all four.</summary>
        public static int CounterDirection(int weaponMask, double die)
        {
            int mask = (weaponMask & OpeningMath.AllAttacks) == 0 ? OpeningMath.AllAttacks : weaponMask & OpeningMath.AllAttacks;
            int n = OpeningMath.DirectionCount(mask);
            int pick = double.IsNaN(die) ? 0 : (int)Math.Floor(Math.Max(0, Math.Min(0.999999999, die)) * n);
            for (int d = OpeningMath.AttackUp; d <= OpeningMath.AttackRight; d++)
            {
                if ((mask & OpeningMath.Bit(d)) == 0) continue;
                if (pick == 0) return d;
                pick--;
            }
            return OpeningMath.AttackUp;
        }

        /// <summary>An attack direction as its movement bit (0 for none).</summary>
        public static uint AttackFlag(int attack) => attack switch
        {
            OpeningMath.AttackUp => AttackUp,
            OpeningMath.AttackDown => AttackDown,
            OpeningMath.AttackLeft => AttackLeft,
            OpeningMath.AttackRight => AttackRight,
            _ => 0u,
        };

        /// <summary>
        /// ONE FRAME of the forced counter-swing: READY = his attack and guard bits replaced by <paramref name="attackFlag"/> (held - the engine readies the
        /// swing); RELEASE = every attack and guard bit out (the held attack let go - the engine releases it; no guard so it is not cancelled). His moves
        /// and every other bit stay his. <paramref name="edit"/>: GuardRaised = our attack bit written, AttackCleared / GuardCleared = his own taken out.
        /// </summary>
        public static uint CounterFrame(uint flags, uint attackFlag, bool ready, out InputEdit edit)
        {
            edit = InputEdit.None;
            uint own = flags & (AiInputMath.AttackMask | OpeningForceMath.GuardBits);
            uint f = flags & ~(AiInputMath.AttackMask | OpeningForceMath.GuardBits);
            if ((own & OpeningForceMath.GuardBits) != 0) edit |= InputEdit.GuardCleared;
            if (ready && attackFlag != 0)
            {
                if ((own & AiInputMath.AttackMask) != attackFlag) edit |= InputEdit.GuardRaised;
                return f | (attackFlag & AiInputMath.AttackMask);
            }
            if ((own & AiInputMath.AttackMask) != 0) edit |= InputEdit.AttackCleared;
            return f;
        }

        public static string SwingName(HoldSwing s) => s switch
        {
            HoldSwing.Wins => "WINS the hold",
            HoldSwing.IntoGuard => "into his held guard",
            HoldSwing.ThirdSide => "a third side",
            _ => "direction unread",
        };

        public static string EndName(HoldEnd e) => e switch
        {
            HoldEnd.Staggered => "staggered",
            HoldEnd.Timeout => "the hold ran out",
            HoldEnd.FreshHold => "a fresh hold",
            HoldEnd.HeHitYou => "he hit you",
            HoldEnd.TargetSwitch => "another target",
            HoldEnd.BackHit => "a hit from behind",
            HoldEnd.Gap => "no link after the hold",
            HoldEnd.Off => "off / gone",
            _ => "?",
        };

        /// <summary>The weapon class index by attack directions: 0 = 2 or fewer (spears), 1 = 3 (no thrust - axes, maces), 2 = 4 (swords).</summary>
        public static int ClassIndex(int weaponMask)
        {
            int n = OpeningMath.DirectionCount(weaponMask);
            return n >= 4 ? 2 : n == 3 ? 1 : 0;
        }
    }

    /// <summary>
    /// Step 107: THE HOLD CHAIN on one man (a struct the logic keeps; pure). None → HOLDING (your landed front hit on an unshielded man: a random guard G,
    /// the first hold) → a LINK (he blocks your strike into G, or blocks you in the AFTER phase): re-held from the block for the last length + per link,
    /// your next wind-up within the bonus time adds the chain-speed bonus → AFTER (the hold ran out: the counter-swing roll; a block within the link
    /// window is still a link) → None. The stagger / a fresh hold / the switches end it in the logic.
    /// </summary>
    public struct OpeningHold
    {
        /// <summary>The man (an agent index; −1 none).</summary>
        public int Target;

        public HoldPhase Phase;

        /// <summary>His held guard G (an engine UsageDirection 4-7) and the opening it leaves, O (0-3).</summary>
        public int Guard, Opening;

        /// <summary>Links so far in this chain (0 = the first hold); holds = links + 1.</summary>
        public int Links;

        /// <summary>The current hold: its start (the entry hit / the last block), its end, its length (s).</summary>
        public double Start, Until, Length;

        /// <summary>The chain-speed bonus given in this hold (s) - at most once a hold, at your first wind-up after a link.</summary>
        public double Bonus;
        public bool WindUpSeen;

        /// <summary>Your latest wind-up's speed share in this hold (0-1) - the perfect strike's timing after a hold's stagger.</summary>
        public double Score;

        /// <summary>After the hold ran out: a block until this is still a link.</summary>
        public double AfterUntil;

        public bool Active => Phase != HoldPhase.None && Target >= 0;

        public bool Holding(int target) => Phase == HoldPhase.Holding && Target >= 0 && Target == target;

        /// <summary>A block by <paramref name="target"/> now is a LINK: he holds now, or the hold ran out within the link window.</summary>
        public bool LinkOpen(int target, double now) =>
            Target >= 0 && Target == target && (Phase == HoldPhase.Holding || (Phase == HoldPhase.After && now <= AfterUntil + 1e-9));

        /// <summary>A FRESH hold on <paramref name="target"/> (link 0) in <paramref name="guard"/> for <paramref name="length"/> s.</summary>
        public void Begin(int target, int guard, bool sidesCrossed, double now, double length)
        {
            Target = target;
            Phase = HoldPhase.Holding;
            Guard = guard;
            Opening = OpeningMath.OpeningFor(guard, sidesCrossed);
            Links = 0;
            Start = now;
            Length = Math.Max(0, length);
            Until = now + Length;
            Bonus = 0;
            WindUpSeen = false;
            Score = 0;
            AfterUntil = 0;
        }

        /// <summary>A LINK at <paramref name="now"/> (his block): re-held from now for the last length + per link (capped); the same guard. Returns the new length.</summary>
        public double Link(double now, in OpeningHoldRules r)
        {
            Links++;
            Phase = HoldPhase.Holding;
            Length = OpeningHoldMath.LinkHold(in r, Length);
            Start = now;
            Until = now + Length;
            Bonus = 0;
            WindUpSeen = false;
            Score = 0;
            AfterUntil = 0;
            return Length;
        }

        /// <summary>
        /// Your WIND-UP at <paramref name="now"/> during the hold: its speed share (since the hold's start) is the timing score; the FIRST wind-up after a
        /// LINK also lengthens the hold by the chain-speed bonus (capped). Returns the bonus added (s).
        /// </summary>
        public double NoteWindUp(double now, in OpeningHoldRules r)
        {
            if (Phase != HoldPhase.Holding) return 0;
            double delay = now - Start;
            Score = OpeningHoldMath.ChainSpeedShare(in r, delay);
            if (WindUpSeen) return 0;
            WindUpSeen = true;
            if (Links == 0) return 0;
            double want = OpeningHoldMath.ChainBonus(in r, delay);
            double length = OpeningHoldMath.Extend(in r, Length, want);
            double added = length - Length;
            Length = length;
            Until = Start + Length;
            Bonus = added;
            return added;
        }

        /// <summary>The hold has run out at <paramref name="now"/>.</summary>
        public bool HoldOver(double now) => Phase == HoldPhase.Holding && now >= Until - 1e-9;

        /// <summary>The hold ran out: the AFTER phase (a block within the link window is still a link).</summary>
        public void EndHold(double now, in OpeningHoldRules r)
        {
            Phase = HoldPhase.After;
            AfterUntil = now + r.LinkWindow;
        }

        /// <summary>The AFTER phase is over.</summary>
        public bool Lapsed(double now) => Phase == HoldPhase.After && now > AfterUntil + 1e-9;

        public void Reset() => this = None;

        public static OpeningHold None => new OpeningHold { Target = -1, Guard = OpeningMath.Unknown, Opening = OpeningMath.Unknown };
    }

    /// <summary>Step 107: the hold-block openings over one battle - the [summary] "openings - hold-block" line. Main thread; no allocation per note.</summary>
    public sealed class OpeningHoldStats
    {
        /// <summary>Fresh holds (an entry hit) - all, after a sword's third side / a strike into the guard that landed anyway; links (during a hold / after it ran out).</summary>
        public int Holds, FreshAfterThirdSide, FreshAfterLeak, Links, LinksAfter;

        /// <summary>Hold lengths asked (fresh + links): sum / count / max (s); the first holds' weapon-speed factors.</summary>
        public double LengthSum, LengthMax, FactorSum;
        public int LengthCount, FactorCount;

        /// <summary>The chain-speed bonus: given (count, sum), full ones; the wind-ups after a link that came too late for any.</summary>
        public int BonusGiven, BonusFull, BonusNone;
        public double BonusSum;

        /// <summary>Your swings during a hold by <see cref="HoldSwing"/> x the weapon class (2 / 3 / 4 directions).</summary>
        public readonly int[] Swings = new int[(int)HoldSwing.Count * 3];

        /// <summary>Staggers from a hold by the weapon class; winning swings that took his 'can defend'; winning swings he blocked anyway.</summary>
        public readonly int[] Staggers = new int[3];
        public int FlagsOff, WinBlocked;

        /// <summary>Holds ended by <see cref="HoldEnd"/>.</summary>
        public readonly int[] Ends = new int[(int)HoldEnd.Count];

        /// <summary>Counter-swings: rolled after the first hold / a lengthened one, thrown after each, played (ready → release), cut short, refused (not his to drive).</summary>
        public int CounterRolledFirst, CounterRolledLater, CounterThrownFirst, CounterThrownLater, CounterReleased, CounterCut, CounterRefused;

        /// <summary>Entries refused: he was reeling / stagger-immune; an unshielded man's parry with no hold on him (no entry - 106's replaced).</summary>
        public int Immune, BlocksNoHold;

        /// <summary>Toni's shield addition: holds on SHIELDMEN (fresh - by a hit / by his shield block), their first holds' sum (s), shields LOWERED for a winning strike.</summary>
        public int ShieldHolds, ShieldHoldsByBlock, ShieldLowered;
        public double ShieldFirstSum;

        /// <summary>The arrow on the opening: holds that showed it (the opening known).</summary>
        public int ArrowShown;

        public int Errors;

        public void NoteLength(double length)
        {
            if (double.IsNaN(length)) return;
            LengthSum += length;
            LengthCount++;
            if (length > LengthMax) LengthMax = length;
        }

        public void NoteSwing(HoldSwing s, int weaponMask)
        {
            if (s >= HoldSwing.Count) return;
            Swings[(int)s * 3 + OpeningHoldMath.ClassIndex(weaponMask)]++;
        }

        public void NoteEnd(HoldEnd e)
        {
            if (e < HoldEnd.Count) Ends[(int)e]++;
        }

        public void NoteBonus(double bonus, double max)
        {
            if (!(bonus > 0))
            {
                BonusNone++;
                return;
            }
            BonusGiven++;
            BonusSum += bonus;
            if (bonus >= max - 1e-6) BonusFull++;
        }

        private static void Classes(StringBuilder t, int[] a, int offset, CultureInfo c) =>
            t.Append(a[offset + 2].ToString(c)).Append(" / ").Append(a[offset + 1].ToString(c)).Append(" / ").Append(a[offset].ToString(c));

        public string SummaryLine(in OpeningHoldRules r)
        {
            var c = CultureInfo.InvariantCulture;
            var t = new StringBuilder();
            t.Append("openings - hold-block (step 107; ").Append(r.On ? "ON" : "OFF (" + r.OffBecause + ")").Append("): holds started ").Append(Holds.ToString(c))
             .Append(" (after a sword's third side ").Append(FreshAfterThirdSide.ToString(c)).Append(", after a strike into the guard that landed anyway ").Append(FreshAfterLeak.ToString(c))
             .Append("), LINKS ").Append(Links.ToString(c)).Append(" (after the hold ran out ").Append(LinksAfter.ToString(c)).Append(')');
            t.Append("; SHIELDMEN: holds ").Append(ShieldHolds.ToString(c)).Append(" (by his shield block ").Append(ShieldHoldsByBlock.ToString(c)).Append(')');
            if (ShieldHolds > 0) t.Append(", first hold mean ").Append((ShieldFirstSum / ShieldHolds).ToString("0.00", c)).Append(" s");
            t.Append(", shields LOWERED for a winning strike (GUARD BROKEN) ").Append(ShieldLowered.ToString(c));
            t.Append("; hold lengths");
            if (LengthCount > 0)
                t.Append(" mean ").Append((LengthSum / LengthCount).ToString("0.00", c)).Append(" s, longest ").Append(LengthMax.ToString("0.00", c)).Append(" s (")
                 .Append(LengthCount.ToString(c)).Append(" asked)");
            else t.Append(" none");
            if (FactorCount > 0) t.Append(", the weapon-speed factor mean x").Append((FactorSum / FactorCount).ToString("0.00", c));
            t.Append("; chain-speed bonus given ").Append(BonusGiven.ToString(c)).Append(" (full ").Append(BonusFull.ToString(c));
            if (BonusGiven > 0) t.Append(", mean +").Append((BonusSum / BonusGiven).ToString("0.00", c)).Append(" s");
            t.Append("), too slow for any ").Append(BonusNone.ToString(c));
            t.Append("; your swings in a hold (sword / axe-mace / spear): WINS ");
            Classes(t, Swings, (int)HoldSwing.Wins * 3, c);
            t.Append(", into his guard ");
            Classes(t, Swings, (int)HoldSwing.IntoGuard * 3, c);
            t.Append(", a third side ");
            Classes(t, Swings, (int)HoldSwing.ThirdSide * 3, c);
            t.Append(", unread ");
            Classes(t, Swings, (int)HoldSwing.Unknown * 3, c);
            t.Append("; STAGGERS by weapon (sword / axe-mace / spear) ");
            Classes(t, Staggers, 0, c);
            t.Append(" ('can defend' taken for a winning swing ").Append(FlagsOff.ToString(c)).Append(", a winning swing blocked anyway ").Append(WinBlocked.ToString(c)).Append(')');
            t.Append("; COUNTER-SWINGS after the first hold rolled ").Append(CounterRolledFirst.ToString(c)).Append(" / thrown ").Append(CounterThrownFirst.ToString(c))
             .Append(", after a lengthened hold rolled ").Append(CounterRolledLater.ToString(c)).Append(" / thrown ").Append(CounterThrownLater.ToString(c))
             .Append(" (released ").Append(CounterReleased.ToString(c)).Append(", cut short ").Append(CounterCut.ToString(c)).Append(", refused ").Append(CounterRefused.ToString(c)).Append(')');
            t.Append("; the arrow on the opening shown for ").Append(ArrowShown.ToString(c)).Append(" holds (the poses: the forced-guard line's 'the balance guard with a weapon')");
            t.Append("; no hold: reeling / stagger-immune ").Append(Immune.ToString(c)).Append(", his parries with no hold on him (step 106's off balance replaced) ")
             .Append(BlocksNoHold.ToString(c));
            t.Append("; ended by");
            for (int i = 0; i < (int)HoldEnd.Count; i++)
                t.Append(i == 0 ? " " : ", ").Append(OpeningHoldMath.EndName((HoldEnd)i)).Append(' ').Append(Ends[i].ToString(c));
            if (Errors > 0) t.Append("; ERRORS ").Append(Errors.ToString(c));
            return t.ToString();
        }
    }
}

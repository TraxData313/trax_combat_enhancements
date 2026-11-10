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

        /// <summary>Step 111 (Toni's training field 2026-11-10, pic 9): YOU reel or lie down (his riposte, a kick, a hit) - he is no longer off balance.</summary>
        YouReel,

        Count,
    }

    /// <summary>Step 111: why a hold was NOT started (or a link not taken) - one rule for every route a hold starts by (your hit, his shield block,
    /// a chain after GUARD BROKEN).</summary>
    public enum HoldRefusal : byte
    {
        None,

        /// <summary>He reels now (our stagger - an opening, a riposte, a back hit, any <c>ImpactState</c> reel): the PERFECT STRIKE window, never a hold.</summary>
        HeReels,

        /// <summary>His reel is over but he is still inside the stagger immunity after it: not recovered yet (his block there is a plain block).</summary>
        HeImmune,

        /// <summary>You reel or lie down (his riposte - "X ripostes - you reel" -, a kick, a hit): he is clearly not off balance.</summary>
        YouReel,
    }

    /// <summary>Step 107: where the hold chain on one man stands.</summary>
    public enum HoldPhase : byte
    {
        None,

        /// <summary>He holds the forced block now.</summary>
        Holding,

        /// <summary>The hold ran out; a block of yours within <c>OpeningChainResetSeconds</c> is still a link.</summary>
        After,

        /// <summary>Step 108: from the hit until his guard is VISIBLE (or the recovery cap): his attacks held off, the guard pushed, no arrow, the
        /// hold's clock not started.</summary>
        Recovering,
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
            SwingSeconds = Pos(s.OpeningHoldSwingSeconds);
            MarginAt0 = Pos(s.OpeningHoldReadMarginAtSkill0);
            MarginAt200 = Pos(s.OpeningHoldReadMarginAtSkill200);
            RecoveryCap = Pos(s.OpeningHoldRecoveryCapSeconds);
            ArrowShow = s.OpeningHoldArrowShow;
            Message = s.OpeningHoldMessage;
            InfoLines = Math.Max(0, s.OpeningLogInfoLines);
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
            EndsWhenYouReel = s.OpeningHoldEndsWhenYouReel;
            ArrowSwingMax = Pos(s.OpeningArrowSwingHoldMaxSeconds);
        }

        public static OpeningHoldRules From(TraxSettings s) => new OpeningHoldRules(s);

        public bool ModEnabled { get; }
        public bool OpeningsSwitch { get; }
        public bool Switch { get; }

        /// <summary>The master switch FIRST, then <c>OpeningsEnabled</c>, then <c>OpeningHoldsEnabled</c>.</summary>
        public bool On => ModEnabled && OpeningsSwitch && Switch;

        public string? OffBecause => !ModEnabled ? "ModEnabled" : !OpeningsSwitch ? "OpeningsEnabled" : !Switch ? "OpeningHoldsEnabled" : null;

        /// <summary>Step 108: YOUR swing's time at the reference weapon speed (s) - the first hold of a man without a shield is this + the reading
        /// margin, x the weapon-speed factor.</summary>
        public double SwingSeconds { get; }

        /// <summary>Step 108: the reading margin (s) at your weapon skill 0 and 200 (a straight line, clamped above 200).</summary>
        public double MarginAt0 { get; }
        public double MarginAt200 { get; }

        /// <summary>Step 108: the longest wait from the hit for his guard to show before the hold's clock starts anyway (s; 0 = the clock from the hit,
        /// step 107).</summary>
        public double RecoveryCap { get; }

        /// <summary>Step 108: the red arrow on a hold's opening (<c>OpeningHoldArrowShow</c>) / the "Unbalanced ..." line (<c>OpeningHoldMessage</c>).</summary>
        public bool ArrowShow { get; }
        public bool Message { get; }

        /// <summary>Step 108: the openings' per-event log lines at Info per battle (then verbose) - <c>OpeningLogInfoLines</c>.</summary>
        public int InfoLines { get; }

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

        /// <summary>Step 111: YOU reeling or down ends his hold and refuses a new one (<c>OpeningHoldEndsWhenYouReel</c>).</summary>
        public bool EndsWhenYouReel { get; }

        /// <summary>Step 111: the red arrow keeps its direction through your swing (wind-up to contact / miss) at most this long (s;
        /// <c>OpeningArrowSwingHoldMaxSeconds</c>; 0 = no latch - step 108's arrow).</summary>
        public double ArrowSwingMax { get; }

        public string Describe()
        {
            var c = CultureInfo.InvariantCulture;
            if (!On) return "hold-block openings OFF (" + OffBecause + ") - unshielded men follow step 106 (off balance after his block)";
            return "hold-block openings ON (steps 107-108): your landed front hit on a man WITHOUT a shield -> his attacks held off and his guard (a random "
                   + "direction) pushed until it SHOWS (at most " + RecoveryCap.ToString("0.0#", c) + " s), then he HOLDS that block for (your swing "
                   + SwingSeconds.ToString("0.0#", c) + " s + a reading margin " + MarginAt0.ToString("0.0#", c) + "-" + MarginAt200.ToString("0.0#", c)
                   + " s by your weapon skill 0-200) x clamp(" + SpeedReference.ToString(c) + " / your weapon's speed, " + FactorMin.ToString("0.0#", c) + "-"
                   + FactorMax.ToString("0.0#", c) + "), the red arrow on the opening " + (ArrowShow ? "from that moment" : "OFF (OpeningHoldArrowShow)")
                   + (Message ? ", 'Unbalanced ...' on screen" : string.Empty) + "; a sword must strike exactly the opening, any other weapon any side but his guard"
                   + " -> he staggers; into his guard = a block = a link: re-held for + " + PerLink.ToString("0.0#", c) + " s (+ up to " + BonusMax.ToString("0.0#", c)
                   + " s for your next wind-up within " + BonusFull.ToString("0.0#", c) + "-" + BonusZero.ToString("0.0#", c) + " s), at most " + Cap.ToString("0.0#", c)
                   + " s; a hold over without a stagger -> his counter-swing " + (100 * CounterFirst).ToString("0", c) + "% (first hold) / "
                   + (100 * CounterLater).ToString("0", c) + "% (a lengthened one); a SHIELDMAN (your hit, or his correct shield block of your attack) holds his shield "
                   + "in the unbalanced guard for " + ShieldSmallSeconds.ToString("0.0#", c) + " s (a small shield, length " + ShieldSmallSize.ToString("0", c) + ") to "
                   + ShieldBigSeconds.ToString("0.0#", c) + " s (a big one, " + ShieldBigSize.ToString("0", c) + ") x the same weapon factor, links as above; a winning "
                   + "strike LOWERS his shield (GUARD BROKEN); step 111: no hold while he reels or is stagger-immune (every route, his shield block too)"
                   + (EndsWhenYouReel ? ", a hold ends when YOU reel" : string.Empty) + ", no kick / bash while he is held"
                   + (ArrowSwingMax > 0 ? ", the arrow never turns while your swing is in the air (at most " + ArrowSwingMax.ToString("0.0#", c) + " s)" : string.Empty);
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

        /// <summary>Step 108: the skill the reading margin reaches its top at (and is clamped above).</summary>
        public const double MarginTopSkill = 200;

        /// <summary>Step 108: THE READING MARGIN (s) by YOUR skill in the weapon you hold (the timed block's skill): <c>...AtSkill0</c> at 0, a straight
        /// line to <c>...AtSkill200</c> at 200, clamped outside; an unknown skill = the skill-0 margin.</summary>
        public static double ReadMargin(in OpeningHoldRules r, double skill)
        {
            double k = double.IsNaN(skill) || double.IsInfinity(skill) ? 0 : Math.Max(0, Math.Min(1, skill / MarginTopSkill));
            return r.MarginAt0 + (r.MarginAt200 - r.MarginAt0) * k;
        }

        /// <summary>THE FIRST HOLD of a man without a shield (s; step 108): (your swing time + the reading margin by your skill) x the weapon-speed
        /// factor, at most the cap. It counts from his VISIBLE guard.</summary>
        public static double FirstHold(in OpeningHoldRules r, double weaponSpeed, double skill) =>
            Math.Min(r.Cap, (r.SwingSeconds + ReadMargin(in r, skill)) * SpeedFactor(in r, weaponSpeed));

        /// <summary>Step 108: the recovery is over - his guard is visible, or the cap has run out since the hit.</summary>
        public static bool RecoveryOver(in OpeningHoldRules r, double since, double now, bool visible) => visible || now >= since + r.RecoveryCap - 1e-9;

        /// <summary>
        /// Step 108 - THE ON-SCREEN LINE when a hold starts, in the PLAYER's view (constant strings, no allocation). The word after "Unbalanced" is
        /// where his guard went as you see it - the side opposite the opening - and the brackets name the strike that wins, from the side the red
        /// arrow shows (the arrow is attacker-relative: top = your overhead "from top down", bottom = your thrust "from below", left / right = your
        /// swing from that side). This holds whatever <c>OpeningSidesCrossed</c> says: that setting only maps the engine's guard names onto the
        /// attacks they block. A weapon that wins on more than one side (an axe, a mace: any side but his guard) gets "Unbalanced - guard X, hit any
        /// other side", X the side his guard covers as you see it. Null = no guard / no opening.
        /// </summary>
        public static string? UnbalancedText(int guard, int weaponMask, bool sidesCrossed)
        {
            if (!OpeningMath.IsGuard(guard)) return null;
            int win = WinMask(guard, weaponMask, sidesCrossed);
            if (OpeningMath.DirectionCount(win) > 1)
            {
                return OpeningMath.AttackBlockedBy(guard, sidesCrossed) switch
                {
                    OpeningMath.AttackUp => "Unbalanced - guard HIGH, hit any other side",
                    OpeningMath.AttackDown => "Unbalanced - guard LOW, hit any other side",
                    OpeningMath.AttackLeft => "Unbalanced - guard LEFT, hit any other side",
                    OpeningMath.AttackRight => "Unbalanced - guard RIGHT, hit any other side",
                    _ => null,
                };
            }
            int o = OpeningMath.DirectionCount(win) == 1 ? FirstOf(win) : OpeningMath.OpeningFor(guard, sidesCrossed);
            return o switch
            {
                OpeningMath.AttackUp => "Unbalanced DOWN (hit from top down)",
                OpeningMath.AttackDown => "Unbalanced UP (hit from below)",
                OpeningMath.AttackLeft => "Unbalanced RIGHT (hit from the left)",
                OpeningMath.AttackRight => "Unbalanced LEFT (hit from the right)",
                _ => null,
            };
        }

        private static int FirstOf(int mask)
        {
            for (int d = OpeningMath.AttackUp; d <= OpeningMath.AttackRight; d++)
                if ((mask & OpeningMath.Bit(d)) != 0) return d;
            return OpeningMath.Unknown;
        }

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
            HoldEnd.YouReel => "you reeled",
            _ => "?",
        };

        /// <summary>
        /// Step 111 (Toni's training field 2026-11-10): ONE gate for every route a hold starts by (your landed hit, his shield block, a link after it):
        /// you reeling or down first (with <paramref name="endsWhenYouReel"/>), then he reels (our stagger - the perfect-strike window), then he is
        /// still inside the stagger immunity after it (not recovered). <see cref="HoldRefusal.None"/> = a hold may start; step 108's recovery then waits
        /// for his guard to SHOW.
        /// </summary>
        public static HoldRefusal Refuse(bool heReels, bool heImmune, bool youReel, bool endsWhenYouReel)
        {
            if (youReel && endsWhenYouReel) return HoldRefusal.YouReel;
            if (heReels) return HoldRefusal.HeReels;
            return heImmune ? HoldRefusal.HeImmune : HoldRefusal.None;
        }

        public static string RefusalName(HoldRefusal r) => r switch
        {
            HoldRefusal.HeReels => "he still reels (the perfect-strike window)",
            HoldRefusal.HeImmune => "he is not recovered (inside the stagger immunity)",
            HoldRefusal.YouReel => "you reel",
            _ => "none",
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

        /// <summary>Step 108: he is still RECOVERING from the hit (his guard not shown yet).</summary>
        public bool Recovering(int target) => Phase == HoldPhase.Recovering && Target >= 0 && Target == target;

        /// <summary>Step 108: his guard is forced now - recovering or holding.</summary>
        public bool Forced(int target) => (Phase == HoldPhase.Holding || Phase == HoldPhase.Recovering) && Target >= 0 && Target == target;

        /// <summary>A block by <paramref name="target"/> now is a LINK: he holds now (or is recovering - his block shows the guard), or the hold ran
        /// out within the link window.</summary>
        public bool LinkOpen(int target, double now) =>
            Target >= 0 && Target == target && (Phase == HoldPhase.Holding || Phase == HoldPhase.Recovering || (Phase == HoldPhase.After && now <= AfterUntil + 1e-9));

        /// <summary>Step 108: the hit (or his shield block) that began this chain - the recovery's start.</summary>
        public double HitAt;

        /// <summary>Step 108: how long his guard took to show (s; NaN while recovering), and whether the cap started the clock instead.</summary>
        public double RecoverySeconds;
        public bool RecoveryCapped;

        /// <summary>
        /// A FRESH hold on <paramref name="target"/> (link 0) in <paramref name="guard"/>, <paramref name="length"/> s long. Step 108: it begins
        /// RECOVERING (the clock not started) unless <paramref name="recoveryCap"/> is 0 (step 107 - the clock from the hit). While recovering
        /// <see cref="Until"/> is the latest the hold can end (the cap + the length) - the force's bound.
        /// </summary>
        public void Begin(int target, int guard, bool sidesCrossed, double now, double length, double recoveryCap = 0)
        {
            Target = target;
            Guard = guard;
            Opening = OpeningMath.OpeningFor(guard, sidesCrossed);
            Links = 0;
            HitAt = now;
            Start = now;
            Length = Math.Max(0, length);
            Bonus = 0;
            WindUpSeen = false;
            Score = 0;
            AfterUntil = 0;
            RecoveryCapped = false;
            if (recoveryCap > 0)
            {
                Phase = HoldPhase.Recovering;
                Until = now + recoveryCap + Length;
                RecoverySeconds = double.NaN;
            }
            else
            {
                Phase = HoldPhase.Holding;
                Until = now + Length;
                RecoverySeconds = 0;
            }
        }

        /// <summary>Step 108: his guard SHOWS (or the cap ran out - <paramref name="capped"/>): the hold's clock starts now.</summary>
        public void GuardShown(double now, bool capped)
        {
            if (Phase != HoldPhase.Recovering) return;
            Phase = HoldPhase.Holding;
            RecoverySeconds = Math.Max(0, now - HitAt);
            RecoveryCapped = capped;
            Start = now;
            Until = now + Length;
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

        public static OpeningHold None => new OpeningHold { Target = -1, Guard = OpeningMath.Unknown, Opening = OpeningMath.Unknown, RecoverySeconds = double.NaN };
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

        /// <summary>Step 107d: step 81's kick / bash / push roll at the counter-swing's own wind-up SKIPPED (a kick there would cancel the forced swing).</summary>
        public int CounterKickRollsSkipped;

        /// <summary>Entries refused: he was reeling / stagger-immune; an unshielded man's parry with no hold on him (no entry - 106's replaced).</summary>
        public int Immune, BlocksNoHold;

        /// <summary>Toni's shield addition: holds on SHIELDMEN (fresh - by a hit / by his shield block), their first holds' sum (s), shields LOWERED for a winning strike.</summary>
        public int ShieldHolds, ShieldHoldsByBlock, ShieldLowered;
        public double ShieldFirstSum;

        /// <summary>The arrow on the opening: holds that showed it (the opening known).</summary>
        public int ArrowShown;

        public int Errors;

        /// <summary>Step 108: THE RECOVERY (hit -> guard visible): holds that waited for it, guards seen (their seconds: sum / max), started by the cap,
        /// cut before the guard showed (a hit / block / end); the recovery's frames whose guard read was unread.</summary>
        public int Recoveries, RecoveredSeen, RecoveryCapped, RecoveryCut;
        public double RecoverySum, RecoveryMax;

        /// <summary>Step 108: his blow landed on you during the chain - while recovering, while holding (forced), in the AFTER phase after a thrown
        /// counter-swing, in the AFTER phase on his own (no counter-swing); a pending kick held off by a forced guard.</summary>
        public int HitYouRecovering, HitYouHolding, HitYouAfterCounter, HitYouAfterOwn;

        /// <summary>Step 108: the "Unbalanced ..." line shown; holds whose arrow was off by its switch.</summary>
        public int MessagesShown, ArrowOff;

        /// <summary>Step 111: holds REFUSED by <see cref="OpeningHoldMath.Refuse"/> - by your hit / by his block x the refusal (None unused).</summary>
        public readonly int[] RefusedByHit = new int[4], RefusedByBlock = new int[4];

        /// <summary>Step 111: your swings whose arrow change was HELD (the arrow kept its direction until the swing resolved); "Unbalanced" lines
        /// deferred until your swing resolved; step 81's kick / bash rolls refused for a man under a hold / forced guard; running kick / bash
        /// attempts cut when a forced guard began on him.</summary>
        public int ArrowSwingsHeld, MessagesDeferred, KickRollsRefused, KicksCut;

        public void NoteRefused(HoldRefusal r, bool byBlock)
        {
            if (r == HoldRefusal.None || (int)r >= 4) return;
            (byBlock ? RefusedByBlock : RefusedByHit)[(int)r]++;
        }

        public void NoteRecovery(double seconds, bool capped)
        {
            if (capped) RecoveryCapped++;
            else RecoveredSeen++;
            if (double.IsNaN(seconds)) return;
            RecoverySum += seconds;
            if (seconds > RecoveryMax) RecoveryMax = seconds;
        }

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
             .Append(" (released ").Append(CounterReleased.ToString(c)).Append(", cut short ").Append(CounterCut.ToString(c)).Append(", refused ").Append(CounterRefused.ToString(c))
             .Append(", step 81's kick roll skipped for it ").Append(CounterKickRollsSkipped.ToString(c)).Append(')');
            t.Append("; the arrow on the opening shown for ").Append(ArrowShown.ToString(c)).Append(" holds (off by its switch ").Append(ArrowOff.ToString(c))
             .Append("; the poses: the forced-guard line's 'the balance guard with a weapon')");
            t.Append("; STEP 108 - THE RECOVERY (hit -> his guard visible, the hold's clock from then): waited ").Append(Recoveries.ToString(c)).Append(", guard SEEN ")
             .Append(RecoveredSeen.ToString(c));
            int rn = RecoveredSeen + RecoveryCapped;
            if (rn > 0) t.Append(" (mean ").Append((RecoverySum / rn).ToString("0.00", c)).Append(" s, longest ").Append(RecoveryMax.ToString("0.00", c)).Append(" s)");
            t.Append(", started by the cap ").Append(RecoveryCapped.ToString(c)).Append(" (").Append(r.RecoveryCap.ToString("0.0#", c)).Append(" s), cut before it showed ")
             .Append(RecoveryCut.ToString(c)).Append("; 'Unbalanced ...' shown ").Append(MessagesShown.ToString(c));
            t.Append("; HIS BLOWS ON YOU in a chain: while recovering ").Append(HitYouRecovering.ToString(c)).Append(", while holding ").Append(HitYouHolding.ToString(c))
             .Append(", after the hold - his forced counter-swing ").Append(HitYouAfterCounter.ToString(c)).Append(", his own attack ").Append(HitYouAfterOwn.ToString(c));
            t.Append("; no hold: reeling / stagger-immune ").Append(Immune.ToString(c)).Append(", his parries with no hold on him (step 106's off balance replaced) ")
             .Append(BlocksNoHold.ToString(c));
            t.Append("; STEP 111 - no hold (your hit / his block): he reels ").Append(RefusedByHit[(int)HoldRefusal.HeReels].ToString(c)).Append(" / ")
             .Append(RefusedByBlock[(int)HoldRefusal.HeReels].ToString(c)).Append(", not recovered (stagger-immune) ").Append(RefusedByHit[(int)HoldRefusal.HeImmune].ToString(c))
             .Append(" / ").Append(RefusedByBlock[(int)HoldRefusal.HeImmune].ToString(c)).Append(", you reel ").Append(RefusedByHit[(int)HoldRefusal.YouReel].ToString(c))
             .Append(" / ").Append(RefusedByBlock[(int)HoldRefusal.YouReel].ToString(c)).Append(" (").Append(r.EndsWhenYouReel ? "on" : "OFF - OpeningHoldEndsWhenYouReel")
             .Append("); your swings whose arrow change was held until they resolved ").Append(ArrowSwingsHeld.ToString(c)).Append(", 'Unbalanced' lines deferred past your swing ")
             .Append(MessagesDeferred.ToString(c)).Append("; kick / bash rolls refused for a held man ").Append(KickRollsRefused.ToString(c)).Append(", his running kick / bash cut by the forced guard ")
             .Append(KicksCut.ToString(c)).Append(" (both must stay the only way: a held man never kicks)");
            t.Append("; ended by");
            for (int i = 0; i < (int)HoldEnd.Count; i++)
                t.Append(i == 0 ? " " : ", ").Append(OpeningHoldMath.EndName((HoldEnd)i)).Append(' ').Append(Ends[i].ToString(c));
            if (Errors > 0) t.Append("; ERRORS ").Append(Errors.ToString(c));
            return t.ToString();
        }
    }
}

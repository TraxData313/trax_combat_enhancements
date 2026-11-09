using System;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 104 (Toni 2026-11-09: "can you add the reel vs mounted troops as well somehow, like that 3rd round, we were on horsebacks fighting, I
    /// blocked, can you implement a similar system there?"; DESIGN decision 93, AI_NOTES "Step 104"): TIMED BLOCKS ON HORSEBACK, read live.
    /// <see cref="ShadowOn"/> = the master switch FIRST, then Athletics and the impact system, then <c>TimedBlocksEnabled</c> - the step-104a
    /// shadow decides on every correct block of a rider's blow whatever <c>TimedBlocksOnHorseback</c> says; <see cref="Live"/> adds that switch:
    /// step 104b's reel in the saddle is APPLIED (the rear and the knock-off stay shadow until 104c, the strike / push-out until 104d).
    /// </summary>
    public readonly struct TimedRiderRules
    {
        public TimedRiderRules(TraxSettings s)
        {
            ModEnabled = s.ModEnabled;
            ImpactOn = s.AthleticsEnabled && s.ImpactEnabled;
            TimedOn = s.TimedBlocksEnabled;
            Switch = s.TimedBlocksOnHorseback;
            ReelMax = Math.Max(0, R(s.TimedRiderReelMaxSeconds));
            ReelYouMax = Math.Max(0, R(s.TimedRiderReelAgainstYouMaxSeconds));
            ReelOnYou = s.TimedRiderReelOnYou;
            ReelAction = s.TimedRiderReelAction ?? string.Empty;
            AnimSpeed = Math.Max(0.05, Nan(R(s.TimedRiderReelAnimSpeed), 1));
            AnimMax = Math.Max(0, Nan(R(s.TimedRiderReelAnimMaxSeconds), 1));
            NoGuard = s.TimedRiderNoGuard;
            HorseCheck = Clamp01(s.TimedRiderHorseCheckPercent / 100.0);
            FromFoot = s.TimedRiderFromFoot;
            RearMinScore = Clamp01(R(s.TimedRiderRearMinScore));
            RearShare = Clamp01(s.TimedRiderRearPercent / 100.0);
            DismountShare = Clamp01(s.TimedRiderDismountPercent / 100.0);
            DismountWeaponShare = Clamp01(s.TimedRiderDismountWeaponPercent / 100.0);
            StrikeOnHorseback = s.PerfectStrikeOnHorseback;
            StrikeAnySide = s.PerfectStrikeHorsebackAnySide;
            PushOutMounted = s.TimedBlockPushOutMounted;
            PerfectStrikeOn = s.PerfectStrikeEnabled;
            StrikeGrace = Math.Max(0, R(s.PerfectStrikeGraceSeconds));
            SlowMoOn = s.TimedBlockSlowMo;
            SlowMoMinScore = Clamp01(R(s.TimedBlockSlowMoMinScore));
            RiderWalk = R(s.ImpactMomentumRiderWalkMetresPerSecond);
            RiderGallop = R(s.ImpactMomentumRiderGallopMetresPerSecond);
            PerfectSkipsImmunity = s.TimedBlockPerfectSkipsImmunity;
        }

        public static TimedRiderRules From(TraxSettings s) => new TimedRiderRules(s);

        public bool ModEnabled { get; }
        public bool ImpactOn { get; }
        public bool TimedOn { get; }

        /// <summary><c>TimedBlocksOnHorseback</c> (the player page) - ON by default since step 104b (config format 29 moved 104a's OFF).</summary>
        public bool Switch { get; }

        /// <summary>The step-104a shadow decides (and logs) - the switch not needed: it says what the switch WOULD do.</summary>
        public bool ShadowOn => ModEnabled && ImpactOn && TimedOn;

        /// <summary>Step 104b: the reel in the saddle is APPLIED (the module also asks the impact system's riposte family to be live - not
        /// shadow mode).</summary>
        public bool Live => ShadowOn && Switch;

        public double ReelMax { get; }
        public double ReelYouMax { get; }
        public bool ReelOnYou { get; }

        /// <summary><c>TimedRiderReelAction</c> - THE REEL'S ANIMATION, played on the rider's channel 1 (upper body) only. Default
        /// <c>act_stagger_backward</c> (at <see cref="AnimSpeed"/> 1.0 - RBM's mounted stagger); Toni picks another by eye with the J test tool
        /// (its first rider candidate plays exactly this setting) and writes its name here - a setting change, no code; "none" = the lock only.</summary>
        public string ReelAction { get; }
        public double AnimSpeed { get; }
        public double AnimMax { get; }
        public bool NoGuard { get; }

        /// <summary>His horse's top speed share while he reels (0-1; 1 = never checked).</summary>
        public double HorseCheck { get; }

        public bool FromFoot { get; }
        public double RearMinScore { get; }
        public double RearShare { get; }
        public double DismountShare { get; }
        public double DismountWeaponShare { get; }
        public bool StrikeOnHorseback { get; }
        public bool StrikeAnySide { get; }
        public bool PushOutMounted { get; }
        public bool PerfectStrikeOn { get; }
        public double StrikeGrace { get; }
        public bool SlowMoOn { get; }
        public double SlowMoMinScore { get; }

        /// <summary>Step 53's rider momentum references (m/s) - the knock-off's speed band.</summary>
        public double RiderWalk { get; }
        public double RiderGallop { get; }

        /// <summary>Step 101: a perfect block ignores his immunity after his last stagger (never while he still reels).</summary>
        public bool PerfectSkipsImmunity { get; }

        public string? OffBecause => !ModEnabled ? "ModEnabled" : !ImpactOn ? "the impact system off" : !TimedOn ? "TimedBlocksEnabled" : null;

        public string Describe()
        {
            var c = CultureInfo.InvariantCulture;
            if (!ShadowOn) return "timed blocks on horseback: shadow OFF (" + OffBecause + ")";
            return "timed blocks on horseback: " + (Switch ? "the switch ON - the reel in the saddle LIVE (step 104b; the rear / knock-off / strike / push-out still shadow)" : "the switch OFF - SHADOW only, nothing applied")
                   + "; the reel up to " + ReelMax.ToString("0.00", c) + " s (on you " + ReelYouMax.ToString("0.00", c) + (ReelOnYou ? "" : ", off") + "), "
                   + (string.IsNullOrEmpty(ReelAction) ? "none" : ReelAction) + " x" + AnimSpeed.ToString("0.00", c) + " for at most " + AnimMax.ToString("0.00", c)
                   + " s then the lock" + (NoGuard ? " (no attack, no guard)" : " (no attack)") + ", his horse at " + (100 * HorseCheck).ToString("0", c) + "%; "
                   + (FromFoot ? "a man on foot reels a rider too" : "rider against rider only") + "; the rear from timing " + RearMinScore.ToString("0.00", c)
                   + " at " + (100 * RearShare).ToString("0", c) + "% x the timing; knocked off at a perfect block " + (100 * DismountShare).ToString("0", c)
                   + "% (a parry x" + DismountWeaponShare.ToString("0.00", c) + ") x his horse's speed band x the match; perfect strike from horseback "
                   + (StrikeOnHorseback ? (StrikeAnySide ? "any side" : "strict opposites") : "off") + "; a mounted push-out " + (PushOutMounted ? "his heavy parry" : "off");
        }

        private static double R(float v) => float.IsNaN(v) ? double.NaN : Math.Round((double)v, 5);

        private static double Nan(double v, double fallback) => double.IsNaN(v) || double.IsInfinity(v) ? fallback : v;

        private static double Clamp01(double v) => double.IsNaN(v) ? 0 : Math.Max(0, Math.Min(1, v));
    }

    /// <summary>Step 104: who rode, who blocked.</summary>
    public enum TimedRiderCombo : byte
    {
        None,

        /// <summary>A rider blocked a rider's blow (Toni's tournament round).</summary>
        RiderVsRider,

        /// <summary>A man on foot blocked a rider's blow (behind <c>TimedRiderFromFoot</c>).</summary>
        FootVsRider,

        /// <summary>A rider blocked a man on foot - the foot attacker's riposte works today; 104d adds the mounted push-out and the strike.</summary>
        RiderVsFoot,
    }

    /// <summary>Step 104: why the rider would NOT reel (in the order asked; <see cref="None"/> = asked and rolled).</summary>
    public enum TimedRiderRefusal : byte
    {
        None,

        /// <summary>The shadow's rules off (the master switch, the impact system, timed blocks).</summary>
        Off,

        /// <summary>Score 0 - an untimed block: today's Rider refusal stands exactly (no die).</summary>
        NotTimed,

        /// <summary>No riposte row (its ceiling 0 / the riposte family off).</summary>
        NoRow,

        /// <summary>A couched lance (the passive attack) - never.</summary>
        Lance,

        /// <summary>A man on foot blocked and <c>TimedRiderFromFoot</c> is off.</summary>
        FromFootOff,

        /// <summary>The rider is you and <c>TimedRiderReelOnYou</c> is off.</summary>
        OnYouOff,

        /// <summary>He is down (the decision's own refusal).</summary>
        Down,

        /// <summary>Another outcome of the same block replaced it (a stagger / knockdown of the blocker, a throw-back).</summary>
        Replaced,

        /// <summary>He still reels - never restart his upper body.</summary>
        Reeling,

        /// <summary>His stagger immunity after his last reel (a perfect block skips it with <c>TimedBlockPerfectSkipsImmunity</c>).</summary>
        Immune,
    }

    /// <summary>Step 104a: the inputs of one shadow decision (the module fills them from the impact decision and both men).</summary>
    public struct TimedRiderFacts
    {
        public ImpactEvent Event;
        public bool BlockerMounted, AttackerMounted, BlockerIsPlayer, AttackerIsPlayer, AttackerPassive;

        /// <summary>The block's timing score (decision 91's - the skill window, the AI's share, a parry's share).</summary>
        public double Score;

        /// <summary>The riposte row: in the row, its chance (the timed blend + bonus, the halving and the you-share already applied - the
        /// decision's own <see cref="ImpactRoll.Chance"/>; a perfect block's halving already skipped) and its refusal (Rider for a rider).</summary>
        public bool RowInRow;
        public double RowChance;
        public ImpactRefusal RowRefusal;

        /// <summary>Step 56's untimed length for this rider (s; <see cref="ImpactMath.RiposteStun56"/>) and the stun cap.</summary>
        public double Length56, StunCap;

        /// <summary>The riposte's pressure on him (NaN = 0) - the knock-off's match term.</summary>
        public double XRiposte;

        /// <summary>His horse's speed into the blow (m/s) - the knock-off's band.</summary>
        public double HorseSpeed;

        /// <summary>His state: still reeling, stagger-immune.</summary>
        public bool AttackerReeling, AttackerImmune;

        /// <summary>The multiplier his tiredness puts on his horse now (1 = none; NaN = unknown).</summary>
        public double TiredMountMultiplier;

        /// <summary>The foot attacker's riposte happened today (<see cref="TimedRiderCombo.RiderVsFoot"/> only).</summary>
        public bool FootRiposteHappened;
    }

    /// <summary>Step 104a: what 104b-104d WOULD do with one correct block of a rider's blow (NaN / false = not reached).</summary>
    public struct TimedRiderShadow
    {
        public TimedRiderCombo Combo;
        public bool Asked;
        public TimedRiderRefusal Refusal;
        public double Score;

        public double ReelChance, ReelDie;
        public bool Reel;

        /// <summary>The reel's length (s), its animation part and the lock alone after it; held at the you-cap.</summary>
        public double ReelSeconds, AnimSeconds, LockSeconds;
        public bool CappedForYou;

        /// <summary>His horse's top speed multiplier during the reel.</summary>
        public double HorseCheck;

        public double SpeedBand, MatchFactor, DismountChance, DismountDie;
        public bool Dismount;

        public double RearChance, RearDie;
        public bool Rear;

        /// <summary>Your perfect strike window on him (s; NaN = none) and the slow motion.</summary>
        public double StrikeWindow;
        public bool SlowMo;

        /// <summary><see cref="TimedRiderCombo.RiderVsFoot"/>: 104d's mounted push-out and your strike from horseback.</summary>
        public bool PushOutMounted, StrikeFromHorseback;
    }

    /// <summary>
    /// Step 104 - THE RIDER LADDER (AI_NOTES "Step 104" §2, DESIGN decision 93): the reel in the saddle (step 101's timed chance - s 0 = today's
    /// refusal exactly, no die), its length (step 56's blended to <c>TimedRiderReelMaxSeconds</c> by s; on you at most
    /// <c>TimedRiderReelAgainstYouMaxSeconds</c>), the horse checked; once the reel is taken a PERFECT block rolls the knock-off first, then a
    /// sharp one (s ≥ <c>TimedRiderRearMinScore</c>) the rear - one die each. Never a rear or a knock-off on YOU (the AI's half-strength timing).
    /// Pure.
    /// </summary>
    public static class TimedRiderMath
    {
        /// <summary>The knock-off's speed band: a standing horse counts this much, a gallop all of it.</summary>
        public const double StandingBand = 0.25;

        /// <summary>The knock-off's match term: min(1, this + S(x_r)) - half for even fresh men, all of it against a weak or tired rider.</summary>
        public const double MatchBase = 0.5;

        public static TimedRiderCombo ComboOf(bool blockerMounted, bool attackerMounted)
            => attackerMounted ? (blockerMounted ? TimedRiderCombo.RiderVsRider : TimedRiderCombo.FootVsRider)
               : blockerMounted ? TimedRiderCombo.RiderVsFoot : TimedRiderCombo.None;

        /// <summary>The reel's length: step 56's <paramref name="l56"/> blended to the rider's max by s (the stun cap; on you the you-cap) -
        /// <see cref="TimedBlockMath.Length"/> with the rider's numbers.</summary>
        public static double ReelLength(in TimedRiderRules r, double l56, double s, double stunCap, bool toYou, out bool cappedForYou)
            => TimedBlockMath.Length(l56, s, r.ReelMax, stunCap, toYou, r.ReelYouMax, out cappedForYou);

        /// <summary>The animation's part of a reel of <paramref name="reelSeconds"/>: min(the reel, <c>TimedRiderReelAnimMaxSeconds</c>, the
        /// clip's own length at its speed); 0 for "none" / no action. <paramref name="naturalSeconds"/> NaN = unknown (not limited by it).</summary>
        public static double AnimSeconds(in TimedRiderRules r, double reelSeconds, double naturalSeconds)
        {
            if (!(reelSeconds > 0) || IsNone(r.ReelAction)) return 0;
            double a = Math.Min(reelSeconds, r.AnimMax);
            if (naturalSeconds > 0 && !double.IsInfinity(naturalSeconds)) a = Math.Min(a, naturalSeconds / Math.Max(0.05, r.AnimSpeed));
            return Math.Max(0, a);
        }

        /// <summary>"none" or empty = no animation (the lock only).</summary>
        public static bool IsNone(string? action) => string.IsNullOrEmpty(action) || string.Equals(action, "none", StringComparison.OrdinalIgnoreCase);

        /// <summary>His horse's multiplier while he reels: min(his tired multiplier, the check share).</summary>
        public static double HorseCheck(double tiredMultiplier, double share)
        {
            double t = double.IsNaN(tiredMultiplier) || tiredMultiplier <= 0 ? 1 : Math.Min(1, tiredMultiplier);
            return Math.Min(t, Math.Max(0, Math.Min(1, share)));
        }

        /// <summary>The rear's chance: share x s from the min score (0 below it).</summary>
        public static double RearChance(double s, double minScore, double share)
        {
            if (!(s > 0) || s + 1e-9 < minScore) return 0;
            return Math.Max(0, Math.Min(1, share * Math.Min(1, s)));
        }

        /// <summary>The knock-off's speed band: <see cref="StandingBand"/> at or below the walk reference, 1 at the gallop, a line between.</summary>
        public static double SpeedBand(double speed, double walk, double gallop)
        {
            if (double.IsNaN(speed)) speed = 0;
            double t = gallop > walk ? (speed - walk) / (gallop - walk) : speed >= gallop ? 1 : 0;
            t = Math.Max(0, Math.Min(1, t));
            return StandingBand + (1 - StandingBand) * t;
        }

        /// <summary>The knock-off's match term: min(1, 0.5 + S(x_r)) (x_r NaN = 0).</summary>
        public static double MatchFactor(double xr) => Math.Min(1, MatchBase + ImpactMath.S(double.IsNaN(xr) ? 0 : xr));

        /// <summary>The knock-off's chance: a PERFECT block only - share x (a shield 1 / a parry the weapon share) x the speed band x the match.</summary>
        public static double DismountChance(in TimedRiderRules r, double s, bool shieldBlock, double speedBand, double match)
        {
            if (!TimedBlockMath.IsPerfect(s)) return 0;
            double c = r.DismountShare * (shieldBlock ? 1 : r.DismountWeaponShare) * speedBand * match;
            return Math.Max(0, Math.Min(1, c));
        }

        /// <summary>The gates before any die (in order): the rules, a rider's blow, a timed block, the row, the lance, the switches, his
        /// state.</summary>
        public static TimedRiderRefusal Gate(in TimedRiderRules r, in TimedRiderFacts f)
        {
            if (!r.ShadowOn) return TimedRiderRefusal.Off;
            if (!(f.Score > 0)) return TimedRiderRefusal.NotTimed;
            if (!f.RowInRow) return TimedRiderRefusal.NoRow;
            if (f.AttackerPassive) return TimedRiderRefusal.Lance;
            if (!f.BlockerMounted && !r.FromFoot) return TimedRiderRefusal.FromFootOff;
            if (f.AttackerIsPlayer && !r.ReelOnYou) return TimedRiderRefusal.OnYouOff;
            if (f.RowRefusal == ImpactRefusal.Down) return TimedRiderRefusal.Down;
            if (f.RowRefusal == ImpactRefusal.Replaced) return TimedRiderRefusal.Replaced;
            if (f.AttackerReeling) return TimedRiderRefusal.Reeling;
            if (f.AttackerImmune && !(TimedBlockMath.IsPerfect(f.Score) && r.PerfectSkipsImmunity)) return TimedRiderRefusal.Immune;
            return TimedRiderRefusal.None;
        }

        /// <summary>
        /// THE SHADOW DECISION (104a): what 104b-104d would do. The reel - one die at the row's chance; taken = its length, the animation's part,
        /// the horse check; then the knock-off (perfect, never on you) and, if he stayed on, the rear (sharp, never on you) - one die each; your
        /// perfect strike window and the slow motion. <see cref="TimedRiderCombo.RiderVsFoot"/> only notes 104d's additions (no die).
        /// </summary>
        public static TimedRiderShadow Decide(in TimedRiderRules r, in TimedRiderFacts f, double naturalAnimSeconds, IRandomSource dice)
        {
            var o = new TimedRiderShadow
            {
                Combo = ComboOf(f.BlockerMounted, f.AttackerMounted),
                Score = f.Score,
                ReelChance = double.NaN,
                ReelDie = double.NaN,
                ReelSeconds = double.NaN,
                AnimSeconds = double.NaN,
                LockSeconds = double.NaN,
                HorseCheck = double.NaN,
                SpeedBand = double.NaN,
                MatchFactor = double.NaN,
                DismountChance = double.NaN,
                DismountDie = double.NaN,
                RearChance = double.NaN,
                RearDie = double.NaN,
                StrikeWindow = double.NaN,
            };
            if (o.Combo == TimedRiderCombo.None || !ImpactMath.CanRiposte(f.Event)) return o;
            o.Asked = true;
            if (o.Combo == TimedRiderCombo.RiderVsFoot)
            {
                if (!r.ShadowOn)
                {
                    o.Refusal = TimedRiderRefusal.Off;
                    return o;
                }
                bool timed = f.Score > 0;
                o.Refusal = timed ? TimedRiderRefusal.None : TimedRiderRefusal.NotTimed;
                o.PushOutMounted = timed && f.FootRiposteHappened && r.PushOutMounted;
                o.StrikeFromHorseback = timed && f.FootRiposteHappened && f.BlockerIsPlayer && r.PerfectStrikeOn && r.StrikeOnHorseback;
                return o;
            }
            o.Refusal = Gate(in r, in f);
            if (o.Refusal != TimedRiderRefusal.None) return o;
            o.ReelChance = Math.Max(0, Math.Min(1, f.RowChance));
            if (!(o.ReelChance > 0)) return o;
            o.ReelDie = dice.NextDouble();
            o.Reel = o.ReelDie < o.ReelChance;
            if (!o.Reel) return o;
            o.ReelSeconds = ReelLength(in r, f.Length56, f.Score, f.StunCap, f.AttackerIsPlayer, out o.CappedForYou);
            o.AnimSeconds = AnimSeconds(in r, o.ReelSeconds, naturalAnimSeconds);
            o.LockSeconds = Math.Max(0, o.ReelSeconds - o.AnimSeconds);
            o.HorseCheck = HorseCheck(f.TiredMountMultiplier, r.HorseCheck);
            if (!f.AttackerIsPlayer) // the AI's half-strength timing never rears your horse or unhorses you
            {
                o.SpeedBand = SpeedBand(f.HorseSpeed, r.RiderWalk, r.RiderGallop);
                o.MatchFactor = MatchFactor(f.XRiposte);
                o.DismountChance = DismountChance(in r, f.Score, f.Event == ImpactEvent.RightBlock, o.SpeedBand, o.MatchFactor);
                if (o.DismountChance > 0)
                {
                    o.DismountDie = dice.NextDouble();
                    o.Dismount = o.DismountDie < o.DismountChance;
                }
                if (!o.Dismount)
                {
                    o.RearChance = RearChance(f.Score, r.RearMinScore, r.RearShare);
                    if (o.RearChance > 0)
                    {
                        o.RearDie = dice.NextDouble();
                        o.Rear = o.RearDie < o.RearChance;
                    }
                }
            }
            if (f.BlockerIsPlayer && r.PerfectStrikeOn && r.StrikeOnHorseback) o.StrikeWindow = o.ReelSeconds + r.StrikeGrace;
            o.SlowMo = f.BlockerIsPlayer && r.SlowMoOn && f.Score + 1e-9 >= r.SlowMoMinScore;
            return o;
        }

        public static string ComboName(TimedRiderCombo c) => c switch
        {
            TimedRiderCombo.RiderVsRider => "rider vs rider",
            TimedRiderCombo.FootVsRider => "foot vs rider",
            TimedRiderCombo.RiderVsFoot => "rider vs foot",
            _ => "none",
        };

        public static string RefusalName(TimedRiderRefusal x) => x switch
        {
            TimedRiderRefusal.None => "none",
            TimedRiderRefusal.Off => "off",
            TimedRiderRefusal.NotTimed => "untimed (today's refusal)",
            TimedRiderRefusal.NoRow => "no riposte row",
            TimedRiderRefusal.Lance => "a couched lance",
            TimedRiderRefusal.FromFootOff => "from foot off",
            TimedRiderRefusal.OnYouOff => "on you off",
            TimedRiderRefusal.Down => "he is down",
            TimedRiderRefusal.Replaced => "replaced by another outcome",
            TimedRiderRefusal.Reeling => "already reeling",
            TimedRiderRefusal.Immune => "stagger-immune",
            _ => "?",
        };

        /// <summary>The [impact] "rider shadow" line's body (after "rider shadow: ").</summary>
        public static string Describe(in TimedRiderShadow o, string attacker, string blocker) => Describe(in o, attacker, blocker, false);

        /// <summary>Step 104b: the same line; <paramref name="reelLive"/> = the reel it rolled is APPLIED ("REELS" - the rear / knock-off after it
        /// still shadow).</summary>
        public static string Describe(in TimedRiderShadow o, string attacker, string blocker, bool reelLive)
        {
            if (reelLive && o.Reel && o.Combo != TimedRiderCombo.RiderVsFoot)
                return DescribeShadow(in o, attacker, blocker).Replace("WOULD reel ", "REELS (live) ").Replace(" - nothing applied", " - the reel LIVE; the rear / knock-off shadow only (step 104c)");
            return DescribeShadow(in o, attacker, blocker);
        }

        private static string DescribeShadow(in TimedRiderShadow o, string attacker, string blocker)
        {
            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(160);
            sb.Append(ComboName(o.Combo)).Append(" - ").Append(blocker).Append(" blocked ").Append(attacker).Append(", timing ").Append(o.Score.ToString("0.00", c));
            if (o.Combo == TimedRiderCombo.RiderVsFoot)
            {
                sb.Append(": the foot attacker's riposte as today");
                if (o.PushOutMounted) sb.Append("; 104d WOULD push out (his heavy parry)");
                if (o.StrikeFromHorseback) sb.Append("; 104d WOULD let your strike from horseback count");
                if (o.Refusal == TimedRiderRefusal.NotTimed) sb.Append(" (untimed)");
                return sb.Append(" - nothing applied").ToString();
            }
            if (o.Refusal != TimedRiderRefusal.None) return sb.Append(": WOULD NOT reel - ").Append(RefusalName(o.Refusal)).Append(" - nothing applied").ToString();
            sb.Append(": reel ").Append((100 * o.ReelChance).ToString("0", c)).Append('%');
            if (double.IsNaN(o.ReelDie)) return sb.Append(" (not rolled) - nothing applied").ToString();
            sb.Append(" (die ").Append(o.ReelDie.ToString("0.00", c)).Append(") → ");
            if (!o.Reel) return sb.Append("no reel - nothing applied").ToString();
            sb.Append("WOULD reel ").Append(o.ReelSeconds.ToString("0.00", c)).Append(" s").Append(o.CappedForYou ? " (held at the you-cap)" : string.Empty)
              .Append(" (animation ").Append(o.AnimSeconds.ToString("0.00", c)).Append(" s + lock ").Append(o.LockSeconds.ToString("0.00", c)).Append(" s), horse x")
              .Append(o.HorseCheck.ToString("0.00", c));
            if (!double.IsNaN(o.DismountChance) && o.DismountChance > 0)
                sb.Append("; knocked off ").Append((100 * o.DismountChance).ToString("0.0", c)).Append("% (speed band ").Append(o.SpeedBand.ToString("0.00", c))
                  .Append(", match ").Append(o.MatchFactor.ToString("0.00", c)).Append(o.Dismount ? ") → YES" : ") → no");
            if (!double.IsNaN(o.RearChance) && o.RearChance > 0)
                sb.Append("; rear ").Append((100 * o.RearChance).ToString("0", c)).Append(o.Rear ? "% → YES" : "% → no");
            if (!double.IsNaN(o.StrikeWindow)) sb.Append("; your PERFECT STRIKE window ").Append(o.StrikeWindow.ToString("0.00", c)).Append(" s, any side");
            if (o.SlowMo) sb.Append("; slow motion");
            return sb.Append(" - nothing applied").ToString();
        }
    }

    /// <summary>Step 104a: the [summary] "timed blocks on horseback (shadow)" line - every correct block of / by a rider, what would happen.</summary>
    public sealed class TimedRiderStats
    {
        public readonly int[] ByCombo = new int[4];
        public readonly int[] Refused = new int[11];        // by TimedRiderRefusal
        public readonly int[] ReelsByBand = new int[TimedBlockMath.Bands];
        public readonly int[] AskedByBand = new int[TimedBlockMath.Bands];
        public int Yours, TheAis, OnYou;
        public int Rolled, Reels, CappedForYou, Rears, RearsAsked, Dismounts, DismountsAsked, StrikeWindows, SlowMos;
        public double ReelChanceSum, ReelSecondsSum, AnimSecondsSum, RearChanceSum, DismountChanceSum, StrikeWindowSum;
        public int FootRipostes, FootPushOuts, FootStrikes, FootUntimed;

        public void Note(in TimedRiderShadow o, bool blockerIsPlayer, bool attackerIsPlayer)
        {
            if (!o.Asked) return;
            ByCombo[(int)o.Combo]++;
            if (o.Combo == TimedRiderCombo.RiderVsFoot)
            {
                if (o.Refusal == TimedRiderRefusal.NotTimed) FootUntimed++;
                else FootRipostes++;
                if (o.PushOutMounted) FootPushOuts++;
                if (o.StrikeFromHorseback) FootStrikes++;
                return;
            }
            if (blockerIsPlayer) Yours++;
            else TheAis++;
            if (attackerIsPlayer) OnYou++;
            int band = TimedBlockMath.Band(o.Score);
            if (band >= 0 && band < AskedByBand.Length) AskedByBand[band]++;
            if (o.Refusal != TimedRiderRefusal.None)
            {
                int i = (int)o.Refusal;
                if (i >= 0 && i < Refused.Length) Refused[i]++;
                return;
            }
            if (double.IsNaN(o.ReelDie)) return;
            Rolled++;
            ReelChanceSum += o.ReelChance;
            if (!o.Reel) return;
            Reels++;
            if (band >= 0 && band < ReelsByBand.Length) ReelsByBand[band]++;
            ReelSecondsSum += o.ReelSeconds;
            AnimSecondsSum += o.AnimSeconds;
            if (o.CappedForYou) CappedForYou++;
            if (!double.IsNaN(o.DismountDie))
            {
                DismountsAsked++;
                DismountChanceSum += o.DismountChance;
                if (o.Dismount) Dismounts++;
            }
            if (!double.IsNaN(o.RearDie))
            {
                RearsAsked++;
                RearChanceSum += o.RearChance;
                if (o.Rear) Rears++;
            }
            if (!double.IsNaN(o.StrikeWindow))
            {
                StrikeWindows++;
                StrikeWindowSum += o.StrikeWindow;
            }
            if (o.SlowMo) SlowMos++;
        }

        public string SummaryLine(in TimedRiderRules r)
        {
            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(400);
            sb.Append("timed blocks on horseback (shadow, step 104a): ");
            if (!r.ShadowOn) sb.Append("shadow OFF (").Append(r.OffBecause).Append(") - ");
            else sb.Append(r.Switch ? "the switch ON - the reel LIVE (its own line, step 104b), the rest below NOT APPLIED; " : "the switch OFF - NOTHING APPLIED; ");
            int riders = ByCombo[(int)TimedRiderCombo.RiderVsRider] + ByCombo[(int)TimedRiderCombo.FootVsRider];
            sb.Append("correct blocks of a rider's blow ").Append(riders.ToString(c)).Append(" (rider vs rider ").Append(ByCombo[(int)TimedRiderCombo.RiderVsRider].ToString(c))
              .Append(" / foot vs rider ").Append(ByCombo[(int)TimedRiderCombo.FootVsRider].ToString(c)).Append("; yours ").Append(Yours.ToString(c))
              .Append(", the AI's ").Append(TheAis.ToString(c)).Append(", on you ").Append(OnYou.ToString(c)).Append(") by timing band [none / <0.25 / <0.5 / <0.8 / 0.8+] ");
            Bands(sb, AskedByBand, c);
            sb.Append("; WOULD reel ").Append(Reels.ToString(c)).Append(" of ").Append(Rolled.ToString(c)).Append(" rolled (expected ").Append(ReelChanceSum.ToString("0.0", c)).Append(") by band ");
            Bands(sb, ReelsByBand, c);
            sb.Append(", mean ").Append(Reels > 0 ? (ReelSecondsSum / Reels).ToString("0.00", c) : "-").Append(" s (animation ")
              .Append(Reels > 0 ? (AnimSecondsSum / Reels).ToString("0.00", c) : "-").Append(" s), held at the you-cap ").Append(CappedForYou.ToString(c))
              .Append(", horses checked to ").Append((100 * r.HorseCheck).ToString("0", c)).Append("% ").Append(Reels.ToString(c))
              .Append("; knocked off ").Append(Dismounts.ToString(c)).Append(" of ").Append(DismountsAsked.ToString(c)).Append(" perfect (mean ")
              .Append(DismountsAsked > 0 ? (100 * DismountChanceSum / DismountsAsked).ToString("0.0", c) + "%" : "-").Append(")")
              .Append("; rears ").Append(Rears.ToString(c)).Append(" of ").Append(RearsAsked.ToString(c)).Append(" sharp (mean ")
              .Append(RearsAsked > 0 ? (100 * RearChanceSum / RearsAsked).ToString("0", c) + "%" : "-").Append(")")
              .Append("; your perfect strike windows ").Append(StrikeWindows.ToString(c)).Append(" (mean ").Append(StrikeWindows > 0 ? (StrikeWindowSum / StrikeWindows).ToString("0.00", c) : "-")
              .Append(" s, any side), slow motions ").Append(SlowMos.ToString(c)).Append("; would not reel: ");
            bool any = false;
            for (int i = 1; i < Refused.Length; i++)
            {
                if (Refused[i] == 0) continue;
                if (any) sb.Append(", ");
                sb.Append(TimedRiderMath.RefusalName((TimedRiderRefusal)i)).Append(' ').Append(Refused[i].ToString(c));
                any = true;
            }
            if (!any) sb.Append("none");
            sb.Append("; a rider blocking a man on foot ").Append(ByCombo[(int)TimedRiderCombo.RiderVsFoot].ToString(c)).Append(" (timed ").Append(FootRipostes.ToString(c))
              .Append(", untimed ").Append(FootUntimed.ToString(c)).Append("; 104d would add the mounted push-out ").Append(FootPushOuts.ToString(c))
              .Append(", your strike from horseback ").Append(FootStrikes.ToString(c)).Append(')');
            return sb.ToString();
        }

        private static void Bands(StringBuilder sb, int[] bands, CultureInfo c)
        {
            sb.Append('[');
            for (int i = 0; i < bands.Length; i++)
            {
                if (i > 0) sb.Append(" / ");
                sb.Append(bands[i].ToString(c));
            }
            sb.Append(']');
        }
    }

    /// <summary>Step 104b: why a live reel in the saddle ended - every lock it set (his input, your gate, his horse) given back.</summary>
    public enum RiderReelEnd : byte
    {
        /// <summary>Its length ran out (the normal end).</summary>
        Ended,

        /// <summary>He died or left the field (no engine call on him).</summary>
        Removed,

        /// <summary>He is no longer on a horse (knocked off, his horse killed).</summary>
        Dismounted,

        /// <summary>A switch went off mid-reel (the master switch first, the impact system, timed blocks, <c>TimedBlocksOnHorseback</c>,
        /// the riposte family not live).</summary>
        SwitchedOff,

        /// <summary>The mission ended (before the summary) or the behaviour was removed.</summary>
        MissionEnd,

        /// <summary>An error - every reel let go at once, the reel off for the battle.</summary>
        Error,
    }

    /// <summary>
    /// Step 104b - THE REEL IN THE SADDLE, LIVE (DESIGN decision 93, AI_NOTES "104b - built"): the [summary] "timed blocks on horseback (step
    /// 104b)" line - reels decided by score band, taken / not started, the animation, the lock (the AI's frames edited, your presses
    /// swallowed), lengths asked → measured, every lock released by reason (LEFT OVER AT THE MISSION END must read 0), the horse checked and
    /// its speed (at the reel's start → 0.5 s later → its end - the engine's answer to the MountSpeed lever). Main thread.
    /// </summary>
    public sealed class TimedRiderReelStats
    {
        public const int EndCount = 6;

        public readonly int[] DecidedByBand = new int[TimedBlockMath.Bands];
        public readonly int[] EndedBy = new int[EndCount];
        public int Decided, Yours, OnYou, PoolFull, Started, NotStarted;
        public int AnimTaken, AnimRefused, AnimMissing, AnimNone, AnimCleared;
        public int LockAi, LockYou, LockNone;
        public long AiFrames, AiAttacksOut, AiGuardsOut, AiReadiesCancelled;
        public long YourFrames, YourAttacksSwallowed, YourGuardsSwallowed, YourReadiesCancelled;
        public int Measured;
        public double AskedSum, MeasuredSum;
        public int HorsesChecked;
        public double HorseCheckSum;
        public int SpeedStartN, SpeedHalfN, SpeedEndN;
        public double SpeedStartSum, SpeedHalfSum, SpeedEndSum;
        public int SlowMoAsked;

        /// <summary>Reels still holding a lock when the summary is written (the mission end released them first - must be 0).</summary>
        public int LeftOver;

        public void NoteDecided(double score, bool blockerIsPlayer, bool attackerIsPlayer)
        {
            Decided++;
            int b = TimedBlockMath.Band(score);
            if (b >= 0 && b < DecidedByBand.Length) DecidedByBand[b]++;
            if (blockerIsPlayer) Yours++;
            if (attackerIsPlayer) OnYou++;
        }

        public void NoteEnded(RiderReelEnd why, bool started, double asked, double measured)
        {
            int i = (int)why;
            if (i >= 0 && i < EndedBy.Length) EndedBy[i]++;
            if (!started)
            {
                NotStarted++;
                return;
            }
            // asked → measured: the reels that ran their whole length (an early end is counted by its reason only)
            if (why != RiderReelEnd.Ended || double.IsNaN(measured) || double.IsNaN(asked) || measured < 0) return;
            Measured++;
            AskedSum += asked;
            MeasuredSum += measured;
        }

        public void NoteSpeed(int when, double speed)
        {
            if (double.IsNaN(speed) || double.IsInfinity(speed)) return;
            if (when == 0)
            {
                SpeedStartN++;
                SpeedStartSum += speed;
            }
            else if (when == 1)
            {
                SpeedHalfN++;
                SpeedHalfSum += speed;
            }
            else
            {
                SpeedEndN++;
                SpeedEndSum += speed;
            }
        }

        /// <summary>One frame of the lock: the AI's (<paramref name="you"/> false) or yours - counted by what it took out.</summary>
        public void NoteFrame(bool you, InputEdit edit)
        {
            if (you) YourFrames++;
            else AiFrames++;
            if ((edit & InputEdit.AttackCleared) != 0)
            {
                if (you) YourAttacksSwallowed++;
                else AiAttacksOut++;
            }
            if ((edit & InputEdit.GuardCleared) != 0)
            {
                if (you) YourGuardsSwallowed++;
                else AiGuardsOut++;
            }
            if ((edit & InputEdit.ReadyCancelled) != 0)
            {
                if (you) YourReadiesCancelled++;
                else AiReadiesCancelled++;
            }
        }

        public static string EndName(RiderReelEnd e) => e switch
        {
            RiderReelEnd.Ended => "at its end",
            RiderReelEnd.Removed => "death / removal",
            RiderReelEnd.Dismounted => "dismounted",
            RiderReelEnd.SwitchedOff => "a switch off",
            RiderReelEnd.MissionEnd => "the mission end",
            RiderReelEnd.Error => "an error",
            _ => "?",
        };

        public string SummaryLine(in TimedRiderRules r, bool riposteLive)
        {
            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(500);
            sb.Append("timed blocks on horseback (step 104b): ");
            if (!r.ShadowOn) sb.Append("OFF (").Append(r.OffBecause).Append(") - ");
            else if (!r.Switch) sb.Append("OFF (TimedBlocksOnHorseback) - ");
            else if (!riposteLive) sb.Append("OFF (the riposte not live - shadow mode / the family off) - ");
            else sb.Append("the reel LIVE - ");
            sb.Append("reels decided ").Append(Decided.ToString(c)).Append(" by timing band [none / <0.25 / <0.5 / <0.8 / 0.8+] [");
            for (int i = 0; i < DecidedByBand.Length; i++)
            {
                if (i > 0) sb.Append(" / ");
                sb.Append(DecidedByBand[i].ToString(c));
            }
            sb.Append("] (your blocks ").Append(Yours.ToString(c)).Append(", on you ").Append(OnYou.ToString(c)).Append(", pool full ").Append(PoolFull.ToString(c))
              .Append("); started ").Append(Started.ToString(c)).Append(", gone before the tick ").Append(NotStarted.ToString(c))
              .Append("; in the saddle (whole reels) asked mean ").Append(Measured > 0 ? (AskedSum / Measured).ToString("0.00", c) : "-").Append(" s → measured mean ")
              .Append(Measured > 0 ? (MeasuredSum / Measured).ToString("0.00", c) : "-").Append(" s (").Append(Measured.ToString(c)).Append(")")
              .Append("; the animation ").Append(TimedRiderMath.IsNone(r.ReelAction) ? "none" : r.ReelAction).Append(" x").Append(r.AnimSpeed.ToString("0.00", c))
              .Append(" on channel 1: taken ").Append(AnimTaken.ToString(c)).Append(", refused ").Append(AnimRefused.ToString(c)).Append(", not in his set ")
              .Append(AnimMissing.ToString(c)).Append(", none ").Append(AnimNone.ToString(c)).Append(", cut at its part ").Append(AnimCleared.ToString(c))
              .Append("; the lock (no attack").Append(r.NoGuard ? ", no guard" : string.Empty).Append(", steering free): the AI ").Append(LockAi.ToString(c))
              .Append(" (frames ").Append(AiFrames.ToString(c)).Append(" - attacks out ").Append(AiAttacksOut.ToString(c)).Append(", guards out ").Append(AiGuardsOut.ToString(c))
              .Append(", readies cancelled ").Append(AiReadiesCancelled.ToString(c)).Append("), you ").Append(LockYou.ToString(c)).Append(" (presses swallowed - attacks ")
              .Append(YourAttacksSwallowed.ToString(c)).Append(", guards ").Append(YourGuardsSwallowed.ToString(c)).Append("), not locked ").Append(LockNone.ToString(c))
              .Append("; locks released: ");
            for (int i = 0; i < EndedBy.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(EndName((RiderReelEnd)i)).Append(' ').Append(EndedBy[i].ToString(c));
            }
            sb.Append(" - left over at the mission end ").Append(LeftOver.ToString(c))
              .Append("; horses checked ").Append(HorsesChecked.ToString(c)).Append(" (mean x").Append(HorsesChecked > 0 ? (HorseCheckSum / HorsesChecked).ToString("0.00", c) : "-")
              .Append("; his speed at the start ").Append(Mean(SpeedStartSum, SpeedStartN, c)).Append(" → 0.5 s ").Append(Mean(SpeedHalfSum, SpeedHalfN, c))
              .Append(" → the end ").Append(Mean(SpeedEndSum, SpeedEndN, c)).Append(" m/s)")
              .Append("; slow motions asked ").Append(SlowMoAsked.ToString(c));
            return sb.ToString();
        }

        private static string Mean(double sum, int n, CultureInfo c) => n > 0 ? (sum / n).ToString("0.0", c) : "-";
    }

    /// <summary>Step 104b: the on-screen words of a reel in the saddle.</summary>
    public static class TimedRiderLines
    {
        /// <summary>YOUR timed block reeled a rider: "Your block - X reels in the saddle 1.45 s / 1.50 (guard 0.20 s / 0.15)"; null with no
        /// timing read (the plain line then).</summary>
        public static string? YourReel(string? victim, ImpactEvent ev, double seconds, double maxSeconds, TimingSource source, double age, double perfect)
        {
            string? guard = TimedBlockMath.GuardText(source, age, perfect);
            if (guard == null) return null;
            var c = CultureInfo.InvariantCulture;
            string v = string.IsNullOrEmpty(victim) ? "someone" : victim!;
            return "Your " + (ev == ImpactEvent.Parry ? "parry" : "block") + " - " + v + " reels in the saddle " + (double.IsNaN(seconds) ? "-" : seconds.ToString("0.00", c))
                   + " s / " + Math.Max(0, maxSeconds).ToString("0.00", c) + " (" + guard + ")";
        }
    }

    /// <summary>Step 104a: one rider candidate of the J animation test tool.</summary>
    public enum RiderAnimKind : byte
    {
        /// <summary>The reel as set (<c>TimedRiderReelAction</c> at <c>TimedRiderReelAnimSpeed</c>), channel 1.</summary>
        Configured,

        /// <summary>A named action on his channel 1.</summary>
        Action,

        /// <summary>His weapon's own MOUNTED blocked recoil for a direction, channel 1.</summary>
        Recoil,

        /// <summary>A mounted blocker's push-out as his guard's own heavy parry, channel 1.</summary>
        PushParryHeavy,

        /// <summary>A mounted blocker's push-out with a named bash, channel 1 (never a default - for the eyes only).</summary>
        PushAction,

        /// <summary>The engine's own rear: our 1-damage blow flagged MakesRear on his HORSE (no action of ours on the horse).</summary>
        Rear,
    }

    public readonly struct RiderAnimCandidate
    {
        public RiderAnimCandidate(RiderAnimKind kind, string action, float speed, int direction, string label)
        {
            Kind = kind;
            Action = action;
            Speed = speed;
            Direction = direction;
            Label = label;
        }

        public RiderAnimKind Kind { get; }
        public string Action { get; }
        public float Speed { get; }
        public int Direction { get; }
        public string Label { get; }
    }

    /// <summary>
    /// Step 104a - THE RIDER CANDIDATES of the J test tool and the MOUNTED blocked recoils (item_usage_sets.xml v1.4.8, the <c>is_mounted="True"</c>
    /// usages' <c>blocked_action</c> / <c>quick_blocked_action</c>). Every one plays on the RIDER's channel 1 (the upper body) - never channel 0 on a
    /// rider, never an action on the horse (RBM's crash: a riderless horse given a stagger on channel 0); the rear is the engine's own, through a blow.
    /// </summary>
    public static class TimedRiderAnimMath
    {
        /// <summary>The only channel any action of ours touches on a rider.</summary>
        public const int RiderChannel = 1;

        public static readonly RiderAnimCandidate[] Candidates = BuildCandidates();

        /// <summary>The mounted recoil's base name (after "act_[quick_]blocked_") for his class and the direction; null = none. A wide-grip
        /// two-handed axe recoils like a staff on horseback; a dagger like a one-hander.</summary>
        public static string? MountedCore(RecoilClass cls, int direction, string? usageId)
        {
            if (cls == RecoilClass.TwoHanded && usageId != null && usageId.StartsWith("twohanded_widegrip", StringComparison.Ordinal)) cls = RecoilClass.Staff;
            switch (cls)
            {
                case RecoilClass.OneHanded:
                case RecoilClass.Dagger:
                    return direction switch { TimedRecoilMath.Up => "overswing_horseback", TimedRecoilMath.Down => "thrust_1h_horseback", TimedRecoilMath.Left => "slash_horseback_left", TimedRecoilMath.Right => "slash_horseback_right", _ => null };
                case RecoilClass.TwoHanded:
                    return direction switch { TimedRecoilMath.Up => "overswing_2h_horseback", TimedRecoilMath.Down => "thrust_2h_horseback", TimedRecoilMath.Left => "slash_2h_horseback_left", TimedRecoilMath.Right => "slash_2h_horseback_right", _ => null };
                case RecoilClass.Staff:
                    return direction switch { TimedRecoilMath.Up => "overswing_staff_horseback", TimedRecoilMath.Down => "thrust_staff_horseback", TimedRecoilMath.Left => "slashleft_staff_horseback", TimedRecoilMath.Right => "slashright_staff_horseback", _ => null };
                case RecoilClass.Spear:
                    return direction switch { TimedRecoilMath.Up => "overswing_spear_horseback", TimedRecoilMath.Down => "thrust_staff_horseback", _ => null };
                case RecoilClass.Lance:
                case RecoilClass.Flail:
                case RecoilClass.Fist:
                case RecoilClass.FistShield:
                    return TimedRecoilMath.Core(cls, direction); // the same names mounted and on foot
                default:
                    return null;
            }
        }

        /// <summary>The names to try, best first: the blocked recoil, then the quick one; empty = none for his weapon / that direction.</summary>
        public static string[] MountedRecoilNames(RecoilClass cls, int direction, string? usageId)
        {
            string? core = MountedCore(cls, direction, usageId);
            if (core == null) return new string[0];
            return new[] { "act_blocked_" + core, "act_quick_blocked_" + core };
        }

        private static RiderAnimCandidate[] BuildCandidates()
        {
            var list = new System.Collections.Generic.List<RiderAnimCandidate>(16)
            {
                new RiderAnimCandidate(RiderAnimKind.Configured, string.Empty, float.NaN, -1, "the reel as set (TimedRiderReelAction)"),
                new RiderAnimCandidate(RiderAnimKind.Action, "act_stagger_backward", 1f, -1, "act_stagger_backward x1.0 (RBM's mounted stagger)"),
                new RiderAnimCandidate(RiderAnimKind.Action, "act_stagger_backward", 0.5f, -1, "act_stagger_backward x0.5 (slowed)"),
                new RiderAnimCandidate(RiderAnimKind.Action, "act_strike2_chest_front", 1f, -1, "act_strike2_chest_front x1.0 (a hit flinch)"),
                new RiderAnimCandidate(RiderAnimKind.Action, "act_strike_chest_front", 1f, -1, "act_strike_chest_front x1.0 (a light flinch)"),
            };
            for (int pass = 0; pass < 2; pass++)
                for (int d = 0; d < TimedRecoilMath.DirectionCount; d++)
                {
                    float speed = pass == 0 ? 1f : 0.5f;
                    list.Add(new RiderAnimCandidate(RiderAnimKind.Recoil, string.Empty, speed, d,
                        "his mounted blocked recoil, " + TimedRecoilMath.DirectionName(d) + (pass == 0 ? " x1.0" : " x0.5 (slowed)")));
                }
            list.Add(new RiderAnimCandidate(RiderAnimKind.PushParryHeavy, TimedPushOutMath.ParryHeavy, 1f, -1, "mounted push-out: his guard's heavy parry (104d's choice)"));
            list.Add(new RiderAnimCandidate(RiderAnimKind.PushAction, "act_shield_bash", 1f, -1, "mounted push-out: act_shield_bash (for the eyes only)"));
            list.Add(new RiderAnimCandidate(RiderAnimKind.Rear, string.Empty, 1f, -1, "the REAR: the game's own rear flag on his horse (EXPERIMENTAL)"));
            return list.ToArray();
        }
    }
}

using System;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 100g - THE PERFECT STRIKE (Toni 2026-11-08: "if I attack in the opposite way (say I blocked his right attack, coming from my left
    /// and then swing from my right to left) after I blocked him and he is staggered I do 2-3x damage against him and I see some notification
    /// like PERFECT STRIKE"; AI_NOTES "Step 100" - "100g - built", DESIGN decision 91 (j)): the settings, read live. <see cref="Enabled"/> = the
    /// master switch FIRST, then Athletics and the impact system (the timed riposte lives there), then <c>TimedBlocksEnabled</c>, then
    /// <c>PerfectStrikeEnabled</c>.
    /// </summary>
    public readonly struct PerfectStrikeRules
    {
        public PerfectStrikeRules(TraxSettings s)
        {
            ModEnabled = s.ModEnabled;
            ImpactOn = s.AthleticsEnabled && s.ImpactEnabled;
            TimedOn = s.TimedBlocksEnabled;
            Switch = s.PerfectStrikeEnabled;
            MinMultiplier = PerfectStrikeMath.ClampMultiplier(F(s.PerfectStrikeMinMultiplier));
            MaxMultiplier = PerfectStrikeMath.ClampMultiplier(F(s.PerfectStrikeMaxMultiplier));
            GraceSeconds = Math.Max(0, Math.Min(PerfectStrikeMath.MaxGraceSeconds, Nan0(F(s.PerfectStrikeGraceSeconds))));
            CentreMessage = s.PerfectStrikeCentreMessage;
            HorsebackSwitch = s.TimedBlocksOnHorseback;
            OnHorsebackSwitch = s.PerfectStrikeOnHorseback;
            HorsebackAnySide = s.PerfectStrikeHorsebackAnySide;
        }

        public static PerfectStrikeRules From(TraxSettings s) => new PerfectStrikeRules(s);

        public bool ModEnabled { get; }
        public bool ImpactOn { get; }
        public bool TimedOn { get; }
        public bool Switch { get; }
        public bool Enabled => ModEnabled && ImpactOn && TimedOn && Switch;

        /// <summary>The damage multiplier after a block timed at score 0 (1-5).</summary>
        public double MinMultiplier { get; }

        /// <summary>The damage multiplier after a perfectly timed block, score 1 (1-5).</summary>
        public double MaxMultiplier { get; }

        /// <summary>Battle seconds the window stays open after his stagger's end (a counter already on its way).</summary>
        public double GraceSeconds { get; }

        /// <summary>Also the big line in the middle of the screen (the game's quick information), not only the gold line in the feed.</summary>
        public bool CentreMessage { get; }

        /// <summary>Step 104d: <c>TimedBlocksOnHorseback</c> (the player page).</summary>
        public bool HorsebackSwitch { get; }

        /// <summary>Step 104d: <c>PerfectStrikeOnHorseback</c> (Advanced).</summary>
        public bool OnHorsebackSwitch { get; }

        /// <summary>Step 104d: <c>PerfectStrikeHorsebackAnySide</c> (Advanced) - on a reeling RIDER any landed hit on his body counts.</summary>
        public bool HorsebackAnySide { get; }

        /// <summary>
        /// Step 104d - THE PERFECT STRIKE ON HORSEBACK: a rider's reel in the saddle taken after YOUR timed block opens a window on him, and your
        /// strike may come from horseback (on a rider or a man on foot). The perfect strike's chain first (the master switch FIRST), then
        /// <c>TimedBlocksOnHorseback</c>, then <c>PerfectStrikeOnHorseback</c>. Off = no rider window, a strike from horseback never counts.
        /// </summary>
        public bool MountedOn => Enabled && HorsebackSwitch && OnHorsebackSwitch;

        public string? MountedOffBecause => !Enabled ? OffBecause : !HorsebackSwitch ? "TimedBlocksOnHorseback" : !OnHorsebackSwitch ? "PerfectStrikeOnHorseback" : null;

        public string? OffBecause => !ModEnabled ? "ModEnabled" : !ImpactOn ? "the impact system off" : !TimedOn ? "TimedBlocksEnabled" : !Switch ? "PerfectStrikeEnabled" : null;

        public string Describe()
        {
            var c = CultureInfo.InvariantCulture;
            if (!Enabled) return "perfect strike OFF (" + OffBecause + ")";
            return "perfect strike ON: after YOUR timed block staggers him, your first attack from the strict opposite (his right ↔ your right swing, left ↔ left, his overhead ↔ your thrust from below, his thrust ↔ your overhead) that lands on his body while he reels (+"
                   + GraceSeconds.ToString("0.00", c) + " s) does x" + MinMultiplier.ToString("0.0#", c) + " (timing 0) to x" + MaxMultiplier.ToString("0.0#", c)
                   + " (timing 1) damage; the line in the middle of the screen " + (CentreMessage ? "on" : "off")
                   + "; on horseback (step 104d) " + (MountedOn ? "ON - a rider reeling in the saddle after your timed block: " + (HorsebackAnySide ? "ANY side" : "strict opposites")
                                                                  + " on his body (never his horse); your strike may come from horseback (strict opposites on a man on foot)"
                                                                : "OFF (" + MountedOffBecause + ")");
        }

        private static double Nan0(double v) => double.IsNaN(v) ? 0 : v;

        /// <summary>A float setting as the decimal it was written as.</summary>
        private static double F(float v) => float.IsNaN(v) ? double.NaN : Math.Round((double)v, 5);
    }

    /// <summary>Step 100g: what a landed hit of yours on a man in an open window is.</summary>
    public enum StrikeVerdict : byte
    {
        /// <summary>From the opposite side of the attack you blocked - the perfect strike (the window is used up).</summary>
        Perfect,

        /// <summary>Not the strict opposite (the same side, a side swing after an overhead / thrust, ...) - counted, the window stays open.</summary>
        WrongSide,

        /// <summary>Your attack's direction unreadable - nothing (not counted as a wrong side).</summary>
        Unknown,

        /// <summary>Step 103: HIS blocked attack's direction was unknown - nothing counts (counted apart, the window stays open).</summary>
        BlockedUnknown,
    }

    /// <summary>A side as seen from one man's own body.</summary>
    public enum BodySide : byte
    {
        None,
        Left,
        Right,
    }

    /// <summary>
    /// Step 100g - the perfect strike's pure rules. THE DIRECTION MAPPING (verified in the v1.4.8 decompile - AI_NOTES "100g - built",
    /// "Step 103"): the engine's <c>Agent.UsageDirection</c> (<see cref="Up"/> 0 overhead, <see cref="Down"/> 1 the attack from below - a
    /// thrust / stab, <see cref="Left"/> 2, <see cref="Right"/> 3 - the same values as <see cref="TimedRecoilMath"/>) is the ATTACKER's own:
    /// AttackRight = wound up on HIS right, sweeping right → left. Proof: the game's training field (TrainingFieldMissionController) has the
    /// trainer attack with <c>MovementControlFlag.AttackRight</c> (0x80) and asks you to "Defend from left" (DefendLeft) - his right swing comes
    /// at the defender's LEFT, because two men facing each other have mirrored left / right.
    /// Toni's example: he blocked the enemy's RIGHT attack (AttackRight 3, coming at Toni's left), then swings from HIS right to his left -
    /// Toni's own AttackRight 3. So "the opposite side" in Toni's frame is the SAME engine value: a blocked AttackRight is answered by an
    /// AttackRight, a blocked AttackLeft by an AttackLeft.
    /// STEP 103 (Toni 2026-11-09, "top-down, down-top, not top/down-left/right"): STRICT OPPOSITES - a blocked OVERHEAD is answered ONLY by
    /// your attack from below (your AttackDown - the thrust / stab), a blocked THRUST (his attack from below) ONLY by your overhead; a side swing
    /// after either is a wrong side (step 100g let either side swing answer an overhead and any swing answer a thrust). The game pairs them the
    /// same way (TrainingFieldMissionController's opposite objective: AttackUp ↔ AttackDown, AttackLeft ↔ AttackRight). An UNKNOWN blocked
    /// direction: nothing counts (<see cref="StrikeVerdict.BlockedUnknown"/> - counted, the window kept). A weapon with no attack from below
    /// (an axe: its usage set has no <c>attack_down</c> on foot) never reports AttackDown, so it cannot answer a blocked overhead.
    /// </summary>
    public static class PerfectStrikeMath
    {
        public const int Up = TimedRecoilMath.Up;
        public const int Down = TimedRecoilMath.Down;
        public const int Left = TimedRecoilMath.Left;
        public const int Right = TimedRecoilMath.Right;

        /// <summary>The multipliers' range (the settings' too).</summary>
        public const double MinAllowedMultiplier = 1, MaxAllowedMultiplier = 5;

        public const double MaxGraceSeconds = 1;

        /// <summary>Open windows kept at once (yours only - a handful at most; the oldest replaced when full).</summary>
        public const int MaxWindows = 8;

        /// <summary>The gold of the on-screen line (#RRGGBBAA).</summary>
        public const string GoldHex = "#FFD700FF";

        public static bool IsAttack(int dir) => dir >= Up && dir <= Right;

        public static bool IsSideSwing(int dir) => dir == Left || dir == Right;

        /// <summary>The side of the ATTACKER's own body a side swing starts from (AttackRight = his right).</summary>
        public static BodySide FromOwnSide(int dir) => dir == Right ? BodySide.Right : dir == Left ? BodySide.Left : BodySide.None;

        /// <summary>The side of the DEFENDER's body a side swing comes at - mirrored (the training field: AttackRight → "Defend from left").</summary>
        public static BodySide ArrivesAtDefenderSide(int dir) => dir == Right ? BodySide.Left : dir == Left ? BodySide.Right : BodySide.None;

        /// <summary>Step 103: the engine value of YOUR answer to his blocked <paramref name="blocked"/> - the strict opposite: his overhead ↔
        /// your attack from below (thrust), his thrust ↔ your overhead, his side swing ↔ the SAME value (your swing from the other side of your
        /// body); -1 when his direction is unknown.</summary>
        public static int Opposite(int blocked) => blocked switch
        {
            Up => Down,
            Down => Up,
            Left => Left,
            Right => Right,
            _ => -1,
        };

        /// <summary>
        /// Your landed attack (<paramref name="strike"/>, YOUR own direction) against the attack you blocked (<paramref name="blocked"/>, HIS own
        /// direction; -1 unknown): perfect only for the strict opposite (<see cref="Opposite"/>). Your direction unreadable = Unknown; his
        /// unknown = BlockedUnknown (nothing counts).
        /// </summary>
        public static StrikeVerdict Verdict(int blocked, int strike)
        {
            if (!IsAttack(strike)) return StrikeVerdict.Unknown;
            if (!IsAttack(blocked)) return StrikeVerdict.BlockedUnknown;
            return strike == Opposite(blocked) ? StrikeVerdict.Perfect : StrikeVerdict.WrongSide;
        }

        /// <summary>
        /// Step 104d: the verdict for a window's kind. A window on a reeling RIDER (<paramref name="riderWindow"/>) with
        /// <c>PerfectStrikeHorsebackAnySide</c> (<paramref name="anySide"/>): ANY landed hit on his body is perfect, whatever its direction - a
        /// mounted swing is dictated by which side you pass him (Toni's answer 4). Otherwise (a man on foot, or any-side off) step 103's strict
        /// opposites, <see cref="Verdict(int, int)"/>.
        /// </summary>
        public static StrikeVerdict Verdict(bool riderWindow, bool anySide, int blocked, int strike) =>
            riderWindow && anySide ? StrikeVerdict.Perfect : Verdict(blocked, strike);

        public static double ClampMultiplier(double v) =>
            double.IsNaN(v) ? MinAllowedMultiplier : Math.Max(MinAllowedMultiplier, Math.Min(MaxAllowedMultiplier, v));

        /// <summary>The damage multiplier for the timing score of the block that caused the stagger: min at 0, a straight line to max at 1.</summary>
        public static double Multiplier(double score, double min, double max)
        {
            double s = double.IsNaN(score) ? 0 : Math.Max(0, Math.Min(1, score));
            return ClampMultiplier(min) + (ClampMultiplier(max) - ClampMultiplier(min)) * s;
        }

        public static double Multiplier(double score, in PerfectStrikeRules r) => Multiplier(score, r.MinMultiplier, r.MaxMultiplier);

        /// <summary>The landed damage x the multiplier (applied LAST - after the roll, tiredness and the reduction - so the hit lands exactly k
        /// times what it would have); 0 stays 0, NaN / negative = unchanged.</summary>
        public static float Apply(float damage, double k)
        {
            if (!(damage > 0) || double.IsNaN(k) || k <= 0) return damage;
            return (float)(damage * k);
        }

        /// <summary>The window is open while he reels (his reel end, re-timed by the measured stagger) plus the grace.</summary>
        public static bool Open(double now, double reelingUntil, double grace) =>
            !double.IsNaN(reelingUntil) && now <= reelingUntil + Math.Max(0, grace) + 1e-9;

        /// <summary>The on-screen line: "PERFECT STRIKE x2.8".</summary>
        public static string Text(double k) => "PERFECT STRIKE x" + k.ToString("0.0", CultureInfo.InvariantCulture);

        /// <summary>A direction in words from its owner's point of view ("right swing (from his right)").</summary>
        public static string Name(int dir) => dir switch
        {
            Up => "overhead",
            Down => "thrust",
            Left => "left swing",
            Right => "right swing",
            _ => "unknown",
        };
    }

    /// <summary>Step 100g: one open window - a man staggered by YOUR timed block, waiting for your counter.</summary>
    public struct PerfectStrikeWindow
    {
        public int Victim;

        /// <summary>His blocked attack's direction (his own; -1 unknown).</summary>
        public int Blocked;

        /// <summary>Your block's timing score (0-1).</summary>
        public double Score;

        public double OpenedAt;

        /// <summary>Wrong-side hits of yours on him in this window.</summary>
        public int WrongSide;

        /// <summary>Step 104d: opened by his REEL IN THE SADDLE (a rider), not a riposte stagger on foot.</summary>
        public bool Rider;
    }

    /// <summary>Step 100g: the open windows (a fixed array, no allocation after construction). Main thread.</summary>
    public sealed class PerfectStrikeBook
    {
        private readonly PerfectStrikeWindow[] _w = new PerfectStrikeWindow[PerfectStrikeMath.MaxWindows];

        public int Count { get; private set; }

        public ref PerfectStrikeWindow this[int i] => ref _w[i];

        public int IndexOf(int victim)
        {
            for (int i = 0; i < Count; i++)
                if (_w[i].Victim == victim) return i;
            return -1;
        }

        /// <summary>A new window on <paramref name="victim"/>. <paramref name="dropped"/> = an unused window it replaced (the same man staggered
        /// again, or the oldest when full) - the caller counts it as expired unused.</summary>
        public void Open(int victim, int blocked, double score, double now, out bool dropped) => Open(victim, blocked, score, now, false, out dropped, out _);

        /// <summary>The same; <paramref name="rider"/> = a rider's reel in the saddle (step 104d); <paramref name="droppedRider"/> = the dropped
        /// window was a rider's.</summary>
        public void Open(int victim, int blocked, double score, double now, bool rider, out bool dropped, out bool droppedRider)
        {
            droppedRider = false;
            dropped = false;
            int i = IndexOf(victim);
            if (i >= 0) dropped = true;
            else if (Count < _w.Length) i = Count++;
            else
            {
                i = 0;
                for (int j = 1; j < Count; j++)
                    if (_w[j].OpenedAt < _w[i].OpenedAt) i = j;
                dropped = true;
            }
            if (dropped) droppedRider = _w[i].Rider;
            _w[i] = new PerfectStrikeWindow { Victim = victim, Blocked = PerfectStrikeMath.IsAttack(blocked) ? blocked : -1, Score = score, OpenedAt = now, Rider = rider };
        }

        public void RemoveAt(int i)
        {
            if (i < 0 || i >= Count) return;
            _w[i] = _w[Count - 1];
            _w[Count - 1] = default;
            Count--;
        }

        public void Clear()
        {
            for (int i = 0; i < Count; i++) _w[i] = default;
            Count = 0;
        }
    }

    /// <summary>Step 100g: the perfect strikes over one battle - the [summary] "perfect strike" line.</summary>
    public sealed class PerfectStrikeStats
    {
        /// <summary>By YOUR strike's direction (0-3).</summary>
        public readonly int[] Landed = new int[4];

        /// <summary>By HIS blocked attack's direction + 1 (0 = unknown, 1-4 = overhead / thrust / left / right).</summary>
        public readonly int[] LandedAfter = new int[5];

        /// <summary>Wrong-side hits by YOUR strike's direction.</summary>
        public readonly int[] WrongSide = new int[4];

        /// <summary>Step 103: wrong-side hits by HIS blocked direction (0-3) x YOUR strike's (0-3) - index blocked * 4 + strike.</summary>
        public readonly int[] WrongSideAfter = new int[16];

        /// <summary>Step 103: your hits in a window whose blocked direction was unknown (nothing counts).</summary>
        public int BlockedUnknown;

        public int Opened, Replaced, ExpiredUnused, ClosedBySwitch, NotOnBody, Mounted, OffMainThread, UnknownDirection, Errors;
        public double MultiplierSum, MultiplierMin = double.NaN, MultiplierMax = double.NaN, ScoreSum;
        public double DamageBeforeSum, DamageAfterSum;

        /// <summary>Step 104d - THE PERFECT STRIKE ON HORSEBACK: windows on reeling riders (opened / replaced / expired unused / closed by a
        /// switch; none - he was knocked off), landed on them (from horseback / on foot), wrong side (any side off), your hits on such a rider's
        /// HORSE (ignored - the window kept), your strikes from horseback on men on foot (landed / wrong side).</summary>
        public int RiderOpened, RiderReplaced, RiderExpiredUnused, RiderClosedBySwitch, RiderKnockedOffNoWindow;
        public int RiderLanded, RiderLandedFromHorseback, RiderWrongSide, HorseHits, FootLandedFromHorseback, FootWrongSideFromHorseback;
        public double RiderMultiplierSum, RiderMultiplierMin = double.NaN, RiderMultiplierMax = double.NaN, RiderScoreSum, RiderDamageBeforeSum, RiderDamageAfterSum;

        /// <summary>Step 104d: one perfect strike on a reeling rider (also counted in <see cref="NoteLanded"/>).</summary>
        public void NoteRiderLanded(double score, double k, float before, float after, bool fromHorseback)
        {
            RiderLanded++;
            if (fromHorseback) RiderLandedFromHorseback++;
            RiderMultiplierSum += k;
            RiderScoreSum += score;
            if (double.IsNaN(RiderMultiplierMin) || k < RiderMultiplierMin) RiderMultiplierMin = k;
            if (double.IsNaN(RiderMultiplierMax) || k > RiderMultiplierMax) RiderMultiplierMax = k;
            RiderDamageBeforeSum += before;
            RiderDamageAfterSum += after;
        }

        /// <summary>Step 104d: the [summary] "perfect strike on horseback" line.</summary>
        public string MountedSummaryLine(in PerfectStrikeRules r)
        {
            var c = CultureInfo.InvariantCulture;
            var t = new StringBuilder();
            t.Append("perfect strike on horseback (step 104d; ").Append(r.MountedOn ? (r.HorsebackAnySide ? "ON, any side on a rider" : "ON, strict opposites on a rider too") : "OFF (" + r.MountedOffBecause + ")")
             .Append("): windows on riders reeling in the saddle after your timed block ").Append(RiderOpened.ToString(c))
             .Append(" (none - knocked off his horse ").Append(RiderKnockedOffNoWindow.ToString(c)).Append("), PERFECT STRIKES on them ").Append(RiderLanded.ToString(c))
             .Append(" (you riding ").Append(RiderLandedFromHorseback.ToString(c)).Append(", on foot ").Append((RiderLanded - RiderLandedFromHorseback).ToString(c)).Append(')');
            if (RiderLanded > 0)
                t.Append(", mean multiplier x").Append((RiderMultiplierSum / RiderLanded).ToString("0.00", c))
                 .Append(" (x").Append(RiderMultiplierMin.ToString("0.00", c)).Append("-x").Append(RiderMultiplierMax.ToString("0.00", c))
                 .Append(", mean timing ").Append((RiderScoreSum / RiderLanded).ToString("0.00", c))
                 .Append("), damage ").Append(RiderDamageBeforeSum.ToString("0", c)).Append(" → ").Append(RiderDamageAfterSum.ToString("0", c));
            else t.Append(", mean multiplier -");
            t.Append("; rider windows expired unused ").Append(RiderExpiredUnused.ToString(c));
            if (RiderReplaced > 0) t.Append(" (").Append(RiderReplaced.ToString(c)).Append(" replaced)");
            if (RiderClosedBySwitch > 0) t.Append(", closed by a switch ").Append(RiderClosedBySwitch.ToString(c));
            if (RiderWrongSide > 0) t.Append("; wrong side on a rider (any side off) ").Append(RiderWrongSide.ToString(c));
            t.Append("; your hits on such a rider's HORSE (ignored, the window kept) ").Append(HorseHits.ToString(c))
             .Append("; your strikes from horseback on men on foot (strict opposites): landed ").Append(FootLandedFromHorseback.ToString(c))
             .Append(", wrong side ").Append(FootWrongSideFromHorseback.ToString(c));
            if (Mounted > 0) t.Append("; hits from horseback not counted (switch off) ").Append(Mounted.ToString(c));
            return t.ToString();
        }

        public int LandedTotal
        {
            get
            {
                int n = 0;
                foreach (int x in Landed) n += x;
                return n;
            }
        }

        public int WrongSideTotal
        {
            get
            {
                int n = 0;
                foreach (int x in WrongSide) n += x;
                return n;
            }
        }

        public void NoteLanded(int strike, int blocked, double score, double k, float before, float after)
        {
            if (PerfectStrikeMath.IsAttack(strike)) Landed[strike]++;
            LandedAfter[PerfectStrikeMath.IsAttack(blocked) ? blocked + 1 : 0]++;
            MultiplierSum += k;
            ScoreSum += score;
            if (double.IsNaN(MultiplierMin) || k < MultiplierMin) MultiplierMin = k;
            if (double.IsNaN(MultiplierMax) || k > MultiplierMax) MultiplierMax = k;
            DamageBeforeSum += before;
            DamageAfterSum += after;
        }

        public void NoteWrongSide(int strike, int blocked = -1)
        {
            if (!PerfectStrikeMath.IsAttack(strike)) return;
            WrongSide[strike]++;
            if (PerfectStrikeMath.IsAttack(blocked)) WrongSideAfter[blocked * 4 + strike]++;
        }

        /// <summary>Step 103: the mismatches after one blocked direction - "after his blocked overhead (wanted your thrust): your left 1 / ...".</summary>
        private void AppendAfter(StringBuilder t, int blocked, CultureInfo c)
        {
            int b = blocked * 4, n = WrongSideAfter[b] + WrongSideAfter[b + 1] + WrongSideAfter[b + 2] + WrongSideAfter[b + 3];
            if (n == 0) return;
            t.Append("; after his blocked ").Append(PerfectStrikeMath.Name(blocked)).Append(" (wanted your ")
             .Append(PerfectStrikeMath.Name(PerfectStrikeMath.Opposite(blocked))).Append("): your left ").Append(WrongSideAfter[b + PerfectStrikeMath.Left].ToString(c))
             .Append(" / right ").Append(WrongSideAfter[b + PerfectStrikeMath.Right].ToString(c))
             .Append(" / overhead ").Append(WrongSideAfter[b + PerfectStrikeMath.Up].ToString(c))
             .Append(" / thrust ").Append(WrongSideAfter[b + PerfectStrikeMath.Down].ToString(c));
        }

        public string SummaryLine(in PerfectStrikeRules r)
        {
            var c = CultureInfo.InvariantCulture;
            var t = new StringBuilder();
            int landed = LandedTotal;
            t.Append("perfect strike (step 100g; ").Append(r.Enabled ? "ON" : "OFF (" + r.OffBecause + ")").Append("): windows opened by your timed blocks ")
             .Append(Opened.ToString(c)).Append(", PERFECT STRIKES landed ").Append(landed.ToString(c))
             .Append(" (your left swing ").Append(Landed[PerfectStrikeMath.Left].ToString(c))
             .Append(", right swing ").Append(Landed[PerfectStrikeMath.Right].ToString(c))
             .Append(", overhead ").Append(Landed[PerfectStrikeMath.Up].ToString(c))
             .Append(", thrust from below ").Append(Landed[PerfectStrikeMath.Down].ToString(c))
             .Append("; after his blocked left ").Append(LandedAfter[PerfectStrikeMath.Left + 1].ToString(c))
             .Append(" / right ").Append(LandedAfter[PerfectStrikeMath.Right + 1].ToString(c))
             .Append(" / overhead ").Append(LandedAfter[PerfectStrikeMath.Up + 1].ToString(c))
             .Append(" / thrust ").Append(LandedAfter[PerfectStrikeMath.Down + 1].ToString(c))
             .Append(" / unknown ").Append(LandedAfter[0].ToString(c)).Append(')');
            if (landed > 0)
                t.Append(", mean multiplier x").Append((MultiplierSum / landed).ToString("0.00", c))
                 .Append(" (x").Append(MultiplierMin.ToString("0.00", c)).Append("-x").Append(MultiplierMax.ToString("0.00", c))
                 .Append(", mean timing ").Append((ScoreSum / landed).ToString("0.00", c))
                 .Append("), damage ").Append(DamageBeforeSum.ToString("0", c)).Append(" → ").Append(DamageAfterSum.ToString("0", c));
            else t.Append(", mean multiplier -");
            t.Append("; windows expired unused ").Append(ExpiredUnused.ToString(c));
            if (Replaced > 0) t.Append(" (").Append(Replaced.ToString(c)).Append(" replaced by a new stagger)");
            if (ClosedBySwitch > 0) t.Append(", closed by a switch ").Append(ClosedBySwitch.ToString(c));
            t.Append("; wrong-side hits ").Append(WrongSideTotal.ToString(c))
             .Append(" (left ").Append(WrongSide[PerfectStrikeMath.Left].ToString(c))
             .Append(", right ").Append(WrongSide[PerfectStrikeMath.Right].ToString(c))
             .Append(", overhead ").Append(WrongSide[PerfectStrikeMath.Up].ToString(c))
             .Append(", thrust ").Append(WrongSide[PerfectStrikeMath.Down].ToString(c));
            AppendAfter(t, PerfectStrikeMath.Up, c);
            AppendAfter(t, PerfectStrikeMath.Down, c);
            AppendAfter(t, PerfectStrikeMath.Left, c);
            AppendAfter(t, PerfectStrikeMath.Right, c);
            t.Append(" - strict opposites since step 103; the window stays open)");
            if (BlockedUnknown > 0) t.Append("; your hits after a blocked attack of unknown direction (nothing counts) ").Append(BlockedUnknown.ToString(c));
            if (NotOnBody > 0) t.Append("; your hits on a window man that did not land on his body (blocked, shield, kick / bash, 0 damage) ").Append(NotOnBody.ToString(c));
            if (Mounted > 0) t.Append("; hits from horseback not counted (step 104d's switch off) ").Append(Mounted.ToString(c));
            if (UnknownDirection > 0) t.Append("; your direction unreadable ").Append(UnknownDirection.ToString(c));
            if (OffMainThread > 0) t.Append("; asked off the main thread (skipped) ").Append(OffMainThread.ToString(c));
            if (Errors > 0) t.Append("; ERRORS ").Append(Errors.ToString(c)).Append(" (disabled for the battle)");
            return t.ToString();
        }
    }
}

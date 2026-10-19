using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 62 (AI_NOTES "Step 62"): one mission's MORE-MEN-FALL numbers - every decided LANDED blow (kick, bash, clean hit) on a man on
    /// foot by where it came from (front / side / behind) and by the toughness ratio (attacker ÷ defender) - blows, knocked down, any outcome
    /// - and how often the step-62 multiplier hit its cap. The [summary] "impact direction" / "impact toughness" lines. Main thread only.
    /// </summary>
    public sealed class ImpactChargeStats
    {
        private const int D = ImpactMath.DirectionCount;

        /// <summary>The toughness bands: below 0.67 (the weaker man strikes) / 0.67-1.5 (even) / 1.5-3 / 3 and more; unknown apart.</summary>
        public const int ToughBandCount = 4;

        public readonly int[] DirBlows = new int[D];
        public readonly int[] DirDown = new int[D];
        public readonly int[] DirAny = new int[D];
        public readonly double[] DirFactor = new double[D];

        public readonly int[] ToughBlows = new int[ToughBandCount];
        public readonly int[] ToughDown = new int[ToughBandCount];
        public readonly int[] ToughAny = new int[ToughBandCount];
        public readonly double[] ToughFactor = new double[ToughBandCount];
        public int ToughUnknown;

        /// <summary>Landed blows on a man with a ranged weapon in hand / of them knocked down.</summary>
        public int RangedBlows, RangedDown;

        /// <summary>THE CHARGE's kinds: 0 into a right-side shield block, 1 into a parry, 2 into a wrong-side block, 3 landed from the front,
        /// 4 landed from the side, 5 landed from behind.</summary>
        public const int ChargeKindCount = 6;

        /// <summary>Every decided event (any family), and of them the CHARGING ones (sprinting now / carried after it).</summary>
        public int Decided, Charging, ChargingNow, Carried;

        /// <summary>The charging blows [kind]: count, the ATTACKER reeled (riposte) / thrown back, the DEFENDER staggered / knocked down.</summary>
        public readonly int[] ChargeBlows = new int[ChargeKindCount];
        public readonly int[] ChargeReeled = new int[ChargeKindCount];
        public readonly int[] ChargeThrown = new int[ChargeKindCount];
        public readonly int[] ChargeStaggered = new int[ChargeKindCount];
        public readonly int[] ChargeDown = new int[ChargeKindCount];

        /// <summary>The same for the blows that did NOT charge (the comparison).</summary>
        public readonly int[] OtherBlows = new int[ChargeKindCount];
        public readonly int[] OtherReeled = new int[ChargeKindCount];
        public readonly int[] OtherDown = new int[ChargeKindCount];

        /// <summary>Thrown back - applied (our blow registered on the attacker) / its Fall seen / not seen / not applied (refused, lever off,
        /// he left, a rider); YOU thrown back.</summary>
        public int ThrownApplied, ThrownSeen, ThrownNotSeen, ThrownNotApplied, ThrownYou;

        public static int ChargeKind(in ImpactDecision d)
        {
            switch (d.Event)
            {
                case ImpactEvent.RightBlock: return 0;
                case ImpactEvent.Parry: return 1;
                case ImpactEvent.WrongBlock: return 2;
            }
            return 3 + Math.Max(0, Math.Min(2, d.Direction));
        }

        public static string ChargeKindName(int k) => k switch
        {
            0 => "into a right-side shield block",
            1 => "into a parry",
            2 => "into a WRONG-side block",
            3 => "landed from the front",
            4 => "landed from the side",
            _ => "landed from BEHIND",
        };

        /// <summary>THE CHARGE: one decided event's charge (every family; a rider's never charges).</summary>
        public void NoteCharge(in ImpactDecision d, bool sprintingNow)
        {
            Decided++;
            int k = ChargeKind(in d);
            if (!d.Charging)
            {
                OtherBlows[k]++;
                if (d.Riposte.Happens || d.ThrownBack.Happens) OtherReeled[k]++;
                if (d.Knockdown.Happens) OtherDown[k]++;
                return;
            }
            Charging++;
            if (sprintingNow) ChargingNow++;
            else Carried++;
            ChargeBlows[k]++;
            if (d.Riposte.Happens) ChargeReeled[k]++;
            if (d.ThrownBack.Happens) ChargeThrown[k]++;
            if (d.Stagger.Happens) ChargeStaggered[k]++;
            if (d.Knockdown.Happens) ChargeDown[k]++;
        }

        public static int ToughBand(double ratio)
        {
            if (ratio < 0.67) return 0;
            if (ratio < 1.5) return 1;
            if (ratio < 3) return 2;
            return 3;
        }

        public static string ToughBandName(int b) => b switch
        {
            0 => "weaker attacker (below 0.67)",
            1 => "even (0.67-1.5)",
            2 => "tougher 1.5-3x",
            _ => "much tougher 3x+",
        };

        /// <summary>One decided event (any family; only LANDED blows on a man on foot are counted here).</summary>
        public void NoteDecision(in ImpactDecision d)
        {
            if (!ImpactMath.IsLanded(d.Event) || d.DefenderMounted) return;
            int dir = Math.Max(0, Math.Min(D - 1, d.Direction));
            bool down = d.Knockdown.Happens, any = d.AnyOutcome;
            DirBlows[dir]++;
            DirFactor[dir] += d.DirectionFactor;
            if (down) DirDown[dir]++;
            if (any) DirAny[dir]++;
            if (double.IsNaN(d.ToughnessRatio)) ToughUnknown++;
            else
            {
                int b = ToughBand(d.ToughnessRatio);
                ToughBlows[b]++;
                ToughFactor[b] += d.ToughnessFactor;
                if (down) ToughDown[b]++;
                if (any) ToughAny[b]++;
            }
            if (d.RangedFactor > 1.0001)
            {
                RangedBlows++;
                if (down) RangedDown++;
            }
        }

        /// <summary>The [summary] lines of step 62's factors (one each; "none" when nothing landed).</summary>
        public IReadOnlyList<string> SummaryLines()
        {
            var c = CultureInfo.InvariantCulture;
            var lines = new List<string>();
            var s = new StringBuilder("impact direction (step 62 - every decided landed blow on a man on foot by where it came from; down = knocked down, an outcome = down, staggered or his shield dropped): ");
            for (int i = 0; i < D; i++)
            {
                if (i > 0) s.Append(" | ");
                s.Append(ImpactMath.DirectionName(i)).Append(' ').Append(DirBlows[i].ToString(c));
                if (DirBlows[i] > 0)
                    s.Append(" (x").Append((DirFactor[i] / DirBlows[i]).ToString("0.00", c)).Append(": down ").Append(Pct(DirDown[i], DirBlows[i]))
                        .Append(", an outcome ").Append(Pct(DirAny[i], DirBlows[i])).Append(')');
            }
            lines.Add(s.ToString());
            s = new StringBuilder("impact toughness (step 62 - the same blows by the attacker's toughness ÷ the defender's, courage's measure): ");
            for (int b = 0; b < ToughBandCount; b++)
            {
                if (b > 0) s.Append(" | ");
                s.Append(ToughBandName(b)).Append(' ').Append(ToughBlows[b].ToString(c));
                if (ToughBlows[b] > 0)
                    s.Append(" (x").Append((ToughFactor[b] / ToughBlows[b]).ToString("0.00", c)).Append(": down ").Append(Pct(ToughDown[b], ToughBlows[b]))
                        .Append(", an outcome ").Append(Pct(ToughAny[b], ToughBlows[b])).Append(')');
            }
            s.Append(" | not counted (toughness off / unknown) ").Append(ToughUnknown.ToString(c));
            s.Append("; on a man with a ranged weapon in his hand ").Append(RangedBlows.ToString(c)).Append(" (down ").Append(Pct(RangedDown, RangedBlows)).Append(')');
            lines.Add(s.ToString());
            s = new StringBuilder("impact charges (step 62 - a CHARGE = the attacker sprinting, or his first blow within ImpactChargeCarrySeconds after a sprint): ");
            s.Append(Charging.ToString(c)).Append(" of ").Append(Decided.ToString(c)).Append(" decided events (sprinting at the blow ").Append(ChargingNow.ToString(c))
                .Append(", carried after the sprint ").Append(Carried.ToString(c)).Append(')');
            for (int k = 0; k < ChargeKindCount; k++)
            {
                if (ChargeBlows[k] == 0 && OtherBlows[k] == 0) continue;
                s.Append(" | ").Append(ChargeKindName(k)).Append(' ').Append(ChargeBlows[k].ToString(c));
                if (k <= 1) s.Append(": the attacker reeled ").Append(Pct(ChargeReeled[k], ChargeBlows[k])).Append(", THROWN BACK ").Append(Pct(ChargeThrown[k], ChargeBlows[k]));
                else s.Append(": the defender staggered ").Append(Pct(ChargeStaggered[k], ChargeBlows[k])).Append(", DOWN ").Append(Pct(ChargeDown[k], ChargeBlows[k]));
                s.Append(" (not charging ").Append(OtherBlows[k].ToString(c)).Append(": ")
                    .Append(k <= 1 ? "reeled " + Pct(OtherReeled[k], OtherBlows[k]) : "down " + Pct(OtherDown[k], OtherBlows[k])).Append(')');
            }
            s.Append("; thrown back applied ").Append(ThrownApplied.ToString(c)).Append(" → Fall seen ").Append(ThrownSeen.ToString(c)).Append(", not seen ").Append(ThrownNotSeen.ToString(c))
                .Append(", not applied ").Append(ThrownNotApplied.ToString(c)).Append("; YOU thrown back ").Append(ThrownYou.ToString(c));
            lines.Add(s.ToString());
            return lines;
        }

        internal static string Pct(int n, int of) => of > 0 ? (100.0 * n / of).ToString("0", CultureInfo.InvariantCulture) + "%" : "-";
    }
}

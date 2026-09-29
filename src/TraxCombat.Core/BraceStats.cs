using System;
using System.Collections.Generic;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 23 - one mission's brace numbers (AI_NOTES "Step 23"): who braced and under which order, how long (the "do lines
    /// turtle forever" check), why each brace ended, the per-man margins, the shield taken out and whether the AI kept it,
    /// the guard while bracing, and the attacks that still started. Main thread only (the logic's tick and hit callbacks).
    /// </summary>
    public sealed class BraceStats
    {
        /// <summary>The brace-length bins of the summary (seconds, lower edges).</summary>
        public static readonly double[] LengthEdges = { 0, 5, 15, 30, 60 };

        private readonly int[] _byKind = new int[(int)BraceOrderKind.Count];
        private readonly int[] _byGroup = new int[(int)BraceOrder.Count];
        private readonly int[] _ends = new int[(int)BraceEnd.Count];
        private readonly int[] _lengths = new int[5];
        private readonly int[] _bandBraces = new int[3];
        private readonly double[] _bandSeconds = new double[3];
        private readonly int[] _shieldAtStart = new int[6];

        // ---- men and time
        public int MenPolled;
        public double AiManSeconds;
        public int MenBraced;
        public int Braces => Sum(_byKind);
        public double BraceSeconds;
        public double LongestSeconds;
        public string? LongestName;
        public int LongAtEnd;

        // ---- the per-man margin (units in [−1, 1) - shown × the spread setting)
        public int UnitsRolled;
        public double UnitMin = double.NaN;
        public double UnitMax = double.NaN;
        public double UnitSum;

        // ---- the shield
        public int ShieldOffSwitch;
        public int OneHandedCalls;
        public int ShieldCalls;
        public int ShieldHeld;
        public int SwitchedBack;
        public int NeverGotIt;
        public int WieldErrors;

        // ---- the guard and the attacks while bracing
        public int HitsWhileBracing;
        public int BlockedWhileBracing;
        public long FramesCleared;
        public long GuardsRaised;
        public long ShieldRaisedFrames;
        public long RangedFramesPassed;
        public int MeleeWhileBracing;
        public int RangedWhileBracing;
        public int BracesWithoutACall;

        public bool SettingsChanged;

        public int ByKind(BraceOrderKind k) => _byKind[(int)k];
        public int ByGroup(BraceOrder g) => _byGroup[(int)g];
        public int Ends(BraceEnd e) => _ends[(int)e];
        public int Lengths(int bin) => _lengths[bin];
        public int ShieldAtStart(ShieldStep s) => _shieldAtStart[(int)s];
        public int BandBraces(int band) => _bandBraces[band];

        /// <summary>An AI man's first look this battle: his margin's roll.</summary>
        public void AddMan(double unit)
        {
            MenPolled++;
            UnitsRolled++;
            UnitSum += unit;
            if (double.IsNaN(UnitMin) || unit < UnitMin) UnitMin = unit;
            if (double.IsNaN(UnitMax) || unit > UnitMax) UnitMax = unit;
        }

        /// <summary>A brace began; <paramref name="firstForHim"/> = his first this battle.</summary>
        public void AddStart(BraceOrderKind kind, bool firstForHim)
        {
            _byKind[(int)kind]++;
            _byGroup[(int)BraceMath.Group(kind)]++;
            if (firstForHim) MenBraced++;
        }

        /// <summary>The shield's step at a brace's start (<see cref="ShieldOffSwitch"/> when BraceWieldShield is off).</summary>
        public void AddShieldAtStart(ShieldStep s) => _shieldAtStart[(int)s]++;

        /// <summary>A brace ended after <paramref name="seconds"/>; <paramref name="unit"/> = his roll (the margin band).</summary>
        public void AddEnd(BraceEnd why, double seconds, double unit, string? name)
        {
            _ends[(int)why]++;
            if (!(seconds >= 0)) seconds = 0;
            BraceSeconds += seconds;
            _lengths[LengthBin(seconds)]++;
            int band = UnitBand(unit);
            _bandBraces[band]++;
            _bandSeconds[band] += seconds;
            if (seconds > LongestSeconds)
            {
                LongestSeconds = seconds;
                LongestName = name;
            }
            if (why == BraceEnd.MissionEnd && seconds >= BraceMath.LongBraceSeconds) LongAtEnd++;
        }

        public void AddHit(bool blocked)
        {
            HitsWhileBracing++;
            if (blocked) BlockedWhileBracing++;
        }

        public static int LengthBin(double seconds)
        {
            for (int i = LengthEdges.Length - 1; i > 0; i--)
                if (seconds >= LengthEdges[i]) return i;
            return 0;
        }

        /// <summary>The margin band of a roll: 0 = short (the lowest third, back sooner), 1 = middle, 2 = long.</summary>
        public static int UnitBand(double unit) => double.IsNaN(unit) ? 1 : unit < -1.0 / 3 ? 0 : unit > 1.0 / 3 ? 2 : 1;

        /// <summary>The [summary] "brace" lines.</summary>
        public List<string> SummaryLines(in BraceRules r)
        {
            var lines = new List<string>(8);
            lines.Add("brace by orders (step 23): " + r.Describe() + (SettingsChanged ? " - its settings CHANGED during this battle (the lines mix both)" : string.Empty));
            int braces = Braces;
            if (MenPolled == 0)
            {
                lines.Add("brace: no AI fighter was looked at (the feature was off, or nobody fought)");
                return lines;
            }
            double share = AiManSeconds > 0 ? BraceSeconds / AiManSeconds : 0;
            lines.Add("brace: " + MenBraced + " of " + MenPolled + " AI fighters braced, " + braces + " braces - by order: " + KindsText()
                      + "; by floor: charge " + ByGroup(BraceOrder.Charge) + ", advance " + ByGroup(BraceOrder.Advance) + ", hold and the rest " + ByGroup(BraceOrder.Hold)
                      + "; time bracing " + F0(BraceSeconds) + " man-seconds = " + Pct(share) + " of the AI fighters' time on the field (" + F0(AiManSeconds) + " man-seconds)"
                      + (braces > 0 ? ", avg " + F1(BraceSeconds / braces) + " s a brace, " + F1(MenBraced > 0 ? BraceSeconds / MenBraced : 0) + " s per man who braced, the longest "
                                      + F1(LongestSeconds) + " s" + (LongestName != null ? " (" + LongestName + ")" : string.Empty) : string.Empty));
            lines.Add("brace lengths: under 5 s " + _lengths[0] + ", 5-15 s " + _lengths[1] + ", 15-30 s " + _lengths[2] + ", 30-60 s " + _lengths[3] + ", 60 s or more "
                      + _lengths[4] + "; still bracing when the battle ended " + Ends(BraceEnd.MissionEnd) + " (" + LongAtEnd + " of them for " + F0(BraceMath.LongBraceSeconds)
                      + " s or more) - many long braces and many still bracing at the end = lines that turtle (blocks cost Athletics and stop the refill: a pressed man may never get back to his target)");
            lines.Add("brace ends: " + EndsText());
            lines.Add("brace margins (BraceRecoverSpreadPercent " + r.RecoverSpreadPercent + "): " + UnitsRolled + " men rolled"
                      + (UnitsRolled > 0 ? ", their offsets now min " + Pts(UnitMin, r) + " / avg " + Pts(UnitSum / UnitsRolled, r) + " / max " + Pts(UnitMax, r) + " points on "
                                           + r.RecoverPercent + "%" : string.Empty)
                      + "; braces by his own margin: short (−) " + BandText(0) + ", middle " + BandText(1) + ", long (+) " + BandText(2));
            lines.Add("brace shield (BraceWieldShield " + (r.WieldShield ? "on" : "off") + "): at the brace's start - already in hand " + ShieldAtStart(ShieldStep.InHand)
                      + ", taken out " + ShieldAtStart(ShieldStep.WieldShield) + ", a one-hander first " + ShieldAtStart(ShieldStep.WieldOneHanded)
                      + ", no shield " + ShieldAtStart(ShieldStep.NoShield) + ", a ranged weapon in hand (left alone) " + ShieldAtStart(ShieldStep.RangedInHand)
                      + ", two-hander and no one-hander (left alone) " + ShieldAtStart(ShieldStep.NoOneHandedWeapon) + ", the switch off " + ShieldOffSwitch
                      + "; wield calls: the shield " + ShieldCalls + ", a one-hander " + OneHandedCalls + (WieldErrors > 0 ? ", failed " + WieldErrors : string.Empty)
                      + "; the shield seen in his hand after we asked " + ShieldHeld + ", the AI put it away again while bracing " + SwitchedBack
                      + ", never in his hand by the brace's end " + NeverGotIt);
            lines.Add("brace GUARD: melee hits taken while bracing " + HitsWhileBracing + ", blocked " + BlockedWhileBracing + " (" + Pct(HitsWhileBracing > 0 ? (double)BlockedWhileBracing / HitsWhileBracing : 0)
                      + " - compare the AI holds GUARD line's everyone else); his attacks taken out of his input " + FramesCleared + " frames (a guard raised " + GuardsRaised
                      + "), the shield held up " + ShieldRaisedFrames + " frames (BraceRaiseShield " + (r.RaiseShield ? "on" : "off") + "), ranged frames let through " + RangedFramesPassed
                      + "; melee attacks that started while bracing anyway " + MeleeWhileBracing + " (should be about 0), ranged shots while bracing " + RangedWhileBracing
                      + " (allowed); braces the engine never called our input hook during " + BracesWithoutACall + " (should be 0)");
            return lines;
        }

        private string KindsText()
        {
            var parts = new List<string>();
            for (int i = 0; i < _byKind.Length; i++)
                if (_byKind[i] > 0) parts.Add(BraceMath.Name((BraceOrderKind)i) + " " + _byKind[i]);
            return parts.Count == 0 ? "none" : string.Join(", ", parts);
        }

        private string EndsText()
        {
            var parts = new List<string>();
            for (int i = 0; i < _ends.Length; i++)
                parts.Add(BraceMath.Name((BraceEnd)i) + " " + _ends[i]);
            return string.Join(", ", parts);
        }

        private string BandText(int band) =>
            _bandBraces[band] + (_bandBraces[band] > 0 ? " (avg " + F1(_bandSeconds[band] / _bandBraces[band]) + " s)" : string.Empty);

        private static string Pts(double unit, in BraceRules r)
        {
            double v = BraceMath.Offset(in r, unit) * 100.0;
            return (v > 0.05 ? "+" : string.Empty) + v.ToString("0.0", CultureInfo.InvariantCulture);
        }

        private static int Sum(int[] a)
        {
            int s = 0;
            foreach (int v in a) s += v;
            return s;
        }

        private static string F0(double v) => v.ToString("0", CultureInfo.InvariantCulture);
        private static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
        private static string Pct(double v) => (v * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";
    }
}

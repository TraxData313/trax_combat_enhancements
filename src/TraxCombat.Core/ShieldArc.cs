using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 64 (Anton's looter battles 2026-10-19 - AI_NOTES "Step 64"): THE WALL'S BLOCKS COUNT. The engine's
    /// <c>CorrectSideShieldBlock</c> is native: it compares the shield's guard DIRECTION with the attack's. An AI shieldman's guard
    /// direction is a dice roll most of the time (the game's own <c>AiRandomizedDefendDirectionChance</c> = 1 − (skill ÷ 300 ×
    /// difficulty)³ - 0.89 for a 150-skill legionary on realistic, ~1 on normal: right 1 in 4 by luck) and a shield wall's poses are
    /// fixed (<c>ArrangementOrder.GetShieldDirectionOfUnit</c>: the front rank DefendDown, the inner ranks overhead) - so ~85% of his
    /// blocks read "wrong side" even fresh, and every correct-block rule (the riposte, the thrown back, the cheap cost, the refill running
    /// on) almost never fired. The rule: a SHIELD block whose attacker stands within <see cref="ShieldArcRules.Degrees"/> of the blocker's
    /// facing counts as a RIGHT-SIDE block for this mod, whatever the engine said; outside it the engine's verdict stands. Weapon parries
    /// keep the engine's direction. You and the AI alike. Read live; the master switch first.
    /// </summary>
    public readonly struct ShieldArcRules
    {
        public ShieldArcRules(bool modEnabled, bool on, double degrees)
        {
            ModEnabled = modEnabled;
            On = on;
            Degrees = double.IsNaN(degrees) ? 0 : degrees < 0 ? 0 : degrees > 180 ? 180 : degrees;
        }

        public bool ModEnabled { get; }

        /// <summary><c>ShieldFrontArcIsCorrect</c>.</summary>
        public bool On { get; }

        /// <summary><c>ShieldFrontArcDegrees</c> - the half-angle each side of his facing (0-180).</summary>
        public double Degrees { get; }

        /// <summary>The master switch first, then the switch.</summary>
        public bool Enabled => ModEnabled && On;

        public string? OffBecause => !ModEnabled ? "ModEnabled" : !On ? "ShieldFrontArcIsCorrect" : null;

        public static ShieldArcRules From(TraxSettings s) => new ShieldArcRules(s.ModEnabled, s.ShieldFrontArcIsCorrect, s.ShieldFrontArcDegrees);

        /// <summary>The settings sentence (mission start, summary).</summary>
        public string Describe() => Enabled
            ? "shield front arc: on - a shield block whose attacker stands within " + ShieldArcMath.F0(Degrees) + "° of the blocker's facing counts as a RIGHT-side block "
              + "(the cost, the refill, the impact model's correct block: the riposte, the thrown back), whatever side the game says - except a tired AI man's "
              + "guessed share (step 30's w = TiredBlockRandomPercent × (1 − f)), which keeps the game's verdict; outside it the game's verdict; "
              + "weapon parries keep the game's direction; you and the AI alike (ShieldFrontArcIsCorrect, ShieldFrontArcDegrees " + ShieldArcMath.F0(Degrees) + ")"
            : "shield front arc: off (" + OffBecause + ") - the game's own right / wrong side for every shield block";
    }

    /// <summary>Step 64 pure: when the angle is needed, the verdict, the summary's angle bands.</summary>
    public static class ShieldArcMath
    {
        /// <summary>The angle bands of the summary (degrees off the blocker's facing): 0-15, 15-30, 30-45, 45-60, 60-90, 90-120, 120-180.</summary>
        public static readonly double[] BandTops = { 15, 30, 45, 60, 90, 120, 180 };

        public static int BandCount => BandTops.Length;

        /// <summary>The angle is read only when the rule can change something: it is on and the ENGINE said wrong side with a shield.</summary>
        public static bool NeedsAngle(in ShieldArcRules r, BlockKind engine) => r.Enabled && engine == BlockKind.ShieldWrongSide;

        /// <summary>The attacker within the front arc (an unknown angle - NaN - never).</summary>
        public static bool InArc(in ShieldArcRules r, double angleDegrees) => !double.IsNaN(angleDegrees) && angleDegrees <= r.Degrees + 1e-9;

        /// <summary>OUR verdict: a wrong-side SHIELD block from within the front arc → a right-side block; anything else the engine's.</summary>
        public static BlockKind Resolve(in ShieldArcRules r, BlockKind engine, double angleDegrees) => Resolve(in r, engine, angleDegrees, 0, 1, out _);

        /// <summary>
        /// OUR verdict with step 30 kept: the arc corrects the GAME's own guess of the guard direction (skill-based, ~1 for most troops), not
        /// OURS - a tired AI man's guard is partly a guess on purpose (<c>TiredBlockRandomPercent</c>: w = % × (1 − f), his
        /// <c>Fighter.BlockRandomWeight</c>), so that share of his front blocks keeps the engine's wrong side: turned right only when
        /// <paramref name="roll"/> (<see cref="TiredRoll"/>, 0-1) ≥ <paramref name="tiredWeight"/>. A fresh man (w 0), you (0) and the setting at 0:
        /// every front block right; an empty man at 100 %: the engine's verdict, as before step 64 (a tired wall still breaks).
        /// <paramref name="keptByTiredness"/> = in the arc but kept wrong by that share.
        /// </summary>
        public static BlockKind Resolve(in ShieldArcRules r, BlockKind engine, double angleDegrees, double tiredWeight, double roll, out bool keptByTiredness)
        {
            keptByTiredness = false;
            if (!NeedsAngle(in r, engine) || !InArc(in r, angleDegrees)) return engine;
            if (tiredWeight > 0 && !(roll >= tiredWeight))
            {
                keptByTiredness = true;
                return engine;
            }
            return BlockKind.ShieldRightSide;
        }

        /// <summary>A stateless roll in [0, 1) for one blow - the same for every system that asks about the same collision (the attacker, the
        /// blocker, the attacker's swing number), so the cost, the guard line and the impact model always agree. No allocation, no shared state.</summary>
        public static double TiredRoll(int attackerIndex, int blockerIndex, int swing)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)attackerIndex) * 16777619u;
                h = (h ^ (uint)blockerIndex) * 16777619u;
                h = (h ^ (uint)swing) * 16777619u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (h & 0xFFFFFF) / (double)0x1000000;
            }
        }

        /// <summary>The summary band of an angle (0..<see cref="BandCount"/> − 1).</summary>
        public static int Band(double angleDegrees)
        {
            if (double.IsNaN(angleDegrees) || angleDegrees < 0) angleDegrees = 0;
            for (int i = 0; i < BandTops.Length; i++)
                if (angleDegrees < BandTops[i]) return i;
            return BandTops.Length - 1;
        }

        public static string BandName(int band) =>
            (band <= 0 ? 0 : BandTops[band - 1]).ToString("0", CultureInfo.InvariantCulture) + "-" + BandTops[band].ToString("0", CultureInfo.InvariantCulture) + "°";

        internal static string F0(double v) => v.ToString("0", CultureInfo.InvariantCulture);

        internal static string P0(int part, int whole) => whole <= 0 ? "-" : (100.0 * part / whole).ToString("0", CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>
    /// Step 64 per mission: every melee SHIELD block on a tracked man (once per collision, before any charge) - the ENGINE's verdict and
    /// OURS, for the AI's men and for you apart, how many the front arc turned right, the engine's wrong-side blocks by the attacker's angle
    /// off the blocker's facing (tune <c>ShieldFrontArcDegrees</c> on it), and the ones never looked at (the rule off, an untracked attacker)
    /// + the [summary] "shield front arc" line. Main thread, no allocation per event.
    /// </summary>
    public sealed class ShieldArcStats
    {
        private readonly int[] _engineRight = new int[2], _engineWrong = new int[2], _oursRight = new int[2], _oursWrong = new int[2], _turned = new int[2];
        private readonly int[] _wrongByBand = new int[ShieldArcMath.BandTops.Length];
        private readonly int[] _turnedByBand = new int[ShieldArcMath.BandTops.Length];

        /// <summary>Engine wrong-side blocks the rule did not look at (off, the master switch, an untracked attacker, a failed read).</summary>
        public int NotLooked { get; private set; }

        /// <summary>Front blocks left WRONG by the blocker's tiredness (step 30's share of his guard that is a guess - see <see cref="ShieldArcMath.Resolve(in ShieldArcRules, BlockKind, double, double, double, out bool)"/>).</summary>
        public int KeptByTiredness { get; private set; }

        /// <summary>One shield block: the engine's kind, ours, the angle (NaN = not read), <paramref name="you"/> = the blocker is you;
        /// <paramref name="keptByTiredness"/> = in the arc but kept wrong by his tiredness.</summary>
        public void Note(bool you, BlockKind engine, BlockKind ours, double angleDegrees, bool keptByTiredness = false)
        {
            if (keptByTiredness) KeptByTiredness++;
            if (engine != BlockKind.ShieldRightSide && engine != BlockKind.ShieldWrongSide) return;
            int w = you ? 1 : 0;
            if (engine == BlockKind.ShieldRightSide) _engineRight[w]++;
            else _engineWrong[w]++;
            if (ours == BlockKind.ShieldRightSide) _oursRight[w]++;
            else _oursWrong[w]++;
            if (engine != BlockKind.ShieldWrongSide) return;
            if (double.IsNaN(angleDegrees))
            {
                NotLooked++;
                return;
            }
            int b = ShieldArcMath.Band(angleDegrees);
            _wrongByBand[b]++;
            if (ours == BlockKind.ShieldRightSide)
            {
                _turned[w]++;
                _turnedByBand[b]++;
            }
        }

        public int EngineRight(bool you) => _engineRight[you ? 1 : 0];
        public int EngineWrong(bool you) => _engineWrong[you ? 1 : 0];
        public int OursRight(bool you) => _oursRight[you ? 1 : 0];
        public int OursWrong(bool you) => _oursWrong[you ? 1 : 0];
        public int Turned(bool you) => _turned[you ? 1 : 0];
        public int WrongInBand(int band) => _wrongByBand[band];

        /// <summary>The [summary] "shield front arc" line.</summary>
        public string SummaryLine(in ShieldArcRules r)
        {
            var t = new StringBuilder(320);
            t.Append("shield front arc (step 64, ").Append(r.Enabled ? "on, ±" + ShieldArcMath.F0(r.Degrees) + "°" : "off: " + r.OffBecause).Append("):");
            int all = _engineRight[0] + _engineWrong[0] + _engineRight[1] + _engineWrong[1];
            if (all == 0) return t.Append(" no melee shield block this battle").ToString();
            Who(t, " AI men's shield blocks ", 0);
            if (_engineRight[1] + _engineWrong[1] > 0) Who(t, "; YOURS ", 1);
            int wrong = _engineWrong[0] + _engineWrong[1];
            if (wrong > 0)
            {
                t.Append(" | the game's WRONG side by the attacker's angle off the blocker's facing (turned right):");
                bool first = true;
                for (int b = 0; b < _wrongByBand.Length; b++)
                {
                    if (_wrongByBand[b] == 0) continue;
                    t.Append(first ? " " : ", ").Append(ShieldArcMath.BandName(b)).Append(' ').Append(_wrongByBand[b]);
                    if (_turnedByBand[b] > 0) t.Append(" (").Append(_turnedByBand[b]).Append(')');
                    first = false;
                }
                if (NotLooked > 0) t.Append(first ? " " : ", ").Append("not looked at ").Append(NotLooked).Append(" (the rule off / an untracked attacker)");
                if (KeptByTiredness > 0)
                    t.Append("; from the front but kept WRONG by the blocker's tiredness (step 30's guessed share of a tired man's guard) ").Append(KeptByTiredness);
            }
            return t.ToString();
        }

        private void Who(StringBuilder t, string label, int w)
        {
            int n = _engineRight[w] + _engineWrong[w];
            t.Append(label).Append(n).Append(": the game right ").Append(_engineRight[w]).Append(" (").Append(ShieldArcMath.P0(_engineRight[w], n))
                .Append(") / WRONG ").Append(_engineWrong[w]).Append(" → ours right ").Append(_oursRight[w]).Append(" (").Append(ShieldArcMath.P0(_oursRight[w], n))
                .Append(") / WRONG ").Append(_oursWrong[w]).Append(" - ").Append(_turned[w]).Append(" wrong-side blocks from the front counted right");
        }
    }
}

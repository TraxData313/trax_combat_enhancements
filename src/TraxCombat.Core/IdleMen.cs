using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>Step 69: who holds an idle man (a man standing off while his unit charges) - the diagnostic's columns, in priority order.</summary>
    public enum IdleHolder
    {
        /// <summary>A ranged weapon in his hand, or a ranged / horse-archer / skirmisher formation: standing off to shoot or throw is NORMAL.</summary>
        Ranged,

        /// <summary>Step 41's shield-wall hold keeps him in his slot.</summary>
        WallHold,

        /// <summary>Step 67's fear flock steers his feet (our input owns them - standing at his place, or walking there slowly).</summary>
        FearFlock,

        /// <summary>Step 49's courage: timid (holding among his mates with tightened weights, or walking back).</summary>
        CourageTimid,

        /// <summary>Step 23's brace: guard only (his attacks taken out).</summary>
        Brace,

        /// <summary>None of ours - the game's own AI keeps him there.</summary>
        Game,

        Count,
    }

    /// <summary>Step 69: one man's idle watch (on his record; the logic owns it).</summary>
    public sealed class IdleMind
    {
        /// <summary>His last look's time and place (the speed between looks).</summary>
        public double LastT = double.NaN;
        public double LastX, LastY;

        /// <summary>Standing idle since (NaN = not idle); counted = his run passed the line and was counted whole.</summary>
        public double Since = double.NaN;
        public bool Counted;

        /// <summary>The holders he was counted under this battle (a bit each - the summary's men).</summary>
        public int MenMask;

        public void Reset()
        {
            Since = double.NaN;
            Counted = false;
        }
    }

    /// <summary>
    /// Step 69 pure rules of the IDLE-MEN diagnostic (Toni's screenshot 2026-10-19: "some of my men are sitting there in distance while my
    /// soldiers are finishing the enemy off"): an AI man on foot whose unit is on a CHARGE or an ADVANCE, an enemy within
    /// <see cref="EnemyMetres"/> and none within <see cref="ContactMetres"/>, moving slower than <see cref="StandMetresPerSecond"/> (his
    /// displacement between two looks) for more than <see cref="Seconds"/> - counted by who holds him. Measurement only: it changes nothing.
    /// </summary>
    public static class IdleMath
    {
        /// <summary>An enemy within this many metres - the fight is near.</summary>
        public const double EnemyMetres = 25;

        /// <summary>No enemy within this - he is not fighting.</summary>
        public const double ContactMetres = 3;

        /// <summary>Standing: slower than this between two looks (m/s).</summary>
        public const double StandMetresPerSecond = 0.5;

        /// <summary>Idle only after standing this long; the whole stand is then counted once, and every look's gap after.</summary>
        public const double Seconds = 5;

        /// <summary>A longer gap between looks = not watched meanwhile (the stand starts again).</summary>
        public const double MaxGapSeconds = 2;

        /// <summary>The seconds since his last look (NaN = first look or too long ago) and his speed over them; his place and time kept.</summary>
        public static double Gap(IdleMind m, double now, double px, double py, out double speed)
        {
            double gap = double.IsNaN(m.LastT) || now <= m.LastT ? double.NaN : now - m.LastT;
            speed = double.NaN;
            if (gap > 0 && gap <= MaxGapSeconds && !double.IsNaN(px) && !double.IsNaN(py))
            {
                double dx = px - m.LastX, dy = py - m.LastY;
                speed = Math.Sqrt(dx * dx + dy * dy) / gap;
            }
            else gap = double.NaN;
            m.LastT = now;
            m.LastX = px;
            m.LastY = py;
            return gap;
        }

        /// <summary>His unit's order is a CHARGE (charge, charge a target, attack an object, the AI's charge) or an ADVANCE (the player's, the AI's) -
        /// a shield wall's advance too.</summary>
        public static bool MovingOrder(BraceOrderKind order) =>
            order == BraceOrderKind.Charge || order == BraceOrderKind.ChargeToTarget || order == BraceOrderKind.AttackEntity || order == BraceOrderKind.AiCharge
            || order == BraceOrderKind.Advance || order == BraceOrderKind.AiAdvance;

        /// <summary>The idle test: on a charge / advance, standing, an enemy within <see cref="EnemyMetres"/> but not within <see cref="ContactMetres"/>.</summary>
        public static bool Idle(bool chargeOrAdvance, double speed, double nearestEnemy) =>
            chargeOrAdvance && !double.IsNaN(speed) && speed < StandMetresPerSecond && !double.IsNaN(nearestEnemy)
            && nearestEnemy <= EnemyMetres && nearestEnemy > ContactMetres;

        /// <summary>The seconds this look adds: 0 until he has stood more than <see cref="Seconds"/>; then the whole stand once, then each gap.
        /// Not idle (or no gap) = the stand ends.</summary>
        public static double Advance(IdleMind m, double now, double gap, bool idle)
        {
            if (!idle || double.IsNaN(gap))
            {
                m.Reset();
                return 0;
            }
            if (double.IsNaN(m.Since)) m.Since = now - gap;
            double run = now - m.Since;
            if (run <= Seconds) return 0;
            if (!m.Counted)
            {
                m.Counted = true;
                return run;
            }
            return gap;
        }

        /// <summary>Who holds him, in priority: a ranged weapon / formation (normal), the wall, the fear flock's steering, courage's timidity, the
        /// brace, else the game itself.</summary>
        public static IdleHolder Holder(bool ranged, bool wallHeld, bool fearSteering, bool timid, bool bracing) =>
            ranged ? IdleHolder.Ranged : wallHeld ? IdleHolder.WallHold : fearSteering ? IdleHolder.FearFlock : timid ? IdleHolder.CourageTimid
            : bracing ? IdleHolder.Brace : IdleHolder.Game;

        public static string HolderName(IdleHolder h) => h switch
        {
            IdleHolder.Ranged => "a ranged weapon in hand / a ranged or skirmisher formation (NORMAL)",
            IdleHolder.WallHold => "the wall hold (step 41)",
            IdleHolder.FearFlock => "the fear flock (step 67)",
            IdleHolder.CourageTimid => "courage timid (step 49)",
            IdleHolder.Brace => "the brace - guard only (step 23)",
            IdleHolder.Game => "the game itself (none of ours)",
            _ => h.ToString(),
        };
    }

    /// <summary>Step 69: the idle-men diagnostic's numbers per mission - man-seconds and men by holder, the nearest enemy, the looks.</summary>
    public sealed class IdleStats
    {
        private readonly double[] _seconds = new double[(int)IdleHolder.Count];
        private readonly int[] _men = new int[(int)IdleHolder.Count];
        private readonly double[] _enemy = new double[(int)IdleHolder.Count];
        private readonly long[] _samples = new long[(int)IdleHolder.Count];

        /// <summary>Looks at AI men on foot / of them on a charge or advance / the extra enemy reads beyond courage's scan.</summary>
        public long Looks;
        public long MovingOrderLooks;
        public long EnemyReads;

        /// <summary>Ranged men's idle seconds while one of ours held them too.</summary>
        public double RangedOursSeconds;

        public int Errors;

        /// <summary>Counts <paramref name="seconds"/> under <paramref name="h"/> (his first time under it = one man more).</summary>
        public void Add(IdleMind m, IdleHolder h, double seconds, double nearestEnemy)
        {
            int i = (int)h;
            if (i < 0 || i >= _seconds.Length || !(seconds > 0)) return;
            _seconds[i] += seconds;
            if (!double.IsNaN(nearestEnemy))
            {
                _enemy[i] += nearestEnemy;
                _samples[i]++;
            }
            int bit = 1 << i;
            if ((m.MenMask & bit) != 0) return;
            m.MenMask |= bit;
            _men[i]++;
        }

        public double SecondsOf(IdleHolder h) => _seconds[(int)h];

        public int MenOf(IdleHolder h) => _men[(int)h];

        public double MeanEnemy(IdleHolder h) => _samples[(int)h] > 0 ? _enemy[(int)h] / _samples[(int)h] : double.NaN;

        /// <summary>Ours = the wall, the fear flock, courage, the brace.</summary>
        public double OursSeconds => SecondsOf(IdleHolder.WallHold) + SecondsOf(IdleHolder.FearFlock) + SecondsOf(IdleHolder.CourageTimid) + SecondsOf(IdleHolder.Brace);

        private static string N(double v, string f = "0") => double.IsNaN(v) ? "-" : v.ToString(f, CultureInfo.InvariantCulture);

        private string Cell(IdleHolder h) =>
            IdleMath.HolderName(h) + " " + N(SecondsOf(h)) + " man-s (" + MenOf(h) + " men, the enemy " + N(MeanEnemy(h), "0.0") + " m)";

        public void AddSummaryLines(List<string> lines)
        {
            var sb = new StringBuilder();
            sb.Append("idle men (step 69 - AI men on foot on a charge / advance, an enemy within ").Append(N(IdleMath.EnemyMetres)).Append(" m and none within ")
              .Append(N(IdleMath.ContactMetres)).Append(" m, standing (below ").Append(N(IdleMath.StandMetresPerSecond, "0.0#")).Append(" m/s) more than ")
              .Append(N(IdleMath.Seconds)).Append(" s; the whole stand counted once past it): OURS ").Append(N(OursSeconds)).Append(" man-s - ")
              .Append(Cell(IdleHolder.FearFlock)).Append(", ").Append(Cell(IdleHolder.CourageTimid)).Append(", ").Append(Cell(IdleHolder.Brace)).Append(", ")
              .Append(Cell(IdleHolder.WallHold)).Append("; ").Append(Cell(IdleHolder.Game)).Append("; ").Append(Cell(IdleHolder.Ranged))
              .Append(" - of it held by ours too ").Append(N(RangedOursSeconds)).Append(" man-s; looks ").Append(Looks).Append(" (on a charge / advance ")
              .Append(MovingOrderLooks).Append("), enemy reads beyond courage's scan ").Append(EnemyReads).Append("; errors ").Append(Errors);
            lines.Add(sb.ToString());
        }
    }
}

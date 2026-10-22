using System;
using System.Collections.Generic;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 78c - EACH MAN RESTS ON HIS OWN (Toni 2026-10-22, after 78b's lull: "dont script for all of them to walk away or to push into each other,
    /// only per men rule, if tired - step back and wait to recover, so that if all are tired they should naturally keep distance ... if guy next to you
    /// rushes, there is a chance to make you go in too"; AI_NOTES "Step 78c", DESIGN decision 74). The settings, read live: the master switch FIRST, then
    /// the Athletics system (a bar to read), then <c>RestEnabled</c>, then the numbers. No unit-wide rule anywhere: every decision is one man's.
    /// </summary>
    public readonly struct RestRules
    {
        public RestRules(bool modEnabled, bool athleticsEnabled, bool restEnabled, int tiredMinPercent = 20, int tiredMaxPercent = 45,
            int readyChargeMinPercent = 55, int readyChargeMaxPercent = 95, int readyAdvanceMinPercent = 60, int readyAdvanceMaxPercent = 95,
            int readyHoldMinPercent = 65, int readyHoldMaxPercent = 95, double reachMetres = 3, double maxBackMetres = 4, double defendMetres = 2,
            double contagionMetres = 3, int contagionMaxPercent = 60, int contagionFloorPoints = 10, double gapDiagnosticMetres = 2)
        {
            ModEnabled = modEnabled;
            AthleticsEnabled = athleticsEnabled;
            RestEnabled = restEnabled;
            TiredMinPercent = ClampI(tiredMinPercent, 0, 100);
            TiredMaxPercent = Math.Max(TiredMinPercent, ClampI(tiredMaxPercent, 0, 100));
            ReadyChargeMinPercent = ClampI(readyChargeMinPercent, 0, 100);
            ReadyChargeMaxPercent = Math.Max(ReadyChargeMinPercent, ClampI(readyChargeMaxPercent, 0, 100));
            ReadyAdvanceMinPercent = ClampI(readyAdvanceMinPercent, 0, 100);
            ReadyAdvanceMaxPercent = Math.Max(ReadyAdvanceMinPercent, ClampI(readyAdvanceMaxPercent, 0, 100));
            ReadyHoldMinPercent = ClampI(readyHoldMinPercent, 0, 100);
            ReadyHoldMaxPercent = Math.Max(ReadyHoldMinPercent, ClampI(readyHoldMaxPercent, 0, 100));
            ReachMetres = Clamp(reachMetres, 1, 8);
            MaxBackMetres = Clamp(maxBackMetres, 0, 10);
            DefendMetres = Clamp(defendMetres, 0, 5);
            ContagionMetres = Clamp(contagionMetres, 0, 10);
            ContagionMaxPercent = ClampI(contagionMaxPercent, 0, 100);
            ContagionFloorPoints = ClampI(contagionFloorPoints, 0, 100);
            GapDiagnosticMetres = Clamp(gapDiagnosticMetres, 0.5, 10);
        }

        public static RestRules From(TraxSettings s) =>
            new RestRules(s.ModEnabled, s.AthleticsEnabled, s.RestEnabled, s.RestTiredMinPercent, s.RestTiredMaxPercent, s.RestReadyChargeMinPercent,
                s.RestReadyChargeMaxPercent, s.RestReadyAdvanceMinPercent, s.RestReadyAdvanceMaxPercent, s.RestReadyHoldMinPercent, s.RestReadyHoldMaxPercent,
                s.RestReachMetres, s.RestMaxBackMetres, s.RestDefendMetres, s.RestContagionMetres, s.RestContagionMaxPercent, s.RestContagionFloorPoints,
                s.RestGapDiagnosticMetres);

        public bool ModEnabled { get; }
        public bool AthleticsEnabled { get; }

        /// <summary>RestEnabled - a tired man steps back out of reach and waits to recover.</summary>
        public bool RestEnabled { get; }

        /// <summary>RestTired*Percent: his TIRED mark, rolled once per rest from this range (% of his full bar).</summary>
        public int TiredMinPercent { get; }
        public int TiredMaxPercent { get; }

        /// <summary>RestReady*Percent: his READY mark by his unit's order, rolled once per rest from these ranges.</summary>
        public int ReadyChargeMinPercent { get; }
        public int ReadyChargeMaxPercent { get; }
        public int ReadyAdvanceMinPercent { get; }
        public int ReadyAdvanceMaxPercent { get; }
        public int ReadyHoldMinPercent { get; }
        public int ReadyHoldMaxPercent { get; }

        /// <summary>RestReachMetres: OUT OF REACH = this far from his nearest enemy (he steps back until then); within it he is "at the fight".</summary>
        public double ReachMetres { get; }

        /// <summary>RestMaxBackMetres: the farthest he backs from where his rest began - never the 8-20 m walk-off.</summary>
        public double MaxBackMetres { get; }

        /// <summary>RestDefendMetres: an enemy within this = he defends himself normally (his attacks let through); else his guard only.</summary>
        public double DefendMetres { get; }

        /// <summary>RestContagionMetres: a mate of his unit within this going back in may take him along.</summary>
        public double ContagionMetres { get; }

        /// <summary>RestContagionMaxPercent: the chance at his ready mark (it falls in a line to 0 at the floor).</summary>
        public int ContagionMaxPercent { get; }

        /// <summary>RestContagionFloorPoints: no chance below his tired mark + this many points.</summary>
        public int ContagionFloorPoints { get; }

        /// <summary>RestGapDiagnosticMetres: the summary counts how often a natural gap this wide stood between two engaged fronts - a diagnostic only.</summary>
        public double GapDiagnosticMetres { get; }

        /// <summary>The rest acts: the master switch FIRST, then Athletics, then its own switch.</summary>
        public bool On => ModEnabled && AthleticsEnabled && RestEnabled;

        public string? OffBecause => !ModEnabled ? "ModEnabled" : !AthleticsEnabled ? "AthleticsEnabled" : !RestEnabled ? "RestEnabled" : null;

        public string Describe() =>
            "the rest (step 78c): an AI man on foot at the fight (an enemy within " + F(ReachMetres) + " m, or pressing an enemy line) whose bar falls below his own TIRED mark ("
            + TiredMinPercent + "-" + TiredMaxPercent + "%, rolled each rest) stops pushing and backs out of reach toward his own side facing the enemy (walking, at most " + F(MaxBackMetres)
            + " m from where he began and behind his own front), guard up - he strikes only at an enemy within " + F(DefendMetres) + " m - and waits until his own READY mark (charge "
            + ReadyChargeMinPercent + "-" + ReadyChargeMaxPercent + "%, advance " + ReadyAdvanceMinPercent + "-" + ReadyAdvanceMaxPercent + "%, hold " + ReadyHoldMinPercent + "-"
            + ReadyHoldMaxPercent + "%, rolled each rest; his wounds cap it), then goes back in; a mate within " + F(ContagionMetres) + " m going in takes him along at up to "
            + ContagionMaxPercent + "% near his mark (none below his tired mark + " + ContagionFloorPoints + "); a man in a shield wall rests IN his slot (never leaves it); "
            + "both sides, never you, never riders, no unit-wide rule - " + (On ? "ON" : "OFF (" + OffBecause + "): measured only");

        private static double Clamp(double v, double lo, double hi) => double.IsNaN(v) ? lo : v < lo ? lo : v > hi ? hi : v;

        private static int ClampI(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

        internal static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>Step 78c: how a rest ended.</summary>
    public enum RestIn
    {
        /// <summary>His bar reached his own ready mark - he goes back in.</summary>
        OwnMark,

        /// <summary>A mate next to him went in and took him along (the only social rule).</summary>
        Contagion,

        /// <summary>A fresher mate behind took his place (the rotation's swap) - no rest needed.</summary>
        Rotated,

        /// <summary>Courage's gates let him go (fled, routed, the player took him, a rider ...).</summary>
        Excluded,

        LeftField,

        /// <summary>The rest, Athletics or the master switch went off.</summary>
        SwitchedOff,

        MissionEnd,
        Error,
        Count,
    }

    /// <summary>
    /// Step 78c - the pure part of one man's rest: his two marks rolled from wide spreads (so men never move together), the ready mark capped by his
    /// wounds, the contagion chance, the walk-back limit, the attack hold.
    /// </summary>
    public static class RestMath
    {
        /// <summary>The ready mark sits at least this far under his wound cap (fraction) - a wounded man can reach it. Plumbing.</summary>
        public const double WoundSlack = 0.02;

        /// <summary>... and at least this far above his tired mark, else he does not rest at all (his wounds will not let him recover). Plumbing.</summary>
        public const double MinBand = 0.05;

        /// <summary>He is held back (the clamp) only while his nearest enemy is within the reach + this (m) - farther off his own AI moves him (he keeps up
        /// with his unit when it advances, it never leaves him behind). Plumbing.</summary>
        public const double ClampMarginMetres = 1.0;

        /// <summary>He is held back while his nearest enemy is this close (the clamp's band) - but never more than <see cref="RestRules.MaxBackMetres"/> +
        /// <see cref="FollowSlackMetres"/> behind his own front (<paramref name="behindFront"/>; NaN = unknown): when his line has moved on he follows it
        /// (his own AI) until he is that close again - a resting man stays a few metres behind his front, never left behind.</summary>
        public static bool HoldsBack(in RestRules r, double enemyDist, double behindFront = double.NaN) =>
            !double.IsNaN(enemyDist) && enemyDist < r.ReachMetres + ClampMarginMetres && (double.IsNaN(behindFront) || behindFront <= r.MaxBackMetres + FollowSlackMetres);

        /// <summary>The slack past the walk limit behind his front before he follows his line (m). Plumbing.</summary>
        public const double FollowSlackMetres = 0.5;

        /// <summary>He walks back at this much of the stick - a walk, never a run. Plumbing.</summary>
        public const double WalkStick = 0.6;

        /// <summary>He steps this much past the reach line, so he does not stop exactly at its edge. Plumbing.</summary>
        public const double ReachSlackMetres = 0.3;

        /// <summary>A wall man resting more than this out of his slot is counted (the summary's wall check - Toni: never leave the wall). Plumbing.</summary>
        public const double WallSlotSlackMetres = 0.5;

        /// <summary>A resting man more than this behind his own unit's front is counted (the walk-off Toni saw). Plumbing.</summary>
        public const double FarBehindFrontMetres = 5.0;

        /// <summary>The summary's walk-back histogram: bins of this (m), this many. Plumbing.</summary>
        public const double BackBinMetres = 0.25;

        public const int BackBins = 80;

        /// <summary>His tired mark from a roll 0..1: min + roll × (max − min) (a fraction of his full bar).</summary>
        public static double TiredMark(in RestRules r, double roll) => Lerp(r.TiredMinPercent, r.TiredMaxPercent, roll) / 100.0;

        /// <summary>His ready mark by his unit's order group from a roll 0..1 (no formation = a charge's).</summary>
        public static double ReadyMark(in RestRules r, BraceOrder g, double roll) => g switch
        {
            BraceOrder.Advance => Lerp(r.ReadyAdvanceMinPercent, r.ReadyAdvanceMaxPercent, roll) / 100.0,
            BraceOrder.Hold => Lerp(r.ReadyHoldMinPercent, r.ReadyHoldMaxPercent, roll) / 100.0,
            _ => Lerp(r.ReadyChargeMinPercent, r.ReadyChargeMaxPercent, roll) / 100.0,
        };

        /// <summary>The ready mark he can reach: under his wound cap by <see cref="WoundSlack"/>.</summary>
        public static double CappedReady(double ready, double cap) => double.IsNaN(cap) ? ready : Math.Min(ready, cap - WoundSlack);

        /// <summary>He rests: his bar below his tired mark, at the fight (or a held wall man), and a ready mark he can still reach above it.</summary>
        public static bool Starts(double bar, double tired, double readyCapped, bool atFight) =>
            atFight && !double.IsNaN(bar) && bar < tired - 1e-9 && readyCapped >= tired + MinBand - 1e-9;

        /// <summary>He goes back in by his own mark.</summary>
        public static bool Ready(double bar, double readyCapped) => !double.IsNaN(bar) && bar >= readyCapped - 1e-9;

        /// <summary>The CONTAGION chance (0..1) when a mate beside him goes back in: 0 below his tired mark + the floor points, rising in a line to
        /// ContagionMaxPercent at his ready mark (and above).</summary>
        public static double ContagionChance(in RestRules r, double bar, double tired, double ready)
        {
            if (double.IsNaN(bar) || r.ContagionMaxPercent <= 0) return 0;
            double floor = tired + r.ContagionFloorPoints / 100.0;
            if (bar < floor - 1e-9) return 0;
            double max = r.ContagionMaxPercent / 100.0;
            if (ready <= floor + 1e-9) return max;
            double share = (bar - floor) / (ready - floor);
            return max * (share < 0 ? 0 : share > 1 ? 1 : share);
        }

        /// <summary>THE WALK-BACK LIMIT: how far he still backs now - out of reach of his nearest enemy (+ a little), never past
        /// <see cref="RestRules.MaxBackMetres"/> from where the rest began NOR more than that behind his own unit's front (<paramref name="behindFront"/>;
        /// NaN = unknown) - a few metres behind his front at most, never a walk-off. 0 = he stands (out of reach, or at his limit).</summary>
        public static double WalkBack(in RestRules r, double enemyDist, double fromStart, double behindFront = double.NaN)
        {
            if (double.IsNaN(enemyDist) || enemyDist >= r.ReachMetres - 1e-9) return 0;
            double room = r.MaxBackMetres - (double.IsNaN(fromStart) ? 0 : fromStart);
            if (!double.IsNaN(behindFront)) room = Math.Min(room, r.MaxBackMetres - behindFront);
            if (room <= 1e-6) return 0;
            return Math.Min(r.ReachMetres + ReachSlackMetres - enemyDist, room);
        }

        /// <summary>His attacks held this frame: in a wall always (he only guards in his slot), else unless an enemy is within the defend distance.</summary>
        public static bool HoldsAttacks(in RestRules r, bool wall, double enemyDist) => wall || double.IsNaN(enemyDist) || enemyDist > r.DefendMetres + 1e-9;

        public static string InName(RestIn i) => i switch
        {
            RestIn.OwnMark => "his own ready mark",
            RestIn.Contagion => "contagion (a mate went in)",
            RestIn.Rotated => "rotated (a fresher man took his place)",
            RestIn.Excluded => "excluded",
            RestIn.LeftField => "left the field",
            RestIn.SwitchedOff => "switched off",
            RestIn.MissionEnd => "mission end",
            RestIn.Error => "error",
            _ => i.ToString(),
        };

        private static double Lerp(int lo, int hi, double roll)
        {
            double u = double.IsNaN(roll) ? 0.5 : roll < 0 ? 0 : roll > 1 ? 1 : roll;
            return lo + u * (hi - lo);
        }
    }

    /// <summary>Step 78c per mission: rests started and how they ended, how long, how far men stepped back, facing, the wall men in their slots, the
    /// marks rolled, the contagion, and the natural gap between engaged fronts (a diagnostic) - the two [summary] "rest" lines.</summary>
    public sealed class RestStats
    {
        public long Started, StartedInWall, WouldRestOff, NoBand;
        public readonly long[] Ends = new long[(int)RestIn.Count];
        public double SecondsSum, SecondsMax;
        public long SecondsN;

        /// <summary>The farthest each rest took him from its start (m) - a histogram (p90) and the sums.</summary>
        public readonly long[] BackBins = new long[RestMath.BackBins + 1];
        public double BackSum, BackMax;
        public long BackN;

        /// <summary>The farthest behind his own unit's front while resting (m), and rests more than <see cref="RestMath.FarBehindFrontMetres"/> behind it.</summary>
        public double BehindFrontMax;
        public long FarBehindFront;

        /// <summary>Facing samples while resting (his look against the direction to his nearest enemy) and those turned away (more than 90°).</summary>
        public long FacingSamples, TurnedAway;

        /// <summary>Wall men resting in their slots: looks, looks more than <see cref="RestMath.WallSlotSlackMetres"/> out of the slot, the farthest.</summary>
        public long WallLooks, WallOut;
        public double WallOutMax;

        /// <summary>The marks rolled: the tired marks, the ready marks by order group (charge / advance / hold).</summary>
        public double TiredSum, TiredMin = double.NaN, TiredMax = double.NaN;
        public long TiredN;
        public readonly double[] ReadySum = new double[3];
        public readonly long[] ReadyN = new long[3];

        /// <summary>Contagion: rolls (a mate went in beside a resting man above his floor), and how many went in with him.</summary>
        public long ContagionRolls, ContagionNoChance;

        /// <summary>Frames: the walk back, the clamp, the attack held.</summary>
        public long MoveFrames, ClampFrames, HeldFrames;

        /// <summary>The natural gap between two engaged fronts, by side (0 = the other sides, 1 = your team): unit looks, looks with a gap of the
        /// diagnostic's metres or more, the sum of the gaps (its 25th percentile over the contact).</summary>
        public readonly long[] GapLooks = new long[2], GapClear = new long[2];
        public readonly double[] GapSum = new double[2];

        public long Errors, Looks, Steers;
        public double LookCostMs, SteerCostMs;

        public void AddTired(double mark)
        {
            if (double.IsNaN(mark)) return;
            TiredSum += mark;
            TiredN++;
            if (double.IsNaN(TiredMin) || mark < TiredMin) TiredMin = mark;
            if (double.IsNaN(TiredMax) || mark > TiredMax) TiredMax = mark;
        }

        public void AddReady(BraceOrder g, double mark)
        {
            int i = g == BraceOrder.Advance ? 1 : g == BraceOrder.Hold ? 2 : 0;
            ReadySum[i] += mark;
            ReadyN[i]++;
        }

        /// <summary>A rest's end: why, how long, the farthest from its start.</summary>
        public void AddEnd(RestIn why, double seconds, double back)
        {
            if (why < RestIn.Count) Ends[(int)why]++;
            if (!double.IsNaN(seconds))
            {
                SecondsSum += seconds;
                SecondsN++;
                if (seconds > SecondsMax) SecondsMax = seconds;
            }
            if (double.IsNaN(back)) return;
            BackSum += back;
            BackN++;
            if (back > BackMax) BackMax = back;
            int b = (int)(back / RestMath.BackBinMetres);
            BackBins[b < 0 ? 0 : b > RestMath.BackBins ? RestMath.BackBins : b]++;
        }

        /// <summary>The walk-back's p-th quantile from the histogram (the bin's upper edge).</summary>
        public double BackQuantile(double p)
        {
            if (BackN <= 0) return double.NaN;
            long need = (long)Math.Ceiling(p * BackN), seen = 0;
            for (int i = 0; i <= RestMath.BackBins; i++)
            {
                seen += BackBins[i];
                if (seen >= need) return Math.Min(BackMax, (i + 1) * RestMath.BackBinMetres);
            }
            return BackMax;
        }

        public void AddGap(bool yourSide, double gap, double diag)
        {
            if (double.IsNaN(gap)) return;
            int i = yourSide ? 1 : 0;
            GapLooks[i]++;
            GapSum[i] += gap;
            if (gap >= diag - 1e-9) GapClear[i]++;
        }

        public void AddSummaryLines(List<string> lines, in RestRules r)
        {
            var sb = new System.Text.StringBuilder(720);
            sb.Append("rest (step 78c) - EACH MAN ON HIS OWN ").Append(r.On ? "ON" : "OFF (" + r.OffBecause + ")").Append(": rests started ").Append(Started)
              .Append(" (in a wall slot ").Append(StartedInWall).Append("; tired men at the fight with the rest OFF ").Append(WouldRestOff).Append("; too wounded to recover ")
              .Append(NoBand).Append("), lasted mean ").Append(F1(SecondsN > 0 ? SecondsSum / SecondsN : double.NaN)).Append(" s (max ").Append(F1(SecondsMax))
              .Append(" s); went back in: by his own mark ").Append(Ends[(int)RestIn.OwnMark]).Append(", by contagion ").Append(Ends[(int)RestIn.Contagion])
              .Append(" (rolls ").Append(ContagionRolls).Append(", below the floor ").Append(ContagionNoChance).Append("), other ends:");
            for (int i = (int)RestIn.Rotated; i < (int)RestIn.Count; i++)
                if (Ends[i] > 0) sb.Append(' ').Append(RestMath.InName((RestIn)i)).Append(' ').Append(Ends[i]).Append(',');
            sb.Append(" stepped back from his start mean ").Append(F2(BackN > 0 ? BackSum / BackN : double.NaN)).Append(" m, p90 ").Append(F2(BackQuantile(0.9)))
              .Append(" m, max ").Append(F2(BackN > 0 ? BackMax : double.NaN)).Append(" m (limit ").Append(F1(r.MaxBackMetres)).Append(" m); the farthest behind his own front ")
              .Append(F1(BehindFrontMax)).Append(" m, rests more than ").Append(F1(RestMath.FarBehindFrontMetres)).Append(" m behind it ").Append(FarBehindFront)
              .Append("; men TURNED AWAY while resting ").Append(TurnedAway).Append(" of ").Append(FacingSamples).Append(" facing samples (should be 0); WALL men resting in their slots: ")
              .Append(WallLooks).Append(" looks, more than ").Append(F1(RestMath.WallSlotSlackMetres)).Append(" m out of the slot ").Append(WallOut).Append(" (the farthest ")
              .Append(F2(WallLooks > 0 ? WallOutMax : double.NaN)).Append(" m, should be 0); the marks rolled: tired mean ").Append(Pct(TiredN > 0 ? TiredSum / TiredN : double.NaN))
              .Append(" (").Append(Pct(TiredMin)).Append("-").Append(Pct(TiredMax)).Append("), ready charge ").Append(Pct(ReadyN[0] > 0 ? ReadySum[0] / ReadyN[0] : double.NaN))
              .Append(" / advance ").Append(Pct(ReadyN[1] > 0 ? ReadySum[1] / ReadyN[1] : double.NaN)).Append(" / hold ").Append(Pct(ReadyN[2] > 0 ? ReadySum[2] / ReadyN[2] : double.NaN))
              .Append("; input frames: walked back ").Append(MoveFrames).Append(", clamped ").Append(ClampFrames).Append(", attacks held ").Append(HeldFrames)
              .Append("; cost: looks ").Append(F2(LookCostMs)).Append(" ms (").Append(Looks).Append("), steering ").Append(F2(SteerCostMs)).Append(" ms (").Append(Steers)
              .Append("); errors ").Append(Errors);
            lines.Add(sb.ToString());
            sb.Clear();
            sb.Append("rest (step 78c) - THE NATURAL GAP between engaged fronts (a diagnostic only, never a trigger; its 25th percentile along the contact, ")
              .Append(F1(RhythmMath.GapBinMetres)).Append(" m bins): your team ").Append(Share(1, r.GapDiagnosticMetres)).Append("; the other sides ")
              .Append(Share(0, r.GapDiagnosticMetres));
            lines.Add(sb.ToString());
        }

        private string Share(int i, double diag) =>
            GapLooks[i] == 0
                ? "no engaged looks"
                : "a gap of " + F1(diag) + " m+ in " + Pct((double)GapClear[i] / GapLooks[i]) + " of " + GapLooks[i] + " unit looks (0.5 s each), the gap mean "
                  + F1(GapSum[i] / GapLooks[i]) + " m";

        public static string Pct(double v) => double.IsNaN(v) ? "-" : (100 * v).ToString("0", CultureInfo.InvariantCulture) + "%";

        public static string F1(double v) => double.IsNaN(v) ? "-" : v.ToString("0.0", CultureInfo.InvariantCulture);

        public static string F2(double v) => double.IsNaN(v) ? "-" : v.ToString("0.00", CultureInfo.InvariantCulture);
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>Step 33: which AI share a formation's movement order gives (DESIGN §2 "The sprint").</summary>
    public enum SprintOrder
    {
        /// <summary>Charge, charge a target, attack a gate, the AI's tactical charge - <c>SprintAiChargeSharePercent</c>.</summary>
        Charge,

        /// <summary>Advance, the AI's own advance, a move to a position - <c>SprintAiMoveSharePercent</c>.</summary>
        AdvanceOrMove,

        /// <summary>Hold / halt, retreat, fall back, follow, no order, no formation - never a sprint.</summary>
        None,

        Count,
    }

    /// <summary>Step 33: why a sprint ended.</summary>
    public enum SprintEnd
    {
        /// <summary>You let go of the sprint key.</summary>
        Released,

        /// <summary>The store ran dry (or his points dropped to nothing).</summary>
        StoreEmpty,

        /// <summary>The AI's share of his store is used up (the sprint's full length).</summary>
        LengthDone,

        /// <summary>An AI sprinter came within a few metres of an enemy (<see cref="SprintMath.MeleeRangeMetres"/>).</summary>
        InMelee,

        /// <summary>His formation's order changed again.</summary>
        OrderChanged,

        /// <summary>He mounted a horse.</summary>
        Mounted,

        /// <summary>The master switch, AthleticsEnabled, SprintEnabled (or SprintAiEnabled for an AI sprint) off.</summary>
        SwitchedOff,

        /// <summary>He fell, fled or left the field.</summary>
        LeftField,

        /// <summary>You took him or he became yours / the AI took your hero (RTS Camera) / you left a fight mode or opened a menu.</summary>
        ControlChanged,

        /// <summary>Step 19's hideout boss fight (a fresh start: everyone's store full again).</summary>
        FreshStart,

        /// <summary>The battle ended mid-sprint.</summary>
        MissionEnd,

        /// <summary>An exception on his sprint (logged) - ended to stay safe.</summary>
        Error,

        Count,
    }

    /// <summary>Step 33: what a start attempt did.</summary>
    public enum SprintStart
    {
        Started,

        /// <summary>Less than SprintMinStartPercent of the store is filled.</summary>
        BelowMinimum,

        /// <summary>An empty bar = no store at all.</summary>
        NoStore,

        /// <summary>On a horse - never.</summary>
        Mounted,

        /// <summary>The sprint (or the AI's) is switched off.</summary>
        Off,

        /// <summary>Already sprinting.</summary>
        Running,
    }

    /// <summary>
    /// The sprint's settings for one decision (step 33, Anton 2026-10-12 - DESIGN §2 "The sprint"), read live from
    /// <see cref="TraxSettings"/>. A struct, built where needed.
    /// </summary>
    public readonly struct SprintRules
    {
        public SprintRules(bool modEnabled, bool athleticsEnabled, bool sprintEnabled, bool aiEnabled, double pointsPerSecond, int minStartPercent,
            double maxMultiplier, double minMultiplier, int fullSpeedSharePercent, double refillSeconds, double refillMovingFactor,
            double refillExhaustedFactor, int aiChargeSharePercent, int aiMoveSharePercent)
        {
            ModEnabled = modEnabled;
            AthleticsEnabled = athleticsEnabled;
            SprintEnabled = sprintEnabled;
            AiEnabled = aiEnabled;
            PointsPerSecond = double.IsNaN(pointsPerSecond) || pointsPerSecond < SprintMath.MinPointsPerSecond ? SprintMath.MinPointsPerSecond : pointsPerSecond;
            MinStartPercent = Clamp(minStartPercent, 0, 100);
            MaxMultiplier = double.IsNaN(maxMultiplier) || maxMultiplier < 1 ? 1 : maxMultiplier;
            double min = double.IsNaN(minMultiplier) || minMultiplier < 1 ? 1 : minMultiplier;
            MinMultiplier = min > MaxMultiplier ? MaxMultiplier : min;
            FullSpeedSharePercent = Clamp(fullSpeedSharePercent, 0, 100);
            RefillSeconds = double.IsNaN(refillSeconds) || refillSeconds < SprintMath.MinRefillSeconds ? SprintMath.MinRefillSeconds : refillSeconds;
            RefillMovingFactor = Clamp01(refillMovingFactor);
            RefillExhaustedFactor = Clamp01(refillExhaustedFactor);
            AiChargeSharePercent = Clamp(aiChargeSharePercent, 0, 100);
            AiMoveSharePercent = Clamp(aiMoveSharePercent, 0, 100);
        }

        public bool ModEnabled { get; }

        public bool AthleticsEnabled { get; }

        /// <summary>SprintEnabled (the switch itself).</summary>
        public bool SprintEnabled { get; }

        /// <summary>SprintAiEnabled (the switch itself).</summary>
        public bool AiEnabled { get; }

        /// <summary>SprintPointsPerSecond - Athletics points per stored second.</summary>
        public double PointsPerSecond { get; }

        public int MinStartPercent { get; }

        /// <summary>SprintMaxMultiplier, at least 1.</summary>
        public double MaxMultiplier { get; }

        /// <summary>SprintMinMultiplier, at least 1 and never above <see cref="MaxMultiplier"/>.</summary>
        public double MinMultiplier { get; }

        public int FullSpeedSharePercent { get; }

        /// <summary>SprintRefillSeconds - empty → full standing at full strength.</summary>
        public double RefillSeconds { get; }

        public double RefillMovingFactor { get; }

        public double RefillExhaustedFactor { get; }

        public int AiChargeSharePercent { get; }

        public int AiMoveSharePercent { get; }

        /// <summary>The sprint runs (the master switch first, then Athletics - the store is made of its points - then its own switch).</summary>
        public bool Enabled => ModEnabled && AthleticsEnabled && SprintEnabled;

        /// <summary>The AI sprints on new orders now.</summary>
        public bool AiOn => Enabled && AiEnabled;

        /// <summary>Which switch holds it off ("ModEnabled", "AthleticsEnabled", "SprintEnabled"), null while on.</summary>
        public string? OffBecause => !ModEnabled ? "ModEnabled" : !AthleticsEnabled ? "AthleticsEnabled" : !SprintEnabled ? "SprintEnabled" : null;

        /// <summary>The least share of the store a start needs (0..1).</summary>
        public double MinStartShare => MinStartPercent / 100.0;

        /// <summary>The full-speed part of a sprint (0..1).</summary>
        public double FullSpeedShare => FullSpeedSharePercent / 100.0;

        public static SprintRules From(TraxSettings s) =>
            new SprintRules(s.ModEnabled, s.AthleticsEnabled, s.SprintEnabled, s.SprintAiEnabled, s.SprintPointsPerSecond, s.SprintMinStartPercent,
                s.SprintMaxMultiplier, s.SprintMinMultiplier, s.SprintFullSpeedSharePercent, s.SprintRefillSeconds, s.SprintRefillMovingFactor,
                s.SprintRefillExhaustedFactor, s.SprintAiChargeSharePercent, s.SprintAiMoveSharePercent);

        /// <summary>The settings sentence of the mission-start line, a change and the summary.</summary>
        public string Describe(string keyName)
        {
            if (!Enabled) return "OFF (" + OffBecause + ") - nobody sprints";
            var ci = CultureInfo.InvariantCulture;
            double full = 150 / PointsPerSecond;
            return "ON - hold " + keyName + " (on foot, in a fight); the store = Athletics points / " + PointsPerSecond.ToString("0.#", ci) + " s (150 points = "
                   + full.ToString("0.0#", ci) + " s, empty = 0), a start needs " + MinStartPercent + "% of it; speed x" + MaxMultiplier.ToString("0.00", ci)
                   + " of the CURRENT run speed for the first " + FullSpeedSharePercent + "% of the sprint's length, then a straight line to x" + MinMultiplier.ToString("0.00", ci)
                   + " (a full " + full.ToString("0.0#", ci) + " s sprint: x" + MaxMultiplier.ToString("0.00", ci) + " until " + (full * FullSpeedShare).ToString("0.0#", ci)
                   + " s); drains only while moving; refill " + RefillSeconds.ToString("0.#", ci) + " s standing at full strength, x" + RefillMovingFactor.ToString("0.##", ci)
                   + " moving, x" + RefillExhaustedFactor.ToString("0.##", ci) + " at an empty bar (straight line from the peak line), never while sprinting; no Athletics refill "
                   + "while sprinting; AI " + (AiEnabled ? "ON - on a NEW order: charge " + AiChargeSharePercent + "% of the store, advance / move " + AiMoveSharePercent + "%" : "OFF (SprintAiEnabled)");
        }

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

        private static double Clamp01(double v) => double.IsNaN(v) || v < 0 ? 0 : v > 1 ? 1 : v;
    }

    /// <summary>
    /// Step 33 pure functions (AI_NOTES "Step 33"): the store's size, its refill rate, the start gate, the speed profile.
    /// </summary>
    public static class SprintMath
    {
        /// <summary>SprintPointsPerSecond's floor (never ÷ 0; the range starts at 5).</summary>
        public const double MinPointsPerSecond = 1.0;

        /// <summary>SprintRefillSeconds' floor (the range starts at 1).</summary>
        public const double MinRefillSeconds = 0.1;

        /// <summary>The applied multiplier moves in steps of this (each step = one properties recompute). Plumbing.</summary>
        public const float Step = 0.05f;

        /// <summary>A sprinter drains his store (and his sprint's clock runs) only above this speed - "actually moving". Plumbing.</summary>
        public const float MovingMetresPerSecond = 0.5f;

        /// <summary>An AI sprinter stops once an enemy is this close ("in melee"). Plumbing.</summary>
        public const float MeleeRangeMetres = 3.0f;

        /// <summary>How often an AI sprinter's melee check runs, seconds. Plumbing.</summary>
        public const double MeleeCheckSeconds = 0.25;

        /// <summary>How often every formation's order is read, seconds. Plumbing.</summary>
        public const double OrderPollSeconds = 0.25;

        /// <summary>Less than this many seconds stored is "empty" (float dust). Plumbing.</summary>
        public const double EmptySeconds = 0.01;

        /// <summary>The store's size, seconds, at <paramref name="points"/> Athletics points: points ÷ per second (150 / 20 = 7.5); 0 at empty.</summary>
        public static double Capacity(double points, double pointsPerSecond)
        {
            if (double.IsNaN(points) || points <= 0) return 0;
            double per = double.IsNaN(pointsPerSecond) || pointsPerSecond < MinPointsPerSecond ? MinPointsPerSecond : pointsPerSecond;
            return points / per;
        }

        /// <summary>This fighter's store size now: his CURRENT points (0 while Athletics is off - the caller gates on Enabled).</summary>
        public static double Capacity(in SprintRules s, in AthleticsRules r, Fighter f) =>
            Capacity(f.Fraction * AthleticsMath.PoolPoints(in r, f), s.PointsPerSecond);

        /// <summary>The tiredness factor of the refill: 1 at and above the peak line (f = 1), a straight line down to
        /// SprintRefillExhaustedFactor at an empty bar (f = 0).</summary>
        public static double TiredFactor(in SprintRules s, double peakShare)
        {
            double f = double.IsNaN(peakShare) || peakShare < 0 ? 0 : peakShare > 1 ? 1 : peakShare;
            return s.RefillExhaustedFactor + (1.0 - s.RefillExhaustedFactor) * f;
        }

        /// <summary>The refill's rate multiplier: x SprintRefillMovingFactor while moving faster than a walk, x the tiredness line.</summary>
        public static double RefillMultiplier(in SprintRules s, bool moving, double peakShare) =>
            (moving ? s.RefillMovingFactor : 1.0) * TiredFactor(in s, peakShare);

        /// <summary>Seconds of store refilled per second: capacity ÷ SprintRefillSeconds × <see cref="RefillMultiplier"/>.</summary>
        public static double RefillPerSecond(in SprintRules s, double capacity, bool moving, double peakShare) =>
            capacity <= 0 ? 0 : capacity / s.RefillSeconds * RefillMultiplier(in s, moving, peakShare);

        /// <summary>Seconds from empty to full at a fixed multiplier (7 s standing fresh, 14 moving fresh, 14 standing empty-tired,
        /// 28 both); +∞ when nothing refills.</summary>
        public static double SecondsToFull(in SprintRules s, bool moving, double peakShare)
        {
            double m = RefillMultiplier(in s, moving, peakShare);
            return m > 0 ? s.RefillSeconds / m : double.PositiveInfinity;
        }

        /// <summary>A start is allowed: a store at all, and at least SprintMinStartPercent of it filled.</summary>
        public static bool CanStart(in SprintRules s, double stored, double capacity) =>
            capacity > EmptySeconds && stored > EmptySeconds && stored >= s.MinStartShare * capacity - 1e-9;

        /// <summary>The share of his store an AI man sprints for on a new order of <paramref name="order"/> (0..1).</summary>
        public static double AiShare(in SprintRules s, SprintOrder order) => order switch
        {
            SprintOrder.Charge => s.AiChargeSharePercent / 100.0,
            SprintOrder.AdvanceOrMove => s.AiMoveSharePercent / 100.0,
            _ => 0,
        };

        /// <summary>A formation's order kind (step 23's read) → its sprint share group.</summary>
        public static SprintOrder Group(BraceOrderKind kind) => kind switch
        {
            BraceOrderKind.Charge => SprintOrder.Charge,
            BraceOrderKind.ChargeToTarget => SprintOrder.Charge,
            BraceOrderKind.AttackEntity => SprintOrder.Charge,
            BraceOrderKind.AiCharge => SprintOrder.Charge,
            BraceOrderKind.Advance => SprintOrder.AdvanceOrMove,
            BraceOrderKind.AiAdvance => SprintOrder.AdvanceOrMove,
            BraceOrderKind.Move => SprintOrder.AdvanceOrMove,
            _ => SprintOrder.None,
        };

        /// <summary>
        /// The speed multiplier <paramref name="elapsed"/> seconds (of moving) into a sprint <paramref name="length"/> seconds long:
        /// SprintMaxMultiplier for the first SprintFullSpeedSharePercent of the length, then a straight line to SprintMinMultiplier
        /// at the end. Full 7.5 s at 30%: 1.6 until 2.25 s, 1.35 at 4.875 s, 1.1 at 7.5 s. A zero length = the start speed.
        /// </summary>
        public static double Multiplier(in SprintRules s, double elapsed, double length)
        {
            if (!(length > 0)) return s.MaxMultiplier;
            double full = s.FullSpeedShare * length;
            if (elapsed <= full) return s.MaxMultiplier;
            double rest = length - full;
            double t = rest > 0 ? (elapsed - full) / rest : 1;
            if (t > 1) t = 1;
            return s.MaxMultiplier + (s.MinMultiplier - s.MaxMultiplier) * t;
        }

        /// <summary>The multiplier as applied: rounded to the nearest <see cref="Step"/> (each step one recompute); 1 stays 1.</summary>
        public static float Quantize(double m)
        {
            if (double.IsNaN(m) || m <= 1) return 1f;
            float q = (float)(Math.Round(m / Step, MidpointRounding.AwayFromZero) * Step);
            return q < 1f ? 1f : (float)Math.Round(q, 2);
        }

        /// <summary>"6.2 s" - the bar's label (tenths, rounded down so it never shows more than is there).</summary>
        public static string SecondsText(double seconds)
        {
            if (double.IsNaN(seconds) || seconds < 0) seconds = 0;
            return (Math.Floor(seconds * 10 + 1e-9) / 10).ToString("0.0", CultureInfo.InvariantCulture) + " s";
        }

        /// <summary>The bar's word.</summary>
        public const string LabelWord = "Sprint";

        /// <summary>The bar's colours (#RRGGBBAA): grey below the start mark, blue ready, bright while sprinting.</summary>
        public const string BelowStartHex = "#7A7A7AFF";

        public const string ReadyHex = "#4FA3E0FF";

        public const string SprintingHex = "#FFE36BFF";

        public static string OrderName(SprintOrder o) => o switch
        {
            SprintOrder.Charge => "charge",
            SprintOrder.AdvanceOrMove => "advance / move",
            _ => "other",
        };

        public static string EndName(SprintEnd e) => e switch
        {
            SprintEnd.Released => "key released",
            SprintEnd.StoreEmpty => "store empty",
            SprintEnd.LengthDone => "its share used",
            SprintEnd.InMelee => "an enemy within " + MeleeRangeMetres.ToString("0.#", CultureInfo.InvariantCulture) + " m",
            SprintEnd.OrderChanged => "the order changed again",
            SprintEnd.Mounted => "mounted",
            SprintEnd.SwitchedOff => "switched off",
            SprintEnd.LeftField => "left the field",
            SprintEnd.ControlChanged => "control changed / left the fight",
            SprintEnd.FreshStart => "the hideout's fresh start",
            SprintEnd.MissionEnd => "the battle ended",
            SprintEnd.Error => "an error",
            _ => e.ToString(),
        };
    }

    /// <summary>
    /// One fighter's sprint store (step 33) - allocated once per tracked fighter, reused; no per-tick allocation. The seconds
    /// stored, the running sprint (its length T = the seconds it may use, its clock = seconds of MOVING since it began), and
    /// what the summary needs. A new store is FULL until the first clamp (<see cref="Clamp"/>) pulls it to his capacity.
    /// </summary>
    public sealed class SprintStore
    {
        /// <summary>Seconds stored (+∞ = full until the first clamp).</summary>
        public double Stored { get; private set; } = double.PositiveInfinity;

        public bool Active { get; private set; }

        /// <summary>This sprint's length, seconds: what was stored at its start (you), or the AI's share of it.</summary>
        public double Length { get; private set; }

        /// <summary>Seconds of moving since it began - the speed profile's clock.</summary>
        public double Elapsed { get; private set; }

        /// <summary>Mission time it began.</summary>
        public double Since { get; private set; }

        /// <summary>Seconds stored at its start.</summary>
        public double StoredAtStart { get; private set; }

        /// <summary>The AI order group it began under (None for yours).</summary>
        public SprintOrder Order { get; private set; } = SprintOrder.None;

        /// <summary>The player's sprint (not an AI order sprint).</summary>
        public bool ByKey { get; private set; }

        /// <summary>Pulls the store down to <paramref name="capacity"/> (his points dropped, or the first look). True when it moved
        /// below what it held (not counting the first look's +∞).</summary>
        public bool Clamp(double capacity)
        {
            if (double.IsNaN(capacity) || capacity < 0) capacity = 0;
            if (Stored <= capacity) return false;
            bool real = !double.IsPositiveInfinity(Stored);
            Stored = capacity;
            return real;
        }

        /// <summary>Back to full (a fresh start) - the next clamp sets it to his capacity.</summary>
        public void FillUp() => Stored = double.PositiveInfinity;

        /// <summary>Refills over <paramref name="dt"/> at <paramref name="perSecond"/>, never above <paramref name="capacity"/>; nothing while a sprint runs.</summary>
        public double Refill(double dt, double perSecond, double capacity)
        {
            if (Active || !(dt > 0) || !(perSecond > 0)) return 0;
            double before = Stored;
            double after = before + perSecond * dt;
            if (after > capacity) after = capacity;
            if (after < before) return 0;
            Stored = after;
            return after - before;
        }

        /// <summary>Tries to start a sprint at <paramref name="now"/> that may use <paramref name="share"/> of what is stored (1 for
        /// you; the AI's order share). The SprintMinStartPercent gate is on the whole store.</summary>
        public SprintStart TryStart(in SprintRules s, double now, double capacity, double share, bool byKey, SprintOrder order)
        {
            if (Active) return SprintStart.Running;
            if (!s.Enabled || (!byKey && !s.AiOn)) return SprintStart.Off;
            Clamp(capacity);
            if (!(capacity > SprintMath.EmptySeconds) || !(Stored > SprintMath.EmptySeconds)) return SprintStart.NoStore;
            if (!SprintMath.CanStart(in s, Stored, capacity)) return SprintStart.BelowMinimum;
            double length = Stored * Math.Max(0, Math.Min(1, share));
            if (!(length > SprintMath.EmptySeconds)) return SprintStart.Off;
            Active = true;
            Length = length;
            Elapsed = 0;
            Since = now;
            StoredAtStart = Stored;
            Order = order;
            ByKey = byKey;
            return SprintStart.Started;
        }

        /// <summary>
        /// Runs a sprint over <paramref name="dt"/>: while <paramref name="moving"/> the clock advances and the store drains 1 s a
        /// second (never past the sprint's length or below 0). True when the sprint is over (the store dry, or its length used).
        /// </summary>
        public bool Advance(double dt, bool moving, out SprintEnd end)
        {
            end = SprintEnd.Count;
            if (!Active) return false;
            if (moving && dt > 0)
            {
                double use = Math.Min(dt, Math.Max(0, Length - Elapsed));
                Elapsed += use;
                Stored = Math.Max(0, Stored - use);
            }
            if (Stored <= SprintMath.EmptySeconds)
            {
                end = SprintEnd.StoreEmpty;
                return true;
            }
            if (Elapsed >= Length - 1e-9)
            {
                end = SprintEnd.LengthDone;
                return true;
            }
            return false;
        }

        /// <summary>The speed multiplier now (1 when no sprint runs).</summary>
        public double Multiplier(in SprintRules s) => Active ? SprintMath.Multiplier(in s, Elapsed, Length) : 1.0;

        /// <summary>Ends the running sprint; what is left stays stored. Returns the seconds it ran (its clock).</summary>
        public double Stop()
        {
            if (!Active) return 0;
            Active = false;
            return Elapsed;
        }

        /// <summary>The store as a share of <paramref name="capacity"/> (0..1; 0 with no capacity).</summary>
        public double Share(double capacity)
        {
            if (!(capacity > SprintMath.EmptySeconds)) return 0;
            double v = Math.Min(Stored, capacity) / capacity;
            return v < 0 ? 0 : v > 1 ? 1 : v;
        }

        /// <summary>Seconds stored, capped at <paramref name="capacity"/> (what the bar shows).</summary>
        public double StoredUpTo(double capacity) => Math.Max(0, Math.Min(Stored, Math.Max(0, capacity)));
    }

    /// <summary>Step 33: your sprint store as the sprint bar reads it.</summary>
    public readonly struct SprintReading
    {
        public SprintReading(double stored, double capacity, double minShare, bool active, double multiplier)
        {
            Stored = stored;
            Capacity = capacity;
            MinShare = minShare;
            Active = active;
            Multiplier = multiplier;
        }

        /// <summary>Seconds stored (capped at the capacity).</summary>
        public double Stored { get; }

        /// <summary>The store's size now, seconds.</summary>
        public double Capacity { get; }

        /// <summary>The start mark (SprintMinStartPercent / 100).</summary>
        public double MinShare { get; }

        public bool Active { get; }

        /// <summary>The multiplier applied now (1 when not sprinting).</summary>
        public double Multiplier { get; }

        /// <summary>The fill, 0..1.</summary>
        public double Share => Capacity > SprintMath.EmptySeconds ? Math.Max(0, Math.Min(1, Stored / Capacity)) : 0;

        /// <summary>A sprint could start now (at or above the mark, a store at all).</summary>
        public bool Ready => Capacity > SprintMath.EmptySeconds && Stored > SprintMath.EmptySeconds && Share >= MinShare - 1e-9;

        /// <summary>"Sprint 6.2 s".</summary>
        public string LabelText => SprintMath.LabelWord + " " + SprintMath.SecondsText(Stored);

        /// <summary>The fill's colour: yellow while sprinting, blue when a sprint could start, grey below the mark.</summary>
        public string ColorHex => Active ? SprintMath.SprintingHex : Ready ? SprintMath.ReadyHex : SprintMath.BelowStartHex;
    }

    /// <summary>
    /// Step 33: one mission's sprint numbers + the [summary] "sprint" lines. Main thread.
    /// </summary>
    public sealed class SprintStats
    {
        private readonly int[,] _ends = new int[2, (int)SprintEnd.Count];   // [you 1 / AI 0, end]
        private readonly MeanStd[] _lengths = new MeanStd[2];               // T asked
        private readonly MeanStd[] _ran = new MeanStd[2];                   // seconds it really ran (its clock)
        private readonly double[] _longest = new double[2];
        private readonly int[] _aiStarted = new int[(int)SprintOrder.Count];
        private readonly double[] _aiSeconds = new double[(int)SprintOrder.Count];
        private readonly int[] _aiRefused = new int[(int)SprintOrder.Count];
        private readonly int[] _ordersSeen = new int[(int)SprintOrder.Count];
        private MeanStd _askedSpeed;
        private MeanStd _engineSpeed;
        private double _engineMax;
        private int _farOff;

        public int YouStarted;
        public int YouRefusedBelowMinimum;
        public int YouRefusedEmpty;
        public int YouRefusedMounted;
        public int AiSkippedMounted;
        public int AiSkippedPlayer;
        public int AiSkippedSameOrder;
        public int Recomputes;
        public int Clamps;
        public int FreshStarts;
        public double RefilledSeconds;
        public long MeleeChecks;

        private static int Who(bool you) => you ? 1 : 0;

        public void AddOrderSeen(SprintOrder o) => _ordersSeen[(int)o]++;

        public int OrdersSeen(SprintOrder o) => _ordersSeen[(int)o];

        public void AddAiStart(SprintOrder o) => _aiStarted[(int)o]++;

        public void AddAiRefused(SprintOrder o) => _aiRefused[(int)o]++;

        public int AiStarted(SprintOrder o) => _aiStarted[(int)o];

        public int AiStartedTotal
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _aiStarted.Length; i++) n += _aiStarted[i];
                return n;
            }
        }

        /// <summary>A sprint ended: who, its length, the seconds it ran, why.</summary>
        public void AddEnd(bool you, SprintOrder order, double length, double ran, SprintEnd end)
        {
            int w = Who(you);
            if ((int)end >= 0 && (int)end < (int)SprintEnd.Count) _ends[w, (int)end]++;
            _lengths[w].Add(length);
            _ran[w].Add(ran);
            if (ran > _longest[w]) _longest[w] = ran;
            if (!you && order != SprintOrder.Count) _aiSeconds[(int)order] += ran;
        }

        public int Ends(bool you, SprintEnd end) => _ends[Who(you), (int)end];

        public int Ended(bool you) => _lengths[Who(you)].Count;

        public double AiSeconds(SprintOrder o) => _aiSeconds[(int)o];

        /// <summary>A sample while sprinting: the multiplier asked (relative to his run speed at the sprint's start) and the engine's
        /// top speed ÷ his top then.</summary>
        public void AddSpeed(double asked, double engine)
        {
            if (double.IsNaN(asked) || double.IsNaN(engine) || asked <= 0 || engine <= 0) return;
            _askedSpeed.Add(asked);
            _engineSpeed.Add(engine);
            if (engine > _engineMax) _engineMax = engine;
            if (Math.Abs(engine - asked) > 0.05) _farOff++;
        }

        public int SpeedSamples => _askedSpeed.Count;

        public double AskedMean => _askedSpeed.Mean;

        public double EngineMean => _engineSpeed.Mean;

        /// <summary>The engine followed the asked speed: means within 0.05 (with 20+ samples).</summary>
        public bool? EngineFollows => _askedSpeed.Count < 20 ? (bool?)null : Math.Abs(_engineSpeed.Mean - _askedSpeed.Mean) <= 0.05;

        /// <summary>The [summary] lines.</summary>
        public void AddSummaryLines(List<string> lines, in SprintRules s, string keyName)
        {
            var ci = CultureInfo.InvariantCulture;
            lines.Add("sprint (step 33): " + s.Describe(keyName));
            lines.Add("sprint - you: " + YouStarted + " sprints" + (Ended(true) > 0
                          ? " (length asked avg " + F2(_lengths[1].Mean) + " s, ran avg " + F2(_ran[1].Mean) + " s, longest " + F2(_longest[1]) + " s)"
                          : string.Empty)
                      + "; refused: below " + s.MinStartPercent + "% stored " + YouRefusedBelowMinimum + ", an empty bar " + YouRefusedEmpty + ", on a horse " + YouRefusedMounted
                      + "; ends: " + EndsText(true));
            lines.Add("sprint - AI: new orders seen - charge " + OrdersSeen(SprintOrder.Charge) + ", advance / move " + OrdersSeen(SprintOrder.AdvanceOrMove) + ", other "
                      + OrdersSeen(SprintOrder.None) + "; sprints - charge " + AiStarted(SprintOrder.Charge) + " men (" + F1(AiSeconds(SprintOrder.Charge)) + " s in all"
                      + Avg(AiSeconds(SprintOrder.Charge), AiStarted(SprintOrder.Charge)) + "), advance / move " + AiStarted(SprintOrder.AdvanceOrMove) + " men ("
                      + F1(AiSeconds(SprintOrder.AdvanceOrMove)) + " s" + Avg(AiSeconds(SprintOrder.AdvanceOrMove), AiStarted(SprintOrder.AdvanceOrMove))
                      + "); not started - below " + s.MinStartPercent + "% or empty: charge " + _aiRefused[(int)SprintOrder.Charge] + ", advance / move "
                      + _aiRefused[(int)SprintOrder.AdvanceOrMove] + "; mounted " + AiSkippedMounted + ", yours to drive " + AiSkippedPlayer
                      + "; ends: " + EndsText(false) + "; melee checks " + MeleeChecks.ToString(ci));
            string verdict = EngineFollows == null ? "too few samples to judge (20 needed)"
                : EngineFollows == true ? "the engine's top speed follows the sprint - no cap above the run speed"
                : "the engine's top speed does NOT follow (a native cap?) - tell Claude";
            lines.Add("sprint speed check (÷ his top speed at the sprint's start, while it ran): " + SpeedSamples + " samples - asked avg x" + F2(AskedMean)
                      + ", engine top avg x" + F2(EngineMean) + ", max x" + F2(_engineMax) + ", off by more than 0.05: " + _farOff + " - " + verdict
                      + "; " + Recomputes + " recomputes for the sprint, stores clamped by a points drop " + Clamps + ", refilled " + F1(RefilledSeconds) + " s of store"
                      + (FreshStarts > 0 ? ", fresh starts " + FreshStarts : string.Empty));
        }

        private string EndsText(bool you)
        {
            var parts = new List<string>();
            for (int e = 0; e < (int)SprintEnd.Count; e++)
            {
                int n = _ends[Who(you), e];
                if (n > 0) parts.Add(SprintMath.EndName((SprintEnd)e) + " " + n);
            }
            return parts.Count == 0 ? "none" : string.Join(", ", parts);
        }

        private static string Avg(double sum, int n) => n > 0 ? ", avg " + F2(sum / n) + " s" : string.Empty;

        private static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        private static string F2(double v) => double.IsNaN(v) ? "n/a" : v.ToString("0.00", CultureInfo.InvariantCulture);
    }
}

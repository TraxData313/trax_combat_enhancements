using System;
using System.Collections.Generic;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>Step 33: which AI reserve a formation's movement order gives (DESIGN §2 "The sprint"; step 36 split advance and
    /// move; step 37: one reserve per kind).</summary>
    public enum SprintOrder
    {
        /// <summary>Charge, charge a target, attack a gate, the AI's tactical charge - <c>SprintAiChargeKeepPercent</c>.</summary>
        Charge,

        /// <summary>Step 36: advance ("engage", F1 F4), the AI general's own advance - <c>SprintAiAdvanceKeepPercent</c>.</summary>
        Advance,

        /// <summary>Step 36: a plain move to a position (F1 F1, or an AI general's Move that is neither its advance nor its charge) -
        /// <c>SprintAiMoveKeepPercent</c>.</summary>
        Move,

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

        /// <summary>Step 49: a sprint back ended with his walk back (arrived, among his mates, brave again).</summary>
        Regrouped,

        Count,
    }

    /// <summary>Step 33: what a start attempt did.</summary>
    public enum SprintStart
    {
        Started,

        /// <summary>Less than SprintMinStartPercent of the store is filled.</summary>
        BelowMinimum,

        /// <summary>No store at all: an empty bar with no floor (<c>SprintMinSeconds</c> 0).</summary>
        NoStore,

        /// <summary>On a horse - never.</summary>
        Mounted,

        /// <summary>The sprint (or the AI's) is switched off.</summary>
        Off,

        /// <summary>Already sprinting.</summary>
        Running,

        /// <summary>Step 36: an AI order sprint - his store is at or below the order's reserve (SprintAi*KeepPercent), nothing above it to use.</summary>
        ReserveKept,
    }

    /// <summary>
    /// The sprint's settings for one decision (step 33, Anton 2026-10-12 - DESIGN §2 "The sprint"), read live from
    /// <see cref="TraxSettings"/>. A struct, built where needed.
    /// </summary>
    public readonly struct SprintRules
    {
        public SprintRules(bool modEnabled, bool athleticsEnabled, bool sprintEnabled, bool aiEnabled, double pointsPerSecond, int minStartPercent,
            double maxMultiplier, double minMultiplier, int fullSpeedSharePercent, double refillSeconds, double refillMovingFactor,
            double refillExhaustedFactor, int aiChargeKeepPercent, int aiAdvanceKeepPercent, int aiMoveKeepPercent, double minSeconds = 0,
            double topSpeedReachSeconds = 0)
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
            AiChargeKeepPercent = Clamp(aiChargeKeepPercent, 0, 100);
            AiAdvanceKeepPercent = Clamp(aiAdvanceKeepPercent, 0, 100);
            AiMoveKeepPercent = Clamp(aiMoveKeepPercent, 0, 100);
            MinSeconds = double.IsNaN(minSeconds) || minSeconds < 0 ? 0 : minSeconds;
            TopSpeedReachSeconds = double.IsNaN(topSpeedReachSeconds) || topSpeedReachSeconds <= 0 ? 0
                : topSpeedReachSeconds < SprintMath.MinTopSpeedReachSeconds ? SprintMath.MinTopSpeedReachSeconds : topSpeedReachSeconds;
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

        /// <summary>SprintMaxMultiplier, at least 1 - since step 35 × his FRESH (untired) run speed.</summary>
        public double MaxMultiplier { get; }

        /// <summary>SprintMinMultiplier, at least 1 and never above <see cref="MaxMultiplier"/> - × his CURRENT (tired) run speed.</summary>
        public double MinMultiplier { get; }

        public int FullSpeedSharePercent { get; }

        /// <summary>SprintRefillSeconds - empty → full standing at full strength.</summary>
        public double RefillSeconds { get; }

        public double RefillMovingFactor { get; }

        public double RefillExhaustedFactor { get; }

        /// <summary>Step 37: SprintAiChargeKeepPercent - the reserve a charge leaves, % of his FULL store; he sprints all above it.</summary>
        public int AiChargeKeepPercent { get; }

        /// <summary>Step 37: SprintAiAdvanceKeepPercent - the reserve an advance leaves.</summary>
        public int AiAdvanceKeepPercent { get; }

        /// <summary>Step 37: SprintAiMoveKeepPercent - the reserve a move leaves.</summary>
        public int AiMoveKeepPercent { get; }

        /// <summary>Step 35, SprintMinSeconds: the store is never smaller than this (an exhausted man still has a dodge); 0 = no floor.</summary>
        public double MinSeconds { get; }

        /// <summary>Step 35, SprintTopSpeedReachSeconds: while sprinting his TopSpeedReachDuration is at most this; 0 = the game's own.</summary>
        public double TopSpeedReachSeconds { get; }

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
                s.SprintRefillExhaustedFactor, s.SprintAiChargeKeepPercent, s.SprintAiAdvanceKeepPercent, s.SprintAiMoveKeepPercent, s.SprintMinSeconds,
                s.SprintTopSpeedReachSeconds);

        /// <summary>The settings sentence of the mission-start line, a change and the summary.</summary>
        public string Describe(string keyName)
        {
            if (!Enabled) return "OFF (" + OffBecause + ") - nobody sprints";
            var ci = CultureInfo.InvariantCulture;
            double full = Math.Max(MinSeconds, 150 / PointsPerSecond);
            string floor = MinSeconds > 0
                ? ", never below " + MinSeconds.ToString("0.0#", ci) + " s (an empty bar still holds " + MinSeconds.ToString("0.0#", ci) + " s)"
                : ", empty = 0";
            string reach = TopSpeedReachSeconds > 0
                ? "top speed reached in at most " + TopSpeedReachSeconds.ToString("0.0#", ci) + " s while sprinting (the game's own acceleration: ~2.3-2.8 s on foot)"
                : "the game's own acceleration (SprintTopSpeedReachSeconds 0)";
            return "ON - hold " + keyName + " (on foot, in a fight); the store = Athletics points / " + PointsPerSecond.ToString("0.#", ci) + " s (150 points = "
                   + (150 / PointsPerSecond).ToString("0.0#", ci) + " s" + floor + "), a start needs " + MinStartPercent + "% of it; speed x" + MaxMultiplier.ToString("0.00", ci)
                   + " of his FRESH run speed (whatever his Athletics) for the first " + FullSpeedSharePercent + "% of the sprint's length, then a straight line to x"
                   + MinMultiplier.ToString("0.00", ci) + " of his CURRENT run speed (a full " + full.ToString("0.0#", ci) + " s sprint: x" + MaxMultiplier.ToString("0.00", ci)
                   + " until " + (full * FullSpeedShare).ToString("0.0#", ci) + " s); " + reach + "; drains only while moving; refill " + RefillSeconds.ToString("0.#", ci)
                   + " s standing at full strength, x" + RefillMovingFactor.ToString("0.##", ci)
                   + " moving, x" + RefillExhaustedFactor.ToString("0.##", ci) + " at an empty bar (straight line from the peak line), never while sprinting; no Athletics refill "
                   + "while sprinting; AI " + (AiEnabled ? "ON - " + AiOrdersText() : "OFF (SprintAiEnabled)");
        }

        /// <summary>Step 37: "on a NEW order he sprints what he holds above the order's reserve (stored - keep x his full store), ...:
        /// charge keeps 0%, advance keeps 50%, move keeps 80%; ..."</summary>
        public string AiOrdersText() =>
            "on a NEW order he sprints what he holds above the order's reserve (stored - keep x his full store), nothing below "
            + SprintMath.AiMinSeconds.ToString("0.0#", CultureInfo.InvariantCulture) + " s (no " + MinStartPercent + "% start gate for the AI): charge keeps "
            + AiChargeKeepPercent + "%, advance keeps " + AiAdvanceKeepPercent + "%, move keeps " + AiMoveKeepPercent + "%; hold, retreat, fall back, follow none";

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

        /// <summary>SprintTopSpeedReachSeconds' floor when not 0 (the range starts at 0.2).</summary>
        public const double MinTopSpeedReachSeconds = 0.1;

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

        /// <summary>Step 35: the store's size with its floor - max(<paramref name="minSeconds"/>, points ÷ per second).</summary>
        public static double Capacity(double points, double pointsPerSecond, double minSeconds)
        {
            double raw = Capacity(points, pointsPerSecond);
            return minSeconds > raw ? minSeconds : raw;
        }

        /// <summary>This fighter's store size now: his CURRENT points ÷ per second, never below SprintMinSeconds (step 35; the caller
        /// gates on Enabled).</summary>
        public static double Capacity(in SprintRules s, in AthleticsRules r, Fighter f) =>
            Capacity(PointsOf(in r, f), s.PointsPerSecond, s.MinSeconds);

        /// <summary>The store his points alone would give (no floor).</summary>
        public static double PointsCapacity(in SprintRules s, in AthleticsRules r, Fighter f) => Capacity(PointsOf(in r, f), s.PointsPerSecond);

        /// <summary>Step 35: the floor holds his store up now (his points alone would store less than SprintMinSeconds).</summary>
        public static bool OnFloor(in SprintRules s, in AthleticsRules r, Fighter f) =>
            s.MinSeconds > 0 && PointsCapacity(in s, in r, f) < s.MinSeconds - 1e-9;

        private static double PointsOf(in AthleticsRules r, Fighter f) => f.Fraction * AthleticsMath.PoolPoints(in r, f);

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

        /// <summary>Step 36: an AI order sprint shorter than this is not started (float dust, a twitch). Plumbing.</summary>
        public const double AiMinSeconds = 0.2;

        /// <summary>Step 37: a new order of this kind sprints at all (charge, advance, move - never hold, retreat, fall back, follow).</summary>
        public static bool AiSprints(SprintOrder order) => order == SprintOrder.Charge || order == SprintOrder.Advance || order == SprintOrder.Move;

        /// <summary>Step 36: the share of his FULL store an order of <paramref name="order"/> leaves stored (0..1).</summary>
        public static double AiKeep(in SprintRules s, SprintOrder order) => order switch
        {
            SprintOrder.Charge => s.AiChargeKeepPercent / 100.0,
            SprintOrder.Advance => s.AiAdvanceKeepPercent / 100.0,
            SprintOrder.Move => s.AiMoveKeepPercent / 100.0,
            _ => 0,
        };

        /// <summary>
        /// Step 37 (Anton 2026-10-12, one number per order): the seconds an AI man sprints on a new order = <paramref name="stored"/> -
        /// keep x <paramref name="capacity"/> - everything he holds above the order's reserve - 0 when that is below
        /// <see cref="AiMinSeconds"/> (and for an order that never sprints). Keeps 0 / 50 / 80: a charge sprints all of it; an advance
        /// from 100% → 50%, 90% → 40%, 60% → 10%, 45% none; a move from 100% → 20%, 90% → 10%, 60% none.
        /// </summary>
        public static double AiAmount(in SprintRules s, SprintOrder order, double stored, double capacity)
        {
            if (!AiSprints(order) || double.IsNaN(capacity) || capacity <= 0 || double.IsNaN(stored)) return 0;
            if (stored > capacity) stored = capacity;
            double amount = stored - AiKeep(in s, order) * capacity;
            return amount >= AiMinSeconds - 1e-9 ? amount : 0;
        }

        /// <summary>Step 36: the reserve alone stops this sprint (his store at or within <see cref="AiMinSeconds"/> of the keep share).</summary>
        public static bool AiReserveStops(in SprintRules s, SprintOrder order, double stored, double capacity)
        {
            double keep = AiKeep(in s, order);
            if (!(keep > 0) || double.IsNaN(capacity) || capacity <= 0) return false;
            if (stored > capacity) stored = capacity;
            return stored - keep * capacity < AiMinSeconds - 1e-9;
        }

        /// <summary>A formation's order kind (step 23's read) → its sprint share group.</summary>
        public static SprintOrder Group(BraceOrderKind kind) => kind switch
        {
            BraceOrderKind.Charge => SprintOrder.Charge,
            BraceOrderKind.ChargeToTarget => SprintOrder.Charge,
            BraceOrderKind.AttackEntity => SprintOrder.Charge,
            BraceOrderKind.AiCharge => SprintOrder.Charge,
            BraceOrderKind.Advance => SprintOrder.Advance,
            BraceOrderKind.AiAdvance => SprintOrder.Advance,
            BraceOrderKind.Move => SprintOrder.Move,
            _ => SprintOrder.None,
        };

        /// <summary>
        /// Step 35 (Anton 2026-10-12): the RUN FACTOR the stat decorator writes on his run property <paramref name="elapsed"/> seconds
        /// (of moving) into a sprint <paramref name="length"/> seconds long, as a multiple of his FRESH (untired) run speed - it
        /// REPLACES his tiredness run multiplier <paramref name="run"/> while he sprints. SprintMaxMultiplier for the first
        /// SprintFullSpeedSharePercent of the length (whatever his Athletics), then a straight line down to SprintMinMultiplier ×
        /// <paramref name="run"/> (his CURRENT run) at the end. A fresh man at 2.0 / 1.1: 2.0 → 1.1; an exhausted one (run 0.3):
        /// 2.0 → 0.33. A zero length = the start speed.
        /// </summary>
        public static double RunFactor(in SprintRules s, double elapsed, double length, double run)
        {
            if (double.IsNaN(run) || run <= 0) run = 1;
            if (!(length > 0)) return s.MaxMultiplier;
            double full = s.FullSpeedShare * length;
            if (elapsed <= full) return s.MaxMultiplier;
            double end = s.MinMultiplier * run;
            double rest = length - full;
            double t = rest > 0 ? (elapsed - full) / rest : 1;
            if (t > 1) t = 1;
            return s.MaxMultiplier + (end - s.MaxMultiplier) * t;
        }

        /// <summary>A FRESH man's profile (run 1): SprintMax → SprintMin × his run speed. Full 7.5 s at 1.6 / 1.1 / 30%: 1.6 until
        /// 2.25 s, 1.35 at 4.875 s, 1.1 at 7.5 s.</summary>
        public static double Multiplier(in SprintRules s, double elapsed, double length) => RunFactor(in s, elapsed, length, 1.0);

        /// <summary>The run factor as applied: rounded to the nearest <see cref="Step"/> (each step one recompute), never below one
        /// step (an exhausted man's fade ends below 1 - step 35).</summary>
        public static float Quantize(double m)
        {
            if (double.IsNaN(m) || m <= Step) return Step;
            float q = (float)(Math.Round(m / Step, MidpointRounding.AwayFromZero) * Step);
            return q < Step ? Step : (float)Math.Round(q, 2);
        }

        /// <summary>Step 35: his TopSpeedReachDuration while sprinting - min(the game's, <paramref name="ours"/>); ours 0 (or none) = the
        /// game's.</summary>
        public static float TopSpeedReach(float game, double ours)
        {
            if (double.IsNaN(ours) || ours <= 0) return game;
            if (ours < MinTopSpeedReachSeconds) ours = MinTopSpeedReachSeconds;
            return game > ours ? (float)ours : game;
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
            SprintOrder.Advance => "advance",
            SprintOrder.Move => "move",
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
            SprintEnd.Regrouped => "back with his group",
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

        /// <summary>Step 49: a timid man's sprint back to his group (its own reserve; it does not end at 3 m from an enemy - he is getting AWAY).</summary>
        public bool Regroup { get; private set; }

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
            if (!(capacity > SprintMath.EmptySeconds)) return SprintStart.NoStore; // step 35: with a floor there is always a store
            if (!(Stored > SprintMath.EmptySeconds)) return SprintStart.BelowMinimum;
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
            Regroup = false;
            return SprintStart.Started;
        }

        /// <summary>
        /// Step 36 / 37: an AI order sprint - <see cref="SprintMath.AiAmount"/> seconds (all he holds above the order's reserve), no
        /// SprintMinStartPercent gate (the reserve replaces it). Off: the switches or an order that never sprints; ReserveKept: his store is at or
        /// below the order's keep share; BelowMinimum: less than <see cref="SprintMath.AiMinSeconds"/> to sprint.
        /// </summary>
        public SprintStart TryStartAi(in SprintRules s, double now, double capacity, SprintOrder order)
        {
            if (Active) return SprintStart.Running;
            if (!s.AiOn || !SprintMath.AiSprints(order)) return SprintStart.Off;
            Clamp(capacity);
            if (!(capacity > SprintMath.EmptySeconds)) return SprintStart.NoStore;
            double length = SprintMath.AiAmount(in s, order, Stored, capacity);
            if (!(length > 0))
                return SprintMath.AiReserveStops(in s, order, Stored, capacity) ? SprintStart.ReserveKept : SprintStart.BelowMinimum;
            Active = true;
            Length = length;
            Elapsed = 0;
            Since = now;
            StoredAtStart = Stored;
            Order = order;
            ByKey = false;
            Regroup = false;
            return SprintStart.Started;
        }

        /// <summary>
        /// Step 49: a timid man's sprint back - <paramref name="length"/> seconds (CourageMath.SprintAmount: what he holds above the courage
        /// reserve, or the panic one), filed as a move order's sprint. Off: SprintAiEnabled off; ReserveKept: nothing above the reserve.
        /// </summary>
        public SprintStart TryStartRegroup(in SprintRules s, double now, double capacity, double length)
        {
            if (Active) return SprintStart.Running;
            if (!s.AiOn) return SprintStart.Off;
            Clamp(capacity);
            if (!(capacity > SprintMath.EmptySeconds)) return SprintStart.NoStore;
            if (!(length > 0)) return SprintStart.ReserveKept;
            Active = true;
            Length = length > Stored ? Stored : length;
            Elapsed = 0;
            Since = now;
            StoredAtStart = Stored;
            Order = SprintOrder.Move;
            ByKey = false;
            Regroup = true;
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

        /// <summary>A fresh man's speed multiplier now (1 when no sprint runs).</summary>
        public double Multiplier(in SprintRules s) => Active ? SprintMath.Multiplier(in s, Elapsed, Length) : 1.0;

        /// <summary>Step 35: the run factor to write now for a man whose tiredness run multiplier is <paramref name="run"/> (his run when
        /// no sprint runs) - <see cref="SprintMath.RunFactor"/>.</summary>
        public double RunFactor(in SprintRules s, double run) => Active ? SprintMath.RunFactor(in s, Elapsed, Length, run) : run;

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

        /// <summary>The run factor applied now, × his FRESH run speed (his tiredness run multiplier when not sprinting).</summary>
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
        private readonly int[] _aiReserve = new int[(int)SprintOrder.Count];
        private readonly int[] _ordersSeen = new int[(int)SprintOrder.Count];
        private MeanStd _reachGame;
        private MeanStd _reachOurs;
        private MeanStd _askedSpeed;
        private MeanStd _engineSpeed;
        private double _engineMax;
        private int _farOff;

        public int YouStarted;
        public int YouRefusedBelowMinimum;
        public int YouRefusedEmpty;
        public int YouRefusedMounted;

        /// <summary>Step 35: sprints started while the store's floor (SprintMinSeconds) held it up - yours, the AI's.</summary>
        public int YouOffFloor;

        public int AiOffFloor;
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

        /// <summary>Step 36: a new order found his store at or below its reserve - no sprint.</summary>
        public void AddAiReserveKept(SprintOrder o) => _aiReserve[(int)o]++;

        public int AiRefused(SprintOrder o) => _aiRefused[(int)o];

        public int AiReserveKept(SprintOrder o) => _aiReserve[(int)o];

        public int AiReserveKeptTotal
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _aiReserve.Length; i++) n += _aiReserve[i];
                return n;
            }
        }

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

        /// <summary>Step 35: a sprint start's acceleration - the game's TopSpeedReachDuration and ours.</summary>
        public void AddReach(double game, double ours)
        {
            if (double.IsNaN(game) || double.IsNaN(ours) || game <= 0 || ours <= 0) return;
            _reachGame.Add(game);
            _reachOurs.Add(ours);
        }

        public int ReachSamples => _reachGame.Count;

        public double ReachGameMean => _reachGame.Mean;

        public double ReachOursMean => _reachOurs.Mean;

        /// <summary>A sample while sprinting: the run factor asked (× his FRESH run speed - step 35) and the engine's top speed ÷ his
        /// fresh top (his top at the sprint's start ÷ his run multiplier then).</summary>
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
                      + (s.MinSeconds > 0 ? "; " + YouOffFloor + " started off the " + F1(s.MinSeconds) + " s floor (his points alone stored less)" : string.Empty)
                      + "; refused: below " + s.MinStartPercent + "% stored " + YouRefusedBelowMinimum + ", an empty bar " + YouRefusedEmpty + ", on a horse " + YouRefusedMounted
                      + "; ends: " + EndsText(true));
            lines.Add("sprint - AI: new orders seen - charge " + OrdersSeen(SprintOrder.Charge) + ", advance " + OrdersSeen(SprintOrder.Advance) + ", move "
                      + OrdersSeen(SprintOrder.Move) + ", other " + OrdersSeen(SprintOrder.None) + "; sprints - " + KindText(SprintOrder.Charge, in s) + ", "
                      + KindText(SprintOrder.Advance, in s) + ", " + KindText(SprintOrder.Move, in s)
                      + "; not sprinted: reserve kept " + AiReserveKeptTotal + " (charge " + AiReserveKept(SprintOrder.Charge) + ", advance "
                      + AiReserveKept(SprintOrder.Advance) + ", move " + AiReserveKept(SprintOrder.Move) + "), less than "
                      + SprintMath.AiMinSeconds.ToString("0.0#", ci) + " s or empty: charge " + AiRefused(SprintOrder.Charge) + ", advance " + AiRefused(SprintOrder.Advance)
                      + ", move " + AiRefused(SprintOrder.Move) + (s.MinSeconds > 0 ? "; off the " + F1(s.MinSeconds) + " s floor " + AiOffFloor : string.Empty)
                      + "; mounted " + AiSkippedMounted + ", yours to drive " + AiSkippedPlayer
                      + "; ends: " + EndsText(false) + "; melee checks " + MeleeChecks.ToString(ci));
            string verdict = EngineFollows == null ? "too few samples to judge (20 needed)"
                : EngineFollows == true ? "the engine's top speed follows the sprint - no cap above the run speed"
                : "the engine's top speed does NOT follow (a native cap?) - tell Claude";
            string reach = s.TopSpeedReachSeconds <= 0 ? "acceleration: the game's own (SprintTopSpeedReachSeconds 0)"
                : ReachSamples == 0 ? "acceleration: at most " + F2(s.TopSpeedReachSeconds) + " s while sprinting (no start with the game's value seen)"
                : "acceleration while sprinting: the game's top-speed reach avg " + F2(ReachGameMean) + " s → ours " + F2(ReachOursMean) + " s (sprint starts: " + ReachSamples + ")";
            lines.Add("sprint speed check (x his FRESH run speed = his top at the sprint's start ÷ his run multiplier then, while it ran): " + SpeedSamples
                      + " samples - asked avg x" + F2(AskedMean)
                      + ", engine top avg x" + F2(EngineMean) + ", max x" + F2(_engineMax) + ", off by more than 0.05: " + _farOff + " - " + verdict + "; " + reach
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

        /// <summary>Step 37: "move (keeps 80%) 12 men (9.6 s in all, avg 0.80 s)".</summary>
        private string KindText(SprintOrder o, in SprintRules s) =>
            SprintMath.OrderName(o) + " (keeps " + F0(100 * SprintMath.AiKeep(in s, o)) + "%) " + AiStarted(o)
            + " men (" + F1(AiSeconds(o)) + " s in all" + Avg(AiSeconds(o), AiStarted(o)) + ")";

        private static string F0(double v) => v.ToString("0", CultureInfo.InvariantCulture);

        private static string Avg(double sum, int n) => n > 0 ? ", avg " + F2(sum / n) + " s" : string.Empty;

        private static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        private static string F2(double v) => double.IsNaN(v) ? "n/a" : v.ToString("0.00", CultureInfo.InvariantCulture);
    }
}

using System;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>Step 43: where an AI man's fever is.</summary>
    public enum FeverPhase
    {
        /// <summary>No fever - his decisions are the CALM ones (AiCalmFactor).</summary>
        None,

        /// <summary>A new charge / advance order armed it (full); no enemy within FeverContactMetres yet - the peak, the clock not running.</summary>
        Armed,

        /// <summary>He met an enemy: the store burns (faster with a shield in hand) and the aggression fades with it.</summary>
        Burning,
    }

    /// <summary>Step 43: why a fever ended.</summary>
    public enum FeverEnd
    {
        /// <summary>The store burned down to 0 - he is calm again.</summary>
        Faded,

        /// <summary>A new order that is neither a charge nor an advance (hold, move, retreat, stop, follow …) - the general called them back.</summary>
        OrderEnded,

        /// <summary>The master switch, AthleticsEnabled or FeverEnabled off.</summary>
        SwitchedOff,

        /// <summary>The player took him (or he became the player's).</summary>
        PlayerControl,

        /// <summary>He mounted a horse (riders keep the game's own decisions).</summary>
        Mounted,

        /// <summary>He fell, fled or left the field.</summary>
        LeftField,

        /// <summary>The battle ended with the fever running.</summary>
        MissionEnd,

        /// <summary>An exception on his fever (logged) - ended to stay safe.</summary>
        Error,

        Count,
    }

    /// <summary>Step 43: what an order did to a man's fever.</summary>
    public enum FeverArm
    {
        /// <summary>A new fever, full.</summary>
        Armed,

        /// <summary>A different aggressive order (advance → charge, charge → advance) on a running fever: a fresh fever was larger than
        /// what was left - re-armed to it (back to the peak).</summary>
        Rearmed,

        /// <summary>A different aggressive order on a running fever with MORE left than a fresh one - kept as it is.</summary>
        KeptLarger,

        /// <summary>The same kind of order again (charge → charge to a target) - a running fever is not re-armed.</summary>
        Repeat,

        /// <summary>No Athletics points - nothing to burn.</summary>
        NoStore,

        /// <summary>The fever is switched off (or the master switch / Athletics).</summary>
        Off,

        /// <summary>Not a charge / advance order.</summary>
        NotAggressive,
    }

    /// <summary>Step 43 (Anton's change, 2026-10-17): why one fever-sprint check did not start a sprint.</summary>
    public enum FeverSprintRefusal
    {
        /// <summary>The roll missed - chance = FeverSprintChancePercent × fever left ÷ full.</summary>
        MissedRoll,

        /// <summary>His sprint store is at or below his order's reserve (step 37's SprintAi*KeepPercent).</summary>
        ReserveKept,

        /// <summary>No sprint store at all.</summary>
        NoStore,

        /// <summary>Already sprinting.</summary>
        Sprinting,

        /// <summary>An enemy already within FeverSprintMinMetres - in melee.</summary>
        InMelee,

        /// <summary>No enemy within FeverSprintMaxMetres.</summary>
        NoEnemyInRange,

        /// <summary>SprintEnabled or SprintAiEnabled off.</summary>
        SprintOff,

        Count,
    }

    /// <summary>
    /// Step 43 - CALM AND FEVER (Anton, 2026-10-17: "people are defensive by nature … when the general turns to them and says NOW!,
    /// everybody surges … the surge fades"; AI_NOTES "Step 43", DESIGN §2 "Calm and fever"). The settings for one decision, read live
    /// from <see cref="TraxSettings"/>: the master switch first, then AthleticsEnabled (the fever's store IS his Athletics, and the calm
    /// is the brake beside tiredness - Athletics off = the game's own AI), then the calm factor and the fever's switch.
    /// </summary>
    public readonly struct FeverRules
    {
        public FeverRules(bool modEnabled, bool athleticsEnabled, bool feverEnabled, double calmFactor, double pointsPerSecond, double aggressionMultiplier,
            double contactMetres, double shieldDurationMultiplier, bool sprintToClose, double sprintMinMetres, double sprintMaxMetres,
            double sprintCheckSeconds, int sprintChancePercent)
        {
            ModEnabled = modEnabled;
            AthleticsEnabled = athleticsEnabled;
            FeverEnabled = feverEnabled;
            CalmFactor = double.IsNaN(calmFactor) || calmFactor < 1 ? 1 : calmFactor;
            PointsPerSecond = double.IsNaN(pointsPerSecond) || pointsPerSecond < FeverMath.MinPointsPerSecond ? FeverMath.MinPointsPerSecond : pointsPerSecond;
            AggressionMultiplier = double.IsNaN(aggressionMultiplier) || aggressionMultiplier < 1 ? 1 : aggressionMultiplier;
            ContactMetres = double.IsNaN(contactMetres) || contactMetres < FeverMath.MinMetres ? FeverMath.MinMetres : contactMetres;
            ShieldDurationMultiplier = double.IsNaN(shieldDurationMultiplier) || shieldDurationMultiplier < FeverMath.MinShieldDuration
                ? FeverMath.MinShieldDuration : shieldDurationMultiplier;
            SprintToClose = sprintToClose;
            SprintMinMetres = double.IsNaN(sprintMinMetres) || sprintMinMetres < 0 ? 0 : sprintMinMetres;
            double max = double.IsNaN(sprintMaxMetres) || sprintMaxMetres < FeverMath.MinMetres ? FeverMath.MinMetres : sprintMaxMetres;
            SprintMaxMetres = max < SprintMinMetres ? SprintMinMetres : max;
            SprintCheckSeconds = double.IsNaN(sprintCheckSeconds) || sprintCheckSeconds < FeverMath.MinCheckSeconds ? FeverMath.MinCheckSeconds : sprintCheckSeconds;
            SprintChancePercent = sprintChancePercent < 0 ? 0 : sprintChancePercent > 100 ? 100 : sprintChancePercent;
        }

        public bool ModEnabled { get; }
        public bool AthleticsEnabled { get; }

        /// <summary>FeverEnabled (the switch itself).</summary>
        public bool FeverEnabled { get; }

        /// <summary>AiCalmFactor, at least 1: the AI's attack-decision chances are divided by it (1 = the game's own).</summary>
        public double CalmFactor { get; }

        /// <summary>FeverPointsPerSecond: the fever's store = his CURRENT Athletics points ÷ this, in seconds.</summary>
        public double PointsPerSecond { get; }

        /// <summary>FeverAggressionMultiplier, at least 1: the calm values × this at the fever's peak.</summary>
        public double AggressionMultiplier { get; }

        /// <summary>FeverContactMetres: an enemy this close starts the burn (and counts as "in contact" for the summary).</summary>
        public double ContactMetres { get; }

        /// <summary>FeverShieldDurationMultiplier: with a shield in hand the store burns 1 ÷ this fast.</summary>
        public double ShieldDurationMultiplier { get; }

        public bool SprintToClose { get; }
        public double SprintMinMetres { get; }
        public double SprintMaxMetres { get; }
        public double SprintCheckSeconds { get; }
        public int SprintChancePercent { get; }

        /// <summary>The calm applies: master switch, Athletics, a factor above 1.</summary>
        public bool CalmOn => ModEnabled && AthleticsEnabled && CalmFactor > 1 + 1e-9;

        /// <summary>The fever applies: master switch, Athletics, FeverEnabled.</summary>
        public bool Enabled => ModEnabled && AthleticsEnabled && FeverEnabled;

        /// <summary>Feverish men may sprint to close (the sprint's own switches are checked at the start).</summary>
        public bool SprintOn => Enabled && SprintToClose && SprintChancePercent > 0;

        /// <summary>The switch that holds the fever off (the master switch first), or null.</summary>
        public string? OffBecause => !ModEnabled ? "ModEnabled" : !AthleticsEnabled ? "AthleticsEnabled" : !FeverEnabled ? "FeverEnabled" : null;

        /// <summary>The switch that holds the calm off, or null.</summary>
        public string? CalmOffBecause => !ModEnabled ? "ModEnabled" : !AthleticsEnabled ? "AthleticsEnabled" : CalmFactor <= 1 + 1e-9 ? "AiCalmFactor 1" : null;

        public static FeverRules From(TraxSettings s) => new FeverRules(s.ModEnabled, s.AthleticsEnabled, s.FeverEnabled, s.AiCalmFactor,
            s.FeverPointsPerSecond, s.FeverAggressionMultiplier, s.FeverContactMetres, s.FeverShieldDurationMultiplier, s.FeverSprintToClose,
            s.FeverSprintMinMetres, s.FeverSprintMaxMetres, s.FeverSprintCheckSeconds, s.FeverSprintChancePercent);

        /// <summary>The settings sentence for the mission-start line, a change and the summary.</summary>
        public string Describe()
        {
            string calm = CalmOn
                ? "calm: AI men on foot decide to attack " + FeverMath.F2(CalmFactor) + "x less often (AiCalmFactor - the game's attack and riposte chances ÷ "
                  + FeverMath.F2(CalmFactor) + ", e.g. a charge's attack chance 0.144 → " + FeverMath.F3(0.144 / CalmFactor) + ")"
                : "calm: off (" + CalmOffBecause + ") - the game's own attack decisions";
            if (!Enabled) return calm + "; fever: off (" + OffBecause + ")";
            double peak = AggressionMultiplier / (CalmOn ? CalmFactor : 1);
            string fever = "; fever: a NEW charge / advance order arms each AI man on foot with his CURRENT Athletics points / " + FeverMath.F1(PointsPerSecond)
                + " s (FeverPointsPerSecond - 80 points = " + FeverMath.F1(80 / PointsPerSecond) + " s) at x" + FeverMath.F2(AggressionMultiplier)
                + " the calm values (FeverAggressionMultiplier: x" + FeverMath.F2(peak) + " the game's - attack chance 0.144 → " + FeverMath.F3(Math.Min(1, 0.144 * peak))
                + "); the clock starts at the first enemy within " + FeverMath.F1(ContactMetres) + " m (FeverContactMetres), burns x" + FeverMath.F2(1 / ShieldDurationMultiplier)
                + " faster with a shield in hand (FeverShieldDurationMultiplier " + FeverMath.F2(ShieldDurationMultiplier) + "), and the aggression fades in a straight line ("
                + FeverMath.FadeSteps + " steps) back to calm as it empties; a hold / move / retreat order ends it";
            string sprint = !SprintOn ? "; fever sprint: off" + (SprintToClose ? " (FeverSprintChancePercent 0)" : " (FeverSprintToClose)")
                : "; fever sprint: every " + FeverMath.F1(SprintCheckSeconds) + " s a feverish man with sprint above his order's reserve and an enemy "
                  + FeverMath.F1(SprintMinMetres) + "-" + FeverMath.F1(SprintMaxMetres) + " m away sprints with chance " + SprintChancePercent
                  + "% x the fever left (full while armed)";
            return calm + fever + sprint;
        }
    }

    /// <summary>Step 43 pure math: the calm divisor, the store, the fade and its steps, the burn rate, the sprint chance.</summary>
    public static class FeverMath
    {
        public const double MinPointsPerSecond = 1;
        public const double MinMetres = 0.5;
        public const double MinShieldDuration = 0.1;
        public const double MinCheckSeconds = 0.1;

        /// <summary>The fade's steps (plumbing): the multiplier moves in tenths of the burn - 1 recompute at the arming, 9 down the
        /// fade, 1 at the end = ~11 recomputes per man per fever (DESIGN §2 "Calm and fever").</summary>
        public const int FadeSteps = 10;

        /// <summary>Every man is looked at about this often (staggered like the brace; plumbing).</summary>
        public const double PollSeconds = 0.25;

        /// <summary>The fever's store in seconds: his CURRENT points ÷ FeverPointsPerSecond (0 for none).</summary>
        public static double Store(double points, double pointsPerSecond)
        {
            if (double.IsNaN(points) || points <= 0) return 0;
            double per = double.IsNaN(pointsPerSecond) || pointsPerSecond < MinPointsPerSecond ? MinPointsPerSecond : pointsPerSecond;
            return points / per;
        }

        public static double Store(in FeverRules r, double points) => Store(points, r.PointsPerSecond);

        /// <summary>What is left as a share of the full store, 0..1.</summary>
        public static double Share(double left, double full)
        {
            if (!(full > 0) || double.IsNaN(left) || left <= 0) return 0;
            return left >= full ? 1 : left / full;
        }

        /// <summary>The share in <see cref="FadeSteps"/> steps, rounded UP: the peak holds the first tenth of the burn, the last step
        /// (0.1) runs until the store is empty - 0 only when empty.</summary>
        public static double QuantizedShare(double left, double full)
        {
            double s = Share(left, full);
            if (s <= 0) return 0;
            double q = Math.Ceiling(s * FadeSteps - 1e-9) / FadeSteps;
            return q > 1 ? 1 : q < 1.0 / FadeSteps ? 1.0 / FadeSteps : q;
        }

        /// <summary>The aggression multiplier on the calm values: none 1; armed the peak; burning 1 + (peak − 1) × left ÷ full in
        /// <see cref="FadeSteps"/> steps (<paramref name="exact"/> = the straight line itself).</summary>
        public static double Multiplier(in FeverRules r, FeverPhase phase, double left, double full, bool exact = false)
        {
            if (phase == FeverPhase.None || !r.Enabled) return 1;
            if (phase == FeverPhase.Armed) return r.AggressionMultiplier;
            double s = exact ? Share(left, full) : QuantizedShare(left, full);
            return 1 + (r.AggressionMultiplier - 1) * s;
        }

        /// <summary>The calm divisor now: AiCalmFactor while the calm applies, else 1.</summary>
        public static double CalmDivisor(in FeverRules r) => r.CalmOn ? r.CalmFactor : 1;

        /// <summary>
        /// The factor on the game's two attack-decision values for him: the fever's multiplier ÷ the calm divisor. 1 = untouched (the
        /// player, the master switch or Athletics off). Calm 3, peak 3: calm 1/3, the peak exactly the game's (1).
        /// </summary>
        public static float DecisionFactor(in FeverRules r, FeverPhase phase, double left, double full, bool isPlayer)
        {
            if (isPlayer || !r.ModEnabled || !r.AthleticsEnabled) return 1f;
            double f = Multiplier(in r, phase, left, full) / CalmDivisor(in r);
            return (float)f;
        }

        /// <summary>A chance × the factor, clamped 0..1 (NaN or factor 1 = the value untouched). No floor of ours: the game's own
        /// 0.05 clamp is its formula's; calm 3 must mean three times rarer.</summary>
        public static float ApplyChance(float value, float factor)
        {
            if (factor == 1f || float.IsNaN(factor) || float.IsNaN(value)) return value;
            float v = value * factor;
            return v < 0f ? 0f : v > 1f ? 1f : v;
        }

        /// <summary>Seconds of store burned per second: 1, or 1 ÷ FeverShieldDurationMultiplier with a shield in hand.</summary>
        public static double BurnRate(in FeverRules r, bool shieldInHand) => shieldInHand ? 1 / r.ShieldDurationMultiplier : 1;

        /// <summary>A charge or an advance - the orders that arm a fever (a move, hold, retreat … never).</summary>
        public static bool Aggressive(SprintOrder order) => order == SprintOrder.Charge || order == SprintOrder.Advance;

        /// <summary>The order group a formation's kind arms (Charge / Advance), else <see cref="SprintOrder.None"/>.</summary>
        public static SprintOrder Group(BraceOrderKind kind)
        {
            var g = SprintMath.Group(kind);
            return Aggressive(g) ? g : SprintOrder.None;
        }

        /// <summary>The fever-sprint chance at one check: FeverSprintChancePercent × left ÷ full (the exact line; full while armed).</summary>
        public static double SprintChance(in FeverRules r, FeverPhase phase, double left, double full)
        {
            if (!r.SprintOn || phase == FeverPhase.None) return 0;
            double s = phase == FeverPhase.Armed ? 1 : Share(left, full);
            return r.SprintChancePercent / 100.0 * s;
        }

        /// <summary>One roll: <paramref name="roll"/> (uniform 0..1) below the chance starts a sprint.</summary>
        public static bool SprintRoll(double chance, double roll) => chance > 0 && roll < chance;

        /// <summary>The summary's bands of the fever left at a sprint: 0 = 100-67%, 1 = 67-33%, 2 = 33-0%.</summary>
        public const int ShareBands = 3;

        public static int ShareBand(double share) => share > 2.0 / 3 ? 0 : share > 1.0 / 3 ? 1 : 2;

        public static string ShareBandName(int band) => band == 0 ? "100-67%" : band == 1 ? "67-33%" : "33-0%";

        public static string EndName(FeverEnd e) => e switch
        {
            FeverEnd.Faded => "faded",
            FeverEnd.OrderEnded => "a hold / move / retreat order",
            FeverEnd.SwitchedOff => "switched off",
            FeverEnd.PlayerControl => "player control",
            FeverEnd.Mounted => "mounted",
            FeverEnd.LeftField => "left the field",
            FeverEnd.MissionEnd => "battle ended",
            FeverEnd.Error => "error",
            _ => e.ToString(),
        };

        public static string RefusalName(FeverSprintRefusal r) => r switch
        {
            FeverSprintRefusal.MissedRoll => "missed the roll",
            FeverSprintRefusal.ReserveKept => "reserve kept",
            FeverSprintRefusal.NoStore => "no store",
            FeverSprintRefusal.Sprinting => "sprinting already",
            FeverSprintRefusal.InMelee => "in melee",
            FeverSprintRefusal.NoEnemyInRange => "no enemy in range",
            FeverSprintRefusal.SprintOff => "sprint off",
            _ => r.ToString(),
        };

        internal static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        internal static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        internal static string F3(double v) => v.ToString("0.000", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Step 43: one AI man's fever - armed at a new charge / advance order (his CURRENT points ÷ FeverPointsPerSecond seconds), burning
    /// from his first enemy within FeverContactMetres (× 1 ÷ FeverShieldDurationMultiplier while a shield is in his hand), fading to
    /// calm as it empties. One per tracked fighter, reused all battle - no allocation per tick. Main thread.
    /// </summary>
    public sealed class FeverStore
    {
        public FeverPhase Phase { get; private set; }

        /// <summary>The store at its arming (or its last re-arm), seconds.</summary>
        public double Full { get; private set; }

        /// <summary>What is left, seconds of store.</summary>
        public double Left { get; private set; }

        /// <summary>The order that armed it (Charge / Advance).</summary>
        public SprintOrder Order { get; private set; } = SprintOrder.None;

        /// <summary>The player's orders armed it (his formation is under the player's command), not the AI general's.</summary>
        public bool ByPlayer { get; private set; }

        public double ArmedAt { get; private set; }
        public double BurnStartAt { get; private set; }

        /// <summary>Real seconds of this burn with a shield in hand / without.</summary>
        public double BurnShieldSeconds { get; private set; }
        public double BurnPlainSeconds { get; private set; }

        /// <summary>The store when the burn began (seconds).</summary>
        public double FullAtBurn { get; private set; }

        /// <summary>A shield taken up or put away during this burn.</summary>
        public bool ShieldSwitched { get; private set; }

        private int _lastShield = -1;

        /// <summary>Decision-factor recomputes asked during this fever (the logic counts).</summary>
        public int Recomputes;

        /// <summary>The next fever-sprint check (mission time).</summary>
        public double NextSprintCheck;

        /// <summary>Fever sprints started during this fever.</summary>
        public int Sprints;

        public bool Running => Phase != FeverPhase.None;

        public double Share => FeverMath.Share(Left, Full);

        /// <summary>A new order of <paramref name="order"/> for him with <paramref name="points"/> Athletics now.</summary>
        public FeverArm Arm(in FeverRules r, SprintOrder order, double points, double now, bool byPlayer)
        {
            if (!r.Enabled) return FeverArm.Off;
            if (!FeverMath.Aggressive(order)) return FeverArm.NotAggressive;
            double fresh = FeverMath.Store(in r, points);
            if (Running)
            {
                if (order == Order) return FeverArm.Repeat;
                Order = order;
                ByPlayer = byPlayer;
                if (fresh > Left)
                {
                    Full = fresh;
                    Left = fresh;
                    if (Phase == FeverPhase.Burning) FullAtBurn = fresh;
                    return FeverArm.Rearmed;
                }
                return FeverArm.KeptLarger;
            }
            if (!(fresh > 0)) return FeverArm.NoStore;
            Phase = FeverPhase.Armed;
            Full = Left = fresh;
            Order = order;
            ByPlayer = byPlayer;
            ArmedAt = now;
            BurnStartAt = double.NaN;
            BurnShieldSeconds = BurnPlainSeconds = 0;
            FullAtBurn = 0;
            ShieldSwitched = false;
            _lastShield = -1;
            Recomputes = 0;
            NextSprintCheck = now;
            Sprints = 0;
            return FeverArm.Armed;
        }

        /// <summary>His first enemy within reach: the clock starts. False when not armed.</summary>
        public bool StartBurn(double now)
        {
            if (Phase != FeverPhase.Armed) return false;
            Phase = FeverPhase.Burning;
            BurnStartAt = now;
            FullAtBurn = Left;
            return true;
        }

        /// <summary><paramref name="dt"/> seconds of burning, the shield read live. True when the store is empty (faded).</summary>
        public bool Burn(in FeverRules r, double dt, bool shieldInHand)
        {
            if (Phase != FeverPhase.Burning) return false;
            if (dt > 0)
            {
                int s = shieldInHand ? 1 : 0;
                if (_lastShield >= 0 && _lastShield != s) ShieldSwitched = true;
                _lastShield = s;
                if (shieldInHand) BurnShieldSeconds += dt;
                else BurnPlainSeconds += dt;
                Left -= dt * FeverMath.BurnRate(in r, shieldInHand);
            }
            if (Left <= 1e-9)
            {
                Left = 0;
                return true;
            }
            return false;
        }

        /// <summary>A fever-sprint check is due now (every FeverSprintCheckSeconds from the arming, read live): true once per period,
        /// the next one scheduled.</summary>
        public bool SprintDue(in FeverRules r, double now)
        {
            if (!Running || now < NextSprintCheck) return false;
            NextSprintCheck = now + r.SprintCheckSeconds;
            return true;
        }

        /// <summary>The fever ends (every path).</summary>
        public void Stop()
        {
            Phase = FeverPhase.None;
            Left = 0;
        }
    }
}

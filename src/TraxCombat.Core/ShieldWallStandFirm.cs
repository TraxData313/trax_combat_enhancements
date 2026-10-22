using System;
using System.Globalization;
using System.Text;

namespace TraxCombat.Core
{
    /// <summary>Step 75: why a man step 41 looks at does or does not STAND FIRM now - in gate order (the master switch first).</summary>
    public enum StandFirmVerdict
    {
        /// <summary>He stands firm: held by step 41, his formation on a Stop, or on a Move that has reached its place.</summary>
        Stands,

        /// <summary>The master switch (ModEnabled) is off.</summary>
        ModOff,

        /// <summary>ShieldWallHoldsSlots is off (nobody is held - nothing to stand firm).</summary>
        HoldOff,

        /// <summary>ShieldWallStandFirm is off.</summary>
        SwitchedOff,

        /// <summary>Not held by step 41 now (another arrangement, a charge or a retreat, the escape, the wall broke ...).</summary>
        NotHeld,

        /// <summary>His formation's order is not a hold-type order: advance (yours or the AI's), the AI's charge, fall back, follow.</summary>
        NotAHoldOrder,

        /// <summary>A Move that has not reached its place yet (farther than ShieldWallStandFirmArrivedMetres or still moving).</summary>
        NotArrived,

        Count,
    }

    /// <summary>Step 75: what tried to take a man standing firm out of his slot - each refused (the [summary] "wall stands firm" line).</summary>
    public enum StandFirmLever
    {
        /// <summary>Step 42's leash would have freed him to step out at an enemy close in front (the leash is 0 for him).</summary>
        StepOut,

        /// <summary>A running AI sprint (an order's, a fever's) - ended.</summary>
        Sprint,

        /// <summary>Step 68's advance-as-one frame would have sent him toward the game's order slot - his slot instead.</summary>
        Pace,

        /// <summary>His own kick / bash or a punish press carried him out - walked straight back.</summary>
        KickOrPunish,

        /// <summary>Anything else carried him out (the native AI's approach, the press, a blow) - walked straight back.</summary>
        Pushed,

        Count,
    }

    /// <summary>Step 75: one man's "at its place" memory for a Move order - latched once his formation stood at its order position, let go when the
    /// order position moves (a new move) or the formation is pushed far from it. A struct in the module's per-man wall state: no allocation.</summary>
    public struct StandFirmLatch
    {
        public bool Arrived;
        public double OrderX, OrderY;
    }

    /// <summary>
    /// Step 75's settings, read live: the master switch FIRST, then step 41's ShieldWallHoldsSlots (a man stands firm only while he is held), then
    /// ShieldWallStandFirm; the slack, the "at its place" numbers and the two engine levers.
    /// </summary>
    public readonly struct StandFirmRules
    {
        public StandFirmRules(bool modEnabled, bool holdsSlots, bool standFirm, double slackMetres, double arrivedMetres, double stillMetresPerSecond,
            bool pinFrame, bool tightTolerance)
        {
            ModEnabled = modEnabled;
            HoldsSlots = holdsSlots;
            StandFirm = standFirm;
            SlackMetres = Clamp(slackMetres, StandFirmMath.MinSlackMetres, StandFirmMath.MaxSlackMetres, 0.3);
            ArrivedMetres = Clamp(arrivedMetres, StandFirmMath.MinArrivedMetres, StandFirmMath.MaxArrivedMetres, 2.0);
            StillMetresPerSecond = Clamp(stillMetresPerSecond, 0, StandFirmMath.MaxStillMetresPerSecond, 0.5);
            PinFrame = pinFrame;
            TightTolerance = tightTolerance;
        }

        public bool ModEnabled { get; }
        public bool HoldsSlots { get; }
        public bool StandFirm { get; }

        /// <summary>A man standing firm more than this ahead of his slot is walked straight back (to within half of it) - and the tolerance handed
        /// to the engine with the "keep your slot" flag (<see cref="TightTolerance"/>).</summary>
        public double SlackMetres { get; }

        /// <summary>A Move is "at its place" once its formation stands within this of its order position ...</summary>
        public double ArrivedMetres { get; }

        /// <summary>... and its men's average speed is at most this (m/s).</summary>
        public double StillMetresPerSecond { get; }

        /// <summary>The engine's walk target (the formation frame) = his slot - closes the game's "stand where you are" frame under a Stop and step
        /// 68's pace.</summary>
        public bool PinFrame { get; }

        /// <summary>The integrity data's deviation (the tolerance the engine is told) = at most <see cref="SlackMetres"/>.</summary>
        public bool TightTolerance { get; }

        public bool Enabled => ModEnabled && HoldsSlots && StandFirm;

        /// <summary>The switch that holds it off (the master switch first), or null when on.</summary>
        public string? OffBecause => !ModEnabled ? "ModEnabled" : !HoldsSlots ? "ShieldWallHoldsSlots" : !StandFirm ? "ShieldWallStandFirm" : null;

        public static StandFirmRules From(TraxSettings s) => new StandFirmRules(s.ModEnabled, s.ShieldWallHoldsSlots, s.ShieldWallStandFirm,
            s.ShieldWallStandFirmSlackMetres, s.ShieldWallStandFirmArrivedMetres, s.ShieldWallStandFirmStillMetresPerSecond,
            s.ShieldWallStandFirmPinFrame, s.ShieldWallStandFirmTightTolerance);

        public bool SameAs(in StandFirmRules o) => ModEnabled == o.ModEnabled && HoldsSlots == o.HoldsSlots && StandFirm == o.StandFirm
                                                   && SlackMetres == o.SlackMetres && ArrivedMetres == o.ArrivedMetres && StillMetresPerSecond == o.StillMetresPerSecond
                                                   && PinFrame == o.PinFrame && TightTolerance == o.TightTolerance;

        /// <summary>The settings in one sentence (the mission-start and change lines, the summary's head).</summary>
        public string Describe()
        {
            if (!Enabled)
                return "OFF (" + OffBecause + ") - a held shield wall / square / circle on a hold order keeps step 41-68's rules (the step out on the leash, the walk back past "
                       + "max(leash, 1) + 0.25 m)";
            return "ON (ShieldWallStandFirm) - an AI man held in a shield wall, square or circle whose formation is on a STOP, or on a MOVE that has reached its place "
                   + "(within " + F1(ArrivedMetres) + " m of its order position, its men moving at most " + F1(StillMetresPerSecond) + " m/s), never leaves his slot: "
                   + "no step out (the leash 0 for him), no sprint, no advance pace; more than " + F2(SlackMetres) + " m ahead of his slot (his own kick / bash, a press, "
                   + "the game's approach) he is walked straight back to within " + F2(SlackMetres / 2) + " m, facing the enemy - he still guards, blocks, swings at and kicks "
                   + "what is within his reach; " + (PinFrame ? "the engine's walk target = his slot" : "the game's own walk target (ShieldWallStandFirmPinFrame off)") + "; "
                   + (TightTolerance ? "the tolerance handed to the engine " + F2(SlackMetres) + " m at most" : "the formation's own tolerance (ShieldWallStandFirmTightTolerance off)")
                   + "; never under an advance, a charge, fall back, follow or a retreat; a broken wall (its morale) and a man far behind still let go; courage, the fear "
                   + "flock and the block hold never touch a held man";
        }

        private static double Clamp(double v, double min, double max, double fallback) =>
            double.IsNaN(v) ? fallback : v < min ? min : v > max ? max : v;

        internal static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
        internal static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Step 75 - A STANDING SHIELD WALL NEVER LEAVES ITS SLOT (Toni 2026-10-22: "the men in the shield wall still step forward even when on a standing
    /// order, they take a few steps to strike at an enemy, then fall back"; AI_NOTES "Step 75", DESIGN decision 69). Pure: which orders are hold-type,
    /// the "at its place" rule for a Move (latched, with its way out), the decision in gate order, the names.
    /// </summary>
    public static class StandFirmMath
    {
        /// <summary>The settings' ranges. Plumbing.</summary>
        public const double MinSlackMetres = 0.1;
        public const double MaxSlackMetres = 1.5;
        public const double MinArrivedMetres = 0.5;
        public const double MaxArrivedMetres = 20;
        public const double MaxStillMetresPerSecond = 3;

        /// <summary>A Move latched "at its place" lets go once its formation stands more than this many times ShieldWallStandFirmArrivedMetres from its
        /// order position (pushed far back - the game walks it home as a line, step 41-68's rules), or the order position itself moves more than
        /// ShieldWallStandFirmArrivedMetres (a new move). Plumbing.</summary>
        public const double LeaveFactor = 2.0;

        /// <summary>The hold-type orders: the game's Stop and a plain Move (yours, or the AI's own outside an advance / a tactical charge).</summary>
        public static bool IsHoldOrder(BraceOrderKind k) => k == BraceOrderKind.Stop || k == BraceOrderKind.Move;

        /// <summary>The formation's distance from its order position (m; NaN = not known).</summary>
        public static double FromPlace(in WallFacts f)
        {
            if (!f.HasFormationOrder) return double.NaN;
            double dx = f.FormationX - f.FormationOrderX, dy = f.FormationY - f.FormationOrderY;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>The formation's men's average speed (m/s - the integrity data's average velocity).</summary>
        public static double Speed(in WallFacts f)
        {
            double s = Math.Sqrt(f.VelX * f.VelX + f.VelY * f.VelY);
            return double.IsNaN(s) ? 0 : s;
        }

        /// <summary>
        /// The ORDER conditions alone (no switch - the log compares ON and OFF): a Stop stands at once; a Move stands once its formation is within
        /// ArrivedMetres of its order position and moving at most StillMetresPerSecond - then LATCHED (a fighting line shuffles) until the order
        /// position moves more than ArrivedMetres (a new move) or the formation is pushed more than <see cref="LeaveFactor"/> x ArrivedMetres away;
        /// any other order clears the latch. <see cref="StandFirmVerdict.Stands"/> = yes.
        /// </summary>
        public static StandFirmVerdict Conditions(in StandFirmRules r, in WallFacts f, ref StandFirmLatch latch)
        {
            if (!IsHoldOrder(f.Order))
            {
                latch = default;
                return StandFirmVerdict.NotAHoldOrder;
            }
            if (f.Order == BraceOrderKind.Stop)
            {
                latch = default;
                return StandFirmVerdict.Stands;
            }
            double d = FromPlace(in f);
            if (double.IsNaN(d))
            {
                latch = default;
                return StandFirmVerdict.NotArrived;
            }
            if (latch.Arrived)
            {
                double mx = f.FormationOrderX - latch.OrderX, my = f.FormationOrderY - latch.OrderY;
                bool newPlace = mx * mx + my * my > r.ArrivedMetres * r.ArrivedMetres;
                if (!newPlace && d <= LeaveFactor * r.ArrivedMetres) return StandFirmVerdict.Stands;
                latch = default;
            }
            if (d <= r.ArrivedMetres && Speed(in f) <= r.StillMetresPerSecond)
            {
                latch = new StandFirmLatch { Arrived = true, OrderX = f.FormationOrderX, OrderY = f.FormationOrderY };
                return StandFirmVerdict.Stands;
            }
            return StandFirmVerdict.NotArrived;
        }

        /// <summary>The whole decision for a man step 41 judged (<paramref name="held"/> = his verdict was Hold): the master switch FIRST, the hold, the
        /// switch, held, then <paramref name="conditions"/> (<see cref="Conditions"/>).</summary>
        public static StandFirmVerdict Decide(in StandFirmRules r, bool held, StandFirmVerdict conditions)
        {
            if (!r.ModEnabled) return StandFirmVerdict.ModOff;
            if (!r.HoldsSlots) return StandFirmVerdict.HoldOff;
            if (!r.StandFirm) return StandFirmVerdict.SwitchedOff;
            if (!held) return StandFirmVerdict.NotHeld;
            return conditions;
        }

        /// <summary>The leash a man gets: 0 while he stands firm, else step 42's.</summary>
        public static double Leash(bool standsFirm, double leash) => standsFirm ? 0 : leash;

        /// <summary>The tolerance (the integrity data's deviation) handed to the engine with the flag.</summary>
        public static double Tolerance(in StandFirmRules r, bool standsFirm, double formationDeviation)
        {
            if (!standsFirm || !r.TightTolerance) return formationDeviation;
            if (double.IsNaN(formationDeviation)) return r.SlackMetres;
            return Math.Min(formationDeviation, r.SlackMetres);
        }

        public static string Name(StandFirmVerdict v) => v switch
        {
            StandFirmVerdict.Stands => "stands firm",
            StandFirmVerdict.ModOff => "the mod off",
            StandFirmVerdict.HoldOff => "the hold off",
            StandFirmVerdict.SwitchedOff => "switched off",
            StandFirmVerdict.NotHeld => "not held",
            StandFirmVerdict.NotAHoldOrder => "not a hold order",
            StandFirmVerdict.NotArrived => "a move not at its place yet",
            _ => v.ToString(),
        };

        public static string Name(StandFirmLever l) => l switch
        {
            StandFirmLever.StepOut => "the step out (leash)",
            StandFirmLever.Sprint => "a sprint",
            StandFirmLever.Pace => "the advance pace",
            StandFirmLever.KickOrPunish => "his own kick / bash or a punish press",
            StandFirmLever.Pushed => "pushed / the game's approach / a blow",
            _ => l.ToString(),
        };
    }

    /// <summary>
    /// Step 75: one mission's stand-firm numbers - the men and their man-seconds (from the 0.5 s samples), where they stood against their slots ON vs
    /// OFF (in contact), the strikes made from the slot, the levers refused by kind, the walks back, the let-go reasons and the looks that did not stand
    /// - and the [summary] "wall stands firm" line. Main thread only.
    /// </summary>
    public sealed class StandFirmStats
    {
        public const double NearMetres = 0.5;
        public const double FarMetres = 1.0;

        /// <summary>Samples of a man in stand-firm conditions: [1] ON (standing firm), [0] OFF (the switch off - step 41-68's rules).</summary>
        private readonly int[] _samples = new int[2], _contact = new int[2], _overNear = new int[2], _overFar = new int[2];
        private readonly double[] _aheadSum = new double[2], _max = new double[2];
        private readonly int[] _strikesFromSlot = new int[2], _strikesOut = new int[2];
        private readonly int[] _refused = new int[(int)StandFirmLever.Count];
        private readonly int[] _looks = new int[(int)StandFirmVerdict.Count];

        public int MenStood;
        public int Spells, SpellsAfterImpact;
        public MeanStd Back;
        public int LetGoByMorale, LetGoFarBehind, LetGoOther;
        public long FramesPinned;
        public int ToleranceWrites;
        public int SwitchChanges;
        public int Errors;
        public bool EverOn;

        public double SampleSeconds = 0.5;

        public void AddSample(bool on, double ahead, bool contact)
        {
            int w = on ? 1 : 0;
            double a = ahead > 0 && !double.IsNaN(ahead) ? ahead : 0;
            _samples[w]++;
            if (!contact) return;
            _contact[w]++;
            if (a > NearMetres) _overNear[w]++;
            if (a > FarMetres) _overFar[w]++;
            _aheadSum[w] += a;
            if (a > _max[w]) _max[w] = a;
        }

        public int Samples(bool on) => _samples[on ? 1 : 0];
        public int ContactSamples(bool on) => _contact[on ? 1 : 0];
        public int ContactOverNear(bool on) => _overNear[on ? 1 : 0];
        public int ContactOverFar(bool on) => _overFar[on ? 1 : 0];
        public double ContactMax(bool on) => _max[on ? 1 : 0];

        public void AddStrike(bool on, bool fromSlot)
        {
            if (fromSlot) _strikesFromSlot[on ? 1 : 0]++;
            else _strikesOut[on ? 1 : 0]++;
        }

        public int StrikesFromSlot(bool on) => _strikesFromSlot[on ? 1 : 0];
        public int StrikesOut(bool on) => _strikesOut[on ? 1 : 0];

        public void Refuse(StandFirmLever l) => _refused[(int)l]++;
        public int Refused(StandFirmLever l) => _refused[(int)l];

        public void AddLook(StandFirmVerdict v) => _looks[(int)v]++;
        public int Looks(StandFirmVerdict v) => _looks[(int)v];

        public void Began(bool afterImpact, bool ownKick)
        {
            Spells++;
            if (afterImpact) SpellsAfterImpact++;
            Refuse(ownKick ? StandFirmLever.KickOrPunish : StandFirmLever.Pushed);
        }

        public void LetGo(WallVerdict why)
        {
            if (why == WallVerdict.Breaking) LetGoByMorale++;
            else if (why == WallVerdict.FarBehind) LetGoFarBehind++;
            else LetGoOther++;
        }

        /// <summary>The [summary] line.</summary>
        public string SummaryLine(in StandFirmRules r)
        {
            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(640);
            sb.Append("wall stands firm (step 75 - ").Append(r.Enabled ? "ShieldWallStandFirm ON" : "OFF (" + r.OffBecause + ")").Append(" now")
              .Append(EverOn ? string.Empty : ", never on this battle").Append(", slack ").Append(StandFirmRules.F2(r.SlackMetres)).Append(" m): ");
            if (Samples(true) + Samples(false) == 0)
                return sb.Append("no AI man stood in a held shield wall / square / circle on a stop or a move at its place this battle").ToString();
            sb.Append("held ").Append(Seconds(Samples(true))).Append(" man-s (in contact ").Append(Seconds(ContactSamples(true))).Append(" man-s) by ").Append(MenStood)
              .Append(" men | IN CONTACT ahead of the slot - ON ").Append(Ahead(true)).Append(" | OFF ").Append(Ahead(false))
              .Append(" (OFF = the same men and orders with the switch off: ").Append(Seconds(Samples(false))).Append(" man-s)")
              .Append(" | strikes - ON from the slot ").Append(StrikesFromSlot(true)).Append(", out of it ").Append(StrikesOut(true))
              .Append(" (").Append(PerMinute(StrikesFromSlot(true) + StrikesOut(true), ContactSamples(true))).Append(" a man-minute in contact)")
              .Append("; OFF from the slot ").Append(StrikesFromSlot(false)).Append(", out of it ").Append(StrikesOut(false))
              .Append(" (").Append(PerMinute(StrikesFromSlot(false) + StrikesOut(false), ContactSamples(false))).Append(" a man-minute in contact)")
              .Append(" | levers refused - ");
            for (int i = 0; i < (int)StandFirmLever.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(StandFirmMath.Name((StandFirmLever)i)).Append(' ').Append(_refused[i]);
                if ((StandFirmLever)i == StandFirmLever.Pushed) sb.Append(" (after an impact ").Append(SpellsAfterImpact).Append(')');
            }
            sb.Append("; walked back ").Append(Spells).Append(" times, back in ")
              .Append(Back.Count > 0 ? Back.Mean.ToString("0.00", c) + " s (n " + Back.Count + ")" : "- (n 0)")
              .Append("; the engine's walk target pinned to the slot ").Append(FramesPinned).Append(" times, the tolerance tightened ").Append(ToleranceWrites)
              .Append(" times | let go while standing firm: the wall broke (its morale) ").Append(LetGoByMorale).Append(", far behind (rejoining) ").Append(LetGoFarBehind)
              .Append(", other (a new order, a charge ...) ").Append(LetGoOther)
              .Append(" | held looks not standing firm: not a hold order ").Append(Looks(StandFirmVerdict.NotAHoldOrder))
              .Append(", a move not at its place yet ").Append(Looks(StandFirmVerdict.NotArrived)).Append(", switched off ").Append(Looks(StandFirmVerdict.SwitchedOff))
              .Append("; switch changes ").Append(SwitchChanges).Append(", errors ").Append(Errors);
            return sb.ToString();
        }

        private string Seconds(int samples) => (samples * SampleSeconds).ToString("0", CultureInfo.InvariantCulture);

        private string Ahead(bool on)
        {
            int n = ContactSamples(on);
            if (n == 0) return "no samples";
            int w = on ? 1 : 0;
            return "more than " + NearMetres.ToString("0.0", CultureInfo.InvariantCulture) + " m " + Pct(_overNear[w], n) + ", more than "
                   + FarMetres.ToString("0", CultureInfo.InvariantCulture) + " m " + Pct(_overFar[w], n) + ", mean " + StandFirmRules.F2(_aheadSum[w] / n)
                   + " m, the farthest " + StandFirmRules.F2(_max[w]) + " m (" + n + " samples)";
        }

        private string PerMinute(int strikes, int contactSamples)
        {
            double minutes = contactSamples * SampleSeconds / 60;
            return minutes > 0 ? (strikes / minutes).ToString("0.0", CultureInfo.InvariantCulture) : "-";
        }

        private static string Pct(int k, int n) => (100.0 * k / n).ToString("0.0", CultureInfo.InvariantCulture) + "%";
    }
}

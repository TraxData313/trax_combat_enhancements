using System;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 21 (Anton, 2026-09-28: "make the infantry more defensive, especially the guys with the shields" and "make the
    /// archers a bit slower") - the class of one AI attack, decided at its RELEASE from what the fighter really holds (never
    /// his formation or troop class). The battle-pace share of the pause after the attack depends on it.
    /// </summary>
    public enum AttackClass
    {
        /// <summary>Melee on foot with a shield in the other hand - the shield wall.</summary>
        ShieldInfantry = 0,

        /// <summary>Melee on foot without a shield: two-handers, polearms, a one-hander alone.</summary>
        FootMelee = 1,

        /// <summary>Melee on horseback (lancers, horsemen) - no battle-pace share.</summary>
        Rider = 2,

        /// <summary>A bow shot, on foot or mounted (horse archers too).</summary>
        Bow = 3,

        /// <summary>A crossbow shot, on foot or mounted.</summary>
        Crossbow = 4,

        /// <summary>Thrown weapons (javelins, throwing axes and knives, stones), slings and anything else ranged - no share.</summary>
        OtherRanged = 5,

        Count = 6,
    }

    /// <summary>The ranged weapon a release was made with, as the weapon read saw it.</summary>
    public enum RangedWeaponKind
    {
        None = 0,
        Bow = 1,
        Crossbow = 2,
        Other = 3,
    }

    /// <summary>One AI attack's pause after step 21: the TIRED part (step 13's D × (1/m − 1)) and the BATTLE-PACE share
    /// (the defensive wait of foot melee, the archers' extra seconds) on top. Total = both.</summary>
    public readonly struct PacePlan
    {
        public PacePlan(AttackClass cls, double tired, double extra, int percent)
        {
            Class = cls;
            Tired = tired;
            Extra = extra;
            Percent = percent;
        }

        public AttackClass Class { get; }

        /// <summary>Step 13's pause: D × (1/m − 1), 0 at full strength.</summary>
        public double Tired { get; }

        /// <summary>The battle-pace share on top (seconds, ≥ 0).</summary>
        public double Extra { get; }

        /// <summary>The "swing less" % that sized a melee share (0 for archers and classes without one).</summary>
        public int Percent { get; }

        public double Total => Tired + Extra;

        public bool HasExtra => Extra > 0;
    }

    /// <summary>
    /// The battle-pace settings for ONE decision, read live from <see cref="TraxSettings"/> at each attack's end (a change
    /// applies at every fighter's next attack; a pause already running keeps its length). A struct, built where needed.
    /// </summary>
    public readonly struct BattlePaceRules
    {
        public BattlePaceRules(int shieldInfantryPercent, int footMeleePercent, double aiGapSeconds, double bowSeconds, double crossbowSeconds)
        {
            ShieldInfantryPercent = BattlePaceMath.ClampPercent(shieldInfantryPercent);
            FootMeleePercent = BattlePaceMath.ClampPercent(footMeleePercent);
            AiGapSeconds = BattlePaceMath.ClampSeconds(aiGapSeconds);
            BowSeconds = BattlePaceMath.ClampSeconds(bowSeconds);
            CrossbowSeconds = BattlePaceMath.ClampSeconds(crossbowSeconds);
        }

        /// <summary>ShieldInfantrySwingsLessPercent (0-90).</summary>
        public int ShieldInfantryPercent { get; }

        /// <summary>FootMeleeSwingsLessPercent (0-90).</summary>
        public int FootMeleePercent { get; }

        /// <summary>AiMeleeGapSeconds - the AI's own gap between a melee attack's end and its next one at full strength.</summary>
        public double AiGapSeconds { get; }

        /// <summary>ExtraPauseAfterBowShotSeconds.</summary>
        public double BowSeconds { get; }

        /// <summary>ExtraPauseAfterCrossbowShotSeconds.</summary>
        public double CrossbowSeconds { get; }

        public static BattlePaceRules From(TraxSettings s) =>
            new BattlePaceRules(s.ShieldInfantrySwingsLessPercent, s.FootMeleeSwingsLessPercent, s.AiMeleeGapSeconds,
                s.ExtraPauseAfterBowShotSeconds, s.ExtraPauseAfterCrossbowShotSeconds);

        /// <summary>The "swing less" % of a melee class (shield infantry, other foot melee); 0 for every other class.</summary>
        public int SwingsLessPercent(AttackClass c) =>
            c == AttackClass.ShieldInfantry ? ShieldInfantryPercent : c == AttackClass.FootMelee ? FootMeleePercent : 0;

        /// <summary>The extra seconds after a shot (bows, crossbows); 0 for every other class.</summary>
        public double ExtraSeconds(AttackClass c) =>
            c == AttackClass.Bow ? BowSeconds : c == AttackClass.Crossbow ? CrossbowSeconds : 0;

        /// <summary>Some class gets a share now.</summary>
        public bool AnyOn => ShieldInfantryPercent > 0 || FootMeleePercent > 0 || BowSeconds > 0 || CrossbowSeconds > 0;

        public bool SameAs(in BattlePaceRules o) =>
            ShieldInfantryPercent == o.ShieldInfantryPercent && FootMeleePercent == o.FootMeleePercent && AiGapSeconds.Equals(o.AiGapSeconds)
            && BowSeconds.Equals(o.BowSeconds) && CrossbowSeconds.Equals(o.CrossbowSeconds);

        /// <summary>The setting that gives <paramref name="c"/> its share, with its value ("ShieldInfantrySwingsLessPercent 30"), or
        /// "no setting - tiredness only".</summary>
        public string SettingText(AttackClass c) => c switch
        {
            AttackClass.ShieldInfantry => "ShieldInfantrySwingsLessPercent " + ShieldInfantryPercent,
            AttackClass.FootMelee => "FootMeleeSwingsLessPercent " + FootMeleePercent,
            AttackClass.Bow => "ExtraPauseAfterBowShotSeconds " + BattlePaceMath.S1(BowSeconds),
            AttackClass.Crossbow => "ExtraPauseAfterCrossbowShotSeconds " + BattlePaceMath.S1(CrossbowSeconds),
            _ => "no setting - tiredness only",
        };

        /// <summary>The settings sentence of the mission-start line, a mid-battle change and the summary.
        /// <paramref name="paceOn"/> = the AI timer may run (the master switch, Athletics, AttackRatePaceHold).</summary>
        public string Describe(bool paceOn, string? paceOffBecause = null)
        {
            string head = paceOn ? "ON" : "OFF (" + (paceOffBecause ?? "the AI timer") + " off - no AI pause at all)";
            return head + " - AI shield infantry " + Swing(ShieldInfantryPercent, "ShieldInfantrySwingsLessPercent") + ", other AI foot melee "
                   + Swing(FootMeleePercent, "FootMeleeSwingsLessPercent") + " - after each such swing a pause that stretches his expected cycle (his attack + "
                   + "his tired pause + his own gap AiMeleeGapSeconds " + BattlePaceMath.S2(AiGapSeconds) + " s) ÷ (1 - the share), at full strength or tired; "
                   + "AI bowmen " + Wait(BowSeconds, "ExtraPauseAfterBowShotSeconds") + ", crossbowmen " + Wait(CrossbowSeconds, "ExtraPauseAfterCrossbowShotSeconds")
                   + ", on foot and mounted, on top of the tired pause; riders' melee, thrown weapons, slings and you: "
                   + "tiredness only; through the AI timer (guard up) - read at each attack's end";
        }

        private static string Swing(int percent, string key) => percent > 0 ? "swing " + percent + "% less (" + key + ")" : "unchanged (" + key + " 0 = off)";

        private static string Wait(double seconds, string key) =>
            seconds > 0 ? "wait " + BattlePaceMath.S1(seconds) + " s more after each shot (" + key + ")" : "unchanged (" + key + " 0 = off)";
    }

    /// <summary>
    /// Step 21 as pure functions (AI_NOTES "Step 21"): which class an attack is, and the pause after it.
    ///
    /// FOOT MELEE ("swing p% less"): the AI does not attack the moment he may - Anton's logs of 2026-09-27/28 measured a fresh AI
    /// melee cycle of 1.80 s against an attack (wind-up + swing) of 0.78 s: his own gap after an attack is about 1.0 s, and a pause
    /// ADDS to it (the gap after a tired pause was 1.4-1.7 s, not shorter). So step 13's D-based rule stretched to m × (1 − p) would
    /// buy only half the asked cut at full strength (0.43 × 0.78 = 0.33 s on a 1.8 s cycle = 16% fewer swings, not 30%). Instead the
    /// share stretches his whole EXPECTED cycle - his attack D + his tired pause T + his own gap G - by 1 / (1 − q), q = p / 100:
    ///   total pause = (T + q × (D + G)) / (1 − q)        (the share = total − T)
    /// so D + total + G = (D + G + T) / (1 − q): at every tiredness he swings p% less than tiredness alone lets him (fresh shield man,
    /// q 0.3: 0.43 × 1.78 = 0.76 s). ARCHERS: a flat number of seconds on top of the tired pause, sized from the measured fresh shot
    /// cycles (bows ~4.5 s → 2.0 s ≈ 30% fewer shots; crossbows ~6 s → 2.5 s).
    /// </summary>
    public static class BattlePaceMath
    {
        /// <summary>The "swing less" sliders stop here: at 100 the pause would be endless (÷ 0).</summary>
        public const int MaxSwingsLessPercent = 90;

        public static int ClampPercent(int percent) => percent < 0 ? 0 : percent > MaxSwingsLessPercent ? MaxSwingsLessPercent : percent;

        public static double ClampSeconds(double seconds) => double.IsNaN(seconds) || seconds < 0 ? 0 : seconds;

        /// <summary>The class of an attack from what the fighter holds at its release.</summary>
        public static AttackClass Classify(bool melee, bool mounted, bool shieldInOffHand, RangedWeaponKind ranged)
        {
            if (melee) return mounted ? AttackClass.Rider : shieldInOffHand ? AttackClass.ShieldInfantry : AttackClass.FootMelee;
            return ranged == RangedWeaponKind.Bow ? AttackClass.Bow : ranged == RangedWeaponKind.Crossbow ? AttackClass.Crossbow : AttackClass.OtherRanged;
        }

        public static bool IsMelee(AttackClass c) => c == AttackClass.ShieldInfantry || c == AttackClass.FootMelee || c == AttackClass.Rider;

        /// <summary>The class read at the release, checked against the kind of the attack that ENDED (they always agree in play; a
        /// release the read missed falls back to the plain class of its kind - never a share it was not read for).</summary>
        public static AttackClass Consistent(AttackClass read, AttackKind kind, bool mounted)
        {
            if (kind == AttackKind.Melee)
            {
                if (!IsMelee(read)) return mounted ? AttackClass.Rider : AttackClass.FootMelee;
                if (mounted) return AttackClass.Rider; // he mounted between the release and its end: a rider's attack now
                return read;
            }
            return IsMelee(read) ? AttackClass.OtherRanged : read;
        }

        /// <summary>
        /// The pause after one AI attack of class <paramref name="c"/>, duration <paramref name="duration"/> (D, at full animation
        /// speed - NaN = not measured), ending at attack multiplier <paramref name="m"/>: step 13's tired pause plus this class's
        /// battle-pace share (the class doc). A melee share needs D (not measured → the tired part alone, which is 0 then too).
        /// </summary>
        public static PacePlan Plan(in BattlePaceRules r, AttackClass c, double duration, float m)
        {
            double tired = AttackTimerMath.Pause(duration, m);
            int pct = r.SwingsLessPercent(c);
            if (pct > 0)
            {
                if (double.IsNaN(duration) || duration <= 0) return new PacePlan(c, tired, 0, pct);
                double q = pct / 100.0;
                double total = (tired + q * (duration + r.AiGapSeconds)) / (1.0 - q);
                return new PacePlan(c, tired, Math.Max(0, total - tired), pct);
            }
            return new PacePlan(c, tired, r.ExtraSeconds(c), 0);
        }

        /// <summary>The model's cycle of a melee attack (attack end to attack end, the summary's check): his attack as played +
        /// the pause he got + his own gap. With the share: (attack + gap + tired) ÷ (1 − q) when D = played.</summary>
        public static double ModelCycle(double attackPlayed, double pause, double gap) =>
            double.IsNaN(attackPlayed) || attackPlayed <= 0 ? double.NaN : attackPlayed + Math.Max(0, pause) + Math.Max(0, gap);

        /// <summary>Attacks a minute at one attack every <paramref name="cycleSeconds"/> (NaN when unknown).</summary>
        public static double PerMinute(double cycleSeconds) => double.IsNaN(cycleSeconds) || cycleSeconds <= 0 ? double.NaN : 60.0 / cycleSeconds;

        /// <summary>How many fewer attacks a minute, as a share, going from <paramref name="before"/> to <paramref name="after"/> seconds a cycle.</summary>
        public static double FewerShare(double before, double after) =>
            double.IsNaN(before) || double.IsNaN(after) || before <= 0 || after <= 0 ? double.NaN : 1.0 - before / after;

        /// <summary>The class in words for the logs and the summary.</summary>
        public static string Name(AttackClass c) => c switch
        {
            AttackClass.ShieldInfantry => "shield infantry",
            AttackClass.FootMelee => "other foot melee",
            AttackClass.Rider => "riders",
            AttackClass.Bow => "bowmen",
            AttackClass.Crossbow => "crossbowmen",
            _ => "thrown and slings",
        };

        /// <summary>What the class covers, in words.</summary>
        public static string What(AttackClass c) => c switch
        {
            AttackClass.ShieldInfantry => "AI melee on foot, a shield in the other hand",
            AttackClass.FootMelee => "AI melee on foot without a shield - two-handers, polearms, a one-hander alone",
            AttackClass.Rider => "AI melee on horseback",
            AttackClass.Bow => "AI bow shots, on foot and horse archers",
            AttackClass.Crossbow => "AI crossbow shots, on foot and mounted",
            _ => "AI javelins, throwing axes and knives, stones, slings",
        };

        /// <summary>"swings" for melee classes, "shots" for bows and crossbows, "throws" for the rest.</summary>
        public static string Unit(AttackClass c) => IsMelee(c) ? "swings" : c == AttackClass.OtherRanged ? "throws" : "shots";

        internal static string S1(double v) => double.IsNaN(v) ? "n/a" : v.ToString("0.0", CultureInfo.InvariantCulture);

        internal static string S2(double v) => double.IsNaN(v) ? "n/a" : v.ToString("0.00", CultureInfo.InvariantCulture);
    }
}

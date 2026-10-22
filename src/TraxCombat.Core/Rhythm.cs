using System;
using System.Collections.Generic;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>
    /// Step 78 - THE BATTLE'S RHYTHM, part 1 (Toni 2026-10-22, his sketch 1-4: two packs of dogs that do not want to get hurt but will not back down and
    /// will not do reckless things - the clash, the GRIND, then a LULL with a gap; the waves are step 79; AI_NOTES "Step 78", DESIGN decision 72). The
    /// settings, read live: the master switch FIRST, then the Athletics system (a bar to read), then each part's own switch, then the numbers.
    /// </summary>
    public readonly struct RhythmRules
    {
        public RhythmRules(bool modEnabled, bool athleticsEnabled, bool rotationEnabled, bool lullEnabled, bool lullForPlayerUnits = true,
            int tiredPercent = 35, bool useBraceFloor = true, int fresherPercent = 25, double behindMetres = 1.5, double fileMetres = 1.0,
            double contactMetres = 2.5, double besideMetres = 0.7, double approachSeconds = 3, double maxSeconds = 6, double cooldownSeconds = 12,
            double restSeconds = 6, int lullContactMen = 3, double lullMinContactSeconds = 10, int lullStartCharge = 30, int lullStartAdvance = 40,
            int lullStartHold = 50, int lullRecoverCharge = 10, int lullRecoverAdvance = 15, int lullRecoverHold = 20, double lullEaseMetres = 2,
            double lullFrontDepthMetres = 1.5, double lullMaxSeconds = 30, double lullCooldownSeconds = 20, bool lullRotates = true,
            double clockChargeMinSeconds = 30, double clockChargeMaxSeconds = 60, double clockAdvanceMinSeconds = 25, double clockAdvanceMaxSeconds = 50,
            double clockHoldMinSeconds = 20, double clockHoldMaxSeconds = 40, int clockChargePercent = 55, int clockAdvancePercent = 60, int clockHoldPercent = 65,
            int attackRateFallenPercent = 60, double unitDepthMetres = 8, double chargeGraceSeconds = 3, double gapMetres = 3.5, double maxBackMetres = 8)
        {
            ModEnabled = modEnabled;
            AthleticsEnabled = athleticsEnabled;
            RotationEnabled = rotationEnabled;
            LullEnabled = lullEnabled;
            LullForPlayerUnits = lullForPlayerUnits;
            TiredPercent = ClampI(tiredPercent, 0, 100);
            UseBraceFloor = useBraceFloor;
            FresherPercent = ClampI(fresherPercent, 5, 100);
            BehindMetres = Clamp(behindMetres, 0.5, 4);
            FileMetres = Clamp(fileMetres, 0.2, 3);
            ContactMetres = Clamp(contactMetres, 1, 6);
            BesideMetres = Clamp(besideMetres, 0.3, 2);
            ApproachSeconds = Clamp(approachSeconds, 0.5, 10);
            MaxSeconds = Clamp(maxSeconds, 2, 20);
            CooldownSeconds = Clamp(cooldownSeconds, 1, 120);
            RestSeconds = Clamp(restSeconds, 0, 30);
            LullContactMen = ClampI(lullContactMen, 1, 50);
            LullMinContactSeconds = Clamp(lullMinContactSeconds, 0, 120);
            LullStartChargePercent = ClampI(lullStartCharge, 0, 100);
            LullStartAdvancePercent = ClampI(lullStartAdvance, 0, 100);
            LullStartHoldPercent = ClampI(lullStartHold, 0, 100);
            LullRecoverChargePercent = ClampI(lullRecoverCharge, 1, 100);
            LullRecoverAdvancePercent = ClampI(lullRecoverAdvance, 1, 100);
            LullRecoverHoldPercent = ClampI(lullRecoverHold, 1, 100);
            LullEaseMetres = Clamp(lullEaseMetres, 0.5, 8);
            LullFrontDepthMetres = Clamp(lullFrontDepthMetres, 0.5, 5);
            LullMaxSeconds = Clamp(lullMaxSeconds, 5, 180);
            LullCooldownSeconds = Clamp(lullCooldownSeconds, 0, 300);
            LullRotates = lullRotates;
            ClockChargeMinSeconds = Clamp(clockChargeMinSeconds, 0, 600);
            ClockChargeMaxSeconds = Math.Max(ClockChargeMinSeconds, Clamp(clockChargeMaxSeconds, 0, 600));
            ClockAdvanceMinSeconds = Clamp(clockAdvanceMinSeconds, 0, 600);
            ClockAdvanceMaxSeconds = Math.Max(ClockAdvanceMinSeconds, Clamp(clockAdvanceMaxSeconds, 0, 600));
            ClockHoldMinSeconds = Clamp(clockHoldMinSeconds, 0, 600);
            ClockHoldMaxSeconds = Math.Max(ClockHoldMinSeconds, Clamp(clockHoldMaxSeconds, 0, 600));
            ClockChargePercent = ClampI(clockChargePercent, 0, 100);
            ClockAdvancePercent = ClampI(clockAdvancePercent, 0, 100);
            ClockHoldPercent = ClampI(clockHoldPercent, 0, 100);
            AttackRateFallenPercent = ClampI(attackRateFallenPercent, 0, 100);
            UnitDepthMetres = Clamp(unitDepthMetres, 0.5, 40);
            ChargeGraceSeconds = Clamp(chargeGraceSeconds, 0, 15);
            GapMetres = Clamp(gapMetres, 0.5, 10);
            MaxBackMetres = Clamp(maxBackMetres, 0.5, 30);
        }

        public static RhythmRules From(TraxSettings s) =>
            new RhythmRules(s.ModEnabled, s.AthleticsEnabled, s.RotationEnabled, s.LullEnabled, s.LullForPlayerUnits, s.RotationTiredPercent, s.RotationUseBraceFloor,
                s.RotationFresherPercent, s.RotationBehindMetres, s.RotationFileMetres, s.RotationContactMetres, s.RotationBesideMetres, s.RotationApproachSeconds,
                s.RotationMaxSeconds, s.RotationCooldownSeconds, s.RotationRestSeconds, s.LullContactMen, s.LullMinContactSeconds, s.LullStartChargePercent,
                s.LullStartAdvancePercent, s.LullStartHoldPercent, s.LullRecoverChargePercent, s.LullRecoverAdvancePercent, s.LullRecoverHoldPercent, s.LullEaseMetres,
                s.LullFrontDepthMetres, s.LullMaxSeconds, s.LullCooldownSeconds, s.LullRotates, s.LullClockChargeMinSeconds, s.LullClockChargeMaxSeconds,
                s.LullClockAdvanceMinSeconds, s.LullClockAdvanceMaxSeconds, s.LullClockHoldMinSeconds, s.LullClockHoldMaxSeconds, s.LullClockChargePercent,
                s.LullClockAdvancePercent, s.LullClockHoldPercent, s.LullAttackRateFallenPercent, s.LullUnitDepthMetres, s.LullChargeGraceSeconds, s.LullGapMetres,
                s.LullMaxBackMetres);

        public bool ModEnabled { get; }

        /// <summary>The Athletics system - the rhythm reads the men's bars; without it nothing tires, so nothing rotates or lulls.</summary>
        public bool AthleticsEnabled { get; }

        /// <summary>RotationEnabled - a tired front man and a fresher mate right behind him swap places.</summary>
        public bool RotationEnabled { get; }

        /// <summary>LullEnabled - two facing fronts both spent ease back and catch their breath.</summary>
        public bool LullEnabled { get; }

        /// <summary>LullForPlayerUnits - the player's own formations ease back too (never the player himself).</summary>
        public bool LullForPlayerUnits { get; }

        /// <summary>RotationTiredPercent: a front man at or below this bar is tired (or at his brace floor, <see cref="UseBraceFloor"/>).</summary>
        public int TiredPercent { get; }

        public bool UseBraceFloor { get; }

        /// <summary>RotationFresherPercent: the mate behind must have this many points of the bar more.</summary>
        public int FresherPercent { get; }

        /// <summary>RotationBehindMetres: the mate stands at most this far behind him (toward their own side).</summary>
        public double BehindMetres { get; }

        /// <summary>RotationFileMetres: ... and at most this far to the side of his file.</summary>
        public double FileMetres { get; }

        /// <summary>RotationContactMetres: an enemy within this = the man is in contact (a front man of the grind).</summary>
        public double ContactMetres { get; }

        /// <summary>RotationBesideMetres: the fresh man steps up this far beside the tired one before the tired one backs off.</summary>
        public double BesideMetres { get; }

        /// <summary>RotationApproachSeconds: the fresh man must be beside within this, else the swap is called off (the tired man never left).</summary>
        public double ApproachSeconds { get; }

        /// <summary>RotationMaxSeconds: a whole swap's longest time.</summary>
        public double MaxSeconds { get; }

        /// <summary>RotationCooldownSeconds: at most one swap per man in this many seconds.</summary>
        public double CooldownSeconds { get; }

        /// <summary>RotationRestSeconds: the man who went back does not walk forward past his new place for this long.</summary>
        public double RestSeconds { get; }

        /// <summary>LullContactMen: men in contact (both units together) to call two units a fighting pair.</summary>
        public int LullContactMen { get; }

        /// <summary>LullMinContactSeconds: the pair must have fought this long (the first surge spent) before a lull.</summary>
        public double LullMinContactSeconds { get; }

        public int LullStartChargePercent { get; }
        public int LullStartAdvancePercent { get; }
        public int LullStartHoldPercent { get; }
        public int LullRecoverChargePercent { get; }
        public int LullRecoverAdvancePercent { get; }
        public int LullRecoverHoldPercent { get; }

        /// <summary>LullEaseMetres: each front eases back this far from where it stood.</summary>
        public double LullEaseMetres { get; }

        /// <summary>LullFrontDepthMetres: a unit's FRONT MEN stand within this of its front (their mean bar is the front's bar).</summary>
        public double LullFrontDepthMetres { get; }

        public double LullMaxSeconds { get; }
        public double LullCooldownSeconds { get; }

        /// <summary>LullRotates: during a lull the tired front men swap with fresher men behind them, no enemy in reach needed.</summary>
        public bool LullRotates { get; }

        /// <summary>Step 78b - THE BREATHER CLOCK (LullClock*Seconds): a pair in continuous contact for a time rolled between min and max of the more
        /// aggressive unit's order group (rolled once per spell) lulls once both units are worn (<see cref="ClockChargePercent"/> ...) or the blows slowed.</summary>
        public double ClockChargeMinSeconds { get; }
        public double ClockChargeMaxSeconds { get; }
        public double ClockAdvanceMinSeconds { get; }
        public double ClockAdvanceMaxSeconds { get; }
        public double ClockHoldMinSeconds { get; }
        public double ClockHoldMaxSeconds { get; }

        /// <summary>LullClock*Percent: the unit bar (its men in the fight) below which the clock's lull may start.</summary>
        public int ClockChargePercent { get; }
        public int ClockAdvancePercent { get; }
        public int ClockHoldPercent { get; }

        /// <summary>LullAttackRateFallenPercent: ... or the pair's attack rate in contact fell to this share of its peak (0 = never).</summary>
        public int AttackRateFallenPercent { get; }

        /// <summary>LullUnitDepthMetres: a unit's bar for the lull = the mean bar of its men within this of its front (the rotated ones too).</summary>
        public double UnitDepthMetres { get; }

        /// <summary>LullChargeGraceSeconds: a new charge order in a lull's first seconds ends it only once this much of it has passed.</summary>
        public double ChargeGraceSeconds { get; }

        /// <summary>LullGapMetres: the clear gap between the nearest men of the two sides a lull opens.</summary>
        public double GapMetres { get; }

        /// <summary>LullMaxBackMetres: a line steps back at most this far beyond where the lull first put it while the gap does not hold.</summary>
        public double MaxBackMetres { get; }

        /// <summary>The rotation acts: the master switch FIRST, then Athletics, then its own switch.</summary>
        public bool RotationOn => ModEnabled && AthleticsEnabled && RotationEnabled;

        /// <summary>The lull acts: the master switch FIRST, then Athletics, then its own switch.</summary>
        public bool LullOn => ModEnabled && AthleticsEnabled && LullEnabled;

        /// <summary>Either part acts (the input wish's gate).</summary>
        public bool AnyOn => RotationOn || LullOn;

        public string? RotationOffBecause => !ModEnabled ? "ModEnabled" : !AthleticsEnabled ? "AthleticsEnabled" : !RotationEnabled ? "RotationEnabled" : null;

        public string? LullOffBecause => !ModEnabled ? "ModEnabled" : !AthleticsEnabled ? "AthleticsEnabled" : !LullEnabled ? "LullEnabled" : null;

        public string Describe()
        {
            string rot = "ROTATION: an AI man on foot in contact (an enemy within " + F(ContactMetres) + " m) whose bar is at or below " + TiredPercent + "%"
                         + (UseBraceFloor ? " or his brace floor" : string.Empty) + " swaps places with a mate of his unit standing right behind him (within "
                         + F(BehindMetres) + " m behind, " + F(FileMetres) + " m of his file) who has " + FresherPercent + "+ points more - the fresh man steps up "
                         + F(BesideMetres) + " m beside him first (within " + F(ApproachSeconds) + " s, else called off) while the tired man holds and guards, then the slots swap "
                         + "(the game's Formation.SwitchUnitLocations) and the tired man backs into the fresh man's place facing the enemy (whole swap " + F(MaxSeconds)
                         + " s at most); one swap per man per " + F(CooldownSeconds) + " s, never in an attack or a stagger; the man who went back holds his place "
                         + F(RestSeconds) + " s - " + (RotationOn ? "ON" : "OFF (" + RotationOffBecause + "): measured only");
            string lull = "LULL: two facing units in contact (" + LullContactMen + "+ men) - THE BREATHER CLOCK: after continuous contact for a time rolled per spell "
                          + "(charge " + F(ClockChargeMinSeconds) + "-" + F(ClockChargeMaxSeconds) + " s, advance " + F(ClockAdvanceMinSeconds) + "-" + F(ClockAdvanceMaxSeconds)
                          + " s, hold " + F(ClockHoldMinSeconds) + "-" + F(ClockHoldMaxSeconds) + " s, the more aggressive order of the two) once both UNITS are worn - the mean bar of "
                          + "their men within " + F(UnitDepthMetres) + " m of their front below charge " + ClockChargePercent + "% / advance " + ClockAdvancePercent + "% / hold "
                          + ClockHoldPercent + "% - " + (AttackRateFallenPercent > 0 ? "or their blows in contact slowed to " + AttackRateFallenPercent + "% of the spell's peak" : "(the blows not watched)")
                          + "; TIREDNESS: after " + F(LullMinContactSeconds) + "+ s both units spent - below charge " + LullStartChargePercent + "% / advance "
                          + LullStartAdvancePercent + "% / hold " + LullStartHoldPercent + "% - at once; each side backs (at least " + F(LullEaseMetres)
                          + " m) until a CLEAR GAP of " + F(GapMetres) + " m holds between the nearest men of the two sides (measured from the men, a line stepped back at most "
                          + F(MaxBackMetres) + " m more while it does not), facing each other, until one unit has recovered charge +" + LullRecoverChargePercent + " / advance +" + LullRecoverAdvancePercent
                          + " / hold +" + LullRecoverHoldPercent + " points, a new charge order (in its first " + F(ChargeGraceSeconds) + " s: after them), a side breaks or "
                          + F(LullMaxSeconds) + " s; then " + F(LullCooldownSeconds) + " s before the next; " + (LullRotates ? "the tired front men swap with fresher men during it" : "no swaps during it") + "; your units "
                          + (LullForPlayerUnits ? "too" : "never") + " - " + (LullOn ? "ON" : "OFF (" + LullOffBecause + "): measured only");
            return "the battle's rhythm (step 78): " + rot + "; " + lull + "; both sides, never you, never riders, attacks never touched";
        }

        private static double Clamp(double v, double lo, double hi) => double.IsNaN(v) ? lo : v < lo ? lo : v > hi ? hi : v;

        private static int ClampI(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

        internal static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>Step 78: where a man stands in a swap.</summary>
    public enum RotationPhase
    {
        None,

        /// <summary>The tired man HOLDS his place and guards (his attacks held) until the fresh man is beside him.</summary>
        Hold,

        /// <summary>The fresh man walks up beside the tired one.</summary>
        Approach,

        /// <summary>Beside: the slots swapped, the tired man backs into the fresh man's place, facing the enemy.</summary>
        Back,

        /// <summary>... and the fresh man steps into the tired man's place.</summary>
        In,
    }

    /// <summary>Step 78: why a tired front man looked at did not swap.</summary>
    public enum RotationRefusal
    {
        /// <summary>Nobody of his unit right behind him (his file, within the distance).</summary>
        NoMate,

        /// <summary>Men behind him, none fresher by the margin.</summary>
        NotFresher,

        /// <summary>A fresher man behind, but swinging, staggered, in a swap / its cooldown, or walked by another system.</summary>
        MateBusy,

        /// <summary>He himself is swinging, staggered or kicking.</summary>
        Busy,

        /// <summary>He himself is walked by another system (a step back, the wall's pull, the press's walk, courage's walk back).</summary>
        OwnWish,

        /// <summary>A held wall man whose slots cannot be swapped (no positioned slot) - he never walks out of the wall.</summary>
        NoSlots,

        /// <summary>No direction to the enemy to tell behind from in front.</summary>
        NoEnemy,

        Count,
    }

    /// <summary>Step 78: how a swap ended.</summary>
    public enum RotationEnd
    {
        /// <summary>Both in their new places.</summary>
        Done,

        /// <summary>The fresh man never came beside in time - called off, the tired man never left.</summary>
        NeverBeside,

        /// <summary>The whole swap took too long (both let go where they stood).</summary>
        Timeout,

        /// <summary>The fresh man was staggered / knocked down on his way up.</summary>
        Staggered,

        /// <summary>One of them fell or left the field.</summary>
        LeftField,

        /// <summary>A system or the master switch went off.</summary>
        SwitchedOff,

        /// <summary>One of them was excluded (fled, the player took him, a charge broke his wall...).</summary>
        Excluded,

        MissionEnd,
        Error,
        Count,
    }

    /// <summary>Step 78: how a lull ended. <see cref="Wave"/> is step 79's seam (a wave started from either side ends it).</summary>
    public enum LullEnd
    {
        /// <summary>One unit (its men in the fight - step 78b; step 78: its front men) recovered its order's points above its start bar.</summary>
        Recovered,

        /// <summary>A unit got a NEW charge order.</summary>
        ChargeOrder,

        /// <summary>A side broke or routed (its men looked at fell below half the start).</summary>
        Broken,

        /// <summary>The pair dissolved (one faces another enemy, or nobody of it is looked at any more).</summary>
        Lost,

        /// <summary>The longest lull ran out.</summary>
        Timeout,

        /// <summary>The lull (or the master switch, Athletics, your units' switch) went off.</summary>
        SwitchedOff,

        /// <summary>Step 79: a wave started (not built yet - the seam).</summary>
        Wave,

        MissionEnd,
        Count,
    }

    /// <summary>Step 78b: what started a lull.</summary>
    public enum LullTrigger
    {
        /// <summary>The breather clock ran (continuous contact for the pair's rolled time) and both units were worn or the blows had slowed.</summary>
        Clock,

        /// <summary>Both units spent (below LullStart*Percent) after the least contact time - at once, whatever the clock.</summary>
        Tired,

        Count,
    }

    /// <summary>
    /// Step 78 - the pure part: who is tired, who stands right behind him, who is fresher, busy men, a fresh man BESIDE the tired one, the lull's
    /// thresholds by order, a man's place against his unit's lull line. No game types: the action codes are the engine's
    /// <c>Agent.ActionCodeType</c> values (v1.4.8 - the offline smoke checks them against the game's enum).
    /// </summary>
    public static class RhythmMath
    {
        /// <summary>The man behind must stand at least this far behind (m) - a man at his shoulder is beside him, not behind. Plumbing.</summary>
        public const double MinBehindMetres = 0.3;

        /// <summary>The fresh man is BESIDE when he stands no more than this behind the tired man's depth (m). Plumbing.</summary>
        public const double BesideDepthMetres = 0.4;

        /// <summary>... and within the beside distance + this sideways (m). Plumbing.</summary>
        public const double BesideSlackMetres = 0.6;

        /// <summary>A walk's place is reached within this (m). Plumbing.</summary>
        public const double ArriveMetres = 0.4;

        /// <summary>A man more than this ahead of his lull line is walked back to it (m). Plumbing.</summary>
        public const double LullSlackMetres = 0.3;

        /// <summary>The forward clamp bites within this of a line (m) - PressMath's look-ahead (a 6 m/s run between two 0.1 s steers). Plumbing.</summary>
        public const double LookaheadMetres = PressMath.LookaheadMetres;

        /// <summary>A mate counts only when his own look was this recent (s) - a man nobody looks at (a rider, a fleeing man) never. Plumbing.</summary>
        public const double MateFreshSeconds = 1.5;

        /// <summary>The table publishes every this (s). Plumbing.</summary>
        public const double PublishSeconds = 0.5;

        /// <summary>A unit not published for this long is stale (s). Plumbing.</summary>
        public const double StaleSeconds = 2.0;

        /// <summary>Contact lost for less than this does not restart the pair's contact clock (s). Plumbing.</summary>
        public const double ContactGraceSeconds = 2.0;

        /// <summary>A unit's front = this quantile of its men's places toward the enemy. Plumbing.</summary>
        public const double FrontQuantile = 0.9;

        /// <summary>A side whose men looked at fell below this share of the lull's start broke. Plumbing.</summary>
        public const double BrokenShare = 0.5;

        public const int MaxSamples = 512;
        public const int Votes = 4;

        /// <summary>Step 78b: the gap between two units is measured in lateral bins this wide (m) along the contact. Plumbing.</summary>
        public const double GapBinMetres = 2.0;

        /// <summary>... this many bins (centred on the pair's middle: 128 m of front). Plumbing.</summary>
        public const int GapBins = 64;

        /// <summary>The gap "holds" when this quantile of the bins' gaps reaches the target less <see cref="GapSlackMetres"/> (most of the contact,
        /// a stuck man or two aside). Plumbing.</summary>
        public const double GapQuantile = 0.25;

        public const double GapSlackMetres = 0.5;

        /// <summary>A lull's lines first get this long to open the gap before they step back further (s). Plumbing.</summary>
        public const double SettleSeconds = 3.0;

        /// <summary>While the gap does not hold, a side standing at its line steps it back this far a publish (0.5 m/s). Plumbing.</summary>
        public const double LineStepMetres = 0.25;

        /// <summary>A side "stands at its line" when its nearest men are within this of it. Plumbing.</summary>
        public const double AtLineMetres = 0.6;

        /// <summary>The pair's attack rate is smoothed over about this (s); its peak counts only after this much contact. Plumbing.</summary>
        public const double RateTauSeconds = 6.0, RateWarmSeconds = 4.0;

        /// <summary>The bar at or below which he is TIRED: the larger of the setting and (with the brace floor counted) his own brace floor.</summary>
        public static double TiredBar(in RhythmRules r, double braceFloor) =>
            Math.Max(r.TiredPercent / 100.0, r.UseBraceFloor && !double.IsNaN(braceFloor) ? braceFloor : 0);

        public static bool IsTired(in RhythmRules r, double bar, double braceFloor) => !double.IsNaN(bar) && bar <= TiredBar(in r, braceFloor) + 1e-9;

        /// <summary>The mate is FRESHER: the margin above the tired man's bar, and not tired himself.</summary>
        public static bool Fresher(in RhythmRules r, double tiredBar, double mateBar, double mateTiredBar) =>
            !double.IsNaN(mateBar) && mateBar >= tiredBar + r.FresherPercent / 100.0 - 1e-9 && mateBar > mateTiredBar + 1e-9;

        /// <summary>A point's place against a man: <paramref name="along"/> toward the enemy (u), <paramref name="across"/> to the side.</summary>
        public static void AlongAcross(double px, double py, double ux, double uy, double x, double y, out double along, out double across)
        {
            double dx = x - px, dy = y - py;
            along = dx * ux + dy * uy;
            across = -dx * uy + dy * ux;
        }

        /// <summary>The mate stands RIGHT BEHIND him: between <see cref="MinBehindMetres"/> and the behind distance toward their own side, within the
        /// file's width to the side.</summary>
        public static bool Behind(in RhythmRules r, double along, double across) =>
            along <= -MinBehindMetres && along >= -r.BehindMetres - 1e-9 && Math.Abs(across) <= r.FileMetres + 1e-9;

        /// <summary>A mate COVERS him (he is not a front-rank man): standing in front of him toward the enemy, short of the enemy (<paramref name="reach"/>),
        /// within half the file's width to the side.</summary>
        public static bool Covers(in RhythmRules r, double along, double across, double reach) =>
            along >= MinBehindMetres && along < reach && Math.Abs(across) <= 0.5 * r.FileMetres + 0.1;

        /// <summary>The engine's action codes a man is BUSY in: an attack (ready / release / parried / blocked), a fall, a kick or bash, a strike
        /// reaction (the stagger family). The game's <c>Agent.ActionCodeType</c>.</summary>
        public const int ActReadyMelee = 19, ActFall = 23, ActKick = 28, ActWeaponBash = 31, ActStrikeLight = 48, ActMountStrike = 52;

        public static bool BusyAction(int action) =>
            (action >= ActReadyMelee && action <= ActFall) || (action >= ActKick && action <= ActWeaponBash) || (action >= ActStrikeLight && action <= ActMountStrike);

        /// <summary>Busy: an attack, a kick or a stagger under way (the action), or reeling / down from an impact outcome, or a kick attempt of ours.</summary>
        public static bool Busy(int action, bool reeling, bool down, bool kicking) => BusyAction(action) || reeling || down || kicking;

        /// <summary>The side the fresh man comes up on (+1 / −1): his own side of the tired man's file, +1 when straight behind.</summary>
        public static int SideOf(double across) => across < -0.15 ? -1 : 1;

        /// <summary>The point BESIDE the tired man (at his depth, <paramref name="beside"/> to the side).</summary>
        public static void BesidePoint(double px, double py, double ux, double uy, int side, double beside, out double bx, out double by)
        {
            double ex = -uy, ey = ux;
            bx = px + side * beside * ex;
            by = py + side * beside * ey;
        }

        /// <summary>The fresh man is BESIDE the tired one: no more than <see cref="BesideDepthMetres"/> behind his depth and within the beside distance +
        /// the slack to the side - the line has its man at the front either way.</summary>
        public static bool IsBeside(in RhythmRules r, double tx, double ty, double fx, double fy, double ux, double uy)
        {
            AlongAcross(tx, ty, ux, uy, fx, fy, out double along, out double across);
            return along >= -BesideDepthMetres && Math.Abs(across) <= r.BesideMetres + BesideSlackMetres;
        }

        /// <summary>The front's bar below which the lull may start, by the unit's order group.</summary>
        public static double LullStart(in RhythmRules r, BraceOrder g) => g switch
        {
            BraceOrder.Charge => r.LullStartChargePercent / 100.0,
            BraceOrder.Advance => r.LullStartAdvancePercent / 100.0,
            _ => r.LullStartHoldPercent / 100.0,
        };

        /// <summary>The points (bar) a front must regain above its start bar to end the lull, by the unit's order group.</summary>
        public static double LullRecover(in RhythmRules r, BraceOrder g) => g switch
        {
            BraceOrder.Charge => r.LullRecoverChargePercent / 100.0,
            BraceOrder.Advance => r.LullRecoverAdvancePercent / 100.0,
            _ => r.LullRecoverHoldPercent / 100.0,
        };

        /// <summary>Both fronts are SPENT: each front's bar below its own order's start bar (a front of nobody never).</summary>
        public static bool Spent(in RhythmRules r, double barA, BraceOrder ga, double barB, BraceOrder gb) =>
            !double.IsNaN(barA) && !double.IsNaN(barB) && barA < LullStart(in r, ga) && barB < LullStart(in r, gb);

        /// <summary>A side has RECOVERED: its front's bar at its start bar + its order's recover points.</summary>
        public static bool Recovered(in RhythmRules r, double bar, double startBar, BraceOrder g) =>
            !double.IsNaN(bar) && !double.IsNaN(startBar) && bar >= startBar + LullRecover(in r, g) - 1e-9;

        /// <summary>A man's lull verdict from his place against his unit's lull line (<paramref name="ahead"/> = metres ahead of it toward the enemy):
        /// 2 = walk back, 1 = clamp (stop at it), 0 = free (well behind it).</summary>
        public static int LullAction(double ahead) => ahead > LullSlackMetres ? 2 : ahead > -LookaheadMetres ? 1 : 0;

        /// <summary>A NEW charge order: the order's group became Charge from something else.</summary>
        public static bool NewCharge(BraceOrderKind was, BraceOrderKind now) =>
            BraceMath.Group(now) == BraceOrder.Charge && BraceMath.Group(was) != BraceOrder.Charge && was != BraceOrderKind.Count;

        /// <summary>A unit's FRONT and its front men's mean bar from its men's places toward the enemy (<paramref name="along"/>, relative to anything)
        /// and their bars: the front = the <see cref="FrontQuantile"/> of the places; the front men = within <paramref name="depth"/> of it.
        /// <paramref name="sorted"/> is scratch. False for nobody.</summary>
        public static bool FrontOf(double[] along, double[] bars, int n, double depth, double[] sorted, out double front, out double frontBar, out int frontMen)
        {
            front = frontBar = double.NaN;
            frontMen = 0;
            if (n <= 0) return false;
            Array.Copy(along, sorted, n);
            Array.Sort(sorted, 0, n);
            front = BlockHoldMath.Quantile(sorted, n, FrontQuantile);
            double sum = 0;
            for (int i = 0; i < n; i++)
            {
                if (along[i] < front - depth) continue;
                sum += bars[i];
                frontMen++;
            }
            frontBar = frontMen > 0 ? sum / frontMen : double.NaN;
            return frontMen > 0;
        }

        /// <summary>Step 78b: the pair's order group for its clock - the more aggressive of the two (charge, else advance, else hold).</summary>
        public static BraceOrder PairGroup(BraceOrder a, BraceOrder b) =>
            a == BraceOrder.Charge || b == BraceOrder.Charge ? BraceOrder.Charge : a == BraceOrder.Advance || b == BraceOrder.Advance ? BraceOrder.Advance : BraceOrder.Hold;

        /// <summary>Step 78b: the breather clock's time for the pair's group and its roll (0..1): min + roll x (max - min).</summary>
        public static double ClockSeconds(in RhythmRules r, BraceOrder g, double roll)
        {
            double lo, hi;
            switch (g)
            {
                case BraceOrder.Charge:
                    lo = r.ClockChargeMinSeconds;
                    hi = r.ClockChargeMaxSeconds;
                    break;
                case BraceOrder.Advance:
                    lo = r.ClockAdvanceMinSeconds;
                    hi = r.ClockAdvanceMaxSeconds;
                    break;
                default:
                    lo = r.ClockHoldMinSeconds;
                    hi = r.ClockHoldMaxSeconds;
                    break;
            }
            double u = double.IsNaN(roll) ? 0.5 : roll < 0 ? 0 : roll > 1 ? 1 : roll;
            return lo + u * (hi - lo);
        }

        /// <summary>Step 78b: the unit bar below which the clock may start a lull, by the unit's own order group.</summary>
        public static double ClockBar(in RhythmRules r, BraceOrder g) => g switch
        {
            BraceOrder.Charge => r.ClockChargePercent / 100.0,
            BraceOrder.Advance => r.ClockAdvancePercent / 100.0,
            _ => r.ClockHoldPercent / 100.0,
        };

        /// <summary>Step 78b: both units worn for the clock (each below its own order's clock bar).</summary>
        public static bool Worn(in RhythmRules r, double barA, BraceOrder ga, double barB, BraceOrder gb) =>
            !double.IsNaN(barA) && !double.IsNaN(barB) && barA < ClockBar(in r, ga) && barB < ClockBar(in r, gb);

        /// <summary>Step 78b: the pair's blows slowed - the smoothed rate at or below the setting's share of the spell's peak (0 = never).</summary>
        public static bool RateFallen(in RhythmRules r, double rate, double peak) =>
            r.AttackRateFallenPercent > 0 && peak > 1e-9 && !double.IsNaN(rate) && rate <= peak * r.AttackRateFallenPercent / 100.0 + 1e-9;

        /// <summary>Step 78b: the lull's trigger now, or none (Count): TIREDNESS - both spent after the least contact; the CLOCK - its time run and both
        /// worn or the blows slowed.</summary>
        public static LullTrigger Trigger(in RhythmRules r, double contactSeconds, double clockSeconds, double barA, BraceOrder ga, double barB, BraceOrder gb, bool rateFallen)
        {
            if (double.IsNaN(contactSeconds)) return LullTrigger.Count;
            if (contactSeconds >= r.LullMinContactSeconds - 1e-9 && Spent(in r, barA, ga, barB, gb)) return LullTrigger.Tired;
            if (!double.IsNaN(clockSeconds) && contactSeconds >= clockSeconds - 1e-9 && (Worn(in r, barA, ga, barB, gb) || rateFallen)) return LullTrigger.Clock;
            return LullTrigger.Count;
        }

        /// <summary>Step 78b: the mean bar of the men within <paramref name="depth"/> of the front (along the same axis); NaN for nobody.</summary>
        public static double MeanWithin(double[] along, double[] bars, int n, double front, double depth, out int men)
        {
            men = 0;
            if (double.IsNaN(front)) return double.NaN;
            double sum = 0;
            for (int i = 0; i < n; i++)
            {
                if (along[i] < front - depth) continue;
                sum += bars[i];
                men++;
            }
            return men > 0 ? sum / men : double.NaN;
        }

        /// <summary>
        /// Step 78b - THE GAP FROM THE MEN: two units' men in the pair's frame (along = toward B, lat = across, both relative to the same origin), binned
        /// across in <see cref="GapBinMetres"/> bins; in each bin with men of both, A's front-most and B's rear-most toward A - the gap there = B's - A's
        /// (negative = meshed). Returns the bins counted; out: the mean / the 10th / <see cref="GapQuantile"/> / 90th percentile of the gaps, the contact
        /// PLANE (the median of the bins' midpoints) and each side's FRONT (the median of its nearest men). Scratch arrays of <see cref="GapBins"/>.
        /// Allocation-free.
        /// </summary>
        public static int MeasureGap(double[] aAlong, double[] aLat, int na, double[] bAlong, double[] bLat, int nb, double[] maxA, double[] minB, double[] gaps,
            double[] mids, double[] fa, double[] fb, out double mean, out double q10, out double qHold, out double q90, out double plane, out double frontA, out double frontB)
        {
            mean = q10 = qHold = q90 = plane = frontA = frontB = double.NaN;
            int bins = Math.Min(GapBins, Math.Min(maxA.Length, minB.Length));
            for (int i = 0; i < bins; i++)
            {
                maxA[i] = double.NegativeInfinity;
                minB[i] = double.PositiveInfinity;
            }
            for (int i = 0; i < na; i++)
            {
                int k = (int)Math.Floor(aLat[i] / GapBinMetres) + bins / 2;
                if (k < 0 || k >= bins) continue;
                if (aAlong[i] > maxA[k]) maxA[k] = aAlong[i];
            }
            for (int i = 0; i < nb; i++)
            {
                int k = (int)Math.Floor(bLat[i] / GapBinMetres) + bins / 2;
                if (k < 0 || k >= bins) continue;
                if (bAlong[i] < minB[k]) minB[k] = bAlong[i];
            }
            int n = 0;
            double sum = 0;
            for (int k = 0; k < bins; k++)
            {
                if (double.IsNegativeInfinity(maxA[k]) || double.IsPositiveInfinity(minB[k])) continue;
                double g = minB[k] - maxA[k];
                gaps[n] = g;
                mids[n] = 0.5 * (minB[k] + maxA[k]);
                fa[n] = maxA[k];
                fb[n] = minB[k];
                sum += g;
                n++;
            }
            if (n == 0) return 0;
            mean = sum / n;
            Array.Sort(gaps, 0, n);
            Array.Sort(mids, 0, n);
            Array.Sort(fa, 0, n);
            Array.Sort(fb, 0, n);
            q10 = BlockHoldMath.Quantile(gaps, n, 0.1);
            qHold = BlockHoldMath.Quantile(gaps, n, GapQuantile);
            q90 = BlockHoldMath.Quantile(gaps, n, 0.9);
            plane = BlockHoldMath.Quantile(mids, n, 0.5);
            frontA = BlockHoldMath.Quantile(fa, n, 0.5);
            frontB = BlockHoldMath.Quantile(fb, n, 0.5);
            return n;
        }

        /// <summary>Step 78b: the gap holds - its <see cref="GapQuantile"/> at the target less the slack.</summary>
        public static bool GapHolds(in RhythmRules r, double qHold) => !double.IsNaN(qHold) && qHold >= r.GapMetres - GapSlackMetres - 1e-9;

        /// <summary>Step 78b: the lull lines at the start (along A's direction): A's = min(the plane - gap / 2, A's front - the ease), B's =
        /// max(the plane + gap / 2, B's front + the ease) - a meshed fight separates about its middle, a fight with a gap still backs off.</summary>
        public static void StartLines(in RhythmRules r, double plane, double frontA, double frontB, out double lineA, out double lineB)
        {
            lineA = Math.Min(plane - r.GapMetres / 2, frontA - r.LullEaseMetres);
            lineB = Math.Max(plane + r.GapMetres / 2, frontB + r.LullEaseMetres);
        }

        public static string TriggerName(LullTrigger t) => t switch
        {
            LullTrigger.Clock => "the breather clock",
            LullTrigger.Tired => "both spent",
            _ => t.ToString(),
        };

        public static string RefusalName(RotationRefusal r) => r switch
        {
            RotationRefusal.NoMate => "nobody right behind",
            RotationRefusal.NotFresher => "nobody fresher behind",
            RotationRefusal.MateBusy => "the fresher man busy",
            RotationRefusal.Busy => "he was swinging / staggered",
            RotationRefusal.OwnWish => "another system walked him",
            RotationRefusal.NoSlots => "a wall man without slots",
            RotationRefusal.NoEnemy => "no enemy direction",
            _ => r.ToString(),
        };

        public static string EndName(RotationEnd e) => e switch
        {
            RotationEnd.Done => "done",
            RotationEnd.NeverBeside => "never beside (called off)",
            RotationEnd.Timeout => "too long",
            RotationEnd.Staggered => "the fresh man staggered",
            RotationEnd.LeftField => "left the field",
            RotationEnd.SwitchedOff => "switched off",
            RotationEnd.Excluded => "excluded",
            RotationEnd.MissionEnd => "mission end",
            RotationEnd.Error => "error",
            _ => e.ToString(),
        };

        public static string LullEndName(LullEnd e) => e switch
        {
            LullEnd.Recovered => "recovered (the unit's bar)",
            LullEnd.ChargeOrder => "a new charge order",
            LullEnd.Broken => "a side broke",
            LullEnd.Lost => "the pair dissolved",
            LullEnd.Timeout => "too long",
            LullEnd.SwitchedOff => "switched off",
            LullEnd.Wave => "a wave (step 79)",
            LullEnd.MissionEnd => "mission end",
            _ => e.ToString(),
        };
    }

    /// <summary>Step 78: one man's rhythm - his last look (his unit, the time, eligible as a mate), his swap (role, phase, partner, the places), his
    /// rest after going back, his lull wish. Allocated once per tracked man.</summary>
    public sealed class RhythmMind
    {
        /// <summary>His unit (FearMath.UnitKey) at his last look, when, and whether that look let him take part (courage's gates / a held wall man).</summary>
        public int Key = -1;
        public double LookedAt = double.NaN;
        public bool Eligible;

        /// <summary>A held wall man at his last look (his slot is the wall's - the swap and the lull go through the slot).</summary>
        public bool Held;

        /// <summary>Step 78b: in contact at his last look (his attacks count in the pair's attack rate).</summary>
        public bool Contact;

        /// <summary>The swap: his phase, his partner, since when (the swap and this phase), the side the fresh man comes up on, the direction to the
        /// enemy at the start (u), the tired man's and the fresh man's start places.</summary>
        public RotationPhase Phase;
        public Fighter? Partner;
        public bool Tired;
        public double Since = double.NaN, PhaseSince = double.NaN;
        public int Side = 1;
        public double UX, UY;
        public double TiredX, TiredY, FreshX, FreshY;
        public bool DuringLull, Swapped, Arrived;

        /// <summary>His walk's place now (the swap's) and whether he is walking.</summary>
        public double ToX, ToY;

        /// <summary>A held wall man's slot while he swaps (the wall's walk target agrees with the swap's).</summary>
        public bool SlotOverride;
        public double SlotX, SlotY;

        /// <summary>No swap before this; after going back he holds his new place (no walking forward past it) until <see cref="RestUntil"/>.</summary>
        public double NextRotation = double.NegativeInfinity, RestUntil = double.NegativeInfinity, RestLine = double.NaN;

        /// <summary>His lull wish (his unit's lull line holds him) and his unit then.</summary>
        public bool LullWish;
        public int LullKey = -1;
        public bool LullCounted;

        /// <summary>His input wish is on (listed in the logic's rhythm men), the next steer, the next ground check, the hook's counters at its start.</summary>
        public bool Wish;
        public double NextSteer, NextEdge;
        public long MoveAtStart, ClampAtStart, HeldAtStart;

        public bool EverRotated, EverWalkedBack;

        public bool InSwap => Phase != RotationPhase.None;

        /// <summary>The swap is over for him (the logic keeps the rest and the cooldown).</summary>
        public void EndSwap()
        {
            Phase = RotationPhase.None;
            Partner = null;
            Tired = false;
            SlotOverride = false;
            Swapped = Arrived = DuringLull = false;
            Since = PhaseSince = double.NaN;
        }
    }

    /// <summary>
    /// Step 78's UNIT TABLE (team x formation, FearMath.UnitKey): every look notes the man (his place, his bar, in contact or not, the unit of his
    /// nearest enemy, his unit's order, the player's unit or not); every <see cref="RhythmMath.PublishSeconds"/> each unit's centroid, its enemy unit
    /// (the one its men see nearest - a held wall's men see none, its attackers' votes pair it), its FRONT toward that enemy and its front men's mean
    /// bar; then the LULL of each facing pair - its start (both fronts spent after enough contact), its lines (each front eased back), its end.
    /// Arrays allocated once per unit and battle; allocation-free after.
    /// </summary>
    public sealed class RhythmUnits
    {
        private const int N = FearMath.UnitCount;
        private const int V = RhythmMath.Votes;
        private const int M = RhythmMath.MaxSamples;

        // this period
        private readonly int[] _n = new int[N], _contact = new int[N], _held = new int[N];
        private readonly double[]?[] _x = new double[N][], _y = new double[N][], _bar = new double[N][];
        private readonly double[] _sx = new double[N], _sy = new double[N];
        private readonly BraceOrderKind[] _kind = new BraceOrderKind[N];
        private readonly bool[] _player = new bool[N];
        private readonly int[] _voteKey = new int[N * V], _voteN = new int[N * V];

        // scratch
        private readonly double[] _alg = new double[M], _sorted = new double[M];

        // published - the units
        public readonly bool[] Valid = new bool[N];
        public readonly double[] PublishedAt = new double[N];
        public readonly int[] Men = new int[N], ContactMen = new int[N], FrontMen = new int[N], HeldMen = new int[N];
        public readonly double[] CX = new double[N], CY = new double[N];
        public readonly int[] Enemy = new int[N];

        /// <summary>The direction toward its enemy unit, its front (along it, a world scalar = the front point · u) and the front men's mean bar.</summary>
        public readonly double[] UX = new double[N], UY = new double[N], Front = new double[N], FrontBar = new double[N];
        public readonly BraceOrderKind[] Kind = new BraceOrderKind[N];
        public readonly bool[] Player = new bool[N];
        public readonly double[] ContactSince = new double[N], ContactSeen = new double[N];

        // the lulls (per unit; a pair = two units pointing at each other)
        public readonly bool[] InLull = new bool[N];
        public readonly int[] LullPartner = new int[N];
        public readonly double[] LullSince = new double[N], LullUX = new double[N], LullUY = new double[N], LullLine = new double[N], LullEase = new double[N];
        public readonly double[] LullStartBar = new double[N], LullStartGap = new double[N], LullMaxGap = new double[N], CooldownUntil = new double[N];
        public readonly int[] LullStartMen = new int[N];
        public readonly BraceOrder[] LullGroup = new BraceOrder[N];

        // what the last publish did (the module logs and acts on them)
        public readonly int[] Started = new int[N], Ended = new int[N];
        public readonly LullEnd[] EndedWhy = new LullEnd[N];
        public readonly double[] EndedSeconds = new double[N], EndedOpened = new double[N];
        public int StartedN, EndedN;

        // counts (ON and OFF)
        public long Publishes, Pairs, SpentLooks, WouldStart, RefusedPlayer;
        public readonly long[] SpentLooksBy = new long[2];

        // ---- step 78b: the unit's bar, the breather clock, the attack rate, the gap from the men
        private const int G = RhythmMath.GapBins;
        private readonly int[] _att = new int[N];
        private readonly double[] _ga = new double[M], _gl = new double[M], _hb = new double[M], _hl = new double[M];
        private readonly double[] _maxA = new double[G], _minB = new double[G], _gaps = new double[G], _mids = new double[G], _fa = new double[G], _fb = new double[G];

        /// <summary>The mean bar of the unit's men within <see cref="RhythmRules.UnitDepthMetres"/> of its front (the lull's bar - the rotated men too).</summary>
        public readonly double[] UnitBar = new double[N];
        public readonly int[] UnitMen = new int[N];

        /// <summary>The pair's clock roll (0..1, once per spell of contact), its time now, the attack rate in contact (blows a minute per man in contact,
        /// smoothed) and its peak in this spell.</summary>
        public readonly double[] ClockRoll = new double[N], ClockAt = new double[N], RateEma = new double[N], RatePeak = new double[N];

        /// <summary>A running lull: its trigger, its lines at the start, each side's nearest men at the start (along its own direction), the gap measured
        /// now (mean / 10th / the hold quantile / 90th), the seconds it took to open (NaN = not yet), a charge order deferred by the grace (when).</summary>
        public readonly LullTrigger[] LullTriggerOf = new LullTrigger[N];
        public readonly double[] LullLine0 = new double[N], LullFront0 = new double[N], GapMean = new double[N], GapQ10 = new double[N], GapHold = new double[N],
            GapQ90 = new double[N], LullOpenedAfter = new double[N], ChargePendingAt = new double[N], LullStartRate = new double[N], LullStartRatePeak = new double[N],
            LullStartContact = new double[N], LullStartUnitBarB = new double[N];

        /// <summary>The last publish's lulls whose gap OPENED (the first key of each pair) and the ends' extra numbers.</summary>
        public readonly int[] Opened = new int[N];
        public int OpenedN;
        public readonly double[] EndedOpenedAfter = new double[N];
        public readonly LullTrigger[] EndedTrigger = new LullTrigger[N];

        /// <summary>Counts: lines stepped back while the gap did not hold; charge orders deferred by the grace; looks where the clock had run but neither
        /// side was worn and the blows had not slowed; looks a trigger held, by trigger; would-start OFF by trigger; the gap measured in lulls (publishes,
        /// sums of the mean / 10th / 90th).</summary>
        public long LineSteps, ChargeDeferred, ClockWaitLooks, GapLooks;
        public readonly long[] TriggerLooks = new long[(int)LullTrigger.Count], WouldStartBy = new long[(int)LullTrigger.Count];
        public double GapMeanSum, GapQ10Sum, GapQ90Sum, GapMin = double.NaN;

        /// <summary>The clock's dice (the tests seed it).</summary>
        public IRandomSource Dice { get; set; } = ThreadSafeRandom.Shared;

        private double _nextPublish = double.NaN;

        public RhythmUnits()
        {
            Reset();
        }

        public void Reset()
        {
            for (int k = 0; k < N; k++)
            {
                ClearPeriod(k);
                Valid[k] = InLull[k] = Player[k] = false;
                PublishedAt[k] = ContactSince[k] = ContactSeen[k] = LullSince[k] = double.NaN;
                CooldownUntil[k] = double.NegativeInfinity;
                Enemy[k] = LullPartner[k] = -1;
                Men[k] = ContactMen[k] = FrontMen[k] = HeldMen[k] = LullStartMen[k] = 0;
                FrontBar[k] = Front[k] = double.NaN;
                Kind[k] = BraceOrderKind.Count;
                UnitBar[k] = ClockRoll[k] = ClockAt[k] = RateEma[k] = double.NaN;
                RatePeak[k] = 0;
                UnitMen[k] = 0;
                LullOpenedAfter[k] = ChargePendingAt[k] = GapMean[k] = GapQ10[k] = GapHold[k] = GapQ90[k] = double.NaN;
            }
            _nextPublish = double.NaN;
            StartedN = EndedN = OpenedN = 0;
            Publishes = Pairs = SpentLooks = WouldStart = RefusedPlayer = 0;
            SpentLooksBy[0] = SpentLooksBy[1] = 0;
            LineSteps = ChargeDeferred = ClockWaitLooks = GapLooks = 0;
            for (int i = 0; i < (int)LullTrigger.Count; i++) TriggerLooks[i] = WouldStartBy[i] = 0;
            GapMeanSum = GapQ10Sum = GapQ90Sum = 0;
            GapMin = double.NaN;
        }

        /// <summary>Step 78b: an AI man of the unit attacked while in contact at his last look (the pair's attack rate).</summary>
        public void NoteAttack(int key)
        {
            if (key >= 0 && key < N) _att[key]++;
        }

        private void ClearPeriod(int k)
        {
            _att[k] = 0;
            _n[k] = _contact[k] = _held[k] = 0;
            _sx[k] = _sy[k] = 0;
            for (int i = 0; i < V; i++)
            {
                _voteKey[k * V + i] = -1;
                _voteN[k * V + i] = 0;
            }
        }

        /// <summary>The unit is published (and fresh).</summary>
        public bool Live(int key, double now) =>
            key >= 0 && key < N && Valid[key] && !double.IsNaN(PublishedAt[key]) && now - PublishedAt[key] <= RhythmMath.StaleSeconds && now >= PublishedAt[key] - 1e-9;

        /// <summary>The unit is in a lull now and it applies to its men (the player's units only while <see cref="RhythmRules.LullForPlayerUnits"/>).</summary>
        public bool LullApplies(int key, in RhythmRules r) => key >= 0 && key < N && InLull[key] && r.LullOn && (!Player[key] || r.LullForPlayerUnits);

        /// <summary>A man's place against his unit's lull line: metres ahead of it toward the enemy (NaN = no lull).</summary>
        public double AheadOfLullLine(int key, double x, double y) =>
            key < 0 || key >= N || !InLull[key] ? double.NaN : x * LullUX[key] + y * LullUY[key] - LullLine[key];

        /// <summary>A man of the unit is a FRONT man (within the front depth of its published front).</summary>
        public bool IsFrontMan(int key, double x, double y, double depth, double now)
        {
            if (!Live(key, now) || double.IsNaN(Front[key])) return false;
            double a = x * UX[key] + y * UY[key];
            return a >= Front[key] - depth;
        }

        /// <summary>One man of the unit at this look.</summary>
        public void Note(int key, double x, double y, double bar, bool contact, int foeKey, BraceOrderKind kind, bool player, bool held)
        {
            if (key < 0 || key >= N || double.IsNaN(x) || double.IsNaN(y)) return;
            _kind[key] = kind;
            _player[key] = player;
            if (contact) _contact[key]++;
            if (held) _held[key]++;
            _sx[key] += x;
            _sy[key] += y;
            if (foeKey >= 0 && foeKey < N && foeKey != key) Vote(key, foeKey);
            int s = _n[key];
            _n[key] = s + 1;
            if (s >= M) return;
            var xs = _x[key];
            if (xs == null)
            {
                xs = _x[key] = new double[M]; // once per unit and battle
                _y[key] = new double[M];
                _bar[key] = new double[M];
            }
            xs[s] = x;
            _y[key]![s] = y;
            _bar[key]![s] = double.IsNaN(bar) ? 1 : bar;
        }

        private void Vote(int key, int enemyKey)
        {
            int o = key * V, free = -1, low = o;
            for (int i = o; i < o + V; i++)
            {
                if (_voteKey[i] == enemyKey)
                {
                    _voteN[i]++;
                    return;
                }
                if (_voteKey[i] < 0 && free < 0) free = i;
                if (_voteN[i] < _voteN[low]) low = i;
            }
            int at = free >= 0 ? free : low;
            _voteKey[at] = enemyKey;
            _voteN[at] = 1;
        }

        public bool Due(double now)
        {
            if (double.IsNaN(_nextPublish))
            {
                _nextPublish = now + RhythmMath.PublishSeconds;
                return false;
            }
            return now >= _nextPublish || now < _nextPublish - 10 * RhythmMath.PublishSeconds;
        }

        /// <summary>
        /// Publishes every unit noted this period, then the lulls: (1) each unit's centroid, men, contact men and votes; (2) its enemy unit - the most
        /// voted, else a unit that votes for it (a held wall sees nobody: its attackers name it), kept while live; its direction toward that enemy, its
        /// front and its front men's bar; (3) the lulls - a running one ends (switched off, a new charge order, a side broken, the pair lost, a front
        /// recovered, too long), a pair in contact long enough with both fronts spent starts one (ON; counted OFF). Allocation-free.
        /// </summary>
        public void Publish(double now, in RhythmRules r)
        {
            _nextPublish = now + RhythmMath.PublishSeconds;
            Publishes++;
            StartedN = EndedN = OpenedN = 0;
            for (int k = 0; k < N; k++)
            {
                if (_n[k] <= 0) continue;
                var was = Kind[k];
                Men[k] = _n[k];
                ContactMen[k] = _contact[k];
                HeldMen[k] = _held[k];
                CX[k] = _sx[k] / _n[k];
                CY[k] = _sy[k] / _n[k];
                Kind[k] = _kind[k];
                Player[k] = _player[k];
                Valid[k] = true;
                PublishedAt[k] = now;
                // a NEW charge order ends the unit's lull (step 79: a new charge starts a big wave - its seam); step 78b: in the lull's first
                // LullChargeGraceSeconds it waits for them (the general's reflex at the sight of men easing back)
                if (InLull[k] && RhythmMath.NewCharge(was, _kind[k]))
                {
                    if (now - LullSince[k] < r.ChargeGraceSeconds - 1e-9)
                    {
                        if (double.IsNaN(ChargePendingAt[k]))
                        {
                            ChargePendingAt[k] = now;
                            ChargeDeferred++;
                        }
                    }
                    else EndLull(k, LullEnd.ChargeOrder, now, in r);
                }
            }
            // (2) the enemy unit: the most voted; else a live unit voting for this one (a held wall sees none of its attackers)
            for (int k = 0; k < N; k++)
            {
                if (_n[k] <= 0) continue;
                int best = -1, bestN = 0;
                for (int i = k * V; i < k * V + V; i++)
                {
                    int c = _voteKey[i];
                    if (c < 0 || _voteN[i] <= bestN || !Live(c, now)) continue;
                    best = c;
                    bestN = _voteN[i];
                }
                if (best < 0 && Live(Enemy[k], now)) best = Enemy[k];
                Enemy[k] = best;
            }
            for (int k = 0; k < N; k++)
            {
                if (_n[k] <= 0 || Enemy[k] >= 0) continue;
                int best = -1, bestN = 0;
                for (int j = 0; j < N; j++)
                {
                    if (j == k || _n[j] <= 0) continue;
                    for (int i = j * V; i < j * V + V; i++)
                        if (_voteKey[i] == k && _voteN[i] > bestN)
                        {
                            best = j;
                            bestN = _voteN[i];
                        }
                }
                Enemy[k] = best;
            }
            // the front toward the enemy and the front men's bar
            for (int k = 0; k < N; k++)
            {
                if (_n[k] <= 0) continue;
                int e = Enemy[k];
                double ux, uy;
                if (InLull[k])
                {
                    ux = LullUX[k];
                    uy = LullUY[k];
                }
                else if (e >= 0 && Valid[e])
                {
                    ux = CX[e] - CX[k];
                    uy = CY[e] - CY[k];
                    double len = Math.Sqrt(ux * ux + uy * uy);
                    if (!(len > 1e-6))
                    {
                        Front[k] = FrontBar[k] = double.NaN;
                        FrontMen[k] = 0;
                        continue;
                    }
                    ux /= len;
                    uy /= len;
                }
                else
                {
                    Front[k] = FrontBar[k] = double.NaN;
                    FrontMen[k] = 0;
                    continue;
                }
                UX[k] = ux;
                UY[k] = uy;
                var xs = _x[k];
                var ys = _y[k];
                var bs = _bar[k];
                int m = Math.Min(_n[k], M);
                if (xs == null || ys == null || bs == null) continue;
                for (int i = 0; i < m; i++) _alg[i] = xs[i] * ux + ys[i] * uy;
                RhythmMath.FrontOf(_alg, bs, m, r.LullFrontDepthMetres, _sorted, out double front, out double bar, out int men);
                Front[k] = front;
                FrontBar[k] = bar;
                FrontMen[k] = men;
                UnitBar[k] = RhythmMath.MeanWithin(_alg, bs, m, front, r.UnitDepthMetres, out int um); // step 78b: the men in the fight
                UnitMen[k] = um;
            }
            // (3) the lulls - each pair once (the lower key)
            for (int k = 0; k < N; k++)
            {
                if (_n[k] <= 0) continue;
                if (InLull[k])
                {
                    int p = LullPartner[k];
                    if (p < k && p >= 0 && InLull[p]) continue; // the pair is handled from its lower key
                    LookLull(k, p, now, in r);
                    continue;
                }
                int e = Enemy[k];
                if (e < 0 || e >= N || _n[e] <= 0 || InLull[e]) continue;
                if (Enemy[e] != k && Enemy[e] >= 0) continue; // e fights another unit
                if (e < k && Enemy[e] == k) continue; // the pair is handled from its lower key
                LookPair(k, e, now, in r);
            }
            for (int k = 0; k < N; k++)
                if (_n[k] > 0) ClearPeriod(k);
        }

        /// <summary>The gap between two fronts along the first one's direction (m; negative = overlapping).</summary>
        public double GapBetween(int a, int b)
        {
            if (double.IsNaN(Front[a]) || double.IsNaN(Front[b])) return double.NaN;
            // a's front point along u_a = Front[a]; b's front point along u_b = Front[b], i.e. along u_a ≈ −Front[b] (u_b ≈ −u_a)
            return -Front[b] - Front[a];
        }

        /// <summary>
        /// Step 78b - THE GAP FROM THE MEN between a pair (A = <paramref name="a"/>, along A's direction toward B - its lull's frozen one): every sampled
        /// man of both in the pair's frame, binned along the contact (<see cref="RhythmMath.MeasureGap"/>). Out: the mean / 10th / the hold quantile / 90th
        /// of the bins' gaps, the contact plane and each side's nearest men's median - world scalars along A's direction. False = no bin with both.
        /// </summary>
        public bool MeasurePair(int a, int b, out double mean, out double q10, out double qHold, out double q90, out double plane, out double frontA, out double frontB)
        {
            mean = q10 = qHold = q90 = plane = frontA = frontB = double.NaN;
            if (a < 0 || a >= N || b < 0 || b >= N) return false;
            var xa = _x[a];
            var ya = _y[a];
            var xb = _x[b];
            var yb = _y[b];
            if (xa == null || ya == null || xb == null || yb == null) return false;
            double ux = InLull[a] ? LullUX[a] : UX[a], uy = InLull[a] ? LullUY[a] : UY[a];
            if (double.IsNaN(ux) || double.IsNaN(uy)) return false;
            double mx = 0.5 * (CX[a] + CX[b]), my = 0.5 * (CY[a] + CY[b]);
            int na = Math.Min(_n[a], M), nb = Math.Min(_n[b], M);
            for (int i = 0; i < na; i++)
            {
                double dx = xa[i] - mx, dy = ya[i] - my;
                _ga[i] = dx * ux + dy * uy;
                _gl[i] = -dx * uy + dy * ux;
            }
            for (int i = 0; i < nb; i++)
            {
                double dx = xb[i] - mx, dy = yb[i] - my;
                _hb[i] = dx * ux + dy * uy;
                _hl[i] = -dx * uy + dy * ux;
            }
            int bins = RhythmMath.MeasureGap(_ga, _gl, na, _hb, _hl, nb, _maxA, _minB, _gaps, _mids, _fa, _fb, out mean, out q10, out qHold, out q90, out plane,
                out frontA, out frontB);
            if (bins <= 0) return false;
            double o = mx * ux + my * uy;
            plane += o;
            frontA += o;
            frontB += o;
            return true;
        }

        private void LookPair(int a, int b, double now, in RhythmRules r)
        {
            Pairs++;
            bool contact = ContactMen[a] + ContactMen[b] >= r.LullContactMen;
            if (contact)
            {
                if (double.IsNaN(ContactSince[a]) || now - ContactSeen[a] > RhythmMath.ContactGraceSeconds)
                {
                    // a new spell of contact: the clock starts, its roll, the rate's peak afresh
                    ContactSince[a] = ContactSince[b] = now;
                    ClockRoll[a] = ClockRoll[b] = Dice.NextDouble();
                    RateEma[a] = RateEma[b] = double.NaN;
                    RatePeak[a] = RatePeak[b] = 0;
                }
                ContactSeen[a] = ContactSeen[b] = now;
            }
            else if (!double.IsNaN(ContactSeen[a]) && now - ContactSeen[a] > RhythmMath.ContactGraceSeconds)
            {
                ContactSince[a] = ContactSince[b] = double.NaN;
            }
            if (!contact || double.IsNaN(ContactSince[a])) return;
            double since = now - ContactSince[a];
            // step 78b: the pair's attack rate in contact (blows a minute per man in contact), smoothed; its peak after the first seconds
            int cm = ContactMen[a] + ContactMen[b];
            double rate = cm > 0 ? (_att[a] + _att[b]) * 60.0 / (cm * RhythmMath.PublishSeconds) : 0;
            double ema = double.IsNaN(RateEma[a]) ? rate : RateEma[a] + RhythmMath.PublishSeconds / RhythmMath.RateTauSeconds * (rate - RateEma[a]);
            RateEma[a] = RateEma[b] = ema;
            if (since >= RhythmMath.RateWarmSeconds - 1e-9 && ema > RatePeak[a]) RatePeak[a] = RatePeak[b] = ema;
            var ga = BraceMath.Group(Kind[a]);
            var gb = BraceMath.Group(Kind[b]);
            double clock = RhythmMath.ClockSeconds(in r, RhythmMath.PairGroup(ga, gb), ClockRoll[a]);
            ClockAt[a] = ClockAt[b] = clock;
            bool fallen = RhythmMath.RateFallen(in r, ema, RatePeak[a]);
            int on = r.LullOn ? 1 : 0;
            if (RhythmMath.Spent(in r, UnitBar[a], ga, UnitBar[b], gb))
            {
                SpentLooks++;
                SpentLooksBy[on]++;
            }
            var trig = RhythmMath.Trigger(in r, since, clock, UnitBar[a], ga, UnitBar[b], gb, fallen);
            if (trig == LullTrigger.Count)
            {
                if (since >= clock - 1e-9) ClockWaitLooks++;
                return;
            }
            TriggerLooks[(int)trig]++;
            if (now < CooldownUntil[a] || now < CooldownUntil[b]) return;
            if (!r.LullOn)
            {
                WouldStart++;
                WouldStartBy[(int)trig]++;
                CooldownUntil[a] = CooldownUntil[b] = now + Math.Max(r.LullCooldownSeconds, r.LullMaxSeconds / 2); // OFF: one "would start" per spell
                ContactSince[a] = ContactSince[b] = double.NaN; // ... and the clock starts again with the next spell
                return;
            }
            if ((Player[a] || Player[b]) && !r.LullForPlayerUnits)
            {
                RefusedPlayer++;
                CooldownUntil[a] = CooldownUntil[b] = now + r.LullCooldownSeconds;
                return;
            }
            StartLull(a, b, now, in r, ga);
            StartLull(b, a, now, in r, gb);
            // one axis for the pair: B's direction is A's turned round
            LullUX[b] = -LullUX[a];
            LullUY[b] = -LullUY[a];
            LullTriggerOf[a] = LullTriggerOf[b] = trig;
            LullStartRate[a] = LullStartRate[b] = ema;
            LullStartRatePeak[a] = LullStartRatePeak[b] = RatePeak[a];
            LullStartContact[a] = LullStartContact[b] = since;
            LullStartUnitBarB[a] = UnitBar[b];
            LullStartUnitBarB[b] = UnitBar[a];
            if (MeasurePair(a, b, out double mean, out double q10, out double qh, out double q90, out double plane, out double fa, out double fb))
            {
                // step 78b: the lines from the men themselves - a meshed fight separates about its middle
                RhythmMath.StartLines(in r, plane, fa, fb, out double la, out double lb);
                LullLine[a] = la;
                LullLine[b] = -lb;
                LullFront0[a] = fa;
                LullFront0[b] = -fb;
                LullEase[a] = fa - la;
                LullEase[b] = lb - fb;
                LullStartGap[a] = LullStartGap[b] = mean;
                LullMaxGap[a] = LullMaxGap[b] = mean;
                SetGap(a, b, mean, q10, qh, q90);
            }
            else
            {
                // no bin with men of both (a held wall's attackers only, a thin sample): step 78's lines, each front eased back
                LullFront0[a] = Front[a];
                LullFront0[b] = Front[b];
                double gap = GapBetween(a, b);
                LullStartGap[a] = LullStartGap[b] = gap;
                LullMaxGap[a] = LullMaxGap[b] = gap;
                SetGap(a, b, double.NaN, double.NaN, double.NaN, double.NaN);
            }
            LullLine0[a] = LullLine[a];
            LullLine0[b] = LullLine[b];
            Started[StartedN++] = a;
        }

        private void SetGap(int a, int b, double mean, double q10, double qh, double q90)
        {
            GapMean[a] = GapMean[b] = mean;
            GapQ10[a] = GapQ10[b] = q10;
            GapHold[a] = GapHold[b] = qh;
            GapQ90[a] = GapQ90[b] = q90;
        }

        private void StartLull(int k, int partner, double now, in RhythmRules r, BraceOrder g)
        {
            InLull[k] = true;
            LullPartner[k] = partner;
            LullSince[k] = now;
            LullUX[k] = UX[k];
            LullUY[k] = UY[k];
            LullEase[k] = r.LullEaseMetres;
            LullLine[k] = Front[k] - r.LullEaseMetres;
            LullStartBar[k] = UnitBar[k]; // step 78b: the unit's men in the fight (step 78: the front men's)
            LullStartMen[k] = Men[k];
            LullGroup[k] = g;
            LullOpenedAfter[k] = double.NaN;
            ChargePendingAt[k] = double.NaN;
        }

        private void LookLull(int a, int b, double now, in RhythmRules r)
        {
            if (!r.LullOn)
            {
                EndPair(a, b, LullEnd.SwitchedOff, now, in r);
                return;
            }
            if ((Player[a] || (b >= 0 && Player[b])) && !r.LullForPlayerUnits)
            {
                EndPair(a, b, LullEnd.SwitchedOff, now, in r);
                return;
            }
            if (b < 0 || b >= N || !InLull[b] || !Live(b, now) || !Live(a, now))
            {
                EndPair(a, b, LullEnd.Lost, now, in r);
                return;
            }
            if (Men[a] < RhythmMath.BrokenShare * LullStartMen[a] || Men[b] < RhythmMath.BrokenShare * LullStartMen[b])
            {
                EndPair(a, b, LullEnd.Broken, now, in r);
                return;
            }
            if ((Enemy[a] >= 0 && Enemy[a] != b) || (Enemy[b] >= 0 && Enemy[b] != a))
            {
                EndPair(a, b, LullEnd.Lost, now, in r);
                return;
            }
            // step 78b: a charge order deferred by the grace ends the lull once the grace has passed (if it still stands)
            double age = now - LullSince[a];
            for (int i = 0; i < 2; i++)
            {
                int k = i == 0 ? a : b;
                if (double.IsNaN(ChargePendingAt[k]) || age < r.ChargeGraceSeconds - 1e-9) continue;
                ChargePendingAt[k] = double.NaN;
                if (BraceMath.Group(Kind[k]) == BraceOrder.Charge)
                {
                    EndPair(a, b, LullEnd.ChargeOrder, now, in r);
                    return;
                }
            }
            // step 78b: the gap from the men - measured, opened, the lines stepped back while it does not hold
            if (MeasurePair(a, b, out double mean, out double q10, out double qh, out double q90, out _, out double fa, out double fb))
            {
                SetGap(a, b, mean, q10, qh, q90);
                GapLooks++;
                GapMeanSum += mean;
                GapQ10Sum += q10;
                GapQ90Sum += q90;
                if (double.IsNaN(GapMin) || mean < GapMin) GapMin = mean;
                if (double.IsNaN(LullMaxGap[a]) || mean > LullMaxGap[a]) LullMaxGap[a] = LullMaxGap[b] = mean;
                if (RhythmMath.GapHolds(in r, qh))
                {
                    if (double.IsNaN(LullOpenedAfter[a]))
                    {
                        LullOpenedAfter[a] = LullOpenedAfter[b] = age;
                        if (OpenedN < N) Opened[OpenedN++] = a;
                    }
                }
                else if (age >= RhythmMath.SettleSeconds - 1e-9)
                {
                    bool stepped = StepBack(a, fa, in r);
                    stepped |= StepBack(b, -fb, in r);
                    if (stepped) LineSteps++;
                }
            }
            if (RhythmMath.Recovered(in r, UnitBar[a], LullStartBar[a], LullGroup[a]) || RhythmMath.Recovered(in r, UnitBar[b], LullStartBar[b], LullGroup[b]))
            {
                EndPair(a, b, LullEnd.Recovered, now, in r);
                return;
            }
            if (age >= r.LullMaxSeconds - 1e-9) EndPair(a, b, LullEnd.Timeout, now, in r);
        }

        /// <summary>Step 78b: a side standing at its line (its nearest men's median <paramref name="front"/> along its own direction within
        /// <see cref="RhythmMath.AtLineMetres"/> of it) steps the line back <see cref="RhythmMath.LineStepMetres"/>, at most
        /// <see cref="RhythmRules.MaxBackMetres"/> beyond where the lull first put it. A side still ahead of its line (walking, stuck) keeps it.</summary>
        private bool StepBack(int k, double front, in RhythmRules r)
        {
            if (double.IsNaN(front) || front > LullLine[k] + RhythmMath.AtLineMetres) return false;
            double floor = LullLine0[k] - r.MaxBackMetres;
            if (LullLine[k] <= floor + 1e-9) return false;
            LullLine[k] = Math.Max(floor, LullLine[k] - RhythmMath.LineStepMetres);
            if (!double.IsNaN(LullFront0[k])) LullEase[k] = LullFront0[k] - LullLine[k];
            else LullEase[k] += RhythmMath.LineStepMetres;
            return true;
        }

        private void EndPair(int a, int b, LullEnd why, double now, in RhythmRules r)
        {
            if (a >= 0 && a < N && InLull[a]) EndLull(a, why, now, in r);
            else if (b >= 0 && b < N && InLull[b]) EndLull(b, why, now, in r);
        }

        /// <summary>Ends the unit's lull and its partner's (counted once, the pair's numbers recorded). Step 79's waves call it with
        /// <see cref="LullEnd.Wave"/>. False = not in a lull.</summary>
        public bool EndLull(int key, LullEnd why, double now) => EndLull(key, why, now, default);

        private bool EndLull(int key, LullEnd why, double now, in RhythmRules r)
        {
            if (key < 0 || key >= N || !InLull[key]) return false;
            int p = LullPartner[key];
            double seconds = double.IsNaN(LullSince[key]) ? 0 : Math.Max(0, now - LullSince[key]);
            double opened = double.IsNaN(LullMaxGap[key]) || double.IsNaN(LullStartGap[key]) ? double.NaN : LullMaxGap[key] - LullStartGap[key];
            double cool = r.LullCooldownSeconds;
            InLull[key] = false;
            LullPartner[key] = -1;
            CooldownUntil[key] = now + cool;
            AfterLull(key);
            if (p >= 0 && p < N && InLull[p] && LullPartner[p] == key)
            {
                InLull[p] = false;
                LullPartner[p] = -1;
                CooldownUntil[p] = now + cool;
                AfterLull(p);
            }
            if (EndedN < N)
            {
                Ended[EndedN] = key;
                EndedWhy[EndedN] = why;
                EndedSeconds[EndedN] = seconds;
                EndedOpened[EndedN] = opened;
                EndedOpenedAfter[EndedN] = LullOpenedAfter[key];
                EndedTrigger[EndedN] = LullTriggerOf[key];
                EndedN++;
            }
            return true;
        }

        /// <summary>Step 78b: after a lull the breather clock starts again with the next spell of contact (a new roll, the rate's peak afresh).</summary>
        private void AfterLull(int k)
        {
            ContactSince[k] = ContactSeen[k] = double.NaN;
            ClockRoll[k] = RateEma[k] = double.NaN;
            RatePeak[k] = 0;
            ChargePendingAt[k] = double.NaN;
        }

        /// <summary>Every lull ends now (switched off / mission end).</summary>
        public int EndAll(LullEnd why, double now)
        {
            int n = 0;
            EndedN = 0;
            for (int k = 0; k < N; k++)
                if (InLull[k] && EndLull(k, why, now, default)) n++;
            return n;
        }
    }

    /// <summary>Step 78 per mission: the measurement (ON and OFF), the swaps' and the lulls' counts and the [summary] "battle rhythm" lines.</summary>
    public sealed class RhythmStats
    {
        // ---- rotation
        /// <summary>Tired front men looked at (not in a swap or its cooldown), by rotation OFF / ON.</summary>
        public readonly long[] Asked = new long[2];

        /// <summary>... with a fresher mate right behind ready to swap (a CHANCE) - OFF they are only counted.</summary>
        public readonly long[] Chances = new long[2];

        public readonly long[] Refused = new long[(int)RotationRefusal.Count];
        public readonly long[] Ends = new long[(int)RotationEnd.Count];
        public long Started, Swapped, SwapFailed, NoSlotSwap, InLull, RotatedMen;
        public double TiredBarSum, FreshBarSum, WaitSum, WaitMax, DoneSecondsSum;
        public long WaitN, DoneN;

        /// <summary>Looks at men in contact and their bars (the front's freshness), by rotation OFF / ON.</summary>
        public readonly long[] ContactLooks = new long[2];
        public readonly double[] ContactBarSum = new double[2];

        /// <summary>Looks at a tired man holding while his fresh mate came up - the line kept its man (the hold frames: the hook's).</summary>
        public long HoldFrames, MoveFrames, ClampFrames, RestClamps;

        // ---- lull
        public long Lulls, LullUnits, MenWalkedBack, SlotShifts;
        public readonly long[] LullEnds = new long[(int)LullEnd.Count];
        public double LullStartBarSum, LullSecondsSum, LullSecondsMax, OpenedSum, OpenedMax;
        public long LullStartBarN, OpenedN, LullEndsN;
        public double LullStartBarMin = double.NaN, LullStartBarMax = double.NaN;

        // ---- step 78b: by trigger, the time to open the gap, the attack rate at the start
        public readonly long[] LullsBy = new long[(int)LullTrigger.Count];
        public double OpenedSecondsSum, OpenedSecondsMax, StartRateSum, StartRatePeakSum, StartContactSum;
        public long OpenedSecondsN, NeverOpened, StartRateN;

        // ---- cost
        public long Ticks, Looks, Scans, Steers, Errors;
        public double LookCostMs, SteerCostMs, PublishCostMs;

        public void AddContact(bool on, double bar)
        {
            int i = on ? 1 : 0;
            ContactLooks[i]++;
            ContactBarSum[i] += double.IsNaN(bar) ? 1 : bar;
        }

        public void AddStartBar(double bar)
        {
            if (double.IsNaN(bar)) return;
            LullStartBarSum += bar;
            LullStartBarN++;
            if (double.IsNaN(LullStartBarMin) || bar < LullStartBarMin) LullStartBarMin = bar;
            if (double.IsNaN(LullStartBarMax) || bar > LullStartBarMax) LullStartBarMax = bar;
        }

        /// <summary>Step 78b: a lull's end with the seconds its gap took to open (NaN = it never held).</summary>
        public void AddLullEnd(LullEnd why, double seconds, double opened, double openedAfter)
        {
            AddLullEnd(why, seconds, opened);
            if (double.IsNaN(openedAfter))
            {
                NeverOpened++;
                return;
            }
            OpenedSecondsSum += openedAfter;
            OpenedSecondsN++;
            if (openedAfter > OpenedSecondsMax) OpenedSecondsMax = openedAfter;
        }

        /// <summary>Step 78b: a lull's start - its trigger, the pair's attack rate then and its peak, the contact time.</summary>
        public void AddLullStart(LullTrigger why, double rate, double peak, double contactSeconds)
        {
            if (why < LullTrigger.Count) LullsBy[(int)why]++;
            if (!double.IsNaN(contactSeconds)) StartContactSum += contactSeconds;
            if (double.IsNaN(rate) || !(peak > 0)) return;
            StartRateSum += rate;
            StartRatePeakSum += peak;
            StartRateN++;
        }

        public void AddLullEnd(LullEnd why, double seconds, double opened)
        {
            LullEnds[(int)why]++;
            LullEndsN++;
            LullSecondsSum += seconds;
            if (seconds > LullSecondsMax) LullSecondsMax = seconds;
            if (!double.IsNaN(opened))
            {
                OpenedSum += opened;
                OpenedN++;
                if (opened > OpenedMax) OpenedMax = opened;
            }
        }

        public void AddSummaryLines(List<string> lines, in RhythmRules r, RhythmUnits t)
        {
            var sb = new System.Text.StringBuilder(640);
            sb.Append("battle rhythm (step 78) - ROTATION ").Append(r.RotationOn ? "ON" : "OFF (" + r.RotationOffBecause + ")").Append(": tired front men looked at ON ")
              .Append(Asked[1]).Append(" | OFF ").Append(Asked[0]).Append("; chances (a fresher mate right behind) ON ").Append(Chances[1]).Append(" | OFF ").Append(Chances[0])
              .Append("; swaps asked ").Append(Started).Append(", done ").Append(Ends[(int)RotationEnd.Done]).Append(" (slots swapped ").Append(Swapped)
              .Append(", no slots to swap ").Append(NoSlotSwap).Append(", the game refused ").Append(SwapFailed).Append("), ended:");
            for (int i = 0; i < (int)RotationEnd.Count; i++)
                if (Ends[i] > 0) sb.Append(' ').Append(RhythmMath.EndName((RotationEnd)i)).Append(' ').Append(Ends[i]).Append(',');
            sb.Append(" refused:");
            for (int i = 0; i < (int)RotationRefusal.Count; i++)
                if (Refused[i] > 0) sb.Append(' ').Append(RhythmMath.RefusalName((RotationRefusal)i)).Append(' ').Append(Refused[i]).Append(',');
            sb.Append(" the bar at the swap: tired ").Append(Pct(Started > 0 ? TiredBarSum / Started : double.NaN)).Append(" vs fresh ")
              .Append(Pct(Started > 0 ? FreshBarSum / Started : double.NaN)).Append("; the tired man held until the fresh one was beside him: mean ")
              .Append(F2(WaitN > 0 ? WaitSum / WaitN : double.NaN)).Append(" s (max ").Append(F2(WaitMax)).Append(" s), a whole swap ")
              .Append(F2(DoneN > 0 ? DoneSecondsSum / DoneN : double.NaN)).Append(" s; men rotated ").Append(RotatedMen).Append(", during lulls ").Append(InLull)
              .Append("; the bar of men IN CONTACT ON ").Append(Pct(ContactLooks[1] > 0 ? ContactBarSum[1] / ContactLooks[1] : double.NaN)).Append(" (").Append(ContactLooks[1])
              .Append(" looks) | OFF ").Append(Pct(ContactLooks[0] > 0 ? ContactBarSum[0] / ContactLooks[0] : double.NaN)).Append(" (").Append(ContactLooks[0])
              .Append(" looks); input frames: held ").Append(HoldFrames).Append(", walked ").Append(MoveFrames).Append(", clamped ").Append(ClampFrames);
            lines.Add(sb.ToString());

            sb.Clear();
            sb.Append("battle rhythm (step 78) - LULLS ").Append(r.LullOn ? "ON" : "OFF (" + r.LullOffBecause + ")").Append(": started ").Append(Lulls)
              .Append(" (unit bars - the men in the fight - at the start mean ").Append(Pct(LullStartBarN > 0 ? LullStartBarSum / LullStartBarN : double.NaN)).Append(", ")
              .Append(Pct(LullStartBarMin)).Append("-").Append(Pct(LullStartBarMax)).Append("), lasted mean ").Append(F1(LullEndsN > 0 ? LullSecondsSum / LullEndsN : double.NaN))
              .Append(" s (max ").Append(F1(LullSecondsMax)).Append(" s), the gap opened mean ").Append(F1(OpenedN > 0 ? OpenedSum / OpenedN : double.NaN)).Append(" m (max ")
              .Append(F1(OpenedMax)).Append(" m), ended:");
            for (int i = 0; i < (int)LullEnd.Count; i++)
                if (LullEnds[i] > 0) sb.Append(' ').Append(RhythmMath.LullEndName((LullEnd)i)).Append(' ').Append(LullEnds[i]).Append(',');
            sb.Append(" men walked back ").Append(MenWalkedBack).Append(", wall slots shifted ").Append(SlotShifts).Append(" looks; both fronts SPENT in contact ON ")
              .Append(t.SpentLooksBy[1]).Append(" | OFF ").Append(t.SpentLooksBy[0]).Append(" unit looks (0.5 s each); would have started OFF ").Append(t.WouldStart)
              .Append("; refused for your units (LullForPlayerUnits off) ").Append(t.RefusedPlayer).Append("; pairs looked at ").Append(t.Pairs)
              .Append("; cost: looks ").Append(F2(LookCostMs)).Append(" ms (").Append(Looks).Append(", scans ").Append(Scans).Append("), steering ").Append(F2(SteerCostMs))
              .Append(" ms (").Append(Steers).Append("), the table ").Append(F2(PublishCostMs)).Append(" ms over ").Append(t.Publishes).Append(" publishes; errors ").Append(Errors);
            lines.Add(sb.ToString());

            sb.Clear();
            sb.Append("battle rhythm (step 78b) - THE LULL'S TRIGGER AND GAP: started by the breather clock ").Append(LullsBy[(int)LullTrigger.Clock])
              .Append(", by both units spent ").Append(LullsBy[(int)LullTrigger.Tired]).Append(" (after ").Append(F1(Lulls > 0 ? StartContactSum / Lulls : double.NaN))
              .Append(" s in contact on average; the blows in contact at the start ").Append(F1(StartRateN > 0 ? StartRateSum / StartRateN : double.NaN)).Append(" vs the spell's peak ")
              .Append(F1(StartRateN > 0 ? StartRatePeakSum / StartRateN : double.NaN)).Append(" a man-minute); unit looks a trigger held: the clock ")
              .Append(t.TriggerLooks[(int)LullTrigger.Clock]).Append(", spent ").Append(t.TriggerLooks[(int)LullTrigger.Tired]).Append("; the clock run but nobody worn or slowed ")
              .Append(t.ClockWaitLooks).Append(" looks; would have started OFF - the clock ").Append(t.WouldStartBy[(int)LullTrigger.Clock]).Append(", spent ")
              .Append(t.WouldStartBy[(int)LullTrigger.Tired]).Append("; THE GAP measured from the men in lulls (the nearest men of both sides, ").Append(F1(RhythmMath.GapBinMetres))
              .Append(" m bins along the contact, ").Append(t.GapLooks).Append(" publishes): mean ").Append(F1(t.GapLooks > 0 ? t.GapMeanSum / t.GapLooks : double.NaN))
              .Append(" m (p10 ").Append(F1(t.GapLooks > 0 ? t.GapQ10Sum / t.GapLooks : double.NaN)).Append(", p90 ").Append(F1(t.GapLooks > 0 ? t.GapQ90Sum / t.GapLooks : double.NaN))
              .Append(", the lowest ").Append(F1(t.GapMin)).Append(" m) against ").Append(F1(r.GapMetres)).Append(" m wanted; it held (its ")
              .Append((100 * RhythmMath.GapQuantile).ToString("0", CultureInfo.InvariantCulture)).Append("th percentile within ").Append(F1(RhythmMath.GapSlackMetres))
              .Append(" m of it) after mean ").Append(F1(OpenedSecondsN > 0 ? OpenedSecondsSum / OpenedSecondsN : double.NaN)).Append(" s (max ").Append(F1(OpenedSecondsN > 0 ? OpenedSecondsMax : double.NaN))
              .Append(" s, ").Append(OpenedSecondsN).Append(" lulls), never ").Append(NeverOpened).Append("; lines stepped back while it did not hold ").Append(t.LineSteps)
              .Append(" times; charge orders in a lull's first ").Append(F1(r.ChargeGraceSeconds)).Append(" s deferred ").Append(t.ChargeDeferred);
            lines.Add(sb.ToString());
        }

        public static string Pct(double v) => double.IsNaN(v) ? "-" : (100 * v).ToString("0", CultureInfo.InvariantCulture) + "%";

        public static string F1(double v) => double.IsNaN(v) ? "-" : v.ToString("0.0", CultureInfo.InvariantCulture);

        public static string F2(double v) => double.IsNaN(v) ? "-" : v.ToString("0.00", CultureInfo.InvariantCulture);
    }
}

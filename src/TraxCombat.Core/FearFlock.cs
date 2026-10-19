using System;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>Step 67: a unit's order as the fear flock reads it - the drift cap's share and the charge's loose mass.</summary>
    public enum FearOrder
    {
        /// <summary>Stop / hold, move, follow, fall back, no order: the slots stand - the drift under <see cref="FearRules.DriftHoldPercent"/>.</summary>
        Hold,

        /// <summary>A shield wall, square or circle (whatever the order but a charge): the slots stand, the drift as a hold.</summary>
        Wall,

        /// <summary>Advance (the AI's advance on a Move too): the slots walk at the enemy - the full drift.</summary>
        Advance,

        /// <summary>A charge (charge, charge a target, the AI's tactical charge, attack a gate): no slots - the loose mass.</summary>
        Charge,

        /// <summary>A retreat order: the game moves them - the flock leaves them alone.</summary>
        Retreat,

        Count,
    }

    /// <summary>Step 67: where an AI man's fear has him.</summary>
    public enum FearPhase
    {
        /// <summary>Below the start (or not looked at): the game's own AI drives him - his formation place exactly.</summary>
        Calm,

        /// <summary>Afraid: his place blends from his slot toward the shelter (behind a comrade when he has none in front); walked there.</summary>
        Flocking,

        /// <summary>Afraid but covered by a FRESH comrade in front: he holds that spot for a while.</summary>
        Anchored,
    }

    /// <summary>Step 67: why a fear-flock spell ended.</summary>
    public enum FearEnd
    {
        /// <summary>His fear fell below the start (minus the hysteresis).</summary>
        Calm,

        /// <summary>A gate took him (a rider, busy, no formation, the player took him, a held wall...).</summary>
        Excluded,

        /// <summary>His fever presses (courage's step 49b - the charge goes in).</summary>
        FeverPress,

        /// <summary>Courage's surge or the punish's window sent him in.</summary>
        Surge,

        /// <summary>His unit is a shield wall whose morale holds - the wall keeps him.</summary>
        WallHolds,

        /// <summary>A retreat order - the game moves him.</summary>
        Retreat,

        /// <summary>FearFlockEnabled or the master switch off.</summary>
        SwitchedOff,

        LeftField,
        MissionEnd,
        Error,

        /// <summary>Step 69: his side wins near him - the fear his morale and tiredness gave him is calmed away.</summary>
        Winning,

        /// <summary>Step 69: no enemy within FearThreatMetres - he follows his order.</summary>
        NoThreat,

        Count,
    }

    /// <summary>
    /// Step 67's settings, read live (AI_NOTES "Step 67", DESIGN decision 60): the master switch FIRST, FearFlockEnabled; the fear's
    /// morale and tiredness lines and weights, discipline by level, the unit's share, the thin line, the pull's start and full, the
    /// shelter, the drift cap, the cover and the anchor, the sprint back, the wall's break and the charge's loose mass.
    /// </summary>
    public readonly struct FearRules
    {
        public FearRules(bool modEnabled, bool enabled, bool athleticsEnabled, int moraleCalm = 60, int moraleBroken = 15, int freshPercent = 75,
            int moraleWeightPercent = 100, int tiredWeightPercent = 100, int disciplineLevelLow = 6, int disciplineLevelHigh = 26, int disciplineMaxPercent = 70,
            int unitSharePercent = 50, int thinLinePercent = 25, int startPercent = 15, int fullPercent = 80, double shelterBackMetres = 4, double shelterMaxMetres = 12,
            double driftMetresPerMinute = 6, int driftHoldPercent = 25, double coverWidthMetres = 0.9, int anchorFreshPercent = 60, double anchorSeconds = 20,
            double sprintMetres = 8, int sprintChancePercent = 10, int sprintKeepPercent = 50, int wallBreakMorale = 30, double chargeLooseMetres = 12,
            bool winCalms = true, int winEvenOddsPercent = 100, int winClearOddsPercent = 200, int winShakenPercent = 50, double threatMetres = 12,
            int mopUpWinPercent = 50, bool bandEnabled = true, int bandRanksMin = 4, int bandRanksMax = 6, double bandFileMetres = 1.0, double bandRankMetres = 1.0,
            double bandMaxWidthMetres = 40, double bandGapMetres = 1.5, double strikeReachMetres = 2.5, double strikeLeashMetres = 2.0)
        {
            ModEnabled = modEnabled;
            FlockEnabled = enabled;
            AthleticsEnabled = athleticsEnabled;
            MoraleCalm = Pct(moraleCalm);
            MoraleBroken = Pct(moraleBroken);
            FreshPercent = freshPercent < 1 ? 1 : freshPercent > 100 ? 100 : freshPercent;
            MoraleWeightPercent = Pct(moraleWeightPercent);
            TiredWeightPercent = Pct(tiredWeightPercent);
            DisciplineLevelLow = disciplineLevelLow < 0 ? 0 : disciplineLevelLow;
            DisciplineLevelHigh = disciplineLevelHigh < 0 ? 0 : disciplineLevelHigh;
            DisciplineMaxPercent = Pct(disciplineMaxPercent);
            UnitSharePercent = Pct(unitSharePercent);
            ThinLinePercent = Pct(thinLinePercent);
            StartPercent = startPercent < 0 ? 0 : startPercent > 95 ? 95 : startPercent;
            FullPercent = fullPercent < 5 ? 5 : fullPercent > 100 ? 100 : fullPercent;
            ShelterBackMetres = Clamp(shelterBackMetres, 0, 20);
            ShelterMaxMetres = Clamp(shelterMaxMetres, 1, 50);
            DriftMetresPerMinute = Clamp(driftMetresPerMinute, 0, 60);
            DriftHoldPercent = Pct(driftHoldPercent);
            CoverWidthMetres = Clamp(coverWidthMetres, 0.3, 3);
            AnchorFreshPercent = Pct(anchorFreshPercent);
            AnchorSeconds = Clamp(anchorSeconds, 0, 120);
            SprintMetres = Clamp(sprintMetres, 0, 50);
            SprintChancePercent = Pct(sprintChancePercent);
            SprintKeepPercent = Pct(sprintKeepPercent);
            WallBreakMorale = Pct(wallBreakMorale);
            ChargeLooseMetres = Clamp(chargeLooseMetres, 2, 50);
            WinCalms = winCalms;
            WinEvenOddsPercent = winEvenOddsPercent < 0 ? 0 : winEvenOddsPercent > 1000 ? 1000 : winEvenOddsPercent;
            WinClearOddsPercent = winClearOddsPercent < 1 ? 1 : winClearOddsPercent > 1000 ? 1000 : winClearOddsPercent;
            WinShakenPercent = Pct(winShakenPercent);
            ThreatMetres = Clamp(threatMetres, 0, 60);
            MopUpWinPercent = Pct(mopUpWinPercent);
            BandEnabled = bandEnabled;
            BandRanksMin = bandRanksMin < 1 ? 1 : bandRanksMin > 20 ? 20 : bandRanksMin;
            BandRanksMax = bandRanksMax < BandRanksMin ? BandRanksMin : bandRanksMax > 20 ? 20 : bandRanksMax;
            BandFileMetres = Clamp(bandFileMetres, 0.5, 3);
            BandRankMetres = Clamp(bandRankMetres, 0.5, 3);
            BandMaxWidthMetres = Clamp(bandMaxWidthMetres, 5, 150);
            BandGapMetres = Clamp(bandGapMetres, 0, 10);
            StrikeReachMetres = Clamp(strikeReachMetres, 0, 6);
            StrikeLeashMetres = Clamp(strikeLeashMetres, 0.5, 10);
        }

        private static int Pct(int v) => v < 0 ? 0 : v > 100 ? 100 : v;

        private static double Clamp(double v, double min, double max) => double.IsNaN(v) || v < min ? min : v > max ? max : v;

        public static FearRules From(TraxSettings s) => new FearRules(s.ModEnabled, s.FearFlockEnabled, s.AthleticsEnabled, s.FearMoraleCalm, s.FearMoraleBroken,
            s.FearFreshPercent, s.FearMoraleWeightPercent, s.FearTiredWeightPercent, s.DisciplineLevelLow, s.DisciplineLevelHigh, s.DisciplineMaxPercent,
            s.FearUnitSharePercent, s.FearThinLinePercent, s.FearStartPercent, s.FearFullPercent, s.FearShelterBackMetres, s.FearShelterMaxMetres,
            s.FearDriftMetresPerMinute, s.FearDriftHoldPercent, s.FearCoverWidthMetres, s.FearAnchorFreshPercent, s.FearAnchorSeconds, s.FearSprintMetres,
            s.FearSprintChancePercent, s.FearSprintKeepPercent, s.FearWallBreakMorale, s.FearChargeLooseMetres,
            s.FearWinCalms, s.FearWinEvenOddsPercent, s.FearWinClearOddsPercent, s.FearWinShakenPercent, s.FearThreatMetres, s.FearMopUpWinPercent,
            s.FearBandEnabled, s.FearBandRanksMin, s.FearBandRanksMax, s.FearBandFileMetres, s.FearBandRankMetres, s.FearBandMaxWidthMetres, s.FearBandGapMetres,
            s.FearStrikeReachMetres, s.FearStrikeLeashMetres);

        public bool ModEnabled { get; }

        /// <summary>FearFlockEnabled (the switch itself).</summary>
        public bool FlockEnabled { get; }

        /// <summary>The Athletics system - only the tiredness term (and the anchor's freshness) needs it.</summary>
        public bool AthleticsEnabled { get; }

        /// <summary>The master switch FIRST, then FearFlockEnabled.</summary>
        public bool Enabled => ModEnabled && FlockEnabled;

        /// <summary>Which switch holds it off (null = on).</summary>
        public string? OffBecause => !ModEnabled ? "ModEnabled" : !FlockEnabled ? "FearFlockEnabled" : null;

        public int MoraleCalm { get; }
        public int MoraleBroken { get; }
        public int FreshPercent { get; }
        public int MoraleWeightPercent { get; }
        public int TiredWeightPercent { get; }
        public int DisciplineLevelLow { get; }
        public int DisciplineLevelHigh { get; }
        public int DisciplineMaxPercent { get; }
        public int UnitSharePercent { get; }
        public int ThinLinePercent { get; }
        public int StartPercent { get; }
        public int FullPercent { get; }
        public double ShelterBackMetres { get; }
        public double ShelterMaxMetres { get; }
        public double DriftMetresPerMinute { get; }
        public int DriftHoldPercent { get; }
        public double CoverWidthMetres { get; }
        public int AnchorFreshPercent { get; }
        public double AnchorSeconds { get; }
        public double SprintMetres { get; }
        public int SprintChancePercent { get; }
        public int SprintKeepPercent { get; }
        public int WallBreakMorale { get; }
        public double ChargeLooseMetres { get; }

        /// <summary>Step 69: FearWinCalms - his side winning near him fades his fear (off = step 67 exactly).</summary>
        public bool WinCalms { get; }

        /// <summary>Step 69: the local odds (courage's, ×100) at which winning starts to calm him; below it he is losing - step 67 exactly.</summary>
        public int WinEvenOddsPercent { get; }

        /// <summary>Step 69: the local odds (×100) at which his fear is gone.</summary>
        public int WinClearOddsPercent { get; }

        /// <summary>Step 69: how much a broken enemy near him (morale at the broken line, routing) calms him at even odds or better.</summary>
        public int WinShakenPercent { get; }

        /// <summary>Step 69: no enemy within this many metres = no pull (0 = always a threat - step 67).</summary>
        public double ThreatMetres { get; }

        /// <summary>Step 69: a charging unit whose men win at least this (its mean win, %) is in its MOP-UP - no loose-mass leash (0 = never).</summary>
        public int MopUpWinPercent { get; }

        /// <summary>Step 70: FearBandEnabled - the flock's place is a wide BAND along the enemy's front (men keep their place along it, compressed
        /// only in depth, at most the ranks below); off = step 67's point shelter.</summary>
        public bool BandEnabled { get; }

        /// <summary>Step 70: the band's depth in ranks - <see cref="BandRanksMin"/> at a small pull, a line to <see cref="BandRanksMax"/> at the full pull.</summary>
        public int BandRanksMin { get; }

        public int BandRanksMax { get; }

        /// <summary>Step 70: the width of one file (m) and the depth of one rank (m) in the band.</summary>
        public double BandFileMetres { get; }

        public double BandRankMetres { get; }

        /// <summary>Step 70: the band is never wider than this (m).</summary>
        public double BandMaxWidthMetres { get; }

        /// <summary>Step 70: the band's front stands this far short of the enemy's front (m); the pull pushes it back further (ShelterBackMetres × pull).</summary>
        public double BandGapMetres { get; }

        /// <summary>Step 70: a front man (nobody covering him) with an enemy within this (m) is let go to strike - his AI steps in and swings
        /// (0 = never); walked back once more than <see cref="StrikeLeashMetres"/> ahead of his place.</summary>
        public double StrikeReachMetres { get; }

        public double StrikeLeashMetres { get; }

        private static string N(double v, string f = "0.##") => v.ToString(f, CultureInfo.InvariantCulture);

        /// <summary>The settings sentence for the mission-start line, a change and the summary - with the worked example's anchors.</summary>
        public string Describe()
        {
            if (!Enabled) return "fear flock OFF (" + OffBecause + ") - measured only";
            var r = this;
            double looter = FearMath.Pull(in r, FearMath.Own(in r, 20, 0.5, 0));
            double legion = FearMath.Pull(in r, FearMath.Own(in r, 20, 0.5, FearMath.Discipline(in r, 26, false)));
            return "fear flock: fear = 1 - (1 - " + MoraleWeightPercent + "% x morale below " + MoraleCalm + " (full at " + MoraleBroken + ")) x (1 - " + TiredWeightPercent
                + "% x the bar below " + FreshPercent + "%), x (1 - discipline: 0 to level " + DisciplineLevelLow + ", " + DisciplineMaxPercent + "% from level " + DisciplineLevelHigh
                + "), " + UnitSharePercent + "% the unit's, +" + ThinLinePercent + "% with nobody behind him; the pull from " + StartPercent + "% to " + FullPercent
                + "% fear (a looter at morale 20 and half a bar " + N(looter, "0.00") + ", a legionary " + N(legion, "0.00") + "); the shelter " + N(ShelterBackMetres, "0.#")
                + " m behind the unit's centre x its pull, at most " + N(ShelterMaxMetres, "0") + " m from his slot, drifting back at most " + N(DriftMetresPerMinute, "0.#")
                + " m a minute (" + DriftHoldPercent + "% under a hold / a wall); cover within " + N(CoverWidthMetres, "0.0#") + " m of the line, a comrade above "
                + AnchorFreshPercent + "% of his bar anchors " + N(AnchorSeconds, "0") + " s; sprint back beyond " + N(SprintMetres, "0") + " m or alone (+" + SprintChancePercent
                + "% a look beyond 3 m), keeping " + SprintKeepPercent + "%; walls break below morale " + WallBreakMorale + "; a charge's men roam " + N(ChargeLooseMetres, "0")
                + " m x (1 - the pull)" + DescribeWin() + DescribeBand();
        }

        /// <summary>Step 70's clause of the sentence.</summary>
        public string DescribeBand()
        {
            string band = BandEnabled
                ? "; a WIDE BAND (step 70) along the enemy's front, not a point: as wide as the enemy's frontage or his men in ranks of " + N(BandFileMetres, "0.0#")
                  + " m files, whichever is wider (at most " + N(BandMaxWidthMetres, "0") + " m), " + BandRanksMin + " ranks deep at a small pull to " + BandRanksMax
                  + " at the full pull (" + N(BandRankMetres, "0.0#") + " m a rank), its front " + N(BandGapMetres, "0.0#")
                  + " m short of the enemy + the pull's push back; each man keeps his place along the front and is compressed only in depth; in a full file the men "
                  + "behind its ranks slide out toward the band's ends"
                : "; the point shelter of step 67 (FearBandEnabled off)";
            string strike = StrikeReachMetres > 0
                ? "; a front man with an enemy within " + N(StrikeReachMetres, "0.0#") + " m is let go to strike, walked back beyond " + N(StrikeLeashMetres, "0.0#")
                  + " m ahead of his place"
                : "; front men never let go to strike (FearStrikeReachMetres 0)";
            return band + strike;
        }

        /// <summary>Step 69's clause of the sentence.</summary>
        public string DescribeWin()
        {
            string win = WinCalms
                ? "; WINNING calms (step 69): his fear x (1 - win), win 0 at local odds " + N(WinEvenOddsPercent / 100.0, "0.0#") + " (losing below: unchanged) to 1 at "
                  + N(WinClearOddsPercent / 100.0, "0.0#") + ", a broken / routing enemy near him " + WinShakenPercent + "% more, the unit's win " + UnitSharePercent + "%"
                : "; winning calms OFF (FearWinCalms)";
            string threat = ThreatMetres > 0 ? "; no pull without an enemy within " + N(ThreatMetres, "0.#") + " m" : "; a pull without an enemy near too (FearThreatMetres 0)";
            string mop = MopUpWinPercent > 0 && WinCalms ? "; a charge's MOP-UP (its men's win " + MopUpWinPercent + "%+) - no loose-mass leash" : "; no charge mop-up";
            return win + threat + mop;
        }
    }

    /// <summary>
    /// Step 67 pure math (AI_NOTES "Step 67", MECHANICS §9b): FEAR = 1 − (1 − a·m)(1 − b·t) - m the morale share below the calm line, t the
    /// tiredness share below the fresh line - × (1 − DISCIPLINE by level), + the thin line's share, mixed with the unit's; the PULL p =
    /// clamp((fear − start) ÷ (full − start)); his PLACE = his slot + p × (the shelter − his slot) (at most the max from his slot), behind a
    /// comrade when none covers him; the SHELTER = the unit's centre + away from the enemy near it × back × the unit's mean pull, its backward
    /// motion capped by order; the cover / behind tests on courage's scan; the charge's loose radius; the steering stick.
    /// </summary>
    public static class FearMath
    {
        // ---- plumbing (documented constants, not settings)

        /// <summary>A flocking man calms only below the start by this much fear (no flicker at the edge).</summary>
        public const double CalmHysteresis = 0.05;

        /// <summary>A comrade counts in front / behind only past this much along the line to the enemy (m).</summary>
        public const double CoverMinAlongMetres = 0.3;

        /// <summary>"Somebody behind him": a comrade 0.3-3 m back along the line from the enemy, within 1.5 m of it.</summary>
        public const double BehindDepthMetres = 3.0;

        public const double BehindWidthMetres = 1.5;

        /// <summary>The spot behind a comrade: this far behind him, away from the enemy (m).</summary>
        public const double BehindSpotMetres = 1.0;

        /// <summary>He has arrived within this of his place (stands); he walks again beyond <see cref="MoveAgainMetres"/>.</summary>
        public const double ArriveMetres = 0.7;

        public const double MoveAgainMetres = 1.5;

        /// <summary>A place within this of his slot leaves his feet to his own AI (a small pull - the slot is near enough; no tug of war with the
        /// game's own walk to the slot). An anchored man keeps the hold.</summary>
        public const double SlotToleranceMetres = 1.0;

        /// <summary>His place is near enough his slot for the game's own AI to keep him (see <see cref="SlotToleranceMetres"/>).</summary>
        public static bool NearSlot(double tx, double ty, double slotX, double slotY) =>
            (tx - slotX) * (tx - slotX) + (ty - slotY) * (ty - slotY) < SlotToleranceMetres * SlotToleranceMetres;

        /// <summary>The stick: full beyond this distance, down to <see cref="MinStick"/> near his place (a walk, not a dash).</summary>
        public const double SlowMetres = 2.5;

        public const double MinStick = 0.35;

        /// <summary>The steering vector is refreshed every this many seconds per flocker (his frame read then).</summary>
        public const double SteerSeconds = 0.1;

        /// <summary>A sprint back at most once in this many seconds; the chance sprint beyond <see cref="ChanceSprintMetres"/>.</summary>
        public const double SprintCooldownSeconds = 10;

        public const double ChanceSprintMetres = 3;

        /// <summary>"Alone": fewer than this many mates in courage's circle while an enemy is in it.</summary>
        public const int AloneMates = 2;

        /// <summary>The charge's loose radius never below this (m); a pulled man is let go again within it, pulled beyond it + <see cref="ChargeAgainMetres"/>.</summary>
        public const double MinLooseMetres = 1.5;

        public const double ChargeAgainMetres = 2.0;

        /// <summary>The unit table: 8 teams × 16 formations; its means published every <see cref="PublishSeconds"/> (one round of courage's looks).</summary>
        public const int Teams = 8;

        public const int Formations = 16;

        public const int UnitCount = Teams * Formations;

        public const double PublishSeconds = 0.5;

        /// <summary>A published unit is stale (not used) after this long without a look.</summary>
        public const double StaleSeconds = 2.0;

        /// <summary>The shape / spread samples need this many men looked at in the period.</summary>
        public const int ShapeMinMen = 6;

        /// <summary>The discipline bands of the summary: below 0.25, below 0.5, the rest.</summary>
        public const int DisciplineBands = 3;

        // ---- the fear

        /// <summary>The morale share of fear: 0 at or above the calm line, 1 at or below the broken line, a line between; no morale (−1) = 0.</summary>
        public static double MoraleShare(in FearRules r, double morale)
        {
            if (double.IsNaN(morale) || morale < 0) return 0;
            if (r.MoraleCalm <= r.MoraleBroken) return morale < r.MoraleCalm ? 1 : 0;
            return Clamp01((r.MoraleCalm - morale) / (r.MoraleCalm - r.MoraleBroken));
        }

        /// <summary>The tiredness share: 0 at or above the fresh line, 1 at an empty bar, a line between; Athletics off = 0.</summary>
        public static double TiredShare(in FearRules r, double fraction)
        {
            if (!r.AthleticsEnabled || double.IsNaN(fraction)) return 0;
            double fresh = r.FreshPercent / 100.0;
            return Clamp01((fresh - fraction) / fresh);
        }

        /// <summary>The raw fear: 1 − (1 − a·m)(1 − b·t) - two dangers, either frightens, both more.</summary>
        public static double Raw(in FearRules r, double morale, double fraction)
        {
            double m = r.MoraleWeightPercent / 100.0 * MoraleShare(in r, morale);
            double t = r.TiredWeightPercent / 100.0 * TiredShare(in r, fraction);
            return Clamp01(1 - (1 - m) * (1 - t));
        }

        /// <summary>DISCIPLINE by troop level: 0 at or below the low level, the max at or above the high one, a line between; a hero the max.</summary>
        public static double Discipline(in FearRules r, int level, bool hero)
        {
            double max = r.DisciplineMaxPercent / 100.0;
            if (hero) return max;
            if (r.DisciplineLevelHigh <= r.DisciplineLevelLow) return level > r.DisciplineLevelLow ? max : 0;
            return max * Clamp01((level - r.DisciplineLevelLow) / (double)(r.DisciplineLevelHigh - r.DisciplineLevelLow));
        }

        /// <summary>The thin line: + ThinLinePercent × (1 − d) while an enemy is near and nobody stands behind him.</summary>
        public static double ThinLine(in FearRules r, double discipline, bool inContact, bool somebodyBehind) =>
            inContact && !somebodyBehind ? r.ThinLinePercent / 100.0 * (1 - Clamp01(discipline)) : 0;

        /// <summary>HIS OWN fear: the raw fear × (1 − d) + the thin line, 0..1.</summary>
        public static double Own(in FearRules r, double morale, double fraction, double discipline, bool inContact = false, bool somebodyBehind = true) =>
            Clamp01(Raw(in r, morale, fraction) * (1 - Clamp01(discipline)) + ThinLine(in r, discipline, inContact, somebodyBehind));

        /// <summary>His fear mixed with his unit's mean (NaN = his own only).</summary>
        public static double Mixed(in FearRules r, double own, double unitMean)
        {
            if (double.IsNaN(unitMean)) return own;
            double u = r.UnitSharePercent / 100.0;
            return Clamp01((1 - u) * own + u * unitMean);
        }

        /// <summary>THE PULL: 0 below the start, 1 at the full line, a line between.</summary>
        public static double Pull(in FearRules r, double fear)
        {
            double start = r.StartPercent / 100.0, full = r.FullPercent / 100.0;
            if (full <= start) return fear >= start ? 1 : 0;
            return Clamp01((fear - start) / (full - start));
        }

        /// <summary>He starts flocking above the start; a flocking man calms only below the start − <see cref="CalmHysteresis"/>.</summary>
        public static bool Afraid(in FearRules r, double fear, bool flocking) =>
            flocking ? fear > r.StartPercent / 100.0 - CalmHysteresis : fear > r.StartPercent / 100.0;

        // ---- step 69: winning calms, no pull without a threat, the charge's mop-up

        /// <summary>The win from the odds alone: 0 at or below the even line, 1 at or above the clear line, a line between (∞ = 1).</summary>
        public static double OddsWin(in FearRules r, double odds)
        {
            if (double.IsNaN(odds)) return 0;
            double even = r.WinEvenOddsPercent / 100.0, clear = r.WinClearOddsPercent / 100.0;
            if (double.IsPositiveInfinity(odds)) return 1;
            if (clear <= even) return odds >= clear ? 1 : 0;
            return Clamp01((odds - even) / (clear - even));
        }

        /// <summary>
        /// HIS WIN (0..1): his side's local odds (courage's toughness-weighted sums in the same look) at or above the even line - the odds' win,
        /// and the enemy near him SHAKEN (the toughness-weighted mean of their morale shares on the fear's own lines: a routing man's morale is
        /// spent) × WinShakenPercent on top: 1 − (1 − oddsWin)(1 − s × shaken). Below the even line (losing) 0 - step 67 exactly. NaN odds (no
        /// enemy in courage's radius) = NaN (he has no picture of his own: the unit's is used).
        /// </summary>
        public static double Win(in FearRules r, double odds, double enemyShaken)
        {
            if (!r.WinCalms) return 0;
            if (double.IsNaN(odds)) return double.NaN;
            if (odds < r.WinEvenOddsPercent / 100.0) return 0;
            double w = OddsWin(in r, odds);
            double s = r.WinShakenPercent / 100.0 * Clamp01(enemyShaken);
            return Clamp01(1 - (1 - w) * (1 - s));
        }

        /// <summary>The win that calms him: his own, or - when the unit's picture is better - his own mixed with the unit's mean by
        /// FearUnitSharePercent (a winning unit's mass relaxes together; a losing unit gives nothing). No own picture = the unit's; none = 0.</summary>
        public static double WinMixed(in FearRules r, double own, double unitWin)
        {
            if (!r.WinCalms) return 0;
            bool hasOwn = !double.IsNaN(own), hasUnit = !double.IsNaN(unitWin);
            if (!hasOwn) return hasUnit ? Clamp01(unitWin) : 0;
            if (!hasUnit) return Clamp01(own);
            double u = r.UnitSharePercent / 100.0;
            return Clamp01(Math.Max(own, (1 - u) * own + u * unitWin));
        }

        /// <summary>The fear after the win: fear × (1 − win).</summary>
        public static double Calmed(double fear, double win) => Clamp01(Clamp01(fear) * (1 - Clamp01(win)));

        /// <summary>A threat near him: an enemy within ThreatMetres (NaN = none known); ThreatMetres 0 = always (step 67).</summary>
        public static bool Threatened(in FearRules r, double nearestEnemy) =>
            !(r.ThreatMetres > 0) || (!double.IsNaN(nearestEnemy) && nearestEnemy <= r.ThreatMetres);

        /// <summary>A charging unit in its MOP-UP: its men's mean win known and at least MopUpWinPercent (0 = never; winning calms off = never).</summary>
        public static bool MopUp(in FearRules r, double unitWin) =>
            r.WinCalms && r.MopUpWinPercent > 0 && !double.IsNaN(unitWin) && unitWin >= r.MopUpWinPercent / 100.0;

        /// <summary>The fear of an enemy near him as the win reads it: his morale share on the fear's lines (−1 / NaN = 0).</summary>
        public static double ShakenShare(in FearRules r, double enemyMorale) => MoraleShare(in r, enemyMorale);

        public static int DisciplineBand(double d) => d < 0.25 ? 0 : d < 0.5 ? 1 : 2;

        public static string DisciplineBandName(int band) => band == 0 ? "green (discipline < 0.25)" : band == 1 ? "trained (0.25-0.5)" : "veteran (0.5+)";

        // ---- the order

        /// <summary>The order as the flock reads it: a wall arrangement holds unless charging / retreating.</summary>
        public static FearOrder OrderOf(BraceOrderKind order, bool defensiveArrangement)
        {
            switch (order)
            {
                case BraceOrderKind.Charge:
                case BraceOrderKind.ChargeToTarget:
                case BraceOrderKind.AttackEntity:
                case BraceOrderKind.AiCharge:
                    return FearOrder.Charge;
                case BraceOrderKind.Retreat:
                    return FearOrder.Retreat;
                case BraceOrderKind.Advance:
                case BraceOrderKind.AiAdvance:
                    return defensiveArrangement ? FearOrder.Wall : FearOrder.Advance;
                default:
                    return defensiveArrangement ? FearOrder.Wall : FearOrder.Hold;
            }
        }

        public static string OrderName(FearOrder o) => o switch
        {
            FearOrder.Hold => "hold / move",
            FearOrder.Wall => "shield wall",
            FearOrder.Advance => "advance",
            FearOrder.Charge => "charge",
            FearOrder.Retreat => "retreat",
            _ => o.ToString(),
        };

        /// <summary>The summary's drift groups: 0 hold / wall, 1 advance, 2 charge (−1 none).</summary>
        public static int DriftGroup(FearOrder o) => o == FearOrder.Hold || o == FearOrder.Wall ? 0 : o == FearOrder.Advance ? 1 : o == FearOrder.Charge ? 2 : -1;

        public const int DriftGroups = 3;

        public static string DriftGroupName(int g) => g == 0 ? "hold / wall" : g == 1 ? "advance" : "charge";

        /// <summary>The shelter's backward drift allowed in <paramref name="dt"/> seconds (m): the cap × the hold share under a hold / a wall.</summary>
        public static double DriftCap(in FearRules r, FearOrder order, double dt)
        {
            if (!(dt > 0)) return 0;
            double cap = r.DriftMetresPerMinute / 60.0 * dt;
            return order == FearOrder.Hold || order == FearOrder.Wall ? cap * r.DriftHoldPercent / 100.0 : cap;
        }

        // ---- the shelter and his place

        /// <summary>Where the shelter WANTS to be: the centre + away (a unit vector; 0 = no enemy known) × back × the unit's mean pull.</summary>
        public static void ShelterWanted(in FearRules r, double cx, double cy, double awayX, double awayY, double unitPull, out double sx, out double sy)
        {
            double back = r.ShelterBackMetres * Clamp01(unitPull);
            sx = cx + awayX * back;
            sy = cy + awayY * back;
        }

        /// <summary>The shelter's step from <paramref name="px"/>, <paramref name="py"/> toward the wanted point: free forward and sideways, at
        /// most <paramref name="maxBack"/> metres further along "away" (the drift cap). Returns the metres it moved back (≥ 0).</summary>
        public static double DriftStep(double px, double py, double wx, double wy, double awayX, double awayY, double maxBack, out double nx, out double ny)
        {
            double dx = wx - px, dy = wy - py;
            double along = dx * awayX + dy * awayY;
            if (along > maxBack)
            {
                double cut = along - (maxBack > 0 ? maxBack : 0);
                dx -= cut * awayX;
                dy -= cut * awayY;
                along = maxBack > 0 ? maxBack : 0;
            }
            nx = px + dx;
            ny = py + dy;
            return along > 0 ? along : 0;
        }

        /// <summary>HIS PLACE: his slot + pull × (the shelter − his slot), never farther than ShelterMaxMetres from the slot.</summary>
        public static void Place(in FearRules r, double slotX, double slotY, double shelterX, double shelterY, double pull, out double tx, out double ty)
        {
            double dx = (shelterX - slotX) * Clamp01(pull), dy = (shelterY - slotY) * Clamp01(pull);
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len > r.ShelterMaxMetres && len > 1e-9)
            {
                dx *= r.ShelterMaxMetres / len;
                dy *= r.ShelterMaxMetres / len;
            }
            tx = slotX + dx;
            ty = slotY + dy;
        }

        /// <summary>A neighbour against the line from him (<paramref name="px"/>, <paramref name="py"/>) to the nearest enemy (unit
        /// <paramref name="ux"/>, <paramref name="uy"/>, <paramref name="enemyDist"/> away): how far along it, how far aside.</summary>
        public static void AlongAcross(double px, double py, double ux, double uy, double ox, double oy, out double along, out double across)
        {
            double dx = ox - px, dy = oy - py;
            along = dx * ux + dy * uy;
            across = Math.Abs(dx * uy - dy * ux);
        }

        /// <summary>A comrade COVERS him: on the line to the enemy, past <see cref="CoverMinAlongMetres"/>, short of the enemy, within the width.</summary>
        public static bool Covers(in FearRules r, double along, double across, double enemyDist) =>
            along > CoverMinAlongMetres && along < enemyDist - CoverMinAlongMetres && across <= r.CoverWidthMetres;

        /// <summary>A comrade stands BEHIND him (the thin line's test).</summary>
        public static bool Behind(double along, double across) =>
            along < -CoverMinAlongMetres && along > -BehindDepthMetres && across <= BehindWidthMetres;

        /// <summary>The spot behind a comrade at (<paramref name="ox"/>, <paramref name="oy"/>): <see cref="BehindSpotMetres"/> further from the enemy.</summary>
        public static void BehindSpot(double ox, double oy, double ux, double uy, out double sx, out double sy)
        {
            sx = ox - ux * BehindSpotMetres;
            sy = oy - uy * BehindSpotMetres;
        }

        /// <summary>A FRESH comrade anchors him (his bar at or above the line; Athletics off = always fresh), for AnchorSeconds from the start.</summary>
        public static bool Anchors(in FearRules r, double comradeFraction) =>
            r.AnchorSeconds > 0 && (!r.AthleticsEnabled || comradeFraction >= r.AnchorFreshPercent / 100.0);

        public static bool AnchorRunning(in FearRules r, double since, double now) => !double.IsNaN(since) && now - since < r.AnchorSeconds;

        /// <summary>Under a charge: he roams free within (1 − pull) × ChargeLooseMetres of the shelter (never below <see cref="MinLooseMetres"/>).</summary>
        public static double LooseRadius(in FearRules r, double pull) => Math.Max(MinLooseMetres, (1 - Clamp01(pull)) * r.ChargeLooseMetres);

        /// <summary>The sprint back: far from his place, or alone with an enemy near (and not already there).</summary>
        public static bool SprintWanted(in FearRules r, double distance, bool alone) =>
            distance > MoveAgainMetres && ((r.SprintMetres > 0 && distance > r.SprintMetres) || alone);

        /// <summary>The stick: full beyond <see cref="SlowMetres"/>, a line down to <see cref="MinStick"/> at his place.</summary>
        public static double Stick(double distance) => distance >= SlowMetres ? 1 : Math.Max(MinStick, distance / SlowMetres);

        /// <summary>The sprint amount: what he holds above FearSprintKeepPercent of his full store.</summary>
        public static double SprintAmount(in FearRules r, double stored, double capacity) => Math.Max(0, stored - r.SprintKeepPercent / 100.0 * capacity);

        /// <summary>A wall unit breaks (its held men let go) when its mean morale is known and below the line.</summary>
        public static bool WallBreaks(in FearRules r, double unitMorale) => r.Enabled && r.WallBreakMorale > 0 && !double.IsNaN(unitMorale) && unitMorale < r.WallBreakMorale;

        public static string EndName(FearEnd e) => e switch
        {
            FearEnd.Calm => "calm again",
            FearEnd.Excluded => "excluded",
            FearEnd.FeverPress => "fever presses",
            FearEnd.Surge => "surge / punish",
            FearEnd.WallHolds => "the wall holds",
            FearEnd.Retreat => "retreat order",
            FearEnd.SwitchedOff => "switched off",
            FearEnd.LeftField => "left the field",
            FearEnd.MissionEnd => "mission end",
            FearEnd.Error => "error",
            FearEnd.Winning => "winning (step 69)",
            FearEnd.NoThreat => "no enemy near (step 69)",
            _ => e.ToString(),
        };

        public static int UnitKey(int team, int formation) =>
            team < 0 || team >= Teams || formation < 0 || formation >= Formations ? -1 : team * Formations + formation;

        public static double Clamp01(double v) => double.IsNaN(v) || v < 0 ? 0 : v > 1 ? 1 : v;
    }

    /// <summary>Step 67: one AI man's fear-flock state (on his record; the logic owns every path).</summary>
    public sealed class FearMind
    {
        public FearPhase Phase;

        public bool Flocking => Phase != FearPhase.Calm;

        /// <summary>The last look's numbers.</summary>
        public double Own, Fear, Pull;

        /// <summary>Step 69: the last look's fear BEFORE the win (step 67's), the win that calmed it (0..1), a threat near him, the charge's mop-up.</summary>
        public double BaseFear, Win;
        public bool Threat = true;
        public bool MopUp;

        /// <summary>His discipline (NaN until his kit is read).</summary>
        public double Discipline = double.NaN;

        /// <summary>His unit's key in the table (−1 none).</summary>
        public int Key = -1;

        /// <summary>When the running spell began; the anchor's start (NaN = none) and whether this cover was used up.</summary>
        public double Since;
        public double AnchorSince = double.NaN;
        public bool AnchorSpent;

        /// <summary>His place (the steering's target) and whether the steering is on (our input owns his feet) / walking (else standing).</summary>
        public double TargetX, TargetY;
        public bool HasTarget;
        public bool Wish;
        public bool Moving;

        /// <summary>Under a charge: inside the loose radius, left to his AI (the wish off).</summary>
        public bool Loose;

        public double NextSteer;
        public double NextEdge;
        public double LastSprint = double.NaN;
        public double LastLook = double.NaN;

        /// <summary>The running regroup sprint is the flock's (it ends with his spell or on arrival).</summary>
        public bool SprintOurs;

        /// <summary>Looked at once / flocked once this battle (the summary's men).</summary>
        public bool Seen;
        public bool EverFlocked;

        public FearEnd LastEnd = FearEnd.Count;

        /// <summary>Step 70: he slid out of a full file toward the band's ends - his place's lateral stays there for the spell (a world point on
        /// that file's line; the unit it was decided in); the last look's band verdict; let go to strike now.</summary>
        public bool HasSlide;
        public double SlideX, SlideY;
        public int SlideKey = -1;
        public FearBandPlaceKind BandKind = FearBandPlaceKind.NoBand;
        public bool Striking;

        /// <summary>The last look's facts (the log).</summary>
        public bool Covered, SomebodyBehind, InContact;

        public void Start(double now, FearPhase phase)
        {
            Phase = phase;
            Since = now;
        }

        public void End(FearEnd why)
        {
            Phase = FearPhase.Calm;
            LastEnd = why;
            HasTarget = Wish = Moving = Loose = SprintOurs = false;
            AnchorSince = double.NaN;
            AnchorSpent = false;
            HasSlide = Striking = false;
            SlideKey = -1;
            BandKind = FearBandPlaceKind.NoBand;
        }
    }

    /// <summary>
    /// Step 67: THE UNIT TABLE (8 teams × 16 formations, fixed arrays - no allocation): every look adds a man's fear, pull, morale, freshness,
    /// the enemy he sees and where he stands; every <see cref="FearMath.PublishSeconds"/> the means are published, the SHELTER moved (its
    /// backward drift capped by order) and the unit's shape / spread / drift sampled into the stats.
    /// </summary>
    public sealed partial class FearUnits
    {
        private const int N = FearMath.UnitCount;

        // this period
        private readonly int[] _n = new int[N];
        private readonly double[] _fear = new double[N], _pull = new double[N], _morale = new double[N], _fresh = new double[N];
        private readonly int[] _moraleN = new int[N];
        private readonly double[] _ex = new double[N], _ey = new double[N];
        private readonly int[] _en = new int[N];
        private readonly double[] _along2 = new double[N], _across2 = new double[N], _dist = new double[N];
        private readonly double[] _cx = new double[N], _cy = new double[N];
        private readonly FearOrder[] _order = new FearOrder[N];
        private readonly double[] _win = new double[N]; // step 69: the men with a win of their own (an enemy in courage's radius)
        private readonly int[] _winN = new int[N];

        // published
        public readonly int[] Men = new int[N];
        public readonly double[] Fear = new double[N], Pull = new double[N], Morale = new double[N], Fresh = new double[N];
        public readonly bool[] HasEnemy = new bool[N];
        public readonly double[] AwayX = new double[N], AwayY = new double[N];
        public readonly bool[] HasShelter = new bool[N];
        public readonly double[] ShelterX = new double[N], ShelterY = new double[N];
        public readonly double[] CentreX = new double[N], CentreY = new double[N];
        public readonly FearOrder[] Order = new FearOrder[N];
        public readonly double[] PublishedAt = new double[N];

        /// <summary>Step 69: the unit's mean WIN over its men in contact (NaN = none of them had an enemy in courage's radius).</summary>
        public readonly double[] Win = new double[N];

        private double _nextPublish = double.NaN;
        private double _lastPublish = double.NaN;

        public FearUnits()
        {
            Reset();
        }

        public void Reset()
        {
            for (int k = 0; k < N; k++)
            {
                ClearPeriod(k);
                Men[k] = 0;
                Fear[k] = Pull[k] = Morale[k] = Fresh[k] = Win[k] = double.NaN;
                HasEnemy[k] = HasShelter[k] = false;
                PublishedAt[k] = double.NaN;
            }
            _nextPublish = _lastPublish = double.NaN;
            ResetBand(); // step 70
        }

        private void ClearPeriod(int k)
        {
            _n[k] = _moraleN[k] = _en[k] = _winN[k] = 0;
            _fear[k] = _pull[k] = _morale[k] = _fresh[k] = _ex[k] = _ey[k] = _along2[k] = _across2[k] = _dist[k] = _cx[k] = _cy[k] = _win[k] = 0;
            ClearBandPeriod(k); // step 70
        }

        /// <summary>Step 69: the unit's mean win (NaN = not known / none in contact).</summary>
        public double WinOf(int key, double now) => Live(key, now) ? Win[key] : double.NaN;

        /// <summary>The unit is known now (published within <see cref="FearMath.StaleSeconds"/>).</summary>
        public bool Live(int key, double now) => key >= 0 && key < N && Men[key] > 0 && !double.IsNaN(PublishedAt[key]) && now - PublishedAt[key] <= FearMath.StaleSeconds;

        /// <summary>The unit's mean fear (NaN = not known).</summary>
        public double FearOf(int key, double now) => Live(key, now) ? Fear[key] : double.NaN;

        public double MoraleOf(int key, double now) => Live(key, now) ? Morale[key] : double.NaN;

        /// <summary>One look at one man of unit <paramref name="key"/>: his numbers, the enemy centroid he sees (when <paramref name="enemy"/>),
        /// where he stands against the unit's centre and facing (a unit vector or 0); step 69: his own win (NaN = no enemy in courage's radius -
        /// not counted in the unit's mean). <paramref name="fear"/> is his fear BEFORE the win (the unit's mean feeds the next looks' mix).</summary>
        public void Note(int key, double fear, double pull, double morale, double fresh, bool enemy, double ex, double ey, double cx, double cy, double fx, double fy,
            double px, double py, FearOrder order, double win = double.NaN, int man = -1, double binX = double.NaN, double binY = double.NaN)
        {
            if (key < 0 || key >= N) return;
            _n[key]++;
            if (!double.IsNaN(win))
            {
                _win[key] += win;
                _winN[key]++;
            }
            _fear[key] += fear;
            _pull[key] += pull;
            if (!double.IsNaN(morale) && morale >= 0)
            {
                _morale[key] += morale;
                _moraleN[key]++;
            }
            _fresh[key] += double.IsNaN(fresh) ? 1 : fresh;
            if (enemy && !double.IsNaN(ex))
            {
                _ex[key] += ex;
                _ey[key] += ey;
                _en[key]++;
            }
            double dx = px - cx, dy = py - cy;
            if (!double.IsNaN(dx) && !double.IsNaN(dy))
            {
                double along = dx * fx + dy * fy, across = dx * fy - dy * fx;
                bool facing = fx * fx + fy * fy > 1e-6;
                _along2[key] += facing ? along * along : 0;
                _across2[key] += facing ? across * across : 0;
                _dist[key] += Math.Sqrt(dx * dx + dy * dy);
                _cx[key] += cx;
                _cy[key] += cy;
            }
            _order[key] = order;
            NoteBand(key, px, py, fx, fy, man, binX, binY); // step 70: his position (and who he is, where he is bound) for the band's moments, files and ranks
        }

        /// <summary>Time to publish (the first call arms the clock).</summary>
        public bool Due(double now)
        {
            if (double.IsNaN(_nextPublish))
            {
                _nextPublish = now + FearMath.PublishSeconds;
                return false;
            }
            return now >= _nextPublish;
        }

        /// <summary>
        /// Publishes every unit looked at this period: the means, the enemy's direction ("away" = from the enemy centroid to the centre), the
        /// SHELTER (wanted = centre + away × back × mean pull, its step back capped by order), and the stats' samples (spread, depth ÷ width
        /// while in contact, the centre's drift back by order - ON / OFF by <paramref name="on"/>).
        /// </summary>
        public void Publish(double now, in FearRules r, FearStats? stats, bool on)
        {
            double dt = double.IsNaN(_lastPublish) || now <= _lastPublish ? FearMath.PublishSeconds : now - _lastPublish;
            _lastPublish = now;
            _nextPublish = now + FearMath.PublishSeconds;
            for (int k = 0; k < N; k++)
            {
                int n = _n[k];
                if (n <= 0)
                {
                    continue; // stays as it was (stale after StaleSeconds)
                }
                double cx = _cx[k] / n, cy = _cy[k] / n;
                bool had = Live(k, now) && !double.IsNaN(CentreX[k]);
                double oldCx = CentreX[k], oldCy = CentreY[k];
                Men[k] = n;
                Fear[k] = _fear[k] / n;
                Pull[k] = _pull[k] / n;
                Morale[k] = _moraleN[k] > 0 ? _morale[k] / _moraleN[k] : double.NaN;
                Fresh[k] = _fresh[k] / n;
                Win[k] = _winN[k] > 0 ? _win[k] / _winN[k] : double.NaN;
                CentreX[k] = cx;
                CentreY[k] = cy;
                Order[k] = _order[k];
                bool enemy = _en[k] > 0;
                double ax = 0, ay = 0;
                if (enemy)
                {
                    ax = cx - _ex[k] / _en[k];
                    ay = cy - _ey[k] / _en[k];
                    double len = Math.Sqrt(ax * ax + ay * ay);
                    if (len > 1e-6)
                    {
                        ax /= len;
                        ay /= len;
                    }
                    else
                    {
                        ax = ay = 0;
                        enemy = false;
                    }
                }
                HasEnemy[k] = enemy;
                AwayX[k] = ax;
                AwayY[k] = ay;
                FearMath.ShelterWanted(in r, cx, cy, ax, ay, Pull[k], out double wx, out double wy);
                if (!HasShelter[k] || !had)
                {
                    // a unit first seen (or seen again after a gap): the shelter starts at its centre and drifts back from there - never a jump
                    ShelterX[k] = cx;
                    ShelterY[k] = cy;
                    HasShelter[k] = true;
                }
                else
                {
                    double back = FearMath.DriftStep(ShelterX[k], ShelterY[k], wx, wy, ax, ay, FearMath.DriftCap(in r, _order[k], dt), out double nx, out double ny);
                    ShelterX[k] = nx;
                    ShelterY[k] = ny;
                    stats?.NoteShelterBack(back);
                }
                if (stats != null)
                {
                    if (n >= FearMath.ShapeMinMen)
                    {
                        double depth = Math.Sqrt(_along2[k] / n), width = Math.Sqrt(_across2[k] / n);
                        stats.AddShape(on, enemy, _dist[k] / n, depth, width);
                    }
                    if (had && enemy && dt <= FearMath.StaleSeconds)
                    {
                        double moved = (cx - oldCx) * ax + (cy - oldCy) * ay;
                        stats.AddDrift(on, FearMath.DriftGroup(_order[k]), moved, dt);
                    }
                }
                PublishBand(k, now, dt, in r, stats, on, enemy, ax, ay); // step 70: the band, its files, the shape in ranks
                PublishedAt[k] = now;
                ClearPeriod(k);
            }
        }
    }
}

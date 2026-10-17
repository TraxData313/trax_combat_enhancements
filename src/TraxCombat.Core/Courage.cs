using System;
using System.Globalization;

namespace TraxCombat.Core
{
    /// <summary>Step 49: where an AI man's courage is.</summary>
    public enum CouragePhase
    {
        /// <summary>Brave - the game's own AI drives him (the local odds are good enough, or he is not looked at).</summary>
        Brave,

        /// <summary>Timid and far from his mates: he walks back to his formation's centre of mass, facing whatever his AI faces (a
        /// backpedal through his own input, step 16's way), sprinting when it is far.</summary>
        Moving,

        /// <summary>Timid but back among his mates (or at the centre): no walk, only the tightened behaviour weights - he strikes only
        /// what comes within reach and does not hunt.</summary>
        Holding,
    }

    /// <summary>Step 49: why a man is not looked at for courage (in the order asked).</summary>
    public enum CourageSkip
    {
        /// <summary>The player, or a soldier the player took over.</summary>
        NotAi,

        /// <summary>A rider - cavalry roams by design.</summary>
        Mounted,

        /// <summary>No formation (tournaments, arenas, duels): no group to return to.</summary>
        NoFormation,

        /// <summary>Busy with a game job - detached (siege engines, ladders), using an object, in a ladder queue, routing.</summary>
        Busy,

        /// <summary>A siege's attacker (a lone man on the wall top would walk back down the ladder).</summary>
        SiegeAttacker,

        /// <summary>Held in a shield wall / square / circle by step 41 - the wall decides for him.</summary>
        WallHeld,

        Count,
    }

    /// <summary>Step 49: why a timid spell ended.</summary>
    public enum CourageEnd
    {
        /// <summary>The odds came back (above the required odds × the hysteresis) after the minimum hold.</summary>
        Brave,

        /// <summary>The master switch or CourageEnabled off.</summary>
        SwitchedOff,

        /// <summary>The player took him, he mounted, joined a wall, became busy … (a <see cref="CourageSkip"/>).</summary>
        Excluded,

        /// <summary>He fell, fled or left the field.</summary>
        LeftField,

        /// <summary>The battle ended with him timid.</summary>
        MissionEnd,

        /// <summary>An exception on his look (logged) - ended to stay safe.</summary>
        Error,

        /// <summary>Step 49b: his fever (armed or burning) and no shock - the charge goes in.</summary>
        FeverPress,

        /// <summary>Step 49b: a SURGE - a timid man among his mates steps in for CourageSurgeSeconds.</summary>
        Surge,

        Count,
    }

    /// <summary>Step 49: why a walk back (<see cref="CouragePhase.Moving"/>) ended.</summary>
    public enum RegroupEnd
    {
        /// <summary>Within CourageRegroupRadiusMetres of his formation's centre.</summary>
        Arrived,

        /// <summary>CourageRegroupMates mates within the courage radius - he is with his group again.</summary>
        AmongMates,

        /// <summary>The ground ahead on his way stopped being walkable (a wall edge, a ditch) - he holds where he is.</summary>
        EdgeAhead,

        /// <summary>Walked <see cref="CourageMath.MaxMovingSeconds"/> without getting there - he holds where he is.</summary>
        TookTooLong,

        /// <summary>Reserved (a walk now always ends before courage comes back - counted 0).</summary>
        BecameBrave,

        /// <summary>His timid spell ended otherwise (switched off, excluded, left the field, the battle's end).</summary>
        Ended,

        Count,
    }

    /// <summary>Step 49: what one look did to a man's courage.</summary>
    public enum CourageStep
    {
        None,

        /// <summary>Became timid, far from his mates - the walk back starts.</summary>
        TimidMoving,

        /// <summary>Became timid among his mates - he holds (the weights only).</summary>
        TimidHolding,

        /// <summary>Timid and holding, then left alone again (his mates gone, far from the centre) - the walk back starts.</summary>
        StartMoving,

        /// <summary>His walk back ended (<see cref="CourageMind.LastRegroupEnd"/>) - he holds.</summary>
        StopMoving,

        /// <summary>Brave again.</summary>
        Brave,
    }

    /// <summary>Step 49: why a regroup sprint was not started.</summary>
    public enum CourageSprintRefusal
    {
        /// <summary>CourageSprintBack, SprintEnabled or SprintAiEnabled off (or Athletics off - the sprint is his Athletics).</summary>
        Off,

        /// <summary>Already sprinting.</summary>
        Sprinting,

        /// <summary>No store at all.</summary>
        NoStore,

        /// <summary>His store is at or below CourageSprintKeepPercent of his full store.</summary>
        ReserveKept,

        Count,
    }

    /// <summary>Step 49: a man's last landed hits (mission times, the newest four) - his SHOCK window reads them. A struct mutated in place.</summary>
    public struct ShockTimes
    {
        private double _h0, _h1, _h2, _h3;
        private int _next, _count;

        public void Note(double now)
        {
            switch (_next)
            {
                case 0: _h0 = now; break;
                case 1: _h1 = now; break;
                case 2: _h2 = now; break;
                default: _h3 = now; break;
            }
            _next = (_next + 1) % 4;
            if (_count < 4) _count++;
        }

        /// <summary>Hits within the last <paramref name="window"/> seconds.</summary>
        public int Recent(double now, double window)
        {
            if (!(window > 0)) return 0;
            int n = 0;
            for (int i = 0; i < _count; i++)
            {
                double t = i == 0 ? _h0 : i == 1 ? _h1 : i == 2 ? _h2 : _h3;
                if (t <= now && now - t < window) n++;
            }
            return n;
        }
    }

    /// <summary>The required odds and their parts (each a factor on the base).</summary>
    public struct CourageRequired
    {
        public double Base;
        public double Tired;
        public double Morale;
        public double Fever;
        public double Hero;
        public double Shock;
        public double Value;
    }

    /// <summary>
    /// Step 49 - COURAGE AND COHESION (Anton, 2026-10-17 night: "AI men are unrealistically brave" - a man alone circles a shield wall, stands
    /// exhausted among four enemies; "real men stick with their group, not out of professionalism, out of instinct"; AI_NOTES "Step 49",
    /// DESIGN §2 "Courage and cohesion"). The settings for one look, read live from <see cref="TraxSettings"/>: the master switch first, then
    /// CourageEnabled. Courage works WITHOUT the Athletics system (toughness, morale, fever and heroes); tiredness counts only while
    /// AthleticsEnabled is on (the bar is the Athletics bar).
    /// </summary>
    public readonly struct CourageRules
    {
        public CourageRules(bool modEnabled, bool courageEnabled, bool athleticsEnabled, double radiusMetres, int baseOddsPercent, int armourWeightPercent,
            int levelWeightPercent, int healthWeightPercent, int tiredWeightPercent, int moraleWeightPercent, int feverPercent, int heroPercent,
            int hysteresisPercent, double minTimidSeconds, double regroupRadiusMetres, int regroupMates, bool sprintBack, double sprintMetres,
            int sprintEnemies, int sprintKeepPercent, bool tightenWeights, double timidMeleeMetres, double shockSeconds = 6, int shockWeightPercent = 100,
            int heavyWeaponPercent = 150, double flockRadiusMetres = 20, int flockMinMen = 6, int panicOddsPercent = 50, int panicSprintKeepPercent = 0,
            int surgeChancePercent = 5, double surgeSeconds = 4)
        {
            ModEnabled = modEnabled;
            CourageEnabled = courageEnabled;
            AthleticsEnabled = athleticsEnabled;
            RadiusMetres = Clamp(radiusMetres, CourageMath.MinRadiusMetres, CourageMath.MaxRadiusMetres);
            BaseOddsPercent = ClampInt(baseOddsPercent, CourageMath.MinOddsPercent, CourageMath.MaxPercent);
            ArmourWeightPercent = ClampInt(armourWeightPercent, 0, CourageMath.MaxPercent);
            LevelWeightPercent = ClampInt(levelWeightPercent, 0, CourageMath.MaxPercent);
            HealthWeightPercent = ClampInt(healthWeightPercent, 0, 100);
            TiredWeightPercent = ClampInt(tiredWeightPercent, 0, CourageMath.MaxPercent);
            MoraleWeightPercent = ClampInt(moraleWeightPercent, 0, CourageMath.MaxPercent);
            FeverPercent = ClampInt(feverPercent, 0, 100);
            HeroPercent = ClampInt(heroPercent, CourageMath.MinOddsPercent, 200);
            HysteresisPercent = ClampInt(hysteresisPercent, 0, 200);
            MinTimidSeconds = Clamp(minTimidSeconds, 0, 60);
            RegroupRadiusMetres = Clamp(regroupRadiusMetres, CourageMath.MinRadiusMetres / 2, 100);
            RegroupMates = regroupMates < 0 ? 0 : regroupMates;
            SprintBack = sprintBack;
            SprintMetres = Clamp(sprintMetres, 0, 500);
            SprintEnemies = sprintEnemies < 1 ? 1 : sprintEnemies;
            SprintKeepPercent = ClampInt(sprintKeepPercent, 0, 100);
            TightenWeights = tightenWeights;
            TimidMeleeMetres = Clamp(timidMeleeMetres, CourageMath.MinMeleeMetres, CourageMath.MaxMeleeMetres);
            ShockSeconds = Clamp(shockSeconds, 0, 60);
            ShockWeightPercent = ClampInt(shockWeightPercent, 0, CourageMath.MaxPercent);
            HeavyWeaponPercent = ClampInt(heavyWeaponPercent, 100, CourageMath.MaxPercent);
            FlockRadiusMetres = Clamp(flockRadiusMetres, 0, 100);
            FlockMinMen = flockMinMen < 1 ? 1 : flockMinMen;
            PanicOddsPercent = ClampInt(panicOddsPercent, 0, 100);
            PanicSprintKeepPercent = ClampInt(panicSprintKeepPercent, 0, 100);
            SurgeChancePercent = ClampInt(surgeChancePercent, 0, 100);
            SurgeSeconds = Clamp(surgeSeconds, 0, 30);
        }

        private static double Clamp(double v, double min, double max) => double.IsNaN(v) || v < min ? min : v > max ? max : v;

        private static int ClampInt(int v, int min, int max) => v < min ? min : v > max ? max : v;

        public bool ModEnabled { get; }

        /// <summary>CourageEnabled (the switch itself).</summary>
        public bool CourageEnabled { get; }

        /// <summary>The Athletics system - only the tiredness term needs it.</summary>
        public bool AthleticsEnabled { get; }

        /// <summary>The master switch FIRST, then CourageEnabled.</summary>
        public bool Enabled => ModEnabled && CourageEnabled;

        /// <summary>Which switch holds it off (null = on).</summary>
        public string? OffBecause => !ModEnabled ? "ModEnabled" : !CourageEnabled ? "CourageEnabled" : null;

        /// <summary>CourageRadiusMetres - the circle a man weighs his mates and his enemies in.</summary>
        public double RadiusMetres { get; }

        /// <summary>CourageBaseOddsPercent - the odds a fresh, steady man needs: 100 = even (his side's toughness ≥ the enemy's).</summary>
        public int BaseOddsPercent { get; }

        public int ArmourWeightPercent { get; }
        public int LevelWeightPercent { get; }
        public int HealthWeightPercent { get; }
        public int TiredWeightPercent { get; }
        public int MoraleWeightPercent { get; }

        /// <summary>CourageFeverPercent - the required odds at a full fever, in percent of the calm ones (lower = braver).</summary>
        public int FeverPercent { get; }

        /// <summary>CourageHeroPercent - a hero's required odds, in percent of a soldier's.</summary>
        public int HeroPercent { get; }

        public int HysteresisPercent { get; }
        public double MinTimidSeconds { get; }
        public double RegroupRadiusMetres { get; }
        public int RegroupMates { get; }
        public bool SprintBack { get; }
        public double SprintMetres { get; }
        public int SprintEnemies { get; }
        public int SprintKeepPercent { get; }
        public bool TightenWeights { get; }
        public double TimidMeleeMetres { get; }

        /// <summary>Anton's additions (2026-10-17 night): SHOCK - the window a landed hit (or a live impact outcome) keeps a man shaken
        /// (CourageShockSeconds; 0 = no shock), the required odds at a full shock (+CourageShockWeightPercent); a two-hander / polearm man
        /// without a shield weighs CourageHeavyWeaponPercent as an ENEMY; FLOCKING - the walk goes to the allies within
        /// CourageFlockRadiusMetres when at least CourageFlockMinMen are there (else the formation's centre); PANIC - shocked AND below
        /// CouragePanicOddsPercent of the required odds = he sprints whatever the distance, keeping only CouragePanicSprintKeepPercent.</summary>
        public double ShockSeconds { get; }
        public int ShockWeightPercent { get; }
        public int HeavyWeaponPercent { get; }
        public double FlockRadiusMetres { get; }
        public int FlockMinMen { get; }
        public int PanicOddsPercent { get; }
        public int PanicSprintKeepPercent { get; }

        /// <summary>Step 49b (the manager's review of Anton's picture: "a few of them now and then go and hit the wall"): a timid man who is
        /// not shocked, among his mates, an enemy within <see cref="CourageMath.SurgeEnemyMetres"/>, rolls this chance at each look × his bar
        /// share; a hit makes him brave for <see cref="SurgeSeconds"/> - he steps in and swings. 0 = never.</summary>
        public int SurgeChancePercent { get; }
        public double SurgeSeconds { get; }

        public static CourageRules From(TraxSettings s) => new CourageRules(s.ModEnabled, s.CourageEnabled, s.AthleticsEnabled, s.CourageRadiusMetres,
            s.CourageBaseOddsPercent, s.CourageArmourWeightPercent, s.CourageLevelWeightPercent, s.CourageHealthWeightPercent, s.CourageTiredWeightPercent,
            s.CourageMoraleWeightPercent, s.CourageFeverPercent, s.CourageHeroPercent, s.CourageHysteresisPercent, s.CourageMinTimidSeconds,
            s.CourageRegroupRadiusMetres, s.CourageRegroupMates, s.CourageSprintBack, s.CourageSprintMetres, s.CourageSprintEnemies, s.CourageSprintKeepPercent,
            s.CourageTightenWeights, s.CourageTimidMeleeMetres, s.CourageShockSeconds, s.CourageShockWeightPercent, s.CourageHeavyWeaponPercent,
            s.CourageFlockRadiusMetres, s.CourageFlockMinMen, s.CouragePanicOddsPercent, s.CouragePanicSprintKeepPercent,
            s.CourageSurgeChancePercent, s.CourageSurgeSeconds);

        private static string N(double v, string f = "0.##") => v.ToString(f, CultureInfo.InvariantCulture);

        /// <summary>The settings sentence for the mission-start line, a change and the summary.</summary>
        public string Describe()
        {
            if (!Enabled)
                return "courage OFF (" + OffBecause + ") - the game's own bravery; the looks still MEASURE (isolated men, distance from the formation's centre) for the ON / OFF comparison";
            double looter = CourageMath.BaseToughness(in this, CourageMath.ExampleLooterArmour, CourageMath.ExampleLooterLevel);
            double legionary = CourageMath.BaseToughness(in this, CourageMath.ExampleLegionaryArmour, CourageMath.ExampleLegionaryLevel);
            return "courage: each AI man on foot in a formation weighs his side against the enemy within " + N(RadiusMetres, "0.0") + " m (CourageRadiusMetres; full weight to half of it, "
                   + "fading to 0 at its edge), each man by his TOUGHNESS = (1 + " + ArmourWeightPercent + "% x armour / 100) x (1 + " + LevelWeightPercent + "% x level / 30) x (1 - "
                   + HealthWeightPercent + "% x the health he lost) - a looter (armour " + N(CourageMath.ExampleLooterArmour, "0") + ", level " + CourageMath.ExampleLooterLevel + ") "
                   + N(looter, "0.00") + ", an imperial legionary (armour " + N(CourageMath.ExampleLegionaryArmour, "0") + ", level " + CourageMath.ExampleLegionaryLevel + ") "
                   + N(legionary, "0.00") + " = " + N(legionary / looter, "0.0") + " looters; he needs odds of " + BaseOddsPercent + "% (CourageBaseOddsPercent) x (1 + "
                   + TiredWeightPercent + "% x the bar he spent" + (AthleticsEnabled ? "" : " - 0 while Athletics is off") + ") x (1 + " + MoraleWeightPercent
                   + "% x his morale below 50, / 50) x (1 + " + ShockWeightPercent + "% x his SHOCK - landed hits in the last " + N(ShockSeconds, "0.#")
                   + " s, " + CourageMath.ShockCap + " = full) x (" + FeverPercent + "% at a full fever, fading with it) x (" + HeroPercent + "% for a hero); below it he is TIMID: "
                   + "an enemy with a two-hander or polearm and no shield weighs " + HeavyWeaponPercent + "%; "
                   + "far from his mates (fewer than " + RegroupMates + " within the radius - twice that when shocked -, more than " + N(RegroupRadiusMetres, "0.0")
                   + " m from his formation's centre) he walks back to the nearest bigger group of his side (" + FlockMinMen + "+ allies within " + N(FlockRadiusMetres, "0")
                   + " m, else his formation's centre of mass) facing the enemy" + (SprintBack ? ", sprinting what he holds above " + SprintKeepPercent + "% of his store when the centre is more than "
                   + N(SprintMetres, "0") + " m away or " + SprintEnemies + "+ enemies are near; in PANIC (shocked, odds below " + PanicOddsPercent
                   + "% of the required) he sprints at once keeping only " + PanicSprintKeepPercent + "%" : "") + "; among them he holds"
                   + (TightenWeights ? " and strikes only within " + N(TimidMeleeMetres, "0.0") + " m (his behaviour weights)" : " (behaviour weights untouched)")
                   + "; brave again at the required odds + " + HysteresisPercent + "% after " + N(MinTimidSeconds, "0.0") + " s timid"
                   + "; a man in his FEVER presses whatever the odds unless SHOCKED; a timid, unshocked man among his mates with an enemy within "
                   + N(CourageMath.SurgeEnemyMetres, "0") + " m SURGES in for " + N(SurgeSeconds, "0.#") + " s at " + SurgeChancePercent + "% a look x his bar";
        }
    }

    /// <summary>Step 49 - the pure arithmetic of courage (AI_NOTES "Step 49").</summary>
    public static class CourageMath
    {
        // ---- plumbing (documented, not settings)

        /// <summary>Each AI man is looked at about this often (staggered over the ticks, the brace's pattern).</summary>
        public const double PollSeconds = 0.5;

        /// <summary>A neighbour counts in full up to this share of the radius, then fades in a straight line to 0 at its edge - a man
        /// walking across the edge does not flip anybody's courage at once.</summary>
        public const double FullWeightShare = 0.5;

        /// <summary>The toughness's units: armour per 100 points (head + body + legs + arms), level per 30 (a tier-6 troop is level 31).</summary>
        public const double ArmourUnit = 100;
        public const double LevelUnit = 30;

        /// <summary>Levels above this count as this (a level-60 lord is not twice as scary as a level-30 one).</summary>
        public const int LevelCap = 60;

        /// <summary>Morale (the game's 0-100) at and above which morale adds nothing; vanilla's men start at 35-65 (+ perks).</summary>
        public const double MoraleNeutral = 50;

        /// <summary>The log's "isolated": fewer than this many mates within the radius while an enemy is within it.</summary>
        public const int IsolatedMates = 2;

        /// <summary>The log's wobble: a flip timid ↔ brave within this many seconds of the last one.</summary>
        public const double WobbleSeconds = 2;

        /// <summary>A walk back longer than this ends where he is (he holds) - a man who cannot get there does not walk forever.</summary>
        public const double MaxMovingSeconds = 15;

        /// <summary>Step 49b: a surge needs an enemy within this (he can reach him in a few steps); the STANDOFF measure counts timid men whose
        /// nearest enemy is between <see cref="StandoffMinMetres"/> and this, nobody of the two attacking in <see cref="StandoffSeconds"/>.</summary>
        public const double SurgeEnemyMetres = 8;
        public const double StandoffMinMetres = 3;
        public const double StandoffSeconds = 10;

        /// <summary>Step 49b: his fever overrides the odds - armed or burning, NOT shocked (a hit man falls back, fever or not).</summary>
        public static bool FeverPresses(double feverShare, bool shocked) => feverShare > 0 && !shocked;

        /// <summary>Step 49b: a surge may be rolled - timid, not shocked, at least CourageRegroupMates mates, an enemy within 8 m.</summary>
        public static bool SurgeEligible(in CourageRules r, bool timid, bool shocked, int mates, double nearestEnemy) =>
            timid && !shocked && r.SurgeChancePercent > 0 && r.SurgeSeconds > 0 && mates >= r.RegroupMates && !double.IsNaN(nearestEnemy)
            && nearestEnemy <= SurgeEnemyMetres;

        /// <summary>Step 49b: the surge chance at one look = CourageSurgeChancePercent × his bar share (Athletics on; off = × 1).</summary>
        public static double SurgeChance(in CourageRules r, double bar)
        {
            double share = r.AthleticsEnabled ? (double.IsNaN(bar) ? 1 : bar < 0 ? 0 : bar > 1 ? 1 : bar) : 1;
            return r.SurgeChancePercent / 100.0 * share;
        }

        /// <summary>Step 49b: the roll (a die in [0, 1)).</summary>
        public static bool SurgeRoll(double chance, double die) => die < chance;

        /// <summary>Step 49b: a standoff look - timid, his nearest enemy 3-8 m, neither he nor that enemy attacked in the last 10 s
        /// (<paramref name="lastAttack"/> = the later of the two's last melee release; negative = never).</summary>
        public static bool Standoff(bool timid, double nearestEnemy, double lastAttack, double now) =>
            timid && !double.IsNaN(nearestEnemy) && nearestEnemy >= StandoffMinMetres && nearestEnemy <= SurgeEnemyMetres
            && (lastAttack < 0 || now - lastAttack >= StandoffSeconds);

        /// <summary>A holding man starts walking again only this much beyond the regroup radius (no wobble at its edge).</summary>
        public const double MoveAgainExtraMetres = 2;

        /// <summary>The walk's direction: the centre, plus this much of "away from the enemies near him" (both unit vectors).</summary>
        public const double AwayWeight = 0.5;

        /// <summary>The centre lies BEHIND the enemies (the way to it and "away from them" more than ~135° apart): he goes round them.</summary>
        public const double RoundCosine = -0.7;

        /// <summary>The log's "out of the formation": farther than this many regroup radii from its centre.</summary>
        public const double OutOfFormationRadii = 2;

        /// <summary>The timid man's behaviour weights (the game's <c>BehaviorValues</c> curves: y1 at 0 m, y2 at x2, y3 at x3 and beyond):
        /// Melee 10 FLAT out to his reach (CourageTimidMeleeMetres - above vanilla's 8 at 0 m: within reach he fights at full - step 49b), 0.01
        /// by reach + 1.5 m; GoToPos 5 at 0, 6 from 7 m on - so about 0.7 m beyond his reach his own position wins and he stops hunting.
        /// Vanilla's DefaultMove: Melee 8 / 7 m 5 / 20 m 0.01, GoToPos 3 / 7 m 5 / 20 m 6.</summary>
        public const float TimidMeleeY1 = 10f, TimidMeleeY2 = 10f, TimidMeleeY3 = 0.01f, TimidMeleeFade = 1.5f;
        public const float TimidGoToY1 = 5f, TimidGoToX2 = 7f, TimidGoToY2 = 6f, TimidGoToX3 = 20f, TimidGoToY3 = 6f;

        /// <summary>Shock events counted up to this many (each a third of the full shock at 3).</summary>
        public const int ShockCap = 3;

        /// <summary>A shocked man needs this many times CourageRegroupMates to stop walking, and gets this share of the regroup radius - the
        /// shaken pack in tight (Anton: "pushed like a flock of sheep").</summary>
        public const int ShockMatesFactor = 2;
        public const double ShockRadiusShare = 0.5;

        public const double MinRadiusMetres = 2, MaxRadiusMetres = 30;
        public const int MinOddsPercent = 10, MaxPercent = 500;
        public const double MinMeleeMetres = 0.5, MaxMeleeMetres = 6;

        // ---- the worked examples' troops (the settings sentence, the tests, AI_NOTES / HOW-IT-WORKS)
        public const double ExampleLooterArmour = 8;
        public const int ExampleLooterLevel = 5;
        public const double ExampleLegionaryArmour = 110;
        public const int ExampleLegionaryLevel = 26;

        // ---- toughness

        /// <summary>A man's toughness at full health: (1 + a × armour / 100) × (1 + l × min(level, 60) / 30). Armour = his equipment's head +
        /// body + legs + arms (read once - it does not change in a battle). Why both: armour is what a blow meets, the level is his skill
        /// and experience (the troop tree); a looter in rags ≈ 1.35, an armoured tier-5 man ≈ 6 at the defaults - the "4-5 looters per
        /// armoured man" Anton asked for.</summary>
        public static double BaseToughness(in CourageRules r, double armour, int level)
        {
            if (double.IsNaN(armour) || armour < 0) armour = 0;
            if (level < 0) level = 0;
            if (level > LevelCap) level = LevelCap;
            return (1 + r.ArmourWeightPercent / 100.0 * armour / ArmourUnit) * (1 + r.LevelWeightPercent / 100.0 * level / LevelUnit);
        }

        /// <summary>His toughness NOW: the base × (1 − h × the health share he lost) - a man at half health with h 50% counts 0.75.</summary>
        public static double Toughness(in CourageRules r, double baseToughness, double healthShare)
        {
            if (double.IsNaN(baseToughness) || baseToughness <= 0) return 0;
            if (double.IsNaN(healthShare)) healthShare = 1;
            if (healthShare < 0) healthShare = 0;
            if (healthShare > 1) healthShare = 1;
            return baseToughness * (1 - r.HealthWeightPercent / 100.0 * (1 - healthShare));
        }

        /// <summary>An ENEMY's toughness × CourageHeavyWeaponPercent when he carries a two-handed weapon or polearm and no shield (read once
        /// from his kit): such a man's blows break guards and his reach outranges a sword - Anton's "3-4 shock troops with giant axes".</summary>
        public static double EnemyToughness(in CourageRules r, double toughness, bool heavy) => heavy ? toughness * r.HeavyWeaponPercent / 100.0 : toughness;

        /// <summary>The shock share 0..1: min(events, <see cref="ShockCap"/>) ÷ ShockCap (0 with the window at 0).</summary>
        public static double ShockShare(in CourageRules r, int events)
        {
            if (!(r.ShockSeconds > 0) || events <= 0) return 0;
            return (events > ShockCap ? ShockCap : events) / (double)ShockCap;
        }

        /// <summary>PANIC: shocked and the odds below CouragePanicOddsPercent of the required.</summary>
        public static bool Panic(in CourageRules r, double shockShare, double odds, double required) =>
            shockShare > 0 && !double.IsNaN(odds) && odds < required * r.PanicOddsPercent / 100.0;

        /// <summary>The mates a timid man needs around him to stop walking (doubled while shocked).</summary>
        public static int MatesNeeded(in CourageRules r, bool shocked) => shocked ? r.RegroupMates * ShockMatesFactor : r.RegroupMates;

        /// <summary>The regroup radius (halved while shocked).</summary>
        public static double RegroupRadius(in CourageRules r, bool shocked) => shocked ? r.RegroupRadiusMetres * ShockRadiusShare : r.RegroupRadiusMetres;

        /// <summary>A neighbour's weight by his distance: 1 up to <see cref="FullWeightShare"/> of the radius, a straight line to 0 at it.</summary>
        public static double Weight(double distance, double radius)
        {
            if (double.IsNaN(distance) || radius <= 0) return 0;
            double full = radius * FullWeightShare;
            if (distance <= full) return 1;
            if (distance >= radius) return 0;
            return (radius - distance) / (radius - full);
        }

        /// <summary>The local odds: his side's toughness (him included) ÷ the enemy's; no enemy = +∞ (nothing to fear).</summary>
        public static double Odds(double allies, double enemies)
        {
            if (!(enemies > 1e-9)) return double.PositiveInfinity;
            if (double.IsNaN(allies) || allies < 0) allies = 0;
            return allies / enemies;
        }

        /// <summary>The fever's share for the courage term: 1 while armed (the order was just given), left ÷ full while burning, else 0.</summary>
        public static double FeverShare(FeverPhase phase, double left, double full)
        {
            if (phase == FeverPhase.Armed) return 1;
            if (phase != FeverPhase.Burning || !(full > 0)) return 0;
            double s = left / full;
            return s < 0 ? 0 : s > 1 ? 1 : s;
        }

        /// <summary>
        /// The odds he needs: base × (1 + t × (1 − bar)) × (1 + m × max(0, 50 − morale) ÷ 50) × (1 − (1 − F) × fever) × (hero ? H : 1), never below
        /// 0.01. bar = his Athletics points ÷ his FULL pool (ignored while Athletics is off); morale &lt; 0 = none (the game's -1 for no
        /// AI component) = 1.
        /// </summary>
        public static double Required(in CourageRules r, double bar, double morale, double feverShare, bool hero, out CourageRequired parts) =>
            Required(in r, bar, morale, feverShare, hero, 0, out parts);

        /// <summary>The same with his shock share (0..1): × (1 + CourageShockWeightPercent × shock).</summary>
        public static double Required(in CourageRules r, double bar, double morale, double feverShare, bool hero, double shockShare, out CourageRequired parts)
        {
            parts = default;
            parts.Base = r.BaseOddsPercent / 100.0;
            double spent = 0;
            if (r.AthleticsEnabled && !double.IsNaN(bar)) spent = 1 - (bar < 0 ? 0 : bar > 1 ? 1 : bar);
            parts.Tired = 1 + r.TiredWeightPercent / 100.0 * spent;
            double low = double.IsNaN(morale) || morale < 0 ? 0 : Math.Max(0, MoraleNeutral - Math.Min(100, morale)) / MoraleNeutral;
            parts.Morale = 1 + r.MoraleWeightPercent / 100.0 * low;
            if (double.IsNaN(feverShare) || feverShare < 0) feverShare = 0;
            if (feverShare > 1) feverShare = 1;
            parts.Fever = 1 - (1 - r.FeverPercent / 100.0) * feverShare;
            parts.Hero = hero ? r.HeroPercent / 100.0 : 1;
            if (double.IsNaN(shockShare) || shockShare < 0) shockShare = 0;
            if (shockShare > 1) shockShare = 1;
            parts.Shock = 1 + r.ShockWeightPercent / 100.0 * shockShare;
            double v = parts.Base * parts.Tired * parts.Morale * parts.Fever * parts.Hero * parts.Shock;
            parts.Value = v < 0.01 ? 0.01 : v;
            return parts.Value;
        }

        /// <summary>Timid: the odds below the required odds.</summary>
        public static bool Timid(double odds, double required) => !double.IsNaN(odds) && odds < required;

        /// <summary>Brave again: timid at least CourageMinTimidSeconds and the odds at or above required × (1 + hysteresis).</summary>
        public static bool BraveAgain(in CourageRules r, double odds, double required, double timidFor) =>
            timidFor >= r.MinTimidSeconds - 1e-9 && !double.IsNaN(odds) && odds >= required * (1 + r.HysteresisPercent / 100.0);

        /// <summary>A timid man walks back when he is far from the centre AND short of mates (CourageRegroupMates 0 = never walks).</summary>
        public static bool WantsToMove(in CourageRules r, double distance, int mates, bool shocked = false) =>
            !double.IsNaN(distance) && distance > RegroupRadius(in r, shocked) && mates < MatesNeeded(in r, shocked);

        /// <summary>A holding man walks again only beyond the regroup radius + <see cref="MoveAgainExtraMetres"/>, short of mates.</summary>
        public static bool MovesAgain(in CourageRules r, double distance, int mates, bool shocked = false) =>
            !double.IsNaN(distance) && distance > RegroupRadius(in r, shocked) + MoveAgainExtraMetres && mates < MatesNeeded(in r, shocked);

        /// <summary>Arrived: within the regroup radius of the centre.</summary>
        public static bool Arrived(in CourageRules r, double distance, bool shocked = false) => !double.IsNaN(distance) && distance <= RegroupRadius(in r, shocked);

        /// <summary>The walk back asks for a sprint: the centre farther than CourageSprintMetres, or CourageSprintEnemies enemies within the radius.</summary>
        public static bool SprintWanted(in CourageRules r, double distance, int enemies, bool panic = false) =>
            r.SprintBack && (panic || (!double.IsNaN(distance) && distance > r.SprintMetres) || enemies >= r.SprintEnemies);

        /// <summary>The regroup sprint's seconds: what he holds above CourageSprintKeepPercent of his full store (0 below the AI's 0.2 s).</summary>
        public static double SprintAmount(in CourageRules r, double stored, double capacity, bool panic = false)
        {
            if (double.IsNaN(capacity) || capacity <= 0 || double.IsNaN(stored)) return 0;
            if (stored > capacity) stored = capacity;
            double amount = stored - (panic ? r.PanicSprintKeepPercent : r.SprintKeepPercent) / 100.0 * capacity;
            return amount >= SprintMath.AiMinSeconds - 1e-9 ? amount : 0;
        }

        /// <summary>
        /// The walk's world direction: toward the centre (<paramref name="toX"/>, <paramref name="toY"/> - any length) plus
        /// <see cref="AwayWeight"/> × away from the enemies' weighted centre (<paramref name="awayX"/>, <paramref name="awayY"/>; zero = no
        /// enemy). When the two cancel (the centre lies beyond the enemies - a man who looped behind a wall) he goes AROUND: the side of
        /// "away" that leans toward the centre (left on a tie). False only with neither a centre nor an enemy to go by.
        /// </summary>
        public static bool Direction(double toX, double toY, double awayX, double awayY, out double dx, out double dy)
        {
            dx = dy = 0;
            bool hasTo = Unit(ref toX, ref toY);
            bool hasAway = Unit(ref awayX, ref awayY);
            if (!hasTo && !hasAway) return false;
            double x = (hasTo ? toX : 0) + (hasAway ? AwayWeight * awayX : 0);
            double y = (hasTo ? toY : 0) + (hasAway ? AwayWeight * awayY : 0);
            if (hasTo && hasAway && toX * awayX + toY * awayY < RoundCosine)
            {
                // the centre lies behind the enemies: go round them - the perpendicular of "away" on the centre's side
                double lx = -awayY, ly = awayX; // left of away
                double side = lx * toX + ly * toY;
                if (side < -1e-9)
                {
                    lx = -lx;
                    ly = -ly;
                }
                x = lx + AwayWeight * awayX;
                y = ly + AwayWeight * awayY;
            }
            if (!Unit(ref x, ref y)) return false;
            dx = x;
            dy = y;
            return true;
        }

        private static bool Unit(ref double x, ref double y)
        {
            double len = Math.Sqrt(x * x + y * y);
            if (double.IsNaN(len) || len < 1e-9)
            {
                x = y = 0;
                return false;
            }
            x /= len;
            y /= len;
            return true;
        }

        /// <summary>The timid Melee curve for a reach: (y1, x2, y2, x3, y3).</summary>
        public static void TimidMelee(double reach, out float y1, out float x2, out float y2, out float x3, out float y3)
        {
            if (double.IsNaN(reach) || reach < MinMeleeMetres) reach = MinMeleeMetres;
            if (reach > MaxMeleeMetres) reach = MaxMeleeMetres;
            y1 = TimidMeleeY1;
            x2 = (float)reach;
            y2 = TimidMeleeY2;
            x3 = (float)reach + TimidMeleeFade;
            y3 = TimidMeleeY3;
        }

        /// <summary>The game's behaviour curve (HumanAIComponent.BehaviorValues.GetValueAt): y1 at 0, a line to y2 at x2, to y3 at x3, then y3.</summary>
        public static double CurveAt(double x, double y1, double x2, double y2, double x3, double y3)
        {
            if (x <= x2) return x2 > 0 ? (y2 - y1) * x / x2 + y1 : y2;
            if (x <= x3) return x3 > x2 ? (y3 - y2) * (x - x2) / (x3 - x2) + y2 : y3;
            return y3;
        }

        // ---- the log's bands

        public const int OddsBands = 5;

        /// <summary>The local odds band: &lt; 0.25, 0.25-0.5, 0.5-1, 1-2, ≥ 2 (∞ included).</summary>
        public static int OddsBand(double odds) =>
            double.IsNaN(odds) ? 0 : odds < 0.25 ? 0 : odds < 0.5 ? 1 : odds < 1 ? 2 : odds < 2 ? 3 : 4;

        public static string OddsBandName(int band) => band switch
        {
            0 => "< 0.25",
            1 => "0.25-0.5",
            2 => "0.5-1",
            3 => "1-2",
            _ => ">= 2",
        };

        public static string PhaseName(CouragePhase p) => p switch
        {
            CouragePhase.Moving => "walking back",
            CouragePhase.Holding => "holding among his mates",
            _ => "brave",
        };

        public static string EndName(CourageEnd e) => e switch
        {
            CourageEnd.Brave => "brave again",
            CourageEnd.SwitchedOff => "switched off",
            CourageEnd.Excluded => "no longer looked at (yours / a rider / a wall / busy)",
            CourageEnd.LeftField => "left the field",
            CourageEnd.MissionEnd => "the battle ended",
            CourageEnd.Error => "an error",
            CourageEnd.FeverPress => "his fever pressed on (not shocked)",
            CourageEnd.Surge => "a surge",
            _ => e.ToString(),
        };

        public static string RegroupEndName(RegroupEnd e) => e switch
        {
            RegroupEnd.Arrived => "arrived (within the regroup radius)",
            RegroupEnd.AmongMates => "among his mates",
            RegroupEnd.EdgeAhead => "the ground ahead ends",
            RegroupEnd.TookTooLong => "gave up after " + MaxMovingSeconds.ToString("0", CultureInfo.InvariantCulture) + " s",
            RegroupEnd.BecameBrave => "brave on the way",
            RegroupEnd.Ended => "his timid spell ended",
            _ => e.ToString(),
        };

        public static string SkipName(CourageSkip s) => s switch
        {
            CourageSkip.NotAi => "the player's",
            CourageSkip.Mounted => "riders",
            CourageSkip.NoFormation => "no formation",
            CourageSkip.Busy => "busy (detached / an object / a ladder / routing)",
            CourageSkip.SiegeAttacker => "a siege's attackers",
            CourageSkip.WallHeld => "held in a shield wall",
            _ => s.ToString(),
        };

        public static string RefusalName(CourageSprintRefusal r) => r switch
        {
            CourageSprintRefusal.Off => "the sprint off",
            CourageSprintRefusal.Sprinting => "sprinting already",
            CourageSprintRefusal.NoStore => "no store",
            CourageSprintRefusal.ReserveKept => "the reserve kept",
            _ => r.ToString(),
        };
    }

    /// <summary>
    /// Step 49: one man's courage - brave / timid (walking back or holding), since when, the flips, the walk's world direction. Allocated
    /// once per tracked man (like his fever), reused; no per-tick allocation. Main thread (the logic's tick).
    /// </summary>
    public sealed class CourageMind
    {
        public CouragePhase Phase { get; private set; }

        public bool Timid => Phase != CouragePhase.Brave;

        /// <summary>When the running timid spell began (mission time).</summary>
        public double Since { get; private set; }

        /// <summary>When the running walk back began.</summary>
        public double MovingSince { get; private set; }

        /// <summary>The last flip timid ↔ brave (NaN = none yet) and the flips that came within <see cref="CourageMath.WobbleSeconds"/> of it.</summary>
        public double LastFlip { get; private set; } = double.NaN;

        public int Wobbles { get; private set; }

        /// <summary>The last look's numbers (the log).</summary>
        public double Odds = double.NaN;
        public double Required = double.NaN;
        public double LastLook = double.NaN;

        /// <summary>The last walk's end and how long it took.</summary>
        public RegroupEnd LastRegroupEnd { get; private set; } = RegroupEnd.Count;
        public double LastMovingSeconds { get; private set; }

        /// <summary>The walk's target - his formation's centre at the last look - and "away from the enemies" (a unit vector; 0 = none).</summary>
        public double CentreX, CentreY, AwayX, AwayY;

        /// <summary>The world direction last steered (a unit vector).</summary>
        public double DirX, DirY;

        /// <summary>The next ground check on the way (mission time).</summary>
        public double NextEdgeCheck;

        /// <summary>Our behaviour weights are on him (to give back the game's when he is brave again).</summary>
        public bool WeightsSet;

        /// <summary>This walk asked for its sprint already (one ask per walk).</summary>
        public bool SprintAsked;

        /// <summary>A timid spell ever (the summary's men).</summary>
        public bool EverTimid;

        /// <summary>Step 49b: a surge makes him brave until this (mission time; the look leaves him alone meanwhile).</summary>
        public double SurgeUntil = double.NegativeInfinity;

        public bool Surging(double now) => now < SurgeUntil;

        /// <summary>This spell began shocked (the summary's "by shock" vs "by the odds alone"); this walk goes to a bigger group of his side
        /// (not his formation's centre); this walk is a panic (its sprint keeps only the panic reserve); his distance from his formation's
        /// centre when the walk began (the compression measure).</summary>
        public bool ShockStart;
        public bool FlockTarget;
        public bool Panic;
        public double DistanceAtStart = double.NaN;

        /// <summary>
        /// One look: brave → timid below the required odds (walking back when far and short of mates, else holding); timid → brave at the
        /// required odds × (1 + hysteresis) after the minimum hold; a walk ends at the centre, among mates or after
        /// <see cref="CourageMath.MaxMovingSeconds"/>; a holding man left alone far out walks again.
        /// </summary>
        public CourageStep Look(in CourageRules r, double now, double odds, double required, double distance, int mates, bool shocked = false)
        {
            Odds = odds;
            Required = required;
            LastLook = now;
            if (Phase == CouragePhase.Brave)
            {
                if (!CourageMath.Timid(odds, required)) return CourageStep.None;
                Flip(now);
                Since = now;
                EverTimid = true;
                SprintAsked = false;
                if (CourageMath.WantsToMove(in r, distance, mates, shocked))
                {
                    Phase = CouragePhase.Moving;
                    MovingSince = now;
                    return CourageStep.TimidMoving;
                }
                Phase = CouragePhase.Holding;
                return CourageStep.TimidHolding;
            }
            if (Phase == CouragePhase.Moving)
            {
                // a walk back is finished before courage comes back - out of the enemy's reach is not yet "back with his group"
                // (Anton: "short of courage a man retreats to the formation he came from")
                if (CourageMath.Arrived(in r, distance, shocked)) return StopMoving(RegroupEnd.Arrived, now);
                if (mates >= CourageMath.MatesNeeded(in r, shocked)) return StopMoving(RegroupEnd.AmongMates, now);
                if (now - MovingSince >= CourageMath.MaxMovingSeconds) return StopMoving(RegroupEnd.TookTooLong, now);
                return CourageStep.None;
            }
            if (CourageMath.BraveAgain(in r, odds, required, now - Since))
            {
                Phase = CouragePhase.Brave;
                Flip(now);
                return CourageStep.Brave;
            }
            if (CourageMath.MovesAgain(in r, distance, mates, shocked))
            {
                Phase = CouragePhase.Moving;
                MovingSince = now;
                SprintAsked = false;
                return CourageStep.StartMoving;
            }
            return CourageStep.None;
        }

        /// <summary>The walk ends between looks (arrived on the way, the ground ahead ends): he holds.</summary>
        public CourageStep StopMoving(RegroupEnd why, double now)
        {
            if (Phase != CouragePhase.Moving) return CourageStep.None;
            EndMoving(why, now);
            Phase = CouragePhase.Holding;
            return CourageStep.StopMoving;
        }

        /// <summary>The timid spell ends for any other reason (switched off, excluded, left the field …) - he is brave (the game's AI).</summary>
        public void End(double now)
        {
            if (Phase == CouragePhase.Brave) return;
            if (Phase == CouragePhase.Moving) EndMoving(RegroupEnd.Ended, now);
            Phase = CouragePhase.Brave;
            Flip(now);
        }

        private void EndMoving(RegroupEnd why, double now)
        {
            LastRegroupEnd = why;
            LastMovingSeconds = now >= MovingSince ? now - MovingSince : 0;
        }

        private void Flip(double now)
        {
            if (!double.IsNaN(LastFlip) && now - LastFlip < CourageMath.WobbleSeconds) Wobbles++;
            LastFlip = now;
        }
    }
}

using System;
using System.Collections.Generic;

namespace TraxCombat.Core
{
    /// <summary>
    /// Every setting of the mod - the complete parameter table of docs/DESIGN.md, one entry per
    /// row, with type, range, MCM group, label and plain-words description. This is the ONE place
    /// a setting is declared: the config file, defaults.json, the MCM page, the log and the tests
    /// all walk <see cref="All"/>. DEFAULT VALUES are not here: each comes from defaults.json at the
    /// repo root, embedded at build (<see cref="DefaultsFile"/> - the one truth for defaults).
    /// SchemaTests parse DESIGN.md's table and fail on any drift in keys or types (DESIGN's
    /// Default column is the INITIAL value and is not compared); DefaultsFileTests check
    /// defaults.json against this list.
    ///
    /// Adding a setting: a row in DESIGN's table, an entry here (in the group it belongs to -
    /// the order here is the order in the files and in MCM), a typed property on
    /// <see cref="TraxSettings"/>, its key and value in defaults.json (then
    /// <see cref="DefaultsFile.RefreshCommand"/> writes its comments), all in the same commit.
    /// </summary>
    public static class SettingsSchema
    {
        // Static fields initialise in TEXT order: the counter and the groups must stay above
        // the parameters, and All must stay below them.
        private static int _next;

        public static readonly ParamGroup MasterGroup = new ParamGroup(0, "Master switch");
        public static readonly ParamGroup DamageGroup = new ParamGroup(1, "Damage randomness");
        public static readonly ParamGroup AthleticsGroup = new ParamGroup(2, "Athletics");
        public static readonly ParamGroup TiredGroup = new ParamGroup(3, "Tired fighters");
        public static readonly ParamGroup StepBackGroup = new ParamGroup(4, "Tired fighters step back");
        public static readonly ParamGroup RegenGroup = new ParamGroup(5, "Regeneration");
        public static readonly ParamGroup PlayerBarsGroup = new ParamGroup(6, "Bars - you and your target");
        public static readonly ParamGroup SquadBarsGroup = new ParamGroup(7, "Bars - your squads");
        public static readonly ParamGroup AdvancedGroup = new ParamGroup(8, "Advanced");

        // ------------------------------------------------------------------ master switch

        /// <summary>THE master switch (Anton, 2026-09-27). Every feature checks it FIRST, live:
        /// off = that feature steps aside and the game runs vanilla (CLAUDE.md hard requirement).</summary>
        public static readonly ParamDef ModEnabled = Bool("ModEnabled", MasterGroup,
            "Mod enabled",
            "Turn the whole mod off to play a battle exactly as vanilla - for example to fight the same battle with and without it and compare. Off: no damage rolls, no Athletics costs, refill or slow attacks, no bars; the log still records every battle (its summary says mod ON or OFF). Applies live, even mid-battle; switched back on, everyone starts with a full Athletics bar.");

        // ------------------------------------------------------------------ damage randomness

        public static readonly ParamDef DamageRandomEnabled = Bool("DamageRandomEnabled", DamageGroup,
            "Damage randomness",
            "On: every hit that lands deals a random share of its normal damage (see the spread). Off: damage is the game's own (the rest of the mod still runs).");

        public static readonly ParamDef DamageRandomPercent = Int("DamageRandomPercent", 0, 100, DamageGroup,
            "Spread (± %)",
            "How far a hit's damage may swing up or down, in percent. 50 means a 50-damage hit lands for anything from 25 to 75, rolled fresh on every hit. 0 turns the randomness off.");

        public static readonly ParamDef DamageRandomMelee = Bool("DamageRandomMelee", DamageGroup,
            "Randomize melee hits",
            "Randomize hits by swords, axes, maces, spears and fists, and horse-charge bumps.");

        public static readonly ParamDef DamageRandomRanged = Bool("DamageRandomRanged", DamageGroup,
            "Randomize ranged hits",
            "Randomize hits by arrows, bolts, sling stones and thrown weapons.");

        public static readonly ParamDef DamageRandomOnMounts = Bool("DamageRandomOnMounts", DamageGroup,
            "Randomize hits on horses",
            "Also randomize hits that land on horses and other mounts.");

        public static readonly ParamDef DamageRandomOnShields = Bool("DamageRandomOnShields", DamageGroup,
            "Randomize shield damage",
            "Also randomize the damage a shield takes when it blocks a blow. Off: shields wear down at the game's usual pace.");

        // ------------------------------------------------------------------ Athletics

        public static readonly ParamDef AthleticsEnabled = Bool("AthleticsEnabled", AthleticsGroup,
            "Athletics",
            "On: every fighter has an Athletics bar - his stamina, as big as his Athletics skill on the character screen - that his attacks drain and rest refills. Near the top of the bar he fights at full strength; below it his damage, swing speed and run speed fall, down to slow attacks when it is empty. Off: no Athletics bar at all.");

        public static readonly ParamDef AthleticsPoolFloor = Int("AthleticsPoolFloor", 0, 1000, AthleticsGroup,
            "Smallest bar (points)",
            "No fighter's Athletics bar is smaller than this, whatever his Athletics skill - so a recruit with skill 20 still gets 50 points (5 blows). 0 = no floor: the bar is exactly the skill (never below 1 point). Changing it mid-battle keeps everyone's share: a fighter at 60% stays at 60%.");

        public static readonly ParamDef AthleticsPoolPerSkill = Float("AthleticsPoolPerSkill", 0.1, 5, AthleticsGroup,
            "Bar points per skill point",
            "Athletics points per point of Athletics skill. 1.0: the bar tops at the skill - skill 180, a bar of 180. Changing it mid-battle keeps everyone's share.");

        public static readonly ParamDef AthleticsPeakPercent = Int("AthleticsPeakPercent", 10, 100, AthleticsGroup,
            "Full strength above (% of the bar)",
            "At or above this share of his own bar a fighter is at full strength: full damage upside, full swing speed, full run speed. Below it each of those falls in a straight line to its lowest value at an empty bar. 100 = only a full bar is full strength.");

        public static readonly ParamDef HealthCapsAthletics = Bool("HealthCapsAthletics", AthleticsGroup,
            "Wounds cap the bar",
            "On: a wounded fighter can only use the share of his bar that matches the health he has left - at 75% health, 75% of the bar; the rest is cut at once and never refills while the wound lasts. The full-strength line stays where it was, so a badly wounded fighter never gets back to full strength.");

        public static readonly ParamDef CostPerBlow = Float("CostPerBlow", 0, 100, AthleticsGroup,
            "Cost per blow",
            "Athletics points one attack costs before the hero and leader discounts, whatever the size of the bar - so a bigger Athletics skill means more blows. With the defaults a recruit (a 50 bar) empties after 5 blows, a legionary (130) after 13. 0 = attacks are free.");

        public static readonly ParamDef CostOnMiss = Bool("CostOnMiss", AthleticsGroup,
            "Misses cost too",
            "On: every attack costs Athletics, landed or not. Off: only attacks that hit something (a body, a shield, a parrying weapon) cost.");

        public static readonly ParamDef HeroCostMultiplier = Float("HeroCostMultiplier", 0, 2, AthleticsGroup,
            "Hero cost multiplier",
            "Heroes (lords, companions and you) pay this share of the cost per blow. 0.75 = a quarter less than a common soldier.");

        public static readonly ParamDef PartyLeaderCostMultiplier = Float("PartyLeaderCostMultiplier", 0, 2, AthleticsGroup,
            "Party leader multiplier",
            "The hero who leads the fighter's own party (you for your party, a lord for his) pays this share again, on top of the hero discount: 0.75 × 0.75 × 10 = about 5.6 per blow.");

        // ------------------------------------------------------------------ tired fighters (below the peak)

        public static readonly ParamDef ExhaustedAttackSpeedPercent = Int("ExhaustedAttackSpeedPercent", 5, 100, TiredGroup,
            "Attack speed when empty (%)",
            "Attack speed of a fighter whose Athletics is empty, in percent of normal: swings, thrusts, bow draw, crossbow reload, throws. Between the full-strength line and empty it falls in a straight line. 100 = attacks never slow down.");

        public static readonly ParamDef MinMoveSpeedMultiplier = Float("MinMoveSpeedMultiplier", 0.1, 1, TiredGroup,
            "Run speed when empty (x)",
            "Top speed on foot of a fighter whose Athletics is empty, times his normal top speed. Between the full-strength line and empty it falls in a straight line, so fresh men overtake tired ones. 1.0 = tired men run as fast as fresh ones.");

        public static readonly ParamDef MountMinSpeedMultiplier = Float("MountMinSpeedMultiplier", 0.1, 1, TiredGroup,
            "Horse speed when the rider is empty (x)",
            "Top speed of a horse whose rider's Athletics is empty, times its normal top speed, on the same straight line. 1.0 = horses never slow down, however tired the rider.");

        public static readonly ParamDef DamageBonusFollowsAthletics = Bool("DamageBonusFollowsAthletics", TiredGroup,
            "Damage upside follows Athletics",
            "On: the lucky side of the damage roll shrinks as the attacker tires - at full strength a hit can land up to +50%, halfway down to empty up to +25%, empty never above normal. The unlucky side never changes. Off: every attacker gets the full roll.");

        // ------------------------------------------------------------------ tired fighters step back (step 5d)

        public static readonly ParamDef StepBackEnabled = Bool("StepBackEnabled", StepBackGroup,
            "Tired fighters step back",
            "On: after a melee swing, a tired AI fighter on foot may step back a little, facing his enemy with his guard up, then return to his place in the formation - so the tired fall back and the fresh take the blows. Never you, never riders, never after a shot or a throw. Off mid-battle: everyone stepping back returns to his formation at once.");

        public static readonly ParamDef StepBackMaxChancePercent = Int("StepBackMaxChancePercent", 0, 100, StepBackGroup,
            "Chance when empty (%)",
            "Chance, in percent, that a fighter with an empty Athletics bar steps back after a melee swing. It falls in a straight line to 0 at the full-strength line: halfway down, half this chance; at full strength never. 0 = nobody steps back.");

        public static readonly ParamDef StepBackDistance = Float("StepBackDistance", 0.5, 5, StepBackGroup,
            "Step distance (m)",
            "How far back a fighter steps, in metres, straight away from the enemy he fights. A tired man walks slowly, so he may not get all the way before the time below runs out.");

        public static readonly ParamDef StepBackSeconds = Float("StepBackSeconds", 0.3, 5, StepBackGroup,
            "Step time (s)",
            "How long a step back lasts at most, in seconds. Then his formation takes him again and he walks back to his place.");

        public static readonly ParamDef StepBackEnemyRange = Float("StepBackEnemyRange", 1, 20, StepBackGroup,
            "Only with an enemy within (m)",
            "A fighter steps back only while the enemy he fights is at most this many metres away - with nobody close there is nothing to step back from.");

        public static readonly ParamDef StepBackHoldAttacks = Bool("StepBackHoldAttacks", StepBackGroup,
            "No swings while stepping back",
            "On: a fighter stepping back does not attack - guard up only - until he is back with his formation. Off: he may still strike while he backs away.");

        public static readonly ParamDef StepBackMaxAtOnce = Int("StepBackMaxAtOnce", 1, 1000, StepBackGroup,
            "Most at once (whole battle)",
            "At most this many fighters, on all sides together, step back at the same time. It keeps big battles cheap and stops a whole front line from stepping back together.");

        // ------------------------------------------------------------------ regeneration

        public static readonly ParamDef RegenDelayBlowTimes = Float("RegenDelayBlowTimes", 0, 20, RegenGroup,
            "Rest before refill (blows)",
            "How long a fighter must go without attacking before his Athletics starts to refill, counted in blow lengths (see the next setting). 2 × 1.5 s = 3 seconds.");

        public static readonly ParamDef BlowTimeSeconds = Float("BlowTimeSeconds", 0.1, 10, RegenGroup,
            "Length of one blow (s)",
            "How many seconds one blow counts as, for the rest delay above.");

        public static readonly ParamDef FullRegenSecondsStanding = Float("FullRegenSecondsStanding", 1, 600, RegenGroup,
            "Refill time at rest (s)",
            "Seconds to refill from empty to full while standing still or walking.");

        public static readonly ParamDef RegenMultiplierAtFullRun = Float("RegenMultiplierAtFullRun", 0, 1, RegenGroup,
            "Refill rate at a full run (x)",
            "How fast Athletics refills while running flat out (or riding at the horse's top speed), times the rate at rest. Between a walk and a full run it falls in a straight line. 1.0 = running refills as fast as resting; 0 = no refill at a full run.");

        public static readonly ParamDef WalkEffortFraction = Float("WalkEffortFraction", 0.05, 1, RegenGroup,
            "Walking pace (share of top speed)",
            "Up to this share of his current top speed a fighter (or the horse he rides) counts as walking and refills at the full rate. The game walks people at 1.8 m/s and their top speed is about 4 to 5 m/s, so 0.4 covers a walk. 1.0 = any pace refills at the full rate.");

        // ------------------------------------------------------------------ bars: you and your target

        public static readonly ParamDef ShowPlayerBar = Bool("ShowPlayerBar", PlayerBarsGroup,
            "Your Athletics bar",
            "Show your own Athletics next to your health bar.");

        public static readonly ParamDef ShowTargetBar = Bool("ShowTargetBar", PlayerBarsGroup,
            "Target's Athletics bar",
            "Show a small Athletics bar for the fighter you are looking or aiming at. Aiming at a horse shows its rider.");

        public static readonly ParamDef TargetBarMaxDistance = Float("TargetBarMaxDistance", 1, 200, PlayerBarsGroup,
            "Target bar range (m)",
            "How far away, in metres, a fighter you look at still gets a bar.");

        public static readonly ParamDef TargetBarLingerSeconds = Float("TargetBarLingerSeconds", 0, 10, PlayerBarsGroup,
            "Target bar linger (s)",
            "Seconds the target's bar stays after your aim leaves him, so it does not flicker.");

        // The bar colours (DESIGN §3 additions, step 6) - thresholds on f, the share of the
        // full-strength line left: green at or above the line, blue just below it, then these.
        public static readonly ParamDef BarYellowBelowPercent = Int("BarYellowBelowPercent", 0, 100, PlayerBarsGroup,
            "Yellow at or below (% of the line)",
            "Colour of the Athletics bars: green at or above the full-strength line, blue just below it, and yellow once the Athletics left is at or below this percent of the line. With the line at 75% of the bar, 75 turns the bar yellow from about 56% of the bar down.");

        public static readonly ParamDef BarOrangeBelowPercent = Int("BarOrangeBelowPercent", 0, 100, PlayerBarsGroup,
            "Orange at or below (% of the line)",
            "The Athletics bars turn orange once the Athletics left is at or below this percent of the full-strength line (50: half of the line, about 38% of the bar with the line at 75%).");

        public static readonly ParamDef BarRedBelowPercent = Int("BarRedBelowPercent", 0, 100, PlayerBarsGroup,
            "Red at or below (% of the line)",
            "The Athletics bars turn red once the Athletics left is at or below this percent of the full-strength line (25: a quarter of the line, about 19% of the bar with the line at 75%). An empty bar is always red, and its number turns red too.");

        // ------------------------------------------------------------------ bars: your squads

        public static readonly ParamDef ShowFormationBars = Bool("ShowFormationBars", SquadBarsGroup,
            "Squad bars",
            "Show the average Athletics of each of your formations in a bar floating above it.");

        public static readonly ParamDef FormationBarsAlways = Bool("FormationBarsAlways", SquadBarsGroup,
            "Squad bars always",
            "On: the squad bars are always shown. Off: only while the game shows its own formation markers (marker key held or orders menu open).");

        public static readonly ParamDef ShowFormationSpread = Bool("ShowFormationSpread", SquadBarsGroup,
            "Show the spread",
            "Draw a band on each squad bar showing how far the men's Athletics spreads around the average.");

        public static readonly ParamDef FormationSpreadStdDevs = Float("FormationSpreadStdDevs", 0, 3, SquadBarsGroup,
            "Spread band width (std devs)",
            "Width of that band on each side of the average, in standard deviations. 1 = about two men in three fall inside it.");

        public static readonly ParamDef FormationBarHeight = Float("FormationBarHeight", 0, 10, SquadBarsGroup,
            "Squad bar height (m)",
            "How high above the formation's centre its bar floats, in metres.");

        public static readonly ParamDef ShowInOrderMenu = Bool("ShowInOrderMenu", SquadBarsGroup,
            "Orders menu panel",
            "While the orders menu is open, list each formation's average Athletics and spread, for example \"Infantry 72 ± 8\".");

        // ------------------------------------------------------------------ advanced

        public static readonly ParamDef HudRefreshSeconds = Float("HudRefreshSeconds", 0.02, 1, AdvancedGroup,
            "Bar refresh (s)",
            "How often your bar and the target's bar update, in seconds. Lower is smoother and costs a little more.");

        // Where your bar sits (step 6) - UI pixels of the game's 1920 x 1080 reference layout; the
        // game's own UI scale multiplies them, exactly as it does the vanilla health bar's.
        public static readonly ParamDef PlayerBarWidth = Int("PlayerBarWidth", 40, 800, AdvancedGroup,
            "Your bar: length (px)",
            "Length of your Athletics bar, in the game's UI pixels (the 1920 x 1080 layout; the game's UI scale applies). 205 matches the inside of the vanilla health bar above it.");

        public static readonly ParamDef PlayerBarHeight = Int("PlayerBarHeight", 2, 40, AdvancedGroup,
            "Your bar: thickness (px)",
            "Thickness of your Athletics bar, in the game's UI pixels.");

        public static readonly ParamDef PlayerBarOffsetRight = Int("PlayerBarOffsetRight", 0, 1800, AdvancedGroup,
            "Your bar: from the right edge (px)",
            "Distance from the right edge of the screen to the right end of your Athletics bar, in the game's UI pixels. 62 lines it up under the vanilla health bar.");

        public static readonly ParamDef PlayerBarOffsetBottom = Int("PlayerBarOffsetBottom", 0, 1000, AdvancedGroup,
            "Your bar: from the bottom edge (px)",
            "Distance from the bottom of the screen to your bar's row (the word Athletics, the number and the bar), in the game's UI pixels. 54 puts it just under the vanilla health and horse bars.");

        public static readonly ParamDef FormationStatsRefreshSeconds = Float("FormationStatsRefreshSeconds", 0.05, 2, AdvancedGroup,
            "Squad stats refresh (s)",
            "How often the squad averages and spreads are recomputed, in seconds.");

        public static readonly ParamDef VerboseLogging = Bool("VerboseLogging", AdvancedGroup,
            "Verbose log",
            "Write every damage roll, blow and exhaustion to trax_combat.log (rate-limited, so a big battle cannot flood it). Off: only loading, settings, battle start and end, battle summaries and errors.");

        // ------------------------------------------------------------------ the list (keep LAST)

        /// <summary>Every setting, in file and MCM order. <c>All[i].Index == i</c>.</summary>
        public static readonly IReadOnlyList<ParamDef> All = new[]
        {
            ModEnabled,
            DamageRandomEnabled, DamageRandomPercent, DamageRandomMelee, DamageRandomRanged,
            DamageRandomOnMounts, DamageRandomOnShields,
            AthleticsEnabled, AthleticsPoolFloor, AthleticsPoolPerSkill, AthleticsPeakPercent, HealthCapsAthletics,
            CostPerBlow, CostOnMiss, HeroCostMultiplier, PartyLeaderCostMultiplier,
            ExhaustedAttackSpeedPercent, MinMoveSpeedMultiplier, MountMinSpeedMultiplier, DamageBonusFollowsAthletics,
            StepBackEnabled, StepBackMaxChancePercent, StepBackDistance, StepBackSeconds, StepBackEnemyRange,
            StepBackHoldAttacks, StepBackMaxAtOnce,
            RegenDelayBlowTimes, BlowTimeSeconds, FullRegenSecondsStanding, RegenMultiplierAtFullRun,
            WalkEffortFraction,
            ShowPlayerBar, ShowTargetBar, TargetBarMaxDistance, TargetBarLingerSeconds,
            BarYellowBelowPercent, BarOrangeBelowPercent, BarRedBelowPercent,
            ShowFormationBars, FormationBarsAlways, ShowFormationSpread, FormationSpreadStdDevs,
            FormationBarHeight, ShowInOrderMenu,
            HudRefreshSeconds, PlayerBarWidth, PlayerBarHeight, PlayerBarOffsetRight, PlayerBarOffsetBottom,
            FormationStatsRefreshSeconds, VerboseLogging,
        };

        /// <summary>The groups in order.</summary>
        public static readonly IReadOnlyList<ParamGroup> Groups = new[]
        {
            MasterGroup, DamageGroup, AthleticsGroup, TiredGroup, StepBackGroup, RegenGroup, PlayerBarsGroup, SquadBarsGroup, AdvancedGroup,
        };

        private static readonly Dictionary<string, ParamDef> ByKey = BuildIndex();

        /// <summary>Finds a setting by key, ignoring case (a hand edit of "damageRandomPercent"
        /// still counts).</summary>
        public static bool TryGet(string key, out ParamDef def)
        {
            if (key != null && ByKey.TryGetValue(key, out var found))
            {
                def = found;
                return true;
            }
            def = null!;
            return false;
        }

        /// <summary>The settings of one group, in order.</summary>
        public static IEnumerable<ParamDef> InGroup(ParamGroup group)
        {
            foreach (var p in All)
                if (p.Group == group) yield return p;
        }

        private static Dictionary<string, ParamDef> BuildIndex()
        {
            var map = new Dictionary<string, ParamDef>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < All.Count; i++)
            {
                var p = All[i];
                if (p.Index != i)
                    throw new InvalidOperationException("SettingsSchema.All is out of declaration order at " + p.Key);
                map.Add(p.Key, p);
            }
            return map;
        }

        // No default values here - each ParamDef takes its default from defaults.json (DefaultsFile).

        private static ParamDef Bool(string key, ParamGroup group, string label, string description,
            ApplyTiming timing = ApplyTiming.Live)
            => new ParamDef(_next++, key, ParamType.Bool, 0, 1, group, label, description, timing);

        private static ParamDef Int(string key, int min, int max, ParamGroup group, string label,
            string description, ApplyTiming timing = ApplyTiming.Live)
            => new ParamDef(_next++, key, ParamType.Int, min, max, group, label, description, timing);

        private static ParamDef Float(string key, double min, double max, ParamGroup group, string label,
            string description, ApplyTiming timing = ApplyTiming.Live)
            => new ParamDef(_next++, key, ParamType.Float, min, max, group, label, description, timing);
    }
}

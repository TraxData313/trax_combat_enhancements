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

        // The words a player reads (step 10b): the pool, the bar and its points are "Athletics"; the
        // character-screen skill is "the Athletics skill"; the "peak line" is the white mark on the bar
        // (AthleticsPeakPercent) - at or above it a fighter is at full strength; "empty" = 0 Athletics.
        // Units in the label: (points) (%) (x) (m) (s) (px) (MB). Every setting applies at once, even
        // mid-battle (ParamDef.HintText says so in MCM); a description adds only what a mid-battle
        // change does beyond that.

        public static readonly ParamGroup MasterGroup = new ParamGroup(0, "Master switch");
        public static readonly ParamGroup DamageGroup = new ParamGroup(1, "Damage randomness");
        public static readonly ParamGroup AthleticsGroup = new ParamGroup(2, "Athletics");
        public static readonly ParamGroup TiredGroup = new ParamGroup(3, "Tired fighters");
        public static readonly ParamGroup StepBackGroup = new ParamGroup(4, "Tired fighters step back");
        public static readonly ParamGroup RefillGroup = new ParamGroup(5, "Refill");
        public static readonly ParamGroup PlayerBarGroup = new ParamGroup(6, "Your Athletics bar");
        public static readonly ParamGroup OrderStripGroup = new ParamGroup(7, "Orders menu strip");
        public static readonly ParamGroup AdvancedGroup = new ParamGroup(8, "Advanced");

        // ------------------------------------------------------------------ master switch

        /// <summary>THE master switch (Anton, 2026-09-27). Every feature checks it FIRST, live:
        /// off = that feature steps aside and the game runs vanilla (CLAUDE.md hard requirement).</summary>
        public static readonly ParamDef ModEnabled = Bool("ModEnabled", MasterGroup,
            "Mod enabled",
            "Turn the whole mod off and every battle is plain vanilla: no damage rolls, no Athletics, no slow attacks, no step backs, no bars. The log still records each battle (its summary says mod ON or OFF), so the same battle can be fought both ways and compared. Switched back on, everyone starts with a full Athletics bar.");

        // ------------------------------------------------------------------ damage randomness

        public static readonly ParamDef DamageRandomEnabled = Bool("DamageRandomEnabled", DamageGroup,
            "Damage randomness",
            "On: every hit that lands deals a random share of its normal damage (see Spread). Off: every hit deals the game's own damage; the rest of the mod still runs.");

        public static readonly ParamDef DamageRandomPercent = Int("DamageRandomPercent", 0, 100, DamageGroup,
            "Spread (± %)",
            "How far a hit's damage may swing up or down, in percent. 50: a 50-damage hit lands for anything from 25 to 75, rolled fresh on every hit. 0 turns the randomness off.");

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
            "On: every fighter has an Athletics bar - his stamina, as big as his Athletics skill - that his attacks drain and rest refills. At or above the peak line he fights at full strength; below it his damage upside, attack speed and run speed fall, down to slow attacks and a slow run when it is empty, and tired AI fighters step back. Off: no Athletics at all - no costs, no slowing, no step backs, no bars.");

        public static readonly ParamDef AthleticsPoolFloor = Int("AthleticsPoolFloor", 0, 1000, AthleticsGroup,
            "Smallest bar (points)",
            "No fighter's Athletics bar is smaller than this, whatever his Athletics skill - so a recruit with Athletics skill 20 still gets a 50-point bar (5 blows). 0 = no floor: the bar is exactly the skill (never below 1 point). Changed mid-battle, everyone keeps his share: a fighter at 60% stays at 60%.");

        public static readonly ParamDef AthleticsPoolPerSkill = Float("AthleticsPoolPerSkill", 0.1, 5, AthleticsGroup,
            "Bar points per skill point",
            "Athletics points per point of Athletics skill. 1.0: the bar tops at the skill - skill 180, a 180-point bar. Changed mid-battle, everyone keeps his share.");

        public static readonly ParamDef AthleticsPeakPercent = Int("AthleticsPeakPercent", 10, 100, AthleticsGroup,
            "Peak line (% of the bar)",
            "Where the peak line sits - the white mark on the bar - in percent of each fighter's own bar. At or above it he is at full strength: full damage upside, attack speed and run speed. Below it each of those falls in a straight line to its lowest value at an empty bar. 100 = only a full bar is full strength.");

        public static readonly ParamDef HealthCapsAthletics = Bool("HealthCapsAthletics", AthleticsGroup,
            "Wounds cap the bar",
            "On: a wounded fighter can only use the share of his bar that matches the health he has left - at 75% health, 75% of the bar; the rest is cut at once and does not refill while the wound lasts. The peak line stays where it was, so a badly wounded fighter never gets back to full strength.");

        public static readonly ParamDef CostPerBlow = Float("CostPerBlow", 0, 100, AthleticsGroup,
            "Cost per blow (points)",
            "Athletics points one attack costs a common soldier, whatever the size of his bar - so a bigger Athletics skill means more blows. With the defaults a recruit (a 50-point bar) is empty after 5 blows, a legionary (130) after 13. 0 = attacks are free.");

        public static readonly ParamDef CostOnMiss = Bool("CostOnMiss", AthleticsGroup,
            "Misses cost too",
            "On: every attack costs Athletics, landed or not. Off: only attacks that hit something (a body, a shield, a parrying weapon) cost.");

        public static readonly ParamDef HeroCostMultiplier = Float("HeroCostMultiplier", 0, 2, AthleticsGroup,
            "Hero cost (x)",
            "Heroes (lords, companions and you) pay the cost per blow times this. 0.75 = a quarter less than a common soldier.");

        public static readonly ParamDef PartyLeaderCostMultiplier = Float("PartyLeaderCostMultiplier", 0, 2, AthleticsGroup,
            "Party leader cost (x)",
            "The hero who leads the fighter's own party (you for your party, a lord for his) pays times this again, on top of the hero price: 10 × 0.75 × 0.75 = about 5.6 per blow.");

        // ------------------------------------------------------------------ tired fighters (below the peak line)

        public static readonly ParamDef ExhaustedAttackSpeedPercent = Int("ExhaustedAttackSpeedPercent", 5, 100, TiredGroup,
            "Attack speed when empty (%)",
            "How fast a fighter with an empty bar attacks, in percent of normal: the wind-up and swing, thrusts, bow draw, crossbow reload and throws - and, for the AI, the pause between attacks (the next two switches). 20 = one attack where a fresh man makes five. From the peak line down to empty it falls in a straight line. 100 = attacks never slow down. Blocking is never slowed.");

        public static readonly ParamDef AttackRateAiDecisions = Bool("AttackRateAiDecisions", TiredGroup,
            "Tired AI attack less often",
            "On: a tired AI fighter also decides to attack (and to strike back after a parry) less often, looses arrows less readily and aims longer before a shot - by his attack speed - so his whole rhythm slows, not only the swing. Blocking is never touched. Off: only the animations slow down.");

        public static readonly ParamDef AttackRatePaceHold = Bool("AttackRatePaceHold", TiredGroup,
            "Tired AI keep a slower pace",
            "On: after each melee swing a tired AI fighter on foot holds his next attack, guard up, until his time between attacks has grown to his fresh rhythm divided by his attack speed - at 50% attack speed one attack every 2 seconds instead of every second. Never you, never riders. Off mid-battle: every held fighter may attack again at once.");

        public static readonly ParamDef MinMoveSpeedMultiplier = Float("MinMoveSpeedMultiplier", 0.1, 1, TiredGroup,
            "Run speed when empty (x)",
            "Top speed on foot of a fighter with an empty bar, times his normal top speed. From the peak line down to empty it falls in a straight line, so fresh men overtake tired ones. 1.0 = tired men run as fast as fresh ones.");

        public static readonly ParamDef MountMinSpeedMultiplier = Float("MountMinSpeedMultiplier", 0.1, 1, TiredGroup,
            "Horse speed when the rider is empty (x)",
            "Top speed of a horse whose rider's bar is empty, times its normal top speed, on the same straight line. 1.0 = horses never slow down, however tired the rider.");

        public static readonly ParamDef DamageBonusFollowsAthletics = Bool("DamageBonusFollowsAthletics", TiredGroup,
            "Damage upside follows Athletics",
            "On: the lucky side of the damage roll shrinks as the attacker tires - at or above the peak line a hit can land up to +50% (with Spread at 50), halfway down to empty up to +25%, empty never above normal. The unlucky side never changes. Off: every attacker gets the full roll.");

        // ------------------------------------------------------------------ tired fighters step back (step 5d)

        public static readonly ParamDef StepBackEnabled = Bool("StepBackEnabled", StepBackGroup,
            "Tired fighters step back",
            "On: after a melee swing, a tired AI fighter on foot may step back a little, facing his enemy with his guard up, then return to his place in the formation - so the tired fall back and the fresh take the blows. Never you, never riders, never after a shot or a throw; field battles only. Off mid-battle: everyone stepping back returns to his formation at once.");

        public static readonly ParamDef StepBackMaxChancePercent = Int("StepBackMaxChancePercent", 0, 100, StepBackGroup,
            "Chance when empty (%)",
            "Chance, in percent, that a fighter with an empty bar steps back after a melee swing. It falls in a straight line to 0 at the peak line: halfway down, half this chance; at full strength never. 0 = nobody steps back.");

        public static readonly ParamDef StepBackDistance = Float("StepBackDistance", 0.5, 5, StepBackGroup,
            "Step distance (m)",
            "How far back a fighter steps, in metres, straight away from the enemy he fights. A tired man walks slowly, so he may not get all the way before the step time runs out.");

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

        // ------------------------------------------------------------------ refill (regeneration)

        public static readonly ParamDef RegenDelayBlowTimes = Float("RegenDelayBlowTimes", 0, 20, RefillGroup,
            "Rest before refill (blows)",
            "How long a fighter must go without attacking before his Athletics starts to refill, counted in blow lengths (the next setting): 2 × 1.5 s = 3 seconds.");

        public static readonly ParamDef BlowTimeSeconds = Float("BlowTimeSeconds", 0.1, 10, RefillGroup,
            "Length of one blow (s)",
            "How many seconds one blow counts as, for the rest before refill.");

        public static readonly ParamDef FullRegenSecondsStanding = Float("FullRegenSecondsStanding", 1, 600, RefillGroup,
            "Refill time at rest (s)",
            "Seconds to refill an empty bar to full while standing still or walking.");

        public static readonly ParamDef RegenMultiplierAtFullRun = Float("RegenMultiplierAtFullRun", 0, 1, RefillGroup,
            "Refill rate at a full run (x)",
            "How fast Athletics refills while running flat out (or riding at the horse's top speed), times the rate at rest. Between a walk and a full run it falls in a straight line. 1.0 = running refills as fast as resting; 0 = no refill at a full run.");

        public static readonly ParamDef WalkEffortFraction = Float("WalkEffortFraction", 0.05, 1, RefillGroup,
            "Walking pace (share of top speed)",
            "Up to this share of his current top speed a fighter (or the horse he rides) counts as walking and refills at the full rate. The game walks people at 1.8 m/s and their top speed is about 4 to 5 m/s, so 0.4 covers a walk. 1.0 = any pace refills at the full rate.");

        // ------------------------------------------------------------------ your Athletics bar (step 6)

        public static readonly ParamDef ShowPlayerBar = Bool("ShowPlayerBar", PlayerBarGroup,
            "Your Athletics bar",
            "Your Athletics bar, bottom right under your health bar: the Athletics you have left and your whole bar (\"132 / 180\"), a white mark at the peak line, the fill in the colour of your strength (green at full strength, then blue, yellow, orange, red) and the part your wounds hold shown dark.");

        // The looked-at fighter's bar (ShowTargetBar, TargetBarMaxDistance, TargetBarLingerSeconds) is
        // LATER (step 7): its settings left the schema in step 10b - no switch that does nothing
        // (review R21). DESIGN's "Planned parameters" keeps the rows.

        // The bar colours (DESIGN §3 additions, step 6) - thresholds on f, the share of the peak line
        // left: green at or above the line, blue just below it, then these. The strip uses them too.
        public static readonly ParamDef BarYellowBelowPercent = Int("BarYellowBelowPercent", 0, 100, PlayerBarGroup,
            "Yellow at or below (% of the peak line)",
            "The colours of your bar and of the orders-menu strip: green at or above the peak line, blue just below it, and yellow once the Athletics left is at or below this percent of the peak line. With the line at 75% of the bar, 75 turns it yellow from about 56% of the bar down.");

        public static readonly ParamDef BarOrangeBelowPercent = Int("BarOrangeBelowPercent", 0, 100, PlayerBarGroup,
            "Orange at or below (% of the peak line)",
            "Orange once the Athletics left is at or below this percent of the peak line (50: about 38% of the bar with the line at 75%).");

        public static readonly ParamDef BarRedBelowPercent = Int("BarRedBelowPercent", 0, 100, PlayerBarGroup,
            "Red at or below (% of the peak line)",
            "Red once the Athletics left is at or below this percent of the peak line (25: about 19% of the bar with the line at 75%). An empty bar is always red, and your bar's word and number turn red too.");

        // ------------------------------------------------------------------ the orders-menu strip (step 9)

        // The squad bars floating above the formations (ShowFormationBars, FormationBarsAlways,
        // FormationBarHeight) are LATER (step 8): their settings left the schema in step 10b (review
        // R21). DESIGN's "Planned parameters" keeps the rows. The spread and health settings below
        // will serve them too.

        public static readonly ParamDef ShowInOrderMenu = Bool("ShowInOrderMenu", OrderStripGroup,
            "Orders menu strip",
            "While the orders menu is open, a slim strip under each of your formation cards: the men's average Athletics, in percent of their own bars, with its spread (\"72% ± 8\"), over a bar in the colour of their strength. You are not counted - you have your own bar.");

        public static readonly ParamDef ShowFormationHealth = Bool("ShowFormationHealth", OrderStripGroup,
            "Show average health",
            "The strip also shows the men's average health left, for example \"HP 81%\".");

        public static readonly ParamDef ShowFormationSpread = Bool("ShowFormationSpread", OrderStripGroup,
            "Show the spread",
            "The strip shows how far the men's Athletics spreads around the average: the \"± 8\" after the number and a lighter band on its bar. Off: the average alone.");

        public static readonly ParamDef FormationSpreadStdDevs = Float("FormationSpreadStdDevs", 0, 3, OrderStripGroup,
            "Spread width (std devs)",
            "How wide the spread is on each side of the average, in standard deviations - the strip's \"± 8\" is this width, in percent of the bar. 1 = about two men in three fall inside it.");

        public static readonly ParamDef OrderStripUnderCards = Bool("OrderStripUnderCards", OrderStripGroup,
            "Strip under the cards",
            "On: the strip sits under each of the game's formation cards (read live, so it follows any resolution, UI scale or order-menu mod that keeps the cards). Off - or whenever the cards cannot be matched - a compact panel at the top of the screen lists the same numbers instead.");

        // ------------------------------------------------------------------ advanced

        public static readonly ParamDef HudRefreshSeconds = Float("HudRefreshSeconds", 0.02, 1, AdvancedGroup,
            "Bar refresh (s)",
            "How often your Athletics bar and the orders-menu strip update, in seconds. Lower is smoother and costs a little more.");

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

        // The orders-menu strip (step 9) - UI pixels of the 1080p layout, measured from each vanilla
        // formation card (read live); the fallback panel's place. The game's UI scale applies.
        public static readonly ParamDef OrderStripTextSize = Int("OrderStripTextSize", 8, 30, AdvancedGroup,
            "Orders strip: text size (px)",
            "Font size of the orders-menu strip's numbers, in the game's UI pixels.");

        public static readonly ParamDef OrderStripTextOffset = Int("OrderStripTextOffset", -40, 60, AdvancedGroup,
            "Orders strip: numbers offset (px)",
            "Distance from the bottom of a formation card to the top of its numbers, in the game's UI pixels. The numbers sit at the left and right ends, leaving the middle to the game's order icons.");

        public static readonly ParamDef OrderStripBarOffset = Int("OrderStripBarOffset", -40, 60, AdvancedGroup,
            "Orders strip: bar offset (px)",
            "Distance from the bottom of a formation card to the top of its Athletics bar, in the game's UI pixels. 20 puts it just under the game's order icons, which hang 20 px below a card.");

        public static readonly ParamDef OrderStripBarHeight = Int("OrderStripBarHeight", 1, 20, AdvancedGroup,
            "Orders strip: bar thickness (px)",
            "Thickness of the orders-menu strip's Athletics bar, in the game's UI pixels.");

        public static readonly ParamDef OrderStripSideMargin = Int("OrderStripSideMargin", 0, 40, AdvancedGroup,
            "Orders strip: side margin (px)",
            "How far the strip's numbers and bar keep in from a card's left and right edges, in the game's UI pixels.");

        public static readonly ParamDef OrderPanelOffsetTop = Int("OrderPanelOffsetTop", 0, 1000, AdvancedGroup,
            "Orders panel: from the top (px)",
            "Where the fallback panel sits (used when the cards cannot be matched, or with Strip under the cards off): distance from the top of the screen, in the game's UI pixels. It is centred left to right.");

        public static readonly ParamDef OrderPanelWidth = Int("OrderPanelWidth", 120, 900, AdvancedGroup,
            "Orders panel: width (px)",
            "Width of the fallback panel, in the game's UI pixels.");

        public static readonly ParamDef FormationStatsRefreshSeconds = Float("FormationStatsRefreshSeconds", 0.05, 2, AdvancedGroup,
            "Strip averages refresh (s)",
            "How often the orders-menu strip's averages (Athletics, spread, health) are worked out, in seconds of battle time.");

        public static readonly ParamDef VerboseLogging = Bool("VerboseLogging", AdvancedGroup,
            "Verbose log",
            "Also write every damage roll, blow, step back and pace hold to trax_combat.log, rate-limited so a big battle cannot flood it. These lines carry a ~ before their tag and are the only ones a trim cuts. Off: loading, settings, battle start and end, first-time events, summaries and errors only.");

        // Step 10b (review R7): the log's size cap, and what a trim keeps (LogTrim).
        public static readonly ParamDef LogMaxMegabytes = Int("LogMaxMegabytes", 1, 100, AdvancedGroup,
            "Log size limit (MB)",
            "Largest size of the mod's log, trax_combat.log, in megabytes. Past it the oldest verbose lines are cut, down to about half the limit; every other line - loading, settings, battle start and end, first-time events, summaries, errors - is kept.");

        // ------------------------------------------------------------------ the list (keep LAST)

        /// <summary>Every setting, in file and MCM order (= declaration order). <c>All[i].Index == i</c>.</summary>
        public static readonly IReadOnlyList<ParamDef> All = new[]
        {
            ModEnabled,
            DamageRandomEnabled, DamageRandomPercent, DamageRandomMelee, DamageRandomRanged,
            DamageRandomOnMounts, DamageRandomOnShields,
            AthleticsEnabled, AthleticsPoolFloor, AthleticsPoolPerSkill, AthleticsPeakPercent, HealthCapsAthletics,
            CostPerBlow, CostOnMiss, HeroCostMultiplier, PartyLeaderCostMultiplier,
            ExhaustedAttackSpeedPercent, AttackRateAiDecisions, AttackRatePaceHold, MinMoveSpeedMultiplier, MountMinSpeedMultiplier, DamageBonusFollowsAthletics,
            StepBackEnabled, StepBackMaxChancePercent, StepBackDistance, StepBackSeconds, StepBackEnemyRange,
            StepBackHoldAttacks, StepBackMaxAtOnce,
            RegenDelayBlowTimes, BlowTimeSeconds, FullRegenSecondsStanding, RegenMultiplierAtFullRun,
            WalkEffortFraction,
            ShowPlayerBar, BarYellowBelowPercent, BarOrangeBelowPercent, BarRedBelowPercent,
            ShowInOrderMenu, ShowFormationHealth, ShowFormationSpread, FormationSpreadStdDevs, OrderStripUnderCards,
            HudRefreshSeconds, PlayerBarWidth, PlayerBarHeight, PlayerBarOffsetRight, PlayerBarOffsetBottom,
            OrderStripTextSize, OrderStripTextOffset, OrderStripBarOffset, OrderStripBarHeight, OrderStripSideMargin,
            OrderPanelOffsetTop, OrderPanelWidth,
            FormationStatsRefreshSeconds, VerboseLogging, LogMaxMegabytes,
        };

        /// <summary>The groups in order.</summary>
        public static readonly IReadOnlyList<ParamGroup> Groups = new[]
        {
            MasterGroup, DamageGroup, AthleticsGroup, TiredGroup, StepBackGroup, RefillGroup, PlayerBarGroup, OrderStripGroup, AdvancedGroup,
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

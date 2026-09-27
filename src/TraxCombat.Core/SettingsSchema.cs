using System;
using System.Collections.Generic;

namespace TraxCombat.Core
{
    /// <summary>
    /// Every setting of the mod - the complete parameter table of docs/DESIGN.md, one entry per
    /// row, with range, MCM group and plain-words description. This is the ONE place a setting
    /// is declared: the config file, the MCM page, the log and the tests all walk
    /// <see cref="All"/>. A test (SchemaMatchesDesignTests) parses DESIGN.md's table and fails
    /// on any drift in keys or defaults.
    ///
    /// Adding a setting: a row in DESIGN's table, an entry here (in the group it belongs to -
    /// the order here is the order in the file and in MCM), a typed property on
    /// <see cref="TraxSettings"/>, all in the same commit.
    /// </summary>
    public static class SettingsSchema
    {
        // Static fields initialise in TEXT order: the counter and the groups must stay above
        // the parameters, and All must stay below them.
        private static int _next;

        public static readonly ParamGroup DamageGroup = new ParamGroup(0, "Damage randomness");
        public static readonly ParamGroup AthleticsGroup = new ParamGroup(1, "Athletics");
        public static readonly ParamGroup ExhaustionGroup = new ParamGroup(2, "Exhaustion");
        public static readonly ParamGroup RegenGroup = new ParamGroup(3, "Regeneration");
        public static readonly ParamGroup PlayerBarsGroup = new ParamGroup(4, "Bars - you and your target");
        public static readonly ParamGroup SquadBarsGroup = new ParamGroup(5, "Bars - your squads");
        public static readonly ParamGroup AdvancedGroup = new ParamGroup(6, "Advanced");

        // ------------------------------------------------------------------ damage randomness

        public static readonly ParamDef DamageRandomEnabled = Bool("DamageRandomEnabled", true, DamageGroup,
            "Damage randomness",
            "Master switch. On: every hit that lands deals a random share of its normal damage (see the spread). Off: damage is the game's own.");

        public static readonly ParamDef DamageRandomPercent = Int("DamageRandomPercent", 50, 0, 100, DamageGroup,
            "Spread (± %)",
            "How far a hit's damage may swing up or down, in percent. 50 means a 50-damage hit lands for anything from 25 to 75, rolled fresh on every hit. 0 turns the randomness off.");

        public static readonly ParamDef DamageRandomMelee = Bool("DamageRandomMelee", true, DamageGroup,
            "Randomize melee hits",
            "Randomize hits by swords, axes, maces, spears and fists, and horse-charge bumps.");

        public static readonly ParamDef DamageRandomRanged = Bool("DamageRandomRanged", true, DamageGroup,
            "Randomize ranged hits",
            "Randomize hits by arrows, bolts, sling stones and thrown weapons.");

        public static readonly ParamDef DamageRandomOnMounts = Bool("DamageRandomOnMounts", true, DamageGroup,
            "Randomize hits on horses",
            "Also randomize hits that land on horses and other mounts.");

        public static readonly ParamDef DamageRandomOnShields = Bool("DamageRandomOnShields", false, DamageGroup,
            "Randomize shield damage",
            "Also randomize the damage a shield takes when it blocks a blow. Off: shields wear down at the game's usual pace.");

        // ------------------------------------------------------------------ Athletics

        public static readonly ParamDef AthleticsEnabled = Bool("AthleticsEnabled", true, AthleticsGroup,
            "Athletics",
            "On: every fighter has an Athletics bar - his stamina, named after the Athletics skill on the character screen (which will set its size) - that his attacks drain and rest refills; an empty bar means slow attacks. Off: no Athletics bar at all.");

        public static readonly ParamDef MaxAthletics = Int("MaxAthletics", 100, 10, 1000, AthleticsGroup,
            "Bar size (points)",
            "How many Athletics points a fresh fighter's bar holds. Changing it mid-battle keeps everyone's share: a fighter at 60% stays at 60%.");

        public static readonly ParamDef CostPerBlow = Float("CostPerBlow", 10, 0, 100, AthleticsGroup,
            "Cost per blow",
            "Athletics points one attack costs before the hero and leader discounts. With the defaults a common soldier gets 10 blows out of a full bar. 0 = attacks are free.");

        public static readonly ParamDef CostOnMiss = Bool("CostOnMiss", true, AthleticsGroup,
            "Misses cost too",
            "On: every attack costs Athletics, landed or not. Off: only attacks that hit something (a body, a shield, a parrying weapon) cost.");

        public static readonly ParamDef HeroCostMultiplier = Float("HeroCostMultiplier", 0.75, 0, 2, AthleticsGroup,
            "Hero cost multiplier",
            "Heroes (lords, companions and you) pay this share of the cost per blow. 0.75 = a quarter less than a common soldier.");

        public static readonly ParamDef PartyLeaderCostMultiplier = Float("PartyLeaderCostMultiplier", 0.75, 0, 2, AthleticsGroup,
            "Party leader multiplier",
            "The hero who leads the fighter's own party (you for your party, a lord for his) pays this share again, on top of the hero discount: 0.75 × 0.75 × 10 = about 5.6 per blow.");

        // ------------------------------------------------------------------ exhaustion

        public static readonly ParamDef ExhaustedAttackSpeedPercent = Int("ExhaustedAttackSpeedPercent", 20, 5, 100, ExhaustionGroup,
            "Exhausted attack speed (%)",
            "Attack speed of a fighter whose Athletics is empty, in percent of normal: swings, thrusts, bow draw, crossbow reload, throws. 100 = no slowdown.");

        public static readonly ParamDef ExhaustedRecoverPercent = Int("ExhaustedRecoverPercent", 0, 0, 90, ExhaustionGroup,
            "Recover above (%)",
            "Once exhausted, normal speed returns only when Athletics climbs above this percent of the pool. 0 = the moment it is above empty.");

        // ------------------------------------------------------------------ regeneration

        public static readonly ParamDef RegenDelayBlowTimes = Float("RegenDelayBlowTimes", 2, 0, 20, RegenGroup,
            "Rest before refill (blows)",
            "How long a fighter must go without attacking before his Athletics starts to refill, counted in blow lengths (see the next setting). 2 × 1.5 s = 3 seconds.");

        public static readonly ParamDef BlowTimeSeconds = Float("BlowTimeSeconds", 1.5, 0.1, 10, RegenGroup,
            "Length of one blow (s)",
            "How many seconds one blow counts as, for the rest delay above.");

        public static readonly ParamDef FullRegenSecondsStanding = Float("FullRegenSecondsStanding", 60, 1, 600, RegenGroup,
            "Refill time standing (s)",
            "Seconds to refill from empty to full while standing still.");

        public static readonly ParamDef FullRegenSecondsMoving = Float("FullRegenSecondsMoving", 120, 1, 1200, RegenGroup,
            "Refill time moving (s)",
            "Seconds to refill from empty to full while walking, running or riding.");

        public static readonly ParamDef MovingSpeedThreshold = Float("MovingSpeedThreshold", 0.5, 0, 5, RegenGroup,
            "Moving above (m/s)",
            "Speed in metres per second above which a fighter (or the horse he rides) counts as moving, for the refill time.");

        // ------------------------------------------------------------------ bars: you and your target

        public static readonly ParamDef ShowPlayerBar = Bool("ShowPlayerBar", true, PlayerBarsGroup,
            "Your Athletics bar",
            "Show your own Athletics next to your health bar.");

        public static readonly ParamDef ShowTargetBar = Bool("ShowTargetBar", true, PlayerBarsGroup,
            "Target's Athletics bar",
            "Show a small Athletics bar for the fighter you are looking or aiming at. Aiming at a horse shows its rider.");

        public static readonly ParamDef TargetBarMaxDistance = Float("TargetBarMaxDistance", 30, 1, 200, PlayerBarsGroup,
            "Target bar range (m)",
            "How far away, in metres, a fighter you look at still gets a bar.");

        public static readonly ParamDef TargetBarLingerSeconds = Float("TargetBarLingerSeconds", 2, 0, 10, PlayerBarsGroup,
            "Target bar linger (s)",
            "Seconds the target's bar stays after your aim leaves him, so it does not flicker.");

        // ------------------------------------------------------------------ bars: your squads

        public static readonly ParamDef ShowFormationBars = Bool("ShowFormationBars", true, SquadBarsGroup,
            "Squad bars",
            "Show the average Athletics of each of your formations in a bar floating above it.");

        public static readonly ParamDef FormationBarsAlways = Bool("FormationBarsAlways", true, SquadBarsGroup,
            "Squad bars always",
            "On: the squad bars are always shown. Off: only while the game shows its own formation markers (marker key held or orders menu open).");

        public static readonly ParamDef ShowFormationSpread = Bool("ShowFormationSpread", true, SquadBarsGroup,
            "Show the spread",
            "Draw a band on each squad bar showing how far the men's Athletics spreads around the average.");

        public static readonly ParamDef FormationSpreadStdDevs = Float("FormationSpreadStdDevs", 1.0, 0, 3, SquadBarsGroup,
            "Spread band width (std devs)",
            "Width of that band on each side of the average, in standard deviations. 1 = about two men in three fall inside it.");

        public static readonly ParamDef FormationBarHeight = Float("FormationBarHeight", 3.0, 0, 10, SquadBarsGroup,
            "Squad bar height (m)",
            "How high above the formation's centre its bar floats, in metres.");

        public static readonly ParamDef ShowInOrderMenu = Bool("ShowInOrderMenu", true, SquadBarsGroup,
            "Orders menu panel",
            "While the orders menu is open, list each formation's average Athletics and spread, for example \"Infantry 72 ± 8\".");

        // ------------------------------------------------------------------ advanced

        public static readonly ParamDef HudRefreshSeconds = Float("HudRefreshSeconds", 0.1, 0.02, 1, AdvancedGroup,
            "Bar refresh (s)",
            "How often your bar and the target's bar update, in seconds. Lower is smoother and costs a little more.");

        public static readonly ParamDef FormationStatsRefreshSeconds = Float("FormationStatsRefreshSeconds", 0.25, 0.05, 2, AdvancedGroup,
            "Squad stats refresh (s)",
            "How often the squad averages and spreads are recomputed, in seconds.");

        public static readonly ParamDef VerboseLogging = Bool("VerboseLogging", false, AdvancedGroup,
            "Verbose log",
            "Write every damage roll, blow and exhaustion to trax_combat.log (rate-limited, so a big battle cannot flood it). Off: only loading, settings, battle start and end, battle summaries and errors.");

        // ------------------------------------------------------------------ the list (keep LAST)

        /// <summary>Every setting, in file and MCM order. <c>All[i].Index == i</c>.</summary>
        public static readonly IReadOnlyList<ParamDef> All = new[]
        {
            DamageRandomEnabled, DamageRandomPercent, DamageRandomMelee, DamageRandomRanged,
            DamageRandomOnMounts, DamageRandomOnShields,
            AthleticsEnabled, MaxAthletics, CostPerBlow, CostOnMiss, HeroCostMultiplier,
            PartyLeaderCostMultiplier,
            ExhaustedAttackSpeedPercent, ExhaustedRecoverPercent,
            RegenDelayBlowTimes, BlowTimeSeconds, FullRegenSecondsStanding, FullRegenSecondsMoving,
            MovingSpeedThreshold,
            ShowPlayerBar, ShowTargetBar, TargetBarMaxDistance, TargetBarLingerSeconds,
            ShowFormationBars, FormationBarsAlways, ShowFormationSpread, FormationSpreadStdDevs,
            FormationBarHeight, ShowInOrderMenu,
            HudRefreshSeconds, FormationStatsRefreshSeconds, VerboseLogging,
        };

        /// <summary>The groups in order.</summary>
        public static readonly IReadOnlyList<ParamGroup> Groups = new[]
        {
            DamageGroup, AthleticsGroup, ExhaustionGroup, RegenGroup, PlayerBarsGroup, SquadBarsGroup, AdvancedGroup,
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

        private static ParamDef Bool(string key, bool def, ParamGroup group, string label, string description,
            ApplyTiming timing = ApplyTiming.Live)
            => new ParamDef(_next++, key, ParamType.Bool, def ? 1 : 0, 0, 1, group, label, description, timing);

        private static ParamDef Int(string key, int def, int min, int max, ParamGroup group, string label,
            string description, ApplyTiming timing = ApplyTiming.Live)
            => new ParamDef(_next++, key, ParamType.Int, def, min, max, group, label, description, timing);

        private static ParamDef Float(string key, double def, double min, double max, ParamGroup group, string label,
            string description, ApplyTiming timing = ApplyTiming.Live)
            => new ParamDef(_next++, key, ParamType.Float, def, min, max, group, label, description, timing);
    }
}

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
        public static readonly ParamGroup ExhaustionGroup = new ParamGroup(3, "Exhaustion");
        public static readonly ParamGroup RegenGroup = new ParamGroup(4, "Regeneration");
        public static readonly ParamGroup PlayerBarsGroup = new ParamGroup(5, "Bars - you and your target");
        public static readonly ParamGroup SquadBarsGroup = new ParamGroup(6, "Bars - your squads");
        public static readonly ParamGroup AdvancedGroup = new ParamGroup(7, "Advanced");

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
            "On: every fighter has an Athletics bar - his stamina, named after the Athletics skill on the character screen (which will set its size) - that his attacks drain and rest refills; an empty bar means slow attacks. Off: no Athletics bar at all.");

        public static readonly ParamDef MaxAthletics = Int("MaxAthletics", 10, 1000, AthleticsGroup,
            "Bar size (points)",
            "How many Athletics points a fresh fighter's bar holds. Changing it mid-battle keeps everyone's share: a fighter at 60% stays at 60%.");

        public static readonly ParamDef CostPerBlow = Float("CostPerBlow", 0, 100, AthleticsGroup,
            "Cost per blow",
            "Athletics points one attack costs before the hero and leader discounts. With the defaults a common soldier gets 10 blows out of a full bar. 0 = attacks are free.");

        public static readonly ParamDef CostOnMiss = Bool("CostOnMiss", AthleticsGroup,
            "Misses cost too",
            "On: every attack costs Athletics, landed or not. Off: only attacks that hit something (a body, a shield, a parrying weapon) cost.");

        public static readonly ParamDef HeroCostMultiplier = Float("HeroCostMultiplier", 0, 2, AthleticsGroup,
            "Hero cost multiplier",
            "Heroes (lords, companions and you) pay this share of the cost per blow. 0.75 = a quarter less than a common soldier.");

        public static readonly ParamDef PartyLeaderCostMultiplier = Float("PartyLeaderCostMultiplier", 0, 2, AthleticsGroup,
            "Party leader multiplier",
            "The hero who leads the fighter's own party (you for your party, a lord for his) pays this share again, on top of the hero discount: 0.75 × 0.75 × 10 = about 5.6 per blow.");

        // ------------------------------------------------------------------ exhaustion

        public static readonly ParamDef ExhaustedAttackSpeedPercent = Int("ExhaustedAttackSpeedPercent", 5, 100, ExhaustionGroup,
            "Exhausted attack speed (%)",
            "Attack speed of a fighter whose Athletics is empty, in percent of normal: swings, thrusts, bow draw, crossbow reload, throws. 100 = no slowdown.");

        public static readonly ParamDef ExhaustedRecoverPercent = Int("ExhaustedRecoverPercent", 0, 90, ExhaustionGroup,
            "Recover above (%)",
            "Once exhausted, normal speed returns only when Athletics climbs above this percent of the pool. 0 = the moment it is above empty.");

        // ------------------------------------------------------------------ regeneration

        public static readonly ParamDef RegenDelayBlowTimes = Float("RegenDelayBlowTimes", 0, 20, RegenGroup,
            "Rest before refill (blows)",
            "How long a fighter must go without attacking before his Athletics starts to refill, counted in blow lengths (see the next setting). 2 × 1.5 s = 3 seconds.");

        public static readonly ParamDef BlowTimeSeconds = Float("BlowTimeSeconds", 0.1, 10, RegenGroup,
            "Length of one blow (s)",
            "How many seconds one blow counts as, for the rest delay above.");

        public static readonly ParamDef FullRegenSecondsStanding = Float("FullRegenSecondsStanding", 1, 600, RegenGroup,
            "Refill time standing (s)",
            "Seconds to refill from empty to full while standing still.");

        public static readonly ParamDef FullRegenSecondsMoving = Float("FullRegenSecondsMoving", 1, 1200, RegenGroup,
            "Refill time moving (s)",
            "Seconds to refill from empty to full while walking, running or riding.");

        public static readonly ParamDef MovingSpeedThreshold = Float("MovingSpeedThreshold", 0, 5, RegenGroup,
            "Moving above (m/s)",
            "Speed in metres per second above which a fighter (or the horse he rides) counts as moving, for the refill time.");

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
            MasterGroup, DamageGroup, AthleticsGroup, ExhaustionGroup, RegenGroup, PlayerBarsGroup, SquadBarsGroup, AdvancedGroup,
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

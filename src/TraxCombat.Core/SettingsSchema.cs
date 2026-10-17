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
        public static readonly ParamGroup DamageGroup = new ParamGroup(1, "Damage");
        public static readonly ParamGroup AthleticsGroup = new ParamGroup(2, "Athletics");
        public static readonly ParamGroup TiredGroup = new ParamGroup(3, "Tired fighters");
        public static readonly ParamGroup StepBackGroup = new ParamGroup(4, "Tired fighters step back");
        public static readonly ParamGroup BattlePaceGroup = new ParamGroup(5, "Battle pace (AI)");
        public static readonly ParamGroup BraceGroup = new ParamGroup(6, "Brace by orders (AI)");
        public static readonly ParamGroup SprintGroup = new ParamGroup(7, "Sprint");
        public static readonly ParamGroup RefillGroup = new ParamGroup(8, "Refill");
        public static readonly ParamGroup PlayerBarGroup = new ParamGroup(9, "Your Athletics bar");
        public static readonly ParamGroup OrderStripGroup = new ParamGroup(10, "Orders menu strip");
        public static readonly ParamGroup AltMarkersGroup = new ParamGroup(11, "Formation markers (hold ALT)");
        public static readonly ParamGroup AdvancedGroup = new ParamGroup(12, "Advanced");

        // ------------------------------------------------------------------ master switch

        /// <summary>THE master switch (Anton, 2026-09-27). Every feature checks it FIRST, live:
        /// off = that feature steps aside and the game runs vanilla (CLAUDE.md hard requirement).</summary>
        public static readonly ParamDef ModEnabled = Bool("ModEnabled", MasterGroup,
            "Mod enabled",
            "Turn the whole mod off and every battle is plain vanilla: no damage rolls or reduction, no Athletics, no pauses between attacks, no step backs, no bars. The log still records each battle (its summary says mod ON or OFF), so the same battle can be fought both ways and compared. Switched back on, everyone starts with a full Athletics bar.");

        // ------------------------------------------------------------------ damage randomness

        public static readonly ParamDef DamageRandomEnabled = Bool("DamageRandomEnabled", DamageGroup,
            "Damage randomness",
            "On: every hit that lands deals a random share of its normal damage (see Spread). Off: no roll - every hit deals the game's own damage (less the damage reduction below); the rest of the mod still runs.");

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
            "Also randomize the damage a shield takes when it blocks a blow. Off: shields wear down at the game's usual pace (less the damage reduction below).");

        // Step 25 (Anton, 2026-10-10): every hit weaker, after the roll - slower fights.
        public static readonly ParamDef DamageReductionPercent = Int("DamageReductionPercent", 0, DamageReduction.MaxPercent, DamageGroup,
            "Damage reduction (%)",
            "Every hit deals this much less damage, after the random roll - on people, horses and shields alike, from any weapon, arrow, kick or horse charge, and whether the roll is on or off. 25: an average 100-damage hit lands for 75, so it takes about a third more hits to bring a man down. A hit never drops below 1. Falls and hits on doors and siege engines keep the game's damage. 0 = the game's damage.");

        // ------------------------------------------------------------------ Athletics

        public static readonly ParamDef AthleticsEnabled = Bool("AthleticsEnabled", AthleticsGroup,
            "Athletics",
            "On: every fighter has an Athletics bar - his stamina, as big as his Athletics skill - that his attacks and blocks drain and rest refills. At or above the peak line he fights at full strength; below it his damage upside, attack speed and run speed fall - down to long pauses between attacks and a slow run when it is empty - and tired AI fighters step back. Off: no Athletics at all - no costs, no slowing, no step backs, no bars.");

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

        // Step 18 (Anton, 2026-09-28): kicks and shield bashes cost too - their own price, the blow's multipliers.
        public static readonly ParamDef CostPerKickOrBash = Float("CostPerKickOrBash", 0, 20, AthleticsGroup,
            "Cost per kick or shield bash (points)",
            "Athletics points one kick or one shield bash costs a common soldier - yours and the AI's alike. Heroes pay it times the hero cost and party leaders times both, just like a blow: 3 costs a hero 2.25 and a party leader about 1.7. Paid once, when the kick or bash starts, landed or not. It never starts the pause between attacks. 0 = kicks and bashes are free.");

        // Step 22 (Anton, 2026-09-29): defending costs too - the DEFENDER pays for every melee blow he blocks, by how.
        public static readonly ParamDef CostPerShieldBlock = Float("CostPerShieldBlock", 0, 20, AthleticsGroup,
            "Cost per shield block (points)",
            "Athletics points a common soldier pays for each melee blow he blocks with his shield held on the right side - yours and the AI's alike, on foot or mounted. Heroes pay it times the hero cost and party leaders times both, like a blow. Paid once per blocked blow; it does not stop your refill unless the switch below is off. Blocking is never held or slowed. Arrows and bolts stopped by a shield are free. 0 = free.");

        public static readonly ParamDef CostPerWrongSideShieldBlock = Float("CostPerWrongSideShieldBlock", 0, 20, AthleticsGroup,
            "Cost per wrong-side shield block (points)",
            "Athletics points a common soldier pays for each melee blow his shield stops while held to the wrong side - the blow came from another direction than his guard, and the game counts it as a clumsy block. Heroes and party leaders pay less, like a blow. 0 = free.");

        public static readonly ParamDef CostPerWeaponParry = Float("CostPerWeaponParry", 0, 20, AthleticsGroup,
            "Cost per weapon parry (points)",
            "Athletics points a common soldier pays for each melee blow he blocks or parries with his weapon (no shield) - a chamber block too. Heroes and party leaders pay less, like a blow. 0 = free.");

        // Step 29 (Anton, 2026-10-12): a skilful defender recovers while he stays on the defence - a clean block or
        // parry still costs, but only a wrong-side block stops the refill.
        public static readonly ParamDef OnlyWrongSideBlocksStopRefill = Bool("OnlyWrongSideBlocksStopRefill", AthleticsGroup,
            "Only wrong-side blocks stop the refill",
            "On: a shield block on the right side and a weapon parry still cost their points, but the bar keeps refilling underneath - a good blocker can win his breath back while he stays on the defence. Only a block with the shield held to the wrong side (and your own attacks, kicks and bashes) restarts the wait before the refill. Off: every paid block restarts that wait, so a man under constant attack never refills.");

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
            "How often a fighter with an empty bar can attack, in percent of normal. His attacks play at full speed; after each one he must wait before he can start the next: the attack's own length times (100 / this - 1) - at 20 he waits four attack lengths, so he attacks once where a fresh man attacks five times. From the peak line down to empty it falls in a straight line. 100 = attacks never slow down. Blocking is never slowed.");

        // Step 13 - PAUSE ONLY (Anton's playtest call): the slow-down is a no-attack timer after each
        // attack, the animations play at full speed (AttackTimerMath, AI_NOTES "Step 13").
        public static readonly ParamDef AttackRatePlayerTimer = Bool("AttackRatePlayerTimer", TiredGroup,
            "Your attacks wait out the pause",
            "On: below the peak line, after each of your attacks your attack button does nothing until the pause is over - the Attack recovery bar above your Athletics bar fills back up meanwhile. Keep the button held and your next attack starts the moment it is full. Blocking, kicks, moving and switching weapons always work. Off: your own attacks are never held (the AI's still are). Off mid-battle: your pause ends at once.");

        public static readonly ParamDef AttackRatePaceHold = Bool("AttackRatePaceHold", TiredGroup,
            "Tired AI wait out the pause",
            "On: after each attack - melee or ranged, on foot or mounted - a tired AI fighter does not start another until his pause is over; his guard stays up. The \"Battle pace (AI)\" waits ride on it too. Off: no AI fighter is held - neither the tired pause nor the battle-pace waits. Off mid-battle: every waiting fighter may attack again at once.");

        // Step 16 (BATTLE_PACING lever #2): the AI's pause through its own input - held men keep blocking.
        public static readonly ParamDef AttackRatePaceByInput = Bool("AttackRatePaceByInput", TiredGroup,
            "Tired AI keep their guard up (new way)",
            "On: a tired AI fighter waiting out his pause keeps fighting in every way but one - only his wish to attack is taken out of his own controls, so he still blocks, parries and moves as he likes. Off: the old way - the game's own 'no attack' order, under which waiting men blocked almost nothing (2-13% in the playtest). An A/B switch: the battle summary's guard line compares the two. A pause already running finishes the way it began.");

        public static readonly ParamDef AiHoldRaiseGuard = Bool("AiHoldRaiseGuard", TiredGroup,
            "Held AI raise their guard",
            "On: when a tired AI fighter who is waiting out his pause or stepping back wants to attack, he raises his guard instead (a block - with a shield, the shield). Off: his attack is simply dropped and he blocks only when his own AI decides to. Only with the new ways (\"Tired AI keep their guard up\", \"Step back = walk backwards\").");

        public static readonly ParamDef AttackRateAiDecisions = Bool("AttackRateAiDecisions", TiredGroup,
            "Tired AI also decide to attack less",
            "On: a tired AI fighter also decides to attack (and to strike back after a parry) less often, looses arrows less readily and aims longer - by his attack speed - on top of the pause. Off: the pause alone slows him. With both on they stack and tired AI fighters attack slower than asked, so it is off unless you want that.");

        // Step 20b (Anton after his playtest of 2026-09-28): a straight line by f, like the run speed - it was
        // max(m, this) in steps 13-20. The range stops at 5 (the line at 0 would freeze a swing at empty).
        public static readonly ParamDef AttackAnimationMinPercent = Int("AttackAnimationMinPercent", 5, 100, TiredGroup,
            "Attack animation speed when empty (%)",
            "How fast the attack animations - wind-up and swing, thrust, bow draw, throw, crossbow reload - play for a fighter with an empty bar, in percent of normal. From the peak line down to empty they slow in a straight line (at 85: full speed at the peak line, 92.5% halfway, 85% empty), so a tired man's swings look a little heavier. The pause between attacks stays the same in seconds - a slower swing only adds its own extra moment. 100 = always full speed: the whole slow-down is the pause. With \"Attack speed when empty\" at 100 they never slow either. Run speed is not affected.");

        // Step 32 (Anton, 2026-10-12 - "chain swings stack"): the pause becomes a refill clock of stored swings (Core SwingStack,
        // AI_NOTES "Step 32"). Off = step 13's pause after every attack.
        public static readonly ParamDef SwingStackEnabled = Bool("SwingStackEnabled", TiredGroup,
            "Swing stack",
            "On: every fighter stores swings - one per 20 Athletics points he has now (see below), never fewer than 2. Each attack spends one and needs no wait while one is left; with none left he waits out the pause as before. One comes back per pause length while he is not attacking, so he attacks just as often on average - but he can save swings up and strike several in a row. Your Attack recovery bar shows the count. Off: the pause after every attack below the peak line (the old way).");

        public static readonly ParamDef SwingStackPointsPerSwing = Int("SwingStackPointsPerSwing", 5, 100, TiredGroup,
            "Athletics points per stored swing (points)",
            "The swing stack holds his CURRENT Athletics points divided by this, rounded (7.5 makes 8): a full 150 bar holds 8, half of it 4, a full 50 bar 3. Lower = more stored swings.");

        public static readonly ParamDef SwingStackMinimum = Int("SwingStackMinimum", 1, 10, TiredGroup,
            "Fewest stored swings",
            "The swing stack never holds fewer places than this, however low his Athletics - even empty he can save up this many swings.");

        public static readonly ParamDef KicksUseSwings = Bool("KicksUseSwings", TiredGroup,
            "Kicks and shield bashes use a stored swing",
            "On (with the swing stack): a kick or a shield bash spends a stored swing like an attack, and with none left it cannot start - your kick key does nothing until one is back. It still never starts a pause by itself. Off: kicks and bashes are free of the stack (they still cost their Athletics).");

        public static readonly ParamDef MinMoveSpeedMultiplier = Float("MinMoveSpeedMultiplier", 0.1, 1, TiredGroup,
            "Run speed when empty (x)",
            "Top speed on foot of a fighter with an empty bar, times his normal top speed. From the peak line down to empty it falls in a straight line, so fresh men overtake tired ones. 1.0 = tired men run as fast as fresh ones.");

        public static readonly ParamDef MountMinSpeedMultiplier = Float("MountMinSpeedMultiplier", 0.1, 1, TiredGroup,
            "Horse speed when the rider is empty (x)",
            "Top speed of a horse whose rider's bar is empty, times its normal top speed, on the same straight line. 1.0 = horses never slow down, however tired the rider.");

        public static readonly ParamDef DamageBonusFollowsAthletics = Bool("DamageBonusFollowsAthletics", TiredGroup,
            "Damage upside follows Athletics",
            "On: the lucky side of the damage roll shrinks as the attacker tires - at or above the peak line a hit can land up to +50% (with Spread at 50), halfway down to empty up to +25%, empty never above normal. The unlucky side never changes. Off: every attacker gets the full roll.");

        // Step 30 (Anton, 2026-10-12): the tired AI guesses his block direction - the game's own randomiser
        // (AiRandomizedDefendDirectionChance) raised by this share x (1 - f). AI_NOTES "Step 30".
        public static readonly ParamDef TiredBlockRandomPercent = Int("TiredBlockRandomPercent", 0, 100, TiredGroup,
            "Tired AI blocks in a random direction (%)",
            "How much a tired AI fighter's block direction is left to chance. At or above the peak line his own AI picks it. Below it, this share times how far down he is becomes a random pick instead: at 100, halfway down half his blocks go in a random direction, and an empty man guesses every block - right only one time in four, by luck. A wrong guess with a shield is a wrong-side block (it costs more and stops his refill); with a weapon the blow lands. It raises the game's own chance for clumsy fighters, never lowers it. On foot and mounted; never you. 0 = off: the game's own blocking.");

        // ------------------------------------------------------------------ tired fighters step back (step 5d)

        public static readonly ParamDef StepBackEnabled = Bool("StepBackEnabled", StepBackGroup,
            "Tired fighters step back",
            "On: after a melee swing, a tired AI fighter on foot may step back a little, facing his enemy with his guard up, then return to his place in the formation - so the tired fall back and the fresh take the blows. Never you, never riders, never after a shot or a throw; field battles only. Off mid-battle: everyone stepping back returns to his formation at once.");

        // Step 16: the step back as a backpedal through the AI's own input (BATTLE_PACING section B).
        public static readonly ParamDef StepBackBackpedal = Bool("StepBackBackpedal", StepBackGroup,
            "Step back = walk backwards (new way)",
            "On: a fighter stepping back walks backwards, like you holding S - he keeps facing his enemy with his guard up and stops when he has covered the step distance or the step time runs out (or the ground behind him ends). Off: the old way - the game walks him to a spot behind him, and most men turned their backs to do it (80% in the playtest). An A/B switch: the summary's facing and guard lines compare the two. A step back already running finishes the way it began.");

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

        // ------------------------------------------------------------------ battle pace (step 21)

        // Step 21 (Anton, 2026-09-28: "make the infantry more defensive, especially the guys with the shields ... swing 30% less" and
        // "make the archers a bit slower ... about 30% slower overall"): a share on top of the AI's pause after each attack, at any
        // tiredness (BattlePaceMath, AI_NOTES "Step 21"). The class is read at each attack from what the fighter really holds.
        public static readonly ParamDef ShieldInfantrySwingsLessPercent = Int("ShieldInfantrySwingsLessPercent", 0, BattlePaceMath.MaxSwingsLessPercent, BattlePaceGroup,
            "Shield infantry swing less (%)",
            "AI soldiers on foot fighting with a shield in their other hand swing this many percent less often than they otherwise would, fresh or tired: after each swing they wait a little longer, guard up, before the next. 30 = 7 swings where they made 10. Read at every swing from what he really holds - heroes and lords too; never you, never riders. Rides on \"Tired AI wait out the pause\" (off there = no AI pause at all). 0 = off. A change applies at each fighter's next swing.");

        public static readonly ParamDef FootMeleeSwingsLessPercent = Int("FootMeleeSwingsLessPercent", 0, BattlePaceMath.MaxSwingsLessPercent, BattlePaceGroup,
            "Other infantry swing less (%)",
            "The same for AI soldiers on foot fighting WITHOUT a shield - two-handed swords and axes, polearms, a one-hander alone: this many percent fewer swings, fresh or tired. 0 = off. A change applies at each fighter's next swing.");

        public static readonly ParamDef AiMeleeGapSeconds = Float("AiMeleeGapSeconds", 0, 5, BattlePaceGroup,
            "AI's own gap between swings (s)",
            "How long an AI soldier on foot at full strength waits on his own between the end of one swing and the start of the next - about 1 second in the playtests. The two settings above size their wait on his whole rhythm (his swing, his tired pause and this gap), so that he really swings that percent less. The battle summary's \"battle pace\" lines show his real gap after a wait: raise this if it reads longer, lower it if shorter.");

        public static readonly ParamDef ExtraPauseAfterBowShotSeconds = Float("ExtraPauseAfterBowShotSeconds", 0, 10, BattlePaceGroup,
            "Bowmen: extra wait after each shot (s)",
            "AI bowmen - on foot and horse archers alike - wait this many seconds more after each shot, on top of any tired pause. At 2.0 they fire about 30% fewer arrows a minute (a fresh AI bowman shot about once every 4.5 seconds in the playtests). Never you. 0 = off. A change applies at each archer's next shot.");

        public static readonly ParamDef ExtraPauseAfterCrossbowShotSeconds = Float("ExtraPauseAfterCrossbowShotSeconds", 0, 10, BattlePaceGroup,
            "Crossbowmen: extra wait after each shot (s)",
            "AI crossbowmen wait this many seconds more after each shot, on top of any tired pause. At 2.5 they fire about 30% fewer bolts a minute (a fresh AI crossbowman shoots about once every 6 seconds, the long reload included). Thrown weapons and slings have no extra wait. Never you. 0 = off. A change applies at each crossbowman's next shot.");

        // Step 41 (Anton, 2026-10-17: a fresh shield wall waiting for the enemy - "men step 2-3 m forward out of the wall to swing at
        // an enemy, then walk back to their slot ... keep their slot, the way RBM does it"): WallMath, AI_NOTES "Step 41", RESEARCH §O.
        // In this group: it is the same aim as the rest of it - a more defensive, more tactical AI infantry.
        public static readonly ParamDef ShieldWallHoldsSlots = Bool("ShieldWallHoldsSlots", BattlePaceGroup,
            "Shield walls hold their slots",
            "AI soldiers on foot in a shield wall, square or circle keep their place in the formation instead of stepping out of it at the enemy they fight - they still fight whatever reaches them, so the wall stays a wall. Under any order but a charge or a retreat; a man far behind his place may still run to rejoin. Both sides, never you, never riders, a siege's attackers or naval battles. Off: the game's own way - each man may walk a few steps out at his enemy and back.");

        // Step 42 (Anton, 2026-10-17, his playtest of step 41: held men "looked many times like they were about to reach to hit but
        // stopped, maybe as the enemy took a few steps back" - "give them just a bit of a step if the enemy moves a small step back, but
        // not as far"): the leash, LeashMath, AI_NOTES "Step 42".
        public static readonly ParamDef ShieldWallStepOutMetres = Float("ShieldWallStepOutMetres", 0, LeashMath.MaxMetres, BattlePaceGroup,
            "Shield walls: a step out to strike (m)",
            "With \"Shield walls hold their slots\" on: a held soldier may step up to this many metres out of his place at an enemy close in front of it (one who backed off just out of reach), strike, then he is held again and walks back to his place - he may step out again once he is back. 0 = no step at all: they stand fast and fight only what reaches them. Does nothing while \"hold their slots\" is off.");

        // ------------------------------------------------------------------ brace by orders (step 23)

        // Step 23 (Anton, 2026-09-29: "make soldiers not swing but only defend when they have reached a certain floor, that
        // depends on their current orders, until they have replenished the floor +20%" and "when defending they whip out their
        // shields if they have one"; the spread: "+- 5% additive to those 20% ... per soldier (once rolled on a battle, adds some
        // bravery-like randomness)"): BraceMath, AI_NOTES "Step 23". The bar = points ÷ his full pool (what his bar shows).
        public static readonly ParamDef BraceEnabled = Bool("BraceEnabled", BraceGroup,
            "Tired AI brace by their orders",
            "An AI soldier whose Athletics bar falls to his formation's order floor (the settings below) stops attacking in melee and only defends, guard up, until he refills to that floor + the recover margin. Bows, crossbows and throws go on. AI heroes and riders too; never you. Off: AI men swing at any Athletics. Switching it off lifts every brace at once.");

        public static readonly ParamDef BraceFloorChargePercent = Int("BraceFloorChargePercent", 0, 100, BraceGroup,
            "Brace floor: charging (%)",
            "Under a CHARGE order (charge, charge a target, the AI's own charge, attacking a gate) a soldier braces when his Athletics bar is at or below this % of his full bar. 0 = only when empty. The new floor applies at once when his formation's order changes.");

        public static readonly ParamDef BraceFloorAdvancePercent = Int("BraceFloorAdvancePercent", 0, 100, BraceGroup,
            "Brace floor: advancing (%)",
            "Under an ADVANCE order (yours, or the AI's own advance into the enemy) a soldier braces at or below this % of his full bar.");

        public static readonly ParamDef BraceFloorHoldPercent = Int("BraceFloorHoldPercent", 0, 100, BraceGroup,
            "Brace floor: holding and the rest (%)",
            "Under every other order - hold / halt, move to a position, retreat, fall back, follow, no order - a soldier braces at or below this % of his full bar. High = a holding line swings first and then braces early.");

        // Step 28 (Anton, 2026-10-12, a tournament log: the 1v1 fighters have no formation, so they braced on the hold floor
        // 60% - 62% of their time, the longest 163 s, the AI blocking ~95% of his swings): no formation has its own floor, and
        // every man's floor gets his own roll (AI_NOTES "Step 28").
        public static readonly ParamDef BraceFloorNoFormationPercent = Int("BraceFloorNoFormationPercent", 0, 100, BraceGroup,
            "Brace floor: no formation - tournaments, arenas, duels (%)",
            "A soldier in no formation - tournament and arena fighters, duels - braces at or below this % of his full bar. Low = a duel opponent fights on until he is nearly spent, instead of turtling behind his shield.");

        public static readonly ParamDef BraceFloorSpreadPercent = Int("BraceFloorSpreadPercent", 0, 30, BraceGroup,
            "Brace floor spread per soldier (± %)",
            "Each AI soldier's own floor is his order's floor plus or minus up to this many points, rolled once per soldier and battle (apart from the margin's roll below) - some brace sooner, some fight on longer. Never below 0 or above 100. 0 = everyone exactly on his order's floor. A change rescales everyone's roll at once.");

        public static readonly ParamDef BraceRecoverPercent = Int("BraceRecoverPercent", 0, 100, BraceGroup,
            "Brace until floor + (%)",
            "A bracing soldier attacks again once his bar is back to his floor + this many points (60 + 20 = 80%). A wound caps it: he stops bracing at the most his wounds let him refill to, so a wounded man is never stuck. Blocks cost Athletics and stop the refill, so a man pressed hard may brace a long time.");

        public static readonly ParamDef BraceRecoverSpreadPercent = Int("BraceRecoverSpreadPercent", 0, 50, BraceGroup,
            "Brace margin spread per soldier (± %)",
            "Each AI soldier's own margin is the setting above plus or minus up to this many points, rolled once per soldier and battle - some come back sooner, some later (bravery). 0 = everyone exactly the margin above. A change rescales everyone's roll at once; his target is never below his floor + 1.");

        public static readonly ParamDef BraceWieldShield = Bool("BraceWieldShield", BraceGroup,
            "Bracing soldiers take out their shield",
            "When a soldier starts bracing and carries a shield that is not in his hand, he takes it out (and a one-handed weapon first if his weapon needs both hands). A man with a bow or crossbow in hand is left alone. When the brace ends his own AI picks his weapons again.");

        public static readonly ParamDef BraceRaiseShield = Bool("BraceRaiseShield", BraceGroup,
            "Bracing soldiers keep the shield up",
            "While bracing with a shield in hand a soldier holds it up whenever his own AI has no guard up (his own blocks and parries stay his). Off: the guard is raised only when he wants to attack.");

        // ------------------------------------------------------------------ the sprint (step 33)

        // Step 33 (Anton, 2026-10-12: "a short burst of speed, meant to feel a little like a dodge, and for the AI to make charges
        // sharper"): Core SprintMath, AI_NOTES "Step 33". The store in seconds = current Athletics points ÷ SprintPointsPerSecond.
        public static readonly ParamDef SprintEnabled = Bool("SprintEnabled", SprintGroup,
            "Sprint",
            "Every fighter on foot has a sprint: a short burst of speed stored in seconds - his Athletics points now divided by the points per second below (150 points = 7.5 s; empty = none). Hold the sprint key to run faster; the AI sprints when its formation gets a new charge or advance order. While sprinting his Athletics does not refill. Never on a horse. Off: no sprint, speeds back to normal at once.");

        public static readonly ParamDef SprintKey = Key("SprintKey", SprintGroup,
            "Sprint key",
            "Hold this key to sprint (on foot, in a fight). Type its name: " + KeyNames.ListText + ". Left Control is free in the game's default keys (Left Shift is zoom, Z crouch, Caps Lock walk, Left Alt the formation markers).");

        public static readonly ParamDef SprintPointsPerSecond = Float("SprintPointsPerSecond", 5, 100, SprintGroup,
            "Athletics points per sprint second (points)",
            "The sprint store holds his CURRENT Athletics points divided by this, in seconds: 150 points = 7.5 s at 20, 50 points = 2.5 s. When his points drop (a blow, a wound) the stored seconds drop with them. Lower = longer sprints.");

        // Step 35 (Anton, 2026-10-12, after two tournaments: "when tired the store collapsed"): the store's floor.
        public static readonly ParamDef SprintMinSeconds = Float("SprintMinSeconds", 0, 5, SprintGroup,
            "Smallest sprint store (s)",
            "The sprint store is never smaller than this, however tired he is: an exhausted man (an empty Athletics bar) still holds 2 s of sprint - a dodge - it just refills slower. Above it the store is his points divided by the points per second. 0 = no floor (an empty bar cannot sprint).");

        public static readonly ParamDef SprintMinStartPercent = Int("SprintMinStartPercent", 0, 100, SprintGroup,
            "Least stored to start (%)",
            "A sprint starts only with at least this percent of the store filled - no spamming tiny bursts. 0 = any amount.");

        public static readonly ParamDef SprintMaxMultiplier = Float("SprintMaxMultiplier", 1, 3, SprintGroup,
            "Sprint speed at its start (x)",
            "The sprint's top speed, times his FRESH run speed (as if untired) - whatever his Athletics: 2.0 = twice a fresh man's run, for a tired man too. A sprint from a half-full store starts at this speed too.");

        public static readonly ParamDef SprintMinMultiplier = Float("SprintMinMultiplier", 1, 3, SprintGroup,
            "Sprint speed at its end (x)",
            "After the full-speed part the speed falls in a straight line to this times his CURRENT run speed (after tiredness) at the sprint's end: a fresh man ends at 1.1 x his run, an exhausted one at 1.1 x his slow run. Above the start speed = the start speed all the way.");

        public static readonly ParamDef SprintFullSpeedSharePercent = Int("SprintFullSpeedSharePercent", 0, 100, SprintGroup,
            "Full-speed part of a sprint (%)",
            "The first this-percent of a sprint's length run at the start speed, then it falls to the end speed. A sprint is as long as the seconds stored when it began: a full 7.5 s sprint runs 2.25 s at full speed at 30. 100 = full speed all the way.");

        // Step 35: the game's foot acceleration (TopSpeedReachDuration, ~2.3-2.8 s) is longer than a short sprint's full-speed part.
        public static readonly ParamDef SprintTopSpeedReachSeconds = Float("SprintTopSpeedReachSeconds", 0, 5, SprintGroup,
            "Sprint acceleration (s to top speed)",
            "While sprinting a man reaches his top speed in at most this many seconds (the game's own on foot is about 2.3-2.8 s - longer than a short sprint's full-speed part). Back to the game's value when the sprint ends. 0 = the game's own acceleration.");

        public static readonly ParamDef SprintRefillSeconds = Float("SprintRefillSeconds", 1, 120, SprintGroup,
            "Sprint refill time (s)",
            "Seconds to refill an empty sprint store to full while standing still or walking at full strength (at or above the peak line). It refills only while he is not sprinting.");

        public static readonly ParamDef SprintRefillMovingFactor = Float("SprintRefillMovingFactor", 0, 1, SprintGroup,
            "Sprint refill while running (x)",
            "The sprint store refills at this times its rate while he moves faster than a walk (0.5 = half: 14 s instead of 7). 1 = moving refills as fast as standing.");

        public static readonly ParamDef SprintRefillExhaustedFactor = Float("SprintRefillExhaustedFactor", 0, 1, SprintGroup,
            "Sprint refill when exhausted (x)",
            "Below the peak line the sprint store refills slower, in a straight line down to this times its rate at an empty bar (0.5 = half). It multiplies with the running factor: running and half tired at 0.5 / 0.5 = 0.375 of the rate.");

        public static readonly ParamDef SprintAiEnabled = Bool("SprintAiEnabled", SprintGroup,
            "AI soldiers sprint on new orders",
            "When an AI formation gets a NEW movement order - yours or its own general's - each of its soldiers on foot sprints once: everything he holds above that order's reserve below (a charge all of it, an advance down to half, a move only a little). He stops early once an enemy is within a few metres or the order changes again. Hold, retreat, fall back and follow never start one. Never you, never a hero you drive.");

        public static readonly ParamDef SprintAiChargeKeepPercent = Int("SprintAiChargeKeepPercent", 0, 100, SprintGroup,
            "AI sprint on a charge: keeps (% of the store)",
            "On a new CHARGE order (charge - F1 F3 -, charge a target, the AI general's own charge) each AI soldier on foot keeps this percent of his FULL sprint store for later orders and sprints everything above it. 0 = a charge sprints all of it; 100 = no sprint on a charge.");

        public static readonly ParamDef SprintAiAdvanceKeepPercent = Int("SprintAiAdvanceKeepPercent", 0, 100, SprintGroup,
            "AI sprint on an advance: keeps (% of the store)",
            "On a new ADVANCE order (advance / engage - F1 F4 -, or the AI general's own advance) each AI soldier on foot keeps this percent of his FULL sprint store for later orders and sprints everything above it. 50: from a full store he sprints half, from 60% a tenth, at or below half none.");

        public static readonly ParamDef SprintAiMoveKeepPercent = Int("SprintAiMoveKeepPercent", 0, 100, SprintGroup,
            "AI sprint on a move: keeps (% of the store)",
            "On a new MOVE order (move to a position - F1 F1 -, or an AI general's move that is neither its advance nor its charge) each AI soldier on foot keeps this percent of his FULL sprint store for later orders and sprints everything above it - so troops moved into position still have their sprint for the real charge. 80: from a full store he sprints a fifth, from 90% a tenth, at or below 80% none.");

        // ------------------------------------------------------------------ refill (regeneration)

        public static readonly ParamDef RegenDelayBlowTimes = Float("RegenDelayBlowTimes", 0, 20, RefillGroup,
            "Rest before refill (blows)",
            "How long a fighter must go without attacking before his Athletics starts to refill, counted in blow lengths (the next setting): 2 × 1.5 s = 3 seconds.");

        public static readonly ParamDef BlowTimeSeconds = Float("BlowTimeSeconds", 0.1, 10, RefillGroup,
            "Length of one blow (s)",
            "How many seconds one blow counts as, for the rest before refill.");

        public static readonly ParamDef FullRegenSecondsStanding = Float("FullRegenSecondsStanding", 1, 600, RefillGroup,
            "Refill time at rest (s)",
            "Seconds to refill an empty bar to full while standing still or walking. The bar refills faster while it is low and slower near full (the next setting); this stays the whole time from empty to full.");

        // Step 14 (Anton: "recover faster when it's low and slower as it is fuller ... maybe half linear"):
        // the rate is a straight line in the fill, r0 × (1 − (1 − k) × x), with r0 chosen so empty → full
        // still takes FullRegenSecondsStanding (AthleticsMath.RegenRateAtEmpty).
        public static readonly ParamDef RegenRateNearFullPercent = Int("RegenRateNearFullPercent", 10, 100, RefillGroup,
            "Refill speed near full (%)",
            "The bar refills fastest when it is empty and slows down as it fills, in a straight line: near full it refills at this percent of its speed near empty. The refill time at rest still takes it from empty to full. At 50 with 60 seconds: half the bar comes back in about 25 seconds, three quarters (the peak line) in about 41, the last quarter in about 19. 100 = the same speed all the way.");

        public static readonly ParamDef RegenMultiplierAtFullRun = Float("RegenMultiplierAtFullRun", 0, 1, RefillGroup,
            "Refill rate at a full run (x)",
            "How fast Athletics refills while running flat out (or riding at the horse's top speed), times the rate at rest. Between a walk and a full run it falls in a straight line. 1.0 = running refills as fast as resting; 0 = no refill at a full run.");

        public static readonly ParamDef WalkEffortFraction = Float("WalkEffortFraction", 0.05, 1, RefillGroup,
            "Walking pace (share of top speed)",
            "Up to this share of his current top speed a fighter (or the horse he rides) counts as walking and refills at the full rate. The game walks people at 1.8 m/s and their top speed is about 4 to 5 m/s, so 0.4 covers a walk. 1.0 = any pace refills at the full rate.");

        // Step 19 (Anton, 2026-09-28: "they will come fresh and we will be tired"): the hideout boss fight is a
        // fresh start for the player's side (HideoutBossFightMath, AI_NOTES "Step 19").
        public static readonly ParamDef HideoutBossFightRefill = Bool("HideoutBossFightRefill", RefillGroup,
            "Hideout boss fight: your side starts fresh",
            "In a hideout, the moment the fight with the boss begins - the duel or all against all - you and every man of yours still standing refill to a full Athletics bar (wounds still cap it) and any pause or step back running on you ends: the boss and his men come fresh, and so do you. In a duel your men stand aside, so only you refill. Off: you face the boss as tired as the first fight left you.");

        // ------------------------------------------------------------------ your Athletics bar (step 6)

        public static readonly ParamDef ShowPlayerBar = Bool("ShowPlayerBar", PlayerBarGroup,
            "Your Athletics bar",
            "Your Athletics bar, bottom right under your health bar: the Athletics you have left and your whole bar (\"132 / 180\"), a white mark at the peak line, the fill in the colour of your strength (green at full strength, then blue, yellow, orange, red) and the part your wounds hold shown dark.");

        // Step 12 (Anton's playtest: no bar in the training field - it runs in the game's walk-about
        // mode, not a battle mode). HudGate's outside-a-battle rule; the orders strip stays fights-only.
        public static readonly ParamDef ShowPlayerBarOutsideBattles = Bool("ShowPlayerBarOutsideBattles", PlayerBarGroup,
            "Your bar outside battles too",
            "Outside a battle - the training field, a town, a village - your bar shows while you hold a weapon or a shield, and stays while your Athletics refills; with empty hands and a full bar it goes. Never during a conversation, a barter or a cutscene. Off: your bar shows in fights only (battles, duels, tournaments, stealth).");

        // Step 13 (Anton's playtest: "above that bar add a bar 'attack recovery' that empties when I
        // attack and until it fills I can't attack; inside it add the secs delay added").
        public static readonly ParamDef ShowAttackRecoveryBar = Bool("ShowAttackRecoveryBar", PlayerBarGroup,
            "Attack recovery bar",
            "A slim bar just above your Athletics bar, shown with it: it empties when you attack below the peak line and fills back up over your pause - until it is full you cannot start another attack - with the seconds left inside it (\"1.3 s\"). At full strength there is no pause and it stays full. Hidden while your pause is switched off (Your attacks wait out the pause).");

        public static readonly ParamDef FlashBarOnEarlyAttack = Bool("FlashBarOnEarlyAttack", PlayerBarGroup,
            "Flash it on an early attack",
            "The Attack recovery bar flashes twice when you press attack while it is still filling (the press does nothing).");

        // Step 33: your sprint store, a slim bar above the Attack recovery bar.
        public static readonly ParamDef ShowSprintBar = Bool("ShowSprintBar", PlayerBarGroup,
            "Sprint bar",
            "A slim bar above the Attack recovery bar, shown with your Athletics bar: the seconds of sprint you have stored (\"Sprint 6.2 s\"), a mark at the least you need to start one, grey below it, blue when ready and bright while you sprint. Hidden while the sprint is off.");

        // The looked-at fighter's bar (ShowTargetBar, TargetBarMaxDistance, TargetBarLingerSeconds) is
        // LATER (step 7): its settings left the schema in step 10b - no switch that does nothing
        // (review R21). DESIGN's "Planned parameters" keeps the rows.

        // The bar colours (DESIGN §3 additions, step 6) - thresholds on f, the share of the peak line
        // left: green at or above the line, blue just below it, then these. The strip and the ALT
        // labels (step 20) use them too.
        public static readonly ParamDef BarYellowBelowPercent = Int("BarYellowBelowPercent", 0, 100, PlayerBarGroup,
            "Yellow at or below (% of the peak line)",
            "The colours of your bar, of the orders-menu strip and of the numbers under the formation markers: green at or above the peak line, blue just below it, and yellow once the Athletics left is at or below this percent of the peak line. With the line at 75% of the bar, 75 turns it yellow from about 56% of the bar down.");

        public static readonly ParamDef BarOrangeBelowPercent = Int("BarOrangeBelowPercent", 0, 100, PlayerBarGroup,
            "Orange at or below (% of the peak line)",
            "Orange once the Athletics left is at or below this percent of the peak line (50: about 38% of the bar with the line at 75%).");

        public static readonly ParamDef BarRedBelowPercent = Int("BarRedBelowPercent", 0, 100, PlayerBarGroup,
            "Red at or below (% of the peak line)",
            "Red once the Athletics left is at or below this percent of the peak line (25: about 19% of the bar with the line at 75%). An empty bar is always red, and your bar's word and number turn red too.");

        // ------------------------------------------------------------------ the orders-menu strip (step 9)

        // The squad bars were LATER (step 8, left the schema in step 10b - review R21); step 20 built them
        // in their "only while vanilla shows its formation markers" form: the ALT labels below
        // (ShowAltMarkerStats). FormationBarsAlways / FormationBarHeight stay in DESIGN's "Planned
        // parameters". The spread and health settings here serve the strip and the ALT labels alike.

        public static readonly ParamDef ShowInOrderMenu = Bool("ShowInOrderMenu", OrderStripGroup,
            "Orders menu strip",
            "While the orders menu is open, a slim strip under each of your formation cards: the men's average Athletics, in percent of their own bars, with its spread (\"72% ± 8\"), over a bar in the colour of their strength. You are not counted - you have your own bar.");

        public static readonly ParamDef ShowFormationHealth = Bool("ShowFormationHealth", OrderStripGroup,
            "Show average health",
            "The strip and the numbers under the formation markers (hold ALT) also show the men's average health left, for example \"HP 81%\".");

        public static readonly ParamDef ShowFormationSpread = Bool("ShowFormationSpread", OrderStripGroup,
            "Show the spread",
            "The strip and the numbers under the formation markers show how far the men's Athletics spreads around the average: the \"± 8\" after the number and a lighter band on the bar. Off: the average alone.");

        public static readonly ParamDef FormationSpreadStdDevs = Float("FormationSpreadStdDevs", 0, 3, OrderStripGroup,
            "Spread width (std devs)",
            "How wide the spread is on each side of the average, in standard deviations - the \"± 8\" is this width, in percent of the bar. 1 = about two men in three fall inside it.");

        // Step 24 (Anton, 2026-09-29: "some indication above the squad of ready men, that are not resting in their current order
        // status ... on the ALT and on the formations view"): ReadyMath, AI_NOTES "Step 24". One switch for both places.
        public static readonly ParamDef ShowReadyCount = Bool("ShowReadyCount", OrderStripGroup,
            "Show the ready count",
            "The strip and the numbers under the formation markers (hold ALT) also show how many men are READY - not bracing (Brace by orders) - out of the formation, for example \"ready 34/50\" (you are not counted). It turns yellow and red as fewer are ready. Hidden while bracing is off.");

        public static readonly ParamDef ReadyYellowBelowPercent = Int("ReadyYellowBelowPercent", 0, 100, OrderStripGroup,
            "Ready count yellow at or below (%)",
            "The ready count turns yellow once this percent of the formation or fewer are ready (75: a quarter of the men bracing).");

        public static readonly ParamDef ReadyRedBelowPercent = Int("ReadyRedBelowPercent", 0, 100, OrderStripGroup,
            "Ready count red at or below (%)",
            "The ready count turns red once this percent of the formation or fewer are ready (50: half the men bracing).");

        public static readonly ParamDef OrderStripUnderCards = Bool("OrderStripUnderCards", OrderStripGroup,
            "Strip under the cards",
            "On: the strip sits under each of the game's formation cards (read live, so it follows any resolution, UI scale or order-menu mod that keeps the cards). Off - or whenever the cards cannot be matched - a compact panel at the top of the screen lists the same numbers instead.");

        // ------------------------------------------------------------------ the formation markers (step 20)

        // Anton (2026-09-28): "I see the Athletics and health numbers above the troops when I hold ALT".
        // Shown exactly while the game shows its formation markers (ALT held, or the orders menu open).
        public static readonly ParamDef ShowAltMarkerStats = Bool("ShowAltMarkerStats", AltMarkersGroup,
            "Numbers under the formation markers",
            "While the game shows its formation markers above the troops - you hold ALT, or the orders menu is open - each marker also gets the men's average Athletics with its spread (\"72% ± 8\", in the colour of their strength) and their average health (\"HP 81%\"), just under the troop count and distance, over a slim bar. Your formations and your allies' always; the enemy's with the next switch.");

        public static readonly ParamDef AltMarkersShowEnemy = Bool("AltMarkersShowEnemy", AltMarkersGroup,
            "Enemy formations too",
            "The enemy's formation markers get the numbers too - see which of their lines is tired before you charge it. Off: yours and your allies' only.");

        // ------------------------------------------------------------------ advanced

        public static readonly ParamDef HudRefreshSeconds = Float("HudRefreshSeconds", 0.02, 1, AdvancedGroup,
            "Bar refresh (s)",
            "How often your Athletics bar, the orders-menu strip and the numbers under the formation markers update, in seconds. Lower is smoother and costs a little more. The markers' numbers follow the markers every frame whatever this says.");

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
            "Distance from the bottom of the screen to your bar's row (the word Athletics, the number and the bar), in the game's UI pixels. 30 leaves room for the Attack recovery bar above it, both just under the vanilla health and horse bars.");

        // The Attack recovery bar (step 13) - above your Athletics bar, its right end lined up with it
        // (PlayerBarOffsetRight); UI pixels of the 1080p layout, the game's UI scale applies.
        public static readonly ParamDef RecoveryBarWidth = Int("RecoveryBarWidth", 40, 800, AdvancedGroup,
            "Recovery bar: length (px)",
            "Length of the Attack recovery bar, in the game's UI pixels. 205 matches your Athletics bar under it.");

        public static readonly ParamDef RecoveryBarHeight = Int("RecoveryBarHeight", 2, 40, AdvancedGroup,
            "Recovery bar: thickness (px)",
            "Thickness of the Attack recovery bar, in the game's UI pixels. The seconds inside it need about 12.");

        public static readonly ParamDef RecoveryBarOffsetAbove = Int("RecoveryBarOffsetAbove", -400, 600, AdvancedGroup,
            "Recovery bar: above your bar (px)",
            "How far above your Athletics bar's row the Attack recovery bar's row sits (bottom to bottom), in the game's UI pixels. 24 = right on top of it. It moves with your bar.");

        public static readonly ParamDef SprintBarOffsetAbove = Int("SprintBarOffsetAbove", -400, 600, AdvancedGroup,
            "Sprint bar: above your bar (px)",
            "How far above your Athletics bar's row the sprint bar's row sits (bottom to bottom), in the game's UI pixels - as long and thick as the Attack recovery bar. 46 = right on top of the recovery bar. It moves with your bar.");

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

        public static readonly ParamDef OrderStripReadyOffset = Int("OrderStripReadyOffset", -40, 80, AdvancedGroup,
            "Orders strip: ready count offset (px)",
            "Distance from the bottom of a formation card to the top of its ready count (\"ready 34/50\"), in the game's UI pixels. 24 puts it right under the strip's bar, filling the gap to the next card; a cell that would leave the screen is lifted to its edge.");

        public static readonly ParamDef OrderStripSideMargin = Int("OrderStripSideMargin", 0, 40, AdvancedGroup,
            "Orders strip: side margin (px)",
            "How far the strip's numbers and bar keep in from a card's left and right edges, in the game's UI pixels.");

        public static readonly ParamDef OrderPanelOffsetTop = Int("OrderPanelOffsetTop", 0, 1000, AdvancedGroup,
            "Orders panel: from the top (px)",
            "Where the fallback panel sits (used when the cards cannot be matched, or with Strip under the cards off): distance from the top of the screen, in the game's UI pixels. It is centred left to right.");

        public static readonly ParamDef OrderPanelWidth = Int("OrderPanelWidth", 120, 900, AdvancedGroup,
            "Orders panel: width (px)",
            "Width of the fallback panel, in the game's UI pixels.");

        // The numbers under the formation markers (step 20) - UI pixels of the 1080p layout, measured from
        // each vanilla marker (read live); the game's UI scale applies.
        public static readonly ParamDef AltMarkerTextSize = Int("AltMarkerTextSize", 8, 30, AdvancedGroup,
            "Marker numbers: text size (px)",
            "Font size of the numbers under the formation markers, in the game's UI pixels.");

        public static readonly ParamDef AltMarkerOffset = Int("AltMarkerOffset", -200, 200, AdvancedGroup,
            "Marker numbers: gap under the marker (px)",
            "Distance from the bottom of a formation marker (its distance number) to the top of our numbers, in the game's UI pixels. Negative moves them up over the marker.");

        public static readonly ParamDef AltMarkerBarWidth = Int("AltMarkerBarWidth", 20, 300, AdvancedGroup,
            "Marker numbers: bar length (px)",
            "Length of the slim Athletics bar under the marker's numbers, in the game's UI pixels. 60 = as wide as the marker's distance row.");

        public static readonly ParamDef AltMarkerBarHeight = Int("AltMarkerBarHeight", 0, 20, AdvancedGroup,
            "Marker numbers: bar thickness (px)",
            "Thickness of that bar, in the game's UI pixels. 0 = no bar, the numbers alone.");

        public static readonly ParamDef FormationStatsRefreshSeconds = Float("FormationStatsRefreshSeconds", 0.05, 2, AdvancedGroup,
            "Formation averages refresh (s)",
            "How often the formations' averages (Athletics, spread, health) for the orders-menu strip and the formation markers are worked out - every formation of every side - in seconds of battle time.");

        public static readonly ParamDef VerboseLogging = Bool("VerboseLogging", AdvancedGroup,
            "Verbose log",
            "Also write every damage roll, blow, step back and AI pause to trax_combat.log, rate-limited so a big battle cannot flood it. These lines carry a ~ before their tag and are the only ones a trim cuts. Off: loading, settings, battle start and end, first-time events, summaries and errors only.");

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
            DamageRandomOnMounts, DamageRandomOnShields, DamageReductionPercent,
            AthleticsEnabled, AthleticsPoolFloor, AthleticsPoolPerSkill, AthleticsPeakPercent, HealthCapsAthletics,
            CostPerBlow, CostPerKickOrBash, CostPerShieldBlock, CostPerWrongSideShieldBlock, CostPerWeaponParry, OnlyWrongSideBlocksStopRefill, CostOnMiss, HeroCostMultiplier, PartyLeaderCostMultiplier,
            ExhaustedAttackSpeedPercent, AttackRatePlayerTimer, AttackRatePaceHold, AttackRatePaceByInput, AiHoldRaiseGuard, AttackRateAiDecisions, AttackAnimationMinPercent,
            SwingStackEnabled, SwingStackPointsPerSwing, SwingStackMinimum, KicksUseSwings,
            MinMoveSpeedMultiplier, MountMinSpeedMultiplier, DamageBonusFollowsAthletics, TiredBlockRandomPercent,
            StepBackEnabled, StepBackBackpedal, StepBackMaxChancePercent, StepBackDistance, StepBackSeconds, StepBackEnemyRange,
            StepBackHoldAttacks, StepBackMaxAtOnce,
            ShieldInfantrySwingsLessPercent, FootMeleeSwingsLessPercent, AiMeleeGapSeconds, ExtraPauseAfterBowShotSeconds, ExtraPauseAfterCrossbowShotSeconds,
            ShieldWallHoldsSlots, ShieldWallStepOutMetres,
            BraceEnabled, BraceFloorChargePercent, BraceFloorAdvancePercent, BraceFloorHoldPercent, BraceFloorNoFormationPercent, BraceFloorSpreadPercent,
            BraceRecoverPercent, BraceRecoverSpreadPercent,
            BraceWieldShield, BraceRaiseShield,
            SprintEnabled, SprintKey, SprintPointsPerSecond, SprintMinSeconds, SprintMinStartPercent, SprintMaxMultiplier, SprintMinMultiplier, SprintFullSpeedSharePercent,
            SprintTopSpeedReachSeconds,
            SprintRefillSeconds, SprintRefillMovingFactor, SprintRefillExhaustedFactor, SprintAiEnabled, SprintAiChargeKeepPercent,
            SprintAiAdvanceKeepPercent, SprintAiMoveKeepPercent,
            RegenDelayBlowTimes, BlowTimeSeconds, FullRegenSecondsStanding, RegenRateNearFullPercent, RegenMultiplierAtFullRun,
            WalkEffortFraction, HideoutBossFightRefill,
            ShowPlayerBar, ShowPlayerBarOutsideBattles, ShowAttackRecoveryBar, FlashBarOnEarlyAttack, ShowSprintBar,
            BarYellowBelowPercent, BarOrangeBelowPercent, BarRedBelowPercent,
            ShowInOrderMenu, ShowFormationHealth, ShowFormationSpread, FormationSpreadStdDevs, ShowReadyCount, ReadyYellowBelowPercent, ReadyRedBelowPercent, OrderStripUnderCards,
            ShowAltMarkerStats, AltMarkersShowEnemy,
            HudRefreshSeconds, PlayerBarWidth, PlayerBarHeight, PlayerBarOffsetRight, PlayerBarOffsetBottom,
            RecoveryBarWidth, RecoveryBarHeight, RecoveryBarOffsetAbove, SprintBarOffsetAbove,
            OrderStripTextSize, OrderStripTextOffset, OrderStripBarOffset, OrderStripBarHeight, OrderStripReadyOffset, OrderStripSideMargin,
            OrderPanelOffsetTop, OrderPanelWidth,
            AltMarkerTextSize, AltMarkerOffset, AltMarkerBarWidth, AltMarkerBarHeight,
            FormationStatsRefreshSeconds, VerboseLogging, LogMaxMegabytes,
        };

        /// <summary>The groups in order.</summary>
        public static readonly IReadOnlyList<ParamGroup> Groups = new[]
        {
            MasterGroup, DamageGroup, AthleticsGroup, TiredGroup, StepBackGroup, BattlePaceGroup, BraceGroup, SprintGroup, RefillGroup, PlayerBarGroup, OrderStripGroup, AltMarkersGroup, AdvancedGroup,
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

        /// <summary>Step 33: a key by name - its index in <see cref="KeyNames.All"/> (0 .. count − 1).</summary>
        private static ParamDef Key(string key, ParamGroup group, string label, string description,
            ApplyTiming timing = ApplyTiming.Live)
            => new ParamDef(_next++, key, ParamType.Key, 0, KeyNames.All.Length - 1, group, label, description, timing);

        private static ParamDef Float(string key, double min, double max, ParamGroup group, string label,
            string description, ApplyTiming timing = ApplyTiming.Live)
            => new ParamDef(_next++, key, ParamType.Float, min, max, group, label, description, timing);
    }
}

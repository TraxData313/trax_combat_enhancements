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

        // Step 59: TWO MCM pages. The PLAYER page ("Trax Combat Enhancements", MCM's main page and the top of config.json): the
        // master switch and six short groups a player reads at a glance - 20 settings. The ADVANCED page ("Trax Combat
        // Enhancements - Advanced"): every other setting, grouped by system as before step 59, the logging and test tools last.
        // A group's Order runs across both pages (the file's order); each page shows its own groups in that order.
        public static readonly ParamGroup MasterGroup = new ParamGroup(0, "Master switch", SettingsPage.Player);
        public static readonly ParamGroup PlayerDamageGroup = new ParamGroup(1, "How hard hits land", SettingsPage.Player);
        public static readonly ParamGroup PlayerTiredGroup = new ParamGroup(2, "Tiredness (Athletics)", SettingsPage.Player);
        public static readonly ParamGroup PlayerAiGroup = new ParamGroup(3, "How the AI fights", SettingsPage.Player);
        public static readonly ParamGroup PlayerImpactGroup = new ParamGroup(4, "Staggers and knockdowns", SettingsPage.Player);
        public static readonly ParamGroup PlayerSprintGroup = new ParamGroup(5, "Sprinting", SettingsPage.Player);
        public static readonly ParamGroup PlayerScreenGroup = new ParamGroup(6, "On screen", SettingsPage.Player);
        public static readonly ParamGroup DamageGroup = new ParamGroup(7, "Damage");
        public static readonly ParamGroup AthleticsGroup = new ParamGroup(8, "Athletics");
        public static readonly ParamGroup TiredGroup = new ParamGroup(9, "Tired fighters");
        public static readonly ParamGroup StepBackGroup = new ParamGroup(10, "Tired fighters step back");
        public static readonly ParamGroup BattlePaceGroup = new ParamGroup(11, "Battle pace (AI)");
        public static readonly ParamGroup BraceGroup = new ParamGroup(12, "Brace by orders (AI)");
        public static readonly ParamGroup SprintGroup = new ParamGroup(13, "Sprint");
        public static readonly ParamGroup FeverGroup = new ParamGroup(14, "Aggression and fever (AI)"); // step 43
        public static readonly ParamGroup CourageGroup = new ParamGroup(15, "Courage and cohesion (AI)"); // step 49 - after the fever (the AI's behaviour groups together)
        public static readonly ParamGroup CavalryFlockGroup = new ParamGroup(16, "Cavalry flocking (AI)"); // step 55 - after courage (the AI's behaviour groups together)
        public static readonly ParamGroup ImpactGroup = new ParamGroup(17, "Impact: staggers, shield drops, knockdowns"); // step 48 (IMPACT I2) - after the combat behaviour groups
        public static readonly ParamGroup RefillGroup = new ParamGroup(18, "Refill");
        public static readonly ParamGroup PlayerBarGroup = new ParamGroup(19, "Your Athletics bar");
        public static readonly ParamGroup OrderStripGroup = new ParamGroup(20, "Orders menu strip");
        public static readonly ParamGroup AltMarkersGroup = new ParamGroup(21, "Hold ALT: formation markers, Athletics and fever bars"); // step 44 renamed it (was "Formation markers (hold ALT)"), step 46 again (was "... and fever marks")
        public static readonly ParamGroup OrderClickGroup = new ParamGroup(22, "Orders: no accidental charges"); // step 72 - before "Advanced"
        public static readonly ParamGroup LineDisciplineGroup = new ParamGroup(23, "Line discipline by tier"); // step 73 - before "Advanced"
        public static readonly ParamGroup AdvancedGroup = new ParamGroup(24, "Advanced"); // the logging and test tools (the spike) - last

        // ------------------------------------------------------------------ master switch

        /// <summary>THE master switch (Anton, 2026-09-27). Every feature checks it FIRST, live:
        /// off = that feature steps aside and the game runs vanilla (CLAUDE.md hard requirement).</summary>
        public static readonly ParamDef ModEnabled = Bool("ModEnabled", MasterGroup,
            "Mod enabled",
            "Turn the whole mod off and every battle is plain vanilla: no damage rolls or reduction, no Athletics, no pauses between attacks, no step backs, no bars. The log still records each battle (its summary says mod ON or OFF), so the same battle can be fought both ways and compared. Switched back on, everyone starts with a full Athletics bar.");

        // ------------------------------------------------------------------ the player page (step 59)
        // Anton, 2026-10-18: "reorganise the MCM menu ... leave a few options for me and the players". The settings a player
        // understands and reaches for - the master switch, how hard hits land, how fast men tire, how the AI fights, the impact system
        // on / off, the sprint and its key, what the screen shows - in a few plainly named groups, on MCM's main page and at the top of
        // config.json. Every other setting is on the Advanced page, grouped by system as before. A NEW setting goes to Advanced unless a
        // player needs it to set up his game (CLAUDE.md). Keys never move between pages without a reason: a key's page is not stored
        // anywhere, so moving one is free (no config migration).

        public static readonly ParamDef DamageRandomPercent = Int("DamageRandomPercent", 0, 100, PlayerDamageGroup,
            "Damage roll (± %)",
            "Every hit that lands deals a random share of its normal damage - this far up or down, rolled fresh on every hit. 50: a 50-damage hit lands for anything from 25 to 75. 0 = no roll. Which hits roll: Advanced page, Damage.");

        // Step 25 (Anton, 2026-10-10): every hit weaker, after the roll - slower fights.
        public static readonly ParamDef DamageReductionPercent = Int("DamageReductionPercent", 0, DamageReduction.MaxPercent, PlayerDamageGroup,
            "Damage reduction (%)",
            "Every hit deals this much less damage, after the random roll - on people, horses and shields alike, from any weapon, arrow, kick or horse charge, and whether the roll is on or off. 25: an average 100-damage hit lands for 75, so it takes about a third more hits to bring a man down. A hit never drops below 1. Falls and hits on doors and siege engines keep the game's damage. 0 = the game's damage.");

        public static readonly ParamDef AthleticsEnabled = Bool("AthleticsEnabled", PlayerTiredGroup,
            "Athletics (stamina)",
            "On: every fighter has an Athletics bar - his stamina, as big as his Athletics skill - that his attacks and blocks drain and rest refills. At or above the peak line (the white mark) he fights at full strength; below it the top of his damage roll, his attack speed and his run speed fall - down to long pauses between attacks and a slow run when it is empty - and tired AI fighters step back. Off: no Athletics at all - no costs, no slowing, no step backs, no bars (and no sprint, aggression by tiredness or staggers).");

        public static readonly ParamDef CostPerBlow = Float("CostPerBlow", 0, 100, PlayerTiredGroup,
            "Cost per swing (points)",
            "How fast men tire: the Athletics points one attack costs a common soldier, whatever the size of his bar - so a bigger Athletics skill means more blows. At 10 a recruit (a 50-point bar) is empty after 5 blows, a legionary (130) after 13. Higher = everyone tires sooner. 0 = attacks are free. Kicks, bashes and blocks have their own costs: Advanced page, Athletics.");

        public static readonly ParamDef HeroCostMultiplier = Float("HeroCostMultiplier", 0, 2, PlayerTiredGroup,
            "Heroes' cost (x)",
            "Heroes (lords, companions and you) pay every Athletics cost - blows, kicks, bashes, blocks - times this, so they last longer than common soldiers. 0.75 = a quarter less. 1 = the same as anyone.");

        public static readonly ParamDef AiAggressionFreshPercent = Int("AiAggressionFreshPercent", 0, FeverMath.MaxPercent, PlayerAiGroup,
            "AI aggression when fresh (%)",
            "How eagerly an AI soldier on foot with a FULL Athletics bar decides to start an attack (and to strike back after a parry), in percent of the game's own AI. It slides in a straight line to the next setting as his bar empties - the more tired he is, the less he swings. 60: a charging man's 14% chance becomes 8.6%. Tournaments and duels too. Never you, never riders (their swings come with each pass), never archers' shooting. 100 with the next at 100 = the game's own AI.");

        public static readonly ParamDef AiAggressionExhaustedPercent = Int("AiAggressionExhaustedPercent", 0, FeverMath.MaxPercent, PlayerAiGroup,
            "AI aggression when exhausted (%)",
            "The same at an EMPTY bar: 20 = a fifth of the game's eagerness (a charging man's 14% becomes 2.9%). A wounded man's bar is capped by his wounds, so he is less eager too. On top of the pause after each attack - a tired man has both brakes.");

        public static readonly ParamDef FeverEnabled = Bool("FeverEnabled", PlayerAiGroup,
            "Fever on a charge or advance order",
            "When an AI formation gets a NEW charge or advance order - yours or its own general's - each of its soldiers on foot gets a FEVER: for a while he attacks with a surge of eagerness, then calms down again. The fever's clock starts only when he first comes near an enemy. A hold, move or retreat order ends it. Never you, never riders; tournaments and duels have no orders, so no fever. Off: every man's eagerness follows his bar alone. How strong and how long: Advanced page, Aggression and fever.");

        // Step 41 (Anton, 2026-10-17: a fresh shield wall waiting for the enemy - "men step 2-3 m forward out of the wall to swing at
        // an enemy, then walk back to their slot ... keep their slot, the way RBM does it"): WallMath, AI_NOTES "Step 41", RESEARCH §O.
        public static readonly ParamDef ShieldWallHoldsSlots = Bool("ShieldWallHoldsSlots", PlayerAiGroup,
            "Shield walls hold their slots",
            "AI soldiers on foot in a shield wall, square or circle keep their place in the formation instead of stepping out of it at the enemy they fight - they still fight whatever reaches them, so the wall stays a wall. Under any order but a charge or a retreat; a man far behind his place may still run to rejoin. Both sides, never you, never riders, a siege's attackers or naval battles. Off: the game's own way - each man may walk a few steps out at his enemy and back.");

        // Step 42 (Anton, 2026-10-17, his playtest of step 41: held men "looked many times like they were about to reach to hit but
        // stopped, maybe as the enemy took a few steps back" - "give them just a bit of a step if the enemy moves a small step back, but
        // not as far"): the leash, LeashMath, AI_NOTES "Step 42".
        public static readonly ParamDef ShieldWallStepOutMetres = Float("ShieldWallStepOutMetres", 0, LeashMath.MaxMetres, PlayerAiGroup,
            "Shield walls: a step out to strike (m)",
            "With \"Shield walls hold their slots\" on: a held soldier may step up to this many metres out of his place at an enemy close in front of it (one who backed off just out of reach), strike, then he is held again and walks back to his place - he may step out again once he is back. 0 = no step at all: they stand fast and fight only what reaches them. Does nothing while \"hold their slots\" is off.");

        // Step 75 (Toni 2026-10-22: "the men in the shield wall still step forward even when on a standing order, they take a few steps to strike at an
        // enemy, then fall back" - a switch to test on and off): StandFirmMath, AI_NOTES "Step 75", DESIGN decision 69.
        public static readonly ParamDef ShieldWallStandFirm = Bool("ShieldWallStandFirm", PlayerAiGroup,
            "Shield walls stand firm on a hold order",
            "With \"Shield walls hold their slots\" on: a shield wall, square or circle you (or the enemy's general) ordered to STOP - or moved to a place it has reached and stands on - never leaves its ground. Each soldier keeps his place whatever happens: no step out to strike, no sprint; if a kick, a shove or a blow carries him a little forward he backs straight into his place, facing the enemy. He still blocks and strikes - and kicks or bashes - whatever comes within his reach, so an enemy who pushes at the wall tires himself on it. Not while the wall advances, charges, follows or falls back; a wall whose courage breaks still breaks. Both sides, never you. Off: the step out above and the game's own small steps.");

        // Step 76 (Toni 2026-10-22: "my shield wall-ers have spears but they keep using their swords, even though the spear has a longer reach"):
        // RBM's weapon preference + the close radius from the spear's length, the back rank, a man facing a polearm - SpearMath, AI_NOTES "Step 76",
        // DESIGN decision 70.
        public static readonly ParamDef SpearPreferenceEnabled = Bool("SpearPreferenceEnabled", PlayerAiGroup,
            "Spearmen keep their spears",
            "AI soldiers who carry a spear (or another polearm) AND a sword, axe or mace fight with the spear while no enemy is close, and draw the sidearm only once an enemy has stood within the spear's reach for a moment (how close depends on how long the spear is); a few seconds after the last close enemy the spear comes back. A man in the back ranks of a shield wall, square or halted line keeps his spear whatever is close - he thrusts over the man in front - and a man facing only spears keeps his. Both sides, never you. Off: the game's own choice (most men draw their sword at once). The numbers: Advanced page, Battle pace (AI).");

        // Step 77 (Toni's battles 2026-10-22: 108 recruits' rear ranks walked round his shield wall's ends and behind it, "dancing", then through
        // the middle): PressMath, AI_NOTES "Step 77", DESIGN decision 71.
        public static readonly ParamDef PressDontPass = Bool("PressDontPass", PlayerAiGroup,
            "Press, don't pass",
            "AI soldiers on foot fighting an enemy LINE - a shield wall, a line or a block whose men hold their places - press against its front instead of walking past it: no stepping round its ends or through it to come at it from behind. They go round an end only when their own unit is clearly wider than the enemy line and they move with it as a mass, and through it only where a real gap has opened (men fallen); a man who got behind it anyway walks back round its nearest end. The back ranks stay behind their own front rank, and frightened men bunch deeper instead of spreading wider than the line they face. Both sides, never you, never riders; they still fight whatever is in reach. Off: the game's own - the log still measures, so flipping it mid-battle compares the two. The numbers: Advanced page, Courage and cohesion (AI).");

        public static readonly ParamDef CourageEnabled = Bool("CourageEnabled", PlayerAiGroup,
            "Courage and cohesion",
            "Every AI soldier on foot in a formation weighs his mates against the enemies near him - by how tough each one is (armour, level, wounds). Short of courage he is TIMID: far from his mates he walks back to a bigger group of his side, facing the enemy (sprinting when it is far); among them he holds and strikes only what comes within reach - no lone man circling your shield wall, no exhausted man standing among four enemies. A big enough crowd stays brave and wraps around a line as a mass. Never you, never riders, never men held in a shield wall. Works without the Athletics system (then tiredness does not count). Off: the game's own bravery - the log still measures, so flipping it mid-battle compares the two.");

        public static readonly ParamDef CavalryFlockEnabled = Bool("CavalryFlockEnabled", PlayerAiGroup,
            "Cavalry flocking",
            "AI riders on a charge stop going in one by one. A fresh, eager rider charges again and again as in the game; the more tired he gets, the more he looks for riders of his own kind in his formation - horse archers with horse archers, skirmishers with skirmishers, lancers with lancers - and the bigger the group he wants before he attacks again: 4-5 when half tired, most of his kind when exhausted. The flock never sits: it keeps riding, drifting toward the unit's centre while its riders are tired and bending toward the enemy as its most rested riders lead, and when it is big enough it charges together, as a wave. Both sides, never you; only riders whose formation is on a CHARGE (hold, move, follow and advance keep them in their slots). Works without courage. Off: the game's own riders - the log still measures, so flipping it mid-battle compares the two.");

        // Step 73 (Toni 2026-10-20: 120 tier-1 recruits came at his 60 tier-5 men on a front ~2 deep and wrapped round him; he wants a switch
        // "so I can feel that I can't stretch that line longer"): the player's own formations under the line discipline - DESIGN decision 67.
        public static readonly ParamDef LineDisciplineForPlayer = Bool("LineDisciplineForPlayer", PlayerAiGroup,
            "Line discipline: your units too",
            "Green troops cannot hold a long, thin line: a foot formation stretches no thinner than its men's training allows - recruits at least 5 deep, trained men 3, veterans and elites 1 (the average over its men). The AI's formations always keep to it. On: yours too - when you drag a line wider than your men can hold, the line stops growing at their limit (you see it in the drag) and the order keeps to it; as men fall the line shrinks back to stay deep enough. Cavalry, squares and circles are left alone. Off: your own lines are as wide as you drag them. The ranks per tier and the system's own switch: Advanced page, Line discipline by tier.");

        public static readonly ParamDef ImpactEnabled = Bool("ImpactEnabled", PlayerImpactGroup,
            "Staggers, shield drops, knockdowns",
            "Kicks, shield bashes, blocks and clean hits can stagger a man, make him drop his shield or knock him down - more likely the more tired he is and the more he is outmatched (both men's CURRENT Athletics, the weapon and its swing, the guard, both men's skill). Two fresh, even men: never. Needs the Athletics system on. Off: the game's own reactions only. Each kind on its own and every chance: Advanced page, Impact.");

        public static readonly ParamDef ImpactOnPlayerPercent = Int("ImpactOnPlayerPercent", 0, 100, PlayerImpactGroup,
            "Against you (%)",
            "Every chance of a stagger, a dropped shield or a knockdown against YOU is this percent of the AI's. 100 = the same rules as everyone; 0 = you are never staggered, disarmed of your shield or knocked down by it.");

        // Step 33 (Anton, 2026-10-12: "a short burst of speed, meant to feel a little like a dodge, and for the AI to make charges
        // sharper"): Core SprintMath, AI_NOTES "Step 33".
        public static readonly ParamDef SprintEnabled = Bool("SprintEnabled", PlayerSprintGroup,
            "Sprint",
            "Every fighter on foot has a sprint: a short burst of speed stored in seconds - the more Athletics he has left, the longer (150 points = 7.5 s; empty = none). Hold the sprint key to run faster; the AI sprints when its formation gets a new charge or advance order. While sprinting his Athletics does not refill. Never on a horse. Off: no sprint, speeds back to normal at once. How fast and how long: Advanced page, Sprint.");

        public static readonly ParamDef SprintKey = Key("SprintKey", PlayerSprintGroup,
            "Sprint key",
            "Hold this key to sprint (on foot, in a fight). Type its name: " + KeyNames.ListText + ". Left Control is free in the game's default keys (Left Shift is zoom, Z crouch, Caps Lock walk, Left Alt the formation markers).");

        public static readonly ParamDef ShowPlayerBar = Bool("ShowPlayerBar", PlayerScreenGroup,
            "Your Athletics bar",
            "Your Athletics bar, bottom right under your health bar: the Athletics you have left and your whole bar (\"132 / 180\"), a white mark at the peak line, the fill in the colour of your strength (green at full strength, then blue, yellow, orange, red) and the part your wounds hold shown dark. The attack recovery and sprint bars above it show with it.");

        public static readonly ParamDef AltMarksEveryone = Bool("AltMarksEveryone", PlayerScreenGroup,
            "Hold ALT: bars on every man",
            "While you hold ALT (or the orders menu is open), small Athletics and fever bars float above the soldiers. Off (the default): only on the few men you look at - the ones nearest the centre of the screen and close to the camera; the formations' averages are under their markers. On: every man on the screen gets his bars (at most a cap, the nearest first). How many and how far: Advanced page, Hold ALT.");

        public static readonly ParamDef ImpactShowMessages = Bool("ImpactShowMessages", PlayerScreenGroup,
            "On-screen lines: knocked down, shield lost",
            "A short line in the game's message feed when YOU are knocked down, knocked off your horse, lose your shield or reel from a riposte - and when you knock someone down, off his horse, break his guard or knock his shield away. At most one line every few seconds, several things at once merged into one. Never in shadow mode (nothing really happens there).");

        // ------------------------------------------------------------------ damage randomness (the Advanced page from here on)

        public static readonly ParamDef DamageRandomEnabled = Bool("DamageRandomEnabled", DamageGroup,
            "Damage randomness",
            "On: every hit that lands deals a random share of its normal damage (the Damage roll on the main page). Off: no roll - every hit deals the game's own damage (less the damage reduction); the rest of the mod still runs.");

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

        public static readonly ParamDef SprintFullCritical = Bool("SprintFullCritical", DamageGroup,
            "A charge rolls the full damage bonus",
            "Step 62: a man who CHARGES - sprinting into the blow, or his first blow just after a sprint - rolls the whole damage bonus however tired he is (the bonus otherwise shrinks with his Athletics). Off: his tiredness counts as for any blow.");

        // ------------------------------------------------------------------ Athletics (its switch, CostPerBlow and HeroCostMultiplier on the player page - step 59)

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

        // Step 18 (Anton, 2026-09-28): kicks and shield bashes cost too - their own price, the blow's multipliers.
        public static readonly ParamDef CostPerKickOrBash = Float("CostPerKickOrBash", 0, 20, AthleticsGroup,
            "Cost per kick or shield bash (points)",
            "Athletics points one kick or one shield bash costs a common soldier - yours and the AI's alike. Heroes pay it times the hero cost and party leaders times both, just like a blow: 6 costs a hero 4.5 and a party leader about 3.4. Paid once, when the kick or bash starts, landed or not. It never starts the pause between attacks. 0 = kicks and bashes are free.");

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

        // Step 64 (Anton's looter battles 2026-10-19: ~85% of an AI shieldman's blocks were "wrong side" even fresh - the native AI picks a
        // random guard direction most of the time and a shield wall's poses are fixed): a shield block from the FRONT counts as a correct one.
        public static readonly ParamDef ShieldFrontArcIsCorrect = Bool("ShieldFrontArcIsCorrect", AthleticsGroup,
            "A shield facing the blow blocks it right",
            "On: a melee blow stopped by a shield whose bearer FACES the attacker (within the angle below) counts as a right-side block for this mod - its cost, the refill, the staggers and the attacker's reel or fall off the shield - whatever side the game says the shield was held to. A raised shield facing the blow covers it. A TIRED AI man still misjudges a share of his blocks - the share \"Tired AI blocks in a random direction\" gives him - so a worn-out wall can still be broken. From the side or behind the game's own verdict stands. Weapon parries keep the game's direction (skill still matters there). You and the AI alike. Off: the game's verdict always.");

        public static readonly ParamDef ShieldFrontArcDegrees = Int("ShieldFrontArcDegrees", 0, 180, AthleticsGroup,
            "Shield front arc (degrees each side)",
            "How far off a shieldman's facing the attacker may stand for a shield block to count as a right-side block (60: within 60 degrees left or right of where he faces - the front third of a circle). 0 = only dead ahead; 180 = from anywhere.");

        public static readonly ParamDef CostOnMiss = Bool("CostOnMiss", AthleticsGroup,
            "Misses cost too",
            "On: every attack costs Athletics, landed or not. Off: only attacks that hit something (a body, a shield, a parrying weapon) cost.");

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

        // Steps 41 / 42's ShieldWallHoldsSlots and ShieldWallStepOutMetres were here; on the player page since step 59.

        // Step 63 (Anton's battle 2026-10-19 11:02: wall men in contact more than 1 m ahead of their slots 11.1%, step 42's 0.6-2.5%):
        // WallPullMath, AI_NOTES "Step 63".
        public static readonly ParamDef ShieldWallPullBackSeconds = Float("ShieldWallPullBackSeconds", 0, WallPullMath.MaxSeconds, BattlePaceGroup,
            "Shield walls: walk a man back after (s)",
            "With \"Shield walls hold their slots\" on (player page, How the AI fights): a soldier of a held wall who stands beyond his step out ahead of his place - thrown forward by a blow, pushed by the press - is held again at once, and if he has not started back on his own after this many seconds (and is not on the ground or reeling) he backs off to his place, facing the enemy. 0 = the game's own pull only.");

        // Step 68 (Toni's 100 v 100 of 2026-10-19: a shield wall ordered forward (F1 then F4 = advance) had 15.4% of its men in contact more than
        // 1 m ahead of their slots - the game's advance sends every man to a slot AT the enemy's line; "they need to hold the wall and push the whole
        // wall, not to break up"): WallPaceMath, AI_NOTES "Step 68", DESIGN decision 61.
        public static readonly ParamDef ShieldWallAdvanceAsOne = Bool("ShieldWallAdvanceAsOne", BattlePaceGroup,
            "Shield walls: advance as one line",
            "With \"Shield walls hold their slots\" on (player page, How the AI fights): a shield wall, square or circle ordered to advance (or moved, falling back, following) near the enemy walks forward as ONE line - each soldier only a short step ahead of his place in the line as it stands, instead of to the game's place at the enemy's line - so the whole wall pushes together and nobody runs ahead of it. A charge still lets them go. Off: the game's own advance (men in front walk into the enemy while their mates fight).");

        public static readonly ParamDef ShieldWallAdvanceStepMetres = Float("ShieldWallAdvanceStepMetres", WallPaceMath.MinStepMetres, WallPaceMath.MaxStepMetres, BattlePaceGroup,
            "Shield walls: advance step (m)",
            "How far ahead of his place in the line a soldier of an advancing wall is sent each moment (the setting above). Smaller = a slower, tighter push; larger = a faster push that lets the line stretch a little.");

        public static readonly ParamDef ShieldWallAdvanceNearMetres = Float("ShieldWallAdvanceNearMetres", 0, WallPaceMath.MaxNearMetres, BattlePaceGroup,
            "Shield walls: advance as one within (m)",
            "The advance-as-one line (above) begins when the wall's front is this close to the front of the nearest enemy formation; farther away the game's own march. 0 = only in contact.");

        public static readonly ParamDef ShieldWallKickReturnMetres = Float("ShieldWallKickReturnMetres", 0, WallPullMath.MaxKickReturnMetres, BattlePaceGroup,
            "Shield walls: back to the line after a kick (m)",
            "With \"Shield walls hold their slots\" on: a soldier of a held wall who kicked or shield-bashed (or stepped in at a staggered enemy) and stands more than this ahead of his place walks straight back to it, facing the enemy, as soon as he can move - no wait. 0 = off (only the walk back above, past his step out).");

        // Step 75 (Toni 2026-10-22): the stand-firm wall's numbers and its two engine levers - StandFirmMath, AI_NOTES "Step 75", DESIGN decision 69.
        public static readonly ParamDef ShieldWallStandFirmSlackMetres = Float("ShieldWallStandFirmSlackMetres", StandFirmMath.MinSlackMetres, StandFirmMath.MaxSlackMetres, BattlePaceGroup,
            "Stand firm: walked back beyond (m)",
            "With \"Shield walls stand firm on a hold order\" on (player page, How the AI fights): a soldier of a standing wall who is more than this ahead of his place - carried by his own kick or bash, the press, a blow - backs straight into it (to within half of it), facing the enemy, as soon as he can move. Also the tolerance the game is told for the wall. Smaller = a stiffer wall.");

        public static readonly ParamDef ShieldWallStandFirmArrivedMetres = Float("ShieldWallStandFirmArrivedMetres", StandFirmMath.MinArrivedMetres, StandFirmMath.MaxArrivedMetres, BattlePaceGroup,
            "Stand firm: a moved wall is at its place within (m)",
            "A wall MOVED to a place stands firm once it stands within this many metres of the place it was ordered to (and has stopped moving - the next setting); a Stop order stands firm at once. It stays so until it is ordered elsewhere or pushed more than twice this far from its place.");

        public static readonly ParamDef ShieldWallStandFirmStillMetresPerSecond = Float("ShieldWallStandFirmStillMetresPerSecond", 0, StandFirmMath.MaxStillMetresPerSecond, BattlePaceGroup,
            "Stand firm: a moved wall has stopped below (m/s)",
            "A moved wall counts as standing at its place only once its soldiers' average speed is at most this (metres a second) - so a wall still walking in is not frozen early.");

        public static readonly ParamDef ShieldWallStandFirmPinFrame = Bool("ShieldWallStandFirmPinFrame", BattlePaceGroup,
            "Stand firm: the game walks each man to his place",
            "On: the place the game walks each soldier of a standing wall to is his own place in the wall (on a Stop the game's is wherever he happens to stand, so a man who drifted out stays out). Off: the game's own.");

        public static readonly ParamDef ShieldWallStandFirmTightTolerance = Bool("ShieldWallStandFirmTightTolerance", BattlePaceGroup,
            "Stand firm: a tight tolerance for the game",
            "On: the game is told the standing wall's men may be no more than \"walked back beyond\" (above) away from their places, instead of the formation's own looser spread. Off: the formation's own.");

        // Step 76 (Toni 2026-10-22): the spear preference's numbers (RBM's, as parameters) and its three additions; thrusts that pass a friend - SpearMath,
        // ThrustPassMath, AI_NOTES "Step 76", DESIGN decision 70.
        public static readonly ParamDef SpearPolearmFavor = Float("SpearPolearmFavor", SpearMath.MinFavor, SpearMath.MaxFavor, BattlePaceGroup,
            "Spears: how strongly the spear is favoured",
            "With \"Spearmen keep their spears\" on (player page, How the AI fights): how strongly an AI soldier with a spear and a sidearm prefers the spear while no enemy is close - the game's own weighting is 1. 35 = a clear preference (RBM's value).");

        public static readonly ParamDef SpearSidearmFavor = Float("SpearSidearmFavor", SpearMath.MinFavor, SpearMath.MaxFavor, BattlePaceGroup,
            "Spears: how strongly the sidearm is favoured up close",
            "Once an enemy has been close (the settings below), how strongly he prefers his sword, axe or mace instead - the game's own weighting is 1. 55 = RBM's value.");

        public static readonly ParamDef SpearSidearmAfterSeconds = Float("SpearSidearmAfterSeconds", 0, SpearMath.MaxDelaySeconds, BattlePaceGroup,
            "Spears: sidearm after an enemy is close (s)",
            "An enemy must stay within his close distance this many seconds before he draws the sidearm - a man passing by (or a rider charging through) does not make him switch.");

        public static readonly ParamDef SpearPolearmBackAfterSeconds = Float("SpearPolearmBackAfterSeconds", 0, SpearMath.MaxDelaySeconds, BattlePaceGroup,
            "Spears: the spear again after (s)",
            "This many seconds after the last close enemy he takes the spear again - so he does not switch back and forth while an enemy drifts in and out.");

        public static readonly ParamDef SpearPolearmBackMountedSeconds = Float("SpearPolearmBackMountedSeconds", 0, SpearMath.MaxDelaySeconds, BattlePaceGroup,
            "Spears: the lance again after, mounted (s)",
            "The same for a rider: he rides out of the melee quickly and needs his lance for the next charge.");

        public static readonly ParamDef SpearRidingFastMetresPerSecond = Float("SpearRidingFastMetresPerSecond", 0, SpearMath.MaxRidingMetresPerSecond, BattlePaceGroup,
            "Spears: a rider faster than this rides through (m/s)",
            "A rider moving faster than this is riding through, not fighting where he stands: nothing counts as close, so lancers do not swap to the sword and back as they pass through a formation.");

        public static readonly ParamDef SpearCloseFromLength = Bool("SpearCloseFromLength", BattlePaceGroup,
            "Spears: close distance from the spear's length",
            "On: how close an enemy must come before the sidearm comes out depends on the length of his longest spear (the next settings) - a long pike is clumsy sooner than a short spear. Off: the same distance for every spear (\"close distance when not known\" below).");

        public static readonly ParamDef SpearCloseBaseMetres = Float("SpearCloseBaseMetres", 0, SpearMath.MaxCloseBaseMetres, BattlePaceGroup,
            "Spears: close distance, the base (m)",
            "The close distance = this + a share of the spear's length (the next setting), kept between the two limits below. 1.0 m + 60% of a 2 m spear = 2.2 m.");

        public static readonly ParamDef SpearCloseLengthPercent = Int("SpearCloseLengthPercent", 0, (int)SpearMath.MaxCloseLengthPercent, BattlePaceGroup,
            "Spears: close distance, share of the length (%)",
            "How much of the spear's length counts toward the close distance (above).");

        public static readonly ParamDef SpearCloseMinMetres = Float("SpearCloseMinMetres", SpearMath.MinCloseMetres, SpearMath.MaxCloseMetres, BattlePaceGroup,
            "Spears: close distance, at least (m)",
            "The close distance is never shorter than this, however short the spear.");

        public static readonly ParamDef SpearCloseMaxMetres = Float("SpearCloseMaxMetres", SpearMath.MinCloseMetres, SpearMath.MaxCloseMetres, BattlePaceGroup,
            "Spears: close distance, at most (m)",
            "The close distance is never longer than this, however long the spear.");

        public static readonly ParamDef SpearCloseFallbackMetres = Float("SpearCloseFallbackMetres", SpearMath.MinCloseMetres, SpearMath.MaxCloseMetres, BattlePaceGroup,
            "Spears: close distance when not known (m)",
            "The close distance when the spear's length is not known, or with \"close distance from the spear's length\" off. 2.5 m = RBM's.");

        public static readonly ParamDef SpearBackRankKeeps = Bool("SpearBackRankKeeps", BattlePaceGroup,
            "Spears: the back ranks keep their spears",
            "On: a soldier standing behind a comrade in a shield wall, square or circle keeps his spear whatever is close - he thrusts over the man in front (\"Thrusts pass a friend\" below lets the thrust through). Off: he draws his sidearm like anyone.");

        public static readonly ParamDef SpearBackRankInStandingLine = Bool("SpearBackRankInStandingLine", BattlePaceGroup,
            "Spears: ... in a halted line too",
            "On: the same for a soldier behind a comrade in a plain line ordered to stop. Off: only shield walls, squares and circles.");

        public static readonly ParamDef SpearKeepVsPolearm = Bool("SpearKeepVsPolearm", BattlePaceGroup,
            "Spears: keep the spear against spears",
            "On: an enemy close who holds a spear (or another polearm) himself does not make him draw the sidearm - a spear against a spear. Off: every close enemy counts.");

        public static readonly ParamDef SpearLookSeconds = Float("SpearLookSeconds", SpearMath.MinLookSeconds, SpearMath.MaxLookSeconds, BattlePaceGroup,
            "Spears: look every (s)",
            "How often each such soldier looks around him for close enemies (each look a little earlier or later, so a big army never looks all at once).");

        public static readonly ParamDef SpearWieldOverride = Bool("SpearWieldOverride", BattlePaceGroup,
            "Spears: force the spear while nothing is close (test)",
            "A FALLBACK to test if the preference alone does not hold: while no enemy is close, the AI's own order to draw the sidearm is turned into drawing the spear. Off by default.");

        public static readonly ParamDef ThrustsPassFriends = Bool("ThrustsPassFriends", BattlePaceGroup,
            "Thrusts pass a friend",
            "On: a THRUST that touches a man of the soldier's own side carries on instead of stopping on him, so a second-rank spearman can reach the enemy past the man in front of him. Swings, kicks and bashes still stop on a friend. Both sides, you too. Off: the game's own - the thrust stops on the friend and the thruster is briefly stunned.");

        public static readonly ParamDef ThrustsPassPolearmsOnly = Bool("ThrustsPassPolearmsOnly", BattlePaceGroup,
            "Thrusts pass a friend: spears only",
            "On: only a polearm's thrust passes a friend (a sword's thrust stops on him). Off: every thrust.");

        public static readonly ParamDef FriendlyHitStunSeconds = Float("FriendlyHitStunSeconds", 0, ThrustPassMath.MaxStunSeconds, BattlePaceGroup,
            "Thrusts pass a friend: stun after hitting a friend (s)",
            "While \"Thrusts pass a friend\" is on: how long a soldier is stunned after his blow touches a friend (the game's own is 0.4 s; it applies to every friendly touch, swings too). The game's own comes back at the battle's end and when the switch goes off.");

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
        // Step 59: SprintEnabled and SprintKey are on the player page.
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
            "When an AI formation gets a NEW movement order - yours or its own general's - each of its soldiers on foot sprints once: everything he holds above that order's reserve below (a charge all of it, an advance down to half, a move only a little). An advance or move sprint stops once an enemy is within a few metres, a charge's runs into contact (the settings below); any ends when the order changes again. Hold, retreat, fall back and follow never start one. Never you, never a hero you drive.");

        public static readonly ParamDef SprintAiChargeKeepPercent = Int("SprintAiChargeKeepPercent", 0, 100, SprintGroup,
            "AI sprint on a charge: keeps (% of the store)",
            "On a new CHARGE order (charge - F1 F3 -, charge a target, the AI general's own charge) each AI soldier on foot keeps this percent of his FULL sprint store for later orders and sprints everything above it. 0 = a charge sprints all of it; 100 = no sprint on a charge.");

        public static readonly ParamDef SprintAiAdvanceKeepPercent = Int("SprintAiAdvanceKeepPercent", 0, 100, SprintGroup,
            "AI sprint on an advance: keeps (% of the store)",
            "On a new ADVANCE order (advance / engage - F1 F4 -, or the AI general's own advance) each AI soldier on foot keeps this percent of his FULL sprint store for later orders and sprints everything above it. 50: from a full store he sprints half, from 60% a tenth, at or below half none.");

        public static readonly ParamDef SprintAiMoveKeepPercent = Int("SprintAiMoveKeepPercent", 0, 100, SprintGroup,
            "AI sprint on a move: keeps (% of the store)",
            "On a new MOVE order (move to a position - F1 F1 -, or an AI general's move that is neither its advance nor its charge) each AI soldier on foot keeps this percent of his FULL sprint store for later orders and sprints everything above it - so troops moved into position still have their sprint for the real charge. 80: from a full store he sprints a fifth, from 90% a tenth, at or below 80% none.");

        public static readonly ParamDef SprintOnRout = Bool("SprintOnRout", SprintGroup,
            "AI sprint when a man routs",
            "When an AI soldier on foot breaks and ROUTS (the game's own rout - his morale gone), he spends his WHOLE sprint store once, at the rout's start, for the first get-away (no order reserve, it does not end near an enemy). The rout itself stays the game's - only the speed is added. Needs the AI sprint on. Off: routing men run at their normal tired speed.");

        // Step 64 (Anton's looter battles 2026-10-19: looters sprinted early, slowed before the wall, nobody bounced): the AI's charge sprints
        // carry INTO contact.
        public static readonly ParamDef SprintChargeIntoContact = Bool("SprintChargeIntoContact", SprintGroup,
            "AI charge sprints run into contact",
            "On: an AI soldier's sprint on a CHARGE order, and a feverish man's sprint at a near enemy, do not stop a few metres short - they run on until his first attack (that blow lands as a charge) or until he stops moving. Off: they end once an enemy is within 3 m (advance and move sprints always do).");

        public static readonly ParamDef SprintChargeWaitBelowSeconds = Float("SprintChargeWaitBelowSeconds", 0, 10, SprintGroup,
            "AI charge sprint: a small store waits (s)",
            "On a new CHARGE order an AI soldier who would sprint less than this many seconds (a weak man's small store) does not spend it at once from far away: he waits until an enemy is within the distance below, then sprints. A bigger store sprints at the order, as before. 0 = never wait.");

        public static readonly ParamDef SprintChargeWaitMetres = Float("SprintChargeWaitMetres", 1, 40, SprintGroup,
            "AI charge sprint: a waiting man goes at (m)",
            "A charging AI soldier whose small store waits (the setting above) starts his sprint once an enemy is within this many metres - near enough that it lasts to the enemy's line.");

        // ------------------------------------------------------------------ aggression and fever (step 43)

        // Step 43 (Anton, 2026-10-17: "people are defensive by nature - they don't want to throw their life away ... when the general
        // turns to them and says NOW!, everybody surges ... the more experienced they are the longer they follow that order, and the
        // more tired they are the shorter they fight hard"; then: "a man at work doesn't hold a pace until he drops; the more tired he
        // is, the less he swings"): FeverMath, AI_NOTES "Step 43". The game's own two attack-decision chances x a share that follows
        // his bar; the fever lifts it to an absolute peak, fading back as the store burns. Step 59: the two aggression percents and
        // FeverEnabled are on the player page.
        public static readonly ParamDef FeverPointsPerSecond = Float("FeverPointsPerSecond", 1, 50, FeverGroup,
            "Athletics points per fever second (points)",
            "A man's fever lasts his CURRENT Athletics points divided by this, in seconds: 100 points = 10 s at 10, a tired man at 40 points only 4 s. Veterans have bigger Athletics pools, so they surge longer; tired men surge shorter. Lower = longer fevers. Read when the order comes.");

        public static readonly ParamDef FeverAggressionPercent = Int("FeverAggressionPercent", 0, FeverMath.MaxPercent, FeverGroup,
            "Fever aggression at its peak (%)",
            "In his fever a man decides to attack this eagerly, in percent of the game's own AI, whatever his bar - but never less than his tired eagerness. It fades in a straight line back to his tired eagerness as his fever burns down. At or below the full-bar setting the fever adds nothing to a fresh man.");

        public static readonly ParamDef FeverContactMetres = Float("FeverContactMetres", 1, 20, FeverGroup,
            "Fever starts burning at (m)",
            "The fever's clock starts the first time an enemy comes within this many metres of him - a general orders the charge from afar, and the fever should not be spent on the walk. Once burning it burns on, enemy near or not, until it is empty.");

        public static readonly ParamDef FeverShieldDurationMultiplier = Float("FeverShieldDurationMultiplier", 0.1, 2, FeverGroup,
            "Fever length with a shield in hand (x)",
            "While a man holds a shield in his other hand his fever burns faster, so it lasts this share as long (0.7 = 70%): a shield man waits for the opening behind it. Read live - a man who takes his shield off his back, or puts it away for a two-handed axe, burns at the other rate from then on. 1 = the same with or without.");

        public static readonly ParamDef FeverSprintToClose = Bool("FeverSprintToClose", FeverGroup,
            "Feverish men sprint the last metres",
            "A man in his fever may sometimes sprint at an enemy a few metres away (the distances below): every check he rolls the chance below, scaled by how much fever he has left. He spends only what he holds above his order's sprint reserve (the Sprint settings). Needs the sprint and \"AI soldiers sprint on new orders\" on.");

        public static readonly ParamDef FeverSprintMinMetres = Float("FeverSprintMinMetres", 0, 10, FeverGroup,
            "Fever sprint: enemy at least (m)",
            "A fever sprint starts only with the nearest enemy at least this far away - closer is already melee.");

        public static readonly ParamDef FeverSprintMaxMetres = Float("FeverSprintMaxMetres", 1, 40, FeverGroup,
            "Fever sprint: enemy at most (m)",
            "A fever sprint starts only with an enemy within this many metres - near enough that a small store lasts to him. It runs into contact (until his first attack) while \"AI charge sprints run into contact\" (Sprint) is on, else it ends once an enemy is within a few metres.");

        public static readonly ParamDef FeverSprintCheckSeconds = Float("FeverSprintCheckSeconds", 0.1, 10, FeverGroup,
            "Fever sprint: a roll every (s)",
            "How often a feverish man who could sprint at an enemy rolls for it, in seconds.");

        public static readonly ParamDef FeverSprintChancePercent = Int("FeverSprintChancePercent", 0, 100, FeverGroup,
            "Fever sprint: chance per roll (%)",
            "The chance of a fever sprint at each roll at the fever's start (and while it waits for the first enemy); it falls in a straight line with the fever left - half the fever left, half the chance; empty, none. Several sprints per fever are possible, rarer as it fades. 0 = never.");

        // ------------------------------------------------------------------ courage and cohesion (step 49)
        // Anton, 2026-10-17 night: AI men are unrealistically brave - a lone man circles a shield wall, stands exhausted among four enemies.
        // Each AI man on foot weighs his side against the enemy near him by TOUGHNESS; short of courage he walks back to a bigger group of
        // his side and only strikes what comes within reach (Core CourageMath, AI_NOTES "Step 49"). Its switch CourageEnabled is on the
        // player page (step 59).

        public static readonly ParamDef CourageRadiusMetres = Float("CourageRadiusMetres", CourageMath.MinRadiusMetres, CourageMath.MaxRadiusMetres, CourageGroup,
            "Courage: looks around him (m)",
            "The circle a man weighs his mates and his enemies in. Men within half of it count in full, then less and less to 0 at its edge (so a man walking across the edge does not flip anybody's courage at once).");

        public static readonly ParamDef CourageBaseOddsPercent = Int("CourageBaseOddsPercent", CourageMath.MinOddsPercent, CourageMath.MaxPercent, CourageGroup,
            "Courage: odds a fresh man needs (%)",
            "How strong his side must be around him, in percent of the enemy's strength there, for a fresh, steady soldier to stay brave: 100 = even odds. Tiredness, low morale and shock raise it; a fever and being a hero lower it. Lower = braver soldiers.");

        public static readonly ParamDef CourageArmourWeightPercent = Int("CourageArmourWeightPercent", 0, CourageMath.MaxPercent, CourageGroup,
            "Toughness: armour weight (% per 100 armour)",
            "How much armour makes a man count: his toughness is x (1 + this % x his armour / 100) - head, body, legs and arms added up. At 200 a looter in rags (armour ~8) counts 1.16, a man in 110 armour 3.2.");

        public static readonly ParamDef CourageLevelWeightPercent = Int("CourageLevelWeightPercent", 0, CourageMath.MaxPercent, CourageGroup,
            "Toughness: level weight (% per 30 levels)",
            "How much a man's level (his troop tier - his skill and experience) makes him count: x (1 + this % x level / 30), levels above 60 count as 60. At 100 a level-5 looter counts x1.17, a level-26 legionary x1.87. With the armour: about 4-5 looters per armoured veteran.");

        public static readonly ParamDef CourageHealthWeightPercent = Int("CourageHealthWeightPercent", 0, 100, CourageGroup,
            "Toughness: wounds weight (%)",
            "A wounded man counts less: x (1 - this % x the share of his health he lost). At 50 a man at half health counts 75%.");

        public static readonly ParamDef CourageHeavyWeaponPercent = Int("CourageHeavyWeaponPercent", 100, CourageMath.MaxPercent, CourageGroup,
            "Toughness: an enemy with a two-hander (%)",
            "An ENEMY who carries a two-handed weapon or a polearm and no shield counts this much more (150 = half again): a great axe breaks a guard and outreaches a sword - tired, shaken men will not stay against three or four such shock troops. Read once from his kit. 100 = no difference.");

        public static readonly ParamDef CourageTiredWeightPercent = Int("CourageTiredWeightPercent", 0, CourageMath.MaxPercent, CourageGroup,
            "Courage: tiredness raises the odds needed (%)",
            "A tired man clings to his mates: the odds he needs x (1 + this % x the share of his Athletics bar he has spent). At 100 an empty man needs twice the odds of a fresh one. Not counted while the Athletics system is off.");

        public static readonly ParamDef CourageMoraleWeightPercent = Int("CourageMoraleWeightPercent", 0, CourageMath.MaxPercent, CourageGroup,
            "Courage: low morale raises the odds needed (%)",
            "The game's own morale (0-100; men start at 35-65 and lose it as their side falls): below 50 the odds he needs x (1 + this % x (50 - morale) / 50). At 100 a man at morale 25 needs 1.5 times the odds. High morale adds nothing - that is the fever's job.");

        public static readonly ParamDef CourageShockSeconds = Float("CourageShockSeconds", 0, 60, CourageGroup,
            "Shock: lasts (s)",
            "A man who was hit is SHOCKED for this many seconds: each landed hit in that window counts (and, once the impact system's effects are built, each stagger, knockdown or dropped shield), three = full shock. A shocked man needs higher odds, wants more mates around him and packs in tighter; shocked and badly outmatched he PANICS. 0 = no shock.");

        public static readonly ParamDef CourageShockWeightPercent = Int("CourageShockWeightPercent", 0, CourageMath.MaxPercent, CourageGroup,
            "Shock: raises the odds needed (%)",
            "At full shock the odds he needs x (1 + this %): at 100 three hits in the window double them, one hit adds a third.");

        public static readonly ParamDef CourageFeverPercent = Int("CourageFeverPercent", 0, 100, CourageGroup,
            "Courage in a fever (%)",
            "A man in his FEVER (the charge's surge - Aggression and fever) who is NOT shaken by a hit presses on whatever the odds - the charge goes in. Once hit, his fever only lowers the odds he needs: to this share while it is full, back to 100% as it burns out. When it fades, the weak side's men pull back. 100 = the fever does not lower the odds of a hit man.");

        public static readonly ParamDef CourageHeroPercent = Int("CourageHeroPercent", CourageMath.MinOddsPercent, 200, CourageGroup,
            "Courage of heroes (%)",
            "A hero (a lord, a companion) needs this share of the odds a soldier needs - they lead from the front. Their armour and level already make them count for a lot.");

        public static readonly ParamDef CourageHysteresisPercent = Int("CourageHysteresisPercent", 0, 200, CourageGroup,
            "Courage: brave again above (+%)",
            "A timid man becomes brave again only when the odds are this much ABOVE what he needs (25 = 25% more) - so he does not flicker between brave and timid at the edge.");

        public static readonly ParamDef CourageMinTimidSeconds = Float("CourageMinTimidSeconds", 0, 10, CourageGroup,
            "Courage: timid at least (s)",
            "Once timid a man stays timid at least this long, whatever the odds do.");

        public static readonly ParamDef CourageSurgeChancePercent = Int("CourageSurgeChancePercent", 0, 100, CourageGroup,
            "Surge: chance a look (%)",
            "Now and then a timid man steps in: one who is not shaken by a hit, stands among his mates and has an enemy within 8 m rolls this chance about every half second, times his Athletics bar (a tired man surges less). A surge makes him brave for the seconds below - he steps in and swings, then the normal rules take him back. At 5 a fresh man surges about every 10 s. 0 = never.");

        public static readonly ParamDef CourageSurgeSeconds = Float("CourageSurgeSeconds", 0, 30, CourageGroup,
            "Surge: lasts (s)",
            "How long a surge makes a timid man brave.");

        public static readonly ParamDef CourageRegroupRadiusMetres = Float("CourageRegroupRadiusMetres", 1, 30, CourageGroup,
            "Regroup: close enough (m)",
            "A timid man walks back only when he is farther than this from his group's centre; within it he has arrived and holds (half of it when shocked - shaken men pack in).");

        public static readonly ParamDef CourageRegroupMates = Int("CourageRegroupMates", 0, 30, CourageGroup,
            "Regroup: mates around him (men)",
            "A timid man with at least this many mates within the courage circle is with his group - he holds there instead of walking (twice as many when shocked). 0 = timid men never walk back, they only hold.");

        public static readonly ParamDef CourageFlockRadiusMetres = Float("CourageFlockRadiusMetres", 0, 100, CourageGroup,
            "Regroup: a bigger group within (m)",
            "A timid man walks to the nearest bigger group of his own side - the centre of his allies within this distance, when there are enough of them (below) - like sheep pushed into the flock; else to his formation's centre of mass. 0 = always the formation's centre.");

        public static readonly ParamDef CourageFlockMinMen = Int("CourageFlockMinMen", 1, 100, CourageGroup,
            "Regroup: a bigger group is (men)",
            "How many allies within the distance above make a group worth running to.");

        public static readonly ParamDef CourageSprintBack = Bool("CourageSprintBack", CourageGroup,
            "Timid men sprint back",
            "A timid man whose group is far (below), or who has several enemies near him, sprints back - spending only what he holds above his reserve (below). In a PANIC he sprints at once and may spend nearly everything. Needs the sprint and \"AI soldiers sprint on new orders\" on.");

        public static readonly ParamDef CourageSprintMetres = Float("CourageSprintMetres", 0, 100, CourageGroup,
            "Sprint back: the group farther than (m)",
            "A timid man sprints back when his group's centre is farther than this.");

        public static readonly ParamDef CourageSprintEnemies = Int("CourageSprintEnemies", 1, 30, CourageGroup,
            "Sprint back: enemies near him (men)",
            "...or when at least this many enemies are within the courage circle.");

        public static readonly ParamDef CourageSprintKeepPercent = Int("CourageSprintKeepPercent", 0, 100, CourageGroup,
            "Sprint back: reserve kept (%)",
            "A timid man's sprint back spends only what he holds above this share of his full sprint store - a bit of his sprint, the rest kept for the fight.");

        public static readonly ParamDef CouragePanicOddsPercent = Int("CouragePanicOddsPercent", 0, 100, CourageGroup,
            "Panic: below these odds (%)",
            "A SHOCKED man whose odds are below this share of what he needs PANICS: he sprints back at once, whatever the distance, and keeps only the panic reserve below. 0 = no panic.");

        public static readonly ParamDef CouragePanicSprintKeepPercent = Int("CouragePanicSprintKeepPercent", 0, 100, CourageGroup,
            "Panic: sprint reserve kept (%)",
            "In a panic a man sprints everything above this share of his sprint store - his order's reserve and the normal sprint-back reserve do not hold him. 0 = all of it: panic uses all their energy.");

        public static readonly ParamDef CourageTightenWeights = Bool("CourageTightenWeights", CourageGroup,
            "Timid men stop hunting",
            "While timid a man's own AI fights only an enemy within reach (below) and otherwise keeps to his place - the game's per-man behaviour weights, re-applied after every order (the game resets them). Brave again: the game's own. Off: timid men only walk back, their AI's eagerness untouched.");

        public static readonly ParamDef CourageTimidMeleeMetres = Float("CourageTimidMeleeMetres", CourageMath.MinMeleeMetres, CourageMath.MaxMeleeMetres, CourageGroup,
            "Timid men strike within (m)",
            "A timid man still fights an enemy this close - beyond it his own place wins. 2 m is about a sword's reach.");

        // ------------------------------------------------------------------ the fear flock (step 67, courage's evolution)
        // Anton, 2026-10-19: "as troops lose their morale and freshness, they flock less to their formation position and more to the centre of
        // their unit's mass, pushed back away from the enemy mass ... stepping behind their comrades". FEAR per AI man on foot = low morale and
        // lost freshness x (1 - his discipline), mixed with his unit's; it blends his place from his slot toward a SHELTER behind the unit's centre
        // (Core FearMath, AI_NOTES "Step 67"). In courage's group: the same looks, the same walk-back lever, one system.

        public static readonly ParamDef FearFlockEnabled = Bool("FearFlockEnabled", CourageGroup,
            "Fear flock: afraid men bunch up and fall back",
            "As an AI soldier on foot loses MORALE and Athletics his fear grows - less for disciplined, high-tier troops. A fearful man leaves his place in the formation for a spot among his mates, behind a comrade if he can, and the frightened mass edges back from the enemy: green troops thicken a thin line, a spent unit becomes a herd that is easy to surround. A fresh unit obeys exactly. Courage's walk back (a man alone among enemies) goes first. Off: men keep their formation places (courage still works).");

        public static readonly ParamDef FearMoraleCalm = Int("FearMoraleCalm", 0, 100, CourageGroup,
            "Fear: morale calm at (0-100)",
            "At or above this morale (the game's own, 0-100 - a man starts about 35-65 and loses it as his side loses men) morale adds no fear.");

        public static readonly ParamDef FearMoraleBroken = Int("FearMoraleBroken", 0, 100, CourageGroup,
            "Fear: morale broken at (0-100)",
            "At or below this morale, morale alone is full fear; between the two a straight line.");

        public static readonly ParamDef FearFreshPercent = Int("FearFreshPercent", 1, 100, CourageGroup,
            "Fear: fresh above (% of his bar)",
            "With at least this share of his Athletics bar a man's tiredness adds no fear; below it a straight line to full fear at an empty bar. With the Athletics system off tiredness never adds fear.");

        public static readonly ParamDef FearMoraleWeightPercent = Int("FearMoraleWeightPercent", 0, 100, CourageGroup,
            "Fear: from low morale (%)",
            "How much of the morale fear counts. Morale and tiredness add up like two dangers: either can frighten a man; both together more.");

        public static readonly ParamDef FearTiredWeightPercent = Int("FearTiredWeightPercent", 0, 100, CourageGroup,
            "Fear: from tiredness (%)",
            "How much of the tiredness fear counts.");

        public static readonly ParamDef DisciplineLevelLow = Int("DisciplineLevelLow", 0, 60, CourageGroup,
            "Discipline: none up to level",
            "A man's DISCIPLINE comes from his troop level (his tier): at or below this level he has none - looters, recruits. His fear is multiplied by (1 - discipline).");

        public static readonly ParamDef DisciplineLevelHigh = Int("DisciplineLevelHigh", 1, 62, CourageGroup,
            "Discipline: full at level",
            "At or above this level a man has the full discipline below (26 is a top-tier imperial legionary); between the two a straight line. Heroes always have it.");

        public static readonly ParamDef DisciplineMaxPercent = Int("DisciplineMaxPercent", 0, 100, CourageGroup,
            "Discipline: the most (%)",
            "The discipline of the best troops: at 70 a veteran feels 30% of the fear a looter would - he holds his place until he is truly spent. 100 = elite men never flock.");

        public static readonly ParamDef FearUnitSharePercent = Int("FearUnitSharePercent", 0, 100, CourageGroup,
            "Fear: the unit's share (%)",
            "A man's fear is his own mixed with his unit's average by this share - so the mass moves together and one shaken man does not run while his mates stand.");

        public static readonly ParamDef FearThinLinePercent = Int("FearThinLinePercent", 0, 100, CourageGroup,
            "Fear: nobody behind him (+%)",
            "A man with an enemy near and NO comrade behind him (a single rank, a wide spread) feels this much more fear, times (1 - his discipline): green troops thicken a thin line by themselves, veterans hold it.");

        public static readonly ParamDef FearStartPercent = Int("FearStartPercent", 0, 95, CourageGroup,
            "Fear flock: starts at (% fear)",
            "Below this fear a man keeps exactly to his formation place - a fresh unit obeys exactly.");

        public static readonly ParamDef FearFullPercent = Int("FearFullPercent", 5, 100, CourageGroup,
            "Fear flock: all the way at (% fear)",
            "At this fear a man's place has moved all the way from his slot to the shelter behind his unit's centre; between the start and this a straight line.");

        public static readonly ParamDef FearShelterBackMetres = Float("FearShelterBackMetres", 0, 20, CourageGroup,
            "Shelter: behind the centre (m)",
            "The SHELTER is his unit's centre of mass pushed back from the enemy near the unit by this many metres times the unit's own fear - a frightened mass edges back from the enemy.");

        public static readonly ParamDef FearShelterMaxMetres = Float("FearShelterMaxMetres", 1, 50, CourageGroup,
            "Shelter: at most from his slot (m)",
            "A fearful man's place is never farther than this from his formation slot.");

        public static readonly ParamDef FearDriftMetresPerMinute = Float("FearDriftMetresPerMinute", 0, 60, CourageGroup,
            "Shelter: drifts back at most (m a minute)",
            "However frightened, the shelter moves back from the enemy at most this fast - no endless shuffle; at the extreme the game's own rout takes over.");

        public static readonly ParamDef FearDriftHoldPercent = Int("FearDriftHoldPercent", 0, 100, CourageGroup,
            "Shelter: drift under hold / shield wall (%)",
            "Under a hold order or in a shield wall, square or circle the drift above is only this share - such units stay nearly in place.");

        public static readonly ParamDef FearCoverWidthMetres = Float("FearCoverWidthMetres", 0.3, 3, CourageGroup,
            "Cover: a comrade within (m of the line)",
            "A comrade COVERS a man when he stands between him and the nearest enemy, at most this far to the side of the line between them. An uncovered fearful man steps in behind the nearest comrade.");

        public static readonly ParamDef FearAnchorFreshPercent = Int("FearAnchorFreshPercent", 0, 100, CourageGroup,
            "Cover: a fresh comrade anchors (% of his bar)",
            "A covering comrade with at least this share of his Athletics bar ANCHORS the man behind him: he keeps that spot (for the time below) instead of drifting on. As the comrade tires, the pull comes back.");

        public static readonly ParamDef FearAnchorSeconds = Float("FearAnchorSeconds", 0, 120, CourageGroup,
            "Cover: anchored for (s)",
            "How long a fresh comrade in front keeps a fearful man in his spot before the pull of the mass takes him again. 0 = never anchored.");

        public static readonly ParamDef FearSprintMetres = Float("FearSprintMetres", 0, 50, CourageGroup,
            "Fear flock: sprint back farther than (m)",
            "A fearful man farther than this from his place among his mates - or alone with an enemy near - sprints back to it, spending only what he holds above the reserve below. Needs the sprint and \"AI soldiers sprint on new orders\" on.");

        public static readonly ParamDef FearSprintChancePercent = Int("FearSprintChancePercent", 0, 100, CourageGroup,
            "Fear flock: sprint now and then (%)",
            "A fearful man a few metres from his place and not running rolls this chance about every half second to sprint there anyway.");

        public static readonly ParamDef FearSprintKeepPercent = Int("FearSprintKeepPercent", 0, 100, CourageGroup,
            "Fear flock: sprint reserve kept (%)",
            "The fear flock's sprint spends only what a man holds above this share of his full sprint store.");

        public static readonly ParamDef FearWallBreakMorale = Int("FearWallBreakMorale", 0, 100, CourageGroup,
            "Shield walls break below morale (0-100)",
            "A shield wall, square or circle keeps its men in their slots while its men's average morale is at least this; below it the wall breaks and its frightened men join the flock. 0 = a wall never breaks.");

        public static readonly ParamDef FearChargeLooseMetres = Float("FearChargeLooseMetres", 2, 50, CourageGroup,
            "Charging: a calm man roams within (m)",
            "Under a charge there are no slots: a fearful man may roam this far from his unit's shelter times (1 - his flock pull) - the more afraid, the tighter the mass; beyond it he is pulled back in.");

        // Step 69 (Toni 2026-10-19: "some of my men are sitting there in distance while my soldiers are finishing the enemy off"): fear came from
        // morale and tiredness only, never from who is winning - a tired green WINNER kept his shelter behind his unit's centre. Winning near him
        // now calms it (courage's own local odds, the enemy's morale), no enemy near = no pull, a charge's mop-up drops the loose-mass leash
        // (Core FearMath.Win / Threatened / MopUp, AI_NOTES "Step 69"). Losing men flock exactly as before.

        public static readonly ParamDef FearWinCalms = Bool("FearWinCalms", CourageGroup,
            "Fear: winning calms",
            "A man whose side is clearly winning around him (the same local odds courage weighs, and the enemy near him shaken or routing) loses the fear his morale and tiredness gave him, so tired winners go in for the kill instead of hanging back; a winning unit relaxes together. Losing men flock exactly as before. Off: fear ignores who is winning.");

        public static readonly ParamDef FearWinEvenOddsPercent = Int("FearWinEvenOddsPercent", 0, 1000, CourageGroup,
            "Fear: winning from odds (%)",
            "His side's local odds (toughness-weighted, as courage counts them; 100 = even) at which winning starts to calm him. Below it he is losing and his fear is untouched.");

        public static readonly ParamDef FearWinClearOddsPercent = Int("FearWinClearOddsPercent", 1, 1000, CourageGroup,
            "Fear: no fear at odds (%)",
            "At these local odds (200 = two to one) his fear is gone; between the line above and this a straight line.");

        public static readonly ParamDef FearWinShakenPercent = Int("FearWinShakenPercent", 0, 100, CourageGroup,
            "Fear: a broken enemy calms (%)",
            "At even odds or better, an enemy near him whose morale is broken (routing men included) calms him by up to this much more - the enemy's morale read on the fear's own lines above.");

        public static readonly ParamDef FearThreatMetres = Float("FearThreatMetres", 0, 60, CourageGroup,
            "Fear flock: only with an enemy within (m)",
            "With no enemy this close a man is not pulled anywhere by his fear - he follows his order (his morale and bars still show). 0 = fear pulls him even with no enemy near.");

        public static readonly ParamDef FearMopUpWinPercent = Int("FearMopUpWinPercent", 0, 100, CourageGroup,
            "Charging: mop-up when winning (%)",
            "A charging unit whose men in contact win at least this much on average (the few or broken enemy left) is mopping up: its frightened men are no longer held near the mass and go for the kill. 0 = never.");

        // Step 70 (Toni's 30 v 50 looters 2026-10-19 15:59: the frightened looters stood as a column ~15 men deep facing his line - "it needs to be
        // 4-5-6 men deep", a wide mass, a few stepping forward to swing; and a few looters ran round BEHIND his line alone after the first charge):
        // the flock's shelter is a wide BAND along the enemy's front, not a point (Core FearBandMath / FearUnits.BandPlace), front men are let go to
        // strike within reach, and a LONE RUNNER - alone, out beyond his line - is pulled back (Core LoneRunnerMath, AI_NOTES "Step 70").

        public static readonly ParamDef FearBandEnabled = Bool("FearBandEnabled", CourageGroup,
            "Fear flock: a wide band, not a point",
            "Frightened men gather in a wide, shallow band along the enemy's front - each keeps his place along the line and only closes up in depth, a few ranks deep (below); men behind a full file slide out to the band's ends. Off: they draw to one point behind the unit's centre (deep columns).");

        public static readonly ParamDef FearBandRanksMin = Int("FearBandRanksMin", 1, 20, CourageGroup,
            "Fear band: ranks deep, a little afraid",
            "How many men deep the frightened mass stands at a small pull; it deepens in a straight line to the next setting at the full pull.");

        public static readonly ParamDef FearBandRanksMax = Int("FearBandRanksMax", 1, 20, CourageGroup,
            "Fear band: ranks deep, terrified",
            "How many men deep the frightened mass stands at the full pull (never below the setting above). A file deeper than this sends its rear men out to the band's ends.");

        public static readonly ParamDef FearBandFileMetres = Float("FearBandFileMetres", 0.5, 3, CourageGroup,
            "Fear band: file width (m)",
            "The width one file of men takes in the band - the band's width from its men is men ÷ ranks × this.");

        public static readonly ParamDef FearBandRankMetres = Float("FearBandRankMetres", 0.5, 3, CourageGroup,
            "Fear band: rank depth (m)",
            "The depth one rank takes in the band - its depth is the ranks × this.");

        public static readonly ParamDef FearBandMaxWidthMetres = Float("FearBandMaxWidthMetres", 5, 150, CourageGroup,
            "Fear band: at most this wide (m)",
            "The band is as wide as the enemy's front it faces, or its men in ranks, whichever is wider - but never wider than this.");

        public static readonly ParamDef FearBandGapMetres = Float("FearBandGapMetres", 0, 10, CourageGroup,
            "Fear band: front stands off the enemy (m)",
            "The band's front stands this far short of the enemy's front; the more afraid the unit, the further back it is pushed (the shelter's metres above), creeping back at the drift above.");

        public static readonly ParamDef FearStrikeReachMetres = Float("FearStrikeReachMetres", 0, 6, CourageGroup,
            "Fear flock: front men strike within (m)",
            "A frightened man in the band's front rank (nobody between him and the enemy) with an enemy this close is let go to step in and swing; he is walked back once he is further ahead of his place than the next setting. 0 = the flock never lets them go.");

        public static readonly ParamDef FearStrikeLeashMetres = Float("FearStrikeLeashMetres", 0.5, 10, CourageGroup,
            "Fear flock: back to the mass beyond (m)",
            "A front man let go to strike is walked back to his place in the band once he stands this far ahead of it.");

        public static readonly ParamDef LoneRunnerEnabled = Bool("LoneRunnerEnabled", CourageGroup,
            "Lone runners come back",
            "A man alone (fewer than two mates near, an enemy close) out beyond his line - ahead of his unit's front (below) or past the enemy's front - is no longer sent on by his fever, his charge sprint ends, and he walks back to his group - unless he is chasing a beaten enemy. Never you, never riders. Off: measured only.");

        public static readonly ParamDef LoneRunnerAheadMetres = Float("LoneRunnerAheadMetres", 0, 30, CourageGroup,
            "Lone runner: ahead of his line (m)",
            "A man alone counts as a lone runner when he stands more than this far ahead of his own unit's front toward the enemy (or anywhere past the enemy's front).");

        // Step 77 (Toni 2026-10-22): press, don't pass - its numbers (PressMath, AI_NOTES "Step 77", DESIGN decision 71).
        public static readonly ParamDef PressToleranceMetres = Float("PressToleranceMetres", 0, 3, CourageGroup,
            "Press: carried past a line's front (m)",
            "With \"Press, don't pass\" on (player page): a soldier carried more than this past an enemy line's front (a shove, a blow) walks straight back out of it, facing it.");

        public static readonly ParamDef PressWatchMetres = Float("PressWatchMetres", 0.5, 20, CourageGroup,
            "Press: watched within (m)",
            "Within this many metres of an enemy line's front a soldier's movement toward it stops at the front - he presses and fights, he does not walk past.");

        public static readonly ParamDef PressGapMetres = Float("PressGapMetres", 0.5, 10, CourageGroup,
            "Press: a real gap is at least (m)",
            "Soldiers may go THROUGH an enemy line only where an empty stretch of its front at least this wide has opened (its front men fallen, nobody stepping up).");

        public static readonly ParamDef PressManWidthMetres = Float("PressManWidthMetres", 0.4, 3, CourageGroup,
            "Press: one front man covers (m)",
            "How much of a line's front one front-rank soldier closes - a gap is the stretch between front men that nobody covers.");

        public static readonly ParamDef PressFrontDepthMetres = Float("PressFrontDepthMetres", 0.5, 5, CourageGroup,
            "Press: a line's front rank is (m deep)",
            "A man standing within this behind a line's front holds it (the rear man who steps up into a fallen man's place closes the gap); also a man more than this behind his own unit's front is in its back ranks.");

        public static readonly ParamDef PressWrapMarginMetres = Float("PressWrapMarginMetres", 0, 60, CourageGroup,
            "Press: wider by to go round (m)",
            "A unit may go round an enemy line's ends only when its men spread at least this much wider than the line - a real overlap, not a few stragglers.");

        public static readonly ParamDef PressWrapMates = Int("PressWrapMates", 0, 30, CourageGroup,
            "Press: mates near to go round",
            "... and a soldier goes round with it only when at least this many of his mates are close to him (next setting) - he moves as part of the mass; a lone man or a small group never goes round.");

        public static readonly ParamDef PressWrapMatesMetres = Float("PressWrapMatesMetres", 1, 8, CourageGroup,
            "Press: mates near within (m)",
            "How close those mates must be for a soldier to go round an enemy line's end with them.");

        public static readonly ParamDef PressRoundEndMetres = Float("PressRoundEndMetres", 0.5, 10, CourageGroup,
            "Press: walked round an end by (m)",
            "A soldier who got behind an enemy line anyway (and may not be there) walks back round its nearest end, passing this far beyond it, then forward to his side.");

        public static readonly ParamDef PressWinPercent = Float("PressWinPercent", 0, 100, CourageGroup,
            "Press: lets go when winning (%)",
            "When a soldier's unit is winning this much (the same measure as \"winners don't hang back\": the local odds and the enemy's broken morale) the enemy line counts as beaten - its men go where they like.");

        public static readonly ParamDef PressLineMinMen = Int("PressLineMinMen", 3, 200, CourageGroup,
            "Press: a line has at least (men)",
            "An enemy formation counts as a line to press against only with at least this many men on foot.");

        public static readonly ParamDef PressLineInSlotPercent = Float("PressLineInSlotPercent", 0, 100, CourageGroup,
            "Press: a line's men in their places (%)",
            "A formation that is not a held shield wall or a held block counts as a line only while at least this share of its men stand within 3 m of their places (a hold, move or advance - never a charge or a retreat).");

        public static readonly ParamDef PressBandMarginMetres = Float("PressBandMarginMetres", 0, 10, CourageGroup,
            "Press: frightened men no wider than the line + (m)",
            "Frightened soldiers bunching into their band never spread wider than the enemy line they face plus this on each side; the men who do not fit stand deeper, in more ranks, instead of out at the sides.");

        public static readonly ParamDef PressBlockEndMetres = Float("PressBlockEndMetres", 0, 10, CourageGroup,
            "Press: a block's sides at the enemy's ends + (m)",
            "In contact a green unit's block (line discipline) keeps its men sideways within the enemy line's ends plus this - not out round them.");

        public static readonly ParamDef PressEndSlackMetres = Float("PressEndSlackMetres", 0, 10, CourageGroup,
            "Press: back ranks past a line's end (m)",
            "A soldier of a back rank may drift sideways at most this far past an enemy line's end - the back ranks stay behind their own front rank and press on.");

        // ------------------------------------------------------------------ cavalry flocking (step 55)
        // Anton, 2026-10-18: fresh, eager riders dance as in vanilla; the more tired a rider, the bigger the group of his own kind he wants
        // before he attacks again - the flock rides on, pulled toward the unit's centre by its tired riders and toward the goal by its rested
        // ones, and FLIPS into a charge as one (Core FlockMath, AI_NOTES "Step 55"). Its switch CavalryFlockEnabled is on the player page (step 59).

        public static readonly ParamDef CavalryFlockFreeEagerPercent = Int("CavalryFlockFreeEagerPercent", 10, 100, CavalryFlockGroup,
            "Riders charge alone above (%)",
            "A rider's EAGERNESS is his Athletics bar (halved when he is alone among enemies, below). At or above this he is free - the game's own charging, again and again. Below it he flocks with his kind. With the Athletics system off everyone is at 100%.");

        public static readonly ParamDef CavalryFlockHysteresisPercent = Int("CavalryFlockHysteresisPercent", 0, 30, CavalryFlockGroup,
            "Free again above the line by (%)",
            "A flocking rider charges alone again only once his eagerness is this far above the line - so he does not flicker between flocking and charging at the edge.");

        public static readonly ParamDef CavalryFlockMidEagerPercent = Int("CavalryFlockMidEagerPercent", 5, 95, CavalryFlockGroup,
            "Group size: half-tired at (%)",
            "The group-size curve's middle point: a rider at this eagerness wants the group size below before he attacks again. At the free line he wants 1 (himself); between, a straight line.");

        public static readonly ParamDef CavalryFlockMidGroupSize = Int("CavalryFlockMidGroupSize", 1, 20, CavalryFlockGroup,
            "Group size: half-tired wants (riders)",
            "How many riders of his kind (him included) a half-tired rider wants within reach before he attacks again.");

        public static readonly ParamDef CavalryFlockExhaustedSharePercent = Int("CavalryFlockExhaustedSharePercent", 0, 100, CavalryFlockGroup,
            "Group size: exhausted wants (% of his kind)",
            "An exhausted rider wants this share of his kind's riders in his formation before he attacks again (never fewer than the half-tired size); a straight line from the middle point down to empty. A flock goes in when the group has the size its riders' AVERAGE eagerness wants.");

        public static readonly ParamDef CavalryFlockTimid = Bool("CavalryFlockTimid", CavalryFlockGroup,
            "Lone riders flock sooner",
            "A rider alone among many enemies - courage's odds (Courage and cohesion: the toughness of his side against the enemy's near him, with tiredness, morale, shock and heroes) - counts at half his eagerness, so he looks for his kind even when he is fresh. Needs Courage and cohesion on (its numbers are used).");

        public static readonly ParamDef CavalryFlockTimidRadiusMetres = Float("CavalryFlockTimidRadiusMetres", 5, 40, CavalryFlockGroup,
            "Lone rider: looks around him (m)",
            "The circle a rider weighs his side against the enemy in - wider than a man on foot's, a horse covers ground fast.");

        public static readonly ParamDef CavalryFlockGatherRadiusMetres = Float("CavalryFlockGatherRadiusMetres", 5, 100, CavalryFlockGroup,
            "Riders with him within (m)",
            "The flocking riders of his kind this close to him are \"with him\" - his group.");

        public static readonly ParamDef CavalryFlockSeparationMetres = Float("CavalryFlockSeparationMetres", 1, 10, CavalryFlockGroup,
            "Flock: room between riders (m)",
            "Each flocking rider rides to his own place in the flock, about this far from the next - horses never stack on one point.");

        public static readonly ParamDef CavalryFlockLeadSeconds = Float("CavalryFlockLeadSeconds", 0, 5, CavalryFlockGroup,
            "Flock: rides ahead (s)",
            "A flocking rider rides to his group's centre led this many seconds ahead along the flock's heading (at the flock's speed, at least a canter) - the flock keeps moving instead of converging on a still dot. 0 = the centre itself.");

        public static readonly ParamDef CavalryFlockTightenWeights = Bool("CavalryFlockTightenWeights", CavalryFlockGroup,
            "Flocking riders stop picking targets",
            "While flocking a rider's own AI strikes only what is right beside him and does not ride off for a target - the game's per-rider behaviour weights, re-applied after every order (the game resets them). Back in: the game's own. Off: the ride with the flock alone.");

        public static readonly ParamDef CavalryFlockWaveSeconds = Float("CavalryFlockWaveSeconds", 0, 60, CavalryFlockGroup,
            "A wave charges for (s)",
            "When a flock goes in, its riders charge as the game's own riders for this long before each one decides again by his eagerness - the eager ones dance on, the tired ones flock again.");

        public static readonly ParamDef CavalryFlockMaxSeconds = Float("CavalryFlockMaxSeconds", 5, 180, CavalryFlockGroup,
            "Give up flocking after (s)",
            "A rider who has flocked this long without his flock going in is released on his own (a wave of one).");

        // ------------------------------------------------------------------ impact (step 48 = IMPACT I2, docs/IMPACT.md §3 / §5)
        // Anton, 2026-10-17: kicks, bashes, blocks and clean hits may stagger, drop a shield or knock down, by both men's CURRENT
        // Athletics, the weapon, the swing and the skill. One number - the PRESSURE x on the man who takes the blow - and for each
        // outcome a ceiling and an S-curve (Core ImpactMath). Order here: the switches, Anton's anchors (the ceilings), how long, the
        // limits, who - then the model's inner numbers (pressure, weapon, skill, guard, curve) a player rarely touches.
        // Step 59: the master switch ImpactEnabled and ImpactOnPlayerPercent are on the player page.

        public static readonly ParamDef ImpactKicksEnabled = Bool("ImpactKicksEnabled", ImpactGroup,
            "Impact from kicks and bashes",
            "A kick or shield bash that lands may knock a tired man down or make him drop his shield, and makes him reel longer the more tired he is. Off: kicks and bashes are the game's own.");

        public static readonly ParamDef ImpactBlocksEnabled = Bool("ImpactBlocksEnabled", ImpactGroup,
            "Impact on blocks and parries",
            "A blow blocked on the WRONG side may stagger the tired blocker, knock him down or make him drop his shield; a correct block (right-side shield, parry) breaks him mainly on a big mismatch - it is mostly the attacker's risk (the riposte). Off: blocks are the game's own.");

        public static readonly ParamDef ImpactHitsEnabled = Bool("ImpactHitsEnabled", ImpactGroup,
            "Impact from clean hits",
            "A weapon blow that lands unblocked may stagger a tired man longer or knock him down. Off: hits are the game's own.");

        public static readonly ParamDef ImpactRiposteEnabled = Bool("ImpactRiposteEnabled", ImpactGroup,
            "Riposte: a good guard staggers a tired attacker",
            "A man who blocks on the right side or parries may stagger the attacker who swung at him - more likely the more tired and outmatched the attacker is: a tired man flailing into a good guard opens himself up, a window to strike back.");

        public static readonly ParamDef ImpactShadowMode = Bool("ImpactShadowMode", ImpactGroup,
            "Shadow mode: decide and log only",
            "On: the impact system decides every kick, block and hit and writes what WOULD happen to the log (trax_combat.log's summary), but changes nothing in the battle - to read the numbers of your own battles before playing with it. Off: what it decides happens.");

        public static readonly ParamDef KickShieldDropMaxPercent = Int("KickShieldDropMaxPercent", 0, 100, ImpactGroup,
            "Kick: shield dropped, at most (%)",
            "The highest chance that a kick or bash makes a man drop the shield in his hand - reached when he is exhausted or badly outmatched. 0 in the green between even men.");

        public static readonly ParamDef KickKnockdownMaxPercent = Int("KickKnockdownMaxPercent", 0, 100, ImpactGroup,
            "Kick: knocked down, at most (%)",
            "The highest chance that a kick or bash knocks a man to the ground.");

        public static readonly ParamDef RightBlockStaggerMaxPercent = Int("RightBlockStaggerMaxPercent", 0, 100, ImpactGroup,
            "Right-side block: staggered, at most (%)",
            "The highest chance that a man who blocks a blow with his shield on the RIGHT side staggers back - reached only on a BIG mismatch (his own tiredness counts as little as the correct-block setting below says), longer the more tired he is.");

        public static readonly ParamDef RightBlockShieldDropMaxPercent = Int("RightBlockShieldDropMaxPercent", 0, 100, ImpactGroup,
            "Right-side block: shield dropped, at most (%)",
            "The highest chance that a RIGHT-side shield block makes him drop his shield. 0: a clean block is the reward for skill - only kicks and wrong-side blocks drop shields.");

        public static readonly ParamDef RightBlockKnockdownMaxPercent = Int("RightBlockKnockdownMaxPercent", 0, 100, ImpactGroup,
            "Right-side block: knocked down, at most (%)",
            "The highest chance that a RIGHT-side shield block still knocks him down - a huge mismatch (a peasant's stick against a berserker's axe), hardly his own tiredness.");

        public static readonly ParamDef ParryStaggerMaxPercent = Int("ParryStaggerMaxPercent", 0, 100, ImpactGroup,
            "Parry: staggered, at most (%)",
            "The highest chance that a man who parries a blow with his weapon staggers back - a parry is a correct block: reached only on a BIG mismatch.");

        public static readonly ParamDef ParryKnockdownMaxPercent = Int("ParryKnockdownMaxPercent", 0, 100, ImpactGroup,
            "Parry: knocked down, at most (%)",
            "The highest chance that a parry still knocks the parrying man down - a huge mismatch, hardly his own tiredness.");

        public static readonly ParamDef WrongBlockStaggerMaxPercent = Int("WrongBlockStaggerMaxPercent", 0, 100, ImpactGroup,
            "Wrong-side block: staggered, at most (%)",
            "The highest chance that a man who blocks with his shield on the WRONG side staggers back.");

        public static readonly ParamDef WrongBlockShieldDropMaxPercent = Int("WrongBlockShieldDropMaxPercent", 0, 100, ImpactGroup,
            "Wrong-side block: shield dropped, at most (%)",
            "The highest chance that a WRONG-side shield block makes him drop his shield.");

        public static readonly ParamDef WrongBlockKnockdownMaxPercent = Int("WrongBlockKnockdownMaxPercent", 0, 100, ImpactGroup,
            "Wrong-side block: knocked down, at most (%)",
            "The highest chance that a WRONG-side shield block knocks him down.");

        public static readonly ParamDef HitStaggerMaxPercent = Int("HitStaggerMaxPercent", 0, 100, ImpactGroup,
            "Clean hit: staggered longer, at most (%)",
            "The highest chance that a blow that lands unblocked staggers a man longer than the game's own flinch.");

        public static readonly ParamDef HitKnockdownMaxPercent = Int("HitKnockdownMaxPercent", 0, 100, ImpactGroup,
            "Clean hit: knocked down, at most (%)",
            "The highest chance that a blow that lands unblocked knocks a man down.");

        public static readonly ParamDef RiposteStaggerMaxPercent = Int("RiposteStaggerMaxPercent", 0, 100, ImpactGroup,
            "Riposte: attacker staggered, at most (%)",
            "The highest chance that a right-side block or parry staggers the ATTACKER - for an empty or outmatched attacker swinging into a good guard: a tired man flailing into a correct block opens himself up, whoever holds it (the next setting lets the blocker's own tiredness lower it).");

        public static readonly ParamDef CorrectBlockTiredWeightPercent = Int("CorrectBlockTiredWeightPercent", 0, 100, ImpactGroup,
            "Correct block: blocker's own tiredness counts (%)",
            "On a CORRECT block (right-side shield or a parry) the blocker's own tiredness counts only this much toward staggering or knocking him down - a correct block breaks him mainly on a BIG mismatch (a peasant's stick against a berserker's axe), so skill pays. 100: his tiredness counts in full, as on a wrong-side block or a clean hit.");

        public static readonly ParamDef RiposteBlockerTiredPercent = Int("RiposteBlockerTiredPercent", 0, 100, ImpactGroup,
            "Riposte: lowered by the blocker's tiredness (%)",
            "How much the BLOCKER's own tiredness lowers the riposte: its chance × (1 − this × his tiredness). 0: a tired man's correct block still makes a tired attacker reel. 100: only a blocker in the green ripostes at full chance, an empty one never.");

        public static readonly ParamDef StaggerBaseSeconds = Float("StaggerBaseSeconds", 0.1, 3, ImpactGroup,
            "Stagger length, fresh (s)",
            "How long a stagger lasts on a man in the green, in seconds. It grows with his tiredness by the next setting.");

        public static readonly ParamDef StaggerTiredExtraPercent = Int("StaggerTiredExtraPercent", 0, 500, ImpactGroup,
            "Stagger longer when exhausted (+%)",
            "An exhausted man's stagger lasts this much longer than a fresh man's (150: 0.6 s fresh, 1.5 s empty), in a straight line with his tiredness - and less predictably (the randomness below).");

        public static readonly ParamDef StaggerMaxSeconds = Float("StaggerMaxSeconds", 0.2, 5, ImpactGroup,
            "Stagger length, at most (s)",
            "No stagger lasts longer than this, in seconds, whatever the roll.");

        public static readonly ParamDef KickReelTiredExtraPercent = Int("KickReelTiredExtraPercent", 0, 500, ImpactGroup,
            "Kicked man reels longer when exhausted (+%)",
            "A kicked or bashed man reels back as in the game, but this much longer when exhausted (150: 2.5 times as long at an empty bar), in a straight line with his tiredness - so a kick gives a tired man's enemy time to strike. A fresh man reels as in the game.");

        public static readonly ParamDef GetUpMaxSlowdown = Float("GetUpMaxSlowdown", 1, 4, ImpactGroup,
            "Knocked-down man gets up slower, at most (x)",
            "An exhausted man knocked to the ground takes up to this many times as long to get up (2 = twice as long at an empty bar), in a straight line with his tiredness. 1 = the game's own.");

        public static readonly ParamDef BlockStunTiredExtraPercent = Int("BlockStunTiredExtraPercent", 0, 500, ImpactGroup,
            "Blocker's guard held longer when exhausted (+%)",
            "After every block or parry the blocker cannot act for a moment (the game's own stun); an exhausted blocker's lasts this much longer (100: twice as long at an empty bar). 0 (the default since the spike's playtest): the game's own stun - lengthening it changed nothing the engine plays; the stagger is the window.");

        public static readonly ParamDef RiposteStunMaxSeconds = Float("RiposteStunMaxSeconds", 0, 3, ImpactGroup,
            "Riposte: attacker's recoil, at most (s)",
            "A riposted attacker recoils for up to this many seconds - half of it when he is fresh, all of it when he is empty.");

        public static readonly ParamDef ImpactRandomPercent = Int("ImpactRandomPercent", 0, 100, ImpactGroup,
            "Lengths' randomness when exhausted (± %)",
            "Every stagger, reel, get-up and stun length is rolled within ± this percent at an EMPTY bar (less as he is fresher, none in the green): a fresh man's stagger is predictable, an exhausted man's is not.");

        public static readonly ParamDef ImpactStunCapSeconds = Float("ImpactStunCapSeconds", 0.8, 5, ImpactGroup,
            "Longest stun after a block (s)",
            "The game caps every block stun at 0.8 s; while the impact system runs it allows up to this many seconds for the stuns it lengthens (every other stun keeps the game's 0.8 s).");

        public static readonly ParamDef ImpactStaggerImmunitySeconds = Float("ImpactStaggerImmunitySeconds", 0, 20, ImpactGroup,
            "No new stagger for (s) after one",
            "After a stagger (or a lengthened reel) ends, the same man cannot be staggered again for this many seconds - nobody is stun-locked to death.");

        public static readonly ParamDef ImpactKnockdownImmunitySeconds = Float("ImpactKnockdownImmunitySeconds", 0, 30, ImpactGroup,
            "No new knockdown for (s) after getting up",
            "After a man gets up from a knockdown he cannot be knocked down again for this many seconds.");

        public static readonly ParamDef ImpactDiminishWindowSeconds = Float("ImpactDiminishWindowSeconds", 0, 60, ImpactGroup,
            "Repeat impacts: remembered for (s)",
            "Every stagger, dropped shield or knockdown a man suffered in the last this many seconds makes the next one against him less likely (the factor below). 0 = never.");

        public static readonly ParamDef ImpactDiminishFactor = Float("ImpactDiminishFactor", 0, 1, ImpactGroup,
            "Repeat impacts: chance kept each (x)",
            "Each recent impact multiplies the next chance against the same man by this (0.5: one recent impact halves it, two quarter it). 1 = no limit.");

        public static readonly ParamDef ShieldPickupLockSeconds = Float("ShieldPickupLockSeconds", 0, 120, ImpactGroup,
            "Dropped shield: no pickup for (s)",
            "A dropped shield lies on the ground and can be picked up again the game's way (the AI only when no enemy is within 20 m; you with the use key). Above 0: nobody picks a dropped shield up for this many seconds. 0 = the game's own.");

        public static readonly ParamDef ImpactRiderDismountPercent = Int("ImpactRiderDismountPercent", 0, 100, ImpactGroup,
            "Rider knocked off his horse (% of a knockdown)",
            "A rider is never staggered (the horse shares the move); a \"knockdown\" on a rider knocks him OFF his horse instead, at this percent of the knockdown chance. Horses themselves are the game's own.");

        public static readonly ParamDef ImpactTiredNeverShrugsOff = Bool("ImpactTiredNeverShrugsOff", ImpactGroup,
            "Tired men always flinch",
            "The game lets a man shrug off a light blow without flinching; on: a tired man shrugs off less the more tired he is (his flinch threshold falls with his strength), so a clean hit always tells on an exhausted man.");

        public static readonly ParamDef ImpactMomentumEnabled = Bool("ImpactMomentumEnabled", ImpactGroup,
            "Momentum: a running blow hits harder",
            "Step 52 (Anton: a fresh shock unit charging a tired line must be devastating): the attacker's own speed into the blow - his run or sprint towards the man he strikes - adds to the blow's impact, for kicks, bashes and weapon blows, blocked or not. Riders: their horse's speed, at the rider settings below. Off = only the weapon and the swing count.");

        public static readonly ParamDef ImpactMomentumPercent = Int("ImpactMomentumPercent", 0, 200, ImpactGroup,
            "Momentum: impact added at a full sprint (+%)",
            "A blow struck at the sprint speed below or faster has this much more impact (60: x1.6 - a sprinting axe-man throws a tired man around); at a walk or slower nothing is added, in between a straight line.");

        public static readonly ParamDef ImpactMomentumWalkMetresPerSecond = Float("ImpactMomentumWalkMetresPerSecond", 0, 10, ImpactGroup,
            "Momentum: no bonus up to (m/s)",
            "Up to this speed into the blow (metres a second) a man adds no momentum - the game's walk is about 1.8 m/s, a run 4-5 m/s.");

        public static readonly ParamDef ImpactMomentumSprintMetresPerSecond = Float("ImpactMomentumSprintMetresPerSecond", 0.5, 20, ImpactGroup,
            "Momentum: full bonus from (m/s)",
            "From this speed into the blow (metres a second) the whole momentum bonus is added - a fresh man's sprint (step 33) runs about 7-9 m/s, a run 4-5 m/s.");

        public static readonly ParamDef ImpactMomentumRiderPercent = Int("ImpactMomentumRiderPercent", 0, 200, ImpactGroup,
            "Momentum: a rider's blow at a gallop (+%)",
            "Step 53: a rider's swing or thrust carries his HORSE's speed into the blow - this much more impact at a full gallop (30: x1.3, half a man's sprint, so a cavalry charge into a tired line throws men down without felling everyone); a couched lance never (it is the game's own big hit already). 0 = riders add nothing.");

        public static readonly ParamDef ImpactMomentumRiderWalkMetresPerSecond = Float("ImpactMomentumRiderWalkMetresPerSecond", 0, 20, ImpactGroup,
            "Momentum: a rider adds nothing up to (m/s)",
            "Up to this horse speed into the blow (metres a second) a rider adds no momentum - a horse at a walk; a trot is about 4-5 m/s.");

        public static readonly ParamDef ImpactMomentumRiderGallopMetresPerSecond = Float("ImpactMomentumRiderGallopMetresPerSecond", 0.5, 30, ImpactGroup,
            "Momentum: a rider's full bonus from (m/s)",
            "From this horse speed into the blow (metres a second) a rider adds the whole rider bonus - a good horse gallops at about 9-11 m/s.");

        // Step 53's on-screen lines: their switch ImpactShowMessages is on the player page since step 59.
        public static readonly ParamDef ImpactMessagesIncludeYourMen = Bool("ImpactMessagesIncludeYourMen", ImpactGroup,
            "On-screen lines: your side's heroes too",
            "Off: only what happens to you and what you do. On: also when a man of your side knocks an enemy HERO down or off his horse, or when a hero of your side is knocked down or off his horse.");

        public static readonly ParamDef ImpactMessageMinSeconds = Float("ImpactMessageMinSeconds", 0, 10, ImpactGroup,
            "On-screen lines: at most one every (s)",
            "The shortest time between two of these lines; what happens meanwhile is merged into the next one (\"You knock Aldric down; your shield is knocked away\"). 0 = a line for every tick that has something.");

        public static readonly ParamDef ImpactPunishEnabled = Bool("ImpactPunishEnabled", ImpactGroup,
            "The AI punishes a staggered or downed man",
            "Step 54: when the man an AI soldier fights is reeling from a stagger or lies knocked down (by this impact system - yours too), the soldier presses the opening: he decides to attack as eagerly as the setting below for as long as it lasts (+ a short grace), and a timid man close to him steps in for it. Both sides; never you (you punish with your own hands). Off = the AI ignores the openings.");

        public static readonly ParamDef ImpactPunishAggressionPercent = Int("ImpactPunishAggressionPercent", 0, 200, ImpactGroup,
            "Punish: attack eagerness in an opening (%)",
            "How eagerly an AI soldier decides to attack a reeling or downed enemy, as a share of the game's own AI (100 = the game's full eagerness; the aggression setting's 60% fresh / 20% empty and a fever's 80% are raised to it, never lowered).");

        public static readonly ParamDef ImpactPunishGraceSeconds = Float("ImpactPunishGraceSeconds", 0, 3, ImpactGroup,
            "Punish: grace after the opening (s)",
            "The opening lasts this much longer than the stagger or the time on the ground - a swing started just as he recovers still counts.");

        public static readonly ParamDef ImpactPunishRangeMetres = Float("ImpactPunishRangeMetres", 0, 10, ImpactGroup,
            "Punish: a timid man steps in within (m)",
            "A TIMID soldier (courage) whose enemy reels or lies down within this many metres of him steps in for the opening, like a surge - brave until it closes. Farther away he keeps his distance. 0 = timid men never do.");

        // Step 62 (Anton 2026-10-19: more men fall, and the charge matters) - all on the Advanced page, this group.
        public static readonly ParamDef ImpactShrugOffYieldsToImpact = Bool("ImpactShrugOffYieldsToImpact", ImpactGroup,
            "A decided knockdown or stagger always plays",
            "The game lets a man shrug a light blow off (no reaction at all) BEFORE it asks whether he falls - so a knockdown decided on such a blow was lost. On: the blow is decided first, and a knockdown or stagger refuses the shrug-off, so it plays. Off: the game's shrug-off wins (as before step 62).");

        public static readonly ParamDef ImpactFallBackupSeconds = Float("ImpactFallBackupSeconds", 0, ImpactFallMath.MaxBackupSeconds, ImpactGroup,
            "Knockdown not seen: knock him down after (s)",
            "If a knockdown decided on a landed blow (a kick, a bash, a clean hit) has not started to play within this many seconds, the man is knocked down by a blow of our own - once. 0 = no backup.");

        public static readonly ParamDef ImpactToughnessWeight = Float("ImpactToughnessWeight", 0, 2, ImpactGroup,
            "Toughness: armour and rank count (power)",
            "How much both men's TOUGHNESS - armour, rank and the health left, the courage settings' measure - weighs in who staggers or knocks down whom: the mismatch × (attacker's toughness ÷ defender's) to this power. 0.5: a legionary (4.4 times as tough as a looter) hits as if twice as strong, and the looter's blows on him half as strong. 0 = only tiredness, weapons and skill count.");

        public static readonly ParamDef ImpactRangedInHandPercent = Int("ImpactRangedInHandPercent", 100, 300, ImpactGroup,
            "Archer caught in melee: hit harder (%)",
            "A blow that lands on a man holding a bow, crossbow or sling counts this much stronger - both hands busy, no guard (130: x1.3). 100 = like anyone.");

        public static readonly ParamDef ImpactRearPercent = Int("ImpactRearPercent", 100, 500, ImpactGroup,
            "Hit from behind: counts (%)",
            "A blow that lands on a man from BEHIND counts this much stronger in the strength comparison (200: as if twice as strong). Since step 63 a blow from behind presses him straight instead ('Hit from behind: extra pressure') and this stays at 100 - both together count the direction twice. 100 = like a blow from the front.");

        public static readonly ParamDef ImpactFlankPercent = Int("ImpactFlankPercent", 100, 500, ImpactGroup,
            "Hit from the side: counts (%)",
            "A blow that lands on a man from the SIDE counts this much stronger in the strength comparison (140: x1.4). Since step 63 'Hit from the side: extra pressure' carries it and this stays at 100. 100 = like a blow from the front.");

        public static readonly ParamDef ImpactRearDegrees = Int("ImpactRearDegrees", 90, 180, ImpactGroup,
            "From behind: beyond (degrees)",
            "A blow counts as from behind when the attacker stands more than this many degrees from where the man faces (120: the back third of a circle).");

        public static readonly ParamDef ImpactFlankDegrees = Int("ImpactFlankDegrees", 0, 180, ImpactGroup,
            "From the side: from (degrees)",
            "A blow counts as from the side from this many degrees off his facing up to the 'from behind' angle (60: between 60 and 120). Never above the 'from behind' angle.");

        // Step 63 (Anton's battle 2026-10-19 11:02: from behind 13% down vs the front 11%) - a strong rear blow, on the Advanced page, this group.
        public static readonly ParamDef ImpactRearPressure = Float("ImpactRearPressure", 0, 2, ImpactGroup,
            "Hit from behind: extra pressure",
            "A blow that lands on a man from BEHIND presses him this much more toward a stagger or a fall, the way tiredness does (an empty man counts 1). 0.5: a fresh man struck in the back reels like a half-exhausted one, and a blow from behind can floor even a fresh man. 0 = like a blow from the front.");

        public static readonly ParamDef ImpactFlankPressure = Float("ImpactFlankPressure", 0, 2, ImpactGroup,
            "Hit from the side: extra pressure",
            "A blow that lands on a man from the SIDE presses him this much more toward a stagger or a fall (0.2: a fresh man struck from the side reels a little more often). 0 = like a blow from the front.");

        public static readonly ParamDef ImpactChargeCarrySeconds = Float("ImpactChargeCarrySeconds", 0, 5, ImpactGroup,
            "Charge: a sprint carries into the first blow for (s)",
            "A man who was sprinting strikes his first blow within this many seconds after the sprint as a CHARGE - its full momentum (and the full damage roll, if that switch is on), even though he slowed down to swing. An attack he BEGAN while sprinting or within this time stays a charge until it ends, however long the swing. Only the first blow; while he still sprints, every blow. 0 = only a blow struck while sprinting (or an attack begun while sprinting).");

        public static readonly ParamDef RiposteKnockdownMaxPercent = Int("RiposteKnockdownMaxPercent", 0, 100, ImpactGroup,
            "Charge into a good shield block: thrown back, at most (%)",
            "A CHARGING man (sprinting into the blow) whose blow is blocked on the RIGHT side of a shield can be thrown to the ground - at most this chance, for a full sprint by a weaker or tired man into a fresh, tougher shieldman; nothing for a walking attacker (he only reels). You too. 0 = never.");

        public static readonly ParamDef RiposteKnockdownFrom = Float("RiposteKnockdownFrom", 0, 0.95, ImpactGroup,
            "Thrown back: starts at pressure",
            "Where the thrown-back chance starts on the pressure scale of the riposte (0 = any mismatch against the charger counts, 1 = an empty, outmatched attacker). The other curves: a stagger from 0, a knockdown from 0.35.");

        public static readonly ParamDef RiposteMomentumPercent = Int("RiposteMomentumPercent", 0, 300, ImpactGroup,
            "Charge into a good block: his momentum against him (+%)",
            "On a right-side block or a parry, the attacker's own speed into the blow is turned against him - the riposte (he reels, or is thrown back) counts this much more at a full sprint (100: twice). 0 = his speed does not count on a correct block.");

        public static readonly ParamDef ImpactMultiplierCapPercent = Int("ImpactMultiplierCapPercent", 100, 2000, ImpactGroup,
            "Charge, toughness, direction together: at most (%)",
            "The momentum of a charge, the toughness difference, a blow from behind and an archer caught in melee multiply each other - together they never count more than this (800: x8), nor less than its inverse, so no stack of them becomes a cheat.");

        // Step 65 (Anton 2026-10-19: "make the AI do a few shield bashes and kicks now and then - the game has it but the NPCs never use them";
        // "a looter kicking a heavy shielded troop shouldn't do too much - maybe now and then a lucky kick staggers him for a fast bit"):
        // AI kicks and bashes through step 16's input hook; the kicked man BRACES (the impact model's pressure decides it).
        public static readonly ParamDef AiKicksEnabled = Bool("AiKicksEnabled", ImpactGroup,
            "AI soldiers kick and shield-bash",
            "On: now and then an AI soldier on foot in melee with an enemy kicks him (no shield in hand) or bashes him with his shield - more often against a man hiding behind his guard, in the fever of a charge and for a skilled man, less as he tires. Each costs Athletics and a stored swing, like yours. Off: the game's AI, which never kicks.");

        public static readonly ParamDef AiKicksPerMinute = Float("AiKicksPerMinute", 0, 20, ImpactGroup,
            "AI kicks: chance per man per minute",
            "How often an AI soldier in melee contact kicks or bashes, per minute of contact, before the reasons below raise or lower it (0.1: one a man every ten minutes of fighting - a few a minute across a 50 against 50 clash). 0 = never.");

        public static readonly ParamDef AiKickReachMetres = Float("AiKickReachMetres", 0.5, 4, ImpactGroup,
            "AI kicks: reach (m)",
            "An AI soldier kicks or bashes only the enemy he fights, and only within this many metres (a kick reaches about a pace).");

        public static readonly ParamDef AiKickFacingDegrees = Int("AiKickFacingDegrees", 0, 180, ImpactGroup,
            "AI kicks: the enemy in front within (degrees)",
            "The enemy must stand within this many degrees of where the AI soldier faces - a kick goes forward.");

        public static readonly ParamDef AiKickCooldownSeconds = Float("AiKickCooldownSeconds", 0, 60, ImpactGroup,
            "AI kicks: at least this long between two (s)",
            "After a kick or bash an AI soldier waits at least this many seconds before the next.");

        public static readonly ParamDef AiKickTurtleMultiplier = Float("AiKickTurtleMultiplier", 1, 10, ImpactGroup,
            "AI kicks: x against a man behind his guard",
            "The chance is this many times higher against an enemy who keeps blocking - his guard up now and his blocks of the last seconds (below) - a kick is the answer to a turtle. 1 = no difference.");

        public static readonly ParamDef AiKickTurtleSeconds = Float("AiKickTurtleSeconds", 0.5, 15, ImpactGroup,
            "AI kicks: blocks remembered for (s)",
            "How long an enemy's blocks count toward 'he keeps blocking' (they fade over this time).");

        public static readonly ParamDef AiKickTurtleBlocks = Float("AiKickTurtleBlocks", 0.5, 10, ImpactGroup,
            "AI kicks: blocks that make a turtle",
            "How many recent blocks (his guard up right now counts as one) make an enemy a full turtle - the multiplier above in full; fewer give part of it.");

        public static readonly ParamDef AiKickFeverMultiplier = Float("AiKickFeverMultiplier", 1, 10, ImpactGroup,
            "AI kicks: x in the fever of a charge",
            "The chance is this many times higher while the AI soldier is in the fever of a charge (Aggression and fever). 1 = no difference.");

        public static readonly ParamDef AiKickSkillLowPercent = Int("AiKickSkillLowPercent", 0, 500, ImpactGroup,
            "AI kicks: x at Athletics skill 0 (%)",
            "The chance for a man with no Athletics skill, in % of the base (50: half). It rises in a straight line to the next setting at skill 300 - a veteran kicks more than a recruit.");

        public static readonly ParamDef AiKickSkillHighPercent = Int("AiKickSkillHighPercent", 0, 500, ImpactGroup,
            "AI kicks: x at Athletics skill 300 (%)",
            "The chance for a man with 300 Athletics skill, in % of the base (200: twice).");

        public static readonly ParamDef AiKickMinBarPercent = Int("AiKickMinBarPercent", 0, 100, ImpactGroup,
            "AI kicks: none below this Athletics (%)",
            "Below this much of his Athletics bar an AI soldier never kicks or bashes - a tired man saves his breath. Between this and the next setting the chance rises in a straight line.");

        public static readonly ParamDef AiKickFullBarPercent = Int("AiKickFullBarPercent", 0, 100, ImpactGroup,
            "AI kicks: full chance from this Athletics (%)",
            "At or above this much of his Athletics bar an AI soldier kicks at the full chance.");

        public static readonly ParamDef AiKickKicksARaisedShield = Bool("AiKickKicksARaisedShield", ImpactGroup,
            "AI kicks: a raised shield is kicked, not bashed",
            "On: an AI shieldman whose enemy holds his own shield up toward him KICKS instead of bashing (a raised shield stops a bash, not a kick). Off: a man with a shield always bashes.");

        public static readonly ParamDef KickBraceEnabled = Bool("KickBraceEnabled", ImpactGroup,
            "The kicked man braces",
            "On: a fresh, well-armoured, skilled man mostly braces against a kick or shield bash - no knock-back, his guard kept; a lucky kick gives him only a short stagger. The brace fails more often as he tires and the stronger the kicker (his own strength, rank, Athletics, a heavy shield for a bash); a man near the end of his Athletics never braces. You and the AI alike. Off: every kick that lands knocks a man back, as in the game.");

        public static readonly ParamDef KickBraceFromPressure = Float("KickBraceFromPressure", -3, 2, ImpactGroup,
            "Brace: fails from this pressure",
            "The brace fails more often the more pressure the kick puts on him - the same pressure that decides staggers and knockdowns (his tiredness, the strength, rank and skill of both men, a blow from behind). Below this pressure he always braces; the failure chance grows along a smooth curve to the next setting. -0.7: a looter's kick on a fresh legionary fails about one time in eight.");

        public static readonly ParamDef KickBraceToPressure = Float("KickBraceToPressure", -2, 3, ImpactGroup,
            "Brace: always fails from this pressure",
            "At or above this pressure the brace always fails (1: an empty man, or a much stronger kicker on a tired one).");

        public static readonly ParamDef KickBraceMinBarPercent = Int("KickBraceMinBarPercent", 0, 100, ImpactGroup,
            "Brace: never below this Athletics (%)",
            "A man with less than this much of his Athletics bar left never braces - a spent man takes the kick in full (knocked back, and the staggers and knockdowns of a tired man).");

        public static readonly ParamDef KickBraceReelPercent = Int("KickBraceReelPercent", 5, 100, ImpactGroup,
            "Brace: what is left of the knock-back (%)",
            "A braced man's reaction to the kick plays this fast: what is left of its length, in % (20: a fifth - barely a twitch). The fastest the game is asked to play anything is 8 times its speed.");

        public static readonly ParamDef KickShortReelPercent = Int("KickShortReelPercent", 5, 100, ImpactGroup,
            "Brace failed: a fresh man's reel (%)",
            "When the brace fails on a FRESH man he reels for only this much of the game's knock-back (40: a short stagger, 'a fast bit'); the more tired he is the closer to the full knock-back - and a tired man's lengthened reel - it gets.");

        public static readonly ParamDef KickBraceRefusesKnockBack = Bool("KickBraceRefusesKnockBack", ImpactGroup,
            "Brace: no knock-back at all",
            "On: a braced man is not knocked back at all (the game's knock-back is refused for that kick, and whatever reaction is left plays fast). Off: the knock-back plays, only cut short.");

        // Step 66 (Anton's looter battle 2026-10-19 14:16 - no AI kick seen, asked 0 in 49 man-minutes of contact: "not a bad idea for men to kick
        // and bash more - a defensive supplement to all that killing, disturbing"; "and shield bashes, maybe make them as frequent as swings when the
        // man has a shield"): a kick waits for reach; a shieldman's swings earn his bashes.
        public static readonly ParamDef AiKickWaitSeconds = Float("AiKickWaitSeconds", 0, 10, ImpactGroup,
            "AI kicks: wait this long to get in reach (s)",
            "When an AI soldier decides to kick or bash while the enemy he fights is close (next setting) but not yet in reach or not in front, he keeps the wish this many seconds and kicks the moment the enemy is in reach. 0 = only an enemy already in reach.");

        public static readonly ParamDef AiKickWaitMetres = Float("AiKickWaitMetres", 1, 10, ImpactGroup,
            "AI kicks: close enough to wait for (m)",
            "The enemy he fights must stand within this many metres for the AI soldier to wait for reach (above) - farther away the wish is dropped.");

        public static readonly ParamDef AiBashShareOfSwingsPercent = Int("AiBashShareOfSwingsPercent", 0, 300, ImpactGroup,
            "AI shield bashes: per swing (%)",
            "An AI soldier with a shield bashes about this many times per 100 of his own melee swings (100: one bash per swing - as often as he swings). Each of his swings earns this share of a bash, spent soon after when the enemy is in reach. A raised shield facing him is kicked instead. Men without a shield kick at the rate per minute above. 0 = shieldmen never bash.");

        public static readonly ParamDef AiBashDelaySeconds = Float("AiBashDelaySeconds", 0.1, 10, ImpactGroup,
            "AI shield bashes: after his swing within about (s)",
            "On average this many seconds after the swing that earned it the AI soldier bashes (at a full Athletics bar; longer as he tires, never below the 'none below' Athletics). If he swings again first the bash waits - at most two are saved up.");

        public static readonly ParamDef AiBashCooldownSeconds = Float("AiBashCooldownSeconds", 0, 60, ImpactGroup,
            "AI shield bashes: at least this long between two (s)",
            "After a shield bash an AI soldier waits at least this many seconds before his next kick or bash (kept short, so bashes can keep up with his swings).");

        // Step 68 (Toni's 100 v 100 of 2026-10-19: 92 AI kicks / bashes landed "on the man" but the impact model decided 33 - a kick or bash that lands
        // on a FRIEND is cancelled by the game and never decided: a rear-rank man kicking the enemy past his own front-rank mate). AiKickMath.FriendInPath.
        public static readonly ParamDef AiKickFriendClearMetres = Float("AiKickFriendClearMetres", 0, 2, ImpactGroup,
            "AI kicks: not through a friend (m)",
            "An AI soldier does not kick or bash the enemy he fights while one of his own side stands between them within this many metres of the line from him to that enemy (his kick would land on his mate) - he waits for a clear line instead. 0 = off.");

        public static readonly ParamDef ImpactMismatchWeight = Float("ImpactMismatchWeight", 0, 2, ImpactGroup,
            "Mismatch weight (pressure per doubling)",
            "How much an outmatched man suffers: every doubling of the mismatch beyond the even band adds this to the pressure on him (0.25: an enemy four times stronger counts as much as being about a third tired). 0 = only tiredness counts.");

        public static readonly ParamDef ImpactEvenBandDoublings = Float("ImpactEvenBandDoublings", 0, 3, ImpactGroup,
            "Even match band (doublings)",
            "A mismatch within this many doublings either way counts as an even match (0.5 = a factor of 1.41) - so two fresh men of a kind never stagger each other. Only the part beyond it adds pressure (or takes pressure away, for the weaker attacker).");

        public static readonly ParamDef ImpactBodyExponent = Float("ImpactBodyExponent", 0, 2, ImpactGroup,
            "Body strength from Athletics points (exponent)",
            "Body strength = his CURRENT Athletics points to this power (0.5: four times the points = twice the body). A 200-point king at full strength against a 50-point man at 20% is 4.5 times the body. 0 = Athletics points do not count, only tiredness.");

        public static readonly ParamDef ImpactBodyFloorPoints = Int("ImpactBodyFloorPoints", 1, 200, ImpactGroup,
            "Body strength floor (points)",
            "An empty man still has a body: his body strength never counts fewer points than this.");

        public static readonly ParamDef ImpactStaggeredBonus = Float("ImpactStaggeredBonus", 0, 3, ImpactGroup,
            "Pressure on a reeling man (+)",
            "A man still reeling from a stagger or a lengthened reel of this system gets this much extra pressure: a kick very likely knocks him down, even a weapon blow can. A fresh man's ordinary flinch does not count.");

        public static readonly ParamDef ImpactDamageWeight = Float("ImpactDamageWeight", 0, 5, ImpactGroup,
            "Damage dealt adds pressure (x)",
            "A blow that lands adds this times the share of his full health it took (1: a blow that takes 30% of his health adds 0.3). 0 = the damage does not count (waiting for Anton's answer to IMPACT §8 question 1).");

        public static readonly ParamDef ImpactRefWeightKg = Float("ImpactRefWeightKg", 0.1, 10, ImpactGroup,
            "Reference weapon weight (kg)",
            "A weapon (or shield) of this weight counts 1 in the impact and guard numbers; heavier counts more by the exponent below.");

        public static readonly ParamDef ImpactWeightExponent = Float("ImpactWeightExponent", 0, 2, ImpactGroup,
            "Weight's part (exponent)",
            "Impact and guard grow with (weight ÷ reference weight) to this power (0.5: a 6 kg shield guards twice as well as a 1.5 kg one). 0 = weight does not count.");

        public static readonly ParamDef ImpactRefSwingSpeed = Float("ImpactRefSwingSpeed", 10, 200, ImpactGroup,
            "Reference swing speed",
            "A swing (or thrust) of this speed - the weapon's own number, slowed by a tired man's slower swing - counts 1 in the impact; faster counts more by the exponent below.");

        public static readonly ParamDef ImpactSpeedExponent = Float("ImpactSpeedExponent", 0, 3, ImpactGroup,
            "Swing speed's part (exponent)",
            "The impact grows with (swing speed ÷ reference speed) to this power. 0 = speed does not count.");

        public static readonly ParamDef ImpactBluntFactor = Float("ImpactBluntFactor", 0.1, 3, ImpactGroup,
            "Blunt weapons' impact (x)",
            "Maces, hammers, clubs and other blunt blows hit this many times harder in the impact numbers.");

        public static readonly ParamDef ImpactCutFactor = Float("ImpactCutFactor", 0.1, 3, ImpactGroup,
            "Cutting weapons' impact (x)",
            "Swords, axes and other cutting blows count this many times in the impact numbers.");

        public static readonly ParamDef ImpactPierceFactor = Float("ImpactPierceFactor", 0.1, 3, ImpactGroup,
            "Piercing weapons' impact (x)",
            "Thrusts and other piercing blows count this many times in the impact numbers (a point pushes less than an edge or a head).");

        public static readonly ParamDef ImpactKickFactor = Float("ImpactKickFactor", 0, 5, ImpactGroup,
            "Kick's impact",
            "A kick's impact number (a 1.2 kg sword's swing is about 0.9).");

        public static readonly ParamDef ImpactBashFactor = Float("ImpactBashFactor", 0, 5, ImpactGroup,
            "Shield bash's impact (x)",
            "A shield bash's impact = the shield's weight number times this.");

        public static readonly ParamDef ImpactSkillMinFactor = Float("ImpactSkillMinFactor", 0.1, 2, ImpactGroup,
            "Skill 0 counts (x)",
            "The weapon skill of the man who strikes (Athletics for a kick, one-handed for a bash) and of the man who guards (one-handed for a shield, his weapon's skill for a parry, Athletics for no guard) scale his side: skill 0 counts this much, rising in a straight line to the next value at skill 300.");

        public static readonly ParamDef ImpactSkillMaxFactor = Float("ImpactSkillMaxFactor", 0.1, 3, ImpactGroup,
            "Skill 300 counts (x)",
            "What skill 300 (and above) counts: a recruit (20) 0.78, a veteran (200) 1.08, a 300 lord 1.25 with the defaults.");

        public static readonly ParamDef ImpactGuardShieldRight = Float("ImpactGuardShieldRight", 0.05, 5, ImpactGroup,
            "Guard: shield on the right side (x)",
            "How well a shield held on the right side takes a blow, times the shield's weight number (a 4 kg shield 1.63).");

        public static readonly ParamDef ImpactGuardParry = Float("ImpactGuardParry", 0.05, 5, ImpactGroup,
            "Guard: weapon parry (x)",
            "How well a parrying weapon takes a blow, times its weight number.");

        public static readonly ParamDef ImpactGuardShieldWrong = Float("ImpactGuardShieldWrong", 0.05, 5, ImpactGroup,
            "Guard: shield on the wrong side (x)",
            "How well a shield held on the WRONG side takes a blow, times the shield's weight number - a fraction of the right side's.");

        public static readonly ParamDef ImpactGuardNone = Float("ImpactGuardNone", 0.05, 5, ImpactGroup,
            "Guard: none (his body)",
            "The guard of a man hit clean or kicked: his body only.");

        public static readonly ParamDef ImpactStaggerFrom = Float("ImpactStaggerFrom", 0, 0.95, ImpactGroup,
            "Stagger starts at pressure",
            "A stagger's chance starts at this pressure and climbs along an S-curve to its ceiling at a pressure of 1 (an empty even man).");

        public static readonly ParamDef ImpactShieldDropFrom = Float("ImpactShieldDropFrom", 0, 0.95, ImpactGroup,
            "Shield drop starts at pressure",
            "A dropped shield needs at least this much pressure before it can happen at all.");

        public static readonly ParamDef ImpactKnockdownFrom = Float("ImpactKnockdownFrom", 0, 0.95, ImpactGroup,
            "Knockdown starts at pressure",
            "A knockdown needs at least this much pressure before it can happen at all - the worse the outcome, the more pressure it needs.");

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

        // ------------------------------------------------------------------ your Athletics bar (step 6; its switch ShowPlayerBar on the player page since step 59)

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

        // Step 58 (Anton, 2026-10-18: "this ALT view is too cluttered. Maybe add those lines as some averages on the group") - the formation's
        // FEVER share on its label, under "ready 34/50" (AI_NOTES "Step 58").
        public static readonly ParamDef ShowFeverOnLabels = Bool("ShowFeverOnLabels", AltMarkersGroup,
            "Fever share under the formation markers",
            "Each formation's numbers under its marker also say how many of its men are in a FEVER (the surge after a charge or advance order), \"fever 12/40\" in orange, under the ready count - the group's surge at a glance. Shown only while at least one of its men is in a fever.");

        // Step 44 (Anton, 2026-10-17): "which soldiers are in FEVER" while the game shows its ALT icons - our own small mark above each
        // man in a fever (FeverMarkView, AI_NOTES "Step 44"), shown exactly with the formation markers above.
        public static readonly ParamDef ShowFeverMarks = Bool("ShowFeverMarks", AltMarkersGroup,
            "Fever marks above the men",
            "While the game shows its markers (you hold ALT, or the orders menu is open), a small bar above each man in a FEVER (the surge after a charge or advance order - Aggression and fever): bright orange while he still runs at the enemy, fading through yellow as the fever burns out after the first clash, gone when it is over. Its length is the fever left. Calm men get nothing; you never have a fever.");

        public static readonly ParamDef FeverMarksShowEnemy = Bool("FeverMarksShowEnemy", AltMarkersGroup,
            "Enemy fever marks too",
            "The enemy's men in a fever get the mark too - see their charge coming and when its surge is spent. Off: yours and your allies' only.");

        // Step 46 (Anton, 2026-10-17): "how tired they are in a bar above that fever bar, a green bar that gets to red" - every man's own
        // Athletics bar under ALT, drawn by the same view as the fever bar (one projection per man, one pool, one cap - AI_NOTES "Step 46").
        public static readonly ParamDef ShowAthleticsMarks = Bool("ShowAthleticsMarks", AltMarkersGroup,
            "Athletics bars above the men",
            "While the game shows its markers (you hold ALT, or the orders menu is open), a small Athletics bar above EVERY soldier's head - how tired he is: its length is his Athletics left, its colour the bar's own (green at full strength, blue just below, then yellow, orange, red), the part his wounds hold dark red. It sits just above his fever bar, at the same height whether he is in a fever or not. You have your own bar.");

        public static readonly ParamDef AthleticsMarksShowEnemy = Bool("AthleticsMarksShowEnemy", AltMarkersGroup,
            "Enemy Athletics bars too",
            "The enemy's soldiers get their Athletics bar too - see who is spent before you charge. Off: yours and your allies' only.");

        // Step 58 (Anton, 2026-10-18: "only show them on up to 5 soldiers that I'm directly looking at and are really close, making it work in
        // tournaments too") - the per-man bars on the few men nearest the screen's centre and close to the camera (AI_NOTES "Step 58").
        // Its switch AltMarksEveryone is on the player page since step 59.
        public static readonly ParamDef AltNearMarksMax = Int("AltNearMarksMax", 1, 50, AltMarkersGroup,
            "Bars on the men you look at: how many",
            "How many men get their bars at most - the ones nearest the centre of the screen (where you look), among the men close to the camera. In a tournament or a duel this is your opponent.");

        public static readonly ParamDef AltNearMarksMetres = Float("AltNearMarksMetres", 2, 100, AltMarkersGroup,
            "Bars on the men you look at: how close (m)",
            "Only men at most this far from the camera get their bars, in metres. The camera sits a few metres behind you, so 15 m is about the men in front of you and the next rank.");

        public static readonly ParamDef FeverMarkMax = Int("FeverMarkMax", 1, 1000, AltMarkersGroup,
            "Most men marked at once",
            "At most this many men get their bars (Athletics and fever) in one frame - with bars on every man, the men nearest to the camera first; it also caps the men you look at. A big battle has hundreds of men on the screen; fewer cost less and clutter less.");

        // Step 67 (Anton, 2026-10-19: "give me eyes on their morale too: ALT to see the average morale of the whole group, and the same 5
        // closest guys' morale too") - a morale bar under the men's bars, the unit's morale and freshness on the near formations' labels.
        public static readonly ParamDef ShowMoraleMarks = Bool("ShowMoraleMarks", AltMarkersGroup,
            "Morale bars above the men",
            "While you hold ALT, a small BLUE bar under the Athletics and fever bars of the men who get them: his morale (the game's own, 0-100 - full = 100). The enemy's follow \"Enemy Athletics bars too\". No bar where the game keeps no morale (tournaments, arenas).");

        public static readonly ParamDef ShowMoraleOnLabels = Bool("ShowMoraleOnLabels", AltMarkersGroup,
            "Morale and freshness under the near formations",
            "The formations nearest you also say \"morale 64, fresh 41%\" in blue under their marker: their men's average morale and average Athletics.");

        public static readonly ParamDef MoraleLabelMetres = Float("MoraleLabelMetres", 5, 500, AltMarkersGroup,
            "Formation morale: within (m)",
            "Only formations at most this far away (the distance their marker shows) get the morale line.");

        public static readonly ParamDef MoraleLabelMax = Int("MoraleLabelMax", 1, 16, AltMarkersGroup,
            "Formation morale: at most (formations)",
            "At most this many formations - the nearest - get the morale line, so the screen stays uncluttered.");

        // ------------------------------------------------------------------ orders: no accidental charges (step 72)
        // Toni (2026-10-19): placing a unit with a click or a drag, the cursor crossing another unit's floating banner turned the move into a
        // charge at that unit - his men sprinted off and wasted their sprint; "the unit only goes after another unit on charge when my cursor is
        // directly over their icon and when I click it, not click and drag". Core OrderClickMath / OrderClickTracker, AI_NOTES "Step 72".

        public static readonly ParamDef OrderNoAccidentalCharge = Bool("OrderNoAccidentalCharge", OrderClickGroup,
            "No accidental charge orders",
            "In the orders menu, a left click charges an enemy unit only when the cursor is ON its banner icon - when you press AND when you let go - and you did not drag. A drag (placing a unit with its frontage) or a click near a banner or over the men places the move order instead, so your men never sprint off at a unit you only swept across. The F-key orders and the gamepad are the game's own. Off: the game's own rule - letting go of the button anywhere near an enemy banner (or close to an enemy unit) charges it.");

        public static readonly ParamDef OrderChargeDragPixels = Int("OrderChargeDragPixels", 1, 200, OrderClickGroup,
            "Charge click: a drag from (px)",
            "With \"No accidental charge orders\" on: the cursor this far from where you pressed the button (the ground point you pressed on, as the camera turns) makes the click a DRAG - it never charges. In UI pixels of a 1080-pixel-high screen, scaled to yours. The game's own click tolerance is 10.");

        public static readonly ParamDef OrderChargeIconPixels = Int("OrderChargeIconPixels", 1, 200, OrderClickGroup,
            "Charge click: on the banner within (px)",
            "With \"No accidental charge orders\" on: the cursor within this distance of an enemy unit's banner icon counts as ON it - a click there charges the unit. In UI pixels of a 1080-pixel-high screen, scaled to yours; the icon is 50 across. The game's own reach is 70 around the screen centre, so more than 70 changes nothing.");

        // ------------------------------------------------------------------ line discipline by tier (step 73)
        // Toni (2026-10-20): 60 tier-5 men against 120 tier-1 recruits - the recruits attacked on a very broad front about 2 deep, wider than
        // his army, and engulfed him; "a low tier untrained troop cannot keep that disciplined line all the way and execute that encircling".
        // Core LineDisciplineMath (D = the men's mean ranks, at most ceil(men / D) files), AI_NOTES "Step 73", DESIGN decision 67.

        public static readonly ParamDef LineDisciplineEnabled = Bool("LineDisciplineEnabled", LineDisciplineGroup,
            "Line discipline by tier",
            "A foot formation (infantry, and archers / skirmishers - see below) in a line, shield wall, loose or scatter arrangement stretches no thinner than its men can hold: each man's tier gives his minimum ranks (the next seven settings), the formation's minimum depth D is their average, recomputed as men fall or move, and its line is at most men / D files wide (rounded up - 3.5 deep gives a 4th rank about half full). Whenever the game or its AI makes a line wider (its wide form orders, an envelop, the line spreading) or losses make it too thin, it is narrowed back at once, before the men move. Cavalry, horse archers, squares, circles, columns and wedges are left alone; naval battles never. Your own formations follow \"Line discipline: your units too\" (the player page, How the AI fights). Off: the game's own widths.");

        public static readonly ParamDef LineDisciplineRanksT0 = Float("LineDisciplineRanksT0", LineDisciplineMath.MinRanks, LineDisciplineMath.MaxRanks, LineDisciplineGroup,
            "Tier 0: minimum ranks",
            "The fewest ranks a tier-0 man (a peasant, a looter, a villager) can hold in a line. The formation's minimum depth is the average over its men. 1 = no limit of his own.");

        public static readonly ParamDef LineDisciplineRanksT1 = Float("LineDisciplineRanksT1", LineDisciplineMath.MinRanks, LineDisciplineMath.MaxRanks, LineDisciplineGroup,
            "Tier 1: minimum ranks",
            "The fewest ranks a tier-1 man (a recruit) can hold. 120 recruits at 6 make a line at most 20 men wide.");

        public static readonly ParamDef LineDisciplineRanksT2 = Float("LineDisciplineRanksT2", LineDisciplineMath.MinRanks, LineDisciplineMath.MaxRanks, LineDisciplineGroup,
            "Tier 2: minimum ranks",
            "The fewest ranks a tier-2 man can hold.");

        public static readonly ParamDef LineDisciplineRanksT3 = Float("LineDisciplineRanksT3", LineDisciplineMath.MinRanks, LineDisciplineMath.MaxRanks, LineDisciplineGroup,
            "Tier 3: minimum ranks",
            "The fewest ranks a tier-3 man can hold.");

        public static readonly ParamDef LineDisciplineRanksT4 = Float("LineDisciplineRanksT4", LineDisciplineMath.MinRanks, LineDisciplineMath.MaxRanks, LineDisciplineGroup,
            "Tier 4: minimum ranks",
            "The fewest ranks a tier-4 man can hold.");

        public static readonly ParamDef LineDisciplineRanksT5 = Float("LineDisciplineRanksT5", LineDisciplineMath.MinRanks, LineDisciplineMath.MaxRanks, LineDisciplineGroup,
            "Tier 5: minimum ranks",
            "The fewest ranks a tier-5 man (a veteran) can hold. At 1 a formation of them is never narrowed.");

        public static readonly ParamDef LineDisciplineRanksT6 = Float("LineDisciplineRanksT6", LineDisciplineMath.MinRanks, LineDisciplineMath.MaxRanks, LineDisciplineGroup,
            "Tier 6: minimum ranks",
            "The fewest ranks a tier-6 man (an elite) can hold - also any higher tier a mod adds.");

        public static readonly ParamDef LineDisciplineHeroTier = Int("LineDisciplineHeroTier", 0, LineDisciplineMath.MaxTier, LineDisciplineGroup,
            "Heroes count as tier",
            "A hero in a formation - a lord, a companion, you - counts as a man of this tier (whatever his level): 6 = an elite, he holds his place.");

        public static readonly ParamDef LineDisciplineRangedToo = Bool("LineDisciplineRangedToo", LineDisciplineGroup,
            "Archers and skirmishers too",
            "On: archer and skirmisher formations keep the same minimum depth as infantry. Off: only infantry formations are held to it.");

        public static readonly ParamDef LineDisciplineInSieges = Bool("LineDisciplineInSieges", LineDisciplineGroup,
            "In sieges too",
            "On: sieges and sally-outs too. Off: there the walls, gates and streets set the width, as in the game.");

        public static readonly ParamDef LineDisciplinePreviewCap = Bool("LineDisciplinePreviewCap", LineDisciplineGroup,
            "Your order drag stops at the limit",
            "With your units under the line discipline: your formation tells the game its own maximum width is the limit, so when you drag its line in the orders menu the preview stops growing there and the order keeps to it. Off: the drag shows any width and the formation is narrowed right after the order.");

        public static readonly ParamDef LineDisciplineShowMessage = Bool("LineDisciplineShowMessage", LineDisciplineGroup,
            "Say so on screen",
            "A short line on screen when your line order asked wider than your men can hold, e.g. \"Infantry: these men hold no thinner than 5 deep - 24 men wide at most\".");

        public static readonly ParamDef LineDisciplinePollSeconds = Float("LineDisciplinePollSeconds", LineDisciplineMath.MinPollSeconds, LineDisciplineMath.MaxPollSeconds, LineDisciplineGroup,
            "Recount the depth every (s)",
            "How often each formation's minimum depth is recounted from its men's tiers; at once anyway when men fall, join or leave it. The width itself is checked every frame.");

        // step 74: THE BLOCK HOLD - a formation of low line discipline keeps its block in a charge and the last metres of an advance (Toni's battle
        // 2026-10-20 20:30). Core BlockHoldMath, AI_NOTES "Step 74", DESIGN decision 68.

        public static readonly ParamDef LineBlockHoldEnabled = Bool("LineBlockHoldEnabled", LineDisciplineGroup,
            "The block holds into contact",
            "A formation held to a minimum depth here (green troops - the depth above 1) keeps its BLOCK when it charges and in the last metres of an advance: each man stays within the block's width (its men at the limit above x their spacing) across its front and within its depth, around its centre line toward the enemy. A man who runs round the flank, trails along the edge, runs ahead or straggles is walked back by his own input, facing the enemy - the block still goes into contact at its own pace, and its front men fight. Elites (a depth of 1) are free. Both sides; your units by \"Line discipline: your units too\" (the player page). Off: the game's own charge - every man runs at his own target.");

        public static readonly ParamDef LineBlockSlackMetres = Float("LineBlockSlackMetres", 0, 20, LineDisciplineGroup,
            "Block: room past its width (m)",
            "How far a man may stand past the block's half-width across its front (and behind its rear) before he is walked back - at the full hold (a depth of 3 and more).");

        public static readonly ParamDef LineBlockFullAtDepth = Float("LineBlockFullAtDepth", 1.5, 12, LineDisciplineGroup,
            "Block: full hold from depth",
            "The minimum depth at which the hold is full; between 1 (free) and this the block is looser (see the next setting). 3: recruits and tier-2 / tier-3 men are held fully, tier-4 men (3) fully, veterans (2) half.");

        public static readonly ParamDef LineBlockLooseMetres = Float("LineBlockLooseMetres", 0, 60, LineDisciplineGroup,
            "Block: extra room when barely held (m)",
            "The extra room of a block that is only just held (a depth just above 1), shrinking to 0 at the full-hold depth: a looser block for better troops.");

        public static readonly ParamDef LineBlockRankMetres = Float("LineBlockRankMetres", 0.5, 3, LineDisciplineGroup,
            "Block: one rank deep (m)",
            "The depth of one rank of the block: it is ranks x this deep (its men ÷ its files).");

        public static readonly ParamDef LineBlockAheadMetres = Float("LineBlockAheadMetres", 0, 20, LineDisciplineGroup,
            "Block: run ahead at most (m)",
            "How far a man may run ahead of the block's front with no enemy within reach before he is held back for the block to come up.");

        public static readonly ParamDef LineBlockStrikeReachMetres = Float("LineBlockStrikeReachMetres", 0, 6, LineDisciplineGroup,
            "Block: free to fight within (m)",
            "A man with an enemy this close fights as the game has him - unless he stands more than the next setting beyond the block. 0 = never free.");

        public static readonly ParamDef LineBlockStrikeLeashMetres = Float("LineBlockStrikeLeashMetres", 0, 10, LineDisciplineGroup,
            "Block: a fighter beyond it at most (m)",
            "A fighting man is left to fight while he stands no more than this beyond the block's room; farther out (round the flank, behind the enemy) he is walked back.");

        public static readonly ParamDef LineBlockNearMetres = Float("LineBlockNearMetres", 0, 200, LineDisciplineGroup,
            "Block: hold an advance within (m)",
            "An advance, move or hold is held as a block once the enemy's front is this close (a charge always).");

        public static readonly ParamDef LineBlockLeadMetres = Float("LineBlockLeadMetres", 0, 6, LineDisciplineGroup,
            "Block: a walked-back man leads it by (m)",
            "A man walked back aims this far ahead of his place while the block advances, so he keeps pace with it instead of stopping.");

        public static readonly ParamDef LineBlockMopUpWinPercent = Float("LineBlockMopUpWinPercent", 0, 100, LineDisciplineGroup,
            "Block: let go when winning (%)",
            "The block lets its men go when they win this much (the fear flock's win: 100% = 2 : 1 local odds or a broken enemy) - they chase the beaten.");

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

        // The fever marks (step 44) - UI pixels of the 1080p layout (the game's UI scale applies), and the height above the man.
        public static readonly ParamDef FeverMarkWidth = Int("FeverMarkWidth", 4, 100, AdvancedGroup,
            "Fever mark: length (px)",
            "Length of the fever mark above a man near the camera, in the game's UI pixels (smaller far away - 60% at 60 m). The fill inside it is the fever left.");

        public static readonly ParamDef FeverMarkHeight = Int("FeverMarkHeight", 2, 30, AdvancedGroup,
            "Fever mark: thickness (px)",
            "Thickness of the fever mark, in the game's UI pixels (smaller far away).");

        public static readonly ParamDef FeverMarkHeightMetres = Float("FeverMarkHeightMetres", 1, 4, AdvancedGroup,
            "Fever mark: height above the man (m)",
            "How high above a man's feet his fever mark floats, in metres - the mark's bottom edge sits there (his Athletics bar sits above it). The game's own ALT icon over your men is at about 2 m. A rider's bars sit 1 m higher.");

        // The Athletics bars above the men (step 46) - UI pixels of the 1080p layout, like the fever mark's.
        public static readonly ParamDef AthleticsMarkWidth = Int("AthleticsMarkWidth", 4, 100, AdvancedGroup,
            "Athletics bar above a man: length (px)",
            "Length of the Athletics bar above a man near the camera, in the game's UI pixels (smaller far away - 60% at 60 m). The fill inside it is his Athletics left.");

        public static readonly ParamDef AthleticsMarkHeight = Int("AthleticsMarkHeight", 2, 30, AdvancedGroup,
            "Athletics bar above a man: thickness (px)",
            "Thickness of the Athletics bar above a man, in the game's UI pixels (smaller far away).");

        public static readonly ParamDef AthleticsMarkGap = Int("AthleticsMarkGap", 0, 20, AdvancedGroup,
            "Athletics bar above a man: gap over the fever bar (px)",
            "Space between a man's Athletics bar and the fever bar under it, in the game's UI pixels. The fever bar's place is kept while he has no fever, so his Athletics bar never jumps.");

        public static readonly ParamDef FormationStatsRefreshSeconds = Float("FormationStatsRefreshSeconds", 0.05, 2, AdvancedGroup,
            "Formation averages refresh (s)",
            "How often the formations' averages (Athletics, spread, health) for the orders-menu strip and the formation markers are worked out - every formation of every side - in seconds of battle time.");

        // Step 47 (IMPACT I1, docs/IMPACT.md §7 - Anton approved the plan 2026-10-17): the SPIKE - a test tool, not gameplay. Its loud
        // values are documented constants (Core SpikeMath), not settings: it proves the engine's levers, it tunes nothing.
        public static readonly ParamDef ImpactSpike = Bool("ImpactSpike", AdvancedGroup,
            "Impact test (spike) - not gameplay",
            "A TEST TOOL for the coming impact system (staggers, dropped shields, knockdowns): while on, every battle applies fixed loud values - half the landed kicks knock the man down, blocks stun three times longer for half the men, every other kicked man reels at half speed, the first block every 10 s staggers the blocker, a kicked man drops his shield, one knockdown blow of the mod's own every 20 s - and the log's summary says per lever whether the game followed (WORKS / FAILS / UNSEEN). Leave it off to play. Live.");

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
            // the player page's six groups (step 59)
            DamageRandomPercent, DamageReductionPercent,
            AthleticsEnabled, CostPerBlow, HeroCostMultiplier,
            AiAggressionFreshPercent, AiAggressionExhaustedPercent, FeverEnabled, ShieldWallHoldsSlots, ShieldWallStepOutMetres, ShieldWallStandFirm, SpearPreferenceEnabled, PressDontPass, CourageEnabled, CavalryFlockEnabled, LineDisciplineForPlayer,
            ImpactEnabled, ImpactOnPlayerPercent,
            SprintEnabled, SprintKey,
            ShowPlayerBar, AltMarksEveryone, ImpactShowMessages,
            // the Advanced page
            DamageRandomEnabled, DamageRandomMelee, DamageRandomRanged,
            DamageRandomOnMounts, DamageRandomOnShields, SprintFullCritical,
            AthleticsPoolFloor, AthleticsPoolPerSkill, AthleticsPeakPercent, HealthCapsAthletics,
            CostPerKickOrBash, CostPerShieldBlock, CostPerWrongSideShieldBlock, CostPerWeaponParry, OnlyWrongSideBlocksStopRefill,
            ShieldFrontArcIsCorrect, ShieldFrontArcDegrees, CostOnMiss, PartyLeaderCostMultiplier,
            ExhaustedAttackSpeedPercent, AttackRatePlayerTimer, AttackRatePaceHold, AttackRatePaceByInput, AiHoldRaiseGuard, AttackRateAiDecisions, AttackAnimationMinPercent,
            SwingStackEnabled, SwingStackPointsPerSwing, SwingStackMinimum, KicksUseSwings,
            MinMoveSpeedMultiplier, MountMinSpeedMultiplier, DamageBonusFollowsAthletics, TiredBlockRandomPercent,
            StepBackEnabled, StepBackBackpedal, StepBackMaxChancePercent, StepBackDistance, StepBackSeconds, StepBackEnemyRange,
            StepBackHoldAttacks, StepBackMaxAtOnce,
            ShieldInfantrySwingsLessPercent, FootMeleeSwingsLessPercent, AiMeleeGapSeconds, ExtraPauseAfterBowShotSeconds, ExtraPauseAfterCrossbowShotSeconds,
            ShieldWallPullBackSeconds, ShieldWallAdvanceAsOne, ShieldWallAdvanceStepMetres, ShieldWallAdvanceNearMetres, ShieldWallKickReturnMetres,
            ShieldWallStandFirmSlackMetres, ShieldWallStandFirmArrivedMetres, ShieldWallStandFirmStillMetresPerSecond, ShieldWallStandFirmPinFrame, ShieldWallStandFirmTightTolerance,
            SpearPolearmFavor, SpearSidearmFavor, SpearSidearmAfterSeconds, SpearPolearmBackAfterSeconds, SpearPolearmBackMountedSeconds, SpearRidingFastMetresPerSecond,
            SpearCloseFromLength, SpearCloseBaseMetres, SpearCloseLengthPercent, SpearCloseMinMetres, SpearCloseMaxMetres, SpearCloseFallbackMetres,
            SpearBackRankKeeps, SpearBackRankInStandingLine, SpearKeepVsPolearm, SpearLookSeconds, SpearWieldOverride, ThrustsPassFriends, ThrustsPassPolearmsOnly, FriendlyHitStunSeconds,
            BraceEnabled, BraceFloorChargePercent, BraceFloorAdvancePercent, BraceFloorHoldPercent, BraceFloorNoFormationPercent, BraceFloorSpreadPercent,
            BraceRecoverPercent, BraceRecoverSpreadPercent,
            BraceWieldShield, BraceRaiseShield,
            SprintPointsPerSecond, SprintMinSeconds, SprintMinStartPercent, SprintMaxMultiplier, SprintMinMultiplier, SprintFullSpeedSharePercent,
            SprintTopSpeedReachSeconds,
            SprintRefillSeconds, SprintRefillMovingFactor, SprintRefillExhaustedFactor, SprintAiEnabled, SprintAiChargeKeepPercent,
            SprintAiAdvanceKeepPercent, SprintAiMoveKeepPercent, SprintOnRout, SprintChargeIntoContact, SprintChargeWaitBelowSeconds, SprintChargeWaitMetres,
            FeverPointsPerSecond, FeverAggressionPercent, FeverContactMetres, FeverShieldDurationMultiplier,
            FeverSprintToClose, FeverSprintMinMetres, FeverSprintMaxMetres, FeverSprintCheckSeconds, FeverSprintChancePercent,
            CourageRadiusMetres, CourageBaseOddsPercent, CourageArmourWeightPercent, CourageLevelWeightPercent, CourageHealthWeightPercent,
            CourageHeavyWeaponPercent, CourageTiredWeightPercent, CourageMoraleWeightPercent, CourageShockSeconds, CourageShockWeightPercent, CourageFeverPercent,
            CourageHeroPercent, CourageHysteresisPercent, CourageMinTimidSeconds, CourageSurgeChancePercent, CourageSurgeSeconds, CourageRegroupRadiusMetres, CourageRegroupMates, CourageFlockRadiusMetres,
            CourageFlockMinMen, CourageSprintBack, CourageSprintMetres, CourageSprintEnemies, CourageSprintKeepPercent, CouragePanicOddsPercent,
            CouragePanicSprintKeepPercent, CourageTightenWeights, CourageTimidMeleeMetres,
            FearFlockEnabled, FearMoraleCalm, FearMoraleBroken, FearFreshPercent, FearMoraleWeightPercent, FearTiredWeightPercent,
            DisciplineLevelLow, DisciplineLevelHigh, DisciplineMaxPercent, FearUnitSharePercent, FearThinLinePercent, FearStartPercent, FearFullPercent,
            FearShelterBackMetres, FearShelterMaxMetres, FearDriftMetresPerMinute, FearDriftHoldPercent, FearCoverWidthMetres, FearAnchorFreshPercent,
            FearAnchorSeconds, FearSprintMetres, FearSprintChancePercent, FearSprintKeepPercent, FearWallBreakMorale, FearChargeLooseMetres,
            FearWinCalms, FearWinEvenOddsPercent, FearWinClearOddsPercent, FearWinShakenPercent, FearThreatMetres, FearMopUpWinPercent,
            FearBandEnabled, FearBandRanksMin, FearBandRanksMax, FearBandFileMetres, FearBandRankMetres, FearBandMaxWidthMetres, FearBandGapMetres,
            FearStrikeReachMetres, FearStrikeLeashMetres, LoneRunnerEnabled, LoneRunnerAheadMetres,
            PressToleranceMetres, PressWatchMetres, PressGapMetres, PressManWidthMetres, PressFrontDepthMetres, PressWrapMarginMetres, PressWrapMates,
            PressWrapMatesMetres, PressRoundEndMetres, PressWinPercent, PressLineMinMen, PressLineInSlotPercent, PressBandMarginMetres, PressBlockEndMetres,
            PressEndSlackMetres,
            CavalryFlockFreeEagerPercent, CavalryFlockHysteresisPercent, CavalryFlockMidEagerPercent, CavalryFlockMidGroupSize,
            CavalryFlockExhaustedSharePercent, CavalryFlockTimid, CavalryFlockTimidRadiusMetres, CavalryFlockGatherRadiusMetres, CavalryFlockSeparationMetres,
            CavalryFlockLeadSeconds, CavalryFlockTightenWeights, CavalryFlockWaveSeconds, CavalryFlockMaxSeconds,
            ImpactKicksEnabled, ImpactBlocksEnabled, ImpactHitsEnabled, ImpactRiposteEnabled, ImpactShadowMode,
            KickShieldDropMaxPercent, KickKnockdownMaxPercent, RightBlockStaggerMaxPercent, RightBlockShieldDropMaxPercent, RightBlockKnockdownMaxPercent,
            ParryStaggerMaxPercent, ParryKnockdownMaxPercent, WrongBlockStaggerMaxPercent, WrongBlockShieldDropMaxPercent, WrongBlockKnockdownMaxPercent,
            HitStaggerMaxPercent, HitKnockdownMaxPercent, RiposteStaggerMaxPercent, CorrectBlockTiredWeightPercent, RiposteBlockerTiredPercent,
            StaggerBaseSeconds, StaggerTiredExtraPercent, StaggerMaxSeconds, KickReelTiredExtraPercent, GetUpMaxSlowdown, BlockStunTiredExtraPercent,
            RiposteStunMaxSeconds, ImpactRandomPercent, ImpactStunCapSeconds,
            ImpactStaggerImmunitySeconds, ImpactKnockdownImmunitySeconds, ImpactDiminishWindowSeconds, ImpactDiminishFactor, ShieldPickupLockSeconds,
            ImpactRiderDismountPercent, ImpactTiredNeverShrugsOff,
            ImpactMomentumEnabled, ImpactMomentumPercent, ImpactMomentumWalkMetresPerSecond, ImpactMomentumSprintMetresPerSecond,
            ImpactMomentumRiderPercent, ImpactMomentumRiderWalkMetresPerSecond, ImpactMomentumRiderGallopMetresPerSecond,
            ImpactMessagesIncludeYourMen, ImpactMessageMinSeconds,
            ImpactPunishEnabled, ImpactPunishAggressionPercent, ImpactPunishGraceSeconds, ImpactPunishRangeMetres,
            ImpactShrugOffYieldsToImpact, ImpactFallBackupSeconds, ImpactToughnessWeight, ImpactRangedInHandPercent,
            ImpactRearPercent, ImpactFlankPercent, ImpactRearDegrees, ImpactFlankDegrees, ImpactRearPressure, ImpactFlankPressure,
            ImpactChargeCarrySeconds, RiposteKnockdownMaxPercent, RiposteKnockdownFrom, RiposteMomentumPercent, ImpactMultiplierCapPercent,
            AiKicksEnabled, AiKicksPerMinute, AiKickReachMetres, AiKickFacingDegrees, AiKickCooldownSeconds, AiKickTurtleMultiplier, AiKickTurtleSeconds,
            AiKickTurtleBlocks, AiKickFeverMultiplier, AiKickSkillLowPercent, AiKickSkillHighPercent, AiKickMinBarPercent, AiKickFullBarPercent,
            AiKickKicksARaisedShield, KickBraceEnabled, KickBraceFromPressure, KickBraceToPressure, KickBraceMinBarPercent, KickBraceReelPercent,
            KickShortReelPercent, KickBraceRefusesKnockBack,
            AiKickWaitSeconds, AiKickWaitMetres, AiBashShareOfSwingsPercent, AiBashDelaySeconds, AiBashCooldownSeconds, AiKickFriendClearMetres,
            ImpactMismatchWeight, ImpactEvenBandDoublings, ImpactBodyExponent, ImpactBodyFloorPoints, ImpactStaggeredBonus, ImpactDamageWeight,
            ImpactRefWeightKg, ImpactWeightExponent, ImpactRefSwingSpeed, ImpactSpeedExponent, ImpactBluntFactor, ImpactCutFactor, ImpactPierceFactor,
            ImpactKickFactor, ImpactBashFactor, ImpactSkillMinFactor, ImpactSkillMaxFactor,
            ImpactGuardShieldRight, ImpactGuardParry, ImpactGuardShieldWrong, ImpactGuardNone,
            ImpactStaggerFrom, ImpactShieldDropFrom, ImpactKnockdownFrom,
            RegenDelayBlowTimes, BlowTimeSeconds, FullRegenSecondsStanding, RegenRateNearFullPercent, RegenMultiplierAtFullRun,
            WalkEffortFraction, HideoutBossFightRefill,
            ShowPlayerBarOutsideBattles, ShowAttackRecoveryBar, FlashBarOnEarlyAttack, ShowSprintBar,
            BarYellowBelowPercent, BarOrangeBelowPercent, BarRedBelowPercent,
            ShowInOrderMenu, ShowFormationHealth, ShowFormationSpread, FormationSpreadStdDevs, ShowReadyCount, ReadyYellowBelowPercent, ReadyRedBelowPercent, OrderStripUnderCards,
            ShowAltMarkerStats, AltMarkersShowEnemy, ShowFeverOnLabels, ShowFeverMarks, FeverMarksShowEnemy, ShowAthleticsMarks, AthleticsMarksShowEnemy,
            AltNearMarksMax, AltNearMarksMetres, FeverMarkMax, ShowMoraleMarks, ShowMoraleOnLabels, MoraleLabelMetres, MoraleLabelMax,
            OrderNoAccidentalCharge, OrderChargeDragPixels, OrderChargeIconPixels,
            LineDisciplineEnabled, LineDisciplineRanksT0, LineDisciplineRanksT1, LineDisciplineRanksT2, LineDisciplineRanksT3, LineDisciplineRanksT4, LineDisciplineRanksT5,
            LineDisciplineRanksT6, LineDisciplineHeroTier, LineDisciplineRangedToo, LineDisciplineInSieges, LineDisciplinePreviewCap, LineDisciplineShowMessage, LineDisciplinePollSeconds,
            LineBlockHoldEnabled, LineBlockSlackMetres, LineBlockFullAtDepth, LineBlockLooseMetres, LineBlockRankMetres, LineBlockAheadMetres, LineBlockStrikeReachMetres, LineBlockStrikeLeashMetres, LineBlockNearMetres, LineBlockLeadMetres, LineBlockMopUpWinPercent,
            HudRefreshSeconds, PlayerBarWidth, PlayerBarHeight, PlayerBarOffsetRight, PlayerBarOffsetBottom,
            RecoveryBarWidth, RecoveryBarHeight, RecoveryBarOffsetAbove, SprintBarOffsetAbove,
            OrderStripTextSize, OrderStripTextOffset, OrderStripBarOffset, OrderStripBarHeight, OrderStripReadyOffset, OrderStripSideMargin,
            OrderPanelOffsetTop, OrderPanelWidth,
            AltMarkerTextSize, AltMarkerOffset, AltMarkerBarWidth, AltMarkerBarHeight,
            FeverMarkWidth, FeverMarkHeight, FeverMarkHeightMetres, AthleticsMarkWidth, AthleticsMarkHeight, AthleticsMarkGap,
            FormationStatsRefreshSeconds, ImpactSpike, VerboseLogging, LogMaxMegabytes,
        };

        /// <summary>The groups in order - the player page's first (step 59), then the Advanced page's.</summary>
        public static readonly IReadOnlyList<ParamGroup> Groups = new[]
        {
            MasterGroup, PlayerDamageGroup, PlayerTiredGroup, PlayerAiGroup, PlayerImpactGroup, PlayerSprintGroup, PlayerScreenGroup,
            DamageGroup, AthleticsGroup, TiredGroup, StepBackGroup, BattlePaceGroup, BraceGroup, SprintGroup, FeverGroup, CourageGroup, CavalryFlockGroup, ImpactGroup, RefillGroup, PlayerBarGroup, OrderStripGroup, AltMarkersGroup, OrderClickGroup, LineDisciplineGroup, AdvancedGroup,
        };

        /// <summary>Step 59: the groups of one MCM page, in order.</summary>
        public static IReadOnlyList<ParamGroup> GroupsOn(SettingsPage page)
        {
            var list = new List<ParamGroup>();
            foreach (var g in Groups)
                if (g.Page == page) list.Add(g);
            return list;
        }

        /// <summary>Step 59: the settings of one MCM page, in order.</summary>
        public static IReadOnlyList<ParamDef> OnPage(SettingsPage page)
        {
            var list = new List<ParamDef>();
            foreach (var g in Groups)
                if (g.Page == page)
                    list.AddRange(InGroup(g));
            return list;
        }

        /// <summary>Step 59: the player page's settings (the short main page), in order.</summary>
        public static IReadOnlyList<ParamDef> PlayerPage => OnPage(SettingsPage.Player);

        /// <summary>Step 59: what MCM calls each page - the player page is the mod's name.</summary>
        public static string PageTitle(SettingsPage page) =>
            page == SettingsPage.Player ? "Trax Combat Enhancements" : "Trax Combat Enhancements - Advanced";

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

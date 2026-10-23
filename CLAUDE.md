# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

**Trax Combat Enhancements** (working title) — a combat mod for *Mount & Blade II:
Bannerlord* v1.4.8 that makes fights a bit more fun: every landed hit rolls ±50% damage and then lands 25% lighter (step 25, `DamageReductionPercent`, Anton:
slower fights - AFTER the roll, on people, horses and shields, whatever the roll's switches; falls and objects never;
Core `DamageReduction`),
and every fighter has an **Athletics** bar — his stamina, as big as his Athletics skill —
that blows drain and rest refills. The top quarter of his own bar is full strength; below
it his damage upside, attack RATE and run speed fall, down to one attack in five when empty -
since step 13 PAUSE ONLY: the animations play at full speed (step 20b: a straight line by f down to
85% at empty - Anton's tuning; the run floor 0.6) and a no-attack timer of
D × (1/m − 1) follows each attack (D at FULL animation speed, so the pause in seconds never grows with
the slower swing; the player's input gated; the AI's attack bits taken out of its
own input - guard really up - since step 16, NoAttack before; the player sees an Attack recovery bar
above his Athletics bar); wounds cap the bar; tired AI fighters step back out of the press after a
swing (step 5d) - since step 16 a BACKPEDAL through the AI's own input, facing the enemy, and the
pause survives it (one per-man `AgentComponent` on `OnAIInputSet` does both; A/B switches keep the old ways).
Heroes and party leaders pay less per blow (and big-skill heroes have
big bars), so the game leans hero-centred. Kicks and shield bashes cost a little too (step 18:
`CostPerKickOrBash` 3 - 5 since step 66, 6 since step 71, 8 since step 81 - × the same multipliers, once each, never an attack pause). DEFENDING costs too (step 22):
every melee blow a fighter BLOCKS costs HIM - `CostPerShieldBlock` 1 (2 since step 68, 3 since step 71; shield, right side), `CostPerWrongSideShieldBlock`
5 (2 since step 43, 5 again since step 68, 6 since step 71), `CostPerWeaponParry` 2 (3 since step 71) - × the same multipliers, once per blocked blow (OnMeleeHit's collision data, Core `BlockMath` /
`BlockTracker`); since step 29 only a WRONG-side shield block restarts the refill delay (`OnlyWrongSideBlocksStopRefill`,
Anton: a skilful defender recovers on the defence - a clean block or parry costs, the refill runs on underneath; off =
every paid block, step 22); never an attack pause or a step back; missiles into a shield
free. Step 22 also shipped the refill a straight line again (`RegenRateNearFullPercent` 100) and the run floor 0.3
(config format 5 migrates both once). A hideout's boss
fight is a fresh start for the player's side (step 19, `HideoutBossFightRefill`: when the game's
"Win the Duel" / "Win the Fight" objective appears, his side refills to its wound cap and every pause /
step back on them ends; in a duel his men stand aside, so only he refills). BATTLE PACE (step 21,
Anton: slower, more defensive fights): the AI's pause after each attack gets a share on top of tiredness
by the attack's CLASS, read at its release from what he holds (`IWeaponFacts`) - shield infantry swing
`ShieldInfantrySwingsLessPercent` (30) % less, other foot melee `FootMeleeSwingsLessPercent` (15): pause =
(T + q (D + G)) / (1 − q), G = `AiMeleeGapSeconds` (1.0, the AI's own gap measured in Anton's logs - a pause
adds to it), so fresh men are held too; bowmen +`ExtraPauseAfterBowShotSeconds` (2.0 s), crossbowmen
+`ExtraPauseAfterCrossbowShotSeconds` (2.5 s); riders' melee, thrown weapons, slings and the player: none;
the summary's "battle pace" lines give each class's attacks a minute per man. BRACE BY ORDERS (step 23,
Anton: "not swing but only defend" below a floor set by the orders): each AI man's bar (points ÷ his FULL pool)
is compared about every 0.25 s (staggered) with his formation's movement-order floor - charge
`BraceFloorChargePercent` (20), advance `BraceFloorAdvancePercent` (40; an AI formation's Move under an advance
behaviour counts), the rest `BraceFloorHoldPercent` (60), NO formation (tournaments, arenas, duels - step 28)
`BraceFloorNoFormationPercent` (20); each man's floor ± his own roll of `BraceFloorSpreadPercent` (10, once per man and
battle, its own dice - step 28); at or below it he BRACES (a fourth wish on step 16's
input component: melee attack bits out, guard up; ranged let through) until floor + `BraceRecoverPercent` (20)
± his own roll of `BraceRecoverSpreadPercent` (5, once per man and battle), the target capped by his wounds (the
band slides down under the cap - never stuck); the shield taken out (`BraceWieldShield`: TryToWieldWeaponInSlot, a
one-hander first) and held up (`BraceRaiseShield`); the summary's "brace" lines answer "do lines turtle". The Athletics
bar is shown for the player (step 6)
and — averaged, with a ± spread and the men's health — in a strip under each formation card of
the orders menu (step 9) and, while you hold ALT (or the orders menu is open), under each of the
game's formation markers, the enemy's too (step 20: vanilla's markers read live); both also count
each formation's men READY - not bracing - as "ready 34/50", plain / yellow / red (step 24, `ShowReadyCount`;
hidden while bracing is off). A tired AI man's block DIRECTION is partly a guess (step 30, Anton 2026-10-12:
`TiredBlockRandomPercent` 100 - w = % × (1 − f), the stat decorator raises the game's own per-agent
`AiRandomizedDefendDirectionChance` to own + (1 − own) × w; never the player; Core `TiredBlock`). SWING STACK (step 32, `SwingStackEnabled`): the pause runs only once a fighter's
stored swings - max(`SwingStackMinimum` 2 - 3 since step 81, round(his CURRENT points / `SwingStackPointsPerSwing` 20)) - are spent; one comes
back per pause while he is not attacking (an AI melee man's own ~1 s gap after a swing from the stack excluded), so he can
burst; kicks / bashes spend one too and are gated at 0 (`KicksUseSwings` - your kick key, a held AI man's input); the
recovery bar reads "Swings 4/8". SPRINT (step 33, `SprintEnabled`): a store of seconds = current points / `SprintPointsPerSecond`
20, never below `SprintMinSeconds` 2 (step 35), refilled in 7 s (slower moving or tired), 20% to start; hold `SprintKey` (a key
NAME, LeftControl) or an AI formation's NEW order (step 37, Anton: one number per order kind, the RESERVE he keeps, % of
his FULL store - he sprints everything above it, stored − keep: charge keeps 0, advance 50, move 80, so men moved or
advanced into position keep their sprint for the real charge; no 20% gate for the AI, 0.2 s minimum) → step 35: `SprintMaxMultiplier` (2.0) × his
FRESH run for the first 30% of it, whatever his Athletics, then a line to 1.1 × his CURRENT run (the decorator writes the
sprint's run factor on `MaxSpeedMultiplier` in place of the tiredness multiplier, and caps `TopSpeedReachDuration` at
`SprintTopSpeedReachSeconds` 1 s while sprinting - the game's ~2.3-2.8 s), no Athletics refill meanwhile; on foot only; the
sprint bar above the recovery bar. SHIELD WALLS HOLD THEIR SLOTS (step 41, `ShieldWallHoldsSlots`, Anton 2026-10-17: men stepped
2-3 m out of a waiting wall to swing and walked back): every AI man on foot in a shield wall / square / circle, under any
order but a charge or a retreat (not detached, fleeing, or far from his slot AND behind the line; never you, riders, a
siege's attackers, naval), gets the engine's "keep with the formation instead of moving to the agent" flag - vanilla's own
`Agent.SetFormationIntegrityData` call rewritten with it TRUE from `OnPreMissionTick`, right after each of vanilla's ~0.5 s
updates (his private update timer read once by reflection; RESEARCH §O) - RBM's idea without Harmony; off = vanilla's value
written back at once; the summary's "wall" lines measure how far men stand AHEAD of their slots, ON vs OFF. A SMALL STEP OUT
(step 42, `ShieldWallStepOutMetres` L 1.0 m, Anton's playtest of 41: held men "looked many times like they were about to reach
to hit but stopped"): a LEASH per held man - an enemy (the AI's target, else his immediate enemy) within L + 2 m in front of his
SLOT frees him (vanilla's own value - he closes and swings) until he stands L out of his slot or the enemy is gone, then he is
held (TRUE) and walks back; a new step only once back within min(0.3 m, L/2) of his slot; "out" = the facing for a shield wall,
away from the centre for a square / circle; a man out is looked at every 0.1 s; L 0 = step 41 exactly; the summary's "wall step
out" line (steps, a man's rate, how far, how each ended, the overshoot past L). AGGRESSION FOLLOWS ATHLETICS AND THE FEVER OF A CHARGE (step 43,
Anton 2026-10-17: "a man at work doesn't hold a pace until he drops; the more tired he is, the less he swings" - and "when the
general says NOW!, everybody surges ... the surge fades"): every AI man ON FOOT's two melee attack-decision values (the game's
`AIAttackOnDecideChance` - 0.144 charge / 0.096 / 0.05 wall - and `AIAttackOnParryChance`, assigned with `=` by the base model)
× a share sliding with his bar (points ÷ FULL pool) from `AiAggressionFreshPercent` 60 to `AiAggressionExhaustedPercent` 20, in
0.05 steps riding the speeds' re-target (the stat decorator; never you, never riders, never ranged; × 5e's m if that switch is
on) - a tired man has two brakes, fewer decisions and the pause; a NEW charge / advance order (the sprint's order poll) arms a
FEVER of his CURRENT points ÷ `FeverPointsPerSecond` 10 s at an ABSOLUTE `FeverAggressionPercent` 80 (never below his tired
share), its clock from his first enemy within `FeverContactMetres` 5 m, burning × 1/0.7 with a shield in hand (read live),
fading to his CURRENT tired share; a hold / move / retreat ends it; feverish men roll every 1 s (50% × the fever left) for a
sprint at an enemy 3-12 m off, above their order's reserve; no formation = no fever; the [fever] summary proves it - AI melee
attacks per man-minute in contact by bar band, without / in a fever / in no formation. Swings are cheaper: `CostPerBlow` 3 and
`CostPerWrongSideShieldBlock` 2 in defaults.json (config format 8 moves a file's old 10 / 5 once). THE FEVER MARK UNDER ALT
(step 44, `ShowFeverMarks`, Anton 2026-10-17: "see which soldiers are in FEVER" where the game shows its ALT icons): exactly while
vanilla shows its formation markers (step 20's flag - ALT or the orders menu), a small bar of OURS above each AI man in a fever
(armed / burning; vanilla's own per-man icon mesh is never touched - private, rewritten on every highlight): bright orange and full
while armed, the fill = the fever left fading through yellow, gone when it ends; the feet + `FeverMarkHeightMetres` 2.5 m through the
combat camera every frame, vanilla's icon fade (1.5 → 8 m), smaller far away; yours always, the enemy's behind `FeverMarksShowEnemy`;
at most `FeverMarkMax` 200 (the nearest on the screen) from a reused pool - 0.03 ms a frame, 0 bytes for 400 men in a fever (smoke). AN ATHLETICS
BAR ABOVE EVERY MAN UNDER ALT (step 46, `ShowAthleticsMarks`, Anton 2026-10-17: "how tired they are in a bar above that fever bar ... green
that gets to red"): the same view (the log's "ALT bars") draws a stack per man - his Athletics bar (fill = points ÷ FULL pool, the wound cap
dark red, BarMath's band colours) above his fever bar, the fever bar's place kept so it never jumps; every tracked man but you (riders 1 m
higher, heroes too), the enemy's behind `AthleticsMarksShowEnemy`; ONE cap (`FeverMarkMax` = men marked); the pick sorts and projects
nearest first until the cap is full (an off-screen man never takes a slot) - 0.07 ms a frame, 0 bytes, 200 projections for 1000 men all
on the screen (smoke). THE ALT VIEW DECLUTTERED (step 58, Anton 2026-10-18: "too cluttered ... only show them on up to 5 soldiers that I'm
directly looking at and are really close, making it work in tournaments too"): by default those bars go only on the `AltNearMarksMax` 5 men nearest
the screen's CENTRE (the angle from the combat camera's forward) within `AltNearMarksMetres` 15 m of the camera, no distance fade (`FeverMarkMax`
the ceiling; `AltMarksEveryone` = step 46's view); the formation labels add the group's `fever 12/40` under `ready 34/50` (`ShowFeverOnLabels`);
men with no valid team (an arena's crowd) are never marked, so in a tournament ALT shows your opponent's bars - 0.11 ms a frame, 0 bytes, ~5
projections (smoke). `CostPerBlow` 7 in defaults.json since step 46 (Anton's playtest; config format 9 moves a format-8 file's 3 once), 8 since step 68 (Toni; config format 14), 10 again since step 71 (Toni; config format 15). THE IMPACT
SYSTEM (docs/IMPACT.md - staggers, dropped shields, knockdowns by both men's Athletics; Anton approved it 2026-10-17, steps 47-53) starts with
step 47's SPIKE (Advanced `ImpactSpike`, off - a TEST TOOL): fixed loud values prove the six engine levers in a real battle - a kick's
KnockDown decision, the block stun x3 with the cap 2 s, a reaction at speed 0.5, a forced `act_stagger_backward`, a queued shield drop,
a knockdown blow of our own - each against its own control in the same battle, and the `[summary] impact spike` lines read WORKS / FAILS
/ UNSEEN per lever with what it means for steps 48-53 (PLAYTEST D16). The levers are the REUSABLE seams of 48-53: `ImpactHooks` (the
damage decorator's KnockDown decision + block stun deciders - the stun on worker threads -, the stun cap, our blow's mark) and
`IImpactBody` (SlowReaction, ForceStagger, DropShield, OwnKnockdownBlow - never a rider or a horse). Step 48 (I2) is THE MODEL (Core
`ImpactMath`, the MCM group "Impact: staggers, shield drops, knockdowns" - 60 settings, `ImpactEnabled` on, `ImpactShadowMode` OFF -
decision 40): every kick / bash, block and clean hit on a tracked man is decided ONCE - the pressure x (T + the mismatch of both men's
CURRENT points, the weapon's impact vs the guard, both skills, beyond the even band + the reeling bonus) → each outcome's ceiling × an
S-curve, the anti-abuse state on his record (`TrackedAgent.Impact`) - a landed blow in the decorator's KnockDown seam, a block in
OnMeleeHit; step 48 LOGGED only - each effect step adds its family to `ImpactMath.EffectsBuilt`. Step 50 (I3): KICKS AND BASHES LIVE
- the knockdown answered in the decorator's KnockDown seam, the shield dropped from the NEXT TICK (never in the callback, never in his own
release / bash) and left pickable (the AI's pickup lockable by `ShieldPickupLockSeconds` through a decorator of the game's ItemPickupModel), the
reel and the fall-and-rise played at 1 ÷ the decided factors (x2.5 / x2 at most) and MEASURED by `ReactionWatch`, their real ends written into
`ImpactState` (the 2 s / 5 s immunities from them); never a rider (step 53); YOU lines; each lever disabled alone on an error; a live family
whose effect is not built yet (hits 52) decides on COPIES of the records (`ImpactRules.KeepsState`); the `[summary] impact live`
lines (PLAYTEST D19). Step 51 (I4): BLOCKS AND THE RIPOSTE LIVE - the blocker's stun x (1 + T) in the decorator's stun seam (WORKER threads,
no die, T from a per-man snapshot the tick writes; the game's cap raised to `ImpactStunCapSeconds` while blocks are live, every untouched stun
at the game's 0.8), the rolled stagger and the riposte (the ATTACKER's) = `act_stagger_backward` from the TICK (with, then over the engine's
priority; at the speed for IMPACT §3.7's length - the animation's own length learned from the measured ones), watched, the end into the
record; a wrong-side shield drop from the tick; a blocker knocked down = our own blow from the tick, its get-up slowed; blocks made from inside
a stagger counted; never a rider; YOU lines; each lever (BlockStun / ForcedStagger / OwnBlow) disabled alone; the `[summary] impact live` block
lines (PLAYTEST D20; DESIGN decision 43). Step 52 (I5): CLEAN HITS LIVE + THE REELING BONUS + MOMENTUM - a clean hit's knockdown in the
decorator's KnockDown seam (like a kick's, its get-up slowed), its stagger = the engine's OWN flinch slowed to §3.7's length (the flinch's length
learned from untouched controls), the measured end = the reeling time; tired men never shrug off (`ImpactTiredNeverShrugsOff`: the decorator's
`DecideAgentShrugOffBlow` - main thread - asks `IShrugOffDecider` when the game says yes: threshold × (1 − T)); the reeling bonus +0.75 real on
every family; MOMENTUM (Anton: "a fresh shock unit attacking a tired line must be devastating") - the attacker's own speed toward the victim
(`ImpactFacts.AttackerSpeed`, vanilla's velocity formula) × (1 + `ImpactMomentumPercent` 60 % × clamp((v − 2.0) ÷ (7.0 − 2.0))) on every
family's impact, never a rider's; `ImpactHitLiveStats` - the `[summary] impact momentum` line (every decided blow by the attacker's speed band:
the proof of the shock charge) and the clean hits by weapon class × band (PLAYTEST D21; DESIGN decision 44). Step 53 (I6): RIDERS + THE
ON-SCREEN LINES + THE GUIDE - a rider's knockdown = KNOCKED OFF his horse at `ImpactRiderDismountPercent` 50 % (the model's `Dismount`): a landed
blow answered in the decorator's `DecideAgentDismountedByBlow` (`IDismountDecider`, main thread - the engine asks it INSTEAD of the KnockDown
question for a rider), a blocking rider by our own blow flagged `CanDismount` from the tick (`IImpactBody.OwnDismountBlow`), each followed until
seen off the horse (2 s); never a stagger / slowed reaction on a rider, his shield drops, his block stun x (1 + T) like anyone's; the RIDER's
MOMENTUM = his horse's speed (vanilla's rider velocity) × (1 + `ImpactMomentumRiderPercent` 30 % × clamp((v − 3) ÷ (10 − 3))), never a couched
lance (`IsDoingPassiveAttack`); the ON-SCREEN LINES (`ImpactShowMessages`): what was APPLIED to you (red #D65252) / by you (purple #AE4AD9) /
your side's heroes (white, `ImpactMessagesIncludeYourMen`) → Core `ImpactMessageQueue` (one per `ImpactMessageMinSeconds` 1.5 s, merged) → the
game's message feed from the tick (`IImpactMessageSink`), never in shadow; lever `Dismount`; the `[summary] impact riders` / `impact messages`
lines; HOW-IT-WORKS §11c rewritten as one player section; PLAYTEST's IMPACT MORNING SCRIPT + D22; DESIGN decision 45. Step 54 (I7): THE AI
PUNISHES A WINDOW (Anton: "otherwise it's a player-only weapon") - a WINDOW = a man reeling or down from a live impact outcome (his `ImpactState`,
yours too) + `ImpactPunishGraceSeconds` 0.5; at the fever's look (~0.25 s) an AI man on foot in contact reads his target (`IPunishBody`:
GetTargetAgent, else ImmediateEnemy - only while some window is open anywhere) and, while it is in a window, his aggression share is raised to
`ImpactPunishAggressionPercent` 100 (step 43's recompute path, never lowered); a man within `ImpactPunishRangeMetres` 3 gets courage's surge clock
(a timid one steps in - `CourageEnd.PunishWindow`; never in a panic); never the player as attacker, never a rider, never in shadow; the
`[summary] impact punish` lines (windows opened / seen / PUNISHED / wasted; attacks per man-minute against a target in a window vs not, ON vs
OFF) - PLAYTEST D23, DESIGN decision 46. All seven IMPACT steps are built. RIGHT BLOCKS FAVOUR THE DEFENDER (step 56, Anton's
tournament 2026-10-18: "make sure right blocks are a possible penalty for the ATTACKER"): on a CORRECT block (right-side shield, parry) the
blocker's T counts `CorrectBlockTiredWeightPercent` 25 % in x, the ceilings 30 / 10 (defaults.json); the riposte = 70 % × S(x_r) × (1 −
`RiposteBlockerTiredPercent` 0 × T_D) - two empty even men: the blocker ≤ 5 %, the attacker reels 70 %; the wrong side unchanged;
`BlockStunTiredExtraPercent` 0 (the spike's lever 2 failed); config format 10 (PLAYTEST D25, DESIGN decision 48). ROUTING MEN SPRINT
AWAY (step 57, `SprintOnRout`, Anton: "use their available sprint for the first get-away"): the game's own rout calls
`MissionBehavior.OnAgentFleeing` once per broken man (never for a Retreat order) - an AI man on foot spends his WHOLE sprint store once
(`SprintStore.TryStartRout`: no reserve, no end near an enemy; a running order sprint handed over); behind SprintAiEnabled / SprintEnabled / the
master switch; the [summary] "sprint on rout" line (PLAYTEST D26, DESIGN decision 49). MORE MEN FALL, AND THE CHARGE MATTERS (step 62,
Anton 2026-10-19 - DESIGN decision 52, AI_NOTES "Step 62", 14 Advanced settings in the Impact group + `SprintFullCritical` in Damage): THE LOST
FALLS - the engine asks the SHRUG-OFF first (`Mission.CreateMeleeBlow` 5555, then knock-back, then knockdown) and a weapon blow flagged ShrugOff
plays no reaction (14 of his 35 decided falls): the decorator's shrug-off seam now DECIDES the blow (`IShrugOffDecider` gets the blow - its
OwnerId is the attacker) and refuses the shrug-off for a decided knockdown / stagger (`ImpactShrugOffYieldsToImpact`), the KnockDown seam takes
that same decision; a decided fall not seen within `ImpactFallBackupSeconds` 0.5 = our own knockdown blow (lever 6) from the tick, once; a
killing blow is never decided; the [summary] "impact falls" line. TOUGHNESS (courage's measure, shared: R × (t_A ÷ t_D)^`ImpactToughnessWeight`
0.5 - a legionary on a looter ×2.1; the kit read once a man), a bow in the defender's hand ×1.3, DIRECTION (a landed blow from behind ×2, the
side ×1.4 - the victim's body facing), THE CHARGE (sprinting, or the first blow within `ImpactChargeCarrySeconds` 1.5 after a sprint - the
AI's sprint ends 3 m from the enemy: his log had no blow at all in the sprinting band) counts the full momentum, THE REFLECTED CHARGE (a correct
block reads the blow without its momentum and the charger's momentum against him; a charge into a RIGHT-SIDE SHIELD block THROWN BACK - our
blow on him - up to `RiposteKnockdownMaxPercent` 40 × his charge's share, you too: "You are thrown back by X's shield"), THE UNCAPPED CRITICAL
(a charging man's melee blow rolls the full ±50 %), THE CAP (the multipliers together ×8); the [summary] "impact direction / toughness /
charges" and "damage critical" lines (PLAYTEST D28). FALLS ON THE HIT, A STRONG REAR BLOW, THE WALL PULLS MEN BACK (step 63, Anton's battle
2026-10-19 11:02 - DESIGN decisions 53 / 54, AI_NOTES "Step 63", 3 Advanced settings, config format 11): the 126 of 198 falls played only by
our backup were all FRIENDS' blows (a rear-rank swing into his own front man - `Mission.MeleeHitCallback` cancels a non-enemy's damage, a
0-damage blow plays nothing): a friend's blow / block is never decided (`ImpactFacts.NotEnemies`), every enemy's decided fall was played by the
game at the blow; `ImpactFallBackupSeconds` 0.5 → 0.2. DIRECTION AS PRESSURE: a landed blow from behind adds `ImpactRearPressure` 0.5 to x
straight (like tiredness - "a blow from behind can floor even a fresh man": two fresh even men 7 % down / 40 % staggered from behind, 0 / 0 in
front), the side `ImpactFlankPressure` 0.2; step 62's multipliers ship at x1 (`ImpactRearPercent` / `ImpactFlankPercent` 100 - format 11 moves
200 / 140). THE WALL PULLS MEN BACK: a held wall man more than max(L, 1) + 0.25 m ahead of his slot is OUT OF PLACE - looked at every 0.1 s
and held again at each look; once out `ShieldWallPullBackSeconds` (0.5, Advanced, "Battle pace (AI)") able to move he is WALKED BACK (a
backpedal toward his slot facing the enemy - step 16's input hook, its own `WallPull` wish) to within max(L, 1) ÷ 2, at most 4 s; the
[summary] "wall pulled back" line (spells after an impact / other, THE TIME BACK) (PLAYTEST D29). THE WALL'S BLOCKS COUNT, THE CHARGE HITS
AT SPEED (step 64, Anton's looter battles 2026-10-19 12:44 / 12:47 - DESIGN decisions 55 / 56, AI_NOTES "Step 64", 5 Advanced settings, config format
12): `CorrectSideShieldBlock` is native - the guard's direction against the attack's - and the AI guesses its guard direction most of the time (the
game's `AiRandomizedDefendDirectionChance` = 1 − (skill-level)³, ~0.9-1; a wall's poses fixed), so ~85% of a fresh shieldman's blocks read WRONG
side and no correct-block rule fired: a SHIELD block whose attacker stands within `ShieldFrontArcDegrees` 60 of the blocker's facing counts as a
RIGHT-side block for every system of ours (`ShieldFrontArcIsCorrect`; cost / refill, guard lines, the impact model's event; Core `ShieldArcMath`,
`AthleticsLogic.BlockKindOf` - one verdict per collision), you too, parries the engine's, except a tired AI man's step-30 share w of front blocks
(a stateless roll per blow - a worn-out wall still breaks); the [summary] "shield front arc" line (the game's verdict against ours). A CHARGE order's
and a FEVER sprint run INTO contact (`SprintChargeIntoContact`: no end at 3 m - his first attack ends it, or 0.5 s standing still); a charge sprint
shorter than `SprintChargeWaitBelowSeconds` 3 WAITS for an enemy within `SprintChargeWaitMetres` 10; the fever sprint at 4-8 m (was 3-12); the
carry `ImpactChargeCarrySeconds` 1.5 → 3 and an attack BEGUN in the charge stays one to its blow (`TrackedAgent.ChargeAttack`); the [summary]
"sprint into contact" line. Worked example: a looter's first (charging) blow into a fresh legionary wall bounces 33% (thrown back 15%), a later one
16% - before 2.7% (PLAYTEST D30). THE AI KICKS AND BASHES, THE KICKED MAN BRACES (step 65, Anton 2026-10-19 - DESIGN decisions 57 / 58,
AI_NOTES "Step 65", 21 Advanced settings in the Impact group, no config-format change): the engine's kick is an EVENT flag
(`EventControlFlag.Kick`, contextual - with the guard up a bash) and the AI's own `AiKick` tendency does nothing, so an AI man on foot with the
enemy he fights within `AiKickReachMetres` 1.6 / `AiKickFacingDegrees` 40 KICKS (no shield) or BASHES (a shield; a raised shield facing him is
kicked) through step 16's input hook (a fifth wish, `AiInputState.KickPhase`: the guard 0.4 s for a bash, the kick key 0.15 s, then his own input
held off until it plays out ≤ 1.5 s - RBM's phases) at `AiKicksPerMinute` 0.1 a man-minute × skill (0.5-2) × bar (0 below 35%) × up to 3 against a
TURTLE (`TrackedAgent.TurtleBlocks` + his guard up now) × 2 in a fever, 10 s apart; a look every ~0.25 s, one die against the best case, the engine
read only under it (`IAiKickBody`); the cost = step 18's (seen by the poll); the [summary] "ai kicks" lines (asked → PLAYED → landed). THE BRACE:
inside the impact model's one kick decision - failure = S((x + 0.7) ÷ 1.7) on the kick's own pressure (`KickBraceFromPressure` / `ToPressure`); a
man below `KickBraceMinBarPercent` 20 never braces; braced = no knockdown / drop (`ImpactRefusal.Braced`), the game's knock-back REFUSED in the
damage decorator's `DecideAgentKnockedBackByBlow` (`ImpactHooks.IKnockBackDecider` - the blow decided there, the KnockDown seam takes it), the
leftover reaction at 5 x (`ImpactWatchPurpose.Brace`, `ImpactLiveMath.FastSpeedFor` ≤ 8 x); failed = step 50 with the reel × (0.4 + 0.6 T); you
alike, on-screen lines for yours; the [summary] "kick brace" lines by tier / bar band. Worked example: a fresh looter's kick on a fresh legionary
fails 12.9% - a short reel (×0.40) one in eight, 0 falls, 0 shields; two fresh even men brace 63% (PLAYTEST D31). MORE AI KICKS AND BASHES, EACH COSTING MORE (step 66, Anton's looter battle 2026-10-19 14:16 - "ai kicks: asked 0" in 49.4
man-minutes of contact - DESIGN decision 59, AI_NOTES "Step 66", 5 Advanced settings, config format 13): `CostPerKickOrBash` 3 → 5 (you
too); A WAIT FOR REACH - a decision with the enemy he fights within `AiKickWaitMetres` 5 but beyond 1.6 m / out of front waits up to
`AiKickWaitSeconds` 3 (no new die, the engine read each look; step 65's die mostly came at a weapon's length and was lost), so the rates
are per minute in contact; a man WITHOUT a shield kicks at `AiKicksPerMinute` 1.2 (0.1 before) × the factors, `AiKickCooldownSeconds` 8 apart
(a fresh looter 0.71 a minute, an average man 1.03, a legionary 1.17; ×3 vs a turtle, ×2 in a fever); a SHIELDMAN bashes WITH HIS SWINGS
(Anton: "as frequent as swings") - each melee release of an AI shield-infantry man earns `AiBashShareOfSwingsPercent` 100 % of a bash
(`TrackedAgent.BashCredit`, at most 2), spent ~`AiBashDelaySeconds` 1.5 s ÷ the bar factor later, `AiBashCooldownSeconds` 2 apart - the
model: 0.95 bashes a swing at 100-75 % of the bar, 0.60 at 75-50, 0.20 at 50-25, none below 35 %; the [summary] "ai kicks - bashes per
swing and the wait for reach" line (PLAYTEST D31). COURAGE AND COHESION (step 49, `CourageEnabled`, Anton 2026-10-17 night: "AI men are
unrealistically brave" - a lone man circles a shield wall, stands exhausted among four enemies; + his three additions: shock, flocking,
panic): every AI man on foot in a formation is looked at every ~0.5 s (staggered; one `Mission.GetNearbyAgents` into a reused list): his
side's TOUGHNESS vs the enemy's within `CourageRadiusMetres` 6 m (toughness = armour, level, wounds - a looter 1.35, a legionary 5.97 =
4.4 looters; an enemy with a two-hander / polearm and no shield x1.5) = his local odds, against the odds he needs (`CourageBaseOddsPercent`
100 x tiredness x low morale x SHOCK - landed hits in the last 6 s - x the fever (40% at full) x a hero's 75%). Below it he is TIMID: far
from his mates he WALKS BACK - a fourth input wish (`Regroup`, step 16's backpedal: facing the enemy) - to the nearest BIGGER group of his
side (6+ within 20 m - FLOCKING), else his formation's centre of mass, round the enemy when it stands between, sprinting when far (above a
50% reserve) or in a PANIC (shocked, odds below half the required: everything, `CouragePanicSprintKeepPercent` 0); among his mates he
HOLDS - his behaviour weights tightened (Melee at full only within 2 m, re-applied every look - the game resets them on orders), the game's
back when brave; shocked men want twice the mates and pack in. Step 49b: a man in his FEVER who is not hit presses whatever the odds (the
first clash happens; shock beats fever), and a timid, unhit man among his mates SURGES in now and then (`CourageSurgeChancePercent` 5 × his
bar a look, 4 s); the summary measures the STANDOFF. Never you, riders, walled men, a siege's attackers, no formation. Works
without Athletics. The looks run ON and OFF: the `[courage]` summary compares isolated men and their distance from the formation's centre
both ways (PLAYTEST D18). CAVALRY FLOCKING (step 55, `CavalryFlockEnabled`, Anton 2026-10-18 + his refinement the same morning: "fresh and
eager riders do the vanilla dance ... the more tired a rider gets, the more he goes to meet others of his own kind ... the flock FLIPS"): a
census every 0.5 s of every AI rider whose formation is on a CHARGE (both sides, never you, never a busy rider; hold / move / follow / advance
keep slots); his KIND read once (horse archer / skirmisher / melee) - each kind of each formation its own flock; his EAGERNESS = his bar (x0.5
when courage's odds within 15 m say he is alone); at 75% or more he is FREE (vanilla); below it he FLOCKS: ridden by `Agent.SetScriptedPosition`
(vanilla's own way of riding an AI horse - the horse race, the joust) to his GROUP's centre (the flocking riders of his kind within 25 m, a hash
grid) led 2 s ahead along a heading the rested riders pull toward the formation's goal and the tired ones toward the unit's centre, in a fixed
sunflower slot 3.5 m apart, his horseback weights lowered; the GROUP-SIZE CURVE wants 1 at 75%, 4 at 40%, 65% of his kind at 0; the group FLIPS
when it has the size its MEAN eagerness wants and its leader (most eager) is half ready - all released at once, a WAVE (10 s of the game's charge),
then each decides again; the [cavalry] summary: rider time free / flocking / in a wave, flips and wave size by eagerness, time between passes and
LONE PASSES ON vs OFF (PLAYTEST D24). MORALE EYES + THE FEAR FLOCK (step 67, Anton 2026-10-19: "give me eyes on their
morale too" and "as troops lose their morale and freshness, they flock less to their formation position and more to the centre of their unit's
mass, but pushed back away from the enemy mass ... stepping behind their comrades" - DESIGN decision 60, AI_NOTES "Step 67", 29 Advanced
settings: 25 in "Courage and cohesion (AI)", 4 in the ALT group; no config-format change): under ALT a BLUE morale bar (`Agent.GetMorale()` ÷ 100,
read at the formation-stats refresh) at the bottom of the near men's stacks (`ShowMoraleMarks`; the fever bar lifted above it) and "morale 64,
fresh 41%" under the nearest `MoraleLabelMax` 4 formations within `MoraleLabelMetres` 60 m (`ShowMoraleOnLabels`); THE FEAR FLOCK
(`FearFlockEnabled`, courage's evolution - it rides courage's look and its input lever): FEAR = 1 − (1 − m)(1 − t) (morale below `FearMoraleCalm`
60, full at 15; the bar below `FearFreshPercent` 75 %) × (1 − DISCIPLINE by level: 0 to level 6, 70 % from level 26, a hero 70 %) + 25 % × (1 − d)
with an enemy near and nobody behind him, half his unit's; the PULL from 15 % to 80 % fear blends his place from his slot to his unit's SHELTER
(its centre pushed back from the enemy near it × 4 m × the unit's pull, starting at the centre and creeping back at most 6 m a minute - a quarter
under a hold / a wall), at most 12 m from his slot, behind a comrade when uncovered, held 20 s behind a FRESH comrade (60 %+ of his bar - an
ANCHOR), sprinting back when 8 m+ off or alone (half his store); a place within 1 m of his slot = his own AI; a charge = a loose mass of (1 − p)
× 12 m; walls hold while their men's morale is 30+, step 41's men let go below it (`WallVerdict.Breaking`); a fifth input wish `Flock` - backpedal
> wall pull > courage's walk back > the flock; a fever press / surge ends a spell; never you, riders, a siege's attackers, no formation, a retreat;
the `[fear]` lines and the 7 `[summary] fear flock` lines ON vs OFF (PLAYTEST D32). THE WALL PUSHES AS ONE + KICKS THROUGH THE IMPACT MODEL + MEN TIRE MORE (step 68, Toni's 100 v 100 of 2026-10-19:
his shield wall ordered forward - F1 → F4 = ADVANCE - had 15.4 % of its men in contact more than 1 m ahead of their slots (standing 0.8 %);
92 AI kicks / bashes "landed on the man", 33 decided; "they need to hold the wall and push the whole wall, not to break up"; "tire the men
more ... after the first impact" - DESIGN decision 61, AI_NOTES "Step 68", 5 Advanced settings, config format 14): the cause - under an
advance the game's ORDER slot of each man lies at the enemy's line and every AI man walks to it (`Agent.TrySetFormationFrame`), while step
41's flag holds him to his slot in the line as it stands; now a held man of a moving order (advance - yours or the AI's -, move, fall back,
follow; never a stop, a charge still releases) within `ShieldWallAdvanceNearMetres` 10 m of the enemy's front walks to his slot in the line +
`ShieldWallAdvanceStepMetres` 1 m toward the game's slot (`ShieldWallAdvanceAsOne`; vanilla's own call right after vanilla's update, the leash
keeps it, every let-go / the switch / the master switch gives the game's frame back) - the line advances as one; a held man's OWN kick / bash
(or a punish press) = out of place from `ShieldWallKickReturnMetres` 0.5 m, walked back at once; the undecided kicks were FRIENDS (a rear-rank
man kicking his own front man - the game cancels it, step 63) - `AiKickFriendClearMetres` 0.5 holds a kick with a mate on the line to the
enemy (the decision waits); every landed kick / bash on an enemy is decided once (into a raised shield: not decided - the shield stopped it);
the costs `CostPerBlow` 7 → 8, a right-side shield block 1 → 2, a wrong-side block 2 → 5 (the kick 5 and the parry 2 kept); the [summary]
lines "wall slots ON by order", "wall advance as one", the kicks' victims and "impact kicks / bashes landed vs decided" (PLAYTEST D33).
WINNERS DON'T HANG BACK (step 69, Toni's battles of 2026-10-19 15:5x / 16:0x - a few of his men standing 10-20 m off while his charge finished the
enemy; the 100 v 100's green men pull 0.30, afraid 67 %, 163 'by chance' sprints back - DESIGN decision 62, AI_NOTES "Step 69", 6 Advanced settings,
no migration): the fear flock's fear ignored who was winning; now at courage's look his WIN (`FearMath.Win`: 0 below even local odds - losing men
keep every step-67 number -, 1 at `FearWinClearOddsPercent` 200 = 2 : 1, + `FearWinShakenPercent` 50 % × the enemy near him shaken on the fear's
morale lines; the unit's mean win of its men in contact when better) calms it - fear × (1 − win), the unit table still fed the fear before the
win; no enemy within `FearThreatMetres` 12 = no pull (one enemy read beyond courage's 8 m); a CHARGING unit whose men win `FearMopUpWinPercent` 50 %+
is MOPPING UP - no loose-mass leash; spells end "winning" / "no enemy near"; THE IDLE-MEN DIAGNOSTIC (Core `IdleMen.cs`, measurement only): AI men
of a charge / advance standing > 5 s with an enemy within 25 m and none within 3 m, by who holds them (ranged - normal -, the wall, the fear
flock, courage, the brace, the game) - the [summary] "fear flock and winning" and "idle men" lines (PLAYTEST D34).
 THE FLOCK IS A WIDE BAND + NO LONE RUNNER BEHIND THE LINE (step 70, Toni's 30 v 50 looters of 2026-10-19 15:59 - the
frightened looters stood as a column ~15 deep (`depth ÷ width in contact 1.44`, 3.5 m deep, 2.6 m wide, `stepped in behind a comrade 1394 looks`)
where he wanted a wide mass 4-6 deep along his line; and a few looters ran round BEHIND his line alone - DESIGN decisions 63 / 64, AI_NOTES "Step 70",
11 Advanced settings, no migration): the cause - step 67's shelter was a POINT (a hold's place slid toward it sideways as much as back, a charge
pulled its men onto it) and stepping in behind a comrade stacked the files; now a BAND per unit (Core `FearBand.cs`): along the enemy's front (the
enemy its men see, aligned with the enemy formation's own facing - `IFearBody.EnemyFront`), as wide as that frontage or the men in ranks of
`FearBandFileMetres` 1 m (at most `FearBandMaxWidthMetres` 40), `FearBandRanksMin` 4 to `FearBandRanksMax` 6 ranks deep by the pull, its front
`FearBandGapMetres` 1.5 m short of the enemy pushed back 4 m × the pull at step 67's drift cap; a man keeps his place ALONG the front and is
compressed in depth by his rank in his file (ranks by id at each publish); the men behind a full file slide to the nearest file with room
(toward the ends; reserved; kept for the spell); no stepping in behind a comrade in a full file; a front man with an enemy within
`FearStrikeReachMetres` 2.5 is let go to strike, walked back past `FearStrikeLeashMetres` 2 (`FearBandEnabled` off = the point); THE LONE
RUNNER (Core `LoneRunner.cs`): an AI man on foot alone (fewer than 2 mates near, an enemy within 8 m) more than `LoneRunnerAheadMetres` 4
ahead of his unit's front or past the enemy's front - counted by cause (sprint into contact / punish / surge / fever / chasing a target behind
the front - you, a hero - / the game); `LoneRunnerEnabled`: the fever no longer presses a man alone, a sprint into contact ends alone or past
the enemy's front (`SprintEnd.LoneRunner`), a lone runner walks back (courage's walk, its side round the enemy kept, never turned round by his
fever) unless he chases a beaten enemy; the [summary] "fear flock band" and "lone runners" lines (PLAYTEST D35). MEN TIRE FASTER STILL (step 71, Toni 2026-10-19: "back to 10 for the swings ... they need to tire faster still" -
DESIGN decision 65, AI_NOTES "Step 71", config format 15): `CostPerBlow` 8 → 10, `CostPerKickOrBash` 5 → 6, `CostPerWrongSideShieldBlock` 5 → 6,
`CostPerShieldBlock` 2 → 3, `CostPerWeaponParry` 2 → 3; heroes / party leaders keep their × 0.75 each, the refill unchanged (60 s) (PLAYTEST D36). NO ACCIDENTAL CHARGE ORDERS (step 72, Toni 2026-10-19: a click or a drag that crossed
another unit's floating banner became a charge at it - his men wasted their sprint; DESIGN decision 66, AI_NOTES "Step 72"): vanilla charges the
FOCUSED enemy unit (banner within 70 px of the SCREEN CENTRE, or within 10 m of the camera) on ANY left release in the orders menu; a layerless
`OrderClickView` (joined on the logic's first tick, after the game's `MissionFormationTargetSelectionHandler`, so its OnPreDisplayMissionTick runs
first) holds that handler's PUBLIC targeting switch (`SetIsFormationTargetingDisabled`) OFF while the button is down unless the press is ON the
focused unit's icon (`OrderChargeIconPixels` 30), still on it and no drag (`OrderChargeDragPixels` 12) - the release finds no unit, the game's
placer's move (the dragged frontage) stands; nothing re-issued, so no charge / sprint / fever starts; idle = vanilla (F keys), gamepad and naval vanilla;
`OrderNoAccidentalCharge` on, Advanced group "Orders: no accidental charges"; the [summary] "order clicks" line (PLAYTEST D37). LINE DISCIPLINE BY TIER (step 73, Toni 2026-10-20: 120 tier-1 recruits came at his 60 tier-5 men on a front ~2 deep and wrapped round
him - the AI's FormOrder Wide = round(√(men ÷ 16)) × 16 files, 48 for 120 men; DESIGN decision 67, AI_NOTES "Step 73"): a man's minimum ranks by
his tier (`GetBattleTier`, 7 → 6; heroes as `LineDisciplineHeroTier` 6) from `LineDisciplineRanksT0`-`T6` 6 / 5 / 4 / 3 / 2 / 1 / 1 (7 / 6 / 5 / 4 / 3 / 2 / 1 since step 74 - config format 16), D = the
formation's MEAN, its line at most ceil(men ÷ D) files (never below the game's √men; a fractional D = a partly filled last rank - 100 at 3.5 → 29
files, the 4th rank 45 %); every line / shield wall / loose / scatter formation (not mounted, squares, circles, columns, wedges; ranged by
`LineDisciplineRangedToo`; never naval; sieges by `LineDisciplineInSieges`) wider than that is narrowed in the logic's PRE-TICK (right after the
team tick where the AI's behaviours set their widths, before the wall) with the game's own `SetFormOrder(FormOrderCustom(width))` - a WIDENING or
an AUTO-SHRINK (losses / a new D; D recounted every `LineDisciplinePollSeconds` 1 s and on a count change; a clamp the game does not take
retried after 1 s); the PLAYER's formations too (`LineDisciplineForPlayer`, the PLAYER page - 21 settings) and, `LineDisciplinePreviewCap`,
they carry `DisciplinedLineFormation` - the game's plain `LineFormation` whose virtual `MaximumWidth` answers the cap (`Clone` keeps the type),
put in with the game's own `Formation.Rearrange` (a line → line transfer shares the unit tables), again after the game's next arrangement order:
vanilla's order drag and its move clamp each width to `formation.MaximumWidth`, so the drag preview STOPS GROWING at the cap; his line orders
counted (OnOrderIssued) and said on screen (`LineDisciplineShowMessage`); off = the game's form orders given back, the capped arrangements out;
Advanced group "Line discipline by tier"; the [summary] "line discipline" lines (PLAYTEST D38). THE GREEN BLOCK STAYS A BLOCK INTO CONTACT
(step 74, Toni's battle 2026-10-20 20:30 - 180 T1 recruits held 5 deep spread into a wide crowd just before contact, a trail round his right along
a rock; DESIGN decision 68, AI_NOTES "Step 74"): the cause in v1.4.8 - `TacticFullScaleAttack` swaps `BehaviorAdvance` for `BehaviorTacticalCharge`
near contact, which gives a formation at least as wide as its enemy `MovementOrderCharge` (no slots: every man at his own target); the map one
rank deeper (T0 7 … T5 2, T6 1; config format 16); a formation step 73 holds with D above 1 keeps its BLOCK in a charge and an advance within
`LineBlockNearMetres` 30: in its frame (the median centre line, the fear band's direction) a man beyond the cap's half-width + `LineBlockSlackMetres`
2, `LineBlockAheadMetres` 3 past its front or the slack behind is walked back by a sixth input wish (`AiInputState.Block` - the LAST: backpedal >
wall pull > courage's walk > the flock > the block), his place moving with the block + `LineBlockLeadMetres`; the hold by D (`LineBlockFullAtDepth`
3, + `LineBlockLooseMetres` 10 × (1 − s); elites free); strikers within `LineBlockStrikeReachMetres` 2.5 free (leash 2); released at
`LineBlockMopUpWinPercent` 50; the fear band clamped inside the block; THE DIAGNOSTIC - per held formation its order / behaviour / arrangement /
spread / men out of their slots every 0.5 s, the `[block]` change and FIRST CONTACT lines, the [summary] "block hold" and "block shape" lines
(PLAYTEST D39). A STANDING WALL STANDS FIRM (step 75, Toni 2026-10-22: "the men in the shield wall still step forward
even when on a standing order, they take a few steps to strike at an enemy, then fall back" - a switch to test on and off; DESIGN decision 69,
AI_NOTES "Step 75"): `ShieldWallStandFirm` (PLAYER page, 22 settings, on) - a man step 41 holds whose shield wall / square / circle is on a STOP,
or on a MOVE at its place (`Formation.CurrentPosition` within `ShieldWallStandFirmArrivedMetres` 2 of `Formation.OrderPosition`, its men at most
`ShieldWallStandFirmStillMetresPerSecond` 0.5 m/s - latched per man until the order position moves 2 m or the wall is pushed 4 m off;
`StandFirmMath.Conditions`) never leaves his slot: step 42's leash 0 for him (every step it would give counted as refused), a running AI sprint
ended (`SprintEnd.StandFirm`), the engine's walk target pinned to his slot (`ShieldWallStandFirmPinFrame` - under a Stop the game's frame is his
OWN position, `StandGround`; step 68's `TrySetFormationFrame`), the integrity tolerance cut to the slack (`ShieldWallStandFirmTightTolerance`), and
more than `ShieldWallStandFirmSlackMetres` 0.3 m ahead he is walked straight back to 0.15 m (step 63's backpedal, at once, never while his kick
plays - `WallPullMath.Step(…, standFirmMetres)`); his attack input untouched (guard, blocks, swings and kicks / bashes in reach); never under an
advance / charge / fall back / follow / retreat; step 67's morale break and step 41's escape still let go; courage, the fear flock, the block hold
and the step back already skip held men. Core `ShieldWallStandFirm.cs`, Module `AthleticsLogic.StandFirm.cs`; the `[wall]` stand-firm lines and the
[summary] "wall stands firm" line (ON vs OFF in contact, strikes from the slot, the levers refused by kind) (PLAYTEST D40). SPEARMEN KEEP THEIR SPEARS, THE SECOND RANK THRUSTS PAST
(step 76, Toni 2026-10-22: "my shield wall-ers have spears but they keep using their swords, even though the spear has a longer reach";
DESIGN decision 70, AI_NOTES "Step 76"): `SpearPreferenceEnabled` (PLAYER page, 23 settings, on) - RBM's weapon preference without Harmony: an
AI man (both sides, never you) carrying a melee polearm AND a melee sidearm is looked at every ~`SpearLookSeconds` 0.5 from the tick (the
game's proximity search, never inside the stat model - RBM's Agent.Build deadlock); RBM's two timers - the polearm favoured
(`AiWeaponFavorMultiplierPolearm` x`SpearPolearmFavor` 35) while no enemy is close, the sidearm (`...Melee` x`SpearSidearmFavor` 55) once one has
been close `SpearSidearmAfterSeconds` 2, the polearm again `SpearPolearmBackAfterSeconds` 4 (mounted 2) after the last; a flip = the tick's
budgeted recompute, the stat decorator writes the favor after the base model (which assigns 1 / 1 / 1); CLOSE = within 1.0 m + 60% of his
longest polearm's length (1.5-3.5 m; unknown 2.5 - RBM's), not running away, not wielding a polearm himself (`SpearKeepVsPolearm` - facing only
polearms he keeps his); a rider faster than 4 m/s has nothing close; a BACK-RANK man (`Agent.GetFormationFileAndRankInfo` rank 1+ with a man
of his in front - `LineFormation.GetUnit(file ± 1, rank - 1)`) of a shield wall / square / circle, or a line on a stop, keeps the polearm at
once whatever is close; the wield fallback `SpearWieldOverride` (off) turns a sidearm Wield into the polearm's on step 16's hook. A THRUST
PASSES A FRIEND (`ThrustsPassFriends`, Advanced, on): the damage decorator's `DecideWeaponCollisionReaction` answers `ContinueChecking` before
the base model for a thrust (not a kick / bash) into a man of the attacker's side - vanilla bounces the friend's 0-damage blow; swings still
bounce; the friendly-hit stun (`StunPeriodAttackerFriendlyFire`, 0.4) held at `FriendlyHitStunSeconds` 0.1 and given back at the battle's end /
switched off. Core `Spears.cs`, Module `AthleticsLogic.Spears.cs` / `SpearBody.cs` / `Models/ThrustPass.cs`; the `[spears]` lines and the
[summary] "spears" (time holding the polearm / the sidearm by what was close, ON vs OFF) and "thrusts past friends" lines (the same man then
hitting an enemy within 0.5 s, the friend's flinch) (PLAYTEST D41). RBM's data route (its polearm thrust collision check from 35%) is the
fallback, not built. PRESS, DON'T PASS (step 77, Toni's battles 2026-10-22 10:2x-10:48: 108 T1 recruits' rear ranks walked round his
shield wall's ends and behind it, "dancing", then through the middle - stand firm off changed nothing; DESIGN decision 71, AI_NOTES "Step 77"):
`PressDontPass` (PLAYER page, 16 settings, on) - an AI man on foot (both sides, never you, never riders - courage's look) whose unit fights an
enemy LINE (`PressMath.LineKind`: 8+ men, not retreating, held by step 41 / a held block / 60% in their slots under no charge; a square or
circle = a ring) never walks past its FRONT: the line's front segment every 0.5 s from its own men (`PressLines` - the median of each
man-wide bin's front-most man, the ends, the GAPS of 2 m+ no front man covers, a rear face); within `PressWatchMetres` 4 the part of his
FINAL movement toward it is taken out of his input (a seventh wish, `AiInputState.Press` - the CLAMP filters last, whoever wrote the vector),
past it by `PressToleranceMetres` 0.5 he is walked straight back out, behind the whole line walked ROUND its nearest end; THROUGH only at a
gap, ROUND only when his unit spans `PressWrapMarginMetres` 6 more than the line and he has 5 mates within 4 m (a wrap kept once begun);
a rear-rank man never more than 1 m sideways past the end; a broken line or his unit winning 50% lets go; the walk after the backpedal and
the wall's pull, before courage's walk, the flock and the block; his attacks untouched; the fear band never wider than its enemy line + 1 m
a side (its surplus deeper - Toni's 27 m → 25 m in 5 ranks); the block hold's sideways room within the enemy line's ends in contact
(`BlockHoldMath.DecideRange`). Core `PressDontPass.cs`, Module `AthleticsLogic.Press.cs`; the `[press]` lines and the two [summary] "press,
don't pass" lines (crossings by where and cause, ON vs OFF) (PLAYTEST D42). THE BATTLE'S RHYTHM, PART 1 - ROTATION AND THE LULL (step 78, Toni 2026-10-22, sketch 1-4: "two packs of dogs - they don't want to get
hurt but they won't back down"; DESIGN decision 72, AI_NOTES "Step 78"): `RotationEnabled` / `LullEnabled` / `LullForPlayerUnits` (PLAYER page, 27
settings, on) + 24 Advanced in "Courage and cohesion (AI)" - riding courage's looks (a held wall man through FearSkipped), both need Athletics:
a FRONT man in contact (an enemy within 2.5 m, no mate in front of him) whose bar is at or below 35% or his brace floor swaps with the freshest
mate 0.3-1.5 m right behind him in his file who is 25 points fresher - the fresh man walks up 0.7 m BESIDE him while the tired man holds and
guards (an eighth input wish, `AiInputState.Rhythm` - the walk after the press's walk, before courage / the flock / the block; the clamp after the
press's), then the game's public `Formation.SwitchUnitLocations` swaps their slots and the tired man backs into the fresh man's place facing the
enemy and rests there 6 s; never in a swing / stagger, one swap per man per 12 s; a held wall man's slot IS the swap's place (LookWall), so
stand firm agrees; THE LULL - `RhythmUnits` pairs facing units every 0.5 s (a held wall by its attackers' votes); after 10 s in contact with BOTH
fronts' mean bar below charge 30 / advance 40 / hold 50 % each eases back 2 m to its lull line facing the enemy (walked back, clamped there; a
held wall's slots shifted back), the tired swap to the back during it; it ends when a front regains 10 / 15 / 20 points, a new charge order, a
side breaks, 30 s - then 20 s for the pair; your units only with their switch; step 79's waves end it via `EndLull(key, LullEnd.Wave)`. Core
`Rhythm.cs`, Module `AthleticsLogic.Rhythm.cs` / `RhythmBody.cs`; the `[rhythm]` lines and the two [summary] "battle rhythm" lines (swaps by
end / refusal, the bar of men in contact ON vs OFF, lulls by end, the gap opened) (PLAYTEST D43). THE LULL THAT SHOWS, NO WRAPPING ROUND THE ENDS (step 78b, Toni's 225 v 229 battle 2026-10-22: "between the two fronts [a lull], never, not for
a sec" - 2 lulls in 435 s, the rotation kept the FRONT men's bars up, the fronts meshed (-20.7 m), one lull ended at once by a charge order; the
enemy's 69 m of recruits went round both ends of his ~31 m line through step 77's wrap; DESIGN decision 73, AI_NOTES "Step 78b", 16 Advanced
settings, config format 17): THE BREATHER CLOCK - a pair in continuous contact for a time rolled per spell by the more aggressive order (charge
30-60 s, advance 25-50, hold 20-40) lulls once BOTH UNITS are worn (the mean bar of their men within `LullUnitDepthMetres` 8 m of the front -
the rotated men too: charge 55 / advance 60 / hold 65%) or the pair's blows in contact fell to 60% of the spell's peak; both units below step
78's 30 / 40 / 50% = at once after 10 s; the recovery on the unit bar, +20 / +25 / +30 (format 17 moves an untouched 10 / 15 / 20); a charge order
in a lull's first 3 s waits; THE CLEAR GAP measured from the MEN (2 m bins along the contact, each side's nearest men): the lines from the contact
plane ± `LullGapMetres` 3.5 ÷ 2 (at least the 2 m ease from each side's own nearest men) - a meshed fight separates about its middle - held when
the bins' 25th percentile reaches 3 m, else a side standing at its line steps it back 0.5 m/s (at most 8 m); NO WRAPPING - `PressWrapAllowed` off,
past a line's end + 1 m the front BENDS back `PressWingBendDegrees` 30 (an overlapping wing curls toward the end and stands off it facing it, a
man forward of it walked back), a unit ORDERED there (a Move) free; the `[rhythm]` / `[press]` first-time lines and a third [summary] line each
(the lull's trigger and gap; no wrapping - wings held, the old wrap counter 0) (PLAYTEST D43b). EACH MAN RESTS ON HIS OWN - THE LULL REMOVED (step 78c,
Toni 2026-10-22 after playing 78b: "they are at it the whole time, then they like for some reason all just turn their backs and walk away very far off
... only per men rule, if tired - step back and wait to recover"; DESIGN decision 74, AI_NOTES "Step 78c"): steps 78 / 78b's unit-wide LULL is GONE -
its code, its 29 settings (`LullEnabled`, `LullForPlayerUnits`, 27 `Lull*`; an old config.json's keys dropped with one note each, no format change -
FormatVersion stays 17), its tests and smoke; the rotation, 78b's no wrapping and the unit table (now only measuring) stay. `RestEnabled` (PLAYER page,
26 settings, on) + 15 `Rest*` Advanced in "Courage and cohesion (AI)": an AI man on foot at the fight (an enemy within `RestReachMetres` 3, or
pressing an enemy line) whose bar falls below his OWN tired mark (rolled each rest, `RestTiredMinPercent` 20 - `RestTiredMaxPercent` 45) walks
straight back toward his own side (his unit's axis - the mean of its men's directions to their nearest enemies) out of reach, facing the enemy,
at most `RestMaxBackMetres` 4 from where his rest began and behind his own front, never running (stick 0.6), then STANDS within reach + 1 m (the
rest's own zero vector + step 78's clamp - the game's charge never drags him in; farther off, or 4.5 m+ behind his front, his own AI keeps him with
his line), guard up - he strikes only at an enemy within `RestDefendMetres` 2 - until his OWN ready mark (rolled each rest by his unit's order:
charge 55-95, advance 60-95, hold 65-95 %, 2 points under his wound cap); CONTAGION, the only social rule: each time a man of his unit within
`RestContagionMetres` 3 goes back in, a resting man rolls `RestContagionMaxPercent` 60 % × his way from his tired mark + `RestContagionFloorPoints`
10 to his ready mark (0 below that floor) - a cascade through one reused queue, still each man's own roll; no unit-wide event, no shared clock. A
SHIELD WALL man (step 41 held - a stop, step 68's advance as one, stand firm) rests IN his slot: no walk, step 42's leash 0, guard only; the rotation
may still swap him. The rest's walk sits after the wall's pull and BEFORE the press's, the swap's, courage's and the block's; a step back, the
wall's pull and the fear flock win; routing / excluded men's rests end at once; the rotation first (a swap ends a rest - "rotated"); you never; a
new order never drags resting men in. Core `Rest.cs`, Module `AthleticsLogic.Rest.cs` (+ `IRhythmBody.Facing`, `AiInputState.RestMove /
RestHoldAttacks`); the `[rhythm]` first REST / WALKED BACK / REST IN A WALL SLOT / BACK IN / IN BY CONTAGION lines and the two [summary] "rest (step
78c)" lines (rests, how long, back in by own mark / contagion, stepped back mean / p90, behind the front, TURNED AWAY, WALL men out of their slots,
the marks; THE NATURAL GAP of 2 m+ between engaged fronts by side - `RestGapDiagnosticMetres`, a diagnostic only) (PLAYTEST D43c). WAR CRIES ONLY (step 79, Toni 2026-10-22: battles that
sound alive; DESIGN decision 75, AI_NOTES "Step 79"): SOUND ONLY - `Agent.MakeVoice` (Yell on foot, HorseRally on a horse - vanilla's own yell's choice),
never a change to movement, readiness, the rest marks, morale or orders (the smoke's grind: rests identical on and off). `WarCriesEnabled` (PLAYER page, 27
settings, on) + 10 `WarCry*` Advanced in "Courage and cohesion (AI)": a rested man going back in by his own ready mark shouts 35 %, a mate going in by
contagion answers 20 % (0-1 s later); a formation's NEW charge order (the order poll - yours or the AI general's; vanilla yells only on yours, morale x
0.33 % a man) makes 40 % of its men roar, at most 12 a unit (shared out when many units charge at once: ceil(16 x 1.5 / units)), staggered 0-1.5 s, one
roar a unit per 15 s; limits checked when a cry is due - one cry a man per 8 s, 8 a unit and 16 a battle in any 1 s (Core `WarCryLimiter`, sliding
windows) - over a limit = dropped; never you, the routed / fleeing, the dead; queued from the rest's look / the order poll and played from the tick
(`TickWarCries`), never in a callback; fixed arrays. Core `WarCry.cs`, Module `AthleticsLogic.WarCry.cs` / `WarCryBody.cs`; the `[warcry]` first-time
lines and the [summary] "war cries (step 79)" line (cries by cause, dropped by each limit, the peak in any 1 s, one man's shortest gap) (PLAYTEST D44). AI KICKS AND BASHES LAND ON ENEMIES, NOT FRIENDS (step 80, Toni's logs since step 68 - 427 of 668 landed AI kicks / bashes on a FRIEND at 16:17, 314 of 570 at 17:18 on 2026-10-22; DESIGN decision 76, AI_NOTES "Step 80", 2 Advanced settings, config format 18): step 68 measured a friend against the line to the target, but the kick goes along his FACING - now Core `AiKickPathProbe` (fed by `Mission.GetNearbyAllyAgents` into a reused list): the enemy he fights must be IN his strike path (along his facing, 0 < along ≤ `AiKickReachMetres` 1.6, within the half-width - a kick's `AiKickFriendClearMetres` 0.5 → 0.6, a bash's `AiBashPathHalfWidthMetres` 0.75) and no friend in it closer, else he waits; a friend first within `AiKickRearRankMetres` 1.2 = a rear-rank man: no kick, no wait; a bash's path read again after its 0.4 s guard (`AiKickEnd.PathBlocked`); the rates unchanged; the [summary] "ai kicks - whom they hit and the strike path" line (kick / bash hits on an enemy / a friend / missed, the enemy hits a man-minute, the refusals) (PLAYTEST D45). AI KICKS AND BASHES AS A SHARE OF THE MELEE MOVES (step 81, Toni 2026-10-22: "since they add good disturbance to the battle and also dont do much damage I want to try increase them"; DESIGN decision 77, AI_NOTES "Step 81", config format 19): the decision point is the AI's own melee WIND-UP (step 13's phases see `ReadyMelee`, queue it - `NoteAiMeleeReady` - and the tick rolls it - `DecideAiMove`): with the enemy he fights within `AiKickReachMetres` 1.6 / 40° in front, a SHIELDMAN kicks `AiShieldmanKickPercent` 20 / bashes `AiShieldmanBashPercent` 35 / swings the rest, a man with NO shield kicks `AiNoShieldKickPercent` 35 / swings the rest (kick + bash never over 100 - the bash cut); a rolled kick / bash TAKES THE PLACE of that swing (the attempt's guard cancels the wind-up - a kick's own 0.15 s lead, a bash's 0.4 s); beyond kick reach nothing is rolled (a spearman keeps his ground); a rolled bash into a raised shield is a kick; refused - he swings - by safety (staggered / down / his kick playing / the Athletics / the pool), an empty swing stack, `AiKickCooldownSeconds` (now both kinds, 2 s) or step 80's strike path (no waiting any more). Retired: the per-minute rate and its skill / bar / turtle / fever factors, the bar gate, the wait for reach, the bash credit and the bash's cooldown (14 keys - `ConfigFile.DropRetiredKickKeys`); `SwingStackMinimum` 2 → 3, `CostPerKickOrBash` 6 → 8 (format 19 moves untouched values); the [summary] "ai kicks - shares of the melee moves in kick reach (step 81)" line (rolled and done per man type, kicks and bashes a man-minute in contact, the refusals by group, the Athletics spent) (PLAYTEST D46). MEETING A BASH OR A KICK THE RIGHT WAY (step 82, Toni 2026-10-22: "if met properly the initiator should be caught off balance"; DESIGN decision 78, AI_NOTES "Step 82", 20 settings, no config-format change): the defender's guard is read at contact (channel 1's action, its direction, the defend flag - Core `ImpactMeetMath`; the engine stops a bash with any raised shield and lands every kick and every bash on a weapon) - a bash met by a guard RIGHT or a kick by a guard DOWN = a correct meet: the defender holds (the knock-back refused), the ATTACKER rolls fall / stagger on the riposte's pressure (bash shield 70 / 30, weapon 45 / 15; kick shield 60 / 25, weapon 50 / 30 - the riposte's forced stagger / step 62's own blow from the tick); a bash the shield stopped with a wrong guard = the blocker's rows 85 / 40 / drop 35 (a block's); a bash that landed on a wrong weapon guard 95 / 50, no guard or mid-swing 98 / 60 / drop 45 (the ceiling × step 65's brace curve); a kick met otherwise = step 65; `ImpactMeetRulesEnabled` (the player page) + 19 Advanced; THE AI READS IT - an AI target of a starting kick / bash rolls once at `MeetAiReadScalePercent` × the chance he blocks a swing correctly (the game's AIBlockOnDecideAbility × the right-direction share, step 30's tired guess inside) and raises DefendRight / DefendDown through step 16's hook 0.15 s after the start until the hit (`AiInputState.MeetGuard`); step 81's raised-shield swap only for a shield guarding right; the [summary] "impact meets" lines (PLAYTEST D47). The bar for the
fighter you look at is LATER (DESIGN §3; its settings wait in DESIGN's "Planned parameters"). Words:
the pool/bar/points are "Athletics", the character-screen skill is "the Athletics skill" (it
used to be called "endurance"), the "peak line" is the white mark at the top quarter of the bar,
"the pause" is the no-attack timer after an attack (step 13).

**The full spec is `docs/DESIGN.md`. Read it before any work.** Released on the **Steam Workshop and
Nexus Mods** (mods/14198, since v1.3.0).

## Who does what

Same team and spirit as the sibling mods (`..\TrainingBattlesMod`, `..\ImmersiveAI`). Anton
is the **product owner** — dreams, directs, playtests. Claude is the **developer**. Anton is
an AI engineer but newer to modding, so explain Bannerlord mechanics when they surface. We
work as friends: have opinions, push back, propose.

## How this project is built — the manager protocol (Anton's rule, 2026-09-27)

The work is a numbered **BUILD ORDER** in `TASKS_TODO.md`. It exists so that a session can
die at any moment (tokens run out) and the next one loses nothing.

- **The main session is the manager.** It keeps its own context small: it picks the next
  step, briefs ONE agent to do it, checks the result (build, tests, the agent's report),
  and moves on. It does not read the game's source itself when an agent can.
- **One step at a time, one agent at a time.** Never fan out parallel agents. Quality over
  speed.
- **A step is done only when**: it builds, `dotnet test` is green, its `TASKS_DONE.md` entry
  is written, its `TASKS_TODO.md` line is checked, `AI_NOTES.md` holds any new findings,
  and it is **committed and pushed**. Nothing lives only in a conversation.
- **Mark the step `[~]` in TASKS_TODO before starting it** (and commit that), so a crashed
  session shows what was in flight.
- **Resuming**: read `TASKS_TODO.md` → the first `[~]` or `[ ]` step in BUILD ORDER →
  `git status` / `git log -5` for half-done work → read the step's `AI_NOTES.md` section →
  continue or restart that step.
- An agent's brief must stand alone: point it at this file, `docs/DESIGN.md`, the step's
  `AI_NOTES.md` section and `docs/RESEARCH.md`; say exactly what "done" means (the list above).

## The files

- **TASKS_TODO.md** — Anton's board: short lines only, readable at a glance. BUILD ORDER
  (the steps), PLAYTEST, BUGS, NOT DECIDED. At most a "(see AI_NOTES)" tag on a line.
- **AI_NOTES.md** — Claude's detail companion: one section per step / idea — designs, APIs
  found, gotchas. Read the step's section before picking up its line.
- **TASKS_DONE.md** — the changelog of WORK: one dense `- [x]` entry per finished step, what
  was built and WHY, ending with a `(YYYY.MM.DD HH.MM.SS)` timestamp. Write it so the next
  session starts warm.
- **docs/DESIGN.md** — the behavior contract + the parameter table.
- **docs/RESEARCH.md** — verified game-API findings for v1.4.8 (built in step 2).

## Hard requirements

- **Every number is a parameter.** All values in DESIGN's parameter table live in the config
  file (created on first run, a plain-words explanation beside each value) AND in MCM.
  A new number means a new row in DESIGN's table, same commit.
- **Which MCM page (step 59, Anton 2026-10-18: "leave a few options for me and the players").** MCM has two pages:
  the short PLAYER page "Trax Combat Enhancements" (21 settings since step 73: the master switch, how hard hits land, how fast men tire,
  how the AI fights - each system's own switch -, staggers on / off + against you, the sprint + its key, what the screen
  shows) and "Trax Combat Enhancements - Advanced" (everything else, by system). **A new setting goes to the Advanced page
  by default** (its system's group). It goes to the player page only if a player needs it to set up HIS game - a new
  system's on / off switch, or one number that changes how the game feels - never an inner number, a layout pixel or a
  test tool; and the page stays short (SettingsPagesTests pins its exact list: change it there on purpose). Player-page
  labels / hints in a player's words (no "f", seam, lever, step numbers); a hint that points at another setting names
  the page and group it is on.
- **MCM is truly optional.** The sibling mod learned this the expensive way (its MCM
  settings class has an MCM base type, so the mod will not load without MCM): the game calls
  `Assembly.GetTypes()` on our DLL at startup, and ANY type whose base type, interface or
  field type comes from a missing assembly makes the whole mod fail to load. MCM types may
  appear in METHOD BODIES ONLY (e.g. MCM's fluent builder), or in a satellite assembly loaded
  by hand. Same rule for any other optional dependency. Trap inside the trap (step 3): a
  LAMBDA whose parameter is an MCM type is cached by the compiler in a static field typed
  `Func<McmType,…>` — an MCM field after all. `McmBridge` hands MCM `Action<object>` instance
  methods instead; `tools/AssemblyGuard` (run by every deploy) catches any slip.
- **Decorate game models, never subclass `Default*`/`Sandbox*` models.** `AddModel` replaces
  by base type; a subclass silently drops War Sails' and other mods' versions. Extend the
  abstract model and delegate to the previously registered one (sibling lesson). If a model
  cannot carry what we need, Harmony is acceptable — say why in AI_NOTES.
- **Performance**: battles have 500–1000 agents. No per-agent allocations per tick; poll
  what needs polling at a modest rate; UI updates throttled.
- **Hot-swappable settings (Anton, 2026-09-27): changing ANY parameter in MCM must never
  need a game restart.** Target: live — the next hit / blow / HUD refresh uses the new
  value, even mid-battle (MCM opens from the Escape menu). Code reads parameters from the
  one shared config object AT USE TIME, never copies them into fields at mission start;
  anything that must be rebuilt (e.g. a HUD toggled on, an agent's cached speed penalty)
  listens for a settings-changed event and rebuilds itself. Where live truly is not
  possible, the fallback is "applies from the next battle" — never "restart the game" —
  and that parameter's MCM hint and config-file comment say so. Hand edits of the config
  file are re-read at every mission start (and on load), so they need no restart either.
  Every change is logged (`[config] X: old → new (source: MCM|file)`).
- **The master switch comes first (Anton, 2026-09-27).** Every feature gates on
  `TraxSettings.Shared.ModEnabled` FIRST, read live: off = that feature is pure vanilla at once
  (it lifts whatever it applied - penalties, bars, step-backs), while the mission logging keeps
  running so an ON battle and an OFF battle compare (DESIGN §4). Back on = a fresh start
  (everyone's Athletics full). New code: check it before anything else, and add the feature's
  "off" behaviour to the smoke's master-switch step.
- **Defaults live in `defaults.json`, nowhere else (step 5b, DESIGN §2c).** The schema has no
  default values; a new setting needs `"Key": value` in defaults.json, then
  `dotnet run --project tools/DefaultsTool -- refresh`. DESIGN's Default column is the INITIAL
  value only - never "fix" it to match a tuned defaults.json.
- **Save-safe**: the mod lives inside missions. It must add nothing to the campaign save, so
  it can be enabled or removed mid-campaign.
- **Logging built for one big playtest at the end (Anton, 2026-09-27).** Anton tests
  everything at once when the build is finished, so the log must let us troubleshoot any
  feature WITHOUT a second run. One rolling log file (`trax_combat.log`) beside the config
  file, capped at `LogMaxMegabytes` (8); a trim cuts ONLY the oldest verbose lines (marked `~`
  before the tag) and keeps every other line - summaries, first-time lines, settings, errors
  (step 10b, Core `LogTrim`). Timestamped lines tagged by area (`[config]`, `[mcm]`, `[mission]`,
  `[damage]`, `[athletics]`, `[speed]`, `[rate]`, `[stepback]`, `[brace]`, `[guard]`, `[sprint]`, `[wall]`, `[fever]`, `[courage]`, `[fear]`, `[cavalry]`, `[impact]`, `[orders]`, `[spears]`, `[hud]`, `[error]`). Always logged: mod/game version
  at load, every parameter value on load and on change, each mission start/end (type,
  scene, agent counts), which behaviors/views attached, and every caught exception with its
  stack. Per-battle SUMMARY at mission end (damage rolls: count, min/avg/max factor;
  Athletics: blows charged, exhaustions entered/left, heroes' lowest Athletics, formation
  averages). Chatty per-event lines (each roll, each blow, each regen tick) only when
  `VerboseLogging` is on — and even then rate-limited so a 1000-agent battle cannot flood
  the file; a hot path asks `TraxLog.VerboseWants(bucket)` BEFORE building its line (R8). Every
  game hook is wrapped in try/catch that logs `[error]` and fails SAFE (the
  vanilla behavior), so a bug in the mod never crashes a battle.
- **docs/PLAYTEST.md grows with every step**: what Anton should try, what he should see, and
  which log lines prove it worked. It is the script for the final test session.
- **Commit as each good piece lands** — not only at step end. Small, working commits.

## Layout (real since step 3, 2026-09-27 — keep it true)

```
TraxCombatEnhancements.sln    Core + Module + tests (the tools build on their own)
Directory.Build.props         C# 10, nullable, GameFolder, McmBinFolder; *.user override imported;
                              TraxModVersion read from module\SubModule.xml <Version> (step 11) - both
                              DLLs' <Version> is $(TraxModVersion); an empty one fails the build
defaults.json                 THE ONE TRUTH for every default value (DESIGN §2c): every setting,
                              its value, its explanation + range as // lines. Anton tunes it and
                              pushes; embedded in TraxCombat.Core.dll at build. After a schema
                              wording/range/order change: dotnet run --project tools/DefaultsTool -- refresh
README.md                     the GitHub page (step 60's retelling - the same story and shape as the Workshop page): the
                              hook "Men tire. Lines hold. A fresh charge at the right moment breaks them.", "The idea" (7
                              bites), "How a battle plays" (tiredness and rest, orders and the fever, holding the line, blows
                              that count, duels and tournaments, what you see - screenshots 01 + 02), Settings, Good to know
                              (RBM politely), Install, credits, the developers' fold. Player words only: no numbers tables,
                              no setting keys
docs/HOW-IT-WORKS.md          THE PLAYER GUIDE (step 31; made light by step 60; linked from the README and the Workshop
                              page): each system in plain words, three small tables (attacks per bar, brace by order),
                              Tactics, Settings; every section ends with a link to its numbers in MECHANICS.md. No setting
                              keys here
docs/MECHANICS.md             step 60, THE MECHANICS REFERENCE (the old guide's tables moved, not recomputed): every mechanic
                              by system in TABLES computed from defaults.json (pool / attacks per bar, tiredness by bar level,
                              the swing stack, refill, block costs + "does a parrying man climb", tired block guesses, brace
                              floors + a worked example, step back, pace + the shield wall, aggression and the fever, courage,
                              cavalry flocking, impact - every chance table and worked example -, damage, the sprint, HUD
                              colours, the MCM pages) - setting names in small print; sections 1-16, anchors kept from the old
                              guide's numbering. A tuned default changes its tables: recompute them here
docs/BATTLE_PACING.md         step 15's research (how RBM paces battles, our levers) - developer reading
docs/REVIEW.md                the code reviews: step 10a (R1-R24) and step 17's second one over steps 12-16
                              (R25-R36) - every finding with its severity, place, scenario and status (fixed
                              in which commit / not a bug / deferred / For Anton)
docs/PLAYTEST.md              THE script of Anton's one playtest session (step 10b: one ordered run,
                              parts A-G, ~2 h; appendix L1-L8 = every log line and summary block)
src/TraxCombat.Core/          netstandard2.0 — pure logic, no game refs, unit-tested:
  ParamDef.cs                 one setting: key, type, range, group, label, plain-words
                              description, apply timing (Live / NextBattle); Normalize, Format; step 59: SettingsPage
                              (Player / Advanced) + ParamGroup.Page - the MCM page a group sits on;
                              its Default comes from defaults.json (DefaultsFile), never code; step 33: ParamType.Key +
                              KeyNames (a key by NAME, stored as its index; FormatJson writes "LeftControl"; the
                              file / defaults.json read the name, MCM a text box)
  SettingsSchema.cs           EVERY setting of DESIGN's table (498 - step 82: ImpactMeetRulesEnabled after ImpactOnPlayerPercent (the player page, 28) and the 19 Meet* after AiKickRearRankMetres (Advanced, the Impact group); 478 - step 81: the AI kick rate's 14 out (AiKicksPerMinute and its factors, the bar gate, the wait, the bash credit and cooldown), AiShieldmanKickPercent / AiShieldmanBashPercent / AiNoShieldKickPercent after AiKicksEnabled (Advanced, the Impact group); 489 - step 80: AiBashPathHalfWidthMetres / AiKickRearRankMetres after AiKickFriendClearMetres (Advanced, the Impact group); 487 - step 79: WarCriesEnabled after RestEnabled (the player page, 27) and 10 WarCry* after RestGapDiagnosticMetres (Advanced, "Courage and cohesion (AI)"); 476 - step 78c: the lull's 29 out (LullEnabled / LullForPlayerUnits on the player page, 27 Lull*), RestEnabled after RotationEnabled (the player page, 26) and 15 Rest* after RotationRestSeconds (Advanced, "Courage and cohesion (AI)"); 489 - step 78b's PressWrapAllowed / PressWingBendDegrees after PressEndSlackMetres and 14 Lull* (the clock, the unit's bar, the grace, the gap) after LullRotates, all Advanced; 473 - step 78's RotationEnabled / LullEnabled / LullForPlayerUnits after PressDontPass
                              (the player page, 27) and 24 Rotation* / Lull* after PressEndSlackMetres (Advanced, "Courage and cohesion (AI)"); 446 - step 77's PressDontPass after SpearPreferenceEnabled (the player page,
                              24) and 15 Press* after LoneRunnerAheadMetres (Advanced, "Courage and cohesion (AI)"); 430 - step 76's SpearPreferenceEnabled after ShieldWallStandFirm (the player
                              page, 23) and 17 Spear* + ThrustsPassFriends / ThrustsPassPolearmsOnly / FriendlyHitStunSeconds after
                              ShieldWallStandFirmTightTolerance (Advanced, "Battle pace (AI)"); 409 - step 75's ShieldWallStandFirm after ShieldWallStepOutMetres (the player
                              page, 22) and 5 ShieldWallStandFirm* after ShieldWallKickReturnMetres (Advanced, "Battle pace (AI)"); 403 - step 74's 11 LineBlock* after LineDisciplinePollSeconds (Advanced, "Line discipline
                              by tier"); 392 - step 73's LineDisciplineForPlayer after CavalryFlockEnabled (the player page, 21) and 14
                              LineDiscipline* after OrderChargeIconPixels in their own Advanced group "Line discipline by tier" (Order 23, "Advanced" 24);
                              377 - step 72's OrderNoAccidentalCharge / OrderChargeDragPixels / OrderChargeIconPixels after MoraleLabelMax
                              in their own Advanced group "Orders: no accidental charges" (Order 22, "Advanced" 23); 374 - step 70's FearBandEnabled / FearBandRanksMin / FearBandRanksMax / FearBandFileMetres /
                              FearBandRankMetres / FearBandMaxWidthMetres / FearBandGapMetres / FearStrikeReachMetres / FearStrikeLeashMetres / LoneRunnerEnabled /
                              LoneRunnerAheadMetres after FearMopUpWinPercent, all Advanced; 363 - step 69's FearWinCalms / FearWinEvenOddsPercent / FearWinClearOddsPercent /
                              FearWinShakenPercent / FearThreatMetres / FearMopUpWinPercent after FearChargeLooseMetres, all Advanced; 357 - step 68's ShieldWallAdvanceAsOne / ShieldWallAdvanceStepMetres / ShieldWallAdvanceNearMetres /
                              ShieldWallKickReturnMetres after ShieldWallPullBackSeconds and AiKickFriendClearMetres after AiBashCooldownSeconds, all Advanced;
                              352 - step 67's 25 fear-flock settings after CourageTimidMeleeMetres
                              (courage's group) and 4 morale-eyes settings after FeverMarkMax (the ALT group), all Advanced; 323 - step 66's AiKickWaitSeconds / AiKickWaitMetres / AiBashShareOfSwingsPercent /
                              AiBashDelaySeconds / AiBashCooldownSeconds after KickBraceRefusesKnockBack, all Advanced; 318 - step 65's 14 AiKick* + 7 KickBrace* / KickShortReelPercent in the
                              Impact group after ImpactMultiplierCapPercent, all Advanced; 297 - step 64's ShieldFrontArcIsCorrect / ShieldFrontArcDegrees after
                              OnlyWrongSideBlocksStopRefill and SprintChargeIntoContact / SprintChargeWaitBelowSeconds / SprintChargeWaitMetres after
                              SprintOnRout, all Advanced; 292 - step 63's ImpactRearPressure / ImpactFlankPressure after ImpactFlankDegrees
                              and ShieldWallPullBackSeconds in "Battle pace (AI)" after ExtraPauseAfterCrossbowShotSeconds, all Advanced; 289 -
                              step 62's 13 in the Impact group after ImpactPunishRangeMetres and
                              SprintFullCritical after DamageRandomOnShields; STEP 59: two MCM pages - the PLAYER page's 20 declared
                              FIRST (ModEnabled, then six player groups: "How hard hits land", "Tiredness (Athletics)", "How the AI
                              fights", "Staggers and knockdowns", "Sprinting", "On screen" - Orders 0-6), the Advanced page's 16 groups
                              after (Orders 7-22, "Damage" first, "Advanced" last); GroupsOn / OnPage / PlayerPage / PageTitle; the
                              moved 19 left their old groups (the notes below name their OLD places); step 58's ShowFeverOnLabels after AltMarkersShowEnemy and AltMarksEveryone, AltNearMarksMax, AltNearMarksMetres after AthleticsMarksShowEnemy (before FeverMarkMax); step 57's SprintOnRout after SprintAiMoveKeepPercent; step 56's CorrectBlockTiredWeightPercent and RiposteBlockerTiredPercent right after RiposteStaggerMaxPercent; step 55's 14 in "Cavalry flocking (AI)" after courage; step 54's 4 punish settings after the on-screen lines; step 53's 3 rider-momentum + 3 on-screen-line settings after the momentum ones; step 52's 4 momentum settings after ImpactTiredNeverShrugsOff; step 47's ImpactSpike in Advanced, before VerboseLogging; step 48's 60 in
                              "Impact: staggers, shield drops, knockdowns" - after the courage group, before Refill; step 49's 29 (49b's two surge settings) in "Courage and
                              cohesion (AI)" - after the fever), in file + MCM order, 23 groups (7 player + 16 Advanced)
                              ("Master switch" first, "Damage" second - step 25 renamed it from
                              "Damage randomness", "Battle pace (AI)" - step 21 - after the step back (step 41's
                              ShieldWallHoldsSlots and step 42's ShieldWallStepOutMetres at its end),
                              "Brace by orders (AI)" - step 23 - after it, "Sprint" - step 33 - after that, "Aggression and
                              fever (AI)" - step 43 - after the sprint,
                              "Hold ALT: formation markers, Athletics and fever bars" - step 20, renamed by steps 44 and 46 (was
                              "Formation markers (hold ALT)"; + step 58's ShowFeverOnLabels, ShowFeverMarks, FeverMarksShowEnemy, step 46's ShowAthleticsMarks,
                              AthleticsMarksShowEnemy, step 58's AltMarksEveryone / AltNearMarksMax / AltNearMarksMetres, then FeverMarkMax; Advanced + AthleticsMarkWidth / Height / Gap) - before
                              "Advanced" last; step 10b's one vocabulary and
                              units in its header comment) — the one place a setting is declared
                              (a test parses DESIGN.md: keys + types); NO default values. The LATER
                              features' settings are NOT here (DESIGN "Planned parameters")
  DefaultsFile.cs             defaults.json: the embedded copy → ParamDef.Default (fail safe:
                              problems listed, never thrown), strict Check (keys both ways,
                              JSON types, ranges, comment layout), Write (the repo file and the
                              MCM export alike), ResolveAll (tests); UseValuesForTests
  ModSwitchLog.cs             the master switch over one mission: toggles with times, on-share,
                              "mod ON" / "mod OFF" / "mod was on for N% …" for the summary header
  TraxSettings.cs             THE live settings object (TraxSettings.Shared), read at use time:
                              typed properties, Set(key, value, source), Version, Changed event
  ConfigFile.cs               config.json TEXT: commented writer (header incl. how to revert +
                              // above each key; AppendSettings shared with defaults.json - step 59: a
                              PageBanner where the MCM page changes, the player page's sections first),
                              tolerant reader (comments, trailing commas, casing, "0,75"), Apply;
                              FormatVersion 19 + Migrate (step 81: SwingStackMinimum 2 → 3 (any older file), CostPerKickOrBash 6 → 8 (a format 15-18 file), AiKickCooldownSeconds 8 → 2 (13-18), a tuned value kept and said - MoveOrKeepIfMoved; DropRetiredKickKeys - the AI kick rate's 14 keys leave Unknown in any format, one note each; step 80: AiKickFriendClearMetres 0.5 → 0.6 (a format 14-17 file), a tuned value kept and said - MoveOrKeepIfMoved; step 78c: DropRetiredLullKeys - the lull's 29 keys leave Unknown in any format, one note each, no format change; step 78b's move below has nothing left to move;
                              step 78b: LullRecoverChargePercent / Advance / Hold 10 / 15 / 20 → 20 / 25 / 30 (a format 16 file), a tuned value kept and said - MoveOrKeepIfMoved; step 74: LineDisciplineRanksT0-T5 6/5/4/3/2/1 → 7/6/5/4/3/2 (a format 15 file), a tuned value kept
                              and said - MoveOrKeepIfMoved, T6 never; step 71: CostPerBlow 8 → 10, CostPerWrongSideShieldBlock 5 → 6, CostPerShieldBlock 2 → 3 (a format 14 file),
                              CostPerKickOrBash 5 → 6 (13+), CostPerWeaponParry 2 → 3 once, a tuned value kept and said - MoveOrKeepIfMoved; step 68: CostPerBlow 7 → 8 (a format 9+ file), CostPerWrongSideShieldBlock 2 → 5 (8+), CostPerShieldBlock 1 → 2
                              once, a tuned value kept and said; step 66: a format-12 file's CostPerKickOrBash 3 → 5, AiKicksPerMinute 0.1 → 1.2,
                              AiKickCooldownSeconds 10 → 8 once, tuned values kept + noted; step 64: a format-11 file's FeverSprintMinMetres 3 → 4, FeverSprintMaxMetres
                              12 → 8, ImpactChargeCarrySeconds 1.5 → 3 once, tuned values kept + noted;
                              step 63: a format-10 file's ImpactFallBackupSeconds 0.5 → 0.2,
                              ImpactRearPercent 200 → 100, ImpactFlankPercent 140 → 100 once, tuned values kept + noted;
                              step 56: an older file's RightBlockStaggerMaxPercent 80 → 30,
                              RightBlockKnockdownMaxPercent 40 → 10, ParryStaggerMaxPercent 80 → 30, ParryKnockdownMaxPercent 40 → 10,
                              RiposteStaggerMaxPercent 50 → 70, BlockStunTiredExtraPercent 100 → 0 once, tuned values kept + noted -
                              MoveOrKeep; step 46: a format-8 file's CostPerBlow 3 → 7 once, a tuned value
                              kept and noted - KeptTuned; step 13: a format-1 file's old defaults -
                              AttackRateAiDecisions true, PlayerBarOffsetBottom 54 - get the new ones once;
                              step 14: a format-2 file's MinMoveSpeedMultiplier 0.3 → the default once;
                              step 20b: a format-3 file's 0.7 → 0.6 and a format 2-3 file's
                              AttackAnimationMinPercent 100 → 85 once; step 22: a format 3-4 file's
                              RegenRateNearFullPercent 50 → 100 and a format-4 file's MinMoveSpeedMultiplier
                              0.6 → 0.3 once; step 37: a format-6 file's SprintAiAdvanceKeepPercent 0 → 50 and
                              SprintAiMoveKeepPercent 70 → 80 once; step 43: any older file's CostPerBlow 10 → 3 (today's default - 7 since step 46) and
                              CostPerWrongSideShieldBlock 5 → 2 once; any format: DropRetiredSprintKeys - step 33's two
                              SprintAi*SharePercent and step 36's three SprintAi*UsePercent leave Unknown, one note each;
                              Migrate(read, defaultOf) = the
                              seam the tests hand defaults.json's values through - they run on DESIGN's)
  ConfigMerge.cs              THE FILE-REWRITE RULE (MCM wins for what it touched, the disk for
                              the rest) + EditTracker (what MCM changed since the last write)
  RateLimiter.cs              per-tag token bucket for chatty log lines, counts what it drops;
                              Peek (R8: ask before building a line - a "no" is counted as dropped)
  LogTrim.cs                  step 10b, the log's trim rule (R7): entries (a stamped line + the
                              unstamped lines under it), KEPT = every non-verbose line + the [load]
                              [compat] [config] [mcm] [mission] [summary] [error] tags, CUT = the oldest
                              verbose (~) lines at one point in time; last resort: the oldest kept;
                              one note at the top; Utf8Bytes
  McmPlan.cs                  step 12, when the MCM bridge tries and when it stops: ModuleEnabled (MCM's module
                              Bannerlord.MBOptionScreen in the enabled list; unknown = try), Decide (no DLL →
                              NotLoaded, DLL but module off → ModuleNotEnabled, one line), GiveUp after the first
                              + MaxRetries (30, a documented constant) "not ready"s, the three [mcm] lines
  SingleCopy.cs               step 11, ONE copy runs: TryClaim(token) on an AppDomain data slot (the first
                              SubModule instance to ask runs, a later one is refused and counted - works
                              for one shared assembly and for two), Refused, CopiesIn (the release id
                              and its dotted variants), IdFromManifest, the [compat] lines + the message
  RandomSource.cs             IRandomSource (injectable dice); ThreadSafeRandom ([ThreadStatic]
                              Random per thread, the game's); SeededRandom (tests, smoke)
  DamageRoll.cs               DESIGN §1 pure: HitFacts, DamageRules (live from TraxSettings),
                              Decide = the skip rules (ModOff last), factor U[1-p, 1+p×upside),
                              Upside(rules, attacker f) (DESIGN §2), game rounding, 0 stays 0,
                              positive never below 1; RollOutcome (Upside, Ceiling);
                              DamageCategory, DamageSkipReason; step 25: DamageRules.ReductionPercent
  DamageReduction.cs          step 25 pure (AI_NOTES "Step 25"): ReductionSkipReason (objects, no victim, fall,
                              0 hit, the setting at 0, ModOff LAST - counted like the roll's), DamageReduction -
                              Decide (independent of the roll's switches), Apply = x (1 − r/100), 0 stays 0,
                              positive never below 1, clamped 0-90 (MaxPercent), Reduce → ReductionOutcome (the
                              game's / after the roll / landed), Example ("an average 100 hit lands 75")
  DamageStats.cs              per-mission roll stats (kinds, min/avg/max, before → after, avg per
                              hit, dice histogram, skips by reason, mod-OFF hits unrolled, the
                              upside by the attacker's f + rolls above their top, errors per
                              site, thread) + the [summary] text; thread-safe; step 25: the reduction -
                              hits by kind, the game's → after the roll → landed, the percents used, skips by
                              reason + the [summary] "damage reduction" line
  Athletics.cs                DESIGN §2 pure (Athletics v2, step 5c): AthleticsRules (live;
                              Enabled = ModEnabled && AthleticsEnabled, OffBecause, the floors),
                              Fighter (state as a FRACTION of the FULL pool, AthleticsSkill,
                              Health, the three applied multipliers), AthleticsMath - ONE function
                              per rule (PoolPoints = max(floor, per-skill × skill), UsableFraction
                              = the health cap, PeakShare = f, Attack/Run/MountSpeedMultiplier =
                              floor + (1 − floor) × f, DamageUpside, BlowCostPoints in points,
                              step 18: KickOrBashCostPoints + ChargeKickOrBash (Fighter.KicksAndBashes,
                              never Blows; cost 0 = nothing at all),
                              RegenRateMultiplier by effort, SpeedUpdateNeeded (0.05 step), PeakBin;
                              step 14's refill curve: RegenCurve = 1 − (1 − k) x, RegenRateAtEmpty r0 =
                              ln(1/k) / ((1 − k) T), RefillFrom / RefillSeconds - exact per step;
                              step 19: FreshStart - the refill to the top the wounds allow + FreshStartOutcome;
                              step 22: BlockCostSetting / BlockCostPoints by BlockKind + ChargeBlock
                              (Fighter.BlocksPaid, never Blows; cost 0 = nothing at all), the three
                              CostPer*Block/Parry rules; step 29: BlockRestartsRefill - only a wrong-side
                              block restarts the delay while OnlyWrongSideBlocksStopRefill is on, else
                              Drain leaves LastBlowTime / Regenerating alone; BlowOutcome.RestartedDelay)
                              + Charge / ApplyHealth / Regen / Read; BlowKind, BlowOutcome,
                              RegenOutcome, AthleticsReading (HUD snapshot incl. f, usable pool,
                              BelowFull = below the top it can refill to - step 12)
  KickBash.cs                 step 18: KickBashKind, KickBashTracker - ONE decision per kick / shield bash:
                              Observe(channel, action) = the rising edge on channel 1 or 0 (OR-ed: one
                              kick on both = one), Hit = the fallback when no channel shows one (the
                              poll's late sight within SameActionSeconds 1.0 not charged again); the
                              engine's action codes (the smoke checks them)
  Block.cs                    step 22: BlockKind (none / shield right side / shield WRONG side / weapon parry),
                              BlockMath - KindOf (the engine's AttackBlockedWithShield, CorrectSideShieldBlock,
                              CollisionResult; missiles, kicks / bashes, horse charges, the shield on the back
                              = None), the result codes (the smoke checks them), Name, SettingKey;
                              BlockTracker (a struct on the defender: the same attacker's same swing within
                              SameBlowSeconds 1.0 = the same blow - one charge per blocked blow)
  HideoutBossFight.cs         step 19: the hideout boss fight's facts - BossObjectiveId, the duel / battle text
                              ids (KindOf from TextObject.Value), the controllers by name, BossFightKind,
                              HideoutSide (Player / Boss / Aside / Gone), RefillOffBecause (ModEnabled first);
                              HideoutBossFightStats: the intro, the fight's start (once), the refill counts, what
                              it released, the boss side's freshness + the [athletics] line and the [summary]
                              line ("intro played, fight never seen: tell Claude")
  StepBack.cs                 DESIGN §2 step back pure (step 5d): StepBackRules (live; Enabled =
                              ModEnabled && AthleticsEnabled && StepBackEnabled, Describe),
                              StepBackMath (Chance = max × (1 − f), Roll, AwayFrom = the spot,
                              Radians = the game's facing convention, FacingCosine/Bin,
                              SpeedAway/MotionBin, LevelEnough, TimeUp read live; plumbing
                              MaxHeightStep 1 m, MaxStartsPerTick 20), StepBackNotRolled /
                              StepBackRefusal / StepBackEnd (every reason the summary names; step 16:
                              Arrived, EdgeAhead); step 16: StepBackRules.Backpedal (StepBackBackpedal)
  StepBackStats.cs            per-mission step-back counters + the 8 [summary] lines (rolls by
                              f bin, starts, ends by reason, moves, facing, guard, release checks);
                              step 16: starts by technique + TechniqueText (MIXED when switched), every
                              0.25 s facing + "back turned at ANY sample", arrived, m/s, input releases
  AiInput.cs                  step 16 pure (AI_NOTES "Step 16"): AiInputMath - HoldAttacks (only the attack
                              bits out; a guard DefendDown raised when he wanted to attack, always in a
                              ready = cancel, never release; his own guard kept), Backpedal (his move bits
                              out), BackpedalVector (the away line in his own frame = Mat3.TransformToLocal),
                              Covered / Arrived, Direction, LaterEnd, DeferredStillWorth, the engine's bit
                              values (the smoke checks them) + plumbing (full stick, 0.6 m / 0.25 s ground
                              check, 0.25 s samples); InputEdit
  AiHoldStats.cs              step 16 per-mission: the GUARD by state (held / stepping back / both / everyone
                              else tired / fresh), the hook's counts (men hooked, callback already on / on /
                              off, calls per second, edits, never-called holds, errors), overlaps and Legacy
                              deferrals, switches - the 4 [summary] "AI holds" lines
  AttackRate.cs               DESIGN §2 attack RATE pure (step 5e): AttackKind, AttackPhase (wind-up,
                              held, release, clean release, recoil, reload, pause), the AI timer's
                              reasons (PaceNotHeld - step 16: CoveredByStepBack / PaceRefusal / PaceEnd /
                              PaceRelease), AttackRateRules (live; AiDecisionsOn / PaceOn / PlayerTimerOn,
                              the animation floor, the master switch first; step 16: PaceByInput,
                              RaiseGuard, PaceTechnique), AttackRateMath (ScaleChance x m / ScaleWait
                              ÷ m, CycleCap, TargetCycle = fresh ÷ m, Verdict ±15%; plumbing constants)
  AttackTimer.cs              step 13 PAUSE ONLY pure: AttackTimerMath (Pause = D x (1/m − 1), Worth
                              ≥ 0.1 s, step 20b: AnimationMultiplier(f, A) = A + (1 − A) f (was max(m, A)),
                              PeakShareOfAttack / AnimationForAttack = f read off the APPLIED m,
                              AtFullSpeed = played × animation (D's unit), CountdownText "1.3 s" (tenths
                              up), FlashOn - 2 pulses, the recovery bar's colours), PlayerAttackTimer
                              (YOUR timer: the hold from the release's start, the countdown, Frame =
                              the input gate's decision - clear / swallowed / flash / ended / held at
                              the end, Release), PlayerGateFrame, PlayerTimerEnd, AttackRecoveryReading
                              (the Attack recovery bar's read: Share, SecondsText, the flash)
  AttackRateStats.cs          per-mission attack rate: melee / ranged x AI / you x f band - the
                              animation asked, every phase, the cycle, m, the target (step 20b: + the
                              slower swing's own time, AnimationExtra), measured ÷ target + verdict; the
                              TIMER rows (D, m, the pause asked, the measured gap to the next attack,
                              after its end, early starts; step 16: the timer's floor D/m and the cycle
                              against it; step 20b: the played attack beside D, the floor = played +
                              the pause); left-out counts (mixed, beyond the
                              cap, cancelled, chained; step 16: cycles with a step back inside are
                              COUNTED and shown apart per band); your timer (presses swallowed,
                              flashes, held-button fires, missed attacks, releases); the AI timer (by
                              input / by NoAttack, kinds, mounted, reasons, ends); the guard by f;
                              the AI-decision recomputes; the [summary] "attack rate" lines; step 21:
                              each timer row's battle-pace share, the peak band in the holds list
  BattlePace.cs               step 21 pure (AI_NOTES "Step 21"): AttackClass (shield infantry, other foot
                              melee, riders, bow, crossbow, thrown and slings), RangedWeaponKind, PacePlan
                              (the tired part + the share), BattlePaceRules (live; SettingText, Describe),
                              BattlePaceMath - Classify / Consistent (the class from what he holds, checked
                              against the attack that ended), Plan (foot melee: (T + q (D + G)) / (1 − q);
                              bows / crossbows: + seconds; the rest: T), ModelCycle, PerMinute, FewerShare,
                              the class names; MaxSwingsLessPercent 90
  BattlePaceStats.cs          step 21 per-mission, per class (AI only): attacks by N men, pauses by reason
                              (the share alone / tired + the share / tired only), no pause, the tired part and
                              the share, the cycle (a minute per man while fighting), the AI's own gap after a
                              pause, the model's cycle at 0 and with the setting (foot melee), the archers'
                              share inside a cycle + the 7 [summary] "battle pace" lines
  SwingStack.cs               step 32 pure (AI_NOTES "Step 32"): SwingStackRules (live; Enabled = ModEnabled &&
                              AthleticsEnabled && SwingStackEnabled, KicksOn, Describe), SwingStackMath (RoundAway - half away
                              from zero, Cap = max(min, round(points / per swing)), Full, FallbackSwingSeconds 0.8, burst bins,
                              CountText "4/8"), SwingStack (one fighter: Stored, Progress, the clock - Advance stops while
                              attacking, a refill < 0.1 s fills at once, StartGrace / EndGrace for the AI's own gap; Spend →
                              FromStack / AfterWaiting / AtEmpty; Clamp, ProgressAt, SecondsToNext, FillUp, the burst Run),
                              SwingStackStats (+ the 5 [summary] "swing stack" lines); AttackTimer.cs: PlayerAttackTimer's
                              pauseOverride / StartCountdown / Retarget, AttackRecoveryReading.WithStack
  Sprint.cs                   step 33 pure (AI_NOTES "Step 33"): SprintOrder (charge / advance / move / none - step 36 split
                              advance and move), SprintEnd, SprintStart (step 36: + ReserveKept), SprintRules (live; Enabled =
                              ModEnabled && AthleticsEnabled && SprintEnabled, AiOn, Describe; step 37: the three AI keep
                              percents - the reserves -, AiOrdersText), SprintMath (Capacity = points / per second - step 35: max(SprintMinSeconds, it),
                              PointsCapacity, OnFloor -, TiredFactor, RefillMultiplier /
                              RefillPerSecond / SecondsToFull - 7 / 14 / 14 / 28, CanStart (20% - yours), Group (step 23's
                              kinds); step 37: AiSprints (charge / advance / move), AiKeep, AiAmount = stored − keep x cap or 0
                              below AiMinSeconds 0.2, AiReserveStops; step 35's RunFactor (SprintMax × the FRESH run, a line to SprintMin × the CURRENT run -
                              Multiplier = a fresh man's), Quantize 0.05 (never below 0.05), TopSpeedReach = min(game, ours),
                              the bar's colours; plumbing MovingMetresPerSecond
                              0.5, MeleeRangeMetres 3, MeleeCheckSeconds / OrderPollSeconds 0.25), SprintStore (one fighter:
                              Stored - full until the first Clamp -, Refill, TryStart, Advance - drains only moving -, Stop,
                              Share; step 35: RunFactor; step 36: TryStartAi - no 20% gate), SprintReading (the bar), SprintStats
                              (+ the 4 [summary] "sprint" lines; step 36: by order kind with its reserve ("keeps N%"), reserve kept;
                              step 35: off-the-floor starts, the acceleration game → ours, the speed check × the fresh run);
                              step 64: SprintEnd.Attacked / Stopped, SprintRules.ChargeIntoContact / ChargeWaitBelowSeconds /
                              ChargeWaitMetres + ChargeText, SprintMath.IntoContact / ChargeWaits / StoppedEndSeconds 0.5, SprintStore.IntoContact /
                              StillFor (TryStartAi(…, intoContact), Advance ends a sprint into contact after 0.5 s still once he moved),
                              SprintStats' into-contact / waiting counts + the 6th [summary] line "sprint into contact"
  ShieldArc.cs                step 64 pure (AI_NOTES "Step 64"): ShieldArcRules (live: ModEnabled FIRST, ShieldFrontArcIsCorrect, the
                              degrees 0-180; Describe), ShieldArcMath (NeedsAngle - the rule on and the ENGINE's wrong-side shield block, InArc,
                              Resolve - in the arc → a right-side block unless a tired AI man's step-30 share w keeps the engine's (TiredRoll:
                              a stateless hash of both men and the swing, so every system agrees), Band / BandName - 0-15 … 120-180°),
                              ShieldArcStats (the game's verdict against ours by AI / you, turned, the engine's wrong side by angle band, not
                              looked at, kept by tiredness + the [summary] "shield front arc" line)
  AiKicks.cs                  step 65 pure (AI_NOTES "Step 65"): AiKickKind / AiKickPhase (Guard / Press / Commit) / AiKickSkip / AiKickEnd,
                              AiKickRules (live: ModEnabled FIRST, AthleticsEnabled, AiKicksEnabled, a rate; Describe), AiKickMath (SkillFactor,
                              BarFactor, TurtleShare / TurtleFactor, PerMinute, LookChance = 1 − e^(−rate dt), LookGap, KindFor, FirstPhase / Next /
                              PhaseEnd, Frame - a phase's input bits (the Kick event bit, DefendUp), IsBusyAction, Faded / NoteBlock - the turtle
                              memory; plumbing LookSeconds 0.25, BashLeadSeconds 0.4, PressSeconds 0.15, WatchSeconds 1.5, MaxAttempts 64 - RBM's),
                              AiKickStats (asked → played → landed by kind × reason, ends, why not, Athletics spent, cost + the 4 [summary] lines); step 66:
                              AiKickRules.WaitSeconds / WaitMetres / BashShare / BashDelay / BashCooldown (Enabled = a kick rate OR a bash share),
                              AiKickMath.BashCreditCap 2 / AddBashCredit / BashPerMinute (60 × the bar factor ÷ the delay) / CooldownFor / WithCooldown
                              (1 ÷ (cooldown + 1 ÷ rate)) / Waits, AiKickSkip.Waiting, MaxAttempts 64 → 256, AiKickStats' shield swings / credit / waits
                              + the 5th [summary] line "bashes per swing and the wait for reach"
                              step 68: AiKickSkip.FriendInTheWay (appended - SkipCount 16), AiKickRules.FriendClear (AiKickFriendClearMetres), AiKickMath.FriendInPath
                              (a man of his side > 0.2 m past him along the line to the enemy, within the clearance of it), AiKickVictim (target / another enemy /
                              FRIEND / untracked) + AiKickStats.Victims / NoteVictim / VictimsText and FriendChecksBlocked / Clear (the summary's clauses)
                              step 80: AiKickPath (clear / the enemy off the path / a friend first / behind a friend) + AiKickPathProbe (his frame along his
                              FACING: the enemy in the path, the nearest friend in it closer - allocation-free), AiKickMath.TargetInPath (the cheap gate),
                              FriendPathMinAlong 0.2 → 0, AiKickRules.BashPathHalfWidth / RearRank / PathHalfWidth(kind), AiKickSkip.TargetOffPath /
                              BehindFriend (SkipCount 18), AiKickEnd.PathBlocked (EndCount 7), AiKickOutcome + AiKickStats' outcomes by kind, the path's
                              verdicts, PathLine - the 6th [summary] line; FriendInPath removed
                              step 81: THE SHARES - AiKickRules.ShieldKick / ShieldBash / NoShieldKick (0-1, kick + bash cut to 100 - ShieldSharesCut) / SwingShare,
                              Cooldown for both kinds; AiKickMath.Roll (the die: kick, bash, swing = None), InReach, GroupOf / GroupName (not a move / safety /
                              stack empty / cooldown / path), KickLeadSeconds 0.15 (a kick's guard first - FirstPhase = Guard for both; Frame's Guard sets DefendUp
                              for both), PhaseEnd(phase, start, kind), MaxPendingMoves 256; AiKickSkip rebuilt (16 - OutOfReach … ReadyOver); the rate, its
                              factors, the look, the turtle memory, the bash credit and the wait REMOVED; AiKickStats by man type (ReasonCount 2), NoteMove /
                              Rolled / Done / NoteRefused / Refused, RaisedShieldKicks, QueueFull, SharesLine - the 5th [summary] line
  KickBrace.cs                step 65 pure: KickBraceOutcome (braced / short reel / full reel / down), KickBraceRules (live: ModEnabled FIRST,
                              KickBraceEnabled, From / ToPressure, MinBar, BraceReel, ShortReel, RefusesKnockBack; Describe), KickBraceMath (FailChance
                              = S on x, Spent, ReelCut = short + (1 − short) T, Classify, TierBand by toughness), KickBraceStats (by tier × bar band,
                              spent, the knock-back seam's counts, the cut reactions seen / timed, you + the 5 [summary] "kick brace" lines); Impact.cs:
                              ImpactRules.Brace, ImpactDecision.Brace / BraceFail / BraceDie / BraceCut / BraceSpent / DefenderToughness, ImpactRefusal.Braced
                              (RefusalCount 8), Decide's BraceRoll before the knockdown / drop rolls; ImpactLive.cs: ImpactWatchPurpose.Brace,
                              ImpactLiveMath.MaxSpeed 8 / NeedsFast / FastSpeedFor; ImpactRiders.cs: ImpactMessageKind Braced / ShortReel / BashBraced /
                              BashShortReel; ImpactSpike.cs: ImpactActions.DefendShield
  ImpactMeet.cs               step 82 pure (AI_NOTES "Step 82"): MeetGuardKind / MeetDir / ImpactMeet (the meet table's rows), ImpactMeetRules (ModEnabled FIRST,
                              ImpactMeetRulesEnabled, the 16 ceilings, the read's scale / reaction / hold; AiReads, Describe), ImpactMeetMath (GuardOf - the action / its
                              direction / the defend flag, Classify, AttackerAtRisk, HasRow, Row, Guard - a correct meet's guard, ReadChance / ReadChanceAt, GuardFlag /
                              GuardFrame, GuardUp / TooLate; the engine's codes - the smoke checks them), ImpactMeetStats (the 3 [summary] "impact meets" lines); Impact.cs:
                              ImpactFacts.Meet / AltBlocked / MeetGuardSkill, ImpactDecision.Meet / MeetAttacker / MeetBlocked, ImpactRules.Meet, Pressure's meet guard,
                              Decide → DecideMeet (the attacker on x_r - Riposte / ThrownBack -, a stopped bash's blocker rows, a landed bash through the brace curve)
  Brace.cs                    step 23 pure (AI_NOTES "Step 23"): BraceOrderKind (the game's movement orders + the
                              AI's advance / tactical charge on a Move), BraceOrder (charge / advance / hold / no formation -
                              step 28), BraceEnd,
                              ShieldStep, HandFacts, BraceBand, BraceRules (live; Enabled = ModEnabled && AthleticsEnabled
                              && BraceEnabled, OffBecause, Describe), BraceMath - Group, Band (floor = min(F, cap − M),
                              target = min(F + M, cap); M = recover ± his roll, ≥ 1 point), Enter / Leave / LeaveReason,
                              RollUnit / Offset / Margin (the per-man spread, rescaled live), step 28: FloorOffset / Floor
                              (his own floor = the order's + his floor roll x BraceFloorSpreadPercent, clamped 0-1; Band takes
                              both rolls), ShieldStepFor; plumbing
                              PollSeconds 0.25, ShieldCheckSeconds 1, MaxWieldCalls 3, LongBraceSeconds 30
  BraceStats.cs               step 23 per-mission: men polled / braced, braces by order and floor, AI fighter-time vs
                              brace time, lengths (5 bins), ends by reason, the margins rolled + length by margin band,
                              the shield (at start, calls, seen, put away, never), the guard while bracing, the hook's
                              frames, attacks while bracing; step 28: by floor incl. no formation, the floor offsets rolled
                              + braces by floor-offset band + the 8 [summary] "brace" lines
  ShieldWall.cs               step 41 pure (AI_NOTES "Step 41", RESEARCH §O): WallArrangement (shield wall / square / circle /
                              column / other), WallVerdict (held, or why not - in GATE ORDER: ModOff, SwitchedOff, Naval,
                              SiegeAttacker, NotAi, Mounted, NoFormation, Detached, Arrangement, ChargeOrder, RetreatOrder,
                              NoOrder, Retreating, FarBehind), WallFacts (the gates + positions: him, his slot, the facing, the
                              centre, the integrity data), WallRules (live: ModEnabled FIRST, ShieldWallHoldsSlots; Describe;
                              step 42: StepOutMetres clamped 0-3, Leash = it while on else 0 - the 2-argument constructor = 0),
                              WallMath - IsDefensive, OrderVerdict, Eligible (no switches, no escape - the samples), Decide,
                              DistanceToSlot, FarFromSlot (CatchUpSpeeds 6 - vanilla's radius), BehindLine, Escapes, Ahead (along
                              any facing); plumbing PollSeconds 0.25, SampleSeconds 0.5, FallbackRewriteSeconds 0.1, ContactMetres
                              5, AheadNear / FarMetres 1 / 2
  ShieldWallStats.cs          step 41 per-mission: WallAheadSamples (> 1 m / > 2 m ahead, mean of the part ahead, the farthest;
                              in contact apart), WallWriteKind (poll / after vanilla / fallback), WallStats (looks by verdict,
                              writes by kind, vanilla's updates seen + interval, releases by reason, the samples ON / OFF, men
                              without a timer, left the field, held at the end, cost) + the 5 [summary] "wall" lines; step 42:
                              WallWriteKind.Leash, WallStats.Leash, the 6th line (the step out)
  ShieldWallPull.cs           step 63 pure (AI_NOTES "Step 63"): WallOutOfPlace (one held man's spell beyond the leash - since, after an
                              impact, farthest, pulling / pulled, the pull's seconds, able to move since; a struct in WallState),
                              WallOutEnd, WallOutMove, WallPullMath (Beyond = max(L, 1) + 0.25, BackAt = max(L, 1) / 2, Step - out, back,
                              the walk back once out the setting able to move, paused while down / reeling, at most MaxPullSeconds 4;
                              Describe), WallPullStats (spells after an impact / other, ends, THE TIME BACK by cause and by who walked
                              him + the [summary] "wall pulled back" line); ShieldWallStats.cs: WallWriteKind.OutOfPlace, WallStats.Pull
                              step 68: Step(…, kickReturnMetres, ownKick) - his OWN kick / bash (KickWindowSeconds 2.5) or a punish press = out of place from
                              ShieldWallKickReturnMetres, back within half of it, walked back at once (no wait; works with the pull at 0); WallOutOfPlace.AfterOwnKick;
                              WallPullStats.SpellsAfterKick / BackAfterKick (the line's step-68 clause)
  ShieldWallPace.cs           step 68 pure (AI_NOTES "Step 68", DESIGN decision 61): WallOrderGroup (standing / advance / moving), WallPaceRules
                              (ModEnabled FIRST, ShieldWallHoldsSlots, ShieldWallAdvanceAsOne; the step clamped 0.3-3 m, near), WallPaceMath (GroupOf, Paces - every
                              held order but a stop, FrontGap - the centres' distance less half of each depth, Engaged, OrderLead, Pace - his slot in the line + at most
                              the step toward the game's order slot when it lies farther and the fronts are near, Why - off / standing / no order slot / far / within the
                              step), WallPaceSkip, WallPaceStats (WallAheadSamples by order group ON, paced writes / men by group, the order slot's lead, the gap, skips,
                              restored, errors + the [summary] "wall slots ON by order" and "wall advance as one" lines; WallStats.Pace)
  ShieldWallLeash.cs          step 42 pure (AI_NOTES "Step 42"): WallLeash (Free / Armed / Returning, the step's start, start
                              out and farthest out - a struct in WallState), LeashMove (Freed, Reined, EnemyGone, LeashOff,
                              BackInSlot), LeashEnd (reached the leash / enemy gone / leash set to 0 / let go / fell / still
                              out), LeashMath - OutDirection / OutOf / Out (the facing; a square / circle: centre → his slot,
                              the facing within MinRadialMetres 0.5), EnemyCloseInFront (in front of the slot's line, within L +
                              StrikeReachMetres 2 of the SLOT, + EnemyHysteresisMetres 0.5 while out), RearmAt = min(BackInSlot
                              0.3, L/2), NeedsEnemy (the native read only when he could step / is out), Step (L ≤ 0 = the hard
                              hold), Describe; plumbing LookSeconds 0.1, OverMetres 0.5, MaxMetres 3; WallLeashStats (steps, men,
                              a man's rate while held, farthest out, ends, the overshoot of the reined, back in slot, time out,
                              in-contact samples past L / L + 0.5) + the [summary] "wall step out" line
  Fever.cs                    step 43 pure (AI_NOTES "Step 43"): FeverRules (live; ModEnabled FIRST, AthleticsEnabled = AggressionOn,
                              Enabled = + FeverEnabled, SprintOn; the percents, the store, contact, shield, the sprint roll; Describe with
                              a charge's values fresh / half / empty / peak), FeverMath (TiredShare = E + (F − E) × bar, AggressionShare -
                              armed max(T, P), burning T + (P − T) × left ÷ full, never below T -, Quantize 0.05 (ShareStep), DecisionFactor
                              (1 for the player / off), ApplyChance (× then clamp 0-1, no floor), Store = points ÷ per second, BurnRate,
                              Group (charge / advance), SprintChance (× left ÷ full, full armed), SprintRoll, share bands; plumbing
                              PollSeconds 0.25, MaxPercent 200), FeverStore (one man: Arm → Armed / Rearmed / KeptLarger / Repeat /
                              NoStore / Off / NotAggressive, StartBurn, Burn - the shield live, SprintDue, Stop), FeverPhase, FeverEnd,
                              FeverArm, FeverSprintRefusal
  FeverStats.cs               step 43 per-mission: fevers armed by order and by you / the AI general, re-arms, contact delay, never met,
                              burns by shield, ends, decision recomputes per AI man-minute and per fever, fever sprints by the fever left
                              + refusals, THE PROOF - AI melee attacks per man-minute in contact by bar band (no fever / in a fever / no
                              formation), the values written by band and in a fever + the 10 [summary] "fever" lines
  TiredBlock.cs               step 30 pure (AI_NOTES "Step 30", RESEARCH §M): TiredBlockRules (live: ModEnabled FIRST,
                              AthleticsEnabled, TiredBlockRandomPercent > 0; Share, OffBecause, Describe), TiredBlockMath -
                              Weight = share × (1 − f) (0 at f 1, off, the player), OwnChance (the game's value clamped 0-1:
                              a final-round tournament fighter's −1.10 → 0), Chance = own + (1 − own) × w (w 0 = the game's
                              value untouched), RightShare; TiredBlockStats (the chance raised by f band, the AI's GUARD by f
                              band - shield right / WRONG side / parry / landed - + the [summary] lines; step 34: the
                              guard split by a shield in hand at the blow - all / WITH / WITHOUT, 4 lines - and the
                              recomputes with a shield);
                              Fighter.BlockRandomWeight = the w applied now (Athletics.cs)
  ReadyCount.cs               step 24 pure (AI_NOTES "Step 24"): ReadyBand (plain / yellow / red), ReadyHidden (ModOff
                              first, AthleticsOff, BraceOff, SwitchedOff, NoMen), ReadyRules (live: ShowReadyCount,
                              Ready*BelowPercent, Describe), ReadyMath - HiddenFor, Band (the most alarming wins, "at
                              or below"), Text "ready 34/50" (FormationAthleticsStats.Ready of Total), PlainHex = the
                              text brushes' own #E8E8E8FF; ReadyCountStats (shown by band, the fewest ready, hidden by
                              reason + the [summary] "ready count" line of each view)
  AthleticsBar.cs             step 6 (and the strip's colours; step 7's LATER target bar would reuse it): BarBand, BarRules (live
                              Bar*BelowPercent), BarMath - Band (green at the peak line, blue just
                              below, yellow/orange/red at or below their % of the line, the most
                              alarming wins, empty always red), Fill / Usable / PeakLine (the
                              shares the prefab's FillBarWidgets draw), DisplayNumbers ("132 / 180",
                              rounded UP - 0 only when empty), the colours (#RRGGBBAA constants)
  HudGate.cs                  THE show/hide rule of every HUD view: HudHide (ModOff FIRST,
                              AthleticsOff, ToggleOff, HideBattleUI, PhotoMode, NotFightMode,
                              OutsideBattlesOff, NoPlayer, NotTracked, OutsideIdle, ViewCondition; +
                              MissionEnd / Failed as removal reasons), HudGateInput (+ HudOutside, step 12:
                              the OUTSIDE-A-BATTLE rule of the player bar - walk-about mode, tracked, a
                              weapon drawn or below full, the 1 s grace Lingers / OutsideLingerSeconds),
                              Decide, ShowReason (HudShow: fight / weapon drawn / refilling / grace),
                              Describe / ShortName for the log
  HudStats.cs                 one HUD view over one mission: time on screen, hidden by reason,
                              builds, removals by reason, refreshes, errors, disabled, movie
                              failure; for bars: time per colour, colour changes, empty, wounded
                              (lowest usable) + the [summary] "hud:" lines; ConditionName (the
                              view's own condition in the summary - "orders menu closed"); step 12:
                              outside a battle - builds by HudShow, seconds on screen, the clause
  OrderStrip.cs               step 9, the orders-menu strip pure: OrderCard / OrderCardFrame (the
                              vanilla cards read in one frame, pixels), StripFormation,
                              OrderStripMath (Match = the drawn set of 8 + slot k vs formation k by
                              the member counts → Aligned / NotYet / Mismatch / Problem; PlaceCell in
                              pixels, lifted at the screen's edge; Signature; Numbers / texts
                              "72% ± 8" "HP 81%"; Band; the slot names), StripLayout (the OrderStrip*
                              settings; step 24: the ready row at OrderStripReadyOffset counts in Top / Depth
                              only while ReadyRules.Shown - 39 px deep, else step 9's 23), StripFallback,
                              OrderStripStats (+ the [summary] strip line); DescribeValues + "ready 34/40"
  AltMarkers.cs               step 20, hold ALT pure: AltMarker (one vanilla marker as read: key = team index x 16 +
                              formation, team type, vanilla's point, WSign, distance, men, its widget's size / alpha /
                              pinned offsets, the stats), MarkerWidget, AltMarkerFrame (<= 64, reused), AltMarkerMath -
                              Pair (widgets to targets: index first, then the exact point), Nominal (a fallback's size),
                              Sight (enemy off / no stats / faded / not laid out / pinned / behind / shown), FullyOnScreen
                              (vanilla's test), Place (the marker's bottom centre + the gap x the UI scale, a 200 px
                              centring box), VanillaAlpha (the prefab's distance fade); AltMarkerLayout (the AltMarker*
                              settings), AltMarkerTechnique / AltMarkerFallback, AltMarkerStats (+ the [summary] line)
  FeverMarks.cs               step 44, the fever mark under ALT pure (AI_NOTES "Step 44"): FeverMarkSkip (NotPlaced, EnemyOff,
                              Behind, TooClose, OffScreen, Capped - in the order asked), FeverMarksHidden (ModOff FIRST,
                              AthleticsOff, FeverOff, SwitchedOff), FeverMark (one man: side, armed / share, the world point,
                              distance, in front, the screen point), FeverMarkFrame (reused; arrays double only past their
                              most), FeverMarkRules (live: the switches, FeverMarkMax, the layout; Describe), FeverMarkMath -
                              Tint (orange armed / at the burn's start → yellow as it empties, alpha 1 → 0.45, 0 empty), Fade
                              (vanilla's ALT icon: 0 under 1.5 m, a line to 8 m), SizeFactor (1 to 20 m → 0.6 at 60 m), PreSkip,
                              Pick (the nearest N - Array.Sort keys/items, allocation-free), Size, Place, OnScreen, the names;
                              FeverMarkStats (+ the [summary] line: shows, frames, marks mean / max, the cap, skips, cost);
                              step 46: FeverMark = one man's whole mark (+ InFever, Mounted, BarFill / BarUsable / Band,
                              HasBar / HasFever / FeverSlot), FeverMarkFrame.DropLast / Keep, IMarkProjector + PickOnScreen<T>
                              (sorted only when the cap bites, projected NEAREST FIRST until it is full - an off-screen man takes
                              no slot, the rest never projected), FeverMarkStats.NoteBars + the "hud: ALT bars" summary (men
                              marked, Athletics bars by colour + wounded, fever bars, men looked at / in a fever)
  AthleticsMarks.cs           step 46, the Athletics bar above every man under ALT pure (AI_NOTES "Step 46"): AthleticsMarksHidden
                              (ModOff FIRST, AthleticsOff, SwitchedOff), AthleticsMarkRules (live: ShowAthleticsMarks, the enemy's
                              switch, width / height / gap; Describe), MarkBox, AthleticsMarkMath - RiderLiftMetres 1, WoundHex
                              (the player bar's), MarkHeight, Decide (the bars by side - the fever's place kept while fever bars
                              can show for his side - then behind / too close), Read (fill = points ÷ FULL pool, the wound cap,
                              BarMath.Band), Layout (the stack: the Athletics bar, the gap, the fever bar; only the fever bar =
                              step 44's mark), BandColor, ParseHex, Wounded, DescribeBar / DescribeMan, HiddenName
  AltNearMarks.cs             step 58, the ALT view decluttered pure (AI_NOTES "Step 58"): AltNearRules (live: AltMarksEveryone, AltNearMarksMax,
                              AltNearMarksMetres, FeverMarkMax; Cap = min(near max, FeverMarkMax) / FeverMarkMax, ModeName, Describe), AltNearMath
                              (Facing = cos of the angle from the camera's forward, CentreKey = 1 − cos - the pick's key, AngleDegrees, InRange, Fade -
                              none in the near view), FeverLabelHidden / FeverLabelRules (ModOff FIRST) / FeverLabelMath ("fever 12/40", the fever
                              orange, HiddenFor - nobody in a fever = hidden) / FeverLabelStats (+ the [summary] "fever share" line). Also step 58:
                              FeverMark.Facing, FeverMarkSkip.TooFar (before OffScreen), FeverMarkFrame.Elect(index, key), FeverMarkStats.NoteMode
                              + the summary's "mode (step 58)" clause; FormationAthleticsStats.InFever (SpreadStats.cs)
  ImpactSpike.cs              step 47, IMPACT I1 pure (AI_NOTES "Step 47", RESEARCH §R10): SpikeLever (the six), SpikeVerdict (Unseen /
                              Works / Fails), StaggerKind, ForceRefusal (Gone, NotHuman, Mounted, GameObject, Water, Down - in the
                              order asked), WatchKind / WatchEvent, ActionRead (both channels' type + index), SpikeHit (one collision),
                              ImpactActions (the engine's codes - the smoke checks them; IsStrike / IsFall / IsRecoil / IsDefend),
                              SpikeRules (live: ModEnabled FIRST, ImpactSpike; Describe), KickPlan, SpikeMath - the loud values as
                              documented constants (StunFactor 3, StunCapSeconds 2, ReactionSpeed 0.5, every 10 / 20 s, 1 damage, the
                              windows, the pools, MaxActionsPerTick 20), Kick (the 4-kick rotation: KnockDown untouched / reel untouched /
                              KnockDown slowed / reel slowed), StunTreated (an EVEN blocker index), StunAsked, Due, IgnorePriority (it
                              alternates), KickLanded, CountVerdict / LengthVerdict / Combine, the names, NextStep (what each verdict means
                              for steps 48-53 - IMPACT §7's fallbacks); ReactionWatch (a struct state machine over the channel reads:
                              Appeared / NewAction - a _continue - / Ended with its length / NeverAppeared / Capped)
  ImpactSpikeStats.cs         step 47: BlockStunTally (lever 2's worker-thread counters - Interlocked only), ImpactSpikeStats (per lever:
                              asked, seen, the lengths treated vs control - MeanStd -, refusals, disabled levers, the cost) + Verdict
                              per lever + the 7 [summary] "impact spike" lines (one when never on)
  Impact.cs                   step 48, IMPACT I2 THE MODEL pure (AI_NOTES "Step 48", docs/IMPACT.md §3): ImpactEvent (kick, bash, right-side
                              block, parry, wrong-side block, clean hit), ImpactFamily (kicks / blocks / hits / riposte - one switch, one
                              effect step each), ImpactDamageType, ImpactOutcome, ImpactRefusal, ImpactFacts (one event's inputs - a
                              struct the module fills), ImpactState (one man: reeling / stagger-immune / down / knockdown-immune until, his
                              last 6 outcomes; Reel, KnockedDown, EndReel / GotUp - the measured ends, step 50 on), ImpactRoll, ImpactDecision (x and
                              its parts, the four rolls, the lengths), ImpactRules (live: ModEnabled FIRST, AthleticsEnabled, ImpactEnabled, the
                              families, ShadowNow = the switch or the spike, Applies = decided && !shadow && the effect built, step 50:
                              KeepsState = Applies || (decided && shadow) - a live family with no effect built decides on copies; ModeText,
                              Describe - the anchors in words), ImpactMath (EventOf, BarBand, S, Curve, SkillFactor, WeightFactor, SwingImpact,
                              Impact, Guard, Body, Band, Lengthen / Spread, Pressure, RipostePressure, Ceilings, Decide - THE one decision per
                              event, Describe - one decision in full; plumbing SkillCap 300, MaxRemembered 6, VanillaReelSeconds 0.45,
                              VanillaFallRiseSeconds 1.5, EffectsBuilt = Kicks since step 50, + Blocks | Riposte since step 51); step 51:
                              ImpactRules.DecidesEvent (a right-side block / parry decided for the riposte alone when only the blocks are off),
                              CanRiposte, BlockStunFactor (1 + extra × T - NO die: the worker-thread seam applies the decision's number),
                              LengthenedStun (the game's stun held at its cap first, × the factor, at most ours); step 52: EffectsBuilt + Hits (every
                              family live), ImpactFacts.AttackerSpeed / TwoHanded, ImpactDecision.AttackerSpeed / Momentum, the momentum rules,
                              MomentumShare / MomentumFactor (1 + % × clamp((v − walk) ÷ (sprint − walk)); 1 for a rider / off) / SpeedBand (standing /
                              walking / running / sprinting), BlowImpact (Impact = it × the momentum)
  ImpactStats.cs              step 48: ImpactSkip, ImpactStats (by event × the defender's bar band: decided, x, per outcome in the row /
                              rolled / chance / happened; the riposte by the attacker's band; refusals; halvings; dismounts; shadow / live /
                              applied - step 50: AppliedByEvent, "did" per event; lengths; cost) + the [summary] "impact" lines (one when never on)
                              step 68: KickLanded / KickIntoShield / KickOther by kick / bash, KickLandedByPlayer, KickSkipped by reason (Skip counts a kick's
                              own), NoteKickHit, KickCoverageLine - the [summary] "impact kicks / bashes landed vs decided" line (unaccounted must be 0)
  ImpactLive.cs               step 50, IMPACT I3 pure (AI_NOTES "Step 50"): ImpactLever (KnockDown / ShieldDrop / SlowReaction / Watch - each
                              disabled alone), ImpactNotApplied (rider, outside the seam, lever off, pool full, no shield at the tick, left the
                              field, busy in his own release / bash), ImpactWatchPurpose (reel / get-up), ImpactLiveMath (SpeedFor = 1 ÷ factor,
                              min 0.25; NeedsSlow > 1.001; PickupLocked; plumbing = the spike's pools / windows, MaxActionsPerTick 20,
                              BusyWaitSeconds 1), ImpactLiveStats (decided → applied → seen per outcome by the kicked man's band, not applied by
                              reason, the reel / get-up asked vs measured + untouched controls, shields dropped / picked up / locked, the
                              limits, levers, the tick) + the 4 [summary] "impact live" lines; step 51: + the levers BlockStun / ForcedStagger /
                              OwnBlow (LeverCount 7), ImpactNotApplied CannotForce / EngineRefused, the watch purposes Stagger / Riposte /
                              BlockGetUp / Guard; step 52: + the lever ShrugOff (LeverCount 8), the purposes HitFlinch / HitGetUp / HitControl
  ImpactBlocks.cs             step 51, IMPACT I4 pure (AI_NOTES "Step 51"): ImpactBlockMath (the three block kinds, StaggerSpeed = natural ÷
                              asked 0.25-2, StaggerNaturalGuessSeconds 0.9, MaxGuardWatches 16, MaxPending 64, the YOU words), StaggerNatural (the
                              forced stagger's own length LEARNED from measured × speed), BlockStunLiveTally (the stun seam's worker-thread counts -
                              Interlocked), ImpactBlockLiveStats (per block kind × the blocker's band × outcome decided → applied → seen, not applied
                              by reason, the staggers asked vs measured, BLOCKS MADE FROM INSIDE A STAGGER, the blockers knocked down, the riposte by
                              the attacker's band, the guard asked vs measured, the cap, limits / YOU / shock) + the 8 [summary] "impact live" block lines
  ImpactHits.cs               step 52, IMPACT I5 pure (AI_NOTES "Step 52"): ImpactHitMath (the weapon classes cut / pierce / blunt / two-handed,
                              ShrugTired / ShrugOffThreshold = the game's × (1 − T) / StillShrugsOff, FlinchFactor = asked ÷ the flinch's own length
                              (≥ 1), FlinchNaturalGuessSeconds 0.45, MaxControlWatches 8, the YOU words), FlinchNatural (the flinch's own length LEARNED),
                              ImpactHitLiveStats (clean hits per class × band - knocked down / staggered decided → applied → seen, knockdowns on REELING
                              men vs the rest and on shrugged-off blows, the shrug-offs refused by band, the flinch asked vs measured vs controls, the
                              get-ups, limits; MOMENTUM - every decided blow of every family by the attacker's speed band, all men and below half a bar)
                              + the [summary] "impact momentum" line and the clean-hit lines; step 53: the momentum line's RIDERS columns
                              (RiderBlowsBy by the horse's band, couched lances apart)
  ImpactRiders.cs             step 53, IMPACT I6 pure (AI_NOTES "Step 53"): ImpactDismountWay (the seam / our blow), ImpactRiderMath (SeenWithinSeconds
                              2, the pools 32, the families), ImpactRiderLiveStats (decisions on riders by family, dismounts decided by band → applied by
                              way → seen / not seen, why not, the riders' shields, staggers / ripostes refused + the 2 [summary] "impact riders" lines),
                              ImpactMessageKind / Role, ImpactMessageMath (the words, the game's colours #D65252 / #AE4AD9 / white, Stronger,
                              NotableForYourMen), ImpactMessageQueue (the rate limit + merge: 3 parts, "(+N more)", duplicates once, Clear counted + the
                              [summary] "impact messages" line); Impact.cs: ImpactFacts.AttackerPassive, the rider momentum rules, MomentumFactor(…,
                              mounted, passive), RiderSpeedBand; ImpactLive.cs: ImpactLever.Dismount (LeverCount 9), ImpactNotApplied.NoHorse
  ImpactPunish.cs             step 54, IMPACT I7 pure (AI_NOTES "Step 54"): PunishRules (live: ModEnabled FIRST, Athletics, ImpactEnabled, not shadow /
                              the spike, a family on = WindowsLive - measured then, ON or OFF; + ImpactPunishEnabled = Enabled; Share in 0.05 steps;
                              Describe), PunishMath (WindowEnd = max(reeling, down), InWindow + the grace, Raise = max(his share, P) - never the player,
                              TimidPresses within the range / not in a panic, AnyWindowSlackSeconds 1), PunishStats (windows opened / seen / punished by
                              victim - AI men / you -, attacks and man-seconds by punish on / off x target in a window / not, raised / ended / timid
                              presses, target reads, cost + the 4 [summary] "impact punish" lines); Courage.cs: CourageEnd.PunishWindow
  ImpactFalls.cs              step 62 pure (AI_NOTES "Step 62"): ImpactFallMath (BackupDue, Fatal - health − damage < 1, MaxBackupSeconds 3),
                              ImpactFallStats (decided by kind - kicks / clean hits -, played by the game / by our backup blow with their delays, still
                              not seen, left the field first, watch replaced, 0-damage blows, backups sent / refused / failed, the shrug-offs refused for a
                              fall / a stagger / decided with nothing, taken by the KnockDown seam, killed + the [summary] "impact falls" line); Impact.cs:
                              step 62's facts (Fatal, both toughnesses, a ranged weapon in hand, AttackAngleDegrees, AttackerCharging), rules, decision
                              fields (ToughnessRatio / Factor, RangedFactor, Direction / DirectionFactor, Extra, Capped, Charging / MeasuredSpeed /
                              ChargeShare / RiposteMomentum, ThrownBack + its get-up, Damage), EffectiveSpeed, ToughnessFactor, RangedFactor,
                              DirectionOf / DirectionFactor, CapMultiplier, the riposte's reflected momentum and recoil by max(T_A, x_r), the thrown back
  ImpactCharge.cs             step 62: ImpactChargeStats - landed blows by direction and by toughness band (+ men with a bow, the capped decisions), the
                              CHARGES by what they met (a right block / parry / wrong block / landed front / side / rear: the attacker reeled / thrown
                              back, the defender staggered / down; vs the blows that did not charge), thrown back applied / seen + the [summary] "impact
                              direction / toughness / charges" lines; DamageStats.AddMeleeCharge + the "damage critical" line (DamageRoll.ChargeUpside);
                              step 63: DirPressure (the direction line's "+0.50 pressure"). Impact.cs step 63: ImpactFacts.NotEnemies (a friend's
                              blow - never decided, ImpactSkip.NotEnemies, ImpactFallStats.Friendly), ImpactDecision.DirectionPart,
                              ImpactRules.RearPressure / FlankPressure, ImpactMath.DirectionPressure (x += it on landed blows); step 64:
                              ImpactChargeStats.CarriedByAttack (NoteCharge(…, byAttack) - the charges line's "by an attack begun in the charge")
  Courage.cs                  step 49 pure (AI_NOTES "Step 49", RESEARCH §S): CouragePhase (brave / walking back / holding), CourageSkip (in the
                              order asked), CourageEnd, RegroupEnd, CourageStep, CourageSprintRefusal, ShockTimes (his last 4 landed hits), CourageRequired
                              (the terms), CourageRules (live; ModEnabled FIRST, CourageEnabled; Athletics only for the tiredness term; Describe with the
                              looter / legionary numbers), CourageMath (BaseToughness, Toughness, EnemyToughness (the heavy x), Weight (full to half the
                              radius), Odds, FeverShare, ShockShare, Panic, MatesNeeded / RegroupRadius (shocked x2 / x0.5), Required, Timid, BraveAgain,
                              WantsToMove, MovesAgain, Arrived, SprintWanted, SprintAmount (the reserve; a panic's), Direction (to the target + away, round
                              the enemy), TimidMelee / CurveAt, the bands and names; plumbing PollSeconds 0.5 …), CourageMind (one man: Look → the step,
                              StopMoving, End, the walk's target / direction, flips and wobbles, ShockStart / FlockTarget / Panic)
  CourageStats.cs             step 49 per-mission: isolated / in-contact man-seconds ON vs OFF, the distance histogram (mean, p90), odds bands in contact and
                              out of the formation, skips, ends, walks' ends + time to regroup, sprints + refusals, shock vs odds, flock vs centre, panic,
                              compression (a walk's distance start → end by shock), toughness, weights, cost + the 10 [summary] "courage" lines
  FearFlock.cs                step 67 pure (AI_NOTES "Step 67", DESIGN decision 60): FearOrder (hold / wall / advance / charge / retreat), FearPhase
                              (calm / flocking / anchored), FearEnd, FearRules (live; ModEnabled FIRST, FearFlockEnabled; Athletics only for the
                              tiredness term; Describe with the looter / legionary anchors), FearMath (MoraleShare, TiredShare, Raw = 1 − (1 − m)(1 − t),
                              Discipline by level, ThinLine, Own, Mixed, Pull, Afraid + the hysteresis, OrderOf, DriftCap / DriftStep - the shelter's
                              backward cap, ShelterWanted, Place, AlongAcross / Covers / Behind / BehindSpot, Anchors / AnchorRunning, LooseRadius,
                              NearSlot (the 1 m dead zone), SprintWanted / SprintAmount, Stick, WallBreaks, UnitKey; plumbing ArriveMetres 0.7,
                              MoveAgainMetres 1.5, SteerSeconds 0.1, the table 8 × 16, PublishSeconds 0.5 …), FearMind (one man: phase, fear / pull,
                              his place, the wish, the anchor, the sprint), FearUnits (THE UNIT TABLE: Note per look, Publish every 0.5 s - the means,
                              "away", the SHELTER starting at the centre and creeping back by order, the shape / drift samples)
  FearFlockStats.cs           step 67 per-mission: FearStats - looks / contact / covered / nobody behind / alone ON vs OFF, spread and depth ÷ width ON vs
                              OFF, the centre's drift by order group ON vs OFF, fear / pull / afraid by discipline band, spells and ends, anchors, steps
                              behind a comrade, sprints, courage's yields, walls broken, the loose mass, near-slot looks, steering, cost + the 7 [summary]
                              "fear flock" lines; step 69: AddWin by odds band (losing / even to clear / clear / the unit's), calmed by winning, no
                              threat, the mop-up, the threat reads + AddWinLines (the [summary] "fear flock and winning" line)
  IdleMen.cs                  step 69 (AI_NOTES "Step 69", DESIGN decision 62) - THE IDLE-MEN DIAGNOSTIC, measurement only: IdleHolder (ranged - normal -,
                              the wall hold, the fear flock, courage timid, the brace, the game), IdleMind (per man: the last look's place / time, standing
                              since, the holders counted), IdleMath (EnemyMetres 25, ContactMetres 3, StandMetresPerSecond 0.5, Seconds 5, MaxGapSeconds 2;
                              Gap / Idle / Advance / Holder / MovingOrder), IdleStats (man-seconds, men and the enemy's distance by holder + the [summary]
                              "idle men" line)
  FearBand.cs                 step 70 (AI_NOTES "Step 70", DESIGN decision 63) - THE WIDE BAND: FearBandPlaceKind (no band / kept / compressed / slid /
                              full), FearBandMath (Ranks by the pull, Width = max(the enemy frontage, men ÷ ranks × the file) capped, LateralCentre - along
                              the enemy's front, FrontWanted / FrontStep - short of the enemy, pushed back by the pull, the drift cap back, Files, RankOf,
                              Slides, FreeFile - toward the ends on a tie; MaxFiles 64, MaxSamples 512, AlignCosine −0.5), the FearUnits partial (each
                              period's moments and samples with ids and bin points - a slid man's file; PublishBand - the direction (the enemy seen, the enemy
                              formation's facing), own / enemy front, the band's front point, depth, width, centre, files, each man's RANK in his file by id;
                              BandPlace - his lateral kept, depth by rank, the slide reserved; Fronts / PastEnemyFront - the lone runner's geometry;
                              NoteEnemyFront), FearBandStats (men per occupied file - the depth in RANKS - / the deepest / the width mean and p90 ON vs OFF,
                              files over the ranks, places by kind, steps in kept / refused, strikes + the [summary] "fear flock band" line)
  OrderClick.cs               step 72 (DESIGN decision 66) - NO ACCIDENTAL CHARGE ORDERS: OrderClickRules (ModEnabled FIRST, OrderNoAccidentalCharge,
                              DragPixels / IconPixels at 1080p; Describe), OrderClickMath (Scaled by height like vanilla's 70, IsDrag, OnIcon, VanillaPick -
                              vanilla's focus rule replicated: a unit within 10 m of the camera first, else the banner nearest the screen centre within 70 px),
                              OrderClickTracker (per click: Idle / Holding / Released; Disable = dragged (sticky) || pressed off the icon || off it now; the
                              hold's decision stands through the release frame; NoteOrder; outcome ChargeKept / ConvertedDrag / ConvertedOffIcon / PlainMove /
                              Leak / Aborted), OrderClickStats (the [summary] "order clicks" line)
  LineDiscipline.cs           step 73 (DESIGN decision 67) - LINE DISCIPLINE BY TIER: LineDisciplineRules (ModEnabled FIRST, LineDisciplineEnabled,
                              ForPlayer, the map T0-T6, HeroTier, RangedToo, InSieges, PreviewCap, ShowMessage, PollSeconds; Same, Describe),
                              LineShape / LineMission / LineSkip / LineFacts, LineDisciplineMath (TierOf, MinDepth = the mean, MaxFiles = ceil(men ÷ D),
                              GameMinFiles √men, CapFiles, Width / CapWidth (+ 0.05 m slack) / FilesOf = the game's FlankWidth and its floor, Ranks,
                              LastRankMen, Verdict in gate order, ShapeText), LineClampCause (Widened / AutoShrink), LineRecord, LineSideStats,
                              LineDisciplineStats (the [summary] "line discipline" lines, PreviewVerdict)
  BlockHold.cs                step 74 (DESIGN decision 68) - THE BLOCK HOLD + THE BLOCK DIAGNOSTIC: BlockHoldRules (ModEnabled FIRST,
                              LineDisciplineEnabled, LineBlockHoldEnabled, the numbers; Describe), BlockPull / BlockLookKind / BlockEnd, BlockHoldMath
                              (Strength by D, Loose, Ranks / Depth, Allowance, Decide - across / ahead / behind, the striker's leash, the hysteresis -, Lead,
                              Quantile; plumbing), BlockMind (per man), BlockUnits (the unit table: per period his place, slot distance, nearest enemy, step 73's
                              cap, the formation's reads; Publish - u from the fear band, the MEDIAN centre line, 10th / 90th spread, speed, near, first
                              contact; Frame / World extrapolated by the speed), BlockSideStats / BlockHoldStats (the [summary] "block hold" lines),
                              BlockShapeStats (the diagnostic's ring per formation, binned by the time from first contact - the "block shape" lines);
                              FearBand.cs: FearUnits.SetBlock / ClearBlock / BandBlockClamps - the band clamped inside its block
  LoneRunner.cs               step 70 (DESIGN decision 64) - THE LONE RUNNER: LoneCause (sprint into contact / punish / surge / fever press / chasing a target
                              behind the front / the game), LoneTarget (you / a hero / another), LoneRunnerRules (ModEnabled FIRST, LoneRunnerEnabled,
                              AheadMetres; Describe), LoneRunnerMath (Alone, Beyond, IsLone, Winning - the BEATEN: odds at the clear line and the enemy shaken
                              0.5+, Cause, FeverPresses / ForcesWalk / SprintEnds - the three fixes; EnemyMetres 8, PastFrontMetres 1), LoneMind (per man:
                              alone and when, lone, the cause, Forced - a walk back running), LoneRunnerStats (man-seconds and men by cause ON / OFF, targets
                              by whom, the fixes' counts + the [summary] "lone runners" line); Courage.cs: CourageMind.Look(…, lone), RoundSide,
                              CourageMath.Direction(…, ref side) - the side round the enemy kept for a walk; Sprint.cs: SprintEnd.LoneRunner
  MoraleEyes.cs               step 67 pure: MoraleMarkRules (ModEnabled FIRST, ShowMoraleMarks, the enemy by AthleticsMarksShowEnemy; Off), MoraleMarkMath
                              (the blue #4C9BFF, Fill = morale ÷ 100, Known), MoraleLabelRules / MoraleLabelHidden / MoraleLabelMath (the nearest
                              MoraleLabelMax within MoraleLabelMetres, "morale 64, fresh 41%"), MoraleLabelStats (+ the [summary] "morale line" line);
                              AthleticsMarks.cs: Decide(…, MoraleMarkRules) / Layout(…, hasMorale, …) - the morale bar at the stack's bottom
                              (MarkBox.MoraleWidth / MoraleHeight / FeverLift); FeverMarks.cs: FeverMark.Morale / HasMorale, FeverMarkStats' morale clause;
                              SpreadStats.cs: FormationAthleticsStats.MeanMorale; ShieldWall.cs: WallVerdict.Breaking
  CavalryFlock.cs             step 55 pure (AI_NOTES "Step 55", RESEARCH §T): RiderKind, FlockSkip, RiderFlockState (free / flocking / wave), FlockReason,
                              FlockEnd, FlockRules (live; ModEnabled FIRST, CavalryFlockEnabled; TimidOn = its switch && CourageEnabled; Describe),
                              FlockMath (KindOf, Key = team x formation x kind, Eagerness, IsFree + hysteresis, Readiness, GroupNeeded - THE CURVE, Heading
                              - THE BLEND, Unit, GoalVote, Flips, SlotOffset - the sunflower, SteerPoint, Reissue, Velocity, LonePass, EagerBand; plumbing
                              CensusSeconds 0.5, TimidEagerFactor 0.5, LeaderReadyShare 0.5, MinFlockSeconds 3, MinLeadSpeed 4, the weight curves ...),
                              RiderFlock (one rider: kind, key, state, slot, wave, census position / velocity, the point sent, passes), FlockTable (per
                              flock: riders, centre, the goal / centre of mass read once a census, slots), FlockGrid ("who is with me" - a hash grid)
  CavalryFlockStats.cs        step 55 per-mission: riders by kind, rider time FREE / FLOCKING / WAVE, starts / ends, flips + wave size by mean eagerness +
                              the leader's, time between passes by eagerness ON / OFF, lone passes ON / OFF, spread, the lever, cost + the 7 [summary]
                              "cavalry" lines
  Spears.cs                   step 76 (DESIGN decision 70) - SPEARMEN KEEP THEIR SPEARS + A THRUST PASSES A FRIEND: SpearSlot / SpearLoadout
                              (SpearMath.Classify - RBM's polearm + sidearm, thrown / consumable / ranged neither, the longest polearm), SpearContext
                              (mounted, speed, arrangement, order, rank, a man in front, standing firm), SpearCondition (nothing close / close / a back
                              rank / facing only polearms / riding through), SpearHeld, SpearFlip, SpearMind (per man: the loadout, Favored / Close,
                              RBM's two timers, the look clock), SpearRules (ModEnabled FIRST; Describe), SpearMath (CloseRadius, BackRank, RidingFast,
                              Condition, Step - the timers, Favors, HeldOf, NextLook - the jitter, WieldRewrite), SpearStats (the 2 [summary] "spears"
                              lines); ThrustVerdict / ThrustFacts / ThrustPassRules / ThrustPassMath (Decide in gate order, StunWanted, the vanilla 0.4)
                              / ThrustPassStats (the [summary] "thrusts past friends" line)
  WarCry.cs                   step 79 (DESIGN decision 75) - WAR CRIES, SOUND ONLY: WarCryRules (ModEnabled FIRST, WarCriesEnabled, 10 numbers;
                              Describe), WarCryCause (back in / answer / charge), WarCryGate (ok / man / unit / battle / gone / switched off / queue),
                              WarCryMath (Chance, Rolls, Delay, ManReady / NextAllowed, ChargeRoars - the unit's cooldown, ChargeShoutersPerUnit - the
                              battle's cap shared over the stagger), CryWindow (a fixed ring of the last 1 s), WarCryLimiter (the man's limit, the unit's
                              and the battle's caps, the peaks), WarCryQueue<T> (fixed arrays, swap-remove), WarCryStats (+ the [summary] "war cries" line)
  Rest.cs                     step 78c (DESIGN decision 74) - EACH MAN RESTS ON HIS OWN: RestRules (ModEnabled FIRST, AthleticsEnabled, RestEnabled,
                              15 numbers; Describe), RestIn (own mark / contagion / rotated / excluded / ...), RestMath (TiredMark / ReadyMark by order,
                              CappedReady - the wound cap, Starts, Ready, ContagionChance, WalkBack - the walk-back limit from his start and behind his front,
                              HoldsBack - within reach + 1 m and not 4.5 m+ behind his front, HoldsAttacks; plumbing WalkStick 0.6, the wall slot slack 0.5,
                              5 m far behind), RestStats (+ the two [summary] "rest (step 78c)" lines; GapLooks / GapClear by side - the natural-gap
                              diagnostic RhythmUnits feeds)
  Rhythm.cs                   step 78c: the lull REMOVED (its rules, math, unit-table parts, stats); RhythmRules = the rotation + RestEnabled (RestOn,
                              AnyOn), RhythmMind + the rest's per-man state, RhythmUnits = fronts / front bars / BehindFront / the men's mean axis / the
                              natural gap of engaged pairs (MeasurePair - a diagnostic). Before - step 78 (DESIGN decision 72) - THE BATTLE'S RHYTHM: RhythmRules (ModEnabled FIRST, AthleticsEnabled, the two
                              switches, 24 numbers), RhythmMath (tired / fresher / behind / covers / beside / busy codes / the lull by order), RhythmMind,
                              RhythmUnits (the unit table: fronts, front bars, the enemy unit, the lulls' start / lines / ends; EndLull - 79's wave),
                              RhythmStats (the two [summary] "battle rhythm" lines); step 78b (decision 73): LullTrigger, THE BREATHER CLOCK (PairGroup,
                              ClockSeconds, ClockBar, Worn, RateFallen, Trigger), MeanWithin (the unit's bar), THE GAP FROM THE MEN (MeasureGap -
                              2 m bins -, StartLines, GapHolds), RhythmUnits' UnitBar / ClockRoll / ClockAt / RateEma / RatePeak / NoteAttack / MeasurePair /
                              the line steps / the charge grace / Opened, RhythmStats' trigger / open-time counts + the third [summary] line
  PressDontPass.cs            step 77 (DESIGN decision 71) - PRESS, DON'T PASS: PressRules (ModEnabled FIRST, PressDontPass, 15 numbers; Describe; step 78b:
                              WrapAllowed, WingBendDegrees - WingPlane / FrontDistance, PressAction.Wing / Ordered, Decide(..., ordered), the third
                              [summary] line "NO WRAPPING"),
                              PressLineKind (none / slots / wall / block / ring), PressAction (no line / far / CLAMP / PUSH / RETURN / through / wrap /
                              broken / winning), PressCause (courage / fear / block / charge target / the game), PressEnd, PressOrder; PressMath (LineKind
                              in gate order, FrontOf - one face's front = the median of each man-wide bin's front-most man, the ends, the gaps no front
                              man covers; InGap, UnitWraps / ManWraps, Decide / ReturnPlace / DecideRing, ClampVector - the input clamp, BandWidth /
                              BandRanks, BlockRange, CauseOf; plumbing LookaheadMetres 0.6, CrossedMetres 1, BehindExtraMetres 1, PushToMetres 0.3,
                              ReturnOvershootMetres 1, MaxGaps 8, the 0.5 s period), PressMind (per man: the wish, the line / face, the crossing watch),
                              PressLines (THE LINE TABLE: Note / Vote per look, Publish - the line test and both faces (a ring's centre and radius),
                              each unit's enemy line by its men's votes, its face with a hysteresis, its width ((q90 - q10) / 0.8, a held block's cap at
                              most) and front, wraps kept once begun; Frame / World / Inward / Across / RingFrame / Ends), PressStats (the two [summary]
                              "press, don't pass" lines); FearBand.cs: FearUnits.PressBandMarginMetres / PressBandClamps / PressBandExtraRanks (the
                              band never wider than its enemy formation + the margin, its ranks enough for all its men), FearBandMath.RanksFor;
                              BlockHold.cs: BlockHoldMath.DecideRange (an asymmetric lateral range - symmetric = Decide)
  SpreadStats.cs              MeanStd (Welford, population std), FormationAthleticsStats (squad
                              mean ± std, band, mean f, at full strength, mean health - step 9; step 23:
                              Bracing / Ready / Total - the next step's "ready men"),
                              RunSpeedCheck (engine top / asked / moving by f); 5c's
                              attack-interval classes went in 5e
  AthleticsStats.cs           per-mission Athletics counters + the [summary] text (pools from the
                              skill, blows by kind, riders, detection cross-checks, kicks / bashes
                              charged apart - by channel, at the hit, free (step 18), blocks paid by
                              the defender - by kind, by you / AI heroes / other AI, free, off, the
                              same blow again, missiles into a shield (step 22), the delay restarted vs
                              kept running by kind (step 29), free actions,
                              exhaustions + peak zone, fighter-time by f, heroes, player,
                              formations, health cap, regen by effort and its curve, refills from
                              empty to the peak line (step 14), run-speed checks by f,
                              recomputes, walk vs run speeds, tick cost, errors)
src/TraxCombat.Module/        net472 — the Bannerlord module, TraxCombatEnhancements.dll:
  SubModule.cs                entry point: load log (+ "module: <Id>" - which copy runs), config
                              init/re-reads, MCM register/retry (the module list handed to McmBridge -
                              step 12), the two model decorators
                              (OnGameStart), AthleticsLogic per mission + its PlayerAttackGate at
                              index 0 (step 13); step 11: claims SingleCopy
                              FIRST in OnSubModuleLoad - refused = _inert (an INSTANCE flag: one
                              assembly may serve both modules), every hook returns at once; the
                              running copy's ReportCopiesOnce ([compat] + one message, main menu)
  ModPaths.cs                 Configs\TraxCombatEnhancements\ via EngineFilePaths.ConfigsPath
  ConfigStore.cs              config.json ↔ TraxSettings.Shared: first run, re-read at game and
                              mission start, write after MCM Done by the rewrite rule, backups;
                              the [config] defaults: line; RevertAllToDefaults, ExportDefaults;
                              step 13: ConfigFile.Migrate at every read (logged "migrated config.json …",
                              the file rewritten as the current format) and on the MCM-save re-read
  TraxLog.cs                  trax_combat.log: tagged lines, trimmed past LogMaxMegabytes (read live) to
                              half by LogTrim (a failed trim retries after 1 MB), Verbose (rate-limited,
                              only when VerboseLogging, written "~[tag]"), VerboseWants(bucket) (R8: the
                              hot paths ask it before building a line), Limited (always, rate-limited per bucket),
                              Error (stack, rate-limited, in-game notice); ONE handle kept open
                              (AutoFlush, shared read/write/delete - read it with FileShare.ReadWrite),
                              Release() at every mission end and at unload (step 10a)
  Mcm/McmBridge.cs            the MCM PAGES — fluent builder, MCM types in METHOD BODIES ONLY,
                              no MCM-typed lambdas (read its class doc before touching it);
                              step 12: first try at the main menu (MCM builds its services there), none
                              with MCM's module off, retries 1/s capped by Core McmPlan;
                              step 59: TWO pages (BuildPage returns object; MCM keeps settings by Id) - the
                              player page (SettingsId, the old id, "Trax Combat Enhancements": 21 settings since step 73,
                              7 groups, then "Defaults" with Revert) = _settings, the Advanced page
                              (AdvancedSettingsId, "... - Advanced": 337 settings since step 68, 16 groups) = _advanced;
                              each its own "default" preset (its own settings); group "Defaults": the Revert
                              (both pages, reverts both) / Save-defaults-file (Advanced only) BUTTONS
                              (ProxyRef<Action>, both pages refreshed via PropertyChanged), on Advanced just
                              above Advanced (MCM order = 2 x the schema's; MCM's UI sorts ascending)
  Models/TraxDamageModel.cs   AgentApplyDamageModel DECORATOR — forwards everything; overrides
                              ApplyGeneralDamageModifiers: BaseModel first, then the roll
                              (our exceptions → the game's value); step 47: DecideAgentKnockedDownByBlow = the base's answer OR
                              ImpactHooks' decider, CalculateDefendedBlowStunMultipliers = the base, then ImpactHooks (any thread); step 52:
                              DecideAgentShrugOffBlow = the base's answer, a YES asked again of ImpactHooks.ShrugOffKept (main thread); step 53:
                              DecideAgentDismountedByBlow = the base's answer OR ImpactHooks.DismountOnBlow (main thread)
  Models/ThrustPass.cs        step 76: IThrustPassReads (IsFriendOf, a polearm in hand - the smoke plays them) and ThrustPass.Passes - the damage
                              decorator's DecideWeaponCollisionReaction asks it BEFORE the base model: a thrust into a friend = ContinueChecking; every
                              friendly contact and the enemy thrusts of a man who just passed one handed to the logic; HoldStun / RestoreStun (the
                              game's StunPeriodAttackerFriendlyFire remembered once). TraxAgentStatModel.ApplySpearFavor (UpdateAgentStats and
                              InitializeAgentStats, after everything): AthleticsLogic.SpearFavorFor - one favor from his flag
  Models/ImpactHooks.cs       step 47, the impact system's MODEL seams: IKnockDownDecider (main thread, every collision on a man on foot)
                              and IBlockStunDecider (ANY thread - reads only) plugged in by the running logic; KnockDownOnBlow (OR-ed,
                              a throw = the game's answer), AfterDefendedBlowStun (a throw = the base's stun kept, the exception handed
                              to the main thread - TakeStunFailure; while our cap is raised every stun nobody lengthened keeps the game's
                              own cap), RaiseStunCap / RestoreStunCap (ManagedParameters StunPeriodMax, the game's value remembered once,
                              a higher cap left alone), InOwnBlow ([ThreadStatic] - our blow inside RegisterBlow); step 50: IShieldPickupLock +
                              PickupLock / ShieldPickupLocked (a throw = the game's answer); step 52: IShrugOffDecider + ShrugOff / ShrugOffKept
                              (the base model's CalculateStaggerThresholdDamage asked only when a decider is plugged; a throw = the game's answer);
                              step 53: IDismountDecider + Dismount / DismountOnBlow (OR-ed; a throw = the game's answer)
  Models/TraxItemPickupModel.cs  step 50: ItemPickupModel DECORATOR (registered only over an existing one) - forwards everything;
                              IsItemAvailableForAgent also refuses a SHIELD to an AI man within ShieldPickupLockSeconds of a live kick
                              knocking his away (only the AI asks this model; the player's use key never)
  Models/DamageRandomizer.cs  feature 1, game side: game structs → HitFacts → Decide → roll with
                              the attacker's f (the rider's on a horse charge) as the upside,
                              stats, [damage] lines (mission start, first roll + thread,
                              verbose roll/skip), the [summary] damage block; step 25: Reduce -
                              AFTER the roll (or on the game's value when the roll is skipped),
                              DamageReduction's rules, the first reduced hit logged in full, ", reduced
                              → N" on the verbose lines, the reduction in the mission-start line
  Models/TraxAgentStatModel.cs AgentStatCalculateModel DECORATOR — forwards everything;
                              UpdateAgentStats: base first, then the attack ANIMATIONS × the line
                              A + (1 − A) f, f read off the applied m (step 20b; A =
                              AttackAnimationMinPercent, 100 = full speed - step 13), the run
                              multiplier (+ the AI's attack values while AttackRateAiDecisions,
                              5e - off by default since 13), or a slowed rider's horse's
                              (SpeedFactorsFor - managed reads only); step 30: a tired AI man's
                              AiRandomizedDefendDirectionChance = TiredBlockMath.Chance(the base's, w) - never
                              the main agent; step 43: an AI man on foot's two melee attack-decision chances × his aggression
                              share (SpeedPenalty.ScaleAttackDecisions; never the main agent or a rider); + the tournament
                              SetAILevelMultiplier fix
  Models/SpeedPenalty.cs      the penalties on AgentDrivenProperties: attack (swing, thrust/draw,
                              reload), the AI's decisions (5e: attack / riposte / shoot chance x m,
                              the aim ÷ m), run (MaxSpeedMultiplier), horse (MountSpeed) - nothing
                              else (never handling or any defence value) + Snapshot / AiSnapshot
  Missions/AthleticsLogic.cs  MissionLogic in every SP mission (partial - with .Engine, .Api,
                              .Log, .StepBack, .AttackRate, .Hud): lifecycle, start/end
                              lines ("mod ON/OFF"), master-switch toggles ([mission] line each),
                              damage stats reset (AfterStart), the [summary] block
  Missions/AthleticsLogic.Engine.cs  the Athletics engine: per-agent state (by Agent.Index +
                              dense array; a man leaving, a stale record at a reused index and a
                              deleted agent are all dropped by Forget - 10a), the Athletics skill +
                              hero/leader flags at spawn, blow
                              detection (poll ReleaseMelee, OnMeleeHit, OnAgentShootMissile,
                              OnMissileHit), step 18's kicks / bashes (channel 1 + channel 0 on foot
                              - ObserveLowerAction; the hit reads both, then KickOrBashHit;
                              ChargeKickOrBash - no timer, no step back; the YOU line), step 22's
                              blocks (OnMeleeHit's LAST try: BlockHitTaken → BlockMath.KindOf →
                              BlockTaken - the defender's BlockTracker, ChargeBlock, AfterCharge; the
                              YOU line per kind - step 29: "the refill keeps running" or "restarts the refill
                              delay"; OnMissileHit counts missiles into a shield), the health
                              cap (OnAgentHit + every regen step), regen
                              by effort, three speed multipliers re-targeted in 0.05 steps and
                              applied by UpdateAgentProperties (≤ 50 a tick), the horse table
                              (OnAgentMount/Dismount), hot swap, SpeedFactorsFor (the decorator's
                              lookup), the run-speed and walk/run sampling, Failed
  Missions/AthleticsLogic.Api.cs  READ API for steps 6-9: TryGetReading(agent) (points, pool,
                              usable pool, f, peak line, multipliers), TryGetPeakShare(agent),
                              TryGetFormationStats(formation) (incl. mean f and health; the player
                              left out, as the order cards; step 20: EVERY team's formations - slots by
                              Team.TeamIndex, MaxStatTeams 8, one allocation-free pass; step 23: + how many brace),
                              FormationStatsVersion, IsRunning; step 44: TryGetFever(agent, out armed, out share) (an
                              AI man's running fever - never the player, false with the fever off) + its allocation-free
                              walk FeverWalkCount / TryGetFeverAt(i) (0 while no fever runs); step 46: MarkWalkCount(everyone) /
                              TryGetMarkAt(i, everyone, rules, bands, ref FeverMark, out agent) - his fever and his Athletics bar's
                              reading (never you, never a man gone)
  Missions/AthleticsLogic.Log.cs  [athletics]/[speed] lines (verbose buckets) + summary feed
                              (releases every step back before the summary, then its lines)
  Missions/AthleticsLogic.StepBack.cs  step 5d bookkeeping: the roll at every counted swing's
                              END (EndRelease), the queue started from the tick (never in an
                              engine callback), the cap, time read live, every release path
                              (time, order/arrangement/formation change, detach, player, mount,
                              rout, switch-off = all at once, mission end; left the field = no
                              engine call; handed over to a game job = never disabled), the
                              guard count (OnMeleeHit), [stepback] lines, the first one in full;
                              step 16: the body by technique at the START (StepBackBackpedal), the
                              backpedal's wish on / off (on EVERY path; step 17: one the game takes over
                              is still released - our callback off, R27), Steer every tick (the vector,
                              arrived, edge ahead), every-0.25 s samples (the first's written into its
                              end line), a hold running then marked overlapped, the switch logged
  Missions/StepBackBody.cs    IStepBackBody = the ENGINE side of the step back behind one seam
                              (the smoke plays it); GameStepBackBody: mission kind (battle mode,
                              no tournament/arena by behaviour NAME, no naval), vanilla's gate
                              CanBeAssignedForScriptedMovement, orders, target, navmesh checks,
                              SetScriptedPositionAndDirection / DisableScriptedMovement, flag
                              checks; StepBackState / Plan / Snapshot / Release; step 16: Steer (None
                              here), InputStepBackBody (the backpedal: the same probe, hooked instead of a
                              frame, Steer = distance along the checked line + the ground 0.6 m further
                              back every 0.25 s + the away line in his body frame; a game GoToPosition =
                              handed over; release = the input stops, the callback off if idle)
  Missions/AiInputHook.cs     step 16: AiInputState (the logic's wishes - hold, step hold, backpedal +
                              the vector - and the hook's counts / the first man's captured frames),
                              AiInputComponent (AgentComponent.OnAIInputSet: idle = one bool; never the
                              player or a non-AI agent; nothing thrown to the engine), AiInputHook (Hook
                              - the component added lazily FROM THE TICK + the engine's callback on;
                              UnhookIfIdle - off only if ours and no other component overrides the hook
                              (RTS Camera's); SetHold / SetBackpedal / Clear; Apply - the managed filter
                              the smoke drives; FlagNames); step 23: the brace wish (BraceHoldAttacks - melee
                              only: BraceRanged / a ranged action let through; BraceShieldInHand → the shield
                              held up, RaiseShield), SetBrace, the brace's frame counts; Active = ANY wish; step 49: the Regroup wish
                              (RegroupX / Y - a timid man's walk back; SetRegroup, ApplyRegroup - a step back's backpedal wins; the component lets it
                              through with Athletics off); step 63: the WallPull wish (WallPullX / Y - a wall man out of place walked back to his
                              slot; SetWallPull, ApplyWallPull - before the regroup, a step back's backpedal wins, the master switch and
                              ShieldWallHoldsSlots read live; through with Athletics off too)
  Missions/AthleticsLogic.AiHolds.cs  step 16 shared bookkeeping: EnsureInput, NoteHooked, the
                              component's errors logged from the tick, the GUARD by state (OnMeleeHit),
                              the [summary] "AI holds" lines, HoldsHeader (the technique in the header)
  Missions/AthleticsLogic.AttackRate.cs  steps 5e / 13: every channel-1 action change closes / opens
                              a phase (filed at the band at its start, BEFORE the action's charge)
                              and builds the attack's D (wind-up + release, ranged + the reload),
                              flagging its END; AttackEnded (after the step-back roll) → yours or the
                              AI TIMER (the old pace hold: NoAttack for D x (1/m − 1) after every
                              attack, melee and ranged, riders too - queued, started by TickPace,
                              lifted on every path: time, an attack slipping through, switched off,
                              left the field, the player took him, mission end, a game job waited
                              out); AttackBegan (the gap after the last timer); the ready-progress
                              poll (wind-up vs held); cycles; the guard by f; the switch notes (AI
                              decisions / the animation floor re-applied); [rate] lines (mission
                              start, the first slowed fighter's values, the first AI timer), the summary;
                              step 16: the body by technique at the START (AttackRatePaceByInput - a
                              waiting NoAttack keeps its own), the wish on / off on every path, THE TIMER
                              SURVIVES A STEP BACK (R1's drop gone: by input at once; NoAttack behind a
                              scripted walk DEFERRED - TickDeferred sets it the tick the walk ends or counts
                              it covered), the never-called warning, the first hold's input frames;
                              step 21: AiAttackEnded → NoteClassAttack + BattlePaceMath.Plan → AskPause (the
                              old decision, a reason literal when no pause; a FRESH man is held when his class
                              has a share), the pause's parts in PaceState, the share in the timer rows and the
                              first timer's line
  Missions/AthleticsLogic.BattlePace.cs  step 21: ReadAttackClass at each release (AI only; a failing read =
                              the plain class of its kind, one [error]), NoteClassAttack, SetPaceModel,
                              PaceNotStarted (a refused / superseded pause), NotePaceCycle (by class, the cap
                              + the share inside), NotePaceGap, the settings line at mission start and on a
                              change, FirstOfClass (one [rate] line per class), WriteBattlePaceSummary
  Missions/AthleticsLogic.SwingStack.cs  step 32: the clock (StackOnAction before the phases, StackRegenSample in the
                              regen pass - + the fill by f, StackWaitOver for a waiting AI man, RetargetPlayerWait every gate
                              frame), SpendSwing (releases, kicks / bashes; the YOU lines), StackClamp (after every charge),
                              AskStackPause (the AI: a swing stored = no pause + his own gap as grace; 0 = AskPause with the
                              wait), AskStackWaitAfterKick, PlayerStackHoldIfEmpty, KickGate (your kick bit out at 0),
                              NoteStackSettings, StackPass (the smoke), WriteSwingStackSummary; the player timer, the AI timer
                              (PaceState.StackWait), AiInputHook (HoldKicks / HoldKick) and the recovery read carry the rest
  Missions/AthleticsLogic.Sprint.cs  step 33: TickSprint (from TickAthletics BEFORE the recompute loop: switched off →
                              EndAllSprints; your key every tick - a PRESS starts, letting go ends; PollOrders every 0.25 s -
                              a formation's kind changed = a new order → OrderChangedFor its AI men; ForgetOrders while the AI
                              sprint is off), SprintPass every 0.1 s (Clamp to his points, RunSprint - drain while moving, melee
                              every 0.25 s, the run factor into TrackedAgent.SprintRun in 0.05 steps → SpeedDirty -, Refill),
                              BeginSprint / EndSprint (every path), SprintLeftField, SprintFreshStart (step 19), CloseSprints,
                              WriteSprintSummary, TryGetPlayerSprint (the bar); step 36: OrderChangedFor → TryStartAi (step 37: all
                              above the order's reserve), reserve kept counted + its first line; step 35: SpeedFactorsFor puts SprintRun IN PLACE
                              of the run and hands the decorator SprintTopSpeedReachSeconds (+ remembers the game's reach,
                              TrackedAgent.GameReach); the floor and the acceleration in the first-sprint lines; a sprint setting
                              change marks every sprinter for a recompute;
                              RegenPass skips a sprinter; the recompute budget's start ROTATES (the first man left waiting); step 43:
                              PollOrdersFor - the order poll runs while the AI sprint OR the fever is on and hands each changed
                              formation's men to both (SprintFormationRead.ByPlayer = !Formation.IsAIControlled)
  Missions/SprintBody.cs      step 33: ISprintBody (the smoke plays it) + GameSprintBody (formations by Mission.Teams /
                              FormationsIncludingEmpty, the order by GameBraceBody.KindOf, motion, GetNearbyEnemyAgentCount,
                              the key through Mission.InputManager.IsKeyDown(InputKey) - KeyFor, PlayerCanSprint = a fight
                              mode and the order menu closed), SprintBodyDefault
  Missions/AthleticsLogic.Brace.cs  step 23: StartBrace / NoteBraceSettings (the [brace] lines), TickBrace (switched
                              off → EndAllBraces at once; else a staggered slice so each man is looked at ~every
                              0.25 s), PollBrace (not AI / the player → none; his two rolls once - the margin on BraceDice,
                              the floor on BraceFloorDice (step 28); the order → the band →
                              BeginBrace / EndBrace / KeepBracing), BeginBrace (EnsureInput, Hook, SetBrace, the
                              hands, the shield's first wield, the first brace in full), KeepBracing (hands every
                              look, the shield seen / put away, a wield every 1 s up to 3), EndBrace (every path;
                              the callback released if idle), BraceLeftField, BraceFreshStart (step 19),
                              CloseBraces (mission end), BraceHitTaken (the guard), NoteBraceRelease, WriteBraceSummary
  Missions/AthleticsLogic.TiredBlock.cs  step 30: StartTiredBlock / NoteTiredBlockSettings (the [guard] lines),
                              TiredBlockWeight (RetargetSpeed carries w with the speed multipliers - same 0.05 steps
                              and recompute budget; RetargetSpeed is an instance method since), NoteTiredBlock /
                              TiredBlockApplied (the decorator's write counted by f band, the first in full),
                              TiredGuardHit → GuardTaken (OnMeleeHit, before the block's charge: the AI's guard by f
                              band; you never; step 34: ShieldInHand = step 21's IWeaponFacts read at the blow and
                              at the recompute, a failing read = one [error], counted without), WriteTiredBlockSummary;
                              step 64: TiredGuardHit takes the attacker (our verdict), the start / change / summary call the arc's
  Missions/AthleticsLogic.ShieldArc.cs  step 64, THE WALL'S BLOCKS COUNT: BlockKindOf(attacker, victim, cd) = OUR block kind of one collision
                              for every system (BlockHitTaken's cost / refill, TiredGuardHit, the impact model's ImpactEventOf) - the engine's,
                              turned right for a wrong-side shield block in the front arc (IImpactInputs.FacingAngle = step 62's AngleFrom; both
                              men tracked - an untracked attacker no native read; a tired AI man's step-30 share kept by ShieldArcMath.TiredRoll);
                              ShieldArcHitTaken (OnMeleeHit FIRST, own try: both verdicts counted once, the first turned block in full, verbose
                              "guard-arc"), StartShieldArc / NoteShieldArcSettings / WriteShieldArcSummary. Step 64's sprint: AthleticsLogic.Sprint.cs
                              (OrderChangedFor - a small charge store WAITS: TrackedAgent.ChargeSprintWaiting, ChargeWaitLook from the pass,
                              DropChargeWait; TryStartAi(…, intoContact) for charges and fevers; RunSprint skips the 3 m end for a sprint into
                              contact; SprintAttackBegan from AttackBegan ends it - SprintEnd.Attacked), AthleticsLogic.ImpactModel.cs
                              (MarkChargeAttack at AttackBegan → TrackedAgent.ChargeAttack, IsCharging(…, out byAttack), spent with the carry,
                              cleared at AttackEnded)
  Missions/AthleticsLogic.AiKicks.cs  step 65, THE AI KICKS AND BASHES: TickAiKicks (from TickAthletics: off - master switch first - ends every
                              attempt; AdvanceAiKicks every tick - gone / struck ended, PLAYED when step 18's KickBashTracker shows it, the phases by
                              time, the commit ended when over or after 1.5 s; a staggered slice of looks), LookAiKick (the gates on our records, ONE
                              die against the best case, IAiKickBody's reads only under it, the target's turtle memory + guard, the kind, BeginAiKick -
                              EnsureInput, AiInputHook.SetKick, the body's Hook), EndAiKick (every path, the body's Release - never for a man gone),
                              AiKickLeftField, AiKickHit (OnMeleeHit's alternative-attack path, once an attempt), NoteTurtleBlock (BlockHitTaken),
                              NoteAiKickCost (ChargeKickOrBash), WriteAiKickSummary (after the brace's lines); TrackedAgent's AiKick* fields +
                              TurtleBlocks / TurtleBlocksAt; AiInputHook.cs: AiInputState.KickPhase / KickKind (Active includes it), SetKick, ApplyKick
                              (Core Frame - written LAST in the component, the kick key never past step 32's no-swing hold, still in the commit); step 66: THE WAIT FOR
                              REACH (TrackedAgent.AiKickWaitUntil / Since / Chance - no die while waiting, the engine read each look, ended on reach /
                              run out / DropAiKickWait), the shieldman's die = his EARNED bash (TrackedAgent.BashCredit, spent on the decision),
                              NoteAiBashSwing (StartRelease, after the class read: a shield-infantry release earns AiBashShareOfSwingsPercent of a
                              bash), the cooldown by kind (AiKickMath.CooldownFor)
                              step 68: in reach and in front, a friend on the line (AiKickFriendClearMetres) = FriendInTheWay - the decision waits
                              like one out of reach (one first-time line); AiKickHit(st, intoShield, victim) - whom it landed on (AiKickVictimOf)
                              step 80: in reach and in front the kind first, then StrikePathOf (TargetInPath, then the body's StrikePath) - off the path /
                              a friend first = waits, behind a friend = dropped; AdvanceAiKicks(now, rules) - a bash's PressPath after its guard
                              (PathBlocked); every first collision → AiKickOutcome, a played attempt that never landed = Miss
                              step 81: THE DECISION POINT - NoteAiMeleeReady (from PhasesOnAction at an AI melee wind-up: queued only, TrackedAgent.AiMoveQueued),
                              TickAiKicks rolls the queue (DecideAiMove: not a move in reach / the roll / the refusals - Refuse, NoStoredSwing - / BeginAiKick) and
                              samples the contact time in a staggered slice; LookAiKick, NoteAiBashSwing, NoteTurtleBlock, the wait and TrackedAgent's
                              TurtleBlocks / BashCredit / AiKickWait* removed
  Missions/AiKickBody.cs      step 65: IAiKickBody (the smoke plays it) - IsActive, IsAiControlled, Hands (horse, rout, a ranged weapon, a shield), Target
                              (GetTargetAgent, else ImmediateEnemy - an enemy on foot; the distance, the angle off his facing = GameImpactInputs.AngleFrom),
                              FacingTo, Hook / Release (step 16's hook); GameAiKickBody, AiKickBodyDefault
                              step 68: FriendInTheWay (Mission.GetNearbyAllyAgents into a reused MBList, AiKickMath.FriendInPath per mate), IsEnemy
                              step 80: FriendInTheWay → StrikePath (his position, GetMovementDirection, the target; his side within min(reach, the
                              enemy's along) + the half-width fed to Core AiKickPathProbe; no mission / team = refused)
  Missions/AthleticsLogic.KickBrace.cs  step 65, THE KICKED MAN BRACES: the IKnockBackDecider (the damage decorator's DecideAgentKnockedBackByBlow → a
                              landed kick / bash decided there through ImpactModelOnLandedBlow - _inKnockBackSeam, the pending slot's _pendingBraced -,
                              a braced man's knock-back REFUSED while KickBraceRefusesKnockBack), NoteBrace (from ApplyKick: counted, the first lines, the
                              on-screen lines), the Brace watch's Appeared / Measured / NeverAppeared (ApplyKick watches a cut reaction with
                              ImpactWatchPurpose.Brace - LiveSlow plays it at FastSpeedFor), Start / NoteKickBraceSettings, WriteKickBraceSummary;
                              Models/ImpactHooks.cs IKnockBackDecider + KnockBack / KnockBackOnBlow; Models/TraxDamageModel.cs DecideAgentKnockedBackByBlow
  Missions/AthleticsLogic.ImpactMeet.cs  step 82, MEETING A KICK OR A BASH: FillMeet (the guard at contact into the facts, the first meet per row in full),
                              ApplyMeet (counted; the attacker's fall / stagger and a stopped bash's blocker outcomes queued through step 51's QueueBlock),
                              THE AI READ (MeetReadStart - once per start, from BeginAiKick and MeetKickSeen (step 18's KickOrBashStarted); TickMeetReads - the guard
                              after the reaction, the hold; MeetReadHitTaken from OnMeleeHit; EndMeetRead / MeetLeftField; a fixed pool of 128), WriteImpactMeetSummary;
                              Missions/MeetBody.cs: IMeetBody (Guard - GetCurrentActionType(1) / GetCurrentActionDirection(1) / GetDefendMovementFlag + the item's
                              weight / skill; Ability - AIBlockOnDecideAbility / AiRandomizedDefendDirectionChance; Aim - Mission.GetNearbyEnemyAgents; CanRead; Hook /
                              Release), GameMeetBody, MeetBodyDefault; ImpactModel.cs: a stopped kick / bash decided in OnMeleeHit, _pendingMeetHeld (the knock-back
                              refused in KickBrace.cs's seam even with the brace off); AiKicks.cs: the raised-shield swap only for a shield guarding right;
                              AiInputHook.cs: AiInputState.MeetGuard / MeetGuardFlag (Active), SetMeetGuard, the frame in the component; TrackedAgent.MeetRead*
  Missions/WallBody.cs        step 41: WallState (held, his list slot, vanilla's timer + its last StartTime, the fallback's
                              next rewrite, the next sample), IWallBody (the smoke plays it), GameWallBody (the facts by managed
                              reads + IsRetreating; the slot GetCurrentGlobalPositionOfUnit(agent, true), CurrentDirection,
                              CachedAveragePosition, CachedFormationIntegrityData; Hold = SetFormationIntegrityData(…, true);
                              Release = vanilla's own formula; the timer field _cachedAndFormationValuesUpdateTimer looked up once,
                              read once per man; EnemyWithin = GetNearbyEnemyAgentCount; step 42: TryEnemy = GetTargetAgent, else
                              ImmediateEnemy, an active enemy only; WallState.Leash / NextLeashLook / EverStepped; step 63: WallState.Out
                              (Core WallOutOfPlace) / LastOutLook, IWallBody.ToLocal / Hook / ReleaseInput - step 16's hook and his body
                              frame for the walk back), WallBodyDefault
                              step 68: WallState.Paced / EverPaced; WallFacts' HasOrderSlot / OrderX / OrderY (Formation.GetOrderPositionOfUnit) and EnemyGap
                              (CachedClosestEnemyFormation's CachedMedianPosition, Depth) read for a held moving order; IWallBody.Pace (Agent.TrySetFormationFrame
                              with his GetWorldPosition moved by SetVec2 and GetBaseFormationFrame's direction) / Unpace (the game's own frame back)
  Missions/AthleticsLogic.Wall.cs  step 41: StartWall (the [wall] line), TickWall from OnPreMissionTick (the switch / master
                              switch → ReleaseAllWalls at once; every frame each held man whose timer moved is rewritten -
                              LookWall AfterVanilla - or the fallback every 0.1 s; a staggered look every 0.25 s - the gates,
                              hold / let go with vanilla's value, a sample every second look), the held list (swap-remove),
                              WallLeftField (Forget - no engine call), WallFailed, CloseWalls, WriteWallSummary; step 42: the
                              leash in LookWall (only while L > 0 or a man is out - L 0 = step 41's calls exactly; a man out =
                              vanilla's value given back, kept in the list, nothing written - goto Sample), the per-frame loop
                              looks at a man out every 0.1 s instead of after vanilla, Manage (into the list), NoteLeash, EndStep
                              (every let-go path ends a step), the first step out / its end in full, a live L change line; step 63:
                              OutOfPlaceLook (from every HOLD look: WallPullMath.Step - a spell begins / ends back, the walk back on /
                              off), the per-frame loop looks at a man out of place every 0.1 s (only with ShieldWallPullBackSeconds above 0),
                              StartWallPull (EnsureInput, Hook, SetWallPull, the vector) / SteerWallPull / StopWallPull (the callback
                              released - never for a man gone), EndOutOfPlace on every let-go path (the leash freeing him, a gate,
                              ReleaseAllWalls, WallLeftField, WallFailed; CloseWalls = still out), the first out / walked back / back lines
                              step 68: PaceLook at every hold write and at a stepping man's 0.1 s look (WallPaceMath.Pace → body.Pace; else the
                              game's frame back when ours was on him), Unpace on every let-go path (a gate, ReleaseAllWalls, WallFailed; none for a man gone
                              or at the mission's end), UnpaceAll when the switch goes off, the pace / kick-return change lines; the ON samples by order
                              group; OutOfPlaceLook's kick return (KickBash running / begun within 2.5 s / AiKickPhase / Punishing; never walked while
                              kicking); WriteWallSummary + the two step-68 lines; the outLooks run with the pull OR the kick return
  Missions/AthleticsLogic.Fever.cs  step 43: StartFever / NoteFeverSettings (the [fever] lines), RetargetDecision (the share
                              in 0.05 steps → SpeedDirty; called from RetargetSpeed and every look), FeverOrderFor (from the order poll:
                              arm / re-arm / end), TickFever (switched off → EndAllFevers; a staggered slice, each man ~every 0.25 s),
                              PollFever (player / rider → ended; contact = EnemyWithin(FeverContactMetres) → the burn; the shield by
                              IWeaponFacts; the sprint check; the proof's contact time by group and bar band), FeverSprintCheck (the
                              gates, the two proximity reads, FeverDice → TryStartAi + BeginSprint), EndFever (every path), FeverLeftField,
                              CloseFevers, NoteFeverAttack (StartRelease), NoteDecisionValues (the decorator), WriteFeverSummary; step 54:
                              RetargetDecision raises a Punishing man's share (PunishMath.Raise), PollFever runs PunishLook after the contact read
  Missions/ImpactBody.cs      step 47: IImpactBody - the impact levers' ENGINE side, one method per lever (the smoke plays it): IsActive,
                              IsMounted, CanForce (never a horse, a rider, a siege engine / ladder, water, already down), Read (both
                              channels' GetCurrentActionType + GetCurrentAction(ch).Index), SlowReaction (SetCurrentActionSpeed),
                              ForceStagger (SetActionChannel(0, act_stagger_*) - CanForce checked again first), TryShieldInHand /
                              SlotHoldsShield / HasAnyShield (the managed equipment), DropShield (Mission.AddTickAction(DropItem) - queued,
                              vanilla's way), OwnKnockdownBlow (vanilla's drowning blow through RegisterBlow: KnockDown | KnockBack, ≥ 1
                              damage, owned by the attacker); step 53: OwnDismountBlow (the same blow flagged CanDismount - only on a
                              mounted man), OnPlayerSide (Team.IsPlayerAlly - your men's lines); GameImpactBody, ImpactBodyDefault
  Missions/AthleticsLogic.ImpactRiders.cs  step 53, RIDERS + THE ON-SCREEN LINES: the IDismountDecider (the decorator's dismount seam →
                              ImpactModelOnLandedBlow with _inDismountSeam), ApplyRider (from ImpactDecide for a mounted defender: the dismount
                              answered in the seam / our dismount blow queued for a block, the shield drop into step 50's queue (Rider-flagged),
                              the YOU line), the riders' pools (ImpactRiderPending 32 - RunRiderPending: blocks live, the levers on, a horse still
                              under him, OwnDismountBlow; ImpactRiderCheck 32 - seen off the horse / not within 2 s → GotUp(now, 0)), RidersLeftField,
                              CloseRiders, the summary; ImpactMessage (only from APPLIED paths: ApplyKick / ApplyHit / RunOwnBlow / RunStagger /
                              RunLiveDrops / the dismounts; to you, by you, your men's notable with a hero) → Core ImpactMessageQueue →
                              TickImpactMessages (from TickImpact, own try) → IImpactMessageSink (GameImpactMessageSink = InformationManager in the
                              game's colours; ImpactMessageSinkDefault - the smoke swaps it)
  Missions/PunishBody.cs      step 54: IPunishBody (the smoke plays it) - Target(st, out distance) = GetTargetAgent(), else ImmediateEnemy, an active
                              enemy, the distance from the positions (native reads, main thread); GamePunishBody, PunishBodyDefault
  Missions/AthleticsLogic.ImpactPunish.cs  step 54, THE AI PUNISHES A WINDOW: StartPunish / NotePunishSettings (the [impact] lines), NotePunishWindow
                              (from the END of ImpactDecide, live only: a new window counted per victim, _punishAnyUntil moved), PunishLook (from
                              PollFever after the contact read: the proof's time, the target read only in contact while a window is open anywhere,
                              Punishing - RetargetDecision raises the share -, PunishTimid - courage's SurgeUntil, a timid spell ended), PunishClear
                              (the player / a rider), NotePunishAttack (StartRelease: by on / off x in a window, a window punished, the YOU line),
                              WritePunishSummary (after the riders')
  Missions/CourageBody.cs     step 49: CourageScan (reused arrays, 128 neighbours), ICourageBody (the smoke plays it), GameCourageBody (MissionAllows - battle
                              mode, no tournament / arena / naval; Gate; ReadKit - SpawnEquipment's armour sums, Character.Level, a two-hander / polearm
                              and no shield; Health ÷ HealthLimit; GetMorale; Formation.CachedAveragePosition; Scan = GetNearbyAgents into one MBList;
                              Flock = GetNearbyAllyAgents; ToLocal; GroundOk; Tighten = HumanAIComponent.OverrideBehaviorParams Melee + GoToPos; Restore =
                              Agent.RefreshBehaviorValues(order, arrangement); Hook / Release), CourageBodyDefault
  Missions/AthleticsLogic.Courage.cs  step 49: StartCourage / NoteCourageSettings (the [courage] lines), TickCourage (switched off → EndAllTimid; the staggered
                              looks ON and OFF; the walkers steered every tick), PollCourage (the gates, the circle's sums, the measurement, the required
                              odds with shock / fever, CourageMind.Look → BeginWalk / WalkEnded / brave), PickTarget (FLOCKING), AskSprintBack (TryStartRegroup,
                              the panic reserve), SteerWalkers (arrived on the way, the edge, the vector into AiInputState.Regroup*), the weights (re-applied
                              every look, the game's back), EndTimid on every path, CourageLeftField, CourageHitTaken (OnAgentHit - the shock window),
                              CloseCourage, WriteCourageSummary
  Missions/FearBody.cs        step 67: IFearBody (the smoke plays it - its courage stand-in) + GameFearBody (Unit: the table key from Team.TeamIndex /
                              FormationIndex, the order by GameBraceBody.KindOf + a wall arrangement, CachedAveragePosition, CurrentDirection; Slot =
                              GetCurrentGlobalPositionOfUnit(agent, true)), FearBodyDefault; step 69: OrderKind (step 23's KindOf), NearestEnemy
                              (Mission.GetNearbyEnemyAgents into a reused MBList), Ranged (a ranged weapon in hand - thrown too -, a ranged / horse-archer /
                              skirmisher formation by its index or PhysicalClass); step 70: EnemyFront (CachedClosestEnemyFormation - its centre, facing,
                              Width, Depth; once a unit a period), Target (GetTargetAgent - you / a hero / another; lone runners only)
  Missions/AthleticsLogic.Fear.cs  step 69: FearLookCore's WIN (FearNear.Odds / Shaken from courage's look - FearMath.Win / WinMixed / Calmed, the unit
                              table fed the fear BEFORE the win and his own win), the threat line (one NearestEnemy beyond the scan), the charge's
                              mop-up (no leash / anchor / step-in), FearEnd.Winning / NoThreat; IdleLook (from FearLookCore and a held wall man's
                              FearSkipped) + the first IDLE line per holder; WriteFearSummary adds the winning and idle lines. Step 67, THE FEAR FLOCK: StartFear / NoteFearSettings (the [fear] lines), TickFearSwitch (the master switch first - off =
                              EndAllFear at once) and TickFearUnits (the table published every 0.5 s, the walls' break re-armed) from TickCourage,
                              FearScan (courage's scan read once more before it is cleared: the nearest enemy, cover, behind, the spot behind a
                              comrade), FearSkipped (a held wall man noted in his unit; other gates end a spell), FearLook (every courage path: Off /
                              Free / Walking / Surge / FeverPress - the fear into the table and the stats ON and OFF, the gates, the spell, his place,
                              the anchor, the loose mass, the 1 m dead zone, StartFlockInput / SteerFlocker / StopFlockInput - the Flock wish on step
                              16's hook, AskFearSprint), FearWallBreaks / NoteWallBreakMan (step 41's LookWall asks them), EndFear on every path,
                              FearLeftField, CloseFear, WriteFearSummary (after courage's lines); TrackedAgent.Fear / Morale / CourageLevel;
                              AiInputHook.cs: AiInputState.Flock (FlockX / Y), SetFlock, ApplyFlock (after the wall's pull and the walk back)
  Missions/OrderClickGuard.cs  step 72: IOrderClickWorld (the order mode, the button, Look - the game's pick, cursor → icon / press point px -,
                              SetTargetingDisabled) and OrderClickGuard (engine-free; Tick once a frame, OnOrder, the [orders] first-time lines, Release);
                              Missions/OrderClickView.cs: the layerless MissionView - Prepare (naval / no handler = stand down; its index vs the handler's),
                              OnPreDisplayMissionTick → the guard, the engine reads (MBWindowManager.WorldToScreenInsideUsableArea, the order flag at the
                              press, Screen.RealScreenResolution*), the player's OrderController.OnOrderIssued; AthleticsLogic.Orders.cs: AttachOrderClicks
                              (from AttachHud) and the summary
  Missions/LineDiscipline.cs  step 73: ILineWorld (Mission, BeginLook, Read, Ranks, Clamp → taken, GiveBack, SetPreview → PreviewChange,
                              PreviewCapReads, Name, Show) and LineDiscipline (engine-free: Tick from the pre-tick - the verdict, D recounted, the
                              clamp + its cause, the retry, the preview per slot, the [lines] first-time lines, switch changes / give-backs;
                              NotePlayerLineOrder - asked vs the cap, AT THE CAP or too wide, the on-screen line; SummaryLines);
                              Missions/LineWorld.cs: GameLineWorld (slots team × 8, LineFacts from the formation and its QuerySystem, GetBattleTier
                              over GetAllUnits, SetFormOrder(FormOrderCustom) + read back, the replaced form order kept and given back, the
                              arrangement swap); Missions/DisciplinedLineFormation.cs: the game's LineFormation with MaximumWidth = the cap (CapReads)
                              and Clone of its own type; AthleticsLogic.LineDiscipline.cs: StartLines (AfterStart), TickLines (OnPreMissionTick,
                              before the wall), the player's OnOrderIssued (each formation's share of the dragged line), the summary
  Missions/AthleticsLogic.StandFirm.cs  step 75: StartStandFirm / TickStandFirmSettings (the [wall] lines), StandFirmLook (from LookWall after step
                              41's verdict: the order conditions ON and OFF - WallState.FirmLatch -, the decision, a running sprint ended), FirmWouldStep
                              (the leash's step refused - a copy of step 42's state machine), FirmFrameLook (the frame pinned to his slot, step 68's pace
                              refused), FirmTolerance, FirmSpellBegan / FirmSpellBack (from OutOfPlaceLook), NoteFirmAttack (StartRelease - strikes from
                              the slot ON / OFF), WriteStandFirmSummary; WallBody.cs: WallState.Firm / FirmCond / FirmLatch / FirmLeash / FirmOut, the
                              formation's CurrentPosition / OrderPosition read for a Move; Core ShieldWallStandFirm.cs: StandFirmRules / StandFirmVerdict /
                              StandFirmLever / StandFirmLatch / StandFirmMath / StandFirmStats
  Missions/AthleticsLogic.Spears.cs  step 76: StartSpears (the [spears] lines), TickSpears (from TickAthletics: the settings - off lifts every
                              favour -, TickThrustRules - the stun held / given back -, TickFlinchWatch, each man whose look is due), LookSpear (the gate,
                              the loadout cache, the context, the radius, the scan, the condition, the time split ON / OFF, RBM's timers, SpeedDirty on a
                              flip, WieldWish), Unfavor / UnfavorAll / ClearWieldWishes, SpearLeftField, SpearFavorFor (the decorator's read),
                              NoteSpearAttack (StartRelease), NoteFriendlyContact / NoteEnemyThrust (the damage decorator's), CloseSpears, WriteSpearSummary;
                              Missions/SpearBody.cs: ISpearBody (Ready, Gate, ReadLoadout - the 4 items compared, Context - GameBraceBody.KindOf, the
                              arrangement, GetFormationFileAndRankInfo + LineFormation.GetUnit, CloseEnemies - GetNearbyEnemyAgents into a reused MBList,
                              WieldedSlot, Hook / Release), GameSpearBody, SpearBodyDefault; TrackedAgent.Spear / SpearItem0-3 / SpearHeldNow /
                              ThrustPassAt; AiInputHook.cs: AiInputState.SpearWield (+ slot, mask, rewrites), SetSpearWield, ApplySpearWield
  Missions/AthleticsLogic.BlockHold.cs  step 74: StartBlock / NoteBlockSettings, TickBlockSwitch (the master switch first - every pull let go),
                              TickBlockUnits (the frames published after the fear band's, the diagnostic sampled, the [block] change / FIRST CONTACT lines,
                              the fear band told the block - FearUnits.SetBlock), BlockLook (from FearLook on every courage path, ON and OFF: the unit table
                              fed - step 73's cap and the formation's reads once a period, his slot distance -, the measurement, the gates, BlockHoldMath
                              .Decide, the Block wish on step 16's hook), SteerBlockers / SteerBlocker (every 0.1 s, the place extrapolated with the block),
                              EndBlockPull on every path, BlockSkipped (a held wall man still sampled), CloseBlock, WriteBlockSummary (after the fear's);
                              Missions/BlockBody.cs: IBlockBody (Cap via LineDiscipline.TryCap, Formation - the order / behaviour / arrangement names, cached
                              -, Name), GameBlockBody, BlockBodyDefault; LineDiscipline.TryCap (the last look's cap per slot); AiInputHook.cs: AiInputState
                              .Block (BlockX / Y), SetBlock, ApplyBlock, BlockOn - the last wish; TrackedAgent.BlockHold (Core BlockMind)
  Missions/AthleticsLogic.WarCry.cs  step 79: StartWarCries / NoteWarCrySettings, WarCryEvent (from GoIn: back in by his own mark; from the
                              contagion: an answer), NoteChargeOrder / ChargeRoars (from PollOrders - a kind newly a charge; the poll runs while the
                              war cries are on too), Enqueue, TickWarCries (from TickAthletics after TickSprint: off = every waiting cry dropped; each
                              due cry through the limiter, then IWarCryBody.Shout), WriteWarCrySummary (after the rhythm's); Missions/WarCryBody.cs:
                              IWarCryBody (MayShout - active, AI, not you, not running away / retreating; IsMounted; Shout = Agent.MakeVoice Yell /
                              HorseRally, NoPrediction), GameWarCryBody, WarCryBodyDefault; TrackedAgent.WarCryNext / WarCryLast / WarCryQueued
  Missions/AthleticsLogic.Rest.cs  step 78c: StartRest / NoteRestSettings / TickRestSwitch, RestLook (from RhythmLookCore after the rotation: the marks,
                              the start, the facing / behind-front / wall-slot samples, ready → GoIn), BeginRest, SteerRest (every 0.1 s: the walk back along
                              his unit's axis, the stand + clamp, the attack hold; a wall man guard only), GoIn + CONTAGION (one reused queue), EndRest /
                              EndAllRests; RestingInWall (LookWall's leash 0); the [rhythm] rest lines. Step 78c removed the lull from
  Missions/AthleticsLogic.Rhythm.cs  step 78: the swap (FindMate - one scan for a tired front man, StartSwap / SteerSwap - hold, beside,
                              Formation.SwitchUnitLocations, back), the lull (the unit table published from TickCourage, each man walked back /
                              clamped at his unit's line), RhythmWallSlot (LookWall: a held man's slot = the swap's place / shifted back), the
                              [rhythm] lines, EndLullByWave (79); Missions/RhythmBody.cs: IRhythmBody (PlayerUnit, CanSwap, SwapSlots); AiInputHook.cs:
                              the eighth wish AiInputState.Rhythm (RhythmMove / RhythmClamp / RhythmHoldAttacks), RhythmOn, SetRhythm, ApplyRhythmMove,
                              ApplyRhythmClamp; TrackedAgent.Rhythm (Core RhythmMind)
  Missions/AthleticsLogic.Press.cs  step 77: StartPress / NotePressSettings (the [press] lines), TickPressSwitch (the master switch first -
                              every wish let go), TickPressLines (after the blocks' frames: a held block / a ring / its body width per unit, the
                              table published, the first line / gap lines), PressLook (from FearLook after the block's, ON and OFF: the vote, his line
                              and face, the verdict, the measurement - looks past a front, the CROSSING watch by where and cause, one Target read per
                              crossing -, the wish), SteerPressers / SteerPresser (every 0.1 s: the walk out / round the end, or the clamp's direction
                              into the line within 0.6 m of it and a rear-rank man's outward side, his own frame), EndPress on every path (the hook's
                              frames into the stats), PressLeftField, ClosePress, PressBlockRange (the block hold's range cut to the enemy line's
                              ends after first contact), WritePressSummary (after the block's); the line table fed by BlockNoteUnit (one set of
                              native reads; held wall men too); FearNear.FoeKey / PressMates (courage's scan); AiInputHook.cs: AiInputState.Press
                              (PressFront / PressNX-NY, PressSide / PressSX-SY, PressMove / PressX-Y, the frame counts), PressOn, SetPress,
                              ApplyPressMove (after the wall's pull), ApplyPressClamp (LAST, after every wish); TrackedAgent.Press (Core PressMind)
  Missions/AthleticsLogic.LoneRunner.cs  step 70: StartLoneRunners / NoteLoneSettings (the [courage] lines), LoneLook (at courage's look, ON and OFF:
                              alone, out by the band's frame, the cause, IFearBody.Target for lone men only, the first of each cause in full), NoteFeverRefused,
                              NoteForcedWalk (LoneMind.Forced), LoneSprintEnds (RunSprint's 0.25 s check of a sprint into contact - SprintEnd.LoneRunner);
                              AthleticsLogic.Courage.cs: the fever / shock once before ON / OFF, FIX 1 (no fever press alone or on a forced walk), FIX 3
                              (Look(…, lone), a surge cancelled, the walk's side kept), AthleticsLogic.ImpactPunish.cs: no punish step-in for a lone / forced man;
                              AthleticsLogic.Fear.cs: ReadEnemyFront, the band place (a hold blends, a slid man the whole way, a charge loose in the band), no
                              step-in in a full file, FRONT MEN STRIKE; TrackedAgent.Lone
  Missions/FlockBody.cs       step 55: IFlockBody (the smoke plays it) + GameFlockBody (MissionAllows, the gate - AI, formation, busy, a CHARGE order;
                              ReadKind - bow / crossbow / sling with ammo, throwing weapons; Key - TeamIndex / FormationIndex; the goal - the order's
                              TargetFormation, Formation.TargetFormation, CachedClosestEnemyFormation; CanStart = CanBeAssignedForScriptedMovement; MoveTo =
                              Agent.SetScriptedPosition (no navmesh = false); Release = DisableScriptedMovement while GoToPosition is on; Tighten / Restore
                              the horseback weights), FlockBodyDefault
  Missions/AthleticsLogic.Flock.cs  step 55: StartFlock / NoteFlockSettings (the [cavalry] lines), TickFlock (switched off → EndAllFlocking; the census every
                              0.5 s ON and OFF), Census (pass 1: riders, kinds, flocks, the spread; pass 2: DecideRider - the timid look, eagerness, free /
                              flock / rested / gave up, BeginFlocking; pass 3: the grid, GroupLook - the group, the heading, the curve, Flip or Issue), the
                              weights, EndFlocking on every path, FlockLeftField, NoteFlockAttack (StartRelease / OnAgentShootMissile: passes, lone, the gap),
                              CloseFlock, WriteFlockSummary; TrackedAgent.Flock (Core RiderFlock)
  Missions/AthleticsLogic.Impact.cs  step 47, THE SPIKE (docs/IMPACT.md §7 I1): StartImpact (the deciders plugged into ImpactHooks, the cap
                              raised while on, the [impact] start line), TickImpact (from OnMissionTick, own try: the switch - master switch
                              first, off = the cap back and everything let go -, lever 2's worker-thread failure, the queued calls ≤ 20 a
                              tick, the watches, the shields), the deciders (KnockDownForKick - the rotation's next kick; SetBlockStun - x3
                              for an even blocker, any thread), ImpactHit (OnMeleeHit's part: a landed kick → the KnockDown / reel watch +
                              the shield drop; a block → the stagger every 10 s, else our blow every 20 s, else lever 2's recoil + guard
                              watches - ≤ 32 slots), the fixed pools of structs (SpikeWatch 64, SpikePending 64, SpikeShield 32),
                              DisableLever (one [error], "lever N disabled", the others run on), ImpactLeftField (Forget), the first-time
                              lines, WriteImpactSummary (CloseImpact first); the logic IS the IKnockDownDecider / IBlockStunDecider;
                              step 48: KnockDownOnBlow = the spike's answer OR the model's (SpikeKnockDown + ImpactModelOnLandedBlow),
                              TickImpact runs TickImpactModel (own try) before the spike's early return, the summary adds the model's
  Missions/AthleticsLogic.ImpactModel.cs  step 48, THE MODEL in the mission: StartImpactModel (the [impact] start line with the anchors),
                              TickImpactModel (the switch / mode lines, on-off time), ImpactEventOf (the collision → one of the six events -
                              BlockMath.KindOf, SpikeMath.KickLanded), ImpactModelOnLandedBlow (the decorator's KnockDown seam decides a landed
                              blow and remembers it - returns the knockdown only once its family Applies), ImpactModelHitTaken (OnMeleeHit's
                              own try: the decorator's decision taken, else decided here), ImpactGate (master switch first, Athletics,
                              ImpactEnabled, the family, both tracked), ImpactDecide (both men's Athletics into ImpactFacts, Decide on their
                              TrackedAgent.Impact states, ImpactStats, LastImpact), the first-of-kind / first-outcome lines (the mode words
                              built only when a line is written), ImpactInputs / ImpactDice (the smoke's seams); step 50: the decision on
                              COPIES of both records, written back only when KeepsState (and never for a rider), then ApplyKick when the
                              kicks apply (inSeam = asked from the decorator - the only place a knockdown can be answered); the KnockDown
                              seam's error disables the live KnockDown lever; step 52: ApplyHit when clean hits apply (the rider branch counted),
                              ImpactHitLiveStats.NoteMomentum for EVERY decision (the speed band)
                              step 68: ImpactModelHitTaken counts every kick / bash collision (landed / into a shield / other, by you) first;
                              WriteImpactModelSummary adds the coverage line
  Missions/AthleticsLogic.ImpactLive.cs  step 50, KICKS AND BASHES LIVE: ApplyKick (counted; the knockdown answered + its Fall watched with
                              GetUpFactor, else the reel watched with ReelFactor; the shield drop queued; the YOU line), TickImpactLive (from
                              TickImpact, own try: not live = every drop / watch let go at once; RunLiveDrops - from the tick, never during
                              his own release / bash (≤ 1 s), the shield in hand now, IImpactBody.DropShield, followed; TickLiveWatches -
                              ReactionWatch, SlowReaction at 1 ÷ factor the tick it shows and on each _continue, the measured end →
                              ImpactState.EndReel / GotUp; TickLiveShields - left the hand, picked up again, ShieldDroppedAt), the pickup lock
                              (IShieldPickupLock), DisableImpactLever (one [error], that lever only), ImpactLiveLeftField, the summary;
                              fixed pools (64 watches, 64 drops, 32 shields), no allocation; step 51: a family's watches / drops let go alone
                              (LetGoLiveWatches / LetGoLiveDrops - a drop or a shield marks Block), TickBlocksSwitch from the tick, RunBlockPending
                              after the drops (one call budget), the block watches' events handed to the ImpactBlocks part; step 52: TickHitsSwitch,
                              the hit watches (HitFlinch / HitGetUp / HitControl - slowed like a reel / a get-up) handed to the ImpactHits part, the
                              hits' [summary] lines
  Missions/AthleticsLogic.ImpactBlocks.cs  step 51, BLOCKS AND THE RIPOSTE LIVE: BlockStunLive (the stun seam while the spike is off - ANY
                              thread: _stunLive + the switches read live, GetAnyThread, x BlockStunFactor from TrackedAgent.ImpactTired,
                              LengthenedStun, the Interlocked tally), TickBlocksSwitch (what stops applying let go, RaiseBlockCap / ReleaseBlockCap -
                              never while the spike runs, SnapshotTired every tick: 1 − f, x ImpactOnPlayerPercent for you, 0 on a horse),
                              ApplyBlock (from ImpactDecide: the knockdown → our blow queued, else the stagger; the wrong-side drop into step 50's
                              queue; the riposte → the ATTACKER's stagger queued; no outcome → the guard timed, ≤ 16; the YOU lines), RunBlockPending
                              (CanForce first - no engine call on a rider / a man down; ForceStagger with, then over the priority at StaggerSpeed;
                              OwnKnockdownBlow; each watched; a lever's error collected, disabled after the loop), the block watches (Stagger /
                              Riposte → EndReel + the learned length; BlockGetUp → GotUp; Guard → the stun measured), NoteInsideLiveStagger (blocks
                              / hits from inside), FixState (an outcome not played leaves no reeling / ground time), the let-go paths; a pool of 64
  Missions/AthleticsLogic.ImpactHits.cs  step 52, CLEAN HITS LIVE + MOMENTUM: the IShrugOffDecider (ShrugOffKept - clean hits on men on foot
                              while hits apply: the game's threshold × (1 − T), counted by band; its own lever ShrugOff), ApplyHit (from ImpactDecide:
                              the knockdown answered in the seam + its HitGetUp watch slowed by GetUpFactor; else the stagger = a HitFlinch watch on
                              his own flinch slowed to the asked length - FlinchFactor over the LEARNED FlinchNatural; no outcome = a HitControl
                              watch, ≤ 8; shock; the YOU line; the first-time lines), TickHitsSwitch (not live = the hits' watches let go), the hit
                              watches' Appeared / Measured (EndReel - the reeling bonus' time, GotUp, the controls into the learned length) /
                              NeverAppeared; _seamShrugOff (a knockdown on a blow the game had shrugged off - counted)
  Missions/ImpactInputs.cs    step 48: IImpactInputs (the engine's side of one event - the smoke plays it), GameImpactInputs (managed reads:
                              the collision's slot - Item.Weight, GetModifiedSwing / ThrustSpeedForCurrentUsage by StrikeType, RelevantSkill;
                              a bash = the shield, one-handed; the defender's WieldedOffhandWeapon shield / WieldedWeapon parry and its skill;
                              MountAgent; HealthLimit; the collision's DamageType / InflictedDamage; TypeOf), ImpactInputsDefault; step 51:
                              BlowName (the weapon's name for a YOU line - asked only when you are one of the two); step 52: SpeedInto (MOMENTUM -
                              the attacker's MovementVelocity turned by MovementDirectionAsAngle, vanilla's formula, along the line to the victim;
                              engine reads, main thread), the two-handed grip (IsTwoHanded || IsPolearm); step 53: RiderSpeedInto (a rider's =
                              vanilla's rider velocity: MovementVelocity.y × the mount's GetMovementDirection()), AttackerPassive = IsDoingPassiveAttack
                              on a horse (a couched lance)
  Missions/BraceBody.cs       step 23: BraceState (his roll, step 28's floor roll FloorUnit, the running brace, the shield's
                              progress), IBraceBody (the
                              engine side - the smoke plays it), GameBraceBody (the order from
                              GetReadonlyMovementOrderReference().OrderEnum + FormationAI.ActiveBehavior for an AI Move;
                              the hands from the wielded indices + managed equipment; TryToWieldWeaponInSlot WithAnimation;
                              AiInputHook.Hook / UnhookIfIdle), BraceBodyDefault (the smoke swaps it once)
  Missions/WeaponFacts.cs     step 21: IWeaponFacts (what he holds at a release - the smoke plays it) +
                              GameWeaponFacts (WieldedOffhandWeapon's usage IsShield; WieldedWeapon's
                              WeaponClass Bow / Crossbow - the slot index is a pointer read, the item managed:
                              safe in a hit callback) + WeaponFactsDefault (the smoke swaps it once)
  Missions/AthleticsLogic.PlayerTimer.cs  step 13, YOUR timer: the hold from the release's start
                              (PlayerAttackStarting), the countdown (PlayerAttackEnded), the gate's
                              frame (GatePlayerInput: native flags → GateFrame, managed - clears
                              AttackMask while held), swallowed presses, the flash, hold-to-attack,
                              missed attacks, releases (switched off, not you, mission end), the tick's
                              safety net, TryGetPlayerRecovery (the recovery bar's read), [athletics]
                              YOU lines; SmokePlayer (the smoke's stand-in for Mission.MainAgent); step 17:
                              only YOUR hands - YouDrive (no pause while the AI drives your hero - RTS Camera's
                              free camera - R25; SmokePlayerAiControlled), a game object in use (a siege engine
                              fires on your attack bits) never held (R26)
  Missions/AthleticsLogic.Hideout.cs  step 19: the hideout boss fight = a fresh start - NoteHideoutMission (first
                              tick: the controller by NAME, MissionObjectiveLogic), TickHideout (top of the Athletics
                              tick: the intro = CutScene mode, the objective by reference), ObserveObjective (the boss
                              objective's id → BossFightBegan once), BossFightBegan (side reads FIRST - Team.IsPlayerAlly
                              / Team.Invalid = aside; a throw = one [error], nothing refilled - then FreshStart + every
                              release: Finish / EndHold / ReleasePlayerTimer (FreshStart), a queued / deferred one
                              dropped, ResetPhases, RetargetSpeed exact); HideoutSideOf = the smoke's teams
  Missions/PlayerAttackGate.cs  step 13: a MissionLogic added then moved to INDEX 0 of
                              Mission.MissionBehaviors (SubModule, OnMissionBehaviorInitialize) - it
                              pre-ticks right after MissionMainAgentController wrote the input
  Missions/PaceBody.cs        PaceState (+ D, kind, the attack's end, the pause); IPaceBody = the AI
                              timer's ENGINE side (the smoke plays it); GamePaceBody: NoAttack via
                              SetScriptedFlags only on a free man (no GoToPosition / NoAttack / object
                              / ladder / detachment; riders allowed since 13), lifted only while he is
                              free, else Waiting; step 16: InputPaceBody (no flag - hooked, the same
                              refusals, never waits; release = the callback off if idle); PaceState +
                              ByInput, Overlapped, Deferred, the call count at the start, hits taken;
                              step 21: the pause's parts (Tired, Share), its Class and Percent
  Missions/TrackedAgent.cs    one fighter's record (step 22: + Block, his BlockTracker): Core Fighter + detection, f-bin, fresh top
                              speed and slowed-horse fields, his StepBackState (null until needed),
                              the attack-rate phase state (step 20b: + the phase's animation multiplier),
                              the running attack's D (at full animation speed) + as played and its end, his
                              last rest by kind, his last timer (for the gap), his PaceState, his
                              AiInputState (step 16, null until first held by input); step 21: the attack's
                              class (read at its release), the last ended AI attack's class / share / model for
                              the next cycle note, the classes he attacked with, his last timer's class; step 48: Impact (his
                              Core ImpactState - reeling, immunities, recent outcomes); step 51: ImpactTired (his T for the block stun seam -
                              a volatile float the tick writes, worker threads read); step 54: Punishing / PunishTarget / PunishInWindow / PunishOn /
                              PunishContact (as the punisher), PunishWindowEnd / Seen / Hit (as the victim)
  Missions/AthleticsLogic.Hud.cs  step 6: AttachHud on the logic's FIRST TICK (the screen runs by
                              then - RESEARCH §G) - each view via MissionScreen.AddMissionView with
                              a GauntletHudLayer, "[hud] attached:" lines; WriteHudSummary (the
                              [summary] hud: lines + each view's own; the screen finalizes views
                              before it). Attached: the player bar, the recovery bar, the orders strip
                              (step 9, with its GauntletOrderCards + MissionStripFormations), the ALT
                              markers (step 20, with GauntletFormationMarkers + ProjectedFormationMarkers), the fever
                              marks (step 44, with GameFeverMarks over a second GauntletFormationMarkers - since step 46 the
                              "ALT bars": every man's Athletics bar above his fever bar)
  Hud/TraxHudView.cs          THE BASE OF EVERY HUD VIEW (MissionView): one GauntletLayer + movie +
                              VM that exists exactly while HudGate says so, read every frame
                              (ReadFrame → Tick(in HudFrame) - the smoke drives Tick); refresh every
                              HudRefreshSeconds; OnLayerFrame every frame (step 9); suspend/resume;
                              Finish at mission end; every entry wrapped - an error or a movie that
                              does not load disables the view for the mission (layer removed,
                              [error], "[hud] … DISABLED"); [hud] lines for build / removal / not
                              shown, each with its reason, in the view's OWN rate buckets (step 13:
                              hud-layer:<view>, hud-outside:<view>) (QuietConditionToggles: a view's own
                              condition coming and going → verbose after the first build); HudStats;
                              step 12: OutsideToggle / ReadOutside = a view's outside-a-battle rule (the
                              grace clock; builds / removals outside a fight in the "hud-outside" bucket);
                              step 20: ReadsIndicatorKey (the frame reads the game's ALT key for that view); step 46: ToggleOn (the
                              view's switch read live - a view may take two) + ToggleName (the log's words for it)
  Hud/HudLayer.cs             HudFrame (one frame as a view sees it; IsFightMode = Battle, Duel,
                              Tournament, Stealth; step 12: IsWalkMode = StartUp, PlayerWeaponDrawn by
                              HandsFull; OrderMenuOpen; step 20: ShowIndicatorsKey - game key 5, Left Alt),
                              IHudLayer (the engine seam), GauntletHudLayer
                              (vanilla's recipe: IsCustomType check, LoadMovie, AddLayer; release
                              the movie BEFORE RemoveLayer; a failed movie never goes on screen)
  Hud/PlayerAthleticsView.cs  step 6, DESIGN §3.1: the player's bar - TryGetReading(main) → BarMath
                              → the VM; logs the first values per layer and the FIRST time of each
                              colour / empty / wound per battle (later colour changes verbose only);
                              step 12: outside a battle too (ShowPlayerBarOutsideBattles, BelowFull)
  Hud/PlayerAthleticsVM.cs    its ViewModel - every property EXACTLY the bound widget property's
                              type (float / Color / bool / string: Gauntlet converts only strings);
                              change-checked setters; the number text rebuilt only when it changes
  Hud/AttackRecoveryView.cs   step 13 (Anton's "attack recovery" bar): just above the Athletics bar -
                              EMPTY at your attack, FILLING over your pause with the seconds inside,
                              FULL otherwise; the flash on an early press; read every frame
                              (OnLayerFrame), layout at the refresh; the Athletics bar's gate + its own
                              switch + ShowPlayerBar and AttackRatePlayerTimer on; first values / first
                              pause / first flash lines (bucket hud-recovery), the summary line
  Hud/AttackRecoveryVM.cs     its ViewModel (layout, Fill, FillColor, SecondsText / SecondsShown, FlashOn)
  Hud/SprintBarView.cs        step 33: the sprint bar above the recovery bar ("Sprint 6.2 s", the store's fill, the start mark,
                              grey / blue / yellow) - read every frame, layout at the refresh; fights only, ShowSprintBar +
                              ShowPlayerBar + SprintEnabled; first values / first sprint lines (bucket hud-sprint), the summary
  Hud/SprintBarVM.cs          its ViewModel (layout, Fill, FillColor, MarkOffset / MarkShown, LabelText by the tenth)
  Hud/OrderCards.cs           step 9's engine seams: IOrderCardSource / GauntletOrderCards (the
                              "MissionOrder" layer found with the public FindLayer, its widget tree
                              walked once per open - card = OrderTroopItemBrushWidget in a two-widget
                              slot - and each card's pixels / visibility / CurrentMemberCount read
                              every frame; nothing patched), IStripFormations / MissionStripFormations
  Hud/OrderStripView.cs       step 9, DESIGN §3.4: the orders-menu strip - while the menu is open,
                              a cell under each card (placed every frame in pixels, slot k checked
                              against formation k), the fallback panel (reasons, 0.5 s / 1 s grace),
                              values every refresh, [hud] first placement / card changes / fallbacks
                              / values lines, the [summary] strip line; step 24: the ready count per cell
                              at each values push (ReadyMath; ReadyStats + its [summary] line)
  Hud/OrderStripVM.cs         its ViewModels: the root + 8 fixed OrderStripCellVM in one
                              MBBindingList, bound TWICE by the prefab (the cells, the panel rows);
                              step 24: ReadyText / ReadyColor / ReadyShown / ReadyMarginTop, SetReady,
                              ReadyColors (shared with the ALT labels)
  Hud/FormationMarkers.cs     step 20's engine seams: IFormationMarkerSource (TryReadShown = vanilla's own
                              "markers shown", Read → MarkerRead Live / PointsOnly / Projected / Unavailable),
                              GauntletFormationMarkers (the "MissionFormationMarker" layer by the public
                              FindLayer, GetMovieIdentifier("FormationMarker").DataSource = the game's
                              MissionFormationMarkerVM - IsEnabled, Targets' Formation / ScreenPosition / WSign /
                              Distance / TeamType / Size - and its FormationMarkerListPanel widgets' Position,
                              Size, AlphaFactor, IsTargetingAFormation, own offsets; nothing patched),
                              ProjectedFormationMarkers (the fallback: every team's formations, the median's
                              ground + 3 m through WorldToScreen from the combat camera, nominal sizes)
  Hud/AltMarkerView.cs        step 20, DESIGN §3 item 3 (hold ALT): shown exactly while vanilla shows its
                              markers (its flag; else ALT or the orders menu copied; NeedsPlayer false); every
                              frame: read, pair (or nominal), Sight, Place each label under its marker, values
                              when the stats / settings version moves; fallbacks (a marker without its widget,
                              no widgets, no layer → own projection for the show) once per show, the first of
                              each in full; [hud] first-show line (technique, markers, labels, no-label reasons),
                              values line, the [summary] line (AltMarkerStats); step 24: the ready count in
                              PushValues (every side; ReadyStats + its [summary] line); step 58: the fever share in PushValues
                              ("fever 12/40" from FormationAthleticsStats.InFever, FeverLabelMath.HiddenFor; FeverStats + its [summary]
                              line; the values line's "fever 12/40" and the rules' sentence)
  Hud/AltMarkersVM.cs         its ViewModels: the root + a GROW-ONLY MBBindingList of AltMarkerLabelVM keyed by
                              team + formation (a label never swaps formations when vanilla re-sorts); step 24:
                              ReadyText / ReadyColor / ReadyShown, SetReady; step 58: FeverText / FeverShown, SetFever
  Hud/FeverMarkSource.cs      step 44's engine seam: IFeverMarkSource (TryReadShown = vanilla's "markers shown" flag, NewShow,
                              BeginFrame - the camera, the screen in pixels and the UI scale -, Locate - side, the feet +
                              FeverMarkHeightMetres, distance, in front -, Project, Describe; the smoke plays it); GameFeverMarks
                              (step 20's GauntletFormationMarkers for the flag, CombatCamera Position / Direction, Agent.Team /
                              IsActive / Position, MBWindowManager.WorldToScreen; the scale from our own layer's UIContext);
                              step 46: Locate sets Mounted (Agent.MountAgent, managed) and lifts a rider RiderLiftMetres; step 58: Locate
                              sets Facing (the angle from the camera's forward) and refuses a man with no valid team (Team.IsValid - an
                              arena's crowd, Team.Invalid)
  Hud/FeverMarkView.cs        step 44, DESIGN §3 item 3b (hold ALT): shown exactly while vanilla shows its markers (FeverEnabled
                              FIRST in the condition, then its flag, else ALT / the orders menu copied; NeedsPlayer false); every
                              frame: the logic's fevers walked (TryGetFeverAt), Locate, PreSkip, Project + on screen, Pick the
                              nearest FeverMarkMax, Size / Place / Tint × vanilla's fade into the pool; [hud] first-marks line,
                              "FeverEnabled switched off", the [summary] line (FeverMarkStats, its own stopwatch); vanilla's own
                              per-man icon (MissionAgentLabelView's mesh) never touched. STEP 46 (§3 item 3c): named "ALT bars" in
                              the log; the toggle = ShowAthleticsMarks OR ShowFeverMarks, the condition = something to draw
                              (FeverEnabled or the Athletics bars) then the flag; the walk = every man (TryGetMarkAt) with the bars
                              on, AthleticsMarkMath.Decide, PickOnScreen through a struct Projector, Layout (the stack), the band /
                              wound / fever colours × the fade; the first-bars line (men looked at, in a fever, marked, bars by
                              colour, the first drawn - "calm" / "armed"), "… (the Athletics bars stay)". STEP 58 (§3 item 3d): the
                              NEAR view by default (AltNearRules) - a candidate beyond AltNearMarksMetres skipped TooFar, the rest
                              elected by CentreKey(Facing), the cap min(AltNearMarksMax, FeverMarkMax), no fade (AltNearMath.Fade);
                              AltMarksEveryone = step 46's ranking by distance; MarkStats.NoteMode; the first-bars line's near cap,
                              "N° off the centre" per man and the mode sentence at its end
  Hud/FeverMarksVM.cs         its ViewModels: the root + a GROW-ONLY POOL (MBBindingList of FeverMarkVM - mark i = the i-th
                              drawn this frame, the rest hidden); FeverMarkVM: IsShown, PositionX / Y, Width / Height (Scaled*
                              pixels), Fill, FillColor, FrameColor (the fade's alpha baked in); step 46: the box (PositionX / Y =
                              its top-left, BoxWidth / BoxHeight), FeverShown, the Athletics bar - BarShown, BarWidth, BarHeight,
                              BarFill, BarUsable, BarColor, BarFrameColor, BarWoundColor
tests/TraxCombat.Core.Tests/  net8.0 xUnit (1165 - step 82: + Step82Tests.cs (12 - the settings placed / shipped, the master switch first, the guard read, the rows and their ceilings, the correct meet and the attacker-side pressure, the stopped / landed wrong meets, a kick met otherwise as before, the read chance by bar, the reaction cut and the guard frame, the summary lines); 498 settings; 1153 - step 81: + Step81Tests.cs (9 - the shares placed / shipped, the cut over 100, the roll's split, 100 000 seeded rolls near 20 / 35 / 45 and 35 / 65, the spear rule, the refusal groups, the stack minimum 3 and the cost 8, format 19, the shares line), Step65Tests' AiKickTests rebuilt, Step66Tests down to the format-13 migration; 478 settings; 1154 - step 80: + Step80Tests.cs (10 - the settings, the rear-rank man, the facing vs the line, a bash wider than a kick, men beside / behind / beyond, the enemy in the path, the probe vs the cheap gate, a two-deep line, the summary line, format 18); 489 settings; 1144 - step 79: + Step79Tests.cs (12 - the settings placed / live, the master switch first, the chances and a
                              seeded 10 000 rolls, the delays, the man's limit, the unit's cap, the battle's cap at 1000 men in 80 units, the window
                              sliding / cleared, the charge roar's cooldown, the shouters shared out, the queue, the summary line); 487 settings; 1132 - step 78c: + Step78cTests.cs (10 - the settings placed / live, the master switch first, the marks by order and the wound cap, the start, the contagion chance, the walk-back limit and the hold, the attack hold, the lull's keys dropped from an old config, the natural gap by side, the summary lines), the lull's tests out of Step78Tests / Step78bTests; 476 settings; step 78b: + Step78bTests.cs - the 16 settings placed / live, format 17 (moved / kept), the clock (the roll by order, worn / slowed / spent), the whole unit's bar under a fresh rotated front, the blows slowing, the gap from the men (bins, the plane, the lines from a meshed fight, opened, stepped back, the cap), the charge grace, the bent front and the wing, no wrap / ordered, the summary lines; 489 settings; 1121 - step 78: + Step78Tests.cs - the 27 settings placed (the player page 27), the switches, tired /
                              fresher / behind / covered / beside, busy actions, the lull by order, the unit table, the lull's start / lines / ends,
                              your units, the summary lines; 473 settings; 1109 - step 77: + Step77Tests.cs - the 16 settings placed (the player page 24) / live / defaults, the
                              master switch first, the line test, Toni's wall's front segment (its ends, no gap, its rear face, a man stepped out),
                              the gaps (four files fallen 2.8 m, two 1.2 m none, a rear man stepping up none, a loose line), the verdicts (clamp /
                              push / round the nearest end / broken / winning), through and the wrap (26.6 m on 23.6 m never, 60 m with 5 mates),
                              the rear rank's side, the input clamp, the table (the wall a line, the recruits' enemy line / face / width / front,
                              a wider unit wraps, a unit behind attacks the rear face), the band 27 m → 25 m in 5 ranks, the block's range at the
                              ends (= step 74 when symmetric), the cause order, the summary lines; 446 settings; 1096 - step 76: + Step76Tests.cs - the 21 settings placed (the player page 23) / live / defaults, the
                              master switch first, the loadout (RBM's), the radius from the length, RBM's timers (2 s / 4 s / 2 s mounted, a passing
                              and a drifting enemy), the back rank and a thinned file, facing polearms, the favors, the wield bits, the collision
                              decision, the summary lines; 430 settings; 1085 - step 75: + Step75Tests.cs - the 6 settings placed (the player page 22) / live / defaults, the
                              switches with the master switch first, the hold orders, a move at its place latched and its ways out, the leash 0 / the
                              tolerance, the walk back from the slack, SprintEnd.StandFirm, the summary line; 409 settings; 1077 - step 74: + Step74Tests.cs - Step74MapTests (the shipped map, Toni's examples on it, format 16 move / keep) and
                              Step74BlockHoldTests (the 11 settings, the switches, the strength by D, the rule - across / ahead / behind / the fighters / the
                              leash / the hysteresis -, the unit table's median frame, quantiles, the diagnostic's bins, the summary lines); the format-15
                              expectations → 16; 403 settings; 1066 - step 73: + Step73Tests.cs - the 15 settings placed (the player page 21, the group before Advanced) / live / defaults, the
                              master switch first then the system then yours, which formations count, the tier clamp and heroes, the mean depth, the
                              width math (ceil; the narrowest line no thinner than D for every D and men; the game's floor of the cap width), Toni's
                              battle, the summary lines; 392 settings, 18 Advanced groups; 1057 - step 72: + Step72Tests.cs - the 3 settings placed / live, the master switch first, the scaling, vanilla's pick, the
                              click / drag / near / off-the-icon / plain / leak / abort / quick-second-click decisions, the summary line; 377 settings, 17 Advanced
                              groups; 1045 - step 71: + Step71Tests.cs - the new costs with the multipliers and the hint's 5 / 13 blows, format 15 - move,
                              keep, the chains through steps 66 / 68, a default that never moved; the older format tests → 15; 1042 - step 70: + Step70Tests.cs - the 11 settings placed / live, the master switch first and the sentences, the band's
                              ranks / width / lateral centre / files / front creep, the slide rule and the free file, the band facing his line, a man's place kept
                              along the front, THE 40-MAN LOOTER EXAMPLE (step 67's point 1 file of 40 vs the band 9 files, the deepest 5), the band line, alone /
                              out / the lone runner, the cause order, the three fixes, courage's forced walk, the lone line, SprintEnd.LoneRunner; 1027 - step 69: + Step69Tests.cs - the six settings placed / live, the master switch first and the
                              sentence, the win (losing / even 0, 2 : 1 = 1, the shaken enemy, the switch), the unit's win, THE WORKED EXAMPLE (a tired
                              green and veteran at odds 0.5 / 1 / 1.5 / 2 / 3), every losing number = step 67, the threat line, the mop-up, the new ends,
                              the unit table's win, the idle watch / holders / moving orders / line, the winning line; 1004 - step 68: + Step68Tests.cs - the five settings placed / live, the pace (the order groups, the
                              front gap, the capped point, why not, the master switch first), the by-order and pace lines, the walk back after a kick,
                              no kick through a friend and whom a kick hit, every landed kick against the decisions, the new costs and config format
                              14; the older format tests → 14 (and pinned where step 68's own notes join); ImpactTests' group 118, 357 settings;
                              991 - step 67: + Step67Tests.cs - the 29 settings placed / live, the master switch first, the fear's two dangers,
                              discipline by level, THE WORKED EXAMPLE (looters vs legionaries at fresh / half / spent × morale 80 / 50 / 20 → fear and
                              pull), the thin line, the unit's share and the hysteresis, his place, the shelter's creep by order, cover / behind / the
                              spot behind a comrade, the anchor, the charge's loose mass, the sprint, the 1 m dead zone, the wall's break, the unit
                              table, the summary; the morale bar at the stack's bottom, the morale line, the HUD clause; CourageTests' group 53;
                              955 - step 66: + Step66Tests.cs - Step66Tests (the five settings, the bashes keep the feature on without kicks,
                              the credit and its cap, the wait and the capped rate, the format-13 migration, the summary line), Step66ExamplesTests
                              (THE WORKED EXAMPLES - kicks a minute for a looter / an average man / a legionary fresh, at half a bar, vs a turtle, in a
                              fever; a shieldman's swings and bashes by bar band in a seeded walk through the real rules); the format strings → 13;
                              947 - step 65: + Step65Tests.cs - AiKickTests (the settings, the master switch first, the rate's
                              factors, a clash a few a minute, the kind, every phase's input bits, the busy actions, the turtle memory, the summary),
                              KickBraceTests (the settings, the failure curve and the cut, a braced man never down / disarmed and no reeling state, a
                              spent man = step 50 exactly, you x ImpactOnPlayerPercent, the summary), Step65ExamplesTests (THE WORKED EXAMPLE - a looter
                              on a fresh legionary 12.9% / ~13% short, 0 falls / drops; even fresh men ~63% braced; a fresh kicker on an empty man step
                              50); ImpactTests / ImpactLiveTests pin the brace off; 929 - step 64: + Step64Tests.cs - ShieldFrontArcTests (the settings, the verdict in / out of the
                              arc, parries and the game's right side never touched, the master switch, a tired man's step-30 share and the
                              stateless roll, the summary line, the correct-block rules it brings), ChargeIntoContactTests (the settings, charge
                              / fever into contact - advance / move never, the 0.5 s still end only after he moved, the small store's wait, the
                              format-12 fever move, the summary line), Step64ExamplesTests (the carry 3 s migration, the charges line's
                              carried-by-an-attack, a RUNNING blow proportional, THE WORKED EXAMPLE - looters into a fresh legionary wall
                              before / after, even men unchanged); the format strings → 12, the sprint summary's 6 lines; 913 - step 63: + Step63Tests.cs - FallsOnTheHitTests (the format-11 migration, the 0.2 s backup,
                              a friend's blow counted in both lines), RearBlowTests (the settings, pressure straight into x on landed blows only, THE
                              WORKED EXAMPLES on the shipped values - even fresh men front 0 / side 8 % staggered / behind 7 % down 40 % staggered, a
                              legionary standing behind a fresh peasant archer 47 %, charging 50 %, the front unchanged -, the cap / ceilings, the
                              migration 200 / 140 → 100), WallPullTests (the setting, out beyond max(L, 1) + 0.25 / back within half, the walk only
                              after standing out the setting able to move, never while down, 4 s at most, 0 = never, the summary); ChargeTests /
                              ChargeExamplesTests pinned to step 62's model (pressure 0, x2 / x1.4); 900 - step 62: + ChargeTests (the lost falls' rules and line, toughness, direction, the charge and
                              the thrown back, the reel by the comparison, the cap, the critical, the lines) and ChargeExamplesTests (the worked
                              examples on the shipped values - a legionary charging a peasant archer's back, looters into a legionary shield
                              wall, you into a shieldman, even men); step 59: + SettingsPagesTests - every setting on exactly one MCM page and no key lost,
                              the player page's exact list / labels / groups in order (12-20), the groups player page first and Advanced
                              last with unique titles, plain words on the player page, config.json / defaults.json player page first
                              under a banner per page; step 58: + AltNearMarksTests - the angle from the camera's forward and its key, the range, no fade in
                              the near view, THE NEAR PICK (five by angle, an off-screen central man takes no slot, the nearest man at the edge waits), the
                              every-man ranking, the cap under FeverMarkMax, the settings live / grouped / ordered / near by default, the sentences, the
                              label's "fever 12/40" / clamp / hide order with the master switch first, both summaries; step 57: + RoutSprintTests - the setting placed / on / live, the whole store once with no reserve and
                              below the start gate, an empty store / no store refused, the rout mark cleared by other starts, every switch with the
                              master switch first, the summary line; step 56: + RightBlocksTests - two EMPTY even men: a correct block ≤ 5 % on the blocker and the
                              riposte at 70 %, the wrong side at 90 / 50 / 40; two fresh men nothing; a tired attacker into a good guard; the stick vs
                              the axe still staggers; 100 / 100 = step 48; the decision line; the format-10 migration (old defaults moved, tuned kept and
                              said, format 10 untouched) - and ImpactTests pin the step-48 weights for IMPACT §4 A-G; step 55: + CavalryFlockTests - the 14 settings, the master switch, kinds / keys, eagerness + the free
                              line's hysteresis, THE GROUP-SIZE CURVE, THE HEADING BLEND, the flip, a mixed-eagerness flock that flips only when its rested
                              rider leads, the sunflower slots / steer point, a rider's spell and wave, the grid, the table, the summary; step 54: + ImpactPunishTests - the four settings placed / live, the gates (the master switch first, no
                              window in shadow / the spike / every family off, measured with the punish off), the window (reeling / down + the grace,
                              a measured early get-up closes it, nothing = never), the share raised never lowered / never the player / 0.05 steps / off,
                              the timid press by range and panic, a real decision on an exhausted blocker opens one (a fresh one none), the summary;
                              step 53: + ImpactRidersTests - the six settings placed / live, a rider's momentum (none to
                              3 m/s, x1.15 at a canter, x1.3 at a gallop, a couched lance x1, the foot / rider settings apart, the one switch), the horse
                              bands, the momentum in the decision, THE CAVALRY CHARGE (gallop > standing, below a sprinting man's, never the whole line, a
                              fresh line 0), a rider's knockdown = knocked off at half on every family / never a stagger / his shield at the full chance /
                              0 and 100 %, the immunity after a dismount, the riposte never on a mounted attacker, the lines' words and colours, the
                              notable rule, the RATE LIMIT and MERGE (1.5 s, 3 parts + more, duplicates, a new clock, 0, Clear), the riders' summary;
                              step 52: + ImpactHitsTests - the momentum factor (none to a walk, x1.3 at a run, x1.6 at
                              a sprint, clamped, a rider's his horse's since step 53, off / 0 %, live), the speed bands, momentum in EVERY family's impact and the riposte
                              turned round, a sprinting axe vs a standing one on a half-tired man (x10 the knockdowns) and on a fresh man (nothing),
                              two EMPTY even men on a clean hit at x = 1 (80 / 50), the reeling bonus +0.75 on all six events, a live clean hit's
                              record, the shrug-off threshold x (1 − T), the flinch factor and its learned length, the classes, the summary lines;
                              step 51: + ImpactBlocksTests - the block stun x (1 + T) from T alone and the
                              decision rolling no die for it, the game's stun capped before it is lengthened, the riposte's roles (the attacker's
                              T, the defender's guard x his f) and its records, the riposte alone with the blocks off, two EMPTY even men at the
                              ceilings LIVE, a wrong-side block never better than a right one at any bar, one record entry per outcome, the
                              stagger's speed and the learned animation length, the new levers / reasons, the stun tally from 4 threads, the
                              summary lines; step 50: + ImpactLiveTests - Applies with every switch, shadow vs live states, the reel /
                              get-up ranges and spread, the speed asked, the anti-abuse on measured ends, you x ImpactOnPlayerPercent, the
                              pickup lock, vanilla's drop rule, the summary; step 49 / 49b: + CourageTests, the four worked examples) — schema vs DESIGN.md (keys + types), one copy
                              runs (SingleCopyTests: claims on private slots, copies, texts), when
                              MCM is tried and when it stops (McmPlanTests - step 12),
                              defaults.json (DefaultsFileTests), master switch, settings, config
                              file, merge rule, rate limiter (+ Peek), the log trim (LogTrimTests:
                              kept kinds, one cut point, stacks, notes, the last resort, bytes),
                              damage roll/rules/dice/stats/upside, the damage reduction
                              (DamageReductionTests, step 25: 100 → 75, 0, the 90 clamp, rounding,
                              never below 1, 0 stays 0, which hits, the roll off still reduced, the
                              master switch, the average after the roll, the summary line),
                              Athletics v2 rules (pool, f, cap, curves, effort regen, the refill curve
                              - T for any k, 100 = the old rule to the bit, exact steps - DESIGN's
                              blow counts), kicks and bashes (KickBashTests, step 18: 3 / 2.25 / 1.69,
                              free at 0 and off, one decision per action across both channels and the
                              hit), blocks (BlockTests, step 22: 1 / 5 / 2 x the multipliers, live, drain
                              and the refill delay, free at 0 and off, the classifier, once per blow, the
                              refill at 100 exactly linear, the format-5 migration), only a wrong-side block stops the
                              refill (BlockRefillTests, step 29: the rule on / off, the clock and the running
                              refill untouched, live, the master switch, a hero parrying every 1.5 s from 0
                              climbs +1 a parry, a floor soldier parrying sinks, the summary split), the hideout boss fight (HideoutBossFightTests, step 19: the refill to the
                              wound cap, the ids and text ids, the gate, the line and the summary texts), mean/std,
                              the run-speed check, Athletics summary, the
                              attack rate (rules, AI values x / ÷ m, the cap, verdicts, phases /
                              cycles / timers / holds / guard + summary; step 20b: the slower swing in the
                              target and the floor), the timer (AttackTimerTests: the pause, the animation
                              LINE by f (step 20b: 1 / 0.85 / 0.925, 100, 0, f off m, D at full speed), the
                              countdown text, the flash, YOUR timer as the gate drives it, the recovery
                              read), the config migration (step 20b: format 3 → 4 through the seam),
                              step back (chance by f, dice, spot, facing, stats, summary), step 16's
                              AI holds by input (AiInputTests: the bit rule, the move bits, the
                              backwards vector in any frame, distance covered, the later end, the
                              switches, the GUARD / hook / overlap lines, the step-back technique and
                              samples, stepped-back cycles and the timer's floor), the bar
                              (bands by f on DESIGN's fighters, shares, numbers, colours), the HUD
                              gate (order, master switch first; step 12: outside a battle - the
                              walk-about mode, weapon / refilling / grace, BelowFull) and HUD stats +
                              summary, the
                              orders strip (matching card sets, cell placement at any scale and
                              the lift, texts, band, health in the squad stats, summary), the ALT markers
                              (AltMarkerTests, step 20: pairing through a re-sort / a missing widget, sight,
                              placement at any scale and pinned, vanilla's fade, nominal size, summary), the
                              battle pace (BattlePaceTests, step 21: the class from what he holds, the pause
                              for each class fresh and tired - the percent fewer at every m -, archers' extra,
                              0 = off, ranges, the settings sentence, the summary lines), the brace (BraceTests,
                              step 23: the floor by order, the hysteresis, the wound cap's band, an order change,
                              the per-man margin, the switches, the shield step and RaiseShield, the summary, the
                              formation's Ready / Bracing; step 28: the no-formation floor, each man's own floor roll -
                              range, apart from the margin, live, clamped at 0 / 100, the hysteresis, the wound cap), the swing stack (SwingStackTests, step 32: the cap - half away
                              from zero, the floor at empty, the examples -, spending, the refill per pause at the current m,
                              the clock stopped while attacking, the AI's gap, the clamp, bursts, fresh starts, live rules, the
                              switch and the master switch, your countdown's retarget, the bar's count, the summary), the sprint (SprintTests,
                              step 33: the store = points / 20, the clamp, refill 7 / 14 / 14 / 28 s, the 20% gate, the profile
                              1.6 to 2.25 s then 1.35 / 1.1, a 30% store 2.25 s long, 0.05 steps, drain only moving, the rest kept,
                              AI shares and order groups, the switches, the key names in the file / defaults / MCM, the summary;
                              step 35: the fresh-run peak 2.0 → 1.1 / exhausted 2.0 → 0.33, the 2 s floor (points 0, pool 50 at
                              50%, pool 150 full), its slow refill, the acceleration cap on / off / 0, live, the master switch;
                              step 37: each order's reserve on Anton's examples (advance 100 / 90 / 60 / 45%, move 100 / 90 /
                              60%, charge all), keep above the store, a move → a second move = reserve kept → the charge takes
                              the 80%, live, the master switch, the summary, the format-7 migration (use keys dropped, old
                              reserve defaults moved, user values kept) and step 33's shares dropped),
                              the ready count (ReadyCountTests, step 24: hidden
                              rules in order, bands at or below 75 / 50, text, colours, the strip cell one row
                              deeper only while drawn and the 1080p bottom card lifted, log text, summary), the
                              tired block direction (TiredBlockTests, step 30: w at f 1 / 0.5 / 0 x 0 / 50 / 100, never
                              the player, the master switch first, the chance own + (1 − own) x w with a negative /
                              NaN / > 1 game value, w 0 untouched, the setting live and placed, the sentence, the
                              summary lines; step 34: the shield / no-shield split adds up, its 2 lines), the shield
                              wall's hold (ShieldWallTests, step 41: the gates in order with the master switch first, the three
                              arrangements, every order, the escape - far AND behind, far ahead held, no speed never far -, ahead
                              of the slot on any facing and a non-unit one, the switch live and on by default, the sentence, the
                              samples' part ahead, the summary lines), the leash (ShieldWallLeashTests, step 42: 0 = the hard
                              hold, freed below L with an enemy close, held at L, back in the slot before the next step - min(0.3,
                              L/2) -, no enemy = held, a man first seen in his slot, a live L change, the reach from the slot and
                              its hysteresis, a square's outward side, the setting placed and live, the switches, the sentence,
                              the summary line), aggression and the fever (FeverTests, step 43: the share by
                              the bar 60 → 20, 0.05 steps and no floor, the player / switches, x 5e's m, the store from current
                              points, armed = the absolute peak, contact, the shield live mid-fever, the fade to the CURRENT tired share,
                              re-arming, an order's end, the sprint chance x the fever left, the roll cadence, the reserve cap, the
                              settings, the format-8 migration 10 → 3 / 5 → 2 with tuned values kept, the summary lines), the
                              fever mark (FeverMarkTests, step 44: orange armed / at the burn's start, through yellow, alpha 1 →
                              0.45, gone empty; vanilla's 1.5 → 8 m fade; 60% size at 60 m and the UI scale; the placement and on
                              screen; the skips in order; the nearest-N pick and the reused / grown frame; hidden reasons with the
                              master switch first; the settings live and grouped; the sentence; the summary line), the Athletics bar
                              above every man (AthleticsMarkTests, step 46: the fill / band per reading at the peak line 75, the wound
                              cap and the fill never past it, the band colours = BarMath's hex, the stack - bar + gap + fever bar, the
                              fever place kept / dropped, only the fever bar = step 44's mark, UI scale and 60% far away, a rider 1 m up;
                              who gets which bar by side with each switch, behind / too close, hidden reasons with the master switch
                              first, the settings live / grouped / ordered; PickOnScreen - an off-screen man nearer takes no slot, only
                              what is needed projected; the summary with colours; the log's "calm"), the format-9 migration (FeverTests:
                              a format-8 file's CostPerBlow 3 → 7 once, a tuned one kept and noted, formats 5 / 7 from 10 end at 7, an
                              older file's 3 kept), the impact spike (ImpactSpikeTests, step 47: the master switch first, the setting
                              Advanced / off / live / before VerboseLogging, the sentence; the 4-kick rotation and a disabled lever's
                              part off, the even-blocker split, x3 held at the cap, Due / IgnorePriority, a landed kick only; the watch -
                              a reel on either channel through a second strike, a fall through its _continue, never shown, a recoil, a
                              guard from the block, the stagger we started, the cap; the count / length verdicts and a FAIL winning; every
                              lever's next step for 48-53; the stun tally from 4000 parallel calls; each lever's verdict, a disabled lever
                              = FAILS, the 7 summary lines, the never-on line), the impact model (ImpactTests, step 48: the 60
                              settings in their group after the fever, live, the master switch first then Athletics then ImpactEnabled then the
                              family, step 50: only the kicks apply, shadow and the spike force shadow, the sentence's anchors; the S-curve, the
                              even band, skill / weight / impact / guard numbers; EVERY worked example of IMPACT §4 - A (the theory by bar), two
                              empty / two fresh even men, B the king and the man both ways, C the stick and the axe, D you and the 220 king, E
                              the weapons (theory), F reeling, G the riposte; the lengths of §3.7 (theory) and the randomness bounds and caps;
                              stagger immunity 2 s after its end and the halving for 8 s, a knockdown's ground / 5 s immunity / replaced stagger,
                              a tired man's lengthened reel = reeling, a fresh man's not; window 0 / factor 1; you at 50 / 0 %, riders, the
                              shield, the damage term, the dice order; the summary lines and one decision in full). They
                              run on DESIGN's INITIAL values (DesignTable.cs: a module
                              initializer), so tuning defaults.json never breaks them. Keep green.
module/SubModule.xml          release manifest (Id TraxCombatEnhancements, v1.0.0) - THE one home of the
                              version (the DLLs read it at build; stamped once, on release day)
module/GUI/Prefabs/           the HUD movies - file name = movie name, `Trax…` (prefab names are
                              global across modules): TraxPlayerAthleticsBar.xml (step 6: native
                              sprite BlankWhiteSquare_9 + brush AgentHUD.Interaction.Text only;
                              FillBarWidgets draw shares; no widget takes mouse events),
                              TraxAttackRecoveryBar.xml (step 13: the label - step 32: @LabelText, "Swings 4/8" -, a frame, the fill, the
                              seconds centred on it, the flash overlay - same native sprite / brush), TraxSprintBar.xml (step 33: the
                              label @LabelText, a frame, the fill @Fill / @FillColor, the start mark @MarkOffset - same sprite / brush),
                              TraxOrderStrip.xml (step 9: {Cells} ItemTemplates - the cells placed
                              by Scaled* pixel bindings, the panel rows; the ± band = a FillBarWidget
                              ChangeWidget; a list widget carries no @binding - its own bindings
                              would resolve against the list), TraxAltMarkers.xml (step 20: {Labels}
                              ItemTemplate - a centring box per formation placed by Scaled* pixel bindings,
                              the numbers in vanilla's own marker brush NameMarker.Distance.Text, the
                              strip's bar; step 24: + "ready 34/50" under the bar, centred; step 58: + "fever 12/40" under it,
                              the fever orange as a literal Brush.FontColor, @FeverShown), both with
                              step 24's ready TextWidget (Brush.FontColor bound; the strip's under its
                              bar at @ReadyMarginTop, the panel's after the health), TraxFeverMarks.xml (step 44:
                              {Marks} ItemTemplate - one small bar per mark placed and sized by Scaled* pixel
                              bindings, a dark frame @FrameColor, a FillBarWidget inside 1-px margins @Fill /
                              @FillColor; BlankWhiteSquare_9 only; step 46: each item a box @BoxWidth x @BoxHeight holding the
                              Athletics bar at its top - frame @BarFrameColor, inside @BarWoundColor, the reachable part @BarUsable,
                              the fill @BarFill / @BarColor, @BarShown - and the fever bar at its bottom, @FeverShown)
tools/deploy.ps1              build → AssemblyGuard → OfflineSmoke → install as
                              Modules\TraxCombatEnhancements.Dev "Trax Combat Enhancements (dev)",
                              module\GUI copied beside bin
tools/package.ps1             step 11, THE RELEASE: manifest gate (release Id + Name, vX.Y.Z) → build →
                              unit tests → AssemblyGuard on both DLLs (hard) → OfflineSmoke → DLL version
                              = manifest → dist\TraxCombatEnhancements from scratch (SubModule.xml, our 2
                              DLLs + pdbs, GUI\Prefabs - checked against that list: never MCM, Newtonsoft
                              or game DLLs) → dist\TraxCombatEnhancements_vX.Y.Z.zip (forward-slash
                              entries; an existing one only with -Force) → file list + sizes. dist\ is
                              git-ignored
tools/WORKSHOP-UPLOAD.md      the release loop, step by step (first upload, updates) + the uploader's quirks +
                              "The images" (where the F12 shots live, the gallery list in order)
tools/WorkshopCreate.xml      the FIRST upload (creates the item, Private, tags, preview .jpg, the short pitch) -
                              run once; paths = this PC (C:\Users\Asus ROG\Documents\GitHub\..., C: Steam game)
tools/WorkshopUpdate.xml      every later upload - ITEM_ID placeholder until the first upload
tools/make_workshop_update.py step 26, from the sibling: WorkshopUpdate.xml → a one-off task in %TEMP% that also
                              sends STEAM-DESCRIPTION.bbcode (escaped, &#10;) and --visibility; --notes required
tools/STEAM-DESCRIPTION.bbcode  the Workshop page (≤ 8000 bytes; 5745 now - step 60's retelling, the README's story
                              in the same shape): the hook, "The idea" (7 bullets), "How a battle plays" (six themes in
                              short paragraphs), the links to docs/HOW-IT-WORKS.md and docs/MECHANICS.md, "Settings"
                              (the short MCM page + Advanced), "Good to know" (NOT an overhaul, RBM named politely as
                              the deep one), credits. Steam BBCode: [h1] [b] [i] [list][*] [url]; check the byte count
tools/preview_thumbnail.html  the Workshop preview TEMPLATE (PANEL_* placeholders) → make_thumbnail.py
tools/preview_thumbnail.jpg   THE preview (1024², ~275 KB, under Steam's 1 MB): a real battle, the hold-ALT
                              close-up ("ready 1/9" vs "ready 37/53"), your two bars, name + "Slower, tactical
                              battles. Heroes matter."
tools/make_thumbnail.py       step 26 (the sibling's recipe): crops (SHOTS) + paint-outs (ERASE) from
                              screenshots/originals → fills the template → headless Edge → the .jpg
tools/make_gallery.py         step 26: the Workshop gallery, docs/screenshots/01-05 (2560 px, < 1 MB each),
                              in order with captions
docs/screenshots/             the gallery (committed; README shows 01). The RAW F12 shots go in /screenshots/
                              (git-ignored) - Steam keeps them in <Steam>\userdata\<id>\760\remote\261550\screenshots
tools/AssemblyGuard/          soft-dependency guard (from the sibling): MCM, Harmony, ButterLib,
                              UIExtenderEx, NavalDLC, CustomBattle in any type surface = FAIL
tools/OfflineSmoke/           the real DLL on .NET Framework with the game's DLLs, no game
                              launched: types load without MCM, config flows, tournament fix,
                              damage (Program.Damage.cs: the real decorator over the game's
                              CustomAgentApplyDamageModel, fed the game's own hit structs),
                              Athletics (Program.Athletics.cs: the real stat decorator, damage
                              decorator and AthleticsLogic on uninitialized Agent objects - the
                              curves blow by blow, DESIGN's blow counts, the health cap, the
                              upside by f, horses; step 18: kicks and bashes - the costs, once each
                              on either channel or at the hit, no timer / roll, 0 / off, the YOU line,
                              the game's action codes; step 22: blocks - Core's classifier on the game's
                              own AttackCollisionData and result codes, the prices by kind and by who, once
                              per blocked blow, no pause / roll, a recruit emptied by blocks, live, 0 / off;
                              step 29: BlocksLetTheRefillRun - a parrying hero at 0 climbs through the real
                              BlockTaken, wrong-side blocks hold a man at 0, the switch live; the older steps
                              pin OnlyWrongSideBlocksStopRefill off in AthleticsDefaults),
                              the step back (Program.StepBack.cs: the real
                              logic's bookkeeping with a stand-in IStepBackBody - rolls by f,
                              queue, cap, live time, every release path, logs, summary), the
                              attack rate (Program.AttackRate.cs, step 13: full-speed animations,
                              the AI timer D x (1/m − 1) - melee, ranged from the reload, a throw,
                              riders - the gap = the timer, every lift path with a stand-in IPaceBody;
                              YOUR timer through the real logic via SmokePlayer + GateFrame: the hold
                              from the release, swallowed presses, the flash, hold-to-attack, a
                              missed attack, the switches, ranged; the gate FIRST in a stand-in
                              mission's behaviour list; the first-slowed line through the real
                              decorator; R1 re-checked for step 16 - refused = held, started = the
                              NoAttack hold deferred then set, a short pause covered = counted), the AI
                              holds by input (Program.AiInput.cs, step 16: the Core bits vs the game's
                              enum, the override check, the real logic with stand-in input bodies - the
                              timer by input, AiInputHook.Apply frame by frame, the timer surviving a
                              backpedal both ways round, arrived / edge ahead, the master switch,
                              leaving the field, the never-called warning, the switch back to the old
                              ways, OnMeleeHit's guard by state, the summary; the older steps run the
                              OLD techniques - AthleticsDefaults pins them), the decorator's animation floor (100 / 0 / 60), the master
                              switch (step backs released, AI timers lifted, your countdown released,
                              the bar, the strip and the ALT labels removed too; step 19: a hideout boss fight refills nobody), the
                              hideout boss fight (Program.Hideout.cs, step 19: the game's MissionObjective type with the
                              boss id and names, stand-in teams - the battle refills the whole side to a wound's cap and
                              ends every step back / pause / queued one / your pause, the duel you alone, off = nobody,
                              a failing read = one [error] and nothing refilled, the lines), the recovery bar (Program.Recovery.cs:
                              its prefab, the real view over a running timer), the HUD (Program.Hud.cs: PrefabIsValid - any prefab against the
                              game's own widget types, properties, brushes, sprites and the VMs'
                              property types, DataSource lists into their ItemTemplates, no @binding
                              on a list widget; the real PlayerAthleticsView driven by made-up
                              HudFrames with a FakeHudLayer - every hide reason, colours, wound,
                              empty, refresh, summary; step 12: outside a battle - the walk-about mode,
                              a weapon drawn, the grace, refilling, menus, the switch; the fail safe),
                              the orders strip
                              (Program.Strip.cs: the real OrderStripView with stand-in cards and
                              formations - layouts, UI scale, RTS Camera's set, the lift, values,
                              live switches, every fallback, quiet reopen, summary, fail safe),
                              the ALT markers (Program.AltMarkers.cs, step 20: the prefab; the real
                              AltMarkerView with stand-in marker sources - vanilla's flag and the copied rule,
                              labels under the markers at UI scale 1 and 4/3, a re-sort, colours / values,
                              the enemy switch, behind / faded / not laid out / pinned, live settings, every
                              fallback incl. our own projection, the gates, the master switch, summary, fail safe),
                              defaults read from the embedded defaults.json, the MCM pages built
                              by MCM's real builder (step 59: BOTH - every setting on exactly one; the player
                              page Master switch first, its six groups, Defaults (Revert) last; Advanced: the
                              schema's order, the Defaults buttons above Advanced, Advanced last; each group's
                              settings in order; each page's Reset only its own settings; an Advanced slider
                              live) and the buttons clicked (the export on Advanced, the player page's revert
                              refreshing both); step 12: MCM's DLL loaded with its module off (one line, no
                              try) and MCM never ready (31 attempts, one give-up line); step 10a: the log writer (readable while held, new path,
                              release) and stale records forgotten; step 10b: the trim at a 1 MB cap
                              keeps a summary, a first-time line and an error with its stack through
                              2 MB of verbose lines, the ~ mark, VerboseWants loses no count; step
                              11 (Program.Copies.cs, LAST): two real SubModule instances - the second
                              stands down (no log line, model, mission, message), the first registers
                              ONE decorator of each kind and reports once; step 20b (Program.AttackRate.cs):
                              a slower swing through the real logic keeps the full-speed pause; step 21
                              (Program.BattlePace.cs): the stand-in weapon read for EVERY logic (step 3 - a native
                              read on a fake agent would crash), the older steps pinned at 0 (BattlePaceOff), each
                              class through the real logic (a fresh shield man 0.78 s, a tired one, a two-hander,
                              riders, bows fresh / tired / mounted, a crossbow, a javelin, you never), hot swap,
                              the master switch (its own step lifts a fresh man's share too), the summary; and the
                              cost - 1000 men held at once, the AI timer's tick ~0.005 ms, 0 bytes; step 23
                              (Program.Brace.cs): the stand-in brace body for EVERY logic (step 4), the real logic -
                              charge 20 → 40, hold 60 → 80, the AI's advance, an order change both ways, a wounded
                              man 30 → his cap 50, the player never, a man taken over, the input frame (ranged let
                              through, the shield up), the shield steps, the timer and the brace never lifting each
                              other, the guard, live floors / margins, leaving the field, the switches, the summary;
                              the cost - 1000 men looked at, 0.003 ms a tick, 0 bytes; step 28 (BraceNoFormationAndFloorRoll):
                              a no-formation man braces at 20 ± his own floor roll (not 60), his floor dice apart from the
                              margin's, both sliders live, the floor lines (step 23's checks pin BraceFloorSpreadPercent 0 -
                              BraceDefaults); step 24
                              (Program.Ready.cs): the ready count through the real strip and ALT views -
                              texts / colours, the deeper cell and the lifted bottom card, the panel rows, new
                              stats, the thresholds / BraceEnabled / ShowReadyCount live, the master switch, the
                              lines (the older strip / ALT steps pin ShowReadyCount off); step 25
                              (Program.Damage.cs, DamageReductionThroughDecorator): the real decorator - after
                              the roll (100 → 37.5..112.5, mean 75), the roll off still reduced on every kind,
                              never a fall / object / 0 hit, never below 1, live, the master switch, the lines
                              (the older damage steps pin DamageReductionPercent 0 - DamageDefaults); step 30
                              (Program.TiredBlock.cs): the real decorator + logic - DrivenProperty 40, the game's
                              chance untouched fresh, f 0.52 over −1.10 → 0.48, own 0.2 → 0.584, empty → 1, no
                              compounding, never you, the slider live (50 / 0), AthleticsEnabled / the master switch,
                              a braced man's own guard direction untouched by the input frame, the guard by f band
                              (step 34: split by a shield in hand - the stand-in read, a shield dropped mid-fight),
                              the [guard] and [summary] lines; the cost - 1000 tired men block + recompute, ~0.1 µs,
                              0 bytes (the older steps pin TiredBlockRandomPercent 0 - AthleticsDefaults);
                              step 32 (Program.SwingStack.cs): the real logic - your burst of stored swings, the hold from
                              the last one's release, the countdown to the next, the kick key refused at 0, hold-to-attack,
                              saving up; the AI by input - no hold from the stack, his own gap not refilling, at 0 held
                              exactly for the rest, his kick bit taken out; kicks spend one; the switches; the summary; the
                              recovery bar's "Swings 4/8" through the real view; the cost - 1000 men waiting, 0.09 ms a tick,
                              0 bytes (the older steps pin SwingStackEnabled off - SwingStackOff in AthleticsDefaults); step 33
                              (Program.Sprint.cs): every SprintKey name = a game InputKey (LeftControl 29); the real logic with a
                              stand-in ISprintBody - your key (press / release / refusals / a menu / a horse / the AI driving you),
                              the real decorator putting MaxSpeedMultiplier ABOVE 1 (0.80 x 1.6), no refill while sprinting, the
                              profile in 0.05 steps, drain only moving, the refill rates, the AI on new orders (charge / advance /
                              hold, melee, a rider, a man you drive), SprintAiEnabled and the master switch, left the field, the
                              fresh start, the clamp, the summary; the cost - 1000 men on a charge, 0.006 ms a tick, 0 bytes, ~12
                              recomputes a man; the sprint bar prefab and view (the older steps pin SprintEnabled off - SprintOff
                              in AthleticsDefaults; step 33's own checks pin SprintMinSeconds / SprintTopSpeedReachSeconds 0 -
                              SprintDefaults); step 35 (SprintTuningThroughTheLogic): an EXHAUSTED you through the real logic and
                              decorator - the run property 0.8 x 2.0 at the start (not 0.8 x 0.3 x 2.0), the fade towards 0.33,
                              TopSpeedReachDuration 2.6 → 1.0 and back, the floor's refill, reach 0 / live, the master switch,
                              an AI man off the floor, SprintMinSeconds 0 refused, the lines; step 36 / 37 (SprintByOrderThroughTheLogic):
                              shock troops - a move 1 s down to the 80% reserve, a second nothing (reserve kept), the charge
                              sprints the 80% left, an advance 2.5 s, the reserve live, the master switch, the summary; step 41
                              (Program.Wall.cs): the stand-in wall body for EVERY logic (step 4); the game's side - v1.4.8's Agent
                              has the private readonly Timer, the real Timer driven as vanilla drives it moves its StartTime exactly
                              on vanilla's update frames (6 in 3 s), our defensive set = GetArrangementOrderDefensiveness's; the
                              real logic - one man held / left per reason, the rewrite in the next pre-tick after vanilla's update,
                              the fallback every 0.1 s, a charge / a retreat let go with vanilla's value, the escape, the player
                              taking a man, the samples ON vs OFF, the switch and the master switch live, leaving the field, the
                              summary; the cost - 1000 men held, 0.016 ms a pre-tick, 0 bytes; the master-switch step lets held
                              men go (step 41's checks pin ShieldWallStepOutMetres 0 - WallDefaults); step 42 (Program.Leash.cs):
                              the real logic with men walking out at 2 m/s and back at 1 m/s - a shield wall's man and a square's
                              side man freed (nothing written), no rewrite at vanilla's update while out, reined within the 0.1 s
                              look (1.03 m at L 1), no new step before back in the slot, enemy gone, L 0 live (held, then not one
                              enemy read) and 2 live, the master switch, the lines and the summary; L 0 = step 41's exact writes
                              with and without an enemy in front; the cost - 1000 men, 200 out, 0.015 ms a pre-tick, 0 bytes; step 43
                              (Program.Fever.cs): aggression and the fever through the real decorator and logic - the share by the bar
                              (0.144 → 0.086 full, 0.058 at a half bar), never you or a rider, a charge armed at the absolute 0.8
                              peak, nothing burned before contact, the burn and the fade (0.75 at 7 of 10 s), the shield taken out
                              (4.9 s for 7 s of store), back to 0.6 after 5 recomputes, the proof's groups, the fever sprint (refusals,
                              the reserve, the roll a second), re-arming, 100 / 100 and FeverEnabled live, the lines and the summary;
                              the cost - 1000 men in a fever, 0.005 ms a tick, 0 bytes; the master-switch step ends a fever and
                              gives the game's values back (the older steps pin the share 100 / 100 and the fever off - FeverOff); step 44
                              (Program.FeverMarks.cs): the prefab; the real FeverMarkView over the REAL logic's fevers (TryGetFever and
                              the walk) with a stand-in camera (FakeFeverMarks: a pinhole at the origin) - vanilla's flag and the copied
                              rule, armed orange full / burning 0.7 / a faint yellow sliver / gone when faded, calm men, you, behind,
                              too close, off the screen, not placed, the enemy switch, the cap = the nearest on the screen, the fade at
                              5 m, 60% at 60 m, the height / length live, UI scale 4/3, no camera, FeverEnabled off, a hold, the gates,
                              the lines and the summary; the fail safe; the cost - 1000 men, 400 in a fever, the cap 200, 0.03 ms a frame,
                              0 bytes; the master-switch step takes the marks away and back (step 44's checks pin ShowAthleticsMarks
                              off - FeverMarkDefaults); step 46 (Program.AthleticsMarks.cs): the real view over the REAL logic's men
                              - every man but you a bar coloured green / blue / yellow / orange / red by his bar, the wound cap, a rider
                              1 m higher, both bars for the men in a fever, no jump when a fever ends, the enemy switch, the fever bars
                              off (the bars drop onto the point) / the Athletics bars off (step 44 exactly), the cap = the nearest on the
                              screen (an off-screen man nearer takes no slot, 4 projections), gap / length / bands live, the fade,
                              FeverEnabled off keeps them, nothing to draw, both switches, Athletics off, vanilla's flag, the lines and
                              the summary; the fail safe mid-walk; the cost - 1000 men ALL on the screen, 400 in a fever, the cap 200,
                              0.07 ms a frame, 0 bytes, 200 projections a frame; the master-switch step takes a calm man's bar away and
                              back; step 47 (Program.Impact.cs): the stand-in impact body for EVERY logic (step 6); the game's side - the
                              act_stagger_* fields of ActionIndexCache (by reflection: its static constructor asks the engine), Core's
                              codes = ActionCodeType / CombatCollisionResult, MissionTickAction.DropItem, the blow flags, StunPeriodMax
                              raised and restored, the engine calls' signatures; THE SPIKE through the real damage decorator and logic -
                              kicks 1-8 rotating (4 falls, the reel 0.42 → 0.83 s and the fall-and-rise 1.5 → 3.0 s at 0.5, slowed again on
                              a _continue), a missing fall, a rider, a 0-damage kick, a shield dropped and picked up / one never dropped, a
                              stagger (accepted, 0.9 s, a block and a hit from inside) and our blow (a Fall), a rider's block refused with
                              no engine call, a stagger the engine refuses; the stun x3 for an even blocker through the real decorator
                              (0.21 → 0.63, the cap 2 s, an odd one's 0.9 clamped to the game's 0.8) from worker threads, the recoil and
                              guard x3 vs the control; ImpactSpike off / on live, a watched man leaving, all six WORKS in the summary; the
                              fail safe (five levers disabled one by one by their errors, lever 1 still on, each [error] once, FAILS); the
                              cost - 1000 men, 10 blocks a tick, 0.001 ms a tick, 0 bytes; the master-switch step turns the spike vanilla
                              at once and back; step 48 (Program.ImpactModel.cs): the stand-in inputs for EVERY logic (step 7 - the
                              game's reads native pointers), the game's DamageTypes / StrikeType map; THE MODEL through the real decorator's
                              KnockDown seam and the real logic - the king's kick on the 50-at-20% man decided once in the decorator (x 1.33,
                              90 / 80, the dice knock him down - KnockDown since step 50) and taken by OnMeleeHit's part, the lines in full;
                              on the ground refused; a right-side block x 1.09 (80 / 40, the stagger and stun lengths; live it writes no record -
                              step 50), in shadow a wrong-side block while reeling (immune, +0.75, halved) and fresh x 1.42, the man's tired swing on the fresh king x −0.76; even men at 25%
                              (parry 77 / 36, a clean hit 68 / 35 in the decorator), two fresh even men 0; a bash; riders; you at 0 %; shadow /
                              the spike forcing shadow / a family off / ImpactEnabled off live; a failing read; an untracked attacker; a missile;
                              the summary; the cost - 1000 men, 20 events a tick, 0.016 ms a tick, 0 bytes; the master-switch step: off =
                              nothing decided; step 49 (Program.Courage.cs): the stand-in world (men on a plane) through the real logic - a lone looter
                              walks back facing five metal men (0.05 < 1.00), sprints, holds among his mates, brave again with the weights back; a mass
                              stays brave; a fever's 0.4; a walled man and a rider never looked at; shock + great axes = a panic sprint of all 5 s;
                              flocking to an 8-man group; the switch off mid-walk; Athletics off; the summary; the cost - 1000 men 0.13 ms a tick,
                              0 bytes; the master-switch step lets the timid go; step 50 (Program.ImpactLive.cs): KICKS LIVE through the real
                              decorator and logic with the stand-in body - an exhausted man kicked down, the shield dropped from the tick and
                              picked up (the pickup lock), the fall-and-rise slowed x2 (0.5 at Fall and its _continue) and measured 3.0 s, his
                              get-up immunity from it, a second kick refused, its reel slowed 1 / 2.5 and measured; a bash on a tired turtle (the
                              drop waits out his own bash); you vs a fresh king (nothing - the YOU line) and exhausted (down); a rider (nothing);
                              courage's shock; shadow = nothing; a throwing slow / drop disables that lever alone; the master switch; the
                              summary; the cost - 1000 men, 20 live kicks a tick, 0.018 ms a tick, 0 bytes; step 51 (Program.ImpactBlocks.cs):
                              BLOCKS LIVE through the real decorator's stun seam from worker threads and the real logic with the stand-in body -
                              the cap 0.8 → 2 s, an exhausted blocker's stun x2 (a fresh one's untouched, the game's 1.2 held at 0.8 first, the
                              settings live, you x ImpactOnPlayerPercent, a rider never), a right-side block's stagger from the tick at speed 0.6
                              for 1.5 s (measured 1.5, the animation's 0.9 s learned, a block and a hit from inside counted, the end + 2 s in his
                              record), a wrong-side block's drop + stagger, a blocker knocked down by our blow (x2 get-up measured 3.0 s, ONE
                              shock), the riposte on you (the YOU line), you staggered (the weapon's name), a rider / a man down / the engine
                              refusing (no call, record fixed), the guard x1.2 vs fresh, shadow / riposte off / blocks off = the riposte alone,
                              three levers disabled alone, the summary; the cost - 1000 men, 20 live blocks + 20 seam calls a tick, 0.030 ms a
                              tick, 0 bytes; the master-switch step's blocks part; the spike's checks run with the model off; step 52
                              (Program.ImpactHits.cs): CLEAN HITS LIVE through the real decorator, the real shrug-off hook (a CustomAgentApplyDamageModel,
                              DamageInterruptAttackThresholdCut 5) and the real logic with the stand-in body - a control flinch 0.47 s, an exhausted
                              man's stagger 1.5 s (speed 0.30) measured 1.52 s and its end in his record, a man at 70 % staggered then KICKED down
                              (the reeling bonus; a calm man's kick 0 %), an axe blow on a staggered man down, shrug-offs refused / kept (a rider,
                              a kick, the switches), a knockdown on a shrugged-off blow counted, YOU, a rider, shadow, the slow lever and a
                              throwing shrug-off decider, the master switch, the summary; THE SHOCK CHARGE (200 men each, seeded: a 40 % line -
                              a standing sword 6 % down, a standing axe 13 %, the axe SPRINTING 36 %; an exhausted line 54 %; kicks 19 → 45 %); the
                              cost - 1000 men, 20 live clean hits + 20 shrug-off questions a tick, 0.029 ms, 0 bytes; the master-switch step's
                              clean-hit part; step 53 (Program.ImpactRiders.cs): RIDERS LIVE through the real decorator's DISMOUNT seam and the
                              real logic - an exhausted rider knocked off (25 %) in the seam and seen off, never a stagger on a rider, a rider's
                              shield dropped from the tick, a blocking rider knocked off by our CanDismount blow (owned by the attacker) and seen,
                              no horse by the tick refused, not seen after 2 s = his record fixed, a fresh rider untouched, the KnockDown seam never
                              answering a rider, a galloping rider x1.30 / a couched lance x1.00, THE LINES (you knocked off red at once, two by you
                              within 1.5 s = one merged purple line, your shield red, your man unhorsing an enemy hero white behind the switch, none
                              in shadow, the switch off), the dismount lever disabled alone, a throwing decider = the game's; THE CAVALRY CHARGE (a
                              fresh rider's sword into a 40 % line: standing 3 % down, gallop 10 %, an exhausted line 45 %, a fresh line 0); the cost -
                              1000 men half riders, 40 rider events a tick, 0.04 ms, 0 bytes; the master-switch step's riders part; the stand-in
                              FakeMessageSink swapped in with the impact body; step 54 (Program.ImpactPunish.cs): the stand-in punish body for EVERY logic;
                              THE AI PUNISHES A WINDOW through the real logic and decorator - no target read before a window; an exhausted blocker's
                              broken block (the real decision) opens a 2.1 s window; the man fighting him 0.6 → 1.0 (the game's 0.144 / 0.080 written),
                              his attack punishes it once; you / a rider never; a timid man at 4 m keeps away, at 2.5 m steps in; the switch live; back
                              to 0.6 after end + grace; YOUR window pressed and punished; shadow none; the summary; the cost - 1000 men, half facing a
                              reeling target, 0.011 ms a tick, 0 bytes; the master-switch step's punish part; step 55 (Program.Flock.cs): the lever's game
                              side (SetScriptedPosition / DisableScriptedMovement / CanBeAssignedForScriptedMovement, GoToPosition 1, the goal reads); the
                              stand-in flock body for EVERY logic; CAVALRY FLOCKING through the real logic - a tiring unit of 20 lancers: the dance
                              untouched, five at 55-60% flip as ONE wave of 5 after 3 s, flock again after the 10 s wave, the whole unit exhausted in two
                              clusters: no flip without a leader, the heading leaning to the unit's centre, one rested rider = a wave of 10; a mixed
                              formation = two waves of 4; a lone fresh rider among legionaries flocks (timid); held cavalry / you untouched; give-up;
                              no ground; lone passes / pass gaps ON and OFF; the switch; the summary; the cost - 1000 men, 500 riders, 0.022 ms a tick,
                              0 bytes; the master-switch step's flocking part; step 56 (Program.RightBlocks.cs): the embedded step-56 defaults through the real logic -
                              two EMPTY even men: a right-side block / parry leaves the blocker standing, the ATTACKER reels (70 %, his forced
                              stagger from the tick), the wrong side 90 / 50 / 40, two fresh men nothing, the game's stun; the steps 48-55 checks pin
                              the step-48 block model (ImpactModelDefaults → Step48BlockModel); step 57 (Program.RoutSprint.cs): the real logic's
                              OnAgentFleeing - a routing man's whole 5 s store, never ended near an enemy, run dry, empty on a second call, a running
                              move sprint handed over, you / a rider never, SprintOnRout / SprintAiEnabled / the master switch off = none, the first-rout
                              line and the summary; step 58 (Program.NearMarks.cs): the real FeverMarkView in the NEAR view - the five men
                              nearest the screen's centre within 15 m (a closer man at the edge waits, a dead-centre man at 25 m never counts,
                              5 projections), no fade, the enemy switch, the cap / range live, FeverMarkMax the ceiling, AltMarksEveryone = step 46
                              exactly, the master switch, the log, the summary; a TOURNAMENT (the flag unreadable: the near bars only while ALT is
                              held, the opponent first); the real ALT labels' "fever 12/40" (the switches, the surge over, the summary); the cost -
                              1000 men on the screen, 389 within 15 m, 0.11 ms a frame, 0 bytes, 5 projections; the step 44 / 46 checks pin the
                              every-man view (FeverMarkDefaults); step 62 (Program.ImpactCharge.cs): THE LOST FALLS through the real shrug-off hook,
                              the KnockDown seam and OnMeleeHit (decided at the shrug-off, refused, one decision, played; the backup blow after 0.5 s
                              once; refused in the water; a stagger refuses too; a killing blow never decided; the switch off; no backup at 0; a
                              kick's fall); THE CHARGE (toughness from the kit read once, behind / side / a bow, the carry into the first blow only,
                              a running sprint, a rout never, YOU thrown back by a shieldman with the red line, a parry never, an empty man's charge
                              rolling 0.5-1.5 through the real damage decorator); THE CHARGE ON A LINE (200 men seeded); the cost - 1000 men, 20 hits
                              + 20 blocks a tick, 0.07 ms, 0 bytes; the master-switch step's part; the steps 48-61 checks pin step 62 off
                              (Step62Off); step 63 (Program.Step63.cs): FALLS ON THE HIT (a friend's blow never decided, no backup; an enemy's
                              fall played at once; the backup after 0.2 s), A STRONG REAR BLOW on a line (200 men each, seeded: even fresh men
                              front 0 / behind ~8 % down, a legionary behind fresh archers ~45 %), THE WALL PULLS MEN BACK (a man thrown 2 m by
                              a fall: out of place, the 0.1 s look, never walked while down, walked back 0.5 s after he is up through the real
                              ApplyWallPull, the wish off and released once back; 0 = no look, no walk; the master switch; A / B in the
                              stand-in world); the step 41 / 42 checks pin ShieldWallPullBackSeconds 0 (WallDefaults), step 62's its 0.5 backup
                              and x2 / x1.4 (Step62On); step 64 (Program.Step64.cs): THE WALL'S BLOCKS COUNT through the logic's OnMeleeHit
                              parts (a wrong-side block from 20° → the impact model's RightBlock, the guard line and the arc's count; a charging
                              looter thrown back; an exhausted man's front block kept wrong, half at w 0.5 and every system agreeing; 100° the
                              game's; an untracked attacker no read; live; the summary), THE AI CHARGE HITS AT SPEED (a legionary's charge sprint
                              into contact ended by his first attack, a looter's small store waiting to 10 m then ended standing still, a new
                              order dropping a wait, the switches live, the summary line), THE CHARGE CARRIES TO THE BLOW (an attack begun 2.5 s
                              after the sprint strikes at 4 s as a charge, spent; 3.2 s none), the master-switch step's arc part; the older steps
                              pin the arc off and the step-64 sprint off (AthleticsDefaults), step 62's its 1.5 s carry; step 65 (Program.Step65.cs):
                              the stand-in AI kick body for EVERY logic (the Kick bit, DefendUp, DefendShield, the busy codes = the game's), THE AI KICKS
                              through the real look / attempt and the real hook filter (asked, the frames, PLAYED by step 18's poll, the cost, the hit,
                              the cooldown, a bash's guard first, a raised shield kicked, NOT played, every gate, the turtle, the switches), A CLASH (40
                              men 10 min: 5.2 a minute; 1000 men 0.004 ms a tick, 0 bytes), THE BRACE through the real decorator's knock-back + KnockDown
                              questions (braced - refused, one decision, 5 x; lucky - 2.5 x; 200 seeded looter kicks: 13.5% short, 0 falls / drops; a
                              spent man; your lines; the switches), the master-switch part; the older steps pin the AI kicks and the brace off
                              (AthleticsDefaults); since step 66 the clash at 1.2 a minute (47.3 a minute for 40 men, the model 52.5); step 66
                              (Program.Step66.cs): a shieldman's swing earning his bash (asked with the 2 s cooldown, the credit spent; none without
                              one), a decision at 2.5 m waiting and kicking at 1.2 m, a wait run out / dropped, 6 m never waited for, the kick's 5 (6 since step 71);
                              a clash - 20 shieldmen 0.98 bashes a swing, 20 kickers 20.1 a minute (the model 20.7); step 67
                              (Program.Step67.cs): THE FEAR FLOCK through the real logic with courage's stand-in world (it plays IFearBody) - a fresh
                              line obeys, a frightened single rank draws into its mass and the shelter creeps back at the hold's cap, the input frame,
                              a fever press, the switch, an anchor and its break, courage's walk back first, a sprint back, a wall that holds at morale
                              40 and breaks at 25, a charge's loose mass; the A / B (spread ON 1.94 m vs OFF 3.01 m); the cost (1000 men, 500 flocking,
                              ~0.2 ms a tick, 0 bytes); THE MORALE EYES through the real views (the bar, the line); the master-switch part; the older
                              steps pin the flock and the eyes off ; step 69 (Program.Step69.cs): WINNERS DON'T HANG BACK (a tired green unit winning 6 : 1 calmed, OFF flocks, ON live ends
                              the spells 'winning'; a partial win mops up - the roamer 12 m out not leashed; a LOSING rank's every position and fear =
                              step 67; no pull with the enemy 20 m off), IDLE MEN (six holders filed, never a walker / fighter / hold), the cost (1000
                              men 0.31 ms a tick, 0 bytes), the master-switch part; FearDefaults pins the threat line / mop-up off (160 steps); step 70 (Program.Step70.cs): THE BAND (40
                              frightened looters in a column before a 20 m line - the band's own files the deepest 6 over 8; FearBandEnabled OFF 25 deep, ON live a
                              band; a front man let go to strike at 1.5 m, walked back past the leash), NO LONE RUNNER BEHIND THE LINE (a feverish looter alone
                              behind a legionary line: fixes OFF held by his fever and counted, ON walked round the line's end to his group; his sprint into contact
                              ended alone while a charging mass kept its sprints), the cost (1000 men 0.20 ms a tick, 0 bytes), the master-switch part;
                              FearDefaults / CourageDefaults pin the band, the strike and the lone-runner fixes off (163 steps);
                              step 72 (Program.Step72.cs): NO ACCIDENTAL CHARGE ORDERS through the real OrderClickGuard with a stand-in world and a
                              stand-in of the game's release, in both frame orders (a drag onto a banner = a move, a click on the icon = the charge, near = a
                              move, no button = untouched, the menu / switch off mid-hold, ModEnabled off = vanilla, a naval stand-down, the FAILURE line;
                              the MCM page count 17 Advanced groups) (164 steps);
                              step 73 (Program.Step73.cs): LINE DISCIPLINE BY TIER through the real LineDiscipline with a stand-in world (Toni's
                              recruits 48 → 24 files, the veterans untouched, a mixed unit 32, an auto-shrink, the AI re-widening narrowed, your drag
                              stopped at the capped maximum width and counted at the cap with the on-screen line, the preview cap / your switch off
                              and on, a re-install, a clamp not taken retried after 1 s, naval / sieges, a skipped arrangement, the lines), the REAL
                              DisciplinedLineFormation over a stand-in IFormation (the game's own LineFormation code: the cap answered, the game's
                              floor of it = exactly the cap's files, the clone's type), the cost (20 formations / 1000 men 0.4 µs a look), the
                              master-switch part; the MCM page counts 21 / 18 groups (167 steps);
                              step 74 (Program.Step74.cs): THE BLOCK HOLD through the real logic with courage's stand-in world and a stand-in block body (every
                              logic gets it: nothing counted unless a step-74 check sets a cap) - 180 recruits D 6 charging a 28.5 m line under a stand-in of
                              the game's charge (a trail heading round the right flank): OFF 35 m from the median, 36 men beyond; ON within 24 m (26 at worst
                              between looks), contact as fast, men walked back, fighters free; the live switch; the [block] / [summary] lines; a D 1 elite
                              unit untouched; the input frame's priority; the cost (1000 men 0.23 ms a tick, 0 bytes); the master-switch part (170 steps);
                              step 75 (Program.Step75.cs): A STANDING WALL STANDS FIRM through the real logic with a stand-in world (ten men of a halted
                              wall, an enemy 2.2 m in front, shoved, swinging): ON 0 steps out (refused), the frame pinned, the tolerance 0.3, within
                              0.03 m mean, swings from the slot; OFF live the old steps out; an advance never; a move only at its place; a sprint ended;
                              his kick walked back; let-go paths; the cost (1000 men 0.08 ms a tick); the older steps pin the switch off (172 steps);
                              step 76 (Program.Step76.cs): SPEARMEN KEEP THEIR SPEARS through the real looks with a stand-in body and the REAL
                              stat decorator (x35 / x55 after 2 s / back after 4 s, the back rank, a thinned file, facing spears, the radius from
                              the length, riders, a dropped spear, the loadout cache, the wield fallback, the switches); A THRUST PASSES A FRIEND
                              through the REAL damage decorator over CustomAgentApplyDamageModel (ContinueChecking / a swing Bounced / polearms
                              only, the follow-up hit, the flinch, the stun 0.4 -> 0.1 s and back); the cost (1000 men 0.004 ms a tick, 0 bytes);
                              every logic starts with a NOT-ready stand-in spear body (176 steps);
                              step 78 (Program.Step78.cs): THE BATTLE'S RHYTHM through the real logic with courage's stand-in world and the REAL input
                              filter - two lines grinding: 12 of 12 swaps, the front 25% → 85%, 0 file-frames without a front man; two spent fronts: the
                              gap 1.0 → 4.4 m, closed on recovery, a new charge order ends a lull; a held wall man's slot; the cost (1000 men 0.004 ms a
                              tick, 0 bytes); the older steps pin both switches off (183 steps);
                              step 78c (Program.Step78c.cs): EACH MAN RESTS ON HIS OWN - a meshed six-minute grind (316 rests by all 84 men, at most 12
                              begun in any 2 s, back in by own mark 46 / contagion 74, backed at most 3.85 m, never toward the enemy's side, 0 turned away);
                              both sides tired = a gap 1.0 → 3.2 m by itself; one side tired = the fresh side presses; a 12 x 2 shield wall: 0 men more than
                              0.5 m out of their slots because of resting; the input frame; the switches; 1000 men 0.026 ms a tick, 0 bytes (187 steps);
                              step 79 (Program.Step79.cs): WAR CRIES through the real order poll and queue with a COUNTING voice stand-in (every logic
                              gets it; the older steps pin the cries off) - a 1000-man charge (10 units): 30 queued, 20 played over +0.15..+1.50 s, the
                              battle's peak 16 in any 1 s, a unit's 3, no routed / dead / you, riders yes; the roar cooling 15 s; a charge 5 s later 5 men
                              held by their own 8 s; the shortest gap 8.65 s; 0 bytes; the switches drop what waits; step 78c's grind with the cries on
                              and off - rests identical, back-in cries 15 of 28, answers 8 of 52 (190 steps);
                              step 80 (Program.Step80.cs): KICKS LAND ON ENEMIES - two two-deep lines of 12 files, half shieldmen, shuffling and
                              turning, 3 minutes through the real looks with a stand-in engine finding the first body along the facing: the strike
                              path ON friends 4.2% of the hits, enemy hits 1.12 a man-minute; OFF 27.2%, 1.62 (191 steps); since step 81 the lines are
                              driven by the men's own swings through the real phases (ON 1.7%, OFF 10.4%);
                              step 82 (Program.Step82.cs): the engine's codes behind the guard read; THE ROWS through the real seams and OnMeleeHit (a spent basher staggered
                              from the tick 70 %, a weapon-right bash's knock-back refused with the brace off, an exhausted blocker 85 %, a spent kicker thrown down, a kick
                              with no guard = the brace, YOU, the switches); THE AI READS IT (once per start, the guard after 0.15 s, met at the hit, too late, a kick DOWN,
                              the hold, never you, your bash seen by the poll, the master switch); FRESH VS TIRED DEFENDERS (400 bashes each: fresh - the basher reels 24 % /
                              falls 6 %, the defender never; tired - staggered 35 % / down 30 %); the cost - 1000 men 0.037 ms a tick, 0 bytes (196 steps);
                              step 81 (Program.Step81.cs; step 66's smokes removed, step 65's rebuilt on the decision point): THE SHARES IN A GRIND - the two lines,
                              the stack on (min 3), 3 minutes: shieldmen rolled 21.0 / 33.4 / 45.6 % → done 17.3 / 20.8 / 61.9 %, no shield 34.0 / 66.0 → 28.1 / 71.9 %,
                              4.28 a man-minute, 0 kicks at an empty stack; tiring 4.3 / 5.3 / 90.4 and 7.8 / 92.2 %; THE STACK GATES THE KICK; the cost - 1000 men
                              winding up, 0.004 ms a tick, 0 bytes (191 steps);
                              step 78b (Program.Step78b.cs; its lull smoke removed by step 78c): THE LULL THAT SHOWS - a MESHED six-minute grind (14 x 3 a side) through the real logic and
                              the REAL input filter: 7 lulls (2 by the breather clock), each gap 4.0-4.4 m from the men, held after 2.8 s; NO WRAPPING -
                              a 69 m unit on a 31 m held line under a stand-in charge that sends every man beyond the ends round them: step 77's wrap 80
                              men past the front, the wrap off 0 (every wing man at an end, behind the bent front); the same unit ORDERED there goes round;
                              the cost (1000 men in a lull 0.024 ms a tick, 0 bytes); step 77's smoke pins its wrap on, step 78's its own lull (186 steps);
                              step 77 (Program.Step77.cs): PRESS, DON'T PASS through the real logic with courage's stand-in world (a per-formation
                              facing) and the REAL input filter (AiInputHook.Apply) - Toni's 108 recruits (D 6, 18 files, 26.6 m) charging his 23 m
                              two-deep held wall with half their rear ranks dancing round its ends: OFF 34 behind the wall, 36 crossings; ON 0 past
                              the front at any step, 81 pressing it, the clamp bit 183 frames, 0 crossings; a man behind walked round its end; four
                              fallen files = one 2.8 m gap: 6 of 6 through, nobody else past, nobody walked back from behind a broken line; a 60 m
                              unit wraps as a body, a lone straggler does not; the input frame; the cost (1000 men 0.19 ms a tick, 0 bytes); the
                              older steps pin PressDontPass off (179 steps);
                              step 68 (Program.Step68.cs): THE WALL PUSHES AS ONE through the real logic (an advancing man near the enemy sent 1 m
                              toward the game's slot 6 m ahead, a stop / a march / a slot within the step left alone, after vanilla's update, the leash
                              keeps it, a charge / the switch / the step live, the samples by order, the lines), its cost (1000 men 0.041 ms a tick,
                              0 bytes), BACK TO THE LINE AFTER A KICK, NO KICK THROUGH A FRIEND (+ whom it hit), KICKS LANDED VS DECIDED through the
                              real seams; the master-switch part; the older wall steps pin the step-68 settings off (157 steps; the config checks work
                              for any tuned default)
                              TRAX_SMOKE_KEEP=1 keeps its temp folder + log to read
tools/DefaultsTool/           defaults.json upkeep: refresh (rewrite comments/order, keep every
                              value) | check; --file <path> for an exported one
tools/SmartAppControl/        steps 62-63: when Windows' Smart App Control refuses the freshly built tool exes / DLLs (this PC):
                              run_tests.fsx, run_smoke.ps1, run_guard.fsx, refresh_defaults.fsx load OUR assemblies from bytes
                              (never mapped, never checked); install_dev.ps1 = deploy.ps1's step 4; loadcheck.ps1 = the installed
                              pair's LoadFile under Windows PowerShell (the game's runtime) - run it before calling a deploy done
                              (AI_NOTES "Step 62" / "Step 63"). Never change Windows security settings
```

## Build & deploy

```powershell
dotnet build -c Release
dotnet test  -c Release
powershell -ExecutionPolicy Bypass -File tools\deploy.ps1
```

Game path and MCM path in `Directory.Build.props`; personal overrides in
`Directory.Build.props.user` (git-ignored). The deploy runs the AssemblyGuard and the offline
smoke test before it installs anything (`-SkipSmoke` only if the smoke cannot run on a
machine). The deploy fails while the game runs (DLL lock) — say so and hand Anton the deploy
line.

## Release

Live: Workshop item **3817259431** (v1.3.0 uploaded 2026-10-22, v1.3.1 NOT yet - Steam was not running on 2026-10-23, the command is in TASKS_TODO; v1.2.0 2026-10-18, v1.1.1 2026-10-14, v1.1.0 2026-10-12, v1.0.0 2026-10-10; on 2026-10-22 the page said "awaiting approval by moderators" - hidden from others until Valve approves the latest version; the YouTube video attached to its gallery), Nexus Mods **mods/14198** (v1.3.1 since 2026-10-23 - the main file updated in place, 1.3.0 archived; v1.3.0 published 2026-10-22 with tools/NEXUS-DESCRIPTION.bbcode, the gallery and the YouTube video), GitHub releases v1.0.0 to v1.3.1 with the zips.
The whole loop is **`tools/WORKSHOP-UPLOAD.md`** — read it before any release. **Publishing is
Anton's decision, after his playtest**: Claude never runs the uploader, and never posts or
uploads anything, without his yes for that release. **The release rhythm** (the sibling's, Anton
2026.07.28): fixes collect in `main`, versions do not — `module/SubModule.xml`'s `<Version>` (the
version's only home) is stamped ONCE, on release day, and committed BEFORE
`powershell -ExecutionPolicy Bypass -File tools\package.ps1` (the DLL carries the commit it was
built from). Then the uploader with `tools\WorkshopCreate.xml` the first time (never again),
`tools\WorkshopUpdate.xml` after. The dev and the release copy may both be enabled: the first
to load runs, the other stands down (Core `SingleCopy`).

**Editing text files: use the Edit/Write tools, never PowerShell `Get-Content`/`Set-Content`.**
Windows PowerShell 5.1 reads BOM-less UTF-8 as ANSI and writes it back as mojibake (every
—, →, ± in these docs was mangled once, 2026-09-27). **Commit messages**: PowerShell 5.1
splits a message containing double quotes into separate arguments — write it to a file and
`git commit -F <file>`, or commit from the Bash tool with a heredoc.

## References

- **Decompiled game, this exact version**: `..\reference\game-decompiled\` (CampaignSystem,
  SandBox, MountAndBlade, MountAndBlade.View, NavalDLC…). Missing assemblies (e.g. the
  GauntletUI / ViewModelCollection ones for HUD work) can be decompiled beside them:
  `ilspycmd -p -o <out> <dll>` with `$env:DOTNET_ROLL_FORWARD='LatestMajor'`.
- **Sibling mods** for proven patterns: `..\TrainingBattlesMod` (CLAUDE.md "footguns" list,
  model decorators, Gauntlet windows, deploy/package tools, AssemblyGuard, Workshop upload
  loop in `tools/WORKSHOP-UPLOAD.md`), `..\ImmersiveAI` (MCM bridge, mission views).
- Game install: `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord`.
  Workshop mods (MCM = 2859238197, maybe RCM) under `...\steamapps\workshop\content\261550\`.

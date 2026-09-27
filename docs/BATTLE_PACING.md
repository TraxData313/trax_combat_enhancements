# Battle pacing — how to make battles longer and more tactical (step 15, research)

Anton's ask (2026-09-27): battles end fast (an 80v80 infantry fight in 4–5 minutes). "I wanted to have
a way to slow them down a bit for tactical decisions or hero units to become more powerful, so check
what different tactics RBM deploys to make the battle take longer and feel more strategic — I want
something like that but without the units overhaul it introduces." Then: "do that research, but don't
fix anything, just let me see what is in there."

So this is a **menu, not a build**. Nothing in the mod was changed. Every estimate says how sure it is;
anything the game has to confirm is marked **UNVERIFIED**.

Sources: RBM v4.5.0.2 (Workshop 2859251492, `Id RBM`, 6 DLLs: RBM, RBMAI, RBMCombat, RBMCampaign,
RBMConfig, RBMTournament), decompiled to `..\reference\RBM-decompiled\` (outside the repo, read only —
no RBM code is copied here, only described); the v1.4.8 game in `..\reference\game-decompiled\`;
RTS Camera Command System 5.3.x (Workshop 3596693285) decompiled to
`..\reference\RTSCamera.CommandSystem-decompiled\`; Anton's log of 2026-09-27 (21:53, 22:40, 22:53).

---

## The menu

Length = how much longer the **melee part** of a battle gets (the approach is not counted — walking is
not tactics). "Hero" = does it make heroes (you, lords, companions) relatively stronger. Cost: S = a
day or less, M = a step like 5d or 13, L = several steps. Every number would be a slider (CLAUDE.md).

| # | Lever | What it does, in one line | Length (confidence) | Tactical feel | Hero power | Cost | Risk | Harmony? | Conflicts |
|---|---|---|---|---|---|---|---|---|---|
| 1 | **Troop damage scale** (ours) | Troops hit troops for × k (say 0.6); blows by heroes, and optionally on heroes, keep their own numbers | melee × 1/k: k 0.6 → **+65%** (medium-high) | some (more time to react) | **up, strongly** | **S** | low | no | none (it multiplies after the ±50% roll) |
| 2 | **The guard really up** (ours, fixes a flaw) | Tired AI men waiting out their pause, or stepping back, keep blocking and keep facing; the step back becomes a backpedal; the pause survives a step back | **+10–25%** (low-medium), more with the pause kept | **up** (the step back finally reads as intended) | neutral | **M** | medium | no | RTS Camera's input rewrites (order of components) |
| 3 | **Troop caution** (RBM-like, ours) | Troops start attacks and ripostes less readily (the AI's own decision chances × k); heroes keep full aggression | **+15–40%** (low) | some (sparring, not trading) | up | **S** | low-medium (too low looks passive) | no | `AttackRateAiDecisions` writes the same values |
| 4 | **Rank rotation** (ours, on Athletics) | In a holding line, a front-rank man below an Athletics mark swaps places with the freshest man behind him | +10–30% for holding lines, 0 for charging blobs (low) | **up, strongly** | neutral | M-L | medium-high | no | replaces the step back for men in a holding line |
| 5 | **Front ranks only** (RBM's idea, public API) | In a holding line the rear ranks stop flowing round their mates — a man fights only what reaches his slot | +10–30% (low) | **up, strongly** | up if heroes are exempt | M | medium (reset on every order) | no for holding lines; **yes** for charging men | RTS Camera's volley / defensive hold set the same values |
| 6 | **Morale** (ours) | Initial morale, morale lost per fallen friend, and optionally: tired formations waver, fresh ones steady, broken men rally after 10 s unhit | ±10% (medium: routs are only ~10% of removals now) | up (breaking a flank matters) | up if heroes steady men near them | S (numbers) / M (Athletics-linked, rally) | low-medium | no | other morale mods (a decorator stacks) |
| 7 | **Armour weight** (RBM's `ArmorMultiplier`, ours) | Armour counts × k in the game's damage formula | +10% for tier 1-2 fights, up to +60% for armoured tiers (formula: high; battles: low) | some (armour and tier matter) | up (heroes wear the best) | S | medium (shifts every troop's value, ranged vs armour) | no | other damage mods |
| 8 | **AI line discipline** (RBM's tactics, ours) | A battle plan of our own for the AI commander: advance as a formation, hold, charge late; archers screen, cavalry guards flanks | +20–50% (low) | **up, the most** | neutral | **L** | high | no for a new plan; **yes** to change vanilla's | RTS Camera's order patches; any AI overhaul mod |
| 9 | RBM "frontline" micro | Every man re-decides every 0–2 s: fight, step back 0–0.3 m, step to an ally, side-step to a gap | moderate (UNVERIFIED) | up | neutral | L | high | **yes** (formation internals) | RTS Camera, AI mods |
| 10 | RBM posture | Blocks drain posture; a broken posture staggers, disarms, or lets damage crush through | unclear (opens as many men as it saves) | feel only | neutral | L | high | **yes** | our Athletics (two bars) |
| 11 | Athletics knobs (existing) | Cost per blow, the floor, regen — already in `defaults.json` | **negative today** (see the baseline), positive once #2 lands | — | — | 0 | low | no | — |

### My recommended package

1. **#2 — the guard really up** (and the step back fixed with it). The log says our own design is the
   biggest accelerant we have: tired men are **defenceless** today (blocked 2–13% while their pause
   runs, 4–6% while stepping back, against 33–45% for everyone else), and in the 240v240 **41% of all
   landed melee hits struck men in those two states**. Fixing that makes the mod do what DESIGN §2
   promises ("the tired fall back and the fresh take the blows") and buys length for free.
2. **#1 — troop damage scale, heroes exempt.** The one lever that is certain, cheap, and answers both
   halves of the ask at once: battles last longer AND heroes stand out, because a hero kills at the
   old rate while troops kill each other slower. Start mild (k 0.75 ≈ +33%) and tune.
3. **#3 — troop caution** as a cheap A/B switch on top: same technique the stat decorator already uses
   in step 5e, so it costs little to try; keep it only if the log shows it reads well.
4. **Next, for the "strategic" feel: #4 rank rotation** (or #5 front ranks only) — both are public API,
   both give lines depth that matters, and both work on the part of the battle you command: your
   holding lines. Build it after 1-3 are measured.

Why not the rest: #8 is the most RBM-like but it is an AI-commander project in itself; #9 and #10 need
Harmony in the formation and combat internals (the route this mod avoids — CLAUDE.md), and #10 would put
a second stamina-like bar next to Athletics. #6 (morale) and #7 (armour) are cheap add-ons for later:
morale if you want breaking a flank to decide battles, armour if you fight armoured tiers — tonight's
tier-1/2 battles would barely feel #7.

Combined, 1+2+3 would roughly double the melee (×1.67 × 1.15 × 1.2 ≈ ×2.3 at k 0.6) — probably more
than you want given the 120v120 already "was nice", which is why each is a slider.

### The step back — the finding and the fix, short

- **Why ours turns its back:** our step back asks the engine to *go to a spot 2 m away*
  (`SetScriptedPositionAndDirection`, the `GoToPosition` scripted frame) — the same navigation vanilla
  uses to walk a man to a ladder or a dropped weapon. A man navigating faces where he walks; the
  "face your enemy" direction only applies when he arrives, and he almost never arrives (2 of 2159).
- **How RBM keeps them facing:** RBM never tells a man to walk anywhere. Its back step
  (`Frontline.cs`) moves the man's own *formation spot* to 0–0.3 m behind where he stands and locks his
  position there (`Agent.SetTargetPosition`), re-deciding about every half second, only during a charge,
  with the formation frame switched off. The target is always within one shuffle, so the engine never
  switches to "walking somewhere" — the combat AI keeps turning him to his enemy while he shuffles back.
  It is a Harmony prefix on `Formation.GetOrderPositionOfUnit`.
- **Our best fix path, no Harmony:** the game has a public per-man hook, `AgentComponent.OnAIInputSet`,
  that hands us the AI's own input every update — its movement flags and its movement vector in the
  man's own frame. Writing "backwards" into that vector for 1.5 s is a player pressing S: he backpedals,
  facing whatever the AI faces — his enemy. The same hook can clear only the attack bits and keep the
  guard up. RTS Camera Command System (which you run) already uses this exact hook to move men back to
  their slots without turning them. Details, fallbacks and what to measure: section B.

---

## Baseline — tonight's numbers (trax_combat.log, 2026-09-27)

The mission clock in the summary includes deployment; the table splits it.

| Battle | Agents | Mission clock | Deployment | Approach (after deployment) | **Melee** | Removed | Landed hits | Dmg / hit | Technique |
|---|---|---|---|---|---|---|---|---|---|
| 80v80 infantry, 21:53 | 161 | 544 s | 331 s | ~64 s (first javelin 394.6 s) | **~150 s** (you fell at 537 s, then left) | 86 killed, 1 fled (57 enemies still up) | 687 | 19.1 | old: animations × m + AI decisions + pace hold |
| 120v120 + archers, 22:40 | 241 | 453 s | 5 s | first arrow 53 s, first melee ~89 s | **~364 s** (your infantry gone by 349 s) | 194 killed, 25 fled | 779 (565 melee, 205 ranged) | 32.7 | step 13: pause-only timer |
| 240v240 infantry, 22:53 | 481 | 380 s | 59 s | ~150 s (first melee 211 s) | **~170 s** | 417 killed, 21 fled | 1423 (all melee but 2) | 38.7 | step 13 |

What happens after contact (240v240, your infantry of 207, from the orders strip):
211 s contact → 221 s: 168 men, **average f 0.22**, 37 exhausted → 228 s: 134 men, f 0.07 → 235 s: 102
men, f 0.03 → 248 s: 70 men → 316 s: 22 men. **137 of 207 men fell in the first 37 seconds.** The
melee killed 2.5 men a second.

Three things in the log explain most of it:

1. **Athletics empties within ~10 s of contact.** A tier-1/2 soldier has a pool of 50 (the floor), a
   blow costs 10, full strength ends after 2 blows and the bar is empty after 5. By 221 s the average
   man was at f 0.22 — from then on nearly everyone fights under the pause.
2. **Tired men stop defending.** The summary's guard line (share of melee hits on men on foot that were
   blocked or parried):

   | Battle | peak (f 1) | f 0.5–1 | f < 0.5 | empty | **held by the AI timer** | **stepping back** | everyone else |
   |---|---|---|---|---|---|---|---|
   | 80v80 (old technique) | 55% | 34% | 38% | 3% | 6% | 4% | 39% |
   | 120v120 | 71% | 42% | 40% | 24% | **13%** | **6%** | 45% |
   | 240v240 | 67% | 37% | 29% | 16% | **2%** | **5%** | 33% |

   DESIGN §2 says the AI waits out its pause "guard up" and steps back "facing its enemy with its guard
   up". Neither happens: a man under the engine's `NoAttack` flag blocks almost nothing (UNVERIFIED as
   cause — the numbers could partly reflect who gets held, but 2% against 29–67% is hard to explain any
   other way), and a man stepping back has his back turned (mid-step: 80% back turned in all three
   battles; avg 0.59–0.78 m moved of the 2 m asked).
3. **Those two states take a big share of the killing.** 240v240: 311 hits on held men (2% blocked) +
   297 on men stepping back (5%) = ~588 landed hits out of ~1420 landed melee hits — **41%**. If they had
   blocked like their peers (~30%), about 160 fewer hits would have landed (−11%); if the guard were
   really up (~50%), about 290 fewer (−20%). That is lever #2's +10–25%.

So today **exhaustion shortens the melee**: it makes men attack less (the pause works — the timers read
on target) but defend far less. The Athletics knobs (#11) would make battles *shorter* if turned up
before #2 lands.

Other facts worth having: ~3.2–3.6 landed hits per man removed in the step-13 battles (7.9 in the 80v80,
lower damage per hit); routs are ~10% of removals (25 of 219, 21 of 438) — battles end by killing, not
by breaking; the AI's own gap between melee attacks at full strength is ~1.1 s (cycle 1.86 s).

---

## A. How RBM makes battles longer, technique by technique

RBM patches with Harmony at mission start (`RBM.RBMAIPatchLogic.EarlyStart` → `RBMAiPatcher.DoPatching`
→ `PatchAll` over RBMAI; `RBMCombatPatcher`, `RBMTournamentPatcher`); its settings live in
`RBMConfig.RBMConfig` (an XML file, its own screen). The part that makes battles long is almost all
**formation-level AI** plus its **damage/armour rework**; the part that makes them feel "real" is
posture, hit stop and the AI commander's battle plans.

### A1. Frontline micro — `RBMAI\Frontline.cs`
- **What:** a Harmony prefix on `Formation.GetOrderPositionOfUnit` (infantry and ranged formations,
  field battles, more than `FrontlineMinFormationSize` 25 men, **charge orders only**). Each man keeps an
  `AIMindset` with five scores — Attack, BackStep, FindAlly, FlankAllyLeft/Right — fed by what is around
  him: allies in front / left / right within 1.35 m, enemies within 2 m in front, his shield, a shield
  wall, a two-hander, his posture, stamina and health, banner bearers and heroes (heroes lean to
  Attack). Scores move slowly (a square-root damping in `SetValue`), a decision holds for a random 0 to
  `FrontlineDecisionTimerMax` (2 s). Then: Attack = vanilla; BackStep = a spot 0–0.3 m behind him (half
  "away from his target", half "back along the formation's direction"); FindAlly = 0.15–0.3 m toward the
  nearest ally; Flank = 0.15–0.3 m sideways; Rest (posture below half and no enemy in front) = stay put.
  Every move is skipped if another man stands nearer the spot (`IsPositionOccupied`). The move is the
  returned spot **and** `Agent.SetTargetPosition(spot)` (a position lock); the lock is cleared at the
  next query (`ClearTargetFrame`).
- **Effect:** men close gaps and avoid being the lone man in front; tired / broken men give ground a
  little. Fewer 3-on-1 surrounds → slower killing. Size of the effect UNVERIFIED (nothing to measure it
  here).
- **Kind / hook:** AI behaviour, Harmony into the formation. Settings: `FrontlineEnabled`, the four
  weights (`FrontlineAttackWeight`, `…BackStepWeight`, `…FindAllyWeight`, `…FlankWeight`), the timer.
- **Without Harmony:** the idea of the back step, yes (section B); the whole micro, no (it lives inside
  `GetOrderPositionOfUnit`).

### A2. A charge that keeps its formation — several patches
- `OverrideSetMovementOrder` (prefix `Formation.SetMovementOrder`) and `OverrideMovementOrder`
  (prefix `MovementOrder.GetSubstituteOrder`): a plain Charge becomes **Charge-to-target** the closest
  significantly large enemy formation — formations fight formations, not every man the nearest enemy.
- `OverrideFormationMovementComponent` (prefix `HumanAIComponent.GetFormationFrame`): in a
  charge-to-target, every man still gets **his slot in the formation** as his position and faces his
  target, at a non-charging speed. Vanilla gives a charging man no slot at all (the charge order has no
  position, so `GetFormationFrame` switches the frame off and the man fights freely).
- `Frontline.OverrideFormation.Postfix_GetDirectionOfUnit` (postfix `Formation.GetDirectionOfUnit`):
  charging infantry's formation direction = toward the man's own target within 20 m.
- `OverrideParallelFormationMovement` (postfix `HumanAIComponent.ParallelUpdateFormationMovement`):
  keeps men of a moving formation catching up with it; switches the frame off in a plain charge.
- `OverrideUpdateFormationOrders` (prefix `Agent.UpdateFormationOrders`): in a charge-to-target a shield
  man with his target within 7 m raises his shield.
- **Effect:** THE line-battle feel — a charge arrives as a line and stays roughly one; only the front
  fights. Probably RBM's biggest single contributor to long melees, together with A3 (UNVERIFIED).
- **Without Harmony:** no — a charging man's slot can only be given by patching the formation.

### A3. Behaviour values for charging men — `OverrideHumanAIComponent`
- **What:** a postfix on `HumanAIComponent.SetBehaviorValueSet` rewrites the engine's distance curves
  ("how much do I want melee / my slot / to shoot, by distance"). Vanilla's charging infantry want melee
  with weight 8 at 0 m, 4 at 7 m, 1 at 20 m (`BehaviorValueSet.Charge`); RBM's want it 5.5 at 0 m,
  **1 at 2 m**, ~0 at 10 m, and their slot a little more. Cavalry and horse archers get their own curves
  (heavier horses charge harder).
- **Effect:** a charging man only engages what is next to him; the rest keep to the formation (which
  A2 gives them). Together with A2 this is "how many ranks actually fight".
- **Without Harmony:** the values themselves are public (`agent.SetAIBehaviorValues(kind, y1, x2, y2,
  x3, y3)`, the extension over `HumanAIComponent.OverrideBehaviorParams`), and the game resets them on
  every order / arrangement change (`RefreshBehaviorValues`), so we would re-apply them then. But in a
  vanilla charge the man has no slot to hold, so a tight melee curve alone may leave him idle — our
  lever #5 therefore targets holding lines only.

### A4. The AI commander's plans — `Tactics.cs` and friends
- `Tactics.EarlyStartPatch` (postfix `MissionCombatantsLogic.EarlyStart`): in field battles each AI team's
  tactic list is **replaced** — by culture: Empire gets `RBMTacticEmbolon` (a wedge), Aserai
  `RBMTacticAttackSplitSkirmishers`, Sturgia/Nords `RBMTacticAttackSplitInfantry` (split infantry, one
  half flanks — `RBMBehaviorInfantryAttackFlank`), Battania split archers, Khuzait ranged harassment;
  everyone `TacticFullScaleAttack`; defenders also `TacticDefensiveEngagement` / `TacticDefensiveLine`.
  `TacticCoordinatedRetreat` is weighted 0 (a side below 10% power with no foot troops left just breaks:
  morale 0).
- `TacticFullScaleAttackPatch` / `TacticDefensiveEngagementPatch`: while advancing, infantry
  **regroups and advances as a formation** (`BehaviorRegroup` 1.75, charge 0); archers screened
  skirmish; cavalry **protects the flanks**, then charges. `Utilities.HasBattleBeenJoined`: "joined" when
  the main infantry is within **75 m** of a significant enemy or a third of it is in melee — only then
  the charge. `Utilities.FixCharge` resets the infantry to charge weight 1 at that point.
- `OverrideBehaviorCharge` (prefix `BehaviorCharge.CalculateCurrentOrder`): infantry facing enemy
  cavalry **forms a shield wall and holds**; skirmishers pull back 7 m before the lines meet; a big
  infantry formation (≥ 30) charges its target formation and picks shield wall / loose / line by its
  weapons (`DecideArrangementOrderForFormation`).
- New behaviours added to every formation (postfix `TeamAIGeneral.OnUnitAddedToFormationForTheFirstTime`):
  archer skirmish / flank, forward skirmish, infantry flank attack, cavalry charge, embolon, horse archer
  skirmish.
- **Effect:** armies manoeuvre — the melee starts later, as lines, and flanks happen. Tactical feel:
  large. Length: longer mostly because the lines meet as lines (A2/A3).
- **Without Harmony:** a new plan, yes — `Team.ClearTacticOptions` / `AddTacticOption` and
  `FormationAI.AddAiBehavior` / `SetBehaviorWeight` are public, and `TacticComponent` can be subclassed.
  Changing vanilla's own plans the RBM way, no.

### A5. Pace and cohesion — `AdjustSpeedLimitPatch`, `FormationPaceFix`, `FormationCatchUpGate`
- A shield-wall charge not under arrow fire moves at **half speed**; other charges at ≥ 0.9; an
  advancing formation marches at the pace of its **slowest 20%** (`Formation.CacheMovementSpeedOfUnits`
  postfix) so it arrives together; stragglers are allowed a wider gap before the formation waits.
- **Effect:** lines arrive in order (approach time, some feel). **Without Harmony:** no — the formation
  rewrites a man's speed limit every update.

### A6. Per-man combat AI values — `AgentAi.OverrideSetAiRelatedProperties`
- **What:** a postfix on `AgentStatCalculateModel.SetAiRelatedProperties` rewrites the AI's block,
  parry, attack-decision, shield and aim values (`AIBlockOnDecideAbility`, `AIParryOnDecideAbility`,
  `AIAttackOnDecideChance` 0.15 (shield men 0.10–0.15), `AIDecideOnAttackChance` 0.5,
  `AiDefendWithShieldDecisionChanceValue` 1, `AiAttackingShieldDefenseChance` 1, `AIHoldingReadyMaxDuration`
  1 s …), and with stamina on scales several of them down as the man tires.
- **Surprise:** these are **not simply "more defensive"**. At realistic difficulty vanilla gives a
  tier-1 recruit (skill 20) a block ability of 0.62; RBM (its own AI level: skill / 250) gives him 0.30;
  at skill 100 both give ~0.8. Vanilla's attack chance for a
  charging man is 0.144 (our log's "attack chance 0.144"); RBM's 0.15. RBM's long battles do not come
  from here.
- **Without Harmony:** yes, trivially — these are `AgentDrivenProperties`, which our stat decorator
  already writes (step 5e). That is our lever #3.

### A7. Posture and stamina — `StanceLogic`, `Stance`, `PostureDamage`
- **Posture** (30 + 20–80 by skill, + armour weight): every block, parry and chamber drains it
  (postfix `Mission.CreateMeleeBlow`); at 0 the man staggers, drops his weapon or shield, or damage
  crushes through; it refills slowly (3× after 10 s out of combat). Arrows and javelins drain it too.
- **Stamina** (1000 × (1 + 3 × Athletics/500) — Athletics again): drained by blocks, hits and armour
  weight; at empty a man's weapon handling (his block speed) × 0.5, run × 0.85, swing × 0.85, aim worse.
  **The opposite of our rule** "blocking is never slowed". Above 85% stamina a man heals 0.9 HP every
  10 s.
- **Effect on length:** unclear — posture breaks open men up as often as blocks save them. It is mostly
  feel (and the player-side HUD bars we took the style from). **Without Harmony:** no. Not recommended:
  a second bar beside Athletics.

### A8. Morale
- `RBMCombat.CampaignChanges.InitializeMoralePatch` (prefix `CommonAIComponent.InitializeMorale`): every
  man starts at vanilla's **best** roll (35 + 30 + bonuses) instead of 35 + a random 0–29.
- `AgentAi.OnTickPatch` (postfix `HumanAIComponent.OnTick`): a panicked man with morale above 0 who has
  not been hit in melee for **10 s rallies** (`CommonAIComponent.StopRetreating`) and rejoins.
- `AgentAi.ChargeDamageCallbackPatch`: a horse charge that knocks a man down or back may panic him.
- `Tactics.OverrideTacticCoordinatedRetreat`: no orderly retreat; below 10% power with no foot troops a
  side's morale drops to 0.
- **Effect:** fewer early routs, rallies — longer and a bit more swingy. **Without Harmony:** the morale
  numbers yes (`BattleMoraleModel` is an abstract model we can decorate; `GetMorale` / `SetMorale` /
  `ChangeMorale` and `CommonAIComponent.StopRetreating` are public). Our lever #6.

### A9. Damage and armour — the overhaul part (`RBMCombat`)
- `DamageRework.OverrideDamageCalc` (prefix `MissionCombatMechanicsHelper.ComputeBlowDamage`, skips the
  original): RBM's own damage formula — armour per body part (`ArmorRework.ChangeBodyPartArmor`, prefix
  `Agent.GetBaseArmorEffectivenessForBodyPart`) × `ArmorMultiplier` (2), armour thresholds per damage
  type and weapon type, blunt trauma through armour, edge vs flat of the blade, thrust bonuses
  (`MagnitudeChanges`). Plus new item stats (`RBMCombat_*` XMLs: armours, weapons, shields, horses) and
  **new troop equipment** (`RBMCombat_unit_overhaul`, NPCCharacters) — the unit overhaul you do not want.
- **Effect:** armoured men survive many more hits → long battles, tiers matter a lot. Without RBM's item
  data the formula alone would behave differently (UNVERIFIED how).
- **Without Harmony:** a cousin, yes: vanilla routes every hit through the public
  `StrikeMagnitudeCalculationModel.ComputeRawDamage(type, magnitude, armour, absorbed)` (damage × 50 /
  (50 + armour), minus a share of armour), which a decorator can feed "armour × k". Our lever #7.

### A10. Other things RBM does
- **Hit stop** (`HitStopLogic`): a brief time slow on the player's parries and hits — feel.
- **Siege** tweaks (archer points, defender morale in a last stand — `KeepBattleEnabled` is siege-only).
- **Rotation of tired men: RBM has none.** Its only "rest" is a man with broken posture and no enemy in
  front standing still (Frontline `Rest`).
- A **fast-forward key** (Ctrl+V, "Vroom") — a hint that RBM battles run long.

### A11. RBM's battle settings, for reference
AI: `HitStopEnabled`, `PostureEnabled`, `StaminaEnabled`, `PostureGUIEnabled`, `PlayerPostureMultiplier`
(1 / 1.5 / 2), `VanillaCombatAi` (off = its A6 values), `KeepBattleEnabled`, `FrontlineEnabled`,
`FrontlineMinFormationSize` (25), `FrontlineDecisionTimerMax` (2 s), the four Frontline weights (1),
`AiBehaviorLogEnabled`. Combat: `ArmorMultiplier` (2), `ArmorThresholdModifier`, `MaceBluntModifier`,
`BluntTraumaBonus`, `ThrustMagnitudeModifier`, `RealisticRangedReload`, `RealisticArrowArc`,
`PassiveShoulderShields`, `TroopOverhaulActive`, `SneakAttackInstaKill`, per-weapon-type factors, price
modifiers. (Plus a whole campaign economy — not relevant.)

---

## B. The step back: what RBM does, why ours turns, and how to fix it

### What ours does and what the log shows
After a tired AI man's melee swing: `SetScriptedPositionAndDirection` to a spot 2 m straight away from
his target, asked to face him, flags `DoNotRun` + `NoAttack` (+ `ConsiderRotation` from the direction),
held 1.5 s, then `DisableScriptedMovement` (AI_NOTES step 5d). All three battles agree:

| | 80v80 | 120v120 | 240v240 |
|---|---|---|---|
| started | 1080 | 969 | 2254 |
| facing his enemy at the start | 100% | 100% | 100% |
| **back turned mid-step** | 85% | 82% | 80% |
| moved (of 2 m) | 0.78 m | 0.63 m | 0.59 m |
| reached the spot | 15 | 3 | 2 |
| blocked while stepping back | 4% | 6% | 5% |
| swings while stepping back (0 expected) | 13 | 11 | 14 |

Everything else about it works: the engine takes the frame, releases are clean, nothing lingers.

### Why ours turns its back
`GoToPosition` is the AI's **navigation** mode — vanilla uses it to walk a man to a dropped item, a
ladder or a siege engine. A navigating man walks facing his path; the requested direction
(`ConsiderRotation`) is applied **on arrival**. Our spot is 2 m away and a tired man walks slowly, so he
spends the whole 1.5 s "on the way" and turns round at once. The native locomotion itself cannot be
read, so this is inferred from the numbers above (100% facing before, ~80% turned mid-step, 2 arrivals
in 2159) — consistent, but UNVERIFIED as a statement about the engine's code.

### What RBM's BackStep does, exactly (`RBMAI\Frontline.cs`)
1. Where: the prefix on `Formation.GetOrderPositionOfUnit`, which the engine calls when it refreshes a
   man's formation frame — about every 0.45–0.55 s per man (`Agent._cachedAndFormationValuesUpdateTimer`)
   and on order changes.
2. When: charge orders only (plain or to-target), infantry or ranged formations over 25 men, AI men,
   field battles, when his BackStep score wins (crowded front × (2 − posture)) and holds for 0–2 s.
3. How far: a spot **0–0.3 m** behind his current position (the mean of "away from his target" and
   "back along the formation's facing"), skipped if another man is closer to it.
4. How it moves him: it returns that spot as his formation position **and** calls
   `Agent.SetTargetPosition(spot)` — a native position lock (`MovementLockedState.PositionLocked`) that
   vanilla itself uses to walk scripted characters to a point. The lock is removed at the next query
   (`ClearTargetFrame`), and in a charge RBM switches the formation frame off after every update
   (`OverrideParallelFormationMovement`), so the lock is the only thing moving him.
5. Facing: RBM sets **no direction at all** for the back step (a position lock, not a frame lock). His
   facing is left to the combat AI, which keeps him turned to his enemy. RBM also points charging
   infantry's formation direction at their targets (`Postfix_GetDirectionOfUnit`).

**Why his men keep facing:** the destination is never more than a shuffle away, re-picked from where he
stands every ~0.5 s, so the engine never treats it as "walk somewhere" — it is a small adjustment of
where he stands, which the combat AI makes while facing its target. It is also small: over a 0–2 s
decision he gives perhaps 0.2–0.6 m of ground, not 2 m.

### Our options, public API only (best first)

1. **An AI-input component (recommended).** `AgentComponent` has a public virtual
   `OnAIInputSet(ref EventControlFlag, ref MovementControlFlag, ref Vec2 inputVector)`; the engine calls
   it with the AI's input for every man whose `Agent.SetHasOnAiInputSetCallback(true)` is on, before it
   uses it (`Agent.OnAIInputSet`, an engine callback). `inputVector` is his movement in his **own frame**
   (x right, y forward) — RTS Camera turns world directions into it with `Frame.rotation.TransformToLocal`.
   So a step back becomes: for `StepBackSeconds`, write `inputVector = (0, −s)` (backwards at a walk-ish
   size), and in `movementFlag` clear the attack bits (`AttackMask`) and make sure a defend bit is up (the
   AI's own, or `DefendAuto` / `DefendDown` when it has none). No scripted frame, no `NoAttack`, no
   destination: he backpedals like a player holding S, facing whatever the AI faces — his enemy. When we
   stop writing, his own AI and his formation frame take him back.
   - **Proof it is live:** RTS Camera Command System adds a component to every agent
     (`CommandSystemAgentComponent`, `Agent.AddComponent` in `CommandSystemLogic.OnAgentCreated`), turns
     the callback on in `Initialize`, and in `AgentAIInputHandler.OnAIInputSetForDefensiveHold` rewrites
     `inputVector` to walk men back to their slot **without turning them**; its volley mode cancels
     attacks with exactly "clear `AttackMask`, set `DefendDown`" (`SetCancelAttack`).
   - **The same hook fixes the AI timer's guard.** Instead of the engine's `NoAttack` flag (2–13% blocked
     while held), clear only the attack bits during the pause and keep a guard up. A/B-able: the log's
     guard line measures it directly.
   - **And it lets the pause survive a step back** (the BUGS line "Empty AI attacks too fast": today a
     started step back drops the pause, so an empty man — who steps back after every swing — swings again
     1.5 s later; the log reads a 2.2 s cycle at empty against a target of 8.5–9.3 s, and 976 pauses "not
     held: stepping back" in the 120v120). With one component doing both, the attack bits simply stay
     cleared until the later of the two ends: no hand-over between two mechanisms, nothing to drop.
   - Step 14's run-speed floor (0.3 → 0.7) will make a tired man's backpedal faster too — whatever the
     backpedal speed turns out to be, it rides on his current top speed.
   - **Costs and care:** one small component per agent (allocated at spawn, not per tick); the callback
     only on while a man is stepping back or held — but never switch it **off** on a man whose callback
     was already on (RTS Camera needs it on for everyone). With RTS Camera, both components write the same
     input; the later-added one wins (ours would be added after its `OnAgentCreated` one). How often the
     engine calls it, from which thread, how fast a `(0, −1)` backpedal is, and whether the AI's look
     stays on its enemy while its input is overwritten: **UNVERIFIED** — the existing `[summary] step
     back` lines (facing mid-step, metres moved, guard) settle all of it in one battle.
2. **RBM's short hops, without its patch.** `Agent.SetTargetPosition(spot 0.3–0.5 m behind him)`,
   re-picked from his current position every ~0.3 s over the 1.5 s, `ClearTargetFrame()` at the end (and
   on every release path). Public, what RBM itself relies on. Unknowns: in a HOLDING line the formation
   frame stays on and may fight the lock (RBM only does it in a charge, frame off) — UNVERIFIED which
   wins; attacks would still need `NoAttack` or option 1.
3. **Rank swap, for holding lines** — `Formation.SwitchUnitLocations(tired, freshBehind)` (public; vanilla
   uses it for banner bearers): the formation frame, which carries a direction, walks the tired man one
   rank back and brings the fresh man forward. It is the literal "fresh step in", but only where men
   have slots (holding, not charging). That is lever #4.
4. **One-line experiment:** add the engine's `Drag` scripted flag (`Agent.SetDraggingMode(true)`) to our
   current step. Nothing in the game's managed code uses it (the stealth body-drag?); a dragging walk may
   go backwards — or look odd. UNVERIFIED either way; cheap to try.
5. **Hang back** (5d's written fallback): `SetAIBehaviorValues` toward vanilla's shield-wall melee curve
   while tired — he stops pressing instead of stepping back. Safe, but not a visible step back.
6. **Harmony, RBM's way** — not needed if 1 works.

What I would **not** do: shorten our scripted walk to 0.3 m hops (still `GoToPosition` navigation —
might keep facing, might not, and keeps `NoAttack`'s missing guard); `SetMovementDirection` or
`MovementInputVector` from outside the AI (low-level, rewritten by the AI every update).

---

## C. Our own levers, in detail

### #1 Troop damage scale (heroes exempt)
- **What:** in `TraxDamageModel.ApplyGeneralDamageModifiers` (after the base model and our ±50% roll),
  × `TroopDamageScale` when both attacker and victim are non-heroes; separate factors for troop → hero
  and hero → troop (default 1); melee / ranged apart if wanted. "Hero" = `Agent.IsHero` (lords,
  companions, the player) — the flag the Athletics code already reads.
- **Length:** the melee is a stream of landed hits (~3.2–3.6 per man removed); at k they land k as hard,
  so first order the melee lasts 1/k as long: k 0.75 → +33%, 0.6 → +65%, 0.5 → ×2. Medium-high
  confidence (second-order effects: more time for exhaustion and routs).
- **Heroes:** a hero's blow on a troop is unchanged → he kills at the old rate while the battle around
  him slows — his share of the kills grows. With troop → hero also < 1 he survives longer too.
- **Care:** knockdowns and staggers read the final damage (DESIGN §1), so scaled-down troops knock each
  other down less; the log's damage lines would show both numbers. **Cost S, risk low, no Harmony.**

### #2 The guard really up (and the step back fixed)
Section B, option 1: one input component for both the step back and the AI timer's pause; the design
text (DESIGN §2) stays as it is — this makes it true. Keep `StepBackSeconds` / `StepBackDistance` (the
distance becomes "as far as a backpedal gets in the time"). The pause no longer ends when a step back
starts, so an empty man really attacks at 20% (today 2.2 s cycles against 8.5–9.3 s) — that alone
stretches the tired phase. The summary's facing / moves / guard / cycle-by-f lines already measure all
of it. **Cost M, risk medium (the hook's details are UNVERIFIED), no Harmony.**

### #3 Troop caution
- **What:** in `TraxAgentStatModel.UpdateAgentStats` (already writes AI values when
  `AttackRateAiDecisions` is on): for non-hero AI, `AIAttackOnDecideChance` and `AIAttackOnParryChance`
  × `TroopAggression` (say 0.6); optionally `AIBlockOnDecideAbility` / `AIParryOnDecideAbility` raised
  toward 0.99 (better defence — allowed, DESIGN only forbids slowing blocks). Vanilla itself does this
  by order: a charging man's attack chance is 3× a shield-wall man's (`Agent.Defensiveness`: charge 0,
  hold 1, shield wall / square / circle +1).
- **Length:** the AI waits ~1.1 s between melee attacks at full strength; 5e's log showed that scaling
  these chances does stretch that gap. +15–40%, low confidence (the native decision loop is invisible).
- **Heroes:** heroes keep full aggression → up. **Cost S, risk low-medium, no Harmony.**

### #4 Rank rotation
- **What:** a formation-level check every ~1 s (not per man per tick): in a formation holding a line
  (movement state Hold, arrangement line / loose / shield wall), find front-rank men
  (`IFormationUnit.FormationRankIndex == 0`) below an Athletics mark (e.g. f < 0.3) and swap each with
  the freshest man in the rank behind within one file (`Formation.SwitchUnitLocations`), a few swaps per
  formation per check. The formation frame moves both; the strip's "± spread" shows the rotation.
- **Length:** +10–30% for lines that hold (fresh men block 67–71%, empty ones 16–24%); nothing in a
  charge (no slots). Low confidence. **Tactical:** depth becomes a real choice — your line holds as long
  as it has fresh men behind it. It is the man-by-man version of what you did by hand in the 240v240
  (a fresh reserve, which "felt strategic").
- **Care:** every swap rebuilds the line's unit table (`LineFormation.SwitchUnitLocations` →
  `ReconstructUnitsFromUnits2D`) — budget it; men walking through each other mid-melee may look messy;
  whether the frame keeps them facing while they swap is UNVERIFIED (the front ranks get a direction
  force from `ArrangementOrder.CalculateFormationDirectionEnforcingFactorForRank`, the rear ranks less).
  **Cost M-L, risk medium-high, no Harmony.**

### #5 Front ranks only
- **What:** for AI men in a holding line, `agent.SetAIBehaviorValues(Melee, …)` toward vanilla's
  shield-wall curve (weight 4 at 0 m, 0 at 5 m) or tighter, re-applied whenever the game resets them (any
  movement or arrangement order — we already watch those for the step back); heroes exempt.
- **Length / feel:** rear ranks hold their slots instead of flowing round → fewer men per enemy, a line
  that looks like a line. +10–30%, low confidence. **Not in a charge** (no slot to hold without Harmony —
  see A2/A3).
- **Care:** RTS Camera's volley and defensive-hold modes set these same values on their own state changes
  — the two would overwrite each other while those modes are on. **Cost M, risk medium, no Harmony.**

### #6 Morale
- **What:** a `BattleMoraleModel` decorator (the model is abstract and public — the same pattern as our
  two decorators): `GetEffectiveInitialMorale` + x, `CalculateMaxMoraleChangeDueToAgentIncapacitated` × k;
  optionally Athletics-linked (a formation's men lose more morale while exhausted, less while fresh —
  our formation averages already exist) and a rally (a routed man with morale left and 10 s unhit
  rejoins — RBM's rule, public `CommonAIComponent.StopRetreating`). Heroes steadying men near them would
  be a hero lever.
- **Length:** routs are ~10% of removals today, so ±10%. **Tactical:** breaking a tired flank, or holding
  with fresh reserves, starts to win battles. **Cost S (numbers) / M (Athletics-linked + rally), risk
  low-medium, no Harmony.**

### #7 Armour weight
- **What:** decorate `StrikeMagnitudeCalculationModel` and pass `armour × k` into `ComputeRawDamage`.
  Vanilla: damage = magnitude × 50 / (50 + armour), then the cut part loses armour × 0.5 (pierce 0.33,
  blunt 0.2). A 70-magnitude cut: on a recruit (armour 5) 61 → 53 at × 2; on medium armour (20) 40 → 19.
- **Length:** small in tier 1-2 battles like tonight's, large for armoured troops. **Heroes** wear the
  best armour → up. **Care:** it changes what every troop is worth (and how arrows fare) — a balance
  change, though no unit data changes. **Cost S, risk medium, no Harmony.**

### #8 AI line discipline
A battle plan of our own (a `TacticComponent` subclass, added with `Team.AddTacticOption` in our mission
logic; vanilla's options can be cleared first): infantry advances in formation (vanilla's Advance order
keeps slots), charges only when close — or never, pushing with Advance so men keep their slots and #5
applies; archers screened; cavalry on the flanks. The most RBM-like lever and the most work: sieges and
naval battles excluded, every culture and army mix to test. **Cost L, risk high, no Harmony for a new
plan.**

### #11 The Athletics knobs we already have
`CostPerBlow`, `AthleticsPoolFloor`, `ExhaustedAttackSpeedPercent`, the regen settings. Today tired men
defend badly (baseline), so more exhaustion = **shorter** melee. After #2, more exhaustion = fewer
attacks from men who still defend = longer. Retune after #2.

---

## D. Compatibility with what we built, and with RTS Camera Command System

| Lever | Athletics bar | Pause timer (you / AI) | Step back | Damage roll | RTS Camera Command System |
|---|---|---|---|---|---|
| #1 troop damage | — | — | — | after the roll, one more factor; the summary shows both | no overlap (it does not touch damage) |
| #2 guard up | — | **replaces** the AI timer's `NoAttack` with input bits (A/B switch); your timer untouched | **replaces** the scripted walk | — | both rewrite AI input via the same hook; the later component wins; never switch its callback off |
| #3 caution | — | stacks with the timer (the timer is the hard floor) | — | — | no overlap |
| #4 rotation | reads f | — | replaces it for men in a holding line | — | patches formation width / spacing, not slots — probably fine (UNVERIFIED) |
| #5 front ranks | — | — | — | — | its volley / defensive hold set the same behaviour values |
| #6 morale | reads formation averages | — | — | — | no overlap |
| #7 armour | — | — | — | before the roll (inside the game's formula) | no overlap |
| #8 AI plan | — | — | — | — | patches order handling (`OrderController`, `MovementOrder.GetSubstituteOrder` / `GetPositionAux`, `Formation` spacing, `ArrangementOrder`); AI-controlled formations only — test together |

RTS Camera Command System, what it patches (from its DLL): `OrderController` (set orders, facing, the
"active order" queries), `OrderTroopPlacer`, `MissionOrderVM` / order UI, `MovementOrder.GetSubstituteOrder`
and `GetPositionAux` (the same two methods RBM patches), `Formation` (tick, form order, min/max interval
and distance, desired width), `ArrangementOrder`, `FacingOrder`, `HumanAIComponent.GetDesiredSpeedInFormation`,
square and circle shapes. It does **not** patch `GetOrderPositionOfUnit`, `GetDirectionOfUnit`, the
damage or stat models or morale. Its per-man input component (volley, defensive hold) is the one real
neighbour — see #2 and #5. RBM stays declared incompatible (DESIGN §5); nothing here needs it.

---

## Not recommended, and why
- **RBM's frontline micro, posture, pace patches** (#9, #10, A5) — Harmony into formation and combat
  internals; posture would be a second bar beside Athletics.
- **RBM's stamina penalties on defence** (A7) — the opposite of our "blocking is never slowed".
- **A longer start distance** — more walking, not more tactics; you already control the approach.
- **A "second wind" heal for rested men** (RBM: +0.9 HP / 10 s above 85% stamina) — too small to matter;
  only interesting as a hero-only perk.

## UNVERIFIED — only the game can tell
1. Whether `NoAttack` is what drops the held AI's guard (2–13% blocked), or partly who gets held.
2. The native reason our step back turns (navigation faces the path, direction on arrival) — inferred
   from the log.
3. For the input hook (#2): how often and on which thread `OnAIInputSet` runs; how fast a `(0, −1)`
   backpedal moves; whether the AI keeps looking at its enemy while its input is overwritten; whether
   `DefendAuto` works for the AI; the order of our component and RTS Camera's.
4. Whether a position lock (`SetTargetPosition`) beats an active formation frame in a holding line.
5. What the `Drag` scripted flag does to an AI man on foot.
6. Every "Length" estimate except #1's first-order one — each lever's summary lines would measure it.
7. How much of RBM's battle length comes from each of its parts (A1–A9) — nothing here could measure RBM.
8. Whether rank swaps look clean mid-melee and keep the men facing (#4).

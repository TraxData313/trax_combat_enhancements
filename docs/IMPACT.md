# Impact — staggers, dropped shields and knockdowns

**Status: APPROVED by Anton (2026-10-17 night, every §8 recommendation taken) - on the board as steps 47, 48 and 50-54 (I1-I7; step 49,
courage, was inserted between I2 and I3, so I3 = 50, I4 blocks = 51, I5 hits = 52, I6 riders = 53, I7 = 54). Built so far:
step 47 = I1, the spike (Advanced `ImpactSpike`; its verdicts per lever in PLAYTEST D16 - read them before trusting the levers);
step 48 = I2, THE MODEL (Core `Impact.cs`: every §3 formula, every §4 example reproduced by a unit test; all 60 §5 settings in MCM's
"Impact: staggers, shield drops, knockdowns" group; every kick / bash / block / clean hit decided once and logged, the [summary]
"impact" lines - PLAYTEST D17). `ImpactShadowMode` ships OFF (Claude's call, DESIGN decision 40 - Anton wants to see it live in the
morning; the shadow switch still turns every effect into logging only); step 50 = I3, **KICKS AND BASHES LIVE** (2026-10-18: the knockdown
answered in the decorator's seam, the shield dropped from the tick and left pickable - the AI's pickup lockable by `ShieldPickupLockSeconds`,
the reel and the fall-and-rise slowed by the decided factors and MEASURED, their real ends in each man's record for the immunities; never
on a rider; AI_NOTES "Step 50", PLAYTEST D19); step 51 = I4, **BLOCKS AND THE RIPOSTE LIVE** (2026-10-18: the blocker's stun x (1 + T) in
the decorator's worker-thread seam - no die - with the game's cap raised to `ImpactStunCapSeconds` while blocks are live; the rolled stagger
and the riposte = `act_stagger_backward` from the tick at the speed for §3.7's length, measured, the end into the record; a wrong-side shield
drop from the tick; a blocker knocked down by our own blow, its get-up slowed; never on a rider; AI_NOTES "Step 51", DESIGN decision 43,
PLAYTEST D20); step 52 = I5, **CLEAN HITS LIVE + THE REELING BONUS + MOMENTUM** (2026-10-18: a clean hit's knockdown answered in the
decorator's seam like a kick's, its get-up slowed; its stagger = the engine's own flinch slowed to §3.7's length (the flinch's own length
learned in battle), the measured end = the reeling time; tired men never shrug a blow off - the game's threshold × f in the decorator's
`DecideAgentShrugOffBlow`; the reeling bonus real on every family; MOMENTUM - Anton's step-52 wish, §8's "the attacker's run" answered yes:
the attacker's own speed into the blow × up to +60% impact at a sprint, every family, never a rider's; AI_NOTES "Step 52", DESIGN decision 44,
PLAYTEST D21); step 53 = I6, **RIDERS + THE ON-SCREEN LINES + THE GUIDE** (2026-10-18: a rider's knockdown = KNOCKED OFF his horse at 50%
of the chance - a landed blow answered in the damage decorator's `DecideAgentDismountedByBlow` (main thread; the engine asks it instead of
the KnockDown question for a rider), a blocking rider by our own blow flagged `CanDismount` from the tick, each followed until seen off the
horse; never a stagger on a rider, his shield can drop, his block stun like anyone's, horses untouched; the RIDER's MOMENTUM = his horse's
speed, +30% at a gallop (10 m/s), never a couched lance; §8 q12's on-screen lines - red to you, purple by you, white for your side's heroes
behind a switch, at most one per 1.5 s, merged, never in shadow; docs/HOW-IT-WORKS.md §11c rewritten as one player-facing section; PLAYTEST's
IMPACT MORNING SCRIPT + D22; AI_NOTES "Step 53", DESIGN decision 45); step 54 = I7, **THE AI PUNISHES A WINDOW** (2026-10-18, §8 q14 answered yes -
Anton: "otherwise it's a player-only weapon": an AI man on foot whose target reels or lies down - the player's windows too - decides to attack at
`ImpactPunishAggressionPercent` 100 % of the game's AI for the window + 0.5 s, step 43's lever, checked at the fever's ~0.25 s look; a timid man
within 3 m steps in like a surge; AI_NOTES "Step 54", DESIGN decision 46, PLAYTEST D23). **All seven I-steps (I1-I7) are built.** Step 56 (2026-10-18, Anton's tournament: "make sure right blocks are a possible
penalty for the ATTACKER - now I felt them as a penalty for the defender"): **a correct block is the attacker's risk** - §3.2's T counts
only `CorrectBlockTiredWeightPercent` (25 %) on a right-side block or a parry, their ceilings 30 / 10, the riposte (§3.6) × (1 −
`RiposteBlockerTiredPercent` (0) × T_D) instead of × f_D, up to 70 %; the wrong side unchanged; the block stun's lengthening off
(`BlockStunTiredExtraPercent` 0 - the spike's lever 2 FAILED); §4 H has the numbers; DESIGN decision 48, AI_NOTES "Step 56".
Step 62 (2026-10-19, Anton after his battle of 09:30: "more men fall ... the charge matters"): **THE LOST FALLS** - the engine asks the
shrug-off FIRST in `Mission.CreateMeleeBlow` and a weapon blow flagged ShrugOff plays no reaction (14 of his 35 decided falls): the
shrug-off question now decides the blow and refuses itself for a decided knockdown / stagger, a decided fall not seen in 0.5 s = our own
blow (lever 6), a killing blow never decided; §3.3's R × **TOUGHNESS** (t_A ÷ t_D)^0.5 (courage's measure) × a ranged man in melee 1.3 ×
**DIRECTION** (a landed blow from behind ×2, the side ×1.4) - with the momentum at most ×8 together; **THE CHARGE** = sprinting or the first
blow within 1.5 s after a sprint, its speed at least the sprint reference; §3.6's riposte reads the blow WITHOUT its momentum and the
charger's momentum against him (×2 at a sprint), the blocker's toughness against his, its recoil `RiposteStunMaxSeconds` × (0.5 + 0.5 ×
max(T_A, x_r)); a charge into a RIGHT-SIDE SHIELD block THROWN BACK (our blow on him) up to 40 % × S(x_r) × his charge's share; a charging
man's melee blow rolls the full damage upside. DESIGN decision 52, AI_NOTES "Step 62", MECHANICS §11c.
Every number in this plan is a model - the playtest decides.**
(Written as a plan in step 45:) The game-side facts behind every line are in
`docs/RESEARCH.md` §R (file + line in the v1.4.8 decompile); the short version is in `AI_NOTES.md` "Step 45". The build order
(§7) goes into `TASKS_TODO.md` only after Anton says yes.

---

## 0. In one screen

- **One number decides everything: the PRESSURE x on the man who takes the blow.**
  x = **how tired he is** (0 in the green, 1 when empty) **+ how outmatched he is** (both men's CURRENT Athletics points, the
  weapon and the swing against his guard, both men's skill — a factor of 1.4 either way counts as an even match) **+ a bonus
  if he is already reeling from an impact stagger**.
- **Each outcome has a ceiling and a curve**: chance = ceiling × an S-curve of x. Anton's anchors are the ceilings — a kick on
  an exhausted man: shield dropped up to **90%**, knocked down up to **80%**; a block in the right direction: staggered up to
  **80%**. Two even men in the green: **0%**, always.
- **The outcomes**: a longer reel (every kick, every block — continuous, no dice), a **stagger** (a real stagger animation —
  rolled), a **dropped shield** (rolled), a **knockdown** (rolled) with a **slower get-up** (up to ×2), and the **riposte** —
  a fresh, skilful blocker staggers a tired attacker (rolled).
- **Longer and more random the more tired he is**: a stagger lasts 0.6 s fresh to 1.5 s (1.05-1.95) empty; a kicked man reels
  up to ×2.5 longer; a knocked-down man gets up up to ×2 slower.
- **Nobody is stun-locked to death**: after a stagger 2 s of immunity to the next one, after getting up 5 s of immunity to the
  next knockdown, and every outcome halves the next chance against the same man for 8 s.
- **No Harmony needed.** Everything goes through our two model decorators (they already exist), two public `Agent` calls
  (`SetActionChannel`, `SetCurrentActionSpeed`), `DropItem`, and — for knocking down a man who BLOCKED — a blow of our own,
  the way vanilla builds its drowning blow.
- **Build order**: a SPIKE first (I1 — prove each engine lever in a real battle), then the model in SHADOW mode (I2 — it
  decides and logs, changes nothing), then kicks (I3), blocks (I4), clean hits (I5), riders / feedback / guide (I6).

---

## 1. Anton's wish, in order (2026-10-17 — all twelve points)

| # | Anton's words, short | Where in this plan |
|---|---|---|
| 1 | A kick / bash may make the opponent DROP HIS SHIELD — 0% in the green, up to 90% fully exhausted (break an exhausted turtle: tournaments, 1v1s) | §3.5 kick row, §4 A / D |
| 2 | A blow blocked in the RIGHT direction (shield or parry) may STAGGER the blocker — 0% green, up to 80% exhausted; a counter-attack window, LONGER (random) the more tired; the player too | §3.5 right-block / parry rows, §3.7, §3.8 |
| 3 | A kicked / bashed exhausted man must reel LONGER — today the NPC always recovers in time to block | §3.5 kick row ("the reel"), §3.7 |
| 4 | A kick / bash may KNOCK DOWN — up to 80% exhausted; an exhausted man gets UP slower, up to ×2 | §3.5 kick row, §3.7 |
| 5 | A WRONG-direction block: the tired blocker can be staggered, even his shield dropped; the attacker's WEAPON and SPEED count | §3.3, §3.5 wrong-side row, §4 B |
| 6 | A RIGHT-direction block can still stagger, even knock down, when the mismatch is huge (a peasant's stick against a veteran berserker's two-handed axe) | §3.2 (the mismatch), §4 C |
| 7 | A fresh veteran's right-direction block can STAGGER THE ATTACKER (a riposte window), longer the worse the attacker's condition | §3.6 |
| 8 | A man already STAGGERED: a kick very likely knocks him down; even a weapon hit can — weapon, swing speed, bluntness, the Athletics difference | §3.2 (the reeling bonus), §4 E |
| 9 | A normal UNDEFENDED blow that lands can knock down — same conditions | §3.5 clean-hit row |
| 10 | The user's SKILL with the weapon used | §3.3 (skill) |
| 11 | THE ATHLETICS DIFFERENCE: both men's CURRENT Athletics — pool size AND how full. A 200 at 100% against a 50 at 20% is very bad for the 50; as the player grows he kicks weaklings around, but cannot kick-win a tournament against kings with 200+ | §3.2 (body), §4 B / D |
| 12 | "also add the damage dealt be able to …" (unfinished) | §8 question 1 (best guesses; a parameter is ready at 0) |

---

## 2. What the game lets us do (RESEARCH §R, in short)

| Lever | Where | What it gives | Risk |
|---|---|---|---|
| `DecideAgentKnockedDownByBlow` | our damage decorator | knockdown on any LANDED blow — kicks and bashes too (vanilla never knocks down with a kick) | low — vanilla's own question |
| `DecideAgentDismountedByBlow` | the same | a rider knocked off | low |
| `DecideAgentShrugOffBlow` / `CalculateStaggerThresholdDamage` | the same | whether a landed blow makes him flinch at all | low |
| `CalculateDefendedBlowStunMultipliers` | the same | the BLOCKER's guard frozen longer; the ATTACKER's recoil longer (riposte) — by ref; capped at 0.8 s unless we raise the game's `StunPeriodMax` | low; runs OFF the main thread |
| `DecideCrushedThrough` | the same | a blow breaks through the guard and lands | low |
| `SetActionChannel(act_stagger_*)` | from our tick | a real stagger animation, at the speed we ask | medium — RBM uses it; riders / horses crash |
| `SetCurrentActionSpeed` | from our tick | the engine's own reel / fall-and-rise slowed | medium — unproven on engine-started actions |
| `DropItem` (queued) | from our tick | the shield lands on the ground, pickable | low — vanilla's drop key |
| a blow of our own (`RegisterBlow`, KnockDown, 1 damage) | from our tick | a man who BLOCKED is knocked down (a blocked blow is never registered by vanilla) | medium — 1 HP, our own callbacks see it |

**The one hard fact that shapes the design**: vanilla computes knockdown / knockback for a BLOCKED blow and throws the answer
away (it never registers a blocked blow). So "a block knocks him down" needs our own blow (or a crush-through), and "a block
staggers him" means the blocker's stun or a stagger animation of ours — never the game's knockdown question.

---

## 3. The model

### 3.1 Words

- **A** = the attacker (who kicked, bashed or swung); **D** = the defender (who was hit, or who blocked). For the riposte (§3.6)
  the roles turn round.
- **bar** = his points ÷ his FULL pool (step 23's reading). **f** = his peak share — 1 at or above the peak line (75% of the
  bar), down to 0 when empty (step 5c). **T = 1 − f** = how TIRED he is: 0 in the green, 1 empty.
- **points** = his CURRENT Athletics points (pool × bar) — the number that holds both "how big" and "how full" (Anton's point 11).
- An **impact stagger** = a stagger this system started or lengthened (not a fresh man's ordinary vanilla flinch).

### 3.2 The pressure x

x = T_D + `ImpactMismatchWeight` × band(log₂ R) + (reeling ? `ImpactStaggeredBonus` : 0) + `ImpactDamageWeight` × damage ÷ his
max health

- **T_D** — the defender's tiredness. This alone gives Anton's anchors: in the green 0, empty 1.
- **R — the mismatch**, a product of three ratios (each 1 for two identical men):
  R = (B_A ÷ B_D)^`ImpactBodyExponent` × (I_A × K_A) ÷ (G_D × K_D)
  - **Body** B = his current points, never below `ImpactBodyFloorPoints` (10). Exponent 0.5: four times the points = twice the
    body. A 200 at 100% (200 points) against a 50 at 20% (10 points): ×4.5.
  - **I = the blow's impact**, **G = the defender's guard**, **K = skill** (§3.3).
- **band(m)** — the even-match band: a mismatch inside ± `ImpactEvenBandDoublings` (0.5 = a factor 1.41) counts as nothing;
  beyond it, only the part beyond. So two men of a kind in the green are ALWAYS at 0, and a factor of 2 against you starts to
  tell. Negative (the attacker is the weaker) subtracts — a fresh king is safe from a tired recruit.
- **Weight 0.25 per doubling**: R = 4 (two doublings, 1.5 beyond the band) adds 0.375 — a fresh man under that pressure is
  where a half-tired one is.
- **Reeling bonus** (`ImpactStaggeredBonus` 0.75): he is inside an impact stagger (ours, or a reel we lengthened). A fresh man's
  ordinary flinch does not count — so a kick-chain on a fresh man is impossible (his first kick only knocks him back, as vanilla).
- **Damage** (`ImpactDamageWeight`, default 0 = off until Anton answers §8 question 1): a blow that takes 30% of his health
  would add 0.3.

### 3.3 Impact, guard, skill (the weapon and the swing — points 5, 6, 8, 10)

- **Impact of a swing / thrust** I = (weight ÷ `ImpactRefWeightKg` 1.5)^`ImpactWeightExponent` 0.5 × (swing speed ×
  his animation share ÷ `ImpactRefSwingSpeed` 90)^`ImpactSpeedExponent` 1.0 × the damage type (`ImpactBluntFactor` 1.3,
  `ImpactCutFactor` 1.0, `ImpactPierceFactor` 0.8). The swing speed = the weapon's modified swing (or thrust) speed; his
  animation share = step 20b's tired swing (85% at empty) — a tired man's blow carries less. Heavier at faster = bigger, as
  Anton asked. Examples: a 0.6 kg stick 0.87, a 1.2 kg sword 0.91, a 1.8 kg mace 1.34, a 3 kg two-handed axe 1.26.
- **Kick** I = `ImpactKickFactor` 1.2. **Bash** I = (shield weight ÷ 1.5)^0.5 × `ImpactBashFactor` 1.0.
- **Guard** G: a right-side shield block `ImpactGuardShieldRight` 1.0 × (shield weight ÷ 1.5)^0.5 (a 4 kg shield 1.63); a weapon
  parry `ImpactGuardParry` 0.8 × (his weapon's weight ÷ 1.5)^0.5; a WRONG-side shield block `ImpactGuardShieldWrong` 0.4 × the
  shield's; no guard (a clean hit, a kick) `ImpactGuardNone` 1.0 — his body only.
- **Skill** K = `ImpactSkillMinFactor` 0.75 + (`ImpactSkillMaxFactor` 1.25 − 0.75) × min(1, skill ÷ 300): the attacker's skill
  with the weapon he used (a kick: Athletics; a bash: one-handed), the defender's with what he guards with (shield: one-handed;
  a parry: his weapon's skill; no guard: Athletics). A recruit (20) 0.78, a veteran (200) 1.08, a 300 lord 1.25.

### 3.4 From pressure to chance

chance = ceiling × S((x − from) ÷ (1 − from)) × `ImpactDiminishFactor`^n, with S(t) = 3t² − 2t³ for t in 0..1 (0 below, 1
above) — the smooth S-curve: slow near the green, steep in the middle, flat at the top.

- **from** per outcome: stagger `ImpactStaggerFrom` 0, shield drop `ImpactShieldDropFrom` 0.2, knockdown `ImpactKnockdownFrom`
  0.35 — the worse the outcome, the more pressure it needs before it can happen at all.
- **n** = the outcomes this man has suffered in the last `ImpactDiminishWindowSeconds` (8 s); factor 0.5 (§3.9).
- One roll per outcome per blow, in the order knockdown → shield drop → stagger; a knockdown replaces the stagger; a knockdown
  and a dropped shield can come together (the exhausted turtle broken).

### 3.5 The per-event table (the ceilings — all parameters)

| Event (how we know it) | Always (no dice) | Stagger | Shield dropped | Knockdown |
|---|---|---|---|---|
| **Kick / bash lands** (landed alternative attack) | the vanilla knock-back reel, ×(1 + 1.5 T_D × the roll) longer (point 3) | — (the reel IS the stagger) | `KickShieldDropMaxPercent` 90 | `KickKnockdownMaxPercent` 80 |
| **Right-side shield block** | the blocker's stun ×(1 + T_D) | `RightBlockStaggerMaxPercent` 80 (step 56: 30) | `RightBlockShieldDropMaxPercent` 0 | `RightBlockKnockdownMaxPercent` 40 (step 56: 10) |
| **Weapon parry** | the parrier's stun ×(1 + T_D) | `ParryStaggerMaxPercent` 80 (step 56: 30) | — | `ParryKnockdownMaxPercent` 40 (step 56: 10) |
| **Wrong-side shield block** | the blocker's stun ×(1 + T_D) | `WrongBlockStaggerMaxPercent` 90 | `WrongBlockShieldDropMaxPercent` 50 | `WrongBlockKnockdownMaxPercent` 40 |
| **Clean hit lands** (weapon, not blocked) | a tired man never shrugs a blow off (the threshold × f) | `HitStaggerMaxPercent` 80 (his flinch lengthened) | — | `HitKnockdownMaxPercent` 50 |
| **Any of these on a man reeling from an impact stagger** | — | (immune — §3.9) | as its row, x + 0.75 | as its row, x + 0.75 |
| **Riposte** (a right-side block or parry, §3.6) | — | `RiposteStaggerMaxPercent` 50 (step 56: 70) — the ATTACKER | — | — |

**Step 65 - the kicked man BRACES.** The kick / bash row's "always" knock-back is now asked first: the brace fails with S((x + 0.7) ÷ 1.7) on
the kick's own x (`KickBraceFromPressure` / `ToPressure`); braced = no knockdown, no shield drop, the knock-back refused, the leftover reaction
×0.2; failed = the row as before, the reel × (0.4 + 0.6 T_D); below 20% of his bar no brace (DESIGN decision 58, MECHANICS §11c). The AI now
kicks and bashes too (decision 57).

**Step 56 - a CORRECT block (right-side shield, parry) is the attacker's risk.** For these two rows only, x = `CorrectBlockTiredWeightPercent`
(0.25) × T_D + w × band(log₂ R) (+ reeling, + damage): the blocker is broken mainly by a BIG mismatch (§4 C's stick against the axe still
staggers), never by his own tiredness alone; the WRONG-side row, kicks and clean hits keep T_D in full - the direction of the block is what pays.
The "always" column's stun lengthening is OFF since step 56 (`BlockStunTiredExtraPercent` 0: the spike's lever 2 FAILED - "the stun does not
lengthen what the engine plays"); the seam remains, x 1 = the game's stun. The numbers: §4 H.

How each outcome is MADE (RESEARCH §R): a stagger on a block = `act_stagger_*` on channel 0 (the direction from the blow) at the
speed that gives the rolled length; a stagger on a clean hit = the engine's own flinch, slowed; the kick's reel = the engine's
own knock-back, slowed; a knockdown on a landed blow = the KnockDown flag (plus KnockBack); a knockdown on a BLOCK = a blow of
our own (1 damage, KnockDown) or a crush-through — spike I1 picks; a shield = `DropItem`, queued, never during his own swing or
bash (vanilla's rule); a slower get-up = his fall-and-rise action slowed.

### 3.6 The riposte (point 7)

The same pressure with the roles turned: x_r = T_A + 0.25 × band(log₂ R_r), R_r = (B_D ÷ B_A)^0.5 × (G_D × K_D) ÷ (I_A × K_A);
chance = `RiposteStaggerMaxPercent` 50 × S(x_r) × **f_D** (only a defender in the green ripostes at full chance — "a FRESH
veteran"). Made by the attacker's recoil stun (`CalculateDefendedBlowStunMultipliers`, attacker side): his vanilla 0.1-0.25 s
recoil grows to `RiposteStunMaxSeconds` × (0.5 + 0.5 T_A) — up to 1.0 s for an empty attacker — the worse his condition, the
longer (Anton). Examples: a veteran (200 at 100%, skill 200) blocking a recruit (50 at 40%) 50%; the same recruit fresh 20%; two
even fresh men 0%; a fresh man parrying an equal at 20% 45%.

**Step 56 (Anton's tournament, 2026-10-18) - the riposte is driven by the ATTACKER:** chance = `RiposteStaggerMaxPercent` (70) × S(x_r) ×
(1 − `RiposteBlockerTiredPercent` (0) × T_D), x_r as above (T_A + the mismatch turned - his impact against the guard's stability, both bodies,
both skills). The blocker's own tiredness no longer saves a tired attacker (it still enters through his smaller body); 100 gives step 48's ×
f_D back. Two EMPTY even men: x_r = 1 → **70%**, the blocker himself ≤ 5% staggered, never down; a fresh attacker into any guard ~0 (1% off
a 4 kg shield). Made since step 51 by a forced stagger of the attacker (`act_stagger_backward`), its length the recoil above. The numbers
(the game's tired swing 0.85 + 0.15 f; swords 1.2 kg at 92, a 4 kg shield): the veteran blocking the recruit at 40% **70%**, the recruit
fresh 28%, a fresh man parrying an equal at 20% 66%, the veteran himself at 50% still 70%; the attacker at 50% / 25% / 0% into a FRESH guard
41% / 70% / 70%, into a guard as tired as he is 28% / 62% / 70%, into an EMPTY guard 18% / 52% / 70%.

### 3.7 How long (the randomness grows with tiredness — points 2, 3, 4)

Every length = base × (1 + extra × T) × a roll in [1 − r, 1 + r], r = `ImpactRandomPercent` 30% × T (a fresh man's stagger is
predictable, an exhausted man's is not), never above its cap.

| T (bar) | Stagger (`StaggerBaseSeconds` 0.6, +150%) | Kick reel (+150%) | Get-up (+100%, `GetUpMaxSlowdown` 2) | Block stun (+100%) |
|---|---|---|---|---|
| 0 (green) | 0.60 s | ×1.00 | ×1.00 | ×1.00 |
| 0.25 (bar ~56%) | 0.82 s (0.76-0.89) | ×1.38 | ×1.25 | ×1.25 |
| 0.5 (bar ~38%) | 1.05 s (0.89-1.21) | ×1.75 | ×1.50 | ×1.50 |
| 0.75 (bar ~19%) | 1.27 s (0.99-1.56) | ×2.12 | ×1.75 | ×1.75 |
| 1 (empty) | 1.50 s (1.05-1.95) | ×2.50 | ×2.00 | ×2.00 |

Caps: a stagger `StaggerMaxSeconds` 2.0; a stun `ImpactStunCapSeconds` 2.0 (the game's `StunPeriodMax` 0.8 raised to it while
the system is on; every blow we do not lengthen keeps vanilla's 0.8 — we clamp it ourselves).

### 3.8 Who

- **Everyone on foot**: AI soldiers, heroes, lords, the player — as attacker AND as victim (Anton: "applies to the player too").
  `ImpactOnPlayerPercent` 100 scales every chance against you (0 = you are never staggered / disarmed / knocked down).
- **Tournaments, arenas, duels**: the same rules — that is where Anton wants the exhausted turtle broken.
- **Riders** (proposal, §8 question 4): never a forced stagger (the horse's legs share channel 0 — RBM crashed a horse with it);
  a "knockdown" becomes **knocked off the horse** at `ImpactRiderDismountPercent` 50% of the knockdown chance; stun multipliers
  apply. **Horses**: untouched (vanilla rears). (Step 53 built it so: the dismount in `DecideAgentDismountedByBlow`, a blocking rider by
  our own `CanDismount` blow; his shield can drop; the rider's own blows carry his horse's speed at +30% - DESIGN decision 45.)
- **Not**: missiles, a man using a siege engine or ladder, a man in water, friendly fire, the dead.

### 3.9 Anti-abuse — nobody is stun-locked to death

- **Stagger immunity** `ImpactStaggerImmunitySeconds` 2.0 from the END of an impact stagger: no new forced stagger (his stun can
  still grow — that is vanilla's guard freeze, not a lock).
- **Knockdown immunity** `ImpactKnockdownImmunitySeconds` 5.0 from getting UP.
- **Diminishing returns**: each outcome in the last 8 s halves the next chance against the same man (§3.4).
- **No chain on the fresh**: the reeling bonus needs an impact stagger — a fresh man's ordinary flinch does not give it (§3.2).
- **The kick has a price already**: 3 Athletics (step 18) and one stored swing (step 32) — kick-spam empties the kicker.
- **A dropped shield** stays on the ground: the AI picks shields up only when no enemy is within 20 m (vanilla), so he fights the
  rest of the melee without it; you pick it up with the use key. `ShieldPickupLockSeconds` 0 (a lever if re-pickup feels cheap).
- **Per tick**: at most `ImpactMaxActionsPerTick` 20 engine calls (forced actions, drops, own blows) - the rest wait a tick.

### 3.10 With what we already have

| System | Interplay |
|---|---|
| Athletics (5c), costs (18, 22) | the only inputs for the body and the tiredness; no new costs (an option: a knockdown costs the victim points — not proposed) |
| Pause (13), swing stack (32) | a staggered or reeling man cannot attack; his pause and stack run on as they do now; a riposte only pays if the defender has a swing stored |
| Brace (23) | a bracing man is a turtle — exactly the man impact can break; nothing in brace changes |
| Tired block direction (30) | tired men already guess wrong more often → more WRONG-side blocks → more impact: the two compound (watch it in the summary) |
| Fever (43) | feverish men attack more → more blocks and kicks on both sides; no "poise" from a fever (§8 question 10) |
| Damage roll / reduction (4, 25) | vanilla's own knockdowns stay (we OR ours on top); our roll moves the damage share only if `ImpactDamageWeight` > 0 |
| Shield wall hold (41/42) | a staggered man in a wall stays in his slot; a dropped shield opens a gap — the wall's hold does not change |
| Step back (5d), sprint (33) | untouched; a knocked-down man cannot step back or sprint until he is up |
| The master switch | off = vanilla at once: the decorators give the game's answers, nothing queued is played, `StunPeriodMax` back to 0.8 |

---

## 4. Worked examples (computed with the defaults above; S-curve, band 0.5, weight 0.25)

**A. Two even men** (pool 100, skill 100, a 1.2 kg sword, a 4 kg shield); the attacker fresh, the defender at the bar shown.

| Defender's bar | T_D | Kick: shield / down | Right block: stagger / down | Parry: stagger / down | Wrong side: stagger / shield / down | Clean hit: stagger / down |
|---|---|---|---|---|---|---|
| 100% | 0 | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 / 0 | 0 / 0 |
| 75% (peak line) | 0 | 0 / 0 | 0 / 0 | 0 / 0 | 1 / 0 / 0 | 0 / 0 |
| 50% | 0.33 | 14% / 1% | 21% / 0 | 31% / 1% | 39% / 12% / 3% | 21% / 0 |
| 25% | 0.67 | 82% / 70% | 59% / 19% | 77% / 36% | 88% / 48% / 38% | 68% / 35% |
| 10% | 0.87 | 90% / 80% | 79% / 39% | 80% / 40% | 90% / 50% / 40% | 80% / 50% |
| 0% | 1 | 90% / 80% | 80% / 40% | 80% / 40% | 90% / 50% / 40% | 80% / 50% |

Two EMPTY even men: every kick 90% / 80% — an exhausted 1v1 ends on the ground. (Step 48's tests: the 75% row's wrong side is
0.6% - a fresh attacker against a man at 75 points with a 0.4 guard is just beyond the even band; it read 0 before. Every other figure
of §4 comes out of `ImpactMath` within the doc's rounding.)

**B. The 200-at-100% king against the 50-at-20% man** (king skill 250, man skill 60; swords, 3.5 kg shields)

| What | Pressure x | Shield | Down | Stagger |
|---|---|---|---|---|
| The king kicks the man | 1.33 | 90% | 80% | (reels ×2.1) |
| The king swings, the man blocks on the right side | 1.09 | — | 40% | 80% |
| … the man blocks on the WRONG side | 1.42 | 50% | 40% | 90% |
| The man kicks the fresh king | −0.46 | 0 | 0 | (a vanilla knock-back only) |
| The man swings, the fresh king blocks | −0.76 | — | 0 | 0 |
| The man kicks the king at 30% | 0.35 | 9% | 0 | (reels ×1.9) |
| The man kicks the king at 0% | 1.00 | 90% | 80% | — an empty king is as weak as anyone |

**C. The peasant's stick against the veteran berserker's two-handed axe** (berserker pool 180, skill 200; peasant pool 50,
skill 20; the peasant parries with a 0.6 kg stick)

| What | x | Stagger | Down |
|---|---|---|---|
| The axe on a FRESH peasant's parry | 0.55 | 46% | 9% |
| The axe on the peasant's parry at 50% | 1.01 | 80% | 40% |
| The axe lands clean on the fresh peasant | 0.31 | 18% | 0 |
| The peasant's stick on the fresh berserker's shield | −0.45 | 0 | 0 |

**D. The player (Athletics 100, skill 100) against a tournament king (220, skill 250)**

| What | x | Shield | Down |
|---|---|---|---|
| You kick the FRESH king | −0.04 | 0 | 0 — you cannot kick-win against a fresh king |
| You kick the king at 60% | 0.20 | 0 | 0 |
| You kick the king at 30% | 0.60 | 45% | 26% |
| You kick the king at 10% | 0.99 | 90% | 80% — tire him first, then break him |
| The king kicks you, fresh | 0.17 | 0 | 0 |
| The king kicks you at 40% | 0.80 | 76% | 62% |
| The king swings, you block on the right side at 40% | 0.53 | — | 7% (stagger 43%) |

**E. The weapon counts** (even men, the defender at 50% → at 35%):

| Attacker's weapon | Wrong-side block: stagger / shield / down | Parry: stagger / down |
|---|---|---|
| 0.6 kg stick (blunt) | 36 / 11 / 2% → 71 / 34 / 22% | 29 / 1% → 59 / 19% |
| 1.2 kg sword (cut) | 39 / 12 / 3% → 73 / 36 / 24% | 31 / 1% → 61 / 21% |
| 1.8 kg mace (blunt) | 58 / 24 / 13% → 85 / 46 / 35% | 47 / 10% → 74 / 33% |
| 3 kg two-handed axe (cut, slower) | 54 / 22 / 11% → 83 / 44 / 34% | 44 / 8% → 72 / 31% |

**F. A man already reeling from an impact stagger** (even men): fresh — a kick knocks him down 54% (shield 69%), a sword blow
34%; at 50% — a kick 80% (shield 90%), a sword or a mace 50%.

**G. The riposte**: see §3.6 (50% / 20% / 0% / 45%).

(A-G are the STEP-48 model - a correct block counting T_D in full, its ceilings 80 / 40, the riposte 50 % × f_D; `ImpactTests` keeps
them with those weights pinned. Step 56's answer follows.)

**H. Step 56 - right blocks favour the defender** (defaults.json: right block / parry 30 / 10, riposte 70, a correct block counting 25 % of
T_D, the riposte not lowered by T_D; the attacker's tired swing 0.85 + 0.15 f as the game plays it; Core `ImpactMath`, `RightBlocksTests`):

| Two even men | Right-side block: stagger / down — riposte | Parry: stagger / down — riposte | Wrong side: stagger / shield / down |
|---|---|---|---|
| attacker fresh, blocker at 50% | 1% / 0 — 0 | 2% / 0 — 0 | 39 / 12 / 3% |
| attacker fresh, blocker at 25% | 2% / 0 — 0 | 10% / 0 — 0 | 88 / 48 / 38% |
| attacker fresh, blocker EMPTY | 8% / 0 — 0 | 21% / 4% — 0 | 90 / 50 / 40% |
| both at 50% | 0 / 0 — 28% | 1% / 0 — 18% | 23 / 4 / 0% |
| both at 25% | 0 / 0 — 62% | 2% / 0 — 52% | 67 / 31 / 19% |
| both EMPTY | 1% / 0 — **70%** | 5% / 0 — **70%** | 90 / 50 / 40% |
| both fresh | 0 / 0 — 1% | 0 / 0 — 0 | 0 / 0 / 0 |

The king and the man (§4 B): the king's blow on the man's right-side block 17% / 2% (was 80 / 40); the man's swing on the fresh king's
shield - the king ripostes 70%. You at 40% blocking the 220 king: right side staggered 2%, never down (was 43% / 7%); the WRONG side 85% /
shield 46% / down 35%. **The stick and the axe (§4 C) still staggers:** the axe on the fresh peasant's parry 17% / 2% (x 0.55 - all of it the
mismatch), at half a bar 26% / 7%, empty 30% / 10% (the ceilings); the stick on the fresh berserker's shield - he ripostes 30%. A rider (§3.8):
a right-side block never knocks him off (x below the knockdown's 0.35 start).

---

## 5. Parameters (proposed defaults — they enter DESIGN's table and `defaults.json` only in the step that builds them)

| Group | Setting | Default | Meaning |
|---|---|---|---|
| Switches | `ImpactEnabled` | true | the whole system (after `ModEnabled`, `AthleticsEnabled`) |
| | `ImpactKicksEnabled` / `ImpactBlocksEnabled` / `ImpactHitsEnabled` / `ImpactRiposteEnabled` | true | one per event family (A/B in a battle) |
| | `ImpactShadowMode` | false | decide and log, change nothing (planned true for I2 until I3; shipped false - DESIGN decision 40) |
| Pressure | `ImpactMismatchWeight` | 0.25 | x per doubling of the mismatch beyond the band |
| | `ImpactEvenBandDoublings` | 0.5 | a mismatch inside ±0.5 doublings (×1.41) is an even match |
| | `ImpactBodyExponent` | 0.5 | how the points ratio turns into body strength |
| | `ImpactBodyFloorPoints` | 10 | an empty man still has a body |
| | `ImpactStaggeredBonus` | 0.75 | x added on a man reeling from an impact stagger |
| | `ImpactDamageWeight` | 0 | x per share of his max health the blow took (Anton's point 12 — §8 q1) |
| Weapon | `ImpactRefWeightKg` / `ImpactWeightExponent` | 1.5 / 0.5 | the weight's part of the impact |
| | `ImpactRefSwingSpeed` / `ImpactSpeedExponent` | 90 / 1.0 | the swing's part |
| | `ImpactBluntFactor` / `ImpactCutFactor` / `ImpactPierceFactor` | 1.3 / 1.0 / 0.8 | the damage type |
| | `ImpactKickFactor` / `ImpactBashFactor` | 1.2 / 1.0 | a kick's impact; a bash's × the shield's weight factor |
| Skill | `ImpactSkillMinFactor` / `ImpactSkillMaxFactor` | 0.75 / 1.25 | skill 0 → 300 |
| Guard | `ImpactGuardShieldRight` / `ImpactGuardParry` / `ImpactGuardShieldWrong` / `ImpactGuardNone` | 1.0 / 0.8 / 0.4 / 1.0 | × the guarding item's weight factor (not for None) |
| Curve | `ImpactStaggerFrom` / `ImpactShieldDropFrom` / `ImpactKnockdownFrom` | 0 / 0.2 / 0.35 | where each outcome's S-curve starts |
| Ceilings | `KickShieldDropMaxPercent` / `KickKnockdownMaxPercent` | 90 / 80 | Anton's anchors |
| | `RightBlockStaggerMaxPercent` / `RightBlockShieldDropMaxPercent` / `RightBlockKnockdownMaxPercent` | 80 / 0 / 40 | |
| | `ParryStaggerMaxPercent` / `ParryKnockdownMaxPercent` | 80 / 40 | |
| | `WrongBlockStaggerMaxPercent` / `WrongBlockShieldDropMaxPercent` / `WrongBlockKnockdownMaxPercent` | 90 / 50 / 40 | |
| | `HitStaggerMaxPercent` / `HitKnockdownMaxPercent` | 80 / 50 | |
| | `RiposteStaggerMaxPercent` | 50 | |
| Lengths | `StaggerBaseSeconds` / `StaggerTiredExtraPercent` / `StaggerMaxSeconds` | 0.6 / 150 / 2.0 | §3.7 |
| | `KickReelTiredExtraPercent` | 150 | the kicked man's reel |
| | `GetUpMaxSlowdown` | 2.0 | an empty man gets up ×2 slower |
| | `BlockStunTiredExtraPercent` | 100 | every block's stun |
| | `RiposteStunMaxSeconds` | 1.0 | the riposted attacker's recoil |
| | `ImpactRandomPercent` | 30 | the roll's spread at empty (× T) |
| | `ImpactStunCapSeconds` | 2.0 | the game's 0.8 s stun cap raised to this |
| Limits | `ImpactStaggerImmunitySeconds` / `ImpactKnockdownImmunitySeconds` | 2.0 / 5.0 | §3.9 |
| | `ImpactDiminishWindowSeconds` / `ImpactDiminishFactor` | 8 / 0.5 | §3.9 |
| | `ShieldPickupLockSeconds` | 0 | 0 = vanilla pickup |
| Who | `ImpactOnPlayerPercent` | 100 | every chance against you × this |
| | `ImpactRiderDismountPercent` | 50 | a rider's "knockdown" = knocked off, at this share |
| | `ImpactTiredNeverShrugsOff` | true | the flinch threshold × f |
| Momentum (step 52) | `ImpactMomentumEnabled` / `ImpactMomentumPercent` | true / 60 | the attacker's speed into the blow adds up to +60% impact |
| | `ImpactMomentumWalkMetresPerSecond` / `ImpactMomentumSprintMetresPerSecond` | 2.0 / 7.0 | nothing up to a walk, all of it from a sprint |
| Riders + lines (step 53) | `ImpactMomentumRiderPercent` / `…RiderWalkMetresPerSecond` / `…RiderGallopMetresPerSecond` | 30 / 3.0 / 10.0 | a rider's blow carries his horse's speed (never a couched lance) |
| | `ImpactShowMessages` / `ImpactMessagesIncludeYourMen` / `ImpactMessageMinSeconds` | true / false / 1.5 | §8 q12's on-screen lines |
| Plumbing (constants, documented) | `ImpactMaxActionsPerTick` 20, the watch list's size, the skill's 300 | | |

---

## 6. How it will show (logging and the player)

- `[impact]` lines: the settings at mission start (with the anchors in words: "a kick on an empty even man: shield 90%, down
  80%"); the FIRST of each outcome per battle in full ("Rhagaea's kick on Aldric (bar 12%, T 0.84; points 190 vs 12, kick vs
  no guard, skill 230 vs 60 → x 1.21): shield dropped (90%), knocked down (80% → rolled 0.41) - gets up ×1.9 slower"); YOU
  lines (what happened to you, and what you did); verbose per event in its own bucket.
- `[summary] impact`: events by kind × the defender's bar band (100-75 / 75-50 / 50-25 / 25-0) → chance asked (mean) → rolled →
  applied → refused (immunity, diminishing, a rider, the engine refused the action, a release in progress); stagger / reel /
  get-up lengths asked vs MEASURED (the watch list times the real action); shields dropped → picked up (by whom, after how long);
  knockdowns → time on the ground; ripostes; the cost per tick.
- The player sees it on screen: a short message line when YOU drop your shield / are knocked down, and when you break someone
  (§8 question 12).

---

## 7. The build order (proposal — into TASKS_TODO after Anton's yes)

Each step ships on its own, with tests, the smoke, its summary lines, the master switch first and a PLAYTEST entry. I1-I7 are
working names: they get BUILD ORDER numbers when Anton puts them on the board (step 46 is already queued - the Athletics bar per
soldier under ALT).

| Step | What | Proof in the log |
|---|---|---|
| **I1 SPIKE** (riskiest first) | ONE Advanced switch `ImpactSpike` (off): fixed, loud values to prove each engine lever in a real battle — every landed kick gets the KnockDown flag; every block ×3 stun (cap raised to 2 s); every kicked man's reel and every knockdown's rise at speed 0.5; the first block every 10 s → `act_stagger_backward` on the blocker; every kicked man with a shield drops it (queued); one blow of our own (KnockDown, 1 damage) on a blocker every 20 s. Nothing of the model. A tournament + a 50v50 | `[impact] spike: kick KnockDown flags N → Fall seen M`, `reaction speed 0.5: StrikeKnockBack 0.42 s → 0.85 s measured`, `forced stagger accepted a/N, lasted 0.9 s, blocks from inside 0`, `block stun ×3: defender 0.21 → 0.63 s asked, defend-hit measured 0.6 s; calls off the main thread K`, `shield dropped N, picked up M after s`, `own knockdown blow N → Fall seen M` |
| **I2 The model, SHADOW** (step 48, built 2026-10-17: `ImpactShadowMode` ships OFF - decision 40; nothing is applied anyway until 49-51) | Core `Impact.cs` (pure, unit-tested: x, R, band, S-curve, ceilings, lengths, immunity, diminishing returns, the reeling state), every setting in DESIGN + defaults.json + MCM, the inputs read at every kick / block / hit; decisions LOGGED, nothing applied | `[summary] impact (shadow): kicks 41 → would drop 6, knock down 4; right blocks 380 → would stagger 52 …` by bar band — Anton reads his own battles' numbers before anything changes |
| **I3 Kicks and bashes LIVE** (points 1, 3, 4, 11) - step 50, BUILT 2026-10-18 | knockdown, dropped shield, the longer reel, the slower get-up, immunity + diminishing returns | `[impact] YOU: your kick on … : knocked down (62%)`, the summary's applied vs asked, reel / get-up measured |
| **I4 Blocks LIVE** (points 2, 5, 6, 7) - step 51, BUILT 2026-10-18 | the stun by tiredness, the rolled stagger, wrong-side shield drops, a block knocked down (the technique I1 proved), the riposte | stagger lengths measured, blocks made from inside a stagger ~0, ripostes by the attacker's bar |
| **I5 Clean hits + the reeling state** (points 8, 9) - step 52, BUILT 2026-10-18 (+ MOMENTUM, Anton's step-52 addition) | tired men never shrug off, the flinch lengthened, knockdowns on landed blows, the reeling bonus; the attacker's speed into the blow adds impact (every family) | knockdowns by weapon class and bar band; blows by the attacker's speed band and their outcome rates |
| **I6 Riders, feedback, guide** - step 53, BUILT 2026-10-18 (+ the rider's momentum) | knocked off a horse (the dismount seam; our CanDismount blow for a block), the player's message lines, `docs/HOW-IT-WORKS.md` §11c rewritten, PLAYTEST's morning script + D22 | dismounts decided → applied → seen; the momentum line's RIDERS columns; lines shown / merged |
| **I7 The AI punishes a window** - step 54, BUILT 2026-10-18 | the AI man's aggression share raised to `ImpactPunishAggressionPercent` (step 43's lever) while his target reels or lies down (+ a grace), a timid man within 3 m steps in; both sides, the player's windows too, never the player as attacker | `impact punish proof`: attacks per man-minute on targets in a window vs not, punish ON vs OFF; windows opened / seen / punished / wasted |

**Harmony**: none needed (RESEARCH §R9). What it would buy: setting flags and stun periods on the game's own blow in a
`CreateMeleeBlow` postfix (RBM's way) instead of slowing the reaction a tick later and building our own blow for blocked hits.
If spike I1 shows a lever failing, the fallbacks in order: (1) `SetOverridenStrikeAndDeathAction` (a per-man swap of the
reaction the engine plays) for a reaction that refuses to slow; (2) a crush-through instead of our own blow for a knocked-down
blocker; (3) only then Harmony — a soft dependency in a satellite assembly behind `tools/AssemblyGuard`, Anton's call.

---

## 8. Open questions for Anton (each with Claude's recommendation)

1. **"Also add the damage dealt be able to …"** — your sentence stopped there. Best guesses: (a) the blow's DAMAGE adds pressure
   (a hit that takes 30% of his health adds 0.3 — `ImpactDamageWeight` 1.0); (b) damage TAKEN drains the victim's Athletics (a
   blow knocks the wind out of him — a new rule, its own step); (c) a staggered or downed man takes MORE damage; (d) damage dealt
   to a SHIELD breaks it sooner. **Recommend (a) at 1.0** — vanilla's own knockdown already reads the damage share — and ask
   which you meant.
2. **Stun-lock limits**: 2 s stagger immunity, 5 s knockdown immunity, each outcome halving the next for 8 s. **Recommend yes.**
3. **A dropped shield**: picked up again the vanilla way (the AI only once no enemy is within 20 m — so not mid-melee; you with
   the use key)? **Recommend yes, vanilla** (`ShieldPickupLockSeconds` 0).
4. **Horses**: riders knocked OFF instead of down (half the knockdown chance), no stagger animation on riders, horses untouched?
   **Recommend yes.**
5. **You**: the same rules against you as against the AI (`ImpactOnPlayerPercent` 100)? In first-person view a forced stagger
   animation may look odd — RBM blocks the player's input instead. **Recommend 100, the same animation; spike I1 shows how it
   looks.**
6. **Tournaments**: the same rules (blunt weapons, if a tournament gives them, push harder — `ImpactBluntFactor` 1.3).
   **Recommend the same** — tournaments are where you wanted the turtle broken.
7. **Shield drop on a RIGHT-side block**: 0 (only kicks / bashes and wrong-side blocks drop shields)? **Recommend 0** — a clean
   block is the reward for skill.
8. **The riposte ceiling** 50% for a fresh defender against an empty attacker? **Recommend 50**.
9. **Shadow mode first** (I2 logs what WOULD happen in your battles before anything changes)? **Recommend yes** — the numbers
   above are a model; your logs tune them.
10. **Fever and impact**: does a man in a fever resist staggers ("poise")? **Recommend no** — the fever makes men swing, not
    stand firm.
11. **Kick spam**: kicks become strong against tired men. They cost 3 Athletics and a stored swing already. **Recommend: watch
    the summary; raise `CostPerKickOrBash` only if the AI or you spam.**
12. **Feedback on screen**: a short message when YOU are disarmed / knocked down / break someone? **Recommend yes, one line,
    rate-limited.** (Built in step 53: `ImpactShowMessages` - red to you, purple by you, merged within 1.5 s; your side's heroes behind
    `ImpactMessagesIncludeYourMen`.)
13. **Weapon disarm** (a parry knocking the WEAPON out of a tired hand) — you did not ask. **Recommend not now** (a later step
    if you want it; the same lever as the shield).
14. **The AI using the window** (I7): should the AI attack harder when its target is staggered or down? **Recommend yes, after
    I5, if the playtest shows the AI wasting windows.** (Built in step 54 without waiting for the playtest - Anton: "otherwise it's a
    player-only weapon"; `ImpactPunishEnabled` off = the AI ignores windows, and the summary measures both ways.)

### What a combat designer would also ask (things not in Anton's list)

- **From behind / the flank**: a blow from behind should hit harder (RBM ×1.25). A factor `ImpactFromBehindFactor` (1.25) is
  cheap — add it?
- **Armour**: heavy armour steadies a man (RBM down to ×0.5 knockdown in heavy armour); vanilla's knockdown resistance ignores
  armour. A stability factor by armour weight?
- **Shields by size**: in the model through the shield's weight (a 6 kg tower shield 2.0, a 2 kg buckler 1.15) — enough?
- **Two-handers and polearms parrying**: vanilla gives them extra stability (+0.25 / +0.35 in its stun formula) — mirror it in G?
- **The attacker's run**: a sprinting kicker (step 33) or a running swing hits harder (vanilla's movement-speed bonus) — add
  his speed to the impact? **Answered in step 52 (Anton: "a fresh shock unit attacking a tired line must be devastating"): yes -
  MOMENTUM, I × (1 + `ImpactMomentumPercent` 60% × clamp((v − 2.0) ÷ (7.0 − 2.0), 0, 1)), v = his own speed along the line to the man he
  strikes; every family; never a rider's (step 53).**
- **Missiles**: a javelin into a tired man's shield could drop it; arrows never. Out of scope for v1?
- **Getting up costs breath**: a knocked-down man loses some Athletics getting up? Not proposed (he is already exhausted).
- **Battle scale**: knockdowns in a 1000-man battle are many engine calls — the per-tick cap and the summary's cost line watch it.
- **Heroes' perks**: in a campaign, vanilla scales the ATTACKER's stun on a blocked or parried blow by the Athletics perk
  MightyBlow (SandboxAgentApplyDamageModel.cs 780-783); our multipliers sit on top of the base model's answer, so the perk
  keeps working.
- **Siege and naval**: a man on a ladder or a ship's deck — no forced animation (the spike checks ladders); knockdowns on walls
  are vanilla's.

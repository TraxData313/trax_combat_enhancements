# The mechanics reference

Every table and number behind Trax Combat Enhancements, system by system. This is the reference; for the
same systems in plain words, read the player guide first: [HOW-IT-WORKS.md](HOW-IT-WORKS.md). The short
story is in the [README](../README.md).

Every number here is the **shipped default** (`defaults.json`), and every one of them is a setting you can
change — in the Mod Configuration Menu (MCM) if you have it, or in
`Documents\Mount and Blade II Bannerlord\Configs\TraxCombatEnhancements\config.json`. The setting's
name is in the small print under each table, so you know which slider moves what. If you tune a setting,
the tables that use it change with it.

Want the formulas and every parameter with its range? [DESIGN.md](DESIGN.md) is the full contract.

**Contents**

1. [How to read this](#1-how-to-read-this)
2. [The Athletics bar](#2-the-athletics-bar)
3. [What tiredness does](#3-what-tiredness-does)
4. [The swing stack](#4-the-swing-stack)
5. [Resting and the refill](#5-resting-and-the-refill)
6. [Defending costs too](#6-defending-costs-too)
7. [Tired AI guess their blocks](#7-tired-ai-guess-their-blocks)
8. [Brace by orders](#8-brace-by-orders)
9. [Stepping back out of the press](#9-stepping-back-out-of-the-press)
10. [Battle pace](#10-battle-pace)
11. [Eagerness, and the fever of a charge](#11-eagerness-and-the-fever-of-a-charge)
11b. [Courage: men keep to their group](#11b-courage-men-keep-to-their-group)
11b2. [Cavalry flocking: riders go in as waves](#11b2-cavalry-flocking-riders-go-in-as-waves)
11c. [Staggers, shield drops and knockdowns](#11c-staggers-shield-drops-and-knockdowns)
12. [Damage](#12-damage)
13. [Heroes, and the hideout boss](#13-heroes-and-the-hideout-boss)
14. [The sprint](#14-the-sprint)
14b. [Your orders: a click charges, a drag never does](#14b-your-orders-a-click-charges-a-drag-never-does)
15. [What you see on screen](#15-what-you-see-on-screen)
16. [Where the settings live](#16-where-the-settings-live)

---

## 1. How to read this

- **The bar** means a man's Athletics bar as a share of his **full** bar: 100% fresh, 0% empty. Wounds cap
  how far it can refill (section 2).
- **The peak line** is the white mark at **75%**. At or above it a man is at full strength; every tiredness
  effect slides in a straight line from there down to its worst at an empty bar.
- **"The base game"** means vanilla Bannerlord v1.4.8's own value, before this mod.
- Percentages in the impact tables (section 11c) are **chances** per blow; the rest are amounts.
- Every example assumes the shipped defaults. The small print under each table names the settings it uses.

## 2. The Athletics bar

A fighter's bar is as big as his **Athletics skill** (the one on the character screen), never smaller
than 50 points. One attack — a swing, thrust, shot or throw — costs 10 points, missed or not (7 until step 68, 8 until
step 71: Toni, "they need to tire faster still").

| Athletics skill | Bar (points) | Attacks from full to empty: soldier | hero | hero who leads his party |
|---:|---:|---:|---:|---:|
| 0–50 | 50 | 5 | 7 | 9 |
| 100 | 100 | 10 | 13 | 18 |
| 150 | 150 | 15 | 20 | 27 |
| 200 | 200 | 20 | 27 | 36 |
| 300 | 300 | 30 | 40 | 53 |

(Bar ÷ cost, rounded - a recruit's 50 points are gone at his 5th swing, a legionary's 130 at his 13th.)
Heroes (lords, companions, you) pay a quarter less per attack; a hero who leads a party pays a quarter
less again (10 → 7.5 → 5.6 points). Attacks are cheap because the AI is careful (section 11): it decides
to swing far less often than the base game's AI, so each swing need not cost much for a long fight to wear men down.

**The peak line.** The white mark on the bar sits at **75%**. Above it a man is at full strength;
the first quarter of his bar is "free". Below it the effects in the next section start. A 50-point
recruit spends his free quarter in his second swing; a 150-skill hero who leads his party gets about
seven.

**Wounds cap the bar.** A man at half health can only refill to half his bar.

<sub>Settings: `AthleticsPoolPerSkill` 1.0, `AthleticsPoolFloor` 50, `CostPerBlow` 10 (7 until step 68, 8 until step 71), `CostOnMiss` on,
`HeroCostMultiplier` 0.75, `PartyLeaderCostMultiplier` 0.75, `AthleticsPeakPercent` 75,
`HealthCapsAthletics` on.</sub>

## 3. What tiredness does

Everything below is measured against the peak line: at 75% or more, nothing changes. Under it, every
effect slides in a straight line down to its worst at an empty bar.

| Bar | Pause after a swing ¹ | Swing animation speed | Attacks per minute ² | Run speed | Best damage roll ³ | Chance a tired AI picks his block at random ⁴ |
|---:|---:|---:|---:|---:|---:|---:|
| 100% | none | 100% | 100% | 100% | +50% | 0% |
| 75% (the line) | none | 100% | 100% | 100% | +50% | 0% |
| 60% | 0.15 s | 97% | 84% | 86% | +40% | 20% |
| 50% | 0.29 s | 95% | 73% | 77% | +33% | 33% |
| 30% | 0.74 s | 91% | 52% | 58% | +20% | 60% |
| 15% | 1.42 s | 88% | 36% | 44% | +10% | 80% |
| 0% (empty) | 3.20 s | 85% | 20% | 30% | +0% | 100% |

1. After a typical 0.8 s swing (Anton's battle logs average about that). With the swing stack
   (section 4) it is the wait only when no stored swing is left — and the time each one takes to come
   back. The pause is a wait before the next attack, not slow motion: blocking, moving and turning are never held. Your own pause fills
   the Attack recovery bar; the AI waits with its guard up. A heavier, slower weapon waits longer.
2. Roughly, compared with the same man fresh — the rate the pause aims at. Empty, he attacks once
   where he used to attack five times.
3. Every hit rolls its damage (section 12). A tired man still rolls as low as a fresh one, but his
   lucky high rolls shrink.
4. Only AI men, never you. See section 7 for what that means for his blocks.

Horses are not slowed by their rider's bar by default.

<sub>Settings: `ExhaustedAttackSpeedPercent` 20 (attacks at empty), `AttackAnimationMinPercent` 85,
`MinMoveSpeedMultiplier` 0.3, `MountMinSpeedMultiplier` 1.0 (horses off), `DamageBonusFollowsAthletics` on,
`TiredBlockRandomPercent` 100.</sub>

## 4. The swing stack

The pause after an attack does not come after **every** attack. Each fighter stores swings — one for
every 20 points of Athletics he has **right now**, never fewer than 2. An attack (a swing, a thrust, a
shot, a throw — or a kick or shield bash) spends one. While he has one stored, he attacks with no wait
at all. With none left, he waits the pause of section 3 for the next one.

Swings come back one per pause (the pause of section 3 at his bar **now**) while he is not attacking.
So over a whole fight he attacks just as often as before — but a man who holds back can save swings
up and then strike several in a row. One slow, telegraphed swing every few seconds is easy to read; a
burst of four is not.

Stored swings at most (his points ÷ 20, rounded half up, at least 2):

| Bar size | Full (100%) | 75% | 50% | 25% | Empty |
|---:|---:|---:|---:|---:|---:|
| 50 | 3 | 2 | 2 | 2 | 2 |
| 100 | 5 | 4 | 3 | 2 | 2 |
| 150 | 8 | 6 | 4 | 2 | 2 |
| 200 | 10 | 8 | 5 | 3 | 2 |
| 300 | 15 | 11 | 8 | 4 | 2 |

The count follows his points as they fall: each attack also costs Athletics, so a burst shrinks the
stack's size too. *Example:* a 150-skill hero at half his bar holds 4 — he can strike 4 times back to
back (7.5 points each) — and by then his bar is at 30% and he holds 2 at most, so each next one takes
about 0.75 s to come back.

How long the swings take to come back, after a 0.8 s swing, standing back from the fight:

| Bar | One swing back | A 150 bar's whole stack, from 0 |
|---:|---:|---:|
| 75–100% | at once | at once |
| 60% | 0.15 s | 0.8 s (5 swings) |
| 50% | 0.29 s | 1.2 s (4) |
| 30% | 0.74 s | 1.5 s (2) |
| 15% | 1.42 s | 2.8 s (2) |
| 0% (empty) | 3.20 s | 6.4 s (2) |

(His bar refills meanwhile, so the real wait is a little shorter.) AI soldiers' battle-pace waits
(section 10) come on top of their refill, and an AI swordsman's own breather between swings doesn't
count as rest — so the stack never makes the AI attack more often than before.

**Kicks and shield bashes** spend a stored swing too (they never start a pause of their own). With
none stored, your kick key does nothing until one is back — no more kicking through your pause.

Your **Attack recovery bar** (section 15) shows the stack: **"Swings 4/8"**, and the bar filling toward
the next one.

<sub>Settings: `SwingStackEnabled` on (off = a pause after every attack below the peak line),
`SwingStackPointsPerSwing` 20, `SwingStackMinimum` 2, `KicksUseSwings` on.</sub>

## 5. Resting and the refill

The bar starts to refill **3 seconds** after his last attack, kick, shield bash or wrong-side block
(clean blocks and parries don't stop it — section 6). It refills at a steady rate, a share of the
whole bar per second, so a big bar gains more points per second than a small one.

| What he is doing | Empty → full | Empty → the peak line |
|---|---:|---:|
| Standing or walking (up to 40% of his top speed) | 60 s | 45 s |
| Jogging (70% of top speed) | 80 s | 60 s |
| Running flat out | 120 s | 90 s |

A wounded man refills only to his health share (at 60% health, to 60% of the bar).

<sub>Settings: `RegenDelayBlowTimes` 2 × `BlowTimeSeconds` 1.5 (the 3 s), `FullRegenSecondsStanding` 60,
`RegenMultiplierAtFullRun` 0.5, `WalkEffortFraction` 0.4, `RegenRateNearFullPercent` 100 (a straight line).</sub>

## 6. Defending costs too

Every melee blow a man **blocks** costs him a little. Missiles into a shield are free.

| Block | Cost (soldier) | Hero | Hero party leader | Stops his refill? |
|---|---:|---:|---:|---|
| Shield, on the right side | 3 | 2.25 | 1.69 | No — the refill keeps running |
| Weapon parry | 3 | 2.25 | 1.69 | No — the refill keeps running |
| Shield, held on the **wrong** side | 6 | 4.5 | 3.38 | **Yes** — the 3 s wait starts again |
| A kick or shield bash he makes | 6 | 4.5 | 3.38 | Yes (it is an attack) |

(Step 68, Toni: "tire the men more ... after the first impact" — a right-side block 1 → 2, a wrong-side block 2 → 5. Step 71,
Toni: "they need to tire faster still" — a right-side block and a parry 2 → 3, a wrong-side block and a kick or bash 5 → 6.) So a skilful defender can catch his breath **while** defending. Can a man who is already empty
climb back while blocking a blow every 1.5 seconds (a steady duel), standing still?

| Bar size | Soldier, parrying or a right-side shield block | Hero | Hero party leader |
|---:|---|---|---|
| 50 | sinks (−70 a minute) | sinks (−40) | sinks (−18) |
| 80 | sinks (−40) | sinks slowly (−10) | climbs slowly (+13) |
| 100 | sinks (−20) | climbs slowly (+10) | climbs (+33) |
| 150 | climbs (+30) | climbs (+60) | climbs (+83) |
| 200 | climbs (+80) | climbs (+110) | climbs (+133) |

Points per minute, net (the refill: his bar a minute; 40 blocks a minute × the price). A right-side shield block costs
what a parry does — before step 68 it always climbed; since step 71 (3 a block) a soldier holds even only on a 120-point
bar. Moving fast while fighting refills slower (section 5), and a wrong-side block every 1.5 s holds anyone at the
bottom — by its price (6 points) and because each one stops the refill.

**What counts as the right side (step 64).** A raised shield facing the blow covers it: a shield block whose
attacker stands within **60°** of the blocker's facing (the front third of a circle) is a **right-side** block for
everything in this mod — its cost, the refill, and the staggers and bounces of section 11c — whatever side the
game says the shield was held to. From the side or behind, the game's own verdict. Why: the game decides "right
side" by the guard's DIRECTION against the attack's, and an AI man's guard direction is mostly a dice roll (a
150-skill legionary on realistic guesses 89% of his guards; a shield wall's front rank holds a fixed pose) — in
Anton's looter battle 85% of a FRESH shieldman's blocks read "wrong side". Weapon parries keep the game's direction.
A **tired** AI man still misjudges his share (section 7): that share of his front blocks keeps the game's verdict.

| A fresh shieldman, blows from the front | The game said | Counts now |
|---|---|---|
| right side (7.3% of the blows that reached him in Anton's battle) | right | right |
| wrong side (39.0%) | wrong | **right** |
| a tired AI man's guessed share (section 7's table: 33% at half a bar, all of them when empty) | wrong | wrong |

<sub>Settings: `CostPerShieldBlock` 3 (1 until step 68, 2 until step 71), `CostPerWeaponParry` 3 (2 until step 71), `CostPerWrongSideShieldBlock` 6
(2 from step 43 to step 68, 5 until step 71), `CostPerKickOrBash` 6 (3 until step 66, 5 until step 71), `OnlyWrongSideBlocksStopRefill` on, `ShieldFrontArcIsCorrect` on, `ShieldFrontArcDegrees` 60.</sub>

## 7. Tired AI guess their blocks

In the vanilla game a skilled AI fighter almost always blocks the right way — a tournament champion
late in the tournament essentially never misses. With this mod, below the peak line an AI man's block
direction is partly a **guess**: the emptier he is, the more of his blocks are picked at random. A
random pick is right only 1 time in 4.

For a skilled fighter (one who never guesses in vanilla):

| AI man's bar | Blocks picked at random | With a weapon: parry goes the wrong way (blow lands) |
|---:|---:|---:|
| 75–100% | 0% | 0% |
| 60% | 20% | 15% |
| 50% | 33% | 25% |
| 30% | 60% | 45% |
| 15% | 80% | 60% |
| 0% | 100% | 75% (3 in 4) |

**Shields are more forgiving.** A shield is a big thing: a wrong guess usually still catches the blow,
but on the wrong side — which costs him 6 points and stops his refill (section 6). So a tired shield
man doesn't just take the hit; he stops recovering. Since step 64 a shield block from the front counts as the
right side (section 6) — except this table's guessed share: a tired man's guesses stay wrong-side blocks even
from the front, so a worn-out wall still breaks (section 11c).

Low-skill troops (recruits, levies) already guess a lot in the vanilla game; for them the mod changes
little. It matters most for the elite: veterans, lords and tournament opponents. You are never
affected — your block is your mouse.

<sub>Setting: `TiredBlockRandomPercent` 100 (50 = half as many guesses at every level; 0 = off). It
works through the game's own "randomised defend direction" value, so it is the game's AI doing the
guessing.</sub>

## 8. Brace by orders

Your orders decide how hard your men fight before they stop attacking and only defend. When an AI
man's bar falls to his formation's **floor**, he **braces**: no more attacks, shield out and raised,
guard up. Arrows and throws are still allowed. He braces until his bar climbs back to the floor plus
20 — then he fights again.

| Order | Floor (braces at) | Fights again at | His own floor, with his roll |
|---|---:|---:|---:|
| Charge | 20% | 40% | 10–30% |
| Advance | 40% | 60% | 30–50% |
| Hold, stand, follow, fall back, move… | 60% | 80% | 50–70% |
| No formation (tournaments, arenas, duels) | 20% | 40% | 10–30% |

**Each man is a little different.** Once per battle each man rolls his own floor, ±10 around his
order's, and his own recovery, 20 ± 5. So a line doesn't brace as one: the men give way one by one.

*Example.* A holding spearman rolls +4 on his floor and −3 on his recovery. He braces when his bar
falls to **64%**, and fights again when it reaches 64 + 17 = **81%**. You order the charge: his floor
drops to 24%, and he is back in the fight at once with everything he saved.

A wounded man's band slides down under his wound cap, so he is never stuck bracing forever. The player
never braces — that is your call.

<sub>Settings: `BraceFloorChargePercent` 20, `BraceFloorAdvancePercent` 40, `BraceFloorHoldPercent` 60,
`BraceFloorNoFormationPercent` 20, `BraceFloorSpreadPercent` 10, `BraceRecoverPercent` 20,
`BraceRecoverSpreadPercent` 5, `BraceWieldShield` on, `BraceRaiseShield` on, `BraceEnabled` on.</sub>

## 9. Stepping back out of the press

After a swing, a tired AI soldier may walk **backwards** about 2 m out of the press — facing his
enemy, guard up, for up to 1.5 s — and let a fresher man take his place. The chance follows his bar:

| Bar | 75–100% | 60% | 50% | 30% | 15% | 0% |
|---|---:|---:|---:|---:|---:|---:|
| Steps back after a swing | 0% | 20% | 33% | 60% | 80% | 100% |

Only when an enemy is within 4 m, never on a ship's deck, never in tournaments or arenas, and at most
50 men at once.

<sub>Settings: `StepBackMaxChancePercent` 100, `StepBackDistance` 2, `StepBackSeconds` 1.5,
`StepBackEnemyRange` 4, `StepBackMaxAtOnce` 50, `StepBackEnabled` on.</sub>

## 10. Battle pace

On top of tiredness, AI soldiers simply fight at a steadier pace. Shield infantry are the most patient
— a shield wall should hold, not flail.

| AI fighter | Effect | Fresh man, after a 0.8 s swing |
|---|---|---|
| Foot, with a shield | 30% fewer swings | +0.77 s wait (about 23 swings a minute instead of 33) |
| Foot, no shield | 15% fewer swings | +0.32 s wait |
| Bowman | +2.0 s after each shot | |
| Crossbowman | +2.5 s after each shot | |
| Riders, throwers, slingers, and you | unchanged | |

A tired man's own pause comes on top, and the "fewer" still holds at every bar level.

<sub>Settings: `ShieldInfantrySwingsLessPercent` 30, `FootMeleeSwingsLessPercent` 15, `AiMeleeGapSeconds` 1.0,
`ExtraPauseAfterBowShotSeconds` 2.0, `ExtraPauseAfterCrossbowShotSeconds` 2.5.</sub>

**Shield walls hold their slots.** In the base game a man in a shield wall walks a few steps out of it to
swing at the enemy in front of him, then walks back — every man does it, and the wall bulges and breaks.
With this mod he keeps his place in the formation and fights whatever reaches him, so the wall stays a wall.

| Formation | Holds its slots? |
|---|---|
| Shield wall, square, circle — on hold, move, advance, fall back, follow | yes, both sides |
| Shield wall, square, circle — on a charge or a retreat | no (the charge breaks the wall on purpose) |
| Line, loose, scatter, skein, column | no — the game's own behaviour |
| A man far behind his place (about 20 m) | runs to rejoin, then holds |
| Riders, you, a siege's attackers, naval battles | no |

**A small step out to strike.** Held men still get one short step: when the enemy in front of a man backs off just
out of his reach, he steps out after him (up to 1 m by default), strikes, and is held again at the end of that step and
walks back to his place. He steps again only once he is back. Nobody chases far.

| The enemy in front of his place is… | He… |
|---|---|
| within his reach | fights from his place (and may lean in up to the step) |
| up to 1 m + a strike (2 m) away, in front of his place | steps out after him, at most 1 m, then walks back |
| farther off, beside or behind his place | stays in his place |
| (square, circle) out to the side or the back | steps OUTWARDS on that side — each face of the square fights its own way |

**Thrown out, walked back** (step 63). A held man who ends up beyond his step ahead of his place — thrown forward
by a blow, shoved by the press — is held again at once (looked at ten times a second), and if he has not come back
half a second after he can move again (up from a fall, done reeling), he backs off to his place facing the enemy:

| He stands… | What happens |
|---|---|
| up to 1.25 m ahead of his place (the step + a margin) | nothing more — the step and its end |
| more than 1.25 m ahead, on the ground or reeling | held; he gets up / recovers where he is |
| more than 1.25 m ahead, able to move, for 0.5 s | backs off to within 0.5 m of his place (at most 4 s) |

(With a longer step the marks grow: 2 m → out beyond 2.25 m, back within 1 m.)

**Back to the line after a kick** (step 68). A held man who kicked or shield-bashed — or stepped in at a staggered enemy
(section 11c, "The enemy goes for the opening") — and stands more than **0.5 m** ahead of his place walks straight back as soon
as his kick is over: no half-second wait, back to within 0.25 m.

**The wall pushes as one** (step 68, Toni's 100 v 100: his wall ordered forward — F1 then F4, **advance** — had 15% of its men
in contact more than 1 m ahead of their places; standing, 1%). Under an advance the game sends every man to a place AT the enemy's
line, so the men free to move walk on into the enemy while their mates fight, and the line stretches. Now, within 10 m of the
enemy's front, each man of a held shield wall, square or circle under a moving order is sent only **1 m** ahead of his place in
the line as it stands — every man the same step, so the whole wall walks forward together, pushes at the pace its fighting
front allows, and nobody runs ahead of it:

| The wall's order, near the enemy | Where each man walks |
|---|---|
| Hold / stop | his place (the game's own) |
| **Advance** (yours or the AI's), move, fall back, follow | his place in the line + 1 m toward the game's place for him — the line advances as one |
| Charge | released — the game's own melee (the charge breaks the wall on purpose) |
| Any order, farther than 10 m from the enemy's front | the game's own march |

The step out to strike still works inside the push. A wall whose morale breaks (section 11b1) still breaks.

<sub>Settings: `ShieldWallHoldsSlots` on (live — switch it off mid-battle to see the difference); `ShieldWallStepOutMetres`
1.0 m (0 = they stand fast and fight only what reaches them; 2-3 = a looser wall; live); `ShieldWallPullBackSeconds` 0.5 (Advanced,
"Battle pace (AI)"; 0 = the game's own pull only; live); step 68 (Advanced, "Battle pace (AI)", live): `ShieldWallAdvanceAsOne` on,
`ShieldWallAdvanceStepMetres` 1.0 (0.3-3), `ShieldWallAdvanceNearMetres` 10 (0-60), `ShieldWallKickReturnMetres` 0.5 (0 = off).</sub>

## 11. Eagerness, and the fever of a charge

Men don't want to throw their lives away, and a man at work doesn't hold a pace until he drops. In the
base game AI soldiers trade blows until they drop. With this mod an AI soldier on foot decides to start an
attack **less often than the base game's AI — and less still the more tired he is**: 60% as often with a
full Athletics bar, sliding in a straight line to 20% at an empty one. He holds his place, blocks, parries
and waits for an opening. Tournaments and duels too.

| His Athletics bar | Eagerness (× the base game) | Chance to attack at each decision: on a charge | other orders | shield wall, square, circle |
|---:|---:|---:|---:|---:|
| Base game (any bar) | 100% | 14.4% | 9.6% | 5% |
| 100% (full) | 60% | 8.6% | 5.8% | 3.0% |
| 75% | 50% | 7.2% | 4.8% | 2.5% |
| 50% | 40% | 5.8% | 3.8% | 2.0% |
| 25% | 30% | 4.3% | 2.9% | 1.5% |
| 0% (empty) | 20% | 2.9% | 1.9% | 1.0% |

His counter-strike right after a parry (8% / 6% / 4% in the base game) shrinks the same way. This is on
top of the pause after each attack (section 3): a tired man has **two brakes** — he decides to attack less
often, and waits longer after each attack. A wounded man's bar is capped by his wounds (section 2), so he
is less eager too. Riders keep the base game's (their swings come with each pass), and so do archers'
shots and you.

**The fever.** When a formation gets a **new charge or advance order** — yours or its own general's —
every soldier on foot in it gets a **fever**: he attacks at **80%** of the base game's eagerness, whatever
his bar (a fresh man rises from 60% to 80%, an exhausted one from 20% to 80%). The fever lasts his Athletics
points **right now** ÷ 10, in seconds — so veterans with big bars surge longer, and men already tired from a
long fight surge shorter. Its clock starts only when he first comes within **5 m** of an enemy, so a charge
ordered from afar is not spent on the walk.

| His Athletics now | Fever | With a shield in hand |
|---:|---:|---:|
| 50 points | 5 s | 3.5 s |
| 80 | 8 s | 5.6 s |
| 100 | 10 s | 7 s |
| 150 | 15 s | 10.5 s |
| 200 | 20 s | 14 s |

A man with a **shield in his hand** burns his fever faster (it lasts 70% as long) — he fights from behind
it. It is read live: take the shield off your back, or put it away for a two-handed axe, and the rate
changes from that moment.

**The surge fades** in a straight line from 80% back down to his tired eagerness as the fever burns — and
his tired eagerness itself keeps sinking as he fights:

| Fever left | A man still at a full bar | A man at a half bar |
|---:|---:|---:|
| 100% (and before he reaches the enemy) | 80% (11.5% on a charge) | 80% (11.5%) |
| 75% | 75% | 70% |
| 50% | 70% | 60% |
| 25% | 65% | 50% |
| 0% — back to his bar | 60% (8.6%) | 40% (5.8%) |

A hold, move or retreat order ends the fever at once (the general called them back). A second charge or
advance while it runs does not start it over — unless a fresh fever would be longer than what is left.

**They sprint the last metres.** A man in his fever with an enemy 4–8 m away (3–12 m before step 64) may sprint at him — into contact, until his first attack: every
second he has a 50% chance at the fever's start, sliding down with the fever (half the fever, 25%;
none when it is gone). He spends only the sprint he holds above his order's reserve (section 14).

A bracing man (section 8) still does not attack, fever or not.

**See it: hold ALT.** Each formation's numbers under its marker say how many of its men surge —
**"fever 12/40"** in orange — and the few men you look at (the ones nearest the centre of the screen, close
to you) get a small bar above their heads: **bright orange** and full while he runs at the enemy, shrinking
and turning **yellow** as the fever burns after the clash, gone when it is spent (section 15).

<sub>Settings: `AiAggressionFreshPercent` 60, `AiAggressionExhaustedPercent` 20 (both 100 = the base game's AI),
`FeverEnabled` on, `FeverPointsPerSecond` 10, `FeverAggressionPercent` 80, `FeverContactMetres` 5,
`FeverShieldDurationMultiplier` 0.7, `FeverSprintToClose` on, `FeverSprintMinMetres` 4, `FeverSprintMaxMetres` 8 (step 64; 3 / 12 before),
`FeverSprintCheckSeconds` 1, `FeverSprintChancePercent` 50.</sub>

## 11b. Courage: men keep to their group

Real men stick with their group — not out of drill, out of instinct. In the base game a lone AI soldier
circles your shield wall, or stands among four enemies until he drops. With this mod every AI soldier on
foot keeps asking himself: **how strong are we here, against them?** He looks 6 m around him and weighs
every man — his side (himself too) against the enemy — by how **tough** each one is:

| Man | Armour | Level | Toughness |
|---|---|---|---|
| A looter in rags | ~8 | 5 | 1.35 |
| A recruit | ~20 | 6 | 1.68 |
| A tier-3 infantryman | ~50 | 16 | 3.07 |
| An imperial legionary | ~110 | 26 | 5.97 |
| A lord in full plate | ~150 | 30 | 8.0 |

So it takes about **four or five looters to face one armoured veteran**. A wounded man counts less (half
his health lost = 75%), and an enemy with a **great axe or a polearm and no shield counts half again** —
shock troops break men.

He needs his side to be at least **as strong** as the enemy around him. Less, and he is **timid**. What
he needs moves:

| He is… | Odds he needs |
|---|---|
| fresh and steady | 1.0 (even) |
| half his Athletics bar spent | 1.5 |
| empty | 2.0 |
| morale 25 (his side is falling) | 1.5 |
| hit once in the last 6 s | 1.33 |
| hit three times | 2.0 |
| in his fever (section 11), not hit | **presses whatever the odds** |
| in a full fever, but hit | × 0.4 |
| a hero | 0.75 |

They multiply: a tired (half bar) man who was just hit needs 1.5 × 1.33 = 2.

**A timid man** does one of two things:

- **Far from his mates** (fewer than 4 within 6 m, more than 5 m from his group) he **walks back** to the
  nearest bigger group of his side (6+ men within 20 m), else to his formation's centre — **facing the
  enemy**, backing away, going round them if they stand between. He sprints back when his group is more
  than 12 m off or 3+ enemies are on him, spending what he holds above half his sprint store.
- **Among his mates** he **holds**: he fights an enemy within 2 m (a sword's reach), and otherwise keeps his
  place instead of hunting.

He is brave again once he is back and the odds are 25% better than he needs (after at least 2 s).

**The charge goes in.** A man in the fever of a charge (section 11) who has not been hit **attacks whatever the
odds** — the first clash always happens. Once hit he is shaken, and the fever no longer carries him: he falls
back to the mass. When the fever is spent, the outmatched side lines up opposite the enemy.

**Now and then one steps in.** A timid man who is not shaken, stands among his mates and has an enemy within 8 m
may **surge**: about every half second a 5% chance (times his Athletics bar — a tired man surges less), so a
fresh man about once in 10 s. A surge makes him brave for 4 s: he steps in, swings, and comes back.

**Shock and panic.** A man who was hit wants more mates around him (twice as many) and packs in tighter —
shaken men flock together and are pushed as a mass, they do not scatter. A shocked man whose odds are
below **half** of what he needs **panics**: he sprints back at once and spends **all** his sprint.

**What you will see** (the numbers in DESIGN's worked examples):

| The fight | What happens |
|---|---|
| 40 looters attack your 20-man metal shield wall | They charge in with their fever and clash with the wall; those you hit fall back. When the fever is spent they stand in a thick arc in front of the wall, single men surging in now and then. Looters who split off round a flank (3 of them against 6 of yours: odds 0.15) walk back to the mass. Nobody circles you alone. |
| 200 looters attack the same wall | The front still holds back (odds 0.5), but presses while the charge's fever burns; at the wall's ends 30 looters against 6 of yours (odds 1.1) stay brave and **wrap round it as a crowd**. |
| Two equal armies | Line against line at even odds: they fight. To envelop a line they need about **2 men per defender** — 40 against a 20-man wall wraps it, 30 do not. |
| 4 fresh men with great axes hit the flank of your tired line | Your flank man (bar 30%, hit twice: needs 2.8) faces odds 0.67 — he **panics** and sprints to the nearest bigger group; his mates too: the flank folds back into the mass. Rest your men, or keep fresh troops behind them. |

Never you, never riders, never men holding a shield wall (section 10 holds them), not a siege's
attackers, not tournaments or duels. It works even with the Athletics system off (then tiredness does not
count).

<sub>Settings ("Courage and cohesion (AI)"): `CourageEnabled` on, `CourageRadiusMetres` 6, `CourageBaseOddsPercent` 100,
`CourageArmourWeightPercent` 200, `CourageLevelWeightPercent` 100, `CourageHealthWeightPercent` 50, `CourageHeavyWeaponPercent` 150,
`CourageTiredWeightPercent` 100, `CourageMoraleWeightPercent` 100, `CourageShockSeconds` 6, `CourageShockWeightPercent` 100,
`CourageFeverPercent` 40, `CourageHeroPercent` 75, `CourageHysteresisPercent` 25, `CourageMinTimidSeconds` 2,
`CourageSurgeChancePercent` 5, `CourageSurgeSeconds` 4,
`CourageRegroupRadiusMetres` 5, `CourageRegroupMates` 4, `CourageFlockRadiusMetres` 20, `CourageFlockMinMen` 6, `CourageSprintBack` on,
`CourageSprintMetres` 12, `CourageSprintEnemies` 3, `CourageSprintKeepPercent` 50, `CouragePanicOddsPercent` 50,
`CouragePanicSprintKeepPercent` 0, `CourageTightenWeights` on, `CourageTimidMeleeMetres` 2.</sub>

## 11b1. The fear flock: frightened men become a herd

Courage (above) is a man looking at the odds around him. **Fear** is slower and goes deeper: as a soldier's
**morale** falls (the game's own - his side losing men, his officers falling) and his **Athletics** runs out, he
stops caring about his place in the formation and wants to be **among his own**, ideally **behind a comrade**.
How much he feels depends on his **discipline** - his troop level:

| Man | Level | Discipline | Feels of the fear |
|---|---|---|---|
| A looter, a recruit | 5-6 | 0 | all of it |
| A tier-3 man | 16 | 0.35 | 65% |
| An imperial legionary, a hero | 26+ | 0.70 | 30% |

**Fear** = 1 − (1 − morale fear)(1 − tiredness fear), × (1 − discipline):

- **morale fear**: 0 at morale 60 or more, a straight line to 1 at 15;
- **tiredness fear**: 0 at 75% of his bar or more, a straight line to 1 at an empty bar;
- **nobody behind him** (a single rank, a wide spread, an enemy within 8 m): + 25% × (1 − discipline);
- half of it is **his unit's average** - the mass moves together.

Below **15%** fear he keeps his slot exactly (a fresh unit obeys exactly). From 15% to **80%** the **pull** grows
from 0 to 1: his place slides from his slot toward the unit's **shelter** - its centre of mass, pushed back from
the enemy near it (4 m × the unit's pull), and that shelter **creeps back** at most **6 m a minute** (1.5 m a minute
under a hold or in a shield wall). Uncovered, he leans toward the spot **behind the nearest comrade**; a
**fresh** comrade in front (60% of his bar or more) **anchors** him - he holds that spot for **20 s** - and when
that comrade tires, the pull takes him again. Far from his place (8 m+) or **alone** among enemies he **sprints**
back (half his sprint store). Under a **charge** there are no slots: a frightened unit becomes a loose mass around
its shelter, tighter the more afraid it is.

**The worked example** - his own fear, and his pull toward the shelter (the unit feeling as he does, someone behind him):

| | morale 80 | morale 50 | morale 20 |
|---|---|---|---|
| **Looter, fresh** | fear 0.00 - pull 0.00 | 0.22 - **0.11** | 0.89 - **1.00** |
| **Looter, half a bar** | 0.33 - **0.28** | 0.48 - **0.51** | 0.93 - **1.00** |
| **Looter, spent** | 1.00 - **1.00** | 1.00 - **1.00** | 1.00 - **1.00** |
| **Legionary, fresh** | 0.00 - 0.00 | 0.07 - 0.00 | 0.27 - **0.18** |
| **Legionary, half a bar** | 0.10 - 0.00 | 0.14 - 0.00 | 0.28 - **0.20** |
| **Legionary, spent** | 0.30 - **0.23** | 0.30 - **0.23** | 0.30 - **0.23** |

So a looter band that has lost its nerve or its breath becomes a **herd** - a blob drawn in on itself, edging back
from you, easy to wrap round - while legionaries keep their lines until they are spent and broken, and even then
only drift a quarter of the way. A fresh looter line facing an enemy with nobody behind it already feels 0.25: the
ends of a thin line draw in toward its middle.

**Shield walls hold** while their men's average morale is **30 or more**; below it the wall breaks and its
frightened men join the flock. A man walking back by courage (alone among enemies) goes first; a man in the fever of
a charge, or surging in, is left alone.

Never you, never riders, not a siege's attackers, not tournaments or duels, not a retreat order. Works with the
Athletics system off (then tiredness adds no fear) and with courage off.

<sub>Settings ("Courage and cohesion (AI)"): `FearFlockEnabled` on, `FearMoraleCalm` 60, `FearMoraleBroken` 15, `FearFreshPercent` 75,
`FearMoraleWeightPercent` 100, `FearTiredWeightPercent` 100, `DisciplineLevelLow` 6, `DisciplineLevelHigh` 26, `DisciplineMaxPercent` 70,
`FearUnitSharePercent` 50, `FearThinLinePercent` 25, `FearStartPercent` 15, `FearFullPercent` 80, `FearShelterBackMetres` 4,
`FearShelterMaxMetres` 12, `FearDriftMetresPerMinute` 6, `FearDriftHoldPercent` 25, `FearCoverWidthMetres` 0.9, `FearAnchorFreshPercent` 60,
`FearAnchorSeconds` 20, `FearSprintMetres` 8, `FearSprintChancePercent` 10, `FearSprintKeepPercent` 50, `FearWallBreakMorale` 30,
`FearChargeLooseMetres` 12.</sub>

### Winners don't hang back (step 69)

Fear used to come from morale and tiredness alone - never from **who is winning**. A tired green man whose side was
clearly beating the enemy around him still kept back behind his unit's centre, and on a charge he was held near the
mass while his mates finished the fight. Now **winning calms**:

- Each look, his side's strength near him is weighed against the enemy's - the same **local odds** courage counts
  (toughness-weighted, within 6 m). **Below even odds he is losing: nothing changes** - a losing unit flocks and backs
  up exactly as before.
- From even odds (1 : 1) his fear fades in a straight line and is **gone at two to one**. An enemy near him whose
  morale is broken (routing men included) calms him up to 50% more, but only when he is not losing.
- His unit's picture counts too: a man with no enemy near takes his unit's win; a winning unit relaxes together.
- **No enemy within 12 m = no pull** at all: he follows his order. His morale and bars still show.
- **The mop-up:** when a charging unit's men are winning on average by half or more (the enemy near it few or broken),
  nobody is held near the mass any more - they go in for the kill.

A tired man (30% of his bar) at morale 50, his unit feeling and winning as he does - fear / pull:

| His side's local odds | 0.5 (losing) | 1 (even) | 1.5 | 2 | 3 |
|---|---|---|---|---|---|
| **Green looter** (no discipline) | 0.69 - **0.83** | 0.69 - **0.83** | 0.34 - **0.30** | 0.00 - 0.00 | 0.00 - 0.00 |
| **Veteran legionary** (discipline 0.7) | 0.21 - **0.09** | 0.21 - **0.09** | 0.10 - 0.00 | 0.00 - 0.00 | 0.00 - 0.00 |

The battle summary gains two lines: **"fear flock and winning"** (fear before → after by odds band - the losing
band must read unchanged) and **"idle men"** - AI men of a charge or advance standing still for more than 5 s with an
enemy within 25 m but none within 3 m, counted by who holds them: the fear flock, courage, the brace, the shield
wall, a bow / crossbow / javelin (normal - skirmishers stand off to throw) or the game itself.

<sub>Settings ("Courage and cohesion (AI)"): `FearWinCalms` on, `FearWinEvenOddsPercent` 100, `FearWinClearOddsPercent` 200,
`FearWinShakenPercent` 50, `FearThreatMetres` 12, `FearMopUpWinPercent` 50.</sub>

### A wide band, not a column (step 70)

The frightened used to draw toward **one point** behind their unit's centre - every man slid toward it sideways as
much as backwards - so a looter unit facing your shield wall became a deep, narrow column, ~15 men deep. Now the
point is a **band** laid along the enemy's front:

- **Its direction** is the enemy's front line (the enemy formation's own facing when it faces them).
- **Its width** is the frontage they face - your line - or their men in ranks of 1 m files, whichever is wider, at
  most 40 m.
- **Its depth** grows with fear: **4 ranks** a little afraid, **5** at a middle pull, **6** terrified (1 m a rank).
- **Its front** stands 1.5 m short of your front, pushed back up to 4 m more by fear, creeping back no faster than
  the drift above.
- **Each man keeps his place along the front** and only closes up in depth. In a file deeper than the band's ranks,
  the men behind them **slide out toward the band's ends** - the mass widens instead of deepening. Nobody steps in
  behind a comrade where the file is already full.
- **Front men still fight:** a frightened man in the front rank with an enemy within 2.5 m steps in and swings; if
  he ends up more than 2 m ahead of his place he is walked back to the mass.

| Fear pull | 0 - 0.24 | 0.25 - 0.74 | 0.75 - 1 |
|---|---|---|---|
| **Ranks deep** | 4 | 5 | 6 |

Worked example (the battle that asked for it): 40 green looters, pull 0.61, in a column 3 wide and 14 deep before a
20 m shield line. The old point shelter squeezed them into **one file of 40**; the band gives **9 files of about 4-5,
the deepest 5** - a wide, shallow mass, 9 m across. 120 terrified men before the same line fill it 6 deep; 300 fill
the 40 m cap and the rest stand where their depth puts them.

The summary's **"fear flock band"** line gives, in contact, men per occupied 1 m file (the depth in ranks) and the
deepest file, the width - mean and p90, ON vs OFF - and how many files are deeper than the band's ranks.

<sub>Settings ("Courage and cohesion (AI)"): `FearBandEnabled` on, `FearBandRanksMin` 4, `FearBandRanksMax` 6, `FearBandFileMetres` 1,
`FearBandRankMetres` 1, `FearBandMaxWidthMetres` 40, `FearBandGapMetres` 1.5, `FearStrikeReachMetres` 2.5, `FearStrikeLeashMetres` 2.</sub>

### No lone runner behind your line (step 70)

A **lone runner** is an AI man on foot (never you, never riders) with **fewer than 2 mates** near him and an enemy
within 8 m, who stands **more than 4 m ahead of his own unit's front** or **past the enemy's front**. Men charge in -
but they do not break up like that:

- **The fever of a charge no longer sends a man in alone.** With mates around him it still presses against bad odds
  (the first clash happens); alone, courage decides him.
- **A charge's or a fever's sprint ends** the moment he is alone or past the enemy's front - the mass keeps its
  sprint and its punch.
- **A lone runner walks back to his group** (courage's own walk back, round the end of the enemy's line if it stands
  between, on one side), whatever the odds - unless he is chasing a **beaten** enemy (his side two to one around him
  and the enemy near him broken).

The summary's **"lone runners"** line counts them - man-seconds and men - by what put them there: a sprint into
contact, a punish step-in, a courage surge, the fever's press, chasing a target behind the enemy's front (and whom:
you, a hero, another man) or the game itself, with the fixes ON and OFF.

<sub>Settings ("Courage and cohesion (AI)"): `LoneRunnerEnabled` on, `LoneRunnerAheadMetres` 4.</sub>

## 11b2. Cavalry flocking: riders go in as waves

In the base game a charging rider picks a target, rides through, turns and goes again — every rider on his
own, so a tired cavalry unit ends up trickling into the enemy **one by one**. With this mod:

**Fresh riders dance.** A rider whose **eagerness** — his Athletics bar — is **75% or more** charges as in the
game, again and again.

**Tired riders look for their own kind.** Below 75% he rides back to the riders of **his own kind** in his
formation — horse archers with horse archers, skirmishers (javelins) with skirmishers, lancers and swordsmen
with each other — and waits for a group before he attacks again. **The more tired, the bigger the group:**

| His eagerness | The group he wants (his kind, him included) |
|---|---|
| 75% or more | none — he charges alone (the game's dance) |
| about 57% | 3 |
| 40% | 4 |
| 20% | about 9 (of a 20-rider kind) |
| empty | 65% of his kind |

A flock goes in when it has the size its riders' **average** eagerness wants **and** its most rested rider —
the **leader** — has at least half the eagerness of a free rider. Then **all of it goes at once**: a wave, the
game's own charge for 10 s; after that each rider decides again — the eager ones dance on, the tired ones
flock again. Fresh: small waves, often. Tired: big waves, rarely — the time between charges stretches by itself.

**The flock never sits.** It keeps riding, each rider in his own place about 3.5 m from the next: while its
riders are tired it drifts toward the **centre of your unit**; as they rest it bends toward the **enemy** the
formation is charging — the rested riders pull it there, and when it is big enough it flips into the charge.

**A rider alone among many enemies** (courage's odds, section 11b) counts at half his eagerness, so even a
fresh rider looks for his kind when he finds himself alone. A wounded rider counts by his bar like anyone —
a flock never waits for health.

Only riders whose formation is on a **charge** (F1 + F3, or the AI's charge). On hold, move, follow or advance
your riders keep their places in the formation anyway. Never you. Not tournaments or naval battles.

**Watch for it:** a charge of your cavalry into a big infantry fight. The first pass is the game's; after a few
passes the riders wheel off in twos and threes, ride together, and come back in groups — and late in the battle
the whole squadron gathers and goes in as one. Flip "Cavalry flocking" off in MCM mid-battle to see the
game's trickle again.

<sub>Settings ("Cavalry flocking (AI)"): `CavalryFlockEnabled` on, `CavalryFlockFreeEagerPercent` 75, `CavalryFlockHysteresisPercent` 5,
`CavalryFlockMidEagerPercent` 40, `CavalryFlockMidGroupSize` 4, `CavalryFlockExhaustedSharePercent` 65, `CavalryFlockTimid` on,
`CavalryFlockTimidRadiusMetres` 15, `CavalryFlockGatherRadiusMetres` 25, `CavalryFlockSeparationMetres` 3.5, `CavalryFlockLeadSeconds` 2,
`CavalryFlockTightenWeights` on, `CavalryFlockWaveSeconds` 10, `CavalryFlockMaxSeconds` 45.</sub>

## 11c. Staggers, shield drops and knockdowns

In the base game a fresh man and an exhausted one take a blow the same way: a kick only pushes him back, a
blocked swing bounces off, and he is guarding again before you can swing twice. With this mod a blow can
**break** a man — **stagger** him (a real window for the next blow), knock his **shield** out of his hand,
**knock him down** (he gets up slower), or knock a **rider off his horse**. On a fresh man of your size
nothing changes.

### The idea: impact against stability

Every kick, shield bash, block and clean hit is weighed once: the **impact** of the blow against the
**stability** of the man who takes it.

- **How tired he is** decides most of it (except on a block in the right direction — see the ceilings). In the green (his bar at or above the peak line, 75%) a blow does
  nothing more than in the base game; the emptier he gets, the more it does — an empty man is at the top of
  every table below.
- **How you two compare** comes next: both men's **current** Athletics (how big the bar is AND how full — a
  200-point man at full against a 50-point man at 20% is a giant against a dwarf), what struck him (a heavy,
  fast or blunt weapon hits harder, a kick hard, a shield bash with its shield's weight), what he guarded with
  (a big shield, a parrying weapon, nothing), and both men's skill with it. A mismatch inside a factor of about
  1.4 counts as an even match — so two men of a kind in the green are **always** safe.
- **Armour and rank** (step 62): both men's **toughness** — the courage system's measure of armour, level and the
  health left — weighs in too: a legionary hits a looter as if twice as strong (below).
- **Where the blow comes from** (step 63): a blow from behind presses a man like half an empty bar on top of his own
  tiredness, from the side a fifth of one (below) — **a blow from behind can floor even a fresh man**.
- **A man already reeling** from one of these staggers is far easier to drop (below).
- **A man running into his blow** hits harder — the charge (below); a charge into a good shield block **bounces**.

### What each blow can do (the ceilings)

The most each blow can do — reached on an exhausted man, or on a fresh man by a giant:

| The blow | Staggered | Shield knocked away | Knocked down | Always, the more tired he is |
|---|---:|---:|---:|---|
| A **kick** or **shield bash** that lands | (his reel IS the stagger) | 90% | 80% | he reels up to ×2.5 longer |
| A blow he blocks on the **right side** with his shield | 30% — only on a big mismatch | — | 10% | — |
| A blow he **parries** with his weapon | 30% — only on a big mismatch | — | 10% | — |
| A blow he blocks on the **WRONG side** | 90% | 50% | 40% | — |
| A **clean hit** (it lands) | 80% (his flinch, longer) | — | 50% | he never shrugs it off |
| **The riposte**: a right-side block or parry staggers the ATTACKER | 70% | — | — | the attacker reels up to 1 s |

**A correct block is the attacker's risk, a wrong one the defender's.** On a right-side block or a parry the
blocker's own tiredness counts only a quarter — he is broken mainly by a **big mismatch** (a peasant's stick
against a berserker's two-handed axe), and the man who swung into a good guard **reels** the more tired and
outmatched he is, however tired the blocker. On the WRONG side the blocker's tiredness counts in full. So the
direction of your block is what pays — swinging as much as you can into a good guard is a mistake.

A knocked-down man always gets up slower (up to ×2 when empty); a knockdown replaces the stagger, and a
knockdown and a lost shield can come together (the exhausted turtle broken).

### Tiredness: two even men

Two men of a kind (Athletics 100, skill 100, swords, 4 kg shields), the attacker **fresh**, the man who takes
the blow at the bar shown:

| His bar | Kick: shield / down | Right-side block: staggered / down | Parry: staggered / down | WRONG side: staggered / shield / down | Clean hit: staggered / down |
|---|---:|---:|---:|---:|---:|
| 100% – 75% (green) | 0% / 0% | 0% / 0% | 0% / 0% | 0% / 0% / 0% | 0% / 0% |
| 50% | 14% / 1% | 1% / 0% | 2% / 0% | 39% / 12% / 3% | 21% / 0% |
| 25% | 82% / 70% | 2% / 0% | 10% / 0% | 88% / 48% / 38% | 68% / 35% |
| 10% | 90% / 80% | 6% / 0% | 19% / 3% | 90% / 50% / 40% | 80% / 50% |
| 0% (empty) | 90% / 80% | 8% / 0% | 21% / 4% | 90% / 50% / 40% | 80% / 50% |

(A fresh man's blow on an exhausted guard: a good guard holds — the WRONG side breaks.)

**Two fresh men**: nothing, ever — whatever they do to each other. **Two men equally tired** (both at the bar
shown) — exhaustion never cancels out:

| Both at | Kick: shield / down | Right-side block: staggered / down — **the attacker reels** | Parry: staggered / down — **the attacker reels** | WRONG side: staggered / shield / down | Clean hit: staggered / down |
|---|---:|---:|---:|---:|---:|
| 75% and above | 0% / 0% | 0% / 0% — 1% | 0% / 0% — 0% | 0% / 0% / 0% | 0% / 0% |
| 50% | 7% / 0% | 0% / 0% — 28% | 1% / 0% — 18% | 23% / 4% / 0% | 21% / 0% |
| 25% | 56% / 38% | 0% / 0% — 62% | 2% / 0% — 52% | 67% / 31% / 19% | 59% / 24% |
| 0% (both empty) | 90% / 80% | 1% / 0% — 70% | 5% / 0% — 70% | 90% / 50% / 40% | 80% / 50% |

At the end of a long fight every blow tells — and **every missed blow counts**: a swing into a good guard
leaves the attacker reeling (an opening for the blocker), a wrong-side block leaves the blocker broken.

**The riposte, by the attacker's bar** (two men of a kind, a right-side block with a 4 kg shield):

| The attacker's bar | Into a fresh guard | Into a guard as tired as he is | Into an empty guard |
|---|---:|---:|---:|
| 75% and above | 1–4% | 1% | 0% |
| 50% | 41% | 28% | 18% |
| 25% | 70% | 62% | 52% |
| 0% (empty) | 70% | 70% | 70% |

A fresh attacker never reels; a tired one flailing into a good guard opens himself up, whoever holds it.

### The mismatch: big against small

(The tables of this part weigh Athletics, weapons, guards and skill - both men of the same armour and rank;
step 62's **toughness** adds to them: the next part.)

**The king and the man.** A king with 200 Athletics, full (skill 250), against a man with 50, down to 20%
(skill 60), both with 3.5 kg shields:

| What | Staggered | Shield | Knocked down |
|---|---:|---:|---:|
| The king kicks the man | (reels ×2.1) | 90% | 80% |
| The king swings, the man blocks on the right side | 17% (a giant against a dwarf) | — | 2% |
| … the man blocks on the WRONG side | 90% | 50% | 40% |
| The king's blow lands on the man | 80% | — | 50% |
| The man kicks the FRESH king | (a normal push) | 0% | 0% |
| The man swings, the fresh king blocks | 0% — and the king **ripostes** 70% | — | 0% |
| The man kicks the king at 30% | (reels ×1.9) | 9% | 0% |
| The man kicks the king at 0% | — | 90% | 80% — an empty king is as weak as anyone |

**You and a tournament king.** You (Athletics 100, skill 100) against a king with 220 (skill 250):

| What | Shield | Knocked down |
|---|---:|---:|
| You kick the king, fresh | 0% | 0% — no kick-win against a fresh king |
| You kick the king at 60% | 0% | 0% |
| You kick the king at 30% | 45% | 26% |
| You kick the king at 10% | 90% | 80% — tire him first, then break him |
| The king kicks you, fresh | 0% | 0% |
| The king kicks you at 40% | 76% | 62% |
| The king swings, you block on the right side at 40% | — | 0% (staggered 2%) |
| The king swings, you block on the WRONG side at 40% | 46% | 35% (staggered 85%) |

**The stick and the axe.** A peasant (50 Athletics, skill 20) parrying a veteran berserker's two-handed axe
(180 Athletics, skill 200) with a 0.6 kg stick — the mismatch counts even on a good block:

| What | Staggered | Knocked down |
|---|---:|---:|
| The axe on the FRESH peasant's parry | 17% | 2% |
| The axe on his parry at half a bar | 26% | 7% |
| The axe on his parry, empty | 30% | 10% (the ceilings) |
| The axe lands clean on the fresh peasant | 18% | 0% |
| The peasant's stick on the fresh berserker's shield | 0% | 0% (the berserker ripostes 30%) |

**The riposte** — a good guard turns a blow round and staggers the attacker: a fresh veteran blocking a
recruit at 40% — 70%; the same recruit fresh — 28%; a fresh man parrying an equal at 20% — 66%; two fresh even
men — about 1%. The blocker's own tiredness does not save the attacker (the veteran at half a bar: still 70%);
the more tired the attacker, the longer he reels (0.5 s → 1 s).

### Armour and rank: toughness

Since step 62 both men's **toughness** — the same measure courage uses (section 11b): (1 + 2 × armour ÷ 100) ×
(1 + level ÷ 30, the level counted up to 60) × (1 − half the health he has lost) — weighs in every blow: the
mismatch × (the striker's toughness ÷ the struck man's) to the power ½. Strongly, not absolutely: a legionary
against a looter (4.4 times as tough) counts **×2.1** — about as much as a quarter of a bar of tiredness — and the
looter's blows on him **×0.48**. Two men of the same toughness: nothing changes.

| Troop | armour | level | toughness | a legionary's blow on him | his blow on a legionary |
|---|---:|---:|---:|---:|---:|
| a looter | 8 | 5 | 1.35 | ×2.10 | ×0.48 |
| a peasant archer | 15 | 11 | 1.78 | ×1.83 | ×0.55 |
| you (a level-20 hero, armour 80) | 80 | 20 | 4.33 | ×1.17 | ×0.85 |
| an imperial legionary | 110 | 26 | 5.97 | ×1.00 | ×1.00 |
| a lord in plate | 200 | 30 | 10.00 | ×0.77 | ×1.29 |

A man who still holds his **bow, crossbow or sling** when the blow lands takes it ×1.3 (both hands busy, no
guard). A shieldless man's weaker guard shows on its own: he cannot block, so every blow that reaches him is a
clean hit.

### Where the blow comes from

Since step 63 a blow that lands from **behind** (the attacker more than 120° off where the man faces) adds **0.5**
to the pressure — as if he had half an empty bar on top of his own tiredness —, from the **side** (60–120°) **0.2**;
kicks, bashes and clean hits; a blocked blow met the guard (and a blow from behind is never blocked). (Step 62
multiplied the strength comparison instead — ×2 behind, ×1.4 the side — which between even men did almost nothing:
Anton's battle read 13% down from behind vs 11% from the front.) **A blow from behind can floor even a fresh man.**
Two even men (pool 100, skill 100, a 1.2 kg sword), the chance he is knocked **down** (staggered when not):

| The man's bar | front | side | behind |
|---|---:|---:|---:|
| 100% / 75% (fresh) | 0% (0%) | 0% (8%) | **7% (40%)** |
| 50% | 0% (21%) | 10% (44%) | **42% (74%)** |
| 25% | 35% (68%) | 49% (80%) | 50% (80%) |
| empty | 50% (80%) | 50% (80%) | 50% (80%) — the clean hit's ceilings |

A fresh legionary's sword (1.3 kg, skill 130, Athletics 130) on a **peasant archer** with his bow in his hands
(Athletics 40):

| The archer's bar | front | side | behind | charging, front | charging, behind |
|---|---:|---:|---:|---:|---:|
| 100% (fresh) | 1% (29%) | 17% (52%) | **47%** (78%) | 14% (49%) | **50%** (80%) |
| 60% | 27% (62%) | 47% (78%) | 50% (80%) | 44% (76%) | 50% (80%) |
| 40% and below | 50% (80%) | 50% (80%) | 50% (80%) | 50% (80%) | 50% (80%) — the clean hit's ceilings |

So **a unit that reaches the back of an engaged line throws its men to the ground** — standing or charging — and
archers caught with their bows out go first. Two fresh legionaries, from behind: down 7%, staggered 40% (step 62:
never down). The pressure is no multiplier: the ×8 cap of the step-62 factors never sees it, and nothing passes the
ceilings.

### A man already reeling is easy prey

While a man reels from any stagger of this system — a kick's long reel, a block's stagger, a riposte, a clean
hit's stagger — the next blow on him counts as if he were three quarters more tired. Two even men:

| He reels and is… | A kick: shield / down | A sword blow lands: down | A mace blow lands: down |
|---|---:|---:|---:|
| fresh | 69% / 54% | 34% | 35% |
| at half a bar | 90% / 80% | 50% | 50% |

(The same blows on the same calm, fresh man: nothing.) He cannot be staggered *again* while he reels — but he
can be knocked down.

### The charge: momentum

**A man running into his blow hits harder.** Up to a walk (2 m/s) nothing changes; at a full sprint (7 m/s and
faster) the blow's impact is **×1.6**, in between a straight line — kicks, shield bashes and weapon blows alike,
blocked or not. Only his speed TOWARD the man he strikes counts. Since step 62 a **charge** counts as a full
sprint whatever speed was measured at the blow: a man sprinting (section 14), or the **first** blow he strikes
within **3 s** after his sprint ended (1.5 s before step 64), or the blow of an attack he **began** while sprinting or
within those 3 s, however long the swing (step 64; men slow down to swing — Anton's battle of 2026-10-19 had no blow at
all in the sprinting band before). A **running** man who is not charging counts by his speed: a 4.5 m/s blow is half
a sprint, in the momentum and in the bounce off a shield alike. A charging man's blow also rolls the
**full damage range** however tired he is (section 12). **The shock charge** — a fresh attacker into a
line at 40% of its bar:

| The attacker | Staggered | Knocked down |
|---|---:|---:|
| a sword, standing | 37% | 5% |
| a sword, running (4.5 m/s) | 48% | 13% |
| a sword, sprinting | 57% | 21% |
| a two-handed axe, standing | 51% | 15% |
| a two-handed axe, running | 61% | 26% |
| a two-handed axe, sprinting | 68% | 34% |
| a kick, standing | (shield 40%) | 22% |
| a kick, sprinting | (shield 68%) | 52% |

So **hold a fresh shock unit back, place it on the flank or behind a tired, engaged line, and charge at the
right moment**: the tired men are thrown around — staggered, knocked down, their shields knocked away — and
every man who reels is easier to drop for the next blow. A charge into a **fresh** line is something else:
momentum alone hardly moves a fresh man (a sprinting swordsman on a fresh equal: nothing; a sprinting
two-handed axe: 4% staggered, never down).

**A charge into a good shield wall bounces** (step 62). On a **correct** block (right side, or a parry) the
charger's own momentum turns against him: his blow counts without its momentum, and his speed multiplies the
riposte (×2 at a full sprint) — the shieldman's toughness against his counts too. On a **right-side shield
block** a charging man can be **THROWN BACK** — knocked to the ground (up to 40%, more the faster he came, the
more tired and the weaker he is against the shieldman); a walking man only reels, a parry never throws anyone.
Into a FRESH legionary's right-side block (a 4 kg shield):

| The attacker | walking in: reels | charging: THROWN BACK | charging: reels (when not thrown) |
|---|---:|---:|---:|
| a looter, fresh | 34% | 33% | 39% |
| a looter at 60% | 62% | 40% | 42% |
| a looter at 30% | 70% | 40% | 42% |
| you, fresh (a level-20 hero) | 2% | 12% | 18% |
| you at 50% | 44% | 37% | 41% |
| you at 20% | 70% | 40% | 42% |
| a legionary, fresh (an even man) | 1% | 10% | 15% |
| a legionary at 50% | 40% | 36% | 40% |

The legionary himself never moves (staggered 0%). So looters rushing a fresh shield wall go down one in three and
most of the rest reel — and a player who sprints head-on into a high-tier shieldman can land on his back ("You
are thrown back by …'s shield", red). Charge the flank, the back, or a tired line. (Smoke, 200 looters seeded:
charging - thrown back 30%, reel 43%; walking - 0% / 30%.)

**The wall's blocks count, and the looters hit it at speed (step 64).** Anton's looter battles of 2026-10-19 showed
none of this happening: 85% of a fresh legionary's blocks were "wrong side" by the game (section 6) — and a wrong-side
block never bounces anyone — and only 14 of 160 blows were charges (the looters spent their small sprint far out and
stopped 3 m short). Now a block from the front is a right-side block, the charge sprint runs into contact (section 14)
and the charge carries to the blow. Of the looters' blows that reach a fresh legionary wall's shieldmen (46% of them
blocked, as in that battle; the rest land):

| A looter's blow into the wall | Before step 64 | Now |
|---|---:|---:|
| his FIRST blow (charging): he reels or is thrown back | 2.7% (all blows) | **33%** |
| … of it THROWN BACK (on the ground) | under 1% | **15%** |
| a later blow (walking): he reels | 2.7% (all blows) | **16%** |
| the legionary staggered or down (fresh) | 0% | 0% |

A tired wall is another matter: its men's guessed blocks stay wrong-side (section 7), and a wrong-side block from a
looter on an EMPTY legionary floors him 40% / staggers him 90% (a right-side one: 0% / 4%). Two fresh even men and a
walking blow into a right block are unchanged (riposte 1%, never thrown; charging: thrown back 10%).

**The cap.** Momentum × toughness × direction × a bow in hand together count at most ×8 (and at least ⅛): a
sprinting legionary into a peasant archer's back reaches ×7.6 — under it; a lord charging a looter archer's back
×12.5 is held at 8.

**The cavalry charge.** A rider's blow carries his **horse's** speed — half a sprinting man's bonus (×1.3 at a
full gallop of 10 m/s, nothing at a walk of 3 m/s or slower), so a cavalry charge into a tired line throws men
down without felling the whole line. A fresh rider's sword (1.2 kg) into a line at 40%:

| The rider | Staggered | Knocked down |
|---|---:|---:|
| standing | 37% | 5% |
| at a trot (5 m/s) | 41% | 7% |
| at a canter (6.5 m/s) | 43% | 9% |
| at a full gallop | 48% | 13% |
| at a full gallop, a two-handed axe | 61% | 26% |
| at a full gallop into a line at 25% | 75% | 44% (standing: 68% / 35%) |
| at a full gallop into an empty line | 80% | 50% (the ceilings - never more) |
| at a full gallop into a FRESH line | 0% | 0% |

A **couched lance** gets nothing from this: its hit is already the game's own big one. The horse's own charge
(running a man over) is the game's too.

### Riders

A rider is **never staggered** (the horse shares his every move) — a "knockdown" on a rider knocks him **off
his horse** instead, at **half** the knockdown's chance; his **shield** can be knocked from his hand as a man on
foot's. A fresh attacker on foot against a rider at the bar shown:

| The rider's bar | A kick lands: off / shield | Right-side block: off | WRONG side: off / shield | A clean hit: off |
|---|---:|---:|---:|---:|
| 100% – 75% | 0% / 0% | 0% | 0% / 0% | 0% |
| 50% | 1% / 14% | 0% | 1% / 12% | 0% |
| 25% | 35% / 82% | 0% | 19% / 48% | 17% |
| 0% (empty) | 40% / 90% | 0% | 20% / 50% | 25% |

A blocking rider's guard is like anyone's. Horses themselves are the game's own
(they rear as before).

### How long

The more tired he is, the longer — and the more **random** (± up to 30% when empty: a fresh man's timing is
predictable, an exhausted man's is not):

| His bar | A stagger lasts | A kicked man reels | He gets up | A riposted attacker reels |
|---|---|---|---|---|
| 75% and above | 0.6 s | as normal | as normal | 0.5 s |
| about 56% | 0.8 s (0.8–0.9) | ×1.4 | ×1.25 slower | 0.6 s |
| about 38% | 1.05 s (0.9–1.2) | ×1.75 | ×1.5 slower | 0.75 s |
| about 19% | 1.3 s (1.0–1.6) | ×2.1 | ×1.75 slower | 0.9 s |
| 0% (empty) | 1.5 s (1.05–1.95) | ×2.5 | ×2 slower | 1 s |

A blocker's guard after a block is the game's own (a longer one changed nothing you could see in a playtest —
`BlockStunTiredExtraPercent`, 0); the stagger is the window. Since step 62 a riposted attacker reels by his
tiredness **or** by how outmatched he is, whichever is more (a fresh looter into a legionary's shield reels the
whole second).

### Every decided fall plays

Before step 62 a knockdown decided on a light blow was often lost: the game decides first whether a man **shrugs
a blow off** (no reaction at all), and only then whether he falls — Anton's battle of 2026-10-19: 35 knockdowns
decided, 7 falls seen, 14 on shrugged-off blows. Now the blow is weighed at that first question, and a decided
knockdown or stagger refuses the shrug-off; a decided fall that still has not started a moment later (0.2 s since
step 63, 0.5 before) is played by a knockdown of our own. A blow that kills is never weighed (he dies, he does not
fall). Since step 63 a **friend's** blow — a rear-rank man's swing into his own front man — is never weighed
either: the game cancels its damage and plays nothing, and in Anton's battle of 11:02 those were 126 of the 198
decided falls, every one played only by our knockdown (which threw the front man forward, out of his wall). Every
fall decided on an enemy's blow was played by the game at the blow itself.

### Nobody is stun-locked to death

- After a stagger ends, **no new stagger for 2 seconds** (he can still block).
- After he gets up, **no new knockdown for 5 seconds**; a man on the ground is not knocked down again.
- Every stagger, knockdown or lost shield **halves** the next chance against the same man for **8 seconds**.
- A kick still costs you Athletics and a stored swing (sections 4 and 6), so spamming it empties you.
- **A dropped shield** lies on the ground and can be picked up again the normal way — you with the use key,
  the AI only once no enemy is within 20 m (so not in the middle of a melee).

### The enemy goes for the opening

A man who **reels or lies on the ground** is an opening — for you, and for the AI. An AI soldier on foot whose enemy
reels or is down (yours included: **if you stagger, the men on you press it**) decides to attack as eagerly as the
game's own AI for as long as it lasts **+ 0.5 s**, whatever his tiredness had made of him (section 11); a **timid**
man (section 11b) within **3 m** steps in for it, like a surge. How much more eager, in a charge (the game's attack
decision 0.144):

| He is | Normally decides to attack | Facing an opening |
|---|---:|---:|
| fresh (full bar) | 0.086 (60%) | 0.144 (100%) — ×1.7 |
| in a charge's fever (peak) | 0.115 (80%) | 0.144 — ×1.25 |
| half tired | 0.058 (40%) | 0.144 — ×2.5 |
| exhausted | 0.029 (20%) | 0.144 — ×5 |

He still decides himself — the opening makes him eager, not certain. Riders keep the game's own timing.

### The AI kicks and bashes (steps 65-66)

The game's AI never kicks; now it does. An AI soldier on foot with the enemy he fights **within 1.6 m and in front of him** (40°)
**kicks** him — or, with a shield in his hand, **shield-bashes** him (a shieldman facing a **raised shield** kicks instead: a raised shield
stops a bash, not a kick). Step 66 (Anton's looter battle of 2026-10-19: not one kick in 49 man-minutes of contact): men fight at a
weapon's length, so a man who decides to kick while that enemy is **within 5 m** but not yet in reach **waits up to 3 s** and kicks the
moment he is — the rates below are **per minute in contact**.

**A man without a shield kicks** at `AiKicksPerMinute` 1.2 a minute × the factors, at least **8 s** apart (the table: in steady contact,
the cooldown in — Core `Step66ExamplesTests`):

| He is | × | Fresh | Half his bar (50%) | vs a full **turtle** (× 3) | In the **fever** (× 2) | Turtle + fever |
|---|---:|---:|---:|---:|---:|---:|
| a looter (Athletics 30) | 0.65 | **0.71** | 0.28 | 1.78 | 1.29 | 2.88 |
| an average man (Athletics 100) | 1.0 | **1.03** | | | | |
| a legionary (Athletics 130) | 1.15 | **1.17** | 0.48 | 2.67 | 2.02 | 3.94 |
| … below 35% of his bar | × 0 | never | | | | |

(A turtle = 2 blocks in the last ~4 s, his guard up now counting one; the bar factor is a straight line from 35% to 75%. Step 65 asked
0.1 a minute and 10 s apart: a fresh looter 0.064 — one kick in 16 minutes of contact.)

**A shieldman bashes as often as he swings** (Anton: "maybe make them as frequent as swings when the man has a shield"): each of his
melee swings earns a bash (`AiBashShareOfSwingsPercent` 100; at most 2 saved), and he spends it about **1.5 s** later at a full bar —
longer as he tires (÷ the bar factor), never below 35% — at least **2 s** after his last bash. A shieldman never kicks at the per-minute
rate; his kicks are the bashes he turns on a raised shield. What a shieldman in steady contact does, a minute (a seeded walk through the
real rules; his swings without bashes from the 14:16 battle's fever proof, each swing 1.3 s of his time, each bash 1.2 s):

| His bar | Swings without bashes | Swings | Bashes | Bashes a swing |
|---|---:|---:|---:|---:|
| 100-75% | 8.1 | 7.1 | 6.7 | **0.95** |
| 75-50% | 16.5 | 13.8 | 8.3 | 0.60 |
| 50-25% | 11.4 | 10.8 | 2.2 | 0.20 |
| 25-0% | 3.7 | 3.6 | 0 | 0 |
| a fresh wall man (swings 30% less, step 21) | 6.0 | 5.5 | 5.3 | 0.96 |

The bashes' own time takes about one swing in eight from a fresh man; his stored swings (step 32) are never the brake at the top of the
bar (a fresh man's refill ~1 s; the stack ~100% full there). What brakes a tired shieldman is his price: **each kick or bash costs
`CostPerKickOrBash` 6** × his multipliers (4.5 a hero, 3.38 a hero party leader) and a stored swing (sections 4, 6), like yours — a
fresh shieldman bashing 7 times a minute spends ~42 points a minute on it on top of his swings, and below 75% he bashes less, below 35%
never. Never you (your E key is yours), never a rider, never a man staggered, down, routing, with a bow in hand, or without the
Athletics or a stored swing for it. `AiBashShareOfSwingsPercent` 50 = one bash in two swings (0.50 a swing fresh, 7.5 swings and 3.7 bashes a minute).

**Never through a friend; every landed kick decided** (step 68). In Toni's 100 v 100 the AI's kicks and bashes "landed on the
man" 92 times but only 33 were decided: the rest hit one of the kicker's OWN side — a rear-rank man kicking at the enemy past
his own front-rank mate. The game cancels a friend's kick (nothing plays), and the mod never decides it. Now an AI man does not
kick or bash while a man of his side stands between him and his enemy (within **0.5 m** of the line between them) — he waits
for a clear line, up to the same 3 s as for reach. Every kick or bash that lands on an enemy — the AI's and yours — gets its one
decision (the brace, the knockdown, the lost shield); a kick into a **raised shield** is stopped by the shield and decides nothing
(the shield did its job).

<sub>Settings: `AiKickFriendClearMetres` 0.5 (Advanced, impact group; 0 = no check; live).</sub>

### The kicked man braces (step 65)

A landed kick or bash no longer always knocks a man back. Before anything else the man **braces** — and a fresh, heavy, high-tier, skilled
man mostly takes it standing. The brace fails along the same **pressure x** as everything in this section (his tiredness, both men's
Athletics, armour and rank, skill, a bash's shield, a blow from behind): never below x −0.7, always from x 1.0, an S-curve between. A man
below **20%** of his bar never braces.

- **Braced**: no knock-back (the game's is refused), no knockdown, no lost shield; whatever twitch is left plays at **5×** speed.
- **The brace fails, he is fresh**: a **short reel** — **40%** of the game's knock-back ("a fast bit"); still never a knockdown or a lost
  shield on a fresh man.
- **The brace fails, he is tired**: the reel grows with his tiredness — at an empty bar the full step-50 behaviour (the long reel, the
  falls, the lost shields).

**The worked example** (Core `Step65ExamplesTests`, 20 000 seeded kicks a row; a looter = pool 50, Athletics 30, toughness 1.35; a
legionary = pool 130, Athletics 130, toughness 5.97; even men = pool 100, Athletics 100, toughness 3.0):

| Kicker → kicked man | x | Brace fails | Braced | Short reel | Full reel | Knocked down | Shield lost |
|---|---:|---:|---:|---:|---:|---:|---:|
| fresh looter → **fresh legionary** | −0.32 | 12.9% | **87%** | **13%** (×0.40) | 0 | **0** | **0** |
| looter at 60% → fresh legionary | −0.41 | 7.8% | 92% | 8% | 0 | 0 | 0 |
| fresh looter → legionary at half his bar | +0.14 | 49% | 51% | 49% (×0.90) | 0 | 0 | 0 |
| fresh looter → legionary at 30% | +0.50 | 79% | 21% | 0 | 70% | 9% | 23% |
| fresh looter → legionary at 10% (no brace) | +0.87 | — | 0 | 0 | 29% | 71% | 84% |
| two fresh even men | 0.00 | 37% | 63% | 37% (×0.40) | 0 | 0 | 0 |
| fresh → even man at half his bar | +0.40 | 71% | 29% | 70% (×0.90) | 0 | 1% | 10% |
| fresh legionary → fresh looter | +0.45 | 75% | 25% | 71% | 0 | 4% | — |
| fresh legionary → an EMPTY man (no brace) | +1.55 | — | 0 | 0 | 21% | **79%** | 90% |

Two fresh legionaries, one bashing (a 4 kg shield): x +0.03, the brace fails 40%. The same for you — the brace guards you against an AI's
kick (× `ImpactOnPlayerPercent`), and an AI man braces against yours: a short line says so (**red** "You brace against X's kick" / "X's kick
staggers you for a moment"; **purple** "X braces against your kick" / "A lucky kick - X staggers for a moment").

<sub>Settings (Advanced, "Impact: staggers, shield drops, knockdowns"): `AiKicksEnabled` on, `AiKicksPerMinute` 1.2 (0.1 until step 66),
`AiKickReachMetres` 1.6, `AiKickFacingDegrees` 40, `AiKickCooldownSeconds` 8 (10 until step 66), `AiKickTurtleMultiplier` 3, `AiKickTurtleSeconds` 4, `AiKickTurtleBlocks` 2,
`AiKickFeverMultiplier` 2, `AiKickSkillLowPercent` 50, `AiKickSkillHighPercent` 200, `AiKickMinBarPercent` 35, `AiKickFullBarPercent` 75,
`AiKickKicksARaisedShield` on; `KickBraceEnabled` on, `KickBraceFromPressure` −0.7, `KickBraceToPressure` 1.0, `KickBraceMinBarPercent` 20,
`KickBraceReelPercent` 20, `KickShortReelPercent` 40, `KickBraceRefusesKnockBack` on; step 66: `AiKickWaitSeconds` 3, `AiKickWaitMetres` 5,
`AiBashShareOfSwingsPercent` 100, `AiBashDelaySeconds` 1.5, `AiBashCooldownSeconds` 2, `CostPerKickOrBash` 6 (5 until step 71).</sub>

### You

The same rules apply to you — every chance against you × `ImpactOnPlayerPercent` (100%). A short line in the
game's message feed tells you when it matters: **red** when you are knocked down, knocked off your horse, lose
your shield or reel from a riposte; **purple** when you knock a man down or off his horse, break his guard or
knock his shield away. At most one line every 1.5 s — what happens meanwhile is merged into the next ("You
knock Aldric off his horse; you knock Rolf down"). Want your side's big moments too? `ImpactMessagesIncludeYourMen`
adds a **white** line when one of your men knocks an enemy **hero** down or off his horse, or a hero of yours goes
down. Every outcome you give or take is also in the log (`[impact] YOU: ...`).

### With the rest of the mod

- **Courage** (section 11b): every stagger, knockdown or lost shield is a hit that **shakes** a man — shaken men
  want more mates around them and pack in; a tired line that is being thrown around folds back into the mass.
- **The fever** (section 11): a charge's fever makes men swing, it does not make them stand firm — a feverish
  man is staggered like anyone. A charge (fever) + sprint (section 14) + momentum is the shock unit's whole blow.
- **Tired blocks** (section 7): tired AI guess their block direction — more wrong-side blocks, so more broken guards.

<sub>Settings ("Impact: staggers, shield drops, knockdowns"): `ImpactEnabled` on, `ImpactKicksEnabled` / `ImpactBlocksEnabled` /
`ImpactHitsEnabled` / `ImpactRiposteEnabled` on, `ImpactShadowMode` off (on = decide and log only); the ceilings `KickShieldDropMaxPercent` 90,
`KickKnockdownMaxPercent` 80, `RightBlockStaggerMaxPercent` 30, `RightBlockShieldDropMaxPercent` 0, `RightBlockKnockdownMaxPercent` 10,
`ParryStaggerMaxPercent` 30, `ParryKnockdownMaxPercent` 10, `WrongBlockStaggerMaxPercent` 90, `WrongBlockShieldDropMaxPercent` 50,
`WrongBlockKnockdownMaxPercent` 40, `HitStaggerMaxPercent` 80, `HitKnockdownMaxPercent` 50, `RiposteStaggerMaxPercent` 70; a correct block
`CorrectBlockTiredWeightPercent` 25, `RiposteBlockerTiredPercent` 0; the lengths
`StaggerBaseSeconds` 0.6, `StaggerTiredExtraPercent` 150, `StaggerMaxSeconds` 2, `KickReelTiredExtraPercent` 150, `GetUpMaxSlowdown` 2,
`BlockStunTiredExtraPercent` 0, `RiposteStunMaxSeconds` 1, `ImpactRandomPercent` 30, `ImpactStunCapSeconds` 2; the limits
`ImpactStaggerImmunitySeconds` 2, `ImpactKnockdownImmunitySeconds` 5, `ImpactDiminishWindowSeconds` 8, `ImpactDiminishFactor` 0.5,
`ShieldPickupLockSeconds` 0; who `ImpactOnPlayerPercent` 100, `ImpactRiderDismountPercent` 50, `ImpactTiredNeverShrugsOff` on; momentum
`ImpactMomentumEnabled` on, `ImpactMomentumPercent` 60, `ImpactMomentumWalkMetresPerSecond` 2, `ImpactMomentumSprintMetresPerSecond` 7,
`ImpactMomentumRiderPercent` 30, `ImpactMomentumRiderWalkMetresPerSecond` 3, `ImpactMomentumRiderGallopMetresPerSecond` 10; the lines
`ImpactShowMessages` on, `ImpactMessagesIncludeYourMen` off, `ImpactMessageMinSeconds` 1.5; the opening `ImpactPunishEnabled` on,
`ImpactPunishAggressionPercent` 100, `ImpactPunishGraceSeconds` 0.5, `ImpactPunishRangeMetres` 3; step 62 `ImpactShrugOffYieldsToImpact` on,
`ImpactFallBackupSeconds` 0.2 (step 63; 0.5 before), `ImpactToughnessWeight` 0.5 (with `CourageArmourWeightPercent` 200, `CourageLevelWeightPercent` 100,
`CourageHealthWeightPercent` 50), `ImpactRangedInHandPercent` 130, `ImpactRearPercent` 100 and `ImpactFlankPercent` 100 (step 63; 200 / 140
before), `ImpactRearDegrees` 120, `ImpactFlankDegrees` 60, step 63 `ImpactRearPressure` 0.5 and `ImpactFlankPressure` 0.2, `ImpactChargeCarrySeconds` 3 (step 64; 1.5 before), `RiposteKnockdownMaxPercent` 40, `RiposteKnockdownFrom` 0, `RiposteMomentumPercent` 100,
`ImpactMultiplierCapPercent` 800 (and `SprintFullCritical` on, section 12); the pressure `ImpactMismatchWeight` 0.25,
`ImpactEvenBandDoublings` 0.5, `ImpactStaggeredBonus` 0.75 and the weapon / guard / skill factors (DESIGN's table).</sub>

## 12. Damage

Two things, in this order:

1. **The roll.** Every landed hit is multiplied by a random 50%–150%, so no two blows are the same.
   A tired attacker loses the top of that range (section 3).
2. **The reduction.** Then every hit — on people, horses and shields — lands **25% lighter**, so
   fights last longer. Falls and objects are never touched.

What an average 100-damage hit actually lands:

| Attacker | Lowest | Highest | Average |
|---|---:|---:|---:|
| Fresh (at or above the peak line) | 38 | 113 | 75 |
| Half way to empty (bar 38%) | 38 | 94 | 66 |
| Empty | 38 | 75 | 56 |
| Empty, but **charging** (step 62) | 38 | 113 | 75 |

A **charging** man — sprinting into his melee blow, or striking the first blow within 3 s after a sprint, or the blow
of an attack begun in the charge (section 11c) — rolls the whole range however tired he is: the sprint's critical is never capped. Hits on a shield are not
rolled by default (still reduced). A hit never drops below 1.

<sub>Settings: `DamageRandomPercent` 50, `DamageReductionPercent` 25, `DamageRandomOnShields` off,
`DamageRandomMelee` / `Ranged` / `OnMounts` on, `SprintFullCritical` on.</sub>

## 13. Heroes, and the hideout boss

- **Heroes pay less** — a quarter less per attack, kick, bash and block; a party leader a quarter less
  again (section 2). With a hero's big Athletics skill, his bar is big too: late in a battle your heroes
  are often the freshest men on the field.
- **The hideout boss fight is a fresh start.** When the "Win the Duel" / "Win the Fight" objective
  appears, your side refills to its wound cap and every pause and step back ends. In a duel your men
  stand aside, so only you refill. The boss comes fresh — so do you.

<sub>Settings: `HeroCostMultiplier` 0.75, `PartyLeaderCostMultiplier` 0.75, `HideoutBossFightRefill` on.</sub>

## 14. The sprint

Hold **Left Ctrl** to sprint: a short burst of speed, on foot, in a fight or walking about a town or village (so street fights have it too). Think of it as a dodge — out of a
bad spot, onto a fleeing archer, round a shield wall's flank. The AI uses it too: when a formation gets a
**new** charge or advance order, its men sprint off together, so charges hit harder and sooner.

**How much sprint you have** is your Athletics points divided by 20, in seconds — but **never less than
2 seconds**. A tired man has a shorter sprint; even an empty bar still holds a 2 s dodge (it just refills
slowly).

| Your Athletics bar | Pool 50 | Pool 100 | Pool 150 |
|---|---:|---:|---:|
| 100% (full) | 2.5 s | 5 s | 7.5 s |
| 75% (the peak line) | 2 s (floor) | 3.75 s | 5.6 s |
| 50% | 2 s (floor) | 2.5 s | 3.75 s |
| 25% | 2 s (floor) | 2 s (floor) | 2 s (floor) |
| 10% | 2 s (floor) | 2 s (floor) | 2 s (floor) |
| 0% (empty) | 2 s (floor) | 2 s (floor) | 2 s (floor) |

A sprint starts only with at least 20% of the store filled, and runs at full speed for its first 30%.

**How fast.** A sprint starts at **2 ×** the speed you run when *fresh* — whatever your Athletics, a tired
man sprints as fast as a fresh one at the start. It holds that for the first 30% of its length, then eases off
in a straight line to **1.1 ×** your *current* run speed at the end — so a fresh man ends a little faster than
his run, an exhausted one ends back at his tired jog. While sprinting you also **reach top speed in 1 second**
(the game's own acceleration on foot takes about 2.3–2.8 s), so even a short tap gets you going.

| Your Athletics bar | Your run now (× a fresh run) | Sprint peak (× a fresh run) | Sprint end (× a fresh run) |
|---|---:|---:|---:|
| 100% / 75% (the peak line) | × 1.00 | × 2.00 | × 1.10 |
| 50% | × 0.77 | × 2.00 | × 0.84 |
| 25% | × 0.53 | × 2.00 | × 0.59 |
| 10% | × 0.39 | × 2.00 | × 0.43 |
| 0% (empty) | × 0.30 | × 2.00 | × 0.33 |

A sprint is as long as what you had stored when it began, and even a part-full store starts at full speed.
The clock only runs while you actually move; let go of the key and the rest stays stored.

| Into the sprint | Fresh: a full 7.5 s sprint | Empty bar: the 2 s floor |
|---|---:|---:|
| the start | × 2.00 | × 2.00 |
| 0.6 s | × 2.00 | × 2.00 (end of full speed) |
| 1 s | × 2.00 | × 1.52 |
| 1.5 s | × 2.00 | × 0.93 |
| 2 s | × 2.00 | × 0.33 (the end) |
| 2.25 s | × 2.00 (end of full speed) | — |
| 3 s | × 1.87 | — |
| 4.5 s | × 1.61 | — |
| 6 s | × 1.36 | — |
| 7.5 s | × 1.10 (the end) | — |

(× a fresh run speed.) A fresh soldier running ~3.9 m/s covers about 49 m in a full 7.5 s sprint instead of
29 m.

**The refill.** While you are not sprinting the store fills back up — **7 seconds** from empty to full
standing at full strength. Moving faster than a walk halves the rate, and so does being tired (in a straight
line from the peak line down to an empty bar). The two multiply. The times are for the whole store, whatever
its size — an exhausted man's 2 s floor takes 14 s standing.

| Empty → full store | Standing or walking | Running |
|---|---:|---:|
| 100% / 75% (at or above the peak line) | 7 s | 14 s |
| 50% | 8.4 s | 16.8 s |
| 25% | 10.5 s | 21 s |
| 10% | 12.4 s | 24.7 s |
| 0% (empty) | 14 s | 28 s |

**The catch.** While you sprint your **Athletics bar does not refill**. It costs nothing else — but a sprint
right after a fight keeps you tired longer. Never on a horse.

**The AI** sprints once per *new* order — at the moment you (or its own general) give it. Each kind of order
has one number: the **reserve** a man keeps, as a share of his **full** store. He sprints everything he holds
above it, and nothing once he is at or below it:

| Order | Keeps | From 100% | From 90% | From 60% |
|---|---|---|---|---|
| **Charge** (F1 F3, or the AI's charge) | 0% | all of it (100%) | all of it (90%) | all of it (60%) |
| **Advance** (F1 F4 "engage", or the AI's advance) | 50% | 50% | 40% | 10% |
| **Move** (F1 F1, move to a position) | 80% | 20% | 10% | none |
| Hold, retreat, fall back, follow | — | none | none | none |
| **He routs** (his morale breaks — the game's own rout, not a retreat order) | 0% | all of it, once | all of it | all of it |

(A man with a 5 s store: a move from full sprints 1 s, an advance 2.5 s, a charge 5 s.) **A man who breaks and
routs** spends his whole store for the first get-away, once, at the moment he breaks — and does not stop when an
enemy is close (he is running away). The rout is the game's; only the speed is ours. So a broken line scatters
fast — and the men who spent their sprint charging have little left to flee with.

An advance or move sprint stops early when an enemy is within 3 m or the order changes again. A **charge** sprint
runs **into contact** (step 64): it ends with his first attack — that blow lands as a charge (section 11c) — or when he
is stopped (half a second standing still in the press). And a man whose charge sprint would be **shorter than 3 s**
(a looter's 2 s floor) does not spend it at the order from far away: he **waits** until an enemy is within **10 m**, then
sprints — a 2 s sprint covers about 12-15 m, so it lasts to the line. A fresh legionary's 6.5 s still goes at the order.
Give a formation a new charge order and watch it surge — tired men too, off their 2 s floor. In their fever (section
11) men may also sprint again at an enemy **4-8 m** off (3-12 m before step 64), with what they hold above the reserve —
into contact as well.

**Tip — shock troops.** Move your shock infantry round behind the enemy with plain **move** orders: the first
move costs them a little sprint and none of them touches the last 80%. When you give the real **charge**, they
still have most of their store and hit the enemy's back at a sprint. Men advanced into position keep half.

<sub>Settings: `SprintEnabled` on, `SprintKey` LeftControl (a key name: LeftControl, RightControl, LeftAlt,
RightAlt, LeftShift, RightShift, CapsLock, Tab, Q, T, Y, H, J, K, L, B, N, M, C, MiddleMouseButton,
X1MouseButton, X2MouseButton), `SprintPointsPerSecond` 20, `SprintMinSeconds` 2, `SprintMinStartPercent` 20,
`SprintMaxMultiplier` 2.0, `SprintMinMultiplier` 1.1, `SprintFullSpeedSharePercent` 30,
`SprintTopSpeedReachSeconds` 1, `SprintRefillSeconds` 7, `SprintRefillMovingFactor` 0.5,
`SprintRefillExhaustedFactor` 0.5, `SprintAiEnabled` on, `SprintAiChargeKeepPercent` 0,
`SprintAiAdvanceKeepPercent` 50, `SprintAiMoveKeepPercent` 80, `SprintOnRout` on, step 64 `SprintChargeIntoContact` on,
`SprintChargeWaitBelowSeconds` 3, `SprintChargeWaitMetres` 10. The run-now column comes from tiredness: `MinMoveSpeedMultiplier` 0.3 at an empty
bar, `AthleticsPeakPercent` 75.</sub>

## 14b. Your orders: a click charges, a drag never does

In the orders menu the game charges an enemy unit whenever you let go of the left button while that unit is "in your sights" - its
banner within about 70 px of the centre of the screen, or the unit within 10 m of the camera - even if you were dragging out a
line for your own men. Step 72 narrows that:

| Your click | What your men do |
|---|---|
| Press ON an enemy banner icon (within 30 px), let go there, no drag | **Charge that unit** (the game's own targeted charge) |
| Drag (the cursor 12 px or more from where you pressed - a camera turn counts) | **Move** to the dragged line, its frontage kept - never a charge, even released on a banner |
| Click near a banner, over the men, or press off the icon and let go on it | **Move** to the point |
| F keys (F1, F3 …) - no click | The game's own: an F-key charge still goes for the unit you look at |

Nothing is re-ordered: while your click is not a deliberate one the game's unit targeting is switched off, so the game's own move
order is the only one that goes out - no charge starts, so no sprint and no fever is spent. The pixels are those of a 1080-high screen,
scaled to yours. The gamepad and naval battles keep the game's own rule.

<sub>Settings (Advanced page, *Orders: no accidental charges*): `OrderNoAccidentalCharge` (on), `OrderChargeDragPixels` 12,
`OrderChargeIconPixels` 30.</sub>

## 14c. Line discipline: green troops cannot hold a thin line

Toni's battle (2026-10-20): his 60 tier-5 men against 120 tier-1 recruits - the recruits came on a front about two men deep, wider than his whole
army, and wrapped round him. In the game an AI line on the advance forms round(√(men ÷ 16)) × 16 men wide - 48 for 120 men, 2.5 deep. Since
step 73 every foot formation - infantry, archers and skirmishers - in a line, shield wall, loose or scatter arrangement stretches no thinner
than its men can hold. Each man's tier gives his minimum ranks:

| Tier | Typical men | Minimum ranks |
|---|---|---|
| 0 | peasants, looters, villagers | 6 |
| 1 | recruits | 5 |
| 2 | | 4 |
| 3 | trained men | 3 |
| 4 | | 2 |
| 5 | veterans | 1 |
| 6 (and any higher tier a mod adds) | elites | 1 |
| a hero | lords, companions, you | as tier 6 |

The formation's minimum depth **D** is the AVERAGE over its men, and its line is at most **ceil(men ÷ D)** men wide (rounded up - never
narrower than the game's own √men), at its own spacing. Rounding up gives a partly filled last rank by itself:

| The unit | D | At most | Its shape |
|---|---|---|---|
| 120 recruits (T1) | 5.0 | 24 men wide | 5 full ranks (the game's advance wanted 48 wide, 2.5 deep) |
| the same after 20 fall | 5.0 | 20 men wide | 5 full ranks - narrowed again at once |
| 60 veterans (T5) | 1.0 | 60 men wide | never narrowed |
| 100 men, half T2 and half T3 | 3.5 | 29 men wide | 3 full ranks + 13 (the 4th rank 45 % full) |
| 70 T1 + 30 T3 + 20 T5 | 3.83 | 32 men wide | 3 full ranks + 24 |
| 60 looters (T0) | 6.0 | 10 men wide | 6 full ranks |

- **The AI**: whenever the game or its AI makes a line wider - its wide and wider form orders, an envelop, the line re-forming as men fall -
  it is narrowed back in the same frame, before the men move to their new places; losses or a change of D shrink it back the same way. D is
  recounted every second and at once when men fall, join or leave.
- **You** (on by default): your own formations keep to it too. Drag a line wider than your men can hold in the orders menu and the preview
  STOPS GROWING at the limit; the order keeps to it, and a line on screen says so: *"your Infantry: these men hold no thinner than 5 deep -
  24 men wide at most"*.
- **Left alone**: cavalry and horse archers, squares, circles, columns and wedges; naval battles; sieges and sally-outs (unless switched on -
  the walls and streets set the width there).
- **With the rest of the mod**: the fear flock (§11b1) and its band move frightened men from the places the narrowed line gives them - a
  deeper block simply has more ranks to compress; the shield wall (§10) holds the narrowed slots.

<sub>Settings: *Line discipline: your units too* (player page, How the AI fights) = `LineDisciplineForPlayer` (on); Advanced page, *Line
discipline by tier*: `LineDisciplineEnabled` (on), `LineDisciplineRanksT0`-`LineDisciplineRanksT6` 6 / 5 / 4 / 3 / 2 / 1 / 1,
`LineDisciplineHeroTier` 6, `LineDisciplineRangedToo` (on), `LineDisciplineInSieges` (off), `LineDisciplinePreviewCap` (on),
`LineDisciplineShowMessage` (on), `LineDisciplinePollSeconds` 1.</sub>

## 15. What you see on screen

**Your Athletics bar** sits under your health, with the white peak line at 75%. Its colour:

| Colour | Your bar |
|---|---|
| Green | at or above the peak line (75%) |
| Blue | just under it |
| Yellow | 56% or less |
| Orange | 38% or less |
| Red | 19% or less, and empty |

It also shows outside battles (walking about a town) while a weapon is drawn or the bar is refilling.

**The Attack recovery bar** sits just above it and shows your swing stack (section 4): **"Swings 4/8"**
— stored swings out of how many you can hold now — and the bar filling toward the next one. At 0 the
seconds until the next are written inside it. Press attack (or kick) with none stored and it flashes —
the press is swallowed; hold the button and the attack fires the moment a swing is back.

**The sprint bar** sits above that: **"Sprint 6.2 s"** — the seconds of sprint you have stored — and a bar
with a white mark at the 20% a sprint needs to start. Grey below the mark, blue when you can sprint,
yellow while you sprint.

**The orders menu** shows, under each formation card: the men's average Athletics with its ± spread,
their average health, and **"ready 34/50"** — how many are ready to fight (not bracing).

**Hold ALT** for the same numbers under the game's own formation markers, yours **and the enemy's** —
the whole group at a glance:

```
        (the game's marker: 40, the icon, 85 m)
              72% ± 8   HP 81%        the men's average Athletics ± spread (its colour = their strength), health
              [██████▒▒░░|░░]         the same as a bar, the white tick at the peak line
              ready 34/40             how many are ready to fight (not bracing)
              fever 12/40             how many surge in a fever (orange) - only while someone does
              morale 64, fresh 41%    the men's average morale and Athletics (blue) - the 4 nearest formations within 60 m
```

| "ready" colour | When |
|---|---|
| Plain | more than 75% of the men ready |
| Yellow | 75% or fewer ready |
| Red | 50% or fewer ready — this line is spent |

**Athletics bars over the men you look at** (hold ALT, or the orders menu open): the **five men nearest the
centre of the screen**, within **15 m** of the camera, get a small bar above their heads — yours, your
allies' or the enemy's (not yours: you have your own) — his own Athletics bar in miniature: its length is
the Athletics he has left, its colour the bar's colour (green at full strength, blue just below the peak
line, then yellow, orange, red), the part his wounds hold dark red. Turn the camera onto the man you want
to read; the group's averages are on the markers. **In a tournament, an arena fight or a duel** (no
formation markers there) hold ALT the same way: your opponent's bars show — how tired he is, and whether
he is in a fever. The crowd in the stands never gets one. Want the old view, a bar over every soldier on
the screen? Switch `AltMarksEveryone` on.

```
     a fresh man:            a tired man in a charge:
        [██████████]            [█████░░░░░]   his Athletics bar (green / yellow)
                                [▓▓▓▓▓▓▓▓▓▓]   his fever bar under it (orange)
        [██████░░░░]            [███░░░░░░░]   his morale at the bottom (blue, 0-100)
```

**Morale bars** (step 67): at the bottom of the same men's bars, a **blue** bar - his morale, full = 100 (the
game's own number: a man starts at about 35-65 and loses it as his side loses men; below about 15 he may rout). In a
tournament there is no morale, so no bar.

**Fever marks**: under that bar, a second small bar on each man in a **fever** (section 11) — yours and
the enemy's. Its length is the fever left. His Athletics bar stays at the same height whether he is in a
fever or not, so nothing jumps.

| Fever mark | Means |
|---|---|
| Bright orange, full | armed — he is charging and has not met the enemy yet |
| Orange, shrinking | the clash began: his fever burns (faster with a shield in hand) |
| Faint yellow, short | nearly spent — he is about to calm down |
| No mark | calm (no fever), or you |

The bars of the men you look at stay bright however close they are (none closer than 1.5 m to the camera),
and a rider's sit a little higher. With `AltMarksEveryone` on, close bars fade out like the game's own icons,
far ones are a little smaller, and in a huge battle only the 200 nearest men on the screen get them.

**Lines in the message feed** (section 11c): red when you are knocked down, knocked off your horse, lose your
shield or reel from a riposte; purple when you knock a man down or off his horse, break his guard or knock his
shield away — the colours the game uses for its own couched-lance lines. At most one every 1.5 s, merged.

<sub>Settings: `ShowPlayerBar`, `ShowAttackRecoveryBar`, `ShowSprintBar`, `ShowInOrderMenu`, `ShowAltMarkerStats`,
`AltMarkersShowEnemy`, `ShowReadyCount`, `Bar*BelowPercent`, `Ready*BelowPercent`, `ShowAthleticsMarks`, `AthleticsMarksShowEnemy`,
`ShowFeverMarks`, `FeverMarksShowEnemy`, `ShowFeverOnLabels`, `ShowMoraleMarks`, `ShowMoraleOnLabels`, `MoraleLabelMetres` 60, `MoraleLabelMax` 4, `AltMarksEveryone` off, `AltNearMarksMax` 5, `AltNearMarksMetres` 15,
`FeverMarkMax` 200 (men marked, both bars - the every-man view's cap and the ceiling), `ImpactShowMessages`, `ImpactMessagesIncludeYourMen`,
`ImpactMessageMinSeconds` 1.5, and the HUD's sizes and offsets
(`AthleticsMarkWidth` 22, `AthleticsMarkHeight` 5, `AthleticsMarkGap` 2, `FeverMarkWidth` 22, `FeverMarkHeight` 5,
`FeverMarkHeightMetres` 2.5).</sub>

## 16. Where the settings live

(The tactics these numbers create are in the player guide: [HOW-IT-WORKS.md → Tactics](HOW-IT-WORKS.md#tactics).)

Every number above is a setting: in **MCM** (optional) or in the commented `config.json` beside the
log. Changes apply at once, even mid-battle — nothing needs a restart. Each feature has its own switch,
and a **master switch** turns the whole mod off mid-battle, so you can play the same fight both ways.

MCM shows **two pages** (Mod Options):

- **Trax Combat Enhancements** — the short page, the 21 settings most players want:

  | Group | Settings |
  |---|---|
  | Master switch | Mod enabled |
  | How hard hits land | Damage roll (± %), Damage reduction (%) |
  | Tiredness (Athletics) | Athletics (stamina) on / off, Cost per swing — how fast men tire, Heroes' cost (x) |
  | How the AI fights | AI aggression when fresh / when exhausted (%), Fever on a charge or advance order, Shield walls hold their slots + a step out to strike (m), Courage and cohesion, Cavalry flocking, Line discipline: your units too |
  | Staggers and knockdowns | Staggers, shield drops, knockdowns on / off, Against you (%) |
  | Sprinting | Sprint on / off, Sprint key |
  | On screen | Your Athletics bar, Hold ALT: bars on every man, On-screen lines (knocked down, shield lost) |
  | Defaults | **Revert all to defaults** — every setting of both pages |

- **Trax Combat Enhancements - Advanced** — everything else, for tuning: every system's own group
  (damage, Athletics costs, tired fighters, the step back, battle pace, brace, sprint, aggression and
  fever, courage, cavalry flocking, impact, the refill, the bars, the orders strip, hold ALT, no accidental
  charge orders, line discipline by tier), the
  Defaults buttons (*Revert all to defaults* and *Save current values as a defaults file*), and the
  logging and test tools last.

`config.json` holds the same settings in the same order: the main ones first, then the advanced ones under
an `ADVANCED - for tuning` banner. The key names below are the file's.

- Too slow? Lower `DamageReductionPercent`, `ShieldInfantrySwingsLessPercent` or the brace floors, or raise
  `AiAggressionFreshPercent` / `AiAggressionExhaustedPercent`.
- Want the old fighting, everyone flailing? `AiAggressionFreshPercent` and `AiAggressionExhaustedPercent` 100 (the base
  game's AI at any bar).
- Not slow enough? Raise them, or lower `ExhaustedAttackSpeedPercent`.

The full list of settings, with ranges and what each does: [DESIGN.md → Parameters](DESIGN.md#parameters).
How the game's own code was used: [RESEARCH.md](RESEARCH.md). Every log line the mod writes:
[PLAYTEST.md](PLAYTEST.md).

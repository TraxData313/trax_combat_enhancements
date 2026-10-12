# How it works — the player's guide

Everything Trax Combat Enhancements does in a fight, in plain words and tables. Every number here is
the **shipped default** (`defaults.json`), and every one of them is a setting you can change — in the
Mod Configuration Menu (MCM) if you have it, or in
`Documents\Mount and Blade II Bannerlord\Configs\TraxCombatEnhancements\config.json`. The setting's
name is in the small print under each table, so you know which slider moves what.

Want the formulas and every parameter? [DESIGN.md](DESIGN.md) is the full contract.

**Contents**

1. [The idea in one minute](#1-the-idea-in-one-minute)
2. [The Athletics bar](#2-the-athletics-bar)
3. [What tiredness does](#3-what-tiredness-does)
4. [Resting and the refill](#4-resting-and-the-refill)
5. [Defending costs too](#5-defending-costs-too)
6. [Tired AI guess their blocks](#6-tired-ai-guess-their-blocks)
7. [Brace by orders](#7-brace-by-orders)
8. [Stepping back out of the press](#8-stepping-back-out-of-the-press)
9. [Battle pace](#9-battle-pace)
10. [Damage](#10-damage)
11. [Heroes, and the hideout boss](#11-heroes-and-the-hideout-boss)
12. [What you see on screen](#12-what-you-see-on-screen)
13. [Tactics this creates](#13-tactics-this-creates)
14. [Changing things](#14-changing-things)

---

## 1. The idea in one minute

Every fighter has a stamina bar, called **Athletics**. Attacking, kicking and blocking spend it;
resting refills it. While it is in its top quarter the man is at full strength. Below that he attacks
less often, runs slower, hits less hard at best, and — if he is AI — starts to guess which way to
block. His formation's orders decide how low he lets it go before he stops swinging and only defends.

So battles get slower, and **when** you commit a formation matters as much as which troops you bring.
A rested line beats a spent one. Fresh shock infantry thrown at a line that has been fighting for two
minutes is a real blow.

Nothing is slow motion. A tired man's swing looks almost normal; he just waits longer before the next
one.

## 2. The Athletics bar

A fighter's bar is as big as his **Athletics skill** (the one on the character screen), never smaller
than 50 points. One attack — a swing, thrust, shot or throw — costs 10 points, missed or not.

| Athletics skill | Bar (points) | Attacks from full to empty: soldier | hero | hero who leads his party |
|---:|---:|---:|---:|---:|
| 0–50 | 50 | 5 | 7 | 9 |
| 100 | 100 | 10 | 13 | 18 |
| 150 | 150 | 15 | 20 | 27 |
| 200 | 200 | 20 | 27 | 36 |
| 300 | 300 | 30 | 40 | 53 |

Heroes (lords, companions, you) pay a quarter less per attack; a hero who leads a party pays a quarter
less again (10 → 7.5 → 5.6 points).

**The peak line.** The white mark on the bar sits at **75%**. Above it a man is at full strength;
the first quarter of his bar is "free". Below it the effects in the next section start. A 50-point
recruit spends his free quarter in barely more than one swing; a 150-skill hero who leads his party
gets about seven.

**Wounds cap the bar.** A man at half health can only refill to half his bar.

<sub>Settings: `AthleticsPoolPerSkill` 1.0, `AthleticsPoolFloor` 50, `CostPerBlow` 10, `CostOnMiss` on,
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

1. After a typical 0.8 s swing (Anton's battle logs average about that). The pause is a wait before
   the next attack, not slow motion: blocking, moving and turning are never held. Your own pause fills
   the Attack recovery bar; the AI waits with its guard up. A heavier, slower weapon waits longer.
2. Roughly, compared with the same man fresh — the rate the pause aims at. Empty, he attacks once
   where he used to attack five times.
3. Every hit rolls its damage (section 10). A tired man still rolls as low as a fresh one, but his
   lucky high rolls shrink.
4. Only AI men, never you. See section 6 for what that means for his blocks.

Horses are not slowed by their rider's bar by default.

<sub>Settings: `ExhaustedAttackSpeedPercent` 20 (attacks at empty), `AttackAnimationMinPercent` 85,
`MinMoveSpeedMultiplier` 0.3, `MountMinSpeedMultiplier` 1.0 (horses off), `DamageBonusFollowsAthletics` on,
`TiredBlockRandomPercent` 100.</sub>

## 4. Resting and the refill

The bar starts to refill **3 seconds** after his last attack, kick, shield bash or wrong-side block
(clean blocks and parries don't stop it — section 5). It refills at a steady rate, a share of the
whole bar per second, so a big bar gains more points per second than a small one.

| What he is doing | Empty → full | Empty → the peak line |
|---|---:|---:|
| Standing or walking (up to 40% of his top speed) | 60 s | 45 s |
| Jogging (70% of top speed) | 80 s | 60 s |
| Running flat out | 120 s | 90 s |

A wounded man refills only to his health share (at 60% health, to 60% of the bar).

<sub>Settings: `RegenDelayBlowTimes` 2 × `BlowTimeSeconds` 1.5 (the 3 s), `FullRegenSecondsStanding` 60,
`RegenMultiplierAtFullRun` 0.5, `WalkEffortFraction` 0.4, `RegenRateNearFullPercent` 100 (a straight line).</sub>

## 5. Defending costs too

Every melee blow a man **blocks** costs him a little. Missiles into a shield are free.

| Block | Cost (soldier) | Hero | Hero party leader | Stops his refill? |
|---|---:|---:|---:|---|
| Shield, on the right side | 1 | 0.75 | 0.56 | No — the refill keeps running |
| Weapon parry | 2 | 1.5 | 1.13 | No — the refill keeps running |
| Shield, held on the **wrong** side | 5 | 3.75 | 2.81 | **Yes** — the 3 s wait starts again |
| A kick or shield bash he makes | 3 | 2.25 | 1.69 | Yes (it is an attack) |

So a skilful defender can catch his breath **while** defending. Can a man who is already empty climb
back while parrying a blow every 1.5 seconds (a steady duel), standing still?

| Bar size | Soldier, parrying | Hero, parrying | Hero party leader, parrying | Anyone, clean shield blocks |
|---:|---|---|---|---|
| 50 | sinks (−30 a minute) | sinks (−10) | climbs slowly (+5) | climbs |
| 80 | holds even | climbs (+20) | climbs (+35) | climbs |
| 100 | climbs (+20) | climbs (+40) | climbs (+55) | climbs |
| 150 | climbs (+70) | climbs (+90) | climbs (+105) | climbs |
| 200 | climbs (+120) | climbs (+140) | climbs (+155) | climbs |

Points per minute, net. A shield on the right side always climbs. Moving fast while fighting refills
slower (section 4), and a wrong-side block every 1.5 s holds anyone at the bottom.

<sub>Settings: `CostPerShieldBlock` 1, `CostPerWeaponParry` 2, `CostPerWrongSideShieldBlock` 5,
`CostPerKickOrBash` 3, `OnlyWrongSideBlocksStopRefill` on.</sub>

## 6. Tired AI guess their blocks

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
but on the wrong side — which costs him 5 points and stops his refill (section 5). So a tired shield
man doesn't just take the hit; he gets more tired, faster.

Low-skill troops (recruits, levies) already guess a lot in the vanilla game; for them the mod changes
little. It matters most for the elite: veterans, lords and tournament opponents. You are never
affected — your block is your mouse.

<sub>Setting: `TiredBlockRandomPercent` 100 (50 = half as many guesses at every level; 0 = off). It
works through the game's own "randomised defend direction" value, so it is the game's AI doing the
guessing.</sub>

## 7. Brace by orders

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

## 8. Stepping back out of the press

After a swing, a tired AI soldier may walk **backwards** about 2 m out of the press — facing his
enemy, guard up, for up to 1.5 s — and let a fresher man take his place. The chance follows his bar:

| Bar | 75–100% | 60% | 50% | 30% | 15% | 0% |
|---|---:|---:|---:|---:|---:|---:|
| Steps back after a swing | 0% | 20% | 33% | 60% | 80% | 100% |

Only when an enemy is within 4 m, never on a ship's deck, never in tournaments or arenas, and at most
50 men at once.

<sub>Settings: `StepBackMaxChancePercent` 100, `StepBackDistance` 2, `StepBackSeconds` 1.5,
`StepBackEnemyRange` 4, `StepBackMaxAtOnce` 50, `StepBackEnabled` on.</sub>

## 9. Battle pace

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

## 10. Damage

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

Hits on a shield are not rolled by default (still reduced). A hit never drops below 1.

<sub>Settings: `DamageRandomPercent` 50, `DamageReductionPercent` 25, `DamageRandomOnShields` off,
`DamageRandomMelee` / `Ranged` / `OnMounts` on.</sub>

## 11. Heroes, and the hideout boss

- **Heroes pay less** — a quarter less per attack, kick, bash and block; a party leader a quarter less
  again (section 2). With a hero's big Athletics skill, his bar is big too: late in a battle your heroes
  are often the freshest men on the field.
- **The hideout boss fight is a fresh start.** When the "Win the Duel" / "Win the Fight" objective
  appears, your side refills to its wound cap and every pause and step back ends. In a duel your men
  stand aside, so only you refill. The boss comes fresh — so do you.

<sub>Settings: `HeroCostMultiplier` 0.75, `PartyLeaderCostMultiplier` 0.75, `HideoutBossFightRefill` on.</sub>

## 12. What you see on screen

**Your Athletics bar** sits under your health, with the white peak line at 75%. Its colour:

| Colour | Your bar |
|---|---|
| Green | at or above the peak line (75%) |
| Blue | just under it |
| Yellow | 56% or less |
| Orange | 38% or less |
| Red | 19% or less, and empty |

It also shows outside battles (walking about a town) while a weapon is drawn or the bar is refilling.

**The Attack recovery bar** sits just above it: empty right after your attack, filling over your pause
with the seconds left written inside, full when you can attack again. Press attack too early and it
flashes — the press is swallowed; hold the button and the attack fires the moment the bar is full.

**The orders menu** shows, under each formation card: the men's average Athletics with its ± spread,
their average health, and **"ready 34/50"** — how many are ready to fight (not bracing).

**Hold ALT** for the same numbers under the game's own formation markers, yours **and the enemy's**.

| "ready" colour | When |
|---|---|
| Plain | more than 75% of the men ready |
| Yellow | 75% or fewer ready |
| Red | 50% or fewer ready — this line is spent |

<sub>Settings: `ShowPlayerBar`, `ShowAttackRecoveryBar`, `ShowInOrderMenu`, `ShowAltMarkerStats`,
`AltMarkersShowEnemy`, `ShowReadyCount`, `Bar*BelowPercent`, `Ready*BelowPercent`, and the HUD's sizes
and offsets.</sub>

## 13. Tactics this creates

- **Keep your troops rested.** A line at full strength beats a line at 30% almost every time — it
  attacks twice as often, runs faster and blocks true. Don't spend men you don't need yet.
- **Hold, then charge.** A holding line braces at 60% and keeps its strength in reserve while the enemy
  wears itself out on it. When their markers go red (hold ALT), order the charge: your men drop their
  guard and surge in with what they saved.
- **Fresh shock troops into tired lines.** A reserve of fresh infantry or cavalry thrown at a formation
  that has been fighting for two minutes meets men who swing once where they used to swing five times,
  run at a third of their speed and guess their blocks.
- **Rotate.** Pull a spent formation back, let it walk for a minute (walking refills as fast as
  standing), put it back in.
- **Don't let your hero swing himself empty.** Watch your bar; step out, block on the right side, and
  let it climb before you go again.
- **Block on the right side.** Clean blocks and parries let your bar refill while you defend; a
  wrong-side block stops it.
- **Read the enemy.** "ready 12/48" in red under an enemy marker is an invitation.

## 14. Changing things

Every number above is a setting: in **MCM** (optional) or in the commented `config.json` beside the
log. Changes apply at once, even mid-battle — nothing needs a restart. Each feature has its own switch,
and a **master switch** turns the whole mod off mid-battle, so you can play the same fight both ways.

- Too slow? Lower `DamageReductionPercent`, `ShieldInfantrySwingsLessPercent` or the brace floors.
- Not slow enough? Raise them, or lower `ExhaustedAttackSpeedPercent`.

The full list of settings, with ranges and what each does: [DESIGN.md → Parameters](DESIGN.md#parameters).
How the game's own code was used: [RESEARCH.md](RESEARCH.md). Every log line the mod writes:
[PLAYTEST.md](PLAYTEST.md).

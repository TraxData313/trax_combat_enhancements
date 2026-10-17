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
4. [The swing stack](#4-the-swing-stack)
5. [Resting and the refill](#5-resting-and-the-refill)
6. [Defending costs too](#6-defending-costs-too)
7. [Tired AI guess their blocks](#7-tired-ai-guess-their-blocks)
8. [Brace by orders](#8-brace-by-orders)
9. [Stepping back out of the press](#9-stepping-back-out-of-the-press)
10. [Battle pace](#10-battle-pace)
11. [Eagerness, and the fever of a charge](#11-eagerness-and-the-fever-of-a-charge)
11b. [Courage: men keep to their group](#11b-courage-men-keep-to-their-group)
11c. [Impact: kicks that break a tired man](#11c-impact-kicks-that-break-a-tired-man)
12. [Damage](#12-damage)
13. [Heroes, and the hideout boss](#13-heroes-and-the-hideout-boss)
14. [The sprint](#14-the-sprint)
15. [What you see on screen](#15-what-you-see-on-screen)
16. [Tactics this creates](#16-tactics-this-creates)
17. [Changing things](#17-changing-things)

---

## 1. The idea in one minute

Every fighter has a stamina bar, called **Athletics**. Attacking, kicking and blocking spend it;
resting refills it. While it is in its top quarter the man is at full strength. Below that he attacks
less often, runs slower, hits less hard at best, and — if he is AI — starts to guess which way to
block. His formation's orders decide how low he lets it go before he stops swinging and only defends.

AI soldiers are also **careful** by nature: they hold their line and spar rather than trade blows until
they drop, and the more tired they are, the less they swing — until you (or their general) order the
**charge**. Then they surge, and the surge fades (section 11).

So battles get slower, and **when** you commit a formation matters as much as which troops you bring.
A rested line beats a spent one. Fresh shock infantry thrown at a line that has been fighting for two
minutes is a real blow.

Nothing is slow motion. A tired man's swing looks almost normal; he just waits longer before the next
one — and anyone can save up a few swings and strike them back to back (the swing stack, section 4).

## 2. The Athletics bar

A fighter's bar is as big as his **Athletics skill** (the one on the character screen), never smaller
than 50 points. One attack — a swing, thrust, shot or throw — costs 7 points, missed or not.

| Athletics skill | Bar (points) | Attacks from full to empty: soldier | hero | hero who leads his party |
|---:|---:|---:|---:|---:|
| 0–50 | 50 | 7 | 10 | 13 |
| 100 | 100 | 14 | 19 | 25 |
| 150 | 150 | 21 | 29 | 38 |
| 200 | 200 | 29 | 38 | 51 |
| 300 | 300 | 43 | 57 | 76 |

Heroes (lords, companions, you) pay a quarter less per attack; a hero who leads a party pays a quarter
less again (7 → 5.25 → 3.94 points). Attacks are cheap because the AI is careful (section 11): it decides
to swing far less often than the base game's AI, so each swing need not cost much for a long fight to wear men down.

**The peak line.** The white mark on the bar sits at **75%**. Above it a man is at full strength;
the first quarter of his bar is "free". Below it the effects in the next section start. A 50-point
recruit spends his free quarter in about two swings; a 150-skill hero who leads his party gets about
ten.

**Wounds cap the bar.** A man at half health can only refill to half his bar.

<sub>Settings: `AthleticsPoolPerSkill` 1.0, `AthleticsPoolFloor` 50, `CostPerBlow` 7, `CostOnMiss` on,
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
back (5.25 points each) — and by then his bar is at 36% and he holds 3 at most, so each next one takes
about 0.6 s to come back.

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
| Shield, on the right side | 1 | 0.75 | 0.56 | No — the refill keeps running |
| Weapon parry | 2 | 1.5 | 1.13 | No — the refill keeps running |
| Shield, held on the **wrong** side | 2 | 1.5 | 1.13 | **Yes** — the 3 s wait starts again |
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
slower (section 5), and a wrong-side block every 1.5 s holds anyone at the bottom — not by its price
(2 points) but because each one stops the refill.

<sub>Settings: `CostPerShieldBlock` 1, `CostPerWeaponParry` 2, `CostPerWrongSideShieldBlock` 2,
`CostPerKickOrBash` 3, `OnlyWrongSideBlocksStopRefill` on.</sub>

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
but on the wrong side — which costs him 2 points and stops his refill (section 6). So a tired shield
man doesn't just take the hit; he stops recovering.

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

<sub>Settings: `ShieldWallHoldsSlots` on (live — switch it off mid-battle to see the difference); `ShieldWallStepOutMetres`
1.0 m (0 = they stand fast and fight only what reaches them; 2-3 = a looser wall; live).</sub>

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

**They sprint the last metres.** A man in his fever with an enemy 3–12 m away may sprint at him: every
second he has a 50% chance at the fever's start, sliding down with the fever (half the fever, 25%;
none when it is gone). He spends only the sprint he holds above his order's reserve (section 14).

A bracing man (section 8) still does not attack, fever or not.

**See it: hold ALT.** Every man in a fever — yours and the enemy's — gets a small bar above his head while
you hold ALT (or the orders menu is open): **bright orange** and full while he runs at the enemy, shrinking
and turning **yellow** as the fever burns after the clash, gone when it is spent (section 15).

<sub>Settings: `AiAggressionFreshPercent` 60, `AiAggressionExhaustedPercent` 20 (both 100 = the base game's AI),
`FeverEnabled` on, `FeverPointsPerSecond` 10, `FeverAggressionPercent` 80, `FeverContactMetres` 5,
`FeverShieldDurationMultiplier` 0.7, `FeverSprintToClose` on, `FeverSprintMinMetres` 3, `FeverSprintMaxMetres` 12,
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

## 11c. Impact: kicks that break a tired man

In the base game a kick only pushes a man back, and he is guarding again before you can swing. With this
mod a **kick or a shield bash on a tired man** can do much more — **knock him down**, make him **drop his
shield**, and he **reels longer** and **gets up slower**. On a fresh man nothing changes. (This is the first
part of the impact system; blocks and clean hits follow in later versions.)

What counts is how tired **he** is, and how you two compare: both men's **current** Athletics (how big the
bar is AND how full), what struck him and both men's skill. Two even men, the kicker fresh:

| His bar | Shield dropped | Knocked down | His reel | His get-up |
|---|---:|---:|---|---|
| 100% – 75% (green) | 0% | 0% | as normal | as normal |
| 50% | 14% | 1% | ×1.5 longer | ×1.33 slower |
| 25% | 82% | 70% | ×2.0 longer | ×1.67 slower |
| 10% | 90% | 80% | ×2.3 longer | ×1.87 slower |
| 0% (empty) | 90% | 80% | ×2.5 longer | ×2 slower |

The more tired he is, the more **random** the reel and the get-up get (± up to 30% when empty) — a fresh
man's timing is predictable, an exhausted man's is not.

**Size matters.** A big, fresh fighter cannot be kicked over: you (Athletics 100) against a tournament king
with 220:

| You kick the king at… | Shield dropped | Knocked down |
|---|---:|---:|
| 100% (fresh) | 0% | 0% — no kick-win against a fresh king |
| 30% | 45% | 26% |
| 10% | 90% | 80% — tire him first, then break him |

**Nobody is kicked to death.** After he gets up he cannot be knocked down again for **5 seconds**; after a
long reel no new stagger for **2 seconds**; and each knockdown or dropped shield **halves** the next chance
against him for **8 seconds**. A man on the ground is not kicked down again. A kick still costs you
Athletics and a stored swing (sections 4 and 6), so spamming it empties you.

**A dropped shield** lies on the ground and can be picked up again the normal way — you with the use key,
the AI only once no enemy is within 20 m (so not in the middle of a melee).

The same rules apply to you (every chance against you × `ImpactOnPlayerPercent`, 100%), and every kick you
give or take is written to the log (`[impact] YOU: ...`). Riders are not knocked down by kicks yet.

<sub>Settings ("Impact: staggers, shield drops, knockdowns"): `ImpactEnabled` on, `ImpactKicksEnabled` on,
`ImpactShadowMode` off (on = decide and log only), `KickShieldDropMaxPercent` 90, `KickKnockdownMaxPercent` 80,
`KickReelTiredExtraPercent` 150, `GetUpMaxSlowdown` 2, `ImpactRandomPercent` 30, `ImpactKnockdownImmunitySeconds` 5,
`ImpactStaggerImmunitySeconds` 2, `ImpactDiminishWindowSeconds` 8, `ImpactDiminishFactor` 0.5, `ShieldPickupLockSeconds` 0,
`ImpactOnPlayerPercent` 100.</sub>

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

Hits on a shield are not rolled by default (still reduced). A hit never drops below 1.

<sub>Settings: `DamageRandomPercent` 50, `DamageReductionPercent` 25, `DamageRandomOnShields` off,
`DamageRandomMelee` / `Ranged` / `OnMounts` on.</sub>

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

(A man with a 5 s store: a move from full sprints 1 s, an advance 2.5 s, a charge 5 s.)

A man stops early when an enemy is within 3 m or the order changes again. Give a formation a new charge order
and watch it surge — tired men too, off their 2 s floor. In their fever (section 11) men may also sprint again
at an enemy a few metres off, with what they hold above the reserve.

**Tip — shock troops.** Move your shock infantry round behind the enemy with plain **move** orders: the first
move costs them a little sprint and none of them touches the last 80%. When you give the real **charge**, they
still have most of their store and hit the enemy's back at a sprint. Men advanced into position keep half.

<sub>Settings: `SprintEnabled` on, `SprintKey` LeftControl (a key name: LeftControl, RightControl, LeftAlt,
RightAlt, LeftShift, RightShift, CapsLock, Tab, Q, T, Y, H, J, K, L, B, N, M, C, MiddleMouseButton,
X1MouseButton, X2MouseButton), `SprintPointsPerSecond` 20, `SprintMinSeconds` 2, `SprintMinStartPercent` 20,
`SprintMaxMultiplier` 2.0, `SprintMinMultiplier` 1.1, `SprintFullSpeedSharePercent` 30,
`SprintTopSpeedReachSeconds` 1, `SprintRefillSeconds` 7, `SprintRefillMovingFactor` 0.5,
`SprintRefillExhaustedFactor` 0.5, `SprintAiEnabled` on, `SprintAiChargeKeepPercent` 0,
`SprintAiAdvanceKeepPercent` 50, `SprintAiMoveKeepPercent` 80. The run-now column comes from tiredness: `MinMoveSpeedMultiplier` 0.3 at an empty
bar, `AthleticsPeakPercent` 75.</sub>

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

**Hold ALT** for the same numbers under the game's own formation markers, yours **and the enemy's**.

| "ready" colour | When |
|---|---|
| Plain | more than 75% of the men ready |
| Yellow | 75% or fewer ready |
| Red | 50% or fewer ready — this line is spent |

**Athletics bars over the men** (hold ALT, or the orders menu open): a small bar above **every soldier's**
head — yours, your allies' and the enemy's (not yours: you have your own) — his own Athletics bar in
miniature: its length is the Athletics he has left, its colour the bar's colour (green at full strength,
blue just below the peak line, then yellow, orange, red), the part his wounds hold dark red. Watch a line
go from green to red as the fight goes on, and pick the tired enemy to charge.

```
     a fresh man:            a tired man in a charge:
        [██████████]            [█████░░░░░]   his Athletics bar (green / yellow)
                                [▓▓▓▓▓▓▓▓▓▓]   his fever bar under it (orange)
```

**Fever marks**: under that bar, a second small bar on each man in a **fever** (section 11) — yours and
the enemy's. Its length is the fever left. His Athletics bar stays at the same height whether he is in a
fever or not, so nothing jumps.

| Fever mark | Means |
|---|---|
| Bright orange, full | armed — he is charging and has not met the enemy yet |
| Orange, shrinking | the clash began: his fever burns (faster with a shield in hand) |
| Faint yellow, short | nearly spent — he is about to calm down |
| No mark | calm (no fever), or you |

Close to the camera the bars fade out (none closer than 1.5 m), far away they are a little smaller, a
rider's sit a little higher, and in a huge battle only the 200 nearest men on the screen get them.

<sub>Settings: `ShowPlayerBar`, `ShowAttackRecoveryBar`, `ShowSprintBar`, `ShowInOrderMenu`, `ShowAltMarkerStats`,
`AltMarkersShowEnemy`, `ShowReadyCount`, `Bar*BelowPercent`, `Ready*BelowPercent`, `ShowAthleticsMarks`, `AthleticsMarksShowEnemy`,
`ShowFeverMarks`, `FeverMarksShowEnemy`, `FeverMarkMax` 200 (men marked, both bars), and the HUD's sizes and offsets
(`AthleticsMarkWidth` 22, `AthleticsMarkHeight` 5, `AthleticsMarkGap` 2, `FeverMarkWidth` 22, `FeverMarkHeight` 5,
`FeverMarkHeightMetres` 2.5).</sub>

## 16. Tactics this creates

- **The charge is a surge — time it.** A charge order lifts your men to 80% of the base game's eagerness for
  their fever (about 10 s for a rested 100-point soldier, less for tired ones), then they settle back to what
  their bars allow. A tired line on a charge surges briefly; a rested one hits hard and long.
  Order it when it will decide the fight, not as a standing order. A new advance or charge restarts it
  only if that gives them more.
- **Keep your troops rested.** A line at full strength beats a line at 30% almost every time — it
  attacks twice as often, runs faster and blocks true. Don't spend men you don't need yet.
- **Hold, then charge.** A holding line braces at 60% and keeps its strength in reserve while the enemy
  wears itself out on it. Put it in a shield wall and it keeps its shape while it does. When their markers go red (hold ALT), order the charge: your men drop their
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
- **Sprint to get out, not to get in tired.** A short Ctrl-dodge saves a cornered hero; a long sprint into
  a fight arrives with a bar that has not refilled. Charge orders make your men sprint by themselves —
  give the charge when the gap is short enough that their store carries them all the way in.

## 17. Changing things

Every number above is a setting: in **MCM** (optional) or in the commented `config.json` beside the
log. Changes apply at once, even mid-battle — nothing needs a restart. Each feature has its own switch,
and a **master switch** turns the whole mod off mid-battle, so you can play the same fight both ways.

- Too slow? Lower `DamageReductionPercent`, `ShieldInfantrySwingsLessPercent` or the brace floors, or raise
  `AiAggressionFreshPercent` / `AiAggressionExhaustedPercent`.
- Want the old fighting, everyone flailing? `AiAggressionFreshPercent` and `AiAggressionExhaustedPercent` 100 (the base
  game's AI at any bar).
- Not slow enough? Raise them, or lower `ExhaustedAttackSpeedPercent`.

The full list of settings, with ranges and what each does: [DESIGN.md → Parameters](DESIGN.md#parameters).
How the game's own code was used: [RESEARCH.md](RESEARCH.md). Every log line the mod writes:
[PLAYTEST.md](PLAYTEST.md).

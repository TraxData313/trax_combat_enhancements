# How it works — the player's guide

Everything Trax Combat Enhancements does in a fight, in plain words: what each system does, what you will
see, and what to do about it. The short story is in the [README](../README.md).

**Where the numbers live.** This guide keeps them to a minimum. Every table, every worked example and the
name of every setting are in the mechanics reference, [MECHANICS.md](MECHANICS.md) — each section below
ends with a link to its numbers there. Every number is a setting you can change, live, even mid-battle.

**Contents**

- [The idea in one minute](#the-idea-in-one-minute)
- [Your Athletics bar](#your-athletics-bar)
- [What tiredness does](#what-tiredness-does)
- [Rest, and defending while you rest](#rest-and-defending-while-you-rest)
- [Orders set how hard they fight](#orders-set-how-hard-they-fight)
- [Careful men, and the fever of a charge](#careful-men-and-the-fever-of-a-charge)
- [The shield wall holds](#the-shield-wall-holds)
- [Courage: men stick together](#courage-men-stick-together)
- [Cavalry comes in waves](#cavalry-comes-in-waves)
- [Blows that count: staggers, shields, knockdowns](#blows-that-count-staggers-shields-knockdowns)
- [Damage](#damage)
- [Heroes, duels and tournaments](#heroes-duels-and-tournaments)
- [The sprint](#the-sprint)
- [Your orders: a click charges, a drag never does](#your-orders-a-click-charges-a-drag-never-does)
- [What you see on screen](#what-you-see-on-screen)
- [Tactics](#tactics)
- [Settings](#settings)

---

## The idea in one minute

Every fighter has a stamina bar, called **Athletics**. Attacking, kicking and blocking spend it; resting
refills it. While it is in its top quarter the man is at full strength. Below that he attacks less often,
runs slower, hits less hard at best, and — if he is AI — starts to guess which way to block.

AI soldiers are also **careful** by nature: they hold their line and spar rather than trade blows until
they drop, and the more tired they are, the less they swing — until you (or their general) order the
**charge**. Then they surge, and the surge fades.

So battles get slower, and **when** you commit a formation matters as much as which troops you bring. A
rested line beats a spent one. And a fresh unit thrown at a line that has been fighting for two minutes is
a real blow: tired men are staggered, knocked down, their shields knocked away.

Nothing is slow motion. A tired man's swing looks almost normal; he just waits longer before the next one.

## Your Athletics bar

A fighter's bar is as big as his **Athletics skill** — the one on the character screen — and never smaller
than 50 points. Every attack costs a little of it, missed or not; so do kicks, shield bashes and blocks.
**Heroes pay a quarter less**, and a hero who leads a party a quarter less again — with a hero's big
Athletics skill on top, your heroes outlast their men.

The **white mark** on the bar sits at three quarters: above it a man is at full strength, below it the
effects begin. **Wounds cap the bar**: a man at half health can only refill to half.

| A bar of… | Attacks from full to empty: a soldier | a hero who leads his party |
|---:|---:|---:|
| 50 (a recruit) | 5 | 9 |
| 100 | 10 | 18 |
| 200 (a veteran hero) | 20 | 36 |

Numbers: [MECHANICS §2](MECHANICS.md#2-the-athletics-bar).

## What tiredness does

Under the white mark every effect slides in a straight line to its worst at an empty bar:

- **He attacks less often.** After an attack he waits before the next — at an empty bar he attacks once
  where he used to attack five times. Blocking, moving and turning are never held.
- **But he can save swings up.** Everyone stores a few swings (more with a fuller bar) and can strike them
  back to back with no wait; the wait comes once the stored swings run out. One slow swing every few
  seconds is easy to read — a burst of four is not.
- **He runs slower** — at an empty bar about a third of his fresh speed.
- **His lucky hits shrink.** A tired man still rolls as low as a fresh one, but his best blows are gone.
- **Tired AI guess their blocks.** A tired AI fighter starts picking his block direction at random — even a
  tournament champion who never misses in the base game. You are never affected: your block is your mouse.

Numbers: [MECHANICS §3](MECHANICS.md#3-what-tiredness-does), the stored swings
[§4](MECHANICS.md#4-the-swing-stack), the block guesses [§7](MECHANICS.md#7-tired-ai-guess-their-blocks).

## Rest, and defending while you rest

The bar starts to refill a few seconds after a man's last attack — about a minute from empty to full
standing or walking, slower jogging, half as fast running flat out. Walking refills as fast as standing,
so a formation pulled back and walked about recovers.

**A skilful defender recovers on the defence.** A clean block on the right side, or a parry, costs a
little but lets the refill run on underneath. A block on the **wrong** side (the shield caught the blow
off-side) costs twice as much, stops the refill and starts the wait again. So a man who reads his enemy can
catch his breath while defending — and a man who keeps guessing wrong stays at the bottom. **A raised shield facing the blow covers
it:** a shield block against a man in front of you counts as the right side, whichever way the game says you held
it — the same for the AI. From the side or behind, the side still matters, and so does it for a weapon parry. A
tired soldier still misjudges some of his blocks, so a worn-out wall can still be broken.

Numbers: [MECHANICS §5](MECHANICS.md#5-resting-and-the-refill), [§6](MECHANICS.md#6-defending-costs-too).

## Orders set how hard they fight

Your order decides how far your men let their bars fall before they stop attacking and only **brace** —
shield out and up, guard up, arrows still allowed — until they have caught their breath.

| Order | They brace at | They fight again at |
|---|---:|---:|
| Charge | 20% | 40% |
| Advance | 40% | 60% |
| Hold, move, follow, fall back… | 60% | 80% |

Each man rolls his own floor once per battle, a little above or below his order's, so a line gives way man
by man, never all at once. Order the charge and the braced men are back in the fight at once with
everything they saved.

A tired AI soldier may also **step back** a couple of metres out of the press after a swing — facing his
enemy, guard up — and let a fresher man take his place. And AI soldiers simply fight at a steadier
**pace**: shield infantry swing least (a shield wall should hold, not flail), archers and crossbowmen take a
moment longer between shots.

Numbers: [MECHANICS §8](MECHANICS.md#8-brace-by-orders), [§9](MECHANICS.md#9-stepping-back-out-of-the-press),
[§10](MECHANICS.md#10-battle-pace).

## Careful men, and the fever of a charge

A man at work doesn't hold a pace until he drops. An AI soldier on foot decides to start an attack less
often than the base game's AI — about two thirds as often when fresh, a fifth as often when empty. He
holds his place, blocks, and waits for an opening.

**The fever.** When a formation gets a **new charge or advance order** — yours or its own general's — every
man on foot in it surges: eager whatever his bar, sprinting the last metres at the enemy. The fever starts
burning when he first comes close to the enemy (a charge ordered from afar is not spent on the walk), and
fades back to what his bar allows. A man with a big, full bar surges longer; a man already tired from a long
fight surges short; a man fighting from behind a shield burns it faster. A hold order ends it.

**See it:** hold ALT — each formation's marker shows how many of its men are in a fever, and the men you
look at carry a small orange bar that shrinks as the fever burns.

Numbers: [MECHANICS §11](MECHANICS.md#11-eagerness-and-the-fever-of-a-charge).

## The shield wall holds

In the base game a man in a shield wall walks a few steps out to swing at the enemy and walks back — every
man does it, and the wall bulges and breaks. With this mod a man in a shield wall, square or circle keeps
his place and fights whatever reaches him. When the enemy in front of him backs off just out of reach, he
takes one short step out after him, strikes, and walks back to his place. A man thrown out of the wall by a
blow or shoved out by the press is held again at once and, as soon as he is back on his feet, backs off into his
place facing the enemy — the wall closes up; a man who kicked or bashed walks straight back after it. On a charge the
wall breaks on purpose; loose lines and columns keep the game's own behaviour.
**A standing wall stands firm:** order a wall to **stop** (or move it to a place and let it arrive) and nobody steps out at all — each
man strikes, blocks and kicks only from his place, and a man a kick or a shove carries forward backs straight in (player page: *Shield
walls stand firm on a hold order*).
**Spearmen keep their spears:** a soldier with a spear and a sword fights with the spear until an enemy has stood within the
spear's reach for a moment, and the men in the back ranks of a wall keep their spears whatever is close (player page: *Spearmen keep
their spears*). **Thrusts pass a friend:** a spear thrust that touches a comrade carries on to the enemy behind him instead of stopping
on him - swings still stop (Advanced page, *Battle pace (AI)*).
**Press, don't pass:** soldiers fighting an enemy line press against its front instead of walking round its ends or through it - they go round only when their own unit is clearly wider and moves as a mass, through only where a real gap has opened, and a man who got behind it walks back round its end (player page: *Press, don't pass*).
**The battle's rhythm:** in the grind a worn-out man at the front swaps places with a fresher mate right behind him - the fresh man steps up beside him first, so the line never opens - and when the front men of BOTH sides are spent and nobody breaks, both fronts ease back a couple of metres, face each other and catch their breath while the tired go to the back; it ends when one side has recovered or gets a new charge order (player page: *Tired front men swap with fresh ones*, *Lulls when both fronts are spent*, *Lulls: your units ease back too*).

**The wall pushes as one.** Order a shield wall to **advance** (F1, then F4) and in the base game its men each walk to
a place at the enemy's line — the ones free to move walk on into the enemy while their mates fight, and the wall breaks
up. Now, close to the enemy, every man of the wall is sent only a short step ahead of his place in the line, all
together: the whole wall walks forward as one, pushes at the pace its fighting front allows, and nobody runs ahead of
it. Moving, falling back and following work the same way; a charge still lets the men go.

Numbers: [MECHANICS §10](MECHANICS.md#10-battle-pace) (the end of it).

## Courage: men stick together

Real men stick with their group. Every AI soldier on foot keeps asking: **how strong are we here, against
them?** He weighs the men around him — armour, level, wounds; a man with a great axe or a polearm and no
shield counts extra — and it takes about four or five looters to face one armoured veteran. Tired, hit or
losing men need better odds.

- **Outmatched and alone,** he walks back to the nearest bigger group of his side, facing the enemy. Nobody
  circles your shield wall alone any more.
- **Outmatched among his mates,** he holds his place and fights only what is in reach; now and then a fresh
  man steps in, swings, and comes back.
- **Shaken men pack in**: a man who was just hit wants more mates around him, so a battered line folds into a
  mass instead of scattering. A shaken man facing hopeless odds **panics** and sprints back.
- **A mass envelops, a few don't.** A crowd wraps round the ends of your wall when it really outnumbers you
  there — about two men for every defender. Thirty looters against your twenty-man wall stand in an arc in
  front of it.
- **The charge still goes in.** A man in his fever who has not been hit attacks whatever the odds: the first
  clash always happens - but only with his mates around him. A man who ends up **alone past your line** (his
  sprint carried him through, or he chased you on your horse) no longer stays there: his sprint stops and he
  walks back round your line to his group, unless he is running down a beaten enemy.
- **Routing men run for it.** When a man's morale breaks, he spends his whole sprint on the first get-away —
  so a broken line scatters fast, and men who spent their sprint charging have little left to flee with.

**Fear makes a herd.** As a unit loses its morale and its breath, its men care less about their places in the line
and more about being **among their own**. A frightened unit closes up and edges back from you - not into a deep
column, but into a **wide, shallow mass** along your line, four to six men deep: each man keeps his place along the
front and only closes up from behind; men who would stand too deep slip out to its ends. Its front men still step
forward to swing at what is in reach and come back to the mass. Green troops do it early - a looter line
with nobody behind it thickens by itself - while veterans hold their shape until they are truly spent. A fresh
comrade in front steadies a man for a while; a shield wall holds until its morale breaks. Hold ALT to see it
coming: each soldier you look at shows his **morale** in blue under his bars, and the nearest formations say
"morale 64, fresh 41%".

**Winning calms.** Fear only grips men who are losing: a man whose side clearly outnumbers or outclasses the enemy
around him (two to one), or faces a broken one, forgets his fear and goes in - tired winners finish the fight instead
of hanging back, and a charge that is mopping up is not held near its mass; with no enemy near, nobody is pulled back.

Numbers: [MECHANICS §11b](MECHANICS.md#11b-courage-men-keep-to-their-group) (the toughness table, the odds,
four worked fights), the fear flock [§11b1](MECHANICS.md#11b1-the-fear-flock-frightened-men-become-a-herd), the rout sprint
[§14](MECHANICS.md#14-the-sprint).

## Cavalry comes in waves

In the base game every charging rider picks his own target, rides through, turns and goes again, so a tired
cavalry unit trickles into the enemy one by one. With this mod, **fresh riders dance** the game's dance;
**tired riders look for their own kind** — horse archers with horse archers, skirmishers with skirmishers,
lancers with lancers — and ride together until the group is big enough, then go in **all at once**. Fresh:
small waves, often. Tired: big waves, rarely. Late in a battle the whole squadron gathers and comes back as
one. Only on a charge; never you.

Numbers: [MECHANICS §11b2](MECHANICS.md#11b2-cavalry-flocking-riders-go-in-as-waves).

## Blows that count: staggers, shields, knockdowns

On a fresh man of your size nothing changes. But a blow on a **tired** man can **break** him:

- **Stagger** him — a real opening for the next blow.
- Knock his **shield** out of his hand (he can pick it up again once the fight moves off).
- **Knock him down** — and the more tired he is, the slower he gets up.
- Knock a **rider off his horse**.

**Exhaustion makes mistakes count.** At the end of a long fight every blow tells. Two fresh men can do
nothing to each other; two empty men are thrown about by every kick and every bad block.

**A correct block punishes the attacker; a wrong one, the defender.** Block on the right side or parry, and
you stand — the man who swung tired into your good guard **reels**, and that is your moment. Block on the wrong
side when you are tired, and you are the one broken. Flailing into a good guard is a mistake.

**Size counts.** Every blow weighs the striker against the man he strikes: how much Athletics each has left,
the weapon (heavy, fast, blunt), the guard (a big shield, a stick), both men's skill. A king at full strength
kicks a spent recruit off his feet; the recruit's kick barely moves a fresh king — but an empty king is as weak
as anyone. Tire him first, then break him.

**Armour and rank count.** A heavily armoured veteran is far harder to move than a looter in rags — and his blows
move the looter far more. A legionary hits a looter as if twice as strong; the looter's blows on him count half.
An archer caught in melee with his bow still in his hands is easier to knock over.

**Hit from behind.** A blow from behind can floor even a fresh man: it hits him as if he were already half
exhausted, and a blow from the side a little. Two fresh men of a kind never knock each other down face to face —
from behind, one blow in fifteen does, and two in five stagger him. A unit that reaches the back of an engaged line
throws men to the ground, and archers caught there go down one blow in two.

**The charge.** A man running into his blow hits harder; at a full sprint, much harder. The sprint carries into
the **first** blow he strikes just after it — and a swing he began at a sprint lands as a charge however long it
takes (men slow down to swing) — and a sprinting blow rolls the **full damage
bonus** however tired he is. A rider carries his horse's speed. So **a fresh shock unit charging a tired line throws
men around** — staggered, knocked down, shields and riders knocked off — and every man who reels is easier to drop
with the next blow. Into a fresh line the same charge only trades blows.

**A charge into a good shield wall bounces.** Sprint into a shield that blocks you on the **right** side and your own
speed is turned against you: you reel, and the faster you came, the more tired you are and the tougher the shieldman,
the likelier you are **thrown back to the ground** — looters rushing a fresh legionary wall go down one in three,
and most of the rest reel off the shields. Every shield facing the charge counts, so the wall's front holds.
A walking man only reels; a parry never throws anyone. This goes for **you** too: charge a high-tier shieldman
head-on and you can land on your back ("You are thrown back by …'s shield"). Charge the flank, the back, or a
tired line — not a fresh wall.

**Every fall plays, on the blow.** When the game would have let a blow slip off a man without a reaction, a
knockdown or stagger decided for that blow now plays anyway, the moment it lands; if a decided fall still does not
start, the man is put on the ground a fifth of a second later. A friend's blow — a man in the second rank swinging
into his own front man — never knocks anyone down.

**Kicks and shield bashes.** The AI now kicks and shield-bashes. A man without a shield kicks about once a minute
while he fights — more against a man who hides behind his guard and in the fever of a charge, veterans more than
recruits, tired men not at all. A man with a shield bashes about as often as he swings while he is fresh, less as he
tires. They are not free: a kick or a bash costs 5 Athletics points (a swing 8), so a shieldman who bashes a lot tires
sooner — and so do you if you kick a lot. And a kick is no longer a free push: a fresh, well-armoured veteran **braces** and barely moves — a looter's kick staggers a fresh legionary for a moment
perhaps one time in eight, and never knocks him down. Tire him first: a spent man cannot brace, and then kicks and bashes
throw him about. The same goes for you, both ways. A man never kicks through his own mate: a soldier in the second rank
waits until nobody of his side stands between him and the enemy.


**The enemy goes for the opening.** When a man reels or lies on the ground — you included — the men fighting
him press it at once.

**Nobody is stun-locked to death:** after a stagger or a knockdown a man has a moment of safety, and every
repeat against the same man is less likely.

You get a short line in the message feed when it matters: **red** when it happens to you, **purple** when you do
it.

Numbers: [MECHANICS §11c](MECHANICS.md#11c-staggers-shield-drops-and-knockdowns) — every chance by tiredness,
the king and the man, the stick and the axe, armour and rank, blows from behind, the shock charge, a charge into a
shield wall, the cavalry charge, riders, how long.

## Damage

Every landed hit rolls its damage — anywhere from half to one and a half times the game's — so no two blows
are the same; a tired attacker loses the top of that range — unless he **charges**: a sprinting blow (or the first
blow just after a sprint) rolls the whole range, however tired he is. Then every hit lands **a quarter lighter**,
so fights last longer.

Numbers: [MECHANICS §12](MECHANICS.md#12-damage).

## Heroes, duels and tournaments

**Heroes matter.** They pay less per blow and have big bars, so late in a battle your lords and companions are
often the freshest men on the field — and a fresh man of size breaks tired men.

**In a tournament, an arena or a duel** there are no formations: men brace only near the bottom of their bars,
and there is no charge to surge on. A long bout is decided by stamina and timing: the tired champion starts
guessing his blocks; swing into his good guard while he is fresh and you reel, wait until he swings into yours
and he does. Hold ALT to see your opponent's bars.

**The hideout boss fight is a fresh start**: when the duel or the final fight begins, your side refills — the
boss comes fresh, and so do you.

Numbers: [MECHANICS §13](MECHANICS.md#13-heroes-and-the-hideout-boss).

## The sprint

Hold **Left Ctrl** for a short burst of speed on foot — a dodge out of a bad spot, onto a fleeing archer, round
a shield wall's flank. It reaches full speed in a second and fades back to your run. A tired man has a shorter
sprint, but even an empty bar keeps a two-second dodge. While you sprint your Athletics bar does not refill.

**The AI sprints too**, once per new order: on a charge everything they have, on an advance all but half a full store,
on a move only a little — so men moved into position still have their sprint for the real charge. A **charge** runs
all the way **into** the enemy and its first blow lands at speed; a man with only a little sprint (a looter) keeps it
until the enemy is close, so he still has it when he hits the line.

Numbers: [MECHANICS §14](MECHANICS.md#14-the-sprint).

## Your orders: a click charges, a drag never does

A click on an enemy unit's banner orders a charge at it; a drag never does. In the orders menu, press and let go with the cursor right
on the floating banner icon of an enemy unit and your selected men charge it, as in the game. Anything else - dragging out a line for
your men, even across or onto an enemy banner, or a click near a banner or over the enemy's men - places your men where you pointed,
and they keep their sprint for the charge you really mean. The F-key charge works as it always did. You can switch it off on the
Advanced page (*Orders: no accidental charges*).

Numbers: [MECHANICS §14b](MECHANICS.md#14b-your-orders-a-click-charges-a-drag-never-does).

## Green troops cannot hold a thin line

A long, thin line takes discipline. Each man's training sets how few ranks he can stand in - peasants and looters 7, recruits 6, tier-2
men 5, tier-3 men 4, tier-4 men 3, veterans 2, elites 1 (lords, companions and you count as elites) - and a foot formation's line is never
thinner than the average of its men. So 120 recruits stand at most 20 wide and 6 deep, where the game would have spread them 48 wide and
barely 2 deep; 60 veterans no thinner than 2 deep; elites may stand in one long rank if you ask; a mix of 4.5 gives a 5th rank about a third full. The AI's lines keep to it
whatever their general wants - no more green mass two men deep wrapping round your army - and as men fall the line closes up to stay deep
enough. Yours keep to it too: drag a line wider than your men can hold and it simply stops growing at their limit, with a line on screen
saying why. Cavalry, squares and circles are left alone. You can turn it off for your own units on the main page (*Line discipline: your
units too*); the ranks per tier are on the Advanced page (*Line discipline by tier*). And the block holds right into the clash: when the AI
throws such a formation into its final charge, its men no longer scatter into a wide crowd or trail round your flank - a man who runs out
of the block is walked back into it, while its front still hits you at full speed.

Numbers: [MECHANICS §14c](MECHANICS.md#14c-line-discipline-green-troops-cannot-hold-a-thin-line).

## What you see on screen

- **Your Athletics bar**, under your health, with the white mark at three quarters. Green at full strength,
  then blue, yellow, orange, red as you tire.
- **The Attack recovery bar** above it: **"Swings 4/8"** — your stored swings — filling toward the next. Press
  attack with none stored and it flashes.
- **The sprint bar** above that: the seconds of sprint you hold.
- **The orders menu**: under each formation card, the men's average Athletics and health, and **"ready 34/50"** —
  how many are ready to fight rather than bracing.
- **Hold ALT**: the same numbers under the game's own formation markers, **yours and the enemy's**, plus how many
  men are in a fever. "ready 12/48" in red under an enemy marker is an invitation.
- **Hold ALT and look at a man**: the five men nearest the centre of your screen, close by, get a small bar over
  their heads — his Athletics, his fever under it, and his **morale** in blue at the bottom. Works in tournaments too
  (no morale there).
- **Hold ALT near a formation**: the nearest few also say **"morale 64, fresh 41%"** — their average morale and
  Athletics. Low morale and a low bar: that unit is about to become a herd.

Numbers and colours: [MECHANICS §15](MECHANICS.md#15-what-you-see-on-screen).

## Tactics

- **The charge is a surge — time it.** A charge lifts your men for their fever, then they settle back to what
  their bars allow. A rested line hits hard and long; a tired one surges briefly. Order it when it will decide
  the fight, not as a standing order.
- **Keep your troops rested.** A line at full strength beats a line at a third almost every time — it attacks
  more often, runs faster and blocks true. Don't spend men you don't need yet.
- **Hold, then charge.** A holding shield wall keeps its shape and its strength while the enemy wears itself out
  on it. When their markers go red (hold ALT), order the charge.
- **Fresh shock troops into tired lines.** Hold a fresh reserve back, place it on the flank or behind a tired,
  engaged enemy line, and charge at the right moment — at a run, or with cavalry at a gallop. A charge into their
  **backs** throws men to the ground; archers caught with bows in their hands go over most easily.
- **Never charge a fresh shield wall head-on.** Its right-side blocks throw your sprinting men back — the charge
  bounces. Let it come to you, or tire it first, or go round it.
- **Move, then charge.** Move your shock infantry round with plain move orders: they keep most of their sprint for
  the real charge.
- **Rotate.** Pull a spent formation back, let it walk for a minute, put it back in.
- **Don't let your hero swing himself empty.** Watch your bar; step out, block on the right side, and let it
  climb before you go again.
- **Block on the right side.** Clean blocks let your bar refill while you defend, and a tired man who swings into
  your good guard reels — strike then.
- **Sprint to get out, not to get in tired.** A short dodge saves a cornered hero; a long sprint into a fight
  arrives with a bar that has not refilled.

## Settings

Everything works out of the box. If you have the **Mod Configuration Menu** (MCM, optional), Mod Options shows
two pages:

- **Trax Combat Enhancements** — the short page: the master switch, how hard hits land, how fast men tire, each AI
  system's own switch (aggression and the fever, shield walls, courage, cavalry flocking), line discipline for your units, staggers on or off and
  how much they apply to you, the sprint and its key, what the screen shows, and *Revert all to defaults*.
- **Trax Combat Enhancements - Advanced** — every other number, by system, for tuning.

Without MCM, the same settings are in a commented `config.json` in
`Documents\Mount and Blade II Bannerlord\Configs\TraxCombatEnhancements\`. Every change applies at once, even
mid-battle, and the **master switch** turns the whole mod off mid-fight — play the same battle both ways.

Every setting's name, what it moves and where it sits: [MECHANICS §16](MECHANICS.md#16-where-the-settings-live).
The full contract with every range: [DESIGN.md](DESIGN.md).

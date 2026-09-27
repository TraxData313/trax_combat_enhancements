# Design — Trax Combat Enhancements

The contract for what the mod does. Anton's ask (2026-09-27) is the source; where it was
ambiguous, the reading chosen is written down under **Interpretations** so it can be
overturned in one line. Every number here is a parameter (see **Parameters**) — nothing is
hard-coded.

Target: Mount & Blade II: Bannerlord **v1.4.8** (singleplayer). Released on **Steam Workshop only**.

---

## 1. Damage randomness

Every strike that LANDS — melee or ranged, by anyone on any side, player included — has its
final damage multiplied by a fresh random factor drawn uniformly from
`[1 − p, 1 + p]`, `p = DamageRandomPercent / 100` (default 50%).
A 50-damage hit lands for anything from 25 to 75, rolled anew on every hit.

- Applies to hits on people and (toggle) on horses. Horse-charge bumps count as melee.
- NOT randomized: blows stopped by a shield (toggle `DamageRandomOnShields`, off), fall
  damage (not a strike), hits on doors, siege engines and other objects.
- A hit the game computes as 0 stays 0. A positive hit never rounds below 1.
- Knockdown, stagger and dismount stay the game's own rules — but they read the final
  (rolled) damage, so a high roll knocks down more often than a low one. Crush-through is
  decided before damage and is unaffected.
- A tired attacker loses the lucky side of the roll (§2, `DamageBonusFollowsAthletics`): his
  range is `[1 − p, 1 + p × f]`, f = his share of the peak line left.

## 2. Athletics (the stamina bar)

A per-fighter pool — the **Athletics** bar — in every combat mission. Simpler cousin of RCM's
posture: it only ever drains by attacking. Near the top of his OWN bar a fighter is at full
strength; below that line his damage upside, swing speed and run speed fall in straight lines,
down to slow attacks and a slow run when it is empty. (Athletics v2, Anton 2026-09-27, built in
step 5c: it replaced step 5's flat 100-point pool, the cliff at 0 and the standing/moving
regen.)

- **It is called ATHLETICS, everywhere** (Anton, 2026-09-27: "so it is the athletics bar that
  gets depleted" — so every player sees at once that the Athletics skill controls all of
  it). The pool, the bar, the points: **Athletics**. The skill on the character screen:
  **Athletics skill**. Every player-facing word — MCM, config keys and comments, bars,
  messages, log tags (`[athletics]`), README, Steam page — and the code too (one
  vocabulary: `AthleticsLogic`, `AthleticsEnabled`, `FormationAthleticsStats` and so on).
  The word used to be "endurance" (renamed in step 5b); TASKS_DONE history and RESEARCH's
  findings keep their words.
- **The pool IS the Athletics skill** (Anton's pick): pool = max(`AthleticsPoolFloor` (50),
  `AthleticsPoolPerSkill` (1.0) × Athletics skill), never below 1 point. Skill 180 → the bar
  tops at 180; a 300-skill hero gets 300. The bar's number matches the skill screen: heroes
  (lords, companions, the player) use their real skill, troops the skill in their troop data.
  Riders too — Athletics, never Riding. Every fighter starts a mission full. Changing either
  number mid-battle keeps each fighter's fraction (60% stays 60%). The floor is Anton's
  experiment slider: real troops (v1.4.8 data) have Athletics 20 (recruits), 40 (tier 2),
  60 (elite cataphract), 130 (legionary), 170 (Fian champion) — without the floor a recruit
  would be empty after two swings.
- **Cost, in points**: every blow costs `CostPerBlow` (10) POINTS × multipliers, whatever the
  pool — so a bigger Athletics pool = more blows = heroes stronger:
  - heroes (lords, companions, the player): × `HeroCostMultiplier` (0.75)
  - the hero who LEADS the fighter's party (the player for their own party, a lord for his):
    × `PartyLeaderCostMultiplier` (0.75) on top → 0.75 × 0.75 × 10 = 5.6 per blow.
- **What is a blow**: a melee swing or thrust, a shot, a throw. A couched lance or braced
  spear hit has no swing, so it costs one blow when it LANDS. Kicks, shield bashes and
  siege engines (ballista, onager) cost nothing.
- **The peak zone** — the top of every fighter's OWN bar (Anton, 2026-09-27). At or above
  `AthleticsPeakPercent` (75) % of the fighter's pool the bar is GREEN and the fighter is at
  full strength: full damage upside, full swing speed, full run speed, never steps back.
  Below the line each of those falls in a straight line down to its floor at 0. With cost in
  points, bigger pools stay in the zone longer — recruit (floor 50): 2 swings at full
  strength, 5 to empty; legionary (130): 4 and 13; Fian champion (170): 5 and 17; a
  300-skill party leader (cost 5.6): 14 and 54 (53⅓ blows' worth). A fresh fighter always
  runs in and lands its first blows at full strength.
  `f = min(E / (AthleticsPeakPercent% × pool), 1)` — the share of the peak line left, E =
  current Athletics points — drives every curve below.
- **Health caps the pool** (`HealthCapsAthletics`, on): the usable pool = pool × health
  left. Pool 100 at 75% health → 75; a fighter holding 80 drops to 75 at once (at the hit),
  and regen never fills above the cap. The peak line stays measured on the FULL pool, so a
  badly wounded fighter can never climb back into full strength (at 50% health, f is at most
  0.67) — wounds make you weaker, not only shorter-winded.
- **Below the peak, three things weaken** (each 1 at f = 1, its floor at f = 0):
  - **Damage upside** (`DamageBonusFollowsAthletics`, on): the roll of §1 becomes
    `[1 − p, 1 + p × f]` with f from the ATTACKER (the rider's for a horse charge; an attacker
    Athletics does not follow keeps the full upside). In the peak zone +50%; halfway down to
    0, +25%; at 0 no upside at all — only the −50% side. The downside never changes.
  - **Attack speed** = S + (1 − S) × f, S = `ExhaustedAttackSpeedPercent` (20%): melee swings
    and thrusts, bow draw, crossbow reload, throws. Full speed in the peak zone, 20% at 0 —
    a straight line, no cliff. **Exhausted** means E = 0 (for logs and bars).
    **What "attack speed" means (Anton, 2026-09-27): the RATE of attacking, the whole cycle
    — ready/wind-up, swing, recovery, and for the AI the pause before its next attack — not
    only the swing animation.** At a multiplier of 0.5 a fighter who attacked once a second
    attacks once every two seconds. The combat itself slows down. Built in step 5e, three
    techniques on the same m (research: AI_NOTES "Step 5e"):
    - **The animations** (always, since step 5): swing, thrust / bow draw / throw and reload
      speed × m - the wind-up, the swing with its follow-through, the draw, the reload.
    - **The AI's decisions** (`AttackRateAiDecisions`, on - an A/B switch): the AI's chance to
      attack at a decision, to riposte after a parry and to loose × m, its aim before a shot ÷ m
      - so its pause between attacks grows with the rest. The player has no AI: his pause is his.
    - **The pace hold** (`AttackRatePaceHold`, on - an A/B switch): after each MELEE swing of a
      tired AI fighter on foot, he may not start his next attack (the engine's own "no attack"
      flag, guard up) until his next blow can land no sooner than his fresh cycle ÷ m after this
      one (his fresh cycle = his own time between blows at full strength, else the battle's AI
      average). Never the player, never riders, never while he steps back; ranged fighters get
      the first two techniques only.
    - **Blocking is never slowed**: weapon handling, shield speed and every defence value of the
      AI are left alone. Shared and unavoidable: a tired man's slower swing keeps him committed
      longer. Not reachable: the recoil after a blocked blow plays at the game's own speed
      (per-agent animation speed is not safe to touch - AI_NOTES "Step 5e").
    - **Measured**: the summary's `attack rate` lines give, per f band, melee and ranged, AI and
      you apart, every phase's average, the cycle, m, the target (the peak's cycle ÷ m) and
      measured ÷ target with a verdict word (on target within ±15%, too fast, too slow), the pace
      holds, and whether tired men block as often as fresh ones.
  - **PAUSE ONLY — Anton's playtest call (2026-09-27), supersedes the animation technique
    above (built in step 13).** In game the animation slow-down read as "slow-mo" and felt
    strange. So: every attack animation (wind-up, swing, thrust, draw, throw, reload) plays at
    FULL speed, always; the whole slow-down is a **no-attack timer** after each attack. After
    an attack of duration D (measured, this attack's own wind-up + release), the fighter may
    not start another attack for D × (1/m − 1) — so the rate is exactly × m (m 0.5: pause = D,
    one attack every 2 s instead of 1 s; m 0.2 at empty: pause = 4 × D). Blocking, parrying,
    moving are never held. Applies to the PLAYER (his attack input does nothing until the
    timer runs out; holding the button starts the attack the moment it ends) and to the AI
    (the pace hold, now melee AND ranged, carries the whole slow-down; the AI-decision scaling
    stays an A/B switch, off if it double-counts). The player sees it: a **countdown**
    ("1.3 s") beside his Athletics bar while the timer runs, and the **bar flashes** when he
    tries to attack before it ends. A slider keeps a little slow-mo possible
    (`AttackAnimationMinPercent`, default 100 = none) for anyone who wants it back.
  - **Run speed on foot** = M + (1 − M) × f, M = `MinMoveSpeedMultiplier` (0.3). Tired men
    slow down, so fresher men overtake them. Horses keep their speed (Anton's pick):
    `MountMinSpeedMultiplier` (1.0 = unaffected; lower it to let a tired rider's horse slow on
    the same curve).
- **Regeneration** starts after `RegenDelayBlowTimes` (2) × `BlowTimeSeconds` (1.5 s) with no
  attack, and follows EFFORT = speed ÷ the fighter's current top speed (the horse's for
  riders). Standing or walking — effort up to `WalkEffortFraction` (0.4) — refills from empty
  to full in `FullRegenSecondsStanding` (60 s). Faster than a walk the rate falls in a
  straight line to × `RegenMultiplierAtFullRun` (0.5) at top speed (120 s from empty to full
  at a flat-out run). Never above the health cap. Any new blow stops regeneration and
  restarts the delay. (0.4 because the game walks people at 1.8 m/s and their top speed on
  foot works out at about 4–5 m/s — RESEARCH §D. "Current" top speed: a tired man's top is
  lower, so keeping up with a walking formation is harder work for him.)
- **Cavalry**: riders use the very same pool (their Athletics skill). Riding never drains
  it; only blows do. The horse has no Athletics of its own.
- **Tired fighters step back** (built in step 5d - the literal rule; `StepBackEnabled`, on):
  when a MELEE swing ends, an AI fighter on foot may step back, facing its enemy with its guard
  up. Chance = `StepBackMaxChancePercent` (100) × (1 − f), f after the swing's cost: 0% in the
  peak zone (no roll at all), 50% halfway down to 0, every swing at 0. He walks
  `StepBackDistance` (2 m) straight away from the enemy he fights, facing him, making no swings
  (`StepBackHoldAttacks`), for `StepBackSeconds` (1.5 s, read live) - a tired man walks slowly,
  so he may not get all the way - then his formation takes him back. Never the player, never
  riders, never after ranged attacks, kicks or bashes. The point: the tired fall back and the
  fresh take the blows.
  - **How**: the engine's own scripted movement (`SetScriptedPositionAndDirection`, released by
    `DisableScriptedMovement`) - how vanilla sends a soldier out of his formation to pick up
    arrows mid-battle. The formation keeps his place and takes him back. Research, the
    alternatives weighed (RBM's formation patch, the AI's behaviour values) and the fallback:
    AI_NOTES "Step 5d".
  - **Only where it is safe** (Claude's calls, Anton can overturn): field battles only - not
    tournaments, arena fights, duels or naval battles; not while the formation stands in a
    shield wall, square or circle (they exist to hold) or is ordered to retreat; not for men the
    game is using (ladders, siege towers and engines, picking something up, routing); only with
    the enemy he fights within `StepBackEnemyRange` (4 m); only to a level spot on the navmesh
    with a straight way back (no wall edges, stairs, fences); at most `StepBackMaxAtOnce` (50) at
    once on the whole field.
  - **Always ends**: his time is up; he falls or leaves; his formation gets a new order or
    arrangement; he mounts, routs, changes formation, or the player takes him; the battle ends;
    or the step back (Athletics, the whole mod) is switched off - then everyone stepping back
    walks back at once.
  - If the playtest shows men turning their backs, the nearest thing that works is "hang back":
    tired men hold their place instead of pressing, through the AI's own behaviour values - no
    scripted movement (AI_NOTES "Step 5d").

## 2b. Athletics v2 (folded into §2)

Anton's additions of 2026-09-27 were written here while step 5 built the first version; step
5c built them and folded them into §2, which is now the one current spec. The step-back rule
(built in step 5d) is §2's last bullet.

## 2c. Defaults file (built in step 5b)

`defaults.json` at the repo root holds EVERY parameter's default, one key each, with the
plain-words explanation above it — the one place Anton tunes defaults, then pushes. **It is
the single truth for default values**: the build embeds it in `TraxCombat.Core.dll` and the
settings schema reads each default from it while it is built (the schema declares types,
ranges, groups and wording — no default values). So the first-run config file, a key missing
from config.json, MCM's Default preset (its Reset buttons), the "Revert all to defaults"
button and the "(default …)" in comments, MCM hints and the log all show the file's values.

- **MCM, group "Defaults"**: **Revert all to defaults** puts every setting back to its
  defaults.json value at once — live, even mid-battle; each change is logged
  `(source: defaults)` and config.json is rewritten. **Save current values as a defaults
  file** writes the values in play as `defaults.json`, comments and all, next to config.json
  (the path is logged and shown on screen) — copy it over the repo's to make a tuning found in
  game the new defaults.
- **Without MCM**: delete a key's line in config.json (it takes its default at the next battle
  start) or delete the file (everything does); the config file's header says so.
- **The Parameters table's Default column is the INITIAL value** — what the setting started
  with. It is NOT compared with the code or with defaults.json: Anton tunes defaults.json and
  never has to edit this document for it. The table must still list exactly the schema's keys,
  each with a value of the right type (the schema test).
- **Checks** (DefaultsFileTests): every schema key is in defaults.json and nothing else; each
  value has the right type (true/false, a whole number, a number) and lies inside its range;
  the // lines are exactly what the mod writes; the build embedded that very file. When a
  setting's wording, range or place changes, `dotnet run --project tools/DefaultsTool --
  refresh` rewrites every comment and keeps every value. The unit tests run on this table's
  INITIAL values, so a tuned default never breaks them; the offline smoke checks that the real
  DLL read every default from the embedded file.
- **A new setting**: its row in the table below, its schema entry, `"Key": value` anywhere in
  defaults.json, then the refresh command puts it in place with its comments.

## 3. Showing Athletics

All toggles, all on by default. **Built: items 1 and 4. Items 2 and 3 are LATER** (Anton moved
them off the build order, 2026-09-27): their designs stay here, their settings wait in "Planned
parameters" below, and nothing of them is in the game, MCM or the config file.

1. **Player bar** (`ShowPlayerBar`): the player's Athletics bar near the vanilla health bar,
   in the spirit of RBM's posture bar (RBM = Realistic Battle Mod, confirmed by Anton —
   style reference only). Built in step 6.
2. **LATER (step 7) — Looked-at NPC** (`ShowTargetBar`): a small bar for the fighter the player
   is aiming at / looking at, within `TargetBarMaxDistance`; it lingers `TargetBarLingerSeconds`
   after the aim leaves so it does not flicker. Aiming at a horse shows its rider.
3. **LATER (step 8) — Squad bars** (`ShowFormationBars`): above each of the PLAYER'S formations,
   a bar of the formation's average Athletics, with a band of ± `FormationSpreadStdDevs` (1)
   standard deviations (`ShowFormationSpread`). `FormationBarsAlways` off = only while vanilla
   shows its formation markers (marker key held or orders menu open).
4. **Orders menu** (`ShowInOrderMenu`): while the orders menu is open, a strip directly UNDER
   each of vanilla's formation cards (built in step 9 — below). Numbers INSIDE vanilla's cards
   would need UIExtenderEx and risk clashing with RTS Camera Command System — not worth it
   (Claude's call 2026-09-27, see RESEARCH implication 1).

**The orders-menu strip as built (step 9):** one cell under each card the game draws, as wide as
the card. Just under the card, left: the men's average Athletics as a share of their own pools
and its spread, `72% ± 8` (the ± is `FormationSpreadStdDevs` standard deviations, in points of
the bar); right: their average health left, `HP 81%` (`ShowFormationHealth`; the game's font has
no heart). The middle stays free for the game's own order icons, which hang 20 px under a card.
Under those, a slim bar (`OrderStripBarHeight`, 4 px): the average fill coloured by the men's
average f with the player bar's colours (green … red), a lighter band from mean − k·σ to
mean + k·σ (`ShowFormationSpread`), a thin tick at the peak line. The player himself is not
counted (the cards count the men under his command; he has his own bar). Refreshed every
`FormationStatsRefreshSeconds`.
- **How it lines up** (no UIExtenderEx, nothing patched): the game's own cards are READ live —
  the order layer's widget tree is walked once per open, and every frame each drawn card's
  screen position and size are read and the cell is placed on its bottom edge in pixels. So it
  fits any resolution, UI scale, the keyboard columns and the gamepad row, and RTS Camera
  Command System's reworked cards (its columns run bottom to top; the same reader works). Card
  slot k is formation k (Infantry, Archers, Cavalry, …); each card's own man-count confirms it
  every frame. A cell that would leave the screen is lifted to its edge.
- **Fallback — a compact panel** at the top centre (`OrderPanelOffsetTop`, `OrderPanelWidth`), one
  row per formation ("1 Infantry  72% ± 8  HP 81%" and the same bar), when the cards cannot be
  trusted for that open (none found, not whole sets of 8, two sets at once, none drawn 0.5 s
  after the open, a card disagreeing with its formation for 1 s) or `OrderStripUnderCards` is off.
  The next open tries the cards again. Every fallback is logged with its reason.
- Five Advanced settings place the cell (`OrderStripTextSize`, `OrderStripTextOffset`,
  `OrderStripBarOffset`, `OrderStripBarHeight`, `OrderStripSideMargin`), all live. The defaults
  make the cell 23 px deep, which fits the bottom card of a column at 1920 × 1080 exactly.

Bars show in fights (battle, duel, tournament and stealth modes — step 6 added stealth:
a stealth mission's fights cost Athletics too), with the player on the field, never while the
game's "hide battle UI" or photo mode is on, and never while the master switch or Athletics is
off. Every one of these is read live, every frame (a switch flipped mid-battle takes the bar
away or brings it back at once).

**Outside a battle — the player bar only (step 12, Anton's playtest: the training field showed no
bar, because it runs in the game's walk-about mode, not a battle mode).** With
`ShowPlayerBarOutsideBattles` on, the player bar also shows in the walk-about mode (the game's
`StartUp`: the training field, towns, villages, a lord's hall, arena practice) — with the player on
the field and his Athletics tracked — while he **holds a weapon or a shield** (anything wielded in
either hand; fists only do not count) **or his Athletics is below full** (below the top it can
refill to — a wound's cap counts as full), so it stays up while it refills; one second after both
end it goes (a short grace, so a weapon switch — both hands empty for a moment — does not flicker
it). Never in a conversation, barter, deployment, cutscene or replay. The orders-menu strip keeps
the fights-only rule. The log names the reason at each appearance and removal outside a battle
("outside a battle: a weapon drawn", "… no weapon drawn and your Athletics full"), and the
summary's `hud:` line ends with "outside a battle: shown Nx (weapon drawn N, refilling N), on
screen N s".

**The player bar as built (step 6):** bottom right, one row under the vanilla health bar (and
the horse bar), its right end lined up with the health bar's fill: the word *Athletics*, the
number `current / pool` (current rounded UP, so it reads 0 only when truly empty), and a slim
bar (`PlayerBarWidth` × `PlayerBarHeight`, placed by `PlayerBarOffsetRight` /
`PlayerBarOffsetBottom` — UI pixels of the 1080p layout, the game's UI scale applies). Inside
the bar: the fill in the colour of f, a white marker at the peak line, the part the wounds hold
dark red-brown (from the usable share to the end — `HealthCapsAthletics`), the rest dark grey.
Empty (E = 0): the word reads *Exhausted* and the word, the number and the bar's frame turn
red. Refreshed every `HudRefreshSeconds`.

**Additions (Anton, 2026-09-27):**
- The player bar (and, LATER, the target bar) shows the Athletics NUMBER (current / pool) and a
  marker at the peak line (`AthleticsPeakPercent`, 75% of the pool). Fill colour by f (the share
  of the peak line left): in the peak zone green; below the line blue; f at or below
  `BarYellowBelowPercent` (75) % yellow; `BarOrangeBelowPercent` (50) orange;
  `BarRedBelowPercent` (25) red. The orders-menu strip uses the same colours.
- Squads also show their AVERAGE HEALTH (`ShowFormationHealth`, on) — in the orders menu (built,
  step 9) and, LATER (step 8), above the formation.
- Orders menu: our strip sits directly UNDER the vanilla formation cards, one cell per card
  ("below the arrows remaining", Anton's words) — Athletics average ± spread and average
  health. Still no UIExtenderEx; if the cards' positions cannot be matched reliably, the
  step falls back to a compact panel and says so. (Built in step 9 — item 4 above.)

## 4. Configuration

Every parameter lives in two places that stay in sync:

- a config file created on first run under
  `Documents\Mount and Blade II Bannerlord\Configs\TraxCombatEnhancements\` — hand-editable,
  with a plain-words explanation beside every value;
- the **Mod Configuration Menu (MCM)** when installed. MCM must stay OPTIONAL: without it the
  mod runs on the file alone (see CLAUDE.md, hard requirements).

**What the player reads** (step 10b): nine groups, the same in MCM and the file, in this order —
*Master switch, Damage randomness, Athletics, Tired fighters, Tired fighters step back, Refill,
Your Athletics bar, Orders menu strip, Advanced*; MCM shows its *Defaults* buttons (§2c) just above
*Advanced*, which stays last. One vocabulary: the pool, the bar and its points are *Athletics*; the
character-screen skill is *the Athletics skill*; the *peak line* is the white mark on the bar
(`AthleticsPeakPercent`) — at or above it a fighter is at full strength; *empty* = 0 Athletics.
Every label carries its unit — (points) (%) (x) (m) (s) (px) (MB). Every MCM hint ends
"Applies at once, even mid-battle." and the default; the config file's header says a hand edit
applies at the next battle start.

**Master switch** (`ModEnabled`, Anton 2026-09-27, built in step 5b): one checkbox at the top
of MCM (and the first key of the config file) turns the WHOLE mod off — live, even mid-battle —
so the same battle can be fought with and without it and compared. Off: the damage roll hands
back the game's own number; nobody pays Athletics, nobody refills; every speed penalty (attack,
run, horse) is lifted at once (the stat decorator checks the switch itself, so even a recompute
before the logic's next tick is vanilla - the AI's attack values included, 5e); the player bar
and the orders-menu strip (steps 6, 9) hide; tired fighters never step back (5d) and every pace
hold is lifted at once (5e).
Back on: everyone starts with a full Athletics bar — a fresh start, not a resume. The two model
decorators stay registered (they cannot be removed mid-game) and pass everything through; the
tournament AI-level fix, which only keeps vanilla behaviour intact, stays. **Logging stays on**:
the mission start line and the `[summary]` header say `mod ON` / `mod OFF`; a toggle
mid-battle is logged with its time, and the header then says "mod was on for N% of the battle
(started ON; OFF at 40.1 s, …)". The summary's vanilla-observable lines (duration, killed /
knocked out / fled, agents) are unchanged, and while off the damage lines record the game's own
hit numbers unrolled (factor 1, "damage while the mod was OFF … avg N per hit") so an ON run and
an OFF run compare line by line. Every feature checks `ModEnabled` FIRST (CLAUDE.md).

**Hot swap**: a change in MCM applies LIVE — the next hit, blow or HUD refresh uses it, even
mid-battle. Nothing ever needs a game restart; if some parameter can only apply from the
next battle, its description says so. Hand edits to the file are picked up at the next
battle start.

**When MCM writes the file** (its Done button), a hand edit made meanwhile is never lost: the
file on disk is re-read at that moment, MCM's value is written only for the settings MCM
changed, and every other setting keeps the value on disk (it takes effect at the next battle
start). Unknown keys are kept in a "not recognised" section; a file that does not parse is
saved as `config.json.broken-<time>` before a fresh one replaces it. MCM's Reset buttons
(and "Revert all to defaults") restore the defaults from `defaults.json` (§2c). Ranges live in the schema
(`src/TraxCombat.Core/SettingsSchema.cs`) and are printed beside each key in the file. The
file also carries `ConfigVersion`, a format stamp — not a setting.

**The log** (`trax_combat.log`, beside config.json; one rolling file across game starts): every
line is `yyyy.MM.dd HH:mm:ss.fff [tag] message`; a VERBOSE line (written only while
`VerboseLogging` is on, rate-limited per kind) carries a `~` before its tag:
`… ~[damage] melee on a person: …`. Past `LogMaxMegabytes` (8) the file is trimmed to about half
of it, and a trim never loses the lines the playtest is read from (step 10b, review R7):
- KEPT: every line that is not verbose — load, settings, MCM, mission start and end, every
  feature's "mission start" / "first … this battle" / "YOU …" line, the `[summary]` blocks,
  `[error]` with its stack, the `[log]` notes — and any `[load]` `[compat]` `[config]` `[mcm]`
  `[mission]` `[summary]` `[error]` line even if written verbose;
- CUT: the oldest verbose lines, at one point in time (a newer one is never cut while an older one
  stays). The trim's note, `[log] (log trimmed at … : N older verbose lines cut, up to …)`, sits at
  the top of the file.
- Only if the kept lines ALONE pass half the limit (dozens of battles over several game starts)
  do the oldest of them go too — the file stays bounded. Turning `VerboseLogging` on can never
  cost a line the log would have had with it off.

## 5. Compatibility

- **RBM (Realistic Battle Mod) is NOT compatible** (Anton, 2026-09-27): it has its own
  posture and stamina and patches the same combat. The Steam page says so plainly. If RBM
  is enabled, the mod logs `[compat]` and shows ONE message at the main menu / campaign
  start ("Trax Combat Enhancements is not compatible with Realistic Battle Mod — disable
  one of them"). It does not refuse to run.
- Other combat mods that change damage or attack speed may stack with ours; the log's
  per-battle summary is the tool to see it.
- **One copy runs** (step 11): with two copies of this mod enabled at once (the dev install and
  the Workshop release), the first to load runs and the other stands down in every hook (no log
  line, config, MCM page, model or mission logic of its own) - otherwise both damage decorators
  would roll every hit. The running copy logs `[compat]` and shows ONE message at the main menu.
- **Save-safe**: nothing is added to the campaign save (no saveable types, no campaign
  behaviours) - the mod can be added or removed mid-campaign.

## Parameters

The Default column is the INITIAL value; the shipped default is whatever `defaults.json`
says (§2c).

| Key | Default (initial) | What it does |
|---|---|---|
| `ModEnabled` | true | THE master switch. Off = the whole mod steps aside and every battle is pure vanilla (live, even mid-battle); the log still records each battle for comparison. |
| `DamageRandomEnabled` | true | Master switch for damage randomness. |
| `DamageRandomPercent` | 50 | ± spread in percent. 50 → a 50-damage hit lands for 25–75. 0 = off. |
| `DamageRandomMelee` | true | Randomize melee hits. |
| `DamageRandomRanged` | true | Randomize arrows, bolts and thrown weapons. |
| `DamageRandomOnMounts` | true | Randomize hits that land on horses too. |
| `DamageRandomOnShields` | false | Randomize the damage a shield takes when it blocks. |
| `AthleticsEnabled` | true | Switch for Athletics (the stamina bar). |
| `AthleticsPoolFloor` | 50 | Smallest possible Athletics pool (for anyone with a low Athletics skill; 0 = none, never below 1 point). |
| `AthleticsPoolPerSkill` | 1.0 | Pool per point of Athletics skill (1.0 → the bar tops at the skill). |
| `AthleticsPeakPercent` | 75 | At or above this % of their own pool a fighter is at full strength (green); below it they weaken in a straight line to 0. |
| `HealthCapsAthletics` | true | Health left caps the usable pool. |
| `CostPerBlow` | 10 | Athletics points one blow costs before multipliers (points, whatever the pool). |
| `CostOnMiss` | true | true: every attack costs, landed or not. false: only blows that land. |
| `HeroCostMultiplier` | 0.75 | Cost multiplier for heroes. |
| `PartyLeaderCostMultiplier` | 0.75 | Extra multiplier for a party's leading hero, on top of the hero one. |
| `ExhaustedAttackSpeedPercent` | 20 | Attack speed (the attack RATE) at 0 Athletics, percent of normal (a straight line up to 100% at the peak line) - since step 13 delivered by the no-attack timer D × (1/m − 1) after each attack. |
| `AttackRatePlayerTimer` | true | Step 13: your own no-attack timer - after each of your attacks below the peak line the attack button does nothing until it ends (held, it attacks the moment it ends); blocking, kicks, moving, weapon switches always work. Off: your attacks are never held. |
| `AttackRatePaceHold` | true | Step 13: the AI's no-attack timer - after each attack (melee and ranged, on foot and mounted) a tired AI fighter may not start another for D × (1/m − 1), guard up (NoAttack; A/B switch). |
| `AttackRateAiDecisions` | false | Tired AI fighters also decide to attack (and riposte) less often, loose less readily and aim longer - × / ÷ their attack speed (A/B switch). Off since step 13: on top of the timer it double-counts (the log read 128% / 172% too slow). |
| `AttackAnimationMinPercent` | 100 | Step 13: the attack animations (swing, thrust / draw / throw, reload) play at max(m, this %) - 100 = always full speed (the whole slow-down is the timer); lower brings a little slow-mo back. |
| `MinMoveSpeedMultiplier` | 0.3 | Top speed on foot at 0 Athletics. |
| `MountMinSpeedMultiplier` | 1.0 | Horse top speed at the rider's 0 Athletics (1.0 = horses never slow). |
| `DamageBonusFollowsAthletics` | true | The damage upside shrinks with the attacker's Athletics below the peak. |
| `StepBackEnabled` | true | Tired AI fighters on foot step back after melee swings (§2). Off mid-battle: everyone stepping back returns to his formation at once. |
| `StepBackMaxChancePercent` | 100 | Chance to step back at 0 Athletics (0 at the peak, straight line between). |
| `StepBackDistance` | 2.0 | Metres a fighter steps back, straight away from his enemy. |
| `StepBackSeconds` | 1.5 | Longest a step back lasts before the formation takes over again. |
| `StepBackEnemyRange` | 4.0 | Steps back only while the enemy he fights is within this many metres. |
| `StepBackHoldAttacks` | true | No swings while stepping back (guard up only). |
| `StepBackMaxAtOnce` | 50 | Most fighters stepping back at the same time, all sides together. |
| `RegenDelayBlowTimes` | 2 | Idle blows before regeneration starts. |
| `BlowTimeSeconds` | 1.5 | How long "one blow" is, for the delay above. |
| `FullRegenSecondsStanding` | 60 | Seconds from empty to full while standing still or walking. |
| `RegenMultiplierAtFullRun` | 0.5 | Regen rate at top speed, relative to standing or walking. |
| `WalkEffortFraction` | 0.4 | Up to this share of top speed counts as walking (full regen). |
| `ShowPlayerBar` | true | Player Athletics bar. |
| `ShowPlayerBarOutsideBattles` | true | Outside the fight modes (the training field, towns, villages - the game's walk-about mode) the player bar shows too, while you hold a weapon or a shield or your Athletics is below full; never in a conversation, barter, deployment or cutscene (§3, step 12). Off: fights only. |
| `ShowAttackRecoveryBar` | true | Step 13 (Anton): the Attack recovery bar just above your Athletics bar, shown with it - empties when you attack below the peak line, fills back over your no-attack timer (you cannot attack until it is full), the seconds left inside it ("1.3 s"); full and quiet at full strength. Hidden while `AttackRatePlayerTimer` is off. |
| `FlashBarOnEarlyAttack` | true | Step 13: the Attack recovery bar flashes (two quick pulses) when you press attack while it still fills. |
| `BarYellowBelowPercent` | 75 | Bar turns yellow at or below this % of the peak line (blue just below the line, green above it). |
| `BarOrangeBelowPercent` | 50 | Orange at or below this %. |
| `BarRedBelowPercent` | 25 | Red at or below this % (an empty bar is always red). |
| `ShowFormationSpread` | true | ± spread in the orders-menu strip: the "± 8" and the lighter band (off: the strip shows the average alone). |
| `FormationSpreadStdDevs` | 1.0 | Band width in standard deviations (the strip's "± 8" is this width). |
| `ShowInOrderMenu` | true | The orders-menu strip: under each formation card, the men's average Athletics ± spread (bar + "72% ± 8"). |
| `ShowFormationHealth` | true | The orders-menu strip also shows average health ("HP 81%"). |
| `OrderStripUnderCards` | true | true: the strip sits under the vanilla cards (read live) when they can be matched, else the compact panel. false: always the compact panel. |
| `HudRefreshSeconds` | 0.1 | (Advanced) How often the player bar and the orders-menu strip update. |
| `PlayerBarWidth` | 205 | (Advanced) Length of your bar in UI pixels of the 1920 × 1080 layout (the game's UI scale applies); 205 = the inside of the vanilla health bar. |
| `PlayerBarHeight` | 12 | (Advanced) Thickness of your bar, UI pixels. |
| `PlayerBarOffsetRight` | 62 | (Advanced) Screen's right edge → your bar's right end, UI pixels (62 = under the vanilla health bar). |
| `PlayerBarOffsetBottom` | 30 | (Advanced) Screen's bottom edge → your bar's row (label, number, bar), UI pixels (30 = the Attack recovery bar fits above it, both just under the health and horse bars; it was 54 until step 13). |
| `RecoveryBarWidth` | 205 | (Advanced) Length of the Attack recovery bar, UI pixels (205 = your Athletics bar's). |
| `RecoveryBarHeight` | 14 | (Advanced) Thickness of the Attack recovery bar, UI pixels (the seconds inside it need about 12). |
| `RecoveryBarOffsetAbove` | 24 | (Advanced) Your Athletics bar's row → the Attack recovery bar's row (bottom to bottom), UI pixels (24 = right on top of it; it moves with your bar). |
| `OrderStripTextSize` | 13 | (Advanced) Font size of the strip's numbers, UI pixels. |
| `OrderStripTextOffset` | 1 | (Advanced) A card's bottom edge → the top of its numbers, UI pixels (they sit left and right of the vanilla order icons). |
| `OrderStripBarOffset` | 20 | (Advanced) A card's bottom edge → the top of its strip bar, UI pixels (20 = just under the vanilla order icons). |
| `OrderStripBarHeight` | 4 | (Advanced) Thickness of the strip bar, UI pixels. |
| `OrderStripSideMargin` | 2 | (Advanced) How far the numbers and the bar keep in from a card's sides, UI pixels. |
| `OrderPanelOffsetTop` | 80 | (Advanced) Screen's top edge → the fallback panel (centred), UI pixels. |
| `OrderPanelWidth` | 300 | (Advanced) Width of the fallback panel, UI pixels. |
| `FormationStatsRefreshSeconds` | 0.25 | (Advanced) How often formation averages and spreads are recomputed. |
| `VerboseLogging` | false | Log every roll, blow and exhaustion (rate-limited) to `trax_combat.log`. Off = load, settings, mission start/end, per-battle summaries and errors only. |
| `LogMaxMegabytes` | 8 | (Advanced) Size limit of `trax_combat.log`, MB. Past it a trim cuts the oldest VERBOSE lines, down to about half; every other line is kept (§4 "The log"). |

New parameters discovered while building go into this table in the same commit.

## Planned parameters (§3 additions — not in the schema yet)

The step that builds each one moves its row into the Parameters table above (the schema
test reads that table only) and removes any retired rows in the same commit. (Step 5d moved
its four `StepBack*` rows up and added `StepBackEnemyRange`, `StepBackHoldAttacks`,
`StepBackMaxAtOnce`; step 6 moved the three `Bar*BelowPercent` rows up and added the four
`PlayerBar*` layout rows; step 9 moved `ShowFormationHealth` up and added `OrderStripUnderCards`,
the five `OrderStrip*` and the two `OrderPanel*` rows.)

The two features Anton moved to LATER (2026-09-27, §3 items 2 and 3) keep their rows here. They
were in the schema, MCM and the config file until step 10b took them out (review R21: no switch
that does nothing). The step that builds a feature moves its rows back up (and into the schema,
TraxSettings and defaults.json). The designs are §3 and AI_NOTES "Step 7" / "Step 8".

| Key | Default (initial) | What it does | When |
|---|---|---|---|
| `ShowTargetBar` | true | Bar for the fighter you look at. | LATER (step 7) |
| `TargetBarMaxDistance` | 30 | Metres — how far away a looked-at fighter still gets a bar. | LATER (step 7) |
| `TargetBarLingerSeconds` | 2 | Seconds the bar stays after your aim leaves the fighter. | LATER (step 7) |
| `ShowFormationBars` | true | Average bars above your formations. | LATER (step 8) |
| `FormationBarsAlways` | true | true: always shown. false: only while vanilla shows formation markers. | LATER (step 8) |
| `FormationBarHeight` | 3.0 | Metres above the formation's centre for its bar. | LATER (step 8) |

`ShowFormationSpread`, `FormationSpreadStdDevs` and `ShowFormationHealth` stay in the table
above: the orders-menu strip uses them now, and the squad bars will too.

Retired in step 5c (Athletics v2): `MaxAthletics` (→ the pool is the Athletics skill),
`FullRegenSecondsMoving` and `MovingSpeedThreshold` (→ regen by effort),
`ExhaustedRecoverPercent` (→ the gradual curve has no recovery line).

## Interpretations (Anton can overturn any of these)

1. **"A blow" = an attack the fighter makes** — swing, thrust, shot, throw — landed or not.
   Anton's damage rule says "each time it lands"; the Athletics rule does not, so every
   attack costs. `CostOnMiss = false` switches to landed-only.
2. **Hero multiplier is 0.75.** The ask said 0.75 and also "0.5, 10 → 5"; the leader
   formula 0.75 × 0.75 × 10 settles it at 0.75. One number to change if 0.5 was meant.
3. **"Party leader" = the hero leading the fighter's own party** — the player, and AI lords
   for their own parties. Stacks with the hero multiplier.
4. **Blocking, running and riding cost nothing.** Only blows drain. (Blocking: confirmed by
   Anton, 2026-09-27.)
5. **Weakening is gradual below the peak line** (Anton's pick for Athletics v2, step 5c) — it
   replaced the first ask's cliff at 0. "Exhausted" still names E = 0.
6. **Applies in every combat mission** (field battles, sieges, hideouts, custom battles,
   tournaments and arena). Per-mission toggles only if playtest asks for them.
7. **Custom battle has no parties**: there the "party leader" is the side's general, or —
   when a side has none — every hero on that side.
8. **Kicks and shield bashes are free**; a couched-lance / braced-spear hit costs one blow
   when it lands; siege engines are free.
9. **Shield damage is not randomized** by default; horse-charge bumps follow the melee
   toggle; fall damage and hits on objects never roll.
10. **RBM is declared incompatible** (Anton confirmed, 2026-09-27) — see §5.
11. **Step backs happen in field battles only** (step 5d, Claude's call): not in tournaments,
    arena fights, duels or naval battles (nobody there to step in; moving decks), not from a
    shield wall, square or circle, and the roll comes when the swing ENDS ("after each swing"),
    so the swing always completes first. Two numbers the spec did not have became settings:
    `StepBackEnemyRange` (only with the enemy close) and `StepBackMaxAtOnce` (a cap, so a whole
    front line never steps back together); `StepBackHoldAttacks` makes "guard up" a switch.
12. **The player bar's details** (step 6, Claude's calls): it sits UNDER the health bar (RBM's
    place for its bars too), with a label *Athletics* so a new player knows what it is; the
    colour thresholds apply in order of alarm (if they are set out of order, the most alarming
    band that applies wins; an empty bar is always red); the colours themselves are fixed in the
    code (MCM has no colour picker) - `BarMath` in Core, one place to change; empty = red text
    and frame rather than a pulse (a pulse at a 0.1 s refresh would stutter); stealth missions
    count as fights. The bar's size and place became four Advanced settings (`PlayerBar*`).

13. **The attack rate** (step 5e, Claude's calls): the AI's pause follows m through the AI's own
    attack values AND an exact hold after each melee swing - two techniques because the native
    meaning of the AI values cannot be seen, each an A/B switch so the playtest can keep the one
    that reads on target; the hold is melee-only and never for the player or riders; the AI's
    "hold a readied blow" time is NOT lengthened (a raised weapon means a lowered guard - tired
    men must not defend worse); cycles with a step back in them are left out of the measurement
    (that pause is the step back's).

14. **The orders-menu strip** (step 9, Claude's calls): the numbers are SHARES of each man's own
    bar ("72%"), not points (pools differ per man since 5c); the "± 8" is the band's half-width
    (`FormationSpreadStdDevs` × σ, so number and band agree) and `ShowFormationSpread` switches both
    off; health reads "HP 81%" (the game's font has no ♥); the squad numbers leave the player out
    (the cards count the men under his command — his own bar shows him), which also changes the
    summary's "your formations" line; the numbers sit left and right of vanilla's order icons and
    the bar under them; the fallback panel sits at the top centre; `OrderStripUnderCards` lets
    Anton force the panel if the strip ever looks wrong.

15. **The log's trim and the LATER settings** (step 10b; review R7 - the manager's decision, R21):
    a trim never cuts a non-verbose line (the `~` mark tells them apart), so turning
    `VerboseLogging` on costs nothing the playtest reads, and the cap became `LogMaxMegabytes` (8);
    the six settings of the two LATER features left the schema (no switch that does nothing) and
    wait in "Planned parameters"; the Defaults buttons moved above Advanced.

Decisions 7–10 and the new parameters `DamageRandomOnShields`, `ExhaustedRecoverPercent`
(retired in 5c), `TargetBarMaxDistance` and `TargetBarLingerSeconds` (LATER, step 7), `FormationBarsAlways` (LATER, step 8),
`HudRefreshSeconds`, `FormationStatsRefreshSeconds` came out of step 2's research
(`docs/RESEARCH.md`, "Design implications"), settled by Claude while Anton was away
(2026-09-27) — every one is a default he can overturn.

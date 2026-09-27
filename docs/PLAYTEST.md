# Playtest script

Anton tests everything in one session at the end. Each build step adds its checks here:
what to do, what you should see, and which lines in `trax_combat.log` prove it.

Log file: `Documents\Mount and Blade II Bannerlord\Configs\TraxCombatEnhancements\trax_combat.log`
Turn on `VerboseLogging` (MCM or config file) for the first run — it records every roll and
blow, rate-limited.

If anything looks wrong: note the time, finish or leave the battle, and hand Claude the log.
Every battle ends with a `[summary]` block — that alone answers most questions.

---

## 0. Before the first battle
- Enable only **Trax Combat Enhancements (dev)** (plus MCM if you want the menu).
- Start or load a campaign.
- *Mod enabled* (the master switch, top of the MCM page) stays ON for everything except
  section 4, which compares a battle with and without the mod.

Every log line looks like `2026.09.30 20:15:02.117 [tag] message`. The tags: `[load]`
`[compat]` `[config]` `[mcm]` `[mission]` `[summary]` `[damage]` `[speed]` `[athletics]`
`[hud]` `[error]` `[log]`. Search the file for a tag to follow one area.

---

## 1. Loading, config, MCM, log

This section checks the frame only — loading, settings, the log. Check it before the
features (damage randomness, section 2, is already active in every fight).

**1a. The mod loads — with MCM.** Enable MCM, start the game.
- You see: at the main menu, a line *"Trax Combat Enhancements 0.1.0… loaded - settings in
  Mod Options."* No error dialog.
- Log:
  - `[load] ==================== Trax Combat Enhancements 0.1.0…` (the version; the part after
    `+` is the git commit it was built from)
  - `[load] dll: …\Modules\TraxCombatEnhancements.Dev\bin\Win64_Shipping_Client\TraxCombatEnhancements.dll (built …)`
    — proves the dev copy is the one running
  - `[load] game: v1.4.8.…` and `[load] modules (N): …` (every enabled module, in load order)
  - `[compat] Realistic Battle Mod (RBM) not enabled - good`
  - `[mcm] settings page registered at main menu (attempt 1): MCM 5.…, page "Trax Combat Enhancements", 33 settings in 8 groups, …, Default preset = the mod's defaults (defaults.json); group "Defaults": buttons "Revert all to defaults" and "Save current values as a defaults file".`
    (`at retry (attempt N)` is fine too — MCM was just slow to wake)
  - `[config] defaults: defaults.json (embedded in TraxCombat.Core.dll), 33 keys for 33 settings - every default read from it`
    — any `defaults.json PROBLEM:` line under it → tell Claude (that setting runs on a fallback).

**1b. The mod loads — WITHOUT MCM.** Quit, disable *Mod Configuration Menu v5* in the launcher
(Harmony / ButterLib / UIExtenderEx may stay on), start again. Then turn MCM back on.
- You see: the game starts normally; the line now ends *"- settings in config.json."*
- Log: `[mcm] MCM (Mod Configuration Menu) is not loaded - no settings page; the mod runs on config.json alone. That is fine.`
- This is the check that matters most: a mod that hard-needs MCM refuses to start for every
  player without it (the sibling mod's expensive lesson).

**1c. The config file, with explanations.** Open
`Documents\Mount and Blade II Bannerlord\Configs\TraxCombatEnhancements\config.json`.
- You see: a header saying where the file lives, how edits apply and how to get the defaults
  back (delete a key's line, delete the file, or MCM's *Revert all to defaults*), then 8
  sections (Master switch, Damage randomness, Athletics, Exhaustion, Regeneration, Bars - you
  and your target, Bars - your squads, Advanced) and above EVERY key a `//` explanation in
  plain words ending `(default …, range … to …)`. The defaults are the ones in the mod's
  `defaults.json` (section 5).
- Log, first run only: `[config] first run: created config.json with every default and a plain-words explanation beside each value`
- Log, every start: `[config] settings in effect (33, version 0):` followed by 33 lines like
  `[config]   DamageRandomPercent = 50` — a value you changed shows `(default 50)` after it.

**1d. The MCM page.** Main menu → Options → Mod Options → *Trax Combat Enhancements*.
- You see: the same 8 groups, 33 settings — *Master switch* (Mod enabled) on top — checkboxes
  for the on/off ones, sliders for the numbers (the slider ends are the ranges from the file),
  and at the bottom a *Defaults* group with two buttons, *Revert all to defaults* and *Save
  current values as a defaults file* (section 5). Hovering a setting shows its explanation and
  default. The preset list has *Default*; the page's Reset puts every value back to the mod's
  defaults (defaults.json — not to what was loaded).

**1e. A change in MCM applies at once — even mid-battle.** In a battle, Escape → Options →
Mod Options → move *Spread (± %)* from 50 to 40, press Done.
- Log, the moment the slider moves (dragging logs each step; Cancel logs the way back):
  `[config] DamageRandomPercent: 50 → 40 (source: MCM)`
- Log, on Done: `[mcm] Done pressed - writing config.json` then
  `[config] wrote config.json (MCM Done): from MCM DamageRandomPercent`
- The file now says `"DamageRandomPercent": 40`. No restart asked for, ever.

**1f. A hand edit is picked up at the next battle.** Alt-Tab, change `"MaxAthletics": 100` to
`150` in config.json, save, start (or enter) the next battle.
- Log: `[config] MaxAthletics: 100 → 150 (source: file)` and
  `[config] config.json re-read at mission start: 1 change(s), settings version N`
- Every mission start writes `[config] config.json re-read at mission start: no changes`
  when nothing changed.
- **The rewrite rule** (optional, 1 minute): hand-edit one value, then — BEFORE the next
  battle — change a DIFFERENT value in MCM and press Done. The hand edit must still be in the
  file. Log: `… kept hand edit(s) from the file that apply at the next battle start: MaxAthletics = 150 (now 100)`.
- Mistakes are safe: a typo'd key → `[config] file problem: "MaxAthletic" is not a setting of this version - ignored (typo?)`
  (kept in the file under "Not recognised"); a bad number → `[config] could not read config.json at mission start (line …)`
  and the values stay as they were.

**1g. Mission start and end, and the summary.** Fight any battle to the end (or retreat).
- Log, at the start:
  - `[config] config.json re-read at mission start: …`
  - `[mission] attached: AthleticsLogic (views arrive with steps 6-9)`
  - `[mission] start: scene <scene id>, field battle, mode …, combat type Combat, game Campaign, agents so far …, mod ON`
  - `[mission] first tick: N agents active, …` and `[mission] deployment finished: N agents active, mode Battle`
- Log, at the end — the `[summary]` block:
  - `[summary] ==== scene …, field battle - ended (mission end) after N s, result: player victory, mod ON ====`
  - `[summary] agents built: N (N people incl. N heroes, N mounts)`
  - `[summary] people removed: N killed, N knocked out, N fled, N other`
  - `[summary] still on the field: …`
  - `[summary] errors logged during this mission: 0`
  - `[summary] ==== end of summary ====`
- Every mission gets these lines — towns, taverns and arenas too (then "other mission").

**1h. The two hooks are in place.** Once per game start/load:
- `[damage] damage model decorator registered over SandBox.GameComponents.SandboxAgentApplyDamageModel - damage randomness rolls on its result (ApplyGeneralDamageModifiers)`
  — with War Sails on, over `NavalDLC.GameComponents.NavalAgentApplyDamageModel`; in a custom
  battle over `TaleWorlds.MountAndBlade.CustomAgentApplyDamageModel`.
- `[speed] agent stat model decorator registered over … - scales swing / thrust-and-draw / reload speed by each fighter's Athletics multiplier; tournament AI-level fix active over N base model(s)`
- In a tournament, when a round starts: `[speed] tournament AI level multiplier 1 → 1.33 passed on to N base stat model(s)`
  (the opponents get tougher each round, as in vanilla — our hook must not swallow that).

**1i. Errors and RBM.**
- There should be NO `[error]` lines. If one appears, the game shows a red line *"Trax Combat
  Enhancements: an error was caught and logged…"*, the mod keeps going (vanilla behaviour for
  that hook), and the stack is in the log — note the time and send the log.
- If RBM is enabled you see a yellow line *"Trax Combat Enhancements is not compatible with
  Realistic Battle Mod - disable one of them."* and the log says
  `[compat] Realistic Battle Mod is ENABLED (…) - NOT compatible…`. Test with RBM off.

---

## 2. Damage randomness

Every hit that lands — yours, your troops', the enemy's — deals a fresh random share of its
normal damage: by default anything from 50% to 150%. How it works: the game computes a hit's
damage as usual (weapon, swing speed, skill, perks, armour), and just before it applies the
number we multiply it by a dice roll. A hit the game makes 0 stays 0; a real hit never drops
below 1. Knockdown, stagger and dismount are still the game's own rules, but they read the
rolled number — a big roll knocks down more often.

Set up once: `VerboseLogging` on (MCM → Advanced → *Verbose log*), and in the game's Options →
Gameplay, *Report Damage* on (the default) so you see "Delivered N damage" for your hits.
A **custom battle** against a weak side (looters, or a small recruit army) is the easiest
ground for all of this.

**2a. The hook is live.** Start the battle, let anyone land a hit.
- Log, at the start:
  `[damage] mission start: damage randomness ON, spread ±50% (a 50-damage hit lands for 25-75), melee on, ranged on, on mounts on, on shields off - read live on every hit`
- Log, at the first hit anybody lands (always written, verbose or not):
  `[damage] first roll this mission - on the main thread: melee on a person: Imperial Recruit → Looter, Pitchfork, 18 → 23 (x1.26)`
  — proves the game calls our hook. "on the main thread" settles an open question; if it
  says `NOT the main thread`, tell Claude (it is still safe, the dice are per thread).
- In a custom battle the registration line says `… registered over TaleWorlds.MountAndBlade.CustomAgentApplyDamageModel …`.

**2b. Same blow, different numbers.** Strike looters the same way ~10 times — e.g. overhead
swings to the body, standing still, same weapon.
- You see: "Delivered N cut damage." jumping around between near-identical hits (a
  34-damage blow lands anywhere from 17 to 51). The combat detail often adds "Extra damage
  from skills, perks and effects: N" or "Reduced damage …" — that is mostly our roll.
- Log (verbose), one line per hit:
  `[damage] melee on a person: <your name> (you) → Looter, <weapon>, 34 → 41 (x1.21)` —
  game's number → the number you saw, and the factor, always between x0.50 and x1.50.

**2c. Shoot.** Bow, crossbow or javelins at the enemy.
- Log: `[damage] ranged on a person: <your name> (you) → Looter, <the arrows / bolts / javelin>, 27 → 19 (x0.70)`
  (for a missile the "weapon" is the missile item).

**2d. Hit a horse — and ride into people.** Strike or shoot an enemy's horse (not the rider),
then charge infantry on horseback at speed.
- You see: "Delivered N damage to mount." varying like the rest.
- Log: `[damage] melee on a mount: <your name> (you) → horse <horse> of <rider>, <weapon>, 30 → 22 (x0.73)`
- Charge bump (counts as melee): `[damage] horse charge on a person: horse <your horse> of <your name> (you) → Looter, charge bump, 12 → 15 (x1.25)`

**2e. Hit a shield.** Strike (or shoot) an enemy who holds his shield up.
- Default — shield damage is NOT rolled. Log:
  `[damage] not rolled - shield blocks (DamageRandomOnShields off): melee on a shield: <your name> (you) → Looter, <weapon>, 18`
- Optional: MCM → *Randomize shield damage* on, hit shields again →
  `[damage] melee on a shield: … 18 → 24 (x1.33)`. The line proves the roll; the shield's hit
  points are then cut inside the engine from that number (not checkable offline — a shield
  breaking after fewer or more blows than usual confirms it). Turn it back off.

**2f. Spread to 0 mid-battle.** Escape → Options → Mod Options → Trax Combat Enhancements →
*Spread (± %)* → 0 → Done → back to the fight → the same blow a few times.
- You see: the same blow now delivers the same number every time (vanilla).
- Log: `[config] DamageRandomPercent: 50 → 0 (source: MCM)` (dragging logs each step), then
  per hit `[damage] not rolled - spread 0 (DamageRandomPercent): melee on a person: …`.
- Put it back to 50: the VERY NEXT hit rolls again (`[damage] melee on a person: … (x…)`).

**2g. Ranged off mid-battle.** MCM → *Randomize ranged hits* off → shoot, then hit in melee.
- Log: `[config] DamageRandomRanged: true → false (source: MCM)`; arrows
  `[damage] not rolled - ranged (DamageRandomRanged off): ranged on a person: …`; melee hits
  still roll. Turn it back on.
- The other switches work the same way (optional): *Randomize melee hits* →
  `not rolled - melee (DamageRandomMelee off)` (charge bumps too); *Randomize hits on horses* →
  `not rolled - on mounts (DamageRandomOnMounts off)`; the master *Damage randomness* →
  `not rolled - switched off (DamageRandomEnabled)`.

**2h. Never rolled: falls and objects** (optional, in a siege). Jump off a wall; hit a gate
or a siege engine with an axe.
- Log: `[damage] not rolled - fall damage: fall on a person: … → <your name> (you), no weapon, 12`
  and `[damage] not rolled - objects (doors, siege engines, ships): melee on an object: <your name> (you) → (object), <weapon>, 30`.

**2i. The summary.** End the battle (win, lose or retreat). The `[summary]` block now has:
```
[summary] damage rolls: 812 hits (melee 650, ranged 120, mounts 40, shields 2); factor min 0.50 / avg 1.002 / max 1.50; damage 21934 → 22011 (+0.4%), avg 27.1 per hit
[summary] damage by kind: melee 650 x0.50..1.50 avg 1.004 (17000 → 17100) | ranged 120 x0.50..1.49 avg 0.996 (… → …) | mounts … | shields …
[summary] damage dice, 10 equal slices from the lowest to the highest possible roll (even = fair): 81 79 85 80 77 84 83 80 79 84
[summary] damage not rolled: 37 hits - fall damage 3, spread 0 (DamageRandomPercent) 4, shield blocks (DamageRandomOnShields off) 30
[summary] damage roll errors: none
[summary] damage rolls ran on the main thread: all 812
```
- What proves what: **min ≥ 0.50 and max ≤ 1.50** → the bounds hold; **avg ≈ 1.00** and
  damage before ≈ after → fair on average (a small fight wobbles a few percent); the **dice
  slices** roughly even (clearer in a big battle); every switch you flipped shows up as its
  own reason under **not rolled**; **errors: none**.
- In a big battle expect `[log] N more verbose [damage] lines were suppressed by the rate limit`
  — that is the flood guard, not a problem.
- If `damage roll errors` is not "none": the game kept its own damage for those hits, the
  first one per mission is an `[error] damage.roll: …` block with its stack above — send the log.

---

## 3. Athletics (the stamina bar)

Every fighter — you, your men, the enemy — has an Athletics bar (100 points; from step 5c its
size comes from his Athletics skill). Every attack costs
some: a swing or thrust when it starts, a shot or throw when it leaves the hand, a couched
lance or braced spear when it hits. Kicks, shield bashes, blocking, running and riding are
free. At 0 the fighter is **exhausted**: his attacks run at 20% speed (wind-up, strike, bow
draw, crossbow reload, throws — the game's own "swing speed" numbers, times 0.2). About 3 s
after his last attack the pool starts to refill — full in 60 s standing, 120 s moving — and
the moment it is above 0 the speed is back. Costs: a common soldier 10 a blow (10 blows), a
hero (lord, companion) 7.5, the hero who leads his own party (you, a lord) 5.6 (18 blows).

Set up once: `VerboseLogging` on. A **custom battle** (you are the general there, so the
leader price) against looters or recruits is the easiest ground; one campaign fight with a
companion in your party covers the hero price. No bar yet (steps 6-9) — the log is the
readout.

**3a. The engine is on.** Start any battle.
- Log, at the start:
  - `[athletics] mission start: ON - pool 100, cost per blow 10.0 / hero 7.5 / party leader 5.6, misses cost: yes, exhausted attacks at 20% (recover above 0%), refill after 3.0 s rest: full in 60 s standing / 120 s moving (above 0.5 m/s) - read live`
  - `[athletics] party-leader rule: campaign - …` (or `no campaign (custom battle) - the side's general, or every hero of a side without one`)
  - `[speed] stat model on top in this mission: ours, over <the game's model> - the attack-speed penalty is applied on every recompute`
    — **if it says `WARNING: … not ours`**, another mod took the slot: tell Claude.
  - `[athletics] party leader: <your name> - pays x0.56 per blow (5.6 now)` — one line per
    leader: you, and each enemy lord leading his party (custom battle: the enemy's hero).
  - `[athletics] first tick: tracking N fighters`

**3b. Swing until empty — feel the 20%.** Swing continuously (at the air or at looters, it
makes no difference: misses cost too). As party leader you last 18 swings.
- You see: after the 18th swing everything you do with the weapon is very slow — a wind-up
  and strike take about five times as long. Blocking and moving are normal.
- Log (always): `[athletics] YOU are exhausted at 42.3 s: 0 of 100 after 18 blows this mission - attacks at 20% speed until you rest (refill starts 3.0 s after your last blow)`
- Log (verbose), one line per swing: `[athletics] blow melee (on foot): <you> (you) - cost 5.6 (x0.56: hero party leader), 100.0 → 94.4 of 100`
  … the last one ends `4.4 → 0.0 of 100 - EXHAUSTED`.
- Once per battle, for the FIRST fighter anyone sees exhausted (often an AI soldier):
  - `[speed] first exhaustion this mission: <name> at … s - attack properties before: swing 1.012, thrust/draw 1.012, reload 0.930 → after UpdateAgentProperties: swing 0.202, thrust/draw 0.202, reload 0.186 (x0.20 / x0.20 / x0.20, asked x0.20) - the penalty is in the agent's properties`
  - later `[speed] first exhausted fighter recovers after … s: properties just before - … (x0.20 …; they stayed penalized: yes)`
    and `[speed] first exhausted fighter back to full speed: properties now … (x1.00 / x1.00 / x1.00 …)`.
  - These prove the numbers reached the fighter. Whether the ENGINE honours a 0.2 (it might
    clamp it — never tested before) is what your eyes and the summary's **attack speed
    check** (3i) settle. Values that "did NOT take the asked factor" or "stayed penalized: NO"
    → tell Claude.

**3c. Stop and wait ~3 s.** Right after 3b, stand still and do nothing.
- You see: for about 3 seconds you are still slow; then your attacks are normal again (the
  moment the refill starts, since `ExhaustedRecoverPercent` is 0).
- Log: `[athletics] YOU recovered at 48.4 s: 0.2 of 100 after 3.1 s exhausted - full attack speed again`
  — "after 3.1 s" = the 3.0 s rest plus at most one 0.1 s refill step.

**3d. Stand vs run — compare the refill.** Empty yourself, then stand still for a full
minute. Empty yourself again, then keep running (or ride) until full.
- Log after standing: `[athletics] YOU are back to full at … s: 0 → 100 in 60.0 s of refill (standing 60.0 s, moving 0.0 s; empty to full takes 60 s standing, 120 s moving)`
- Log after running: `… 0 → 100 in 120.0 s of refill (standing 0.0 s, moving 120.0 s; …)`
  (a mix shows both parts; any attack restarts the 3 s and a new refill run).
- Summary: `[summary] Athletics regen: N fighter-seconds standing, M moving; K refills to full`
  — M above 0 proves the moving rule fires (riders count their horse's speed).

**3e. Shoot a bow until empty.** Bow or crossbow, keep shooting (18 shots as leader).
- You see: drawing the bow (or reloading the crossbow) becomes very slow. Javelins and
  throwing axes likewise.
- Log (verbose): `[athletics] blow ranged (on foot): <you> (you) - cost 5.6 (x0.56: hero party leader), …`
- Summary: `Athletics detection: … shots seen N (+0 extra projectiles of the same shot ignored) | ranged releases seen by the poll M | …`
  — N and M close together = both signals agree; and
  `attack speed check, ranged - time between shots: fresh median … | exhausted median … → x… (asked x5.00) - exhausted attacks ARE slower`.

**3f. Ride and swing.** Mount up, swing at enemies from the saddle; then ride around 30 s
without attacking; then (if you have one) couch a lance and hit someone.
- You see: the same slow-down once empty on horseback; riding itself never drains you.
- Log (verbose): `[athletics] blow melee (mounted): …`; a couched hit: `[athletics] blow couched/braced (mounted): …`
  (one per hit; a second hit within 1.5 s is free).
- Summary: `Athletics blows charged: … - by riders N, on foot M` and
  `Athletics detection: melee releases seen X (mounted Y) | … | melee hits by fighters H (on foot …, mounted …): during a counted release A, outside one B [in action: …]`
  — **B should be small next to A.** A large B, above all for riders, means swings are
  slipping past the detector (RESEARCH UNVERIFIED #2) — send the log.

**3g. A companion or lord vs a soldier.** A campaign fight with a companion in your party
(and a lord on the other side).
- Log (verbose): soldiers `cost 10.0`, your companion `cost 7.5 (x0.75: hero)`, you and a
  lord who leads his party `cost 5.6 (x0.56: hero party leader)`.
- Summary: `[summary] Athletics heroes: N flagged, M party leaders (<names>); lowest a hero reached: <name> 12.5 of 100`.
- In an army, only each party's OWN leader gets the leader price (not the army's marshal
  for everyone); a garrison lord in a siege pays the hero price only.

**3h. Change it mid-battle in MCM.** While you (or others) are exhausted: Escape → Options →
Mod Options → Trax Combat Enhancements.
- *Exhausted attack speed (%)* 20 → 50, Done, back to the fight.
  - You see: exhausted attacks now at half speed instead of a fifth.
  - Log: `[config] ExhaustedAttackSpeedPercent: 20 → 50 (source: MCM)` then, on the first
    frame back, `[speed] ExhaustedAttackSpeedPercent now 50%: N exhausted fighters get the new speed on the next tick`.
  - The summary's attack-speed check compares against the value at the END — set it back
    to 20 for a clean reading.
- *Athletics* (its own switch) off.
  - You see: every slow fighter is back to normal speed at once.
  - Log: `[athletics] AthleticsEnabled switched OFF mid-mission: N fighters back to full, M attack-speed penalties lifted (applied on the next tick)`;
    the summary later counts `attacks while Athletics was off`. Turn it on again →
    `[athletics] AthleticsEnabled switched ON mid-mission: everyone starts full`.
- Optional: *Misses cost too* off → swinging at the air is free, only hits cost:
  verbose `blow melee (landed)` / `blow ranged (landed)`; summary `landed-only swings` /
  `landed-only shots`. Optional: *Bar size (points)* 100 → 200 → everyone keeps his share
  (the next blow line says `of 200`).

**3i. The summary.** End the battle. After the damage lines, the `[summary]` block has:
```
[summary] Athletics settings at the end: ON - pool 100, cost per blow 10.0 / hero 7.5 / party leader 5.6, …
[summary] Athletics blows charged: 412 (melee swings 300, shots/throws 100, couched/braced hits 12, landed-only swings 0, landed-only shots 0) - by riders 60, on foot 352; Athletics spent 3890 points
[summary] Athletics detection: melee releases seen 300 (mounted 50) | shots seen 100 (+0 extra projectiles of the same shot ignored) | ranged releases seen by the poll 98 | melee hits by fighters 280 (on foot 240, mounted 40): during a counted release 276, outside one 4 [in action: Other(0) 4]
[summary] Athletics free (never charged): kicks 3, shield bashes 5, kick/bash hits 6, couched hits within one blow-length of the last 2, attacks while Athletics was off 0, releases / shots waiting for a landed hit (misses cost: no) 0 / 0
[summary] Athletics exhaustions: 45 entered, 30 left
[summary] Athletics heroes: 12 flagged, 5 party leaders (you, Derthert, …); lowest a hero reached: Rhagaea 12.5 of 100
[summary] Athletics you: 25 blows, 1 exhaustion, lowest 0.0 of 100
[summary] Athletics your formations at the end: 1 Infantry 72 ± 8 (40 men, 2 exhausted) | 2 Archers 95 ± 3 (20 men)
[summary] Athletics regen: 1234 fighter-seconds standing, 567 moving; 38 refills to full
[summary] attack speed check, melee - time between swings: fresh median 1.35 s, avg 1.52 s (n 250) | exhausted median 6.10 s, avg 6.40 s (n 30) → x4.52 (asked x5.00) - exhausted attacks ARE slower
[summary] attack speed check, melee - swing length (swings that hit nothing): fresh median … | exhausted median … → x… - exhausted attacks ARE slower
[summary] attack speed check, ranged - time between shots: fresh median … | exhausted median … → x… - exhausted attacks ARE slower
[summary] attack speed updates: 75 recomputes asked (UpdateAgentProperties), the decorator applied a penalty in 80 recomputes; 12 intervals spanning a change of state left out
[summary] Athletics tick cost: avg 0.120 ms, max 1.300 ms per tick over 5400 ticks; fighters polled avg 480, max 1020
[summary] speeds for step 5c: on foot walk limit avg 1.80 m/s (n 480), top avg 4.90 m/s (n 480) → walk/top 0.37; horses walk …; refill samples speed/top in tenths …
[summary] Athletics errors: none
```
What proves what:
- **The penalty works in the engine**: the three `attack speed check` lines say
  `exhausted attacks ARE slower` (x3-x5 is right: the AI's thinking time between attacks
  is not slowed, so "time between swings" stays under x5; "swing length" is the purest
  measure). **`are NOT clearly slower … tell Claude`** = the engine clamps the multiplier —
  the one thing only the game can tell. `not enough samples` = too few exhausted attacks;
  fight longer (a big battle gives plenty).
- **Detection**: `outside one` small next to `during a counted release`; `mounted` numbers
  above 0 after riding; `shots seen` ≈ `ranged releases seen by the poll`; kicks and bashes
  listed under **free** (a kick count of 0 with kick/bash hits above 0 only means kicks run
  on another action channel — they are free either way).
- **Heroes and leaders**: the names you expect in `party leaders`; your own blows and
  exhaustions under `Athletics you`.
- **Formations** (steps 8-9 draw these): one entry per formation of yours with men left,
  mean ± spread in points.
- **Cost**: in a 500+ battle `Athletics tick cost` avg should stay well under 1 ms. Above
  2 ms → tell Claude (the poll can move to worker threads).
- **Step 5c's numbers**: the `speeds for step 5c` line (walk limit vs top speed, on foot and
  horses, and how fast refilling fighters really moved) — just send it along.
- **Errors**: `Athletics errors: none`. Otherwise each failed spot fell back to vanilla (no
  cost, no penalty) and the first one per place is an `[error] athletics.…` / `[error] speed.…`
  block with its stack above.

---

## 4. The master switch — the same battle with and without the mod

*Mod enabled* (`ModEnabled`, MCM → *Master switch*, the first key of config.json) turns the
WHOLE mod off, live: no damage rolls, no Athletics costs, no refill, no slow attacks (and,
once they exist, no bars and no step-backs). The log keeps recording, so two battles can be
laid side by side. Switched back on, everyone starts with a full Athletics bar.

**4a. A/B — one custom battle ON, the same one OFF.** Custom battle, the same two armies and
map both times (e.g. 100 v 100 infantry), fight each to the end the same way.
- Run A: *Mod enabled* on. Run B: off BEFORE the battle starts (main menu → Mod Options, or
  `"ModEnabled": false` in config.json).
- Log, run B at the start: `[mission] start: … agents so far …, mod OFF (ModEnabled) - this battle runs as vanilla; the log still records it for comparison`
  and `[damage] mission start: mod OFF (ModEnabled) - no hit is rolled, the game's own numbers are recorded for comparison; with the mod on: damage randomness ON, …`
- Compare the two `[summary]` blocks:
  - headers: run A `…, result: …, mod ON ====`, run B `…, mod OFF ====`, each with its duration;
  - `people removed: N killed, N knocked out, N fled` — how deadly each run was;
  - damage: run A `damage rolls: N hits (…); factor …; damage X → Y (+z%), avg Y/N per hit`,
    run B `damage while the mod was OFF - the game's own numbers, not rolled (factor 1.00): N hits (…); damage X → X (+0.0%), avg … per hit`
    (and `damage rolls: none this mission`) — the two "avg per hit" numbers should be close
    (the roll is fair on average); the hit COUNTS and deaths show how the battle's pace changed;
  - Athletics: run B `Athletics settings at the end: OFF - the whole mod is switched off (ModEnabled) - everyone full, no penalty`,
    no blows charged, no exhaustions.

**4b. Flip it mid-battle.** In one battle, while you or others are exhausted (section 3b):
Escape → Options → Mod Options → *Mod enabled* off → Done → fight ~20 s → back on.
- You see: the moment you are back in the fight, every slow fighter swings at full speed;
  damage numbers stop varying (same blow, same number); after switching on, everyone is fresh.
- Log (config, the moment you click): `[config] ModEnabled: true → false (source: MCM)`, later `… false → true …`.
- Log (the next frame of the battle):
  - `[mission] mod switched OFF (ModEnabled) at 42.3 s - vanilla from now on: no damage rolls (hits are recorded unrolled), no Athletics costs, refill or slow attacks (penalties lifted), no bars`
  - `[athletics] the whole mod (ModEnabled) switched OFF mid-mission: N fighters back to full, M attack-speed penalties lifted (applied on the next tick)`
  - on again: `[mission] mod switched ON (ModEnabled) at 64.0 s - everything back on: …` and
    `[athletics] the whole mod (ModEnabled) switched ON mid-mission: everyone starts full`
  - verbose: hits while off `[damage] not rolled - mod OFF (ModEnabled): melee on a person: …, 34`
- Summary header: `…, mod was on for 70% of the battle (started ON; OFF at 42.3 s, ON at 64.0 s) ====`,
  and both damage lines plus `damage avg per hit: rolled (mod ON) … | mod OFF …`.
- Leave it ON afterwards (it is saved in config.json like any setting).

---

## 5. Defaults — defaults.json, "Revert all to defaults", "Save current values"

Every default the mod ships lives in ONE file, `defaults.json` in the repo, built into the
mod. The first-run config.json, MCM's Reset and *Revert all to defaults* all use it.

**5a. The defaults loaded.** Any start.
- Log: `[config] defaults: defaults.json (embedded in TraxCombat.Core.dll), 33 keys for 33 settings - every default read from it`
  (no `defaults.json PROBLEM:` lines).

**5b. Revert all to defaults — mid-battle.** In a battle, change two or three settings in MCM
(e.g. *Spread (± %)* 50 → 20, *Exhausted attack speed (%)* 20 → 60), Done, fight a little;
then Escape → Mod Options → *Defaults* → **Revert all to defaults**.
- You see: the page's sliders jump back to the defaults at once, a green line *"Trax Combat
  Enhancements: every setting is back to its default (2 changed) - applied now; config.json
  saved."*; back in the fight, damage spreads ±50% again and exhausted fighters are at 20%.
- Log:
  - `[mcm] "Revert all to defaults" pressed`
  - one line per setting that moved: `[config] DamageRandomPercent: 20 → 50 (source: defaults)`
  - `[config] wrote config.json (reverted to defaults): every value as it is in effect now`
  - `[config] reverted all 33 settings to their defaults (defaults.json (embedded in TraxCombat.Core.dll)): 2 changed, applied live`
  - then, on the next frame, the features' own lines (e.g. `[speed] ExhaustedAttackSpeedPercent now 20%: …`).
- config.json now holds every default (a hand edit waiting in it is overwritten too — the
  revert means "everything"). MCM's Cancel does not undo a revert.

**5c. Save current values as a defaults file.** Tune a few settings in MCM, then *Defaults* →
**Save current values as a defaults file**.
- You see: a green line with the path — `…\Configs\TraxCombatEnhancements\defaults.json`.
- Log: `[mcm] "Save current values as a defaults file" pressed` and
  `[config] saved the current values as a defaults file: <path> - 2 differ from the built-in defaults: DamageRandomPercent 35 (built-in 50), ShowPlayerBar false (built-in true). Copy it over the repo's defaults.json to make these the defaults.`
- The file opens in any editor: the same header and `//` explanations as the repo's
  defaults.json, your values. Nothing in the game changed.
- To make them the mod's defaults: copy it over `trax_combat_enhancements\defaults.json` and
  hand it to Claude (or build and deploy yourself — the tests check it).

**5d. A changed default reaches the game** (Claude does this part, or Anton after 5c):
change one value in the repo's `defaults.json` (e.g. `"DamageRandomPercent": 40`), build and
deploy, then:
- delete config.json (or rename it) and start: the fresh file says `"DamageRandomPercent": 40`
  with `(default 40, range 0 to 100)` above it; the log says `[config]   DamageRandomPercent = 40`;
- MCM: the hint of *Spread (± %)* ends `Default: 40.`; the page's Reset and *Revert all to
  defaults* both put it at 40;
- delete just that key's line from config.json → at the next battle start it comes back as 40
  (`[config] not in the file, default used: DamageRandomPercent`).

(steps below are added as the features land)

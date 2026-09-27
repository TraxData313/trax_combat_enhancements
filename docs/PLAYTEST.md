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
  - `[mcm] settings page registered at main menu (attempt 1): MCM 5.…, page "Trax Combat Enhancements", 38 settings in 8 groups, …, Default preset = the mod's defaults (defaults.json); group "Defaults": buttons "Revert all to defaults" and "Save current values as a defaults file".`
    (`at retry (attempt N)` is fine too — MCM was just slow to wake)
  - `[config] defaults: defaults.json (embedded in TraxCombat.Core.dll), 38 keys for 38 settings - every default read from it`
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
  sections (Master switch, Damage randomness, Athletics, Tired fighters, Regeneration, Bars - you
  and your target, Bars - your squads, Advanced) and above EVERY key a `//` explanation in
  plain words ending `(default …, range … to …)`. The defaults are the ones in the mod's
  `defaults.json` (section 5).
- Log, first run only: `[config] first run: created config.json with every default and a plain-words explanation beside each value`
- Log, every start: `[config] settings in effect (38, version 0):` followed by 38 lines like
  `[config]   DamageRandomPercent = 50` — a value you changed shows `(default 50)` after it.

**1d. The MCM page.** Main menu → Options → Mod Options → *Trax Combat Enhancements*.
- You see: the same 8 groups, 38 settings — *Master switch* (Mod enabled) on top — checkboxes
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

**1f. A hand edit is picked up at the next battle.** Alt-Tab, change `"AthleticsPoolFloor": 50` to
`150` in config.json, save, start (or enter) the next battle.
- Log: `[config] AthleticsPoolFloor: 50 → 150 (source: file)` and
  `[config] config.json re-read at mission start: 1 change(s), settings version N`
- Every mission start writes `[config] config.json re-read at mission start: no changes`
  when nothing changed.
- **The rewrite rule** (optional, 1 minute): hand-edit one value, then — BEFORE the next
  battle — change a DIFFERENT value in MCM and press Done. The hand edit must still be in the
  file. Log: `… kept hand edit(s) from the file that apply at the next battle start: AthleticsPoolFloor = 150 (now 50)`.
  Put it back to 50 afterwards (section 3 counts blows with the floor at 50).
- Mistakes are safe: a typo'd key → `[config] file problem: "AthleticsPoolFlor" is not a setting of this version - ignored (typo?)`
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
  `[damage] mission start: damage randomness ON, spread ±50% (a 50-damage hit lands for 25-75), melee on, ranged on, on mounts on, on shields off, upside follows the attacker's Athletics (DamageBonusFollowsAthletics) on - read live on every hit`
  (the upside part is section 3's — 3g)
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

Every fighter — you, your men, the enemy — has an Athletics bar **as big as his Athletics
skill** (character screen; troops from their troop data), but never under 50 points. Every
attack costs points: a swing or thrust when it starts, a shot or throw when it leaves the
hand, a couched lance or braced spear when it hits — 10 for a common soldier, 7.5 for a hero
(lord, companion), 5.6 for the hero who leads his own party (you, a lord), whatever the size
of his bar. Kicks, shield bashes, blocking, running and riding are free.

**The top quarter of his OWN bar is full strength** (the peak line, 75%). Below it three
things fall in straight lines as the bar empties:
- the lucky side of his damage roll (+50% at the line, +25% halfway down, none at empty — the
  unlucky −50% side never changes),
- his attack speed — wind-up, strike, bow draw, crossbow reload, throws — down to 20% at empty,
- his run speed on foot, down to x0.30 at empty (horses keep their speed).
At 0 he is **exhausted**. Wounds cap the bar: at 60% health only 60% of it can be used, and
the peak line does not move, so a badly wounded man never gets back to full strength. About
3 s after his last attack the bar refills — empty to full in 60 s standing or walking, half
that fast running flat out, never above a wound's cap.

What to expect, blow by blow (DESIGN §2):

| Fighter | Athletics skill | Bar | A blow costs | Blows at full strength | Blows to empty |
|---|---|---|---|---|---|
| Recruit | 20 | 50 (the floor) | 10 | 2 | 5 |
| Tier-2 soldier (infantryman) | 40 | 50 (the floor) | 10 | 2 | 5 |
| Elite cataphract | 60 | 60 | 10 | 2 | 6 |
| Legionary | 130 | 130 | 10 | 4 | 13 |
| Fian champion | 170 | 170 | 10 | 5 | 17 |
| You as general in a custom battle (commanders have 80–90) | 90 | 90 | 5.6 | 5 | 16 |
| A 300-skill party leader | 300 | 300 | 5.6 | 14 | 54 |

Set up once: `VerboseLogging` on. The easiest ground is a **custom battle**: you as general
(the leader price), your side **Imperial Legionaries**, the enemy **Imperial Recruits** (or
looters). One campaign fight with a companion covers the hero price. No bar on screen yet
(steps 6-9) — the log is the readout, and your eyes for the speeds.

**3a. The engine is on — and everyone's bar.** Start the battle.
- Log, at the start:
  - `[athletics] mission start: ON - pool = the Athletics skill x1.00, at least 50; full strength at 75% of the pool and above; cost per blow 10.0 / hero 7.5 / party leader 5.6 points, misses cost: yes; when empty: attacks at 20%, run x0.30, horses x1.00 (never slowed); damage upside follows Athletics: yes; wounds cap the pool: yes; refill after 3.0 s rest: empty to full in 60 s at a walk or slower (up to 0.40 of top speed), x0.50 at a full run - read live`
  - `[athletics] party-leader rule: …` (custom battle: `no campaign (custom battle) - the side's general, or every hero of a side without one`)
  - `[speed] stat model on top in this mission: ours, over <the game's model> - the attack-speed, run-speed and horse-speed penalties are applied on every recompute`
    — **if it says `WARNING: … not ours`**, another mod took the slot: tell Claude.
  - `[athletics] party leader: <name> - Athletics skill 90 → pool 90 (full strength down to 68), pays x0.56 per blow (5.6 now)` — one per leader.
  - **`[athletics] YOU: Athletics skill 90 → pool 90 (the skill x1.00, at least 50); full strength down to 68 (75%); a blow costs you 5.6 - about 5 blows at full strength, 16 to empty`**
    — note YOUR two numbers; 3b checks them. The skill must match your character screen.
  - `[athletics] first tick: tracking N fighters`
- Log (verbose), one line per soldier at spawn (rate-limited — a sample):
  `[athletics] pool at spawn: Imperial Recruit - Athletics skill 20 → pool 50 (the floor); 2 blows at full strength, 5 to empty`
  and `[athletics] pool at spawn: Imperial Legionary - Athletics skill 130 → pool 130; 4 blows at full strength, 13 to empty`.
  A `(not readable)` after the skill = the character could not be read → tell Claude.

**3b. You: full strength, then weaker every swing, then empty.** Swing continuously at the
air (misses cost too), counting.
- You see: the first ~5 swings at normal speed; from then on every swing a little slower;
  at empty (~16) a wind-up and strike take about five times as long. Try to run: at empty you
  run at about a third of your normal pace. Blocking is normal.
- Log (always):
  - `[athletics] YOU dropped below full strength at … s: 61.9 of 90 (the line is 68) after 5 blows this mission - f 0.92: attacks x0.93, run x0.94, damage upside 92% of the full`
  - `[athletics] YOU are exhausted at … s: 0 of 90 after 16 blows this mission - attacks at 20% speed, run x0.30, no damage upside until you rest (refill starts 3.0 s after your last blow)`
- Log (verbose), one line per swing with f (the share of your peak line left):
  `[athletics] blow melee (on foot): <you> (you) - cost 5.6 (x0.56: hero party leader), 90.0 → 84.4 of 90 (f 1.00 → 1.00)`,
  the 5th `… 67.5 → 61.9 of 90 (f 1.00 → 0.92) - below full strength`,
  the 16th `5.6 → 0.0 of 90 (f 0.08 → 0.00) - EXHAUSTED`; and lines like `[speed] <you> (you): attacks x0.93, run x0.94 (f 0.92)`
  each time your speeds move by a 0.05 step or more.
- Once per battle, for the FIRST fighter anyone sees empty (often an AI soldier):
  - `[speed] first exhaustion this mission: <name> at … s - properties before: swing …, thrust/draw …, reload …, run … (while attacks x0.41, run x0.49 applied) → after UpdateAgentProperties: … (x0.49 / x0.49 / x0.49, run x0.62; asked attacks x0.20 / x0.41 = x0.49, run x0.30 / x0.49 = x0.62) - the penalties are in the agent's properties`
  - later `[speed] first exhausted fighter leaves 0 after … s: properties just before - … (x0.20 / x0.20 / x0.20, run x0.30 of his fresh values; they stayed penalized: yes) - they now climb with his bar`
  - and `[speed] first exhausted fighter back at full strength: properties now … (x1.00 / x1.00 / x1.00, run x1.00 of his fresh values …)`.
  - These prove the numbers reached the fighter. Whether the ENGINE honours them is what your
    eyes and the summary's **attack speed check** and **run speed check** (3m) settle.
    "did NOT take the asked factors" or "stayed penalized: NO" → tell Claude.

**3c. Recruits run dry, legionaries keep going.** Let the lines meet and watch.
- You see: recruits slow down after a handful of swings; legionaries keep swinging at full
  speed much longer; exhausted men visibly lag behind fresh ones when a formation moves.
- Log (verbose, samples): a recruit's second swing `… 40.0 → 30.0 of 50 (f 1.00 → 0.80) - below full strength`,
  his fifth `10.0 → 0.0 of 50 (f 0.27 → 0.00) - EXHAUSTED`; a legionary's fourth
  `100.0 → 90.0 of 130 (f 1.00 → 0.92) - below full strength`, his 13th `- EXHAUSTED`.
- Summary: `Athletics pools (the Athletics skill x1.00, at least 50; settings at the end): N fighters - min 50 / avg … / max 130; K at the floor; you 90 (skill 90); party leaders: …`
  — K = the troops under skill 50 (all the recruits). `whose skill could not be read` above 0 → tell Claude.

**3d. Rest — the bar and the speeds climb back.** Right after 3b, stand still.
- You see: about 3 s later your attacks and your run begin to recover — gradually, not at
  once; after ~45 s of rest (the peak line is 75% of a 60 s refill) you are at full strength.
- Log (always):
  - `[athletics] YOU are off empty at … s: 0.2 of 90 after 3.1 s at 0 - attacks and run speed now climb with your bar (full at 68)` (3.1 s = the 3 s rest + one 0.1 s refill step)
  - `[athletics] YOU are back at full strength at … s: 67.6 of 90 (the line is 68)`
  - `[athletics] YOU are back to full at … s: 0 → 90 of 90 in 60.0 s of refill (at a walk or slower 60.0 s, faster 0.0 s; avg rate x1.00; empty to full takes 60 s at rest, 120 s at a full run)`

**3e. Walk vs run — the refill follows your effort.** Empty yourself, then WALK (the walk
key) until full. Empty yourself again, then RUN flat out (circles are fine) until full.
- Log after walking: `… in 60.0 s of refill (at a walk or slower 60.0 s, faster 0.0 s; avg rate x1.00; …)`
- Log after running: `… in ~120 s of refill (… faster ~120 s; avg rate x0.5…)` — avg rate near x0.50 = flat
  out the whole time; a mix of paces lands in between. Any attack restarts the 3 s and a new refill.
- Summary:
  - `Athletics regen: N fighter-seconds refilling - at a walk or slower (effort up to 0.40) A s at the full rate, faster B s at avg x…; refills to the top: … to full, … to a wound's cap`
  - `Athletics refill effort (speed ÷ current top speed), seconds per tenth (0-0.1 … 0.9-1, above 1): …` — where the
    refilling time really went (standing near 0, a formation's walk near 0.4, a run near 1).
  - `walk vs run speeds (tune WalkEffortFraction, now 0.40): on foot walk limit avg 1.80 m/s (n …), top avg … m/s (n …) → walk/top …; horses walk …, top … → walk/top …`
    — **this line tunes `WalkEffortFraction`**: the game walks people at 1.8 m/s; if walk/top on
    foot comes out well above 0.40 (say 0.5), walking troops refill slower than meant — tell
    Claude the number (the setting should sit at, or a little above, that ratio).

**3f. Tired men run slower, horses do not.** Watch an exhausted soldier try to keep up, and
run yourself when empty (3b).
- You see: empty men move at about a third of their pace; fresh ones overtake them. Riders
  on tired horses? No — horses keep their speed (`MountMinSpeedMultiplier` 1.0).
- Summary:
  - `run speed check, on foot (÷ the fighter's own top speed when fresh), by f: peak (f 1) engine top x1.00 asked x1.00, moving p90 x… max x… (n …) | f 0.5-1 engine top x0.8… asked x0.8… | f below 0.5 … | empty (f 0) engine top x0.30 asked x0.30, … - the engine's top speed follows the curve`
    — "engine top" ≈ "asked" in every row = the engine took the run penalty. **`does NOT follow the asked curve` → tell Claude**
    (the engine may read another speed property). `moving p90` falling from row to row = tired men really ran slower.
  - `run speed check, horses (…), by the rider's f: MountMinSpeedMultiplier 1.00 = horses never slow - … - unaffected, as asked`
- Optional, horses: *Horse speed when the rider is empty (x)* 1.0 → 0.5, ride and swing until
  empty — your horse slows. Log: `[speed] first horse slowed this mission: the horse of <you> (you) - MountSpeed … → … after UpdateAgentProperties (asked x0.50 of its fresh speed; MountMinSpeedMultiplier 0.50)`;
  the horses' summary line then ends `- the engine's top speed follows the curve`. Set it back to 1.0.

**3g. The damage upside shrinks with the attacker's bar.** Fight looters fresh, then keep
fighting while tired, then empty.
- You see: fresh, the same blow lands anywhere from −50% to +50%; tired, the big numbers stop
  coming; empty, never above the game's normal damage (only lower).
- Log (verbose): `[damage] melee on a person: <you> (you) → Looter, <weapon>, 40 → 52 (x1.30, attacker f 1.00 → up to x1.50)`,
  tired `… (x…, attacker f 0.40 → up to x1.20)`, empty `… (x…, attacker f 0.00 → up to x1.00)`;
  a horse charge reads the rider's f.
- Summary: `damage upside by the attacker's Athletics (f = the share of his peak line left; upside = how much of the +p he was allowed): peak (f 1) N hits avg x1.0… max x1.50 upside 100% | f 0.5-1 … max x1.3… upside 7…% | f below 0.5 … | empty (f 0) … max x1.00 upside 0% | no pool (attacker not tracked) …; rolls above their allowed top: 0`
  — the `max` falls row by row; **`rolls above their allowed top` must be 0**.
- Optional A/B: *Damage upside follows Athletics* off → every row says `upside 100%`.

**3h. Get wounded — the cap.** Take a few hits (fight without armour, or stand in front of archers).
- You see: after a bad wound you stay weaker even rested — at 60% health your bar can only
  refill to 60%, below the 75% line, so your attacks and your run stay a little slow.
- Log (always): `[athletics] YOU are wounded at … s (62% health): Athletics capped at 55.8 of 90 (cut 11.2) - f now 0.83; full strength needs 68, out of reach until healed`
  (a cut happens only when you held more than the health left), later
  `[athletics] YOU are refilled to the wound's cap (62%) at … s: … → 56 of 90 in … s of refill (…)`.
- Log (verbose): `[athletics] health cap: <name> at 40% health - Athletics 90.0 → 52.0 of 130 (f 0.53)`.
- Summary: `Athletics health cap: N cuts (a wound pulled Athletics down to the health left), biggest … points, … points in all`
  and `refills to the top: … to full, M to a wound's cap`.

**3i. Shoot a bow until empty.** Bow or crossbow, keep shooting.
- You see: each draw (or crossbow reload) a little slower below the line, very slow at empty.
  Javelins and throwing axes likewise.
- Log (verbose): `[athletics] blow ranged (on foot): <you> (you) - cost 5.6 (x0.56: hero party leader), … (f … → …)`
- Summary: `Athletics detection: … shots seen N (+0 extra projectiles of the same shot ignored) | ranged releases seen by the poll M | …`
  — N and M close together = both signals agree; and
  `attack speed check, ranged - time between shots, by f: peak (f 1) median … | … | empty (f 0) median … x… (asked x5.00, n …) - tired attacks ARE slower (…)`.

**3j. Ride and swing.** Mount up, swing at enemies from the saddle; then ride around 30 s
without attacking; then (if you have one) couch a lance and hit someone.
- You see: the same curve on horseback; riding itself never drains you.
- Log (verbose): `[athletics] blow melee (mounted): …`; a couched hit: `[athletics] blow couched/braced (mounted): …`
  (one per hit; a second hit within 1.5 s is free).
- Summary: `Athletics blows charged: … - by riders N, on foot M` and
  `Athletics detection: melee releases seen X (mounted Y) | … | melee hits by fighters H (on foot …, mounted …): during a counted release A, outside one B [in action: …]`
  — **B should be small next to A.** A large B, above all for riders, means swings are
  slipping past the detector (RESEARCH UNVERIFIED #2) — send the log.

**3k. A companion or lord vs a soldier.** A campaign fight with a companion in your party
(and a lord on the other side).
- Log: `[athletics] party leader: <lord> - Athletics skill … → pool …` for each leader; verbose
  `[athletics] hero: <companion> - Athletics skill … → pool …, pays x0.75 per blow (7.5 now)`.
- Log (verbose): soldiers `cost 10.0`, your companion `cost 7.5 (x0.75: hero)`, you and a
  lord who leads his party `cost 5.6 (x0.56: hero party leader)`.
- Summary: `[summary] Athletics heroes: N flagged, M party leaders (<names>); lowest a hero reached: <name> 12.5 of 120`.
- In an army, only each party's OWN leader gets the leader price (not the army's marshal
  for everyone); a garrison lord in a siege pays the hero price only.

**3l. Change it mid-battle in MCM.** While some fighters are tired: Escape → Options →
Mod Options → Trax Combat Enhancements. Every change applies on the first frame back.
- *Tired fighters* → *Attack speed when empty (%)* 20 → 50.
  - You see: empty men attack at half speed instead of a fifth; tired ones a bit faster.
  - Log: `[config] ExhaustedAttackSpeedPercent: 20 → 50 (source: MCM)`, then
    `[speed] speed settings now: when empty attacks at 50%, run x0.30, horses x1.00; full strength at 75% of the pool - N fighters get new speeds over the next ticks (at most 50 recomputes a tick)`.
  - The summary's checks compare with what was applied — set it back to 20 for a clean reading.
- *Athletics* → *Smallest bar (points)* 50 → 100.
  - Log: `[athletics] pool settings now: the Athletics skill x1.00, at least 100 - pools now min 100 / avg … / max …, … at the floor; everyone keeps his share (a fighter at 60% stays at 60%)`;
    a recruit's next blow line says `of 100`. Set it back to 50.
- *Full strength above (% of the bar)* 75 → 50: more men count as full strength at once (the
  `[speed] speed settings now: … full strength at 50% …` line). Set it back to 75.
- *Athletics* (its own switch) off.
  - You see: every slow fighter is back to normal at once.
  - Log: `[athletics] AthleticsEnabled switched OFF mid-mission: N fighters back to full, M speed penalties lifted (applied over the next ticks)`;
    the summary later counts `attacks while Athletics was off`. On again →
    `[athletics] AthleticsEnabled switched ON mid-mission: everyone starts full (a wound's cap applies again at the next refill step)`.
- Optional: *Misses cost too* off → swinging at the air is free, only hits cost:
  verbose `blow melee (landed)` / `blow ranged (landed)`; summary `landed-only swings` /
  `landed-only shots`.

**3m. The summary.** End the battle. After the damage lines, the `[summary]` block has
(numbers made up):
```
[summary] Athletics settings at the end: ON - pool = the Athletics skill x1.00, at least 50; full strength at 75% of the pool and above; …
[summary] Athletics pools (the Athletics skill x1.00, at least 50; settings at the end): 400 fighters - min 50 / avg 88.5 / max 130; 200 at the floor; you 90 (skill 90); party leaders: you 90, Arcor 80
[summary] Athletics blows charged: 412 (melee swings 300, shots/throws 100, couched/braced hits 12, landed-only swings 0, landed-only shots 0) - by riders 60, on foot 352; Athletics spent 3890 points
[summary] Athletics detection: melee releases seen 300 (mounted 50) | shots seen 100 (+0 extra projectiles of the same shot ignored) | ranged releases seen by the poll 98 | melee hits by fighters 280 (on foot 240, mounted 40): during a counted release 276, outside one 4 [in action: Other(0) 4]
[summary] Athletics free (never charged): kicks 3, shield bashes 5, kick/bash hits 6, couched hits within one blow-length of the last 2, attacks while Athletics was off 0, releases / shots waiting for a landed hit (misses cost: no) 0 / 0
[summary] Athletics exhaustions (empty, f 0): 45 entered, 30 left; the peak zone: left 380 times (a blow took a fighter below his line), re-entered 150 times (by refill)
[summary] Athletics fighter-time by f (the share of his peak line left): peak (f 1) 71.0%, f 0.5-1 16.0%, f below 0.5 9.0%, empty (f 0) 4.0% of 52000 fighter-seconds
[summary] Athletics heroes: 2 flagged, 2 party leaders (you, Arcor); lowest a hero reached: Arcor 12.5 of 80
[summary] Athletics you: skill 90 → pool 90; 25 blows, 1 exhaustion, lowest 0.0 of 90
[summary] Athletics your formations at the end: 1 Infantry 72 ± 8 (40 men, 2 exhausted) f avg 0.81, 22 at full strength | 2 Archers 95 ± 3 (20 men) f avg 1.00, 20 at full strength
[summary] Athletics health cap: 120 cuts (a wound pulled Athletics down to the health left), biggest 60.0 points, 2400 points in all
[summary] Athletics regen: 9000 fighter-seconds refilling - at a walk or slower (effort up to 0.40) 7000 s at the full rate, faster 2000 s at avg x0.71; refills to the top: 38 to full, 12 to a wound's cap
[summary] Athletics refill effort (speed ÷ current top speed), seconds per tenth (0-0.1 … 0.9-1, above 1): 5200 300 400 900 200 150 150 200 400 900 200, max 1.30
[summary] attack speed check, melee - time between swings, by f: peak (f 1) median 1.35 s (n 250) | f 0.5-1 median 1.55 s x1.15 (asked x1.20, n 180) | f below 0.5 median 2.60 s x1.93 (asked x2.10, n 90) | empty (f 0) median 6.10 s x4.52 (asked x5.00, n 30) - tired attacks ARE slower (empty (f 0) vs the peak)
[summary] attack speed check, melee - swing length (swings that hit nothing), by f: … - tired attacks ARE slower (…)
[summary] attack speed check, ranged - time between shots, by f: … - tired attacks ARE slower (…)
[summary] speed updates: 900 recomputes asked (UpdateAgentProperties: fighters 900, horses 0; a change below x0.05 waits; 0 held a tick by the per-tick budget), the decorator applied attack penalties in 1200 recomputes, run penalties in 1200, horse penalties in 0; intervals left out as their two ends fell in different f bins: melee 40, swing lengths 60, ranged 5
[summary] run speed check, on foot (÷ the fighter's own top speed when fresh), by f: peak (f 1) engine top x1.00 asked x1.00, moving p90 x0.95 max x1.10 (n 30000) | … | empty (f 0) engine top x0.30 asked x0.30, moving p90 x0.30 max x0.35 (n 900) - the engine's top speed follows the curve
[summary] run speed check, horses (÷ the horse's own top speed while its rider was fresh), by the rider's f: MountMinSpeedMultiplier 1.00 = horses never slow - peak (f 1) engine top x1.00 asked x1.00, … - unaffected, as asked
[summary] walk vs run speeds (tune WalkEffortFraction, now 0.40): on foot walk limit avg 1.80 m/s (n 480), top avg 4.50 m/s (n 480) → walk/top 0.40; horses walk …, top … → walk/top …
[summary] Athletics tick cost: avg 0.120 ms, max 1.300 ms per tick over 5400 ticks; fighters polled avg 480, max 1020
[summary] Athletics errors: none
```
What proves what:
- **Pools from the skill**: min / avg / max and `at the floor` fit the troops you brought;
  your pool = your Athletics skill; each leader's pool = his skill (character screen).
- **The peak zone**: `fighter-time by f` — most time at full strength in a short fight, more
  below the line in a long one; `the peak zone: left N times` > 0.
- **Attack speed follows the curve in the engine**: the three `attack speed check` lines —
  each row's x close to its asked x (x1.1-1.2 just below the line, ~x2 below half, x3-x5
  empty: the AI's thinking time between attacks is not slowed, so "time between swings"
  stays under the asked; "swing length" is the purest measure) and `tired attacks ARE
  slower`. **`are NOT clearly slower … tell Claude`** = the engine clamps the multiplier.
  `not enough samples` = too few tired attacks; fight longer.
- **Run speed follows the curve**: `run speed check, on foot … - the engine's top speed follows the curve`,
  `moving p90` falling row by row. **`does NOT follow` → tell Claude.** Horses: `unaffected, as asked`.
- **Damage upside**: the `max` falls row by row; `rolls above their allowed top: 0`.
- **Health cap**: `cuts` above 0 after a real fight; `to a wound's cap` refills.
- **Regen by effort**: `at a walk or slower … faster … at avg x…` and the effort tenths; the
  **`walk vs run speeds`** ratio is the number that tunes `WalkEffortFraction` (send it along).
- **Recomputes stay cheap**: `speed updates` in the hundreds or low thousands for a big battle;
  `held a tick by the per-tick budget` 0 or small; `Athletics tick cost` avg well under 1 ms in
  a 500+ battle (above 2 ms → tell Claude).
- **Detection**: `outside one` small next to `during a counted release`; `mounted` numbers
  above 0 after riding; `shots seen` ≈ `ranged releases seen by the poll`; kicks and bashes
  listed under **free** (a kick count of 0 with kick/bash hits above 0 only means kicks run
  on another action channel — they are free either way).
- **Formations** (steps 8-9 draw these): one entry per formation of yours with men left,
  mean ± spread in points, the men's average f and how many are at full strength.
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
  - `[athletics] the whole mod (ModEnabled) switched OFF mid-mission: N fighters back to full, M speed penalties lifted (applied over the next ticks)`
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
- Log: `[config] defaults: defaults.json (embedded in TraxCombat.Core.dll), 38 keys for 38 settings - every default read from it`
  (no `defaults.json PROBLEM:` lines).

**5b. Revert all to defaults — mid-battle.** In a battle, change two or three settings in MCM
(e.g. *Spread (± %)* 50 → 20, *Attack speed when empty (%)* 20 → 60), Done, fight a little;
then Escape → Mod Options → *Defaults* → **Revert all to defaults**.
- You see: the page's sliders jump back to the defaults at once, a green line *"Trax Combat
  Enhancements: every setting is back to its default (2 changed) - applied now; config.json
  saved."*; back in the fight, damage spreads ±50% again and exhausted fighters are at 20%.
- Log:
  - `[mcm] "Revert all to defaults" pressed`
  - one line per setting that moved: `[config] DamageRandomPercent: 20 → 50 (source: defaults)`
  - `[config] wrote config.json (reverted to defaults): every value as it is in effect now`
  - `[config] reverted all 38 settings to their defaults (defaults.json (embedded in TraxCombat.Core.dll)): 2 changed, applied live`
  - then, on the next frame, the features' own lines (e.g. `[speed] speed settings now: when empty attacks at 20%, …`).
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
